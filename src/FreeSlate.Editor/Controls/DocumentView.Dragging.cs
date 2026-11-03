using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using FreeSlate.Editing;
using SkiaSharp;

namespace FreeSlate.Editor.Controls;

/// <summary>
/// A drawing dragged with the main button. One in the line goes through the text: a bar shows where it would go, letting go puts it
/// there. A floating one follows the pointer over the pages, the text going round it as it goes (a step of its own, taken back as it
/// goes on, kept where it is let go, Escape takes it back); its anchor goes to the paragraph under its top, which a mark in the margin
/// shows. Near the columns' edges and middles and the margins it lines up with them, a line along each it lines up with (Alt held,
/// not); at a column's edge or middle of a page of one column it stays aligned there.
/// </summary>
public sealed partial class DocumentView
{
    /// <summary>How far the pointer goes before a press on a drawing drags it, and how near an edge it lines up with it, in pixels.</summary>
    const double DragDistance = 4, SnapDistance = 6;

    static readonly Cursor Grab = new(StandardCursorType.SizeAll);

    /// <summary>An anchor, the mark of a floating drawing's paragraph: its outline on a box of 10 × 12, a ring above (see DrawAnchor).</summary>
    static readonly Geometry AnchorSign = Geometry.Parse("M5,3.4 V11.4 M2.6,5.6 H7.4 M1,8 Q1.3,11.4 5,11.4 Q8.7,11.4 9,8");

    /// <summary>
    /// A drawing pressed: where, its page, its box then in the view's coordinates, whether it floats; once it moves, where its box
    /// goes (a page and its top left there in points, an alignment across, the lines it lines up with), and whether a step shows it there.
    /// </summary>
    sealed class Dragged(Point start, IPointer pointer, int page, Rect box, bool floating)
    {
        public Point Start { get; } = start;
        public IPointer Pointer { get; } = pointer;
        public int Page { get; } = page;
        public Rect Box { get; } = box;
        public bool Floating { get; } = floating;
        public bool Moving { get; set; }
        public (int Page, double X, double Y, string? Across)? Target { get; set; }
        public (int Page, double X, double Y, string? Across)? Shown { get; set; }
        public Rect Ghost { get; set; }
        public (double? X, double? Y) Guides { get; set; }
        public bool Previewed { get; set; }
        public bool Scheduled { get; set; }
    }

    Dragged? dragged;

    /// <summary>Whether a drawing is being dragged over the sheets.</summary>
    public bool DraggingDrawing => dragged is { Moving: true };

    /// <summary>A press on the drawing just selected: it may be dragged from there.</summary>
    void BeginDrag(Point point, IPointer pointer)
    {
        if (SelectedDrawing() is not var (page, at) || BoxOf(page, at) is not { } box)
            return;
        var floating = pictures.Places(Page(page)).InlineBox(pictures.Spots(Page(page))[at]) == null;
        dragged = new Dragged(point, pointer, page, box, floating);
        pointer.Capture(this);
    }

    /// <summary>The pointer moved while a drawing is pressed: once far enough, where the drawing goes.</summary>
    void DragTo(Point point, KeyModifiers modifiers)
    {
        var d = dragged!;
        if (!d.Moving && ((Vector)(point - d.Start)).Length < DragDistance)
            return;
        d.Moving = true;
        Cursor = d.Floating ? Grab : Cursor.Default;
        if (!d.Floating)
        {
            d.Ghost = d.Box.Translate(point - d.Start);
            DropAt(PlaceAt(point));
            InvalidateVisual();
            return;
        }
        if (RowNear(point.Y) is not { } row)
            return;
        var moved = d.Box.Translate(point - d.Start);
        var pt = factor * PxPerPt;
        var (x, y, w, h) = ((moved.X - Left(row)) / pt, moved.Y / pt - row.Top / PxPerPt, moved.Width / pt, moved.Height / pt);
        string? across = null;
        (double? X, double? Y) guides = (null, null);
        if ((modifiers & KeyModifiers.Alt) == 0 && session?.Document.TextArea(row.Index) is var (columns, top, bottom))
        {
            var near = SnapDistance / pt;
            // Across: a column's left, middle or right, the nearest of them; down: the margins and the middle between them.
            var lines = columns.SelectMany(c => new[] { (At: c.Left, Delta: c.Left - x, Side: "left"), (At: (c.Left + c.Right) / 2, Delta: (c.Left + c.Right) / 2 - (x + w / 2), Side: "center"),
                (At: c.Right, Delta: c.Right - (x + w), Side: "right") }).Where(c => Math.Abs(c.Delta) <= near).OrderBy(c => Math.Abs(c.Delta)).ToList();
            if (lines.Count > 0)
            {
                x += lines[0].Delta;
                guides.X = lines[0].At;
                across = columns.Count == 1 ? lines[0].Side : null;
            }
            var downs = new[] { (At: top, Delta: top - y), (At: (top + bottom) / 2, Delta: (top + bottom) / 2 - (y + h / 2)), (At: bottom, Delta: bottom - (y + h)) }
                .Where(r => Math.Abs(r.Delta) <= near).OrderBy(r => Math.Abs(r.Delta)).ToList();
            if (downs.Count > 0)
            {
                y += downs[0].Delta;
                guides.Y = downs[0].At;
            }
        }
        d.Target = (row.Index, x, y, across);
        d.Guides = guides;
        d.Ghost = new Rect(Left(row) + x * pt, (row.Top / PxPerPt + y) * pt, moved.Width, moved.Height);
        // The text goes round it as soon as the pointer rests a moment (moves that come first go before).
        if (!d.Scheduled)
        {
            d.Scheduled = true;
            Dispatcher.UIThread.Post(() =>
            {
                d.Scheduled = false;
                if (ReferenceEquals(dragged, d))
                    Preview(d);
            }, DispatcherPriority.Background);
        }
        InvalidateVisual();
    }

