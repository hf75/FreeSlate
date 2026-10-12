using System.Globalization;
using System.Text;

namespace FreeSlate.Docx.Layout;

/// <summary>
/// The fields Word computes when it prints (catalog: field.evaluation; measured with docx/local/fields, a document whose
/// fields saved wrong results): page numbers and references to pages (PAGE, NUMPAGES, SECTIONPAGES, PAGEREF),
/// sequences (SEQ), the text of the nearest paragraph of a style (STYLEREF), the section number (SECTION), dates and
/// times (DATE, TIME, PRINTDATE, SAVEDATE) and conditions (IF). Word keeps the saved results of the others (document
/// properties, formulas, FILENAME, NUMWORDS, USERNAME, DOCVARIABLE, MERGEFIELD …), and so does the layout.
/// </summary>
/// <remarks>
/// Error texts and the words of PAGEREF \p are in the language of Word's user interface: German as measured with
/// docx/local/fielderrors, else English; an empty field shows nothing.
/// </remarks>
public sealed class FieldEvaluator
{
    readonly DocumentContext context;
    readonly bool german;

    string T(string english, string germanText) => german ? germanText : english;

    /// <summary>Whether a result is one of Word's error texts (which Word sets in bold before Word 2013).</summary>
    public static bool IsError(string value) => value.StartsWith("Error! ", StringComparison.Ordinal) || value.StartsWith("Fehler! ", StringComparison.Ordinal);
    readonly Dictionary<FieldInline, string> sequences = new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<FieldInline, int> sections = new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<FieldInline, ParagraphBlock> paragraphOf = new(ReferenceEqualityComparer.Instance);

    /// <summary>The text of every bookmark and the paragraph it begins in (REF).</summary>
    readonly Dictionary<string, (StringBuilder Text, ParagraphBlock Paragraph)> bookmarks = new(StringComparer.Ordinal);

    /// <summary>The body's paragraphs in their order, and the place of each.</summary>
    readonly List<ParagraphBlock> paragraphs = [];
    readonly Dictionary<ParagraphBlock, int> order = new(ReferenceEqualityComparer.Instance);

    // What the pages of the last layout tell.
    List<PageBox> pages = [];
    readonly Dictionary<string, PageBox> bookmarkPages = new(StringComparer.Ordinal);
    readonly Dictionary<ParagraphBlock, PageBox> paragraphPages = new(ReferenceEqualityComparer.Instance);

    /// <summary>The time the fields' dates and times show: when the document is printed.</summary>
    readonly DateTime now = DateTime.Now;

    public FieldEvaluator(DocumentContext context, List<Block> blocks)
    {
        this.context = context;
        german = context.Culture.TwoLetterISOLanguageName == "de";
        var counters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var section = 0;
        var open = new Dictionary<string, string>(StringComparer.Ordinal);
        void Inlines(IEnumerable<Inline> inlines, ParagraphBlock paragraph)
        {
            foreach (var inline in inlines)
            {
                switch (inline)
                {
                    case BookmarkInline start:
                        bookmarks.TryAdd(start.Name, (new StringBuilder(), paragraph));
                        if (start.Id != null)
                            open[start.Id] = start.Name;
                        continue;
                    case BookmarkEndInline end:
                        open.Remove(end.Id);
                        continue;
                    case TextInline text:
                        foreach (var name in open.Values)
                            bookmarks[name].Text.Append(text.Text);
                        continue;
                }
                if (inline is not FieldInline field)
                    continue;
                paragraphOf[field] = paragraph;
                sections[field] = section;
                if (field.Type == "SEQ")
                    sequences[field] = Sequence(field, counters) ?? T("Error! No sequence specified.", "Fehler! Es wurde keine Folge festgelegt.");
                Inlines(field.Cached, paragraph);
            }
        }
        void Blocks(IEnumerable<Block> list)
        {
            foreach (var block in list)
            {
                switch (block)
                {
                    case ParagraphBlock paragraph:
                        order[paragraph] = paragraphs.Count;
                        paragraphs.Add(paragraph);
                        // A heading resets the sequences that restart at its level (SEQ \s).
                        if (paragraph.Format.OutlineLevel is { } level and < 9)
                        {
                            foreach (var key in counters.Keys.Where(k => k.EndsWith($"\u0001{level + 1}", StringComparison.Ordinal)).ToList())
                                counters[key] = 0;
                        }
                        Inlines(paragraph.Content, paragraph);
                        if (paragraph.EndsSection != null)
                            section++;
                        break;
                    case TableBlock table:
                        foreach (var row in table.Table.Rows)
                        {
                            foreach (var cell in row.Cells)
                                Blocks(cell.Blocks);
                        }
                        break;
                }
            }
        }
        Blocks(blocks);
    }

