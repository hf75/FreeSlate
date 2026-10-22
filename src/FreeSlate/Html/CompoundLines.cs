using System.Text;
using FreeSlate.Drawing;
using FreeSlate.Drawing.Geometry;
using FreeSlate.Rendering;

namespace FreeSlate.Html;

/// <summary>
/// Compound outlines (a:ln/@cmpd, catalog: line.compound): parallel strokes within the line width, measured in
/// PowerPoint: dbl 1/3 line, 1/3 gap, 1/3 line; thinThick 1/5, 1/5, 3/5 with the thin line outside (left of the
/// path direction, which is outside for clockwise shapes); thickThin the reverse; tri 1/6, 1/6, 2/6, 1/6, 1/6.
/// The path is flattened and offset, so open and closed paths work alike.
/// </summary>
public static class CompoundLines
{
    /// <summary>Center offset (towards the outside) and width of each stroke, in line widths.</summary>
    public static (double Offset, double Width)[]? Parts(string compound) => compound switch
    {
        "dbl" => [(-1.0 / 3, 1.0 / 3), (1.0 / 3, 1.0 / 3)],
        "thinThick" => [(0.4, 0.2), (-0.2, 0.6)],
        "thickThin" => [(0.2, 0.6), (-0.4, 0.2)],
        "tri" => [(5.0 / 12, 1.0 / 6), (0, 1.0 / 3), (-5.0 / 12, 1.0 / 6)],
        _ => null,
    };

    /// <summary>
    /// The strokes as shape paths, each with an outline of its own width and a single line; <paramref name="stroke"/>
    /// gives the paint attributes of such an outline in HTML.
    /// </summary>
    public static List<SceneItem> Strokes(EvaluatedPath path, Outline outline, Func<Outline, string> stroke)
    {
        var parts = Parts(outline.Compound);
        if (parts == null)
            return [];
        var width = Math.Max(Units.ToPx(outline.WidthEmu), 0.5);
        var figures = Flatten(path.Segments);
        var items = new List<SceneItem>();
        foreach (var (offset, share) in parts)
        {
            var d = new StringBuilder();
            foreach (var (points, closed) in figures)
            {
                var shifted = Offset(points, closed, offset * width);
                if (shifted.Count < 2) continue;
                d.Append($"M{Units.Css(shifted[0].X)} {Units.Css(shifted[0].Y)}");
                for (var i = 1; i < shifted.Count; i++)
                    d.Append($"L{Units.Css(shifted[i].X)} {Units.Css(shifted[i].Y)}");
                if (closed) d.Append('Z');
            }
            if (d.Length > 0)
            {
                var part = outline with { WidthEmu = Math.Max(share * width, 0.25) * Units.EmuPerPixel, Compound = "sng" };
                items.Add(new ShapePathItem(d.ToString(), Fill.None, part, $" fill=\"none\"{stroke(part)}"));
            }
        }
        return items;
    }

    /// <summary>The path as polylines (figures), curves and arcs sampled every pixel or so.</summary>
    public static List<(List<(double X, double Y)> Points, bool Closed)> Flatten(List<PathSegment> segments)
    {
        var figures = new List<(List<(double X, double Y)>, bool)>();
        List<(double X, double Y)>? current = null;
        (double X, double Y) position = (0, 0), start = (0, 0);
        foreach (var s in segments)
        {
            var v = s.Values;
            switch (s.Command)
            {
                case 'M':
                    if (current is { Count: > 1 }) figures.Add((current, false));
                    position = start = (v[0], v[1]);
                    current = [position];
                    break;
                case 'L':
                    current ??= [position];
                    position = (v[0], v[1]);
                    current.Add(position);
                    break;
                case 'Q':
                {
                    current ??= [position];
                    var (p0, p1, p2) = (position, (v[0], v[1]), (v[2], v[3]));
                    var n = Steps(Distance(p0, p1) + Distance(p1, p2));
                    for (var i = 1; i <= n; i++)
                    {
                        var t = (double)i / n;
                        var u = 1 - t;
                        current.Add((u * u * p0.X + 2 * u * t * p1.Item1 + t * t * p2.Item1, u * u * p0.Y + 2 * u * t * p1.Item2 + t * t * p2.Item2));
                    }
                    position = p2;
                    break;
                }
                case 'C':
                {
                    current ??= [position];
                    var (p0, p1, p2, p3) = (position, (v[0], v[1]), (v[2], v[3]), (v[4], v[5]));
                    var n = Steps(Distance(p0, p1) + Distance(p1, p2) + Distance(p2, p3));
                    for (var i = 1; i <= n; i++)
                    {
                        var t = (double)i / n;
                        var u = 1 - t;
                        current.Add((
                            u * u * u * p0.X + 3 * u * u * t * p1.Item1 + 3 * u * t * t * p2.Item1 + t * t * t * p3.Item1,
                            u * u * u * p0.Y + 3 * u * u * t * p1.Item2 + 3 * u * t * t * p2.Item2 + t * t * t * p3.Item2));
                    }
                    position = p3;
                    break;
                }
                case 'A':
                    current ??= [position];
                    foreach (var p in Arc(position, v[0], v[1], v[2], v[3] != 0, v[4] != 0, (v[5], v[6])))
                        current.Add(p);
                    position = (v[5], v[6]);
                    break;
                case 'Z':
                    if (current is { Count: > 1 }) figures.Add((current, true));
                    current = null;
                    position = start;
                    break;
            }
        }
        if (current is { Count: > 1 }) figures.Add((current, false));
        return figures;
    }

