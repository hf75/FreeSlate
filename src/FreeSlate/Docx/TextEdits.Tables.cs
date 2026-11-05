using System.Globalization;
using System.Xml.Linq;
using FreeSlate.Opc;

namespace FreeSlate.Docx;

/// <summary>
/// Tables changed as a whole (catalog: table.structure): their shape read as the rectangles of the grid their cells cover, changed
/// there, and written back as Word writes tables (measured in Word 365 by COM, scratchpad tables/: Tables.Add, Cell.Merge, Cell.Split,
/// Rows and Columns added, deleted and set wider).
/// </summary>
static partial class TextEdits
{
    /// <summary>
    /// A table's shape: the widths of its grid's columns (twips), its rows, and its cells as the rectangles of the grid they cover (rows
    /// from Top to before Bottom, columns from Left to before Right): a cell merged across spans columns (w:gridSpan), one merged down
    /// goes on in the rows below (w:vMerge, the cells there holding nothing of their own). Changed, the rows are written anew: each
    /// cell with its width (w:tcW, the sum of the grid columns it spans), its span and its merge, the cells that go on with a merge
    /// down with an empty paragraph, as Word writes them; a row that does not reach the grid's first or last column with w:gridBefore
    /// or w:gridAfter. What a cell holds moves with it, its paragraphs the same elements (their ids and line info kept).
    /// </summary>
    internal sealed class TableShape
    {
        readonly XElement table;
        readonly XNamespace w;

        /// <summary>A cell: the rectangle of the grid it covers, and the w:tc that holds its properties and content.</summary>
        public sealed class Cell
        {
            public int Top, Bottom, Left, Right;
            public required XElement Element;

            /// <summary>Cells that go on with this one's merge down, read with it: their content, which Word does not show, kept where the cell still reaches.</summary>
            public List<XElement> Below { get; } = [];
        }

        /// <summary>The widths of the grid's columns, twips.</summary>
        public List<int> Grid { get; }

        /// <summary>The rows, each its w:tr (its own properties kept, its cells written anew).</summary>
        public List<XElement> Rows { get; }

        /// <summary>The cells, in reading order once written.</summary>
        public List<Cell> Cells { get; }

        public int Columns => Grid.Count;

        TableShape(XElement table, List<int> grid, List<XElement> rows, List<Cell> cells) =>
            (this.table, w, Grid, Rows, Cells) = (table, table.Name.Namespace, grid, rows, cells);

        /// <summary>
        /// The shape of a table as its rows say it. A table whose rows or cells stand in other elements (content controls, custom
        /// markup), or that changes are tracked in (rows or cells put in or taken out, merged as a change), is not changed so.
        /// </summary>
        /// <exception cref="NotSupportedException">The table is not one of plain rows and cells.</exception>
        public static TableShape Of(XElement table)
        {
            var w = table.Name.Namespace;
            if (table.Elements().FirstOrDefault(e => e.Name.Namespace == w && e.Name.LocalName is not ("tblPr" or "tblGrid" or "tr")) is { } other)
                throw new NotSupportedException($"The table holds a w:{other.Name.LocalName} among its rows.");
            var rows = table.Elements(w + "tr").ToList();
            if (rows.Count == 0)
                throw new NotSupportedException("The table has no rows.");
            var cells = new List<Cell>();
            for (var r = 0; r < rows.Count; r++)
            {
                var tr = rows[r];
                if (tr.Element(w + "trPr") is { } trPr && (trPr.Element(w + "ins") != null || trPr.Element(w + "del") != null))
                    throw new NotSupportedException("A row of the table is put in or taken out as a tracked change.");
                if (tr.Elements().FirstOrDefault(e => e.Name.Namespace == w && e.Name.LocalName is not ("trPr" or "tblPrEx" or "tc")) is { } odd)
                    throw new NotSupportedException($"A row of the table holds a w:{odd.Name.LocalName} among its cells.");
                var column = Number(tr.Element(w + "trPr")?.Element(w + "gridBefore"));
                foreach (var tc in tr.Elements(w + "tc"))
                {
                    var tcPr = tc.Element(w + "tcPr");
                    if (tcPr?.Elements().Any(e => e.Name.LocalName is "cellIns" or "cellDel" or "cellMerge" or "tcPrChange") == true)
                        throw new NotSupportedException("A cell of the table is changed as a tracked change.");
                    // Cells merged across the old way, each its own w:tc (which the model folds into the first), are not shaped so.
                    if (tcPr?.Element(w + "hMerge") != null)
                        throw new NotSupportedException("A cell of the table is merged across with w:hMerge.");
                    var span = Math.Max(1, Number(tcPr?.Element(w + "gridSpan")));
                    var merge = tcPr?.Element(w + "vMerge");
                    var goesOn = merge != null && merge.Attribute(w + "val")?.Value is null or "continue";
                    // A cell going on with a merge down joins the cell above it of the same columns; one with none there stands alone.
                    if (goesOn && cells.LastOrDefault(c => c.Bottom == r && c.Left == column && c.Right == column + span) is { } above)
                    {
                        above.Bottom = r + 1;
                        above.Below.Add(tc);
                    }
                    else
                        cells.Add(new Cell { Top = r, Bottom = r + 1, Left = column, Right = column + span, Element = tc });
                    column += span;
                }
            }
            var columns = cells.Max(c => c.Right);
            var grid = table.Element(w + "tblGrid")?.Elements(w + "gridCol").Select(g => Number(g, "w")).ToList() ?? [];
            if (grid.Count < columns)
            {
                // Columns the grid does not list take the widths of cells that span them alone, else the average of those it lists.
                var fill = grid.Count > 0 ? (int)grid.Average() : 1440;
                for (var k = grid.Count; k < columns; k++)
                    grid.Add(cells.FirstOrDefault(c => c.Left == k && c.Right == k + 1 && Width(c.Element) > 0) is { } alone ? Width(alone.Element) : fill);
            }
            for (var k = 0; k < grid.Count; k++)
                if (grid[k] <= 0)
                    grid[k] = 1440;
            return new TableShape(table, grid, rows, cells);
        }

        /// <summary>The cell that covers a place of the grid; null where a row does not reach it.</summary>
        public Cell? At(int row, int column) => Cells.FirstOrDefault(c => c.Top <= row && row < c.Bottom && c.Left <= column && column < c.Right);

        /// <summary>Where a line of the grid stands from its left edge, twips.</summary>
        public int Line(int column) => Grid.Take(column).Sum();

