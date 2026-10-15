using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace FreeSlate.Docx;

/// <summary>
/// Data-bound content controls (w:dataBinding, catalog: sdt.dataBinding, package.customXml): Word refreshes their text
/// from the bound custom XML part when it opens a document, so the printed text is the node's value, not the text the
/// document stored (measured with docx/local/binding: a plain text control, a date control formatted by its
/// w:dateFormat and w:lid, a control bound to the core properties; a node that does not exist keeps the stored text).
/// A picture control (w:picture) binds its picture, not text: it keeps the picture it stores
/// (docx/external/libreoffice/extras-ooxmlexport/sdt-picture-databinding: a node of base64 data, the stored picture
/// the same).
/// </summary>
public sealed class DataBinding(DocumentContext context)
{
    /// <summary>The data store items by their ids: the custom XML parts and the package's properties.</summary>
    Dictionary<string, XDocument>? items;

    static readonly Regex Prefix = new("xmlns:([\\w.-]+)\\s*=\\s*(?:'([^']*)'|\"([^\"]*)\")", RegexOptions.Compiled);

    /// <summary>The text a bound content control shows, or null where it is not bound or its node does not exist.</summary>
    public string? Text(XElement sdtPr)
    {
        var binding = sdtPr.Child("dataBinding");
        if (binding?.Attr("xpath") is not { Length: > 0 } xpath || sdtPr.Child("picture") != null)
            return null;
        // A binding without a data store item takes the first custom XML part, in the order the document's
        // relationships list them, in which the node exists (Word's PDF of docx/external/libreoffice/extras-ooxmlexport/
        // tdf147724: "HERUNTERLADEN" from item2, listed before item1, whose node says "ABC"); one naming an item that is
        // not there keeps the stored text.
        var store = binding.Attr("storeItemID");
        IEnumerable<XDocument> documents = string.IsNullOrWhiteSpace(store) ? Items().Values
            : Items().GetValueOrDefault(store.Trim().ToUpperInvariant()) is { } named ? [named] : [];
        string? value = null;
        foreach (var document in documents)
        {
            value = Evaluate(document, xpath, binding.Attr("prefixMappings"));
            if (value != null)
                break;
        }
        // An empty node leaves a control showing its placeholder as it is (measured with docx/local/bindempty: the
        // placeholder text stays, without w:showingPlcHdr the control is empty; docx/external/poi/60316).
        if (value == null || value.Length == 0 && sdtPr.Child("showingPlcHdr") != null)
            return null;
        // One with a placeholder of the glossary shows that placeholder's text (Word's PDF of docx/external/libreoffice/
        // extras-ooxmlexport/fdo67013: a header's control bound to an empty title, its own text "Sample Doc 002",
        // prints "[Type the document title]").
        if (value.Length == 0 && sdtPr.Child("placeholder")?.Child("docPart").Val() is { Length: > 0 } partName
            && Placeholders().GetValueOrDefault(partName) is { Length: > 0 } placeholder)
            return placeholder;
        // Line feeds break the line in a plain text control of several lines (w:text w:multiLine), elsewhere they are
        // dropped, no space in their place (docx/external/libreoffice/extras-ooxmlexport/tdf81507: a description of
        // four lines bound four times, as multiLine 1, multiLine 0, w:text alone and a rich text control).
        value = sdtPr.Child("text")?.Attr("multiLine") is "1" or "true" or "on"
            ? value.Replace("\r\n", "\n").Replace('\r', '\n')
            : value.Replace("\r", "").Replace("\n", "");
        // A date shows in the control's format and language; list controls their entry's display text.
        if (sdtPr.Child("date") is { } date)
        {
            if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var when))
                return value;
            var culture = CultureInfo.InvariantCulture;
            try
            {
                if (date.Child("lid").Val() is { Length: > 0 } lid)
                    culture = CultureInfo.GetCultureInfo(lid);
            }
            catch (CultureNotFoundException) { }
            var format = date.Child("dateFormat").Val();
            return string.IsNullOrEmpty(format) ? when.ToString("d", culture) : when.ToString(format, culture);
        }
        if ((sdtPr.Child("dropDownList") ?? sdtPr.Child("comboBox")) is { } list
            && list.Elements(Wml.W + "listItem").FirstOrDefault(i => i.Attr("value") == value) is { } item)
            return item.Attr("displayText") ?? value;
        return value;
    }

    /// <summary>
    /// The value of the node an XPath finds in a data store item, or null. A prefix the mappings do not declare names the
    /// item's namespaces in their order, ns0 its root's, as Word writes them (Word's PDF of docx/external/libreoffice/
    /// extras-ooxmlexport/tdf151912: /ns0:BlogPostInfo/ns0:PostTitle without w:prefixMappings finds the title in a
    /// part whose root has a default namespace).
    /// </summary>
    static string? Evaluate(XDocument document, string xpath, string? mappings)
    {
        var namespaces = new XmlNamespaceManager(new NameTable());
        foreach (Match m in Prefix.Matches(mappings ?? ""))
            namespaces.AddNamespace(m.Groups[1].Value, m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value);
        var uris = document.Root?.DescendantsAndSelf().Select(e => e.Name.NamespaceName).Where(u => u.Length > 0).Distinct().ToList() ?? [];
        foreach (Match m in Undeclared.Matches(xpath))
        {
            var prefix = m.Groups[1].Value;
            if (namespaces.LookupNamespace(prefix) == null && prefix.StartsWith("ns", StringComparison.Ordinal)
                && int.TryParse(prefix.AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out var k) && k < uris.Count)
                namespaces.AddNamespace(prefix, uris[k]);
        }
        try
        {
            return (document.XPathEvaluate(xpath, namespaces) as IEnumerable<object>)?.FirstOrDefault() switch
            {
                XElement element => element.Value,
                XAttribute attribute => attribute.Value,
                XText text => text.Value,
                _ => null,
            };
        }
        catch (Exception e) when (e is XPathException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    static readonly Regex Undeclared = new("(?<![\\w.-])([A-Za-z_][\\w.-]*):(?=[A-Za-z_*])", RegexOptions.Compiled);

    /// <summary>The texts of the glossary's building blocks (w:docPart) by their names.</summary>
    Dictionary<string, string>? placeholders;

    Dictionary<string, string> Placeholders()
    {
        if (placeholders != null)
            return placeholders;
        placeholders = new Dictionary<string, string>(StringComparer.Ordinal);
        var package = context.Package;
        if (package.FirstRelationship(context.MainPart, "glossaryDocument")?.Target is not { } glossary || !package.Exists(glossary))
            return placeholders;
        foreach (var part in context.Load(glossary).Descendants(Wml.W + "docPart"))
        {
            if (part.Child("docPartPr")?.Child("name").Val() is { Length: > 0 } name)
                placeholders.TryAdd(name, string.Concat(part.Child("docPartBody")?.Descendants(Wml.W + "t").Select(t => t.Value) ?? []));
        }
        return placeholders;
    }

    Dictionary<string, XDocument> Items()
    {
        if (items != null)
            return items;
        items = new Dictionary<string, XDocument>(StringComparer.Ordinal);
        var package = context.Package;
        foreach (var rel in package.Relationships(context.MainPart).Where(r => r.Is("customXml") && !r.External && package.Exists(r.Target)))
        {
            var props = package.FirstRelationship(rel.Target, "customXmlProps")?.Target;
            if (props == null || !package.Exists(props))
                continue;
            var id = context.Load(props).Root?.Attributes().FirstOrDefault(a => a.Name.LocalName == "itemID")?.Value;
            if (id != null)
                items.TryAdd(id.Trim().ToUpperInvariant(), context.Load(rel.Target));
        }
        // The package's core and extended properties have ids of their own.
        foreach (var (id, type) in new[] { ("{6C3C8BC8-F283-45AE-878A-BAB7291924A1}", "core-properties"), ("{6668398D-A668-4E3E-A5EB-62B293D839F1}", "extended-properties") })
        {
            if (package.FirstRelationship("/", type)?.Target is { } part && package.Exists(part))
                items.TryAdd(id, context.Load(part));
        }
        // Word supplies the cover page properties where the package has none, their values empty (docx/external/
        // libreoffice/extras-ooxmlexport/sdt-header: a date control bound to PublishDate in a header's cell shows
        // nothing, not its stored "February 28, 2013"; dkvert, wpg-nested: controls showing their placeholder keep it).
        XNamespace cover = "http://schemas.microsoft.com/office/2006/coverPageProps";
        items.TryAdd("{55AF091B-3C7A-41E3-B477-F2FDAA23CFDA}", new XDocument(new XElement(cover + "CoverPageProperties",
            new[] { "PublishDate", "Abstract", "CompanyAddress", "CompanyPhone", "CompanyFax", "CompanyEmail" }.Select(n => new XElement(cover + n)))));
        return items;
    }
}
