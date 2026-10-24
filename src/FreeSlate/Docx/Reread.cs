using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace FreeSlate.Docx;

public sealed partial class ModelReader
{
    /// <summary>
    /// Where reading the body stood before each of its children: how many number steps the body had asked (see
    /// NumberSteps), and whether reading could begin afresh there, nothing waiting from the children before it: a paragraph
    /// whose mark is deleted, an open field, a break or a bookmark between paragraphs, which go into the next paragraph.
    /// </summary>
    readonly Dictionary<XElement, (int Steps, bool Fresh)> bodyPoints = new(ReferenceEqualityComparer.Instance);

    /// <summary>While re-reading (see Reread): the steps asked, the answers given them, and the points of the children read.</summary>
    List<(NumberStep Step, object? Answer)>? rereadSteps;
    Queue<object?>? rereadAnswers;
    readonly Dictionary<XElement, (int Steps, bool Fresh)> rereadPoints = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// The element before each child of the body (null for the first), which XLinq finds only by walking the body from
    /// its first child (see Siblings).
    /// </summary>
    readonly Dictionary<XElement, XElement?> childBefore = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// The body's blocks as reading left them last (Body, Reread), the child of the body each was read from (null for one
    /// the reader made of others), and how many blocks each child stands for.
    /// </summary>
    List<Block>? readBlocks;
    readonly List<XElement?> readTops = [];
    readonly Dictionary<XElement, int> blockCounts = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// The body's blocks after some of its children changed, read anew where they changed: the children
    /// <paramref name="old"/> of the body (one after the other, white space aside) are replaced in the XML loaded for the
    /// document by <paramref name="children"/>, prepared as the document's XML is when it is loaded
    /// (DocumentContext.Prepare); where there are no old ones, the new go before <paramref name="before"/>. They are read
    /// from the last child before them where reading could begin afresh up to the first after them where it goes on as it
    /// did, the children between read anew with them (what a field over paragraphs, a deleted mark or a bookmark between
    /// paragraphs joins); their number steps are replayed with the body's others (see NumberStep), and the children after
    /// them whose numbers change are read anew too. The blocks read anew keep the ids of the blocks they stand for (see
    /// Keep), or of <paramref name="like"/> where given (another version of the body, the same but for the change); new
    /// ones take ids of their own. Null, the XML left as it was, where reading cannot so begin or go on (a paragraph whose
    /// mark is hidden, a field to the body's end, children the reader made of others), where note references come, go or
    /// change their order (the notes' marks are read with the notes), where sections would begin or end elsewhere, where a
    /// content control shows its placeholder (which later ones show too) or in the markup view: the document has to be
    /// read again.
    /// </summary>
    public List<Block>? Reread(List<Block> body, IReadOnlyList<XElement> old, XElement? before, IReadOnlyList<XElement> children, IReadOnlyList<Block>? like = null)
    {
        if (context.Markup || bodyElement == null || old.Count == 0 && (before == null || children.Count == 0)
            || old.Append(before).OfType<XElement>().Any(e => e.Parent != bodyElement || !bodyPoints.ContainsKey(e)))
            return null;
        // The old children one after the other, with nothing but white space between them.
        for (var k = 1; k < old.Count; k++)
        {
            if (After(old[k - 1]) != old[k])
                return null;
        }
        // Sections end where they did: only the last old child and the last new one may end one, both or neither, with
        // the same properties, the mark that holds the break deleted in both or in neither.
        var (oldEnd, newEnd) = (old.Count > 0 ? SectionEnd(old[^1]) : null, children.Count > 0 ? SectionEnd(children[^1]) : null);
        if (old.SkipLast(1).Any(e => SectionEnd(e) != null) || children.SkipLast(1).Any(c => SectionEnd(c) != null) || old.Concat(children).Any(BreaksWithin)
            || (oldEnd == null) != (newEnd == null) || oldEnd != null && (!XNode.DeepEquals(oldEnd, newEnd) || MarkDeleted(old[^1]) != MarkDeleted(children[^1])))
            return null;
        // Whether a field reaches beyond its paragraphs is worked out from the field characters of the whole body (see
        // Unclosed): where the change's are not those it had, children before it read otherwise.
        if (!old.SelectMany(FieldCharacters).SequenceEqual(children.SelectMany(FieldCharacters)))
            return null;
        // What a content control showing its placeholder shows, the later ones show too (see Shown); what a document of
        // another kind or a subdocument brings in (w:altChunk, w:subDoc) is not read again alone.
        if (old.Concat(children).Any(Elsewhere))
            return null;
        // Reading begins at the last child up to the first changed one where it could begin afresh.
        var first = old.Count > 0 ? old[0] : before!;
        var leading = new List<XElement>();
        for (var e = first; !bodyPoints[e].Fresh;)
        {
            if (ChildBefore(e) is not { } previous || !bodyPoints.ContainsKey(previous))
                return null;
            leading.Insert(0, e = previous);
        }
        // The blocks read from the children (each block from the child of the body it stands in). A paragraph that stands
        // for no block of its own and whose mark is not deleted went into the block of the one before it (a hidden mark
        // takes in the next paragraph, see JoinHidden).
        var (tops, counts) = Tops(body);
        bool Absorbed(XElement e) => e.Name == Wml.W + "p" && !counts.ContainsKey(e) && e.Child("pPr").Child("rPr") is var mark
            && mark.Child("del") == null && mark.Child("moveFrom") == null;
        // It goes on as it did at the first child after them where it could begin afresh, the children up to it read
        // anew (those the last old one took in too); further on where the new children do not end afresh.
        var trailing = new List<XElement>();
        var next = old.Count > 0 ? After(old[^1]) : before;
        void Further()
        {
            while (next != null && (!(bodyPoints.TryGetValue(next, out var point) && point.Fresh) || Absorbed(next)))
            {
                trailing.Add(next);
                next = After(next);
            }
        }
        Further();
        // Where none of those children has blocks, the new ones go before the blocks of the children after them.
        var place = body.Count;
        for (var e = first; e != null; e = After(e))
        {
            if (counts.ContainsKey(e))
            {
                place = Find(tops, e);
                break;
            }
        }
        (int From, int To)? Range()
        {
            var read = leading.Concat(old).Concat(trailing).ToHashSet<XElement>(ReferenceEqualityComparer.Instance);
            // The blocks read from them stand together: from the first, as many as they stand for, all of them.
            var count = read.Sum(e => counts.GetValueOrDefault(e));
            var (from, to) = (place, place);
            if (count > 0)
            {
                from = read.Where(counts.ContainsKey).Min(e => Find(tops, e));
                to = from + count;
            }
            // The block before them took nothing of them (what a paragraph read took in, the one it went into is read
            // too), nor does it stay as it is only before what they were (a hidden mark's before a table).
            read.Add(first);
            return trailing.All(bodyPoints.ContainsKey) && !leading.Concat(trailing).Any(Elsewhere) && to <= tops.Count
                && Enumerable.Range(from, to - from).All(k => tops[k] is { } top && read.Contains(top))
                && read.Where(Absorbed).All(e => ChildBefore(e) is { } taker && read.Contains(taker))
                && !(from > 0 && body[from - 1] is ParagraphBlock previous
                    && (previous.MarkFormat.Vanish == true || previous.Content.Any(i => i.Xml is { } xml && Top(xml) is { } top && read.Contains(top))))
                ? (from, to) : null;
        }
        if (Range() is not { } range)
            return null;
        var steps = numberSteps.GetValueOrDefault(context.MainPart) ?? [];
        var start = bodyPoints[leading.Count > 0 ? leading[0] : first].Steps;
        // The new children in place of the old ones, in their section.
        var section = sectionOf.GetValueOrDefault(first);
        foreach (var child in children)
            sectionOf[child] = section;
        var putBack = Put(first, old, children);
        // Which fields of the body end in it (see Unclosed): the change's field characters are those it had (see above),
        // each new begin in place of the old one. How the body's drawings stack is worked out anew where the change holds
        // drawings (see StackingOrder).
        if (unclosed.TryGetValue(bodyElement, out var open))
        {
            foreach (var (was, now) in old.SelectMany(e => e.Descendants(Wml.W + "fldChar")).Zip(children.SelectMany(e => e.Descendants(Wml.W + "fldChar"))))
            {
                if (open.Remove(was))
                    open.Add(now);
            }
        }
        var stacked = old.Concat(children).Any(e => e.Descendants().Any(d => d.Name == Drawings.Wp + "anchor" || d.Name.Namespace == Vml.V));
        if (stacked)
            context.Changed(bodyElement.Document!);
        // Back as it was, where reading the change does not come out so.
        List<Block>? Undone()
        {
            putBack();
            unclosed.Remove(bodyElement);
            if (stacked)
                context.Changed(bodyElement.Document!);
            return null;
        }
        // Their steps first, asked of throwaway counters; then the body's steps with them replayed, and the children read
        // with their answers.
        var probe = Read([.. leading, .. children, .. trailing], null);
        for (var further = 0; probe == null && next != null && further < 4; further++)
        {
            trailing.Add(next);
            next = After(next);
            Further();
            if (Range() is not { } wider)
                return Undone();
            range = wider;
            probe = Read([.. leading, .. children, .. trailing], null);
        }
        var end = next != null ? bodyPoints[next].Steps : steps.Count;
        // Note references count where they stand, their marks read with the notes: the change keeps those it had, in
        // their order, or the notes read otherwise.
        if (probe is not { } asked || !asked.Steps.Select(s => s.Step).OfType<NoteStep>().SequenceEqual(steps.Skip(start).Take(end - start).Select(s => s.Step).OfType<NoteStep>()))
            return Undone();
        // Where they ask the steps the old ones asked (a paragraph of no list asks none), the answers are those they had:
        // no steps are replayed, and no blocks after them read anew.
        var same = asked.Steps.Count == end - start && asked.Steps.Select(s => s.Step).SequenceEqual(steps.Skip(start).Take(end - start).Select(s => s.Step));
        List<(NumberStep Step, object? Answer)> all = same ? steps : [.. steps.Take(start), .. asked.Steps, .. steps.Skip(end)];
        var answers = same ? null : Numbers.Replay(all.Select(s => s.Step), context.Numbering, new NoteNumbers(context.Settings));
        if (Read([.. leading, .. children, .. trailing], answers?.Skip(start).Take(asked.Steps.Count) ?? steps.Skip(start).Take(end - start).Select(s => s.Answer)) is not { } got)
            return Undone();
        var (from, to) = range;
        Keep(like != null ? [.. like.Skip(from).Take(like.Count - (body.Count - (to - from)))] : body.GetRange(from, to - from), got.Blocks);
        var result = new List<Block>(body.Count + got.Blocks.Count - (to - from));
        result.AddRange(CollectionsMarshal.AsSpan(body)[..from]);
        result.AddRange(got.Blocks);
        // The children after them whose numbers changed, read anew with their answers, each with the blocks read from it.
        var shift = got.Steps.Count - (end - start);
        // The steps after them whose answers changed, by their places before the change, in order.
        var differing = new List<int>();
        for (var k = end; answers != null && k < steps.Count; k++)
        {
            if (!Equals(Answer(steps[k].Answer), Answer(answers[k + shift])))
                differing.Add(k);
        }
        bool Changed(int at, int until) => differing.BinarySearch(at) is var k && (k < 0 ? ~k : k) is var first && first < differing.Count && differing[first] < until;
        for (var i = to; i < body.Count;)
        {
            if (answers == null)
            {
                result.AddRange(CollectionsMarshal.AsSpan(body)[i..]);
                break;
            }
            var top = tops[i];
            var j = i;
            while (j < body.Count && tops[j] == top)
                j++;
            var until = j < body.Count && tops[j] is { } after && bodyPoints.TryGetValue(after, out var following) ? following.Steps : steps.Count;
            if (top == null || !bodyPoints.TryGetValue(top, out var point) || until < point.Steps)
            {
                // What the reader made of several children (a field over paragraphs joined) and what stands elsewhere (a
                // placeholder shown from an earlier control) is not read alone: the numbers after it stay as they were.
                if (Changed(Math.Min(end, steps.Count), steps.Count))
                    return Undone();
                result.AddRange(CollectionsMarshal.AsSpan(body)[i..]);
                break;
            }
            if (!Changed(point.Steps, until))
            {
                result.AddRange(CollectionsMarshal.AsSpan(body)[i..j]);
                i = j;
                continue;
            }
            // Read anew from where reading could begin afresh before it, with the marks between paragraphs it takes.
            var marks = new List<XElement>();
            for (var e = top; !bodyPoints[e].Fresh;)
            {
                if (ChildBefore(e) is not { } previous || !bodyPoints.ContainsKey(previous) || counts.ContainsKey(previous))
                    return Undone();
                marks.Insert(0, e = previous);
            }
            if (Read([.. marks, top], answers.Skip(point.Steps + shift).Take(until - point.Steps)) is not { } again || again.Blocks.Count != j - i)
                return Undone();
            Keep(body.GetRange(i, j - i), again.Blocks);
            result.AddRange(again.Blocks);
            i = j;
        }
        // What reading leaves for the next change: the body's steps and answers, the points of its children (those after
        // the change moved on by the steps it added), and the children its blocks were read from (blocks read anew after
        // the change were read from the same).
        if (answers != null)
            numberSteps[context.MainPart] = [.. all.Select((s, k) => (s.Step, answers[k]))];
        foreach (var e in old.Concat(leading).Concat(trailing))
            bodyPoints.Remove(e);
        for (var e = shift != 0 ? next : null; e != null; e = After(e))
        {
            if (bodyPoints.TryGetValue(e, out var point))
                bodyPoints[e] = (point.Steps + shift, point.Fresh);
        }
        foreach (var (e, point) in got.Points)
            bodyPoints[e] = (point.Steps + start, point.Fresh);
        for (var k = from; k < to; k++)
        {
            if (tops[k] is { } top && --counts[top] == 0)
                counts.Remove(top);
        }
        var readFrom = got.Blocks.Select(b => Top(Of(b))).ToList();
        foreach (var top in readFrom.OfType<XElement>())
            counts[top] = counts.GetValueOrDefault(top) + 1;
        tops.RemoveRange(from, to - from);
        tops.InsertRange(from, readFrom);
        readBlocks = result;
        return result;
    }

