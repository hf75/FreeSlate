using System.Text;
using FreeSlate.Drawing;

namespace FreeSlate.Html;

/// <summary>Turns fills and outlines into SVG paint attributes; gradients, patterns and images go into defs.</summary>
public sealed class SvgPaint
{
    int nextId;

    /// <summary>A document-unique id for defs (gradients, patterns, clip paths).</summary>
    public string NewId(string prefix) => $"{prefix}{++nextId}";

    /// <summary>fill/fill-opacity attributes for a shape box of w × h pixels whose origin lies at <paramref name="origin"/> on the slide.</summary>
    /// <param name="outline">The shape's outline as path data in the box, for gradients that follow it (path="shape").</param>
    /// <param name="offset">Where the w × h box lies in the shape's coordinates: a gradient spans the bounds of the path
    /// it fills (see SlideRenderer).</param>
    public string Fill(Fill fill, double w, double h, StringBuilder defs, double shapeRotation = 0, (double X, double Y) origin = default,
        string? outline = null, (double X, double Y) offset = default) => fill switch
    {
        SolidFill s => Color("fill", s.Color),
        GradientFill g => $" fill=\"url(#{Gradient(g, w, h, defs, shapeRotation, outline, offset)})\"",
        PatternFill p => $" fill=\"url(#{Pattern(p, defs, origin)})\"",
        PictureFill p => $" fill=\"url(#{Picture(p, w, h, defs)})\"",
        SlideFill { Fill: SolidFill s } => Color("fill", s.Color),
        SlideFill s => $" fill=\"url(#{SlidePattern(s, defs)})\"",
        _ => " fill=\"none\"",
    };

    /// <summary>The slide background as one pattern tile covering the slide, mapped into the shape's coordinates.</summary>
    string SlidePattern(SlideFill s, StringBuilder defs)
    {
        var id = NewId("sb");
        var paint = Fill(s.Fill, s.Width, s.Height, defs);
        var (a, b, c, d, e, f) = s.ToShape;
        defs.Append($"<pattern id=\"{id}\" patternUnits=\"userSpaceOnUse\" width=\"{Units.Css(s.Width)}\" height=\"{Units.Css(s.Height)}\" " +
                    $"patternTransform=\"matrix({Units.Css4(a)} {Units.Css4(b)} {Units.Css4(c)} {Units.Css4(d)} {Units.Css(e)} {Units.Css(f)})\">" +
                    $"<rect width=\"{Units.Css(s.Width)}\" height=\"{Units.Css(s.Height)}\"{paint}/></pattern>");
        return id;
    }

    /// <summary>
    /// A preset pattern as an 8×8 pixel tile. PowerPoint aligns the tiles to the slide, so the pattern is shifted
    /// by the shape's position (harvested and verified: tools/Fidelity harvest-patterns).
    /// </summary>
    string Pattern(PatternFill p, StringBuilder defs, (double X, double Y) origin)
    {
        var id = NewId("t");
        var tile = p.Bits ?? PatternPresets.Tiles.GetValueOrDefault(p.Preset) ?? PatternPresets.Tiles["pct50"];
        var bits = new StringBuilder();
        for (var row = 0; row < 8; row++)
        {
            for (var column = 0; column < 8; column++)
            {
                if ((tile[row] & (0x80 >> column)) != 0)
                    bits.Append($"M{column} {row}h1v1h-1z");
            }
        }
        // A pattern of its own may have bits larger than a pixel (Word's VML patterns: a point).
        var period = 8 * p.Cell;
        double Phase(double v) => ((-v % period) + period) % period;
        var scale = Math.Abs(p.Cell - 1) < 1e-9 ? "" : $" scale({Units.Css4(p.Cell)})";
        defs.Append($"<pattern id=\"{id}\" patternUnits=\"userSpaceOnUse\" width=\"8\" height=\"8\" patternTransform=\"translate({Units.Css(Phase(origin.X))} {Units.Css(Phase(origin.Y))}){scale}\">" +
                    $"<rect width=\"8\" height=\"8\"{Color("fill", p.Background)} shape-rendering=\"crispEdges\"/>" +
                    (bits.Length > 0 ? $"<path d=\"{bits}\"{Color("fill", p.Foreground)} shape-rendering=\"crispEdges\"/>" : "") +
                    "</pattern>");
        return id;
    }

