using System.Globalization;
using System.Security;
using System.Text;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// A table of authorities as the notation writes it and Word makes one (References > Mark Citation, Insert Table of Authorities):
/// "{authority Cases: Brown v. Board of Education, 347 U.S. 483 (1954) | Brown}" among a paragraph's text marks the citation (Word's TA
/// field, showing nothing: its long citation, its short one and its category), "{authority Brown}" a later mention by the short citation,
/// read back at the end of the paragraph's line; "{authorities}" on a line of its own makes the table once the write is in, as Word's
/// Insert Table of Authorities does for the category All: a TOA field for each category that has authorities (TOA \h \c "1" \p), its
/// heading the category's name in the style toa heading, an entry for each authority in the style table of authorities, its long citation,
/// a tab with dots and the pages it is cited on as they show their numbers, "passim" where they are five or more, the last field ending in
/// an empty paragraph after the entries; "{authorities Cases}" one category's (measured in Word 365 by COM with TablesOfAuthorities.Add,
/// Category 0 and 1: the fields back to back, one ending where the next begins, entries in alphabetical order whatever their case, pages
/// "1, 2, 3, 4" and from five on "passim", the tab a twentieth of a point inside the text's right edge; styles defined as Word defines
/// them where the document has none). Read back as one line, with the ids of its first and last paragraph.
/// </summary>
static class Authorities
{
    const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    /// <summary>Word's categories, 1 to 7, by their English names and the names Word gives them in German (8 to 16 are numbered).</summary>
    static readonly string[] English = ["Cases", "Statutes", "Other Authorities", "Rules", "Treatises", "Regulations", "Constitutional Provisions"];

    static readonly string[] German = ["Fälle", "Statuten", "Weitere Grundlagen", "Regeln", "Abhandlungen", "Regulierungen", "Verfassungsprovisionen"];

    /// <summary>A category's number by its name (English or German, any case, singular too) or its number 1 to 16; null for none.</summary>
    public static int? Category(string name)
    {
        var given = name.Trim();
        if (int.TryParse(given, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            return number is >= 1 and <= 16 ? number : null;
        for (var k = 0; k < English.Length; k++)
        {
            if (Same(given, English[k]) || Same(given, German[k]))
                return k + 1;
        }
        return null;
        static bool Same(string given, string name) => given.Equals(name, StringComparison.OrdinalIgnoreCase)
            || given.Length > 3 && name.StartsWith(given, StringComparison.OrdinalIgnoreCase) && name.Length - given.Length <= 2;
    }

    /// <summary>A category's name as the heading and the notation say it: English, or German in a German text (Word's names there).</summary>
    public static string Name(int category, int lcid = 1033) =>
        category is >= 1 and <= 7 ? ((lcid & 0x3FF) == 0x07 ? German : English)[category - 1] : category.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// A mark's text in the notation as it reads in braces after "authority": "Cases: long | short" (the short citation left out where it is
    /// the long one), "short" for a later mention; null where it says none (see Notation.Authority).
    /// </summary>
    public static (string Short, string? Long, int? Category)? Parse(string inner)
    {
        var text = inner.Trim();
        var colon = text.IndexOf(':');
        if (colon > 0 && Category(text[..colon]) is { } category)
        {
            var rest = text[(colon + 1)..].Trim();
            var bar = rest.LastIndexOf('|');
            var longText = (bar >= 0 ? rest[..bar] : rest).Trim();
            var shortText = bar >= 0 ? rest[(bar + 1)..].Trim() : "";
            if (longText.Length == 0)
                return null;
            return (shortText.Length > 0 ? shortText : longText, longText, category);
        }
        return text.Length > 0 ? (text, null, null) : null;
    }

    /// <summary>A mark as the notation writes it at the end of its paragraph's line: " {authority Cases: long | short}", " {authority short}".</summary>
    public static string Text(AuthorityMark mark) =>
        " {authority " + (mark.Long is { } longText
            ? Name(mark.Category ?? 1) + ": " + longText + (mark.Short != longText ? " | " + mark.Short : "")
            : mark.Short) + "}";

    /// <summary>A TA field's instruction for a mark as Word's Mark Citation writes it, a quote in a citation escaped (\").</summary>
    public static string Instruction(string shortText, string? longText, int? category)
    {
        static string Quoted(string text) => "\"" + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
        return longText != null
            ? $" TA \\l {Quoted(longText)} \\s {Quoted(shortText)} \\c {(category ?? 1).ToString(CultureInfo.InvariantCulture)} "
            : $" TA \\s {Quoted(shortText)} ";
    }

    /// <summary>
    /// The categories a line asks for ("{authorities Cases, Statutes}"), in order; null for all that have authorities ("{authorities}"); what
    /// names no category is told back.
    /// </summary>
    public static List<int>? Asked(string? text, List<string> problems)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
            return null;
        var asked = new List<int>();
        foreach (var part in text.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Category(part) is { } category)
            {
                if (!asked.Contains(category))
                    asked.Add(category);
            }
            else
                problems.Add($"{part} is none of Word's categories of authorities (Cases, Statutes, Other Authorities, Rules, Treatises, Regulations, Constitutional Provisions, or 8 to 16).");
        }
        return asked.Count > 0 ? asked : null;
    }

