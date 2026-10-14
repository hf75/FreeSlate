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

    /// <summary>The first note reference in every bookmark (NOTEREF).</summary>
    readonly Dictionary<string, NoteReferenceInline> noteMarks = new(StringComparer.Ordinal);

    /// <summary>The body's paragraphs in their order, and the place of each.</summary>
    readonly List<ParagraphBlock> paragraphs = [];
    readonly Dictionary<ParagraphBlock, int> order = new(ReferenceEqualityComparer.Instance);

    // What the pages of the last layout tell.
    List<PageBox> pages = [];

    /// <summary>The pages of the layout in progress, where a header's STYLEREF looks back from its page.</summary>
    public List<PageBox>? Current { get; set; }

    /// <summary>Whether a header's STYLEREF had to guess its page's paragraphs (no layout before): another one follows.</summary>
    bool guessed;

    /// <summary>The page whose header the layout in progress measures, for its fields without a page of their own.</summary>
    public PageBox? Laying { get; set; }

    /// <summary>
    /// The body paragraphs of a page whose header is laid out: while the layout in progress lays the page out, those
    /// of the same page of the last layout (a header's height decides where the page's text begins).
    /// </summary>
    List<ParagraphBlock> ParagraphsOf(PageBox on)
    {
        if (Current is { } current && !current.Contains(on) && on.Paragraphs.Count == 0)
        {
            if (pages.FirstOrDefault(p => p.Number == on.Number) is { } before)
                return before.Paragraphs;
            guessed = true;
        }
        return on.Paragraphs;
    }

    /// <summary>The pages before a page, the nearest first: of the layout in progress where it holds the page or is
    /// laying it out, else of the last layout.</summary>
    IEnumerable<PageBox> Before(PageBox on)
    {
        var list = Current is { } current && (current.Contains(on) || current.Count == 0 || current[^1].Number < on.Number) ? current : pages;
        var index = list.IndexOf(on);
        if (index < 0)
            index = list.Count(p => p.Number < on.Number);
        for (var p = index - 1; p >= 0; p--)
            yield return list[p];
    }
    readonly Dictionary<string, PageBox> bookmarkPages = new(StringComparer.Ordinal);
    readonly Dictionary<ParagraphBlock, PageBox> paragraphPages = new(ReferenceEqualityComparer.Instance);

    /// <summary>The time the fields' dates and times show: when the document is printed.</summary>
    readonly DateTime now;

    public FieldEvaluator(DocumentContext context, List<Block> blocks)
    {
        this.context = context;
        now = context.Printed ?? DateTime.Now;
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
                    case NoteReferenceInline note when !note.Own:
                        foreach (var name in open.Values)
                            noteMarks.TryAdd(name, note);
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
        var again = guessed;
        guessed = false;
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
        return again || before.Any(f => Value(f.Key, null) != f.Value);

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
            // NOTEREF: the mark of the note in the bookmark, Word's error where the bookmark is missing (measured with
            // docx/local/nref-*: a saved "9" printed as the note's 1; the error in bold in modes 14 and 15, with and
            // without MERGEFORMAT; powertools/RP043, RP044).
            case "NOTEREF" when words.Count > 1 && !words[1].StartsWith('\\') && !bookmarks.ContainsKey(words[1]):
                return T("Error! Bookmark not defined.", "Fehler! Textmarke nicht definiert.");
            case "NOTEREF" when words.Count > 1 && noteMarks.TryGetValue(words[1], out var note) && note.Mark.Length > 0:
                return note.Mark;
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

    /// <summary>
    /// The vertical alignment a NOTEREF with \f gives its number, its note mark's (measured with docx/local/nref-ok-f-14,
    /// -15: superscript as the mark), or null.
    /// </summary>
    public string? NoteAlignment(FieldInline field)
    {
        var words = Words(field.Instruction);
        return field.Type == "NOTEREF" && words.Count > 1 && words.Contains("\\f") && noteMarks.TryGetValue(words[1], out var note)
            ? note.Format.VerticalAlign : null;
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
        on ??= Laying;
        var name = words[1];
        if (CharacterReference(name, words.Contains("\\l"), field, on) is { } marked)
            return marked;
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
            var here = ParagraphsOf(on).Where(Matches).ToList();
            found = here.Count > 0 ? last ? here[^1] : here[0] : null;
            if (found == null)
            {
                foreach (var page in Before(on))
                {
                    found = page.Paragraphs.LastOrDefault(Matches);
                    if (found != null)
                        break;
                }
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
    /// STYLEREF of a character style: the text of the first run of runs in it on the page of a header or footer (the
    /// last with \l), else the last before the page; in the body the nearest before the field, else after it; null
    /// where the name is no character style (docx/external/libreoffice/extras-ooxmlexport/tdf163894_from_top: a
    /// dictionary's headers show the first and the last entry of the page).
    /// </summary>
    string? CharacterReference(string name, bool last, FieldInline field, PageBox? on)
    {
        var style = context.Styles.Find(name) is { Type: "character" } byId ? byId
            : context.Styles.IdByName(name, "character") is { } id ? context.Styles.Find(id) : null;
        if (style == null)
            return null;
        // The groups of runs in the style, in a paragraph's order.
        List<string> Groups(ParagraphBlock paragraph)
        {
            var groups = new List<string>();
            StringBuilder? current = null;
            foreach (var inline in paragraph.Content)
            {
                if (inline is TextInline text && text.Format.Style == style.Id)
                    (current ??= new StringBuilder()).Append(text.Text);
                else if (inline is TextInline or TabInline or FieldInline && current != null)
                {
                    groups.Add(current.ToString());
                    current = null;
                }
            }
            if (current != null)
                groups.Add(current.ToString());
            return groups.Where(g => g.Trim().Length > 0).ToList();
        }
        // The groups of runs in the style on a page, as its body's lines show them (a paragraph's runs on the pages
        // they stand on).
        List<string> OnPage(PageBox page)
        {
            var groups = new List<string>();
            StringBuilder? current = null;
            foreach (var line in page.Lines.Where(l => l.Top >= page.BodyTop - 0.5 && l.Top < page.BodyBottom))
            {
                foreach (var piece in line.Line.Pieces)
                {
                    if (piece.Style.CharacterStyle == style.Id && !piece.IsLabel)
                        (current ??= new StringBuilder()).Append(piece.Text);
                    else if (current != null && piece.Text.Trim().Length > 0)
                    {
                        groups.Add(current.ToString());
                        current = null;
                    }
                }
            }
            if (current != null)
                groups.Add(current.ToString());
            return groups.Where(g => g.Trim().Length > 0).ToList();
        }
        string? found = null;
        if (on != null)
        {
            var source = Current is { } current && !current.Contains(on) && on.Lines.Count == 0 ? pages.FirstOrDefault(p => p.Number == on.Number) : on;
            if (source == null)
                guessed = true;
            var here = source != null ? OnPage(source) : [];
            found = here.Count > 0 ? last ? here[^1] : here[0] : null;
            foreach (var page in Before(on))
            {
                if (found != null)
                    break;
                found = OnPage(page).LastOrDefault();
            }
        }
        else if (paragraphOf.TryGetValue(field, out var paragraph))
        {
            var at = order.GetValueOrDefault(paragraph);
            found = paragraphs.Take(at + 1).SelectMany(Groups).LastOrDefault() ?? paragraphs.Skip(at + 1).SelectMany(Groups).FirstOrDefault();
        }
        return found?.Trim() ?? T($"Error! Use the Home tab to apply {name} to the text that you want to appear here.",
            $"Fehler! Verwenden Sie die Registerkarte 'Start', um {name} dem Text zuzuweisen, der hier angezeigt werden soll.");
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
    /// Where Word's own date data differ from .NET's for a language: its AM and PM (none in most European languages,
    /// upper case in all English ones), its short time and its short date (measured with docx/local/datelang); the
    /// calendar is the Gregorian in all of them (ar-SA too).
    /// </summary>
    static readonly Dictionary<string, (string Am, string Pm, string? Time, string? Date)> WordDates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = ("AM", "PM", "h:mm tt", null),
        ["de"] = ("", "", null, null),
        ["fr"] = ("", "", null, null),
        ["es"] = ("", "", null, null),
        ["it"] = ("", "", null, null),
        ["nl"] = ("", "", null, null),
        ["ru"] = ("", "", null, null),
        ["pt"] = ("", "", null, null),
        ["sv"] = ("", "", "HH.mm", null),
        ["pl"] = ("AM", "PM", "h:mm tt", null),
        ["tr"] = ("ÖÖ", "ÖS", "h:mm tt", null),
        ["el"] = ("πμ", "μμ", "h:mm tt", null),
        ["ja"] = ("午前", "午後", "tth時mm分", null),
        ["zh"] = ("上午", "下午", "H时mm分", null),
        ["ko"] = ("오전", "오후", "tt h시 mm분", "yyyy-MM-dd"),
        ["ar"] = ("ص", "م", "h:mm  tt", "dd/MM/yyyy"),
        // With a plain quotation mark, not the gershayim; its AM by its PM.
        ["he"] = ("לפנה\"צ", "אחה\"צ", "h:mm tt", "dd/MM/yyyy"),
    };

    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, CultureInfo?> languageCultures = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The culture a w:lang value ("en-US") formats dates in, as Word does, or null where .NET knows none.</summary>
    static CultureInfo? LanguageCulture(string? language) => string.IsNullOrEmpty(language) ? null : languageCultures.GetOrAdd(language, name =>
    {
        try
        {
            var culture = (CultureInfo)CultureInfo.GetCultureInfo(name).Clone();
            var format = culture.DateTimeFormat;
            if (format.Calendar is not GregorianCalendar)
                format.Calendar = new GregorianCalendar();
            if (WordDates.TryGetValue(culture.TwoLetterISOLanguageName, out var word))
            {
                (format.AMDesignator, format.PMDesignator) = (word.Am, word.Pm);
                if (word.Time != null)
                    format.ShortTimePattern = word.Time;
                if (word.Date != null)
                    format.ShortDatePattern = word.Date;
            }
            return CultureInfo.ReadOnly(culture);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    });

    /// <summary>
    /// DATE, TIME, PRINTDATE and SAVEDATE show the time of printing in the picture of their \@ switch, else in the
    /// short date (or short time) of the field's language, whose names and AM/PM they take too, whatever the
    /// computer's regional settings (measured with docx/local/datelang on a German Windows: en-US "Tuesday, October
    /// 13, 2026", "10/13/2026" and "12:22 PM", de-DE "Dienstag", "13.10.2026" and no AM/PM, fr-FR "mardi",
    /// "13/10/2026" and "oct."); Word's AM/PM is .NET's tt.
    /// </summary>
    string Date(FieldInline field, List<string> words)
    {
        var culture = LanguageCulture(field.Format.Language) ?? context.Culture;
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
