using System.Xml.Linq;
using FreeSlate.Html;
using FreeSlate.Pptx;

namespace FreeSlate.Docx;

/// <summary>A drawing in a run (w:drawing/wp:inline, catalog: drawing.inline): it stands in the line like a character.</summary>
public sealed record DrawingInline(XElement Drawing, string Part, RunFormat Format) : Inline(Format)
{
    /// <summary>The content of the shape's text box (wps:txbx/w:txbxContent), or null.</summary>
    public List<Block>? Text { get; init; }

    /// <summary>The text boxes of the shapes in a group or canvas (catalog: drawing.groups, drawing.canvas), by shape.</summary>
    public IReadOnlyList<(XElement Shape, List<Block> Text)>? ChildText { get; init; }

    /// <summary>The box the drawing takes in the line: its extent with the effect extents around it, in points.</summary>
    public (double Width, double Height) Box => Drawings.Box(Drawing);
}

/// <summary>A floating drawing anchored at its run (w:drawing/wp:anchor, catalog: drawing.anchor); it takes no room in the line.</summary>
public sealed record AnchorInline(XElement Anchor, string Part, RunFormat Format) : Inline(Format)
{
    /// <summary>The content of the shape's text box (wps:txbx/w:txbxContent), or null.</summary>
    public List<Block>? Text { get; init; }

    /// <summary>The text boxes of the shapes in a group or canvas (catalog: drawing.groups, drawing.canvas), by shape.</summary>
    public IReadOnlyList<(XElement Shape, List<Block> Text)>? ChildText { get; init; }
}

/// <summary>A drawing placed on a page: its element, the part its relationships belong to and its extent's box.</summary>
public sealed record PlacedDrawing(XElement Drawing, string Part, double X, double Y, double Width, double Height)
{
    /// <summary>Behind the text (wp:anchor/@behindDoc).</summary>
    public bool Behind { get; init; }

    /// <summary>The stacking order among floating drawings (wp:anchor/@relativeHeight); inline drawings 0.</summary>
    public long Order { get; init; }

    /// <summary>The content of the shape's text box, laid out in its inner rectangle and drawn over the shape.</summary>
    public List<Block>? Text { get; init; }

    /// <summary>The text boxes of the shapes in a group or canvas (catalog: drawing.groups, drawing.canvas), by shape.</summary>
    public IReadOnlyList<(XElement Shape, List<Block> Text)>? ChildText { get; init; }

    /// <summary>The text box's body properties (insets, vertical anchor) of the shape (wps:bodyPr).</summary>
    public XElement? Body => Drawing.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData")?.Element(Drawings.Wps + "wsp")?.Element(Drawings.Wps + "bodyPr");
}

/// <summary>
/// Word drawings (catalog: drawing.inline, drawing.anchor, drawing.shapes, drawing.groups, drawing.canvas): pictures,
/// shapes, groups, canvases, charts and SmartArt are converted to PresentationML and drawn by the slide renderer with
/// the document's theme and color mapping, shared with PowerPoint (pptx features picture.*, shape.*, chart.*,
/// smartart.*).
/// </summary>
public sealed class Drawings(DocumentContext context, System.Globalization.CultureInfo? culture = null)
{
    public static readonly XNamespace Wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    public static readonly XNamespace Wp14 = "http://schemas.microsoft.com/office/word/2010/wordprocessingDrawing";
    public static readonly XNamespace Pic = "http://schemas.openxmlformats.org/drawingml/2006/picture";
    public static readonly XNamespace Wps = "http://schemas.microsoft.com/office/word/2010/wordprocessingShape";
    public static readonly XNamespace Wpg = "http://schemas.microsoft.com/office/word/2010/wordprocessingGroup";
    public static readonly XNamespace Wpc = "http://schemas.microsoft.com/office/word/2010/wordprocessingCanvas";

    const double EmuPerPoint = 12700;

    /// <summary>Marks on converted VML WordArt: the em box's share above the capitals and below the baseline.</summary>
    static readonly XName WordArtTop = XNamespace.Get("urn:freeslate:wordart") + "top", WordArtBottom = XNamespace.Get("urn:freeslate:wordart") + "bottom";

    PresentationContext? presentation;
    readonly Dictionary<string, SlideRenderer> renderers = new(StringComparer.OrdinalIgnoreCase);
    readonly SvgPaint paint = new();

    /// <summary>The presentation context the slide renderer draws with: the same package, fonts and glyphs.</summary>
    public PresentationContext Presentation => presentation ??= new PresentationContext(context.Package, context.FontLocator)
    {
        Glyphs = context.Glyphs,
        Culture = culture ?? System.Globalization.CultureInfo.CurrentCulture,
    };

    /// <summary>The paint of drawings and text effects: one counter for the ids of their gradients and patterns.</summary>
    public SvgPaint Paint => paint;

    /// <summary>The document's theme colors with its color mapping (w:clrSchemeMapping).</summary>
    public Drawing.ColorContext Colors => colors ??= new Drawing.ColorContext(context.Theme, ColorMap());

    Drawing.ColorContext? colors;