    /// <summary>
    /// Puts children of the body in place of old ones (where there are none, before <paramref name="first"/>): after the
    /// node before the first, the old ones taken out each with the node before it known (see Siblings), the element
    /// before each child kept (see childBefore). Returns what puts the old ones back.
    /// </summary>
    Action Put(XElement first, IReadOnlyList<XElement> old, IReadOnlyList<XElement> children)
    {
        var body = bodyElement!;
        var previous = ChildBefore(first);
        var anchor = Siblings.NodeBefore(body, previous, first);
        var after = old.Count > 0 ? After(old[^1]) : first;
        Add(anchor, children);
        Take(children.Count > 0 ? children[^1] : anchor, old);
        Link(previous, old, children, after);
        return () =>
        {
            Take(anchor, children);
            Add(anchor, old);
            Link(previous, children, old, after);
        };

        void Add(XNode? at, IReadOnlyList<XElement> elements)
        {
            if (elements.Count == 0)
                return;
            if (at != null)
                at.AddAfterSelf(elements);
            else
                body.AddFirst(elements);
        }

        // Takes elements out one after the other, the first after the node given (the body's first where null).
        void Take(XNode? at, IReadOnlyList<XElement> elements)
        {
            foreach (var e in elements)
            {
                at = Siblings.NodeBefore(body, at, e);
                Siblings.Remove(e, at);
            }
        }

        void Link(XElement? before, IReadOnlyList<XElement> gone, IReadOnlyList<XElement> elements, XElement? next)
        {
            foreach (var e in gone)
                childBefore.Remove(e);
            foreach (var e in elements)
            {
                childBefore[e] = before;
                before = e;
            }
            if (next != null)
                childBefore[next] = before;
        }
    }

