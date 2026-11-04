using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using FreeSlate.Rendering;
using FreeSlate.Skia;
using SkiaSharp;

namespace FreeSlate.Editor.Controls;

/// <summary>
/// Tables in the view: where their rows stand on the pages (the scenes' TableRowItem) and so their cells, the lines of the cells of the
/// table the cursor stands in drawn faintly over the page (as Word's gridlines show the lines a table's look leaves out), the cursor
/// going up and down from cell to cell and a click in a cell going into that cell, and the lines between cells dragged: an upright one
/// makes the columns beside it wider and narrower (the outside ones the table), one across makes the row above it higher.
/// </summary>
public sealed partial class DocumentView
{
    public static readonly StyledProperty<IBrush?> GuideBrushProperty = AvaloniaProperty.Register<DocumentView, IBrush?>(nameof(GuideBrush));

    /// <summary>The lines of the cells of the table the cursor stands in, dashed over the page (see DrawGuides).</summary>
    public IBrush? GuideBrush
    {
        get => GetValue(GuideBrushProperty);
        set => SetValue(GuideBrushProperty, value);
    }

    /// <summary>The rows of a table a page places, in the page's order (points, see TableRowItem).</summary>
    List<TableRowItem> RowsOf(int page, int table) => [.. Page(page).Items.OfType<TableRowItem>().Where(r => r.Table == table)];

    /// <summary>The cells of the tables a page shows, read once for each of its scenes (a change of a table draws its pages anew).</summary>
    readonly ConditionalWeakTable<PageScene, Dictionary<int, TableCells?>> cellsShown = new();

    /// <summary>A table's cells as a page shows it (see WordDocument.CellsOf); null for a table the document no longer has.</summary>
    TableCells? CellsOn(int page, int table)
    {
        if (session?.Document is not WordDocument word)
            return null;
        var known = cellsShown.GetOrCreateValue(Page(page));
        return known.TryGetValue(table, out var cells) ? cells : known[table] = word.CellsOf(table);
    }

    /// <summary>The cell of a table a point of a page is in (CSS pixels of the page); null outside tables.</summary>
    TableCell? CellAt(int page, SKPoint local)
    {
        foreach (var table in Page(page).Items.OfType<TableRowItem>().Select(r => r.Table).Distinct())
        {
            if (CellsOn(page, table) is { } cells && CellAreas(page, cells).FirstOrDefault(c => c.Area.Contains(local)) is { Cell: { } cell })
                return cell;
        }
        return null;
    }

    /// <summary>
    /// The cells of a table as a page shows them: each one's rectangle in CSS pixels of the page, over the rows of it the page places (a
    /// cell a page break cuts on each of its pages, the head on each page that repeats it).
    /// </summary>
    List<(TableCell Cell, SKRect Area)> CellAreas(int page, TableCells cells)
    {
        var rows = RowsOf(page, cells.Table);
        var made = new List<(TableCell, SKRect)>();
        foreach (var cell in cells.Cells)
        {
            var placed = rows.Where(r => r.Row >= cell.Top && r.Row < cell.Bottom).ToList();
            if (placed.Count == 0 || placed[0].Lines.Length <= cell.Right)
                continue;
            // Repeated, a head row stands at the page's top apart from the rows the page has of its own.
            foreach (var part in placed.GroupBy(r => r.Repeated))
            {
                var lines = part.First().Lines;
                var (left, right) = (Math.Min(lines[cell.Left], lines[cell.Right]), Math.Max(lines[cell.Left], lines[cell.Right]));
                made.Add((cell, new SKRect((float)(left * PxPerPt), (float)(part.Min(r => r.Top) * PxPerPt), (float)(right * PxPerPt), (float)(part.Max(r => r.Bottom) * PxPerPt))));
            }
        }
        return made;
    }

    /// <summary>The table the cursor stands in, with its cells; null outside tables.</summary>
    TableCells? CursorTable => session?.Table;

