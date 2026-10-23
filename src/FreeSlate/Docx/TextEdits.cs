using System.Xml.Linq;
using FreeSlate.Opc;

namespace FreeSlate.Docx;

/// <summary>
/// Changes to a paragraph made in the XML it was read from (catalog: package.opc): the part's bytes parsed afresh, the
/// elements the model was read from found there again by where they stand (see Inline.Xml, PartText.Start), the change
/// made to them, and the paragraphs it touched written back into the part's bytes, every other byte as it was. Places
/// are offsets in a paragraph's text (ParagraphText). Text can change where the model has it as the text of a w:t;
/// tabs, breaks, symbols, hyphens and drawings can be taken away whole. What the reader made of other things (a field's
/// result, a list number, a content control's bound text) and a paragraph it joined from several cannot be changed so:
/// that throws NotSupportedException, and nothing is changed.
/// </summary>
static class TextEdits
{
    static readonly XNamespace W14 = "http://schemas.microsoft.com/office/word/2010/wordml";

    /// <summary>The part an element was read from, parsed afresh: its text, its XML and its elements by where they start.</summary>
    sealed class Part
    {
        readonly Dictionary<int, XElement> starts = [];

        public Part(DocumentContext context, XElement read)
        {
            Name = context.PartOf(read) ?? throw new NotSupportedException("The paragraph was not read from a part of the package.");
            Read = read.Document!;
            var bytes = context.Package.ReadBytes(Name);
            Text = new PartText(bytes);
            Xml = XDocument.Load(new MemoryStream(bytes), LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            foreach (var element in Xml.Descendants())
                starts.TryAdd(Text.Start(element), element);
        }

        public string Name { get; }
        public PartText Text { get; }
        public XDocument Xml { get; }

        /// <summary>The part's XML as the document was read from it.</summary>
        XDocument Read { get; }

        /// <summary>
        /// The element a read element of the part is in the part's XML, or null where it was made or moved after reading
        /// or read from another part (a placeholder's text from the glossary).
        /// </summary>
        public XElement? Find(XElement? read) =>
            read != null && read.Document == Read && Text.Start(read) is var at and >= 0 && starts.TryGetValue(at, out var found) && found.Name.LocalName == read.Name.LocalName
                ? found : null;

        /// <summary>The part's bytes with the given paragraphs (elements of Xml) written anew in their places, each with what follows it.</summary>
        public byte[] Written(IEnumerable<(XElement Paragraph, IEnumerable<XElement> Written)> paragraphs) =>
            Text.Spliced(paragraphs.Select(p =>
            {
                var start = Text.Start(p.Paragraph);
                return (start, Text.End(start), string.Concat(p.Written.Select(PartText.Xml)));
            }));
    }

    /// <summary>The text the reader takes from a w:t: trimmed without xml:space preserve, its line breaks as spaces (see ModelReader).</summary>
    static string ReadText(XElement t)
    {
        var space = t.Attribute(XNamespace.Xml + "space")?.Value ?? t.Document?.Root?.Attribute(XNamespace.Xml + "space")?.Value;
        var text = space == "preserve" ? t.Value : t.Value.Trim(' ', '\t', '\r', '\n').Replace('\t', ' ');
        return text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
    }

    /// <summary>
    /// The w:t a text inline was read from, in the part afresh, where it stands in the paragraph (a content control
    /// showing its placeholder may show another control's text) and its text is the element's text; else null.
    /// </summary>
    static XElement? TextOf(Part part, Inline inline, XElement paragraph) =>
        inline is TextInline text && text.Xml is { Name.LocalName: "t" } read && part.Find(read) is { } t && t.Ancestors().Contains(paragraph) && ReadText(t) == text.Text ? t : null;

    /// <summary>The fields begun and not ended among some elements (begun counting one, ended minus one).</summary>
    static int OpenFields(IEnumerable<XElement> elements) =>
        elements.Where(e => e.Name.LocalName == "fldChar").Sum(c => c.Attribute(c.Name.Namespace + "fldCharType")?.Value switch { "begin" => 1, "end" => -1, _ => 0 });

