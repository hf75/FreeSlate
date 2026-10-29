using System.Net;
using System.Text;
using FreeSlate.Text;

namespace FreeSlate.Rendering;

/// <summary>
/// Writes pages as HTML (catalog: engine.output): a section the page's size holding the drawings behind the text, an SVG
/// canvas with the page's own drawing, then the drawings in front, the pictures' alternative texts and the bookmarks.
/// </summary>
public static class HtmlScene
{
    const double PxPerPt = 96.0 / 72.0;

    static string Px(double points) => Units.Css(points * PxPerPt);

    /// <summary>The page's HTML section.</summary>
    public static string Page(PageScene scene)
    {
        var html = new StringBuilder();
        // With the markup pane the page is as wide as the pane reaches; printed, it is made smaller to fit the paper's
        // width, standing in the middle of its height (as Word prints with markup).
        if (scene.Sheet is { } sheet)
        {
            html.Append($"<section class=\"page markup {scene.Paper}\" id=\"page-{scene.Number}\" style=\"width:{Px(scene.Width)}px;height:{Px(scene.Height)}px;"
                + $"--pw:{Px(sheet.PaperWidth)}px;--s:{Units.Css4(sheet.Scale)};--oy:{Units.Css(sheet.Offset)}px\">");
            html.Append($"<div class=\"sheet\" style=\"width:{Px(scene.Width)}px;height:{Px(scene.Height)}px\">");
        }
        else
            html.Append($"<section class=\"page {scene.Paper}\" id=\"page-{scene.Number}\" style=\"width:{Px(scene.Width)}px;height:{Px(scene.Height)}px\">");
        Write(scene.Items, html);
        if (scene.Sheet != null)
            html.Append("</div>");
        html.Append("</section>\n");
        return html.ToString();
    }

    /// <summary>An item as HTML or SVG.</summary>
    public static string Markup(SceneItem item)
    {
        var html = new StringBuilder();
        Write([item], html);
        return html.ToString();
    }

    /// <summary>Items as HTML or SVG.</summary>
    public static string Markup(IEnumerable<SceneItem> items)
    {
        var html = new StringBuilder();
        Write(items, html);
        return html.ToString();
    }

    /// <summary>
    /// PowerPoint's sign for a picture it cannot show (see SlideRenderer.PowerPointSign; class fs-nopic in the style
    /// sheet): on white the frame, in its clip 7 pixels in the icon, PowerPoint's bitmap (a 14 × 16 raised box with a
    /// 6 × 5 red cross), and the message, which the style sheet cuts with an ellipsis 11 pixels before the frame's right
    /// edge (as GDI+ trims it) and makes 1-bit (its coverage from 40 % black, else clear). Where the item has a box on
    /// the slide's pixels, it is moved there (a translation of its own: the browser rounds borders and clips in the
    /// coordinates of the shape, which may stand between pixels).
    /// </summary>
    static string PowerPointMissingPicture(MissingPictureItem missing)
    {
        var box = missing.Box is var (x, y, width, height)
            ? $" style=\"width:{Units.Css(width)}px;height:{Units.Css(height)}px;transform:translate({Units.Css(x)}px,{Units.Css(y)}px)\"" : "";
        var text = Math.Abs(missing.TextLeft - 27.25) > 0.005 ? $" style=\"left:{Units.Css(missing.TextLeft - 7)}px\"" : "";
        return $"<div class=\"fs-nopic\"{box}><div><svg width=\"14\" height=\"16\" viewBox=\"0 0 14 16\">" +
               "<path fill=\"#808080\" d=\"M0 0h14v1H1v14H0z\"/><path fill=\"#000\" d=\"M13 1h1v15H0v-1h13z\"/>" +
               "<path fill=\"#c0c0c0\" d=\"M12 1h1v14H1v-1h11z\"/><path fill=\"#fff\" d=\"M1 1h11v13H1z\"/>" +
               "<path fill=\"#f00\" d=\"M4 5h2v1H4zM8 5h2v1H8zM5 6h4v1H5zM6 7h2v1H6zM5 8h4v1H5zM4 9h2v1H4zM8 9h2v1H8z\"/></svg>" +
               $"<span{text}>The picture can't be displayed.</span></div></div>";
    }

