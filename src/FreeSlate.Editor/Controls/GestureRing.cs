using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;

namespace FreeSlate.Editor.Controls;

/// <summary>
/// The gesture ring, a marking menu: eight fields around a point, one direction each, their places fixed so that the hand learns them
/// (see Slots); a field may hold a ring of its own, a second level. The fields show signs only, so that the ring stays small, and the
/// name of the field pointed at stands beside the ring; the ring is of glass, and where the text it acts on lies under it, a window is
/// cut into it: the text shows there, and as it reads with what the field pointed at would do (Preview).
/// The right button held on the selection, or on a word where there is none, brings it after a moment of rest; moved into a field and
/// let go there, it does what the field does, let go in the middle nothing; moved on out of a field that holds a ring, that ring comes
/// where the pointer is. Moved on at once beyond a few pixels, it does what the direction does without a ring (a turn and a second
/// direction for a second level), with a thin trace and the field's name for a moment. A short click opens it to be clicked (a click on
/// a field with a ring opens that ring, one in the middle goes back). The left button, a finger or a pen held on the selection brings it
/// too, where there is no right button. With the keyboard, Space held on a selection brings it at the pointer: the arrows choose (two
/// together a diagonal), Enter opens a field's ring, Backspace goes back, letting go of Space does what is chosen, Escape nothing. It
/// lies over the editor and takes the pointer only while it has a gesture.
/// </summary>
public sealed class GestureRing : Control
{
    /// <summary>How long the pointer rests before the ring shows, and how long the left button is held on the selection for it, in milliseconds.</summary>
    const double RestTime = 230, HoldTime = 450;

    /// <summary>How far the pointer goes before the ring shows to choose blindly, and before a press on the selection selects anew; how far a blind stroke turns aside for a second level.</summary>
    const double BlindDistance = 15, SelectDistance = 4, Turn = 12;

    /// <summary>The middle of the ring, where letting go does nothing (its radius, the least of the inner circle), and how far a sign keeps from its field's edges and rims.</summary>
    const double Hub = 36, Pad = 6;

    /// <summary>The widest the inner circle grows around a window; a wider window runs on through the band of fields, a sign over it on a spot of the ring's surface.</summary>
    const double MostInner = 64;

    /// <summary>The gap around the field the pointer is in, which shows it; how much the glass of the band of fields covers, and of the inner circle.</summary>
    const double Gap = 3, Glass = 0.94, InnerGlass = 0.5;

    /// <summary>
    /// A field's sign, its ink in pixels: no taller than SignHeight, no wider than SignWidth, and of about the area of a square of
    /// SignArea, so that signs of every shape look alike in size.
    /// </summary>
    const double SignHeight = 16, SignWidth = 22, SignArea = 14;

    /// <summary>Half the angle of a field.</summary>
    const double Half = Math.PI / 8;

    /// <summary>How long a ring takes to come, and how long the name of a field chosen blindly stays, in milliseconds.</summary>
    const double Appear = 120, Confirmation = 900;

    /// <summary>
    /// A field of the ring: its name's key among the texts (its name where there is none), its sign, what it does, or the ring it holds
    /// (eight fields, as the ring has); neither: an empty field.
    /// </summary>
    public sealed record Slot(string NameKey, string Sign, Action? Act = null, IReadOnlyList<Slot>? Under = null, bool Slanted = false)
    {
        public bool IsEmpty => Act == null && Under == null;
    }

    /// <summary>
    /// A ring as it shows: its fields, its centre, the field of the ring before it that holds it, and its radii: the inner circle (the
    /// middle, where letting go does nothing, or around the window onto the text), where its signs stand, and how far it reaches.
    /// </summary>
    sealed record Level(IReadOnlyList<Slot> Slots, Point Centre, Slot? Parent, double Inner, double Middle, double Outer);

    enum Mode { None, Waiting, Blind, Held, Clicking, Holding, KeysWaiting, Keys }

