using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace FreeSlate.Editor.Controls;

/// <summary>
/// The picture selected turned and tilted by hand. A knob above its frame turns it about its middle (see DrawingFormatting.Rotation): it
/// follows the pointer, at the quarter turns it stays a little (Alt held, not), with Shift it goes by 15 degrees, its angle beside the
/// pointer; letting go keeps it, Escape takes it back. Its frame and handles turn with it, the handles sizing it along its own sides.
/// Tilted by hand (see BeginTilt), dragging the picture turns it in space as Word's 3-D Rotation does (see PictureTilt), in perspective:
/// across about its upright axis, up and down about its level one, a globe round its middle showing how; Enter, Escape or a press
/// elsewhere ends it, Escape while dragging takes the drag back.
/// </summary>
public sealed partial class DocumentView
{
    /// <summary>The knob that turns a picture, among the handles (see Handles): above the middle of its frame's top.</summary>
    static readonly (int Across, int Down) Knob = (0, -2);

    /// <summary>How far above the frame the knob stands, how near a quarter turn the knob stays at it (degrees), and the turn Shift goes by.</summary>
    const double KnobDistance = 22, QuarterReach = 4, ShiftTurn = 15;

    /// <summary>How far a picture tilted by hand turns for each pixel the pointer goes, and how far at most, in degrees.</summary>
    const double TiltPerPixel = 0.4, TiltMost = 75;

    static readonly Cursor Turning = new(StandardCursorType.Hand);

    /// <summary>
    /// The frame of the drawing selected as it shows turned: its own size about the middle of its box (which a turn of 45 up to 135 degrees
    /// swaps in the line), and how far it is turned clockwise, in the view's coordinates; a picture with its caption as its box, unturned
    /// (its picture turns in it). Null where no drawing is selected.
    /// </summary>
    (Rect Frame, double Turn)? TurnedFrame()
    {
        if (SelectedDrawing() is not var (page, at) || BoxOf(page, at) is not { } box || session is not { } editing)
            return null;
        if (editing.Caption != null || editing.TurnShown is not { Rotation: { } turn and not 0 } || editing.Drawing is not { Width: { } w, Height: { } h })
            return (box, 0);
        var pt = factor * PxPerPt;
        return (FrameAbout(box.Center, new Size(w * pt, h * pt)), turn);
    }

    /// <summary>Whether the drawing selected can be turned: a picture, by itself or with its caption, or a table's frame (see EditSession.TurnShown).</summary>
    bool TurnsHere => session?.TurnShown != null && !cropping && !tilting;

    /// <summary>The selection where it is a picture shown turned: its frame unturned and how far it is turned (see TurnedFrame); null otherwise.</summary>
    public (Rect Frame, double Turn)? SelectionTurned() => TurnedFrame() is var (frame, turn) && turn != 0 ? (frame, turn) : null;

    /// <summary>A turn about a point by so many degrees clockwise, as the view's matrix.</summary>
    static Matrix TurnAbout(Point centre, double degrees) =>
        Matrix.CreateTranslation(-centre.X, -centre.Y) * Matrix.CreateRotation(degrees * Math.PI / 180) * Matrix.CreateTranslation(centre.X, centre.Y);

    static Point Turned(Point point, Point centre, double degrees) => point.Transform(TurnAbout(centre, degrees));

    /// <summary>A frame of a size about a point.</summary>
    static Rect FrameAbout(Point centre, Size size) => new(centre.X - size.Width / 2, centre.Y - size.Height / 2, size.Width, size.Height);

    /// <summary>The pointer over a handle (or a crop mark) of a frame shown turned (and mirrored) so: the way it sizes it on the screen, the nearest of the eight.</summary>
    static Cursor CursorOf((int Across, int Down) handle, Matrix turn)
    {
        var way = new Point(handle.Across, handle.Down).Transform(turn) - new Point(0, 0).Transform(turn);
        (int, int)[] ways = [(1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1)];
        return CursorOf(ways[(int)Math.Round(Math.Atan2(way.Y, way.X) * 180 / Math.PI / 45) & 7]);
    }

    /// <summary>Where the knob of a frame stands before the frame turns: above its top's middle.</summary>
    static Point KnobPoint(Rect frame) => new(frame.Center.X, frame.Top - KnobDistance);