    /// <summary>
    /// The lines of the cells of the table the cursor stands in, dashed and faint over the pages in view: the shape of a table whose look
    /// leaves lines out (see Tones), as Word's gridlines show it, only while one writes in it, a new one shown by the ring too.
    /// </summary>
    void DrawGuides(DrawingContext context, IReadOnlyList<(Row Row, double Left)> visible)
    {
        if (GuideBrush is not { } brush || CursorTable is not { } cells)
            return;
        var pen = new Pen(brush, 1, new DashStyle([2, 3], 0));
        foreach (var (row, left) in visible)
        {
            foreach (var (_, area) in CellAreas(row.Index, cells))
            {
                // On whole pixels, half a pixel in, so that the dashes are sharp.
                var (x1, y1) = (Math.Round(left + area.Left * factor) + 0.5, Math.Round((row.Top + area.Top) * factor) + 0.5);
                var (x2, y2) = (Math.Round(left + area.Right * factor) - 0.5, Math.Round((row.Top + area.Bottom) * factor) - 0.5);
                context.DrawRectangle(null, pen, new Rect(new Point(x1, y1), new Point(x2, y2)));
            }
        }
    }

    /// <summary>The grid column of a table a place of a page stands over (x in points); the nearest where it stands beside the table.</summary>
    int? ColumnAt(int page, int table, double x)
    {
        if (RowsOf(page, table) is not [var row, ..] || row.Lines.Length < 2)
            return null;
        var lines = row.Lines;
        var best = 0;
        for (var k = 0; k < lines.Length - 1; k++)
        {
            var (from, to) = (Math.Min(lines[k], lines[k + 1]), Math.Max(lines[k], lines[k + 1]));
            if (x >= from && x < to)
                return k;
            if (Math.Abs((from + to) / 2 - x) < Math.Abs((Math.Min(lines[best], lines[best + 1]) + Math.Max(lines[best], lines[best + 1])) / 2 - x))
                best = k;
        }
        return best;
    }

    /// <summary>
    /// Where a key up or down takes the cursor in a table, as in Word: from a cell's first or last line to the cell above or below it,
    /// at the column it goes up and down to (see goal); out of the table's first or last row to the text before or after the table.
    /// Null where the cursor is not in a table's cell or the cell has another line that way (the lines in their order then).
    /// </summary>
    (int Page, int At, bool End)? CellAcross(int page, int at, bool down)
    {
        if (session?.Document is not WordDocument word)
            return null;
        var places = pictures.Places(Page(page));
        var spots = pictures.Spots(Page(page));
        if (spots.Count == 0)
            return null;
        var (current, after) = CaretSpot(spots, at);
        var block = places.Lines[current.Line].Line.Block;
        if (word.TableOf(block) is not { } table || word.CellsOf(table) is not { } cells || cells.Cells.FirstOrDefault(c => c.Paragraphs.Contains(block)) is not { } cell)
            return null;
        var x = goal ??= places.CaretAt(current, after).Top.X;
        var step = down ? 1 : -1;
        bool Holds(TextPlaces places, int line, ISet<int> paragraphs) => places.Lines[line].Line.Offsets.Length > 0 && paragraphs.Contains(places.Lines[line].Line.Block);
        // Another line of the cell that way: the lines in their order.
        var own = cell.Paragraphs.ToHashSet();
        for (var k = current.Line + step; k >= 0 && k < places.Lines.Count; k += step)
        {
            if (Holds(places, k, own))
                return null;
        }
        var row = down ? cell.Bottom : cell.Top - 1;
        ISet<int> target;
        if (row < 0 || row >= cells.Rows)
        {
            // Out of the table: the first line that way that is none of its cells', in the page's own text.
            var all = cells.Cells.SelectMany(c => c.Paragraphs).ToHashSet();
            target = new HashSet<int>();
            for (var (p, k) = (page, current.Line + step); p >= 0 && p < scenes.Count; p += step, k = -1)
            {
                var on = pictures.Places(Page(p));
                for (k = k >= 0 ? k : down ? 0 : on.Lines.Count - 1; k >= 0 && k < on.Lines.Count; k += step)
                {
                    var line = on.Lines[k].Line;
                    if (line.Offsets.Length > 0 && !line.Floating && !all.Contains(line.Block) && session.Document.DrawingAround(line.Block) == null)
                        return HitOn(p, k, x, new HashSet<int> { line.Block });
                }
            }
            return null;
        }
        var column = ColumnAt(page, table, x / PxPerPt) ?? cell.Left;
        var next = cells.Cells.FirstOrDefault(c => c.Top <= row && row < c.Bottom && c.Left <= column && column < c.Right)
            ?? cells.Cells.FirstOrDefault(c => c.Top <= row && row < c.Bottom);
        if (next == null)
            return null;
        target = next.Paragraphs.ToHashSet();
        // Its first line going down, its last going up, on this page or the next ones that way.
        for (var p = page; p >= 0 && p < scenes.Count; p += step)
        {
            var on = pictures.Places(Page(p));
            for (var k = down ? 0 : on.Lines.Count - 1; k >= 0 && k < on.Lines.Count; k += step)
            {
                if (Holds(on, k, target))
                    return HitOn(p, k, x, target);
            }
        }
        return null;
    }