    /// <summary>Sets a w:t's text, kept as it is (xml:space preserve) where spaces begin or end it or tabs are in it.</summary>
    static void SetText(XElement t, string text)
    {
        t.Value = text;
        if (text.Length > 0 && (char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[^1]) || text.Contains('\t') || text.Contains("  ")))
            t.SetAttributeValue(XNamespace.Xml + "space", "preserve");
    }

    /// <summary>Whether an inline is taken away whole with the element it was read from (a tab, a break, a symbol, a drawing).</summary>
    static bool Removable(Inline inline) =>
        inline is TabInline or BreakInline or DrawingInline or AnchorInline
        || inline is TextInline { Xml.Name.LocalName: "sym" or "noBreakHyphen" or "softHyphen" };

    /// <summary>The paragraph (w:p) an element stands in, nearest.</summary>
    static XElement ParagraphOf(XElement element) =>
        element.AncestorsAndSelf().FirstOrDefault(a => a.Name.LocalName == "p" && a.Name.Namespace == element.Document?.Root?.Name.Namespace)
        ?? throw new NotSupportedException("The text stands in no paragraph.");

    /// <summary>
    /// The text of a paragraph from <paramref name="offset"/>, so many characters, replaced by <paramref name="text"/>:
    /// the new text goes into the text it continues (the text before it, else after it, as typing takes the format before
    /// it), else into a run of its own with the format of the run before it. Returns the part and its new bytes.
    /// </summary>
    public static (string Part, byte[] Bytes) Replace(DocumentContext context, ParagraphBlock paragraph, int offset, int length, string text)
    {
        if (offset < paragraph.TextOffset || length < 0 || offset + length > ParagraphText.End(paragraph))
            throw new ArgumentOutOfRangeException(nameof(offset), "The range is not in the paragraph's text.");
        if (text.Any(c => char.IsControl(c) && c != '\t'))
            throw new ArgumentException("The text holds control characters; breaks and paragraphs are not text.", nameof(text));
        var part = new Part(context, paragraph.Element);
        var p = part.Find(paragraph.Element) ?? throw new NotSupportedException("The paragraph is not where it was read.");
        var end = offset + length;
        var changed = new Dictionary<XElement, string>();
        var removed = new List<XElement>();
        (XElement T, int Index)? target = null;
        XElement? before = null, after = null;
        var at = paragraph.TextOffset;
        foreach (var inline in paragraph.Content)
        {
            var (s, e) = (at, at + ParagraphText.Length(inline));
            at = e;
            if (e == s)
                continue;
            var t = TextOf(part, inline, p);
            // What was not read from the paragraph (the paragraph's before a deleted mark or after a hidden one, a
            // placeholder shown from elsewhere) stays as it is, and new text does not continue it.
            if (t == null && Foreign(part, inline, p))
            {
                if (Math.Max(s, offset) < Math.Min(e, end) || text.Length > 0 && s < offset && offset <= e)
                    throw new NotSupportedException("The range reaches into content of another paragraph (one whose mark is hidden or deleted, or a placeholder).");
                continue;
            }
            if (t != null)
            {
                var current = changed.GetValueOrDefault(t, ((TextInline)inline).Text);
                if (Math.Max(s, offset) < Math.Min(e, end))
                    current = current[..(Math.Max(offset, s) - s)] + current[(Math.Min(end, e) - s)..];
                changed[t] = current;
                // The new text continues the text before the place, else the text after it.
                if (target == null && s <= offset && offset <= e)
                    target = (t, offset - s);
                if (e <= offset)
                    before = t;
                else if (after == null && s >= end)
                    after = t;
            }
            else if (Math.Max(s, offset) < Math.Min(e, end))
            {
                if (!Removable(inline) || part.Find(inline.Xml) is not { } element || !element.Ancestors().Contains(p))
                    throw new NotSupportedException($"The range holds what cannot be taken away so ({inline.GetType().Name}).");
                removed.Add(element);
            }
            else if (s == offset && after == null)
                after = part.Find(inline.Xml);
        }
        foreach (var (t, value) in changed.ToList())
        {
            if (target is { } into && into.T == t)
                changed[t] = value[..into.Index] + text + value[into.Index..];
        }
        var paragraphs = new HashSet<XElement>();
        foreach (var (t, value) in changed)
        {
            if (value == ReadText(t))
                continue;
            SetText(t, value);
            paragraphs.Add(ParagraphOf(t));
        }
        foreach (var element in removed)
        {
            paragraphs.Add(ParagraphOf(element));
            var run = element.Parent;
            element.Remove();
            // A run left with nothing but its properties goes too.
            if (run is { Name.LocalName: "r" } && !run.Elements().Any(c => c.Name.LocalName != "rPr"))
                run.Remove();
        }
        if (target == null && text.Length > 0)
            paragraphs.Add(NewRun(p, before, after, text));
        return (part.Name, part.Written(Outermost(paragraphs).Select(p => (p, (IEnumerable<XElement>)[p]))));
    }