    /// <summary>Whether a field's result depends on the pages.</summary>
    public static bool Paged(FieldInline field) => field.Type is "PAGE" or "NUMPAGES" or "SECTIONPAGES" or "PAGEREF" or "STYLEREF";

    /// <summary>Takes in the pages of a layout; whether a result that depends on them changed.</summary>
    public bool Update(List<PageBox> laid)
    {
        var before = new Dictionary<FieldInline, string?>(ReferenceEqualityComparer.Instance);
        foreach (var field in paragraphs.SelectMany(p => Fields(p.Content)).Where(Paged))
            before[field] = Value(field, null);
        pages = laid;
        bookmarkPages.Clear();
        paragraphPages.Clear();
        foreach (var page in laid)
        {
            foreach (var (name, _, _) in page.Bookmarks)
                bookmarkPages.TryAdd(name, page);
            foreach (var paragraph in page.Paragraphs)
                paragraphPages.TryAdd(paragraph, page);
        }
        return before.Any(f => Value(f.Key, null) != f.Value);

        static IEnumerable<FieldInline> Fields(IEnumerable<Inline> inlines)
        {
            foreach (var field in inlines.OfType<FieldInline>())
            {
                yield return field;
                foreach (var nested in Fields(field.Cached))
                    yield return nested;
            }
        }
    }