    /// <summary>The place nearest to a column in a line of a page, among the lines of some paragraphs.</summary>
    (int Page, int At, bool End)? HitOn(int page, int line, float x, ISet<int> paragraphs)
    {
        var places = pictures.Places(Page(page));
        var (toPage, placed) = places.Lines[line];
        var middle = toPage.MapPoint((float)(placed.Starts.DefaultIfEmpty(0).First() * PxPerPt), (float)((placed.Top + placed.Bottom) / 2 * PxPerPt));
        return places.HitPlace(new SKPoint(x, middle.Y), l => paragraphs.Contains(l.Block)) is var (spot, after) ? Placed(page, spot, after) : null;
    }

    /// <summary>How near a point of the view must be to a line between cells to take it (pixels), and how narrow a column may become (points).</summary>
    const double LineReach = 4, LeastColumn = 12;

    static readonly Cursor Sideways = new(StandardCursorType.SizeWestEast), Upways = new(StandardCursorType.SizeNorthSouth);

    /// <summary>
    /// A line between cells of a table as a page shows it: the table, the line (a line of the grid for an upright one; for one across, the
    /// row above it, whose height it is), whether it is upright, the page, and where it stands there (points: an upright one's x, one
    /// across its y).
    /// </summary>
    sealed record TableLine(int Table, int Line, bool Upright, int Page, double At);

    /// <summary>
    /// The line between cells of a table a point of the view is on, the nearest within reach: an upright one where cells of a row meet
    /// or the table's side is, one across where cells end at a row's bottom (not where a page cuts the row); null elsewhere.
    /// </summary>
    TableLine? TableLineAt(Point point)
    {
        if (session == null || cropping || tilting || dragged != null || rows.FirstOrDefault(r => point.Y / factor >= r.Top && point.Y / factor <= r.Top + r.Size.Height) is not { } row)
            return null;
        var (x, y) = ((point.X - Left(row)) / factor / PxPerPt, (point.Y / factor - row.Top) / PxPerPt);
        var reach = LineReach / factor / PxPerPt;
        var (best, nearest) = ((TableLine?)null, reach);
        foreach (var placed in Page(row.Index).Items.OfType<TableRowItem>().GroupBy(r => r.Table))
        {
            if (CellsOn(row.Index, placed.Key) is not { } cells)
                continue;
            foreach (var part in placed)
            {
                if (y < part.Top - reach || y > part.Bottom + reach)
                    continue;
                var here = cells.Cells.Where(c => c.Top <= part.Row && part.Row < c.Bottom && c.Right < part.Lines.Length).ToList();
                if (y >= part.Top && y <= part.Bottom)
                {
                    foreach (var k in here.SelectMany(c => new[] { c.Left, c.Right }).Distinct())
                    {
                        if (Math.Abs(x - part.Lines[k]) is var d && d < nearest)
                            (best, nearest) = (new TableLine(placed.Key, k, true, row.Index, part.Lines[k]), d);
                    }
                }
                if (Math.Abs(y - part.Bottom) is var dy && dy < nearest && !Cut(row.Index, part)
                    && here.Any(c => c.Bottom == part.Row + 1 && x >= Math.Min(part.Lines[c.Left], part.Lines[c.Right]) && x <= Math.Max(part.Lines[c.Left], part.Lines[c.Right])))
                    (best, nearest) = (new TableLine(placed.Key, part.Row, false, row.Index, part.Bottom), dy);
            }
        }
        return best;
    }

