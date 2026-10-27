using System.Xml.Linq;

namespace FreeSlate.Docx;

/// <summary>A piece of a paragraph's content with its resolved character formatting.</summary>
public abstract record Inline(RunFormat Format)
{
    /// <summary>
    /// The element of its part's XML the inline was read from (w:t, w:tab, w:br, w:drawing, a field's w:fldChar …), which
    /// writing an edited document changes in place; null for what the reader made of other things (a list number, a
    /// computed field result, an imported HTML run).
    /// </summary>
    public XElement? Xml { get; init; }
}

/// <summary>Text of one run (catalog: run.basic).</summary>
public sealed record TextInline(string Text, RunFormat Format) : Inline(Format)
{
    /// <summary>The hyperlink the text belongs to (catalog: field.hyperlinks): a URL or "#" and a bookmark, or null.</summary>
    public string? Link { get; init; }
}

/// <summary>
/// A footnote or endnote reference (w:footnoteReference, w:endnoteReference, catalog: note.footnotes, note.endnotes):
/// the note's number as its mark, or nothing where a custom mark follows.
/// </summary>
public sealed record NoteReferenceInline(string Id, bool Endnote, string Mark, RunFormat Format) : Inline(Format)
{
    /// <summary>The note's own mark at its start (w:footnoteRef, w:endnoteRef), which refers to nothing.</summary>
    public bool Own { get; init; }
}

/// <summary>The line of a note separator (w:separator, w:continuationSeparator) in the separator notes.</summary>
public sealed record SeparatorInline(bool Continuation, RunFormat Format) : Inline(Format);

/// <summary>Where a bookmark starts (w:bookmarkStart, catalog: field.bookmarks): an anchor in the HTML.</summary>
public sealed record BookmarkInline(string Name, RunFormat Format) : Inline(Format)
{
    /// <summary>The id its w:bookmarkEnd names.</summary>
    public string? Id { get; init; }
}

/// <summary>Where a bookmark ends (w:bookmarkEnd): what REF fields show of it ends here.</summary>
public sealed record BookmarkEndInline(string Id, RunFormat Format) : Inline(Format);

/// <summary>
/// A legacy check box form field (FORMCHECKBOX, catalog: field.forms): checked or not, its size in points (null: the
/// run's font size).
/// </summary>
public sealed record CheckBoxInline(bool Checked, double? Size, RunFormat Format) : Inline(Format);

/// <summary>
/// Ruby (w:ruby, catalog: run.ruby): base text with phonetic text set above it, aligned by w:rubyAlign and raised
/// w:hpsRaise (here in points) above the base's baseline.
/// </summary>
public sealed record RubyInline(List<TextInline> Base, List<TextInline> Ruby, string Align, double Raise, RunFormat Format) : Inline(Format);

/// <summary>The start or end of an editable range (w:permStart, w:permEnd, catalog: review.permissions).</summary>
public sealed record PermissionInline(bool Start, RunFormat Format) : Inline(Format);

/// <summary>
/// An equation (catalog: math): an m:oMath in the text, or a display equation of an m:oMathPara on lines of its own,
/// justified by m:jc or the settings' m:defJc; its runs formatted by their w:rPr over the paragraph's style.
/// </summary>
public sealed record MathInline(XElement Element, bool Display, string? Justification, RunFormat Format, IReadOnlyDictionary<XElement, RunFormat> RunFormats)
    : Inline(Format);

/// <summary>A tab character (w:tab in a run, catalog: paragraph.tabs).</summary>
public sealed record TabInline(RunFormat Format) : Inline(Format)
{
    /// <summary>
    /// An absolute position tab (w:ptab): its alignment (left, center, right), what it is relative to (margin,
    /// indent) and its leader; null for an ordinary tab.
    /// </summary>
    public (string Alignment, string RelativeTo, string Leader)? Positional { get; init; }
}

/// <summary>A break (w:br, w:cr, catalog: run.content): textWrapping (a line break), page or column, with its clear.</summary>
public sealed record BreakInline(string Type, string? Clear, RunFormat Format) : Inline(Format);

/// <summary>
/// A field whose result depends on the page it stands on (PAGE, NUMPAGES, SECTIONPAGES; catalog: field.evaluation):
/// its instruction and the result Word saved, which stands where the page is not known.
/// </summary>
public sealed record FieldInline(string Instruction, RunFormat Format, List<Inline> Cached) : Inline(Format)
{
    /// <summary>The field's type, the instruction's first word in capitals ("PAGE").</summary>
    public string Type => TypeOf(Instruction);

    /// <summary>
    /// The format of an error text the field shows: its separate character's run's (measured with docx/local/ef-15,
    /// ef-14: of a begin, an instruction, a separate and an end run in Calibri, Times New Roman and Arial, a saved
    /// result in Arial 14 and a mark in Courier New, PAGEREF's and REF's errors take the separate run's font and size;
    /// docx/external/libreoffice/extras-ooxmlexport/fdo78910), or null for a field without one.
    /// </summary>
    public RunFormat? ErrorFormat { get; init; }

    /// <summary>
    /// The format of a simple field's result with \* MERGEFORMAT once a header or footer showed it empty on some page:
    /// its paragraph style's, on every page (measured with docx/local/fsf4-b, fsf4-d: a STYLEREF that finds an empty
    /// heading on one page prints plain on all; docx/external/libreoffice/extras-ooxmlexport/tdf170602_exportContext),
    /// or null.
    /// </summary>
    public RunFormat? Unmerged { get; init; }

    /// <summary>The number format of the field's \* switch ("Arabic", "roman", "ALPHABETIC" …), or null.</summary>
    public string? NumberFormat
    {
        get
        {
            var parts = Instruction.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i + 1 < parts.Length; i++)
            {
                if (parts[i] == "\\*" && !parts[i + 1].Equals("MERGEFORMAT", StringComparison.OrdinalIgnoreCase)
                    && !parts[i + 1].Equals("CHARFORMAT", StringComparison.OrdinalIgnoreCase))
                    return parts[i + 1];
            }
            return null;
        }
    }

    /// <summary>
    /// The field's name, upper case; a backslash before it does not hide it (measured: docx/external/libreoffice/
    /// extras-ooxmlexport/tdf85161, "\SYMBOL 94 \f "Symbol"" without a result prints ⊥).
    /// </summary>
    public static string TypeOf(string instruction)
    {
        var name = instruction.Trim().Split(' ', 2)[0];
        return (name.Length > 1 && name[0] == '\\' && char.IsLetter(name[1]) ? name.TrimStart('\\') : name).ToUpperInvariant();
    }

    /// <summary>The fields whose result the layout computes (catalog: field.evaluation); the others show their saved result.</summary>
    public static bool IsPageField(string instruction) => TypeOf(instruction) is "PAGE" or "NUMPAGES" or "SECTIONPAGES" or "PAGEREF"
        or "STYLEREF" or "SEQ" or "SECTION" or "DATE" or "TIME" or "PRINTDATE" or "SAVEDATE" or "IF" or "REF" or "SYMBOL" or "NOTEREF" or "LISTNUM"
        or "AUTONUM" or "AUTONUMLGL" or "AUTONUMOUT" or "BIDIOUTLINE" or "DOCPROPERTY" or "";

    /// <summary>The paragraph numbering fields, which number the paragraphs they stand in (catalog: field.evaluation).</summary>
    public static bool IsAutoNumber(string instruction) => TypeOf(instruction) is "AUTONUM" or "AUTONUMLGL" or "AUTONUMOUT" or "BIDIOUTLINE";
}

/// <summary>
/// A block of the body: a paragraph or a table. Id names it within its document as the reader numbered it (0 for a block
/// made elsewhere, a markup balloon's); a copy of it (with) keeps it, so that what a layout keeps of a block finds the copy
/// it laid out (a paragraph set without its leading break) and does not hang on the object.
/// </summary>
public abstract record Block
{
    public int Id { get; init; }
}

/// <summary>A paragraph with its resolved paragraph formatting, its content and the format of its mark.</summary>
public sealed record ParagraphBlock(XElement Element, ParagraphFormat Format, List<Inline> Content, RunFormat MarkFormat) : Block
{
    /// <summary>The section this paragraph ends (its w:pPr/w:sectPr), or null.</summary>
    public SectionProperties? EndsSection { get; init; }

    /// <summary>
    /// The paragraphs (w:p) the paragraph was read from where the reader joined several, in their order: those whose
    /// marks are deleted (a tracked change) with the one they run into, which is Element, or one whose mark is hidden,
    /// which is Element, with the ones it runs into (see ModelReader.JoinHidden); null for a paragraph of its own.
    /// </summary>
    public IReadOnlyList<XElement>? Parts { get; init; }

    /// <summary>
    /// The paragraph's tracked deleted text outside the markup view, which is not shown but counts where autofit tables
    /// measure their cells (see RunFormat.Deleted), or null.
    /// </summary>
    public List<TextInline>? Deleted { get; init; }

    /// <summary>
    /// Whether anything of the paragraph's content shows: not hidden, not a bookmark or permission mark, not a run without
    /// text (docx/local/bmc-*-o*, -n*: a cell's last paragraph after a nested table holding an empty w:t takes no room).
    /// </summary>
    public bool Shows => Content.Any(i => i is not (BookmarkInline or BookmarkEndInline or PermissionInline or TextInline { Text.Length: 0 }) && i.Format.Vanish != true);

    /// <summary>The tracked change of the paragraph's mark and its tracked formatting change, in the markup view.</summary>
    public RevisionMark? MarkRevision { get; init; }
    public FormatChange? Change { get; init; }

    /// <summary>The paragraph's list number or bullet (catalog: numbering.counting), or null.</summary>
    public ListLabel? Label { get; init; }

    /// <summary>
    /// Where the paragraph goes on in a linked text box (the Line.TextStart of the first line the box before had no room
    /// for), laid out from there without its space before; null for the whole paragraph.
    /// </summary>
    public int? ResumeAt { get; init; }

    /// <summary>
    /// How many characters of the paragraph's text (ParagraphText) come before its content: 0, 1 for a copy set
    /// without the page or column break that began it.
    /// </summary>
    public int TextOffset { get; init; }
}

/// <summary>A table (catalog: table.structure); laid out by the table engine.</summary>
public sealed record TableBlock(XElement Element, TableModel Table) : Block;

/// <summary>Page and section properties of a section (w:sectPr, catalog: section.page, section.columns).</summary>
public sealed class SectionProperties
{
    public double PageWidth = 612, PageHeight = 792;
    public double Top = 72, Bottom = 72, Left = 72, Right = 72, Header = 36, Footer = 36, Gutter;
    public bool TopFixed, BottomFixed;
    public string Start = "nextPage";
    public int Columns = 1;
    public double ColumnSpace = 36;
    public bool ColumnSeparator;
    public List<(double Width, double Space)>? ColumnWidths;
    public string VerticalAlign = "top";
    public XElement? Element;

    /// <summary>The direction of the section's text (w:textDirection): lrTb, tbRl, btLr, tbRlV, tbLrV or lrTbV.</summary>
    public string TextDirection = "lrTb";

    /// <summary>The section as printed, where this is it turned for laying out vertical text (<see cref="Turned"/>), else null.</summary>
    public SectionProperties? Upright;

    /// <summary>
    /// Whether the section's lines run down its pages (measured with docx/local/vertical2: tbRl and btLr stack them from
    /// the right, tbRlV and tbLrV from the left; lrTbV keeps them across).
    /// </summary>
    public bool Vertical => TextDirection is "tbRl" or "btLr" or "tbRlV" or "tbLrV";

    public bool LinesFromLeft => TextDirection is "tbRlV" or "tbLrV";

    /// <summary>
    /// Whether the section's East Asian characters are turned against its lines (measured with docx/local/vertical2):
    /// upright in tbRl, tbRlV and tbLrV, lying in lrTbV; btLr turns all text with its lines.
    /// </summary>
    public bool TurnEastAsian => TextDirection is "tbRl" or "tbRlV" or "tbLrV" or "lrTbV";

    /// <summary>
    /// The section turned a quarter for laying out its vertical text: the page's height across, its lines running from
    /// the top margin to the bottom one, the first at the right margin (at the left one where they stack from the left;
    /// the pages are mirrored after their layout). The gutter is left out.
    /// </summary>
    public SectionProperties Turned()
    {
        var turned = (SectionProperties)MemberwiseClone();
        turned.Upright = this;
        (turned.PageWidth, turned.PageHeight) = (PageHeight, PageWidth);
        (turned.Left, turned.Right) = (Top, Bottom);
        (turned.Top, turned.Bottom) = LinesFromLeft ? (Left, Right) : (Right, Left);
        (turned.TopFixed, turned.BottomFixed, turned.Gutter) = (false, false, 0);
        return turned;
    }

    /// <summary>The header and footer parts the section names, by type: default, first, even (catalog: headerFooter.parts).</summary>
    public Dictionary<string, string> Headers = new(StringComparer.Ordinal), Footers = new(StringComparer.Ordinal);

    /// <summary>Whether the section's first page has a header and footer of its own (w:titlePg).</summary>
    public bool TitlePage;

    /// <summary>The page numbering of the section (w:pgNumType): the number of its first page, or null to go on, and the format.</summary>
    public int? PageNumberStart;
    public string PageNumberFormat = "decimal";

    /// <summary>
    /// Chapter numbers before page numbers (w:pgNumType w:chapStyle, w:chapSep): the heading level whose number they
    /// show, or null, and the separator.
    /// </summary>
    public int? ChapterLevel;
    public string ChapterSeparator = "-";

