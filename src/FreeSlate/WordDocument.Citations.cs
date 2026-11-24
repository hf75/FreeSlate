using System.Text;
using System.Xml.Linq;
using FreeSlate.Docx;
using FreeSlate.Docx.Citations;

namespace FreeSlate;

/// <summary>
/// Citations and bibliographies as Word keeps them (catalog: fields.citation): the document's sources in a custom XML part related from
/// the main part (b:Sources, its style Word's References > Style), citations CITATION fields, bibliographies BIBLIOGRAPHY fields. Word
/// computes neither when it opens nor when it prints a document, it shows their saved results; the results are set here as Word's style
/// formats them (see CitationStyle), as Word's Update Citations and Bibliography sets them.
/// </summary>
public sealed partial class WordDocument
{
    const string RelationshipTypes = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/";

    /// <summary>The custom XML part that holds the document's sources, related from the main part; null where it has none.</summary>
    string? SourcesPart()
    {
        Read();
        foreach (var related in package.Relationships(context.MainPart).Where(r => !r.External && r.Is("customXml") && package.Exists(r.Target)))
        {
            var text = Encoding.UTF8.GetString(package.ReadBytes(related.Target));
            if (text.Contains(SourceList.Namespace, StringComparison.Ordinal) && SourceList.Read(text) != null)
                return related.Target;
        }
        return null;
    }

    /// <summary>The document's sources and their style as Word keeps them; null where it has none.</summary>
    public SourceList? Sources => SourcesPart() is { } part ? SourceList.Read(Encoding.UTF8.GetString(package.ReadBytes(part))) : null;

    /// <summary>
    /// Gives the document these sources (and their style), a change of its own: the part that held them anew, or one made as Word makes
    /// it (a custom XML item and its properties naming the bibliography's schema, related from the main part). Each source's order among
    /// those cited (RefOrder) as the citations stand now.
    /// </summary>
    public void SetSources(SourceList list)
    {
        var bytes = Encoding.UTF8.GetBytes(list.Write(CitationOrder(list)));
        var changes = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        if (SourcesPart() is { } part)
            changes[part] = bytes;
        else
        {
            var properties = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>"
                             + $"<ds:datastoreItem ds:itemID=\"{{{Guid.NewGuid().ToString().ToUpperInvariant()}}}\" xmlns:ds=\"http://schemas.openxmlformats.org/officeDocument/2006/customXml\">"
                             + $"<ds:schemaRefs><ds:schemaRef ds:uri=\"{SourceList.Namespace}\"/></ds:schemaRefs></ds:datastoreItem>";
            Added("", [new NewPart("sources", "/customXml/item1.xml", "application/xml", RelationshipTypes + "customXml", bytes),
                new NewPart("sourceProperties", "/customXml/itemProps1.xml", "application/vnd.openxmlformats-officedocument.customXmlProperties+xml",
                    RelationshipTypes + "customXmlProps", Encoding.UTF8.GetBytes(properties), From: "sources")], changes, context.MainPart);
        }
        ChangeParts(changes);
    }

    /// <summary>The style the document's citations are in: the sources' (by Word's style sheet or name), else APA, Word's default.</summary>
    public CitationStyle CitationStyleOf(SourceList? list) =>
        CitationStyle.Named(list?.SelectedStyle) ?? CitationStyle.Named(list?.StyleName) ?? CitationStyle.All[0];

    /// <summary>The tags the body's citations name, in the order it first cites them (numbered styles number by it).</summary>
    public IReadOnlyList<string> CitationOrder(SourceList? list = null)
    {
        list ??= Sources;
        var order = new List<string>();
        if (list == null)
            return order;
        foreach (var citation in Citations())
        {
            foreach (var cited in CitationRequest.Parse(citation.Instruction, list)?.Sources ?? [])
            {
                if (!order.Contains(cited.Source.Tag, StringComparer.OrdinalIgnoreCase))
                    order.Add(cited.Source.Tag);
            }
        }
        return order;
    }

    /// <summary>
    /// The saved results of the body's citations set to their text as the sources' style formats it, where they differ (Word's Update
    /// Citations and Bibliography); one change for all, none where all are so. Citations of no source the document has stay as they are.
    /// Returns whether a change was made.
    /// </summary>
    public bool RefreshCitations()
    {
        if (Sources is not { } list)
            return false;
        var citations = Citations();
        if (citations.Count == 0)
            return false;
        var style = CitationStyleOf(list);
        var context = new CitationContext(list, CitationOrder(list));
        var stale = citations.Select(c => (c.Field, c.Shown, c.Top, Text: CitationRequest.Parse(c.Instruction, list) is { } request
                ? string.Concat(style.Cite(request, context).Select(p => p.Text)) : null))
            .Where(c => c.Text != null && c.Text != c.Shown)
            .ToList();
        if (stale.Count == 0)
            return false;
        Change(stale[0].Top, stale[^1].Top, (stretch, _) => TextEdits.Results(stretch, [.. stale.Select(c => (c.Field, c.Text!))]));
        return true;
    }

