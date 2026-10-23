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
    /// <summary>Items still held as their HTML (MarkupItem), not drawn; their SVG is drawn as SvgDrawing reads it.</summary>
    public int Markup { get; set; }

    /// <summary>Effects drawn without them: a picture's recoloring.</summary>
    public int Effects { get; set; }

    /// <summary>Pictures not drawn: formats neither Skia nor SvgDrawing reads.</summary>
    public int Pictures { get; set; }

    /// <summary>Paints drawn in one color: gradients and outlines of text.</summary>
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
/// glyphs' outlines, as the HTML draws glyphs of outlines; effects through image filters as SVG's filters draw them.
/// What the scene still holds as markup is not drawn, and some paints are drawn more simply (see SkiaStatistics).
/// </summary>
public static class SkiaScene
{
    const double PxPerPt = 96.0 / 72.0;

    static float Px(double points) => (float)(points * PxPerPt);

    /// <summary>
    /// A page drawn on a canvas whose units are CSS pixels from the page's top left, its paper white; with
    /// <paramref name="links"/>, where the page's links stand in those units and what they lead to (LinkItem.Target).
    /// </summary>
    /// <param name="vector">Whether the canvas keeps vectors and pictures as they are (a PDF's): its pictures are then
    /// not evened for pixels (see SvgDrawing.Evened).</param>
    public static void Draw(PageScene scene, SKCanvas canvas, SkiaStatistics? statistics = null, List<(SKRect Area, string Target)>? links = null,
        bool vector = false)
    {
        using var paper = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
        canvas.DrawRect(0, 0, Px(scene.Width), Px(scene.Height), paper);
        var drawer = new Drawer(canvas, statistics ?? new SkiaStatistics(), glyphs: scene.Glyphs, exactDashes: scene.ExactDashes, links: links,
            pixels: vector ? null : SKMatrix.Identity);
        drawer.Predefine(scene.Items);
        drawer.Draw(scene.Items);
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
    /// <param name="metadata">The document's title, author and so on; by default none, its producer FreeSlate.</param>
    public static void Pdf(IEnumerable<PageScene> scenes, Stream output, SkiaStatistics? statistics = null, SKDocumentPdfMetadata? metadata = null)
    {
        using var stream = new SKManagedWStream(output);
        using var document = SKDocument.CreatePdf(stream, metadata ?? PdfMetadata());
        foreach (var scene in scenes)
        {
            var canvas = document.BeginPage((float)scene.Width, (float)scene.Height);
            canvas.Scale((float)(1 / PxPerPt));
            Draw(scene, canvas, statistics, vector: true);
            document.EndPage();
        }
        document.Close();
    }

    /// <summary>
    /// A PDF's settings: produced by FreeSlate, what Skia rasterizes in 300 dots an inch, pictures not already JPEG kept
    /// lossless (the metadata's own default of 0 makes them JPEG of the lowest quality).
    /// </summary>
    public static SKDocumentPdfMetadata PdfMetadata() =>
        new() { Producer = "FreeSlate", RasterDpi = 300, EncodingQuality = SKDocumentPdfMetadata.DefaultEncodingQuality };

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

    /// <param name="onPage">Where the canvas's origin lies on the page (a recording's, see Record); by default the canvas
    /// stands at the page's origin when the drawing begins.</param>
    /// <param name="cache">The pictures decoded so far, shared with the drawer of the page.</param>
    /// <param name="glyphs">The glyph outlines markup names by id (see PageScene.Glyphs).</param>
    /// <param name="defined">The page's definitions so far, shared with the drawer of the page.</param>
    /// <param name="exactDashes">Whether dash patterns repeat in the outline's exact width (see PageScene.ExactDashes).</param>
    /// <param name="links">Where the links drawn on the page itself stand on it (not those within recordings), or null.</param>
    /// <param name="pixels">The matrix under which what the canvas draws lands on pixels (a recording's, its parent's;
    /// the identity for a canvas of pixels), or null where it lands in a PDF.</param>
    sealed class Drawer(SKCanvas canvas, SkiaStatistics statistics, SKMatrix? onPage = null, Dictionary<string, Drawer.Decoded>? cache = null,
        GlyphCache? glyphs = null, Dictionary<string, System.Xml.Linq.XElement>? defined = null, bool exactDashes = false,
        List<(SKRect Area, string Target)>? links = null, SKMatrix? pixels = null)
    {
        /// <summary>The matrix under which what the canvas draws now lands on pixels, or null (see pixels).</summary>
        SKMatrix? OnPixels => pixels?.PreConcat(canvas.TotalMatrix);

        /// <summary>The elements the page's definitions (DefsItem) hold by id, which markup refers to.</summary>
        readonly Dictionary<string, System.Xml.Linq.XElement> definitions = defined ?? [];

        static readonly System.Xml.Linq.XNamespace Svg = "http://www.w3.org/2000/svg";

        /// <summary>What markup names by id: the page's definitions, else a glyph's outline.</summary>
        System.Xml.Linq.XElement? Definition(string id) =>
            definitions.GetValueOrDefault(id) ?? (glyphs?.Outline(id) is { } outline
                ? new System.Xml.Linq.XElement(Svg + "path", new System.Xml.Linq.XAttribute("id", id), new System.Xml.Linq.XAttribute("d", outline))
                : null);

        /// <summary>Markup drawn as the SVG it is (a chart's, a table's), where it is SVG that SvgDrawing reads.</summary>
        bool Markup(string html)
        {
            var start = html.AsSpan().TrimStart();
            if (start.Length == 0)
                return true;
            // Boxes, also where a 3D body's sides (an svg of class sp) come before its faces.
            if (start.StartsWith("<div class=\"sp\"") || start.StartsWith("<svg class=\"sp\""))
                return Boxes(html);
            if (!start.StartsWith("<") || start.StartsWith("<div") || start.StartsWith("<section") || start.StartsWith("<span"))
                return false;
            using var picture = SvgDrawing.Fragment(html, Definition, OnPixels);
            if (picture == null)
                return false;
            canvas.DrawPicture(picture);
            return true;
        }

        static readonly Regex CssTransform = new(@"transform:\s*(matrix3d|matrix)\(([^)]*)\)", RegexOptions.Compiled);

        /// <summary>
        /// Markup of boxes (a 3D body's: divs of class sp, placed by CSS transforms, 3D ones projected by matrix3d) with
        /// SVG in them: every box's transform taken onto the canvas as the homography it gives its plane (whose points
        /// have z 0), its SVG drawn through SvgDrawing; false where the markup is not such boxes.
        /// </summary>
        bool Boxes(string html)
        {
            System.Xml.Linq.XElement holder;
            try
            {
                holder = System.Xml.Linq.XElement.Parse($"<boxes>{html}</boxes>");
            }
            catch (System.Xml.XmlException)
            {
                return false;
            }
            void Box(System.Xml.Linq.XElement element)
            {
                switch (element.Name.LocalName)
                {
                    case "div":
                        canvas.Save();
                        if (element.Attribute("style")?.Value is { } style && CssTransform.Match(style) is { Success: true } match)
                        {
                            var values = match.Groups[2].Value.Split(',').Select(v => double.Parse(v.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                            if (match.Groups[1].Value == "matrix" && values.Length == 6)
                                Concat(Matrix(new Html.Matrix(values[0], values[1], values[2], values[3], values[4], values[5])));
                            else if (values.Length == 16)
                            {
                                // Column-major: a point (x, y, 0, 1) of the box's plane goes to columns 0, 1 and 3.
                                Concat(new SKMatrix((float)values[0], (float)values[4], (float)values[12], (float)values[1], (float)values[5], (float)values[13],
                                    (float)values[3], (float)values[7], (float)values[15]));
                            }
                        }
                        foreach (var child in element.Elements())
                            Box(child);
                        canvas.Restore();
                        break;
                    case "svg":
                        using (var picture = SvgDrawing.Fragment(element.ToString(System.Xml.Linq.SaveOptions.DisableFormatting), Definition, OnPixels))
                        {
                            if (picture != null)
                                canvas.DrawPicture(picture);
                        }
                        break;
                }
            }
            foreach (var element in holder.Elements())
                Box(element);
            return true;
        }

        /// <summary>Keeps a definitions item's elements by id for markup to refer to (those of an id already kept, with <paramref name="first"/> not).</summary>
        void Define(string markup, bool first = false)
        {
            try
            {
                var holder = System.Xml.Linq.XElement.Parse($"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\">{markup}</svg>");
                foreach (var element in holder.Descendants())
                {
                    if (element.Attribute("id")?.Value is { Length: > 0 } id && !(first && definitions.ContainsKey(id)))
                        definitions[id] = element;
                }
            }
            catch (System.Xml.XmlException)
            {
            }
        }

        /// <summary>
        /// Keeps the definitions of a page's items before they are drawn, the first of an id: the HTML's references find
        /// definitions anywhere in the document, those of text's paints after the text, at the page's end.
        /// </summary>
        public void Predefine(IEnumerable<SceneItem> items)
        {
            foreach (var item in items)
            {
                if (item is DefsItem defined)
                    Define(defined.Markup, first: true);
                else if (item is IHoldsItems holder)
                    Predefine(holder.Items);
            }
        }

        static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

        /// <summary>The pictures of the page, decoded once (a picture may stand on a page many times).</summary>
        readonly Dictionary<string, Decoded> pictures = cache ?? [];

        /// <summary>The page's own transform: where the canvas stood when the drawing began (patterns align to the page).</summary>
        readonly SKMatrix page = onPage ?? canvas.TotalMatrix;

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
                    if (!Markup(markup.Html))
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
                    if (links == null)
                    {
                        Draw(link.Items);
                        break;
                    }
                    // Recorded once, the link's text gives where it stands on the page.
                    using (var drawn = Record(link.Items))
                    {
                        if (!drawn.CullRect.IsEmpty && page.TryInvert(out var toPage))
                            links.Add((toPage.PreConcat(canvas.TotalMatrix).MapRect(drawn.CullRect), link.Target));
                        canvas.DrawPicture(drawn);
                    }
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
                case AltTextItem or BookmarkItem or HiddenTextItem or HiddenSpanItem or SvgTextItem { Hidden: true }:
                    break;
                case TextItem text:
                    Text(text);
                    break;
                case MathGlyphItem math:
                    MathGlyph(math);
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
                    Effected(filtered.Effects, filtered.Items);
                    break;
                case FilterCopyItem copied:
                {
                    using var picture = Record(copied.Items);
                    Effects(copied.Effects, picture);
                    canvas.DrawPicture(picture);
                    break;
                }
                case ReflectionItem reflected:
                {
                    using var picture = Record(reflected.Items);
                    canvas.DrawPicture(picture);
                    Reflection(reflected.Reflection, picture);
                    break;
                }
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
                case ShapePathItem { Transform: null, Effects: null } shapePath:
                    ShapePath(shapePath);
                    break;
                case ShapePathItem shapePath:
                    canvas.Save();
                    if (shapePath.Transform is { } pathTransform)
                        Concat(Matrix(pathTransform));
                    Effected(shapePath.Effects, [shapePath with { Transform = null, Effects = null }]);
                    canvas.Restore();
                    break;
                case ShapeRectItem shapeRect:
                {
                    using var path = new SKPath();
                    path.AddRect(SKRect.Create((float)shapeRect.X, (float)shapeRect.Y, (float)shapeRect.Width, (float)shapeRect.Height));
                    Shape(path, shapeRect.Fill, shapeRect.Outline, shapeRect.Attributes, dashesClosed: false);
                    break;
                }
                case BackgroundItem background:
                {
                    var bounds = SKRect.Create((float)background.Width, (float)background.Height);
                    if (background.Fill is PictureFill { Tiling: null } stretched)
                    {
                        Picture(stretched.Image, bounds, null);
                        break;
                    }
                    // A gradient as the HTML's definitions of it paint it (see Served).
                    if (background.Fill is GradientFill && background.Defs.Length > 0)
                        Define(background.Defs);
                    if (((background.Fill is GradientFill ? Served(background.Paint, "fill", bounds) : null) ?? Paint(background.Fill, bounds)) is { } fill)
                    {
                        canvas.DrawRect(bounds, fill);
                        fill.Dispose();
                    }
                    break;
                }
                case ImageItem image:
                    Picture(image.Source, SKRect.Create((float)image.X, (float)image.Y, (float)image.Width, (float)image.Height), null);
                    break;
                case GlyphRunItem glyphRun:
                    GlyphRun(glyphRun);
                    break;
                case SvgTextItem { Effects: null } svgText:
                    SvgText(svgText);
                    break;
                case SvgTextItem svgText:
                    Effected(svgText.Effects, [svgText with { Effects = null }]);
                    break;
                case ShapeEllipseItem ellipse:
                    ShapeEllipse(ellipse);
                    break;
                case DefsItem defined:
                    Define(defined.Markup);
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
                case MissingPictureItem missing:
                    MissingPicture(missing);
                    break;
            }
        }

        static readonly SKTypeface Tahoma = SKTypeface.FromFamilyName("Tahoma") ?? SKTypeface.FromFamilyName("Segoe UI") ?? SKTypeface.Default;

        /// <summary>
        /// Office's sign for a picture it cannot show, as the HTML draws it (see HtmlScene): Word's on white in four
        /// 0.14 pt black lines, its icon and the message wrapped in 1.2 pt Tahoma 1.45 pt apart within its padding;
        /// PowerPoint's in a 1-pixel grey frame, its 13-pixel icon and the message on one line in 11-pixel Tahoma, cut
        /// at the frame.
        /// </summary>
        void MissingPicture(MissingPictureItem missing)
        {
            var (w, h) = ((float)missing.Width, (float)missing.Height);
            using var black = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            using var grey = new SKPaint { Color = new SKColor(0x80, 0x80, 0x80), Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
            using var red = new SKPaint { Color = new SKColor(0xe0, 0, 0), IsAntialias = true };
            using var white = new SKPaint { Color = SKColors.White };
            canvas.Save();
            if (missing.Message is { } message)
            {
                canvas.DrawRect(SKRect.Create(w, h), white);
                using var font = new SKFont(Tahoma, Px(1.2)) { Subpixel = true, Edging = SKFontEdging.Antialias };
                var (left, right, top) = (Px(3.44), w - Px(3.5), Px(0.79));
                canvas.Save();
                canvas.ClipRect(SKRect.Create(w, h));
                var line = "";
                var baseline = top + (Px(1.45) - (font.Metrics.Descent - font.Metrics.Ascent)) / 2 - font.Metrics.Ascent;
                foreach (var word in message.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var longer = line.Length == 0 ? word : line + " " + word;
                    if (line.Length > 0 && font.MeasureText(longer) > right - left)
                    {
                        canvas.DrawText(line, left, baseline, font, black);
                        (line, baseline) = (word, baseline + Px(1.45));
                    }
                    else
                        line = longer;
                }
                if (line.Length > 0)
                    canvas.DrawText(line, left, baseline, font, black);
                canvas.Restore();
                using var frame = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = Px(0.14), IsAntialias = true };
                canvas.DrawLine(0, Px(0.06), w, Px(0.06), frame);
                canvas.DrawLine(0, h - 0.08f, w, h - 0.08f, frame);
                canvas.DrawLine(Px(0.06), 0, Px(0.06), h, frame);
                canvas.DrawLine(w - 0.08f, 0, w - 0.08f, h, frame);
                // The icon's viewBox of 4 × 5 met in its box of 1.68 × 1.92 pt, centred.
                var scale = Math.Min(Px(1.68) / 4, Px(1.92) / 5);
                canvas.Translate(Px(0.84) + (Px(1.68) - 4 * scale) / 2, Px(0.84) + (Px(1.92) - 5 * scale) / 2);
                canvas.Scale(scale);
                canvas.DrawRect(SKRect.Create(0.5f, 0.5f, 3, 4), white);
                canvas.DrawRect(SKRect.Create(0.5f, 0.5f, 3, 4), grey);
                canvas.DrawRect(SKRect.Create(1.5f, 1.5f, 1, 1), red);
            }
            else
            {
                using var frame = new SKPaint { Color = new SKColor(0x6d, 0x6d, 0x6d), Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
                canvas.DrawRect(SKRect.Create(0.5f, 0.5f, w - 1, h - 1), frame);
                canvas.ClipRect(SKRect.Create(1, 1, w - 2, h - 2));
                using var font = new SKFont(Tahoma, 11) { Subpixel = true, Edging = SKFontEdging.Antialias };
                canvas.DrawText("The picture can't be displayed.", 25, 6 + (13 - (font.Metrics.Descent - font.Metrics.Ascent)) / 2 - font.Metrics.Ascent, font, black);
                canvas.Translate(7, 6);
                canvas.DrawRect(SKRect.Create(0.5f, 0.5f, 12, 12), white);
                canvas.DrawRect(SKRect.Create(0.5f, 0.5f, 12, 12), grey);
                using var cross = new SKPaint { Color = new SKColor(0xe0, 0, 0), Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true };
                canvas.DrawLine(4, 4, 9, 9, cross);
                canvas.DrawLine(9, 4, 4, 9, cross);
            }
            canvas.Restore();
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
            // A paint of its own (Word's text fills and outlines): the fill and the stroke its SVG attributes give over the
            // letters' box, as the HTML paints them.
            SKPaint? painted = null, outlined = null;
            var own = text.Paint != null && !text.Hollow && SvgDrawing.Paints(text.Paint,
                SKRect.Create(Px(text.X), Px(text.Y - font.Ascent * text.Size), Px(text.Width), Px((font.Ascent + font.Descent) * text.Size)), Definition, out painted, out outlined);
            if (text.Paint != null && !text.Hollow && !own)
                statistics.Paints++;
            using var fill = own ? painted : text.Hollow ? (text.Shadow ? Brush(RgbColor.White) : null) : Brush(text.Color);
            // Outlined letters 0.14 pt wide, bold of a font without a bold face stroked a 35th of the size wide (in the
            // fill's paint where the text has a paint of its own without an outline).
            using var stroke = own && outlined != null ? outlined
                : text.StrokedBold ? (own ? painted?.Clone() : Pen(text.Color, Px(text.Size / 35)))
                : text.Hollow ? Pen(text.Color, Px(0.14)) : null;
            if (stroke != null && stroke != outlined)
            {
                (stroke.Style, stroke.StrokeWidth) = (SKPaintStyle.Stroke, text.Hollow ? Px(0.14) : Px(text.Size / 35));
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
                var ids = run.Glyphs;
                var points = run.Positions;
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

        /// <summary>A glyph of an equation, the one its layout chose (a script form, a stretched variant).</summary>
        void MathGlyph(MathGlyphItem item)
        {
            using var fill = Brush(item.Color);
            if (Typeface(item.Font) is { } typeface)
            {
                using var face = new SKFont(typeface, Px(item.Size)) { Subpixel = true, LinearMetrics = true, Hinting = SKFontHinting.None, Edging = SKFontEdging.Antialias };
                using var builder = new SKTextBlobBuilder();
                var run = builder.AllocatePositionedRun(face, 1);
                run.Glyphs[0] = (ushort)item.Glyph;
                run.Positions[0] = new SKPoint(Px(item.X), Px(item.Y));
                using var blob = builder.Build();
                if (blob != null)
                    canvas.DrawText(blob, 0, 0, fill);
                return;
            }
            if (Outline(item.Font, item.Glyph) is not { } path)
                return;
            canvas.Save();
            canvas.Translate(Px(item.X), Px(item.Y));
            canvas.Scale((float)(item.Size * PxPerPt / item.Font.UnitsPerEm));
            canvas.DrawPath(path, fill);
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
                var ids = run.Glyphs;
                var points = run.Positions;
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

        static readonly Regex Blending = new(@"mix-blend-mode:\s*([\w-]+)", RegexOptions.Compiled);

        void ShapePath(ShapePathItem item)
        {
            // Its blending with what lies behind it (a fill overlay's, see SlideRenderer): the path drawn on a layer that
            // blends so.
            if (Blending.Match(item.Attributes) is { Success: true } blending && SvgDrawing.BlendMode(blending.Groups[1].Value) is { } mode && mode != SKBlendMode.SrcOver)
            {
                using var layer = new SKPaint { BlendMode = mode };
                canvas.SaveLayer(layer);
                ShapePath(item with { Attributes = Blending.Replace(item.Attributes, "") });
                canvas.Restore();
                return;
            }
            using var path = SKPath.ParseSvgPathData(item.Data);
            if (path != null)
                Shape(path, item.Fill, item.Outline, item.Attributes, item.DashesClosed);
        }

        /// <summary>
        /// An outline filled and stroked as DrawingML gives it: a picture stretched over it, a gradient as the SVG
        /// attributes paint it (see Served), else its own paint; the outline's gradient or pattern over its bounds.
        /// </summary>
        void Shape(SKPath path, Fill fill, Outline? outline, string attributes, bool dashesClosed)
        {
            if (fill is PictureFill { Tiling: null } picture)
                Stretched(picture, path);
            else if (((fill is GradientFill or SlideFill { Fill: GradientFill } ? Served(attributes, "fill", path.Bounds) : null) ?? Paint(fill, path.Bounds)) is { } brush)
            {
                canvas.DrawPath(path, brush);
                brush.Dispose();
            }
            if (outline is { Visible: true })
            {
                using var pen = Pen(outline, dashesClosed);
                // A gradient or a pattern along the outline, over the path's bounds as its fill would lie.
                using var paint = outline.Fill is GradientFill or PatternFill
                    ? (outline.Fill is GradientFill ? Served(attributes, "stroke", path.Bounds) : null) ?? Paint(outline.Fill, path.Bounds)
                    : null;
                if (paint?.Shader is { } shader)
                    pen.Shader = shader;
                canvas.DrawPath(path, pen);
            }
        }

        /// <summary>
        /// A gradient as the item's SVG attributes paint it with a server among the page's definitions (as SvgPaint
        /// writes it: PowerPoint's blends, angles in the unit square, path gradients as trapezoids), or null.
        /// </summary>
        SKPaint? Served(string attributes, string property, SKRect bounds)
        {
            if (Regex.Match(attributes, "(?<![\\w-])" + property + "=\"url\\(#([^)\"]+)\\)\"") is not { Success: true } named
                || Definition(named.Groups[1].Value) is not { } server)
                return null;
            var opacity = Regex.Match(attributes, "(?<![\\w-])" + property + "-opacity=\"([^\"]+)\"") is { Success: true } given
                && double.TryParse(given.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var o) ? o : 1;
            return SvgDrawing.Shader(server, bounds, opacity, Definition) is { } shader ? new SKPaint { Shader = shader, Style = SKPaintStyle.Fill, IsAntialias = true } : null;
        }

        /// <summary>
        /// A picture stretched over what a path fills (a slide's picture, its fill rectangle within the path's box, its
        /// crop beyond), cut at the path, as SvgPaint's pattern of it draws it.
        /// </summary>
        void Stretched(PictureFill fill, SKPath path)
        {
            var bounds = path.Bounds;
            var (w, h) = (bounds.Width, bounds.Height);
            var (fl, ft, fr, fb) = fill.FillRect;
            var (sl, st, sr, sb) = fill.SourceRect;
            var (innerW, innerH) = (w * (1 - fl - fr), h * (1 - ft - fb));
            var (fullW, fullH) = (innerW / Math.Max(1e-6, 1 - sl - sr), innerH / Math.Max(1e-6, 1 - st - sb));
            var (x, y) = (bounds.Left + w * fl - sl * fullW, bounds.Top + h * ft - st * fullH);
            // Its recoloring (see BlipEffects) through the filter its primitives make, as the HTML's filter of them.
            using var recolored = fill.Filter != null ? Recoloring(fill.Filter) : null;
            if (fill.Filter != null && recolored == null)
                statistics.Effects++;
            canvas.Save();
            canvas.ClipPath(path, antialias: true);
            if (recolored != null)
                canvas.SaveLayer(recolored);
            Picture(fill.Image, SKRect.Create((float)x, (float)y, (float)fullW, (float)fullH), null);
            if (recolored != null)
                canvas.Restore();
            canvas.Restore();
        }

        /// <summary>The pictures recolored so far, by picture and filter primitives.</summary>
        readonly Dictionary<(SKImage, string), SKImage?> recolorings = [];

        /// <summary>A picture through its recoloring (its filter primitives, see BlipEffects), made once; null where they do not draw.</summary>
        SKImage? Recolored(SKImage image, string primitives)
        {
            if (recolorings.TryGetValue((image, primitives), out var made))
                return made;
            using var paint = Recoloring(primitives);
            using var surface = paint != null ? SKSurface.Create(new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Premul)) : null;
            if (surface != null)
            {
                surface.Canvas.SaveLayer(paint);
                surface.Canvas.DrawImage(image, 0, 0);
                surface.Canvas.Restore();
                made = surface.Snapshot();
            }
            return recolorings[(image, primitives)] = made;
        }

        /// <summary>A layer's paint drawing a picture's recoloring, from its filter primitives (see BlipEffects), or null.</summary>
        static SKPaint? Recoloring(string primitives)
        {
            try
            {
                return SvgDrawing.Filter(System.Xml.Linq.XElement.Parse($"<filter xmlns=\"http://www.w3.org/2000/svg\">{primitives}</filter>")) is { } filter
                    ? new SKPaint { ImageFilter = filter } : null;
            }
            catch (System.Xml.XmlException)
            {
                return null;
            }
        }

        /// <summary>A run of slide text from its glyphs' outlines in pixels (see GlyphRunItem), filled with its paint over the paint's box.</summary>
        void GlyphRun(GlyphRunItem run)
        {
            using var path = new SKPath();
            var origin = SKMatrix.CreateTranslation((float)run.X, (float)run.Y).PreConcat(SKMatrix.CreateScale((float)run.Scale, (float)run.Scale));
            foreach (var glyph in run.Glyphs)
            {
                if (Outline(run.Font, glyph.Glyph) is not { } outline)
                    continue;
                var placed = origin.PreConcat(SKMatrix.CreateTranslation((float)glyph.X, (float)glyph.Y));
                if (glyph.Stretch is { } k)
                    placed = placed.PreConcat(SKMatrix.CreateScale(1, (float)k));
                path.AddPath(outline, in placed);
            }
            var (bx, by, bw, bh) = run.PaintBox;
            if ((run.Paint is { } paint ? Paint(paint, SKRect.Create((float)bx, (float)by, (float)bw, (float)bh)) : Brush(run.Color)) is { } fill)
            {
                canvas.DrawPath(path, fill);
                fill.Dispose();
            }
            if (run.Outline is { Visible: true } line)
            {
                using var pen = Pen(line);
                canvas.DrawPath(path, pen);
            }
        }

        /// <summary>
        /// Slide text the browser sets (see SvgTextItem): its glyphs shaped as the browser shapes them, every cluster at
        /// its place, right-to-left text from its right end; a plain underline and strikethrough from the font's metrics.
        /// </summary>
        void SvgText(SvgTextItem item)
        {
            var font = item.Font;
            var glyphs = font.Glyphs(item.Text, item.Kerning, item.RightToLeft);
            if (glyphs.Length == 0 || item.Positions.Length == 0)
                return;
            var size = item.Size;
            var places = new double[glyphs.Length];
            if (!item.RightToLeft)
            {
                var (cluster, x, inCluster) = (-1, 0.0, 0.0);
                for (var i = 0; i < glyphs.Length; i++)
                {
                    if (glyphs[i].Cluster != cluster)
                    {
                        cluster = glyphs[i].Cluster;
                        (x, inCluster) = (item.Positions[Math.Min(cluster, item.Positions.Length - 1)], 0);
                    }
                    places[i] = x + inCluster + glyphs[i].OffsetX * size;
                    inCluster += glyphs[i].Advance * size;
                }
            }
            else
            {
                // Right-to-left text goes whole to the left of its right end, the x the HTML gives the browser (Positions
                // hold its characters' places, the first character's at the right).
                var rightEnd = Regex.Match(item.Placement, "x=\"([^\"]+)\"") is { Success: true } end
                    && double.TryParse(end.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var given)
                    ? given : item.Positions.Min() + item.Width;
                var x = rightEnd - glyphs.Sum(g => (g.Advance + g.Kerning) * size);
                for (var i = 0; i < glyphs.Length; i++)
                {
                    places[i] = x + glyphs[i].OffsetX * size;
                    x += (glyphs[i].Advance + glyphs[i].Kerning) * size;
                }
            }
            using var fill = Brush(item.Color);
            if (Typeface(font) is { } typeface)
            {
                using var face = new SKFont(typeface, (float)size)
                {
                    Subpixel = true, LinearMetrics = true, Hinting = SKFontHinting.None, Edging = SKFontEdging.Antialias,
                    // The browser slants italic of a font without an italic face and emboldens bold of one without a bold face.
                    SkewX = item.Italic && !typeface.IsItalic ? -0.21f : 0, Embolden = item.Bold && !typeface.IsBold,
                };
                using var builder = new SKTextBlobBuilder();
                var run = builder.AllocatePositionedRun(face, glyphs.Length);
                var ids = run.Glyphs;
                var points = run.Positions;
                for (var i = 0; i < glyphs.Length; i++)
                {
                    ids[i] = (ushort)glyphs[i].Id;
                    points[i] = new SKPoint((float)places[i], (float)(item.Y - glyphs[i].OffsetY * size));
                }
                using var blob = builder.Build();
                if (blob != null)
                    canvas.DrawText(blob, 0, 0, fill);
            }
            else
            {
                var scale = size / font.UnitsPerEm;
                for (var i = 0; i < glyphs.Length; i++)
                {
                    if (Outline(font, glyphs[i].Id) is not { } path)
                        continue;
                    canvas.Save();
                    canvas.Translate((float)places[i], (float)(item.Y - glyphs[i].OffsetY * size));
                    canvas.Scale((float)scale);
                    canvas.DrawPath(path, fill);
                    canvas.Restore();
                }
            }
            var (left, right) = (places[0], item.RightToLeft ? item.Positions[0] + item.Width : places[^1] + glyphs[^1].Advance * size);
            if (item.Underline)
                canvas.DrawRect(SKRect.Create((float)left, (float)(item.Y + font.UnderlinePosition * size), (float)(right - left), (float)Math.Max(1, font.UnderlineThickness * size)), fill);
            if (item.Strike)
                canvas.DrawRect(SKRect.Create((float)left, (float)(item.Y - font.StrikeoutPosition * size), (float)(right - left), (float)Math.Max(1, font.StrikeoutThickness * size)), fill);
        }

        /// <summary>
        /// Items recorded as a picture in this canvas's coordinates (bounded by what they draw), for drawing them through
        /// an image filter or more than once.
        /// </summary>
        SKPicture Record(List<SceneItem> items)
        {
            using var recorder = new SKPictureRecorder();
            // With a bounding box hierarchy the picture's bounds shrink to what it draws.
            var recording = recorder.BeginRecording(SKRect.Create(-1e6f, -1e6f, 2e6f, 2e6f), useRTree: true);
            // The recording starts at this canvas's origin; patterns still align to the page.
            var at = page.TryInvert(out var inverse) ? inverse.PreConcat(canvas.TotalMatrix) : canvas.TotalMatrix;
            var origin = at.TryInvert(out var back) ? back : SKMatrix.Identity;
            new Drawer(recording, statistics, origin, pictures, glyphs, definitions, exactDashes, pixels: OnPixels).Draw(items);
            return recorder.EndRecording();
        }

        /// <summary>Items drawn through what an effect list draws (see Effects), as they are without one.</summary>
        void Effected(EffectModel? effects, List<SceneItem> items)
        {
            if (effects == null)
            {
                Draw(items);
                return;
            }
            using var picture = Record(items);
            Effects(effects, picture);
        }

        /// <summary>
        /// A picture drawn with an effect list (see EffectModel), as SVG's filter primitives draw it (see SvgEffects.Filter):
        /// its shadow and glow behind it, the picture itself, its inner shadow on it. The picture goes through a filter
        /// only where it is blurred or its edges are softened, so that its text and pictures stay what they are.
        /// </summary>
        void Effects(EffectModel model, SKPicture picture)
        {
            void Through(SKImageFilter? filter)
            {
                if (filter == null)
                    return;
                using var paint = new SKPaint { ImageFilter = filter };
                canvas.DrawPicture(picture, paint);
            }
            Through(Outer(model));
            if (model.Source)
            {
                if (Own(model) is { } own)
                    Through(own);
                else
                    canvas.DrawPicture(picture);
            }
            Through(Inner(model));
        }

        /// <summary>The filter of an effect list's shadow and glow, merged in this order; null without them.</summary>
        static SKImageFilter? Outer(EffectModel model)
        {
            var layers = new List<SKImageFilter>();
            if (model.Shadow is { } shadow)
                layers.Add(SKImageFilter.CreateDropShadowOnly((float)shadow.Dx, (float)shadow.Dy, (float)shadow.Sigma, (float)shadow.Sigma, SkiaScene.Color(shadow.Color)));
            if (model.Glow is { } glow)
            {
                // The silhouette made opaque wherever it is not clear, grown by four rectangles whose corners lie on a circle.
                var solid = SKImageFilter.CreateColorFilter(SKColorFilter.CreateColorMatrix([0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 255, 0]));
                var grown = new SKImageFilter[4];
                for (var k = 0; k < 4; k++)
                {
                    var angle = (11.25 + 22.5 * k) * Math.PI / 180;
                    grown[k] = SKImageFilter.CreateDilate((float)(glow.Dilate * Math.Cos(angle)), (float)(glow.Dilate * Math.Sin(angle)), solid);
                }
                layers.Add(Colored(glow.Color, SKImageFilter.CreateBlur((float)glow.Sigma, (float)glow.Sigma, SKImageFilter.CreateMerge(grown))));
            }
            return layers.Count switch { 0 => null, 1 => layers[0], _ => SKImageFilter.CreateMerge([.. layers]) };
        }

        /// <summary>The filter of an effect list's blur and soft edges on the drawing itself; null without them.</summary>
        static SKImageFilter? Own(EffectModel model)
        {
            if (model.Blur == null && model.SoftEdge == null)
                return null;
            var own = model.Blur is { } blur ? SKImageFilter.CreateBlur((float)blur, (float)blur) : SKImageFilter.CreateOffset(0, 0);
            if (model.SoftEdge is { } soft)
            {
                var edge = SKImageFilter.CreateBlur((float)soft.Sigma, (float)soft.Sigma, SKImageFilter.CreateErode((float)soft.Erode, (float)soft.Erode, Alpha()));
                own = SKImageFilter.CreateBlendMode(SKBlendMode.SrcIn, edge, own);
            }
            return own;
        }

        /// <summary>The filter of an effect list's inner shadow: what lies outside the silhouette, blurred, moved and colored, kept within it.</summary>
        static SKImageFilter? Inner(EffectModel model)
        {
            if (model.InnerShadow is not { } inner)
                return null;
            var outside = SKImageFilter.CreateBlendMode(SKBlendMode.SrcOut, Alpha(), SKImageFilter.CreateShader(SKShader.CreateColor(SKColors.Black)));
            var moved = SKImageFilter.CreateOffset((float)inner.Dx, (float)inner.Dy, SKImageFilter.CreateBlur((float)inner.Sigma, (float)inner.Sigma, outside));
            return SKImageFilter.CreateBlendMode(SKBlendMode.SrcIn, Alpha(), Colored(inner.Color, moved));
        }

        /// <summary>The drawing's alpha alone, in black (SVG's SourceAlpha).</summary>
        static SKImageFilter Alpha() =>
            SKImageFilter.CreateColorFilter(SKColorFilter.CreateColorMatrix([0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0]));

        /// <summary>A color where the input is, as opaque as it is.</summary>
        static SKImageFilter Colored(RgbColor color, SKImageFilter input) =>
            SKImageFilter.CreateColorFilter(SKColorFilter.CreateBlendMode(SkiaScene.Color(color), SKBlendMode.SrcIn), input);

        /// <summary>
        /// A reflection of what a picture holds: its copy under the reflection's transform, blurred, faded by a white
        /// gradient's alpha along the fade (see SvgEffects.ReflectionOf).
        /// </summary>
        void Reflection(ReflectionModel reflection, SKPicture picture)
        {
            var transform = Matrix(reflection.Transform);
            var bounds = transform.MapRect(picture.CullRect);
            bounds.Inflate((float)(3 * reflection.Sigma + 1), (float)(3 * reflection.Sigma + 1));
            canvas.SaveLayer(bounds, null);
            using (var blur = reflection.Blurred ? new SKPaint { ImageFilter = SKImageFilter.CreateBlur((float)reflection.Sigma, (float)reflection.Sigma) } : null)
                canvas.DrawPicture(picture, in transform, blur);
            using var fade = new SKPaint
            {
                Shader = SKShader.CreateLinearGradient(new SKPoint((float)reflection.From.X, (float)reflection.From.Y), new SKPoint((float)reflection.To.X, (float)reflection.To.Y),
                    [SKColors.White.WithAlpha((byte)Math.Round(Math.Clamp(reflection.FromAlpha, 0, 1) * 255)), SKColors.White.WithAlpha((byte)Math.Round(Math.Clamp(reflection.ToAlpha, 0, 1) * 255))],
                    SKShaderTileMode.Clamp),
                BlendMode = SKBlendMode.DstIn,
            };
            canvas.DrawRect(bounds, fade);
            canvas.Restore();
        }

        /// <summary>A decoded picture: an image, or an SVG picture's drawing.</summary>
        internal readonly record struct Decoded(SKImage? Image, SvgDrawing? Drawing);

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
                    return Pattern(pattern);
                case SlideFill slide:
                    return Paint(slide.Fill, bounds);
                case PictureFill { Tiling: { } tile, NaturalSize: { W: > 0, H: > 0 } size } picture when Picture(picture.Image).Image is { } image:
                {
                    // Tiles of the picture's own size times the scale, aligned to a corner, an edge or the middle of
                    // what is filled and offset; flips mirror every other tile (see SvgPaint.Tiled).
                    var (tw, th) = (size.W * tile.Sx, size.H * tile.Sy);
                    var (w, h) = (bounds.Width, bounds.Height);
                    var ax = tile.Align switch { "t" or "ctr" or "b" => (w - tw) / 2, "tr" or "r" or "br" => w - tw, _ => 0 };
                    var ay = tile.Align switch { "l" or "ctr" or "r" => (h - th) / 2, "bl" or "b" or "br" => h - th, _ => 0 };
                    var placed = SKMatrix.CreateScale((float)(tw / image.Width), (float)(th / image.Height))
                        .PostConcat(SKMatrix.CreateTranslation((float)(bounds.Left + ax + tile.Tx * PxPerPt), (float)(bounds.Top + ay + tile.Ty * PxPerPt)));
                    // Its recoloring (see BlipEffects) on the picture itself, every tile alike.
                    var recolored = picture.Filter != null ? Recolored(image, picture.Filter) : null;
                    if (picture.Filter != null && recolored == null)
                        statistics.Effects++;
                    return new SKPaint
                    {
                        Shader = (recolored ?? image).ToShader(tile.Flip is "x" or "xy" ? SKShaderTileMode.Mirror : SKShaderTileMode.Repeat,
                            tile.Flip is "y" or "xy" ? SKShaderTileMode.Mirror : SKShaderTileMode.Repeat, Sampling, placed),
                        Style = SKPaintStyle.Fill, IsAntialias = true,
                    };
                }
                case PictureFill:
                    statistics.Pictures++;
                    return null;
                default:
                    return null;
            }
        }

        /// <summary>
        /// A DrawingML outline as a pen, as SvgPaint.Stroke strokes it: its width, at least half a pixel, caps (none on
        /// the dashes of a square-capped line, whose ends get theirs apart, see Dashes.SquareEnds), joins, and dashes in
        /// units of its width (whole pixels unless exactDashes), round caps within them, on a closed figure from where
        /// its pattern starts.
        /// </summary>
        SKPaint Pen(Outline outline, bool closed = false)
        {
            var width = Math.Max(Units.ToPx(outline.WidthEmu), 0.5);
            var pen = Pen(Fill.MainColor(outline.Fill) ?? RgbColor.Black, (float)width);
            var dash = Dashes.Pattern(outline);
            pen.StrokeCap = outline.Cap switch { "rnd" => SKStrokeCap.Round, "sq" when dash is not { Length: > 0 } => SKStrokeCap.Square, _ => SKStrokeCap.Butt };
            pen.StrokeJoin = outline.Join switch { "round" => SKStrokeJoin.Round, "bevel" => SKStrokeJoin.Bevel, _ => SKStrokeJoin.Miter };
            pen.StrokeMiter = (float)Math.Max(1, outline.MiterLimit);
            if (dash is { Length: > 0 })
            {
                var round = outline.Cap == "rnd";
                var unit = exactDashes && outline.WidthEmu > 0 ? Units.ToPx(outline.WidthEmu) : Dashes.Unit(width);
                var lengths = new float[dash.Length + dash.Length % 2];
                for (var i = 0; i < dash.Length; i += 2)
                {
                    var (d, space) = (dash[i], i + 1 < dash.Length ? dash[i + 1] : 0);
                    (lengths[i], lengths[i + 1]) = round ? ((float)(Math.Max(0, d - 1) * unit), (float)((space + 1) * unit)) : ((float)(d * unit), (float)(space * unit));
                }
                var period = lengths.Sum();
                if (period > 0)
                {
                    var offset = round && closed ? -unit / 2 : 0;
                    pen.PathEffect = SKPathEffect.CreateDash(lengths, (float)(((offset % period) + period) % period));
                }
            }
            return pen;
        }

        static readonly Regex Opacity = new(" opacity=\"([0-9.]+)\"", RegexOptions.Compiled);

        /// <summary>A group: its opacity where it has one (a 3D face's) and its isolation (a fill overlay's), its other attributes not.</summary>
        void Group(SvgGroupItem group)
        {
            if (group.ColorMatrix is { Length: 20 } matrix)
            {
                // A face's light: the group drawn on a layer its color matrix colors.
                using var light = new SKPaint { ColorFilter = SKColorFilter.CreateColorMatrix([.. matrix.Select(v => (float)v)]) };
                canvas.SaveLayer(light);
                Group(group with { ColorMatrix = null });
                canvas.Restore();
                return;
            }
            if (group.Transform != null || group.Effects != null)
            {
                canvas.Save();
                if (group.Transform is { } transform)
                    Concat(Matrix(transform));
                Effected(group.Effects, [group with { Transform = null, Effects = null }]);
                canvas.Restore();
                return;
            }
            // A group of its own, which what blends within it blends with.
            if (group.Attributes.Contains("isolation:isolate", StringComparison.Ordinal))
            {
                canvas.SaveLayer();
                Group(group with { Attributes = group.Attributes.Replace("isolation:isolate", "", StringComparison.Ordinal) });
                canvas.Restore();
                return;
            }
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

        /// <summary>
        /// A preset pattern (or a pattern of its own bits) as tiles of 8 × 8 bits, a bit a pixel (Cell pixels for Word's
        /// VML patterns), in the foreground where set, aligned to the page as PowerPoint aligns them to the slide (see
        /// SvgPaint.Pattern).
        /// </summary>
        SKPaint Pattern(PatternFill pattern)
        {
            var tile = pattern.Bits ?? PatternPresets.Tiles.GetValueOrDefault(pattern.Preset) ?? PatternPresets.Tiles["pct50"];
            using var bitmap = new SKBitmap(new SKImageInfo(8, 8, SKColorType.Rgba8888, SKAlphaType.Premul));
            var (fore, back) = (SkiaScene.Color(pattern.Foreground), SkiaScene.Color(pattern.Background));
            for (var row = 0; row < 8; row++)
                for (var column = 0; column < 8; column++)
                    bitmap.SetPixel(column, row, (tile[row] & (0x80 >> column)) != 0 ? fore : back);
            // Where the drawing's origin lies on the page gives the tiles' phase.
            var onPage = page.Invert().PreConcat(canvas.TotalMatrix);
            var period = 8 * pattern.Cell;
            double Phase(double v) => ((-v % period) + period) % period;
            var placed = SKMatrix.CreateScale((float)pattern.Cell, (float)pattern.Cell)
                .PostConcat(SKMatrix.CreateTranslation((float)Phase(onPage.TransX), (float)Phase(onPage.TransY)));
            using var image = SKImage.FromBitmap(bitmap);
            return new SKPaint
            {
                Shader = image.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, new SKSamplingOptions(SKFilterMode.Nearest), placed),
                Style = SKPaintStyle.Fill, IsAntialias = true,
            };
        }

        void Picture(PictureItem item)
        {
            // Its recoloring through the filter its attribute names among the page's definitions.
            using var recolored = item.Filter.Length > 0 && Regex.Match(item.Filter, @"url\(#([^)]+)\)") is { Success: true } named
                && Definition(named.Groups[1].Value) is { Name.LocalName: "filter" } element && SvgDrawing.Filter(element) is { } filter
                ? new SKPaint { ImageFilter = filter } : null;
            if (item.Filter.Length > 0 && recolored == null)
                statistics.Effects++;
            if (recolored != null)
                canvas.SaveLayer(recolored);
            Picture(item.Source, SKRect.Create((float)item.X, (float)item.Y, (float)item.Width, (float)item.Height), item.ClipPath);
            if (recolored != null)
                canvas.Restore();
        }

        /// <summary>A picture stretched over a rectangle, cut at a path where it has one (SVG path data).</summary>
        void Picture(string source, SKRect rect, string? clipPath)
        {
            var decoded = Picture(source);
            if (decoded is { Image: null, Drawing: null })
            {
                statistics.Pictures++;
                return;
            }
            canvas.Save();
            using (var clip = clipPath != null ? SKPath.ParseSvgPathData(clipPath) : null)
            {
                if (clip != null)
                    canvas.ClipPath(clip, antialias: true);
            }
            if (decoded.Image is { } image)
            {
                // On pixels, squeezed far more along one axis than along the other, evened first (see SvgDrawing.Evened).
                using var evened = OnPixels is { } onPixels
                    ? SvgDrawing.Evened(image, onPixels.PreConcat(SKMatrix.CreateScaleTranslation(rect.Width / image.Width, rect.Height / image.Height, rect.Left, rect.Top)))
                    : null;
                canvas.DrawImage(evened ?? image, rect, Sampling);
            }
            else if (decoded.Drawing is { Size: { Width: > 0, Height: > 0 } size } drawing)
            {
                // An SVG picture of a size (a viewBox, or a width and a height) stretched over the rectangle, as the
                // HTML's image with preserveAspectRatio none; one without a size in its own pixels from the rectangle's
                // corner, cut at it (as the browser draws it: LibreOffice's extras-uiwriter/tdf157131).
                canvas.Translate(rect.Left, rect.Top);
                if (drawing.Sized)
                    canvas.Scale(rect.Width / size.Width, rect.Height / size.Height);
                else
                    canvas.ClipRect(SKRect.Create(rect.Width, rect.Height));
                canvas.DrawPicture(drawing.Picture);
            }
            canvas.Restore();
        }

        Decoded Picture(string uri)
        {
            if (!pictures.TryGetValue(uri, out var decoded))
                pictures[uri] = decoded = Decode(uri);
            return decoded;
        }

        /// <summary>
        /// A picture of a data URI: an image in a format Skia decodes, or an SVG picture (a document's own, a metafile as
        /// the converter writes it, see SvgDrawing); neither for other formats.
        /// </summary>
        static Decoded Decode(string uri)
        {
            if (!uri.StartsWith("data:", StringComparison.Ordinal) || uri.IndexOf(',') is not (> 0 and var comma)
                || !uri.AsSpan(0, comma).EndsWith(";base64", StringComparison.Ordinal))
                return default;
            try
            {
                var data = Convert.FromBase64String(uri[(comma + 1)..]);
                if (!uri.AsSpan(5).StartsWith("image/svg+xml", StringComparison.Ordinal))
                    return new Decoded(SKImage.FromEncodedData(data), null);
                return SvgDrawing.Load(data) is { } drawing ? new Decoded(null, drawing) : default;
            }
            catch (FormatException)
            {
                return default;
            }
        }
    }
}
