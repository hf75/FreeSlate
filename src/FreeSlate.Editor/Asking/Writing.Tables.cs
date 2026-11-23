using FreeSlate.Editing;
using FreeSlate.Editor.Writing;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// Tables as a write puts them in (see Notation.Table, TableWords): a new one made as GitHub's table gives it (see Markup.Table), then given
/// what the notation says besides, as the ring gives it: cells merged, the cells' fills, where their text stands and which way it runs,
/// their lines, the table's look, head, alignment, float and pages turned across. A table of the text a write takes away waits after the
/// place (see KeepTables) to be written again in place, so that what the notation does not say of it stays (its columns' widths, its
/// rows' heights, its lines): rows and columns added like its last ones or taken away at its end, cells merged or split back, the text of
/// the cells that read otherwise written anew, its looks as the notation says them.
/// </summary>
sealed partial class Writing
{
    /// <summary>The tables of the text taken away, in their order, waiting to be written again (see Rework), and the empty paragraphs they wait before.</summary>
    readonly List<int> keptTables = [];
    readonly List<int> parking = [];

    /// <summary>The footnotes' marks in tables' cells: by the table, the cell's row and column, the offset in its first paragraph, the label (see End).</summary>
    readonly List<(int Table, int Row, int Column, int Offset, string Label)> tableNotes = [];

    /// <summary>The tables of the body whose paragraphs are all among those from one to another (in reading order), not those in tables.</summary>
    List<int> TablesIn(int first, int last)
    {
        var tables = new List<int>();
        if (word == null)
            return tables;
        var range = new List<int>();
        for (int? paragraph = first; paragraph is { } at; paragraph = document.Next(at))
        {
            range.Add(at);
            if (at == last)
                break;
        }
        var within = range.ToHashSet();
        var taken = new HashSet<int>();
        foreach (var paragraph in range)
        {
            if (taken.Contains(paragraph) || word.TableOf(paragraph) is not { } table || tables.Contains(table) || word.CellsOf(table) is not { } cells)
                continue;
            var all = cells.Cells.SelectMany(c => c.Paragraphs).ToList();
            if (cells.Cells[0].First == paragraph && all.All(within.Contains))
            {
                tables.Add(table);
                taken.UnionWith(all);
            }
        }
        return tables;
    }

    /// <summary>
    /// The tables of text about to be taken away (see Replace) moved after the empty paragraph made for the place, each before an empty
    /// paragraph of its own (tables that meet join), to wait there to be written again; what else the text holds then stands together,
    /// from its first paragraph on.
    /// </summary>
    void KeepTables(IReadOnlyList<int> tables, int made)
    {
        var park = made;
        foreach (var table in tables)
        {
            var before = document.InsertBlocks(park, "<w:p/>")[0];
            word!.MoveTable(table, before);
            parking.Add(before);
            keptTables.Add(table);
            park = before;
        }
    }

    /// <summary>The tables kept that were not written again taken away, and the paragraphs they waited before.</summary>
    void EndTables()
    {
        foreach (var unused in keptTables.ToList())
            Step(() =>
            {
                word!.RemoveTable(unused);
                return null;
            }, quiet: true);
        keptTables.Clear();
        foreach (var empty in parking.Where(document.Contains).ToList())
            Step(() =>
            {
                document.Remove(empty);
                return null;
            }, quiet: true);
        parking.Clear();
    }

    /// <summary>
    /// The table kept that a table the notation gives is most likely written from: the one most of whose cells read as its cells do in the
    /// same places, then one of as many columns and rows, else the first (taken from those waiting).
    /// </summary>
    int Kept(Notation.Table wanted)
    {
        int Likeness(int table)
        {
            if (word!.CellsOf(table) is not { } cells)
                return -1;
            var alike = cells.Cells.Count(c => c.Top < wanted.Texts.Count && c.Left < wanted.Texts[c.Top].Count
                && wanted.Texts[c.Top][c.Left].Length > 0 && Same(Reading.CellText(document, c, objects).Replace("\\|", "|", StringComparison.Ordinal), wanted.Texts[c.Top][c.Left]));
            return alike * 10 + (cells.Columns == wanted.Aligns.Count ? 5 : 0) + (cells.Rows == wanted.Rows.Count ? 3 : 0);
        }
        var best = keptTables.Select((table, order) => (table, order, likeness: Likeness(table))).OrderByDescending(t => t.likeness).ThenBy(t => t.order).First().table;
        keptTables.Remove(best);
        return best;
    }