        /// <summary>
        /// Merges the cells of a rectangle of the grid into one, as Word does (Cell.Merge): the first cell's properties, the content of
        /// all of them in reading order, cells with nothing in them leaving nothing (the first one's empty paragraph where all are).
        /// </summary>
        /// <exception cref="NotSupportedException">A cell reaches out of the rectangle, or a row does not reach into it.</exception>
        public Cell Merge(int top, int bottom, int left, int right)
        {
            var inside = Cells.Where(c => c.Top < bottom && top < c.Bottom && c.Left < right && left < c.Right).OrderBy(c => c.Top).ThenBy(c => c.Left).ToList();
            if (inside.Any(c => c.Top < top || c.Bottom > bottom || c.Left < left || c.Right > right))
                throw new NotSupportedException("A cell reaches out of what is merged.");
            if (inside.Sum(c => (c.Bottom - c.Top) * (c.Right - c.Left)) != (bottom - top) * (right - left))
                throw new NotSupportedException("A row does not reach into all of what is merged.");
            var first = inside[0];
            var filled = inside.Where(c => !Empty(c)).ToList();
            if (filled.Count > 0)
            {
                // The first cell's content goes too where it is empty: the others' content alone stands in the merged cell.
                var content = filled.SelectMany(Content).ToList();
                foreach (var node in Content(first).Concat(content).Distinct().ToList())
                    node.Remove();
                first.Element.Add(content);
            }
            foreach (var cell in inside.Skip(1))
            {
                Cells.Remove(cell);
                // Whatever the merged cells kept below them (their content Word does not show) goes with them.
                first.Below.AddRange(cell.Below);
            }
            (first.Top, first.Bottom, first.Left, first.Right) = (top, bottom, left, right);
            return first;
        }

        /// <summary>
        /// The smallest rectangle of the grid that holds a given one and every cell reaching into it whole: what merging over it takes, as
        /// a cell is never anything but a rectangle.
        /// </summary>
        public (int Top, int Bottom, int Left, int Right) Enclosing(int top, int bottom, int left, int right)
        {
            for (var grown = true; grown;)
            {
                grown = false;
                foreach (var cell in Cells.Where(c => c.Top < bottom && top < c.Bottom && c.Left < right && left < c.Right))
                {
                    if (cell.Top < top || cell.Bottom > bottom || cell.Left < left || cell.Right > right)
                    {
                        (top, bottom, left, right) = (Math.Min(top, cell.Top), Math.Max(bottom, cell.Bottom), Math.Min(left, cell.Left), Math.Max(right, cell.Right));
                        grown = true;
                    }
                }
            }
            return (top, bottom, left, right);
        }

        /// <summary>
        /// Takes a line between cells away, as Word's eraser does: an upright one (a line of the grid) through rows from one to before
        /// another, one across (the line before a row) through grid columns; each stretch of it between two cells merges them (see
        /// Merge), into the smallest rectangle holding both where they do not make one (see Enclosing). The table's own sides stay.
        /// Returns the first cell merged.
        /// </summary>
        public Cell? Erase(bool upright, int line, int from, int to)
        {
            Cell? first = null;
            if (upright ? line <= 0 || line >= Grid.Count : line <= 0 || line >= Rows.Count)
                return null;
            for (var k = from; k < to; k++)
            {
                var (before, after) = upright ? (At(k, line - 1), At(k, line)) : (At(line - 1, k), At(line, k));
                if (before == null || after == null || ReferenceEquals(before, after))
                    continue;
                var (top, bottom, left, right) = Enclosing(Math.Min(before.Top, after.Top), Math.Max(before.Bottom, after.Bottom),
                    Math.Min(before.Left, after.Left), Math.Max(before.Right, after.Right));
                if (Cells.Any(c => c.Top < bottom && top < c.Bottom && c.Left < right && left < c.Right) is false
                    || (bottom - top) * (right - left) != Cells.Where(c => c.Top >= top && c.Bottom <= bottom && c.Left >= left && c.Right <= right).Sum(c => (c.Bottom - c.Top) * (c.Right - c.Left)))
                    continue;
                var merged = Merge(top, bottom, left, right);
                first ??= merged;
            }
            return first;
        }

        /// <summary>
        /// Splits cells along an upright line, as Word's pen does: at a line of the grid within them, or where <paramref name="width"/>
        /// is given, at that many twips into grid column <paramref name="column"/> (a new line of the grid there, the cells of other rows
        /// that cover the column spanning both of its parts). The part left of the line keeps the cell, its content and properties;
        /// the part right of it is a new cell with the cell's properties and an empty paragraph like its last one. Returns the new cells.
        /// </summary>
        public List<Cell> SplitDown(IReadOnlyCollection<Cell> cells, int column, int? width = null)
        {
            var line = column;
            if (width is { } part)
            {
                if (part <= 0 || part >= Grid[column])
                    throw new ArgumentOutOfRangeException(nameof(width), "The line is not within the column.");
                Grid.Insert(column + 1, Grid[column] - part);
                Grid[column] = part;
                foreach (var cell in Cells)
                {
                    if (cell.Left > column)
                        cell.Left++;
                    if (cell.Right > column)
                        cell.Right++;
                }
                line = column + 1;
            }
            var made = new List<Cell>();
            foreach (var cell in cells)
            {
                if (cell.Left >= line || cell.Right <= line)
                    throw new ArgumentException("A cell does not reach over the line.", nameof(cells));
                var right = new Cell { Top = cell.Top, Bottom = cell.Bottom, Left = line, Right = cell.Right, Element = Fresh(cell) };
                cell.Right = line;
                Cells.Add(right);
                made.Add(right);
            }
            return made;
        }

        /// <summary>
        /// Splits cells along a line across, as Word's pen does: at a line between rows within them, or where <paramref name="height"/>
        /// is given, that many twips down into row <paramref name="row"/> (a new row after it, its own properties those of the row,
        /// the cells of the row not split going on down into it as merged; the two rows at least as high as the line leaves them). The
        /// part above the line keeps the cell; the part below is a new cell like the right part of an upright split. Returns the new cells.
        /// </summary>
        public List<Cell> SplitAcross(IReadOnlyCollection<Cell> cells, int row, int? height = null, int? rowHeight = null)
        {
            var line = row;
            if (height is { } part)
            {
                var copy = new XElement(Rows[row].Name, Rows[row].Element(w + "tblPrEx") is { } exceptions ? new XElement(exceptions) : null,
                    Rows[row].Element(w + "trPr") is { } trPr ? new XElement(trPr) : null);
                copy.Element(w + "trPr")?.Element(w + "tblHeader")?.Remove();
                Strip(copy);
                Rows.Insert(row + 1, copy);
                foreach (var cell in Cells)
                {
                    if (cell.Top > row)
                        cell.Top++;
                    if (cell.Bottom > row)
                        cell.Bottom++;
                }
                if (rowHeight is { } whole && whole > part)
                {
                    SetHeight(row, part);
                    SetHeight(row + 1, whole - part);
                }
                line = row + 1;
            }
            var made = new List<Cell>();
            foreach (var cell in cells)
            {
                if (cell.Top >= line || cell.Bottom <= line)
                    throw new ArgumentException("A cell does not reach over the line.", nameof(cells));
                var below = new Cell { Top = line, Bottom = cell.Bottom, Left = cell.Left, Right = cell.Right, Element = Fresh(cell) };
                cell.Bottom = line;
                Cells.Add(below);
                made.Add(below);
            }
            return made;
        }

