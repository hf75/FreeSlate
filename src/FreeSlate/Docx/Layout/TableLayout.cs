using FreeSlate.Drawing;

namespace FreeSlate.Docx.Layout;

/// <summary>A filled rectangle on a page: a border line or shading, in points from the page's top left.</summary>
public sealed record FillRect(double X, double Y, double Width, double Height, RgbColor Color);

/// <summary>A stroked line on a page (dotted and dashed borders, diagonal cell borders) with an SVG dash pattern.</summary>
public sealed record Stroke(double X1, double Y1, double X2, double Y2, double Width, RgbColor Color, string? Dash = null)
{
    public Stroke Offset(double dx, double dy) => this with { X1 = X1 + dx, Y1 = Y1 + dy, X2 = X2 + dx, Y2 = Y2 + dy };
}

/// <summary>A vertical border stripe through a row: its left edge, width, color and dash pattern.</summary>
public sealed record Stripe(double X, double Width, RgbColor Color, string? Dash = null);

/// <summary>
/// A floating drawing anchored in a cell (catalog: drawing.anchor) with the frames it is placed in: the top of its
/// paragraph and of its line, the cell's text area (positions relative to the column, the margin or the character), the
/// cell inside its lines (relative to the page and the page's margins across) and the cell's content (relative to the
/// page and the margins down); null frames are the page's, for drawings outside tables.
/// </summary>
/// <remarks>
/// Measured with docx/local/cell-anchor: Word places these frames in the cell whether layoutInCell is set or not.
/// </remarks>
public sealed record CellAnchor(AnchorInline Anchor, double ParagraphTop, double LineTop)
{
    public (double Left, double Width)? Text { get; init; }
    public (double Left, double Width)? Cell { get; init; }
    public (double Top, double Height)? Content { get; init; }

    /// <summary>Where the drawing is anchored in its line, for positions relative to the character.</summary>
    public double? CharacterX { get; init; }

    public CellAnchor Offset(double dx, double dy) => this with
    {
        ParagraphTop = ParagraphTop + dy,
        LineTop = LineTop + dy,
        CharacterX = CharacterX + dx,
        Text = Text is { } t ? (t.Left + dx, t.Width) : null,
        Cell = Cell is { } c ? (c.Left + dx, c.Width) : null,
        Content = Content is { } v ? (v.Top + dy, v.Height) : null,
    };
}

/// <summary>The content of one cell in a row: lines, nested tables' fills and strokes, relative to the row's top.</summary>
public sealed class CellPart
{
    public List<PlacedLine> Lines { get; } = [];
    public List<FillRect> Fills { get; } = [];
    public List<Stroke> Strokes { get; } = [];
    public List<CellAnchor> Anchors { get; } = [];

    /// <summary>Where the cell's content starts below the row's top (its top line and margin, without alignment).</summary>
    public double ContentTop { get; init; }

    /// <summary>The cell's bottom margin.</summary>
    public double BottomMargin { get; init; }

    /// <summary>The diagonal lines of the cell: its left and right edge and the lines (tl2br, tr2bl).</summary>
    public (double Left, double Right, BorderLine? Down, BorderLine? Up) Diagonals { get; init; }

    /// <summary>The cell's vertical alignment (w:vAlign) and how far it lowered the content.</summary>
    public string? Align { get; init; }
    public double Lowered { get; init; }

    /// <summary>The part moved down by <paramref name="dy"/> points.</summary>
    public CellPart Shifted(double dy)
    {
        var moved = new CellPart { ContentTop = ContentTop, BottomMargin = BottomMargin, Diagonals = Diagonals, Align = Align, Lowered = Lowered + dy };
        moved.Lines.AddRange(Lines.Select(l => l with { Top = l.Top + dy }));
        moved.Fills.AddRange(Fills.Select(f => f with { Y = f.Y + dy }));
        moved.Strokes.AddRange(Strokes.Select(s => s.Offset(0, dy)));
        moved.Anchors.AddRange(Anchors.Select(a => a.Offset(0, dy)));
        return moved;
    }
}

/// <summary>
/// A laid-out table row: its height and, relative to the row's top left (the table's origin), its cells' content,
/// the shading and vertical lines that run through it, the lines at its top and the lines below it where the table
/// ends or breaks after it.
/// </summary>
public sealed class RowBox
{
    public double Height { get; set; }
    public bool Header { get; init; }
    public bool CantSplit { get; init; }

    /// <summary>Whether the row has an exact height (w:hRule="exact").</summary>
    public bool Exact { get; init; }

    /// <summary>The row's at-least height without its top line (w:trHeight), 0 without one.</summary>
    public double Least { get; init; }

    /// <summary>The height of the lines at the row's top, above its content.</summary>
    public double TopWidth { get; init; }

    public List<CellPart> Cells { get; } = [];

    /// <summary>Cell shading: left edge, width and color, from the row's top through its height.</summary>
    public List<(double X, double Width, RgbColor Color)> Shading { get; } = [];
    public List<Stripe> Verticals { get; } = [];
    public List<FillRect> TopBorders { get; } = [];
    public List<Stroke> TopStrokes { get; } = [];

    /// <summary>The lines below the row where the table ends or breaks after it, drawn at the row's bottom.</summary>
    public List<FillRect> BottomBorders { get; } = [];
    public List<Stroke> BottomStrokes { get; } = [];
    public double BottomBorderWidth { get; set; }

    /// <summary>Lines and fills of rows laid out with cell spacing: each cell's own frame, through the row.</summary>
    public List<FillRect> Frames { get; } = [];

    /// <summary>All lines of the row's cells.</summary>
    public IEnumerable<PlacedLine> Lines => Cells.SelectMany(c => c.Lines);

    /// <summary>
    /// The row cut at <paramref name="available"/> points (catalog: table.rows): each cell keeps the lines that end
    /// above the cut, a paragraph's last line with the paragraph's space after, the rest continue at the top of the
    /// continuation, below its top line and margin, which is at least the row's at-least height again. Null where no
    /// cell has a line above the cut or none below it, and, unless <paramref name="force"/>d (a row taller than its
    /// page), for rows Word does not split (measured with docx/local/tblsplit-*, rowsplit-*, rowsplit2-*): those that
    /// may not split, header rows, rows of exact height, rows whose at-least height does not fit above the cut and
    /// rows with a cell whose first line does not (a picture line with space after it that fits alone moves on too).
    /// </summary>
    public (RowBox Head, RowBox Tail)? Split(double available, bool force = false)
    {
        if (!force && (CantSplit || Header || Exact || TopWidth + Least > available + 0.01)
            || Frames.Count > 0 || Cells.Any(c => c.Lines.Any(l => l.Angle != 0)))
            return null;
        var head = Copy(available);
        var tail = Copy(0);
        var any = false;
        var continues = false;
        var tailHeight = TopWidth;
        foreach (var cell in Cells)
        {
            var headPart = new CellPart { ContentTop = cell.ContentTop, BottomMargin = cell.BottomMargin, Diagonals = cell.Diagonals, Align = cell.Align, Lowered = cell.Lowered };
            var tailPart = new CellPart { ContentTop = cell.ContentTop, BottomMargin = cell.BottomMargin, Diagonals = cell.Diagonals, Align = cell.Align };
            // Where the lines would stand without the cell's vertical alignment, which each part applies again.
            var rest = cell.Lines.FirstOrDefault(l => l.Top - cell.Lowered + l.Line.Height + l.After > available + 0.01);
            if (!force && rest != null && ReferenceEquals(rest, cell.Lines[0]))
                return null;
            var offset = rest != null ? rest.Top - cell.ContentTop : 0;
            foreach (var line in cell.Lines)
            {
                if (rest == null || line.Top < rest.Top)
                {
                    headPart.Lines.Add(line);
                    any = true;
                }
                else
                {
                    tailPart.Lines.Add(line with { Top = line.Top - offset });
                    continues = true;
                }
            }
            foreach (var fill in cell.Fills)
            {
                if (rest == null || fill.Y < rest.Top)
                    headPart.Fills.Add(fill);
                else
                    tailPart.Fills.Add(fill with { Y = fill.Y - offset });
            }
            foreach (var stroke in cell.Strokes)
            {
                if (rest == null || stroke.Y1 < rest.Top)
                    headPart.Strokes.Add(stroke);
                else
                    tailPart.Strokes.Add(stroke.Offset(0, -offset));
            }
            foreach (var anchor in cell.Anchors)
            {
                if (rest == null || anchor.LineTop < rest.Top)
                    headPart.Anchors.Add(anchor);
                else
                    tailPart.Anchors.Add(anchor.Offset(0, -offset) with { Content = anchor.Content is { } v ? (cell.ContentTop, v.Height) : null });
            }
            if (tailPart.Lines.Count > 0)
                tailHeight = Math.Max(tailHeight, tailPart.Lines.Max(l => l.Top + l.Line.Height + l.After) + cell.BottomMargin);
            head.Cells.Add(headPart);
            tail.Cells.Add(tailPart);
        }
        if (!any || !continues)
            return null;
        tail.Height = Math.Max(tailHeight, TopWidth + Least);
        Realign(head.Cells, null);
        Realign(tail.Cells, tail.Height);
        return (head, tail);
    }

