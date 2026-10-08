using System.Text;
using Engine.Drawing;

namespace Engine.Html;

/// <summary>Turns fills and outlines into SVG paint attributes; gradients, patterns and images go into defs.</summary>
public sealed class SvgPaint
{
    int nextId;

    string NewId(string prefix) => $"{prefix}{++nextId}";

    /// <summary>fill/fill-opacity attributes for a shape box of w × h pixels.</summary>
    public string Fill(Fill fill, double w, double h, StringBuilder defs, double shapeRotation = 0) => fill switch
    {
        SolidFill s => Color("fill", s.Color),
        GradientFill g => $" fill=\"url(#{Gradient(g, w, h, defs, shapeRotation)})\"",
        PatternFill p => Color("fill", RgbColor.Mix(p.Background, p.Foreground, PatternDensity(p.Preset))),
        PictureFill p => $" fill=\"url(#{Picture(p, w, h, defs)})\"",
        _ => " fill=\"none\"",
    };

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
            sb.Append($" stroke-dasharray=\"{string.Join(' ', dash.Select(d => Units.Css(d * width)))}\"");
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
        var stops = new StringBuilder();
        foreach (var s in LinearLightStops(g.Stops))
        {
            stops.Append($"<stop offset=\"{Units.Css4(s.Position)}\" stop-color=\"{s.Color.ToHex()}\"");
            if (s.Color.A < 0.999)
                stops.Append($" stop-opacity=\"{Units.Css4(s.Color.A)}\"");
            stops.Append("/>");
        }

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
        }
        else
        {
            // Path gradient: from the focus rectangle (offset 0) to the edge of the tile rectangle (offset 1).
            var (fl, ft, fr, fb) = g.FocusRect;
            var (tl, tt, tr, tb) = g.TileRect;
            var cx = w * (fl + (1 - fr)) / 2;
            var cy = h * (ft + (1 - fb)) / 2;
            double[] xs = [w * tl, w * (1 - tr)];
            double[] ys = [h * tt, h * (1 - tb)];
            var r = xs.SelectMany(x => ys.Select(y => Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)))).Max();
            defs.Append($"<radialGradient id=\"{id}\" gradientUnits=\"userSpaceOnUse\" cx=\"{Units.Css(cx)}\" cy=\"{Units.Css(cy)}\" r=\"{Units.Css(Math.Max(r, 1))}\">{stops}</radialGradient>");
        }
        return id;
    }

    /// <summary>
    /// PowerPoint blends gradient colors in linear light, browsers in sRGB; intermediate stops computed in linear
    /// light make the browser's interpolation follow PowerPoint's curve.
    /// </summary>
    static IEnumerable<GradientStop> LinearLightStops(List<GradientStop> stops)
    {
        const int Steps = 8;
        static double ToLinear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        static double ToSrgb(double c) => c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055;

        for (var i = 0; i < stops.Count; i++)
        {
            yield return stops[i];
            if (i + 1 == stops.Count)
                break;
            var (a, b) = (stops[i], stops[i + 1]);
            for (var k = 1; k < Steps; k++)
            {
                var t = (double)k / Steps;
                double Mix(double x, double y) => ToSrgb(ToLinear(x) + (ToLinear(y) - ToLinear(x)) * t);
                var color = new RgbColor(Mix(a.Color.R, b.Color.R), Mix(a.Color.G, b.Color.G), Mix(a.Color.B, b.Color.B), a.Color.A + (b.Color.A - a.Color.A) * t);
                yield return new GradientStop(a.Position + (b.Position - a.Position) * t, color);
            }
        }
    }

    string Picture(PictureFill p, double w, double h, StringBuilder defs)
    {
        var id = NewId("p");
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
                    $"width=\"{Units.Css(fullW)}\" height=\"{Units.Css(fullH)}\" preserveAspectRatio=\"none\"/></pattern>");
        return id;
    }
}