    /// <summary>
    /// The paint attributes of an outline; <paramref name="closed"/> tells whether the stroked path consists of closed
    /// figures only, where round-capped dashes start where the pattern does (see <see cref="Dashes"/>).
    /// </summary>
    public string Stroke(Outline? outline, double w, double h, StringBuilder defs, (double X, double Y) origin = default, bool closed = false)
    {
        if (outline is not { Visible: true })
            return " stroke=\"none\"";

        var width = Math.Max(Units.ToPx(outline.WidthEmu), 0.5);
        var sb = new StringBuilder(outline.Fill switch
        {
            SolidFill s => Color("stroke", s.Color),
            GradientFill g => $" stroke=\"url(#{Gradient(g, w, h, defs, 0)})\"",
            // Pattern outlines show the pattern itself, aligned to the slide like pattern fills (catalog: line.fill).
            PatternFill p => $" stroke=\"url(#{Pattern(p, defs, origin)})\"",
            _ => " stroke=\"#000\"",
        });
        sb.Append($" stroke-width=\"{Units.Css(width)}\"");
        var dash = Dashes.Pattern(outline);
        // PowerPoint ends dashes flat under square caps; only an open line's ends get them (Dashes.SquareEnds).
        var squareDashes = outline.Cap == "sq" && dash is { Length: > 0 };
        sb.Append(outline.Cap switch { "rnd" => " stroke-linecap=\"round\"", "sq" when !squareDashes => " stroke-linecap=\"square\"", _ => "" });
        sb.Append(outline.Join switch
        {
            "round" => " stroke-linejoin=\"round\"",
            "bevel" => " stroke-linejoin=\"bevel\"",
            _ => $" stroke-linejoin=\"miter\" stroke-miterlimit=\"{Units.Css(Math.Max(1, outline.MiterLimit))}\"",
        });
        if (dash is { Length: > 0 })
        {
            // Round caps lie within PowerPoint's dashes and a round dash is at least as long as the line is wide:
            // SVG adds the caps to every dash, so the dashes shrink by the cap and the spaces take what they lose. On
            // closed figures the dashes start where the pattern does; an open line's pattern starts half a width
            // before the line, where its cap begins (measured with generated/lines-dash: a 0.5:2 pattern on a round
            // 4 pt line repeats every 3 widths; dots on an ellipse lie on [0, 1) widths, on a line on [-½, ½)).
            var round = outline.Cap == "rnd";
            var unit = Dashes.Unit(width);
            var lengths = new double[dash.Length + dash.Length % 2];
            for (var i = 0; i < dash.Length; i += 2)
            {
                var (d, space) = (dash[i], i + 1 < dash.Length ? dash[i + 1] : 0);
                (lengths[i], lengths[i + 1]) = round ? (Math.Max(0, d - 1), space + 1) : (d, space);
            }
            sb.Append($" stroke-dasharray=\"{string.Join(' ', lengths.Select(d => Units.Css(d * unit)))}\"");
            if (round && closed)
                sb.Append($" stroke-dashoffset=\"{Units.Css(-unit / 2)}\"");
        }
        return sb.ToString();
    }

    static string Color(string attribute, RgbColor color) =>
        color.A < 0.999
            ? $" {attribute}=\"{color.ToHex()}\" {attribute}-opacity=\"{Units.Css4(color.A)}\""
            : $" {attribute}=\"{color.ToHex()}\"";

    /// <summary>Rough share of foreground pixels until patterns are drawn as real SVG patterns.</summary>
    static double PatternDensity(string preset) => preset switch
    {
        _ when preset.StartsWith("pct", StringComparison.Ordinal) && int.TryParse(preset[3..], out var pct) => pct / 100.0,
        _ when preset.StartsWith("lt", StringComparison.Ordinal) => 0.25,
        _ when preset.StartsWith("dk", StringComparison.Ordinal) => 0.5,
        _ => 0.35,
    };