    /// <param name="resolve">Finds the part a relationship id of the main document points to.</param>
    /// <param name="culture">The reader's regional format, which picks Word's Normal template (see below).</param>
    /// <remarks>
    /// What the document leaves out comes from Word's Normal template (measured on German Word with
    /// docx/local/nosect*): without w:sectPr its paper, A4; with one but without w:pgSz Letter; margins it leaves out
    /// 2.5 cm, the bottom 2 cm, header and footer 0.5 in. Regions measuring in inches get Word's US template: Letter
    /// and 1 inch margins.
    /// </remarks>
    public static SectionProperties Read(XElement? sectPr, Func<string, string?>? resolve = null, System.Globalization.CultureInfo? culture = null)
    {
        var s = new SectionProperties { Element = sectPr };
        if (Metric(culture))
        {
            (s.Top, s.Bottom, s.Left, s.Right) = (70.85, 56.7, 70.85, 70.85);
            if (sectPr == null)
                (s.PageWidth, s.PageHeight) = (595.3, 841.9);
        }
        if (sectPr == null)
            return s;
        foreach (var (name, into) in new[] { ("headerReference", s.Headers), ("footerReference", s.Footers) })
        {
            foreach (var reference in sectPr.Elements(Wml.W + name))
            {
                // A type Word does not know is the default one, where none is given besides (docx/external/poi/60293:
                // type="odd" with even and odd headers).
                if (reference.Attr(Wml.R + "id") is { } id && resolve?.Invoke(id) is { } part)
                {
                    var type = reference.Attr("type") ?? "default";
                    if (type is "even" or "first" or "default")
                        into[type] = part;
                    else
                        into.TryAdd("default", part);
                }
            }
        }
        s.TitlePage = sectPr.Child("titlePg").OnOff() == true;
        var numbering = sectPr.Child("pgNumType");
        s.PageNumberStart = (int?)numbering.Number("start");
        s.PageNumberFormat = numbering.Attr("fmt") ?? "decimal";
        s.ChapterLevel = (int?)numbering.Number("chapStyle") is { } chapter and >= 1 and <= 9 ? chapter : null;
        s.ChapterSeparator = numbering.Attr("chapSep") switch
        {
            "period" => ".", "colon" => ":", "emDash" => "\u2014", "enDash" => "\u2013", _ => "-",
        };
        var size = sectPr.Child("pgSz");
        s.PageWidth = size.Twips("w") ?? s.PageWidth;
        s.PageHeight = size.Twips("h") ?? s.PageHeight;
        var margins = sectPr.Child("pgMar");
        // A negative top or bottom margin is kept whatever the header or footer needs (ISO/IEC 29500 17.6.11).
        var top = margins.Twips("top") ?? s.Top;
        var bottom = margins.Twips("bottom") ?? s.Bottom;
        (s.Top, s.TopFixed) = (Math.Abs(top), top < 0);
        (s.Bottom, s.BottomFixed) = (Math.Abs(bottom), bottom < 0);
        s.Left = margins.Twips("left") ?? s.Left;
        s.Right = margins.Twips("right") ?? s.Right;
        s.Header = margins.Twips("header") ?? s.Header;
        s.Footer = margins.Twips("footer") ?? s.Footer;
        s.Gutter = margins.Twips("gutter") ?? 0;
        s.Start = sectPr.Child("type").Val() ?? "nextPage";
        var cols = sectPr.Child("cols");
        s.Columns = Math.Max(1, (int)(cols.Number("num") ?? 1));
        s.ColumnSpace = cols.Twips("space") ?? 36;
        s.ColumnSeparator = cols.Attr("sep") is "1" or "true" or "on";
        if (cols?.Attr("equalWidth") is "0" or "false" or "off" && cols.Elements(Wml.W + "col").Any())
            s.ColumnWidths = cols.Elements(Wml.W + "col").Select(c => (c.Twips("w") ?? 0, c.Twips("space") ?? 0)).ToList();
        s.VerticalAlign = sectPr.Child("vAlign").Val() ?? "top";
        s.TextDirection = sectPr.Child("textDirection").Val() ?? "lrTb";
        return s;
    }

