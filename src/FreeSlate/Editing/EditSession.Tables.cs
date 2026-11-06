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

    /// <summary>The cells selected as last found, with the version and the selection they were found for (see CellSelection).</summary>
    (int Changes, TextPlace Caret, TextPlace Anchor, CellRange? Range)? cellsFound;

    /// <summary>
    /// The cells selected together where the selection reaches from a cell of a table into another cell of it, as Word selects whole
    /// cells then: the rectangle of the table's grid that holds both, each cell reaching into it whole (as a merged cell stands); null
    /// where the selection begins and ends in one cell, or not in cells of one table.
    /// </summary>
    public CellRange? CellSelection
    {
        get
        {
            if (cellsFound is var (changes, caret, anchor, found) && changes == document.Changes && caret == Caret && anchor == Anchor)
                return found;
            var range = FindCells();
            cellsFound = (document.Changes, Caret, Anchor, range);
            return range;
        }
    }

    CellRange? FindCells()
    {
        if (!Selecting || document is not WordDocument word || !document.Contains(Caret.Paragraph) || !document.Contains(Anchor.Paragraph)
            || word.TableOf(Anchor.Paragraph) is not { } table || word.TableOf(Caret.Paragraph) != table || word.CellsOf(table) is not { } cells)
            return null;
        var (from, to) = (cells.Cells.FirstOrDefault(c => c.Paragraphs.Contains(Anchor.Paragraph)), cells.Cells.FirstOrDefault(c => c.Paragraphs.Contains(Caret.Paragraph)));
        if (from == null || to == null || ReferenceEquals(from, to))
            return null;
        return word.Enclosing(table, Math.Min(from.Top, to.Top), Math.Max(from.Bottom, to.Bottom), Math.Min(from.Left, to.Left), Math.Max(from.Right, to.Right));
    }

    /// <summary>Cells of a table selected together, the anchor at the start of the first and the cursor at the end of the last (as dragging over them leaves them).</summary>
    public void Select(CellRange range)
    {
        if (CellsIn(range) is not [var first, .., var last])
            return;
        Place(new TextPlace(first.First, 0));
        Place(new TextPlace(last.Last, document.Text(last.Last).Length), extend: true);
    }

    /// <summary>
    /// The cells selected copied (see Copy): their text as a spreadsheet's rows (a tab between cells, a line feed after each row, a cell
    /// of more lines or of tabs or quotes in quotes, its quotes doubled; a cell merged over more places at its first, the others empty),
    /// and their markup as a table of their own (see WordDocument.CopyCells); null without cells selected, or where the document keeps
    /// them so not.
    /// </summary>
    Fragment? CopiedCells()
    {
        if (CellSelection is not { } range || document is not WordDocument word || word.CopyCells(range.Table, range.Top, range.Bottom, range.Left, range.Right) is not { } markup)
            return null;
        var cells = CellsIn(range);
        var text = new System.Text.StringBuilder();
        for (var r = range.Top; r < range.Bottom; r++)
        {
            for (var c = range.Left; c < range.Right; c++)
            {
                if (c > range.Left)
                    text.Append('\t');
                if (cells.FirstOrDefault(k => k.Top == r && k.Left == c) is { } cell)
                    text.Append(Field(string.Join("\n", cell.Paragraphs.Select(p => document.Text(p).Replace("￼", "", StringComparison.Ordinal)))));
            }
            text.Append("\r\n");
        }
        return new Fragment(text.ToString(), [markup], document, Cells: true);

        static string Field(string text) => text.IndexOfAny(['\t', '\n', '"']) >= 0 ? "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : text;
    }

    /// <summary>
    /// Cells copied of this document put in, a handling of its own: in a table over its cells from the first of the cells acted on on
    /// (see WordDocument.PasteCells), the cells they went over selected then; at the start of a paragraph right after a table, as rows at
    /// its end (the two would meet, and Word joins tables that meet); elsewhere in the body as a table of their own in place of the
    /// selection, as a table is put in (see InsertTable), the cursor after it. False where they are another document's, or cannot go
    /// there.
    /// </summary>
    bool PastedCells(Fragment fragment)
    {
        if (fragment.Markup is not [var markup] || !ReferenceEquals(fragment.From, document) || document is not WordDocument word)
            return false;
        try
        {
            if (CellsActed is { } at)
            {
                Act(false, () =>
                {
                    var went = word.PasteCells(at.Table, at.Top, at.Left, markup);
                    return Spanning(went);
                });
                return true;
            }
            Act(false, () =>
            {
                var (paragraph, offset) = DeleteSelection(typing: false);
                if (offset == 0 && word.TableBefore(paragraph) is { } above && word.CellsOf(above) is { } joined)
                    return Spanning(word.PasteCells(above, joined.Rows, 0, markup));
                if (offset > 0)
                    paragraph = document.Split(paragraph, offset);
                document.InsertBlocks(paragraph, markup, before: true);
                return Same(new TextPlace(paragraph, 0));
            });
            return true;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Text of a spreadsheet's rows (see CopiedCells) put over a table's cells from the first of the cells acted on on (see
    /// WordDocument.PasteTexts), a handling of its own, the cells they went over selected then: rows of as many cells each, more than one,
    /// apart by tabs, each row ended by a line end as a spreadsheet copies them, or more than one row. False outside tables, and for other
    /// text (a tab in a line goes in as text, as Word puts it).
    /// </summary>
    bool PastedTexts(string text)
    {
        if (CellsActed is not { } at || document is not WordDocument word)
            return false;
        var rows = Rows(text);
        if (rows.Count == 0 || rows[0].Count < 2 || rows.Any(r => r.Count != rows[0].Count) || rows.Count == 1 && !text.EndsWith('\n') && !text.EndsWith('\r'))
            return false;
        try
        {
            Act(false, () => Spanning(word.PasteTexts(at.Table, at.Top, at.Left, rows)));
            return true;
        }
        catch (NotSupportedException)
        {
            // Taken back (see Act), as where changes are tracked: the text goes in as text.
            return false;
        }
    }

    /// <summary>The cursor and the anchor that select cells of a table together (see Select), as a handling leaves them.</summary>
    (TextPlace Caret, TextPlace Anchor) Spanning(CellRange range)
    {
        if (CellsIn(range) is not [var first, ..] cells)
            return (Caret, Anchor);
        if (cells.Count == 1)
            return Same(new TextPlace(first.First, 0));
        var last = cells[^1];
        return (new TextPlace(last.Last, document.Text(last.Last).Length), new TextPlace(first.First, 0));
    }

    /// <summary>
    /// A spreadsheet's rows as their cells' texts (as Excel copies them): cells apart by tabs, rows by line ends (one at the end, none
    /// after), a cell begun with a quote quoted up to its closing one (its tabs and line ends its own, a quote doubled one).
    /// </summary>
    static List<IReadOnlyList<string>> Rows(string text)
    {
        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var cell = new System.Text.StringBuilder();
        var i = 0;
        while (i < text.Length)
        {
            if (cell.Length == 0 && text[i] == '"')
            {
                // A quoted cell, up to its closing quote: its line ends one each.
                var j = i + 1;
                for (; j < text.Length; j++)
                {
                    if (text[j] == '"')
                    {
                        if (j + 1 >= text.Length || text[j + 1] != '"')
                            break;
                        j++;
                    }
                    else if (text[j] == '\r' && j + 1 < text.Length && text[j + 1] == '\n')
                        continue;
                    cell.Append(text[j] == '\r' ? '\n' : text[j]);
                }
                i = j + 1;
                continue;
            }
            var c = text[i++];
            if (c == '\t')
            {
                row.Add(cell.ToString());
                cell.Clear();
            }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i < text.Length && text[i] == '\n')
                    i++;
                row.Add(cell.ToString());
                cell.Clear();
                rows.Add(row);
                row = [];
            }
            else
                cell.Append(c);
        }
        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }
        return rows;
    }

    /// <summary>The cells of a table that a rectangle of its grid holds, in reading order; none where the table is gone.</summary>
    public IReadOnlyList<TableCell> CellsIn(CellRange range) => document is WordDocument word && word.CellsOf(range.Table) is { } cells
        ? [.. cells.Cells.Where(c => c.Top < range.Bottom && range.Top < c.Bottom && c.Left < range.Right && range.Left < c.Right)]
        : [];

    /// <summary>What a handling of cells acts on: the cells selected (see CellSelection), else the cursor's cell; null outside tables.</summary>
    public CellRange? CellsActed => CellSelection ?? (Table is { } table && Cell is { } cell ? new CellRange(table.Table, cell.Top, cell.Bottom, cell.Left, cell.Right) : null);

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
    /// Rows put in above the cells acted on (see CellsActed) or below them, as many as they span (as Word puts in as many as are selected),
    /// like their first or last row; the cursor then in the first new row's cell of their first column.
    /// </summary>
    public bool InsertRow(bool above) => CellsActed is { } cells
        && ChangeTable(word => word.AddRows(cells.Table, above ? cells.Top : cells.Bottom, cells.Bottom - cells.Top, like: above ? cells.Top : cells.Bottom - 1, column: cells.Left));

    /// <summary>Columns put in left of the cells acted on or right of them, as many as they span, like their first or last column, the table as wide as it was; the cursor then in the first new column's cell of their first row.</summary>
    public bool InsertColumn(bool left) => CellsActed is { } cells
        && ChangeTable(word => word.AddColumns(cells.Table, left ? cells.Left : cells.Right, cells.Right - cells.Left, like: left ? cells.Left : cells.Right - 1, row: cells.Top));

    /// <summary>The rows of the cells acted on taken away (all of them, the table), the cursor then in the row after them.</summary>
    public bool RemoveRow() => CellsActed is { } cells && ChangeTable(word => word.RemoveRows(cells.Table, cells.Top, cells.Bottom));

    /// <summary>The columns of the cells acted on taken away, the table as wide as it was (all of them, the table).</summary>
    public bool RemoveColumn() => CellsActed is { } cells && ChangeTable(word => word.RemoveColumns(cells.Table, cells.Left, cells.Right));

    /// <summary>
    /// Where the text stands in the cells acted on as the page shows them, whichever way each runs its text (see WordDocument.PlaceText):
    /// up and down, top, center or bottom, and across, left, center or right; null leaves either.
    /// </summary>
    public bool PlaceText(string? vertical, string? horizontal) => CellsActed is { } cells && ChangeTable(word =>
    {
        word.PlaceText(cells.Table, cells.Top, cells.Bottom, cells.Left, cells.Right, vertical, horizontal, shown: true);
        return null;
    });

    /// <summary>Where the page shows the text of the first of the cells acted on (see WordDocument.CellPlace); null outside tables.</summary>
    public (string Vertical, string Horizontal)? CellPlace => document is WordDocument word && CellsActed is { } cells
        && CellsIn(cells) is [var first, ..] ? word.CellPlace(cells.Table, first.Top, first.Left, shown: true) : null;

    /// <summary>The text of the cells acted on turned a quarter: btLr up the page, tbRl down it, null across again (see WordDocument.TurnText).</summary>
    public bool TurnText(string? direction) => CellsActed is { } cells && ChangeTable(word =>
    {
        word.TurnText(cells.Table, cells.Top, cells.Bottom, cells.Left, cells.Right, direction);
        return null;
    });

    /// <summary>Which way the first of the cells acted on runs its text (see WordDocument.CellDirection): btLr, tbRl, or null across; null outside tables too.</summary>
    public string? CellDirection => document is WordDocument word && CellsActed is { } cells
        && CellsIn(cells) is [var first, ..] ? word.CellDirection(cells.Table, first.Top, first.Left) : null;

    /// <summary>The cursor's table floating beside the text, left (null), right or centered in its column (see WordDocument.FloatTable).</summary>
    public bool FloatTable(string? across) => Table is { } table && ChangeTable(word =>
    {
        word.FloatTable(table.Table, across);
        return null;
    });

    /// <summary>
    /// A floating table put where its top left is to stand at a point of a page (see WordDocument.PlaceTable), a handling of its own; the
    /// cursor and the selection stay. False where it does not float, or in other documents than Word's.
    /// </summary>
    public bool PlaceTable(int table, int page, double x, double y, string? alignAcross = null)
    {
        if (document is not WordDocument word || word.TableFloat(table) == null)
            return false;
        try
        {
            Act(false, () =>
            {
                word.PlaceTable(table, page, x, y, alignAcross);
                return (Caret, Anchor);
            });
            return true;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// A table moved in the text to stand right before a paragraph of the body (see WordDocument.MoveTable), a handling of its own; the
    /// cursor and the selection stay. False where it cannot go there, or in other documents than Word's.
    /// </summary>
    public bool MoveTable(int table, int before)
    {
        if (document is not WordDocument word)
            return false;
        try
        {
            Act(false, () =>
            {
                word.MoveTable(table, before);
                return (Caret, Anchor);
            });
            return true;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>The cells of the rows of the cells acted on merged into one, with what reaches into them (see WordDocument.MergeAround).</summary>
    public bool MergeRow() => CellsActed is { } cells && Table is { } table && ChangeTable(word => word.MergeAround(cells.Table, cells.Top, cells.Bottom, 0, table.Columns));

    /// <summary>The cells selected merged into one, as Word's Merge Cells (see WordDocument.MergeCells); false without cells selected.</summary>
    public bool MergeCells() => CellSelection is { } cells && ChangeTable(word => word.MergeCells(cells.Table, cells.Top, cells.Bottom, cells.Left, cells.Right));

    /// <summary>What the cells acted on hold taken away, as Word's Delete over cells (see WordDocument.ClearCells), the cursor then in the first of them.</summary>
    public bool ClearCells() => CellsActed is { } cells && ChangeTable(word => word.ClearCells(cells.Table, cells.Top, cells.Bottom, cells.Left, cells.Right));

    /// <summary>
    /// The cursor's cell split into so many of even width side by side, or (<paramref name="across"/>) of even height one below the other,
    /// as the pen splits cells (see WordDocument.SplitCell); the cursor then in the first.
    /// </summary>
    public bool SplitCell(bool across, int parts) => Table is { } table && Cell is { } cell
        && ChangeTable(word => word.SplitCell(table.Table, cell.Top, cell.Left, across, parts));

    /// <summary>The cells acted on shaded with a color (hex RRGGBB), or without one of their own (null; see WordDocument.ShadeCells).</summary>
    public bool ShadeCells(string? fill) => CellsActed is { } cells && ChangeTable(word =>
    {
        word.ShadeCells(cells.Table, cells.Top, cells.Bottom, cells.Left, cells.Right, fill);
        return null;
    });

    /// <summary>Lines of the cells acted on given a line of their own, or as the table's look gives them (null; see WordDocument.SetLines).</summary>
    public bool SetCellLines(CellLines which, CellBorder? border) => CellsActed is { } cells && ChangeTable(word =>
    {
        word.SetLines(cells.Table, cells.Top, cells.Bottom, cells.Left, cells.Right, which, border);
        return null;
    });

    /// <summary>A stretch of a line of a table's grid given a line of its own, or as the table's look gives it (null; see WordDocument.SetLine).</summary>
    public bool SetTableLine(int table, bool upright, int line, int from, int to, CellBorder? border) => ChangeTable(word =>
    {
        word.SetLine(table, upright, line, from, to, border);
        return null;
    });

    /// <summary>The cursor's table aligned centered, right, or left (null; see WordDocument.AlignTable).</summary>
    public bool AlignTable(string? alignment) => Table is { } table && ChangeTable(word =>
    {
        word.AlignTable(table.Table, alignment);
        return null;
    });

    /// <summary>The cursor's table as wide as the text again, at its left edge (see WordDocument.WidenTable).</summary>
    public bool WidenTable() => Table is { } table && ChangeTable(word =>
    {
        word.WidenTable(table.Table);
        return null;
    });

    /// <summary>The rows after the head sorted by the column of the cursor's cell (see WordDocument.SortRows), the cursor then in the first row sorted.</summary>
    public bool SortRows(bool descending) => Table is { } table && Cell is { } cell
        && ChangeTable(word => word.SortRows(table.Table, cell.Left, table.HeadRow ? 1 : 0, descending));

    /// <summary>The cursor's table turned, its rows its columns (see WordDocument.Transpose).</summary>
    public bool TransposeTable() => Table is { } table && ChangeTable(word => word.Transpose(table.Table));

    /// <summary>The columns of the cursor's table made even (see WordDocument.DistributeColumns).</summary>
    public bool DistributeColumns() => Table is { } table && ChangeTable(word =>
    {
        word.DistributeColumns(table.Table);
        return null;
    });

    /// <summary>The cursor's table in a table style (see WordDocument.StyleTable).</summary>
    public bool StyleTable(string style) => Table is { } table && ChangeTable(word =>
    {
        word.StyleTable(table.Table, style);
        return null;
    });

    /// <summary>The cursor's table with a head row, or without a head (see WordDocument.SetHead).</summary>
    public bool SetHead(bool on) => Table is { } table && ChangeTable(word =>
    {
        word.SetHead(table.Table, on);
        return null;
    });

    /// <summary>The cursor's table taken away, the cursor then after it.</summary>
    public bool RemoveTable() => Table is { } table && ChangeTable(word => word.RemoveTable(table.Table));

    /// <summary>
    /// A change of a table of the body as a whole (see WordDocument's changes of tables), a handling of its own: <paramref name="change"/>
    /// makes it and names the paragraph to go on in, the cursor then at its start; where it names none, the cursor stays (a table's
    /// lines moved keep its paragraphs). False in other documents than Word's.
    /// </summary>
    /// <summary>Whether the cursor's table stands alone on pages turned across (see WordDocument.TableAcross); false outside tables.</summary>
    public bool TableAcross => document is WordDocument word && Table is { } table && word.TableAcross(table.Table);

    /// <summary>
    /// The cursor's table put on pages of its own turned across (landscape), or back among the pages of the text around it (see
    /// WordDocument.TurnTablePages); the cursor stays. False outside tables or in other documents than Word's.
    /// </summary>
    public bool TurnTablePages(bool across) => Table is { } table && ChangeTable(word =>
    {
        word.TurnTablePages(table.Table, across);
        return null;
    });

    public bool ChangeTable(Func<WordDocument, int?> change)
    {
        if (document is not WordDocument word)
            return false;
        Act(false, () => change(word) is { } paragraph ? Same(new TextPlace(paragraph, 0)) : (Caret, Anchor));
        return true;
    }
}
