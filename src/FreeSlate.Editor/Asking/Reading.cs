using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
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
static partial class Reading
{
    /// <summary>
    /// Prefixes of the levels' styles (see Levels), as the notation writes them: a numbered point's with the number or letter it shows
    /// (its list's label, "3.", "b)").
    /// </summary>
    public static string Prefix(string? style, string? label = null) => style switch
    {
        null or "" or "Normal" => "",
        "Title" => "# ",
        "Heading1" => "## ",
        "Heading2" => "### ",
        "ListBullet" => "- ",
        "ListBullet2" => "  - ",
        "ListNumber" => (label is { Length: > 0 } ? label : "1.") + " ",
        "ListNumber2" => "  " + (label is { Length: > 0 } ? label : "a)") + " ",
        var other => "{" + other + "} ",
    };

    /// <summary>
    /// A paragraph's line in the notation: its level's prefix and its text, text whose start would read as the mark of another block (a
    /// heading's "#", a point's "-" or "1.", a quotation's "&gt;") with that mark escaped, so that it writes back as text.
    /// </summary>
    public static string Paragraph(IEditableDocument document, int paragraph, Objects objects, string? align = null)
    {
        var prefix = Prefix(document.ParagraphStyle(paragraph), (document as WordDocument)?.ListLabel(paragraph));
        var line = Line(document, paragraph, objects);
        if (prefix.Length == 0 && BlockStart().Match(line) is { Success: true } start)
            line = start.Groups[1].Success ? line.Insert(start.Groups[1].Length, "\\") : "\\" + line;
        // Its own formatting before its text (see ParagraphLook), an alignment as the paragraphs of its kind have it (a shape's) aside.
        return prefix + (document is WordDocument word ? Settings(word, paragraph, align) : "") + line;
    }

