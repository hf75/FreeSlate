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
        .sp text.sel{fill-opacity:0}
        .glyphs{position:absolute;left:0;top:0}
        .fs-media{position:absolute;left:0;top:0;width:100%;height:100%;object-fit:fill;visibility:hidden}
        .fs-media.playing{visibility:visible}
        .fs-play{position:fixed;right:16px;bottom:16px;width:44px;height:44px;border:0;border-radius:50%;background:#c43e1c;color:#fff;font-size:18px;cursor:pointer;box-shadow:0 2px 8px rgba(0,0,0,.4);z-index:10}
        body:has(.slide:target) .fs-play{display:none}
        .fs-show{position:fixed;inset:0;background:#000;z-index:2147483647;overflow:hidden;user-select:none}
        .fs-stage{position:absolute;left:0;top:0;transform-origin:0 0;overflow:hidden}
        .fs-stage>.slide{position:absolute!important;left:0;top:0;margin:0!important;display:block!important;opacity:1;outline:none!important}
        .fs-cover{position:absolute;inset:0;opacity:0;pointer-events:none;background:#000}
        .fs-end{display:none;position:absolute;left:0;right:0;top:12px;color:#fff;font:16px system-ui,sans-serif;text-align:center}
        @media print{.fs-play{display:none}}
        """;

    /// <summary>The slide show player (catalog: interactivity.player), embedded in every document.</summary>
    static readonly Lazy<string> Player = new(() =>
    {
        using var stream = typeof(PptxConverter).Assembly.GetManifestResourceStream("player.js")
                           ?? throw new InvalidOperationException("Missing resource player.js");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("</script", "<\\/script", StringComparison.OrdinalIgnoreCase);
    });

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
            var slideData = new SlideData { Number = number, Hidden = hidden, Section = slideSection };
            var slideContext = new SlideContext(context, part, number);
            if (SlideShow.Transition(slideContext) is { } transition)
                slideData.Members.Add($"\"transition\":{transition.ToJsonString()}");
            if (SlideShow.Timing(slideContext) is { } timing)
                slideData.Members.Add($"\"timing\":{timing.ToJsonString()}");
            data.Add(slideData);
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
        html.Append(context.Glyphs.Svg());
        html.Append("<main class=\"deck\">\n").Append(slides).Append("</main>\n");
        html.Append("<script type=\"application/json\" id=\"freeslate-data\">").Append(deck.Json(data)).Append("</script>\n");
        html.Append("<script>").Append(Player.Value).Append("</script>\n");
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
