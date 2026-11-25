using System.Globalization;
using System.Text;
using System.Xml.Linq;
using FreeSlate.Docx;

namespace FreeSlate;

/// <summary>
/// Building blocks of Word's galleries as they stand in a body (catalog: sdt.docPart): a block content control whose w:docPartObj names
/// the gallery (Cover Pages for Word's Insert > Cover Page, which stands first in the body and of which Word keeps one, w:docPartUnique),
/// holding the block's paragraphs, its fields content controls of their own named by their w:alias (Title, Subtitle, Author …), also in
/// the text boxes of its drawings; and the cover page properties a cover's fields are bound to.
/// </summary>
public sealed partial class WordDocument
{
    /// <summary>The namespace of Word's cover page properties, a custom XML item (catalog: package.customXml).</summary>
    public const string CoverPageNamespace = "http://schemas.microsoft.com/office/2006/coverPageProps";

    /// <summary>The data store id Word gives the cover page properties' item (measured in Word 365 by COM: CustomXMLParts; see Docx.DataBinding).</summary>
    public const string CoverPageItemId = "{55AF091B-3C7A-41E3-B477-F2FDAA23CFDA}";

    /// <summary>The custom XML item that holds the document's cover page properties, related from the main part; null where it has none.</summary>
    string? CoverPagePart()
    {
        Read();
        foreach (var related in package.Relationships(context.MainPart).Where(r => !r.External && r.Is("customXml") && package.Exists(r.Target)))
        {
            if (context.Load(related.Target).Root?.Name == XName.Get("CoverPageProperties", CoverPageNamespace))
                return related.Target;
        }
        return null;
    }

