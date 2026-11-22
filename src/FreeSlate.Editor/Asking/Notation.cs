using System.Text;
using System.Text.RegularExpressions;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// The notation Claude reads a document in and writes into it (see Prompt): Markdown for the text, its headings and points the
/// editor's levels (see Writing.Levels) and its marks the meanings (see Writing.Meanings), {Style|text} for any meaning and {Style} before a
/// paragraph for any level; GitHub's tables; fenced blocks of a vector picture (svg) and of WordArt (wordart); "::: columns 2" to ":::"
/// for text in columns; \pagebreak; footnotes as [^1] with "[^1]: text"; and WordprocessingML where nothing else says it (w:p, w:tbl
/// as blocks; w:r, m:oMath and the like among the text). Read here into blocks as far as a text says them: one streaming in gives the
/// blocks it has finished, and the text block it is writing as it reads so far (see Parse).
/// </summary>
public static partial class Notation
{
    public abstract record Block;

    /// <summary>A paragraph: its style (null for text), what it holds.</summary>
    public sealed record Paragraph(string? Style, IReadOnlyList<Piece> Pieces) : Block;

    /// <summary>A table: its rows of cells, each the pieces of its text; the alignment of each column (left, center, right or null); whether the first row is its head.</summary>
    public sealed record Table(IReadOnlyList<IReadOnlyList<IReadOnlyList<Piece>>> Rows, IReadOnlyList<string?> Aligns) : Block;

    /// <summary>A fenced block of a kind of its own (svg, wordart): the settings on its first line, its body.</summary>
    public sealed record Fenced(string Kind, IReadOnlyDictionary<string, string> Settings, string Body) : Block;

    /// <summary>Text in columns begins: so many, a line between them or not.</summary>
    public sealed record Columns(int Count, bool Line) : Block;

    /// <summary>Text in columns ends: what follows runs as it ran before them.</summary>
    public sealed record ColumnsEnd : Block;

    public sealed record PageBreak : Block;

    /// <summary>A line across the text (Markdown's thematic break).</summary>
    public sealed record Rule : Block;

    /// <summary>Blocks of WordprocessingML as they are given (w:p, w:tbl, w:sdt).</summary>
    public sealed record Raw(string Markup) : Block;

    public abstract record Piece;

    /// <summary>Text with a meaning (a character style) or none.</summary>
    public sealed record Run(string Text, string? Style) : Piece;

    /// <summary>A line break within the paragraph (Shift+Enter).</summary>
    public sealed record Break : Piece;

    /// <summary>Where a footnote's mark stands: its label, whose text the notes give.</summary>
    public sealed record NoteMark(string Label) : Piece;

    /// <summary>WordprocessingML among the text as it is given (w:r, m:oMath, w:fldSimple, w:hyperlink …).</summary>
    public sealed record RawRun(string Markup) : Piece;

    /// <summary>The text block being written as far as it reads: its style, and its text without the notation's marks.</summary>
    public sealed record Tail(string? Style, string Text);

    /// <summary>
    /// What a text says: its blocks finished, in order; the block it is writing, where that is text; the footnotes' texts by their
    /// labels; and what could not be read (said back to Claude).
    /// </summary>
    public sealed record Parsed(IReadOnlyList<Block> Blocks, Tail? Tail, IReadOnlyDictionary<string, string> Notes, IReadOnlyList<string> Problems);

    /// <summary>The styles of the headings # to ###: the title, a chapter, a section (deeper ones are sections too).</summary>
    static readonly string[] Headings = ["Title", "Heading1", "Heading2"];

    enum Kind { Blank, Unsure, Text, Heading, Item, Quote, Table, Fence, ColumnsOpen, ColumnsClose, PageBreak, Rule, Raw, Note, Styled }

    [GeneratedRegex(@"^(#{1,6})\s+(.*?)\s*#*\s*$")]
    private static partial Regex HeadingLine();

    [GeneratedRegex(@"^( *)([-*+]|\d{1,3}[.)])\s+(.*)$")]
    private static partial Regex ItemLine();

    [GeneratedRegex(@"^\[\^([^\]\s]+)\]:\s?(.*)$")]
    private static partial Regex NoteLine();

