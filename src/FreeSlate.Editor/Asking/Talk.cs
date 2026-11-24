using System.Globalization;
using System.Text;
using System.Text.Json;
using FreeSlate.Editing;
using FreeSlate.Editor.Controls;
using FreeSlate.Skia;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// A talk with Claude about a place of the document (see Focus), begun by a wish typed at the gesture ring: the document, the focus and
/// the wish go to Claude, which writes into the document with its tools as its answer streams in (see Writing), looks at pages, or
/// answers in words; further wishes go on in the same talk. All its changes stay a preview until the user settles them: kept as one step
/// to undo, or taken back. What the ring shows beside it (Line, Answer) follows the talk.
/// </summary>
public sealed class Talk : GestureRing.IAsked
{
    /// <summary>How many rounds of tools a wish may take before the talk stops it.</summary>
    const int MostRounds = 40;

    readonly EditSession editing;
    readonly DocumentView sheets;
    readonly Focus focus;
    readonly IClaudeLink link;
    readonly Func<string, string> text;
    readonly string language;

    /// <summary>The conversation, begun with the first wish (where Claude cannot be reached so, the line says why).</summary>
    IClaudeConversation? conversation;

    /// <summary>The document's objects as Claude read them with the first wish, kept as it writes (see Objects).</summary>
    Objects? objects;

    /// <summary>How many steps the session had done before the talk: its changes are all after them.</summary>
    readonly int before;

    CancellationTokenSource? cancel;
    bool begun, settled;

    /// <summary>
    /// A talk about a place, Claude reached by a link; <paramref name="text"/> gives the texts of the interface by their keys,
    /// <paramref name="language"/> names the interface's language for Claude.
    /// </summary>
    public Talk(EditSession editing, DocumentView sheets, IClaudeLink link, Focus focus, Func<string, string> text, string language)
    {
        (this.editing, this.sheets, this.link, this.focus, this.text, this.language) = (editing, sheets, link, focus, text, language);
        before = editing.Steps;
    }

    /// <summary>What Claude is doing, while it works; else its words where they are short, or nothing.</summary>
    public string Line { get; private set; } = "";

    /// <summary>Claude's words in its last answer, null before it said any.</summary>
    public string? Answer { get; private set; }

    public bool Working { get; private set; }

    /// <summary>Whether the talk changed the document so far.</summary>
    public bool Changed => editing.Steps > before;

    public event Action? Updated;

    /// <summary>The talk settled: kept or taken back.</summary>
    public event Action? Settled;

    /// <summary>A wish: the first one begins the talk with the document and the focus; later ones go on with it.</summary>
    public void Send(string wish)
    {
        if (Working || settled || wish.Trim().Length == 0)
            return;
        _ = Run(wish.Trim());
    }

    /// <summary>The changes kept as one step to undo, or all taken back; Claude stopped where it is still at work.</summary>
    public void Settle(bool keep)
    {
        if (settled)
            return;
        settled = true;
        cancel?.Cancel();
        sheets.Edit(() =>
        {
            if (!keep)
            {
                while (editing.Steps > before && editing.TakeBack())
                {
                }
            }
            // Kept: one step, the cursor after what was written, nothing selected; where nothing changed (a question), the selection as it was.
            else if (Changed)
            {
                editing.JoinSince(before);
                editing.Place(editing.Selection.End);
            }
        });
        Working = false;
        Settled?.Invoke();
    }

