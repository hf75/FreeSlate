using System.Globalization;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace FreeSlate.Docx.Citations;

/// <summary>A piece of a citation's or a bibliography entry's text with the formatting Word gives it ("\n" a line break, "\t" a tab).</summary>
public readonly record struct Piece(string Text, bool Italic = false, bool Bold = false, bool Underline = false);

/// <summary>
/// One source of a citation with its own switches (CITATION's \p pages, \v volume, \f prefix, \s suffix, \n without author, \y without
/// year, \t without title; after a \m they belong to the source it adds).
/// </summary>
public sealed record Cited(Source Source, string? Pages = null, string? Volume = null, string? Prefix = null, string? Suffix = null,
    bool NoAuthor = false, bool NoYear = false, bool NoTitle = false);

/// <summary>
/// What a CITATION field asks for (catalog: fields.citation): its sources in order ("CITATION Smi20 \p 33 \m Lee18": Smi20, then Lee18
/// added with \m) and the language its words are in (\l, a Windows language id: 1033 English, 1031 German).
/// </summary>
public sealed partial record CitationRequest(IReadOnlyList<Cited> Sources, int Lcid)
{
    /// <summary>A CITATION field's instruction as Word writes it, its sources found among <paramref name="list"/>; null where it names none of them.</summary>
    public static CitationRequest? Parse(string instruction, SourceList list, int lcid = 1033)
    {
        var tokens = Tokens().Matches(instruction).Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Value).ToList();
        if (tokens.Count < 2 || !tokens[0].Equals("CITATION", StringComparison.OrdinalIgnoreCase))
            return null;
        var cited = new List<Cited>();
        Cited? current = null;
        void Close()
        {
            if (current != null)
                cited.Add(current);
            current = null;
        }
        for (var k = 1; k < tokens.Count; k++)
        {
            var token = tokens[k];
            string? Next() => k + 1 < tokens.Count ? tokens[++k] : null;
            switch (token.ToLowerInvariant())
            {
                case "\\l":
                    if (int.TryParse(Next(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var language))
                        lcid = language;
                    break;
                case "\\m":
                    Close();
                    if (Next() is { } added && list.Find(added) is { } source)
                        current = new Cited(source);
                    break;
                case "\\p" when current != null:
                    current = current with { Pages = Next() };
                    break;
                case "\\v" when current != null:
                    current = current with { Volume = Next() };
                    break;
                case "\\f" when current != null:
                    current = current with { Prefix = Next() };
                    break;
                case "\\s" when current != null:
                    current = current with { Suffix = Next() };
                    break;
                case "\\n" when current != null:
                    current = current with { NoAuthor = true };
                    break;
                case "\\y" when current != null:
                    current = current with { NoYear = true };
                    break;
                case "\\t" when current != null:
                    current = current with { NoTitle = true };
                    break;
                default:
                    if (k == 1 && list.Find(token) is { } first)
                        current = new Cited(first);
                    break;
            }
        }
        Close();
        return cited.Count == 0 ? null : new CitationRequest(cited, lcid);
    }

    [GeneratedRegex("\"([^\"]*)\"|\\S+")]
    private static partial Regex Tokens();

    /// <summary>The CITATION field's instruction for these sources, as Word writes it (" CITATION Smi20 \p 33 \l 1033  \m Lee18 ").</summary>
    public string Instruction()
    {
        var text = new StringBuilder(" CITATION");
        for (var k = 0; k < Sources.Count; k++)
        {
            var cited = Sources[k];
            text.Append(k == 0 ? " " : "  \\m ").Append(cited.Source.Tag);
            static string Quoted(string value) => value.Contains(' ', StringComparison.Ordinal) || value.Length == 0 ? $"\"{value}\"" : value;
            if (cited.Pages != null)
                text.Append(" \\p ").Append(Quoted(cited.Pages));
            if (cited.Volume != null)
                text.Append(" \\v ").Append(Quoted(cited.Volume));
            if (cited.Prefix != null)
                text.Append(" \\f \"").Append(cited.Prefix).Append('"');
            if (cited.Suffix != null)
                text.Append(" \\s \"").Append(cited.Suffix).Append('"');
            if (cited.NoAuthor)
                text.Append(" \\n");
            if (cited.NoYear)
                text.Append(" \\y");
            if (cited.NoTitle)
                text.Append(" \\t");
            if (k == 0)
                text.Append(" \\l ").Append(Lcid.ToString(CultureInfo.InvariantCulture));
        }
        return text.Append(' ').ToString();
    }
}

