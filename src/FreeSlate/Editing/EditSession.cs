namespace FreeSlate.Editing;

/// <summary>A place in a document's text: a paragraph by its id and an offset in its text (see IEditableDocument).</summary>
public readonly record struct TextPlace(int Paragraph, int Offset);

/// <summary>
/// An editor's work on a document without its surface: the cursor and the selection as places in its text, and what an
/// editor's keys do there, as a word processor's do. Typing replaces the selection; Backspace and Delete take the
/// selection or the character before or after the cursor away, at a paragraph's edge joining it with the one beside it;
/// Enter splits the paragraph; formats go to the selection's text. Each handling is one step to undo and do again, the
/// changes it made in the document undone together; typing on from where the last typing ended adds to it. A handling
/// the document cannot make (see IEditableDocument) leaves it as it was and throws NotSupportedException. A selection
/// reaches over paragraphs of one body, cell, text box or shape.
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
    }

    /// <summary>Types text at the cursor in place of the selection; line feeds and returns in it split paragraphs.</summary>
    public void Type(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        Act(lines.Length == 1 && !Selecting, () =>
        {
            var at = DeleteSelection();
            for (var k = 0; k < lines.Length; k++)
            {
                if (k > 0)
                    at = new TextPlace(document.Split(at.Paragraph, at.Offset), 0);
                if (lines[k].Length > 0)
                {
                    document.Replace(at.Paragraph, at.Offset, 0, lines[k]);
                    at = at with { Offset = at.Offset + lines[k].Length };
                }
            }
            return (at, at);
        });
    }

    /// <summary>Splits the paragraph at the cursor in place of the selection (Enter).</summary>
    public void Enter() => Type("\n");

    /// <summary>Takes the selection away, or the character before the cursor; at a paragraph's start joins it with the one before.</summary>
    public void Backspace() => Act(false, () =>
    {
        if (Selecting)
            return Same(DeleteSelection());
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
        var joined = document.Join(before);
        return Same(new TextPlace(joined, joined == before ? end : 0));
    });

    /// <summary>Takes the selection away, or the character after the cursor; at a paragraph's end joins it with the one after.</summary>
    public void Delete() => Act(false, () =>
    {
        if (Selecting)
            return Same(DeleteSelection());
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

    /// <summary>Gives the selected text character formatting; the selection stays.</summary>
    public void Format(TextFormatting format) => Act(false, () =>
    {
        foreach (var (paragraph, from, to) in Stretches())
        {
            if (to > from)
                document.Format(paragraph, from, to - from, format);
        }
        return (Caret, Anchor);
    });

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
        (Caret, Anchor, placed) = (step.CaretBefore, step.AnchorBefore, true);
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
        (Caret, Anchor, placed) = (step.CaretAfter, step.AnchorAfter, true);
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

    /// <summary>Takes the selection away (in a handling); returns where the cursor then stands.</summary>
    TextPlace DeleteSelection()
    {
        if (!Selecting)
            return Caret;
        var (start, end, between) = Ordered();
        if (start.Paragraph == end.Paragraph)
        {
            document.Replace(start.Paragraph, start.Offset, end.Offset - start.Offset, "");
            return start;
        }
        if (end.Offset > 0)
            document.Replace(end.Paragraph, 0, end.Offset, "");
        for (var k = between.Count - 1; k >= 0; k--)
            document.Remove(between[k]);
        var length = document.Text(start.Paragraph).Length;
        if (length > start.Offset)
            document.Replace(start.Paragraph, start.Offset, length - start.Offset, "");
        var joined = document.Join(start.Paragraph);
        return new TextPlace(joined, joined == start.Paragraph ? start.Offset : 0);
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

    /// <summary>The selection's ends in reading order and the paragraphs between them.</summary>
    (TextPlace Start, TextPlace End, List<int> Between) Ordered()
    {
        if (Caret.Paragraph == Anchor.Paragraph)
            return Caret.Offset <= Anchor.Offset ? (Caret, Anchor, []) : (Anchor, Caret, []);
        foreach (var (from, to) in new[] { (Anchor, Caret), (Caret, Anchor) })
        {
            var between = new List<int>();
            for (var p = document.After(from.Paragraph); p != null; p = document.After(p.Value))
            {
                if (p == to.Paragraph)
                    return (from, to, between);
                between.Add(p.Value);
            }
        }
        throw new NotSupportedException("The selection reaches over its body, table cell, text box or shape.");
    }

    static (TextPlace, TextPlace) Same(TextPlace place) => (place, place);

    /// <summary>How many UTF-16 units the character before or after an offset takes (two for a surrogate pair).</summary>
    static int Before(string text, int offset) => offset >= 2 && char.IsLowSurrogate(text[offset - 1]) && char.IsHighSurrogate(text[offset - 2]) ? 2 : 1;
    static int After(string text, int offset) => offset + 1 < text.Length && char.IsHighSurrogate(text[offset]) && char.IsLowSurrogate(text[offset + 1]) ? 2 : 1;
}