    /// <summary>
    /// The page background (catalog: document.background): the fill of w:background or its v:background on a
    /// rectangle the size of the page, behind everything; Word prints it whether or not w:displayBackgroundShape is
    /// set (measured with docx/local/misc-nothing, misc-nobg).
    /// </summary>
    public string Background(double width, double height)
    {
        // Word's VML gradients repeat over the page in tiles (see Vml.BackgroundGradient, BackgroundTile).
        if (context.Background?.Element(Vml.V + "background") is { } vml)
        {
            const double Px = 96.0 / 72;
            var (w, h) = (Units.Css(width * Px), Units.Css(height * Px));
            string Page(string defs, string paint) =>
                $"<div class=\"sp\" style=\"width:{w}px;height:{h}px\"><svg width=\"{w}\" height=\"{h}\" viewBox=\"0 0 {w} {h}\"><defs>{defs}</defs>" +
                $"<rect width=\"{w}\" height=\"{h}\"{paint} shape-rendering=\"crispEdges\"/></svg></div>";
            if (Vml.BackgroundGradient(vml) is { } gradient)
            {
                // The gradient turned back at its ends across the whole page, written out: PDF viewers (Windows' among
                // them) draw a mirrored SVG gradient that browsers print as nothing.
                var (vx, vy) = (gradient.X2 - gradient.X1, gradient.Y2 - gradient.Y1);
                var along = new[] { (0.0, 0.0), (width, 0.0), (0.0, height), (width, height) }
                    .Select(c => ((c.Item1 - gradient.X1) * vx + (c.Item2 - gradient.Y1) * vy) / (vx * vx + vy * vy)).ToList();
                var (first, last) = ((int)Math.Floor(along.Min()), (int)Math.Ceiling(along.Max()));
                var count = Math.Max(1, last - first);
                var stops = new System.Text.StringBuilder();
                for (var k = first; k < first + count; k++)
                {
                    var ramp = (k & 1) == 0 ? gradient.Stops : gradient.Stops.AsEnumerable().Reverse().Select(s => (Pos: 1 - s.Pos, s.Hex, s.Alpha)).ToList();
                    foreach (var (pos, hex, alpha) in ramp)
                        stops.Append($"<stop offset=\"{Units.Css4((k - first + Math.Clamp(pos, 0, 1)) / count)}\" stop-color=\"#{hex.ToLowerInvariant()}\"" +
                            (alpha < 1 ? $" stop-opacity=\"{Units.Css4(alpha)}\"" : "") + "/>");
                }
                var id = paint.NewId("bg");
                var (x1, y1) = (gradient.X1 + first * vx, gradient.Y1 + first * vy);
                var (x2, y2) = (gradient.X1 + (first + count) * vx, gradient.Y1 + (first + count) * vy);
                return Page($"<linearGradient id=\"{id}\" gradientUnits=\"userSpaceOnUse\" x1=\"{Units.Css(x1 * Px)}\" y1=\"{Units.Css(y1 * Px)}\" " +
                    $"x2=\"{Units.Css(x2 * Px)}\" y2=\"{Units.Css(y2 * Px)}\">{stops}</linearGradient>", $" fill=\"url(#{id})\"");
            }
            // A picture over the page Word prints as tiles of its pixels at its 0.12 pt dots (measured with
            // docx/local/bgpreset-pic-stretch: 64 × 48 and 150 × 100 pixels at 96 and 300 dpi); a pattern as its
            // bitmap, a point a bit, white in the fill color and black in color2 (bgpreset-pat-*: Word's 10 %, large
            // checkerboard and diagonal brick patterns, 8 × 8 GIF pictures).
            if (Vml.BackgroundPicture(vml) is { } picture)
            {
                if (picture.Kind == "frame" && Presentation.Image(context.MainPart, picture.Id) is { } uri
                    && Presentation.ImageSize(context.MainPart, picture.Id, 96) is var (pixelsWide, pixelsHigh) && pixelsWide > 0 && pixelsHigh > 0)
                {
                    var (tw, th) = (Units.Css(pixelsWide * 0.12 * Px), Units.Css(pixelsHigh * 0.12 * Px));
                    var id = paint.NewId("bg");
                    return Page($"<pattern id=\"{id}\" patternUnits=\"userSpaceOnUse\" width=\"{tw}\" height=\"{th}\"><image href=\"{uri}\" width=\"{tw}\" height=\"{th}\" preserveAspectRatio=\"none\"/></pattern>",
                        $" fill=\"url(#{id})\"");
                }
                if (picture.Kind == "pattern" && context.Package.Relationship(context.MainPart, picture.Id) is { External: false } rel && context.Package.Exists(rel.Target)
                    && Html.Gif.Decode(context.Package.ReadBytes(rel.Target)) is var (bitsWide, bitsHigh, rgba) && bitsWide > 0 && bitsHigh > 0)
                {
                    var bits = new System.Text.StringBuilder($"<rect width=\"{Units.Css(bitsWide * Px)}\" height=\"{Units.Css(bitsHigh * Px)}\" fill=\"#{picture.Color2.ToLowerInvariant()}\"/>");
                    for (var y = 0; y < bitsHigh; y++)
                        for (var x = 0; x < bitsWide; x++)
                        {
                            var at = (y * bitsWide + x) * 4;
                            // A run of white bits as one rectangle.
                            if (rgba[at] + rgba[at + 1] + rgba[at + 2] < 3 * 128 || x > 0 && rgba[at - 4] + rgba[at - 3] + rgba[at - 2] >= 3 * 128)
                                continue;
                            var run = 1;
                            while (x + run < bitsWide && rgba[at + 4 * run] + rgba[at + 4 * run + 1] + rgba[at + 4 * run + 2] >= 3 * 128)
                                run++;
                            bits.Append($"<rect x=\"{Units.Css(x * Px)}\" y=\"{Units.Css(y * Px)}\" width=\"{Units.Css(run * Px)}\" height=\"{Units.Css(Px)}\" fill=\"#{picture.Color.ToLowerInvariant()}\"/>");
                        }
                    var id = paint.NewId("bg");
                    return Page($"<pattern id=\"{id}\" patternUnits=\"userSpaceOnUse\" width=\"{Units.Css(bitsWide * Px)}\" height=\"{Units.Css(bitsHigh * Px)}\" shape-rendering=\"crispEdges\">{bits}</pattern>",
                        $" fill=\"url(#{id})\"");
                }
            }
            if (Vml.BackgroundTile(vml) is { } tile && Drawing.FillReader.Read(tile.Fill, Colors) is Drawing.GradientFill tileFill)
            {
                var defs = new System.Text.StringBuilder();
                var tilePaint = paint.Fill(tileFill, tile.Width * Px, tile.Height * Px, defs);
                return Page(defs.ToString(), tilePaint);
            }
        }
        if (Vml.BackgroundFill(context.Background, Colors) is not { } fill)
            return "";
        var (cx, cy) = ((long)Math.Round(width * EmuPerPoint), (long)Math.Round(height * EmuPerPoint));
        var drawing = new XElement(Wp + "anchor",
            new XElement(Wp + "extent", new XAttribute("cx", cx), new XAttribute("cy", cy)),
            new XElement(Wp + "docPr", new XAttribute("id", 0), new XAttribute("name", "Background")),
            new XElement(Ns.A + "graphic", new XElement(Ns.A + "graphicData", new XAttribute("uri", Wps.NamespaceName),
                new XElement(Wps + "wsp", new XElement(Wps + "cNvSpPr"),
                    new XElement(Wps + "spPr",
                        new XElement(Ns.A + "xfrm", new XElement(Ns.A + "off", new XAttribute("x", 0), new XAttribute("y", 0)), new XElement(Ns.A + "ext", new XAttribute("cx", cx), new XAttribute("cy", cy))),
                        new XElement(Ns.A + "prstGeom", new XAttribute("prst", "rect"), new XElement(Ns.A + "avLst")),
                        fill, new XElement(Ns.A + "ln", new XElement(Ns.A + "noFill"))),
                    new XElement(Wps + "bodyPr")))));
        return Render(new PlacedDrawing(drawing, context.MainPart, 0, 0, width, height));
    }