    /// <summary>The knob over a frame (not yet turned): a stem from the frame's top and a ring as a handle's.</summary>
    void DrawKnob(DrawingContext context, Rect frame)
    {
        var pen = new Pen(DrawingBrush, 1.5);
        var knob = KnobPoint(frame);
        context.DrawLine(new Pen(DrawingBrush, 1), new Point(frame.Center.X, frame.Top), new Point(knob.X, knob.Y + HandleRadius + 1));
        context.DrawEllipse(Sheet ?? Brushes.White, pen, knob, HandleRadius + 1, HandleRadius + 1);
        context.DrawEllipse(DrawingBrush, null, knob, 1.6, 1.6);
    }

    /// <summary>The pointer turning the picture by its knob: the angle from its middle, at a quarter turn or by 15 degrees as the keys have it.</summary>
    void TurnTo(Dragged d, Point point, KeyModifiers modifiers)
    {
        var centre = d.Frame.Center;
        var by = (Math.Atan2(point.Y - centre.Y, point.X - centre.X) - Math.Atan2(d.Start.Y - centre.Y, d.Start.X - centre.X)) * 180 / Math.PI;
        var angle = d.Turn + by;
        if ((modifiers & KeyModifiers.Shift) != 0)
            angle = Math.Round(angle / ShiftTurn) * ShiftTurn;
        else if ((modifiers & KeyModifiers.Alt) == 0 && Math.Round(angle / 90) * 90 is var quarter && Math.Abs(angle - quarter) <= QuarterReach)
            angle = quarter;
        angle = (angle % 360 + 360) % 360;
        if (Math.Abs(angle - 360) < 1e-6)
            angle = 0;
        d.Angle = angle;
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

    /// <summary>The angle a picture is turned to while its knob is dragged, beside the pointer as the ring names its fields.</summary>
    void DrawAngle(DrawingContext context, Dragged d, Point at)
    {
        if (d.Angle is not { } angle)
            return;
        var surface = this.TryFindResource("TipSurface", ActualThemeVariant, out var s) ? s as IBrush : null;
        var ink = this.TryFindResource("TipInk", ActualThemeVariant, out var i) ? i as IBrush : null;
        var text = new FormattedText($"{angle:0}°", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("fonts:Inter#Inter"), FontStyle.Normal, FontWeight.SemiBold), 11.5, ink ?? Brushes.White);
        var box = new Rect(at.X + 16, at.Y - 28, text.Width + 16, text.Height + 8);
        context.DrawRectangle(surface ?? Brushes.Black, null, new RoundedRect(box, box.Height / 2));
        context.DrawText(text, new Point(box.X + 8, box.Y + 4));
    }

    /// <summary>Whether the picture selected is being tilted by hand.</summary>
    bool tilting;

    /// <summary>A press while tilting: where, and how the picture was tilted then; once the pointer moves, the tilt it goes to.</summary>
    sealed class TiltDrag(Point start, IPointer pointer, PictureTilt from)
    {
        public Point Start { get; } = start;
        public IPointer Pointer { get; } = pointer;
        public PictureTilt From { get; } = from;
        public bool Moving { get; set; }
        public PictureTilt? Target { get; set; }
        public PictureTilt? Shown { get; set; }
        public bool Previewed { get; set; }
        public bool Scheduled { get; set; }
    }

    TiltDrag? tiltDrag;

    /// <summary>Whether the picture selected is being tilted by hand (see BeginTilt).</summary>
    public bool Tilting => tilting && session?.TurnShown != null && SelectedDrawing() != null;

    /// <summary>The picture selected is tilted by hand from here (see the class); false where the selection holds no picture.</summary>
    public bool BeginTilt()
    {
        if (session?.TurnShown == null || SelectedDrawing() == null)
            return false;
        EndCrop();
        tilting = true;
        Cursor = Grab;
        InvalidateVisual();
        return true;
    }

    /// <summary>Tilting by hand ends; a drag going on goes back.</summary>
    public void EndTilt()
    {
        if (!tilting && tiltDrag == null)
            return;
        if (tiltDrag != null)
            EndTiltDrag(keep: false);
        tilting = false;
        Cursor = Text;
        InvalidateVisual();
    }

    /// <summary>Whether a point of the view is on the picture being tilted, or near it, where a press tilts it.</summary>
    bool TiltsAt(Point point) => SelectedDrawing() is var (page, at) && BoxOf(page, at) is { } box && box.Inflate(KnobDistance).Contains(point);

