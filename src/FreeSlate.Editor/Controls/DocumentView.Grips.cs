using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using FreeSlate.Editing;
using FreeSlate.Rendering;

namespace FreeSlate.Editor.Controls;

/// <summary>
/// The grips of a table's rows and columns: beside the table the pointer is at, left of the row it is at and above the column, small
/// and faint, in the accent where the pointer is on one, with a plus between two of them. A click on a grip selects the row or column
/// (Shift another one up to it), Delete or Backspace then takes them away; dragged along the table it moves them there (rows and
/// columns of their own, as cells merged across them cannot be parted), dragged out of the table it takes them away, each one step.
/// A click on a plus puts a row or a column in right there.
/// </summary>
public sealed partial class DocumentView
{
    /// <summary>How far beside a table its grips stand (pixels), how near a line between two the pointer brings a plus, and how far out a grip goes away.</summary>
    const double GripOff = 11, GripZone = 22, PlusReach = 5, OutReach = 46;

    /// <summary>
    /// A grip of a table as a page shows it: the table, whether a row's (else a column's), the row or grid column (for a plus, the one it
    /// puts the new one before), the page, and whether it is a plus between two.
    /// </summary>
    sealed record TableGrip(int Table, bool Row, int Index, int Page, bool Plus);

    /// <summary>The rows or columns of a table selected by their grips: the table, whether rows, and from which to before which.</summary>
    sealed record GripSelection(int Table, bool Rows, int From, int To);

    GripSelection? gripped;

    /// <summary>A grip pressed: which, where; once it moves, where it goes (the row or column before which, null for none), whether out.</summary>
    sealed class GripDrag(TableGrip grip, Point start, IPointer pointer, GripSelection moved)
    {
        public TableGrip Grip { get; } = grip;
        public Point Start { get; } = start;
        public IPointer Pointer { get; } = pointer;
        public GripSelection Moved { get; } = moved;
        public Point At { get; set; } = start;
        public bool Moving { get; set; }
        public int? Before { get; set; }
        public bool Out { get; set; }
    }

    GripDrag? gripDrag;

    /// <summary>The table the pointer is at, by its block's id, whose grips show (see DrawGrips), and where the pointer is over the view.</summary>
    int? pointedTable;

    Point? pointerAt;

    /// <summary>The box of a table's rows on a page, in the view's coordinates; null where the page shows none of them.</summary>
    Rect? TableBox(int page, int table)
    {
        if (rows.FirstOrDefault(r => r.Index == page) is not { } row || RowsOf(page, table) is not { Count: > 0 } placed)
            return null;
        var pt = factor * PxPerPt;
        var lines = placed[0].Lines;
        return new Rect(new Point(Left(row) + lines.Min() * pt, (row.Top / PxPerPt + placed.Min(p => p.Top)) * pt),
            new Point(Left(row) + lines.Max() * pt, (row.Top / PxPerPt + placed.Max(p => p.Bottom)) * pt));
    }

    /// <summary>
    /// The grip of a table a point of the view is on: left of the table a row's (a plus where two rows meet, or below the last), above it a
    /// column's (a plus at a line of the grid between two, or right of the last); null elsewhere.
    /// </summary>
    TableGrip? GripAt(Point point)
    {
        if (session == null || shaping != null || cropping || tilting || OnPage(point) is not var (page, _, _) || rows.FirstOrDefault(r => r.Index == page) is not { } row)
            return null;
        var pt = factor * PxPerPt;
        foreach (var table in Page(page).Items.OfType<TableRowItem>().Select(r => r.Table).Distinct())
        {
            if (TableBox(page, table) is not { } box || CellsOn(page, table) is not { } cells)
                continue;
            var placed = RowsOf(page, table).Where(r => !r.Repeated).ToList();
            if (placed.Count == 0)
                continue;
            if (point.X >= box.Left - GripZone && point.X < box.Left - LineReach - 1 && point.Y >= box.Top - PlusReach && point.Y <= box.Bottom + PlusReach)
            {
                foreach (var part in placed)
                {
                    var bottom = (row.Top / PxPerPt + part.Bottom) * pt;
                    if (Math.Abs(point.Y - bottom) <= PlusReach && cells.Cells.Any(c => c.Bottom == part.Row + 1))
                        return new TableGrip(table, true, part.Row + 1, page, true);
                }
                if (placed.FirstOrDefault(p => point.Y >= (row.Top / PxPerPt + p.Top) * pt && point.Y < (row.Top / PxPerPt + p.Bottom) * pt) is { } at)
                    return new TableGrip(table, true, at.Row, page, false);
            }
            if (point.Y >= box.Top - GripZone && point.Y < box.Top - LineReach - 1 && point.X >= box.Left - PlusReach && point.X <= box.Right + PlusReach)
            {
                var lines = placed[0].Lines;
                for (var k = 1; k < lines.Length; k++)
                {
                    if (Math.Abs(point.X - (Left(row) + lines[k] * pt)) <= PlusReach)
                        return new TableGrip(table, false, k, page, true);
                }
                if (ColumnAt(page, table, (point.X - Left(row)) / pt) is { } column)
                    return new TableGrip(table, false, column, page, false);
            }
        }
        return null;
    }