    /// <summary>The element before a child of the body (see childBefore), white space and comments aside.</summary>
    XElement? ChildBefore(XElement child) => childBefore.TryGetValue(child, out var before) ? before : Before(child);

    /// <summary>
    /// The child of the body each of its blocks was read from and how many blocks each child stands for: as reading left
    /// them where the blocks are those it left (see readBlocks), else worked out (and kept for them).
    /// </summary>
    (List<XElement?> Tops, Dictionary<XElement, int> Counts) Tops(List<Block> body)
    {
        if (!ReferenceEquals(body, readBlocks) || readTops.Count != body.Count)
        {
            readTops.Clear();
            blockCounts.Clear();
            foreach (var block in body)
            {
                var top = Top(Of(block));
                readTops.Add(top);
                if (top != null)
                    blockCounts[top] = blockCounts.GetValueOrDefault(top) + 1;
            }
            readBlocks = body;
        }
        return (readTops, blockCounts);
    }

    /// <summary>The place of the first block read from a child of the body (see Tops), or -1.</summary>
    static int Find(List<XElement?> tops, XElement top)
    {
        var span = CollectionsMarshal.AsSpan(tops);
        for (var k = 0; k < span.Length; k++)
        {
            if (ReferenceEquals(span[k], top))
                return k;
        }
        return -1;
    }

