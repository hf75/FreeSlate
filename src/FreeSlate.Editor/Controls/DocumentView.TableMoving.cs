using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using FreeSlate.Editing;
using FreeSlate.Rendering;

namespace FreeSlate.Editor.Controls;

/// <summary>
/// A table moved by its handle: a small square off its top left corner, beside its grips, shown while the pointer is at the table. A click on
/// it selects the whole table (its cells together, see EditSession.CellSelection). Dragged, a table floating beside the text follows the
/// pointer over the pages, the text going round it as it goes (a step of its own, taken back as it goes on, kept where it is let go, Escape
/// takes it back), lining up with the columns' edges and middles (Alt held, not), aligned there on a page of one column; a table in the text
/// goes between its paragraphs, a bar showing where, letting go puts it there.
/// </summary>
public sealed partial class DocumentView
{
    /// <summary>How large the handle is (pixels); its middle stands as far off the table's top left as the grips stand off its sides.</summary>
    const double TableHandleSize = 14;

    /// <summary>The handle's sign: a cross with heads at its ends, on its box.</summary>
    static readonly Geometry MoveSign = Geometry.Parse("M7,3.4 V10.6 M3.4,7 H10.6");
    static readonly Geometry MoveHeads = Geometry.Parse("M7,1.6 L5.2,3.8 H8.8 Z M7,12.4 L5.2,10.2 H8.8 Z M1.6,7 L3.8,5.2 V8.8 Z M12.4,7 L10.2,5.2 V8.8 Z");

    /// <summary>
    /// A table's handle pressed: the table, its page, where, the box of its rows there (view coordinates), whether it floats; once the
    /// pointer moves, where it goes (floating: a page, its top left there in points and an alignment across; in the text: the paragraph it
    /// goes before), the lines it lines up with, and whether a step shows it.
    /// </summary>
    sealed class TableDrag(int table, int page, Point start, IPointer pointer, Rect box, bool floating)
    {
        public int Table { get; } = table;
        public int Page { get; } = page;
        public Point Start { get; } = start;
        public IPointer Pointer { get; } = pointer;
        public Rect Box { get; } = box;
        public bool Floating { get; } = floating;
        public Point At { get; set; } = start;
        public bool Moving { get; set; }
        public (int Page, double X, double Y, string? Across)? Target { get; set; }
        public (int Page, double X, double Y, string? Across)? Shown { get; set; }
        public Rect Ghost { get; set; }
        public double? Guide { get; set; }
        public int? Before { get; set; }
        public Rect? Bar { get; set; }
        public bool Previewed { get; set; }
        public bool Scheduled { get; set; }
    }

    TableDrag? tableDrag;

    /// <summary>The handle of a table whose rows on a page stand in a box (view coordinates).</summary>
    static Rect TableHandle(Rect box) => new(box.Left - GripOff - TableHandleSize / 2, box.Top - GripOff - TableHandleSize / 2, TableHandleSize, TableHandleSize);

    /// <summary>The table whose handle a point of the view is on, with the page it is on there; null elsewhere.</summary>
    (int Table, int Page)? TableHandleAt(Point point)
    {
        if (session == null || shaping != null || cropping || tilting || OnPage(point) is not var (page, _, _))
            return null;
        foreach (var table in Page(page).Items.OfType<TableRowItem>().Select(r => r.Table).Distinct())
        {
            if (TableBox(page, table) is { } box && TableHandle(box).Inflate(2).Contains(point))
                return (table, page);
        }
        return null;
    }

    /// <summary>A press on a table's handle: a drag begins (or a click, see EndTableDrag). Whether there was one.</summary>
    bool TablePress(Point point, IPointer pointer)
    {
        if (TableHandleAt(point) is not var (table, page) || TableBox(page, table) is not { } box || session?.Document is not WordDocument word)
            return false;
        Focus();
        gripped = null;
        tableDrag = new TableDrag(table, page, point, pointer, box, word.TableFloat(table) != null);
        pointer.Capture(this);
        Cursor = Grab;
        return true;
    }

