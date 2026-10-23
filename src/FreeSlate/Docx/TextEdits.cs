using System.Xml.Linq;
using FreeSlate.Opc;

namespace FreeSlate.Docx;

/// <summary>
/// A stretch of the body's XML as text (catalog: package.opc), for changing what was read from it (see WordDocument): its
/// elements parsed afresh within copies of the elements around them in the part (its root and body, which declare their
/// namespaces), each known by where it starts, the elements the model was read from found there again by where they
/// stand (which the document knows from their line info), and the elements changed written back into the text, every
/// other character as it was.
/// </summary>
sealed class Stretch
{
    readonly Dictionary<int, XElement> starts = [];
    readonly Func<XElement, int> where;
    readonly int head, tail;

    /// <summary>
    /// The stretch <paramref name="text"/> within <paramref name="head"/> and <paramref name="tail"/>, the start and end
    /// tags around it; <paramref name="where"/> tells where an element the model was read from starts in their text, or
    /// -1 for one not read from the stretch.
    /// </summary>
    public Stretch(string head, string text, string tail, Func<XElement, int> where)
    {
        Text = new PartText(head + text + tail);
        Xml = XDocument.Parse(Text.Text, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        foreach (var element in Xml.Descendants())
            starts.TryAdd(Text.Start(element), element);
        (this.where, this.head, this.tail) = (where, head.Length, tail.Length);
    }

    /// <summary>The stretch's text within the tags around it, and its XML parsed from it.</summary>
    public PartText Text { get; }
    public XDocument Xml { get; }

    /// <summary>Where the stretch's own text begins in Text.</summary>
    public int Head => head;

    /// <summary>The element a read element of the stretch is in its XML, or null where it was made after reading or read elsewhere.</summary>
    public XElement? Find(XElement? read) =>
        read != null && where(read) is var at and >= 0 && starts.TryGetValue(at, out var found) && found.Name.LocalName == read.Name.LocalName ? found : null;

    /// <summary>The stretch's own text with the given elements (of Xml) written anew in their places, each with what follows it.</summary>
    public string Written(IEnumerable<(XElement Element, IEnumerable<XElement> Written)> elements) =>
        Spliced(elements.Select(e =>
        {
            var start = Text.Start(e.Element);
            return (start, Text.End(start), string.Concat(e.Written.Select(PartText.Xml)));
        }));

    /// <summary>The stretch's own text with stretches of Text (not overlapping) replaced.</summary>
    public string Spliced(IEnumerable<(int Start, int End, string Text)> changes)
    {
        var text = Text.Replaced(changes);
        return text[head..^tail];
    }
}

/// <summary>
/// Changes to paragraphs made in the XML they were read from (catalog: package.opc): in a stretch of the body's XML
/// parsed afresh (see Stretch), the elements the model was read from found there again by where they stand (see
/// Inline.Xml), the change made to them, and the paragraphs it touched written back into the stretch's text, every
/// other character as it was. Places are offsets in a paragraph's text (ParagraphText). Text can change where the model
/// has it as the text of a w:t; tabs, breaks, symbols, hyphens and drawings can be taken away whole. What the reader
/// made of other things (a field's result, a list number, a content control's bound text) and a paragraph it joined from
/// several cannot be changed so: that throws NotSupportedException, and nothing is changed.
/// </summary>
static class TextEdits
{
    static readonly XNamespace W14 = "http://schemas.microsoft.com/office/word/2010/wordml";

    /// <summary>The text the reader takes from a w:t: trimmed without xml:space preserve, its line breaks as spaces (see ModelReader).</summary>
    static string ReadText(XElement t)
    {
        var space = t.Attribute(XNamespace.Xml + "space")?.Value ?? t.Document?.Root?.Attribute(XNamespace.Xml + "space")?.Value;
        var text = space == "preserve" ? t.Value : t.Value.Trim(' ', '\t', '\r', '\n').Replace('\t', ' ');
        return text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
    }

    /// <summary>
    /// The w:t a text inline was read from, in the stretch afresh, where it stands in the paragraph (a content control
    /// showing its placeholder may show another control's text) and its text is the element's text; else null.
    /// </summary>
    static XElement? TextOf(Stretch stretch, Inline inline, XElement paragraph) =>
        inline is TextInline text && text.Xml is { Name.LocalName: "t" } read && stretch.Find(read) is { } t && t.Ancestors().Contains(paragraph) && ReadText(t) == text.Text ? t : null;

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

