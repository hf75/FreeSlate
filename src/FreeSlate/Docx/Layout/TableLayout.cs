using FreeSlate.Drawing;

namespace FreeSlate.Docx.Layout;

/// <summary>A filled rectangle on a page: a border line or shading, in points from the page's top left.</summary>
public sealed record FillRect(double X, double Y, double Width, double Height, RgbColor Color);

/// <summary>
/// A laid-out table row: its height, the lines of its cells and their fills relative to the row's top left (the
/// table's origin), and what it needs at a page break.
/// </summary>
public sealed class RowBox
{
    public double Height { get; set; }
    public bool Header { get; init; }
    public bool CantSplit { get; init; }

    /// <summary>Lines with their left edge and top relative to the table's origin and the row's top.</summary>
    public List<PlacedLine> Lines { get; } = [];
    public List<FillRect> Shading { get; } = [];
    public List<FillRect> Borders { get; } = [];

    /// <summary>The border below the row where the table ends or breaks after it, drawn at the row's bottom.</summary>
    public List<FillRect> BottomBorders { get; } = [];
    public double BottomBorderWidth { get; set; }
}

/// <summary>A laid-out table: its left edge from the column's, its width and its rows.</summary>
public sealed class TableBox
{
    public double Left { get; init; }
    public double Width { get; init; }
    public List<RowBox> Rows { get; } = [];
}

/// <summary>
/// Lays out tables the way Word 2013 and later do (catalog: engine.tableLayout, table.width, table.borders,
/// table.rows, table.cells), measured with docx/local/tbl-geom: the grid starts at the indent from the column's left
/// edge (older compatibility modes move it left by the first cell's left margin); each border starts at its grid
/// line and extends right or down; text starts the larger of the left margin plus half the border and the whole
/// border after the grid line, and below the whole top border plus the top margin; a row is its top border and its
/// content, at least or exactly its height; a table's bottom border lies below its last row.
/// </summary>
public sealed class TableLayout(DocumentContext context, ParagraphLayout paragraphs)
{
    /// <summary>The default left and right cell margin where neither table nor style gives one (0.075 in).</summary>
    const double DefaultMargin = 5.4;

    bool? modern;

    /// <summary>Whether the document lays tables out as Word 2013 does (compatibility mode 15 and later).</summary>
    bool Modern => modern ??= context.Settings.Child("compat")?.Elements(Wml.W + "compatSetting")
        .FirstOrDefault(e => e.Attr("name") == "compatibilityMode")?.Attr("val") is { } v && int.TryParse(v, out var mode) && mode >= 15;