    /// <summary>
    /// The table of authorities made in place of a paragraph (the one its line put in, see Writing), of the marks as they stand then: one
    /// TOA field for each category asked for (each that has authorities, where none is named); returns its paragraphs' ids.
    /// </summary>
    public static IReadOnlyList<int> Make(WordDocument word, int placeholder, IReadOnlyList<int>? asked)
    {
        var marked = Entries(word, placeholder);
        var styles = (asked ?? [.. marked.Keys]).Any(marked.ContainsKey) ? Styles(word) : ("", "");
        while (word.LayingOut && word.LayOut(1000))
        {
        }
        var lcid = word.LanguageOf(placeholder);
        var right = (int)Math.Round(word.TextWidth(placeholder) * 20) - 10;
        string Number(int page) => word.PageNumberText(page) ?? (page + 1).ToString(CultureInfo.InvariantCulture);
        var entries = Entries(word, placeholder);
        var ids = word.InsertBlocks(placeholder, Markup(styles, entries, asked, Number, right, lcid), before: true);
        word.Remove(placeholder);
        // The pages again once the table stands, which may push the citations on: the table anew where one moved.
        while (word.LayingOut && word.LayOut(1000))
        {
        }
        var now = Entries(word, null);
        if (Shown(now, Number) != Shown(entries, Number) && word.After(ids[^1]) is { } following)
        {
            // The fields over its paragraphs taken away in one, up to the paragraph after it (see Contents.Make).
            var again = word.InsertBlocks(ids[0], Markup(styles, now, asked, Number, right, lcid), before: true);
            word.RemoveBetween(again[^1], following);
            ids = again;
        }
        return ids;
    }

    /// <summary>The authorities as their pages show them, to tell whether the pages moved.</summary>
    static string Shown(SortedDictionary<int, List<(string Long, SortedSet<int> Pages)>> entries, Func<int, string> number) =>
        string.Join("\n", entries.SelectMany(c => c.Value.Select(e => c.Key + " " + e.Long + " " + string.Join(",", e.Pages.Select(number)))));

