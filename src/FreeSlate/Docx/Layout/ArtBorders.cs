using System.Globalization;
using System.Text;
using FreeSlate.Drawing;

namespace FreeSlate.Docx.Layout;

/// <summary>A shape of an art border: SVG path data in points, its fill and outline.</summary>
public sealed record ArtShape(string Path, RgbColor? Fill, RgbColor? Stroke, double StrokeWidth);

/// <summary>
/// Art page borders (catalog: section.pageBorders) Word draws as tiles of its own pictures, measured with
/// docx/local/pgb-art, pgb-art2, pgb-art3 and pgb-c (sizes 10 to 31 pt on A4, Letter and A5 landscape): a square tile
/// of the border's width in each corner and between them on every side as many tiles as fit whole of the picture's
/// length, stretched to fill the side. The basic borders (dots, dashes, squares, thin and wide lines) are drawn here
/// from their geometry; Word's other art borders are pictures of its own and are left out.
/// </summary>
public static class ArtBorders
{
    /// <summary>Word's device dot in points.</summary>
    const double DeviceDot = 0.12;

    /// <summary>
    /// The length of a side tile of a basic art border in border widths, horizontal and vertical (from Word's tile
    /// counts at all sizes: the line borders' 4.42 to 4.52 and 5.57 to 5.89).
    /// </summary>
    static (double Horizontal, double Vertical)? Nominal(string style) => style switch
    {
        "basicBlackDots" or "basicWhiteDots" or "basicBlackSquares" or "basicWhiteSquares" => (1, 1),
        "basicBlackDashes" or "basicWhiteDashes" => (2.43, 2.43),
        "basicThinLines" or "basicWideInline" or "basicWideOutline" => (4.47, 5.73),
        "basicWideMidline" => (3.11, 4.1),
        _ => null,
    };

    /// <summary>
    /// How Word's tiles of the bottom and the right differ from the mirror of the top's and left's (docx/local/pgb-art and
    /// pgb-art2, the same in points at all sizes): the line borders' rules sit 0.24 pt further out (0.18 pt outside the
    /// outer edge, 0.06 pt inside it at the top), the wide bands at the edges are a dot wider on both edges (the middle
    /// one is not); the white dashes lie 0.24 to 0.38 pt further out.
    /// </summary>
    static (double Rules, double Shapes, double Grow) Far(string style) => style switch
    {
        "basicThinLines" or "basicWideInline" or "basicWideOutline" => (0.24, 0, DeviceDot),
        "basicWideMidline" => (0.24, 0, 0),
        "basicWhiteDashes" => (0, 0.24, 0),
        _ => (0, 0, 0),
    };

    /// <summary>Boxes widened by <paramref name="s"/> and <paramref name="v"/> (in widths) on both edges.</summary>
    static Part[] Grown(Part[] parts, double s, double v) =>
        s == 0 && v == 0 ? parts : [.. parts.Select(p => p is Box b ? b with { S0 = b.S0 - s, S1 = b.S1 + s, V0 = b.V0 - v, V1 = b.V1 + v } : p)];

    /// <summary>Whether Word's art border of a style is drawn here.</summary>
    public static bool Drawn(string style) => Nominal(style) != null;

    /// <summary>
    /// A motif in its tile: along (0 to 1, the tile's length) and across (0 at the border's outer edge, 1 at its inner
    /// edge); rectangles, ellipses, polygons and lines with their paint.
    /// </summary>
    abstract record Part(RgbColor? Fill, RgbColor? Outline, double Line);

    sealed record Box(double S0, double V0, double S1, double V1, RgbColor? Fill, RgbColor? Outline, double Line = 0) : Part(Fill, Outline, Line);

    sealed record Dot(double S, double V, double Radius, RgbColor? Fill, RgbColor? Outline) : Part(Fill, Outline, 0);

    sealed record Polygon((double S, double V)[] Points, RgbColor? Fill, RgbColor? Outline) : Part(Fill, Outline, 0);

    sealed record Rule(double S0, double V0, double S1, double V1, double Line) : Part(null, null, Line);