    /// <summary>A table kept written again before the anchor (see the class): moved there, shaped, its cells' text and its looks as the notation says them.</summary>
    void Rework(int table, Notation.Table wanted)
    {
        if (anchor is not { } at || word == null)
            return;
        Step(() =>
        {
            if (AfterTable(at))
                document.InsertBlocks(at, "<w:p/>", before: true);
            word.MoveTable(table, at);
            Shape(table, wanted);
            Rewrite(table, wanted);
            Dress(table, wanted);
            var cells = word.CellsOf(table) ?? throw new InvalidOperationException("The table written again was not found again.");
            written.AddRange(cells.Cells.SelectMany(c => c.Paragraphs));
            return new TextPlace(cells.Cells[^1].Last, document.Text(cells.Cells[^1].Last).Length);
        });
    }

    /// <summary>A table as many rows and columns as the notation gives it (added like its last ones, or taken away at its end), its cells merged as it says.</summary>
    void Shape(int table, Notation.Table wanted)
    {
        var cells = word!.CellsOf(table)!;
        var (rows, columns) = (wanted.Rows.Count, wanted.Aligns.Count);
        if (rows > cells.Rows)
            Try(() => word.AddRows(table, cells.Rows, rows - cells.Rows, cells.Rows - 1));
        else if (rows < cells.Rows)
            Try(() => word.RemoveRows(table, rows, cells.Rows));
        cells = word.CellsOf(table)!;
        if (columns > cells.Columns)
            Try(() => word.AddColumns(table, cells.Columns, columns - cells.Columns, cells.Columns - 1));
        else if (columns < cells.Columns)
            Try(() => word.RemoveColumns(table, columns, cells.Columns));
        Merge(table, wanted, split: true);
    }

    /// <summary>
    /// A table's cells merged as the notation says (each "&lt;" with the cell before it, each "^" with the one above it); where
    /// <paramref name="split"/>, cells merged that it does not say split back first.
    /// </summary>
    void Merge(int table, Notation.Table wanted, bool split)
    {
        var spans = Spans(wanted);
        if (split)
        {
            foreach (var cell in word!.CellsOf(table)!.Cells.Where(c => c.Bottom - c.Top > 1 || c.Right - c.Left > 1).ToList())
            {
                if (spans.Contains((cell.Top, cell.Bottom, cell.Left, cell.Right)))
                    continue;
                if (cell.Right - cell.Left > 1)
                    Try(() => word.SplitCell(table, cell.Top, cell.Left, across: false, cell.Right - cell.Left));
                if (cell.Bottom - cell.Top > 1)
                {
                    for (var column = cell.Left; column < cell.Right; column++)
                    {
                        var at = column;
                        Try(() => word.SplitCell(table, cell.Top, at, across: true, cell.Bottom - cell.Top));
                    }
                }
            }
        }
        var now = word!.CellsOf(table)!.Cells.Select(c => (c.Top, c.Bottom, c.Left, c.Right)).ToHashSet();
        foreach (var span in spans.Where(s => !now.Contains(s)))
            Try(() => word.MergeCells(table, span.Top, span.Bottom, span.Left, span.Right));
    }

