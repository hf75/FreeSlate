using System.Text;
using System.Xml.Linq;
using FreeSlate.Drawing;

namespace FreeSlate.Html;

/// <summary>
/// Builds SVG filters for DrawingML effect lists (catalog: effect.outerShadow, effect.innerShadow,
/// effect.presetShadow, effect.glow, effect.softEdge) and reflections (catalog: effect.reflection).
/// The constants come from radial profiles measured in PowerPoint renderings (generated/objects, slide 4):
/// shadow σ = 0.35 × blurRad; glow = dilate r/2 then σ = r/6; soft edge = erode 0.85 r then σ = r/3; blur σ = r/3
/// (generated/outlines).
/// </summary>
public static class SvgEffects
{
    static int nextId;

    /// <summary>Which effects a filter draws.</summary>
    [Flags]
    public enum Parts
    {
        /// <summary>The shadow and glow around the shape, without the shape itself.</summary>
        Outer = 1,

        /// <summary>The shape with what happens on it: blur, soft edges and inner shadow.</summary>
        Shape = 2,

        All = Outer | Shape,
    }

    /// <summary>Appends a filter for the effects of <paramref name="effectLst"/> to <paramref name="defs"/> and returns its id, or null.</summary>
    /// <param name="stroke">The outline width in pixels: a blur that does not grow stays within the outline's outer edge.</param>
    /// <param name="parts">The effects drawn; a 3D body casts its shadow and glow from its back and has no soft edges.</param>
    /// <param name="region">The area the shape covers when it is not the box (0, 0, width, height).</param>
    /// <param name="frame">The turn and flips of the shape on the slide: a shadow that does not rotate with the shape
    /// (rotWithShape 0, as PowerPoint writes its shadows) keeps its direction on the slide (measured).</param>
    public static string? Filter(XElement? effectLst, ColorContext colors, double width, double height, StringBuilder defs, bool boundingBox = false, double stroke = 0,
        Parts parts = Parts.All, (double X, double Y, double W, double H)? region = null, Matrix? frame = null)
    {
        if (effectLst == null)
            return null;
        // Preset shadows (shdw1 … shdw20, the classic hard-edged shadows) are drawn as unblurred outer shadows
        // with their distance and direction (catalog: effect.presetShadow).
        var outer = parts.HasFlag(Parts.Outer);
        var own = parts.HasFlag(Parts.Shape);
        var shadow = outer ? effectLst.Element(Ns.A + "outerShdw") ?? effectLst.Element(Ns.A + "prstShdw") : null;
        // Scaled, skewed or flipped shadows are drawn as a transformed copy of the shape (see Shadow).
        if (shadow != null && Transformed(shadow))
            shadow = null;
        var glow = outer ? effectLst.Element(Ns.A + "glow") : null;
        var soft = own && outer ? effectLst.Element(Ns.A + "softEdge") : null;
        var inner = own ? effectLst.Element(Ns.A + "innerShdw") : null;
        var blurEffect = own ? effectLst.Element(Ns.A + "blur") : null;
        if (shadow == null && glow == null && soft == null && inner == null && blurEffect == null)
            return null;

        var id = $"e{Interlocked.Increment(ref nextId)}";
        var steps = new StringBuilder();
        var merge = new List<string>();
        var extent = 0.0;
        var source = "SourceGraphic";

        if (blurEffect != null)
        {
            // Blur (a:blur, catalog: effect.blur): without grow the result is cut at the shape's outer edge.
            var r = Units.ToPx(blurEffect.Long("rad") ?? 0);
            steps.Append($"<feGaussianBlur in=\"SourceGraphic\" stdDeviation=\"{Units.Css(r / 3)}\" result=\"blur\"/>");
            source = "blur";
            extent = blurEffect.Bool("grow") ?? true ? r * 1.5 : stroke / 2;
        }

        if (soft != null)
        {
            var r = Units.ToPx(soft.Long("rad") ?? 0);
            steps.Append($"<feMorphology in=\"SourceAlpha\" operator=\"erode\" radius=\"{Units.Css(r * 0.85)}\" result=\"se\"/>");
            steps.Append($"<feGaussianBlur in=\"se\" stdDeviation=\"{Units.Css(r / 3)}\" result=\"sb\"/>");
            steps.Append($"<feComposite in=\"{source}\" in2=\"sb\" operator=\"in\" result=\"soft\"/>");
            source = "soft";
        }
        if (glow != null)
        {
            var r = Units.ToPx(glow.Long("rad") ?? 0);
            var color = colors.ResolveChild(glow) ?? RgbColor.Black;
            // Around a 3D body (outer effects only) the glow is half as wide: dilated by r/4, σ = r/8 (measured).
            var (dilate, sigma) = own ? (r / 2, r / 6) : (r / 4, r / 8);
            steps.Append($"<feMorphology in=\"SourceAlpha\" operator=\"dilate\" radius=\"{Units.Css(dilate)}\" result=\"gd\"/>");
            steps.Append($"<feGaussianBlur in=\"gd\" stdDeviation=\"{Units.Css(sigma)}\" result=\"gb\"/>");
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
            var (dx, dy) = (distance * Math.Cos(direction), distance * Math.Sin(direction));
            if (frame is { } turn && shadow.Bool("rotWithShape") == false)
            {
                var back = turn with { E = 0, F = 0 };
                back = back.Invert();
                (dx, dy) = (back.A * dx + back.C * dy, back.B * dx + back.D * dy);
            }
            steps.Append($"<feGaussianBlur in=\"SourceAlpha\" stdDeviation=\"{Units.Css(blur * 0.35)}\"/>");
            steps.Append($"<feOffset dx=\"{Units.Css(dx)}\" dy=\"{Units.Css(dy)}\" result=\"so\"/>");
            steps.Append($"<feFlood flood-color=\"{color.ToHex()}\" flood-opacity=\"{Units.Css4(color.A)}\"/>");
            steps.Append("<feComposite in2=\"so\" operator=\"in\" result=\"shadow\"/>");
            merge.Insert(0, "shadow");
            extent = Math.Max(extent, distance + blur * 2);
        }
        if (own)
            merge.Add(source);
        if (inner != null)
        {
            // Inner shadow: the shape's outside, blurred and moved against the direction, colored and kept inside
            // the shape (measured: at 45° the shadow lies along the inner right and bottom edges).
            var blur = Units.ToPx(inner.Long("blurRad") ?? 0);
            var distance = Units.ToPx(inner.Long("dist") ?? 0);
            var direction = (inner.Long("dir") ?? 0) / 60000.0 * Math.PI / 180;
            var color = colors.ResolveChild(inner) ?? RgbColor.Black;
            steps.Append("<feComponentTransfer in=\"SourceAlpha\" result=\"io\"><feFuncA type=\"table\" tableValues=\"1 0\"/></feComponentTransfer>");
            steps.Append($"<feGaussianBlur in=\"io\" stdDeviation=\"{Units.Css(blur * 0.35)}\"/>");
            steps.Append($"<feOffset dx=\"{Units.Css(-distance * Math.Cos(direction))}\" dy=\"{Units.Css(-distance * Math.Sin(direction))}\" result=\"ib\"/>");
            steps.Append($"<feFlood flood-color=\"{color.ToHex()}\" flood-opacity=\"{Units.Css4(color.A)}\"/>");
            steps.Append("<feComposite in2=\"ib\" operator=\"in\"/>");
            steps.Append("<feComposite in2=\"SourceAlpha\" operator=\"in\" result=\"inner\"/>");
            merge.Add("inner");
        }

        var (rx, ry, rw, rh) = region ?? (0, 0, width, height);
        defs.Append(boundingBox
            ? $"<filter id=\"{id}\" x=\"-0.5\" y=\"-1\" width=\"2\" height=\"3\" color-interpolation-filters=\"sRGB\">"
            : $"<filter id=\"{id}\" filterUnits=\"userSpaceOnUse\" x=\"{Units.Css(rx - extent)}\" y=\"{Units.Css(ry - extent)}\" " +
              $"width=\"{Units.Css(rw + 2 * extent)}\" height=\"{Units.Css(rh + 2 * extent)}\" color-interpolation-filters=\"sRGB\">");
        defs.Append(steps);
        defs.Append("<feMerge>").Append(string.Concat(merge.Select(m => $"<feMergeNode in=\"{m}\"/>"))).Append("</feMerge></filter>");
        return id;
    }

