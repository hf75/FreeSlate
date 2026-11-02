using System.Collections.Immutable;
using System.Xml.Linq;
using FreeSlate.Docx;
using FreeSlate.Opc;

namespace FreeSlate;

/// <summary>
/// The footnotes and endnotes of a Word document: read after the body (their numbers come from the order of their
/// references), and read anew where a change gives their part new bytes or moves their references, their paragraphs
/// keeping their ids through that, note by note (ModelReader.Keep). A change is made in a note's paragraphs as in the
/// body's (see TextEdits), in the XML of the note in its part (see ChangeNote): each change a version of its own with the
/// part's bytes, which undoing and doing again put back; the note changed is read anew alone where it can be (see
/// NotesText); the pages are laid out again from the block that refers to a note whose XML changed (Paginator.Renote).
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
    /// A notes part's text as the notes were read whole from it last, which the line info of their XML refers to, and the
    /// notes changed since and read alone (see ChangeNote), each with the stretch of that text its children stood in,
    /// their text now and that text within the start tags of the part's root and the note (Piece), which the note's XML
    /// was parsed from: the part's text now is the text with those stretches in.
    /// </summary>
    sealed record NotesText(PartText Text, Dictionary<NoteKey, NoteIsland> Islands);

    sealed record NoteIsland(int Start, int End, string Inner, PartText Piece);

    /// <summary>The footnotes' and the endnotes' part's text (see NotesText), by whether they are endnotes.</summary>
    readonly Dictionary<bool, NotesText> notesTexts = [];

    /// <summary>
    /// The notes as the layout takes them (see Notes), read anew since the layout had them where pending, and the notes
    /// whose XML changed since then (or that came or went), which it lays out again from their references.
    /// </summary>
    (Dictionary<string, List<Block>> Footnotes, List<(int Section, List<Block> Blocks)> Endnotes, List<Block>? Separator, List<Block>? Continuation) notesLaid;
    bool notesPending;
    readonly HashSet<NoteKey> notesChanged = [];

    /// <summary>
    /// The notes being read bit by bit before a long document's pages are laid out from the first (see Step), which their
    /// pages want first; what they read as, once read (see ReadNotes); and whether those read so wait for the layout, which
    /// reads them otherwise (see Scenes).
    /// </summary>
    IEnumerator<bool>? notesAhead;
    (Dictionary<string, List<Block>> Footnotes, Dictionary<string, List<Block>> Endnotes)? readAhead;
    bool notesFresh;

    /// <summary>The notes read on for the time of a slice (see notesAhead).</summary>
    void NotesAhead()
    {
        IEnumerable<bool> Reading()
        {
            var (footnotes, endnotes) = (new Dictionary<string, List<Block>>(StringComparer.Ordinal), new Dictionary<string, List<Block>>(StringComparer.Ordinal));
            foreach (var note in reader.NotesBitByBit(false, footnotes))
                yield return note;
            foreach (var note in reader.NotesBitByBit(true, endnotes))
                yield return note;
            readAhead = (footnotes, endnotes);
        }
        notesAhead ??= Reading().GetEnumerator();
        var clock = System.Diagnostics.Stopwatch.GetTimestamp();
        while (notesAhead.MoveNext())
        {
            if (System.Diagnostics.Stopwatch.GetElapsedTime(clock).TotalMilliseconds >= SliceTime)
                return;
        }
        notesAhead.Dispose();
        notesAhead = null;
    }

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
        var (footnotes, endnoteParts) = readAhead ?? (reader.Notes(endnotes: false), reader.Notes(endnotes: true));
        readAhead = null;
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
                // As it read from these bytes before (undoing, doing again; see readFrom), else as it reads now.
                if (((NotePart(endnote) is { } notesPart ? Recalled(notesPart, key) : null) ?? wasRead.GetValueOrDefault(key)) is { } before)
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
                Collect(noteParagraphs, block, block);
            }
        }
        layout?.Forget(gone);
        var endnotes = endnoteParts.Where(n => n.Key is not ("separator" or "continuationSeparator" or "continuationNotice")).OrderBy(n => reader.EndnoteNumber(n.Key))
            .Where(n => reader.EndnoteNumber(n.Key) > 0).Select(n => (reader.EndnoteSection(n.Key), n.Value)).ToList();
        notesLaid = (footnotes, endnotes, endnoteParts.GetValueOrDefault("separator"), endnoteParts.GetValueOrDefault("continuationSeparator"));
        notesTexts.Clear();
        foreach (var endnote in new[] { false, true })
        {
            if (NotePart(endnote) is { } part)
                notesTexts[endnote] = new NotesText(new PartText(package.ReadBytes(part)), []);
        }
        reader.NotesMoved = false;
        touched.Clear();
        noteParts = NoteParts();
        (notesKnown, notesPending) = (true, true);
    }

    /// <summary>The paragraphs of a block of a note or story (itself, those of its tables and text boxes) by id, with the block.</summary>
    static void Collect(Dictionary<int, (ParagraphBlock Paragraph, Block Top)> into, Block block, Block top)
    {
        if (block is ParagraphBlock paragraph)
            into.TryAdd(paragraph.Id, (paragraph, top));
        foreach (var list in ModelReader.Inner(block))
        {
            foreach (var inner in list)
                Collect(into, inner, top);
        }
    }

    /// <summary>The paragraphs of a block of a note forgotten by id (itself, those of its tables and text boxes), and added to those gone.</summary>
    void ForgetNote(Block block, HashSet<ParagraphBlock> gone)
    {
        if (block is ParagraphBlock paragraph)
        {
            gone.Add(paragraph);
            if (noteParagraphs.TryGetValue(paragraph.Id, out var known) && ReferenceEquals(known.Paragraph, paragraph))
                noteParagraphs.Remove(paragraph.Id);
        }
        foreach (var list in ModelReader.Inner(block))
        {
            foreach (var inner in list)
                ForgetNote(inner, gone);
        }
    }

    /// <summary>Whether blocks hold a numbered paragraph (in their tables and text boxes too).</summary>
    static bool Numbered(IEnumerable<Block> blocks) =>
        blocks.Any(block => block is ParagraphBlock { Label: not null } || ModelReader.Inner(block).Any(Numbered));

    /// <summary>The list a block of the body, of a note, of a header or footer or of a comment stands in (the body's blocks, the note's, the story's, the comment's) and its place there.</summary>
    (List<Block> List, int Index) TopList(Block top, bool walking = false)
    {
        var list = noteOf.TryGetValue(top, out var key) ? notesRead[key] : storyOf.TryGetValue(top, out var part) ? reader.Stories[part]
            : commentOf.TryGetValue(top, out var comment) ? commentsRead[comment] : null;
        return list == null ? (blocks, IndexOf(top, walking)) : (list, list.FindIndex(b => ReferenceEquals(b, top)));
    }

    /// <summary>The footnotes' or the endnotes' part, where the package has it.</summary>
    string? NotePart(bool endnote) =>
        package.FirstRelationship(context.MainPart, endnote ? "endnotes" : "footnotes") is { External: false } related && package.Exists(related.Target) ? related.Target : null;

    /// <summary>
    /// The other parts a change of a stretch of the body gives new bytes, with the notes it took the last references of away
    /// gone from theirs, every other character as it was (measured in Word 365 by COM, scratchpad b9/noteedit.vbs: a reference
    /// taken away takes its note along); as a tracked change, the notes stay deleted as such, as Word leaves them.
    /// </summary>
    IReadOnlyDictionary<string, byte[]?>? NotesGone(Stretch stretch, string text, ImmutableList<Island> changed, bool tracked, IReadOnlyDictionary<string, byte[]?>? changes)
    {
        if (!stretch.Text.Text.Contains("noteReference", StringComparison.Ordinal))
            return changes;
        var own = stretch.Written([]);
        if (own == text)
            return changes;
        var gone = TextEdits.Referred(stretch, own);
        gone.ExceptWith(TextEdits.Referred(stretch, text));
        if (gone.Count == 0)
            return changes;
        // (Read as they are now, to be taken back with the ids they have where undoing brings them back: see Remember.)
        KnowNotes();
        var body = wholeText.Replaced(changed.Select(i => (i.Start, i.End, i.Text)));
        var all = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        if (changes != null)
        {
            foreach (var (name, bytes) in changes)
                all[name] = bytes;
        }
        foreach (var endnote in new[] { false, true })
        {
            var kind = endnote ? "endnote" : "footnote";
            // (A reference left to the note, which Word does not make, keeps it.)
            var notes = gone.Where(n => n.Endnote == endnote && (tracked || !System.Text.RegularExpressions.Regex.IsMatch(body, $@"<[^<>]*{kind}Reference\b[^<>]*\bid=[""']{n.Id}[""']"))).Select(n => n.Id).ToHashSet();
            if (notes.Count == 0 || NotePart(endnote) is not { } part || all.ContainsKey(part))
                continue;
            var read = new PartText(package.ReadBytes(part));
            var root = XDocument.Parse(read.Text, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo).Root!;
            var tracking = tracked ? Tracked(() => ChangeId(part, () => read.Text)) : null;
            var cuts = new List<(int Start, int End, string Text)>();
            foreach (var note in root.Elements(root.Name.Namespace + kind).Where(n => notes.Contains((string?)n.Attribute(n.Name.Namespace + "id") ?? "")))
            {
                var at = read.Start(note);
                if (tracking != null)
                    TextEdits.NoteDeleted(note, tracking);
                cuts.Add((at, read.End(at), tracking != null ? PartText.Xml(note) : ""));
            }
            if (tracking != null)
                changeId = tracking.NextId;
            all[part] = read.Spliced(cuts);
        }
        return all;
    }

    /// <summary>
    /// A change made in the paragraphs of a note (see Change for the body's): <paramref name="edit"/> gives the new text of
    /// a stretch of the note's children in the XML of its part (see Stretch), which takes the place of theirs, every other
    /// character of the part as it was; the part's new bytes (with those of the other parts the change gives, see Put)
    /// a version of their own. The note's new text is parsed apart, made ready as the part's XML is when loaded, and the
    /// note read alone, its paragraphs taking the ids they had (see NotesText); where it holds numbered paragraphs, which
    /// count on over the notes, the notes are read whole (see ReadNotes). Returns where an element of the note's new text
    /// stands in it.
    /// </summary>
    Func<XElement, int> ChangeNote(NoteKey key, Func<Stretch, Tracking?, string> edit, IReadOnlyDictionary<string, byte[]?>? changes)
    {
        if (NotePart(key.Endnote) is not { } part || !notesXml.TryGetValue(key, out var element) || element.Document?.Root is not { } root
            || !notesTexts.TryGetValue(key.Endnote, out var notes))
            throw new NotSupportedException("The note is not where it was read.");
        // The note's text as the last change left it (parsed apart), else as the part was read whole.
        var island = notes.Islands.GetValueOrDefault(key);
        var text = island?.Piece ?? notes.Text;
        var (start, rootStart) = (text.Start(element), text.Start(root));
        if (start < 0 || rootStart < 0 || text.Text[text.TagEnd(start) - 2] == '/')
            throw new NotSupportedException("The note is not where it was read.");
        // Its children, within the start tags of the part's root and the note, which the stretch is parsed within.
        var (open, close) = (text.TagEnd(start), text.Text.LastIndexOf('<', text.End(start) - 1));
        var head = text.Text[rootStart..text.TagEnd(rootStart)] + text.Text[start..open];
        var tail = text.Text[close..text.End(start)] + $"</{Qualified(text.Text, rootStart)}>";
        // An element read from the note (not one a control showing a placeholder shows from another part, see ModelReader.Shown).
        var stretch = new Stretch(head, text.Text[open..close], tail,
            e => e.Document == element.Document && text.Start(e) is var at && at >= open && at < close ? at - open + head.Length : -1);
        var tracking = Tracked(() => ChangeId(part, () => notes.Text.Text));
        var inner = edit(stretch, tracking);
        if (tracking != null)
            changeId = tracking.NextId;
        // The note's new text within the start tags of the part's root and the note, and the part's text with it.
        var piece = new PartText(head + inner + tail);
        var islandsNow = new Dictionary<NoteKey, NoteIsland>(notes.Islands)
        {
            [key] = island is { } was ? was with { Inner = inner, Piece = piece } : new NoteIsland(open, close, inner, piece),
        };
        var given = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, changed) in changes ?? new Dictionary<string, byte[]?>())
            given[name] = changed;
        var spliced = notes.Text.Spliced(islandsNow.Values.OrderBy(i => i.Start).Select(i => (i.Start, i.End, i.Inner)));
        given[part] = spliced;
        var read = notesRead[key];
        List<Block>? alone = null;
        XElement? note = null;
        if (!Numbered(read) && XDocument.Parse(piece.Text, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo).Root?.Elements().FirstOrDefault() is { } parsed)
        {
            parsed.Add(context.Prepare(parsed));
            (alone, note) = (reader.Note(parsed, key.Endnote), parsed);
        }
        var partsBefore = partBytes;
        Put(given);
        Func<XElement, int> where;
        if (alone != null && note != null)
        {
            // The note read alone takes the place of what it was: its paragraphs with the ids they had, those before
            // forgotten (by the layout too), its blocks among the notes the layout takes.
            touched.Remove(part);
            ModelReader.Keep(read, alone);
            var gone = new HashSet<ParagraphBlock>(ReferenceEqualityComparer.Instance);
            foreach (var block in read)
            {
                noteOf.Remove(block);
                ForgetNote(block, gone);
            }
            foreach (var block in alone)
            {
                noteOf[block] = key;
                Collect(noteParagraphs, block, block);
            }
            layout?.Forget(gone);
            (notesRead[key], notesXml[key], notesTexts[key.Endnote]) = (alone, note, notes with { Islands = islandsNow });
            if (!key.Endnote)
                notesLaid.Footnotes[key.Id] = alone;
            else if (notesLaid.Endnotes.FindIndex(e => ReferenceEquals(e.Blocks, read)) is var k and >= 0)
                notesLaid.Endnotes[k] = (notesLaid.Endnotes[k].Section, alone);
            notesChanged.Add(key);
            notesPending = true;
            where = e => piece.Start(e) is var at && at >= head.Length && at < head.Length + inner.Length ? at - head.Length : -1;
        }
        else
        {
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
            var now = new PartText(spliced);
            var innerStart = notesXml.TryGetValue(key, out var reread) && now.Start(reread) is var found and >= 0 ? now.TagEnd(found) : -1;
            where = e => now.Start(e) is var at && innerStart >= 0 && at >= innerStart && at < innerStart + inner.Length ? at - innerStart : -1;
        }
        versions.RemoveRange(version + 1, versions.Count - version - 1);
        versions.Add(new Version(whole, islands, kept, partBytes));
        version++;
        fresh = notesRead.GetValueOrDefault(key) ?? [];
        return where;
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
