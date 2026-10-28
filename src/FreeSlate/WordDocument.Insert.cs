using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using FreeSlate.Docx;
using FreeSlate.Opc;

namespace FreeSlate;

/// <summary>
/// What a Word document takes besides text (see IEditableDocument.InsertMarkup): markup of runs put into a paragraph
/// (pictures, charts, shapes, WordArt, equations, fields), blocks put next to one (paragraphs, tables, sections), notes;
/// and the parts they bring (a picture's, a chart's and its workbook's, the notes'), which each version keeps as it has
/// them, so that undoing and doing again put them back with the body.
/// </summary>
public sealed partial class WordDocument
{
    static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    static readonly XNamespace ContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";
    static readonly XNamespace Wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";

    /// <summary>The usual prefixes of the namespaces of WordprocessingML and DrawingML, which markup put in may name without declaring them.</summary>
    static readonly (string Prefix, string Uri)[] Usual =
    [
        ("w", "http://schemas.openxmlformats.org/wordprocessingml/2006/main"),
        ("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"),
        ("m", "http://schemas.openxmlformats.org/officeDocument/2006/math"),
        ("wp", "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"),
        ("a", "http://schemas.openxmlformats.org/drawingml/2006/main"),
        ("pic", "http://schemas.openxmlformats.org/drawingml/2006/picture"),
        ("c", "http://schemas.openxmlformats.org/drawingml/2006/chart"),
        ("wps", "http://schemas.microsoft.com/office/word/2010/wordprocessingShape"),
        ("wpg", "http://schemas.microsoft.com/office/word/2010/wordprocessingGroup"),
        ("wpc", "http://schemas.microsoft.com/office/word/2010/wordprocessingCanvas"),
        ("wp14", "http://schemas.microsoft.com/office/word/2010/wordprocessingDrawing"),
        ("w14", "http://schemas.microsoft.com/office/word/2010/wordml"),
        ("w15", "http://schemas.microsoft.com/office/word/2012/wordml"),
        ("a14", "http://schemas.microsoft.com/office/drawing/2010/main"),
        ("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006"),
        ("v", "urn:schemas-microsoft-com:vml"),
        ("o", "urn:schemas-microsoft-com:office:office"),
    ];

    /// <summary>
    /// The bytes of the parts but the main part that changes gave new bytes, null for those they took away, as the
    /// document reads now; the others are as the package was opened (OpcPackage.Original). Each version keeps them as it
    /// has them (see Version), which undoing and doing again put back.
    /// </summary>
    ImmutableDictionary<string, byte[]?> partBytes = ImmutableDictionary.Create<string, byte[]?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The names of the parts the package had when opened or was given since: a new part is not named so (see NewPart).</summary>
    HashSet<string>? named;

    /// <summary>The id the next drawing put in takes (its wp:docPr's, which Word wants unique), 0 before the first.</summary>
    int nextDrawing;

