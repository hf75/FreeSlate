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
    /// The body's blocks after some of its children changed, read anew where they changed: the blocks
    /// <paramref name="first"/> to <paramref name="first"/> + <paramref name="count"/> - 1 of <paramref name="body"/> (as
    /// this reader read it, each from a child of the body of its own) are replaced in the XML loaded for the document by
    /// <paramref name="children"/> (none to take them away), prepared as the document's XML is when it is loaded. The new
    /// children are read with the state reading left before the old ones, their number steps replayed with the body's
    /// others (see NumberStep), and the blocks after them whose numbers change are read anew too; blocks that stand for the
    /// old ones keep their ids, new ones take ids of their own. Null, the XML left as it was, where reading cannot begin
    /// afresh before the change or after it (a field, a paragraph whose mark is hidden or deleted, a bookmark reaching
    /// across), where a note reference is involved (the notes' marks are read with the notes) or in the markup view: the
    /// document has to be read again.
    /// </summary>
    public List<Block>? Reread(List<Block> body, int first, int count, IReadOnlyList<XElement> children)
    {
        if (context.Markup || bodyElement == null || count < 1 || first < 0 || first + count > body.Count)
            return null;
        var old = body.GetRange(first, count);
        if (old.Any(b => Of(b).Parent != bodyElement || !bodyPoints.ContainsKey(Of(b)))
            || old.OfType<ParagraphBlock>().Any(p => p.MarkFormat.Vanish == true)
            || first > 0 && body[first - 1] is ParagraphBlock { MarkFormat.Vanish: true })
            return null;
        // The old blocks' children are one after the other, with nothing between them.
        for (var k = 1; k < old.Count; k++)
        {
            if (Of(old[k]).PreviousNode is not XElement before || before != Of(old[k - 1]))
                return null;
        }
        // What goes with them: marks between paragraphs before them, which the first takes, and after them, which the
        // last takes, up to the next block.
        var leading = new List<XElement>();
        if (!bodyPoints[Of(old[0])].Fresh)
        {
            for (var node = Of(old[0]).PreviousNode; ; node = node.PreviousNode)
            {
                if (node == null)
                    return null;
                if (node is not XElement e)
                    continue;
                if (e.Name.LocalName is "p" or "tbl" || !bodyPoints.TryGetValue(e, out var point))
                    return null;
                leading.Insert(0, e);
                if (point.Fresh)
                    break;
            }
        }
        var next = first + count < body.Count ? body[first + count] : null;
        if (next != null && (Of(next).Parent != bodyElement || !bodyPoints.TryGetValue(Of(next), out var nextPoint) || !nextPoint.Fresh))
            return null;
        var trailing = Trailing(Of(old[^1]), next != null ? Of(next) : null);
        // Marks between paragraphs need a paragraph to go into.
        if (trailing == null || children.Count == 0 && (leading.Count > 0 || trailing.Count > 0))
            return null;
        var steps = numberSteps.GetValueOrDefault(context.MainPart) ?? [];
        var start = bodyPoints[leading.Count > 0 ? leading[0] : Of(old[0])].Steps;
        var end = next != null ? bodyPoints[Of(next)].Steps : steps.Count;
        if (steps.Skip(start).Take(end - start).Any(s => s.Step is NoteStep))
            return null;
        // The new children in place of the old ones, in their section.
        var section = sectionOf.GetValueOrDefault(Of(old[0]));
        var anchor = Of(old[0]).PreviousNode;
        foreach (var child in children)
        {
            sectionOf[child] = section;
            Of(old[0]).AddBeforeSelf(child);
        }
        foreach (var b in old)
            Of(b).Remove();
        // Which fields of the body end in it is worked out anew (see Unclosed).
        unclosed.Remove(bodyElement);
        var elements = leading.Concat(children).Concat(trailing).ToList();
        // Their steps first, asked of throwaway counters; then the body's steps with them replayed, and the children read
        // with their answers.
        List<(NumberStep Step, object? Answer)> all = [];
        List<object?> answers = [];
        (List<Block> Blocks, List<(NumberStep Step, object? Answer)> Steps, Dictionary<XElement, (int Steps, bool Fresh)> Points)? read = null;
        if (Read(elements, null) is { } probe && !probe.Steps.Any(s => s.Step is NoteStep))
        {
            all = [.. steps.Take(start), .. probe.Steps, .. steps.Skip(end)];
            answers = Numbers.Replay(all.Select(s => s.Step), context.Numbering, new NoteNumbers(context.Settings));
            read = Read(elements, answers.Skip(start).Take(probe.Steps.Count));
        }
        // Back as it was, where reading the change does not come out so.
        List<Block>? Undone()
        {
            foreach (var child in children)
                child.Remove();
            foreach (var b in Enumerable.Reverse(old))
            {
                if (anchor != null)
                    anchor.AddAfterSelf(Of(b));
                else
                    bodyElement.AddFirst(Of(b));
            }
            unclosed.Remove(bodyElement);
            return null;
        }
        if (read is not { } got)
            return Undone();
        Keep(old, got.Blocks);
        var result = new List<Block>(body.Count + got.Blocks.Count - count);
        result.AddRange(body.Take(first));
        result.AddRange(got.Blocks);
        // The blocks after them whose numbers changed, read anew with their answers.
        var shift = got.Steps.Count - (end - start);
        for (var i = first + count; i < body.Count; i++)
        {
            var block = body[i];
            var from = bodyPoints.TryGetValue(Of(block), out var point) ? point.Steps : -1;
            var to = i + 1 < body.Count && bodyPoints.TryGetValue(Of(body[i + 1]), out var following) ? following.Steps : steps.Count;
            if (from < 0 || Enumerable.Range(from, to - from).All(k => Equals(Answer(steps[k].Answer), Answer(answers[k + shift]))))
            {
                result.Add(block);
                continue;
            }
            // Read anew, it keeps its own state: the steps it asked, the points of its children.
            var again = point.Fresh && Trailing(Of(block), i + 1 < body.Count ? Of(body[i + 1]) : null) is { } after
                ? Read([Of(block), .. after], answers.Skip(from + shift).Take(to - from)) : null;
            if (again is not { Blocks: [_] })
                return Undone();
            Keep([block], again.Value.Blocks);
            result.Add(again.Value.Blocks[0]);
        }
        // What reading leaves for the next change: the body's steps and answers, and the points of its children.
        numberSteps[context.MainPart] = [.. all.Select((s, k) => (s.Step, answers[k]))];
        foreach (var e in old.Select(b => Of(b)).Concat(leading).Concat(trailing))
            bodyPoints.Remove(e);
        foreach (var (e, point) in bodyPoints.ToList())
        {
            if (point.Steps >= end)
                bodyPoints[e] = (point.Steps + shift, point.Fresh);
        }
        foreach (var (e, point) in got.Points)
            bodyPoints[e] = (point.Steps + start, point.Fresh);
        return result;
    }

    /// <summary>
    /// The children of the body after a block's own up to the next block's (marks between paragraphs, which the block
    /// takes), the body's own section properties aside; null where a paragraph or table stands among them.
    /// </summary>
    List<XElement>? Trailing(XElement block, XElement? next)
    {
        var trailing = new List<XElement>();
        foreach (var e in block.ElementsAfterSelf())
        {
            if (e == next)
                break;
            if (e.Name.LocalName is "p" or "tbl")
                return null;
            if (e.Name.LocalName != "sectPr")
                trailing.Add(e);
        }
        return trailing;
    }

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
    /// Blocks read anew keep the ids of the blocks they stand for: a block of the same kind in the same place, the blocks
    /// within it (a table's cells', a text box's) where they are as many.
    /// </summary>
    static void Keep(List<Block> old, List<Block> read)
    {
        for (var i = 0; i < Math.Min(old.Count, read.Count); i++)
        {
            if (old[i].GetType() != read[i].GetType())
                continue;
            var (was, now) = (Ids(old[i]), Ids(read[i]));
            read[i] = was.Count == now.Count ? Renumbered(read[i], now.Zip(was).ToDictionary(p => p.First, p => p.Second)) : read[i] with { Id = old[i].Id };
        }
    }

    /// <summary>The ids of a block and of the blocks within it, in order.</summary>
    static List<int> Ids(Block block)
    {
        var ids = new List<int>();
        foreach (var list in Inner(block))
        {
            foreach (var inner in list)
                ids.AddRange(Ids(inner));
        }
        ids.Add(block.Id);
        return ids;
    }

    /// <summary>The lists of blocks within a block: a paragraph's text boxes', a table's cells'.</summary>
    static IEnumerable<List<Block>> Inner(Block block)
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

    /// <summary>A block read anew with the ids of the one it stands for, the blocks within it too (their lists changed in place).</summary>
    static Block Renumbered(Block block, Dictionary<int, int> map)
    {
        foreach (var list in Inner(block))
        {
            for (var k = 0; k < list.Count; k++)
                list[k] = Renumbered(list[k], map);
        }
        return block with { Id = map.GetValueOrDefault(block.Id, block.Id) };
    }
}
