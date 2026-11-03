using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using FreeSlate.Editing;

namespace FreeSlate.Editor.Controls;

/// <summary>
/// A drawing dragged with the main button. One in the line goes through the text: a bar shows where it would go, letting go puts it
/// there. A floating one follows the pointer over the pages, the text going round it as it goes (a step of its own, taken back as it
/// goes on, kept where it is let go, Escape takes it back); its anchor goes to the paragraph under its top, which a mark in the margin
/// shows. Near the columns' edges and middles and the margins it lines up with them, a line along each it lines up with (Alt held,
/// not); at a column's edge or middle of a page of one column it stays aligned there. The handles at the corners and the middles of
/// the sides of the drawing selected size it the same way: a corner keeps its proportions, a side stretches it; the opposite side
/// stays, an alignment there too, and a side it moves lines up with the columns' edges and the margins.
/// </summary>
public sealed partial class DocumentView
{
    /// <summary>How far the pointer goes before a press on a drawing drags it, and how near an edge it lines up with it, in pixels.</summary>
    const double DragDistance = 4, SnapDistance = 6;

    /// <summary>A handle's radius, how near the pointer is to take it, and the size a drawing keeps at least, in pixels.</summary>
    const double HandleRadius = 4, HandleReach = 7, Least = 8;

    static readonly Cursor Grab = new(StandardCursorType.SizeAll);

    /// <summary>The handles: the corners and the middles of the sides, as which way each moves (−1 left or up, 1 right or down, 0 neither).</summary>
    static readonly (int Across, int Down)[] Handles = [(-1, -1), (0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0)];

    /// <summary>An anchor, the mark of a floating drawing's paragraph: its outline on a box of 10 × 12, a ring above (see DrawAnchor).</summary>
    static readonly Geometry AnchorSign = Geometry.Parse("M5,3.4 V11.4 M2.6,5.6 H7.4 M1,8 Q1.3,11.4 5,11.4 Q8.7,11.4 9,8");

    /// <summary>Where a drawing dragged goes: a page, its box's top left there and its size in points, and the alignments it keeps or takes.</summary>
    readonly record struct Goal(int Page, double X, double Y, double Width, double Height, string? Across, string? Down);

    /// <summary>
    /// A drawing pressed: where, its page, its box then in the view's coordinates, whether it floats, the handle pressed (none: it moves)
    /// and how it stood; once the pointer moves, where it goes, its outline there, the lines it lines up with, and whether a step shows it.
    /// </summary>
    sealed class Dragged(Point start, IPointer pointer, int page, Rect box, bool floating, (int Across, int Down)? handle, DrawingFormatting? stood)
    {
        public Point Start { get; } = start;
        public IPointer Pointer { get; } = pointer;
        public int Page { get; } = page;
        public Rect Box { get; } = box;
        public bool Floating { get; } = floating;
        public (int Across, int Down)? Handle { get; } = handle;
        public DrawingFormatting? Stood { get; } = stood;
        public bool Moving { get; set; }
        public Goal? Target { get; set; }
        public Goal? Shown { get; set; }
        public Rect Ghost { get; set; }
        public (double? X, double? Y) Guides { get; set; }
        public bool Previewed { get; set; }
        public bool Scheduled { get; set; }
    }

    Dragged? dragged;

    /// <summary>Whether a drawing is being dragged over the sheets.</summary>
    public bool DraggingDrawing => dragged is { Moving: true };

    /// <summary>A press on the drawing just selected, or on one of its handles: it may be dragged, or sized, from there.</summary>
    void BeginDrag(Point point, IPointer pointer, (int Across, int Down)? handle = null)
    {
        if (SelectedDrawing() is not var (page, at) || BoxOf(page, at) is not { } box)
            return;
        var floating = pictures.Places(Page(page)).InlineBox(pictures.Spots(Page(page))[at]) == null;
        dragged = new Dragged(point, pointer, page, box, floating, handle, session?.Drawing);
        pointer.Capture(this);
    }

    /// <summary>The handle of the drawing selected a point of the view is on, or null.</summary>
    (int Across, int Down)? HandleAt(Point point)
    {
        if (dragged != null || SelectedDrawing() is not var (page, at) || BoxOf(page, at) is not { } box)
            return null;
        var frame = box.Inflate(1.5);
        foreach (var handle in Handles)
        {
            if (((Vector)(point - HandlePoint(frame, handle))).Length <= HandleReach)
                return handle;
        }
        return null;
    }

