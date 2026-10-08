using System.Text;
using FreeSlate.Drawing;

namespace FreeSlate.Html;

/// <summary>Turns fills and outlines into SVG paint attributes; gradients, patterns and images go into defs.</summary>
public sealed class SvgPaint
{
    int nextId;

    string NewId(string prefix) => $"{prefix}{++nextId}";

    /// <summary>fill/fill-opacity attributes for a shape box of w × h pixels whose origin lies at <paramref name="origin"/> on the slide.</summary>
    public string Fill(Fill fill, double w, double h, StringBuilder defs, double shapeRotation = 0, (double X, double Y) origin = default) => fill switch
    {
        SolidFill s => Color("fill", s.Color),
        GradientFill g => $" fill=\"url(#{Gradient(g, w, h, defs, shapeRotation)})\"",
        PatternFill p => $" fill=\"url(#{Pattern(p, defs, origin)})\"",
        PictureFill p => $" fill=\"url(#{Picture(p, w, h, defs)})\"",
        _ => " fill=\"none\"",
    };

    /// <summary>
    /// A preset pattern as an 8×8 pixel tile. PowerPoint aligns the tiles to the slide, so the pattern is shifted
    /// by the shape's position (harvested and verified: tools/Fidelity harvest-patterns).
    /// </summary>
    string Pattern(PatternFill p, StringBuilder defs, (double X, double Y) origin)
    {
        var id = NewId("t");
        var tile = PatternPresets.Tiles.GetValueOrDefault(p.Preset) ?? PatternPresets.Tiles["pct50"];
        var bits = new StringBuilder();
        for (var row = 0; row < 8; row++)
        {
            for (var column = 0; column < 8; column++)
            {
                if ((tile[row] & (0x80 >> column)) != 0)
                    bits.Append($"M{column} {row}h1v1h-1z");
            }
        }
        double Phase(double v) => ((-v % 8) + 8) % 8;
        defs.Append($"<pattern id=\"{id}\" patternUnits=\"userSpaceOnUse\" width=\"8\" height=\"8\" patternTransform=\"translate({Units.Css(Phase(origin.X))} {Units.Css(Phase(origin.Y))})\">" +
                    $"<rect width=\"8\" height=\"8\"{Color("fill", p.Background)} shape-rendering=\"crispEdges\"/>" +
                    (bits.Length > 0 ? $"<path d=\"{bits}\"{Color("fill", p.Foreground)} shape-rendering=\"crispEdges\"/>" : "") +
                    "</pattern>");
        return id;
    }

    public string Stroke(Outline? outline, double w, double h, StringBuilder defs)
    {
        if (outline is not { Visible: true })
            return " stroke=\"none\"";

        var width = Math.Max(Units.ToPx(outline.WidthEmu), 0.5);
        var sb = new StringBuilder(outline.Fill switch
        {
            SolidFill s => Color("stroke", s.Color),
            GradientFill g => $" stroke=\"url(#{Gradient(g, w, h, defs, 0)})\"",
            PatternFill p => Color("stroke", RgbColor.Mix(p.Background, p.Foreground, PatternDensity(p.Preset))),
            _ => " stroke=\"#000\"",
        });
        sb.Append($" stroke-width=\"{Units.Css(width)}\"");
        sb.Append(outline.Cap switch { "rnd" => " stroke-linecap=\"round\"", "sq" => " stroke-linecap=\"square\"", _ => "" });
        sb.Append(outline.Join switch
        {
            "round" => " stroke-linejoin=\"round\"",
            "bevel" => " stroke-linejoin=\"bevel\"",
            _ => $" stroke-linejoin=\"miter\" stroke-miterlimit=\"{Units.Css(Math.Max(1, outline.MiterLimit))}\"",
        });
        if (Dash(outline.Dash) is { } dash)
        {
            // PowerPoint's dash lengths include round and square caps; SVG adds the caps to every dash.
            var capped = outline.Cap is "rnd" or "sq";
            var lengths = dash.Select((d, i) => i % 2 == 0 ? (capped ? Math.Max(0, d - 1) : d) : (capped ? d + 1 : d));
            sb.Append($" stroke-dasharray=\"{string.Join(' ', lengths.Select(d => Units.Css(d * width)))}\"");
        }
        return sb.ToString();
    }

    static string Color(string attribute, RgbColor color) =>
        color.A < 0.999
            ? $" {attribute}=\"{color.ToHex()}\" {attribute}-opacity=\"{Units.Css4(color.A)}\""
            : $" {attribute}=\"{color.ToHex()}\"";