    /// <summary>
    /// A selection made at a point other than by a press there (the ring's, see SelectDrawingAt): cropping and tilting by hand end first,
    /// unless the point is on their picture.
    /// </summary>
    void Leave(Point point)
    {
        if ((cropping || tilting) && DrawingAt(point) is var there && (there == null || there != SelectedDrawing()))
        {
            EndCrop();
            EndTilt();
        }
        if (shaping != null && !OnShaped(point))
            EndShape();
    }

    /// <summary>A press of the main button while tilting: on the picture it tilts it from there; false elsewhere.</summary>
    bool TiltPress(Point point, IPointer pointer)
    {
        if (session?.TurnShown is not { } shown || !TiltsAt(point))
            return false;
        tiltDrag = new TiltDrag(point, pointer, shown.Tilt ?? PictureTilt.Flat);
        pointer.Capture(this);
        return true;
    }

    /// <summary>
    /// The pointer moved while tilting: across it turns the picture about its upright axis, down about its level one, from the angles it
    /// stood at (its camera's own where none were given), its camera kept (one straight on, in perspective).
    /// </summary>
    void TiltTo(Point point)
    {
        var d = tiltDrag!;
        var by = point - d.Start;
        if (!d.Moving && ((Vector)by).Length < DragDistance)
            return;
        d.Moving = true;
        static double Within(double degrees) => Math.Clamp(Math.IEEERemainder(degrees, 360), -TiltMost, TiltMost);
        var (x, y, z) = d.From.Angles;
        var camera = d.From.Camera == PictureTilt.Flat.Camera ? "perspectiveFront" : d.From.Camera;
        d.Target = new PictureTilt(camera, Within(x + by.X * TiltPerPixel), Within(y + by.Y * TiltPerPixel), z, d.From.Fov);
        if (!d.Scheduled)
        {
            d.Scheduled = true;
            Dispatcher.UIThread.Post(() =>
            {
                d.Scheduled = false;
                if (ReferenceEquals(tiltDrag, d))
                    PreviewTilt(d);
            }, DispatcherPriority.Background);
        }
        InvalidateVisual();
    }

    /// <summary>The picture tilted as the drag has it, a step of its own in place of the one that showed it before.</summary>
    void PreviewTilt(TiltDrag d)
    {
        if (session is not { } editing || d.Target is not { } tilt || d.Shown == d.Target)
            return;
        Edit(() =>
        {
            if (d.Previewed)
            {
                editing.TakeBack();
                d.Previewed = false;
            }
            d.Previewed = Stepped(editing, () => editing.TiltPicture(tilt));
        });
        d.Shown = d.Target;
    }

    /// <summary>The press while tilting ends: let go (<paramref name="keep"/>), the picture stays tilted as it shows (a step); else as it was.</summary>
    void EndTiltDrag(bool keep)
    {
        if (tiltDrag is not { } d)
            return;
        tiltDrag = null;
        d.Pointer.Capture(null);
        if (keep && d.Moving)
            PreviewTilt(d);
        else if (d.Previewed && session is { } editing)
            Edit(() => editing.TakeBack());
        InvalidateVisual();
    }

    /// <summary>
    /// The picture being tilted by hand: a globe round its middle, its equator and its meridian leaning as far as the picture is tilted
    /// each way, faint; the picture's box a dashed line round it.
    /// </summary>
    void DrawTilt(DrawingContext context)
    {
        if (!tilting)
            return;
        if (session?.TurnShown is not { } shown || SelectedDrawing() is not var (page, at) || BoxOf(page, at) is not { } box)
        {
            tilting = false;
            return;
        }
        var accent = DrawingBrush ?? Brushes.Orange;
        var (x, y, _) = (tiltDrag?.Target ?? shown.Tilt ?? PictureTilt.Flat).Angles;
        var centre = box.Center;
        var r = Math.Min(box.Width, box.Height) / 2 + 12;
        var line = new Pen(accent, 1);
        using (context.PushOpacity(0.7))
        {
            context.DrawEllipse(null, line, centre, r, r);
            context.DrawEllipse(null, line, centre, r, Math.Max(0.5, r * Math.Abs(Math.Sin(y * Math.PI / 180))));
            context.DrawEllipse(null, line, centre, Math.Max(0.5, r * Math.Abs(Math.Sin(x * Math.PI / 180))), r);
        }
        context.DrawRectangle(null, new Pen(accent, 1, new DashStyle([3, 3], 0)), box.Inflate(1.5));
    }
}