    /// <summary>The child of the body an element stands in (itself, or one around it), or null for one outside it (made by the reader).</summary>
    XElement? Top(XElement element)
    {
        var top = element;
        while (top.Parent is { } parent && parent != bodyElement)
            top = parent;
        return top.Parent == bodyElement ? top : null;
    }

    /// <summary>The element before one among its siblings, white space and comments aside.</summary>
    static XElement? Before(XElement element)
    {
        for (var node = element.PreviousNode; node != null; node = node.PreviousNode)
        {
            if (node is XElement before)
                return before;
        }
        return null;
    }

    /// <summary>The element after one among its siblings, white space and comments aside.</summary>
    static XElement? After(XElement element)
    {
        for (var node = element.NextNode; node != null; node = node.NextNode)
        {
            if (node is XElement after)
                return after;
        }
        return null;
    }

    /// <summary>The properties of the section a child of the body ends, a paragraph's (w:sectPr in its w:pPr), or null.</summary>
    static XElement? SectionEnd(XElement child) => child.Name == Wml.W + "p" ? child.Child("pPr").Child("sectPr") : null;

    /// <summary>Whether a paragraph's mark is deleted (a tracked change): it runs into the next one, a section break it holds with it.</summary>
    static bool MarkDeleted(XElement p) => p.Child("pPr").Child("rPr") is { } mark && (mark.Child("del") ?? mark.Child("moveFrom")) != null;

