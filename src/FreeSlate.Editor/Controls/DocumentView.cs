using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Input.TextInput;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FreeSlate.Editing;
using FreeSlate.Rendering;
using FreeSlate.Skia;
using SkiaSharp;

namespace FreeSlate.Editor.Controls;

/// <summary>
/// A document as its sheets, one below the other in a scroll viewer and drawn by Skia from their scenes, only those in view. Keys and
/// typed text go to its editing session and the sheets a change touched are drawn again; an input method's text being composed shows
/// at the cursor until it is committed.
/// </summary>
public sealed class DocumentView : Control
{
    const double PxPerPt = 96.0 / 72;
    const double Edge = 16, Gap = 16;

    public static readonly StyledProperty<IBrush?> SheetProperty = AvaloniaProperty.Register<DocumentView, IBrush?>(nameof(Sheet));

    public static readonly StyledProperty<BoxShadows> SheetShadowProperty = AvaloniaProperty.Register<DocumentView, BoxShadows>(nameof(SheetShadow));

    public static readonly StyledProperty<IBrush?> CaretBrushProperty = AvaloniaProperty.Register<DocumentView, IBrush?>(nameof(CaretBrush));

    public static readonly StyledProperty<IBrush?> SelectionBrushProperty = AvaloniaProperty.Register<DocumentView, IBrush?>(nameof(SelectionBrush));

    static readonly Cursor Text = new(StandardCursorType.Ibeam);

    static DocumentView() => AffectsRender<DocumentView>(SheetProperty, SheetShadowProperty, CaretBrushProperty, SelectionBrushProperty);

    readonly Pictures pictures = new();
    readonly DispatcherTimer blinker = new() { Interval = TimeSpan.FromMilliseconds(530) };
    readonly Ime ime;
    EditSession? session;
    IReadOnlyList<PageScene> scenes = [];
    List<Row> rows = [];
    double factor = 1;
    ScrollViewer? scroller;
    WindowBase? window;

    /// <summary>
    /// The cursor and where the selection began, each a page (its index) and a place among the page's characters in order
    /// (TextPlaces.Spots): before the character of that index, the count of them after the last.
    /// </summary>
    (int Page, int At)? caret, anchor;

    /// <summary>
    /// Whether the cursor, where its place begins a line other than the character before it, stands at the end of that character's
    /// line (after a click there or End) rather than at the start of the next one.
    /// </summary>
    bool lineEnd;

    bool selecting, fromSession, layingOut, blink = true;

    /// <summary>The input method's text being composed at the cursor, not yet in the document.</summary>
    string? preedit;

    /// <summary>The copy last made, with its formatting, for pasting it as it was while the clipboard still holds its text.</summary>
    Fragment? copied;

    public DocumentView()
    {
        ClipToBounds = true;
        Focusable = true;
        Cursor = Text;
        blinker.Tick += (_, _) =>
        {
            blink = !blink;
            InvalidateVisual();
        };
        ime = new Ime(this);
        TextInputMethodClientRequested += (_, e) =>
        {
            if (session != null)
                e.Client = ime;
        };
    }

    public IBrush? Sheet
    {
        get => GetValue(SheetProperty);
        set => SetValue(SheetProperty, value);
    }

    public BoxShadows SheetShadow
    {
        get => GetValue(SheetShadowProperty);
        set => SetValue(SheetShadowProperty, value);
    }

    public IBrush? CaretBrush
    {
        get => GetValue(CaretBrushProperty);
        set => SetValue(CaretBrushProperty, value);
    }

    public IBrush? SelectionBrush
    {
        get => GetValue(SelectionBrushProperty);
        set => SetValue(SelectionBrushProperty, value);
    }

    /// <summary>Whether the document holds nothing yet: one empty paragraph.</summary>
    public bool IsEmpty { get; private set; } = true;

    /// <summary>The document came to hold something, or nothing any more.</summary>
    public event Action? IsEmptyChanged;

