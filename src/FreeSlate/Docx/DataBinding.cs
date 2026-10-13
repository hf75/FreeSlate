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
        var store = binding.Attr("storeItemID") ?? "";
        if (Items().GetValueOrDefault(store.Trim().ToUpperInvariant()) is not { } document)
            return null;
        var namespaces = new XmlNamespaceManager(new NameTable());
        foreach (Match m in Prefix.Matches(binding.Attr("prefixMappings") ?? ""))
            namespaces.AddNamespace(m.Groups[1].Value, m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value);
        string? value;
        try
        {
            value = (document.XPathEvaluate(xpath, namespaces) as IEnumerable<object>)?.FirstOrDefault() switch
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
        if (value == null)
            return null;
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
        return items;
    }
}
