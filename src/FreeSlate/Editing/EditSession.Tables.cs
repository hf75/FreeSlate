namespace FreeSlate.Editing;

/// <summary>
/// The handlings of tables (see WordDocument's TableMarkup, CellsOf and the changes of tables as a whole): a table put in at the
/// cursor, Tab from cell to cell, and any change of a table as a handling of its own.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>The table the cursor stands in, the innermost (see WordDocument.TableOf), with its cells; null outside tables and in other documents.</summary>
    public TableCells? Table => document is WordDocument word && document.Contains(Caret.Paragraph) && word.TableOf(Caret.Paragraph) is { } table ? word.CellsOf(table) : null;

    /// <summary>The cell of that table the cursor stands in; null outside tables.</summary>
    public TableCell? Cell => Table is { } table ? table.Cells.FirstOrDefault(c => c.Paragraphs.Contains(Caret.Paragraph)) : null;

    /// <summary>
    /// Puts a new table in at the cursor, a handling of its own, as Word's Insert Table does (measured in Word 365 by COM, scratchpad
    /// tables/insert.vbs): at a paragraph's start before it, elsewhere between the paragraph's halves split there (at its end before the
    /// new empty one), its markup as WordDocument.TableMarkup writes it; with a selection at its start. The cursor then in its first
    /// cell. False in other documents than Word's.
    /// </summary>
    public bool InsertTable(int columns, int rows, string? style = null, string? textStyle = null)
    {
        if (document is not WordDocument word)
            return false;
        Act(false, () =>
        {
            var (paragraph, offset) = Selecting ? Selection.Start : Caret;
            if (offset > 0)
                paragraph = document.Split(paragraph, offset);
            var made = document.InsertBlocks(paragraph, word.TableMarkup(paragraph, columns, rows, style, textStyle), before: true);
            return Same(new TextPlace(made[0], 0));
        });
        return true;
    }

    /// <summary>
    /// Tab in a table, as Word's (measured in Word 365 by COM, scratchpad tables/tab.vbs): the next cell's text selected, or with
    /// <paramref name="back"/> the cell's before (none before the first); in the last cell a new row like the last one, a handling
    /// of its own, the cursor in its first cell (as Word copies a row: the cells' spans and properties, their paragraphs' marks).
    /// False outside tables, where Tab is the editor's.
    /// </summary>
    public bool NextCell(bool back = false)
    {
        if (document is not WordDocument word || Table is not { } table)
            return false;
        var k = -1;
        for (var i = 0; i < table.Cells.Count && k < 0; i++)
        {
            if (table.Cells[i].Paragraphs.Contains(Caret.Paragraph))
                k = i;
        }
        if (k < 0)
            return false;
        if (back)
        {
            if (k > 0)
                Select(table.Cells[k - 1]);
            return true;
        }
        if (k + 1 < table.Cells.Count)
        {
            Select(table.Cells[k + 1]);
            return true;
        }
        Act(false, () => Same(new TextPlace(word.AddRows(table.Table, table.Rows, 1, like: table.Rows - 1), 0)));
        return true;
    }

    /// <summary>A cell's text selected, as Tab selects it: from its first paragraph's start to its last one's end.</summary>
    public void Select(TableCell cell)
    {
        Place(new TextPlace(cell.First, 0));
        Place(new TextPlace(cell.Last, document.Text(cell.Last).Length), extend: true);
    }

    /// <summary>
    /// A change of a table of the body as a whole (see WordDocument's changes of tables), a handling of its own: <paramref name="change"/>
    /// makes it and names the paragraph to go on in, the cursor then at its start; where it names none, the cursor stays (a table's
    /// lines moved keep its paragraphs). False in other documents than Word's.
    /// </summary>
    public bool ChangeTable(Func<WordDocument, int?> change)
    {
        if (document is not WordDocument word)
            return false;
        Act(false, () => change(word) is { } paragraph ? Same(new TextPlace(paragraph, 0)) : (Caret, Anchor));
        return true;
    }
}