    /// <summary>
    /// Cells aligned to the middle or the bottom align again in a part of a split row: within the part's height, or
    /// for the part before the cut down to the bottom of its tallest content (measured with docx/local/rowsplit-28c).
    /// </summary>
    static void Realign(List<CellPart> parts, double? height)
    {
        double? Bottom(CellPart part) => part.Lines.Count == 0 ? null : part.Lines.Max(l => l.Top + l.Line.Height + l.After) - part.Lowered + part.BottomMargin;
        var bottoms = parts.Select(Bottom).ToList();
        if (height == null && bottoms.All(b => b == null))
            return;
        var room = height ?? bottoms.Max(b => b ?? 0);
        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i].Align is not ("center" or "bottom") || bottoms[i] is not { } bottom)
                continue;
            var free = Math.Max(0, room - bottom);
            var lowered = parts[i].Align == "center" ? free / 2 : free;
            if (Math.Abs(lowered - parts[i].Lowered) > 0.001)
                parts[i] = parts[i].Shifted(lowered - parts[i].Lowered);
        }
    }

    RowBox Copy(double height)
    {
        var copy = new RowBox
        {
            Height = height, Header = Header, CantSplit = CantSplit, Exact = Exact, Least = Least, TopWidth = TopWidth, BottomBorderWidth = BottomBorderWidth,
        };
        copy.Shading.AddRange(Shading);
        copy.Verticals.AddRange(Verticals);
        copy.TopBorders.AddRange(TopBorders);
        copy.TopStrokes.AddRange(TopStrokes);
        copy.BottomBorders.AddRange(BottomBorders);
        copy.BottomStrokes.AddRange(BottomStrokes);
        return copy;
    }
}

/// <summary>A laid-out table: its left edge from the column's, its width and its rows.</summary>
public sealed class TableBox
{
    public double Left { get; init; }
    public double Width { get; init; }
    public List<RowBox> Rows { get; } = [];

    /// <summary>
    /// The frame of a table with cell spacing (its own four borders), which the paginator draws around the table's
    /// rows on every page; its top is in the first row's height, its bottom below the last row.
    /// </summary>
    public (BorderLine? Top, BorderLine? Left, BorderLine? Bottom, BorderLine? Right) Frame { get; init; }
}

/// <summary>
/// Lays out tables the way Word 2013 and later do (catalog: engine.tableLayout, table.width, table.borders,
/// table.rows, table.cells), measured with docx/local/tbl-geom and tbl-features: the grid starts at the indent from
/// the column's left edge (older compatibility modes put the first cell's text there) and moves right by half the
/// left border, every vertical line centered on its grid line; text starts the larger of the margin and half the
/// line after it, below the whole top line and the top margin; a row is its top line and its content, at least or
/// exactly its height (at least without the line, exactly with it); a table's bottom line lies below its last row.
/// Double lines take three times their width (line, gap, line), triple five times. With cell spacing every cell has
/// its own frame, the cells twice the spacing apart and from the table's frame.
/// </summary>
public sealed class TableLayout(DocumentContext context, ParagraphLayout paragraphs)
{
    /// <summary>Whether contextual spacing works in cells (see <see cref="Content(List{Block}, double, PageFields?, string?, string?)"/>).</summary>
    bool? contextual;

    /// <summary>
    /// The left and right cell margin of tables without any table style (see <see cref="TableFormat.Styled"/>): 0.5 pt,
    /// measured with docx/local/tblnostyle-12, -14 and -15 for lines of 0 to 6 pt.
    /// </summary>
    const double DefaultMargin = 0.5;

    /// <summary>The paragraph layout the cells' text is laid out with.</summary>
    public ParagraphLayout Paragraphs => paragraphs;

    bool? modern;

    /// <summary>Whether the document lays tables out as Word 2013 does (compatibility mode 15 and later).</summary>
    bool Modern => modern ??= context.Settings.Child("compat")?.Elements(Wml.W + "compatSetting")
        .FirstOrDefault(e => e.Attr("name") == "compatibilityMode")?.Attr("val") is { } v && int.TryParse(v, out var mode) && mode >= 15;

    /// <summary>Stands for no paragraph where contextual spacing compares paragraph styles.</summary>
    public const string NoParagraph = "\0";

    /// <summary>The paragraph style a paragraph has, as contextual spacing compares them.</summary>
    string? StyleOf(ParagraphBlock paragraph) => context.Styles.ParagraphStyleOf(paragraph.Format.Style);

    /// <summary>The first paragraph of a table: its first cell's, in a nested table the nested table's.</summary>
    public static ParagraphBlock? FirstParagraph(TableModel table) =>
        table.Rows.FirstOrDefault()?.Cells.FirstOrDefault()?.Blocks.FirstOrDefault() switch
        {
            ParagraphBlock paragraph => paragraph,
            TableBlock nested => FirstParagraph(nested.Table),
            _ => null,
        };

    /// <summary>
    /// The styles of the paragraphs before and after every cell's content in the document's order, which contextual
    /// spacing compares the cell's first and last paragraphs with (measured with docx/local/c5-cells): a cell's first
    /// paragraph follows the last paragraph of the cell before it in its row, the first cell's the end of the row
    /// before (whose mark has the default paragraph style) or, in the first row, the paragraph before the table; a
    /// cell's last paragraph precedes the next cell's first, the last cell's the end of its row.
    /// </summary>
    Dictionary<CellModel, (string? Before, string? After)> Neighbors(List<RowModel> rows, string? before)
    {
        var result = new Dictionary<CellModel, (string?, string?)>(ReferenceEqualityComparer.Instance);
        var rowEnd = context.Styles.ParagraphStyleOf(null);
        string? Last(CellModel cell) => cell.Blocks.LastOrDefault() switch
        {
            ParagraphBlock p => StyleOf(p),
            TableBlock t => t.Table.Rows.Count > 0 ? rowEnd : NoParagraph,
            _ => NoParagraph,
        };
        string? First(CellModel cell) => cell.Blocks.FirstOrDefault() switch
        {
            ParagraphBlock p => StyleOf(p),
            TableBlock t => FirstParagraph(t.Table) is { } p ? StyleOf(p) : NoParagraph,
            _ => NoParagraph,
        };
        for (var r = 0; r < rows.Count; r++)
        {
            var cells = rows[r].Cells;
            for (var c = 0; c < cells.Count; c++)
                result[cells[c]] = (c > 0 ? Last(cells[c - 1]) : r > 0 ? rowEnd : before, c + 1 < cells.Count ? First(cells[c + 1]) : rowEnd);
        }
        return result;
    }

    /// <param name="before">The style of the paragraph before the table (<see cref="NoParagraph"/> for none), which contextual spacing compares its first paragraph with.</param>
    public TableBox Layout(TableModel table, double available, PageFields? fields = null, string? before = NoParagraph)
    {
        var box = Laid(table, available, fields, before);
        // The markup view marks every line of a table whose formatting changed; the balloon hangs from its first line.
        if (table.Change is { } change)
        {
            var lines = box.Rows.SelectMany(r => r.Cells).SelectMany(c => c.Lines).ToList();
            foreach (var line in lines)
                line.Line.Changed = true;
            if (lines.Count > 0)
                lines[0].Line.BlockChange = change;
        }
        return box;
    }