    /// <summary>The start of a line that the notation reads as a block's mark: a number and its point (the number the group), "#", "-", "+", "&gt;".</summary>
    [GeneratedRegex(@"^(?:(\d{1,3})[.)]\s|#{1,6}\s|[-+]\s|>)")]
    private static partial Regex BlockStart();

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
        // An envelope before the letter and a sheet of labels read as the "Mailings:" line says them, not as paragraphs (see Mailings).
        var mailed = word != null ? Mailings.Hidden(word) : [];
        copiedLast = null;
        for (var paragraph = document.Beginning; paragraph is { } p; paragraph = document.Next(p))
        {
            if (mailed.Contains(p))
                continue;
            // A section that ended with the paragraph before: its break on a line of its own (see BreakLine), between the columns' marks.
            var boundary = word != null && document.Previous(p) is { } before && !mailed.Contains(before) ? BreakLine(word, before) : null;
            // Text in columns as the notation writes it.
            if (word != null && word.TableOf(p) == null)
            {
                var section = Section(word, p);
                if (section != columns)
                {
                    if (columns > 1)
                        text.Append(":::\n");
                    if (boundary is { Length: > 0 })
                        text.Append(boundary).Append('\n');
                    boundary = null;
                    if (section > 1)
                        text.Append(ColumnsMark(word, p, section)).Append('\n');
                    columns = section;
                }
            }
            if (boundary is { Length: > 0 })
                text.Append(boundary).Append('\n');
            // A cover of FreeSlate's designs as one line, with the ids of its first and last paragraph (see Covers).
            if (word != null && Covers.At(word, p) is var (coverFirst, coverLast, coverLine) && coverFirst == p)
            {
                text.Append('¶').Append(p.ToString(CultureInfo.InvariantCulture)).Append(coverLast != p ? "–¶" + coverLast.ToString(CultureInfo.InvariantCulture) : "")
                    .Append(' ').Append(coverLine).Append('\n');
                while (document.Next(p) is { } next && p != coverLast)
                    p = next;
                paragraph = p;
                continue;
            }
            // A bibliography as one line, with the ids of its first and last paragraph (see Citing), its table (IEEE's) within it.
            if (word != null && Citing.Around(word, p) is var (bibliographyFirst, bibliographyLast) && bibliographyFirst == p)
            {
                text.Append('¶').Append(p.ToString(CultureInfo.InvariantCulture)).Append("–¶").Append(bibliographyLast.ToString(CultureInfo.InvariantCulture)).Append(" {bibliography}\n");
                while (document.Next(p) is { } next && p != bibliographyLast)
                    p = next;
                paragraph = p;
                tableShown = null;
                continue;
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
            // A table of authorities as one line, with the ids of its first and last paragraph (see Authorities).
            if (word != null && Authorities.At(word, p) is var (authoritiesEnd, authoritiesLine))
            {
                text.Append('¶').Append(p.ToString(CultureInfo.InvariantCulture)).Append("–¶").Append(authoritiesEnd.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(authoritiesLine).Append('\n');
                while (document.Next(p) is { } next && p != authoritiesEnd)
                    p = next;
                paragraph = p;
                continue;
            }
            // An index as one line, with the ids of its first and last paragraph (see Index).
            if (word != null && Index.At(word, p) is { } indexEnd)
            {
                text.Append('¶').Append(p.ToString(CultureInfo.InvariantCulture)).Append("–¶").Append(indexEnd.ToString(CultureInfo.InvariantCulture)).Append(" {index}\n");
                while (document.Next(p) is { } next && p != indexEnd)
                    p = next;
                paragraph = p;
                continue;
            }
            // A table of contents as one line, with the ids of its first and last paragraph (see Contents).
            if (word != null && Contents.At(word, p) is var (last, title, label))
            {
                text.Append('¶').Append(p.ToString(CultureInfo.InvariantCulture)).Append("–¶").Append(last.ToString(CultureInfo.InvariantCulture))
                    .Append(label is { } figures ? " {figures " + figures : " {contents" + (title is { } named ? " " + named : "")).Append("}\n");
                while (document.Next(p) is { } next && p != last)
                    p = next;
                paragraph = p;
                continue;
            }
            // A drop cap and the paragraph it begins as one line with the ids of both, "{drop}" before its text (see Notation.DropMark).
            if (word != null && word.DropCapOf(p) is var (lines, margin) && word.After(p) is { } begun && word.TableOf(begun) == null
                && word.DropCapOf(begun) == null && word.Text(p) is { Length: > 0 } letter)
            {
                var whole = Paragraph(document, begun, objects);
                var own = Line(document, begun, objects);
                var prefix = whole.EndsWith(own, StringComparison.Ordinal) ? whole[..^own.Length] : "";
                var mark = "{drop" + (lines != 3 ? " " + lines.ToString(CultureInfo.InvariantCulture) : "") + (margin ? " margin" : "") + "} ";
                text.Append('¶').Append(p.ToString(CultureInfo.InvariantCulture)).Append("–¶").Append(begun.ToString(CultureInfo.InvariantCulture)).Append(' ')
                    .Append(prefix).Append(mark).Append(Line(document, p, objects, plain: true)).Append(own).Append('\n');
                paragraph = p = begun;
                continue;
            }
            // What the notation says otherwise than by a paragraph's text: a page or column break alone, a line across, an empty paragraph
            // ending a section (said by its break's line, the columns' marks or the tables' settings), formatting of its own the settings do not
            // say (as WordprocessingML).
            if (word != null && Special(word, p) is var (shownAs, hidden))
            {
                if (!hidden)
                    text.Append('¶').Append(p.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(shownAs).Append('\n');
                continue;
            }
            // A formula in a paragraph of its own as the notation writes one ($$…$$).
            if (document.Text(p) == "￼" && objects.Formula(p, 0) is (var latex, true))
            {
                text.Append('¶').Append(p.ToString(CultureInfo.InvariantCulture)).Append(" $$").Append(latex).Append("$$\n");
                continue;
            }
            text.Append('¶').Append(p.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(Paragraph(document, p, objects)).Append('\n');
        }
        if (columns > 1)
            text.Append(":::\n");
        copiedLast = null;
        if (word != null && PageOf(word, objects) is { Length: > 0 } page)
            text.Append("\nPage: ").Append(page).Append('\n').Append(SectionsOf(word, objects));
        if (word != null && Mailings.PropertiesOf(word) is { Length: > 0 } properties)
            text.Append("\nProperties: ").Append(properties).Append('\n');
        if (word != null && Mailings.MailingsOf(word) is { Length: > 0 } mailings)
            text.Append("\nMailings: ").Append(mailings).Append('\n');
        if (objects.Known.Any())
        {
            text.Append("\nObjects:\n");
            foreach (var id in objects.Known.ToList())
                text.Append(objects.Details(id)).Append('\n');
        }
        if (word != null && Commenting.Read(word, objects) is { Length: > 0 } comments)
            text.Append("\nComments:\n").Append(comments);
        if (word != null && Reviewing.Read(word) is { Length: > 0 } changes)
            text.Append("\nChanges:\n").Append(changes);
        if (word != null && Citing.Block(word) is { } sources)
            text.Append("\nSources:\n").Append(sources).Append('\n');
        if (objects.Notes.Any())
        {
            text.Append("\nNotes:\n");
            foreach (var note in objects.Notes)
                text.Append("[^").Append(note.Label).Append("]: ").Append(note.Endnote ? "(endnote) " : "").Append(note.Text).Append('\n');
        }
        return text.ToString();
    }

    /// <summary>
    /// The page as Claude reads it and the page tool sets it (see Arranging.Page): the paper and its orientation, the margins (top right
    /// bottom left), whether the first page has a header and footer of its own, and the header and footer as the notation writes them.
    /// </summary>
    public static string PageOf(WordDocument word, Objects objects)
    {
        // The letter's pages, after an envelope before it (see Mailings).
        if (word.AfterEnvelope is not { } first)
            return "";
        SectionFormatting section;
        try
        {
            section = word.SectionOf(first);
        }
        catch (NotSupportedException)
        {
            return "";
        }
        static string Cm(double? points) => (Math.Abs(points ?? 0) / 72 * 2.54).ToString("0.##", CultureInfo.InvariantCulture) + "cm";
        var (width, height) = (section.PageWidth ?? 595.3, section.PageHeight ?? 841.9);
        var paper = Arranging.Papers.FirstOrDefault(p => Math.Abs(p.Value.Width - Math.Min(width, height)) < 3 && Math.Abs(p.Value.Height - Math.Max(width, height)) < 3).Key
            ?? $"{Cm(width)} × {Cm(height)}";
        var said = new StringBuilder(paper).Append(width > height ? " landscape" : " portrait");
        said.Append(", margins ").Append(Cm(section.Top)).Append(' ').Append(Cm(section.Right)).Append(' ').Append(Cm(section.Bottom)).Append(' ')
            .Append(Cm(section.Left)).Append(" (top right bottom left)");
        // The first page's own header and footer, as PageMore says them.
        foreach (var (name, footer) in new[] { ("header", false), ("footer", true) })
        {
            int? story;
            try
            {
                story = word.ShownHeader(first, "default", footer);
            }
            catch (NotSupportedException)
            {
                story = null;
            }
            var lines = new List<string>();
            for (var paragraph = story; paragraph is { } p; paragraph = word.After(p))
                lines.Add(StoryLine(word, p, objects).Replace("\t", "\\t", StringComparison.Ordinal));
            if (string.Concat(lines).Trim().Length > 0)
                said.Append(", ").Append(name).Append(": \"").Append(string.Join(" / ", lines)).Append('"');
            if (!footer && story is { } header && Arranging.WatermarkText(word, header) is { } watermark)
                said.Append(", watermark: ").Append(Watermarks.Said(word, header, watermark));
        }
        if (section.Border is { Kind: not "none" } border)
            said.Append(", border: ").Append(border.Kind).Append(border.Color is { } color ? " #" + color : "")
                .Append(' ').Append(border.Width.ToString("0.##", CultureInfo.InvariantCulture)).Append("pt round the pages");
        if (section.PageNumberFormat is { } numbering and not "decimal" || section.PageNumberStart is >= 0)
            said.Append(", page numbers ").Append(section.PageNumberFormat switch
            {
                "lowerRoman" => "i",
                "upperRoman" => "I",
                "lowerLetter" => "a",
                "upperLetter" => "A",
                _ => "1",
            }).Append(section.PageNumberStart is >= 0 and var start ? " from " + start.ToString(CultureInfo.InvariantCulture) : "");
        if (section.LineNumbers is { CountBy: > 0 } numbered)
            said.Append(", line numbers every ").Append(numbered.CountBy.ToString(CultureInfo.InvariantCulture))
                .Append(numbered.Restart switch { "newSection" => " (each section)", "continuous" => " (continuous)", _ => "" });
        if (word.AutoHyphenation)
            said.Append(", words hyphenated");
        if (word.PageColor is { } pages)
            said.Append(", page colour: #").Append(pages);
        said.Append(PageMore(word, objects, first, section));
        if (word.TrackRevisions)
            said.Append(", Track Changes on");
        // Word's Restrict Editing (see Mailings.Protections).
        if (word.Protection is { } protection)
            said.Append(", editing restricted to ").Append(Mailings.ProtectionSaid(protection)).Append(" (protection: ").Append(Mailings.ProtectionName(protection)).Append(')');
        return said.ToString();
    }

    /// <summary>A paragraph of a header or footer in the notation (see Line), a watermark it holds left out (see Arranging.Watermark).</summary>
    public static string StoryLine(WordDocument word, int paragraph, Objects objects) =>
        Arranging.WatermarkAt(word, paragraph) is { } mark
            ? Line(word, paragraph, 0, mark, objects) + Line(word, paragraph, mark + 1, word.Text(paragraph).Length, objects)
            : Line(word, paragraph, objects);

    /// <summary>The properties a paragraph of the body has (its w:pPr as copying keeps it), with the paragraph as copied; null where it cannot be copied.</summary>
    static (XElement Paragraph, XElement? Properties)? Copied(WordDocument word, int paragraph)
    {
        if (copiedLast is var (lastWord, lastParagraph, changes, lastCopy, lastProperties) && ReferenceEquals(lastWord, word) && lastParagraph == paragraph && changes == word.Changes)
            return (lastCopy, lastProperties);
        // Copied to the next one's start the paragraph comes whole, its mark and properties with it (an empty one too); the last of its body,
        // or one before a table, as far as its text goes.
        var copied = (word.After(paragraph) is { } next ? word.Copy(paragraph, 0, next, 0) : null)
            ?? word.Copy(paragraph, 0, paragraph, word.Text(paragraph).Length);
        if (copied is not [var first, ..])
            return null;
        try
        {
            var p = Markup.Parsed(first).Elements().FirstOrDefault();
            if (p == null)
                return null;
            var properties = p.Elements().FirstOrDefault(e => e.Name.LocalName == "pPr");
            copiedLast = (word, paragraph, word.Changes, p, properties);
            return (p, properties);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    /// <summary>
    /// A paragraph the notation says otherwise than by its text, and how (see Document): a page break alone as \pagebreak, a column break
    /// alone as \columnbreak, an empty paragraph with a line under it as Markup's Rule writes it as ---, an empty paragraph that ends a section
    /// as nothing (its section break is a line of its own, see BreakLine); a paragraph with properties the notation does not say
    /// (see ParagraphLook) as its WordprocessingML, its section aside. Null for a paragraph the notation says by its level, its settings and
    /// its text.
    /// </summary>
    static (string Shown, bool Hidden)? Special(WordDocument word, int paragraph)
    {
        var text = word.Text(paragraph);
        if (text.Trim(' ', '\f').Length == 0 && text.Contains('\f'))
            return ("\\pagebreak", false);
        if (text.Trim(' ', '\u000E').Length == 0 && text.Contains('\u000E'))
            return ("\\columnbreak", false);
        if (text.Length == 0 && Ends(word, paragraph))
            return ("", true);
        if (Own(word, paragraph) is not { Count: > 0 } own || Copied(word, paragraph) is not var (copied, _))
            return null;
        if (text.Length == 0 && own is [{ Name.LocalName: "pBdr" } border] && border.Elements().ToList() is [{ Name.LocalName: "bottom" } rule]
            && (string?)rule.Attributes().FirstOrDefault(a => a.Name.LocalName == "val") == "single" && (string?)rule.Attributes().FirstOrDefault(a => a.Name.LocalName == "sz") == "6")
            return ("---", false);
        if (ParagraphLook.Of(own, text == "￼").Other.Count == 0)
            return null;
        // Its section's end is a line of its own after it.
        var raw = new XElement(copied);
        raw.Descendants().Where(e => e.Name.LocalName == "sectPr" && e.Parent?.Name.LocalName == "pPr").Remove();
        return (raw.ToString(SaveOptions.DisableFormatting).Replace(" xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"", "", StringComparison.Ordinal), false);
    }

    /// <summary>Whether a paragraph of the body ends its section (its properties hold the section's w:sectPr).</summary>
    static bool Ends(WordDocument word, int paragraph)
    {
        try
        {
            return word.SectionEnd(paragraph) == paragraph;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>Whether two sections set up their pages alike (paper, orientation, margins), as columns' sections do.</summary>
    static bool Alike(SectionFormatting one, SectionFormatting other) =>
        Close(one.PageWidth, other.PageWidth) && Close(one.PageHeight, other.PageHeight) && Close(one.Top, other.Top) && Close(one.Bottom, other.Bottom)
        && Close(one.Left, other.Left) && Close(one.Right, other.Right);

    static bool Close(double? one, double? other) => Math.Abs((one ?? 0) - (other ?? 0)) < 0.5;

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

    /// <summary>
    /// A paragraph's text in the notation: its meanings marked, its objects' tokens and notes' marks, its tabs and breaks named; its
    /// anchors at its end (see Anchors).
    /// </summary>
    public static string Line(IEditableDocument document, int paragraph, Objects objects, bool plain = false) =>
        document is WordDocument word
            ? Reviewing.WithMark(word, paragraph, Line(document, paragraph, 0, document.Text(paragraph).Length, objects, plain: plain))
              + Anchors(word, paragraph) + string.Concat(word.IndexEntriesOf(paragraph).Select(e => " {index " + e.Replace(":", ": ", StringComparison.Ordinal) + "}"))
              + string.Concat(word.AuthoritiesOf(paragraph).Select(Authorities.Text))
            : Line(document, paragraph, 0, document.Text(paragraph).Length, objects, plain: plain);

    /// <summary>
    /// The anchors a paragraph's line ends with, " {#name}" each: the bookmarks that begin in it, but those round a caption's number (which
    /// reads as its anchor, see Numbering), a table of contents' (_Toc…, see Contents) and Word's others whose names begin with "_" where
    /// no field of the document leads to them or reads them.
    /// </summary>
    static string Anchors(WordDocument word, int paragraph)
    {
        var spans = word.BookmarkSpans(paragraph);
        if (spans.Count == 0)
            return "";
        var anchors = new StringBuilder();
        // A legacy form field's bookmark of its name, which comes with the field (see Forms).
        var fields = word.FormFieldsOf(paragraph).Select(f => f.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var (name, _, end) in spans)
        {
            if (fields.Contains(name))
                continue;
            if (name.StartsWith("_Toc", StringComparison.Ordinal) || end is { } after && after > 0 && word.FieldInstruction(paragraph, after - 1) is { } instruction
                && Numbering(word, paragraph, word.Text(paragraph), after - 1, instruction) != null)
                continue;
            if (name.StartsWith('_') && !word.ReferencedBookmarks().Contains(name))
                continue;
            anchors.Append(" {#").Append(name).Append('}');
        }
        return anchors.ToString();
    }

    /// <summary>
    /// A caption's number as the notation writes it (see Notation.Anchor): a SEQ field of a label alone (in arabic numbers) right after
    /// that label ("Figure 1"), {#name} where a bookmark holds the two from the label's beginning, {#} where none does; null for another
    /// field or a sequence otherwise numbered (its token).
    /// </summary>
    static string? Numbering(WordDocument word, int paragraph, string text, int offset, string instruction)
    {
        var words = instruction.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2 || !words[0].Equals("SEQ", StringComparison.OrdinalIgnoreCase))
            return null;
        for (var k = 2; k < words.Length; k += 2)
        {
            if (words[k] != "\\*" || k + 1 >= words.Length || !(words[k + 1].Equals("ARABIC", StringComparison.OrdinalIgnoreCase)
                || words[k + 1].Equals("MERGEFORMAT", StringComparison.OrdinalIgnoreCase)))
                return null;
        }
        if (LabelBefore().Match(text[..offset]) is not { Success: true } label || !label.Groups[1].Value.Equals(words[1], StringComparison.OrdinalIgnoreCase))
            return null;
        var named = word.BookmarkSpans(paragraph).FirstOrDefault(s => s.Start <= label.Index && s.End == offset + 1);
        return named.Name is { } name ? "{#" + name + "}" : "{#}";
    }

    [GeneratedRegex(@"(\p{L}[\p{L}\p{N}_]{0,39})\.?\s*$")]
    private static partial Regex LabelBefore();

    /// <summary>A reference the notation writes (see Notation.Reference): REF of a bookmark as {ref name}, PAGEREF as {page name}; null for others.</summary>
    static string? Referring(string instruction)
    {
        var words = instruction.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2 || words[1].StartsWith('\\') || words.Skip(2).Any(w => w is not ("\\h" or "\\*") && !w.Equals("MERGEFORMAT", StringComparison.OrdinalIgnoreCase)))
            return null;
        return words[0].ToUpperInvariant() switch
        {
            "REF" => "{ref " + words[1] + "}",
            "PAGEREF" => "{page " + words[1] + "}",
            _ => null,
        };
    }

    /// <summary>
    /// A stretch of a paragraph's text in the notation (see Line): its direct formatting said too (see RunLook) but where it is
    /// <paramref name="plain"/> (a drop cap's letter, whose look the drop cap is).
    /// </summary>
    public static string Line(IEditableDocument document, int paragraph, int from, int to, Objects objects, bool inLink = false, bool plain = false)
    {
        var text = document.Text(paragraph);
        var styles = document.CharacterStyles(paragraph);
        var word = document as WordDocument;
        // Its links as the notation writes them, their text as it reads within them, the link's own style aside.
        var links = word != null && !inLink ? word.LinksOf(paragraph).Where(l => l.Start >= from && l.End <= to && l.End > l.Start).ToList() : [];
        // Its citations as the notation writes them, "[@key, p. 33]" for the text they show (see Citing).
        var citations = word != null ? word.CitationsOf(paragraph).Where(c => c.Start >= from && c.End <= to && c.End > c.Start).ToList() : [];
        // Its content controls, form fields, merge fields and facts' fields as the notation writes them, "{checkbox}" (see Forms).
        var forms = word != null ? Forms.Stretches(word, paragraph, objects).Where(f => f.Start >= from && f.End <= to && f.End > f.Start).ToList() : [];
        // Its runs' own looks (see Looks).
        var looks = word != null && !plain ? Looks(word, paragraph) : [];
        // Its tracked changes as the notation writes them (see Reviewing.Marker), a link and a citation one piece of them.
        var marker = word != null && !inLink && Reviewing.Spans(word, paragraph) is { Count: > 0 } spans
            ? new Reviewing.Marker([.. spans.Where(s => !s.Mark && (s.Deleted != null ? s.Offset >= from && s.Offset <= to : s.Offset < to && s.Offset + s.Length > from))]) : null;
        var line = new StringBuilder();
        var k = from;
        while (k < to)
        {
            marker?.At(k, line);
            if (citations.FirstOrDefault(c => c.Start == k) is { End: > 0 } citation && Citing.ItemsOf(citation.Instruction) is { } cited)
            {
                line.Append(Notation.CiteText(cited));
                k = citation.End;
                continue;
            }
            if (forms.FirstOrDefault(f => f.Start == k) is { End: > 0 } form)
            {
                line.Append(form.Said);
                k = form.End;
                continue;
            }
            if (links.FirstOrDefault(l => l.Start == k) is { End: > 0 } link)
            {
                line.Append('[').Append(Line(document, paragraph, link.Start, link.End, objects, inLink: true)).Append("](").Append(link.Target).Append(')');
                k = link.End;
                continue;
            }
            var styled = styles.FirstOrDefault(s => s.Offset <= k && k < s.Offset + s.Length);
            var end = styled.Length > 0 ? Math.Min(to, styled.Offset + styled.Length) : Math.Min(to, styles.Where(s => s.Offset > k).Select(s => s.Offset).DefaultIfEmpty(to).Min());
            end = Math.Min(end, links.Where(l => l.Start > k).Select(l => l.Start).DefaultIfEmpty(end).Min());
            end = Math.Min(end, citations.Where(c => c.Start > k).Select(c => c.Start).DefaultIfEmpty(end).Min());
            end = Math.Min(end, forms.Where(f => f.Start > k).Select(f => f.Start).DefaultIfEmpty(end).Min());
            // A stretch ends where its runs' look does too, and where a tracked change begins or ends.
            var looked = looks.FirstOrDefault(l => l.Offset <= k && k < l.Offset + l.Length);
            end = Math.Min(end, looked.Length > 0 ? looked.Offset + looked.Length : looks.Where(l => l.Offset > k).Select(l => l.Offset).DefaultIfEmpty(end).Min());
            if (marker != null)
                end = marker.Next(k, end);
            // Runs of a look the notation does not say as their WordprocessingML.
            if (looked is { Length: > 0, Other: true } && word != null && Raw(word, paragraph, k, end) is { } raw)
            {
                line.Append(raw);
                k = Math.Max(end, k + 1);
                continue;
            }
            var part = new StringBuilder();
            for (var c = k; c < end; c++)
                part.Append(Character(word, paragraph, text, c, objects));
            line.Append(Wrapped(styled.Length > 0 ? styled.Style : null, looked.Length > 0 ? looked.Look : null, part.ToString(), marked: marker?.Formatting != true));
            k = Math.Max(end, k + 1);
        }
        marker?.End(to, to == text.Length, line);
        return line.ToString();
    }

    /// <summary>The fields the notation names ({page}, {pages}, {date}) by their instructions; null for others.</summary>
    public static string? FieldName(string instruction) => instruction.Trim().Split(' ', 2)[0].ToUpperInvariant() switch
    {
        "PAGE" => "page",
        "NUMPAGES" => "pages",
        "DATE" => "date",
        _ => null,
    };

    /// <summary>
    /// A character as the notation writes it: a field it names by its name ({page}), another object by its token, a note by its mark, a
    /// line or page break marked, the notation's marks escaped.
    /// </summary>
    static string Character(WordDocument? word, int paragraph, string text, int offset, Objects objects)
    {
        var c = text[offset];
        switch (c)
        {
            case '￼':
                if (word?.FieldInstruction(paragraph, offset) is { } instruction)
                {
                    if (FieldName(instruction) is { } name)
                        return "{" + name + "}";
                    if ((Referring(instruction) ?? Numbering(word, paragraph, text, offset, instruction)) is { } written)
                        return written;
                }
                if (objects.Formula(paragraph, offset) is var (latex, _))
                    return "$" + latex + "$";
                return objects.Token(paragraph, offset);
            case '\v':
                return "<br>";
            // A page or column break among the text as WordprocessingML (alone in its paragraph, it reads as \pagebreak or \columnbreak).
            case '\f':
                return "<w:r><w:br w:type=\"page\"/></w:r>";
            case '\u000E':
                return "<w:r><w:br w:type=\"column\"/></w:r>";
            case '*' or '_' or '{' or '}' or '[' or ']' or '|' or '`' or '\\' or '<' or '$' or '~' or '^' or '¶':
                return "\\" + c;
            case '=' when offset + 1 < text.Length && text[offset + 1] == '=':
                return "\\=";
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