    /// <summary>
    /// Whether a child of the body shows what stands elsewhere: a content control showing its placeholder (the first
    /// such control's content, see Shown), a document of another kind or a subdocument (w:altChunk, w:subDoc).
    /// </summary>
    static bool Elsewhere(XElement child) =>
        child.DescendantsAndSelf().Any(d => d.Name == Wml.W + "showingPlcHdr" || d.Name == Wml.W + "altChunk" || d.Name == Wml.W + "subDoc");

    /// <summary>The kinds of the field characters a child of the body holds (w:fldChar begin, separate, end), in order.</summary>
    static IEnumerable<string?> FieldCharacters(XElement child) => child.Descendants(Wml.W + "fldChar").Select(c => c.Attr("fldCharType"));

    /// <summary>Whether a child of the body other than a paragraph holds a paragraph that ends a section (in a content control).</summary>
    static bool BreaksWithin(XElement child) => child.Name != Wml.W + "p" && child.Descendants(Wml.W + "sectPr").Any(s => s.Parent?.Name.LocalName == "pPr");

    /// <summary>The element a block of the body was read from.</summary>
    static XElement Of(Block block) => block switch
    {
        ParagraphBlock paragraph => paragraph.Element,
        TableBlock table => table.Element,
        _ => throw new ArgumentOutOfRangeException(nameof(block)),
    };

    /// <summary>A step's answer as compared: a list number by its text and its own number.</summary>
    static object? Answer(object? answer) => answer is ValueTuple<string, NumberingLevel, string> label ? (label.Item1, label.Item3) : answer;

