using FreeSlate.Rendering;

namespace FreeSlate;

/// <summary>
/// A document whose text an editor changes paragraph by paragraph (see Editing.EditSession): a Word document
/// (WordDocument) or a presentation (PresentationDocument). Paragraphs go by the ids the places of its pages carry (see
/// PlacesItem), offsets count in a paragraph's text as those places do. Each change is a version of its own, which Undo
/// goes back from and Redo forward to again; a change the document cannot make throws NotSupportedException and leaves
/// it as it was.
/// </summary>
public interface IEditableDocument : IDisposable
{
    /// <summary>
    /// The pages or slides as the document reads now; those a change left as they were keep their scene. Pages may come
    /// not drawn yet (PageScene.Drawn), to be drawn by Scene when they are wanted.
    /// </summary>
    IReadOnlyList<PageScene> Scenes();

    /// <summary>
    /// A page or slide drawn: a page Scenes gave not drawn yet is drawn now, in its place in the list Scenes gave. A
    /// document may keep only so many of the pages it drew so drawn, those nearest the page asked for last: the others
    /// go back to not drawn in that list, to be drawn again when they are wanted.
    /// </summary>
    PageScene Scene(int page);

    /// <summary>
    /// The page or slide (its index) that shows a place of a paragraph's text as the pages are laid out now, found without
    /// drawing them, else null (a paragraph of a text box, of a page not laid out yet). A page left as it was laid out
    /// before a change, which the layout has not come to yet (see LayingOut), shows the paragraph as it read then.
    /// </summary>
    int? PageOf(int paragraph, int offset);

    /// <summary>
    /// Whether pages after those a change laid out stand as they did before it: a long document is laid out again bit by
    /// bit after a change (see LayOut), as an editor does while it waits for input.
    /// </summary>
    bool LayingOut { get; }

    /// <summary>
    /// Lays out about so many more of the pages a change left as they were (see LayingOut), or as many as about 10 ms give
    /// time for, for input to come between; whether more are left.
    /// </summary>
    bool LayOut(int pages = 0);

    /// <summary>
    /// Whether the document changes a paragraph of that id: in a Word document those of its body, of its footnotes and
    /// endnotes and of its headers and footers (in tables and text boxes too), not of comments; on slides those of shapes
    /// and tables.
    /// </summary>
    bool Contains(int paragraph);

    /// <summary>The text of a paragraph.</summary>
    string Text(int paragraph);

    /// <summary>
    /// The paragraph the text begins with, where a cursor starts: in a Word document the first of its body, of the first
    /// cell where a table begins it, not of a header; on slides the first of the first shape or table cell with text; null
    /// where none is.
    /// </summary>
    int? Beginning { get; }

    /// <summary>
    /// The paragraph just before or after a paragraph in its body, table cell, text box or shape, or null where none is
    /// (at its edge, or where a table stands between).
    /// </summary>
    int? Before(int paragraph);
    int? After(int paragraph);

    /// <summary>
    /// The paragraph just after or before a paragraph in reading order: in a Word document on over the tables of its body,
    /// cell or text box, their cells row by row; null at the end of its body or text box (on slides: of its shape or
    /// table cell, as After and Before).
    /// </summary>
    int? Next(int paragraph);
    int? Previous(int paragraph);

    /// <summary>
    /// Replaces so many characters of a paragraph's text from a place by other text, its tabs ("\t"), line breaks ("\v"),
    /// page and column breaks ("\f", "\u000E") as the document's own, as the Tab key, Shift+Enter and Ctrl+Enter put them
    /// in (on slides, which lack page and column breaks, as line breaks); it holds no paragraph breaks. What a content
    /// control shows as its placeholder goes as Word takes it (see Placeholder).
    /// </summary>
    void Replace(int paragraph, int offset, int length, string text);

