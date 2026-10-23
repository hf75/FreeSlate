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

    const string StrictDrawing = "http://purl.oclc.org/ooxml/drawingml/wordprocessingDrawing";

    /// <summary>
    /// Word shapes and groups, which Strict names in the drawing namespace (docx/external/openxml-sdk/Strict01:
    /// wp:wsp with wp:spPr, wp:bodyPr, wp:txbx …), are their transitional wps and wpg elements.
    /// </summary>
    static readonly Dictionary<string, string> StrictShapes = new(StringComparer.Ordinal)
    {
        ["wsp"] = Wps, ["cNvSpPr"] = Wps, ["cNvCnPr"] = Wps, ["spPr"] = Wps, ["style"] = Wps, ["txbx"] = Wps,
        ["linkedTxbx"] = Wps, ["bodyPr"] = Wps, ["wgp"] = Wpg, ["cNvGrpSpPr"] = Wpg, ["grpSpPr"] = Wpg, ["grpSp"] = Wpg,
    };

    const string DrawingMain = "http://schemas.openxmlformats.org/drawingml/2006/main";

    /// <summary>Word 2010's relative sizes and positions (wp14:pctWidth …), which Strict writes as "45.5%" too.</summary>
    const string Wp14 = "http://schemas.microsoft.com/office/word/2010/wordprocessingDrawing";

    /// <summary>A text box's content, which Strict documents of Word name in the wne namespace (docx/external/openxml-sdk/Strict01).</summary>
    static readonly XName StrictTextBoxContent = XName.Get("txbxContent", "http://schemas.microsoft.com/office/word/2006/wordml");
    static readonly XName TextBoxContent = XName.Get("txbxContent", "http://schemas.openxmlformats.org/wordprocessingml/2006/main");

    static readonly XName WordTextDirection = XName.Get("textDirection", "http://schemas.openxmlformats.org/wordprocessingml/2006/main");

    /// <summary>Strict's text directions (ST_TextDirection) as the transitional ones.</summary>
    static readonly Dictionary<string, string> StrictDirections = new(StringComparer.Ordinal)
    {
        ["tb"] = "lrTb", ["rl"] = "tbRl", ["lr"] = "btLr", ["tbV"] = "lrTbV", ["rlV"] = "tbRlV", ["lrV"] = "tbLrV",
    };
    const string Wps = "http://schemas.microsoft.com/office/word/2010/wordprocessingShape";
    const string Wpg = "http://schemas.microsoft.com/office/word/2010/wordprocessingGroup";

    /// <summary>A strict part's markup renamed to the transitional namespaces; other parts as they are.</summary>
    public static XDocument Transition(XDocument document)
    {
        if (document.Root is { } root && IsStrict(root))
            Transition(root);
        return document;
    }

    /// <summary>Whether a part's root is of Strict's namespaces.</summary>
    public static bool IsStrict(XElement root) =>
        root.Name.NamespaceName.StartsWith(Prefix, StringComparison.Ordinal)
        || root.Attributes().Any(a => a.IsNamespaceDeclaration && a.Value.StartsWith(Prefix, StringComparison.Ordinal));

    /// <summary>An element of a strict part, and what it holds, renamed to the transitional namespaces.</summary>
    public static void Transition(XElement root)
    {
        foreach (var element in root.DescendantsAndSelf())
        {
            if (element.Name.NamespaceName == StrictDrawing && StrictShapes.TryGetValue(element.Name.LocalName, out var shapeNs))
                element.Name = XNamespace.Get(shapeNs) + element.Name.LocalName;
            else if (element.Name == StrictTextBoxContent)
                element.Name = TextBoxContent;
            else if (Transitional.TryGetValue(element.Name.NamespaceName, out var ns))
                element.Name = XNamespace.Get(ns) + element.Name.LocalName;
            // Text directions have names of their own in Strict (docx/external/libreoffice/extras-ooxmlexport/
            // tdf79272_strictDxa: a cell of "lr" reads upward, tdf150822: a section of "rl" runs down).
            if (element.Name == WordTextDirection && element.Attributes().FirstOrDefault(a => a.Name.LocalName == "val") is { } direction
                && StrictDirections.TryGetValue(direction.Value, out var transitional))
                direction.Value = transitional;
            // DrawingML's percentages are "15%" in Strict, thousandths of a percent in transitional markup (Strict01:
            // chart lines tx1 lumMod 15 % drawn black).
            // So are Word's relative sizes and positions, as element text (Strict01: wp14:pctWidth "45.5%", the cover
            // page's panels placed and sized from it).
            if (element.Name.NamespaceName == Wp14 && element.Name.LocalName.StartsWith("pct", StringComparison.Ordinal) && !element.HasElements
                && element.Value.Trim() is var text && text.EndsWith('%')
                && double.TryParse(text[..^1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var share))
                element.Value = ((long)Math.Round(share * 1000)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (element.Name.NamespaceName == DrawingMain)
            {
                foreach (var attribute in element.Attributes().Where(a => !a.IsNamespaceDeclaration && a.Value.EndsWith('%')))
                {
                    if (double.TryParse(attribute.Value[..^1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var percent))
                        attribute.Value = ((long)Math.Round(percent * 1000)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            if (!element.Attributes().Any(a => a.Name.NamespaceName.StartsWith(Prefix, StringComparison.Ordinal)
                                               || a.IsNamespaceDeclaration && a.Value.StartsWith(Prefix, StringComparison.Ordinal)))
                continue;
            element.ReplaceAttributes(element.Attributes().Select(a =>
                a.IsNamespaceDeclaration ? new XAttribute(a.Name, Transitional.GetValueOrDefault(a.Value, a.Value))
                : Transitional.TryGetValue(a.Name.NamespaceName, out var attributeNs) ? new XAttribute(XNamespace.Get(attributeNs) + a.Name.LocalName, a.Value)
                : new XAttribute(a)).ToList());
        }
    }
}
