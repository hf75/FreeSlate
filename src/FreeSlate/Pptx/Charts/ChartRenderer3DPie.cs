using System.Text;
using FreeSlate.Drawing;

namespace FreeSlate.Pptx.Charts;

/// <summary>
/// 3D charts without right-angle axes (catalog: chart.threeD): a perspective camera and the 3D pie. Measured on
/// probes of boxes and pies at many rotations and fields of view: the camera looks at the plot box's centre with a
/// focal length that makes the field of view (perspective / 2 degrees) span the plot box's height; the scene is
/// turned by rotY about the vertical and then by rotX about the horizontal; the drawn geometry, enlarged by 2.5 %,
/// is moved and scaled so that its projection is centred in the plot box and fills it.
/// </summary>
public sealed partial class ChartRenderer
{
    /// <summary>The perspective camera of a 3D chart: model points (y up, z away from the viewer) to points on the chart.</summary>
    sealed class Perspective
    {
        readonly double cosY, sinY, cosX, sinX;
        double focal, centerX, centerY, scale = 1, offsetX, offsetY;

        public Perspective(double rotX, double rotY)
        {
            (sinY, cosY) = Math.SinCos(rotY * Math.PI / 180);
            (sinX, cosX) = Math.SinCos(rotX * Math.PI / 180);
        }

        /// <summary>A model point turned into view space (yaw, then pitch); x right, y up, z away from the viewer.</summary>
        public (double X, double Y, double Z) View(double x, double y, double z)
        {
            var (vx, vz) = (x * cosY + z * sinY, -x * sinY + z * cosY);
            return (vx, y * cosX + vz * sinX, -y * sinX + vz * cosX);
        }

        /// <summary>A view-space point back in (centred) model space.</summary>
        public (double X, double Y, double Z) ToModel((double X, double Y, double Z) v)
        {
            var (y, vz) = (v.Y * cosX - v.Z * sinX, v.Y * sinX + v.Z * cosX);
            return (v.X * cosY - vz * sinY, y, v.X * sinY + vz * cosY);
        }

        /// <summary>A view-space direction (a normal) of a model direction.</summary>
        public (double X, double Y, double Z) Direction(double x, double y, double z) => View(x, y, z);

        public (double X, double Y) Project((double X, double Y, double Z) v)
        {
            var depth = Math.Max(1e-6, scale * v.Z + 1);
            return (centerX + focal * (scale * v.X + offsetX) / depth, centerY - focal * (scale * v.Y + offsetY) / depth);
        }

        public (double X, double Y) Map(double x, double y, double z) => Project(View(x, y, z));

        /// <summary>The camera position in view space: the point every viewing ray starts from.</summary>
        public (double X, double Y, double Z) Eye => (-offsetX / scale, -offsetY / scale, -1 / scale);

        /// <summary>
        /// Places the camera: the view-space points <paramref name="volume"/> (already enlarged) are projected centred in
        /// the plot box and as large as it allows; <paramref name="fieldOfView"/> in degrees (at least 0.1).
        /// </summary>
        public void Fit(IReadOnlyList<(double X, double Y, double Z)> volume, Box plot, double fieldOfView)
        {
            centerX = plot.X + plot.W / 2;
            centerY = plot.Y + plot.H / 2;
            focal = plot.H / 2 / Math.Tan(Math.Max(0.1, fieldOfView) * Math.PI / 360);
            var nearest = volume.Min(v => v.Z);
            var limit = nearest < 0 ? 0.98 / -nearest : 1e6;
            (double W, double H, double Cx, double Cy) Extent()
            {
                double x0 = double.MaxValue, x1 = double.MinValue, y0 = double.MaxValue, y1 = double.MinValue;
                foreach (var v in volume)
                {
                    var (px, py) = Project(v);
                    (x0, x1, y0, y1) = (Math.Min(x0, px), Math.Max(x1, px), Math.Min(y0, py), Math.Max(y1, py));
                }
                return (x1 - x0, y1 - y0, (x0 + x1) / 2, (y0 + y1) / 2);
            }
            void Centre()
            {
                // The projection's centre moves almost linearly with the offsets: a few corrections centre it.
                for (var i = 0; i < 40; i++)
                {
                    var (_, _, cx, cy) = Extent();
                    if (Math.Abs(cx - centerX) < 1e-4 && Math.Abs(cy - centerY) < 1e-4)
                        break;
                    offsetX -= (cx - centerX) / focal;
                    offsetY += (cy - centerY) / focal;
                }
            }
            double Fill()
            {
                Centre();
                var (w, h, _, _) = Extent();
                return Math.Max(w / plot.W, h / plot.H);
            }
            // The fill grows with the scale (the camera comes closer): bisect for a projection filling the box.
            double lo = 0, hi = Math.Min(limit, 1);
            scale = hi;
            while (Fill() < 1 && hi < limit)
            {
                lo = hi;
                hi = Math.Min(limit, hi * 2);
                scale = hi;
            }
            for (var i = 0; i < 60; i++)
            {
                scale = (lo + hi) / 2;
                (offsetX, offsetY) = (0, 0);
                if (Fill() > 1) hi = scale; else lo = scale;
            }
            scale = lo > 0 ? lo : hi;
            (offsetX, offsetY) = (0, 0);
            Fill();
        }
    }

