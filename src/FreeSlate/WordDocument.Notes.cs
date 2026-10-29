using System.Xml.Linq;
using FreeSlate.Docx;
using FreeSlate.Opc;

namespace FreeSlate;

/// <summary>
/// The footnotes and endnotes of a Word document: read after the body (their numbers come from the order of their
/// references), and read anew where a change gives their part new bytes or moves their references, their paragraphs
/// keeping their ids through that, note by note (ModelReader.Keep). A change is made in a note's paragraphs as in the
/// body's (see TextEdits), in the XML of the note in its part (see ChangeNote): each change a version of its own with the
/// part's bytes, which undoing and doing again put back; the pages are laid out again from the block that refers to a
/// note whose XML changed (Paginator.Renote).
/// </summary>
public sealed partial class WordDocument
{
    /// <summary>A note by kind and id (a w:footnote's or w:endnote's w:id).</summary>
    readonly record struct NoteKey(bool Endnote, string Id);

    /// <summary>
    /// The notes as read last (see ReadNotes): the blocks of each, the XML each was read from, and their paragraphs (of
    /// tables and text boxes too) by id, each with the block of its note it stands in, and the note each such block is of.
    /// </summary>
    Dictionary<NoteKey, List<Block>> notesRead = [];
    Dictionary<NoteKey, XElement> notesXml = [];
    readonly Dictionary<int, (ParagraphBlock Paragraph, Block Top)> noteParagraphs = [];
    readonly Dictionary<Block, NoteKey> noteOf = new(ReferenceEqualityComparer.Instance);

    /// <summary>Whether the notes were read for the document as it reads now (not before it was read whole last).</summary>
    bool notesKnown;

    /// <summary>
    /// The notes as the layout takes them (see Notes), read anew since the layout had them where pending, and the notes
    /// whose XML changed since then (or that came or went), which it lays out again from their references.
    /// </summary>
    (Dictionary<string, List<Block>> Footnotes, List<(int Section, List<Block> Blocks)> Endnotes, List<Block>? Separator, List<Block>? Continuation) notesLaid;
    bool notesPending;
    readonly HashSet<NoteKey> notesChanged = [];

    /// <summary>The notes read where they are not known or read otherwise now (see NotesChanged).</summary>
    void KnowNotes()
    {
        if (!notesKnown || NotesChanged())
            ReadNotes();
    }

    /// <summary>
    /// Reads the notes anew (see Notes): each note's paragraphs take the ids they had (by place and text, see
    /// ModelReader.Keep), those of notes gone forgotten (by the layout too); the notes whose XML differs from what they
    /// were read from before, and those that came or went, are those changed.
    /// </summary>
    void ReadNotes()
    {
        var footnotes = reader.Notes(endnotes: false);
        var endnoteParts = reader.Notes(endnotes: true);
        var (wasRead, wasXml) = (notesRead, notesXml);
        (notesRead, notesXml) = ([], []);
        foreach (var (endnote, notes) in new[] { (false, footnotes), (true, endnoteParts) })
        {
            var elements = new Dictionary<string, XElement>(StringComparer.Ordinal);
            if (NotePart(endnote) is { } part && context.Load(part).Root is { } root)
            {
                foreach (var note in root.Elements(Wml.W + (endnote ? "endnote" : "footnote")))
                    elements.TryAdd(note.Attr("id") ?? "", note);
            }
            foreach (var (id, blocks) in notes)
            {
                // The separators and the continuation notice go by their types, and no change is made in them.
                if (id is "separator" or "continuationSeparator" or "continuationNotice")
                    continue;
                var key = new NoteKey(endnote, id);
                if (wasRead.TryGetValue(key, out var before))
                    ModelReader.Keep(before, blocks);
                notesRead[key] = blocks;
                if (elements.TryGetValue(id, out var element))
                    notesXml[key] = element;
                if (element == null || !wasXml.TryGetValue(key, out var was) || !XNode.DeepEquals(was, element))
                    notesChanged.Add(key);
            }
        }
        notesChanged.UnionWith(wasRead.Keys.Where(key => !notesRead.ContainsKey(key)));
        // The paragraphs of the notes as read before are gone (read anew in their place).
        var gone = new HashSet<ParagraphBlock>(noteParagraphs.Values.Select(p => p.Paragraph), ReferenceEqualityComparer.Instance);
        noteParagraphs.Clear();
        noteOf.Clear();
        foreach (var (key, blocks) in notesRead)
        {
            foreach (var block in blocks)
            {
                noteOf[block] = key;
                CollectNote(block, block);
            }
        }
        layout?.Forget(gone);
        var endnotes = endnoteParts.Where(n => n.Key is not ("separator" or "continuationSeparator" or "continuationNotice")).OrderBy(n => reader.EndnoteNumber(n.Key))
            .Where(n => reader.EndnoteNumber(n.Key) > 0).Select(n => (reader.EndnoteSection(n.Key), n.Value)).ToList();
        notesLaid = (footnotes, endnotes, endnoteParts.GetValueOrDefault("separator"), endnoteParts.GetValueOrDefault("continuationSeparator"));
        reader.NotesMoved = false;
        touched.Clear();
        noteParts = NoteParts();
        (notesKnown, notesPending) = (true, true);
    }