    async Task Run(string wish)
    {
        Working = true;
        Answer = null;
        Say("AskThinking");
        cancel = new CancellationTokenSource();
        var token = cancel.Token;
        try
        {
            List<ClaudePart> parts = begun ? [new TextPart("A further wish about the same place: " + wish)] : First(wish);
            begun = true;
            conversation ??= link.Begin(Prompt.System, Prompt.Tools);
            for (var round = 0; round < MostRounds && !settled; round++)
            {
                var results = new List<ClaudePart>();
                var writes = new Dictionary<string, Writing>(StringComparer.Ordinal);
                var answer = new StringBuilder();
                var turnBefore = editing.Steps;
                var stop = "end_turn";
                string? explanation = null;
                await foreach (var happened in conversation.Turn(parts, token))
                {
                    if (settled)
                        return;
                    switch (happened)
                    {
                        case Thinking:
                            Say("AskThinking");
                            break;
                        case TextGrew grew:
                            answer.Append(grew.Text);
                            Answer = answer.ToString().Trim();
                            Say("AskAnswering");
                            break;
                        case ToolBegan { Name: "write" } began:
                            writes[began.Id] = new Writing(editing, sheets, focus, objects!);
                            Say("AskWriting");
                            break;
                        case ToolBegan { Name: "picture" or "tone" or "page" or "comment" }:
                            Say("AskWriting");
                            break;
                        case ToolBegan { Name: "look" }:
                            Say("AskLooking");
                            break;
                        case ToolGrew grew when writes.TryGetValue(grew.Id, out var writing):
                            writing.Grew(grew.Json);
                            break;
                        case ToolCalled called:
                            results.Add(await Use(called, writes));
                            break;
                        case FellBack:
                            // What came before the fallback is not read on: its writes go too.
                            TakeBackTo(turnBefore);
                            (results, answer) = ([], new StringBuilder());
                            writes.Clear();
                            Answer = null;
                            break;
                        case TurnEnded ended:
                            (stop, explanation) = (ended.StopReason, ended.Explanation);
                            break;
                    }
                    Updated?.Invoke();
                }
                if (stop == "refusal")
                {
                    TakeBackTo(turnBefore);
                    Answer = explanation is { Length: > 0 } why ? text("AskRefused") + " " + why : text("AskRefused");
                    break;
                }
                if (results.Count == 0)
                {
                    if (stop == "max_tokens")
                        Answer = (Answer is { Length: > 0 } said ? said + " " : "") + text("AskCut");
                    break;
                }
                parts = results;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e) when (!settled)
        {
            Answer = string.Format(CultureInfo.CurrentCulture, text("AskFailed"), Short(e.Message));
        }
        finally
        {
            Working = false;
            Line = "";
            if (!settled)
            {
                if (Answer is not { Length: > 0 })
                    Answer = Changed ? text("AskDone") : null;
                Updated?.Invoke();
            }
        }
    }

    /// <summary>The first message: the document in the notation, the focus told, the interface's language, the wish; a picture in the focus as it is.</summary>
    List<ClaudePart> First(string wish)
    {
        var document = editing.Document;
        objects = new Objects(document);
        var message = new StringBuilder();
        message.Append("<document>\n").Append(Reading.Document(document, objects)).Append("</document>\n\n");
        message.Append("<focus>\n").Append(Reading.Focus(document, focus, objects)).Append("\n</focus>\n\n");
        message.Append("The user's interface is in ").Append(language).Append(".\n\n");
        message.Append("<wish>\n").Append(wish).Append("\n</wish>");
        List<ClaudePart> parts = [new TextPart(message.ToString())];
        if (focus.Kind == Focus.Kinds.Picture && document is WordDocument word && word.PictureOf(focus.First, focus.Offset) is { } bytes && Media(bytes) is { } type)
            parts.Add(new ImagePart(bytes, type));
        return parts;
    }

    /// <summary>The media type of a picture Claude can see (PNG, JPEG, GIF, WebP), by its first bytes; null for others.</summary>
    static string? Media(byte[] bytes) => bytes switch
    {
        [0x89, 0x50, 0x4E, 0x47, ..] => "image/png",
        [0xFF, 0xD8, 0xFF, ..] => "image/jpeg",
        [0x47, 0x49, 0x46, ..] => "image/gif",
        [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50, ..] => "image/webp",
        _ => null,
    };

    /// <summary>A tool used: a write made whole, a page shown, an object changed, the document's tone; what it gives Claude back.</summary>
    async Task<ToolResultPart> Use(ToolCalled called, Dictionary<string, Writing> writes)
    {
        switch (called.Name)
        {
            case "write":
            {
                var writing = writes.TryGetValue(called.Id, out var going) ? going : writes[called.Id] = new Writing(editing, sheets, focus, objects!);
                var (told, error) = writing.Finish(called.Json);
                return new ToolResultPart(called.Id, told, [], error);
            }
            case "look":
            {
                var document = editing.Document;
                // The pages laid out to the end, as they read now.
                while (document.LayingOut && document.LayOut(64))
                {
                }
                var count = document.Scenes().Count;
                var page = Number(called.Json, "page");
                if (page is not { } shown || shown < 1 || shown > count)
                    return new ToolResultPart(called.Id, $"There are pages 1 to {count}.", [], Error: true);
                var png = SkiaScene.Png(document.Scene(shown - 1), 1.25);
                return new ToolResultPart(called.Id, $"Page {shown} of {count}.", [new ImagePart(png, "image/png")]);
            }
            case "picture":
            {
                var (told, error) = Arranging.Picture(editing, sheets, objects!, focus, called.Json);
                return new ToolResultPart(called.Id, told, [], error);
            }
            case "tone":
            {
                var (told, error) = await Arranging.Tone(editing, sheets, called.Json);
                return new ToolResultPart(called.Id, told, [], error);
            }
            case "page":
            {
                var (told, error) = Arranging.Page(editing, sheets, objects!, called.Json);
                return new ToolResultPart(called.Id, told, [], error);
            }
            case "comment":
            {
                var (told, error) = Commenting.Comment(editing, sheets, objects!, called.Json);
                return new ToolResultPart(called.Id, told, [], error);
            }
            default:
                return new ToolResultPart(called.Id, $"There is no tool {called.Name}.", [], Error: true);
        }
    }

    static int? Number(string json, string name)
    {
        try
        {
            using var input = JsonDocument.Parse(json);
            return input.RootElement.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static string? String(string json, string name)
    {
        try
        {
            using var input = JsonDocument.Parse(json);
            return input.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Takes back what a turn did (one Claude declined, or one another model went on from): the steps after so many.</summary>
    void TakeBackTo(int steps) => sheets.Edit(() =>
    {
        while (editing.Steps > steps && editing.TakeBack())
        {
        }
    });

    void Say(string key)
    {
        Line = text(key);
        Updated?.Invoke();
    }

    static string Short(string message) => message.Length <= 160 ? message : message[..160] + "…";
}