    /// <summary>
    /// Word's sign for a picture it cannot show: on white its broken-picture icon and the message in 1.2 pt Tahoma
    /// inside four 0.14 pt black lines centred 0.06 pt in from the edge, which the message's clipping leaves whole
    /// (docx/external/libreoffice/extras-ooxmlimport/linked-picture, extras-ooxmlexport/FileWithInvalidImageLink,
    /// tdf115883 77.5 -> 99.8 %, tdf59699, found by agent BM).
    /// </summary>
    static string WordMissingPicture(string message) =>
        "<div class=\"fs-nolink\" style=\"position:absolute;left:0;top:0;width:100%;height:100%;background:#fff;pointer-events:none\">" +
        "<div style=\"position:absolute;left:0;top:0;width:100%;height:100%;box-sizing:border-box;overflow:hidden;padding:.79pt 3.5pt 0 3.44pt;font:1.2pt/1.45pt Tahoma,'Segoe UI',sans-serif;color:#000\">" +
        WebUtility.HtmlEncode(message) + "</div>" +
        "<svg style=\"position:absolute;left:0;top:0;width:100%;height:100%;overflow:visible\"><g stroke=\"#000\" stroke-width=\".14pt\">" +
        "<line x1=\"0\" y1=\".06pt\" x2=\"100%\" y2=\".06pt\"/><line x1=\"0\" y1=\"100%\" x2=\"100%\" y2=\"100%\" transform=\"translate(0 -.08)\"/>" +
        "<line x1=\".06pt\" y1=\"0\" x2=\".06pt\" y2=\"100%\"/><line x1=\"100%\" y1=\"0\" x2=\"100%\" y2=\"100%\" transform=\"translate(-.08 0)\"/></g>" +
        "<svg x=\".84pt\" y=\".84pt\" width=\"1.68pt\" height=\"1.92pt\" viewBox=\"0 0 4 5\">" +
        "<rect x=\".5\" y=\".5\" width=\"3\" height=\"4\" fill=\"#fff\" stroke=\"#808080\"/><rect x=\"1.5\" y=\"1.5\" width=\"1\" height=\"1\" fill=\"#e00000\"/></svg></svg></div>";

