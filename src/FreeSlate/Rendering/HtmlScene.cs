using System.Net;
using System.Text;

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
                case RectItem rect:
                    html.Append($"<rect x=\"{Px(rect.X)}\" y=\"{Px(rect.Y)}\" width=\"{Px(rect.Width)}\" height=\"{Px(rect.Height)}\" fill=\"{rect.Color.ToHex()}\"/>");
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
                case AltTextItem alt:
                    html.Append($"<div class=\"alt\" role=\"img\" aria-label=\"{WebUtility.HtmlEncode(alt.Text)}\" style=\"left:{Px(alt.X)}px;top:{Px(alt.Y)}px;width:{Px(alt.Width)}px;height:{Px(alt.Height)}px\"></div>");
                    break;
                case BookmarkItem bookmark:
                    html.Append($"<a class=\"bm\" id=\"{WebUtility.HtmlEncode(bookmark.Name)}\" style=\"left:{Px(bookmark.X)}px;top:{Px(bookmark.Y)}px\"></a>");
                    break;
                default:
                    throw new NotSupportedException(item.GetType().Name);
            }
        }
    }
}
