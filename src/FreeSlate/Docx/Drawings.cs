using System.Xml.Linq;
using FreeSlate.Html;
using FreeSlate.Pptx;
using FreeSlate.Rendering;

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

    /// <summary>
    /// Whether the drawing is a field's result (a complex or a simple field's, a hyperlink's) or an OLE object, which is
    /// a field of its own (EMBED): before Word 2013 the paragraph mark after one wider than its line goes to the next.
    /// </summary>
    public bool InField { get; init; }
}

/// <summary>A floating drawing anchored at its run (w:drawing/wp:anchor, catalog: drawing.anchor); it takes no room in the line.</summary>
public sealed record AnchorInline(XElement Anchor, string Part, RunFormat Format) : Inline(Format)
{
    /// <summary>The content of the shape's text box (wps:txbx/w:txbxContent), or null.</summary>
    public List<Block>? Text { get; init; }

    /// <summary>The text boxes of the shapes in a group or canvas (catalog: drawing.groups, drawing.canvas), by shape.</summary>
    public IReadOnlyList<(XElement Shape, List<Block> Text)>? ChildText { get; init; }

    /// <summary>The left and right indent of the paragraph the drawing is anchored in, in points.</summary>
    public (double Left, double Right) Indents { get; set; }
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

    /// <summary>
    /// How far an inline drawing moved across with its line onto Word's device and by the PDF's growth: its text box's
    /// text counts its dots from where the drawing stood before (PageRenderer.TextBox).
    /// </summary>
    public double DeviceShift { get; init; }

    /// <summary>The text boxes of the shapes in a group or canvas (catalog: drawing.groups, drawing.canvas), by shape.</summary>
    public IReadOnlyList<(XElement Shape, List<Block> Text)>? ChildText { get; init; }

    /// <summary>
    /// The area of the page the drawing's shapes are cut at, its text box's text not (a table cell's inside, see
    /// Paginator.Placed), or null.
    /// </summary>
    public (double X, double Y, double Width, double Height)? Clip { get; init; }

    /// <summary>Whether the drawing is sized to its text already (PageRenderer.FitToText), which it is only once.</summary>
    public bool FittedToText { get; init; }

    /// <summary>Whether the bottom margin moved the drawing up, as Word 2013 holds one that text goes around (Paginator.Unpinned).</summary>
    public bool HeldUp { get; init; }

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

    /// <summary>
    /// A mark on converted VML WordArt: the shares of its shape left of, above, right of and below the text's ink
    /// (fractions of its width and height).
    /// </summary>
    static readonly XName WordArtInk = XNamespace.Get("urn:freeslate:wordart") + "ink";

    PresentationContext? presentation;
    readonly Dictionary<string, SlideRenderer> renderers = new(StringComparer.OrdinalIgnoreCase);
    readonly SvgPaint paint = new() { PrintedPictures = true, ExactDashes = true };

    /// <summary>The presentation context the slide renderer draws with: the same package, fonts and glyphs.</summary>
    public PresentationContext Presentation => presentation ??= new PresentationContext(context.Package, context.FontLocator)
    {
        Glyphs = context.Glyphs,
        Culture = culture ?? System.Globalization.CultureInfo.CurrentCulture,
        Word = true,
    };

    /// <summary>The paint of drawings and text effects: one counter for the ids of their gradients and patterns.</summary>
    public SvgPaint Paint => paint;

    /// <summary>The document's theme colors with its color mapping (w:clrSchemeMapping).</summary>
    public Drawing.ColorContext Colors => colors ??= new Drawing.ColorContext(context.Theme, ColorMap());

    Drawing.ColorContext? colors;

