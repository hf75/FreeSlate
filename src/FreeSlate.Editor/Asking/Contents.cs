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
/// (see At).
/// </summary>
static partial class Contents
{
    /// <summary>The levels of headings a table of contents lists.</summary>
    const int Deepest = 3;

    const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    /// <summary>
    /// The table of contents made in place of a paragraph (the one its line put in, see Writing), the document's headings as they stand then,
    /// a change of the document's; returns the ids of its paragraphs.
    /// </summary>
    public static IReadOnlyList<int> Make(WordDocument word, int placeholder, string? title)
    {
        var styles = Styles(word);
        var headings = Headings(word, placeholder);
        // A bookmark round each heading's text where it has none of a table of contents (Word's names, _Toc and a number).
        var names = new List<string>();
        foreach (var (paragraph, _, _) in headings)
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
        var ids = word.InsertBlocks(placeholder, Markup(title, styles, headings, names, pages, right), before: true);
        word.Remove(placeholder);
        // The pages again once the table stands, which may push the headings on: the entries anew where one moved.
        var now = Pages(word, names);
        if (!now.SequenceEqual(pages))
        {
            var again = word.InsertBlocks(ids[0], Markup(title, styles, headings, names, now, right), before: true);
            foreach (var id in ids)
                word.Remove(id);
            ids = again;
        }
        return ids;
    }

    /// <summary>
    /// Where a table of contents begins at a paragraph of the body (its title in the style TOC Heading, or its first entry in a style toc 1 to
    /// toc 9): its last paragraph (the entries', or an empty one after them that holds the end of Word's field) and its title; else null.
    /// </summary>
    public static (int Last, string? Title)? At(WordDocument word, int paragraph)
    {
        string? Name(int p) => word.ParagraphStyle(p) is { } id ? word.StyleName(id) : null;
        static bool Entry(string? name) => name != null && EntryStyle().IsMatch(name);
        string? title = null;
        int? first = paragraph;
        if (string.Equals(Name(paragraph), "TOC Heading", StringComparison.OrdinalIgnoreCase))
        {
            title = Clean(word.Text(paragraph));
            first = word.After(paragraph);
        }
        if (first is not { } entry || !Entry(Name(entry)))
            return null;
        var last = entry;
        for (var next = word.After(entry); next is { } at && Entry(Name(at)); next = word.After(at))
            last = at;
        // Word's own table ends its field in an empty paragraph after the entries, which belongs to it.
        if (word.After(last) is { } after && word.Text(after).Length == 0
            && !(Name(after) is { } named && (named.StartsWith("heading", StringComparison.OrdinalIgnoreCase) || named.Equals("Title", StringComparison.OrdinalIgnoreCase))))
            last = after;
        return (last, title is { Length: > 0 } ? title : null);
    }

    /// <summary>The table of contents a paragraph of the body stands in (see At): its first and last paragraph, or null.</summary>
    public static (int First, int Last)? Around(WordDocument word, int paragraph)
    {
        string? Name(int p) => word.ParagraphStyle(p) is { } id ? word.StyleName(id) : null;
        static bool Part(string? name) => name != null && (EntryStyle().IsMatch(name) || name.Equals("TOC Heading", StringComparison.OrdinalIgnoreCase));
        var first = paragraph;
        // The empty paragraph that ends Word's field belongs to the table before it.
        if (!Part(Name(first)) && word.Text(first).Length == 0 && word.Before(first) is { } before && Part(Name(before)))
            first = before;
        if (!Part(Name(first)))
            return null;
        while (word.Before(first) is { } previous && Part(Name(previous)))
            first = previous;
        return At(word, first) is var (last, _) ? (first, last) : null;
    }

    [GeneratedRegex(@"^toc [1-9]$", RegexOptions.IgnoreCase)]
    private static partial Regex EntryStyle();

    /// <summary>The ids of the styles of the title and of the entries of each level: the document's (by Word's names), else defined as Word defines them.</summary>
    static (string Title, string[] Levels) Styles(WordDocument word)
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

    /// <summary>The table's paragraphs: its title; an entry for each heading, the field beginning in the first and ending in the last.</summary>
    static string Markup(string? title, (string Title, string[] Levels) styles, List<(int Paragraph, int Level, string Text)> headings, List<string> names,
        List<int> pages, int right)
    {
        static string Escaped(string text) => SecurityElement.Escape(text) ?? "";
        const string begin = "<w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText xml:space=\"preserve\"> TOC \\o \"1-3\" \\h \\z \\u </w:instrText></w:r>"
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