    /// <summary>
    /// A run of its own for new text where no text is to continue: after the run of the text before it, before the run
    /// of what comes after it, else at the paragraph's end; in the format of the run it follows or precedes, else of the
    /// paragraph's mark. Returns the paragraph it went into.
    /// </summary>
    static XElement NewRun(XElement p, XElement? before, XElement? after, string text)
    {
        var w = p.Name.Namespace;
        var beside = (before ?? after)?.AncestorsAndSelf(w + "r").FirstOrDefault();
        var properties = beside?.Element(w + "rPr") ?? p.Element(w + "pPr")?.Element(w + "rPr");
        var t = new XElement(w + "t");
        SetText(t, text);
        var run = new XElement(w + "r", properties != null ? new XElement(w + "rPr", properties.Elements().Where(e => e.Name.LocalName is not ("ins" or "del" or "rPrChange"))) : null, t);
        if (before?.AncestorsAndSelf(w + "r").FirstOrDefault() is { } previous)
            previous.AddAfterSelf(run);
        else if (after?.AncestorsAndSelf(w + "r").FirstOrDefault() is { } next)
            next.AddBeforeSelf(run);
        else
            p.Add(run);
        return ParagraphOf(run);
    }

    /// <summary>The paragraphs not within another of them.</summary>
    static IEnumerable<XElement> Outermost(HashSet<XElement> paragraphs) => paragraphs.Where(p => !p.Ancestors().Any(paragraphs.Contains));

