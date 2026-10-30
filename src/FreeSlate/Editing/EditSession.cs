namespace FreeSlate.Editing;

/// <summary>A place in a document's text: a paragraph by its id and an offset in its text (see IEditableDocument).</summary>
public readonly record struct TextPlace(int Paragraph, int Offset);

/// <summary>
/// A selection copied (see EditSession.Copy): its text, as the system's clipboard takes it, and where the document keeps
/// more, its paragraphs in the document's own markup for pasting them with their formatting into it (see
/// IEditableDocument.Copy), the document they are of (<paramref name="From"/>: another takes the text).
/// </summary>
public sealed record Fragment(string Text, IReadOnlyList<string>? Markup, IEditableDocument? From = null);

/// <summary>
/// An editor's work on a document without its surface: the cursor and the selection as places in its text, and what an
/// editor's keys do there, as a word processor's do. Typing replaces the selection; Backspace and Delete take the
/// selection or the character before or after the cursor away, at a paragraph's edge joining it with the one beside it;
/// Enter splits the paragraph, Shift+Enter breaks the line; formats go to the selection's text, without one to the word
/// the cursor stands in or to the text typed next; text copied is the selection's, text pasted replaces it. Each
/// handling is one step to undo and do again, the changes it made in the document undone together; typing on from where
/// the last typing ended adds to it. A handling the document cannot make (see IEditableDocument) leaves it as it was and
/// throws NotSupportedException. A selection reaches over the paragraphs of a story in reading order (a Word document's
/// body over its tables too); deleting it or typing over it takes the whole tables in it away where its ends stand in
/// one body, cell or text box, and the rows of a table it reaches into from outside (see IEditableDocument.RemoveAcross),
/// as Word does; one over cells of a table alone is refused (Word clears them or takes them away).
/// </summary>
public sealed class EditSession(IEditableDocument document)
{
    /// <summary>A handling: the changes it made in the document, and the cursor and the selection's anchor before and after it.</summary>
    sealed record Step(int Changes, TextPlace CaretBefore, TextPlace AnchorBefore, TextPlace CaretAfter, TextPlace AnchorAfter, bool Typing);

    readonly List<Step> done = [], undone = [];

    /// <summary>How many steps were done when the document was saved (or opened); -1 where that state cannot come back.</summary>
    int saved;

    /// <summary>Whether the cursor was placed since the last typing, which typing then does not add to.</summary>
    bool placed = true;

    public IEditableDocument Document => document;

    /// <summary>The cursor, and where the selection began (the cursor itself without a selection).</summary>
    public TextPlace Caret { get; private set; }
    public TextPlace Anchor { get; private set; }

    /// <summary>Whether text is selected.</summary>
    public bool Selecting => Caret != Anchor;

    /// <summary>
    /// The character formatting text typed at the cursor takes, given where no text was selected and the cursor stood in no
    /// word (see Format); null where none is. Placing the cursor forgets it, as undoing and doing again do; typing,
    /// Backspace and Delete keep it (measured in Word 365 by COM: text typed, taken back and typed again takes it).
    /// </summary>
    public TextFormatting? Pending { get; private set; }

    /// <summary>
    /// The selected text as plain text: the stretches of its paragraphs, a line feed ("\r\n") between them, objects (the
    /// paragraph texts' U+FFFC) left out; empty where nothing is selected.
    /// </summary>
    public string Selected =>
        Selecting ? string.Join("\r\n", Stretches().Select(s => document.Text(s.Paragraph)[s.From..s.To].Replace("\uFFFC", "", StringComparison.Ordinal))) : "";

    /// <summary>Whether the document differs from how it was saved or opened.</summary>
    public bool Changed => saved != done.Count;

    /// <summary>
    /// The selection copied: its text (see Selected) and, where the document keeps it, its markup for pasting it with its
    /// formatting (see IEditableDocument.Copy); empty where nothing is selected.
    /// </summary>
    public Fragment Copy()
    {
        if (!Selecting)
            return new Fragment("", null);
        var (start, end, _) = Ordered();
        return new Fragment(Selected, document.Copy(start.Paragraph, start.Offset, end.Paragraph, end.Offset), document);
    }