    /// <summary>
    /// The pointer moved while a table's handle is pressed: once far enough, floating, where the table goes (its box moved as far, lined up
    /// with a column's left, middle or right where near); in the text, the paragraph nearest the pointer that it would go before.
    /// </summary>
    void TableDragTo(Point point, KeyModifiers modifiers)
    {
        var d = tableDrag!;
        d.At = point;
        if (!d.Moving && ((Vector)(point - d.Start)).Length < DragDistance)
            return;
        d.Moving = true;
        if (RowNear(point.Y) is not { } row)
            return;
        var pt = factor * PxPerPt;
        var moved = d.Box.Translate(point - d.Start);
        if (!d.Floating)
        {
            (d.Before, d.Bar) = BetweenParagraphs(row, point, d.Table) is var (before, bar) ? (before, bar) : ((int?)null, (Rect?)null);
            d.Ghost = moved;
            InvalidateVisual();
            return;
        }
        var (x, y, w) = ((moved.X - Left(row)) / pt, moved.Y / pt - row.Top / PxPerPt, moved.Width / pt);
        string? across = null;
        d.Guide = null;
        if ((modifiers & KeyModifiers.Alt) == 0 && session?.Document.TextArea(row.Index) is var (columns, _, _))
        {
            var near = SnapDistance / pt;
            var lines = columns.SelectMany(c => new[] { (At: c.Left, Delta: c.Left - x, Side: "left"), (At: (c.Left + c.Right) / 2, Delta: (c.Left + c.Right) / 2 - (x + w / 2), Side: "center"),
                (At: c.Right, Delta: c.Right - (x + w), Side: "right") }).Where(c => Math.Abs(c.Delta) <= near).OrderBy(c => Math.Abs(c.Delta)).ToList();
            if (lines.Count > 0)
            {
                x += lines[0].Delta;
                d.Guide = lines[0].At;
                across = columns.Count == 1 ? lines[0].Side : null;
            }
        }
        d.Ghost = new Rect(Left(row) + x * pt, (row.Top / PxPerPt + y) * pt, moved.Width, moved.Height);
        d.Target = (row.Index, x, y, across);
        if (!d.Scheduled)
        {
            d.Scheduled = true;
            Dispatcher.UIThread.Post(() =>
            {
                d.Scheduled = false;
                if (ReferenceEquals(tableDrag, d))
                    PreviewTable(d);
            }, DispatcherPriority.Background);
        }
        InvalidateVisual();
    }

    /// <summary>
    /// The paragraph of the body a table dragged in the text would go before at a point of a page: of those that begin on it outside tables,
    /// the one whose top is nearest the pointer, not the one after the table already nor one right after another table (tables that meet
    /// join); with the bar that shows it, across the column at its top (view coordinates). Null where none is near.
    /// </summary>
    (int Paragraph, Rect Bar)? BetweenParagraphs(Row row, Point point, int table)
    {
        if (session?.Document is not WordDocument word)
            return null;
        var places = pictures.Places(Page(row.Index));
        var pt = factor * PxPerPt;
        var y = (point.Y / factor - row.Top) / PxPerPt;
        (int Paragraph, double Top, double Left)? best = null;
        foreach (var (toPage, line) in places.Lines)
        {
            if (line.Floating || Array.IndexOf(line.Offsets, 0) < 0 || word.TableOf(line.Block) != null || word.DrawingAround(line.Block) != null
                || word.TableBefore(line.Block) != null)
                continue;
            var top = toPage.MapPoint(0, (float)(line.Top * PxPerPt)).Y / PxPerPt;
            if (best == null || Math.Abs(top - y) < Math.Abs(best.Value.Top - y))
                best = (line.Block, top, line.Starts.DefaultIfEmpty(0).Min());
        }
        if (best is not var (paragraph, at, left) || session.Document.TextArea(row.Index) is not var (columns, _, _))
            return null;
        var column = columns.LastOrDefault(c => c.Left <= left + 1, columns[0]);
        return (paragraph, new Rect(Left(row) + column.Left * pt, (row.Top / PxPerPt + at) * pt - 1.5, (column.Right - column.Left) * pt, 3));
    }