    // The light of perspective charts (measured on pies): 0.4 ambient plus a diffuse light from the upper front at
    // 45° and slightly from the left, and a small white highlight on surfaces facing halfway between it and the eye.
    const double Ambient = 0.4, LightX = -0.06, LightY = 0.43, LightZ = -0.43;

    static double Diffuse((double X, double Y, double Z) normal, double lightX = LightX) =>
        Ambient + Math.Max(0, normal.X * lightX + normal.Y * LightY + normal.Z * LightZ);

    // The cut faces of exploded slices see the light from the right (measured: 0.68 facing right, 0.4 facing left).
    const double CutLightX = 0.232;

    static double Highlight((double X, double Y, double Z) normal)
    {
        // The half vector lies 22.5° above the viewing direction: highlights peak at about 67.5° of tilt.
        var dot = normal.Y * Math.Sin(Math.PI / 8) - normal.Z * Math.Cos(Math.PI / 8);
        return dot <= 0 ? 0 : 0.036 * Math.Pow(dot, 34);
    }

    static RgbColor Lit(RgbColor color, double shade, double highlight = 0) =>
        new(Math.Min(1, color.R * shade + highlight), Math.Min(1, color.G * shade + highlight), Math.Min(1, color.B * shade + highlight), color.A);

    /// <summary>
    /// A 3D pie: a disk 0.24 of its radius thick (times hPercent), its slices clockwise from the back starting at
    /// rotY, tilted by rotX. Tops are lit by the chart light, sides get a gradient across the pie (the diffuse light of
    /// their front, falling by 0.06 from left to right), exploded slices move out by their explosion and show their
    /// cut faces. Slices keep their outline (white by default).
    /// </summary>
    string Pie3D(ChartGroup group, Box outer, View3D view)
    {
        var series = group.Series.FirstOrDefault();
        if (series == null)
            return "";
        var values = series.Values.Select(v => Math.Abs(v ?? 0)).ToList();
        var total = values.Sum();
        if (total <= 0)
            return "";
        var thickness = 0.24 * (view.HeightPercent ?? 100) / 100;
        var seriesExplosion = (ChartModel.Number(series.Element, "explosion") ?? 0) / 100;
        double Explosion(int i) => series.Points.TryGetValue(i, out var dPt) && ChartModel.Number(dPt, "explosion") is { } e ? e / 100 : seriesExplosion;

        // Slices: start and end angle (degrees clockwise from the back, as seen from above) and their offset.
        var slices = new List<(int Index, double Start, double End, double Dx, double Dz)>();
        var angle = view.RotY;
        for (var i = 0; i < values.Count; i++)
        {
            var sweep = values[i] / total * 360;
            if (sweep <= 0)
                continue;
            var middle = (angle + sweep / 2) * Math.PI / 180;
            var e = Explosion(i);
            slices.Add((i, angle, angle + sweep, e * Math.Sin(middle), e * Math.Cos(middle)));
            angle += sweep;
        }
        (double X, double Y, double Z) Rim(double degrees, double y, (double Dx, double Dz) offset) =>
            (Math.Sin(degrees * Math.PI / 180) + offset.Dx, y, Math.Cos(degrees * Math.PI / 180) + offset.Dz);

        // The camera fits the whole disk, as wide as the farthest exploded slice reaches, enlarged by 2.5 %.
        var camera = new Perspective(view.RotX, 0);
        var volume = new List<(double X, double Y, double Z)>();
        var reach = (1 + slices.Max(s => Math.Sqrt(s.Dx * s.Dx + s.Dz * s.Dz))) * 1.025;
        for (var k = 0; k < 72; k++)
        {
            var (sin, cos) = Math.SinCos(k * Math.PI / 36);
            foreach (var y in new[] { thickness / 2, -thickness / 2 })
                volume.Add(camera.View(sin * reach, y * 1.025, cos * reach));
        }
        camera.Fit(volume, outer, view.Perspective / 2);
        var eye = camera.Eye;

        // Which faces the eye sees: a face is visible when the eye lies on the side its outward normal points to.
        bool Faces((double X, double Y, double Z) point, (double X, double Y, double Z) normal)
        {
            var p = camera.View(point.X, point.Y, point.Z);
            var n = camera.Direction(normal.X, normal.Y, normal.Z);
            return (eye.X - p.X) * n.X + (eye.Y - p.Y) * n.Y + (eye.Z - p.Z) * n.Z > 0;
        }
        var topUp = Faces((0, thickness / 2, 0), (0, 1, 0));
        var capY = topUp ? thickness / 2 : -thickness / 2;
        var capNormal = camera.Direction(0, topUp ? 1 : -1, 0);
        var capShade = Diffuse(capNormal);
        var capHighlight = Highlight(capNormal);

        // The sides' gradient runs across the whole pie, from its left to its right edge.
        var rimPoints = slices.SelectMany(s => Enumerable.Range(0, 25).Select(k => camera.Map(
            Rim(s.Start + (s.End - s.Start) * k / 24, 0, (s.Dx, s.Dz)).X, 0, Rim(s.Start + (s.End - s.Start) * k / 24, 0, (s.Dx, s.Dz)).Z))).ToList();
        var (left, right) = (rimPoints.Min(p => p.X), rimPoints.Max(p => p.X));
        // The diffuse light of the side facing the eye, falling across the pie by the light's horizontal part.
        var front = camera.Direction(0, 0, -1);
        var sideBase = Ambient + front.Y * LightY + front.Z * LightZ;
        var slope = LightX;

        string PathOf(IEnumerable<(double X, double Y)> points) => string.Concat(points.Select((p, i) => (i == 0 ? "M" : "L") + XY(p))) + "Z";
        string Stroke(Outline? outline) => outline is { Visible: true } ? paint.Stroke(outline, 0, 0, defs) : "";

        string Side((int Index, double Start, double End, double Dx, double Dz) s, RgbColor color, Outline? outline)
        {
            // The visible part of the slice's rim: where the outward normal faces the eye.
            var steps = Math.Max(2, (int)Math.Ceiling((s.End - s.Start) / 2));
            var visible = new List<(double From, double To)>();
            double? from = null;
            for (var k = 0; k <= steps; k++)
            {
                var a = s.Start + (s.End - s.Start) * k / steps;
                var r = Rim(a, 0, (s.Dx, s.Dz));
                var shown = Faces(r, (Math.Sin(a * Math.PI / 180), 0, Math.Cos(a * Math.PI / 180)));
                if (shown && from == null) from = a;
                if (!shown && from != null) { visible.Add((from.Value, a)); from = null; }
            }
            if (from != null) visible.Add((from.Value, s.End));
            var svg = new StringBuilder();
            foreach (var (a0, a1) in visible)
            {
                var n = Math.Max(2, (int)Math.Ceiling((a1 - a0) / 2));
                var top = Enumerable.Range(0, n + 1).Select(k => a0 + (a1 - a0) * k / n).Select(a => Rim(a, thickness / 2, (s.Dx, s.Dz))).ToList();
                var bottom = Enumerable.Range(0, n + 1).Select(k => a1 - (a1 - a0) * k / n).Select(a => Rim(a, -thickness / 2, (s.Dx, s.Dz))).ToList();
                var polygon = top.Concat(bottom).Select(p => camera.Map(p.X, p.Y, p.Z)).ToList();
                var id = paint.NewId("pie");
                defs.Append($"<linearGradient id=\"{id}\" gradientUnits=\"userSpaceOnUse\" x1=\"{Px(left)}\" y1=\"0\" x2=\"{Px(right)}\" y2=\"0\">");
                for (var k = 0; k <= 8; k++)
                {
                    var t = k / 4.0 - 1;
                    var shade = Math.Max(Ambient, sideBase + slope * t);
                    var c = Lit(color, shade);
                    defs.Append($"<stop offset=\"{Units.Css4(k / 8.0)}\" stop-color=\"{c.ToHex()}\"{(c.A < 0.999 ? $" stop-opacity=\"{Units.Css4(c.A)}\"" : "")}/>");
                }
                defs.Append("</linearGradient>");
                svg.Append($"<path d=\"{PathOf(polygon)}\" fill=\"url(#{id})\"{Stroke(outline)}/>");
            }
            return svg.ToString();
        }