    /// <summary>Checks that a place of a paragraph's text does not fall between the halves of a character (a surrogate pair).</summary>
    static void Between(ParagraphBlock paragraph, int offset)
    {
        var at = paragraph.TextOffset;
        foreach (var inline in paragraph.Content)
        {
            var length = ParagraphText.Length(inline);
            if (inline is TextInline text && offset > at && offset < at + length && char.IsHighSurrogate(text.Text[offset - at - 1]) && char.IsLowSurrogate(text.Text[offset - at]))
                throw new ArgumentOutOfRangeException(nameof(offset), "The place is within a character (between the halves of a surrogate pair).");
            at += length;
        }
    }

    /// <summary>The element a paragraph was read from, in the stretch afresh.</summary>
    static XElement Find(Stretch stretch, ParagraphBlock paragraph) =>
        stretch.Find(paragraph.Element) ?? throw new NotSupportedException("The paragraph is not where it was read.");

    /// <summary>
    /// The text of a paragraph from <paramref name="offset"/>, so many characters, replaced by <paramref name="text"/>:
    /// the new text goes into the text it continues (the text before it, else after it, as typing takes the format before
    /// it), else into a run of its own with the format of the run before it. Returns the stretch's new text.
    /// </summary>
    public static string Replace(Stretch stretch, ParagraphBlock paragraph, int offset, int length, string text)
    {
        if (offset < paragraph.TextOffset || length < 0 || offset + length > ParagraphText.End(paragraph))
            throw new ArgumentOutOfRangeException(nameof(offset), "The range is not in the paragraph's text.");
        if (text.Any(c => char.IsControl(c) && c != '\t'))
            throw new ArgumentException("The text holds control characters; breaks and paragraphs are not text.", nameof(text));
        Between(paragraph, offset);
        Between(paragraph, offset + length);
        var p = Find(stretch, paragraph);
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
            var t = TextOf(stretch, inline, p);
            // What was not read from the paragraph (the paragraph's before a deleted mark or after a hidden one, a
            // placeholder shown from elsewhere) stays as it is, and new text does not continue it.
            if (t == null && Foreign(stretch, inline, p))
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
                if (!Removable(inline) || stretch.Find(inline.Xml) is not { } element || !element.Ancestors().Contains(p))
                    throw new NotSupportedException($"The range holds what cannot be taken away so ({inline.GetType().Name}).");
                removed.Add(element);
            }
            else if (s == offset && after == null)
                after = stretch.Find(inline.Xml);
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
        return stretch.Written(Outermost(paragraphs).Select(p => (p, (IEnumerable<XElement>)[p])));
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
    /// too. Returns the stretch's new text and where the second paragraph begins in it.
    /// </summary>
    public static (string Text, int Second) Split(Stretch stretch, ParagraphBlock paragraph, int offset)
    {
        if (offset < paragraph.TextOffset || offset > ParagraphText.End(paragraph))
            throw new ArgumentOutOfRangeException(nameof(offset), "The place is not in the paragraph's text.");
        Between(paragraph, offset);
        var p = Find(stretch, paragraph);
        Whole(stretch, paragraph, p);
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
                var element = stretch.Find(inline.Xml);
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
            var t = TextOf(stretch, inline, p) ?? throw new NotSupportedException($"The place is within what cannot be split ({inline.GetType().Name}).");
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
        // In the stretch, so that it is written with the prefixes of its scope.
        p.AddAfterSelf(second);
        return (stretch.Written([(p, [p, second])]), stretch.Text.Start(p) - stretch.Head + PartText.Xml(p).Length);
    }

    /// <summary>A paragraph taken away with all its content, the section it ends merging with the next one.</summary>
    public static string Remove(Stretch stretch, ParagraphBlock paragraph)
    {
        var p = Find(stretch, paragraph);
        Whole(stretch, paragraph, p);
        // A body, a cell or a text box ends with a paragraph (the body before its own section properties): seen in the
        // document, as the stretch may end with the paragraph.
        if (!paragraph.Element.ElementsAfterSelf().Any(e => e.Name.LocalName is "p" or "tbl" or "sdt" or "customXml"))
            throw new NotSupportedException("The paragraph is the last of its body, cell or text box.");
        // Nor does a field over several paragraphs lose its beginning or its end.
        if (OpenFields(p.Descendants()) != 0)
            throw new NotSupportedException("The paragraph holds the beginning or the end of a field over several paragraphs.");
        return stretch.Written([(p, [])]);
    }

