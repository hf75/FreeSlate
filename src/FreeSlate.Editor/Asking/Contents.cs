using System.Globalization;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// A table of contents as the notation writes it ("{contents Title}" on a line of its own) and Word makes one: a TOC field over the headings
/// of levels 1 to 3 (TOC \o "1-3" \h \z \u), an entry for each heading in Word's styles toc 1 to toc 3 (defined as Word defines them where the
/// document has none) with a link to a bookmark round the heading's text (_Toc…, put there where it has none) and its page as a PAGEREF field;
/// its title before it in the style TOC Heading. The engine computes the pages as Word does when it prints; the results kept are the pages as
/// laid out once the table stands, which Word shows. Read back as one line, "{contents Title}", with the ids of its first and last paragraph
/// (see At). A table of figures ("{figures Figure}", Word's Insert Table of Figures) is one too, over the captions numbered by a label's
/// sequence (TOC \h \z \c "Figure"), each entry the caption's text with its number as the page shows it, in Word's style table of figures.
/// </summary>
static partial class Contents
{
    /// <summary>The levels of headings a table of contents lists.</summary>
    const int Deepest = 3;

    const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    /// <summary>The name of Word's style of a table of figures' entries.</summary>
    const string FiguresStyle = "table of figures";

    /// <summary>
    /// The table of contents made in place of a paragraph (the one its line put in, see Writing), the document's headings as they stand then,
    /// a change of the document's; with a label the table of figures of the captions of that label. Returns the ids of its paragraphs.
    /// </summary>
    public static IReadOnlyList<int> Make(WordDocument word, int placeholder, string? title, string? label = null)
    {
        var styles = Styles(word, figures: label != null);
        var entries = label != null ? Captions(word, placeholder, label) : Headings(word, placeholder);
        // A bookmark round each heading's or caption's text where it has none of a table of contents (Word's names, _Toc and a number).
        var names = new List<string>();
        foreach (var (paragraph, _, _) in entries)
        {
            if (word.BookmarksOf(paragraph).FirstOrDefault(n => n.StartsWith("_Toc", StringComparison.Ordinal)) is { } known)
            {
                names.Add(known);
                continue;
            }
            var number = Random.Shared.Next(100_000_000, 999_999_999).ToString(CultureInfo.InvariantCulture);
            word.InsertMarkup(paragraph, word.Text(paragraph).Length, $"<w:bookmarkEnd w:id=\"{number}\"/>");
            word.InsertMarkup(paragraph, 0, $"<w:bookmarkStart w:id=\"{number}\" w:name=\"_Toc{number}\"/>");
            names.Add("_Toc" + number);
        }
        var right = (int)Math.Round(word.TextWidth(placeholder) * 20);
        var pages = Pages(word, names);
        // A caption's entry with its number as the page shows it, known once the pages are laid out.
        if (label != null)
            entries = [.. entries.Select(e => (e.Paragraph, e.Level, Shown(word, e.Paragraph)))];
        var instruction = label != null ? $" TOC \\h \\z \\c \"{label}\" " : " TOC \\o \"1-3\" \\h \\z \\u ";
        var ids = word.InsertBlocks(placeholder, Markup(title, styles, entries, names, pages, right, instruction), before: true);
        word.Remove(placeholder);
        // The pages again once the table stands, which may push the headings on: the entries anew where one moved.
        var now = Pages(word, names);
        if (!now.SequenceEqual(pages) && word.After(ids[^1]) is { } following)
        {
            // The table made before has its field over its paragraphs: taken away in one, up to the paragraph after it (the place was
            // not the last of the body, see Writing.End).
            var again = word.InsertBlocks(ids[0], Markup(title, styles, entries, names, now, right, instruction), before: true);
            word.RemoveBetween(again[^1], following);
            ids = again;
        }
        return ids;
    }

    /// <summary>
    /// Where a table of contents begins at a paragraph of the body (its title in the style TOC Heading, or its first entry in a style toc 1 to
    /// toc 9 or table of figures): its last paragraph (the entries', or an empty one after them that holds the end of Word's field), its title,
    /// and a table of figures' label (its first entry's first word); else null.
    /// </summary>
    public static (int Last, string? Title, string? Label)? At(WordDocument word, int paragraph)
    {
        string? Name(int p) => word.ParagraphStyle(p) is { } id ? word.StyleName(id) : null;
        string? title = null;
        int? first = paragraph;
        if (string.Equals(Name(paragraph), "TOC Heading", StringComparison.OrdinalIgnoreCase))
        {
            title = Clean(word.Text(paragraph));
            first = word.After(paragraph);
        }
        if (first is not { } entry)
            return null;
        var figures = title == null && string.Equals(Name(entry), FiguresStyle, StringComparison.OrdinalIgnoreCase);
        bool Entry(string? name) => name != null && (figures ? name.Equals(FiguresStyle, StringComparison.OrdinalIgnoreCase) : EntryStyle().IsMatch(name));
        if (!Entry(Name(entry)))
            return null;
        var last = entry;
        for (var next = word.After(entry); next is { } at && Entry(Name(at)); next = word.After(at))
            last = at;
        // Word's own table ends its field in an empty paragraph after the entries, which belongs to it.
        if (word.After(last) is { } after && word.Text(after).Length == 0
            && !(Name(after) is { } named && (named.StartsWith("heading", StringComparison.OrdinalIgnoreCase) || named.Equals("Title", StringComparison.OrdinalIgnoreCase))))
            last = after;
        var label = figures ? Clean(word.Text(entry)).Split(' ', 2)[0].TrimEnd('.', ':') : null;
        return (last, title is { Length: > 0 } ? title : null, label is { Length: > 0 } ? label : figures ? "Figure" : null);
    }

