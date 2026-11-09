using System.Globalization;
using Avalonia;
using Avalonia.Controls;
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
/// not); at a column's edge or middle of a page of one column it stays aligned there. The size ring sizes the drawing selected the same
/// way (see Pull), as a handle at one of its corners or the middles of its sides dragged would: a corner keeps its proportions, a side
/// stretches it; the opposite side stays, an alignment there too, and a side it moves lines up with the columns' edges and the margins.
/// The handles do not show: the ring reaches them wherever the drawing lies, also where they would lie out of the view.
/// </summary>
public sealed partial class DocumentView
{
    /// <summary>How far the pointer goes before a press on a drawing drags it, and how near an edge it lines up with it, in pixels.</summary>
    const double DragDistance = 4, SnapDistance = 6;

    /// <summary>The knob's radius less one (see DrawKnob), how near the pointer is to take it, and the size a drawing keeps at least, in pixels.</summary>
    const double HandleRadius = 4, HandleReach = 7, Least = 8;

    static readonly Cursor Grab = new(StandardCursorType.SizeAll);

    /// <summary>
    /// The handles a drawing is sized by (see Pull): the corners and the middles of the sides, as which way each moves (−1 left or up, 1
    /// right or down, 0 neither).
    /// </summary>
    static readonly (int Across, int Down)[] Handles = [(-1, -1), (0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0)];

    /// <summary>An anchor, the mark of a floating drawing's paragraph: its outline on a box of 10 × 12, a ring above (see DrawAnchor).</summary>
    static readonly Geometry AnchorSign = Geometry.Parse("M5,3.4 V11.4 M2.6,5.6 H7.4 M1,8 Q1.3,11.4 5,11.4 Q8.7,11.4 9,8");

    /// <summary>Where a drawing dragged goes: a page, its box's top left there and its size in points, and the alignments it keeps or takes.</summary>
    readonly record struct Goal(int Page, double X, double Y, double Width, double Height, string? Across, string? Down);

    /// <summary>
    /// A drawing pressed: where, its page, its box then in the view's coordinates, whether it floats, the handle pressed (none: it moves)
    /// and how it stood; once the pointer moves, where it goes, its outline there, the lines it lines up with, and whether a step shows it.
    /// </summary>
    sealed class Dragged(Point start, IPointer? pointer, int page, Rect box, bool floating, (int Across, int Down)? handle, DrawingFormatting? stood)
    {
        public Point Start { get; } = start;
        /// <summary>The pointer that drags it; none where the size ring pulls a handle (see Pull).</summary>
        public IPointer? Pointer { get; } = pointer;
        public int Page { get; } = page;
        public Rect Box { get; } = box;
        public bool Floating { get; } = floating;
        public (int Across, int Down)? Handle { get; } = handle;
        public DrawingFormatting? Stood { get; } = stood;
        /// <summary>Its frame unturned and how far it was turned then (see TurnedFrame); the angle the knob turns it to, and the one a step shows.</summary>
        public Rect Frame { get; init; } = box;
        public double Turn { get; init; }
        public double? Angle { get; set; }
        public double? ShownAngle { get; set; }
        /// <summary>Where the pointer is now.</summary>
        public Point At { get; set; } = start;
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

    /// <summary>
    /// How near the view's top or bottom the pointer scrolls it while it drags (a drawing, or the selection), and how fast it then
    /// scrolls at most, where it is at the edge (past it, half again as fast), in pixels and pixels a second.
    /// </summary>
    const double ScrollZone = 36, ScrollSpeed = 1500;

    readonly DispatcherTimer scrolling = new() { Interval = TimeSpan.FromMilliseconds(15) };

    /// <summary>Where the pointer dragging last was, in the view's visible part (it stays there while the pages scroll under it), and the keys held.</summary>
    (Point InView, KeyModifiers Modifiers)? held;

    long scrolledAt;

    /// <summary>The pointer dragging at a point of the view: near the view's top or bottom the pages scroll, so that it reaches on.</summary>
    void Hold(Point point, KeyModifiers modifiers)
    {
        if (scroller == null || dragged is { Moving: false })
            return;
        var inView = point - scroller.Offset;
        held = (inView, modifiers);
        if (Depth(inView.Y) == 0)
            scrolling.Stop();
        else if (!scrolling.IsEnabled)
        {
            scrolledAt = Environment.TickCount64;
            scrolling.Start();
        }
    }

