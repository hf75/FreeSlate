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
    /// <summary>The pages or slides as the document reads now; those a change left as they were keep their scene.</summary>
    IReadOnlyList<PageScene> Scenes();

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

    /// <summary>Replaces so many characters of a paragraph's text from a place by other text (without breaks).</summary>
    void Replace(int paragraph, int offset, int length, string text);

    /// <summary>Splits a paragraph in two at a place, as Enter does; returns the id of the second.</summary>
    int Split(int paragraph, int offset);

    /// <summary>Joins a paragraph with the next, as Delete at its end does; returns the id of the joined paragraph.</summary>
    int Join(int paragraph);

    /// <summary>Takes a paragraph away with all its content.</summary>
    void Remove(int paragraph);

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