    /// <summary>
    /// Children of the body read with the state reading leaves before a child it can begin afresh at, with the given
    /// answers to their number steps (or throwaway counters asked them): their blocks, steps and points, or null where
    /// they end with something waiting for the next child.
    /// </summary>
    (List<Block> Blocks, List<(NumberStep Step, object? Answer)> Steps, Dictionary<XElement, (int Steps, bool Fresh)> Points)? Read(
        IReadOnlyList<XElement> elements, IEnumerable<object?>? answers)
    {
        var (savedCounter, savedNotes, savedPart, savedSection) = (counter, noteNumbers, currentPart, currentSection);
        if (answers == null)
            (counter, noteNumbers) = (new ListCounter(context.Numbering), new NoteNumbers(context.Settings));
        currentPart = context.MainPart;
        (rereadSteps, rereadAnswers) = ([], answers != null ? new Queue<object?>(answers) : null);
        rereadPoints.Clear();
        try
        {
            var blocks = new List<Block>();
            ParagraphBlock? pending = null;
            ReadBlocks(bodyElement!, elements, blocks, ref pending);
            if (pending != null || openFields.Count > 0 || strayBreaks.Count > 0 || blockBookmarks.Count > 0 || rereadAnswers is { Count: > 0 }
                || blocks.OfType<ParagraphBlock>().Any(p => p.MarkFormat.Vanish == true))
                return null;
            return (blocks, rereadSteps, new Dictionary<XElement, (int Steps, bool Fresh)>(rereadPoints, ReferenceEqualityComparer.Instance));
        }
        finally
        {
            (counter, noteNumbers, currentPart, currentSection) = (savedCounter, savedNotes, savedPart, savedSection);
            (rereadSteps, rereadAnswers) = (null, null);
            openFields.Clear();
            strayBreaks.Clear();
            blockBookmarks.Clear();
        }
    }

    /// <summary>
    /// Blocks read anew (<paramref name="read"/>, changed in place) keep the ids of the blocks they stand for (see Kept):
    /// the same blocks at the beginning and at the end (of the same kind and text), those between them by place.
    /// </summary>
    internal static void Keep(IReadOnlyList<Block> old, List<Block> read)
    {
        var head = 0;
        while (head < old.Count && head < read.Count && Same(old[head], read[head]))
            head++;
        var tail = 0;
        while (tail < old.Count - head && tail < read.Count - head && Same(old[old.Count - 1 - tail], read[read.Count - 1 - tail]))
            tail++;
        for (var i = 0; i < read.Count; i++)
        {
            var k = i >= read.Count - tail ? i + old.Count - read.Count : i < old.Count - tail ? i : -1;
            if (k >= 0)
                read[i] = Kept(old[k], read[i]);
        }
    }

    /// <summary>
    /// A block read anew with the ids of the block it stands for: of the same kind, it takes that block's id, and the
    /// blocks within it (a table's cells', a text box's) keep theirs list by list where it has as many lists; else it
    /// stays as read.
    /// </summary>
    static Block Kept(Block old, Block read)
    {
        if (old.GetType() != read.GetType())
            return read;
        var (was, now) = (Inner(old).ToList(), Inner(read).ToList());
        if (was.Count == now.Count)
        {
            for (var k = 0; k < now.Count; k++)
                Keep(was[k], now[k]);
        }
        return read.Id == old.Id ? read : read with { Id = old.Id };
    }

    /// <summary>Whether a block read anew reads as one read before: of the same kind, with the same text (a table: all its paragraphs').</summary>
    static bool Same(Block old, Block read) => old.GetType() == read.GetType() && Signature(old) == Signature(read);

    static string Signature(Block block) =>
        block is ParagraphBlock paragraph ? ParagraphText.Of(paragraph) : string.Join((char)1, Inner(block).SelectMany(list => list).Select(Signature));

    /// <summary>The lists of blocks within a block: a paragraph's text boxes', a table's cells'.</summary>
    internal static IEnumerable<List<Block>> Inner(Block block)
    {
        if (block is ParagraphBlock paragraph)
        {
            foreach (var inline in paragraph.Content)
            {
                var (text, children) = inline switch
                {
                    DrawingInline d => (d.Text, d.ChildText),
                    AnchorInline a => (a.Text, a.ChildText),
                    _ => (null, null),
                };
                if (text != null)
                    yield return text;
                foreach (var (_, inner) in children ?? [])
                    yield return inner;
            }
        }
        else if (block is TableBlock table)
        {
            foreach (var cell in table.Table.Rows.SelectMany(r => r.Cells))
                yield return cell.Blocks;
        }
    }
}