    /// <summary>
    /// Takes away the part of a paragraph's text that a selection going on in other paragraphs holds: a placeholder of a content
    /// control it holds up to its edge goes too (see WordDocument.Replace).
    /// </summary>
    void TakePart(int paragraph, int offset, int length);

    /// <summary>
    /// The text a field shows where the character at a place of a paragraph's text is one, else null: Word's smart cut goes by
    /// what it shows (see EditSession.Tidied).
    /// </summary>
    string? FieldText(int paragraph, int offset);

    /// <summary>
    /// The stretch of a paragraph's text a link or a field shows that ends at a place (<paramref name="ending"/>) or begins at
    /// it, null where none does: Backspace behind one and Delete before one select it first (see EditSession.Backspace).
    /// </summary>
    (int Start, int End)? FieldAt(int paragraph, int offset, bool ending);

    /// <summary>Where the cursor stands for a place of a paragraph's text: behind what a field computes where the place is within it (a list number), else there.</summary>
    int Standing(int paragraph, int offset);

    /// <summary>
    /// A selection from a place to a later one, brought out over the links and fields it holds one end of, whose ends and
    /// what they show go whole (see EditSession.Ordered).
    /// </summary>
    (int First, int From, int Last, int To) OverFields(int first, int from, int last, int to);

    /// <summary>
    /// The fields whose beginnings and ends stand in different paragraphs within a selection from a place to a later one taken away,
    /// their results left as text, a step in taking the selection away.
    /// </summary>
    void Unfield(int first, int from, int last, int to);

    /// <summary>
    /// The stretch of a paragraph's text a content control shows as its placeholder where a range of it goes into one,
    /// else the first one it reaches into from outside: text put in where it goes into one (see Replace) takes the whole
    /// placeholder's place, taking characters away there leaves it as it is, and a range reaching into one from outside
    /// is replaced around it, as Word does with what it shows so (see Docx.TextEdits.Placeholder); null elsewhere (a
    /// slide's placeholder shows its prompt in no paragraph).
    /// </summary>
    (int Start, int End)? Placeholder(int paragraph, int offset, int length);

    /// <summary>
    /// The word a place in a paragraph's text stands inside, which the application's character formatting takes where no
    /// text is selected (see Editing.Words): its offset and length, or null at a word's edge or between words.
    /// </summary>
    (int Offset, int Length)? WordAt(int paragraph, int offset);

    /// <summary>Splits a paragraph in two at a place, as Enter does; returns the id of the second.</summary>
    int Split(int paragraph, int offset);

    /// <summary>
    /// Joins a paragraph with the next, as Delete at its end does, or <paramref name="backward"/> as Backspace at the next's
    /// start does (in Word the joined paragraph keeps the first's mark then); returns the id of the joined paragraph.
    /// </summary>
    int Join(int paragraph, bool backward = false);

    /// <summary>
    /// The places in a paragraph's text where paragraphs the document reads as one meet (in Word those whose marks are
    /// hidden or deleted run into the next, see Docx.ParagraphBlock.Parts), in order; none for a paragraph of its own.
    /// </summary>
    IReadOnlyList<int> Seams(int paragraph);

    /// <summary>
    /// A paragraph the document reads from several brought apart where two of them meet (see Seams), the mark between
    /// them a paragraph's own again (neither hidden nor deleted), as Word makes it where a selection over it is deleted or
    /// typed over; returns the id of the paragraph after it, the first keeping the paragraph's id.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The document tracks its changes, or does not bring them apart so (a style hides the mark, a tracked move took it);
    /// nothing changes.
    /// </exception>
    int Separate(int paragraph, int offset);

    /// <summary>
    /// How deleting a selection over paragraphs treats their marks, as the application does (measured in Word 365 and
    /// PowerPoint 365 by COM, WordBasic.EditClear and TextRange.Delete): in Word (true) the paragraph the selection begins
    /// inside keeps its mark, it and the paragraph it ends in staying apart, and typing over a selection that ends where a
    /// paragraph begins keeps the mark before it; in PowerPoint (false) they are joined.
    /// </summary>
    bool KeepsMarks { get; }

