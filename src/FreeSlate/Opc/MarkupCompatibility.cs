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

    public static void Process(XDocument document)
    {
        foreach (var alternate in document.Descendants(Mc + "AlternateContent").ToList())
        {
            if (alternate.Parent == null)
                continue;   // inside an alternative that was already removed
            var chosen = alternate.Elements(Mc + "Choice").FirstOrDefault(IsSupported)
                         ?? alternate.Elements(Mc + "Choice").FirstOrDefault(IsSlideShow)
                         ?? alternate.Element(Mc + "Fallback");
            if (chosen == null)
                alternate.Remove();
            else
                alternate.ReplaceWith(chosen.Elements());
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

    static bool IsSupported(XElement choice)
    {
        var requires = ((string?)choice.Attribute("Requires") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return requires.All(prefix => choice.GetNamespaceOfPrefix(prefix) is { } ns && Supported.Contains(ns.NamespaceName));
    }
}
