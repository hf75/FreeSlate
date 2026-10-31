using FreeSlate.Docx;

namespace FreeSlate;

/// <summary>
/// The tables of a Word document as deleting a selection with one end in them changes them (catalog: table.structure):
/// Word takes the rows a selection reaches into from outside a table away whole.
/// </summary>
public sealed partial class WordDocument
{
    /// <summary>
    /// Takes away a selection from a place to a later one whose paragraphs are not of one body, cell or text box, one in a
    /// table (at any depth) the other stands beside, as deleting it does in Word (measured in Word 365 by COM, Delete,
    /// Backspace and Selection.Delete alike; typed over with <paramref name="typing"/>). From a paragraph into a table
    /// after it the paragraph's text from the place on, its mark, what stands between them and the table's rows up to the
    /// one the selection ends in (before the one it ends at the beginning of) go, and the paragraph's text before the
    /// place goes on at the beginning of the row after, a space between where the selection began at its end and took
    /// more than the mark (Word neither types over such a selection nor deletes it with track changes on: it does
    /// nothing); one into the table's last row takes the table away and joins the paragraph with the one after it, as a
    /// selection to that one's beginning does. From a table's row into a paragraph after the table the rows from that one
    /// on, what stands between (other tables too) and the paragraph's text up to the place go, the paragraph keeping its
    /// mark; one that ends where the paragraph just after the table begins selects cells (Word clears them, or takes rows
    /// away with Backspace). Returns where the cursor then stands, or null where the two paragraphs are of one body, cell
    /// or text box.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The ends stand in different cells of one table or select cells, each in a table of which rows would be left (Word
    /// joins the two), or what is taken away cannot be (see TextEdits.RemoveRows, Replace, RemoveBetween, Join); nothing
    /// changes.
    /// </exception>
    public (int Paragraph, int Offset)? RemoveAcross(int first, int offset, int last, int lastOffset, bool typing = false)
    {
        var (list, i, j, firstCell, lastCell) = Apart(first, last);
        if (firstCell == null && lastCell == null)
            return null;
        if (i == j)
            throw new NotSupportedException("The selection's ends stand in different cells of one table (Word clears cells or takes them away).");
        if (i > j)
            throw new ArgumentException("The first place comes after the last.", nameof(first));
        if (firstCell != null && lastCell != null)
        {
            // From one table into another: as to the paragraph after the second, where the selection reaches into its last row.
            var second = (TableBlock)list[j];
            if (RowOf(second, lastCell) != second.Table.Rows.Count - 1 || (lastOffset == 0 && ReferenceEquals(FirstOf(second.Table.Rows[^1]), Find(last).Paragraph))
                || j + 1 >= list.Count || list[j + 1] is not ParagraphBlock after)
                throw new NotSupportedException("The selection's ends stand in two tables of which rows would be left (Word joins them).");
            (last, lastOffset) = (after.Id, 0);
        }
        var start = version;
        try
        {
            return firstCell == null ? Into(first, offset, last, lastOffset, typing) : OutOf(first, last, lastOffset);
        }
        catch
        {
            // The changes made before the refusal, taken back and forgotten (see Refused).
            if (version > start)
            {
                Go(start);
                versions.RemoveRange(start + 1, versions.Count - start - 1);
            }
            throw;
        }
    }

