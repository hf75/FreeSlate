using System.Globalization;
using System.Security;
using System.Text;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// An index as the notation writes it and Word makes one: "{index term}" among a paragraph's text marks it for the term (Word's Mark
/// Entry, an XE field showing nothing; "main: sub" a subentry), read back at the end of the paragraph's line; "{index}" on a line of its
/// own makes the index once the write is in: Word's INDEX field with a heading for each first letter (\h "A", the style index heading),
/// an entry for each term in the style index 1 and its subentries in index 2, with the pages its marks stand on ("Apples, 3, 7"), the
/// field ending in an empty paragraph after them, as Word's Insert Index writes it (styles defined as Word defines them where the document
/// has none). Read back as one line, "{index}", with the ids of its first and last paragraph.
/// </summary>
static class Index
{
    const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    /// <summary>The index made in place of a paragraph (the one its line put in, see Writing), of the marks as they stand then; returns its paragraphs' ids.</summary>
    public static IReadOnlyList<int> Make(WordDocument word, int placeholder)
    {
        var styles = Styles(word);
        while (word.LayingOut && word.LayOut(1000))
        {
        }
        var entries = Entries(word, placeholder);
        string Number(int page) => word.PageNumberText(page) ?? (page + 1).ToString(CultureInfo.InvariantCulture);
        var ids = word.InsertBlocks(placeholder, Markup(styles, entries, Number), before: true);
        word.Remove(placeholder);
        // The pages again once the index stands, which may push the marked paragraphs on: the index anew where one moved.
        while (word.LayingOut && word.LayOut(1000))
        {
        }
        var now = Entries(word, null);
        if (!Same(now, entries) && word.After(ids[^1]) is { } following)
        {
            // The index made before has its field over its paragraphs: taken away in one, up to the paragraph after it (see Contents.Make).
            var again = word.InsertBlocks(ids[0], Markup(styles, now, Number), before: true);
            word.RemoveBetween(again[^1], following);
            ids = again;
        }
        return ids;
    }

