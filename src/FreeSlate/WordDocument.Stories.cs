using System.Collections.Immutable;
using System.Xml.Linq;
using FreeSlate.Docx;
using FreeSlate.Docx.Layout;
using FreeSlate.Opc;

namespace FreeSlate;

/// <summary>
/// The headers and footers of a Word document: their stories as the reader read them for the layout (ModelReader.Story),
/// their paragraphs found by id. A change is made in a story's paragraphs as in the body's (see TextEdits), in the XML of
/// its part (see ChangeStory): each change a version of its own with the part's bytes, which undoing and doing again put
/// back; the story read anew (ModelReader.Restory), its paragraphs keeping their ids; the pages that show it finished
/// anew, laid out again from the first where it takes other room (Paginator.Restory).
/// </summary>
public sealed partial class WordDocument
{
    /// <summary>
    /// The paragraphs of the stories the reader read (of their tables and text boxes too) by id, each with the block of
    /// its story it stands in, and the part each such block is of; indexed for the reader and the count of its readings
    /// (ModelReader.StoriesRead) they were indexed for.
    /// </summary>
    readonly Dictionary<int, (ParagraphBlock Paragraph, Block Top)> storyParagraphs = [];
    readonly Dictionary<Block, string> storyOf = new(ReferenceEqualityComparer.Instance);
    (ModelReader? Reader, int Read) storiesIndexed;

    /// <summary>The stories read anew since the layout had them (see Restory), which it lays out again (Paginator.Restory).</summary>
    readonly HashSet<string> storiesChanged = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The paragraphs of the stories indexed where the reader read stories since (see storyParagraphs).</summary>
    void KnowStories()
    {
        // A document read whole again knows the stories it read before (see ModelReader.KeepStories).
        reader.ReadKeptStories();
        if (ReferenceEquals(storiesIndexed.Reader, reader) && storiesIndexed.Read == reader.StoriesRead)
            return;
        storyParagraphs.Clear();
        storyOf.Clear();
        foreach (var (part, story) in reader.Stories)
        {
            foreach (var block in story)
            {
                storyOf[block] = part;
                Collect(storyParagraphs, block, block);
            }
        }
        storiesIndexed = (reader, reader.StoriesRead);
    }

    /// <summary>
    /// Stories read anew after their parts were given new bytes (see ModelReader.Restory, alone or with the others): the
    /// paragraphs of those before forgotten (by the layout too), the new ones indexed, the stories read anew for the
    /// layout to lay out again.
    /// </summary>
    void Restory(IEnumerable<string> parts)
    {
        var gone = new HashSet<ParagraphBlock>(ReferenceEqualityComparer.Instance);
        foreach (var part in parts)
        {
            foreach (var (story, before) in reader.Restory(part))
            {
                foreach (var block in before)
                    Gone(block, gone);
                // A part undoing took away again is laid out anew with the section that named it.
                if (package.Exists(story))
                    storiesChanged.Add(story);
                else
                    storiesChanged.Remove(story);
            }
        }
        layout?.Forget(gone);
        FieldEvaluator.StoriesChanged(context);
        KnowStories();

        static void Gone(Block block, HashSet<ParagraphBlock> gone)
        {
            if (block is ParagraphBlock paragraph)
                gone.Add(paragraph);
            foreach (var list in ModelReader.Inner(block))
            {
                foreach (var inner in list)
                    Gone(inner, gone);
            }
        }
    }

    /// <summary>The stories the reader read whose parts two versions have otherwise (see Version.Parts).</summary>
    List<string> StoriesDiffering(ImmutableDictionary<string, byte[]?> one, ImmutableDictionary<string, byte[]?> other) =>
        [.. reader.Stories.Keys.Where(part => one.TryGetValue(part, out var a) != other.TryGetValue(part, out var b) || !ReferenceEquals(a, b))];

    /// <summary>
    /// A change made in the paragraphs of a header or footer (see Change for the body's): <paramref name="edit"/> gives
    /// the new text of the children of its part's root (see Stretch), which takes the place of theirs, every other
    /// character of the part as it was; the part's new bytes (with those of the other parts the change gives, see Put) a
    /// version of their own, the story read anew (see Restory). Returns where an element of the new text stands in it.
    /// </summary>
    Func<XElement, int> ChangeStory(string part, Func<Stretch, Tracking?, string> edit, IReadOnlyDictionary<string, byte[]?>? changes)
    {
        if (context.Load(part).Root is not { } root)
            throw new NotSupportedException("The header or footer is not where it was read.");
        var text = new PartText(package.ReadBytes(part));
        var start = text.Start(root);
        if (start < 0 || text.Text[text.TagEnd(start) - 2] == '/')
            throw new NotSupportedException("The header or footer is not where it was read.");
        // The root's children, within its start and end tags, which the stretch is parsed within.
        var (open, close) = (text.TagEnd(start), text.Text.LastIndexOf('<', text.End(start) - 1));
        var (head, tail) = (text.Text[start..open], text.Text[close..text.End(start)]);
        // An element read from the part (not one a control showing a placeholder shows from another part, see ModelReader.Shown).
        var stretch = new Stretch(head, text.Text[open..close], tail,
            e => e.Document == root.Document && text.Start(e) is var at && at >= open && at < close ? at - open + head.Length : -1);
        var tracking = Author != null ? new Tracking(Author, ChangedAt ?? DateTime.Now, ChangeId(part, () => text.Text)) : null;
        var inner = edit(stretch, tracking);
        if (tracking != null)
            changeId = tracking.NextId;
        var given = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, changed) in changes ?? new Dictionary<string, byte[]?>())
            given[name] = changed;
        var spliced = text.Spliced([(open, close, inner)]);
        given[part] = spliced;
        var partsBefore = partBytes;
        Put(given);
        try
        {
            Restory([part]);
        }
        catch
        {
            Restore(partsBefore);
            Restory([part]);
            throw;
        }
        versions.RemoveRange(version + 1, versions.Count - version - 1);
        versions.Add(new Version(whole, islands, kept, partBytes));
        version++;
        fresh = reader.Stories.GetValueOrDefault(part) ?? [];
        var now = new PartText(spliced);
        return e => now.Start(e) is var at && at >= open && at < open + inner.Length ? at - open : -1;
    }
}