    /// <summary>The rows (columns) a grip takes with it: its own, all the selected ones where it is among them; widened to those cells merged over them reach.</summary>
    GripSelection Taken(TableGrip grip)
    {
        if (gripped is { } selected && selected.Table == grip.Table && selected.Rows == grip.Row && grip.Index >= selected.From && grip.Index < selected.To)
            return selected;
        var (from, to) = (grip.Index, grip.Index + 1);
        if (CellsOn(grip.Page, grip.Table) is { } cells)
        {
            for (var grown = true; grown;)
            {
                grown = false;
                foreach (var c in cells.Cells)
                {
                    var (a, b) = grip.Row ? (c.Top, c.Bottom) : (c.Left, c.Right);
                    if (a < to && b > from && (a < from || b > to))
                        (from, to, grown) = (Math.Min(from, a), Math.Max(to, b), true);
                }
            }
        }
        return new GripSelection(grip.Table, grip.Row, from, to);
    }

    /// <summary>A press on a grip: a plus puts a row or column in there; a grip begins a drag (or a click: see EndGripDrag). Whether there was one.</summary>
    bool GripPress(Point point, IPointer pointer, KeyModifiers modifiers)
    {
        if (GripAt(point) is not { } grip)
            return false;
        Focus();
        if (grip.Plus)
        {
            gripped = null;
            if (session is { } editing)
                Edit(() => editing.ChangeTable(word => grip.Row
                    ? word.AddRows(grip.Table, grip.Index, 1, like: Math.Max(0, grip.Index - 1))
                    : word.AddColumns(grip.Table, grip.Index, 1, like: Math.Max(0, grip.Index - 1))));
            return true;
        }
        if ((modifiers & KeyModifiers.Shift) != 0 && gripped is { } selected && selected.Table == grip.Table && selected.Rows == grip.Row)
        {
            gripped = selected with { From = Math.Min(selected.From, grip.Index), To = Math.Max(selected.To, grip.Index + 1) };
            SelectBand(gripped, grip.Page);
            InvalidateVisual();
            return true;
        }
        gripDrag = new GripDrag(grip, point, pointer, Taken(grip));
        pointer.Capture(this);
        Cursor = Grab;
        return true;
    }

    /// <summary>
    /// A grip dragged to a point: along the table the row or column goes before the one there (between rows or columns of their own),
    /// out of the table (beside it, or above it for a column) it goes away.
    /// </summary>
    void GripDragTo(Point point)
    {
        var d = gripDrag!;
        d.At = point;
        if (!d.Moving && Math.Abs(point.X - d.Start.X) + Math.Abs(point.Y - d.Start.Y) < DragDistance)
            return;
        d.Moving = true;
        d.Before = null;
        if (TableBox(d.Grip.Page, d.Grip.Table) is not { } box || rows.FirstOrDefault(r => r.Index == d.Grip.Page) is not { } row || CellsOn(d.Grip.Page, d.Grip.Table) is not { } cells)
            return;
        d.Out = d.Grip.Row ? point.X < box.Left - OutReach || point.X > box.Right + OutReach : point.Y < box.Top - OutReach || point.Y > box.Bottom + OutReach;
        if (d.Out)
        {
            InvalidateVisual();
            return;
        }
        var pt = factor * PxPerPt;
        // The line between two rows (columns) of their own nearest to the pointer: where they would go.
        bool Free(int line) => !cells.Cells.Any(c => d.Grip.Row ? c.Top < line && line < c.Bottom : c.Left < line && line < c.Right);
        var placed = RowsOf(d.Grip.Page, d.Grip.Table).Where(r => !r.Repeated).ToList();
        var lines = d.Grip.Row
            ? placed.Select(p => (Line: p.Row, At: (row.Top / PxPerPt + p.Top) * pt)).Append((Line: placed[^1].Row + 1, At: (row.Top / PxPerPt + placed[^1].Bottom) * pt))
            : placed[0].Lines.Select((x, k) => (Line: k, At: Left(row) + x * pt));
        var best = lines.Where(l => Free(l.Line)).Select(l => (l.Line, Away: Math.Abs((d.Grip.Row ? point.Y : point.X) - l.At))).OrderBy(l => l.Away).Select(l => (int?)l.Line).FirstOrDefault();
        if (best is { } before && (before < d.Moved.From || before > d.Moved.To))
            d.Before = before;
        InvalidateVisual();
    }