        /// <summary>
        /// Splits a cell into so many of even width side by side, at the grid's lines where the parts end on one, else at new ones (see
        /// SplitDown); or (<paramref name="across"/>) into so many one below the other: a cell of one row into as many rows, the other
        /// cells of its row going on down through them (see SplitAcross), one of as many rows or a multiple at the lines between them.
        /// Returns the cell, its first part.
        /// </summary>
        /// <exception cref="NotSupportedException">The cell spans rows that do not part so evenly.</exception>
        public Cell Split(Cell cell, bool across, int parts)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(parts, 2);
            if (across)
            {
                var span = cell.Bottom - cell.Top;
                if (span == 1)
                {
                    for (var (k, last) = (1, cell); k < parts; k++)
                        last = SplitAcross([last], last.Top, height: 1)[0];
                }
                else if (span % parts == 0)
                {
                    for (var k = parts - 1; k >= 1; k--)
                        SplitAcross([cell], cell.Top + span * k / parts);
                }
                else
                    throw new NotSupportedException("The cell's rows do not part so evenly.");
                return cell;
            }
            var (left, right) = (Line(cell.Left), Line(cell.Right));
            for (var k = parts - 1; k >= 1; k--)
            {
                var x = left + (right - left) * k / parts;
                var column = Enumerable.Range(cell.Left, cell.Right - cell.Left).First(c => Line(c + 1) > x);
                if (Line(column) == x)
                    SplitDown([cell], column);
                else
                    SplitDown([cell], column, x - Line(column));
            }
            return cell;
        }

        /// <summary>
        /// Puts rows in before row <paramref name="at"/> (at the end where it is the count), each with cells like those of row
        /// <paramref name="like"/> (their spans, properties and paragraphs' marks, as Word's InsertRowsAbove and Below do), cells merged
        /// down over the place going on through them. Returns the first new row's cells.
        /// </summary>
        public List<Cell> InsertRows(int at, int count, int like)
        {
            var source = Rows[like];
            var pattern = Cells.Where(c => c.Top <= like && like < c.Bottom).OrderBy(c => c.Left).ToList();
            for (var k = 0; k < count; k++)
            {
                var row = new XElement(source.Name, source.Element(w + "tblPrEx") is { } exceptions ? new XElement(exceptions) : null,
                    source.Element(w + "trPr") is { } trPr ? new XElement(trPr) : null);
                row.Element(w + "trPr")?.Element(w + "tblHeader")?.Remove();
                row.Element(w + "trPr")?.Element(w + "trHeight")?.Remove();
                Strip(row);
                Rows.Insert(at, row);
            }
            foreach (var cell in Cells)
            {
                if (cell.Top >= at)
                    cell.Top += count;
                if (cell.Bottom > at && cell.Top < at)
                    cell.Bottom += count;
                else if (cell.Bottom >= at && cell.Top >= at + count)
                    cell.Bottom += count;
            }
            var first = new List<Cell>();
            for (var k = 0; k < count; k++)
            {
                foreach (var cell in pattern)
                {
                    // Where a cell merged down goes on through the place, no new cell.
                    if (Cells.Any(c => c.Top < at + k && at + k < c.Bottom && c.Left < cell.Right && cell.Left < c.Right))
                        continue;
                    var made = new Cell { Top = at + k, Bottom = at + k + 1, Left = cell.Left, Right = cell.Right, Element = Fresh(cell) };
                    Cells.Add(made);
                    if (k == 0)
                        first.Add(made);
                }
            }
            return first;
        }

        /// <summary>
        /// Puts columns in before grid column <paramref name="at"/> (at the end where it is the count), each as wide as the average
        /// column and the table as wide as it was (the columns giving way in proportion), each row a new cell there like the cell of
        /// column <paramref name="like"/> beside it; a cell merged across over the place spans the new columns too. Returns the new
        /// cells of the first new column.
        /// </summary>
        public List<Cell> InsertColumns(int at, int count, int like)
        {
            var whole = Grid.Sum();
            var average = whole / Grid.Count;
            for (var k = 0; k < count; k++)
                Grid.Insert(at, average);
            Fit(whole);
            foreach (var cell in Cells)
            {
                if (cell.Left >= at)
                    cell.Left += count;
                if (cell.Right > at && cell.Left < at)
                    cell.Right += count;
                else if (cell.Right >= at && cell.Left >= at + count)
                    cell.Right += count;
            }
            var first = new List<Cell>();
            var from = like >= at ? like + count : like;
            foreach (var cell in Cells.Where(c => c.Left <= from && from < c.Right).OrderBy(c => c.Top).ToList())
            {
                // A cell spanning over the place takes the new columns in (see above); one beside it is copied.
                if (cell.Left < at && at + count < cell.Right + (cell.Right >= at + count ? 0 : 1) && cell.Right > at)
                    continue;
                if (Cells.Any(c => c.Left < at && at < c.Right && c.Top < cell.Bottom && cell.Top < c.Bottom))
                    continue;
                for (var k = 0; k < count; k++)
                {
                    var made = new Cell { Top = cell.Top, Bottom = cell.Bottom, Left = at + k, Right = at + k + 1, Element = Fresh(cell) };
                    Cells.Add(made);
                    if (k == 0)
                        first.Add(made);
                }
            }
            // Rows that do not reach the place (ending before it, or beginning after it) get no cell there: their cells are left as they are.
            return first;
        }

        /// <summary>Takes rows away, with the cells that lie in them alone; a cell merged down over them keeps its content, starting below them.</summary>
        public void RemoveRows(int from, int to)
        {
            if (from <= 0 && to >= Rows.Count)
                throw new ArgumentException("A table keeps a row.", nameof(to));
            var count = to - from;
            foreach (var cell in Cells.ToList())
            {
                if (cell.Top >= from && cell.Bottom <= to)
                {
                    Cells.Remove(cell);
                    continue;
                }
                cell.Top = cell.Top >= to ? cell.Top - count : Math.Min(cell.Top, from);
                cell.Bottom = cell.Bottom >= to ? cell.Bottom - count : Math.Min(cell.Bottom, from);
            }
            Rows.RemoveRange(from, count);
        }

