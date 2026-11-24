using System.Globalization;
using System.Text;
using System.Text.Json;
using FreeSlate.Editing;
using FreeSlate.Editor.Controls;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// Comments as Claude reads and writes them, as Word's reviewing does (see WordDocument.Comments): its comment tool puts one on a
/// paragraph or on words in it, answers one, marks one done or takes one away, by the session's own handlings (each a step); its
/// comments are by "Claude". The document's comments are read after its notes, each with its id, author, the words it is on and its
/// text, answers under the comment they answer.
/// </summary>
static class Commenting
{
    const string Author = "Claude", Initials = "C";

    /// <summary>The comment tool: what it did, as the document then reads (its comments), or why it did nothing.</summary>
    public static (string Text, bool Error) Comment(EditSession editing, DocumentView sheets, Objects objects, string json)
    {
        if (editing.Document is not WordDocument word)
            return ("This document has no comments.", true);
        JsonElement input;
        try
        {
            using var parsed = JsonDocument.Parse(json);
            input = parsed.RootElement.Clone();
        }
        catch (JsonException)
        {
            return ("The input of comment is a JSON object.", true);
        }
        string? Text(string name) => input.TryGetProperty(name, out var value) ? value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        } : null;
        string Runs(string text) => Markup.RunsOf(Notation.Inline(text.Trim(), null)).Runs;
        string? refused = null;
        void Step(Func<TextPlace?> change)
        {
            sheets.Edit(() =>
            {
                try
                {
                    editing.Handle(change);
                }
                catch (Exception e) when (e is NotSupportedException or ArgumentException or InvalidOperationException)
                {
                    refused = e.Message;
                }
            });
        }
        string? made = null;
        if (Text("remove") is { Length: > 0 } removed)
            Step(() =>
            {
                word.DeleteComment(removed);
                return null;
            });
        else if (Text("resolve") is { Length: > 0 } resolved)
            Step(() =>
            {
                word.Resolve(resolved);
                return null;
            });
        else if (Text("text") is not { Length: > 0 } text)
            return ("Give the comment's text (text), or the id of a comment to resolve or remove.", true);
        else if (Text("reply_to") is { Length: > 0 } answered)
            Step(() =>
            {
                made = word.Reply(answered, Runs(text), Author, Initials);
                return null;
            });
        else
        {
            if (!int.TryParse(Text("paragraph"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var paragraph) || !word.Contains(paragraph))
                return ("Give the paragraph the comment is on (its id).", true);
            var (start, end) = (0, word.Text(paragraph).Length);
            // The words it is on, found in the paragraph as they read (the notation's marks aside), else the whole paragraph.
            if (Text("words") is { Length: > 0 } words)
            {
                var at = word.Text(paragraph).IndexOf(words.Trim(), StringComparison.Ordinal);
                if (at < 0)
                    return ($"¶{paragraph.ToString(CultureInfo.InvariantCulture)} does not contain \"{words.Trim()}\" as it reads.", true);
                (start, end) = (at, at + words.Trim().Length);
            }
            Step(() =>
            {
                made = word.AddComment(new TextPlace(paragraph, start), new TextPlace(paragraph, end), Runs(text), Author, Initials);
                return null;
            });
        }
        if (refused != null)
            return ($"Not done: {refused}", true);
        return ((made != null ? $"Comment {made} made. " : "Done. ") + "Comments now:\n" + Read(word, objects), false);
    }

    /// <summary>
    /// The document's comments as Claude reads them: "[comment 3] by Ann on ¶14 "the words": its text", answers indented under the
    /// comment they answer, a resolved one marked done; empty where it has none.
    /// </summary>
    public static string Read(WordDocument word, Objects objects)
    {
        IReadOnlyList<WordComment> all;
        try
        {
            all = word.Comments();
        }
        catch (NotSupportedException)
        {
            return "";
        }
        var text = new StringBuilder();
        void Write(WordComment comment, int depth)
        {
            text.Append(new string(' ', 2 * depth)).Append("[comment ").Append(comment.Id).Append("] by ").Append(comment.Author);
            if (comment.Parent == null && comment.Start is { } start)
            {
                text.Append(" on ¶").Append(start.Paragraph.ToString(CultureInfo.InvariantCulture));
                if (comment.End is { } end && word.Contains(start.Paragraph))
                {
                    var until = end.Paragraph == start.Paragraph ? end.Offset : word.Text(start.Paragraph).Length;
                    var words = Reading.Line(word, start.Paragraph, start.Offset, Math.Max(start.Offset, until), objects);
                    if (until - start.Offset < word.Text(start.Paragraph).Length && words.Trim().Length > 0)
                        text.Append(" \"").Append(words.Length > 120 ? words[..120] + "…" : words).Append('"');
                }
            }
            if (comment.Done)
                text.Append(" (done)");
            text.Append(": ").Append(string.Join(" / ", comment.Paragraphs.Where(word.Contains).Select(p => Reading.Line(word, p, objects)).Where(l => l.Length > 0)))
                .Append('\n');
            foreach (var answer in all.Where(c => c.Parent == comment.Id))
                Write(answer, depth + 1);
        }
        foreach (var comment in all.Where(c => c.Parent == null || all.All(o => o.Id != c.Parent)))
            Write(comment, 0);
        return text.ToString();
    }
}