    /// <summary>
    /// The drag of a grip ended: let go out of the table, its rows or columns taken away; along it, moved where it shows; without moving, a
    /// click, which selects them (Escape and a lost pointer take it back).
    /// </summary>
    void EndGripDrag(bool keep)
    {
        if (gripDrag is not { } d)
            return;
        gripDrag = null;
        Cursor = Text;
        var moved = d.Moved;
        if (keep && session is { } editing)
        {
            if (!d.Moving)
            {
                gripped = moved;
                SelectBand(moved, d.Grip.Page);
            }
            else if (d.Out)
            {
                gripped = null;
                Edit(() => editing.ChangeTable(word => moved.Rows ? word.RemoveRows(moved.Table, moved.From, moved.To) : word.RemoveColumns(moved.Table, moved.From, moved.To)));
            }
            else if (d.Before is { } before)
            {
                gripped = null;
                Edit(() => editing.ChangeTable(word => moved.Rows ? word.MoveRows(moved.Table, moved.From, moved.To, before) : word.MoveColumns(moved.Table, moved.From, moved.To, before)));
            }
        }
        InvalidateVisual();
    }

    /// <summary>
    /// The cells of rows or columns selected by their grips selected together too (see EditSession.CellSelection), from the first one's
    /// start to the last one's end, so that the ring acts on them (their colour, their lines); the band shows them.
    /// </summary>
    void SelectBand(GripSelection band, int page)
    {
        if (session is not { } editing || CellsOn(page, band.Table) is not { } cells)
            return;
        var inside = cells.Cells.Where(c => band.Rows ? c.Top >= band.From && c.Bottom <= band.To : c.Left >= band.From && c.Right <= band.To).ToList();
        if (inside.Count == 0)
            return;
        editing.Place(new TextPlace(inside[0].First, 0));
        editing.Place(new TextPlace(inside[^1].Last, editing.Document.Text(inside[^1].Last).Length), extend: true);
        Show(editing.Caret, editing.Anchor, reveal: false);
    }

    /// <summary>The rows or columns selected by their grips taken away (Delete or Backspace); whether there were any.</summary>
    bool RemoveGripped()
    {
        if (gripped is not { } selected || session is not { } editing)
            return false;
        gripped = null;
        Edit(() => editing.ChangeTable(word => selected.Rows ? word.RemoveRows(selected.Table, selected.From, selected.To) : word.RemoveColumns(selected.Table, selected.From, selected.To)));
        return true;
    }

    /// <summary>The table the pointer is at, or over the grips beside it, its grips then shown.</summary>
    void PointAtTable(Point point)
    {
        int? table = null;
        if (OnPage(point) is var (page, _, _))
        {
            foreach (var id in Page(page).Items.OfType<TableRowItem>().Select(r => r.Table).Distinct())
            {
                if (TableBox(page, id) is { } box && new Rect(box.Left - GripZone, box.Top - GripZone, box.Width + GripZone, box.Height + GripZone).Contains(point))
                    table = id;
            }
        }
        if (table != pointedTable)
        {
            pointedTable = table;
            InvalidateVisual();
        }
        else if (table != null)
            InvalidateVisual();
    }