    [GeneratedRegex(@"^\{([A-Za-z][A-Za-z0-9]*)\}\s+(.*)$")]
    private static partial Regex StyledLine();

    [GeneratedRegex(@"^:::\s*columns?\s*=?\s*(\d+)\s*(.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex ColumnsLine();

    [GeneratedRegex(@"^\s*<(w:p|w:tbl|w:sdt|w:customXml|w:bookmarkStart|w:bookmarkEnd)[\s/>]")]
    private static partial Regex RawLine();

    [GeneratedRegex(@"(\w+)\s*=\s*(""[^""]*""|'[^']*'|\S+)")]
    private static partial Regex Setting();

    /// <summary>
    /// Reads a text into blocks. <paramref name="final"/> where the text is whole; else it is still coming, its last line maybe cut,
    /// and a block counts as finished only once what follows it shows it ended (a paragraph a blank line, a list point the next line),
    /// so that a block given once is given alike as the text grows.
    /// </summary>
    public static Parsed Parse(string text, bool final)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var count = lines.Length;
        // The last line is still being written where the text is not whole.
        bool Open(int k) => !final && k == count - 1;
        var blocks = new List<Block>();
        var notes = new Dictionary<string, string>(StringComparer.Ordinal);
        var problems = new List<string>();
        Tail? tail = null;
        var columnsOpen = 0;

        // Whether a block whose last line is before line k ended there: the line there decides once it is written far enough to tell.
        bool Ended(int k, Func<string, bool> goesOn)
        {
            if (k >= count)
                return final;
            var line = lines[k];
            if (!Open(k))
                return !goesOn(line);
            if (line.Trim().Length == 0 || Classify(line, open: true) == Kind.Unsure)
                return false;
            return !goesOn(line);
        }

        var i = 0;
        while (i < count)
        {
            var line = lines[i];
            var open = Open(i);
            var kind = Classify(line, open);
            switch (kind)
            {
                case Kind.Blank or Kind.Unsure:
                    i++;
                    continue;
                case Kind.Heading:
                {
                    var match = HeadingLine().Match(line);
                    var style = Headings[Math.Min(match.Groups[1].Length, Headings.Length) - 1];
                    if (open)
                    {
                        tail = new Tail(style, Plain(match.Groups[2].Value));
                        i++;
                        continue;
                    }
                    blocks.Add(new Paragraph(style, Inline(match.Groups[2].Value, null)));
                    i++;
                    continue;
                }
                case Kind.Item:
                {
                    var match = ItemLine().Match(line);
                    var deep = match.Groups[1].Value.Length >= 2;
                    var ordered = char.IsDigit(match.Groups[2].Value[0]);
                    var body = new StringBuilder(match.Groups[3].Value);
                    var k = i + 1;
                    // Lines indented under the point go on with it.
                    static bool Continues(string next) => next.StartsWith("  ", StringComparison.Ordinal) && next.Trim().Length > 0 && !ItemLine().IsMatch(next);
                    while (k < count && !Open(k) && Continues(lines[k]))
                        body.Append(' ').Append(lines[k++].Trim());
                    var point = ordered ? match.Groups[2].Value + " " + body : body.ToString();
                    var style = ordered ? null : deep ? "ListBullet2" : "ListBullet";
                    if (!Ended(k, Continues))
                    {
                        tail = new Tail(style, Plain(point + (k < count && Open(k) ? " " + lines[k].Trim() : "")));
                        i = count;
                        continue;
                    }
                    blocks.Add(new Paragraph(style, Inline(point, null)));
                    i = k;
                    continue;
                }
                case Kind.Quote:
                {
                    var body = new StringBuilder();
                    var k = i;
                    static bool Continues(string next) => next.TrimStart().StartsWith('>');
                    while (k < count && !Open(k) && Continues(lines[k]))
                        body.Append(body.Length > 0 ? " " : "").Append(lines[k++].TrimStart()[1..].Trim());
                    if (k == i || !Ended(k, Continues))
                    {
                        var partial = body.ToString() + (k < count && Open(k) && Continues(lines[k]) ? " " + lines[k].TrimStart()[1..].Trim() : "");
                        tail = new Tail(null, Plain(partial.Trim()));
                        i = count;
                        continue;
                    }
                    blocks.Add(new Paragraph(null, Inline(body.ToString(), "Quotation")));
                    i = k;
                    continue;
                }
                case Kind.Table:
                {
                    var k = i;
                    static bool Continues(string next) => next.TrimStart().StartsWith('|');
                    while (k < count && !Open(k) && Continues(lines[k]))
                        k++;
                    if (k == i || !Ended(k, Continues))
                    {
                        i = count;
                        continue;
                    }
                    if (TableOf(lines[i..k]) is { } table)
                        blocks.Add(table);
                    else
                        problems.Add("A table needs a row of cells and a line of dashes under it: | a | b | then |---|---|.");
                    i = k;
                    continue;
                }
                case Kind.Fence:
                {
                    var fence = line.TrimStart();
                    var marks = fence.Length - fence.TrimStart('`').Length;
                    var info = fence[marks..].Trim();
                    var close = -1;
                    for (var k = i + 1; k < count && close < 0; k++)
                    {
                        if (!Open(k) && lines[k].Trim().StartsWith(new string('`', marks), StringComparison.Ordinal) && lines[k].Trim().Trim('`').Length == 0)
                            close = k;
                    }
                    if (close < 0 && !final)
                    {
                        i = count;
                        continue;
                    }
                    var end = close < 0 ? count : close;
                    var body = string.Join("\n", lines[(i + 1)..end]);
                    var kindName = info.Split(' ', 2)[0].ToLowerInvariant();
                    var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (Match setting in Setting().Matches(info.Length > kindName.Length ? info[kindName.Length..] : ""))
                        settings[setting.Groups[1].Value] = setting.Groups[2].Value.Trim('"', '\'');
                    if (close < 0)
                        problems.Add($"The ```{kindName} block was not closed; it was read to the end.");
                    if (kindName is "svg" or "wordart")
                        blocks.Add(new Fenced(kindName, settings, body));
                    else if (kindName is "xml" or "ooxml" or "wordml")
                        blocks.Add(new Raw(body));
                    else
                    {
                        // Code of another kind: its lines as text, as they stand.
                        foreach (var codeLine in body.Split('\n'))
                            blocks.Add(new Paragraph(null, [new Run(codeLine, null)]));
                    }
                    i = end + 1;
                    continue;
                }
                case Kind.ColumnsOpen:
                {
                    if (open)
                    {
                        i++;
                        continue;
                    }
                    var match = ColumnsLine().Match(line.Trim());
                    var columns = Math.Clamp(int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), 1, 6);
                    var rest = match.Groups[2].Value.ToLowerInvariant();
                    if (columnsOpen > 0)
                    {
                        blocks.Add(new ColumnsEnd());
                        columnsOpen--;
                    }
                    blocks.Add(new Columns(columns, rest.Contains("line", StringComparison.Ordinal) || rest.Contains("rule", StringComparison.Ordinal) || rest.Contains("sep", StringComparison.Ordinal)));
                    columnsOpen++;
                    i++;
                    continue;
                }
                case Kind.ColumnsClose:
                {
                    if (open)
                    {
                        i++;
                        continue;
                    }
                    if (columnsOpen > 0)
                    {
                        blocks.Add(new ColumnsEnd());
                        columnsOpen--;
                    }
                    i++;
                    continue;
                }
                case Kind.PageBreak or Kind.Rule:
                {
                    if (!open)
                        blocks.Add(kind == Kind.PageBreak ? new PageBreak() : new Rule());
                    i++;
                    continue;
                }
                case Kind.Raw:
                {
                    var rest = string.Join("\n", lines[i..]);
                    var start = rest.IndexOf('<');
                    // An element whose closing tag has come is whole, also where the line it ends on goes on being written.
                    if (Balanced(rest, start) is not { } end)
                    {
                        if (final)
                            problems.Add("WordprocessingML that does not close was left out: " + Shorten(rest[start..]));
                        i = count;
                        continue;
                    }
                    blocks.Add(new Raw(rest[start..end]));
                    // On past the lines it took; what stands after it on its last line is read as a line of its own.
                    var taken = rest[..end].Count(c => c == '\n');
                    var after = rest[end..].Split('\n', 2)[0];
                    if (after.Trim().Length > 0)
                    {
                        lines[i + taken] = after;
                        i += taken;
                    }
                    else
                        i += taken + 1;
                    continue;
                }
                case Kind.Note:
                {
                    var match = NoteLine().Match(line);
                    var body = new StringBuilder(match.Groups[2].Value.Trim());
                    var k = i + 1;
                    static bool Continues(string next) => next.StartsWith("  ", StringComparison.Ordinal) && next.Trim().Length > 0;
                    while (k < count && !Open(k) && Continues(lines[k]))
                        body.Append(' ').Append(lines[k++].Trim());
                    if (open || Ended(k, Continues) || final)
                        notes[match.Groups[1].Value] = body.ToString();
                    i = open ? count : k;
                    continue;
                }
                default:
                {
                    // Text, or text of a style of its own ({MarginNote} …): its lines up to a blank one or a block of another kind.
                    string? style = null;
                    var first = line;
                    if (kind == Kind.Styled)
                    {
                        var match = StyledLine().Match(line);
                        (style, first) = (match.Groups[1].Value, match.Groups[2].Value);
                    }
                    var parts = new List<string> { first };
                    var k = i + 1;
                    bool Continues(string next) => next.Trim().Length > 0 && Classify(next, open: false) == Kind.Text;
                    while (k < count && !Open(k) && Continues(lines[k]))
                        parts.Add(lines[k++]);
                    if (open || !Ended(k, Continues))
                    {
                        if (k < count && Open(k) && Continues(lines[k]))
                            parts.Add(lines[k]);
                        tail = new Tail(style, Plain(Joined(parts)));
                        i = count;
                        continue;
                    }
                    blocks.Add(new Paragraph(style, Inline(Joined(parts), null)));
                    i = k;
                    continue;
                }
            }
        }
        // Columns still open at the end of a whole text end there.
        if (final)
        {
            for (; columnsOpen > 0; columnsOpen--)
                blocks.Add(new ColumnsEnd());
        }
        return new Parsed(blocks, tail, notes, problems);
    }

