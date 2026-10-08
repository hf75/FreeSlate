using System.Globalization;
using System.Text;
using System.Xml.Linq;
using FreeSlate.Drawing;

namespace FreeSlate.Pptx;

/// <summary>
/// Ink (p:contentPart with an InkML part, catalog: picture.ink): every trace drawn as a stroke with its brush (width,
/// color, transparency, tip). The smallest trace coordinates go to the content part's top left corner; a part as
/// large as its points plus the last brush's width (its natural size, as PowerPoint writes it) keeps the ink's
/// scale, any other size stretches the points to fill it; stroke widths never scale (measured with ten parts moved,
/// enlarged and at their natural size). A part with a highlighter (rasterOp maskPen) is drawn half transparent as a
/// whole.
/// </summary>
static class Ink
{
    static readonly XNamespace InkMl = "http://www.w3.org/2003/InkML";
    static readonly XNamespace InkAction = "http://schemas.microsoft.com/office/powerpoint/2014/inkAction";

    /// <summary>Whether a part is ink: InkML, or InkActionML (ink recorded for replay).</summary>
    public static bool IsInk(XDocument part) => part.Root?.Name == InkMl + "ink" || part.Root?.Name == InkAction + "actions";

    sealed record Brush(double Width, RgbColor Color, double Opacity, bool Rectangle, bool Highlighter = false);

    /// <summary>SVG markup of an InkML part drawn into a box of <paramref name="width"/> × <paramref name="height"/> pixels.</summary>
    public static string Render(XDocument ink, double width, double height)
    {
        var root = ink.Root;
        if (root == null)
            return "";
        var definitions = root.Descendants(InkMl + "definitions").ToList();
        var brushes = definitions.SelectMany(d => d.Elements(InkMl + "brush"))
            .Where(b => b.Attribute(XNamespace.Xml + "id") != null)
            .ToDictionary(b => "#" + b.Attribute(XNamespace.Xml + "id")!.Value, ReadBrush);
        var contexts = definitions.SelectMany(d => d.Elements(InkMl + "context"))
            .Where(c => c.Attribute(XNamespace.Xml + "id") != null)
            .ToDictionary(c => "#" + c.Attribute(XNamespace.Xml + "id")!.Value, c => c);

        // Traces with their brushes, points in the ink's units (channels X and Y wherever they stand).
        var traces = new List<(List<(double X, double Y)> Points, Brush Brush)>();
        Brush? previousBrush = null;
        // InkActionML adds strokes and may remove them again; the strokes still there are drawn.
        var removed = root.Descendants(InkAction + "action").Where(a => a.Attribute("type")?.Value == "remove")
            .SelectMany(a => a.Descendants(InkAction + "actionData")).Select(d => d.Attribute("ref")?.Value?.TrimStart('#'))
            .Where(r => r != null).ToHashSet();
        foreach (var trace in root.Descendants(InkMl + "trace"))
        {
            if (removed.Count > 0 && (removed.Contains(trace.Attribute(XNamespace.Xml + "id")?.Value) || removed.Contains(trace.Parent?.Attribute(XNamespace.Xml + "id")?.Value))
                || trace.Ancestors(InkAction + "action").Any(a => a.Attribute("type")?.Value == "remove"))
                continue;
            var brush = trace.Attribute("brushRef")?.Value is { } brushRef && brushes.TryGetValue(brushRef, out var b) ? b : previousBrush ?? new Brush(0.05, RgbColor.Black, 1, false);
            previousBrush = brush;
            var context = trace.Attribute("contextRef")?.Value is { } contextRef && contexts.TryGetValue(contextRef, out var c) ? c : contexts.Values.FirstOrDefault();
            var channels = context?.Descendants(InkMl + "channel").Select(ch => ch.Attribute("name")?.Value ?? "").ToList() ?? ["X", "Y"];
            var (ix, iy) = (Math.Max(0, channels.IndexOf("X")), Math.Max(0, channels.IndexOf("Y")));
            if (iy == ix) iy = ix + 1;
            var points = Points(trace.Value, Math.Max(channels.Count, Math.Max(ix, iy) + 1)).Select(p => (p[ix], p[iy])).ToList();
            if (points.Count > 0)
                traces.Add((points, brush));
        }
        if (traces.Count == 0)
            return "";

        var all = traces.SelectMany(t => t.Points).ToList();
        var (minX, maxX, minY, maxY) = (all.Min(p => p.X), all.Max(p => p.X), all.Min(p => p.Y), all.Max(p => p.Y));
        // One unit of the ink is 1/1000 cm unless the context says otherwise.
        var unit = 96 / 2.54 / Resolution(contexts.Values.FirstOrDefault());
        var natural = traces[^1].Brush.Width * 96 / 2.54;
        double Scale(double extent, double box)
        {
            var span = extent * unit;
            return span < 1e-6 || Math.Abs(box - (span + natural)) < 0.75 ? unit : box / extent;
        }
        var (sx, sy) = (Scale(maxX - minX, width), Scale(maxY - minY, height));
        var (ox, oy) = (0.0, 0.0);
        var highlighter = traces.Any(t => t.Brush.Highlighter);

        var svg = new StringBuilder();
        foreach (var (points, brush) in traces)
        {
            var d = new StringBuilder();
            for (var i = 0; i < points.Count; i++)
            {
                var (x, y) = (ox + (points[i].X - minX) * sx, oy + (points[i].Y - minY) * sy);
                d.Append(i == 0 ? 'M' : 'L').Append(Units.Css(x)).Append(' ').Append(Units.Css(y));
            }
            if (points.Count == 1)
                d.Append("h0.01");   // a dot
            var stroke = brush.Width * 96 / 2.54;
            var cap = brush.Rectangle ? "square" : "round";
            var join = brush.Rectangle ? "miter" : "round";
            svg.Append($"<path d=\"{d}\" fill=\"none\" stroke=\"{brush.Color.ToHex()}\" stroke-width=\"{Units.Css(stroke)}\" stroke-linecap=\"{cap}\" stroke-linejoin=\"{join}\"");
            var opacity = highlighter ? Math.Min(brush.Opacity, 0.5) : brush.Opacity;
            if (opacity < 0.999)
                svg.Append($" stroke-opacity=\"{Units.Css4(opacity)}\"");
            svg.Append("/>");
        }
        return svg.ToString();
    }