    readonly DispatcherTimer rest = new();
    readonly DispatcherTimer frames = new() { Interval = TimeSpan.FromMilliseconds(15) };
    readonly HashSet<Key> arrows = [];
    readonly List<Level> levels = [];
    readonly List<Point> stroke = [];
    readonly Dictionary<Slot, Geometry> signs = [];
    Mode mode;
    Point start, at;
    IPointer? pointer;
    int? chosen;
    long since;
    (string Name, Point At, long Since)? confirmed;

    public GestureRing()
    {
        IsHitTestVisible = false;
        rest.Tick += (_, _) => Rested();
        frames.Tick += (_, _) =>
        {
            var now = Environment.TickCount64;
            if (confirmed is { } c && now - c.Since > Confirmation)
                confirmed = null;
            if (confirmed == null && (levels.Count == 0 || now - since > Appear))
                frames.Stop();
            InvalidateVisual();
        };
    }

    /// <summary>The eight fields of the first ring, from the top clockwise: up, up right, right, down right, down, down left, left, up left.</summary>
    public IReadOnlyList<Slot> Slots { get; set; } = [];

    /// <summary>
    /// Makes the text at a point of the ring's (the editor's) coordinates the one a field acts on: the selection where the point is on it,
    /// else the word there, which it selects; false where there is no text to act on.
    /// </summary>
    public Func<Point, bool>? Target { get; set; }

    /// <summary>Where the text a field acts on lies near a point, in the ring's coordinates (its line's part of the selection): the window in the ring.</summary>
    public Func<Point, Rect?>? Window { get; set; }

    /// <summary>
    /// The field pointed at changed: the text is to read as that field would make it (null: as it was). What the field then does keeps
    /// what its preview made; a ring closed without doing anything has a preview taken back by this with null.
    /// </summary>
    public Action<Slot?>? Preview { get; set; }

    /// <summary>Whether a point of the ring's coordinates is on the selection.</summary>
    public Func<Point, bool>? OnSelection { get; set; }

    /// <summary>A press of the left button on the selection that was none for the ring: a click there (false) or the start of selecting (true), as the sheets take them.</summary>
    public Action<Point, IPointer, bool>? Press { get; set; }

    /// <summary>Whether the ring has a gesture going: it takes the pointer, Escape takes it back.</summary>
    public bool IsBusy => mode != Mode.None;

    /// <summary>Takes the presses on the sheets the ring answers: the right button (Ctrl and the button on a Mac), and the left one on the selection.</summary>
    public void Attach(Control sheets) => sheets.AddHandler(PointerPressedEvent, Pressed, RoutingStrategies.Tunnel);

    Level? Current => levels.Count > 0 ? levels[^1] : null;

    void Pressed(object? sender, PointerPressedEventArgs e)
    {
        if (mode != Mode.None)
            return;
        var properties = e.GetCurrentPoint(this).Properties;
        var point = e.GetPosition(this);
        var right = properties.IsRightButtonPressed
            || OperatingSystem.IsMacOS() && properties.IsLeftButtonPressed && (e.KeyModifiers & KeyModifiers.Control) != 0;
        if (right && Target?.Invoke(point) == true)
            Begin(Mode.Waiting, point, e.Pointer, RestTime);
        else if (!right && properties.IsLeftButtonPressed && e.KeyModifiers == KeyModifiers.None && OnSelection?.Invoke(point) == true)
            Begin(Mode.Holding, point, e.Pointer, HoldTime);
        else
            return;
        e.Handled = true;
        e.Pointer.Capture(this);
    }

    void Begin(Mode now, Point point, IPointer? by, double wait)
    {
        (mode, start, at, pointer, chosen) = (now, point, point, by, null);
        stroke.Clear();
        stroke.Add(point);
        rest.Interval = TimeSpan.FromMilliseconds(wait);
        rest.Start();
    }

    /// <summary>The pointer or the key rested long enough: the ring shows where the gesture began (the pointer, for the keyboard).</summary>
    void Rested()
    {
        rest.Stop();
        if (mode is Mode.Waiting or Mode.Holding)
            Open(Mode.Held, start);
        else if (mode == Mode.KeysWaiting)
            Open(Mode.Keys, start);
    }

