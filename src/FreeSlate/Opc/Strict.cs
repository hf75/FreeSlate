using System.Xml.Linq;

namespace FreeSlate.Opc;

/// <summary>
/// Strict Office Open XML (ISO/IEC 29500 Strict, catalog: package.strict) names its markup with namespaces of its own;
/// read as the transitional namespaces of the same schemas (from the target namespaces of the strict schemas of
/// ECMA-376 Part 1 and the transitional ones of Part 4, schema file by schema file), it is the markup the engine reads.
/// Relationship types are matched by their last segment and need no mapping; the packaging namespaces are the same.
/// </summary>
static class Strict
{
    const string Prefix = "http://purl.oclc.org/ooxml/";

    static readonly Dictionary<string, string> Transitional = new(StringComparer.Ordinal)
    {
        ["http://purl.oclc.org/ooxml/drawingml/chart"] = "http://schemas.openxmlformats.org/drawingml/2006/chart",
        ["http://purl.oclc.org/ooxml/drawingml/chartDrawing"] = "http://schemas.openxmlformats.org/drawingml/2006/chartDrawing",
        ["http://purl.oclc.org/ooxml/drawingml/diagram"] = "http://schemas.openxmlformats.org/drawingml/2006/diagram",
        ["http://purl.oclc.org/ooxml/drawingml/lockedCanvas"] = "http://schemas.openxmlformats.org/drawingml/2006/lockedCanvas",
        ["http://purl.oclc.org/ooxml/drawingml/main"] = "http://schemas.openxmlformats.org/drawingml/2006/main",
        ["http://purl.oclc.org/ooxml/drawingml/picture"] = "http://schemas.openxmlformats.org/drawingml/2006/picture",
        ["http://purl.oclc.org/ooxml/drawingml/spreadsheetDrawing"] = "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing",
        ["http://purl.oclc.org/ooxml/drawingml/wordprocessingDrawing"] = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing",
        ["http://purl.oclc.org/ooxml/presentationml/main"] = "http://schemas.openxmlformats.org/presentationml/2006/main",
        ["http://purl.oclc.org/ooxml/officeDocument/characteristics"] = "http://schemas.openxmlformats.org/officeDocument/2006/characteristics",
        ["http://purl.oclc.org/ooxml/officeDocument/bibliography"] = "http://schemas.openxmlformats.org/officeDocument/2006/bibliography",
        ["http://purl.oclc.org/ooxml/officeDocument/sharedTypes"] = "http://schemas.openxmlformats.org/officeDocument/2006/sharedTypes",
        ["http://purl.oclc.org/ooxml/officeDocument/customXml"] = "http://schemas.openxmlformats.org/officeDocument/2006/customXml",
        ["http://purl.oclc.org/ooxml/schemaLibrary/main"] = "http://schemas.openxmlformats.org/schemaLibrary/2006/main",
        ["http://purl.oclc.org/ooxml/officeDocument/customProperties"] = "http://schemas.openxmlformats.org/officeDocument/2006/custom-properties",
        ["http://purl.oclc.org/ooxml/officeDocument/extendedProperties"] = "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties",
        ["http://purl.oclc.org/ooxml/officeDocument/docPropsVTypes"] = "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes",
        ["http://purl.oclc.org/ooxml/officeDocument/math"] = "http://schemas.openxmlformats.org/officeDocument/2006/math",
        ["http://purl.oclc.org/ooxml/officeDocument/relationships"] = "http://schemas.openxmlformats.org/officeDocument/2006/relationships",
        ["http://purl.oclc.org/ooxml/spreadsheetml/main"] = "http://schemas.openxmlformats.org/spreadsheetml/2006/main",
        ["http://purl.oclc.org/ooxml/wordprocessingml/main"] = "http://schemas.openxmlformats.org/wordprocessingml/2006/main",
    };

    /// <summary>A strict part's markup renamed to the transitional namespaces; other parts as they are.</summary>
    public static XDocument Transition(XDocument document)
    {
        if (document.Root is not { } root
            || !root.Name.NamespaceName.StartsWith(Prefix, StringComparison.Ordinal)
               && !root.Attributes().Any(a => a.IsNamespaceDeclaration && a.Value.StartsWith(Prefix, StringComparison.Ordinal)))
            return document;
        foreach (var element in root.DescendantsAndSelf())
        {
            if (Transitional.TryGetValue(element.Name.NamespaceName, out var ns))
                element.Name = XNamespace.Get(ns) + element.Name.LocalName;
            if (!element.Attributes().Any(a => a.Name.NamespaceName.StartsWith(Prefix, StringComparison.Ordinal)
                                               || a.IsNamespaceDeclaration && a.Value.StartsWith(Prefix, StringComparison.Ordinal)))
                continue;
            element.ReplaceAttributes(element.Attributes().Select(a =>
                a.IsNamespaceDeclaration ? new XAttribute(a.Name, Transitional.GetValueOrDefault(a.Value, a.Value))
                : Transitional.TryGetValue(a.Name.NamespaceName, out var attributeNs) ? new XAttribute(XNamespace.Get(attributeNs) + a.Name.LocalName, a.Value)
                : new XAttribute(a)).ToList());
        }
        return document;
    }
}
