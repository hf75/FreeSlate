using System.Globalization;
using System.Security;
using System.Text;
using System.Xml.Linq;
using FreeSlate.Docx;

namespace FreeSlate;

/// <summary>
/// An envelope as Word's Mailings > Envelopes > Add to Document puts one before the letter: its size by Word's name for it (DL, C4, C5,
/// C6), the addressee's lines and the sender's.
/// </summary>
public sealed record Envelope(string Size, IReadOnlyList<string> To, IReadOnlyList<string> From);

/// <summary>
/// A sheet of labels as Word's Mailings > Labels makes one (see WordDocument.LabelSheets): its product's number, the paper (twips), its
/// top and side margins, the labels across and down, a label's width and height, the gap between two across, the indent of a label's
/// text from its sides and the space before its first line (twips, as Word sets them for the product).
/// </summary>
public sealed record LabelSheet(string Product, int PaperWidth, int PaperHeight, int Top, int Side, int Across, int Down, int Width, int Height, int Gap,
    int Indent, int Before = 111)
{
    /// <summary>A label's size in millimetres, "70 × 37 mm".</summary>
    public string Size => FormattableString.Invariant($"{Math.Round(Width / 56.6929, 1)} × {Math.Round(Height / 56.6929, 1)} mm");
}

/// <summary>
/// Mailings as Word's Mailings tab makes them: a form letter (Start Mail Merge > Letters, w:mailMerge in the settings), an envelope added
/// to the document (a section of its own before the letter), a sheet of labels (a table of the sheet's labels in a section of its own).
/// </summary>
public sealed partial class WordDocument
{
    /// <summary>
    /// Whether the document is a form letter, Word's main document of a mail merge of letters (w:mailMerge in the settings). Set, a
    /// change of its own, as Word saves one set up without a data source (measured with Word 365 by COM, MailMerge.MainDocumentType of
    /// letters and Document.Save: w:mainDocumentType formLetters, w:dataType textFile, w:activeRecord -1); taken away, a document of its
    /// own again (Word's Normal Word Document).
    /// </summary>
    /// <exception cref="NotSupportedException">The document has no settings part; nothing changes.</exception>
    public bool FormLetter
    {
        get
        {
            Read();
            return SettingsPart() is { } part && context.Load(part).Root?.Element(Wml.W + "mailMerge") is { } merge
                && merge.Child("mainDocumentType").Val() == "formLetters";
        }
        set
        {
            if (FormLetter == value)
                return;
            var part = SettingsPart() ?? throw new NotSupportedException("The document has no settings part.");
            ChangeParts(new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase)
            {
                [part] = Setting("mailMerge", value
                    ? p => $"<{p}mailMerge><{p}mainDocumentType {p}val=\"formLetters\"/><{p}dataType {p}val=\"textFile\"/><{p}activeRecord {p}val=\"-1\"/></{p}mailMerge>"
                    : null),
            });
        }
    }

    /// <summary>
    /// Word's envelopes by its names for them (all lying across): the paper (twips), the top margin, where the addressee's frame stands
    /// on it (x, y) as Envelope.Insert places it (measured with Word 365 by COM: DL 220 × 110 mm, C5 229 × 162, C6 162 × 114 and C4 324 ×
    /// 229; the sender's frame at 721, 1201 on each, the margins 567 at the sides and 708 below).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, (int Width, int Height, int Top, int X, int Y)> EnvelopeSizes =
        new Dictionary<string, (int, int, int, int, int)>(StringComparer.OrdinalIgnoreCase)
        {
            ["DL"] = (12474, 6237, 360, 5761, 3119),
            ["C5"] = (12984, 9185, 360, 5761, 3119),
            ["C6"] = (9185, 6463, 360, 5761, 4593),
            ["C4"] = (18370, 12984, 732, 4321, 3232),
        };

    const string EnvelopeAddressName = "envelope address", EnvelopeReturnName = "envelope return";

    /// <summary>The paragraphs of the envelope at the document's start (see EnvelopeOf), its section's last one ending it; empty where it has none.</summary>
    public IReadOnlyList<int> EnvelopeParagraphs()
    {
        if (Beginning is not { } first || SectionEnd(first) is not { } end || end == Last())
            return [];
        var paragraphs = new List<int>();
        var addressed = false;
        for (int? paragraph = first; paragraph is { } p; paragraph = Next(p))
        {
            if (TableOf(p) != null)
                return [];
            paragraphs.Add(p);
            var name = ParagraphStyle(p) is { } id ? StyleName(id) : null;
            addressed |= name is not null && (name.Equals(EnvelopeAddressName, StringComparison.OrdinalIgnoreCase) || name.Equals(EnvelopeReturnName, StringComparison.OrdinalIgnoreCase));
            if (p == end)
                break;
        }
        return addressed ? paragraphs : [];
    }

    /// <summary>The body's last paragraph.</summary>
    int? Last()
    {
        int? last = null;
        for (int? paragraph = Beginning; paragraph is { } p; paragraph = Next(p))
            last = p;
        return last;
    }

    /// <summary>The first paragraph of the document after its envelope (see EnvelopeOf), its first where it has none: where the letter begins.</summary>
    public int? AfterEnvelope => EnvelopeParagraphs() is { Count: > 0 } envelope ? Next(envelope[^1]) : Beginning;

    /// <summary>
    /// The envelope at the document's start, as Word's Add to Document puts one: a section of its own before the letter whose paragraphs
    /// are in the styles envelope return and envelope address; its size by the section's paper (a size of its own as width × height in
    /// millimetres), the two addresses' lines; null where the document begins otherwise.
    /// </summary>
    public Envelope? EnvelopeOf()
    {
        if (EnvelopeParagraphs() is not { Count: > 0 } paragraphs)
            return null;
        var section = SectionOf(paragraphs[0]);
        var (width, height) = ((int)Math.Round((section.PageWidth ?? 0) * 20), (int)Math.Round((section.PageHeight ?? 0) * 20));
        var size = EnvelopeSizes.FirstOrDefault(s => Math.Abs(s.Value.Width - Math.Max(width, height)) < 30 && Math.Abs(s.Value.Height - Math.Min(width, height)) < 30).Key
            ?? FormattableString.Invariant($"{Math.Round(Math.Max(width, height) / 56.6929)} × {Math.Round(Math.Min(width, height) / 56.6929)} mm");
        List<string> Lines(string style) => [.. paragraphs.Where(p => ParagraphStyle(p) is { } id && StyleName(id)?.Equals(style, StringComparison.OrdinalIgnoreCase) == true).Select(Text)];
        return new Envelope(size, Lines(EnvelopeAddressName), Lines(EnvelopeReturnName));
    }

    /// <summary>
    /// The document given an envelope before the letter as Word's Mailings > Envelopes > Add to Document gives it (measured with Word 365
    /// by COM, Envelope.Insert): a section of its own at the start, on the envelope's paper lying across (margins 18 pt above as Word sets
    /// them for DL, C5 and C6, 567 twips at the sides, 708 below, the paper from the printer's manual feed, its pages numbered from 0), an
    /// empty paragraph, the sender's lines in the style envelope return and the addressee's in envelope address, each in a frame those
    /// styles place on the page (the addressee's 3 inches wide and 1.5 high where Word puts it for the size, the sender's at the top left
    /// in 10 pt); the styles defined where the document lacks them (in its own font: Word's take the theme's headings font), their frames
    /// set for the size. One in its place where the document has one; null takes it away. One change.
    /// </summary>
    /// <exception cref="ArgumentException">The size is none of Word's named ones (see EnvelopeSizes), or there is no addressee.</exception>
    public void SetEnvelope(Envelope? envelope)
    {
        Read();
        if (envelope != null && !EnvelopeSizes.ContainsKey(envelope.Size))
            throw new ArgumentException("An envelope is DL, C4, C5 or C6.", nameof(envelope));
        if (envelope != null && envelope.To.Count == 0)
            throw new ArgumentException("An envelope has the addressee's lines.", nameof(envelope));
        var start = Changes;
        if (EnvelopeParagraphs() is { Count: > 0 } had && Next(had[^1]) is { } letter)
        {
            RemoveBetween(had[0], letter);
            Remove(had[0]);
        }
        if (envelope != null && Beginning is { } opening && TableOf(opening) != null)
            throw new NotSupportedException("The document begins with a table: an envelope goes before a paragraph.");
        if (envelope != null)
        {
            var size = EnvelopeSizes[envelope.Size];
            var address = StyleIdNamed(EnvelopeAddressName) ?? "EnvelopeAddress";
            var sender = StyleIdNamed(EnvelopeReturnName) ?? "EnvelopeReturn";
            var normal = StyleIdNamed("Normal") is { } id ? $"<w:basedOn w:val=\"{SecurityElement.Escape(id)}\"/>" : "";
            static string Frame(int width, int x, int y) =>
                $"<w:framePr w:w=\"{width}\" w:h=\"2160\" w:hRule=\"exact\" w:hSpace=\"141\" w:wrap=\"auto\" w:vAnchor=\"page\" w:hAnchor=\"page\" w:x=\"{x}\" w:y=\"{y}\"/>";
            DefineStyles($"<w:styles xmlns:w=\"{Wml.W.NamespaceName}\">"
                + $"<w:style w:type=\"paragraph\" w:styleId=\"{SecurityElement.Escape(address)}\"><w:name w:val=\"{EnvelopeAddressName}\"/>{normal}<w:uiPriority w:val=\"99\"/><w:unhideWhenUsed/>"
                + $"<w:pPr>{Frame(4319, size.X, size.Y)}<w:spacing w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"/></w:pPr></w:style>"
                + $"<w:style w:type=\"paragraph\" w:styleId=\"{SecurityElement.Escape(sender)}\"><w:name w:val=\"{EnvelopeReturnName}\"/>{normal}<w:uiPriority w:val=\"99\"/><w:unhideWhenUsed/>"
                + $"<w:pPr>{Frame(4320, 721, 1201)}<w:spacing w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"/></w:pPr><w:rPr><w:sz w:val=\"20\"/><w:szCs w:val=\"20\"/></w:rPr></w:style>"
                + "</w:styles>");
            string Lines(IEnumerable<string> lines, string style) => string.Concat(lines.Select(line =>
                $"<w:p><w:pPr><w:pStyle w:val=\"{SecurityElement.Escape(style)}\"/><w:framePr w:wrap=\"auto\"/></w:pPr>"
                + (line.Length > 0 ? $"<w:r><w:t xml:space=\"preserve\">{SecurityElement.Escape(line)}</w:t></w:r>" : "") + "</w:p>"));
            var markup = "<w:p><w:pPr><w:spacing w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"/></w:pPr></w:p>" + Lines(envelope.From, sender) + Lines(envelope.To, address)
                + $"<w:p><w:pPr><w:sectPr><w:pgSz w:w=\"{size.Width}\" w:h=\"{size.Height}\" w:orient=\"landscape\"/><w:pgMar w:top=\"{size.Top}\" w:right=\"567\" w:bottom=\"708\" w:left=\"567\" w:header=\"720\" w:footer=\"720\" w:gutter=\"0\"/>"
                + "<w:paperSrc w:first=\"15\"/><w:pgNumType w:start=\"0\"/><w:cols w:space=\"720\"/></w:sectPr></w:pPr></w:p>";
            if (Beginning is { } first)
                InsertBlocks(first, markup, before: true);
        }
        Combine(Changes - start);
    }

    /// <summary>
    /// Word's label sheets by their products (Mailings > Labels > Options), as Word makes a sheet of each (measured with Word 365 by COM,
    /// MailingLabel.CreateNewDocument: all on A4, its paper 11905 × 16837 twips; Avery Zweckform's 3474, 70 × 37 mm, 3 × 8 without
    /// margins; 3475 and 3490, 70 × 36 mm; 3422, 70 × 35 mm; 3425, 105 × 57 mm, 2 × 5; 3651, 52.5 × 29.7 mm, 4 × 10; Avery's L7160 and
    /// J8160, 63.5 × 38.1 mm, 3 × 7 with gaps of 2.5 mm; L7161, 63.5 × 46.6 mm, 3 × 6; L7162, 99.1 × 33.9 mm, 2 × 8; L7163, 99.1 × 38.1
    /// mm, 2 × 7; L7165, 99.1 × 67.7 mm, 2 × 4).
    /// </summary>
    public static readonly IReadOnlyList<LabelSheet> LabelSheets =
    [
        new("3474", 11905, 16837, 0, 0, 3, 8, 3968, 2103, 0, 99),
        new("3475", 11905, 16837, 255, 0, 3, 8, 3968, 2040, 0, 99),
        new("3490", 11905, 16837, 255, 0, 3, 8, 3968, 2040, 0, 99),
        new("3422", 11905, 16837, 481, 0, 3, 8, 3968, 1984, 0, 99),
        new("3425", 11905, 16837, 340, 0, 2, 5, 5952, 3231, 0, 144),
        new("3651", 11905, 16837, 0, 0, 4, 10, 2976, 1683, 0, 74),
        new("L7160", 11905, 16837, 858, 408, 3, 7, 3600, 2160, 144, 90),
        new("J8160", 11905, 16837, 858, 408, 3, 7, 3600, 2160, 144, 90),
        new("L7161", 11905, 16837, 500, 408, 3, 6, 3600, 2639, 144, 90),
        new("L7162", 11905, 16837, 738, 264, 2, 8, 5616, 1920, 144, 140),
        new("L7163", 11905, 16837, 858, 264, 2, 7, 5616, 2160, 144, 140),
        new("L7165", 11905, 16837, 735, 264, 2, 4, 5616, 3839, 144, 140),
    ];

    /// <summary>
    /// The sheet of labels the document holds (see SetLabels): its first table that is laid out as a product's sheet (fixed, its rows of
    /// the label's height exactly, its columns the labels' and the gaps' widths) in a section of the sheet's paper and margins; the sheet,
    /// the table's first paragraph and its last (its last cell's, or the empty one after it where the reader reads that one as a paragraph
    /// of its own: a hidden mark joins it with the table), and the labels' texts, their lines split by line feeds, row by row; null where it
    /// has none.
    /// </summary>
    public (LabelSheet Sheet, int First, int Last, IReadOnlyList<string> Labels)? LabelsOf()
    {
        Read();
        for (int? paragraph = Beginning; paragraph is { } p; paragraph = Next(p))
        {
            if (TableOf(p) is not { } table)
                continue;
            if (Found(p) is not var (_, top) || top is not TableBlock block)
                return null;
            var grid = block.Element.Child("tblGrid")?.Elements(Wml.W + "gridCol").Select(g => (int)(g.Number("w") ?? 0)).ToList() ?? [];
            var heights = block.Element.Elements(Wml.W + "tr").Select(r => r.Child("trPr").Child("trHeight")).ToList();
            if (heights.Count == 0 || heights.Any(h => h.Attr("hRule") != "exact") || heights.Select(h => h.Val()).Distinct().Count() != 1
                || block.Element.Child("tblPr").Child("tblLayout").Attr("type") != "fixed")
                return null;
            var height = (int)(heights[0].Number("val") ?? 0);
            SectionFormatting section;
            try
            {
                section = SectionOf(p);
            }
            catch (NotSupportedException)
            {
                return null;
            }
            int Twips(double? points) => (int)Math.Round((points ?? 0) * 20);
            var sheet = LabelSheets.FirstOrDefault(s => s.Height == height && Math.Abs(Twips(section.Top) - s.Top) <= 2 && Math.Abs(Twips(section.Left) - s.Side) <= 2
                && grid.SequenceEqual(Grid(s)));
            if (sheet == null || CellsOf(table) is not { } cells)
                return null;
            var labels = new List<string>();
            foreach (var cell in cells.Cells.Where(c => sheet.Gap == 0 || c.Left % 2 == 0))
                labels.Add(string.Join("\n", cell.Paragraphs.Select(Text)));
            var last = cells.Cells[^1].Last;
            if (Next(last) is { } after && Text(after).Length == 0)
                last = after;
            return (sheet, cells.Cells[0].First, last, labels);
        }
        return null;
    }

    /// <summary>
    /// The document made a sheet of labels as Word's Mailings > Labels makes one (measured with Word 365 by COM,
    /// MailingLabel.CreateNewDocument): a table of the sheet's labels without lines, laid out fixed, indented and its cells' margins as
    /// Word sets them, its rows exactly the label's height and kept whole, a gap column between two labels across where the sheet has
    /// gaps, each label's lines in paragraphs indented from its sides, the first with space before it, single lines without space after
    /// them (as the table style Word's sheets name, Table Grid, gives them); after it the body's last paragraph, its mark hidden, so that
    /// the sheet takes no page more; the sheet's paper and margins the section's (Word's sheet takes its paper from the printer's manual
    /// feed, w:paperSrc, which the section's formatting does not set). The labels fill the sheet's cells in order, rows on as many pages
    /// as they need; one label alone fills the whole sheet (Word's Full page of the same label). Word makes a sheet a document of its
    /// own: the document is to hold nothing but its last, empty paragraph, or to be a sheet already, whose table is given anew in place.
    /// One change.
    /// </summary>
    /// <exception cref="ArgumentException">No label is given.</exception>
    /// <exception cref="NotSupportedException">The document holds more than an empty paragraph and is no sheet; nothing changes.</exception>
    public void SetLabels(LabelSheet sheet, IReadOnlyList<string> labels)
    {
        if (labels.Count == 0)
            throw new ArgumentException("A sheet of labels has at least one label.", nameof(labels));
        Read();
        var had = LabelsOf() is var (_, sheetFirst, _, _) && TableOf(sheetFirst) is { } sheetTable ? sheetTable : (int?)null;
        if (had == null && (Beginning is not { } blank || Next(blank) != null || Text(blank).Length > 0 || TableOf(blank) != null))
            throw new NotSupportedException("A sheet of labels is a document of its own: the document is to be empty.");
        var start = Changes;
        // No header or footer (Word's new sheet has none), which would take its room from the labels.
        foreach (var kind in new[] { "default", "first", "even" })
        {
            foreach (var footer in new[] { false, true })
            {
                if (Sections() is { Count: > 0 } sections && Own(sections[^1].Properties, kind, footer) != null)
                    Change(blocks[^1], blocks[^1], (stretch, _) => TextEdits.RemoveReference(stretch, BodySection()!, kind, footer), lastSection: true);
            }
        }
        var cells = labels.Count == 1 ? Enumerable.Repeat(labels[0], sheet.Across * sheet.Down).ToList() : labels.ToList();
        var rows = (cells.Count + sheet.Across - 1) / sheet.Across;
        var table = new StringBuilder("<w:tbl><w:tblPr><w:tblW w:w=\"0\" w:type=\"auto\"/><w:tblInd w:w=\"-15\" w:type=\"dxa\"/><w:tblBorders>");
        foreach (var side in new[] { "top", "left", "bottom", "right", "insideH", "insideV" })
            table.Append($"<w:{side} w:val=\"none\" w:sz=\"0\" w:space=\"0\" w:color=\"auto\"/>");
        table.Append("</w:tblBorders><w:tblLayout w:type=\"fixed\"/><w:tblCellMar><w:left w:w=\"15\" w:type=\"dxa\"/><w:right w:w=\"15\" w:type=\"dxa\"/></w:tblCellMar>")
            .Append("<w:tblLook w:val=\"0000\" w:firstRow=\"0\" w:lastRow=\"0\" w:firstColumn=\"0\" w:lastColumn=\"0\" w:noHBand=\"0\" w:noVBand=\"0\"/></w:tblPr><w:tblGrid>");
        foreach (var width in Grid(sheet))
            table.Append("<w:gridCol w:w=\"").Append(width.ToString(CultureInfo.InvariantCulture)).Append("\"/>");
        table.Append("</w:tblGrid>");
        var indent = $"<w:ind w:left=\"{sheet.Indent}\" w:right=\"{sheet.Indent}\"/>";
        const string Single = " w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"";
        for (var row = 0; row < rows; row++)
        {
            table.Append("<w:tr><w:tblPrEx><w:tblCellMar><w:top w:w=\"0\" w:type=\"dxa\"/><w:bottom w:w=\"0\" w:type=\"dxa\"/></w:tblCellMar></w:tblPrEx>")
                .Append($"<w:trPr><w:cantSplit/><w:trHeight w:hRule=\"exact\" w:val=\"{sheet.Height}\"/></w:trPr>");
            for (var across = 0; across < sheet.Across; across++)
            {
                if (across > 0 && sheet.Gap > 0)
                    table.Append($"<w:tc><w:tcPr><w:tcW w:w=\"{sheet.Gap}\" w:type=\"dxa\"/></w:tcPr><w:p><w:pPr><w:spacing{Single}/>{indent}</w:pPr></w:p></w:tc>");
                var index = row * sheet.Across + across;
                var lines = index < cells.Count ? cells[index].Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n') : [""];
                table.Append($"<w:tc><w:tcPr><w:tcW w:w=\"{sheet.Width}\" w:type=\"dxa\"/></w:tcPr>");
                for (var k = 0; k < lines.Length; k++)
                    table.Append("<w:p><w:pPr><w:spacing").Append(k == 0 ? $" w:before=\"{sheet.Before}\"" : "").Append(Single).Append("/>").Append(indent).Append("</w:pPr>")
                        .Append(lines[k].Length > 0 ? $"<w:r><w:t xml:space=\"preserve\">{SecurityElement.Escape(lines[k])}</w:t></w:r>" : "").Append("</w:p>");
                table.Append("</w:tc>");
            }
            table.Append("</w:tr>");
        }
        table.Append("</w:tbl>");
        var setup = new SectionFormatting
        {
            PageWidth = sheet.PaperWidth / 20.0, PageHeight = sheet.PaperHeight / 20.0, Orientation = "portrait", Top = sheet.Top / 20.0, Bottom = 0,
            Left = sheet.Side / 20.0, Right = sheet.Side / 20.0, Header = 36, Footer = 36,
        };
        if (had is { } old)
        {
            // The sheet the document is already: its table given in place (the hidden paragraph after it goes with the table the reader
            // reads, so that the table cannot be taken away alone), its section the new sheet's.
            var (model, top) = TableById(old);
            Change(top, top, (stretch, _) =>
            {
                var element = stretch.Find(model.Element) ?? throw new NotSupportedException("The table is not where it was read.");
                var at = stretch.Text.Start(element);
                return stretch.Spliced([(at, stretch.Text.End(at), table.ToString())]);
            });
            if (CellsOf(TableOf(Beginning!.Value) ?? old) is { } made)
                Format(made.Cells[0].First, setup);
        }
        else
        {
            var end = Beginning!.Value;
            InsertBlocks(end, table.ToString(), before: true);
            Format(end, setup);
            Format(end, new ParagraphFormatting { Left = sheet.Indent / 20.0, Right = sheet.Indent / 20.0 });
            HideMark(end);
        }
        Combine(Changes - start);
    }

    /// <summary>A sheet's table's columns: the labels' widths, a gap's between two where the sheet has gaps.</summary>
    static List<int> Grid(LabelSheet sheet) => [.. Enumerable.Range(0, sheet.Across * 2 - 1).Where(k => k % 2 == 0 || sheet.Gap > 0).Select(k => k % 2 == 0 ? sheet.Width : sheet.Gap)];

    /// <summary>A paragraph's mark hidden (w:vanish in its w:pPr's w:rPr), as Word hides the one after a sheet of labels. One change.</summary>
    void HideMark(int paragraph)
    {
        var (p, top) = Find(paragraph);
        Change(top, top, (stretch, _) =>
        {
            var element = stretch.Find(p.Parts?[^1] ?? p.Element) ?? throw new NotSupportedException("The paragraph is not where it was read.");
            var w = element.Name.Namespace;
            var properties = element.Element(w + "pPr");
            if (properties == null)
                element.AddFirst(properties = new XElement(w + "pPr"));
            var mark = properties.Element(w + "rPr");
            if (mark == null)
            {
                mark = new XElement(w + "rPr");
                if (properties.Elements().FirstOrDefault(e => e.Name.LocalName is "sectPr" or "pPrChange") is { } after)
                    after.AddBeforeSelf(mark);
                else
                    properties.Add(mark);
            }
            if (mark.Element(w + "vanish") == null)
            {
                string[] later = ["webHidden", "color", "spacing", "w", "kern", "position", "sz", "szCs", "highlight", "u", "effect", "bdr", "shd", "fitText",
                    "vertAlign", "rtl", "cs", "em", "lang", "eastAsianLayout", "specVanish", "oMath"];
                if (mark.Elements().FirstOrDefault(e => later.Contains(e.Name.LocalName)) is { } after)
                    after.AddBeforeSelf(new XElement(w + "vanish"));
                else
                    mark.Add(new XElement(w + "vanish"));
            }
            return stretch.Written([(element, [element])]);
        });
    }
}
