using System.Globalization;
using System.Text;
using FreeSlate.Editing;
using FreeSlate.Editor.Writing;

namespace FreeSlate.Editor.Asking;

/// <summary>What a wish is about: the place the ring was opened at, as the editor's selection gave it when the wish was sent.</summary>
public sealed record Focus(Focus.Kinds Kind, int First, int Last, int Offset = 0, int Length = 0, int? Table = null)
{
    public enum Kinds
    {
        /// <summary>Words within one paragraph (from Offset, Length characters).</summary>
        Words,
        /// <summary>Whole paragraphs, First to Last.</summary>
        Paragraphs,
        /// <summary>The cursor in a paragraph with text, at Offset.</summary>
        Cursor,
        /// <summary>The cursor in an empty paragraph.</summary>
        Empty,
        /// <summary>A picture (or other drawing) whose character stands at Offset of First.</summary>
        Picture,
        /// <summary>A table: its first cell's first paragraph to its last cell's last one.</summary>
        Table,
    }

    /// <summary>The place a session's selection gives: a drawing, a table's cells, words, paragraphs, or the cursor.</summary>
    public static Focus Of(EditSession editing)
    {
        var document = editing.Document;
        var (start, end) = editing.Selection;
        if (editing.Drawing != null)
            return new(Kinds.Picture, start.Paragraph, start.Paragraph, start.Offset, 1);
        if (editing.Table is { } table && (editing.CellSelection != null || !editing.Selecting))
            return new(Kinds.Table, table.Cells[0].First, table.Cells[^1].Last, Table: table.Table);
        if (editing.Selecting)
        {
            // A selection ending at the start of a paragraph ends with the one before.
            var last = end.Paragraph;
            if (end.Offset == 0 && last != start.Paragraph && document.Previous(last) is { } before)
                last = before;
            var whole = start.Offset == 0 && (last != end.Paragraph || end.Offset >= document.Text(last).Length);
            if (start.Paragraph == last && !whole)
                return new(Kinds.Words, start.Paragraph, start.Paragraph, start.Offset, end.Offset - start.Offset);
            return new(Kinds.Paragraphs, start.Paragraph, last);
        }
        var caret = editing.Caret;
        return document.Text(caret.Paragraph).Length == 0
            ? new(Kinds.Empty, caret.Paragraph, caret.Paragraph)
            : new(Kinds.Cursor, caret.Paragraph, caret.Paragraph, caret.Offset);
    }
}

/// <summary>
/// A document as Claude reads it (see Notation, Prompt): each paragraph on a line of its own after its id (¶12), in the notation it
/// writes in; a table after the ids of its first and last paragraphs, as GitHub writes one; text in columns between "::: columns 2" and
/// ":::"; its objects by their tokens (see Objects), told after the text with its notes. And the focus of a wish told in words.
/// </summary>
static class Reading
{
    /// <summary>Prefixes of the levels' styles (see Levels), as the notation writes them.</summary>
    static string Prefix(string? style) => style switch
    {
        null or "" or "Normal" => "",
        "Title" => "# ",
        "Heading1" => "## ",
        "Heading2" => "### ",
        "ListBullet" => "- ",
        "ListBullet2" => "  - ",
        var other => "{" + other + "} ",
    };

    /// <summary>
    /// The document's body in the notation, paragraph by paragraph from the first, with the ids Claude writes to; then its objects (where
    /// and how each stands, see Objects.Details) and its notes' texts.
    /// </summary>
    public static string Document(IEditableDocument document, Objects objects)
    {
        var text = new StringBuilder();
        var word = document as WordDocument;
        var columns = 1;
        int? tableShown = null;
        for (var paragraph = document.Beginning; paragraph is { } p; paragraph = document.Next(p))
        {
            // Text in columns as the notation writes it.
            if (word != null && word.TableOf(p) == null)
            {
                var section = Section(word, p);
                if (section != columns)
                {
                    if (columns > 1)
                        text.Append(":::\n");
                    if (section > 1)
                        text.Append("::: columns ").Append(section.ToString(CultureInfo.InvariantCulture)).Append('\n');
                    columns = section;
                }
            }
            if (word?.TableOf(p) is { } table)
            {
                if (tableShown == table)
                    continue;
                tableShown = table;
                if (word.CellsOf(table) is { } cells)
                    Table(word, cells, text, objects);
                continue;
            }
            tableShown = null;
            text.Append('¶').Append(p.ToString(CultureInfo.InvariantCulture)).Append(' ')
                .Append(Prefix(document.ParagraphStyle(p))).Append(Line(document, p, objects)).Append('\n');
        }
        if (columns > 1)
            text.Append(":::\n");
        if (objects.Known.Any())
        {
            text.Append("\nObjects:\n");
            foreach (var id in objects.Known.ToList())
                text.Append(objects.Details(id)).Append('\n');
        }
        if (objects.Notes.Any())
        {
            text.Append("\nNotes:\n");
            foreach (var note in objects.Notes)
                text.Append("[^").Append(note.Label).Append("]: ").Append(note.Endnote ? "(endnote) " : "").Append(note.Text).Append('\n');
        }
        return text.ToString();
    }

