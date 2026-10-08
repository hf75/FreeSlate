using System.Xml.Linq;

namespace Engine.Opc;

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

    public static void Process(XDocument document)
    {
        foreach (var alternate in document.Descendants(Mc + "AlternateContent").ToList())
        {
            if (alternate.Parent == null)
                continue;   // inside an alternative that was already removed
            var chosen = alternate.Elements(Mc + "Choice").FirstOrDefault(IsSupported)
                         ?? alternate.Element(Mc + "Fallback");
            if (chosen == null)
                alternate.Remove();
            else
                alternate.ReplaceWith(chosen.Elements());
        }
    }

    static bool IsSupported(XElement choice)
    {
        var requires = ((string?)choice.Attribute("Requires") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return requires.All(prefix => choice.GetNamespaceOfPrefix(prefix) is { } ns && Supported.Contains(ns.NamespaceName));
    }
}
