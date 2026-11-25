using System.Text;
using System.Text.RegularExpressions;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// The notation Claude reads a document in and writes into it (see Prompt): Markdown for the text, its headings and points the
/// editor's levels (see Writing.Levels) and its marks the meanings (see Writing.Meanings), {Style|text} for any meaning and {Style} before a
/// paragraph for any level; GitHub's tables; fenced blocks of a vector picture (svg) and of WordArt (wordart); "::: columns 2" to ":::"
/// for text in columns; \pagebreak, \columnbreak, \sectionbreak; footnotes as [^1] with "[^1]: text"; direct formatting as ==marked==,
/// ~~struck~~, H~2~O, x^2^, {font=Georgia|text} (see RunLook) and "{align=right}" before a paragraph's text (see ParagraphLook); and
/// WordprocessingML where nothing else says it (w:p, w:tbl
/// as blocks; w:r, m:oMath and the like among the text). Read here into blocks as far as a text says them: one streaming in gives the
/// blocks it has finished, and the text block it is writing as it reads so far (see Parse).
/// </summary>
public static partial class Notation
{
    public abstract record Block;

    /// <summary>A paragraph: its style (null for text), what it holds.</summary>
    public sealed record Paragraph(string? Style, IReadOnlyList<Piece> Pieces) : Block
    {
        /// <summary>A numbered point's number as written ("3." gives 3), which a list beginning with it starts at (see Writing.Lists).</summary>
        public int? Number { get; init; }

        /// <summary>Its own formatting, from "{align=right after=12pt}" at its text's start (see ParagraphLook); null for none.</summary>
        public ParagraphLook? Look { get; init; }
    }

    /// <summary>
    /// A section break, "\sectionbreak" on a line of its own (Word's Layout > Breaks): the section ends there, the next one beginning as its
    /// kind says (nextPage, continuous, evenPage, oddPage; see Writing.Break, WordDocument.BreakSection).
    /// </summary>
    public sealed record SectionBreak(string Kind) : Block;

    /// <summary>A column break, "\columnbreak" on a line of its own: what follows begins in the next column.</summary>
    public sealed record ColumnBreak : Block;

    /// <summary>
    /// A table: its rows of cells, each the pieces of its text; the alignment of each column (left, center, right or null). And as the
    /// notation gives them besides GitHub's: the table's own settings, from a line "{table look=book head=none …}" right before it; each
    /// cell's settings, from "{fill=yellow valign=middle …}" at its start; whether a cell is merged with the one before it ("&lt;") or
    /// above it ("^"), else its own ('\0'); and each cell's text as written, its settings aside.
    /// </summary>
    public sealed record Table(IReadOnlyList<IReadOnlyList<IReadOnlyList<Piece>>> Rows, IReadOnlyList<string?> Aligns) : Block
    {
        public IReadOnlyDictionary<string, string> Settings { get; init; } = new Dictionary<string, string>();
        public IReadOnlyList<IReadOnlyList<IReadOnlyDictionary<string, string>>> Looks { get; init; } = [];
        public IReadOnlyList<IReadOnlyList<char>> Merged { get; init; } = [];
        public IReadOnlyList<IReadOnlyList<string>> Texts { get; init; } = [];
    }

    /// <summary>A fenced block of a kind of its own (svg, wordart): the settings on its first line, its body.</summary>
    public sealed record Fenced(string Kind, IReadOnlyDictionary<string, string> Settings, string Body) : Block;

    /// <summary>Text in columns begins: so many, a line between them or not.</summary>
    public sealed record Columns(int Count, bool Line) : Block
    {
        /// <summary>The columns' widths as shares of each other ("widths=2:1"), null for columns of even width; and the space between them (points), null for the section's.</summary>
        public IReadOnlyList<double>? Widths { get; init; }
        public double? Space { get; init; }
    }

    /// <summary>Text in columns ends: what follows runs as it ran before them.</summary>
    public sealed record ColumnsEnd : Block;

    public sealed record PageBreak : Block;

    /// <summary>
    /// A table of contents ("{contents Title}" on a line of its own; see Contents), with its title, or none; a table of figures ("{figures
    /// Figure}") lists the captions of a label instead, its label given (Word's Insert Table of Figures).
    /// </summary>
    public sealed record ContentsTable(string? Title, string? Label = null) : Block
    {
        /// <summary>Whether it is an index ("{index}" on a line of its own; see Index) rather than a table of contents.</summary>
        public bool Index { get; init; }

        /// <summary>Whether it is a bibliography ("{bibliography}" on a line of its own; see Citing) of the document's sources.</summary>
        public bool Bibliography { get; init; }

        /// <summary>
        /// Whether it is a table of authorities ("{authorities}" on a line of its own; see Authorities): the categories it names ("Cases,
        /// Statutes"), empty for all; null for another table.
        /// </summary>
        public string? Authorities { get; init; }
    }

    /// <summary>A line across the text (Markdown's thematic break).</summary>
    public sealed record Rule : Block;

    /// <summary>A cover page ("{cover style=band title=… }" on a line of its own; see Covers): its design and fields.</summary>
    public sealed record Cover(IReadOnlyDictionary<string, string> Settings) : Block;

    /// <summary>Blocks of WordprocessingML as they are given (w:p, w:tbl, w:sdt).</summary>
    public sealed record Raw(string Markup) : Block;

