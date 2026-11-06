using System.Xml.Linq;
using FreeSlate.Docx;

namespace FreeSlate;

/// <summary>
/// Tables on pages of their own turned across (landscape), as Word's Page Setup applied to a selected table puts them (measured
/// with Word 365 by COM, its page setup dialog applied to a table selected between paragraphs, at the body's start and before its
/// last paragraph): the table's section split in three, a paragraph right before the table ending the part before it, one right
/// after the table ending the table's own part, whose pages are turned; the part after it goes on with the section's properties.
/// Unlike Word, which writes the section's first page number into every part (the pages after the table numbered from it again),
/// only the first part keeps what the section has of its own: its headers and footers, its first page's own ones, how it begins
/// and its first page's number; the parts after it go on with them, numbered on (Word gives the first part the headers and
/// footers too). The paragraphs that end the parts are 1 pt high, so that they make no page of their own. A table as wide as its text
/// takes the width of the text on the turned pages, and back on upright ones that of theirs (Word keeps a table's width, so that a table
/// put across for room has none more).
/// </summary>
public sealed partial class WordDocument
{
    /// <summary>
    /// Whether a table of the body stands alone on pages turned across (see the class): its section begins with it and ends with
    /// the paragraph right after it, on pages wider than high.
    /// </summary>
    public bool TableAcross(int table)
    {
        Read();
        var (model, top) = TableById(table);
        if (!ReferenceEquals(model, top))
            return false;
        var index = IndexOf(top);
        return (index == 0 || blocks[index - 1] is ParagraphBlock { EndsSection: not null })
            && index + 1 < blocks.Count && blocks[index + 1] is ParagraphBlock { EndsSection: { } turned } && turned.PageWidth > turned.PageHeight;
    }

    /// <summary>
    /// Puts a table of the body on pages of its own turned across (see the class), or back among the pages of the text around it
    /// (<paramref name="across"/> false): the paragraphs that ended the parts taken away, the section after the table given back what
    /// the first part had of its own. One change; nothing happens where the table stands so already.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The table stands in a table, right after another table, or last with no paragraph after it, or changes are tracked; nothing
    /// changes.
    /// </exception>
    public void TurnTablePages(int table, bool across)
    {
        Read();
        if (TableAcross(table) == across)
            return;
        var (model, top) = TableById(table);
        if (!ReferenceEquals(model, top))
            throw new NotSupportedException("A table in a table stands on its table's pages.");
        if (Author != null)
            throw new NotSupportedException("Pages of a table's own are not made as a tracked change.");
        var full = Full(table);
        var start = version;
        try
        {
            if (across)
                Across(top);
            else
                Upright(top);
            // As wide as the text again, where it was so or would reach past it on upright pages.
            if (full || !across && TableById(table).Table.Table.Grid.Sum() > TextWidthOf(table) + 0.01)
                WidenTable(table);
            Combine(version - start);
        }
        catch
        {
            Back(start);
            throw;
        }
    }

    /// <summary>The width of the text of the column a table of the body stands in, points.</summary>
    double TextWidthOf(int table) => ColumnWidth(CellsOf(table)!.Cells[0].First);

    /// <summary>Whether a table is as wide as the text of its column, less the 10 twips Word leaves (see TableMarkup), within 1 pt.</summary>
    bool Full(int table) => Math.Abs(TableById(table).Table.Table.Grid.Sum() - (TextWidthOf(table) - 0.5)) <= 1;

    void Across(Block table)
    {
        var index = IndexOf(table);
        if (index + 1 >= blocks.Count || blocks[index + 1] is not ParagraphBlock after)
            throw new NotSupportedException("No paragraph follows the table.");
        var properties = SectionEndOf(after.Id)?.EndsSection?.Element ?? BodySection() ?? new XElement(Wml.W + "sectPr");
        // The part before the table, where the section does not begin with it, keeps what the section has of its own.
        ParagraphBlock? before = null;
        if (index > 0 && blocks[index - 1] is not ParagraphBlock { EndsSection: not null })
            before = blocks[index - 1] as ParagraphBlock ?? throw new NotSupportedException("The table stands right after another table.");
        if (before != null)
            InsertBlocks(before.Id, Ending(properties, own: true));
        // The table's own part, its pages turned (Format swaps their width and height); the first part where the section begins with it.
        var turned = InsertBlocks(after.Id, Ending(properties, own: before == null), before: true)[0];
        Format(turned, new SectionFormatting { Orientation = "landscape" });
        // The part after the table goes on with what those before it have of their own.
        Own(after.Id, null);
    }

