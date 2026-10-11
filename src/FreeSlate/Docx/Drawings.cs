using System.Xml.Linq;
using FreeSlate.Html;
using FreeSlate.Pptx;

namespace FreeSlate.Docx;

/// <summary>A drawing in a run (w:drawing/wp:inline, catalog: drawing.inline): it stands in the line like a character.</summary>
public sealed record DrawingInline(XElement Drawing, string Part, RunFormat Format) : Inline(Format)
{
    /// <summary>The content of the shape's text box (wps:txbx/w:txbxContent), or null.</summary>
    public List<Block>? Text { get; init; }

    /// <summary>The box the drawing takes in the line: its extent with the effect extents around it, in points.</summary>
    public (double Width, double Height) Box => Drawings.Box(Drawing);
}

/// <summary>A floating drawing anchored at its run (w:drawing/wp:anchor, catalog: drawing.anchor); it takes no room in the line.</summary>
public sealed record AnchorInline(XElement Anchor, string Part, RunFormat Format) : Inline(Format)
{
    /// <summary>The content of the shape's text box (wps:txbx/w:txbxContent), or null.</summary>
    public List<Block>? Text { get; init; }
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

    /// <summary>The text box's body properties (insets, vertical anchor) of the shape (wps:bodyPr).</summary>
    public XElement? Body => Drawing.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData")?.Element(Drawings.Wps + "wsp")?.Element(Drawings.Wps + "bodyPr");
}

/// <summary>
/// Word drawings (catalog: drawing.inline, drawing.anchor, drawing.shapes, drawing.groups, drawing.canvas): pictures,
/// shapes, groups, canvases, charts and SmartArt are converted to PresentationML and drawn by the slide renderer with
/// the document's theme and color mapping, shared with PowerPoint (pptx features picture.*, shape.*, chart.*,
/// smartart.*).
/// </summary>
public sealed class Drawings(DocumentContext context)
{
    public static readonly XNamespace Wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    public static readonly XNamespace Wp14 = "http://schemas.microsoft.com/office/word/2010/wordprocessingDrawing";
    public static readonly XNamespace Pic = "http://schemas.openxmlformats.org/drawingml/2006/picture";
    public static readonly XNamespace Wps = "http://schemas.microsoft.com/office/word/2010/wordprocessingShape";
    public static readonly XNamespace Wpg = "http://schemas.microsoft.com/office/word/2010/wordprocessingGroup";
    public static readonly XNamespace Wpc = "http://schemas.microsoft.com/office/word/2010/wordprocessingCanvas";

    const double EmuPerPoint = 12700;

    PresentationContext? presentation;
    readonly Dictionary<string, SlideRenderer> renderers = new(StringComparer.OrdinalIgnoreCase);
    readonly SvgPaint paint = new();

    /// <summary>The presentation context the slide renderer draws with: the same package, fonts and glyphs.</summary>
    public PresentationContext Presentation => presentation ??= new PresentationContext(context.Package, context.FontLocator) { Glyphs = context.Glyphs };

    /// <summary>The fonts the drawings' text uses.</summary>
    public IEnumerable<string> UsedFonts => presentation?.UsedFonts ?? [];

    /// <summary>The text box content element of a drawing's shape (wps:txbx/w:txbxContent), or null.</summary>
    public static XElement? TextBoxContent(XElement drawing) =>
        drawing.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData")?.Element(Wps + "wsp")?.Element(Wps + "txbx")?.Element(Wml.W + "txbxContent");

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
        var graphic = placed.Drawing.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData");
        var content = graphic?.Elements().FirstOrDefault();
        if (graphic == null || content == null)
            return "";
        var docPr = placed.Drawing.Element(Wp + "docPr");
        var (w, h) = Extent(placed.Drawing);
        var element = Convert(content, docPr, graphic);
        if (element == null)
            return "";
        Place(element, placed.X, placed.Y, w, h);
        if (docPr?.Attribute("hidden")?.Value is "1" or "true")
            return "";
        return Renderer(placed.Part).RenderElement(element, placed.Part);
    }

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
                group.Add(new XElement(Ns.P + "sp",
                    new XElement(Ns.P + "nvSpPr", new XElement(Ns.P + "cNvPr", new XAttribute("id", 0), new XAttribute("name", "")), new XElement(Ns.P + "cNvSpPr"), new XElement(Ns.P + "nvPr")),
                    new XElement(Ns.P + "spPr", new XElement(Ns.A + "prstGeom", new XAttribute("prst", "rect"), new XElement(Ns.A + "avLst")),
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
        return element;
    }

    /// <summary>Deep copies of an element's children (DrawingML a: elements stay as they are).</summary>
    static IEnumerable<XElement> Copy(XElement? e) => e?.Elements().Select(c => new XElement(c)) ?? [];
}
