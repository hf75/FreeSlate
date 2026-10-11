namespace FreeSlate.Docx.Layout;

/// <summary>
/// A line placed on a page: its left edge (the column's) and its top, in points from the page's top left; a line of
/// rotated text (table cells, catalog: table.cells) turns by <paramref name="Angle"/> degrees clockwise about a point
/// given relative to its left edge and top.
/// </summary>
public sealed record PlacedLine(Line Line, double Left, double Top, double Angle = 0, double PivotX = 0, double PivotY = 0);

/// <summary>A page of the laid-out document.</summary>
public sealed class PageBox
{
    public int Number { get; init; }
    public required SectionProperties Section { get; init; }
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

    public double Width => Section.PageWidth;
    public double Height => Section.PageHeight;
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
    enum Top { Document, Section, Break, Continuous }

    Top top = Top.Document;
    double pendingAfter;      // space after the previous paragraph
    ParagraphBlock? previous;

    public List<PageBox> Paginate(List<Block> blocks, SectionProperties lastSection)
    {
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
            StartSection(properties, s == 0);
            for (var i = 0; i < sectionBlocks.Count; i++)
            {
                if (sectionBlocks[i] is TableBlock)
                    PlaceTable(sectionBlocks, i);
                else
                    Place(sectionBlocks, i);
            }
            // A continuous section break balances the columns above it.
            if (s + 1 < sections.Count && sections[s + 1].Properties.Start == "continuous" && section.Columns > 1)
                Balance();
            CloseColumns();
        }
        if (pages.Count == 0)
            NewPage(lastSection, firstOfSection: true);
        foreach (var p in pages)
            AlignVertically(p);
        if (reader != null)
        {
            // NUMPAGES counts the blank pages, SECTIONPAGES does not (measured with docx/local/hf-parity).
            var sectionPages = pages.Where(p => !p.Blank).GroupBy(p => p.SectionIndex).ToDictionary(g => g.Key, g => g.Count());
            foreach (var p in pages)
            {
                var fields = new PageFields(p.DisplayNumber, p.Section.PageNumberFormat, pages.Count, sectionPages.GetValueOrDefault(p.SectionIndex));
                if (p.Header != null)
                    p.Lines.AddRange(Story(p.Header, p, fields, header: true).Lines);
                if (p.Footer != null)
                    p.Lines.AddRange(Story(p.Footer, p, fields, header: false).Lines);
            }
        }
        return pages;
    }

    void StartSection(SectionProperties properties, bool first)
    {
        var start = first ? "nextPage" : properties.Start;
        section = properties;
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
        var number = firstOfSection && properties.PageNumberStart is { } restart ? restart
            : pages.Count == 0 ? 1 : pages[^1].DisplayNumber + 1;
        // The first page of a section with a title page has the first header; with different odd and even pages
        // (w:evenAndOddHeaders) even pages have the even one; a type the sections do not give is an empty header.
        var type = firstOfSection && properties.TitlePage ? "first"
            : number % 2 == 0 && context.Settings.Child("evenAndOddHeaders").OnOff() == true ? "even" : "default";
        // Mirrored margins swap the left and right margins of even pages; the gutter is on the inside, at the top
        // (w:gutterAtTop) or on the right (w:rtlGutter).
        var even = number % 2 == 0 && context.Settings.Child("mirrorMargins").OnOff() == true;
        var gutterAtTop = context.Settings.Child("gutterAtTop").OnOff() == true;
        var rightGutter = properties.Element.Child("rtlGutter").OnOff() == true;
        var gutterLeft = !gutterAtTop && (even ? rightGutter : !rightGutter);
        var left = (even ? properties.Right : properties.Left) + (gutterLeft ? properties.Gutter : 0);
        var width = properties.PageWidth - properties.Left - properties.Right - (gutterAtTop ? 0 : properties.Gutter);
        page = new PageBox
        {
            Number = pages.Count + 1,
            Section = properties,
            DisplayNumber = number,
            SectionIndex = sectionIndex,
            FirstOfSection = firstOfSection,
            Blank = blank,
            Header = reader != null && !blank ? properties.Headers.GetValueOrDefault(type) : null,
            Footer = reader != null && !blank ? properties.Footers.GetValueOrDefault(type) : null,
            TextLeft = left,
            TextWidth = Math.Max(1, width),
        };
        // A header that reaches below the top margin pushes the body down, a footer above the bottom margin pushes
        // its end up, each with the space before and after its paragraphs, unless the margin is negative (fixed;
        // measured with docx/local/hf-tall, hf-short).
        page.BodyTop = properties.Top + (gutterAtTop ? properties.Gutter : 0);
        page.BodyBottom = properties.PageHeight - properties.Bottom;
        if (page.Header != null && !properties.TopFixed)
            page.BodyTop = Math.Max(page.BodyTop, Story(page.Header, page, null, header: true).Bottom);
        if (page.Footer != null && !properties.BottomFixed)
            page.BodyBottom = Math.Min(page.BodyBottom, Story(page.Footer, page, null, header: false).Top);
        pages.Add(page);
        column = 0;
        y = page.BodyTop;
        columnTop = y;
        columnEmpty = true;
        flow.Clear();
    }

    /// <summary>
    /// A header or footer laid out on a page of a section (catalog: headerFooter.parts): across the text area, a
    /// header from w:header below the page's top edge, a footer ending w:footer above its bottom edge; where its
    /// content begins and ends.
    /// </summary>
    (List<PlacedLine> Lines, double Top, double Bottom) Story(string part, PageBox on, PageFields? fields, bool header)
    {
        var s = on.Section;
        var lines = new List<PlacedLine>();
        var left = on.TextLeft;
        var width = on.TextWidth;
        var y = 0.0;
        double after = 0;
        ParagraphBlock? before = null;
        foreach (var paragraph in reader!.Story(part).OfType<ParagraphBlock>())
        {
            var gap = before == null ? paragraph.Format.Before ?? 0 : Math.Max(after, paragraph.Format.Before ?? 0);
            y += gap;
            foreach (var line in layout.Layout(paragraph, width, fields))
            {
                lines.Add(new PlacedLine(line, left, y));
                y += line.Height;
            }
            after = paragraph.Format.After ?? 0;
            before = paragraph;
        }
        y += after;
        var top = header ? s.Header : s.PageHeight - s.Footer - y;
        for (var i = 0; i < lines.Count; i++)
            lines[i] = lines[i] with { Top = lines[i].Top + top };
        return (lines, top, top + y);
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

    double Bottom => page!.BodyBottom;

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
    void PlaceTable(List<Block> blocks, int index)
    {
        var table = (TableBlock)blocks[index];
        var (left, width) = Column(column);
        var box = tables.Layout(table.Table, width);
        if (!columnEmpty)
            y += pendingAfter;
        var headers = box.Rows.TakeWhile(r => r.Header).ToList();
        RowBox? last = null;
        var tableTop = y;
        // Where the table breaks, the line below the page's last row closes it, and a frame (cell spacing) ends.
        void Break()
        {
            if (last != null)
                Below(last, left);
            TableLayout.Frame(box, left, tableTop, y, page!.Borders);
            NextColumn(natural: true);
            (left, width) = Column(column);
            tableTop = y;
        }
        for (var r = 0; r < box.Rows.Count; r++)
        {
            var row = box.Rows[r];
            while (y + row.Height > Bottom + 0.01)
            {
                // A row that does not fit splits at a line of each cell (unless it may not split, measured with
                // docx/local/tbl-autofit), else moves on; a row taller than a page that cannot split overflows.
                if (Bottom - y > row.TopWidth + 1 && row.Split(Bottom - y) is { } parts)
                {
                    PlaceRow(parts.Head, left);
                    last = parts.Head;
                    row = parts.Tail;
                }
                else if (columnEmpty)
                    break;
                Break();
                if (!row.Header)
                {
                    foreach (var header in headers)
                    {
                        PlaceRow(header, left);
                        last = header;
                    }
                }
            }
            PlaceRow(row, left);
            last = row;
        }
        // A table that another table follows directly continues in it: Word joins them, one line between (measured
        // with docx/local/tbl-geom).
        if (last != null && !(index + 1 < blocks.Count && blocks[index + 1] is TableBlock))
        {
            Below(last, left);
            y += last.BottomBorderWidth;
        }
        TableLayout.Frame(box, left, tableTop, y, page!.Borders);
        pendingAfter = 0;
        page!.TrailingSpace = 0;
        previous = null;
        paragraphNumber++;
    }

    void PlaceRow(RowBox row, double left)
    {
        TableLayout.Draw(row, row.Height, left, y, page!.Lines, page.Shading, page.Borders, page.Strokes);
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
        var gap = !columnEmpty ? Math.Max(pendingAfter, before) : top switch
        {
            Top.Document => before,
            Top.Section => Math.Max(0, before - pendingAfter),
            // Below balanced columns, whose height holds the space after their last paragraphs, what the space before
            // has more than that space after (measured with page-setup: the heading of the three-column section stands
            // level with the other columns, its 8 pt before already in the 8 pt after above).
            Top.Continuous => balanced ? Math.Max(0, before - pendingAfter) : Math.Max(pendingAfter, before),
            _ => 0,
        };
        balanced = false;
        // The space a balanced column gives the paragraph where it does not begin the column.
        var flowGap = Math.Max(pendingAfter, before);

        // Keep with next and keep lines together: move the paragraph to the next column when it (and the first line
        // of the next paragraph) does not fit here but would fit in an empty column.
        if (!columnEmpty)
        {
            var needed = gap + (format.KeepLines == true ? Needed(lines) : FirstLines(lines, format));
            if (format.KeepNext == true && next != null)
                needed = gap + lines.Sum(l => l.Height) + KeptNext(blocks, index + 1, width);
            // What does not fit even an empty column is broken anyway.
            if (y + needed > Bottom + 0.01 && needed - gap <= Bottom - page!.BodyTop + 0.01)
            {
                NextColumn(natural: true);
                (left, width) = Column(column);
                gap = 0;
            }
        }

        y += gap;
        var widow = format.WidowControl ?? true;
        // Lines of this paragraph in the current column, and whether it began the column there.
        var here = 0;
        var hereAtTop = columnEmpty;
        for (var k = 0; k < lines.Count; k++)
        {
            var line = lines[k];
            if (y + line.FitHeight > Bottom + 0.01 && !columnEmpty)
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
                        k -= back;
                        line = lines[k];
                    }
                }
                NextColumn(natural: true);
                (left, width) = Column(column);
                here = 0;
                hereAtTop = true;
            }
            flow.Add(new FlowLine(page!.Lines.Count, k == 0 ? flowGap : 0, k == 0 && here == 0 ? gap : 0, paragraphNumber, k, lines.Count,
                widow, format.KeepNext == true, format.KeepLines == true));
            page!.Lines.Add(new PlacedLine(line, left, y));
            here++;
            y += line.Height;
            columnEmpty = false;
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
        pendingAfter = SpaceAfter(paragraph, next);
        // Bar tabs reach down through the space after the paragraph (measured with docx/generated/tabs).
        lines[^1].BarExtension = pendingAfter;
        if (flow.Count > 0 && flow[^1].Paragraph == paragraphNumber)
            flow[^1].After = pendingAfter;
        page!.TrailingSpace = pendingAfter;
        previous = paragraph;
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
    static double FirstLines(List<Line> lines, ParagraphFormat format) =>
        Needed(lines.Take((format.WidowControl ?? true) && lines.Count > 1 ? 2 : 1).ToList());

    /// <summary>The height lines need on a page: all of them, the last without what may hang past the bottom.</summary>
    static double Needed(List<Line> lines) => lines.Count == 0 ? 0 : lines.Sum(l => l.Height) - lines[^1].Overhang;

    /// <summary>What a kept-with-next paragraph needs of the following ones: their keep chain and the first lines of the last.</summary>
    double KeptNext(List<Block> blocks, int index, double width)
    {
        if (blocks[index] is not ParagraphBlock next || blocks[index - 1] is not ParagraphBlock before)
            return 0;
        var lines = layout.Layout(next, width);
        var gap = Math.Max(SpaceAfter(before, next), SpaceBefore(next));
        if (next.Format.KeepNext == true && index + 1 < blocks.Count && blocks[index + 1] is ParagraphBlock)
            return gap + lines.Sum(l => l.Height) + KeptNext(blocks, index + 1, width);
        return gap + FirstLines(lines, next.Format);
    }

    /// <summary>The space before a paragraph; contextual spacing drops it after a paragraph of the same style.</summary>
    double SpaceBefore(ParagraphBlock paragraph)
    {
        if (paragraph.Format.ContextualSpacing == true && previous != null && SameStyle(previous, paragraph))
            return 0;
        return paragraph.Format.Before ?? 0;
    }

    /// <summary>The space after a paragraph; contextual spacing drops it before a paragraph of the same style.</summary>
    double SpaceAfter(ParagraphBlock paragraph, ParagraphBlock? next)
    {
        if (paragraph.Format.ContextualSpacing == true && next != null && SameStyle(paragraph, next))
            return 0;
        return paragraph.Format.After ?? 0;
    }

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