    /// <summary>
    /// Whether the document makes its changes as tracked changes (in Word with an author set, see WordDocument.Author),
    /// which deleting a selection over paragraphs treats otherwise (see Editing.EditSession).
    /// </summary>
    bool Tracks { get; }

    /// <summary>
    /// Puts markup of the document's own kind into a paragraph at a place, as pasting or an application's Insert commands
    /// put it there: in a Word document WordprocessingML that stands among runs (runs; a picture, chart, shape or WordArt
    /// in a w:drawing; an equation; a field), its namespaces named by their usual prefixes (w, r, wp, a, pic, c, m, wps,
    /// wpg …) or declared in it, with the parts it needs added to the package (see NewPart). Returns the ids of the
    /// paragraphs it brought (those of its text boxes), in their order. Slides take none (NotSupportedException).
    /// </summary>
    IReadOnlyList<int> InsertMarkup(int paragraph, int offset, string markup, IReadOnlyList<NewPart>? parts = null);

    /// <summary>
    /// Puts blocks of the document's own kind after a paragraph in its body, table cell or text box (before it with
    /// <paramref name="before"/>), with the parts they need (see NewPart): in a Word document paragraphs and tables as
    /// WordprocessingML, a paragraph among them that ends a section beginning one after it. Returns the ids of the
    /// paragraphs they hold, in reading order (a table's cell by cell). Slides take none (NotSupportedException).
    /// </summary>
    IReadOnlyList<int> InsertBlocks(int paragraph, string markup, bool before = false, IReadOnlyList<NewPart>? parts = null);

    /// <summary>
    /// Puts a footnote at a place (an endnote with <paramref name="endnote"/>): its reference mark there and its text in
    /// the document's notes after the note's own mark, given as markup of runs (see InsertMarkup). Slides have no such
    /// notes (NotSupportedException).
    /// </summary>
    void InsertNote(int paragraph, int offset, string markup, bool endnote = false);

    /// <summary>Takes a paragraph away with all its content.</summary>
    void Remove(int paragraph);

    /// <summary>
    /// Takes away what stands between two paragraphs of one body, table cell, text box or shape, the first before the
    /// second: the paragraphs and whole tables there, as deleting a selection over them does.
    /// </summary>
    void RemoveBetween(int first, int last);

    /// <summary>
    /// Takes away a selection from a place to a later one whose paragraphs are not of one body, table cell or text box
    /// (one in a table the other is not in), as deleting it does in the application (typed over with
    /// <paramref name="typing"/>): in a Word document the table's rows the selection reaches into go whole (see
    /// WordDocument.RemoveAcross). Returns where the cursor then stands, or null where the paragraphs are of one body,
    /// cell, text box or shape (as on slides, whose paragraphs are walked within one shape or cell: see Next).
    /// </summary>
    (int Paragraph, int Offset)? RemoveAcross(int first, int offset, int last, int lastOffset, bool typing);

    /// <summary>Whether the character at a place of a paragraph's text is text of the paragraph's own: in no content control, link or field, not something else a run holds.</summary>
    bool PlainText(int paragraph, int offset);

    /// <summary>Whether two paragraphs are of one body, table cell, text box or shape, which taking a selection from the first to the second leaves the first of (see RemoveAcross).</summary>
    bool OfOneList(int first, int last);

    /// <summary>
    /// The selection from a place to a later one in the document's own markup, as copying it keeps it for pasting with its
    /// formatting (see Paste): in a Word document each paragraph it reaches into as a w:p with its properties and what of
    /// its content the selection holds, the last one without its mark (see WordDocument.Copy). Null where the document keeps
    /// only the selection's text (slides; in a Word document a selection over a table, in a note, header or comment).
    /// </summary>
    IReadOnlyList<string>? Copy(int first, int offset, int last, int lastOffset);