    /// <summary>
    /// Pastes what was copied at the cursor in place of the selection, a handling of its own: its markup with its
    /// formatting where it has some of this document's and the document takes it there (see IEditableDocument.Paste),
    /// else its text (see Paste(string)).
    /// </summary>
    public void Paste(Fragment fragment)
    {
        if (fragment.Markup is { } markup && ReferenceEquals(fragment.From, document))
        {
            try
            {
                Act(false, () =>
                {
                    var at = DeleteSelection(typing: true);
                    var before = document.Text(at.Paragraph);
                    var (paragraph, offset) = document.Paste(at.Paragraph, at.Offset, markup);
                    // The paragraphs it went into read with its text there (objects aside), as text pasted must (see
                    // IEditableDocument.Replace): else it went into what the document computes (a field's result).
                    static string Plain(string text) => text.Replace("\uFFFC", "", StringComparison.Ordinal);
                    var lines = fragment.Text.Split("\r\n");
                    var reads = lines.Length == 1 ? Plain(document.Text(paragraph)) == Plain(before[..at.Offset] + lines[0] + before[at.Offset..])
                        : Plain(document.Text(paragraph)) == Plain(lines[^1] + before[at.Offset..]);
                    // Of more, the paragraphs before the cursor's back to the one the first went into.
                    for (int? p = paragraph, k = lines.Length - 2; reads && k >= 0; k--)
                    {
                        p = document.Before(p!.Value);
                        reads = p != null && Plain(document.Text(p.Value)) == Plain(k == 0 ? before[..at.Offset] + lines[0] : lines[k.Value]);
                    }
                    if (!reads)
                        throw new NotSupportedException("The pasted would not read so: it went into what the document computes.");
                    return Same(new TextPlace(paragraph, offset));
                });
                return;
            }
            catch (NotSupportedException)
            {
                // Taken back (see Act): the text goes in instead.
            }
        }
        Paste(fragment.Text);
    }

    public bool CanUndo => done.Count > 0;
    public bool CanRedo => undone.Count > 0;

    /// <summary>
    /// Puts the cursor at a place, within its paragraph's text and not between the halves of a character (a surrogate
    /// pair); <paramref name="extend"/> keeps the selection's anchor (Shift).
    /// </summary>
    public void Place(TextPlace caret, bool extend = false)
    {
        if (document.Contains(caret.Paragraph))
        {
            var text = document.Text(caret.Paragraph);
            var offset = Math.Clamp(caret.Offset, 0, text.Length);
            if (offset > 0 && offset < text.Length && char.IsLowSurrogate(text[offset]) && char.IsHighSurrogate(text[offset - 1]))
                offset--;
            caret = caret with { Offset = offset };
        }
        Caret = caret;
        if (!extend)
            Anchor = caret;
        placed = true;
        Pending = null;
    }

    /// <summary>
    /// Selects all the text of the story the cursor stands in, in reading order (see IEditableDocument.Next): a Word
    /// document's body with its tables, from a table's cell too (as Word's Ctrl+A), a text box's text, a shape's.
    /// </summary>
    public void SelectAll()
    {
        if (!document.Contains(Caret.Paragraph))
            return;
        var (first, last) = (Caret.Paragraph, Caret.Paragraph);
        while (document.Previous(first) is { } before)
            first = before;
        while (document.Next(last) is { } after)
            last = after;
        Place(new TextPlace(first, 0));
        Place(new TextPlace(last, document.Text(last).Length), extend: true);
    }

    /// <summary>
    /// Types text at the cursor in place of the selection, in the format given for typing (Pending); line feeds and returns
    /// in it split paragraphs, tabs and line breaks ("\t", "\v") stand in them.
    /// </summary>
    public void Type(string text) => Put(text, typing: true);

    /// <summary>
    /// Pastes text at the cursor in place of the selection, as Type puts it, a handling of its own: what it holds but text,
    /// line feeds, tabs and breaks left out (other control characters and U+FFFC, which stands for an object; page and
    /// column breaks as the document has them, see IEditableDocument.Replace).
    /// </summary>
    public void Paste(string text) =>
        Put(new string([.. text.Where(c => (!char.IsControl(c) || c is '\n' or '\r' or '\t' or '\v' or '\f' or '\u000E') && c != '\uFFFC')]), typing: false);

