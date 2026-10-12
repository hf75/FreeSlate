using System.Xml.Linq;
using FreeSlate.Drawing;

namespace FreeSlate.Docx.Layout;

/// <summary>
/// A line placed on a page: its left edge (the column's) and its top, in points from the page's top left; a line of
/// rotated text (table cells, catalog: table.cells) turns by <paramref name="Angle"/> degrees clockwise about a point
/// given relative to its left edge and top.
/// </summary>
public sealed record PlacedLine(Line Line, double Left, double Top, double Angle = 0, double PivotX = 0, double PivotY = 0)
{
    /// <summary>Where inline VML shapes hang from (see Vml.Hangs): a paragraph's first line's top with its space before.</summary>
    public double? HangTop { get; init; }

    /// <summary>A line of the body that line numbering counts (not in tables, notes or suppressed paragraphs).</summary>
    public bool Numbered { get; init; }
}

/// <summary>A page of the laid-out document.</summary>
public sealed class PageBox
{
    public int Number { get; init; }
    public required SectionProperties Section { get; init; }

    /// <summary>The footnotes that begin on the page, in the order of their references.</summary>
    public List<string> Footnotes { get; } = [];

    /// <summary>The body's paragraphs that begin on the page, in their order (for STYLEREF and PAGEREF).</summary>
    public List<ParagraphBlock> Paragraphs { get; } = [];

    /// <summary>The number of the last chapter heading on the page or before it (section.pageNumbers), or "".</summary>
    public string Chapter { get; set; } = "";

    /// <summary>What PAGE fields put before the page number: the chapter number and its separator, or "".</summary>
    public string ChapterPrefix => Section.ChapterLevel != null && Chapter.Length > 0 ? Chapter + Section.ChapterSeparator : "";

    /// <summary>The height of the footnotes of a page of several columns, which their text must leave free.</summary>
    public double NoteBand { get; set; }
    public List<PlacedLine> Lines { get; } = [];

    /// <summary>The page's number as PAGE fields show it (w:pgNumType restarts it; catalog: section.pageNumbers).</summary>
    public int DisplayNumber { get; init; }

    /// <summary>The index of the page's section and whether the page is the section's first.</summary>
    public int SectionIndex { get; init; }
    public bool FirstOfSection { get; init; }

    /// <summary>The header and footer parts of the page, or null (catalog: headerFooter.parts).</summary>
    public string? Header { get; init; }
    public string? Footer { get; init; }

    /// <summary>Where the body begins and ends: at the margins, or below a header and above a footer that reach past them.</summary>
    public double BodyTop { get; set; }
    public double BodyBottom { get; set; }

    /// <summary>The left edge and the width of the text area (mirrored margins and the gutter placed).</summary>
    public double TextLeft { get; init; }
    public double TextWidth { get; init; }

    /// <summary>A page left blank so that the next section starts on an odd or even page.</summary>
    public bool Blank { get; init; }

    /// <summary>The space after the paragraph that ends on this page, which vertical alignment counts.</summary>
    public double TrailingSpace { get; set; }

    /// <summary>Lines between columns (w:cols/@sep): x, top and bottom in points.</summary>
    public List<(double X, double Top, double Bottom)> ColumnLines { get; } = [];

    /// <summary>Table cell shading, table borders and dotted, dashed or diagonal lines, drawn in this order below the text.</summary>
    public List<FillRect> Shading { get; } = [];
    public List<FillRect> Borders { get; } = [];
    public List<Stroke> Strokes { get; } = [];

    /// <summary>Floating drawings anchored on the page (catalog: drawing.anchor).</summary>
    public List<PlacedDrawing> Drawings { get; } = [];

    /// <summary>The areas floating tables keep free of text on the page (catalog: table.floating).</summary>
    public List<Paginator.Exclusion> FloatingTables { get; } = [];

    /// <summary>The page's border (catalog: section.pageBorders), or null.</summary>
    public PageBorderBox? Border { get; set; }

    /// <summary>Line numbers (catalog: section.lineNumbers): the number, its style, where it ends and its baseline.</summary>
    public List<(string Text, RunStyle Style, double Right, double Baseline)> LineNumbers { get; } = [];

    /// <summary>Bookmarks that start on the page: their names and where their paragraph begins (catalog: field.bookmarks).</summary>
    public List<(string Name, double X, double Y)> Bookmarks { get; } = [];

    /// <summary>
    /// Alternative texts of tables on the page (w:tblCaption, w:tblDescription, catalog: table.accessibility) with the
    /// part of the table the page holds.
    /// </summary>
    public List<(string Text, double X, double Y, double Width, double Height)> Alternatives { get; } = [];

    public double Width => Section.PageWidth;
    public double Height => Section.PageHeight;

    /// <summary>
    /// A page of vertical text is laid out turned a quarter (its <see cref="Section"/> turned): this is the page as
    /// printed, with what stands upright on it (header, footer and page border); null on other pages.
    /// </summary>
    public PageBox? Upright { get; set; }

    /// <summary>The size of the page as printed.</summary>
    public double PaperWidth => (Section.Upright ?? Section).PageWidth;
    public double PaperHeight => (Section.Upright ?? Section).PageHeight;
}

/// <summary>
/// Breaks the body into pages (catalog: engine.pagination, section.page, section.columns, paragraph.pagination):
/// sections start on a new page, an even or odd page or continue on the same page; lines fill the columns of the
/// text area; paragraphs keep their spacing (the larger of the space after the one and before the next, measured
/// with docx/generated/text-basic, as ISO/IEC 29500 17.3.1.33 says), keep with the next paragraph, keep their lines
/// together and avoid widows and orphans. At the top of a page the space before depends on how the page began
/// (measured with docx/local/sp-pagetop): all of it on the first page, after a section break what the larger space
/// has more than the space after the section's last paragraph, none after a page or column break, a page break
/// before or a natural break.
/// </summary>
/// <param name="reader">Reads the header and footer parts; without it pages have none.</param>
public sealed class Paginator(DocumentContext context, ParagraphLayout layout, ModelReader? reader = null)
{
    readonly List<PageBox> pages = [];
    readonly TableLayout tables = new(context, layout);

    // The current position: page, column, the top of the next line and whether the column is still empty.
    PageBox? page;
    SectionProperties section = new();
    int sectionIndex;
    int column;
    double y;
    bool columnEmpty = true;

    /// <summary>Where the current section's columns begin on this page (below an earlier section of the page).</summary>
    double columnTop;

    /// <summary>The lines of the current section on this page with the space before each, for balancing its columns.</summary>
    readonly List<FlowLine> flow = [];

    /// <summary>
    /// A line of the current section on this page: its place in the page's lines, the space before it where it does
    /// not begin a column (and where it does, for the first), its paragraph and its place in it.
    /// </summary>
    sealed record FlowLine(int Index, double Gap, double FirstGap, int Paragraph, int Line, int Lines, bool Widows, bool KeepNext, bool KeepLines)
    {
        /// <summary>The space after the line's paragraph when the line is its last.</summary>
        public double After { get; set; }
    }

    /// <summary>Whether the section before ended with balanced columns, whose height holds their last space after.</summary>
    bool balanced;

    int paragraphNumber;
    /// <summary>How the current column began, which decides the space before its first paragraph.</summary>
    enum Top { Document, Section, Break, Continuous, Leading }

    Top top = Top.Document;
    double pendingAfter;      // space after the previous paragraph
    double pendingOwnAfter;   // the same before contextual spacing dropped it
    ParagraphBlock? previous;

    /// <summary>
    /// The paragraph contextual spacing compares the next one with: the previous paragraph, or after a table the last
    /// paragraph of its last cell (measured with docx/local/c2-html: a contextually spaced paragraph after a table
    /// whose last paragraph has its style loses its space before).
    /// </summary>
    ParagraphBlock? stylePrevious;

