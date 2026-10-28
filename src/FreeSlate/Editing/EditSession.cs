namespace FreeSlate.Editing;

/// <summary>A place in a document's text: a paragraph by its id and an offset in its text (see IEditableDocument).</summary>
public readonly record struct TextPlace(int Paragraph, int Offset);

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
/// one body, cell or text box (as Word does), and is refused where they do not (where Word takes rows away or empties
/// cells).
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
    /// Puts blocks of the document's own kind after the cursor's paragraph (see IEditableDocument.InsertBlocks), a
    /// handling of its own: paragraphs, tables, a section's end. The cursor then stands at the start of the first
    /// paragraph they hold; returns their ids in reading order (a table's cell by cell), to type into.
    /// </summary>
    public IReadOnlyList<int> InsertBlocks(string markup, IReadOnlyList<NewPart>? parts = null)
    {
        IReadOnlyList<int> made = [];
        Act(false, () =>
        {
            made = document.InsertBlocks(Caret.Paragraph, markup, parts: parts);
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
            return (Caret, Anchor);
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
            throw new NotSupportedException("The place is in text the document does not change (a header, footer, note or comment).");
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
    /// apart from the last (but where a table stands just before the last), and typing over a selection that ends where
    /// a paragraph begins keeps the mark before it. Returns where the cursor then stands.
    /// </summary>
    TextPlace DeleteSelection(bool typing)
    {
        if (!Selecting)
            return Caret;
        var (start, end, between) = Ordered();
        // (An empty last paragraph is not where the selection begins one: it ends at its end too, as Ctrl+A does.)
        if (typing && document.KeepsMarks && start.Paragraph != end.Paragraph && end.Offset == 0 && document.Text(end.Paragraph).Length > 0
            && document.Before(end.Paragraph) is { } kept)
        {
            end = new TextPlace(kept, document.Text(kept).Length);
            between = kept == start.Paragraph ? [] : between[..^1];
        }
        if (start.Paragraph == end.Paragraph)
        {
            document.Replace(start.Paragraph, start.Offset, end.Offset - start.Offset, "");
            return start;
        }
        // Its ends of one body, cell or text box: where they are not, Word takes rows away or empties cells.
        if (between.Count == 0 && document.After(start.Paragraph) != end.Paragraph)
            throw new NotSupportedException("The selection's ends are not of one body, cell or text box (one stands in a table the other does not).");
        var length = document.Text(start.Paragraph).Length;
        var apart = !typing && document.KeepsMarks && start.Offset > 0 && start.Offset < length && !(end.Offset == 0 && document.Before(end.Paragraph) == null);
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
                return start;
            var joined = document.Join(start.Paragraph);
            at = new TextPlace(joined, joined == start.Paragraph ? start.Offset : 0);
        }
        if (spaced && rest.Length > 0 && !char.IsWhiteSpace(rest[0]) && at.Offset > 0 && !char.IsWhiteSpace(document.Text(at.Paragraph)[at.Offset - 1]))
            document.Replace(at.Paragraph, at.Offset, 0, " ");
        return at;
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
        throw new NotSupportedException("The selection's ends are not of one story (the body, a text box, a shape).");
    }

    static (TextPlace, TextPlace) Same(TextPlace place) => (place, place);

    /// <summary>How many UTF-16 units the character before or after an offset takes (two for a surrogate pair).</summary>
    static int Before(string text, int offset) => offset >= 2 && char.IsLowSurrogate(text[offset - 1]) && char.IsHighSurrogate(text[offset - 2]) ? 2 : 1;
    static int After(string text, int offset) => offset + 1 < text.Length && char.IsHighSurrogate(text[offset]) && char.IsLowSurrogate(text[offset + 1]) ? 2 : 1;
}