    static int Steps(double length) => Math.Clamp((int)Math.Ceiling(length / 1.5), 4, 256);

    static double Distance((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>Samples an SVG elliptical arc (endpoint parameterization, rotation in degrees).</summary>
    static IEnumerable<(double X, double Y)> Arc((double X, double Y) from, double rx, double ry, double rotation, bool large, bool sweep, (double X, double Y) to)
    {
        rx = Math.Abs(rx);
        ry = Math.Abs(ry);
        if (rx < 1e-9 || ry < 1e-9 || from == to)
        {
            yield return to;
            yield break;
        }
        var phi = rotation * Math.PI / 180;
        var (sin, cos) = Math.SinCos(phi);
        var dx = (from.X - to.X) / 2;
        var dy = (from.Y - to.Y) / 2;
        var x1 = cos * dx + sin * dy;
        var y1 = -sin * dx + cos * dy;
        var lambda = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry);
        if (lambda > 1) { rx *= Math.Sqrt(lambda); ry *= Math.Sqrt(lambda); }
        var num = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1;
        var den = rx * rx * y1 * y1 + ry * ry * x1 * x1;
        var factor = (large == sweep ? -1 : 1) * Math.Sqrt(Math.Max(0, num / Math.Max(den, 1e-12)));
        var cx1 = factor * rx * y1 / ry;
        var cy1 = -factor * ry * x1 / rx;
        var cx = cos * cx1 - sin * cy1 + (from.X + to.X) / 2;
        var cy = sin * cx1 + cos * cy1 + (from.Y + to.Y) / 2;
        var theta = Math.Atan2((y1 - cy1) / ry, (x1 - cx1) / rx);
        var end = Math.Atan2((-y1 - cy1) / ry, (-x1 - cx1) / rx);
        var delta = end - theta;
        if (sweep && delta < 0) delta += 2 * Math.PI;
        if (!sweep && delta > 0) delta -= 2 * Math.PI;
        var n = Steps(Math.Abs(delta) * Math.Max(rx, ry));
        for (var i = 1; i <= n; i++)
        {
            var a = theta + delta * i / n;
            var (s, c) = Math.SinCos(a);
            yield return (cos * rx * c - sin * ry * s + cx, sin * rx * c + cos * ry * s + cy);
        }
    }

    /// <summary>Offsets a polyline by <paramref name="distance"/> to the left of its direction, with mitered corners.</summary>
    public static List<(double X, double Y)> Offset(List<(double X, double Y)> points, bool closed, double distance)
    {
        // Drop repeated points; normals of zero-length segments are undefined.
        var p = new List<(double X, double Y)>();
        foreach (var point in points)
        {
            if (p.Count == 0 || Distance(p[^1], point) > 1e-6) p.Add(point);
        }
        if (closed && p.Count > 2 && Distance(p[0], p[^1]) < 1e-6) p.RemoveAt(p.Count - 1);
        if (p.Count < 2 || Math.Abs(distance) < 1e-9)
            return p;

        (double X, double Y) Normal(int i)
        {
            var a = p[i];
            var b = p[(i + 1) % p.Count];
            var length = Distance(a, b);
            return ((b.Y - a.Y) / length, -(b.X - a.X) / length);
        }

        var result = new List<(double X, double Y)>(p.Count);
        var segments = closed ? p.Count : p.Count - 1;
        for (var i = 0; i < p.Count; i++)
        {
            (double X, double Y) n;
            if (!closed && i == 0) n = Normal(0);
            else if (!closed && i == p.Count - 1) n = Normal(segments - 1);
            else
            {
                var n1 = Normal((i - 1 + segments) % segments);
                var n2 = Normal(i % segments);
                var dot = n1.X * n2.X + n1.Y * n2.Y;
                var scale = 1 / Math.Max(0.25, 1 + dot);   // miter, limited for very sharp corners
                n = ((n1.X + n2.X) * scale, (n1.Y + n2.Y) * scale);
            }
            result.Add((p[i].X + n.X * distance, p[i].Y + n.Y * distance));
        }
        return result;
    }
}
