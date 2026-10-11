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
        .page>svg{position:absolute;left:0;top:0;overflow:visible}
        .page text{white-space:pre;font-variant-ligatures:none}
        .page text.sel{fill-opacity:0}
        .glyphs{position:absolute;left:0;top:0}
        @media print{body{background:none}.page{margin:0;break-after:page}}
        """;

    /// <summary>The HTML document of a Word document; a password-protected one needs its password.</summary>
    /// <param name="paperExtra">
    /// Points of paper added right of and below every page when it is printed (the fidelity tool prints wider paper
    /// than the page, because browsers round paper sizes, and cuts the page out again); 0 prints on the page's size.
    /// </param>
    /// <exception cref="PasswordException">The document needs a password, or the password is wrong.</exception>
    public static string ToHtml(string docxPath, FontLocator? fonts = null, string? password = null, double paperExtra = 0)
    {
        using var context = new DocumentContext(OpcPackage.Open(docxPath, password), fonts);
        var reader = new ModelReader(context);
        var blocks = reader.Body();
        var styles = new RunStyles(context);
        var layout = new ParagraphLayout(context, styles);
        var pages = new Paginator(context, layout).Paginate(blocks, reader.LastSection());
        var renderer = new PageRenderer(context);
        var body = new StringBuilder();
        // One named @page per paper size: printed, every page keeps its own size and nothing is added around it.
        var papers = new List<(double Width, double Height)>();
        foreach (var page in pages)
        {
            var paper = papers.FindIndex(p => Math.Abs(p.Width - page.Width) < 0.01 && Math.Abs(p.Height - page.Height) < 0.01);
            if (paper < 0)
            {
                papers.Add((page.Width, page.Height));
                paper = papers.Count - 1;
            }
            body.Append(renderer.Render(page, $"paper{paper}"));
        }
        var paperCss = new StringBuilder();
        for (var i = 0; i < papers.Count; i++)
            paperCss.Append($"@page paper{i}{{size:{Units.Css(papers[i].Width + paperExtra)}pt {Units.Css(papers[i].Height + paperExtra)}pt;margin:0}}.paper{i}{{page:paper{i}}}\n");

        var title = Path.GetFileNameWithoutExtension(docxPath);
        var html = new StringBuilder();
        html.Append("<!DOCTYPE html>\n<html>\n<head>\n<meta charset=\"utf-8\">\n<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">\n");
        html.Append($"<title>{WebUtility.HtmlEncode(title)}</title>\n<meta name=\"generator\" content=\"FreeSlate\">\n");
        // Fonts the browser cannot find by name (Office cloud fonts such as Aptos) are embedded.
        html.Append("<style>\n").Append(PptxConverter.FontFaces(context.UsedFonts, context.FontLocator)).Append(Css).Append('\n').Append(paperCss)
            .Append("</style>\n</head>\n<body>\n");
        html.Append(context.Glyphs.Svg());
        html.Append("<main class=\"document\">\n").Append(body).Append("</main>\n</body>\n</html>\n");
        return html.ToString();
    }

    public static void ToFile(string docxPath, string htmlPath, FontLocator? fonts = null, string? password = null, double paperExtra = 0)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(htmlPath))!);
        File.WriteAllText(htmlPath, ToHtml(docxPath, fonts, password, paperExtra));
    }
}