    /// <summary>Shows the session's document from its top, the cursor at the beginning of its text; the page's first place may be a header's.</summary>
    public void Open(EditSession editing)
    {
        session = editing;
        scenes = editing.Document.Scenes();
        Arrange();
        if (scroller != null)
            scroller.Offset = default;
        (caret, anchor, lineEnd) = ((0, 0), (0, 0), false);
        if (editing.Document.Beginning is { } first && Locate(new TextPlace(first, 0), 0) != null)
        {
            editing.Place(new TextPlace(first, 0));
            Show(editing.Caret, editing.Anchor);
        }
        else
            Moved();
        CheckEmpty();
        LayOutLater();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        scroller = this.FindAncestorOfType<ScrollViewer>();
        if (scroller != null)
        {
            scroller.ScrollChanged += OnScrolled;
            scroller.SizeChanged += OnResized;
        }
        window = TopLevel.GetTopLevel(this) as WindowBase;
        if (window != null)
        {
            window.Activated += OnWindowActivity;
            window.Deactivated += OnWindowActivity;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        blinker.Stop();
        if (scroller != null)
        {
            scroller.ScrollChanged -= OnScrolled;
            scroller.SizeChanged -= OnResized;
        }
        scroller = null;
        if (window != null)
        {
            window.Activated -= OnWindowActivity;
            window.Deactivated -= OnWindowActivity;
        }
        window = null;
        base.OnDetachedFromVisualTree(e);
    }

    void OnScrolled(object? sender, ScrollChangedEventArgs e)
    {
        InvalidateVisual();
        ime.Moved();
    }

    void OnResized(object? sender, SizeChangedEventArgs e) => InvalidateMeasure();

    void OnWindowActivity(object? sender, EventArgs e) => Blink();

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        Blink();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        Blink();
    }

    /// <summary>The cursor shows anew and blinks from there, where the keyboard's input comes to it; otherwise it stays away.</summary>
    void Blink()
    {
        var ours = IsFocused && (window == null || window.IsActive);
        blink = ours;
        blinker.Stop();
        if (ours)
            blinker.Start();
        InvalidateVisual();
    }

    static Size PageSize(PageScene scene) => new(scene.Width * PxPerPt, scene.Height * PxPerPt);

    /// <summary>Where a row of the layout stands (unzoomed CSS pixels): its page's size and top.</summary>
    sealed record Row(int Index, Size Size, double Top);

