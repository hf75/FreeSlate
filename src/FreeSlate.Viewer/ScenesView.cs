using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.VisualTree;
using FreeSlate.Editing;
using FreeSlate.Rendering;
using FreeSlate.Skia;
using SkiaSharp;
using System.Runtime.CompilerServices;

namespace FreeSlate.Viewer;

/// <summary>A page of Word's own rendering to compare with: its picture and its size in CSS pixels.</summary>
sealed record WordPicture(string File, double Width, double Height);

/// <summary>
/// The document's pages drawn by Skia from their scenes (catalog: engine.output), one below the other in a scroll viewer,
/// only those in view; zoomed as the viewer's page script zooms (a fit or a factor), with Word's pages beside them,
/// over them or against them as the compare box says. With an editing session (see Session) keys and typed text change
/// the document at the cursor, and the pages the change touched are drawn again.
/// </summary>
sealed class ScenesView : Control
{
    const double PxPerPt = 96.0 / 72;
    const double Edge = 16, Gap = 16, Beside = 24, Caption = 20;

    IReadOnlyList<PageScene> scenes = [];
    IReadOnlyList<WordPicture> word = [];
    string comparison = "off";
    double blend = 0.5;
    object zoom = "auto";
    double factor = 1;
    List<Row> rows = [];
    ScrollViewer? scroller;
    int shown;
    readonly Pictures pictures = new();

    /// <summary>Where a row of the layout stands (unzoomed CSS pixels): our page's box and Word's.</summary>
    sealed record Row(int Index, Rect Ours, Rect? Theirs, double Top, double Height);

    /// <summary>The page in view changed (its number, 1 for the first).</summary>
    public event Action<int>? PageChanged;

    /// <summary>A link was clicked: its target (a web address, or # and a bookmark's name).</summary>
    public event Action<string>? LinkClicked;

    /// <summary>
    /// The cursor moved: the place it stands at (a paragraph's Block.Id and an offset in its text, see Docx.ParagraphText)
    /// and how many characters the selection holds (0 without one); null where it left the text.
    /// </summary>
    public event Action<(int Block, int Offset, int Selected)?>? PlaceChanged;

    /// <summary>
    /// The cursor and where the selection began, each a page (its index) and a place among the page's characters in
    /// order (TextPlaces.Spots): before the character of that index, the count of them after the last; null before the
    /// first click into text.
    /// </summary>
    (int Page, int At)? caret, anchor;

    /// <summary>
    /// Whether the cursor, where its place begins a line other than the character before it, stands at the end of that
    /// character's line (after a click there or End) rather than at the start of the next one.
    /// </summary>
    bool lineEnd;

    /// <summary>The character the cursor stands at on its page, before it, or after it at the end of its line.</summary>
    (TextPlaces.Spot Spot, bool After) CaretSpot(List<TextPlaces.Spot> spots, int at) =>
        at >= spots.Count || at > 0 && lineEnd && spots[at - 1].Line != spots[at].Line ? (spots[Math.Min(at, spots.Count) - 1], true) : (spots[at], false);

    /// <summary>Whether the left button is down after a click into text: moving selects.</summary>
    bool selecting;

    /// <summary>
    /// The editing session over the document the pages show (see Editing.EditSession), or null where it is only shown:
    /// it follows the cursor and the selection, and keys and typed text go to it.
    /// </summary>
    public EditSession? Session { get; set; }

    /// <summary>An edit was made, in so many milliseconds with the pages drawn after it.</summary>
    public event Action<double>? Edited;

    /// <summary>The frame after an edit was drawn, so many milliseconds after the key (on the render thread).</summary>
    public event Action<double>? Drawn;

    /// <summary>When the last edit began (Stopwatch ticks) until the frame after it is drawn, else 0.</summary>
    long editedAt;

    /// <summary>Whether a slice of the layout an edit left waits to run (see LayOutLater).</summary>
    bool layingOut;

    /// <summary>The document could not make an edit, for this reason.</summary>
    public event Action<string>? Refused;

    /// <summary>Whether the cursor is being set from the session, which it then does not follow back.</summary>
    bool fromSession;

    /// <summary>Whether the blinking cursor shows.</summary>
    bool blink = true;
    readonly Avalonia.Threading.DispatcherTimer blinker;