    /// <summary>
    /// The grips of the table the pointer is at (see the class): the row's left of it and the column's above, faint; the one the pointer is
    /// on in the accent, a plus where it is between two; the rows or columns selected tinted; while one is dragged, a bar where it would go,
    /// or out of the table its rows tinted as going.
    /// </summary>
    void DrawGrips(DrawingContext context, IReadOnlyList<(Row Row, double Left)> visible, Point? pointer)
    {
        var accent = DrawingBrush is ISolidColorBrush { Color: var a } ? a : Colors.Orange;
        var faint = GuideBrush ?? Brushes.LightGray;
        if (gripped is { } selected)
            DrawBand(context, visible, selected, new SolidColorBrush(accent, 0.16));
        if (gripDrag is { Moving: true } d)
        {
            if (d.Out)
                DrawBand(context, visible, d.Moved, new SolidColorBrush(Color.FromRgb(0xD4, 0x72, 0x10), 0.22));
            else
            {
                DrawBand(context, visible, d.Moved, new SolidColorBrush(accent, 0.16));
                if (d.Before is { } before && TableBox(d.Grip.Page, d.Grip.Table) is { } box && rows.FirstOrDefault(r => r.Index == d.Grip.Page) is { } row)
                {
                    var pt = factor * PxPerPt;
                    var placed = RowsOf(d.Grip.Page, d.Grip.Table).Where(r => !r.Repeated).ToList();
                    var bar = new Pen(new SolidColorBrush(accent), 2.5);
                    if (d.Grip.Row)
                    {
                        var y = placed.FirstOrDefault(p => p.Row == before) is { } at ? (row.Top / PxPerPt + at.Top) * pt : (row.Top / PxPerPt + placed[^1].Bottom) * pt;
                        context.DrawLine(bar, new Point(box.Left - 4, y), new Point(box.Right + 4, y));
                    }
                    else
                    {
                        var x = Left(row) + placed[0].Lines[Math.Min(before, placed[0].Lines.Length - 1)] * pt;
                        context.DrawLine(bar, new Point(x, box.Top - 4), new Point(x, box.Bottom + 4));
                    }
                }
            }
            return;
        }
        if (pointedTable is not { } table || pointer is not { } p || OnPage(p) is not var (page, _, _) || TableBox(page, table) is not { } frame
            || rows.FirstOrDefault(r => r.Index == page) is not { } at2)
            return;
        var on = GripAt(p) is { Table: var t } grip && t == table ? grip : null;
        var scale = factor * PxPerPt;
        var placedRows = RowsOf(page, table).Where(r => !r.Repeated).ToList();
        // The row's grip at the pointer's height, the column's at its place across (where the pointer is over the table or beside it).
        var rowAt = placedRows.FirstOrDefault(r => p.Y >= (at2.Top / PxPerPt + r.Top) * scale && p.Y < (at2.Top / PxPerPt + r.Bottom) * scale);
        if (rowAt != null)
        {
            var (top, bottom) = ((at2.Top / PxPerPt + rowAt.Top) * scale, (at2.Top / PxPerPt + rowAt.Bottom) * scale);
            var lit = on is { Row: true, Plus: false } && on.Index == rowAt.Row;
            context.DrawRectangle(lit ? new SolidColorBrush(accent) : faint, null, new Rect(frame.Left - GripOff - 2.5, top + (bottom - top) / 2 - 7, 5, 14), 2.5, 2.5);
        }
        var columnAt = ColumnAt(page, table, (p.X - Left(at2)) / scale);
        if (columnAt is { } k && placedRows.Count > 0 && k + 1 < placedRows[0].Lines.Length)
        {
            var (left, right) = (Left(at2) + Math.Min(placedRows[0].Lines[k], placedRows[0].Lines[k + 1]) * scale, Left(at2) + Math.Max(placedRows[0].Lines[k], placedRows[0].Lines[k + 1]) * scale);
            var lit = on is { Row: false, Plus: false } && on.Index == k;
            context.DrawRectangle(lit ? new SolidColorBrush(accent) : faint, null, new Rect(left + (right - left) / 2 - 7, frame.Top - GripOff - 2.5, 14, 5), 2.5, 2.5);
        }
        if (on is { Plus: true })
        {
            var centre = on.Row
                ? new Point(frame.Left - GripOff, placedRows.FirstOrDefault(r => r.Row == on.Index) is { } next
                    ? (at2.Top / PxPerPt + next.Top) * scale : (at2.Top / PxPerPt + placedRows[^1].Bottom) * scale)
                : new Point(Left(at2) + placedRows[0].Lines[on.Index] * scale, frame.Top - GripOff);
            var ring = new Pen(new SolidColorBrush(accent), 1.4);
            context.DrawEllipse(Sheet ?? Brushes.White, ring, centre, 7, 7);
            var ink = new Pen(CaretBrush ?? Brushes.Black, 1.4);
            context.DrawLine(ink, centre + new Vector(-3.5, 0), centre + new Vector(3.5, 0));
            context.DrawLine(ink, centre + new Vector(0, -3.5), centre + new Vector(0, 3.5));
        }
    }

    /// <summary>The rows or columns of a table tinted, over the cells that lie in them, on the pages in view.</summary>
    void DrawBand(DrawingContext context, IReadOnlyList<(Row Row, double Left)> visible, GripSelection band, IBrush brush)
    {
        foreach (var (row, _) in visible)
        {
            if (session == null || CellsOn(row.Index, band.Table) is not { } cells)
                continue;
            foreach (var (cell, area) in CellAreas(row.Index, cells))
            {
                var (a, b) = band.Rows ? (cell.Top, cell.Bottom) : (cell.Left, cell.Right);
                if (a >= band.From && b <= band.To)
                    context.FillRectangle(brush, new Rect(Left(row) + area.Left * factor, (row.Top + area.Top) * factor, area.Width * factor, area.Height * factor));
            }
        }
    }
}
