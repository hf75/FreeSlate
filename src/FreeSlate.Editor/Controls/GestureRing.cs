using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.Utilities;

namespace FreeSlate.Editor.Controls;

/// <summary>
/// The gesture ring, a marking menu: its fields share the circle around a point, one direction each, in an order that stays so that the
/// hand learns them (see Slots); a field may hold a ring of its own, a second level. The ring is always of one size; its fields show
/// signs only, with room all around them, and the name of the field pointed at stands beside the ring. It lies over everything, of
/// glass, its middle clear: the text it acts on stands there as far as it can (see Measure), framed exactly around all of it, under
/// the ring where the ring lies over it, and reads as the field pointed at would make it (Preview). The middle of a ring of a second
/// level is the way back to the ring before: the sign of the field that holds it stands there while no field is pointed at, and it
/// lights up as a field does where the pointer is in it. Where that field says so (Slot.Settles), the middle is split as the ring is: its
/// left half keeps what the ring shows (a tick), its right half takes it away (a cross), either closing the ring, not going back; the two
/// there while a field is pointed at too, so that the way out is always in view; a new table's size stepped.
/// What it acts on may have fields of its own (see Choose), a picture its places among the text.
/// A field may be pulled (see Slot.Pulls), a picture's side say: pressed and dragged, it goes as far as the pointer goes from where it
/// took it, what that makes standing beside the ring; let go, that stays and the ring with it, to be pulled again; Escape takes the pull
/// back. Its name beside the ring says so (RingPull). Held, moved on out of such a field, the pull begins where the pointer leaves the ring.
/// The right button held on the selection, or on a word where there is none, brings it after a moment of rest; moved into a field and
/// let go there, it does what the field does, let go in the middle nothing; moved on out of a field that holds a ring, that ring comes
/// where the pointer is, and let go in its middle it stays to be clicked (once the pointer was on its fields, it goes back, or closes on a
/// split middle). Moved on at once beyond a few pixels, it does what the direction does without a ring (a turn and a second direction
/// for a second level), with a thin trace and the field's name for a moment. A short click opens it to be clicked (a click on a field
/// with a ring opens that ring, one in the middle goes back, as Backspace does, or closes the ring on a split middle); pressed in its
/// middle and moved on, it goes with the pointer, the rings before it along. The left button, a finger or a pen held on the selection
/// brings it too, where there is no right button. With the keyboard, Space held on a selection brings it at the pointer: the arrows
/// choose the field nearest their direction (two together a diagonal), Enter opens a field's ring, Backspace goes back, letting go of
/// Space does what is chosen, Escape nothing. It lies over the editor and takes the pointer only while it has a gesture.
/// A ring comes and goes as the cards over the sheets do (see Motion): closed, it goes as it showed, the field done lit, the frame around
/// the text with it; a ring giving way to another goes as that one comes, the frame staying.
/// </summary>
public sealed class GestureRing : Control
{
    /// <summary>How long the pointer rests before the ring shows, and how long the left button is held on the selection for it, in milliseconds.</summary>
    const double RestTime = 230, HoldTime = 450;

    /// <summary>
    /// How far the pointer goes before the ring shows to choose blindly, and before a press on the selection selects anew (or one in the
    /// middle of a ring to be clicked moves it); how far a blind stroke turns aside for a second level; how near to where the gesture
    /// began it chooses nothing on the first ring.
    /// </summary>
    const double BlindDistance = 15, SelectDistance = 4, Turn = 12, Still = 6;

    /// <summary>
    /// The ring's radii, the same always: its inner circle, the middle, clear, where letting go does nothing, and its outer edge; the
    /// signs stand halfway between them, with room all around.
    /// </summary>
    const double Inner = 34, Outer = 90, Middle = (Inner + Outer) / 2;

    /// <summary>The room around the field the pointer is in, which shows it, and the round of its corners.</summary>
    const double Gap = 5, Round = 7;

    /// <summary>The way back in the middle of a ring of a second level: the radius of its button of glass, and its sign's size beside the fields'.</summary>
    const double Hub = 14, HubSign = 0.75;

    /// <summary>How much the glass of the band of fields covers.</summary>
    const double Glass = 0.94;

    /// <summary>
    /// A field's sign, its ink in pixels: no taller than SignHeight, no wider than SignWidth, and of about the area of a square of
    /// SignArea, so that signs of every shape look alike in size.
    /// </summary>
    const double SignHeight = 16, SignWidth = 22, SignArea = 14;

    /// <summary>The radius of a swatch's disc, about as large as a sign.</summary>
    const double SwatchRadius = 9;

    /// <summary>How long the name of a field chosen blindly stays, in milliseconds.</summary>
    const double Confirmation = 900;

    /// <summary>How small a ring is as it begins to come and as it is gone (see Motion), a share of its size.</summary>
    const double Small = 0.92;

    /// <summary>
    /// A field of the ring: its name's key among the texts (its name where there is none), its sign, and what it does or the ring it
    /// holds. A sign is a few letters, or an icon: path data, drawn as it is given, centred; or a swatch, a disc of a colour. A field that
    /// steps does what it does and its ring stays, to be clicked again (a count up or down). A field that holds a ring may split it: its
    /// fields lie in two halves, right and left of a line through its middle, from the top round (one more up, one fewer down on each
    /// side, as a stepper has them); and it may show something of its own while that ring shows (Opened as it comes; Left as it goes,
    /// kept where it closed beside the ring, by a click there, Enter or letting go there, else taken back: Escape, the way back), with a
    /// line about it under the ring while no field is pointed at (Status); and the middle of its ring may be split, its left half keeping
    /// what the ring shows, its right half taking it away, either closing the ring (Settles). A field may be pulled instead of doing
    /// something (Pulls): what it gives as it is taken (null: nothing to pull now) goes as far as the pointer goes from there (see IPull).
    /// </summary>
    public sealed record Slot(string NameKey, string Sign, Action? Act = null, IReadOnlyList<Slot>? Under = null, bool Slanted = false, string? Icon = null,
        Color? Swatch = null, bool Steps = false, bool Split = false, Action? Opened = null, Action<bool>? Left = null, Func<string>? Status = null,
        bool Settles = false, Func<IPull?>? Pulls = null);

    /// <summary>
    /// A field pulled (see Slot.Pulls): told how far the pointer is from where it took the field as it goes, the keys held, and at the end
    /// whether what it made stays; what it makes, which stands beside the ring while it goes (null: the field's name), and the pointer then.
    /// </summary>
    public interface IPull
    {
        void To(Vector by, KeyModifiers modifiers);

        void End(bool keep);

        string? Reading { get; }

        Cursor? Cursor { get; }
    }

    /// <summary>
    /// The ring talking with Claude (see Ask): what it says beside the ring while nothing is typed (what Claude is doing), Claude's words in
    /// its last answer, whether Claude is at work, and when any of that changes; a further wish sent, the changes kept or taken back.
    /// </summary>
    public interface IAsked
    {
        string Line { get; }

        string? Answer { get; }

        bool Working { get; }

        /// <summary>Whether the talk changed the document: only then is there something to keep or take back.</summary>
        bool Changed { get; }

        event Action? Updated;

        void Send(string wish);

        void Settle(bool keep);
    }

    /// <summary>A ring as it shows: its fields, its centre, and the field of the ring before it that holds it (whose split and middle it has).</summary>
    sealed record Level(IReadOnlyList<Slot> Slots, Point Centre, Slot? Parent)
    {
        public bool Split => Parent?.Split == true;

        /// <summary>Whether its middle is split, keeping what it shows on the left and taking it away on the right, either closing the ring (see Slot.Settles).</summary>
        public bool Settles => Parent?.Settles == true;
    }

    /// <summary>
    /// A ring as it is drawn: its level, the field pointed at, whether its way back is pointed at (and the half that keeps), whether it is
    /// moved, and the name beside it. A ring going has the time of the clock it began to go; one closed, the frame around the text it
    /// acted on, which goes with it (one giving way to another leaves its frame to that).
    /// </summary>
    sealed record Look(Level Level, int? Chosen, bool Backward, bool Keeping, bool Moving, string? Label)
    {
        public long Left { get; init; }
        public Framing? Frame { get; init; }