    public ScenesView()
    {
        ClipToBounds = true;
        Focusable = true;
        blinker = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(530) };
        blinker.Tick += (_, _) =>
        {
            blink = !blink;
            if (caret != null)
                InvalidateVisual();
        };
    }

    public IReadOnlyList<PageScene> Scenes
    {
        get => scenes;
        set
        {
            scenes = value;
            (caret, anchor, selecting) = (null, null, false);
            blinker.Stop();
            PlaceChanged?.Invoke(null);
            pictures.Clear();
            Arrange();
        }
    }

    /// <summary>Word's pages and how they are shown: off, side, blend or diff.</summary>
    public void Compare(string mode, IReadOnlyList<WordPicture> pages)
    {
        var position = Position();
        (comparison, word) = (mode, pages);
        Arrange();
        Restore(position);
    }

    /// <summary>How much of Word's page shows over ours in the blend (0 none, 1 all).</summary>
    public double Blend
    {
        get => blend;
        set
        {
            blend = Math.Clamp(value, 0, 1);
            InvalidateVisual();
        }
    }

    /// <summary>The zoom: "auto" fits the width without enlarging, "width" fits it, "page" a whole page, a number is the factor.</summary>
    public object Zoom
    {
        get => zoom;
        set
        {
            var position = Position();
            zoom = value;
            InvalidateMeasure();
            Later(() => Restore(position));
        }
    }

    static void Later(Action action) => Avalonia.Threading.Dispatcher.UIThread.Post(action, Avalonia.Threading.DispatcherPriority.Loaded);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        scroller = this.FindAncestorOfType<ScrollViewer>();
        if (scroller != null)
        {
            scroller.ScrollChanged += OnScrolled;
            scroller.SizeChanged += OnResized;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (scroller != null)
        {
            scroller.ScrollChanged -= OnScrolled;
            scroller.SizeChanged -= OnResized;
        }
        scroller = null;
        base.OnDetachedFromVisualTree(e);
    }

    void OnScrolled(object? sender, ScrollChangedEventArgs e)
    {
        InvalidateVisual();
        Track();
    }

    void OnResized(object? sender, SizeChangedEventArgs e)
    {
        if (zoom is string)
        {
            var position = Position();
            InvalidateMeasure();
            Later(() => Restore(position));
        }
    }

    static Size PageSize(PageScene scene) => new(scene.Width * PxPerPt, scene.Height * PxPerPt);

    /// <summary>
    /// Where Word's page lies on ours: a page with the markup pane is wider than the paper and printed smaller by its
    /// scale, in the middle of the paper's height; Word's PDF is that paper (as the page script places it).
    /// </summary>
    static Rect Placement(PageScene? scene, WordPicture picture) =>
        scene?.Sheet is { } sheet && sheet.Scale > 0
            ? new Rect(0, -sheet.Offset / sheet.Scale, picture.Width / sheet.Scale, picture.Height / sheet.Scale)
            : new Rect(0, 0, picture.Width, picture.Height);

    /// <summary>Lays the pages out in rows, unzoomed: ours, and in the side mode Word's beside each.</summary>
    void Arrange()
    {
        rows = [];
        var side = comparison == "side";
        var count = Math.Max(scenes.Count, side ? word.Count : 0);
        var top = Edge;
        for (var i = 0; i < count; i++)
        {
            var scene = i < scenes.Count ? scenes[i] : null;
            var picture = i < word.Count ? word[i] : null;
            var ours = scene != null ? PageSize(scene) : new Size(picture!.Width, picture.Height);
            var captionHeight = side ? Caption : 0;
            Rect? theirs = null;
            if (side)
            {
                var box = picture != null && scene?.Sheet == null ? new Size(picture.Width, picture.Height) : ours;
                theirs = new Rect(ours.Width + Beside, captionHeight, box.Width, box.Height);
            }
            var height = Math.Max(ours.Height, theirs?.Height ?? 0) + captionHeight;
            rows.Add(new Row(i, new Rect(0, captionHeight, ours.Width, ours.Height), theirs, top, height));
            top += height + Gap;
        }
        // Pages only Word has come after ours in the blend and difference modes.
        if (!side && comparison != "off")
        {
            for (var i = scenes.Count; i < word.Count; i++)
            {
                rows.Add(new Row(i, default, new Rect(0, Caption, word[i].Width, word[i].Height), top, word[i].Height + Caption));
                top += word[i].Height + Caption + Gap;
            }
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    double NaturalWidth => rows.Count == 0 ? 1 : rows.Max(r => Math.Max(r.Ours.Right, r.Theirs?.Right ?? 0));
    double NaturalHeight => rows.Count == 0 ? 1 : rows.Max(r => r.Height);
    double TotalHeight => rows.Count == 0 ? 0 : rows[^1].Top + rows[^1].Height + Edge;

    protected override Size MeasureOverride(Size availableSize)
    {
        var viewport = scroller?.Bounds.Size ?? availableSize;
        var across = (viewport.Width - 2 * Edge) / NaturalWidth;
        var down = (viewport.Height - 2 * Edge) / NaturalHeight;
        factor = Math.Max(0.05, zoom switch
        {
            "width" => across,
            "page" => Math.Min(across, down),
            double value => value,
            _ => Math.Min(1, across),
        });
        if (double.IsNaN(factor) || double.IsInfinity(factor))
            factor = 1;
        return new Size(Math.Max(viewport.Width, NaturalWidth * factor + 2 * Edge), TotalHeight * factor);
    }

    /// <summary>Where a row's left edge stands in the view (rows are centered).</summary>
    double Left(Row row) => Math.Max(Edge, (Bounds.Width - Math.Max(row.Ours.Right, row.Theirs?.Right ?? 0) * factor) / 2);

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(0x7f, 0x7f, 0x7f)), new Rect(Bounds.Size));
        var view = scroller != null ? new Rect(scroller.Offset.X, scroller.Offset.Y, scroller.Viewport.Width, scroller.Viewport.Height) : new Rect(Bounds.Size);
        var visible = rows.Where(r => r.Top * factor < view.Bottom && (r.Top + r.Height) * factor > view.Top)
            .Select(r => (Row: r, Left: Left(r))).ToList();
        // The pages in view drawn before the frame records them: the document draws its pages when they are wanted.
        foreach (var (row, _) in visible)
        {
            if (row.Index < scenes.Count)
                Page(row.Index);
        }
        // The first frame after an edit tells when it is drawn.
        var (since, drawn) = (editedAt, Drawn);
        editedAt = 0;
        if (visible.Count > 0)
            context.Custom(new Drawing(new Rect(Bounds.Size), visible, factor, scenes, word, comparison, blend, pictures, Marks(visible),
                since == 0 || drawn == null ? null : () => Avalonia.Threading.Dispatcher.UIThread.Post(() => drawn(System.Diagnostics.Stopwatch.GetElapsedTime(since).TotalMilliseconds))));
    }

    /// <summary>What a page shows over its text: the areas of the selection and the cursor (from its top to its bottom), in CSS pixels of the page.</summary>
    sealed record Mark(List<SKPoint[]> Areas, (SKPoint Top, SKPoint Bottom)? Caret);

    /// <summary>The selection and the cursor on the rows in view, by page.</summary>
    Dictionary<int, Mark> Marks(List<(Row Row, double Left)> visible)
    {
        var marks = new Dictionary<int, Mark>();
        if (caret == null)
            return marks;
        var inView = visible.Select(v => v.Row.Index).Where(i => i < scenes.Count).ToHashSet();
        foreach (var (page, from, count) in Selected())
        {
            if (!inView.Contains(page))
                continue;
            var spots = pictures.Spots(Page(page));
            marks[page] = new Mark(pictures.Places(Page(page)).Selection(spots[from], spots[from + count - 1]), null);
        }
        if (caret is var (cursorPage, at) && blink && inView.Contains(cursorPage) && pictures.Spots(Page(cursorPage)) is { Count: > 0 } all)
        {
            var (spot, after) = CaretSpot(all, at);
            var bar = pictures.Places(Page(cursorPage)).CaretAt(spot, after);
            marks[cursorPage] = new Mark(marks.TryGetValue(cursorPage, out var selected) ? selected.Areas : [], bar);
        }
        return marks;
    }

    /// <summary>The page in view: the last whose top is above a third of the view's height.</summary>
    int Current()
    {
        if (scroller == null || rows.Count == 0 || scenes.Count == 0)
            return scenes.Count > 0 ? 1 : 0;
        var mark = scroller.Offset.Y + scroller.Viewport.Height / 3;
        var n = 1;
        foreach (var row in rows)
        {
            if (row.Index >= scenes.Count || (row.Top + row.Ours.Top) * factor > mark)
                break;
            n = row.Index + 1;
        }
        return n;
    }

    void Track()
    {
        var n = Current();
        if (n != shown)
        {
            shown = n;
            PageChanged?.Invoke(n);
        }
    }

    /// <summary>Scrolls to a page's top, or to <paramref name="points"/> below it.</summary>
    public void GoTo(int page, double? points = null)
    {
        if (scroller == null || rows.Count == 0)
            return;
        var row = rows[Math.Clamp(page, 1, rows.Count) - 1];
        var top = points is { } y ? row.Top + row.Ours.Top + y * PxPerPt - Edge : row.Top - Edge;
        scroller.Offset = new Vector(scroller.Offset.X, Math.Max(0, top * factor));
        Track();
    }

    /// <summary>The target of the link under a point of the view, or null.</summary>
    string? LinkAt(Point point)
    {
        foreach (var row in rows)
        {
            if (row.Index >= scenes.Count)
                continue;
            var (x, y) = ((point.X - Left(row)) / factor - row.Ours.X, point.Y / factor - row.Top - row.Ours.Y);
            if (x < 0 || y < 0 || x > row.Ours.Width || y > row.Ours.Height)
                continue;
            return pictures.Links(Page(row.Index)).LastOrDefault(l => l.Area.Contains((float)x, (float)y)).Target;
        }
        return null;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (selecting && PlaceAt(e.GetPosition(this)) is var (page, at, end))
        {
            (caret, lineEnd) = ((page, at), end);
            Moved();
            return;
        }
        Cursor = LinkAt(e.GetPosition(this)) != null ? new Cursor(StandardCursorType.Hand)
            : PlaceAt(e.GetPosition(this), near: false) != null ? new Cursor(StandardCursorType.Ibeam) : Cursor.Default;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        if (LinkAt(e.GetPosition(this)) is { } target)
        {
            e.Handled = true;
            LinkClicked?.Invoke(target);
            return;
        }
        // A click into the text puts the cursor there, with Shift selects up to there; dragging selects.
        if (PlaceAt(e.GetPosition(this)) is var (page, at, end))
        {
            Focus();
            (caret, lineEnd) = ((page, at), end);
            if ((e.KeyModifiers & KeyModifiers.Shift) == 0 || anchor == null)
                anchor = (page, at);
            selecting = true;
            e.Pointer.Capture(this);
            e.Handled = true;
            Moved();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (selecting)
        {
            selecting = false;
            e.Pointer.Capture(null);
        }
    }

    /// <summary>
    /// The place a point of the view hits: on the page it stands on, or with <paramref name="near"/> on the page nearest
    /// to it, the character it is over or the nearest one, before it or after it (see TextPlaces.HitPlace), and whether
    /// that is after a line's last character; null off the text.
    /// </summary>
    (int Page, int At, bool End)? PlaceAt(Point point, bool near = true)
    {
        Row? best = null;
        var bestDistance = double.MaxValue;
        foreach (var row in rows)
        {
            if (row.Index >= scenes.Count)
                continue;
            var y = point.Y / factor - row.Top - row.Ours.Y;
            var distance = y < 0 ? -y : y > row.Ours.Height ? y - row.Ours.Height : 0;
            if (distance < bestDistance)
                (best, bestDistance) = (row, distance);
        }
        if (best == null || !near && bestDistance > 0)
            return null;
        var local = new SKPoint((float)((point.X - Left(best)) / factor - best.Ours.X), (float)(point.Y / factor - best.Top - best.Ours.Y));
        if (!near && (local.X < 0 || local.X > best.Ours.Width))
            return null;
        var places = pictures.Places(Page(best.Index));
        if (places.HitPlace(local) is not var (spot, after))
            return null;
        if (!near)
        {
            // Over the text itself: within its line and character.
            var (toPage, line) = places.Lines[spot.Line];
            if (!toPage.TryInvert(out var inverse))
                return null;
            var p = inverse.MapPoint(local);
            var (x, y) = (p.X / PxPerPt, p.Y / PxPerPt);
            if (y < line.Top || y > line.Bottom || x < Math.Min(line.Starts[spot.Index], line.Ends[spot.Index]) - 2
                || x > Math.Max(line.Starts[spot.Index], line.Ends[spot.Index]) + 2)
                return null;
        }
        return Placed(best.Index, spot, after);
    }

    /// <summary>The place before or after a character of a page, and whether that is after its line's last one.</summary>
    (int Page, int At, bool End) Placed(int page, TextPlaces.Spot spot, bool after)
    {
        var spots = pictures.Spots(Page(page));
        var at = pictures.Index(Page(page), spot) + (after ? 1 : 0);
        return (page, at, after && (at >= spots.Count || spots[at].Line != spot.Line));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (caret is not var (page, at) || page >= scenes.Count)
            return;
        if (Session is { } session)
        {
            var control = (e.KeyModifiers & KeyModifiers.Control) != 0;
            var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
            // Copying, cutting and pasting go through the clipboard as text.
            if (control && !shift && e.Key is Key.C or Key.X or Key.V && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            {
                e.Handled = true;
                if (e.Key == Key.V)
                    Paste(clipboard);
                else if (session.Selecting)
                {
                    try
                    {
                        _ = clipboard.SetTextAsync(session.Selected);
                    }
                    catch (NotSupportedException refused)
                    {
                        Refused?.Invoke(refused.Message);
                        return;
                    }
                    if (e.Key == Key.X)
                        Edit(session.Delete);
                }
                return;
            }
            Action? edit = e.Key switch
            {
                Key.Back => session.Backspace,
                Key.Delete => session.Delete,
                Key.Enter when !control && !shift => session.Enter,
                Key.Enter when shift && !control => session.LineBreak,
                Key.A when control => session.SelectAll,
                Key.Z when control && !shift => () => session.Undo(),
                Key.Y when control => () => session.Redo(),
                Key.Z when control && shift => () => session.Redo(),
                Key.B when control => () => Toggle(PlacesItem.Bold),
                Key.I when control => () => Toggle(PlacesItem.Italic),
                Key.U when control => () => Toggle(PlacesItem.Underlined),
                _ => null,
            };
            if (edit != null)
            {
                e.Handled = true;
                Edit(edit);
                return;
            }
        }
        var places = pictures.Places(Page(page));
        var spots = pictures.Spots(Page(page));
        (int Page, int At, bool End)? moved = e.Key switch
        {
            Key.Left => at > 0 ? (page, at - 1, false) : page > 0 ? (page - 1, pictures.Spots(Page(page - 1)).Count, true) : null,
            Key.Right => at < spots.Count ? (page, at + 1, false) : page + 1 < scenes.Count ? (page + 1, 0, false) : null,
            Key.Home or Key.End when spots.Count > 0 => LineEdge(page, at, e.Key == Key.End),
            Key.Up or Key.Down when spots.Count > 0 => LineAcross(page, at, e.Key == Key.Down),
            _ => null,
        };
        if (moved is not var (movedPage, movedAt, movedEnd))
            return;
        e.Handled = true;
        (caret, lineEnd) = ((movedPage, movedAt), movedEnd);
        if ((e.KeyModifiers & KeyModifiers.Shift) == 0)
            anchor = (movedPage, movedAt);
        Moved();
        Reveal((movedPage, movedAt));
    }

    /// <summary>The first or the last place of the cursor's line: after its last character, before the paragraph's mark.</summary>
    (int Page, int At, bool End)? LineEdge(int page, int at, bool end)
    {
        var spots = pictures.Spots(Page(page));
        var line = CaretSpot(spots, at).Spot.Line;
        var first = spots.FindIndex(s => s.Line == line);
        var last = spots.FindLastIndex(s => s.Line == line);
        if (!end)
            return (page, first, false);
        var places = pictures.Places(Page(page)).Lines[line].Line;
        return places.Starts[spots[last].Index] == places.Ends[spots[last].Index] ? (page, last, false) : (page, last + 1, true);
    }

    /// <summary>The place in the line above or below the cursor's that stands nearest to it, on the next page over its edge.</summary>
    (int Page, int At, bool End)? LineAcross(int page, int at, bool down)
    {
        var places = pictures.Places(Page(page));
        var spots = pictures.Spots(Page(page));
        var (current, currentAfter) = CaretSpot(spots, at);
        var (top, bottom) = places.CaretAt(current, currentAfter);
        var x = top.X;
        // The nearest line beside this one, in the page's order of lines.
        var lineIndex = current.Line + (down ? 1 : -1);
        while (lineIndex >= 0 && lineIndex < places.Lines.Count && places.Lines[lineIndex].Line.Offsets.Length == 0)
            lineIndex += down ? 1 : -1;
        if (lineIndex < 0 || lineIndex >= places.Lines.Count)
        {
            var next = page + (down ? 1 : -1);
            if (next < 0 || next >= scenes.Count)
                return null;
            var nextSpots = pictures.Spots(Page(next));
            return nextSpots.Count == 0 ? null : (next, down ? 0 : nextSpots.Count, !down);
        }
        var (toPage, line) = places.Lines[lineIndex];
        var middle = toPage.MapPoint((float)(line.Starts[0] * PxPerPt), (float)((line.Top + line.Bottom) / 2 * PxPerPt));
        var target = new SKPoint(x, middle.Y);
        return places.HitPlace(target) is var (spot, after) ? Placed(page, spot, after) : null;
    }

    /// <summary>Scrolls the cursor's page into view where it is out of it.</summary>
    void Reveal((int Page, int At) place)
    {
        if (scroller == null || place.Page >= rows.Count)
            return;
        var row = rows[place.Page];
        var spots = pictures.Spots(Page(place.Page));
        if (spots.Count == 0)
            return;
        var (spot, after) = CaretSpot(spots, place.At);
        var (top, bottom) = pictures.Places(Page(place.Page)).CaretAt(spot, after);
        var (y1, y2) = ((row.Top + row.Ours.Y + top.Y) * factor, (row.Top + row.Ours.Y + bottom.Y) * factor);
        if (y1 < scroller.Offset.Y)
            scroller.Offset = new Vector(scroller.Offset.X, Math.Max(0, y1 - 24));
        else if (y2 > scroller.Offset.Y + scroller.Viewport.Height)
            scroller.Offset = new Vector(scroller.Offset.X, y2 - scroller.Viewport.Height + 24);
    }

    /// <summary>The cursor moved: it shows (and blinks again from there), and the window learns its place.</summary>
    void Moved()
    {
        blink = true;
        blinker.Stop();
        blinker.Start();
        InvalidateVisual();
        if (caret is not var (page, at) || page >= scenes.Count)
        {
            PlaceChanged?.Invoke(null);
            return;
        }
        var spots = pictures.Spots(Page(page));
        if (spots.Count == 0)
        {
            PlaceChanged?.Invoke(null);
            return;
        }
        var places = pictures.Places(Page(page));
        var (spot, after) = CaretSpot(spots, at);
        var (block, offset) = places.PlaceAt(spot, after);
        PlaceChanged?.Invoke((block, offset, Selected().Sum(s => s.Count)));
        // The session's cursor and anchor follow the view's, unless they came from it.
        if (Session != null && !fromSession && anchor is var (anchorPage, anchorAt) && anchorPage < scenes.Count
            && pictures.Spots(Page(anchorPage)) is { Count: > 0 } anchorSpots)
        {
            var (from, fromAfter) = anchorAt >= anchorSpots.Count ? (anchorSpots[^1], true) : (anchorSpots[anchorAt], false);
            var (anchorBlock, anchorOffset) = pictures.Places(Page(anchorPage)).PlaceAt(from, fromAfter);
            Session.Place(new TextPlace(anchorBlock, anchorOffset));
            Session.Place(new TextPlace(block, offset), extend: true);
        }
    }

    /// <summary>
    /// Makes an edit through the session: the pages after it drawn (those it left as they were from their recordings), the
    /// cursor and the selection where the session has them; a refusal is reported and changes nothing.
    /// </summary>
    void Edit(Action change)
    {
        if (Session is not { } session)
            return;
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            change();
        }
        catch (Exception e) when (e is NotSupportedException or ArgumentException)
        {
            Refused?.Invoke(e.Message);
            return;
        }
        Update(session.Document.Scenes());
        Show(session.Caret, session.Anchor);
        editedAt = started;
        Edited?.Invoke(System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        LayOutLater();
    }

    /// <summary>
    /// Lays out what the document left of its pages, after an edit or as it opens (see IEditableDocument.LayingOut),
    /// slice by slice while the window waits: at the dispatcher's background priority, after input and rendering; the
    /// pages shown anew after each slice, the cursor and the selection placed anew where the slice changed the cursor's
    /// page.
    /// </summary>
    public void LayOutLater()
    {
        if (layingOut || Session?.Document is not { LayingOut: true })
            return;
        layingOut = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            layingOut = false;
            if (Session is not { Document: { LayingOut: true } document } session)
                return;
            document.LayOut();
            var shown = caret is var (page, _) && page < scenes.Count ? scenes[page] : null;
            Update(document.Scenes());
            if (shown != null && (caret is not var (now, _) || now >= scenes.Count || !ReferenceEquals(scenes[now], shown)))
                Show(session.Caret, session.Anchor);
            LaidOut?.Invoke();
            LayOutLater();
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    /// <summary>A slice of the pages left to lay out was laid out (see LayOutLater).</summary>
    public event Action? LaidOut;

    /// <summary>
    /// All pages laid out and drawn, for printing or writing them (in a long document this takes its time): a list of its
    /// own, as the document keeps only so many of them drawn (see IEditableDocument.Scene).
    /// </summary>
    public IReadOnlyList<PageScene> Finished()
    {
        if (Session?.Document is not { } document)
            return scenes;
        while (document.LayingOut)
            document.LayOut(int.MaxValue);
        Update(document.Scenes());
        var drawn = new PageScene[scenes.Count];
        for (var k = 0; k < scenes.Count; k++)
            drawn[k] = Page(k);
        return drawn;
    }

    /// <summary>A page drawn, for what its items tell (see Page).</summary>
    public PageScene DrawnPage(int page) => Page(page);

    /// <summary>A page's scene drawn: the document draws its pages when they are wanted (see IEditableDocument.Scene).</summary>
    PageScene Page(int index) => scenes[index] is { Drawn: false } && Session?.Document is { } document ? document.Scene(index) : scenes[index];

    /// <summary>The pages as the document reads now: recordings kept for the scenes that stayed, the rows laid out again where pages came, went or changed size.</summary>
    public void Update(IReadOnlyList<PageScene> changed)
    {
        var resized = changed.Count != scenes.Count;
        if (resized)
            pictures.Keep(changed);
        else
        {
            // As many pages as before: those whose scene changed forget their recording, those that changed size are laid out anew.
            var gone = new List<PageScene>();
            for (var i = 0; i < changed.Count; i++)
            {
                if (ReferenceEquals(changed[i], scenes[i]))
                    continue;
                gone.Add(scenes[i]);
                resized |= changed[i].Width != scenes[i].Width || changed[i].Height != scenes[i].Height;
            }
            pictures.Forget(gone);
        }
        scenes = changed;
        if (resized)
            Arrange();
        else
            InvalidateVisual();
    }

    /// <summary>Puts the cursor and the selection's anchor at places of the document's text, on the pages that show them.</summary>
    public void Show(TextPlace caretAt, TextPlace anchorAt)
    {
        var near = caret?.Page ?? 0;
        if (Locate(caretAt, near) is not var (page, at, end))
            return;
        (caret, lineEnd) = ((page, at), end);
        anchor = anchorAt == caretAt ? (page, at) : Locate(anchorAt, page) is var (anchorPage, anchorIndex, _) ? (anchorPage, anchorIndex) : (page, at);
        fromSession = true;
        try
        {
            Moved();
        }
        finally
        {
            fromSession = false;
        }
        Reveal((page, at));
    }

    /// <summary>
    /// Where a page shows a place: the character at its offset on the page nearest to <paramref name="near"/> that shows it,
    /// else the character before it (a place within a field's result); null where no page shows its paragraph.
    /// </summary>
    (int Page, int At, bool End)? Locate(TextPlace place, int near)
    {
        for (var step = 0; step < scenes.Count; step++)
        {
            foreach (var page in step == 0 ? [near] : new[] { near + step, near - step })
            {
                if (page >= 0 && page < scenes.Count && pictures.Places(Page(page)).Exact(place.Paragraph, place.Offset) is { } spot)
                    return Placed(page, spot, false);
            }
        }
        for (var page = 0; page < scenes.Count; page++)
        {
            if (pictures.Places(Page(page)).Find(place.Paragraph, place.Offset) is var (spot, after))
                return Placed(page, spot, after);
        }
        return null;
    }

    /// <summary>The formatting the selection's first character shows, else the character before the cursor (see PlacesItem.Formats).</summary>
    byte FormatHere()
    {
        if (Selected() is [var (page, from, _), ..])
            return pictures.Places(Page(page)).FormatAt(pictures.Spots(Page(page))[from]);
        if (caret is not var (cursorPage, at) || pictures.Spots(Page(cursorPage)) is not { Count: > 0 } spots)
            return 0;
        return pictures.Places(Page(cursorPage)).FormatAt(spots[Math.Clamp(at - 1, 0, spots.Count - 1)]);
    }

    /// <summary>
    /// Turns bold, italic or underline on for the selection where its first character lacks it, else off (Ctrl+B, I, U);
    /// without a selection as the format given for typing has it, else the character before the cursor.
    /// </summary>
    void Toggle(byte flag)
    {
        var pending = Session!.Pending;
        var on = flag switch
        {
            PlacesItem.Bold when !Session.Selecting && pending?.Bold is { } bold => bold,
            PlacesItem.Italic when !Session.Selecting && pending?.Italic is { } italic => italic,
            PlacesItem.Underlined when !Session.Selecting && pending?.Underline is { } underline => underline != "none",
            _ => (FormatHere() & flag) != 0,
        };
        Session!.Format(flag switch
        {
            PlacesItem.Bold => new TextFormatting { Bold = !on },
            PlacesItem.Italic => new TextFormatting { Italic = !on },
            _ => new TextFormatting { Underline = on ? "none" : "single" },
        });
    }

    /// <summary>Pastes the clipboard's text at the cursor in place of the selection (Ctrl+V).</summary>
    async void Paste(IClipboard clipboard)
    {
        if (await clipboard.TryGetTextAsync() is { Length: > 0 } text && Session is { } session)
            Edit(() => session.Paste(text));
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (Session == null || caret == null || string.IsNullOrEmpty(e.Text) || e.Text.Any(char.IsControl))
            return;
        e.Handled = true;
        var text = e.Text;
        Edit(() => Session.Type(text));
    }

    /// <summary>The selection page by page: the page and the range of its characters (from the index, so many), in order.</summary>
    List<(int Page, int From, int Count)> Selected()
    {
        if (caret is not var (page, at) || anchor is not var (anchorPage, anchorAt) || (page, at) == (anchorPage, anchorAt))
            return [];
        var ((p1, a1), (p2, a2)) = (anchorPage, anchorAt).CompareTo((page, at)) < 0 ? ((anchorPage, anchorAt), (page, at)) : ((page, at), (anchorPage, anchorAt));
        var result = new List<(int, int, int)>();
        for (var p = p1; p <= p2 && p < scenes.Count; p++)
        {
            var count = pictures.Spots(Page(p)).Count;
            var (from, to) = (p == p1 ? a1 : 0, p == p2 ? a2 : count);
            if (to > from)
                result.Add((p, from, to - from));
        }
        return result;
    }

    /// <summary>The position in the document: the page in view and how far down it the view's top is (a share of its height).</summary>
    public (int Page, double Share) Position()
    {
        var n = Current();
        if (n == 0 || scroller == null || rows.Count < n)
            return (0, 0);
        var row = rows[n - 1];
        var height = row.Height * factor;
        return (n, height > 0 ? (scroller.Offset.Y - row.Top * factor) / height : 0);
    }

    /// <summary>Scrolls back to a position (see Position), after a change of zoom or of the document.</summary>
    public void Restore((int Page, double Share) position)
    {
        if (scroller == null || position.Page == 0 || rows.Count == 0)
            return;
        var row = rows[Math.Min(position.Page, rows.Count) - 1];
        scroller.Offset = new Vector(scroller.Offset.X, Math.Max(0, (row.Top + position.Share * row.Height) * factor));
        Track();
    }

    /// <summary>The pages' recorded drawings and links and Word's pictures, made once and drawn on the render thread.</summary>
    sealed class Pictures
    {
        /// <summary>The recordings, as long as their scenes live: a document keeps only so many pages drawn (see IEditableDocument.Scene).</summary>
        readonly ConditionalWeakTable<PageScene, Recording> pages = new();
        readonly Dictionary<string, SKImage?> images = [];

        /// <summary>
        /// A page recorded once: its drawing, where its links stand on it in CSS pixels and where they lead, its places,
        /// its characters in order (TextPlaces.Spots) and where each line's begin among them.
        /// </summary>
        sealed record Recording(SKPicture Picture, List<(SKRect Area, string Target)> Links, TextPlaces Places, List<TextPlaces.Spot> Spots, int[] LineStarts);

        public void Clear()
        {
            lock (this)
            {
                foreach (var (_, recording) in pages)
                    recording.Picture.Dispose();
                pages.Clear();
            }
        }

        /// <summary>Forgets the recordings of those pages (not disposed: a frame may still draw them).</summary>
        public void Forget(List<PageScene> scenes)
        {
            lock (this)
            {
                foreach (var gone in scenes)
                    pages.Remove(gone);
            }
        }

        /// <summary>Forgets the recordings of pages not among <paramref name="scenes"/> (not disposed: a frame may still draw them).</summary>
        public void Keep(IReadOnlyList<PageScene> scenes)
        {
            lock (this)
            {
                var kept = scenes.ToHashSet(ReferenceEqualityComparer.Instance);
                foreach (var gone in pages.Select(page => page.Key).Where(scene => !kept.Contains(scene)).ToList())
                    pages.Remove(gone);
            }
        }

        public SKPicture Page(PageScene scene) => Recorded(scene).Picture;

        /// <summary>Where the page's links stand on it in CSS pixels and where they lead.</summary>
        public List<(SKRect Area, string Target)> Links(PageScene scene) => Recorded(scene).Links;

        /// <summary>The places of the page's text (see TextPlaces).</summary>
        public TextPlaces Places(PageScene scene) => Recorded(scene).Places;

        /// <summary>The page's characters in order, line after line.</summary>
        public List<TextPlaces.Spot> Spots(PageScene scene) => Recorded(scene).Spots;

        /// <summary>Where a character stands among the page's characters in order.</summary>
        public int Index(PageScene scene, TextPlaces.Spot spot) => Recorded(scene).LineStarts[spot.Line] + spot.Index;

        Recording Recorded(PageScene scene)
        {
            lock (this)
            {
                if (pages.TryGetValue(scene, out var recorded))
                    return recorded;
                using var recorder = new SKPictureRecorder();
                var size = PageSize(scene);
                var links = new List<(SKRect Area, string Target)>();
                var lines = new List<(SKMatrix ToPage, PlacesItem Line)>();
                SkiaScene.Draw(scene, recorder.BeginRecording(SKRect.Create((float)size.Width, (float)size.Height)), links: links, places: lines);
                var places = new TextPlaces(lines);
                var starts = new int[lines.Count];
                for (var k = 1; k < lines.Count; k++)
                    starts[k] = starts[k - 1] + lines[k - 1].Line.Offsets.Length;
                var recording = new Recording(recorder.EndRecording(), links, places, places.Spots(), starts);
                pages.AddOrUpdate(scene, recording);
                return recording;
            }
        }

        public SKImage? Image(string file)
        {
            lock (this)
            {
                if (!images.TryGetValue(file, out var image))
                {
                    try
                    {
                        image = SKImage.FromEncodedData(File.ReadAllBytes(file));
                    }
                    catch (IOException)
                    {
                        image = null;
                    }
                    images[file] = image;
                }
                return image;
            }
        }
    }

    /// <summary>The rows in view drawn on Avalonia's Skia canvas.</summary>
    sealed class Drawing(Rect bounds, List<(Row Row, double Left)> rows, double factor, IReadOnlyList<PageScene> scenes, IReadOnlyList<WordPicture> word,
        string comparison, double blend, Pictures pictures, Dictionary<int, Mark> marks, Action? done) : ICustomDrawOperation
    {
        public Rect Bounds => bounds;

        public bool HitTest(Point p) => false;

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Dispose() { }

        public void Render(ImmediateDrawingContext context)
        {
            if (context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not { } feature)
                return;
            using var lease = feature.Lease();
            var canvas = lease.SkCanvas;
            using var captionFont = new SKFont(SKTypeface.Default, 12);
            using var captionPaint = new SKPaint { Color = SKColors.White.WithAlpha(217), IsAntialias = true };
            using var missing = new SKPaint { Color = SKColors.White.WithAlpha(153), Style = SKPaintStyle.Stroke, StrokeWidth = 2, PathEffect = SKPathEffect.CreateDash([6, 4], 0) };
            foreach (var (row, left) in rows)
            {
                canvas.Save();
                canvas.Translate((float)left, (float)(row.Top * factor));
                canvas.Scale((float)factor);
                var scene = row.Index < scenes.Count ? scenes[row.Index] : null;
                var picture = row.Index < word.Count ? word[row.Index] : null;
                var side = comparison == "side";
                if (side)
                    canvas.DrawText($"FreeSlate · Seite {row.Index + 1}", 0, 14, SKTextAlign.Left, captionFont, captionPaint);
                if (scene != null)
                {
                    canvas.Save();
                    canvas.Translate((float)row.Ours.X, (float)row.Ours.Y);
                    canvas.ClipRect(SKRect.Create((float)row.Ours.Width, (float)row.Ours.Height));
                    // The recording kept alive while it plays: an edit may forget it meanwhile, and its finalizer must
                    // not free it under the canvas (a crash while typing fast).
                    var page = pictures.Page(scene);
                    canvas.DrawPicture(page);
                    GC.KeepAlive(page);
                    // The selection over the text, the cursor at its place.
                    if (marks.TryGetValue(row.Index, out var mark))
                    {
                        using var selected = new SKPaint { Color = new SKColor(0x33, 0x99, 0xFF, 0x60), IsAntialias = true };
                        foreach (var area in mark.Areas)
                        {
                            using var path = new SKPath();
                            path.AddPoly(area);
                            canvas.DrawPath(path, selected);
                        }
                        if (mark.Caret is var (top, bottom))
                        {
                            using var pen = new SKPaint { Color = SKColors.Black, StrokeWidth = (float)Math.Max(1, 1.5 / factor), IsAntialias = true };
                            canvas.DrawLine(top, bottom, pen);
                        }
                    }
                    // Word's page over ours: blended, or as the difference of the two.
                    if (!side && comparison != "off" && picture != null && pictures.Image(picture.File) is { } over)
                    {
                        var at = Placement(scene, picture);
                        using var paint = comparison == "diff"
                            ? new SKPaint { BlendMode = SKBlendMode.Difference }
                            : new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(blend * 255)) };
                        canvas.DrawImage(over, SKRect.Create((float)at.X, (float)at.Y, (float)at.Width, (float)at.Height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), paint);
                    }
                    canvas.Restore();
                }
                else if (side)
                    canvas.DrawRect(SKRect.Create((float)row.Ours.X, (float)row.Ours.Y, (float)row.Ours.Width, (float)row.Ours.Height), missing);
                if (row.Theirs is { } theirs)
                {
                    var caption = scene == null && !side ? $"Nur bei Word · Seite {row.Index + 1}" : $"Word · Seite {row.Index + 1}";
                    canvas.DrawText(caption, (float)theirs.X, 14, SKTextAlign.Left, captionFont, captionPaint);
                    canvas.Save();
                    canvas.Translate((float)theirs.X, (float)theirs.Y);
                    canvas.ClipRect(SKRect.Create((float)theirs.Width, (float)theirs.Height));
                    if (picture != null && pictures.Image(picture.File) is { } image)
                    {
                        using var paper = new SKPaint { Color = SKColors.White };
                        canvas.DrawRect(SKRect.Create((float)theirs.Width, (float)theirs.Height), paper);
                        var at = Placement(scene, picture);
                        canvas.DrawImage(image, SKRect.Create((float)at.X, (float)at.Y, (float)at.Width, (float)at.Height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
                    }
                    else
                        canvas.DrawRect(SKRect.Create((float)theirs.Width, (float)theirs.Height), missing);
                    canvas.Restore();
                }
                canvas.Restore();
            }
            // A frame may draw the operation again: what waits for its first drawing hears of that once.
            Interlocked.Exchange(ref done, null)?.Invoke();
        }
    }
}