    /// <summary>Items as HTML and SVG, in their order.</summary>
    public static void Write(IEnumerable<SceneItem> items, StringBuilder html)
    {
        foreach (var item in items)
        {
            switch (item)
            {
                case MarkupItem markup:
                    html.Append(markup.Html);
                    break;
                case CanvasItem canvas:
                    html.Append($"<svg width=\"{Px(canvas.Width)}\" height=\"{Px(canvas.Height)}\" viewBox=\"0 0 {Px(canvas.Width)} {Px(canvas.Height)}\">");
                    Write(canvas.Items, html);
                    html.Append("</svg>");
                    break;
                case TurnedItem turned:
                    html.Append($"<g transform=\"translate({Px(turned.Width)} 0) rotate(90)\">");
                    Write(turned.Items, html);
                    html.Append("</g>");
                    break;
                case ClipItem clip:
                    html.Append($"<div style=\"position:absolute;overflow:hidden;left:{Px(clip.X)}px;top:{Px(clip.Y)}px;width:{Px(clip.Width)}px;height:{Px(clip.Height)}px\">"
                        + $"<div style=\"position:absolute;left:{Px(-clip.X)}px;top:{Px(-clip.Y)}px\">");
                    Write(clip.Items, html);
                    html.Append("</div></div>");
                    break;
                case BitmapItem bitmap:
                    html.Append($"<image x=\"{Px(bitmap.X)}\" y=\"{Px(bitmap.Y)}\" width=\"{Px(bitmap.Width)}\" height=\"{Px(bitmap.Height)}\" preserveAspectRatio=\"none\" href=\"{Metafiles.Png.DataUri(bitmap.PixelWidth, bitmap.PixelHeight, bitmap.Rgba)}\"/>");
                    break;
                case BoxItem box:
                    html.Append($"<div class=\"sp\"{box.Attributes} style=\"");
                    if (box.Size is { } size)
                        html.Append($"width:{Units.Css(size.Width)}px;height:{Units.Css(size.Height)}px");
                    if (box.Transform is { } transform)
                        html.Append(box.Size != null ? ";" : "").Append("transform:").Append(transform.Css());
                    html.Append("\">");
                    Write(box.Items, html);
                    html.Append("</div>");
                    break;
                case SvgItem svg:
                    html.Append($"<svg width=\"{Units.Css(svg.Width)}\" height=\"{Units.Css(svg.Height)}\" viewBox=\"0 0 {Units.Css(svg.Width)} {Units.Css(svg.Height)}\">");
                    Write(svg.Items, html);
                    html.Append("</svg>");
                    break;
                case GroupBoxItem group:
                    html.Append($"<div class=\"grp\"{group.Attributes}>");
                    Write(group.Items, html);
                    html.Append("</div>");
                    break;
                case ShapePathItem shapePath:
                    html.Append($"<path{shapePath.Lead} d=\"{shapePath.Data}\"{shapePath.Attributes}/>");
                    break;
                case ShapeRectItem { AtOrigin: true } atOrigin:
                    html.Append($"<rect width=\"{Units.Css(atOrigin.Width)}\" height=\"{Units.Css(atOrigin.Height)}\"{atOrigin.Attributes}/>");
                    break;
                case ShapeRectItem shapeRect:
                    html.Append($"<rect x=\"{Units.Css(shapeRect.X)}\" y=\"{Units.Css(shapeRect.Y)}\" width=\"{Units.Css(shapeRect.Width)}\" height=\"{Units.Css(shapeRect.Height)}\"{shapeRect.Attributes}/>");
                    break;
                case BackgroundItem background:
                    html.Append($"<svg class=\"bg\" width=\"{Units.Css(background.Width)}\" height=\"{Units.Css(background.Height)}\">");
                    if (background.Defs.Length > 0)
                        html.Append("<defs>").Append(background.Defs).Append("</defs>");
                    html.Append($"<rect width=\"{Units.Css(background.Width)}\" height=\"{Units.Css(background.Height)}\"{background.Paint}/></svg>");
                    break;
                case ImageItem image:
                    html.Append($"<image href=\"{image.Source}\" x=\"{Units.Css(image.X)}\" y=\"{Units.Css(image.Y)}\" width=\"{Units.Css(image.Width)}\" height=\"{Units.Css(image.Height)}\" preserveAspectRatio=\"none\"/>");
                    break;
                case GlyphRunItem glyphRun:
                    WriteGlyphRun(glyphRun, html);
                    break;
                case SvgTextItem svgText:
                    WriteSvgText(svgText, html);
                    break;
                case ShapeEllipseItem ellipse:
                    html.Append($"<ellipse cx=\"{Units.Css(ellipse.X)}\" cy=\"{Units.Css(ellipse.Y)}\" rx=\"{Units.Css(ellipse.RadiusX)}\" ry=\"{Units.Css(ellipse.RadiusY)}\" "
                        + $"transform=\"rotate({Units.Css(ellipse.Angle)} {Units.Css(ellipse.X)} {Units.Css(ellipse.Y)})\"{ellipse.Attributes}/>");
                    break;
                case DefsItem defined:
                    html.Append("<defs>").Append(defined.Markup).Append("</defs>");
                    break;
                case PictureItem picture:
                    html.Append($"<g clip-path=\"url(#{picture.Clip})\"><image href=\"{picture.Source}\" x=\"{Units.Css(picture.X)}\" y=\"{Units.Css(picture.Y)}\" "
                        + $"width=\"{Units.Css(picture.Width)}\" height=\"{Units.Css(picture.Height)}\" preserveAspectRatio=\"none\"{picture.Filter}/></g>");
                    break;
                case MissingPictureItem missing:
                    html.Append(missing.Message is { } message ? WordMissingPicture(message) : PowerPointMissingPicture(missing));
                    break;
                case TransformItem transformed:
                    html.Append($"<g transform=\"{transformed.Css ?? transformed.Transform.Css()}\">");
                    Write(transformed.Items, html);
                    html.Append("</g>");
                    break;
                case SvgGroupItem grouped:
                    html.Append($"<g{grouped.Attributes}>");
                    Write(grouped.Items, html);
                    html.Append("</g>");
                    break;
                case WrappedItem wrapped:
                    html.Append(wrapped.Before);
                    Write(wrapped.Items, html);
                    html.Append(wrapped.After);
                    break;
                case RectItem rect:
                    html.Append($"<rect x=\"{Px(rect.X)}\" y=\"{Px(rect.Y)}\" width=\"{Px(rect.Width)}\" height=\"{Px(rect.Height)}\" fill=\"{rect.Color.ToHex()}\"");
                    if (rect.Color.A < 0.999)
                        html.Append($" fill-opacity=\"{Fine(rect.Color.A)}\"");
                    html.Append("/>");
                    break;
                case RuleItem rule:
                    html.Append($"<rect x=\"{Px(rule.X)}\" y=\"{Px(rule.Y)}\" width=\"{Px(rule.Width)}\" height=\"{Px(rule.Height)}\" fill=\"#000\"/>");
                    break;
                case RotatedItem rotated:
                    html.Append($"<g transform=\"rotate({Units.Css(rotated.Angle)} {Px(rotated.PivotX)} {Px(rotated.PivotY)})\">");
                    Write(rotated.Items, html);
                    html.Append("</g>");
                    break;
                case LinkItem link:
                    html.Append($"<a href=\"{WebUtility.HtmlEncode(link.Target)}\">");
                    Write(link.Items, html);
                    html.Append("</a>");
                    break;
                case CheckBoxItem box:
                    html.Append($"<rect x=\"{Px(box.X)}\" y=\"{Px(box.Y)}\" width=\"{Px(box.Side)}\" height=\"{Px(box.Side)}\" fill=\"none\" stroke=\"#000\" stroke-width=\"{Px(0.48)}\"/>");
                    if (box.Checked)
                        html.Append($"<path d=\"M{Px(box.X)} {Px(box.Y)}L{Px(box.X + box.Side)} {Px(box.Bottom)}M{Px(box.X + box.Side)} {Px(box.Y)}L{Px(box.X)} {Px(box.Bottom)}\" stroke=\"#000\" stroke-width=\"{Px(0.48)}\"/>");
                    break;
                case LineItem line:
                    html.Append($"<line x1=\"{Px(line.X1)}\" y1=\"{Px(line.Y1)}\" x2=\"{Px(line.X2)}\" y2=\"{Px(line.Y2)}\" stroke=\"{line.Color.ToHex()}\" stroke-width=\"{Px(line.Width)}\"/>");
                    break;
                case DashesItem dashed:
                    foreach (var (x1, y1, x2, y2) in dashed.Dashes)
                        html.Append($"<path d=\"M{Px(x1)} {Px(y1)}L{Px(x2)} {Px(y2)}\" stroke=\"{dashed.Color.ToHex()}\" stroke-width=\"{Px(dashed.Width)}\"/>");
                    break;
                case PathsItem paths:
                    html.Append($"<g transform=\"scale({Units.Css4(PxPerPt)})\">");
                    foreach (var path in paths.Paths)
                    {
                        var stroke = path.Stroke is { } color ? $" stroke=\"{color.ToHex()}\" stroke-width=\"{Units.Css4(path.StrokeWidth)}\"" : "";
                        html.Append($"<path d=\"{path.Data}\" fill=\"{path.Fill?.ToHex() ?? "none"}\"{stroke}/>");
                    }
                    html.Append("</g>");
                    break;
                case CurveItem curve:
                    html.Append($"<path d=\"M{Px(curve.Start.X)} {Px(curve.Start.Y)}");
                    foreach (var (first, second, end) in curve.Curves)
                        html.Append($"C{Px(first.X)} {Px(first.Y)} {Px(second.X)} {Px(second.Y)} {Px(end.X)} {Px(end.Y)}");
                    html.Append($"\" fill=\"none\" stroke=\"{curve.Color.ToHex()}\" stroke-width=\"{Px(curve.Width)}\"/>");
                    break;
                case FilterItem filtered:
                    html.Append($"<g filter=\"url(#{filtered.Filter})\">");
                    Write(filtered.Items, html);
                    html.Append("</g>");
                    break;
                case FilterCopyItem { Bare: true } copied:
                    html.Append($"<use href=\"#{copied.Id}\" filter=\"url(#{copied.Filter})\"/>");
                    Write(copied.Items, html);
                    break;
                case FilterCopyItem copied:
                    html.Append($"<use href=\"#{copied.Id}\" filter=\"url(#{copied.Filter})\"/><g id=\"{copied.Id}\">");
                    Write(copied.Items, html);
                    html.Append("</g>");
                    break;
                case ReflectionItem reflected:
                    html.Append($"<g id=\"rf{reflected.Reflection.Id}\">");
                    Write(reflected.Items, html);
                    html.Append("</g>").Append(Html.SvgEffects.Reflected(reflected.Reflection.Id, reflected.Reflection.Transform, reflected.Reflection.Blurred));
                    break;
                case AltTextItem alt:
                    html.Append($"<div class=\"alt\" role=\"img\" aria-label=\"{WebUtility.HtmlEncode(alt.Text)}\" style=\"left:{Px(alt.X)}px;top:{Px(alt.Y)}px;width:{Px(alt.Width)}px;height:{Px(alt.Height)}px\"></div>");
                    break;
                case BookmarkItem bookmark:
                    html.Append($"<a class=\"bm\" id=\"{WebUtility.HtmlEncode(bookmark.Name)}\" style=\"left:{Px(bookmark.X)}px;top:{Px(bookmark.Y)}px\"></a>");
                    break;
                case PlacesItem:
                    break;
                case TextItem text:
                    WriteText(text, html);
                    break;
                case GlyphsItem glyphs:
                    WriteGlyphs(glyphs, html);
                    break;
                case MathGlyphItem math:
                    WriteMathGlyph(math, html);
                    break;
                case HiddenSpanItem span:
                    html.Append($"<text class=\"sel\" x=\"{Px(span.X)}\" y=\"{Px(span.Y)}\" font-size=\"{Px(span.Size)}\" textLength=\"{Px(span.Width)}\" lengthAdjust=\"spacingAndGlyphs\">{WebUtility.HtmlEncode(span.Text)}</text>");
                    break;
                case HiddenTextItem hidden:
                {
                    var xs = string.Join(' ', Enumerable.Range(0, hidden.Text.Length).Where(i => !char.IsLowSurrogate(hidden.Text[i]))
                        .Select(i => Units.Css((hidden.X + hidden.Positions[i]) * PxPerPt)));
                    html.Append($"<text class=\"sel\" x=\"{xs}\" y=\"{Px(hidden.Y)}\" font-family=\"{Family(hidden.Font)}\" font-size=\"{Px(hidden.Size)}\">{WebUtility.HtmlEncode(hidden.Text)}</text>");
                    break;
                }
                case LabelItem label:
                    html.Append($"<text x=\"{Px(label.X)}\" y=\"{Px(label.Y)}\" text-anchor=\"end\" font-family=\"{Family(label.Font)}\" font-size=\"{Px(label.Size)}\" fill=\"{label.Color.ToHex()}\"");
                    if (label.Bold) html.Append(" font-weight=\"700\"");
                    if (label.Italic) html.Append(" font-style=\"italic\"");
                    html.Append($">{WebUtility.HtmlEncode(label.Text)}</text>");
                    break;
                case AcrossItem across:
                    html.Append($"<g transform=\"translate({Px(across.X)} {Px(across.Y)}) rotate(-90)\">");
                    Write(across.Items, html);
                    html.Append("</g>");
                    break;
                case DecorationItem decoration:
                    html.Append("<g aria-hidden=\"true\" style=\"user-select:none\">");
                    Write(decoration.Items, html);
                    html.Append("</g>");
                    break;
                default:
                    throw new NotSupportedException(item.GetType().Name);
            }
        }
    }