    /// <summary>A formula in a paragraph of its own, centred ($$…$$): its LaTeX (see Formulas).</summary>
    public sealed record MathBlock(string Latex) : Block;

    public abstract record Piece
    {
        /// <summary>The direct formatting it has besides its meaning (==marked==, {font=Georgia|…}, see RunLook); null for none.</summary>
        public RunLook? Look { get; init; }
    }

    /// <summary>Text with a meaning (a character style) or none.</summary>
    public sealed record Run(string Text, string? Style) : Piece;

    /// <summary>A line break within the paragraph (Shift+Enter).</summary>
    public sealed record Break : Piece;

    /// <summary>Where a footnote's mark stands: its label, whose text the notes give.</summary>
    public sealed record NoteMark(string Label) : Piece;

    /// <summary>WordprocessingML among the text as it is given (w:r, m:oMath, w:fldSimple, w:hyperlink …).</summary>
    public sealed record RawRun(string Markup) : Piece;

    /// <summary>An object of the document by its token ("[picture 3]", see Objects): it stands here, as it was.</summary>
    public sealed record Kept(int Id) : Piece;

    /// <summary>A link: its text's pieces (in the link's style), and where it leads (an address, or "#" and a bookmark's name).</summary>
    public sealed record Link(IReadOnlyList<Piece> Pieces, string Address) : Piece;

    /// <summary>A field the notation names: {page} the page's number, {pages} the pages' count, {date} today's date.</summary>
    public sealed record FieldMark(string Name) : Piece;

    /// <summary>
    /// An anchor, {#name}: in a caption right after its label ("Figure {#sales}") the caption's number, counted with the document's other
    /// captions of that label, the label and number its text to refer to; elsewhere its paragraph's text (a heading's). An empty name
    /// ({#}) numbers a caption no reference needs.
    /// </summary>
    public sealed record Anchor(string Name) : Piece;

    /// <summary>A reference to an anchor: {ref name} shows its text (a caption's label and number, a heading's text), {page name} its page.</summary>
    public sealed record Reference(string Name, bool Page) : Piece;

    /// <summary>
    /// A drop cap, "{drop}" at the start of a paragraph: its first letter large, so many lines down (3 unless "{drop 4}"), in the text or
    /// in the margin ("{drop margin}"), as Word's Insert > Drop Cap makes it (see WordDocument.DropCap).
    /// </summary>
    public sealed record DropMark(int Lines, bool Margin) : Piece;

    /// <summary>A mark for the index, "{index term}" ("{index main: sub}" a subentry): Word's XE field, showing nothing (see Index).</summary>
    public sealed record IndexMark(string Term) : Piece;

    /// <summary>
    /// A citation marked for a table of authorities, "{authority Cases: long citation | short}" ("{authority short}" a later mention): Word's
    /// TA field, showing nothing (see Authorities).
    /// </summary>
    public sealed record Authority(string Short, string? Long, int? Category) : Piece;

    /// <summary>
    /// A citation, "[@smith20]" as Pandoc writes one ("[see @smith20, p. 33; -@lee18]"): Word's CITATION field of the sources it names
    /// by their tags, its text as the document's citation style formats them (see Citing).
    /// </summary>
    public sealed record CiteMark(IReadOnlyList<CiteItem> Items) : Piece;

    /// <summary>
    /// One source of a citation: its tag, the words before it ("see "), its pages and volume (", p. 33", ", vol. 2"), the words after it
    /// (", ch. 4"), and what it leaves out: the author ("-@tag"), the year (" -year"), the title (" -title").
    /// </summary>
    public sealed record CiteItem(string Tag, string? Prefix = null, string? Pages = null, string? Volume = null, string? Suffix = null,
        bool NoAuthor = false, bool NoYear = false, bool NoTitle = false);

    /// <summary>A citation's sources from the text between its brackets ("see @smith20, p. 33; -@lee18"), or null where it is none.</summary>
    public static IReadOnlyList<CiteItem>? CiteItems(string inner)
    {
        var items = new List<CiteItem>();
        foreach (var part in inner.Split(';'))
        {
            if (CiteItemText().Match(part) is not { Success: true } item)
                return null;
            var rest = item.Groups[4].Value;
            var (noYear, noTitle) = (CiteFlag("year").IsMatch(rest), CiteFlag("title").IsMatch(rest));
            rest = CiteFlag("year").Replace(CiteFlag("title").Replace(rest, ""), "");
            string? pages = null, volume = null, suffix = null;
            var trimmed = rest.Trim();
            if (trimmed.StartsWith(','))
            {
                var segments = trimmed[1..].Split(',').Select(s => s.Trim()).ToList();
                var k = 0;
                for (; k < segments.Count; k++)
                {
                    if (pages == null && PagesText().Match(segments[k]) is { Success: true } p)
                        pages = p.Groups[1].Value.Trim();
                    else if (volume == null && VolumeText().Match(segments[k]) is { Success: true } v)
                        volume = v.Groups[1].Value.Trim();
                    else
                        break;
                }
                if (k < segments.Count)
                    suffix = ", " + string.Join(", ", segments.Skip(k));
            }
            else if (trimmed.Length > 0)
                suffix = " " + trimmed;
            items.Add(new CiteItem(item.Groups[3].Value, item.Groups[1].Value is { Length: > 0 } prefix ? prefix.TrimStart() : null, pages, volume, suffix,
                item.Groups[2].Success && item.Groups[2].Value.Length > 0, noYear, noTitle));
        }
        return items.Count > 0 ? items : null;
    }

