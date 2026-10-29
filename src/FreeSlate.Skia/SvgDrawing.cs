using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using SkiaSharp;

namespace FreeSlate.Skia;

/// <summary>
/// An SVG picture drawn with Skia (catalog: engine.output): a document's own SVG pictures and its metafiles as the
/// converter writes them (see Metafiles.SvgWriter). It reads SVG 1.1's structure and painting (svg, g, a, switch, use,
/// defs, symbol; path, rect, circle, ellipse, line, polyline, polygon; text and tspan with lists of places; image;
/// clipPath; linear and radial gradients; patterns), presentation attributes, style attributes and style sheets of
/// simple selectors (element, class, id); masks by luminance; blending (mix-blend-mode, isolation); filters of the
/// primitives Office's effects are made of (see Painter.Graph). Markers and other filters are left out, what they apply
/// to drawn as it is.
/// </summary>
public sealed class SvgDrawing
{
    /// <summary>The picture in the SVG's viewport: from (0, 0) to its size.</summary>
    public SKPicture Picture { get; }

    /// <summary>The viewport's size in pixels: the SVG's width and height, else its viewBox's, else 300 × 150.</summary>
    public SKSize Size { get; }

    /// <summary>Whether the SVG has a size of its own (a viewBox, or a width and a height) to stretch over a picture's box.</summary>
    public bool Sized { get; }

    SvgDrawing(SKPicture picture, SKSize size, bool sized) => (Picture, Size, Sized) = (picture, size, sized);