    /// <summary>A font's family as an SVG font-family.</summary>
    static string Family(FontData font) => $"'{WebUtility.HtmlEncode(font.Family.Replace("'", "\\'"))}'";

    /// <summary>
    /// The axes of a named instance of a variable font (FontData.Variations) as a declaration to append to a style, ""
    /// for other fonts: the browser finds the file by the family and sets the instance's axes.
    /// </summary>
    public static string Variations(FontData font) => font.Variations is { } axes
        ? ";font-variation-settings:" + string.Join(',', axes.Select(a => $"'{a.Axis}' {Units.Css(a.Value)}"))
        : "";

    /// <summary>A run of text as text the browser draws (and selects), every character at its position.</summary>
    static void WriteText(TextItem item, StringBuilder html)
    {
        var text = item.Text;
        var (x, positions) = (item.X, item.Positions);
        var on = item.Features.Where(f => !f.StartsWith('-') && f != "kern").ToList();
        // Scaled text (w:w) is drawn scaled about its start, which scales the places it is written at too: they hold the
        // scale already, so they are written without it (docx/generated/character-formats: "scaled 150 %" spread out
        // over the run after it).
        var scale = Math.Abs(item.Scale - 1) > 1e-6 ? item.Scale : 1;
        string X(int from, int to) => string.Join(' ', Enumerable.Range(from, to - from).Where(i => !char.IsLowSurrogate(text[i]))
            .Select(i => Units.Css((x + positions[i] / scale) * PxPerPt)));
        // Words whose glyphs depend on their neighbors (ligatures, contextual alternates and swashes; catalog:
        // run.openType) must reach the browser whole: it shapes every character that has a position of its own alone
        // (measured: Chrome draws "office" without its ffi ligature when each character has its x). Such a word gets
        // the position of its start and, for every cluster, how far it stands from where the browser's own advances
        // put it; the browser's advances differ from these by up to 0.01 pt a glyph, so the other words keep the
        // position of every character.
        // Right-to-left text goes to the browser whole, from its right end, so that it is shaped and reads in order
        // (as in PowerPoint); justification widens its spaces.
        var words = item.RightToLeft ? [] : Math.Abs(item.Scale - 1) < 1e-6 ? ContextualWords(item) : [];
        html.Append("<text");
        if (item.RightToLeft)
        {
            html.Append($" x=\"{Px(x + item.Width / scale)}\" direction=\"rtl\" unicode-bidi=\"bidi-override\"");
            // Justification widens its spaces, and a line that squeezes them narrows them (Skia alike).
            if (Math.Abs(item.WordSpacing) > 1e-9)
                html.Append($" word-spacing=\"{Px(item.WordSpacing / scale)}\"");
        }
        else if (words.Count == 0)
            html.Append($" x=\"{X(0, text.Length)}\"");
        var paint = item.Paint ?? $" fill=\"{item.Color.ToHex()}\"";
        html.Append($" y=\"{Px(item.Y)}\" font-family=\"{Family(item.Font)}\" font-size=\"{Px(item.Size)}\"{paint}");
        if (item.StrokedBold)
            html.Append($" stroke=\"{item.Color.ToHex()}\" stroke-width=\"{Px(item.Size / 35)}\" stroke-miterlimit=\"10\"");
        else if (item.Bold) html.Append(" font-weight=\"700\"");
        if (item.Italic) html.Append(" font-style=\"italic\"");
        // Our positions already hold the kerning; the browser must not add its own. The features the run turns on are
        // the browser's to apply too. Right-to-left text the browser shapes itself: Arabic with its kerning and
        // ligatures, as Word sets it (FontData.FontFeaturesAlways).
        html.Append(item.RightToLeft && FontData.FontFeaturesAlways(text)
            ? " style=\"font-kerning:normal;font-variant-ligatures:normal" : " style=\"font-kerning:none");
        if (on.Count > 0)
            html.Append(";font-feature-settings:").Append(string.Join(',', on.Select(f => $"'{f}' 1")));
        html.Append(Variations(item.Font)).Append('"');
        if (Math.Abs(item.Scale - 1) > 1e-6)
            html.Append($" transform=\"translate({Units.Css(x * PxPerPt)} 0) scale({Units.Css4(item.Scale)} 1) translate({Units.Css(-x * PxPerPt)} 0)\"");
        html.Append('>');
        // The browser joins Arabic letters across bidirectional controls, Word does not (FontData.NonJoining); the
        // piece is drawn with its direction overridden, so they have nothing else to do there.
        // A word over several runs keeps its letters joined at the runs' ends: zero width joiners there (as Word draws
        // the word in one, docx/external/powertools/RP/RP051-Arabic).
        if (words.Count == 0)
            html.Append(WebUtility.HtmlEncode((item.RightToLeft && item.JoinsBefore ? "\u200D" : "")
                + (item.RightToLeft && FontData.FontFeaturesAlways(text) ? FontData.NonJoining(text) : text)
                + (item.RightToLeft && item.JoinsAfter ? "\u200D" : "")));
        else
        {
            var at = 0;
            foreach (var (from, to) in words)
            {
                if (from > at)
                    html.Append($"<tspan x=\"{X(at, from)}\">{WebUtility.HtmlEncode(text[at..from])}</tspan>");
                html.Append($"<tspan x=\"{Units.Css((x + positions[from]) * PxPerPt)}\" dx=\"{Shifts(item, text[from..to], positions.AsSpan(from, to - from + 1))}\">{WebUtility.HtmlEncode(text[from..to])}</tspan>");
                at = to;
            }
            if (at < text.Length)
                html.Append($"<tspan x=\"{X(at, text.Length)}\">{WebUtility.HtmlEncode(text[at..])}</tspan>");
        }
        html.Append("</text>");
    }