    /// <summary>
    /// The lines of a paragraph as one text: a line ending in two spaces or a backslash breaks there (a line break within the
    /// paragraph, marked "&lt;br&gt;"), the others run on with a space.
    /// </summary>
    static string Joined(List<string> lines)
    {
        var text = new StringBuilder();
        for (var k = 0; k < lines.Count; k++)
        {
            var line = lines[k];
            var breaks = k < lines.Count - 1 && (line.EndsWith("  ", StringComparison.Ordinal) || line.EndsWith('\\'));
            text.Append(k == 0 ? line.TrimEnd() : line.Trim());
            if (breaks && text.Length > 0 && text[^1] == '\\')
                text.Length--;
            if (k < lines.Count - 1)
                text.Append(breaks ? "<br>" : " ");
        }
        return text.ToString().Trim();
    }

    /// <summary>What a line begins, as far as it is written: an open line whose start could still become something else is unsure.</summary>
    static Kind Classify(string line, bool open)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0)
            return Kind.Blank;
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
            return Kind.Fence;
        if (trimmed == ":::")
            return open ? Kind.Unsure : Kind.ColumnsClose;
        if (trimmed.StartsWith(":::", StringComparison.Ordinal))
            return ColumnsLine().IsMatch(trimmed) ? Kind.ColumnsOpen : open ? Kind.Unsure : Kind.Text;
        if (trimmed == "\\pagebreak")
            return Kind.PageBreak;
        if (trimmed.Length >= 3 && (trimmed.All(c => c == '-') || trimmed.All(c => c == '*') || trimmed.All(c => c == '_')))
            return open ? Kind.Unsure : Kind.Rule;
        if (HeadingLine().IsMatch(line))
            return Kind.Heading;
        if (ItemLine().IsMatch(line))
            return Kind.Item;
        if (trimmed.StartsWith('>'))
            return Kind.Quote;
        if (trimmed.StartsWith('|'))
            return Kind.Table;
        if (RawLine().IsMatch(line))
            return Kind.Raw;
        if (NoteLine().IsMatch(trimmed))
            return Kind.Note;
        if (StyledLine().IsMatch(line))
            return Kind.Styled;
        if (open)
        {
            // A start that could still become a mark of a block is not text yet.
            var c = trimmed[0];
            if (trimmed.Length < 12 && (c is '#' or '-' or '*' or '+' or ':' or '`' or '\\' or '[' or '{' or '<' || char.IsDigit(c))
                || line.StartsWith(' ') && trimmed.Length < 3)
                return Kind.Unsure;
        }
        return Kind.Text;
    }

    /// <summary>A table of GitHub's kind: a row of cells, a row of dashes (their colons aligning the columns), rows of cells; null where it is none.</summary>
    static Table? TableOf(string[] lines)
    {
        static List<string> Cells(string line)
        {
            var row = line.Trim();
            if (row.StartsWith('|'))
                row = row[1..];
            if (row.EndsWith('|') && !row.EndsWith("\\|", StringComparison.Ordinal))
                row = row[..^1];
            var cells = new List<string>();
            var cell = new StringBuilder();
            for (var k = 0; k < row.Length; k++)
            {
                if (row[k] == '\\' && k + 1 < row.Length && row[k + 1] == '|')
                {
                    cell.Append('|');
                    k++;
                }
                else if (row[k] == '|')
                {
                    cells.Add(cell.ToString().Trim());
                    cell.Clear();
                }
                else
                    cell.Append(row[k]);
            }
            cells.Add(cell.ToString().Trim());
            return cells;
        }
        if (lines.Length < 2)
            return null;
        var dashes = Cells(lines[1]);
        if (dashes.Count == 0 || dashes.Any(d => d.Length == 0 || d.Trim(':', '-', ' ').Length > 0 || !d.Contains('-')))
            return null;
        var aligns = dashes.Select(d => d.StartsWith(':') && d.EndsWith(':') ? "center" : d.EndsWith(':') ? "right" : d.StartsWith(':') ? "left" : null).ToList();
        var rows = new List<IReadOnlyList<IReadOnlyList<Piece>>>();
        foreach (var line in lines.Where((_, k) => k != 1))
        {
            var cells = Cells(line);
            // As many cells as the head has: too few filled up empty, too many left out.
            rows.Add([.. Enumerable.Range(0, dashes.Count).Select(k => k < cells.Count ? Inline(cells[k].Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase), null) : (IReadOnlyList<Piece>)[])]);
        }
        return new Table(rows, aligns);
    }

    /// <summary>
    /// The end of the element of markup that begins at a place (after its closing tag), its elements nested; null where it does not
    /// close in the text (as one streaming in may not yet).
    /// </summary>
    public static int? Balanced(string text, int start)
    {
        if (start < 0 || start >= text.Length || text[start] != '<')
            return null;
        var depth = 0;
        var k = start;
        while (k < text.Length)
        {
            var open = text.IndexOf('<', k);
            if (open < 0)
                return null;
            var close = text.IndexOf('>', open);
            if (close < 0)
                return null;
            // Comments and processing instructions stand for nothing here.
            if (text.AsSpan(open).StartsWith("<!--"))
            {
                var after = text.IndexOf("-->", open, StringComparison.Ordinal);
                if (after < 0)
                    return null;
                k = after + 3;
                continue;
            }
            // A quote may hold a '>': the tag ends at the first '>' outside quotes.
            var quote = '\0';
            close = -1;
            for (var c = open + 1; c < text.Length; c++)
            {
                if (quote != '\0')
                {
                    if (text[c] == quote)
                        quote = '\0';
                }
                else if (text[c] is '"' or '\'')
                    quote = text[c];
                else if (text[c] == '>')
                {
                    close = c;
                    break;
                }
            }
            if (close < 0)
                return null;
            if (text[open + 1] == '/')
                depth--;
            else if (text[close - 1] != '/' && text[open + 1] != '?' && text[open + 1] != '!')
                depth++;
            k = close + 1;
            if (depth == 0)
                return k;
        }
        return null;
    }

    static string Shorten(string text) => text.Length <= 80 ? text : text[..80] + "…";

    /// <summary>A text without the notation's marks, as it shows while it is being written.</summary>
    static string Plain(string text) => PlainMarks().Replace(text.Replace("<br>", " ", StringComparison.OrdinalIgnoreCase), "").Replace("\\", "", StringComparison.Ordinal);

    [GeneratedRegex(@"\{[A-Za-z][A-Za-z0-9]*\||\[\^[^\]]*\]?|[*_`}]|<[^>]*>?")]
    private static partial Regex PlainMarks();

    /// <summary>
    /// The pieces of a paragraph's text: **important**, *emphasized* (or _so_), {Style|text}, `a term`, [^note] marks, line breaks
    /// (&lt;br&gt;, a line feed in a table's cell), [text](address) as its text, WordprocessingML (w:r, m:oMath …) as it is; a backslash
    /// takes the next mark as text. Text without a mark of its own takes <paramref name="style"/>.
    /// </summary>
    public static IReadOnlyList<Piece> Inline(string text, string? style)
    {
        var pieces = new List<Piece>();
        var buffer = new StringBuilder();
        var (important, emphasized) = (false, false);
        void Flush()
        {
            if (buffer.Length == 0)
                return;
            pieces.Add(new Run(buffer.ToString(), important ? "Important" : emphasized ? "Emphasized" : style));
            buffer.Clear();
        }
        var k = 0;
        while (k < text.Length)
        {
            var c = text[k];
            var rest = text.AsSpan(k);
            if (c == '\\' && k + 1 < text.Length && "\\*_{}[]|<>`#".Contains(text[k + 1]))
            {
                buffer.Append(text[k + 1]);
                k += 2;
                continue;
            }
            if (c == '\n')
            {
                Flush();
                pieces.Add(new Break());
                k++;
                continue;
            }
            if (rest.StartsWith("**") && (important || text.IndexOf("**", k + 2, StringComparison.Ordinal) > k + 2))
            {
                Flush();
                important = !important;
                k += 2;
                continue;
            }
            if (c is '*' or '_')
            {
                var opening = !emphasized && k + 1 < text.Length && !char.IsWhiteSpace(text[k + 1]) && text.IndexOf(c, k + 1) > k + 1
                    && (c == '*' || k == 0 || !char.IsLetterOrDigit(text[k - 1]));
                var closing = emphasized && k > 0 && !char.IsWhiteSpace(text[k - 1]) && (c == '*' || k + 1 >= text.Length || !char.IsLetterOrDigit(text[k + 1]));
                if (opening || closing)
                {
                    Flush();
                    emphasized = !emphasized;
                    k++;
                    continue;
                }
            }
            if (c == '{' && StyledText().Match(text, k) is { Success: true } styled)
            {
                Flush();
                foreach (var piece in Inline(styled.Groups[2].Value, styled.Groups[1].Value))
                    pieces.Add(piece);
                k += styled.Length;
                continue;
            }
            if (c == '[' && NoteMarkText().Match(text, k) is { Success: true } note)
            {
                Flush();
                pieces.Add(new NoteMark(note.Groups[1].Value));
                k += note.Length;
                continue;
            }
            if (c == '[' && LinkText().Match(text, k) is { Success: true } link)
            {
                buffer.Append(link.Groups[1].Value);
                k += link.Length;
                continue;
            }
            if (c == '`' && CodeText().Match(text, k) is { Success: true } code)
            {
                Flush();
                pieces.Add(new Run(code.Groups[1].Value, "Term"));
                k += code.Length;
                continue;
            }
            if (c == '<' && BreakTag().Match(text, k) is { Success: true } br)
            {
                Flush();
                pieces.Add(new Break());
                k += br.Length;
                continue;
            }
            if (c == '<' && RawStart().IsMatch(text, k) && Balanced(text, k) is { } end)
            {
                Flush();
                pieces.Add(new RawRun(text[k..end]));
                k = end;
                continue;
            }
            buffer.Append(c);
            k++;
        }
        Flush();
        return pieces;
    }

    [GeneratedRegex(@"\G\{([A-Za-z][A-Za-z0-9]*)\|((?:[^{}\\]|\\.)*)\}")]
    private static partial Regex StyledText();

    [GeneratedRegex(@"\G\[\^([^\]\s]+)\]")]
    private static partial Regex NoteMarkText();

    [GeneratedRegex(@"\G\[([^\]]*)\]\(([^)\s]*)\)")]
    private static partial Regex LinkText();

    [GeneratedRegex(@"\G`([^`]+)`")]
    private static partial Regex CodeText();

    [GeneratedRegex(@"\G<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BreakTag();

    [GeneratedRegex(@"\G<(w|m|wp|a|mc):[A-Za-z]")]
    private static partial Regex RawStart();
}