    /// <summary>How far a height of the visible part lies in its top zone (negative) or its bottom zone, in shares of the zone; 0 between.</summary>
    double Depth(double y)
    {
        var height = scroller?.Viewport.Height ?? 0;
        return y < ScrollZone ? Math.Max(-1.5, (y - ScrollZone) / ScrollZone) : y > height - ScrollZone ? Math.Min(1.5, (y - height + ScrollZone) / ScrollZone) : 0;
    }

    /// <summary>
    /// A moment of scrolling while the pointer drags near an edge, the faster the nearer: what is under the pointer then is what it drags
    /// to, a drawing following it, the selection reaching there.
    /// </summary>
    void Scroll()
    {
        var (inView, modifiers) = held ?? default;
        var depth = held != null ? Depth(inView.Y) : 0;
        if (scroller == null || depth == 0 || !(dragged is { Moving: true } || selecting))
        {
            scrolling.Stop();
            return;
        }
        var now = Environment.TickCount64;
        var seconds = Math.Clamp((now - scrolledAt) / 1000.0, 0, 0.05);
        scrolledAt = now;
        var top = Math.Clamp(scroller.Offset.Y + Math.Sign(depth) * depth * depth * ScrollSpeed * seconds, 0, Math.Max(0, scroller.Extent.Height - scroller.Viewport.Height));
        if (Math.Abs(top - scroller.Offset.Y) < 0.01)
            return;
        scroller.Offset = new Vector(scroller.Offset.X, top);
        var point = inView + scroller.Offset;
        if (dragged != null)
            DragTo(point, modifiers);
        else
        {
            SelectTo(point);
            InvalidateVisual();
        }
    }

    /// <summary>How far an arrow moves a floating drawing, and with Shift, in points.</summary>
    const double NudgeStep = 1, FarStep = 10;

    /// <summary>
    /// The arrows pressed one after another on a floating drawing: its page and its box's top left in points where they began, how far
    /// they have moved it, the place of its character then, and the document's changes after the last; null before them.
    /// </summary>
    (int Page, double X, double Y, double Dx, double Dy, TextPlace Drawing, int Changes)? nudged;

    /// <summary>
    /// An arrow on the floating drawing selected: it moves a point that way, ten with Shift (its anchor going with it as a drag takes
    /// it). Arrows one after another, with nothing else between them, are one step: the one before taken back, all of them made again.
    /// False where no floating drawing is selected, the arrow then moving the cursor.
    /// </summary>
    bool Nudge(Key key, bool far)
    {
        if (session is not { } editing || SelectedDrawing() is not var (page, at) || pictures.Places(Page(page)).InlineBox(pictures.Spots(Page(page))[at]) != null
            || BoxOf(page, at) is not { } box)
            return false;
        var step = far ? FarStep : NudgeStep;
        var (dx, dy) = key switch { Key.Left => (-step, 0.0), Key.Right => (step, 0.0), Key.Up => (0.0, -step), _ => (0.0, step) };
        var drawing = editing.Anchor.Offset < editing.Caret.Offset ? editing.Anchor : editing.Caret;
        if (nudged is { } before && before.Changes == editing.Document.Changes && before.Drawing == drawing && editing.CanUndo)
        {
            var (total, totalY) = (before.Dx + dx, before.Dy + dy);
            Edit(() =>
            {
                editing.TakeBack();
                editing.PlaceDrawing(before.Page, before.X + total, before.Y + totalY);
            });
            nudged = before with { Dx = total, Dy = totalY };
        }
        else
        {
            var row = rows[page];
            var pt = factor * PxPerPt;
            var (x, y) = ((box.X - Left(row)) / pt, box.Y / pt - row.Top / PxPerPt);
            Edit(() => editing.PlaceDrawing(page, x + dx, y + dy));
            nudged = (page, x, y, dx, dy, drawing, 0);
        }
        nudged = nudged.Value with { Drawing = editing.Anchor.Offset < editing.Caret.Offset ? editing.Anchor : editing.Caret, Changes = editing.Document.Changes };
        return true;
    }

    /// <summary>The pointer let go: no more scrolling for it.</summary>
    void LetGo()
    {
        scrolling.Stop();
        held = null;
    }

    /// <summary>A press on the drawing just selected, or on its knob: it may be dragged, or turned, from there.</summary>
    void BeginDrag(Point point, IPointer pointer, (int Across, int Down)? handle = null)
    {
        if (Take(point, pointer, handle) != null)
            pointer.Capture(this);
    }

