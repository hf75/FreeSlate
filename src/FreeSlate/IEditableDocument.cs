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
    /// Whether pages after those a change laid out stand as they did before it: a long document is laid out again bit by
    /// bit after a change (see LayOut), as an editor does while it waits for input.
    /// </summary>
    bool LayingOut { get; }

    /// <summary>Lays out about so many more of the pages a change left as they were (see LayingOut); whether more are left.</summary>
    bool LayOut(int pages = 4);

    /// <summary>
    /// Whether the document changes a paragraph of that id: in a Word document those of its body (in tables and text boxes
    /// too), not of headers, footers, notes or comments; on slides those of shapes and tables.
    /// </summary>
    bool Contains(int paragraph);

    /// <summary>The text of a paragraph.</summary>
    string Text(int paragraph);

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
    /// in (on slides, which lack page and column breaks, as line breaks); it holds no paragraph breaks.
    /// </summary>
    void Replace(int paragraph, int offset, int length, string text);

    /// <summary>
    /// The word a place in a paragraph's text stands inside, which the application's character formatting takes where no
    /// text is selected (see Editing.Words): its offset and length, or null at a word's edge or between words.
    /// </summary>
    (int Offset, int Length)? WordAt(int paragraph, int offset);

    /// <summary>Splits a paragraph in two at a place, as Enter does; returns the id of the second.</summary>
    int Split(int paragraph, int offset);

    /// <summary>Joins a paragraph with the next, as Delete at its end does; returns the id of the joined paragraph.</summary>
    int Join(int paragraph);

    /// <summary>
    /// How deleting a selection over paragraphs treats their marks, as the application does (measured in Word 365 and
    /// PowerPoint 365 by COM, WordBasic.EditClear and TextRange.Delete): in Word (true) the paragraph the selection begins
    /// inside keeps its mark, it and the paragraph it ends in staying apart, and typing over a selection that ends where a
    /// paragraph begins keeps the mark before it; in PowerPoint (false) they are joined.
    /// </summary>
    bool KeepsMarks { get; }

    /// <summary>Takes a paragraph away with all its content.</summary>
    void Remove(int paragraph);

    /// <summary>
    /// Takes away what stands between two paragraphs of one body, table cell, text box or shape, the first before the
    /// second: the paragraphs and whole tables there, as deleting a selection over them does.
    /// </summary>
    void RemoveBetween(int first, int last);

    /// <summary>Gives a stretch of a paragraph's text character formatting, and a paragraph paragraph formatting.</summary>
    void Format(int paragraph, int offset, int length, TextFormatting format);
    void Format(int paragraph, ParagraphFormatting format);

    /// <summary>How many changes the document holds as it reads now: 0 as opened, one more for each change made, one less for each undone.</summary>
    int Changes { get; }

    /// <summary>Whether a change can be undone, and one undone done again.</summary>
    bool CanUndo { get; }
    bool CanRedo { get; }

    /// <summary>Undoes the last change not undone, or does the last change undone again; false where there is none.</summary>
    bool Undo();
    bool Redo();

    /// <summary>Writes the document in its own format, to a stream or to a file (over the one it was opened from too).</summary>
    void Save(Stream output);
    void Save(string target);
}
