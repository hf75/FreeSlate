using System.Text;
using FreeSlate.Drawing;
using FreeSlate.Drawing.Geometry;
using FreeSlate.Html;

namespace FreeSlate.Pptx;

/// <summary>
/// The extruded body of a 3D shape (catalog: effect.shape3d): the outline is swept back by the extrusion depth; its
/// sides and, seen from behind, its back face are turned and projected by the camera, sorted back to front and lit
/// by <see cref="Lighting"/>. Coordinates are the pixels of the shape's unturned box; the front face is drawn by the
/// caller.
/// </summary>
static class Solid
{
    /// <summary>A 2D point of the shape's box in the scene (relative to the box center, turned and flipped in its plane).</summary>
    static Func<(double X, double Y), (double X, double Y)> Plane(double w, double h, double rotation, bool flipH, bool flipV)
    {
        var plane = Matrix.Rotate(rotation).Then(Matrix.Scale(flipH ? -1 : 1, flipV ? -1 : 1)).Then(Matrix.Translate(-w / 2, -h / 2));
        return p => (plane.A * p.X + plane.C * p.Y + plane.E, plane.B * p.X + plane.D * p.Y + plane.F);
    }

    /// <summary>The closed outlines of the filled paths, in scene coordinates.</summary>
    public static List<List<(double X, double Y)>> Outlines(EvaluatedGeometry geometry, double w, double h, double rotation, bool flipH, bool flipV)
    {
        var map = Plane(w, h, rotation, flipH, flipV);
        var result = new List<List<(double X, double Y)>>();
        foreach (var path in geometry.Paths.Where(p => p.Fill != "none"))
        {
            foreach (var (points, _) in CompoundLines.Flatten(path.Segments))
            {
                var mapped = points.Select(map).ToList();
                if (mapped.Count > 1 && Distance(mapped[0], mapped[^1]) < 0.01)
                    mapped.RemoveAt(mapped.Count - 1);
                if (mapped.Count >= 3)
                    result.Add(mapped);
            }
        }
        return result;
    }

    /// <summary>
    /// The sides and the back face of the body between z = <paramref name="front"/> and z = <paramref name="back"/>
    /// (pixels, z towards the eye) as SVG polygons in the box's pixels, farthest first.
    /// </summary>
    public static string Body(Camera camera, List<List<(double X, double Y)>> outlines, double w, double h, double front, double back, RgbColor color)
    {
        if (front - back < 0.01 || outlines.Count == 0)
            return "";
        var faces = new List<(double Depth, string Markup)>();
        (double X, double Y) Screen((double X, double Y, double Z) p)
        {
            var q = camera.Flatten(camera.Turn(p));
            return (w / 2 + q.X, h / 2 + q.Y);
        }
        bool Visible((double X, double Y, double Z) normal, (double X, double Y, double Z) point)
        {
            var view = camera.View(point);
            return normal.X * view.X + normal.Y * view.Y + normal.Z * view.Z > 1e-9;
        }
        void Add(List<(double X, double Y, double Z)> polygon, (double X, double Y, double Z) normal)
        {
            var center = (X: polygon.Average(p => p.X), Y: polygon.Average(p => p.Y), Z: polygon.Average(p => p.Z));
            if (!Visible(normal, center))
                return;
            var lit = Lighting.Apply(color, Lighting.Shade(normal, camera.View(center)));
            var d = new StringBuilder();
            for (var i = 0; i < polygon.Count; i++)
            {
                var (x, y) = Screen(polygon[i]);
                d.Append(i == 0 ? 'M' : 'L').Append(Units.Css(x)).Append(' ').Append(Units.Css(y));
            }
            // A hairline in the face's color closes the seams between neighbouring faces.
            var hex = lit.ToHex();
            faces.Add((camera.Turn(center).Z, $"<path d=\"{d}Z\" fill=\"{hex}\" stroke=\"{hex}\" stroke-width=\"0.5\" stroke-linejoin=\"round\"/>"));
        }

        foreach (var outline in outlines)
        {
            var area = 0.0;
            for (var i = 0; i < outline.Count; i++)
            {
                var (a, b) = (outline[i], outline[(i + 1) % outline.Count]);
                area += a.X * b.Y - b.X * a.Y;
            }
            var sign = area >= 0 ? 1 : -1;
            for (var i = 0; i < outline.Count; i++)
            {
                var (a, b) = (outline[i], outline[(i + 1) % outline.Count]);
                var (dx, dy) = (b.X - a.X, b.Y - a.Y);
                var length = Math.Sqrt(dx * dx + dy * dy);
                if (length < 1e-6)
                    continue;
                // Outward normal: clockwise outlines on screen (positive area with y down) have it on the left of travel.
                var normal = (sign * dy / length, -sign * dx / length, 0.0);
                Add([(a.X, a.Y, front), (b.X, b.Y, front), (b.X, b.Y, back), (a.X, a.Y, back)], normal);
            }
            // The back face, when the body is seen from behind.
            Add(outline.Select(p => (p.X, p.Y, back)).ToList(), (0, 0, -1));
        }
        return string.Concat(faces.OrderBy(f => f.Depth).Select(f => f.Markup));
    }

    static double Distance((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
