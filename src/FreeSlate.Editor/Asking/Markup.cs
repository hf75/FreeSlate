using System.Globalization;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using FreeSlate.Skia;
using SkiaSharp;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// What blocks of the notation (see Notation) are in a Word document: WordprocessingML of its own kind, with the parts a picture
/// brings, where the marks of footnotes stand, and what a section of columns needs besides (see Made). The styles are the tone's (see
/// Writing.Tones): levels and meanings by their ids, tables in its table look.
/// </summary>
static partial class Markup
{
    /// <summary>
    /// The place the blocks go to: the width of the text there (points), the page setup of its section, a new table there of so many
    /// columns and rows in the tone's look as Word's Insert Table makes it (see WordDocument.TableMarkup), and the markup an object's token
    /// goes in as (see Objects.Markup; null where there is no such object).
    /// </summary>
    public sealed record Place(double TextWidth, SectionFormatting Section, Func<int, int, string> EmptyTable, Func<int, string?>? Kept = null)
    {
        /// <summary>How the paragraph an object's token alone makes is aligned: as the paragraph it stood alone in was (see Objects.AlignOf).</summary>
        public Func<int, string?>? KeptAlign { get; init; }

        /// <summary>The tone's colours for light lines and for rules (hex), which boxes' lines take (see Tone.LineColors).</summary>
        public (string Light, string Dark) Lines { get; init; } = ("D6D3CD", "3F3C37");
    }

    /// <summary>
    /// A block made: its markup (blocks, or nothing where it makes none), the parts it brings, the footnotes whose marks it holds (the
    /// paragraph by its place among the paragraphs it brings, the character offset there, the label), and what is to be done with it
    /// once it is in: a drawing in its first paragraph to float beside the text on a side (left, right), the section after it to go on
    /// on the same page (a section of columns ended).
    /// </summary>
    public sealed record Made(string Blocks, IReadOnlyList<NewPart> Parts, IReadOnlyList<(int Paragraph, int Offset, string Label)> Notes,
        string? Float = null, bool GoOn = false, string? Problem = null)
    {
        /// <summary>Whether the drawing it brings is a box to be made as high as its text once it is in (see WordDocument.FitTextBox).</summary>
        public bool Fit { get; init; }
    }

    const double EmuPerPoint = 12700;

    /// <summary>The namespaces markup given as it is may name by their prefixes, as the document takes them (see WordDocument.InsertMarkup).</summary>
    static readonly (string Prefix, string Uri)[] Namespaces =
    [
        ("w", "http://schemas.openxmlformats.org/wordprocessingml/2006/main"),
        ("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"),
        ("m", "http://schemas.openxmlformats.org/officeDocument/2006/math"),
        ("wp", "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"),
        ("a", "http://schemas.openxmlformats.org/drawingml/2006/main"),
        ("pic", "http://schemas.openxmlformats.org/drawingml/2006/picture"),
        ("c", "http://schemas.openxmlformats.org/drawingml/2006/chart"),
        ("wps", "http://schemas.microsoft.com/office/word/2010/wordprocessingShape"),
        ("wpg", "http://schemas.microsoft.com/office/word/2010/wordprocessingGroup"),
        ("wpc", "http://schemas.microsoft.com/office/word/2010/wordprocessingCanvas"),
        ("wp14", "http://schemas.microsoft.com/office/word/2010/wordprocessingDrawing"),
        ("w14", "http://schemas.microsoft.com/office/word/2010/wordml"),
        ("w15", "http://schemas.microsoft.com/office/word/2012/wordml"),
        ("a14", "http://schemas.microsoft.com/office/drawing/2010/main"),
        ("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006"),
        ("v", "urn:schemas-microsoft-com:vml"),
        ("o", "urn:schemas-microsoft-com:office:office"),
    ];

    /// <summary>A block made at a place (see Made).</summary>
    public static Made Of(Notation.Block block, Place place) => block switch
    {
        Notation.Paragraph paragraph => Paragraph(paragraph.Style, paragraph.Pieces, null, kept: place.Kept, keptAlign: place.KeptAlign),
        Notation.Table table => Table(table, place),
        Notation.Fenced { Kind: "svg" } svg => Svg(svg, place),
        Notation.Fenced { Kind: "wordart" } art => WordArt(art, place),
        Notation.Fenced { Kind: "box" } box => Box(box, place),
        Notation.Fenced { Kind: "shape" } shape => Shape(shape, place),
        Notation.Fenced { Kind: "chart" } chart => Charts.Of(chart, place),
        Notation.Fenced { Kind: "smartart" } diagram => SmartArts.Of(diagram, place),
        // Sections of columns are the writing's to make, which knows where they begin (see Writing.Put, SectionEnd).
        Notation.Columns or Notation.ColumnsEnd => new("", [], []),
        Notation.PageBreak => new("<w:p><w:r><w:br w:type=\"page\"/></w:r></w:p>", [], []),
        Notation.Rule => new("<w:p><w:pPr><w:pBdr><w:bottom w:val=\"single\" w:sz=\"6\" w:space=\"1\" w:color=\"auto\"/></w:pBdr></w:pPr></w:p>", [], []),
        Notation.Raw raw => Checked(raw.Markup) is { } problem ? new("", [], [], Problem: problem) : new(raw.Markup, [], []),
        Notation.MathBlock math => new(Formulas.Display(math.Latex), [], []),
        _ => new("", [], []),
    };

    /// <summary>
    /// The end of a section of columns, given the columns it has: a paragraph that holds its page setup, of the section it ends, as small as
    /// Word draws a paragraph (a line of a point), so that it takes no room. Given no columns, the section before the columns ends so,
    /// as it was set up.
    /// </summary>
    public static string SectionEnd(SectionFormatting section, (int Count, bool Line)? columns)
    {
        static string Twips(double? points) => ((int)Math.Round((points ?? 0) * 20)).ToString(CultureInfo.InvariantCulture);
        var markup = new StringBuilder("<w:p><w:pPr><w:spacing w:before=\"0\" w:after=\"0\" w:line=\"20\" w:lineRule=\"exact\"/><w:rPr><w:sz w:val=\"2\"/><w:szCs w:val=\"2\"/></w:rPr><w:sectPr>");
        var start = columns != null ? "continuous" : section.Start ?? "nextPage";
        markup.Append("<w:type w:val=\"").Append(start).Append("\"/>");
        markup.Append("<w:pgSz w:w=\"").Append(Twips(section.PageWidth ?? 595.3)).Append("\" w:h=\"").Append(Twips(section.PageHeight ?? 841.9)).Append('"');
        if (section.Orientation == "landscape")
            markup.Append(" w:orient=\"landscape\"");
        markup.Append("/>");
        markup.Append("<w:pgMar w:top=\"").Append(Twips(section.Top ?? 72)).Append("\" w:right=\"").Append(Twips(section.Right ?? 72))
            .Append("\" w:bottom=\"").Append(Twips(section.Bottom ?? 72)).Append("\" w:left=\"").Append(Twips(section.Left ?? 72))
            .Append("\" w:header=\"").Append(Twips(section.Header ?? 35.4)).Append("\" w:footer=\"").Append(Twips(section.Footer ?? 35.4))
            .Append("\" w:gutter=\"").Append(Twips(section.Gutter ?? 0)).Append("\"/>");
        // The border round the pages, as the page tool gives it (see TextEdits.Format).
        if (section.Border is { Kind: not "none" } border)
        {
            markup.Append("<w:pgBorders").Append(border.FromPage ? " w:offsetFrom=\"page\"" : "").Append('>');
            // An art border's width in points, a line's in eighths of a point.
            var size = (FreeSlate.Docx.Layout.ArtBorders.Names.Contains(border.Kind) ? Math.Clamp((int)Math.Round(border.Width), 1, 31)
                : Math.Clamp((int)Math.Round(border.Width * 8), 2, 96)).ToString(CultureInfo.InvariantCulture);
            var space = Math.Clamp((int)Math.Round(border.Space), 0, 31).ToString(CultureInfo.InvariantCulture);
            foreach (var side in new[] { "top", "left", "bottom", "right" })
                markup.Append($"<w:{side} w:val=\"{border.Kind}\" w:sz=\"{size}\" w:space=\"{space}\" w:color=\"{border.Color ?? "auto"}\"")
                    .Append(border.Shadow ? " w:shadow=\"1\"/>" : "/>");
            markup.Append("</w:pgBorders>");
        }
        // The lines' numbers, as the page tool gives them (see TextEdits.Format).
        if (section.LineNumbers is { CountBy: > 0 } numbered)
            markup.Append("<w:lnNumType w:countBy=\"").Append(numbered.CountBy.ToString(CultureInfo.InvariantCulture)).Append('"')
                .Append(numbered.Restart is "newSection" or "continuous" ? $" w:restart=\"{numbered.Restart}\"" : "").Append("/>");
        var (count, line) = columns ?? (section.Columns ?? 1, false);
        markup.Append("<w:cols w:space=\"").Append(Twips(section.ColumnSpace ?? 36)).Append('"');
        if (count > 1)
            markup.Append(" w:num=\"").Append(count.ToString(CultureInfo.InvariantCulture)).Append('"');
        if (line)
            markup.Append(" w:sep=\"1\"");
        markup.Append("/>");
        if (section.VerticalAlign is { } align && align != "top")
            markup.Append("<w:vAlign w:val=\"").Append(align).Append("\"/>");
        if (section.TitlePage == true)
            markup.Append("<w:titlePg/>");
        return markup.Append("</w:sectPr></w:pPr></w:p>").ToString();
    }