    /// <summary>The table of contents or of figures a paragraph of the body stands in (see At): its first and last paragraph, or null.</summary>
    public static (int First, int Last)? Around(WordDocument word, int paragraph)
    {
        string? Name(int p) => word.ParagraphStyle(p) is { } id ? word.StyleName(id) : null;
        static bool Part(string? name) => name != null && (EntryStyle().IsMatch(name) || name.Equals("TOC Heading", StringComparison.OrdinalIgnoreCase)
            || name.Equals(FiguresStyle, StringComparison.OrdinalIgnoreCase));
        var first = paragraph;
        // The empty paragraph that ends Word's field belongs to the table before it.
        if (!Part(Name(first)) && word.Text(first).Length == 0 && word.Before(first) is { } before && Part(Name(before)))
            first = before;
        if (!Part(Name(first)))
            return null;
        while (word.Before(first) is { } previous && Part(Name(previous)))
            first = previous;
        return At(word, first) is var (last, _, _) ? (first, last) : null;
    }

    [GeneratedRegex(@"^toc [1-9]$", RegexOptions.IgnoreCase)]
    private static partial Regex EntryStyle();

    /// <summary>
    /// The ids of the styles of the title and of the entries of each level: the document's (by Word's names), else defined as Word defines
    /// them; a table of figures' one level in the style table of figures.
    /// </summary>
    static (string Title, string[] Levels) Styles(WordDocument word, bool figures = false)
    {
        var missing = new StringBuilder();
        var normal = word.StyleIdNamed("Normal") ?? "Normal";
        string Id(string name, string id, string definition)
        {
            if (word.StyleIdNamed(name) is { } known)
                return known;
            missing.Append(definition);
            return id;
        }
        if (figures)
        {
            var entry = Id(FiguresStyle, "TableofFigures", $"<w:style w:type=\"paragraph\" w:styleId=\"TableofFigures\"><w:name w:val=\"{FiguresStyle}\"/>"
                + $"<w:basedOn w:val=\"{SecurityElement.Escape(normal)}\"/><w:next w:val=\"{SecurityElement.Escape(normal)}\"/><w:uiPriority w:val=\"99\"/>"
                + "<w:unhideWhenUsed/><w:pPr><w:spacing w:after=\"0\"/></w:pPr></w:style>");
            if (missing.Length > 0)
                word.DefineStyles($"<w:styles xmlns:w=\"{W}\">{missing}</w:styles>");
            return (entry, [entry]);
        }
        var heading = word.StyleIdNamed("heading 1");
        var title = Id("TOC Heading", "TOCHeading", "<w:style w:type=\"paragraph\" w:styleId=\"TOCHeading\"><w:name w:val=\"TOC Heading\"/>"
            + (heading != null ? $"<w:basedOn w:val=\"{SecurityElement.Escape(heading)}\"/>" : "") + $"<w:next w:val=\"{SecurityElement.Escape(normal)}\"/>"
            + "<w:uiPriority w:val=\"39\"/><w:unhideWhenUsed/><w:qFormat/><w:pPr><w:outlineLvl w:val=\"9\"/></w:pPr></w:style>");
        var levels = Enumerable.Range(1, Deepest).Select(level => Id($"toc {level}", $"TOC{level}",
            $"<w:style w:type=\"paragraph\" w:styleId=\"TOC{level}\"><w:name w:val=\"toc {level}\"/><w:basedOn w:val=\"{SecurityElement.Escape(normal)}\"/>"
            + $"<w:next w:val=\"{SecurityElement.Escape(normal)}\"/><w:autoRedefine/><w:uiPriority w:val=\"39\"/><w:unhideWhenUsed/><w:pPr><w:spacing w:after=\"100\"/>"
            + (level > 1 ? $"<w:ind w:left=\"{(level - 1) * 220}\"/>" : "") + "</w:pPr></w:style>")).ToArray();
        if (missing.Length > 0)
            word.DefineStyles($"<w:styles xmlns:w=\"{W}\">{missing}</w:styles>");
        return (title, levels);
    }

    /// <summary>The captions numbered by a label's sequence in order (a SEQ field of the label among their text), the place left out.</summary>
    static List<(int Paragraph, int Level, string Text)> Captions(WordDocument word, int placeholder, string label)
    {
        var found = new List<(int, int, string)>();
        for (int? paragraph = word.Beginning; paragraph is { } p; paragraph = word.Next(p))
        {
            if (p == placeholder)
                continue;
            var text = word.Text(p);
            for (var k = 0; k < text.Length; k++)
            {
                if (text[k] == '￼' && word.FieldInstruction(p, k)?.Split(' ', StringSplitOptions.RemoveEmptyEntries) is ["SEQ", var named, ..]
                    && named.Equals(label, StringComparison.OrdinalIgnoreCase))
                {
                    found.Add((p, 1, ""));
                    break;
                }
            }
        }
        return found;
    }