    /// <summary>
    /// Gives the document's cover page properties, which the Date and Abstract of a cover of Word's gallery show bound to them (its
    /// PublishDate and Abstract; measured in Word 365 by COM, a cover of the gallery inserted and its date set: a custom XML item of the id
    /// <see cref="CoverPageItemId"/>, "&lt;CoverPageProperties&gt;&lt;PublishDate&gt;2026-11-25T00:00:00&lt;/PublishDate&gt;&lt;Abstract/&gt;"
    /// and the company's address, phone, fax and e-mail empty): those given set (an empty one emptied), the others as they were, the item
    /// made as Word makes it where the document has none. A change of its own.
    /// </summary>
    public void SetCoverPageProperties(DateTime? published, string? summary)
    {
        XNamespace ns = CoverPageNamespace;
        var part = CoverPagePart();
        var document = part != null ? Xml(package.ReadBytes(part)) : new XDocument(new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(ns + "CoverPageProperties", new[] { "PublishDate", "Abstract", "CompanyAddress", "CompanyPhone", "CompanyFax", "CompanyEmail" }.Select(n => new XElement(ns + n))));
        var root = document.Root!;
        void Set(string name, string? value)
        {
            if (value == null)
                return;
            if (root.Element(ns + name) is not { } element)
                root.Add(element = new XElement(ns + name));
            element.Value = value;
        }
        Set("PublishDate", published?.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture));
        Set("Abstract", summary);
        var bytes = Bytes(document);
        var changes = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        if (part != null)
            changes[part] = bytes;
        else
        {
            var properties = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>"
                             + $"<ds:datastoreItem ds:itemID=\"{CoverPageItemId}\" xmlns:ds=\"http://schemas.openxmlformats.org/officeDocument/2006/customXml\">"
                             + $"<ds:schemaRefs><ds:schemaRef ds:uri=\"{CoverPageNamespace}\"/></ds:schemaRefs></ds:datastoreItem>";
            Added("", [new NewPart("coverPage", "/customXml/item1.xml", "application/xml", RelationshipTypes + "customXml", bytes),
                new NewPart("coverPageProperties", "/customXml/itemProps1.xml", "application/vnd.openxmlformats-officedocument.customXmlProperties+xml",
                    RelationshipTypes + "customXmlProps", Encoding.UTF8.GetBytes(properties), From: "coverPage")], changes, context.MainPart);
        }
        ChangeParts(changes);
    }

    /// <summary>The building block of a gallery a paragraph of the body stands in (its content control), or null.</summary>
    XElement? GalleryControl(int paragraph, string gallery)
    {
        if (Found(paragraph) is not { } found)
            return null;
        var element = found.Paragraph.Parts?[0] ?? found.Paragraph.Element;
        return element.Ancestors().FirstOrDefault(a => a.Name.LocalName == "sdt" && a.Elements().FirstOrDefault(e => e.Name.LocalName == "sdtPr") is { } properties
            && properties.Elements().Any(e => e.Name.LocalName == "docPartObj" && e.Elements().Any(g => g.Name.LocalName == "docPartGallery"
                && g.Attributes().Any(v => v.Name.LocalName == "val" && v.Value == gallery))));
    }

    /// <summary>
    /// The building block of a gallery (Cover Pages …) a paragraph of the body stands in: its first and last paragraph, its tag (w:tag) and
    /// its fields, each content control within it that has a name (w:alias) and the text it shows (a text control bound to the document's
    /// properties the property's, as Word shows it, see Docx.DataBinding); null where it stands in none.
    /// </summary>
    public (int First, int Last, string? Tag, IReadOnlyDictionary<string, string> Fields)? GalleryBlock(int paragraph, string gallery)
    {
        if (GalleryControl(paragraph, gallery) is not { } control)
            return null;
        var (first, last) = (paragraph, paragraph);
        while (Before(first) is { } before && GalleryControl(before, gallery) == control)
            first = before;
        while (After(last) is { } after && GalleryControl(after, gallery) == control)
            last = after;
        XElement? Property(XElement sdt, string name) => sdt.Elements().FirstOrDefault(e => e.Name.LocalName == "sdtPr")?.Elements().FirstOrDefault(e => e.Name.LocalName == name);
        DataBinding? binding = null;
        string? Value(XElement? e) => (string?)e?.Attributes().FirstOrDefault(a => a.Name.LocalName == "val");
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in control.Descendants().Where(e => e.Name.LocalName == "sdt" && e != control))
        {
            if (Value(Property(field, "alias")) is not { Length: > 0 } name || fields.ContainsKey(name))
                continue;
            var content = field.Elements().FirstOrDefault(e => e.Name.LocalName == "sdtContent");
            static string Words(XElement e) => string.Concat(e.Descendants().Where(t => t.Name.LocalName == "t" && t.Name.Namespace == Wml.W).Select(t => t.Value));
            // Its paragraphs' text, a line feed between them; a placeholder shown counts as none; a date control's date as it keeps it
            // (w:fullDate, yyyy-MM-dd), whatever its format shows.
            var paragraphs = content?.Elements().Where(e => e.Name.LocalName == "p").ToList() ?? [];
            var date = (string?)Property(field, "date")?.Attributes().FirstOrDefault(a => a.Name.LocalName == "fullDate");
            var bound = date == null && Property(field, "dataBinding") != null && field.Elements().FirstOrDefault(e => e.Name.LocalName == "sdtPr") is { } sdtPr
                ? (binding ??= new DataBinding(context)).Text(sdtPr) : null;
            fields[name] = bound is { } shown ? shown.Trim()
                : content == null || Property(field, "showingPlcHdr") != null ? ""
                : date is { Length: >= 10 } ? date[..10]
                : paragraphs.Count > 0 ? string.Join("\n", paragraphs.Select(Words)) : Words(content);
        }
        return (first, last, Value(Property(control, "tag")), fields);
    }

    /// <summary>
    /// The building block of a gallery a paragraph of the body stands in taken away whole, with all it holds (Word's Remove Current Cover
    /// Page), one change; nothing where it stands in none.
    /// </summary>
    /// <exception cref="NotSupportedException">The block is all the body holds, or not where it was read; nothing changes.</exception>
    public void RemoveGalleryBlock(int paragraph, string gallery)
    {
        if (GalleryBlock(paragraph, gallery) is not var (first, last, _, _) || GalleryControl(paragraph, gallery) is not { } control)
            return;
        var (_, firstTop) = Find(first);
        var (_, lastTop) = Find(last);
        var (i, j) = (IndexOf(firstTop), IndexOf(lastTop));
        if (i == 0 && j == blocks.Count - 1)
            throw new NotSupportedException("The building block is all the body holds.");
        Change(firstTop, lastTop, (stretch, _) =>
        {
            var sdt = stretch.Find(control) ?? throw new NotSupportedException("The building block is not where it was read.");
            return stretch.Written([(sdt, [])]);
        }, () => [.. blocks.Take(i), .. blocks.Skip(j + 1)]);
    }
}
