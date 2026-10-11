using System.Xml.Linq;

namespace FreeSlate.Opc;

/// <summary>
/// Resolves mc:AlternateContent (ECMA-376 Part 3): the first mc:Choice whose required namespaces the engine
/// implements wins, otherwise mc:Fallback. Office writes newer features this way with an older equivalent as fallback.
/// </summary>
public static class MarkupCompatibility
{
    public static readonly XNamespace Mc = "http://schemas.openxmlformats.org/markup-compatibility/2006";

    /// <summary>Namespaces whose content the engine can render; everything else falls back.</summary>
    public static readonly HashSet<string> Supported =
    [
        "http://schemas.openxmlformats.org/drawingml/2006/main",
        "http://schemas.openxmlformats.org/presentationml/2006/main",
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships",
        "http://schemas.openxmlformats.org/drawingml/2006/picture",
        "http://schemas.openxmlformats.org/markup-compatibility/2006",
        // Word drawings (catalog: drawing.shapes, drawing.groups, drawing.canvas): shapes, groups and canvases of
        // Word 2010, whose VML fallback the engine does not need.
        "http://schemas.microsoft.com/office/word/2010/wordprocessingShape",
        "http://schemas.microsoft.com/office/word/2010/wordprocessingGroup",
        "http://schemas.microsoft.com/office/word/2010/wordprocessingCanvas",
        "http://schemas.microsoft.com/office/word/2010/wordprocessingDrawing",
    ];

    /// <summary>
    /// Namespaces of slide transitions and timing that the player handles (Office 2010, 2013 and morph
    /// transitions; unknown ones degrade to a fade there), chosen only for p:transition and p:timing.
    /// </summary>
    static readonly HashSet<string> SlideShow =
    [
        "http://schemas.microsoft.com/office/powerpoint/2010/main",
        "http://schemas.microsoft.com/office/powerpoint/2012/main",
        "http://schemas.microsoft.com/office/powerpoint/2015/09/main",
    ];

    /// <summary>
    /// Word's ink (wpi, catalog: drawing.ink), taken only where no fallback picture is given: Word's own picture of the
    /// ink is what it prints.
    /// </summary>
    static bool IsWordInk(XElement choice) => (choice.Attribute("Requires")?.Value ?? "")
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .All(prefix => choice.GetNamespaceOfPrefix(prefix)?.NamespaceName is "http://schemas.microsoft.com/office/word/2010/wordprocessingInk");

    /// <summary>Attributes the engine adds to fallback content about the alternative it replaces.</summary>
    public static readonly XNamespace Annotation = "urn:freeslate:annotation";

    public static void Process(XDocument document)
    {
        foreach (var alternate in document.Descendants(Mc + "AlternateContent").ToList())
        {
            if (alternate.Parent == null)
                continue;   // inside an alternative that was already removed
            if (ChartStyle(alternate) is { } style)
            {
                alternate.ReplaceWith(style);
                continue;
            }
            var chosen = alternate.Elements(Mc + "Choice").FirstOrDefault(IsSupported)
                         ?? alternate.Elements(Mc + "Choice").FirstOrDefault(IsSlideShow)
                         ?? alternate.Elements(Mc + "Choice").FirstOrDefault(IsEquation)
                         ?? alternate.Element(Mc + "Fallback")
                         ?? alternate.Elements(Mc + "Choice").FirstOrDefault(IsChartEx)
                         ?? alternate.Elements(Mc + "Choice").FirstOrDefault(IsWordInk);
            if (chosen == null)
            {
                alternate.Remove();
                continue;
            }
            if (chosen.Name == Mc + "Fallback")
            {
                AnnotateZooms(alternate, chosen);
                AnnotateModels(alternate, chosen);
            }
            alternate.ReplaceWith(chosen.Elements());
        }
    }

    /// <summary>
    /// Zoom objects (catalog: interactivity.zoom; slide, section and summary zooms of PowerPoint 2016) fall back to
    /// pictures linked to their slides: each picture, in order, gets its zoom's target (slide:sldId or
    /// section:sectionId), whether the show returns to the zoom (returnToParent, default true) and the duration of
    /// its zoom transition (transitionDur; none: the target slide's own transition).
    /// </summary>
    static void AnnotateZooms(XElement alternate, XElement fallback)
    {
        var objects = alternate.Elements(Mc + "Choice").Descendants()
            .Where(e => e.Name.LocalName is "sldZmObj" or "sectionZmObj" or "summaryZmObj").ToList();
        if (objects.Count == 0)
            return;
        var pictures = fallback.Descendants().Where(e => e.Name.LocalName == "pic").ToList();
        for (var i = 0; i < objects.Count && i < pictures.Count; i++)
        {
            var obj = objects[i];
            var target = obj.Name.LocalName == "sldZmObj" ? "slide:" + (string?)obj.Attribute("sldId") : "section:" + (string?)obj.Attribute("sectionId");
            var properties = obj.Elements().FirstOrDefault(e => e.Name.LocalName == "zmPr");
            pictures[i].SetAttributeValue(Annotation + "zoom", target);
            pictures[i].SetAttributeValue(Annotation + "zoomReturn", (string?)properties?.Attribute("returnToParent") is "0" or "false" ? "0" : "1");
            if ((string?)properties?.Attribute("transitionDur") is { Length: > 0 } duration)
                pictures[i].SetAttributeValue(Annotation + "zoomDur", duration);
        }
    }

