using System.Globalization;
using System.Net;
using System.Text;
using FreeSlate.Docx;
using FreeSlate.Docx.Layout;
using FreeSlate.Fonts;
using FreeSlate.Opc;

namespace FreeSlate;

/// <summary>
/// Converts a Word document into one self-contained HTML document (catalog: engine.output for docx): the pages one
/// below the other at their size, a single page through #page-N, laid out by FreeSlate's own engine the way Word lays
/// the document out for printing.
/// </summary>
public static class DocxConverter
{
    const string Css = """
        *{box-sizing:border-box}
        html,body{margin:0;padding:0}
        body{background:#7f7f7f;font-family:system-ui,sans-serif}
        .page{position:relative;overflow:hidden;margin:16px auto;background:#fff}
        body:has(.page:target){background:#fff}
        body:has(.page:target) .page:not(:target){display:none}
        .page:target{margin:0}
        .page>svg,.sheet>svg{position:absolute;left:0;top:0;overflow:visible}
        .page>.sheet{position:absolute;left:0;top:0}
        .page text{white-space:pre;font-variant-ligatures:none}
        .page text.sel{fill-opacity:0}
        .glyphs{position:absolute;left:0;top:0}
        .page .sp{position:absolute;left:0;top:0;transform-origin:0 0}
        .page a.bm{position:absolute;width:0;height:0}
        .page .alt{position:absolute;pointer-events:none}
        .page a text{cursor:pointer}
        .page .grp{position:absolute;left:0;top:0}
        .page .sp>svg{position:absolute;left:0;top:0;overflow:visible}
        .page .sp text{white-space:pre;font-variant-ligatures:none}
        .fs-nopic{position:absolute;left:0;top:0;width:100%;height:100%;box-sizing:border-box;border:1px solid #6d6d6d;overflow:hidden;padding:5px 6px;font:11px/13px Tahoma,'Segoe UI',sans-serif;color:#000;white-space:nowrap;pointer-events:none}
        .fs-nopic svg{vertical-align:top;margin-right:5px}
        @media print{body{background:none}.page{margin:0;break-after:page}.page.markup{width:var(--pw)!important}.page.markup>.sheet{transform:translateY(var(--oy)) scale(var(--s));transform-origin:0 0}.page [shape-rendering][stroke]:not([stroke=none]){shape-rendering:auto}}
        """;

