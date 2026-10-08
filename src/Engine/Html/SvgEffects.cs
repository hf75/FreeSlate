using System.Text;
using System.Xml.Linq;
using Engine.Drawing;

namespace Engine.Html;

/// <summary>
/// Builds SVG filters for DrawingML effect lists (catalog: effect.outerShadow, effect.glow, effect.softEdge).
/// The constants come from radial profiles measured in PowerPoint renderings (generated/objects, slide 4):
/// shadow σ = 0.35 × blurRad; glow = dilate r/2 then σ = r/6; soft edge = erode 0.85 r then σ = r/3.
/// </summary>
public static class SvgEffects
{
    static int nextId;

    /// <summary>Appends a filter for the effects of <paramref name="effectLst"/> to <paramref name="defs"/> and returns its id, or null.</summary>
    public static string? Filter(XElement? effectLst, ColorContext colors, double width, double height, StringBuilder defs, bool boundingBox = false)
    {
        if (effectLst == null)
            return null;
        var shadow = effectLst.Element(Ns.A + "outerShdw");
        var glow = effectLst.Element(Ns.A + "glow");
        var soft = effectLst.Element(Ns.A + "softEdge");
        if (shadow == null && glow == null && soft == null)
            return null;

        var id = $"e{Interlocked.Increment(ref nextId)}";
        var steps = new StringBuilder();
        var merge = new List<string>();
        var extent = 0.0;
        var source = "SourceGraphic";

        if (soft != null)
        {
            var r = Units.ToPx(soft.Long("rad") ?? 0);
            steps.Append($"<feMorphology in=\"SourceAlpha\" operator=\"erode\" radius=\"{Units.Css(r * 0.85)}\" result=\"se\"/>");
            steps.Append($"<feGaussianBlur in=\"se\" stdDeviation=\"{Units.Css(r / 3)}\" result=\"sb\"/>");
            steps.Append("<feComposite in=\"SourceGraphic\" in2=\"sb\" operator=\"in\" result=\"soft\"/>");
            source = "soft";
        }
        if (glow != null)
        {
            var r = Units.ToPx(glow.Long("rad") ?? 0);
            var color = colors.ResolveChild(glow) ?? RgbColor.Black;
            steps.Append($"<feMorphology in=\"SourceAlpha\" operator=\"dilate\" radius=\"{Units.Css(r / 2)}\" result=\"gd\"/>");
            steps.Append($"<feGaussianBlur in=\"gd\" stdDeviation=\"{Units.Css(r / 6)}\" result=\"gb\"/>");
            steps.Append($"<feFlood flood-color=\"{color.ToHex()}\" flood-opacity=\"{Units.Css4(color.A)}\"/>");
            steps.Append("<feComposite in2=\"gb\" operator=\"in\" result=\"glow\"/>");
            merge.Add("glow");
            extent = Math.Max(extent, r * 1.5);
        }
        if (shadow != null)
        {
            var blur = Units.ToPx(shadow.Long("blurRad") ?? 0);
            var distance = Units.ToPx(shadow.Long("dist") ?? 0);
            var direction = (shadow.Long("dir") ?? 0) / 60000.0 * Math.PI / 180;
            var color = colors.ResolveChild(shadow) ?? RgbColor.Black;
            steps.Append($"<feGaussianBlur in=\"SourceAlpha\" stdDeviation=\"{Units.Css(blur * 0.35)}\"/>");
            steps.Append($"<feOffset dx=\"{Units.Css(distance * Math.Cos(direction))}\" dy=\"{Units.Css(distance * Math.Sin(direction))}\" result=\"so\"/>");
            steps.Append($"<feFlood flood-color=\"{color.ToHex()}\" flood-opacity=\"{Units.Css4(color.A)}\"/>");
            steps.Append("<feComposite in2=\"so\" operator=\"in\" result=\"shadow\"/>");
            merge.Insert(0, "shadow");
            extent = Math.Max(extent, distance + blur * 2);
        }
        merge.Add(source);

        defs.Append(boundingBox
            ? $"<filter id=\"{id}\" x=\"-0.5\" y=\"-1\" width=\"2\" height=\"3\" color-interpolation-filters=\"sRGB\">"
            : $"<filter id=\"{id}\" filterUnits=\"userSpaceOnUse\" x=\"{Units.Css(-extent)}\" y=\"{Units.Css(-extent)}\" " +
              $"width=\"{Units.Css(width + 2 * extent)}\" height=\"{Units.Css(height + 2 * extent)}\" color-interpolation-filters=\"sRGB\">");
        defs.Append(steps);
        defs.Append("<feMerge>").Append(string.Concat(merge.Select(m => $"<feMergeNode in=\"{m}\"/>"))).Append("</feMerge></filter>");
        return id;
    }
}