    /// <summary>The floating table dragged put where it goes, as a step of its own in place of the one that showed it before.</summary>
    void PreviewTable(TableDrag d)
    {
        if (session is not { } editing || d.Target is not var (page, x, y, across) || d.Shown == d.Target)
            return;
        Edit(() =>
        {
            if (d.Previewed)
            {
                editing.TakeBack();
                d.Previewed = false;
            }
            d.Previewed = Stepped(editing, () => editing.PlaceTable(d.Table, page, x, y, across));
        });
        d.Shown = d.Target;
    }

    /// <summary>
    /// The drag of a table's handle ends: let go (<paramref name="keep"/>), a floating table stays where it went, one in the text goes where
    /// the bar was; else it stays as it was. A press that did not move was a click: the whole table selected.
    /// </summary>
    void EndTableDrag(bool keep)
    {
        if (tableDrag is not { } d)
            return;
        tableDrag = null;
        Cursor = Text;
        if (session is not { } editing)
            return;
        if (!d.Moving)
        {
            if (keep && editing.Document is WordDocument word && word.CellsOf(d.Table) is { } cells)
            {
                editing.Select(new CellRange(d.Table, 0, cells.Rows, 0, cells.Columns));
                Show(editing.Caret, editing.Anchor, reveal: false);
            }
        }
        else if (d.Floating)
        {
            if (keep)
                PreviewTable(d);
            else if (d.Previewed)
                Edit(() => editing.TakeBack());
        }
        else if (keep && d.Before is { } before)
            Edit(() => editing.MoveTable(d.Table, before));
        InvalidateVisual();
    }

    /// <summary>
    /// The handle of the table the pointer is at, beside its grips: faint, in the accent where the pointer is on it; while it is dragged, a
    /// floating table's frame where it goes with the line it lines up with, or for one in the text its outline at the pointer and the bar
    /// where it goes.
    /// </summary>
    void DrawTableHandle(DrawingContext context, Point? pointer)
    {
        var accent = DrawingBrush ?? CaretBrush ?? Brushes.Orange;
        if (tableDrag is { Moving: true } d)
        {
            var dashed = new Pen(accent, 1, new DashStyle([4, 3], 0));
            context.DrawRectangle(null, d.Floating ? new Pen(accent, 1.5) : dashed, d.Ghost.Inflate(1.5), 2, 2);
            if (d.Floating && d.Guide is { } x && d.Target is var (page, _, _, _) && rows.FirstOrDefault(r => r.Index == page) is { } row)
            {
                var sheet = SheetRect(row);
                var at = Math.Round(Left(row) + x * factor * PxPerPt) + 0.5;
                context.DrawLine(new Pen(accent, 1), new Point(at, sheet.Top), new Point(at, sheet.Bottom));
            }
            if (!d.Floating && d.Bar is { } bar)
                context.FillRectangle(accent, bar);
            return;
        }
        if (pointedTable is not { } table || pointer is not { } p || OnPage(p) is not var (pointedPage, _, _) || TableBox(pointedPage, table) is not { } box)
            return;
        var handle = TableHandle(box);
        var lit = TableHandleAt(p) is var (on, _) && on == table;
        var ink = lit ? accent : GuideBrush ?? Brushes.LightGray;
        context.DrawRectangle(Sheet ?? Brushes.White, new Pen(ink, 1.2), handle, 3, 3);
        using (context.PushTransform(Matrix.CreateTranslation(handle.X, handle.Y)))
        {
            context.DrawGeometry(null, new Pen(ink, 1.2), MoveSign);
            context.DrawGeometry(ink, null, MoveHeads);
        }
    }
}
