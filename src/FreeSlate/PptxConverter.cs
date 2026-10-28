using System.Globalization;
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
        iframe.fs-media{border:0}
        .fs-model{position:absolute;left:0;top:0;width:100%;height:100%;visibility:hidden;pointer-events:none}
        .fs-model.playing{visibility:visible}
        .fs-modeled>svg{visibility:hidden}
        .fs-nopic{position:absolute;left:0;top:0;width:100%;height:100%;box-sizing:border-box;border:1px solid #6d6d6d;overflow:hidden;padding:5px 6px;font:11px/13px Tahoma,'Segoe UI',sans-serif;color:#000;white-space:nowrap;pointer-events:none}
        .fs-nopic svg{vertical-align:top;margin-right:5px}
        .fs-play{position:fixed;right:16px;bottom:16px;width:44px;height:44px;border:0;border-radius:50%;background:#c43e1c;color:#fff;font-size:18px;cursor:pointer;box-shadow:0 2px 8px rgba(0,0,0,.4);z-index:10}
        body:has(.slide:target) .fs-play{display:none}
        .fs-show{position:fixed;inset:0;background:#000;z-index:2147483647;overflow:hidden;user-select:none}
        .fs-stage{position:absolute;left:0;top:0;transform-origin:0 0;overflow:hidden;overflow:clip}
        .fs-stage>.slide{position:absolute!important;left:0;top:0;margin:0!important;display:block!important;opacity:1;outline:none!important}
        .fs-cover{position:absolute;inset:0;opacity:0;pointer-events:none;background:#000}
        .fs-end{display:none;position:absolute;left:0;right:0;top:12px;color:#fff;font:16px system-ui,sans-serif;text-align:center}
        .cm-marker{display:none;position:absolute;z-index:5;min-width:22px;height:22px;padding:0 5px;border-radius:11px 11px 11px 2px;color:#fff;font:600 11px/22px system-ui,sans-serif;text-align:center;text-decoration:none;box-shadow:0 1px 3px rgba(0,0,0,.4)}
        .comments{display:none;margin:-8px auto 16px;font:14px/1.4 system-ui,sans-serif;color:#222}
        body.fs-comments .deck .cm-marker{display:block}
        body.fs-comments .comments{display:block}
        body:has(.slide:target) .comments{display:none}
        .cm{background:#fff;border-radius:6px;padding:10px 12px;margin:8px 0;box-shadow:0 1px 3px rgba(0,0,0,.25)}
        .cm:target{outline:2px solid #2b6cb0}
        .cm[data-status=resolved]>header,.cm[data-status=resolved]>.cm-text,.cm[data-status=closed]>header,.cm[data-status=closed]>.cm-text{opacity:.6}
        .cm header{display:flex;align-items:center;gap:8px;flex-wrap:wrap}
        .cm-avatar{display:inline-block;width:24px;height:24px;border-radius:50%;color:#fff;font-size:11px;font-weight:600;line-height:24px;text-align:center}
        .cm time{color:#666;font-size:12px}
        .cm-status{font-size:11px;padding:1px 6px;border-radius:8px;background:#e6f4ea;color:#1e6b34}
        .cm-text p{margin:4px 0 0}
        .cm-reply{margin:8px 0 0 32px;padding-top:8px;border-top:1px solid #e5e5e5}
        .cm-task{margin-top:6px;font-size:12px;color:#333;background:#eef3fb;padding:4px 8px;border-radius:4px}
        .cm-reactions{margin-top:6px;display:flex;gap:6px}
        .cm-reactions span{font-size:12px;background:#f1f1f1;border-radius:10px;padding:1px 8px}
        .fs-comments-toggle{position:fixed;right:72px;bottom:16px;height:44px;padding:0 14px;border:0;border-radius:22px;background:#fff;color:#222;font:600 14px system-ui,sans-serif;cursor:pointer;box-shadow:0 2px 8px rgba(0,0,0,.4);z-index:10}
        body.fs-comments .fs-comments-toggle{background:#2b6cb0;color:#fff}
        body:has(.slide:target) .fs-comments-toggle{display:none}
        @media print{.fs-play,.fs-comments-toggle{display:none}}
        """;

    /// <summary>The slide show player (catalog: interactivity.player), embedded in every document.</summary>
    static readonly Lazy<string> Player = new(() => Script("player.js"));

    /// <summary>The glTF reader and WebGL renderer of 3D models (catalog: picture.model3d), in documents that have one.</summary>
    static readonly Lazy<string> Models = new(() => Script("model3d.js"));

    static string Script(string name)
    {
        using var stream = typeof(PptxConverter).Assembly.GetManifestResourceStream(name)
                           ?? throw new InvalidOperationException("Missing resource " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("</script", "<\\/script", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The presentation's slides as scenes (catalog: engine.output), each its size in points and its items as the HTML
    /// draws them; the slides the slide show leaves out only where <paramref name="hidden"/> asks for them.
    /// </summary>
    /// <param name="pixels">The device pixels the slides are drawn on, as in <see cref="ToHtml"/>; the scene keeps their size.</param>
    /// <exception cref="PasswordException">The presentation needs a password, or the password is wrong.</exception>
    public static List<Rendering.PageScene> ToScenes(string pptxPath, FontLocator? fonts = null, string? password = null, bool embeddedFontsFirst = false,
        CultureInfo? culture = null, bool hidden = false, Rendering.PixelGrid? pixels = null)
    {
        using var context = new PresentationContext(OpcPackage.Open(pptxPath, password), fonts, embeddedFontsFirst)
        {
            Culture = culture ?? CultureInfo.CurrentCulture,
            Pixels = pixels ?? Rendering.PixelGrid.Css,
        };
        var deck = new DeckInfo(context);
        var paint = new SvgPaint();
        var scenes = new List<Rendering.PageScene>();
        for (var i = 0; i < context.SlideParts.Count; i++)
        {
            var part = context.SlideParts[i];
            if (!hidden && deck.IsHidden(part))
                continue;
            var (width, height, items) = new SlideRenderer(new SlideContext(context, part, i + 1), paint).Scene();
            var scene = new Rendering.PageScene { Number = i + 1, Paper = "", Width = width * 72 / 96, Height = height * 72 / 96, Glyphs = context.Glyphs };
            scene.Items.AddRange(items);
            scenes.Add(scene);
        }
        return scenes;
    }

    /// <summary>The HTML document of a presentation; a password-protected one needs its password.</summary>
    /// <param name="embeddedFontsFirst">
    /// Use the fonts embedded in the presentation even where the same family is installed (as on a computer without them).
    /// </param>
    /// <param name="culture">
    /// The regional format of numbers and dates in charts, the system's (<see cref="CultureInfo.CurrentCulture"/>) as in
    /// PowerPoint when null.
    /// </param>
    /// <param name="pixels">
    /// The device pixels the slides are drawn on, by default their own: each slide is scaled to them, outlines and
    /// rectangles drawn where PowerPoint's antialiasing samples them and text snapped to them (see PixelGrid, which
    /// also tells how to draw a slide as PowerPoint's own picture of it).
    /// </param>
    /// <exception cref="PasswordException">The presentation needs a password, or the password is wrong.</exception>
    public static string ToHtml(string pptxPath, FontLocator? fonts = null, string? password = null, bool embeddedFontsFirst = false,
        CultureInfo? culture = null, Rendering.PixelGrid? pixels = null)
    {
        using var context = new PresentationContext(OpcPackage.Open(pptxPath, password), fonts, embeddedFontsFirst)
        {
            Culture = culture ?? CultureInfo.CurrentCulture,
            Pixels = pixels ?? Rendering.PixelGrid.Css,
        };
        var deck = new DeckInfo(context);
        var paint = new SvgPaint();
        var width = Units.ToPx(context.SlideWidth);
        var slides = new StringBuilder();
        var data = new List<SlideData>();
        string? section = null;
        // Morph (catalog: transition.morph) pairs the shapes of a slide and the one before it by name, their words or
        // letters by text: both slides carry their shapes' names, and their text in units when morph goes by them.
        var morphs = Enumerable.Range(0, context.SlideParts.Count)
            .Select(i => SlideShow.Transition(new SlideContext(context, context.SlideParts[i], i + 1)) is { } t && t["type"]?.GetValue<string>() == "morph"
                ? t["option"]?.GetValue<string>() ?? "byObject" : null)
            .ToList();
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
            var comments = Comments.Read(context, part);
            var morph = new[] { morphs[i], i + 1 < morphs.Count ? morphs[i + 1] : null }.Where(m => m != null)
                .OrderByDescending(m => m == "byChar" ? 2 : m == "byWord" ? 1 : 0).FirstOrDefault();
            slides.Append(new SlideRenderer(new SlideContext(context, part, number), paint) { Morph = morph }.Render(attributes.ToString(), notes + Comments.Markers(comments, number)))
                .Append(Comments.Panel(comments, number, width)).Append('\n');
            var slideData = new SlideData { Number = number, Hidden = hidden, Section = slideSection };
            var slideContext = new SlideContext(context, part, number);
            if (SlideShow.Transition(slideContext) is { } transition)
                slideData.Members.Add($"\"transition\":{transition.ToJsonString()}");
            if (SlideShow.Timing(slideContext) is { } timing)
                slideData.Members.Add($"\"timing\":{timing.ToJsonString()}");
            if (SlideShow.Laser(slideContext) is { } laser)
                slideData.Members.Add($"\"laser\":{laser.ToJsonString()}");
            if (SlideShow.Events(slideContext) is { } events)
                slideData.Members.Add($"\"events\":{events.ToJsonString()}");
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
        html.Append("<style>\n").Append(FontFaces(context.UsedFonts, context.FontLocator)).Append(Css).Append("\n</style>\n</head>\n<body>\n");
        html.Append(context.Glyphs.Svg());
        html.Append("<main class=\"deck\">\n").Append(slides).Append("</main>\n");
        html.Append("<script type=\"application/json\" id=\"freeslate-data\">").Append(deck.Json(data)).Append("</script>\n");
        if (context.HasModels)
            html.Append("<script>").Append(Models.Value).Append("</script>\n");
        html.Append("<script>").Append(Player.Value).Append("</script>\n");
        html.Append("</body>\n</html>\n");
        return html.ToString();
    }

    public static void ToFile(string pptxPath, string htmlPath, FontLocator? fonts = null, string? password = null, bool embeddedFontsFirst = false,
        CultureInfo? culture = null, Rendering.PixelGrid? pixels = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(htmlPath))!);
        File.WriteAllText(htmlPath, ToHtml(pptxPath, fonts, password, embeddedFontsFirst, culture, pixels));
    }

    /// <summary>
    /// @font-face rules for used fonts that the browser cannot find by name: the fonts the presentation embeds
    /// (catalog: text.embeddedFonts) and fonts that are not installed, such as Office cloud fonts.
    /// </summary>
    internal static string FontFaces(IEnumerable<string> families, FontLocator fonts)
    {
        var css = new StringBuilder();
        foreach (var family in families.Order(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var face in fonts.Pinned(family).DistinctBy(f => (f.Bold, f.Italic)))
            {
                var names = string.Join(",", face.FullNames.Select(n => $"local('{n.Replace("'", "\\'")}')"));
                css.Append($"@font-face{{font-family:'{family.Replace("'", "\\'")}';src:{names};" +
                           $"font-weight:{(face.Bold ? 700 : 400)};font-style:{(face.Italic ? "italic" : "normal")}}}\n");
            }
            foreach (var face in fonts.Embeddable(family).DistinctBy(f => (f.Bold, f.Italic)))
            {
                var bytes = face.Embedded != null ? face.Embedded.Value : File.ReadAllBytes(face.Path);
                if (bytes == null)
                    continue;
                var data = Convert.ToBase64String(bytes);
                var format = bytes is [(byte)'O', (byte)'T', (byte)'T', (byte)'O', ..] ? "opentype" : "truetype";
                css.Append($"@font-face{{font-family:'{family.Replace("'", "\\'")}';src:url(data:font/{format};base64,{data}) format('{format}');" +
                           $"font-weight:{(face.Bold ? 700 : 400)};font-style:{(face.Italic ? "italic" : "normal")}}}\n");
            }
        }
        return css.ToString();
    }
}