/// <summary>
/// What a style knows of the document when it formats a citation or the bibliography: all its sources (in the list's order) and the tags
/// in the order the document first cites them (numbered styles number by it; a style may tell apart sources of one author and year by
/// all the others).
/// </summary>
public sealed record CitationContext(SourceList Sources, IReadOnlyList<string> Order)
{
    /// <summary>A source's place among those cited, 1 the first; sources never cited follow in the list's order.</summary>
    public int Number(Source source)
    {
        for (var k = 0; k < Order.Count; k++)
        {
            if (Order[k].Equals(source.Tag, StringComparison.OrdinalIgnoreCase))
                return k + 1;
        }
        var rest = Sources.Sources.Where(s => !Order.Any(o => o.Equals(s.Tag, StringComparison.OrdinalIgnoreCase))).ToList();
        return Order.Count + rest.IndexOf(source) + 1;
    }
}

/// <summary>How a bibliography's entries stand: paragraphs as they are, with a hanging indent (half an inch), or a table of numbers and entries.</summary>
public enum BibliographyLayout
{
    Plain,
    Hanging,
    NumberTable,
}

/// <summary>A bibliography entry: its text, and its number where the layout sets numbers in a column of their own ("[1] ").</summary>
public sealed record BibliographyEntry(IReadOnlyList<Piece> Text, IReadOnlyList<Piece>? Number = null);

/// <summary>A bibliography as a style lays it out: its entries in order and how they stand.</summary>
public sealed record BibliographyList(IReadOnlyList<BibliographyEntry> Entries, BibliographyLayout Layout);

/// <summary>
/// One of Word's citation styles (Word's References > Style: APA, MLA, Chicago …), made by FreeSlate as Word formats them (measured,
/// Word's style sheets not copied): a citation's text for a CITATION field, the bibliography's entries for a BIBLIOGRAPHY field, in
/// English or German as the field's language asks (other languages in English).
/// </summary>
public abstract class CitationStyle
{
    /// <summary>The style's name as Word writes it (b:Sources' StyleName, Word's English name: "APA", "Harvard - Anglia" …).</summary>
    public abstract string Name { get; }

    /// <summary>Word's style sheet for it (b:Sources' SelectedStyle: "\APASixthEditionOfficeOnline.xsl").</summary>
    public abstract string File { get; }

    /// <summary>The version b:Sources names with it, where Word writes one ("6" for APA's sixth edition).</summary>
    public virtual string? Version => null;

    /// <summary>A citation's text.</summary>
    public abstract IReadOnlyList<Piece> Cite(CitationRequest request, CitationContext context);

    /// <summary>The bibliography of all of the document's sources in a language (1033, 1031 …).</summary>
    public abstract BibliographyList Bibliography(CitationContext context, int lcid);

    /// <summary>Word's styles, APA (its default) first.</summary>
    public static IReadOnlyList<CitationStyle> All { get; } =
    [
        new Styles.Apa(), new Styles.Mla(), new Styles.Chicago(), new Styles.Ieee(), new Styles.Harvard(), new Styles.IsoDate(),
        new Styles.IsoNumbers(), new Styles.Turabian(), new Styles.Gb7714(), new Styles.GostName(), new Styles.GostTitle(), new Styles.Sist02(),
    ];

