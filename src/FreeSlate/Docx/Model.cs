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
public sealed record NoteReferenceInline(string Id, bool Endnote, string Mark, RunFormat Format) : Inline(Format);

/// <summary>The line of a note separator (w:separator, w:continuationSeparator) in the separator notes.</summary>
public sealed record SeparatorInline(bool Continuation, RunFormat Format) : Inline(Format);

/// <summary>Where a bookmark starts (w:bookmarkStart, catalog: field.bookmarks): an anchor in the HTML.</summary>
public sealed record BookmarkInline(string Name, RunFormat Format) : Inline(Format);

/// <summary>The start or end of an editable range (w:permStart, w:permEnd, catalog: review.permissions).</summary>
public sealed record PermissionInline(bool Start, RunFormat Format) : Inline(Format);

/// <summary>
/// An equation (catalog: math): an m:oMath in the text, or a display equation of an m:oMathPara on lines of its own,
/// justified by m:jc or the settings' m:defJc; its runs formatted by their w:rPr over the paragraph's style.
/// </summary>
public sealed record MathInline(XElement Element, bool Display, string? Justification, RunFormat Format, IReadOnlyDictionary<XElement, RunFormat> RunFormats)
    : Inline(Format);

/// <summary>A tab character (w:tab in a run, catalog: paragraph.tabs).</summary>
public sealed record TabInline(RunFormat Format) : Inline(Format);

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

    /// <summary>The fields whose result the layout computes.</summary>
    public static bool IsPageField(string instruction) => TypeOf(instruction) is "PAGE" or "NUMPAGES" or "SECTIONPAGES";
}

/// <summary>A block of the body: a paragraph or a table.</summary>
public abstract record Block;

/// <summary>A paragraph with its resolved paragraph formatting, its content and the format of its mark.</summary>
public sealed record ParagraphBlock(XElement Element, ParagraphFormat Format, List<Inline> Content, RunFormat MarkFormat) : Block
{
    /// <summary>The section this paragraph ends (its w:pPr/w:sectPr), or null.</summary>
    public SectionProperties? EndsSection { get; init; }

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

    /// <summary>The header and footer parts the section names, by type: default, first, even (catalog: headerFooter.parts).</summary>
    public Dictionary<string, string> Headers = new(StringComparer.Ordinal), Footers = new(StringComparer.Ordinal);

    /// <summary>Whether the section's first page has a header and footer of its own (w:titlePg).</summary>
    public bool TitlePage;

    /// <summary>The page numbering of the section (w:pgNumType): the number of its first page, or null to go on, and the format.</summary>
    public int? PageNumberStart;
    public string PageNumberFormat = "decimal";