    public TableBox Layout(TableModel table, double available, PageFields? fields = null)
    {
        var format = table.Format;
        var rows = table.Rows.Where(r => !r.Format.Hidden).ToList();
        var columns = Math.Max(table.Grid.Count, rows.Count == 0 ? 0 : rows.Max(r => r.Cells.Count == 0 ? 0 : r.Cells.Max(c => c.Column + c.Format.GridSpan) + r.Format.GridAfter));
        var grid = Grid(table, columns, available);
        var x = new double[grid.Length + 1];
        for (var k = 0; k < grid.Length; k++)
            x[k + 1] = x[k] + grid[k];

        // Every cell's place in the grid and the rows a vertical merge spans.
        var at = new CellModel?[rows.Count, Math.Max(1, columns)];
        for (var r = 0; r < rows.Count; r++)
        {
            foreach (var cell in rows[r].Cells)
            {
                for (var k = cell.Column; k < Math.Min(columns, cell.Column + cell.Format.GridSpan); k++)
                    at[r, k] = cell;
            }
        }
        // The cell that starts a vertical merge, for each cell that continues it.
        var origin = new Dictionary<CellModel, (int Row, CellModel Cell)>();
        var span = new Dictionary<CellModel, int>();
        for (var r = 0; r < rows.Count; r++)
        {
            foreach (var cell in rows[r].Cells)
            {
                if (cell.MergedAbove && r > 0 && cell.Column < columns && at[r - 1, cell.Column] is { } above)
                {
                    (int Row, CellModel Cell) start = origin.TryGetValue(above, out var o) ? o : (r - 1, above);
                    origin[cell] = start;
                    span[start.Cell] = r - start.Row + 1;
                }
            }
        }

        // Borders: for each row boundary (0 … rows) and grid column the horizontal line, for each row and grid line
        // the vertical one.
        var horizontal = new BorderLine?[rows.Count + 1, Math.Max(1, columns)];
        var vertical = new BorderLine?[rows.Count, columns + 1];
        for (var r = 0; r <= rows.Count; r++)
        {
            for (var k = 0; k < columns; k++)
            {
                var above = r > 0 ? at[r - 1, k] : null;
                var below = r < rows.Count ? at[r, k] : null;
                // Inside a vertical merge there is no line; a merged cell's borders are those of the cell that starts it.
                var aboveCell = above != null && origin.TryGetValue(above, out var ao) ? ao.Cell : above;
                var belowCell = below != null && origin.TryGetValue(below, out var bo) ? bo.Cell : below;
                if (aboveCell != null && aboveCell == belowCell)
                    continue;
                var tableLine = r == 0 || above == null ? TableBorder(rows[Math.Min(r, rows.Count - 1)], format, b => b.Top)
                    : r == rows.Count || below == null ? TableBorder(rows[r - 1], format, b => b.Bottom)
                    : TableBorder(rows[r], format, b => b.InsideH);
                horizontal[r, k] = Edge(aboveCell?.CellBorders.Bottom, belowCell?.CellBorders.Top, tableLine);
            }
        }
        for (var r = 0; r < rows.Count; r++)
        {
            for (var k = 0; k <= columns; k++)
            {
                var left = k > 0 ? at[r, k - 1] : null;
                var right = k < columns ? at[r, k] : null;
                if (left != null && left == right)
                    continue;
                var tableLine = k == 0 || left == null ? TableBorder(rows[r], format, b => b.Left)
                    : k == columns || right == null ? TableBorder(rows[r], format, b => b.Right)
                    : TableBorder(rows[r], format, b => b.InsideV);
                // A merged cell's borders are those of the cell that starts the merge.
                var leftCell = left != null && origin.TryGetValue(left, out var lo) ? lo.Cell : left;
                var rightCell = right != null && origin.TryGetValue(right, out var ro) ? ro.Cell : right;
                vertical[r, k] = Edge(leftCell?.CellBorders.Right, rightCell?.CellBorders.Left, tableLine);
            }
        }

        // The table's left edge: Word 2013 puts the grid at the indent; older versions put the first cell's text there.
        // The grid moves right by half the table's left border; every vertical line is centered on its grid line, so
        // the left border's outer edge lies at the table's left edge (measured with docx/local/tbl-geom: a 3 pt
        // inside line of a table with 0.5 pt outside lines is centered 0.25 pt right of its grid line).
        var shift = rows.Count > 0 ? Width(vertical[0, 0]) / 2 : 0;
        var firstMargin = rows.Count > 0 && rows[0].Cells.Count > 0 ? Margins(rows[0], rows[0].Cells[0], format).Left : DefaultMargin;
        var rightBorder = rows.Count > 0 ? Width(vertical[0, columns]) : 0;
        var tableWidth = x[^1] + shift + rightBorder / 2;
        var indent = format.Indent ?? 0;
        double origin0 = format.Justification switch
        {
            "center" => (available - tableWidth) / 2,
            "right" or "end" => available - tableWidth,
            _ => indent - (Modern ? 0 : shift + Math.Max(firstMargin, shift)),
        };
        var box = new TableBox { Left = origin0, Width = tableWidth };

        // Cell content, row heights.
        var laid = new Dictionary<CellModel, CellContent>();
        var contentHeights = new double[rows.Count];
        var topWidths = new double[rows.Count + 1];
        for (var r = 0; r <= rows.Count; r++)
        {
            for (var k = 0; k < columns; k++)
                topWidths[r] = Math.Max(topWidths[r], Width(horizontal[r, k]));
        }
        for (var r = 0; r < rows.Count; r++)
        {
            foreach (var cell in rows[r].Cells)
            {
                if (cell.MergedAbove && origin.ContainsKey(cell))
                    continue;
                var c0 = Math.Min(cell.Column, columns);
                var c1 = Math.Min(cell.Column + cell.Format.GridSpan, columns);
                var margins = Margins(rows[r], cell, format);
                var leftLine = Width(vertical[r, c0]);
                var rightLine = Width(vertical[r, c1]);
                var textLeft = x[c0] + shift + Math.Max(margins.Left, leftLine / 2);
                var textRight = x[c1] + shift - Math.Max(margins.Right, rightLine / 2);
                var width = Math.Max(1, textRight - textLeft);
                var content = Content(cell.Blocks, width, fields);
                laid[cell] = content with { Left = textLeft, Width = width };
                var height = content.Height;
                if (!span.ContainsKey(cell))
                    contentHeights[r] = Math.Max(contentHeights[r], margins.Top + height + margins.Bottom);
            }
        }
        var heights = new double[rows.Count];
        for (var r = 0; r < rows.Count; r++)
        {
            var f = rows[r].Format;
            var content = contentHeights[r];
            if (f.Height is { } h && h > 0)
            {
                // At least: the content area (without the row's top border); exactly: the whole row.
                content = f.HeightRule == "exact" ? Math.Max(0, h - topWidths[r]) : Math.Max(content, h);
            }
            heights[r] = content;
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

        // A horizontal line's extent over grid column k: over the vertical lines at its ends.
        (double From, double To) Span(int k, int r)
        {
            var from = x[k] + shift - Width(vertical[r, k]) / 2;
            var to = x[k + 1] + shift + Width(vertical[r, k + 1]) / 2;
            return (from, to);
        }

        // Row boxes: shading, borders and lines relative to each row's top.
        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            var rowBox = new RowBox { Header = row.Format.Header, CantSplit = row.Format.CantSplit };
            rowBox.Height = topWidths[r] + heights[r];
            foreach (var cell in row.Cells)
            {
                if (cell.MergedAbove && origin.ContainsKey(cell))
                    continue;
                var c0 = Math.Min(cell.Column, columns);
                var c1 = Math.Min(cell.Column + cell.Format.GridSpan, columns);
                var rowsSpanned = span.GetValueOrDefault(cell, 1);
                var total = 0.0;
                for (var k = r; k < Math.Min(rows.Count, r + rowsSpanned); k++)
                    total += heights[k] + (k > r ? topWidths[k] : 0);
                // Shading fills the cell from its grid lines (the borders are drawn over it).
                if ((Shading.Color(cell.Format.Shading) ?? Shading.Color(row.ShadingException) ?? Shading.Color(table.Format.Shading)) is { } fill)
                    rowBox.Shading.Add(new FillRect(origin0 + x[c0] + shift, 0, x[c1] - x[c0], topWidths[r] + total, fill));
                var content = laid[cell];
                var margins = Margins(row, cell, format);
                var free = total - margins.Top - content.Height - margins.Bottom;
                var lowered = cell.Format.VerticalAlign switch
                {
                    "center" => Math.Max(0, free) / 2,
                    "bottom" => Math.Max(0, free),
                    _ => 0,
                };
                var dx = origin0 + content.Left;
                var dy = topWidths[r] + margins.Top + lowered;
                foreach (var line in content.Lines)
                    rowBox.Lines.Add(line with { Left = dx + line.Left, Top = dy + line.Top });
                foreach (var nested in content.Shading)
                    rowBox.Shading.Add(nested with { X = dx + nested.X, Y = dy + nested.Y });
                foreach (var nested in content.Borders)
                    rowBox.Borders.Add(nested with { X = dx + nested.X, Y = dy + nested.Y });
            }
            // Horizontal lines at the row's top (from the outer edge of the vertical line at their start to that at
            // their end), vertical lines through the row, centered on their grid lines.
            for (var k = 0; k < columns; k++)
            {
                if (horizontal[r, k] is { Visible: true } line)
                {
                    var (from, to) = Span(k, r);
                    rowBox.Borders.Add(new FillRect(origin0 + from, 0, to - from, line.Width, line.Color));
                }
            }
            for (var k = 0; k <= columns; k++)
            {
                if (vertical[r, k] is { Visible: true } line)
                    rowBox.Borders.Add(new FillRect(origin0 + x[k] + shift - line.Width / 2, 0, line.Width, rowBox.Height, line.Color));
            }
            // The line below the row, drawn where the table ends or breaks after it.
            rowBox.BottomBorderWidth = 0;
            for (var k = 0; k < columns; k++)
            {
                if (horizontal[r + 1, k] is { Visible: true } line)
                {
                    var (from, to) = Span(k, r);
                    rowBox.BottomBorders.Add(new FillRect(origin0 + from, 0, to - from, line.Width, line.Color));
                    rowBox.BottomBorderWidth = Math.Max(rowBox.BottomBorderWidth, line.Width);
                }
            }
            box.Rows.Add(rowBox);
        }
        return box;
    }

