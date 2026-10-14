using System.Xml.Linq;

namespace FreeSlate.Docx;

/// <summary>A piece of a paragraph's content with its resolved character formatting.</summary>
public abstract record Inline(RunFormat Format);

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

    public static string TypeOf(string instruction) =>
        instruction.Trim().Split(' ', 2)[0].ToUpperInvariant();

    /// <summary>The fields whose result the layout computes (catalog: field.evaluation); the others show their saved result.</summary>
    public static bool IsPageField(string instruction) => TypeOf(instruction) is "PAGE" or "NUMPAGES" or "SECTIONPAGES" or "PAGEREF"
        or "STYLEREF" or "SEQ" or "SECTION" or "DATE" or "TIME" or "PRINTDATE" or "SAVEDATE" or "IF" or "REF" or "SYMBOL" or "";
}

/// <summary>A block of the body: a paragraph or a table.</summary>
public abstract record Block;

/// <summary>A paragraph with its resolved paragraph formatting, its content and the format of its mark.</summary>
public sealed record ParagraphBlock(XElement Element, ParagraphFormat Format, List<Inline> Content, RunFormat MarkFormat) : Block
{
    /// <summary>The section this paragraph ends (its w:pPr/w:sectPr), or null.</summary>
    public SectionProperties? EndsSection { get; init; }

    /// <summary>Whether anything of the paragraph's content shows: not hidden, not a bookmark or permission mark.</summary>
    public bool Shows => Content.Any(i => i is not (BookmarkInline or BookmarkEndInline or PermissionInline) && i.Format.Vanish != true);

    /// <summary>The tracked change of the paragraph's mark and its tracked formatting change, in the markup view.</summary>
    public RevisionMark? MarkRevision { get; init; }
    public FormatChange? Change { get; init; }

    /// <summary>The paragraph's list number or bullet (catalog: numbering.counting), or null.</summary>
    public ListLabel? Label { get; init; }
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

    /// <summary>The directions of the w:bdo and w:dir around the runs being read, or null.</summary>
    string? bidiOverride, bidiEmbedding;

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

