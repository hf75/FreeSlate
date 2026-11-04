using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using FreeSlate.Rendering;
using SkiaSharp;

namespace FreeSlate.Editor.Controls;

/// <summary>
/// A table shaped by hand: its cells' lines all show, the table framed; the pointer is a pen over its cells and an eraser over its
/// lines. A stroke across cells draws a line through them, from side to side of each, and splits them (at a line of the grid within
/// reach, else at a new one, the cells of other rows spanning it); a stroke along a line takes it away, the cells on either side
/// merging, into the smallest rectangle holding them where they would not make one. What a stroke does shows while it is drawn, the
/// line and the cells it touches lit; let go, it is one step, Escape takes it back first. Enter, Escape or a press outside the table
/// ends it; a double click on a line between cells, or the table's ring, begins it.
/// </summary>
public sealed partial class DocumentView
{
    /// <summary>The table shaped by hand, by its block's id; null while none is.</summary>
    int? shaping;

    /// <summary>Whether a table is shaped by hand (see BeginShape).</summary>
    public bool Shaping => shaping != null;

    /// <summary>
    /// A stroke while shaping: where it began, on which page, the line it began on (an eraser's, along it); once it has gone far enough,
    /// whether it erases and which way it goes.
    /// </summary>
    sealed class PenStroke(Point start, IPointer pointer, int page, TableLine? on)
    {
        public Point Start { get; } = start;
        public IPointer Pointer { get; } = pointer;
        public int Page { get; } = page;
        public TableLine? On { get; } = on;
        public Point At { get; set; } = start;
        public bool? Erasing { get; set; }
        public bool Upright { get; set; }
    }

    PenStroke? stroke;

    /// <summary>What a stroke does: the cells it splits along a line (where and how), or the line it erases and over what stretch.</summary>
    sealed record Shape(bool Erasing, bool Upright, int Line, double? Part, double? Height, List<TableCell> Cells, int From, int To, double At, double Along0, double Along1);

    static Cursor? pen, eraser;

