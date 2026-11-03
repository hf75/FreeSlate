using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;

namespace FreeSlate.Editor.Controls;

/// <summary>
/// The gesture ring that gives text a meaning: eight fields around a point, one direction each, their places fixed so that the hand
/// learns them (see Slots). The right button held on the selection, or on a word where there is none, brings it after a moment of rest;
/// moved into a field and let go there, it gives that field's meaning, let go in the middle it gives none. Moved on at once beyond a few
/// pixels, it gives the meaning of the direction without a ring: a thin trace, and the meaning's name for a moment to confirm it. A
/// short click opens it to be clicked. The left button, a finger or a pen held on the selection brings it too, for pointers without a
/// right button. With the keyboard, Space held on a selection brings it at the pointer: the arrows choose, two together a diagonal,
/// letting go of Space gives, Escape gives none. It lies over the editor and takes the pointer only while it has a gesture.
/// </summary>
public sealed class MeaningRing : Control
{
    /// <summary>How long the pointer rests before the ring shows, and how long the left button is held on the selection for it, in milliseconds.</summary>
    const double RestTime = 230, HoldTime = 450;

    /// <summary>How far the pointer goes before the ring shows to choose blindly, and how far it goes before a press on the selection selects anew.</summary>
    const double BlindDistance = 15, SelectDistance = 4;

    /// <summary>The ring's middle, where letting go gives nothing, and its outer edge, in pixels from its centre.</summary>
    const double Middle = 35, Outer = 98;

    /// <summary>How long the ring takes to come, and how long the name of a meaning given blindly stays, in milliseconds.</summary>
    const double Appear = 110, Confirmation = 900;

    /// <summary>A field of the ring: the character style it gives ("" takes the meaning away, null: an empty field), its name's key and its sign.</summary>
    public sealed record Slot(string? Style, string NameKey, string Sign, bool Slanted = false);

    enum Mode { None, Waiting, Blind, Held, Clicking, Holding, KeysWaiting, Keys }

    readonly DispatcherTimer rest = new();
    readonly DispatcherTimer frames = new() { Interval = TimeSpan.FromMilliseconds(15) };
    readonly HashSet<Key> arrows = [];
    Mode mode;
    Point start, at, centre;
    IPointer? pointer;
    int? chosen;
    long since;
    (string Name, Point At, long Since)? confirmed;

    public MeaningRing()
    {
        IsHitTestVisible = false;
        rest.Tick += (_, _) => Rested();
        frames.Tick += (_, _) =>
        {
            var now = Environment.TickCount64;
            if (confirmed is { } c && now - c.Since > Confirmation)
                confirmed = null;
            if (confirmed == null && (mode is not (Mode.Held or Mode.Clicking or Mode.Keys) || now - since > Appear))
                frames.Stop();
            InvalidateVisual();
        };
    }

    /// <summary>The eight fields, from the top clockwise: up, up right, right, down right, down, down left, left, up left.</summary>
    public IReadOnlyList<Slot> Slots { get; set; } = [];

    /// <summary>
    /// Makes the text at a point of the ring's (the editor's) coordinates the one to give a meaning: the selection where the point is on
    /// it, else the word there, which it selects; false where there is no text to give one.
    /// </summary>
    public Func<Point, bool>? Target { get; set; }

    /// <summary>Whether a point of the ring's coordinates is on the selection.</summary>
    public Func<Point, bool>? OnSelection { get; set; }

    /// <summary>Gives the selection a meaning by its character style ("" takes it away).</summary>
    public Action<string>? Give { get; set; }

    /// <summary>A press of the left button on the selection that was none for the ring: a click there (false) or the start of selecting (true), as the sheets take them.</summary>
    public Action<Point, IPointer, bool>? Press { get; set; }

    /// <summary>Whether the ring has a gesture going: it takes the pointer, Escape takes it back.</summary>
    public bool IsBusy => mode != Mode.None;

