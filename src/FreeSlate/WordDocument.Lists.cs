using System.Globalization;
using System.Xml.Linq;
using FreeSlate.Docx;

namespace FreeSlate;

/// <summary>
/// The lists of a Word document as an editor keeps them (catalog: numbering.definitions, numbering.counting): which list a paragraph
/// is numbered in by its own formatting (OwnList; ParagraphFormatting.List and ListLevel give it), the number or bullet it shows
/// (ListLabel), and new lists of a numbering definition that start over (AddList), as Word's Restart Numbering makes them: lists of
/// the same definition count on together, one that starts over does so where it is first used (see ListCounter).
/// </summary>
public sealed partial class WordDocument
{
    /// <summary>
    /// The list a paragraph is numbered in by its own formatting (its w:pPr's w:numPr): the numbering instance's id and the level, each
    /// null where it says none (its style's then counting; a w:numId 0 switches its style's list off).
    /// </summary>
    public (string? List, int? Level) OwnList(int paragraph)
    {
        var p = Find(paragraph).Paragraph.Element;
        var w = p.Name.Namespace;
        if (p.Element(w + "pPr")?.Element(w + "numPr") is not { } numPr)
            return (null, null);
        var level = (string?)numPr.Element(w + "ilvl")?.Attribute(w + "val");
        return ((string?)numPr.Element(w + "numId")?.Attribute(w + "val"),
            int.TryParse(level, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ilvl) ? ilvl : null);
    }

    /// <summary>The number or bullet a paragraph shows, as its list gives it ("3.", "b)", "•"), null where it shows none.</summary>
    public string? ListLabel(int paragraph) => Find(paragraph).Paragraph.Label?.Text;

    /// <summary>The numbering definition (w:abstractNum) a paragraph's number or bullet counts in, by its id; null where it shows none.</summary>
    public string? ListDefinition(int paragraph) => Find(paragraph).Paragraph.Label?.List;

    /// <summary>
    /// A new list of a numbering definition (a w:num of its w:abstractNum in the numbering part) that starts over at a number where it
    /// is first used (a w:startOverride of its first level): its id, which paragraphs are then numbered in (ParagraphFormatting.List).
    /// Its id follows the greatest the part has. A version of its own, with the body as it was: no paragraph is numbered in the new list
    /// yet, so that none is read anew, and undoing or doing it again reads none either (see Go and ListsOnly).
    /// </summary>
    /// <exception cref="NotSupportedException">The document has no numbering part, or no such definition in it; nothing changes.</exception>
    public string AddList(string definition, int start = 1)
    {
        Read();
        if (NumberingPart() is not { } part)
            throw new NotSupportedException("The document has no numbering part.");
        var lists = Xml(package.ReadBytes(part));
        var root = lists.Root!;
        var w = root.Name.Namespace;
        if (!root.Elements(w + "abstractNum").Any(a => (string?)a.Attribute(w + "abstractNumId") == definition))
            throw new NotSupportedException($"The document has no numbering definition {definition}.");
        var id = (root.Elements(w + "num")
            .Select(n => int.TryParse((string?)n.Attribute(w + "numId"), NumberStyles.None, CultureInfo.InvariantCulture, out var k) ? k : 0)
            .DefaultIfEmpty(0).Max() + 1).ToString(CultureInfo.InvariantCulture);
        var made = new XElement(w + "num", new XAttribute(w + "numId", id),
            new XElement(w + "abstractNumId", new XAttribute(w + "val", definition)),
            new XElement(w + "lvlOverride", new XAttribute(w + "ilvl", "0"),
                new XElement(w + "startOverride", new XAttribute(w + "val", start.ToString(CultureInfo.InvariantCulture)))));
        Number(root, new XElement(w + "numbering", made));
        Put(new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase) { [part] = Bytes(lists) });
        context.Numbering.Take(made);
        versions.RemoveRange(version + 1, versions.Count - version - 1);
        versions.Add(new Version(whole, islands, kept, partBytes));
        version++;
        return id;
    }

    /// <summary>
    /// The lists (w:num) of a version of the numbering part, where it differs from the part as it is otherwise only by lists one of
    /// them has and the other not, as AddList makes them: no paragraph is numbered otherwise for that (a paragraph that names one of them
    /// differs too, and is read anew so), so that the body is not read whole again for it (see Go); null where it differs otherwise.
    /// </summary>
    static IReadOnlyList<XElement>? ListsOnly(byte[]? was, byte[]? now)
    {
        if (was == null || now == null)
            return null;
        XElement before, after;
        try
        {
            (before, after) = (Xml(was).Root!, Xml(now).Root!);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
        var w = before.Name.Namespace;
        static string Said(XElement e) => e.ToString(SaveOptions.DisableFormatting);
        if (after.Name != before.Name || !before.Attributes().Select(a => a.ToString()).SequenceEqual(after.Attributes().Select(a => a.ToString()))
            || !before.Elements().Where(e => e.Name != w + "num").Select(Said).SequenceEqual(after.Elements().Where(e => e.Name != w + "num").Select(Said)))
            return null;
        var had = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var num in before.Elements(w + "num"))
            had.TryAdd((string?)num.Attribute(w + "numId") ?? "", Said(num));
        var lists = after.Elements(w + "num").ToList();
        return lists.All(num => !had.TryGetValue((string?)num.Attribute(w + "numId") ?? "", out var same) || same == Said(num)) ? lists : null;
    }
}