    public List<Block> Body()
    {
        var body = context.Document.Root.Child("body");
        var blocks = new List<Block>();
        counter = new ListCounter(context.Numbering);
        currentPart = context.MainPart;
        if (body != null)
        {
            // Each section's properties stand at its end.
            bodyElement = body;
            var pending = new List<XElement>();
            var index = 0;
            foreach (var e in body.Elements())
            {
                pending.Add(e);
                if (e.Name == Wml.W + "p" && e.Child("pPr").Child("sectPr") is { } properties)
                {
                    foreach (var p in pending)
                        sectionOf[p] = (properties, index);
                    pending.Clear();
                    index++;
                }
            }
            foreach (var p in pending)
                sectionOf[p] = (body.Child("sectPr"), index);
            currentSection = sectionOf.Count > 0 ? sectionOf[body.Elements().First()] : (body.Child("sectPr"), 0);
            ReadBlocks(body, blocks);
            JoinHidden(blocks, cell: false);
        }
        return blocks;
    }

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
        return space == "preserve" ? t.Value : t.Value.Trim(' ', '\t', '\r', '\n').Replace('\t', ' ');
    }

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
            if (k + 1 < blocks.Count && blocks[k + 1] is ParagraphBlock next)
            {
                blocks[k] = paragraph with { Content = [.. paragraph.Content, .. next.Content], MarkFormat = next.MarkFormat, EndsSection = next.EndsSection };
                blocks.RemoveAt(k + 1);
                k--;
            }
            else if (k + 1 < blocks.Count && !paragraph.Shows)
            {
                // Before a table one with nothing to show is gone as well (docx/external/poi/bug59058: an HTML form's
                // hidden "Top of Form").
                blocks.RemoveAt(k);
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
    public SectionProperties LastSection() => SectionProperties.Read(context.Document.Root.Child("body").Child("sectPr"), Resolve, context.Culture);

    /// <summary>The part a relationship id of the main document points to, where it exists.</summary>
    string? Resolve(string id) =>
        context.Package.Relationship(context.MainPart, id) is { External: false } rel && context.Package.Exists(rel.Target) ? rel.Target : null;

    readonly Dictionary<string, List<Block>> stories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The numbers of footnotes and endnotes in the order their references stand in the document.</summary>
    readonly Dictionary<string, int> footnoteNumbers = new(StringComparer.Ordinal), endnoteNumbers = new(StringComparer.Ordinal);

    /// <summary>The marks of the notes (by kind and id) and the sections of the endnotes' references.</summary>
    readonly Dictionary<(bool Endnote, string Id), string> noteMarks = [];
    readonly Dictionary<string, int> endnoteSections = new(StringComparer.Ordinal);

    /// <summary>The count of each kind of note and the section of its last reference, for restarts.</summary>
    readonly int[] noteCounts = new int[2], noteSectionsSeen = [-1, -1];

    /// <summary>An endnote's number (the order of its reference), or 0 where nothing refers to it.</summary>
    public int EndnoteNumber(string id) => endnoteNumbers.GetValueOrDefault(id);

    /// <summary>The index of the section that refers to an endnote.</summary>
    public int EndnoteSection(string id) => endnoteSections.GetValueOrDefault(id);

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
            return;
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
            noteMark = type == "normal" && noteMarks.TryGetValue((endnotes, id), out var mark) ? mark : "";
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
            if (type is "separator" or "continuationSeparator")
            {
                foreach (var paragraph in blocks.OfType<ParagraphBlock>().Where(p => p.Element.Descendants().Any(e => e.Name.LocalName is "separator" or "continuationSeparator")))
                {
                    var f = paragraph.Format;
                    (f.Before, f.After, f.BeforeLines, f.AfterLines, f.BeforeAuto, f.AfterAuto, f.Line, f.LineRule) = (0, 0, null, null, false, false, 240, "auto");
                }
            }
            notes[type == "normal" ? id : type] = blocks;
        }
        noteMark = null;
        (counter, currentPart) = (savedCounter, savedPart);
        return notes;
    }

    /// <summary>
    /// The mark of the next note of a kind (catalog: note.footnotes, note.endnotes), as Word numbers them (measured with
    /// docx/local/fnpages-c): in the format, from the start and with the restarts of the section of its reference
    /// (w:footnotePr, w:endnotePr), else of the document's settings; decimal footnotes and lower roman endnotes by
    /// default, counting on through the sections.
    /// </summary>
    string NextNoteMark(bool endnote)
    {
        var name = endnote ? "endnotePr" : "footnotePr";
        var (own, settings) = (currentSection.Properties.Child(name), context.Settings.Child(name));
        string? Value(string property) => own.Child(property).Val() ?? settings.Child(property).Val();
        var kind = endnote ? 1 : 0;
        if (Value("numRestart") == "eachSect" && noteSectionsSeen[kind] != currentSection.Index)
            noteCounts[kind] = 0;
        noteSectionsSeen[kind] = currentSection.Index;
        var start = int.TryParse(Value("numStart"), out var first) ? first : 1;
        return NumberText.Format(++noteCounts[kind] + start - 1, Value("numFmt") ?? (endnote ? "lowerRoman" : "decimal"));
    }

    /// <summary>The blocks of a header or footer part (catalog: headerFooter.parts), read once.</summary>
    public List<Block> Story(string part)
    {
        if (stories.TryGetValue(part, out var blocks))
            return blocks;
        blocks = [];
        var (bodyCounter, bodyPart) = (counter, currentPart);
        counter = new ListCounter(context.Numbering);
        currentPart = part;
        if (context.Load(part).Root is { } root)
            ReadBlocks(root, blocks);
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

    void ReadBlocks(XElement container, List<Block> blocks)
    {
        // A paragraph whose mark was deleted as a tracked change runs into the next one, which keeps its own
        // formatting and number (catalog: review.revisions; measured with docx/generated/revisions); before a table or
        // at the end it stays.
        ParagraphBlock? pending = null;
        foreach (var e in container.Elements())
        {
            if (container == bodyElement && sectionOf.TryGetValue(e, out var section))
                currentSection = section;
            if (e.Name.Namespace != Wml.W)
                continue;
            // A table whose rows were all deleted is gone (docx/external/powertools/RA001-Tracked-Revisions-01: a moved
            // and deleted table between paragraphs whose marks were deleted too).
            if (e.Name.LocalName == "tbl" && Gone(e, context.Markup))
                continue;
            // Marks of ranges between paragraphs (moves, bookmarks, comments, permissions) leave a paragraph whose mark
            // is gone waiting for the next (docx/external/powertools/RP/RP015-MoveFrom-MoveTo: a moveFromRangeEnd
            // after the paragraph a move left).
            if (pending != null && e.Name.LocalName != "p" && !e.Name.LocalName.EndsWith("RangeStart", StringComparison.Ordinal)
                && !e.Name.LocalName.EndsWith("RangeEnd", StringComparison.Ordinal)
                && e.Name.LocalName is not ("bookmarkStart" or "bookmarkEnd" or "permStart" or "permEnd" or "proofErr"))
            {
                blocks.Add(pending);
                pending = null;
            }
            switch (e.Name.LocalName)
            {
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
                        paragraph = paragraph with { Content = [.. pending.Content, .. paragraph.Content] };
                        pending = null;
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
                        ReadBlocks(content, blocks);
                        // A bound control shows its node's value in its first paragraph's formatting.
                        if (e.Child("sdtPr") is { } sdtPr && Binding.Text(sdtPr) is { } bound
                            && blocks.Skip(start).OfType<ParagraphBlock>().FirstOrDefault() is { } first)
                        {
                            var format = first.Content.FirstOrDefault()?.Format ?? first.MarkFormat;
                            first.Content.Clear();
                            first.Content.Add(new TextInline(bound, format));
                        }
                    }
                    break;
                case "customXml" or "smartTag" or "ins" or "moveTo":
                    ReadBlocks(e, blocks);
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
        if (pending != null)
            blocks.Add(pending);
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
                    Opc.MarkupCompatibility.Process(document);
                    container.Add(document.Root.Child("body")?.Elements().Where(x => x.Name != Wml.W + "sectPr").Select(x => new XElement(x)));
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
    ParagraphBlock Paragraph(XElement p, bool number = true)
    {
        var pPr = p.Child("pPr");
        var direct = new ParagraphFormat();
        direct.Apply(pPr);
        var format = context.Styles.Paragraph(direct.Style, direct, table: tableParagraph);
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
        if ((number ? counter.Next(format.NumberingId, format.NumberingLevel ?? 0, mark.Language) : null) is { } next)
        {
            format = context.Styles.Paragraph(direct.Style, direct, next.Level.ParagraphProperties, tableParagraph, numberingUnderStyle: direct.NumberingId == null);
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
            label = new ListLabel(next.Text, numberFormat, next.Level.Suffix, next.Level.Justification, next.Level) { Own = next.Own };
        }
        // The margins of the HTML divisions around the paragraph add to its indents (catalog: paragraph.htmlDivs).
        if (context.Divs.Chain(format.DivId) is { Count: > 0 } divs)
        {
            format.Left = (format.Left ?? 0) + divs.Sum(d => d.Left);
            format.Right = (format.Right ?? 0) + divs.Sum(d => d.Right);
        }
        var content = new List<Inline>();
        var field = new FieldState();
        ReadInlines(p, styleId, content, field);
        return new ParagraphBlock(p, format, content, mark)
        {
            EndsSection = pPr.Child("sectPr") is { } sectPr ? SectionProperties.Read(sectPr, Resolve, context.Culture) : null,
            Label = label,
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
            public bool InResult;
            public List<Inline>? Cached;
        }

        public readonly Stack<Frame> Frames = new();

        /// <summary>Inside a field's instruction, also that of an outer field (a nested field is part of it).</summary>
        public bool Hidden => Frames.Any(f => !f.InResult);

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
        static List<string> Arguments(string instruction)
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
                case "hyperlink":
                {
                    // An external target through the relationship, a bookmark in the document through w:anchor.
                    var target = e.Attr(Wml.R + "id") is { } rid && context.Package.Relationship(currentPart, rid) is { External: true } rel ? rel.Target : null;
                    if (e.Attr("anchor") is { } bookmark)
                        target = (target ?? "") + "#" + bookmark;
                    var saved = (link, tocLink);
                    link = target ?? link;
                    tocLink |= openFields.Any(f => f.InResult && FieldInline.TypeOf(f.Instruction.ToString()) == "TOC");
                    ReadInlines(e, paragraphStyle, content, field);
                    (link, tocLink) = saved;
                    break;
                }
                case "permStart" or "permEnd":
                    field.Target(content).Add(new PermissionInline(e.Name.LocalName == "permStart", context.Styles.Run(paragraphStyle, null, null, tableRun)));
                    break;
                case "bookmarkStart" when e.Attr("name") is { } name && !name.StartsWith("_GoBack", StringComparison.Ordinal):
                    field.Target(content).Add(new BookmarkInline(name, context.Styles.Run(paragraphStyle, null, null, tableRun)) { Id = e.Attr("id") });
                    break;
                case "bookmarkEnd" when e.Attr("id") is { } endId:
                    field.Target(content).Add(new BookmarkEndInline(endId, context.Styles.Run(paragraphStyle, null, null, tableRun)));
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
                case "commentRangeStart" or "commentRangeEnd" when context.Markup && e.Attr("id") is { } commentId:
                    if (e.Name.LocalName == "commentRangeStart")
                        CommentAnchored(commentId);
                    field.Target(content).Add(new CommentInline(commentId, e.Name.LocalName == "commentRangeStart", context.Styles.Run(paragraphStyle, null, null, tableRun)));
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
                case "fldSimple" when FieldInline.IsPageField(e.Attr("instr") ?? ""):
                    var cached = new List<Inline>();
                    ReadInlines(e, paragraphStyle, cached, new FieldState());
                    field.Target(content).Add(new FieldInline(e.Attr("instr")!, cached.FirstOrDefault()?.Format ?? context.Styles.Run(paragraphStyle, null, null, tableRun), cached));
                    break;
                case "fldSimple":
                    ReadInlines(e, paragraphStyle, content, field);
                    break;
                case "sdt":
                    if (e.Child("sdtContent") is { } sdtContent)
                    {
                        // A bound control shows its node's value in its first run's formatting.
                        if (e.Child("sdtPr") is { } sdtPr && Binding.Text(sdtPr) is { } bound)
                        {
                            var inner = new List<Inline>();
                            ReadInlines(sdtContent, paragraphStyle, inner, new FieldState());
                            var format = inner.FirstOrDefault(i => i is TextInline)?.Format ?? context.Styles.Run(paragraphStyle, null, null, tableRun);
                            field.Target(content).Add(new TextInline(bound, format));
                        }
                        else
                            ReadInlines(sdtContent, paragraphStyle, content, field);
                    }
                    break;
            }
        }
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
            content.Add(new MathInline(e, false, null, format, Formats(e)));
        else if (e.Name.LocalName == "oMathPara")
        {
            var jc = (string?)e.Element(m + "oMathParaPr")?.Element(m + "jc")?.Attribute(m + "val")
                     ?? (string?)context.Settings?.Element(m + "mathPr")?.Element(m + "defJc")?.Attribute(m + "val") ?? "centerGroup";
            // The equations of one math paragraph stand on one line, one after the other (measured with
            // docx/local/mathset-*: E = mc and F = ma in one m:oMathPara show as E = mcF = ma).
            var maths = e.Elements(m + "oMath").ToList();
            var math = maths.Count == 1 ? maths[0] : new XElement(m + "oMath", maths.SelectMany(o => o.Elements()));
            if (maths.Count > 0)
                content.Add(new MathInline(math, true, jc, format, Formats(math)));
        }
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
        if (FieldInline.TypeOf(instruction) is "PAGE" or "NUMPAGES" or "SECTIONPAGES" or "PAGEREF" && field.InstructionFormat is { } code
            && !(saved != null && instruction.Contains("MERGEFORMAT", StringComparison.OrdinalIgnoreCase)))
            return code;
        return saved ?? field.Format;
    }

    void Run(XElement r, string? paragraphStyle, List<Inline> content, FieldState field)
    {
        var direct = new RunFormat();
        direct.Apply(r.Child("rPr"));
        var characterStyle = direct.Style;
        if (tocLink && context.Styles.Find(characterStyle) is { } linkStyle
            && (string.Equals(linkStyle.Name, "Hyperlink", StringComparison.OrdinalIgnoreCase) || string.Equals(linkStyle.Id, "Hyperlink", StringComparison.OrdinalIgnoreCase)))
            characterStyle = null;
        var format = context.Styles.Run(paragraphStyle, characterStyle, direct, tableRun);
        (format.Override, format.Embedding) = (bidiOverride, bidiEmbedding);
        if (context.Markup)
        {
            format.Revision = revision;
            if (r.Child("rPr")?.Child("rPrChange") is { } rPrChange)
                format.Change = new FormatChange(context.Describe.Run(r.Child("rPr"), rPrChange.Child("rPr")), context.Author(rPrChange.Attr("author")));
        }
        foreach (var e in r.Elements())
        {
            if (e.Name.Namespace != Wml.W)
                continue;
            switch (e.Name.LocalName)
            {
                case "fldChar":
                    switch (e.Attr("fldCharType"))
                    {
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
                        case "begin":
                            // Legacy form fields show their state: a check box drawn as a box, a drop-down its entry.
                            if (e.Child("ffData") is { } form && !field.Hidden)
                            {
                                if (form.Child("checkBox") is { } box)
                                {
                                    var on = box.Child("checked") is { } c ? c.OnOff() ?? true : box.Child("default").Val() is "1" or "true" or "on";
                                    field.Target(content).Add(new CheckBoxInline(on, box.Child("size").Number("val") is { } half ? half / 2 : null, format));
                                }
                                else if (form.Child("ddList") is { } list)
                                {
                                    var entries = list.Elements(Wml.W + "listEntry").Select(x => x.Val() ?? "").ToList();
                                    var index = list.Child("result").Number("val") is { } chosen ? (int)chosen : list.Child("default").Number("val") is { } preset ? (int)preset : 0;
                                    if (index >= 0 && index < entries.Count)
                                        field.Target(content).Add(new TextInline(entries[index], format));
                                }
                            }
                            field.Frames.Push(new FieldState.Frame(format));
                            break;
                        case "separate" when field.Frames.Count > 0:
                            var frame = field.Frames.Peek();
                            frame.InResult = true;
                            if (FieldInline.IsPageField(frame.Instruction.ToString()))
                                frame.Cached = [];
                            break;
                        case "end" when field.Frames.Count > 0:
                            var ended = field.Frames.Pop();
                            // A field without a result (no separator) that Word computes gets one too (measured with
                            // docx/external/libreoffice/extras-ooxmlexport/tdf154478: a PAGEREF of a missing bookmark
                            // shows Word's error text on a line of its own).
                            var instruction = ended.Instruction.ToString();
                            if (ended.Cached == null && !ended.InResult && !field.Hidden && FieldInline.IsPageField(instruction) && instruction.Trim().Length > 0)
                                ended.Cached = [];
                            if (ended.Cached != null)
                                field.Target(content).Add(new FieldInline(instruction, ResultFormat(ended, instruction), ended.Cached));
                            break;
                    }
                    break;
                case "instrText":
                    if (field.Frames.Count > 0 && !field.Frames.Peek().InResult)
                    {
                        var open = field.Frames.Peek();
                        open.Instruction.Append(e.Value);
                        if (open.InstructionFormat == null && e.Value.Trim().Length > 0)
                            open.InstructionFormat = format;
                    }
                    if (openFields.Count > 0 && !openFields[^1].InResult)
                        openFields[^1].Instruction.Append(e.Value);
                    break;
                case "t":
                case "delText" when context.Markup:
                    if (!field.Hidden)
                        // Line feeds and carriage returns in the text show as spaces, CR LF as one (measured with
                        // docx/local/wtcontrol: Word breaks no line there; tabs stay tabs).
                        field.Target(content).Add(new TextInline(Text(e).Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' '), format) { Link = field.Link ?? link });
                    break;
                case "delInstrText" when context.Markup:
                    if (field.Frames.Count > 0 && !field.Frames.Peek().InResult)
                        field.Frames.Peek().Instruction.Append(e.Value);
                    break;
                case "tab":
                    if (!field.Hidden)
                        field.Target(content).Add(new TabInline(format));
                    break;
                case "pgNum":
                    // The page number as an element of the run (catalog: section.pageNumbers), shown as a PAGE field would.
                    if (!field.Hidden)
                        field.Target(content).Add(new FieldInline("PAGE", format, []));
                    break;
                case "ptab":
                    if (!field.Hidden)
                        field.Target(content).Add(new TabInline(format) { Positional = (e.Attr("alignment") ?? "left", e.Attr("relativeTo") ?? "margin", e.Attr("leader") ?? "none") });
                    break;
                case "br":
                    // Page and column breaks in table cells break nothing at all, the text going on in its line
                    // (measured with docx/local/cb-1, cb-2, cb-4 in modes 14 and 15; in a text box a line break, cb-5;
                    // docx/external/libreoffice/extras-ooxmlexport/fdo68607).
                    if (!field.Hidden && !(tableParagraph != null && e.Attr("type") is "page" or "column"))
                        field.Target(content).Add(new BreakInline(e.Attr("type") ?? "textWrapping", e.Attr("clear"), format));
                    break;
                case "cr":
                    if (!field.Hidden)
                        field.Target(content).Add(new BreakInline("textWrapping", null, format));
                    break;
                case "ruby" when !field.Hidden:
                {
                    List<TextInline> Texts(XElement? part)
                    {
                        var texts = new List<TextInline>();
                        foreach (var r in part?.Elements(Wml.W + "r") ?? [])
                        {
                            var own = new RunFormat();
                            own.Apply(r.Child("rPr"));
                            var runFormat = context.Styles.Run(paragraphStyle, own.Style, own, tableRun);
                            var value = string.Concat(r.Elements(Wml.W + "t").Select(Text));
                            if (value.Length > 0)
                                texts.Add(new TextInline(value, runFormat));
                        }
                        return texts;
                    }
                    var properties = e.Child("rubyPr");
                    field.Target(content).Add(new RubyInline(Texts(e.Child("rubyBase")), Texts(e.Child("rt")),
                        properties.Child("rubyAlign").Val() ?? "center", (properties.Child("hpsRaise").Number("val") ?? 0) / 2, format));
                    break;
                }
                case "dayShort" or "dayLong" or "monthShort" or "monthLong" or "yearShort" or "yearLong":
                    // Date blocks show today's date in the run's language, else the regional one: 11, Sonntag, 10,
                    // Oktober, 26, 2026 (measured with docx/local/run-content).
                    if (!field.Hidden)
                    {
                        var culture = context.Culture;
                        try
                        {
                            if (format.Language is { Length: > 0 } language)
                                culture = System.Globalization.CultureInfo.GetCultureInfo(language);
                        }
                        catch (System.Globalization.CultureNotFoundException) { }
                        var today = DateTime.Now;
                        var shown = e.Name.LocalName switch
                        {
                            "dayShort" => today.Day.ToString(culture),
                            "dayLong" => culture.DateTimeFormat.GetDayName(today.DayOfWeek),
                            "monthShort" => today.Month.ToString(culture),
                            "monthLong" => culture.DateTimeFormat.GetMonthName(today.Month),
                            "yearShort" => (today.Year % 100).ToString("00", culture),
                            _ => today.Year.ToString(culture),
                        };
                        field.Target(content).Add(new TextInline(shown, format));
                    }
                    break;
                case "noBreakHyphen":
                    // Word prints the font's own hyphen, which no line breaks after (a word joiner keeps it with the
                    // next character; fonts such as Arial have no U+2011, measured with docx/local/run-content).
                    if (!field.Hidden)
                        field.Target(content).Add(new TextInline("-\u2060", format));
                    break;
                case "softHyphen":
                    if (!field.Hidden)
                        field.Target(content).Add(new TextInline("­", format));
                    break;
                case "drawing":
                    if (!field.Hidden)
                    {
                        foreach (var d in e.Elements())
                        {
                            var text = TextBox(d);
                            var children = ChildTextBoxes(d);
                            if (d.Name == Drawings.Wp + "inline")
                                field.Target(content).Add(new DrawingInline(d, currentPart, format) { Text = text, ChildText = children });
                            else if (d.Name == Drawings.Wp + "anchor")
                                field.Target(content).Add(new AnchorInline(d, currentPart, format) { Text = text, ChildText = children });
                        }
                    }
                    break;
                case "pict" or "object" when !field.Hidden:
                    // Legacy VML drawings, converted to Word drawings (catalog: vml.shapes, vml.ole, vml.watermarks).
                    foreach (var d in Vml.ToDrawings(e))
                    {
                        var text = TextBox(d);
                        var children = ChildTextBoxes(d);
                        if (d.Name == Drawings.Wp + "inline")
                            field.Target(content).Add(new DrawingInline(d, currentPart, format) { Text = text, ChildText = children });
                        else
                            field.Target(content).Add(new AnchorInline(d, currentPart, format) { Text = text, ChildText = children });
                    }
                    break;
                case "footnoteReference" or "endnoteReference" when !field.Hidden && e.Attr("id") is { } noteId:
                {
                    var endnote = e.Name.LocalName == "endnoteReference";
                    var numbers = endnote ? endnoteNumbers : footnoteNumbers;
                    if (!numbers.ContainsKey(noteId))
                    {
                        numbers[noteId] = numbers.Count + 1;
                        noteMarks[(endnote, noteId)] = NextNoteMark(endnote);
                        if (endnote)
                            endnoteSections[noteId] = currentSection.Index;
                    }
                    var custom = e.Attr("customMarkFollows") is "1" or "true" or "on";
                    field.Target(content).Add(new NoteReferenceInline(noteId, endnote, custom ? "" : noteMarks[(endnote, noteId)], format));
                    break;
                }
                case "footnoteRef" or "endnoteRef" when !field.Hidden && noteMark != null:
                    field.Target(content).Add(new NoteReferenceInline(noteId ?? "", e.Name.LocalName == "endnoteRef", noteMark, format) { Own = true });
                    break;
                case "separator" or "continuationSeparator" when !field.Hidden:
                    field.Target(content).Add(new SeparatorInline(e.Name.LocalName == "continuationSeparator", format));
                    break;
                case "sym":
                    if (!field.Hidden && e.Attr("char") is { } code && int.TryParse(code, System.Globalization.NumberStyles.HexNumber, null, out var value))
                    {
                        var symbol = format.Clone();
                        symbol.Ascii = symbol.HAnsi = e.Attr("font") ?? symbol.Ascii;
                        symbol.AsciiTheme = symbol.HAnsiTheme = null;
                        field.Target(content).Add(new TextInline(char.ConvertFromUtf32(value), symbol));
                    }
                    break;
            }
        }
    }
}