        /// <summary>
        /// Whether the ring talks with Claude (its fields away), Claude is at work, and there is something to keep or take back (its middle
        /// split then).
        /// </summary>
        public bool Talking { get; init; }
        public bool Working { get; init; }
        public bool Settling { get; init; }
    }

    /// <summary>The frame around the text a ring acts on: the text's parts, or a drawing's frame turned; in the accent where a field is pointed at.</summary>
    sealed record Framing(IReadOnlyList<Rect> Parts, (Rect Frame, double Turn)? Turned, bool Lit);

    /// <summary>
    /// The signs of a split middle (see Slot.Settles), their ink about the origin: a filled tick for keeping what the ring shows, a filled
    /// cross for taking it away.
    /// </summary>
    static readonly Geometry Tick = Geometry.Parse("M-7.4,0.4 L-5,-2 L-1.8,1.2 L5,-5.6 L7.4,-3.2 L-1.8,6 Z");
    static readonly Geometry Cross = Geometry.Parse("M-5.6,-4 L-4,-5.6 L0,-1.6 L4,-5.6 L5.6,-4 L1.6,0 L5.6,4 L4,5.6 L0,1.6 L-4,5.6 L-5.6,4 L-1.6,0 Z");

    enum Mode { None, Waiting, Blind, Held, Clicking, Gripped, Moving, Holding, KeysWaiting, Keys, Pulling, Asking }

    /// <summary>How long the ring takes to go aside as Claude begins to write (ms), the widest the line beside it grows, and Claude's card.</summary>
    const double GlideTime = 320, LineWidest = 460, LineNarrowest = 240, CardWidth = 400;

    /// <summary>Claude's words that fit on the line beside the ring; longer ones go on a card under it.</summary>
    const int ShortAnswer = 64;

    /// <summary>The pointer while it moves the ring, as while it moves a picture.</summary>
    static readonly Cursor Grab = new(StandardCursorType.SizeAll);

    readonly DispatcherTimer rest = new();
    readonly Frames frames;
    readonly HashSet<Key> arrows = [];
    readonly List<Level> levels = [];
    /// <summary>Rings going (see Motion), the last one latest.</summary>
    readonly List<Look> leaving = [];
    readonly List<Point> stroke = [];
    readonly Dictionary<Slot, Geometry> signs = [];
    /// <summary>The fields of the first ring of the gesture going (see Choose).</summary>
    IReadOnlyList<Slot> first = [];
    Mode mode;
    Point start, at;
    IPointer? pointer;
    int? chosen;
    /// <summary>
    /// Whether the way back is pointed at; and whether the pointer was on a field of the ring now, so that the held pointer finds the
    /// way back in the middle of a ring that came where it is only once it went on from there.
    /// </summary>
    bool backward, visited;
    /// <summary>Whether the pointer is in the left half of a split middle, which keeps what the ring shows (see Slot.Settles).</summary>
    bool keeping;
    /// <summary>Where the middle of a ring to be clicked was pressed, and where its centre stood then: moved on, the ring goes with the pointer.</summary>
    (Point At, Point Centre) grip;
    /// <summary>A field pulled (see Slot.Pulls), and where the pointer took it.</summary>
    IPull? pull;
    Point pulledAt;
    /// <summary>When the ring there came, and when the first ring of the gesture did, with the frame around the text (on Motion's clock).</summary>
    long since, opened;
    (string Name, Point At, long Since)? confirmed;
    /// <summary>
    /// What is typed at the ring for Claude, and the cursor in it; the talk it began, and since when; what the line says where Claude
    /// cannot be reached; where the line and Claude's card stand, as last drawn.
    /// </summary>
    string typed = "";
    int typedAt;
    IAsked? asked;
    long talking;
    string? unreached;
    Rect lineBox;
    Rect? cardBox;
    /// <summary>The wishes sent while the editor runs, the last last, which ↑ brings back; the one brought back.</summary>
    static readonly List<string> Wishes = [];
    int recalled = -1;
    /// <summary>Where the ring goes aside from and to as Claude begins to write, and since when.</summary>
    (Point From, Point To, long Since)? glide;

    public GestureRing()
    {
        IsHitTestVisible = false;
        frames = new Frames(this, Step);
        rest.Tick += (_, _) => Rested();
    }

    /// <summary>A frame while something of the ring moves: it is drawn again; false once nothing moves.</summary>
    bool Step()
    {
        InvalidateVisual();
        leaving.RemoveAll(look => Motion.Going(look.Left) <= 0);
        if (confirmed is { } c && Motion.Since(c.Since) > Confirmation)
            confirmed = null;
        if (glide is var (from, to, began) && levels.Count > 0)
        {
            var t = Math.Clamp(Motion.Since(began) / GlideTime, 0, 1);
            levels[^1] = levels[^1] with { Centre = from + (to - from) * (1 - Math.Pow(1 - t, 3)) };
            if (t >= 1)
                glide = null;
        }
        return leaving.Count > 0 || confirmed != null || Current != null && Motion.Coming(since) < 1 || glide != null || asked?.Working == true;
    }

    /// <summary>The fields of the first ring, from the top clockwise; they share the circle evenly.</summary>
    public IReadOnlyList<Slot> Slots { get; set; } = [];

    /// <summary>
    /// The fields of the first ring for what the gesture acts on where they are others than Slots (a picture's, say), asked as it
    /// begins, once Target has chosen what it acts on; null keeps Slots.
    /// </summary>
    public Func<IReadOnlyList<Slot>?>? Choose { get; set; }

    /// <summary>
    /// Makes the text at a point of the ring's (the editor's) coordinates the one a field acts on: the selection where the point is on it,
    /// else the word there, which it selects; false where there is no text to act on.
    /// </summary>
    public Func<Point, bool>? Target { get; set; }

    /// <summary>The parts of the text a field acts on, one for each line in the order of the text, in the ring's coordinates: what the ring frames.</summary>
    public Func<IReadOnlyList<Rect>>? Selection { get; set; }

    /// <summary>
    /// Where the selection is a drawing shown turned: its frame before it turns, in the ring's coordinates, and how far it is turned
    /// clockwise about its middle, in degrees; the ring frames it turned so (instead of Selection's parts). Null otherwise.
    /// </summary>
    public Func<(Rect Frame, double Turn)?>? Turned { get; set; }

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

    /// <summary>Whether the ring asks Claude (see Ask): what Claude writes is framed whole, not line by line.</summary>
    public bool IsAsking => mode == Mode.Asking;

    /// <summary>
    /// Where a wish typed at the ring goes as Enter sends it: the talk with Claude it begins (see IAsked), or null where Claude cannot be
    /// reached, Unreachable then saying why on the line beside the ring.
    /// </summary>
    public Func<string, IAsked?>? Ask { get; set; }

    public Func<string>? Unreachable { get; set; }

    /// <summary>Where the ring goes as Claude begins to write, given where it stands, so that what is written shows: beside the pages; null to stay.</summary>
    public Func<Point, Point?>? Aside { get; set; }

    /// <summary>The wheel turned over the ring while it talks with Claude: the sheets scroll under it.</summary>
    public Action<PointerWheelEventArgs>? Wheel { get; set; }

    /// <summary>Takes the presses on the sheets the ring answers: the right button (Ctrl and the button on a Mac), and the left one on the selection.</summary>
    public void Attach(Control sheets) => sheets.AddHandler(PointerPressedEvent, Pressed, RoutingStrategies.Tunnel);

    Level? Current => levels.Count > 0 ? levels[^1] : null;

