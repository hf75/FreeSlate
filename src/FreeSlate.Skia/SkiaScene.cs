using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using FreeSlate.Drawing;
using FreeSlate.Html;
using FreeSlate.Rendering;
using FreeSlate.Text;
using SkiaSharp;

namespace FreeSlate.Skia;

/// <summary>What a scene held that the Skia backend did not draw, or drew more simply, counted while drawing.</summary>
public sealed class SkiaStatistics
{
    /// <summary>Items still held as their HTML or SVG (MarkupItem), not drawn.</summary>
    public int Markup { get; set; }

    /// <summary>Effects drawn without them: filters (glow, shadow, soft edges, a picture's recoloring) and reflections.</summary>
    public int Effects { get; set; }

    /// <summary>Pictures not drawn: SVG and other formats Skia does not decode, tiled picture fills.</summary>
    public int Pictures { get; set; }

    /// <summary>Paints drawn in one color: gradients and outlines of text, patterns.</summary>
    public int Paints { get; set; }

    public void Add(SkiaStatistics other)
    {
        Markup += other.Markup;
        Effects += other.Effects;
        Pictures += other.Pictures;
        Paints += other.Paints;
    }
}

/// <summary>
/// Draws scenes (see PageScene) with Skia (catalog: engine.output): a page on a canvas in CSS pixels (96 an inch), as
/// the HTML backend's page is, the Word engine's items in points and the DrawingML renderer's in pixels; text from its
/// glyphs' outlines, as the HTML draws glyphs of outlines. What the scene still holds as markup is not drawn, and
/// effects and some paints are drawn more simply (see SkiaStatistics).
/// </summary>
public static class SkiaScene
{
    const double PxPerPt = 96.0 / 72.0;

    static float Px(double points) => (float)(points * PxPerPt);

    /// <summary>A page drawn on a canvas whose units are CSS pixels from the page's top left, its paper white.</summary>
    public static void Draw(PageScene scene, SKCanvas canvas, SkiaStatistics? statistics = null)
    {
        using var paper = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
        canvas.DrawRect(0, 0, Px(scene.Width), Px(scene.Height), paper);
        new Drawer(canvas, statistics ?? new SkiaStatistics()).Draw(scene.Items);
    }

