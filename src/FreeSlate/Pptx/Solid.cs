using System.Text;
using FreeSlate.Drawing;
using FreeSlate.Drawing.Geometry;
using FreeSlate.Html;

namespace FreeSlate.Pptx;

/// <summary>
/// The body of a 3D shape (catalog: effect.shape3d): the top bevel between the outline (behind) and the front face
/// (in the shape's plane), and the extrusion behind it. Faces are turned and projected by the camera, sorted back to
/// front and lit by <see cref="Lighting"/>. Outlines are kept in the shape's own pixels (local) and mapped into the
/// scene (relative to the box center, turned and flipped in the plane); output is in the pixels of the shape's
/// unturned box. The front face itself is drawn by the caller.
/// </summary>
sealed class Solid(Camera camera, double w, double h, double rotation, bool flipH, bool flipV)
{
    readonly Matrix plane = Matrix.Rotate(rotation).Then(Matrix.Scale(flipH ? -1 : 1, flipV ? -1 : 1)).Then(Matrix.Translate(-w / 2, -h / 2));
    readonly List<(double Depth, string Markup)> faces = [];
    readonly StringBuilder defs = new();
    static int gradients;

    (double X, double Y) Scene((double X, double Y) p) => (plane.A * p.X + plane.C * p.Y + plane.E, plane.B * p.X + plane.D * p.Y + plane.F);

    /// <summary>The closed outlines of the filled paths in the shape's pixels, simplified where they barely turn.</summary>
    public static List<List<(double X, double Y)>> Outlines(EvaluatedGeometry geometry)
    {
        var result = new List<List<(double X, double Y)>>();
        foreach (var path in geometry.Paths.Where(p => p.Fill != "none"))
        {
            foreach (var (points, _) in CompoundLines.Flatten(path.Segments))
            {
                var outline = points.ToList();
                if (outline.Count > 1 && Distance(outline[0], outline[^1]) < 0.01)
                    outline.RemoveAt(outline.Count - 1);
                outline = Simplify(outline);
                if (outline.Count >= 3)
                    result.Add(outline);
            }
        }
        return result;
    }

    /// <summary>Drops points where the outline turns by less than 4° and the segments stay short.</summary>
    static List<(double X, double Y)> Simplify(List<(double X, double Y)> points)
    {
        if (points.Count < 8)
            return points;
        var kept = new List<(double X, double Y)> { points[0] };
        for (var i = 1; i < points.Count - 1; i++)
        {
            var (a, b, c) = (kept[^1], points[i], points[i + 1]);
            var angle1 = Math.Atan2(b.Y - a.Y, b.X - a.X);
            var angle2 = Math.Atan2(c.Y - b.Y, c.X - b.X);
            var turn = Math.Abs(Math.IEEERemainder(angle2 - angle1, 2 * Math.PI));
            if (turn > 4 * Math.PI / 180 || Distance(a, c) > 24)
                kept.Add(b);
        }
        kept.Add(points[^1]);
        return kept;
    }

    /// <summary>
    /// The outline moved inwards by <paramref name="distance"/> pixels: every vertex along its corner's bisector so that
    /// both edges keep the distance (miters limited to four times the distance).
    /// </summary>
    public static List<(double X, double Y)> Inset(List<(double X, double Y)> outline, double distance)
    {
        if (distance <= 0)
            return outline;
        var sign = SignedArea(outline) >= 0 ? 1 : -1;
        var result = new List<(double X, double Y)>(outline.Count);
        for (var i = 0; i < outline.Count; i++)
        {
            var (prev, cur, next) = (outline[(i - 1 + outline.Count) % outline.Count], outline[i], outline[(i + 1) % outline.Count]);
            var n1 = Inward(prev, cur, sign);
            var n2 = Inward(cur, next, sign);
            var (mx, my) = (n1.X + n2.X, n1.Y + n2.Y);
            var dot = 1 + n1.X * n2.X + n1.Y * n2.Y;
            var factor = dot < 1e-6 ? 4 : Math.Min(4, 2 / dot);
            var (ox, oy) = dot < 1e-6 ? (n1.X * distance, n1.Y * distance) : (mx / 2 * factor * distance, my / 2 * factor * distance);
            result.Add((cur.X + ox, cur.Y + oy));
        }
        return result;
    }