    /// <summary>A shadow drawn as a copy of the shape: the filter that blurs and colors it and the copy's transform.</summary>
    public sealed record ShadowCopy(string Filter, Matrix Transform);

    /// <summary>Whether an outer shadow scales, skews or flips the shape.</summary>
    static bool Transformed(XElement shadow) =>
        (shadow.Long("sx") ?? 100000) != 100000 || (shadow.Long("sy") ?? 100000) != 100000 || (shadow.Long("kx") ?? 0) != 0 || (shadow.Long("ky") ?? 0) != 0;

    /// <summary>
    /// An outer shadow that scales, skews or flips the shape (outerShdw sx, sy, kx, ky, the perspective shadows), or
    /// null: about its alignment point (algn, bottom center by default) the shape is skewed, then scaled, then moved
    /// by dist in direction dir; the blur comes last (measured, generated/effects).
    /// </summary>
    public static ShadowCopy? Shadow(XElement? effectLst, ColorContext colors, double width, double height, StringBuilder defs, Matrix? frame = null)
    {
        var shadow = effectLst?.Element(Ns.A + "outerShdw");
        if (shadow == null || !Transformed(shadow))
            return null;
        var sx = (shadow.Long("sx") ?? 100000) / 100000.0;
        var sy = (shadow.Long("sy") ?? 100000) / 100000.0;
        var kx = Math.Tan((shadow.Long("kx") ?? 0) / 60000.0 * Math.PI / 180);
        var ky = Math.Tan((shadow.Long("ky") ?? 0) / 60000.0 * Math.PI / 180);
        // A shadow that does not rotate with the shape is made on the slide, about the box around the turned shape.
        var turn = frame is { } f && shadow.Bool("rotWithShape") == false ? f with { E = 0, F = 0 } : Matrix.Identity;
        var (boxWidth, boxHeight) = (Math.Abs(turn.A) * width + Math.Abs(turn.C) * height, Math.Abs(turn.B) * width + Math.Abs(turn.D) * height);
        var (ax, ay) = (shadow.Str("algn") ?? "b") switch
        {
            "tl" => (-0.5, -0.5), "t" => (0, -0.5), "tr" => (0.5, -0.5),
            "l" => (-0.5, 0.0), "ctr" => (0, 0), "r" => (0.5, 0),
            "bl" => (-0.5, 0.5), "br" => (0.5, 0.5), _ => (0, 0.5),
        };
        (ax, ay) = (ax * boxWidth, ay * boxHeight);
        var distance = Units.ToPx(shadow.Long("dist") ?? 0);
        var direction = (shadow.Long("dir") ?? 0) / 60000.0 * Math.PI / 180;
        // About the box's center on the slide: p' = A + S·K·(p − A) + offset with K the skew and S the scale; then back
        // into the shape's own coordinates.
        var m = new Matrix(sx, sy * ky, sx * kx, sy, 0, 0);
        var onSlide = Matrix.Translate(ax + distance * Math.Cos(direction), ay + distance * Math.Sin(direction)).Then(m).Then(Matrix.Translate(-ax, -ay));
        var transform = Matrix.Translate(width / 2, height / 2).Then(turn.Invert()).Then(onSlide).Then(turn).Then(Matrix.Translate(-width / 2, -height / 2));
        var blur = Units.ToPx(shadow.Long("blurRad") ?? 0);
        var color = colors.ResolveChild(shadow) ?? RgbColor.Black;
        // The filter covers the transformed box with room for the blur.
        (double X, double Y)[] corners = [(0, 0), (width, 0), (0, height), (width, height)];
        var mapped = corners.Select(c => (X: transform.A * c.X + transform.C * c.Y + transform.E, Y: transform.B * c.X + transform.D * c.Y + transform.F)).ToList();
        var margin = blur * 2 + 2;
        var (minX, minY) = (mapped.Min(c => c.X) - margin, mapped.Min(c => c.Y) - margin);
        var (maxX, maxY) = (mapped.Max(c => c.X) + margin, mapped.Max(c => c.Y) + margin);
        var id = $"e{Interlocked.Increment(ref nextId)}";
        defs.Append($"<filter id=\"{id}\" filterUnits=\"userSpaceOnUse\" x=\"{Units.Css(minX)}\" y=\"{Units.Css(minY)}\" width=\"{Units.Css(maxX - minX)}\" height=\"{Units.Css(maxY - minY)}\" color-interpolation-filters=\"sRGB\">" +
                    $"<feGaussianBlur in=\"SourceAlpha\" stdDeviation=\"{Units.Css(blur * 0.35)}\" result=\"sb\"/>" +
                    $"<feFlood flood-color=\"{color.ToHex()}\" flood-opacity=\"{Units.Css4(color.A)}\"/>" +
                    "<feComposite in2=\"sb\" operator=\"in\"/></filter>");
        return new ShadowCopy(id, transform);
    }