    /// <summary>
    /// A page as a PNG image of <paramref name="width"/> by <paramref name="height"/> pixels (by default its size in CSS
    /// pixels, rounded up), <paramref name="scale"/> pixels a CSS pixel.
    /// </summary>
    public static byte[] Png(PageScene scene, double scale = 1, int? width = null, int? height = null, SkiaStatistics? statistics = null)
    {
        var w = width ?? (int)Math.Ceiling(Px(scene.Width) * scale);
        var h = height ?? (int)Math.Ceiling(Px(scene.Height) * scale);
        using var surface = SKSurface.Create(new SKImageInfo(Math.Max(1, w), Math.Max(1, h), SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        canvas.Scale((float)scale);
        Draw(scene, canvas, statistics);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>
    /// Pages as a PDF document (catalog: engine.output): every page of its size, its text in the fonts it is set in
    /// (their subsets embedded), the rest as vectors and pictures.
    /// </summary>
    public static void Pdf(IEnumerable<PageScene> scenes, Stream output, SkiaStatistics? statistics = null)
    {
        using var stream = new SKManagedWStream(output);
        using var document = SKDocument.CreatePdf(stream, new SKDocumentPdfMetadata { Producer = "FreeSlate", RasterDpi = 300 });
        foreach (var scene in scenes)
        {
            var canvas = document.BeginPage((float)scene.Width, (float)scene.Height);
            canvas.Scale((float)(1 / PxPerPt));
            Draw(scene, canvas, statistics);
            document.EndPage();
        }
        document.Close();
    }

    /// <summary>The fonts as Skia's typefaces, made once; null where Skia cannot read one (its glyphs are drawn from outlines).</summary>
    static readonly ConditionalWeakTable<FontData, SKTypeface?[]> typefaces = [];

    static SKTypeface? Typeface(FontData font) => typefaces.GetValue(font, f =>
        [f.Memory is { } bytes ? SKTypeface.FromData(SKData.CreateCopy(bytes.Span), f.Index) : SKTypeface.FromFile(f.Path, f.Index)])[0];

    /// <summary>The outlines of a font's glyphs as paths in font units (see FontData.Outline), made once.</summary>
    static readonly ConditionalWeakTable<FontData, Dictionary<uint, SKPath?>> outlines = [];

    static SKPath? Outline(FontData font, uint glyph)
    {
        var paths = outlines.GetValue(font, _ => []);
        lock (paths)
        {
            if (!paths.TryGetValue(glyph, out var path))
            {
                var data = font.Outline(glyph);
                paths[glyph] = path = data.Length == 0 ? null : SKPath.ParseSvgPathData(data);
            }
            return path;
        }
    }

    static SKColor Color(RgbColor c) =>
        new((byte)Math.Round(Math.Clamp(c.R, 0, 1) * 255), (byte)Math.Round(Math.Clamp(c.G, 0, 1) * 255),
            (byte)Math.Round(Math.Clamp(c.B, 0, 1) * 255), (byte)Math.Round(Math.Clamp(c.A, 0, 1) * 255));

    static SKMatrix Matrix(Html.Matrix m) => new()
    {
        ScaleX = (float)m.A, SkewY = (float)m.B, SkewX = (float)m.C, ScaleY = (float)m.D, TransX = (float)m.E, TransY = (float)m.F, Persp2 = 1,
    };

    sealed class Drawer(SKCanvas canvas, SkiaStatistics statistics)
    {
        static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

        /// <summary>The pictures of the page, decoded once (a picture may stand on a page many times).</summary>
        readonly Dictionary<string, SKImage?> pictures = [];

        public void Draw(IEnumerable<SceneItem> items)
        {
            foreach (var item in items)
                Draw(item);
        }

        void Concat(SKMatrix matrix) => canvas.SetMatrix(canvas.TotalMatrix.PreConcat(matrix));

        void Draw(SceneItem item)
        {
            switch (item)
            {
                case MarkupItem markup:
                    if (markup.Html.Length > 0)
                        statistics.Markup++;
                    break;
                case CanvasItem surface:
                    Draw(surface.Items);
                    break;
                case TurnedItem turned:
                    canvas.Save();
                    canvas.Translate(Px(turned.Width), 0);
                    canvas.RotateDegrees(90);
                    Draw(turned.Items);
                    canvas.Restore();
                    break;
                case RectItem rect:
                    FillRect(SKRect.Create(Px(rect.X), Px(rect.Y), Px(rect.Width), Px(rect.Height)), rect.Color);
                    break;
                case RuleItem rule:
                    FillRect(SKRect.Create(Px(rule.X), Px(rule.Y), Px(rule.Width), Px(rule.Height)), RgbColor.Black);
                    break;
                case RotatedItem rotated:
                    canvas.Save();
                    canvas.RotateDegrees((float)rotated.Angle, Px(rotated.PivotX), Px(rotated.PivotY));
                    Draw(rotated.Items);
                    canvas.Restore();
                    break;
                case LinkItem link:
                    Draw(link.Items);
                    break;
                case CheckBoxItem box:
                {
                    using var pen = Pen(RgbColor.Black, Px(0.48));
                    canvas.DrawRect(SKRect.Create(Px(box.X), Px(box.Y), Px(box.Side), Px(box.Side)), pen);
                    if (box.Checked)
                    {
                        canvas.DrawLine(Px(box.X), Px(box.Y), Px(box.X + box.Side), Px(box.Bottom), pen);
                        canvas.DrawLine(Px(box.X + box.Side), Px(box.Y), Px(box.X), Px(box.Bottom), pen);
                    }
                    break;
                }
                case LineItem line:
                {
                    using var pen = Pen(line.Color, Px(line.Width));
                    canvas.DrawLine(Px(line.X1), Px(line.Y1), Px(line.X2), Px(line.Y2), pen);
                    break;
                }
                case DashesItem dashed:
                {
                    using var pen = Pen(dashed.Color, Px(dashed.Width));
                    foreach (var (x1, y1, x2, y2) in dashed.Dashes)
                        canvas.DrawLine(Px(x1), Px(y1), Px(x2), Px(y2), pen);
                    break;
                }
                case PathsItem paths:
                    canvas.Save();
                    canvas.Scale((float)PxPerPt);
                    foreach (var path in paths.Paths)
                    {
                        using var shape = SKPath.ParseSvgPathData(path.Data);
                        if (shape == null)
                            continue;
                        if (path.Fill is { } fill)
                        {
                            using var brush = Brush(fill);
                            canvas.DrawPath(shape, brush);
                        }
                        if (path.Stroke is { } stroke)
                        {
                            using var pen = Pen(stroke, (float)path.StrokeWidth);
                            canvas.DrawPath(shape, pen);
                        }
                    }
                    canvas.Restore();
                    break;
                case AltTextItem or BookmarkItem or HiddenTextItem:
                    break;
                case TextItem text:
                    Text(text);
                    break;
                case GlyphsItem glyphs:
                    Glyphs(glyphs);
                    break;
                case LabelItem label:
                    Label(label);
                    break;
                case AcrossItem across:
                    canvas.Save();
                    canvas.Translate(Px(across.X), Px(across.Y));
                    canvas.RotateDegrees(-90);
                    Draw(across.Items);
                    canvas.Restore();
                    break;
                case DecorationItem decoration:
                    Draw(decoration.Items);
                    break;
                case FilterItem filtered:
                    statistics.Effects++;
                    Draw(filtered.Items);
                    break;
                case FilterCopyItem copied:
                    statistics.Effects++;
                    Draw(copied.Items);
                    break;
                case ReflectionItem reflected:
                    statistics.Effects++;
                    Draw(reflected.Items);
                    break;
                case CurveItem curve:
                {
                    using var path = new SKPath();
                    path.MoveTo(Px(curve.Start.X), Px(curve.Start.Y));
                    foreach (var (first, second, end) in curve.Curves)
                        path.CubicTo(Px(first.X), Px(first.Y), Px(second.X), Px(second.Y), Px(end.X), Px(end.Y));
                    using var pen = Pen(curve.Color, Px(curve.Width));
                    canvas.DrawPath(path, pen);
                    break;
                }
                case ClipItem clip:
                    canvas.Save();
                    canvas.ClipRect(SKRect.Create(Px(clip.X), Px(clip.Y), Px(clip.Width), Px(clip.Height)));
                    Draw(clip.Items);
                    canvas.Restore();
                    break;
                case BitmapItem bitmap:
                {
                    var info = new SKImageInfo(bitmap.PixelWidth, bitmap.PixelHeight, SKColorType.Rgba8888, SKAlphaType.Unpremul);
                    using var image = SKImage.FromPixelCopy(info, bitmap.Rgba);
                    if (image != null)
                        canvas.DrawImage(image, SKRect.Create(Px(bitmap.X), Px(bitmap.Y), Px(bitmap.Width), Px(bitmap.Height)), Sampling);
                    break;
                }
                case BoxItem box:
                    canvas.Save();
                    if (box.Transform is { } transform)
                        Concat(Matrix(transform));
                    Draw(box.Items);
                    canvas.Restore();
                    break;
                case SvgItem svg:
                    Draw(svg.Items);
                    break;
                case GroupBoxItem group:
                    Draw(group.Items);
                    break;
                case ShapePathItem shapePath:
                    ShapePath(shapePath);
                    break;
                case ShapeEllipseItem ellipse:
                    ShapeEllipse(ellipse);
                    break;
                case DefsItem:
                    break;
                case TransformItem transformed:
                    canvas.Save();
                    Concat(Matrix(transformed.Transform));
                    Draw(transformed.Items);
                    canvas.Restore();
                    break;
                case SvgGroupItem grouped:
                    Group(grouped);
                    break;
                case WrappedItem wrapped:
                    Draw(wrapped.Items);
                    break;
                case PictureItem picture:
                    Picture(picture);
                    break;
            }
        }

        void FillRect(SKRect rect, RgbColor color)
        {
            using var brush = Brush(color);
            canvas.DrawRect(rect, brush);
        }

        static SKPaint Brush(RgbColor color) => new() { Color = SkiaScene.Color(color), Style = SKPaintStyle.Fill, IsAntialias = true };

        static SKPaint Pen(RgbColor color, float width) =>
            new() { Color = SkiaScene.Color(color), Style = SKPaintStyle.Stroke, StrokeWidth = width, IsAntialias = true };

        /// <summary>
        /// Text the HTML gives the browser: shaped with its features, every cluster at its place on the baseline, the
        /// glyphs of a cluster after each other; right-to-left text from its right end, its spaces wider by WordSpacing.
        /// </summary>
        void Text(TextItem text)
        {
            var font = text.Font;
            var glyphs = font.GlyphsWithFeatures(text.Text, text.Features, text.RightToLeft, text.Language);
            if (glyphs.Length == 0)
                return;
            var places = new double[glyphs.Length];
            if (!text.RightToLeft)
            {
                var (cluster, x, inCluster) = (-1, 0.0, 0.0);
                for (var i = 0; i < glyphs.Length; i++)
                {
                    if (glyphs[i].Cluster != cluster)
                    {
                        cluster = glyphs[i].Cluster;
                        (x, inCluster) = (text.Positions[Math.Min(cluster, text.Positions.Length - 1)], 0);
                    }
                    places[i] = x + inCluster + glyphs[i].OffsetX * text.Size;
                    inCluster += glyphs[i].Advance * text.Size;
                }
            }
            else
            {
                bool Space(int g) => glyphs[g].Cluster < text.Text.Length && text.Text[glyphs[g].Cluster] == ' ';
                var total = glyphs.Select((g, i) => g.Advance * text.Size + (Space(i) ? text.WordSpacing : 0)).Sum();
                var x = text.Width - total;
                for (var i = 0; i < glyphs.Length; i++)
                {
                    places[i] = x + glyphs[i].OffsetX * text.Size;
                    x += glyphs[i].Advance * text.Size + (Space(i) ? text.WordSpacing : 0);
                }
            }
            if (text.Paint != null && !text.Hollow)
                statistics.Paints++;
            using var fill = text.Hollow ? (text.Shadow ? Brush(RgbColor.White) : null) : Brush(text.Color);
            // Outlined letters 0.14 pt wide, bold of a font without a bold face stroked a 35th of the size wide.
            using var stroke = text.Hollow ? Pen(text.Color, Px(0.14)) : text.StrokedBold ? Pen(text.Color, Px(text.Size / 35)) : null;
            if (stroke != null)
            {
                stroke.StrokeMiter = 10;
                stroke.StrokeJoin = text.Hollow ? SKStrokeJoin.Round : SKStrokeJoin.Miter;
            }
            if (Typeface(font) is { } typeface)
            {
                using var face = new SKFont(typeface, Px(text.Size), (float)text.Scale)
                {
                    Subpixel = true, LinearMetrics = true, Hinting = SKFontHinting.None, Edging = SKFontEdging.Antialias,
                    // The browser slants italic of a font without an italic face.
                    SkewX = text.Italic && !typeface.IsItalic ? -0.21f : 0,
                };
                using var builder = new SKTextBlobBuilder();
                var run = builder.AllocatePositionedRun(face, glyphs.Length);
                var ids = run.GetGlyphSpan();
                var points = run.GetPositionSpan();
                for (var i = 0; i < glyphs.Length; i++)
                {
                    ids[i] = (ushort)glyphs[i].Id;
                    points[i] = new SKPoint(Px(text.X + places[i] * text.Scale), Px(text.Y - glyphs[i].OffsetY * text.Size));
                }
                using var blob = builder.Build();
                if (blob == null)
                    return;
                if (fill != null)
                    canvas.DrawText(blob, 0, 0, fill);
                if (stroke != null)
                    canvas.DrawText(blob, 0, 0, stroke);
                return;
            }
            var scale = text.Size * PxPerPt / font.UnitsPerEm;
            if (stroke != null)
                stroke.StrokeWidth /= (float)scale;
            for (var i = 0; i < glyphs.Length; i++)
            {
                if (Outline(font, glyphs[i].Id) is not { } path)
                    continue;
                canvas.Save();
                canvas.Translate(Px(text.X + places[i] * text.Scale), Px(text.Y - glyphs[i].OffsetY * text.Size));
                canvas.Scale((float)(scale * text.Scale), (float)scale);
                if (fill != null)
                    canvas.DrawPath(path, fill);
                if (stroke != null)
                    canvas.DrawPath(path, stroke);
                canvas.Restore();
            }
        }

        /// <summary>Glyphs from their outlines, as the HTML's glyph paths stand (see HtmlScene).</summary>
        void Glyphs(GlyphsItem item)
        {
            var data = item.Font;
            var scale = item.Size * PxPerPt / data.UnitsPerEm;
            canvas.Save();
            canvas.Translate(Px(item.X), Px(item.Y));
            canvas.Scale((float)scale);
            if (item.SyntheticItalic)
                canvas.Skew((float)Math.Tan(-12 * Math.PI / 180), 0);
            using var fill = item.Hollow ? (item.Shadow ? Brush(RgbColor.White) : null) : Brush(item.Color);
            using var stroke = item.Hollow ? Pen(item.Color, (float)(0.14 * PxPerPt / scale))
                : item.SyntheticBold ? Pen(item.Color, (float)(data.UnitsPerEm / 35.0)) : null;
            if (stroke != null)
                stroke.StrokeJoin = item.Hollow ? SKStrokeJoin.Round : SKStrokeJoin.Miter;
            foreach (var glyph in item.Glyphs)
            {
                if (Outline(data, glyph.Id) is not { } path)
                    continue;
                canvas.Save();
                if (glyph.TurnX is { } middle)
                    canvas.RotateDegrees(-90, (float)middle, (float)(-data.EmMiddle * data.UnitsPerEm));
                canvas.Translate((float)glyph.X, (float)glyph.Y);
                if (Math.Abs(item.Scale - 1) > 1e-6)
                    canvas.Scale((float)item.Scale, 1);
                if (fill != null)
                    canvas.DrawPath(path, fill);
                if (stroke != null)
                    canvas.DrawPath(path, stroke);
                canvas.Restore();
            }
            canvas.Restore();
        }

        /// <summary>A label ending at its point: its glyphs at their advances.</summary>
        void Label(LabelItem label)
        {
            var glyphs = label.Font.GlyphsWithFeatures(label.Text, []);
            var x = label.X - glyphs.Sum(g => g.Advance * label.Size);
            var scale = label.Size * PxPerPt / label.Font.UnitsPerEm;
            using var fill = Brush(label.Color);
            if (Typeface(label.Font) is { } typeface && glyphs.Length > 0)
            {
                using var face = new SKFont(typeface, Px(label.Size)) { Subpixel = true, LinearMetrics = true, Hinting = SKFontHinting.None, Edging = SKFontEdging.Antialias };
                using var builder = new SKTextBlobBuilder();
                var run = builder.AllocatePositionedRun(face, glyphs.Length);
                var ids = run.GetGlyphSpan();
                var points = run.GetPositionSpan();
                for (var i = 0; i < glyphs.Length; i++)
                {
                    ids[i] = (ushort)glyphs[i].Id;
                    points[i] = new SKPoint(Px(x + glyphs[i].OffsetX * label.Size), Px(label.Y - glyphs[i].OffsetY * label.Size));
                    x += glyphs[i].Advance * label.Size;
                }
                using var blob = builder.Build();
                if (blob != null)
                    canvas.DrawText(blob, 0, 0, fill);
                return;
            }
            foreach (var glyph in glyphs)
            {
                if (Outline(label.Font, glyph.Id) is { } path)
                {
                    canvas.Save();
                    canvas.Translate(Px(x + glyph.OffsetX * label.Size), Px(label.Y - glyph.OffsetY * label.Size));
                    canvas.Scale((float)scale);
                    canvas.DrawPath(path, fill);
                    canvas.Restore();
                }
                x += glyph.Advance * label.Size;
            }
        }

        void ShapePath(ShapePathItem item)
        {
            using var path = SKPath.ParseSvgPathData(item.Data);
            if (path == null)
                return;
            if (Paint(item.Fill, path.Bounds) is { } fill)
            {
                canvas.DrawPath(path, fill);
                fill.Dispose();
            }
            if (item.Outline is { Visible: true } outline)
            {
                using var pen = Pen(outline);
                canvas.DrawPath(path, pen);
            }
        }

        void ShapeEllipse(ShapeEllipseItem item)
        {
            using var path = new SKPath();
            path.AddOval(SKRect.Create((float)(item.X - item.RadiusX), (float)(item.Y - item.RadiusY), (float)(2 * item.RadiusX), (float)(2 * item.RadiusY)));
            path.Transform(SKMatrix.CreateRotationDegrees((float)item.Angle, (float)item.X, (float)item.Y));
            if (Paint(item.Fill, path.Bounds) is { } fill)
            {
                canvas.DrawPath(path, fill);
                fill.Dispose();
            }
        }

        /// <summary>A DrawingML fill as a paint over the bounds of what it fills; null for none (and pictures, see Picture).</summary>
        SKPaint? Paint(Fill fill, SKRect bounds)
        {
            switch (fill)
            {
                case SolidFill solid:
                    return Brush(solid.Color);
                case GradientFill { Stops.Count: > 0 } gradient:
                {
                    var colors = gradient.Stops.Select(s => SkiaScene.Color(s.Color)).ToArray();
                    var positions = gradient.Stops.Select(s => (float)Math.Clamp(s.Position, 0, 1)).ToArray();
                    SKShader shader;
                    if (gradient.Path == null)
                    {
                        var angle = (gradient.Angle ?? 0) * Math.PI / 180;
                        var (dx, dy) = (Math.Cos(angle), Math.Sin(angle));
                        var half = (Math.Abs(bounds.Width * dx) + Math.Abs(bounds.Height * dy)) / 2;
                        var (cx, cy) = (bounds.MidX, bounds.MidY);
                        shader = SKShader.CreateLinearGradient(new SKPoint((float)(cx - dx * half), (float)(cy - dy * half)),
                            new SKPoint((float)(cx + dx * half), (float)(cy + dy * half)), colors, positions, SKShaderTileMode.Clamp);
                    }
                    else
                    {
                        // A path gradient from the middle of its focus rectangle (fillToRect, insets from the sides) out.
                        var (l, t, r, b) = gradient.FocusRect;
                        var focus = new SKPoint(bounds.Left + bounds.Width * (float)((l + 1 - r) / 2), bounds.Top + bounds.Height * (float)((t + 1 - b) / 2));
                        var radius = new[] { (bounds.Left, bounds.Top), (bounds.Right, bounds.Top), (bounds.Left, bounds.Bottom), (bounds.Right, bounds.Bottom) }
                            .Max(c => Math.Sqrt((c.Item1 - focus.X) * (c.Item1 - focus.X) + (c.Item2 - focus.Y) * (c.Item2 - focus.Y)));
                        shader = SKShader.CreateRadialGradient(focus, (float)Math.Max(radius, 1), colors, positions, SKShaderTileMode.Clamp);
                    }
                    return new SKPaint { Shader = shader, Style = SKPaintStyle.Fill, IsAntialias = true };
                }
                case PatternFill pattern:
                    statistics.Paints++;
                    return Brush(new RgbColor((pattern.Foreground.R + pattern.Background.R) / 2, (pattern.Foreground.G + pattern.Background.G) / 2,
                        (pattern.Foreground.B + pattern.Background.B) / 2, Math.Max(pattern.Foreground.A, pattern.Background.A)));
                case SlideFill slide:
                    return Paint(slide.Fill, bounds);
                case PictureFill:
                    statistics.Pictures++;
                    return null;
                default:
                    return null;
            }
        }

        /// <summary>A DrawingML outline as a pen: its width, at least half a pixel, caps, joins and dashes.</summary>
        static SKPaint Pen(Outline outline)
        {
            var width = Math.Max(Units.ToPx(outline.WidthEmu), 0.5);
            var pen = Pen(Fill.MainColor(outline.Fill) ?? RgbColor.Black, (float)width);
            pen.StrokeCap = outline.Cap switch { "rnd" => SKStrokeCap.Round, "sq" => SKStrokeCap.Square, _ => SKStrokeCap.Butt };
            pen.StrokeJoin = outline.Join switch { "round" => SKStrokeJoin.Round, "bevel" => SKStrokeJoin.Bevel, _ => SKStrokeJoin.Miter };
            pen.StrokeMiter = (float)Math.Max(1, outline.MiterLimit);
            if (Dashes.Pattern(outline) is { Length: > 0 } dash)
            {
                var round = outline.Cap == "rnd";
                var lengths = new float[dash.Length + dash.Length % 2];
                for (var i = 0; i < dash.Length; i += 2)
                {
                    var (d, space) = (dash[i], i + 1 < dash.Length ? dash[i + 1] : 0);
                    (lengths[i], lengths[i + 1]) = round ? ((float)(Math.Max(0, d - 1) * width), (float)((space + 1) * width)) : ((float)(d * width), (float)(space * width));
                }
                if (lengths.Sum() > 0)
                    pen.PathEffect = SKPathEffect.CreateDash(lengths, 0);
            }
            return pen;
        }

        static readonly Regex Opacity = new(" opacity=\"([0-9.]+)\"", RegexOptions.Compiled);

        /// <summary>A group: its opacity where it has one (a 3D face's), its other attributes not.</summary>
        void Group(SvgGroupItem group)
        {
            if (Opacity.Match(group.Attributes) is { Success: true } match
                && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var opacity))
            {
                canvas.SaveLayer(new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255)) });
                Draw(group.Items);
                canvas.Restore();
                return;
            }
            Draw(group.Items);
        }

        void Picture(PictureItem picture)
        {
            if (!pictures.TryGetValue(picture.Source, out var image))
                pictures[picture.Source] = image = Decode(picture.Source);
            if (image == null)
            {
                statistics.Pictures++;
                return;
            }
            if (picture.Filter.Length > 0)
                statistics.Effects++;
            canvas.Save();
            using (var clip = SKPath.ParseSvgPathData(picture.ClipPath))
            {
                if (clip != null)
                    canvas.ClipPath(clip, antialias: true);
            }
            canvas.DrawImage(image, SKRect.Create((float)picture.X, (float)picture.Y, (float)picture.Width, (float)picture.Height), Sampling);
            canvas.Restore();
        }

        /// <summary>A picture of a data URI in a format Skia decodes, else null.</summary>
        static SKImage? Decode(string uri)
        {
            if (!uri.StartsWith("data:", StringComparison.Ordinal) || uri.IndexOf(',') is not (> 0 and var comma)
                || !uri.AsSpan(0, comma).EndsWith(";base64", StringComparison.Ordinal))
                return null;
            try
            {
                return SKImage.FromEncodedData(Convert.FromBase64String(uri[(comma + 1)..]));
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