    /// <summary>A paragraph of a style, its pieces as runs; with an alignment where one is given (a cell's column, a picture's line).</summary>
    static Made Paragraph(string? style, IReadOnlyList<Notation.Piece> pieces, string? align, string? before = null, Func<int, string?>? kept = null,
        Func<int, string?>? keptAlign = null)
    {
        // An object alone in its paragraph stands as it stood alone before, else in a line of its own as the notation's drawings do: centred.
        if (style == null && align == null && before == null && pieces is [Notation.Kept alone])
            align = keptAlign?.Invoke(alone.Id) is { } was ? (was == "left" ? null : was) : "center";
        var markup = new StringBuilder("<w:p>");
        if (style != null || align != null)
        {
            markup.Append("<w:pPr>");
            if (style != null)
                markup.Append("<w:pStyle w:val=\"").Append(Escape(style)).Append("\"/>");
            if (align != null)
                markup.Append("<w:jc w:val=\"").Append(align == "justify" ? "both" : align).Append("\"/>");
            markup.Append("</w:pPr>");
        }
        // Marks at its end (anchors, index marks, as a paragraph reads back) without the spaces before them.
        pieces = TrailingMarksTrimmed(pieces);
        // A caption's anchors number it; another paragraph's are bookmarks round its text (see Notation.Anchor).
        var anchors = new List<string>();
        if (style == CaptionStyle)
            pieces = Numbered(pieces);
        else if (pieces.Any(p => p is Notation.Anchor))
            (pieces, anchors) = Unanchored(pieces);
        var ids = anchors.Select(_ => BookmarkId()).ToList();
        for (var k = 0; k < anchors.Count; k++)
            markup.Append("<w:bookmarkStart w:id=\"").Append(ids[k]).Append("\" w:name=\"").Append(Escape(anchors[k])).Append("\"/>");
        markup.Append(before);
        var notes = new List<(int, int, string)>();
        var offset = before != null ? 1 : 0;
        markup.Append(Runs(pieces, notes, 0, ref offset, kept));
        foreach (var id in ids)
            markup.Append("<w:bookmarkEnd w:id=\"").Append(id).Append("\"/>");
        return new(markup.Append("</w:p>").ToString(), [], notes);
    }

    /// <summary>
    /// A paragraph's pieces with the marks at its end (anchors and index marks, which Reading writes there after a space each) kept, the
    /// spaces before and between them left out, so that a paragraph written back as it reads keeps its text.
    /// </summary>
    static IReadOnlyList<Notation.Piece> TrailingMarksTrimmed(IReadOnlyList<Notation.Piece> pieces)
    {
        var k = pieces.Count;
        while (k > 0 && (pieces[k - 1] is Notation.Anchor or Notation.IndexMark || pieces[k - 1] is Notation.Run { Text: var blank } && string.IsNullOrWhiteSpace(blank)))
            k--;
        if (k == pieces.Count || !pieces.Skip(k).Any(p => p is Notation.Anchor or Notation.IndexMark))
            return pieces;
        var result = pieces.Take(k).ToList();
        if (result.Count > 0 && result[^1] is Notation.Run last)
        {
            result.RemoveAt(result.Count - 1);
            if (last.Text.TrimEnd(' ', '\t') is { Length: > 0 } kept)
                result.Add(last with { Text = kept });
        }
        result.AddRange(pieces.Skip(k).Where(p => p is not Notation.Run));
        return result;
    }

    /// <summary>The style of captions, Word's (whose name is "caption"), which numbers their anchors (see Numbered).</summary>
    public const string CaptionStyle = "Caption";

    /// <summary>The language a write's citations name (CITATION's \l, a Windows language id): the text's where it is written, set as a write begins.</summary>
    public static int CitationLanguage { get; set; } = 1033;

    /// <summary>
    /// The runs of a paragraph's pieces of text, to go in among other text, objects by their tokens as <paramref name="kept"/> gives them,
    /// and where in them footnotes' marks stand (offset, label); a caption's anchors numbering it (see Numbered).
    /// </summary>
    public static (string Runs, IReadOnlyList<(int Offset, string Label)> Notes) RunsOf(IReadOnlyList<Notation.Piece> pieces, Func<int, string?>? kept = null,
        bool caption = false)
    {
        var notes = new List<(int, int, string)>();
        var offset = 0;
        var runs = Runs(caption ? Numbered(pieces) : pieces, notes, 0, ref offset, kept);
        return (runs, notes.Select(n => (n.Item2, n.Item3)).ToList());
    }

    /// <summary>
    /// A caption's label and number (see Numbered): the label's text as written, with its meaning, the label as the SEQ field names its
    /// sequence, and the bookmark round both, none for {#}.
    /// </summary>
    sealed record CaptionNumber(string Lead, string? Style, string Label, string? Name) : Notation.Piece;

    /// <summary>
    /// A caption's pieces, each anchor in it its number (Word's SEQ field, in its own sequence for each label, as Word's Insert Caption
    /// makes it): the word before it its label ("Figure {#sales}", "Abb. {#}"), a bookmark of the anchor's name round the two, which
    /// {ref name} shows as Word's cross-reference to "label and number" does; "Figure" where no word stands before it.
    /// </summary>
    static IReadOnlyList<Notation.Piece> Numbered(IReadOnlyList<Notation.Piece> pieces)
    {
        if (!pieces.Any(p => p is Notation.Anchor))
            return pieces;
        var result = new List<Notation.Piece>();
        foreach (var piece in pieces)
        {
            if (piece is not Notation.Anchor anchor)
            {
                result.Add(piece);
                continue;
            }
            var name = anchor.Name.Length > 0 ? anchor.Name : null;
            if (result.Count > 0 && result[^1] is Notation.Run run && LabelBefore().Match(run.Text) is { Success: true } label)
            {
                result.RemoveAt(result.Count - 1);
                if (label.Groups[1].Length > 0)
                    result.Add(run with { Text = label.Groups[1].Value });
                result.Add(new CaptionNumber(run.Text[label.Groups[2].Index..], run.Style, label.Groups[2].Value, name));
            }
            else
                result.Add(new CaptionNumber("", null, "Figure", name));
        }
        return result;
    }