    /// <summary>How many columns the section a paragraph of the body stands in has (1 where it cannot be told).</summary>
    static int Section(WordDocument word, int paragraph)
    {
        try
        {
            return Math.Max(1, word.SectionOf(paragraph).Columns ?? 1);
        }
        catch (NotSupportedException)
        {
            return 1;
        }
    }

    /// <summary>
    /// A table as GitHub writes one, after the ids of its first and last paragraph, with what the notation says besides (see TableWords):
    /// the table's own settings on a line before it where it is not as a new table is ("{table look=book head=none}"); each column's
    /// alignment where its cells below the head share one; each cell's own settings at its start ("{fill=yellow valign=middle}"); a cell
    /// merged with the one before it as "&lt;", with the one above it as "^"; a cell's paragraphs split by &lt;br&gt;.
    /// </summary>
    static void Table(WordDocument word, TableCells table, StringBuilder text, Objects objects)
    {
        text.Append('¶').Append(table.Cells[0].First.ToString(CultureInfo.InvariantCulture)).Append("–¶")
            .Append(table.Cells[^1].Last.ToString(CultureInfo.InvariantCulture))
            .Append(" table, ").Append(table.Rows.ToString(CultureInfo.InvariantCulture)).Append(" rows × ")
            .Append(table.Columns.ToString(CultureInfo.InvariantCulture)).Append(" columns:\n");
        var settings = new List<string>();
        var look = word.TableLook(table.Table);
        if (TableWords.LookName(look?.Style) is { } name and not "tone")
            settings.Add("look=" + name);
        if (!table.HeadRow)
            settings.Add("head=none");
        if (look?.Alignment is { } alignment)
            settings.Add("align=" + alignment);
        if (word.TableFloat(table.Table) is { } floats)
            settings.Add("float=" + floats);
        if (word.TableAcross(table.Table))
            settings.Add("across=yes");
        if (settings.Count > 0)
            text.Append("{table ").Append(string.Join(" ", settings)).Append("}\n");
        var places = table.Cells.ToDictionary(c => c, c => word.CellPlace(table.Table, c.Top, c.Left));
        var aligns = Aligns(table, places);
        for (var row = 0; row < table.Rows; row++)
        {
            text.Append('|');
            for (var column = 0; column < table.Columns; column++)
            {
                var cell = table.Cells.FirstOrDefault(c => c.Top <= row && row < c.Bottom && c.Left <= column && column < c.Right);
                var shown = cell == null ? "" : cell.Top != row ? "^" : cell.Left != column ? "<" : Cell(word, table, cell, places[cell], aligns[column], objects);
                text.Append(' ').Append(shown).Append(" |");
            }
            text.Append('\n');
            if (row == 0)
                text.Append('|').Append(string.Concat(aligns.Select(a => a switch { "center" => ":---:|", "right" => "---:|", _ => "---|" }))).Append('\n');
        }
    }

    /// <summary>Each column's alignment: the one all its cells of their own below the head share (all of them in a table of a row), null where that is the left or there is none.</summary>
    public static string?[] Aligns(TableCells table, IReadOnlyDictionary<TableCell, (string Vertical, string Horizontal)?> places)
    {
        var aligns = new string?[table.Columns];
        for (var column = 0; column < table.Columns; column++)
        {
            var shared = table.Cells.Where(c => c.Left == column && c.Right == column + 1 && (c.Top > 0 || table.Rows == 1))
                .Select(c => TableWords.Across(places[c]?.Horizontal)).Distinct().ToList();
            aligns[column] = shared is [var one] && one != "left" ? one : null;
        }
        return aligns;
    }

    /// <summary>A cell as the notation writes it: its own settings where it has them (fill, align where not its column's, valign, turn), its text.</summary>
    static string Cell(WordDocument word, TableCells table, TableCell cell, (string Vertical, string Horizontal)? place, string? align, Objects objects)
    {
        var settings = new List<string>();
        if (word.CellFill(table.Table, cell.Top, cell.Left) is { } fill)
            settings.Add("fill=" + TableWords.FillName(fill));
        if (place is var (vertical, horizontal))
        {
            if (TableWords.Across(horizontal) != (align ?? "left"))
                settings.Add("align=" + TableWords.Across(horizontal));
            if (vertical != "top")
                settings.Add("valign=" + TableWords.DownName(vertical));
        }
        if (TableWords.TurnName(word.CellDirection(table.Table, cell.Top, cell.Left)) is { } turn)
            settings.Add("turn=" + turn);
        var content = CellText(word, cell, objects);
        if (content is "^" or "<")
            content = "\\" + content;
        return (settings.Count > 0 ? "{" + string.Join(" ", settings) + "} " : "") + content;
    }