    /// <summary>Preset dash patterns in multiples of the line width (ST_PresetLineDashVal).</summary>
    static double[]? Dash(string preset) => preset switch
    {
        "dot" => [1, 3],
        "dash" => [4, 3],
        "lgDash" => [8, 3],
        "dashDot" => [4, 3, 1, 3],
        "lgDashDot" => [8, 3, 1, 3],
        "lgDashDotDot" => [8, 3, 1, 3, 1, 3],
        "sysDash" => [3, 1],
        "sysDot" => [1, 1],
        "sysDashDot" => [3, 1, 1, 1],
        "sysDashDotDot" => [3, 1, 1, 1, 1, 1],
        _ => null,
    };

    /// <summary>Rough share of foreground pixels until patterns are drawn as real SVG patterns.</summary>
    static double PatternDensity(string preset) => preset switch
    {
        _ when preset.StartsWith("pct", StringComparison.Ordinal) && int.TryParse(preset[3..], out var pct) => pct / 100.0,
        _ when preset.StartsWith("lt", StringComparison.Ordinal) => 0.25,
        _ when preset.StartsWith("dk", StringComparison.Ordinal) => 0.5,
        _ => 0.35,
    };

    string Gradient(GradientFill g, double w, double h, StringBuilder defs, double shapeRotation)
    {
        var id = NewId("g");
        var stops = StopMarkup(PowerPointStops(g.Stops, g.Bell));

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
                         $"gradientTransform=\"{counterRotation.Trim()} scale({Units.Css(w)} {Units.Css(h)})\"";
            }
            else
            {
                var half = (Math.Abs(w * cos) + Math.Abs(h * sin)) / 2;
                coords = $"x1=\"{Units.Css(w / 2 - half * cos)}\" y1=\"{Units.Css(h / 2 - half * sin)}\" x2=\"{Units.Css(w / 2 + half * cos)}\" y2=\"{Units.Css(h / 2 + half * sin)}\"" +
                         (counterRotation.Length > 0 ? $" gradientTransform=\"{counterRotation.Trim()}\"" : "");
            }
            defs.Append($"<linearGradient id=\"{id}\" gradientUnits=\"userSpaceOnUse\" {coords}>{stops}</linearGradient>");
            return id;
        }

        var (fl, ft, fr, fb) = g.FocusRect;
        var (tl, tt, tr, tb) = g.TileRect;
        if (g.Path is "rect" or "shape")
            return RectGradient(g, id, w, h, defs);

        // Circle: from the focus rectangle (offset 0) to the edge of the tile rectangle (offset 1).
        var cx = w * (fl + (1 - fr)) / 2;
        var cy = h * (ft + (1 - fb)) / 2;
        double[] xs = [w * tl, w * (1 - tr)];
        double[] ys = [h * tt, h * (1 - tb)];
        var r = xs.SelectMany(x => ys.Select(y => Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)))).Max();
        defs.Append($"<radialGradient id=\"{id}\" gradientUnits=\"userSpaceOnUse\" cx=\"{Units.Css(cx)}\" cy=\"{Units.Css(cy)}\" r=\"{Units.Css(Math.Max(r, 1))}\">{stops}</radialGradient>");
        return id;
    }

    /// <summary>
    /// A rectangular path gradient (path="rect"; "shape" uses the same construction): concentric rectangles from
    /// the focus rectangle (offset 0) to the tile rectangle (offset 1), drawn exactly as four trapezoids with linear
    /// gradients perpendicular to their side, inside a pattern the size of the shape.
    /// </summary>
    string RectGradient(GradientFill g, string id, double w, double h, StringBuilder defs)
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
        defs.Append($"<pattern id=\"{id}\" patternUnits=\"userSpaceOnUse\" width=\"{Units.Css(w)}\" height=\"{Units.Css(h)}\">{sb}</pattern>");
        return id;
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
        defs.Append($"<pattern id=\"{id}\" patternUnits=\"userSpaceOnUse\" width=\"{Units.Css(w)}\" height=\"{Units.Css(h)}\">" +
                    $"<image href=\"{p.Image}\" x=\"{Units.Css(innerX - sl * fullW)}\" y=\"{Units.Css(innerY - st * fullH)}\" " +
                    $"width=\"{Units.Css(fullW)}\" height=\"{Units.Css(fullH)}\" preserveAspectRatio=\"none\"{filter}/></pattern>");
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