    /// <summary>Lays the pages out in rows, unzoomed.</summary>
    void Arrange()
    {
        rows = [];
        var top = Edge;
        for (var i = 0; i < scenes.Count; i++)
        {
            var size = PageSize(scenes[i]);
            rows.Add(new Row(i, size, top));
            top += size.Height + Gap;
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    double NaturalWidth => rows.Count == 0 ? 1 : rows.Max(r => r.Size.Width);
    double TotalHeight => rows.Count == 0 ? 0 : rows[^1].Top + rows[^1].Size.Height + Edge;

    /// <summary>A page at its size where it fits the width, smaller where it does not.</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var viewport = scroller?.Bounds.Size ?? availableSize;
        factor = Math.Max(0.05, Math.Min(1, (viewport.Width - 2 * Edge) / NaturalWidth));
        if (double.IsNaN(factor) || double.IsInfinity(factor))
            factor = 1;
        return new Size(Math.Max(viewport.Width, NaturalWidth * factor + 2 * Edge), Math.Max(viewport.Height, TotalHeight * factor));
    }

    double Left(Row row) => Math.Max(Edge, (Bounds.Width - row.Size.Width * factor) / 2);

    Rect SheetRect(Row row) => new(Left(row), row.Top * factor, row.Size.Width * factor, row.Size.Height * factor);

    public override void Render(DrawingContext context)
    {
        // Clicks count everywhere, also beside the sheets.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        var view = scroller != null ? new Rect(scroller.Offset.X, scroller.Offset.Y, scroller.Viewport.Width, scroller.Viewport.Height) : new Rect(Bounds.Size);
        var visible = rows.Where(r => r.Top * factor < view.Bottom && (r.Top + r.Size.Height) * factor > view.Top).Select(r => (Row: r, Left: Left(r))).ToList();
        if (visible.Count == 0)
            return;
        foreach (var (row, _) in visible)
        {
            context.DrawRectangle(Sheet, null, new RoundedRect(SheetRect(row)), SheetShadow);
            // The document draws its pages when they are wanted, before the frame records them.
            Page(row.Index);
        }
        context.Custom(new Drawing(new Rect(Bounds.Size), visible, factor, scenes, pictures, Marks(visible), Skia(SelectionBrush), Skia(CaretBrush)));
        // The text being composed over the page at the cursor, underlined, as input methods show it.
        if (preedit != null && CaretRect() is { Height: > 0 } bar)
        {
            var text = new FormattedText(preedit, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, Math.Max(8, bar.Height * 0.72), CaretBrush);
            var box = new Rect(bar.X, bar.Y, text.Width + 4, bar.Height);
            context.FillRectangle(Sheet ?? Brushes.White, box);
            context.DrawText(text, new Point(bar.X + 2, bar.Y + (bar.Height - text.Height) / 2));
            context.DrawLine(new Pen(CaretBrush), new Point(box.X + 2, box.Bottom - 1.5), new Point(box.Right - 2, box.Bottom - 1.5));
        }
    }

    static SKColor Skia(IBrush? brush) => brush is ISolidColorBrush { Color: var c } ? new SKColor(c.R, c.G, c.B, c.A) : SKColors.Black;

    /// <summary>The character the cursor stands at on its page, before it, or after it at the end of its line.</summary>
    (TextPlaces.Spot Spot, bool After) CaretSpot(List<TextPlaces.Spot> spots, int at) =>
        at >= spots.Count || at > 0 && lineEnd && spots[at - 1].Line != spots[at].Line ? (spots[Math.Min(at, spots.Count) - 1], true) : (spots[at], false);

    /// <summary>What a page shows over its text: the areas of the selection and the cursor (from its top to its bottom), in CSS pixels of the page.</summary>
    sealed record Mark(List<SKPoint[]> Areas, (SKPoint Top, SKPoint Bottom)? Caret);

    /// <summary>The selection and the cursor on the rows in view, by page.</summary>
    Dictionary<int, Mark> Marks(List<(Row Row, double Left)> visible)
    {
        var marks = new Dictionary<int, Mark>();
        if (caret == null)
            return marks;
        var inView = visible.Select(v => v.Row.Index).ToHashSet();
        foreach (var (page, from, count) in Selected())
        {
            if (!inView.Contains(page))
                continue;
            var spots = pictures.Spots(Page(page));
            marks[page] = new Mark(pictures.Places(Page(page)).Selection(spots[from], spots[from + count - 1]), null);
        }
        if (caret is var (cursorPage, at) && blink && preedit == null && inView.Contains(cursorPage) && pictures.Spots(Page(cursorPage)) is { Count: > 0 } all)
        {
            var (spot, after) = CaretSpot(all, at);
            var bar = pictures.Places(Page(cursorPage)).CaretAt(spot, after);
            marks[cursorPage] = new Mark(marks.TryGetValue(cursorPage, out var selected) ? selected.Areas : [], bar);
        }
        return marks;
    }

    /// <summary>The cursor's bar in the view's coordinates (its top to its bottom, a pixel wide), or an empty rectangle.</summary>
    Rect CaretRect()
    {
        if (caret is not var (page, at) || page >= rows.Count || pictures.Spots(Page(page)) is not { Count: > 0 } spots)
            return default;
        var row = rows[page];
        var (spot, after) = CaretSpot(spots, at);
        var (top, bottom) = pictures.Places(Page(page)).CaretAt(spot, after);
        var (y1, y2) = ((row.Top + top.Y) * factor, (row.Top + bottom.Y) * factor);
        return new Rect(Left(row) + top.X * factor, Math.Min(y1, y2), 1, Math.Abs(y2 - y1));
    }

    void Preedit(string? text)
    {
        preedit = string.IsNullOrEmpty(text) ? null : text;
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (selecting && PlaceAt(e.GetPosition(this)) is var (page, at, end))
        {
            (caret, lineEnd) = ((page, at), end);
            Moved();
        }
    }

    /// <summary>A click puts the cursor at the place nearest to it, with Shift selects up to there; dragging selects.</summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        Focus();
        if (PlaceAt(e.GetPosition(this)) is var (page, at, end))
        {
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
    /// The place a point of the view hits, on the page nearest to it: the character it is over or the nearest one, before it or after it
    /// (see TextPlaces.HitPlace), and whether that is after a line's last character; null without text.
    /// </summary>
    (int Page, int At, bool End)? PlaceAt(Point point)
    {
        Row? best = null;
        var bestDistance = double.MaxValue;
        foreach (var row in rows)
        {
            var y = point.Y / factor - row.Top;
            var distance = y < 0 ? -y : y > row.Size.Height ? y - row.Size.Height : 0;
            if (distance < bestDistance)
                (best, bestDistance) = (row, distance);
        }
        if (best == null)
            return null;
        var local = new SKPoint((float)((point.X - Left(best)) / factor), (float)(point.Y / factor - best.Top));
        return pictures.Places(Page(best.Index)).HitPlace(local) is var (spot, after) ? Placed(best.Index, spot, after) : null;
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
        if (e.Handled || session is not { } editing || caret is not var (page, at) || page >= scenes.Count)
            return;
        var control = (e.KeyModifiers & KeyModifiers.Control) != 0 || (e.KeyModifiers & KeyModifiers.Meta) != 0;
        var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        if (control && !shift && e.Key is Key.C or Key.X or Key.V && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            e.Handled = true;
            if (e.Key == Key.V)
                Paste(clipboard);
            else if (editing.Selecting)
            {
                try
                {
                    copied = editing.Copy();
                    _ = clipboard.SetTextAsync(copied.Text);
                }
                catch (NotSupportedException)
                {
                    return;
                }
                if (e.Key == Key.X)
                    Edit(editing.Delete);
            }
            return;
        }
        Action? edit = e.Key switch
        {
            Key.Back => editing.Backspace,
            Key.Delete => editing.Delete,
            Key.Enter when !control && !shift => editing.Enter,
            Key.Enter when shift && !control => editing.LineBreak,
            Key.Tab when !control && !shift => () => editing.Type("\t"),
            Key.A when control => editing.SelectAll,
            Key.Z when control && !shift => () => editing.Undo(),
            Key.Y when control => () => editing.Redo(),
            Key.Z when control && shift => () => editing.Redo(),
            _ => null,
        };
        if (edit != null)
        {
            e.Handled = true;
            Edit(edit);
            return;
        }
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
        if (!shift)
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
        var x = places.CaretAt(current, currentAfter).Top.X;
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
        return places.HitPlace(new SKPoint(x, middle.Y)) is var (spot, after) ? Placed(page, spot, after) : null;
    }

    /// <summary>Scrolls the cursor into view where it is out of it.</summary>
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
        var (y1, y2) = ((row.Top + top.Y) * factor, (row.Top + bottom.Y) * factor);
        if (y1 < scroller.Offset.Y)
            scroller.Offset = new Vector(scroller.Offset.X, Math.Max(0, y1 - 24));
        else if (y2 > scroller.Offset.Y + scroller.Viewport.Height)
            scroller.Offset = new Vector(scroller.Offset.X, y2 - scroller.Viewport.Height + 24);
    }

    /// <summary>The cursor moved: it shows and blinks again from there, the input method learns where it stands, the session follows.</summary>
    void Moved()
    {
        Blink();
        ime.Moved();
        if (session == null || fromSession || caret is not var (page, at) || page >= scenes.Count || anchor is not var (anchorPage, anchorAt)
            || anchorPage >= scenes.Count || pictures.Spots(Page(page)) is not { Count: > 0 } spots || pictures.Spots(Page(anchorPage)) is not { Count: > 0 } anchorSpots)
            return;
        var (spot, after) = CaretSpot(spots, at);
        var (block, offset) = pictures.Places(Page(page)).PlaceAt(spot, after);
        var (from, fromAfter) = anchorAt >= anchorSpots.Count ? (anchorSpots[^1], true) : (anchorSpots[anchorAt], false);
        var (anchorBlock, anchorOffset) = pictures.Places(Page(anchorPage)).PlaceAt(from, fromAfter);
        session.Place(new TextPlace(anchorBlock, anchorOffset));
        session.Place(new TextPlace(block, offset), extend: true);
    }

    /// <summary>
    /// Makes an edit through the session: the pages after it drawn (those it left as they were from their recordings), the cursor and the
    /// selection where the session has them. What the document refuses changes nothing.
    /// </summary>
    public void Edit(Action change)
    {
        if (session is not { } editing)
            return;
        try
        {
            change();
        }
        catch (Exception e) when (e is NotSupportedException or ArgumentException)
        {
            return;
        }
        Update(editing.Document.Scenes());
        Show(editing.Caret, editing.Anchor);
        CheckEmpty();
        LayOutLater();
    }

    void CheckEmpty()
    {
        if (session is not { } editing)
            return;
        // One page with one place on it, the mark of the cursor's paragraph, which holds nothing, not even an object (one in its text).
        var empty = scenes.Count == 1 && pictures.Spots(Page(0)).Count <= 1 && editing.Document.Text(editing.Caret.Paragraph).Length == 0;
        if (empty != IsEmpty)
        {
            IsEmpty = empty;
            IsEmptyChanged?.Invoke();
        }
    }

    /// <summary>
    /// Lays out what the document left of its pages, slice by slice while the window waits: after input and rendering; in a browser, which has
    /// one thread, with the page's turn given back before each slice.
    /// </summary>
    void LayOutLater()
    {
        if (layingOut || session?.Document is not { LayingOut: true })
            return;
        layingOut = true;
        if (OperatingSystem.IsBrowser())
        {
            LayOutGivingWay();
            return;
        }
        Dispatcher.UIThread.Post(() =>
        {
            layingOut = false;
            if (session is { Document: { LayingOut: true } document } editing)
            {
                Slice(editing, document);
                LayOutLater();
            }
        }, DispatcherPriority.Background);
    }

    async void LayOutGivingWay()
    {
        try
        {
            while (true)
            {
                await Task.Delay(1);
                if (session is not { Document: { LayingOut: true } document } editing)
                    return;
                Slice(editing, document);
            }
        }
        finally
        {
            layingOut = false;
        }
    }

    /// <summary>A slice of the layout: the pages shown anew, the cursor placed anew where the slice changed its page.</summary>
    void Slice(EditSession editing, IEditableDocument document)
    {
        document.LayOut();
        var shown = caret is var (page, _) && page < scenes.Count ? scenes[page] : null;
        Update(document.Scenes());
        if (shown != null && (caret is not var (now, _) || now >= scenes.Count || !ReferenceEquals(scenes[now], shown)))
            Show(editing.Caret, editing.Anchor);
        CheckEmpty();
    }

    /// <summary>A page's scene drawn: the document draws its pages when they are wanted (see IEditableDocument.Scene).</summary>
    PageScene Page(int index) => scenes[index] is { Drawn: false } && session?.Document is { } document ? document.Scene(index) : scenes[index];

    /// <summary>The pages as the document reads now: recordings kept for the scenes that stayed, the rows laid out again where pages came, went or changed size.</summary>
    void Update(IReadOnlyList<PageScene> changed)
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
    void Show(TextPlace caretAt, TextPlace anchorAt)
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
    /// Where a page shows a place: the character at its offset on the page nearest to <paramref name="near"/> or on the page the document lays it
    /// out on (see IEditableDocument.PageOf) or those around them, else the character before it (a place within a field's result); null where
    /// none of these pages shows its paragraph. Only those pages are drawn to look at them.
    /// </summary>
    (int Page, int At, bool End)? Locate(TextPlace place, int near)
    {
        if (near >= 0 && near < scenes.Count && pictures.Places(Page(near)).Exact(place.Paragraph, place.Offset) is { } here)
            return Placed(near, here, false);
        var pages = new List<int>();
        void Look(int page)
        {
            if (page >= 0 && page < scenes.Count && !pages.Contains(page))
                pages.Add(page);
        }
        Look(near);
        if (session?.Document.PageOf(place.Paragraph, place.Offset) is { } laid)
        {
            Look(laid);
            Look(laid + 1);
            Look(laid - 1);
        }
        for (var step = 1; step <= 2; step++)
        {
            Look(near + step);
            Look(near - step);
        }
        foreach (var page in pages)
        {
            if (pictures.Places(Page(page)).Exact(place.Paragraph, place.Offset) is { } spot)
                return Placed(page, spot, false);
        }
        foreach (var page in pages)
        {
            if (pictures.Places(Page(page)).Find(place.Paragraph, place.Offset) is var (spot, after))
                return Placed(page, spot, after);
        }
        return null;
    }

    async void Paste(IClipboard clipboard)
    {
        if (await clipboard.TryGetTextAsync() is { Length: > 0 } text && session is { } editing)
            Edit(() =>
            {
                if (copied is { } fragment && fragment.Text == text)
                    editing.Paste(fragment);
                else
                    editing.Paste(text);
            });
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (session is not { } editing || caret == null || string.IsNullOrEmpty(e.Text) || e.Text.Any(char.IsControl))
            return;
        e.Handled = true;
        var text = e.Text;
        Edit(() => editing.Type(text));
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

    /// <summary>
    /// What the input method asks of the view: where the cursor stands (for its window of candidates), and the text being composed, which shows at
    /// the cursor until it is committed; the surrounding text it does not get yet.
    /// </summary>
    sealed class Ime(DocumentView view) : TextInputMethodClient
    {
        public override Visual TextViewVisual => view;

        public override bool SupportsPreedit => true;

        public override bool SupportsSurroundingText => false;

        public override string SurroundingText => "";

        public override Rect CursorRectangle => view.CaretRect();

        public override TextSelection Selection { get => default; set { } }

        public override void SetPreeditText(string? preeditText) => view.Preedit(preeditText);

        /// <summary>The cursor moved or the view scrolled: the input method moves its window.</summary>
        public void Moved() => RaiseCursorRectangleChanged();
    }

    /// <summary>The pages' recorded drawings and their places, made once and drawn on the render thread.</summary>
    sealed class Pictures
    {
        /// <summary>The recordings, as long as their scenes live: a document keeps only so many pages drawn (see IEditableDocument.Scene).</summary>
        readonly ConditionalWeakTable<PageScene, Recording> pages = new();

        /// <summary>A page recorded once: its drawing, its places, its characters in order (TextPlaces.Spots) and where each line's begin among them.</summary>
        sealed record Recording(SKPicture Picture, TextPlaces Places, List<TextPlaces.Spot> Spots, int[] LineStarts);

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

        public TextPlaces Places(PageScene scene) => Recorded(scene).Places;

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
                var lines = new List<(SKMatrix ToPage, PlacesItem Line)>();
                // Its pictures decoded once, at two pixels a CSS pixel, not again at every frame.
                SkiaScene.Draw(scene, recorder.BeginRecording(SKRect.Create((float)size.Width, (float)size.Height)), places: lines, pictureScale: 2);
                var places = new TextPlaces(lines);
                var starts = new int[lines.Count];
                for (var k = 1; k < lines.Count; k++)
                    starts[k] = starts[k - 1] + lines[k - 1].Line.Offsets.Length;
                var recording = new Recording(recorder.EndRecording(), places, places.Spots(), starts);
                pages.AddOrUpdate(scene, recording);
                return recording;
            }
        }
    }