    /// <summary>
    /// The drawing selected taken at a point to be dragged, by a pointer (none for the size ring, see Pull), as a whole, by a handle or by
    /// its knob; null where none is selected.
    /// </summary>
    Dragged? Take(Point point, IPointer? pointer, (int Across, int Down)? handle)
    {
        if (SelectedDrawing() is not var (page, at) || BoxOf(page, at) is not { } box)
            return null;
        var floating = pictures.Places(Page(page)).InlineBox(pictures.Spots(Page(page))[at]) == null;
        var (frame, turn) = TurnedFrame() ?? (box, 0);
        return dragged = new Dragged(point, pointer, page, box, floating, handle, session?.Drawing) { Frame = frame, Turn = turn };
    }

    /// <summary>
    /// The drawing selected sized by the size ring (see GestureRing.Slot.Pulls) as a handle dragged would size it: the handle that stands
    /// that way on the screen (−1, 0 or 1 across and down; of a picture turned, the side or the corner, as the way is one, that stands
    /// nearest that way), moved as far as the pull goes (see SizeTo, SizeTurned). Null where nothing is selected that is sized so (a
    /// table's frame is as large as its table), or while something else is dragged.
    /// </summary>
    public GestureRing.IPull? Pull((int Across, int Down) way)
    {
        if (dragged != null || cropping || tilting || session?.FrameSelected == true || TurnedFrame() is not var (frame, turn))
            return null;
        var side = way.Across == 0 || way.Down == 0;
        var handle = turn == 0 ? way : Handles.Where(h => (h.Across == 0 || h.Down == 0) == side).MaxBy(h =>
        {
            var on = Turned(new Point(h.Across, h.Down), default, turn);
            return (on.X * way.Across + on.Y * way.Down) / Math.Sqrt(h.Across * h.Across + h.Down * h.Down);
        });
        return Take(Turned(HandlePoint(frame, handle), frame.Center, turn), null, handle) is { } d ? new Pulled(this, d) : null;
    }

    /// <summary>A pull of the size ring sizing the drawing selected (see Pull): its handle as far as the pull goes; kept, or taken back.</summary>
    sealed class Pulled(DocumentView view, Dragged d) : GestureRing.IPull
    {
        public void To(Vector by, KeyModifiers modifiers)
        {
            if (ReferenceEquals(view.dragged, d))
                view.DragTo(d.Start + by, modifiers);
        }

        public void End(bool keep)
        {
            if (ReferenceEquals(view.dragged, d))
                view.EndDrag(keep);
        }

        public string? Reading => view.SizeReading();

        public Cursor? Cursor => CursorOf(d.Handle!.Value, d.Turn);
    }

    /// <summary>
    /// The size of the drawing selected as the size ring reads it, as large as a pull has it while it goes: "8,5 × 6,2 cm", in inches where
    /// the region measures so; null where none is selected.
    /// </summary>
    public string? SizeReading()
    {
        (double Width, double Height) size;
        if (dragged is { Moving: true, Handle: not null, Target: { } goal })
            size = (goal.Width, goal.Height);
        else if (TurnedFrame() is var (frame, _))
            size = (frame.Width / (factor * PxPerPt), frame.Height / (factor * PxPerPt));
        else
            return null;
        var metric = RegionInfo.CurrentRegion.IsMetric;
        string Length(double points) => metric ? (points / 72 * 2.54).ToString("0.0", CultureInfo.CurrentCulture) : (points / 72).ToString("0.00", CultureInfo.CurrentCulture);
        return $"{Length(size.Width)} × {Length(size.Height)} {(metric ? "cm" : "in")}";
    }

    /// <summary>Whether a point of the view is on the knob that turns the picture selected (its frame turned as it shows, see TurnedFrame).</summary>
    bool KnobAt(Point point) => dragged == null && TurnsHere && TurnedFrame() is var (box, turn)
        && ((Vector)(Turned(point, box.Center, -turn) - KnobPoint(box.Inflate(1.5)))).Length <= HandleReach;

    /// <summary>The pointer over a handle of a drawing turned so far: the way the handle sizes it on the screen; over the knob, turning.</summary>
    static Cursor CursorOf((int Across, int Down) handle, double turn) =>
        handle == Knob ? Turning : turn == 0 ? CursorOf(handle) : CursorOf(handle, TurnAbout(default, turn));

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