    /// <summary>The floating drawing dragged put where it goes, as a step of its own in place of the one that showed it before.</summary>
    void Preview(Dragged d)
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
            try
            {
                d.Previewed = editing.PlaceDrawing(page, x, y, across);
            }
            catch (Exception e) when (e is NotSupportedException or ArgumentException)
            {
                // It stays where it was.
            }
        });
        d.Shown = d.Target;
    }

    /// <summary>
    /// The drag ends: let go (<paramref name="keep"/>), the drawing stays where it went (in the line, it goes where the bar was); else
    /// it goes back. A press that did not move was a click.
    /// </summary>
    void EndDrag(bool keep)
    {
        if (dragged is not { } d)
            return;
        dragged = null;
        Cursor = Text;
        if (d.Moving && d.Floating)
        {
            if (keep)
                Preview(d);
            else if (d.Previewed && session is { } editing)
                Edit(() => editing.TakeBack());
        }
        else if (d.Moving)
        {
            var to = dropAt;
            DropAt(null);
            if (keep && to is var (page, at, _) && session is { } editing && SessionPlace(page, at) is { } place)
                Edit(() => editing.MoveDrawing(place));
        }
        InvalidateVisual();
    }

    /// <summary>Where a place of the view stands in the text (see Moved): a paragraph and an offset in it.</summary>
    TextPlace? SessionPlace(int page, int at)
    {
        if (page >= scenes.Count || pictures.Spots(Page(page)) is not { Count: > 0 } spots)
            return null;
        var (spot, after) = SpotAt(spots, at, false);
        var (block, offset) = pictures.Places(Page(page)).PlaceAt(spot, after);
        return new TextPlace(block, offset);
    }

    /// <summary>The row a height of the view is on, else the nearest.</summary>
    Row? RowNear(double y) => rows.Count == 0 ? null
        : rows.MinBy(r => y / factor < r.Top ? r.Top - y / factor : y / factor > r.Top + r.Size.Height ? y / factor - r.Top - r.Size.Height : 0);

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (dragged is { } d && ReferenceEquals(e.Pointer, d.Pointer))
            EndDrag(keep: false);
    }

    /// <summary>
    /// The drawing selected as a whole, framed a little outside its box; while it is dragged floating, the frame where it goes and the
    /// lines it lines up with; floating, the mark of its anchor's paragraph in the margin.
    /// </summary>
    void DrawDrawing(DrawingContext context)
    {
        var pen = new Pen(DrawingBrush, 1.5);
        if (dragged is { Moving: true, Floating: true } d)
        {
            context.DrawRectangle(null, pen, d.Ghost.Inflate(1.5), 2, 2);
            if (rows.FirstOrDefault(r => r.Index == d.Target?.Page) is { } row)
            {
                var sheet = SheetRect(row);
                var guide = new Pen(DrawingBrush, 1);
                var pt = factor * PxPerPt;
                if (d.Guides.X is { } x)
                    context.DrawLine(guide, new Point(Math.Round(Left(row) + x * pt) + 0.5, sheet.Top), new Point(Math.Round(Left(row) + x * pt) + 0.5, sheet.Bottom));
                if (d.Guides.Y is { } y)
                    context.DrawLine(guide, new Point(sheet.Left, Math.Round(sheet.Top + y * pt) + 0.5), new Point(sheet.Right, Math.Round(sheet.Top + y * pt) + 0.5));
            }
        }
        else if (!SelectionHidden && SelectedDrawing() is var (page, at) && BoxOf(page, at) is { } box)
        {
            context.DrawRectangle(null, pen, box.Inflate(1.5), 2, 2);
            // In the line, dragged: its outline with the pointer, the bar where it goes (see Render).
            if (dragged is { Moving: true } inLine)
                context.DrawRectangle(null, new Pen(DrawingBrush, 1, new DashStyle([4, 3], 0)), inLine.Ghost, 2, 2);
        }
        if (SelectedDrawing() is var (anchorPage, anchorAt) && anchorPage < rows.Count)
            DrawAnchor(context, anchorPage, anchorAt);
    }

    /// <summary>A floating drawing's anchor: a mark in the margin left of the line its character stands in.</summary>
    void DrawAnchor(DrawingContext context, int page, int at)
    {
        var places = pictures.Places(Page(page));
        var spots = pictures.Spots(Page(page));
        if (at >= spots.Count || places.InlineBox(spots[at]) != null)
            return;
        var (toPage, line) = places.Lines[spots[at].Line];
        var left = line.Starts.Concat(line.Ends).DefaultIfEmpty(0).Min();
        var top = toPage.MapPoint((float)(left * PxPerPt), (float)(line.Top * PxPerPt));
        var bottom = toPage.MapPoint((float)(left * PxPerPt), (float)(line.Bottom * PxPerPt));
        var row = rows[page];
        var (x, y) = (Left(row) + top.X * factor - 18, (row.Top + (top.Y + bottom.Y) / 2) * factor - 6);
        var pen = new Pen(DrawingBrush, 1.2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        using (context.PushTransform(Matrix.CreateTranslation(Math.Round(x), Math.Round(y))))
        {
            context.DrawEllipse(null, pen, new Point(5, 1.9), 1.5, 1.5);
            context.DrawGeometry(null, pen, AnchorSign);
        }
    }
}