    /// <summary>A citation's sources as the notation writes them (see CiteItems).</summary>
    public static string CiteText(IEnumerable<CiteItem> items) =>
        "[" + string.Join("; ", items.Select(i => (i.Prefix ?? "") + (i.NoAuthor ? "-" : "") + "@" + i.Tag + (i.NoYear ? " -year" : "") + (i.NoTitle ? " -title" : "")
            + (i.Volume != null ? ", vol. " + i.Volume : "") + (i.Pages != null ? ", p. " + i.Pages : "") + (i.Suffix ?? ""))) + "]";

    /// <summary>A citation in brackets: a source's tag after an "@" among its words, not a link's text.</summary>
    [GeneratedRegex(@"\G\[([^\[\]\n]*@[\p{L}\p{N}_][^\[\]\n]*)\](?!\()")]
    private static partial Regex CiteBrackets();

    /// <summary>One source of a citation: the words before it (ending in a space), "-" leaving the author out, "@" and the tag, the rest.</summary>
    [GeneratedRegex(@"^\s*((?:[^@]*\s)?)(-?)@([\p{L}\p{N}_](?:[\p{L}\p{N}_:.\-/]*[\p{L}\p{N}_])?)(.*)$")]
    private static partial Regex CiteItemText();

    static Regex CiteFlag(string what) => new(@"(?:^|\s)-" + what + @"\b", RegexOptions.IgnoreCase);