    /// <summary>
    /// A paragraph joined with the paragraph after it, as Delete at its end joins them (measured in Word 365 by COM,
    /// Delete, Backspace and Range.Delete alike): the first keeps its properties (its style, alignment, spacing, indents,
    /// list), the second's content goes after its own, and it takes the second's mark (the mark's run properties) and the
    /// section the second ends (the section the first ended merging with the next); an empty first goes away with what
    /// marks it holds (a bookmark), the second staying as it was, but for what the first holds of a field over paragraphs
    /// (the end of a table of contents), which goes on at the second's beginning. Marks between them (bookmarks, comment
    /// ranges) go between their contents. Returns the stretch's new text and whether the first paragraph stays.
    /// </summary>
    public static (string Text, bool First) Join(Stretch stretch, ParagraphBlock paragraph, ParagraphBlock next)
    {
        var p = Find(stretch, paragraph);
        var q = Find(stretch, next);
        Whole(stretch, paragraph, p);
        Whole(stretch, next, q);
        var marks = new List<XElement>();
        for (var node = p.NextNode; node != q; node = node.NextNode)
        {
            if (node == null)
                throw new NotSupportedException("The paragraphs do not follow one another.");
            if (node is XElement e)
            {
                if (!IsMark(e))
                    throw new NotSupportedException($"A w:{e.Name.LocalName} stands between the paragraphs.");
                marks.Add(e);
            }
        }
        var w = p.Name.Namespace;
        if (ParagraphText.End(paragraph) == paragraph.TextOffset)
        {
            // What an empty first holds of a field over paragraphs (a table of contents' end) goes on into the second.
            if (p.Descendants(w + "fldChar").Any())
            {
                var held = p.Nodes().Where(n => n is not XElement { Name.LocalName: "pPr" }).ToList();
                foreach (var node in held.Concat(marks))
                    node.Remove();
                if (q.Element(w + "pPr") is { } own)
                    own.AddAfterSelf(held, marks);
                else
                    q.AddFirst(held, marks);
                return (stretch.Spliced([(stretch.Text.Start(p), stretch.Text.End(stretch.Text.Start(q)), PartText.Xml(q))]), false);
            }
            return (stretch.Written([(p, [])]), false);
        }
        var (start, end) = (stretch.Text.Start(p), stretch.Text.End(stretch.Text.Start(q)));
        // The second's mark and section in the first's properties, where they stand in w:pPr: before a change of them.
        var properties = p.Element(w + "pPr");
        properties?.Element(w + "rPr")?.Remove();
        properties?.Element(w + "sectPr")?.Remove();
        var taken = q.Element(w + "pPr")?.Elements().Where(e => e.Name.LocalName is "rPr" or "sectPr").Select(e => new XElement(e)).ToList() ?? [];
        if (taken.Count > 0)
        {
            if (properties == null)
                p.AddFirst(properties = new XElement(w + "pPr"));
            if (properties.Element(w + "pPrChange") is { } change)
                change.AddBeforeSelf(taken);
            else
                properties.Add(taken);
        }
        var content = q.Nodes().Where(n => n is not XElement { Name.LocalName: "pPr" }).ToList();
        foreach (var node in marks.Concat(content))
            node.Remove();
        p.Add(marks, content);
        return (stretch.Spliced([(start, end, PartText.Xml(p))]), true);
    }

    /// <summary>Whether an element between paragraphs is a mark, which goes into a paragraph (a bookmark, a comment's range, a proofing mark).</summary>
    static bool IsMark(XElement element) =>
        element.Name.LocalName is "bookmarkStart" or "bookmarkEnd" or "commentRangeStart" or "commentRangeEnd" or "permStart" or "permEnd" or "proofErr"
        || element.Name.LocalName.EndsWith("RangeStart", StringComparison.Ordinal) || element.Name.LocalName.EndsWith("RangeEnd", StringComparison.Ordinal);

    /// <summary>
    /// Checks that a paragraph's content was read from it alone (not joined with another), bookmarks and other marks
    /// aside, which the reader moves into the paragraph after them.
    /// </summary>
    static void Whole(Stretch stretch, ParagraphBlock paragraph, XElement p)
    {
        if (paragraph.Content.Any(inline => ParagraphText.Length(inline) > 0 && Foreign(stretch, inline, p)))
            throw new NotSupportedException("The paragraph holds content of another paragraph (one whose mark is hidden or deleted, or a placeholder).");
    }

    /// <summary>
    /// Whether an inline of a paragraph was not read from it: from the paragraph before, whose deleted mark joins it to
    /// this one, from the next, which this one's hidden mark joins, a placeholder another content control or the glossary
    /// shows; what the reader made of nothing (Xml null) aside.
    /// </summary>
    static bool Foreign(Stretch stretch, Inline inline, XElement p) =>
        inline.Xml != null && (stretch.Find(inline.Xml) is not { } element || !element.Ancestors().Contains(p));
}