        /// <summary>Takes columns away, with the cells that lie in them alone, the table as wide as it was (the others taking their width in proportion).</summary>
        public void RemoveColumns(int from, int to)
        {
            if (from <= 0 && to >= Grid.Count)
                throw new ArgumentException("A table keeps a column.", nameof(to));
            var whole = Grid.Sum();
            var count = to - from;
            foreach (var cell in Cells.ToList())
            {
                if (cell.Left >= from && cell.Right <= to)
                {
                    Cells.Remove(cell);
                    continue;
                }
                cell.Left = cell.Left >= to ? cell.Left - count : Math.Min(cell.Left, from);
                cell.Right = cell.Right >= to ? cell.Right - count : Math.Min(cell.Right, from);
            }
            Grid.RemoveRange(from, count);
            Fit(whole);
            // A row left without a cell (its cells all in the columns taken) loses nothing more: it is taken too.
            for (var r = Rows.Count - 1; r >= 0; r--)
            {
                if (!Cells.Any(c => c.Top <= r && r < c.Bottom))
                {
                    Rows.RemoveAt(r);
                    foreach (var cell in Cells)
                    {
                        if (cell.Top > r)
                            cell.Top--;
                        if (cell.Bottom > r)
                            cell.Bottom--;
                    }
                }
            }
        }

        /// <summary>Whether rows from one to before another are rows of their own: no cell merged down reaches over their top or bottom.</summary>
        public bool Apart(int from, int to) =>
            !Cells.Any(c => (c.Top < from && from < c.Bottom) || (c.Top < to && to < c.Bottom));

        /// <summary>Whether columns from one to before another are columns of their own: no cell merged across reaches over their sides.</summary>
        public bool ApartColumns(int from, int to) =>
            !Cells.Any(c => (c.Left < from && from < c.Right) || (c.Left < to && to < c.Right));

        /// <summary>
        /// Moves rows (from one to before another, rows of their own) to stand before row <paramref name="before"/> (counted as the rows
        /// stand before the move, the count for the end), which no cell merged down reaches over.
        /// </summary>
        /// <exception cref="NotSupportedException">The rows or the place are within cells merged down.</exception>
        public void MoveRows(int from, int to, int before)
        {
            if (!Apart(from, to) || (before > 0 && before < Rows.Count && Cells.Any(c => c.Top < before && before < c.Bottom)))
                throw new NotSupportedException("Cells merged down reach over the rows or the place they go.");
            if (before >= from && before <= to)
                return;
            var order = Enumerable.Range(0, Rows.Count).ToList();
            var moved = order.GetRange(from, to - from);
            order.RemoveRange(from, to - from);
            order.InsertRange(before > from ? before - (to - from) : before, moved);
            Reorder(order, rows: true);
        }

        /// <summary>Moves columns (from one to before another, columns of their own) to stand before grid column <paramref name="before"/>, as MoveRows.</summary>
        public void MoveColumns(int from, int to, int before)
        {
            if (!ApartColumns(from, to) || (before > 0 && before < Grid.Count && Cells.Any(c => c.Left < before && before < c.Right)))
                throw new NotSupportedException("Cells merged across reach over the columns or the place they go.");
            if (before >= from && before <= to)
                return;
            var order = Enumerable.Range(0, Grid.Count).ToList();
            var moved = order.GetRange(from, to - from);
            order.RemoveRange(from, to - from);
            order.InsertRange(before > from ? before - (to - from) : before, moved);
            Reorder(order, rows: false);
        }

        /// <summary>The rows (or grid columns) in a new order: the old index of each in its new place.</summary>
        void Reorder(List<int> order, bool rows)
        {
            var place = new int[order.Count];
            for (var k = 0; k < order.Count; k++)
                place[order[k]] = k;
            if (rows)
            {
                var old = Rows.ToList();
                for (var k = 0; k < order.Count; k++)
                    Rows[k] = old[order[k]];
            }
            else
            {
                var old = Grid.ToList();
                for (var k = 0; k < order.Count; k++)
                    Grid[k] = old[order[k]];
            }
            foreach (var cell in Cells)
            {
                // A cell of rows (columns) of their own keeps its extent; its first one says where it goes.
                if (rows)
                    (cell.Top, cell.Bottom) = (place[cell.Top], place[cell.Top] + cell.Bottom - cell.Top);
                else
                    (cell.Left, cell.Right) = (place[cell.Left], place[cell.Left] + cell.Right - cell.Left);
            }
        }

        /// <summary>
        /// Sorts the rows from <paramref name="from"/> on by the text of their cells in a column, as the language of the document
        /// orders words (numbers by their value where every cell holds one), empty cells last; rows merged down are not sorted.
        /// </summary>
        /// <exception cref="NotSupportedException">A cell merged down stands in the rows sorted.</exception>
        public void Sort(int column, int from, bool descending, CultureInfo culture)
        {
            if (Cells.Any(c => c.Bottom > from && c.Bottom - c.Top > 1))
                throw new NotSupportedException("A cell merged down stands in the rows sorted.");
            var keys = Enumerable.Range(from, Rows.Count - from).Select(r => (Row: r, Text: At(r, column) is { } cell ? Text(cell).Trim() : "")).ToList();
            var numbers = keys.Where(k => k.Text.Length > 0).All(k => double.TryParse(k.Text, NumberStyles.Number, culture, out _));
            var comparer = StringComparer.Create(culture, CompareOptions.IgnoreCase);
            int Compare((int Row, string Text) a, (int Row, string Text) b)
            {
                if (a.Text.Length == 0 || b.Text.Length == 0)
                    return a.Text.Length == b.Text.Length ? a.Row.CompareTo(b.Row) : a.Text.Length == 0 ? 1 : -1;
                var order = numbers
                    ? double.Parse(a.Text, NumberStyles.Number, culture).CompareTo(double.Parse(b.Text, NumberStyles.Number, culture))
                    : comparer.Compare(a.Text, b.Text);
                return (descending ? -order : order) is var given && given != 0 ? given : a.Row.CompareTo(b.Row);
            }
            keys.Sort(Compare);
            Reorder([.. Enumerable.Range(0, from), .. keys.Select(k => k.Row)], rows: true);
        }

        /// <summary>
        /// Turns the table's rows into its columns and its columns into its rows: each cell where its mirror over the diagonal is, its
        /// lines turned with it (its top one its left one), the columns of even width over the table's width, the rows as high as
        /// their text. The look's marks turn too (w:tblLook: the first row's the first column's, the last row's the last column's,
        /// bands of rows bands of columns), so that a head row becomes a head column and back; a head row is repeated on each page.
        /// </summary>
        public void Transpose()
        {
            var whole = Grid.Sum();
            var (rows, columns) = (Grid.Count, Rows.Count);
            var headRow = TurnLook();
            var first = Rows[0];
            var made = new List<XElement>();
            for (var r = 0; r < rows; r++)
            {
                var row = r < Rows.Count ? Rows[r] : new XElement(first.Name, first.Element(w + "trPr") is { } trPr ? new XElement(trPr) : null);
                row.Element(w + "trPr")?.Element(w + "tblHeader")?.Remove();
                row.Element(w + "trPr")?.Element(w + "trHeight")?.Remove();
                Strip(row);
                made.Add(row);
            }
            if (headRow)
                Property(made[0].Element(w + "trPr") ?? Made(made[0], w + "trPr"), "tblHeader", RowOrder);
            Rows.Clear();
            Rows.AddRange(made);
            foreach (var cell in Cells)
            {
                (cell.Top, cell.Bottom, cell.Left, cell.Right) = (cell.Left, cell.Right, cell.Top, cell.Bottom);
                if (cell.Element.Element(w + "tcPr")?.Element(w + "tcBorders") is { } borders)
                {
                    foreach (var (one, other) in new[] { ("top", "left"), ("bottom", "right"), ("top", "start"), ("bottom", "end") })
                    {
                        var (a, b) = (borders.Element(w + one), borders.Element(w + other));
                        a?.Remove();
                        b?.Remove();
                        if (a != null)
                            borders.Add(new XElement(w + other, a.Attributes()));
                        if (b != null)
                            borders.Add(new XElement(w + one, b.Attributes()));
                    }
                    Order(borders, BorderOrder);
                }
            }
            Grid.Clear();
            Grid.AddRange(Even(whole, columns));
        }