    /// <summary>Whether a culture's region measures in centimeters (Word's template then is A4 with metric margins).</summary>
    static bool Metric(System.Globalization.CultureInfo? culture)
    {
        if (culture == null || string.IsNullOrEmpty(culture.Name))
            return false;
        try
        {
            return new System.Globalization.RegionInfo(culture.Name).IsMetric;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

/// <summary>
/// Reads the body of the main document into blocks with resolved formatting (catalog: document.body): paragraphs with
/// their runs, breaks and tabs; content controls, custom XML, smart tags, hyperlinks and insertions are transparent;
/// deletions and the instructions of fields are left out, field results stay (catalog: field.structure,
/// review.revisions: the document as it reads with all changes accepted).
/// </summary>
public sealed partial class ModelReader(DocumentContext context)
{
    /// <summary>The blocks read so far, which number them (Block.Id; not in their document's order: a table's cells come first).</summary>
    int blocksRead;

    /// <summary>
    /// The formats of the runs and marks read so far, one of each (see Shared): a document's runs have few formats between
    /// them, and a format the model holds is not changed once it is read.
    /// </summary>
    readonly Dictionary<RunFormat, RunFormat> formats = new(new SharedFormats());

    /// <summary>
    /// For each format of formats, the copies of it that further runs of one paragraph take where more than one of them
    /// has it (see taken).
    /// </summary>
    readonly Dictionary<RunFormat, List<RunFormat>> copies = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// How many runs of the paragraph being read took each format of formats, null outside paragraphs: the layout tells
    /// a paragraph's runs apart by their formats (a piece of a line is one run's, see ParagraphLayout.LayoutLines), so
    /// each run of a paragraph keeps a format of its own, as each read its own before formats were shared, among the
    /// runs of the paragraphs before it too where they run into it (their marks hidden or deleted, see JoinHidden).
    /// </summary>
    Dictionary<RunFormat, int>? taken;

    /// <summary>What the paragraph read last took (see taken), where its mark is hidden or deleted: it runs into the next one.</summary>
    Dictionary<RunFormat, int>? runsOn;

    /// <summary>
    /// A run's or mark's format as it was read before where an equal one was (see formats), else this one; in a paragraph
    /// a copy of it where another run of the paragraph took it (see taken); outside paragraphs this one.
    /// </summary>
    RunFormat Shared(RunFormat format)
    {
        if (taken == null)
            return format;
        if (!formats.TryGetValue(format, out var shared))
            formats[shared = format] = format;
        var before = taken.GetValueOrDefault(shared);
        taken[shared] = before + 1;
        if (before == 0)
            return shared;
        if (!copies.TryGetValue(shared, out var made))
            copies[shared] = made = [];
        while (made.Count < before)
        {
            var copy = shared.Clone();
            copy.Origin = shared.Origin is { } origin ? origin with { Resolved = copy } : null;
            made.Add(copy);
        }
        return made[before - 1];
    }

    /// <summary>The format of a separator's paragraph: single spacing, no space before or after (a copy, as formats are shared).</summary>
    ParagraphFormat SeparatorFormat(ParagraphFormat format)
    {
        var f = format.Clone();
        (f.Before, f.After, f.BeforeLines, f.AfterLines, f.BeforeAuto, f.AfterAuto, f.Line, f.LineRule) = (0, 0, null, null, false, false, 240, "auto");
        return Shared(f);
    }

    /// <summary>The paragraphs' formats read so far, one of each, as formats holds the runs' (see Shared).</summary>
    readonly Dictionary<ParagraphFormat, ParagraphFormat> paragraphFormats = new(new SharedParagraphFormats());

    ParagraphFormat Shared(ParagraphFormat format)
    {
        if (paragraphFormats.TryGetValue(format, out var shared))
            return shared;
        paragraphFormats[format] = format;
        return format;
    }

    sealed class SharedParagraphFormats : IEqualityComparer<ParagraphFormat>
    {
        public bool Equals(ParagraphFormat? a, ParagraphFormat? b) => ReferenceEquals(a, b) || a != null && b != null && FormatValues<ParagraphFormat>.Equal(a, b);

        public int GetHashCode(ParagraphFormat format) => FormatValues<ParagraphFormat>.Hash(format);
    }

    /// <summary>
    /// Formats equal where their values are and they were resolved from equal values (RunFormat.Origin: the run's own
    /// properties, its character style and the table level by reference), which reading them anew under another
    /// paragraph style resolves the same (see Restyled).
    /// </summary>
    sealed class SharedFormats : IEqualityComparer<RunFormat>
    {
        public bool Equals(RunFormat? a, RunFormat? b) =>
            ReferenceEquals(a, b) || a != null && b != null && FormatValues<RunFormat>.Equal(a, b) && (a.Origin, b.Origin) switch
            {
                (null, null) => true,
                ({ } x, { } y) => FormatValues<RunFormat>.Equal(x.Direct, y.Direct) && x.CharacterStyle == y.CharacterStyle && ReferenceEquals(x.Table, y.Table),
                _ => false,
            };

        // With where they were resolved from: formats of equal values resolved in many tables would else share a hash.
        public int GetHashCode(RunFormat format) => format.Origin is { } origin
            ? HashCode.Combine(FormatValues<RunFormat>.Hash(format), FormatValues<RunFormat>.Hash(origin.Direct), origin.CharacterStyle,
                origin.Table is { } table ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(table) : 0)
            : FormatValues<RunFormat>.Hash(format);
    }

    /// <summary>The last id given a block (Block.Id); a reader set to another's numbers its blocks after it.</summary>
    public int LastId
    {
        get => blocksRead;
        init => blocksRead = value;
    }

    /// <summary>The tracked change whose content is being read, in the markup view (catalog: review.revisions), or null.</summary>
    RevisionMark? revision;

    /// <summary>The list counters of the story being read: the body and every header, footer and note count on their own.</summary>
    ListCounter counter = new(context.Numbering);

    /// <summary>The part being read, whose relationships the drawings' pictures resolve against.</summary>
    string currentPart = context.MainPart;

    /// <summary>The hyperlink (w:hyperlink) whose runs are being read, or null.</summary>
    string? link;

    /// <summary>
    /// Whether the runs being read are a hyperlink's in a table of contents, which do not show the Hyperlink style:
    /// Word prints TOC entries in w:hyperlink as if their runs had no character style where it is the Hyperlink
    /// style, their direct formatting and other character styles kept, with or without TOC \h (measured with
    /// docx/local/toclink, toclink-2: a Hyperlink style of blue, underlined, bold 16 pt shows none of it there,
    /// outside w:hyperlink, outside the field and in a HYPERLINK field it does; docx/external/libreoffice/
    /// extras-ooxmlexport/tdf126994_lostPageBreak).
    /// </summary>
    bool tocLink;

    /// <summary>
    /// The complex fields open at the runs being read, across paragraphs (a FieldState is a paragraph's): their
    /// instructions and whether their results are being read; a table of contents spans its entries' paragraphs.
    /// </summary>
    readonly List<(System.Text.StringBuilder Instruction, bool InResult)> openFields = [];

    /// <summary>How many simple fields (w:fldSimple) and hyperlinks (w:hyperlink) are around the runs being read.</summary>
    int simpleFields;

    /// <summary>Whether the runs being read are in a field: a complex or a simple field, a hyperlink.</summary>
    bool InField => simpleFields > 0 || openFields.Count > 0;

    /// <summary>The directions of the w:bdo and w:dir around the runs being read, or null.</summary>
    string? bidiOverride, bidiEmbedding;

    /// <summary>
    /// The content of the first content control showing its placeholder of each docPart, and whether it is a block
    /// control's (see <see cref="Shown"/>).
    /// </summary>
    readonly Dictionary<string, (XElement Content, bool Block)> placeholders = new(StringComparer.Ordinal);

    /// <summary>
    /// What a content control shows: in every control showing its placeholder (w:showingPlcHdr) Word shows the content
    /// of the first such control of the same docPart in the document, a block control's paragraphs in later block
    /// controls and its first paragraph's runs in inline ones; a block control after an inline one keeps its own
    /// (measured with docx/local/sph-*: controls of "Alpha one", "Bravo two" and "Charlie three" all show "Alpha one",
    /// in one paragraph or three, plain or rich text, two block controls and an inline one "Block one"; controls not
    /// showing their placeholder take no part, nor those of another docPart or of none; docx/external/poi/55966).
    /// </summary>
    XElement Shown(XElement sdt, XElement content, bool block)
    {
        if (sdt.Child("sdtPr") is not { } properties || properties.Child("showingPlcHdr") == null
            || properties.Child("placeholder").Child("docPart").Attr("val") is not { } part)
            return content;
        if (!placeholders.TryGetValue(part, out var first))
        {
            placeholders[part] = (content, block);
            return content;
        }
        if (block)
            return first.Block ? first.Content : content;
        return first.Block ? first.Content.Descendants(Wml.W + "p").FirstOrDefault() ?? content : first.Content;
    }

    /// <summary>How deep the table being read is nested, and how deep tables may nest before only their paragraphs are
    /// read (a guard for the stack: Word itself does not open a test document with 5000 nested tables).</summary>
    int tableDepth;
    const int MaxTableDepth = 64;

    /// <summary>The values of data-bound content controls (catalog: sdt.dataBinding).</summary>
    DataBinding? binding;
    DataBinding Binding => binding ??= new DataBinding(context);

    /// <summary>The section (its w:sectPr and its index) of every element of the body, and of the one being read.</summary>
    readonly Dictionary<XElement, (XElement? Properties, int Index)> sectionOf = [];
    (XElement? Properties, int Index) currentSection;
    XElement? bodyElement;

    /// <summary>The body's blocks, read whole (see BeginBody).</summary>
    public List<Block> Body()
    {
        var blocks = BeginBody();
        while (ReadBody(int.MaxValue))
        {
        }
        return blocks;
    }

    /// <summary>
    /// Begins to read the body in portions, as Body reads it whole: the list its blocks go into, which ReadBody fills,
    /// complete once it is done (the paragraphs whose marks are hidden joined, see JoinHidden). With
    /// <paramref name="children"/> only so many of its first children (for its first pages), those after the last
    /// section break among them in the section the next break ends.
    /// </summary>
    public List<Block> BeginBody(int children = int.MaxValue)
    {
        var body = context.Document.Root.Child("body");
        var blocks = new List<Block>();
        counter = new ListCounter(context.Numbering);
        currentPart = context.MainPart;
        bodyReading = null;
        if (body != null)
        {
            // Each section's properties stand at its end.
            bodyElement = body;
            var pending = new List<XElement>();
            var index = 0;
            XElement? before = null;
            childBefore.Clear();
            foreach (var e in body.Elements().Take(children))
            {
                childBefore[e] = before;
                before = e;
                pending.Add(e);
                if (e.Name == Wml.W + "p" && e.Child("pPr").Child("sectPr") is { } properties)
                {
                    foreach (var p in pending)
                        sectionOf[p] = (properties, index);
                    pending.Clear();
                    index++;
                }
            }
            // Where the body is still being read (see DocumentContext.Loading), the section ahead of its children so far.
            var last = (children < int.MaxValue ? body.Elements().Skip(children) : []).Select(e => e.Name == Wml.W + "p" ? e.Child("pPr").Child("sectPr") : null)
                .FirstOrDefault(properties => properties != null) ?? body.Child("sectPr") ?? context.SectionAhead();
            foreach (var p in pending)
                sectionOf[p] = (last, index);
            currentSection = sectionOf.Count > 0 ? sectionOf[body.Elements().First()] : (body.Child("sectPr"), 0);
            BodyChildren = childBefore.Count;
            bodyReading = (body, blocks, JoinedFields(SplitTables(body.Elements().Take(children))).GetEnumerator(), null);
        }
        return blocks;
    }

    /// <summary>
    /// The body being read in portions (see BeginBody): its element, its blocks so far, the children left to read (as
    /// ReadBlocks reads them) and a paragraph whose mark is gone, waiting for the next.
    /// </summary>
    (XElement Body, List<Block> Blocks, IEnumerator<XElement> Children, ParagraphBlock? Pending)? bodyReading;

    /// <summary>How many children the body read last has (see BeginBody).</summary>
    public int BodyChildren { get; private set; }

    /// <summary>Reads so many more children of the body (see BeginBody); whether any are left.</summary>
    public bool ReadBody(int children)
    {
        if (bodyReading is not var (body, blocks, rest, pending))
            return false;
        for (var k = 0; k < children; k++)
        {
            if (!rest.MoveNext())
            {
                rest.Dispose();
                bodyReading = null;
                if (pending != null)
                    blocks.Add(pending);
                JoinHidden(blocks, cell: false);
                Tops(blocks);
                return false;
            }
            ReadBlock(body, rest.Current, blocks, ref pending);
        }
        bodyReading = (body, blocks, rest, pending);
        return true;
    }

    /// <summary>The properties of the section the body's reading stands in (see ReadBody).</summary>
    public SectionProperties ReadingSection() => SectionProperties.Read(currentSection.Properties, Resolve, context.Culture);

    /// <summary>
    /// The text of a w:t: without xml:space="preserve" on the element itself or on its part's root Word drops the
    /// spaces, tabs and line ends at its ends and shows tabs inside as spaces, keeping the spaces inside (measured with
    /// docx/local/ws-1 to ws-5: "a", " " and "b" show "ab", "a " and "b" too, "a  b" keeps both spaces, a no-break
    /// space stays, "a\tb" shows "a b"; xml:space on w:body, the paragraph or the run does not count, on w:document it
    /// does; docx/external/libreoffice/extras-ooxmlimport/xml_space, tdf124670).
    /// </summary>
    static string Text(XElement t)
    {
        var space = t.Attribute(XNamespace.Xml + "space")?.Value ?? t.Document?.Root?.Attribute(XNamespace.Xml + "space")?.Value;
        var value = Value(t);
        return space == "preserve" ? value : value.Trim(' ', '\t', '\r', '\n').Replace('\t', ' ');
    }

    /// <summary>
    /// An element's text: of one read with line info the string of its one text node where it has one, which the model
    /// then shares with the XML where XElement.Value would copy it (an element read without line info holds its text as
    /// such a string already, which reading its nodes would make a node of).
    /// </summary>
    static string Value(XElement e) =>
        ((System.Xml.IXmlLineInfo)e).HasLineInfo() && e.FirstNode is XText text && text.NextNode == null ? text.Value : e.Value;

    /// <summary>
    /// Paragraphs whose mark is hidden (w:vanish on the mark, catalog: run.hidden) as Word prints them (measured with
    /// docx/local/hiddenmark): such a paragraph runs into the next one, the joined paragraph in its own formatting
    /// (the first's alignment and space before); one whose text is all hidden too is gone; before a table it stays,
    /// without its space after; a cell's last paragraph stays even when all of it is hidden, the body's goes.
    /// </summary>
    static void JoinHidden(List<Block> blocks, bool cell)
    {
        for (var k = 0; k < blocks.Count; k++)
        {
            if (blocks[k] is not ParagraphBlock { MarkFormat.Vanish: true, EndsSection: null } paragraph)
                continue;
            // One with nothing to show is gone, the next keeping its own formatting (measured with docx/local/hmp-1,
            // hmp-2: an empty paragraph with a hidden mark, centered and shaded yellow, leaves "B" its 24 pt before and
            // no shading, after text or a table; docx/external/libreoffice/extras-ooxmlexport/cell-grid-span). Before a
            // table it is gone as well (docx/external/poi/bug59058: an HTML form's hidden "Top of Form").
            if (k + 1 < blocks.Count && !paragraph.Shows)
            {
                blocks.RemoveAt(k);
                k--;
                continue;
            }
            if (k + 1 < blocks.Count && blocks[k + 1] is ParagraphBlock next)
            {
                blocks[k] = paragraph with
                {
                    Content = [.. paragraph.Content, .. next.Content], MarkFormat = next.MarkFormat, EndsSection = next.EndsSection,
                    Parts = [.. paragraph.Parts ?? [paragraph.Element], .. next.Parts ?? [next.Element]],
                };
                blocks.RemoveAt(k + 1);
                k--;
            }
            else if (k + 1 < blocks.Count)
            {
                var format = paragraph.Format.Clone();
                (format.After, format.AfterLines, format.AfterAuto) = (0, null, false);
                blocks[k] = paragraph with { Format = format };
            }
            else if (!cell && !paragraph.Shows)
                blocks.RemoveAt(k);
        }
    }

    /// <summary>The properties of the document's last section (the body's own w:sectPr).</summary>
    public SectionProperties LastSection() =>
        SectionProperties.Read(context.Document.Root.Child("body") is { } body ? body.LastNode as XElement is { } last && last.Name == Wml.W + "sectPr" ? last : body.Child("sectPr") : null,
            Resolve, context.Culture);

    /// <summary>The part a relationship id of the main document points to, where it exists.</summary>
    string? Resolve(string id) =>
        context.Package.Relationship(context.MainPart, id) is { External: false } rel && context.Package.Exists(rel.Target) ? rel.Target : null;

    readonly Dictionary<string, List<Block>> stories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The numbers and marks of footnotes and endnotes in the order their references stand in the document.</summary>
    NoteNumbers? noteNumbers;
    NoteNumbers NoteNumbers => noteNumbers ??= new NoteNumbers(context.Settings);

    /// <summary>An endnote's number (the order of its reference), or 0 where nothing refers to it.</summary>
    public int EndnoteNumber(string id) => NoteNumbers.Endnotes.GetValueOrDefault(id);

    /// <summary>The index of the section that refers to an endnote.</summary>
    public int EndnoteSection(string id) => NoteNumbers.EndnoteSections.GetValueOrDefault(id);

    /// <summary>What each story asked for numbers (see NumberStep), in order, with the answers, by its part.</summary>
    readonly Dictionary<string, List<(NumberStep Step, object? Answer)>> numberSteps = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The numbers a story's paragraphs, fields and note references asked for, in order, with what they were answered.</summary>
    public IReadOnlyList<(NumberStep Step, object? Answer)> NumberSteps(string part) => numberSteps.GetValueOrDefault(part) ?? [];

    /// <summary>The parts whose stories asked for numbers, in the order they first asked.</summary>
    public IEnumerable<string> NumberedParts => numberSteps.Keys;

    /// <summary>
    /// Asks the story's list counter or the document's note numbers a step, recording it with its answer; a paragraph of
    /// no list (see ListCounter.Counts) asks nothing, so that paragraphs without numbers come and go without steps.
    /// </summary>
    object? Ask(NumberStep step)
    {
        if (step is ListStep list && !counter.Counts(list.List, list.Level))
            return null;
        // A re-read records its steps apart and takes their answers from the body's steps replayed (see Reread).
        if (rereadSteps != null)
        {
            var given = rereadAnswers != null ? rereadAnswers.Dequeue() : Numbers.Answer(step, counter, NoteNumbers);
            rereadSteps.Add((step, given));
            return given;
        }
        var answer = Numbers.Answer(step, counter, NoteNumbers);
        if (!numberSteps.TryGetValue(currentPart, out var steps))
            numberSteps[currentPart] = steps = [];
        steps.Add((step, answer));
        return answer;
    }

    /// <summary>The number of the note being read, which its w:footnoteRef or w:endnoteRef shows, and its id.</summary>
    string? noteMark, noteId;

    /// <summary>
    /// The footnotes or endnotes of the document (catalog: note.footnotes, note.endnotes) by id, read after the
    /// body: their numbers come from the order of their references; separators are read under their types.
    /// </summary>
    /// <summary>The comments of the comments part by id, read once.</summary>
    Dictionary<string, XElement>? commentElements;

    /// <summary>The comments whose ranges the body has, in their order (Word numbers comments so).</summary>
    readonly List<string> commentOrder = [];

    string? CommentsPart => context.Package.FirstRelationship(context.MainPart, "comments")?.Target;

    XElement? CommentElement(string id)
    {
        if (commentElements == null)
        {
            commentElements = new(StringComparer.Ordinal);
            if (CommentsPart is { } part && context.Package.Exists(part) && context.Load(part).Root is { } root)
            {
                foreach (var comment in root.Elements(Wml.W + "comment"))
                    commentElements.TryAdd(comment.Attr("id") ?? "", comment);
            }
        }
        return commentElements.GetValueOrDefault(id);
    }

    /// <summary>
    /// A comment's range begins in the body (markup view): the comment takes the next number and its author a place
    /// among the document's authors.
    /// </summary>
    void CommentAnchored(string id)
    {
        if (commentOrder.Contains(id))
        {
            // Its author named again where the authors read are noted (see DocumentContext.Named).
            context.Named?.Add(CommentElement(id)?.Attr("author") ?? "");
            return;
        }
        commentOrder.Add(id);
        context.Author(CommentElement(id)?.Attr("author"));
    }

    /// <summary>
    /// The comments of the body's ranges (catalog: review.comments), read after the body: each with its author, its
    /// label (the author's initials and the comment's number in the order of the ranges, "[A11]") and its content.
    /// </summary>
    public Dictionary<string, Comment> Comments()
    {
        var comments = new Dictionary<string, Comment>(StringComparer.Ordinal);
        if (CommentsPart is not { } part)
            return comments;
        // Replies and resolved comments (w15:commentsEx): a comment is known by its last paragraph's w14:paraId.
        XNamespace w14 = "http://schemas.microsoft.com/office/word/2010/wordml", w15 = "http://schemas.microsoft.com/office/word/2012/wordml";
        var extended = new Dictionary<string, (string? Parent, bool Done)>(StringComparer.OrdinalIgnoreCase);
        if (context.Package.FirstRelationship(context.MainPart, "commentsExtended")?.Target is { } extendedPart && context.Package.Exists(extendedPart))
        {
            foreach (var e in context.Load(extendedPart).Root?.Elements(w15 + "commentEx") ?? [])
            {
                if ((string?)e.Attribute(w15 + "paraId") is { } paraId)
                    extended[paraId] = ((string?)e.Attribute(w15 + "paraIdParent"), (string?)e.Attribute(w15 + "done") is "1" or "true");
            }
        }
        string? ParaId(string id) => (string?)CommentElement(id)?.Elements(Wml.W + "p").LastOrDefault()?.Attribute(w14 + "paraId");
        var numbers = commentOrder.Select((id, k) => (id, k + 1)).Where(p => ParaId(p.id) != null).ToDictionary(p => ParaId(p.id)!, p => p.Item2, StringComparer.OrdinalIgnoreCase);
        var (savedCounter, savedPart) = (counter, currentPart);
        counter = new ListCounter(context.Numbering);
        currentPart = part;
        for (var k = 0; k < commentOrder.Count; k++)
        {
            if (CommentElement(commentOrder[k]) is not { } element)
                continue;
            var blocks = new List<Block>();
            ReadBlocks(element, blocks);
            JoinHidden(blocks, cell: false);
            var (parent, done) = ParaId(commentOrder[k]) is { } paraId && extended.TryGetValue(paraId, out var ex) ? ex : (null, false);
            var answers = parent != null && numbers.TryGetValue(parent, out var number) ? $"R{number}" : "";
            comments[commentOrder[k]] = new Comment(commentOrder[k], context.Author(element.Attr("author")), $"[{element.Attr("initials")}{k + 1}{answers}]", blocks)
            {
                Reply = answers.Length > 0,
                Done = done,
            };
        }
        (counter, currentPart) = (savedCounter, savedPart);
        return comments;
    }

    public Dictionary<string, List<Block>> Notes(bool endnotes)
    {
        var notes = new Dictionary<string, List<Block>>(StringComparer.Ordinal);
        var part = context.Package.FirstRelationship(context.MainPart, endnotes ? "endnotes" : "footnotes")?.Target;
        if (part == null || !context.Package.Exists(part) || context.Load(part).Root is not { } root)
            return notes;
        var (savedCounter, savedPart) = (counter, currentPart);
        counter = new ListCounter(context.Numbering);
        currentPart = part;
        foreach (var note in root.Elements(Wml.W + (endnotes ? "endnote" : "footnote")))
        {
            var id = note.Attr("id") ?? "";
            var type = note.Attr("type") ?? "normal";
            noteMark = type == "normal" && NoteNumbers.Marks.TryGetValue((endnotes, id), out var mark) ? mark : "";
            noteId = id;
            var blocks = new List<Block>();
            ReadBlocks(note, blocks);
            JoinHidden(blocks, cell: false);
            // A note without paragraphs takes the room of an empty one (measured with docx/local/fnempty-*: the
            // separator of an empty note stands where a note of one line puts it, 0, 8 and 20 pt of space after
            // included; docx/external/powertools/WC/WC064-Footnote).
            if (blocks.Count == 0 && type == "normal")
                blocks.Add(Paragraph(new XElement(Wml.W + "p")));
            // The paragraph of a separator's line stands at single spacing without space before or after, whatever it
            // and the defaults say (measured with docx/local/fsep-*: before 12, after 0, 10 and 20 pt, double spacing
            // all put the separator 16.2 pt above the note's baseline; docx/external/pandoc/golden/notes); text
            // paragraphs of a separator keep theirs (libreoffice/extras-ooxmlexport/tdf123262_textFootnoteSeparators).
            // A separator of several paragraphs keeps the spacing of its line's paragraph too (found by agent BE in
            // Word's PDFs: docx/external/libreoffice/extras-ooxmlexport/testFootnote, a separator and an empty
            // paragraph at 1.5 lines and 10 pt after, the line 17.3 pt higher; tdf123262, three paragraphs), and so
            // does one the settings list among the special notes (w:footnotePr/w:footnote; measured with
            // docx/local/fsr-*: Normal's 8 pt after and 259/240 lines, its 12 pt before and 3 pt after, the defaults'
            // spacing and 20 pt after of its own put the line 25.2, 31.2, 25.2 and 36.2 pt above the note's
            // baseline, listed by the ids -1 and 0 or 0 and 1; not listed 16.2, its own spacing too; found by an agent
            // in Word's PDFs of LibreOffice's extras-uiwriter/tdf104649, tdf126723).
            var listed = context.Settings.Child(endnotes ? "endnotePr" : "footnotePr")?.Elements(Wml.W + (endnotes ? "endnote" : "footnote"))
                .Any(e => e.Attr("id") == id) == true;
            if (type is "separator" or "continuationSeparator" && blocks.Count == 1 && !listed)
            {
                for (var k = 0; k < blocks.Count; k++)
                {
                    if (blocks[k] is ParagraphBlock paragraph && paragraph.Element.Descendants().Any(e => e.Name.LocalName is "separator" or "continuationSeparator"))
                        blocks[k] = paragraph with { Format = SeparatorFormat(paragraph.Format) };
                }
            }
            // The continuation notice counts only where the settings list it among the special notes (see
            // Paginator.SeparatorHeight).
            if (type == "continuationNotice" && context.Settings.Child(endnotes ? "endnotePr" : "footnotePr")?
                    .Elements(Wml.W + (endnotes ? "endnote" : "footnote")).Any(e => e.Attr("id") == id) != true)
                continue;
            notes[type == "normal" ? id : type] = blocks;
        }
        // A notes part without separators has Word's own (docx/external/libreoffice/extras-ooxmlexport/tdf152203: notes
        // of type normal alone, a 144 pt line above them on every page).
        foreach (var kind in new[] { "separator", "continuationSeparator" })
        {
            if (notes.ContainsKey(kind) || !notes.Keys.Any(k => k is not ("separator" or "continuationSeparator")))
                continue;
            var blocks = new List<Block>();
            ReadBlocks(new XElement(Wml.W + (endnotes ? "endnote" : "footnote"), new XElement(Wml.W + "p", new XElement(Wml.W + "r", new XElement(Wml.W + kind)))), blocks);
            for (var k = 0; k < blocks.Count; k++)
            {
                if (blocks[k] is ParagraphBlock paragraph)
                    blocks[k] = paragraph with { Format = SeparatorFormat(paragraph.Format) };
            }
            notes[kind] = blocks;
        }
        noteMark = null;
        (counter, currentPart) = (savedCounter, savedPart);
        return notes;
    }

    /// <summary>
    /// The list counter the header and footer stories share (see Story), and the list and level of the Normal style
    /// where it is numbered.
    /// </summary>
    ListCounter? storyCounter;
    (string? List, int Level)? normalList;

    /// <summary>
    /// The blocks of a header or footer part (catalog: headerFooter.parts), read once. Their lists count on from story
    /// to story in the order they are first read, which the paginator makes Word's: section by section the even header,
    /// the default header, the even footer, the default footer, the first page's header and footer. Each story ends with
    /// a mark of the Normal style, and eight more come before the first: where Normal is numbered, each takes a number
    /// (measured with docx/local/hfn-*: a header's item 1 and a footer's of the same list 2, with a header of two items
    /// 3, a list of its own in the footer 1; Normal numbered, the default header's paragraph 9 and the default
    /// footer's 11, with an even header and footer before them 11 and 15, a second section's 13 and 15, a header
    /// paragraph of numId 0 leaves the footer 10; the notes' separators and their numbers count for none;
    /// docx/external/libreoffice/extras-ooxmlexport/tdf135164_cancelledNumbering: "xi" and "xv").
    /// </summary>
    public List<Block> Story(string part)
    {
        if (stories.TryGetValue(part, out var blocks))
            return blocks;
        blocks = [];
        var (bodyCounter, bodyPart) = (counter, currentPart);
        if (storyCounter == null)
        {
            storyCounter = new ListCounter(context.Numbering);
            var normal = context.Styles.Paragraph(null, new ParagraphFormat(), table: null);
            if (storyCounter.Counts(normal.NumberingId, normal.NumberingLevel ?? 0))
            {
                normalList = (normal.NumberingId, normal.NumberingLevel ?? 0);
                for (var k = 0; k < 8; k++)
                    storyCounter.Next(normal.NumberingId, normal.NumberingLevel ?? 0);
            }
        }
        counter = storyCounter;
        currentPart = part;
        if (context.Load(part).Root is { } root)
            ReadBlocks(root, blocks);
        if (normalList is var (list, level))
            storyCounter.Next(list, level);
        JoinHidden(blocks, cell: false);
        (counter, currentPart) = (bodyCounter, bodyPart);
        return stories[part] = blocks;
    }

    /// <summary>Bookmarks that start between paragraphs, for the next paragraph.</summary>
    readonly List<Inline> blockBookmarks = [];

    /// <summary>The paragraph with the bookmarks that started before it between paragraphs at its start.</summary>
    ParagraphBlock WithBlockBookmarks(ParagraphBlock paragraph)
    {
        if (blockBookmarks.Count == 0)
            return paragraph;
        var marked = paragraph with { Content = [.. blockBookmarks, .. paragraph.Content] };
        blockBookmarks.Clear();
        return marked;
    }

    /// <summary>
    /// The blocks of a container with the paragraphs a computed field runs across joined into one, as Word prints them:
    /// the text before the field, its result and the text after its end in one paragraph of the last one's properties,
    /// tables in its saved result gone (measured with docx/local/fsp-ref, fsp-miss, fsp-align: "A", the REF's
    /// "Target" or its error and " B" on one line, right aligned as the field's last paragraph; docx/external/
    /// libreoffice/extras-ooxmlexport/line-break-in-ref, tdf171299_tableInField, extras-ooxmlimport/tdf125038b,
    /// writerfilter-dmapper/if-field). Fields Word keeps the saved results of, as tables of contents, keep their
    /// paragraphs.
    /// </summary>
    /// <summary>
    /// The blocks of a container with a table inside a paragraph (w:p > w:tbl) as a table right below the
    /// paragraph's text before it, without that part's space after or the empty line of a break ending it, and the text
    /// after it with the paragraph's mark, where there is any (Word's PDF of docx/external/libreoffice/
    /// extras-ooxmlimport/tdf111550: "[outer:A2]" and a line break, then an inner table, in a cell: the inner table's
    /// text a line below, the outer row ending below the inner table).
    /// </summary>
    IEnumerable<XElement> SplitTables(IEnumerable<XElement> elements)
    {
        foreach (var e in elements)
        {
            if (e.Name != Wml.W + "p" || e.Element(Wml.W + "tbl") == null)
            {
                yield return e;
                continue;
            }
            var pPr = e.Element(Wml.W + "pPr");
            var part = new List<XElement>();
            XElement Paragraph(bool last)
            {
                var properties = pPr == null ? new XElement(Wml.W + "pPr") : new XElement(pPr);
                if (!last)
                {
                    properties.Element(Wml.W + "sectPr")?.Remove();
                    var spacing = properties.Element(Wml.W + "spacing");
                    if (spacing == null)
                        properties.Add(spacing = new XElement(Wml.W + "spacing"));
                    spacing.SetAttributeValue(Wml.W + "after", "0");
                    spacing.SetAttributeValue(Wml.W + "afterAutospacing", null);
                    if (part.LastOrDefault() is { } run && run.Name == Wml.W + "r" && run.Elements().LastOrDefault() is { } br && br.Name == Wml.W + "br"
                        && br.Attribute(Wml.W + "type") is null or { Value: "textWrapping" })
                        part[^1] = new XElement(run.Name, run.Attributes(), run.Elements().SkipLast(1));
                }
                var paragraph = new XElement(e.Name, e.Attributes(), properties, part);
                part = [];
                return paragraph;
            }
            foreach (var child in e.Elements().Where(c => c.Name != Wml.W + "pPr"))
            {
                if (child.Name == Wml.W + "tbl")
                {
                    yield return Paragraph(last: false);
                    yield return child;
                }
                else
                    part.Add(child);
            }
            if (part.Count == 0 && pPr?.Element(Wml.W + "sectPr") == null)
                continue;
            var end = Paragraph(last: true);
            if (sectionOf.TryGetValue(e, out var section))
                sectionOf[end] = section;
            yield return end;
        }
    }

    IEnumerable<XElement> JoinedFields(IEnumerable<XElement> elements)
    {
        var list = elements as IList<XElement> ?? elements.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (e.Name != Wml.W + "p" || !OpensComputedField(e))
            {
                yield return e;
                continue;
            }
            var depth = FieldDepth(e);
            var j = i + 1;
            while (j < list.Count && depth > 0)
                depth += FieldDepth(list[j++]);
            if (depth > 0 || list[j - 1].Name != Wml.W + "p")
            {
                yield return e;
                continue;
            }
            var last = list[j - 1];
            var parts = new List<object>();
            if (last.Child("pPr") is { } lastPr)
                parts.Add(new XElement(lastPr));
            for (var k = i; k < j; k++)
            {
                if (list[k].Name == Wml.W + "p")
                    parts.AddRange(list[k].Elements().Where(x => x.Name != Wml.W + "pPr").Select(x => new XElement(x)));
            }
            var joined = new XElement(Wml.W + "p", last.Attributes(), parts);
            if (sectionOf.TryGetValue(last, out var lastSection))
                sectionOf[joined] = lastSection;
            yield return joined;
            i = j - 1;
        }

        // Fields begun and not ended in an element (fldChar begin less end, nested ones too).
        static int FieldDepth(XElement element) =>
            element.Descendants(Wml.W + "fldChar").Sum(f => f.Attr("fldCharType") switch { "begin" => 1, "end" => -1, _ => 0 });

        // Whether the outermost field still open at a paragraph's end is one whose result Word computes.
        static bool OpensComputedField(XElement p)
        {
            var open = new List<System.Text.StringBuilder>();
            foreach (var node in p.Descendants())
            {
                if (node.Name == Wml.W + "fldChar")
                {
                    switch (node.Attr("fldCharType"))
                    {
                        case "begin":
                            open.Add(new System.Text.StringBuilder());
                            break;
                        case "end" when open.Count > 0:
                            open.RemoveAt(open.Count - 1);
                            break;
                    }
                }
                else if (node.Name == Wml.W + "instrText" && open.Count > 0)
                    open[^1].Append(node.Value);
            }
            return open.Count > 0 && open[0].ToString().TrimStart().Split(' ', 2)[0].ToUpperInvariant()
                is "REF" or "IF" or "NOTEREF" or "PAGEREF" or "STYLEREF";
        }
    }

    void ReadBlocks(XElement container, List<Block> blocks)
    {
        // A paragraph whose mark was deleted as a tracked change runs into the next one, which keeps its own
        // formatting and number (catalog: review.revisions; measured with docx/generated/revisions); before a table or
        // at the end it stays.
        ParagraphBlock? pending = null;
        ReadBlocks(container, blocks, ref pending);
        if (pending != null)
            blocks.Add(pending);
    }

    /// <summary>
    /// The blocks of a container, a paragraph whose mark is gone left waiting in <paramref name="pending"/>: it joins
    /// the next paragraph also across the edges of content controls and other containers of paragraphs (docx/external/
    /// powertools/RP/RP018-MoveFrom-MoveTo-CC: a paragraph moved away, alone in its content control, joins the one
    /// after the control).
    /// </summary>
    /// <summary>
    /// The text of a paragraph whose mark is gone as it joins the next one: in that paragraph's style, the runs' own
    /// formatting and character styles kept (measured with docx/local/dm-a, dm-b, dm-c: text of a Times New Roman 14
    /// style before a Normal paragraph prints in Calibri 11, bold where its runs are, Normal text before the style's
    /// paragraph in Times New Roman 14; docx/external/libreoffice/extras-ooxmlexport/tdf125894).
    /// </summary>
    List<Inline> Restyled(ParagraphBlock joining, string? style)
    {
        if (context.Styles.ParagraphStyleOf(joining.Format.Style) == context.Styles.ParagraphStyleOf(style))
            return joining.Content;
        return joining.Content.Select(inline =>
        {
            if (inline is not TextInline { Format.Origin: { } origin } text || !ReferenceEquals(origin.Resolved, text.Format))
                return inline;
            var format = context.Styles.Run(style, origin.CharacterStyle, origin.Direct, origin.Table);
            (format.Override, format.Embedding, format.Source, format.Vanish, format.Revision, format.Change)
                = (text.Format.Override, text.Format.Embedding, text.Format.Source, text.Format.Vanish, text.Format.Revision, text.Format.Change);
            format.Origin = origin with { Resolved = format };
            return text with { Format = format };
        }).ToList();
    }

    void ReadBlocks(XElement container, List<Block> blocks, ref ParagraphBlock? pending) => ReadBlocks(container, container.Elements(), blocks, ref pending);

    void ReadBlocks(XElement container, IEnumerable<XElement> elements, List<Block> blocks, ref ParagraphBlock? pending)
    {
        foreach (var e in JoinedFields(SplitTables(elements)))
            ReadBlock(container, e, blocks, ref pending);
    }

    /// <summary>An element of a container read into its blocks (see ReadBlocks).</summary>
    void ReadBlock(XElement container, XElement e, List<Block> blocks, ref ParagraphBlock? pending)
    {
        if (container == bodyElement && sectionOf.TryGetValue(e, out var section))
            currentSection = section;
        // Where reading stood before each child of the body, for reading some of them anew (see Reread).
        if (container == bodyElement && e.Parent == bodyElement)
        {
            var fresh = pending == null && openFields.Count == 0 && strayBreaks.Count == 0 && blockBookmarks.Count == 0;
            if (rereadSteps != null)
                rereadPoints[e] = (rereadSteps.Count, fresh);
            else
                bodyPoints[e] = (numberSteps.TryGetValue(currentPart, out var asked) ? asked.Count : 0, fresh);
        }
        if (e.Name.Namespace != Wml.W)
            return;
        // A table whose rows were all deleted is gone (docx/external/powertools/RA001-Tracked-Revisions-01: a moved
        // and deleted table between paragraphs whose marks were deleted too).
        if (e.Name.LocalName == "tbl" && Gone(e, context.Markup))
            return;
        // Marks of ranges between paragraphs (moves, bookmarks, comments, permissions) leave a paragraph whose mark
        // is gone waiting for the next (docx/external/powertools/RP/RP015-MoveFrom-MoveTo: a moveFromRangeEnd
        // after the paragraph a move left).
        if (pending != null && e.Name.LocalName != "p" && !e.Name.LocalName.EndsWith("RangeStart", StringComparison.Ordinal)
            && !e.Name.LocalName.EndsWith("RangeEnd", StringComparison.Ordinal)
            && e.Name.LocalName is not ("bookmarkStart" or "bookmarkEnd" or "permStart" or "permEnd" or "proofErr")
            && !(e.Name.LocalName is "sdt" && (e.Child("sdtPr") is not { } controlPr || Binding.Text(controlPr) == null)
                 || e.Name.LocalName is "customXml" or "smartTag" or "ins" or "moveTo"))
        {
            blocks.Add(pending);
            pending = null;
        }
        switch (e.Name.LocalName)
        {
            case "br":
                strayBreaks.Add(e);
                break;
            case "p" when context.Markup:
            {
                // In the markup view a paragraph whose mark was deleted stays apart, its mark marked.
                var mark = e.Child("pPr")?.Child("rPr");
                var markRevision = mark?.Child("del") is { } deleted ? new RevisionMark("del", context.Author(deleted.Attr("author")))
                    : mark?.Child("ins") is { } inserted ? new RevisionMark("ins", context.Author(inserted.Attr("author"))) : revision;
                var change = e.Child("pPr")?.Child("pPrChange") is { } pPrChange
                    ? new FormatChange(context.Describe.Paragraph(e.Child("pPr"), pPrChange.Child("pPr")), context.Author(pPrChange.Attr("author"))) : null;
                blocks.Add(WithBlockBookmarks(Paragraph(e) with { MarkRevision = markRevision, Change = change }));
                break;
            }
            case "p":
            {
                // A mark moved away (w:moveFrom) is gone as a deleted one is (docx/external/powertools/RP/
                // RP015-MoveFrom-MoveTo: the paragraph a move left joins the next).
                var markDeleted = e.Child("pPr")?.Child("rPr") is { } markRun && (markRun.Child("del") ?? markRun.Child("moveFrom")) != null;
                var paragraph = WithBlockBookmarks(Paragraph(e, number: !markDeleted));
                if (pending != null)
                {
                    paragraph = paragraph with
                    {
                        Content = [.. Restyled(pending, paragraph.Format.Style), .. paragraph.Content],
                        Parts = [.. pending.Parts ?? [pending.Element], .. paragraph.Parts ?? [paragraph.Element]],
                    };
                    pending = null;
                    // Joined so, a paragraph of contextual spacing loses its space before, whatever the styles
                    // (measured with docx/local/dmk-f, dmk-h, dmk-j, dmk-k: after a paragraph of 3.5 pt after the
                    // gap is those 3.5 pt, the joined one's 18 pt and the first one's 24 pt gone; dmk-a: without
                    // contextual spacing its own 18 pt; docx/external/libreoffice/extras-uiwriter/tdf125916).
                    if (paragraph.Format.ContextualSpacing == true)
                    {
                        var joined = paragraph.Format.Clone();
                        (joined.Before, joined.BeforeLines, joined.BeforeAuto) = (0, null, false);
                        paragraph = paragraph with { Format = Shared(joined) };
                    }
                }
                if (markDeleted)
                    pending = paragraph;
                else
                    blocks.Add(paragraph);
                break;
            }
            case "tbl" when tableDepth < MaxTableDepth:
                tableDepth++;
                try
                {
                    blocks.Add(Table(e));
                }
                finally
                {
                    tableDepth--;
                }
                break;
            case "tbl":
                foreach (var nested in e.Descendants(Wml.W + "p").Where(p => !p.Ancestors(Wml.W + "p").Any()))
                    blocks.Add(Paragraph(nested));
                break;
            case "sdt":
                if (e.Child("sdtContent") is { } content)
                {
                    var start = blocks.Count;
                    // A bound control shows its node's value in its first paragraph's formatting.
                    if (e.Child("sdtPr") is { } sdtPr && Binding.Text(sdtPr) is { } bound)
                    {
                        ReadBlocks(content, blocks);
                        if (blocks.Skip(start).OfType<ParagraphBlock>().FirstOrDefault() is { } first)
                        {
                            var format = first.Content.FirstOrDefault()?.Format ?? first.MarkFormat;
                            first.Content.Clear();
                            first.Content.Add(new TextInline(bound, format));
                        }
                    }
                    else
                        ReadBlocks(Shown(e, content, block: true), blocks, ref pending);
                }
                break;
            case "customXml" or "smartTag" or "ins" or "moveTo":
                ReadBlocks(e, blocks, ref pending);
                break;
            // A bookmark between paragraphs starts at the next one and ends after the last (measured with docx/
            // external/libreoffice/extras-ooxmlexport/tdf154478: PAGEREF finds a table of contents heading by the
            // bookmark before the first paragraph of its content control, page 3 in Word).
            case "bookmarkStart" when e.Attr("name") is { } name && !name.StartsWith("_GoBack", StringComparison.Ordinal):
                blockBookmarks.Add(new BookmarkInline(name, context.Styles.Run(null, null, null, tableRun)) { Id = e.Attr("id") });
                break;
            case "bookmarkEnd" when e.Attr("id") is { } endId && blocks.LastOrDefault() is ParagraphBlock last:
                last.Content.Add(new BookmarkEndInline(endId, last.MarkFormat));
                break;
            case "subDoc" when e.Attr(Wml.R + "id") is { } subId && context.Package.Relationship(currentPart, subId) is { } sub:
                blocks.Add(SubDocument(sub.Target));
                break;
            case "altChunk" when e.Attr(Wml.R + "id") is { } chunkId && context.Package.Relationship(currentPart, chunkId) is { External: false } chunk
                                 && context.Package.Exists(chunk.Target):
                ReadBlocks(AltChunk(chunk.Target), blocks);
                break;
        }
    }

    /// <summary>
    /// Imported content (w:altChunk, catalog: document.altChunk) as WordprocessingML, converted the way Word converts it
    /// when it opens the document (measured with docx/local/altchunk): a WordprocessingML package's body in the
    /// document's own formatting; plain text a paragraph per line in Courier New 10.5 pt without spacing; HTML through
    /// <see cref="HtmlImport"/>; RTF through <see cref="RtfImport"/>.
    /// </summary>
    XElement AltChunk(string part)
    {
        var container = new XElement(Wml.W + "body");
        var bytes = context.Package.ReadBytes(part);
        var type = context.Package.ContentType(part).ToLowerInvariant();
        string Text()
        {
            using var reader = new StreamReader(new MemoryStream(bytes), System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }
        XElement Paragraph(string text, XElement? rPr, XElement? pPr) =>
            new(Wml.W + "p", pPr != null ? new XElement(pPr) : null,
                new XElement(Wml.W + "r", rPr != null ? new XElement(rPr) : null, new XElement(Wml.W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text)));
        try
        {
            if (type.Contains("wordprocessingml") || type.Contains("ms-word"))
            {
                using var package = new Opc.OpcPackage(new MemoryStream(bytes));
                if (package.FirstRelationship("/", "officeDocument")?.Target is { } main && package.Exists(main))
                {
                    var document = package.ReadXml(main);
                    Opc.MarkupCompatibility.Process(document, chartEx: true);
                    container.Add(document.Root.Child("body")?.Elements().Where(x => x.Name != Wml.W + "sectPr").Select(x => new XElement(x)));
                    // Its section breaks keep their kind, but their pages are the section's the chunk stands in
                    // (docx/external/libreoffice/writerfilter-dmapper/alt-chunk: a chunk of square pages of 283 pt in an
                    // A4 section, its first section on a new A4 page, the rest after it on that page).
                    foreach (var breakProperties in container.Descendants(Wml.W + "sectPr").ToList())
                    {
                        foreach (var page in breakProperties.Elements().Where(x => x.Name.LocalName is "pgSz" or "pgMar" or "cols" or "docGrid").ToList())
                            page.Remove();
                        foreach (var page in currentSection.Properties?.Elements().Where(x => x.Name.LocalName is "pgSz" or "pgMar" or "cols" or "docGrid") ?? [])
                            breakProperties.Add(new XElement(page));
                    }
                }
            }
            else if (type.Contains("rfc822") || type.Contains("mhtml") || type.StartsWith("multipart/", StringComparison.Ordinal))
            {
                // A web archive: its HTML (the pictures it carries are left out).
                if (Mhtml.Html(bytes) is { } archived)
                    container.Add(HtmlImport.Convert(archived));
            }
            else if (type.Contains("html"))
                container.Add(HtmlImport.Convert(Text()));
            else if (type.Contains("rtf"))
                container.Add(RtfImport.Convert(bytes, name => context.Styles.IdByName(name, "paragraph"), context.Styles.AddImported));
            else if (type.StartsWith("text/", StringComparison.Ordinal))
            {
                var rPr = new XElement(Wml.W + "rPr",
                    new XElement(Wml.W + "rFonts", new XAttribute(Wml.W + "ascii", "Courier New"), new XAttribute(Wml.W + "hAnsi", "Courier New"), new XAttribute(Wml.W + "cs", "Courier New")),
                    new XElement(Wml.W + "sz", new XAttribute(Wml.W + "val", "21")), new XElement(Wml.W + "szCs", new XAttribute(Wml.W + "val", "21")));
                var pPr = new XElement(Wml.W + "pPr", new XElement(Wml.W + "spacing", new XAttribute(Wml.W + "before", "0"), new XAttribute(Wml.W + "after", "0"),
                    new XAttribute(Wml.W + "line", "240"), new XAttribute(Wml.W + "lineRule", "auto")), new XElement(rPr));
                foreach (var line in Text().Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
                    container.Add(Paragraph(line, rPr, pPr));
            }
        }
        catch (Exception e) when (e is InvalidDataException or IOException or System.Xml.XmlException or InvalidOperationException
                                       or FormatException or ArgumentException or IndexOutOfRangeException or OverflowException)
        {
            // A chunk that cannot be read is left out.
        }
        return container;
    }

    /// <summary>
    /// A collapsed subdocument (w:subDoc, catalog: document.subDoc): a paragraph of the default style showing the
    /// subdocument's path, underlined in the hyperlink color (the Hyperlink style where the document has one, else
    /// the theme's), linking to it (measured with docx/local/subdoc).
    /// </summary>
    ParagraphBlock SubDocument(string target)
    {
        var shown = Uri.TryCreate(target, UriKind.Absolute, out var uri) && uri.IsFile ? uri.LocalPath : Uri.UnescapeDataString(target);
        var block = Paragraph(new XElement(Wml.W + "p"));
        var styleId = context.Styles.ParagraphStyleOf(null);
        RunFormat format;
        if (context.Styles.Find("Hyperlink") is { Type: "character" })
            format = context.Styles.Run(styleId, "Hyperlink", null, tableRun);
        else
        {
            format = context.Styles.Run(styleId, null, null, tableRun).Clone();
            format.Color = context.Theme.Colors.TryGetValue("hlink", out var hlink) ? hlink.ToHex().TrimStart('#').ToUpperInvariant() : "467886";
            format.Underline = "single";
        }
        block.Content.Add(new TextInline(shown, format) { Link = target });
        return block;
    }

    /// <param name="number">Whether a numbered paragraph takes its number (not where it runs into the next one).</param>
    /// <summary>
    /// Breaks (w:br) between paragraphs, in the body or a cell: they break at the start of the next paragraph, in the
    /// next table too (docx/external/libreoffice/extras-ooxmlimport/tdf108714: page breaks between paragraphs and
    /// after a table, line breaks before one and in its cells).
    /// </summary>
    readonly List<XElement> strayBreaks = [];

    /// <summary>A paragraph's indents in characters in points (ParagraphFormat.ApplyCharacterUnits).</summary>
    void CharacterIndents(ParagraphFormat format, string? styleId)
    {
        if (format.LeftChars is null or 0 && format.RightChars is null or 0 && format.FirstLineChars is null or 0)
            return;
        var normal = context.Styles.Run(context.Styles.DefaultParagraphStyle, null, null).Size ?? 10;
        format.ApplyCharacterUnits(normal, context.Styles.Run(styleId, null, null).Size ?? normal);
    }

    /// <summary>The field begin characters of each story that no end character of the same story closes, by story.</summary>
    readonly Dictionary<XElement, HashSet<XElement>> unclosed = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Whether a field's begin character has no end in its story: the body, a header or footer, a note, a comment or a
    /// text box's content, whose own fields are counted apart.
    /// </summary>
    bool Unclosed(XElement begin)
    {
        var w = Wml.W;
        var story = begin.Ancestors().FirstOrDefault(a => a.Name == w + "txbxContent" || a.Name == w + "body" || a.Name == w + "hdr" || a.Name == w + "ftr"
            || a.Name == w + "footnote" || a.Name == w + "endnote" || a.Name == w + "comment");
        if (story == null)
            return false;
        if (!unclosed.TryGetValue(story, out var open))
        {
            open = new HashSet<XElement>(ReferenceEqualityComparer.Instance);
            var stack = new Stack<XElement>();
            void Walk(XElement element)
            {
                foreach (var child in element.Elements())
                {
                    if (child.Name == w + "txbxContent")
                        continue;
                    if (child.Name == w + "fldChar")
                    {
                        if ((string?)child.Attribute(w + "fldCharType") == "begin")
                            stack.Push(child);
                        else if ((string?)child.Attribute(w + "fldCharType") == "end" && stack.Count > 0)
                            stack.Pop();
                        continue;
                    }
                    Walk(child);
                }
            }
            Walk(story);
            foreach (var left in stack)
                open.Add(left);
            unclosed[story] = open;
        }
        return open.Contains(begin);
    }

    ParagraphBlock Paragraph(XElement p, bool number = true)
    {
        // The runs of a paragraph take formats of their own among them, and among those of the paragraph before it where
        // that one runs into it (see taken); a paragraph in a text box of one of its runs among its own.
        var outer = taken;
        (taken, runsOn) = (runsOn ?? new(ReferenceEqualityComparer.Instance), null);
        try
        {
            var paragraph = ReadParagraph(p, number);
            runsOn = paragraph.MarkFormat.Vanish == true
                || p.Child("pPr")?.Child("rPr") is { } markRun && (markRun.Child("del") ?? markRun.Child("moveFrom")) != null ? taken : null;
            return paragraph;
        }
        finally
        {
            taken = outer;
        }
    }

    ParagraphBlock ReadParagraph(XElement p, bool number)
    {
        var pPr = p.Child("pPr");
        var direct = new ParagraphFormat();
        direct.Apply(pPr);
        var format = context.Styles.Paragraph(direct.Style, direct, table: tableParagraph);
        // Numbering switched off in the paragraph itself (w:numId 0) leaves it the indents of its own w:ind alone, none
        // of its styles' (measured with docx/local/nz0-*: a style's left 567 and hanging 567 gone, with numbering in
        // the style or without, a direct left 1136 kept; docx/external/libreoffice/extras-ooxmlexport/tdf95374).
        // The right one stays (docx/local/nzs-15, nzs-14: a paragraph of a style of right 1500 with numId 0 of its own
        // ends its lines 1500 twips from the right).
        if (direct.NumberingId == "0")
            (format.Left, format.FirstLine, format.LeftChars, format.FirstLineChars)
                = (direct.Left, direct.FirstLine, direct.LeftChars, direct.FirstLineChars);
        CharacterIndents(format, direct.Style);
        var styleId = context.Styles.ParagraphStyleOf(direct.Style);
        var markDirect = new RunFormat();
        markDirect.Apply(pPr.Child("rPr"));
        var mark = context.Styles.Run(styleId, markDirect.Style, markDirect, tableRun);
        // A numbered paragraph takes the level's indentation and tabs over its style's, under its own (measured
        // with docx/generated/lists: a level indented 360 twips puts a List Paragraph, whose style says 720, at
        // 360); the number is drawn in the paragraph mark's formatting with the level's run properties over it.
        ListLabel? label = null;
        // A paragraph of nothing but a section break takes no number and counts for none (measured with
        // docx/local/sn-1 to sn-6: items 1, 2, an empty numbered section end, then 3; in modes 14 and 15;
        // docx/external/libreoffice/extras-ooxmlexport/tdf170602_exportContext).
        if (pPr.Child("sectPr") != null && !p.Elements().Where(e => e.Name != Wml.W + "pPr").SelectMany(e => e.DescendantsAndSelf())
                .Any(e => e.Name.LocalName is "t" or "tab" or "br" or "cr" or "drawing" or "pict" or "object" or "sym" or "ptab" or "fldChar" or "fldSimple" or "noBreakHyphen" or "softHyphen"))
            number = false;
        if ((number ? Ask(new ListStep(format.NumberingId, format.NumberingLevel ?? 0, mark.Language)) as (string Text, NumberingLevel Level, string Own)? : null) is { } next)
        {
            format = context.Styles.Paragraph(direct.Style, direct, next.Level.ParagraphProperties, tableParagraph, numberingUnderStyle: direct.NumberingId == null);
            CharacterIndents(format, direct.Style);
            var levelRun = new RunFormat();
            levelRun.Apply(next.Level.RunProperties);
            // The number takes the mark's highlight but not its shading, the paragraph style's neither: only the
            // level's own w:shd shades it (measured with docx/local/labelshd: a mark shaded yellow, directly, through
            // its style or in a pattern, leaves the number and its tab or space white, a mark highlighted yellow
            // highlights them, a level's shading or highlight is theirs; docx/external/libreoffice/
            // extras-tiledrendering/tdf114799_shd, tdf114799_highlight).
            var numberFormat = mark.Clone();
            (numberFormat.ShadingPattern, numberFormat.ShadingColor, numberFormat.ShadingFill) = (null, null, null);
            numberFormat.Overlay(levelRun);
            label = new ListLabel(next.Text, numberFormat, next.Level.Suffix, next.Level.Justification, next.Level)
            {
                Own = next.Own, List = context.Numbering.Find(format.NumberingId)?.AbstractId,
            };
        }
        // The margins of the HTML divisions around the paragraph add to its indents (catalog: paragraph.htmlDivs).
        if (context.Divs.Chain(format.DivId) is { Count: > 0 } divs)
        {
            format.Left = (format.Left ?? 0) + divs.Sum(d => d.Left);
            format.Right = (format.Right ?? 0) + divs.Sum(d => d.Right);
        }
        var content = new List<Inline>();
        var field = new FieldState();
        var outerDeleted = deletedText;
        var deleted = deletedText = [];
        // A paragraph of nothing but equations shows them as a display equation, as in an m:oMathPara, whatever the
        // paragraph's justification (measured with docx/local/dmath-*: one or two m:oMath alone or after a bookmark
        // centered with limits above and below as in an m:oMathPara, in left and right aligned paragraphs too, m:defJc
        // left at the left; after text or a space inline; pandoc/golden/mark_math).
        foreach (var stray in strayBreaks)
            Run(new XElement(Wml.W + "r", new XElement(stray)), styleId, content, field);
        strayBreaks.Clear();
        if (LoneMath(p) is { Count: > 0 } maths)
        {
            ReadInlines(new XElement(p.Name, p.Elements().Where(e => e.Name.Namespace != Equations.MathLayout.M)), styleId, content, field);
            ReadMath(new XElement(Equations.MathLayout.M + "oMathPara", maths.Select(o => new XElement(o))), styleId, content);
        }
        else
            ReadInlines(p, styleId, content, field);
        deletedText = outerDeleted;
        // A first line or hanging indent in characters counts the characters of the paragraph's first run, its size
        // and its character spacing, the spacing twice where the document balances single- and double-byte widths
        // (measured with docx/local/chu2-15, chu2-14, chu2-grid, chu2-bal: firstLineChars 200 of 14 pt runs in a style
        // of 10.5 pt indents 28 pt, 29.2 pt with 0.6 pt spacing, 30.4 pt balanced; the mark's size counts for nothing;
        // docx/external/libreoffice/extras-ooxmlexport/tdf131594).
        if (format.FirstLineChars is { } characters && characters != 0
            && content.OfType<TextInline>().FirstOrDefault(t => t.Text.Length > 0)?.Format is { Size: { } runSize } first)
        {
            var spacing = (first.Spacing ?? 0) * (context.Profile.BalanceSpaces ? 2 : 1);
            format.ApplyCharacterUnits(context.Styles.Run(context.Styles.DefaultParagraphStyle, null, null).Size ?? 10, runSize + spacing);
        }
        foreach (var anchor in content.OfType<AnchorInline>())
            anchor.Indents = (format.Left ?? 0, format.Right ?? 0);
        // A paragraph mark within a field's instruction belongs to it and is hidden: the paragraph runs into the next
        // one (Word's PDF of docx/external/libreoffice/extras-globalfilter/fieldmark_QUOTE_nest: a QUOTE field's
        // instruction over seven paragraphs, a nested QUOTE in it, prints its result's six lines from the top).
        if (openFields.Any(f => !f.InResult))
        {
            mark = mark.Clone();
            mark.Vanish = true;
        }
        return new ParagraphBlock(p, Shared(format), content, Shared(mark))
        {
            Id = ++blocksRead,
            EndsSection = pPr.Child("sectPr") is { } sectPr ? SectionProperties.Read(sectPr, Resolve, context.Culture) : null,
            Label = label,
            Deleted = deleted.Count > 0 ? deleted : null,
        };
    }

    /// <summary>
    /// The blocks of a drawing's text box (catalog: drawing.textBoxes), read like body content; a table's style does
    /// not reach into text boxes in its cells.
    /// </summary>
    /// <summary>The text boxes of the shapes in a group or canvas, or null (catalog: drawing.groups, drawing.canvas).</summary>
    List<(XElement Shape, List<Block> Text)>? ChildTextBoxes(XElement drawing)
    {
        var content = drawing.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData")?.Elements().FirstOrDefault();
        if (content == null || content.Name.Namespace != Drawings.Wpg && content.Name.Namespace != Drawings.Wpc)
            return null;
        var result = new List<(XElement, List<Block>)>();
        foreach (var shape in content.Descendants(Drawings.Wps + "wsp"))
        {
            // A shape that goes on with a linked story has no text of its own; it shows what the boxes before it leave
            // (docx/external/libreoffice/extras-ooxmlexport/tdf73499: "textboxes!" in a group's second shape).
            if (shape.Element(Drawings.Wps + "linkedTxbx") != null)
            {
                result.Add((shape, []));
                continue;
            }
            if (shape.Element(Drawings.Wps + "txbx")?.Element(Wml.W + "txbxContent") is not { } box)
                continue;
            var (savedParagraph, savedRun) = (tableParagraph, tableRun);
            (tableParagraph, tableRun) = (null, null);
            var blocks = new List<Block>();
            ReadBlocks(box, blocks);
            JoinHidden(blocks, cell: false);
            (tableParagraph, tableRun) = (savedParagraph, savedRun);
            result.Add((shape, blocks));
        }
        return result.Count > 0 ? result : null;
    }

    List<Block>? TextBox(XElement drawing)
    {
        if (Drawings.TextBoxContent(drawing) is not { } content)
            return null;
        var (savedParagraph, savedRun) = (tableParagraph, tableRun);
        (tableParagraph, tableRun) = (null, null);
        var blocks = new List<Block>();
        ReadBlocks(content, blocks);
        JoinHidden(blocks, cell: false);
        (tableParagraph, tableRun) = (savedParagraph, savedRun);
        return blocks;
    }

    /// <summary>
    /// Where complex fields stand: a field's instruction is collected until w:fldChar separate, its result shows after
    /// it; the result of a page field (PAGE, NUMPAGES …) is kept apart, the layout computes it.
    /// </summary>
    sealed class FieldState
    {
        public sealed class Frame(RunFormat format)
        {
            public readonly System.Text.StringBuilder Instruction = new();
            public readonly RunFormat Format = format;
            /// <summary>The format of the instruction's first run with text.</summary>
            public RunFormat? InstructionFormat;
            /// <summary>The instruction's pieces in the formats of their runs (MACROBUTTON's display text).</summary>
            public readonly List<(string Text, RunFormat Format)> InstructionRuns = [];
            public bool InResult;
            public List<Inline>? Cached;
            /// <summary>The format of the run of the separate character, which an error text takes.</summary>
            public RunFormat? SeparateFormat;

            /// <summary>Whether the field is locked (w:fldLock): Word keeps its saved result.</summary>
            public bool Locked;

            /// <summary>
            /// The instruction of the date or time a legacy text form field shows instead of its saved result (w:textInput
            /// of w:type currentDate or currentTime, in its w:format), or null.
            /// </summary>
            public string? CurrentDate;

            /// <summary>Whether the field prints no result (SET, ASK), once its instruction is read.</summary>
            public bool Silent => silent ??= Instruction.ToString().TrimStart().Split(' ', 2)[0].ToUpperInvariant() is "SET" or "ASK";
            bool? silent;
        }

        public readonly Stack<Frame> Frames = new();

        /// <summary>
        /// How many fields begun in the paragraph have no end in their story (see ModelReader.Unclosed): their instruction
        /// and result print as text.
        /// </summary>
        public int Unclosed;

        /// <summary>Inside a field's instruction, also that of an outer field (a nested field is part of it).</summary>
        /// <summary>
        /// Whether what is read now does not print: a field's instruction, and the result of a field that sets a
        /// bookmark or asks for one (SET, ASK; docx/external/libreoffice/extras-ooxmlexport/fdo76163: "Praun et al. 2001"
        /// saved as a SET's result is not printed).
        /// </summary>
        public bool Hidden => Frames.Any(f => !f.InResult);

        /// <summary>
        /// Whether what is read now is the result of a field that sets a bookmark or asks for one (SET, ASK), which
        /// prints nothing, its text kept hidden for the bookmarks in it (docx/external/libreoffice/extras-ooxmlexport/
        /// fdo76163: "Praun et al. 2001" saved as a SET's result is not printed; extras-ooxmlimport/n751017: a REF to
        /// the bookmark inside an ASK's result shows "bar", the ASK nothing).
        /// </summary>
        public bool Silent => Frames.Any(f => f.InResult && f.Silent);

        /// <summary>The target of a HYPERLINK field whose result is being read: its URL and \l bookmark (catalog: field.hyperlinks).</summary>
        public string? Link
        {
            get
            {
                foreach (var frame in Frames)
                {
                    if (frame.InResult && Hyperlink(frame.Instruction.ToString()) is { } target)
                        return target;
                }
                return null;
            }
        }

        static string? Hyperlink(string instruction)
        {
            var parts = Arguments(instruction);
            if (parts.Count == 0 || !parts[0].Equals("HYPERLINK", StringComparison.OrdinalIgnoreCase))
                return null;
            string? url = null, bookmark = null;
            for (var i = 1; i < parts.Count; i++)
            {
                if (parts[i] == "\\l" && i + 1 < parts.Count)
                    bookmark = parts[++i];
                else if (parts[i].StartsWith('\\'))
                    i += parts[i] is "\\o" or "\\t" ? 1 : 0;
                else
                    url ??= parts[i];
            }
            return url == null && bookmark == null ? null : (url ?? "") + (bookmark != null ? "#" + bookmark : "");
        }

        /// <summary>A field instruction's words; quoted arguments are one word without their quotes.</summary>
        public static List<string> Arguments(string instruction)
        {
            var words = new List<string>();
            var i = 0;
            while (i < instruction.Length)
            {
                while (i < instruction.Length && char.IsWhiteSpace(instruction[i]))
                    i++;
                if (i >= instruction.Length)
                    break;
                if (instruction[i] == '"')
                {
                    var end = instruction.IndexOf('"', i + 1);
                    if (end < 0) end = instruction.Length;
                    words.Add(instruction[(i + 1)..end]);
                    i = end + 1;
                }
                else
                {
                    var start = i;
                    while (i < instruction.Length && !char.IsWhiteSpace(instruction[i]))
                        i++;
                    words.Add(instruction[start..i]);
                }
            }
            return words;
        }

        /// <summary>Where result content goes: the saved result of the innermost page field, else the paragraph.</summary>
        public List<Inline> Target(List<Inline> content) => Frames.FirstOrDefault(f => f.Cached != null)?.Cached ?? content;
    }

    void ReadInlines(XElement container, string? paragraphStyle, List<Inline> content, FieldState field)
    {
        foreach (var e in container.Elements())
        {
            if (e.Name.Namespace == Equations.MathLayout.M)
            {
                ReadMath(e, paragraphStyle, field.Target(content));
                continue;
            }
            if (e.Name.Namespace != Wml.W)
                continue;
            switch (e.Name.LocalName)
            {
                case "r":
                    Run(e, paragraphStyle, content, field);
                    break;
                // A break straight in a paragraph breaks as a run's (tdf108714).
                case "br":
                    Run(new XElement(Wml.W + "r", new XElement(e)), paragraphStyle, content, field);
                    break;
                case "hyperlink":
                {
                    // An external target through the relationship, a bookmark in the document through w:anchor.
                    var target = e.Attr(Wml.R + "id") is { } rid && context.Package.Relationship(currentPart, rid) is { External: true } rel ? rel.Target : null;
                    if (e.Attr("anchor") is { } bookmark)
                        target = (target ?? "") + "#" + bookmark;
                    var saved = (link, tocLink);
                    link = target ?? link;
                    tocLink |= openFields.Any(f => f.InResult && FieldInline.TypeOf(f.Instruction.ToString()) == "TOC");
                    simpleFields++;
                    ReadInlines(e, paragraphStyle, content, field);
                    simpleFields--;
                    (link, tocLink) = saved;
                    break;
                }
                case "permStart" or "permEnd":
                    field.Target(content).Add(new PermissionInline(e.Name.LocalName == "permStart", context.Styles.Run(paragraphStyle, null, null, tableRun)) { Xml = e });
                    break;
                case "bookmarkStart" when e.Attr("name") is { } name && !name.StartsWith("_GoBack", StringComparison.Ordinal):
                    field.Target(content).Add(new BookmarkInline(name, context.Styles.Run(paragraphStyle, null, null, tableRun)) { Id = e.Attr("id"), Xml = e });
                    break;
                case "bookmarkEnd" when e.Attr("id") is { } endId:
                    field.Target(content).Add(new BookmarkEndInline(endId, context.Styles.Run(paragraphStyle, null, null, tableRun)) { Xml = e });
                    break;
                case "ins" or "moveTo" or "del" or "moveFrom" when context.Markup:
                {
                    // In the markup view tracked changes keep their text, marked with their kind and author.
                    var saved = revision;
                    revision = new RevisionMark(e.Name.LocalName, context.Author(e.Attr("author")));
                    ReadInlines(e, paragraphStyle, content, field);
                    revision = saved;
                    break;
                }
                case "del" when !field.Hidden:
                    // Without the markup a deleted note reference still counts in the numbering, its note not shown
                    // (measured with docx/local/fnd-b, fnd-c, fnd-d: the second of three references deleted leaves the
                    // others numbered 1 and 3; docx/external/libreoffice/extras-ooxmlexport/tdf141660).
                    foreach (var deleted in e.Descendants().Where(x => x.Name == Wml.W + "footnoteReference" || x.Name == Wml.W + "endnoteReference"))
                        Ask(new NoteStep(deleted.Name.LocalName == "endnoteReference", null, currentSection.Index, currentSection.Properties));
                    // Its text stays as hidden text that the widths of autofit tables count (see RunFormat.Deleted).
                    deleting = true;
                    foreach (var run in e.Elements(Wml.W + "r"))
                        Run(run, paragraphStyle, content, field);
                    deleting = false;
                    break;
                case "commentRangeStart" or "commentRangeEnd" when context.Markup && e.Attr("id") is { } commentId:
                    if (e.Name.LocalName == "commentRangeStart")
                        CommentAnchored(commentId);
                    field.Target(content).Add(new CommentInline(commentId, e.Name.LocalName == "commentRangeStart", context.Styles.Run(paragraphStyle, null, null, tableRun)) { Xml = e });
                    break;
                case "smartTag" or "customXml" or "ins" or "moveTo":
                    ReadInlines(e, paragraphStyle, content, field);
                    break;
                case "bdo" or "dir":
                {
                    // Explicit overrides and embeddings (catalog: run.complexScript) reach the runs inside them.
                    var saved = (bidiOverride, bidiEmbedding);
                    if (e.Name.LocalName == "bdo")
                        bidiOverride = e.Val() ?? bidiOverride;
                    else
                        bidiEmbedding = e.Val() ?? bidiEmbedding;
                    ReadInlines(e, paragraphStyle, content, field);
                    (bidiOverride, bidiEmbedding) = saved;
                    break;
                }
                case "fldSimple" when FieldInline.IsPageField(e.Attr("instr") ?? "") && e.Attr("fldLock") is not ("1" or "true" or "on"):
                    var cached = new List<Inline>();
                    ReadInlines(e, paragraphStyle, cached, new FieldState());
                    var styleFormat = context.Styles.Run(paragraphStyle, null, null, tableRun);
                    var simpleFormat = cached.FirstOrDefault()?.Format ?? styleFormat;
                    if (FieldInline.TypeOf(e.Attr("instr")!) == "LISTNUM")
                    {
                        if (!field.Hidden && ListNumber(e.Attr("instr")!, e) is { } listed)
                            field.Target(content).Add(new TextInline(listed, simpleFormat) { Xml = e });
                        break;
                    }
                    if (FieldInline.IsAutoNumber(e.Attr("instr")!))
                    {
                        if (!field.Hidden)
                            field.Target(content).Add(AutoNumber(e.Attr("instr")!, e, simpleFormat) with { Xml = e });
                        break;
                    }
                    // A simple field has no instruction runs: what Word computes prints in the paragraph style's format,
                    // in the saved result's only with \* MERGEFORMAT, its errors in the style's bold (measured with
                    // docx/local/fsf-15, fsf-12, fsf2-*, fsf5-15: PAGE, NUMPAGES, SEQ, DATE, PAGEREF, SECTION and
                    // STYLEREF saved italic 16 pt red print plain 10 pt, with MERGEFORMAT italic 16 pt red, in the body
                    // and in a header; an error of REF or STYLEREF bold 10 pt either way). REF takes its bookmark's
                    // text with its format, which the saved result holds (Word's PDF of docx/external/libreoffice/
                    // extras-ooxmlimport/tdf133647_unicode: a REF of a heading in Calibri Light prints in Calibri Light).
                    var instr = e.Attr("instr")!;
                    var merged = instr.Contains("MERGEFORMAT", StringComparison.OrdinalIgnoreCase);
                    var computed = FieldInline.TypeOf(instr) is "PAGE" or "NUMPAGES" or "SECTIONPAGES" or "SEQ" or "DATE" or "TIME"
                        or "PAGEREF" or "SECTION" or "STYLEREF";
                    field.Target(content).Add(new FieldInline(instr, merged || !computed ? simpleFormat : styleFormat, cached)
                    {
                        ErrorFormat = styleFormat,
                        Unmerged = merged && FieldInline.TypeOf(instr) == "STYLEREF" && cached.Count > 0 ? styleFormat : null,
                        Xml = e,
                    });
                    break;
                case "fldSimple":
                    simpleFields++;
                    ReadInlines(e, paragraphStyle, content, field);
                    simpleFields--;
                    break;
                case "sdt":
                    if (e.Child("sdtContent") is { } sdtContent)
                    {
                        // A bound control shows its node's value in its first run's formatting.
                        if (e.Child("sdtPr") is { } sdtPr && Binding.Text(sdtPr) is { } bound)
                        {
                            var inner = new List<Inline>();
                            ReadInlines(sdtContent, paragraphStyle, inner, new FieldState());
                            var plainFormat = context.Styles.Run(paragraphStyle, null, null, tableRun);
                            // Its five spaces for an empty node stand in the paragraph style's format, not the runs' (Word's
                            // PDF of docx/external/libreoffice/extras-ooxmlexport/sdt-2-run: Calibri 11 beside runs of 22 pt).
                            // A value that takes the place of the placeholder the control showed leaves the placeholder's
                            // formatting behind, in the control's own (Word's PDF of docx/external/libreoffice/
                            // writerfilter-dmapper/content-control-data-binding-color: "ABC" from the node in black, not
                            // in the red of the Placeholder Text style its stored run has).
                            RunFormat ControlFormat()
                            {
                                var own = new RunFormat();
                                own.Apply(sdtPr.Child("rPr"));
                                return context.Styles.Run(paragraphStyle, own.Style, own, tableRun);
                            }
                            var format = bound == DataBinding.Empty ? plainFormat
                                : sdtPr.Child("showingPlcHdr") != null ? ControlFormat()
                                : inner.FirstOrDefault(i => i is TextInline)?.Format ?? plainFormat;
                            field.Target(content).Add(new TextInline(bound, format) { Xml = e });
                        }
                        else
                            ReadInlines(Shown(e, sdtContent, block: false), paragraphStyle, content, field);
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// The m:oMath of a paragraph that holds nothing else: no text, no math paragraph, only marks of ranges, proofing
    /// marks and empty runs beside them; else none.
    /// </summary>
    static List<XElement> LoneMath(XElement p)
    {
        var maths = new List<XElement>();
        foreach (var e in p.Elements())
        {
            if (e.Name == Equations.MathLayout.M + "oMath")
                maths.Add(e);
            else if (e.Name.Namespace != Wml.W || e.Name.LocalName == "pPr" || e.Name.LocalName.EndsWith("RangeStart", StringComparison.Ordinal)
                     || e.Name.LocalName.EndsWith("RangeEnd", StringComparison.Ordinal)
                     || e.Name.LocalName is "bookmarkStart" or "bookmarkEnd" or "proofErr" or "permStart" or "permEnd")
                continue;
            else if (e.Name.LocalName != "r" || e.Elements().Any(c => c.Name.LocalName is not ("rPr" or "lastRenderedPageBreak")))
                return [];
        }
        return maths;
    }

    /// <summary>Equations (catalog: math): an m:oMath in the text, the display equations of an m:oMathPara.</summary>
    void ReadMath(XElement e, string? paragraphStyle, List<Inline> content)
    {
        var m = Equations.MathLayout.M;
        var format = context.Styles.Run(paragraphStyle, null, null, tableRun);
        Dictionary<XElement, RunFormat> Formats(XElement math)
        {
            var formats = new Dictionary<XElement, RunFormat>();
            foreach (var rPr in math.Descendants(Wml.W + "rPr"))
            {
                var direct = new RunFormat();
                direct.Apply(rPr);
                formats[rPr] = context.Styles.Run(paragraphStyle, direct.Style, direct, tableRun);
            }
            return formats;
        }
        if (e.Name.LocalName == "oMath")
            content.Add(new MathInline(e, false, null, format, Formats(e)) { Xml = e });
        else if (e.Name.LocalName == "oMathPara")
        {
            var jc = (string?)e.Element(m + "oMathParaPr")?.Element(m + "jc")?.Attribute(m + "val")
                     ?? (string?)context.Settings?.Element(m + "mathPr")?.Element(m + "defJc")?.Attribute(m + "val") ?? "centerGroup";
            // The equations of one math paragraph stand on one line, one after the other (measured with
            // docx/local/mathset-*: E = mc and F = ma in one m:oMathPara show as E = mcF = ma).
            var maths = e.Elements(m + "oMath").ToList();
            var math = maths.Count == 1 ? maths[0] : new XElement(m + "oMath", maths.SelectMany(o => o.Elements()));
            if (maths.Count > 0)
                content.Add(new MathInline(math, true, jc, format, Formats(math)) { Xml = e });
        }
    }

    /// <summary>
    /// What a MACROBUTTON field without a result shows: its display text, the instruction after the macro's name, in
    /// the formats of its runs (docx/external/powertools/DB-HeadersFooters-Dest/Fax: "[Name - jump to next field with
    /// F11]" in 6.5 pt where the instruction's runs are).
    /// </summary>
    static IEnumerable<Inline> MacroButton(List<(string Text, RunFormat Format)> runs)
    {
        // The display text begins after the field's name, the macro's name and the spaces around them.
        var whole = string.Concat(runs.Select(r => r.Text));
        var at = 0;
        for (var word = 0; word < 2; word++)
        {
            while (at < whole.Length && char.IsWhiteSpace(whole[at]))
                at++;
            while (at < whole.Length && !char.IsWhiteSpace(whole[at]))
                at++;
        }
        while (at < whole.Length && char.IsWhiteSpace(whole[at]))
            at++;
        var offset = 0;
        foreach (var (text, format) in runs)
        {
            var from = Math.Max(0, at - offset);
            if (from < text.Length)
                yield return new TextInline(text[from..], format);
            offset += text.Length;
        }
    }

    /// <summary>
    /// A LISTNUM field's number (catalog: field.evaluation): in the list of its name (an abstract definition's w:name,
    /// else Word's default list of that name), without one in the list of the story's last numbered paragraph or the
    /// default list; at the level \l gives, else one deeper than a LISTNUM before it in its paragraph, else at the last
    /// LISTNUM's level without a name and the first with one; from \s on where given. It counts with the list, which
    /// numbered paragraphs count on (measured with docx/local/lnum-*: 1) 2) 3); "1)a)" then "b)"; after \l 3 a plain
    /// one at level 3, a LegalDefault one at level 1; a LISTNUM between paragraphs of a list "%1." numbered 2. and the
    /// paragraph after it 3.; MyList's \l 2 1.1. and 1.2. under its first heading).
    /// </summary>
    string? ListNumber(string instruction, XElement at)
    {
        var words = FieldState.Arguments(instruction.Replace("\\", " \\"));
        var name = words.Count > 1 && !words[1].StartsWith('\\') ? words[1] : null;
        int? level = null, start = null;
        for (var i = 1; i + 1 < words.Count; i++)
        {
            if (words[i] == "\\l" && int.TryParse(words[i + 1], out var l))
                level = Math.Clamp(l, 1, 9) - 1;
            if (words[i] == "\\s" && int.TryParse(words[i + 1], out var s))
                start = s;
        }
        return Ask(new ListNumStep(name, level, start, at.AncestorsAndSelf(Wml.W + "p").FirstOrDefault())) as string;
    }

    /// <summary>
    /// What a paragraph numbering field shows (catalog: field.evaluation): AUTONUM, AUTONUMLGL and AUTONUMOUT the
    /// number of their paragraph by its outline level (ListCounter.AutoNumber), whatever result was saved; BIDIOUTLINE
    /// counts too but prints ### in bold (measured with docx/local/anf-a to anf-c, in modes 14 and 15: BIDIOUTLINE in
    /// body text and headings ###, then AUTONUMOUT 2., 3., II., A., 1.; AUTONUMLGL 3.1.2., 4.1.1., body text after a
    /// ninth level heading 1.0.1.0.0.0.0.0.1.1.; two fields in one paragraph the same number;
    /// docx/external/libreoffice/extras-ooxmlexport/99_Fields).
    /// </summary>
    TextInline AutoNumber(string instruction, XElement at, RunFormat format)
    {
        var paragraph = at.AncestorsAndSelf(Wml.W + "p").FirstOrDefault();
        var direct = new ParagraphFormat();
        direct.Apply(paragraph?.Child("pPr"));
        var outline = context.Styles.Paragraph(direct.Style, direct).OutlineLevel;
        var type = FieldInline.TypeOf(instruction);
        var text = (string)Ask(new AutoNumStep(type, outline is >= 0 and <= 8 ? outline.Value + 1 : 0, paragraph))!;
        if (type != "BIDIOUTLINE")
            return new TextInline(text, format);
        var bold = format.Clone();
        bold.Bold = true;
        return new TextInline("###", bold);
    }

    /// <summary>
    /// The format of a field result the layout computes: a page field Word computes as it prints takes the format of
    /// its instruction's first run, not its saved result's, which it keeps only with \* MERGEFORMAT (measured with
    /// docx/local/fldfmt: PAGE, NUMPAGES and PAGEREF of a bold begin, an italic instruction, a red 16 pt saved result
    /// and a 9 pt end print italic 12 pt, an instruction beginning in a plain run plain, with MERGEFORMAT red 16 pt,
    /// without a saved result italic; toclink-2: a PAGEREF in a table of contents); the other computed fields keep
    /// the saved result's format.
    /// </summary>
    static RunFormat ResultFormat(FieldState.Frame field, string instruction)
    {
        var saved = field.Cached?.FirstOrDefault()?.Format;
        // NOTEREF computes only its error, in the instruction's format whatever the switches (docx/local/nref-miss-*).
        if (FieldInline.TypeOf(instruction) == "NOTEREF" && field.InstructionFormat is { } instructed)
            return instructed;
        if (FieldInline.TypeOf(instruction) is "PAGE" or "NUMPAGES" or "SECTIONPAGES" or "PAGEREF" && field.InstructionFormat is { } code
            && !(saved != null && instruction.Contains("MERGEFORMAT", StringComparison.OrdinalIgnoreCase)))
            return code;
        return saved ?? field.Format;
    }

    /// <summary>
    /// Whether the runs read are tracked deleted text outside the markup view, and the paragraph's deleted text read so
    /// far (see ParagraphBlock.Deleted).
    /// </summary>
    bool deleting;
    List<TextInline>? deletedText;

    void Run(XElement r, string? paragraphStyle, List<Inline> content, FieldState field)
    {
        var direct = new RunFormat();
        direct.Apply(r.Child("rPr"));
        var characterStyle = direct.Style;
        // So do the runs of a HYPERLINK field in a table of contents' result; direct formatting stays (measured with
        // docx/local/toch-15: an entry as a HYPERLINK field with the Hyperlink style prints plain, one with direct blue
        // and underline so, the field outside the table in the style; docx/external/libreoffice/extras-ooxmlexport/
        // tdf150166, core-docnode/tdf150086).
        var inTocField = openFields.Any(f => f.InResult && FieldInline.TypeOf(f.Instruction.ToString()) == "TOC")
                         && openFields.Any(f => FieldInline.TypeOf(f.Instruction.ToString()) == "HYPERLINK");
        if ((tocLink || inTocField) && context.Styles.Find(characterStyle) is { } linkStyle
            && (string.Equals(linkStyle.Name, "Hyperlink", StringComparison.OrdinalIgnoreCase) || string.Equals(linkStyle.Id, "Hyperlink", StringComparison.OrdinalIgnoreCase)))
            characterStyle = null;
        var format = context.Styles.Run(paragraphStyle, characterStyle, direct, tableRun);
        (format.Override, format.Embedding) = (bidiOverride, bidiEmbedding);
        format.Source = (r.Child("rPr")?.ToString(SaveOptions.DisableFormatting) ?? "", r.Attr("rsidRPr"), r.Attr("rsidR"));
        if (field.Silent)
            format.Vanish = true;
        if (context.Markup)
        {
            format.Revision = revision;
            if (r.Child("rPr")?.Child("rPrChange") is { } rPrChange)
                format.Change = new FormatChange(context.Describe.Run(r.Child("rPr"), rPrChange.Child("rPr")), context.Author(rPrChange.Attr("author")));
        }
        format.Origin = (direct, characterStyle, tableRun, format);
        if (deleting)
        {
            (format.Vanish, format.Deleted) = (true, true);
            format = Shared(format);
            foreach (var deleted in r.Elements(Wml.W + "delText"))
                deletedText?.Add(new TextInline(Text(deleted).Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' '), format));
            return;
        }
        format = Shared(format);
        foreach (var e in r.Elements())
        {
            if (e.Name.Namespace != Wml.W)
                continue;
            switch (e.Name.LocalName)
            {
                case "fldChar":
                    switch (e.Attr("fldCharType"))
                    {
                        case "begin" when Unclosed(e):
                            break;
                        case "begin":
                            openFields.Add((new(), false));
                            break;
                        case "separate" when openFields.Count > 0:
                            openFields[^1] = (openFields[^1].Instruction, true);
                            break;
                        case "end" when openFields.Count > 0:
                            openFields.RemoveAt(openFields.Count - 1);
                            break;
                    }
                    switch (e.Attr("fldCharType"))
                    {
                        case "begin" when Unclosed(e):
                            // A field whose end is not in its story (only in a text box's) shows its instruction and its
                            // result as text (docx/external/libreoffice/extras-ooxmlimport/tdf152200-bad_fldChar_end:
                            // " FORMTEXT " and five spaces, found by agent BK).
                            field.Unclosed++;
                            break;
                        case "begin":
                            // Legacy form fields show their state: a check box drawn as a box, a drop-down its entry.
                            if (e.Child("ffData") is { } form && !field.Hidden)
                            {
                                if (form.Child("checkBox") is { } box)
                                {
                                    var on = box.Child("checked") is { } c ? c.OnOff() ?? true : box.Child("default").Val() is "1" or "true" or "on";
                                    field.Target(content).Add(new CheckBoxInline(on, box.Child("size").Number("val") is { } half ? half / 2 : null, format) { Xml = e });
                                }
                                else if (form.Child("ddList") is { } list)
                                {
                                    var entries = list.Elements(Wml.W + "listEntry").Select(x => x.Val() ?? "").ToList();
                                    var index = list.Child("result").Number("val") is { } chosen ? (int)chosen : list.Child("default").Number("val") is { } preset ? (int)preset : 0;
                                    if (index >= 0 && index < entries.Count)
                                        field.Target(content).Add(new TextInline(entries[index], format) { Xml = e });
                                }
                            }
                            // A locked field keeps its saved result (docx/external/libreoffice/extras-ooxmlexport/
                            // tdf148380_fldLocked: a SAVEDATE and a PRINTDATE as saved).
                            // A text form field of the current date or time shows it as Word prints, in its format, not
                            // its saved result nor a date field nested in its instruction (Word's PDF of docx/external/
                            // libreoffice/extras-ww8export/tdf79435_legacyInputFields: "10/12/2026 11:42:42 AM" for
                            // currentDate in M/d/yyyy h:mm:ss am/pm, "12-Oct-26" for currentTime in d-MMM-yy).
                            var current = e.Child("ffData")?.Child("textInput") is { } input && input.Child("type").Val() is "currentDate" or "currentTime"
                                ? input.Child("format").Val() is { Length: > 0 } picture ? $"TIME \\@ \"{picture}\"" : input.Child("type").Val() == "currentDate" ? "DATE" : "TIME"
                                : null;
                            field.Frames.Push(new FieldState.Frame(format) { Locked = e.Attr("fldLock") is "1" or "true" or "on", CurrentDate = current });
                            break;
                        case "separate" when field.Frames.Count > 0:
                            var frame = field.Frames.Peek();
                            frame.InResult = true;
                            frame.SeparateFormat = format;
                            if (!frame.Locked && (FieldInline.IsPageField(frame.Instruction.ToString()) || frame.CurrentDate != null))
                                frame.Cached = [];
                            break;
                        case "end" when field.Frames.Count > 0:
                            var ended = field.Frames.Pop();
                            // A field without a result (no separator) that Word computes gets one too (measured with
                            // docx/external/libreoffice/extras-ooxmlexport/tdf154478: a PAGEREF of a missing bookmark
                            // shows Word's error text on a line of its own).
                            var instruction = ended.Instruction.ToString();
                            // GOTOBUTTON shows its text after the bookmark's name as MACROBUTTON after the macro's
                            // (docx/external/libreoffice/extras-ooxmlexport/Unsupportedtextfields: "Line 5").
                            if (FieldInline.TypeOf(instruction) is "MACROBUTTON" or "GOTOBUTTON" && !ended.InResult && !field.Hidden)
                            {
                                field.Target(content).AddRange(MacroButton(ended.InstructionRuns));
                                break;
                            }
                            // An EQ field shows its instruction laid out (EqField), its error bold where its text is not.
                            if (FieldInline.TypeOf(instruction) == "EQ" && !ended.InResult)
                            {
                                var errorFormat = (ended.InstructionFormat ?? ended.Format).Clone();
                                errorFormat.Bold = errorFormat.Bold != true;
                                if (!field.Hidden && EqField.Inlines(ended.InstructionRuns, context.ListSeparator,
                                        context.Culture.TwoLetterISOLanguageName == "de" ? "Fehler!" : "Error!", errorFormat) is { } shown)
                                    field.Target(content).AddRange(shown);
                                break;
                            }
                            if (FieldInline.TypeOf(instruction) == "LISTNUM")
                            {
                                if (!field.Hidden && ListNumber(instruction, e) is { } listed)
                                    field.Target(content).Add(new TextInline(listed, ended.InstructionFormat ?? ended.Format) { Xml = e });
                                break;
                            }
                            if (FieldInline.IsAutoNumber(instruction))
                            {
                                if (!field.Hidden)
                                    field.Target(content).Add(AutoNumber(instruction, e, ended.InstructionFormat ?? ended.Format) with { Xml = e });
                                break;
                            }
                            if (ended.Cached == null && !ended.InResult && !ended.Locked && !field.Hidden && FieldInline.IsPageField(instruction) && instruction.Trim().Length > 0)
                                ended.Cached = [];
                            // A field nested in another's instruction shows nothing of its own.
                            if (ended.Cached != null && !field.Hidden)
                                field.Target(content).Add(new FieldInline(ended.CurrentDate ?? instruction, ResultFormat(ended, instruction), ended.Cached) { ErrorFormat = ended.SeparateFormat, Xml = e });
                            break;
                    }
                    break;
                case "instrText":
                    if (field.Frames.Count > 0 && !field.Frames.Peek().InResult)
                    {
                        var open = field.Frames.Peek();
                        open.Instruction.Append(e.Value);
                        open.InstructionRuns.Add((e.Value, format));
                        if (open.InstructionFormat == null && e.Value.Trim().Length > 0)
                            open.InstructionFormat = format;
                    }
                    // Instruction text after the separate character is part of the result, which Word prints as text
                    // (docx/external/libreoffice/uibase-uno/cond-field-cached-value: a DOCVARIABLE's saved "Sunday" in a
                    // w:instrText of its result, found by agent BK).
                    else if ((field.Frames.Count > 0 || field.Unclosed > 0) && !field.Hidden)
                        field.Target(content).Add(new TextInline(e.Value, format) { Link = field.Link ?? link, Xml = e });
                    if (openFields.Count > 0 && !openFields[^1].InResult)
                        openFields[^1].Instruction.Append(e.Value);
                    break;
                case "t":
                case "delText" when context.Markup:
                    if (!field.Hidden)
                        // Line feeds and carriage returns in the text show as spaces, CR LF as one (measured with
                        // docx/local/wtcontrol: Word breaks no line there; tabs stay tabs).
                        field.Target(content).Add(new TextInline(Text(e).Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' '), format) { Link = field.Link ?? link, Xml = e });
                    break;
                case "delInstrText" when context.Markup:
                    if (field.Frames.Count > 0 && !field.Frames.Peek().InResult)
                        field.Frames.Peek().Instruction.Append(e.Value);
                    break;
                // Hidden runs print none of their tabs, breaks and drawings either (docx/external/libreoffice/core-undo/
                // anchor-type-change-position: a hidden flowchart and line break before a caption in a frame).
                case "tab":
                    if (!field.Hidden && format.Vanish != true)
                        field.Target(content).Add(new TabInline(format) { Xml = e });
                    break;
                case "pgNum":
                    // The page number as an element of the run (catalog: section.pageNumbers), shown as a PAGE field would.
                    if (!field.Hidden)
                        field.Target(content).Add(new FieldInline("PAGE", format, []) { Xml = e });
                    break;
                case "ptab":
                    if (!field.Hidden && format.Vanish != true)
                        field.Target(content).Add(new TabInline(format) { Positional = (e.Attr("alignment") ?? "left", e.Attr("relativeTo") ?? "margin", e.Attr("leader") ?? "none"), Xml = e });
                    break;
                case "br":
                    // Page and column breaks in table cells break nothing at all, the text going on in its line
                    // (measured with docx/local/cb-1, cb-2, cb-4 in modes 14 and 15; in a text box a line break, cb-5;
                    // docx/external/libreoffice/extras-ooxmlexport/fdo68607); in notes they break the line only
                    // (docx/external/libreoffice/extras-ooxmlimport/tdf129659: an endnote's address on its next line).
                    if (!field.Hidden && format.Vanish != true && !(tableParagraph != null && e.Attr("type") is "page" or "column"))
                        field.Target(content).Add(new BreakInline(noteMark != null && e.Attr("type") is "page" or "column" ? "textWrapping" : e.Attr("type") ?? "textWrapping",
                            e.Attr("clear"), format) { Xml = e });
                    break;
                case "cr":
                    if (!field.Hidden && format.Vanish != true)
                        field.Target(content).Add(new BreakInline("textWrapping", null, format) { Xml = e });
                    break;
                case "ruby" when !field.Hidden:
                {
                    List<TextInline> Texts(XElement? part, double? size = null)
                    {
                        var texts = new List<TextInline>();
                        foreach (var r in part?.Elements(Wml.W + "r") ?? [])
                        {
                            var own = new RunFormat();
                            own.Apply(r.Child("rPr"));
                            var runFormat = context.Styles.Run(paragraphStyle, own.Style, own, tableRun);
                            if (size != null)
                            {
                                runFormat = runFormat.Clone();
                                runFormat.Size = size;
                            }
                            var value = string.Concat(r.Elements(Wml.W + "t").Select(Text));
                            if (value.Length > 0)
                                texts.Add(new TextInline(value, runFormat));
                        }
                        return texts;
                    }
                    var properties = e.Child("rubyPr");
                    // The ruby text takes w:hps's size, whatever its runs say (measured with docx/local/rbs-15: hps 16
                    // over runs of 20 pt and hps 40 over runs of 8 pt print 8 and 20 pt; extras-rtfexport/tdf138779).
                    field.Target(content).Add(new RubyInline(Texts(e.Child("rubyBase")), Texts(e.Child("rt"), properties.Child("hps").Number("val") / 2),
                        properties.Child("rubyAlign").Val() ?? "center", (properties.Child("hpsRaise").Number("val") ?? 0) / 2, format) { Xml = e });
                    break;
                }
                case "dayShort" or "dayLong" or "monthShort" or "monthLong" or "yearShort" or "yearLong":
                    // Date blocks show the date the document is printed in the regional format, whatever the run's
                    // language: 11, Sonntag, 10, Oktober, 26, 2026 (measured with docx/local/run-content; dlg-15: runs in
                    // en-US, fr-FR, ja-JP and de-DE alike "Dienstag", "Oktober"; docx/external/powertools/CA/
                    // CA007-DayLong: "Montag" in en-US).
                    if (!field.Hidden)
                    {
                        var culture = context.Culture;
                        var today = context.Printed ?? DateTime.Now;
                        var shown = e.Name.LocalName switch
                        {
                            "dayShort" => today.Day.ToString(culture),
                            "dayLong" => culture.DateTimeFormat.GetDayName(today.DayOfWeek),
                            "monthShort" => today.Month.ToString(culture),
                            "monthLong" => culture.DateTimeFormat.GetMonthName(today.Month),
                            "yearShort" => (today.Year % 100).ToString("00", culture),
                            _ => today.Year.ToString(culture),
                        };
                        field.Target(content).Add(new TextInline(shown, format) { Xml = e });
                    }
                    break;
                case "noBreakHyphen":
                    // Word prints the font's own hyphen, which no line breaks after (a word joiner keeps it with the
                    // next character; fonts such as Arial have no U+2011, measured with docx/local/run-content).
                    if (!field.Hidden)
                        field.Target(content).Add(new TextInline("-\u2060", format) { Xml = e });
                    break;
                case "softHyphen":
                    if (!field.Hidden)
                        field.Target(content).Add(new TextInline("­", format) { Xml = e });
                    break;
                case "drawing":
                    if (!field.Hidden && format.Vanish != true)
                    {
                        foreach (var d in e.Elements())
                        {
                            var text = TextBox(d);
                            var children = ChildTextBoxes(d);
                            // From Word 2010 on a drawing turned across its line swaps its box's sides (Drawings.Box).
                            if (context.Profile.TurnedDrawingsSwapSides && Drawings.TurnedAcross(d))
                                d.SetAttributeValue(Drawings.SwappedTurn, "1");
                            if (d.Name == Drawings.Wp + "inline")
                                field.Target(content).Add(new DrawingInline(d, currentPart, format) { Text = text, ChildText = children, InField = InField, Xml = e });
                            // A hidden floating drawing (wp:docPr hidden) is gone, the text not wrapping around it either
                            // (docx/external/libreoffice/extras-ooxmlexport/tdf162527_hidden_image, tdf169802_hidden_shape:
                            // a picture and a rectangle wrapped tight, eleven full lines).
                            else if (d.Name == Drawings.Wp + "anchor" && d.Element(Drawings.Wp + "docPr")?.Attribute("hidden")?.Value is not ("1" or "true"))
                                field.Target(content).Add(new AnchorInline(d, currentPart, format) { Text = text, ChildText = children, Xml = e });
                        }
                    }
                    break;
                case "pict" or "object" when !field.Hidden && format.Vanish != true:
                    // Legacy VML drawings, converted to Word drawings (catalog: vml.shapes, vml.ole, vml.watermarks); an
                    // object may show itself as a Word drawing instead (docx/external/openxml-sdk/Strict01: w:object
                    // holding w:drawing and w:objectEmbed, an EMF of 362 by 146 pt).
                    foreach (var d in Vml.ToDrawings(e).Concat(e.Elements(Wml.W + "drawing").SelectMany(drawing => drawing.Elements())))
                    {
                        var text = TextBox(d);
                        var children = ChildTextBoxes(d);
                        if (d.Name == Drawings.Wp + "inline")
                            field.Target(content).Add(new DrawingInline(d, currentPart, format) { Text = text, ChildText = children, InField = InField || e.Name.LocalName == "object", Xml = e });
                        else
                            field.Target(content).Add(new AnchorInline(d, currentPart, format) { Text = text, ChildText = children, Xml = e });
                    }
                    break;
                case "footnoteReference" or "endnoteReference" when !field.Hidden && e.Attr("id") is { } noteId:
                {
                    var endnote = e.Name.LocalName == "endnoteReference";
                    var mark = (string)Ask(new NoteStep(endnote, noteId, currentSection.Index, currentSection.Properties))!;
                    var custom = e.Attr("customMarkFollows") is "1" or "true" or "on";
                    field.Target(content).Add(new NoteReferenceInline(noteId, endnote, custom ? "" : mark, format) { Xml = e });
                    break;
                }
                case "footnoteRef" or "endnoteRef" when !field.Hidden && noteMark != null:
                    field.Target(content).Add(new NoteReferenceInline(noteId ?? "", e.Name.LocalName == "endnoteRef", noteMark, format) { Own = true, Xml = e });
                    break;
                case "separator" or "continuationSeparator" when !field.Hidden:
                    field.Target(content).Add(new SeparatorInline(e.Name.LocalName == "continuationSeparator", format) { Xml = e });
                    break;
                case "sym":
                    if (!field.Hidden && e.Attr("char") is { } code && int.TryParse(code, System.Globalization.NumberStyles.HexNumber, null, out var value))
                    {
                        var symbol = format.Clone();
                        symbol.Ascii = symbol.HAnsi = e.Attr("font") ?? symbol.Ascii;
                        symbol.AsciiTheme = symbol.HAnsiTheme = null;
                        field.Target(content).Add(new TextInline(char.ConvertFromUtf32(value), symbol) { Xml = e });
                    }
                    break;
                // A run in a run counts as a run of its own (docx/external/libreoffice/writerfilter-ooxml/nested-runs:
                // "Test text box" in a text box's nested run).
                case "r":
                    Run(e, paragraphStyle, content, field);
                    break;
            }
        }
    }
}
