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

    /// <summary>Whether the body has a field that Word updates when it prints, which updates its IF fields too.</summary>
    bool bodyUpdates;

    /// <summary>The document's properties by Word's DOCPROPERTY names: the core and extended ones that hold text, the custom ones.</summary>
    Dictionary<string, string?>? properties;

    /// <summary>A custom property of a type the layout does not show (dates, booleans ...), which keeps the saved result.</summary>
    const string Unread = "\u0001";

    /// <summary>
    /// A document property's text for DOCPROPERTY, Word's error where the document has none of that name or it is empty;
    /// null (the saved result) for the built-in ones the layout does not know at print time (dates, counts, security).
    /// </summary>
    string? DocumentProperty(string name)
    {
        properties ??= Properties();
        if (properties.TryGetValue(name, out var value))
            return value == Unread ? null : string.IsNullOrEmpty(value) ? T("Error! Unknown document property name.", "Fehler! Unbekannter Name für Dokument-Eigenschaft.") : value;
        return name.ToUpperInvariant() is "BYTES" or "CHARACTERS" or "CHARACTERSWITHSPACES" or "CREATETIME" or "LASTPRINTED" or "LASTSAVEDTIME"
            or "LINES" or "PAGES" or "PARAGRAPHS" or "REVISIONNUMBER" or "SECURITY" or "TOTALEDITINGTIME" or "WORDS" or "ODMADOCID"
            ? null
            : T("Error! Unknown document property name.", "Fehler! Unbekannter Name für Dokument-Eigenschaft.");
    }

    /// <summary>The package's core, extended and custom properties by Word's names (see <see cref="DocumentProperty"/>).</summary>
    Dictionary<string, string?> Properties()
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var package = context.Package;
        System.Xml.Linq.XElement? Part(string type) =>
            package.FirstRelationship("/", type)?.Target is { } part && package.Exists(part) ? context.Load(part).Root : null;
        static string? Child(System.Xml.Linq.XElement? root, string name) => root?.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value;
        var core = Part("core-properties");
        foreach (var (word, element) in new[] { ("Title", "title"), ("Subject", "subject"), ("Author", "creator"), ("Keywords", "keywords"),
                     ("Comments", "description"), ("Category", "category"), ("LastSavedBy", "lastModifiedBy") })
            result[word] = Child(core, element);
        var app = Part("extended-properties");
        foreach (var (word, element) in new[] { ("Company", "Company"), ("Manager", "Manager"), ("HyperlinkBase", "HyperlinkBase"),
                     ("Template", "Template"), ("NameofApplication", "Application") })
            result[word] = Child(app, element);
        if (Part("custom-properties") is { } custom)
        {
            foreach (var property in custom.Elements().Where(e => e.Name.LocalName == "property"))
            {
                if ((string?)property.Attribute("name") is not { } name)
                    continue;
                var value = property.Elements().FirstOrDefault();
                result[name] = value?.Name.LocalName is "lpwstr" or "lpstr" or "bstr" or "i1" or "i2" or "i4" or "i8" or "int" or "ui1" or "ui2"
                    or "ui4" or "ui8" or "uint" or "r4" or "r8" or "decimal" ? value.Value : Unread;
            }
        }
        return result;
    }

    /// <summary>Whether a result is one of Word's error texts (which Word sets in bold).</summary>
    public static bool IsError(string value) => value.StartsWith("Error! ", StringComparison.Ordinal) || value.StartsWith("Fehler! ", StringComparison.Ordinal);
    readonly Dictionary<FieldInline, string> sequences = new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<FieldInline, int> sections = new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<FieldInline, ParagraphBlock> paragraphOf = new(ReferenceEqualityComparer.Instance);

    /// <summary>The text of every bookmark and the paragraph it begins in (REF).</summary>
    readonly Dictionary<string, (StringBuilder Text, ParagraphBlock Paragraph)> bookmarks = new(StringComparer.Ordinal);

    /// <summary>The first note reference in every bookmark (NOTEREF).</summary>
    readonly Dictionary<string, NoteReferenceInline> noteMarks = new(StringComparer.Ordinal);

    /// <summary>The paragraphs that stand where bookmarks are open: their text is the bookmarks' (see For).</summary>
    readonly HashSet<ParagraphBlock> covered = new(ReferenceEqualityComparer.Instance);

    /// <summary>The words of the instructions of fields (see Words), each split once.</summary>
    readonly Dictionary<string, List<string>> instructionWords = new(StringComparer.Ordinal);

    List<string> WordsOf(FieldInline field) =>
        instructionWords.TryGetValue(field.Instruction, out var words) ? words : instructionWords[field.Instruction] = Words(field.Instruction);

    /// <summary>The type and the number format of the instructions of fields (FieldInline.Type, NumberFormat), each worked out once.</summary>
    readonly Dictionary<string, (string Type, string? Format)> instructionKinds = new(StringComparer.Ordinal);

    (string Type, string? Format) KindOf(FieldInline field) =>
        instructionKinds.TryGetValue(field.Instruction, out var kind) ? kind : instructionKinds[field.Instruction] = (field.Type, field.NumberFormat);

    /// <summary>The fields of the body whose results depend on the pages (see Paged), which Update compares.</summary>
    readonly List<FieldInline> pagedFields = [];

    /// <summary>The body's blocks, and its paragraphs in their order with the place of each (by Block.Id), worked out where asked.</summary>
    readonly List<Block> blocks;
    List<ParagraphBlock>? paragraphs;
    Dictionary<int, int>? order;

    List<ParagraphBlock> Paragraphs
    {
        get
        {
            if (paragraphs == null)
            {
                paragraphs = [];
                void Add(IEnumerable<Block> list)
                {
                    foreach (var block in list)
                    {
                        if (block is ParagraphBlock paragraph)
                            paragraphs.Add(paragraph);
                        else if (block is TableBlock table)
                        {
                            foreach (var cell in table.Table.Rows.SelectMany(r => r.Cells))
                                Add(cell.Blocks);
                        }
                    }
                }
                Add(blocks);
            }
            return paragraphs;
        }
    }

    Dictionary<int, int> Order
    {
        get
        {
            if (order == null)
            {
                order = [];
                for (var k = 0; k < Paragraphs.Count; k++)
                    order[Paragraphs[k].Id] = k;
            }
            return order;
        }
    }

    /// <summary>
    /// The paragraphs that hold fields of the body whose results depend on the page they stand on (PAGE, SECTIONPAGES,
    /// PAGEREF \p), by Block.Id: the pages of those alone are taken in (see paragraphPages).
    /// </summary>
    readonly HashSet<int> pagedParagraphs = [];

    // What the pages of the last layout tell.
    List<PageBox> pages = [];

    /// <summary>The pages of the layout in progress, where a header's STYLEREF looks back from its page.</summary>
    public List<PageBox>? Current { get; set; }

    /// <summary>Whether a header's STYLEREF had to guess its page's paragraphs (no layout before): another one follows.</summary>
    bool guessed;

    /// <summary>The fields of headers and footers that came out empty on a page (see <see cref="FieldInline.Unmerged"/>).</summary>
    readonly HashSet<FieldInline> emptied = new(ReferenceEqualityComparer.Instance);

    /// <summary>Whether a field came out empty for the first time in the layout in progress: another one follows.</summary>
    bool newlyEmptied;

    /// <summary>Whether another layout follows: a header's STYLEREF guessed, or a field came out empty for the first time.</summary>
    internal bool Pending => guessed || newlyEmptied;

    /// <summary>Notes that a header's or footer's field came out empty on a page.</summary>
    public void Empty(FieldInline field) => newlyEmptied |= emptied.Add(field);

    /// <summary>Whether a header's or footer's field came out empty on some page.</summary>
    public bool Emptied(FieldInline field) => emptied.Contains(field);

    /// <summary>The page whose header the layout in progress measures, for its fields without a page of their own.</summary>
    public PageBox? Laying { get; set; }

    /// <summary>
    /// What the fields know at a point of a layout in progress (see Paginator.Checkpoint): the last layout's pages and
    /// the pages its bookmarks and paragraphs stood on, the fields that came out empty, and whether another layout
    /// follows.
    /// </summary>
    internal sealed record Snapshot(List<PageBox> Pages, Dictionary<string, PageBox> BookmarkPages, Dictionary<int, PageBox> ParagraphPages,
        HashSet<FieldInline> Emptied, bool Guessed, bool NewlyEmptied);

    /// <summary>The fields' state now, the last layout's shared with <paramref name="before"/> where it is the same layout.</summary>
    internal Snapshot Take(Snapshot? before)
    {
        var same = before != null && ReferenceEquals(before.Pages, pages);
        return new(pages, same ? before!.BookmarkPages : bookmarkPages, same ? before!.ParagraphPages : paragraphPages,
            new(emptied, ReferenceEqualityComparer.Instance), guessed, newlyEmptied);
    }

    /// <summary>
    /// Whether fields read in these blocks what they read in another evaluator's (edited) blocks: the same bookmarks
    /// with the same text and notes, and the same sequence numbers; what depends on pages aside.
    /// </summary>
    internal bool ReadsAs(FieldEvaluator other) =>
        ReferenceEquals(bookmarks, other.bookmarks) && ReferenceEquals(noteMarks, other.noteMarks) && ReferenceEquals(sequences, other.sequences)
        || bookmarks.Count == other.bookmarks.Count
        && bookmarks.All(b => other.bookmarks.TryGetValue(b.Key, out var o) && o.Text.ToString() == b.Value.Text.ToString())
        && noteMarks.Count == other.noteMarks.Count && noteMarks.All(n => other.noteMarks.TryGetValue(n.Key, out var o) && ReferenceEquals(o, n.Value))
        && sequences.Count == other.sequences.Count && sequences.All(s => other.sequences.TryGetValue(s.Key, out var o) && o == s.Value);

    /// <summary>Whether the fields know what a snapshot of the same layout knew: those that came out empty and whether another layout follows.</summary>
    internal bool Knows(Snapshot snapshot) => emptied.SetEquals(snapshot.Emptied) && guessed == snapshot.Guessed && newlyEmptied == snapshot.NewlyEmptied;

    /// <summary>Goes back to a snapshot's state.</summary>
    internal void Restore(Snapshot snapshot)
    {
        pages = snapshot.Pages;
        (bookmarkPages, paragraphPages) = (snapshot.BookmarkPages, snapshot.ParagraphPages);
        emptied.Clear();
        emptied.UnionWith(snapshot.Emptied);
        (guessed, newlyEmptied) = (snapshot.Guessed, snapshot.NewlyEmptied);
    }

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
    Dictionary<string, PageBox> bookmarkPages = new(StringComparer.Ordinal);
    /// <summary>The page each paragraph of the last layout began on, by Block.Id (its copies' too).</summary>
    Dictionary<int, PageBox> paragraphPages = [];

    /// <summary>The time the fields' dates and times show: when the document is printed.</summary>
    readonly DateTime now;

    public FieldEvaluator(DocumentContext context, List<Block> blocks)
    {
        this.context = context;
        this.blocks = blocks;
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
                var type = KindOf(field).Type;
                bodyUpdates |= type is not ("IF" or "SYMBOL" or "DOCPROPERTY" or "");
                if (type is "PAGE" or "SECTIONPAGES" || type == "PAGEREF" && WordsOf(field).Contains("\\p"))
                    pagedParagraphs.Add(paragraph.Id);
                if (Paged(type))
                    pagedFields.Add(field);
                sections[field] = section;
                if (type == "SEQ")
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
                        if (open.Count > 0)
                            covered.Add(paragraph);
                        // A heading resets the sequences that restart at its level (SEQ \s).
                        if (paragraph.Format.OutlineLevel is { } level and < 9 && counters.Count > 0)
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

    /// <summary>An evaluator of other blocks that reads what this one read of its own (see For).</summary>
    FieldEvaluator(FieldEvaluator source, List<Block> blocks)
    {
        (context, german, now, this.blocks) = (source.context, source.german, source.now, blocks);
        (bodyUpdates, properties) = (source.bodyUpdates, source.properties);
        (sequences, sections, paragraphOf, bookmarks, noteMarks) = (source.sequences, source.sections, source.paragraphOf, source.bookmarks, source.noteMarks);
        (covered, instructionWords, pagedParagraphs) = (source.covered, source.instructionWords, source.pagedParagraphs);
        (instructionKinds, pagedFields) = (source.instructionKinds, source.pagedFields);
    }

    /// <summary>
    /// The fields of the blocks after an edit (see Paginator.Relayout), <paramref name="differing"/> those that differ
    /// (the edited ones and the ones they replaced): where none of those holds what fields read (bookmarks, fields, note
    /// references), stands where a bookmark is open or, with sequences, is a heading, what this evaluator read of its
    /// blocks holds for the new ones too (what the pages tell comes from a snapshot, see Restore); else they are read
    /// whole.
    /// </summary>
    internal FieldEvaluator For(List<Block> blocks, IEnumerable<Block> differing) =>
        differing.Any(Affects) ? new FieldEvaluator(context, blocks) : new FieldEvaluator(this, blocks);

    /// <summary>Whether a block changes what fields read (see For), in its paragraphs and its tables' cells.</summary>
    bool Affects(Block block) => block switch
    {
        ParagraphBlock paragraph => paragraph.Content.Any(i => i is BookmarkInline or BookmarkEndInline or FieldInline or NoteReferenceInline)
            || covered.Contains(paragraph) || sequences.Count > 0 && paragraph.Format.OutlineLevel is < 9,
        TableBlock table => table.Table.Rows.SelectMany(r => r.Cells).SelectMany(c => c.Blocks).Any(Affects),
        _ => false,
    };

    /// <summary>The fields of the body whose results came out otherwise in the last Update, and the paragraph each stands in.</summary>
    internal IReadOnlyList<FieldInline> Changed => changed;
    List<FieldInline> changed = [];

    internal ParagraphBlock? ParagraphOf(FieldInline field) => paragraphOf.GetValueOrDefault(field);

    /// <summary>Whether a field's result depends on the pages.</summary>
    public static bool Paged(FieldInline field) => Paged(field.Type);

    static bool Paged(string type) => type is "PAGE" or "NUMPAGES" or "SECTIONPAGES" or "PAGEREF" or "STYLEREF";

    /// <summary>Takes in the pages of a layout; whether a result that depends on them changed.</summary>
    public bool Update(List<PageBox> laid)
    {
        var before = new Dictionary<FieldInline, string?>(ReferenceEqualityComparer.Instance);
        foreach (var field in pagedFields)
            before[field] = Value(field, null);
        var again = guessed || newlyEmptied;
        (guessed, newlyEmptied) = (false, false);
        pages = laid;
        // New maps: snapshots and evaluators share the old ones (see Take, Restore).
        (bookmarkPages, paragraphPages) = (new(StringComparer.Ordinal), []);
        foreach (var page in laid)
        {
            foreach (var (name, _, _) in page.Bookmarks)
                bookmarkPages.TryAdd(name, page);
            if (pagedParagraphs.Count == 0)
                continue;
            foreach (var paragraph in page.Paragraphs)
            {
                if (pagedParagraphs.Contains(paragraph.Id))
                    paragraphPages.TryAdd(paragraph.Id, page);
            }
        }
        changed = [.. before.Where(f => Value(f.Key, null) != f.Value).Select(f => f.Key)];
        return again || changed.Count > 0;
    }

    /// <summary>A field's result, or null where it keeps the one Word saved; <paramref name="on"/> is the page of a header or footer.</summary>
    public string? Value(FieldInline field, PageBox? on)
    {
        var words = WordsOf(field);
        var (type, numberFormat) = KindOf(field);
        var format = NumberText.FieldFormat(numberFormat);
        var paragraph = paragraphOf.GetValueOrDefault(field);
        var page = on ?? (paragraph != null ? paragraphPages.GetValueOrDefault(paragraph.Id) : null);
        switch (type)
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
            // A reference without a bookmark's name (docx/external/libreoffice/extras-ooxmlexport/Unsupportedtextfields:
            // NOTEREF without a name and REF "").
            case "NOTEREF" or "REF" when words.Count < 2 || words[1].Length == 0 || words[1].StartsWith('\\'):
                return T("Error! No bookmark name given.", "Fehler! Es wurde kein Textmarkenname vergeben.");
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
            // Word computes IF where it updates the fields of the story when it prints: always in headers and footers, in
            // the body where it has a field of the kinds Word updates then (measured with docx/local/iff-*: a body's IF
            // keeps its saved "maybe" in modes 11 to 15 and without one, a header's prints "yes"; ifg-*: with a PAGEREF,
            // REF, SEQ, DATE, NUMPAGES, PAGE or STYLEREF beside it the body's prints "yes" too, with a header's PAGE not;
            // docx/external/libreoffice/extras-ooxmlimport/tdf125038c, found by agent BK).
            case "IF" when on != null || bodyUpdates:
                return Condition(words);
            // Headers and footers show a document property as it is when Word prints, Word's error where the package has
            // none of that name or it is empty; the body keeps the result Word saved (measured with docx/local/dpp-*: a
            // saved "Stale" in the body whatever the property, in a header a custom property's and the core title's
            // value, the error in bold for a missing custom property and an empty subject; docx/external/libreoffice/
            // extras-odfexport/tdf151100, found by agent BL).
            case "DOCPROPERTY" when on != null && words.Count > 1:
                return DocumentProperty(words[1]);
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
    /// A SYMBOL field's character in its own font (\f) and size (\s, points) over the field's formatting, or null
    /// (docx/external/libreoffice/extras-ooxmlexport/tdf85161: code 94 in Symbol prints ⊥).
    /// </summary>
    public RunFormat? SymbolFormat(FieldInline field, RunFormat format)
    {
        if (field.Type != "SYMBOL")
            return null;
        var words = WordsOf(field);
        string? After(string name) => words.IndexOf(name) is var i and >= 0 && i + 1 < words.Count ? words[i + 1] : null;
        var (font, size) = (After("\\f"), After("\\s"));
        if (font == null && size == null)
            return null;
        var shown = format.Clone();
        if (font is { Length: > 0 })
        {
            (shown.Ascii, shown.HAnsi, shown.EastAsia, shown.ComplexScript) = (font, font, font, font);
            (shown.AsciiTheme, shown.HAnsiTheme, shown.EastAsiaTheme, shown.ComplexScriptTheme) = (null, null, null, null);
        }
        if (size != null && double.TryParse(size, NumberStyles.Float, CultureInfo.InvariantCulture, out var points) && points > 0)
            (shown.Size, shown.SizeComplex) = (points, points);
        return shown;
    }

    /// <summary>
    /// The vertical alignment a NOTEREF with \f gives its number, its note mark's (measured with docx/local/nref-ok-f-14,
    /// -15: superscript as the mark), or null.
    /// </summary>
    public string? NoteAlignment(FieldInline field)
    {
        var words = WordsOf(field);
        return field.Type == "NOTEREF" && words.Count > 1 && words.Contains("\\f") && noteMarks.TryGetValue(words[1], out var note)
            ? note.Format.VerticalAlign : null;
    }

    /// <summary>Whether a bookmark stands in a paragraph before another.</summary>
    bool Above(string bookmark, ParagraphBlock paragraph)
    {
        var at = Paragraphs.FindIndex(p => p.Content.OfType<BookmarkInline>().Any(b => b.Name == bookmark));
        return at >= 0 && at < Order.GetValueOrDefault(paragraph.Id);
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
            // A built-in heading goes by the name Word's user interface gives it, in its language (docx/external/
            // libreoffice/extras-ooxmlexport/suppress-non-numerical, core-fields/styleref-flags, tdf86790: "Heading 1"
            // finds no "heading 1" in German Word; tdf149089: nor does "标题 1"; StyleRef-DE, tdf166850: "Überschrift
            // 1" finds it).
            var own = id != null ? context.Styles.Find(id)?.Name : null;
            if (own != null && Heading(own) is { } ownLevel)
                own = T($"Heading {ownLevel}", $"Überschrift {ownLevel}");
            // So does Normal, "Standard" in German Word (docx/external/libreoffice/extras-ooxmlexport/
            // Unsupportedtextfields: STYLEREF Normal prints the error).
            if (string.Equals(own, "Normal", StringComparison.OrdinalIgnoreCase))
                return string.Equals(T("Normal", "Standard"), name, StringComparison.OrdinalIgnoreCase);
            return string.Equals(id, name, StringComparison.OrdinalIgnoreCase) || string.Equals(own, name, StringComparison.OrdinalIgnoreCase);
        }
        ParagraphBlock? found = null;
        var last = words.Contains("\\l");
        if (on != null)
        {
            // An empty paragraph of the style that ended a section at the page's top before the next section took
            // the page empties the field on the way, though the page shows what follows (Word's PDF of docx/external/
            // libreoffice/extras-ooxmlexport/tdf170602_exportContext: an empty heading holding a section break after a
            // page break; the headers print "Einleitung" and all others plain; docx/local/tdh-g).
            if (field.Unmerged != null && on.Passed.Any(Matches))
                Empty(field);
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
            // Without one on the page or before it, the first after it (measured with docx/local/fsf4-c: a header's
            // STYLEREF on a page before the first heading prints that heading, not the error).
            found ??= Paragraphs.FirstOrDefault(Matches);
        }
        else if (paragraphOf.TryGetValue(field, out var paragraph))
        {
            var at = Order.GetValueOrDefault(paragraph.Id);
            found = Paragraphs.Take(at + 1).LastOrDefault(Matches) ?? Paragraphs.Skip(at + 1).FirstOrDefault(Matches);
        }
        // A heading level that no paragraph has gives an error of its own (docx/external/libreoffice/
        // extras-ooxmlexport/tdf146248).
        if (found == null && int.TryParse(name, out _))
            return T("Error! No text of specified style in document.", "Fehler! Kein Text mit angegebener Formatvorlage im Dokument.");
        if (found == null)
            return T($"Error! Use the Home tab to apply {name} to the text that you want to appear here.",
                $"Fehler! Verwenden Sie die Registerkarte 'Start', um {name} dem Text zuzuweisen, der hier angezeigt werden soll.");
        if (words.Contains("\\n") || words.Contains("\\r") || words.Contains("\\w"))
            return found.Label?.Text ?? "";
        // \s (Word's captions with chapter numbers): the number alone, from its first digit to its last, without the
        // level's text around it (docx/external/powertools/HC061: "Table {STYLEREF 1 \s}." of a heading "1." prints
        // "Table 1.").
        if (words.Contains("\\s"))
        {
            var label = found.Label?.Text ?? "";
            var (first, end) = (label.IndexOfAny("0123456789".ToCharArray()), label.LastIndexOfAny("0123456789".ToCharArray()));
            return first >= 0 ? label[first..(end + 1)] : label.Trim().Trim('.', ')', '(', ':', '-');
        }
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
            var at = Order.GetValueOrDefault(paragraph.Id);
            found = Paragraphs.Take(at + 1).SelectMany(Groups).LastOrDefault() ?? Paragraphs.Skip(at + 1).SelectMany(Groups).FirstOrDefault();
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
        // SAVEDATE too shows the time Word prints the document, whatever its properties say (measured with
        // docx/local/svd-core, svd-none: saved 2020-01-02 or without core properties, both print the export time, a
        // CREATEDATE its result, locked fields theirs; docx/external/libreoffice/extras-ooxmlexport/
        // tdf148380_modifiedField, pandoc/text_in_shape_format).
        var time = now;
        var at = words.IndexOf("\\@");
        // Without a picture PRINTDATE and SAVEDATE show the short date and the long time of the field's language, to the
        // minute (docx/external/libreoffice/extras-ooxmlexport/tdf148380_printField: "12/10/2026 11:21:00" in en-GB,
        // "3:34:00 PM" printed at 15:34:25).
        var dated = field.Type is "PRINTDATE" or "SAVEDATE";
        if (dated)
            time = new DateTime(time.Year, time.Month, time.Day, time.Hour, time.Minute, 0, time.Kind);
        var picture = at >= 0 && at + 1 < words.Count ? words[at + 1]
            : field.Type == "TIME" ? culture.DateTimeFormat.ShortTimePattern
            : dated ? culture.DateTimeFormat.ShortDatePattern + " " + culture.DateTimeFormat.LongTimePattern
            : culture.DateTimeFormat.ShortDatePattern;
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
            // A backslash takes the character after it as it is (docx/external/libreoffice/extras-uiwriter/tdf164620:
            // "dddd\ dd\ MMMM\ yyyy" prints "Wednesday 14 October 2026", "dd\/MM\/yyyy\ H:mm:ss\ AM/PM"
            // "14/10/2026 15:36:19 PM").
            if (!quoted && c == '\\')
            {
                if (i + 1 < picture.Length)
                    sb.Append('\\').Append(picture[++i]);
                continue;
            }
            // Separators stay as written, not the culture's.
            if (!quoted && c is '/' or ':' or '%')
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
                // A switch written against its argument's quote is a word of its own (docx/external/libreoffice/
                // extras-uiwriter/tdf164620: SAVEDATE \@"MMMM\ d', 'yyyy").
                if (!quoted && sb.Length == 2 && sb[0] == '\\')
                {
                    words.Add(sb.ToString());
                    sb.Clear();
                }
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
        // A backslash before the field's name does not hide it (as Model.TypeOf).
        if (words.Count > 0 && words[0].Length > 1 && words[0][0] == '\\' && char.IsLetter(words[0][1]))
            words[0] = words[0].TrimStart('\\');
        return words;
    }
}