        /// <summary>
        /// The table's look (w:tblLook) turned with its rows and columns: what it says of the first row it says of the first column and
        /// back, so of the last ones and of the bands; written as Word writes it, each mark an attribute and all of them in w:val's bits.
        /// Returns whether the first row is marked then (a head row).
        /// </summary>
        bool TurnLook()
        {
            if (table.Element(w + "tblPr")?.Element(w + "tblLook") is not { } look)
                return false;
            var bits = look.Attribute(w + "val") is { } val && int.TryParse(val.Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var given) ? given : 0;
            bool On(string name, int bit) => look.Attribute(w + name) is { } a ? a.Value is "1" or "true" or "on" : (bits & bit) != 0;
            var marks = new (string Name, int Bit, bool On)[]
            {
                ("firstRow", 0x20, On("firstColumn", 0x80)), ("lastRow", 0x40, On("lastColumn", 0x100)), ("firstColumn", 0x80, On("firstRow", 0x20)),
                ("lastColumn", 0x100, On("lastRow", 0x40)), ("noHBand", 0x200, On("noVBand", 0x400)), ("noVBand", 0x400, On("noHBand", 0x200)),
            };
            var turned = bits & ~0x7E0;
            foreach (var (name, bit, on) in marks)
            {
                look.SetAttributeValue(w + name, on ? "1" : "0");
                if (on)
                    turned |= bit;
            }
            look.SetAttributeValue(w + "val", turned.ToString("X4", CultureInfo.InvariantCulture));
            return marks[0].On;
        }

        /// <summary>The table in a table style (w:tblStyle), its own formatting staying.</summary>
        public void SetStyle(string style)
        {
            var tblPr = table.Element(w + "tblPr") ?? Made(table, w + "tblPr");
            var made = Property(tblPr, "tblStyle", TableOrder);
            made.RemoveAttributes();
            made.SetAttributeValue(w + "val", style);
        }

        /// <summary>
        /// The table with a head row or without a head: the look marks its first row (w:tblLook firstRow, Word's Header Row) and the row
        /// repeats on each page the table goes on to (w:tblHeader), or neither, nor a head column (firstColumn).
        /// </summary>
        public void SetHead(bool on)
        {
            var tblPr = table.Element(w + "tblPr") ?? Made(table, w + "tblPr");
            var look = Property(tblPr, "tblLook", TableOrder);
            var bits = look.Attribute(w + "val") is { } val && int.TryParse(val.Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var given) ? given : 0;
            bits = on ? (bits | 0x20) & ~0x80 : bits & ~0xA0;
            look.SetAttributeValue(w + "val", bits.ToString("X4", CultureInfo.InvariantCulture));
            look.SetAttributeValue(w + "firstRow", on ? "1" : "0");
            look.SetAttributeValue(w + "firstColumn", "0");
            foreach (var row in Rows)
                row.Element(w + "trPr")?.Element(w + "tblHeader")?.Remove();
            if (on)
                Property(Rows[0].Element(w + "trPr") ?? Made(Rows[0], w + "trPr"), "tblHeader", RowOrder);
            foreach (var row in Rows)
            {
                if (row.Element(w + "trPr") is { HasElements: false, HasAttributes: false } empty)
                    empty.Remove();
            }
        }

        /// <summary>The columns of even width over the table's width, as Word gives a new table's (the rest a twip each to the last ones).</summary>
        public void Distribute()
        {
            var whole = Grid.Sum();
            var even = Even(whole, Grid.Count);
            Grid.Clear();
            Grid.AddRange(even);
        }

        /// <summary>
        /// Gives a stretch of a line of the grid a line of its own, as Word does where a side of cells is given one (Borders' LineStyle,
        /// LineWidth and Color, measured in Word 365 by COM, scratchpad tables/looks.vbs): an upright one (grid line
        /// <paramref name="line"/>) through rows from <paramref name="from"/> to before <paramref name="to"/>, one across (the line before
        /// row <paramref name="line"/>) through grid columns. Each cell on either side gets it on its side there (w:tcBorders, nil where
        /// it is none): a cell merged down on an upright line in each row it passes, above a line across in its last row; the cell the
        /// table's own side has alone. Null takes those sides away, the line as the table's look gives it again. Within a merged cell
        /// there is no line.
        /// </summary>
        public void SetLine(bool upright, int line, int from, int to, CellBorder? border)
        {
            var (lines, along) = upright ? (Grid.Count, Rows.Count) : (Rows.Count, Grid.Count);
            ArgumentOutOfRangeException.ThrowIfNegative(line);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(line, lines);
            for (var k = Math.Max(0, from); k < Math.Min(to, along); k++)
            {
                var (before, after) = upright
                    ? (line > 0 ? At(k, line - 1) : null, line < Grid.Count ? At(k, line) : null)
                    : (line > 0 ? At(line - 1, k) : null, line < Rows.Count ? At(line, k) : null);
                if (before != null && ReferenceEquals(before, after))
                    continue;
                if (before != null)
                    Side(InRow(before, upright ? k : line - 1), upright ? "right" : "bottom", border);
                if (after != null)
                    Side(InRow(after, upright ? k : line), upright ? "left" : "top", border);
            }
        }

        /// <summary>
        /// Gives lines of a rectangle of the grid a line of their own (see SetLine): its sides, the lines across within it and the upright
        /// ones, as Word's Borders of cells selected do.
        /// </summary>
        public void SetLines(int top, int bottom, int left, int right, CellLines which, CellBorder? border)
        {
            if (which.HasFlag(CellLines.Top))
                SetLine(false, top, left, right, border);
            if (which.HasFlag(CellLines.Bottom))
                SetLine(false, bottom, left, right, border);
            if (which.HasFlag(CellLines.Left))
                SetLine(true, left, top, bottom, border);
            if (which.HasFlag(CellLines.Right))
                SetLine(true, right, top, bottom, border);
            if (which.HasFlag(CellLines.Across))
                for (var r = top + 1; r < bottom; r++)
                    SetLine(false, r, left, right, border);
            if (which.HasFlag(CellLines.Upright))
                for (var k = left + 1; k < right; k++)
                    SetLine(true, k, top, bottom, border);
        }