    /// <summary>
    /// The body's citations in order (in its paragraphs, tables' cells and text boxes): each CITATION field's element as read (its
    /// beginning, or the simple field), its instruction, the text it shows and the block of the body it stands in. The model reads a
    /// citation as the text it shows (it computes nothing of it), so they are found in the paragraphs' XML.
    /// </summary>
    List<(XElement Field, string Instruction, string Shown, Block Top)> Citations()
    {
        Read();
        var found = new List<(XElement, string, string, Block)>();
        void Blocks(IEnumerable<Block> list, Block? top)
        {
            foreach (var block in list)
            {
                if (block is ParagraphBlock paragraph)
                {
                    var text = ParagraphText.Of(paragraph);
                    foreach (var (start, end, element) in TextEdits.Stretches(paragraph))
                    {
                        if (InstructionOf(element) is { } instruction && FieldInline.TypeOf(instruction) == "CITATION")
                        {
                            var (from, to) = (Math.Clamp(start - paragraph.TextOffset, 0, text.Length), Math.Clamp(end - paragraph.TextOffset, 0, text.Length));
                            found.Add((element, instruction.Trim(), text[from..Math.Max(from, to)], top ?? block));
                        }
                    }
                }
                else
                {
                    foreach (var inner in ModelReader.Inner(block))
                        Blocks(inner, top ?? block);
                }
            }
        }
        Blocks(blocks, null);
        return found;
    }

    /// <summary>The citations of a paragraph's text, in order: the stretch each shows and its CITATION field's instruction.</summary>
    public IReadOnlyList<(int Start, int End, string Instruction)> CitationsOf(int paragraph) =>
        [.. TextEdits.Stretches(Find(paragraph).Paragraph).Select(s => (s.Start, s.End, Instruction: InstructionOf(s.Element)))
            .Where(s => s.Instruction != null && FieldInline.TypeOf(s.Instruction) == "CITATION").Select(s => (s.Start, s.End, s.Instruction!.Trim()))];

    /// <summary>A field's instruction by its element as read: a simple field's; a complex field's beginning's instrText up to its separator (those of fields within it aside); null for another element.</summary>
    static string? InstructionOf(XElement element)
    {
        if (element.Name.LocalName == "fldSimple")
            return (string?)element.Attributes().FirstOrDefault(a => a.Name.LocalName == "instr");
        if (element.Name.LocalName != "fldChar" || (string?)element.Attributes().FirstOrDefault(a => a.Name.LocalName == "fldCharType") != "begin"
            || element.Ancestors().FirstOrDefault(a => a.Name.LocalName == "p") is not { } holder)
            return null;
        var (text, depth, started) = (new StringBuilder(), 0, false);
        foreach (var e in holder.Descendants())
        {
            if (!started)
            {
                (started, depth) = (e == element, e == element ? 1 : 0);
                continue;
            }
            if (e.Name.LocalName == "fldChar")
            {
                var type = (string?)e.Attributes().FirstOrDefault(a => a.Name.LocalName == "fldCharType");
                if (type == "begin")
                    depth++;
                else if (type == "end" && --depth == 0 || type == "separate" && depth == 1)
                    break;
            }
            else if (e.Name.LocalName == "instrText" && depth == 1)
                text.Append(e.Value);
        }
        return text.ToString();
    }

    /// <summary>
    /// A bibliography of the document's sources as Word's Insert Bibliography makes it (see CitationMarkup.Bibliography) in their style,
    /// its words in a language (1033 English, 1031 German …), its entries in the paragraph style of the id given; null where the
    /// document has no sources.
    /// </summary>
    public string? BibliographyMarkup(int lcid, string paragraphStyle)
    {
        if (Sources is not { } list)
            return null;
        var style = CitationStyleOf(list);
        var bibliography = style.Bibliography(new CitationContext(list, CitationOrder(list)), lcid);
        return CitationMarkup.Bibliography(bibliography, paragraphStyle, lcid, Random.Shared.Next(1, int.MaxValue), Random.Shared.Next(1, int.MaxValue));
    }

    /// <summary>The instructions of the body's citations (CITATION fields) in order.</summary>
    public IReadOnlyList<string> CitationInstructions() => [.. Citations().Select(c => c.Instruction)];

    /// <summary>
    /// Whether a paragraph stands in a bibliography: in Word's bibliography content control (w:bibliography), or in the building block
    /// of the Bibliographies gallery that holds one (Word's Insert Bibliography).
    /// </summary>
    public bool InBibliography(int paragraph)
    {
        if (Found(paragraph) is not { } found)
            return false;
        var element = found.Paragraph.Parts?[0] ?? found.Paragraph.Element;
        return element.Ancestors().Any(a => a.Name.LocalName == "sdt" && a.Elements().FirstOrDefault(e => e.Name.LocalName == "sdtPr") is { } properties
            && properties.Elements().Any(e => e.Name.LocalName == "bibliography"
                || e.Name.LocalName == "docPartObj" && e.Elements().Any(g => g.Name.LocalName == "docPartGallery" && g.Attributes().Any(v => v.Name.LocalName == "val" && v.Value == "Bibliographies"))));
    }

    /// <summary>
    /// The language a paragraph's text is in, as the Windows language id a CITATION or BIBLIOGRAPHY field names it by (\l: 1033 English,
    /// 1031 German …): its mark's (its style's, the document's defaults), else its first text's, else the interface's.
    /// </summary>
    public int LanguageOf(int paragraph)
    {
        var p = Find(paragraph).Paragraph;
        var name = p.MarkFormat.Language ?? p.Content.OfType<TextInline>().Select(t => t.Format.Language).FirstOrDefault(l => l != null) ?? context.Culture.Name;
        try
        {
            if (System.Globalization.CultureInfo.GetCultureInfo(name).LCID is var lcid and not (127 or 4096))
                return lcid;
        }
        catch (System.Globalization.CultureNotFoundException)
        {
        }
        return name.StartsWith("de", StringComparison.OrdinalIgnoreCase) ? 1031 : 1033;
    }
}
