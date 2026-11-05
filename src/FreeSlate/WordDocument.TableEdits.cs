using System.Globalization;
using System.Text;
using FreeSlate.Docx;

namespace FreeSlate;

/// <summary>
/// The tables of a Word document as an editor shapes them (catalog: table.structure): put in as Word's Insert Table puts one, their cells
/// as the rectangles of the grid they cover, and changed as a whole, each change one version: rows and columns put in, taken out and
/// moved, cells merged and split, the grid's lines moved, rows made higher, the rows sorted by a column, the rows turned into the
/// columns (see TextEdits.TableShape). A table is known by its block's id, which it keeps through these changes; its paragraphs may
/// not (a change that makes more or fewer cells reads them anew), so each change says where to go on, a paragraph of a cell.
/// </summary>
public sealed partial class WordDocument
{
    /// <summary>
    /// The markup of a new table as Word's Insert Table writes one (measured in Word 365 by COM, scratchpad tables/insert.vbs and
    /// widths.vbs: Tables.Add with Word 9's behaviour and fixed widths, the dialog's defaults alike): a table style, an automatic
    /// width, its columns of even width over the width of the text the paragraph stands in less 10 twips (whole twips, the rest one
    /// each to the last columns), each cell as wide, with an empty paragraph. Here the paragraphs of a style for the text in cells
    /// where one is given; the first row its head, repeated on each page the table goes on to, and only it marked as Word's look
    /// says (Word marks its first column too), so that the head can become the first column (see Transpose).
    /// </summary>
    public string TableMarkup(int paragraph, int columns, int rows, string? style = null, string? textStyle = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);
        var width = (int)Math.Round(TextWidth(paragraph) * 20) - 10;
        var grid = TextEdits.TableShape.Even(Math.Max(columns * 20, width), columns);
        var markup = new StringBuilder("<w:tbl><w:tblPr>");
        if (style != null)
            markup.Append("<w:tblStyle w:val=\"").Append(System.Security.SecurityElement.Escape(style)).Append("\"/>");
        markup.Append("<w:tblW w:w=\"0\" w:type=\"auto\"/>")
            .Append("<w:tblLook w:val=\"0420\" w:firstRow=\"1\" w:lastRow=\"0\" w:firstColumn=\"0\" w:lastColumn=\"0\" w:noHBand=\"0\" w:noVBand=\"1\"/></w:tblPr><w:tblGrid>");
        foreach (var column in grid)
            markup.Append("<w:gridCol w:w=\"").Append(column.ToString(CultureInfo.InvariantCulture)).Append("\"/>");
        markup.Append("</w:tblGrid>");
        var cell = textStyle != null ? $"<w:p><w:pPr><w:pStyle w:val=\"{System.Security.SecurityElement.Escape(textStyle)}\"/></w:pPr></w:p>" : "<w:p/>";
        for (var r = 0; r < rows; r++)
        {
            markup.Append(r == 0 ? "<w:tr><w:trPr><w:tblHeader/></w:trPr>" : "<w:tr>");
            foreach (var column in grid)
                markup.Append("<w:tc><w:tcPr><w:tcW w:w=\"").Append(column.ToString(CultureInfo.InvariantCulture)).Append("\" w:type=\"dxa\"/></w:tcPr>").Append(cell).Append("</w:tc>");
            markup.Append("</w:tr>");
        }
        return markup.Append("</w:tbl>").ToString();
    }

    /// <summary>
    /// The width of the text a paragraph of the body stands in, points: its section's column (columns of even width); in a table's
    /// cell the cell's width less its margins (Word's of 5.4 points each side).
    /// </summary>
    public double TextWidth(int paragraph)
    {
        if (TableOf(paragraph) is { } table && CellsOf(table) is { } cells && cells.Cells.FirstOrDefault(c => c.Paragraphs.Contains(paragraph)) is { } cell)
        {
            var (model, _) = TableById(table);
            var grid = model.Table.Grid;
            var width = Enumerable.Range(cell.Left, cell.Right - cell.Left).Sum(k => k < grid.Count ? grid[k] : 0);
            return Math.Max(36, width - 2 * 5.4);
        }
        return ColumnWidth(paragraph);
    }

    /// <summary>The width of the column of its section a paragraph of the body stands in, points (columns of even width), in a table's cell or not.</summary>
    double ColumnWidth(int paragraph)
    {
        var section = SectionOf(paragraph);
        var count = Math.Max(1, section.Columns ?? 1);
        return Math.Max(36, ((section.PageWidth ?? 595.3) - (section.Left ?? 72) - (section.Right ?? 72) - (section.Gutter ?? 0) - (count - 1) * (section.ColumnSpace ?? 36)) / count);
    }

    /// <summary>
    /// The table of the body a paragraph stands in, the innermost one where tables stand in cells of tables: its block's id; null for
    /// a paragraph outside tables, in a text box, a note, a header or a footer.
    /// </summary>
    public int? TableOf(int paragraph)
    {
        if (!paragraphs.TryGetValue(paragraph, out var found) || found.Top is not TableBlock)
            return null;
        var path = PathOf(found.Top, found.Paragraph);
        for (var level = path.Count - 1; level >= 0; level--)
        {
            switch (path[level].List[path[level].Index])
            {
                case TableBlock table:
                    return table.Id;
                // A paragraph holding the one asked about in a text box: not a table's text.
                case ParagraphBlock holder when !ReferenceEquals(holder, found.Paragraph):
                    return null;
            }
        }
        return null;
    }

    /// <summary>
    /// The table of the body a paragraph of the body follows right after, nothing between them (a table put in before the paragraph would
    /// join it, as Word joins tables that meet): its block's id; null where the paragraph follows none, or is not the body's own.
    /// </summary>
    public int? TableBefore(int paragraph)
    {
        if (!paragraphs.TryGetValue(paragraph, out var found) || found.Top is not ParagraphBlock)
            return null;
        var index = blocks.IndexOf(found.Top);
        return index > 0 && blocks[index - 1] is TableBlock table ? table.Id : null;
    }

    /// <summary>A table of the body by its block's id, with the block of the body it stands in (itself, or a table it is nested in).</summary>
    (TableBlock Table, Block Top) TableById(int table)
    {
        TableBlock? Search(Block block) => block is TableBlock t && t.Id == table ? t : ModelReader.Inner(block).SelectMany(list => list).Select(Search).FirstOrDefault(found => found != null);
        foreach (var top in blocks)
        {
            if (top is TableBlock && Search(top) is { } found)
                return (found, top);
        }
        throw new ArgumentException("No table of the body has the id.", nameof(table));
    }

    /// <summary>
    /// A table's cells as the rectangles of its grid they cover (see TableCell), in reading order (row by row, a cell merged down in
    /// the row it begins in), with its numbers of rows and of the grid's columns; null where no table of the body has the id.
    /// </summary>
    public TableCells? CellsOf(int table)
    {
        TableBlock model;
        try
        {
            model = TableById(table).Table;
        }
        catch (ArgumentException)
        {
            return null;
        }
        var cells = new List<(int Top, int Bottom, int Left, int Right, CellModel Cell)>();
        var rows = model.Table.Rows;
        for (var r = 0; r < rows.Count; r++)
        {
            foreach (var cell in rows[r].Cells)
            {
                var (left, right) = (cell.Column, cell.Column + Math.Max(1, cell.Format.GridSpan));
                var above = cell.MergedAbove ? cells.FindLastIndex(c => c.Bottom == r && c.Left == left && c.Right == right) : -1;
                if (above >= 0)
                    cells[above] = cells[above] with { Bottom = r + 1 };
                else
                    cells.Add((r, r + 1, left, right, cell));
            }
        }
        var columns = Math.Max(model.Table.Grid.Count, cells.Count > 0 ? cells.Max(c => c.Right) : 0);
        var made = new List<TableCell>();
        foreach (var (top, bottom, left, right, cell) in cells)
        {
            var paragraphs = cell.Blocks.SelectMany(Flat).Select(p => p.Id).ToList();
            if (paragraphs.Count == 0)
                continue;
            var last = cell.Blocks.OfType<ParagraphBlock>().LastOrDefault()?.Id ?? paragraphs[^1];
            var empty = cell.Blocks.All(b => b is ParagraphBlock p && ParagraphText.Of(p).Length == 0);
            made.Add(new TableCell(top, bottom, left, right, paragraphs[0], last, paragraphs, empty));
        }
        var look = model.Element.Element(model.Element.Name.Namespace + "tblPr")?.Element(model.Element.Name.Namespace + "tblLook");
        bool Marked(string name, int bit)
        {
            var w = model.Element.Name.Namespace;
            if (look?.Attribute(w + name) is { } given)
                return given.Value is "1" or "true" or "on";
            return look?.Attribute(w + "val") is { } val && int.TryParse(val.Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bits) && (bits & bit) != 0;
        }
        return new TableCells(table, rows.Count, columns, made, model.Table.Format.BidiVisual, Marked("firstRow", 0x20), Marked("firstColumn", 0x80));
    }

    /// <summary>
    /// Changes a table of the body as a whole (see TextEdits.TableShape), one change: its shape read, changed and written back. Returns
    /// the paragraph to go on in: the first of the cell the change names, read anew, or the first of the table's first cell.
    /// </summary>
    /// <exception cref="NotSupportedException">The table is not one of plain rows and cells, or changes are tracked; nothing changes.</exception>
    int Reshape(int table, Func<TextEdits.TableShape, TextEdits.TableShape.Cell?> change, bool widths = false)
    {
        if (Author != null)
            throw new NotSupportedException("Tables are not changed as a whole as a tracked change.");
        var (model, top) = TableById(table);
        var at = -1;
        var where = Change(top, top, (stretch, _) =>
        {
            var (text, named) = TextEdits.Reshape(stretch, model, change, widths);
            at = named;
            return text;
        });
        if (at >= 0 && Fresh().FirstOrDefault(p => (p.Parts ?? [p.Element]).Any(e => where(e) == at)) is { } paragraph)
            return paragraph.Id;
        return CellsOf(table) is { Cells: [var first, ..] } ? first.First : throw new InvalidOperationException("The table changed has no cell.");
    }

    /// <summary>
    /// Puts rows into a table before row <paramref name="at"/> (at its end where it is the count), each with cells like those of row
    /// <paramref name="like"/>, as Word's Insert Rows Above and Below do (see TextEdits.TableShape.InsertRows); returns the first
    /// paragraph of the first new row's first cell.
    /// </summary>
    public int AddRows(int table, int at, int count, int like, int column = 0) =>
        Reshape(table, shape => shape.InsertRows(at, count, like) is var made && made.Count > 0
            ? made.FirstOrDefault(c => c.Left <= column && column < c.Right) ?? made.MinBy(c => c.Left) : null);

    /// <summary>
    /// Puts columns into a table before grid column <paramref name="at"/> (at its end where it is the count), the table as wide as it
    /// was (see TextEdits.TableShape.InsertColumns); returns the first paragraph of the first new column's cell in row
    /// <paramref name="row"/>, else its first cell.
    /// </summary>
    public int AddColumns(int table, int at, int count, int like, int row = 0) =>
        Reshape(table, shape => shape.InsertColumns(at, count, like) is var made && made.Count > 0
            ? made.FirstOrDefault(c => c.Top <= row && row < c.Bottom) ?? made.MinBy(c => c.Top) : null);

    /// <summary>
    /// Takes rows of a table away (from one to before another); all of them, the table. Returns the paragraph to go on in: the first
    /// cell's of the row after them (before them at the table's end), or the paragraph after the table taken away.
    /// </summary>
    public int RemoveRows(int table, int from, int to)
    {
        var cells = CellsOf(table) ?? throw new ArgumentException("No table of the body has the id.", nameof(table));
        if (from <= 0 && to >= cells.Rows)
            return RemoveTable(table);
        var next = Math.Min(from, cells.Rows - (to - from) - 1);
        return Reshape(table, shape =>
        {
            shape.RemoveRows(from, to);
            return shape.At(next, 0) ?? shape.Cells.FirstOrDefault();
        });
    }

    /// <summary>Takes columns of a table away (from one to before another), the table as wide as it was; all of them, the table (see RemoveRows).</summary>
    public int RemoveColumns(int table, int from, int to)
    {
        var cells = CellsOf(table) ?? throw new ArgumentException("No table of the body has the id.", nameof(table));
        if (from <= 0 && to >= cells.Columns)
            return RemoveTable(table);
        var next = Math.Min(from, cells.Columns - (to - from) - 1);
        return Reshape(table, shape =>
        {
            shape.RemoveColumns(from, to);
            return shape.At(0, next) ?? shape.Cells.FirstOrDefault();
        });
    }

    /// <summary>
    /// Takes a table away whole; returns the paragraph after it, where the cursor goes on (a table never ends its body or cell).
    /// </summary>
    public int RemoveTable(int table)
    {
        var (model, top) = TableById(table);
        var after = Next(Flat(model)[^1].Id) ?? throw new NotSupportedException("No paragraph follows the table.");
        var gone = Flat(model).Select(p => p.Id).ToHashSet();
        Change(top, top, (stretch, tracking) =>
        {
            if (tracking != null)
                throw new NotSupportedException("A table is not taken away as a tracked change here.");
            var tbl = stretch.Find(model.Element) ?? throw new NotSupportedException("The table is not where it was read.");
            var start = stretch.Text.Start(tbl);
            return stretch.Spliced([(start, stretch.Text.End(start), "")]);
        }, () => ReferenceEquals(top, model) && Author == null ? [.. blocks.Take(IndexOf(top)), .. blocks.Skip(IndexOf(top) + 1)] : null);
        return gone.Contains(after) ? throw new InvalidOperationException("The paragraph after the table was in it.") : after;
    }

    /// <summary>
    /// Merges the cells of a rectangle of a table's grid into one, as Word does (see TextEdits.TableShape.Merge); returns its first paragraph.
    /// </summary>
    /// <exception cref="NotSupportedException">A cell reaches out of the rectangle, or a row does not reach into it; nothing changes.</exception>
    public int MergeCells(int table, int top, int bottom, int left, int right) =>
        Reshape(table, shape => shape.Merge(top, bottom, left, right));

    /// <summary>
    /// Splits cells of a table along an upright line, as a pen drawn down through them does (see TextEdits.TableShape.SplitDown): at grid
    /// line <paramref name="line"/>, or with <paramref name="part"/> that many points into grid column <paramref name="line"/>. Each
    /// cell is named by a place of the grid it covers. Returns the first paragraph of the first new cell.
    /// </summary>
    public int SplitDown(int table, IReadOnlyList<(int Row, int Column)> cells, int line, double? part = null) =>
        Reshape(table, shape => shape.SplitDown(Named(shape, cells), line, part is { } points ? InTwips(points) : null).OrderBy(c => c.Top).FirstOrDefault());

    /// <summary>
    /// Splits cells of a table along a line across, as a pen drawn through them does (see TextEdits.TableShape.SplitAcross): at the line
    /// before row <paramref name="line"/>, or with <paramref name="part"/> that many points down into row <paramref name="line"/>, the
    /// row then as high as <paramref name="height"/> together (its two rows at least as the line parts it). Returns the first paragraph
    /// of the first new cell.
    /// </summary>
    public int SplitAcross(int table, IReadOnlyList<(int Row, int Column)> cells, int line, double? part = null, double? height = null) =>
        Reshape(table, shape => shape.SplitAcross(Named(shape, cells), line, part is { } points ? InTwips(points) : null, height is { } whole ? InTwips(whole) : null)
            .OrderBy(c => c.Left).FirstOrDefault());

    /// <summary>
    /// Splits the cell covering a place of a table's grid into so many of even width side by side, or (<paramref name="across"/>) one
    /// below the other, as the pen splits cells (see TextEdits.TableShape.Split); returns its first paragraph.
    /// </summary>
    /// <exception cref="NotSupportedException">The cell spans rows that do not part so evenly; nothing changes.</exception>
    public int SplitCell(int table, int row, int column, bool across, int parts) =>
        Reshape(table, shape => shape.Split(shape.At(row, column) ?? throw new ArgumentException("No cell covers the place.", nameof(row)), across, parts));

    /// <summary>
    /// Takes a line between a table's cells away, as Word's eraser does (see TextEdits.TableShape.Erase): an upright one, grid line
    /// <paramref name="line"/>, through rows from <paramref name="from"/> to before <paramref name="to"/>; one across, the line before
    /// row <paramref name="line"/>, through grid columns; the cells on either side merge. Returns the first paragraph of the first cell
    /// merged.
    /// </summary>
    public int EraseLine(int table, bool upright, int line, int from, int to) =>
        Reshape(table, shape => shape.Erase(upright, line, from, to));

    /// <summary>
    /// Merges the cells of a rectangle of a table's grid and every cell that reaches into it, into the smallest rectangle that holds them
    /// (see TextEdits.TableShape.Enclosing); returns its first paragraph.
    /// </summary>
    public int MergeAround(int table, int top, int bottom, int left, int right) =>
        Reshape(table, shape =>
        {
            var (t, b, l, r) = shape.Enclosing(top, bottom, left, right);
            return shape.Merge(t, b, l, r);
        });

    static List<TextEdits.TableShape.Cell> Named(TextEdits.TableShape shape, IReadOnlyList<(int Row, int Column)> cells) =>
        [.. cells.Select(c => shape.At(c.Row, c.Column) ?? throw new ArgumentException("No cell covers a place named.", nameof(cells))).Distinct()];

    /// <summary>
    /// Moves a line of a table's grid by so many points to the right as it shows (to the left in a table from the right), as dragging it
    /// does in Word (see TextEdits.TableShape.MoveLine): the column right of an inside line giving way, the outside ones moving the
    /// table's edges; no column narrower than <paramref name="least"/> points. The paragraphs stay as they are.
    /// </summary>
    public void MoveLine(int table, int line, double by, double least = 12)
    {
        var (model, _) = TableById(table);
        var delta = InTwips(model.Table.Format.BidiVisual ? -by : by);
        Reshape(table, shape =>
        {
            // A table centered grows and shrinks on both sides alike: its side moved goes as far as the pointer twice as wide.
            var moved = shape.Alignment == "center" && (line == 0 || line == shape.Columns) ? 2 * delta : delta;
            var at = line == 0 ? moved : shape.Line(line) + moved;
            shape.MoveLine(line, at, InTwips(least));
            return null;
        }, widths: true);
    }

    /// <summary>
    /// Gives a stretch of a line of a table's grid a line of its own, as Word does where a side of cells is given one (see
    /// TextEdits.TableShape.SetLine): an upright one, grid line <paramref name="line"/>, through rows from <paramref name="from"/> to
    /// before <paramref name="to"/>; one across, the line before row <paramref name="line"/>, through grid columns; null, the line as
    /// the table's look gives it. The paragraphs stay as they are.
    /// </summary>
    public void SetLine(int table, bool upright, int line, int from, int to, CellBorder? border) =>
        Reshape(table, shape =>
        {
            shape.SetLine(upright, line, from, to, border);
            return null;
        }, widths: true);

    /// <summary>
    /// Gives lines of a rectangle of a table's grid a line of their own, as Word's Borders of cells selected do (see
    /// TextEdits.TableShape.SetLines); null, the lines as the table's look gives them. The paragraphs stay as they are.
    /// </summary>
    public void SetLines(int table, int top, int bottom, int left, int right, CellLines which, CellBorder? border) =>
        Reshape(table, shape =>
        {
            shape.SetLines(top, bottom, left, right, which, border);
            return null;
        }, widths: true);

    /// <summary>
    /// The cells reaching into a rectangle of a table's grid shaded with a color (hex RRGGBB), as Word's Shading of cells writes it;
    /// null, without a shading of their own (see TextEdits.TableShape.Shade). The paragraphs stay as they are.
    /// </summary>
    public void ShadeCells(int table, int top, int bottom, int left, int right, string? fill) =>
        Reshape(table, shape =>
        {
            shape.Shade(top, bottom, left, right, fill);
            return null;
        }, widths: true);

    /// <summary>
    /// A table aligned between the text's edges: centered ("center"), right ("right"), or left (null), as Word's Rows.Alignment does
    /// (see TextEdits.TableShape.Align). The paragraphs stay as they are.
    /// </summary>
    public void AlignTable(int table, string? alignment) =>
        Reshape(table, shape =>
        {
            shape.Align(alignment);
            return null;
        }, widths: true);

    /// <summary>
    /// A table as wide as the text of its section's column again, less 10 twips as Word puts one in (see TableMarkup), its columns in
    /// proportion, at the text's left edge. The paragraphs stay as they are.
    /// </summary>
    public void WidenTable(int table)
    {
        var cells = CellsOf(table) ?? throw new ArgumentException("No table of the body has the id.", nameof(table));
        var whole = (int)Math.Round(ColumnWidth(cells.Cells[0].First) * 20) - 10;
        Reshape(table, shape =>
        {
            shape.Widen(Math.Max(shape.Columns * 20, whole));
            return null;
        }, widths: true);
    }

    /// <summary>
    /// What the cells reaching into a rectangle of a table's grid hold taken away, as Word's Delete over cells selected (see
    /// TextEdits.TableShape.Clear); returns the first paragraph of the first of them.
    /// </summary>
    public int ClearCells(int table, int top, int bottom, int left, int right) =>
        Reshape(table, shape => shape.Clear(top, bottom, left, right), widths: true);

    /// <summary>
    /// Where the text stands in the cells reaching into a rectangle of a table's grid, as the nine buttons of Word's Layout tab put it
    /// (see TextEdits.TableShape.Place): <paramref name="vertical"/> top, center or bottom, <paramref name="horizontal"/> left, center or
    /// right; null leaves either as it is. Where the styles would put a cell or a paragraph elsewhere than at the top or the left asked
    /// for, these are written too, as Word writes what differs from the styles. The paragraphs stay as they are.
    /// </summary>
    public void PlaceText(int table, int top, int bottom, int left, int right, string? vertical, string? horizontal)
    {
        Reshape(table, shape =>
        {
            shape.Place(top, bottom, left, right, vertical, horizontal);
            return null;
        }, widths: true);
        if (vertical != "top" && horizontal != "left" || CellsOf(table) is not { } cells)
            return;
        var (model, _) = TableById(table);
        var spelled = cells.Cells.Where(c => c.Top < bottom && top < c.Bottom && c.Left < right && left < c.Right)
            .Select(c => (Cell: c, Up: vertical == "top" && CellModelAt(model, c) is { } m && m.Format.VerticalAlign is "center" or "bottom",
                Along: horizontal == "left" && c.Paragraphs.Any(p => Find(p).Paragraph.Format.Justification is not (null or "left" or "start"))))
            .Where(s => s.Up || s.Along).ToList();
        if (spelled.Count == 0)
            return;
        Reshape(table, shape =>
        {
            foreach (var (cell, up, along) in spelled)
                shape.Place(cell.Top, cell.Top + 1, cell.Left, cell.Left + 1, up ? "top" : null, along ? "left" : null, spelled: true);
            return null;
        }, widths: true);
    }

    /// <summary>
    /// Where the text stands in a table's cell as its styles and its own properties put it: up and down (top, center or bottom) and along
    /// its first paragraph's lines (left, center or right); null where no cell begins at the place.
    /// </summary>
    public (string Vertical, string Horizontal)? CellPlace(int table, int row, int column)
    {
        if (CellsOf(table)?.Cells.FirstOrDefault(c => c.Top == row && c.Left <= column && column < c.Right) is not { } cell)
            return null;
        var (model, _) = TableById(table);
        var vertical = CellModelAt(model, cell)?.Format.VerticalAlign switch
        {
            "center" => "center",
            "bottom" => "bottom",
            _ => "top",
        };
        var horizontal = Find(cell.First).Paragraph.Format.Justification switch
        {
            "center" => "center",
            "right" or "end" => "right",
            _ => "left",
        };
        return (vertical, horizontal);
    }

    /// <summary>The model of a cell where it begins, the first row it covers.</summary>
    static CellModel? CellModelAt(TableBlock model, TableCell cell) =>
        cell.Top < model.Table.Rows.Count ? model.Table.Rows[cell.Top].Cells.FirstOrDefault(c => c.Column == cell.Left) : null;

    /// <summary>
    /// The cells of a rectangle of a table's grid (each reaching into it whole, as cells selected do) copied as a table of their own in
    /// WordprocessingML (see TextEdits.TableShape.Part), to be put over cells again (see PasteCells) or in as a table (see InsertBlocks).
    /// Null where the table is not one of plain rows and cells, and in a document of Strict names.
    /// </summary>
    public string? CopyCells(int table, int top, int bottom, int left, int right)
    {
        Read();
        if (IsStrictPart(context.MainPart))
            return null;
        try
        {
            var (model, block) = TableById(table);
            return TextEdits.CopyCells(StretchOf(IndexOf(block), IndexOf(block)), model, top, bottom, left, right);
        }
        catch (Exception e) when (e is NotSupportedException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Cells copied (see CopyCells) put over a table's cells from a place of its grid on, as Word's Overwrite Cells puts them (see
    /// TextEdits.TableShape.Overwrite): rows and columns added as they need, the table's lines its own. Returns the cells they went over.
    /// </summary>
    public CellRange PasteCells(int table, int row, int column, string copied)
    {
        var (_, block) = TableById(table);
        if (Elements(copied, RootOf(PartOf(block))) is not [{ Name.LocalName: "tbl" } tbl])
            throw new ArgumentException("The markup is not a table.", nameof(copied));
        var from = TextEdits.TableShape.Of(tbl);
        Reshape(table, shape => shape.Overwrite(row, column, from));
        return Enclosing(table, row, row + from.Rows.Count, column, column + from.Columns);
    }

    /// <summary>
    /// Texts put over a table's cells from a place of its grid on, rows of cells as a spreadsheet copies them (see
    /// TextEdits.TableShape.Overwrite): each cell its text's lines as paragraphs like its own. Returns the cells they went over.
    /// </summary>
    public CellRange PasteTexts(int table, int row, int column, IReadOnlyList<IReadOnlyList<string>> texts)
    {
        Reshape(table, shape => shape.Overwrite(row, column, texts));
        return Enclosing(table, row, row + texts.Count, column, column + (texts.Count > 0 ? texts.Max(r => r.Count) : 0));
    }

    /// <summary>The smallest rectangle of a table's grid that holds another and that each cell reaching into it reaches into whole.</summary>
    public CellRange Enclosing(int table, int top, int bottom, int left, int right)
    {
        var cells = CellsOf(table)?.Cells ?? [];
        for (var grown = true; grown;)
        {
            grown = false;
            foreach (var cell in cells.Where(c => c.Top < bottom && top < c.Bottom && c.Left < right && left < c.Right))
            {
                if (cell.Top >= top && cell.Bottom <= bottom && cell.Left >= left && cell.Right <= right)
                    continue;
                (top, bottom, left, right) = (Math.Min(top, cell.Top), Math.Max(bottom, cell.Bottom), Math.Min(left, cell.Left), Math.Max(right, cell.Right));
                grown = true;
            }
        }
        return new CellRange(table, top, bottom, left, right);
    }

    /// <summary>A table's row at least so high, points (Word's trHeight without a rule); 0, as high as its text. The paragraphs stay as they are.</summary>
    public void SetRowHeight(int table, int row, double height) =>
        Reshape(table, shape =>
        {
            shape.SetHeight(row, height > 0 ? InTwips(height) : 0);
            return null;
        }, widths: true);

    /// <summary>
    /// Moves rows of a table (from one to before another, rows of their own) to stand before row <paramref name="before"/>, counted as
    /// the rows stand before the move (see TextEdits.TableShape.MoveRows); returns the first paragraph of the first row moved.
    /// </summary>
    public int MoveRows(int table, int from, int to, int before) =>
        Reshape(table, shape =>
        {
            var first = shape.At(from, 0) ?? shape.Cells.First(c => c.Top == from);
            shape.MoveRows(from, to, before);
            return first;
        });

    /// <summary>Moves columns of a table to stand before grid column <paramref name="before"/>, as MoveRows; returns the first paragraph of the first column moved.</summary>
    public int MoveColumns(int table, int from, int to, int before) =>
        Reshape(table, shape =>
        {
            var first = shape.At(0, from) ?? shape.Cells.First(c => c.Left == from);
            shape.MoveColumns(from, to, before);
            return first;
        });

    /// <summary>
    /// Sorts a table's rows from <paramref name="from"/> on (after its head) by the text of their cells in a grid column, as the
    /// document's language orders words, numbers by their value (see TextEdits.TableShape.Sort); returns the first paragraph of the
    /// first row sorted.
    /// </summary>
    public int SortRows(int table, int column, int from, bool descending = false) =>
        Reshape(table, shape =>
        {
            shape.Sort(column, from, descending, Culture);
            return shape.At(from, 0);
        });

    /// <summary>Turns a table's rows into its columns and its columns into its rows (see TextEdits.TableShape.Transpose); returns its first cell's first paragraph.</summary>
    public int Transpose(int table) =>
        Reshape(table, shape =>
        {
            shape.Transpose();
            return shape.At(0, 0);
        });

    /// <summary>A table in a table style, its own formatting staying (see TextEdits.TableShape.SetStyle). The paragraphs stay as they are.</summary>
    public void StyleTable(int table, string style) =>
        Reshape(table, shape =>
        {
            shape.SetStyle(style);
            return null;
        }, widths: true);

    /// <summary>A table with a head row or without a head (see TextEdits.TableShape.SetHead). The paragraphs stay as they are.</summary>
    public void SetHead(int table, bool on) =>
        Reshape(table, shape =>
        {
            shape.SetHead(on);
            return null;
        }, widths: true);

    /// <summary>A table's columns made even over its width, as Word's Distribute Columns does (see TextEdits.TableShape.Distribute).</summary>
    public void DistributeColumns(int table) =>
        Reshape(table, shape =>
        {
            shape.Distribute();
            return null;
        }, widths: true);

    /// <summary>The culture the document's text is in, which sorting goes by: its own language's, else the one it was opened in.</summary>
    CultureInfo Culture => context.Culture;

    static int InTwips(double points) => (int)Math.Round(points * 20);
}

/// <summary>
/// A cell of a table as an editor shows it: the rectangle of the table's grid it covers (rows from Top to before Bottom, grid columns
/// from Left to before Right), its first paragraph and the last one of its own (its content's last, after any table in it), all its
/// paragraphs in reading order (those of tables in it too), and whether it holds no text and nothing else.
/// </summary>
public sealed record TableCell(int Top, int Bottom, int Left, int Right, int First, int Last, IReadOnlyList<int> Paragraphs, bool Empty);

/// <summary>
/// A table's cells (see TableCell) in reading order, its numbers of rows and of its grid's columns, whether it goes from the right
/// (w:bidiVisual), and whether its look marks its first row or its first column as its head (w:tblLook).
/// </summary>
public sealed record TableCells(int Table, int Rows, int Columns, IReadOnlyList<TableCell> Cells, bool FromRight, bool HeadRow, bool HeadColumn);

/// <summary>
/// Cells of a table selected together, as Word selects them when a selection reaches from one cell into another: the rectangle of the
/// table's grid they cover (rows from Top to before Bottom, grid columns from Left to before Right), each cell reaching into it whole.
/// </summary>
public sealed record CellRange(int Table, int Top, int Bottom, int Left, int Right);

/// <summary>
/// A line of a table's cells as an editor gives it to a side of them (w:tcBorders): Word's kind of line (w:val: single, double, dotted,
/// dashed; nil for none), its width in points and its color (hex RRGGBB, or auto).
/// </summary>
public sealed record CellBorder(string Kind, double Width = 0.5, string Color = "auto")
{
    /// <summary>No line there, as Word takes a line away (nil).</summary>
    public static readonly CellBorder None = new("nil", 0);
}

/// <summary>Which lines of a rectangle of cells (see WordDocument.SetLines): its four sides, the lines across within it and the upright ones.</summary>
[Flags]
public enum CellLines
{
    Top = 1,
    Bottom = 2,
    Left = 4,
    Right = 8,
    Across = 16,
    Upright = 32,
    Outside = Top | Bottom | Left | Right,
    Inside = Across | Upright,
    All = Outside | Inside,
}