    /// <summary>The paragraphs of a block of a note (itself, those of its tables and text boxes) by id, with the block.</summary>
    void CollectNote(Block block, Block top)
    {
        if (block is ParagraphBlock paragraph)
            noteParagraphs.TryAdd(paragraph.Id, (paragraph, top));
        foreach (var list in ModelReader.Inner(block))
        {
            foreach (var inner in list)
                CollectNote(inner, top);
        }
    }

    /// <summary>The list a block of the body or of a note stands in (the body's blocks, the note's) and its place there.</summary>
    (List<Block> List, int Index) TopList(Block top, bool walking = false)
    {
        if (!noteOf.TryGetValue(top, out var key))
            return (blocks, IndexOf(top, walking));
        var list = notesRead[key];
        return (list, list.FindIndex(b => ReferenceEquals(b, top)));
    }

    /// <summary>The footnotes' or the endnotes' part, where the package has it.</summary>
    string? NotePart(bool endnote) =>
        package.FirstRelationship(context.MainPart, endnote ? "endnotes" : "footnotes") is { External: false } related && package.Exists(related.Target) ? related.Target : null;

    /// <summary>
    /// A change made in the paragraphs of a note (see Change for the body's): <paramref name="edit"/> gives the new text of
    /// a stretch of the note's children in the XML of its part (see Stretch), which takes the place of theirs, every other
    /// character of the part as it was; the part's new bytes (with those of the other parts the change gives, see Put)
    /// a version of their own, the notes read anew. Returns where an element of the note's new text stands in it.
    /// </summary>
    Func<XElement, int> ChangeNote(NoteKey key, Func<Stretch, Tracking?, string> edit, IReadOnlyDictionary<string, byte[]?>? changes)
    {
        if (NotePart(key.Endnote) is not { } part || !notesXml.TryGetValue(key, out var element) || element.Document?.Root is not { } root)
            throw new NotSupportedException("The note is not where it was read.");
        var bytes = package.ReadBytes(part);
        var text = new PartText(bytes);
        var (start, rootStart) = (text.Start(element), text.Start(root));
        if (start < 0 || rootStart < 0 || text.Text[text.TagEnd(start) - 2] == '/')
            throw new NotSupportedException("The note is not where it was read.");
        // Its children, within the start tags of the part's root and the note, which the stretch is parsed within.
        var (open, close) = (text.TagEnd(start), text.Text.LastIndexOf('<', text.End(start) - 1));
        var head = text.Text[rootStart..text.TagEnd(rootStart)] + text.Text[start..open];
        var tail = text.Text[close..text.End(start)] + $"</{Qualified(text.Text, rootStart)}>";
        var stretch = new Stretch(head, text.Text[open..close], tail, e => text.Start(e) is var at && at >= open && at < close ? at - open + head.Length : -1);
        var tracking = Author != null ? new Tracking(Author, ChangedAt ?? DateTime.Now, ChangeId()) : null;
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
            ReadNotes();
        }
        catch
        {
            Restore(partsBefore);
            ReadNotes();
            throw;
        }
        versions.RemoveRange(version + 1, versions.Count - version - 1);
        versions.Add(new Version(whole, islands, kept, partBytes));
        version++;
        fresh = notesRead.GetValueOrDefault(key) ?? [];
        var now = new PartText(spliced);
        return e => now.Start(e) is var at && at >= open && at < open + inner.Length ? at - open : -1;
    }

    /// <summary>The qualified name of the element whose start tag begins at a place of a part's text.</summary>
    static string Qualified(string text, int start)
    {
        var end = start + 1;
        while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] is not ('>' or '/'))
            end++;
        return text[(start + 1)..end];
    }
}
