namespace FreeSlate.Editing;

/// <summary>A place in a document's text: a paragraph by its id and an offset in its text (see IEditableDocument).</summary>
public readonly record struct TextPlace(int Paragraph, int Offset);

/// <summary>
/// A selection copied (see EditSession.Copy): its text, as the system's clipboard takes it, and where the document keeps
/// more, its paragraphs in the document's own markup for pasting them with their formatting into it (see
/// IEditableDocument.Copy), the document they are of (<paramref name="From"/>: another takes the text); or, where it was
/// cells of a table (<paramref name="Cells"/>), the text as a spreadsheet's rows and the cells as a table of their own (see
/// WordDocument.CopyCells).
/// </summary>
public sealed record Fragment(string Text, IReadOnlyList<string>? Markup, IEditableDocument? From = null, bool Cells = false);

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
public sealed partial class EditSession(IEditableDocument document)
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

    /// <summary>The selection in reading order as it stands: over the links and fields it holds one end of (see IEditableDocument.OverFields), which an editor shows.</summary>
    public (TextPlace Start, TextPlace End) Selection => Ordered() is var (start, end, _) ? (start, end) : default;

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
        if (CopiedCells() is { } cells)
            return cells;
        var (start, end, _) = Ordered();
        return new Fragment(Selected, document.Copy(start.Paragraph, start.Offset, end.Paragraph, end.Offset), document);
    }

    /// <summary>
    /// Pastes what was copied at the cursor in place of the selection, a handling of its own: its markup with its
    /// formatting where it has some of this document's and the document takes it there (see IEditableDocument.Paste),
    /// else its text (see Paste(string)); cells copied over the cells from the cursor's or the selected ones' on, outside
    /// tables as a table (see PasteCells).
    /// </summary>
    public void Paste(Fragment fragment)
    {
        if (fragment.Cells && PastedCells(fragment))
            return;
        if (fragment.Markup is { } markup && ReferenceEquals(fragment.From, document) && !fragment.Cells)
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

    /// <summary>How many steps there are to undo.</summary>
    public int Steps => done.Count;

    /// <summary>
    /// The steps done since there were so many made one (handlings an editor makes one, as a tilted picture shown flat while it is cropped
    /// and tilted again), undone and done again together, the cursor and the selection as before the first and after the last; nothing
    /// where fewer than two came since.
    /// </summary>
    public void JoinSince(int steps)
    {
        if (steps < 0 || done.Count - steps < 2)
            return;
        var joined = done.GetRange(steps, done.Count - steps);
        done.RemoveRange(steps, joined.Count);
        done.Add(new Step(Combined(joined.Sum(s => s.Changes)), joined[0].CaretBefore, joined[0].AnchorBefore, joined[^1].CaretAfter, joined[^1].AnchorAfter, false));
        // Saved between them, that state cannot come back.
        if (saved > steps)
            saved = saved == steps + joined.Count ? steps + 1 : -1;
    }

    /// <summary>
    /// Puts the cursor at a place, within its paragraph's text, not between the halves of a character (a surrogate pair) nor
    /// within what a field computes (see IEditableDocument.Standing); <paramref name="extend"/> keeps the selection's anchor (Shift).
    /// </summary>
    public void Place(TextPlace caret, bool extend = false)
    {
        if (document.Contains(caret.Paragraph))
        {
            var text = document.Text(caret.Paragraph);
            var offset = Math.Clamp(caret.Offset, 0, text.Length);
            if (offset > 0 && offset < text.Length && char.IsLowSurrogate(text[offset]) && char.IsHighSurrogate(text[offset - 1]))
                offset--;
            caret = caret with { Offset = document.Standing(caret.Paragraph, offset) };
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
    /// Word's word a character stands in, which a double click on it selects (see Words.Unit): the place before the
    /// character given; at a paragraph's end the last word. Null in an empty paragraph or one the document does not change.
    /// </summary>
    public (TextPlace Start, TextPlace End)? WordAround(TextPlace character)
    {
        if (!document.Contains(character.Paragraph) || document.Text(character.Paragraph) is not { Length: > 0 } text)
            return null;
        var (start, end) = Words.Unit(text, Math.Clamp(character.Offset, 0, text.Length - 1));
        return (character with { Offset = start }, character with { Offset = end });
    }

    /// <summary>
    /// The paragraph a place stands in with its mark, which a triple click selects: up to the start of the paragraph after it in
    /// its body, cell or text box (see IEditableDocument.After), the last one's to its end. Typed over, the mark stays; Delete
    /// and Backspace take the paragraph away, as in Word (measured by COM, scratch/KeyProbe against keys2.vbs).
    /// </summary>
    public (TextPlace Start, TextPlace End)? ParagraphAround(TextPlace place) =>
        document.Contains(place.Paragraph)
            ? (place with { Offset = 0 }, document.After(place.Paragraph) is { } next ? new TextPlace(next, 0) : place with { Offset = document.Text(place.Paragraph).Length })
            : null;

    /// <summary>
    /// Selects over whole words or paragraphs from one to another, as dragging on after a double or triple click does: the start
    /// of the one held stays where the other lies after it, its end where the other lies before it.
    /// </summary>
    public void SelectOver((TextPlace Start, TextPlace End) held, (TextPlace Start, TextPlace End) other)
    {
        Place(held.Start);
        Place(other.End, extend: true);
        // The other lies before: the selection so made runs back, or is none where the other ends where the held one starts.
        if (other.Start != held.Start && (!Selecting || Plain().Start == Caret))
        {
            Place(held.End);
            Place(other.Start, extend: true);
        }
    }

    /// <summary>Where Ctrl with the arrows, Home and End takes the cursor (see Move).</summary>
    public enum Jump { WordLeft, WordRight, ParagraphUp, ParagraphDown, StoryStart, StoryEnd }

    /// <summary>
    /// Moves the cursor as Ctrl with an arrow, Home or End does in Word (measured in Word 365 by COM, WordBasic.WordLeft and
    /// its kin): <paramref name="extend"/>, the selection's moving end; without, from a selection's start back and from its end
    /// on. By words to the start of the unit before the cursor and the end of the one after it (see Words.Unit), over a
    /// paragraph's edge to the end of the one before and the start of the one after (in reading order, see
    /// IEditableDocument.Next); by paragraphs to the start of the cursor's, at its start to the one before's, and to the start of
    /// the one after, in the last one to its end; to the start and the end of the story.
    /// </summary>
    public void Move(Jump jump, bool extend)
    {
        if (!document.Contains(Caret.Paragraph))
            return;
        var back = jump is Jump.WordLeft or Jump.ParagraphUp or Jump.StoryStart;
        var (paragraph, offset) = extend || !Selecting ? Caret : back ? Selection.Start : Selection.End;
        var text = document.Text(paragraph);
        TextPlace Last(int p) => new(p, document.Text(p).Length);
        var to = jump switch
        {
            Jump.WordLeft => Words.Previous(text, offset) is { } start ? new TextPlace(paragraph, start)
                : document.Previous(paragraph) is { } before ? Last(before) : new TextPlace(paragraph, 0),
            Jump.WordRight => Words.Next(text, offset) is { } end ? new TextPlace(paragraph, end)
                : document.Next(paragraph) is { } after ? new TextPlace(after, 0) : Last(paragraph),
            Jump.ParagraphUp => offset == 0 && document.Previous(paragraph) is { } before ? new TextPlace(before, 0) : new TextPlace(paragraph, 0),
            Jump.ParagraphDown => document.Next(paragraph) is { } after ? new TextPlace(after, 0) : Last(paragraph),
            _ => Edge(paragraph, jump == Jump.StoryEnd),
        };
        Place(to, extend);
    }

    /// <summary>The first or the last place of the story a paragraph stands in, in reading order.</summary>
    TextPlace Edge(int paragraph, bool end)
    {
        while ((end ? document.Next(paragraph) : document.Previous(paragraph)) is { } further)
            paragraph = further;
        return new TextPlace(paragraph, end ? document.Text(paragraph).Length : 0);
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
    public void Paste(string text)
    {
        var kept = new string([.. text.Where(c => (!char.IsControl(c) || c is '\n' or '\r' or '\t' or '\v' or '\f' or '\u000E') && c != '\uFFFC')]);
        // A spreadsheet's rows go into a table's cells (see PastedTexts).
        if (kept.Contains('\t', StringComparison.Ordinal) && PastedTexts(kept))
            return;
        Put(kept, typing: false);
    }

    void Put(string text, bool typing)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        // Over cells selected the text takes the first one's place, the others keeping theirs (measured in Word 365 by COM, scratchpad
        // tables/looks.vbs: X typed over four cells stands alone in the first).
        if (CellSelection is { } cells && CellsIn(cells) is [var first, ..])
        {
            Place(new TextPlace(first.First, 0));
            Place(new TextPlace(first.Last, document.Text(first.Last).Length), extend: true);
        }
        ActKeepingPlaces(typing && lines.Length == 1 && !Selecting, () =>
        {
            // Typed into what a content control shows as its placeholder (the cursor in it, or a selection within it), the
            // text takes its whole place; over a selection reaching into one from outside, the selection's text around it
            // (see IEditableDocument.Placeholder).
            (int Start, int End)? shown = null;
            if (lines[0].Length > 0 && Caret.Paragraph == Anchor.Paragraph && document.Contains(Caret.Paragraph))
            {
                var (from, to) = !Selecting ? (Caret.Offset, Caret.Offset) : Ordered() is var (start, end, _) && start.Paragraph == end.Paragraph ? (start.Offset, end.Offset) : (0, 0);
                if (document.Placeholder(Caret.Paragraph, from, to - from) is var (s, e))
                    shown = from >= s && to <= e ? (s, e) : (from, to);
                // With changes tracked the new text follows what it deletes (measured in Word 365 by COM, scratchpad
                // b9/fieldedit2.vbs: a deletion, then the insertion), which a place in the text as it reads then cannot say;
                // it takes the format of the first character selected, which is gone then (scratchpad b9/notefmt). Over a place
                // paragraphs meet see Separated.
                else if (to > from && (document.Tracks || document.KeepsMarks && !document.Seams(Caret.Paragraph).Any(s => s > from && s < to)))
                    shown = (from, to);
            }
            var (firstPut, standIn, markStays) = (false, default(TextPlace?), false);
            TextPlace at;
            if (shown is var (begin, _))
                at = Caret with { Offset = begin };
            else
            {
                (firstPut, standIn, markStays) = OverParagraphs(lines[0], lines.Length > 1);
                at = DeleteSelection(typing: true);
            }
            for (var k = 0; k < lines.Length; k++)
            {
                if (k > 0)
                {
                    at = new TextPlace(k == 1 && markStays && document.Next(at.Paragraph) is { } next ? next : document.Split(at.Paragraph, at.Offset), 0);
                    if (standIn is { } stood)
                    {
                        document.Replace(stood.Paragraph, stood.Offset, 1, "");
                        standIn = null;
                    }
                }
                if (lines[k].Length > 0 && (k > 0 || !firstPut))
                {
                    var (offset, length) = k == 0 && shown is var (s, e) ? (s, e - s) : (at.Offset, 0);
                    document.Replace(at.Paragraph, offset, length, lines[k]);
                    if (Pending is { } format)
                        document.Format(at.Paragraph, at.Offset, lines[k].Length, format);
                    at = at with { Offset = at.Offset + lines[k].Length };
                }
            }
            return (at, at);
        });
    }

    /// <summary>
    /// Before a selection is typed over, without changes tracked: across paragraphs the text takes the format of the first character
    /// selected as over a selection in one paragraph (measured in Word 365 by COM, scratchpad b9/mp: of a bold paragraph's whole text
    /// and part of a plain one's, X bold; at a format's boundary in a paragraph that of the text after it), so it goes in its place first, the
    /// selection then the rest; for a line break alone a stand-in, whose format the first half's mark takes (see TextEdits.Split), over
    /// a selection in one paragraph too. Where the
    /// selection ends where a paragraph begins, the mark before that stays (see DeleteSelection), which is the line break then: Word
    /// leaves no paragraph more (of whole paragraphs selected, one empty one; so with changes tracked too). Not where it begins at
    /// its paragraph's end: Word goes by the text selected after the mark then, which this does not. Returns whether the text
    /// stands, the stand-in's place and whether the mark stays.
    /// </summary>
    (bool Put, TextPlace? StandIn, bool MarkStays) OverParagraphs(string first, bool more)
    {
        if (!Selecting || !document.KeepsMarks)
            return default;
        var (from, to, _) = Ordered();
        if (from.Paragraph == to.Paragraph)
        {
            // (Over one, the text goes in its place in one change, see Put.)
            if (first.Length > 0 || !more || document.Tracks || to.Offset == from.Offset || !document.PlainText(from.Paragraph, from.Offset) || !document.PlainText(to.Paragraph, to.Offset - 1)
                || document.Placeholder(from.Paragraph, from.Offset, to.Offset - from.Offset) != null || document.Seams(from.Paragraph).Any(s => s > from.Offset && s < to.Offset))
                return default;
            document.Replace(from.Paragraph, from.Offset, to.Offset - from.Offset, "x");
            Caret = Anchor = new TextPlace(from.Paragraph, from.Offset + 1);
            return (false, from, false);
        }
        // (Out of or into a table the rows reached into go whole, the first paragraph with them: see RemoveAcross.)
        if (!document.OfOneList(from.Paragraph, to.Paragraph))
            return default;
        var markStays = first.Length == 0 && more && to.Offset == 0 && document.Text(to.Paragraph).Length > 0;
        var head = document.Text(from.Paragraph);
        // (Only where that character is text of the paragraph's own: in a control or a field the text would stay in it and the
        // rest of the selection take it along or go round it; a placeholder the selection reaches into stays until text is put there.)
        if (document.Tracks || markStays || from.Offset >= head.Length || !document.PlainText(from.Paragraph, from.Offset)
            || document.Placeholder(from.Paragraph, from.Offset, head.Length - from.Offset) != null
            || document.OverFields(from.Paragraph, from.Offset + 1, to.Paragraph, to.Offset) is var (begins, at, _, _) && (begins, at) != (from.Paragraph, from.Offset + 1))
            return (false, null, markStays);
        var put = first.Length > 0 ? first : "x";
        document.Replace(from.Paragraph, from.Offset, After(head, from.Offset), put);
        (Anchor, Caret) = (new TextPlace(from.Paragraph, from.Offset + put.Length), to);
        return (first.Length > 0, first.Length > 0 ? null : from, false);
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
    public IReadOnlyList<int> InsertMarkup(string markup, IReadOnlyList<NewPart>? parts = null) => InsertMarkup(markup, parts, picture: false);

    IReadOnlyList<int> InsertMarkup(string markup, IReadOnlyList<NewPart>? parts, bool picture)
    {
        IReadOnlyList<int> made = [];
        Act(false, () =>
        {
            var at = DeleteSelection(typing: false);
            var length = document.Text(at.Paragraph).Length;
            made = document.InsertMarkup(at.Paragraph, at.Offset, markup, parts);
            // A picture in lines exactly so high: they at least so high, as Word would cut it at their height (see WordDocument.OpenLines).
            if (picture && document is WordDocument word)
                word.OpenLines(at.Paragraph);
            return Same(at with { Offset = at.Offset + document.Text(at.Paragraph).Length - length });
        });
        return made;
    }

    /// <summary>
    /// The size a picture takes put in at the cursor (see InsertPicture), in points: its own (its pixels at its resolution, see
    /// Html.ImageInfo) but no wider than the text of the column it stands in nor higher than the page's text, its proportions
    /// kept; null where the bytes are no PNG, JPEG, GIF, BMP or TIFF file or the document takes no pictures so (slides).
    /// </summary>
    public (double Width, double Height)? PictureSize(byte[] bytes) => PictureSize(bytes, Caret.Paragraph);

    /// <summary>The size a picture takes put in in a paragraph (see PictureSize), or the part of it given (see PictureCrop).</summary>
    (double Width, double Height)? PictureSize(byte[] bytes, int paragraph, PictureCrop? part = null)
    {
        if (document is not WordDocument word || PictureType(bytes) == null)
            return null;
        var (pixelsWide, pixelsHigh) = Html.ImageInfo.Size(bytes);
        var crop = part ?? PictureCrop.None;
        if (pixelsWide <= 0 || pixelsHigh <= 0 || crop.Wide <= 0 || crop.High <= 0)
            return null;
        // CSS pixels to points, then into the room there is.
        var (width, height) = (pixelsWide * 0.75 * crop.Wide, pixelsHigh * 0.75 * crop.High);
        var (roomWide, roomHigh) = RoomFor(word, paragraph);
        var scale = Math.Min(1, Math.Min(roomWide / width, roomHigh / height));
        return (width * scale, height * scale);
    }

    /// <summary>
    /// Puts a picture at the cursor in place of the selection, a handling of its own (see InsertMarkup), as Word puts one in
    /// from a file: in the line, at the size given (in points) or at the size it takes there (see PictureSize); named as given
    /// (wp:docPr, which Word shows as the picture's name); its paragraph's lines, where they are exactly so high, at least so high
    /// (see WordDocument.OpenLines). The bytes are a PNG, JPEG, GIF, BMP or TIFF file, kept as they are
    /// (made ready before, see Skia.PictureFit, the size given is the one they were made smaller for). False, and nothing done,
    /// where they are none of those or the document takes no pictures so (slides).
    /// </summary>
    public bool InsertPicture(byte[] bytes, string name, (double Width, double Height)? size = null)
    {
        if (document is not WordDocument || PictureType(bytes) is not var (type, extension) || (size ?? PictureSize(bytes)) is not var (width, height))
            return false;
        const double EmuPerPoint = 12700;
        var (cx, cy) = (Math.Max(1, (long)Math.Round(width * EmuPerPoint)), Math.Max(1, (long)Math.Round(height * EmuPerPoint)));
        var named = System.Security.SecurityElement.Escape(name.Replace('{', '(').Replace('}', ')'));
        InsertMarkup(
            $"<w:r><w:drawing><wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\"><wp:extent cx=\"{cx}\" cy=\"{cy}\"/><wp:effectExtent l=\"0\" t=\"0\" r=\"0\" b=\"0\"/>"
            + $"<wp:docPr id=\"1\" name=\"{named}\"/><wp:cNvGraphicFramePr><a:graphicFrameLocks noChangeAspect=\"1\"/></wp:cNvGraphicFramePr>"
            + "<a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><pic:pic>"
            + $"<pic:nvPicPr><pic:cNvPr id=\"0\" name=\"{named}\"/><pic:cNvPicPr/></pic:nvPicPr>"
            + "<pic:blipFill><a:blip r:embed=\"{picture}\"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>"
            + $"<pic:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"{cx}\" cy=\"{cy}\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr>"
            + "</pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r>",
            [new NewPart("picture", "/word/media/image1" + extension, type, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image", bytes)], picture: true);
        return true;
    }

    /// <summary>How the drawing the selection holds alone (its one character a drawing's) stands among the text (see DrawingFormatting); null otherwise.</summary>
    public DrawingFormatting? Drawing => SelectedDrawing() is var (paragraph, offset) ? document.DrawingOf(paragraph, offset) : null;

    /// <summary>
    /// Gives the drawing the selection holds alone how it stands among the text (see IEditableDocument.Format with DrawingFormatting), a
    /// handling of its own; it stays selected. Without such a selection nothing happens.
    /// </summary>
    public void Format(DrawingFormatting format)
    {
        if (SelectedDrawing() is not var (paragraph, offset))
            return;
        Act(false, () =>
        {
            document.Format(paragraph, offset, format);
            return (Caret, Anchor);
        });
    }

    /// <summary>
    /// Moves the drawing the selection holds alone to a place of the text (see IEditableDocument.MoveDrawing), a handling of its own; it
    /// stays selected there. False where the selection holds none, or where it would go is where it stands.
    /// </summary>
    public bool MoveDrawing(TextPlace to)
    {
        if (SelectedDrawing() is not var (paragraph, offset) || to.Paragraph == paragraph && (to.Offset == offset || to.Offset == offset + 1))
            return false;
        Act(false, () => Around(document.MoveDrawing(paragraph, offset, to.Paragraph, to.Offset)));
        return true;
    }

    /// <summary>
    /// Puts the floating drawing the selection holds alone where its box's top left stands at a point of a page (see
    /// IEditableDocument.PlaceDrawing), a handling of its own; it stays selected. False where the selection holds none floating.
    /// </summary>
    public bool PlaceDrawing(int page, double x, double y, string? alignAcross = null)
    {
        if (SelectedDrawing() is not var (paragraph, offset) || document.DrawingOf(paragraph, offset) is not { Wrap: not "inline" })
            return false;
        Act(false, () => Around(document.PlaceDrawing(paragraph, offset, page, x, y, alignAcross)));
        return true;
    }

    /// <summary>
    /// Gives the drawing the selection holds alone a size (points), a handling of its own; floating, with its box's top left at a point
    /// of a page (see PlaceDrawing), so that the side it is sized from moves and the other stays, and an alignment kept where it is
    /// given. It stays selected. False where the selection holds none.
    /// </summary>
    public bool SizeDrawing(double width, double height, (int Page, double X, double Y)? at = null, string? alignAcross = null, string? alignDown = null)
    {
        if (SelectedDrawing() is not var (paragraph, offset))
            return false;
        Act(false, () => Around(document.SizeDrawing(paragraph, offset, width, height, at, alignAcross, alignDown)));
        return true;
    }

    /// <summary>
    /// Gives the picture the selection holds alone the size it took when it was put in there (see PictureSize), the part of it it shows
    /// so (see CropPicture), a handling of its own; a floating one keeps its top left. False where the selection holds no picture.
    /// </summary>
    public bool RestoreSize()
    {
        if (SelectedDrawing() is not var (paragraph, offset) || document is not WordDocument word || word.PictureOf(paragraph, offset) is not { } bytes
            || PictureSize(bytes, paragraph, Picture(word, paragraph, offset)?.Crop) is not var (width, height))
            return false;
        Act(false, () =>
        {
            word.SizePicture(paragraph, offset, width, height);
            return (Caret, Anchor);
        });
        return true;
    }

    /// <summary>How the picture of the drawing at a place shows (a picture with its caption's own, see WordDocument.PictureAt): its size, its part.</summary>
    static DrawingFormatting? Picture(WordDocument word, int paragraph, int offset) =>
        word.PictureAt(paragraph, offset) is var (p, o) ? word.DrawingOf(p, o) : null;

    /// <summary>
    /// How the picture the selection holds alone shows by itself (a picture with its caption's own, see WordDocument.PictureAt): its size,
    /// the part of it it shows, its shape, colors and line; null where the selection holds no picture.
    /// </summary>
    public DrawingFormatting? PictureShown =>
        SelectedDrawing() is var (paragraph, offset) && document is WordDocument word && word.PictureOf(paragraph, offset) != null ? Picture(word, paragraph, offset) : null;

    /// <summary>The bytes of the picture the selection holds alone (see WordDocument.PictureOf); null where it holds none.</summary>
    public byte[]? PictureBytes => SelectedDrawing() is var (paragraph, offset) && document is WordDocument word ? word.PictureOf(paragraph, offset) : null;

    /// <summary>
    /// Crops the picture the selection holds alone to a part of it (see WordDocument.CropPicture: at its scale, a floating one's box moving
    /// with the sides cropped, or staying where <paramref name="boxStays"/>), a handling of its own; it stays selected. False where the
    /// selection holds no picture, or the part shows nothing of it.
    /// </summary>
    public bool CropPicture(PictureCrop crop, bool boxStays = false)
    {
        if (SelectedDrawing() is not var (paragraph, offset) || document is not WordDocument word || word.PictureOf(paragraph, offset) == null
            || crop.Wide < 0.001 || crop.High < 0.001)
            return false;
        Act(false, () =>
        {
            word.CropPicture(paragraph, offset, crop, boxStays);
            return (Caret, Anchor);
        });
        return true;
    }

    /// <summary>
    /// Crops the picture the selection holds alone to proportions (width to height; 0 or less, the whole picture), a handling of its own:
    /// the most of the picture they let it show, about the middle of what it shows so far, at the scale it has (see CropPicture) but no
    /// larger than the room it has (see PictureSize). It stays selected. False where the selection holds no picture.
    /// </summary>
    public bool CropPicture(double proportions) => Cropped(proportions, null);

    /// <summary>
    /// Shows the picture the selection holds alone in a circle (cropped to a square, see CropPicture, in an ellipse: Word's Crop to Shape),
    /// or back in a rectangle, as it is cropped; a handling of its own, it stays selected. False where the selection holds no picture.
    /// </summary>
    public bool RoundPicture(bool round) => round ? Cropped(1, "ellipse") : Cropped(null, "rect");

    /// <summary>
    /// Turns the picture the selection holds alone in its plane by so many degrees clockwise, on from where it stands (see
    /// DrawingFormatting.Rotation; with its caption, the picture alone, the caption staying level), a handling of its own; it stays
    /// selected. False where the selection holds no picture.
    /// </summary>
    public bool TurnPicture(double by) =>
        TurnShown is { } shown && Shows(new DrawingFormatting { Rotation = (((shown.Rotation ?? 0) + by) % 360 + 360) % 360 });

    /// <summary>
    /// Mirrors the picture the selection holds alone across, left for right, or down, top for bottom (back where it is mirrored so), a
    /// handling of its own. False where the selection holds no picture.
    /// </summary>
    public bool FlipPicture(bool across) =>
        PictureShown is { } shown && Shows(across ? new DrawingFormatting { FlipHorizontal = shown.FlipHorizontal != true } : new DrawingFormatting { FlipVertical = shown.FlipVertical != true });

    /// <summary>Stands the picture the selection holds alone straight, neither turned, mirrored nor tilted, a handling of its own. False where it holds no picture.</summary>
    public bool StraightenPicture() =>
        TurnShown != null && Shows(PictureShown != null ? new DrawingFormatting { Rotation = 0, FlipHorizontal = false, FlipVertical = false, Tilt = PictureTilt.Flat }
            : new DrawingFormatting { Rotation = 0, Tilt = PictureTilt.Flat });

    /// <summary>Tilts the picture the selection holds alone in space as Word's 3-D Rotation does (see PictureTilt), a handling of its own. False where it holds no picture.</summary>
    public bool TiltPicture(PictureTilt tilt) => TurnShown != null && Shows(new DrawingFormatting { Tilt = tilt });

    bool Shows(DrawingFormatting format)
    {
        Format(format);
        return true;
    }

    /// <summary>The picture selected cropped to proportions where given (see CropPicture), and in a shape where given, one handling.</summary>
    bool Cropped(double? proportions, string? shape)
    {
        if (SelectedDrawing() is not var (paragraph, offset) || document is not WordDocument word || word.PictureOf(paragraph, offset) == null
            || Picture(word, paragraph, offset) is not { Crop: { } was, Width: { } width, Height: { } height } || was.Wide <= 0 || was.High <= 0)
            return false;
        // The whole picture as large as it is shown.
        var (wide, high) = (width / was.Wide, height / was.High);
        var crop = was;
        if (proportions is { } ratio)
        {
            if (ratio <= 0)
                crop = PictureCrop.None;
            else
            {
                // As wide and high as the proportions let it be within the whole picture, about the middle of the part shown so far.
                var share = ratio / (wide / high);
                var (across, down) = share <= 1 ? (share, 1.0) : (1.0, 1 / share);
                var (middle, centre) = (Math.Clamp(was.Left + was.Wide / 2, across / 2, 1 - across / 2), Math.Clamp(was.Top + was.High / 2, down / 2, 1 - down / 2));
                crop = new PictureCrop(middle - across / 2, centre - down / 2, 1 - middle - across / 2, 1 - centre - down / 2);
            }
        }
        var (roomWide, roomHigh) = RoomFor(word, paragraph);
        var (shownWide, shownHigh) = (wide * crop.Wide, high * crop.High);
        var scale = Math.Min(1, Math.Min(roomWide / shownWide, roomHigh / shownHigh));
        Act(false, () =>
        {
            if (crop != was)
                word.CropPicture(paragraph, offset, crop);
            if (scale < 0.999)
                word.SizePicture(paragraph, offset, shownWide * scale, shownHigh * scale);
            if (shape != null)
                word.Format(paragraph, offset, new DrawingFormatting { Shape = shape });
            return (Caret, Anchor);
        });
        return true;
    }

    /// <summary>
    /// Shows another picture in the frame of the picture the selection holds alone (see WordDocument.ReplacePicture: as it stands and shows,
    /// filled by the new one), a handling of its own; it stays selected. The bytes are a PNG, JPEG, GIF, BMP or TIFF file, kept as they are
    /// (made ready before, see Skia.PictureFit). False, and nothing done, where they are none of those or the selection holds no picture.
    /// </summary>
    public bool ReplacePicture(byte[] bytes)
    {
        if (SelectedDrawing() is not var (paragraph, offset) || document is not WordDocument word || word.PictureOf(paragraph, offset) == null
            || PictureType(bytes) is not var (type, extension))
            return false;
        Act(false, () =>
        {
            word.ReplacePicture(paragraph, offset, bytes, type, extension);
            return (Caret, Anchor);
        });
        return true;
    }

    /// <summary>
    /// The first paragraph of the caption of the picture the selection holds alone (see WordDocument.AddCaption); null where it has none,
    /// or the selection holds no picture.
    /// </summary>
    public int? Caption => SelectedDrawing() is var (paragraph, offset) && document is WordDocument word ? word.CaptionOf(paragraph, offset) : null;

    /// <summary>
    /// Gives the picture the selection holds alone a caption below it (see WordDocument.AddCaption), a handling of its own; the cursor then
    /// in the caption, to write it. False where the selection holds no picture, or one with its caption.
    /// </summary>
    public bool AddCaption()
    {
        if (SelectedDrawing() is not var (paragraph, offset) || document is not WordDocument word || word.PictureOf(paragraph, offset) == null
            || word.CaptionOf(paragraph, offset) != null)
            return false;
        Act(false, () => Same(new TextPlace(word.AddCaption(paragraph, offset), 0)));
        return true;
    }

    /// <summary>
    /// The picture with its caption whose picture's paragraph a paragraph is (see AddCaption), which is no place for the cursor: the place of
    /// its character, to select it instead; null for other paragraphs.
    /// </summary>
    public (int Paragraph, int Offset)? CaptionedPicture(int paragraph) =>
        document is WordDocument word && word.FigureOf(paragraph) is (var p, var o, true) ? (p, o) : null;

    /// <summary>
    /// Takes the caption of the picture the selection holds alone away (see WordDocument.RemoveCaption), a handling of its own; the picture
    /// stays selected. False where it has none.
    /// </summary>
    public bool RemoveCaption()
    {
        if (SelectedDrawing() is not var (paragraph, offset) || document is not WordDocument word || word.CaptionOf(paragraph, offset) == null)
            return false;
        Act(false, () =>
        {
            word.RemoveCaption(paragraph, offset);
            return (Caret, Anchor);
        });
        return true;
    }

    /// <summary>A drawing's character selected as a whole, as a click selects it: the cursor after it, the anchor before.</summary>
    static (TextPlace Caret, TextPlace Anchor) Around((int Paragraph, int Offset) drawing) =>
        (new TextPlace(drawing.Paragraph, drawing.Offset + 1), new TextPlace(drawing.Paragraph, drawing.Offset));

    /// <summary>The place of the drawing the selection holds alone: one character of one paragraph, a drawing's.</summary>
    (int Paragraph, int Offset)? SelectedDrawing() =>
        Caret.Paragraph == Anchor.Paragraph && Math.Abs(Caret.Offset - Anchor.Offset) == 1 && Math.Min(Caret.Offset, Anchor.Offset) is var at
        && document.DrawingOf(Caret.Paragraph, at) != null ? (Caret.Paragraph, at) : null;

    /// <summary>The content type and file extension of a picture Word keeps as it is, by its first bytes; null for others.</summary>
    static (string Type, string Extension)? PictureType(byte[] bytes) => bytes switch
    {
        [0x89, 0x50, 0x4E, 0x47, ..] => ("image/png", ".png"),
        [0xFF, 0xD8, ..] => ("image/jpeg", ".jpeg"),
        [0x47, 0x49, 0x46, ..] => ("image/gif", ".gif"),
        [0x42, 0x4D, ..] => ("image/bmp", ".bmp"),
        _ when Html.Tiff.IsTiff(bytes) => ("image/tiff", ".tiff"),
        _ => null,
    };

    /// <summary>
    /// The room for a picture in a paragraph of a Word document, in points: the width of its section's text column (columns of
    /// even width), of a table's cell its text's, and the height of the page's text; in a note, header or footer the usual page's.
    /// </summary>
    static (double Width, double Height) RoomFor(WordDocument word, int paragraph)
    {
        SectionFormatting section;
        try
        {
            section = word.SectionOf(paragraph);
        }
        catch (NotSupportedException)
        {
            return (451.3, 697.9);
        }
        var columns = Math.Max(1, section.Columns ?? 1);
        var width = ((section.PageWidth ?? 595.3) - (section.Left ?? 72) - (section.Right ?? 72) - (section.Gutter ?? 0) - (columns - 1) * (section.ColumnSpace ?? 36)) / columns;
        // In a table's cell as wide as its text, so that the column keeps its width.
        if (word.TableOf(paragraph) != null)
            width = Math.Min(width, word.TextWidth(paragraph));
        var height = (section.PageHeight ?? 841.9) - Math.Abs(section.Top ?? 72) - Math.Abs(section.Bottom ?? 72);
        return (Math.Max(36, width), Math.Max(36, height));
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

    /// <summary>
    /// Takes the selection away, or the character before the cursor; at a paragraph's start joins it with the one before.
    /// A link or a field it selects first, and the next key takes it away (measured in Word 365 by COM, scratchpad
    /// b9/fieldedit.vbs, linkback.vbs: Selection.TypeBackspace behind one, the Delete key's command EditClear before one,
    /// complex and simple alike).
    /// </summary>
    public void Backspace() => Act(false, () =>
    {
        if (ClearedCells() is { } cleared)
            return Same(cleared);
        if (Selecting)
            return Same(DeleteSelection(typing: false));
        var (paragraph, offset) = Caret;
        if (offset > 0)
        {
            if (document.FieldAt(paragraph, offset, ending: true) is var (from, to))
            {
                Place(new TextPlace(paragraph, from));
                Place(new TextPlace(paragraph, to), extend: true);
                return (Caret, Anchor);
            }
            var length = Before(document.Text(paragraph), offset);
            document.Replace(paragraph, offset - length, length, "");
            return Same(new TextPlace(paragraph, offset - length));
        }
        if (document.Before(paragraph) is not { } before)
            return (Caret, Anchor);
        // At the beginning of a picture's caption the picture's paragraph is not joined: an empty caption goes, the picture selected.
        if (document is WordDocument word && word.FigureOf(before) is (var p, var o, true))
        {
            if (document.Text(paragraph).Length > 0 || document.After(paragraph) != null)
                return (Caret, Anchor);
            word.RemoveCaption(p, o);
            return Around((p, o));
        }
        var end = document.Text(before).Length;
        var joined = document.Join(before, backward: true);
        return Same(new TextPlace(joined, joined == before ? end : 0));
    });

    /// <summary>
    /// Takes the selection away, or the character after the cursor; at a paragraph's end joins it with the one after. A
    /// link or a field it selects first (see Backspace).
    /// </summary>
    public void Delete() => Act(false, () =>
    {
        if (ClearedCells() is { } cleared)
            return Same(cleared);
        if (Selecting)
            return Same(DeleteSelection(typing: false));
        var (paragraph, offset) = Caret;
        var text = document.Text(paragraph);
        if (offset < text.Length)
        {
            if (document.FieldAt(paragraph, offset, ending: false) is var (from, to))
            {
                Place(new TextPlace(paragraph, to), extend: true);
                return (Caret, Anchor);
            }
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
        Style = over.Style ?? format.Style,
    };

    /// <summary>Gives the paragraphs of the selection, or the cursor's, paragraph formatting (see SelectedParagraphs).</summary>
    public void Format(ParagraphFormatting format) => Act(false, () =>
    {
        foreach (var paragraph in SelectedParagraphs)
            document.Format(paragraph, format);
        return (Caret, Anchor);
    });

    /// <summary>
    /// The paragraphs paragraph formatting reaches, in reading order: the selection's, but not the last where the selection ends at its
    /// start, as in Word (measured by COM: a selection from one paragraph to the start of the third styles two, a triple click's one);
    /// the cursor's where nothing is selected; over cells selected all of theirs.
    /// </summary>
    public IReadOnlyList<int> SelectedParagraphs
    {
        get
        {
            var stretches = Stretches();
            if (CellSelection == null && stretches.Count > 1 && stretches[^1] is (_, 0, 0))
                stretches.RemoveAt(stretches.Count - 1);
            return [.. stretches.Select(s => s.Paragraph)];
        }
    }

    /// <summary>
    /// A change the document offers beyond the handlings above (a Word document's versions of a section, its comments) as a
    /// handling of its own, one step to undo and do again: <paramref name="change"/> makes it and tells where the cursor then
    /// stands; where it tells none, the cursor stays. A refusal leaves the document as it was (see the class).
    /// </summary>
    public void Handle(Func<TextPlace?> change) => Act(false, () => change() is { } caret ? (caret, caret) : (Caret, Anchor));

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

    /// <summary>
    /// Undoes the last step done as though it had never been made (a preview of a change, which an editor shows and takes back): the
    /// cursor and the selection back where they were before it, and nothing to do again, as when it was made; false where there is none.
    /// </summary>
    public bool TakeBack()
    {
        if (!Undo())
            return false;
        undone.Clear();
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
    /// <summary>As Act, for a change that moves the selection on its way: a refusal leaves it where it was.</summary>
    void ActKeepingPlaces(bool typing, Func<(TextPlace Caret, TextPlace Anchor)> change)
    {
        var (caret, anchor) = (Caret, Anchor);
        try
        {
            Act(typing, change);
        }
        catch
        {
            (Caret, Anchor) = (caret, anchor);
            throw;
        }
    }

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
            // A picture's caption written in: the picture with it as high as they then are (see WordDocument.FitFigure); a table's frame
            // changed in its table: as high as its table (see WordDocument.FitFrame).
            if (document.Changes > start && document is WordDocument word && word.FigureOf(after.Caret.Paragraph) is var (paragraph, offset, _))
                word.FitFigure(paragraph, offset);
            else if (document.Changes > start && document is WordDocument framed
                && (framed.FrameOf(after.Caret.Paragraph) ?? (after.Caret.Paragraph == after.Anchor.Paragraph && Math.Abs(after.Caret.Offset - after.Anchor.Offset) == 1
                    && framed.IsFrame(after.Caret.Paragraph, Math.Min(after.Caret.Offset, after.Anchor.Offset)) ? (after.Caret.Paragraph, Math.Min(after.Caret.Offset, after.Anchor.Offset)) : null))
                is var (holder, at))
                framed.FitFrame(holder, at);
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
            done[^1] = last with { Changes = Combined(last.Changes + changes), CaretAfter = after.Caret, AnchorAfter = after.Anchor };
        else
            done.Add(new Step(Combined(changes), caretBefore, anchorBefore, after.Caret, after.Anchor, typing));
        placed = !typing;
    }

    /// <summary>
    /// A step's changes, which it undoes and does again together, taken as one by the document (see IEditableDocument.Combine), so that it
    /// keeps no version between them: how many it counts for them then.
    /// </summary>
    int Combined(int changes)
    {
        var before = document.Changes;
        document.Combine(changes);
        return changes - (before - document.Changes);
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
        if (start.Paragraph != end.Paragraph)
            document.Unfield(start.Paragraph, start.Offset, end.Paragraph, end.Offset);
        var (first, final) = (document.Text(start.Paragraph), document.Text(end.Paragraph));
        // (Of a field what it shows, as of nothing where it shows nothing.)
        string Shown(TextPlace place, string text) => document.FieldText(place.Paragraph, place.Offset) ?? text[place.Offset].ToString();
        var (fromWord, toWord) = (start.Offset < first.Length && Shown(start, first) is var from && (from.Length == 0 || char.IsLetter(from[0])) && (start.Offset == 0 || !char.IsLetterOrDigit(first[start.Offset - 1])),
            end.Offset > 0 && Shown(end with { Offset = end.Offset - 1 }, final) is var to && (to.Length == 0 || char.IsLetterOrDigit(to[^1])) && (end.Offset == final.Length || !char.IsLetterOrDigit(final[end.Offset])));
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
            return tidy ? Tidied(start, fromWord && toWord) : start;
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
            document.TakePart(end.Paragraph, 0, end.Offset);
        // What stands between them, whole tables too (the document refuses where the ends are not of one body or cell).
        if (between.Count > 0)
            document.RemoveBetween(start.Paragraph, end.Paragraph);
        // The last paragraph now follows the first (by place: read anew, it may have taken another id), or is one with it: a hidden
        // mark before a table joins them as the table goes.
        var one = document.Text(start.Paragraph).Length > length;
        var rest = one ? document.Text(start.Paragraph)[length..] : document.After(start.Paragraph) is { } last ? document.Text(last) : "";
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
                document.TakePart(start.Paragraph, start.Offset, length - start.Offset);
            if (apart)
            {
                if (tidy && fromWord && toWord && document.After(start.Paragraph) is { } following)
                    Tidied(new TextPlace(following, 0));
                return start;
            }
            var joined = one ? start.Paragraph : document.Join(start.Paragraph);
            at = new TextPlace(joined, joined == start.Paragraph ? start.Offset : 0);
        }
        if (spaced && rest.Length > 0 && !char.IsWhiteSpace(rest[0]) && at.Offset > 0 && !char.IsWhiteSpace(document.Text(at.Paragraph)[at.Offset - 1]))
            document.Replace(at.Paragraph, at.Offset, 0, " ");
        return tidy && fromWord && toWord ? Tidied(at) : at;
    }

    /// <summary>
    /// The spaces where deleted whole words went tidied, as Word's smart cut does (Options.SmartCutPaste, on by default;
    /// measured in Word 365 by COM, WordBasic.EditClear and Selection.TypeBackspace alike, the selection beginning at a
    /// word's first letter and ending at a word's last, its last character a digit too, or <paramref name="words"/> not, a
    /// selection in one paragraph that begins with a digit or a sign; of a field by what it shows, scratchpad
    /// b9/smartnum.vbs): where the text after the place begins with a space and the text before it ends with one (whole
    /// words only) or the paragraph begins there, that space goes; where the text before ends with a space and the text
    /// after begins with a punctuation mark (. , ; : ! ?), the space before goes; a space that ends a paragraph stays. A
    /// space the document does not change so (one it computes) stays too. Returns where the cursor then stands.
    /// </summary>
    TextPlace Tidied(TextPlace at, bool words = true)
    {
        var text = document.Text(at.Paragraph);
        // (A paragraph's beginning and end as a line feed, which its text does not hold.)
        var (before, after) = (at.Offset > 0 ? text[at.Offset - 1] : '\n', at.Offset < text.Length ? text[at.Offset] : '\n');
        try
        {
            if (after == ' ' && (before == '\n' || words && before == ' '))
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

    /// <summary>
    /// Over cells selected (see CellSelection) what they hold taken away, as Word's Delete and Backspace take it (measured in Word 365 by
    /// COM, scratchpad tables/looks.vbs: each left empty, the cursor in the first); the place the cursor goes to, null without cells selected.
    /// </summary>
    TextPlace? ClearedCells() => CellSelection is { } cells && document is WordDocument word
        ? new TextPlace(word.ClearCells(cells.Table, cells.Top, cells.Bottom, cells.Left, cells.Right), 0)
        : null;

    /// <summary>
    /// The selection's paragraphs with the stretch of each it covers, or the cursor's paragraph where nothing is selected; over cells
    /// selected (see CellSelection) all of theirs, whole.
    /// </summary>
    List<(int Paragraph, int From, int To)> Stretches()
    {
        if (CellSelection is { } cells)
            return [.. CellsIn(cells).SelectMany(c => c.Paragraphs).Select(p => (p, 0, document.Text(p).Length))];
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
        var (start, end, between) = Plain();
        // Word's selection goes over the links and fields it holds one end of (see IEditableDocument.OverFields).
        var (first, from, last, to) = document.OverFields(start.Paragraph, start.Offset, end.Paragraph, end.Offset);
        if ((first, last) != (start.Paragraph, end.Paragraph))
        {
            between = [];
            for (int? next = first; next != null && (next = document.Next(next.Value)) != null && next != last;)
                between.Add(next.Value);
        }
        return (new TextPlace(first, from), new TextPlace(last, to), between);
    }

    /// <summary>The selection's ends in their order, with the paragraphs between them.</summary>
    (TextPlace Start, TextPlace End, List<int> Between) Plain()
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