    /// <summary>A cell's text in the notation: its paragraphs split by &lt;br&gt;, its bars escaped.</summary>
    public static string CellText(IEditableDocument document, TableCell cell, Objects objects) =>
        string.Join("<br>", cell.Paragraphs.Select(p => Line(document, p, objects).Replace("|", "\\|", StringComparison.Ordinal)));

    /// <summary>A paragraph's text in the notation: its meanings marked, its objects' tokens and notes' marks, its tabs and breaks named.</summary>
    public static string Line(IEditableDocument document, int paragraph, Objects objects) => Line(document, paragraph, 0, document.Text(paragraph).Length, objects);

    /// <summary>A stretch of a paragraph's text in the notation (see Line).</summary>
    public static string Line(IEditableDocument document, int paragraph, int from, int to, Objects objects)
    {
        var text = document.Text(paragraph);
        var styles = document.CharacterStyles(paragraph);
        var line = new StringBuilder();
        var k = from;
        while (k < to)
        {
            var styled = styles.FirstOrDefault(s => s.Offset <= k && k < s.Offset + s.Length);
            var end = styled.Length > 0 ? Math.Min(to, styled.Offset + styled.Length) : Math.Min(to, styles.Where(s => s.Offset > k).Select(s => s.Offset).DefaultIfEmpty(to).Min());
            var part = new StringBuilder();
            for (var c = k; c < end; c++)
                part.Append(Character(paragraph, text, c, objects));
            var shown = part.ToString();
            line.Append(styled.Length > 0 && styled.Style.Length > 0 && shown.Trim().Length > 0 ? styled.Style switch
            {
                "Important" => "**" + shown + "**",
                "Emphasized" => "*" + shown + "*",
                var style => "{" + style + "|" + shown + "}",
            } : shown);
            k = Math.Max(end, k + 1);
        }
        return line.ToString();
    }

    /// <summary>A character as the notation writes it: an object by its token, a note by its mark, a line or page break marked, the notation's marks escaped.</summary>
    static string Character(int paragraph, string text, int offset, Objects objects)
    {
        var c = text[offset];
        switch (c)
        {
            case '￼':
                return objects.Token(paragraph, offset);
            case '\v':
                return "<br>";
            case '\f':
                return " [page break] ";
            case '\u000E':
                return " [column break] ";
            case '*' or '_' or '{' or '}' or '[' or ']' or '|' or '`' or '\\' or '<':
                return "\\" + c;
            default:
                return c.ToString();
        }
    }

    /// <summary>
    /// The focus of a wish told for Claude: what it is, its text, what writing to it does, and the page it stands on.
    /// </summary>
    public static string Focus(IEditableDocument document, Focus focus, Objects objects)
    {
        var said = new StringBuilder();
        string Id(int paragraph) => "¶" + paragraph.ToString(CultureInfo.InvariantCulture);
        switch (focus.Kind)
        {
            case Asking.Focus.Kinds.Words:
                said.Append("The words \"").Append(Line(document, focus.First, focus.Offset, focus.Offset + focus.Length, objects)).Append("\" in ").Append(Id(focus.First))
                    .Append(" are selected. Writing to the focus replaces just these words: write them as text within the paragraph, without blocks.");
                break;
            case Asking.Focus.Kinds.Paragraphs:
                said.Append("The paragraphs ").Append(Id(focus.First));
                if (focus.Last != focus.First)
                    said.Append(" to ").Append(Id(focus.Last));
                said.Append(" are selected. Writing to the focus replaces them.");
                break;
            case Asking.Focus.Kinds.Cursor:
                said.Append("The cursor stands in ").Append(Id(focus.First)).Append(" after \"")
                    .Append(Tail(Line(document, focus.First, 0, focus.Offset, objects), 60)).Append("\". Writing text without blocks to the focus puts it in at the cursor; blocks go after ")
                    .Append(Id(focus.First)).Append('.');
                break;
            case Asking.Focus.Kinds.Empty:
                said.Append("The cursor stands in the empty paragraph ").Append(Id(focus.First)).Append(". Writing to the focus puts the content there.");
                break;
            case Asking.Focus.Kinds.Picture:
                said.Append("The object ").Append(objects.Token(focus.First, focus.Offset)).Append(" in ").Append(Id(focus.First))
                    .Append(" is selected; where it is a picture, it is attached. Writing to the focus puts the content after ").Append(Id(focus.First))
                    .Append("; the picture tool changes the object itself.");
                break;
            case Asking.Focus.Kinds.Table:
                said.Append("The table ").Append(Id(focus.First)).Append(" to ").Append(Id(focus.Last))
                    .Append(" is selected. Writing to the focus replaces the whole table: write it again whole, as it is to read.");
                break;
        }
        if (document.PageOf(focus.First, focus.Offset) is { } page)
            said.Append(" It is on page ").Append((page + 1).ToString(CultureInfo.InvariantCulture)).Append(" of ")
                .Append(document.Scenes().Count.ToString(CultureInfo.InvariantCulture)).Append('.');
        return said.ToString();
    }

    static string Tail(string text, int most) => text.Length <= most ? text : "…" + text[^most..];
}