        string Cuts((int Index, double Start, double End, double Dx, double Dz) s, RgbColor color, Outline? outline)
        {
            // The two radial faces of a slice standing apart.
            var svg = new StringBuilder();
            foreach (var (a, sign) in new[] { (s.Start, -1.0), (s.End, 1.0) })
            {
                var rad = a * Math.PI / 180;
                var normal = (sign * Math.Cos(rad), 0.0, -sign * Math.Sin(rad));
                var centre = (s.Dx, 0.0, s.Dz);
                if (!Faces((centre.Item1 + Math.Sin(rad) / 2, 0, centre.Item3 + Math.Cos(rad) / 2), normal))
                    continue;
                var corners = new[]
                {
                    (s.Dx, thickness / 2, s.Dz), Rim(a, thickness / 2, (s.Dx, s.Dz)), Rim(a, -thickness / 2, (s.Dx, s.Dz)), (s.Dx, -thickness / 2, s.Dz),
                };
                var shade = Diffuse(camera.Direction(normal.Item1, normal.Item2, normal.Item3), CutLightX);
                svg.Append($"<path d=\"{PathOf(corners.Select(p => camera.Map(p.Item1, p.Item2, p.Item3)))}\" fill=\"{Lit(color, shade).ToHex()}\"{Stroke(outline)}/>");
            }
            return svg.ToString();
        }