    /// <summary>
    /// The page background (catalog: document.background): the fill of w:background or its v:background on a
    /// rectangle the size of the page, behind everything; Word prints it whether or not w:displayBackgroundShape is
    /// set (measured with docx/local/misc-nothing, misc-nobg). A VML background's paint is still markup.
    /// </summary>
    public List<SceneItem> Background(double width, double height)
    {
        // Word's VML gradients repeat over the page in tiles (see Vml.BackgroundGradient, BackgroundTile).
        if (context.Background?.Element(Vml.V + "background") is { } vml)
        {
            const double Px = 96.0 / 72;
            var (w, h) = (Units.Css(width * Px), Units.Css(height * Px));
            List<SceneItem> Page(string defs, string paint) =>
                [new BoxItem((width * Px, height * Px), null, "", [new SvgItem(width * Px, height * Px,
                    [new DefsItem(defs), new MarkupItem($"<rect width=\"{w}\" height=\"{h}\"{paint} shape-rendering=\"crispEdges\"/>")])])];
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
                    var (tw, th) = (Units.Css(pixelsWide * WordDevice.Dot * Px), Units.Css(pixelsHigh * WordDevice.Dot * Px));
                    var id = paint.NewId("bg");
                    return Page($"<pattern id=\"{id}\" patternUnits=\"userSpaceOnUse\" width=\"{tw}\" height=\"{th}\"><image href=\"{uri}\" width=\"{tw}\" height=\"{th}\" preserveAspectRatio=\"none\"/></pattern>",
                        $" fill=\"url(#{id})\"");
                }
                if (picture.Kind == "pattern" && context.Package.Relationship(context.MainPart, picture.Id) is { External: false } rel && context.Package.Exists(rel.Target)
                    && Html.Gif.Decode(context.Package.ReadBytes(rel.Target)) is var (bitsWide, bitsHigh, rgba) && bitsWide > 0 && bitsHigh > 0)
                {
                    // An 8 × 8 picture other than Word's 48 patterns prints a bit a 600 dpi dot, kept at 150 dpi in Word's
                    // PDF: a tile of 2 × 2 pixels of 0.48 pt, of the bits at (0, 0), (4, 0), (0, 4) and (4, 4) (measured
                    // with docx/local/ypf-a to ypf-f: one dot, a diagonal, half white, four dots; Word's patterns, as
                    // its own documents hold them, are PatternPresets' first 48, ypf-c's checkerboard among them;
                    // docx/external/libreoffice/extras-tiledrendering/tdf159626_yellowPatternFillB).
                    bool White(int x, int y) => rgba[(y * bitsWide + x) * 4] + rgba[(y * bitsWide + x) * 4 + 1] + rgba[(y * bitsWide + x) * 4 + 2] >= 3 * 128;
                    // A tile so fine prints as the mean of its pixels (Word's rasterized page: 239,239,48 for one silver
                    // pixel of four on yellow, 224,224,96 for two), which is what is drawn: browsers drop pattern
                    // cells below a pixel.
                    if (bitsWide == 8 && bitsHigh == 8 && !WordPattern(White))
                    {
                        var share = new[] { (0, 0), (4, 0), (0, 4), (4, 4) }.Count(b => White(b.Item1, b.Item2)) / 4.0;
                        var (fore, back) = (FreeSlate.Drawing.RgbColor.FromHex(picture.Color), FreeSlate.Drawing.RgbColor.FromHex(picture.Color2));
                        var mean = new FreeSlate.Drawing.RgbColor(fore.R * share + back.R * (1 - share), fore.G * share + back.G * (1 - share),
                            fore.B * share + back.B * (1 - share));
                        return Page("", $" fill=\"{mean.ToHex()}\"");
                    }
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
            return [];
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
    /// <summary>
    /// Whether an 8 × 8 picture is one of Word's 48 patterns, its white bits a preset's (measured with Word's own
    /// documents of the 48 patterns: the presets of PatternPresets but horz, vert, cross, dnDiag, upDiag and diagCross).
    /// </summary>
    static bool WordPattern(Func<int, int, bool> white) =>
        FreeSlate.Drawing.PatternPresets.Tiles.Where(p => p.Key is not ("horz" or "vert" or "cross" or "dnDiag" or "upDiag" or "diagCross"))
            .Any(p => Enumerable.Range(0, 8).All(y => Enumerable.Range(0, 8).All(x => white(x, y) == ((p.Value[y] >> (7 - x) & 1) == 1))));

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
            // Without a:chOff the children's space begins at 0 0, so that the group's offset adds to theirs, nested
            // groups' too (docx/external/libreoffice/extras-ooxmlexport/dml-groupshape-childposition: shapes in a group
            // in a group, all without a:chOff and a:chExt, stand where the three offsets add up, within 0.1 pt).
            var (ox, oy) = (Emu(xfrm?.Element(Ns.A + "chOff"), "x", 0), Emu(xfrm?.Element(Ns.A + "chOff"), "y", 0));
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

    /// <summary>
    /// The effect extents Word goes by. Those of a group holding a text box that does not wrap (wrap none, as
    /// WordArt's) Word works out anew, whatever the document says: none left and above, on the right and below its
    /// shapes' widest outline and 0.05 pt, at least 0.49 pt (measured with docx/local/ge-*, gh-*: a group with 9 pt
    /// stored on every side stands at the line's start, the text after it 0.49 pt after its extent, with a 2 pt
    /// outline 2.05 pt, 0 stored the same; with a text box that wraps the stored extents hold;
    /// docx/external/libreoffice/extras-ooxmlexport/Tdf147485, tdf149546).
    /// </summary>
    static XElement? EffectExtent(XElement drawing)
    {
        var stored = drawing.Element(Wp + "effectExtent");
        if (drawing.Attribute(Vml.Converted) != null || drawing.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData")?.Element(Wpg + "wgp") is not { } group
            || !group.Descendants(Wps + "wsp").Any(s => s.Element(Wps + "txbx") != null && (string?)s.Element(Wps + "bodyPr")?.Attribute("wrap") == "none"))
            return stored;
        var outline = group.Descendants(Wps + "spPr").Select(p => p.Element(Ns.A + "ln")).Where(l => l != null && l.Element(Ns.A + "noFill") == null)
            .Select(l => double.TryParse((string?)l!.Attribute("w"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0)
            .DefaultIfEmpty(0).Max();
        var side = Math.Max(6223, outline + 635);
        return new XElement(Wp + "effectExtent", new XAttribute("l", 0), new XAttribute("t", 0), new XAttribute("r", side), new XAttribute("b", side));
    }

    /// <summary>
    /// A drawing's extent with its effect extents, in points; an inline drawing turned by 45 degrees up to 135 (or 225 up
    /// to 315) takes its extent's height across and its width down from Word 2010 on (measured with docx/local/ri-*: a
    /// 100 x 60 pt rectangle at 45, 60, 90, 120 and 250 degrees makes a line 100 pt high and 60 pt wide, at 0, 30 and
    /// 135 degrees one 60 pt high; docx/external/libreoffice/extras-ooxmlexport/tdf103001 in mode 14); Word 2007's
    /// documents hold the turned bounds in their effect extents (extras-ooxmlimport/tdf143475_rotatedWord2007imageInline:
    /// a picture turned 75 degrees in a box of its width and 178.5 pt high). The reader marks those that swap
    /// (<see cref="SwappedTurn"/>).
    /// </summary>
    public static (double Width, double Height) Box(XElement drawing)
    {
        var extent = drawing.Element(Wp + "extent");
        var effect = EffectExtent(drawing);
        double E(XElement? e, string name) => double.TryParse((string?)e?.Attribute(name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v / EmuPerPoint : 0;
        var (width, height) = InlineSwapped(drawing) ? (E(extent, "cy"), E(extent, "cx")) : (E(extent, "cx"), E(extent, "cy"));
        return (width + E(effect, "l") + E(effect, "r"), height + E(effect, "t") + E(effect, "b"));
    }

    /// <summary>Marks an inline drawing whose turn swaps its box's sides (see <see cref="Box"/>).</summary>
    public static readonly XName SwappedTurn = XNamespace.Get("urn:freeslate:drawing") + "swapped";

    /// <summary>Whether an inline drawing's box swaps its sides (see <see cref="Box"/>).</summary>
    public static bool InlineSwapped(XElement drawing) => drawing.Attribute(SwappedTurn) != null;

    /// <summary>Whether an inline drawing is turned by 45 degrees up to 135, or 225 up to 315.</summary>
    public static bool TurnedAcross(XElement drawing)
    {
        if (drawing.Name != Wp + "inline" || drawing.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData")?.Elements().FirstOrDefault() is not { } content)
            return false;
        var xfrm = content.Elements().FirstOrDefault(e => e.Name.LocalName is "spPr" or "grpSpPr")?.Element(Ns.A + "xfrm");
        if (!long.TryParse((string?)xfrm?.Attribute("rot"), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var rot))
            return false;
        var degrees = ((rot / 60000.0) % 360 + 360) % 360;
        return degrees is >= 45 and < 135 or >= 225 and < 315;
    }

    /// <summary>The effect extents left and above the drawing's extent, in points.</summary>
    public static (double Left, double Top) EffectOffset(XElement drawing)
    {
        var effect = EffectExtent(drawing);
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

    /// <summary>The scene items of a drawing whose extent's top left lies at (x, y) points on the page.</summary>
    public List<SceneItem> Render(PlacedDrawing placed)
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
            return Renderer(placed.Part).RenderItems(part, placed.Part);
        }
        var graphic = placed.Drawing.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData");
        var content = graphic?.Elements().FirstOrDefault();
        if (graphic == null || content == null)
            return [];
        var docPr = placed.Drawing.Element(Wp + "docPr");
        var (w, h) = (placed.Width, placed.Height);
        var element = Convert(content, docPr, graphic);
        if (element == null)
            return [];
        Place(element, placed.X, placed.Y, w, h);
        // VML WordArt: the box shrinks to the ink's share of the text's em boxes and advances, about the same turned
        // (and flipped) center.
        if (((string?)element.Attribute(WordArtInk))?.Split(' ').Select(v => double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray() is [var shareLeft, var shareTop, var shareRight, var shareBottom])
        {
            var xfrm = element.Element(Ns.P + "spPr")?.Element(Ns.A + "xfrm");
            var rotation = ((double?)xfrm?.Attribute("rot") ?? 0) / 60000 * Math.PI / 180;
            var (left, top, right, bottom) = (w * shareLeft, h * shareTop, w * shareRight, h * shareBottom);
            var (dx, dy) = ((left - right) / 2 * ((string?)xfrm?.Attribute("flipH") is "1" or "true" ? -1 : 1), (top - bottom) / 2 * ((string?)xfrm?.Attribute("flipV") is "1" or "true" ? -1 : 1));
            var (cx, cy) = (placed.X + w / 2 + Math.Cos(rotation) * dx - Math.Sin(rotation) * dy, placed.Y + h / 2 + Math.Sin(rotation) * dx + Math.Cos(rotation) * dy);
            var (width, height) = (w - left - right, h - top - bottom);
            Place(element, cx - width / 2, cy - height / 2, width, height);
            element.Attribute(WordArtInk)?.Remove();
        }
        if (docPr?.Attribute("hidden")?.Value is "1" or "true")
            return [];
        return Renderer(placed.Part).RenderItems(element, placed.Part);
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
        // A graphic frame in a group or canvas (charts): a frame in its group's coordinates (docx/external/libreoffice/
        // extras-ooxmlimport/tdf124398: a bar chart under a text box in a group).
        if (e.Name == Wpg + "graphicFrame" || e.Name == Wpc + "graphicFrame")
        {
            if (e.Element(Ns.A + "graphic") is not { } frameGraphic)
                return null;
            var frameXfrm = e.Element(e.Name.Namespace + "xfrm");
            return new XElement(Ns.P + "graphicFrame",
                new XElement(Ns.P + "nvGraphicFramePr", CNvPr(e.Element(e.Name.Namespace + "cNvPr"), docPr), new XElement(Ns.P + "cNvGraphicFramePr"), new XElement(Ns.P + "nvPr")),
                new XElement(Ns.P + "xfrm", frameXfrm?.Attributes(), frameXfrm?.Elements().Select(x => new XElement(x))),
                new XElement(frameGraphic));
        }
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
            // A group without a child space maps its children 1:1 from 0 0 (see ChildRect), the slide renderer's
            // default being the group's own offset.
            if (group.Element(Ns.P + "grpSpPr")?.Element(Ns.A + "xfrm") is { } groupXfrm && groupXfrm.Element(Ns.A + "ext") is { } groupExt)
            {
                if (groupXfrm.Element(Ns.A + "chOff") == null)
                    groupXfrm.Add(new XElement(Ns.A + "chOff", new XAttribute("x", 0), new XAttribute("y", 0)));
                if (groupXfrm.Element(Ns.A + "chExt") == null)
                    groupXfrm.Add(new XElement(Ns.A + "chExt", new XAttribute("cx", (string?)groupExt.Attribute("cx") ?? "0"), new XAttribute("cy", (string?)groupExt.Attribute("cy") ?? "0")));
            }
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

    /// <summary>
    /// The bent text of a WordArt text box (catalog: drawing.textBoxes) as a shape without fill or line over it: the
    /// box less its insets, turned and flipped as the box, its text body (wps:txbx3D) without insets of its own.
    /// </summary>
    public PlacedDrawing WordArtText(XElement body, PlacedDrawing box, double left, double top, double right, double bottom)
    {
        var (w, h) = (Math.Max(1, box.Width - left - right), Math.Max(1, box.Height - top - bottom));
        var text = new XElement(body);
        if (text.Element(Ns.A + "bodyPr") is { } bodyPr)
        {
            foreach (var inset in new[] { "lIns", "tIns", "rIns", "bIns" })
                bodyPr.SetAttributeValue(inset, 0);
        }
        var turn = box.Drawing.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData")?.Element(Wps + "wsp")?.Element(Wps + "spPr")?.Element(Ns.A + "xfrm");
        var (cx, cy) = ((long)Math.Round(w * EmuPerPoint), (long)Math.Round(h * EmuPerPoint));
        var xfrm = new XElement(Ns.A + "xfrm", turn?.Attributes().Where(a => a.Name.LocalName is "rot" or "flipH" or "flipV"),
            new XElement(Ns.A + "off", new XAttribute("x", 0), new XAttribute("y", 0)), new XElement(Ns.A + "ext", new XAttribute("cx", cx), new XAttribute("cy", cy)));
        var drawing = new XElement(Wp + "anchor",
            new XElement(Wp + "extent", new XAttribute("cx", cx), new XAttribute("cy", cy)),
            new XElement(Wp + "docPr", new XAttribute("id", 0), new XAttribute("name", "WordArt")),
            new XElement(Ns.A + "graphic", new XElement(Ns.A + "graphicData", new XAttribute("uri", Wps.NamespaceName),
                new XElement(Wps + "wsp", new XElement(Wps + "cNvSpPr"),
                    new XElement(Wps + "spPr", xfrm, new XElement(Ns.A + "prstGeom", new XAttribute("prst", "rect"), new XElement(Ns.A + "avLst")),
                        new XElement(Ns.A + "noFill"), new XElement(Ns.A + "ln", new XElement(Ns.A + "noFill"))),
                    text))));
        return new PlacedDrawing(drawing, box.Part, box.X + left, box.Y + top, w, h) { Behind = box.Behind, Order = box.Order };
    }

    /// <summary>
    /// VML WordArt (v:textpath, catalog: vml.watermarks): its text bent over the shape as PowerPoint draws the warp
    /// of the shape type (textPlain stretched over it), text converted from old WordArt, a paragraph per line, filled
    /// as the shape was; the shape itself is not drawn.
    /// </summary>
    static void WordArt(XElement shape, XElement art, XElement? docPr, XElement graphicData)
    {
        var spPr = shape.Element(Ns.P + "spPr")!;
        // Pictures fill the glyphs too (docx/external/libreoffice/tdf153258_VML_import_WordArt_detection: a purple
        // mesh tiled over "zero", "one" and "more").
        var fill = spPr.Elements().FirstOrDefault(c => c.Name.LocalName is "solidFill" or "gradFill" or "pattFill" or "blipFill" or "noFill") is { } f ? new XElement(f) : new XElement(Ns.A + "solidFill", new XElement(Ns.A + "srgbClr", new XAttribute("val", "000000")));
        // The shape's outline and shadow fall on the glyphs (docx/external/libreoffice/extras-ooxmlexport/fdo79540:
        // "February 2013" in red outlined 2 pt in dark blue, "Pay $12 Dues!" in orange outlined in VML's default 0.75 pt
        // of black).
        var outline = spPr.Element(Ns.A + "ln") is { } ln && ln.Element(Ns.A + "noFill") == null ? new XElement(ln) : null;
        var effects = spPr.Element(Ns.A + "effectLst") is { } shadow ? new XElement(shadow.Name, shadow.Attributes(), shadow.Nodes(), new XAttribute(Pptx.TextWarp.ShapeEffects, 1)) : null;
        foreach (var paint in spPr.Elements().Where(c => c.Name.LocalName is "solidFill" or "gradFill" or "pattFill" or "blipFill" or "noFill" or "ln" or "effectLst").ToList())
            paint.Remove();
        spPr.Add(new XElement(Ns.A + "noFill"), new XElement(Ns.A + "ln", new XElement(Ns.A + "noFill")));
        var (family, bold, italic) = WordArtFont((string?)art.Attribute("style"));
        var warp = (string?)art.Attribute("warp") ?? "textPlain";
        var font = WordArtTypeface(family, bold, italic).Data;
        family = font.Family;
        var lines = art.Value.Split('\n');
        // The warp ignores insets (as PowerPoint's), so the shape itself shrinks to the ink once it is placed.
        if (warp == "textPlain" && art.Attribute("trim") == null && PlainWordArt(art.Value, (string?)art.Attribute("style"), false, 1, 1) is { } plain)
            shape.SetAttributeValue(WordArtInk, string.Join(" ", plain.Shares.Select(v => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture))));
        var adjustments = art.Attributes().Where(a => a.Name.LocalName.StartsWith("adj", StringComparison.Ordinal)).ToList();
        if (art.Value.Length == 0)
            return;
        var guides = adjustments.Select(a => new XElement(Ns.A + "gd", new XAttribute("name", adjustments.Count == 1 ? "adj" : a.Name.LocalName), new XAttribute("fmla", "val " + a.Value)));
        // Lines at VML's pitch, GDI's height (36 pt the size of the text body's runs), plain or bent (docx/external/
        // libreoffice/extras-ooxmlexport/fdo74110: three lines of Arial in textInflateTop 1.117 em apart, not 1.2).
        var pitch = lines.Length > 1
            ? new XElement(Ns.A + "lnSpc", new XElement(Ns.A + "spcPts", new XAttribute("val", (long)Math.Round((font.WinAscent + font.WinDescent) * 3600))))
            : null;
        shape.Add(new XElement(Ns.P + "txBody",
            new XElement(Ns.A + "bodyPr", new XAttribute("wrap", "none"), new XAttribute("lIns", 0), new XAttribute("tIns", 0), new XAttribute("rIns", 0), new XAttribute("bIns", 0),
                new XAttribute("anchor", "ctr"), new XAttribute("fromWordArt", 1),
                new XElement(Ns.A + "prstTxWarp", new XAttribute("prst", warp), new XAttribute(Pptx.TextWarp.TextPath, 1),
                    art.Attribute("trim") != null ? new XAttribute(Pptx.TextWarp.Trimmed, 1) : null,
                    new XElement(Ns.A + "avLst", guides), art.Element(Pptx.TextWarp.OwnPaths))),
            lines.Select(line => new XElement(Ns.A + "p", new XElement(Ns.A + "pPr", new XAttribute("algn", "ctr"), pitch),
                line.Length == 0
                    ? new XElement(Ns.A + "endParaRPr", new XAttribute("lang", "en-US"), new XAttribute("sz", 3600))
                    // Kerned only where its text path says v-text-kern (docx/external/libreoffice/extras-ooxmlexport/
                    // watermark-font: "TestFont" in Arial without the T-e pair; docx/local/vwa-f-arial); kern is the size
                    // kerning begins at, in hundredths of a point.
                    : new XElement(Ns.A + "r", new XElement(Ns.A + "rPr", new XAttribute("lang", "en-US"), new XAttribute("sz", 3600), new XAttribute("b", bold ? 1 : 0),
                        new XAttribute("kern", Kerned((string?)art.Attribute("style")) ? 0 : 400000),
                        new XAttribute("i", italic ? 1 : 0), outline != null ? new XElement(outline) : null, new XElement(fill),
                        effects != null ? new XElement(effects) : null, new XElement(Ns.A + "latin", new XAttribute("typeface", family))),
                        new XElement(Ns.A + "t", line))))));
    }

    /// <summary>
    /// VML WordArt's typeface: its family where it is installed, else Arial, as GDI maps names it lacks (docx/external/
    /// libreoffice/extras-uiwriter/watermark: "DejaVu Sans Light" in Arial, not in Verdana as PowerPoint takes the name;
    /// extras-ooxmlexport/watermark-font: "DejaVu Serif" in Arial too); GDI does not see Office's cloud fonts
    /// (extras-ooxmlexport/fdo74110: "Noto Sans", there only in Office's font cache, in Arial).
    /// </summary>
    static FreeSlate.Text.ResolvedFont WordArtTypeface(string family, bool bold, bool italic) =>
        (FreeSlate.Fonts.FontLocator.Default.IsInstalled(family) ? FreeSlate.Text.FontResolver.Default.TryResolveFamily(family, bold, italic) : null)
        ?? FreeSlate.Text.FontResolver.Default.Resolve("Arial", bold, italic);

    /// <summary>Whether VML WordArt's text path style kerns its text (v-text-kern:t, its last).</summary>
    static bool Kerned(string? style) => (style ?? "").Split(';').LastOrDefault(part => part.Split(':') is [var name, _]
        && name.Trim().Equals("v-text-kern", StringComparison.OrdinalIgnoreCase))?.Split(':')[1].Trim() is "t" or "true";

    /// <summary>The font of VML WordArt's text path style (font-family, font-weight, font-style), Calibri by default.</summary>
    static (string Family, bool Bold, bool Italic) WordArtFont(string? style)
    {
        var (family, bold, italic) = ("Calibri", false, false);
        foreach (var part in (style ?? "").Split(';'))
        {
            var colon = part.IndexOf(':');
            if (colon <= 0)
                continue;
            var (name, value) = (part[..colon].Trim().ToLowerInvariant(), part[(colon + 1)..].Trim());
            if (name == "font-family")
                family = value.Trim('"', '\'', ' ').Replace("&quot;", "");
            else if (name == "font-weight")
                bold = value is "bold" or "bolder" or "600" or "700" or "800" or "900";
            else if (name == "font-style")
                italic = value is "italic" or "oblique";
        }
        return (family, bold, italic);
    }

    /// <summary>
    /// VML WordArt in plain text (textPlain) as VML lays it out: each line's em its share of the shape's height, the
    /// baseline its GDI descent above the line's bottom, lines following each other by GDI's height, the longest
    /// line's advance the shape's width and the others centered at its scale; trimmed text (trim) its ink fitted to the
    /// shape instead. Gives the shares of the shape left of, above, right of and below the ink (fractions of its width
    /// and height, none when trimmed), which PowerPoint's warp fills, and the glyphs' outlines in a shape of
    /// <paramref name="width"/> by <paramref name="height"/> (points from its top left corner); null without ink
    /// (measured with docx/local/vwa-f-*: "DRAFT" and "Typeface" in Arial, Segoe UI, Cambria, Verdana, Georgia and
    /// Arial Black 1 em to the shapes' 100 pt, their side bearings left at the sides; vwa-calibri: Calibri's baseline
    /// 0.2686 em up, its usWinDescent, not hhea's 0.25; vwa-calibri2, vwa-times2, vwa-segoe3: lines 1.221, 1.107 and
    /// 1.330 em apart; vwa-plain: trimmed ink filling the shape).
    /// </summary>
    internal static (double[] Shares, List<(double X, double Y)> Outline)? PlainWordArt(string text, string? style, bool trim, double width, double height)
    {
        var (family, bold, italic) = WordArtFont(style);
        var font = WordArtTypeface(family, bold, italic).Data;
        var lines = text.Split('\n');
        var (units, pitch, descent) = ((double)font.UnitsPerEm, font.WinAscent + font.WinDescent, font.WinDescent);
        var shaped = lines.Select(line => font.Glyphs(line, false)).ToList();
        var advances = shaped.Select(glyphs => glyphs.Sum(g => g.Advance)).ToList();
        var widest = advances.Max();
        // The ink in em from the first line's origin, y up: its sides, the first line's top and the last line's bottom.
        var (inkLeft, inkRight, inkTop, inkBottom) = (double.MaxValue, double.MinValue, (double?)null, (double?)null);
        var outline = new List<(double X, double Y)>();
        for (var i = 0; i < shaped.Count; i++)
        {
            var pen = (widest - advances[i]) / 2;
            foreach (var g in shaped[i])
            {
                var (l, r) = font.GlyphSpan(g.Id);
                var (b, t) = font.GlyphBounds(g.Id);
                if (r > l)
                    (inkLeft, inkRight) = (Math.Min(inkLeft, pen + g.OffsetX + l / units), Math.Max(inkRight, pen + g.OffsetX + r / units));
                if (t > b && i == 0)
                    inkTop = Math.Max(inkTop ?? double.MinValue, t / units);
                if (t > b && i == shaped.Count - 1)
                    inkBottom = Math.Min(inkBottom ?? double.MaxValue, b / units - (shaped.Count - 1) * pitch);
                // The outline y down in em.
                foreach (var contour in Pptx.TextWarp.Contours(font.Outline(g.Id), pen + g.OffsetX, i * pitch - g.OffsetY, 1 / units))
                    outline.AddRange(contour);
                pen += g.Advance;
            }
        }
        if (widest <= 0 || inkRight <= inkLeft || outline.Count == 0)
            return null;
        var (top, bottom) = (inkTop ?? font.CapHeight, inkBottom ?? -(shaped.Count - 1) * pitch);
        var em = 1 / (1 + (lines.Length - 1) * pitch);
        var shares = trim ? new double[4]
            : [inkLeft / widest, (1 - descent - top) * em, (widest - inkRight) / widest, (descent + bottom + (lines.Length - 1) * pitch) * em];
        // The ink's rectangle in em (y down) onto the shape less its shares.
        var (x0, x1, y0, y1) = (shares[0] * width, width - shares[2] * width, shares[1] * height, height - shares[3] * height);
        var placed = outline.Select(p => (x0 + (p.X - inkLeft) / (inkRight - inkLeft) * (x1 - x0), y0 + (p.Y + top) / Math.Max(1e-9, top - bottom) * (y1 - y0))).ToList();
        return (shares, placed);
    }

    /// <summary>
    /// VML WordArt along a path (arches: see Pptx.TextWarp.FollowsPaths) as Word draws it, its lines laid out at 36 pt
    /// as the text body's runs: its glyphs' outlines bent along the warp in a shape of <paramref name="width"/> by
    /// <paramref name="height"/> (contours, points from its top left corner); null for other warps or without ink.
    /// </summary>
    internal static List<List<(double X, double Y)>>? PathWordArt(XElement art, double width, double height)
    {
        const double Size = 36, PxPerPt = 96.0 / 72;
        var warp = new XElement(Ns.A + "prstTxWarp", new XAttribute("prst", (string?)art.Attribute("warp") ?? "textPlain"),
            art.Element(Pptx.TextWarp.OwnPaths) is { } own ? new XElement(own) : null);
        if (Pptx.TextWarp.Definition(warp) is not { } definition || !Pptx.TextWarp.FollowsPaths(definition) || art.Value.Length == 0)
            return null;
        var style = (string?)art.Attribute("style");
        var (family, bold, italic) = WordArtFont(style);
        var font = WordArtTypeface(family, bold, italic).Data;
        var (kerned, scale, pitch) = (Kerned(style), Size * PxPerPt / font.UnitsPerEm, (font.WinAscent + font.WinDescent) * Size * PxPerPt);
        var glyphs = new List<Pptx.TextWarp.Glyph>();
        var widths = new List<double>();
        var lines = art.Value.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            // Each line from the box's left, centered by the warp's alignment.
            var position = 0.0;
            foreach (var g in font.Glyphs(lines[i], kerned))
            {
                var advance = FreeSlate.Text.FontData.MasterUnits(g.Advance * Size) * PxPerPt;
                if (font.Outline(g.Id) is { Length: > 0 } outline)
                {
                    var contours = Pptx.TextWarp.Contours(outline, position + g.OffsetX * Size * PxPerPt, i * pitch - g.OffsetY * Size * PxPerPt, scale);
                    glyphs.Add(new Pptx.TextWarp.Glyph(contours, i, i * pitch) { Middle = position + advance / 2 });
                }
                position += advance + FreeSlate.Text.FontData.MasterUnits(g.Kerning * Size) * PxPerPt;
            }
            widths.Add(position);
        }
        if (glyphs.Count == 0)
            return null;
        var guides = art.Attributes().Where(a => a.Name.LocalName.StartsWith("adj", StringComparison.Ordinal)).ToList();
        var adjustments = guides.Select(a => new FreeSlate.Drawing.Geometry.Guide(guides.Count == 1 ? "adj" : a.Name.LocalName, "val " + a.Value));
        var drop = (0.5 - font.WinDescent) * Size * PxPerPt;
        var laidOut = Enumerable.Range(0, lines.Length).Select(i => new Pptx.TextWarp.Line(i * pitch, drop, i * pitch - font.WinAscent * Size * PxPerPt, i * pitch + font.WinDescent * Size * PxPerPt)).ToList();
        var warped = Pptx.TextWarp.Apply(definition, adjustments, (0, 0, width * PxPerPt, height * PxPerPt), glyphs, fromWordArt: true,
            i => ("ctr", 0, 0, widths[i]), laidOut);
        return warped.SelectMany(g => g).Select(c => c.Select(p => (p.X / PxPerPt, p.Y / PxPerPt)).ToList()).ToList();
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