    /// <param name="resolve">Finds the part a relationship id of the main document points to.</param>
    public static SectionProperties Read(XElement? sectPr, Func<string, string?>? resolve = null)
    {
        var s = new SectionProperties { Element = sectPr };
        if (sectPr == null)
            return s;
        foreach (var (name, into) in new[] { ("headerReference", s.Headers), ("footerReference", s.Footers) })
        {
            foreach (var reference in sectPr.Elements(Wml.W + name))
            {
                if (reference.Attr(Wml.R + "id") is { } id && resolve?.Invoke(id) is { } part)
                    into[reference.Attr("type") ?? "default"] = part;
            }
        }
        s.TitlePage = sectPr.Child("titlePg").OnOff() == true;
        var numbering = sectPr.Child("pgNumType");
        s.PageNumberStart = (int?)numbering.Number("start");
        s.PageNumberFormat = numbering.Attr("fmt") ?? "decimal";
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
        return s;
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
    /// <summary>The list counters of the story being read: the body and every header, footer and note count on their own.</summary>
    ListCounter counter = new(context.Numbering);

    /// <summary>The part being read, whose relationships the drawings' pictures resolve against.</summary>
    string currentPart = context.MainPart;

    /// <summary>The hyperlink (w:hyperlink) whose runs are being read, or null.</summary>
    string? link;

    public List<Block> Body()
    {
        var body = context.Document.Root.Child("body");
        var blocks = new List<Block>();
        counter = new ListCounter(context.Numbering);
        currentPart = context.MainPart;
        if (body != null)
            ReadBlocks(body, blocks);
        return blocks;
    }

    /// <summary>The properties of the document's last section (the body's own w:sectPr).</summary>
    public SectionProperties LastSection() => SectionProperties.Read(context.Document.Root.Child("body").Child("sectPr"), Resolve);

    /// <summary>The part a relationship id of the main document points to, where it exists.</summary>
    string? Resolve(string id) =>
        context.Package.Relationship(context.MainPart, id) is { External: false } rel && context.Package.Exists(rel.Target) ? rel.Target : null;

    readonly Dictionary<string, List<Block>> stories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The numbers of footnotes and endnotes in the order their references stand in the document.</summary>
    readonly Dictionary<string, int> footnoteNumbers = new(StringComparer.Ordinal), endnoteNumbers = new(StringComparer.Ordinal);

    /// <summary>An endnote's number (the order of its reference), or 0 where nothing refers to it.</summary>
    public int EndnoteNumber(string id) => endnoteNumbers.GetValueOrDefault(id);

    /// <summary>The number of the note being read, which its w:footnoteRef or w:endnoteRef shows.</summary>
    string? noteMark;

    /// <summary>
    /// The footnotes or endnotes of the document (catalog: note.footnotes, note.endnotes) by id, read after the
    /// body: their numbers come from the order of their references; separators are read under their types.
    /// </summary>
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
            var numbers = endnotes ? endnoteNumbers : footnoteNumbers;
            noteMark = type == "normal" && numbers.TryGetValue(id, out var number) ? NoteText(number, endnotes) : "";
            var blocks = new List<Block>();
            ReadBlocks(note, blocks);
            notes[type == "normal" ? id : type] = blocks;
        }
        noteMark = null;
        (counter, currentPart) = (savedCounter, savedPart);
        return notes;
    }

    /// <summary>A note's number in its format (w:footnotePr/w:numFmt: decimal for footnotes, lower roman for endnotes).</summary>
    string NoteText(int number, bool endnote)
    {
        var properties = context.Document.Root.Child("body").Child("sectPr").Child(endnote ? "endnotePr" : "footnotePr")
                         ?? context.Settings.Child(endnote ? "endnotePr" : "footnotePr");
        var format = properties.Child("numFmt").Val() ?? (endnote ? "lowerRoman" : "decimal");
        var start = (int)(properties.Child("numStart").Number("val") ?? 1);
        return NumberText.Format(number + start - 1, format);
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
        (counter, currentPart) = (bodyCounter, bodyPart);
        return stories[part] = blocks;
    }

    void ReadBlocks(XElement container, List<Block> blocks)
    {
        // A paragraph whose mark was deleted as a tracked change runs into the next one, which keeps its own
        // formatting and number (catalog: review.revisions; measured with docx/generated/revisions); before a table or
        // at the end it stays.
        ParagraphBlock? pending = null;
        foreach (var e in container.Elements())
        {
            if (e.Name.Namespace != Wml.W)
                continue;
            if (pending != null && e.Name.LocalName != "p")
            {
                blocks.Add(pending);
                pending = null;
            }
            switch (e.Name.LocalName)
            {
                case "p":
                {
                    var markDeleted = e.Child("pPr")?.Child("rPr")?.Child("del") != null;
                    var paragraph = Paragraph(e, number: !markDeleted);
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
                case "tbl":
                    blocks.Add(Table(e));
                    break;
                case "sdt":
                    if (e.Child("sdtContent") is { } content)
                        ReadBlocks(content, blocks);
                    break;
                case "customXml" or "smartTag" or "ins" or "moveTo":
                    ReadBlocks(e, blocks);
                    break;
            }
        }
        if (pending != null)
            blocks.Add(pending);
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
        if ((number ? counter.Next(format.NumberingId, format.NumberingLevel ?? 0) : null) is { } next)
        {
            format = context.Styles.Paragraph(direct.Style, direct, next.Level.ParagraphProperties, tableParagraph);
            var levelRun = new RunFormat();
            levelRun.Apply(next.Level.RunProperties);
            var numberFormat = mark.Clone();
            numberFormat.Overlay(levelRun);
            label = new ListLabel(next.Text, numberFormat, next.Level.Suffix, next.Level.Justification, next.Level);
        }
        var content = new List<Inline>();
        var field = new FieldState();
        ReadInlines(p, styleId, content, field);
        return new ParagraphBlock(p, format, content, mark)
        {
            EndsSection = pPr.Child("sectPr") is { } sectPr ? SectionProperties.Read(sectPr, Resolve) : null,
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
                    var saved = link;
                    link = target ?? link;
                    ReadInlines(e, paragraphStyle, content, field);
                    link = saved;
                    break;
                }
                case "permStart" or "permEnd":
                    field.Target(content).Add(new PermissionInline(e.Name.LocalName == "permStart", context.Styles.Run(paragraphStyle, null, null, tableRun)));
                    break;
                case "bookmarkStart" when e.Attr("name") is { } name && !name.StartsWith("_GoBack", StringComparison.Ordinal):
                    field.Target(content).Add(new BookmarkInline(name, context.Styles.Run(paragraphStyle, null, null, tableRun)));
                    break;
                case "smartTag" or "customXml" or "ins" or "moveTo" or "bdo" or "dir":
                    ReadInlines(e, paragraphStyle, content, field);
                    break;
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
                        ReadInlines(sdtContent, paragraphStyle, content, field);
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
            foreach (var math in e.Elements(m + "oMath"))
                content.Add(new MathInline(math, true, jc, format, Formats(math)));
        }
    }

    void Run(XElement r, string? paragraphStyle, List<Inline> content, FieldState field)
    {
        var direct = new RunFormat();
        direct.Apply(r.Child("rPr"));
        var format = context.Styles.Run(paragraphStyle, direct.Style, direct, tableRun);
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
                            if (ended.Cached != null)
                                field.Target(content).Add(new FieldInline(ended.Instruction.ToString(), ended.Cached.FirstOrDefault()?.Format ?? ended.Format, ended.Cached));
                            break;
                    }
                    break;
                case "instrText":
                    if (field.Frames.Count > 0 && !field.Frames.Peek().InResult)
                        field.Frames.Peek().Instruction.Append(e.Value);
                    break;
                case "t":
                    if (!field.Hidden)
                        field.Target(content).Add(new TextInline(e.Value, format) { Link = field.Link ?? link });
                    break;
                case "tab":
                    if (!field.Hidden)
                        field.Target(content).Add(new TabInline(format));
                    break;
                case "br":
                    if (!field.Hidden)
                        field.Target(content).Add(new BreakInline(e.Attr("type") ?? "textWrapping", e.Attr("clear"), format));
                    break;
                case "cr":
                    if (!field.Hidden)
                        field.Target(content).Add(new BreakInline("textWrapping", null, format));
                    break;
                case "noBreakHyphen":
                    if (!field.Hidden)
                        field.Target(content).Add(new TextInline("‑", format));
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
                        if (d.Name == Drawings.Wp + "inline")
                            field.Target(content).Add(new DrawingInline(d, currentPart, format) { Text = text });
                        else
                            field.Target(content).Add(new AnchorInline(d, currentPart, format) { Text = text });
                    }
                    break;
                case "footnoteReference" or "endnoteReference" when !field.Hidden && e.Attr("id") is { } noteId:
                {
                    var endnote = e.Name.LocalName == "endnoteReference";
                    var numbers = endnote ? endnoteNumbers : footnoteNumbers;
                    if (!numbers.TryGetValue(noteId, out var number))
                        numbers[noteId] = number = numbers.Count + 1;
                    var custom = e.Attr("customMarkFollows") is "1" or "true" or "on";
                    field.Target(content).Add(new NoteReferenceInline(noteId, endnote, custom ? "" : NoteText(number, endnote), format));
                    break;
                }
                case "footnoteRef" or "endnoteRef" when !field.Hidden && noteMark != null:
                    field.Target(content).Add(new TextInline(noteMark, format));
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
