using System.Xml.Linq;
using FreeSlate.Docx;

namespace FreeSlate;

/// <summary>
/// Building blocks of Word's galleries as they stand in a body (catalog: sdt.docPart): a block content control whose w:docPartObj names
/// the gallery (Cover Pages for Word's Insert > Cover Page, which stands first in the body and of which Word keeps one, w:docPartUnique),
/// holding the block's paragraphs, its fields content controls of their own named by their w:alias (Title, Subtitle, Author …), also in
/// the text boxes of its drawings.
/// </summary>
public sealed partial class WordDocument
{
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
    /// its fields, each content control within it that has a name (w:alias) and the text it shows; null where it stands in none.
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
            fields[name] = content == null || Property(field, "showingPlcHdr") != null ? ""
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