    /// <summary>
    /// Puts markup into a paragraph at a place (see IEditableDocument.InsertMarkup and TextEdits.InsertMarkup), with the
    /// parts it needs; returns the ids of the paragraphs of its text boxes.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The place is within what the document computes or keeps, or the markup is to be tracked and holds other elements
    /// than runs; nothing changes.
    /// </exception>
    /// <exception cref="ArgumentException">The markup is not well-formed XML; nothing changes.</exception>
    public IReadOnlyList<int> InsertMarkup(int paragraph, int offset, string markup, IReadOnlyList<NewPart>? parts = null) =>
        Insert(paragraph, offset, markup, parts, new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase));

    IReadOnlyList<int> Insert(int paragraph, int offset, string markup, IReadOnlyList<NewPart>? added, Dictionary<string, byte[]?> changes)
    {
        var (p, top) = Find(paragraph);
        var known = paragraphs.Keys.ToHashSet();
        var elements = Elements(Added(markup, added, changes), context.Document.Root!);
        Change(top, top, (stretch, tracking) => TextEdits.InsertMarkup(stretch, p, offset, [.. elements.Select(e => new XElement(e))], tracking), changes: changes);
        return [.. Within(Find(paragraph).Paragraph).Where(id => !known.Contains(id))];
    }

    /// <summary>
    /// Puts blocks after a paragraph in its body, table cell or text box, or before it (see
    /// IEditableDocument.InsertBlocks and TextEdits.InsertBlocks); a table that would end the body, cell or text box is
    /// followed by an empty paragraph, as Word keeps one there. Returns the ids of the paragraphs they hold.
    /// </summary>
    /// <exception cref="NotSupportedException">The markup holds other blocks than paragraphs and tables, or is to be tracked; nothing changes.</exception>
    /// <exception cref="ArgumentException">The markup is not well-formed XML; nothing changes.</exception>
    public IReadOnlyList<int> InsertBlocks(int paragraph, string markup, bool before = false, IReadOnlyList<NewPart>? parts = null)
    {
        var (p, top) = Find(paragraph);
        var changes = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        var elements = Elements(Added(markup, parts, changes), context.Document.Root!);
        if (elements.Any(e => e.Name != Wml.W + "p" && e.Name != Wml.W + "tbl"))
            throw new NotSupportedException("Blocks put in are paragraphs and tables.");
        if (!before && elements.Count > 0 && elements[^1].Name == Wml.W + "tbl" && ListOf(top, p) is var (list, index) && index == list.Count - 1)
            elements.Add(new XElement(Wml.W + "p"));
        var count = elements.Count;
        Change(top, top, (stretch, tracking) => TextEdits.InsertBlocks(stretch, p, [.. elements.Select(e => new XElement(e))], before, tracking),
            () => Plain(top, p) is { } i
                ? [.. blocks.Take(before ? i : i + 1), .. Enumerable.Range(0, count).Select(_ => (Block)new ModelReader.NewBlock()), .. blocks.Skip(before ? i : i + 1)]
                : null,
            changes);
        // The blocks put in stand next to the paragraph in its list.
        var (q, qTop) = Find(paragraph);
        if (ListOf(qTop, q) is not var (now, at))
            return [];
        var made = before ? now.Skip(Math.Max(0, at - count)).Take(Math.Min(count, at)) : now.Skip(at + 1).Take(count);
        return [.. made.SelectMany(b => (b is ParagraphBlock block ? [block.Id] : Enumerable.Empty<int>()).Concat(Within(b)))];
    }

    /// <summary>
    /// Puts a footnote at a place, or an endnote (see IEditableDocument.InsertNote): its reference there, in the
    /// document's style for note references (else raised), and the note at the end of the notes part, in the style for
    /// note text, its mark first and a space after it as Word writes them; a notes part of its own where the document has
    /// none, with the separators Word puts first.
    /// </summary>
    /// <exception cref="NotSupportedException">The place is within what the document computes or keeps; nothing changes.</exception>
    /// <exception cref="ArgumentException">The markup is not well-formed XML; nothing changes.</exception>
    public void InsertNote(int paragraph, int offset, string markup, bool endnote = false)
    {
        var (w, kind) = (Wml.W, endnote ? "endnote" : "footnote");
        var changes = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        var part = package.FirstRelationship(context.MainPart, kind + "s") is { External: false } related && package.Exists(related.Target) ? related.Target : null;
        XElement Separator(int id, string type) =>
            new(w + kind, new XAttribute(w + "type", type), new XAttribute(w + "id", id),
                new XElement(w + "p", new XElement(w + "pPr", new XElement(w + "spacing", new XAttribute(w + "after", 0), new XAttribute(w + "line", 240), new XAttribute(w + "lineRule", "auto"))),
                    new XElement(w + "r", new XElement(w + type))));
        var notes = part != null
            ? Xml(package.ReadBytes(part))
            : new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), new XElement(w + kind + "s", new XAttribute(XNamespace.Xmlns + "w", w.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "r", Ns.R.NamespaceName), Separator(-1, "separator"), Separator(0, "continuationSeparator")));
        var root = notes.Root!;
        var id = root.Elements(w + kind).Select(n => int.TryParse(n.Attr("id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var k) ? k : 0).DefaultIfEmpty(0).Max() + 1;
        var reference = context.Styles.IdByName(kind + " reference", "character") is { } style
            ? $"<w:rStyle w:val=\"{Escaped(style)}\"/>" : "<w:vertAlign w:val=\"superscript\"/>";
        var textStyle = context.Styles.IdByName(kind + " text", "paragraph");
        var mark = Elements($"<w:r><w:rPr>{reference}</w:rPr><w:{kind}Ref/></w:r><w:r><w:t xml:space=\"preserve\"> </w:t></w:r>", root);
        root.Add(new XElement(w + kind, new XAttribute(w + "id", id),
            new XElement(w + "p", textStyle != null ? new XElement(w + "pPr", new XElement(w + "pStyle", new XAttribute(w + "val", textStyle))) : null, mark, Elements(markup, root))));
        List<NewPart> added = [];
        if (part != null)
            changes[part] = Bytes(notes);
        else
            added.Add(new NewPart("notes", $"/word/{kind}s.xml", $"application/vnd.openxmlformats-officedocument.wordprocessingml.{kind}s+xml",
                $"http://schemas.openxmlformats.org/officeDocument/2006/relationships/{kind}s", Bytes(notes)));
        Insert(paragraph, offset, $"<w:r><w:rPr>{reference}</w:rPr><w:{kind}Reference w:id=\"{id}\"/></w:r>", added, changes);
    }

    /// <summary>A text escaped for an XML attribute's value.</summary>
    static string Escaped(string text) => System.Security.SecurityElement.Escape(text);

    /// <summary>
    /// The parts to add put among the changes (see NewPart): named, related from the main part or from one another, listed
    /// with their content types; returns the markup with the ids of the relationships from the main part in place of
    /// their keys.
    /// </summary>
    string Added(string markup, IReadOnlyList<NewPart>? added, Dictionary<string, byte[]?> changes)
    {
        if (added is not { Count: > 0 })
            return markup;
        if (added.Select(p => p.Key).Distinct(StringComparer.Ordinal).Count() != added.Count || added.Any(p => p.From != null && added.All(q => q.Key != p.From)))
            throw new ArgumentException("The new parts' keys are not each their own, or a part is related from one not among them.", nameof(added));
        var names = added.ToDictionary(p => p.Key, p => Unique(p.Name), StringComparer.Ordinal);
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var group in added.GroupBy(p => p.From is { } from ? names[from] : context.MainPart, StringComparer.OrdinalIgnoreCase))
        {
            var source = group.Key;
            var relationshipsPart = OpcPackage.RelationshipsPart(source);
            var rels = Current(relationshipsPart, changes) is { } bytes ? Xml(bytes)
                : new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), new XElement(Relationships + "Relationships"));
            var taken = rels.Root!.Elements(Relationships + "Relationship").Select(r => (string?)r.Attribute("Id")).OfType<string>().ToHashSet(StringComparer.Ordinal);
            var next = 1;
            foreach (var part in group)
            {
                while (taken.Contains("rId" + next))
                    next++;
                var id = "rId" + next;
                taken.Add(id);
                ids[part.Key] = id;
                rels.Root.Add(new XElement(Relationships + "Relationship", new XAttribute("Id", id), new XAttribute("Type", part.Relationship),
                    new XAttribute("Target", Relative(source, names[part.Key]))));
            }
            changes[relationshipsPart] = Bytes(rels);
        }
        var types = Xml(Current(OpcPackage.ContentTypesPart, changes) ?? throw new NotSupportedException("The package names no content types."));
        foreach (var part in added)
            types.Root!.Add(new XElement(ContentTypes + "Override", new XAttribute("PartName", names[part.Key]), new XAttribute("ContentType", part.ContentType)));
        changes[OpcPackage.ContentTypesPart] = Bytes(types);
        foreach (var part in added)
        {
            var bytes = part.Bytes;
            if (added.Where(p => p.From == part.Key).ToList() is { Count: > 0 } from)
            {
                var text = Encoding.UTF8.GetString(bytes);
                foreach (var related in from)
                    text = text.Replace("{" + related.Key + "}", ids[related.Key], StringComparison.Ordinal);
                bytes = Encoding.UTF8.GetBytes(text);
            }
            changes[names[part.Key]] = bytes;
        }
        foreach (var part in added.Where(p => p.From == null))
            markup = markup.Replace("{" + part.Key + "}", ids[part.Key], StringComparison.Ordinal);
        return markup;
    }

    /// <summary>A part's bytes as the changes so far or the package have them, null where it has none.</summary>
    byte[]? Current(string part, Dictionary<string, byte[]?> changes) =>
        changes.TryGetValue(part, out var bytes) ? bytes : package.Exists(part) ? package.ReadBytes(part) : null;

    /// <summary>
    /// A new part's name: the one wished for where the package has no part so named and had none since it was opened,
    /// else numbered on from it ("/word/media/image1.png", "/word/media/image2.png" …); kept as taken.
    /// </summary>
    string Unique(string wish)
    {
        named ??= new HashSet<string>(package.PartNames, StringComparer.OrdinalIgnoreCase);
        if (!wish.StartsWith('/'))
            wish = "/" + wish;
        var extension = Path.GetExtension(wish);
        var stem = wish[..^extension.Length];
        var digits = stem.Length - stem.TrimEnd("0123456789".ToCharArray()).Length;
        var (prefix, number) = (stem[..^digits], digits > 0 && int.TryParse(stem[^digits..], NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0);
        var name = wish;
        while (named.Contains(name) || package.Exists(name))
            name = prefix + (number = Math.Max(number + 1, 1)).ToString(CultureInfo.InvariantCulture) + extension;
        named.Add(name);
        return name;
    }

    /// <summary>A relationship's target: the part relative to the folder of the part the relationship goes from.</summary>
    static string Relative(string source, string target)
    {
        var from = source[..(source.LastIndexOf('/') + 1)].Split('/', StringSplitOptions.RemoveEmptyEntries);
        var to = target.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var common = 0;
        while (common < from.Length && common < to.Length - 1 && string.Equals(from[common], to[common], StringComparison.OrdinalIgnoreCase))
            common++;
        return string.Join('/', Enumerable.Repeat("..", from.Length - common).Concat(to.Skip(common)));
    }

    /// <summary>
    /// Markup put in as elements: parsed within the namespaces <paramref name="root"/> declares and the usual ones (see
    /// Usual), each element then declaring those it names that the root does not; its drawings numbered on from the main
    /// part's (wp:docPr ids, which Word wants unique).
    /// </summary>
    /// <exception cref="ArgumentException">The markup is not well-formed XML.</exception>
    List<XElement> Elements(string markup, XElement root)
    {
        var declared = root.Attributes().Where(a => a.IsNamespaceDeclaration && a.Name.Namespace != XNamespace.None).ToDictionary(a => a.Name.LocalName, a => a.Value, StringComparer.Ordinal);
        var wrapper = new StringBuilder("<markup");
        foreach (var (prefix, uri) in declared.Select(d => (d.Key, d.Value)).Concat(Usual.Where(u => !declared.ContainsKey(u.Prefix))))
            wrapper.Append(" xmlns:").Append(prefix).Append("=\"").Append(Escaped(uri)).Append('"');
        wrapper.Append('>').Append(markup).Append("</markup>");
        XElement parsed;
        try
        {
            parsed = XElement.Parse(wrapper.ToString(), LoadOptions.PreserveWhitespace);
        }
        catch (XmlException e)
        {
            throw new ArgumentException("The markup is not well-formed XML: " + e.Message, nameof(markup), e);
        }
        var known = declared.Values.ToHashSet(StringComparer.Ordinal);
        var elements = parsed.Elements().ToList();
        foreach (var element in elements)
        {
            var own = element.DescendantsAndSelf().SelectMany(e => e.Attributes()).Where(a => a.IsNamespaceDeclaration)
                .GroupBy(a => a.Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Name.LocalName, StringComparer.Ordinal);
            var needed = element.DescendantsAndSelf().SelectMany(e => e.Attributes().Where(a => !a.IsNamespaceDeclaration).Select(a => a.Name.Namespace).Prepend(e.Name.Namespace))
                .Where(ns => ns != XNamespace.None && ns != XNamespace.Xml && !known.Contains(ns.NamespaceName)).Distinct().ToList();
            element.Remove();
            foreach (var ns in needed)
            {
                if (element.Attributes().Any(a => a.IsNamespaceDeclaration && a.Value == ns.NamespaceName))
                    continue;
                var prefix = Usual.FirstOrDefault(u => u.Uri == ns.NamespaceName).Prefix ?? own.GetValueOrDefault(ns.NamespaceName) ?? "ns" + needed.IndexOf(ns);
                element.Add(new XAttribute(XNamespace.Xmlns + prefix, ns.NamespaceName));
            }
        }
        var drawings = elements.SelectMany(e => e.DescendantsAndSelf(Wp + "docPr")).ToList();
        if (drawings.Count > 0)
        {
            if (nextDrawing == 0)
                nextDrawing = DrawingIds().DefaultIfEmpty(0).Max() + 1;
            foreach (var drawing in drawings)
                drawing.SetAttributeValue("id", nextDrawing++);
        }
        return elements;
    }

    /// <summary>The ids the main part's drawings have (wp:docPr), as it reads now.</summary>
    IEnumerable<int> DrawingIds() =>
        islands.Select(i => i.Text).Prepend(wholeText.Text).SelectMany(text => DrawingId().Matches(text))
            .Select(m => int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0);

    [GeneratedRegex(@"docPr\b[^>]*?\sid=""(\d{1,9})""")]
    private static partial Regex DrawingId();

    /// <summary>The paragraphs of a block's text boxes and table cells (and theirs), in reading order, not the block itself.</summary>
    static IEnumerable<int> Within(Block block)
    {
        foreach (var list in ModelReader.Inner(block))
        {
            foreach (var inner in list)
            {
                if (inner is ParagraphBlock paragraph)
                    yield return paragraph.Id;
                foreach (var id in Within(inner))
                    yield return id;
            }
        }
    }

    /// <summary>Gives parts new bytes, or takes them away (null), in the package and as the document reads now (see partBytes).</summary>
    void Put(IReadOnlyDictionary<string, byte[]?> changes)
    {
        var builder = partBytes.ToBuilder();
        foreach (var (name, bytes) in changes)
        {
            Set(name, bytes);
            builder[name] = bytes;
        }
        partBytes = builder.ToImmutable();
    }

    /// <summary>The other parts put back as a version has them (see Version), those it did not change as the package was opened.</summary>
    void Restore(ImmutableDictionary<string, byte[]?> to)
    {
        if (ReferenceEquals(to, partBytes))
            return;
        foreach (var name in partBytes.Keys.Concat(to.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList())
        {
            var (had, has) = (partBytes.TryGetValue(name, out var now), to.TryGetValue(name, out var then));
            if (had && has && ReferenceEquals(now, then))
                continue;
            Set(name, has ? then : package.Original(name));
        }
        partBytes = to;
    }

    void Set(string name, byte[]? bytes)
    {
        if (bytes != null)
            package.Replace(name, bytes);
        else
            package.Remove(name);
        named?.Add(name);
        context.Forget(name);
    }

    static XDocument Xml(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }

    static byte[] Bytes(XDocument document)
    {
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false) }))
            document.Save(writer);
        return stream.ToArray();
    }
}