    /// <summary>A caption's label at the end of the text before its anchor: a word (letters, digits, underscores), a full stop, spaces.</summary>
    [GeneratedRegex(@"^(.*?)(\p{L}[\p{L}\p{N}_]{0,39})\.?\s*$", RegexOptions.Singleline)]
    private static partial Regex LabelBefore();

    /// <summary>A paragraph's pieces without its anchors, the spaces before one at its end left out, and the anchors' names as bookmarks'.</summary>
    static (IReadOnlyList<Notation.Piece> Pieces, List<string> Names) Unanchored(IReadOnlyList<Notation.Piece> pieces)
    {
        var names = pieces.OfType<Notation.Anchor>().Where(a => a.Name.Length > 0).Select(a => BookmarkName(a.Name)).Distinct(StringComparer.Ordinal).ToList();
        var rest = pieces.Where(p => p is not Notation.Anchor).ToList();
        if (pieces[^1] is Notation.Anchor && rest.Count > 0 && rest[^1] is Notation.Run last)
        {
            var trimmed = last.Text.TrimEnd(' ', '\t');
            rest.RemoveAt(rest.Count - 1);
            if (trimmed.Length > 0)
                rest.Add(last with { Text = trimmed });
        }
        return (rest, names);
    }

    /// <summary>
    /// An anchor's name as a bookmark's, as Word allows them: letters, digits and underscores, a letter first, 40 at most; other
    /// characters as underscores, an "x" before a first digit.
    /// </summary>
    public static string BookmarkName(string name)
    {
        var clean = new string([.. name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_')]);
        if (clean.Length == 0 || !(char.IsLetter(clean[0]) || clean[0] == '_'))
            clean = "x" + clean;
        return clean.Length > 40 ? clean[..40] : clean;
    }

    /// <summary>A new bookmark's id, as Contents gives them: a number too great to meet one of the document's.</summary>
    static string BookmarkId() => Random.Shared.Next(100_000_000, 999_999_999).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The runs of pieces of text, counting the characters they make from an offset, the footnotes' marks noted where they stand, objects
    /// by their tokens as <paramref name="kept"/> gives them (one character each; left out where it gives none).
    /// </summary>
    static string Runs(IReadOnlyList<Notation.Piece> pieces, List<(int, int, string)> notes, int paragraph, ref int offset, Func<int, string?>? kept = null)
    {
        var markup = new StringBuilder();
        foreach (var piece in pieces)
        {
            switch (piece)
            {
                case Notation.Run run:
                    foreach (var (text, tab) in Tabbed(Clean(run.Text)))
                    {
                        markup.Append("<w:r>");
                        if (run.Style != null)
                            markup.Append("<w:rPr><w:rStyle w:val=\"").Append(Escape(run.Style)).Append("\"/></w:rPr>");
                        markup.Append(tab ? "<w:tab/>" : $"<w:t xml:space=\"preserve\">{Escape(text)}</w:t>");
                        markup.Append("</w:r>");
                        offset += tab ? 1 : text.Length;
                    }
                    break;
                case Notation.Break:
                    markup.Append("<w:r><w:br/></w:r>");
                    offset++;
                    break;
                case Notation.NoteMark note:
                    notes.Add((paragraph, offset, note.Label));
                    break;
                case Notation.RawRun raw when Checked(raw.Markup) == null:
                    markup.Append(raw.Markup);
                    offset += TextLength(raw.Markup);
                    break;
                case Notation.Kept token when kept?.Invoke(token.Id) is { } run:
                    markup.Append(run);
                    offset++;
                    break;
                case Notation.Link link:
                    // A link as Word's HYPERLINK field, its result the link's text (the document reads it as that text).
                    markup.Append("<w:fldSimple w:instr=\"").Append(Escape(Hyperlink(link.Address))).Append("\">");
                    markup.Append(Runs(link.Pieces, notes, paragraph, ref offset, kept));
                    markup.Append("</w:fldSimple>");
                    break;
                case Notation.Formula formula:
                    markup.Append(Formulas.Inline(formula.Latex));
                    offset++;
                    break;
                case Notation.FieldMark field:
                    // A field Word computes as it lays the page out (one character of the text), its result as saved till then.
                    markup.Append("<w:fldSimple w:instr=\"").Append(field.Name switch { "pages" => " NUMPAGES ", "date" => " DATE ", _ => " PAGE " })
                        .Append("\"><w:r><w:t>").Append(field.Name == "date" ? DateTime.Today.ToString("d", CultureInfo.CurrentCulture) : "1").Append("</w:t></w:r></w:fldSimple>");
                    offset++;
                    break;
                case CaptionNumber number:
                {
                    // Its result 1 till the write's end, which saves what the page shows (see WordDocument.RefreshFields).
                    var id = number.Name != null ? BookmarkId() : null;
                    if (id != null)
                        markup.Append("<w:bookmarkStart w:id=\"").Append(id).Append("\" w:name=\"").Append(Escape(BookmarkName(number.Name!))).Append("\"/>");
                    if (number.Lead.Length > 0)
                        markup.Append(Runs([new Notation.Run(number.Lead, number.Style)], notes, paragraph, ref offset, kept));
                    markup.Append("<w:fldSimple w:instr=\" SEQ ").Append(Escape(number.Label)).Append(" \\* ARABIC \"><w:r><w:t>1</w:t></w:r></w:fldSimple>");
                    offset++;
                    if (id != null)
                        markup.Append("<w:bookmarkEnd w:id=\"").Append(id).Append("\"/>");
                    break;
                }
                case Notation.IndexMark mark:
                    // Word's Mark Entry: an XE field of the term, which shows nothing (no character of the text).
                    markup.Append("<w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText xml:space=\"preserve\"> XE \"")
                        .Append(Escape(mark.Term.Replace("\"", "", StringComparison.Ordinal).Replace(": ", ":", StringComparison.Ordinal)))
                        .Append("\" </w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"end\"/></w:r>");
                    break;
                case Notation.CiteMark cite:
                    // Word's Insert Citation: the CITATION field in its content control, its text the tags till the write's end, when the
                    // document's citation style formats them (see Citing).
                    markup.Append(FreeSlate.Docx.Citations.CitationMarkup.Citation(Citing.Instruction(cite.Items, CitationLanguage),
                        [new FreeSlate.Docx.Citations.Piece("(" + string.Join("; ", cite.Items.Select(i => i.Tag)) + ")")], Random.Shared.Next(1, int.MaxValue)));
                    offset++;
                    break;
                case Notation.Reference reference:
                    // Word's cross-reference (\h: a link to it), its result the anchor's name till the write's end (see above).
                    markup.Append("<w:fldSimple w:instr=\"").Append(reference.Page ? " PAGEREF " : " REF ").Append(Escape(BookmarkName(reference.Name)))
                        .Append(" \\h \"><w:r><w:t>").Append(reference.Page ? "1" : Escape(reference.Name)).Append("</w:t></w:r></w:fldSimple>");
                    offset++;
                    break;
            }
        }
        return markup.ToString();
    }

    /// <summary>A HYPERLINK field's instruction for an address, or for "#" and a bookmark's name; quotes left out.</summary>
    static string Hyperlink(string address)
    {
        var clean = address.Replace("\"", "", StringComparison.Ordinal).Trim();
        return clean.StartsWith('#') ? $" HYPERLINK \\l \"{clean[1..]}\" " : $" HYPERLINK \"{clean}\" ";
    }

    /// <summary>A text's stretches between tabs, and the tabs.</summary>
    static IEnumerable<(string Text, bool Tab)> Tabbed(string text)
    {
        var parts = text.Split('\t');
        for (var k = 0; k < parts.Length; k++)
        {
            if (k > 0)
                yield return ("", true);
            if (parts[k].Length > 0)
                yield return (parts[k], false);
        }
    }

    /// <summary>About how many characters markup given among the text makes: its text, and one for each drawing, equation or field.</summary>
    static int TextLength(string markup)
    {
        try
        {
            var element = Parsed(markup);
            XNamespace w = Namespaces[0].Uri;
            XNamespace m = Namespaces[2].Uri;
            return element.Descendants(w + "t").Sum(t => t.Value.Length) + element.DescendantsAndSelf(w + "drawing").Count()
                + element.DescendantsAndSelf(m + "oMath").Count(o => o.Ancestors(m + "oMath").FirstOrDefault() == null);
        }
        catch (XmlException)
        {
            return 0;
        }
    }

    /// <summary>A table in the tone's look, its columns of even width over the text, the first row its head; its cells' text aligned as the columns say.</summary>
    static Made Table(Notation.Table table, Place place)
    {
        var columns = table.Aligns.Count;
        var rows = table.Rows.Count;
        var empty = place.EmptyTable(columns, rows);
        const string Cell = "<w:p><w:pPr><w:pStyle w:val=\"TableText\"/></w:pPr></w:p>";
        var parts = empty.Split(Cell);
        if (parts.Length != columns * rows + 1)
            return new("", [], [], Problem: "The table could not be made.");
        var markup = new StringBuilder(parts[0]);
        var notes = new List<(int, int, string)>();
        for (var k = 0; k < columns * rows; k++)
        {
            var (row, column) = (k / columns, k % columns);
            var align = table.Aligns[column];
            markup.Append("<w:p><w:pPr><w:pStyle w:val=\"TableText\"/>");
            if (align is "center" or "right")
                markup.Append("<w:jc w:val=\"").Append(align).Append("\"/>");
            markup.Append("</w:pPr>");
            var offset = 0;
            markup.Append(Runs(table.Rows[row][column], notes, k, ref offset, place.Kept));
            markup.Append("</w:p>").Append(parts[k + 1]);
        }
        return new(markup.ToString(), [], notes);
    }

    /// <summary>
    /// A vector picture: the SVG as Word keeps one (an SVG part, with a PNG drawn from it for programs that read no SVG), in a line of its
    /// own, centred (or as its align says), as wide as its width says (8cm, 60%, 300pt, 400px), else as its own size says, no wider than
    /// the text; its description its alt; its caption under it; float=left or right puts it beside the text that follows.
    /// </summary>
    static Made Svg(Notation.Fenced svg, Place place)
    {
        var body = svg.Body.Trim();
        if (!body.Contains("<svg", StringComparison.OrdinalIgnoreCase))
            return new("", [], [], Problem: "An svg block holds an <svg> element.");
        var bytes = Encoding.UTF8.GetBytes(body);
        var drawing = SvgDrawing.Load(bytes);
        if (drawing == null)
            return new("", [], [], Problem: "The SVG could not be read.");
        // Its own size, CSS pixels to points; else as wide as the text, its proportions kept.
        var (ownWidth, ownHeight) = (drawing.Size.Width * 0.75, drawing.Size.Height * 0.75);
        if (ownWidth <= 0 || ownHeight <= 0)
            (ownWidth, ownHeight) = (place.TextWidth, place.TextWidth / 2);
        var width = Length(svg.Settings.GetValueOrDefault("width"), place.TextWidth) ?? Math.Min(ownWidth, place.TextWidth);
        width = Math.Clamp(width, 12, place.TextWidth);
        var height = width * ownHeight / ownWidth;
        var png = Png(drawing, width, height);
        var alt = svg.Settings.GetValueOrDefault("alt") ?? svg.Settings.GetValueOrDefault("description") ?? "";
        var (cx, cy) = (Math.Max(1, (long)Math.Round(width * EmuPerPoint)), Math.Max(1, (long)Math.Round(height * EmuPerPoint)));
        var described = Escape(alt);
        var run = $"<w:r><w:drawing><wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\"><wp:extent cx=\"{cx}\" cy=\"{cy}\"/><wp:effectExtent l=\"0\" t=\"0\" r=\"0\" b=\"0\"/>"
            + $"<wp:docPr id=\"1\" name=\"Grafik\" descr=\"{described}\"/><wp:cNvGraphicFramePr><a:graphicFrameLocks noChangeAspect=\"1\"/></wp:cNvGraphicFramePr>"
            + "<a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><pic:pic>"
            + $"<pic:nvPicPr><pic:cNvPr id=\"0\" name=\"Grafik\" descr=\"{described}\"/><pic:cNvPicPr/></pic:nvPicPr>"
            + "<pic:blipFill><a:blip r:embed=\"{fallback}\"><a:extLst><a:ext uri=\"{96DAC541-7B7A-43D3-8B79-37D633B846F1}\">"
            + "<asvg:svgBlip xmlns:asvg=\"http://schemas.microsoft.com/office/drawing/2016/SVG/main\" r:embed=\"{vector}\"/></a:ext></a:extLst></a:blip>"
            + "<a:stretch><a:fillRect/></a:stretch></pic:blipFill>"
            + $"<pic:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"{cx}\" cy=\"{cy}\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr>"
            + "</pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r>";
        const string Image = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image";
        NewPart[] parts = [new("fallback", "/word/media/image1.png", "image/png", Image, png), new("vector", "/word/media/image1.svg", "image/svg+xml", Image, bytes)];
        return Placed(run, parts, svg.Settings, place);
    }

    /// <summary>
    /// A drawing's run as a block: in a line of its own (aligned as its settings say, centred by default) with its caption under it in the
    /// tone's caption style; or, where it floats, its run alone, to go in at the start of the paragraph after it (see Writing).
    /// </summary>
    public static Made Placed(string run, IReadOnlyList<NewPart> parts, IReadOnlyDictionary<string, string> settings, Place place)
    {
        var floats = settings.GetValueOrDefault("float")?.ToLowerInvariant() is "left" or "right" ? settings["float"].ToLowerInvariant() : null;
        if (floats != null)
            return new(run, parts, [], Float: floats);
        var align = settings.GetValueOrDefault("align")?.ToLowerInvariant() is "left" or "right" ? settings["align"].ToLowerInvariant() : "center";
        // Kept with the next paragraph only where that is its caption: drawings one after another would otherwise hold each other and the
        // headings between them together, pushing them all to the next page as one.
        var caption = settings.GetValueOrDefault("caption") is { Length: > 0 } given ? given : null;
        var markup = $"<w:p><w:pPr>{(caption != null ? "<w:keepNext/>" : "")}<w:jc w:val=\"{align}\"/></w:pPr>{run}</w:p>";
        if (caption != null)
            markup += Paragraph("Caption", Notation.Inline(caption, null), align == "center" ? "center" : null).Blocks;
        return new(markup, parts, []);
    }

    /// <summary>A PNG of an SVG drawn at about 144 dots an inch for its size in points, white where it is not drawn over.</summary>
    static byte[] Png(SvgDrawing drawing, double width, double height)
    {
        var (w, h) = (Math.Clamp((int)Math.Round(width * 2), 1, 4000), Math.Clamp((int)Math.Round(height * 2), 1, 4000));
        using var surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(w / Math.Max(1, drawing.Size.Width), h / Math.Max(1, drawing.Size.Height));
        canvas.DrawPicture(drawing.Picture);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>A length in points: given in cm, mm, in, pt or px (CSS pixels), or as a share of the text's width (60%); null where none is given or it reads as none.</summary>
    public static double? Length(string? given, double whole)
    {
        if (string.IsNullOrWhiteSpace(given))
            return null;
        var text = given.Trim().ToLowerInvariant();
        var unit = new string([.. text.SkipWhile(c => char.IsDigit(c) || c is '.' or ',' or '-')]).Trim();
        var number = text[..(text.Length - unit.Length)].Trim().Replace(',', '.');
        if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || value <= 0)
            return null;
        return unit switch
        {
            "cm" => value * 72 / 2.54,
            "mm" => value * 72 / 25.4,
            "in" => value * 72,
            "pt" => value,
            "px" or "" => value * 0.75,
            "%" => whole * value / 100,
            _ => null,
        };
    }

    /// <summary>
    /// WordArt: a text box in a line of its own (or floating, see Placed), as wide as the text unless its width says so, its text in its
    /// size, font and colours, bent as its shape says (Word's text warps: arch, circle, wave, inflate …, or their own names, textArchUp …),
    /// with a soft shadow unless shadow=none; a fill of one colour or a gradient of two or three ("#1F3A93,#7A3FB0"), an outline of a
    /// colour, a glow of one.
    /// </summary>
    static Made WordArt(Notation.Fenced art, Place place)
    {
        var lines = art.Body.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        if (lines.Count == 0)
            return new("", [], [], Problem: "A wordart block holds its text.");
        var settings = art.Settings;
        var size = double.TryParse(settings.GetValueOrDefault("size")?.Replace("pt", "", StringComparison.OrdinalIgnoreCase), NumberStyles.Float, CultureInfo.InvariantCulture, out var given)
            ? Math.Clamp(given, 8, 200) : 40;
        var shape = Warp(settings.GetValueOrDefault("shape") ?? settings.GetValueOrDefault("warp") ?? "none");
        var width = Math.Min(Length(settings.GetValueOrDefault("width"), place.TextWidth) ?? place.TextWidth, place.TextWidth);
        var height = Length(settings.GetValueOrDefault("height"), place.TextWidth) ?? size * lines.Count * (shape == "textNoShape" ? 1.45 : 2.1);
        var (cx, cy) = ((long)Math.Round(width * EmuPerPoint), (long)Math.Round(height * EmuPerPoint));
        var properties = new StringBuilder();
        if (settings.GetValueOrDefault("font") is { Length: > 0 } font)
        {
            var family = Escape(font);
            properties.Append($"<w:rFonts w:ascii=\"{family}\" w:hAnsi=\"{family}\" w:eastAsia=\"{family}\" w:cs=\"{family}\"/>");
        }
        if (settings.GetValueOrDefault("bold")?.ToLowerInvariant() is not ("no" or "false" or "0"))
            properties.Append("<w:b/><w:bCs/>");
        if (settings.GetValueOrDefault("italic")?.ToLowerInvariant() is "yes" or "true" or "1")
            properties.Append("<w:i/><w:iCs/>");
        var colors = (settings.GetValueOrDefault("fill") ?? settings.GetValueOrDefault("color") ?? "#1F3A93").Split(',', ';', ' ')
            .Select(Hex).Where(c => c != null).Cast<string>().Take(3).ToList();
        if (colors.Count == 0)
            colors.Add("1F3A93");
        properties.Append("<w:color w:val=\"").Append(colors[0]).Append("\"/>");
        var halfPoints = ((int)Math.Round(size * 2)).ToString(CultureInfo.InvariantCulture);
        properties.Append($"<w:sz w:val=\"{halfPoints}\"/><w:szCs w:val=\"{halfPoints}\"/>");
        if (Hex(settings.GetValueOrDefault("glow")) is { } glow)
            properties.Append($"<w14:glow w14:rad=\"101600\"><w14:srgbClr w14:val=\"{glow}\"><w14:alpha w14:val=\"60000\"/></w14:srgbClr></w14:glow>");
        if (settings.GetValueOrDefault("shadow")?.ToLowerInvariant() is not ("no" or "none" or "false" or "0"))
            properties.Append("<w14:shadow w14:blurRad=\"50800\" w14:dist=\"25400\" w14:dir=\"5400000\" w14:sx=\"100000\" w14:sy=\"100000\" w14:kx=\"0\" w14:ky=\"0\" w14:algn=\"tl\">"
                + "<w14:srgbClr w14:val=\"000000\"><w14:alpha w14:val=\"70000\"/></w14:srgbClr></w14:shadow>");
        if (Hex(settings.GetValueOrDefault("outline")) is { } outline)
            properties.Append($"<w14:textOutline w14:w=\"12700\" w14:cap=\"flat\" w14:cmpd=\"sng\" w14:algn=\"ctr\"><w14:solidFill><w14:srgbClr w14:val=\"{outline}\"/></w14:solidFill>"
                + "<w14:prstDash w14:val=\"solid\"/><w14:round/></w14:textOutline>");
        if (colors.Count == 1)
            properties.Append($"<w14:textFill><w14:solidFill><w14:srgbClr w14:val=\"{colors[0]}\"/></w14:solidFill></w14:textFill>");
        else
        {
            properties.Append("<w14:textFill><w14:gradFill><w14:gsLst>");
            for (var k = 0; k < colors.Count; k++)
                properties.Append($"<w14:gs w14:pos=\"{k * 100000 / (colors.Count - 1)}\"><w14:srgbClr w14:val=\"{colors[k]}\"/></w14:gs>");
            properties.Append("</w14:gsLst><w14:lin w14:ang=\"0\" w14:scaled=\"0\"/></w14:gradFill></w14:textFill>");
        }
        var props = properties.ToString();
        var text = new StringBuilder();
        foreach (var line in lines)
            text.Append($"<w:p><w:pPr><w:jc w:val=\"center\"/><w:spacing w:before=\"0\" w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"/><w:rPr>{props}</w:rPr></w:pPr>")
                .Append($"<w:r><w:rPr>{props}</w:rPr><w:t xml:space=\"preserve\">{Escape(Clean(line))}</w:t></w:r></w:p>");
        var run = $"<w:r><w:drawing><wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\"><wp:extent cx=\"{cx}\" cy=\"{cy}\"/><wp:effectExtent l=\"0\" t=\"0\" r=\"0\" b=\"0\"/>"
            + "<wp:docPr id=\"1\" name=\"WordArt\"/><wp:cNvGraphicFramePr/>"
            + "<a:graphic><a:graphicData uri=\"http://schemas.microsoft.com/office/word/2010/wordprocessingShape\"><wps:wsp><wps:cNvSpPr txBox=\"1\"/>"
            + $"<wps:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"{cx}\" cy=\"{cy}\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom><a:noFill/><a:ln><a:noFill/></a:ln></wps:spPr>"
            + $"<wps:txbx><w:txbxContent>{text}</w:txbxContent></wps:txbx>"
            + "<wps:bodyPr rot=\"0\" vert=\"horz\" wrap=\"square\" lIns=\"0\" tIns=\"0\" rIns=\"0\" bIns=\"0\" anchor=\"ctr\" anchorCtr=\"0\">"
            + $"<a:prstTxWarp prst=\"{shape}\"><a:avLst/></a:prstTxWarp><a:noAutofit/></wps:bodyPr></wps:wsp></a:graphicData></a:graphic></wp:inline></w:drawing></w:r>";
        return Placed(run, [], settings, place);
    }

    /// <summary>
    /// A box: Word's text box (wps:txbx) holding the paragraphs the notation gives within it (levels, points, meanings, links, objects), on a
    /// fill of the cells' colours, in a line of the table lines' kinds in the tone's colours (fine by default), its corners round where its
    /// shape says so; as wide as the text unless its width says so, in a line of its own or floating beside the paragraph after it (see
    /// Placed), and as high as its text once it is in (see Made.Fit).
    /// </summary>
    static Made Box(Notation.Fenced box, Place place)
    {
        var inner = Notation.Parse(box.Body, final: true);
        var paragraphs = new StringBuilder();
        var lines = 0;
        var width = Math.Clamp(Length(box.Settings.GetValueOrDefault("width"), place.TextWidth) ?? place.TextWidth, 36, place.TextWidth);
        foreach (var block in inner.Blocks)
        {
            if (block is not Notation.Paragraph paragraph)
                return new("", [], [], Problem: "A box holds paragraphs, points and their meanings, no tables, graphics or columns.");
            // Notes do not go in text boxes (Word keeps none there): their marks are left out.
            var pieces = paragraph.Pieces.Where(p => p is not Notation.NoteMark).ToList();
            paragraphs.Append(Paragraph(paragraph.Style, pieces, null, kept: place.Kept).Blocks);
            var characters = pieces.OfType<Notation.Run>().Sum(r => r.Text.Length) + 1;
            lines += (int)Math.Ceiling(characters * 5.4 / Math.Max(60, width - 20)) + (paragraph.Style is "Heading1" or "Heading2" or "Title" ? 1 : 0);
        }
        if (paragraphs.Length == 0)
            return new("", [], [], Problem: "A box holds its text: ```box with paragraphs inside.");
        var settings = box.Settings;
        var fill = TableWords.FillHex(settings.GetValueOrDefault("fill"), out var knownFill);
        if (!knownFill)
            return new("", [], [], Problem: $"fill={settings["fill"]} is no fill (gray, yellow, orange, green, blue, rose, violet or none).");
        var line = settings.GetValueOrDefault("line")?.Trim().ToLowerInvariant() ?? "fine";
        var kind = FreeSlate.Editor.Writing.Tables.Lines.FirstOrDefault(l => l.NameKey[4..].Equals(line, StringComparison.OrdinalIgnoreCase));
        if (kind == null && line != "none")
            return new("", [], [], Problem: $"line={line} is no line (fine, strong, double, dotted, dashed or none).");
        var shape = settings.GetValueOrDefault("shape")?.Trim().ToLowerInvariant() is "rounded" or "round" ? "roundRect" : "rect";
        var height = Math.Max(24, lines * 15.5 + 20);
        var (cx, cy) = ((long)Math.Round(width * EmuPerPoint), (long)Math.Round(height * EmuPerPoint));
        var outline = kind == null ? "<a:ln><a:noFill/></a:ln>" : Outline(kind, place.Lines);
        var filled = fill != null ? $"<a:solidFill><a:srgbClr val=\"{fill}\"/></a:solidFill>" : "<a:noFill/>";
        var run = $"<w:r><w:drawing><wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\"><wp:extent cx=\"{cx}\" cy=\"{cy}\"/><wp:effectExtent l=\"0\" t=\"0\" r=\"0\" b=\"0\"/>"
            + "<wp:docPr id=\"1\" name=\"Kasten\"/><wp:cNvGraphicFramePr/>"
            + "<a:graphic><a:graphicData uri=\"http://schemas.microsoft.com/office/word/2010/wordprocessingShape\"><wps:wsp><wps:cNvSpPr txBox=\"1\"/>"
            + $"<wps:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"{cx}\" cy=\"{cy}\"/></a:xfrm><a:prstGeom prst=\"{shape}\"><a:avLst/></a:prstGeom>{filled}{outline}</wps:spPr>"
            + $"<wps:txbx><w:txbxContent>{paragraphs}</w:txbxContent></wps:txbx>"
            + "<wps:bodyPr rot=\"0\" vert=\"horz\" wrap=\"square\" lIns=\"108000\" tIns=\"79200\" rIns=\"108000\" bIns=\"79200\" anchor=\"t\" anchorCtr=\"0\"><a:spAutoFit/></wps:bodyPr>"
            + "</wps:wsp></a:graphicData></a:graphic></wp:inline></w:drawing></w:r>";
        var made = Placed(run, [], settings.ContainsKey("align") ? settings : new Dictionary<string, string>(settings, StringComparer.OrdinalIgnoreCase) { ["align"] = "left" }, place);
        return made with { Fit = true };
    }

    /// <summary>Short names of preset shapes the notation takes besides DrawingML's own (any case).</summary>
    static readonly Dictionary<string, string> ShapeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["arrow"] = "rightArrow", ["right-arrow"] = "rightArrow", ["left-arrow"] = "leftArrow", ["up-arrow"] = "upArrow", ["down-arrow"] = "downArrow",
        ["double-arrow"] = "leftRightArrow", ["circle"] = "ellipse", ["oval"] = "ellipse", ["square"] = "rect", ["rectangle"] = "rect",
        ["rounded"] = "roundRect", ["star"] = "star5", ["callout"] = "wedgeRoundRectCallout", ["speech"] = "wedgeEllipseCallout",
        ["thought"] = "cloudCallout", ["banner"] = "ribbon2", ["scroll"] = "horizontalScroll", ["decision"] = "flowChartDecision",
        ["process"] = "flowChartProcess", ["terminator"] = "flowChartTerminator", ["document"] = "flowChartDocument", ["data"] = "flowChartInputOutput",
        ["smiley"] = "smileyFace", ["lightning"] = "lightningBolt", ["gear"] = "gear6", ["cylinder"] = "can", ["cross"] = "plus",
        ["explosion"] = "irregularSeal1", ["burst"] = "irregularSeal2", ["arrow-line"] = "straightConnector1", ["connector"] = "straightConnector1",
        ["home-plate"] = "homePlate", ["tab"] = "homePlate",
    };

    /// <summary>A preset shape's name as DrawingML has it, from its own name in any case or a short one (see ShapeNames); null for none.</summary>
    public static string? PresetName(string? given)
    {
        var name = given?.Trim() ?? "";
        if (name.Length == 0)
            return "rect";
        if (ShapeNames.TryGetValue(name, out var known))
            return known;
        return FreeSlate.Drawing.Geometry.PresetGeometries.Names.FirstOrDefault(n => n.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A shape of Word's (```shape type=star5 fill=yellow): a preset shape (wps:wsp) of any of DrawingML's 187 kinds or the notation's short
    /// names, filled (a fill's name or #RRGGBB, a strong fill white text on it), outlined as a box is or in a colour (color=#…), turned and
    /// mirrored, with a shadow, with paragraphs of the notation inside it, in the middle; lines and arrows (line, arrow-line) a line of their
    /// length with arrowheads (ends=start|end|both). As large as its width and height say, else four centimetres across and as high as its
    /// kind is (a text box's grows with its text). In a line of its own or floating beside the paragraph after it.
    /// </summary>
    static Made Shape(Notation.Fenced block, Place place)
    {
        var settings = block.Settings;
        if (PresetName(settings.GetValueOrDefault("type") ?? settings.GetValueOrDefault("shape")) is not { } preset)
            return new("", [], [], Problem: $"type={settings.GetValueOrDefault("type")} is no shape: one of Word's (rect, ellipse, triangle, rightArrow, star5, heart, cloud, wedgeRoundRectCallout, flowChartDecision …) or arrow, circle, star, callout, speech, thought, banner, decision, process, line, arrow-line.");
        var isLine = preset is "line" or "lineInv" or "straightConnector1" || preset.StartsWith("bentConnector", StringComparison.Ordinal) || preset.StartsWith("curvedConnector", StringComparison.Ordinal);
        var inner = Notation.Parse(block.Body, final: true);
        var paragraphs = new StringBuilder();
        // A rectangle without a height of its own grows with its text as a box does, its text at the left (see Box); others centre it.
        var growing = !isLine && Length(settings.GetValueOrDefault("height"), place.TextWidth) == null && preset is "rect" or "roundRect";
        foreach (var piece in inner.Blocks)
        {
            if (piece is not Notation.Paragraph paragraph)
                return new("", [], [], Problem: "A shape holds paragraphs, points and their meanings, no tables, graphics or columns.");
            var pieces = paragraph.Pieces.Where(p => p is not Notation.NoteMark).ToList();
            paragraphs.Append(Paragraph(paragraph.Style, pieces, growing ? null : "center", kept: place.Kept).Blocks);
        }
        if (isLine && paragraphs.Length > 0)
            return new("", [], [], Problem: "A line holds no text: write the text beside it, or take a shape that holds it (a callout).");
        var fill = TableWords.FillHex(settings.GetValueOrDefault("fill") ?? (isLine ? "none" : "#2F6DB5"), out var knownFill);
        if (!knownFill)
            return new("", [], [], Problem: $"fill={settings["fill"]} is no fill (gray, yellow, orange, green, blue, rose, violet, none or #RRGGBB).");
        var line = settings.GetValueOrDefault("line")?.Trim().ToLowerInvariant() ?? (isLine || fill == null ? "strong" : "none");
        var kind = FreeSlate.Editor.Writing.Tables.Lines.FirstOrDefault(l => l.NameKey[4..].Equals(line, StringComparison.OrdinalIgnoreCase));
        if (kind == null && line != "none")
            return new("", [], [], Problem: $"line={line} is no line (fine, strong, double, dotted, dashed or none).");
        var color = TableWords.FillHex(settings.GetValueOrDefault("color"), out var knownColor);
        if (!knownColor)
            return new("", [], [], Problem: $"color={settings["color"]} is no colour (#RRGGBB or a fill's name).");
        var width = Math.Clamp(Length(settings.GetValueOrDefault("width"), place.TextWidth) ?? (isLine ? place.TextWidth * 0.5 : 4 / 2.54 * 72), 6, place.TextWidth);
        var given = Length(settings.GetValueOrDefault("height"), place.TextWidth);
        var square = preset is "ellipse" or "star4" or "star5" or "star6" or "star7" or "star8" or "star10" or "star12" or "star16" or "star24" or "star32"
            or "heart" or "smileyFace" or "sun" or "moon" or "gear6" or "gear9" or "donut" or "noSmoking" or "plus" or "octagon" or "hexagon"
            or "pentagon" or "decagon" or "dodecagon" or "heptagon" or "cube" or "teardrop" or "pie" or "chord" or "blockArc" or "flowChartConnector"
            or "mathPlus" or "mathMinus" or "mathMultiply" or "mathDivide" or "mathEqual" or "mathNotEqual" or "irregularSeal1" or "irregularSeal2";
        var height = isLine ? Math.Max(0, given ?? 0) : given ?? width * (square ? 1 : preset.Contains("Arrow", StringComparison.Ordinal) ? 0.5 : 0.62);
        var fit = growing && paragraphs.Length > 0;
        var (cx, cy) = ((long)Math.Round(width * EmuPerPoint), (long)Math.Round(height * EmuPerPoint));
        var outline = kind == null ? "<a:ln><a:noFill/></a:ln>" : Outline(kind, color != null ? (color, color) : fill != null && line != "none" && !isLine ? ("FFFFFF", "FFFFFF") : place.Lines);
        if (isLine)
        {
            var ends = settings.GetValueOrDefault("ends")?.Trim().ToLowerInvariant() ?? (preset == "straightConnector1" ? "end" : "none");
            var arrows = (ends is "start" or "both" ? "<a:headEnd type=\"triangle\"/>" : "") + (ends is "end" or "both" ? "<a:tailEnd type=\"triangle\"/>" : "");
            outline = outline.Replace("</a:ln>", arrows + "</a:ln>", StringComparison.Ordinal);
        }
        var filled = fill != null ? $"<a:solidFill><a:srgbClr val=\"{fill}\"/></a:solidFill>" : "<a:noFill/>";
        // Text on a strong fill is white: the fill's lightness (relative luminance) under the middle.
        var dark = fill != null && Luminance(fill) < 0.45;
        if (dark)
            paragraphs = new StringBuilder(Recoloured(paragraphs.ToString(), "FFFFFF"));
        var turn = Number(settings.GetValueOrDefault("turn")) is { } degrees ? $" rot=\"{(long)Math.Round((((degrees % 360) + 360) % 360) * 60000)}\"" : "";
        var flip = settings.GetValueOrDefault("flip")?.Trim().ToLowerInvariant() switch { "across" or "h" or "horizontal" => " flipH=\"1\"", "down" or "v" or "vertical" => " flipV=\"1\"", "both" => " flipH=\"1\" flipV=\"1\"", _ => "" };
        var shadow = settings.GetValueOrDefault("shadow")?.Trim().ToLowerInvariant() is "yes" or "true"
            ? "<a:effectLst><a:outerShdw blurRad=\"50800\" dist=\"25400\" dir=\"5400000\" algn=\"t\" rotWithShape=\"0\"><a:srgbClr val=\"000000\"><a:alpha val=\"35000\"/></a:srgbClr></a:outerShdw></a:effectLst>"
            : "";
        var alt = settings.GetValueOrDefault("alt") ?? settings.GetValueOrDefault("description") ?? "";
        var text = paragraphs.Length > 0
            ? $"<wps:txbx><w:txbxContent>{paragraphs}</w:txbxContent></wps:txbx>"
              // A rectangle growing with its text is a box, with a box's margins (see Box); other shapes keep their text closer.
              + (fit
                  ? "<wps:bodyPr rot=\"0\" vert=\"horz\" wrap=\"square\" lIns=\"108000\" tIns=\"79200\" rIns=\"108000\" bIns=\"79200\" anchor=\"t\" anchorCtr=\"0\"><a:spAutoFit/></wps:bodyPr>"
                  : "<wps:bodyPr rot=\"0\" vert=\"horz\" wrap=\"square\" lIns=\"72000\" tIns=\"36000\" rIns=\"72000\" bIns=\"36000\" anchor=\"ctr\" anchorCtr=\"0\"><a:noAutofit/></wps:bodyPr>")
            : "<wps:bodyPr rot=\"0\" vert=\"horz\" wrap=\"square\" anchor=\"ctr\" anchorCtr=\"0\"><a:noAutofit/></wps:bodyPr>";
        var run = $"<w:r><w:drawing><wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\"><wp:extent cx=\"{cx}\" cy=\"{cy}\"/><wp:effectExtent l=\"0\" t=\"0\" r=\"0\" b=\"0\"/>"
            + $"<wp:docPr id=\"1\" name=\"Form\" descr=\"{Escape(alt)}\"/><wp:cNvGraphicFramePr/>"
            + "<a:graphic><a:graphicData uri=\"http://schemas.microsoft.com/office/word/2010/wordprocessingShape\"><wps:wsp>"
            + (isLine ? "<wps:cNvCnPr/>" : "<wps:cNvSpPr/>")
            + $"<wps:spPr><a:xfrm{turn}{flip}><a:off x=\"0\" y=\"0\"/><a:ext cx=\"{cx}\" cy=\"{cy}\"/></a:xfrm><a:prstGeom prst=\"{preset}\"><a:avLst/></a:prstGeom>{(isLine ? "<a:noFill/>" : filled)}{outline}{shadow}</wps:spPr>"
            + text + "</wps:wsp></a:graphicData></a:graphic></wp:inline></w:drawing></w:r>";
        var made = Placed(run, [], settings, place);
        return made with { Fit = fit };
    }

    static double? Number(string? text) =>
        double.TryParse(text?.Trim().TrimEnd('°'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>The relative luminance of a colour (hex RRGGBB), 0 black to 1 white.</summary>
    static double Luminance(string hex)
    {
        static double Channel(int value) => value / 255.0 is var c && c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        var rgb = Convert.ToInt32(hex, 16);
        return 0.2126 * Channel(rgb >> 16 & 255) + 0.7152 * Channel(rgb >> 8 & 255) + 0.0722 * Channel(rgb & 255);
    }

    /// <summary>Paragraphs' runs given a colour (white text on a strong fill), their own properties kept, the colour where the schema has it.</summary>
    static string Recoloured(string paragraphs, string color)
    {
        var root = Parsed(paragraphs);
        XNamespace w = Namespaces[0].Uri;
        string[] before = ["rStyle", "rFonts", "b", "bCs", "i", "iCs", "caps", "smallCaps", "strike", "dstrike", "outline", "shadow", "emboss", "imprint", "noProof", "snapToGrid", "vanish", "webHidden"];
        void Colour(XElement properties)
        {
            properties.Elements(w + "color").Remove();
            var colour = new XElement(w + "color", new XAttribute(w + "val", color));
            if (properties.Elements().LastOrDefault(e => before.Contains(e.Name.LocalName)) is { } last)
                last.AddAfterSelf(colour);
            else
                properties.AddFirst(colour);
        }
        foreach (var run in root.Descendants(w + "r"))
        {
            var properties = run.Element(w + "rPr");
            if (properties == null)
                run.AddFirst(properties = new XElement(w + "rPr"));
            Colour(properties);
        }
        foreach (var paragraph in root.Elements(w + "p"))
        {
            var pPr = paragraph.Element(w + "pPr");
            if (pPr == null)
                paragraph.AddFirst(pPr = new XElement(w + "pPr"));
            var mark = pPr.Element(w + "rPr");
            if (mark == null)
                pPr.Add(mark = new XElement(w + "rPr"));
            Colour(mark);
        }
        return string.Concat(root.Elements().Select(e => e.ToString(SaveOptions.DisableFormatting)));
    }

    /// <summary>A shape's line of a kind of the table lines' (see Tables.Lines) in the tone's colours: its width, its colour, double, dotted or dashed.</summary>
    static string Outline(FreeSlate.Editor.Writing.LineKind kind, (string Light, string Dark) colors)
    {
        var color = kind.Rule ? colors.Dark : colors.Light;
        var width = (long)Math.Round((kind.Kind == "double" ? Math.Max(2.25, kind.Width * 3) : kind.Width) * EmuPerPoint);
        var compound = kind.Kind == "double" ? " cmpd=\"dbl\"" : "";
        var dash = kind.Kind switch { "dotted" => "<a:prstDash val=\"sysDot\"/>", "dashed" => "<a:prstDash val=\"dash\"/>", _ => "" };
        return $"<a:ln w=\"{width}\"{compound}><a:solidFill><a:srgbClr val=\"{color}\"/></a:solidFill>{dash}</a:ln>";
    }

    /// <summary>Word's text warp for a shape's name: its own name (textArchUp …) or a short one (arch, circle, wave …); none for straight text.</summary>
    static string Warp(string name) => name.Trim().ToLowerInvariant() switch
    {
        "arch" or "arch-up" or "archup" => "textArchUp",
        "arch-down" or "archdown" => "textArchDown",
        "circle" => "textCircle",
        "button" => "textButton",
        "wave" or "wave1" => "textWave1",
        "wave2" => "textWave2",
        "double-wave" or "doublewave" => "textDoubleWave1",
        "inflate" => "textInflate",
        "deflate" => "textDeflate",
        "inflate-top" => "textInflateTop",
        "inflate-bottom" => "textInflateBottom",
        "deflate-top" => "textDeflateTop",
        "deflate-bottom" => "textDeflateBottom",
        "slant-up" => "textSlantUp",
        "slant-down" => "textSlantDown",
        "triangle" => "textTriangle",
        "triangle-down" => "textTriangleInverted",
        "chevron" => "textChevron",
        "chevron-down" => "textChevronInverted",
        "can" or "can-up" => "textCanUp",
        "can-down" => "textCanDown",
        "fade-right" => "textFadeRight",
        "fade-left" => "textFadeLeft",
        "fade-up" => "textFadeUp",
        "fade-down" => "textFadeDown",
        "cascade-up" => "textCascadeUp",
        "cascade-down" => "textCascadeDown",
        "ring" => "textRingInside",
        "stop" => "textStop",
        "" or "none" or "plain" or "straight" => "textNoShape",
        var other when other.StartsWith("text", StringComparison.Ordinal) && other.Length > 4 => "text" + char.ToUpperInvariant(name.Trim()[4]) + name.Trim()[5..],
        _ => "textNoShape",
    };

    /// <summary>The short name of Word's text warp as the notation writes it (see Warp), else Word's own name; none for straight text.</summary>
    public static string ShapeName(string warp) => warp switch
    {
        "textNoShape" => "none",
        "textArchUp" => "arch",
        "textArchDown" => "arch-down",
        "textCircle" => "circle",
        "textButton" => "button",
        "textWave1" => "wave",
        "textWave2" => "wave2",
        "textDoubleWave1" => "double-wave",
        "textInflate" => "inflate",
        "textDeflate" => "deflate",
        "textSlantUp" => "slant-up",
        "textSlantDown" => "slant-down",
        "textTriangle" => "triangle",
        "textTriangleInverted" => "triangle-down",
        "textChevron" => "chevron",
        "textChevronInverted" => "chevron-down",
        "textCanUp" => "can",
        "textCanDown" => "can-down",
        "textFadeRight" => "fade-right",
        "textFadeLeft" => "fade-left",
        "textFadeUp" => "fade-up",
        "textFadeDown" => "fade-down",
        "textCascadeUp" => "cascade-up",
        "textCascadeDown" => "cascade-down",
        "textRingInside" => "ring",
        "textStop" => "stop",
        _ => warp,
    };

    /// <summary>A colour as six hex digits (from #RRGGBB, RRGGBB or #RGB), or null.</summary>
    static string? Hex(string? given)
    {
        if (string.IsNullOrWhiteSpace(given))
            return null;
        var hex = given.Trim().TrimStart('#');
        if (hex.Length == 3)
            hex = string.Concat(hex.Select(c => $"{c}{c}"));
        return hex.Length == 6 && hex.All(Uri.IsHexDigit) ? hex.ToUpperInvariant() : null;
    }

    /// <summary>Text without what XML cannot hold (control characters but the tab).</summary>
    static string Clean(string text) => string.Concat(text.Where(c => c == '\t' || c >= ' '));

    static string Escape(string text) => SecurityElement.Escape(text) ?? "";

    /// <summary>Markup given as it is read with the usual prefixes declared: null where it is well-formed, else why not.</summary>
    public static string? Checked(string markup)
    {
        try
        {
            Parsed(markup);
            return null;
        }
        catch (XmlException e)
        {
            return $"This WordprocessingML is not well-formed ({e.Message}): {(markup.Length > 120 ? markup[..120] + "…" : markup)}";
        }
    }

    /// <summary>Markup read with the usual prefixes declared, under an element of its own ("root").</summary>
    public static XElement Parsed(string markup)
    {
        var declarations = string.Concat(Namespaces.Select(n => $" xmlns:{n.Prefix}=\"{n.Uri}\""));
        return XElement.Parse($"<root{declarations}>{markup}</root>");
    }
}