    void Open(Mode now, Point point)
    {
        (mode, confirmed) = (now, null);
        levels.Clear();
        levels.Add(Measure(Slots, point, null));
        since = Environment.TickCount64;
        IsHitTestVisible = mode == Mode.Clicking;
        Aim(mode == Mode.Keys ? null : Pointed(at));
        frames.Start();
        InvalidateVisual();
    }

    /// <summary>The ring a field holds, at a point: the first level's direction chosen, the second's to come.</summary>
    void Enter(Slot slot, Point point)
    {
        Aim(null);
        levels.Add(Measure(slot.Under!, point, slot));
        since = Environment.TickCount64;
        frames.Start();
        InvalidateVisual();
    }

    /// <summary>Back to the ring before.</summary>
    void Back()
    {
        Aim(null);
        levels.RemoveAt(levels.Count - 1);
        InvalidateVisual();
    }

    /// <summary>Closes the ring: <paramref name="keep"/>, what the field pointed at does comes next and keeps its preview; else the preview goes.</summary>
    void Close(bool keep)
    {
        if (!keep)
            Aim(null);
        rest.Stop();
        arrows.Clear();
        levels.Clear();
        (mode, chosen, pointer) = (Mode.None, null, null);
        IsHitTestVisible = false;
        InvalidateVisual();
    }

    /// <summary>Points at a field, or at none: the text reads as that field would make it.</summary>
    void Aim(int? field)
    {
        if (field == chosen)
            return;
        chosen = field;
        Preview?.Invoke(field is { } k && Current is { } level ? level.Slots[k] : null);
    }

    static double Far(Point a, Point b) => ((Vector)(a - b)).Length;

    /// <summary>The middle angle of a field, clockwise from the right: 0 is up.</summary>
    static double Angle(int field) => (field * 45 - 90) * Math.PI / 180;

    static Vector Way(double angle) => new(Math.Cos(angle), Math.Sin(angle));

    /// <summary>The field of the ring now in the direction of a point from its centre, or null in its middle and on an empty field.</summary>
    int? Pointed(Point point) => Current is { } level ? Field(level.Slots, point - level.Centre, level.Inner - 1) : null;