    public bool OfOneList(int first, int last)
    {
        try
        {
            return Apart(first, last) is { FirstCell: null, LastCell: null };
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>A selection from a paragraph into a table after it taken away (see RemoveAcross).</summary>
    (int, int) Into(int first, int offset, int last, int lastOffset, bool typing)
    {
        var (list, i, j, _, cell) = Apart(first, last);
        var table = (TableBlock)list[j];
        var rows = table.Table.Rows;
        var row = RowOf(table, cell!);
        // Ending where a row begins, it reaches over the rows before it alone.
        if (lastOffset == 0 && ReferenceEquals(FirstOf(rows[row]), Find(last).Paragraph))
            row--;
        else if (row == rows.Count - 1)
        {
            if (j + 1 >= list.Count || list[j + 1] is not ParagraphBlock after)
                throw new NotSupportedException("No paragraph follows the table.");
            return Over(first, offset, after.Id, typing);
        }
        if (typing || Author != null)
            throw new NotSupportedException("Word does nothing with a selection from a paragraph into a table's rows when it is typed over or changes are tracked.");
        if (rows[row + 1].Cells.FirstOrDefault()?.Blocks.FirstOrDefault() is not ParagraphBlock next)
            throw new NotSupportedException("The row the paragraph's text would go on in does not begin with a paragraph.");
        var text = Text(first);
        var rest = Text(next.Id);
        var spaced = offset == text.Length && offset > 0 && rest.Length > 0 && !char.IsWhiteSpace(rest[0]) && !char.IsWhiteSpace(text[offset - 1])
            && (row >= 0 || j > i + 1);
        if (offset < text.Length)
            Replace(first, offset, text.Length - offset, "");
        // Found again: the change may have read the table anew.
        (list, i, j, _, _) = Apart(first, next.Id);
        table = (TableBlock)list[j];
        rows = table.Table.Rows;
        var (p, top) = Find(first);
        var taken = rows.Take(row + 1).Select(FirstOf).ToList();
        var into = (ParagraphBlock)rows[row + 1].Cells[0].Blocks[0];
        var body = ReferenceEquals(list, blocks);
        var at = -1;
        var where = Change(body ? p : top, body ? table : top, (stretch, tracking) =>
        {
            var (changed, joined) = TextEdits.RemoveRows(stretch, p, table, taken, all: false, into, tracking);
            at = joined;
            return changed;
        }, () => body ? [.. blocks.Take(i), WithRows(table, rows.Skip(row + 1)), .. blocks.Skip(j + 1)] : null);
        var id = Fresh().FirstOrDefault(f => at >= 0 && where(First(f)) == at)?.Id
            ?? throw Refused("The paragraph's content does not go into the row after.");
        if (!markup && !Reads(id, text[..offset] + rest))
            throw Refused("The paragraph's content would not read so in the row after: one of them holds what the document computes or keeps.");
        // A space between, as Word puts one where two paragraphs join.
        if (spaced)
            Replace(id, offset, 0, " ");
        return (id, offset);
    }

    /// <summary>
    /// A selection from a paragraph over whole tables to the beginning of the paragraph after them taken away, as deleting
    /// or typing over it does (see EditSession): the first's text from the place on, what stands between, and the two
    /// joined, a space between where it began at the first's end.
    /// </summary>
    (int, int) Over(int first, int offset, int after, bool typing)
    {
        var text = Text(first);
        var rest = Text(after);
        if (offset < text.Length)
            Replace(first, offset, text.Length - offset, "");
        RemoveBetween(first, after);
        var joined = Join(first);
        var at = joined == first ? offset : 0;
        if (!typing && offset == text.Length && offset > 0 && rest.Length > 0 && !char.IsWhiteSpace(rest[0]) && !char.IsWhiteSpace(text[offset - 1]))
            Replace(joined, at, 0, " ");
        return (joined, at);
    }

    /// <summary>A selection from a table's row into a paragraph after the table taken away (see RemoveAcross).</summary>
    (int, int) OutOf(int first, int last, int lastOffset)
    {
        var (list, i, j, cell, _) = Apart(first, last);
        if (lastOffset == 0 && j == i + 1)
            throw new NotSupportedException("The selection ends where the paragraph after the table begins: it selects cells (Word clears them, or takes rows away with Backspace).");
        if (lastOffset > 0)
            Replace(last, 0, lastOffset, "");
        (list, i, j, cell, _) = Apart(first, last);
        var table = (TableBlock)list[i];
        var row = RowOf(table, cell!);
        var (q, top) = Find(last);
        var rows = table.Table.Rows;
        var taken = rows.Skip(row).Select(FirstOf).ToList();
        var body = ReferenceEquals(list, blocks);
        Change(body ? table : top, body ? q : top, (stretch, tracking) => TextEdits.RemoveRows(stretch, q, table, taken, all: row == 0, null, tracking).Text,
            () => body && Author == null ? [.. blocks.Take(i), .. row > 0 ? [WithRows(table, rows.Take(row))] : Array.Empty<Block>(), .. blocks.Skip(j)] : null);
        return (last, 0);
    }

    /// <summary>
    /// Where two paragraphs stand: the deepest body, cell or text box both stand in, the places there of the blocks that
    /// hold them (themselves, or tables they stand in), and for one in such a table the list of the cell it stands in
    /// there (else null).
    /// </summary>
    (List<Block> List, int First, int Last, List<Block>? FirstCell, List<Block>? LastCell) Apart(int first, int last)
    {
        var (p, pTop) = Find(first);
        var (q, qTop) = Find(last);
        var (from, to) = (PathOf(pTop, p), PathOf(qTop, q));
        if (!ReferenceEquals(from[0].List, to[0].List))
            throw new NotSupportedException("The selection's ends are not of one story.");
        var level = 0;
        while (level + 1 < from.Count && level + 1 < to.Count && from[level].Index == to[level].Index && ReferenceEquals(from[level + 1].List, to[level + 1].List))
            level++;
        return (from[level].List, from[level].Index, to[level].Index, from.Count > level + 1 ? from[level + 1].List : null, to.Count > level + 1 ? to[level + 1].List : null);
    }

    /// <summary>
    /// The lists of blocks a paragraph stands in, from its story's (the body's, a note's, a header's or footer's) inward,
    /// each with the place there of the block that holds it: the cells' of tables, a text box's.
    /// </summary>
    List<(List<Block> List, int Index)> PathOf(Block top, ParagraphBlock paragraph)
    {
        var path = new List<(List<Block> List, int Index)> { TopList(top, walking: true) };
        bool Search(Block block)
        {
            if (ReferenceEquals(block, paragraph))
                return true;
            foreach (var list in ModelReader.Inner(block))
            {
                for (var k = 0; k < list.Count; k++)
                {
                    path.Add((list, k));
                    if (Search(list[k]))
                        return true;
                    path.RemoveAt(path.Count - 1);
                }
            }
            return false;
        }
        return Search(top) ? path : throw new InvalidOperationException("The paragraph is not in the block.");
    }

    /// <summary>The row of a table whose cell has a list of blocks.</summary>
    static int RowOf(TableBlock table, List<Block> cell) =>
        table.Table.Rows.FindIndex(r => r.Cells.Any(c => ReferenceEquals(c.Blocks, cell))) is var row and >= 0 ? row : throw new InvalidOperationException("The cell is not in the table.");

    /// <summary>The first paragraph of a row in reading order, which knows it (see TextEdits.RemoveRows).</summary>
    ParagraphBlock FirstOf(RowModel row) =>
        row.Cells.SelectMany(c => c.Blocks).SelectMany(Flat).FirstOrDefault() ?? throw new NotSupportedException("A row holds no paragraph.");

    /// <summary>A table with only some of its rows, as a change is known to leave it: the table read anew takes its ids, those of its cells' blocks too (see Change).</summary>
    static TableBlock WithRows(TableBlock table, IEnumerable<RowModel> rows)
    {
        var model = new TableModel();
        model.Rows.AddRange(rows);
        return new TableBlock(table.Element, model) { Id = table.Id };
    }
}
