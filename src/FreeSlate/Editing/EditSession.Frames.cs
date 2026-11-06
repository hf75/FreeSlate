namespace FreeSlate.Editing;

/// <summary>Tables in frames (see WordDocument.FrameTable): a table that stands as a picture does, turned, tilted, in front of the text or behind it.</summary>
public sealed partial class EditSession
{
    /// <summary>Whether the selection holds a table's frame alone (see WordDocument.FrameTable).</summary>
    public bool FrameSelected => SelectedDrawing() is var (paragraph, offset) && document is WordDocument word && word.IsFrame(paragraph, offset);

    /// <summary>
    /// How the drawing the selection holds alone shows turned and tilted, for turning and tilting it by hand: a picture's (see
    /// PictureShown) or a table's frame's; null otherwise.
    /// </summary>
    public DrawingFormatting? TurnShown => PictureShown ?? (FrameSelected ? Drawing : null);

    /// <summary>
    /// The frame of the table the selection's frame holds, or of the cursor's table where it stands in one: the place of its character;
    /// null where there is none.
    /// </summary>
    public (int Paragraph, int Offset)? Frame => document is not WordDocument word ? null
        : FrameSelected ? SelectedDrawing() : word.FrameOf(Caret.Paragraph);

    /// <summary>
    /// The cursor's table moved into a frame where it stood (see WordDocument.FrameTable), standing as given (in front of the text or
    /// behind it, turned, tilted …), the frame then selected, so that it turns, tilts and moves as a picture does; a table in a frame
    /// already its frame given the format. A handling of its own; false outside tables or in other documents than Word's.
    /// </summary>
    public bool FrameTable(DrawingFormatting? format = null)
    {
        if (document is not WordDocument word)
            return false;
        if (Frame is var (paragraph, offset))
        {
            Act(false, () =>
            {
                if (format != null)
                    word.Format(paragraph, offset, format);
                return (new TextPlace(paragraph, offset + 1), new TextPlace(paragraph, offset));
            });
            return true;
        }
        if (Table is not { } table)
            return false;
        Act(false, () =>
        {
            var (at, place) = word.FrameTable(table.Table, format);
            return (new TextPlace(at, place + 1), new TextPlace(at, place));
        });
        return true;
    }

    /// <summary>A table back in the text where it stands in a frame (see WordDocument.UnframeTable), for placing it there: its id then.</summary>
    static int Unframed(WordDocument word, int table)
    {
        if (word.CellsOf(table)?.Cells[0].First is not { } first || word.FrameOf(first) is not var (paragraph, offset))
            return table;
        return word.UnframeTable(paragraph, offset);
    }

    /// <summary>
    /// The cursor's table straight again: back in the text from its frame (see UnframeTable) and among the text's pages from pages of its
    /// own turned across (see TurnTablePages), the cursor staying in it; a handling of its own. False outside tables.
    /// </summary>
    public bool StraightenTable()
    {
        if (document is not WordDocument word || Table is not { } table)
            return false;
        var cell = Cell is { } at ? (at.Top, at.Left) : (0, 0);
        Act(false, () =>
        {
            var id = Unframed(word, table.Table);
            word.TurnTablePages(id, false);
            var first = word.CellsOf(id)!.Cells.FirstOrDefault(c => (c.Top, c.Left) == cell) ?? word.CellsOf(id)!.Cells[0];
            return (new TextPlace(first.First, 0), new TextPlace(first.First, 0));
        });
        return true;
    }

    /// <summary>
    /// The table of the selection's frame, or the cursor's table in its frame, back in the text where the frame was anchored (see
    /// WordDocument.UnframeTable), the cursor in its first cell; a handling of its own. False where there is none.
    /// </summary>
    public bool UnframeTable()
    {
        if (document is not WordDocument word || Frame is not var (paragraph, offset))
            return false;
        Act(false, () =>
        {
            var table = word.UnframeTable(paragraph, offset);
            var first = word.CellsOf(table)!.Cells[0].First;
            return (new TextPlace(first, 0), new TextPlace(first, 0));
        });
        return true;
    }
}