        string Cap((int Index, double Start, double End, double Dx, double Dz) s, Fill fill, RgbColor color, Outline? outline)
        {
            var n = Math.Max(2, (int)Math.Ceiling((s.End - s.Start) / 2));
            var points = new List<(double X, double Y)> { camera.Map(s.Dx, capY, s.Dz) };
            for (var k = 0; k <= n; k++)
            {
                var p = Rim(s.Start + (s.End - s.Start) * k / n, capY, (s.Dx, s.Dz));
                points.Add(camera.Map(p.X, p.Y, p.Z));
            }
            var lit = fill is SolidFill ? new SolidFill(Lit(color, capShade, capHighlight)) : fill;
            return $"<path d=\"{PathOf(points)}\"{paint.Fill(lit, width * PxPerPt, height * PxPerPt, defs)}{Stroke(outline)}/>";
        }

        var svg = new StringBuilder();
        var exploded = slices.Any(s => Math.Abs(s.Dx) + Math.Abs(s.Dz) > 1e-9);
        // Far slices first; without explosion all sides go before all tops (tops never hide behind sides).
        var ordered = slices.OrderByDescending(s => camera.View(s.Dx + Math.Sin((s.Start + s.End) / 2 * Math.PI / 180) / 2, 0,
            s.Dz + Math.Cos((s.Start + s.End) / 2 * Math.PI / 180) / 2).Z).ToList();
        foreach (var s in ordered)
        {
            var fill = PointFill(group, series, s.Index);
            var color = Fill.MainColor(fill) ?? RgbColor.Black;
            var outline = PointOutline(series, s.Index);
            if (exploded)
                svg.Append(Cuts(s, color, outline));
            svg.Append(Side(s, color, outline));
            if (exploded)
                svg.Append(Cap(s, fill, color, outline));
        }
        if (!exploded)
        {
            foreach (var s in slices)
                svg.Append(Cap(s, PointFill(group, series, s.Index), Fill.MainColor(PointFill(group, series, s.Index)) ?? RgbColor.Black, PointOutline(series, s.Index)));
        }
        return svg.ToString();
    }
}