    [GeneratedRegex(@"^(?:pp?\.|pages?|S\.|Seiten?)\s*(\S.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex PagesText();

    [GeneratedRegex(@"^(?:vol\.|volume|Bd\.|Band)\s*(\S.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex VolumeText();

    /// <summary>A formula among the text ($…$): its LaTeX (see Formulas).</summary>
    public sealed record Formula(string Latex) : Piece;

    /// <summary>
    /// A tracked change as CriticMarkup writes one (see Reviewing): "{++inserted++}" ('+'), "{--deleted--}" ('-'), "{==formatted==}"
    /// ('='), the id of the document's change it is after its closing mark ("++12}"), none for a new one; its pieces, and its text as
    /// written (marks unescaped), where "¶" is a paragraph's mark.
    /// </summary>
    public sealed record Change(char Kind, IReadOnlyList<Piece> Pieces, int? Id, string Text) : Piece;

    /// <summary>A tracked change among the text: its marks doubled, its text, its closing marks with the change's id where it has one.</summary>
    [GeneratedRegex(@"\G\{(\+\+|--|==)(.*?)\1(\d{0,9})\}", RegexOptions.Singleline)]
    private static partial Regex ChangeText();

    /// <summary>The text block being written as far as it reads: its style, and its text without the notation's marks.</summary>
    public sealed record Tail(string? Style, string Text);

    /// <summary>
    /// What a text says: its blocks finished, in order; the block it is writing, where that is text; the footnotes' texts by their
    /// labels; and what could not be read (said back to Claude).
    /// </summary>
    public sealed record Parsed(IReadOnlyList<Block> Blocks, Tail? Tail, IReadOnlyDictionary<string, string> Notes, IReadOnlyList<string> Problems);

    /// <summary>The styles of the headings # to ###: the title, a chapter, a section (deeper ones are sections too).</summary>
    static readonly string[] Headings = ["Title", "Heading1", "Heading2"];

    enum Kind { Blank, Unsure, Text, Heading, Item, Quote, Table, TableSettings, Fence, Formula, ColumnsOpen, ColumnsClose, PageBreak, Rule, Raw, Note, Styled, Contents, Breaks }

    /// <summary>A section break ("\sectionbreak", "\sectionbreak continuous") or a column break ("\columnbreak") on a line of its own.</summary>
    [GeneratedRegex(@"^\\(sectionbreak|columnbreak)(?:\s+([a-z]+))?$", RegexOptions.IgnoreCase)]
    private static partial Regex BreakLine();

    /// <summary>A section break's kind as WordprocessingML names it (w:type) by the word after "\sectionbreak"; nextPage where none is given, null for a word it does not know.</summary>
    public static string? SectionKind(string? word) => word?.ToLowerInvariant() switch
    {
        null or "" or "next" or "nextpage" or "page" => "nextPage",
        "continuous" => "continuous",
        "even" or "evenpage" => "evenPage",
        "odd" or "oddpage" => "oddPage",
        "column" or "nextcolumn" => "nextColumn",
        _ => null,
    };

    /// <summary>The word "\sectionbreak" takes for a section break's kind (see SectionKind).</summary>
    public static string SectionWord(string? kind) => kind switch
    {
        "continuous" => "continuous",
        "evenPage" => "even",
        "oddPage" => "odd",
        "nextColumn" => "column",
        _ => "next",
    };

    /// <summary>A table of contents: "{contents}" (or "{toc}") on a line of its own, its title after the word; "{figures Label}" a table of figures.</summary>
    [GeneratedRegex(@"^\{\s*(contents|toc|figures)(?:\s+([^{}]*?))?\s*\}$", RegexOptions.IgnoreCase)]
    private static partial Regex ContentsLine();

    /// <summary>An index: "{index}" on a line of its own (see Index).</summary>
    [GeneratedRegex(@"^\{\s*index\s*\}$", RegexOptions.IgnoreCase)]
    private static partial Regex IndexLine();

    /// <summary>A bibliography: "{bibliography}" on a line of its own (see Citing).</summary>
    [GeneratedRegex(@"^\{\s*bibliography\s*\}$", RegexOptions.IgnoreCase)]
    private static partial Regex BibliographyLine();

    /// <summary>A table of authorities: "{authorities}" on a line of its own, the categories it lists after the word (see Authorities).</summary>
    [GeneratedRegex(@"^\{\s*authorities(?:\s+([^{}]*?))?\s*\}$", RegexOptions.IgnoreCase)]
    private static partial Regex AuthoritiesLine();

    /// <summary>A cover page: "{cover style=… title="…" …}" on a line of its own (see Covers).</summary>
    [GeneratedRegex(@"^\{\s*cover(?:\s+([^{}]*?))?\s*\}$", RegexOptions.IgnoreCase)]
    private static partial Regex CoverLine();

    [GeneratedRegex(@"^(#{1,6})\s+(.*?)\s*#*\s*$")]
    private static partial Regex HeadingLine();

    /// <summary>
    /// A point: "- " (or "* ", "+ ") bulleted, "1. " (or "1) ") numbered; indented by two spaces or more a subpoint, a numbered one also by
    /// its letter ("  a) ").
    /// </summary>
    [GeneratedRegex(@"^( *)([-*+]|\d{1,3}[.)]|(?<= {2})[a-z][.)])\s+(.*)$")]
    private static partial Regex ItemLine();

    [GeneratedRegex(@"^\[\^([^\]\s]+)\]:\s?(.*)$")]
    private static partial Regex NoteLine();

    [GeneratedRegex(@"^\{([A-Z][A-Za-z0-9]*)\}\s+(.*)$")]
    private static partial Regex StyledLine();

    [GeneratedRegex(@"^:::\s*columns?\s*=?\s*(\d+)\s*(.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex ColumnsLine();

    [GeneratedRegex(@"^\s*<(w:p|w:tbl|w:sdt|w:customXml|w:bookmarkStart|w:bookmarkEnd)[\s/>]")]
    private static partial Regex RawLine();

    [GeneratedRegex(@"(\w+)\s*=\s*(""[^""]*""|'[^']*'|[^\s}]+)")]
    private static partial Regex Setting();

    /// <summary>A table's own settings on a line before it: "{table look=book head=none}".</summary>
    [GeneratedRegex(@"^\s*\{table(?:\s+([^}]*))?\}\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex TableSettingsLine();

    /// <summary>A cell's settings at its start: "{fill=yellow valign=middle}" (not text of a look of its own, "{color=red|text}").</summary>
    [GeneratedRegex(@"^\{\s*(\w+\s*=[^}|]*)\}\s*")]
    private static partial Regex CellSettings();

    /// <summary>Settings as key=value pairs, a value quoted where it holds spaces.</summary>
    static Dictionary<string, string> SettingsOf(string text)
    {
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match setting in Setting().Matches(text))
            settings[setting.Groups[1].Value] = setting.Groups[2].Value.Trim('"', '\'');
        return settings;
    }

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
        // A table's own settings, read from the line right before it.
        Dictionary<string, string>? tableSettings = null;

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
            if (kind is not (Kind.Table or Kind.TableSettings or Kind.Unsure))
                tableSettings = null;
            switch (kind)
            {
                case Kind.Blank or Kind.Unsure:
                    i++;
                    continue;
                case Kind.TableSettings:
                    if (!open)
                        tableSettings = SettingsOf(TableSettingsLine().Match(line).Groups[1].Value);
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
                    blocks.Add(Paragraphed(style, match.Groups[2].Value));
                    i++;
                    continue;
                }
                case Kind.Item:
                {
                    var match = ItemLine().Match(line);
                    var deep = match.Groups[1].Value.Length >= 2;
                    var marker = match.Groups[2].Value;
                    var ordered = marker[0] is not ('-' or '*' or '+');
                    int? number = char.IsDigit(marker[0]) ? int.Parse(marker[..^1], System.Globalization.CultureInfo.InvariantCulture) : null;
                    var body = new StringBuilder(match.Groups[3].Value);
                    var k = i + 1;
                    // Lines indented under the point go on with it.
                    static bool Continues(string next) => next.StartsWith("  ", StringComparison.Ordinal) && next.Trim().Length > 0 && !ItemLine().IsMatch(next);
                    while (k < count && !Open(k) && Continues(lines[k]))
                        body.Append(' ').Append(lines[k++].Trim());
                    var point = body.ToString();
                    var style = ordered ? deep ? "ListNumber2" : "ListNumber" : deep ? "ListBullet2" : "ListBullet";
                    if (!Ended(k, Continues))
                    {
                        tail = new Tail(style, Plain(point + (k < count && Open(k) ? " " + lines[k].Trim() : "")));
                        i = count;
                        continue;
                    }
                    blocks.Add(Paragraphed(style, point) with { Number = number });
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
                    blocks.Add(Paragraphed(null, body.ToString(), "Quotation"));
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
                    {
                        blocks.Add(tableSettings is { } settings ? table with { Settings = settings } : table);
                        tableSettings = null;
                    }
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
                    // A group's shapes are fenced blocks within it: a fence that opens one (``` and its kind) is closed by the next bare
                    // fence, so that the group closes at the bare fence after them, whether its own fence is longer or not.
                    var grouped = info.StartsWith("group", StringComparison.OrdinalIgnoreCase);
                    var nested = 0;
                    for (var k = i + 1; k < count && close < 0; k++)
                    {
                        if (Open(k))
                            continue;
                        var fenced = lines[k].Trim();
                        if (grouped && fenced.StartsWith("```", StringComparison.Ordinal) && fenced.Trim('`').Length > 0)
                            nested++;
                        else if (fenced.StartsWith(new string('`', grouped && nested > 0 ? 3 : marks), StringComparison.Ordinal) && fenced.Trim('`').Length == 0)
                        {
                            if (grouped && nested > 0)
                                nested--;
                            else
                                close = k;
                        }
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
                    if (kindName is "svg" or "wordart" or "box" or "chart" or "smartart" or "shape" or "bibtex" or "bib" or "group" or "signature")
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
                case Kind.Formula:
                {
                    // $$…$$ on a line, or from a line beginning with $$ to one ending with it.
                    var first = line.Trim()[2..];
                    var body = new StringBuilder();
                    var close = -1;
                    if (first.TrimEnd().EndsWith("$$", StringComparison.Ordinal) && !open)
                    {
                        body.Append(first.TrimEnd()[..^2]);
                        close = i;
                    }
                    else
                    {
                        body.Append(first);
                        for (var k = i + 1; k < count && close < 0; k++)
                        {
                            if (Open(k))
                                break;
                            var next = lines[k].TrimEnd();
                            if (next.EndsWith("$$", StringComparison.Ordinal))
                            {
                                body.Append(' ').Append(next[..^2]);
                                close = k;
                            }
                            else
                                body.Append(' ').Append(next);
                        }
                    }
                    if (close < 0 && !final)
                    {
                        i = count;
                        continue;
                    }
                    if (close < 0)
                        problems.Add("A formula's $$ was not closed; it was read to the end.");
                    blocks.Add(new MathBlock(body.ToString().Trim()));
                    i = close < 0 ? count : close + 1;
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
                    // Columns of widths of their own ("widths=2:1", as many shares as columns) and the space between them ("space=1cm").
                    var given = SettingsOf(rest);
                    var widths = given.GetValueOrDefault("widths")?.Split(':', ',', '/').Select(w => double.TryParse(w.Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var share) && share > 0 ? share : 0).ToList();
                    if (widths != null && (widths.Count != columns || widths.Any(w => w <= 0)))
                    {
                        problems.Add($"widths={given["widths"]} gives no share for each of the {columns} columns (widths=2:1 for two, the first twice as wide).");
                        widths = null;
                    }
                    blocks.Add(new Columns(columns, Regex.IsMatch(rest, @"(^|\s)(lines?|rule|sep)(\s|$)"))
                    {
                        Widths = widths is { Count: > 1 } shares && shares.Distinct().Count() > 1 ? shares : null,
                        Space = Lengths.Points(given.GetValueOrDefault("space")),
                    });
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
                case Kind.Breaks:
                {
                    var match = BreakLine().Match(line.Trim());
                    if (match.Groups[1].Value.Equals("columnbreak", StringComparison.OrdinalIgnoreCase))
                        blocks.Add(new ColumnBreak());
                    else if (SectionKind(match.Groups[2].Success ? match.Groups[2].Value : null) is { } sectionKind)
                        blocks.Add(new SectionBreak(sectionKind));
                    else
                        problems.Add($"\\sectionbreak {match.Groups[2].Value} is no section break: \\sectionbreak, \\sectionbreak continuous, \\sectionbreak even or \\sectionbreak odd.");
                    i++;
                    continue;
                }
                case Kind.Contents:
                {
                    if (!open && IndexLine().IsMatch(lines[i].Trim()))
                        blocks.Add(new ContentsTable(null) { Index = true });
                    else if (!open && BibliographyLine().IsMatch(lines[i].Trim()))
                        blocks.Add(new ContentsTable(null) { Bibliography = true });
                    else if (!open && CoverLine().Match(lines[i].Trim()) is { Success: true } cover)
                        blocks.Add(new Cover(SettingsOf(cover.Groups[1].Value)));
                    else if (!open && AuthoritiesLine().Match(lines[i].Trim()) is { Success: true } authorities)
                        blocks.Add(new ContentsTable(null) { Authorities = authorities.Groups[1].Value.Trim() });
                    else if (!open && ContentsLine().Match(lines[i].Trim()) is { } contents)
                    {
                        var words = contents.Groups[2].Value is { Length: > 0 } given ? given : null;
                        blocks.Add(contents.Groups[1].Value.Equals("figures", StringComparison.OrdinalIgnoreCase)
                            ? new ContentsTable(null, words ?? "Figure") : new ContentsTable(words));
                    }
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
                    blocks.Add(Paragraphed(style, Joined(parts)));
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

    /// <summary>A paragraph of a style, its own settings ("{align=right after=12pt}", see ParagraphLook) taken from its text's start, the rest its pieces.</summary>
    static Paragraph Paragraphed(string? style, string text, string? runStyle = null)
    {
        var (look, rest) = ParagraphLook.Take(text);
        return new Paragraph(style, Inline(rest, runStyle)) { Look = look };
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
        if (trimmed.StartsWith("$$", StringComparison.Ordinal))
            return Kind.Formula;
        if (trimmed == ":::")
            return open ? Kind.Unsure : Kind.ColumnsClose;
        if (trimmed.StartsWith(":::", StringComparison.Ordinal))
            return ColumnsLine().IsMatch(trimmed) ? Kind.ColumnsOpen : open ? Kind.Unsure : Kind.Text;
        if (trimmed == "\\pagebreak")
            return Kind.PageBreak;
        if (BreakLine().IsMatch(trimmed) || open && trimmed.StartsWith("\\", StringComparison.Ordinal) && !trimmed.Contains(' ', StringComparison.Ordinal) && trimmed.Length < 16)
            return open ? Kind.Unsure : Kind.Breaks;
        if (ContentsLine().IsMatch(trimmed) || IndexLine().IsMatch(trimmed) || BibliographyLine().IsMatch(trimmed) || AuthoritiesLine().IsMatch(trimmed)
            || CoverLine().IsMatch(trimmed))
            return open ? Kind.Unsure : Kind.Contents;
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
        if (TableSettingsLine().IsMatch(line))
            return open ? Kind.Unsure : Kind.TableSettings;
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
            // A bar within braces that close is the notation's ({color=red|text}), not a cell's end.
            var braces = 0;
            for (var k = 0; k < row.Length; k++)
            {
                if (row[k] == '\\' && k + 1 < row.Length && row[k + 1] == '|')
                {
                    cell.Append('|');
                    k++;
                }
                else if (row[k] == '\\' && k + 1 < row.Length && row[k + 1] is '{' or '}')
                {
                    // An escaped brace is text, as Inline reads it.
                    cell.Append(row[k]).Append(row[k + 1]);
                    k++;
                }
                else if (row[k] == '{' && row.IndexOf('}', k + 1) > k)
                {
                    braces++;
                    cell.Append('{');
                }
                else if (row[k] == '}' && braces > 0)
                {
                    braces--;
                    cell.Append('}');
                }
                else if (row[k] == '|' && braces == 0)
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
        var looks = new List<IReadOnlyList<IReadOnlyDictionary<string, string>>>();
        var merged = new List<IReadOnlyList<char>>();
        var texts = new List<IReadOnlyList<string>>();
        foreach (var line in lines.Where((_, k) => k != 1))
        {
            var cells = Cells(line);
            var (rowPieces, rowLooks, rowMerged, rowTexts) = (new List<IReadOnlyList<Piece>>(), new List<IReadOnlyDictionary<string, string>>(), new List<char>(), new List<string>());
            // As many cells as the head has: too few filled up empty, too many left out. A cell of "<" alone is merged with the one before
            // it, of "^" alone with the one above (not in the first column or row); settings at a cell's start are its own.
            for (var k = 0; k < dashes.Count; k++)
            {
                var raw = k < cells.Count ? cells[k] : "";
                var mark = raw == "<" && k > 0 ? '<' : raw == "^" && rows.Count > 0 ? '^' : '\0';
                var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (mark == '\0' && CellSettings().Match(raw) is { Success: true } given)
                {
                    settings = SettingsOf(given.Groups[1].Value);
                    raw = raw[given.Length..];
                }
                rowPieces.Add(mark == '\0' ? Inline(raw.Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase), null) : []);
                rowLooks.Add(settings);
                rowMerged.Add(mark);
                rowTexts.Add(mark == '\0' ? raw.Trim() : "");
            }
            rows.Add(rowPieces);
            looks.Add(rowLooks);
            merged.Add(rowMerged);
            texts.Add(rowTexts);
        }
        return new Table(rows, aligns) { Looks = looks, Merged = merged, Texts = texts };
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

    /// <summary>A text without the notation's marks, as it shows while it is being written (a tracked deletion's text left out).</summary>
    static string Plain(string text) =>
        PlainMarks().Replace(PlainLinks().Replace(PlainChanges().Replace(text.Replace("<br>", " ", StringComparison.OrdinalIgnoreCase), ""), "$1"), "").Replace("\\", "", StringComparison.Ordinal);

    /// <summary>A tracked change's marks, and a deletion with its text (see Change).</summary>
    [GeneratedRegex(@"\{--.*?(?:--\d{0,9}\}|$)|\{\+\+|\+\+\d{0,9}\}|\{==|==\d{0,9}\}|¶(?=\+\+|==)", RegexOptions.Singleline)]
    private static partial Regex PlainChanges();

    /// <summary>A text written with the notation's escapes as it reads: each escaped mark itself.</summary>
    static string Unescaped(string text) => EscapedMark().Replace(text, "$1");

    [GeneratedRegex(@"\\([\\*_{}\[\]|<>`#^$.\-+)¶])")]
    private static partial Regex EscapedMark();

    [GeneratedRegex(@"\[([^\]]*)\]\([^)\s]*\)?")]
    private static partial Regex PlainLinks();

    [GeneratedRegex(@"^\{[^{}|\n]*\}\s*|\{[^{}|\n]*\||\[\^[^\]]*\]?|\[(?:" + Objects.Kinds + @")\s+\d[^\]]*\]?|==|[*_`}~^]|<[^>]*>?", RegexOptions.IgnoreCase)]
    private static partial Regex PlainMarks();

    /// <summary>
    /// The pieces of a paragraph's text: **important**, *emphasized* (or _so_), {Style|text}, `a term`, [^note] marks, line breaks
    /// (&lt;br&gt;, a line feed in a table's cell), [text](address) as its text, WordprocessingML (w:r, m:oMath …) as it is; direct
    /// formatting as ==marked==, ~~struck~~, H~2~O, x^2^ and {font=Georgia size=14|text} (see RunLook); a backslash takes the next mark
    /// as text. Text without a mark of its own takes <paramref name="style"/> and <paramref name="look"/>.
    /// </summary>
    public static IReadOnlyList<Piece> Inline(string text, string? style, RunLook? look = null)
    {
        var pieces = new List<Piece>();
        var buffer = new StringBuilder();
        var (important, emphasized) = (false, false);
        var (marked, struck, sub, sup) = (false, false, false, false);
        string? Styled() => important ? "Important" : emphasized ? "Emphasized" : style;
        // The look of text here: the one given, and the compact marks open.
        RunLook? Looked()
        {
            var now = (look ?? RunLook.None).Over(marked ? RunLook.Marked : null).Over(struck ? RunLook.Struck : null)
                .Over(sub ? RunLook.Subscript : null).Over(sup ? RunLook.Superscript : null);
            return now.Empty ? null : now;
        }
        void Flush()
        {
            if (buffer.Length == 0)
                return;
            pieces.Add(new Run(buffer.ToString(), Styled()) { Look = Looked() });
            buffer.Clear();
        }
        // Where a mark of two characters closes after one that opens at k: later, after text that does not end with a space; -1 for nowhere.
        int Closing(string mark, int from)
        {
            for (var at = text.IndexOf(mark, from, StringComparison.Ordinal); at >= 0; at = text.IndexOf(mark, at + 1, StringComparison.Ordinal))
            {
                if (at > from && !char.IsWhiteSpace(text[at - 1]) && text[at - 1] != '\\')
                    return at;
            }
            return -1;
        }
        var k = 0;
        while (k < text.Length)
        {
            var c = text[k];
            var rest = text.AsSpan(k);
            if (c == '\\' && k + 1 < text.Length && "\\*_{}[]|<>`#^$.-+)~=¶".Contains(text[k + 1]))
            {
                buffer.Append(text[k + 1]);
                k += 2;
                continue;
            }
            // ==marked== and ~~struck~~: opening before text that does not begin with a space where they close later, closing after text
            // that does not end with one.
            if (rest.StartsWith("==") || rest.StartsWith("~~"))
            {
                var on = c == '=' ? marked : struck;
                if (on ? k > 0 && !char.IsWhiteSpace(text[k - 1]) : k + 2 < text.Length && !char.IsWhiteSpace(text[k + 2]) && Closing(rest[..2].ToString(), k + 2) > 0)
                {
                    Flush();
                    if (c == '=')
                        marked = !marked;
                    else
                        struck = !struck;
                    k += 2;
                    continue;
                }
            }
            // H~2~O and x^2^: a subscript or superscript of text without spaces, as Pandoc writes them.
            if (c is '~' or '^' && !rest.StartsWith("~~"))
            {
                var on = c == '~' ? sub : sup;
                var close = on ? k : text.IndexOf(c, k + 1);
                if (on || close > k + 1 && !text.AsSpan(k + 1, close - k - 1).ContainsAny(" \t\n") && text[close - 1] != '\\')
                {
                    Flush();
                    if (c == '~')
                        sub = !sub;
                    else
                        sup = !sup;
                    k++;
                    continue;
                }
            }
            // Text of a look of its own: {font=Georgia size=14|text}, {Term color=#C00000|text}.
            if (c == '{' && LookedText().Match(text, k) is { Success: true } looked && RunLook.Parse(looked.Groups[1].Value) is var (lookStyle, given))
            {
                Flush();
                foreach (var piece in Inline(looked.Groups[2].Value, lookStyle ?? Styled(), (Looked() ?? RunLook.None).Over(given)))
                    pieces.Add(piece);
                k += looked.Length;
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
            if (c == '{' && k + 2 < text.Length && text[k + 1] is '+' or '-' or '=' && text[k + 2] == text[k + 1] && ChangeText().Match(text, k) is { Success: true } change)
            {
                Flush();
                var inner = change.Groups[2].Value;
                pieces.Add(new Change(change.Groups[1].Value[0], Inline(inner, style),
                    int.TryParse(change.Groups[3].Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var changeId) ? changeId : null,
                    Unescaped(inner)));
                k += change.Length;
                continue;
            }
            if (c == '{' && StyledText().Match(text, k) is { Success: true } styled)
            {
                Flush();
                foreach (var piece in Inline(styled.Groups[2].Value, styled.Groups[1].Value, Looked()))
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
            if (c == '[' && KeptText().Match(text, k) is { Success: true } kept
                && int.TryParse(kept.Groups[1].Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var id))
            {
                Flush();
                pieces.Add(new Kept(id));
                k += kept.Length;
                continue;
            }
            if (c == '[' && CiteBrackets().Match(text, k) is { Success: true } cite && CiteItems(cite.Groups[1].Value) is { } cited)
            {
                Flush();
                pieces.Add(new CiteMark(cited));
                k += cite.Length;
                continue;
            }
            if (c == '[' && LinkText().Match(text, k) is { Success: true } link)
            {
                Flush();
                pieces.Add(new Link(Inline(link.Groups[1].Value, "Hyperlink", Looked()), link.Groups[2].Value));
                k += link.Length;
                continue;
            }
            if (c == '$' && MathText().Match(text, k) is { Success: true } math)
            {
                Flush();
                pieces.Add(new Formula(math.Groups[1].Value));
                k += math.Length;
                continue;
            }
            if (c == '{' && FieldText().Match(text, k) is { Success: true } field)
            {
                Flush();
                pieces.Add(new FieldMark(field.Groups[1].Value.ToLowerInvariant()) { Look = Looked() });
                k += field.Length;
                continue;
            }
            if (c == '{' && pieces.Count == 0 && buffer.Length == 0 && DropText().Match(text, k) is { Success: true } drop)
            {
                var lines = drop.Groups[1].Success ? int.Parse(drop.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 3;
                pieces.Add(new DropMark(Math.Clamp(lines, 2, 10), drop.Groups[2].Success));
                k += drop.Length;
                continue;
            }
            if (c == '{' && IndexText().Match(text, k) is { Success: true } indexed)
            {
                Flush();
                pieces.Add(new IndexMark(indexed.Groups[1].Value.Trim()));
                k += indexed.Length;
                continue;
            }
            if (c == '{' && AuthorityText().Match(text, k) is { Success: true } authority && Authorities.Parse(authority.Groups[1].Value) is var (shortText, longText, category))
            {
                Flush();
                pieces.Add(new Authority(shortText, longText, category));
                k += authority.Length;
                continue;
            }
            if (c == '{' && AnchorText().Match(text, k) is { Success: true } anchor)
            {
                Flush();
                pieces.Add(new Anchor(anchor.Groups[1].Value));
                k += anchor.Length;
                continue;
            }
            if (c == '{' && ReferenceText().Match(text, k) is { Success: true } reference)
            {
                Flush();
                pieces.Add(new Reference(reference.Groups[2].Value, reference.Groups[1].Value.Equals("page", StringComparison.OrdinalIgnoreCase)) { Look = Looked() });
                k += reference.Length;
                continue;
            }
            if (c == '`' && CodeText().Match(text, k) is { Success: true } code)
            {
                Flush();
                pieces.Add(new Run(code.Groups[1].Value, "Term") { Look = Looked() });
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

    /// <summary>Text of a look of its own: {words|text}, the words settings (see RunLook.Parse), the text holding marks of one level of braces ({page}).</summary>
    [GeneratedRegex(@"\G\{([^{}|\n]+)\|((?:[^{}\\]|\\.|\{(?:[^{}\\]|\\.)*\})*)\}")]
    private static partial Regex LookedText();

    [GeneratedRegex(@"\G\[\^([^\]\s]+)\]")]
    private static partial Regex NoteMarkText();

    [GeneratedRegex(@"\G\[([^\]]*)\]\(([^)\s]*)\)")]
    private static partial Regex LinkText();

    /// <summary>An object's token as Reading writes it (see Objects.Kinds), what may follow its number up to the bracket aside.</summary>
    [GeneratedRegex(@"\G\[(?:" + Objects.Kinds + @")\s+(\d{1,5})(?:[:,\s][^\]\n]*)?\]", RegexOptions.IgnoreCase)]
    private static partial Regex KeptText();

    [GeneratedRegex(@"\G`([^`]+)`")]
    private static partial Regex CodeText();

    [GeneratedRegex(@"\G\{(page|pages|date)\}", RegexOptions.IgnoreCase)]
    private static partial Regex FieldText();

    /// <summary>A drop cap's mark at a paragraph's start: {drop}, {drop 4}, {drop margin}, {drop 2 margin}, the spaces after it left out.</summary>
    [GeneratedRegex(@"\G\{drop(?:\s+(\d{1,2}))?(?:\s+(margin))?\}\s*", RegexOptions.IgnoreCase)]
    private static partial Regex DropText();

    /// <summary>A mark for the index: {index term}, the term what follows the word ("main: sub" a subentry).</summary>
    [GeneratedRegex(@"\G\{index\s+([^{}]+?)\s*\}", RegexOptions.IgnoreCase)]
    private static partial Regex IndexText();

    /// <summary>A citation marked for a table of authorities: {authority Cases: long | short}, {authority short} (see Authorities.Parse).</summary>
    [GeneratedRegex(@"\G\{authority\s+([^{}]+?)\s*\}", RegexOptions.IgnoreCase)]
    private static partial Regex AuthorityText();

    /// <summary>An anchor: {#name}, or {#} alone; a name of letters, digits and _ - . : (see Markup.BookmarkName).</summary>
    [GeneratedRegex(@"\G\{#\s*([\p{L}\p{N}_.:-]*)\s*\}")]
    private static partial Regex AnchorText();

    /// <summary>A reference to an anchor: {ref name} or {page name}.</summary>
    [GeneratedRegex(@"\G\{(ref|page)\s+#?([\p{L}\p{N}_.:-]+)\s*\}", RegexOptions.IgnoreCase)]
    private static partial Regex ReferenceText();

    /// <summary>A formula among the text: $…$, its content neither beginning nor ending with a space, no digit right after it (so that prices stay text).</summary>
    [GeneratedRegex(@"\G\$(?!\s)((?:\\.|[^$\\\n])+?)(?<!\s)\$(?!\d)")]
    private static partial Regex MathText();

    [GeneratedRegex(@"\G<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BreakTag();

    [GeneratedRegex(@"\G<(w|m|wp|a|mc):[A-Za-z]")]
    private static partial Regex RawStart();
}