    string Gradient(GradientFill g, double w, double h, StringBuilder defs, double shapeRotation, string? outline = null, (double X, double Y) offset = default)
    {
        var id = NewId("g");
        var stops = StopMarkup(PowerPointStops(g.Stops, g.Bell));
        var shift = offset == default ? "" : $"translate({Units.Css(offset.X)} {Units.Css(offset.Y)}) ";

        var counterRotation = g.RotateWithShape ? "" : $" rotate({Units.Css(-shapeRotation)} {Units.Css(w / 2)} {Units.Css(h / 2)})";
        if (g.Angle is { } angle)
        {
            var (sin, cos) = Math.SinCos(angle * Math.PI / 180);
            string coords;
            if (g.Scaled)
            {
                // The angle applies in the unit square, which is then stretched to the shape box.
                var half = (Math.Abs(cos) + Math.Abs(sin)) / 2;
                coords = $"x1=\"{Units.Css4(0.5 - half * cos)}\" y1=\"{Units.Css4(0.5 - half * sin)}\" x2=\"{Units.Css4(0.5 + half * cos)}\" y2=\"{Units.Css4(0.5 + half * sin)}\" " +
                         $"gradientTransform=\"{shift}{counterRotation.Trim()} scale({Units.Css(w)} {Units.Css(h)})\"";
            }
            else
            {
                var half = (Math.Abs(w * cos) + Math.Abs(h * sin)) / 2;
                var transform = (shift + counterRotation.Trim()).Trim();
                coords = $"x1=\"{Units.Css(w / 2 - half * cos)}\" y1=\"{Units.Css(h / 2 - half * sin)}\" x2=\"{Units.Css(w / 2 + half * cos)}\" y2=\"{Units.Css(h / 2 + half * sin)}\"" +
                         (transform.Length > 0 ? $" gradientTransform=\"{transform}\"" : "");
            }
            defs.Append($"<linearGradient id=\"{id}\" gradientUnits=\"userSpaceOnUse\" {coords}>{stops}</linearGradient>");
            return id;
        }

        var (fl, ft, fr, fb) = g.FocusRect;
        var (tl, tt, tr, tb) = g.TileRect;
        if (g.Path == "shape" && !string.IsNullOrEmpty(outline))
            return ShapeGradient(g, id, w, h, outline, defs, offset);
        if (g.Path is "rect" or "shape")
            return RectGradient(g, id, w, h, defs, offset);

        // Circle: from the focus rectangle (offset 0) to the edge of the tile rectangle (offset 1).
        var cx = w * (fl + (1 - fr)) / 2;
        var cy = h * (ft + (1 - fb)) / 2;
        double[] xs = [w * tl, w * (1 - tr)];
        double[] ys = [h * tt, h * (1 - tb)];
        var r = xs.SelectMany(x => ys.Select(y => Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)))).Max();
        defs.Append($"<radialGradient id=\"{id}\" gradientUnits=\"userSpaceOnUse\" cx=\"{Units.Css(offset.X + cx)}\" cy=\"{Units.Css(offset.Y + cy)}\" r=\"{Units.Css(Math.Max(r, 1))}\">{stops}</radialGradient>");
        return id;
    }

    /// <summary>
    /// A rectangular path gradient (path="rect"; "shape" uses the same construction): concentric rectangles from
    /// the focus rectangle (offset 0) to the tile rectangle (offset 1), drawn exactly as four trapezoids with linear
    /// gradients perpendicular to their side, inside a pattern the size of the shape.
    /// </summary>
    string RectGradient(GradientFill g, string id, double w, double h, StringBuilder defs, (double X, double Y) offset = default)
    {
        var (fl, ft, fr, fb) = g.FocusRect;
        var (tl, tt, tr, tb) = g.TileRect;
        var focus = (L: w * fl, T: h * ft, R: w * (1 - fr), B: h * (1 - fb));
        var tile = (L: w * tl, T: h * tt, R: w * (1 - tr), B: h * (1 - tb));
        var stops = PowerPointStops(g.Stops, g.Bell);
        var markup = StopMarkup(stops);
        var outer = stops[^1].Color;
        var inner = stops[0].Color;
        var sb = new StringBuilder();
        sb.Append($"<rect x=\"-1\" y=\"-1\" width=\"{Units.Css(w + 2)}\" height=\"{Units.Css(h + 2)}\"{Color("fill", outer)}/>");
        void Side(string sideId, (double X1, double Y1, double X2, double Y2) vector, params (double X, double Y)[] points)
        {
            if (Math.Abs(vector.X2 - vector.X1) + Math.Abs(vector.Y2 - vector.Y1) < 1e-6)
                return;
            defs.Append($"<linearGradient id=\"{sideId}\" gradientUnits=\"userSpaceOnUse\" x1=\"{Units.Css(vector.X1)}\" y1=\"{Units.Css(vector.Y1)}\" x2=\"{Units.Css(vector.X2)}\" y2=\"{Units.Css(vector.Y2)}\">{markup}</linearGradient>");
            sb.Append($"<path d=\"M{string.Join("L", points.Select(p => $"{Units.Css(p.X)} {Units.Css(p.Y)}"))}Z\" fill=\"url(#{sideId})\"/>");
        }
        Side(NewId("g"), (0, focus.T, 0, tile.T), (tile.L, tile.T), (tile.R, tile.T), (focus.R, focus.T), (focus.L, focus.T));
        Side(NewId("g"), (focus.R, 0, tile.R, 0), (tile.R, tile.T), (tile.R, tile.B), (focus.R, focus.B), (focus.R, focus.T));
        Side(NewId("g"), (0, focus.B, 0, tile.B), (tile.R, tile.B), (tile.L, tile.B), (focus.L, focus.B), (focus.R, focus.B));
        Side(NewId("g"), (focus.L, 0, tile.L, 0), (tile.L, tile.B), (tile.L, tile.T), (focus.L, focus.T), (focus.L, focus.B));
        if (focus.R - focus.L > 1e-6 && focus.B - focus.T > 1e-6)
            sb.Append($"<rect x=\"{Units.Css(focus.L)}\" y=\"{Units.Css(focus.T)}\" width=\"{Units.Css(focus.R - focus.L)}\" height=\"{Units.Css(focus.B - focus.T)}\"{Color("fill", inner)}/>");
        defs.Append($"<pattern id=\"{id}\" patternUnits=\"userSpaceOnUse\"{PatternOrigin(offset)} width=\"{Units.Css(w)}\" height=\"{Units.Css(h)}\">{sb}</pattern>");
        return id;
    }

    /// <summary>The x and y of a pattern tile placed at <paramref name="offset"/> (its content moves along).</summary>
    static string PatternOrigin((double X, double Y) offset) =>
        offset == default ? "" : $" x=\"{Units.Css(offset.X)}\" y=\"{Units.Css(offset.Y)}\"";

    /// <summary>
    /// A path gradient that follows the shape (path="shape", catalog: fill.gradient): the shape's outline repeated in
    /// bands, each the box mapped onto a rectangle between the tile rectangle (offset 1) and the focus rectangle
    /// (offset 0) and filled with the gradient's color there (measured: concentric stars, triangles, hearts and arrows
    /// about the focus, not rectangles).
    /// </summary>
    string ShapeGradient(GradientFill g, string id, double w, double h, string outline, StringBuilder defs, (double X, double Y) offset = default)
    {
        var stops = PowerPointStops(g.Stops, g.Bell);
        var (fl, ft, fr, fb) = g.FocusRect;
        var (tl, tt, tr, tb) = g.TileRect;
        var focus = (L: w * fl, T: h * ft, R: w * (1 - fr), B: h * (1 - fb));
        var tile = (L: w * tl, T: h * tt, R: w * (1 - tr), B: h * (1 - tb));
        var pathId = NewId("gp");
        defs.Append($"<path id=\"{pathId}\" d=\"{outline}\"{(offset == default ? "" : $" transform=\"translate({Units.Css(-offset.X)} {Units.Css(-offset.Y)})\"")}/>");
        var sb = new StringBuilder();
        sb.Append($"<rect x=\"-1\" y=\"-1\" width=\"{Units.Css(w + 2)}\" height=\"{Units.Css(h + 2)}\"{Color("fill", stops[^1].Color)}/>");
        const int Bands = 64;
        void Band(double t, RgbColor color)
        {
            var (l, top) = (focus.L + (tile.L - focus.L) * t, focus.T + (tile.T - focus.T) * t);
            var (r, b) = (focus.R + (tile.R - focus.R) * t, focus.B + (tile.B - focus.B) * t);
            var (sx, sy) = ((r - l) / Math.Max(w, 1e-9), (b - top) / Math.Max(h, 1e-9));
            if (sx > 1e-6 && sy > 1e-6)
                sb.Append($"<use href=\"#{pathId}\" transform=\"matrix({Units.Css4(sx)} 0 0 {Units.Css4(sy)} {Units.Css(l)} {Units.Css(top)})\"{Color("fill", color)}/>");
        }
        for (var k = Bands; k >= 1; k--)
            Band((double)k / Bands, ColorAt(stops, (k - 0.5) / Bands));
        Band(0, stops[0].Color);
        defs.Append($"<pattern id=\"{id}\" patternUnits=\"userSpaceOnUse\"{PatternOrigin(offset)} width=\"{Units.Css(w)}\" height=\"{Units.Css(h)}\">{sb}</pattern>");
        return id;
    }

    /// <summary>The color of sorted stops at a position, interpolated in sRGB between its neighbors.</summary>
    static RgbColor ColorAt(List<GradientStop> stops, double position)
    {
        if (position <= stops[0].Position)
            return stops[0].Color;
        for (var i = 1; i < stops.Count; i++)
        {
            if (position <= stops[i].Position)
            {
                var span = stops[i].Position - stops[i - 1].Position;
                if (span <= 1e-9)
                    return stops[i].Color;
                var (a, b, t) = (stops[i - 1].Color, stops[i].Color, (position - stops[i - 1].Position) / span);
                // Like the browser between gradient stops (measured: PowerPoint's path gradients mix in sRGB too).
                return new RgbColor(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);
            }
        }
        return stops[^1].Color;
    }

    static string StopMarkup(IEnumerable<GradientStop> stops)
    {
        var sb = new StringBuilder();
        foreach (var s in stops)
        {
            sb.Append($"<stop offset=\"{Units.Css4(s.Position)}\" stop-color=\"{s.Color.ToHex()}\"");
            if (s.Color.A < 0.999)
                sb.Append($" stop-opacity=\"{Units.Css4(s.Color.A)}\"");
            sb.Append("/>");
        }
        return sb.ToString();
    }

    /// <summary>
    /// The stops as the browser should interpolate them to look like PowerPoint (measured with gradient strips and
    /// PowerPoint's own EMF export, catalog: fill.gradient). A two-color gradient (stops at 0 and 1) and a symmetric
    /// three-color one (A, B, A) are drawn like a GDI+ gradient with a sigma bell blend (normal distribution,
    /// σ = 0.25, normalized to 0…1) mixed in linear light; every other gradient is interpolated linearly in sRGB,
    /// which is what browsers do anyway.
    /// </summary>
    public static List<GradientStop> PowerPointStops(List<GradientStop> stops, bool bell)
    {
        if (stops.Count == 0 || !bell)
            return stops;
        var twoColor = stops.Count == 2;
        var symmetric = stops.Count == 3;
        if (!twoColor && !symmetric)
            return stops;

        // A to B over [start, end] with the bell; the second half of a symmetric gradient runs back from B to A.
        const int Steps = 32;
        var result = new List<GradientStop>();
        void Segment(RgbColor a, RgbColor b, double start, double end, bool back)
        {
            for (var k = 0; k <= Steps; k++)
            {
                var u = (double)k / Steps;
                var position = start + (end - start) * u;
                result.Add(new GradientStop(position, Mix(a, b, Bell(back ? 1 - u : u))));
            }
        }
        if (twoColor)
        {
            Segment(stops[0].Color, stops[1].Color, 0, 1, back: false);
        }
        else
        {
            var focus = stops[1].Position;
            Segment(stops[0].Color, stops[1].Color, 0, focus, back: false);
            Segment(stops[2].Color, stops[1].Color, focus, 1, back: true);
        }
        return result;
    }

    /// <summary>GDI+ sigma bell blend: the normal distribution's CDF with σ = 0.25 around 0.5, scaled to 0…1.</summary>
    static double Bell(double t)
    {
        static double Cdf(double z) => 0.5 * (1 + Erf(z / Math.Sqrt(2)));
        var low = Cdf(-2);
        return Math.Clamp((Cdf((t - 0.5) / 0.25) - low) / (1 - 2 * low), 0, 1);
    }

    /// <summary>The error function (Abramowitz and Stegun 7.1.26, error below 1.5e-7).</summary>
    static double Erf(double x)
    {
        var sign = Math.Sign(x);
        x = Math.Abs(x);
        var t = 1 / (1 + 0.3275911 * x);
        var y = 1 - ((((1.061405429 * t - 1.453152027) * t + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
        return sign * y;
    }

    /// <summary>Mixes two colors in linear light.</summary>
    static RgbColor Mix(RgbColor a, RgbColor b, double t)
    {
        static double ToLinear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        static double ToSrgb(double c) => c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(Math.Max(c, 0), 1 / 2.4) - 0.055;
        double M(double x, double y) => ToSrgb(ToLinear(x) + (ToLinear(y) - ToLinear(x)) * t);
        return new RgbColor(M(a.R, b.R), M(a.G, b.G), M(a.B, b.B), a.A + (b.A - a.A) * t);
    }

    string Picture(PictureFill p, double w, double h, StringBuilder defs)
    {
        var id = NewId("p");
        var filter = "";
        if (p.Filter != null)
        {
            var filterId = NewId("bf");
            defs.Append($"<filter id=\"{filterId}\" x=\"0\" y=\"0\" width=\"1\" height=\"1\" color-interpolation-filters=\"sRGB\">{p.Filter}</filter>");
            filter = $" filter=\"url(#{filterId})\"";
        }

        if (p.Tiling is { } tile && p.NaturalSize.W > 0 && p.NaturalSize.H > 0)
            return Tiled(p, tile, id, w, h, filter, defs);

        var (fl, ft, fr, fb) = p.FillRect;
        var (sl, st, sr, sb) = p.SourceRect;
        var innerX = w * fl;
        var innerY = h * ft;
        var innerW = w * (1 - fl - fr);
        var innerH = h * (1 - ft - fb);
        var fullW = innerW / Math.Max(1e-6, 1 - sl - sr);
        var fullH = innerH / Math.Max(1e-6, 1 - st - sb);
        var (x, y) = (innerX - sl * fullW, innerY - st * fullH);

        // Pattern tiles repeat: a picture ending at the tile's edge blends with its opposite edge where the browser
        // samples across the seam. The tile reaches a margin beyond the box, and on the sides where the picture meets
        // the box's edge, bands repeat its outermost pixels over the margin (a sliver of the picture stretched), as
        // PowerPoint's picture edges keep their pixels' color. Filters act on the whole tile, not on the stretched
        // slivers.
        const double Margin = 4, Overlap = 0.5, Sliver = 0.01, Touch = 0.01;
        var imageId = NewId("pi");
        defs.Append($"<image id=\"{imageId}\" href=\"{p.Image}\" width=\"{Units.Css(fullW)}\" height=\"{Units.Css(fullH)}\" preserveAspectRatio=\"none\"/>");
        var content = new StringBuilder();
        void Band(double bx, double by, double bw, double bh, double vx, double vy, double vw, double vh) =>
            content.Append($"<svg x=\"{Units.Css(bx + Margin)}\" y=\"{Units.Css(by + Margin)}\" width=\"{Units.Css(bw)}\" height=\"{Units.Css(bh)}\" " +
                        $"viewBox=\"{Units.Css4(vx)} {Units.Css4(vy)} {Units.Css4(vw)} {Units.Css4(vh)}\" preserveAspectRatio=\"none\"><use href=\"#{imageId}\"/></svg>");
        var (l, t) = (x <= Touch, y <= Touch);
        var (r, b) = (x + fullW >= w - Touch, y + fullH >= h - Touch);
        var (band, far) = (Margin + Overlap, (X: x + fullW - Overlap, Y: y + fullH - Overlap));
        if (t) Band(x, y - Margin, fullW, band, 0, 0, fullW, Sliver);
        if (b) Band(x, far.Y, fullW, band, 0, fullH - Sliver, fullW, Sliver);
        if (l) Band(x - Margin, y, band, fullH, 0, 0, Sliver, fullH);
        if (r) Band(far.X, y, band, fullH, fullW - Sliver, 0, Sliver, fullH);
        if (t && l) Band(x - Margin, y - Margin, band, band, 0, 0, Sliver, Sliver);
        if (t && r) Band(far.X, y - Margin, band, band, fullW - Sliver, 0, Sliver, Sliver);
        if (b && l) Band(x - Margin, far.Y, band, band, 0, fullH - Sliver, Sliver, Sliver);
        if (b && r) Band(far.X, far.Y, band, band, fullW - Sliver, fullH - Sliver, Sliver, Sliver);
        content.Append($"<use href=\"#{imageId}\" transform=\"translate({Units.Css(x + Margin)} {Units.Css(y + Margin)})\"/>");
        defs.Append($"<pattern id=\"{id}\" patternUnits=\"userSpaceOnUse\" x=\"{Units.Css(-Margin)}\" y=\"{Units.Css(-Margin)}\" " +
                    $"width=\"{Units.Css(w + 2 * Margin)}\" height=\"{Units.Css(h + 2 * Margin)}\">" +
                    (filter.Length > 0 ? $"<g{filter}>{content}</g>" : content.ToString()) + "</pattern>");
        return id;
    }

    /// <summary>
    /// A tiled picture (catalog: fill.picture): tiles of the picture's own size times the scale, aligned to a box
    /// corner, edge or center and offset; flips mirror every other tile.
    /// </summary>
    string Tiled(PictureFill p, TileSpec tile, string id, double w, double h, string filter, StringBuilder defs)
    {
        const double PxPerPt = 96.0 / 72;
        var tw = p.NaturalSize.W * tile.Sx;
        var th = p.NaturalSize.H * tile.Sy;
        var flipX = tile.Flip is "x" or "xy";
        var flipY = tile.Flip is "y" or "xy";
        var cellW = flipX ? 2 * tw : tw;
        var cellH = flipY ? 2 * th : th;
        var ax = tile.Align switch { "t" or "ctr" or "b" => (w - tw) / 2, "tr" or "r" or "br" => w - tw, _ => 0 };
        var ay = tile.Align switch { "l" or "ctr" or "r" => (h - th) / 2, "bl" or "b" or "br" => h - th, _ => 0 };
        var ox = ax + tile.Tx * PxPerPt;
        var oy = ay + tile.Ty * PxPerPt;
        var images = new StringBuilder();
        void Add(bool mirrorX, bool mirrorY, double x, double y)
        {
            var transform = mirrorX || mirrorY
                ? $" transform=\"translate({Units.Css(x + (mirrorX ? tw : 0))} {Units.Css(y + (mirrorY ? th : 0))}) scale({(mirrorX ? -1 : 1)} {(mirrorY ? -1 : 1)})\""
                : $" transform=\"translate({Units.Css(x)} {Units.Css(y)})\"";
            images.Append($"<image href=\"{p.Image}\" width=\"{Units.Css(tw)}\" height=\"{Units.Css(th)}\" preserveAspectRatio=\"none\"{transform}{filter}/>");
        }
        Add(false, false, 0, 0);
        if (flipX) Add(true, false, tw, 0);
        if (flipY) Add(false, true, 0, th);
        if (flipX && flipY) Add(true, true, tw, th);
        defs.Append($"<pattern id=\"{id}\" patternUnits=\"userSpaceOnUse\" x=\"{Units.Css(ox)}\" y=\"{Units.Css(oy)}\" width=\"{Units.Css(cellW)}\" height=\"{Units.Css(cellH)}\">{images}</pattern>");
        return id;
    }
}