    /// <summary>The pointer over the sheets, no button held: the cursor of the knob, of a drawing that can be moved, or of the text.</summary>
    Cursor CursorAt(Point point) => KnobAt(point) ? Turning : DrawingAt(point) != null ? Grab
        : TableHandleAt(point) != null ? Grab : GripAt(point) is { } grip ? (grip.Plus ? Turning : Grab) : TableLineAt(point) is { } line ? (line.Upright ? Sideways : Upways) : TextCursorAt(point);

    /// <summary>The pointer moved while a drawing is pressed: once far enough, where the drawing goes, or how large it gets.</summary>
    void DragTo(Point point, KeyModifiers modifiers)
    {
        var d = dragged!;
        if (!d.Moving && ((Vector)(point - d.Start)).Length < DragDistance)
            return;
        (d.Moving, d.At) = (true, point);
        if (d.Handle == Knob)
        {
            Cursor = Turning;
            TurnTo(d, point, modifiers);
            return;
        }
        if (d.Handle is { } handle)
        {
            Cursor = CursorOf(handle, d.Turn);
            if (d.Turn != 0)
                SizeTurned(d, handle, point);
            else
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

    /// <summary>
    /// A handle of a turned picture dragged: as SizeTo, along the picture's own sides (a corner keeps the proportions), the side opposite
    /// staying where it stands on the screen; it lines up with nothing.
    /// </summary>
    void SizeTurned(Dragged d, (int Across, int Down) handle, Point point)
    {
        var f = d.Frame;
        var by = Turned(point, f.Center, -d.Turn) - Turned(d.Start, f.Center, -d.Turn);
        var (left, top, right, bottom) = (f.Left, f.Top, f.Right, f.Bottom);
        if (handle.Across < 0)
            left = Math.Min(left + by.X, right - Least);
        else if (handle.Across > 0)
            right = Math.Max(right + by.X, left + Least);
        if (handle.Down < 0)
            top = Math.Min(top + by.Y, bottom - Least);
        else if (handle.Down > 0)
            bottom = Math.Max(bottom + by.Y, top + Least);
        if (handle.Across != 0 && handle.Down != 0)
        {
            var scale = Math.Max((right - left) / f.Width, (bottom - top) / f.Height);
            var (w, h) = (f.Width * scale, f.Height * scale);
            (left, right) = handle.Across < 0 ? (right - w, right) : (left, left + w);
            (top, bottom) = handle.Down < 0 ? (bottom - h, bottom) : (top, top + h);
        }
        // Its new middle turned back onto the screen, so that the side opposite stays where it is.
        var middle = Turned(new Point((left + right) / 2, (top + bottom) / 2), f.Center, d.Turn);
        var (width, height) = (right - left, bottom - top);
        d.Ghost = new Rect(middle.X - width / 2, middle.Y - height / 2, width, height);
        d.Guides = (null, null);
        if (RowNear(middle.Y) is not { } row)
            return;
        var pt = factor * PxPerPt;
        Aim(d, new Goal(row.Index, (d.Ghost.X - Left(row)) / pt, d.Ghost.Y / pt - row.Top / PxPerPt, width / pt, height / pt, null, null));
    }

    /// <summary>
    /// Where the drawing dragged is to go: sized, it shows so with the next frame (see PreviewNext); moved, the text goes round it there
    /// as soon as the pointer rests a moment (moves that come first go before).
    /// </summary>
    void Aim(Dragged d, Goal goal)
    {
        d.Target = goal;
        if (d.Handle != null)
            PreviewNext(d);
        else if (!d.Scheduled)
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

    /// <summary>
    /// The drawing dragged shown as the drag has it (sized, or turned by its knob) with the next frame. Not when the dispatcher is idle: a
    /// hand moving the pointer keeps input waiting, and work of a lower priority than input waits as long (Avalonia's dispatcher, as
    /// WPF's), so that the drawing stood still while the pointer moved and jumped when it rested.
    /// </summary>
    void PreviewNext(Dragged d)
    {
        if (d.Scheduled || TopLevel.GetTopLevel(this) is not { } top)
            return;
        d.Scheduled = true;
        top.RequestAnimationFrame(_ =>
        {
            d.Scheduled = false;
            if (ReferenceEquals(dragged, d))
                Preview(d);
        });
    }

    /// <summary>The drawing dragged put where it goes, or sized as it gets, as a step of its own in place of the one that showed it before.</summary>
    void Preview(Dragged d)
    {
        // Turned by its knob: the angle as a step of its own.
        if (d.Handle == Knob)
        {
            if (session is not { } turning || d.Angle is not { } angle || d.ShownAngle == d.Angle)
                return;
            Edit(() =>
            {
                if (d.Previewed)
                {
                    turning.TakeBack();
                    d.Previewed = false;
                }
                d.Previewed = Stepped(turning, () => turning.Format(new DrawingFormatting { Rotation = angle }));
            });
            d.ShownAngle = d.Angle;
            return;
        }
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
                d.Previewed = Stepped(editing, () =>
                {
                    if (d.Handle != null)
                        editing.SizeDrawing(goal.Width, goal.Height, d.Floating ? (goal.Page, goal.X, goal.Y) : null, goal.Across, goal.Down);
                    else
                        editing.PlaceDrawing(goal.Page, goal.X, goal.Y, goal.Across);
                });
            }
            catch (Exception e) when (e is NotSupportedException or ArgumentException)
            {
                // It stays as it was.
            }
        });
        d.Shown = d.Target;
    }