    /// <param name="footnotes">The document's footnotes by id, with "separator" and "continuationSeparator".</param>
    /// <param name="endnotes">The document's endnotes in the order of their references, with the sections that refer to them.</param>
    /// <remarks>
    /// Floating objects with text wrapping (catalog: engine.floats) stand on the page of their anchor and push aside
    /// every line of that page, also of paragraphs above the anchor (measured with docx/generated/wrapping): the
    /// document is paginated again with the objects of each page known from the run before, until the pages keep
    /// their objects (at most four runs).
    /// </remarks>
    public List<PageBox> Paginate(List<Block> blocks, SectionProperties lastSection, Dictionary<string, List<Block>>? footnotes = null,
        List<(int Section, List<Block> Blocks)>? endnotes = null, List<Block>? endnoteSeparator = null)
    {
        List<PageBox> Run()
        {
            var known = new Dictionary<int, List<Exclusion>>();
            var bands = new Dictionary<int, double>();
            List<PageBox> result = [];
            for (var run = 0; run < 4; run++)
            {
                var paginator = new Paginator(context, layout, reader) { seeded = known, seededBands = bands };
                result = paginator.PaginateOnce(blocks, lastSection, footnotes, endnotes, endnoteSeparator);
                var found = result.ToDictionary(p => p.Number, p => p.Drawings.Select(ExclusionOf).OfType<Exclusion>().ToList());
                // Footnotes of later columns of a page take room from its earlier columns too: the page is laid out
                // again with the room they took.
                var foundBands = result.Where(p => p.NoteBand > 0).ToDictionary(p => p.Number, p => p.NoteBand);
                var bandsKept = foundBands.Count == bands.Count && foundBands.All(b => bands.TryGetValue(b.Key, out var old) && Math.Abs(old - b.Value) < 0.01);
                if ((found.Values.All(e => e.Count == 0) || Same(known, found)) && bandsKept)
                    break;
                known = found;
                bands = foundBands;
            }
            return result;
        }
        // Fields Word computes when it prints: those that depend on the pages take them from a layout before.
        layout.Evaluator ??= new FieldEvaluator(context, blocks);
        var pages = Run();
        for (var pass = 0; pass < 2 && layout.Evaluator.Update(pages); pass++)
            pages = Run();
        layout.Evaluator.Update(pages);
        // Footnotes numbered anew on every page (w:numRestart eachPage) take their numbers from the pages they land on;
        // the pages are laid out again with them until they stay (measured with docx/local/fnpages-b).
        for (var pass = 0; pass < 3 && footnotes is { Count: > 0 }; pass++)
        {
            var marks = new Dictionary<(bool Endnote, string Id), string>();
            foreach (var page in pages)
            {
                var (own, settings) = (page.Section.Element.Child("footnotePr"), context.Settings.Child("footnotePr"));
                if ((own.Child("numRestart").Val() ?? settings.Child("numRestart").Val()) != "eachPage")
                    continue;
                var format = own.Child("numFmt").Val() ?? settings.Child("numFmt").Val() ?? "decimal";
                var k = 0;
                foreach (var id in page.Footnotes)
                    marks[(false, id)] = NumberText.Format(++k, format);
            }
            if (marks.Count == 0 || layout.NoteMarks is { } before && before.Count == marks.Count && before.All(m => marks.GetValueOrDefault(m.Key) == m.Value))
                break;
            layout.NoteMarks = marks;
            pages = Run();
        }
        NumberLines(pages);
        return pages;

        static bool Same(Dictionary<int, List<Exclusion>> a, Dictionary<int, List<Exclusion>> b) =>
            a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var other) && other.Count == kv.Value.Count
                && other.Zip(kv.Value).All(p => Math.Abs(p.First.Top - p.Second.Top) < 0.01 && Math.Abs(p.First.Left - p.Second.Left) < 0.01));
    }

    /// <summary>
    /// Line numbers (w:lnNumType, catalog: section.lineNumbers) as Word prints them (measured with
    /// docx/local/line-numbers): every body line of a numbered section counts, empty ones too, except the lines of
    /// paragraphs with w:suppressLineNumbers, tables, notes, headers and footers; the first line has w:start + 1, the
    /// count starting again on every page (newPage, the default), in every section (newSection) or never
    /// (continuous); every w:countBy-th number shows, right-aligned w:distance (0.25 in by default) left of its
    /// column, on the line's baseline, in the Line Number style (not the paragraph's formatting).
    /// </summary>
    void NumberLines(List<PageBox> pages)
    {
        var count = 0;
        var section = -1;
        var styleId = context.Styles.Find("LineNumber") is { Type: "character" } ? "LineNumber" : null;
        var style = layout.Styles.Style(context.Styles.Run(context.Styles.DefaultParagraphStyle, styleId, null), WordSlot.Ascii);
        foreach (var page in pages)
        {
            if (page.Section.Element.Child("lnNumType") is not { } numbering || (numbering.Number("countBy") ?? 0) < 1)
            {
                section = -1;
                continue;
            }
            var start = (int)(numbering.Number("start") ?? 0);
            var every = (int)(numbering.Number("countBy") ?? 1);
            var distance = numbering.Twips("distance") ?? 18;
            var restart = numbering.Attr("restart") ?? "newPage";
            if (section < 0 || restart == "newPage" || restart == "newSection" && page.SectionIndex != section)
                count = start;
            section = page.SectionIndex;
            foreach (var placed in page.Lines.Where(l => l.Numbered))
            {
                count++;
                if (count % every == 0)
                    page.LineNumbers.Add((count.ToString(System.Globalization.CultureInfo.InvariantCulture), style, placed.Left - distance, placed.Top + placed.Line.Baseline));
            }
        }
    }

    /// <summary>
    /// A floating table (w:tblpPr, catalog: table.floating), measured with docx/local/float-tables: it takes no room in
    /// the text; its outer left edge stands w:tblpX from its column (horzAnchor text), the margin or the page, or it
    /// aligns left, right, centered, inside or outside (w:tblpXSpec); its top stands w:tblpY below the top of the
    /// paragraph after it (vertAnchor text), the top margin or the page, or aligns (w:tblpYSpec); text wraps around it
    /// on both sides, kept w:leftFromText, w:rightFromText, w:topFromText and w:bottomFromText away.
    /// </summary>
    void PlaceFloatingTable(List<Block> blocks, int index, XElement position, TableBox box, double left, double width)
    {
        double Twips(string name) => position.Twips(name) ?? 0;
        var height = box.Rows.Sum(r => r.Height) + (box.Rows.Count > 0 ? box.Rows[^1].BottomBorderWidth : 0);
        var tableWidth = box.Width;
        var (frameLeft, frameWidth) = position.Attr("horzAnchor") switch
        {
            "page" => (0.0, page!.Width),
            "margin" => (page!.TextLeft, page.TextWidth),
            _ => (left, width),
        };
        // The top of the paragraph after the table, above its space before (measured with docx/local/floatanchor-*: a
        // table at tblpY 0 stands right below the space after the paragraph before it, whatever the next one has
        // before; docx/external/libreoffice/core-text/floattable-heading-split-hyphen).
        var textTop = y + (columnEmpty ? 0 : pendingAfter);
        var (frameTop, frameHeight) = position.Attr("vertAnchor") switch
        {
            "page" => (0.0, page!.Height),
            "margin" => (page!.BodyTop, page.BodyBottom - page.BodyTop),
            _ => (textTop, 0.0),
        };
        var odd = page!.DisplayNumber % 2 == 1;
        var x = position.Attr("tblpXSpec") switch
        {
            "left" => frameLeft,
            "right" => frameLeft + frameWidth - tableWidth,
            "center" => frameLeft + (frameWidth - tableWidth) / 2,
            "inside" => odd ? frameLeft : frameLeft + frameWidth - tableWidth,
            "outside" => odd ? frameLeft + frameWidth - tableWidth : frameLeft,
            _ => frameLeft + Twips("tblpX"),
        };
        var top = position.Attr("tblpYSpec") switch
        {
            "top" or "inside" => frameTop,
            "bottom" or "outside" => frameTop + frameHeight - height,
            "center" => frameTop + (frameHeight - height) / 2,
            "inline" => textTop,
            _ => frameTop + Twips("tblpY"),
        };
        // The rows at the table's place (their own offset is the table's indent, which the position replaces). A row
        // that does not fit below the rows before it on the page goes on at the top of the next page's body, where
        // the table stands at the same place across, its header rows repeated; the line below the last row of a page
        // closes the part there, and the paragraph after the table begins at the top of its last page (measured with
        // docx/external/libreoffice/core-layout/floattable, floattable-3pages, floattable-compat14 and fdo68607).
        var rowTop = top;
        var partTop = top;
        var onPage = 0;
        RowBox? last = null;
        var headers = box.Rows.TakeWhile(r => r.Header).ToList();
        void Draw(RowBox row)
        {
            TableLayout.Draw(row, row.Height, x - box.Left, rowTop, page!.Lines, page.Shading, page.Borders, page.Strokes, []);
            rowTop += row.Height;
            last = row;
            onPage++;
        }
        void Close(RowBox row)
        {
            foreach (var fill in row.BottomBorders)
                page!.Borders.Add(fill with { X = x - box.Left + fill.X, Y = rowTop + fill.Y });
            page!.Strokes.AddRange(row.BottomStrokes.Select(s => s.Offset(x - box.Left, rowTop)));
            page.FloatingTables.Add(new Exclusion(x - Twips("leftFromText"), partTop - Twips("topFromText"),
                x + tableWidth + Twips("rightFromText"), rowTop + row.BottomBorderWidth + Twips("bottomFromText"), false, "bothSides"));
        }
        var split = false;
        // Before Word 2013 a table anchored to the page or the margin breaks on its first page where it would at the
        // paragraph after it, as many lines down, and so may reach past the bottom margin or stop above it (measured
        // with docx/local/floatsplit-14p-*: as many lines as anchored to the text; dummyv0, -v2, -v4: variants of
        // docx/external/libreoffice/core-doc/floating-table-dummy-text; floattable-compat14); Word 2013 breaks it at
        // the bottom margin (floattable-2cols, floattable-footer).
        var shift = position.Attr("vertAnchor") is "page" or "margin" && context.CompatibilityMode < 15 ? textTop - top : 0;
        double Limit() => Bottom - (split ? 0 : shift);
        // w:doNotBreakWrappedTables keeps the table whole where it stands (docx/external/libreoffice/
        // writerfilter-dmapper/do-not-break-wrapped-tables).
        var breaks = !context.Compat("doNotBreakWrappedTables");
        foreach (var source in box.Rows)
        {
            var row = source;
            // A row that does not fit splits at a line of each cell as in a table in the text, in Word 2013 and before
            // alike (also one that may not split where no row of the page is before it), else moves on; a row that
            // cannot split at the top of a page overflows (measured with docx/local/floatsplit-*: anchored to the text
            // or the page, its first row of lines or of one long paragraph; docx/external/libreoffice/core-layout/
            // floattable-rowsplit, floattable-table-row-keep).
            while (breaks && rowTop + row.Height + row.BottomBorderWidth > Limit() + 0.01)
            {
                if (Limit() - rowTop > row.TopWidth + 1 && row.Split(Limit() - rowTop - row.BottomBorderWidth, onPage == 0) is { } parts)
                {
                    Draw(parts.Head);
                    row = parts.Tail;
                }
                else if (onPage == 0 && (split || rowTop <= page!.BodyTop + 0.01))
                    break;
                if (last != null)
                    Close(last);
                NewPage(section);
                rowTop = partTop = page!.BodyTop;
                onPage = 0;
                split = true;
                if (!row.Header)
                {
                    foreach (var header in headers)
                        Draw(header);
                    onPage = 0;
                }
            }
            Draw(row);
        }
        if (last != null)
            Close(last);
        else
            page.FloatingTables.Add(new Exclusion(x - Twips("leftFromText"), top - Twips("topFromText"),
                x + tableWidth + Twips("rightFromText"), top + height + Twips("bottomFromText"), false, "bothSides"));
        if (split)
        {
            y = page!.BodyTop;
            columnEmpty = true;
            this.top = Top.Break;
        }
        // The paragraph after the table adds all its space before to the space after the paragraph before the table
        // (docx/local/floatanchor-fb: 12 pt after and 24 pt before make 36).
        pendingOwnAfter = 0;
    }

    /// <summary>
    /// A text frame or drop cap (w:framePr, catalog: paragraph.frames): consecutive paragraphs with the same frame
    /// properties, measured with docx/local/frames. The frame takes no room in the text: it is w:w wide (else as wide
    /// as its widest content) and stands w:x from, or aligned (w:xAlign) in, its column (hAnchor text), the margin or
    /// the page, w:y below the top of the paragraph after it (vAnchor text), the margin or the page; text wraps around
    /// it, kept w:hSpace and w:vSpace away (not beside it with wrap notBeside, over it with wrap none). A drop cap is a
    /// frame of its letter at the start of the paragraph after it, a margin drop cap left of the column.
    /// </summary>
    /// <returns>The index of the first block after the frame.</returns>
    int PlaceFrame(List<Block> blocks, int index)
    {
        var first = (ParagraphBlock)blocks[index];
        var properties = first.Format.Frame!;
        var group = new List<ParagraphBlock> { first };
        var end = index + 1;
        while (end < blocks.Count && blocks[end] is ParagraphBlock { EndsSection: null } p && p.Format.Frame is { } other && XNode.DeepEquals(other, properties))
        {
            group.Add(p);
            end++;
        }
        var (left, width) = Column(column);
        double Twips(string name) => properties.Twips(name) ?? 0;
        var frameWidth = properties.Twips("w") is { } w && w > 0 ? w : group.Max(p => layout.Widths(p, pictures: true).Max);
        var laid = group.Select(p => layout.Layout(p, frameWidth)).ToList();
        var content = 0.0;
        for (var k = 0; k < group.Count; k++)
            content += (k > 0 ? Between(group[k - 1], group[k]) : 0) + laid[k].Sum(l => l.Height);
        // The paragraphs' borders frame the content: their top and bottom lines with their space take room.
        var frameBorders = Frame.Read(first.Format);
        var borderTop = frameBorders?.Top is { Visible: true } bt ? bt.Space + bt.Total : 0;
        content += borderTop + (frameBorders?.Bottom is { Visible: true } bb ? bb.Space + bb.Total : 0);
        var height = properties.Twips("h") is { } h && h > 0 ? properties.Attr("hRule") == "exact" ? h : Math.Max(h, content) : content;
        var dropCap = properties.Attr("dropCap");
        // The paragraph the frame belongs to: the first after it.
        var next = end < blocks.Count ? blocks[end] as ParagraphBlock : null;
        var textTop = y + (columnEmpty ? 0 : Gap(next != null ? SpaceBefore(next) : 0));
        var (frameLeft, frameSpan) = properties.Attr("hAnchor") switch
        {
            "page" when dropCap is not "margin" => (0.0, page!.Width),
            "margin" or "page" => (page!.TextLeft, page.TextWidth),
            _ => (left, width),
        };
        var (frameTop, frameHeight) = properties.Attr("vAnchor") switch
        {
            "page" => (0.0, page!.Height),
            "margin" => (page!.BodyTop, page.BodyBottom - page.BodyTop),
            _ => (textTop, 0.0),
        };
        var odd = page!.DisplayNumber % 2 == 1;
        var hSpace = Twips("hSpace");
        var x = dropCap == "margin" ? left - frameWidth - hSpace
            : dropCap == "drop" ? left
            : properties.Attr("xAlign") switch
            {
                "left" => frameLeft,
                "right" => frameLeft + frameSpan - frameWidth,
                "center" => frameLeft + (frameSpan - frameWidth) / 2,
                "inside" => odd ? frameLeft : frameLeft + frameSpan - frameWidth,
                "outside" => odd ? frameLeft + frameSpan - frameWidth : frameLeft,
                _ => frameLeft + Twips("x"),
            };
        var top = dropCap is "drop" or "margin" ? textTop : properties.Attr("yAlign") switch
        {
            "top" or "inside" => frameTop,
            "bottom" or "outside" => frameTop + frameHeight - height,
            "center" => frameTop + (frameHeight - height) / 2,
            "inline" => textTop,
            _ => frameTop + Twips("y"),
        };
        // The frame's lines, its paragraphs' borders around the whole frame.
        var lineTop = top + borderTop;
        for (var k = 0; k < group.Count; k++)
        {
            if (k > 0)
                lineTop += Between(group[k - 1], group[k]);
            foreach (var line in laid[k])
            {
                page.Lines.Add(new PlacedLine(line, x, lineTop));
                lineTop += line.Height;
            }
        }
        frameBorders?.Draw(page, x, frameWidth, first.Format, top, top + height, top: true, bottom: true, between: null);
        // The frame keeps free its side borders too (they stand outside its width, as paragraph borders do).
        var vSpace = Twips("vSpace");
        var outerLeft = frameBorders?.Left is { Visible: true } fl ? 1.4 + fl.Space + fl.Total : 0;
        var outerRight = frameBorders?.Right is { Visible: true } fr ? 1.55 + fr.Space + fr.Total : 0;
        if (properties.Attr("wrap") != "none")
            page.FloatingTables.Add(new Exclusion(x - outerLeft - hSpace, top - vSpace, x + frameWidth + outerRight + hSpace, top + height + vSpace,
                properties.Attr("wrap") == "notBeside", "bothSides"));
        return end;
    }

    /// <summary>The floating objects of each page from the run before, which this run's lines go around.</summary>
    Dictionary<int, List<Exclusion>> seeded = [];

    /// <summary>The height the footnotes of each page with columns took in the run before.</summary>
    Dictionary<int, double> seededBands = [];

    /// <summary>
    /// The area a floating object keeps free of text (catalog: drawing.wrap): its box with the effect extents and the
    /// distances around it; square wrapping keeps the box, top and bottom the whole width, tight and through wrapping
    /// the wrap polygon's width beside each line's text with the distances left and right of it.
    /// </summary>
    public sealed record Exclusion(double Left, double Top, double Right, double Bottom, bool TopAndBottom, string Side)
    {
        /// <summary>The wrap polygon on the page (tight and through wrapping), or null for the box.</summary>
        public (double X, double Y)[]? Polygon { get; init; }

        /// <summary>The drawing's element, which finds the same object again in the next run.</summary>
        public XElement? Source { get; init; }

        public double DistanceLeft { get; init; }
        public double DistanceRight { get; init; }
    }

    static Exclusion? ExclusionOf(PlacedDrawing drawing)
    {
        var element = drawing.Drawing;
        var wrap = element.Elements().FirstOrDefault(e => e.Name.LocalName.StartsWith("wrap", StringComparison.Ordinal));
        if (wrap == null || wrap.Name.LocalName == "wrapNone")
            return null;
        double Emu(XElement e, string name) => double.TryParse((string?)e.Attribute(name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v / 12700 : 0;
        double Fraction(XElement e, string name) => Emu(e, name) * 12700 / 21600;
        var effect = element.Element(Docx.Drawings.Wp + "effectExtent");
        var (el, et, er, eb) = effect != null ? (Emu(effect, "l"), Emu(effect, "t"), Emu(effect, "r"), Emu(effect, "b")) : (0, 0, 0, 0);
        var (dl, dt, dr, db) = (Emu(element, "distL"), Emu(element, "distT"), Emu(element, "distR"), Emu(element, "distB"));
        var side = (string?)wrap.Attribute("wrapText") ?? "bothSides";
        // Tight and through wrapping follow the wrap polygon, in 21600ths of the drawing's extent (measured with
        // docx/generated/wrap-types: text beside an oval and a triangle starts the right distance from the polygon).
        if (wrap.Name.LocalName is "wrapTight" or "wrapThrough"
            && wrap.Element(Docx.Drawings.Wp + "wrapPolygon") is { } polygon
            && polygon.Elements().Where(p => p.Name.LocalName is "start" or "lineTo").Select(p => (X: Fraction(p, "x"), Y: Fraction(p, "y"))).ToList() is { Count: >= 3 } points)
        {
            var onPage = points.Select(p => (drawing.X + p.X * drawing.Width, drawing.Y + p.Y * drawing.Height)).ToArray();
            return new Exclusion(onPage.Min(p => p.Item1) - dl, onPage.Min(p => p.Item2), onPage.Max(p => p.Item1) + dr, onPage.Max(p => p.Item2), false, side)
            {
                Polygon = onPage,
                DistanceLeft = dl,
                DistanceRight = dr,
                Source = element,
            };
        }
        return new Exclusion(drawing.X - el - dl, drawing.Y - et - dt, drawing.X + drawing.Width + er + dr, drawing.Y + drawing.Height + eb + db,
            wrap.Name.LocalName == "wrapTopAndBottom", side) { Source = element };
    }

    /// <summary>The leftmost and rightmost points of a polygon between two heights, or null where it does not reach.</summary>
    static (double Min, double Max)? Span((double X, double Y)[] polygon, double top, double bottom)
    {
        double? min = null, max = null;
        void Take(double x)
        {
            min = min is { } m ? Math.Min(m, x) : x;
            max = max is { } n ? Math.Max(n, x) : x;
        }
        for (var i = 0; i < polygon.Length; i++)
        {
            var (a, b) = (polygon[i], polygon[(i + 1) % polygon.Length]);
            if (a.Y >= top && a.Y <= bottom)
                Take(a.X);
            // Where the edge crosses the band's top and bottom.
            foreach (var y in (double[])[top, bottom])
            {
                if ((a.Y - y) * (b.Y - y) < 0)
                    Take(a.X + (b.X - a.X) * (y - a.Y) / (b.Y - a.Y));
            }
        }
        return min is { } lo && max is { } hi ? (lo, hi) : null;
    }

    /// <summary>
    /// The floating objects of the current page this run knows: those placed so far where they stand now, the others
    /// where the run before placed them.
    /// </summary>
    List<Exclusion> PageExclusions()
    {
        var placed = page!.Drawings.Select(ExclusionOf).OfType<Exclusion>().Concat(page.FloatingTables).ToList();
        var list = seeded.TryGetValue(page.Number, out var before)
            ? before.Where(e => !placed.Any(p => ReferenceEquals(p.Source, e.Source) && e.Source != null)).ToList()
            : [];
        foreach (var exclusion in placed)
        {
            if (!list.Any(e => Math.Abs(e.Left - exclusion.Left) < 0.01 && Math.Abs(e.Top - exclusion.Top) < 0.01))
                list.Add(exclusion);
        }
        return list;
    }

    /// <summary>
    /// The room a line has beside the floating objects: the column less every object its box overlaps, keeping the
    /// sides the object's wrapText allows; where none is left, how far down the line must go.
    /// </summary>
    static LineRoom Room(List<Exclusion> exclusions, double paragraphTop, LineSlot slot, double columnLeft, double columnWidth)
    {
        var top = paragraphTop + slot.Offset;
        var height = slot.Height;
        var segments = new List<(double Start, double End)> { (0, columnWidth) };
        var skipTo = 0.0;
        foreach (var e in exclusions)
        {
            if (top >= e.Bottom - 0.01 || top + height <= e.Top + 0.01)
                continue;
            var (l, r) = (e.Left - columnLeft, e.Right - columnLeft);
            // A wrap polygon keeps free what of it lies beside the line's text.
            if (e.Polygon != null)
            {
                if (Span(e.Polygon, top + slot.TextTop, top + slot.TextBottom) is not { } span)
                    continue;
                (l, r) = (span.Min - e.DistanceLeft - columnLeft, span.Max + e.DistanceRight - columnLeft);
            }
            // Objects kept free above and below, and after a break with w:clear those of the side it clears (an
            // object on the left half of the column is on the left), take the whole line: it moves below them.
            var center = (e.Left + e.Right) / 2 - columnLeft;
            if (e.TopAndBottom || slot.Clear == "all" || slot.Clear == "left" && center < columnWidth / 2 || slot.Clear == "right" && center >= columnWidth / 2)
            {
                segments.Clear();
                skipTo = Math.Max(skipTo, e.Bottom - paragraphTop);
                continue;
            }
            var next = new List<(double Start, double End)>();
            foreach (var (start, end) in segments)
            {
                var before = (start, Math.Min(end, l));
                var after = (Math.Max(start, r), end);
                var keepBefore = before.Item2 > before.start;
                var keepAfter = after.end > after.Item1;
                switch (e.Side)
                {
                    case "left": keepAfter = false; break;
                    case "right": keepBefore = false; break;
                    case "largest":
                        if (keepBefore && keepAfter)
                        {
                            if (before.Item2 - before.start >= after.end - after.Item1) keepAfter = false;
                            else keepBefore = false;
                        }
                        break;
                }
                if (r <= start || l >= end)
                {
                    next.Add((start, end));
                    continue;
                }
                if (keepBefore) next.Add(before);
                if (keepAfter) next.Add(after);
            }
            segments = next;
            if (segments.Count == 0)
                skipTo = Math.Max(skipTo, e.Bottom - paragraphTop);
        }
        return new LineRoom(segments, skipTo);
    }

    List<PageBox> PaginateOnce(List<Block> blocks, SectionProperties lastSection, Dictionary<string, List<Block>>? footnotes = null,
        List<(int Section, List<Block> Blocks)>? endnotes = null, List<Block>? endnoteSeparator = null)
    {
        this.footnotes = footnotes;
        // Each section's properties are given at its end.
        var sections = new List<(List<Block> Blocks, SectionProperties Properties)>();
        var current = new List<Block>();
        foreach (var block in blocks)
        {
            current.Add(block);
            if (block is ParagraphBlock { EndsSection: { } ends })
            {
                sections.Add((current, ends));
                current = [];
            }
        }
        sections.Add((current, lastSection));
        // A section without a header or footer of a type has the previous section's (ISO/IEC 29500 17.10.5).
        for (var s = 1; s < sections.Count; s++)
        {
            foreach (var (type, part) in sections[s - 1].Properties.Headers)
                sections[s].Properties.Headers.TryAdd(type, part);
            foreach (var (type, part) in sections[s - 1].Properties.Footers)
                sections[s].Properties.Footers.TryAdd(type, part);
        }

        for (var s = 0; s < sections.Count; s++)
        {
            var (sectionBlocks, properties) = sections[s];
            sectionIndex = s;
            // Vertical text is laid out on the page turned a quarter (catalog: paragraph.bidi).
            if (properties.Vertical)
                properties = properties.Turned();
            StartSection(properties, s == 0);
            // Endnotes follow the document's last paragraph, after their separator, or each section's where the
            // settings put them at the end of sections (w:endnotePr/w:pos sectEnd), unless the section has none
            // (w:noEndnote; catalog: note.endnotes): after the text of the paragraph that ends the section, an empty
            // one taking no line before them (measured with docx/local/fnpages-c and -e).
            var atSectionEnds = context.Settings.Child("endnotePr").Child("pos").Val() == "sectEnd" && properties.Element.Child("noEndnote").OnOff() != true;
            var due = endnotes?.Where(n => n.Section >= endnotesFrom && (s == sections.Count - 1 || atSectionEnds && n.Section <= s)).ToList();
            var emptyEnd = due is { Count: > 0 } && sectionBlocks.Count > 0 && sectionBlocks[^1] is ParagraphBlock { EndsSection: not null } end
                && end.Content.All(c => c is BookmarkInline or PermissionInline);
            for (var i = 0; i < sectionBlocks.Count - (emptyEnd ? 1 : 0); i++)
            {
                if (sectionBlocks[i] is TableBlock)
                    PlaceTable(sectionBlocks, i);
                else if (sectionBlocks[i] is ParagraphBlock { Format.Frame: not null, EndsSection: null })
                    i = PlaceFrame(sectionBlocks, i) - 1;
                else
                    Place(sectionBlocks, i);
            }
            if (due is { Count: > 0 })
            {
                endnotesFrom = s + 1;
                var trailing = new List<Block>();
                trailing.AddRange(endnoteSeparator ?? []);
                foreach (var note in due)
                    trailing.AddRange(note.Blocks);
                for (var i = 0; i < trailing.Count; i++)
                {
                    if (trailing[i] is TableBlock)
                        PlaceTable(trailing, i);
                    else
                        Place(trailing, i);
                }
            }
            if (emptyEnd)
                Place(sectionBlocks, sectionBlocks.Count - 1);
            // A continuous section break balances the columns above it.
            if (s + 1 < sections.Count && sections[s + 1].Properties.Start == "continuous" && section.Columns > 1)
                Balance();
            CloseColumns();
        }
        if (pages.Count == 0)
            NewPage(lastSection, firstOfSection: true);
        PlaceNotes();
        foreach (var p in pages)
        {
            AlignVertically(p);
            if (p.Section is { Upright: not null, LinesFromLeft: true })
                Mirror(p);
        }
        if (reader != null)
        {
            // NUMPAGES counts the blank pages, SECTIONPAGES does not (measured with docx/local/hf-parity).
            var sectionPages = pages.Where(p => !p.Blank).GroupBy(p => p.SectionIndex).ToDictionary(g => g.Key, g => g.Count());
            foreach (var p in pages)
            {
                var fields = new PageFields(p.DisplayNumber, p.Section.PageNumberFormat, pages.Count, sectionPages.GetValueOrDefault(p.SectionIndex)) { Chapter = p.ChapterPrefix, On = p };
                // Headers and footers of pages of vertical text stand upright (measured with docx/local/vertical2).
                var on = p.Upright ?? p;
                if (p.Header != null)
                    Story(p.Header, on, fields, header: true).AddTo(on);
                if (p.Footer != null)
                    Story(p.Footer, on, fields, header: false).AddTo(on);
            }
        }
        return pages;
    }

    /// <summary>The first section whose endnotes are still to come.</summary>
    int endnotesFrom;

    /// <summary>The number of the last chapter heading placed (section.pageNumbers).</summary>
    string chapter = "";

    void StartSection(SectionProperties properties, bool first)
    {
        var start = first ? "nextPage" : properties.Start;
        // A section whose text runs another way begins a new page.
        if (page != null && (page.Section.Upright != null) != (properties.Upright != null))
            start = "nextPage";
        section = properties;
        layout.Vertical = properties.Upright != null;
        layout.TurnEastAsian = properties.TurnEastAsian;
        layout.LinePitch = GridPitch(properties);
        layout.CharacterExtra = properties.Element.Child("docGrid") is { } charGrid && charGrid.Attr("type") is "linesAndChars" or "snapToChars"
            ? (charGrid.Number("charSpace") ?? 0) / 4096 : 0;
        if (page == null || start != "continuous" && start != "nextColumn")
        {
            // Odd page numbers are right-hand pages: an odd or even page section leaves a blank page where the next
            // number has the wrong parity, and with different odd and even headers or mirrored margins so does any
            // section whose first number has the parity of the page before (measured with docx/local/hf-oddpage,
            // hf-restart-*: page numbers decide, not the sheets).
            if (!first && pages.Count > 0)
            {
                var previousNumber = pages[^1].DisplayNumber;
                var next = properties.PageNumberStart ?? previousNumber + 1;
                var sides = context.Settings.Child("evenAndOddHeaders").OnOff() == true || context.Settings.Child("mirrorMargins").OnOff() == true;
                if (start == "oddPage" && next % 2 == 0 || start == "evenPage" && next % 2 == 1 || sides && next % 2 == previousNumber % 2)
                    NewPage(pages[^1].Section, blank: true);
            }
            NewPage(properties, firstOfSection: true);
            top = first ? Top.Document : Top.Section;
            return;
        }
        // A continuous section goes on below what the page holds already, its first paragraph spaced as any other.
        columnTop = y;
        column = 0;
        columnEmpty = true;
        top = Top.Continuous;
        flow.Clear();
        if (start == "nextColumn")
            NextColumn(natural: false);
    }

    /// <param name="blank">A page left blank before a section: it has a number but no header or footer.</param>
    void NewPage(SectionProperties properties, bool firstOfSection = false, bool blank = false)
    {
        CloseColumns();
        PlaceNotes();
        var number = firstOfSection && properties.PageNumberStart is { } restart ? restart
            : pages.Count == 0 ? 1 : pages[^1].DisplayNumber + 1;
        // The first page of a section with a title page has the first header; with different odd and even pages
        // (w:evenAndOddHeaders) even pages have the even one; a type the sections do not give is an empty header.
        var type = firstOfSection && properties.TitlePage ? "first"
            : number % 2 == 0 && context.Settings.Child("evenAndOddHeaders").OnOff() == true ? "even" : "default";
        // Mirrored margins swap the left and right margins of even pages; the gutter is on the inside, at the top
        // (w:gutterAtTop) or on the right (w:rtlGutter).
        var (left, width) = TextArea(properties, number);
        var gutterAtTop = context.Settings.Child("gutterAtTop").OnOff() == true;
        page = new PageBox
        {
            Number = pages.Count + 1,
            Section = properties,
            DisplayNumber = number,
            SectionIndex = sectionIndex,
            FirstOfSection = firstOfSection,
            Blank = blank,
            Chapter = chapter,
            Header = reader != null && !blank ? properties.Headers.GetValueOrDefault(type) : null,
            Footer = reader != null && !blank ? properties.Footers.GetValueOrDefault(type) : null,
            TextLeft = left,
            TextWidth = Math.Max(1, width),
        };
        // A header that reaches below the top margin pushes the body down, a footer above the bottom margin pushes
        // its end up, each with the space before and after its paragraphs, unless the margin is negative (fixed;
        // measured with docx/local/hf-tall, hf-short).
        // Two pages per sheet (w:printTwoOnOne) swap the top and bottom margins of odd pages (measured with
        // docx/local/print-twoonone: their text starts at the bottom margin's distance; book fold keeps them).
        var swapped = number % 2 == 1 && context.Settings.Child("printTwoOnOne").OnOff() == true;
        page.BodyTop = (swapped ? properties.Bottom : properties.Top) + (gutterAtTop ? properties.Gutter : 0);
        page.BodyBottom = properties.PageHeight - (swapped ? properties.Top : properties.Bottom);
        if (properties.Upright is { } upright)
        {
            var (uprightLeft, uprightWidth) = TextArea(upright, number);
            page.Upright = new PageBox
            {
                Number = page.Number, Section = upright, DisplayNumber = number, SectionIndex = sectionIndex, FirstOfSection = firstOfSection,
                Blank = blank, Chapter = chapter, Header = page.Header, Footer = page.Footer, TextLeft = uprightLeft, TextWidth = uprightWidth,
                BodyTop = upright.Top, BodyBottom = upright.PageHeight - upright.Bottom,
            };
        }
        else
        {
            if (page.Header != null && !properties.TopFixed)
                page.BodyTop = Math.Max(page.BodyTop, Story(page.Header, page, null, header: true).Bottom);
            if (page.Footer != null && !properties.BottomFixed)
                page.BodyBottom = Math.Min(page.BodyBottom, Story(page.Footer, page, null, header: false).Top);
        }
        page.Border = PageBorders.Layout(page.Upright ?? page, context.Settings);
        pages.Add(page);
        column = 0;
        y = page.BodyTop;
        columnTop = y;
        columnEmpty = true;
        flow.Clear();
        if (!blank)
            ContinueNotes();
        if (properties.Columns > 1 && seededBands.TryGetValue(page.Number, out var band))
            notesHeight = Math.Max(notesHeight, band);
    }

    /// <summary>
    /// A header or footer laid out on a page of a section (catalog: headerFooter.parts): across the text area, a
    /// header from w:header below the page's top edge, a footer ending w:footer above its bottom edge; where its
    /// content begins and ends.
    /// </summary>
    /// <summary>
    /// The text area across a page of a section: mirrored margins swap the left and right margins of even pages; the
    /// gutter is on the inside, at the top (w:gutterAtTop) or on the right (w:rtlGutter).
    /// </summary>
    (double Left, double Width) TextArea(SectionProperties properties, int number)
    {
        var even = number % 2 == 0 && context.Settings.Child("mirrorMargins").OnOff() == true;
        var gutterAtTop = context.Settings.Child("gutterAtTop").OnOff() == true;
        var rightGutter = properties.Element.Child("rtlGutter").OnOff() == true;
        var gutterLeft = !gutterAtTop && (even ? rightGutter : !rightGutter);
        var left = (even ? properties.Right : properties.Left) + (gutterLeft ? properties.Gutter : 0);
        var width = properties.PageWidth - properties.Left - properties.Right - (gutterAtTop ? 0 : properties.Gutter);
        return (left, Math.Max(1, width));
    }

    /// <summary>
    /// A page of vertical text whose lines stack from the left (tbRlV, tbLrV): laid out from the right like the others,
    /// its lines, fills and strokes are mirrored across the turned page, each line keeping its own way up.
    /// </summary>
    static void Mirror(PageBox p)
    {
        var across = p.Height;
        for (var i = 0; i < p.Lines.Count; i++)
            p.Lines[i] = p.Lines[i] with { Top = across - p.Lines[i].Top - p.Lines[i].Line.Height, Line = p.Lines[i].Line.Flipped() };
        foreach (var fills in new[] { p.Shading, p.Borders })
        {
            for (var i = 0; i < fills.Count; i++)
                fills[i] = fills[i] with { Y = across - fills[i].Y - fills[i].Height };
        }
        for (var i = 0; i < p.Strokes.Count; i++)
            p.Strokes[i] = p.Strokes[i] with { Y1 = across - p.Strokes[i].Y1, Y2 = across - p.Strokes[i].Y2 };
        for (var i = 0; i < p.ColumnLines.Count; i++)
            p.ColumnLines[i] = (p.ColumnLines[i].X, across - p.ColumnLines[i].Bottom, across - p.ColumnLines[i].Top);
    }

    /// <summary>The line pitch of a section's line grid (w:docGrid), or null where it has none.</summary>
    static double? GridPitch(SectionProperties properties) =>
        properties.Element.Child("docGrid") is { } grid && grid.Attr("type") is "lines" or "linesAndChars" or "snapToChars" && grid.Twips("linePitch") is { } pitch && pitch > 0
            ? pitch : null;

    StoryBox Story(string part, PageBox on, PageFields? fields, bool header)
    {
        // Headers and footers do not snap to the body's grid, and their text runs across the page.
        var (pitch, extra, vertical, turn) = (layout.LinePitch, layout.CharacterExtra, layout.Vertical, layout.TurnEastAsian);
        (layout.LinePitch, layout.CharacterExtra, layout.Vertical, layout.TurnEastAsian) = (null, 0, false, false);
        try
        {
            return StoryOf(part, on, fields, header);
        }
        finally
        {
            (layout.LinePitch, layout.CharacterExtra, layout.Vertical, layout.TurnEastAsian) = (pitch, extra, vertical, turn);
        }
    }

    StoryBox StoryOf(string part, PageBox on, PageFields? fields, bool header)
    {
        var s = on.Section;
        var box = new StoryBox();
        var left = on.TextLeft;
        var width = on.TextWidth;
        var anchors = new List<CellAnchor>();
        var y = 0.0;
        double after = 0;
        var first = true;
        var blocks = reader!.Story(part);
        // Paragraph borders and shading as in the body: paragraphs with the same frame share one box.
        var boxTop = 0.0;
        Frame? previousFrame = null;
        // Lines of text frames placed on the page itself (vAnchor margin or page), not moving with the story.
        var placed = new List<PlacedLine>();
        for (var b = 0; b < blocks.Count; b++)
        {
            var block = blocks[b];
            // A text frame (w:framePr) takes no room in the story, as in the body (see PlaceFrame): it stands aligned in
            // or at w:x from the column, the margin or the page, w:y below the top of the paragraph after it, the
            // margin or the page (measured with docx/external/libreoffice/extras-ooxmlexport/tdf168017: a footer's
            // page number framed centered 1 twip below the next paragraph's top stands on its line).
            if (block is ParagraphBlock { Format.Frame: { } properties } framed && properties.Attr("dropCap") is null or "none")
            {
                var group = new List<ParagraphBlock> { framed };
                var end = b + 1;
                while (end < blocks.Count && blocks[end] is ParagraphBlock { Format.Frame: { } other } p && XNode.DeepEquals(other, properties))
                {
                    group.Add(p);
                    end++;
                }
                double Twips(string name) => properties.Twips(name) ?? 0;
                var frameWidth = properties.Twips("w") is { } w && w > 0 ? w : group.Max(p => layout.Widths(p, fields, pictures: true).Max);
                var laid = group.Select(p => layout.Layout(p, frameWidth, fields)).ToList();
                var content = 0.0;
                for (var k = 0; k < group.Count; k++)
                    content += (k > 0 ? Between(group[k - 1], group[k]) : 0) + laid[k].Sum(l => l.Height);
                var frameHeight = properties.Twips("h") is { } h && h > 0 ? properties.Attr("hRule") == "exact" ? h : Math.Max(h, content) : content;
                var next = end < blocks.Count ? blocks[end] as ParagraphBlock : null;
                var textTop = y + (first ? next?.Format.Before ?? 0 : context.Between(after, next?.Format.Before ?? 0));
                var (spanLeft, span) = properties.Attr("hAnchor") switch
                {
                    "page" => (0.0, on.Width),
                    "margin" => (on.TextLeft, on.TextWidth),
                    _ => (left, width),
                };
                var x = properties.Attr("xAlign") switch
                {
                    "left" or "inside" => spanLeft,
                    "right" or "outside" => spanLeft + span - frameWidth,
                    "center" => spanLeft + (span - frameWidth) / 2,
                    _ => spanLeft + Twips("x"),
                };
                var onPage = properties.Attr("vAnchor") is "page" or "margin";
                var (areaTop, areaHeight) = properties.Attr("vAnchor") switch
                {
                    "page" => (0.0, s.PageHeight),
                    "margin" => (s.Top, s.PageHeight - s.Top - s.Bottom),
                    _ => (textTop, 0.0),
                };
                var frameTop = properties.Attr("yAlign") switch
                {
                    "top" or "inside" => areaTop,
                    "bottom" or "outside" => areaTop + areaHeight - frameHeight,
                    "center" => areaTop + (areaHeight - frameHeight) / 2,
                    "inline" => textTop,
                    _ => areaTop + Twips("y"),
                };
                var lineTop = frameTop;
                for (var k = 0; k < group.Count; k++)
                {
                    if (k > 0)
                        lineTop += Between(group[k - 1], group[k]);
                    foreach (var line in laid[k])
                    {
                        (onPage ? placed : box.Lines).Add(new PlacedLine(line, x, lineTop));
                        lineTop += line.Height;
                    }
                }
                b = end - 1;
                continue;
            }
            if (block is ParagraphBlock paragraph)
            {
                // The paragraph begins below the space after the one before; its own space before lies inside it.
                var paragraphTop = y + (first ? 0 : after);
                y += first ? paragraph.Format.Before ?? 0 : context.Between(after, paragraph.Format.Before ?? 0);
                var frame = Frame.Read(paragraph.Format);
                var continues = frame != null && frame.Same(previousFrame);
                var nextContinues = frame != null && b + 1 < blocks.Count && blocks[b + 1] is ParagraphBlock following && frame.Same(Frame.Read(following.Format));
                double? betweenAt = null;
                if (frame != null && !continues)
                {
                    boxTop = y;
                    y += frame.Top is { Visible: true } t ? t.Space + t.Total : 0;
                }
                else if (frame?.Between is { Visible: true } between)
                {
                    betweenAt = y + between.Space;
                    y += 2 * between.Space + between.Total;
                }
                var lineTops = new Dictionary<AnchorInline, (double Top, double X)>(ReferenceEqualityComparer.Instance);
                foreach (var line in layout.Layout(paragraph, width, fields))
                {
                    foreach (var anchor in line.Anchors ?? [])
                        lineTops[anchor] = (y, left + (line.AnchorX?.GetValueOrDefault(anchor) ?? 0));
                    box.Lines.Add(new PlacedLine(line, left, y));
                    y += line.Height;
                }
                if (frame != null)
                {
                    if (!nextContinues)
                        y += frame.Bottom is { Visible: true } bottomLine ? bottomLine.Space + bottomLine.Total : 0;
                    frame.Draw(box.Shading, box.Borders, left, width, boxTop, y, top: !continues, bottom: !nextContinues, between: betweenAt);
                }
                previousFrame = frame;
                foreach (var anchor in paragraph.Content.OfType<AnchorInline>())
                {
                    var (lineTop, x) = lineTops.GetValueOrDefault(anchor, (paragraphTop, left));
                    anchors.Add(new CellAnchor(anchor, paragraphTop, lineTop) { CharacterX = x });
                }
                after = paragraph.Format.After ?? 0;
            }
            else if (block is TableBlock table)
            {
                y += first ? 0 : after;
                var laid = tables.Layout(table.Table, width, fields);
                var tableTop = y;
                foreach (var row in laid.Rows)
                {
                    TableLayout.Draw(row, row.Height, left, y, box.Lines, box.Shading, box.Borders, box.Strokes, anchors);
                    y += row.Height;
                }
                if (laid.Rows.Count > 0)
                {
                    var last = laid.Rows[^1];
                    box.Borders.AddRange(last.BottomBorders.Select(f => f with { X = left + f.X, Y = y + f.Y }));
                    box.Strokes.AddRange(last.BottomStrokes.Select(st => st.Offset(left, y)));
                    y += last.BottomBorderWidth;
                }
                TableLayout.Frame(laid, left, tableTop, y, box.Borders);
                after = 0;
                previousFrame = null;
            }
            first = false;
        }
        y += after;
        var top = header ? s.Header : s.PageHeight - s.Footer - y;
        box.Move(top);
        box.Lines.AddRange(placed);
        box.Top = top;
        box.Bottom = top + y;
        // Floating drawings of headers and footers (watermarks among them) stand on the page like the body's.
        foreach (var anchor in anchors.Select(a => a.Offset(0, top)))
            box.Drawings.Add(Anchored(anchor.Anchor, on, anchor.ParagraphTop, anchor.LineTop, left, width, anchor, anchor.CharacterX));
        return box;
    }

    /// <summary>A header or footer laid out: its lines, table fills and strokes, and where its content begins and ends.</summary>
    sealed class StoryBox
    {
        public List<PlacedLine> Lines { get; } = [];
        public List<FillRect> Shading { get; } = [];
        public List<FillRect> Borders { get; } = [];
        public List<Stroke> Strokes { get; } = [];
        public List<PlacedDrawing> Drawings { get; } = [];
        public double Top, Bottom;

        public void Move(double dy)
        {
            for (var i = 0; i < Lines.Count; i++)
                Lines[i] = Lines[i] with { Top = Lines[i].Top + dy };
            for (var i = 0; i < Shading.Count; i++)
                Shading[i] = Shading[i] with { Y = Shading[i].Y + dy };
            for (var i = 0; i < Borders.Count; i++)
                Borders[i] = Borders[i] with { Y = Borders[i].Y + dy };
            for (var i = 0; i < Strokes.Count; i++)
                Strokes[i] = Strokes[i].Offset(0, dy);
        }

        public void AddTo(PageBox page)
        {
            page.Lines.AddRange(Lines);
            page.Shading.AddRange(Shading);
            page.Borders.AddRange(Borders);
            page.Strokes.AddRange(Strokes);
            page.Drawings.AddRange(Drawings);
        }
    }

    (double Left, double Width) Column(int index)
    {
        var s = section;
        var left = page!.TextLeft;
        if (s.ColumnWidths is { Count: > 0 } widths)
        {
            for (var k = 0; k < index && k < widths.Count; k++)
                left += widths[k].Width + widths[k].Space;
            return (left, widths[Math.Min(index, widths.Count - 1)].Width);
        }
        var width = (page.TextWidth - s.ColumnSpace * (s.Columns - 1)) / s.Columns;
        return (left + index * (width + s.ColumnSpace), width);
    }

    /// <summary>Where the body text ends: above the footnotes the page holds, or where a note continued past it filled it.</summary>
    double Bottom => Math.Min(page!.BodyBottom - notesHeight, bodyEnd ?? double.MaxValue);

    Dictionary<string, List<Block>>? footnotes;

    /// <summary>The footnotes referenced on the current page and the height they take (with the separator).</summary>
    readonly List<string> pageNotes = [];
    double notesHeight;

    /// <summary>
    /// A note split at the bottom of the current page: its id and the part shown here; what continues of it (and of
    /// the notes after it on its line) on the next page; the part of such a continuation the current page shows; and
    /// where the body text had to end once the note filled the page.
    /// </summary>
    (string Id, NoteBox Shown)? splitNote;
    NoteBox? carried, carriedHere;
    double? bodyEnd;

    /// <summary>A footnote (or a separator) laid out in a width: its lines and fills from its top, and its height.</summary>
    sealed record NoteBox(List<PlacedLine> Lines, List<FillRect> Fills, List<Stroke> Strokes, double Height)
    {
        /// <summary>
        /// The note cut where <paramref name="room"/> points end, between lines: the lines that end within them, and the
        /// rest moved up to begin at the top; no part shown where not even the first line fits, no rest where all fit.
        /// </summary>
        public (NoteBox? Shown, NoteBox? Remainder) Cut(double room)
        {
            if (Height <= room + 0.01)
                return (this, null);
            var cut = Lines.Select(l => l.Top).Where(t => t > 0.01 && Lines.Where(l => l.Top < t - 0.01).All(l => l.Top + l.Line.Height <= room + 0.01))
                .DefaultIfEmpty(0).Max();
            if (cut <= 0.01)
                return (null, this);
            var shown = Lines.Where(l => l.Top < cut - 0.01).ToList();
            return (new NoteBox(shown, Fills.Where(f => f.Y < cut).ToList(), Strokes.Where(s => s.Y1 < cut).ToList(), shown.Max(l => l.Top + l.Line.Height)),
                new NoteBox(Lines.Where(l => l.Top >= cut - 0.01).Select(l => l with { Top = l.Top - cut }).ToList(),
                    Fills.Where(f => f.Y >= cut).Select(f => f with { Y = f.Y - cut }).ToList(),
                    Strokes.Where(s => s.Y1 >= cut).Select(s => s.Offset(0, -cut)).ToList(), Height - cut));
        }
    }

    readonly Dictionary<(string, double), NoteBox> noteBoxes = [];

    /// <summary>
    /// The columns footnotes stand in (measured with docx/local/fnpages-h): a page of several columns has its notes in
    /// columns too, under one separator at the left.
    /// </summary>
    List<(double Left, double Width)> NoteColumns()
    {
        var s = page!.Section;
        if (s.Columns <= 1)
            return [(page.TextLeft, page.TextWidth)];
        var columns = new List<(double Left, double Width)>();
        var left = page.TextLeft;
        for (var k = 0; k < s.Columns; k++)
        {
            var (width, space) = s.ColumnWidths is { Count: > 0 } widths ? widths[Math.Min(k, widths.Count - 1)]
                : ((page.TextWidth - s.ColumnSpace * (s.Columns - 1)) / s.Columns, s.ColumnSpace);
            columns.Add((left, width));
            left += width + space;
        }
        return columns;
    }

    /// <summary>
    /// The columns the notes of a page fall into, whole, in order: each column takes notes until those so far reach its
    /// share of their height (seven notes of two lines in two columns: four and three).
    /// </summary>
    List<List<string>> NoteSpread(IEnumerable<string> ids, int columns)
    {
        var list = ids.ToList();
        var total = list.Sum(id => Note(id).Height);
        var spread = Enumerable.Range(0, columns).Select(_ => new List<string>()).ToList();
        var (c, sum) = (0, 0.0);
        foreach (var id in list)
        {
            spread[c].Add(id);
            sum += Note(id).Height;
            if (c < columns - 1 && sum >= total * (c + 1) / columns - 0.01)
                c++;
        }
        return spread;
    }

    /// <summary>The height the notes of a page of columns take: their tallest column and the separator.</summary>
    double Band(IEnumerable<string> ids)
    {
        var columns = NoteColumns().Count;
        var spread = NoteSpread(ids, columns);
        return spread.Any(c => c.Count > 0) ? SeparatorHeight + spread.Max(c => c.Sum(id => Note(id).Height)) : 0;
    }

    /// <summary>A footnote laid out in the current page's text width (or its first column's).</summary>
    NoteBox Note(string id)
    {
        var width = NoteColumns()[0].Width;
        if (noteBoxes.TryGetValue((id, width), out var box))
            return box;
        var (lines, fills, strokes, height) = footnotes != null && footnotes.TryGetValue(id, out var blocks)
            ? tables.Blocks(blocks, width, null) : ([], [], [], 0);
        return noteBoxes[(id, width)] = new NoteBox(lines, fills, strokes, height);
    }

    /// <summary>The separator above the first footnote of a page.</summary>
    double SeparatorHeight => footnotes != null && footnotes.ContainsKey("separator") ? Note("separator").Height : 0;

    /// <summary>
    /// The height footnotes need below the text (catalog: note.footnotes): their content in the text width, and the
    /// separator above the first footnote of a page.
    /// </summary>
    double NotesHeight(List<string>? ids)
    {
        if (ids == null || footnotes == null || page == null)
            return 0;
        if (page.Section.Columns > 1)
        {
            var fresh = ids.Where(id => footnotes.ContainsKey(id) && !pageNotes.Contains(id)).Distinct().ToList();
            return fresh.Count == 0 ? 0 : Math.Max(0, Band(pageNotes.Concat(fresh)) - notesHeight);
        }
        var height = ids.Where(footnotes.ContainsKey).Sum(id => Note(id).Height);
        if (height > 0 && pageNotes.Count == 0 && carriedHere == null)
            height += SeparatorHeight;
        return height;
    }

    /// <summary>Takes the notes of a line or row onto the current page.</summary>
    void AddNotes(IEnumerable<string>? ids)
    {
        if (ids == null || footnotes == null)
            return;
        var added = ids.Where(id => footnotes.ContainsKey(id) && !pageNotes.Contains(id)).Distinct().ToList();
        notesHeight += NotesHeight(added);
        pageNotes.AddRange(added);
    }

    /// <summary>The height of the current page's notes again, after notes left it.</summary>
    void RecountNotes()
    {
        var kept = pageNotes.ToList();
        pageNotes.Clear();
        notesHeight = carriedHere != null ? SeparatorHeight + carriedHere.Height : 0;
        notesHeight += NotesHeight(kept);
        pageNotes.AddRange(kept);
        if (splitNote is { } split)
            notesHeight -= Note(split.Id).Height - split.Shown.Height;
    }

    /// <summary>
    /// A line whose notes do not fit below it with the text above (measured with docx/local/fnpages-a and -d): it
    /// stays on the page where its notes but the last fit and the last shows a line at least; that note shows as many
    /// lines as the page holds, the rest continues on the next page under the separator before that page's own
    /// notes, and the page takes no more text.
    /// </summary>
    bool SplitNotes(List<string> ids, double room)
    {
        if (footnotes == null || page!.Section.Columns > 1)
            return false;
        ids = ids.Where(id => footnotes.ContainsKey(id) && !pageNotes.Contains(id)).Distinct().ToList();
        if (ids.Count == 0)
            return false;
        room -= pageNotes.Count == 0 && carriedHere == null ? SeparatorHeight : 0;
        var whole = ids.Take(ids.Count - 1).Sum(id => Note(id).Height);
        if (whole > room + 0.01)
            return false;
        var (shown, rest) = Note(ids[^1]).Cut(room - whole);
        if (shown == null || rest == null)
            return false;
        AddNotes(ids);
        splitNote = (ids[^1], shown);
        notesHeight -= Note(ids[^1]).Height - shown.Height;
        carried = rest;
        return true;
    }

    /// <summary>The current page's footnotes at its bottom, above the bottom margin, under their separator.</summary>
    void PlaceNotes()
    {
        if (page == null || pageNotes.Count == 0 && carriedHere == null || footnotes == null)
        {
            pageNotes.Clear();
            notesHeight = 0;
            (splitNote, carriedHere, bodyEnd) = (null, null, null);
            return;
        }
        // A section that puts its footnotes beneath the text (its own w:footnotePr/w:pos; the settings' alone leave
        // them at the page's bottom) has them follow its last line (measured with docx/local/fnpages-f and -g).
        var at = page.Section.Element.Child("footnotePr").Child("pos").Val() == "beneathText" ? Math.Min(y, page.BodyBottom - notesHeight) : page.BodyBottom - notesHeight;
        var x = page.TextLeft;
        void Put(NoteBox box)
        {
            foreach (var line in box.Lines)
                page.Lines.Add(line with { Left = x + line.Left, Top = at + line.Top });
            foreach (var fill in box.Fills)
                page.Borders.Add(fill with { X = x + fill.X, Y = at + fill.Y });
            page.Strokes.AddRange(box.Strokes.Select(s => s.Offset(x, at)));
            at += box.Height;
        }
        if (footnotes.ContainsKey("separator"))
            Put(Note("separator"));
        if (carriedHere != null)
            Put(carriedHere);
        page.Footnotes.AddRange(pageNotes);
        var columns = NoteColumns();
        if (columns.Count > 1)
        {
            // Notes in columns, each column's from the top of the band.
            page.NoteBand = Band(pageNotes);
            var bandTop = at;
            var spread = NoteSpread(pageNotes, columns.Count);
            for (var c = 0; c < columns.Count; c++)
            {
                (x, at) = (columns[c].Left, bandTop);
                foreach (var id in spread[c])
                    Put(Note(id));
            }
        }
        else
        {
            foreach (var id in pageNotes)
                Put(splitNote is { } split && split.Id == id ? split.Shown : Note(id));
        }
        pageNotes.Clear();
        notesHeight = 0;
        (splitNote, carriedHere, bodyEnd) = (null, null, null);
    }

    /// <summary>
    /// The part of a note continued from the page before that a new page shows (Word's continuation separator prints as
    /// its separator, measured with docx/local/fnpages-a and -d), leaving the text some room on pages it would fill.
    /// </summary>
    void ContinueNotes()
    {
        if (carried == null || page == null)
            return;
        var room = page.BodyBottom - page.BodyTop - SeparatorHeight - 36;
        var (shown, rest) = carried.Cut(Math.Max(room, carried.Lines.Count > 0 ? carried.Lines[0].Line.Height : 0));
        (carriedHere, carried) = (shown ?? carried, shown == null ? null : rest);
        notesHeight = SeparatorHeight + carriedHere.Height;
    }

    void NextColumn(bool natural)
    {
        if (column + 1 < section.Columns)
        {
            column++;
            y = columnTop;
        }
        else
            NewPage(section);
        columnEmpty = true;
        top = Top.Break;
    }

    /// <summary>
    /// Places a table row by row (catalog: table.rows): a row that does not fit goes to the next column or page,
    /// where the table's header rows are repeated first; where the table breaks, the line below the last row of the
    /// page closes it. The paragraph before keeps its space after above the table.
    /// </summary>
    /// <remarks>
    /// Measured with docx/local/tblhdr-*: header rows that leave no room for the first row below them (all of it where
    /// it may not split, else its first line) go with it to the next column or page, unless the table starts there.
    /// They repeat only where the row that goes on fits below them in the same way; where they do not, the rows stand
    /// at the top without them, and Word still counts them: what follows the table goes to the next page. A row that
    /// may not split moves on whole and splits only at the top of a page that cannot hold it.
    /// </remarks>
    void PlaceTable(List<Block> blocks, int index)
    {
        var table = (TableBlock)blocks[index];
        var (left, width) = Column(column);
        var box = LaidTable(table, width, stylePrevious, keep: false);
        if (table.Table.Format.Floating is { } floating && box.Rows.Count > 0)
        {
            PlaceFloatingTable(blocks, index, floating, box, left, width);
            return;
        }
        if (!columnEmpty)
            y += pendingAfter;
        // A table whose first row is kept with the next one begins a keep chain, unless it continues one (measured
        // with docx/local/tk1, tk4, tk7, tk9: such rows go to the next page with the paragraphs kept with them).
        var chained = previous != null ? previous.Format.KeepNext == true : tableKept;
        if (!columnEmpty && !chained && RowKept(table.Table, 0) && ChainBreaks(blocks, index, null, width, y))
        {
            NextColumn(natural: true);
            (left, width) = Column(column);
        }
        tableKept = RowKept(table.Table, box.Rows.Count - 1);
        var headers = box.Rows.TakeWhile(r => r.Header).ToList();
        var headersHeight = headers.Sum(h => h.Height);
        // Whether the header rows were left off this column or page.
        var omitted = false;
        RowBox? last = null;
        var tableTop = y;
        // The table's alternative text, for the part of it on each page.
        var alternative = string.Join(": ", new[] { table.Table.Format.Caption, table.Table.Format.Description }.Where(t => !string.IsNullOrWhiteSpace(t)));
        void Describe()
        {
            if (alternative.Length > 0 && y > tableTop)
                page!.Alternatives.Add((alternative, left + box.Left, tableTop, box.Width, y - tableTop));
        }
        // Where the table breaks, the line below the page's last row closes it, and a frame (cell spacing) ends.
        void Break()
        {
            if (last != null)
                Below(last, left);
            Describe();
            TableLayout.Frame(box, left, tableTop, y, page!.Borders);
            NextColumn(natural: true);
            (left, width) = Column(column);
            tableTop = y;
        }
        // Footnotes of a row's cells need room below it as a paragraph's do.
        List<string> NotesOf(RowBox row) => row.Lines.SelectMany(l => l.Line.Notes ?? []).ToList();
        // Whether a row, all of it or its split, fits below a position in this column (as the loop below places it):
        // with the line that closes the table below it where the table breaks (measured with docx/local/tblbottom-*).
        bool Fits(RowBox row, double top) => top + row.Height + row.BottomBorderWidth <= Bottom - NotesHeight(NotesOf(row)) + 0.01;
        (RowBox Head, RowBox Tail)? Split(RowBox row, double top, bool force = false) =>
            Bottom - top > row.TopWidth + 1 ? row.Split(Bottom - top - row.BottomBorderWidth, force) : null;
        bool Takes(RowBox row, double top) => Fits(row, top) || Split(row, top) != null;
        if (headers.Count > 0 && headers.Count < box.Rows.Count && !columnEmpty && !Takes(box.Rows[headers.Count], y + headersHeight))
        {
            NextColumn(natural: true);
            (left, width) = Column(column);
            tableTop = y;
        }
        for (var r = 0; r < box.Rows.Count; r++)
        {
            var row = box.Rows[r];
            while (!Fits(row, y))
            {
                // A row that does not fit splits at a line of each cell (unless it may not split, measured with
                // docx/local/tbl-autofit), else moves on; one that may not split splits at the top of a column it
                // does not fit either, and one that cannot split at all overflows.
                if (Split(row, y, force: columnEmpty) is { } parts)
                {
                    PlaceRow(parts.Head, left);
                    AddNotes(NotesOf(parts.Head));
                    last = parts.Head;
                    row = parts.Tail;
                }
                else if (columnEmpty)
                    break;
                Break();
                omitted = !row.Header && headers.Count > 0 && !Takes(row, y + headersHeight);
                if (!row.Header && !omitted)
                {
                    foreach (var header in headers)
                    {
                        PlaceRow(header, left);
                        last = header;
                    }
                }
            }
            PlaceRow(row, left);
            AddNotes(NotesOf(row));
            last = row;
        }
        // A table that another table follows directly continues in it: Word joins them, one line between (measured
        // with docx/local/tbl-geom).
        if (last != null && !(index + 1 < blocks.Count && blocks[index + 1] is TableBlock))
        {
            Below(last, left);
            y += last.BottomBorderWidth;
        }
        Describe();
        TableLayout.Frame(box, left, tableTop, y, page!.Borders);
        if (omitted)
            y += headersHeight;
        pendingAfter = 0;
        pendingOwnAfter = 0;
        page!.TrailingSpace = 0;
        previous = null;
        stylePrevious = LastParagraph(table.Table);
        paragraphNumber++;
    }

    /// <summary>
    /// Where a floating drawing stands (catalog: drawing.anchor): its offset from, or its alignment in, the frame its
    /// wp:positionH and wp:positionV name (page, margin, column, the margins on either side, the paragraph, the
    /// line), or a percentage of it (wp14:pctPosHOffset); wp:simplePos gives the page position directly.
    /// </summary>
    /// <remarks>
    /// The paragraph's top lies above its own space before, below the space after the paragraph before it (where
    /// the larger of the two separates them, what the space before has more); the line's top is that of the line
    /// holding the anchor (measured with docx/local/anchor-rules, all within 0.05 pt).
    /// </remarks>
    static PlacedDrawing Anchored(AnchorInline anchor, PageBox page, double paragraphTop, double lineTop, double columnLeft, double columnWidth,
        CellAnchor? cell = null, double? character = null)
    {
        var element = anchor.Anchor;
        var (w, h) = Docx.Drawings.Extent(element);
        var s = page.Section;
        double Emu(string? value) => double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v / 12700 : 0;
        // Sizes relative to the page or its margins (wp14:sizeRelH, wp14:sizeRelV; 0 % keeps the extent): shapes take
        // them as they are, pictures only both together, scaled evenly to fit them (measured with
        // docx/local/size-rel-shape and docx/local/size-rel).
        var oddPage = page.DisplayNumber % 2 == 1;
        var rightMargin = s.PageWidth - page.TextLeft - page.TextWidth;
        double? Relative(string name, string percent, bool horizontal)
        {
            if (element.Element(Docx.Drawings.Wp14 + name) is not { } relative
                || !double.TryParse(relative.Element(Docx.Drawings.Wp14 + percent)?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var share)
                || share <= 0)
                return null;
            var frame = ((string?)relative.Attribute("relativeFrom"), horizontal) switch
            {
                ("page", true) => s.PageWidth,
                ("page", false) => s.PageHeight,
                ("leftMargin", _) => page.TextLeft,
                ("rightMargin", _) => rightMargin,
                ("topMargin", _) => s.Top,
                ("bottomMargin", _) => s.Bottom,
                ("insideMargin", true) => oddPage ? page.TextLeft : rightMargin,
                ("outsideMargin", true) => oddPage ? rightMargin : page.TextLeft,
                ("insideMargin", false) => oddPage ? s.Top : s.Bottom,
                ("outsideMargin", false) => oddPage ? s.Bottom : s.Top,
                (_, true) => page.TextWidth,
                (_, false) => s.PageHeight - s.Top - s.Bottom,
            };
            return frame * share / 100000;
        }
        var (relativeWidth, relativeHeight) = (Relative("sizeRelH", "pctWidth", true), Relative("sizeRelV", "pctHeight", false));
        if (element.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData")?.Element(Docx.Drawings.Pic + "pic") != null)
        {
            if (relativeWidth is { } rw && relativeHeight is { } rh && w > 0 && h > 0)
            {
                var scale = Math.Min(rw / w, rh / h);
                (w, h) = (w * scale, h * scale);
            }
        }
        else
            (w, h) = (relativeWidth ?? w, relativeHeight ?? h);
        double x, y;
        if ((string?)element.Attribute("simplePos") is "1" or "true" && element.Element(Docx.Drawings.Wp + "simplePos") is { } simple)
        {
            x = Emu((string?)simple.Attribute("x"));
            y = Emu((string?)simple.Attribute("y"));
        }
        else
        {
            var odd = page.DisplayNumber % 2 == 1;
            var positionH = element.Element(Docx.Drawings.Wp + "positionH");
            var fromH = (string?)positionH?.Attribute("relativeFrom");
            var (x0, xw) = fromH switch
            {
                "character" when character is { } at => (at, 0.0),
                // In a table cell: its text area, or the cell inside its lines for the page and the page margins.
                "page" or "leftMargin" or "rightMargin" or "insideMargin" or "outsideMargin" when cell?.Cell is { } inCell => inCell,
                _ when cell?.Text is { } text => text,
                "page" => (0.0, s.PageWidth),
                "column" or "character" => (columnLeft, columnWidth),
                "leftMargin" => (0.0, page.TextLeft),
                "rightMargin" => (page.TextLeft + page.TextWidth, s.PageWidth - page.TextLeft - page.TextWidth),
                "insideMargin" => odd ? (0.0, page.TextLeft) : (page.TextLeft + page.TextWidth, s.PageWidth - page.TextLeft - page.TextWidth),
                "outsideMargin" => odd ? (page.TextLeft + page.TextWidth, s.PageWidth - page.TextLeft - page.TextWidth) : (0.0, page.TextLeft),
                _ => (page.TextLeft, page.TextWidth),
            };
            x = Position(positionH, x0, xw, w, odd, horizontal: true);
            var positionV = element.Element(Docx.Drawings.Wp + "positionV");
            var (y0, yh) = ((string?)positionV?.Attribute("relativeFrom")) switch
            {
                not ("paragraph" or "line") when cell?.Content is { } content => content,
                "page" => (0.0, s.PageHeight),
                "paragraph" => (paragraphTop, 0.0),
                "line" => (lineTop, 0.0),
                "topMargin" => (0.0, s.Top),
                "bottomMargin" => (s.PageHeight - s.Bottom, s.Bottom),
                "insideMargin" => odd ? (0.0, s.Top) : (s.PageHeight - s.Bottom, s.Bottom),
                "outsideMargin" => odd ? (s.PageHeight - s.Bottom, s.Bottom) : (0.0, s.Top),
                _ => (s.Top, s.PageHeight - s.Top - s.Bottom),
            };
            y = Position(positionV, y0, yh, h, odd, horizontal: false);
        }
        return new PlacedDrawing(element, anchor.Part, x, y, w, h)
        {
            Behind = (string?)element.Attribute("behindDoc") is "1" or "true",
            Order = long.TryParse((string?)element.Attribute("relativeHeight"), out var order) ? order : 0,
            Text = anchor.Text,
            ChildText = anchor.ChildText,
        };

        double Position(XElement? position, double origin, double size, double extent, bool odd, bool horizontal)
        {
            if (position?.Element(Docx.Drawings.Wp + "posOffset")?.Value is { } offset)
                return origin + Emu(offset);
            if (position?.Element(Docx.Drawings.Wp14 + (horizontal ? "pctPosHOffset" : "pctPosVOffset"))?.Value is { } percent)
                return origin + size * Emu(percent) * 12700 / 100000;
            return position?.Element(Docx.Drawings.Wp + "align")?.Value switch
            {
                "right" or "bottom" => origin + size - extent,
                "center" => origin + (size - extent) / 2,
                "inside" => odd == horizontal ? origin : origin + size - extent,
                "outside" => odd == horizontal ? origin + size - extent : origin,
                _ => origin,
            };
        }
    }

    /// <summary>A paragraph's borders and shading (w:pBdr, w:shd).</summary>
    sealed class Frame
    {
        public BorderLine? Top, Left, Bottom, Right, Between;
        public RgbColor? Fill;
        XElement? borders, shading;
        double indentLeft, indentRight;

        public static Frame? Read(ParagraphFormat format)
        {
            var fill = Shading.Color(format.Shading);
            if (format.Borders == null && fill == null)
                return null;
            var b = format.Borders;
            var frame = new Frame
            {
                Top = BorderLine.Read(b.Child("top")),
                Left = BorderLine.Read(b.Child("left") ?? b.Child("start")),
                Bottom = BorderLine.Read(b.Child("bottom")),
                Right = BorderLine.Read(b.Child("right") ?? b.Child("end")),
                Between = BorderLine.Read(b.Child("between")),
                Fill = fill,
                borders = b,
                shading = format.Shading,
                indentLeft = format.Left ?? 0,
                indentRight = format.Right ?? 0,
            };
            return frame.Top is { Visible: true } || frame.Left is { Visible: true } || frame.Bottom is { Visible: true }
                || frame.Right is { Visible: true } || frame.Between is { Visible: true } || fill != null ? frame : null;
        }

        /// <summary>Whether the frame has a line to show.</summary>
        bool Lined => Top is { Visible: true } || Left is { Visible: true } || Bottom is { Visible: true } || Right is { Visible: true }
                      || Between is { Visible: true };

        /// <summary>The frame of an HTML division at its indents (the margins of it and the divisions around it).</summary>
        public static Frame Division(HtmlDiv div, double indentLeft, double indentRight) => new()
        {
            Top = div.Top, Left = div.LeftLine, Bottom = div.Bottom, Right = div.RightLine,
            indentLeft = indentLeft, indentRight = indentRight,
        };

        /// <summary>
        /// Whether another paragraph's frame continues this one: the same borders and indents, whatever their shading
        /// (measured with docx/local/bordergroup-b1, -b3, -b4: bordered paragraphs with different fills or none share
        /// their lines, the between line too; docx/external/libreoffice/extras-ooxmlexport/
        /// tdf153964_topMarginAfterBreak15); paragraphs without borders, by their shading.
        /// </summary>
        public bool Same(Frame? other) =>
            other != null && XNode.DeepEquals(borders, other.borders) && (Lined || XNode.DeepEquals(shading, other.shading))
            && Math.Abs(indentLeft - other.indentLeft) < 0.01 && Math.Abs(indentRight - other.indentRight) < 0.01;

        /// <summary>
        /// The box from <paramref name="boxTop"/> to <paramref name="boxBottom"/>: shading, then the sides, the top or
        /// between line and the bottom line. The shading reaches 1.4 pt left and 1.55 pt right of the text, the side
        /// lines stand their space outside it (measured with docx/local/pbdr).
        /// </summary>
        public void Draw(PageBox page, double columnLeft, double columnWidth, ParagraphFormat format, double boxTop, double boxBottom, bool top, bool bottom, double? between) =>
            Draw(page.Shading, page.Borders, columnLeft, columnWidth, boxTop, boxBottom, top, bottom, between);

        public void Draw(List<FillRect> shadings, List<FillRect> lines, double columnLeft, double columnWidth, double boxTop, double boxBottom, bool top, bool bottom, double? between)
        {
            var innerLeft = columnLeft + indentLeft - 1.4 - (Left is { Visible: true } il ? il.Space : 0);
            var innerRight = columnLeft + columnWidth - indentRight + 1.55 + (Right is { Visible: true } ir ? ir.Space : 0);
            var left = innerLeft - (Left is { Visible: true } l ? l.Total : 0);
            var right = innerRight + (Right is { Visible: true } r ? r.Total : 0);
            if (Fill is { } fill)
                shadings.Add(new FillRect(innerLeft, boxTop, innerRight - innerLeft, boxBottom - boxTop, fill));
            // Double and thin-and-thick lines in their strokes, as tables draw them (docx/external/poi/FancyFoot: a
            // thin-thick top line, 3 pt above 0.75 pt).
            void Across(BorderLine line, double y)
            {
                foreach (var (offset, thickness) in TableLayout.Stripes(line))
                    lines.Add(new FillRect(left, y + offset, right - left, thickness, line.Color));
            }
            void Down(BorderLine line, double x)
            {
                foreach (var (offset, thickness) in TableLayout.Stripes(line))
                    lines.Add(new FillRect(x + offset, boxTop, thickness, boxBottom - boxTop, line.Color));
            }
            if (Left is { Visible: true } leftLine)
                Down(leftLine, left);
            if (Right is { Visible: true } rightLine)
                Down(rightLine, right - rightLine.Total);
            if (top && Top is { Visible: true } topLine)
                Across(topLine, boxTop);
            if (between is { } betweenY && Between is { Visible: true } betweenLine)
                Across(betweenLine, betweenY);
            if (bottom && Bottom is { Visible: true } bottomLine)
                Across(bottomLine, boxBottom - bottomLine.Total);
        }
    }

    /// <summary>
    /// How far a paragraph with mirrored indents (w:mirrorIndents) moves on an even page: its left indent becomes the
    /// inside one, its right the outside, the two trading places where page numbers are even; its lines break as they
    /// would (measured with docx/local/mirror).
    /// </summary>
    double Mirror(ParagraphFormat format) =>
        format.MirrorIndents == true && page!.DisplayNumber % 2 == 0 ? (format.Right ?? 0) - (format.Left ?? 0) : 0;

    void PlaceRow(RowBox row, double left)
    {
        var anchors = new List<CellAnchor>();
        TableLayout.Draw(row, row.Height, left, y, page!.Lines, page.Shading, page.Borders, page.Strokes, anchors);
        foreach (var anchor in anchors)
            page.Drawings.Add(Anchored(anchor.Anchor, page, anchor.ParagraphTop, anchor.LineTop, left, page.TextWidth, anchor, anchor.CharacterX));
        y += row.Height;
        columnEmpty = false;
    }

    /// <summary>The lines below a row at the current position.</summary>
    void Below(RowBox row, double left)
    {
        foreach (var fill in row.BottomBorders)
            page!.Borders.Add(fill with { X = left + fill.X, Y = y + fill.Y });
        page!.Strokes.AddRange(row.BottomStrokes.Select(s => s.Offset(left, y)));
    }

    void Place(List<Block> blocks, int index)
    {
        var paragraph = (ParagraphBlock)blocks[index];
        // A page or column break that begins a paragraph comes before it where the rest of the paragraph goes on in
        // the next page or column (LeadingBreak): its number, its space before and its text go there (measured with
        // docx/external/libreoffice/extras-ooxmlexport/wDateValueFormat: "1 Status" after a page break at the start of
        // its heading stands at the top of the next page, 12 pt down).
        if (LeadingBreak(paragraph) is { } leading)
        {
            if (leading.Type == "page")
                NewPage(section);
            else
                NextColumn(natural: false);
            top = Top.Leading;
            paragraph = paragraph with { Content = paragraph.Content.Where(i => !ReferenceEquals(i, leading)).ToList() };
        }
        var next = index + 1 < blocks.Count ? blocks[index + 1] as ParagraphBlock : null;
        var format = paragraph.Format;
        var (left, width) = Column(column);
        var lines = layout.Layout(paragraph, width);

        if (format.PageBreakBefore == true && !(columnEmpty && column == 0 && page!.Lines.Count == 0))
        {
            NewPage(section);
            top = Top.Break;
            (left, width) = Column(column);
        }

        // Space between this paragraph and the previous one.
        var before = SpaceBefore(paragraph);
        var gap = !columnEmpty ? Gap(before) : top switch
        {
            Top.Document => before,
            Top.Section => Math.Max(0, Gap(before) - pendingAfter),
            // Below balanced columns, whose height holds the space after their last paragraphs, what the space before
            // has more than that space after (measured with page-setup: the heading of the three-column section stands
            // level with the other columns, its 8 pt before already in the 8 pt after above).
            Top.Continuous => balanced ? Math.Max(0, Gap(before) - pendingAfter) : Gap(before),
            // After a break that begins the paragraph, what its space before has more than the space after the
            // paragraph before it (measured with docx/external/libreoffice/extras-ooxmlexport/
            // tdf153964_topMarginAfterBreak15: 60 pt before after an empty paragraph with 8 pt after leave 52 pt;
            // docx/local/breakmark2-*: all of it after paragraphs without space after).
            Top.Leading => Math.Max(0, Gap(before) - pendingAfter),
            _ => 0,
        };
        balanced = false;
        // The space a balanced column gives the paragraph where it does not begin the column.
        var flowGap = Gap(before);

        // Keep with next and keep lines together: move the paragraph to the next column when its keep chain has to
        // break here (ChainBreaks), when it keeps its lines together and they do not fit, also more than a column of
        // them (measured with docx/local/kc7, kc9), or when its first lines do not fit but would in an empty column. A
        // paragraph the one before it is kept with continues that one's chain, which was decided at its start: at the
        // top of a column a chain taller than the rest of it breaks where it must (docx/local/kc1, kc4).
        if (!columnEmpty)
        {
            var chained = previous != null ? previous.Format.KeepNext == true : tableKept;
            bool moves;
            if (format.KeepNext == true && index + 1 < blocks.Count && blocks[index + 1] is ParagraphBlock or TableBlock && !chained)
                moves = ChainBreaks(blocks, index, lines, width, y + gap);
            else if (format.KeepLines == true)
                moves = y + gap + Needed(lines) > Bottom + 0.01;
            else
            {
                var needed = gap + FirstLines(lines, format);
                moves = y + needed > Bottom + 0.01 && needed - gap <= Bottom - page!.BodyTop + 0.01;
            }
            if (moves)
            {
                NextColumn(natural: true);
                (left, width) = Column(column);
                gap = 0;
            }
        }

        // Paragraph borders and shading (catalog: paragraph.borders): paragraphs with the same borders form one box,
        // the top line above the first, the bottom line below the last, the between line between them; a line and
        // its space take room in the paragraph.
        var frame = Frame.Read(format);
        var continues = frame != null && previous != null && frame.Same(Frame.Read(previous.Format)) && !columnEmpty;
        var nextContinues = frame != null && next != null && frame.Same(Frame.Read(next.Format));
        var boxTop = y;
        // The paragraph's top for drawings placed relative to it: its own space before lies inside it.
        var ownBefore = columnEmpty ? gap : Math.Max(0, gap - pendingAfter);
        // HTML divisions with borders frame their paragraphs like paragraph borders, at the division's indents; a
        // division's top line and space come before its first paragraph, its bottom line after its last (measured with
        // docx/local/html-divs).
        var divisions = Divisions(format.DivId);
        var previousDivisions = previous != null ? context.Divs.Chain(previous.Format.DivId).Select(d => d.Id).ToHashSet() : [];
        var nextDivisions = next != null ? context.Divs.Chain(next.Format.DivId).Select(d => d.Id).ToHashSet() : [];
        var divisionTops = new double[divisions.Count];
        for (var d = 0; d < divisions.Count; d++)
            divisionTops[d] = y;
        y += gap;
        for (var d = 0; d < divisions.Count; d++)
        {
            if (!columnEmpty && previousDivisions.Contains(divisions[d].Div.Id))
                continue;
            divisionTops[d] = y;
            y += divisions[d].Frame.Top is { Visible: true } dt ? dt.Space + dt.Total : 0;
        }
        var paragraphTop = (Page: page!, Top: y - ownBefore);
        var betweenAt = y;
        if (frame != null)
        {
            if (!continues)
            {
                boxTop = y;
                y += frame.Top is { Visible: true } t ? t.Space + t.Total : 0;
            }
            else if (frame.Between is { Visible: true } between)
            {
                // The between line has its space above and below it (measured with docx/local/pbdr).
                betweenAt = y + between.Space;
                y += 2 * between.Space + between.Total;
            }
        }
        // An object kept free above and below that would push lines already on the page takes its anchor paragraph to
        // the next page, where the object then stands (measured with docx/generated/wrapping: the rest of the page stays
        // empty).
        if (!columnEmpty && paragraph.Content.OfType<AnchorInline>().Any(anchor =>
                ExclusionOf(Anchored(anchor, page!, paragraphTop.Top, y, left, width)) is { TopAndBottom: true } e && e.Top < y - 0.01 && e.Bottom > page!.BodyTop))
        {
            NextColumn(natural: true);
            (left, width) = Column(column);
        }
        // Lines beside floating objects of the page are laid out again in the room the objects leave; the paragraph's
        // own objects, placed after its lines, count where they will stand (not where the run before put them).
        var exclusions = PageExclusions();
        foreach (var anchor in paragraph.Content.OfType<AnchorInline>())
        {
            if (ExclusionOf(Anchored(anchor, page!, paragraphTop.Top, y, left, width)) is { } own)
            {
                exclusions.RemoveAll(e => e.Source != null && ReferenceEquals(e.Source, own.Source));
                exclusions.Add(own);
            }
        }
        if (exclusions.Count > 0)
        {
            var linesTop = y;
            var bottom = linesTop + lines.Sum(l => l.Height);
            if (exclusions.Any(e => e.Top < Math.Max(bottom, linesTop + 1) && e.Bottom > linesTop))
            {
                var (columnLeft, columnWidth) = (left, width);
                lines = layout.Layout(paragraph, width, room: slot => Room(exclusions, linesTop, slot, columnLeft, columnWidth));
            }
        }
        var firstLineTop = (Page: page!, Top: y);
        var widow = format.WidowControl ?? true;
        // Lines of this paragraph in the current column, and whether it began the column there.
        var here = 0;
        var hereAtTop = columnEmpty;
        (PageBox Page, double Top, double Left, double Width)? anchorAt = null;
        var lineTops = new Dictionary<AnchorInline, (PageBox Page, double Top, double X)>(ReferenceEqualityComparer.Instance);
        var boxPage = page!;
        // Whether the paragraph went on in another column or page, where the objects that pushed its lines down here
        // are not in their way.
        var moved = false;
        for (var k = 0; k < lines.Count; k++)
        {
            var line = lines[k];
            // A line with footnote references needs room for their notes too; a note too long for the rest of the page
            // continues on the next.
            var notesNeeded = NotesHeight(line.Notes);
            var split = false;
            // A line that ends with a hyphen does not end a column: it goes on with the line after it (measured with
            // docx/local/hyph-d).
            var fitHeight = line.Hyphenated && k + 1 < lines.Count ? line.Height + lines[k + 1].FitHeight : line.FitHeight;
            // A line a floating object pushes down has to fit below it, else it goes to the next column or page, with
            // the paragraph's own objects (measured with docx/local/anchorpush-*: a paragraph whose full-width object
            // leaves it no room on the page moves to the next with it, and the lines above keep their places).
            var skip = moved ? 0 : line.Skip;
            if (y + skip + line.FitHeight > Bottom - notesNeeded + 0.01 && !columnEmpty && notesNeeded > 0 && y + skip + line.FitHeight <= Bottom + 0.01
                && SplitNotes(line.Notes!, Bottom - y - skip - line.FitHeight))
                split = true;
            // A line of nothing but a page or column break stays where it is, past the bottom too: it moves nothing on
            // (measured with docx/external/libreoffice/extras-ooxmlexport/FDO77715: headings that begin with a column
            // break, after a full page, stand at the top of the next page, no empty page between).
            else if (y + skip + fitHeight > Bottom - notesNeeded + 0.01 && !columnEmpty && !BreakOnly(line))
            {
                // Widow and orphan control: no single first line at the bottom, no single last line at the top
                // (a paragraph that began the column stays, there is no better place for it).
                if (widow && lines.Count > 1)
                {
                    var back = 0;
                    if (k == 1)
                        back = 1;
                    else if (lines.Count - k == 1 && k >= 2)
                        back = k >= 3 ? 1 : k;
                    if (back > 0 && back <= here && !(hereAtTop && back == here))
                    {
                        page!.Lines.RemoveRange(page.Lines.Count - back, back);
                        flow.RemoveAll(f => f.Index >= page.Lines.Count);
                        // The notes of the lines that move go with them.
                        for (var m = k - back; m < k; m++)
                        {
                            foreach (var id in lines[m].Notes ?? [])
                            {
                                pageNotes.Remove(id);
                                if (splitNote?.Id == id)
                                    (splitNote, carried, bodyEnd) = (null, null, null);
                            }
                        }
                        RecountNotes();
                        k -= back;
                        line = lines[k];
                    }
                }
                NextColumn(natural: true);
                (left, width) = Column(column);
                here = 0;
                hereAtTop = true;
                moved = true;
                skip = 0;
            }
            y += skip;
            // Where the first line stands as planned the paragraph begins above it; moved to another column or page,
            // it begins with the line.
            anchorAt ??= (page!, page == paragraphTop.Page && page == firstLineTop.Page && Math.Abs(y - skip - firstLineTop.Top) < 0.01
                ? paragraphTop.Top : y, left, width);
            foreach (var anchor in line.Anchors ?? [])
                lineTops[anchor] = (page!, y, left + (line.AnchorX?.GetValueOrDefault(anchor) ?? 0));
            if (page!.Lines.Count == 0 && column == 0 && TopTrim(line, format) is var trim and > 0)
                line = line.Trimmed(trim);
            flow.Add(new FlowLine(page!.Lines.Count, k == 0 ? flowGap : 0, k == 0 && here == 0 ? gap : 0, paragraphNumber, k, lines.Count,
                widow, format.KeepNext == true, format.KeepLines == true));
            page!.Lines.Add(new PlacedLine(line, left + Mirror(format), y)
            {
                Numbered = format.SuppressLineNumbers != true,
                HangTop = k == 0 && anchorAt?.Page == page ? Math.Min(y, anchorAt.Value.Top) : null,
            });
            if (k == 0)
                page.Paragraphs.Add(paragraph);
            // A chapter heading numbers its page and the pages after it (measured with docx/local/chapter: a page with
            // the end of one chapter and the start of the next has the next's number).
            if (k == 0 && paragraph.Label is { Own.Length: > 0 } chapterLabel && section.ChapterLevel is { } chapterLevel && format.OutlineLevel == chapterLevel - 1)
                page.Chapter = chapter = chapterLabel.Own;
            here++;
            y += line.Height;
            columnEmpty = false;
            if (split)
                bodyEnd = y;
            else
                AddNotes(line.Notes);
            if (line.EndBreak is "page")
            {
                NewPage(section);
                top = Top.Break;
                (left, width) = Column(column);
                here = 0;
                hereAtTop = true;
            }
            else if (line.EndBreak is "column")
            {
                NextColumn(natural: false);
                (left, width) = Column(column);
                here = 0;
                hereAtTop = true;
            }
        }
        if (frame != null)
        {
            if (!nextContinues)
                y += frame.Bottom is { Visible: true } b ? b.Space + b.Total : 0;
            if (boxPage != page)
                boxTop = page!.BodyTop;
            frame.Draw(page!, left + Mirror(format), width, format, boxTop, y, top: !continues || boxPage != page, bottom: !nextContinues,
                between: continues && boxPage == page ? betweenAt : null);
        }
        for (var d = divisions.Count - 1; d >= 0; d--)
        {
            var (div, divisionFrame) = divisions[d];
            var opens = !previousDivisions.Contains(div.Id);
            var closes = !nextDivisions.Contains(div.Id);
            if (closes)
                y += divisionFrame.Bottom is { Visible: true } db ? db.Space + db.Total : 0;
            var divisionTop = boxPage != page ? page!.BodyTop : divisionTops[d];
            divisionFrame.Draw(page!, left, width, format, divisionTop, y, top: opens || boxPage != page, bottom: closes, between: null);
        }
        foreach (var bookmark in paragraph.Content.OfType<BookmarkInline>())
        {
            var (bookmarkPage, bookmarkTop, bookmarkLeft, _) = anchorAt ?? (page!, y, left, width);
            bookmarkPage.Bookmarks.Add((bookmark.Name, bookmarkLeft, bookmarkTop));
        }
        // Floating drawings anchored in the paragraph, placed relative to its page, column, paragraph or line.
        foreach (var anchor in paragraph.Content.OfType<AnchorInline>())
        {
            var (anchorPage, anchorTop, columnLeft, columnWidth) = anchorAt ?? (page!, y, left, width);
            var inLine = lineTops.TryGetValue(anchor, out var at) && at.Page == anchorPage;
            anchorPage.Drawings.Add(Anchored(anchor, anchorPage, anchorTop, inLine ? at.Top : anchorTop, columnLeft, columnWidth,
                character: inLine ? at.X : columnLeft));
        }
        pendingAfter = SpaceAfter(paragraph, index + 1 < blocks.Count ? blocks[index + 1] : null);
        pendingOwnAfter = OwnAfter(paragraph);
        // Bar tabs reach down through the space after the paragraph (measured with docx/generated/tabs).
        lines[^1].BarExtension = pendingAfter;
        if (flow.Count > 0 && flow[^1].Paragraph == paragraphNumber)
            flow[^1].After = pendingAfter;
        page!.TrailingSpace = pendingAfter;
        previous = paragraph;
        stylePrevious = paragraph;
        paragraphNumber++;
    }

    /// <summary>
    /// Balances the columns of the current section on this page (catalog: section.columns): its lines are counted, not
    /// measured, and spread over the columns, as many in each as the least count that holds them all, the first columns
    /// taking the extra lines (measured with docx/local/cols-balance: 9 lines go 5/4, 15 lines 8/7; in
    /// docx/generated/page-setup a heading and 60 lines go 31/30 although the first column is 42 pt longer), with the
    /// rules of page breaks (widows and orphans, keep with next, keep lines together); the next section begins below
    /// the longest column.
    /// </summary>
    void Balance()
    {
        if (page == null || flow.Count == 0)
            return;
        var items = flow;
        var heights = items.Select(f => page.Lines[f.Index].Line.Height).ToList();
        var fits = items.Select(f => page.Lines[f.Index].Line.FitHeight).ToList();
        var columns = section.Columns;

        // Where a column may end before item b, moved back by the page break rules; -1 when the column began there.
        int BreakBefore(int b, int columnStart)
        {
            var moved = true;
            while (moved && b > columnStart)
            {
                moved = false;
                var item = items[b];
                if (item.Widows && item.Lines > 1)
                {
                    // A single last line goes with the line before it; a single first line goes with its paragraph.
                    if (item.Line == item.Lines - 1 && item.Line >= 1)
                    {
                        b -= item.Line >= 2 ? 1 : item.Line;
                        moved = true;
                        continue;
                    }
                    if (item.Line == 1)
                    {
                        b -= 1;
                        moved = true;
                        continue;
                    }
                }
                if (item.KeepLines && item.Line > 0)
                {
                    b -= item.Line;
                    moved = true;
                    continue;
                }
                // The paragraph before keeps with this one.
                if (item.Line == 0 && b > 0 && items[b - 1].KeepNext)
                {
                    b -= items[b - 1].Line + 1;
                    moved = true;
                }
            }
            return b > columnStart ? b : -1;
        }

        List<(int Column, double Top)>? Place(int count)
        {
            var placement = new List<(int Column, double Top)>();
            var c = 0;
            var used = 0.0;
            var columnStart = 0;
            var bottom = Bottom - columnTop;
            for (var i = 0; i < items.Count; i++)
            {
                var g = i == 0 ? items[0].FirstGap : used > 0 ? items[i].Gap : 0;
                if (used > 0 && (i - columnStart >= count || used + g + fits[i] > bottom + 0.01))
                {
                    var b = BreakBefore(i, columnStart);
                    if (b < 0)
                        b = i;
                    c++;
                    if (c >= columns)
                        return null;
                    placement.RemoveRange(b, placement.Count - b);
                    i = b;
                    columnStart = b;
                    used = 0;
                    g = 0;
                }
                placement.Add((c, used + g));
                used += g + heights[i];
            }
            return placement;
        }

        List<(int Column, double Top)>? result = null;
        for (var count = (items.Count + columns - 1) / columns; result == null && count <= items.Count; count++)
            result = Place(count);
        if (result == null)
            return;
        var bottom = columnTop;
        for (var i = 0; i < items.Count; i++)
        {
            var (c, offset) = result[i];
            var old = page.Lines[items[i].Index];
            page.Lines[items[i].Index] = old with { Left = Column(c).Left, Top = columnTop + offset };
            // A column ends below its last line and the space after its paragraph (measured with page-setup: the
            // next section's heading stands its space after and its own space before below the longest column).
            var columnEnds = i + 1 == items.Count || result[i + 1].Column != c;
            bottom = Math.Max(bottom, columnTop + offset + old.Line.Height + (columnEnds ? items[i].After : 0));
        }
        y = bottom;
        column = 0;
        balanced = true;
    }

    /// <summary>
    /// The lines between the columns of the current section on this page (w:cols/@sep): in the middle of each space
    /// between columns, from the columns' top to the bottom of the longest column (measured with page-setup, page 5).
    /// </summary>
    void CloseColumns()
    {
        if (page == null || flow.Count == 0 || !section.ColumnSeparator || section.Columns < 2)
            return;
        var bottom = flow.Max(f => page.Lines[f.Index].Top + page.Lines[f.Index].Line.Height);
        for (var c = 0; c + 1 < section.Columns; c++)
        {
            var (left, width) = Column(c);
            var next = Column(c + 1).Left;
            page.ColumnLines.Add(((left + width + next) / 2, columnTop, bottom));
        }
        flow.Clear();
    }

    /// <summary>The height a paragraph needs at least: its first two lines with widow control, else its first.</summary>
    /// <summary>
    /// The height the first lines of a paragraph need together: two with widow control, one without; a first line
    /// that ends in a page or column break stands alone, the lines after it go on in the next column anyway (measured
    /// with docx/external/libreoffice/extras-ooxmlexport/FDO77715: headings that begin with a column break near the
    /// bottom of a page leave no empty page).
    /// </summary>
    static double FirstLines(List<Line> lines, ParagraphFormat format) =>
        Needed(lines.Take((format.WidowControl ?? true) && lines.Count > 1 && lines[0].EndBreak is not ("page" or "column") ? 2 : 1).ToList());

    /// <summary>The height lines need on a page: all of them, the last without what may hang past the bottom.</summary>
    static double Needed(List<Line> lines) => lines.Count == 0 ? 0 : lines.Sum(l => l.Height) - lines[^1].Overhang;

    /// <summary>
    /// Whether the paragraph at <paramref name="index"/>, kept with the next one and beginning at <paramref name="top"/>
    /// below other content of the column, moves to the next column with its keep chain: laid on from here, the chain's
    /// paragraphs (those kept with the next one and the first one that is not) reach one that does not fit and cannot
    /// break here (a line or two, too few lines for widow control, lines kept together) and must begin the next column;
    /// the paragraphs kept with it go along back to this one unless one of them can break and give its last lines (as
    /// Word pushes paragraphs back, measured with docx/local/kc2 … kc12: chains taller than a column move too, a
    /// paragraph that can break keeps its start here). Lays out no more than a column of paragraphs, so long chains
    /// (docx/external/powertools/LIR*: 3500 list items kept with each other) cost little.
    /// </summary>
    bool ChainBreaks(List<Block> blocks, int index, List<Line>? lines, double width, double top)
    {
        var y = top;
        // Whether a paragraph of the chain placed so far could break and give its last lines to the next column.
        var gives = false;
        for (var k = index; k < blocks.Count; k++)
        {
            if (blocks[k] is TableBlock table)
            {
                if (table.Table.Format.Floating != null)
                    return false;
                if (k > index && blocks[k - 1] is ParagraphBlock before)
                    y += SpaceAfter(before, table);
                // Rows do not break for a chain: each must fit; one kept with the next carries the chain on, through
                // the table to the block after it where all are (docx/local/tk10, tk11, tk13: a heading goes along with
                // a first row that does not fit below it).
                var box = LaidTable(table, width, k > index ? blocks[k - 1] as ParagraphBlock ?? LastParagraph(blocks, k - 1) : stylePrevious, keep: true);
                for (var r = 0; r < box.Rows.Count; r++)
                {
                    if (y + box.Rows[r].Height + box.Rows[r].BottomBorderWidth > Bottom + 0.01)
                        return !gives;
                    y += box.Rows[r].Height;
                    if (!RowKept(table.Table, r))
                        return false;
                }
                continue;
            }
            if (blocks[k] is not ParagraphBlock paragraph)
                return false;
            if (k > index)
            {
                if (paragraph.Format.PageBreakBefore == true)
                    return false;
                y += blocks[k - 1] switch
                {
                    ParagraphBlock before => Between(before, paragraph),
                    TableBlock before => context.Between(0, SpaceBefore(paragraph, LastParagraph(before.Table), false), 0),
                    _ => 0,
                };
            }
            var own = k == index && lines != null ? lines : KeptLines(paragraph, width);
            var last = paragraph.Format.KeepNext != true || k + 1 >= blocks.Count || blocks[k + 1] is not (ParagraphBlock or TableBlock);
            var room = Bottom - y;
            // The last paragraph needs its first lines here, all of them where it keeps them together.
            var needed = last && paragraph.Format.KeepLines != true ? FirstLines(own, paragraph.Format) : Needed(own);
            if (needed <= room + 0.01)
            {
                if (last)
                    return false;
                gives |= Breaks(own, paragraph.Format, double.MaxValue);
                y += own.Sum(l => l.Height);
                continue;
            }
            if (!last && Breaks(own, paragraph.Format, room))
                return false;
            return !gives;
        }
        return false;
    }

    /// <summary>
    /// Whether a table's row (of those that show) is kept with the next row or block: its first cell's first
    /// paragraph is kept with the next one (measured with docx/local/tk4, tk6, tk8, tk9: the other cells and
    /// paragraphs do not count).
    /// </summary>
    static bool RowKept(TableModel table, int row) =>
        table.Rows.Where(r => !r.Format.Hidden).ElementAtOrDefault(row)?.Cells.FirstOrDefault()?.Blocks.FirstOrDefault() is ParagraphBlock { Format.KeepNext: true };

    /// <summary>Whether the last table placed ends with a row kept with the next block.</summary>
    bool tableKept;

    /// <summary>The last paragraph of the block before a position, if it is a table.</summary>
    static ParagraphBlock? LastParagraph(List<Block> blocks, int index) =>
        index >= 0 && blocks[index] is TableBlock table ? LastParagraph(table.Table) : null;

    /// <summary>
    /// A table laid out at a width below a paragraph (whose style counts for its spacing); one the keep chain
    /// look-ahead laid out (<paramref name="keep"/>) is kept for the placement, which takes it.
    /// </summary>
    TableBox LaidTable(TableBlock table, double width, ParagraphBlock? prior, bool keep)
    {
        var before = prior != null ? context.Styles.ParagraphStyleOf(prior.Format.Style) : TableLayout.NoParagraph;
        if (laidTables.Remove(table, out var laid) && laid.Width == width && laid.Before == before)
        {
            if (keep)
                laidTables[table] = laid;
            return laid.Box;
        }
        var box = tables.Layout(table.Table, width, before: before);
        if (keep)
            laidTables[table] = (width, before, box);
        return box;
    }

    /// <summary>Tables the keep chain look-ahead laid out, with the width and the paragraph style before them.</summary>
    readonly Dictionary<TableBlock, (double Width, string? Before, TableBox Box)> laidTables = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Whether a paragraph's lines can break between columns with <paramref name="room"/> points here: not kept
    /// together, and as many lines as widow and orphan control leave at least on each side (two, else one).
    /// </summary>
    static bool Breaks(List<Line> lines, ParagraphFormat format, double room)
    {
        if (format.KeepLines == true)
            return false;
        var least = format.WidowControl ?? true ? 2 : 1;
        if (lines.Count < 2 * least)
            return false;
        var (fit, height) = (0, 0.0);
        foreach (var line in lines)
        {
            if (height + line.FitHeight > room + 0.01)
                break;
            height += line.Height;
            fit++;
        }
        return fit >= least;
    }

    /// <summary>
    /// The page or column break a paragraph begins with (after marks that take no room) where the rest of the
    /// paragraph goes on in the next page or column: always after a column break, also one that ends the page, whose
    /// paragraph mark goes along and takes a line there with floating objects anchored to it; after a page break
    /// where text or an inline drawing follows, or where w:splitPgBreakAndParaMark takes the mark along, which Word
    /// 2013 and later ignore (compatibility mode 15); null otherwise (measured with docx/local/breakmark-* in modes 11,
    /// 14 and 15, breakmark4-*; docx/external/libreoffice/extras-ooxmlexport/tdf153613_anchoredAfterPgBreak, -2, -3: a picture
    /// anchored after a page break stays on the page before unless text follows or the option is on;
    /// tdf153592_columnBreaks). The start of a content control is a mark that does take its place: the break then
    /// ends the paragraph's first line like any other (ControlStartsBefore).
    /// </summary>
    BreakInline? LeadingBreak(ParagraphBlock paragraph)
    {
        var k = 0;
        while (k < paragraph.Content.Count && paragraph.Content[k] is BookmarkInline or BookmarkEndInline or PermissionInline or CommentInline
               or TextInline { Text.Length: 0 })
            k++;
        if (k >= paragraph.Content.Count || paragraph.Content[k] is not BreakInline { Type: "page" or "column" } leading)
            return null;
        if (ControlStartsBefore(paragraph.Element))
            return null;
        if (leading.Type == "column")
            return leading;
        var rest = paragraph.Content.Skip(k + 1).ToList();
        if (rest.Any(i => i is TextInline { Text.Length: > 0 } or DrawingInline))
            return leading;
        return context.CompatibilityMode < 15 && context.Compat("splitPgBreakAndParaMark") ? leading : null;
    }

    /// <summary>
    /// Whether a content control starts in a paragraph before its first page or column break: a block control whose
    /// first paragraph it is, or a control in the paragraph around or before the break (measured with docx/local/
    /// leadbreak-j, -m, -n, -o, -p, -q: there the space before and the number stay with the break on the page
    /// before, the text goes on at the top of the next page; in a control's second paragraph they go with the text,
    /// as docx/external/libreoffice/extras-ooxmlexport/tdf154478's TOC heading shows).
    /// </summary>
    static bool ControlStartsBefore(XElement element)
    {
        var w = Docx.Wml.W;
        for (var a = element.Parent; a != null && a.Name != w + "body" && a.Name != w + "tc" && a.Name != w + "txbxContent"; a = a.Parent)
            if (a.Name == w + "sdtContent" && a.Descendants(w + "p").FirstOrDefault() == element)
                return true;
        var first = element.Descendants(w + "br").FirstOrDefault(b => b.Attr("type") is "page" or "column");
        if (first == null)
            return false;
        return first.Ancestors(w + "sdt").Any(c => c.Ancestors().Contains(element))
               || element.Descendants(w + "sdt").Any(c => c.IsBefore(first));
    }

    /// <summary>Whether a line holds nothing but a page or column break (and marks that take no room).</summary>
    static bool BreakOnly(Line line) =>
        line.EndBreak is "page" or "column" && line.Pieces.All(p => p.Drawing == null && !p.IsLabel && string.IsNullOrWhiteSpace(p.Text));

    /// <summary>The lines of a paragraph in a keep chain, laid out once for the width they were laid out at.</summary>
    List<Line> KeptLines(ParagraphBlock paragraph, double width)
    {
        if (!keptLines.TryGetValue(paragraph, out var kept) || kept.Width != width)
            keptLines[paragraph] = kept = (width, layout.Layout(paragraph, width));
        return kept.Lines;
    }

    /// <summary>The lines of paragraphs in keep chains and the width they were laid out at.</summary>
    readonly Dictionary<ParagraphBlock, (double Width, List<Line> Lines)> keptLines = new(ReferenceEqualityComparer.Instance);

    bool? suppressTopSpacing, suppressSpacingAtTop;

    /// <summary>
    /// How much less room the first line on a page has above its text (catalog: settings.compatibility): where the
    /// document says w:suppressTopSpacing, an exact line has its baseline 9.6 pt below its top and an at-least line
    /// loses its height less 9.6 pt, whatever its text (measured with docx/local/c5-top and c6-top in modes 15 and
    /// 12: Arial from 8 to 40 pt, Times New Roman and Calibri at 15 to 40 pt high lines, the text of large fonts above
    /// the page's margin then); before Word 2013, where it says w:suppressSpacingAtTopOfPage, an at-least line or one
    /// on the document grid has its natural ascent above the baseline (measured with docx/local/c4-misc12, c5-top12
    /// and c6-top12).
    /// </summary>
    double TopTrim(Line line, ParagraphFormat format)
    {
        suppressTopSpacing ??= context.Compat("suppressTopSpacing");
        suppressSpacingAtTop ??= context.CompatibilityMode < 15 && context.Compat("suppressSpacingAtTopOfPage");
        var rule = format.LineRule ?? "auto";
        if (suppressTopSpacing == true && rule is "exact")
            return line.Baseline - 9.6;
        if (suppressTopSpacing == true && rule is "atLeast")
            return (format.Line ?? 240) / 20 - 9.6;
        if (suppressSpacingAtTop == true && (rule is "atLeast" || rule is "auto" && layout.LinePitch != null && format.SnapToGrid != false))
            return line.Baseline - line.Above - line.RaisedAbove;
        return 0;
    }

    /// <summary>The space between the previous paragraph and one with the given space before (see <see cref="DocumentContext.Between"/>).</summary>
    double Gap(double before) => context.Between(pendingAfter, before, pendingOwnAfter);

    /// <summary>The space between two paragraphs that follow each other (see <see cref="DocumentContext.Between"/>).</summary>
    double Between(ParagraphBlock first, ParagraphBlock second) =>
        context.Between(SpaceAfter(first, second), SpaceBefore(second, first, false), OwnAfter(first));

    /// <summary>The space before the paragraph being placed, after the previous one.</summary>
    double SpaceBefore(ParagraphBlock paragraph) => SpaceBefore(paragraph, stylePrevious, paragraphNumber == 0);

    /// <summary>The last paragraph of a table: its last cell's, in a nested table the nested table's.</summary>
    static ParagraphBlock? LastParagraph(TableModel table) =>
        table.Rows.LastOrDefault()?.Cells.LastOrDefault()?.Blocks.LastOrDefault() switch
        {
            ParagraphBlock paragraph => paragraph,
            TableBlock nested => LastParagraph(nested.Table),
            _ => null,
        };

    /// <summary>The space before a paragraph; contextual spacing drops it after a paragraph of the same style.</summary>
    /// <param name="prior">The paragraph before it, if one.</param>
    /// <param name="first">Whether it begins the document.</param>
    double SpaceBefore(ParagraphBlock paragraph, ParagraphBlock? prior, bool first)
    {
        if (paragraph.Format.ContextualSpacing == true && prior != null && SameStyle(prior, paragraph))
            return 0;
        // HTML auto spacing (w:beforeAutospacing) is 14 pt, none before the document's first paragraph (measured with
        // docx/local/html-divs, altchunk and c2-html, 14 pt after a table too); where the document says
        // w:doNotUseHTMLParagraphAutoSpacing, the paragraph's own space before (measured with docx/local/c2-html).
        if (paragraph.Format.BeforeAuto == true && !context.AddsSpacing)
            return first || prior != null && TableLayout.ListPair(prior, paragraph) ? 0 : 14;
        return paragraph.Format.Before ?? 0;
    }

    /// <summary>The bordered HTML divisions around a paragraph, the outermost first, with their frames at their indents.</summary>
    List<(HtmlDiv Div, Frame Frame)> Divisions(string? id)
    {
        var result = new List<(HtmlDiv, Frame)>();
        var (left, right) = (0.0, 0.0);
        foreach (var div in context.Divs.Chain(id))
        {
            (left, right) = (left + div.Left, right + div.Right);
            if (div.Bordered)
                result.Add((div, Frame.Division(div, left, right)));
        }
        return result;
    }

    /// <summary>The space after a paragraph; contextual spacing drops it before a paragraph of the same style.</summary>
    double SpaceAfter(ParagraphBlock paragraph, ParagraphBlock? next) =>
        paragraph.Format.ContextualSpacing == true && next != null && SameStyle(paragraph, next) ? 0
        : paragraph.Format.AfterAuto == true && !context.AddsSpacing && next != null && TableLayout.ListPair(paragraph, next) ? 0
        : OwnAfter(paragraph);

    /// <summary>The space after a paragraph before the block that follows it: a table's first paragraph counts for contextual spacing (measured with docx/local/c5-cells).</summary>
    double SpaceAfter(ParagraphBlock paragraph, Block? next) =>
        SpaceAfter(paragraph, next switch { ParagraphBlock p => p, TableBlock t => TableLayout.FirstParagraph(t.Table), _ => null });

    /// <summary>A paragraph's space after, contextual spacing aside: HTML auto spacing (w:afterAutospacing) 14 pt.</summary>
    double OwnAfter(ParagraphBlock paragraph) =>
        paragraph.Format.AfterAuto == true && !context.AddsSpacing ? 14 : paragraph.Format.After ?? 0;

    bool SameStyle(ParagraphBlock a, ParagraphBlock b) =>
        context.Styles.ParagraphStyleOf(a.Format.Style) == context.Styles.ParagraphStyleOf(b.Format.Style);

    /// <summary>
    /// Vertical alignment of the page's text (w:vAlign): centered or at the bottom, the text counted from the top
    /// margin (with the space before its first paragraph) to the space after its last paragraph (measured with
    /// page-setup, page 4).
    /// </summary>
    static void AlignVertically(PageBox page)
    {
        var align = page.Section.VerticalAlign;
        if (align is not ("center" or "bottom") || page.Lines.Count == 0)
            return;
        var top = page.BodyTop;
        var bottom = page.BodyBottom;
        var used = page.Lines.Max(l => l.Top + l.Line.Height) + page.TrailingSpace - top;
        var shift = align == "center" ? (bottom - top - used) / 2 : bottom - top - used;
        for (var i = 0; i < page.Lines.Count; i++)
            page.Lines[i] = page.Lines[i] with { Top = page.Lines[i].Top + shift };
        foreach (var fills in new[] { page.Shading, page.Borders })
        {
            for (var i = 0; i < fills.Count; i++)
                fills[i] = fills[i] with { Y = fills[i].Y + shift };
        }
        for (var i = 0; i < page.Strokes.Count; i++)
            page.Strokes[i] = page.Strokes[i].Offset(0, shift);
    }
}