    /// <summary>
    /// Pastes what Copy gave at a place, as the application pastes it with its formatting (see WordDocument.Paste);
    /// returns where the cursor then stands, after it.
    /// </summary>
    (int Paragraph, int Offset) Paste(int paragraph, int offset, IReadOnlyList<string> copied);

    /// <summary>Gives a stretch of a paragraph's text character formatting, and a paragraph paragraph formatting.</summary>
    void Format(int paragraph, int offset, int length, TextFormatting format);
    void Format(int paragraph, ParagraphFormatting format);

    /// <summary>
    /// How the drawing whose character stands at a place of a paragraph's text stands among the text (see DrawingFormatting), every
    /// property given that it has; null where no drawing stands there (on slides always).
    /// </summary>
    DrawingFormatting? DrawingOf(int paragraph, int offset);

    /// <summary>Gives the drawing at a place of a paragraph's text how it stands among the text (see DrawingFormatting); slides have none (NotSupportedException).</summary>
    void Format(int paragraph, int offset, DrawingFormatting format);

    /// <summary>
    /// The stretches of a paragraph's text that have a character style (TextFormatting.Style), in order, neighbouring runs of
    /// one style as one stretch: what the text means where its styles say so. Slides have none.
    /// </summary>
    IReadOnlyList<(int Offset, int Length, string Style)> CharacterStyles(int paragraph);

    /// <summary>The paragraph style of a paragraph by its id (ParagraphFormatting.Style); null for the default one, and on slides.</summary>
    string? ParagraphStyle(int paragraph);

    /// <summary>
    /// Gives the document style definitions, as a style set does in Word (Design, Document Formatting): the w:style elements of
    /// <paramref name="markup"/> (a w:styles element) take the place of the document's of the same ids or join them, and its
    /// w:docDefaults the document's where it has them; so do the w:abstractNum and w:num elements of <paramref name="numbering"/>
    /// (a w:numbering element) in the numbering definitions, for lists the styles name. All of the document is read and laid out
    /// anew under them, one change. Slides have no styles (NotSupportedException).
    /// </summary>
    void DefineStyles(string markup, string? numbering = null);

    /// <summary>The name of a style the document defines (w:name), by its id; null where it defines none of that id (slides: none).</summary>
    string? StyleName(string id);

    /// <summary>How many changes the document holds as it reads now: 0 as opened, one more for each change made, one less for each undone.</summary>
    int Changes { get; }

    /// <summary>Whether a change can be undone, and one undone done again.</summary>
    bool CanUndo { get; }
    bool CanRedo { get; }

    /// <summary>Undoes the last change not undone, or does the last change undone again; false where there is none.</summary>
    bool Undo();
    bool Redo();

    /// <summary>
    /// Takes the last <paramref name="count"/> changes as one, as an editor undoes and does them again together (the keys of words
    /// typed one after the other): the versions between them go, and with them what only they kept; Changes then counts one for them.
    /// </summary>
    void Combine(int count);

    /// <summary>Writes the document in its own format, to a stream or to a file (over the one it was opened from too).</summary>
    void Save(Stream output);
    void Save(string target);
}

/// <summary>
/// A part a change adds to the package with the markup it puts in (see IEditableDocument.InsertMarkup): a picture, a
/// chart, the workbook that holds a chart's data. <see cref="Name"/> is a wish: the part is numbered on where the package
/// has a part so named or had one since it was opened (pictures are known by their part's name). The relationship to it,
/// of the type given, goes from the main part, or from the new part whose key <see cref="From"/> names; where "{key}"
/// stands in the markup or in the bytes of the part the relationship goes from, the relationship's id takes its place.
/// <see cref="Relationships"/> are the part's own (the bytes of a .rels part, its targets relative to the part's folder),
/// where it is a copy of another that has them (a header unlinked from the one before it).
/// </summary>
public sealed record NewPart(string Key, string Name, string ContentType, string Relationship, byte[] Bytes, string? From = null, byte[]? Relationships = null);
