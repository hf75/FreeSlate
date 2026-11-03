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
    /// The size a picture takes put in at the cursor (see InsertPicture), in points: its own (its pixels at its resolution, see
    /// Html.ImageInfo) but no wider than the text of the column it stands in nor higher than the page's text, its proportions
    /// kept; null where the bytes are no PNG, JPEG, GIF, BMP or TIFF file or the document takes no pictures so (slides).
    /// </summary>
    public (double Width, double Height)? PictureSize(byte[] bytes) => PictureSize(bytes, Caret.Paragraph);

    /// <summary>The size a picture takes put in in a paragraph (see PictureSize).</summary>
    (double Width, double Height)? PictureSize(byte[] bytes, int paragraph)
    {
        if (document is not WordDocument word || PictureType(bytes) == null)
            return null;
        var (pixelsWide, pixelsHigh) = Html.ImageInfo.Size(bytes);
        if (pixelsWide <= 0 || pixelsHigh <= 0)
            return null;
        // CSS pixels to points, then into the room there is.
        var (width, height) = (pixelsWide * 0.75, pixelsHigh * 0.75);
        var (roomWide, roomHigh) = RoomFor(word, paragraph);
        var scale = Math.Min(1, Math.Min(roomWide / width, roomHigh / height));
        return (width * scale, height * scale);
    }

    /// <summary>
    /// Puts a picture at the cursor in place of the selection, a handling of its own (see InsertMarkup), as Word puts one in
    /// from a file: in the line, at the size given (in points) or at the size it takes there (see PictureSize); named as given
    /// (wp:docPr, which Word shows as the picture's name). The bytes are a PNG, JPEG, GIF, BMP or TIFF file, kept as they are
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
            [new NewPart("picture", "/word/media/image1" + extension, type, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image", bytes)]);
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
    /// Gives the picture the selection holds alone the size it took when it was put in there (see PictureSize), a handling of its own;
    /// a floating one keeps its top left. False where the selection holds no picture.
    /// </summary>
    public bool RestoreSize()
    {
        if (SelectedDrawing() is not var (paragraph, offset) || document is not WordDocument word || word.PictureOf(paragraph, offset) is not { } bytes
            || PictureSize(bytes, paragraph) is not var (width, height))
            return false;
        Format(new DrawingFormatting { Width = width, Height = height });
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
    /// even width) and the height of the page's text; in a note, header or footer the usual page's.
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

    /// <summary>Gives the paragraphs of the selection, or the cursor's, paragraph formatting.</summary>
    public void Format(ParagraphFormatting format) => Act(false, () =>
    {
        foreach (var (paragraph, _, _) in Stretches())
            document.Format(paragraph, format);
        return (Caret, Anchor);
    });

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