    static (double X, double Y) Inward((double X, double Y) a, (double X, double Y) b, int sign)
    {
        var (dx, dy) = (b.X - a.X, b.Y - a.Y);
        var length = Math.Sqrt(dx * dx + dy * dy);
        // Clockwise outlines on screen (positive area with y down) have the inside on the right of travel.
        return length < 1e-9 ? (0, 0) : (-sign * dy / length, sign * dx / length);
    }

    static double SignedArea(List<(double X, double Y)> outline)
    {
        var area = 0.0;
        for (var i = 0; i < outline.Count; i++)
        {
            var (a, b) = (outline[i], outline[(i + 1) % outline.Count]);
            area += a.X * b.Y - b.X * a.Y;
        }
        return area / 2;
    }

    (double X, double Y) Screen((double X, double Y, double Z) p)
    {
        var q = camera.Flatten(camera.Turn(p));
        return (w / 2 + q.X, h / 2 + q.Y);
    }

    /// <summary>
    /// Adds a scene polygon with its normal, lit in <paramref name="color"/>, if it faces the eye. With a second pair of
    /// normals for its first two corners (a curved surface) the polygon is shaded smoothly between them.
    /// </summary>
    void Face(List<(double X, double Y, double Z)> polygon, (double X, double Y, double Z) normal, RgbColor color,
        ((double X, double Y, double Z) A, (double X, double Y, double Z) B)? smooth = null)
    {
        var center = (X: polygon.Average(p => p.X), Y: polygon.Average(p => p.Y), Z: polygon.Average(p => p.Z));
        var view = camera.View(center);
        if (normal.X * view.X + normal.Y * view.Y + normal.Z * view.Z <= 1e-9)
            return;
        var lit = Lighting.Apply(color, Lighting.Shade(normal, view)).ToHex();
        var fill = $"fill=\"{lit}\"";
        if (smooth is var (na, nb))
        {
            var (ca, cb) = (Lighting.Apply(color, Lighting.Shade(na, view)).ToHex(), Lighting.Apply(color, Lighting.Shade(nb, view)).ToHex());
            if (ca != cb)
            {
                var (pa, pb) = (Screen(polygon[0]), Screen(polygon[1]));
                var id = $"sg{++gradients}";
                defs.Append($"<linearGradient id=\"{id}\" gradientUnits=\"userSpaceOnUse\" x1=\"{Units.Css(pa.X)}\" y1=\"{Units.Css(pa.Y)}\" x2=\"{Units.Css(pb.X)}\" y2=\"{Units.Css(pb.Y)}\">" +
                            $"<stop offset=\"0\" stop-color=\"{ca}\"/><stop offset=\"1\" stop-color=\"{cb}\"/></linearGradient>");
                fill = $"fill=\"url(#{id})\"";
                lit = $"url(#{id})";
            }
        }
        var d = new StringBuilder();
        for (var i = 0; i < polygon.Count; i++)
        {
            var (x, y) = Screen(polygon[i]);
            d.Append(i == 0 ? 'M' : 'L').Append(Units.Css(x)).Append(' ').Append(Units.Css(y));
        }
        // A hairline in the face's color closes the seams between neighbouring faces.
        faces.Add((camera.Turn(center).Z, $"<path d=\"{d}Z\" {fill} stroke=\"{lit}\" stroke-width=\"0.5\" stroke-linejoin=\"round\"/>"));
    }

    /// <summary>The extruded sides between z = <paramref name="front"/> and z = <paramref name="back"/>, and the back face.</summary>
    public void Extrusion(List<List<(double X, double Y)>> outlines, double front, double back, RgbColor color)
    {
        if (front - back < 0.01)
            return;
        foreach (var local in outlines)
        {
            var outline = local.Select(Scene).ToList();
            var sign = SignedArea(outline) >= 0 ? 1 : -1;
            var count = outline.Count;
            var normals = Enumerable.Range(0, count).Select(i => Inward(outline[i], outline[(i + 1) % count], sign)).Select(n => (X: -n.X, Y: -n.Y)).ToList();
            // Corners where the outline turns by less than 25° belong to a curve: their normal is the mean of both edges.
            (double X, double Y, double Z) Corner(int i, (double X, double Y) own)
            {
                var (before, after) = (normals[(i - 1 + count) % count], normals[i]);
                if (before.X * after.X + before.Y * after.Y < Math.Cos(25 * Math.PI / 180))
                    return (own.X, own.Y, 0);
                var (x, y) = (before.X + after.X, before.Y + after.Y);
                var length = Math.Sqrt(x * x + y * y);
                return length < 1e-9 ? (own.X, own.Y, 0) : (x / length, y / length, 0);
            }
            for (var i = 0; i < count; i++)
            {
                var (a, b) = (outline[i], outline[(i + 1) % count]);
                if (normals[i] == (0, 0))
                    continue;
                var (nx, ny) = normals[i];
                var startNormal = Corner(i, normals[i]);
                var endNormal = Corner((i + 1) % count, normals[i]);
                ((double X, double Y, double Z) A, (double X, double Y, double Z) B)? smooth = null;
                if (startNormal != (nx, ny, 0) || endNormal != (nx, ny, 0))
                    smooth = (startNormal, endNormal);
                Face([(a.X, a.Y, front), (b.X, b.Y, front), (b.X, b.Y, back), (a.X, a.Y, back)], (nx, ny, 0), color, smooth);
            }
            Face(outline.Select(p => (p.X, p.Y, back)).ToList(), (0, 0, -1), color);
        }
    }