    void Put(string text, bool typing)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        Act(typing && lines.Length == 1 && !Selecting, () =>
        {
            var at = DeleteSelection(typing: true);
            for (var k = 0; k < lines.Length; k++)
            {
                if (k > 0)
                    at = new TextPlace(document.Split(at.Paragraph, at.Offset), 0);
                if (lines[k].Length > 0)
                {
                    document.Replace(at.Paragraph, at.Offset, 0, lines[k]);
                    if (Pending is { } format)
                        document.Format(at.Paragraph, at.Offset, lines[k].Length, format);
                    at = at with { Offset = at.Offset + lines[k].Length };
                }
            }
            return (at, at);
        });
    }

    /// <summary>Splits the paragraph at the cursor in place of the selection (Enter).</summary>
    public void Enter() => Type("\n");

    /// <summary>Breaks the line at the cursor in place of the selection, within its paragraph (Shift+Enter).</summary>
    public void LineBreak() => Type("\v");

    /// <summary>
    /// Puts markup of the document's own kind at the cursor in place of the selection, a handling of its own (see
    /// IEditableDocument.InsertMarkup): a picture, a chart, a shape or WordArt, an equation, a field, with the parts it
    /// needs; the cursor then after it. Returns the ids of the paragraphs it brought (its text boxes'), to type into.
    /// </summary>
    public IReadOnlyList<int> InsertMarkup(string markup, IReadOnlyList<NewPart>? parts = null)
    {
        IReadOnlyList<int> made = [];
        Act(false, () =>
        {
            var at = DeleteSelection(typing: false);
            var length = document.Text(at.Paragraph).Length;
            made = document.InsertMarkup(at.Paragraph, at.Offset, markup, parts);
            return Same(at with { Offset = at.Offset + document.Text(at.Paragraph).Length - length });
        });
        return made;
    }

    /// <summary>
    /// Puts blocks of the document's own kind after the cursor's paragraph, or before it (see
    /// IEditableDocument.InsertBlocks), a handling of its own: paragraphs, tables, a section's end. The cursor then stands
    /// at the start of the first paragraph they hold; returns their ids in reading order (a table's cell by cell), to type
    /// into.
    /// </summary>
    public IReadOnlyList<int> InsertBlocks(string markup, IReadOnlyList<NewPart>? parts = null, bool before = false)
    {
        IReadOnlyList<int> made = [];
        Act(false, () =>
        {
            made = document.InsertBlocks(Caret.Paragraph, markup, before, parts);
            return Same(made.Count > 0 ? new TextPlace(made[0], 0) : Caret);
        });
        return made;
    }

    /// <summary>
    /// Puts a footnote (an endnote) at the cursor in place of the selection, a handling of its own (see
    /// IEditableDocument.InsertNote): its text given as markup of runs; the cursor then after its reference.
    /// </summary>
    public void InsertNote(string markup, bool endnote = false) => Act(false, () =>
    {
        var at = DeleteSelection(typing: false);
        var length = document.Text(at.Paragraph).Length;
        document.InsertNote(at.Paragraph, at.Offset, markup, endnote);
        return Same(at with { Offset = at.Offset + document.Text(at.Paragraph).Length - length });
    });

    /// <summary>Takes the selection away, or the character before the cursor; at a paragraph's start joins it with the one before.</summary>
    public void Backspace() => Act(false, () =>
    {
        if (Selecting)
            return Same(DeleteSelection(typing: false));
        var (paragraph, offset) = Caret;
        if (offset > 0)
        {
            var length = Before(document.Text(paragraph), offset);
            document.Replace(paragraph, offset - length, length, "");
            return Same(new TextPlace(paragraph, offset - length));
        }
        if (document.Before(paragraph) is not { } before)
            return (Caret, Anchor);
        var end = document.Text(before).Length;
        var joined = document.Join(before, backward: true);
        return Same(new TextPlace(joined, joined == before ? end : 0));
    });

    /// <summary>Takes the selection away, or the character after the cursor; at a paragraph's end joins it with the one after.</summary>
    public void Delete() => Act(false, () =>
    {
        if (Selecting)
            return Same(DeleteSelection(typing: false));
        var (paragraph, offset) = Caret;
        var text = document.Text(paragraph);
        if (offset < text.Length)
        {
            document.Replace(paragraph, offset, After(text, offset), "");
            return (Caret, Anchor);
        }
        if (document.After(paragraph) is null)
        {
            // A table after the paragraph (measured in Word 365 by COM): an empty paragraph goes, another goes on at the
            // beginning of the table's first cell; at the end of a cell or story nothing happens.
            if (document.Next(paragraph) is { } next)
            {
                try
                {
                    if (document.RemoveAcross(paragraph, offset, next, 0, typing: false) is { } across)
                        return Same(new TextPlace(across.Paragraph, across.Offset));
                }
                catch (NotSupportedException)
                {
                }
            }
            return (Caret, Anchor);
        }
        var joined = document.Join(paragraph);
        return Same(new TextPlace(joined, joined == paragraph ? offset : 0));
    });

    /// <summary>
    /// Gives the selected text character formatting, the selection staying; without a selection, as Word's and
    /// PowerPoint's buttons do (measured in Word 365 and PowerPoint 365 by COM): the word the cursor stands inside (see
    /// IEditableDocument.WordAt), else the text typed next there (Pending, with what was given for it before).
    /// </summary>
    public void Format(TextFormatting format)
    {
        if (!Selecting)
        {
            if ((document.Contains(Caret.Paragraph) ? document.WordAt(Caret.Paragraph, Caret.Offset) : null) is not var (offset, length))
            {
                Pending = Pending is { } before ? Merged(before, format) : format;
                return;
            }
            Act(false, () =>
            {
                document.Format(Caret.Paragraph, offset, length, format);
                return (Caret, Anchor);
            });
            return;
        }
        Act(false, () =>
        {
            foreach (var (paragraph, from, to) in Stretches())
            {
                if (to > from)
                    document.Format(paragraph, from, to - from, format);
            }
            return (Caret, Anchor);
        });
    }

    /// <summary>A formatting with the properties another gives in place of its own.</summary>
    static TextFormatting Merged(TextFormatting format, TextFormatting over) => new()
    {
        Bold = over.Bold ?? format.Bold,
        Italic = over.Italic ?? format.Italic,
        Underline = over.Underline ?? format.Underline,
        Size = over.Size ?? format.Size,
        Font = over.Font ?? format.Font,
        Color = over.Color ?? format.Color,
        Highlight = over.Highlight ?? format.Highlight,
    };

    /// <summary>Gives the paragraphs of the selection, or the cursor's, paragraph formatting.</summary>
    public void Format(ParagraphFormatting format) => Act(false, () =>
    {
        foreach (var (paragraph, _, _) in Stretches())
            document.Format(paragraph, format);
        return (Caret, Anchor);
    });

    /// <summary>Undoes the last step done, the cursor and the selection back where they were before it; false where there is none.</summary>
    public bool Undo()
    {
        if (done.Count == 0)
            return false;
        var step = done[^1];
        for (var k = 0; k < step.Changes; k++)
            document.Undo();
        done.RemoveAt(done.Count - 1);
        undone.Add(step);
        (Caret, Anchor, placed, Pending) = (step.CaretBefore, step.AnchorBefore, true, null);
        return true;
    }

    /// <summary>Does the last step undone again, the cursor and the selection where it left them; false where there is none.</summary>
    public bool Redo()
    {
        if (undone.Count == 0)
            return false;
        var step = undone[^1];
        for (var k = 0; k < step.Changes; k++)
            document.Redo();
        undone.RemoveAt(undone.Count - 1);
        done.Add(step);
        (Caret, Anchor, placed, Pending) = (step.CaretAfter, step.AnchorAfter, true, null);
        return true;
    }

    /// <summary>Saves the document (see IEditableDocument.Save); it counts as unchanged from then on.</summary>
    public void Save(string target)
    {
        document.Save(target);
        saved = done.Count;
    }

    /// <summary>
    /// A handling: <paramref name="change"/> makes its changes in the document and gives the cursor and anchor after it. A
    /// step where it changed something (typing added to the last step where it goes on from it); where the document
    /// refuses a change, the changes made so far are undone and the refusal goes on to the caller.
    /// </summary>
    void Act(bool typing, Func<(TextPlace Caret, TextPlace Anchor)> change)
    {
        if (!document.Contains(Caret.Paragraph) || !document.Contains(Anchor.Paragraph))
            throw new NotSupportedException("The place is in text the document does not change (a comment).");
        var start = document.Changes;
        var couldRedo = document.CanRedo;
        var (caretBefore, anchorBefore) = (Caret, Anchor);
        (TextPlace Caret, TextPlace Anchor) after;
        try
        {
            after = change();
        }
        catch
        {
            // A change made before the refusal took the document's own history of what was undone; it is taken back.
            if (document.Changes > start || document.CanRedo != couldRedo)
            {
                undone.Clear();
                if (saved > done.Count)
                    saved = -1;
            }
            while (document.Changes > start)
                document.Undo();
            throw;
        }
        var changes = document.Changes - start;
        if (changes == 0)
            return;
        (Caret, Anchor) = after;
        if (saved > done.Count)
            saved = -1;   // the saved state was undone, and now another goes on from there
        undone.Clear();
        if (typing && !placed && done.Count > 0 && done[^1] is { Typing: true } last && last.CaretAfter == caretBefore && done.Count != saved)
            done[^1] = last with { Changes = last.Changes + changes, CaretAfter = after.Caret, AnchorAfter = after.Anchor };
        else
            done.Add(new Step(changes, caretBefore, anchorBefore, after.Caret, after.Anchor, typing));
        placed = !typing;
    }

    /// <summary>
    /// Takes the selection away (in a handling), as the application deletes it or types over it (see
    /// IEditableDocument.KeepsMarks; measured in Word 365 and PowerPoint 365 by COM): its text in the paragraph it begins
    /// in, the paragraphs and whole tables between and the text of the paragraph it ends in, the two then joined (in the
    /// first's format, an empty first going away), a space between them where it began at the first's end and both texts
    /// meet without one. Where the document keeps marks, deleting keeps a first paragraph the selection began inside
    /// apart from the last (but where a table stands just before the last, or changes are tracked: see tracked below),
    /// typing over a selection that ends where a paragraph begins keeps the mark before it, and deleting whole words
    /// tidies the spaces where they went (see Tidied); a mark that joins paragraphs into one (hidden or deleted) counts
    /// as a paragraph's own (see Separated). Returns where the cursor then stands.
    /// </summary>
    TextPlace DeleteSelection(bool typing)
    {
        if (!Selecting)
            return Caret;
        var (start, end, between) = Ordered();
        (start, end, between) = Separated(start, end, between);
        var (first, final) = (document.Text(start.Paragraph), document.Text(end.Paragraph));
        var (fromWord, toWord) = (start.Offset < first.Length && char.IsLetter(first[start.Offset]) && (start.Offset == 0 || !char.IsLetterOrDigit(first[start.Offset - 1])),
            end.Offset > 0 && char.IsLetter(final[end.Offset - 1]) && (end.Offset == final.Length || !char.IsLetterOrDigit(final[end.Offset])));
        var tidy = !typing && document.KeepsMarks;
        // With changes tracked Word deletes the marks the selection covers, the first paragraph not staying apart (see apart
        // below), but where it begins inside the first and ends where a paragraph begins the mark before that one stays, as
        // typing over it keeps it (measured in Word 365 by COM, Selection.Delete and TypeBackspace: of "Erster |Absatz eins."
        // | "Zweiter |Absatz zwei." the selection between the bars deleted leaves the first's mark deleted and the second in
        // the first's format as a tracked change; up to the second's start, or a third's, the mark before it stays).
        var tracked = document.Tracks && document.KeepsMarks;
        // (An empty last paragraph is not where the selection begins one: it ends at its end too, as Ctrl+A does.)
        if ((typing || tracked && start.Offset > 0) && document.KeepsMarks && start.Paragraph != end.Paragraph && end.Offset == 0
            && document.Text(end.Paragraph).Length > 0 && document.Before(end.Paragraph) is { } kept)
        {
            end = new TextPlace(kept, document.Text(kept).Length);
            between = kept == start.Paragraph ? [] : between[..^1];
        }
        if (start.Paragraph == end.Paragraph)
        {
            document.Replace(start.Paragraph, start.Offset, end.Offset - start.Offset, "");
            return tidy && fromWord && toWord ? Tidied(start) : start;
        }
        // Its ends not of one body, cell or text box (one in a table the other is not in): Word takes the table's rows the
        // selection reaches into away whole (from one into the paragraph after the table, from the row's beginning).
        if (document.RemoveAcross(start.Paragraph, start.Offset, end.Paragraph, end.Offset, typing) is { } across)
        {
            var place = new TextPlace(across.Paragraph, across.Offset);
            return tidy && (fromWord || place.Paragraph == end.Paragraph && place.Offset == 0) && toWord ? Tidied(place) : place;
        }
        var length = document.Text(start.Paragraph).Length;
        var apart = !typing && !tracked && document.KeepsMarks && start.Offset > 0 && start.Offset < length && !(end.Offset == 0 && document.Before(end.Paragraph) == null);
        var spaced = !typing && start.Offset == length && length > 0 && (end.Offset > 0 || between.Count > 0);
        if (end.Offset > 0)
            document.Replace(end.Paragraph, 0, end.Offset, "");
        // What stands between them, whole tables too (the document refuses where the ends are not of one body or cell).
        if (between.Count > 0)
            document.RemoveBetween(start.Paragraph, end.Paragraph);
        // The last paragraph now follows the first (by place: read anew, it may have taken another id).
        var rest = document.After(start.Paragraph) is { } last ? document.Text(last) : "";
        TextPlace at;
        if (!document.KeepsMarks && length > 0)
        {
            // PowerPoint joins the rest into the first paragraph, which keeps its format where all its text goes too.
            var into = document.Join(start.Paragraph);
            if (length > start.Offset)
                document.Replace(into, start.Offset, length - start.Offset, "");
            at = new TextPlace(into, start.Offset);
        }
        else
        {
            if (length > start.Offset)
                document.Replace(start.Paragraph, start.Offset, length - start.Offset, "");
            if (apart)
            {
                if (tidy && fromWord && toWord && document.After(start.Paragraph) is { } following)
                    Tidied(new TextPlace(following, 0));
                return start;
            }
            var joined = document.Join(start.Paragraph);
            at = new TextPlace(joined, joined == start.Paragraph ? start.Offset : 0);
        }
        if (spaced && rest.Length > 0 && !char.IsWhiteSpace(rest[0]) && at.Offset > 0 && !char.IsWhiteSpace(document.Text(at.Paragraph)[at.Offset - 1]))
            document.Replace(at.Paragraph, at.Offset, 0, " ");
        return tidy && fromWord && toWord ? Tidied(at) : at;
    }

    /// <summary>
    /// The spaces where deleted whole words went tidied, as Word's smart cut does (Options.SmartCutPaste, on by default;
    /// measured in Word 365 by COM, WordBasic.EditClear and Selection.TypeBackspace alike, the selection beginning at a
    /// word's first letter and ending at a word's last): where the text after the place begins with a space and the text
    /// before it ends with one, or the paragraph begins there, that space goes; where the text before ends with a space
    /// and the text after begins with a punctuation mark (. , ; : ! ?), the space before goes; a space that ends a
    /// paragraph stays. A space the document does not change so (one it computes) stays too. Returns where the cursor then
    /// stands.
    /// </summary>
    TextPlace Tidied(TextPlace at)
    {
        var text = document.Text(at.Paragraph);
        // (A paragraph's beginning and end as a line feed, which its text does not hold.)
        var (before, after) = (at.Offset > 0 ? text[at.Offset - 1] : '\n', at.Offset < text.Length ? text[at.Offset] : '\n');
        try
        {
            if (after == ' ' && before is ' ' or '\n')
                document.Replace(at.Paragraph, at.Offset, 1, "");
            else if (before == ' ' && after is '.' or ',' or ';' or ':' or '!' or '?')
            {
                document.Replace(at.Paragraph, at.Offset - 1, 1, "");
                return at with { Offset = at.Offset - 1 };
            }
        }
        catch (NotSupportedException)
        {
        }
        return at;
    }

    /// <summary>
    /// A selection over a place where paragraphs the document reads as one meet (in Word a mark hidden or deleted between
    /// them, see IEditableDocument.Seams), those paragraphs brought apart there (see IEditableDocument.Separate): Word
    /// deletes and types over such a mark as over a paragraph's own (measured in Word 365 by COM, hidden text not shown and
    /// the final view, without tracking: of "Erster T|eil eins." with a hidden mark before "Zweiter T|eil zwei.", right
    /// aligned, the selection between the bars deleted leaves "Erster T" apart with its mark shown and "eil zwei." right
    /// aligned, typed over "Erster TXXeil zwei." in the first's format, as of two paragraphs of their own; so over a
    /// deleted mark, from a paragraph before into the second, from the first into one after, over two such paragraphs;
    /// from the place they meet or up to it the mark stays as it is). The places within the selection's first and last
    /// paragraph count, those between go whole. Where the document does not bring them apart (it tracks its changes: Word
    /// deletes the mark as a tracked change) the selection stays as it was.
    /// </summary>
    (TextPlace Start, TextPlace End, List<int> Between) Separated(TextPlace start, TextPlace end, List<int> between)
    {
        try
        {
            // In the first paragraph at the first place after the selection's beginning (and before its end, where it
            // ends there too): the rest a paragraph between, or the one it ends in.
            if (document.Seams(start.Paragraph).FirstOrDefault(s => s > start.Offset, -1) is var seam and > 0
                && (start.Paragraph != end.Paragraph || seam < end.Offset))
            {
                var rest = document.Separate(start.Paragraph, seam);
                if (start.Paragraph == end.Paragraph)
                    end = new TextPlace(rest, end.Offset - seam);
                else
                    between = [rest, .. between];
            }
            // In the last at each place before the selection's end, what stands before it a paragraph between.
            while (end.Paragraph != start.Paragraph && document.Seams(end.Paragraph).FirstOrDefault(s => s > 0 && s < end.Offset, -1) is var last and > 0)
            {
                var rest = document.Separate(end.Paragraph, last);
                between = [.. between, end.Paragraph];
                end = new TextPlace(rest, end.Offset - last);
            }
        }
        catch (NotSupportedException)
        {
        }
        return (start, end, between);
    }

    /// <summary>The selection's paragraphs with the stretch of each it covers, or the cursor's paragraph where nothing is selected.</summary>
    List<(int Paragraph, int From, int To)> Stretches()
    {
        if (!Selecting)
            return [(Caret.Paragraph, Caret.Offset, Caret.Offset)];
        var (start, end, between) = Ordered();
        if (start.Paragraph == end.Paragraph)
            return [(start.Paragraph, start.Offset, end.Offset)];
        return [(start.Paragraph, start.Offset, document.Text(start.Paragraph).Length), .. between.Select(p => (p, 0, document.Text(p).Length)), (end.Paragraph, 0, end.Offset)];
    }

    /// <summary>
    /// The selection's ends in reading order (see IEditableDocument.Next) and the paragraphs between them: walked from
    /// both ends at once, so that a selection made backwards is not walked to the story's end first.
    /// </summary>
    (TextPlace Start, TextPlace End, List<int> Between) Ordered()
    {
        if (Caret.Paragraph == Anchor.Paragraph)
            return Caret.Offset <= Anchor.Offset ? (Caret, Anchor, []) : (Anchor, Caret, []);
        var (fromAnchor, fromCaret) = (new List<int>(), new List<int>());
        int? a = Anchor.Paragraph, c = Caret.Paragraph;
        while (a != null || c != null)
        {
            if (a != null && (a = document.Next(a.Value)) is { } nextA)
            {
                if (nextA == Caret.Paragraph)
                    return (Anchor, Caret, fromAnchor);
                fromAnchor.Add(nextA);
            }
            if (c != null && (c = document.Next(c.Value)) is { } nextC)
            {
                if (nextC == Anchor.Paragraph)
                    return (Caret, Anchor, fromCaret);
                fromCaret.Add(nextC);
            }
        }
        throw new NotSupportedException("The selection's ends are not of one story (the body, a note, a header or footer, a text box, a shape).");
    }

    static (TextPlace, TextPlace) Same(TextPlace place) => (place, place);

    /// <summary>How many UTF-16 units the character before or after an offset takes (two for a surrogate pair).</summary>
    static int Before(string text, int offset) => offset >= 2 && char.IsLowSurrogate(text[offset - 1]) && char.IsHighSurrogate(text[offset - 2]) ? 2 : 1;
    static int After(string text, int offset) => offset + 1 < text.Length && char.IsHighSurrogate(text[offset]) && char.IsLowSurrogate(text[offset + 1]) ? 2 : 1;
}
