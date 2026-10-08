using System.Net;
using System.Text;
using FreeSlate.Fonts;
using FreeSlate.Html;
using FreeSlate.Opc;
using FreeSlate.Pptx;

namespace FreeSlate;

/// <summary>Converts a PowerPoint presentation into one self-contained HTML document.</summary>
public static class PptxConverter
{
    const string Css = """
        *{box-sizing:border-box}
        html,body{margin:0;padding:0}
        body{background:#7f7f7f}
        .slide{position:relative;overflow:hidden;margin:16px auto;background:#fff}
        body:has(.slide:target){background:#fff}
        body:has(.slide:target) .slide:not(:target){display:none}
        .slide:target{margin:0}
        .bg{position:absolute;left:0;top:0}
        .sp{position:absolute;left:0;top:0;transform-origin:0 0}
        .sp>svg{position:absolute;left:0;top:0;overflow:visible}
        .sp text{white-space:pre;font-variant-ligatures:none}
        """;

    public static string ToHtml(string pptxPath, FontLocator? fonts = null)
    {
        using var context = new PresentationContext(OpcPackage.Open(pptxPath));
        var paint = new SvgPaint();
        var slides = new StringBuilder();
        for (var i = 0; i < context.SlideParts.Count; i++)
            slides.Append(new SlideRenderer(new SlideContext(context, context.SlideParts[i], i + 1), paint).Render()).Append('\n');

        var html = new StringBuilder();
        html.Append("<!DOCTYPE html>\n<html>\n<head>\n<meta charset=\"utf-8\">\n");
        html.Append($"<title>{WebUtility.HtmlEncode(Path.GetFileNameWithoutExtension(pptxPath))}</title>\n<style>\n");
        html.Append(FontFaces(context.UsedFonts, fonts ?? FontLocator.Default));
        html.Append(Css).Append("\n</style>\n</head>\n<body>\n").Append(slides).Append("</body>\n</html>\n");
        return html.ToString();
    }

    public static void ToFile(string pptxPath, string htmlPath, FontLocator? fonts = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(htmlPath))!);
        File.WriteAllText(htmlPath, ToHtml(pptxPath, fonts));
    }

    /// <summary>@font-face rules for used fonts that the browser cannot find by name (e.g. Office cloud fonts).</summary>
    static string FontFaces(IEnumerable<string> families, FontLocator fonts)
    {
        var css = new StringBuilder();
        foreach (var family in families.Order(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var face in fonts.Embeddable(family).DistinctBy(f => (f.Bold, f.Italic)))
            {
                var data = Convert.ToBase64String(File.ReadAllBytes(face.Path));
                var format = Path.GetExtension(face.Path).Equals(".otf", StringComparison.OrdinalIgnoreCase) ? "opentype" : "truetype";
                css.Append($"@font-face{{font-family:'{family.Replace("'", "\\'")}';src:url(data:font/{format};base64,{data}) format('{format}');" +
                           $"font-weight:{(face.Bold ? 700 : 400)};font-style:{(face.Italic ? "italic" : "normal")}}}\n");
            }
        }
        return css.ToString();
    }
}