    void Upright(Block table)
    {
        var index = IndexOf(table);
        if (index + 2 >= blocks.Count || blocks[index + 1] is not ParagraphBlock { EndsSection.Element: { } turned } ending)
            throw new NotSupportedException("No part of the text follows the table's pages.");
        if (ending.Shows)
            throw new NotSupportedException("The paragraph that ends the table's pages holds text.");
        // The paragraph that ended the part before the table where it is one made for it (see Ending), which held what the section has
        // of its own; else the table's own part held it.
        var before = index > 0 && blocks[index - 1] is ParagraphBlock { EndsSection.Element: not null } made && IsEnding(made) ? made : null;
        var own = before?.EndsSection!.Element ?? turned;
        var next = blocks[index + 2] switch
        {
            ParagraphBlock p => p,
            var other => Inner(other).FirstOrDefault() as ParagraphBlock ?? throw new NotSupportedException("No paragraph follows the table's pages."),
        };
        Own(next.Id, own);
        // The paragraphs that ended the parts go; the section after the table is the table's again, and the text's before it.
        var gone = new[] { before, ending }.OfType<ParagraphBlock>().Select(p => Find(p.Id).Paragraph).ToList();
        Change(gone[0], gone[^1], (stretch, _) => stretch.Written(gone.Select(p => (stretch.Find(p.Element)
            ?? throw new NotSupportedException("The paragraph is not where it was read."), Enumerable.Empty<XElement>()))),
            () => [.. blocks.Where(b => !gone.Any(g => ReferenceEquals(g, b)))]);
    }

    /// <summary>Whether a paragraph is one that ends a part of a section split for a table's pages (see Ending): empty, 1 pt high.</summary>
    static bool IsEnding(ParagraphBlock paragraph)
    {
        var w = paragraph.Element.Name.Namespace;
        var properties = paragraph.Element.Element(w + "pPr");
        return !paragraph.Shows && (string?)properties?.Element(w + "spacing")?.Attribute(w + "line") == "20"
            && (string?)properties?.Element(w + "spacing")?.Attribute(w + "lineRule") == "exact"
            && (string?)properties?.Element(w + "rPr")?.Element(w + "sz")?.Attribute(w + "val") == "2";
    }

    /// <summary>The blocks a block holds first: a table's first cell's, for finding a paragraph after a table that another follows.</summary>
    static IEnumerable<Block> Inner(Block block) => block is TableBlock t ? ModelReader.Inner(t).SelectMany(list => list).SelectMany(b => b is TableBlock ? Inner(b) : [b]) : [block];

    /// <summary>
    /// The section a paragraph of the body stands in given what another section's properties have of their own, or without its own
    /// (<paramref name="from"/> null; see TextEdits.TakeOwn).
    /// </summary>
    void Own(int paragraph, XElement? from)
    {
        if (SectionEndOf(paragraph) is { } end)
            Change(end, end, (stretch, _) => TextEdits.TakeOwn(stretch, end.EndsSection!.Element, from));
        else
            Change(blocks[^1], blocks[^1], (stretch, _) => TextEdits.TakeOwn(stretch, BodySection(), from), lastSection: true);
    }

    /// <summary>
    /// A paragraph that ends a part of a section split in parts (see the class), with a copy of its page setup: of all it has where the
    /// part is the first (<paramref name="own"/>), else without what it has of its own, the part beginning on a new page and going on
    /// with the headers, footers and numbers of those before it. 1 pt high, no space round it.
    /// </summary>
    static string Ending(XElement properties, bool own)
    {
        var w = properties.Name.Namespace;
        var copy = new XElement(properties);
        copy.Elements(w + "sectPrChange").Remove();
        if (!own)
        {
            copy.Elements().Where(e => e.Name.LocalName is "headerReference" or "footerReference" or "titlePg" or "type").Remove();
            if (copy.Element(w + "pgNumType") is { } numbers)
            {
                numbers.Attribute(w + "start")?.Remove();
                if (!numbers.HasAttributes && !numbers.HasElements)
                    numbers.Remove();
            }
        }
        var paragraph = new XElement(w + "p", new XElement(w + "pPr",
            new XElement(w + "spacing", new XAttribute(w + "before", 0), new XAttribute(w + "after", 0), new XAttribute(w + "line", 20), new XAttribute(w + "lineRule", "exact")),
            new XElement(w + "rPr", new XElement(w + "sz", new XAttribute(w + "val", 2)), new XElement(w + "szCs", new XAttribute(w + "val", 2))),
            copy));
        // Written with the document's own prefixes (w:, r: …), which InsertBlocks declares round it: declared on the paragraph to write
        // them, then left out.
        var declared = (properties.Document?.Root?.Attributes() ?? []).Where(a => a.IsNamespaceDeclaration).ToList();
        if (declared.All(a => a.Value != w.NamespaceName))
            declared.Add(new XAttribute(XNamespace.Xmlns + "w", w.NamespaceName));
        if (declared.All(a => a.Value != Ns.R.NamespaceName))
            declared.Add(new XAttribute(XNamespace.Xmlns + "r", Ns.R.NamespaceName));
        foreach (var declaration in declared)
            paragraph.SetAttributeValue(declaration.Name, declaration.Value);
        var text = paragraph.ToString(SaveOptions.DisableFormatting);
        foreach (var declaration in paragraph.Attributes().Where(a => a.IsNamespaceDeclaration).ToList())
            text = text.Replace(" " + declaration, "");
        return text;
    }
}