    /// <summary>
    /// The top bevel: <paramref name="width"/> pixels wide from the outline inwards, rising <paramref name="height"/>
    /// pixels from the outline (behind) to the front face (z = 0) along the preset's profile; strips of one color are
    /// merged along the outline.
    /// </summary>
    public void Bevel(List<List<(double X, double Y)>> outlines, double width, double height, double[] profile, RgbColor color)
    {
        if (width < 0.01 || height < 0.01)
            return;
        const int Strips = 20;
        foreach (var local in outlines)
        {
            var rings = Enumerable.Range(0, Strips + 1).Select(k => Inset(local, width * k / Strips).Select(Scene).ToList()).ToList();
            var sign = SignedArea(rings[0]) >= 0 ? 1 : -1;
            for (var k = 0; k < Strips; k++)
            {
                var (t0, t1) = ((double)k / Strips, (double)(k + 1) / Strips);
                var (z0, z1) = (-height * (1 - BevelProfiles.At(profile, t0)), -height * (1 - BevelProfiles.At(profile, t1)));
                var slope = (z1 - z0) / (width / Strips);
                var (outer, inner) = (rings[k], rings[k + 1]);
                // Neighbouring pieces of the strip with the same lit color become one polygon.
                List<(double X, double Y, double Z)>? run = null;
                List<(double X, double Y, double Z)>? back = null;
                (double X, double Y, double Z) runNormal = default;
                string? runColor = null;
                void Flush()
                {
                    if (run == null)
                        return;
                    back!.Reverse();
                    Face([.. run, .. back], runNormal, color);
                    run = null;
                }
                for (var i = 0; i < outer.Count; i++)
                {
                    var j = (i + 1) % outer.Count;
                    var inward = Inward(outer[i], outer[j], sign);
                    if (inward == (0, 0))
                        continue;
                    var normal = Normalize((-inward.X * slope, -inward.Y * slope, 1));
                    var hex = Lighting.Apply(color, Lighting.Shade(normal, camera.View((outer[i].X, outer[i].Y, z0)))).ToHex();
                    if (run != null && hex != runColor)
                        Flush();
                    if (run == null)
                    {
                        run = [(outer[i].X, outer[i].Y, z0)];
                        back = [(inner[i].X, inner[i].Y, z1)];
                        runNormal = normal;
                        runColor = hex;
                    }
                    run.Add((outer[j].X, outer[j].Y, z0));
                    back!.Add((inner[j].X, inner[j].Y, z1));
                }
                Flush();
            }
        }
    }

    /// <summary>The faces collected so far as SVG, farthest first.</summary>
    public string Markup() => (defs.Length > 0 ? $"<defs>{defs}</defs>" : "") + string.Concat(faces.OrderBy(f => f.Depth).Select(f => f.Markup));

    /// <summary>SVG path data of outlines (shape pixels).</summary>
    public static string PathData(IEnumerable<List<(double X, double Y)>> outlines)
    {
        var d = new StringBuilder();
        foreach (var outline in outlines)
        {
            for (var i = 0; i < outline.Count; i++)
                d.Append(i == 0 ? 'M' : 'L').Append(Units.Css(outline[i].X)).Append(' ').Append(Units.Css(outline[i].Y));
            d.Append('Z');
        }
        return d.ToString();
    }

    static (double X, double Y, double Z) Normalize((double X, double Y, double Z) v)
    {
        var length = Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        return (v.X / length, v.Y / length, v.Z / length);
    }

    static double Distance((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
