using System.Text;
using FreeSlate.Drawing;
using FreeSlate.Drawing.Geometry;
using FreeSlate.Html;

namespace FreeSlate.Pptx;

/// <summary>
/// The body of a 3D shape (catalog: effect.shape3d): the top bevel between the outline (behind) and the front face
/// (in the shape's plane), the extrusion behind it, the bottom bevel and the back face, and the contour around the
/// body's rims and edges. Faces are turned and projected by the camera, sorted back to front and lit by
/// <see cref="Lighting"/>. Outlines are kept in the shape's own pixels (local) and mapped into the scene (relative to
/// the box center, turned and flipped in the plane); output is in the pixels of the shape's unturned box. The front
/// face itself is drawn by the caller.
/// </summary>
sealed class Solid(Camera camera, Lighting lighting, double w, double h, double rotation, bool flipH, bool flipV)
{
    readonly Matrix plane = Matrix.Rotate(rotation).Then(Matrix.Scale(flipH ? -1 : 1, flipV ? -1 : 1)).Then(Matrix.Translate(-w / 2, -h / 2));
    readonly List<(double Depth, string Markup)> faces = [];
    readonly StringBuilder defs = new();
    readonly StringBuilder contour = new();
    static int gradients;

    (double X, double Y) Scene((double X, double Y) p) => (plane.A * p.X + plane.C * p.Y + plane.E, plane.B * p.X + plane.D * p.Y + plane.F);

