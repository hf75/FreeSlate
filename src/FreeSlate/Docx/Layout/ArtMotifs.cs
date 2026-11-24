using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FreeSlate.Drawing;

namespace FreeSlate.Docx.Layout;

/// <summary>
/// The motifs FreeSlate draws for Word's art page borders that are pictures (apples, balloons, celtic knots …; catalog:
/// section.pageBorders), its own drawings in the spirit of Word's (Word's pictures are its own and are not copied): each art's side
/// tile and corner tile as vector shapes, from Resources/artMotifs.json. A tile's box is 0 to 100 times its length along the side
/// (left to right on the top side) and 0 to 100 across (0 at the page's edge); a corner's 0 to 100 both ways from the top left
/// corner. Upright motifs (pictures) stand so on every side and in every corner; the others (bands) turn with the side: the right
/// side's tile is the top's turned a quarter clockwise, the bottom's a half, the left's three quarters, the corners alike. Where an
/// art is colourable, "color" paints the border's colour. Where Word draws a side or a corner otherwise than the rest (birds only at
/// the top and the bottom, a push pin in one corner), a motif gives that side its own tile ("top", "right", "bottom", "left": drawn
/// and turned as the side tile, none where empty) and a corner its own ("topLeft", "topRight", "bottomRight", "bottomLeft": shapes
/// as they stand in the corner's square, not turned, or the name of a side whose tile the corner continues).
/// </summary>
public static partial class ArtMotifs
{
    /// <summary>A path's segment: M, L, C, Q or Z, its points absolute in the tile's box.</summary>
    internal readonly record struct Segment(char Command, double[] Points);

    internal sealed record Shape(Segment[] Path, string? Fill, string? Stroke, double Width, double Opacity);

    /// <summary>An art's motif (see the class).</summary>
    public sealed class Motif
    {
        internal bool Colorable { get; init; }
        internal bool Upright { get; init; }

        /// <summary>The side tile's length along its side in border widths.</summary>
        public double Length { get; init; } = 1;

        internal Shape[] Side { get; init; } = [];
        internal Shape[]? Corner { get; init; }

        /// <summary>Single sides' tiles in place of Side's (top, right, bottom, left; null where Side's serves), turned alike.</summary>
        internal Shape[]?[] Sides { get; init; } = new Shape[]?[4];

        /// <summary>Single corners' tiles in place of Corner's (top left, top right, bottom right, bottom left), as they stand.</summary>
        internal Shape[]?[] Corners { get; init; } = new Shape[]?[4];

        /// <summary>The side (0 top to 3 left) whose tile each corner continues, squeezed into its square; -1 where none.</summary>
        internal int[] Continues { get; init; } = [-1, -1, -1, -1];
    }

    static readonly string[] SideNames = ["top", "right", "bottom", "left"];
    static readonly string[] CornerNames = ["topLeft", "topRight", "bottomRight", "bottomLeft"];

    static readonly Lazy<Dictionary<string, Motif>> motifs = new(Load);

    /// <summary>The motif FreeSlate draws for an art border, or null where it has none.</summary>
    public static Motif? Of(string style) => motifs.Value.GetValueOrDefault(style);

    static Dictionary<string, Motif> Load()
    {
        var result = new Dictionary<string, Motif>(StringComparer.Ordinal);
        using var stream = typeof(ArtMotifs).Assembly.GetManifestResourceStream("artMotifs.json");
        if (stream == null)
            return result;
        using var json = JsonDocument.Parse(stream);
        foreach (var art in json.RootElement.EnumerateObject())
        {
            var value = art.Value;
            bool Flag(string name) => value.TryGetProperty(name, out var flag) && flag.ValueKind == JsonValueKind.True;
            Shape[]? List(string name) => value.TryGetProperty(name, out var list) && list.ValueKind == JsonValueKind.Array ? Shapes(list) : null;
            result[art.Name] = new Motif
            {
                Colorable = Flag("colorable"),
                Upright = Flag("upright"),
                Length = value.TryGetProperty("length", out var length) && length.TryGetDouble(out var l) && l > 0 ? l : 1,
                Side = List("side") ?? [],
                Corner = List("corner"),
                Sides = [.. SideNames.Select(List)],
                Corners = [.. CornerNames.Select(List)],
                Continues = [.. CornerNames.Select(n => value.TryGetProperty(n, out var named) && named.ValueKind == JsonValueKind.String
                    ? Array.IndexOf(SideNames, named.GetString()) : -1)],
            };
        }
        return result;
    }

