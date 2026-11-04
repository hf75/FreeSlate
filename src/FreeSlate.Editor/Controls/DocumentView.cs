using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Input.TextInput;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using Avalonia.Platform.Storage;
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
public sealed partial class DocumentView : Control
{
    const double PxPerPt = 96.0 / 72;
    const double Edge = 16, Gap = 16;

    public static readonly StyledProperty<IBrush?> SheetProperty = AvaloniaProperty.Register<DocumentView, IBrush?>(nameof(Sheet));

    public static readonly StyledProperty<BoxShadows> SheetShadowProperty = AvaloniaProperty.Register<DocumentView, BoxShadows>(nameof(SheetShadow));

    public static readonly StyledProperty<IBrush?> CaretBrushProperty = AvaloniaProperty.Register<DocumentView, IBrush?>(nameof(CaretBrush));

    public static readonly StyledProperty<IBrush?> SelectionBrushProperty = AvaloniaProperty.Register<DocumentView, IBrush?>(nameof(SelectionBrush));

    public static readonly StyledProperty<bool> ShowsMeaningsProperty = AvaloniaProperty.Register<DocumentView, bool>(nameof(ShowsMeanings));

    public static readonly StyledProperty<IBrush?> MeaningBrushProperty = AvaloniaProperty.Register<DocumentView, IBrush?>(nameof(MeaningBrush));

    public static readonly StyledProperty<IBrush?> PointedBrushProperty = AvaloniaProperty.Register<DocumentView, IBrush?>(nameof(PointedBrush));

    public static readonly StyledProperty<IBrush?> OnPointedBrushProperty = AvaloniaProperty.Register<DocumentView, IBrush?>(nameof(OnPointedBrush));

    public static readonly StyledProperty<IBrush?> DrawingBrushProperty = AvaloniaProperty.Register<DocumentView, IBrush?>(nameof(DrawingBrush));

    static readonly Cursor Text = new(StandardCursorType.Ibeam);