    static Point HandlePoint(Rect frame, (int Across, int Down) handle) =>
        new(frame.Center.X + handle.Across * frame.Width / 2, frame.Center.Y + handle.Down * frame.Height / 2);

    /// <summary>The pointer over a handle: the way it sizes the drawing.</summary>
    static Cursor CursorOf((int Across, int Down) handle) => handle switch
    {
        (-1, -1) => TopLeft,
        (1, 1) => BottomRight,
        (1, -1) => TopRight,
        (-1, 1) => BottomLeft,
        (0, -1) => TopSide,
        (0, 1) => BottomSide,
        (-1, 0) => LeftSide,
        _ => RightSide,
    };

    static readonly Cursor TopLeft = new(StandardCursorType.TopLeftCorner), BottomRight = new(StandardCursorType.BottomRightCorner),
        TopRight = new(StandardCursorType.TopRightCorner), BottomLeft = new(StandardCursorType.BottomLeftCorner), TopSide = new(StandardCursorType.TopSide),
        BottomSide = new(StandardCursorType.BottomSide), LeftSide = new(StandardCursorType.LeftSide), RightSide = new(StandardCursorType.RightSide);

    /// <summary>The pointer over the sheets, no button held: the cursor of a handle, of a drawing that can be moved, or of the text.</summary>
    Cursor CursorAt(Point point) => HandleAt(point) is { } handle ? CursorOf(handle) : DrawingAt(point) != null ? Grab : Text;

    /// <summary>The pointer moved while a drawing is pressed: once far enough, where the drawing goes, or how large it gets.</summary>
    void DragTo(Point point, KeyModifiers modifiers)
    {
        var d = dragged!;
        if (!d.Moving && ((Vector)(point - d.Start)).Length < DragDistance)
            return;
        d.Moving = true;
        if (d.Handle is { } handle)
        {
            Cursor = CursorOf(handle);
            SizeTo(d, handle, point, modifiers);
            return;
        }
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
        d.Guides = guides;
        d.Ghost = new Rect(Left(row) + x * pt, (row.Top / PxPerPt + y) * pt, moved.Width, moved.Height);
        Aim(d, new Goal(row.Index, x, y, w, h, across, null));
    }

    /// <summary>
    /// A handle dragged: the sides it moves where the pointer takes them, the others where they were, no smaller than Least; a corner keeps
    /// the proportions, the larger of its two changes counting. A side it moves lines up with the columns' edges and the margins.
    /// </summary>
    void SizeTo(Dragged d, (int Across, int Down) handle, Point point, KeyModifiers modifiers)
    {
        var (b, by) = (d.Box, point - d.Start);
        var (left, top, right, bottom) = (b.Left, b.Top, b.Right, b.Bottom);
        if (handle.Across < 0)
            left = Math.Min(left + by.X, right - Least);
        else if (handle.Across > 0)
            right = Math.Max(right + by.X, left + Least);
        if (handle.Down < 0)
            top = Math.Min(top + by.Y, bottom - Least);
        else if (handle.Down > 0)
            bottom = Math.Max(bottom + by.Y, top + Least);
        var row = RowNear(top) ?? rows.FirstOrDefault(r => r.Index == d.Page);
        if (row == null)
            return;
        var pt = factor * PxPerPt;
        (double? X, double? Y) guides = (null, null);
        // The sides it moves line up with the columns' edges and the margins.
        if (d.Floating && (modifiers & KeyModifiers.Alt) == 0 && session?.Document.TextArea(row.Index) is var (columns, marginTop, marginBottom))
        {
            var near = SnapDistance;
            double? Nearest(double at, IEnumerable<double> lines) => lines.Select(l => (double?)l).MinBy(l => Math.Abs(l!.Value - at)) is { } best && Math.Abs(best - at) <= near ? best : null;
            var across = columns.SelectMany(c => new[] { c.Left, c.Right }).Select(x => Left(row) + x * pt);
            var down = new[] { marginTop, marginBottom }.Select(y => (row.Top / PxPerPt + y) * pt);
            if (handle.Across != 0 && Nearest(handle.Across < 0 ? left : right, across) is { } x)
            {
                (left, right) = handle.Across < 0 ? (x, right) : (left, x);
                guides.X = (x - Left(row)) / pt;
            }
            if (handle.Down != 0 && Nearest(handle.Down < 0 ? top : bottom, down) is { } y)
            {
                (top, bottom) = handle.Down < 0 ? (y, bottom) : (top, y);
                guides.Y = y / pt - row.Top / PxPerPt;
            }
        }
        // A corner keeps the proportions, from the opposite corner.
        if (handle.Across != 0 && handle.Down != 0)
        {
            var scale = Math.Max((right - left) / b.Width, (bottom - top) / b.Height);
            var (w, h) = (b.Width * scale, b.Height * scale);
            (left, right) = handle.Across < 0 ? (right - w, right) : (left, left + w);
            (top, bottom) = handle.Down < 0 ? (bottom - h, bottom) : (top, top + h);
            // A line it lined up with only where the side still stands there.
            if (guides.X is { } gx && Math.Abs((handle.Across < 0 ? left : right) - (Left(row) + gx * pt)) > 0.5)
                guides.X = null;
            if (guides.Y is { } gy && Math.Abs((handle.Down < 0 ? top : bottom) - (row.Top / PxPerPt + gy) * pt) > 0.5)
                guides.Y = null;
        }
        d.Guides = guides;
        d.Ghost = new Rect(left, top, right - left, bottom - top);
        // An alignment stays where the side it holds to stays.
        var stood = d.Stood;
        var alignAcross = stood?.HorizontalAlign is { } a && (handle.Across == 0 || a == "left" && handle.Across > 0 || a == "right" && handle.Across < 0) ? a : null;
        var alignDown = stood?.VerticalAlign is { } v && (handle.Down == 0 || v == "top" && handle.Down > 0 || v == "bottom" && handle.Down < 0) ? v : null;
        Aim(d, new Goal(row.Index, (left - Left(row)) / pt, top / pt - row.Top / PxPerPt, (right - left) / pt, (bottom - top) / pt, alignAcross, alignDown));
    }