    /// <summary>
    /// A style by its name or Word's style sheet (letters' case and anything but letters and digits aside: "apa", "Harvard-Anglia",
    /// "ISO 690 - Numerical Reference", "\CHICAGO.XSL"); null where Word has none of it.
    /// </summary>
    public static CitationStyle? Named(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        static string Key(string text) => new([.. text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant)]);
        var key = Key(name);
        return All.FirstOrDefault(s => Key(s.Name) == key || Key(s.File) == key || Key(s.File.Replace(".xsl", "", StringComparison.OrdinalIgnoreCase)) == key)
               ?? All.FirstOrDefault(s => s.Aliases.Any(a => Key(a) == key));
    }

    /// <summary>Other names it goes by (German names Word shows, short ones).</summary>
    public virtual IReadOnlyList<string> Aliases => [];
}

/// <summary>
/// Citations and bibliographies as Word's markup (catalog: fields.citation): a citation a CITATION field in a w:citation content control,
/// its result the style's text; a bibliography Word's Insert Bibliography (a Bibliographies building block holding a w:bibliography
/// control and the BIBLIOGRAPHY field, which begins in its first entry and ends in an empty paragraph after them), its entries in the
/// paragraph style Bibliography as the style lays them out.
/// </summary>
public static class CitationMarkup
{
    static string Escaped(string text) => SecurityElement.Escape(text) ?? "";

    /// <summary>Pieces as runs (w:r), proofing off as Word's citations have it.</summary>
    public static string Runs(IEnumerable<Piece> pieces)
    {
        var xml = new StringBuilder();
        foreach (var piece in pieces)
        {
            if (piece.Text.Length == 0)
                continue;
            var properties = (piece.Bold ? "<w:b/><w:bCs/>" : "") + (piece.Italic ? "<w:i/><w:iCs/>" : "") + (piece.Underline ? "<w:u w:val=\"single\"/>" : "") + "<w:noProof/>";
            xml.Append("<w:r><w:rPr>").Append(properties).Append("</w:rPr>");
            var parts = Regex.Split(piece.Text, "(\n|\t)");
            foreach (var part in parts)
            {
                if (part == "\n")
                    xml.Append("<w:br/>");
                else if (part == "\t")
                    xml.Append("<w:tab/>");
                else if (part.Length > 0)
                    xml.Append("<w:t xml:space=\"preserve\">").Append(Escaped(part)).Append("</w:t>");
            }
            xml.Append("</w:r>");
        }
        return xml.ToString();
    }

    /// <summary>A citation as Word's Insert Citation writes it: the field in its content control, its result the citation's text.</summary>
    public static string Citation(CitationRequest request, IReadOnlyList<Piece> text, int id) =>
        $"<w:sdt><w:sdtPr><w:id w:val=\"{id}\"/><w:citation/></w:sdtPr><w:sdtContent>"
        + "<w:r><w:fldChar w:fldCharType=\"begin\"/></w:r>"
        + $"<w:r><w:instrText xml:space=\"preserve\">{Escaped(request.Instruction())}</w:instrText></w:r>"
        + "<w:r><w:fldChar w:fldCharType=\"separate\"/></w:r>" + Runs(text) + "<w:r><w:fldChar w:fldCharType=\"end\"/></w:r></w:sdtContent></w:sdt>";