    /// <summary>
    /// 3D models (catalog: picture.model3d, animation.pictures) fall back to PowerPoint's picture of them: each
    /// picture, in order, gets a copy of its model (am3d:model3d: the model file, the camera, the transform, the lights
    /// and the embedded animation), which the player draws while the slide's timing animates the model.
    /// </summary>
    static void AnnotateModels(XElement alternate, XElement fallback)
    {
        XNamespace am3d = "http://schemas.microsoft.com/office/drawing/2017/model3d";
        var models = alternate.Elements(Mc + "Choice").Descendants(am3d + "model3d").ToList();
        if (models.Count == 0)
            return;
        var pictures = fallback.Descendants().Where(e => e.Name.LocalName == "pic").ToList();
        for (var i = 0; i < models.Count && i < pictures.Count; i++)
            pictures[i].Add(new XElement(Annotation + "model3d", new XElement(models[i])));
    }

    /// <summary>
    /// The chart style of an Office 2010 chart (c14:style 101–148, catalog: chart.formatting): the fallback's content with
    /// its c:style (else one with the c14 style's number less 100) marked as written by Office 2010, which makes
    /// PowerPoint draw a chart without style part in the Office 2007 look of that style; null where the alternative
    /// holds no c14:style.
    /// </summary>
    static List<XElement>? ChartStyle(XElement alternate)
    {
        XNamespace c14 = "http://schemas.microsoft.com/office/drawing/2007/8/2/chart";
        XNamespace c = "http://schemas.openxmlformats.org/drawingml/2006/chart";
        if (alternate.Elements(Mc + "Choice").Elements(c14 + "style").FirstOrDefault() is not { } office2010)
            return null;
        var content = alternate.Element(Mc + "Fallback")?.Elements().Select(e => new XElement(e)).ToList() ?? [];
        if (content.FirstOrDefault(e => e.Name == c + "style") is not { } style)
        {
            style = new XElement(c + "style", int.TryParse((string?)office2010.Attribute("val"), out var value) && value > 100 ? new XAttribute("val", value - 100) : null);
            content.Add(style);
        }
        style.SetAttributeValue(Annotation + "c14", "1");
        return content;
    }

    /// <summary>
    /// Office 2010 markup the engine handles in place of its fallback: transitions and timing for the player, and ink
    /// (p:contentPart), drawn from its InkML instead of the fallback's picture.
    /// </summary>
    static bool IsSlideShow(XElement choice)
    {
        var requires = ((string?)choice.Attribute("Requires") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return choice.Elements().Any() && choice.Elements().All(e => e.Name.LocalName is "transition" or "timing" or "contentPart")
               && requires.All(prefix => choice.GetNamespaceOfPrefix(prefix) is { } ns && (Supported.Contains(ns.NamespaceName) || SlideShow.Contains(ns.NamespaceName)));
    }

    /// <summary>
    /// Shapes with equations (a14:m in their paragraphs, catalog: text.math): drawn from their Office Math instead of
    /// the fallback's picture of them, when a math font is there to lay them out.
    /// </summary>
    static bool IsEquation(XElement choice)
    {
        XNamespace a14 = "http://schemas.microsoft.com/office/drawing/2010/main";
        var requires = ((string?)choice.Attribute("Requires") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return requires.All(prefix => choice.GetNamespaceOfPrefix(prefix) is { } ns && (Supported.Contains(ns.NamespaceName) || ns == a14))
               && choice.Descendants(a14 + "m").Any() && Equations.Equation.Available;
    }

    /// <summary>
    /// Office 2016 charts (catalog: chart.ex; Requires cx1, cx2 … for the chartex namespaces of their versions): drawn
    /// natively where there is no fallback picture of them, which PowerPoint always writes.
    /// </summary>
    static bool IsChartEx(XElement choice)
    {
        var requires = ((string?)choice.Attribute("Requires") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return requires.All(prefix => choice.GetNamespaceOfPrefix(prefix) is { } ns
                                      && (Supported.Contains(ns.NamespaceName) || ns.NamespaceName.StartsWith("http://schemas.microsoft.com/office/drawing/", StringComparison.Ordinal)
                                          && ns.NamespaceName.EndsWith("/chartex", StringComparison.Ordinal)))
               && choice.Descendants().Any(e => e.Name.LocalName == "graphicData" && (string?)e.Attribute("uri") == "http://schemas.microsoft.com/office/drawing/2014/chartex");
    }

    static bool IsSupported(XElement choice)
    {
        var requires = ((string?)choice.Attribute("Requires") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return requires.All(prefix => choice.GetNamespaceOfPrefix(prefix) is { } ns && Supported.Contains(ns.NamespaceName));
    }
}