    /// <summary>The terms marked and their subterms, each with the pages their marks stand on (0-based), in the index's order.</summary>
    static SortedDictionary<string, (SortedSet<int> Pages, SortedDictionary<string, SortedSet<int>> Subs)> Entries(WordDocument word, int? placeholder)
    {
        var comparer = StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true);
        var entries = new SortedDictionary<string, (SortedSet<int>, SortedDictionary<string, SortedSet<int>>)>(comparer);
        for (int? paragraph = word.Beginning; paragraph is { } p; paragraph = word.Next(p))
        {
            if (p == placeholder || word.IndexEntriesOf(p) is not { Count: > 0 } marks)
                continue;
            var page = word.PageOf(p, 0) ?? 0;
            foreach (var mark in marks)
            {
                var parts = mark.Split(':', 2);
                var main = parts[0].Trim();
                if (main.Length == 0)
                    continue;
                if (!entries.TryGetValue(main, out var entry))
                    entries[main] = entry = ([], new SortedDictionary<string, SortedSet<int>>(comparer));
                if (parts.Length > 1 && parts[1].Trim() is { Length: > 0 } sub)
                {
                    if (!entry.Item2.TryGetValue(sub, out var pages))
                        entry.Item2[sub] = pages = [];
                    pages.Add(page);
                }
                else
                    entry.Item1.Add(page);
            }
        }
        return entries;
    }

    static bool Same(SortedDictionary<string, (SortedSet<int> Pages, SortedDictionary<string, SortedSet<int>> Subs)> a,
        SortedDictionary<string, (SortedSet<int> Pages, SortedDictionary<string, SortedSet<int>> Subs)> b) =>
        a.Count == b.Count && a.All(e => b.TryGetValue(e.Key, out var o) && o.Pages.SetEquals(e.Value.Pages) && o.Subs.Count == e.Value.Subs.Count
            && e.Value.Subs.All(s => o.Subs.TryGetValue(s.Key, out var os) && os.SetEquals(s.Value)));

    /// <summary>
    /// Where an index begins at a paragraph of the body (its first paragraph in the style index heading or index 1 to 9): its last paragraph
    /// (the empty one after the entries that holds the end of Word's field, where there is one); else null.
    /// </summary>
    public static int? At(WordDocument word, int paragraph)
    {
        if (!Part(Name(word, paragraph)) || word.Before(paragraph) is { } before && Part(Name(word, before)))
            return null;
        var last = paragraph;
        for (var next = word.After(paragraph); next is { } at && Part(Name(word, at)); next = word.After(at))
            last = at;
        if (word.After(last) is { } after && word.Text(after).Length == 0 && Name(word, after) is null or "Normal")
            last = after;
        return last;
    }

    /// <summary>The index a paragraph of the body stands in (see At): its first and last paragraph, or null.</summary>
    public static (int First, int Last)? Around(WordDocument word, int paragraph)
    {
        var first = paragraph;
        if (!Part(Name(word, first)) && word.Text(first).Length == 0 && word.Before(first) is { } before && Part(Name(word, before)))
            first = before;
        if (!Part(Name(word, first)))
            return null;
        while (word.Before(first) is { } previous && Part(Name(word, previous)))
            first = previous;
        return At(word, first) is { } last ? (first, last) : null;
    }

    static string? Name(WordDocument word, int paragraph) => word.ParagraphStyle(paragraph) is { } id ? word.StyleName(id) : null;

    static bool Part(string? name) => name != null && (name.Equals("index heading", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("index ", StringComparison.OrdinalIgnoreCase) && name.Length == 7 && char.IsDigit(name[^1]));

    /// <summary>The ids of the styles of the letters' headings and of the two levels of entries: the document's, else defined as Word defines them.</summary>
    static (string Heading, string First, string Second) Styles(WordDocument word)
    {
        var missing = new StringBuilder();
        var normal = SecurityElement.Escape(word.StyleIdNamed("Normal") ?? "Normal");
        string Id(string name, string id, string definition)
        {
            if (word.StyleIdNamed(name) is { } known)
                return known;
            missing.Append(definition);
            return id;
        }
        static string Level(int level) => $"<w:style w:type=\"paragraph\" w:styleId=\"Index{level}\"><w:name w:val=\"index {level}\"/>";
        var first = Id("index 1", "Index1", Level(1) + $"<w:basedOn w:val=\"{normal}\"/><w:next w:val=\"{normal}\"/><w:autoRedefine/><w:uiPriority w:val=\"99\"/>"
            + "<w:semiHidden/><w:unhideWhenUsed/><w:pPr><w:spacing w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"/><w:ind w:left=\"240\" w:hanging=\"240\"/></w:pPr></w:style>");
        var second = Id("index 2", "Index2", Level(2) + $"<w:basedOn w:val=\"{normal}\"/><w:next w:val=\"{normal}\"/><w:autoRedefine/><w:uiPriority w:val=\"99\"/>"
            + "<w:semiHidden/><w:unhideWhenUsed/><w:pPr><w:spacing w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"/><w:ind w:left=\"480\" w:hanging=\"240\"/></w:pPr></w:style>");
        var heading = Id("index heading", "IndexHeading", "<w:style w:type=\"paragraph\" w:styleId=\"IndexHeading\"><w:name w:val=\"index heading\"/>"
            + $"<w:basedOn w:val=\"{normal}\"/><w:next w:val=\"{SecurityElement.Escape(first)}\"/><w:uiPriority w:val=\"99\"/><w:semiHidden/><w:unhideWhenUsed/>"
            + "<w:rPr><w:rFonts w:asciiTheme=\"majorHAnsi\" w:eastAsiaTheme=\"majorEastAsia\" w:hAnsiTheme=\"majorHAnsi\" w:cstheme=\"majorBidi\"/><w:b/><w:bCs/></w:rPr></w:style>");
        if (missing.Length > 0)
            word.DefineStyles($"<w:styles xmlns:w=\"{W}\">{missing}</w:styles>");
        return (heading, first, second);
    }

    /// <summary>
    /// The index's paragraphs: a heading for each first letter, the entries with their pages' numbers as the pages show them (roman where
    /// they are), the field beginning in the first and ending in an empty paragraph after them.
    /// </summary>
    static string Markup((string Heading, string First, string Second) styles,
        SortedDictionary<string, (SortedSet<int> Pages, SortedDictionary<string, SortedSet<int>> Subs)> entries, Func<int, string> number)
    {
        static string Escaped(string text) => SecurityElement.Escape(text) ?? "";
        const string begin = "<w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText xml:space=\"preserve\"> INDEX \\h \"A\" </w:instrText></w:r>"
            + "<w:r><w:fldChar w:fldCharType=\"separate\"/></w:r>";
        string Pages(SortedSet<int> pages) => pages.Count > 0 ? ", " + string.Join(", ", pages.Select(number)) : "";
        string Paragraph(string style, string text, bool keep = false) =>
            $"<w:p><w:pPr><w:pStyle w:val=\"{Escaped(style)}\"/>{(keep ? "<w:keepNext/>" : "")}</w:pPr><w:r><w:rPr><w:noProof/></w:rPr><w:t xml:space=\"preserve\">{Escaped(text)}</w:t></w:r></w:p>";
        var text = new StringBuilder();
        string? letter = null;
        foreach (var (term, (pages, subs)) in entries)
        {
            var initial = char.ToUpper(term[0], CultureInfo.CurrentCulture).ToString();
            if (initial != letter)
            {
                text.Append(Paragraph(styles.Heading, initial, keep: true));
                letter = initial;
            }
            text.Append(Paragraph(styles.First, term + Pages(pages)));
            foreach (var (sub, subPages) in subs)
                text.Append(Paragraph(styles.Second, sub + Pages(subPages)));
        }
        if (text.Length == 0)
            return $"<w:p><w:pPr><w:pStyle w:val=\"{Escaped(styles.First)}\"/></w:pPr>{begin}<w:r><w:fldChar w:fldCharType=\"end\"/></w:r></w:p>";
        // The field begins in the first paragraph, after its properties.
        var markup = text.ToString();
        var first = markup.IndexOf("</w:pPr>", StringComparison.Ordinal) + "</w:pPr>".Length;
        return markup[..first] + begin + markup[first..] + "<w:p><w:r><w:fldChar w:fldCharType=\"end\"/></w:r></w:p>";
    }
}