    /// <summary>
    /// A side tile's motif and a corner tile's (its along running from the vertical side's outer edge). The line borders'
    /// side tiles draw a rule along their inner edge too, the corners' tiles none, so that the inner rules of two sides
    /// meet at the corner tile's inner corner.
    /// </summary>
    static (Part[] Side, Part[] Corner) Motif(string style, RgbColor color, bool bottom)
    {
        var white = RgbColor.FromHex("FFFFFF");
        return style switch
        {
            // The black dashes' bottom pictures are their own: the dash runs from the page's edge of the border.
            "basicBlackDashes" when bottom => ([new Box(0.2005, 0.001, 0.7975, 0.47, color, color)],
                [new Box(0.031, 0.001, 0.996, 0.496, color, color), new Box(0.031, 0.001, 0.51, 0.996, color, color)]),
            "basicBlackDots" => ([new Dot(0.5, 0.497, 0.141, color, color)], [new Dot(0.497, 0.497, 0.141, color, color)]),
            "basicWhiteDots" => ([new Box(0, 0, 1, 0.997, color, color), new Dot(0.5, 0.497, 0.141, white, white)],
                [new Box(0, 0, 0.997, 0.997, color, color), new Dot(0.497, 0.497, 0.141, white, white)]),
            "basicBlackSquares" => ([new Box(0.3355, 0.335, 0.6645, 0.664, color, color)], [new Box(0.335, 0.335, 0.664, 0.664, color, color)]),
            "basicWhiteSquares" => ([new Box(0.3355, 0.335, 0.6645, 0.664, white, color)], [new Box(0.335, 0.335, 0.664, 0.664, white, color)]),
            "basicBlackDashes" => ([new Box(0.2005, 0.03, 0.7975, 0.51, color, color)],
                [new Box(0.031, 0.03, 0.996, 0.51, color, color), new Box(0.031, 0.03, 0.51, 0.996, color, color)]),
            "basicWhiteDashes" => ([new Box(0.1638, 0.02, 0.8354, 0.461, null, color)],
                [new Polygon([(0.884, 0.014), (0.023, 0.014), (0.023, 0.874), (0.476, 0.874), (0.476, 0.446), (0.884, 0.446)], white, color)]),
            "basicThinLines" => ([new Box(0, 0, 1, 0.989, white, white), new Rule(0, 0.003, 1, 0.003, 0.0145), new Rule(0, 1.003, 1, 1.003, 0.0145)],
                [new Box(0, 0, 0.989, 0.989, white, white), new Rule(0.003, 0.003, 0.992, 0.003, 0.0145), new Rule(0.003, 0.003, 0.003, 0.992, 0.0145)]),
            "basicWideInline" => ([new Box(0, 0, 1, 0.989, white, white), new Box(0, 0.401, 1, 0.989, color, color), new Rule(0, 0.003, 1, 0.003, 0.0145)],
                [new Box(0, 0, 0.989, 0.989, white, white), new Rule(0.003, 0.003, 0.992, 0.003, 0.0145), new Rule(0.003, 0.003, 0.003, 0.992, 0.0145),
                    new Box(0.401, 0.401, 0.989, 0.989, color, color)]),
            "basicWideMidline" => ([new Box(0, 0, 1, 0.992, white, white), new Box(0, 0.287, 1, 0.707, color, color), new Rule(0, 0.003, 1, 0.003, 0.0097),
                    new Rule(0, 1.003, 1, 1.003, 0.0097)],
                [new Box(0, 0, 0.992, 0.992, white, white), new Rule(0.003, 0.003, 0.995, 0.003, 0.0097), new Rule(0.003, 0.003, 0.003, 0.995, 0.0097),
                    new Box(0.287, 0.287, 0.993, 0.707, color, color), new Box(0.287, 0.287, 0.707, 0.993, color, color)]),
            "basicWideOutline" => ([new Box(0, 0, 1, 0.989, white, white), new Box(0, 0, 1, 0.588, color, color), new Rule(0, 1.003, 1, 1.003, 0.0145)],
                [new Box(0, 0, 0.989, 0.989, white, white), new Box(0, 0, 0.989, 0.588, color, color), new Box(0, 0, 0.588, 0.989, color, color)]),
            _ => ([], []),
        };
    }