    /// <summary>
    /// A paragraph split in two at a place (as Enter splits it): what comes before stays, what comes after goes into a
    /// new paragraph after it with the same paragraph properties, the section the paragraph ends (its w:sectPr) going with
    /// the second, which keeps the mark. Runs and the hyperlinks, insertions and smart tags around the place are split
    /// too. Returns the part, its new bytes and where the second paragraph begins in its text.
    /// </summary>
    public static (string Part, byte[] Bytes, int Second) Split(DocumentContext context, ParagraphBlock paragraph, int offset)
    {
        if (offset < paragraph.TextOffset || offset > ParagraphText.End(paragraph))
            throw new ArgumentOutOfRangeException(nameof(offset), "The place is not in the paragraph's text.");
        var part = new Part(context, paragraph.Element);
        var p = part.Find(paragraph.Element) ?? throw new NotSupportedException("The paragraph is not where it was read.");
        Whole(part, paragraph, p);
        var w = p.Name.Namespace;
        // Where the second paragraph begins: before an element of the paragraph, or at its end.
        XElement? first = null;
        var at = paragraph.TextOffset;
        foreach (var inline in paragraph.Content)
        {
            var (s, e) = (at, at + ParagraphText.Length(inline));
            at = e;
            if (e <= offset && !(e == offset && s == offset && inline is not (BookmarkEndInline or PermissionInline { Start: false } or CommentInline { Start: false })))
                continue;
            if (s >= offset)
            {
                var element = part.Find(inline.Xml);
                if (element == null || !element.Ancestors().Contains(p))
                {
                    // A mark the reader moved in from before the paragraph stays where it is.
                    if (e == s)
                        continue;
                    throw new NotSupportedException($"What the place is before was not read from the paragraph ({inline.GetType().Name}).");
                }
                first = element;
                break;
            }
            // Within a text: its run split there.
            var t = TextOf(part, inline, p) ?? throw new NotSupportedException($"The place is within what cannot be split ({inline.GetType().Name}).");
            var value = ReadText(t);
            var rest = new XElement(t);
            SetText(t, value[..(offset - s)]);
            SetText(rest, value[(offset - s)..]);
            t.AddAfterSelf(rest);
            first = rest;
            break;
        }
        var second = new XElement(p.Name, p.Attributes().Where(a => a.Name.Namespace != W14));
        if (p.Element(w + "pPr") is { } pPr)
        {
            second.Add(new XElement(pPr));
            pPr.Element(w + "sectPr")?.Remove();
        }
        if (first != null)
        {
            // The element and what follows it, out of each element around it up to the paragraph, which is split there.
            var moved = first;
            while (moved.Parent != p)
            {
                var around = moved.Parent ?? throw new NotSupportedException("The place stands in no paragraph.");
                if (around.Name.LocalName is not ("r" or "hyperlink" or "ins" or "moveTo" or "smartTag" or "customXml" or "bdo" or "dir"))
                    throw new NotSupportedException($"The place is within a w:{around.Name.LocalName}, which is not split.");
                var copy = new XElement(around.Name, around.Attributes(),
                    around.Elements().Where(e => e.Name.LocalName is "rPr" or "smartTagPr" or "customXmlPr").Select(e => new XElement(e)));
                var following = moved.NodesAfterSelf().ToList();
                moved.Remove();
                copy.Add(moved);
                foreach (var node in following)
                {
                    node.Remove();
                    copy.Add(node);
                }
                around.AddAfterSelf(copy);
                moved = copy;
            }
            // A field begun before the place and not ended there would be split, which Word does not do.
            if (OpenFields(moved.ElementsBeforeSelf().SelectMany(e => e.DescendantsAndSelf())) != 0)
                throw new NotSupportedException("The place is within a field.");
            var after = moved.NodesAfterSelf().ToList();
            moved.Remove();
            second.Add(moved);
            foreach (var node in after)
            {
                node.Remove();
                second.Add(node);
            }
        }
        // In the part, so that it is written with the prefixes of its scope.
        p.AddAfterSelf(second);
        return (part.Name, part.Written([(p, [p, second])]), part.Text.Start(p) + PartText.Xml(p).Length);
    }

    /// <summary>A paragraph taken away with all its content, the section it ends merging with the next one.</summary>
    public static (string Part, byte[] Bytes) Remove(DocumentContext context, ParagraphBlock paragraph)
    {
        var part = new Part(context, paragraph.Element);
        var p = part.Find(paragraph.Element) ?? throw new NotSupportedException("The paragraph is not where it was read.");
        Whole(part, paragraph, p);
        // A body, a cell or a text box ends with a paragraph (the body before its own section properties).
        if (!p.ElementsAfterSelf().Any(e => e.Name.LocalName is "p" or "tbl" or "sdt" or "customXml"))
            throw new NotSupportedException("The paragraph is the last of its body, cell or text box.");
        // Nor does a field over several paragraphs lose its beginning or its end.
        if (OpenFields(p.Descendants()) != 0)
            throw new NotSupportedException("The paragraph holds the beginning or the end of a field over several paragraphs.");
        return (part.Name, part.Written([(p, [])]));
    }

    /// <summary>
    /// Checks that a paragraph's content was read from it alone (not joined with another), bookmarks and other marks
    /// aside, which the reader moves into the paragraph after them.
    /// </summary>
    static void Whole(Part part, ParagraphBlock paragraph, XElement p)
    {
        if (paragraph.Content.Any(inline => ParagraphText.Length(inline) > 0 && Foreign(part, inline, p)))
            throw new NotSupportedException("The paragraph holds content of another paragraph (one whose mark is hidden or deleted, or a placeholder).");
    }

    /// <summary>
    /// Whether an inline of a paragraph was not read from it: from the paragraph before, whose deleted mark joins it to
    /// this one, from the next, which this one's hidden mark joins, a placeholder another content control or the glossary
    /// shows; what the reader made of nothing (Xml null) aside.
    /// </summary>
    static bool Foreign(Part part, Inline inline, XElement p) =>
        inline.Xml != null && (part.Find(inline.Xml) is not { } element || !element.Ancestors().Contains(p));
}