    /// <summary>
    /// The closed outlines of the filled paths in the shape's pixels, simplified where they barely turn and grown by
    /// <paramref name="grow"/> pixels: a shape's line is part of its body, which reaches to the line's outer edge.
    /// </summary>
    public static List<List<(double X, double Y)>> Outlines(EvaluatedGeometry geometry, double grow = 0)
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
                    result.Add(Inset(outline, -grow));
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
    /// The outline moved inwards by <paramref name="distance"/> pixels (outwards when negative): every vertex along its
    /// corner's bisector so that both edges keep the distance (miters limited to four times the distance).
    /// </summary>
    public static List<(double X, double Y)> Inset(List<(double X, double Y)> outline, double distance)
    {
        if (Math.Abs(distance) < 1e-9)
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
        var q = camera.Flatten(camera.Place(p));
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
        var lit = Lighting.Apply(color, lighting.Shade(normal, view)).ToHex();
        var fill = $"fill=\"{lit}\"";
        if (smooth is var (na, nb))
        {
            var (ca, cb) = (Lighting.Apply(color, lighting.Shade(na, view)).ToHex(), Lighting.Apply(color, lighting.Shade(nb, view)).ToHex());
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
        faces.Add((camera.Place(center).Z, $"<path d=\"{d}Z\" {fill} stroke=\"{lit}\" stroke-width=\"0.5\" stroke-linejoin=\"round\"/>"));
    }

    /// <summary>The extruded sides between z = <paramref name="front"/> and z = <paramref name="back"/>.</summary>
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
        }
    }

    /// <summary>
    /// The back face at z = <paramref name="z"/>: the fill's color, with the line's color on the outer band the line
    /// covers (the back shows the front's fill and line, measured).
    /// </summary>
    public void Back(List<List<(double X, double Y)>> outlines, double z, RgbColor color, RgbColor? line, double lineWidth)
    {
        if (line is { } lineColor && lineWidth > 0.01)
        {
            Area(outlines, z, lineColor, 0);
            Area(outlines.Select(o => Inset(o, lineWidth)).ToList(), z, color, 1e-6);
        }
        else
        {
            Area(outlines, z, color, 0);
        }
    }

    /// <summary>A flat face looking backwards: all outlines at z, filled even-odd.</summary>
    void Area(List<List<(double X, double Y)>> outlines, double z, RgbColor color, double bias)
    {
        var points = outlines.SelectMany(o => o).Select(Scene).ToList();
        if (points.Count < 3)
            return;
        var center = (X: points.Average(p => p.X), Y: points.Average(p => p.Y), Z: z);
        var view = camera.View(center);
        if (-view.Z <= 1e-9)
            return;
        var lit = Lighting.Apply(color, lighting.Shade((0, 0, -1), view)).ToHex();
        var d = new StringBuilder();
        foreach (var outline in outlines)
        {
            for (var i = 0; i < outline.Count; i++)
            {
                var p = Scene(outline[i]);
                var (x, y) = Screen((p.X, p.Y, z));
                d.Append(i == 0 ? 'M' : 'L').Append(Units.Css(x)).Append(' ').Append(Units.Css(y));
            }
            d.Append('Z');
        }
        faces.Add((camera.Place(center).Z + bias, $"<path d=\"{d}\" fill=\"{lit}\" fill-rule=\"evenodd\" stroke=\"{lit}\" stroke-width=\"0.5\" stroke-linejoin=\"round\"/>"));
    }

    /// <summary>
    /// A bevel <paramref name="width"/> pixels wide from the outline inwards along the preset's profile. The top bevel
    /// rises <paramref name="height"/> pixels from the outline (behind) to the front face at z = <paramref name="z"/>;
    /// the bottom bevel falls as far from the outline at z to the back face. The line, part of the body, colors the
    /// outer band of its width (measured). Strips of one color are merged along the outline.
    /// </summary>
    /// <remarks>
    /// Without a color (pictures, gradients) the bevel is drawn as shading over the front face, which then must not be
    /// clipped: a multiplied gray for the diffuse light and an added gray for the specular (and for diffuse light above
    /// one), see <see cref="OverlayMarkup"/>.
    /// </remarks>
    public void Bevel(List<List<(double X, double Y)>> outlines, double width, double height, double[] profile, RgbColor? color,
        double z = 0, bool bottom = false, RgbColor? line = null, double lineWidth = 0)
    {
        if (width < 0.01 || height < 0.01)
            return;
        const int Strips = 20;
        // The strips, split where the line's band ends.
        var ts = Enumerable.Range(0, Strips + 1).Select(k => (double)k / Strips).ToList();
        var lineEnd = line != null && color != null ? lineWidth / width : 0;
        if (lineEnd > 1e-4 && lineEnd < 1 - 1e-4 && ts.All(t => Math.Abs(t - lineEnd) > 1e-4))
        {
            ts.Add(lineEnd);
            ts.Sort();
        }
        double Depth(double t) => bottom ? z - height * BevelProfiles.At(profile, t) : z - height * (1 - BevelProfiles.At(profile, t));
        foreach (var local in outlines)
        {
            var rings = ts.Select(t => Inset(local, width * t).Select(Scene).ToList()).ToList();
            var sign = SignedArea(rings[0]) >= 0 ? 1 : -1;
            for (var k = 0; k + 1 < ts.Count; k++)
            {
                var (t0, t1) = (ts[k], ts[k + 1]);
                var (z0, z1) = (Depth(t0), Depth(t1));
                var slope = (z1 - z0) / (width * (t1 - t0));
                var stripColor = t1 <= lineEnd + 1e-9 ? line : color;
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
                    if (stripColor is { } solid)
                        Face([.. run, .. back], runNormal, solid);
                    else
                        Overlay([.. run, .. back], runNormal);
                    run = null;
                }
                for (var i = 0; i < outer.Count; i++)
                {
                    var j = (i + 1) % outer.Count;
                    var inward = Inward(outer[i], outer[j], sign);
                    if (inward == (0, 0))
                        continue;
                    // Outwards and towards the front (top) or the back (bottom).
                    var normal = bottom ? Normalize((inward.X * slope, inward.Y * slope, -1)) : Normalize((-inward.X * slope, -inward.Y * slope, 1));
                    var shade = lighting.Shade(normal, camera.View((outer[i].X, outer[i].Y, z0)));
                    var hex = stripColor is { } c ? Lighting.Apply(c, shade).ToHex() : $"{shade.Diffuse.R:F2}/{shade.Specular.R:F2}";
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

    /// <summary>How steeply a bevel leaves the outline: its rise per pixel inwards over the first strip.</summary>
    public static double Rise(double width, double height, double[] profile) =>
        width < 0.01 || height < 0.01 ? 0 : height * (BevelProfiles.At(profile, 0.05) - BevelProfiles.At(profile, 0)) / (width * 0.05);

    /// <summary>
    /// The contour (a:sp3d/@contourW): a tube of the given width around the body's rims and its edges along the
    /// extrusion (creases and, on curved sides, the silhouette), drawn unlit over everything else. The tube shows
    /// half its width outside an edge; inside, a face coming towards the eye from the edge at an angle α to the
    /// screen covers all but r·cos α of it (measured: a circle bevel leaves only the outer half, a face seen from
    /// the side a sliver). The rims lie at z = <paramref name="front"/> and <paramref name="back"/>; the faces beside
    /// them leave the front rim rising <paramref name="frontRise"/> per pixel inwards (a top bevel) and the back
    /// rim falling <paramref name="backRise"/>.
    /// </summary>
    public void Contour(List<List<(double X, double Y)>> outlines, double front, double back, double width, double frontRise, double backRise)
    {
        var r = width / 2;
        if (r < 0.01)
            return;
        var deep = front - back > 0.01;
        foreach (var local in outlines)
        {
            var outline = local.Select(Scene).ToList();
            var count = outline.Count;
            var sign = SignedArea(outline) >= 0 ? 1 : -1;
            var inward = Enumerable.Range(0, count).Select(i => Inward(outline[i], outline[(i + 1) % count], sign)).ToList();
            (double X, double Y, double Z) At(int i, double z) => (outline[i].X, outline[i].Y, z);
            (double X, double Y, double Z) Middle(int i, double z) => ((outline[i].X + outline[(i + 1) % count].X) / 2, (outline[i].Y + outline[(i + 1) % count].Y) / 2, z);
            bool Faces((double X, double Y, double Z) normal, (double X, double Y, double Z) at)
            {
                var v = camera.View(at);
                return normal.X * v.X + normal.Y * v.Y + normal.Z * v.Z > 1e-9;
            }
            var walls = Enumerable.Range(0, count).Select(i => deep && inward[i] != (0, 0) && Faces((-inward[i].X, -inward[i].Y, 0), Middle(i, (front + back) / 2))).ToList();
            (double X, double Y, double Z) Surface(int i, double rise) => Normalize((inward[i].X, inward[i].Y, rise));
            (double X, double Y, double Z) SurfaceNormal(int i, double rise) => Normalize((-inward[i].X * rise, -inward[i].Y * rise, 1));

            // The front rim: between the front (face or top bevel) and the sides, or the back when nothing is extruded.
            var frontSides = new List<(double X, double Y, double Z)[]>();
            var frontShown = new List<bool>();
            for (var i = 0; i < count; i++)
            {
                var face = Surface(i, frontRise);
                var other = deep ? (0, 0, -1) : Surface(i, -backRise);
                frontSides.Add([face, other]);
                // A flat shape shows its face from both sides.
                var flat = !deep && frontRise == 0 && backRise == 0;
                var backNormal = Normalize((-inward[i].X * backRise, -inward[i].Y * backRise, -1));
                frontShown.Add(inward[i] != (0, 0) && (flat || walls[i] || Faces(SurfaceNormal(i, frontRise), Middle(i, front)) || !deep && Faces(backNormal, Middle(i, front))));
            }
            Rim(Enumerable.Range(0, count).Select(i => At(i, front)).ToList(), frontSides, frontShown, r);
            if (!deep)
                continue;

            // The back rim: between the sides and the back (face or bottom bevel).
            var backSides = new List<(double X, double Y, double Z)[]>();
            var backShown = new List<bool>();
            for (var i = 0; i < count; i++)
            {
                backSides.Add([(0, 0, 1), Surface(i, -backRise)]);
                var backNormal = Normalize((-inward[i].X * backRise, -inward[i].Y * backRise, -1));
                backShown.Add(inward[i] != (0, 0) && (walls[i] || Faces(backNormal, Middle(i, back))));
            }
            Rim(Enumerable.Range(0, count).Select(i => At(i, back)).ToList(), backSides, backShown, r);

            // Along the extrusion: creases where the outline turns by 25° or more (drawn when a side beside them shows),
            // the silhouette where a curved side turns away.
            for (var i = 0; i < count; i++)
            {
                var previous = (i - 1 + count) % count;
                if (inward[i] == (0, 0) || inward[previous] == (0, 0))
                    continue;
                var turn = inward[previous].X * inward[i].X + inward[previous].Y * inward[i].Y;
                var crease = turn < Math.Cos(25 * Math.PI / 180);
                if (crease ? !(walls[previous] || walls[i]) : walls[previous] == walls[i])
                    continue;
                var p = outline[i];
                var (before, after) = (outline[previous], outline[(i + 1) % count]);
                (double X, double Y, double Z) Along((double X, double Y) q) => Normalize((q.X - p.X, q.Y - p.Y, 0));
                Tube([(p.X, p.Y, front), (p.X, p.Y, back)], [[Along(before), Along(after)]], closed: false, r);
            }
        }
    }

    /// <summary>
    /// The legacyWireframe material: every edge of the body's mesh as a hairline, hidden ones included (the rims and a
    /// line along the extrusion at every corner of the outline, measured).
    /// </summary>
    public void Wireframe(List<List<(double X, double Y)>> outlines, double front, double back)
    {
        var deep = front - back > 0.01;
        foreach (var local in outlines)
        {
            var outline = local.Select(Scene).ToList();
            foreach (var z in deep ? new[] { front, back } : [front])
            {
                for (var i = 0; i < outline.Count; i++)
                {
                    var (x, y) = Screen((outline[i].X, outline[i].Y, z));
                    wires.Append(i == 0 ? 'M' : 'L').Append(Units.Css(x)).Append(' ').Append(Units.Css(y));
                }
                wires.Append('Z');
            }
            if (!deep)
                continue;
            foreach (var p in outline)
            {
                var (a, b) = (Screen((p.X, p.Y, front)), Screen((p.X, p.Y, back)));
                wires.Append('M').Append(Units.Css(a.X)).Append(' ').Append(Units.Css(a.Y)).Append('L').Append(Units.Css(b.X)).Append(' ').Append(Units.Css(b.Y));
            }
        }
    }

    readonly StringBuilder wires = new();

    /// <summary>The tube along a rim: the runs of segments that show, closed when all of them do.</summary>
    void Rim(List<(double X, double Y, double Z)> points, List<(double X, double Y, double Z)[]> sides, List<bool> shown, double r)
    {
        var count = points.Count;
        if (shown.All(s => s))
        {
            Tube(points, sides, closed: true, r);
            return;
        }
        var first = shown.IndexOf(false);
        var run = new List<int>();
        for (var step = 1; step <= count; step++)
        {
            var i = (first + step) % count;
            if (shown[i])
            {
                run.Add(i);
                continue;
            }
            if (run.Count > 0)
                Tube([.. run.Select(k => points[k]), points[(run[^1] + 1) % count]], run.Select(k => sides[k]).ToList(), closed: false, r);
            run.Clear();
        }
    }

    /// <summary>
    /// The visible band of a tube of radius <paramref name="r"/> along a polyline of the scene, with the faces beside
    /// each segment (directions from the edge into them): mitered at the corners, square at open ends.
    /// </summary>
    void Tube(List<(double X, double Y, double Z)> points, List<(double X, double Y, double Z)[]> sides, bool closed, double r)
    {
        var segments = closed ? points.Count : points.Count - 1;
        var screen = points.Select(Screen).ToList();
        var radius = points.Select(p => r * camera.Scale(camera.Place(p))).ToList();
        var normals = new (double X, double Y)[segments];
        var widths = new (double Plus, double Minus)[segments];
        for (var k = 0; k < segments; k++)
        {
            var (a, b) = (screen[k], screen[(k + 1) % points.Count]);
            var length = Distance(a, b);
            if (length < 1e-6)
                continue;
            var n = (X: -(b.Y - a.Y) / length, Y: (b.X - a.X) / length);
            normals[k] = n;
            var (pa, pb) = (points[k], points[(k + 1) % points.Count]);
            var middle = ((pa.X + pb.X) / 2, (pa.Y + pb.Y) / 2, (pa.Z + pb.Z) / 2);
            var view = Normalize(camera.View(middle));
            var m = Screen(middle);
            var scale = camera.Scale(camera.Place(middle));
            var (plus, minus) = (1.0, 1.0);
            foreach (var u in sides[k])
            {
                if (u.X * view.X + u.Y * view.Y + u.Z * view.Z <= 1e-9)
                    continue;
                // The face comes towards the eye: it covers the tube beyond r times its share across the edge.
                var q = Screen((middle.Item1 + u.X, middle.Item2 + u.Y, middle.Item3 + u.Z));
                var across = ((q.X - m.X) * n.X + (q.Y - m.Y) * n.Y) / scale;
                if (across >= 0)
                    plus = Math.Min(plus, across);
                else
                    minus = Math.Min(minus, -across);
            }
            widths[k] = (plus, minus);
        }
        var d = new StringBuilder();
        void Polygon(params (double X, double Y)[] corners)
        {
            // One orientation for all pieces, so that their union fills with the nonzero rule.
            var area = 0.0;
            for (var i = 0; i < corners.Length; i++)
                area += corners[i].X * corners[(i + 1) % corners.Length].Y - corners[(i + 1) % corners.Length].X * corners[i].Y;
            if (Math.Abs(area) < 1e-9)
                return;
            if (area < 0)
                Array.Reverse(corners);
            for (var i = 0; i < corners.Length; i++)
                d.Append(i == 0 ? 'M' : 'L').Append(Units.Css(corners[i].X)).Append(' ').Append(Units.Css(corners[i].Y));
            d.Append('Z');
        }
        (double X, double Y) Offset((double X, double Y) p, (double X, double Y) n, double distance) => (p.X + n.X * distance, p.Y + n.Y * distance);
        for (var k = 0; k < segments; k++)
        {
            var n = normals[k];
            if (n == (0, 0))
                continue;
            var (ia, ib) = (k, (k + 1) % points.Count);
            var (a, b) = (screen[ia], screen[ib]);
            var (plus, minus) = widths[k];
            Polygon(Offset(a, n, radius[ia] * plus), Offset(b, n, radius[ib] * plus), Offset(b, n, -radius[ib] * minus), Offset(a, n, -radius[ia] * minus));
            // Square ends: the tube reaches r beyond an open end.
            var direction = (X: n.Y, Y: -n.X);
            if (!closed && k == 0)
            {
                var back = Offset(a, direction, -radius[ia]);
                Polygon(Offset(a, n, radius[ia] * plus), Offset(a, n, -radius[ia] * minus), Offset(back, n, -radius[ia] * minus), Offset(back, n, radius[ia] * plus));
            }
            if (!closed && k == segments - 1)
            {
                var ahead = Offset(b, direction, radius[ib]);
                Polygon(Offset(b, n, radius[ib] * plus), Offset(ahead, n, radius[ib] * plus), Offset(ahead, n, -radius[ib] * minus), Offset(b, n, -radius[ib] * minus));
            }
            // Corners: the gap between this segment and the next, mitered up to twice the radius, else beveled.
            if (closed || k < segments - 1)
            {
                var next = (k + 1) % segments;
                var m = normals[next];
                if (m == (0, 0))
                    continue;
                foreach (var side in new[] { 1, -1 })
                {
                    var (own, other) = side > 0 ? (plus, widths[next].Plus) : (minus, widths[next].Minus);
                    var reach = radius[ib] * Math.Min(own, other);
                    var p1 = Offset(b, n, side * radius[ib] * own);
                    var p2 = Offset(b, m, side * radius[ib] * other);
                    var bisector = (X: n.X + m.X, Y: n.Y + m.Y);
                    var length = Math.Sqrt(bisector.X * bisector.X + bisector.Y * bisector.Y);
                    var cos = length / 2;
                    if (length > 1e-9 && cos > 0.5)
                        Polygon(b, p1, Offset(b, (bisector.X / length, bisector.Y / length), side * reach / cos), p2);
                    else
                        Polygon(b, p1, p2);
                }
            }
        }
        contour.Append(d);
    }

    readonly List<(double Depth, string Markup)> overlays = [];

    /// <summary>Shading of a scene polygon over a textured front face (multiplied diffuse, added specular).</summary>
    void Overlay(List<(double X, double Y, double Z)> polygon, (double X, double Y, double Z) normal)
    {
        var center = (X: polygon.Average(p => p.X), Y: polygon.Average(p => p.Y), Z: polygon.Average(p => p.Z));
        var view = camera.View(center);
        if (normal.X * view.X + normal.Y * view.Y + normal.Z * view.Z <= 1e-9)
            return;
        var shade = lighting.Shade(normal, view);
        // Textures are shaded in gray: the mean of the channels.
        var diffuse = (shade.Diffuse.R + shade.Diffuse.G + shade.Diffuse.B) / 3;
        var specular = (shade.Specular.R + shade.Specular.G + shade.Specular.B) / 3;
        var d = new StringBuilder();
        for (var i = 0; i < polygon.Count; i++)
        {
            var (x, y) = Screen(polygon[i]);
            d.Append(i == 0 ? 'M' : 'L').Append(Units.Css(x)).Append(' ').Append(Units.Css(y));
        }
        var markup = new StringBuilder();
        var multiply = new RgbColor(Math.Min(1, diffuse), Math.Min(1, diffuse), Math.Min(1, diffuse)).ToHex();
        if (diffuse < 0.999)
            markup.Append($"<path d=\"{d}Z\" fill=\"{multiply}\" style=\"mix-blend-mode:multiply\"/>");
        var add = Math.Clamp(specular + Math.Max(0, diffuse - 1) * 0.6, 0, 1);
        if (add > 0.004)
            markup.Append($"<path d=\"{d}Z\" fill=\"{new RgbColor(add, add, add).ToHex()}\" style=\"mix-blend-mode:plus-lighter\"/>");
        if (markup.Length > 0)
            overlays.Add((camera.Place(center).Z, markup.ToString()));
    }

    /// <summary>The shading drawn over a textured front face, farthest first, and the contour over everything.</summary>
    public string OverlayMarkup(RgbColor? contourColor = null) =>
        string.Concat(overlays.OrderBy(f => f.Depth).Select(f => f.Markup)) +
        (contour.Length > 0 && contourColor is { } color ? $"<path d=\"{contour}\" fill=\"{color.ToHex()}\"{(color.A < 1 ? $" fill-opacity=\"{Units.Css4(color.A)}\"" : "")}/>" : "") +
        (wires.Length > 0 ? $"<path d=\"{wires}\" fill=\"none\" stroke=\"#000\" stroke-width=\"0.75\" stroke-linejoin=\"round\"/>" : "");

    /// <summary>The faces collected so far as SVG, farthest first.</summary>
    public string Markup() => (defs.Length > 0 ? $"<defs>{defs}</defs>" : "") + string.Concat(faces.OrderBy(f => f.Depth).Select(f => f.Markup));

    /// <summary>The outlines at z = <paramref name="z"/> on the screen as path data, with the box they cover.</summary>
    public (string Data, (double X, double Y, double W, double H) Bounds) Projected(List<List<(double X, double Y)>> outlines, double z)
    {
        var d = new StringBuilder();
        var (minX, minY, maxX, maxY) = (double.MaxValue, double.MaxValue, double.MinValue, double.MinValue);
        foreach (var local in outlines)
        {
            for (var i = 0; i < local.Count; i++)
            {
                var p = Scene(local[i]);
                var (x, y) = Screen((p.X, p.Y, z));
                (minX, minY, maxX, maxY) = (Math.Min(minX, x), Math.Min(minY, y), Math.Max(maxX, x), Math.Max(maxY, y));
                d.Append(i == 0 ? 'M' : 'L').Append(Units.Css(x)).Append(' ').Append(Units.Css(y));
            }
            d.Append('Z');
        }
        return d.Length == 0 ? ("", (0, 0, 0, 0)) : (d.ToString(), (minX, minY, maxX - minX, maxY - minY));
    }

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