    /// <summary>
    /// The authorities marked, by category, each with the pages (0-based) its own mark and its later mentions (by its short citation) stand
    /// on, in the order Word lists them (alphabetical, whatever the case); the paragraph left out.
    /// </summary>
    static SortedDictionary<int, List<(string Long, SortedSet<int> Pages)>> Entries(WordDocument word, int? placeholder)
    {
        var comparer = StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true);
        var marks = new List<(AuthorityMark Mark, int Page)>();
        for (int? paragraph = word.Beginning; paragraph is { } p; paragraph = word.Next(p))
        {
            if (p == placeholder || word.AuthoritiesOf(p) is not { Count: > 0 } found)
                continue;
            var page = word.PageOf(p, 0) ?? 0;
            marks.AddRange(found.Select(m => (m, page)));
        }
        var entries = new SortedDictionary<int, List<(string Long, SortedSet<int> Pages)>>();
        foreach (var (mark, _) in marks.Where(m => m.Mark.Long != null))
        {
            var list = entries.TryGetValue(mark.Category ?? 1, out var known) ? known : entries[mark.Category ?? 1] = [];
            if (list.Any(e => comparer.Equals(e.Long, mark.Long)))
                continue;
            var pages = new SortedSet<int>(marks.Where(m => comparer.Equals(m.Mark.Short, mark.Short)).Select(m => m.Page));
            list.Add((mark.Long!, pages));
        }
        foreach (var list in entries.Values)
            list.Sort((a, b) => comparer.Compare(a.Long, b.Long));
        return entries;
    }

    /// <summary>The categories that have authorities marked, in order.</summary>
    public static IReadOnlyList<int> Marked(WordDocument word) => [.. Entries(word, null).Keys];

    /// <summary>
    /// The table's paragraphs as Word's Insert Table of Authorities writes them: for each category a heading (its paragraph mark in the body's
    /// font, not bold), which the category's field begins in (the one before ending there), and its entries; the last field ending in an
    /// empty paragraph after them. A category without authorities is a paragraph of its own without a style, its field beginning and ending
    /// in it round Word's notice in bold (measured: "Es wurden keine Verzeichnis-Einträge gefunden." in German Word).
    /// </summary>
    static string Markup((string Heading, string Entry) styles, SortedDictionary<int, List<(string Long, SortedSet<int> Pages)>> entries,
        IReadOnlyList<int>? asked, Func<int, string> number, int right, int lcid)
    {
        static string Escaped(string text) => SecurityElement.Escape(text) ?? "";
        const string end = "<w:r><w:fldChar w:fldCharType=\"end\"/></w:r>";
        var tabs = $"<w:tabs><w:tab w:val=\"right\" w:leader=\"dot\" w:pos=\"{right.ToString(CultureInfo.InvariantCulture)}\"/></w:tabs>";
        var categories = asked ?? [.. entries.Keys];
        if (categories.Count == 0)
            categories = [1];
        var text = new StringBuilder();
        // Whether a field is still open, its end to stand where the next begins.
        var open = false;
        foreach (var category in categories)
        {
            var begin = $"<w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText xml:space=\"preserve\"> TOA \\h \\c \"{category.ToString(CultureInfo.InvariantCulture)}\" \\p </w:instrText></w:r>"
                        + "<w:r><w:fldChar w:fldCharType=\"separate\"/></w:r>";
            if (entries.GetValueOrDefault(category) is not { Count: > 0 } list)
            {
                text.Append("<w:p>").Append(open ? end : "").Append(begin).Append("<w:r><w:rPr><w:b/><w:bCs/><w:noProof/></w:rPr><w:t xml:space=\"preserve\">")
                    .Append((lcid & 0x3FF) == 0x07 ? "Es wurden keine Verzeichnis-Einträge gefunden." : "No table of authorities entries found.")
                    .Append("</w:t></w:r>").Append(end).Append("</w:p>");
                open = false;
                continue;
            }
            text.Append($"<w:p><w:pPr><w:pStyle w:val=\"{Escaped(styles.Heading)}\"/>{tabs}<w:rPr><w:rFonts w:asciiTheme=\"minorHAnsi\" w:eastAsiaTheme=\"minorEastAsia\" w:hAnsiTheme=\"minorHAnsi\" w:cstheme=\"minorBidi\"/>")
                .Append("<w:b w:val=\"0\"/><w:bCs w:val=\"0\"/><w:noProof/></w:rPr></w:pPr>")
                .Append(open ? end : "").Append(begin)
                .Append($"<w:r><w:rPr><w:noProof/></w:rPr><w:t xml:space=\"preserve\">{Escaped(Name(category, lcid))}</w:t></w:r></w:p>");
            foreach (var (longText, pages) in list)
            {
                var shown = pages.Count >= 5 ? "passim" : string.Join(", ", pages.Select(number));
                text.Append($"<w:p><w:pPr><w:pStyle w:val=\"{Escaped(styles.Entry)}\"/>{tabs}<w:rPr><w:noProof/></w:rPr></w:pPr>")
                    .Append($"<w:r><w:rPr><w:noProof/></w:rPr><w:t xml:space=\"preserve\">{Escaped(longText)}</w:t></w:r>")
                    .Append($"<w:r><w:rPr><w:noProof/></w:rPr><w:tab/><w:t xml:space=\"preserve\">{Escaped(shown)}</w:t></w:r></w:p>");
            }
            open = true;
        }
        return open ? text.Append("<w:p>").Append(end).Append("</w:p>").ToString() : text.ToString();
    }

    /// <summary>
    /// The ids of the styles of the categories' headings and of the entries: the document's (by Word's names), else defined as Word defines
    /// them (measured: toa heading based on Normal, 6 pt before, bold, in the headings' font; table of authorities based on Normal, no space
    /// after, hanging by a body text's size).
    /// </summary>
    static (string Heading, string Entry) Styles(WordDocument word)
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
        var heading = Id("toa heading", "TOAHeading", "<w:style w:type=\"paragraph\" w:styleId=\"TOAHeading\"><w:name w:val=\"toa heading\"/>"
            + $"<w:basedOn w:val=\"{normal}\"/><w:next w:val=\"{normal}\"/><w:uiPriority w:val=\"99\"/><w:semiHidden/><w:unhideWhenUsed/>"
            + "<w:pPr><w:spacing w:before=\"120\"/></w:pPr><w:rPr><w:rFonts w:asciiTheme=\"majorHAnsi\" w:eastAsiaTheme=\"majorEastAsia\" w:hAnsiTheme=\"majorHAnsi\" w:cstheme=\"majorBidi\"/>"
            + "<w:b/><w:bCs/></w:rPr></w:style>");
        var entry = Id("table of authorities", "TableofAuthorities", "<w:style w:type=\"paragraph\" w:styleId=\"TableofAuthorities\"><w:name w:val=\"table of authorities\"/>"
            + $"<w:basedOn w:val=\"{normal}\"/><w:next w:val=\"{normal}\"/><w:uiPriority w:val=\"99\"/><w:semiHidden/><w:unhideWhenUsed/>"
            + "<w:pPr><w:spacing w:after=\"0\"/><w:ind w:left=\"240\" w:hanging=\"240\"/></w:pPr></w:style>");
        if (missing.Length > 0)
            word.DefineStyles($"<w:styles xmlns:w=\"{W}\">{missing}</w:styles>");
        return (heading, entry);
    }

    static string? StyleName(WordDocument word, int paragraph) => word.ParagraphStyle(paragraph) is { } id ? word.StyleName(id) : null;

    static bool Part(string? name) => name != null && (name.Equals("toa heading", StringComparison.OrdinalIgnoreCase) || name.Equals("table of authorities", StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether a paragraph is one of a table of authorities: in its styles, or beginning a TOA field (a category without authorities).</summary>
    static bool Member(WordDocument word, int paragraph) =>
        Part(StyleName(word, paragraph)) || word.FieldsBegunIn(paragraph).Any(i => WordDocument.FieldWords(i) is [var type, ..] && type.Equals("TOA", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Where a table of authorities begins at a paragraph of the body (its first paragraph in the style toa heading or table of authorities):
    /// its last paragraph (the empty one after the entries that holds the end of Word's field, where there is one) and its line in the
    /// notation ("{authorities}" where it lists every category that has authorities, else the categories its fields list); else null.
    /// </summary>
    public static (int Last, string Line)? At(WordDocument word, int paragraph)
    {
        if (!Member(word, paragraph) || word.Before(paragraph) is { } before && Member(word, before))
            return null;
        var last = paragraph;
        for (var next = word.After(paragraph); next is { } at && Member(word, at); next = word.After(at))
            last = at;
        if (word.After(last) is { } after && word.Text(after).Length == 0 && StyleName(word, after) is null or "Normal")
            last = after;
        var listed = new List<int>();
        for (int? p = paragraph; p is { } at; p = at == last ? null : word.After(at))
        {
            foreach (var instruction in word.FieldsBegunIn(at))
            {
                var words = WordDocument.FieldWords(instruction);
                if (words.Count == 0 || !words[0].Equals("TOA", StringComparison.OrdinalIgnoreCase))
                    continue;
                var c = words.FindIndex(w => w.Equals("\\c", StringComparison.OrdinalIgnoreCase));
                if (c > 0 && c + 1 < words.Count && int.TryParse(words[c + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var category) && !listed.Contains(category))
                    listed.Add(category);
            }
        }
        var marked = Marked(word);
        var all = listed.Count == 0 || marked.Count > 0 && listed.Order().SequenceEqual(marked);
        return (last, all ? "{authorities}" : "{authorities " + string.Join(", ", listed.Select(c => Name(c))) + "}");
    }

    /// <summary>The table of authorities a paragraph of the body stands in (see At): its first and last paragraph, or null.</summary>
    public static (int First, int Last)? Around(WordDocument word, int paragraph)
    {
        var first = paragraph;
        if (!Member(word, first) && word.Text(first).Length == 0 && word.Before(first) is { } before && Member(word, before))
            first = before;
        if (!Member(word, first))
            return null;
        while (word.Before(first) is { } previous && Member(word, previous))
            first = previous;
        return At(word, first) is var (last, _) ? (first, last) : null;
    }
}