    /// <summary>
    /// The words of a run (each with the spaces after it) holding a glyph that depends on its neighbors: a cluster of
    /// several characters, or a glyph other than the character's own with the run's features. Besides ligatures and
    /// alternates the run turns on, these are the clusters every font forms: combining marks, emoji sequences and the
    /// conjuncts and contextual forms of complex scripts (catalog: run.complexScript, engine.fonts).
    /// </summary>
    static List<(int From, int To)> ContextualWords(TextItem item)
    {
        var (text, features) = (item.Text, item.Features);
        var result = new List<(int, int)>();
        if (!features.Any(f => f is "liga" or "clig" or "calt" or "hlig" or "dlig" || f.StartsWith("ss", StringComparison.Ordinal))
            && !text.Any(c => c >= 0x300))
            return result;
        var data = item.Font;
        var glyphs = data.GlyphsWithFeatures(text, features, language: item.Language);
        var contextual = new bool[text.Length];
        var byCluster = glyphs.Where(g => g.Cluster < text.Length).GroupBy(g => g.Cluster).OrderBy(g => g.Key).ToList();
        for (var k = 0; k < byCluster.Count; k++)
        {
            var start = byCluster[k].Key;
            var end = k + 1 < byCluster.Count ? byCluster[k + 1].Key : text.Length;
            var own = (end - start == 1 || end - start == 2 && char.IsHighSurrogate(text[start])) && byCluster[k].Count() == 1;
            if (own)
            {
                var alone = data.GlyphsWithFeatures(text[start..end], features, language: item.Language);
                own = alone.Length == 1 && alone[0].Id == byCluster[k].First().Id;
            }
            if (!own)
            {
                for (var i = start; i < end; i++)
                    contextual[i] = true;
            }
        }
        var from = 0;
        for (var i = 1; i <= text.Length; i++)
        {
            if (i < text.Length && !(text[i] != ' ' && text[i - 1] == ' '))
                continue;
            if (contextual.AsSpan(from, i - from).Contains(true))
                result.Add((from, i));
            from = i;
        }
        return result;
    }