    TableBox Laid(TableModel table, double available, PageFields? fields, string? before)
    {
        var format = table.Format;
        var rows = table.Rows.Where(r => !r.Format.Hidden).ToList();
        var neighbors = Neighbors(rows, before);
        var columns = Math.Max(table.Grid.Count, rows.Count == 0 ? 0 : rows.Max(r => r.Cells.Count == 0 ? 0 : r.Cells.Max(c => c.Column + c.Format.GridSpan) + r.Format.GridAfter));
        columns = Math.Max(1, columns);

        // Every cell's place in the grid and the rows a vertical merge spans.
        var at = new CellModel?[rows.Count, columns];
        for (var r = 0; r < rows.Count; r++)
        {
            foreach (var cell in rows[r].Cells)
            {
                for (var k = cell.Column; k < Math.Min(columns, cell.Column + cell.Format.GridSpan); k++)
                    at[r, k] = cell;
            }
        }
        var origin = new Dictionary<CellModel, (int Row, CellModel Cell)>();
        var span = new Dictionary<CellModel, int>();
        for (var r = 1; r < rows.Count; r++)
        {
            foreach (var cell in rows[r].Cells)
            {
                if (cell.MergedAbove && cell.Column < columns && at[r - 1, cell.Column] is { } above)
                {
                    (int Row, CellModel Cell) start = origin.TryGetValue(above, out var o) ? o : (r - 1, above);
                    origin[cell] = start;
                    span[start.Cell] = r - start.Row + 1;
                }
            }
        }
        CellModel? Owner(CellModel? cell) => cell != null && origin.TryGetValue(cell, out var o) ? o.Cell : cell;

        // Cell spacing, even of nothing, keeps the table's frame and the cells' borders apart (measured with
        // docx/local/altchunk-tables: an HTML table with cellspacing="0" has its 1.5 pt frame and 0.75 pt cell borders).
        var spacingSet = rows.Count > 0 ? rows[0].Format.CellSpacing ?? format.CellSpacing : format.CellSpacing;
        var spacing = spacingSet ?? 0;
        if (spacingSet != null)
            return Spaced(table, rows, columns, at, available, spacing, fields, neighbors);

        // Borders: for each row boundary (0 … rows) and grid column the horizontal line, for each row and grid line
        // the vertical one; a cell's own border wins over the table's, two cells' borders are resolved by weight.
        var horizontal = new BorderLine?[rows.Count + 1, columns];
        var vertical = new BorderLine?[rows.Count, columns + 1];
        for (var r = 0; r <= rows.Count; r++)
        {
            for (var k = 0; k < columns; k++)
            {
                var above = r > 0 ? Owner(at[r - 1, k]) : null;
                var below = r < rows.Count ? Owner(at[r, k]) : null;
                // Inside a vertical merge there is no line, nor where no cell is (rows that skip grid columns).
                if (above == below)
                    continue;
                var tableLine = r == 0 || above == null ? TableBorder(rows[Math.Min(r, rows.Count - 1)], format, b => b.Top)
                    : r == rows.Count || below == null ? TableBorder(rows[r - 1], format, b => b.Bottom)
                    : TableBorder(rows[r], format, b => b.InsideH);
                horizontal[r, k] = Edge(above?.CellBorders.Bottom, below?.CellBorders.Top, tableLine, above != null && below != null);
            }
        }
        for (var r = 0; r < rows.Count; r++)
        {
            for (var k = 0; k <= columns; k++)
            {
                var left = k > 0 ? at[r, k - 1] : null;
                var right = k < columns ? at[r, k] : null;
                if (left == right)
                    continue;
                var tableLine = k == 0 || left == null ? TableBorder(rows[r], format, b => b.Left)
                    : k == columns || right == null ? TableBorder(rows[r], format, b => b.Right)
                    : TableBorder(rows[r], format, b => b.InsideV);
                vertical[r, k] = Edge(Owner(left)?.CellBorders.Right, Owner(right)?.CellBorders.Left, tableLine, left != null && right != null);
            }
        }

        // Column widths: the saved grid for fixed tables, Word's autofit for the others.
        var allowance = rows.Count > 0 ? (Width(vertical[0, 0]) + Width(vertical[0, columns])) / 2 : 0;
        var grid = Grid(table, rows, columns, available - (format.Justification is "center" or "right" or "end" ? 0 : format.Indent ?? 0), allowance, fields,
            lines: vertical);
        var x = new double[grid.Length + 1];
        for (var k = 0; k < grid.Length; k++)
            x[k + 1] = x[k] + grid[k];

        // The grid moves right by half the table's left border; every vertical line is centered on its grid line, so
        // the left border's outer edge lies at the table's left edge (measured with docx/local/tbl-geom: a 3 pt
        // inside line of a table with 0.5 pt outside lines is centered 0.25 pt right of its grid line).
        var shift = rows.Count > 0 ? Width(vertical[0, 0]) / 2 : 0;
        var firstMargin = rows.Count > 0 && rows[0].Cells.Count > 0 ? Margins(rows[0], rows[0].Cells[0], format).Left : format.Styled ? 0 : DefaultMargin;
        var rightBorder = rows.Count > 0 ? Width(vertical[0, columns]) : 0;
        var tableWidth = x[^1] + shift + rightBorder / 2;
        var indent = format.Indent ?? 0;
        // Right-to-left tables (w:bidiVisual) start at the right: their first column is the rightmost, their indent
        // and alignment count from the right (measured with docx/local/tbl-features).
        var rtl = format.BidiVisual;
        var justification = format.Justification;
        if (rtl)
            justification = justification switch { "right" or "end" => "left", "center" => "center", _ => "right" };
        double origin0 = justification switch
        {
            "center" => (available - tableWidth) / 2,
            "right" or "end" => available - tableWidth - (rtl ? indent : 0),
            // Older modes put the first cell's text at the indent; a table without any indent (none in its
            // properties or style) has its left line centered on the column's edge there (measured with
            // docx/local/c2-tblpos in modes 11 to 14).
            _ => indent - (Modern ? 0 : format.Indent == null ? shift : shift + Math.Max(firstMargin, shift)),
        };
        var box = new TableBox { Left = origin0, Width = tableWidth };
        double X(double position) => origin0 + (rtl ? tableWidth - position : position);
        (double From, double Width) Range(double from, double to) => rtl ? (origin0 + tableWidth - to, to - from) : (origin0 + from, to - from);

        // Cell content and row heights.
        var topWidths = new double[rows.Count + 1];
        for (var r = 0; r <= rows.Count; r++)
        {
            for (var k = 0; k < columns; k++)
                topWidths[r] = Math.Max(topWidths[r], Width(horizontal[r, k]));
        }
        var laid = new Dictionary<CellModel, CellContent>();
        var contentHeights = new double[rows.Count];
        for (var r = 0; r < rows.Count; r++)
        {
            foreach (var cell in rows[r].Cells)
            {
                if (origin.ContainsKey(cell))
                    continue;
                var c0 = Math.Min(cell.Column, columns);
                var c1 = Math.Min(cell.Column + cell.Format.GridSpan, columns);
                var margins = Margins(rows[r], cell, format);
                var leftLine = Width(vertical[r, c0]);
                var rightLine = Width(vertical[r, c1]);
                var textLeft = x[c0] + shift + Math.Max(margins.Left, leftLine / 2);
                var textRight = x[c1] + shift - Math.Max(margins.Right, rightLine / 2);
                var content = Content(cell, Math.Max(1, textRight - textLeft), fields, neighbors.GetValueOrDefault(cell, (NoParagraph, NoParagraph)));
                laid[cell] = content with { Left = textLeft, Width = Math.Max(1, textRight - textLeft) };
                if (!span.ContainsKey(cell))
                    contentHeights[r] = Math.Max(contentHeights[r], margins.Top + content.Height + margins.Bottom);
            }
        }
        var heights = new double[rows.Count];
        for (var r = 0; r < rows.Count; r++)
        {
            var f = rows[r].Format;
            heights[r] = contentHeights[r];
            if (f.Height is { } h && h > 0)
                heights[r] = f.HeightRule == "exact" ? Math.Max(0, h - topWidths[r]) : Math.Max(heights[r], h);
        }
        // A vertically merged cell taller than its rows makes its last row higher.
        foreach (var (cell, rowsSpanned) in span)
        {
            var r0 = rows.FindIndex(row => row.Cells.Contains(cell));
            if (r0 < 0)
                continue;
            var margins = Margins(rows[r0], cell, format);
            var needed = margins.Top + laid[cell].Height + margins.Bottom;
            var have = 0.0;
            for (var r = r0; r < r0 + rowsSpanned && r < rows.Count; r++)
                have += heights[r] + (r > r0 ? topWidths[r] : 0);
            var last = Math.Min(rows.Count - 1, r0 + rowsSpanned - 1);
            if (needed > have && rows[last].Format.HeightRule != "exact")
                heights[last] += needed - have;
        }

        // Row boxes.
        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            var specified = row.Format.Height is { } rh && rh > 0 ? rh : 0;
            var rowBox = new RowBox
            {
                Header = row.Format.Header, CantSplit = row.Format.CantSplit, TopWidth = topWidths[r], Height = topWidths[r] + heights[r],
                Exact = specified > 0 && row.Format.HeightRule == "exact", Least = row.Format.HeightRule == "exact" ? 0 : specified,
            };
            // Shading of every cell the row passes through, also cells merged from above.
            for (var k = 0; k < columns; k++)
            {
                if (at[r, k] is not { } here || k > 0 && at[r, k - 1] == here)
                    continue;
                var owner = Owner(here)!;
                if ((Shading.Color(owner.Format.Shading) ?? Shading.Color(row.ShadingException) ?? Shading.Color(format.Shading)) is { } fill)
                {
                    var c1 = Math.Min(columns, here.Column + here.Format.GridSpan);
                    var (from, width) = Range(x[k] + shift, x[c1] + shift);
                    rowBox.Shading.Add((from, width, fill));
                }
            }
            foreach (var cell in row.Cells)
            {
                if (origin.ContainsKey(cell))
                    continue;
                var c0 = Math.Min(cell.Column, columns);
                var c1 = Math.Min(cell.Column + cell.Format.GridSpan, columns);
                var rowsSpanned = span.GetValueOrDefault(cell, 1);
                var total = 0.0;
                for (var k = r; k < Math.Min(rows.Count, r + rowsSpanned); k++)
                    total += heights[k] + (k > r ? topWidths[k] : 0);
                var content = laid[cell];
                var margins = Margins(row, cell, format);
                var free = total - margins.Top - content.Height - margins.Bottom;
                var lowered = cell.Format.VerticalAlign switch
                {
                    "center" => Math.Max(0, free) / 2,
                    "bottom" => Math.Max(0, free),
                    _ => 0,
                };
                var (textFrom, _) = Range(content.Left, content.Left + content.Width);
                var top = topWidths[r] + margins.Top;
                var (cellFrom, cellWidth) = Range(x[c0] + shift, x[c1] + shift);
                var part = new CellPart
                {
                    ContentTop = top,
                    BottomMargin = margins.Bottom,
                    Diagonals = (cellFrom, cellFrom + cellWidth, cell.CellBorders.TopLeftToBottomRight, cell.CellBorders.TopRightToBottomLeft),
                    Align = Rotated(cell) ? null : cell.Format.VerticalAlign,
                    Lowered = Rotated(cell) ? 0 : lowered,
                };
                if (Rotated(cell))
                {
                    // tbRl turns clockwise about the content's top right corner, btLr counterclockwise about its bottom
                    // left (measured with docx/local/tbl-features: lines start at the cell's top and at its bottom);
                    // tbRlV and tbLrV run down like tbRl, tbLrV's lines from the left (measured with docx/local/vertical5).
                    var down = cell.Format.TextDirection is "tbRl" or "tbRlV" or "tbLrV";
                    var fromLeft = cell.Format.TextDirection == "tbLrV";
                    var pivotX = down ? textFrom + content.Width : textFrom;
                    var pivotY = down ? top : topWidths[r] + total - margins.Bottom;
                    foreach (var line in content.Lines)
                    {
                        var lineTop = fromLeft ? content.Width - line.Top - line.Line.Height : line.Top;
                        part.Lines.Add(line with { Line = fromLeft ? line.Line.Flipped() : line.Line, Left = pivotX + line.Left, Top = pivotY + lineTop, Angle = down ? 90 : -90, PivotX = -line.Left, PivotY = -lineTop });
                    }
                }
                else
                {
                    foreach (var line in content.Lines)
                        part.Lines.Add(line with { Left = textFrom + line.Left, Top = top + lowered + line.Top });
                }
                foreach (var fill in content.Fills)
                    part.Fills.Add(fill with { X = textFrom + fill.X, Y = top + lowered + fill.Y });
                foreach (var stroke in content.Strokes)
                    part.Strokes.Add(stroke.Offset(textFrom, top + lowered));
                if (!Rotated(cell))
                {
                    // Drawings in the cell: across from its text area or from the inner edge of its left line, down from
                    // its content's top (measured with docx/local/cell-anchor).
                    var inner = vertical[r, c0] is { Visible: true } edge ? edge.Total / 2 : 0;
                    foreach (var anchor in content.Anchors)
                    {
                        var moved = anchor.Offset(textFrom, top + lowered);
                        part.Anchors.Add(moved with
                        {
                            Text = moved.Text ?? (textFrom, content.Width),
                            Cell = moved.Cell ?? (cellFrom + inner, cellWidth - inner),
                            Content = moved.Content ?? (top, Math.Max(0, total - top - margins.Bottom)),
                        });
                    }
                }
                rowBox.Cells.Add(part);
            }
            // Lines at the row's top, from the outer edge of the vertical line at their start to that at their end.
            for (var k = 0; k < columns; k++)
            {
                if (horizontal[r, k] is { Visible: true } line)
                {
                    var (from, to) = Span(k, r);
                    var (left, width) = Range(from, to);
                    Horizontal(line, left, width, 0, rowBox.TopBorders, rowBox.TopStrokes);
                }
                if (horizontal[r + 1, k] is { Visible: true } below)
                {
                    var (from, to) = Span(k, r);
                    var (left, width) = Range(from, to);
                    Horizontal(below, left, width, 0, rowBox.BottomBorders, rowBox.BottomStrokes);
                    rowBox.BottomBorderWidth = Math.Max(rowBox.BottomBorderWidth, below.Total);
                }
            }
            // Vertical lines through the row, centered on their grid lines.
            for (var k = 0; k <= columns; k++)
            {
                if (vertical[r, k] is { Visible: true } line)
                {
                    var center = X(x[k] + shift);
                    foreach (var (offset, thickness) in Stripes(line))
                        rowBox.Verticals.Add(new Stripe(center - line.Total / 2 + offset, thickness, line.Color, Dash(line)));
                }
            }
            box.Rows.Add(rowBox);
        }
        return box;