    /// <summary>The kinds of file a picture comes in that the sheets take: Word keeps those up to TIFF as they are, the others go in as PNG.</summary>
    static readonly HashSet<string> PictureFiles = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".jfif", ".gif", ".bmp", ".tif", ".tiff", ".webp", ".ico" };

    /// <summary>How long the pages in view as they were take to fade into their new look (see Restyle), in milliseconds.</summary>
    const double FadeTime = 260;

    static DocumentView() => AffectsRender<DocumentView>(SheetProperty, SheetShadowProperty, CaretBrushProperty, SelectionBrushProperty, ShowsMeaningsProperty,
        MeaningBrushProperty, PointedBrushProperty, OnPointedBrushProperty, DrawingBrushProperty);

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

    /// <summary>
    /// The word or the paragraph a double or triple click selected, while the pointer held on from there selects by whole ones (see
    /// SelectUnitsAt); null otherwise.
    /// </summary>
    ((TextPlace Start, TextPlace End) Unit, bool Words)? units;

    /// <summary>
    /// Where across its page the cursor goes to as it goes up and down, kept from where it started as long as it goes on only so (Word's
    /// column, measured by COM: over a shorter line and back it stands where it started); null otherwise. Set as it goes so (across).
    /// </summary>
    float? goal;
    bool across;

    /// <summary>Where pictures dragged over the sheets would go while they are (a page and a place among its characters, as the cursor's).</summary>
    (int Page, int At, bool End)? dropAt;

    /// <summary>The input method's text being composed at the cursor, not yet in the document.</summary>
    string? preedit;

    /// <summary>The copy last made, with its formatting, for pasting it as it was while the clipboard still holds its text.</summary>
    Fragment? copied;

    /// <summary>The stretches with a meaning each page shows, found once for its scene (see MeaningMarks).</summary>
    readonly ConditionalWeakTable<PageScene, List<MeaningMark>> meaningMarks = new();

    /// <summary>The stretch with a meaning the pointer is over while meanings show.</summary>
    MeaningMark? pointed;

    /// <summary>
    /// The pages in view as they were before a change of the whole look, fading out over their new look (see Restyle): where they stood
    /// (their sheets in the view's coordinates at the factor then), the view's scroll offset then, and since when.
    /// </summary>
    (List<(Rect Sheet, SKPicture Picture, double Factor)> Pages, Vector Offset, long Since)? fading;

    readonly DispatcherTimer fader = new() { Interval = TimeSpan.FromMilliseconds(15) };

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
        fader.Tick += (_, _) =>
        {
            if (fading is { } fade && Environment.TickCount64 - fade.Since >= FadeTime)
            {
                fading = null;
                fader.Stop();
            }
            InvalidateVisual();
        };
        scrolling.Tick += (_, _) => Scroll();
        ime = new Ime(this);
        TextInputMethodClientRequested += (_, e) =>
        {
            if (session != null)
                e.Client = ime;
        };
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, DraggedOver);
        AddHandler(DragDrop.DragLeaveEvent, (_, _) => DropAt(null));
        AddHandler(DragDrop.DropEvent, Dropped);
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

    /// <summary>The frame of a drawing selected as a whole, and the bar where pictures dragged over the sheets would go.</summary>
    public IBrush? DrawingBrush
    {
        get => GetValue(DrawingBrushProperty);
        set => SetValue(DrawingBrushProperty, value);
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

    /// <summary>
    /// Whether the text shows what it means: each stretch with a meaning (see MeaningName) framed, its meaning's name beside its first line
    /// in the page's margin, and over its start while the pointer is on it. Nothing of the pages moves for it.
    /// </summary>
    public bool ShowsMeanings
    {
        get => GetValue(ShowsMeaningsProperty);
        set => SetValue(ShowsMeaningsProperty, value);
    }

    /// <summary>The frames and names of the meanings.</summary>
    public IBrush? MeaningBrush
    {
        get => GetValue(MeaningBrushProperty);
        set => SetValue(MeaningBrushProperty, value);
    }

    /// <summary>What the pointer is on, and what stands on that.</summary>
    public IBrush? PointedBrush
    {
        get => GetValue(PointedBrushProperty);
        set => SetValue(PointedBrushProperty, value);
    }

    public IBrush? OnPointedBrush
    {
        get => GetValue(OnPointedBrushProperty);
        set => SetValue(OnPointedBrushProperty, value);
    }

    /// <summary>Whether the selection does not show for a while (it stays): the text shows as it reads, a preview of a change to it (see GestureRing.Preview).</summary>
    public bool SelectionHidden
    {
        get => selectionHidden;
        set
        {
            if (selectionHidden == value)
                return;
            selectionHidden = value;
            InvalidateVisual();
        }
    }

    bool selectionHidden;

    /// <summary>The name of the meaning a character style stands for (see IEditableDocument.CharacterStyles), or null for one that stands for none.</summary>
    public Func<string, string?>? MeaningName { get; set; }

    /// <summary>The document changed, or may have: an edit made through the view, undoing or doing again.</summary>
    public event Action? Changed;

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

    /// <summary>The part of the view in sight, in its coordinates.</summary>
    Rect Viewed() => scroller != null ? new Rect(scroller.Offset.X, scroller.Offset.Y, scroller.Viewport.Width, scroller.Viewport.Height) : new Rect(Bounds.Size);

    Rect SheetRect(Row row) => new(Left(row), row.Top * factor, row.Size.Width * factor, row.Size.Height * factor);

    public override void Render(DrawingContext context)
    {
        // Clicks count everywhere, also beside the sheets.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        var view = Viewed();
        var visible = rows.Where(r => r.Top * factor < view.Bottom && (r.Top + r.Size.Height) * factor > view.Top).Select(r => (Row: r, Left: Left(r))).ToList();
        if (visible.Count == 0)
            return;
        foreach (var (row, _) in visible)
        {
            context.DrawRectangle(Sheet, null, new RoundedRect(SheetRect(row)), SheetShadow);
            // The document draws its pages when they are wanted, before the frame records them.
            Page(row.Index);
        }
        context.Custom(new Drawing(new Rect(Bounds.Size), visible, factor, scenes, pictures, Marks(visible), Skia(SelectionBrush), Skia(CaretBrush), Fading()));
        DrawGuides(context, visible);
        if (ShowsMeanings)
            DrawMeanings(context, visible);
        // A drawing selected as a whole: framed, a little outside its box, without the selection's tint and the cursor; a picture cropped by
        // hand whole round its frame.
        DrawDrawing(context);
        DrawCrop(context);
        DrawTilt(context);
        DrawLineDrag(context);
        // Where pictures dragged over the sheets would go.
        if (dropAt is var (dropPage, dropIndex, dropEnd) && BarAt(dropPage, dropIndex, dropEnd) is { Height: > 0 } bar)
            context.FillRectangle(DrawingBrush ?? CaretBrush ?? Brushes.Black, new Rect(bar.X - 1, bar.Y, 2, bar.Height));
        // The text being composed over the page at the cursor, underlined, as input methods show it.
        if (preedit != null && CaretRect() is { Height: > 0 } at)
        {
            var text = new FormattedText(preedit, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, Math.Max(8, at.Height * 0.72), CaretBrush);
            var composed = new Rect(at.X, at.Y, text.Width + 4, at.Height);
            context.FillRectangle(Sheet ?? Brushes.White, composed);
            context.DrawText(text, new Point(at.X + 2, at.Y + (at.Height - text.Height) / 2));
            context.DrawLine(new Pen(CaretBrush), new Point(composed.X + 2, composed.Bottom - 1.5), new Point(composed.Right - 2, composed.Bottom - 1.5));
        }
    }

    static SKColor Skia(IBrush? brush) => brush is ISolidColorBrush { Color: var c } ? new SKColor(c.R, c.G, c.B, c.A) : SKColors.Black;

    /// <summary>
    /// Makes an edit that changes how all of the document looks (its tone, see IEditableDocument.DefineStyles) through the session: the pages
    /// in view as they were fade out over their new look, where they stood in the view.
    /// </summary>
    public void Restyle(Action change)
    {
        var view = scroller != null ? new Rect(scroller.Offset.X, scroller.Offset.Y, scroller.Viewport.Width, scroller.Viewport.Height) : new Rect(Bounds.Size);
        var before = rows.Where(r => r.Top * factor < view.Bottom && (r.Top + r.Size.Height) * factor > view.Top)
            .Select(r => (SheetRect(r), pictures.Page(Page(r.Index)), factor)).ToList();
        Edit(change);
        fading = (before, scroller?.Offset ?? default, Environment.TickCount64);
        fader.Start();
    }

    /// <summary>The pages fading out (see Restyle) where they stand in the view now and how much of them shows, or null.</summary>
    (List<(SKRect Sheet, SKPicture Picture, float Factor)> Pages, float Opacity)? Fading()
    {
        if (fading is not var (pages, offset, since))
            return null;
        var t = Math.Clamp((Environment.TickCount64 - since) / FadeTime, 0, 1);
        // Out quickly at first, then gently.
        var opacity = (float)Math.Pow(1 - t, 2);
        var shift = (scroller?.Offset ?? default) - offset;
        return (pages.Select(p => (SKRect.Create((float)(p.Sheet.X + shift.X), (float)(p.Sheet.Y + shift.Y), (float)p.Sheet.Width, (float)p.Sheet.Height), p.Picture, (float)p.Factor)).ToList(),
            opacity);
    }

    /// <summary>A stretch of text with a meaning as its page shows it: its character style and its areas line by line (CSS pixels of the page).</summary>
    sealed record MeaningMark(string Style, List<SKRect> Areas);

    /// <summary>
    /// The stretches with a meaning a page shows (see IEditableDocument.CharacterStyles), in the order the page shows their starts. A
    /// change of a meaning changes the page's scene, which they are found for once.
    /// </summary>
    List<MeaningMark> MeaningMarks(int page)
    {
        var scene = Page(page);
        if (meaningMarks.TryGetValue(scene, out var known))
            return known;
        var marks = new List<MeaningMark>();
        if (session is { } editing && MeaningName is { } name)
        {
            var places = pictures.Places(scene);
            var styles = new Dictionary<int, IReadOnlyList<(int Offset, int Length, string Style)>>();
            var byStretch = new Dictionary<(int Block, int Offset), MeaningMark>();
            const float PxPerPoint = 96f / 72;
            foreach (var (toPage, line) in places.Lines)
            {
                if (line.Offsets.Length == 0)
                    continue;
                if (!styles.TryGetValue(line.Block, out var stretches))
                    styles[line.Block] = stretches = editing.Document.Contains(line.Block) ? editing.Document.CharacterStyles(line.Block) : [];
                if (stretches.Count == 0)
                    continue;
                // The characters of the line in a stretch with a meaning, one area for each run of them in one stretch.
                (int Offset, double Left, double Right)? run = null;
                void Close()
                {
                    if (run is not var (offset, left, right) || right <= left)
                        return;
                    var stretch = stretches.First(s => s.Offset == offset);
                    if (name(stretch.Style) == null)
                        return;
                    SKPoint[] corners = [toPage.MapPoint((float)(left * PxPerPoint), (float)(line.Top * PxPerPoint)), toPage.MapPoint((float)(right * PxPerPoint), (float)(line.Bottom * PxPerPoint))];
                    var area = new SKRect(corners.Min(c => c.X), corners.Min(c => c.Y), corners.Max(c => c.X), corners.Max(c => c.Y));
                    if (!byStretch.TryGetValue((line.Block, offset), out var mark))
                        marks.Add(byStretch[(line.Block, offset)] = mark = new MeaningMark(stretch.Style, []));
                    mark.Areas.Add(area);
                }
                for (var i = 0; i < line.Offsets.Length; i++)
                {
                    var at = line.Offsets[i];
                    var stretch = stretches.FirstOrDefault(s => at >= s.Offset && at < s.Offset + s.Length);
                    var (left, right) = (Math.Min(line.Starts[i], line.Ends[i]), Math.Max(line.Starts[i], line.Ends[i]));
                    if (stretch.Style != null && run is var (offset, runLeft, runRight) && offset == stretch.Offset)
                    {
                        run = (offset, Math.Min(runLeft, left), Math.Max(runRight, right));
                        continue;
                    }
                    Close();
                    run = stretch.Style != null ? (stretch.Offset, left, right) : null;
                }
                Close();
            }
        }
        meaningMarks.AddOrUpdate(scene, marks);
        return marks;
    }

    /// <summary>
    /// The meanings over the pages in view (see ShowsMeanings): each stretch framed, its meaning's name in the margin right of the page's
    /// text beside its first line, after those of the stretches before it there; the stretch the pointer is on, and its name, in the
    /// pointer's colour, its name also over its start.
    /// </summary>
    void DrawMeanings(DrawingContext context, List<(Row Row, double Left)> visible)
    {
        var typeface = new Typeface(new FontFamily("fonts:Inter#Inter"), FontStyle.Normal, FontWeight.SemiBold);
        var frame = new Pen(MeaningBrush, 1);
        var pointedFrame = new Pen(PointedBrush, 1.5);
        foreach (var (row, left) in visible)
        {
            var marks = MeaningMarks(row.Index);
            if (marks.Count == 0)
                continue;
            Rect ToView(SKRect area) => new(left + area.Left * factor, (row.Top + area.Top) * factor, area.Width * factor, area.Height * factor);
            // The margin begins after the right end of the page's text.
            var places = pictures.Places(Page(row.Index));
            var textRight = 0.0;
            foreach (var (toPage, line) in places.Lines)
            {
                if (line.Offsets.Length > 0)
                    textRight = Math.Max(textRight, toPage.MapPoint((float)(line.Ends.Max() * 96 / 72), 0).X);
            }
            // Where the next name beside a line goes: after the one before it, below it where the sheet ends first.
            var (margin, sheetRight) = (left + textRight * factor + 18, left + row.Size.Width * factor - 8);
            var nextFree = new Dictionary<int, Point>();
            foreach (var mark in marks)
            {
                var isPointed = ReferenceEquals(mark, pointed);
                foreach (var area in mark.Areas)
                    context.DrawRectangle(null, isPointed ? pointedFrame : frame, ToView(area).Inflate(new Thickness(1.5, 0.5)), 2.5, 2.5);
                var first = ToView(mark.Areas[0]);
                // Named now, in the language the interface has now.
                var name = MeaningName?.Invoke(mark.Style) ?? mark.Style;
                var label = new TextLayout(name.ToUpper(CultureInfo.CurrentCulture), typeface, 8.5, isPointed ? OnPointedBrush : MeaningBrush, letterSpacing: 0.7);
                var width = label.WidthIncludingTrailingWhitespace;
                var lineKey = (int)Math.Round(first.Top);
                var at = nextFree.TryGetValue(lineKey, out var free) ? free : new Point(margin, first.Center.Y - label.Height / 2);
                if (at.X > margin && at.X + width > sheetRight)
                    at = new Point(margin, at.Y + label.Height + 3);
                var (x, y) = (at.X, at.Y);
                if (isPointed)
                    context.DrawRectangle(PointedBrush, null, new Rect(x - 4, y - 1.5, width + 8, label.Height + 3), 3, 3);
                label.Draw(context, new Point(x, y));
                nextFree[lineKey] = new Point(x + width + 14, y);
                // Over the start of the stretch the pointer is on as well, where the eye is.
                if (isPointed)
                {
                    var over = new Point(first.Left, first.Top - label.Height - 4);
                    context.DrawRectangle(PointedBrush, null, new Rect(over.X - 4, over.Y - 1.5, width + 8, label.Height + 3), 3, 3);
                    label.Draw(context, over);
                }
            }
        }
    }

    /// <summary>
    /// The paragraph beside a point of the view: of the line at its height on the page it is over, or of the nearest line there if that
    /// is near; with that paragraph's first line on the page and the left edge of the page's text, in the view's coordinates. Null away
    /// from text.
    /// </summary>
    public (int Paragraph, Rect Line, double TextLeft)? ParagraphBeside(Point point)
    {
        foreach (var row in rows)
        {
            if (point.Y < row.Top * factor || point.Y > (row.Top + row.Size.Height) * factor)
                continue;
            var places = pictures.Places(Page(row.Index));
            var y = point.Y / factor - row.Top;
            var nearest = places.Lines.Where(l => l.Line.Offsets.Length > 0).Select(l =>
            {
                var (toPage, line) = l;
                var (top, bottom) = (toPage.MapPoint(0, (float)(line.Top * PxPerPt)).Y, toPage.MapPoint(0, (float)(line.Bottom * PxPerPt)).Y);
                return (line.Block, Away: y < Math.Min(top, bottom) ? Math.Min(top, bottom) - y : y > Math.Max(top, bottom) ? y - Math.Max(top, bottom) : 0);
            }).OrderBy(l => l.Away);
            // The nearest line of the text's own, not of a drawing's text box (a caption is its picture's, of no level).
            foreach (var (best, distance) in nearest)
            {
                if (session?.Document.DrawingAround(best) != null)
                    continue;
                return distance <= 18 && FirstLineOn(row, best) is var (rect, left) ? (best, rect, left) : null;
            }
            return null;
        }
        return null;
    }

    /// <summary>The first line of a paragraph on the first page in view that shows it, and the left edge of that page's text, in the view's coordinates.</summary>
    public (Rect Line, double TextLeft)? FirstLine(int paragraph)
    {
        var view = scroller != null ? new Rect(scroller.Offset.X, scroller.Offset.Y, scroller.Viewport.Width, scroller.Viewport.Height) : new Rect(Bounds.Size);
        foreach (var row in rows.Where(r => r.Top * factor < view.Bottom && (r.Top + r.Size.Height) * factor > view.Top))
        {
            if (FirstLineOn(row, paragraph) is { } found)
                return found;
        }
        return null;
    }

    (Rect Line, double TextLeft)? FirstLineOn(Row row, int paragraph)
    {
        var places = pictures.Places(Page(row.Index));
        SKRect? first = null;
        var textLeft = double.MaxValue;
        foreach (var (toPage, line) in places.Lines)
        {
            if (line.Offsets.Length == 0)
                continue;
            SKPoint[] corners = [toPage.MapPoint((float)(line.Starts.Min() * PxPerPt), (float)(line.Top * PxPerPt)), toPage.MapPoint((float)(line.Ends.Max() * PxPerPt), (float)(line.Bottom * PxPerPt))];
            var area = new SKRect(corners.Min(c => c.X), corners.Min(c => c.Y), corners.Max(c => c.X), corners.Max(c => c.Y));
            textLeft = Math.Min(textLeft, area.Left);
            if (line.Block == paragraph && (first == null || area.Top < first.Value.Top))
                first = area;
        }
        if (first is not { } found)
            return null;
        var left = Left(row);
        return (new Rect(left + found.Left * factor, (row.Top + found.Top) * factor, found.Width * factor, found.Height * factor), left + textLeft * factor);
    }

    /// <summary>The stretch with a meaning at a point of the view, while meanings show.</summary>
    MeaningMark? MeaningAt(Point point)
    {
        if (!ShowsMeanings)
            return null;
        foreach (var row in rows)
        {
            if (point.Y < row.Top * factor || point.Y > (row.Top + row.Size.Height) * factor)
                continue;
            var local = new SKPoint((float)((point.X - Left(row)) / factor), (float)(point.Y / factor - row.Top));
            return MeaningMarks(row.Index).FirstOrDefault(m => m.Areas.Any(a => SKRect.Inflate(a, 1.5f, 0.5f).Contains(local)));
        }
        return null;
    }

    /// <summary>The character the cursor stands at on its page, before it, or after it at the end of its line.</summary>
    (TextPlaces.Spot Spot, bool After) CaretSpot(List<TextPlaces.Spot> spots, int at) => SpotAt(spots, at, lineEnd);

    /// <summary>The character a place stands at on its page, before it, or after it at the end of its line (<paramref name="end"/>, see lineEnd).</summary>
    static (TextPlaces.Spot Spot, bool After) SpotAt(List<TextPlaces.Spot> spots, int at, bool end) =>
        at >= spots.Count || at > 0 && end && spots[at - 1].Line != spots[at].Line ? (spots[Math.Min(at, spots.Count) - 1], true) : (spots[at], false);

    /// <summary>What a page shows over its text: the areas of the selection and the cursor (from its top to its bottom), in CSS pixels of the page.</summary>
    sealed record Mark(List<SKPoint[]> Areas, (SKPoint Top, SKPoint Bottom)? Caret);

    /// <summary>The selection and the cursor on the rows in view, by page.</summary>
    Dictionary<int, Mark> Marks(List<(Row Row, double Left)> visible)
    {
        var marks = new Dictionary<int, Mark>();
        if (caret == null)
            return marks;
        var inView = visible.Select(v => v.Row.Index).ToHashSet();
        // A drawing selected as a whole has its frame instead (see Render).
        var drawing = SelectedDrawing() != null;
        foreach (var (page, from, count) in SelectionHidden || drawing ? [] : Selected())
        {
            if (!inView.Contains(page))
                continue;
            var spots = pictures.Spots(Page(page));
            marks[page] = new Mark(pictures.Places(Page(page)).Selection(spots[from], spots[from + count - 1]), null);
        }
        if (!drawing && caret is var (cursorPage, at) && blink && preedit == null && inView.Contains(cursorPage) && pictures.Spots(Page(cursorPage)) is { Count: > 0 } all)
        {
            var (spot, after) = CaretSpot(all, at);
            var bar = pictures.Places(Page(cursorPage)).CaretAt(spot, after);
            marks[cursorPage] = new Mark(marks.TryGetValue(cursorPage, out var selected) ? selected.Areas : [], bar);
        }
        return marks;
    }

    /// <summary>The cursor's bar in the view's coordinates (its top to its bottom, a pixel wide), or an empty rectangle.</summary>
    Rect CaretRect() => caret is var (page, at) ? BarAt(page, at, lineEnd) : default;

    /// <summary>The bar of a place as the cursor's would be there (see CaretRect, SpotAt), or an empty rectangle.</summary>
    Rect BarAt(int page, int at, bool end)
    {
        if (page >= rows.Count || pictures.Spots(Page(page)) is not { Count: > 0 } spots)
            return default;
        var row = rows[page];
        var (spot, after) = SpotAt(spots, at, end);
        var (top, bottom) = pictures.Places(Page(page)).CaretAt(spot, after);
        var (y1, y2) = ((row.Top + top.Y) * factor, (row.Top + bottom.Y) * factor);
        return new Rect(Left(row) + top.X * factor, Math.Min(y1, y2), 1, Math.Abs(y2 - y1));
    }

    /// <summary>The drawing the selection holds alone, selected as a whole: its page and its place among the page's characters; else null.</summary>
    (int Page, int At)? SelectedDrawing() =>
        Selected() is [(var page, var from, 1)] && pictures.Places(Page(page)).Box(pictures.Spots(Page(page))[from]) != null ? (page, from) : null;

    /// <summary>The box of a drawing among a page's characters (see TextPlaces.Box) in the view's coordinates, or null where the character is none.</summary>
    Rect? BoxOf(int page, int at)
    {
        if (page >= rows.Count || pictures.Places(Page(page)).Box(pictures.Spots(Page(page))[at]) is not { } corners)
            return null;
        var row = rows[page];
        var (left, top) = (corners.Min(c => c.X), corners.Min(c => c.Y));
        return new Rect(Left(row) + left * factor, (row.Top + top) * factor, (corners.Max(c => c.X) - left) * factor, (corners.Max(c => c.Y) - top) * factor);
    }

    /// <summary>
    /// The drawing a point of the view is on: its page and the place of its character among the page's (a floating one's anchor). One in
    /// front of the text first, then one in the line, then one behind the text where no character is over the point; a drawing in
    /// another's text box counts as that one (a picture as its caption's figure), and the text a drawing's box holds is text where the
    /// point is in one of its lines (see TextHolder). A drawing turned or tilted is on the point where it shows (TextPlaces.OnDrawing).
    /// </summary>
    (int Page, int At)? DrawingAt(Point point)
    {
        foreach (var row in rows)
        {
            if (point.Y < row.Top * factor || point.Y > (row.Top + row.Size.Height) * factor)
                continue;
            var scene = Page(row.Index);
            var places = pictures.Places(scene);
            var spots = pictures.Spots(scene);
            var local = new SKPoint((float)((point.X - Left(row)) / factor), (float)(point.Y / factor - row.Top));
            if (TextHolder(places, local) != null)
                return null;
            var found = places.FloatingAt(local, behind: false);
            for (var i = 0; i < spots.Count && found == null; i++)
            {
                if (places.InlineBox(spots[i]) != null && places.OnDrawing(spots[i], local))
                    found = places.PlaceAt(spots[i], after: false);
            }
            if (found == null && !places.Over(local))
                found = places.FloatingAt(local, behind: true);
            if (found is not var (block, offset))
                return null;
            if (session?.Document.DrawingAround(block) is var (outer, at))
                (block, offset) = (outer, at);
            return places.Exact(block, offset) is { } spot ? (row.Index, pictures.Index(scene, spot)) : null;
        }
        return null;
    }

    /// <summary>
    /// The drawing whose box holds the text a point of a page is in (a picture's caption): the point in one of that text's lines with no
    /// drawing in it, within the drawing's box; the place of the drawing's character, or null where the point is in no such text.
    /// </summary>
    (int Paragraph, int Offset)? TextHolder(TextPlaces places, SKPoint local)
    {
        if (session is not { } editing)
            return null;
        for (var k = 0; k < places.Lines.Count; k++)
        {
            var line = places.Lines[k].Line;
            if (line.Offsets.Length == 0 || line.Boxes.Count > 0)
                continue;
            var (top, bottom) = places.CaretAt(new TextPlaces.Spot(k, 0), after: false);
            if (local.Y < Math.Min(top.Y, bottom.Y) || local.Y > Math.Max(top.Y, bottom.Y) || editing.Document.DrawingAround(line.Block) is not var (block, offset)
                || places.Exact(block, offset) is not { } own || places.Box(own) is not { } box || local.X < box.Min(c => c.X) || local.X > box.Max(c => c.X))
                continue;
            return (block, offset);
        }
        return null;
    }

    /// <summary>Whether a point of the view is on a drawing (see DrawingAt).</summary>
    public bool OnDrawing(Point point) => DrawingAt(point) != null;

    /// <summary>Whether a point of the view is on the drawing among a page's characters: where it shows turned or tilted, else on its box (see TextPlaces.OnDrawing).</summary>
    bool Hits(int page, int at, Point point)
    {
        if (page >= rows.Count)
            return false;
        var row = rows[page];
        var local = new SKPoint((float)((point.X - Left(row)) / factor), (float)(point.Y / factor - row.Top));
        return pictures.Places(Page(page)).OnDrawing(pictures.Spots(Page(page))[at], local);
    }

    /// <summary>Selects the drawing a point of the view is on as a whole (see DrawingAt); false where it is on none.</summary>
    public bool SelectDrawingAt(Point point)
    {
        Leave(point);
        if (DrawingAt(point) is not var (page, at))
            return false;
        (anchor, caret, lineEnd) = ((page, at), (page, at + 1), false);
        Moved();
        InvalidateVisual();
        return true;
    }

    /// <summary>Whether the selection is a drawing selected as a whole.</summary>
    public bool DrawingSelected => SelectedDrawing() != null;

    void Preedit(string? text)
    {
        preedit = string.IsNullOrEmpty(text) ? null : text;
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (dragged != null)
        {
            DragTo(e.GetPosition(this), e.KeyModifiers);
            Hold(e.GetPosition(this), e.KeyModifiers);
            e.Handled = true;
            return;
        }
        if (cropDrag != null)
        {
            CropTo(e.GetPosition(this), e.KeyModifiers);
            e.Handled = true;
            return;
        }
        if (tiltDrag != null)
        {
            TiltTo(e.GetPosition(this));
            e.Handled = true;
            return;
        }
        if (lineDrag != null)
        {
            LineDragTo(e.GetPosition(this), e.KeyModifiers);
            e.Handled = true;
            return;
        }
        // Over a drawing the pointer shows that it can be moved, over a handle of the one selected that it can be sized; while cropping, the
        // marks and the picture; while tilting, the picture.
        if (cropping)
            Cursor = CropCursorAt(e.GetPosition(this));
        else if (tilting)
            Cursor = TiltsAt(e.GetPosition(this)) ? Grab : Text;
        else if (!selecting)
            Cursor = CursorAt(e.GetPosition(this));
        if (selecting)
        {
            SelectTo(e.GetPosition(this));
            Hold(e.GetPosition(this), e.KeyModifiers);
        }
        else if (!selecting && MeaningAt(e.GetPosition(this)) is var mark && !ReferenceEquals(mark, pointed))
        {
            pointed = mark;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (pointed != null)
        {
            pointed = null;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// A click puts the cursor at the place nearest to it, with Shift selects up to there; dragging selects. A double click selects the
    /// word there, a triple click the paragraph, as Word does (see SelectUnitsAt); a double click on a picture crops it by hand (see
    /// BeginCrop); while cropping, a press on its marks or on it crops it, while tilting one on it tilts it (see BeginTilt), one
    /// elsewhere ends that first.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        var point = e.GetPosition(this);
        if (cropping)
        {
            if (CropPress(point, e.Pointer))
            {
                e.Handled = true;
                return;
            }
            EndCrop();
        }
        else if (tilting)
        {
            if (TiltPress(point, e.Pointer))
            {
                e.Handled = true;
                return;
            }
            EndTilt();
        }
        else if (e.ClickCount == 2 && (e.KeyModifiers & KeyModifiers.Shift) == 0 && SelectDrawingAt(point) && BeginCrop())
        {
            Focus();
            e.Handled = true;
            return;
        }
        if (e.ClickCount >= 2 && (e.KeyModifiers & KeyModifiers.Shift) == 0 && HandleAt(point) == null && !OnDrawing(point) && TableLineAt(point) == null
            && SelectUnitsAt(point, words: e.ClickCount == 2, e.Pointer))
        {
            e.Handled = true;
            return;
        }
        if (Press(point, e.Pointer, e.KeyModifiers))
            e.Handled = true;
    }

    /// <summary>
    /// A double click selects Word's word the character at a point stands in (see EditSession.WordAround), a triple click its paragraph
    /// with its mark (ParagraphAround); the pointer held on from there selects on by whole words or paragraphs (see SelectTo). Whether
    /// there was text there to select.
    /// </summary>
    bool SelectUnitsAt(Point point, bool words, IPointer pointer)
    {
        if (session is not { } editing || CharacterAt(point) is not { } character
            || (words ? editing.WordAround(character) : editing.ParagraphAround(character)) is not { } unit)
            return false;
        Focus();
        editing.Place(unit.Start);
        editing.Place(unit.End, extend: true);
        Show(editing.Caret, editing.Anchor);
        (units, selecting) = ((unit, words), true);
        pointer.Capture(this);
        return true;
    }

    /// <summary>The selection dragged to a point of the view: the cursor at the place there, by whole words or paragraphs after a double or triple click.</summary>
    void SelectTo(Point point)
    {
        if (units is var (held, words) && session is { } editing)
        {
            if (CharacterAt(point) is { } character && (words ? editing.WordAround(character) : editing.ParagraphAround(character)) is { } unit)
            {
                editing.SelectOver(held, unit);
                Show(editing.Caret, editing.Anchor, reveal: false);
            }
        }
        else if (PlaceAt(point) is var (page, at, end))
        {
            (caret, lineEnd) = ((page, at), end);
            Moved();
        }
    }

    /// <summary>
    /// A press of the main button at a point of the view, as a click takes it: the cursor at the place nearest to it, with Shift the selection
    /// up to there; <paramref name="dragging"/>, the pointer goes on selecting until it is let go. Whether there was a place.
    /// </summary>
    public bool Press(Point point, IPointer pointer, KeyModifiers modifiers, bool dragging = true)
    {
        Focus();
        // A handle of the drawing selected sizes it; a drawing pressed is selected as a whole, and may be dragged from there.
        if ((modifiers & KeyModifiers.Shift) == 0 && HandleAt(point) is { } handle)
        {
            if (dragging)
                BeginDrag(point, pointer, handle);
            return true;
        }
        if ((modifiers & KeyModifiers.Shift) == 0 && SelectDrawingAt(point))
        {
            if (dragging)
                BeginDrag(point, pointer);
            return true;
        }
        // A line between cells of a table is dragged (see BeginLineDrag).
        if ((modifiers & KeyModifiers.Shift) == 0 && dragging && BeginLineDrag(point, pointer))
            return true;
        if (PlaceAt(point) is not var (page, at, end))
            return false;
        (caret, lineEnd) = ((page, at), end);
        if ((modifiers & KeyModifiers.Shift) == 0 || anchor == null)
            anchor = (page, at);
        if (dragging)
        {
            selecting = true;
            pointer.Capture(this);
        }
        Moved();
        return true;
    }

    /// <summary>
    /// Whether a point of the view is on the selection (a drawing selected as a whole: on it where it shows, see Hits, not on a handle); a
    /// picture being cropped or tilted is none, a press on it crops or tilts it.
    /// </summary>
    public bool OnSelection(Point point)
    {
        if (cropping || tilting || TableLineAt(point) != null)
            return false;
        if (SelectedDrawing() is var (drawnPage, drawnAt))
            return Hits(drawnPage, drawnAt, point) && HandleAt(point) == null;
        foreach (var (page, from, count) in Selected())
        {
            if (page >= rows.Count)
                continue;
            var row = rows[page];
            var local = new SKPoint((float)((point.X - Left(row)) / factor), (float)(point.Y / factor - row.Top));
            var spots = pictures.Spots(Page(page));
            if (pictures.Places(Page(page)).Selection(spots[from], spots[from + count - 1]).Any(area =>
                    local.X >= area.Min(c => c.X) && local.X <= area.Max(c => c.X) && local.Y >= area.Min(c => c.Y) && local.Y <= area.Max(c => c.Y)))
                return true;
        }
        return false;
    }

    /// <summary>
    /// The parts of the selection on the sheets in view, one for each line (or run of a line) in the order of the text, as rectangles of
    /// the view's coordinates; a drawing selected as a whole its box, a picture turned what its frame takes turned; none without a
    /// selection.
    /// </summary>
    public List<Rect> SelectionAreas()
    {
        if (SelectionTurned() is var (frame, turn))
            return [new Rect(Turned(frame.TopLeft, frame.Center, turn), Turned(frame.BottomRight, frame.Center, turn))
                .Union(new Rect(Turned(frame.TopRight, frame.Center, turn), Turned(frame.BottomLeft, frame.Center, turn)))];
        if (SelectedDrawing() is var (drawnPage, drawnAt))
            return BoxOf(drawnPage, drawnAt) is { } box ? [box] : [];
        var parts = new List<Rect>();
        var view = Viewed();
        foreach (var (page, from, count) in Selected())
        {
            if (page >= rows.Count || rows[page] is var row && (row.Top * factor >= view.Bottom || (row.Top + row.Size.Height) * factor <= view.Top))
                continue;
            var left = Left(row);
            var spots = pictures.Spots(Page(page));
            foreach (var area in pictures.Places(Page(page)).Selection(spots[from], spots[from + count - 1]))
                parts.Add(new Rect(left + area.Min(c => c.X) * factor, (row.Top + area.Min(c => c.Y)) * factor,
                    (area.Max(c => c.X) - area.Min(c => c.X)) * factor, (area.Max(c => c.Y) - area.Min(c => c.Y)) * factor));
        }
        return parts;
    }

    /// <summary>Selects the word the character at a point of the view belongs to; false where it is no word's.</summary>
    public bool SelectWordAt(Point point)
    {
        Leave(point);
        if (session is not { } editing)
            return false;
        foreach (var row in rows)
        {
            if (point.Y < row.Top * factor || point.Y > (row.Top + row.Size.Height) * factor)
                continue;
            var places = pictures.Places(Page(row.Index));
            var local = new SKPoint((float)((point.X - Left(row)) / factor), (float)(point.Y / factor - row.Top));
            if (places.HitSpot(local) is not { } spot)
                return false;
            // Only the character the point is over, not the nearest one beside the text.
            var (toPage, line) = places.Lines[spot.Line];
            var (left, right) = (Math.Min(line.Starts[spot.Index], line.Ends[spot.Index]), Math.Max(line.Starts[spot.Index], line.Ends[spot.Index]));
            var (a, b) = (toPage.MapPoint((float)(left * PxPerPt), (float)(line.Top * PxPerPt)), toPage.MapPoint((float)(right * PxPerPt), (float)(line.Bottom * PxPerPt)));
            if (local.X < Math.Min(a.X, b.X) - 2 || local.X > Math.Max(a.X, b.X) + 2 || local.Y < Math.Min(a.Y, b.Y) - 2 || local.Y > Math.Max(a.Y, b.Y) + 2)
                return false;
            var (block, offset) = places.PlaceAt(spot, false);
            if (!editing.Document.Contains(block)
                || (editing.Document.WordAt(block, offset + 1) ?? editing.Document.WordAt(block, offset)) is not var (start, length))
                return false;
            editing.Place(new TextPlace(block, start));
            editing.Place(new TextPlace(block, start + length), extend: true);
            Show(editing.Caret, editing.Anchor);
            return true;
        }
        return false;
    }

    /// <summary>The cursor's bar in the view's coordinates, an empty rectangle where it shows nowhere.</summary>
    public Rect CaretBounds => CaretRect();

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        LetGo();
        if (dragged != null)
        {
            EndDrag(keep: true);
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }
        if (cropDrag != null)
        {
            EndCropDrag(keep: true);
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }
        if (tiltDrag != null)
        {
            EndTiltDrag(keep: true);
            e.Handled = true;
            return;
        }
        if (lineDrag != null)
        {
            EndLineDrag(keep: true);
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }
        if (selecting)
        {
            (selecting, units) = (false, null);
            e.Pointer.Capture(null);
        }
    }

    /// <summary>
    /// The place a point of the view hits, on the page nearest to it: the character it is over or the nearest one, before it or after it
    /// (see TextPlaces.HitPlace), and whether that is after a line's last character; null without text.
    /// </summary>
    (int Page, int At, bool End)? PlaceAt(Point point) => Hit(point) is var (page, spot, after) ? Placed(page, spot, after) : null;

    /// <summary>The place before the character a point of the view hits, the one it is over or the nearest one (see PlaceAt); null without text.</summary>
    TextPlace? CharacterAt(Point point) =>
        Hit(point) is var (page, spot, _) && pictures.Places(Page(page)).PlaceAt(spot, false) is var (block, offset) ? new TextPlace(block, offset) : null;

    /// <summary>The character a point of the view hits on the page nearest to it, and whether the point is after it (see TextPlaces.HitPlace).</summary>
    (int Page, TextPlaces.Spot Spot, bool After)? Hit(Point point)
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
        var places = pictures.Places(Page(best.Index));
        // In the text a drawing's box holds (a caption), a place of that text, not of the line the drawing stands in; in a table's cell, of
        // that cell's text (an empty part of the cell is nearer to other cells' lines).
        Func<PlacesItem, bool>? among = TextHolder(places, local) is { } holder && session is { } editing
            ? line => editing.Document.DrawingAround(line.Block) == holder
            : CellAt(best.Index, local) is { } cell ? line => cell.Paragraphs.Contains(line.Block) : null;
        return places.HitPlace(local, among) is var (spot, after) ? (best.Index, spot, after) : null;
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
        // Escape while a drawing is dragged: it goes back; so a line between cells of a table.
        if (dragged is { } d && e.Key == Key.Escape)
        {
            EndDrag(keep: false);
            d.Pointer.Capture(null);
            e.Handled = true;
            return;
        }
        if (lineDrag is { } line && e.Key == Key.Escape)
        {
            EndLineDrag(keep: false);
            line.Pointer.Capture(null);
            e.Handled = true;
            return;
        }
        // While cropping: Escape takes a mark dragged back, else Enter and Escape end cropping; another key ends it and goes on.
        if (cropping)
        {
            if (cropDrag is { } crop && e.Key == Key.Escape)
            {
                EndCropDrag(keep: false);
                crop.Pointer.Capture(null);
                e.Handled = true;
                return;
            }
            EndCrop();
            if (e.Key is Key.Enter or Key.Escape)
            {
                e.Handled = true;
                return;
            }
        }
        // While tilting, the same: Escape takes a drag back, else Enter and Escape end tilting; another key ends it and goes on.
        if (tilting)
        {
            if (tiltDrag != null && e.Key == Key.Escape)
            {
                EndTiltDrag(keep: false);
                e.Handled = true;
                return;
            }
            EndTilt();
            if (e.Key is Key.Enter or Key.Escape)
            {
                e.Handled = true;
                return;
            }
        }
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
        // Ctrl with the arrows, Home and End: by words and paragraphs, to the story's start and end, as Word goes (see EditSession.Move);
        // the paragraph of a picture with its caption is no place for the cursor, the picture is selected instead.
        if (control && (e.KeyModifiers & KeyModifiers.Alt) == 0 && e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End)
        {
            e.Handled = true;
            editing.Move(e.Key switch
            {
                Key.Left => EditSession.Jump.WordLeft,
                Key.Right => EditSession.Jump.WordRight,
                Key.Up => EditSession.Jump.ParagraphUp,
                Key.Down => EditSession.Jump.ParagraphDown,
                Key.Home => EditSession.Jump.StoryStart,
                _ => EditSession.Jump.StoryEnd,
            }, extend: shift);
            if (!shift && editing.CaptionedPicture(editing.Caret.Paragraph) is var (paragraph, offset))
            {
                editing.Place(new TextPlace(paragraph, offset));
                editing.Place(new TextPlace(paragraph, offset + 1), extend: true);
            }
            Show(editing.Caret, editing.Anchor);
            return;
        }
        // A floating drawing selected: the arrows move it (see Nudge).
        if (!control && (e.KeyModifiers & KeyModifiers.Alt) == 0 && e.Key is Key.Left or Key.Right or Key.Up or Key.Down && Nudge(e.Key, shift))
        {
            e.Handled = true;
            return;
        }
        // A picture with its caption selected in the line: down goes into the caption.
        if (e.Key == Key.Down && !shift && !control && SelectedDrawing() != null && editing.Caption is { } caption)
        {
            e.Handled = true;
            editing.Place(new TextPlace(caption, 0));
            Show(editing.Caret, editing.Anchor);
            return;
        }
        // A selection, and an arrow, Home or End without Shift: Left and Right put the cursor at its start or its end, Up and Home go
        // on from its start, Down and End from its end (measured in Word 365 by COM, WordBasic.CharLeft and its kin).
        if (!shift && editing.Selecting && e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End)
        {
            var (start, end) = editing.Selection;
            var from = e.Key is Key.Left or Key.Up or Key.Home ? start : end;
            if (e.Key is Key.Left or Key.Right)
            {
                e.Handled = true;
                editing.Place(from);
                Show(editing.Caret, editing.Anchor);
                return;
            }
            if (Locate(from, page) is var (fromPage, fromAt, fromEnd))
                (page, at, lineEnd, goal) = (fromPage, fromAt, fromEnd, null);
        }
        var spots = pictures.Spots(Page(page));
        // The arrows keep to the text the cursor is in, the page's own or a drawing's (see StoryAt). Up and down keep to the column they
        // started from (see goal); with Shift, in the first line and the last they go to its start or end (measured by COM).
        var story = spots.Count > 0 ? StoryAt(page, CaretSpot(spots, at).Spot) : null;
        (int Page, int At, bool End)? moved = e.Key switch
        {
            Key.Left or Key.Right => Step(page, at, e.Key == Key.Right, story),
            Key.Home or Key.End when spots.Count > 0 => LineEdge(page, at, e.Key == Key.End),
            Key.Up or Key.Down when spots.Count > 0 => (story == null ? CellAcross(page, at, e.Key == Key.Down) : null) ?? LineAcross(page, at, e.Key == Key.Down, story)
                ?? (shift ? LineEdge(page, at, e.Key == Key.Down) : null),
            _ => null,
        };
        if (moved is not var (movedPage, movedAt, movedEnd))
            return;
        e.Handled = true;
        across = e.Key is Key.Up or Key.Down;
        try
        {
            // The paragraph of a picture with its caption is no place for the cursor: the picture is selected instead.
            if (!shift && CaptionedPictureAt(movedPage, movedAt) is { } picture)
            {
                (anchor, caret, lineEnd) = ((movedPage, picture), (movedPage, picture + 1), false);
                Moved();
                Reveal((movedPage, picture));
                return;
            }
            (caret, lineEnd) = ((movedPage, movedAt), movedEnd);
            if (!shift)
                anchor = (movedPage, movedAt);
            Moved();
            Reveal((movedPage, movedAt));
        }
        finally
        {
            across = false;
        }
    }

    /// <summary>
    /// A place of a page in the paragraph of a picture with its caption, no place for the cursor (see EditSession.CaptionedPicture): the
    /// picture's character among the page's, to select it instead; null for other places.
    /// </summary>
    int? CaptionedPictureAt(int page, int at)
    {
        if (session is not { } editing || page >= scenes.Count || pictures.Spots(Page(page)) is not { Count: > 0 } spots)
            return null;
        var places = pictures.Places(Page(page));
        var (block, _) = places.PlaceAt(CaretSpot(spots, at).Spot, after: false);
        return editing.CaptionedPicture(block) is var (paragraph, offset) && places.Exact(paragraph, offset) is { } spot ? pictures.Index(Page(page), spot) : null;
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

    /// <summary>The drawing whose text a character of a page is (a caption, see IEditableDocument.DrawingAround); null for the page's own text.</summary>
    (int Paragraph, int Offset)? StoryAt(int page, TextPlaces.Spot spot) =>
        session?.Document.DrawingAround(pictures.Places(Page(page)).Lines[spot.Line].Line.Block);

    /// <summary>
    /// The place of the character left or right of the cursor, on the next page over its edge, in the text the cursor is in (see StoryAt):
    /// in the page's own the characters of drawings' texts passed over; out of a drawing's text by its start before the drawing, by its
    /// end after it (a caption's start reaches its picture's paragraph first, see CaptionedPictureAt).
    /// </summary>
    (int Page, int At, bool End)? Step(int page, int at, bool right, (int Paragraph, int Offset)? story)
    {
        var spots = pictures.Spots(Page(page));
        var by = right ? 1 : -1;
        var next = at + by;
        if (story is var (paragraph, offset))
        {
            if (next < 0 || next >= spots.Count || StoryAt(page, spots[next]) != story)
                return pictures.Places(Page(page)).Exact(paragraph, offset) is { } own ? (page, pictures.Index(Page(page), own) + (right ? 1 : 0), false) : null;
            return (page, next, false);
        }
        var passed = false;
        while (next >= 0 && next < spots.Count && StoryAt(page, spots[next]) != null)
            (next, passed) = (next + by, true);
        // Over the page's edge: after the last character of the page's own text on the page before.
        if (next < 0)
        {
            if (page == 0)
                return null;
            var before = pictures.Spots(Page(page - 1));
            var last = before.FindLastIndex(s => StoryAt(page - 1, s) == null);
            return (page - 1, last + 1, true);
        }
        if (next > spots.Count || passed && next == spots.Count)
            return page + 1 < scenes.Count ? (page + 1, 0, false) : null;
        return (page, next, false);
    }

    /// <summary>
    /// The place in the line above or below the cursor's that stands nearest to the column it goes up and down to (see goal), on the next
    /// page over its edge, of the text the cursor is in (see StoryAt): out of a drawing's text on from the line the drawing stands in.
    /// </summary>
    (int Page, int At, bool End)? LineAcross(int page, int at, bool down, (int Paragraph, int Offset)? story)
    {
        var places = pictures.Places(Page(page));
        var spots = pictures.Spots(Page(page));
        var (current, currentAfter) = CaretSpot(spots, at);
        var x = goal ??= places.CaretAt(current, currentAfter).Top.X;
        bool Ours(PlacesItem line) => session?.Document.DrawingAround(line.Block) == story;
        // The nearest line beside this one of the same text, in the page's order of lines.
        var lineIndex = current.Line + (down ? 1 : -1);
        while (lineIndex >= 0 && lineIndex < places.Lines.Count && (places.Lines[lineIndex].Line.Offsets.Length == 0 || !Ours(places.Lines[lineIndex].Line)))
            lineIndex += down ? 1 : -1;
        if (lineIndex < 0 || lineIndex >= places.Lines.Count)
        {
            if (story is var (paragraph, offset))
                return places.Exact(paragraph, offset) is { } own ? LineAcross(page, pictures.Index(Page(page), own), down, null) : null;
            var next = page + (down ? 1 : -1);
            if (next < 0 || next >= scenes.Count)
                return null;
            var nextSpots = pictures.Spots(Page(next));
            return nextSpots.Count == 0 ? null : (next, down ? 0 : nextSpots.Count, !down);
        }
        var (toPage, line) = places.Lines[lineIndex];
        var middle = toPage.MapPoint((float)(line.Starts[0] * PxPerPt), (float)((line.Top + line.Bottom) / 2 * PxPerPt));
        return places.HitPlace(new SKPoint(x, middle.Y), Ours) is var (spot, after) ? Placed(page, spot, after) : null;
    }

    /// <summary>Scrolls the cursor into view where it is out of it; not while a drawing is dragged (the view goes with the pointer then).</summary>
    void Reveal((int Page, int At) place)
    {
        if (scroller == null || place.Page >= rows.Count || dragged is { Moving: true } || lineDrag is { Moving: true })
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

    /// <summary>
    /// The cursor moved: it shows and blinks again from there, the input method learns where it stands, the session follows; the column
    /// it goes up and down to is forgotten unless it went so (see goal).
    /// </summary>
    void Moved()
    {
        Blink();
        ime.Moved();
        if (!across)
            goal = null;
        if (session == null || fromSession || caret is not var (page, at) || page >= scenes.Count || anchor is not var (anchorPage, anchorAt)
            || anchorPage >= scenes.Count || pictures.Spots(Page(page)) is not { Count: > 0 } spots || pictures.Spots(Page(anchorPage)) is not { Count: > 0 } anchorSpots)
            return;
        var (spot, after) = CaretSpot(spots, at);
        var (block, offset) = pictures.Places(Page(page)).PlaceAt(spot, after);
        var (from, fromAfter) = anchorAt >= anchorSpots.Count ? (anchorSpots[^1], true) : (anchorSpots[anchorAt], false);
        // An anchor where the cursor is is its place, at a line's end too (not the next line's start).
        var (anchorBlock, anchorOffset) = (anchorPage, anchorAt) == (page, at) ? (block, offset) : pictures.Places(Page(anchorPage)).PlaceAt(from, fromAfter);
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
        // A picture being cropped may show anew.
        cropStale = cropping;
        Update(editing.Document.Scenes());
        Show(editing.Caret, editing.Anchor);
        CheckEmpty();
        LayOutLater();
        Changed?.Invoke();
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

    /// <summary>
    /// Puts the cursor and the selection's anchor at places of the document's text, on the pages that show them, the cursor scrolled into
    /// view (not while the pointer selects, which scrolls as it holds near an edge).
    /// </summary>
    void Show(TextPlace caretAt, TextPlace anchorAt, bool reveal = true)
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
        if (reveal)
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

    /// <summary>Pastes the text the clipboard holds; without text the pictures it holds: picture files copied from a folder, else a picture itself.</summary>
    async void Paste(IClipboard clipboard)
    {
        if (session is not { } editing)
            return;
        if (await clipboard.TryGetTextAsync() is { Length: > 0 } text)
        {
            Edit(() =>
            {
                if (copied is { } fragment && fragment.Text == text)
                    editing.Paste(fragment);
                else
                    editing.Paste(text);
            });
            return;
        }
        if (await clipboard.TryGetDataAsync() is { } data)
            await PastePictures(data);
    }

    /// <summary>The pictures copied data holds put in at the cursor: picture files (copied from a folder), else a picture itself (a screenshot).</summary>
    /// <summary>The cursor at the place a point of the view hits (see PlaceAt), the selection gone; false where there is no text.</summary>
    public bool PlaceCursorAt(Point point)
    {
        Leave(point);
        if (PlaceAt(point) is not var (page, at, end))
            return false;
        (caret, anchor, lineEnd) = ((page, at), (page, at), end);
        Moved();
        InvalidateVisual();
        return true;
    }

    /// <summary>Pictures chosen from files (the platform's picker, several at once) go in at the cursor, one after the other.</summary>
    public async Task PickPictures()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage)
            return;
        string Text(string key, string fallback) => this.TryFindResource(key, out var text) && text is string s ? s : fallback;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Text("InsertPictureFile", "Picture"),
            AllowMultiple = true,
            FileTypeFilter = [new FilePickerFileType(Text("PictureFiles", "Pictures")) { Patterns = [.. PictureFiles.Select(e => "*" + e)] }],
        });
        if (PictureFilesOf(files) is { Count: > 0 } pictures)
            InsertPictures(await Read(pictures));
        Focus();
    }

    /// <summary>
    /// A picture chosen from a file shows in the frame of the picture selected (see EditSession.ReplacePicture: as it stands and shows, the
    /// new one filling it), made ready first as one put in (see InsertPictures) for the size it fills the frame at.
    /// </summary>
    public async Task ReplacePicture()
    {
        if (session is not { } editing || editing.PictureShown is not { Width: { } width, Height: { } height }
            || TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage)
            return;
        string Text(string key, string fallback) => this.TryFindResource(key, out var text) && text is string s ? s : fallback;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Text("PictureReplace", "Replace"),
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(Text("PictureFiles", "Pictures")) { Patterns = [.. PictureFiles.Select(e => "*" + e)] }],
        });
        if (PictureFilesOf(files) is { Count: > 0 } chosen && (await Read(chosen)) is [var (bytes, _), ..])
        {
            var upright = PictureFit.Upright(bytes) ?? AsPng(bytes) ?? bytes;
            var (pixelsWide, pixelsHigh) = Html.ImageInfo.Size(upright);
            // As large as it is shown filling the frame, its middle in it.
            var part = WordDocument.Filling(width, height, pixelsWide, pixelsHigh);
            Edit(() => editing.ReplacePicture(PictureFit.Fitted(upright, width / Math.Max(0.01, part.Wide), height / Math.Max(0.01, part.High))));
        }
        Focus();
    }

    /// <summary>The pictures the clipboard holds go in at the cursor (files of pictures, else a picture itself); its text not.</summary>
    public async Task PasteClipboardPictures()
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard && await clipboard.TryGetDataAsync() is { } data)
            await PastePictures(data);
        Focus();
    }

    async Task PastePictures(IAsyncDataTransfer data)
    {
        if (PictureFilesOf(await data.TryGetFilesAsync()) is { Count: > 0 } files)
            InsertPictures(await Read(files));
        else if (await data.TryGetBitmapAsync() is { } bitmap)
        {
            using (bitmap)
            {
                using var png = new MemoryStream();
                bitmap.Save(png, PngBitmapEncoderOptions.Default);
                InsertPictures([(png.ToArray(), "Picture")]);
            }
        }
    }

    /// <summary>The files of pictures among those given (see PictureFiles).</summary>
    static List<IStorageFile> PictureFilesOf(IEnumerable<IStorageItem>? items) =>
        items == null ? [] : [.. items.OfType<IStorageFile>().Where(f => PictureFiles.Contains(Path.GetExtension(f.Name)))];

    /// <summary>The bytes of files with their names (without their extensions); those that cannot be read left out.</summary>
    static async Task<List<(byte[] Bytes, string Name)>> Read(IEnumerable<IStorageFile> files)
    {
        var read = new List<(byte[], string)>();
        foreach (var file in files)
        {
            try
            {
                await using var stream = await file.OpenReadAsync();
                using var bytes = new MemoryStream();
                await stream.CopyToAsync(bytes);
                read.Add((bytes.ToArray(), Path.GetFileNameWithoutExtension(file.Name)));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
        return read;
    }

    /// <summary>
    /// Puts pictures at the cursor in place of the selection, one after the other (see EditSession.InsertPicture), made ready first
    /// (see PictureFit): upright, of a kind Word keeps as it is, else as PNG (WebP, an icon), and no larger in pixels than their size
    /// needs; what is no picture is left out.
    /// </summary>
    public void InsertPictures(IReadOnlyList<(byte[] Bytes, string Name)> given)
    {
        if (session is not { } editing)
            return;
        Edit(() =>
        {
            foreach (var (bytes, name) in given)
            {
                var upright = PictureFit.Upright(bytes) ?? AsPng(bytes) ?? bytes;
                if (editing.PictureSize(upright) is { } size)
                    editing.InsertPicture(PictureFit.Fitted(upright, size.Width, size.Height), name, size);
            }
        });
    }

    /// <summary>A picture's bytes as PNG, where the platform can read them; else null.</summary>
    static byte[]? AsPng(byte[] bytes)
    {
        try
        {
            using var bitmap = new Bitmap(new MemoryStream(bytes));
            using var png = new MemoryStream();
            bitmap.Save(png, PngBitmapEncoderOptions.Default);
            return png.ToArray();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Pictures dragged over the sheets: they would go to the place under the pointer, which shows; other things are not taken.</summary>
    void DraggedOver(object? sender, DragEventArgs e)
    {
        var files = session != null ? PictureFilesOf(e.DataTransfer.TryGetFiles()) : [];
        e.DragEffects = files.Count > 0 ? DragDropEffects.Copy : DragDropEffects.None;
        DropAt(files.Count > 0 ? PlaceAt(e.GetPosition(this)) : null);
        e.Handled = true;
    }

    void DropAt((int Page, int At, bool End)? at)
    {
        if (at == dropAt)
            return;
        dropAt = at;
        InvalidateVisual();
    }

    /// <summary>Pictures let go over the sheets go in where they were let go, one after the other, as Word puts them in.</summary>
    async void Dropped(object? sender, DragEventArgs e)
    {
        var at = dropAt ?? PlaceAt(e.GetPosition(this));
        DropAt(null);
        var files = PictureFilesOf(e.DataTransfer.TryGetFiles());
        if (files.Count == 0 || at is not var (page, index, end))
            return;
        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
        var read = await Read(files);
        (caret, anchor, lineEnd) = ((page, index), (page, index), end);
        Moved();
        InsertPictures(read);
        Focus();
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
                // Where each line's characters begin among the page's, in the order of its lines (those of floating drawings not lines).
                var starts = new int[places.Lines.Count];
                for (var k = 1; k < places.Lines.Count; k++)
                    starts[k] = starts[k - 1] + places.Lines[k - 1].Line.Offsets.Length;
                var recording = new Recording(recorder.EndRecording(), places, places.Spots(), starts);
                pages.AddOrUpdate(scene, recording);
                return recording;
            }
        }
    }

    /// <summary>The rows in view drawn on Avalonia's Skia canvas, and over them the pages fading out (see Restyle).</summary>
    sealed class Drawing(Rect bounds, List<(Row Row, double Left)> rows, double factor, IReadOnlyList<PageScene> scenes,
        Pictures pictures, Dictionary<int, Mark> marks, SKColor selection, SKColor caret,
        (List<(SKRect Sheet, SKPicture Picture, float Factor)> Pages, float Opacity)? fading = null) : ICustomDrawOperation
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
            // A page as it was: its sheet and its drawing, the more transparent the further the fade has come.
            if (fading is var (pages, opacity))
            {
                using var faded = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(opacity * 255)) };
                foreach (var (sheet, picture, scale) in pages)
                {
                    canvas.DrawRect(sheet, faded);
                    canvas.Save();
                    canvas.Translate(sheet.Left, sheet.Top);
                    canvas.Scale(scale);
                    canvas.ClipRect(SKRect.Create(sheet.Width / scale, sheet.Height / scale));
                    canvas.DrawPicture(picture, faded);
                    canvas.Restore();
                    GC.KeepAlive(picture);
                }
            }
        }
    }
}
