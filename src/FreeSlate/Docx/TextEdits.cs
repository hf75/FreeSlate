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
/// other character as it was. Places are offsets in a paragraph's text (ParagraphText), over all the paragraphs it was
/// read from where the reader joined several (see ParagraphBlock.Parts). Text can change where the model has it as the
/// text of a w:t; tabs, breaks, symbols, hyphens and drawings can be taken away whole. What the reader made of other
/// things (a field's result, a list number, a content control's bound text) and what it shows from elsewhere (a
/// placeholder) cannot be changed so: that throws NotSupportedException, and nothing is changed.
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
    static XElement? TextOf(Stretch stretch, Inline inline, List<XElement> parts) =>
        inline is TextInline text && text.Xml is { Name.LocalName: "t" } read && stretch.Find(read) is { } t && In(t, parts) && ReadText(t) == text.Text ? t : null;

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

    /// <summary>
    /// Sets a w:t's text where it may hold tabs and line breaks ("\t", "\v"), as Word's Tab and Shift+Enter write them:
    /// each a w:tab or a w:br of its own in the run, between w:t of the text around it (none where that is empty).
    /// </summary>
    static void SetContent(XElement t, string text)
    {
        if (text.IndexOfAny(['\t', '\v']) < 0)
        {
            SetText(t, text);
            return;
        }
        var w = t.Name.Namespace;
        var nodes = new List<XElement>();
        var start = 0;
        for (var i = 0; i <= text.Length; i++)
        {
            if (i < text.Length && text[i] is not ('\t' or '\v'))
                continue;
            if (i > start)
            {
                var piece = new XElement(t.Name);
                SetText(piece, text[start..i]);
                nodes.Add(piece);
            }
            if (i < text.Length)
                nodes.Add(new XElement(w + (text[i] == '\t' ? "tab" : "br")));
            start = i + 1;
        }
        t.ReplaceWith(nodes);
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

    /// <summary>
    /// The elements a paragraph was read from, in the stretch afresh and in their order: its own, or the paragraphs the
    /// reader joined into it (see ParagraphBlock.Parts), which follow one another in one body, cell or text box.
    /// </summary>
    static List<XElement> Parts(Stretch stretch, ParagraphBlock paragraph) =>
        [.. (paragraph.Parts ?? [paragraph.Element]).Select(e => stretch.Find(e) ?? throw new NotSupportedException("The paragraph is not where it was read."))];

    /// <summary>Whether an element stands in a paragraph's parts (in their content, or in a text box there).</summary>
    static bool In(XElement element, List<XElement> parts) => element.Ancestors().Any(parts.Contains);

    /// <summary>The part of a paragraph an element stands in.</summary>
    static XElement PartOf(XElement element, List<XElement> parts) => element.Ancestors().First(parts.Contains);

    /// <summary>Whether a paragraph's mark is deleted or moved away (a tracked change).</summary>
    static bool MarkDeleted(XElement p) =>
        p.Element(p.Name.Namespace + "pPr")?.Element(p.Name.Namespace + "rPr") is { } mark && (mark.Element(p.Name.Namespace + "del") ?? mark.Element(p.Name.Namespace + "moveFrom")) != null;

    /// <summary>
    /// The text of a paragraph from <paramref name="offset"/>, so many characters, replaced by <paramref name="text"/>
    /// (its tabs and line breaks, "\t" and "\v", as w:tab and w:br, see SetContent): the new text goes into the text it
    /// continues (the text before it, else after it, as typing takes the format before it), else into a run of its own
    /// where the place is, with the format of the run before it; within text the document computes (a date, a field's
    /// result) it goes nowhere. Returns the stretch's new text.
    /// </summary>
    public static string Replace(Stretch stretch, ParagraphBlock paragraph, int offset, int length, string text, Tracking? tracking = null)
    {
        if (offset < paragraph.TextOffset || length < 0 || offset + length > ParagraphText.End(paragraph))
            throw new ArgumentOutOfRangeException(nameof(offset), "The range is not in the paragraph's text.");
        if (text.Any(c => char.IsControl(c) && c is not ('\t' or '\v')))
            throw new ArgumentException("The text holds control characters other than tabs and line breaks; paragraphs are not text.", nameof(text));
        Between(paragraph, offset);
        Between(paragraph, offset + length);
        if (tracking != null)
            return ReplaceTracked(stretch, paragraph, offset, length, text, tracking);
        var parts = Parts(stretch, paragraph);
        var end = offset + length;
        var changed = new Dictionary<XElement, string>();
        var removed = new List<XElement>();
        (XElement T, int Index)? target = null;
        XElement? before = null, after = null;
        var seen = new HashSet<XElement>();
        var at = paragraph.TextOffset;
        foreach (var inline in paragraph.Content)
        {
            var (s, e) = (at, at + ParagraphText.Length(inline));
            at = e;
            if (e == s)
                continue;
            var t = TextOf(stretch, inline, parts);
            // Text shown a second time (a placeholder showing another's) is not where it stands.
            if (t != null && !seen.Add(t))
            {
                if (Math.Max(s, offset) < Math.Min(e, end) || text.Length > 0 && target == null && s <= offset && offset <= e)
                    throw new NotSupportedException("The range reaches into text shown twice (a placeholder showing another's).");
                continue;
            }
            // What was not read from the paragraph (a placeholder shown from elsewhere) stays as it is, and new text does
            // not continue it.
            if (t == null && Foreign(stretch, inline, parts))
            {
                if (Math.Max(s, offset) < Math.Min(e, end) || text.Length > 0 && s < offset && offset <= e)
                    throw new NotSupportedException("The range reaches into content shown from elsewhere (a placeholder).");
                // New text before it would belong to where it comes from.
                if (text.Length > 0 && s == offset && target == null)
                    throw new NotSupportedException("The place is before content shown from elsewhere (a placeholder).");
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
            else if (text.Length > 0 && s < offset && offset < e)
                throw new NotSupportedException($"The place is within text the document computes ({inline.GetType().Name}).");
            else if (Math.Max(s, offset) < Math.Min(e, end))
            {
                if (!Removable(inline) || stretch.Find(inline.Xml) is not { } element || !In(element, parts))
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
            paragraphs.Add(ParagraphOf(t));
            if (target?.T == t)
                SetContent(t, value);
            else
                SetText(t, value);
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
            paragraphs.Add(NewRun(parts, before, after, text));
        return stretch.Written(Outermost(paragraphs).Select(p => (p, (IEnumerable<XElement>)[p])));
    }

    /// <summary>
    /// A run of its own for new text where no text is to continue: before what comes after the place (its run, or what
    /// holds it in its part of the paragraph: an equation, a field, a content control), else at the end of the paragraph's
    /// last part, never past an object between the text before and the place; in the format of the run of the text before
    /// it or after it, else of the part's mark. Returns the paragraph it went into.
    /// </summary>
    static XElement NewRun(List<XElement> parts, XElement? before, XElement? after, string text)
    {
        var p = after != null ? PartOf(after, parts) : parts[^1];
        var w = p.Name.Namespace;
        var beside = (before ?? after)?.AncestorsAndSelf(w + "r").FirstOrDefault();
        var properties = beside?.Element(w + "rPr") ?? p.Element(w + "pPr")?.Element(w + "rPr");
        var t = new XElement(w + "t");
        var run = new XElement(w + "r", properties != null ? new XElement(w + "rPr", properties.Elements().Where(e => !IsRevision(e) && e.Name.LocalName != "rPrChange")) : null, t);
        SetContent(t, text);
        if ((after?.AncestorsAndSelf(w + "r").FirstOrDefault() ?? after?.AncestorsAndSelf().FirstOrDefault(a => a.Parent == p)) is { } next)
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
    /// too; as a tracked change the first half's mark, the new one, is marked inserted. Of a paragraph the reader joined
    /// from several the part the place is in is split, where the place is between two parts the second; a part that runs
    /// into the next (its mark deleted or hidden) keeps that mark in its second half, the first half ending with a mark
    /// of its own that does not (measured in Word 365 by COM: Enter in the first of two paragraphs joined by a deleted
    /// mark, the final view shown, leaves the first half a plain mark, the second the deleted one). Returns the stretch's
    /// new text and where the second paragraph begins in it.
    /// </summary>
    public static (string Text, int Second) Split(Stretch stretch, ParagraphBlock paragraph, int offset, Tracking? tracking = null)
    {
        if (offset < paragraph.TextOffset || offset > ParagraphText.End(paragraph))
            throw new ArgumentOutOfRangeException(nameof(offset), "The place is not in the paragraph's text.");
        Between(paragraph, offset);
        var parts = Parts(stretch, paragraph);
        Whole(stretch, paragraph, parts);
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
                if (element == null || !In(element, parts))
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
            var t = TextOf(stretch, inline, parts) ?? throw new NotSupportedException($"The place is within what cannot be split ({inline.GetType().Name}).");
            var value = ReadText(t);
            var rest = new XElement(t);
            SetText(t, value[..(offset - s)]);
            SetText(rest, value[(offset - s)..]);
            t.AddAfterSelf(rest);
            first = rest;
            break;
        }
        var p = first != null ? PartOf(first, parts) : parts[^1];
        var w = p.Name.Namespace;
        var second = new XElement(p.Name, p.Attributes().Where(a => a.Name.Namespace != W14));
        if (p.Element(w + "pPr") is { } pPr)
        {
            second.Add(new XElement(pPr));
            pPr.Element(w + "sectPr")?.Remove();
            if (p != parts[^1] && pPr.Element(w + "rPr") is { } joining)
            {
                foreach (var made in joining.Elements().Where(e => e.Name.LocalName is "del" or "moveFrom" or "vanish" or "specVanish").ToList())
                    made.Remove();
            }
        }
        if (first != null)
        {
            // The element and what follows it, out of each element around it up to the paragraph, which is split there:
            // where nothing of it but its properties stands before, it goes whole (no empty run stays behind).
            var moved = first;
            while (moved.Parent != p)
            {
                var around = moved.Parent ?? throw new NotSupportedException("The place stands in no paragraph.");
                if (around.Name.LocalName is not ("r" or "hyperlink" or "ins" or "moveTo" or "smartTag" or "customXml" or "bdo" or "dir"))
                    throw new NotSupportedException($"The place is within a w:{around.Name.LocalName}, which is not split.");
                if (moved.ElementsBeforeSelf().All(e => e.Name.LocalName is "rPr" or "smartTagPr" or "customXmlPr"))
                {
                    moved = around;
                    continue;
                }
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
        if (tracking != null)
            Marked(p, "ins", tracking);
        // In the stretch, so that it is written with the prefixes of its scope.
        p.AddAfterSelf(second);
        return (stretch.Written([(p, [p, second])]), stretch.Text.Start(p) - stretch.Head + PartText.Xml(p).Length);
    }

    /// <summary>
    /// A paragraph taken away with all its content (all the paragraphs it was read from, where the reader joined
    /// several), the section it ends merging with the next one; as a tracked change its runs and its marks are deleted
    /// (see Deleted), what the author inserted going away outright.
    /// </summary>
    public static string Remove(Stretch stretch, ParagraphBlock paragraph, Tracking? tracking = null)
    {
        var parts = Parts(stretch, paragraph);
        Whole(stretch, paragraph, parts);
        // A body, a cell or a text box ends with a paragraph (the body before its own section properties): seen in the
        // document, as the stretch may end with the paragraph.
        if (!(paragraph.Parts?[^1] ?? paragraph.Element).ElementsAfterSelf().Any(e => e.Name.LocalName is "p" or "tbl" or "sdt" or "customXml"))
            throw new NotSupportedException("The paragraph is the last of its body, cell or text box.");
        // Nor does a field over several paragraphs lose its beginning or its end.
        if (OpenFields(parts.SelectMany(p => p.Descendants())) != 0)
            throw new NotSupportedException("The paragraph holds the beginning or the end of a field over several paragraphs.");
        if (tracking == null || parts.All(p => OwnMark(p, tracking)
                && p.Descendants(p.Name.Namespace + "r").All(r => r.Ancestors().FirstOrDefault(IsRevision) is { Name.LocalName: "ins" } ins && tracking.Own(ins))))
            return stretch.Written(parts.Select(p => (p, (IEnumerable<XElement>)[])));
        foreach (var p in parts)
        {
            foreach (var run in p.Descendants(p.Name.Namespace + "r").Where(r => ParagraphOf(r) == p).ToList())
                Deleted(run, tracking);
            if (!MarkDeleted(p))
                Marked(p, "del", tracking);
        }
        return stretch.Written(parts.Select(p => (p, (IEnumerable<XElement>)[p])));
    }

    /// <summary>
    /// A paragraph joined with the paragraph after it, as Delete at its end joins them (measured in Word 365 by COM,
    /// Delete, Backspace and Range.Delete alike): the first keeps its properties (its style, alignment, spacing, indents,
    /// list), the second's content goes after its own, and it takes the second's mark (the mark's run properties) and the
    /// section the second ends (the section the first ended merging with the next); an empty first goes away with what
    /// marks it holds (a bookmark), the second staying as it was, but for what the first holds of a field over paragraphs
    /// (the end of a table of contents), which goes on at the second's beginning. Marks between them (bookmarks, comment
    /// ranges) go between their contents. Of paragraphs the reader joined from several the first's last part and the
    /// second's first part are joined, which takes the first part's mark, deleted where it runs into the second's other
    /// parts (measured in Word 365 by COM: Backspace before such a paragraph keeps its first part's deleted mark); an
    /// empty first goes away with all its parts. Returns the stretch's new text and whether the first paragraph stays.
    /// </summary>
    public static (string Text, bool First) Join(Stretch stretch, ParagraphBlock paragraph, ParagraphBlock next, Tracking? tracking = null)
    {
        var (mine, others) = (Parts(stretch, paragraph), Parts(stretch, next));
        var (p, q) = (mine[^1], others[0]);
        Whole(stretch, paragraph, mine);
        Whole(stretch, next, others);
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
        // As a tracked change the first's mark is deleted, the paragraphs joined as the change is accepted.
        if (tracking != null && !OwnMark(p, tracking))
        {
            Marked(p, "del", tracking);
            return (stretch.Written([(p, [p])]), true);
        }
        if (ParagraphText.End(paragraph) == paragraph.TextOffset)
        {
            // What an empty first holds of a field over paragraphs (a table of contents' end) goes on into the second.
            if (mine.Count > 1 && mine.Any(part => part.Descendants(w + "fldChar").Any()))
                throw new NotSupportedException("The paragraph holds the beginning or the end of a field over several paragraphs.");
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
            return (stretch.Written(mine.Select(part => (part, (IEnumerable<XElement>)[]))), false);
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

    /// <summary>The order of the run properties in w:rPr (CT_ParaRPr, EG_RPrBase): a mark's revisions first.</summary>
    static readonly string[] RunOrder =
    [
        "ins", "del", "moveFrom", "moveTo", "rStyle", "rFonts", "b", "bCs", "i", "iCs", "caps", "smallCaps", "strike", "dstrike", "outline", "shadow",
        "emboss", "imprint", "noProof", "snapToGrid", "vanish", "webHidden", "color", "spacing", "w", "kern", "position", "sz", "szCs", "highlight",
        "u", "effect", "bdr", "shd", "fitText", "vertAlign", "rtl", "cs", "em", "lang", "eastAsianLayout", "specVanish", "oMath",
    ];

    /// <summary>The order of the paragraph properties in w:pPr (CT_PPr): the mark's run properties, the section and a change of them last.</summary>
    static readonly string[] ParagraphOrder =
    [
        "pStyle", "keepNext", "keepLines", "pageBreakBefore", "framePr", "widowControl", "numPr", "suppressLineNumbers", "pBdr", "shd", "tabs",
        "suppressAutoHyphens", "kinsoku", "wordWrap", "overflowPunct", "topLinePunct", "autoSpaceDE", "autoSpaceDN", "bidi", "adjustRightInd",
        "snapToGrid", "spacing", "ind", "contextualSpacing", "mirrorIndents", "suppressOverlap", "jc", "textDirection", "textAlignment",
        "textboxTightWrap", "outlineLvl", "divId", "cnfStyle", "rPr", "sectPr", "pPrChange",
    ];

    /// <summary>
    /// The child of a properties element (w:rPr, w:pPr) with a name, made where there is none in its place by the
    /// schema's order: before the first child that comes after it, elements of other namespaces (extensions, which come
    /// after the schema's own) and a change of the properties (w:rPrChange, w:pPrChange) counting as after it. Where the
    /// element has it twice (docx/external/libreoffice/extras-uiwriter/tdf147199: two w:sz, the reader takes the last),
    /// the others go.
    /// </summary>
    static XElement Property(XElement properties, string name, string[] order)
    {
        var w = properties.Name.Namespace;
        if (properties.Element(w + name) is { } found)
        {
            foreach (var twice in properties.Elements(w + name).Skip(1).ToList())
                twice.Remove();
            return found;
        }
        var rank = Array.IndexOf(order, name);
        var made = new XElement(w + name);
        if (properties.Elements().FirstOrDefault(e => e.Name.Namespace != w || e.Name.LocalName is "rPrChange" or "pPrChange" || Array.IndexOf(order, e.Name.LocalName) > rank) is { } after)
            after.AddBeforeSelf(made);
        else
            properties.Add(made);
        return made;
    }

    /// <summary>The properties element (w:rPr of a run, w:pPr of a paragraph) an element begins with, made where it has none.</summary>
    static XElement Properties(XElement element, string name) =>
        element.Element(element.Name.Namespace + name) ?? Made(element, element.Name.Namespace + name);

    static XElement Made(XElement parent, XName name)
    {
        var made = new XElement(name);
        parent.AddFirst(made);
        return made;
    }

    /// <summary>A length in points as twentieths of a point (twips).</summary>
    static string Twips(double points) => ((int)Math.Round(points * 20)).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Character formatting given to a stretch of a paragraph's text (see TextFormatting): the runs that hold it split
    /// where it begins and ends, each run within it formatted, a field the layout computes with all its runs (begin to
    /// end). Returns the stretch's new text.
    /// </summary>
    public static string Format(Stretch stretch, ParagraphBlock paragraph, int offset, int length, TextFormatting format, Tracking? tracking = null)
    {
        if (offset < paragraph.TextOffset || length < 0 || offset + length > ParagraphText.End(paragraph))
            throw new ArgumentOutOfRangeException(nameof(offset), "The range is not in the paragraph's text.");
        Between(paragraph, offset);
        Between(paragraph, offset + length);
        var parts = Parts(stretch, paragraph);
        var end = offset + length;
        var runs = new List<XElement>();
        var at = paragraph.TextOffset;
        foreach (var inline in paragraph.Content)
        {
            var (s, e) = (at, at + ParagraphText.Length(inline));
            at = e;
            if (e == s || e <= offset || s >= end)
                continue;
            if (inline is FieldInline && stretch.Find(inline.Xml) is { } begin && In(begin, parts) && FieldRuns(begin, PartOf(begin, parts)) is { } field)
            {
                runs.AddRange(field);
                continue;
            }
            var text = inline is TextInline { Xml.Name.LocalName: "t" } t ? t : null;
            var element = text != null ? TextOf(stretch, inline, parts) : inline is RubyInline or MathInline ? null : stretch.Find(inline.Xml);
            if (element == null || !In(element, parts) || element.Parent is not { Name.LocalName: "r" })
                throw new NotSupportedException($"The range holds what cannot be formatted so ({inline.GetType().Name}).");
            if (text != null)
            {
                // The part of the text within the range in a w:t of its own.
                var (from, to) = (Math.Max(offset, s) - s, Math.Min(end, e) - s);
                if (to < text.Text.Length)
                    Cut(element, to);
                if (from > 0)
                    element = Cut(element, from);
            }
            // In a run of its own.
            SplitBefore(element);
            if (element.ElementsAfterSelf().FirstOrDefault() is { } next)
                SplitBefore(next);
            runs.Add(element.Parent!);
        }
        foreach (var run in runs.Distinct())
            Format(run, format, tracking);
        return stretch.Written(parts.Where(p => runs.Any(r => r.Ancestors().Contains(p))).Select(p => (p, (IEnumerable<XElement>)[p])));
    }

    /// <summary>A w:t cut in two at a place in its text (as the reader reads it); returns the second half, after it in its run.</summary>
    static XElement Cut(XElement t, int at)
    {
        var value = ReadText(t);
        var rest = new XElement(t);
        SetText(t, value[..at]);
        SetText(rest, value[at..]);
        t.AddAfterSelf(rest);
        return rest;
    }

    /// <summary>A run split before one of its children: that child and what follows it go into a run of their own after it, with the same properties.</summary>
    static void SplitBefore(XElement child)
    {
        var run = child.Parent!;
        if (!child.ElementsBeforeSelf().Any(e => e.Name.LocalName != "rPr"))
            return;
        var w = run.Name.Namespace;
        var second = new XElement(run.Name, run.Attributes(), run.Element(w + "rPr") is { } properties ? new XElement(properties) : null);
        var moved = child.NodesAfterSelf().Prepend(child).ToList();
        foreach (var node in moved)
            node.Remove();
        second.Add(moved);
        run.AddAfterSelf(second);
    }

    /// <summary>
    /// The runs of a field the layout computes: a w:fldSimple's, or a complex field's from its begin to its end, which
    /// stand in its paragraph; null where it does not end there.
    /// </summary>
    static List<XElement>? FieldRuns(XElement begin, XElement p)
    {
        var w = begin.Name.Namespace;
        if (begin.Name.LocalName == "fldSimple")
            return [.. begin.Descendants(w + "r").Where(r => ParagraphOf(r) == p)];
        if (begin.Parent is not { Name.LocalName: "r" } first)
            return null;
        var runs = new List<XElement>();
        var depth = 0;
        foreach (var run in p.Descendants(w + "r").Where(r => ParagraphOf(r) == p).SkipWhile(r => r != first))
        {
            runs.Add(run);
            foreach (var c in run.Elements(w + "fldChar"))
                depth += (string?)c.Attribute(w + "fldCharType") switch { "begin" => 1, "end" => -1, _ => 0 };
            if (depth == 0)
                return runs;
        }
        return null;
    }

    /// <summary>
    /// Character formatting set on a run's properties, as Word's buttons set it (see TextFormatting); as a tracked change
    /// the properties it had kept in a w:rPrChange (the first change's, where it has one).
    /// </summary>
    static void Format(XElement run, TextFormatting format, Tracking? tracking)
    {
        var w = run.Name.Namespace;
        var properties = Properties(run, "rPr");
        if (tracking != null && properties.Element(w + "rPrChange") == null)
        {
            var change = tracking.Mark(w, "rPrChange");
            change.Add(new XElement(w + "rPr", properties.Elements().Select(e => new XElement(e))));
            properties.Add(change);
        }
        void Set(string name, string? value, params string[] taken)
        {
            var e = Property(properties, name, RunOrder);
            e.SetAttributeValue(w + "val", value);
            foreach (var attribute in taken)
                e.Attribute(w + attribute)?.Remove();
        }
        if (format.Bold is { } bold)
        {
            Set("b", bold ? null : "0");
            Set("bCs", bold ? null : "0");
        }
        if (format.Italic is { } italic)
        {
            Set("i", italic ? null : "0");
            Set("iCs", italic ? null : "0");
        }
        if (format.Underline is { } underline)
            Set("u", underline);
        if (format.Size is { } size)
        {
            var half = ((int)Math.Round(size * 2)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            Set("sz", half);
            Set("szCs", half);
        }
        if (format.Font is { } font)
        {
            var fonts = Property(properties, "rFonts", RunOrder);
            foreach (var slot in new[] { "ascii", "hAnsi", "eastAsia", "cs" })
                fonts.SetAttributeValue(w + slot, font);
            foreach (var theme in new[] { "asciiTheme", "hAnsiTheme", "eastAsiaTheme", "cstheme" })
                fonts.Attribute(w + theme)?.Remove();
        }
        if (format.Color is { } color)
            Set("color", color, "themeColor", "themeTint", "themeShade");
        if (format.Highlight is { } highlight)
            Set("highlight", highlight);
    }

    /// <summary>
    /// Paragraph formatting given to a paragraph (see ParagraphFormatting): in a Strict document's names (start and end
    /// for left and right), and in the names its indents have. Indents and spacing in points stand where indents in
    /// characters, spacing in lines or automatic spacing (of HTML) would stand over them: those, where the paragraph has
    /// them (of its own, of its style or its list, 0 too, which keeps a style's away), are set to 0, as Word sets them.
    /// A paragraph the reader joined from several gets it in all its parts (measured in Word 365 by COM: centering with
    /// the cursor in the first of two paragraphs joined by a deleted mark centers both). Returns the stretch's new text.
    /// </summary>
    public static string Format(Stretch stretch, ParagraphBlock paragraph, ParagraphFormatting format, Tracking? tracking = null)
    {
        var parts = Parts(stretch, paragraph);
        Whole(stretch, paragraph, parts);
        foreach (var p in parts)
            Format(p, paragraph, format, tracking);
        return stretch.Written(parts.Select(p => (p, (IEnumerable<XElement>)[p])));
    }

    static void Format(XElement p, ParagraphBlock paragraph, ParagraphFormatting format, Tracking? tracking)
    {
        var w = p.Name.Namespace;
        var strict = w != Wml.W;
        var properties = Properties(p, "pPr");
        // As a tracked change the properties it had (but for its mark's and its section) are kept in a w:pPrChange.
        if (tracking != null && properties.Element(w + "pPrChange") == null)
        {
            var change = tracking.Mark(w, "pPrChange");
            change.Add(new XElement(w + "pPr", properties.Elements().Where(e => e.Name.LocalName is not ("rPr" or "sectPr")).Select(e => new XElement(e))));
            properties.Add(change);
        }
        if (format.Style is { } style)
            Property(properties, "pStyle", ParagraphOrder).SetAttributeValue(w + "val", style);
        if (format.Alignment is { } alignment)
            Property(properties, "jc", ParagraphOrder).SetAttributeValue(w + "val", strict ? alignment switch { "left" => "start", "right" => "end", _ => alignment } : alignment);
        if (format.Left != null || format.Right != null || format.FirstLine != null)
        {
            var ind = Property(properties, "ind", ParagraphOrder);
            var (left, right) = strict || ind.Attribute(w + "start") != null || ind.Attribute(w + "end") != null ? ("start", "end") : ("left", "right");
            var had = paragraph.Format;
            if (format.Left is { } l)
            {
                ind.SetAttributeValue(w + left, Twips(l));
                ind.SetAttributeValue(w + (left + "Chars"), had.LeftChars != null ? "0" : null);
            }
            if (format.Right is { } r)
            {
                ind.SetAttributeValue(w + right, Twips(r));
                ind.SetAttributeValue(w + (right + "Chars"), had.RightChars != null ? "0" : null);
            }
            // A first line in characters stands over one in twips, and a hanging one makes the left one count in
            // characters too (see ParagraphFormat.ApplyCharacterUnits): with a left indent given, the first line keeps
            // where it stood, in twips.
            if ((format.FirstLine ?? (format.Left != null && had.FirstLineChars is not (null or 0) ? had.FirstLine ?? 0 : null)) is { } first)
            {
                ind.SetAttributeValue(w + (first >= 0 ? "firstLine" : "hanging"), Twips(Math.Abs(first)));
                ind.Attribute(w + (first >= 0 ? "hanging" : "firstLine"))?.Remove();
                ind.Attribute(w + "hangingChars")?.Remove();
                ind.SetAttributeValue(w + "firstLineChars", had.FirstLineChars != null ? "0" : null);
            }
        }
        if (format.Before != null || format.After != null || format.Lines != null)
        {
            var spacing = Property(properties, "spacing", ParagraphOrder);
            var had = paragraph.Format;
            if (format.Before is { } before)
            {
                spacing.SetAttributeValue(w + "before", Twips(before));
                spacing.SetAttributeValue(w + "beforeLines", had.BeforeLines != null ? "0" : null);
                spacing.SetAttributeValue(w + "beforeAutospacing", had.BeforeAuto != null ? "0" : null);
            }
            if (format.After is { } after)
            {
                spacing.SetAttributeValue(w + "after", Twips(after));
                spacing.SetAttributeValue(w + "afterLines", had.AfterLines != null ? "0" : null);
                spacing.SetAttributeValue(w + "afterAutospacing", had.AfterAuto != null ? "0" : null);
            }
            if (format.Lines is { } lines)
            {
                spacing.SetAttributeValue(w + "line", ((int)Math.Round(lines * 240)).ToString(System.Globalization.CultureInfo.InvariantCulture));
                spacing.SetAttributeValue(w + "lineRule", "auto");
            }
        }
    }

    /// <summary>
    /// Replace as a tracked change (see Tracking), as Word makes it with Track Changes on: the text of the range deleted
    /// (each run of it in a w:del, its w:t as w:delText; what the author inserted goes away outright), the new text
    /// inserted after it in a w:ins, in the format of the text before it, or into the author's insertion it continues.
    /// </summary>
    static string ReplaceTracked(Stretch stretch, ParagraphBlock paragraph, int offset, int length, string text, Tracking tracking)
    {
        var parts = Parts(stretch, paragraph);
        var end = offset + length;
        var deleted = new List<XElement>();
        // The element the new text follows (the last of the range, else the text before the place), the one it goes
        // before, and the author's inserted text it goes into.
        XElement? before = null, after = null;
        (XElement T, int Index)? continued = null;
        var at = paragraph.TextOffset;
        foreach (var inline in paragraph.Content)
        {
            var (s, e) = (at, at + ParagraphText.Length(inline));
            at = e;
            if (e == s)
                continue;
            var t = TextOf(stretch, inline, parts);
            var within = Math.Max(s, offset) < Math.Min(e, end);
            if (t == null && Foreign(stretch, inline, parts))
            {
                if (within || text.Length > 0 && s < offset && offset <= e)
                    throw new NotSupportedException("The range reaches into content shown from elsewhere (a placeholder).");
                continue;
            }
            if (t != null)
            {
                var length0 = ((TextInline)inline).Text.Length;
                if (within)
                {
                    // The part within the range in a run of its own.
                    var (from, to) = (Math.Max(offset, s) - s, Math.Min(end, e) - s);
                    if (to < length0)
                        Cut(t, to);
                    if (from > 0)
                        t = Cut(t, from);
                    SplitBefore(t);
                    if (t.ElementsAfterSelf().FirstOrDefault() is { } rest)
                        SplitBefore(rest);
                    deleted.Add(t.Parent!);
                    before = t;
                }
                else if (length == 0 && s <= offset && offset <= e)
                {
                    // At the place: into the author's insertion, else after the text before it (cut there).
                    if (t.Ancestors().FirstOrDefault(IsRevision) is { Name.LocalName: "ins" } ins && tracking.Own(ins))
                        continued ??= (t, offset - s);
                    else if (offset == s)
                        after ??= t;
                    else
                    {
                        if (offset < e)
                            Cut(t, offset - s);
                        before = t;
                    }
                }
                else if (e <= offset)
                    before = t;
                else if (after == null && s >= end)
                    after = t;
            }
            else if (within)
            {
                if (!Removable(inline) || stretch.Find(inline.Xml) is not { } element || !In(element, parts) || element.Parent is not { Name.LocalName: "r" })
                    throw new NotSupportedException($"The range holds what cannot be taken away so ({inline.GetType().Name}).");
                SplitBefore(element);
                if (element.ElementsAfterSelf().FirstOrDefault() is { } rest)
                    SplitBefore(rest);
                deleted.Add(element.Parent!);
                before = element;
            }
            else if (s >= end && after == null)
                after = stretch.Find(inline.Xml);
        }
        // The new text first, while what it follows stands where it did; then the range deleted.
        var touched = new HashSet<XElement>(deleted.Select(run => PartOf(run, parts)));
        if (text.Length > 0)
        {
            if (continued is { } into)
            {
                var value = ReadText(into.T);
                touched.Add(PartOf(into.T, parts));
                SetContent(into.T, value[..into.Index] + text + value[into.Index..]);
            }
            else
                touched.Add(Inserted(parts, before, after, text, tracking));
        }
        foreach (var run in deleted)
            Deleted(run, tracking);
        return stretch.Written(parts.Where(touched.Contains).Select(p => (p, (IEnumerable<XElement>)[p])));
    }

    /// <summary>Whether an element marks a tracked change around the runs in it.</summary>
    static bool IsRevision(XElement element) => element.Name.LocalName is "ins" or "del" or "moveFrom" or "moveTo";

    /// <summary>
    /// New text in a run of its own in a w:ins: after the run of the element before it, else before the run of the
    /// element after it, else at the end of the paragraph's last part; out of the changes around that run (split where
    /// the run does not end or begin them); in the format of the run it follows or precedes, else of the part's mark.
    /// Returns the part it went into.
    /// </summary>
    static XElement Inserted(List<XElement> parts, XElement? before, XElement? after, string text, Tracking tracking)
    {
        var p = before != null ? PartOf(before, parts) : after != null ? PartOf(after, parts) : parts[^1];
        var w = p.Name.Namespace;
        var beside = (before ?? after)?.AncestorsAndSelf(w + "r").FirstOrDefault();
        var properties = beside?.Element(w + "rPr") ?? p.Element(w + "pPr")?.Element(w + "rPr");
        var t = new XElement(w + "t");
        var ins = tracking.Mark(w, "ins");
        ins.Add(new XElement(w + "r", properties != null ? new XElement(w + "rPr", properties.Elements().Where(e => !IsRevision(e) && e.Name.LocalName != "rPrChange")) : null, t));
        SetContent(t, text);
        if (before?.AncestorsAndSelf(w + "r").FirstOrDefault() is { } previous)
        {
            if (before.ElementsAfterSelf().FirstOrDefault() is { } rest)
                SplitBefore(rest);
            Out(previous, tracking, after: true).AddAfterSelf(ins);
        }
        else if (after?.AncestorsAndSelf(w + "r").FirstOrDefault() is { } next)
        {
            if (after.ElementsBeforeSelf().Any(e => e.Name.LocalName != "rPr"))
                SplitBefore(after);
            Out(after.Parent!, tracking, after: false).AddBeforeSelf(ins);
        }
        else
            p.Add(ins);
        return p;
    }

    /// <summary>
    /// A run taken out of the changes around it (w:ins, w:del) to put something beside it: each change split after it
    /// (or before it), the part that goes on in a copy of the change with an id of its own; the outermost change around
    /// it, or the run.
    /// </summary>
    static XElement Out(XElement run, Tracking tracking, bool after)
    {
        var place = run;
        while (place.Parent is { } change && IsRevision(change))
        {
            var others = (after ? place.NodesAfterSelf() : place.NodesBeforeSelf()).ToList();
            if (others.Any(n => n is XElement))
            {
                var part = tracking.Copy(change);
                foreach (var node in others)
                    node.Remove();
                part.Add(others);
                if (after)
                    change.AddAfterSelf(part);
                else
                    change.AddBeforeSelf(part);
            }
            place = change;
        }
        return place;
    }

    /// <summary>
    /// A run deleted as a tracked change: in the author's own insertion it goes away (and the insertion with it where
    /// nothing is left); else into a w:del of its own, its w:t as w:delText and w:instrText as w:delInstrText.
    /// </summary>
    static void Deleted(XElement run, Tracking tracking)
    {
        var w = run.Name.Namespace;
        if (run.Ancestors().FirstOrDefault(IsRevision) is { } change)
        {
            if (change.Name.LocalName is "del" or "moveFrom")
                return;
            if (change.Name.LocalName == "ins" && tracking.Own(change))
            {
                run.Remove();
                if (!change.Elements().Any())
                    change.Remove();
                return;
            }
        }
        foreach (var t in run.Elements(w + "t").ToList())
            t.Name = w + "delText";
        foreach (var instruction in run.Elements(w + "instrText").ToList())
            instruction.Name = w + "delInstrText";
        var del = tracking.Mark(w, "del");
        run.ReplaceWith(del);
        del.Add(run);
    }

    /// <summary>
    /// A paragraph's mark marked as a tracked change of a kind (ins, del) in its run properties, where they begin
    /// (CT_ParaRPr); an insertion takes the place of the changes the mark had (it is a new mark).
    /// </summary>
    static void Marked(XElement p, string kind, Tracking tracking)
    {
        var w = p.Name.Namespace;
        var mark = Property(Properties(p, "pPr"), "rPr", ParagraphOrder);
        if (kind == "ins")
        {
            foreach (var change in mark.Elements().Where(IsRevision).ToList())
                change.Remove();
            mark.AddFirst(tracking.Mark(w, kind));
        }
        else if (mark.Elements().LastOrDefault(IsRevision) is { } last)
            last.AddAfterSelf(tracking.Mark(w, kind));
        else
            mark.AddFirst(tracking.Mark(w, kind));
    }

    /// <summary>Whether a paragraph's mark is the author's own insertion (a tracked change).</summary>
    static bool OwnMark(XElement p, Tracking tracking) =>
        p.Element(p.Name.Namespace + "pPr")?.Element(p.Name.Namespace + "rPr")?.Elements(p.Name.Namespace + "ins").Any(tracking.Own) == true;

    /// <summary>Whether an element between paragraphs is a mark, which goes into a paragraph (a bookmark, a comment's range, a proofing mark).</summary>
    static bool IsMark(XElement element) =>
        element.Name.LocalName is "bookmarkStart" or "bookmarkEnd" or "commentRangeStart" or "commentRangeEnd" or "permStart" or "permEnd" or "proofErr"
        || element.Name.LocalName.EndsWith("RangeStart", StringComparison.Ordinal) || element.Name.LocalName.EndsWith("RangeEnd", StringComparison.Ordinal);

    /// <summary>
    /// Checks that a paragraph's content was read from its parts (see Parts), bookmarks and other marks aside, which the
    /// reader moves into the paragraph after them.
    /// </summary>
    static void Whole(Stretch stretch, ParagraphBlock paragraph, List<XElement> parts)
    {
        if (paragraph.Content.Any(inline => ParagraphText.Length(inline) > 0 && Foreign(stretch, inline, parts)))
            throw new NotSupportedException("The paragraph holds content shown from elsewhere (a placeholder).");
    }

    /// <summary>
    /// Whether an inline of a paragraph was not read from its parts: a placeholder another content control or the
    /// glossary shows; what the reader made of nothing (Xml null) aside.
    /// </summary>
    static bool Foreign(Stretch stretch, Inline inline, List<XElement> parts) =>
        inline.Xml != null && (stretch.Find(inline.Xml) is not { } element || !In(element, parts));
}