    /// <summary>The rows in view drawn on Avalonia's Skia canvas.</summary>
    sealed class Drawing(Rect bounds, List<(Row Row, double Left)> rows, double factor, IReadOnlyList<PageScene> scenes,
        Pictures pictures, Dictionary<int, Mark> marks, SKColor selection, SKColor caret) : ICustomDrawOperation
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
            foreach (var (row, left) in rows)
            {
                canvas.Save();
                canvas.Translate((float)left, (float)(row.Top * factor));
                canvas.Scale((float)factor);
                canvas.ClipRect(SKRect.Create((float)row.Size.Width, (float)row.Size.Height));
                // The recording kept alive while it plays: an edit may forget it meanwhile, and its finalizer must not free it under the canvas.
                var page = pictures.Page(scenes[row.Index]);
                canvas.DrawPicture(page);
                GC.KeepAlive(page);
                if (marks.TryGetValue(row.Index, out var mark))
                {
                    using var selected = new SKPaint { Color = selection, IsAntialias = true };
                    foreach (var area in mark.Areas)
                    {
                        using var path = new SKPath();
                        path.AddPoly(area);
                        canvas.DrawPath(path, selected);
                    }
                    if (mark.Caret is var (top, bottom))
                    {
                        using var pen = new SKPaint { Color = caret, StrokeWidth = (float)Math.Max(1, 1.5 / factor), IsAntialias = true };
                        canvas.DrawLine(top, bottom, pen);
                    }
                }
                canvas.Restore();
            }
        }
    }
}