    static Shape[] Shapes(JsonElement list)
    {
        var shapes = new List<Shape>();
        foreach (var item in list.EnumerateArray())
        {
            string? Text(string name) => item.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            double Number(string name, double otherwise) => item.TryGetProperty(name, out var v) && v.TryGetDouble(out var d) ? d : otherwise;
            double[] Numbers(string name) => item.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
                ? [.. v.EnumerateArray().Select(n => n.TryGetDouble(out var d) ? d : 0)] : [];
            Segment[] path;
            if (Text("d") is { } d)
                path = Parse(d);
            else if (Numbers("rect") is [var x, var y, var w, var h])
                path = [new('M', [x, y]), new('L', [x + w, y]), new('L', [x + w, y + h]), new('L', [x, y + h]), new('Z', [])];
            else if (Numbers("polygon") is { Length: >= 6 } points)
                path = [new('M', [points[0], points[1]]), .. Enumerable.Range(1, points.Length / 2 - 1).Select(k => new Segment('L', [points[2 * k], points[2 * k + 1]])),
                    new('Z', [])];
            else if (Numbers("circle") is [var cx, var cy, var r])
                path = Ellipse(cx, cy, r, r);
            else if (Numbers("ellipse") is [var ex, var ey, var rx, var ry])
                path = Ellipse(ex, ey, rx, ry);
            else
                continue;
            shapes.Add(new Shape(path, Text("fill"), Text("stroke"), Number("width", 0), Number("opacity", 1)));
        }
        return [.. shapes];
    }

    /// <summary>An ellipse as four cubic curves.</summary>
    static Segment[] Ellipse(double cx, double cy, double rx, double ry)
    {
        const double k = 0.5522847498;
        return [new('M', [cx + rx, cy]), new('C', [cx + rx, cy + k * ry, cx + k * rx, cy + ry, cx, cy + ry]),
            new('C', [cx - k * rx, cy + ry, cx - rx, cy + k * ry, cx - rx, cy]), new('C', [cx - rx, cy - k * ry, cx - k * rx, cy - ry, cx, cy - ry]),
            new('C', [cx + k * rx, cy - ry, cx + rx, cy - k * ry, cx + rx, cy]), new('Z', [])];
    }

    [GeneratedRegex(@"[MmLlHhVvCcSsQqTtZz]|[-+]?(?:\d*\.\d+|\d+\.?)(?:[eE][-+]?\d+)?")]
    private static partial Regex Tokens();

    /// <summary>SVG path data of M L H V C S Q T Z (and their relative forms) as absolute M, L, C, Q and Z segments.</summary>
    static Segment[] Parse(string data)
    {
        var tokens = Tokens().Matches(data).Select(m => m.Value).ToList();
        var segments = new List<Segment>();
        var (i, command) = (0, 'M');
        double x = 0, y = 0, startX = 0, startY = 0;
        (double X, double Y)? cubic = null, quadratic = null;
        double Next() => double.Parse(tokens[i++], NumberStyles.Float, CultureInfo.InvariantCulture);
        while (i < tokens.Count)
        {
            if (char.IsLetter(tokens[i][0]))
            {
                command = tokens[i++][0];
                if (command is 'Z' or 'z')
                {
                    segments.Add(new('Z', []));
                    (x, y, cubic, quadratic) = (startX, startY, null, null);
                    continue;
                }
            }
            if (i >= tokens.Count)
                break;
            var relative = char.IsLower(command);
            var (dx, dy) = relative ? (x, y) : (0.0, 0.0);
            switch (char.ToUpperInvariant(command))
            {
                case 'M':
                    (x, y) = (Next() + dx, Next() + dy);
                    (startX, startY) = (x, y);
                    segments.Add(new('M', [x, y]));
                    command = relative ? 'l' : 'L';
                    (cubic, quadratic) = (null, null);
                    break;
                case 'L':
                    (x, y) = (Next() + dx, Next() + dy);
                    segments.Add(new('L', [x, y]));
                    (cubic, quadratic) = (null, null);
                    break;
                case 'H':
                    x = Next() + dx;
                    segments.Add(new('L', [x, y]));
                    (cubic, quadratic) = (null, null);
                    break;
                case 'V':
                    y = Next() + dy;
                    segments.Add(new('L', [x, y]));
                    (cubic, quadratic) = (null, null);
                    break;
                case 'C':
                {
                    double[] p = [Next() + dx, Next() + dy, Next() + dx, Next() + dy, Next() + dx, Next() + dy];
                    segments.Add(new('C', p));
                    (cubic, quadratic, x, y) = ((p[2], p[3]), null, p[4], p[5]);
                    break;
                }
                case 'S':
                {
                    var first = cubic is var (cx, cy) ? (2 * x - cx, 2 * y - cy) : (x, y);
                    double[] p = [first.Item1, first.Item2, Next() + dx, Next() + dy, Next() + dx, Next() + dy];
                    segments.Add(new('C', p));
                    (cubic, quadratic, x, y) = ((p[2], p[3]), null, p[4], p[5]);
                    break;
                }
                case 'Q':
                {
                    double[] p = [Next() + dx, Next() + dy, Next() + dx, Next() + dy];
                    segments.Add(new('Q', p));
                    (quadratic, cubic, x, y) = ((p[0], p[1]), null, p[2], p[3]);
                    break;
                }
                case 'T':
                {
                    var control = quadratic is var (qx, qy) ? (2 * x - qx, 2 * y - qy) : (x, y);
                    double[] p = [control.Item1, control.Item2, Next() + dx, Next() + dy];
                    segments.Add(new('Q', p));
                    (quadratic, cubic, x, y) = (control, null, p[2], p[3]);
                    break;
                }
                default:
                    // An arc or anything else the motifs do not use: the rest of the path left out.
                    return [.. segments];
            }
        }
        return [.. segments];
    }

