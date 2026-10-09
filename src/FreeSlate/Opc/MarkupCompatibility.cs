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

    /// <summary>Attributes the engine adds to fallback content about the alternative it replaces.</summary>
    public static readonly XNamespace Annotation = "urn:freeslate:annotation";

    public static void Process(XDocument document)
    {
        foreach (var alternate in document.Descendants(Mc + "AlternateContent").ToList())
        {
            if (alternate.Parent == null)
                continue;   // inside an alternative that was already removed
            var chosen = alternate.Elements(Mc + "Choice").FirstOrDefault(IsSupported)
                         ?? alternate.Elements(Mc + "Choice").FirstOrDefault(IsSlideShow)
                         ?? alternate.Elements(Mc + "Choice").FirstOrDefault(IsEquation)
                         ?? alternate.Element(Mc + "Fallback");
            if (chosen == null)
            {
                alternate.Remove();
                continue;
            }
            if (chosen.Name == Mc + "Fallback")
                AnnotateZooms(alternate, chosen);
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

    static bool IsSupported(XElement choice)
    {
        var requires = ((string?)choice.Attribute("Requires") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return requires.All(prefix => choice.GetNamespaceOfPrefix(prefix) is { } ns && Supported.Contains(ns.NamespaceName));
    }
}