    /// <summary>The field of a ring in a direction, beyond a distance, or null (an empty field counts as none).</summary>
    static int? Field(IReadOnlyList<Slot> slots, Vector direction, double beyond)
    {
        if (direction.Length < beyond)
            return null;
        var angle = Math.Atan2(direction.Y, direction.X) * 180 / Math.PI + 90;
        var field = ((int)Math.Round(angle / 45) % 8 + 8) % 8;
        return field < slots.Count && !slots[field].IsEmpty ? field : null;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        at = e.GetPosition(this);
        switch (mode)
        {
            case Mode.Waiting when Far(at, start) > BlindDistance:
                rest.Stop();
                mode = Mode.Blind;
                stroke.Add(at);
                break;
            case Mode.Blind:
                stroke.Add(at);
                break;
            case Mode.Holding when Far(at, start) > SelectDistance:
                // Not held but dragged: selecting anew from where it was pressed, as the sheets do.
                var by = pointer;
                Close(keep: false);
                if (by != null)
                    Press?.Invoke(start, by, true);
                return;
            case Mode.Held when Current is { } level:
                Aim(Pointed(at));
                // On out of a field that holds a ring: that ring, where the pointer is.
                if (chosen is { } field && level.Slots[field].Under != null && Far(at, level.Centre) > level.Outer + 6)
                    Enter(level.Slots[field], at);
                break;
            case Mode.Clicking or Mode.Keys when Current is { } level:
                if (mode != Mode.Keys || arrows.Count == 0)
                    Aim(mode == Mode.Clicking && Far(at, level.Centre) > level.Outer + 24 ? null : Pointed(at));
                break;
        }
        if (mode != Mode.None)
        {
            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        at = e.GetPosition(this);
        switch (mode)
        {
            case Mode.Waiting:
                // A short click: the ring stays to be clicked.
                rest.Stop();
                e.Pointer.Capture(null);
                Open(Mode.Clicking, start);
                break;
            case Mode.Holding:
                // A click on the selection, as the sheets take it.
                var by = pointer;
                Close(keep: false);
                e.Pointer.Capture(null);
                if (by != null)
                    Press?.Invoke(start, by, false);
                break;
            case Mode.Blind:
                var blind = Blindly();
                Close(keep: false);
                e.Pointer.Capture(null);
                if (blind is { } slot)
                    Do(slot, at);
                break;
            case Mode.Held when Current is { } level:
                // (The pointer let go last: losing it closes a ring that holds it.)
                if (chosen is { } field && level.Slots[field].Under != null)
                {
                    // Let go on a field that holds a ring: that ring, to be clicked.
                    (mode, IsHitTestVisible) = (Mode.Clicking, true);
                    Enter(level.Slots[field], level.Centre);
                    e.Pointer.Capture(null);
                    break;
                }
                var held = chosen is { } done ? level.Slots[done] : null;
                Close(keep: held != null);
                e.Pointer.Capture(null);
                if (held != null)
                    Do(held, null);
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    /// <summary>
    /// What a blind stroke chose: the field of its direction; where that field holds a ring and the stroke turned aside, the field of the
    /// ring in the direction after the turn (the point of the stroke farthest from the line between its ends).
    /// </summary>
    Slot? Blindly()
    {
        var end = at;
        Vector line = end - start;
        var (corner, aside) = (end, 0.0);
        foreach (var point in stroke)
        {
            var off = line.Length < 1 ? 0 : Math.Abs(line.X * (point.Y - start.Y) - line.Y * (point.X - start.X)) / line.Length;
            if (off > aside)
                (corner, aside) = (point, off);
        }
        var turned = aside > Turn && Far(corner, start) > BlindDistance && Far(end, corner) > BlindDistance;
        if (Field(Slots, (turned ? corner : end) - start, BlindDistance) is not { } first)
            return null;
        var slot = Slots[first];
        if (slot.Under == null)
            return turned ? null : slot;
        return turned && Field(slot.Under, end - corner, BlindDistance) is { } second ? slot.Under[second] : null;
    }

    /// <summary>A click while the ring waits for one: on a field it does that field (one with a ring opens it), in the middle it goes back, elsewhere it closes.</summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (mode != Mode.Clicking || Current is not { } level)
            return;
        e.Handled = true;
        var point = e.GetPosition(this);
        var distance = Far(point, level.Centre);
        if (distance < level.Inner && levels.Count > 1)
        {
            Back();
            return;
        }
        var field = distance <= level.Outer + 4 ? Pointed(point) : null;
        if (field is { } inside && level.Slots[inside].Under != null)
        {
            Enter(level.Slots[inside], level.Centre);
            return;
        }
        Aim(field);
        Close(keep: field != null);
        if (field is { } given)
            Do(level.Slots[given], null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (mode is Mode.Waiting or Mode.Blind or Mode.Held or Mode.Holding)
            Close(keep: false);
    }

    /// <summary>Does what a field does; where it was chosen blindly, its name shows there for a moment.</summary>
    void Do(Slot slot, Point? blindly)
    {
        slot.Act?.Invoke();
        if (blindly is { } point)
        {
            confirmed = (NameOf(slot), point, Environment.TickCount64);
            frames.Start();
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Space went down while the sheets have the keys and a selection: the ring comes at the pointer if it stays down for a moment (see
    /// SpaceUp). Space then types nothing, nor while the ring has another gesture.
    /// </summary>
    public bool SpaceDown(Point pointer)
    {
        if (mode == Mode.None)
            Begin(Mode.KeysWaiting, pointer, null, RestTime);
        return true;
    }

    /// <summary>The pointer moved over the editor: while the ring is up for the keyboard, it chooses too, as long as no arrow is held.</summary>
    public void Follow(Point point)
    {
        if (mode != Mode.Keys || arrows.Count > 0)
            return;
        at = point;
        Aim(Pointed(point));
        InvalidateVisual();
    }

    /// <summary>
    /// Space went up: from the ring, what is chosen is done (nothing in its middle, nor on a field that holds a ring); before the ring
    /// came, nothing happened, and Space is to be typed (false).
    /// </summary>
    public bool SpaceUp()
    {
        if (mode == Mode.KeysWaiting)
        {
            Close(keep: false);
            return false;
        }
        if (mode != Mode.Keys)
            return true;
        var slot = chosen is { } field && Current is { } level ? level.Slots[field] : null;
        var doing = slot is { Act: not null };
        Close(keep: doing);
        if (doing)
            Do(slot!, null);
        return true;
    }

    /// <summary>
    /// A key while the ring has a gesture: the arrows choose for the keyboard (two of them a diagonal), Enter opens the ring of the field
    /// chosen, Backspace goes back; Escape closes any ring. Whether the ring took the key.
    /// </summary>
    public bool KeyChanged(Key key, bool down)
    {
        if (mode == Mode.None)
            return false;
        if (key == Key.Escape)
        {
            if (down)
            {
                pointer?.Capture(null);
                Close(keep: false);
            }
            return true;
        }
        if (mode is not (Mode.Keys or Mode.KeysWaiting))
            return false;
        if (down && mode == Mode.Keys && Current is { } level)
        {
            if (key == Key.Enter && chosen is { } field && level.Slots[field].Under != null)
                Enter(level.Slots[field], level.Centre);
            else if (key == Key.Back && levels.Count > 1)
                Back();
        }
        if (key is Key.Up or Key.Down or Key.Left or Key.Right)
        {
            if (down)
                arrows.Add(key);
            else
                arrows.Remove(key);
            if (down && mode == Mode.Keys && Current is { } now)
            {
                var direction = new Vector(
                    (arrows.Contains(Key.Right) ? 1 : 0) - (arrows.Contains(Key.Left) ? 1 : 0),
                    (arrows.Contains(Key.Down) ? 1 : 0) - (arrows.Contains(Key.Up) ? 1 : 0));
                if (direction != default)
                    Aim(Field(now.Slots, direction, 0.5));
            }
        }
        InvalidateVisual();
        return true;
    }

    /// <summary>A field's name in the language of the interface.</summary>
    string NameOf(Slot slot) => this.TryFindResource(slot.NameKey, out var text) && text is string name ? name : slot.NameKey;

    /// <summary>A colour of the theme: they lie under its variant.</summary>
    IBrush? Brush(string key) => this.TryFindResource(key, ActualThemeVariant, out var brush) ? brush as IBrush : null;

    static readonly FontFamily Inter = new("fonts:Inter#Inter");

    /// <summary>
    /// A sign's outline, its ink centred on the origin and scaled to the size of the others (see SignHeight): a sign is set by the ink it
    /// shows, not by the box its font gives it, in which an exclamation mark stands tall and a quotation mark small and high.
    /// </summary>
    Geometry SignOf(Slot slot)
    {
        if (signs.TryGetValue(slot, out var known))
            return known;
        var typeface = new Typeface(Inter, slot.Slanted ? FontStyle.Italic : FontStyle.Normal, FontWeight.Light);
        var text = new FormattedText(slot.Sign, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 100, Brushes.Black);
        var outline = text.BuildGeometry(default) ?? new StreamGeometry();
        var ink = outline.Bounds;
        var scale = ink.Width <= 0 || ink.Height <= 0 ? 0
            : Math.Min(Math.Min(SignHeight / ink.Height, SignWidth / ink.Width), SignArea / Math.Sqrt(ink.Width * ink.Height));
        outline.Transform = new MatrixTransform(Matrix.CreateTranslation(-ink.Center.X, -ink.Center.Y) * Matrix.CreateScale(scale, scale));
        return signs[slot] = outline;
    }

    /// <summary>The size of a sign's ink as it shows.</summary>
    Size SignSize(Slot slot)
    {
        var outline = SignOf(slot);
        return outline.Bounds.TransformToAABB(outline.Transform?.Value ?? Matrix.Identity).Size;
    }

    /// <summary>
    /// A ring laid out for its signs: a band of fields around an inner circle, each sign in the middle of its field. Where the ring is
    /// about the text in the window, it stands centred on the window and its inner circle goes around it (up to MostInner); else the
    /// inner circle is the middle where letting go does nothing. The band is as wide as lets the widest sign keep Pad from its field's
    /// edges and rims. Wholly in view.
    /// </summary>
    Level Measure(IReadOnlyList<Slot> slots, Point point, Slot? parent)
    {
        var window = Window?.Invoke(start)?.Inflate(new Thickness(5, 3));
        var about = window is { } open && open.Inflate(8).Contains(point);
        // The inner circle around the window, from a centre: through its farthest corner.
        double Around(Point centre, Rect frame) => Math.Clamp(new[] { frame.TopLeft, frame.TopRight, frame.BottomLeft, frame.BottomRight }.Max(c => Far(c, centre)) + 4, Hub, MostInner);
        var centre = about ? window!.Value.Center : point;
        var inner = about ? Around(centre, window!.Value) : Hub;
        // A sign reaches along its field's middle, and towards an edge of its field (a line through the centre at Half on either side
        // of the middle) as far as its half extents along that edge's normal; standing at Middle, it is Middle·sin(Half) from the edge.
        var (middle, along) = (inner, 0.0);
        for (var k = 0; k < 8 && k < slots.Count; k++)
        {
            if (slots[k].IsEmpty)
                continue;
            var (w, h) = (SignSize(slots[k]).Width, SignSize(slots[k]).Height);
            var angle = Angle(k);
            along = Math.Max(along, w / 2 * Math.Abs(Math.Cos(angle)) + h / 2 * Math.Abs(Math.Sin(angle)));
            foreach (var edge in new[] { angle - Half, angle + Half })
                middle = Math.Max(middle, (w / 2 * Math.Abs(Math.Sin(edge)) + h / 2 * Math.Abs(Math.Cos(edge)) + Pad) / Math.Sin(Half));
        }
        middle = Math.Max(middle, inner + Pad + along);
        // The signs in the middle of the band.
        var outer = Math.Max(middle + along + Pad, 2 * middle - inner);
        // Wholly in view, where the gesture began near an edge: its inner circle then goes around the window from where it stands.
        var margin = outer + 8;
        var placed = new Point(Math.Clamp(centre.X, margin, Math.Max(margin, Bounds.Width - margin)), Math.Clamp(centre.Y, margin, Math.Max(margin, Bounds.Height - margin)));
        if (placed != centre && about)
        {
            var grown = Around(placed, window!.Value) - inner;
            (inner, middle, outer) = (inner + grown, middle + grown, outer + grown);
        }
        return new Level(slots, placed, parent, inner, middle, outer);
    }

    public override void Render(DrawingContext context)
    {
        if (IsHitTestVisible)
            context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (mode == Mode.Blind && stroke.Count > 0)
        {
            var trace = new StreamGeometry();
            using (var draw = trace.Open())
            {
                draw.BeginFigure(stroke[0], false);
                foreach (var point in stroke.Skip(1))
                    draw.LineTo(point);
                draw.EndFigure(false);
            }
            context.DrawEllipse(Brush("Accent"), null, start, 3, 3);
            context.DrawGeometry(null, new Pen(Brush("Accent"), 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), trace);
        }
        if (Current is { } level)
            DrawRing(context, level);
        if (confirmed is var (name, point2, given))
        {
            var fading = Math.Clamp((Environment.TickCount64 - given) / Confirmation, 0, 1);
            using (context.PushOpacity(1 - fading * fading))
                Tip(context, name, point2 + new Vector(14, -28), Brush("Accent"), Brush("OnAccent"));
        }
    }

    void DrawRing(DrawingContext context, Level level)
    {
        var appear = Math.Clamp((Environment.TickCount64 - since) / Appear, 0, 1);
        var scale = 0.92 + 0.08 * (1 - Math.Pow(1 - appear, 3));
        var c = level.Centre;
        var (line, ink, faint, accent, onAccent) = (Brush("Hairline"), Brush("Ink"), Brush("InkFaint"), Brush("Accent"), Brush("OnAccent"));
        // Glass of the canvas's tint, so that it shows as a thing of its own on the white page, with a rim a little darker.
        var surface = Brush("Canvas") is ISolidColorBrush { Color: var tint } ? new SolidColorBrush(tint, Glass) : null;
        var clearer = Brush("Canvas") is ISolidColorBrush { Color: var light } ? new SolidColorBrush(light, InnerGlass) : null;
        var hairline = new Pen(line, 1);
        var rim = new Pen(Brush("InkGhost") is ISolidColorBrush { Color: var ghost } ? new SolidColorBrush(ghost, 0.45) : line, 1);
        // The window onto the text: the text's line under the ring shows through it, as it reads now (with the preview).
        var window = Window?.Invoke(start)?.Inflate(new Thickness(5, 3));
        using (context.PushOpacity(appear))
        {
            using (context.PushTransform(Matrix.CreateTranslation(-c.X, -c.Y) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(c.X, c.Y)))
            {
                var everywhere = new RectangleGeometry(new Rect(Bounds.Size).Inflate(400));
                Geometry clip = window is { } cut
                    ? new GeometryGroup { FillRule = FillRule.EvenOdd, Children = new GeometryCollection { everywhere, Rounded(cut) } }
                    : everywhere;
                using (context.PushGeometryClip(clip))
                {
                    // The disc, of glass, raised over the page: the band of fields nearly whole, so that the signs read clearly; the inner
                    // circle clearer, so that the text around the window shows.
                    var inside = new GeometryGroup { FillRule = FillRule.EvenOdd, Children = new GeometryCollection { everywhere, new EllipseGeometry(new Rect(c.X - level.Inner, c.Y - level.Inner, 2 * level.Inner, 2 * level.Inner)) } };
                    using (context.PushGeometryClip(inside))
                        context.DrawRectangle(surface, rim, new RoundedRect(new Rect(c.X - level.Outer, c.Y - level.Outer, 2 * level.Outer, 2 * level.Outer), level.Outer),
                            new BoxShadows(new BoxShadow { OffsetY = 10, Blur = 28, Color = Color.FromArgb(0x1E, 0, 0, 0) },
                                [new BoxShadow { OffsetY = 2, Blur = 6, Color = Color.FromArgb(0x12, 0, 0, 0) }]));
                    context.DrawEllipse(clearer, null, c, level.Inner, level.Inner);
                    // The band of fields: its inner rim, and the edges between the fields.
                    context.DrawEllipse(null, hairline, c, level.Inner, level.Inner);
                    for (var k = 0; k < 8; k++)
                    {
                        var edge = Angle(k) + Half;
                        context.DrawLine(hairline, c + Way(edge) * level.Inner, c + Way(edge) * (level.Outer - 6));
                    }
                    if (chosen is { } pointed)
                        context.DrawGeometry(accent, null, Sector(c, level.Inner + Gap, level.Outer - Gap, Angle(pointed), Gap));
                }
                if (window is { } frame)
                    context.DrawRectangle(null, new Pen(chosen != null ? accent : line, chosen != null ? 1.5 : 1), new RoundedRect(frame, Math.Min(7, frame.Height / 2)));
                for (var k = 0; k < 8 && k < level.Slots.Count; k++)
                {
                    var slot = level.Slots[k];
                    if (slot.IsEmpty)
                        continue;
                    var place = c + Way(Angle(k)) * level.Middle;
                    var size = SignSize(slot);
                    // A sign that has to stand over the window stands on a spot of the ring's surface.
                    if (window is { } open && open.Intersects(new Rect(place - new Vector(size.Width / 2, size.Height / 2), size)))
                        context.DrawEllipse(chosen == k ? accent : Brush("Sheet"), hairline, place, Math.Max(size.Width, size.Height) / 2 + 5, Math.Max(size.Width, size.Height) / 2 + 5);
                    using (context.PushTransform(Matrix.CreateTranslation(place.X, place.Y)))
                        context.DrawGeometry(chosen == k ? onAccent : ink, null, SignOf(slot));
                    // A field that holds a ring: a chevron at its outer edge, pointing on.
                    if (slot.Under != null)
                    {
                        var (tip, back, side) = (c + Way(Angle(k)) * (level.Outer - 5), c + Way(Angle(k)) * (level.Outer - 10), Way(Angle(k) + Math.PI / 2) * 3.5);
                        var chevron = new Pen(chosen == k ? onAccent : faint, 1.5, lineCap: PenLineCap.Round);
                        context.DrawLine(chevron, back + side, tip);
                        context.DrawLine(chevron, back - side, tip);
                    }
                }
                // A ring of a second level away from the window: the sign of the field that holds it, in its middle.
                if (level.Parent is { } parent && (window is not { } over || !over.Intersects(new Rect(c.X - Hub / 2, c.Y - Hub / 2, Hub, Hub))))
                    using (context.PushTransform(Matrix.CreateScale(0.7, 0.7) * Matrix.CreateTranslation(c.X, c.Y)))
                        context.DrawGeometry(faint, null, SignOf(parent));
            }
        }
        // The name of the field pointed at, beside the ring in its direction.
        if (chosen is { } named)
        {
            var way = Way(Angle(named));
            var label = Layout(NameOf(level.Slots[named]), Brush("TipInk"));
            var (w, h) = (label.WidthIncludingTrailingWhitespace + 16, label.Height + 8);
            var reach = w / 2 * Math.Abs(way.X) + h / 2 * Math.Abs(way.Y);
            var middle = c + way * (level.Outer + 8 + reach);
            using (context.PushOpacity(appear))
                Tip(context, label, new Point(middle.X - w / 2, middle.Y - h / 2), Brush("TipSurface"));
        }
    }

    static StreamGeometry Rounded(Rect rect)
    {
        var radius = Math.Min(7, rect.Height / 2);
        var geometry = new StreamGeometry();
        using var draw = geometry.Open();
        draw.BeginFigure(new Point(rect.Left + radius, rect.Top), true);
        draw.LineTo(new Point(rect.Right - radius, rect.Top));
        draw.ArcTo(new Point(rect.Right, rect.Top + radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
        draw.LineTo(new Point(rect.Right, rect.Bottom - radius));
        draw.ArcTo(new Point(rect.Right - radius, rect.Bottom), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
        draw.LineTo(new Point(rect.Left + radius, rect.Bottom));
        draw.ArcTo(new Point(rect.Left, rect.Bottom - radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
        draw.LineTo(new Point(rect.Left, rect.Top + radius));
        draw.ArcTo(new Point(rect.Left + radius, rect.Top), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
        draw.EndFigure(true);
        return geometry;
    }

    /// <summary>
    /// A field less a gap of even width along its edges and rims: the part of the ring between two radii and the lines a gap inside the
    /// field's edges (radians, clockwise from the right).
    /// </summary>
    static StreamGeometry Sector(Point centre, double inner, double outer, double middle, double gap)
    {
        Point At(double radius, double angle) => centre + new Vector(Math.Cos(angle), Math.Sin(angle)) * radius;
        double Inset(double radius) => Math.Asin(Math.Min(1, gap / radius));
        var geometry = new StreamGeometry();
        using var draw = geometry.Open();
        draw.BeginFigure(At(outer, middle - Half + Inset(outer)), true);
        draw.ArcTo(At(outer, middle + Half - Inset(outer)), new Size(outer, outer), 0, false, SweepDirection.Clockwise);
        draw.LineTo(At(inner, middle + Half - Inset(inner)));
        draw.ArcTo(At(inner, middle - Half + Inset(inner)), new Size(inner, inner), 0, false, SweepDirection.CounterClockwise);
        draw.EndFigure(true);
        return geometry;
    }

    static TextLayout Layout(string text, IBrush? ink) => new(text, new Typeface(Inter, FontStyle.Normal, FontWeight.SemiBold), 11.5, ink);

    void Tip(DrawingContext context, string text, Point at, IBrush? fill, IBrush? ink) => Tip(context, Layout(text, ink), at, fill);

    static void Tip(DrawingContext context, TextLayout label, Point at, IBrush? fill)
    {
        var box = new Rect(at.X, at.Y, label.WidthIncludingTrailingWhitespace + 16, label.Height + 8);
        context.DrawRectangle(fill, null, new RoundedRect(box, box.Height / 2), new BoxShadows(new BoxShadow { OffsetY = 3, Blur = 10, Color = Color.FromArgb(0x26, 0, 0, 0) }));
        label.Draw(context, new Point(box.X + 8, box.Y + 4));
    }
}