    /// <summary>Whether a page breaks a row of a table, which goes on on the next page.</summary>
    bool Cut(int page, TableRowItem part) =>
        !part.Repeated && page + 1 < scenes.Count && RowsOf(page + 1, part.Table).Any(r => r.Row == part.Row && !r.Repeated);

    /// <summary>
    /// A line between cells pressed: where, the row's height on its page and what it has beyond its at-least height (for one across); once
    /// the pointer moves, how far it goes (points, snapped), and how far the document shows it gone.
    /// </summary>
    sealed class LineDrag(TableLine line, Point start, IPointer pointer, double height, double extra)
    {
        public TableLine Line { get; } = line;
        public Point Start { get; } = start;
        public IPointer Pointer { get; } = pointer;
        public double Height { get; } = height;
        public double Extra { get; } = extra;
        public double By { get; set; }
        public double? Shown { get; set; }
        public Point At { get; set; } = start;
        public bool Moving { get; set; }
        public bool Previewed { get; set; }
        public bool Scheduled { get; set; }
    }

    LineDrag? lineDrag;

    /// <summary>A press on a line between cells: dragging it begins (see LineDragTo). Whether the point was on one.</summary>
    bool BeginLineDrag(Point point, IPointer pointer)
    {
        if (TableLineAt(point) is not { } line)
            return false;
        var part = line.Upright ? null : RowsOf(line.Page, line.Table).FirstOrDefault(r => r.Row == line.Line);
        lineDrag = new LineDrag(line, point, pointer, part is { } p ? p.Bottom - p.Top : 0, part?.Extra ?? 0);
        pointer.Capture(this);
        Cursor = line.Upright ? Sideways : Upways;
        return true;
    }

    /// <summary>
    /// The line dragged to a point of the view: an upright one across, snapping to the text's edges and to the lines of other tables on the
    /// page (Alt not), one across down; the document shows it there as a step of its own once the view is idle (see PreviewLine).
    /// </summary>
    void LineDragTo(Point point, KeyModifiers modifiers)
    {
        var d = lineDrag!;
        d.At = point;
        var moved = (d.Line.Upright ? point.X - d.Start.X : point.Y - d.Start.Y) / factor / PxPerPt;
        if (!d.Moving && Math.Abs(moved) * factor * PxPerPt < DragDistance)
            return;
        d.Moving = true;
        if (d.Line.Upright && (modifiers & KeyModifiers.Alt) == 0)
        {
            var to = d.Line.At + moved;
            var near = SnapDistance / factor / PxPerPt;
            var edges = new List<double>();
            if (session?.Document.TextArea(d.Line.Page) is { } area)
                edges.AddRange(area.Columns.SelectMany(c => new[] { c.Left, c.Right }));
            edges.AddRange(Page(d.Line.Page).Items.OfType<TableRowItem>().Where(r => r.Table != d.Line.Table).SelectMany(r => r.Lines));
            if (edges.Where(e => Math.Abs(e - to) <= near).OrderBy(e => Math.Abs(e - to)).Select(e => (double?)e).FirstOrDefault() is { } edge)
                moved = edge - d.Line.At;
        }
        d.By = moved;
        if (!d.Scheduled)
        {
            d.Scheduled = true;
            Dispatcher.UIThread.Post(() =>
            {
                d.Scheduled = false;
                if (ReferenceEquals(lineDrag, d))
                    PreviewLine(d);
            }, DispatcherPriority.Background);
        }
        InvalidateVisual();
    }

