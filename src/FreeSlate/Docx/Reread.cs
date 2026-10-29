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
    /// ones take ids of their own. Where note references come, go or change their order, the references after them take
    /// their new marks so, and the notes, whose marks are read with them, are to be read anew (see NotesMoved); where
    /// section breaks come, go or change, the children of the sections around them take their sections anew. Null, the
    /// XML left as it was, where reading cannot so begin or go on (a paragraph whose mark is hidden, a field to the body's
    /// end, children the reader made of others), where a deleted mark holds a section break that comes, goes or changes,
    /// or the sections the change makes number notes otherwise, where a content control shows its placeholder (which
    /// later ones show too), and in the markup view where comment ranges come, go or change their order (comments are
    /// numbered by them) or an author comes whom the body did not name before (authors take their colors in the body's
    /// order): the document has to be read again.
    /// </summary>
    public List<Block>? Reread(List<Block> body, IReadOnlyList<XElement> old, XElement? before, IReadOnlyList<XElement> children, IReadOnlyList<Block>? like = null)
    {
        if (bodyElement == null || old.Count == 0 && (before == null || children.Count == 0)
            || old.Append(before).OfType<XElement>().Any(e => e.Parent != bodyElement || !bodyPoints.ContainsKey(e)))
            return null;
        // In the markup view comments are numbered in the order of their ranges in the body (see CommentAnchored), and
        // authors take their colors in the order the body names them first (see DocumentContext.Author): the change keeps
        // the ranges it had, in their order, and names the authors it named, first in the same order.
        if (context.Markup && !old.SelectMany(CommentRanges).SequenceEqual(children.SelectMany(CommentRanges)))
            return null;
        // The old children one after the other, with nothing but white space between them.
        for (var k = 1; k < old.Count; k++)
        {
            if (After(old[k - 1]) != old[k])
                return null;
        }
        // Sections end where they did where only the last old child and the last new one end one, both or neither, with
        // the same properties, the mark that holds the break deleted in both or in neither. Else (a break put in, taken
        // away or given other properties) the children of the sections the change stands in take their sections anew
        // (below), where no mark that holds a break is deleted and the sections number their notes alike: the notes
        // before the change keep their marks.
        var (oldEnd, newEnd) = (old.Count > 0 ? SectionEnd(old[^1]) : null, children.Count > 0 ? SectionEnd(children[^1]) : null);
        if (old.Concat(children).Any(BreaksWithin))
            return null;
        var sectionsKept = !old.SkipLast(1).Any(e => SectionEnd(e) != null) && !children.SkipLast(1).Any(c => SectionEnd(c) != null) && (oldEnd == null) == (newEnd == null)
            && (oldEnd == null || XNode.DeepEquals(oldEnd, newEnd) && MarkDeleted(old[^1]) == MarkDeleted(children[^1]));
        if (!sectionsKept && (old.Concat(children).Any(e => SectionEnd(e) != null && MarkDeleted(e))
            || !NumberedAlike([.. old.Concat(children).Select(SectionEnd).OfType<XElement>(), sectionOf.GetValueOrDefault(old.Count > 0 ? old[0] : before!).Properties,
                sectionOf.GetValueOrDefault(old.Count > 0 ? old[^1] : before!).Properties])))
            return null;
        // Whether a field's begin has no end in the body is worked out from the field characters of the whole body (see
        // Unclosed), which pair as brackets do: fields the change puts in or takes away whole, begin and end among its
        // children, pair no other characters otherwise. Those left unpaired among the old children and among the new
        // ones are alike, or children before and after it read otherwise.
        var (wasEnds, wasBegins) = Unpaired(old);
        var (nowEnds, nowBegins) = Unpaired(children);
        if (wasEnds != nowEnds || wasBegins.Count != nowBegins.Count)
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
        // The new children in place of the old ones, in their section. Where sections end elsewhere now, the children of
        // the sections the change stands in take theirs anew, from the first child of the section it begins in to the one
        // that ends the section it ends in (to the body's end in its last section), with what they had kept to put back;
        // the children after them move on by the sections the change added (taken away where fewer).
        List<(XElement Child, (XElement? Properties, int Index)? Was)>? sectionsWere = null;
        var (sectionsAdded, sectionsBeyond, sectionFrom) = (0, (XElement?)null, (XElement?)null);
        if (sectionsKept)
        {
            var section = sectionOf.GetValueOrDefault(first);
            foreach (var child in children)
                sectionOf[child] = section;
        }
        else
        {
            var prefix = new List<XElement>();
            for (var e = ChildBefore(first); e != null && SectionEnd(e) == null; e = ChildBefore(e))
                prefix.Insert(0, e);
            var suffix = new List<XElement>();
            for (var e = old.Count > 0 ? After(old[^1]) : before; e != null; e = After(e))
            {
                suffix.Add(e);
                if (SectionEnd(e) != null)
                    break;
            }
            sectionsBeyond = suffix.Count > 0 ? After(suffix[^1]) : null;
            sectionFrom = prefix.Count > 0 ? prefix[0] : null;
            sectionsAdded = children.Count(c => SectionEnd(c) != null) - old.Count(e => SectionEnd(e) != null);
            var index = sectionOf.GetValueOrDefault(prefix.Count > 0 ? prefix[0] : first).Index;
            var were = sectionsWere = [];
            var pending = new List<XElement>();
            void Give(XElement? properties)
            {
                foreach (var e in pending)
                {
                    were.Add((e, sectionOf.TryGetValue(e, out var was) ? was : null));
                    sectionOf[e] = (properties, index);
                }
                pending.Clear();
            }
            foreach (var e in prefix.Concat(children).Concat(suffix))
            {
                pending.Add(e);
                if (SectionEnd(e) is { } properties)
                {
                    Give(properties);
                    index++;
                }
            }
            Give(bodyElement.Child("sectPr") ?? context.SectionAhead());
            Move(sectionsBeyond, sectionsAdded);
        }
        // The children from one on moved on by so many sections.
        void Move(XElement? from, int by)
        {
            for (var e = by != 0 ? from : null; e != null; e = After(e))
            {
                if (sectionOf.TryGetValue(e, out var s))
                    sectionOf[e] = (s.Properties, s.Index + by);
            }
        }
        // In the markup view authors take their colors in the order reading the body names them first: the new children
        // name the authors the old ones named, in the same order, or reading them would color otherwise than reading the
        // body whole (see AuthorsNamed).
        var named = context.Markup ? AuthorsNamed(old) : null;
        var putBack = Put(first, old, children);
        // Which fields of the body end in it (see Unclosed): the begins the change leaves unpaired are those it had (see
        // above), each new one in place of the old one. How the body's drawings stack is worked out anew where the change
        // holds drawings (see StackingOrder).
        if (unclosed.TryGetValue(bodyElement, out var open))
        {
            foreach (var (was, now) in wasBegins.Zip(nowBegins))
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
            if (sectionsWere != null)
            {
                foreach (var (child, was) in sectionsWere)
                {
                    if (was is { } section)
                        sectionOf[child] = section;
                    else
                        sectionOf.Remove(child);
                }
                Move(sectionsBeyond, -sectionsAdded);
            }
            else
            {
                foreach (var child in children)
                    sectionOf.Remove(child);
            }
            unclosed.Remove(bodyElement);
            if (stacked)
                context.Changed(bodyElement.Document!);
            return null;
        }
        if (named != null && !AuthorsNamed(children).SequenceEqual(named))
            return Undone();
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
        if (probe is not { } asked)
            return Undone();
        // Note references count where they stand, their marks read with the notes: where the change's are not those it
        // had, in their order, the notes read otherwise (see NotesMoved), the references after it numbered anew below.
        var notesKept = asked.Steps.Select(s => s.Step).OfType<NoteStep>().SequenceEqual(steps.Skip(start).Take(end - start).Select(s => s.Step).OfType<NoteStep>());
        // Where sections end elsewhere now, the note references of the section the change begins in before it stand in a
        // section of other properties (numbered alike, see above), those after it in sections moved on.
        var (sectionSteps, sectionNow) = sectionFrom != null && bodyPoints.TryGetValue(sectionFrom, out var sectionPoint)
            ? (sectionPoint.Steps, sectionOf[sectionFrom].Properties) : (start, null);
        NumberStep Given(int k, NumberStep step) => sectionsWere == null || step is not NoteStep note ? step
            : k >= end ? note with { Section = note.Section + sectionsAdded } : k >= sectionSteps && k < start ? note with { SectionProperties = sectionNow } : note;
        // Where they ask the steps the old ones asked (a paragraph of no list asks none), the answers are those they had:
        // no steps are replayed, and no blocks after them read anew; not where sections end elsewhere now, which may
        // number the notes after it anew.
        var same = sectionsWere == null && asked.Steps.Count == end - start
            && asked.Steps.Select(s => s.Step).SequenceEqual(steps.Skip(start).Take(end - start).Select(s => s.Step));
        // Else the answers from their steps on, the body's steps before them counted first (their answers stay as they
        // were: the same steps give the same answers), on note numbers that then know every reference of the body.
        var notes = new NoteNumbers(context.Settings);
        var answers = same ? null : Numbers.Replay(asked.Steps.Select(s => s.Step).Concat(steps.Skip(end).Select((s, k) => Given(end + k, s.Step))), context.Numbering,
            notes, steps.Take(start).Select((s, k) => Given(k, s.Step)));
        if (Read([.. leading, .. children, .. trailing], answers?.Take(asked.Steps.Count) ?? steps.Skip(start).Take(end - start).Select(s => s.Answer)) is not { } got)
            return Undone();
        var (from, to) = range;
        Keep(like != null ? [.. like.Skip(from).Take(like.Count - (body.Count - (to - from)))] : body.GetRange(from, to - from), got.Blocks);
        var result = new List<Block>(body.Count + got.Blocks.Count - (to - from));
        result.AddRange(CollectionsMarshal.AsSpan(body)[..from]);
        result.AddRange(got.Blocks);
        // The children after them whose numbers changed, read anew with their answers, each with the blocks read from it.
        var shift = got.Steps.Count - (end - start);
        // The answers to the steps after them, by their places before the change.
        object? Now(int k) => answers![k + shift - start];
        // The steps after them whose answers changed, by their places before the change, in order.
        var differing = new List<int>();
        for (var k = end; answers != null && k < steps.Count; k++)
        {
            if (!SameAnswer(steps[k].Answer, Now(k)))
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
            // A paragraph whose only step is its own number, at the same level of the same list, nothing waiting before or
            // after it, takes its new number in a copy of its block: nothing else of it depends on the number (see
            // ReadParagraph), and a long list numbered anew after an Enter is not read anew from its XML.
            if (j - i == 1 && until - point.Steps == 1 && point.Fresh && (After(top) is not { } beyond || bodyPoints.TryGetValue(beyond, out var then) && then.Fresh)
                && body[i] is ParagraphBlock { Parts: null, EndsSection: null, Label: { } label } paragraph && paragraph.Element == top
                && paragraph.MarkFormat.Vanish != true && steps[point.Steps].Step is ListStep
                && Now(point.Steps) is ValueTuple<string, NumberingLevel, string> number && ReferenceEquals(number.Item2, label.Level))
            {
                result.Add(paragraph with { Label = label with { Text = number.Item1, Own = number.Item3 } });
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
            if (Read([.. marks, top], answers.Skip(point.Steps + shift - start).Take(until - point.Steps)) is not { } again || again.Blocks.Count != j - i)
                return Undone();
            Keep(body.GetRange(i, j - i), again.Blocks);
            result.AddRange(again.Blocks);
            i = j;
        }
        // What reading leaves for the next change: the body's steps and answers, the points of its children (those after
        // the change moved on by the steps it added), and the children its blocks were read from (blocks read anew after
        // the change were read from the same).
        if (answers != null)
        {
            for (var k = sectionsWere != null ? sectionSteps : start; k < start; k++)
                steps[k] = (Given(k, steps[k].Step), steps[k].Answer);
            for (var k = end; sectionsWere != null && k < steps.Count; k++)
                steps[k] = (Given(k, steps[k].Step), steps[k].Answer);
            steps.RemoveRange(start, end - start);
            steps.InsertRange(start, got.Steps);
            for (var k = start; k < steps.Count; k++)
                steps[k] = (steps[k].Step, answers[k - start]);
            numberSteps[context.MainPart] = steps;
            noteNumbers = notes;
        }
        // Sections that end elsewhere may hold endnotes or number footnotes otherwise.
        NotesMoved |= !notesKept || sectionsWere != null;
        foreach (var e in old.Concat(leading).Concat(trailing))
            bodyPoints.Remove(e);
        // The old children are gone for good: a version that has them again reads them from its text.
        foreach (var e in old)
            sectionOf.Remove(e);
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

    /// <summary>
    /// Whether a re-read since the notes were read (see Notes) moved their references: one came or went, or their order
    /// changed, so that the notes number and read otherwise now; set by Reread, taken back by the one who reads them anew.
    /// </summary>
    public bool NotesMoved { get; set; }

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

    /// <summary>Whether sections number their notes alike: all with the same w:footnotePr and w:endnotePr, or without.</summary>
    static bool NumberedAlike(IReadOnlyList<XElement?> sections) =>
        sections.All(s => XNode.DeepEquals(s.Child("footnotePr"), sections[0].Child("footnotePr")) && XNode.DeepEquals(s.Child("endnotePr"), sections[0].Child("endnotePr")));

    /// <summary>Whether a paragraph's mark is deleted (a tracked change): it runs into the next one, a section break it holds with it.</summary>
    static bool MarkDeleted(XElement p) => p.Child("pPr").Child("rPr") is { } mark && (mark.Child("del") ?? mark.Child("moveFrom")) != null;

    /// <summary>
    /// Whether a child of the body shows what stands elsewhere: a content control showing its placeholder (the first
    /// such control's content, see Shown), a document of another kind or a subdocument (w:altChunk, w:subDoc).
    /// </summary>
    static bool Elsewhere(XElement child) =>
        child.DescendantsAndSelf().Any(d => d.Name == Wml.W + "showingPlcHdr" || d.Name == Wml.W + "altChunk" || d.Name == Wml.W + "subDoc");

    /// <summary>The ids of the comment ranges a child of the body begins (w:commentRangeStart), in order.</summary>
    static IEnumerable<string?> CommentRanges(XElement child) => child.Descendants(Wml.W + "commentRangeStart").Select(c => c.Attr("id"));

    /// <summary>
    /// The authors reading children of the body names (see DocumentContext.Named), in its order (which is not the
    /// document's in tables), each once: read with throwaway counters, the authors it adds to those known taken away again.
    /// </summary>
    List<string> AuthorsNamed(IReadOnlyList<XElement> elements)
    {
        var known = context.Authors.Count;
        context.Named = [];
        try
        {
            Read(elements, null);
            return [.. context.Named.Distinct()];
        }
        finally
        {
            context.Named = null;
            context.Authors.RemoveRange(known, context.Authors.Count - known);
        }
    }

    /// <summary>
    /// The field characters of children of the body that pair with none among them, as Unclosed pairs those of the body
    /// (text boxes have their own): how many ends come before any begin to pair with, and the begins no end after them
    /// pairs with, in order.
    /// </summary>
    static (int Ends, List<XElement> Begins) Unpaired(IReadOnlyList<XElement> children)
    {
        var (ends, begins) = (0, new List<XElement>());
        void Walk(XElement element)
        {
            foreach (var child in element.Elements())
            {
                if (child.Name == Wml.W + "txbxContent")
                    continue;
                if (child.Name == Wml.W + "fldChar")
                {
                    if (child.Attr("fldCharType") == "begin")
                        begins.Add(child);
                    else if (child.Attr("fldCharType") == "end")
                    {
                        if (begins.Count > 0)
                            begins.RemoveAt(begins.Count - 1);
                        else
                            ends++;
                    }
                    continue;
                }
                Walk(child);
            }
        }
        foreach (var child in children)
            Walk(child);
        return (ends, begins);
    }

    /// <summary>Whether a child of the body other than a paragraph holds a paragraph that ends a section (in a content control).</summary>
    static bool BreaksWithin(XElement child) => child.Name != Wml.W + "p" && child.Descendants(Wml.W + "sectPr").Any(s => s.Parent?.Name.LocalName == "pPr");

    /// <summary>The element a block of the body was read from.</summary>
    static XElement Of(Block block) => block switch
    {
        ParagraphBlock paragraph => paragraph.Element,
        TableBlock table => table.Element,
        _ => throw new ArgumentOutOfRangeException(nameof(block)),
    };

    /// <summary>Whether two answers to a step are the same: list numbers by their text and their own number.</summary>
    static bool SameAnswer(object? a, object? b) => a is ValueTuple<string, NumberingLevel, string> x && b is ValueTuple<string, NumberingLevel, string> y
        ? x.Item1 == y.Item1 && x.Item3 == y.Item3 : Equals(a, b);

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
    /// the same blocks at the beginning and at the end (of the same kind and text), those between them by place; a block
    /// read where <paramref name="old"/> has a NewBlock keeps the id it was read with.
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

    /// <summary>
    /// Where a change makes a block, in the blocks a change is known to leave (see WordDocument.Change): no block of before
    /// gives the block read there its id, which takes one of its own.
    /// </summary>
    internal sealed record NewBlock : Block;

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
