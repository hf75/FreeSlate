using System.Net;
using System.Text;
using FreeSlate.Fonts;
using FreeSlate.Html;
using FreeSlate.Opc;
using FreeSlate.Pptx;

namespace FreeSlate;

/// <summary>
/// Converts a PowerPoint presentation into one self-contained HTML document (catalog: engine.output): all slides
/// one below the other, a single slide at its original size through #slide-N, speaker notes, sections and the
/// presentation data the player needs.
/// </summary>
public static class PptxConverter
{
    const string Css = """
        *{box-sizing:border-box}
        html,body{margin:0;padding:0}
        body{background:#7f7f7f;font-family:system-ui,sans-serif}
        .slide{position:relative;overflow:hidden;margin:16px auto;background:#fff}
        .slide[data-hidden]:not(:target){opacity:.5;outline:3px dashed #fff;outline-offset:3px}
        .section-title{margin:32px auto 0;color:#fff;font-size:18px;font-weight:600}
        .notes{display:none}
        body:has(.slide:target){background:#fff}
        body:has(.slide:target) .slide:not(:target),body:has(.slide:target) .section-title{display:none}
        .slide:target{margin:0}
        .bg{position:absolute;left:0;top:0}
        .sp{position:absolute;left:0;top:0;transform-origin:0 0}
        .grp{position:absolute;left:0;top:0}
        .sp>svg{position:absolute;left:0;top:0;overflow:visible}
        .sp text{white-space:pre;font-variant-ligatures:none}
        """;

    public static string ToHtml(string pptxPath, FontLocator? fonts = null)
    {
        using var context = new PresentationContext(OpcPackage.Open(pptxPath));
        var deck = new DeckInfo(context);
        var paint = new SvgPaint();
        var width = Units.ToPx(context.SlideWidth);
        var slides = new StringBuilder();
        var data = new List<SlideData>();
        string? section = null;
        for (var i = 0; i < context.SlideParts.Count; i++)
        {
            var number = i + 1;
            var part = context.SlideParts[i];
            var hidden = deck.IsHidden(part);
            var slideSection = deck.SectionOf(number);
            if (slideSection != null && slideSection != section)
                slides.Append($"<h2 class=\"section-title\" style=\"width:{Units.Css(width)}px\">{WebUtility.HtmlEncode(slideSection)}</h2>\n");
            section = slideSection;

            var attributes = new StringBuilder();
            if (hidden) attributes.Append(" data-hidden");
            if (slideSection != null) attributes.Append($" data-section=\"{WebUtility.HtmlEncode(slideSection)}\"");
            var notes = deck.NotesHtml(part) is { } n ? $"<aside class=\"notes\">{n}</aside>" : "";
            slides.Append(new SlideRenderer(new SlideContext(context, part, number), paint).Render(attributes.ToString(), notes)).Append('\n');
            data.Add(new SlideData { Number = number, Hidden = hidden, Section = slideSection });
        }

        var properties = deck.Properties;
        var title = properties.Title ?? Path.GetFileNameWithoutExtension(pptxPath);
        var html = new StringBuilder();
        html.Append("<!DOCTYPE html>\n");
        html.Append(properties.Language is { } language ? $"<html lang=\"{WebUtility.HtmlEncode(language)}\">\n" : "<html>\n");
        html.Append("<head>\n<meta charset=\"utf-8\">\n<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">\n");
        html.Append($"<title>{WebUtility.HtmlEncode(title)}</title>\n");
        html.Append("<meta name=\"generator\" content=\"FreeSlate\">\n");
        foreach (var (name, value) in new[]
                 {
                     ("author", properties.Creator), ("description", properties.Description ?? properties.Subject),
                     ("keywords", properties.Keywords), ("dcterms.created", properties.Created), ("dcterms.modified", properties.Modified),
                 })
        {
            if (value != null)
                html.Append($"<meta name=\"{name}\" content=\"{WebUtility.HtmlEncode(value)}\">\n");
        }
        html.Append("<style>\n").Append(FontFaces(context.UsedFonts, fonts ?? FontLocator.Default)).Append(Css).Append("\n</style>\n</head>\n<body>\n");
        html.Append("<main class=\"deck\">\n").Append(slides).Append("</main>\n");
        html.Append("<script type=\"application/json\" id=\"freeslate-data\">").Append(deck.Json(data)).Append("</script>\n");
        html.Append("</body>\n</html>\n");
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