    /// <summary>
    /// The tiles of one side whose outer edge is at <paramref name="edge"/> (the bottom's and the right's far from the
    /// page's top and left), between the corners' tiles from <paramref name="from"/> to <paramref name="to"/>.
    /// </summary>
    public static void Side(string style, RgbColor color, double width, bool horizontal, bool far, double edge, double from, double to, List<ArtShape> shapes)
    {
        if (Nominal(style) is not { } nominal || to - from <= 0.01 || width <= 0)
            return;
        // As many whole tiles as fit between the corners (a picture's length in whole dots of Word's device and one more:
        // 10 pt 84 dots, 12 pt 101, 16 pt 134), stretched to the side on the device's grid: each tile whole dots long,
        // the first ones a dot shorter than the last ones.
        var first = (int)Math.Ceiling(from / DeviceDot - 1e-6);
        var dots = (int)Math.Floor(to / DeviceDot + 1e-6) - first;
        var length = (Math.Floor((horizontal ? nominal.Horizontal : nominal.Vertical) * width / DeviceDot + 1e-6) + 1) * DeviceDot;
        var count = Math.Max(1, (int)Math.Floor((to - from) / length + 1e-9));
        var (shorter, longer) = (dots / count, dots % count);
        var (side, _) = Motif(style, color, horizontal && far);
        var outward = Far(style);
        if (far)
            side = Grown(side, 0, outward.Grow / width);
        for (var k = 0; k < count; k++)
        {
            var start = (first + k * shorter + Math.Max(0, k - (count - longer))) * DeviceDot;
            var tile = (shorter + (k >= count - longer ? 1 : 0)) * DeviceDot;
            (double X, double Y) At(double s, double v, bool rule)
            {
                var across = far ? edge + (rule ? outward.Rules : outward.Shapes) - v * width : edge + v * width;
                return horizontal ? (start + s * tile, across) : (across, start + s * tile);
            }
            Draw(side, At, width, color, shapes);
        }
    }

    /// <summary>A corner's tile at the corner where the outer edges <paramref name="x"/> and <paramref name="y"/> meet.</summary>
    public static void Corner(string style, RgbColor color, double width, double x, double y, bool right, bool bottom, List<ArtShape> shapes)
    {
        if (Nominal(style) == null || width <= 0)
            return;
        var (_, corner) = Motif(style, color, bottom);
        var outward = Far(style);
        corner = Grown(corner, right ? outward.Grow / width : 0, bottom ? outward.Grow / width : 0);
        (double X, double Y) At(double s, double v, bool rule)
        {
            var shift = rule ? outward.Rules : outward.Shapes;
            return (right ? x + shift - s * width : x + s * width, bottom ? y + shift - v * width : y + v * width);
        }
        Draw(corner, At, width, color, shapes);
    }

    static void Draw(Part[] parts, Func<double, double, bool, (double X, double Y)> place, double across, RgbColor color, List<ArtShape> shapes)
    {
        // Word outlines its fills with a hairline in the border's color.
        var hairline = Math.Max(0.14, 0.005 * across);
        foreach (var part in parts)
        {
            var path = new StringBuilder();
            (double X, double Y) at(double s, double v) => place(s, v, part is Rule);
            void Move((double X, double Y) p, char command) =>
                path.Append(command).Append(Css(p.X)).Append(' ').Append(Css(p.Y));
            switch (part)
            {
                case Box box:
                    Move(at(box.S0, box.V0), 'M');
                    Move(at(box.S1, box.V0), 'L');
                    Move(at(box.S1, box.V1), 'L');
                    Move(at(box.S0, box.V1), 'L');
                    path.Append('Z');
                    break;
                case Dot dot:
                    // A circle of the picture, stretched with its tile.
                    var center = at(dot.S, dot.V);
                    var (along, side) = (at(dot.S + dot.Radius, dot.V), at(dot.S, dot.V + dot.Radius));
                    var rx = Math.Abs(along.X - center.X) + Math.Abs(side.X - center.X);
                    var ry = Math.Abs(along.Y - center.Y) + Math.Abs(side.Y - center.Y);
                    path.Append($"M{Css(center.X - rx)} {Css(center.Y)}a{Css(rx)} {Css(ry)} 0 1 0 {Css(2 * rx)} 0a{Css(rx)} {Css(ry)} 0 1 0 {Css(-2 * rx)} 0Z");
                    break;
                case Polygon polygon:
                    for (var k = 0; k < polygon.Points.Length; k++)
                        Move(at(polygon.Points[k].S, polygon.Points[k].V), k == 0 ? 'M' : 'L');
                    path.Append('Z');
                    break;
                case Rule rule:
                    Move(at(rule.S0, rule.V0), 'M');
                    Move(at(rule.S1, rule.V1), 'L');
                    shapes.Add(new ArtShape(path.ToString(), null, color, Math.Max(0.14, rule.Line * across)));
                    continue;
            }
            shapes.Add(new ArtShape(path.ToString(), part.Fill, part.Outline, part.Outline != null ? hairline : 0));
        }
    }

    static string Css(double points) => points.ToString("0.###", CultureInfo.InvariantCulture);
}