    /// <summary>Where the drawing dragged is to go; the text goes round it there as soon as the pointer rests a moment (moves that come first go before).</summary>
    void Aim(Dragged d, Goal goal)
    {
        d.Target = goal;
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

    /// <summary>The drawing dragged put where it goes, or sized as it gets, as a step of its own in place of the one that showed it before.</summary>
    void Preview(Dragged d)
    {
        if (session is not { } editing || d.Target is not { } goal || d.Shown == d.Target)
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
                d.Previewed = d.Handle != null
                    ? editing.SizeDrawing(goal.Width, goal.Height, d.Floating ? (goal.Page, goal.X, goal.Y) : null, goal.Across, goal.Down)
                    : editing.PlaceDrawing(goal.Page, goal.X, goal.Y, goal.Across);
            }
            catch (Exception e) when (e is NotSupportedException or ArgumentException)
            {
                // It stays as it was.
            }
        });
        d.Shown = d.Target;
    }

    /// <summary>
    /// The drag ends: let go (<paramref name="keep"/>), the drawing stays where it went or as large as it got (in the line, it goes where
    /// the bar was); else it goes back. A press that did not move was a click.
    /// </summary>
    void EndDrag(bool keep)
    {
        if (dragged is not { } d)
            return;
        dragged = null;
        Cursor = Text;
        if (d.Moving && (d.Floating || d.Handle != null))
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
    /// The drawing selected as a whole, framed a little outside its box, with its handles; while it is dragged floating or sized, the
    /// frame where it goes and the lines it lines up with; floating, the mark of its anchor's paragraph in the margin.
    /// </summary>
    void DrawDrawing(DrawingContext context)
    {
        var pen = new Pen(DrawingBrush, 1.5);
        if (dragged is { Moving: true } d && (d.Floating || d.Handle != null))
        {
            var frame = d.Floating || d.Handle == null ? d.Ghost.Inflate(1.5) : (SelectedDrawing() is var (p, a) && BoxOf(p, a) is { } now ? now.Inflate(1.5) : d.Ghost.Inflate(1.5));
            context.DrawRectangle(null, pen, frame, 2, 2);
            if (d.Handle != null)
                DrawHandles(context, frame);
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
            else
                DrawHandles(context, box.Inflate(1.5));
        }
        if (SelectedDrawing() is var (anchorPage, anchorAt) && anchorPage < rows.Count)
            DrawAnchor(context, anchorPage, anchorAt);
    }

    /// <summary>The handles on a drawing's frame: small rings of the sheet's colour.</summary>
    void DrawHandles(DrawingContext context, Rect frame)
    {
        var pen = new Pen(DrawingBrush, 1.5);
        foreach (var handle in Handles)
            context.DrawEllipse(Sheet ?? Brushes.White, pen, HandlePoint(frame, handle), HandleRadius, HandleRadius);
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
