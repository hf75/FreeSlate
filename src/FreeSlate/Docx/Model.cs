using System.Xml.Linq;

namespace FreeSlate.Docx;

/// <summary>A piece of a paragraph's content with its resolved character formatting.</summary>
public abstract record Inline(RunFormat Format);

/// <summary>Text of one run (catalog: run.basic).</summary>
public sealed record TextInline(string Text, RunFormat Format) : Inline(Format);

/// <summary>A tab character (w:tab in a run, catalog: paragraph.tabs).</summary>
public sealed record TabInline(RunFormat Format) : Inline(Format);

/// <summary>A break (w:br, w:cr, catalog: run.content): textWrapping (a line break), page or column, with its clear.</summary>
public sealed record BreakInline(string Type, string? Clear, RunFormat Format) : Inline(Format);

/// <summary>A block of the body: a paragraph or a table.</summary>
public abstract record Block;

/// <summary>A paragraph with its resolved paragraph formatting, its content and the format of its mark.</summary>
public sealed record ParagraphBlock(XElement Element, ParagraphFormat Format, List<Inline> Content, RunFormat MarkFormat) : Block
{
    /// <summary>The section this paragraph ends (its w:pPr/w:sectPr), or null.</summary>
    public SectionProperties? EndsSection { get; init; }
}

/// <summary>A table (catalog: table.structure); laid out by the table engine.</summary>
public sealed record TableBlock(XElement Element) : Block;

/// <summary>Page and section properties of a section (w:sectPr, catalog: section.page, section.columns).</summary>
public sealed class SectionProperties
{
    public double PageWidth = 612, PageHeight = 792;
    public double Top = 72, Bottom = 72, Left = 72, Right = 72, Header = 36, Footer = 36, Gutter;
    public string Start = "nextPage";
    public int Columns = 1;
    public double ColumnSpace = 36;
    public bool ColumnSeparator;
    public List<(double Width, double Space)>? ColumnWidths;
    public string VerticalAlign = "top";
    public XElement? Element;

    public static SectionProperties Read(XElement? sectPr)
    {
        var s = new SectionProperties { Element = sectPr };
        if (sectPr == null)
            return s;
        var size = sectPr.Child("pgSz");
        s.PageWidth = size.Twips("w") ?? s.PageWidth;
        s.PageHeight = size.Twips("h") ?? s.PageHeight;
        var margins = sectPr.Child("pgMar");
        s.Top = margins.Twips("top") ?? s.Top;
        s.Bottom = margins.Twips("bottom") ?? s.Bottom;
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

    /// <summary>The width of the text area (between the margins, without the gutter).</summary>
    public double TextWidth => Math.Max(1, PageWidth - Left - Right - Gutter);
}

/// <summary>
/// Reads the body of the main document into blocks with resolved formatting (catalog: document.body): paragraphs with
/// their runs, breaks and tabs; content controls, custom XML, smart tags, hyperlinks and insertions are transparent;
/// deletions and the instructions of fields are left out, field results stay (catalog: field.structure,
/// review.revisions: the document as it reads with all changes accepted).
/// </summary>
public sealed class ModelReader(DocumentContext context)
{
    public List<Block> Body()
    {
        var body = context.Document.Root.Child("body");
        var blocks = new List<Block>();
        if (body != null)
            ReadBlocks(body, blocks);
        return blocks;
    }

    /// <summary>The properties of the document's last section (the body's own w:sectPr).</summary>
    public SectionProperties LastSection() => SectionProperties.Read(context.Document.Root.Child("body").Child("sectPr"));

    void ReadBlocks(XElement container, List<Block> blocks)
    {
        foreach (var e in container.Elements())
        {
            if (e.Name.Namespace != Wml.W)
                continue;
            switch (e.Name.LocalName)
            {
                case "p":
                    blocks.Add(Paragraph(e));
                    break;
                case "tbl":
                    blocks.Add(new TableBlock(e));
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
    }

    ParagraphBlock Paragraph(XElement p)
    {
        var pPr = p.Child("pPr");
        var direct = new ParagraphFormat();
        direct.Apply(pPr);
        var format = context.Styles.Paragraph(direct.Style, direct);
        var styleId = context.Styles.ParagraphStyleOf(direct.Style);
        var markDirect = new RunFormat();
        markDirect.Apply(pPr.Child("rPr"));
        var mark = context.Styles.Run(styleId, markDirect.Style, markDirect);
        var content = new List<Inline>();
        var field = new FieldState();
        ReadInlines(p, styleId, content, field);
        return new ParagraphBlock(p, format, content, mark)
        {
            EndsSection = pPr.Child("sectPr") is { } sectPr ? SectionProperties.Read(sectPr) : null,
        };
    }

    /// <summary>Where a complex field stands: its instructions are hidden until w:fldChar separate, its result shows.</summary>
    sealed class FieldState
    {
        public readonly Stack<bool> InResult = new();
        public bool Hidden => InResult.Count > 0 && !InResult.Peek();
    }

    void ReadInlines(XElement container, string? paragraphStyle, List<Inline> content, FieldState field)
    {
        foreach (var e in container.Elements())
        {
            if (e.Name.Namespace != Wml.W)
                continue;
            switch (e.Name.LocalName)
            {
                case "r":
                    Run(e, paragraphStyle, content, field);
                    break;
                case "hyperlink" or "smartTag" or "customXml" or "ins" or "moveTo" or "bdo" or "dir":
                    ReadInlines(e, paragraphStyle, content, field);
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

    void Run(XElement r, string? paragraphStyle, List<Inline> content, FieldState field)
    {
        var direct = new RunFormat();
        direct.Apply(r.Child("rPr"));
        var format = context.Styles.Run(paragraphStyle, direct.Style, direct);
        foreach (var e in r.Elements())
        {
            if (e.Name.Namespace != Wml.W)
                continue;
            switch (e.Name.LocalName)
            {
                case "fldChar":
                    switch (e.Attr("fldCharType"))
                    {
                        case "begin": field.InResult.Push(false); break;
                        case "separate":
                            if (field.InResult.Count > 0) { field.InResult.Pop(); field.InResult.Push(true); }
                            break;
                        case "end":
                            if (field.InResult.Count > 0) field.InResult.Pop();
                            break;
                    }
                    break;
                case "t":
                    if (!field.Hidden)
                        content.Add(new TextInline(e.Value, format));
                    break;
                case "tab":
                    if (!field.Hidden)
                        content.Add(new TabInline(format));
                    break;
                case "br":
                    if (!field.Hidden)
                        content.Add(new BreakInline(e.Attr("type") ?? "textWrapping", e.Attr("clear"), format));
                    break;
                case "cr":
                    if (!field.Hidden)
                        content.Add(new BreakInline("textWrapping", null, format));
                    break;
                case "noBreakHyphen":
                    if (!field.Hidden)
                        content.Add(new TextInline("‑", format));
                    break;
                case "softHyphen":
                    if (!field.Hidden)
                        content.Add(new TextInline("­", format));
                    break;
                case "sym":
                    if (!field.Hidden && e.Attr("char") is { } code && int.TryParse(code, System.Globalization.NumberStyles.HexNumber, null, out var value))
                    {
                        var symbol = format.Clone();
                        symbol.Ascii = symbol.HAnsi = e.Attr("font") ?? symbol.Ascii;
                        symbol.AsciiTheme = symbol.HAnsiTheme = null;
                        content.Add(new TextInline(char.ConvertFromUtf32(value), symbol));
                    }
                    break;
            }
        }
    }
}
