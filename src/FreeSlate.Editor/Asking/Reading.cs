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
/// ":::". And the focus of a wish told in words.
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

    /// <summary>The document's body in the notation, paragraph by paragraph from the first, with the ids Claude writes to.</summary>
    public static string Document(IEditableDocument document)
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
                    Table(document, cells, text);
                continue;
            }
            tableShown = null;
            text.Append('¶').Append(p.ToString(CultureInfo.InvariantCulture)).Append(' ')
                .Append(Prefix(document.ParagraphStyle(p))).Append(Line(document, p)).Append('\n');
        }
        if (columns > 1)
            text.Append(":::\n");
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

    /// <summary>A table as GitHub writes one, after the ids of its first and last paragraph; a cell's paragraphs split by &lt;br&gt;.</summary>
    static void Table(IEditableDocument document, TableCells table, StringBuilder text)
    {
        text.Append('¶').Append(table.Cells[0].First.ToString(CultureInfo.InvariantCulture)).Append("–¶")
            .Append(table.Cells[^1].Last.ToString(CultureInfo.InvariantCulture))
            .Append(" table, ").Append(table.Rows.ToString(CultureInfo.InvariantCulture)).Append(" rows × ")
            .Append(table.Columns.ToString(CultureInfo.InvariantCulture)).Append(" columns:\n");
        for (var row = 0; row < table.Rows; row++)
        {
            text.Append('|');
            for (var column = 0; column < table.Columns; column++)
            {
                var cell = table.Cells.FirstOrDefault(c => c.Top <= row && row < c.Bottom && c.Left <= column && column < c.Right);
                // A merged cell shows its text where it begins.
                var shown = cell != null && cell.Top == row && cell.Left == column
                    ? string.Join("<br>", cell.Paragraphs.Select(p => Line(document, p).Replace("|", "\\|", StringComparison.Ordinal)))
                    : "";
                text.Append(' ').Append(shown).Append(" |");
            }
            text.Append('\n');
            if (row == 0)
                text.Append('|').Append(string.Concat(Enumerable.Repeat("---|", table.Columns))).Append('\n');
        }
    }

    /// <summary>A paragraph's text in the notation: its meanings marked, its drawings, tabs and breaks named.</summary>
    public static string Line(IEditableDocument document, int paragraph) => Line(document, paragraph, 0, document.Text(paragraph).Length);

    /// <summary>A stretch of a paragraph's text in the notation (see Line).</summary>
    public static string Line(IEditableDocument document, int paragraph, int from, int to)
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
                part.Append(Character(document, paragraph, text, c));
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

    /// <summary>A character as the notation writes it: a drawing or other object named, a tab, a line or page break marked, the notation's marks escaped.</summary>
    static string Character(IEditableDocument document, int paragraph, string text, int offset)
    {
        var c = text[offset];
        switch (c)
        {
            case '￼':
                return document.DrawingOf(paragraph, offset) is { } drawing
                    ? drawing.Description is { Length: > 0 } description ? $"[picture: {description}]" : "[picture]"
                    : "[*]";
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
    public static string Focus(IEditableDocument document, Focus focus)
    {
        var said = new StringBuilder();
        string Id(int paragraph) => "¶" + paragraph.ToString(CultureInfo.InvariantCulture);
        switch (focus.Kind)
        {
            case Asking.Focus.Kinds.Words:
                said.Append("The words \"").Append(Line(document, focus.First, focus.Offset, focus.Offset + focus.Length)).Append("\" in ").Append(Id(focus.First))
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
                    .Append(Tail(Line(document, focus.First, 0, focus.Offset), 60)).Append("\". Writing text without blocks to the focus puts it in at the cursor; blocks go after ")
                    .Append(Id(focus.First)).Append('.');
                break;
            case Asking.Focus.Kinds.Empty:
                said.Append("The cursor stands in the empty paragraph ").Append(Id(focus.First)).Append(". Writing to the focus puts the content there.");
                break;
            case Asking.Focus.Kinds.Picture:
                var description = document.DrawingOf(focus.First, focus.Offset)?.Description;
                said.Append("A picture in ").Append(Id(focus.First)).Append(" is selected");
                if (description is { Length: > 0 })
                    said.Append(" (its description: \"").Append(description).Append("\")");
                said.Append("; it is attached. Writing to the focus puts the content after ").Append(Id(focus.First))
                    .Append("; describe_picture gives the picture its description.");
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
