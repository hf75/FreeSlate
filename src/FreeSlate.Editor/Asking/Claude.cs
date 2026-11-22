namespace FreeSlate.Editor.Asking;

/// <summary>
/// Claude as the editor reaches it: a conversation begun with what Claude is told first and the tools it may use, turn by turn, its
/// answer streaming back as it comes. The head of the editor sets the link where it can reach Claude (the desktop, given a key, see
/// Link); a walk sets one that plays answers given to it.
/// </summary>
public interface IClaudeLink
{
    IClaudeConversation Begin(string system, IReadOnlyList<ClaudeTool> tools);
}

/// <summary>
/// A conversation with Claude: each turn sends what the user said, or what the tools Claude used gave, and streams Claude's answer
/// back; the conversation keeps all that was said (Claude's thinking too, as it is to be given back) for the next turn.
/// </summary>
public interface IClaudeConversation
{
    IAsyncEnumerable<ClaudeEvent> Turn(IReadOnlyList<ClaudePart> parts, CancellationToken cancel);
}

/// <summary>A tool Claude may use: its name, what it does, its input's JSON schema; whether its input streams in as it is written.</summary>
public sealed record ClaudeTool(string Name, string Description, string Schema, bool Streams = false);

/// <summary>What a turn sends: text, a picture, or what a tool Claude used gave (text, pictures, whether it failed).</summary>
public abstract record ClaudePart;

public sealed record TextPart(string Text) : ClaudePart;

public sealed record ImagePart(byte[] Bytes, string MediaType) : ClaudePart;

public sealed record ToolResultPart(string Id, string Text, IReadOnlyList<ImagePart> Images, bool Error = false) : ClaudePart;

/// <summary>What streams back in a turn.</summary>
public abstract record ClaudeEvent;

/// <summary>Claude thinks before it answers or acts.</summary>
public sealed record Thinking : ClaudeEvent;

/// <summary>More of Claude's text.</summary>
public sealed record TextGrew(string Text) : ClaudeEvent;

/// <summary>Claude begins to use a tool.</summary>
public sealed record ToolBegan(string Id, string Name) : ClaudeEvent;

/// <summary>More of a tool's input, as JSON written so far (the whole of it so far, not the part that came).</summary>
public sealed record ToolGrew(string Id, string Json) : ClaudeEvent;

/// <summary>A tool's input whole: Claude uses it so.</summary>
public sealed record ToolCalled(string Id, string Name, string Json) : ClaudeEvent;

/// <summary>
/// Another model goes on where Claude declined to: what this turn gave before is not to be kept (Claude's text, the tools it began),
/// as the API leaves it out of what it reads on.
/// </summary>
public sealed record FellBack : ClaudeEvent;

/// <summary>The turn ended: why (end_turn, tool_use, max_tokens, refusal …), and where Claude declined, its reason where it gave one.</summary>
public sealed record TurnEnded(string StopReason, string? Explanation = null) : ClaudeEvent;

/// <summary>Where the editor reaches Claude: null where it cannot (no key), and asking says so.</summary>
public static class Claude
{
    public static IClaudeLink? Link { get; set; }
}