    /// <summary>The pointer shaping a table: a pen, its tip at the hot spot; over a line an eraser (drawn for the screen, a cross where it cannot be).</summary>
    static Cursor ToolCursor(bool erasing)
    {
        try
        {
            return erasing ? eraser ??= Drawn(true) : pen ??= Drawn(false);
        }
        catch (Exception)
        {
            return new Cursor(StandardCursorType.Cross);
        }
        static Cursor Drawn(bool erasing)
        {
            var bitmap = new RenderTargetBitmap(new PixelSize(32, 32));
            using (var context = bitmap.CreateDrawingContext())
            {
                var ink = new Pen(Brushes.Black, 1.2) { LineJoin = PenLineJoin.Round };
                if (erasing)
                {
                    using (context.PushTransform(Matrix.CreateRotation(-38 * Math.PI / 180) * Matrix.CreateTranslation(9, 26)))
                    {
                        context.DrawRectangle(Brushes.White, ink, new Rect(-6, -20, 12, 21), 2.5, 2.5);
                        context.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xF4, 0xB4, 0x2A)), null, new Rect(-5.4, -5, 10.8, 5.4), 2, 2);
                        context.DrawLine(ink, new Point(-6, -5.4), new Point(6, -5.4));
                    }
                }
                else
                {
                    var body = Geometry.Parse("M3,29 L5.6,20.6 L17.6,8.6 L23.4,14.4 L11.4,26.4 Z");
                    context.DrawGeometry(Brushes.White, ink, body);
                    context.DrawGeometry(Brushes.Black, null, Geometry.Parse("M3,29 L5.6,20.6 L11.4,26.4 Z"));
                    context.DrawLine(ink, new Point(15.2, 11), new Point(21, 16.8));
                }
            }
            return new Cursor(bitmap, erasing ? new PixelPoint(6, 27) : new PixelPoint(3, 29));
        }
    }

    /// <summary>Begins shaping a table by hand (see the class); false where the document has no such table.</summary>
    public bool BeginShape(int table)
    {
        if (session?.Document is not WordDocument word || word.CellsOf(table) == null)
            return false;
        EndCrop();
        EndTilt();
        (shaping, stroke) = (table, null);
        Cursor = ToolCursor(false);
        InvalidateVisual();
        return true;
    }

    /// <summary>Ends shaping a table by hand.</summary>
    public void EndShape()
    {
        if (shaping == null)
            return;
        if (stroke is { } s)
            s.Pointer.Capture(null);
        (shaping, stroke) = (null, null);
        Cursor = Text;
        InvalidateVisual();
    }

    /// <summary>The page and its point (points) a point of the view is on, the page's row it is on.</summary>
    (int Page, double X, double Y)? OnPage(Point point)
    {
        if (rows.FirstOrDefault(r => point.Y / factor >= r.Top && point.Y / factor <= r.Top + r.Size.Height) is not { } row)
            return null;
        return (row.Index, (point.X - Left(row)) / factor / PxPerPt, (point.Y / factor - row.Top) / PxPerPt);
    }

    /// <summary>Whether a point of the view is on the table shaped, within its cells or on its lines.</summary>
    bool OnShaped(Point point)
    {
        if (shaping is not { } table || OnPage(point) is not var (page, x, y) || CellsOn(page, table) is not { } cells)
            return false;
        var reach = LineReach / factor / PxPerPt;
        var local = new SKPoint((float)(x * PxPerPt), (float)(y * PxPerPt));
        return CellAreas(page, cells).Any(c => SKRect.Inflate(c.Area, (float)(reach * PxPerPt), (float)(reach * PxPerPt)).Contains(local));
    }

    /// <summary>A press while shaping: on the table a stroke begins (true); elsewhere false, which ends shaping.</summary>
    bool ShapePress(Point point, IPointer pointer)
    {
        if (!OnShaped(point) || OnPage(point) is not var (page, _, _))
            return false;
        var on = TableLineAt(point) is { } line && line.Table == shaping ? line : null;
        stroke = new PenStroke(point, pointer, page, on);
        pointer.Capture(this);
        return true;
    }

    /// <summary>The stroke drawn on to a point: once it goes far enough, along the line it began on an eraser, else a pen the way it goes most.</summary>
    void StrokeTo(Point point)
    {
        var s = stroke!;
        s.At = point;
        var (dx, dy) = (point.X - s.Start.X, point.Y - s.Start.Y);
        if (s.Erasing == null && Math.Sqrt(dx * dx + dy * dy) >= 6)
        {
            var along = s.On is { } on && (on.Upright ? Math.Abs(dy) > 2 * Math.Abs(dx) : Math.Abs(dx) > 2 * Math.Abs(dy));
            s.Erasing = along;
            s.Upright = along ? s.On!.Upright : Math.Abs(dy) > Math.Abs(dx);
            Cursor = ToolCursor(along);
        }
        InvalidateVisual();
    }

    /// <summary>
    /// What a stroke does on its page (see Shape), from the table's rows and cells there; null where it does nothing (a line along the
    /// table's sides, a pen through no cell).
    /// </summary>
    Shape? ShapeOf(PenStroke s)
    {
        if (shaping is not { } table || s.Erasing is not { } erasing || CellsOn(s.Page, table) is not { } cells || RowsOf(s.Page, table) is not { Count: > 0 } placed
            || rows.FirstOrDefault(r => r.Index == s.Page) is not { } row)
            return null;
        var pt = factor * PxPerPt;
        (double X, double Y) Points(Point p) => ((p.X - Left(row)) / pt, (p.Y / factor - row.Top) / PxPerPt);
        var (x0, y0) = Points(s.Start);
        var (x1, y1) = Points(s.At);
        var lines = placed[0].Lines;
        var near = SnapDistance / pt;
        // The rows (grid columns) a stretch of the page goes over.
        List<int> RowsOver(double from, double to) =>
            [.. placed.Where(r => !r.Repeated && r.Bottom > Math.Min(from, to) && r.Top < Math.Max(from, to)).Select(r => r.Row).Distinct()];
        List<int> ColumnsOver(double from, double to) =>
            [.. Enumerable.Range(0, lines.Length - 1).Where(k => Math.Max(lines[k], lines[k + 1]) > Math.Min(from, to) && Math.Min(lines[k], lines[k + 1]) < Math.Max(from, to))];
        if (erasing && s.On is { } on)
        {
            if (on.Upright)
            {
                var over = RowsOver(y0, y1);
                if (over.Count == 0 || on.Line <= 0 || on.Line >= cells.Columns)
                    return null;
                var touched = cells.Cells.Where(c => c.Top <= over.Max() && c.Bottom > over.Min() && (c.Right == on.Line || c.Left == on.Line)).ToList();
                return new Shape(true, true, on.Line, null, null, touched, over.Min(), over.Max() + 1, lines[on.Line], Math.Min(y0, y1), Math.Max(y0, y1));
            }
            var columns = ColumnsOver(x0, x1);
            var below = on.Line + 1;
            if (columns.Count == 0 || below >= cells.Rows)
                return null;
            var near0 = cells.Cells.Where(c => c.Left <= columns.Max() && c.Right > columns.Min() && (c.Bottom == below || c.Top == below)).ToList();
            return new Shape(true, false, below, null, null, near0, columns.Min(), columns.Max() + 1, on.At, Math.Min(x0, x1), Math.Max(x0, x1));
        }
        if (s.Upright)
        {
            var over = RowsOver(y0, y1);
            if (over.Count == 0)
                return null;
            // A line of the grid within reach is drawn over again where it runs inside cells; else a new one at the pen's place.
            var existing = Enumerable.Range(1, Math.Max(0, lines.Length - 2)).Where(k => Math.Abs(lines[k] - x0) <= near).OrderBy(k => Math.Abs(lines[k] - x0)).Select(k => (int?)k).FirstOrDefault();
            var column = existing == null ? ColumnAt(s.Page, table, x0) : null;
            var crossed = cells.Cells.Where(c => c.Top <= over.Max() && c.Bottom > over.Min()
                && (existing is { } k ? c.Left < k && k < c.Right : column is { } g && c.Left <= g && g < c.Right)).ToList();
            if (crossed.Count == 0)
                return null;
            if (existing is { } line)
                return new Shape(false, true, line, null, null, crossed, 0, 0, lines[line], 0, 0);
            var part = Math.Abs(x0 - lines[column!.Value]);
            if (part < 3 || Math.Abs(lines[column.Value + 1] - x0) < 3)
                return null;
            return new Shape(false, true, column.Value, part, null, crossed, 0, 0, x0, 0, 0);
        }
        var across = ColumnsOver(x0, x1);
        if (across.Count == 0)
            return null;
        // A line between rows within reach where cells merged down run over it; else a new one at the pen's place in its row.
        var boundary = placed.Where(r => !r.Repeated && r.Row > 0 && Math.Abs(r.Top - y0) <= near).Select(r => (int?)r.Row).FirstOrDefault();
        if (boundary is { } b)
        {
            var spanning = cells.Cells.Where(c => c.Top < b && b < c.Bottom && c.Left <= across.Max() && c.Right > across.Min()).ToList();
            return spanning.Count == 0 ? null : new Shape(false, false, b, null, null, spanning, 0, 0, placed.First(r => r.Row == b).Top, 0, 0);
        }
        if (placed.FirstOrDefault(r => !r.Repeated && r.Top <= y0 && y0 < r.Bottom) is not { } inRow || y0 - inRow.Top < 3 || inRow.Bottom - y0 < 3)
            return null;
        var split = cells.Cells.Where(c => c.Top <= inRow.Row && inRow.Row < c.Bottom && c.Left <= across.Max() && c.Right > across.Min()).ToList();
        return split.Count == 0 ? null : new Shape(false, false, inRow.Row, y0 - inRow.Top, inRow.Bottom - inRow.Top, split, 0, 0, y0, 0, 0);
    }

    /// <summary>The stroke let go: what it does, one step; nothing where it does nothing.</summary>
    void EndStroke(bool keep)
    {
        if (stroke is not { } s)
            return;
        stroke = null;
        Cursor = ToolCursor(false);
        if (keep && shaping is { } table && ShapeOf(s) is { } shape && session is { } editing)
        {
            // Cells merged down are named by their first row; a pen across a row splits the cells there in that row.
            IReadOnlyList<(int Row, int Column)> Named(int? row = null) => [.. shape.Cells.Select(c => (row is { } r && c.Top <= r && r < c.Bottom ? r : c.Top, c.Left))];
            Edit(() => editing.ChangeTable(word => shape switch
            {
                { Erasing: true } => word.EraseLine(table, shape.Upright, shape.Line, shape.From, shape.To),
                { Upright: true } => word.SplitDown(table, Named(), shape.Line, shape.Part),
                _ => word.SplitAcross(table, Named(shape.Part != null ? shape.Line : null), shape.Line, shape.Part, shape.Height),
            }));
        }
        InvalidateVisual();
    }

    /// <summary>
    /// The table shaped, over the pages in view: framed in the accent, every line of its cells drawn; while a stroke is drawn, the line it
    /// draws or the stretch it erases in the accent and the cells it touches lit.
    /// </summary>
    void DrawShaping(DrawingContext context, IReadOnlyList<(Row Row, double Left)> visible)
    {
        if (shaping is not { } table)
            return;
        if (session?.Document is not WordDocument word || word.CellsOf(table) == null)
        {
            EndShape();
            return;
        }
        var lines = new Pen(GuideBrush is ISolidColorBrush { Color: var g } ? new SolidColorBrush(Color.FromArgb(255, (byte)(g.R * 0.7), (byte)(g.G * 0.7), (byte)(g.B * 0.7))) : Brushes.Gray, 1);
        var accent = DrawingBrush is ISolidColorBrush { Color: var a } ? a : Colors.Orange;
        var frame = new Pen(new SolidColorBrush(accent), 1.5);
        var glow = new SolidColorBrush(accent, 0.18);
        var shape = stroke is { } s ? ShapeOf(s) : null;
        foreach (var (row, left) in visible)
        {
            if (CellsOn(row.Index, table) is not { } cells)
                continue;
            var areas = CellAreas(row.Index, cells);
            if (areas.Count == 0)
                continue;
            Rect View(SKRect area) => new(left + area.Left * factor, (row.Top + area.Top) * factor, area.Width * factor, area.Height * factor);
            if (shape != null && stroke?.Page == row.Index)
            {
                foreach (var (cell, area) in areas.Where(c => shape.Cells.Contains(c.Cell)))
                    context.FillRectangle(glow, View(area));
            }
            foreach (var (_, area) in areas)
            {
                var r = View(area);
                context.DrawRectangle(null, lines, new Rect(Math.Round(r.X) + 0.5, Math.Round(r.Y) + 0.5, Math.Max(0, Math.Round(r.Width) - 1), Math.Max(0, Math.Round(r.Height) - 1)));
            }
            var whole = areas.Select(c => c.Area).Aggregate((p, q) => SKRect.Union(p, q));
            context.DrawRectangle(null, frame, View(whole).Inflate(3), 3, 3);
            if (shape == null || stroke?.Page != row.Index)
                continue;
            var pt = factor * PxPerPt;
            var touched = areas.Where(c => shape.Cells.Contains(c.Cell)).Select(c => View(c.Area)).ToList();
            if (touched.Count == 0)
                continue;
            if (shape.Erasing)
            {
                var marker = new Pen(new SolidColorBrush(accent, 0.45), 7) { LineCap = PenLineCap.Round };
                if (shape.Upright)
                {
                    var x = left + shape.At * pt;
                    context.DrawLine(marker, new Point(x, (row.Top / PxPerPt + shape.Along0) * pt), new Point(x, (row.Top / PxPerPt + shape.Along1) * pt));
                }
                else
                {
                    var y = (row.Top / PxPerPt + shape.At) * pt;
                    context.DrawLine(marker, new Point(left + shape.Along0 * pt, y), new Point(left + shape.Along1 * pt, y));
                }
            }
            else
            {
                var drawn = new Pen(new SolidColorBrush(accent), 2) { LineCap = PenLineCap.Round };
                if (shape.Upright)
                {
                    var x = left + shape.At * pt;
                    context.DrawLine(drawn, new Point(x, touched.Min(t => t.Top)), new Point(x, touched.Max(t => t.Bottom)));
                }
                else
                {
                    var y = (row.Top / PxPerPt + shape.At) * pt;
                    context.DrawLine(drawn, new Point(touched.Min(t => t.Left), y), new Point(touched.Max(t => t.Right), y));
                }
            }
        }
    }
}