    /// <summary>A field's result, or null where it keeps the one Word saved; <paramref name="on"/> is the page of a header or footer.</summary>
    public string? Value(FieldInline field, PageBox? on)
    {
        var words = Words(field.Instruction);
        var format = NumberText.FieldFormat(field.NumberFormat);
        var paragraph = paragraphOf.GetValueOrDefault(field);
        var page = on ?? (paragraph != null ? paragraphPages.GetValueOrDefault(paragraph) : null);
        switch (field.Type)
        {
            case "PAGE" when page != null:
                return page.ChapterPrefix + NumberText.Format(page.DisplayNumber, format ?? page.Section.PageNumberFormat);
            case "NUMPAGES" when pages.Count > 0:
                return NumberText.Format(pages.Count, format);
            case "SECTIONPAGES" when page != null:
                return NumberText.Format(pages.Count(p => p.SectionIndex == page.SectionIndex && !p.Blank), format);
            case "SECTION" when sections.TryGetValue(field, out var section):
                return NumberText.Format(section + 1, format);
            case "SEQ":
                return sequences.GetValueOrDefault(field);
            case "PAGEREF" when words.Count > 1 && bookmarkPages.TryGetValue(words[1], out var target):
            {
                // \p: "above" or "below" on the same page, else "on page" and the number.
                var number = target.ChapterPrefix + NumberText.Format(target.DisplayNumber, format ?? target.Section.PageNumberFormat);
                if (!words.Contains("\\p"))
                    return number;
                if (page != target)
                    return T($"on page {number}", $"Seite {number}");
                return paragraph != null && Above(words[1], paragraph) ? T("above", "oben") : T("below", "unten");
            }
            case "PAGEREF" when words.Count > 1 && !bookmarks.ContainsKey(words[1]):
                return T("Error! Bookmark not defined.", "Fehler! Textmarke nicht definiert.");
            case "":
                return "";
            case "STYLEREF" when words.Count > 1:
                return StyleReference(words, field, on);
            case "DATE" or "TIME" or "PRINTDATE" or "SAVEDATE":
                return Date(field, words);
            case "IF":
                return Condition(words);
            case "REF" when words.Count > 1 && !words[1].StartsWith('\\'):
                // REF: the bookmark's text, or with \n the number of its paragraph.
                if (!bookmarks.TryGetValue(words[1], out var marked))
                    return T("Error! Reference source not found.", "Fehler! Verweisquelle konnte nicht gefunden werden.");
                return words.Contains("\\n") || words.Contains("\\r") || words.Contains("\\w") ? marked.Paragraph.Label?.Text ?? "" : marked.Text.ToString();
            case "SYMBOL" when words.Count > 1:
            {
                // SYMBOL: the character of its code (decimal or 0x hexadecimal) in the field's own formatting.
                var code = words[1];
                var ok = code.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? int.TryParse(code[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex) ? hex : -1
                    : int.TryParse(code, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dec) ? dec : -1;
                return ok > 0 && ok <= 0x10FFFF ? char.ConvertFromUtf32(ok) : null;
            }
        }
        return null;
    }

    /// <summary>Whether a bookmark stands in a paragraph before another.</summary>
    bool Above(string bookmark, ParagraphBlock paragraph)
    {
        var at = paragraphs.FindIndex(p => p.Content.OfType<BookmarkInline>().Any(b => b.Name == bookmark));
        return at >= 0 && at < order.GetValueOrDefault(paragraph);
    }

    /// <summary>
    /// SEQ: the next number of its sequence, the current one (\c) or one it resets to (\r n), hidden with \h; \s n
    /// counts anew after every heading of level n.
    /// </summary>
    static string? Sequence(FieldInline field, Dictionary<string, int> counters)
    {
        var words = Words(field.Instruction);
        if (words.Count < 2)
            return null;
        var key = words[1];
        var at = words.IndexOf("\\s");
        if (at >= 0 && at + 1 < words.Count && int.TryParse(words[at + 1], out var level))
            key += $"\u0001{level}";
        var value = counters.GetValueOrDefault(key);
        var reset = words.IndexOf("\\r");
        if (reset >= 0 && reset + 1 < words.Count && int.TryParse(words[reset + 1], out var start))
            value = start;
        else if (!words.Contains("\\c"))
            value++;
        counters[key] = value;
        return words.Contains("\\h") ? "" : NumberText.Format(value, NumberText.FieldFormat(field.NumberFormat));
    }

    /// <summary>
    /// STYLEREF: the text of a paragraph of the style (named, or a heading level by number): in a header or footer the
    /// first on its page (the last with \l), else the last before the page; in the body the nearest before the field,
    /// else after it. \n gives the paragraph's number instead.
    /// </summary>
    string StyleReference(List<string> words, FieldInline field, PageBox? on)
    {
        var name = words[1];
        bool Matches(ParagraphBlock p)
        {
            var id = p.Format.Style ?? context.Styles.DefaultParagraphStyle;
            if (int.TryParse(name, out var level))
                return p.Format.OutlineLevel == level - 1 && id != null && context.Styles.Find(id)?.Name is { } heading && heading.StartsWith("heading", StringComparison.OrdinalIgnoreCase);
            var own = id != null ? context.Styles.Find(id)?.Name : null;
            return string.Equals(id, name, StringComparison.OrdinalIgnoreCase) || string.Equals(own, name, StringComparison.OrdinalIgnoreCase)
                   || own != null && Heading(own) is { } ownLevel && Heading(name) == ownLevel;
        }
        ParagraphBlock? found = null;
        var last = words.Contains("\\l");
        if (on != null)
        {
            var here = on.Paragraphs.Where(Matches).ToList();
            found = here.Count > 0 ? last ? here[^1] : here[0] : null;
            if (found == null)
            {
                var index = pages.IndexOf(on);
                for (var p = index - 1; p >= 0 && found == null; p--)
                    found = pages[p].Paragraphs.LastOrDefault(Matches);
            }
        }
        else if (paragraphOf.TryGetValue(field, out var paragraph))
        {
            var at = order.GetValueOrDefault(paragraph);
            found = paragraphs.Take(at + 1).LastOrDefault(Matches) ?? paragraphs.Skip(at + 1).FirstOrDefault(Matches);
        }
        if (found == null)
            return T($"Error! Use the Home tab to apply {name} to the text that you want to appear here.",
                $"Fehler! Verwenden Sie die Registerkarte 'Start', um {name} dem Text zuzuweisen, der hier angezeigt werden soll.");
        if (words.Contains("\\n") || words.Contains("\\r") || words.Contains("\\w"))
            return found.Label?.Text ?? "";
        return Text(found.Content).Trim();
    }

    /// <summary>
    /// The level of a built-in heading style's name in the languages Word names it in (heading 1, Überschrift 1, Titre 1,
    /// Título 1, Titolo 1, Kop 1 …; field codes name styles as the user's Word does), or null.
    /// </summary>
    static int? Heading(string name)
    {
        var match = System.Text.RegularExpressions.Regex.Match(name.Trim(),
            @"^(heading|überschrift|titre|título|titulo|titolo|kop|rubrik|otsikko|overskrift|nagłówek|заголовок|başlık|nadpis|címsor|kopregel|cabeçalho|títol)\s*(\d)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[2].Value[0] - '0' : null;
    }

    /// <summary>The text of inlines as a paragraph shows it.</summary>
    static string Text(IEnumerable<Inline> inlines)
    {
        var sb = new StringBuilder();
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case TextInline t: sb.Append(t.Text); break;
                case TabInline: sb.Append(' '); break;
                case FieldInline f: sb.Append(Text(f.Cached)); break;
                case NoteReferenceInline n: sb.Append(n.Mark); break;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// DATE, TIME, PRINTDATE and SAVEDATE show the time of printing in the picture of their \@ switch, else in the
    /// document's language's short date (or short time); Word's AM/PM is .NET's tt.
    /// </summary>
    string Date(FieldInline field, List<string> words)
    {
        var culture = context.Culture;
        // SAVEDATE shows when the document was last saved, where its properties say (measured: without them Word
        // shows the time of printing).
        var time = now;
        if (field.Type == "SAVEDATE" && Pptx.DeckInfo.ReadProperties(context.Package, context.Load).Modified is { } modified
            && DateTime.TryParse(modified, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var saved))
            time = saved.ToLocalTime();
        var at = words.IndexOf("\\@");
        var picture = at >= 0 && at + 1 < words.Count ? words[at + 1]
            : field.Type == "TIME" ? culture.DateTimeFormat.ShortTimePattern : culture.DateTimeFormat.ShortDatePattern;
        var sb = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < picture.Length; i++)
        {
            var c = picture[i];
            if (c == '\'')
            {
                quoted = !quoted;
                sb.Append(c);
                continue;
            }
            if (!quoted && (picture.AsSpan(i).StartsWith("am/pm", StringComparison.OrdinalIgnoreCase)))
            {
                sb.Append("tt");
                i += 4;
                continue;
            }
            // Separators stay as written, not the culture's.
            if (!quoted && c is '/' or ':' or '%' or '\\')
            {
                sb.Append('\\').Append(c);
                continue;
            }
            sb.Append(c);
        }
        try
        {
            return time.ToString(sb.ToString(), culture);
        }
        catch (FormatException)
        {
            return time.ToString(culture);
        }
    }

    /// <summary>IF: compares two values (as numbers where both are, else as text, * and ? matching any) and shows one of two texts.</summary>
    static string? Condition(List<string> words)
    {
        if (words.Count < 6 || words[2] is not ("=" or "<>" or "<" or "<=" or ">" or ">="))
            return null;
        var (a, op, b) = (words[1], words[2], words[3]);
        int comparison;
        if (double.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) && double.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            comparison = x.CompareTo(y);
        else if (op is "=" or "<>" && (b.Contains('*') || b.Contains('?')))
            comparison = System.Text.RegularExpressions.Regex.IsMatch(a, "^" + System.Text.RegularExpressions.Regex.Escape(b).Replace("\\*", ".*").Replace("\\?", ".") + "$") ? 0 : 1;
        else
            comparison = string.CompareOrdinal(a, b);
        var result = op switch
        {
            "=" => comparison == 0, "<>" => comparison != 0, "<" => comparison < 0, "<=" => comparison <= 0, ">" => comparison > 0, _ => comparison >= 0,
        };
        return result ? words[4] : words[5];
    }

    /// <summary>The words of a field instruction, quotes removed from quoted ones.</summary>
    static List<string> Words(string instruction)
    {
        var words = new List<string>();
        var sb = new StringBuilder();
        var quoted = false;
        var any = false;
        foreach (var c in instruction)
        {
            if (c == '"')
            {
                quoted = !quoted;
                any = true;
                continue;
            }
            if (char.IsWhiteSpace(c) && !quoted)
            {
                if (sb.Length > 0 || any)
                    words.Add(sb.ToString());
                sb.Clear();
                any = false;
                continue;
            }
            sb.Append(c);
        }
        if (sb.Length > 0 || any)
            words.Add(sb.ToString());
        return words;
    }
}