        /// <summary>The w:tc of a cell in one of its rows: the cell's own in its first, else the one going on with its merge down there.</summary>
        XElement InRow(Cell cell, int row) => row > cell.Top && row - cell.Top - 1 < cell.Below.Count ? cell.Below[row - cell.Top - 1] : cell.Element;

        /// <summary>A side of a cell (w:tcBorders' top, left, bottom, right) given a line, or none of its own (null).</summary>
        void Side(XElement tc, string side, CellBorder? border)
        {
            var tcPr = tc.Element(w + "tcPr") ?? Made(tc, w + "tcPr");
            var borders = tcPr.Element(w + "tcBorders");
            // A side named by the direction of the text (start, end) goes too, which would stand for it beside the one written.
            borders?.Elements().Where(e => e.Name.LocalName == side || e.Name.LocalName == side switch { "left" => "start", "right" => "end", _ => "" }).Remove();
            if (border != null)
            {
                borders ??= Property(tcPr, "tcBorders", CellOrder);
                var made = new XElement(w + side, new XAttribute(w + "val", border.Kind));
                if (border.Kind is not ("nil" or "none"))
                {
                    made.Add(new XAttribute(w + "sz", ((int)Math.Round(border.Width * 8)).ToString(CultureInfo.InvariantCulture)),
                        new XAttribute(w + "space", "0"), new XAttribute(w + "color", border.Color));
                }
                borders.Add(made);
                Order(borders, BorderOrder);
            }
            if (borders is { HasElements: false, HasAttributes: false })
                borders.Remove();
        }

        /// <summary>
        /// The cells reaching into a rectangle of the grid shaded with a color (w:shd clear, its fill; hex RRGGBB), as Word's Shading of
        /// cells writes it (measured in Word 365 by COM, scratchpad tables/looks.vbs); null, without a shading of their own.
        /// </summary>
        public void Shade(int top, int bottom, int left, int right, string? fill)
        {
            foreach (var cell in Cells.Where(c => c.Top < bottom && top < c.Bottom && c.Left < right && left < c.Right))
            {
                foreach (var tc in cell.Below.Prepend(cell.Element))
                {
                    var tcPr = tc.Element(w + "tcPr") ?? Made(tc, w + "tcPr");
                    if (fill == null)
                    {
                        tcPr.Element(w + "shd")?.Remove();
                        continue;
                    }
                    var shd = Property(tcPr, "shd", CellOrder);
                    shd.RemoveAttributes();
                    shd.SetAttributeValue(w + "val", "clear");
                    shd.SetAttributeValue(w + "color", "auto");
                    shd.SetAttributeValue(w + "fill", fill);
                }
            }
        }

        /// <summary>
        /// The table aligned between the text's edges, as Word's Rows.Alignment writes it (measured in Word 365 by COM, scratchpad
        /// tables/looks.vbs): centered or right (w:jc in the table's properties and in each row's), its indent gone; left (null), neither.
        /// </summary>
        public void Align(string? alignment)
        {
            var tblPr = table.Element(w + "tblPr") ?? Made(table, w + "tblPr");
            tblPr.Element(w + "tblInd")?.Remove();
            if (alignment == null)
                tblPr.Element(w + "jc")?.Remove();
            else
                Property(tblPr, "jc", TableOrder).SetAttributeValue(w + "val", alignment);
            foreach (var row in Rows)
            {
                if (alignment != null)
                    Property(row.Element(w + "trPr") ?? Made(row, w + "trPr"), "jc", RowOrder).SetAttributeValue(w + "val", alignment);
                else if (row.Element(w + "trPr") is { } trPr)
                {
                    trPr.Element(w + "jc")?.Remove();
                    if (!trPr.HasElements && !trPr.HasAttributes)
                        trPr.Remove();
                }
            }
        }

        /// <summary>How the table stands between the text's edges (w:jc): center, right (end), or null for left.</summary>
        public string? Alignment => table.Element(w + "tblPr")?.Element(w + "jc")?.Attribute(w + "val")?.Value switch
        {
            "center" => "center",
            "right" or "end" => "right",
            _ => null,
        };

        /// <summary>
        /// The table as wide as a width (twips), its columns in proportion, standing at the text's left edge (no indent, no alignment), as
        /// a new table stands.
        /// </summary>
        public void Widen(int whole)
        {
            Fit(whole);
            Align(null);
        }

        /// <summary>
        /// What the cells reaching into a rectangle of the grid hold taken away, as Word's Delete does over cells selected (measured in
        /// Word 365 by COM, scratchpad tables/looks.vbs): each left with an empty paragraph like its last one. Returns the first of them.
        /// </summary>
        public Cell? Clear(int top, int bottom, int left, int right)
        {
            Cell? first = null;
            foreach (var cell in Cells.Where(c => c.Top < bottom && top < c.Bottom && c.Left < right && left < c.Right).OrderBy(c => c.Top).ThenBy(c => c.Left))
            {
                first ??= cell;
                var empty = EmptyLike(cell.Element);
                foreach (var node in Content(cell).ToList())
                    node.Remove();
                cell.Element.Add(empty);
            }
            return first;
        }

        /// <summary>
        /// Moves a line of the grid to stand so many twips from the table's left edge, as dragging a line between columns does in Word
        /// (Columns.SetWidth with wdAdjustFirstColumn): the column right of it giving way, the table as wide as it was; the table's
        /// right edge makes its last column wider or narrower; its left edge moves the table (its indent) and its first column's left,
        /// in a table aligned centered or right its first column's alone (Word has no indent for it then). No column narrower than
        /// <paramref name="least"/>.
        /// </summary>
        public void MoveLine(int line, int at, int least)
        {
            if (line == 0)
            {
                var moved = Math.Clamp(at, -Grid[0] * 4, Grid[0] - least);
                Grid[0] -= moved;
                if (Alignment == null)
                    Indent(moved);
                return;
            }
            var left = Line(line - 1);
            if (line == Grid.Count)
            {
                Grid[^1] = Math.Max(least, at - left);
                return;
            }
            var right = Line(line + 1);
            var to = Math.Clamp(at, left + least, right - least);
            Grid[line - 1] = to - left;
            Grid[line] = right - to;
        }