    /// <summary>
    /// A bibliography as Word's Insert Bibliography writes it, in the paragraph style of the id given (the document's Bibliography), its
    /// language's switch (\l) as given; two content controls' ids.
    /// </summary>
    public static string Bibliography(BibliographyList list, string style, int lcid, int id, int innerId)
    {
        var begin = "<w:r><w:fldChar w:fldCharType=\"begin\"/></w:r>"
                    + $"<w:r><w:instrText xml:space=\"preserve\"> BIBLIOGRAPHY  \\l {lcid.ToString(CultureInfo.InvariantCulture)} </w:instrText></w:r>"
                    + "<w:r><w:fldChar w:fldCharType=\"separate\"/></w:r>";
        var paragraphStyle = $"<w:pStyle w:val=\"{Escaped(style)}\"/>";
        var indent = list.Layout == BibliographyLayout.Hanging ? "<w:ind w:left=\"720\" w:hanging=\"720\"/>" : "";
        var body = new StringBuilder();
        if (list.Layout == BibliographyLayout.NumberTable)
        {
            // Word's IEEE: the field begins in a paragraph of its own; a table of the page's width, a narrow column of numbers.
            body.Append("<w:p><w:pPr><w:rPr><w:noProof/></w:rPr></w:pPr>").Append(begin).Append("</w:p>");
            body.Append("<w:tbl><w:tblPr><w:tblW w:w=\"5000\" w:type=\"pct\"/><w:tblCellSpacing w:w=\"15\" w:type=\"dxa\"/><w:tblCellMar>"
                        + "<w:top w:w=\"15\" w:type=\"dxa\"/><w:left w:w=\"15\" w:type=\"dxa\"/><w:bottom w:w=\"15\" w:type=\"dxa\"/><w:right w:w=\"15\" w:type=\"dxa\"/></w:tblCellMar>"
                        + "<w:tblLook w:val=\"04A0\" w:firstRow=\"1\" w:lastRow=\"0\" w:firstColumn=\"1\" w:lastColumn=\"0\" w:noHBand=\"0\" w:noVBand=\"1\"/></w:tblPr>"
                        + "<w:tblGrid><w:gridCol w:w=\"433\"/><w:gridCol w:w=\"8639\"/></w:tblGrid>");
            foreach (var entry in list.Entries)
            {
                body.Append("<w:tr><w:trPr><w:tblCellSpacing w:w=\"15\" w:type=\"dxa\"/></w:trPr>")
                    .Append("<w:tc><w:tcPr><w:tcW w:w=\"50\" w:type=\"pct\"/><w:hideMark/></w:tcPr><w:p><w:pPr>").Append(paragraphStyle)
                    .Append("<w:rPr><w:noProof/></w:rPr></w:pPr>").Append(Runs(entry.Number ?? [])).Append("</w:p></w:tc>")
                    .Append("<w:tc><w:tcPr><w:tcW w:w=\"0\" w:type=\"auto\"/><w:hideMark/></w:tcPr><w:p><w:pPr>").Append(paragraphStyle)
                    .Append("<w:rPr><w:noProof/></w:rPr></w:pPr>").Append(Runs(entry.Text)).Append("</w:p></w:tc></w:tr>");
            }
            body.Append("</w:tbl>");
        }
        else
        {
            for (var k = 0; k < Math.Max(1, list.Entries.Count); k++)
            {
                body.Append("<w:p><w:pPr>").Append(paragraphStyle).Append(indent).Append("<w:rPr><w:noProof/></w:rPr></w:pPr>");
                if (k == 0)
                    body.Append(begin);
                if (k < list.Entries.Count)
                    body.Append(Runs([.. list.Entries[k].Number ?? [], .. list.Entries[k].Text]));
                body.Append("</w:p>");
            }
        }
        body.Append("<w:p><w:r><w:fldChar w:fldCharType=\"end\"/></w:r></w:p>");
        return $"<w:sdt><w:sdtPr><w:id w:val=\"{id}\"/><w:docPartObj><w:docPartGallery w:val=\"Bibliographies\"/><w:docPartUnique/></w:docPartObj></w:sdtPr><w:sdtContent>"
               + $"<w:sdt><w:sdtPr><w:id w:val=\"{innerId}\"/><w:bibliography/></w:sdtPr><w:sdtContent>{body}</w:sdtContent></w:sdt>"
               + "</w:sdtContent></w:sdt>";
    }
}