    /// <summary>
    /// SVG markup that a scene still holds (see MarkupItem; a chart's, a table's) recorded in the coordinates it stands
    /// in, its text of class sel transparent as the HTML's style sheet has it; what it names by id and does not define
    /// itself comes from <paramref name="definitions"/> (the page's definitions, the document's glyph outlines). Null
    /// for markup that is no SVG. With <paramref name="pixels"/>, the matrix it is drawn under onto pixels (not into a
    /// PDF): pictures squeezed far more along one axis than along the other are evened for it (see Evened). With
    /// <paramref name="typefaces"/>, the typeface text is drawn in, given the one found for its family, the glyphs it draws
    /// and its text (into a PDF, the font cut down to what the PDF draws).
    /// </summary>
    public static SKPicture? Fragment(string markup, Func<string, XElement?>? definitions = null, SKMatrix? pixels = null,
        Func<SKTypeface, IEnumerable<uint>, string, SKTypeface>? typefaces = null)
    {
        XDocument document;
        try
        {
            using var reader = XmlReader.Create(new StringReader($"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\">{markup}</svg>"),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null });
            document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException)
        {
            return null;
        }
        if (document.Root!.Elements().Any(e => e.Name.Namespace == Svg && e.Name.LocalName is "div" or "section" or "span" or "p"))
            return null;
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(SKRect.Create(-1e6f, -1e6f, 2e6f, 2e6f), useRTree: true);
        var painter = new Painter(document, definitions, "text.sel{fill-opacity:0}", pixels, typefaces);
        painter.Children(document.Root!, canvas, Style.Initial);
        return recorder.EndRecording();
    }

    static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    /// <summary>An SVG filter element as an image filter (see Painter.Graph), or null where it holds what that does not draw.</summary>
    public static SKImageFilter? Filter(XElement filter) => Painter.Graph(filter);

    /// <summary>
    /// The shader of an SVG paint server (a gradient or a pattern) over a box, as it paints an element there, what it
    /// refers to found among <paramref name="definitions"/>; null where it is none.
    /// </summary>
    public static SKShader? Shader(XElement server, SKRect bounds, double opacity, Func<string, XElement?> definitions) =>
        new Painter(new XDocument(new XElement(Svg + "svg")), definitions, null).Server(server, bounds, opacity);

    /// <summary>
    /// The fill and the stroke that SVG presentation attributes give (the scene's paints of text: a color, a gradient or
    /// a pattern among <paramref name="definitions"/>, opacities, a stroke's width, joins and dashes) over a box; null
    /// for one that paints nothing. False where the attributes do not parse.
    /// </summary>
    public static bool Paints(string attributes, SKRect bounds, Func<string, XElement?> definitions, out SKPaint? fill, out SKPaint? stroke)
    {
        (fill, stroke) = (null, null);
        XElement element;
        try
        {
            element = XElement.Parse($"<text xmlns=\"http://www.w3.org/2000/svg\"{attributes}/>");
        }
        catch (XmlException)
        {
            return false;
        }
        (fill, stroke) = new Painter(new XDocument(new XElement(Svg + "svg")), definitions, null).Paints(element, bounds);
        return true;
    }

    /// <summary>
    /// A blend mode as CSS names it (mix-blend-mode, feBlend's mode) as Skia's, which blends as the W3C's compositing
    /// defines it; null for one it does not name.
    /// </summary>
    internal static SKBlendMode? BlendMode(string? mode) => mode?.Trim() switch
    {
        "normal" => SKBlendMode.SrcOver,
        "multiply" => SKBlendMode.Multiply,
        "screen" => SKBlendMode.Screen,
        "overlay" => SKBlendMode.Overlay,
        "darken" => SKBlendMode.Darken,
        "lighten" => SKBlendMode.Lighten,
        "color-dodge" => SKBlendMode.ColorDodge,
        "color-burn" => SKBlendMode.ColorBurn,
        "hard-light" => SKBlendMode.HardLight,
        "soft-light" => SKBlendMode.SoftLight,
        "difference" => SKBlendMode.Difference,
        "exclusion" => SKBlendMode.Exclusion,
        "hue" => SKBlendMode.Hue,
        "saturation" => SKBlendMode.Saturation,
        "color" => SKBlendMode.Color,
        "luminosity" => SKBlendMode.Luminosity,
        "plus-lighter" => SKBlendMode.Plus,
        _ => null,
    };

    /// <summary>
    /// A stroke with crisp edges (shape-rendering crispEdges) as the browser draws it: without antialiasing, and on pixels
    /// (under <paramref name="pixels"/>; null into a PDF) one thinner than a pixel as a line a pixel wide, as opaque as the
    /// stroke covers the pixel: without antialiasing so thin a stroke misses the pixels' centers and vanishes.
    /// </summary>
    internal static void Crisp(SKPaint stroke, SKMatrix? pixels)
    {
        stroke.IsAntialias = false;
        if (pixels is not { } matrix || stroke.StrokeWidth <= 0 || matrix.Persp0 != 0 || matrix.Persp1 != 0)
            return;
        var (across, down) = (matrix.MapVector(stroke.StrokeWidth, 0).Length, matrix.MapVector(0, stroke.StrokeWidth).Length);
        if (across > 1 || down > 1)
            return;
        stroke.StrokeWidth = 0;
        stroke.Color = stroke.Color.WithAlpha((byte)Math.Round(stroke.Color.Alpha * (across + down) / 2));
    }

    /// <summary>
    /// A raster picture as it is drawn onto pixels under <paramref name="pixels"/> (from its own pixels there), averaged
    /// down along the axis it is squeezed more along where that one is squeezed to under half and over twice as much as
    /// the other: Skia's mipmaps take their level from the axis squeezed most and so blur the other, which browsers keep
    /// sharp. The squeezed axis then takes the other's scale, at most a pixel a pixel. Null where it is drawn as it is.
    /// </summary>
    internal static SKImage? Evened(SKImage image, SKMatrix pixels)
    {
        var (sx, sy) = Scales(pixels);
        var (low, high) = (Math.Min(sx, sy), Math.Max(sx, sy));
        if (!(low < 0.5 && low < high / 2) || pixels.Persp0 != 0 || pixels.Persp1 != 0)
            return null;
        var target = Math.Min(high, 1);
        var (width, height) = ((int)Math.Ceiling(image.Width * Math.Min(1, sx / target)), (int)Math.Ceiling(image.Height * Math.Min(1, sy / target)));
        return width < image.Width || height < image.Height ? Averaged(image, Math.Max(1, width), Math.Max(1, height)) : null;
    }

    /// <summary>The scales a matrix gives along the x and the y axis it maps: how long their units come out.</summary>
    static (double X, double Y) Scales(SKMatrix m) =>
        (Math.Sqrt((double)m.ScaleX * m.ScaleX + (double)m.SkewY * m.SkewY), Math.Sqrt((double)m.SkewX * m.SkewX + (double)m.ScaleY * m.ScaleY));

    /// <summary>Dots of a 300-dpi PDF in a CSS pixel.</summary>
    const double PdfDots = 300 / 96.0;

    /// <summary>
    /// A pattern's tile (a picture of <paramref name="size"/> units) as pixels, 300 dots an inch where its units stand
    /// under <paramref name="matrix"/> in CSS pixels, at most 2048 a side; null where Skia makes no surface.
    /// </summary>
    static SKImage? Tile(SKPicture picture, SKSize size, SKMatrix matrix)
    {
        var (sx, sy) = Scales(matrix);
        var (width, height) = ((int)Math.Clamp(Math.Ceiling(size.Width * sx * PdfDots), 1, 2048), (int)Math.Clamp(Math.Ceiling(size.Height * sy * PdfDots), 1, 2048));
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (surface == null)
            return null;
        surface.Canvas.Scale(width / size.Width, height / size.Height);
        surface.Canvas.DrawPicture(picture);
        return surface.Snapshot();
    }

    /// <summary>A raster picture averaged down to a size no larger along either axis: a box filter on premultiplied color.</summary>
    static SKImage? Averaged(SKImage image, int width, int height)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var source = new SKBitmap(info);
        if (!image.ReadPixels(info, source.GetPixels(), info.RowBytes, 0, 0))
            return null;
        var pixels = source.GetPixelSpan();
        var (fx, fy) = ((double)image.Width / width, (double)image.Height / height);
        var sums = new double[width * height * 4];
        var row = new double[width * 4];
        for (var y = 0; y < image.Height; y++)
        {
            // Along the row every pixel goes to the columns it overlaps, by how much of each it covers.
            Array.Clear(row);
            var line = pixels.Slice(y * info.RowBytes, image.Width * 4);
            for (var x = 0; x < image.Width; x++)
            {
                var (from, to) = (x / fx, (x + 1) / fx);
                for (var column = (int)from; column < width && column < to; column++)
                {
                    var share = Math.Min(to, column + 1) - Math.Max(from, column);
                    for (var c = 0; c < 4; c++)
                        row[column * 4 + c] += share * line[x * 4 + c];
                }
            }
            // Down the rows the same.
            var (top, bottom) = (y / fy, (y + 1) / fy);
            for (var r = (int)top; r < height && r < bottom; r++)
            {
                var share = Math.Min(bottom, r + 1) - Math.Max(top, r);
                var at = r * width * 4;
                for (var i = 0; i < row.Length; i++)
                    sums[at + i] += share * row[i];
            }
        }
        var bytes = new byte[sums.Length];
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = (byte)Math.Clamp(Math.Round(sums[i]), 0, 255);
        return SKImage.FromPixelCopy(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul), bytes, width * 4);
    }

    /// <summary>Reads and records an SVG picture, or null where it is not one; its text in <paramref name="typefaces"/>' (see Fragment).</summary>
    public static SvgDrawing? Load(byte[] data, Func<SKTypeface, IEnumerable<uint>, string, SKTypeface>? typefaces = null)
    {
        XDocument document;
        try
        {
            using var reader = XmlReader.Create(new MemoryStream(data), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null });
            document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException)
        {
            return null;
        }
        if (document.Root is not { } root || root.Name.LocalName != "svg")
            return null;
        var viewBox = Numbers(root.Attribute("viewBox")?.Value);
        var width = Length(root.Attribute("width")?.Value, 0);
        var height = Length(root.Attribute("height")?.Value, 0);
        var hasBox = viewBox is { Length: 4 } && viewBox[2] > 0 && viewBox[3] > 0;
        var sized = hasBox || width > 0 && height > 0;
        // A viewBox alone gives the size, one of width and height the other through its aspect ratio.
        var size = new SKSize(300, 150);
        if (width > 0 && height > 0)
            size = new SKSize((float)width, (float)height);
        else if (hasBox && viewBox is [_, _, var boxWidth, var boxHeight])
            size = new SKSize((float)(width > 0 ? width : height > 0 ? height * boxWidth / boxHeight : boxWidth),
                (float)(height > 0 ? height : width > 0 ? width * boxHeight / boxWidth : boxHeight));
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(SKRect.Create(size));
        var painter = new Painter(document, null, null, typefaces: typefaces);
        painter.Viewport(root, canvas, size.Width, size.Height, Style.Initial, outermost: true);
        return new SvgDrawing(recorder.EndRecording(), size, sized);
    }

    static readonly XNamespace Xlink = "http://www.w3.org/1999/xlink";

    /// <summary>The computed style properties that are inherited, and those of the element itself.</summary>
    sealed record Style
    {
        public static readonly Style Initial = new();

        public string Fill { get; init; } = "black";
        public double FillOpacity { get; init; } = 1;
        public string FillRule { get; init; } = "nonzero";
        public string Stroke { get; init; } = "none";
        public double StrokeOpacity { get; init; } = 1;
        public double StrokeWidth { get; init; } = 1;
        public string LineCap { get; init; } = "butt";
        public string LineJoin { get; init; } = "miter";
        public double MiterLimit { get; init; } = 4;
        public double[]? Dashes { get; init; }
        public double DashOffset { get; init; }
        public string ClipRule { get; init; } = "nonzero";
        public string Color { get; init; } = "black";
        public string FontFamily { get; init; } = "sans-serif";
        public double FontSize { get; init; } = 16;
        public bool Bold { get; init; }
        public bool Italic { get; init; }
        public string Anchor { get; init; } = "start";
        public string Decoration { get; init; } = "none";
        public bool Visible { get; init; } = true;
        public bool Pixelated { get; init; }
        public bool Crisp { get; init; }
        public bool NonScalingStroke { get; init; }

        // Not inherited.
        public double Opacity { get; init; } = 1;
        public bool Displayed { get; init; } = true;
        public string? ClipPath { get; init; }
        public string? Mask { get; init; }
        public string? Filter { get; init; }
        public SKBlendMode Blend { get; init; } = SKBlendMode.SrcOver;
        public bool Isolated { get; init; }
        public double StopOpacity { get; init; } = 1;
        public string StopColor { get; init; } = "black";
    }

    sealed class Painter
    {
        readonly Dictionary<string, XElement> ids = [];
        readonly List<(string Selector, int Specificity, int Order, List<(string Name, string Value)> Declarations)> rules = [];
        readonly Func<string, XElement?>? definitions;

        /// <summary>
        /// The matrix under which what the canvas in hand records lands on pixels (a pattern's tile in turn under its
        /// element's and its own), or null where it lands in a PDF.
        /// </summary>
        SKMatrix? outer;

        /// <summary>How many patterns' tiles are being recorded within one another.</summary>
        int patterns;

        /// <summary>The typeface text is drawn in, given the one found for its family and what it draws (see Fragment), or null.</summary>
        readonly Func<SKTypeface, IEnumerable<uint>, string, SKTypeface>? typefaces;

        /// <param name="definitions">What the document names by id and does not define itself.</param>
        /// <param name="sheet">A style sheet the document is drawn with before its own.</param>
        /// <param name="pixels">The matrix the drawing lands on pixels under (see Fragment), or null.</param>
        /// <param name="typefaces">The typeface text is drawn in (see Fragment), or null.</param>
        public Painter(XDocument document, Func<string, XElement?>? definitions, string? sheet, SKMatrix? pixels = null,
            Func<SKTypeface, IEnumerable<uint>, string, SKTypeface>? typefaces = null)
        {
            this.definitions = definitions;
            outer = pixels;
            this.typefaces = typefaces;
            var order = 0;
            if (sheet != null)
                Sheet(sheet, ref order);
            foreach (var element in document.Descendants())
            {
                if (element.Attribute("id")?.Value is { Length: > 0 } id)
                    ids.TryAdd(id, element);
                if (element.Name.LocalName == "style")
                    Sheet(element.Value, ref order);
            }
        }

        /// <summary>Reads a style sheet's rules of simple selectors (element, .class, #id, element.class).</summary>
        void Sheet(string css, ref int order)
        {
            css = Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);
            foreach (Match rule in Regex.Matches(css, @"([^{}]+)\{([^}]*)\}"))
            {
                var declarations = Declarations(rule.Groups[2].Value);
                foreach (var raw in rule.Groups[1].Value.Split(','))
                {
                    var selector = raw.Trim();
                    if (selector.Length == 0 || !Regex.IsMatch(selector, @"^[A-Za-z_*][\w-]*$|^[A-Za-z_]?[\w-]*(?:[.#][\w-]+)+$"))
                        continue;
                    var specificity = selector.Count(c => c == '#') * 100 + selector.Count(c => c == '.') * 10 + (char.IsLetter(selector[0]) ? 1 : 0);
                    rules.Add((selector, specificity, order++, declarations));
                }
            }
        }

        static List<(string Name, string Value)> Declarations(string text) =>
            text.Split(';').Select(d => d.Split(':', 2)).Where(p => p.Length == 2)
                .Select(p => (p[0].Trim().ToLowerInvariant(), p[1].Replace("!important", "").Trim())).Where(p => p.Item1.Length > 0).ToList();

        static bool Matches(XElement element, string selector)
        {
            var at = 0;
            while (at < selector.Length && selector[at] is not ('.' or '#'))
                at++;
            var tag = selector[..at];
            if (tag.Length > 0 && tag != "*" && tag != element.Name.LocalName)
                return false;
            foreach (Match part in Regex.Matches(selector[at..], @"([.#])([\w-]+)"))
            {
                if (part.Groups[1].Value == "#" ? element.Attribute("id")?.Value != part.Groups[2].Value
                        : !(element.Attribute("class")?.Value ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(part.Groups[2].Value))
                    return false;
            }
            return true;
        }

        /// <summary>The properties an element sets: its presentation attributes, the style sheet's rules and its style attribute, in that order.</summary>
        IEnumerable<(string Name, string Value)> Properties(XElement element)
        {
            foreach (var attribute in element.Attributes())
            {
                if (attribute.Name.Namespace == XNamespace.None && attribute.Name.LocalName != "style")
                    yield return (attribute.Name.LocalName, attribute.Value.Trim());
            }
            foreach (var rule in rules.Where(r => Matches(element, r.Selector)).OrderBy(r => r.Specificity).ThenBy(r => r.Order))
            {
                foreach (var declaration in rule.Declarations)
                    yield return declaration;
            }
            if (element.Attribute("style")?.Value is { } style)
            {
                foreach (var declaration in Declarations(style))
                    yield return declaration;
            }
        }

        Style Computed(XElement element, Style parent)
        {
            var style = parent with
            {
                Opacity = 1, Displayed = true, ClipPath = null, Mask = null, Filter = null, Blend = SKBlendMode.SrcOver, Isolated = false, StopOpacity = 1,
                StopColor = "black",
            };
            foreach (var (name, value) in Properties(element))
            {
                if (value == "inherit")
                    continue;
                style = name switch
                {
                    "fill" => style with { Fill = value },
                    "fill-opacity" => style with { FillOpacity = Fraction(value) },
                    "fill-rule" => style with { FillRule = value },
                    "stroke" => style with { Stroke = value },
                    "stroke-opacity" => style with { StrokeOpacity = Fraction(value) },
                    "stroke-width" => style with { StrokeWidth = Length(value, style.FontSize) },
                    "stroke-linecap" => style with { LineCap = value },
                    "stroke-linejoin" => style with { LineJoin = value },
                    "stroke-miterlimit" => style with { MiterLimit = Number(value, 4) },
                    "stroke-dasharray" => style with { Dashes = value == "none" ? null : Numbers(value) },
                    "stroke-dashoffset" => style with { DashOffset = Length(value, style.FontSize) },
                    "clip-rule" => style with { ClipRule = value },
                    "color" => style with { Color = value },
                    "font-family" => style with { FontFamily = value },
                    "font-size" => style with { FontSize = FontSize(value, style.FontSize) },
                    "font-weight" => style with { Bold = value is "bold" or "bolder" || int.TryParse(value, out var weight) && weight >= 600 },
                    "font-style" => style with { Italic = value is "italic" or "oblique" },
                    "text-anchor" => style with { Anchor = value },
                    "text-decoration" => style with { Decoration = value },
                    "visibility" => style with { Visible = value == "visible" },
                    "image-rendering" => style with { Pixelated = value is "pixelated" or "optimizeSpeed" or "crisp-edges" },
                    "shape-rendering" => style with { Crisp = value is "crispEdges" or "optimizeSpeed" },
                    "vector-effect" => style with { NonScalingStroke = value == "non-scaling-stroke" },
                    "opacity" => style with { Opacity = Fraction(value) },
                    "display" => style with { Displayed = value != "none" },
                    "clip-path" => style with { ClipPath = value },
                    "mask" => style with { Mask = value },
                    "filter" => style with { Filter = value },
                    "mix-blend-mode" => style with { Blend = BlendMode(value) ?? SKBlendMode.SrcOver },
                    "isolation" => style with { Isolated = value == "isolate" },
                    "stop-opacity" => style with { StopOpacity = Fraction(value) },
                    "stop-color" => style with { StopColor = value },
                    "font" => Font(style, value),
                    _ => style,
                };
            }
            return style;
        }

        /// <summary>The font shorthand's style, weight, size and family.</summary>
        static Style Font(Style style, string value)
        {
            var match = Regex.Match(value, @"^\s*((?:(?:italic|oblique|normal|bold|bolder|lighter|\d00|small-caps)\s+)*)([\d.]+[a-z%]*)(?:/[^\s]+)?\s+(.+)$");
            if (!match.Success)
                return style;
            var words = match.Groups[1].Value;
            return style with
            {
                Italic = words.Contains("italic") || words.Contains("oblique"),
                Bold = words.Contains("bold") || Regex.IsMatch(words, @"\b[6-9]00\b"),
                FontSize = FontSize(match.Groups[2].Value, style.FontSize),
                FontFamily = match.Groups[3].Value,
            };
        }

        /// <summary>An element that establishes a viewport (svg): its viewBox mapped into width × height, cut there unless overflow shows it.</summary>
        public void Viewport(XElement svg, SKCanvas canvas, double width, double height, Style parent, bool outermost)
        {
            var style = Computed(svg, parent);
            if (!style.Displayed)
                return;
            canvas.Save();
            var overflow = Properties(svg).LastOrDefault(p => p.Name == "overflow").Value;
            if (!outermost && overflow is not ("visible" or "auto"))
                canvas.ClipRect(SKRect.Create((float)width, (float)height));
            if (Numbers(svg.Attribute("viewBox")?.Value) is { Length: 4 } box && box[2] > 0 && box[3] > 0)
                canvas.Concat(ViewBox(box, width, height, svg.Attribute("preserveAspectRatio")?.Value));
            Children(svg, canvas, style);
            canvas.Restore();
        }

        /// <summary>The transform that maps a viewBox into a viewport as preserveAspectRatio asks (by default centered, whole).</summary>
        static SKMatrix ViewBox(double[] box, double width, double height, string? aspect)
        {
            var (sx, sy) = (width / box[2], height / box[3]);
            var parts = (aspect ?? "xMidYMid meet").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var align = parts.Length > 0 ? parts[0] : "xMidYMid";
            double tx = 0, ty = 0;
            if (align != "none")
            {
                var scale = parts.Length > 1 && parts[1] == "slice" ? Math.Max(sx, sy) : Math.Min(sx, sy);
                (sx, sy) = (scale, scale);
                var (freeX, freeY) = (width - box[2] * scale, height - box[3] * scale);
                tx = align.Contains("xMid") ? freeX / 2 : align.Contains("xMax") ? freeX : 0;
                ty = align.Contains("YMid") ? freeY / 2 : align.Contains("YMax") ? freeY : 0;
            }
            return SKMatrix.CreateTranslation((float)tx, (float)ty).PreConcat(SKMatrix.CreateScale((float)sx, (float)sy))
                .PreConcat(SKMatrix.CreateTranslation((float)-box[0], (float)-box[1]));
        }

        public void Children(XElement parent, SKCanvas canvas, Style style)
        {
            foreach (var child in parent.Elements())
            {
                if (parent.Name.LocalName == "switch")
                {
                    if (child.Attribute("requiredExtensions") != null)
                        continue;
                    Element(child, canvas, style);
                    break;
                }
                Element(child, canvas, style);
            }
        }

        void Element(XElement element, SKCanvas canvas, Style parent, int depth = 0)
        {
            if (depth > 32 || element.Name.Namespace != Svg && element.Name.Namespace != XNamespace.None)
                return;
            var name = element.Name.LocalName;
            if (name is "defs" or "clipPath" or "mask" or "pattern" or "linearGradient" or "radialGradient" or "symbol" or "style" or "title" or "desc"
                or "metadata" or "filter" or "marker")
                return;
            var style = Computed(element, parent);
            if (!style.Displayed)
                return;
            canvas.Save();
            if (Transform(element.Attribute("transform")?.Value) is { } transform)
                canvas.Concat(transform);
            if (style.ClipPath is { } clip && Reference(clip) is { Name.LocalName: "clipPath" } clipPath)
                Clip(clipPath, element, canvas, style);
            // Its opacity, its blending with what lies behind it and a group of its own (isolation) on a layer, which
            // takes the element after its filter and its mask.
            var layer = style.Opacity < 1 || style.Blend != SKBlendMode.SrcOver || style.Isolated;
            var layer2 = false;
            if (layer)
                canvas.SaveLayer(new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(Math.Clamp(style.Opacity, 0, 1) * 255)), BlendMode = style.Blend });
            // A filter of one color matrix (a 3D face's light): the element drawn on a layer it colors; other filters the
            // element is drawn through as an image filter graph, those with primitives it lacks left out.
            if (style.Filter is { } filterReference && Reference(filterReference) is { Name.LocalName: "filter" } filter)
            {
                if (filter.Elements().ToList() is [{ Name.LocalName: "feColorMatrix" } colorMatrix] && ColorMatrix(colorMatrix) is { } matrix)
                {
                    canvas.SaveLayer(new SKPaint { ColorFilter = SKColorFilter.CreateColorMatrix(matrix) });
                    layer2 = true;
                }
                else if (Graph(filter) is { } graph)
                {
                    canvas.SaveLayer(new SKPaint { ImageFilter = graph });
                    layer2 = true;
                }
            }
            // A mask: the element drawn on a layer of its own, which the mask's luminance then cuts.
            var mask = style.Mask is { } maskReference && Reference(maskReference) is { Name.LocalName: "mask" } masking ? masking : null;
            if (mask != null)
                canvas.SaveLayer();
            switch (name)
            {
                case "g" or "a" or "switch":
                    Children(element, canvas, style);
                    break;
                case "svg":
                {
                    var (x, y) = (Length(element.Attribute("x")?.Value, 0), Length(element.Attribute("y")?.Value, 0));
                    canvas.Translate((float)x, (float)y);
                    Viewport(element, canvas, Length(element.Attribute("width")?.Value, 0, 100), Length(element.Attribute("height")?.Value, 0, 100), parent, outermost: false);
                    break;
                }
                case "use":
                    if (Reference(element.Attribute(Xlink + "href")?.Value ?? element.Attribute("href")?.Value) is { } target && !target.AncestorsAndSelf().Contains(element))
                    {
                        canvas.Translate((float)Length(element.Attribute("x")?.Value, 0), (float)Length(element.Attribute("y")?.Value, 0));
                        if (target.Name.LocalName == "symbol")
                            Viewport(target, canvas, Length(element.Attribute("width")?.Value ?? target.Attribute("width")?.Value, 0, 100),
                                Length(element.Attribute("height")?.Value ?? target.Attribute("height")?.Value, 0, 100), style, outermost: false);
                        else
                            Element(target, canvas, style, depth + 1);
                    }
                    break;
                case "text":
                    Text(element, canvas, style);
                    break;
                case "image":
                    Image(element, canvas, style);
                    break;
                default:
                    if (Shape(element) is { } path)
                    {
                        using (path)
                            Paint(path, canvas, style);
                    }
                    break;
            }
            if (mask != null)
            {
                using (var luminance = new SKPaint { BlendMode = SKBlendMode.DstIn, ColorFilter = SKColorFilter.CreateLumaColor() })
                {
                    canvas.SaveLayer(luminance);
                    Children(mask, canvas, Computed(mask, Style.Initial));
                    canvas.Restore();
                }
                canvas.Restore();
            }
            if (layer2)
                canvas.Restore();
            if (layer)
                canvas.Restore();
            canvas.Restore();
        }

        /// <summary>
        /// A filter's primitives as an image filter graph (SVG 1.1 chapter 15; those Office's effects are made of:
        /// feColorMatrix, feComponentTransfer, feMorphology, feGaussianBlur, feOffset, feFlood, feMerge, feComposite and
        /// feBlend), each taking SourceGraphic, SourceAlpha, a result named before or the one before it; in user space,
        /// a flood cut at the filter's region. Null for another primitive or other units.
        /// </summary>
        internal static SKImageFilter? Graph(XElement filter)
        {
            if (filter.Attribute("primitiveUnits")?.Value == "objectBoundingBox")
                return null;
            SKRect? region = filter.Attribute("filterUnits")?.Value == "userSpaceOnUse" && filter.Attribute("width") != null && filter.Attribute("height") != null
                ? SKRect.Create((float)Length(filter.Attribute("x")?.Value, 0), (float)Length(filter.Attribute("y")?.Value, 0),
                    (float)Length(filter.Attribute("width")?.Value, 0), (float)Length(filter.Attribute("height")?.Value, 0))
                : null;
            var results = new Dictionary<string, SKImageFilter?>(StringComparer.Ordinal);
            SKImageFilter? last = null;
            // Null is the source graphic: what the filtered element draws.
            SKImageFilter? Input(string? name) => name switch
            {
                null or "" => last,
                "SourceGraphic" => null,
                "SourceAlpha" => SKImageFilter.CreateColorFilter(SKColorFilter.CreateColorMatrix([0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0])),
                _ => results.TryGetValue(name, out var result) ? result : last,
            };
            double Attribute(XElement primitive, string name, double fallback) => Number(primitive.Attribute(name)?.Value, fallback);
            foreach (var primitive in filter.Elements())
            {
                var input = Input(primitive.Attribute("in")?.Value);
                SKImageFilter? made;
                switch (primitive.Name.LocalName)
                {
                    case "feColorMatrix":
                        if (ColorMatrix(primitive) is not { } matrix)
                            return null;
                        made = SKImageFilter.CreateColorFilter(SKColorFilter.CreateColorMatrix(matrix), input);
                        break;
                    case "feComponentTransfer":
                    {
                        byte[] Table(string channel) => Transfer(primitive.Elements().LastOrDefault(e => e.Name.LocalName == "feFunc" + channel));
                        made = SKImageFilter.CreateColorFilter(SKColorFilter.CreateTable(Table("A"), Table("R"), Table("G"), Table("B")), input);
                        break;
                    }
                    case "feMorphology":
                    {
                        var radius = Numbers(primitive.Attribute("radius")?.Value) ?? [0];
                        var (rx, ry) = ((float)radius[0], (float)(radius.Length > 1 ? radius[1] : radius[0]));
                        made = primitive.Attribute("operator")?.Value == "dilate" ? SKImageFilter.CreateDilate(rx, ry, input) : SKImageFilter.CreateErode(rx, ry, input);
                        break;
                    }
                    case "feGaussianBlur":
                    {
                        var deviation = Numbers(primitive.Attribute("stdDeviation")?.Value) ?? [0];
                        made = SKImageFilter.CreateBlur((float)deviation[0], (float)(deviation.Length > 1 ? deviation[1] : deviation[0]), input);
                        break;
                    }
                    case "feOffset":
                        made = SKImageFilter.CreateOffset((float)Attribute(primitive, "dx", 0), (float)Attribute(primitive, "dy", 0), input);
                        break;
                    case "feFlood":
                    {
                        var color = ParseColor(primitive.Attribute("flood-color")?.Value ?? "black") ?? SKColors.Black;
                        var opacity = Math.Clamp(Attribute(primitive, "flood-opacity", 1), 0, 1) * color.Alpha / 255;
                        var flood = SKShader.CreateColor(color.WithAlpha((byte)Math.Round(opacity * 255)));
                        made = region is { } cut ? SKImageFilter.CreateShader(flood, false, cut) : SKImageFilter.CreateShader(flood);
                        break;
                    }
                    case "feMerge":
                        made = SKImageFilter.CreateMerge([.. primitive.Elements().Where(e => e.Name.LocalName == "feMergeNode").Select(e => Input(e.Attribute("in")?.Value))]);
                        break;
                    case "feComposite":
                    {
                        var behind = Input(primitive.Attribute("in2")?.Value);
                        var mode = primitive.Attribute("operator")?.Value ?? "over";
                        made = mode == "arithmetic"
                            ? SKImageFilter.CreateArithmetic((float)Attribute(primitive, "k1", 0), (float)Attribute(primitive, "k2", 0), (float)Attribute(primitive, "k3", 0),
                                (float)Attribute(primitive, "k4", 0), true, behind, input)
                            : SKImageFilter.CreateBlendMode(mode switch
                            {
                                "in" => SKBlendMode.SrcIn, "out" => SKBlendMode.SrcOut, "atop" => SKBlendMode.SrcATop, "xor" => SKBlendMode.Xor, _ => SKBlendMode.SrcOver,
                            }, behind, input);
                        break;
                    }
                    case "feBlend":
                        made = SKImageFilter.CreateBlendMode(BlendMode(primitive.Attribute("mode")?.Value) ?? SKBlendMode.SrcOver, Input(primitive.Attribute("in2")?.Value), input);
                        break;
                    default:
                        return null;
                }
                if (primitive.Attribute("result")?.Value is { Length: > 0 } name)
                    results[name] = made;
                last = made;
            }
            return last;
        }

        /// <summary>
        /// An feFuncR/G/B/A's transfer as a table of the 256 values of its channel (SVG 1.1 15.11: identity, table, discrete,
        /// linear, gamma); the identity without one.
        /// </summary>
        static byte[] Transfer(XElement? function)
        {
            var values = Numbers(function?.Attribute("tableValues")?.Value) ?? [];
            double Get(string name, double fallback) => Number(function?.Attribute(name)?.Value, fallback);
            var table = new byte[256];
            for (var k = 0; k < 256; k++)
            {
                var c = k / 255.0;
                var transferred = (function?.Attribute("type")?.Value) switch
                {
                    "linear" => Get("slope", 1) * c + Get("intercept", 0),
                    "gamma" => Get("amplitude", 1) * Math.Pow(c, Get("exponent", 1)) + Get("offset", 0),
                    "table" when values.Length > 1 => Interpolated(c),
                    "discrete" when values.Length > 0 => values[Math.Min(values.Length - 1, (int)Math.Floor(c * values.Length))],
                    _ => c,
                };
                table[k] = (byte)Math.Round(Math.Clamp(transferred, 0, 1) * 255);
            }
            return table;

            double Interpolated(double c)
            {
                var n = values.Length - 1;
                var at = Math.Min(n - 1, (int)Math.Floor(c * n));
                return values[at] + (c * n - at) * (values[at + 1] - values[at]);
            }
        }

        /// <summary>An feColorMatrix's 4 × 5 matrix (type matrix, saturate or hueRotate as SVG defines them), or null.</summary>
        static float[]? ColorMatrix(XElement primitive)
        {
            var type = primitive.Attribute("type")?.Value ?? "matrix";
            var values = Numbers(primitive.Attribute("values")?.Value);
            switch (type)
            {
                case "matrix":
                    return values is { Length: 20 } ? [.. values.Select(v => (float)v)] : null;
                case "saturate":
                {
                    var s = values is [var saturation, ..] ? saturation : 1;
                    return [(float)(0.213 + 0.787 * s), (float)(0.715 - 0.715 * s), (float)(0.072 - 0.072 * s), 0, 0,
                        (float)(0.213 - 0.213 * s), (float)(0.715 + 0.285 * s), (float)(0.072 - 0.072 * s), 0, 0,
                        (float)(0.213 - 0.213 * s), (float)(0.715 - 0.715 * s), (float)(0.072 + 0.928 * s), 0, 0, 0, 0, 0, 1, 0];
                }
                default:
                    return null;
            }
        }

        XElement? Reference(string? value)
        {
            if (value == null)
                return null;
            var match = Regex.Match(value, @"#([^)'""\s]+)");
            if (!match.Success)
                return null;
            var id = match.Groups[1].Value;
            if (ids.TryGetValue(id, out var target))
                return target;
            var defined = definitions?.Invoke(id);
            if (defined != null)
                ids[id] = defined;
            return defined;
        }

        /// <summary>A basic shape's or a path's outline, or null for other elements.</summary>
        static SKPath? Shape(XElement element)
        {
            double A(string name, double fallback = 0) => Length(element.Attribute(name)?.Value, fallback);
            switch (element.Name.LocalName)
            {
                case "path":
                    return element.Attribute("d")?.Value is { Length: > 0 } d ? SKPath.ParseSvgPathData(d) : null;
                case "rect":
                {
                    var (w, h) = (A("width"), A("height"));
                    if (w <= 0 || h <= 0)
                        return null;
                    var (rx, ry) = (element.Attribute("rx") != null ? A("rx") : A("ry"), element.Attribute("ry") != null ? A("ry") : A("rx"));
                    var path = new SKPath();
                    var rect = SKRect.Create((float)A("x"), (float)A("y"), (float)w, (float)h);
                    if (rx > 0 || ry > 0)
                        path.AddRoundRect(rect, (float)Math.Min(rx, w / 2), (float)Math.Min(ry, h / 2));
                    else
                        path.AddRect(rect);
                    return path;
                }
                case "circle":
                {
                    var r = A("r");
                    if (r <= 0)
                        return null;
                    var path = new SKPath();
                    path.AddCircle((float)A("cx"), (float)A("cy"), (float)r);
                    return path;
                }
                case "ellipse":
                {
                    var (rx, ry) = (A("rx"), A("ry"));
                    if (rx <= 0 || ry <= 0)
                        return null;
                    var path = new SKPath();
                    path.AddOval(SKRect.Create((float)(A("cx") - rx), (float)(A("cy") - ry), (float)(2 * rx), (float)(2 * ry)));
                    return path;
                }
                case "line":
                {
                    var path = new SKPath();
                    path.MoveTo((float)A("x1"), (float)A("y1"));
                    path.LineTo((float)A("x2"), (float)A("y2"));
                    return path;
                }
                case "polyline" or "polygon":
                {
                    var points = Numbers(element.Attribute("points")?.Value) ?? [];
                    if (points.Length < 4)
                        return null;
                    var path = new SKPath();
                    path.MoveTo((float)points[0], (float)points[1]);
                    for (var i = 2; i + 1 < points.Length; i += 2)
                        path.LineTo((float)points[i], (float)points[i + 1]);
                    if (element.Name.LocalName == "polygon")
                        path.Close();
                    return path;
                }
                default:
                    return null;
            }
        }

        /// <summary>Fills and strokes an outline as the style asks (without antialiasing where its shapes render crisp, as the browser does).</summary>
        void Paint(SKPath path, SKCanvas canvas, Style style)
        {
            if (!style.Visible)
                return;
            path.FillType = style.FillRule == "evenodd" ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
            var bounds = path.Bounds;
            if (Brush(style.Fill, style.FillOpacity, style, bounds, canvas.TotalMatrix) is { } fill)
            {
                using (fill)
                {
                    fill.IsAntialias = !style.Crisp;
                    canvas.DrawPath(path, fill);
                }
            }
            if (style.StrokeWidth > 0 && Brush(style.Stroke, style.StrokeOpacity, style, bounds, canvas.TotalMatrix) is { } stroke)
            {
                using (stroke)
                {
                    Stroked(stroke, style);
                    if (style.Crisp)
                        Crisp(stroke, style.NonScalingStroke ? outer : outer?.PreConcat(canvas.TotalMatrix));
                    if (style.NonScalingStroke)
                    {
                        // The stroke's width in the device's pixels: the outline mapped there and stroked unscaled.
                        using var mapped = new SKPath(path);
                        mapped.Transform(canvas.TotalMatrix);
                        canvas.Save();
                        canvas.ResetMatrix();
                        canvas.DrawPath(mapped, stroke);
                        canvas.Restore();
                    }
                    else
                        canvas.DrawPath(path, stroke);
                }
            }
        }

        /// <summary>A paint made a stroke as the style strokes: its width, caps, joins and dashes.</summary>
        static void Stroked(SKPaint stroke, Style style)
        {
            stroke.Style = SKPaintStyle.Stroke;
            stroke.StrokeWidth = (float)style.StrokeWidth;
            stroke.StrokeCap = style.LineCap switch { "round" => SKStrokeCap.Round, "square" => SKStrokeCap.Square, _ => SKStrokeCap.Butt };
            stroke.StrokeJoin = style.LineJoin switch { "round" => SKStrokeJoin.Round, "bevel" => SKStrokeJoin.Bevel, _ => SKStrokeJoin.Miter };
            stroke.StrokeMiter = (float)Math.Max(1, style.MiterLimit);
            if (style.Dashes is { Length: > 0 } dashes && dashes.Any(d => d > 0))
            {
                var pattern = (dashes.Length % 2 == 0 ? dashes : [.. dashes, .. dashes]).Select(d => (float)Math.Max(0, d)).ToArray();
                stroke.PathEffect = SKPathEffect.CreateDash(pattern, (float)style.DashOffset);
            }
        }

        /// <summary>The fill and the stroke an element's own attributes give it over a box (see SvgDrawing.Paints).</summary>
        internal (SKPaint? Fill, SKPaint? Stroke) Paints(XElement element, SKRect bounds)
        {
            var style = Computed(element, Style.Initial);
            var fill = Brush(style.Fill, style.FillOpacity, style, bounds, SKMatrix.Identity);
            var stroke = style.StrokeWidth > 0 ? Brush(style.Stroke, style.StrokeOpacity, style, bounds, SKMatrix.Identity) : null;
            if (stroke != null)
                Stroked(stroke, style);
            return (fill, stroke);
        }

        /// <summary>
        /// A paint for a fill or stroke value: a color, currentColor, or a gradient or pattern of the document (drawn
        /// under the canvas's matrix <paramref name="at"/>).
        /// </summary>
        SKPaint? Brush(string value, double opacity, Style style, SKRect bounds, SKMatrix at)
        {
            if (value is "none" or "transparent" || opacity <= 0)
                return null;
            if (value.StartsWith("url(", StringComparison.Ordinal))
            {
                if (Reference(value) is { } server && Server(server, bounds, opacity, at) is { } shader)
                    return new SKPaint { Shader = shader, IsAntialias = true, Style = SKPaintStyle.Fill };
                // A reference that is not there: the fallback color after it, else nothing.
                var fallback = value[(value.IndexOf(')') + 1)..].Trim();
                return fallback.Length > 0 ? Brush(fallback, opacity, style, bounds, at) : null;
            }
            if (ParseColor(value == "currentColor" ? style.Color : value) is not { } color)
                return null;
            return new SKPaint { Color = color.WithAlpha((byte)Math.Round(color.Alpha * Math.Clamp(opacity, 0, 1))), IsAntialias = true, Style = SKPaintStyle.Fill };
        }

        /// <summary>The shader of a paint server (a gradient or a pattern) over what it paints, drawn under the matrix <paramref name="at"/>.</summary>
        internal SKShader? Server(XElement server, SKRect bounds, double opacity, SKMatrix? at = null)
        {
            // Gradients and patterns inherit their attributes and stops through href.
            string? Inherited(string name)
            {
                for (var (element, hops) = (server, 0); element != null && hops < 8; element = Reference(element.Attribute(Xlink + "href")?.Value ?? element.Attribute("href")?.Value), hops++)
                {
                    if (element.Attribute(name)?.Value is { } value)
                        return value;
                }
                return null;
            }
            var boxUnits = Inherited(server.Name.LocalName == "pattern" ? "patternUnits" : "gradientUnits") != "userSpaceOnUse";
            var transform = Transform(Inherited(server.Name.LocalName == "pattern" ? "patternTransform" : "gradientTransform")) ?? SKMatrix.Identity;
            var box = boxUnits ? SKMatrix.CreateTranslation(bounds.Left, bounds.Top).PreConcat(SKMatrix.CreateScale(bounds.Width, bounds.Height)) : SKMatrix.Identity;
            double Coordinate(string name, double fallback) => Inherited(name) is { } v
                ? v.EndsWith('%') ? Number(v[..^1], 0) / 100 * (boxUnits ? 1 : 100) : Length(v, fallback)
                : fallback;
            switch (server.Name.LocalName)
            {
                case "linearGradient" or "radialGradient":
                {
                    var holder = server;
                    for (var hops = 0; holder != null && !holder.Elements().Any(e => e.Name.LocalName == "stop") && hops < 8; hops++)
                        holder = Reference(holder.Attribute(Xlink + "href")?.Value ?? holder.Attribute("href")?.Value);
                    var stops = holder?.Elements().Where(e => e.Name.LocalName == "stop").ToList() ?? [];
                    if (stops.Count == 0)
                        return null;
                    var colors = new SKColor[stops.Count];
                    var positions = new float[stops.Count];
                    var last = 0f;
                    for (var i = 0; i < stops.Count; i++)
                    {
                        var stop = Computed(stops[i], Style.Initial);
                        var offset = stops[i].Attribute("offset")?.Value ?? "0";
                        last = positions[i] = (float)Math.Max(last, Math.Clamp(offset.EndsWith('%') ? Number(offset[..^1], 0) / 100 : Number(offset, 0), 0, 1));
                        var color = ParseColor(stop.StopColor) ?? SKColors.Black;
                        colors[i] = color.WithAlpha((byte)Math.Round(color.Alpha * Math.Clamp(stop.StopOpacity * opacity, 0, 1)));
                    }
                    var spread = Inherited("spreadMethod") switch { "reflect" => SKShaderTileMode.Mirror, "repeat" => SKShaderTileMode.Repeat, _ => SKShaderTileMode.Clamp };
                    var local = box.PreConcat(transform);
                    if (server.Name.LocalName == "linearGradient")
                    {
                        var start = new SKPoint((float)Coordinate("x1", 0), (float)Coordinate("y1", 0));
                        var end = new SKPoint((float)Coordinate("x2", boxUnits ? 1 : bounds.Right), (float)Coordinate("y2", 0));
                        return SKShader.CreateLinearGradient(start, end, colors, positions, spread, local);
                    }
                    var (cx, cy, r) = (Coordinate("cx", boxUnits ? 0.5 : bounds.MidX), Coordinate("cy", boxUnits ? 0.5 : bounds.MidY), Coordinate("r", boxUnits ? 0.5 : bounds.Width / 2));
                    var (fx, fy) = (Coordinate("fx", cx), Coordinate("fy", cy));
                    return SKShader.CreateTwoPointConicalGradient(new SKPoint((float)fx, (float)fy), 0, new SKPoint((float)cx, (float)cy), (float)Math.Max(r, 1e-6), colors, positions, spread, local);
                }
                case "pattern" when patterns < 4:
                {
                    var (x, y, w, h) = (Coordinate("x", 0), Coordinate("y", 0), Coordinate("width", 0), Coordinate("height", 0));
                    if (w <= 0 || h <= 0)
                        return null;
                    var holder = server;
                    for (var hops = 0; holder != null && !holder.HasElements && hops < 8; hops++)
                        holder = Reference(holder.Attribute(Xlink + "href")?.Value ?? holder.Attribute("href")?.Value);
                    if (holder == null)
                        return null;
                    // The tile in the pattern's space (in user units, or in the box's where it asks for it); its content
                    // has its origin at the tile's corner (SVG 1.1 13.3), mapped there by a viewBox where it has one, else
                    // in user units or the box's size.
                    var tile = box.MapRect(SKRect.Create((float)x, (float)y, (float)w, (float)h));
                    using var recorder = new SKPictureRecorder();
                    var canvas = recorder.BeginRecording(SKRect.Create(tile.Width, tile.Height));
                    if (Numbers(Inherited("viewBox")) is { Length: 4 } viewBox && viewBox[2] > 0 && viewBox[3] > 0)
                        canvas.Concat(ViewBox(viewBox, tile.Width, tile.Height, Inherited("preserveAspectRatio")));
                    else if (Inherited("patternContentUnits") == "objectBoundingBox")
                        canvas.Scale(bounds.Width, bounds.Height);
                    var local = transform.PreConcat(SKMatrix.CreateTranslation(tile.Left, tile.Top));
                    var style = Computed(holder, Style.Initial);
                    // Its content lands on pixels under the element's matrix and the pattern's own; patterns within
                    // patterns go four deep.
                    var around = outer;
                    outer = around?.PreConcat(at ?? SKMatrix.Identity).PreConcat(local);
                    patterns++;
                    Children(holder, canvas, style);
                    patterns--;
                    outer = around;
                    using var picture = recorder.EndRecording();
                    // Where no pixels are known (into a PDF) the tile as a picture in 300 dots an inch: Skia's PDF keeps
                    // an image's tiles as a pattern of it, but rasterizes those of a picture in its own 72.
                    using var pixels = outer == null ? Tile(picture, tile.Size, (at ?? SKMatrix.Identity).PreConcat(local)) : null;
                    var shader = pixels != null
                        ? pixels.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear),
                            local.PreConcat(SKMatrix.CreateScale(tile.Width / pixels.Width, tile.Height / pixels.Height)))
                        : picture.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, SKFilterMode.Linear, local, SKRect.Create(tile.Width, tile.Height));
                    return opacity < 1 ? shader.WithColorFilter(SKColorFilter.CreateBlendMode(SKColors.White.WithAlpha((byte)Math.Round(opacity * 255)), SKBlendMode.DstIn)) : shader;
                }
                default:
                    return null;
            }
        }

        /// <summary>Cuts the canvas at a clip path: the union of its children's outlines, each with its own transform and clip rule.</summary>
        void Clip(XElement clipPath, XElement target, SKCanvas canvas, Style style, int depth = 0)
        {
            if (depth > 8)
                return;
            using var union = new SKPath();
            foreach (var child in clipPath.Elements())
            {
                var childStyle = Computed(child, Computed(clipPath, style));
                if (!childStyle.Displayed || !childStyle.Visible)
                    continue;
                var outline = child.Name.LocalName == "use" && Reference(child.Attribute(Xlink + "href")?.Value ?? child.Attribute("href")?.Value) is { } used ? Shape(used) : Shape(child);
                if (outline == null)
                    continue;
                using (outline)
                {
                    outline.FillType = childStyle.ClipRule == "evenodd" ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
                    if (Transform(child.Attribute("transform")?.Value) is { } transform)
                        outline.Transform(transform);
                    using var simple = outline.Simplify() ?? new SKPath(outline);
                    union.AddPath(simple);
                }
            }
            using var united = union.Simplify() ?? new SKPath(union);
            if (clipPath.Attribute("clipPathUnits")?.Value == "objectBoundingBox" && Shape(target) is { } shape)
            {
                using (shape)
                    united.Transform(SKMatrix.CreateTranslation(shape.Bounds.Left, shape.Bounds.Top).PreConcat(SKMatrix.CreateScale(shape.Bounds.Width, shape.Bounds.Height)));
            }
            if (Transform(clipPath.Attribute("transform")?.Value) is { } own)
                united.Transform(own);
            canvas.ClipPath(united, antialias: true);
            // A clip path cut at its own clip path.
            if (Computed(clipPath, style).ClipPath is { } outer && Reference(outer) is { Name.LocalName: "clipPath" } parent && parent != clipPath)
                Clip(parent, target, canvas, style, depth + 1);
        }

        /// <summary>
        /// Text: its characters at the places its lists give them (x, y, dx, dy, one a character, the last carried on), else
        /// after each other, in tspans of their own style; anchored at their start, middle or end.
        /// </summary>
        void Text(XElement text, SKCanvas canvas, Style style)
        {
            var pen = (X: 0.0, Y: 0.0);
            var pieces = new List<(string Text, double[] X, double[] Y, Style Style)>();
            var preserve = text.Attribute(XNamespace.Xml + "space")?.Value == "preserve";
            void Walk(XElement element, Style own, double[]? xs, double[]? ys, double[]? dxs, double[]? dys)
            {
                xs = Numbers(element.Attribute("x")?.Value) ?? xs;
                ys = Numbers(element.Attribute("y")?.Value) ?? ys;
                dxs = Numbers(element.Attribute("dx")?.Value) ?? dxs;
                dys = Numbers(element.Attribute("dy")?.Value) ?? dys;
                var index = 0;
                foreach (var node in element.Nodes())
                {
                    if (node is XText content)
                    {
                        var value = preserve ? content.Value.Replace('\n', ' ').Replace('\t', ' ') : Regex.Replace(content.Value, @"\s+", " ");
                        var places = new double[value.Length];
                        var heights = new double[value.Length];
                        for (var i = 0; i < value.Length; i++, index++)
                        {
                            if (char.IsLowSurrogate(value[i]))
                            {
                                (places[i], heights[i]) = (double.NaN, double.NaN);
                                index--;
                                continue;
                            }
                            if (xs != null && index < xs.Length)
                                pen.X = xs[index];
                            if (ys != null && index < ys.Length)
                                pen.Y = ys[index];
                            if (dxs != null && index < dxs.Length)
                                pen.X += dxs[index];
                            if (dys != null && index < dys.Length)
                                pen.Y += dys[index];
                            (places[i], heights[i]) = (xs != null && index < xs.Length || dxs != null && index < dxs.Length || i == 0 ? pen.X : double.NaN, pen.Y);
                        }
                        pieces.Add((value, places, heights, own));
                    }
                    else if (node is XElement child && child.Name.LocalName is "tspan" or "a" && Computed(child, own) is { Displayed: true } childStyle)
                    {
                        Walk(child, childStyle, null, null, null, null);
                        // A tspan's text moves the pen on (its pieces set it).
                        if (pieces.Count > 0)
                            pen = Advance(pieces[^1], pen);
                    }
                }
                if (pieces.Count > 0)
                    pen = Advance(pieces[^1], pen);
            }
            (double X, double Y) Advance((string Text, double[] X, double[] Y, Style Style) piece, (double X, double Y) at)
            {
                var font = Font(piece.Style);
                var x = at.X;
                for (var i = piece.Text.Length - 1; i >= 0; i--)
                {
                    if (!double.IsNaN(piece.X[i]))
                    {
                        x = piece.X[i] + font.MeasureText(piece.Text[i..]);
                        return (x, double.IsNaN(piece.Y[i]) ? at.Y : piece.Y[i]);
                    }
                }
                return at;
            }
            Walk(text, style, null, null, null, null);
            if (style.Anchor != "start" && pieces.Count > 0)
            {
                // The whole text moved back by its width or half of it.
                var width = pieces.Sum(p => Font(p.Style).MeasureText(p.Text));
                var shift = style.Anchor == "middle" ? width / 2 : style.Anchor == "end" ? width : 0;
                canvas.Translate((float)-shift, 0);
            }
            foreach (var (value, xs, ys, own) in pieces)
            {
                if (!own.Visible || value.Length == 0)
                    continue;
                using var font = Font(own);
                // The typeface the caller has it drawn in (into a PDF, the one cut down to what is drawn), by the glyphs
                // the one found maps the text to: a subset maps only the characters it was given (a symbol font's
                // private-use ones not).
                using var chosen = typefaces != null && font.Typeface is { } found
                    && typefaces(found, font.GetGlyphs(value).Select(g => (uint)g), value) is var other && !ReferenceEquals(other, found)
                    ? new SKFont(other, font.Size)
                    {
                        Subpixel = true, Edging = SKFontEdging.Antialias, LinearMetrics = true,
                        // The slant and weight the system simulates for a family without such a face, which a typeface
                        // made from the font's data does not (DirectWrite's oblique measured: a skew of 0.34).
                        SkewX = found.FontStyle.Slant != SKFontStyleSlant.Upright && other.FontStyle.Slant == SKFontStyleSlant.Upright ? -0.34f : 0,
                        Embolden = found.FontStyle.Weight >= 600 && other.FontStyle.Weight < 600,
                    }
                    : null;
                void Draw(string run, double x, double y, SKPaint paint)
                {
                    if (chosen == null)
                    {
                        canvas.DrawText(run, (float)x, (float)y, SKTextAlign.Left, font, paint);
                        return;
                    }
                    var ids = font.GetGlyphs(run);
                    using var builder = new SKTextBlobBuilder();
                    var buffer = builder.AllocateRun(chosen, ids.Length, (float)x, (float)y);
                    ids.CopyTo(buffer.Glyphs);
                    using var blob = builder.Build();
                    if (blob != null)
                        canvas.DrawText(blob, 0, 0, paint);
                }
                var bounds = SKRect.Create((float)xs.Where(v => !double.IsNaN(v)).DefaultIfEmpty(0).First(), (float)(ys[0] - own.FontSize), (float)font.MeasureText(value), (float)own.FontSize);
                using var fill = Brush(own.Fill, own.FillOpacity, own, bounds, canvas.TotalMatrix);
                using var stroke = own.StrokeWidth > 0 ? Brush(own.Stroke, own.StrokeOpacity, own, bounds, canvas.TotalMatrix) : null;
                if (stroke != null)
                    Stroked(stroke, own);
                var x = xs[0];
                var y = ys[0];
                for (var i = 0; i < value.Length;)
                {
                    // A run of characters from one given place to the next.
                    var end = i + 1;
                    while (end < value.Length && double.IsNaN(xs[end]))
                        end++;
                    if (!double.IsNaN(xs[i]))
                        x = xs[i];
                    if (!double.IsNaN(ys[i]))
                        y = ys[i];
                    var run = value[i..end];
                    if (fill != null)
                        Draw(run, x, y, fill);
                    if (stroke != null)
                        Draw(run, x, y, stroke);
                    var advance = font.MeasureText(run);
                    if (fill != null && own.Decoration.Contains("underline"))
                        canvas.DrawRect(SKRect.Create((float)x, (float)(y + own.FontSize * 0.1), advance, (float)Math.Max(1, own.FontSize / 16)), fill);
                    if (fill != null && own.Decoration.Contains("line-through"))
                        canvas.DrawRect(SKRect.Create((float)x, (float)(y - own.FontSize * 0.3), advance, (float)Math.Max(1, own.FontSize / 16)), fill);
                    x += advance;
                    i = end;
                }
            }
        }

        /// <summary>
        /// The font of a style: the first family of its list that the system has, or that is found as a file of Office's
        /// cloud fonts (charts' text in Aptos was drawn in Skia's default font, its letters apart where it stands at
        /// Aptos's places), in its weight and slant.
        /// </summary>
        static SKFont Font(Style style)
        {
            var slant = style.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
            var fontStyle = new SKFontStyle(style.Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, slant);
            SKTypeface? typeface = null;
            foreach (var raw in style.FontFamily.Split(','))
            {
                var family = raw.Trim().Trim('\'', '"');
                family = family switch { "serif" => "Times New Roman", "sans-serif" => "Arial", "monospace" => "Courier New", _ => family };
                if (family.Length > 0 && (SKFontManager.Default.MatchFamily(family, fontStyle) ?? FileTypeface(family, style.Bold, style.Italic)) is { } match)
                {
                    typeface = match;
                    break;
                }
            }
            return new SKFont(typeface ?? SKTypeface.Default, (float)style.FontSize) { Subpixel = true, Edging = SKFontEdging.Antialias, LinearMetrics = true };
        }

        static readonly System.Collections.Concurrent.ConcurrentDictionary<(string Family, bool Bold, bool Italic), SKTypeface?> fileTypefaces = new();

        /// <summary>A family that is not installed but found as a font file (Office's cloud fonts), in its face nearest the style; made once.</summary>
        static SKTypeface? FileTypeface(string family, bool bold, bool italic) => fileTypefaces.GetOrAdd((family, bold, italic), key =>
        {
            var faces = Fonts.FontLocator.Default.Faces(key.Family).Where(f => !f.System && f.Embedded == null && f.Variations == null).ToList();
            var face = faces.FirstOrDefault(f => f.Bold == key.Bold && f.Italic == key.Italic) ?? faces.FirstOrDefault(f => f.Italic == key.Italic) ?? faces.FirstOrDefault();
            return face == null ? null : SKTypeface.FromFile(face.Path, face.Index);
        });

        /// <summary>An image: a picture of a data URI (another SVG among them) placed in its box as preserveAspectRatio asks.</summary>
        void Image(XElement image, SKCanvas canvas, Style style)
        {
            var (x, y, w, h) = (Length(image.Attribute("x")?.Value, 0), Length(image.Attribute("y")?.Value, 0),
                Length(image.Attribute("width")?.Value, 0), Length(image.Attribute("height")?.Value, 0));
            var href = image.Attribute(Xlink + "href")?.Value ?? image.Attribute("href")?.Value;
            if (!style.Visible || href == null || !href.StartsWith("data:", StringComparison.Ordinal) || href.IndexOf(',') is not (> 0 and var comma))
                return;
            byte[] data;
            try
            {
                data = href.AsSpan(0, comma).EndsWith(";base64", StringComparison.Ordinal)
                    ? Convert.FromBase64String(href[(comma + 1)..])
                    : System.Text.Encoding.UTF8.GetBytes(Uri.UnescapeDataString(href[(comma + 1)..]));
            }
            catch (FormatException)
            {
                return;
            }
            var aspect = image.Attribute("preserveAspectRatio")?.Value;
            canvas.Save();
            if (href.AsSpan(5).StartsWith("image/svg+xml", StringComparison.Ordinal))
            {
                if (Load(data) is { } drawing)
                {
                    var (iw, ih) = (w > 0 ? w : drawing.Size.Width, h > 0 ? h : drawing.Size.Height);
                    canvas.Translate((float)x, (float)y);
                    canvas.ClipRect(SKRect.Create((float)iw, (float)ih));
                    canvas.Concat(ViewBox([0, 0, drawing.Size.Width, drawing.Size.Height], iw, ih, aspect));
                    canvas.DrawPicture(drawing.Picture);
                }
            }
            else if (SKImage.FromEncodedData(data) is { } picture)
            {
                using (picture)
                {
                    var (iw, ih) = (w > 0 ? w : picture.Width, h > 0 ? h : picture.Height);
                    canvas.Translate((float)x, (float)y);
                    canvas.ClipRect(SKRect.Create((float)iw, (float)ih));
                    canvas.Concat(ViewBox([0, 0, picture.Width, picture.Height], iw, ih, aspect));
                    var sampling = style.Pixelated ? new SKSamplingOptions(SKFilterMode.Nearest) : new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
                    using var paint = new SKPaint { IsAntialias = true };
                    // On pixels, squeezed far more along one axis than along the other, evened first.
                    using var evened = !style.Pixelated && outer is { } pixels ? Evened(picture, pixels.PreConcat(canvas.TotalMatrix)) : null;
                    canvas.DrawImage(evened ?? picture, SKRect.Create(picture.Width, picture.Height), sampling, paint);
                }
            }
            canvas.Restore();
        }
    }

    /// <summary>A transform list (matrix, translate, scale, rotate, skewX, skewY), or null for none.</summary>
    static SKMatrix? Transform(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var result = SKMatrix.Identity;
        foreach (Match match in Regex.Matches(value, @"(matrix|translate|scale|rotate|skewX|skewY)\s*\(([^)]*)\)"))
        {
            var v = Numbers(match.Groups[2].Value) ?? [];
            double At(int i, double fallback = 0) => i < v.Length ? v[i] : fallback;
            var step = match.Groups[1].Value switch
            {
                "matrix" when v.Length >= 6 => new SKMatrix((float)v[0], (float)v[2], (float)v[4], (float)v[1], (float)v[3], (float)v[5], 0, 0, 1),
                "translate" => SKMatrix.CreateTranslation((float)At(0), (float)At(1)),
                "scale" => SKMatrix.CreateScale((float)At(0, 1), (float)At(1, At(0, 1))),
                "rotate" => SKMatrix.CreateRotationDegrees((float)At(0), (float)At(1), (float)At(2)),
                "skewX" => SKMatrix.CreateSkew((float)Math.Tan(At(0) * Math.PI / 180), 0),
                "skewY" => SKMatrix.CreateSkew(0, (float)Math.Tan(At(0) * Math.PI / 180)),
                _ => SKMatrix.Identity,
            };
            result = result.PreConcat(step);
        }
        return result;
    }

    /// <summary>Numbers separated by commas or white space (a list's, a viewBox's), or null for none.</summary>
    static double[]? Numbers(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var matches = Regex.Matches(value, @"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?");
        return matches.Count == 0 ? null : matches.Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray();
    }

    static double Number(string? value, double fallback) =>
        double.TryParse(value?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : fallback;

    static double Fraction(string value) => Math.Clamp(value.EndsWith('%') ? Number(value[..^1], 100) / 100 : Number(value, 1), 0, 1);

    /// <summary>A length in user units (pixels): a number with an absolute unit, an em, or a percentage of the given size.</summary>
    static double Length(string? value, double em, double percentOf = 0)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;
        var match = Regex.Match(value.Trim(), @"^([-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?)\s*([a-z%]*)$");
        if (!match.Success)
            return 0;
        var number = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        return match.Groups[2].Value switch
        {
            "pt" => number * 96 / 72, "pc" => number * 16, "mm" => number * 96 / 25.4, "cm" => number * 96 / 2.54, "in" => number * 96,
            "em" => number * (em > 0 ? em : 16), "ex" => number * (em > 0 ? em : 16) / 2, "%" => number / 100 * percentOf,
            _ => number,
        };
    }

    static double FontSize(string value, double parent) => value switch
    {
        "xx-small" => 9, "x-small" => 10, "small" => 13, "medium" => 16, "large" => 18, "x-large" => 24, "xx-large" => 32,
        "smaller" => parent / 1.2, "larger" => parent * 1.2,
        _ when value.EndsWith('%') => Number(value[..^1], 100) / 100 * parent,
        _ => Length(value, parent) is > 0 and var size ? size : parent,
    };

    /// <summary>A color: #rgb, #rrggbb, rgb() and rgba() in numbers or percentages, or a name; null for none or what is not one.</summary>
    static SKColor? ParseColor(string value)
    {
        value = value.Trim();
        if (value.StartsWith('#'))
        {
            var hex = value[1..];
            if (hex.Length == 3)
                hex = string.Concat(hex.Select(c => $"{c}{c}"));
            return hex.Length == 6 && uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb)
                ? new SKColor((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb) : null;
        }
        if (Regex.Match(value, @"^rgba?\(([^)]*)\)$") is { Success: true } function)
        {
            var parts = function.Groups[1].Value.Split([',', ' ', '/'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
                return null;
            byte Channel(string part) => (byte)Math.Round(Math.Clamp(part.EndsWith('%') ? Number(part[..^1], 0) * 2.55 : Number(part, 0), 0, 255));
            var alpha = parts.Length > 3 ? Fraction(parts[3]) : 1;
            return new SKColor(Channel(parts[0]), Channel(parts[1]), Channel(parts[2]), (byte)Math.Round(alpha * 255));
        }
        if (value is "none" or "transparent" || value.Length == 0)
            return null;
        var named = System.Drawing.Color.FromName(value);
        return named.IsKnownColor ? new SKColor(named.R, named.G, named.B, named.A) : null;
    }
}
