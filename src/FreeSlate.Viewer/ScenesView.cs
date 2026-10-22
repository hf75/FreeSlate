using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.VisualTree;
using FreeSlate.Rendering;
using FreeSlate.Skia;
using SkiaSharp;

namespace FreeSlate.Viewer;

/// <summary>A page of Word's own rendering to compare with: its picture and its size in CSS pixels.</summary>
sealed record WordPicture(string File, double Width, double Height);

/// <summary>
/// The document's pages drawn by Skia from their scenes (catalog: engine.output), one below the other in a scroll viewer,
/// only those in view; zoomed as the viewer's page script zooms (a fit or a factor), with Word's pages beside them,
/// over them or against them as the compare box says.
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

    public ScenesView()
    {
        ClipToBounds = true;
    }

    public IReadOnlyList<PageScene> Scenes
    {
        get => scenes;
        set
        {
            scenes = value;
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
        if (visible.Count > 0)
            context.Custom(new Drawing(new Rect(Bounds.Size), visible, factor, scenes, word, comparison, blend, pictures));
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

    /// <summary>Scrolls to a page's top.</summary>
    public void GoTo(int page)
    {
        if (scroller == null || rows.Count == 0)
            return;
        var row = rows[Math.Clamp(page, 1, rows.Count) - 1];
        scroller.Offset = new Vector(scroller.Offset.X, Math.Max(0, (row.Top - Edge) * factor));
        Track();
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

    /// <summary>The pages' recorded drawings and Word's pictures, made once and drawn on the render thread.</summary>
    sealed class Pictures
    {
        readonly Dictionary<PageScene, SKPicture> pages = new(ReferenceEqualityComparer.Instance);
        readonly Dictionary<string, SKImage?> images = [];

        public void Clear()
        {
            lock (this)
            {
                foreach (var picture in pages.Values)
                    picture.Dispose();
                pages.Clear();
            }
        }

        public SKPicture Page(PageScene scene)
        {
            lock (this)
            {
                if (pages.TryGetValue(scene, out var picture))
                    return picture;
                using var recorder = new SKPictureRecorder();
                var size = PageSize(scene);
                SkiaScene.Draw(scene, recorder.BeginRecording(SKRect.Create((float)size.Width, (float)size.Height)));
                return pages[scene] = recorder.EndRecording();
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
        string comparison, double blend, Pictures pictures) : ICustomDrawOperation
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
                    canvas.DrawPicture(pictures.Page(scene));
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
        }
    }
}