    /// <summary>The rectangles the notation merges cells into (more than one place each), each cell's place resolved to the one it begins at.</summary>
    List<(int Top, int Bottom, int Left, int Right)> Spans(Notation.Table wanted)
    {
        var (rows, columns) = (wanted.Merged.Count, wanted.Aligns.Count);
        var owner = new (int Row, int Column)[rows, columns];
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var mark = column < wanted.Merged[row].Count ? wanted.Merged[row][column] : '\0';
                owner[row, column] = mark == '<' && column > 0 ? owner[row, column - 1] : mark == '^' && row > 0 ? owner[row - 1, column] : (row, column);
            }
        }
        var spans = new List<(int, int, int, int)>();
        foreach (var group in Enumerable.Range(0, rows).SelectMany(r => Enumerable.Range(0, columns).Select(c => (Row: r, Column: c))).GroupBy(p => owner[p.Row, p.Column]))
        {
            if (group.Count() < 2)
                continue;
            var (top, bottom, left, right) = (group.Min(p => p.Row), group.Max(p => p.Row) + 1, group.Min(p => p.Column), group.Max(p => p.Column) + 1);
            // A rectangle only: every place in it of the same cell.
            if (group.Count() == (bottom - top) * (right - left))
                spans.Add((top, bottom, left, right));
            else
                problems.Add($"The cells merged at row {group.Key.Row + 1}, column {group.Key.Column + 1} make no rectangle; they were left apart.");
        }
        return spans;
    }

    /// <summary>The text of a table's cells that reads otherwise than the notation gives it written anew, their objects kept (see Objects.Keep).</summary>
    void Rewrite(int table, Notation.Table wanted)
    {
        foreach (var cell in word!.CellsOf(table)!.Cells.ToList())
        {
            if (cell.Top >= wanted.Texts.Count || cell.Left >= wanted.Texts[cell.Top].Count)
                continue;
            if (Same(Reading.CellText(document, cell, objects).Replace("\\|", "|", StringComparison.Ordinal), wanted.Texts[cell.Top][cell.Left]))
                continue;
            foreach (var paragraph in cell.Paragraphs)
                objects.Keep(paragraph);
            var first = cell.First;
            while (document.After(first) is { } next && cell.Paragraphs.Contains(next))
                first = document.Join(first);
            document.Replace(first, 0, document.Text(first).Length, "");
            var made = Markup.RunsOf(wanted.Rows[cell.Top][cell.Left], Kept);
            if (made.Runs.Length > 0)
                document.InsertMarkup(first, 0, made.Runs);
            tableNotes.AddRange(made.Notes.Select(n => (table, cell.Top, cell.Left, n.Offset, n.Label)));
        }
    }

    /// <summary>Two texts in the notation alike but for their spaces.</summary>
    static bool Same(string one, string other) =>
        string.Join(' ', one.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)) == string.Join(' ', other.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// A table given its looks as the notation says them (see TableWords): each cell's fill, where its text stands (its own, else its
    /// column's, else at the top left), which way it runs, its lines where it names them; the table's look (the tone's own where none is
    /// named), its head (a head row unless head=none), its alignment where one is named, its float (or none), its pages across (or not).
    /// </summary>
    void Dress(int table, Notation.Table wanted)
    {
        var cells = word!.CellsOf(table)!;
        var tone = Tones.Of(document);
        foreach (var cell in cells.Cells)
        {
            var looks = cell.Top < wanted.Looks.Count && cell.Left < wanted.Looks[cell.Top].Count
                ? wanted.Looks[cell.Top][cell.Left] : new Dictionary<string, string>();
            var named = looks.GetValueOrDefault("fill");
            var fill = TableWords.FillHex(named, out var known);
            if (!known)
                problems.Add($"fill={named} is no fill (gray, yellow, orange, green, blue, rose, violet or none).");
            else if (fill != word.CellFill(table, cell.Top, cell.Left))
                Try(() => word.ShadeCells(table, cell.Top, cell.Bottom, cell.Left, cell.Right, fill));
            var across = TableWords.Across(looks.GetValueOrDefault("align") ?? wanted.Aligns.ElementAtOrDefault(cell.Left));
            var down = TableWords.Down(looks.GetValueOrDefault("valign"));
            if (word.CellPlace(table, cell.Top, cell.Left) is var (vertical, horizontal) && (vertical != down || TableWords.Across(horizontal) != across))
                Try(() => word.PlaceText(table, cell.Top, cell.Bottom, cell.Left, cell.Right, down, across));
            var direction = TableWords.Direction(looks.GetValueOrDefault("turn"));
            if (direction != word.CellDirection(table, cell.Top, cell.Left))
                Try(() => word.TurnText(table, cell.Top, cell.Bottom, cell.Left, cell.Right, direction));
            if (looks.GetValueOrDefault("lines") is { } given)
            {
                var (lines, unread) = TableWords.Lines(given);
                foreach (var (which, kind) in lines)
                    Try(() => word.SetLines(table, cell.Top, cell.Bottom, cell.Left, cell.Right, which, kind?.Border(tone) ?? CellBorder.None));
                if (unread.Count > 0)
                    problems.Add($"lines={given}: {string.Join(", ", unread)} not read (which:kind, which top, bottom, left, right, outside, inside, across, upright or all, kind fine, strong, double, dotted, dashed or none).");
            }
        }
        var settings = wanted.Settings;
        var look = settings.GetValueOrDefault("look");
        if ((look != null ? TableWords.LookStyle(look) : Tables.Table.Style) is not { } style)
            problems.Add($"look={look} is no look (tone, grid, rows, book or box).");
        else if (style != word.TableLook(table)?.Style)
            Try(() => word.StyleTable(table, style));
        var head = settings.GetValueOrDefault("head")?.Trim().ToLowerInvariant() is not ("none" or "no" or "false" or "0");
        if (head != word.CellsOf(table)!.HeadRow)
            Try(() => word.SetHead(table, head));
        var floats = settings.GetValueOrDefault("float")?.Trim().ToLowerInvariant() is "left" or "right" or "center" ? settings["float"].Trim().ToLowerInvariant() : null;
        var floating = word.TableFloat(table);
        if (floats != null && floats != floating)
            Try(() => word.FloatTable(table, floats == "left" ? null : floats));
        else if (floats == null && floating != null)
            Try(() => word.AlignTable(table, word.TableLook(table)?.Alignment));
        if (settings.GetValueOrDefault("align")?.Trim().ToLowerInvariant() is { } align)
        {
            var aligned = align is "center" or "right" ? align : null;
            if (aligned != word.TableLook(table)?.Alignment)
                Try(() => word.AlignTable(table, aligned));
        }
        var turned = settings.GetValueOrDefault("across")?.Trim().ToLowerInvariant() is "yes" or "true" or "1";
        if (turned != word.TableAcross(table))
            Try(() => word.TurnTablePages(table, turned));
        if (settings.GetValueOrDefault("width")?.Trim().ToLowerInvariant() is "full" or "100%")
            Try(() => word.WidenTable(table));
        if (settings.GetValueOrDefault("columns")?.Trim().ToLowerInvariant() is "even" or "equal")
            Try(() => word.DistributeColumns(table));
        // Turned or tilted: in a frame of its own, which the picture tool then places, turns and tilts as a picture.
        double? turn = double.TryParse(settings.GetValueOrDefault("turn"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var degrees) ? degrees : null;
        var tilt = settings.GetValueOrDefault("tilt") is { } tiltName ? Objects.Tilts.FirstOrDefault(t => t.Name == tiltName.Trim().ToLowerInvariant()).Camera : null;
        if (settings.ContainsKey("tilt") && tilt == null && settings["tilt"].Trim().ToLowerInvariant() is not ("none" or "flat"))
            problems.Add($"tilt={settings["tilt"]} is no tilt (above, rising, right, falling, below, lying or left).");
        if (turn != null || tilt != null)
            Try(() =>
            {
                word.FrameTable(table, new DrawingFormatting
                {
                    Rotation = turn is { } angle ? (angle % 360 + 360) % 360 : null,
                    Tilt = tilt != null ? new PictureTilt(tilt) : null,
                });
            });
    }

    /// <summary>A change to a table made where the document makes it, else why not said to Claude.</summary>
    void Try(Action change)
    {
        try
        {
            change();
        }
        catch (Exception e) when (e is NotSupportedException or ArgumentException or InvalidOperationException)
        {
            problems.Add(e.Message);
        }
    }

    void Try(Func<int> change) => Try(() => { change(); });
}