    static double Width(BorderLine? line) => line is { Visible: true } ? line.Width : 0;

    /// <summary>
    /// The line where two cells meet (MS-OI29500 17.4.66): a cell's own border wins over the table's; two cell
    /// borders are resolved by weight, style and color.
    /// </summary>
    static BorderLine? Edge(BorderLine? a, BorderLine? b, BorderLine? table)
    {
        if (a != null || b != null)
            return BorderLine.Resolve(a, b);
        return table;
    }

    /// <summary>A table border of a row: the row's exceptions over the table's.</summary>
    static BorderLine? TableBorder(RowModel row, TableFormat format, Func<BorderSet, BorderLine?> side) =>
        (row.BorderExceptions != null ? side(row.BorderExceptions) : null) ?? side(format.Borders);

    /// <summary>A cell's margins: its own over the row's exceptions over the table's.</summary>
    static (double Top, double Left, double Bottom, double Right) Margins(RowModel row, CellModel cell, TableFormat format)
    {
        var m = format.Margins.Overlay(row.MarginExceptions).Overlay(cell.Format.Margins);
        return (m.Top ?? 0, m.Left ?? DefaultMargin, m.Bottom ?? 0, m.Right ?? DefaultMargin);
    }

    /// <summary>
    /// The column widths: the grid Word saved (it is the result of its own autofit); without one, the cells' widths,
    /// else equal shares of the available width.
    /// </summary>
    static double[] Grid(TableModel table, int columns, double available)
    {
        var grid = new double[columns];
        for (var k = 0; k < columns; k++)
            grid[k] = k < table.Grid.Count ? table.Grid[k] : 0;
        if (grid.All(w => w > 0))
            return grid;
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
        return grid;
    }