        /// <summary>A row at least so high (twips, Word's trHeight without a rule); none where it is 0.</summary>
        public void SetHeight(int row, int height)
        {
            var trPr = Rows[row].Element(w + "trPr");
            if (height <= 0)
            {
                trPr?.Element(w + "trHeight")?.Remove();
                return;
            }
            trPr ??= Made(Rows[row], w + "trPr");
            var made = Property(trPr, "trHeight", RowOrder);
            made.RemoveAttributes();
            made.SetAttributeValue(w + "val", height.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>The table's indent from the text's edge moved by so many twips (w:tblInd).</summary>
        void Indent(int moved)
        {
            var tblPr = table.Element(w + "tblPr") ?? Made(table, w + "tblPr");
            var indent = tblPr.Element(w + "tblInd");
            var now = indent != null && indent.Attribute(w + "type")?.Value is null or "dxa" ? Number(indent, "w") : 0;
            var made = Property(tblPr, "tblInd", TableOrder);
            made.RemoveAttributes();
            made.SetAttributeValue(w + "w", (now + moved).ToString(CultureInfo.InvariantCulture));
            made.SetAttributeValue(w + "type", "dxa");
        }

        /// <summary>The columns' widths scaled to make up a width, as whole twips (the rest to the last one).</summary>
        void Fit(int whole)
        {
            var now = Grid.Sum();
            if (now == whole || now <= 0)
                return;
            for (var k = 0; k < Grid.Count; k++)
                Grid[k] = Math.Max(1, (int)Math.Round(Grid[k] * (double)whole / now));
            Grid[^1] += whole - Grid.Sum();
        }

        /// <summary>A width parted into even columns as Word parts it: each the whole twips, the rest one each to the last ones.</summary>
        public static List<int> Even(int whole, int columns)
        {
            var (each, rest) = (whole / columns, whole % columns);
            return [.. Enumerable.Range(0, columns).Select(k => each + (k >= columns - rest ? 1 : 0))];
        }

        /// <summary>The table written back: its grid, and its rows with their cells (see TableShape).</summary>
        public void Write()
        {
            var grid = table.Element(w + "tblGrid");
            if (grid == null)
            {
                grid = new XElement(w + "tblGrid");
                if (table.Element(w + "tblPr") is { } tblPr)
                    tblPr.AddAfterSelf(grid);
                else
                    table.AddFirst(grid);
            }
            grid.RemoveNodes();
            foreach (var width in Grid)
                grid.Add(new XElement(w + "gridCol", new XAttribute(w + "w", width.ToString(CultureInfo.InvariantCulture))));
            foreach (var tr in table.Elements(w + "tr").ToList())
                tr.Remove();
            foreach (var cell in Cells)
                foreach (var below in cell.Below)
                    below.Remove();
            for (var r = 0; r < Rows.Count; r++)
            {
                var tr = Rows[r];
                foreach (var tc in tr.Elements(w + "tc").ToList())
                    tc.Remove();
                var cells = Cells.Where(c => c.Top <= r && r < c.Bottom).OrderBy(c => c.Left).ToList();
                if (cells.Count == 0)
                    throw new InvalidOperationException("A row is left without a cell.");
                for (var k = 1; k < cells.Count; k++)
                    if (cells[k].Left != cells[k - 1].Right)
                        throw new InvalidOperationException("A row's cells do not follow one another.");
                SetGrid(tr, "gridBefore", cells[0].Left);
                SetGrid(tr, "gridAfter", Grid.Count - cells[^1].Right);
                foreach (var cell in cells)
                {
                    var tc = r == cell.Top ? cell.Element : Continuing(cell, r - cell.Top - 1);
                    if (tc.Parent != null)
                        tc.Remove();
                    Shape(tc, cell, r == cell.Top);
                    tr.Add(tc);
                }
                table.Add(tr);
            }
        }

        /// <summary>The widths of the grid written back alone, every cell as wide as the columns it spans: the rows as they are.</summary>
        public void WriteWidths()
        {
            var grid = table.Element(w + "tblGrid");
            if (grid == null || grid.Elements(w + "gridCol").Count() != Grid.Count)
            {
                Write();
                return;
            }
            foreach (var (col, width) in grid.Elements(w + "gridCol").Zip(Grid))
                col.SetAttributeValue(w + "w", width.ToString(CultureInfo.InvariantCulture));
            foreach (var cell in Cells)
                foreach (var tc in cell.Below.Prepend(cell.Element))
                    Shape(tc, cell, null);
        }

        /// <summary>The w:tc that goes on with a cell's merge down in its row so many below its first: the one read there, else a new one.</summary>
        XElement Continuing(Cell cell, int index)
        {
            while (cell.Below.Count <= index)
            {
                var made = new XElement(w + "tc", cell.Element.Element(w + "tcPr") is { } tcPr ? new XElement(tcPr) : null, EmptyLike(cell.Element));
                cell.Below.Add(made);
            }
            return cell.Below[index];
        }

        /// <summary>A cell's own properties as the shape says them: its width, its span and its merge down (<paramref name="first"/> null: its width alone).</summary>
        void Shape(XElement tc, Cell cell, bool? first)
        {
            var tcPr = tc.Element(w + "tcPr") ?? Made(tc, w + "tcPr");
            var width = Property(tcPr, "tcW", CellOrder);
            width.RemoveAttributes();
            width.SetAttributeValue(w + "w", Grid.Skip(cell.Left).Take(cell.Right - cell.Left).Sum().ToString(CultureInfo.InvariantCulture));
            width.SetAttributeValue(w + "type", "dxa");
            if (first is not { } top)
                return;
            tcPr.Element(w + "hMerge")?.Remove();
            if (cell.Right - cell.Left > 1)
                Property(tcPr, "gridSpan", CellOrder).SetAttributeValue(w + "val", (cell.Right - cell.Left).ToString(CultureInfo.InvariantCulture));
            else
                tcPr.Element(w + "gridSpan")?.Remove();
            if (cell.Bottom - cell.Top > 1)
            {
                var merge = Property(tcPr, "vMerge", CellOrder);
                merge.RemoveAttributes();
                if (top)
                    merge.SetAttributeValue(w + "val", "restart");
            }
            else
                tcPr.Element(w + "vMerge")?.Remove();
            // A cell must hold a paragraph last (Word does not open one whose cell ends with a table).
            if (tc.Elements().LastOrDefault() is not { } last || last.Name != w + "p")
                tc.Add(EmptyLike(tc));
        }

        /// <summary>A row's grid columns before or after its cells (w:gridBefore, w:gridAfter, their widths w:wBefore, w:wAfter).</summary>
        void SetGrid(XElement tr, string name, int columns)
        {
            var trPr = tr.Element(w + "trPr");
            var widthName = name == "gridBefore" ? "wBefore" : "wAfter";
            if (columns <= 0)
            {
                trPr?.Element(w + name)?.Remove();
                trPr?.Element(w + widthName)?.Remove();
                if (trPr != null && !trPr.HasElements && !trPr.HasAttributes)
                    trPr.Remove();
                return;
            }
            trPr ??= Made(tr, w + "trPr");
            Property(trPr, name, RowOrder).SetAttributeValue(w + "val", columns.ToString(CultureInfo.InvariantCulture));
            var width = Property(trPr, widthName, RowOrder);
            width.RemoveAttributes();
            var span = name == "gridBefore" ? Grid.Take(columns) : Grid.Skip(Grid.Count - columns);
            width.SetAttributeValue(w + "w", span.Sum().ToString(CultureInfo.InvariantCulture));
            width.SetAttributeValue(w + "type", "dxa");
        }

        /// <summary>A new cell like another: its properties, and an empty paragraph like its last one (its properties and its mark's).</summary>
        XElement Fresh(Cell like) =>
            new(w + "tc", like.Element.Element(w + "tcPr") is { } tcPr ? new XElement(tcPr) : null, EmptyLike(like.Element));

        /// <summary>An empty paragraph like the last one of a cell: its properties (its mark's formatting with them), no ids, no text.</summary>
        XElement EmptyLike(XElement tc)
        {
            var last = tc.Elements(w + "p").LastOrDefault();
            var made = new XElement(w + "p", last?.Element(w + "pPr") is { } pPr ? new XElement(pPr) : null);
            made.Element(w + "pPr")?.Element(w + "sectPr")?.Remove();
            made.Element(w + "pPr")?.Element(w + "pPrChange")?.Remove();
            made.Element(w + "pPr")?.Element(w + "rPr")?.Elements().Where(e => e.Name.LocalName is "ins" or "del" or "moveFrom" or "moveTo").Remove();
            return made;
        }

        /// <summary>A cell's content: what it holds besides its properties.</summary>
        IEnumerable<XElement> Content(Cell cell) => cell.Element.Elements().Where(e => e.Name != w + "tcPr");

        /// <summary>Whether a cell holds nothing to see or keep: no text, no drawing, object, field, table or other mark of content.</summary>
        public bool Empty(Cell cell) => !Content(cell).SelectMany(e => e.DescendantsAndSelf()).Any(e =>
            (e.Name == w + "t" && e.Value.Length > 0) || e.Name.LocalName is "drawing" or "pict" or "object" or "sym" or "tab" or "br" or "cr" or "fldChar"
                or "fldSimple" or "tbl" or "footnoteReference" or "endnoteReference" or "commentReference" or "oMath" or "oMathPara" or "bookmarkStart"
                or "ptab" or "noBreakHyphen" or "softHyphen" or "instrText" or "delText");

        /// <summary>The text of a cell, its paragraphs one after another, a space between.</summary>
        public string Text(Cell cell) =>
            string.Join(" ", Content(cell).Where(e => e.Name == w + "p").Select(p => string.Concat(p.Descendants(w + "t").Select(t => t.Value))));

        /// <summary>A row's own properties without the grid before and after it, which the shape writes.</summary>
        void Strip(XElement row)
        {
            if (row.Element(w + "trPr") is not { } trPr)
                return;
            foreach (var name in new[] { "gridBefore", "gridAfter", "wBefore", "wAfter", "ins", "del", "trPrChange" })
                trPr.Element(w + name)?.Remove();
            if (!trPr.HasElements && !trPr.HasAttributes)
                trPr.Remove();
        }

        static int Number(XElement? e, string name = "val") =>
            e?.Attribute(e.Name.Namespace + name)?.Value is { } value && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;

        /// <summary>A cell's width as w:tcW gives it in twips; 0 where it gives none so (none, a percentage, auto).</summary>
        static int Width(XElement tc)
        {
            var w = tc.Name.Namespace;
            return tc.Element(w + "tcPr")?.Element(w + "tcW") is { } width && width.Attribute(w + "type")?.Value is null or "dxa" ? Number(width, "w") : 0;
        }

        static void Order(XElement properties, string[] order)
        {
            var sorted = properties.Elements().OrderBy(e => Array.IndexOf(order, e.Name.LocalName) is var k and >= 0 ? k : order.Length).ToList();
            foreach (var e in sorted)
                e.Remove();
            properties.Add(sorted);
        }
    }

    /// <summary>The order of the cell properties in w:tcPr (CT_TcPr).</summary>
    static readonly string[] CellOrder =
    [
        "cnfStyle", "tcW", "gridSpan", "hMerge", "vMerge", "tcBorders", "shd", "noWrap", "tcMar", "textDirection", "tcFitText", "vAlign", "hideMark",
        "headers", "cellIns", "cellDel", "cellMerge", "tcPrChange",
    ];

    /// <summary>The order of the row properties in w:trPr (CT_TrPr).</summary>
    static readonly string[] RowOrder =
    [
        "cnfStyle", "divId", "gridBefore", "gridAfter", "wBefore", "wAfter", "cantSplit", "trHeight", "tblHeader", "tblCellSpacing", "jc", "hidden",
        "ins", "del", "trPrChange",
    ];

    /// <summary>The order of the table properties in w:tblPr (CT_TblPr).</summary>
    static readonly string[] TableOrder =
    [
        "tblStyle", "tblpPr", "tblOverlap", "bidiVisual", "tblStyleRowBandSize", "tblStyleColBandSize", "tblW", "jc", "tblCellSpacing", "tblInd",
        "tblBorders", "shd", "tblLayout", "tblCellMar", "tblLook", "tblCaption", "tblDescription", "tblPrChange",
    ];

    /// <summary>The order of a cell's lines in w:tcBorders (CT_TcBorders).</summary>
    static readonly string[] BorderOrder = ["top", "start", "left", "bottom", "end", "right", "insideH", "insideV", "tl2br", "tr2bl"];

    /// <summary>
    /// A table changed as a whole in a stretch (see TableShape): its shape read, changed by <paramref name="change"/>, and written back,
    /// the rows anew or, where <paramref name="widths"/>, its grid's widths alone. Returns the stretch's new text and where in it the
    /// first paragraph of the cell the change names starts (-1 for none), to find it again.
    /// </summary>
    public static (string Text, int At) Reshape(Stretch stretch, TableBlock table, Func<TableShape, TableShape.Cell?> change, bool widths = false)
    {
        var tbl = stretch.Find(table.Element) ?? throw new NotSupportedException("The table is not where it was read.");
        var shape = TableShape.Of(tbl);
        var named = change(shape);
        if (widths)
            shape.WriteWidths();
        else
            shape.Write();
        var start = stretch.Text.Start(tbl);
        var at = -1;
        if (named?.Element.Elements(tbl.Name.Namespace + "p").FirstOrDefault() is { } p)
        {
            // Where the paragraph stands in the table written: marked for a moment and found by its mark, the text before its
            // start the same without it (a mark goes after the element's other attributes).
            var mark = tbl.Name.Namespace + "fsMark";
            p.SetAttributeValue(mark, "1");
            var marked = PartText.Xml(tbl);
            p.SetAttributeValue(mark, null);
            var found = marked.IndexOf("fsMark=\"1\"", StringComparison.Ordinal);
            if (found >= 0)
                at = start - stretch.Head + marked.LastIndexOf('<', found);
        }
        var xml = PartText.Xml(tbl);
        return (stretch.Spliced([(start, stretch.Text.End(start), xml)]), at);
    }
}