    /// <summary>The line where the drag has it, as the document shows it: the last preview taken back, the move made from where it was, a step.</summary>
    void PreviewLine(LineDrag d)
    {
        if (session is not { } editing || d.Shown == d.By)
            return;
        Edit(() =>
        {
            if (d.Previewed)
            {
                editing.TakeBack();
                d.Previewed = false;
            }
            if (Math.Abs(d.By) < 0.05)
                return;
            d.Previewed = Stepped(editing, () => editing.ChangeTable(word =>
            {
                if (d.Line.Upright)
                    word.MoveLine(d.Line.Table, d.Line.Line, d.By, LeastColumn);
                else
                    word.SetRowHeight(d.Line.Table, d.Line.Line, Math.Max(0, d.Height + d.By - d.Extra));
                return null;
            }));
        });
        d.Shown = d.By;
    }

    /// <summary>
    /// The drag of a line ended: let go, where it shows (one step); taken back (Escape, the pointer lost). Pressed and let go without
    /// moving, a click there: the cursor at the place.
    /// </summary>
    void EndLineDrag(bool keep)
    {
        if (lineDrag is not { } d)
            return;
        if (keep && d.Moving)
            PreviewLine(d);
        lineDrag = null;
        Cursor = Text;
        if (!keep && d.Previewed && session is { } editing)
            Edit(() => editing.TakeBack());
        else if (keep && !d.Moving && PlaceAt(d.Start) is var (page, at, end))
        {
            (caret, lineEnd, anchor) = ((page, at), end, (page, at));
            Moved();
        }
        InvalidateVisual();
    }

    /// <summary>
    /// The line dragged: an accent line where it goes, over the table's rows on its page, and beside the pointer the width of the column
    /// left of it (right of the table's left side) or the row's height, in centimetres where the region measures so, else in inches.
    /// </summary>
    void DrawLineDrag(DrawingContext context)
    {
        if (lineDrag is not { Moving: true } d || rows.FirstOrDefault(r => r.Index == d.Line.Page) is not { } row)
            return;
        var parts = RowsOf(d.Line.Page, d.Line.Table);
        if (parts.Count == 0)
            return;
        var pt = factor * PxPerPt;
        var pen = new Pen(DrawingBrush, 1.5);
        var at = d.Line.At + d.By;
        var lines = parts[0].Lines;
        double length;
        if (d.Line.Upright)
        {
            var x = Left(row) + at * pt;
            context.DrawLine(pen, new Point(x, (row.Top / PxPerPt + parts.Min(p => p.Top)) * pt), new Point(x, (row.Top / PxPerPt + parts.Max(p => p.Bottom)) * pt));
            var k = d.Line.Line;
            length = k == 0 ? Math.Abs(lines[1] - at) : Math.Abs(at - lines[k - 1]);
        }
        else
        {
            var y = (row.Top / PxPerPt + at) * pt;
            context.DrawLine(pen, new Point(Left(row) + lines.Min() * pt, y), new Point(Left(row) + lines.Max() * pt, y));
            length = Math.Max(0, d.Height + d.By);
        }
        var shown = RegionInfo.CurrentRegion.IsMetric
            ? $"{(length / 72 * 2.54).ToString("0.0", CultureInfo.CurrentCulture)} cm"
            : $"{(length / 72).ToString("0.00", CultureInfo.CurrentCulture)} in";
        var surface = this.TryFindResource("TipSurface", ActualThemeVariant, out var s) ? s as IBrush : null;
        var ink = this.TryFindResource("TipInk", ActualThemeVariant, out var i) ? i as IBrush : null;
        var text = new FormattedText(shown, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("fonts:Inter#Inter"), FontStyle.Normal, FontWeight.SemiBold), 11.5, ink ?? Brushes.White);
        var box = new Rect(d.At.X + 16, d.At.Y - 28, text.Width + 16, text.Height + 8);
        context.DrawRectangle(surface ?? Brushes.Black, null, new RoundedRect(box, box.Height / 2));
        context.DrawText(text, new Point(box.X + 8, box.Y + 4));
    }
}