    /// <summary>A cell's content laid out: lines and fills relative to its text's top left, its height, its place.</summary>
    sealed record CellContent(List<PlacedLine> Lines, List<FillRect> Shading, List<FillRect> Borders, double Height, double Left = 0, double Width = 0);

    /// <summary>A cell's blocks laid out in its text width: lines with tops from the content's top, and the height.</summary>
    CellContent Content(List<Block> blocks, double width, PageFields? fields)
    {
        var lines = new List<PlacedLine>();
        var shading = new List<FillRect>();
        var borders = new List<FillRect>();
        var y = 0.0;
        var after = 0.0;
        var first = true;
        foreach (var block in blocks)
        {
            switch (block)
            {
                case ParagraphBlock paragraph:
                {
                    var before = paragraph.Format.Before ?? 0;
                    y += first ? before : Math.Max(after, before);
                    foreach (var line in paragraphs.Layout(paragraph, width, fields))
                    {
                        lines.Add(new PlacedLine(line, 0, y));
                        y += line.Height;
                    }
                    after = paragraph.Format.After ?? 0;
                    first = false;
                    break;
                }
                case TableBlock nested:
                {
                    y += first ? 0 : after;
                    var box = Layout(nested.Table, width, fields);
                    foreach (var row in box.Rows)
                    {
                        foreach (var line in row.Lines)
                            lines.Add(line with { Top = y + line.Top });
                        foreach (var fill in row.Shading)
                            shading.Add(fill with { Y = y + fill.Y });
                        foreach (var fill in row.Borders)
                            borders.Add(fill with { Y = y + fill.Y });
                        y += row.Height;
                    }
                    if (box.Rows.Count > 0)
                    {
                        foreach (var fill in box.Rows[^1].BottomBorders)
                            borders.Add(fill with { Y = y + fill.Y });
                        y += box.Rows[^1].BottomBorderWidth;
                    }
                    after = 0;
                    first = false;
                    break;
                }
            }
        }
        return new CellContent(lines, shading, borders, y + after);
    }
}