    /// <summary>The fonts the drawings' text uses.</summary>
    public IEnumerable<string> UsedFonts => presentation?.UsedFonts ?? [];

    /// <summary>The text box content element of a drawing's shape (wps:txbx/w:txbxContent), or null.</summary>
    public static XElement? TextBoxContent(XElement drawing) =>
        drawing.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData")?.Element(Wps + "wsp")?.Element(Wps + "txbx")?.Element(Wml.W + "txbxContent");

    /// <summary>
    /// The text rectangle of a drawing's shape geometry (a:prstGeom with its adjustments) as distances from the
    /// shape's sides in points: the corners of rounded rectangles, the sides of ellipses (catalog: drawing.textBoxes).
    /// </summary>
    public static (double Left, double Top, double Right, double Bottom) TextRect(XElement drawing, double width, double height) =>
        drawing.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData")?.Element(Wps + "wsp") is { } shape ? ShapeTextRect(shape, width, height) : default;

    /// <summary>The text rectangle of a shape (wps:wsp) of the given size, as distances from its sides in points.</summary>
    public static (double Left, double Top, double Right, double Bottom) ShapeTextRect(XElement shape, double width, double height)
    {
        var geometry = shape.Element(Wps + "spPr")?.Element(Ns.A + "prstGeom");
        if ((string?)geometry?.Attribute("prst") is not { } preset || Drawing.Geometry.PresetGeometries.Get(preset) is not { } definition)
            return default;
        var adjustments = geometry.Element(Ns.A + "avLst")?.Elements(Ns.A + "gd")
            .Select(g => new Drawing.Geometry.Guide((string?)g.Attribute("name") ?? "", (string?)g.Attribute("fmla") ?? "")).ToList();
        var evaluated = Drawing.Geometry.GeometryEvaluator.Evaluate(definition, width * EmuPerPoint, height * EmuPerPoint, adjustments);
        // The evaluator works in pixels at 96 dpi.
        const double PointsPerPixel = 0.75;
        return (evaluated.TextLeft * PointsPerPixel, evaluated.TextTop * PointsPerPixel,
            width - evaluated.TextRight * PointsPerPixel, height - evaluated.TextBottom * PointsPerPixel);
    }