        // A horizontal line's extent over grid column k: over the vertical lines at its ends.
        (double From, double To) Span(int k, int r)
        {
            var from = x[k] + shift - Width(vertical[r, k]) / 2;
            var to = x[k + 1] + shift + Width(vertical[r, k + 1]) / 2;
            return (from, to);
        }
    }

    /// <summary>
    /// A table with cell spacing (w:tblCellSpacing, measured with docx/local/tbl-features): the table's frame, twice
    /// the spacing, then the cells, each with its own borders, twice the spacing apart; text starts after a cell's
    /// whole border and its margin.
    /// </summary>
    TableBox Spaced(TableModel table, List<RowModel> rows, int columns, CellModel?[,] at, double available, double spacing, PageFields? fields,
        Dictionary<CellModel, (string? Before, string? After)> neighbors)
    {
        var format = table.Format;
        var gap = 2 * spacing;
        BorderLine? Visible(BorderLine? b) => b is { Visible: true } ? b : null;
        var outer = (Top: Visible(format.Borders.Top), Left: Visible(format.Borders.Left), Bottom: Visible(format.Borders.Bottom), Right: Visible(format.Borders.Right));
        // A table of a width holds its gaps and half its frame within it, the cells their own borders (measured with
        // docx/local/altchunk-tables: a 300 px HTML table is 225.75 pt wide over its frame).
        var extras = gap * (columns + 1) + (Width(outer.Left) + Width(outer.Right)) / 2;
        var sized = format.Width is { Type: "dxa" or "pct" } tw && tw.Value > 0;
        // Autofit columns of cells without preferred widths hold the cells' own borders too, which every spaced cell
        // draws on both sides (HTML tables Word imports, docx/local/altchunk, altchunk-tables): a cell's own, else the
        // table's inside line or side; they take their room from what the columns may have, so a table its text fills
        // is as wide as the page (docx/external/poi/bib-chernigovka.netdo.ru_download_docs_17459).
        double[]? edges = null;
        if (!sized && format.Layout != "fixed" && rows.SelectMany(r => r.Cells).All(c => c.Format.Width is not { Type: "dxa" or "pct" } cw || cw.Value <= 0))
        {
            var inside = Visible(format.Borders.InsideV);
            edges = new double[columns];
            for (var k = 0; k < columns; k++)
            {
                edges[k] = Width(k == 0 ? outer.Left : inside) + Width(k == columns - 1 ? outer.Right : inside);
                foreach (var cell in rows.SelectMany(r => r.Cells).Where(c => c.Column == k && c.Format.GridSpan <= 1))
                    edges[k] = Math.Max(edges[k], Width(Visible(cell.CellBorders.Left) ?? (k == 0 ? outer.Left : inside))
                                                  + Width(Visible(cell.CellBorders.Right) ?? (k == columns - 1 ? outer.Right : inside)));
            }
        }
        var grid = Grid(table, rows, columns, available - extras - (edges?.Sum() ?? 0), sized ? (Width(outer.Left) + Width(outer.Right)) / 2 : 0, fields,
            gaps: sized ? gap * (columns + 1) : 0, widthBase: available);
        if (edges != null)
        {
            for (var k = 0; k < grid.Length && k < edges.Length; k++)
                grid[k] += edges[k];
        }
        var x = new double[grid.Length + 1];
        for (var k = 0; k < grid.Length; k++)
            x[k + 1] = x[k] + grid[k];
        var left0 = Width(outer.Left) / 2 + gap;
        double CellX(int k) => left0 + x[k] + k * gap;
        var tableWidth = CellX(columns) - gap + gap + Width(outer.Right) / 2;
        var origin0 = format.Justification switch
        {
            "center" => (available - tableWidth) / 2,
            "right" or "end" => available - tableWidth,
            _ => format.Indent ?? 0,
        };
        var frameTop = Width(outer.Top) + gap;
        var box = new TableBox { Left = origin0, Width = tableWidth, Frame = outer };
        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            // Rows split where they may, as other tables' rows, unless their cells' frames (borders, shading) would
            // have to (docx/external/poi/drawing: a borderless table with cell spacing 0 holding a whole column).
            var specified = row.Format.Height is { } rh && rh > 0 ? rh : 0;
            var rowBox = new RowBox
            {
                Header = row.Format.Header, CantSplit = row.Format.CantSplit,
                Exact = specified > 0 && row.Format.HeightRule == "exact", Least = row.Format.HeightRule == "exact" ? 0 : specified,
            };
            var height = 0.0;
            var parts = new List<(CellModel Cell, CellContent Content, double Left, double Right, BorderSet Borders, (double Top, double Left, double Bottom, double Right) Margins)>();
            foreach (var cell in row.Cells)
            {
                var c0 = Math.Min(cell.Column, columns);
                var c1 = Math.Min(cell.Column + cell.Format.GridSpan, columns);
                var left = CellX(c0);
                var right = CellX(c1) - gap;
                // A cell's own borders, else the table's inside lines between cells and its sides at the edges.
                var borders = new BorderSet
                {
                    Top = cell.CellBorders.Top ?? (r == 0 ? format.Borders.Top : format.Borders.InsideH) ?? format.Borders.InsideH,
                    Bottom = cell.CellBorders.Bottom ?? (r == rows.Count - 1 ? format.Borders.Bottom : format.Borders.InsideH) ?? format.Borders.InsideH,
                    Left = cell.CellBorders.Left ?? (c0 == 0 ? format.Borders.Left : format.Borders.InsideV) ?? format.Borders.InsideV,
                    Right = cell.CellBorders.Right ?? (c1 == columns ? format.Borders.Right : format.Borders.InsideV) ?? format.Borders.InsideV,
                };
                var margins = Margins(row, cell, format);
                var textLeft = left + Width(borders.Left) + margins.Left;
                var textRight = right - Width(borders.Right) - margins.Right;
                var content = Content(cell, Math.Max(1, textRight - textLeft), fields, neighbors.GetValueOrDefault(cell, (NoParagraph, NoParagraph)));
                parts.Add((cell, content with { Left = textLeft, Width = Math.Max(1, textRight - textLeft) }, left, right, borders, margins));
                height = Math.Max(height, Width(borders.Top) + margins.Top + content.Height + margins.Bottom + Width(borders.Bottom));
            }
            if (row.Format.Height is { } h && h > 0)
                height = row.Format.HeightRule == "exact" ? h : Math.Max(height, h);
            rowBox.Height = (r == 0 ? frameTop : 0) + height + gap;
            var top0 = r == 0 ? frameTop : 0;
            foreach (var (cell, content, left, right, borders, margins) in parts)
            {
                if (Shading.Color(cell.Format.Shading) is { } fill)
                    rowBox.Frames.Add(new FillRect(origin0 + left, top0, right - left, height, fill));
                foreach (var (line, side, rect) in new[]
                {
                    (borders.Top, "top", new FillRect(origin0 + left, top0, right - left, Width(borders.Top), RgbColor.Black)),
                    (borders.Bottom, "bottom", new FillRect(origin0 + left, top0 + height - Width(borders.Bottom), right - left, Width(borders.Bottom), RgbColor.Black)),
                    (borders.Left, "left", new FillRect(origin0 + left, top0, Width(borders.Left), height, RgbColor.Black)),
                    (borders.Right, "right", new FillRect(origin0 + right - Width(borders.Right), top0, Width(borders.Right), height, RgbColor.Black)),
                })
                {
                    if (line is { Visible: true })
                        rowBox.Frames.Add(rect with { Color = line.ColorOf(side, cell: true) });
                }
                var part = new CellPart { ContentTop = top0 + Width(borders.Top) + margins.Top, BottomMargin = margins.Bottom };
                // Vertical alignment in the cell, as in tables without spacing (docx/external/poi/
                // bib-chernigovka.netdo.ru_download_docs_17459: dates centered beside two lines of text).
                var free = height - Width(borders.Top) - margins.Top - content.Height - margins.Bottom - Width(borders.Bottom);
                var lowered = cell.Format.VerticalAlign switch
                {
                    "center" => Math.Max(0, free) / 2,
                    "bottom" => Math.Max(0, free),
                    _ => 0,
                };
                var contentTop = part.ContentTop + lowered;
                foreach (var line in content.Lines)
                    part.Lines.Add(line with { Left = origin0 + content.Left + line.Left, Top = contentTop + line.Top });
                foreach (var nested in content.Fills)
                    part.Fills.Add(nested with { X = origin0 + content.Left + nested.X, Y = contentTop + nested.Y });
                foreach (var stroke in content.Strokes)
                    part.Strokes.Add(stroke.Offset(origin0 + content.Left, contentTop));
                foreach (var anchor in content.Anchors)
                {
                    var moved = anchor.Offset(origin0 + content.Left, contentTop);
                    part.Anchors.Add(moved with
                    {
                        Text = moved.Text ?? (origin0 + content.Left, content.Width),
                        Cell = moved.Cell ?? (origin0 + left + Width(borders.Left), right - left - Width(borders.Left) - Width(borders.Right)),
                        Content = moved.Content ?? (part.ContentTop, Math.Max(0, height - part.ContentTop + top0 - margins.Bottom - Width(borders.Bottom))),
                    });
                }
                rowBox.Cells.Add(part);
            }
            box.Rows.Add(rowBox);
        }
        // The frame's bottom below the last row.
        if (box.Rows.Count > 0)
            box.Rows[^1].BottomBorderWidth = Width(outer.Bottom);
        return box;
    }

    /// <summary>The space a border takes across: all its strokes and gaps.</summary>
    static double Width(BorderLine? line) => line is { Visible: true } ? line.Total : 0;

    /// <summary>
    /// A border's strokes across its total width: one for single and thick lines, two for double (line, gap, line),
    /// three for triple; thin and thick lines with 0.75 pt for thin lines and small gaps (measured with
    /// docx/local/tbl-features).
    /// </summary>
    internal static IEnumerable<(double Offset, double Width)> Stripes(BorderLine line)
    {
        var w = line.Width;
        switch (line.Style)
        {
            case "double":
                yield return (0, w);
                yield return (2 * w, w);
                break;
            case "triple":
                yield return (0, w);
                yield return (2 * w, w);
                yield return (4 * w, w);
                break;
            // Top to bottom and left to right on every side, as Word draws them (measured with docx/local/
            // tblcompound-24, -12, as page borders): thin lines and small gaps 0.75 pt, medium ones half the width,
            // large gaps the width with 1.5 pt lines beside them.
            case "thinThickSmallGap":
                yield return (0, w);
                yield return (w + 0.75, 0.75);
                break;
            case "thickThinSmallGap":
                yield return (0, 0.75);
                yield return (1.5, w);
                break;
            case "thinThickMediumGap":
                yield return (0, w);
                yield return (1.5 * w, w / 2);
                break;
            case "thickThinMediumGap":
                yield return (0, w / 2);
                yield return (w, w);
                break;
            case "thinThickLargeGap":
                yield return (0, 1.5);
                yield return (1.5 + w, 0.75);
                break;
            case "thickThinLargeGap":
                yield return (0, 0.75);
                yield return (0.75 + w, 1.5);
                break;
            case "thinThickThinSmallGap":
                yield return (0, 0.75);
                yield return (1.5, w);
                yield return (w + 2.25, 0.75);
                break;
            case "thinThickThinMediumGap":
                yield return (0, w / 2);
                yield return (w, w);
                yield return (2.5 * w, w / 2);
                break;
            case "thinThickThinLargeGap":
                yield return (0, 0.75);
                yield return (0.75 + w, 1.5);
                yield return (2.25 + 2 * w, 0.75);
                break;
            default:
                yield return (0, w);
                break;
        }
    }

    /// <summary>The SVG dash pattern of dotted and dashed borders, or null for solid ones.</summary>
    static string? Dash(BorderLine line)
    {
        var w = Math.Max(0.25, line.Width);
        return line.Style switch
        {
            "dotted" => $"{Units.Css(w)} {Units.Css(w)}",
            "dashed" or "dashSmallGap" => $"{Units.Css(3 * w)} {Units.Css(line.Style == "dashed" ? 3 * w : w)}",
            "dotDash" or "dashDotStroked" => $"{Units.Css(3 * w)} {Units.Css(w)} {Units.Css(w)} {Units.Css(w)}",
            "dotDotDash" => $"{Units.Css(3 * w)} {Units.Css(w)} {Units.Css(w)} {Units.Css(w)} {Units.Css(w)} {Units.Css(w)}",
            _ => null,
        };
    }

    /// <summary>A horizontal border as rectangles (solid lines) or strokes (dotted and dashed lines).</summary>
    static void Horizontal(BorderLine line, double left, double width, double y, List<FillRect> rects, List<Stroke> strokes)
    {
        foreach (var (offset, thickness) in Stripes(line))
        {
            if (Dash(line) is { } dash)
                strokes.Add(new Stroke(left, y + offset + thickness / 2, left + width, y + offset + thickness / 2, thickness, line.Color, dash));
            else
                rects.Add(new FillRect(left, y + offset, width, thickness, line.Color));
        }
    }

    /// <summary>
    /// The line where two cells meet (measured with docx/local/nilborder-* and rowborder-k, -l): each side brings its
    /// cell's own border, else the table's, and nothing where it is nil or none, so the other side's line still shows
    /// (a row of nil sides below a bottom line keeps it, and its room); two lines are resolved by weight, style and
    /// color (MS-OI29500 17.4.66), a table's thick inside line over a cell's own thin one too. At the table's edge,
    /// where one cell meets none, its own border wins over the table's.
    /// </summary>
    static BorderLine? Edge(BorderLine? a, BorderLine? b, BorderLine? table, bool inside)
    {
        if (!inside)
            return a != null || b != null ? BorderLine.Resolve(a, b) : table;
        static BorderLine? Side(BorderLine? own, BorderLine? table) => (own ?? table) is { Visible: true } line ? line : null;
        return BorderLine.Resolve(Side(a, table), Side(b, table));
    }

    /// <summary>A table border of a row: the row's exceptions over the table's.</summary>
    static BorderLine? TableBorder(RowModel row, TableFormat format, Func<BorderSet, BorderLine?> side) =>
        (row.BorderExceptions != null ? side(row.BorderExceptions) : null) ?? side(format.Borders);

    /// <summary>A cell's margins: its own over the row's exceptions over the table's.</summary>
    static (double Top, double Left, double Bottom, double Right) Margins(RowModel row, CellModel cell, TableFormat format)
    {
        var m = format.Margins.Overlay(row.MarginExceptions).Overlay(cell.Format.Margins);
        var side = format.Styled ? 0 : DefaultMargin;
        return (m.Top ?? 0, m.Left ?? side, m.Bottom ?? 0, m.Right ?? side);
    }

    /// <param name="allowance">What the outer vertical lines take of the available width.</param>
    /// <param name="gaps">What cell spacing takes of a table's own width (twice the spacing beside every cell).</param>
    /// <param name="widthBase">What a percentage width is of, where not the available width.</param>
    /// <param name="lines">The vertical lines of the rows (row, grid line), whose halves count where they are wider
    /// than the cells' margins beside them; none for tables with cell spacing.</param>
    double[] Grid(TableModel table, List<RowModel> rows, int columns, double available, double allowance, PageFields? fields,
        double gaps = 0, double? widthBase = null, BorderLine?[,]? lines = null)
    {
        var grid = new double[columns];
        for (var k = 0; k < columns; k++)
            grid[k] = k < table.Grid.Count ? table.Grid[k] : 0;
        if (table.Format.Layout != "fixed" && columns > 0 && rows.Count > 0)
            return Autofit(table, rows, columns, available, allowance, fields, Target(table, rows, widthBase ?? available, allowance, gaps), lines);
        if (grid.All(w => w > 0))
            return Scaled(grid);
        // From the first row's cell widths.
        foreach (var row in table.Rows)
        {
            foreach (var cell in row.Cells)
            {
                if (cell.Format.Width?.Resolve(available) is { } w && w > 0 && cell.Column < columns && grid[cell.Column] <= 0)
                {
                    for (var k = cell.Column; k < Math.Min(columns, cell.Column + cell.Format.GridSpan); k++)
                        grid[k] = w / cell.Format.GridSpan;
                }
            }
        }
        var missing = grid.Count(w => w <= 0);
        if (missing > 0)
        {
            var share = Math.Max(10, (available - grid.Where(w => w > 0).Sum()) / missing);
            for (var k = 0; k < columns; k++)
                if (grid[k] <= 0) grid[k] = share;
        }
        return Scaled(grid);

        // A fixed table with a width of its own stretches or shrinks its grid to it in proportion, whatever its cells
        // say (measured with docx/local/tblwidth-15 and -14: percentages and points, wider and narrower than the grid).
        double[] Scaled(double[] widths)
        {
            var sum = widths.Sum();
            if (Target(table, rows, widthBase ?? available, allowance, gaps) is not { } target || sum <= 0)
                return widths;
            return widths.Select(w => w * target / sum).ToArray();
        }
    }

    /// <summary>
    /// The width a table asks for (w:tblW in points or percent of the available width) for its grid, or null. A width
    /// in points lies between the outer lines' middles, also with cell spacing (whose gaps it holds); a percentage
    /// between their outer edges, before Word 2013 (compatibility mode below 15) between the middles and wider by the
    /// first cell's and the last cell's margins (measured with docx/local/tblwidth-15, -14, tblwidth2-15, -14 and
    /// compat14-tables).
    /// </summary>
    double? Target(TableModel table, List<RowModel> rows, double available, double allowance, double gaps)
    {
        var format = table.Format;
        if (format.Width is not { Type: "dxa" or "pct" } w || w.Resolve(available) is not { } tw || tw <= 0)
            return null;
        var target = tw - gaps;
        if (w.Type != "pct")
            return target;
        if (Modern)
            return target - allowance;
        if (rows.Count > 0 && rows[0].Cells.Count > 0)
        {
            var (first, last) = (rows[0].Cells[0], rows[0].Cells[^1]);
            target += Margins(rows[0], first, format).Left + Margins(rows[0], last, format).Right;
        }
        return target;
    }

    /// <summary>
    /// Word's autofit column widths (catalog: table.width, measured with docx/local/tbl-autofit in Courier New, all ten
    /// cases within 0.15 pt): every column has a minimum (its widest unbreakable content) and a maximum (its content
    /// unwrapped), each with the cells' margins; a preferred cell width fixes its column. Where the maxima fit, the
    /// columns take them (stretched in proportion to them to a table width that asks for more); where only the minima
    /// fit, every column gets its minimum and a share of the rest in proportion to its maximum less its minimum; where
    /// not even those fit, the minima shrink in proportion, the margins stay; where every column has a preferred width
    /// of its own, those give way instead: columns whose contents need more than they prefer keep their contents'
    /// minimum, the others share the rest in proportion to their preferred widths, none below its contents' minimum
    /// (measured with docx/local/tblpic, table B: beside a 250 pt picture two cells preferring 150 pt take 100 pt
    /// each; tblpic2 V1), and where the contents' minima do not fit either, those shrink (docx/local/tblpic3, table G:
    /// pictures 200, 200 and 100 pt wide in cells preferring 150 pt take 180, 180 and 91 pt).
    /// </summary>
    double[] Autofit(TableModel table, List<RowModel> rows, int columns, double available, double allowance, PageFields? fields, double? target,
        BorderLine?[,]? lines = null)
    {
        var format = table.Format;
        var space = (target ?? available - allowance);
        var (min, max, margins, preferred, natural) = Ranges(table, rows, columns, space, fields, lines);
        var sumMin = min.Sum();
        var sumMax = max.Sum();
        var widths = new double[columns];
        if (sumMax <= space + 0.01)
        {
            if (target == null || sumMax <= 0)
                return max;
            // Stretched to the table's width in proportion to the maxima of the columns without a preferred width.
            var flexible = Enumerable.Range(0, columns).Where(k => preferred[k] == null).ToList();
            if (flexible.Count == 0)
                flexible = Enumerable.Range(0, columns).ToList();
            var fixedSum = Enumerable.Range(0, columns).Except(flexible).Sum(k => max[k]);
            var flexibleSum = flexible.Sum(k => max[k]);
            for (var k = 0; k < columns; k++)
                widths[k] = flexible.Contains(k) && flexibleSum > 0 ? max[k] * (space - fixedSum) / flexibleSum : max[k];
            return widths;
        }
        if (sumMin <= space + 0.01)
        {
            var range = sumMax - sumMin;
            for (var k = 0; k < columns; k++)
                widths[k] = min[k] + (range > 0 ? (space - sumMin) * (max[k] - min[k]) / range : 0);
            return widths;
        }
        // Preferred widths give way where every column has one of its own (as measured); else the minima shrink as
        // they are.
        var own = preferred.All(p => p != null);
        if (own && natural.Sum() <= space + 0.01)
            return Yielding(min, natural, space);
        // Not even the contents' minima fit: they shrink in proportion, the margins stay.
        var shrinking = own ? natural : min;
        var content = Enumerable.Range(0, columns).Sum(k => Math.Max(0, shrinking[k] - margins[k]));
        var room = space - margins.Sum();
        for (var k = 0; k < columns; k++)
            widths[k] = margins[k] + (content > 0 ? Math.Max(0, shrinking[k] - margins[k]) * Math.Max(0, room) / content : 0);
        return widths;
    }

    /// <summary>
    /// Columns whose preferred widths do not fit where their contents would: those that need their minimum keep it,
    /// the others share the rest in proportion to the widths they prefer, none below the minimum of its contents.
    /// </summary>
    static double[] Yielding(double[] min, double[] natural, double space)
    {
        var columns = min.Length;
        var widths = new double[columns];
        var kept = new bool[columns];
        for (var k = 0; k < columns; k++)
            kept[k] = natural[k] >= min[k] - 0.01;
        while (true)
        {
            var rest = space;
            var shares = 0.0;
            for (var k = 0; k < columns; k++)
            {
                if (kept[k])
                    rest -= natural[k];
                else
                    shares += min[k];
            }
            var narrowed = false;
            for (var k = 0; k < columns; k++)
            {
                widths[k] = kept[k] || shares <= 0 ? natural[k] : min[k] * Math.Max(0, rest) / shares;
                if (!kept[k] && widths[k] < natural[k] - 0.01)
                    (kept[k], narrowed) = (true, true);
            }
            if (!narrowed)
                return widths;
        }
    }

    /// <summary>
    /// The columns' minima and maxima, the cells' margins in them, their preferred widths and the minima of their own
    /// cells' contents alone (Natural, before preferred widths fix them and cells across several columns widen them:
    /// spread evenly, a merged cell's nested table would make a narrow column keep more than Word gives it, docx/
    /// external/libreoffice/extras-ooxmlexport/tdf158349_SDTstreamStateStack).
    /// </summary>
    (double[] Min, double[] Max, double[] Margins, double?[] Preferred, double[] Natural) Ranges(TableModel table, List<RowModel> rows, int columns, double space, PageFields? fields,
        BorderLine?[,]? lines = null)
    {
        var format = table.Format;
        var min = new double[columns];
        var max = new double[columns];
        var margins = new double[columns];
        var preferred = new double?[columns];
        var spanning = new List<(int From, int To, double Min, double Max, double? Preferred)>();
        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            // Widths before and after a row (w:wBefore, w:wAfter) do not widen autofit columns (measured with
            // docx/local/tbl-features).
            foreach (var cell in row.Cells)
            {
                var c0 = Math.Min(cell.Column, columns - 1);
                var c1 = Math.Min(columns, cell.Column + cell.Format.GridSpan);
                if (cell.MergedAbove)
                    continue;
                // Margins narrower than half the line beside them give way to it, as in the cell's layout (measured
                // with docx/local/cellborder: margins 0 or 1 pt beside 3 pt lines leave 1.5 pt, 5.4 pt stay).
                var m = Margins(row, cell, format);
                if (lines != null && r < lines.GetLength(0))
                    m = (m.Top, Math.Max(m.Left, Width(lines[r, c0]) / 2), m.Bottom, Math.Max(m.Right, Width(lines[r, c1]) / 2));
                var (cmin, cmax) = Rotated(cell) ? RotatedWidths(cell, fields) : Directed(cell, () => CellWidths(cell.Blocks, fields));
                // A cell that may not wrap (w:noWrap) needs its whole width, unless it prefers a width in points
                // (measured with docx/local/nowrap: such a cell wraps as if it could, one preferring a percentage not).
                if (cell.Format.NoWrap && cell.Format.Width is not { Type: "dxa", Value: > 0 })
                    cmin = cmax;
                // Word measures content at most 22 inches wide, its longest line (measured with docx/local/tbl-features:
                // a column beside one with a very long paragraph gets 52.8 pt, 51.2 pt without the limit).
                cmax = Math.Min(cmax, 1584);
                cmin += m.Left + m.Right;
                cmax += m.Left + m.Right;
                var pref = cell.Format.Width?.Resolve(space) is { } p && p > 0 ? p : (double?)null;
                if (c1 - c0 <= 1)
                {
                    min[c0] = Math.Max(min[c0], cmin);
                    max[c0] = Math.Max(max[c0], cmax);
                    margins[c0] = Math.Max(margins[c0], m.Left + m.Right);
                    if (pref != null)
                        preferred[c0] = Math.Max(preferred[c0] ?? 0, pref.Value);
                }
                else
                    spanning.Add((c0, c1, cmin, cmax, pref));
            }
        }
        // Preferred widths fix their columns first; a cell over several columns then widens them equally where they
        // are too narrow for it together (measured with docx/local/hmerge-g: a 200 pt cell over two 100 pt columns
        // leaves them as they are).
        var natural = (double[])min.Clone();
        for (var k = 0; k < columns; k++)
        {
            if (preferred[k] is { } p)
                min[k] = max[k] = Math.Max(p, min[k]);
        }
        foreach (var (from, to, cmin, cmax, pref) in spanning)
        {
            if (to <= from)
                continue;
            var n = to - from;
            var haveMin = 0.0;
            var haveMax = 0.0;
            for (var k = from; k < to; k++)
            {
                haveMin += min[k];
                haveMax += max[k];
            }
            var wantMin = Math.Max(cmin, pref ?? 0);
            var wantMax = Math.Max(cmax, pref ?? 0);
            for (var k = from; k < to; k++)
            {
                if (wantMin > haveMin) min[k] += (wantMin - haveMin) / n;
                if (wantMax > haveMax) max[k] += (wantMax - haveMax) / n;
                max[k] = Math.Max(max[k], min[k]);
            }
        }
        for (var k = 0; k < columns; k++)
            max[k] = Math.Max(max[k], min[k]);
        return (min, max, margins, preferred, natural);
    }

    /// <summary>The narrowest and widest a cell's content can be: of its paragraphs and nested tables.</summary>
    (double Min, double Max) CellWidths(List<Block> blocks, PageFields? fields)
    {
        double min = 0, max = 0;
        foreach (var block in blocks)
        {
            switch (block)
            {
                case ParagraphBlock paragraph:
                {
                    var (pmin, pmax) = paragraphs.Widths(paragraph, fields, pictures: true);
                    min = Math.Max(min, pmin);
                    max = Math.Max(max, pmax);
                    break;
                }
                case TableBlock nested:
                {
                    var rows = nested.Table.Rows.Where(r => !r.Format.Hidden).ToList();
                    var columns = Math.Max(nested.Table.Grid.Count, rows.Count == 0 ? 0 : rows.Max(r => r.Cells.Count == 0 ? 0 : r.Cells.Max(c => c.Column + c.Format.GridSpan)));
                    if (nested.Table.Format.Layout == "fixed" && nested.Table.Grid.Count > 0)
                    {
                        min = Math.Max(min, nested.Table.Grid.Sum());
                        max = Math.Max(max, nested.Table.Grid.Sum());
                    }
                    else if (columns > 0)
                    {
                        var (nmin, nmax, _, _, _) = Ranges(nested.Table, rows, columns, 100000, fields);
                        min = Math.Max(min, nmin.Sum());
                        max = Math.Max(max, nmax.Sum());
                    }
                    break;
                }
            }
        }
        return (min, max);
    }

    /// <summary>
    /// Blocks laid out in a width as in a table cell or a text box: lines, fills and strokes relative to their top
    /// left, and their height.
    /// </summary>
    public (List<PlacedLine> Lines, List<FillRect> Fills, List<Stroke> Strokes, double Height) Blocks(List<Block> blocks, double width, PageFields? fields)
    {
        var content = Content(blocks, width, fields);
        return (content.Lines, content.Fills, content.Strokes, content.Height);
    }

    /// <summary>A cell's content laid out: lines, fills and strokes relative to its text's top left, its height, its place.</summary>
    sealed record CellContent(List<PlacedLine> Lines, List<FillRect> Fills, List<Stroke> Strokes, double Height, double Left = 0, double Width = 0, double Thickness = 0)
    {
        /// <summary>The floating drawings of the content, relative to its top left.</summary>
        public List<CellAnchor> Anchors { get; init; } = [];
    }

    /// <summary>A cell's blocks laid out in its text width: lines with tops from the content's top, and the height.</summary>
    /// <remarks>
    /// Rotated text (w:textDirection tbRl, btLr) is laid out unwrapped along the cell; its height is its longest
    /// line, its lines are turned when the row is built.
    /// </remarks>
    CellContent Content(CellModel cell, double width, PageFields? fields, (string? Before, string? After) neighbors)
    {
        // An empty cell whose end mark is hidden (w:hideMark) takes no height.
        if (cell.Format.HideMark && cell.Blocks.All(b => b is ParagraphBlock { Content.Count: 0 }))
            return new CellContent([], [], [], 0);
        if (!Rotated(cell))
            return Directed(cell, () => Content(cell.Blocks, width, fields, neighbors.Before, neighbors.After));
        var content = Directed(cell, () => Content(cell.Blocks, Unwrapped, fields));
        var length = content.Lines.Count == 0 ? 0 : content.Lines.Max(l => LineLength(l.Line));
        return content with { Height = length, Thickness = content.Height };
    }

    /// <summary>
    /// Laid out with the cell's text direction (measured with docx/local/vertical2 and vertical5): its lines vertical
    /// where it turns them (tbRl, btLr, tbRlV, tbLrV), its East Asian characters turned against them in tbRlV, tbLrV
    /// (upright) and lrTbV (lying); a cell without a direction of its own keeps the one it stands in.
    /// </summary>
    T Directed<T>(CellModel cell, Func<T> laid)
    {
        var direction = cell.Format.TextDirection;
        if (direction is null or "lrTb")
            return laid();
        var (vertical, turn) = (paragraphs.Vertical, paragraphs.TurnEastAsian);
        (paragraphs.Vertical, paragraphs.TurnEastAsian) = (Rotated(cell), direction is "tbRlV" or "tbLrV" or "lrTbV");
        try
        {
            return laid();
        }
        finally
        {
            (paragraphs.Vertical, paragraphs.TurnEastAsian) = (vertical, turn);
        }
    }

    /// <summary>
    /// Whether two paragraphs that follow each other are items of the same list, between which HTML auto spacing leaves no
    /// space (docx/external/poi/delins as Word prints it: list items 13.8 pt apart in 12 pt Times New Roman, 14 pt
    /// before the first and after the last).
    /// </summary>
    public static bool ListPair(ParagraphBlock first, ParagraphBlock second) =>
        first.Label != null && second.Label != null && first.Format.NumberingId == second.Format.NumberingId;

    /// <summary>The width rotated text is laid out in: wide enough not to wrap.</summary>
    const double Unwrapped = 1584;

    static bool Rotated(CellModel cell) => cell.Format.TextDirection is "tbRl" or "btLr" or "tbRlV" or "tbLrV";

    /// <summary>The length of a line's content, from its left edge to the end of its last piece without trailing spaces.</summary>
    static double LineLength(Line line)
    {
        var end = 0.0;
        foreach (var piece in line.Pieces)
            end = Math.Max(end, piece.X + piece.Width - (piece.IsTab ? 0 : piece.Text.Length - piece.Text.TrimEnd(' ').Length > 0 ? piece.Advances.Skip(piece.Text.TrimEnd(' ').Length).Sum() : 0));
        return end;
    }

    /// <summary>A rotated cell's width: the thickness of its lines.</summary>
    (double Min, double Max) RotatedWidths(CellModel cell, PageFields? fields)
    {
        var content = Directed(cell, () => Content(cell.Blocks, Unwrapped, fields));
        return (content.Height, content.Height);
    }

    /// <param name="previous">The style of the paragraph before the blocks (<see cref="NoParagraph"/> for none).</param>
    /// <param name="next">The style of the paragraph after them.</param>
    /// <remarks>
    /// Contextual spacing works in cells as in the body, but not before Word 2013 where the document says
    /// w:allowSpaceOfSameStyleInTable (catalog: settings.compatibility, measured with docx/local/c5-cells).
    /// </remarks>
    CellContent Content(List<Block> blocks, double width, PageFields? fields, string? previous = NoParagraph, string? next = NoParagraph)
    {
        var lines = new List<PlacedLine>();
        var fills = new List<FillRect>();
        var strokes = new List<Stroke>();
        var anchors = new List<CellAnchor>();
        var y = 0.0;
        var after = 0.0;
        var ownAfter = 0.0;
        var first = true;
        contextual ??= !(context.CompatibilityMode < 15 && context.Compat("allowSpaceOfSameStyleInTable"));
        for (var b = 0; b < blocks.Count; b++)
        {
            var block = blocks[b];
            switch (block)
            {
                // A cell's empty last paragraph after a nested table takes no room, whatever its spacing and mark
                // (measured with docx/local/nestedend-12, -14 and -15).
                case ParagraphBlock last when b == blocks.Count - 1 && b > 0 && blocks[b - 1] is TableBlock && !last.Shows:
                    break;
                case ParagraphBlock paragraph:
                {
                    var style = StyleOf(paragraph);
                    var spaced = paragraph.Format.ContextualSpacing != true || contextual == false;
                    // HTML auto spacing is 14 pt between a cell's blocks, none at its top and bottom (measured with
                    // docx/local/autospace-cells-15 and -14).
                    var autoSpacing = !context.AddsSpacing;
                    var before = !spaced && previous == style ? 0
                        : paragraph.Format.BeforeAuto == true && autoSpacing ? (first || b > 0 && blocks[b - 1] is ParagraphBlock prior && ListPair(prior, paragraph) ? 0 : 14)
                        : paragraph.Format.Before ?? 0;
                    var following = b + 1 < blocks.Count
                        ? blocks[b + 1] switch
                        {
                            ParagraphBlock p => StyleOf(p),
                            TableBlock t => FirstParagraph(t.Table) is { } p ? StyleOf(p) : NoParagraph,
                            _ => NoParagraph,
                        }
                        : next;
                    // Drawings count from the paragraph's top above its own space before, as in the body.
                    var paragraphTop = y + (first ? 0 : after);
                    y += first ? before : context.Between(after, before, ownAfter);
                    var lineTops = new Dictionary<AnchorInline, (double Top, double X)>(ReferenceEqualityComparer.Instance);
                    var firstLine = lines.Count;
                    foreach (var line in paragraphs.Layout(paragraph, width, fields))
                    {
                        foreach (var anchor in line.Anchors ?? [])
                            lineTops[anchor] = (y, line.AnchorX?.GetValueOrDefault(anchor) ?? 0);
                        lines.Add(new PlacedLine(line, 0, y));
                        y += line.Height;
                    }
                    foreach (var anchor in paragraph.Content.OfType<AnchorInline>())
                    {
                        var (lineTop, x) = lineTops.GetValueOrDefault(anchor, (paragraphTop, 0));
                        anchors.Add(new CellAnchor(anchor, paragraphTop, lineTop) { CharacterX = x });
                    }
                    ownAfter = paragraph.Format.AfterAuto == true && autoSpacing
                        ? (b == blocks.Count - 1 || blocks[b + 1] is ParagraphBlock nextParagraph && ListPair(paragraph, nextParagraph) ? 0 : 14)
                        : paragraph.Format.After ?? 0;
                    after = !spaced && following == style ? 0 : ownAfter;
                    if (lines.Count > firstLine)
                        lines[^1] = lines[^1] with { After = after };
                    previous = style;
                    first = false;
                    break;
                }
                case TableBlock nested:
                {
                    y += first ? 0 : after;
                    var box = Layout(nested.Table, width, fields, previous);
                    var tableTop = y;
                    foreach (var row in box.Rows)
                    {
                        Draw(row, row.Height, 0, y, lines, fills, fills, strokes, anchors);
                        y += row.Height;
                    }
                    if (box.Rows.Count > 0)
                    {
                        var last = box.Rows[^1];
                        fills.AddRange(last.BottomBorders.Select(f => f with { Y = y + f.Y }));
                        strokes.AddRange(last.BottomStrokes.Select(s => s.Offset(0, y)));
                        y += last.BottomBorderWidth;
                    }
                    Frame(box, 0, tableTop, y, fills);
                    after = 0;
                    ownAfter = 0;
                    previous = box.Rows.Count > 0 ? context.Styles.ParagraphStyleOf(null) : previous;
                    first = false;
                    break;
                }
            }
        }
        return new CellContent(lines, fills, strokes, y + after) { Anchors = anchors };
    }

    /// <summary>
    /// Draws a row (or the part of it on a page) <paramref name="height"/> points high at (left, top): shading, the
    /// lines at its top and through it, the diagonals and its cells' content.
    /// </summary>
    public static void Draw(RowBox row, double height, double left, double top, List<PlacedLine> lines, List<FillRect> shading, List<FillRect> borders, List<Stroke> strokes,
        List<CellAnchor>? anchors = null)
    {
        foreach (var (x, width, color) in row.Shading)
            shading.Add(new FillRect(left + x, top, width, height, color));
        foreach (var fill in row.Frames)
            borders.Add(fill with { X = left + fill.X, Y = top + fill.Y });
        foreach (var fill in row.TopBorders)
            borders.Add(fill with { X = left + fill.X, Y = top + fill.Y });
        strokes.AddRange(row.TopStrokes.Select(s => s.Offset(left, top)));
        foreach (var stripe in row.Verticals)
        {
            if (stripe.Dash != null)
                strokes.Add(new Stroke(left + stripe.X + stripe.Width / 2, top, left + stripe.X + stripe.Width / 2, top + height, stripe.Width, stripe.Color, stripe.Dash));
            else
                borders.Add(new FillRect(left + stripe.X, top, stripe.Width, height, stripe.Color));
        }
        foreach (var cell in row.Cells)
        {
            var (from, to, down, up) = cell.Diagonals;
            if (down is { Visible: true })
                strokes.Add(new Stroke(left + from, top + row.TopWidth, left + to, top + height, down.Width, down.Color, Dash(down)));
            if (up is { Visible: true })
                strokes.Add(new Stroke(left + to, top + row.TopWidth, left + from, top + height, up.Width, up.Color, Dash(up)));
            foreach (var line in cell.Lines)
                lines.Add(line with { Left = left + line.Left, Top = top + line.Top });
            foreach (var fill in cell.Fills)
                borders.Add(fill with { X = left + fill.X, Y = top + fill.Y });
            strokes.AddRange(cell.Strokes.Select(s => s.Offset(left, top)));
            anchors?.AddRange(cell.Anchors.Select(a => a.Offset(left, top)));
        }
    }

    /// <summary>The frame of a table with cell spacing from <paramref name="top"/> to <paramref name="bottom"/>.</summary>
    public static void Frame(TableBox box, double left, double top, double bottom, List<FillRect> borders)
    {
        var (t, l, b, r) = box.Frame;
        var x = left + box.Left;
        if (t is { Visible: true })
            borders.Add(new FillRect(x, top, box.Width, t.Total, t.ColorOf("top", cell: false)));
        if (b is { Visible: true })
            borders.Add(new FillRect(x, bottom - b.Total, box.Width, b.Total, b.ColorOf("bottom", cell: false)));
        if (l is { Visible: true })
            borders.Add(new FillRect(x, top, l.Total, bottom - top, l.ColorOf("left", cell: false)));
        if (r is { Visible: true })
            borders.Add(new FillRect(x + box.Width - r.Total, top, r.Total, bottom - top, r.ColorOf("right", cell: false)));
    }
}