    /// <summary>A caption's text for its entry, its fields as the page shows them (its number), its other objects and breaks left out.</summary>
    static string Shown(WordDocument word, int paragraph)
    {
        var text = word.Text(paragraph);
        var shown = new StringBuilder();
        for (var k = 0; k < text.Length; k++)
        {
            if (text[k] == '￼')
                shown.Append(word.FieldInstruction(paragraph, k) != null ? word.FieldText(paragraph, k) : "");
            else
                shown.Append(text[k]);
        }
        return Clean(shown.ToString());
    }

    /// <summary>The headings of levels 1 to Deepest in order (by their styles' names, heading 1 …), with their level and their text, the place left out.</summary>
    static List<(int Paragraph, int Level, string Text)> Headings(WordDocument word, int placeholder)
    {
        var styles = Enumerable.Range(1, Deepest).Select(level => word.StyleIdNamed($"heading {level}")).ToArray();
        var found = new List<(int, int, string)>();
        for (int? paragraph = word.Beginning; paragraph is { } p; paragraph = word.Next(p))
        {
            if (p == placeholder || word.ParagraphStyle(p) is not { } style || Array.IndexOf(styles, style) is var k && k < 0)
                continue;
            if (Clean(word.Text(p)) is { Length: > 0 } text)
                found.Add((p, k + 1, text));
        }
        return found;
    }

    /// <summary>The pages the bookmarks stand on as the document is laid out now, counted from 1.</summary>
    static List<int> Pages(WordDocument word, List<string> names)
    {
        while (word.LayingOut && word.LayOut(1000))
        {
        }
        return [.. names.Select(name => (word.PageOf(name) ?? 0) + 1)];
    }

    /// <summary>
    /// The table's paragraphs: its title; an entry for each heading or caption, the field (of the instruction given) beginning in the first and
    /// ending in the last.
    /// </summary>
    static string Markup(string? title, (string Title, string[] Levels) styles, List<(int Paragraph, int Level, string Text)> headings, List<string> names,
        List<int> pages, int right, string instruction)
    {
        static string Escaped(string text) => SecurityElement.Escape(text) ?? "";
        var begin = $"<w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText xml:space=\"preserve\">{Escaped(instruction)}</w:instrText></w:r>"
            + "<w:r><w:fldChar w:fldCharType=\"separate\"/></w:r>";
        const string end = "<w:r><w:fldChar w:fldCharType=\"end\"/></w:r>";
        const string hidden = "<w:rPr><w:noProof/><w:webHidden/></w:rPr>";
        var text = new StringBuilder();
        if (title is { Length: > 0 })
            text.Append($"<w:p><w:pPr><w:pStyle w:val=\"{Escaped(styles.Title)}\"/></w:pPr><w:r><w:t xml:space=\"preserve\">{Escaped(title)}</w:t></w:r></w:p>");
        for (var k = 0; k < headings.Count; k++)
        {
            var (_, level, words) = headings[k];
            text.Append($"<w:p><w:pPr><w:pStyle w:val=\"{Escaped(styles.Levels[level - 1])}\"/><w:tabs><w:tab w:val=\"right\" w:leader=\"dot\" w:pos=\"{right}\"/></w:tabs></w:pPr>");
            if (k == 0)
                text.Append(begin);
            var name = Escaped(names[k]);
            text.Append($"<w:hyperlink w:anchor=\"{name}\" w:history=\"1\"><w:r><w:rPr><w:noProof/></w:rPr><w:t xml:space=\"preserve\">{Escaped(words)}</w:t></w:r>")
                .Append($"<w:r>{hidden}<w:tab/></w:r><w:r>{hidden}<w:fldChar w:fldCharType=\"begin\"/></w:r>")
                .Append($"<w:r>{hidden}<w:instrText xml:space=\"preserve\"> PAGEREF {name} \\h </w:instrText></w:r><w:r>{hidden}<w:fldChar w:fldCharType=\"separate\"/></w:r>")
                .Append($"<w:r>{hidden}<w:t>{pages[k].ToString(CultureInfo.InvariantCulture)}</w:t></w:r><w:r>{hidden}<w:fldChar w:fldCharType=\"end\"/></w:r></w:hyperlink>");
            if (k == headings.Count - 1)
                text.Append(end);
            text.Append("</w:p>");
        }
        if (headings.Count == 0)
            text.Append($"<w:p><w:pPr><w:pStyle w:val=\"{Escaped(styles.Levels[0])}\"/></w:pPr>{begin}{end}</w:p>");
        return text.ToString();
    }

    /// <summary>A heading's text for its entry: its objects and breaks left out.</summary>
    static string Clean(string text) =>
        new string([.. text.Replace('\t', ' ').Replace('\v', ' ').Where(c => c != '￼' && c >= ' ')]).Trim();
}
