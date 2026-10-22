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
                    html.Append($"<path d=\"{shapePath.Data}\"{shapePath.Attributes}/>");
                    break;
                case TransformItem transformed:
                    html.Append($"<g transform=\"{transformed.Transform.Css()}\">");
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
                case FilterCopyItem copied:
                    html.Append($"<use href=\"#{copied.Id}\" filter=\"url(#{copied.Filter})\"/><g id=\"{copied.Id}\">");
                    Write(copied.Items, html);
                    html.Append("</g>");
                    break;
                case ReflectionItem reflected:
                    html.Append($"<g id=\"rf{reflected.Id}\">");
                    Write(reflected.Items, html);
                    html.Append("</g>").Append(Html.SvgEffects.Reflected(reflected.Id, reflected.Transform, reflected.Blurred));
                    break;
                case AltTextItem alt:
                    html.Append($"<div class=\"alt\" role=\"img\" aria-label=\"{WebUtility.HtmlEncode(alt.Text)}\" style=\"left:{Px(alt.X)}px;top:{Px(alt.Y)}px;width:{Px(alt.Width)}px;height:{Px(alt.Height)}px\"></div>");
                    break;
                case BookmarkItem bookmark:
                    html.Append($"<a class=\"bm\" id=\"{WebUtility.HtmlEncode(bookmark.Name)}\" style=\"left:{Px(bookmark.X)}px;top:{Px(bookmark.Y)}px\"></a>");
                    break;
                case TextItem text:
                    WriteText(text, html);
                    break;
                case GlyphsItem glyphs:
                    WriteGlyphs(glyphs, html);
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

    /// <summary>A run of text as text the browser draws (and selects), every character at its position.</summary>
    static void WriteText(TextItem item, StringBuilder html)
    {
        var text = item.Text;
        var (x, positions) = (item.X, item.Positions);
        var on = item.Features.Where(f => !f.StartsWith('-') && f != "kern").ToList();
        string X(int from, int to) => string.Join(' ', Enumerable.Range(from, to - from).Where(i => !char.IsLowSurrogate(text[i]))
            .Select(i => Units.Css((x + positions[i]) * PxPerPt)));
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
            html.Append($" x=\"{Px(x + item.Width)}\" direction=\"rtl\" unicode-bidi=\"bidi-override\"");
            if (item.WordSpacing > 0)
                html.Append($" word-spacing=\"{Px(item.WordSpacing)}\"");
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
        html.Append('"');
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