    /// <summary>
    /// A tile's shapes drawn, each point of the box placed on the page by <paramref name="place"/>; strokes as wide in points as their
    /// width in the box's units of <paramref name="unit"/> points; "color" the border's colour.
    /// </summary>
    static void Draw(Shape[] tile, Func<double, double, (double X, double Y)> place, double unit, RgbColor color, List<ArtShape> shapes)
    {
        foreach (var shape in tile)
        {
            var path = new StringBuilder();
            foreach (var segment in shape.Path)
            {
                path.Append(segment.Command);
                for (var k = 0; k + 1 < segment.Points.Length; k += 2)
                {
                    var (px, py) = place(segment.Points[k], segment.Points[k + 1]);
                    if (k > 0)
                        path.Append(' ');
                    path.Append(Css(px)).Append(' ').Append(Css(py));
                }
            }
            RgbColor? Paint(string? value) => value is null or "none" ? null : value == "color" ? color
                : value.Length == 7 && value[0] == '#' ? RgbColor.FromHex(value[1..]) : null;
            var (fill, stroke) = (Paint(shape.Fill), Paint(shape.Stroke));
            if (fill == null && stroke == null)
                continue;
            shapes.Add(new ArtShape(path.ToString(), fill, stroke, stroke != null ? Math.Max(0.05, shape.Width * unit) : 0)
                { Opacity = shape.Opacity, Round = true });
        }
    }

    /// <summary>
    /// A side's tile, the box's length along it from <paramref name="start"/> for <paramref name="tile"/> points, its outer edge at
    /// <paramref name="edge"/> (the bottom's and the right's far from the page's top and left).
    /// </summary>
    internal static void Side(Motif motif, RgbColor color, double width, bool horizontal, bool far, double edge, double start, double tile, List<ArtShape> shapes)
    {
        var length = 100 * motif.Length;
        (double X, double Y) Place(double x, double y)
        {
            var (u, v) = (x / length, y / 100);
            if (motif.Upright)
            {
                // Upright: the picture stands as drawn, its box the tile's (along the side across the page, down it along).
                var near = far ? edge - width : edge;
                return horizontal ? (start + u * tile, near + v * width) : (near + x / 100 * width, start + y / 100 * tile);
            }
            // Turned with the side: the bottom's and the left's run back along it.
            var along = horizontal == far ? 1 - u : u;
            var across = far ? edge - v * width : edge + v * width;
            return horizontal ? (start + along * tile, across) : (across, start + along * tile);
        }
        Draw(motif.Sides[horizontal ? (far ? 2 : 0) : (far ? 1 : 3)] ?? motif.Side, Place, width / 100, Painted(motif, color), shapes);
    }

    /// <summary>A corner's tile where the outer edges <paramref name="x"/> and <paramref name="y"/> meet.</summary>
    internal static void Corner(Motif motif, RgbColor color, double width, double x, double y, bool right, bool bottom, List<ArtShape> shapes)
    {
        var (left, top) = (right ? x - width : x, bottom ? y - width : y);
        var index = (right, bottom) switch { (false, false) => 0, (true, false) => 1, (true, true) => 2, _ => 3 };
        if (motif.Continues[index] is var continued and >= 0)
        {
            // A side's tile continued into the corner, squeezed into its square.
            var horizontal = continued is 0 or 2;
            Side(motif, color, width, horizontal, continued is 1 or 2, horizontal ? y : x, horizontal ? left : top, width, shapes);
            return;
        }
        if (motif.Corners[index] is { } standing)
        {
            Draw(standing, (bx, by) => (left + bx / 100 * width, top + by / 100 * width), width / 100, Painted(motif, color), shapes);
            return;
        }
        // The side's picture serves as the corner's where the motif has none of its own, its box squeezed into the square.
        var (tile, length) = motif.Corner is { } own ? (own, 100.0) : (motif.Side, 100 * motif.Length);
        (double X, double Y) Place(double bx, double by)
        {
            var (u, v) = (bx / length, by / 100);
            if (motif.Upright || !right && !bottom)
                return (left + u * width, top + v * width);
            return (right, bottom) switch
            {
                (true, false) => (left + (1 - v) * width, top + u * width),
                (true, true) => (left + (1 - u) * width, top + (1 - v) * width),
                _ => (left + v * width, top + (1 - u) * width),
            };
        }
        Draw(tile, Place, width / 100, Painted(motif, color), shapes);
    }

    /// <summary>The colour "color" paints: the border's where the art takes it, else black.</summary>
    static RgbColor Painted(Motif motif, RgbColor color) => motif.Colorable ? color : RgbColor.FromHex("000000");

    static string Css(double points) => points.ToString("0.###", CultureInfo.InvariantCulture);
}
