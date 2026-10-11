namespace FreeSlate.Docx.Layout;

/// <summary>A line placed on a page: its left edge (the column's) and its top, in points from the page's top left.</summary>
public sealed record PlacedLine(Line Line, double Left, double Top);

/// <summary>A page of the laid-out document.</summary>
public sealed class PageBox
{
    public int Number { get; init; }
    public required SectionProperties Section { get; init; }
    public List<PlacedLine> Lines { get; } = [];
    public double Width => Section.PageWidth;
    public double Height => Section.PageHeight;
}

/// <summary>
/// Breaks the body into pages (catalog: engine.pagination, section.page, section.columns, paragraph.pagination):
/// sections start on a new page, an even or odd page or continue on the same page; lines fill the columns of the
/// text area; paragraphs keep their spacing (the larger of the space after the one and before the next, measured
/// with docx/generated/text-basic, as ISO/IEC 29500 17.3.1.33 says), lose the space before at the top of a page they
/// were pushed onto, keep with the next paragraph, keep their lines together and avoid widows and orphans.
/// </summary>
public sealed class Paginator(DocumentContext context, ParagraphLayout layout)
{
    readonly List<PageBox> pages = [];

    // The current position: page, column, the top of the next line and whether the column is still empty.
    PageBox? page;
    SectionProperties section = new();
    int column;
    double y;
    bool columnEmpty = true;
    bool pushed;              // the column's first paragraph came over by a natural page or column break
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

        for (var s = 0; s < sections.Count; s++)
        {
            var (sectionBlocks, properties) = sections[s];
            StartSection(properties, s == 0);
            var paragraphs = sectionBlocks.OfType<ParagraphBlock>().ToList();
            for (var i = 0; i < paragraphs.Count; i++)
                Place(paragraphs, i);
        }
        if (pages.Count == 0)
            NewPage(lastSection);
        foreach (var p in pages)
            AlignVertically(p);
        return pages;
    }

    void StartSection(SectionProperties properties, bool first)
    {
        var start = first ? "nextPage" : properties.Start;
        section = properties;
        if (page == null || start != "continuous" && start != "nextColumn")
        {
            NewPage(properties);
            // Even and odd page sections leave a blank page where the parity does not fit.
            if (!first && (start == "evenPage" && page!.Number % 2 == 1 || start == "oddPage" && page!.Number % 2 == 0))
                NewPage(properties);
            pushed = false;
            return;
        }
        if (start == "nextColumn")
            NextColumn(natural: false);
    }

    void NewPage(SectionProperties properties)
    {
        page = new PageBox { Number = pages.Count + 1, Section = properties };
        pages.Add(page);
        column = 0;
        y = properties.Top;
        columnEmpty = true;
    }

    (double Left, double Width) Column(int index)
    {
        var s = section;
        var left = s.Left + s.Gutter;
        if (s.ColumnWidths is { Count: > 0 } widths)
        {
            for (var k = 0; k < index && k < widths.Count; k++)
                left += widths[k].Width + widths[k].Space;
            return (left, widths[Math.Min(index, widths.Count - 1)].Width);
        }
        var width = (s.TextWidth - s.ColumnSpace * (s.Columns - 1)) / s.Columns;
        return (left + index * (width + s.ColumnSpace), width);
    }

    double Bottom => section.PageHeight - section.Bottom;

    void NextColumn(bool natural)
    {
        if (column + 1 < section.Columns)
        {
            column++;
            y = section.Top;
        }
        else
            NewPage(section);
        columnEmpty = true;
        pushed = natural;
    }

    void Place(List<ParagraphBlock> paragraphs, int index)
    {
        var paragraph = paragraphs[index];
        var format = paragraph.Format;
        var (left, width) = Column(column);
        var lines = layout.Layout(paragraph, width);

        if (format.PageBreakBefore == true && !(columnEmpty && column == 0 && page!.Lines.Count == 0))
        {
            NewPage(section);
            pushed = false;
            (left, width) = Column(column);
        }

        // Space between this paragraph and the previous one.
        var before = SpaceBefore(paragraph);
        var gap = columnEmpty ? (pushed ? 0 : before) : Math.Max(pendingAfter, before);

        // Keep with next and keep lines together: move the paragraph to the next column when it (and the first line
        // of the next paragraph) does not fit here but would fit in an empty column.
        if (!columnEmpty)
        {
            var needed = gap + (format.KeepLines == true ? lines.Sum(l => l.Height) : FirstLines(lines, format));
            if (format.KeepNext == true && index + 1 < paragraphs.Count)
                needed = gap + lines.Sum(l => l.Height) + KeptNext(paragraphs, index + 1, width);
            // What does not fit even an empty column is broken anyway.
            if (y + needed > Bottom + 0.01 && needed - gap <= Bottom - section.Top + 0.01)
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
            if (y + line.Height > Bottom + 0.01 && !columnEmpty)
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
                        k -= back;
                        line = lines[k];
                    }
                }
                NextColumn(natural: true);
                (left, width) = Column(column);
                here = 0;
                hereAtTop = true;
            }
            page!.Lines.Add(new PlacedLine(line, left, y));
            here++;
            y += line.Height;
            columnEmpty = false;
            pushed = false;
            if (line.EndBreak is "page")
            {
                NewPage(section);
                pushed = false;
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
        pendingAfter = SpaceAfter(paragraph, index + 1 < paragraphs.Count ? paragraphs[index + 1] : null);
        previous = paragraph;
    }

    /// <summary>The height a paragraph needs at least: its first two lines with widow control, else its first.</summary>
    static double FirstLines(List<Line> lines, ParagraphFormat format) =>
        lines.Take((format.WidowControl ?? true) && lines.Count > 1 ? 2 : 1).Sum(l => l.Height);

    /// <summary>What a kept-with-next paragraph needs of the following ones: their keep chain and the first lines of the last.</summary>
    double KeptNext(List<ParagraphBlock> paragraphs, int index, double width)
    {
        var next = paragraphs[index];
        var lines = layout.Layout(next, width);
        var gap = Math.Max(SpaceAfter(paragraphs[index - 1], next), SpaceBefore(next));
        if (next.Format.KeepNext == true && index + 1 < paragraphs.Count && index < paragraphs.Count)
            return gap + lines.Sum(l => l.Height) + KeptNext(paragraphs, index + 1, width);
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

    /// <summary>Vertical alignment of the page's text (w:vAlign): centered, at the bottom or spread.</summary>
    static void AlignVertically(PageBox page)
    {
        var align = page.Section.VerticalAlign;
        if (align is not ("center" or "bottom") || page.Lines.Count == 0)
            return;
        var top = page.Section.Top;
        var bottom = page.Section.PageHeight - page.Section.Bottom;
        var used = page.Lines.Max(l => l.Top + l.Line.Height) - top;
        var shift = align == "center" ? (bottom - top - used) / 2 : bottom - top - used;
        for (var i = 0; i < page.Lines.Count; i++)
            page.Lines[i] = page.Lines[i] with { Top = page.Lines[i].Top + shift };
    }
}
