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

    /// <summary>
    /// How PowerPoint sizes the effects of text: unlike a shape's, they grow with the largest font size of the line
    /// (measured with lines of 8 to 240 pt in Aptos and other fonts, in exported slides and in the slide show alike):
    /// outer shadows and glow by (size / 78 pt)^0.695, inner shadows by size / 117 pt, a reflection's blur by
    /// size / 79 pt and its distance by size / 14.5 pt.
    /// </summary>
    public readonly record struct TextScale(double Outer, double Inner, double ReflectionBlur, double ReflectionDistance)
    {
        /// <summary>The scales for a line whose largest font is <paramref name="size"/> points.</summary>
        public static TextScale Of(double size) => new(Math.Pow(Math.Max(size, 0.01) / 78, 0.695), size / 117, size / 79, size / 14.5);
    }

    /// <summary>Appends a filter for the effects of <paramref name="effectLst"/> to <paramref name="defs"/> and returns its id, or null.</summary>
    /// <param name="stroke">The outline width in pixels: a blur that does not grow stays within the outline's outer edge.</param>
    /// <param name="parts">The effects drawn; a 3D body casts its shadow and glow from its back and has no soft edges.</param>
    /// <param name="region">The area the shape covers when it is not the box (0, 0, width, height).</param>
    /// <param name="frame">The turn and flips of the shape on the slide: a shadow that does not rotate with the shape
    /// (rotWithShape 0, as PowerPoint writes its shadows) keeps its direction on the slide (measured).</param>
    /// <param name="text">Effects of a line of text: sized with its font (see <see cref="TextScale"/>); the glow is
    /// dilated by 0.46 r and blurred by r / 6 at the outer scale (measured).</param>
    /// <param name="flat">Outer effects of the flat shape (a 3D scene's backdrop), with a flat shape's glow.</param>
    public static string? Filter(XElement? effectLst, ColorContext colors, double width, double height, StringBuilder defs, bool boundingBox = false, double stroke = 0,
        Parts parts = Parts.All, (double X, double Y, double W, double H)? region = null, Matrix? frame = null, TextScale? text = null, bool flat = false)
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
            var (dilate, sigma) = text is { } scale ? (0.46 * r * scale.Outer, r * scale.Outer / 6) : own || flat ? (r / 2, r / 6) : (r / 4, r / 8);
            steps.Append($"<feMorphology in=\"SourceAlpha\" operator=\"dilate\" radius=\"{Units.Css(dilate)}\" result=\"gd\"/>");
            steps.Append($"<feGaussianBlur in=\"gd\" stdDeviation=\"{Units.Css(sigma)}\" result=\"gb\"/>");
            steps.Append($"<feFlood flood-color=\"{color.ToHex()}\" flood-opacity=\"{Units.Css4(color.A)}\"/>");
            steps.Append("<feComposite in2=\"gb\" operator=\"in\" result=\"glow\"/>");
            merge.Add("glow");
            extent = Math.Max(extent, r * 1.5);
        }
        if (shadow != null)
        {
            var blur = Units.ToPx(shadow.Long("blurRad") ?? 0) * (text?.Outer ?? 1);
            var distance = Units.ToPx(shadow.Long("dist") ?? 0) * (text?.Outer ?? 1);
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
            var blur = Units.ToPx(inner.Long("blurRad") ?? 0) * (text?.Inner ?? 1);
            var distance = Units.ToPx(inner.Long("dist") ?? 0) * (text?.Inner ?? 1);
            var direction = (inner.Long("dir") ?? 0) / 60000.0 * Math.PI / 180;
            var color = colors.ResolveChild(inner) ?? RgbColor.Black;
            steps.Append("<feComponentTransfer in=\"SourceAlpha\" result=\"io\"><feFuncA type=\"table\" tableValues=\"1 0\"/></feComponentTransfer>");
            steps.Append($"<feGaussianBlur in=\"io\" stdDeviation=\"{Units.Css(blur * 0.35)}\"/>");
            steps.Append($"<feOffset dx=\"{Units.Css(-distance * Math.Cos(direction))}\" dy=\"{Units.Css(-distance * Math.Sin(direction))}\" result=\"ib\"/>");
            steps.Append($"<feFlood flood-color=\"{color.ToHex()}\" flood-opacity=\"{Units.Css4(color.A)}\"/>");
            steps.Append("<feComposite in2=\"ib\" operator=\"in\"/>");
            steps.Append("<feComposite in2=\"SourceAlpha\" operator=\"in\" result=\"inner\"/>");
            merge.Add("inner");
            // The outside that casts the shadow must lie within the filter's region.
            extent = Math.Max(extent, distance + blur * 1.2 + 1);
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
    /// null: about its alignment point (algn, bottom center by default) the shape is scaled and skewed, then moved by
    /// dist in direction dir; the blur comes last (measured, generated/effects). Text passes the outer scale of its
    /// line for dist and blur (see <see cref="TextScale"/>).
    /// </summary>
    public static ShadowCopy? Shadow(XElement? effectLst, ColorContext colors, double width, double height, StringBuilder defs, Matrix? frame = null,
        double scale = 1)
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
        var distance = Units.ToPx(shadow.Long("dist") ?? 0) * scale;
        var direction = (shadow.Long("dir") ?? 0) / 60000.0 * Math.PI / 180;
        // About the box's center on the slide: p' = A + M·(p − A) + offset with M = [sx, tan kx; tan ky, sy], the skew
        // not scaled (measured: a flipped shadow keeps its skew's direction); then back into the shape's coordinates.
        var m = new Matrix(sx, ky, kx, sy, 0, 0);
        var onSlide = Matrix.Translate(ax + distance * Math.Cos(direction), ay + distance * Math.Sin(direction)).Then(m).Then(Matrix.Translate(-ax, -ay));
        var transform = Matrix.Translate(width / 2, height / 2).Then(turn.Invert()).Then(onSlide).Then(turn).Then(Matrix.Translate(-width / 2, -height / 2));
        var blur = Units.ToPx(shadow.Long("blurRad") ?? 0) * scale;
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
    /// The markup with its reflection (a:reflection): a copy scaled by sx/sy and skewed by kx/ky about the alignment
    /// point of <paramref name="box"/> (bl with sy = -100 % mirrors it about the bottom edge), moved by dist in
    /// direction dir, faded along fadeDir (90° by default, top to bottom of the copy) from stA at stPos to endA at
    /// endPos (fractions of the copy's extent) and blurred by blurRad. A reflection that does not rotate with its
    /// shape (rotWithShape 0, given the shape's turn in <paramref name="frame"/>) mirrors the turned shape on the
    /// slide (measured). Without a reflection the markup is returned unchanged; without the original
    /// (<paramref name="original"/> false, effect graphs) only the reflection is returned. Text has its distance in
    /// the box and passes <paramref name="distanceScale"/> 0, and its blur scale (see <see cref="TextScale"/>).
    /// </summary>
    public static string Reflection(XElement? effectLst, string markup, (double X, double Y, double W, double H) box, StringBuilder defs, Matrix? frame = null,
        bool original = true, double distanceScale = 1, double blurScale = 1)
    {
        var reflection = effectLst?.Element(Ns.A + "reflection");
        if (reflection == null || markup.Length == 0)
            return markup;
        var n = Interlocked.Increment(ref nextId);
        double Fraction(string name, double fallback) => (reflection.Long(name) ?? fallback * 100000) / 100000.0;
        double Angle(string name, double fallback) => (reflection.Long(name) ?? fallback * 60000) / 60000.0 * Math.PI / 180;
        var (sx, sy) = (Fraction("sx", 1), Fraction("sy", 1));
        var (kx, ky) = (Math.Tan(Angle("kx", 0)), Math.Tan(Angle("ky", 0)));
        var distance = Units.ToPx(reflection.Long("dist") ?? 0) * distanceScale;
        var direction = Angle("dir", 0);
        var blur = Units.ToPx(reflection.Long("blurRad") ?? 0) * blurScale;

        // On the slide for reflections that keep still (about the box around the turned shape), else in the shape.
        var turn = frame is { } f && reflection.Bool("rotWithShape") == false ? f with { E = 0, F = 0 } : Matrix.Identity;
        var center = (X: box.X + box.W / 2, Y: box.Y + box.H / 2);
        var (width, height) = (Math.Abs(turn.A) * box.W + Math.Abs(turn.C) * box.H, Math.Abs(turn.B) * box.W + Math.Abs(turn.D) * box.H);
        var (ax, ay) = (reflection.Str("algn") ?? "b") switch
        {
            "tl" => (-0.5, -0.5), "t" => (0, -0.5), "tr" => (0.5, -0.5),
            "l" => (-0.5, 0.0), "ctr" => (0, 0), "r" => (0.5, 0),
            "bl" => (-0.5, 0.5), "br" => (0.5, 0.5), _ => (0, 0.5),
        };
        (ax, ay) = (ax * width, ay * height);
        var m = new Matrix(sx, ky, kx, sy, 0, 0);
        var onSlide = Matrix.Translate(ax + distance * Math.Cos(direction), ay + distance * Math.Sin(direction)).Then(m).Then(Matrix.Translate(-ax, -ay));
        var transform = Matrix.Translate(center.X, center.Y).Then(turn.Invert()).Then(onSlide).Then(turn).Then(Matrix.Translate(-center.X, -center.Y));

        // The fade runs along fadeDir across the copy (on the slide when the reflection keeps still).
        var fade = Angle("fadeDir", 90);
        var back = turn.Invert();
        var (fx, fy) = (back.A * Math.Cos(fade) + back.C * Math.Sin(fade), back.B * Math.Cos(fade) + back.D * Math.Sin(fade));
        var length = Math.Sqrt(fx * fx + fy * fy);
        (fx, fy) = length > 1e-9 ? (fx / length, fy / length) : (0, 1);
        (double X, double Y)[] corners = [(box.X, box.Y), (box.X + box.W, box.Y), (box.X, box.Y + box.H), (box.X + box.W, box.Y + box.H)];
        var along = corners.Select(c => (transform.A * c.X + transform.C * c.Y + transform.E) * fx + (transform.B * c.X + transform.D * c.Y + transform.F) * fy).ToList();
        var (low, span) = (along.Min(), Math.Max(1e-6, along.Max() - along.Min()));
        var (start, end) = (low + Fraction("stPos", 0) * span, low + Fraction("endPos", 1) * span);
        var (stA, endA) = (Fraction("stA", 1), Fraction("endA", 0));
        var margin = box.W + box.H + blur * 3 + distance;
        defs.Append($"<linearGradient id=\"rg{n}\" gradientUnits=\"userSpaceOnUse\" x1=\"{Units.Css(fx * start)}\" y1=\"{Units.Css(fy * start)}\" x2=\"{Units.Css(fx * end)}\" y2=\"{Units.Css(fy * end)}\">" +
                    $"<stop offset=\"0\" stop-color=\"#fff\" stop-opacity=\"{Units.Css4(stA)}\"/><stop offset=\"1\" stop-color=\"#fff\" stop-opacity=\"{Units.Css4(endA)}\"/></linearGradient>");
        defs.Append($"<mask id=\"rm{n}\" maskUnits=\"userSpaceOnUse\" x=\"{Units.Css(box.X - margin)}\" y=\"{Units.Css(box.Y - margin)}\" width=\"{Units.Css(box.W + 2 * margin)}\" height=\"{Units.Css(box.H + 2 * margin)}\">" +
                    $"<rect x=\"{Units.Css(box.X - margin)}\" y=\"{Units.Css(box.Y - margin)}\" width=\"{Units.Css(box.W + 2 * margin)}\" height=\"{Units.Css(box.H + 2 * margin)}\" fill=\"url(#rg{n})\"/></mask>");
        var filter = "";
        if (blur > 0)
        {
            defs.Append($"<filter id=\"rb{n}\" x=\"-0.2\" y=\"-0.2\" width=\"1.4\" height=\"1.4\"><feGaussianBlur stdDeviation=\"{Units.Css(blur * 0.35)}\"/></filter>");
            filter = $" filter=\"url(#rb{n})\"";
        }
        var reflected = $"<g mask=\"url(#rm{n})\"><g{filter}><g transform=\"{transform.Css()}\"><use href=\"#rf{n}\"/></g></g></g>";
        if (original)
            return $"<g id=\"rf{n}\">{markup}</g>{reflected}";
        defs.Append($"<g id=\"rf{n}\">{markup}</g>");
        return reflected;
    }
}