    /// <summary>The HTML document of a Word document; a password-protected one needs its password.</summary>
    /// <param name="paperExtra">
    /// Points of paper added right of and below every page when it is printed (the fidelity tool prints wider paper
    /// than the page, because browsers round paper sizes, and cuts the page out again); 0 prints on the page's size.
    /// </param>
    /// <param name="culture">
    /// The regional format of numbers and dates in charts and of their default texts, the system's
    /// (<see cref="CultureInfo.CurrentCulture"/>) as in Word when null.
    /// </param>
    /// <exception cref="PasswordException">The document needs a password, or the password is wrong.</exception>
    /// <param name="markup">
    /// Whether the document shows its markup as Word prints it with markup (catalog: review.revisions,
    /// review.comments): tracked changes in their authors' colors, comments and formatting changes in balloons beside
    /// the page; else as it reads with all changes accepted, as Word prints by default.
    /// </param>
    /// <param name="printed">The time the document prints at, which date and time fields show; now when null.</param>
    public static string ToHtml(string docxPath, FontLocator? fonts = null, string? password = null, double paperExtra = 0,
        CultureInfo? culture = null, bool markup = false, DateTime? printed = null)
    {
        using var context = new DocumentContext(OpcPackage.Open(docxPath, password), fonts) { Culture = culture ?? CultureInfo.CurrentCulture, Markup = markup, Printed = printed };
        var reader = new ModelReader(context);
        var blocks = reader.Body();
        var styles = new RunStyles(context);
        var layout = new ParagraphLayout(context, styles);
        // Notes are read after the body: their numbers come from the order of their references.
        var footnotes = reader.Notes(endnotes: false);
        var endnoteParts = reader.Notes(endnotes: true);
        var endnotes = endnoteParts.Where(n => n.Key is not ("separator" or "continuationSeparator" or "continuationNotice")).OrderBy(n => reader.EndnoteNumber(n.Key))
            .Where(n => reader.EndnoteNumber(n.Key) > 0).Select(n => (reader.EndnoteSection(n.Key), n.Value)).ToList();
        var pages = new Paginator(context, layout, reader) { EndnoteContinuation = endnoteParts.GetValueOrDefault("continuationSeparator") }
            .Paginate(blocks, reader.LastSection(), footnotes, endnotes, endnoteParts.GetValueOrDefault("separator"));
        var drawings = new Drawings(context, culture ?? CultureInfo.CurrentCulture);
        // The markup view's comments; its pages have the pane where the document has balloons.
        var comments = markup ? reader.Comments() : [];
        var pane = markup && (comments.Count > 0 || pages.Any(p => p.Lines.Any(l => l.Line.BlockChange != null || l.Line.Pieces.Any(x => x.Style.Change != null))));
        var renderer = new PageRenderer(context, drawings, new TableLayout(context, layout)) { Comments = comments, MarkupPane = pane };
        var body = new StringBuilder();
        // One named @page per paper size: printed, every page keeps its own size and nothing is added around it.
        var papers = new List<(double Width, double Height)>();
        foreach (var page in pages)
        {
            var paper = papers.FindIndex(p => Math.Abs(p.Width - page.PaperWidth) < 0.01 && Math.Abs(p.Height - page.PaperHeight) < 0.01);
            if (paper < 0)
            {
                papers.Add((page.PaperWidth, page.PaperHeight));
                paper = papers.Count - 1;
            }
            body.Append(renderer.Render(page, $"paper{paper}"));
        }
        var paperCss = new StringBuilder();
        for (var i = 0; i < papers.Count; i++)
            paperCss.Append($"@page paper{i}{{size:{Units.Css(papers[i].Width + paperExtra)}pt {Units.Css(papers[i].Height + paperExtra)}pt;margin:0}}.paper{i}{{page:paper{i}}}\n");

        // Document properties (catalog: package.properties) go into the head; the language is the core properties',
        // else the document defaults' (w:lang).
        var properties = Pptx.DeckInfo.ReadProperties(context.Package, context.Load);
        var title = properties.Title ?? Path.GetFileNameWithoutExtension(docxPath);
        var language = properties.Language ?? context.Styles.DefaultRun.Language;
        var html = new StringBuilder();
        html.Append("<!DOCTYPE html>\n").Append(language is { Length: > 0 } ? $"<html lang=\"{WebUtility.HtmlEncode(language)}\">\n" : "<html>\n");
        html.Append("<head>\n<meta charset=\"utf-8\">\n<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">\n");
        html.Append($"<title>{WebUtility.HtmlEncode(title)}</title>\n<meta name=\"generator\" content=\"FreeSlate\">\n");
        foreach (var (name, value) in new[]
                 {
                     ("author", properties.Creator), ("description", properties.Description ?? properties.Subject),
                     ("keywords", properties.Keywords), ("dcterms.created", properties.Created), ("dcterms.modified", properties.Modified),
                 })
        {
            if (value != null)
                html.Append($"<meta name=\"{name}\" content=\"{WebUtility.HtmlEncode(value)}\">\n");
        }
        // Fonts the browser cannot find by name (Office cloud fonts such as Aptos) are embedded.
        html.Append("<style>\n").Append(PptxConverter.FontFaces(context.UsedFonts.Concat(drawings.UsedFonts).Distinct(StringComparer.OrdinalIgnoreCase), context.FontLocator)).Append(Css).Append('\n').Append(paperCss)
            .Append("</style>\n</head>\n<body>\n");
        html.Append(context.Glyphs.Svg());
        html.Append("<main class=\"document\">\n").Append(body).Append("</main>\n</body>\n</html>\n");
        return html.ToString();
    }

    public static void ToFile(string docxPath, string htmlPath, FontLocator? fonts = null, string? password = null, double paperExtra = 0,
        CultureInfo? culture = null, bool markup = false, DateTime? printed = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(htmlPath))!);
        File.WriteAllText(htmlPath, ToHtml(docxPath, fonts, password, paperExtra, culture, markup, printed));
    }
}