    /// <summary>A change made as a preview: whether it made a step (one that changes nothing makes none, nothing to be taken back then).</summary>
    static bool Stepped(EditSession editing, Action change)
    {
        var steps = editing.Steps;
        change();
        return editing.Steps > steps;
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
        LetGo();
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
        if (cropDrag is { } crop && ReferenceEquals(e.Pointer, crop.Pointer))
            EndCropDrag(keep: false);
        if (tiltDrag is { } tilt && ReferenceEquals(e.Pointer, tilt.Pointer))
            EndTiltDrag(keep: false);
        if (lineDrag is { } line && ReferenceEquals(e.Pointer, line.Pointer))
            EndLineDrag(keep: false);
        if (stroke is { } drawn && ReferenceEquals(e.Pointer, drawn.Pointer))
            EndStroke(keep: false);
        if (gripDrag is { } grip && ReferenceEquals(e.Pointer, grip.Pointer))
            EndGripDrag(keep: false);
        if (tableDrag is { } table && ReferenceEquals(e.Pointer, table.Pointer))
            EndTableDrag(keep: false);
    }

    /// <summary>
    /// The drawing selected as a whole, framed a little outside its box (a picture turned, its frame turned with it, see TurnedFrame),
    /// with a picture's knob; while it is dragged floating or sized, the frame where it goes and the lines it lines up
    /// with; turned by its knob, its frame as far as the pointer has it, the angle beside the pointer; floating, the mark of its
    /// anchor's paragraph in the margin.
    /// </summary>
    void DrawDrawing(DrawingContext context)
    {
        var pen = new Pen(DrawingBrush, 1.5);
        if (dragged is { Moving: true } d && (d.Floating || d.Handle != null))
        {
            // Floating, where it goes; else as it shows now (the line has it); turned as it was, or as the knob has it (a picture with its
            // caption level).
            var shows = TurnedFrame()?.Frame;
            var (frame, turn) = d.Handle == Knob ? (shows ?? d.Frame, session?.Caption != null ? 0 : d.Angle ?? d.Turn)
                : d.Floating ? (d.Turn != 0 && d.Handle == null ? FrameAbout(d.Ghost.Center, d.Frame.Size) : d.Ghost, d.Turn)
                : (d.Turn != 0 ? shows ?? d.Ghost : SelectedDrawing() is var (p, a) && BoxOf(p, a) is { } now ? now : d.Ghost, d.Turn);
            using (context.PushTransform(TurnAbout(frame.Center, turn)))
            {
                context.DrawRectangle(null, pen, frame.Inflate(1.5), 2, 2);
                if (d.Handle == Knob)
                    DrawKnob(context, frame.Inflate(1.5));
            }
            if (d.Handle == Knob)
                DrawAngle(context, d, d.At);
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
        else if (!SelectionHidden && !cropping && !tilting && TurnedFrame() is var (box, turned))
        {
            using (context.PushTransform(TurnAbout(box.Center, turned)))
            {
                context.DrawRectangle(null, pen, box.Inflate(1.5), 2, 2);
                if (dragged is not { Moving: true } && TurnsHere)
                    DrawKnob(context, box.Inflate(1.5));
            }
            // In the line, dragged: its outline with the pointer, the bar where it goes (see Render).
            if (dragged is { Moving: true } inLine)
            {
                var ghost = inLine.Turn != 0 ? FrameAbout(inLine.Ghost.Center, inLine.Frame.Size) : inLine.Ghost;
                using (context.PushTransform(TurnAbout(ghost.Center, inLine.Turn)))
                    context.DrawRectangle(null, new Pen(DrawingBrush, 1, new DashStyle([4, 3], 0)), ghost, 2, 2);
            }
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