    /// <summary>The field whose ring shows, a ring of a second level or further; null for the first ring, or none.</summary>
    public Slot? Holder => Current?.Parent;

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
        // The second press of a double click is the sheets' (on a picture, it crops it).
        else if (!right && properties.IsLeftButtonPressed && e.ClickCount < 2 && e.KeyModifiers == KeyModifiers.None && OnSelection?.Invoke(point) == true)
            Begin(Mode.Holding, point, e.Pointer, HoldTime);
        else
            return;
        e.Handled = true;
        e.Pointer.Capture(this);
    }

    void Begin(Mode now, Point point, IPointer? by, double wait)
    {
        (mode, start, at, pointer, chosen) = (now, point, point, by, null);
        first = Choose?.Invoke() ?? Slots;
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
        // A new ring: what was going is gone.
        leaving.Clear();
        levels.Clear();
        levels.Add(Measure(first, point, null, onText: true));
        since = opened = Motion.Now;
        IsHitTestVisible = mode is Mode.Clicking or Mode.Asking;
        Aim(mode == Mode.Keys ? null : Pointed(at));
        frames.Start();
        InvalidateVisual();
    }

    /// <summary>The ring a field holds, at a point: the first level's direction chosen, the second's to come; the ring there goes, that field lit.</summary>
    void Enter(Slot slot, Point point)
    {
        Leave();
        Aim(null);
        levels.Add(Measure(slot.Under!, point, slot, onText: false));
        (since, visited) = (Motion.Now, false);
        slot.Opened?.Invoke();
        frames.Start();
        InvalidateVisual();
    }

    /// <summary>Back to the ring before, which comes again where it stood, the ring there going; what the ring left showed of its own is taken back.</summary>
    void Back()
    {
        Leave();
        Aim(null);
        levels[^1].Parent?.Left?.Invoke(false);
        levels.RemoveAt(levels.Count - 1);
        (since, visited) = (Motion.Now, true);
        frames.Start();
        InvalidateVisual();
    }

    /// <summary>The ring there goes as it shows, another coming in its stead.</summary>
    void Leave()
    {
        if (Showing() is not { } look)
            return;
        leaving.Add(look with { Left = Motion.Now });
        frames.Start();
    }

    /// <summary>
    /// Closes the ring: <paramref name="keep"/>, what the field pointed at does comes next and keeps its preview, and what the rings show of
    /// their own stays (see Slot); else the preview goes, and what they show too. The ring goes as it showed, the field done lit, and the
    /// frame around the text with it.
    /// </summary>
    void Close(bool keep)
    {
        // A talk with Claude settles as the ring closes, its changes kept or not as the ring's preview is.
        if (asked is { } talk)
        {
            talk.Updated -= Talked;
            asked = null;
            talk.Settle(keep);
        }
        (typed, typedAt, unreached, glide, recalled, cardBox) = ("", 0, null, null, -1, null);
        EndPull(keep);
        if (!keep)
            Aim(null);
        rest.Stop();
        arrows.Clear();
        // Its name as it reads before what the rings show is kept or taken back; the frame around the text as that reads after.
        var going = Showing();
        for (var k = levels.Count - 1; k >= 0; k--)
            levels[k].Parent?.Left?.Invoke(keep);
        if (going != null)
        {
            leaving.Add(going with { Left = Motion.Now, Frame = new Framing(Selection?.Invoke() ?? [], Turned?.Invoke(), going.Chosen != null) });
            frames.Start();
        }
        levels.Clear();
        (mode, chosen, pointer, backward) = (Mode.None, null, null, false);
        IsHitTestVisible = false;
        ClearValue(CursorProperty);
        InvalidateVisual();
    }

    /// <summary>Points at a field, at the way back (with none), or at nothing: the text reads as that field would make it.</summary>
    void Aim(int? field, bool back = false)
    {
        backward = back;
        if (field == chosen)
            return;
        chosen = field;
        visited |= field != null;
        Preview?.Invoke(field is { } k && Current is { } level ? level.Slots[k] : null);
    }

    /// <summary>
    /// A field of the ring there pulled from a point (see Slot.Pulls), by a pointer pressed on it or the one held: it goes with the pointer
    /// from there; false where it gives nothing to pull now.
    /// </summary>
    bool Pull(int field, Point from, IPointer? by)
    {
        if (Current is not { } level || level.Slots[field].Pulls?.Invoke() is not { } pulling)
            return false;
        Aim(field);
        (mode, pull, pulledAt, IsHitTestVisible) = (Mode.Pulling, pulling, from, true);
        pointer = by ?? pointer;
        Cursor = pulling.Cursor;
        InvalidateVisual();
        return true;
    }

    /// <summary>The pull ends: what it made stays (<paramref name="keep"/>), else it is taken back; the ring stays, to be clicked.</summary>
    void EndPull(bool keep)
    {
        if (pull is not { } going)
            return;
        (pull, mode) = (null, Mode.Clicking);
        ClearValue(CursorProperty);
        going.End(keep);
        InvalidateVisual();
    }

    /// <summary>Whether a point is in the middle of the ring now; in a ring of a second level, on its way back.</summary>
    bool InMiddle(Point point) => levels.Count > 0 && Far(point, levels[^1].Centre) < Inner - 1;

    bool Backward(Point point) => levels.Count > 1 && InMiddle(point);

    /// <summary>The ring held by its middle where the pointer is now, wholly in view, the rings before it along (and where the gesture began).</summary>
    void Shift()
    {
        var by = InView(grip.Centre + (at - grip.At)) - levels[^1].Centre;
        for (var k = 0; k < levels.Count; k++)
            levels[k] = levels[k] with { Centre = InView(levels[k].Centre + by) };
        start += by;
    }

    static double Far(Point a, Point b) => ((Vector)(a - b)).Length;

    /// <summary>
    /// The middle angle of a field of a ring of so many, clockwise from the right (radians): the first is up; in a split ring the fields lie
    /// half a field on, the first right of the top.
    /// </summary>
    static double Angle(int field, int count, bool split = false) => (2.0 * (field + (split ? 0.5 : 0)) / count - 0.5) * Math.PI;

    static Vector Way(double angle) => new(Math.Cos(angle), Math.Sin(angle));

    /// <summary>
    /// The field of the ring now in the direction of a point from its centre, or null in its middle, and on the first ring where the
    /// gesture began: letting go where it was pressed does nothing, also where the ring stands off from there to be in view.
    /// </summary>
    int? Pointed(Point point) => Current is { } level && !(levels.Count == 1 && Far(point, start) < Still) ? Field(level.Slots, point - level.Centre, Inner - 1, level.Split) : null;

    /// <summary>
    /// The field of a ring nearest to a direction, beyond a distance, or null (a split ring's laid out half a field on). A direction right
    /// between two (as the arrows may give) takes the one after it, clockwise, even where its angle comes out a hair short.
    /// </summary>
    static int? Field(IReadOnlyList<Slot> slots, Vector direction, double beyond, bool split = false)
    {
        if (direction.Length < beyond || slots.Count == 0)
            return null;
        var turn = (Math.Atan2(direction.X, -direction.Y) / (2 * Math.PI) + 1) % 1;
        return (int)Math.Floor(turn * slots.Count + (split ? 0 : 0.5) + 1e-9) % slots.Count;
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
                Aim(Pointed(at), back: visited && Backward(at));
                keeping = level.Settles && at.X < level.Centre.X;
                // On out of a field that holds a ring: that ring, where the pointer is; out of one that is pulled, the pull, from there.
                if (chosen is { } field && Far(at, level.Centre) > Outer + 6)
                {
                    if (level.Slots[field].Under != null)
                        Enter(level.Slots[field], at);
                    else if (level.Slots[field].Pulls != null)
                        Pull(field, at, null);
                }
                break;
            case Mode.Pulling:
                pull?.To(at - pulledAt, e.KeyModifiers);
                break;
            case Mode.Clicking or Mode.Keys when Current is { } level:
                if (mode != Mode.Keys || arrows.Count == 0)
                    Aim(mode == Mode.Clicking && Far(at, level.Centre) > Outer + 24 ? null : Pointed(at), back: mode == Mode.Clicking && Backward(at));
                keeping = level.Settles && at.X < level.Centre.X;
                break;
            case Mode.Gripped when Far(at, grip.At) > SelectDistance:
                // Pressed in its middle and moved on, not clicked: the ring goes with the pointer.
                (mode, Cursor) = (Mode.Moving, Grab);
                Aim(null);
                Shift();
                break;
            case Mode.Moving:
                Shift();
                break;
            case Mode.Asking when asked != null && Current is { } level:
                // Over the split middle the half a click would do is lit.
                backward = InMiddle(at);
                keeping = at.X < level.Centre.X;
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
                if (chosen == null && levels.Count > 1)
                {
                    // Let go in the middle of a ring of a second level: on the way back, the ring before; on a split middle, the ring
                    // closed, what it shows kept on its left half; where that ring came to the pointer, that ring; to be clicked.
                    (mode, IsHitTestVisible) = (Mode.Clicking, true);
                    e.Pointer.Capture(null);
                    if (backward && level.Settles)
                        Close(keep: keeping);
                    else if (backward)
                        Back();
                    // Drawn again, so that it takes the clicks beside it too (see Render), not the text under it.
                    InvalidateVisual();
                    break;
                }
                var held = chosen is { } done ? level.Slots[done] : null;
                if (held is { Steps: true } or { Pulls: not null })
                {
                    // Let go on a field that steps: it steps, and the ring stays to be clicked; on one that is pulled, it stays to be pulled.
                    (mode, IsHitTestVisible) = (Mode.Clicking, true);
                    e.Pointer.Capture(null);
                    if (held.Steps)
                        Do(held, null);
                    InvalidateVisual();
                    break;
                }
                Close(keep: held != null);
                e.Pointer.Capture(null);
                if (held != null)
                    Do(held, null);
                break;
            case Mode.Gripped:
                // Let go in the middle where it was pressed, a click there: back to the ring before, or the first one closed; a split
                // middle closes the ring, what it shows kept on its left half.
                mode = Mode.Clicking;
                e.Pointer.Capture(null);
                if (Current is { Settles: true } settling)
                    Close(keep: grip.At.X < settling.Centre.X);
                else if (levels.Count > 1)
                    Back();
                else
                    Close(keep: false);
                break;
            case Mode.Moving:
                // Moved: it stays where it is, to be clicked.
                mode = Mode.Clicking;
                ClearValue(CursorProperty);
                e.Pointer.Capture(null);
                break;
            case Mode.Pulling:
                // Let go while pulling: what the pull made stays, and the ring, to be pulled again.
                EndPull(keep: true);
                e.Pointer.Capture(null);
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
        if (Field(first, (turned ? corner : end) - start, BlindDistance) is not { } way)
            return null;
        var slot = first[way];
        // A field that is pulled wants the ring.
        if (slot.Under == null)
            return turned || slot.Pulls != null ? null : slot;
        return turned && Field(slot.Under, end - corner, BlindDistance, slot.Split) is { } second && slot.Under[second] is { Pulls: null } picked ? picked : null;
    }

    /// <summary>
    /// A click while the ring waits for one: on a field it does that field (one with a ring opens it, one that steps steps and the ring
    /// stays, one that is pulled goes with the pointer from there, see Pull), elsewhere it closes, what the rings show taken away; in the
    /// middle it is a click as it is let go there (back to the ring
    /// before, or the ring closed: the first one, or one with a split middle), and the ring goes with the pointer moved on from there (see
    /// Shift).
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (mode == Mode.Asking && Current is { } asking)
        {
            e.Handled = true;
            Asked(e.GetPosition(this), asking);
            return;
        }
        if (mode != Mode.Clicking || Current is not { } level)
            return;
        e.Handled = true;
        var point = e.GetPosition(this);
        if (InMiddle(point))
        {
            (mode, grip, pointer) = (Mode.Gripped, (point, level.Centre), e.Pointer);
            e.Pointer.Capture(this);
            return;
        }
        var field = Far(point, level.Centre) <= Outer + 4 ? Pointed(point) : null;
        if (field is { } inside && level.Slots[inside].Under != null)
        {
            Enter(level.Slots[inside], level.Centre);
            return;
        }
        if (field is { } pulled && level.Slots[pulled].Pulls != null)
        {
            // A field that gives nothing to pull now does nothing.
            if (Pull(pulled, point, e.Pointer))
                e.Pointer.Capture(this);
            return;
        }
        if (field is { } step && level.Slots[step].Steps)
        {
            Aim(step);
            Do(level.Slots[step], null);
            InvalidateVisual();
            return;
        }
        Aim(field);
        Close(keep: field != null);
        if (field is { } given)
            Do(level.Slots[given], null);
    }

    /// <summary>
    /// A click while the ring asks Claude. Before a wish went, the ring is as it was: a field does what it does (one with a ring opens
    /// it), elsewhere it closes. While it talks: its middle's left half keeps the changes, its right half takes them back, either closing
    /// it; a click beside it keeps them and closes it once Claude is done; on the ring, the line or the card nothing.
    /// </summary>
    void Asked(Point point, Level level)
    {
        if (asked is not { } talk)
        {
            var field = !InMiddle(point) && Far(point, level.Centre) <= Outer + 4 ? Pointed(point) : null;
            if (field is { } holding && level.Slots[holding].Under != null)
            {
                (typed, typedAt, mode) = ("", 0, Mode.Clicking);
                Enter(level.Slots[holding], level.Centre);
                return;
            }
            if (lineBox.Contains(point))
                return;
            Aim(field);
            Close(keep: field != null);
            if (field is { } given)
                Do(level.Slots[given], null);
            return;
        }
        if (InMiddle(point))
        {
            // With nothing to keep, the middle closes the ring.
            var keep = point.X < level.Centre.X && (talk.Working || talk.Changed);
            talk.Settle(keep);
            Close(keep);
            return;
        }
        if (Far(point, level.Centre) <= Outer + 4 || lineBox.Contains(point) || cardBox?.Contains(point) == true || talk.Working)
            return;
        talk.Settle(keep: true);
        Close(keep: true);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (mode == Mode.Asking && Wheel != null)
        {
            Wheel(e);
            e.Handled = true;
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        // Lost while pulling: the pull taken back, as a drag is.
        if (mode == Mode.Pulling)
            EndPull(keep: false);
        else if (mode is Mode.Waiting or Mode.Blind or Mode.Held or Mode.Holding)
            Close(keep: false);
        // Lost while it holds the ring by its middle: the ring stays where it is, to be clicked.
        else if (mode is Mode.Gripped or Mode.Moving)
        {
            mode = Mode.Clicking;
            ClearValue(CursorProperty);
        }
    }

    /// <summary>Does what a field does; where it was chosen blindly, its name shows there for a moment.</summary>
    void Do(Slot slot, Point? blindly)
    {
        slot.Act?.Invoke();
        if (blindly is { } point)
        {
            confirmed = (NameOf(slot.NameKey), point, Motion.Now);
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
    /// Space went up: from the ring, what is chosen is done (nothing in its middle, nor on a field that holds a ring; a field that steps
    /// steps, and what its ring shows is kept); before the ring came, nothing happened, and Space is to be typed (false).
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
        if (slot is { Steps: true })
        {
            Do(slot, null);
            Close(keep: true);
            return true;
        }
        var doing = slot is { Act: not null };
        Close(keep: doing);
        if (doing)
            Do(slot!, null);
        return true;
    }

    /// <summary>
    /// A key while the ring has a gesture: the arrows choose for the keyboard (two of them a diagonal), Enter opens the ring of the field
    /// chosen; Backspace goes back while a ring shows to be clicked or for the keys (not where the held pointer would bring the ring it
    /// left again at once, nor while it moves one), and the selection under it stays; Escape closes any ring, or takes back a pull going
    /// (see Pull). Whether the ring took the key.
    /// </summary>
    public bool KeyChanged(Key key, bool down, KeyModifiers modifiers = KeyModifiers.None)
    {
        if (mode == Mode.None)
            return false;
        // While the ring asks Claude, the keys are the line's.
        if (mode == Mode.Asking)
        {
            if (down)
                AskingKey(key, modifiers);
            return true;
        }
        if (key == Key.Escape)
        {
            // Escape while a field is pulled takes the pull back, the ring staying; else it closes the ring.
            if (down && mode == Mode.Pulling)
            {
                EndPull(keep: false);
                pointer?.Capture(null);
            }
            else if (down)
            {
                pointer?.Capture(null);
                Close(keep: false);
            }
            return true;
        }
        // While a field is pulled, the keys are the pull's (those held go to it with the pointer).
        if (mode == Mode.Pulling)
            return true;
        if (key == Key.Back && levels.Count > 0)
        {
            if (down && levels.Count > 1 && mode is (Mode.Clicking or Mode.Keys))
                Back();
            return true;
        }
        // Enter on a split ring to be clicked: it closes, what it shows kept.
        if (key == Key.Enter && mode == Mode.Clicking && Current is { Split: true })
        {
            if (down)
                Close(keep: true);
            return true;
        }
        if (mode is not (Mode.Keys or Mode.KeysWaiting))
            return false;
        if (down && mode == Mode.Keys && Current is { } level && key == Key.Enter && chosen is { } field && level.Slots[field].Under != null)
            Enter(level.Slots[field], level.Centre);
        else if (down && mode == Mode.Keys && Current is { } stepping && key == Key.Enter && chosen is { } step && stepping.Slots[step].Steps)
            Do(stepping.Slots[step], null);
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
                    Aim(Field(now.Slots, direction, 0.5, now.Split));
            }
        }
        InvalidateVisual();
        return true;
    }

    /// <summary>
    /// Text typed while a ring shows (to be clicked, held or up for the keys): it goes to the line beside the ring, to ask Claude (see Ask),
    /// the fields' previews taken back; a ring held stays when it is let go. Spaces alone begin nothing. Whether the ring took the text.
    /// </summary>
    public bool Typed(string text)
    {
        if (mode is not (Mode.Clicking or Mode.Held or Mode.Keys or Mode.Asking) || Current == null)
            return false;
        if (asked?.Working == true)
            return true;
        var printable = new string([.. text.Where(c => !char.IsControl(c))]);
        if (typed.Length == 0)
            printable = printable.TrimStart();
        if (printable.Length == 0)
            return true;
        if (mode != Mode.Asking)
        {
            if (printable.Trim().Length == 0)
                return true;
            Aim(null);
            arrows.Clear();
            (mode, IsHitTestVisible, backward) = (Mode.Asking, true, false);
            ClearValue(CursorProperty);
        }
        typed = typed.Insert(typedAt, printable);
        typedAt += printable.Length;
        unreached = null;
        InvalidateVisual();
        return true;
    }

    /// <summary>The ring at a point, its line ready to ask Claude (Ctrl+Space): the ring of what the selection, or the cursor, has.</summary>
    public void OpenAsking(Point point)
    {
        if (mode != Mode.None)
            return;
        Begin(Mode.Asking, point, null, RestTime);
        rest.Stop();
        Open(Mode.Asking, point);
    }

    /// <summary>
    /// A key for the line: Enter sends what is typed (with nothing typed, it keeps Claude's changes); Escape clears it, or closes the ring,
    /// Claude's changes taken back; Backspace and Delete (Ctrl: a word), the arrows, Home and End edit it; ↑ and ↓ bring back wishes sent
    /// before; Ctrl+V pastes text.
    /// </summary>
    void AskingKey(Key key, KeyModifiers modifiers)
    {
        var working = asked?.Working == true;
        var control = (modifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        switch (key)
        {
            case Key.Escape when typed.Length > 0:
                (typed, typedAt) = ("", 0);
                break;
            case Key.Escape:
                asked?.Settle(false);
                Close(keep: false);
                return;
            case Key.Enter when working:
                break;
            case Key.Enter when typed.Trim().Length == 0:
                if (asked is { } talk)
                {
                    talk.Settle(true);
                    Close(keep: true);
                }
                return;
            case Key.Enter:
                Send();
                break;
            case Key.Back when typedAt > 0:
            {
                var from = control ? WordStart(typed, typedAt) : typedAt - 1;
                typed = typed.Remove(from, typedAt - from);
                typedAt = from;
                break;
            }
            case Key.Delete when typedAt < typed.Length:
                typed = typed.Remove(typedAt, 1);
                break;
            case Key.Left:
                typedAt = control ? WordStart(typed, typedAt) : Math.Max(0, typedAt - 1);
                break;
            case Key.Right:
                typedAt = Math.Min(typed.Length, typedAt + 1);
                break;
            case Key.Home:
                typedAt = 0;
                break;
            case Key.End:
                typedAt = typed.Length;
                break;
            case Key.Up when Wishes.Count > 0 && !working:
                recalled = recalled < 0 ? Wishes.Count - 1 : Math.Max(0, recalled - 1);
                (typed, typedAt) = (Wishes[recalled], Wishes[recalled].Length);
                break;
            case Key.Down when recalled >= 0:
                recalled++;
                typed = recalled < Wishes.Count ? Wishes[recalled] : "";
                if (recalled >= Wishes.Count)
                    recalled = -1;
                typedAt = typed.Length;
                break;
            case Key.V when control && !working:
                _ = Paste();
                break;
        }
        InvalidateVisual();
    }

    /// <summary>Where the word before a place in a text begins (Ctrl+Backspace, Ctrl+Left).</summary>
    static int WordStart(string text, int at)
    {
        var k = at;
        while (k > 0 && char.IsWhiteSpace(text[k - 1]))
            k--;
        while (k > 0 && !char.IsWhiteSpace(text[k - 1]))
            k--;
        return k;
    }

    /// <summary>The clipboard's text in the line at its cursor, on one line.</summary>
    async Task Paste()
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
            return;
        var text = await clipboard.TryGetTextAsync();
        if (string.IsNullOrEmpty(text) || mode != Mode.Asking)
            return;
        var line = string.Join(" ", text.Split('\n', '\r').Select(l => l.Trim()).Where(l => l.Length > 0));
        typed = typed.Insert(typedAt, line);
        typedAt += line.Length;
        InvalidateVisual();
    }

    /// <summary>
    /// The wish typed sent: the first begins the talk (see Ask), the ring going aside so that what Claude writes shows (see Aside); later
    /// ones go on with it. Where Claude cannot be reached the line says why, the wish staying typed.
    /// </summary>
    void Send()
    {
        var wish = typed.Trim();
        if (asked == null)
        {
            if (Ask?.Invoke(wish) is not { } talk)
            {
                unreached = Unreachable?.Invoke() ?? "";
                InvalidateVisual();
                return;
            }
            asked = talk;
            talk.Updated += Talked;
            if (Current is { } level && Aside?.Invoke(level.Centre) is { } aside && Far(InView(aside), level.Centre) > 1)
                glide = (level.Centre, InView(aside), Motion.Now);
        }
        else
            asked.Send(wish);
        talking = Motion.Now;
        if (Wishes.Count == 0 || Wishes[^1] != wish)
            Wishes.Add(wish);
        (typed, typedAt, recalled, unreached) = ("", 0, -1, null);
        Talked();
    }

    /// <summary>The talk changed: the ring is drawn again, and moves while Claude works.</summary>
    void Talked()
    {
        frames.Start();
        InvalidateVisual();
    }

    /// <summary>A name in the language of the interface, by its key among the texts (the key where there is none).</summary>
    string NameOf(string key) => this.TryFindResource(key, out var text) && text is string name ? name : key;

    /// <summary>A colour of the theme: they lie under its variant.</summary>
    IBrush? Brush(string key) => this.TryFindResource(key, ActualThemeVariant, out var brush) ? brush as IBrush : null;

    static readonly FontFamily Inter = new("fonts:Inter#Inter");

    /// <summary>
    /// A sign's outline, its ink centred on the origin and scaled to the size of the others (see SignHeight): a sign is set by the ink it
    /// shows, not by the box its font gives it, in which an exclamation mark stands tall and a quotation mark small and high. An icon
    /// stands as it is drawn, as large as it fits.
    /// </summary>
    Geometry SignOf(Slot slot)
    {
        if (signs.TryGetValue(slot, out var known))
            return known;
        if (slot.Icon is { } icon)
        {
            var drawn = Geometry.Parse(icon);
            var box = drawn.Bounds;
            var fit = box.Width <= 0 || box.Height <= 0 ? 0 : Math.Min(1, Math.Min(SignHeight / box.Height, SignWidth / box.Width));
            drawn.Transform = new MatrixTransform(Matrix.CreateTranslation(-box.Center.X, -box.Center.Y) * Matrix.CreateScale(fit, fit));
            return signs[slot] = drawn;
        }
        var typeface = new Typeface(Inter, slot.Slanted ? FontStyle.Italic : FontStyle.Normal, FontWeight.Light);
        var text = new FormattedText(slot.Sign, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 100, Brushes.Black);
        var outline = text.BuildGeometry(default) ?? new StreamGeometry();
        var ink = outline.Bounds;
        var scale = ink.Width <= 0 || ink.Height <= 0 ? 0
            : Math.Min(Math.Min(SignHeight / ink.Height, SignWidth / ink.Width), SignArea / Math.Sqrt(ink.Width * ink.Height));
        outline.Transform = new MatrixTransform(Matrix.CreateTranslation(-ink.Center.X, -ink.Center.Y) * Matrix.CreateScale(scale, scale));
        return signs[slot] = outline;
    }

    /// <summary>
    /// A ring at a point, always of one size, wholly in view. Brought on the text, it stands on the middle of the selection's part on
    /// the line there, so that the text shows in its clear middle, as far as the point it was brought at stays in the middle.
    /// </summary>
    Level Measure(IReadOnlyList<Slot> slots, Point point, Slot? parent, bool onText)
    {
        var centre = point;
        if (onText && Nearest(Selection?.Invoke() ?? [], point) is { } part && part.Inflate(8).Contains(point))
        {
            var near = Inner - 6;
            var y = Math.Clamp(part.Center.Y, point.Y - near, point.Y + near);
            var reach = Math.Sqrt(Math.Max(0, near * near - (point.Y - y) * (point.Y - y)));
            centre = new Point(Math.Clamp(part.Center.X, point.X - reach, point.X + reach), y);
        }
        return new Level(slots, InView(centre), parent);
    }

    /// <summary>A ring's centre where the ring is wholly in view.</summary>
    Point InView(Point centre)
    {
        var margin = Outer + 8;
        return new Point(Math.Clamp(centre.X, margin, Math.Max(margin, Bounds.Width - margin)), Math.Clamp(centre.Y, margin, Math.Max(margin, Bounds.Height - margin)));
    }

    /// <summary>The part of the selection on the line nearest to a point, or null without one.</summary>
    static Rect? Nearest(IReadOnlyList<Rect> parts, Point point)
    {
        Rect? nearest = null;
        var distance = double.MaxValue;
        foreach (var part in parts)
        {
            var away = Math.Max(0, Math.Max(part.Left - point.X, point.X - part.Right)) + Math.Max(0, Math.Max(part.Top - point.Y, point.Y - part.Bottom)) * 4;
            if (away < distance)
                (nearest, distance) = (part, away);
        }
        return nearest;
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
        // The frame around the text the ring acts on, under the rings: it comes with the first ring and stays while one gives way to another.
        if (Current != null)
            DrawFrame(context, Selection?.Invoke() ?? [], Turned?.Invoke(), chosen != null, Motion.Coming(opened));
        // The rings going, under the one there; a ring closed takes its frame along.
        foreach (var look in leaving)
        {
            var left = Motion.Going(look.Left);
            if (look.Frame is { } frame)
                DrawFrame(context, frame.Parts, frame.Turned, frame.Lit, left);
            DrawRing(context, look, left);
        }
        if (Showing() is { } shown)
        {
            DrawRing(context, shown, Motion.Coming(since));
            if (mode == Mode.Asking)
                DrawAsking(context, shown.Level, Motion.Coming(since));
        }
        if (confirmed is var (name, point2, given))
        {
            var fading = Math.Clamp(Motion.Since(given) / Confirmation, 0, 1);
            using (context.PushOpacity(1 - fading * fading))
                Tip(context, name, point2 + new Vector(14, -28), Brush("Accent"), Brush("OnAccent"));
        }
    }

    /// <summary>
    /// The ring there as it shows now, null without one: the field pointed at, the way back, and the name beside it as it reads now (what a
    /// pull makes while it goes; the field's; else the way back's, or the line about what the ring shows, see Slot.Status).
    /// </summary>
    Look? Showing()
    {
        if (Current is not { } level)
            return null;
        // Asking Claude, the line beside the ring says what is going on (see DrawAsking).
        if (mode == Mode.Asking)
            return new Look(level, null, asked != null && backward, keeping, false, null)
            {
                Talking = asked != null,
                Working = asked?.Working == true,
                Settling = asked is { } talk && (talk.Working || talk.Changed),
            };
        var status = chosen == null && !backward && level.Parent?.Status?.Invoke() is { Length: > 0 } line ? line : null;
        var label = pull?.Reading is { } reading ? reading
            : chosen is { } named ? Named(level.Slots[named])
            : backward || status != null ? status ?? NameOf(!level.Settles ? "RingBack" : keeping ? "RingTake" : "RingDrop")
            : null;
        return new Look(level, chosen, backward, keeping, mode == Mode.Moving, label);
    }

    /// <summary>A field's name beside the ring; a field that is pulled says how (see RingPull).</summary>
    string Named(Slot slot) => slot.Pulls == null ? NameOf(slot.NameKey) : $"{NameOf(slot.NameKey)} · {NameOf("RingPull")}";

    /// <summary>
    /// The frame around the text a ring acts on, as far as it has come (0 to 1): in the ring's clear middle, and around it; in the accent
    /// where a field is pointed at; a drawing turned, its frame turned with it.
    /// </summary>
    void DrawFrame(DrawingContext context, IReadOnlyList<Rect> parts, (Rect Frame, double Turn)? turned, bool lit, double presence)
    {
        if (presence <= 0)
            return;
        var framing = new Pen(lit ? Brush("Accent") : Brush("Hairline"), lit ? 1.5 : 1);
        using (context.PushOpacity(presence))
        {
            if (turned is var (frame, turn))
            {
                var middle = frame.Center;
                var turning = Matrix.CreateTranslation(-middle.X, -middle.Y) * Matrix.CreateRotation(turn * Math.PI / 180) * Matrix.CreateTranslation(middle.X, middle.Y);
                using (context.PushTransform(turning))
                    context.DrawGeometry(null, framing, Around([frame]));
            }
            else if (parts.Count > 0)
                context.DrawGeometry(null, framing, Around(parts));
        }
    }

    /// <summary>A ring as it shows, as far as it has come (0 to 1): growing to its size as it comes, shrinking from it as it goes.</summary>
    void DrawRing(DrawingContext context, Look look, double presence)
    {
        if (presence <= 0)
            return;
        var level = look.Level;
        var scale = Motion.Size(presence, Small);
        var c = level.Centre;
        var (count, half) = (level.Slots.Count, Math.PI / Math.Max(1, level.Slots.Count));
        var (line, ink, faint, accent, onAccent) = (Brush("Hairline"), Brush("Ink"), Brush("InkFaint"), Brush("Accent"), Brush("OnAccent"));
        // Glass of the canvas's tint, so that it shows as a thing of its own on the white page, with a rim a little darker.
        var surface = Brush("Canvas") is ISolidColorBrush { Color: var tint } ? new SolidColorBrush(tint, Glass) : null;
        var hairline = new Pen(line, 1);
        var rim = new Pen(Brush("InkGhost") is ISolidColorBrush { Color: var ghost } ? new SolidColorBrush(ghost, 0.45) : line, 1);
        using (context.PushOpacity(presence))
        using (context.PushTransform(Matrix.CreateTranslation(-c.X, -c.Y) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(c.X, c.Y)))
        {
            // The disc, of glass, raised over the page (a little higher while it is moved): the band of fields nearly whole, so that the
            // signs read clearly; the middle clear, so that the text there shows as it is.
            var everywhere = new RectangleGeometry(new Rect(Bounds.Size).Inflate(400));
            var inside = new GeometryGroup { FillRule = FillRule.EvenOdd, Children = new GeometryCollection { everywhere, new EllipseGeometry(new Rect(c.X - Inner, c.Y - Inner, 2 * Inner, 2 * Inner)) } };
            var (rise, blur, shade) = look.Moving ? (16, 36, (byte)0x28) : (10, 28, (byte)0x1E);
            using (context.PushGeometryClip(inside))
                context.DrawRectangle(surface, rim, new RoundedRect(new Rect(c.X - Outer, c.Y - Outer, 2 * Outer, 2 * Outer), Outer),
                    new BoxShadows(new BoxShadow { OffsetY = rise, Blur = blur, Color = Color.FromArgb(shade, 0, 0, 0) },
                        [new BoxShadow { OffsetY = 2, Blur = 6, Color = Color.FromArgb(0x12, 0, 0, 0) }]));
            // The way back pointed at: the middle of glass too, a field as the others (see below).
            if (look.Backward)
                context.DrawEllipse(surface, null, c, Inner, Inner);
            // The band of fields: its inner rim. No lines between the fields, nor between the halves of a split ring: the signs show them,
            // and lines across the text under the glass would read as strokes through it.
            context.DrawEllipse(null, hairline, c, Inner, Inner);
            if (look.Chosen is { } pointed)
                context.DrawGeometry(accent, null, Sector(c, Inner + Gap, Outer - Gap, Angle(pointed, count, level.Split), half, Gap, Round));
            // Talking with Claude the band is empty, an arc going round it while Claude is at work.
            if (look.Working)
            {
                var turn = Motion.Since(talking) / 1000 * 1.6 * Math.PI;
                var arc = new StreamGeometry();
                using (var draw = arc.Open())
                {
                    draw.BeginFigure(c + Way(turn) * Middle, false);
                    draw.ArcTo(c + Way(turn + 1.1) * Middle, new Size(Middle, Middle), 0, false, SweepDirection.Clockwise);
                    draw.EndFigure(false);
                }
                context.DrawGeometry(null, new Pen(accent, 3, lineCap: PenLineCap.Round), arc);
            }
            for (var k = 0; k < (look.Talking ? 0 : count); k++)
            {
                var slot = level.Slots[k];
                var way = Way(Angle(k, count, level.Split));
                var place = c + way * Middle;
                // A swatch: its colour in a disc, ringed so that a pale one shows on the glass and on the accent.
                if (slot.Swatch is { } swatch)
                    context.DrawEllipse(new SolidColorBrush(swatch), new Pen(look.Chosen == k ? onAccent : faint, 1), place, SwatchRadius, SwatchRadius);
                else
                {
                    using (context.PushTransform(Matrix.CreateTranslation(place.X, place.Y)))
                        context.DrawGeometry(look.Chosen == k ? onAccent : ink, null, SignOf(slot));
                }
                // A field that holds a ring: a chevron between its sign and its outer edge, pointing on.
                if (slot.Under != null)
                {
                    var (tip, back, side) = (c + way * (Outer - Gap - 6), c + way * (Outer - Gap - 10.5), new Vector(-way.Y, way.X) * 3.5);
                    var chevron = new Pen(look.Chosen == k ? onAccent : faint, 1.5, lineCap: PenLineCap.Round);
                    context.DrawLine(chevron, back + side, tip);
                    context.DrawLine(chevron, back - side, tip);
                }
            }
            // A ring of a second level: in its middle the way back, the sign of the field that holds it; on a button of glass, so that it
            // reads over the text, and lit as a field where the pointer is in it. While a field is pointed at it is away: the text there
            // reads as that field would make it. A split middle stays then too.
            if (level.Settles || look.Settling)
            {
                // A split middle: keeping what the ring shows on the left (a tick), taking it away on the right (a cross), either closing
                // the ring; each half on a button of glass and lit as a field where the pointer is in it; there while a field is pointed
                // at too, so that keeping and taking away are always to be seen.
                foreach (var (side, sign) in new[] { (-1, Tick), (1, Cross) })
                {
                    var lit = look.Backward && look.Keeping == side < 0;
                    var middle = new Point(c.X + side * Inner / 2, c.Y);
                    if (lit)
                    {
                        var area = side < 0 ? new Rect(c.X - Inner, c.Y - Inner, Inner - Gap / 2, 2 * Inner) : new Rect(c.X + Gap / 2, c.Y - Inner, Inner - Gap / 2, 2 * Inner);
                        using (context.PushGeometryClip(new RectangleGeometry(area)))
                            context.DrawEllipse(accent, null, c, Inner - Gap, Inner - Gap);
                    }
                    else
                        context.DrawEllipse(surface, rim, middle, Hub - 2, Hub - 2);
                    using (context.PushTransform(Matrix.CreateScale(HubSign, HubSign) * Matrix.CreateTranslation(middle.X, middle.Y)))
                        context.DrawGeometry(lit ? onAccent : ink, null, sign);
                }
            }
            else if (level.Parent is { } parent && (look.Backward || look.Chosen == null))
            {
                if (look.Backward)
                    context.DrawEllipse(accent, null, c, Inner - Gap, Inner - Gap);
                else
                    context.DrawEllipse(surface, rim, c, Hub, Hub);
                using (context.PushTransform(Matrix.CreateScale(HubSign, HubSign) * Matrix.CreateTranslation(c.X, c.Y)))
                    context.DrawGeometry(look.Backward ? onAccent : faint, null, SignOf(parent));
            }
        }
        // The name of the field pointed at, beside the ring in its direction; the way back's below it, above where the view ends below;
        // with neither, the line about what the ring shows there (see Slot.Status).
        if (look.Label is { } name)
        {
            var way = look.Chosen is { } named ? Way(Angle(named, count, level.Split)) : new Vector(0, 1);
            var label = Layout(name, Brush("TipInk"));
            var (w, h) = (label.WidthIncludingTrailingWhitespace + 16, label.Height + 8);
            if (look.Chosen == null && c.Y + Outer + 8 + h > Bounds.Height)
                way = new Vector(0, -1);
            var reach = w / 2 * Math.Abs(way.X) + h / 2 * Math.Abs(way.Y);
            var beside = c + way * (Outer + 8 + reach);
            using (context.PushOpacity(presence))
                Tip(context, label, new Point(beside.X - w / 2, beside.Y - h / 2), Brush("TipSurface"));
        }
    }

    /// <summary>
    /// The line beside the ring while it asks Claude, under it (over it where the view ends below): what is typed, its cursor in it; else
    /// why Claude cannot be reached, what Claude is doing (its dots going), Claude's words where they are short, or what to type (faint).
    /// Claude's longer words stand on a card under the line.
    /// </summary>
    void DrawAsking(DrawingContext context, Level level, double presence)
    {
        var c = level.Centre;
        var working = asked?.Working == true;
        var answer = asked is { Working: false } done && typed.Length == 0 && unreached == null ? done.Answer : null;
        var (shown, faint) = unreached != null ? (unreached, false)
            : typed.Length > 0 ? (typed, false)
            : asked == null ? (NameOf("AskPlaceholder"), true)
            : working ? (asked.Line + new string('.', 1 + (int)(Motion.Since(talking) / 450) % 3), false)
            : answer is { Length: > 0 and <= ShortAnswer } said && !said.Contains('\n') ? (Emphasis(said).Text, false)
            : (NameOf(asked.Changed ? "AskFollowUp" : "AskFurther"), true);
        var ink = faint ? Brush("InkFaint") : Brush("TipInk");
        // A long line shows its end, where the cursor is.
        var cut = 0;
        var label = Layout(shown, ink);
        while (label.WidthIncludingTrailingWhitespace > LineWidest - 28 && cut < shown.Length - 1)
        {
            cut += Math.Max(1, (shown.Length - cut) / 12);
            label = Layout("…" + shown[cut..], ink);
        }
        var (w, h) = (Math.Clamp(label.WidthIncludingTrailingWhitespace + 28, LineNarrowest, LineWidest), label.Height + 14);
        TextLayout? card = null;
        if (answer is { Length: > 0 } words && (words.Length > ShortAnswer || words.Contains('\n')))
        {
            // Claude's **words** bold, the rest as it is.
            var (plain, bold) = Emphasis(words);
            var strong = new Typeface(Inter, FontStyle.Normal, FontWeight.Bold);
            card = new TextLayout(plain, new Typeface(Inter), 12.5, Brush("TipInk"), TextAlignment.Left, TextWrapping.Wrap, TextTrimming.WordEllipsis,
                maxWidth: CardWidth - 28, maxLines: 18,
                textStyleOverrides: [.. bold.Select(b => new ValueSpan<TextRunProperties>(b.Start, b.Length, new GenericTextRunProperties(strong, 12.5, foregroundBrush: Brush("TipInk"))))]);
        }
        var cardHeight = card != null ? card.Height + 20 + 8 : 0;
        var below = c.Y + Outer + 10 + h + cardHeight <= Bounds.Height - 4;
        var x = Math.Clamp(c.X - w / 2, 8, Math.Max(8, Bounds.Width - w - 8));
        lineBox = new Rect(x, below ? c.Y + Outer + 10 : c.Y - Outer - 10 - h, w, h);
        var shadow = new BoxShadows(new BoxShadow { OffsetY = 3, Blur = 10, Color = Color.FromArgb(0x26, 0, 0, 0) });
        using (context.PushOpacity(presence))
        {
            var typing = unreached == null && (typed.Length > 0 || asked == null);
            context.DrawRectangle(Brush("TipSurface"), typing ? new Pen(Brush("Accent"), 1) : null, new RoundedRect(lineBox, h / 2), shadow);
            label.Draw(context, new Point(lineBox.X + 14, lineBox.Y + 7));
            if (typing)
            {
                var before = Layout((cut > 0 ? "…" : "") + typed[Math.Min(cut, typedAt)..typedAt], ink).WidthIncludingTrailingWhitespace;
                var caretX = Math.Round(lineBox.X + 14 + (typed.Length == 0 ? 0 : before)) + 0.5;
                context.DrawLine(new Pen(Brush("Accent"), 1.2), new Point(caretX, lineBox.Y + 6), new Point(caretX, lineBox.Bottom - 6));
            }
            if (card != null)
            {
                var cx = Math.Clamp(c.X - CardWidth / 2, 8, Math.Max(8, Bounds.Width - CardWidth - 8));
                var box = new Rect(cx, below ? lineBox.Bottom + 8 : lineBox.Top - 8 - (card.Height + 20), CardWidth, card.Height + 20);
                cardBox = box;
                context.DrawRectangle(Brush("TipSurface"), null, new RoundedRect(box, 12), shadow);
                card.Draw(context, new Point(box.X + 14, box.Y + 10));
            }
            else
                cardBox = null;
        }
    }

    /// <summary>A text of Claude's without its marks of emphasis (**so**), and where the words so marked stand in it, to be set bold.</summary>
    static (string Text, List<(int Start, int Length)> Bold) Emphasis(string text)
    {
        var plain = new System.Text.StringBuilder();
        var bold = new List<(int, int)>();
        var k = 0;
        while (k < text.Length)
        {
            if (text.AsSpan(k).StartsWith("**") && text.IndexOf("**", k + 2, StringComparison.Ordinal) is var end && end > k + 2)
            {
                bold.Add((plain.Length, end - k - 2));
                plain.Append(text, k + 2, end - k - 2);
                k = end + 2;
                continue;
            }
            plain.Append(text[k++]);
        }
        return (plain.ToString(), bold);
    }

    /// <summary>
    /// The frame around the parts of a selection, one for each line in the order of the text, a little outside them: lines that meet,
    /// one below the other and side by side in part, in one outline that follows them all, its corners rounded.
    /// </summary>
    static StreamGeometry Around(IReadOnlyList<Rect> parts)
    {
        const double Aside = 4, Above = 2, Corner = 5;
        // The lines in runs that meet; parts of one line side by side as one.
        var runs = new List<List<Rect>>();
        foreach (var part in parts)
        {
            if (runs.Count > 0 && runs[^1][^1] is var last)
            {
                var sameLine = Math.Min(last.Bottom, part.Bottom) - Math.Max(last.Top, part.Top) > Math.Min(last.Height, part.Height) / 2;
                if (sameLine && part.Left <= last.Right + 2 && last.Left <= part.Right + 2)
                {
                    runs[^1][^1] = last.Union(part);
                    continue;
                }
                if (!sameLine && part.Top - last.Bottom is > -4 and < 3 && part.Left < last.Right && last.Left < part.Right)
                {
                    runs[^1].Add(part);
                    continue;
                }
            }
            runs.Add([part]);
        }
        var geometry = new StreamGeometry();
        using var draw = geometry.Open();
        foreach (var run in runs)
        {
            // Down the right ends of the lines and up their left ends; where two lines meet, halfway between them.
            var meets = new double[run.Count + 1];
            (meets[0], meets[^1]) = (run[0].Top - Above, run[^1].Bottom + Above);
            for (var k = 1; k < run.Count; k++)
                meets[k] = (run[k - 1].Bottom + run[k].Top) / 2;
            var corners = new List<Point>();
            for (var k = 0; k < run.Count; k++)
                corners.AddRange([new Point(run[k].Right + Aside, meets[k]), new Point(run[k].Right + Aside, meets[k + 1])]);
            for (var k = run.Count - 1; k >= 0; k--)
                corners.AddRange([new Point(run[k].Left - Aside, meets[k + 1]), new Point(run[k].Left - Aside, meets[k])]);
            Rounded(draw, Corners(corners), Corner);
        }
        return geometry;
    }

    /// <summary>A closed outline's corners, without the points that are none: one again, or on the straight line between its neighbours.</summary>
    static List<Point> Corners(List<Point> points)
    {
        var kept = new List<Point>(points);
        for (var removed = true; removed && kept.Count > 2;)
        {
            removed = false;
            for (var k = 0; k < kept.Count && kept.Count > 2; k++)
            {
                var (before, at, after) = (kept[(k + kept.Count - 1) % kept.Count], kept[k], kept[(k + 1) % kept.Count]);
                var straight = Math.Abs(before.X - at.X) < 0.01 && Math.Abs(after.X - at.X) < 0.01 || Math.Abs(before.Y - at.Y) < 0.01 && Math.Abs(after.Y - at.Y) < 0.01;
                if (Far(before, at) < 0.01 || straight)
                {
                    kept.RemoveAt(k--);
                    removed = true;
                }
            }
        }
        return kept;
    }

    /// <summary>A closed outline through corners, each rounded as far as half its sides allow.</summary>
    static void Rounded(StreamGeometryContext draw, List<Point> corners, double radius)
    {
        if (corners.Count < 3)
            return;
        static Point Toward(Point from, Point to, double by) => Far(from, to) is var length and > 1e-9 ? from + (Vector)(to - from) * (Math.Min(by, length / 2) / length) : from;
        var count = corners.Count;
        draw.BeginFigure(Toward(corners[0], corners[1], radius), true);
        for (var k = 1; k <= count; k++)
        {
            var (at, before, after) = (corners[k % count], corners[k - 1], corners[(k + 1) % count]);
            draw.LineTo(Toward(at, before, radius));
            draw.QuadraticBezierTo(at, Toward(at, after, radius));
        }
        draw.EndFigure(true);
    }

    /// <summary>
    /// A field less a gap of even width along its edges and rims, its corners rounded: the part of the ring between two radii and the
    /// lines a gap inside the field's edges, for a field's middle and half its angle (radians, clockwise from the right).
    /// </summary>
    static StreamGeometry Sector(Point centre, double inner, double outer, double middle, double half, double gap, double round)
    {
        // A corner's round, by a rim (inside the outer one, outside the inner one) and an edge, on the side of the field's middle: its
        // centre is round from the rim and gap + round from the edge; it meets the rim on the line from the ring's centre through its own,
        // and the gap's line at its foot there.
        (Point OnLine, Point OnRim) Corner(double rim, double inward, double edge, double side)
        {
            var far = rim - inward * round;
            var angle = edge + side * Math.Asin(Math.Min(1, (gap + round) / far));
            return (centre + Way(angle) * far - Way(edge + side * Math.PI / 2) * round, centre + Way(angle) * rim);
        }
        var (first, last) = (middle - half, middle + half);
        var (outerFirst, outerLast) = (Corner(outer, 1, first, 1), Corner(outer, 1, last, -1));
        var (innerFirst, innerLast) = (Corner(inner, -1, first, 1), Corner(inner, -1, last, -1));
        var corner = new Size(round, round);
        var geometry = new StreamGeometry();
        using var draw = geometry.Open();
        draw.BeginFigure(outerFirst.OnLine, true);
        draw.ArcTo(outerFirst.OnRim, corner, 0, false, SweepDirection.Clockwise);
        draw.ArcTo(outerLast.OnRim, new Size(outer, outer), 0, false, SweepDirection.Clockwise);
        draw.ArcTo(outerLast.OnLine, corner, 0, false, SweepDirection.Clockwise);
        draw.LineTo(innerLast.OnLine);
        draw.ArcTo(innerLast.OnRim, corner, 0, false, SweepDirection.Clockwise);
        draw.ArcTo(innerFirst.OnRim, new Size(inner, inner), 0, false, SweepDirection.CounterClockwise);
        draw.ArcTo(innerFirst.OnLine, corner, 0, false, SweepDirection.Clockwise);
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