    /// <summary>
    /// The markup with its reflection (a:reflection): a copy scaled by sx/sy about the alignment point of
    /// <paramref name="box"/> (bl with sy = -100 % mirrors it about the bottom edge), moved by dist in direction dir,
    /// faded from stA at stPos to endA at endPos (fractions of the box height, from the mirrored edge) and blurred by
    /// blurRad. Without a reflection the markup is returned unchanged.
    /// </summary>
    public static string Reflection(XElement? effectLst, string markup, (double X, double Y, double W, double H) box, StringBuilder defs)
    {
        var reflection = effectLst?.Element(Ns.A + "reflection");
        if (reflection == null || markup.Length == 0)
            return markup;
        var n = Interlocked.Increment(ref nextId);
        double Fraction(string name, double fallback) => (reflection.Long(name) ?? fallback * 100000) / 100000.0;
        var sx = Fraction("sx", 1);
        var sy = Fraction("sy", 1);
        var distance = Units.ToPx(reflection.Long("dist") ?? 0);
        var direction = (reflection.Long("dir") ?? 0) / 60000.0 * Math.PI / 180;
        var blur = Units.ToPx(reflection.Long("blurRad") ?? 0);
        var (left, top, right, bottom) = (box.X, box.Y, box.X + box.W, box.Y + box.H);
        var center = (box.X + box.W / 2, box.Y + box.H / 2);
        var (ax, ay) = (reflection.Str("algn") ?? "b") switch
        {
            "tl" => (left, top), "t" => (center.Item1, top), "tr" => (right, top),
            "l" => (left, center.Item2), "ctr" => center, "r" => (right, center.Item2),
            "bl" => (left, bottom), "br" => (right, bottom), _ => (center.Item1, bottom),
        };
        var dx = distance * Math.Cos(direction);
        var dy = distance * Math.Sin(direction);
        var matrix = $"matrix({Units.Css4(sx)} 0 0 {Units.Css4(sy)} {Units.Css(ax - sx * ax + dx)} {Units.Css(ay - sy * ay + dy)})";

        // The fade runs from the edge the copy is mirrored at into the object.
        var edge = sy < 0 ? bottom : top;
        var inward = sy < 0 ? -1 : 1;
        var start = edge + inward * Fraction("stPos", 0) * box.H;
        var end = edge + inward * Fraction("endPos", 1) * box.H;
        var (stA, endA) = (Fraction("stA", 1), Fraction("endA", 0));
        var margin = box.W + box.H + blur * 3;
        defs.Append($"<linearGradient id=\"rg{n}\" gradientUnits=\"userSpaceOnUse\" x1=\"0\" y1=\"{Units.Css(start)}\" x2=\"0\" y2=\"{Units.Css(end)}\">" +
                    $"<stop offset=\"0\" stop-color=\"#fff\" stop-opacity=\"{Units.Css4(stA)}\"/><stop offset=\"1\" stop-color=\"#fff\" stop-opacity=\"{Units.Css4(endA)}\"/></linearGradient>");
        defs.Append($"<mask id=\"rm{n}\" maskUnits=\"userSpaceOnUse\" x=\"{Units.Css(left - margin)}\" y=\"{Units.Css(top - margin)}\" width=\"{Units.Css(box.W + 2 * margin)}\" height=\"{Units.Css(box.H + 2 * margin)}\">" +
                    $"<rect x=\"{Units.Css(left - margin)}\" y=\"{Units.Css(top - margin)}\" width=\"{Units.Css(box.W + 2 * margin)}\" height=\"{Units.Css(box.H + 2 * margin)}\" fill=\"url(#rg{n})\"/></mask>");
        var filter = "";
        if (blur > 0)
        {
            defs.Append($"<filter id=\"rb{n}\" x=\"-0.2\" y=\"-0.2\" width=\"1.4\" height=\"1.4\"><feGaussianBlur stdDeviation=\"{Units.Css(blur * 0.35)}\"/></filter>");
            filter = $" filter=\"url(#rb{n})\"";
        }
        return $"<g id=\"rf{n}\">{markup}</g><g transform=\"{matrix}\"><use href=\"#rf{n}\" mask=\"url(#rm{n})\"{filter}/></g>";
    }
}