    /// <summary>
    /// Where a shape of a group or canvas stands on the page when the drawing's extent lies at (x, y) with size w × h
    /// points: groups map their children's space (a:chOff, a:chExt) onto their own box, nested groups again; a
    /// canvas's children keep its coordinates. Null for a shape not in the drawing.
    /// </summary>
    public static (double X, double Y, double W, double H, double Rotation)? ChildRect(XElement drawing, XElement shape, double x, double y, double w, double h)
    {
        var content = drawing.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData")?.Elements().FirstOrDefault();
        if (content == null || !shape.Ancestors().Contains(content))
            return null;
        double Emu(XElement? e, string name, double fallback) =>
            double.TryParse((string?)e?.Attribute(name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;
        // Page = (tx + sx · child x, ty + sy · child y), child coordinates in EMU.
        double sx = 1 / EmuPerPoint, sy = 1 / EmuPerPoint, tx = x, ty = y;
        if (content.Name == Wpg + "wgp")
        {
            var xfrm = content.Element(Wpg + "grpSpPr")?.Element(Ns.A + "xfrm");
            var (ox, oy) = (Emu(xfrm?.Element(Ns.A + "chOff"), "x", 0), Emu(xfrm?.Element(Ns.A + "chOff"), "y", 0));
            var (cw, ch) = (Emu(xfrm?.Element(Ns.A + "chExt"), "cx", w * EmuPerPoint), Emu(xfrm?.Element(Ns.A + "chExt"), "cy", h * EmuPerPoint));
            (sx, sy) = (cw > 0 ? w / cw : sx, ch > 0 ? h / ch : sy);
            (tx, ty) = (x - ox * sx, y - oy * sy);
        }
        foreach (var group in shape.Ancestors().TakeWhile(a => a != content).Reverse().Where(a => a.Name == Wpg + "grpSp"))
        {
            var xfrm = group.Element(Wpg + "grpSpPr")?.Element(Ns.A + "xfrm");
            var (gx, gy) = (Emu(xfrm?.Element(Ns.A + "off"), "x", 0), Emu(xfrm?.Element(Ns.A + "off"), "y", 0));
            var (gw, gh) = (Emu(xfrm?.Element(Ns.A + "ext"), "cx", 0), Emu(xfrm?.Element(Ns.A + "ext"), "cy", 0));
            var (ox, oy) = (Emu(xfrm?.Element(Ns.A + "chOff"), "x", gx), Emu(xfrm?.Element(Ns.A + "chOff"), "y", gy));
            var (cw, ch) = (Emu(xfrm?.Element(Ns.A + "chExt"), "cx", gw), Emu(xfrm?.Element(Ns.A + "chExt"), "cy", gh));
            var (kx, ky) = (cw > 0 ? gw / cw : 1, ch > 0 ? gh / ch : 1);
            (tx, ty) = (tx + (gx - ox * kx) * sx, ty + (gy - oy * ky) * sy);
            (sx, sy) = (sx * kx, sy * ky);
        }
        var own = shape.Element(Wps + "spPr")?.Element(Ns.A + "xfrm");
        var rotation = Emu(own, "rot", 0) / 60000;
        return (tx + Emu(own?.Element(Ns.A + "off"), "x", 0) * sx, ty + Emu(own?.Element(Ns.A + "off"), "y", 0) * sy,
            Emu(own?.Element(Ns.A + "ext"), "cx", 0) * sx, Emu(own?.Element(Ns.A + "ext"), "cy", 0) * sy, rotation);
    }

    /// <summary>A drawing's extent with its effect extents, in points.</summary>
    public static (double Width, double Height) Box(XElement drawing)
    {
        var extent = drawing.Element(Wp + "extent");
        var effect = drawing.Element(Wp + "effectExtent");
        double E(XElement? e, string name) => double.TryParse((string?)e?.Attribute(name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v / EmuPerPoint : 0;
        return (E(extent, "cx") + E(effect, "l") + E(effect, "r"), E(extent, "cy") + E(effect, "t") + E(effect, "b"));
    }

    /// <summary>The effect extents left and above the drawing's extent, in points.</summary>
    public static (double Left, double Top) EffectOffset(XElement drawing)
    {
        var effect = drawing.Element(Wp + "effectExtent");
        double E(string name) => double.TryParse((string?)effect?.Attribute(name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v / EmuPerPoint : 0;
        return (E("l"), E("t"));
    }

    /// <summary>The drawing's extent in points.</summary>
    public static (double Width, double Height) Extent(XElement drawing)
    {
        var extent = drawing.Element(Wp + "extent");
        double E(string name) => double.TryParse((string?)extent?.Attribute(name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v / EmuPerPoint : 0;
        return (E("cx"), E("cy"));
    }

    /// <summary>The HTML of a drawing whose extent's top left lies at (x, y) points on the page.</summary>
    public string Render(PlacedDrawing placed)
    {
        // Ink (w14:contentPart, catalog: drawing.ink) drawn by the ink renderer shared with PowerPoint into the box.
        var inkPart = (placed.Drawing.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData")?.Elements() ?? placed.Drawing.Elements())
            .FirstOrDefault(e => e.Name.LocalName == "contentPart") ?? placed.Drawing.Elements().FirstOrDefault(e => e.Name.LocalName == "contentPart");
        if (inkPart is { } ink && ink.Attribute(Ns.R + "id") is { } inkId)
        {
            var part = new XElement(Ns.P + "contentPart", new XAttribute(Ns.R + "id", inkId.Value),
                new XElement(P14 + "xfrm",
                    new XElement(Ns.A + "off", new XAttribute("x", (long)Math.Round(placed.X * EmuPerPoint)), new XAttribute("y", (long)Math.Round(placed.Y * EmuPerPoint))),
                    new XElement(Ns.A + "ext", new XAttribute("cx", (long)Math.Round(placed.Width * EmuPerPoint)), new XAttribute("cy", (long)Math.Round(placed.Height * EmuPerPoint)))));
            return Renderer(placed.Part).RenderElement(part, placed.Part);
        }
        var graphic = placed.Drawing.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData");
        var content = graphic?.Elements().FirstOrDefault();
        if (graphic == null || content == null)
            return "";
        var docPr = placed.Drawing.Element(Wp + "docPr");
        var (w, h) = (placed.Width, placed.Height);
        var element = Convert(content, docPr, graphic);
        if (element == null)
            return "";
        Place(element, placed.X, placed.Y, w, h);
        // VML WordArt: the box shrinks to the capitals' share of the em box, about the same turned center.
        if (double.TryParse((string?)element.Attribute(WordArtTop), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var shareTop)
            && double.TryParse((string?)element.Attribute(WordArtBottom), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var shareBottom))
        {
            var rotation = ((double?)element.Element(Ns.P + "spPr")?.Element(Ns.A + "xfrm")?.Attribute("rot") ?? 0) / 60000 * Math.PI / 180;
            var (top, bottom) = (h * shareTop, h * shareBottom);
            var shift = (top - bottom) / 2;
            var (cx, cy) = (placed.X + w / 2 - Math.Sin(rotation) * shift, placed.Y + h / 2 + Math.Cos(rotation) * shift);
            var height = h - top - bottom;
            Place(element, cx - w / 2, cy - height / 2, w, height);
            element.Attribute(WordArtTop)?.Remove();
            element.Attribute(WordArtBottom)?.Remove();
        }
        if (docPr?.Attribute("hidden")?.Value is "1" or "true")
            return "";
        return Renderer(placed.Part).RenderElement(element, placed.Part);
    }

    static readonly XNamespace P14 = "http://schemas.microsoft.com/office/powerpoint/2010/main";

    SlideRenderer Renderer(string part)
    {
        if (!renderers.TryGetValue(part, out var renderer))
        {
            var slide = new SlideContext(Presentation, part, context.Theme, ColorMap());
            renderers[part] = renderer = new SlideRenderer(slide, paint);
        }
        return renderer;
    }

    /// <summary>
    /// The document's color mapping (w:clrSchemeMapping) in PresentationML terms: bg1 and tx1 to the theme's light
    /// and dark colors unless the settings say otherwise.
    /// </summary>
    Dictionary<string, string> ColorMap()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["bg1"] = "lt1", ["tx1"] = "dk1", ["bg2"] = "lt2", ["tx2"] = "dk2",
            ["accent1"] = "accent1", ["accent2"] = "accent2", ["accent3"] = "accent3", ["accent4"] = "accent4",
            ["accent5"] = "accent5", ["accent6"] = "accent6", ["hlink"] = "hlink", ["folHlink"] = "folHlink",
        };
        if (context.Settings.Child("clrSchemeMapping") is { } mapping)
        {
            foreach (var attribute in mapping.Attributes())
            {
                var key = attribute.Name.LocalName switch { "t1" => "tx1", "t2" => "tx2", "hyperlink" => "hlink", "followedHyperlink" => "folHlink", var k => k };
                map[key] = attribute.Value switch
                {
                    "light1" => "lt1", "dark1" => "dk1", "light2" => "lt2", "dark2" => "dk2",
                    "hyperlink" => "hlink", "followedHyperlink" => "folHlink", var v => v,
                };
            }
        }
        return map;
    }

    /// <summary>Sets the converted element's own transform to the given box (points), keeping its turn and flips.</summary>
    static void Place(XElement element, double x, double y, double w, double h)
    {
        var properties = element.Element(Ns.P + "spPr") ?? element.Element(Ns.P + "grpSpPr");
        XElement? xfrm;
        if (element.Name == Ns.P + "graphicFrame")
        {
            xfrm = element.Element(Ns.P + "xfrm");
            if (xfrm == null)
                element.Add(xfrm = new XElement(Ns.P + "xfrm"));
        }
        else
        {
            if (properties == null)
                element.Add(properties = new XElement(element.Name == Ns.P + "grpSp" ? Ns.P + "grpSpPr" : Ns.P + "spPr"));
            xfrm = properties.Element(Ns.A + "xfrm");
            if (xfrm == null)
                properties.AddFirst(xfrm = new XElement(Ns.A + "xfrm"));
        }
        xfrm.Element(Ns.A + "off")?.Remove();
        xfrm.Element(Ns.A + "ext")?.Remove();
        xfrm.AddFirst(new XElement(Ns.A + "off", new XAttribute("x", (long)Math.Round(x * EmuPerPoint)), new XAttribute("y", (long)Math.Round(y * EmuPerPoint))),
            new XElement(Ns.A + "ext", new XAttribute("cx", (long)Math.Round(w * EmuPerPoint)), new XAttribute("cy", (long)Math.Round(h * EmuPerPoint))));
        // A group without a child space maps its children 1:1.
        if (element.Name == Ns.P + "grpSp" && xfrm.Element(Ns.A + "chExt") == null)
        {
            xfrm.Add(new XElement(Ns.A + "chOff", new XAttribute("x", 0), new XAttribute("y", 0)),
                new XElement(Ns.A + "chExt", new XAttribute("cx", (long)Math.Round(w * EmuPerPoint)), new XAttribute("cy", (long)Math.Round(h * EmuPerPoint))));
        }
    }

    /// <summary>A Word drawing element in PresentationML names, or null for content the slide renderer cannot draw.</summary>
    static XElement? Convert(XElement e, XElement? docPr, XElement graphicData)
    {
        if (e.Name == Pic + "pic")
        {
            return new XElement(Ns.P + "pic",
                new XElement(Ns.P + "nvPicPr", CNvPr(e.Element(Pic + "nvPicPr")?.Element(Pic + "cNvPr"), docPr),
                    new XElement(Ns.P + "cNvPicPr", Copy(e.Element(Pic + "nvPicPr")?.Element(Pic + "cNvPicPr"))), new XElement(Ns.P + "nvPr")),
                new XElement(Ns.P + "blipFill", e.Element(Pic + "blipFill")?.Attributes(), Copy(e.Element(Pic + "blipFill"))),
                new XElement(Ns.P + "spPr", e.Element(Pic + "spPr")?.Attributes(), Copy(e.Element(Pic + "spPr"))));
        }
        if (e.Name == Wps + "wsp")
        {
            var connector = e.Element(Wps + "cNvCnPr") != null;
            var nonVisual = connector
                ? new XElement(Ns.P + "nvCxnSpPr", CNvPr(e.Element(Wps + "cNvPr"), docPr), new XElement(Ns.P + "cNvCxnSpPr", Copy(e.Element(Wps + "cNvCnPr"))), new XElement(Ns.P + "nvPr"))
                : new XElement(Ns.P + "nvSpPr", CNvPr(e.Element(Wps + "cNvPr"), docPr), new XElement(Ns.P + "cNvSpPr", Copy(e.Element(Wps + "cNvSpPr"))), new XElement(Ns.P + "nvPr"));
            var shape = new XElement(connector ? Ns.P + "cxnSp" : Ns.P + "sp", nonVisual,
                new XElement(Ns.P + "spPr", e.Element(Wps + "spPr")?.Attributes(), Copy(e.Element(Wps + "spPr"))));
            if (e.Element(Wps + "style") is { } style)
                shape.Add(new XElement(Ns.P + "style", Copy(style)));
            if (e.Element(Wps + "txbxWordArt") is { } art)
                WordArt(shape, art, docPr, graphicData);
            if (e.Element(Wps + "txbx3D") is { } solid)
                shape.Add(new XElement(Ns.P + "txBody", solid.Elements()));
            return shape;
        }
        if (e.Name == Wpg + "wgp" || e.Name == Wpg + "grpSp")
        {
            var group = new XElement(Ns.P + "grpSp",
                new XElement(Ns.P + "nvGrpSpPr", CNvPr(e.Element(Wpg + "cNvPr"), docPr), new XElement(Ns.P + "cNvGrpSpPr"), new XElement(Ns.P + "nvPr")),
                new XElement(Ns.P + "grpSpPr", e.Element(Wpg + "grpSpPr")?.Attributes(), Copy(e.Element(Wpg + "grpSpPr"))));
            foreach (var child in e.Elements())
            {
                if (child.Name.LocalName is "cNvPr" or "cNvGrpSpPr" or "grpSpPr")
                    continue;
                if (Convert(child, null, graphicData) is { } converted)
                    group.Add(converted);
            }
            return group;
        }
        if (e.Name == Wpc + "wpc")
        {
            // A canvas: its background and outline (wpc:bg, wpc:whole) as a rectangle under its shapes.
            var group = new XElement(Ns.P + "grpSp",
                new XElement(Ns.P + "nvGrpSpPr", CNvPr(null, docPr), new XElement(Ns.P + "cNvGrpSpPr"), new XElement(Ns.P + "nvPr")),
                new XElement(Ns.P + "grpSpPr"));
            var bg = e.Element(Wpc + "bg");
            var whole = e.Element(Wpc + "whole");
            if (bg != null || whole != null)
            {
                // The rectangle covers the canvas: the drawing's extent, in the canvas's own coordinates.
                var extent = graphicData.Parent?.Parent?.Element(Wp + "extent");
                group.Add(new XElement(Ns.P + "sp",
                    new XElement(Ns.P + "nvSpPr", new XElement(Ns.P + "cNvPr", new XAttribute("id", 0), new XAttribute("name", "")), new XElement(Ns.P + "cNvSpPr"), new XElement(Ns.P + "nvPr")),
                    new XElement(Ns.P + "spPr",
                        new XElement(Ns.A + "xfrm", new XElement(Ns.A + "off", new XAttribute("x", 0), new XAttribute("y", 0)),
                            new XElement(Ns.A + "ext", new XAttribute("cx", (string?)extent?.Attribute("cx") ?? "0"), new XAttribute("cy", (string?)extent?.Attribute("cy") ?? "0"))),
                        new XElement(Ns.A + "prstGeom", new XAttribute("prst", "rect"), new XElement(Ns.A + "avLst")),
                        bg != null ? Copy(bg) : [new XElement(Ns.A + "noFill")], whole != null ? Copy(whole) : [])));
            }
            foreach (var child in e.Elements())
            {
                if (child.Name.LocalName is "bg" or "whole")
                    continue;
                if (Convert(child, null, graphicData) is { } converted)
                    group.Add(converted);
            }
            return group;
        }
        // Charts, SmartArt, locked canvases: a graphic frame with the graphic as it is.
        if (graphicData.Attribute("uri")?.Value is { } uri && (uri.Contains("/chart", StringComparison.Ordinal) || uri.Contains("/diagram", StringComparison.Ordinal)
            || uri.Contains("lockedCanvas", StringComparison.Ordinal) || uri.Contains("chartex", StringComparison.OrdinalIgnoreCase)))
        {
            return new XElement(Ns.P + "graphicFrame",
                new XElement(Ns.P + "nvGraphicFramePr", CNvPr(null, docPr), new XElement(Ns.P + "cNvGraphicFramePr"), new XElement(Ns.P + "nvPr")),
                new XElement(Ns.P + "xfrm"),
                new XElement(Ns.A + "graphic", new XElement(graphicData)));
        }
        return null;
    }

    /// <summary>
    /// VML WordArt (v:textpath, catalog: vml.watermarks): its text stretched over the shape as PowerPoint draws a
    /// textPlain warp, filled as the shape was; the shape itself is not drawn.
    /// </summary>
    /// <summary>
    /// A run of 3D text (catalog: run.textEffects) as a shape without fill or line whose text body has the run's scene
    /// and body, PowerPoint's 3D text (pptx text.threeD): its first baseline, at PowerPoint's single line's ascent
    /// below the top, on the run's (measured with docx/local/text-3d: Word draws such runs as pictures of their lit
    /// glyphs).
    /// </summary>
    public PlacedDrawing Text3D(string text, Layout.RunStyle style, double x, double baseline, double width)
    {
        var size = style.Size;
        var top = baseline - 1.2 * size * style.Font.Data.AscentRatio;
        var (w, h) = (width + size, 1.2 * size + size / 2);
        var fill = style.TextFill is { } own ? new XElement(own) : new XElement(Ns.A + "solidFill", new XElement(Ns.A + "srgbClr", new XAttribute("val", style.Color.ToHex().TrimStart('#'))));
        var (scene, shape3d) = style.Solid!.Value;
        var family = style.Font.Data.Family;
        var rPr = new XElement(Ns.A + "rPr", new XAttribute("lang", "en-US"), new XAttribute("sz", (long)Math.Round(size * 100)),
            new XAttribute("b", style.Bold ? 1 : 0), new XAttribute("i", style.Italic ? 1 : 0), fill,
            new XElement(Ns.A + "latin", new XAttribute("typeface", family)), new XElement(Ns.A + "ea", new XAttribute("typeface", family)),
            new XElement(Ns.A + "cs", new XAttribute("typeface", family)));
        var body = new XElement(Wps + "txbx3D",
            new XElement(Ns.A + "bodyPr", new XAttribute("wrap", "none"), new XAttribute("lIns", 0), new XAttribute("tIns", 0), new XAttribute("rIns", 0),
                new XAttribute("bIns", 0), new XAttribute("anchor", "t"), new XElement(scene), new XElement(shape3d)),
            new XElement(Ns.A + "p", new XElement(Ns.A + "pPr", new XAttribute("algn", "l")), new XElement(Ns.A + "r", rPr, new XElement(Ns.A + "t", text))));
        var (cx, cy) = ((long)Math.Round(w * EmuPerPoint), (long)Math.Round(h * EmuPerPoint));
        var drawing = new XElement(Wp + "anchor",
            new XElement(Wp + "extent", new XAttribute("cx", cx), new XAttribute("cy", cy)),
            new XElement(Wp + "docPr", new XAttribute("id", 0), new XAttribute("name", "3D text")),
            new XElement(Ns.A + "graphic", new XElement(Ns.A + "graphicData", new XAttribute("uri", Wps.NamespaceName),
                new XElement(Wps + "wsp", new XElement(Wps + "cNvSpPr"),
                    new XElement(Wps + "spPr",
                        new XElement(Ns.A + "xfrm", new XElement(Ns.A + "off", new XAttribute("x", 0), new XAttribute("y", 0)), new XElement(Ns.A + "ext", new XAttribute("cx", cx), new XAttribute("cy", cy))),
                        new XElement(Ns.A + "prstGeom", new XAttribute("prst", "rect"), new XElement(Ns.A + "avLst")),
                        new XElement(Ns.A + "noFill"), new XElement(Ns.A + "ln", new XElement(Ns.A + "noFill"))),
                    body))));
        return new PlacedDrawing(drawing, context.MainPart, x, top, w, h);
    }

    static void WordArt(XElement shape, XElement art, XElement? docPr, XElement graphicData)
    {
        var spPr = shape.Element(Ns.P + "spPr")!;
        var fill = spPr.Elements().FirstOrDefault(c => c.Name.LocalName is "solidFill" or "gradFill" or "noFill") is { } f ? new XElement(f) : new XElement(Ns.A + "solidFill", new XElement(Ns.A + "srgbClr", new XAttribute("val", "000000")));
        foreach (var paint in spPr.Elements().Where(c => c.Name.LocalName is "solidFill" or "gradFill" or "blipFill" or "noFill" or "ln").ToList())
            paint.Remove();
        spPr.Add(new XElement(Ns.A + "noFill"), new XElement(Ns.A + "ln", new XElement(Ns.A + "noFill")));
        var family = "Calibri";
        foreach (var part in ((string?)art.Attribute("style") ?? "").Split(';'))
        {
            var colon = part.IndexOf(':');
            if (colon > 0 && part[..colon].Trim().Equals("font-family", StringComparison.OrdinalIgnoreCase))
                family = part[(colon + 1)..].Trim().Trim('"', '\'', ' ').Replace("&quot;", "");
        }
        // VML fits the text's em box (ascent and descent) to the shape, PowerPoint's warp its ink: the box shrinks to
        // the capitals' share of the em box (measured with docx/local/vml-watermark).
        var font = FreeSlate.Text.FontResolver.Default.ResolveTypeface(family, false, false).Data;
        var (ascent, descent, _) = font.Hhea;
        var em = ascent + descent;
        // The warp ignores insets (as PowerPoint's), so the shape itself shrinks to the capitals once it is placed.
        if (em > 0)
        {
            shape.SetAttributeValue(WordArtTop, ((ascent - font.CapHeight) / em).ToString(System.Globalization.CultureInfo.InvariantCulture));
            shape.SetAttributeValue(WordArtBottom, (descent / em).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        shape.Add(new XElement(Ns.P + "txBody",
            new XElement(Ns.A + "bodyPr", new XAttribute("wrap", "none"), new XAttribute("lIns", 0), new XAttribute("tIns", 0), new XAttribute("rIns", 0), new XAttribute("bIns", 0),
                new XAttribute("anchor", "ctr"), new XElement(Ns.A + "prstTxWarp", new XAttribute("prst", "textPlain"), new XElement(Ns.A + "avLst"))),
            new XElement(Ns.A + "p", new XElement(Ns.A + "pPr", new XAttribute("algn", "ctr")),
                new XElement(Ns.A + "r", new XElement(Ns.A + "rPr", new XAttribute("lang", "en-US"), new XAttribute("sz", 3600), fill, new XElement(Ns.A + "latin", new XAttribute("typeface", family))),
                    new XElement(Ns.A + "t", art.Value)))));
    }

    static XElement CNvPr(XElement? own, XElement? docPr)
    {
        var source = own ?? docPr;
        var element = new XElement(Ns.P + "cNvPr", new XAttribute("id", (string?)source?.Attribute("id") ?? "0"), new XAttribute("name", (string?)source?.Attribute("name") ?? ""));
        foreach (var name in new[] { "descr", "title", "hidden" })
        {
            if ((string?)(docPr ?? own)?.Attribute(name) is { } value)
                element.SetAttributeValue(name, value);
        }
        if (source != null)
            element.Add(source.Elements().Select(c => new XElement(c)));
        // Word keeps a picture's hyperlink on wp:docPr (catalog: drawing.inline): the picture links there.
        if (own != null && docPr?.Element(Ns.A + "hlinkClick") is { } link && element.Element(Ns.A + "hlinkClick") == null)
            element.AddFirst(new XElement(link));
        return element;
    }

    /// <summary>Deep copies of an element's children (DrawingML a: elements stay as they are).</summary>
    static IEnumerable<XElement> Copy(XElement? e) => e?.Elements().Select(c => new XElement(c)) ?? [];
}