    /// <summary>Takes the presses on the sheets the ring answers: the right button (Ctrl and the button on a Mac), and the left one on the selection.</summary>
    public void Attach(Control sheets) => sheets.AddHandler(PointerPressedEvent, Pressed, RoutingStrategies.Tunnel);

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
        // Wholly in view, where the gesture began near an edge.
        centre = new Point(Math.Clamp(point.X, Outer + 8, Math.Max(Outer + 8, Bounds.Width - Outer - 8)), Math.Clamp(point.Y, Outer + 8, Math.Max(Outer + 8, Bounds.Height - Outer - 8)));
        chosen = mode == Mode.Keys ? null : Pointed(at);
        since = Environment.TickCount64;
        IsHitTestVisible = mode == Mode.Clicking;
        frames.Start();
        InvalidateVisual();
    }

    void Close()
    {
        rest.Stop();
        arrows.Clear();
        (mode, chosen, pointer) = (Mode.None, null, null);
        IsHitTestVisible = false;
        InvalidateVisual();
    }

    static double Far(Point a, Point b) => ((Vector)(a - b)).Length;

    /// <summary>The field in the direction of a point from the ring's centre, or null in its middle (an empty field counts as none).</summary>
    int? Pointed(Point point) => Field(point - centre, Middle);

    /// <summary>The field in a direction, beyond a distance: 0 up, then clockwise.</summary>
    int? Field(Vector direction, double beyond)
    {
        if (direction.Length < beyond)
            return null;
        var angle = Math.Atan2(direction.Y, direction.X) * 180 / Math.PI + 90;
        var field = (int)Math.Round(angle / 45);
        field = (field % 8 + 8) % 8;
        return field < Slots.Count && Slots[field].Style != null ? field : null;
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
                break;
            case Mode.Holding when Far(at, start) > SelectDistance:
                // Not held but dragged: selecting anew from where it was pressed, as the sheets do.
                var by = pointer;
                Close();
                if (by != null)
                    Press?.Invoke(start, by, true);
                return;
            case Mode.Held or Mode.Clicking or Mode.Keys:
                if (mode != Mode.Keys || arrows.Count == 0)
                    chosen = mode == Mode.Clicking && Far(at, centre) > Outer + 24 ? null : Pointed(at);
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
                Close();
                e.Pointer.Capture(null);
                if (by != null)
                    Press?.Invoke(start, by, false);
                break;
            case Mode.Blind:
                var blind = Field(at - start, BlindDistance);
                Close();
                e.Pointer.Capture(null);
                if (blind is { } field)
                    Choose(field, at);
                break;
            case Mode.Held:
                var held = chosen;
                Close();
                e.Pointer.Capture(null);
                if (held is { } field2)
                    Choose(field2, null);
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    /// <summary>A click while the ring waits for one: on a field it gives that meaning, elsewhere it closes the ring.</summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (mode != Mode.Clicking)
            return;
        var field = Far(e.GetPosition(this), centre) <= Outer + 4 ? Pointed(e.GetPosition(this)) : null;
        Close();
        if (field is { } given)
            Choose(given, null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (mode is Mode.Waiting or Mode.Blind or Mode.Held or Mode.Holding)
            Close();
    }

    /// <summary>Gives the meaning of a field; where it was given blindly, its name shows there for a moment.</summary>
    void Choose(int field, Point? blindly)
    {
        if (Slots[field].Style is not { } style)
            return;
        Give?.Invoke(style);
        if (blindly is { } point)
        {
            confirmed = (Name(Slots[field]), point, Environment.TickCount64);
            frames.Start();
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Space went down while the sheets have the keys and a selection: the ring comes at the pointer if it stays down for a moment (see
    /// SpaceUp). True while the ring keeps Space, which then types nothing.
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
        chosen = Pointed(point);
        InvalidateVisual();
    }

    /// <summary>
    /// Space went up: from the ring, the meaning chosen is given (none in its middle); before the ring came, nothing happened, and Space
    /// is to be typed (false).
    /// </summary>
    public bool SpaceUp()
    {
        if (mode == Mode.KeysWaiting)
        {
            Close();
            return false;
        }
        if (mode != Mode.Keys)
            return true;
        var field = chosen;
        Close();
        if (field is { } given)
            Choose(given, null);
        return true;
    }

    /// <summary>A key while the ring is up for the keyboard: the arrows choose, two of them a diagonal; Escape closes any ring. Whether the ring took it.</summary>
    public bool KeyChanged(Key key, bool down)
    {
        if (mode == Mode.None)
            return false;
        if (key == Key.Escape)
        {
            if (down)
            {
                pointer?.Capture(null);
                Close();
            }
            return true;
        }
        if (mode is not (Mode.Keys or Mode.KeysWaiting) || key is not (Key.Up or Key.Down or Key.Left or Key.Right))
            return mode is Mode.Keys or Mode.KeysWaiting;
        if (down)
            arrows.Add(key);
        else
            arrows.Remove(key);
        if (down && mode == Mode.Keys)
        {
            var direction = new Vector(
                (arrows.Contains(Key.Right) ? 1 : 0) - (arrows.Contains(Key.Left) ? 1 : 0),
                (arrows.Contains(Key.Down) ? 1 : 0) - (arrows.Contains(Key.Up) ? 1 : 0));
            if (direction != default)
                chosen = Field(direction * Outer, 1);
            InvalidateVisual();
        }
        return true;
    }

    /// <summary>A slot's name in the language of the interface.</summary>
    string Name(Slot slot) => this.TryFindResource(slot.NameKey, out var text) && text is string name ? name : slot.Style ?? "";

    /// <summary>A colour of the theme: they lie under its variant.</summary>
    IBrush? Brush(string key) => this.TryFindResource(key, ActualThemeVariant, out var brush) ? brush as IBrush : null;

    public override void Render(DrawingContext context)
    {
        if (IsHitTestVisible)
            context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (mode == Mode.Blind)
        {
            var pen = new Pen(Brush("Accent"), 2) { LineCap = PenLineCap.Round };
            context.DrawEllipse(Brush("Accent"), null, start, 3, 3);
            context.DrawLine(pen, start, at);
        }
        if (mode is Mode.Held or Mode.Clicking or Mode.Keys)
            DrawRing(context);
        if (confirmed is var (name, point, given))
        {
            var fading = Math.Clamp((Environment.TickCount64 - given) / Confirmation, 0, 1);
            using (context.PushOpacity(1 - fading * fading))
                Pill(context, name, point + new Vector(14, -26), Brush("Accent"), Brush("OnAccent"));
        }
    }

    void DrawRing(DrawingContext context)
    {
        var appear = Math.Clamp((Environment.TickCount64 - since) / Appear, 0, 1);
        var scale = 0.9 + 0.1 * (1 - Math.Pow(1 - appear, 3));
        using var opacity = context.PushOpacity(appear);
        using var transform = context.PushTransform(Matrix.CreateTranslation(-centre.X, -centre.Y) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(centre.X, centre.Y));
        var (surface, line, ink, faint, accent, onAccent) = (Brush("Sheet"), Brush("Hairline"), Brush("InkSoft"), Brush("InkGhost"), Brush("Accent"), Brush("OnAccent"));
        // The disc, raised over the page.
        context.DrawRectangle(surface, new Pen(line, 1), new RoundedRect(new Rect(centre.X - Outer, centre.Y - Outer, 2 * Outer, 2 * Outer), Outer),
            new BoxShadows(new BoxShadow { OffsetY = 6, Blur = 24, Color = Color.FromArgb(0x30, 0, 0, 0) }));
        var typeface = new Typeface(new FontFamily("fonts:Inter#Inter"), FontStyle.Normal, FontWeight.SemiBold);
        for (var k = 0; k < 8 && k < Slots.Count; k++)
        {
            var slot = Slots[k];
            var middle = (k * 45 - 90) * Math.PI / 180;
            var isChosen = chosen == k;
            if (isChosen)
                context.DrawGeometry(accent, null, Sector(centre, Middle + 3, Outer - 3, middle - 21 * Math.PI / 180, middle + 21 * Math.PI / 180));
            // The lines between the fields.
            var edge = middle + 22.5 * Math.PI / 180;
            context.DrawLine(new Pen(line, 1), centre + new Vector(Math.Cos(edge), Math.Sin(edge)) * (Middle + 2), centre + new Vector(Math.Cos(edge), Math.Sin(edge)) * (Outer - 2));
            var place = centre + new Vector(Math.Cos(middle), Math.Sin(middle)) * ((Middle + Outer) / 2 + 2);
            if (slot.Style == null)
            {
                context.DrawEllipse(faint, null, place, 1.5, 1.5);
                continue;
            }
            var colour = isChosen ? onAccent : ink;
            var sign = new TextLayout(slot.Sign, new Typeface(typeface.FontFamily, slot.Slanted ? FontStyle.Italic : FontStyle.Normal, FontWeight.SemiBold), 17, colour);
            var label = new TextLayout(Name(slot), typeface, 10, colour, textAlignment: TextAlignment.Center, maxWidth: 62, textWrapping: TextWrapping.Wrap);
            var height = sign.Height + label.Height - 2;
            sign.Draw(context, new Point(place.X - sign.WidthIncludingTrailingWhitespace / 2, place.Y - height / 2));
            label.Draw(context, new Point(place.X - 31, place.Y - height / 2 + sign.Height - 2));
        }
        // The middle: where letting go gives nothing; the name of the meaning chosen.
        context.DrawEllipse(surface, new Pen(line, 1), centre, Middle - 2, Middle - 2);
        if (chosen is { } k2)
        {
            var name = new TextLayout(Name(Slots[k2]), typeface, 10.5, Brush("Ink"), textAlignment: TextAlignment.Center, maxWidth: 2 * Middle - 10, textWrapping: TextWrapping.Wrap);
            name.Draw(context, new Point(centre.X - (Middle - 5), centre.Y - name.Height / 2));
        }
    }

    /// <summary>A field: the part of the ring between two angles (radians, clockwise from the right) and two radii.</summary>
    static StreamGeometry Sector(Point centre, double inner, double outer, double from, double to)
    {
        Point At(double radius, double angle) => centre + new Vector(Math.Cos(angle), Math.Sin(angle)) * radius;
        var geometry = new StreamGeometry();
        using var draw = geometry.Open();
        draw.BeginFigure(At(outer, from), true);
        draw.ArcTo(At(outer, to), new Size(outer, outer), 0, false, SweepDirection.Clockwise);
        draw.LineTo(At(inner, to));
        draw.ArcTo(At(inner, from), new Size(inner, inner), 0, false, SweepDirection.CounterClockwise);
        draw.EndFigure(true);
        return geometry;
    }

    void Pill(DrawingContext context, string text, Point at, IBrush? fill, IBrush? ink)
    {
        var label = new TextLayout(text, new Typeface(new FontFamily("fonts:Inter#Inter"), FontStyle.Normal, FontWeight.SemiBold), 11.5, ink);
        var box = new Rect(at.X, at.Y, label.WidthIncludingTrailingWhitespace + 16, label.Height + 8);
        context.DrawRectangle(fill, null, new RoundedRect(box, box.Height / 2), new BoxShadows(new BoxShadow { OffsetY = 3, Blur = 10, Color = Color.FromArgb(0x26, 0, 0, 0) }));
        label.Draw(context, new Point(box.X + 8, box.Y + 4));
    }
}