    static Brush ReadBrush(XElement brush)
    {
        string? Property(string name) => brush.Elements(InkMl + "brushProperty").FirstOrDefault(p => p.Attribute("name")?.Value == name)?.Attribute("value")?.Value;
        double Number(string? value, double fallback) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        var widthProperty = brush.Elements(InkMl + "brushProperty").FirstOrDefault(p => p.Attribute("name")?.Value == "width");
        var width = Number(widthProperty?.Attribute("value")?.Value, 0.05);
        if (widthProperty?.Attribute("units")?.Value is "mm") width /= 10;
        else if (widthProperty?.Attribute("units")?.Value is "in") width *= 2.54;
        var color = Property("color") is { } hex ? RgbColor.FromHex(hex) : RgbColor.Black;
        // transparency: 0 (opaque) to 255; highlighters (maskPen) show half.
        var opacity = 1 - Math.Clamp(Number(Property("transparency"), 0), 0, 255) / 255;
        var highlighter = Property("rasterOp") == "maskPen";
        return new Brush(width, color, opacity, Property("tip") == "rectangle", highlighter);
    }

    /// <summary>Points per centimeter of the context's X channel (1000 by default).</summary>
    static double Resolution(XElement? context)
    {
        var property = context?.Descendants(InkMl + "channelProperty").FirstOrDefault(p => p.Attribute("channel")?.Value == "X" && p.Attribute("name")?.Value == "resolution");
        return double.TryParse(property?.Attribute("value")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : 1000;
    }

    /// <summary>
    /// The points of an InkML trace: comma-separated points, values per channel; a value may switch its channel to
    /// explicit (!), first differences (') or second differences (") and the mode stays until it changes.
    /// </summary>
    static List<double[]> Points(string data, int channels)
    {
        var result = new List<double[]>();
        var modes = new char[channels];
        var last = new double[channels];
        var velocity = new double[channels];
        foreach (var point in data.Split(','))
        {
            var tokens = Tokens(point.Trim());
            if (tokens.Count == 0)
                continue;
            var values = new double[channels];
            for (var c = 0; c < channels; c++)
            {
                if (c >= tokens.Count)
                {
                    values[c] = last[c];
                    continue;
                }
                var (mode, number) = tokens[c];
                if (mode != '\0') modes[c] = mode == '!' ? '\0' : mode;
                var value = modes[c] switch
                {
                    '\'' => last[c] + number,
                    '"' => last[c] + velocity[c] + number,
                    _ => number,
                };
                velocity[c] = modes[c] switch { '\'' => number, '"' => velocity[c] + number, _ => value - last[c] };
                values[c] = value;
                last[c] = value;
            }
            result.Add(values);
        }
        return result;
    }

    /// <summary>The values of one point with their mode prefixes (! ' ").</summary>
    static List<(char Mode, double Value)> Tokens(string point)
    {
        var tokens = new List<(char, double)>();
        var i = 0;
        while (i < point.Length)
        {
            while (i < point.Length && char.IsWhiteSpace(point[i])) i++;
            if (i >= point.Length) break;
            var mode = point[i] is '!' or '\'' or '"' ? point[i++] : '\0';
            while (i < point.Length && char.IsWhiteSpace(point[i])) i++;
            var start = i;
            // A number: sign, digits, point, exponent; a new sign or mode starts the next value.
            if (i < point.Length && point[i] is '-' or '+') i++;
            while (i < point.Length && (char.IsDigit(point[i]) || point[i] is '.' or 'e' or 'E' || (point[i] is '-' or '+' && i > start && point[i - 1] is 'e' or 'E'))) i++;
            if (i == start)
            {
                i++;
                continue;
            }
            if (double.TryParse(point[start..i], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                tokens.Add((mode, value));
        }
        return tokens;
    }
}