    /// <summary>
    /// The dx list of a word the browser shapes whole: per character (code point), how far the cluster it begins
    /// stands from where the browser's own unkerned advances put it, 0 within clusters.
    /// </summary>
    /// <param name="positions">The positions of the word's characters and of its end.</param>
    static string Shifts(TextItem item, string text, ReadOnlySpan<double> positions)
    {
        var features = item.Features.Select(f => f == "kern" ? "-kern" : f).ToList();
        var natural = new double[text.Length];
        var starts = new SortedSet<int>();
        foreach (var g in item.Font.GlyphsWithFeatures(text, features, language: item.Language))
        {
            if (g.Cluster >= text.Length)
                continue;
            natural[g.Cluster] += g.Advance * item.Size;
            starts.Add(g.Cluster);
        }
        // Shifts add up: each is what the cluster needs less what the ones before gave, rounded as written.
        var shifts = new List<string>();
        var browser = 0.0;   // where the browser's advances put the cluster, from the first
        var given = 0.0;     // the shifts written so far, in pixels
        var previous = -1;
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsLowSurrogate(text[i]))
                continue;
            var shift = 0.0;
            if (starts.Contains(i))
            {
                if (previous >= 0)
                {
                    browser += natural[previous];
                    shift = Math.Round((positions[i] - positions[0] - browser) * PxPerPt - given, 2);
                    given += shift;
                }
                previous = i;
            }
            shifts.Add(shift == 0 ? "0" : Units.Css(shift));
        }
        // Trailing zeros say nothing.
        var count = shifts.Count;
        while (count > 1 && shifts[count - 1] == "0")
            count--;
        return string.Join(' ', shifts.Take(count));
    }

    /// <summary>Glyphs from the document's paths of their outlines (see GlyphCache), placed in font units.</summary>
    /// <summary>A run of slide text's glyphs as uses of their outlines (see GlyphRunItem).</summary>
    static void WriteGlyphRun(GlyphRunItem run, StringBuilder html)
    {
        var transform = $"translate({Units.Css4(run.X)} {Units.Css4(run.Y)}) scale({Units.Scale(run.Scale)})";
        if (run.Mask != null)
        {
            var (bx, by, bw, bh) = run.PaintBox;
            var (rx, ry, rw, rh) = (Units.Css(-bw), Units.Css(-bh), Units.Css(3 * bw), Units.Css(3 * bh));
            html.Append($"<g transform=\"translate({Units.Css4(bx)} {Units.Css4(by)})\"><rect x=\"{rx}\" y=\"{ry}\" width=\"{rw}\" height=\"{rh}\"{run.PaintAttributes} mask=\"url(#{run.Mask})\"/></g>");
            if (run.StrokeAttributes.Length > 0)
                html.Append($"<g transform=\"{transform}\" fill=\"none\"{run.StrokeAttributes}>").Append(Uses(run.Glyphs)).Append("</g>");
            return;
        }
        html.Append($"<g transform=\"{transform}\"{run.FillAttributes}{run.StrokeAttributes}>").Append(Uses(run.Glyphs)).Append("</g>");
    }

    /// <summary>The uses of a run's glyph outlines in font units (see RunGlyph), as the mask of a painted run holds them too.</summary>
    public static string Uses(List<RunGlyph> glyphs)
    {
        var uses = new StringBuilder();
        foreach (var glyph in glyphs)
        {
            uses.Append($"<use href=\"#{glyph.Id}\"");
            if (glyph.Stretch is { } k)
            {
                uses.Append($" transform=\"matrix(1 0 0 {Units.Css4(k)} {Units.Css1(glyph.X)} {Units.Css1(glyph.Y)})\"/>");
                continue;
            }
            if (Math.Abs(glyph.X) > 0.05) uses.Append($" x=\"{Units.Css1(glyph.X)}\"");
            if (Math.Abs(glyph.Y) > 0.05) uses.Append($" y=\"{Units.Css1(glyph.Y)}\"");
            uses.Append("/>");
        }
        return uses.ToString();
    }

    /// <summary>Slide text as an SVG text element the browser sets (see SvgTextItem).</summary>
    static void WriteSvgText(SvgTextItem text, StringBuilder html)
    {
        html.Append($"<text{(text.Hidden ? " class=\"sel\"" : "")}{text.Placement} y=\"{Units.Css(text.Y)}\" font-family=\"'{WebUtility.HtmlEncode(text.Font.Family.Replace("'", "\\'"))}'\" font-size=\"{Units.Css(text.Size)}\"");
        if (text.Bold) html.Append(" font-weight=\"700\"");
        if (text.Italic) html.Append(" font-style=\"italic\"");
        html.Append(text.Attributes).Append('>').Append(WebUtility.HtmlEncode(text.Text)).Append("</text>");
    }

    /// <summary>
    /// An equation's glyph: as text in its font where it stands for its character (Word's PDFs hold the math as text,
    /// which the rasterizer fits to the pixel grid), else as a use of its outline.
    /// </summary>
    static void WriteMathGlyph(MathGlyphItem glyph, StringBuilder html)
    {
        if (glyph.Character is { } character)
        {
            var family = glyph.Font.Family.Replace("'", "\\'");
            // Drawn only: the equation's text to select is its own layer (HiddenSpanItem).
            html.Append($"<text aria-hidden=\"true\" x=\"{Px(glyph.X)}\" y=\"{Px(glyph.Y)}\" font-family=\"'{WebUtility.HtmlEncode(family)}'\" font-size=\"{Fine(glyph.Size * PxPerPt)}\" fill=\"{glyph.Color.ToHex()}\"");
            if (glyph.Color.A < 0.999)
                html.Append($" fill-opacity=\"{Fine(glyph.Color.A)}\"");
            // Math fonts have the ssty feature for the OpenType script math only (Cambria Math), which HarfBuzz shapes
            // with where the language's private subtag asks for it; without it Chrome draws the plain forms (its PDFs
            // of docx/local/mrd-inl: a subscript 2 and a script x as glyphs 2870 and 3051, Word's, with the tag, 884
            // and 1876 without).
            if (glyph.ScriptLevel > 0)
                html.Append(" lang=\"und-x-hbscmath\"");
            var features = glyph.ScriptLevel > 0 ? $";font-feature-settings:'ssty' {glyph.ScriptLevel}" : "";
            html.Append($" style=\"font-kerning:none;user-select:none{features}\">{WebUtility.HtmlEncode(character)}</text>");
            return;
        }
        var scale = glyph.Size * PxPerPt / glyph.Font.UnitsPerEm;
        html.Append($"<use href=\"#{glyph.Id}\" transform=\"translate({Px(glyph.X)} {Px(glyph.Y)}) scale({Fine(scale)})\" fill=\"{glyph.Color.ToHex()}\"");
        if (glyph.Color.A < 0.999)
            html.Append($" fill-opacity=\"{Fine(glyph.Color.A)}\"");
        html.Append("/>");
    }

    /// <summary>A number with five decimals (an equation's glyph scales and opacities).</summary>
    static string Fine(double value) => Math.Round(value, 5).ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture);

    static void WriteGlyphs(GlyphsItem item, StringBuilder html)
    {
        var data = item.Font;
        var scale = item.Size * PxPerPt / data.UnitsPerEm;
        var transform = $"translate({Units.Css4(item.X * PxPerPt)} {Units.Css4(item.Y * PxPerPt)}) scale({Units.Scale(scale)})";
        if (item.SyntheticItalic)
            transform += " skewX(-12)";
        var paint = item.Hollow
            ? $"fill=\"{(item.Shadow ? "#FFFFFF" : "none")}\" stroke=\"{item.Color.ToHex()}\" stroke-width=\"{Units.Css1(0.14 * PxPerPt / scale)}\" stroke-linejoin=\"round\""
            : $"fill=\"{item.Color.ToHex()}\"" + (item.SyntheticBold
                ? $" stroke=\"{item.Color.ToHex()}\" stroke-width=\"{Units.Css1(data.UnitsPerEm / 35.0)}\" stroke-miterlimit=\"10\""
                : "");
        html.Append($"<g transform=\"{transform}\" {paint}>");
        foreach (var glyph in item.Glyphs)
        {
            html.Append($"<use href=\"#{glyph.Outline}\"");
            var turn = glyph.TurnX is { } middle ? $"rotate(-90 {Units.Css1(middle)} {Units.Css1(-data.EmMiddle * data.UnitsPerEm)}) " : "";
            // Horizontally scaled text (w:w) narrows or widens each glyph about its origin.
            if (Math.Abs(item.Scale - 1) > 1e-6)
                html.Append($" transform=\"{turn}translate({Units.Css1(glyph.X)} {Units.Css1(glyph.Y)}) scale({Units.Css4(item.Scale)} 1)\"");
            else
            {
                if (turn.Length > 0) html.Append($" transform=\"{turn.TrimEnd()}\"");
                if (Math.Abs(glyph.X) > 0.05) html.Append($" x=\"{Units.Css1(glyph.X)}\"");
                if (Math.Abs(glyph.Y) > 0.05) html.Append($" y=\"{Units.Css1(glyph.Y)}\"");
            }
            html.Append("/>");
        }
        html.Append("</g>");
    }
}
