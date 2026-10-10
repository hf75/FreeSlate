using System.Globalization;
using System.Text;
using System.Xml.Linq;
using FreeSlate.Drawing;

namespace FreeSlate.Pptx.Charts;

/// <summary>
/// 3D charts (catalog: chart.threeD). The plot is a box: the category axis along its width, the values along its
/// height and the depth into the screen, with the back wall, the side wall and the floor on its far sides. With
/// right-angle axes (c:rAngAx) the box is drawn in an oblique projection: front faces stay rectangles and the depth
/// runs along (sin rotY, sin rotX) at full length (measured on single boxes with colored walls at 20 rotations).
/// </summary>
public sealed partial class ChartRenderer
{
    /// <summary>The view of c:view3D: rotations, depth and height in percent, right-angle axes, perspective.</summary>
    sealed record View3D(double RotX, double RotY, double DepthPercent, bool RightAngleAxes, double Perspective, double? HeightPercent)
    {
        public static View3D Read(XElement chart, bool pie)
        {
            var view = chart.Element(Ns.C + "view3D");
            return new View3D(
                ChartModel.Number(view, "rotX") ?? (pie ? 30 : 15),
                ChartModel.Number(view, "rotY") ?? (pie ? 0 : 20),
                ChartModel.Number(view, "depthPercent") ?? 100,
                view?.Element(Ns.C + "rAngAx") != null && ChartModel.Flag(view, "rAngAx", false),
                ChartModel.Number(view, "perspective") ?? 30,
                ChartModel.Number(view, "hPercent"));
        }
    }

    /// <summary>
    /// How far thick walls (c:thickness) reach past the box: the side wall left or right of it, the floor below or
    /// above it, the back wall behind it.
    /// </summary>
    readonly record struct Slabs(double Left, double Right, double Below, double Above, double Behind);

    /// <summary>How the box of a 3D chart (x right, y up, z away from the viewer; 0…w, 0…h, 0…d) lands on the chart.</summary>
    abstract class Projection3D
    {
        /// <summary>Scales and places the projected box, with its thick walls, in the plot box.</summary>
        public abstract void Fit(double w, double h, double d, Box plot, Slabs slabs = default);

        /// <summary>A point of the box on the chart, in points.</summary>
        public abstract (double X, double Y) Map(double x, double y, double z);

        /// <summary>Whether the face through a point with the given outward normal is turned towards the viewer.</summary>
        public abstract bool Faces((double X, double Y, double Z) point, (double X, double Y, double Z) normal);

        /// <summary>A painting order: things farther from the viewer have larger values.</summary>
        public abstract double Depth(double x, double y, double z);

        /// <summary>Whether things below <paramref name="plane"/> on an axis (0 x, 1 y, 2 z) lie on the viewer's side of it.</summary>
        public abstract bool NearerBelow(int axis, double plane);

        /// <summary>Whether the viewer looks at the box from its right (else from its left) and from above (else from below).</summary>
        public abstract (bool FromRight, bool FromAbove) Side { get; }

        /// <summary>A direction of the box as the camera sees it (x right, y up, z away); right-angle axes keep the box's own.</summary>
        public virtual (double X, double Y, double Z) Seen((double X, double Y, double Z) direction) => direction;

        /// <summary>The length of a segment of the box on the chart.</summary>
        public double Length((double X, double Y, double Z) a, (double X, double Y, double Z) b)
        {
            var (p, q) = (Map(a.X, a.Y, a.Z), Map(b.X, b.Y, b.Z));
            return Math.Sqrt((p.X - q.X) * (p.X - q.X) + (p.Y - q.Y) * (p.Y - q.Y));
        }
    }

    /// <summary>
    /// The oblique projection of right-angle axes: a point (x, y, z) of the box lies at (x + z sin rotY,
    /// y + z sin rotX), scaled and centered to fit the plot box.
    /// </summary>
    sealed class Oblique(double sx, double sy) : Projection3D
    {
        public double Sx { get; } = sx;
        public double Sy { get; } = sy;
        double scale = 1, offsetX, offsetY;

        public override (double X, double Y) Map(double x, double y, double z) => (offsetX + (x + z * Sx) * scale, offsetY - (y + z * Sy) * scale);

        public override void Fit(double w, double h, double d, Box plot, Slabs slabs = default)
        {
            var depth = d + slabs.Behind;
            var (u0, u1) = (-slabs.Left + Math.Min(0, depth * Sx), w + slabs.Right + Math.Max(0, depth * Sx));
            var (v0, v1) = (-slabs.Below + Math.Min(0, depth * Sy), h + slabs.Above + Math.Max(0, depth * Sy));
            scale = Math.Min(plot.W / Math.Max(1e-9, u1 - u0), plot.H / Math.Max(1e-9, v1 - v0));
            offsetX = plot.X + (plot.W - (u1 - u0) * scale) / 2 - u0 * scale;
            offsetY = plot.Y + (plot.H - (v1 - v0) * scale) / 2 + v1 * scale;
        }

        // Points along (−sin rotY, −sin rotX, 1) fall on one spot: a face is seen when its normal points against it.
        public override bool Faces((double X, double Y, double Z) point, (double X, double Y, double Z) normal) =>
            -normal.X * Sx - normal.Y * Sy + normal.Z < -1e-9;

        // Far rows first, then along the direction the depth runs, then from the bottom (for boxes in rows).
        public override double Depth(double x, double y, double z) => z * 1e6 - x * Math.Sign(Sx) * 1e3 - y * Math.Sign(Sy);

        // The viewer looks along (−sin rotY, −sin rotX, 1): lower coordinates come first where that direction grows.
        public override bool NearerBelow(int axis, double plane) => axis switch { 0 => -Sx > 0, 1 => -Sy > 0, _ => true };

        public override (bool FromRight, bool FromAbove) Side => (Sx >= 0, Sy >= 0);
    }

    /// <summary>
    /// The perspective view of charts without right-angle axes. The camera fits the box each way on its own: its
    /// place across is where the box centred across fills the plot area's width, its place up and down where the box
    /// centred that way fills the height, and it backs off to 1.032 times the farther of both distances, so the box is
    /// off centre the way it does not fill (measured on 40 empty column boxes with colored walls, standard and
    /// clustered, rotations 10–80°, elevations 5–45°, perspectives 20–100, depths 40–400 % and seven plot areas: the
    /// corners within 1–5 px, where centring both ways was up to 77 px off).
    /// </summary>
    sealed class PerspectiveBox(View3D view) : Projection3D
    {
        const double BackOff = 1.032;
        readonly Perspective camera = new(view.RotX, view.RotY);
        double cx, cy, cz;

        public override void Fit(double w, double h, double d, Box plot, Slabs slabs = default)
        {
            var (x0, x1, y0, y1, z1) = (-slabs.Left, w + slabs.Right, -slabs.Below, h + slabs.Above, d + slabs.Behind);
            (cx, cy, cz) = ((x0 + x1) / 2, (y0 + y1) / 2, z1 / 2);
            var (hx, hy, hz) = ((x1 - x0) / 2, (y1 - y0) / 2, z1 / 2);
            var volume = new List<(double X, double Y, double Z)>();
            foreach (var x in new[] { -hx, hx })
                foreach (var y in new[] { -hy, hy })
                    foreach (var z in new[] { -hz, hz })
                        volume.Add(camera.View(x, y, z));
            camera.Fit(volume, plot, view.Perspective / 2, eachWay: true, BackOff);
        }

        public override (double X, double Y) Map(double x, double y, double z) => camera.Map(x - cx, y - cy, z - cz);

        public override bool Faces((double X, double Y, double Z) point, (double X, double Y, double Z) normal)
        {
            var p = camera.View(point.X - cx, point.Y - cy, point.Z - cz);
            var n = camera.View(normal.X, normal.Y, normal.Z);
            var eye = camera.Eye;
            return (eye.X - p.X) * n.X + (eye.Y - p.Y) * n.Y + (eye.Z - p.Z) * n.Z > 1e-9;
        }

        public override double Depth(double x, double y, double z) => camera.View(x - cx, y - cy, z - cz).Z;

        public override (double X, double Y, double Z) Seen((double X, double Y, double Z) direction) => camera.View(direction.X, direction.Y, direction.Z);

        public override bool NearerBelow(int axis, double plane)
        {
            var eye = camera.ToModel(camera.Eye);
            return (axis switch { 0 => eye.X + cx, 1 => eye.Y + cy, _ => eye.Z + cz }) < plane;
        }

        public override (bool FromRight, bool FromAbove) Side
        {
            get
            {
                // An eye right in front counts as from the right (measured: at rotY 0 the side wall stands on the left).
                var eye = camera.ToModel(camera.Eye);
                return (eye.X > -1e-9, eye.Y > -1e-9);
            }
        }
    }

    /// <summary>The 3D chart of the plot area, or null when it is not one this renderer draws in 3D.</summary>
    string? Chart3D(List<ChartGroup> groups, Box outer)
    {
        var group = groups.FirstOrDefault(g => g.Type.EndsWith("3DChart", StringComparison.Ordinal));
        if (group == null)
            return null;
        var view = View3D.Read(model.Chart, group.IsPie);
        if (group.IsBar || group.IsArea || group.IsLine || group.IsSurface)
            return Axes3D(group, outer, view);
        if (group.Type == "pie3DChart")
            return Pie3D(group, outer, view);
        return null;
    }

    // The chart light (measured on bars, walls, areas and lines): 0.4 ambient plus a light from the upper right front,
    // so faces towards the viewer get 1.0, tops and the floor 0.757, right sides and the side wall 0.639, and the
    // sloped tops of areas and lines something in between (0.4 + n · (0.239, 0.357, −0.6) for unit normals n).
    const double TopShade = 0.757, SideShade = 0.639;
    static readonly (double X, double Y, double Z) ChartLight = (0.239, 0.357, -0.6);

    static double Shade(double nx, double ny, double nz)
    {
        var length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
        return length < 1e-12 ? 1 : Ambient + Math.Max(0, (nx * ChartLight.X + ny * ChartLight.Y + nz * ChartLight.Z) / length);
    }

    /// <summary>The frame of a 3D chart with axes: the box in its projection, the value scale and the data positions.</summary>
    sealed class Frame3D
    {
        public required Projection3D Projection { get; init; }
        public required bool Columns { get; init; }
        public required int Categories { get; init; }
        public required double CategoryWidth { get; init; }
        public required double RowDepth { get; init; }
        public required double Depth { get; init; }
        public required AxisLayout Value { get; init; }
        public required ChartAxis CategoryAxis { get; init; }
        public required bool Between { get; init; }
        public int SeriesCount { get; init; } = 1;

        /// <summary>Series stand on their ticks (surfaces) rather than in the middle of their rows.</summary>
        public bool SeriesOnTicks { get; init; }

        public double W { get; set; } = 1;
        public double H { get; set; } = 1;

        /// <summary>A value's position along the value side of the box (height of columns, width of bars).</summary>
        public double ValuePos(double v) => Value.Scale.Fraction(Math.Clamp(v, Value.Scale.Min, Value.Scale.Max)) * (Columns ? H : W);

        /// <summary>A position along the category side (0 … 1 of it).</summary>
        public double CategoryPos(double t) => (CategoryAxis.Reversed ? 1 - t : t) * (Columns ? W : H);

        /// <summary>Where category i's data point sits: in its middle, or on its tick (areas).</summary>
        public double CategoryCenter(int i) =>
            CategoryPos(Between ? (i + 0.5) * CategoryWidth : Categories > 1 ? i / (double)(Categories - 1) : 0.5);

        /// <summary>The depth of series k's data.</summary>
        public double SeriesPos(int k) => SeriesOnTicks ? (SeriesCount > 1 ? k / (double)(SeriesCount - 1) * Depth : Depth / 2) : (k + 0.5) * RowDepth;

        public double SideX => Projection.Side.FromRight ? 0 : W;
        public double FloorY => Projection.Side.FromAbove ? 0 : H;
    }

    /// <summary>
    /// A 3D chart with axes (bars, areas, lines). Bars are boxes: clustered and stacked bars stand in one row whose
    /// depth is depthPercent × (1 + gapDepth) bar widths, each bar depthPercent × its width deep and centered in it;
    /// standard charts (and areas and lines) give every series a row of depthPercent × the category width. The box is
    /// as tall as the plot area's height to width ratio times its width (times ⌊(categories + series) / 2⌋ category
    /// widths for standard charts, measured on 22 combinations); it is scaled to fit and centered.
    /// </summary>
    string Axes3D(ChartGroup group, Box outer, View3D view)
    {
        var categoryAxis = model.Axes.FirstOrDefault(a => a.Kind is "catAx" or "dateAx" && group.AxisIds.Contains(a.Id));
        var valueAxis = model.Axes.FirstOrDefault(a => a.IsValue && group.AxisIds.Contains(a.Id));
        var seriesAxis = model.Axes.FirstOrDefault(a => a.Kind == "serAx" && group.AxisIds.Contains(a.Id));
        if (categoryAxis == null || valueAxis == null || group.Series.Count == 0)
            return "";
        var series = group.Series;
        // Areas and lines without stacking put every series in its own row like standard bars.
        var rows = group.Grouping == "standard" || group.IsLine || group.IsSurface;
        var columns = !group.Horizontal;
        var nCat = Math.Max(1, series.Max(s => Math.Max(s.Values.Count, s.Categories.Count)));
        var nSer = series.Count;
        var gap = group.GapWidth / 100;
        var gapDepth = (ChartModel.Number(group.Element, "gapDepth") ?? 150) / 100;
        var depth = view.DepthPercent / 100;

        var category = new AxisLayout { Axis = categoryAxis, Groups = [group], Style = Style(categoryAxis.TxPr, "categoryAxis") };
        var names = series.Select(s => s.Categories).FirstOrDefault(c => c.Count > 0) ?? [];
        category.Categories = nCat;
        category.Labels = Enumerable.Range(0, nCat).Select(i => i < names.Count ? CategoryLabel(categoryAxis, series, names, i) : (i + 1).ToString(culture)).ToList();
        category.MaxLabelWidth = category.Labels.Max(l => Width(l, category.Style));
        var value = new AxisLayout { Axis = valueAxis, Groups = [group], Style = Style(valueAxis.TxPr, "valueAxis") };
        var seriesLayout = seriesAxis != null && rows
            ? new AxisLayout { Axis = seriesAxis, Groups = [group], Style = Style(seriesAxis.TxPr, "seriesAxis"), Labels = series.Select(s => s.Name).ToList() }
            : null;

        // The box: category axis length 1 (width of columns, height of bars), values along the other side.
        var c = 1.0 / nCat;
        var barWidth = group.IsBar ? (group.Stacked || rows ? c / (1 + gap) : c / (nSer + gap)) : c;
        // Stacked areas share one row as deep as a category (their slab 1 / (1 + gapDepth) of it).
        var rowDepth = rows || !group.IsBar ? depth * c : depth * (1 + gapDepth) * barWidth;
        Projection3D projection = view.RightAngleAxes
            ? new Oblique(Math.Sin(view.RotY * Math.PI / 180), Math.Sin(view.RotX * Math.PI / 180))
            : new PerspectiveBox(view);
        var frame = new Frame3D
        {
            Projection = projection, Columns = columns, Categories = nCat, CategoryWidth = c, RowDepth = rowDepth,
            Depth = rows ? nSer * rowDepth : rowDepth, Value = value, CategoryAxis = categoryAxis,
            Between = !group.IsSurface && (!group.IsArea || valueAxis.CrossBetween != "midCat"),
            SeriesCount = nSer, SeriesOnTicks = group.IsSurface,
        };
        var d = frame.Depth;

        // Thick walls (c:thickness, a percentage of the box's smaller side across the value axis, measured: 10 % of a
        // 0.82 high box reach 0.083 of its width) are slabs outside the box: the side wall beside it on the side the
        // viewer sees, the floor below it, the back wall behind it.
        double Thickness(string wall) => (ChartModel.Number(model.Chart.Element(Ns.C + wall), "thickness") ?? 0) / 100;
        var (fromRight, fromAbove) = (Math.Sin(view.RotY * Math.PI / 180) >= 0, Math.Sin(view.RotX * Math.PI / 180) >= 0);
        var slabs = default(Slabs);
        // A manual layout places the box's plot area itself (layoutTarget inner) or the area its labels share with it
        // (measured: an inner layout 7 pt from the left and top, where COM reports the automatic plot area, moves the
        // box 4 pt up and left, the automatic one lying 11 pt inside the chart on every side).
        var manual = ManualPlot();
        if (ManualOuterPlot() is { } placed)
            outer = placed;
        // Seen from behind (the camera beyond the middle of the box's depth, in perspective at rotations between
        // about 90° and 270°) the back wall stands at the near end of the data and the floor's labels run along its
        // near edge. The vertical axis always stands at the box's left end (x = 0), on whichever of its two edges
        // there lies outside on the screen, its labels and their room on that side: from behind on the right
        // (measured on columns and surfaces turned 100° to 300°). The first pass guesses from the rotation, the
        // others ask the camera.
        var behind = !view.RightAngleAxes && Math.Cos(view.RotY * Math.PI / 180) < 0;
        var axisRight = behind;
        // The labels along the floor's near edges: under an edge within 34° of the horizontal, else beside it.
        (bool Under, bool Right)? floorAt = null, seriesAt = null;
        var plot = outer;
        for (var pass = 0; pass < 3; pass++)
        {
            // The labels take their room from the plot area like on flat charts; the box fills the rest.
            plot = manual ?? Plot3D(outer, columns ? value : category, columns ? category : value, seriesLayout, !frame.Between, axisRight, floorAt, seriesAt);
            var ratio = view.HeightPercent is { } hp ? hp / 100 : plot.H / Math.Max(1, plot.W);
            // Areas with their categories on the box's edges span n - 1 category widths of n: the box keeps the
            // height of n - 1 (measured on 3D stacked areas of 5 and 8 categories, on the ticks and between them).
            // Surfaces are ⌊(categories + series − 1) / 2⌋ category widths high (measured on 16 grids of 2 to 8
            // categories and 2 to 5 series, in perspective and with right-angle axes).
            var rowWidths = Math.Floor((nCat + nSer - (group.IsSurface ? 1 : 0)) / 2.0);
            if (columns)
                (frame.W, frame.H) = (1, rows ? ratio * c * rowWidths : frame.Between ? ratio : ratio * (nCat - 1) * c);
            else
                (frame.W, frame.H) = (1 / Math.Max(1e-9, ratio), 1);
            var unit = Math.Min(frame.W, frame.H);
            var (side, floor, back) = (Thickness("sideWall") * unit, Thickness("floor") * unit, Thickness("backWall") * unit);
            slabs = new Slabs(fromRight ? side : 0, fromRight ? 0 : side, fromAbove ? floor : 0, fromAbove ? 0 : floor, back);
            projection.Fit(frame.W, frame.H, d, plot, slabs);
            behind = !projection.NearerBelow(2, d / 2);
            axisRight = projection.Map(0, 0, d / 2).X > projection.Map(frame.W / 2, 0, d / 2).X;
            if (!view.RightAngleAxes)
            {
                // Under an edge within 34° of the horizontal, else beside it away from the box (measured: categories
                // under at 27° and 32°, beside at 36°; series under at 12°, beside at 41°).
                (bool, bool) Placement((double X, double Y) a, (double X, double Y) b) =>
                    (Math.Abs(b.Y - a.Y) < Math.Tan(34 * Math.PI / 180) * Math.Abs(b.X - a.X), (a.X + b.X) / 2 > projection.Map(frame.W / 2, frame.H / 2, d / 2).X);
                var (front, near) = (behind ? d : 0, frame.SideX == 0 ? frame.W : 0);
                floorAt = Placement(projection.Map(0, frame.FloorY, front), projection.Map(frame.W, frame.FloorY, front));
                seriesAt = Placement(projection.Map(near, frame.FloorY, 0), projection.Map(near, frame.FloorY, d));
            }
            // The value scale follows the box's edge halfway back (in perspective the front edge looks longer).
            var (edgeX, edgeY) = (frame.SideX, frame.FloorY);
            var length = columns ? projection.Length((edgeX, 0, d / 2), (edgeX, frame.H, d / 2)) : projection.Length((0, edgeY, d / 2), (frame.W, edgeY, d / 2));
            ScaleValueAxis3D(value, length);
            // Category labels that do not fit under their categories turn by 45° like on flat charts.
            if (columns && category.HasLabels)
                category.Rotation = Rotation(category, projection.Length((0, edgeY, behind ? d : 0), (frame.W, edgeY, behind ? d : 0)), threeD: true);
        }
        var (w, h) = (frame.W, frame.H);
        var sideX = frame.SideX;
        var floorY = frame.FloorY;
        // The outer faces of thick walls, where labels and axis lines go, and the far ends of the walls.
        var sideOuter = sideX == 0 ? -slabs.Left : w + slabs.Right;
        var floorOuter = floorY == 0 ? -slabs.Below : h + slabs.Above;
        var (farX, topY) = (sideX == 0 ? w : 0, floorY == 0 ? h : 0);
        var (frontZ, backZ, backOuter) = behind ? (d, 0.0, -slabs.Behind) : (0.0, d, d + slabs.Behind);
        // The vertical axis (values of columns, categories of bars) at the box's left end, on the side wall's outer
        // face when the wall stands there, on the outer of its two edges.
        var axisX = sideX == 0 ? sideOuter : 0.0;
        var axisZ = projection.Map(axisX, 0, d).X > projection.Map(axisX, 0, 0).X == axisRight ? d : 0.0;

        var svg = new StringBuilder();
        string Quad(params (double X, double Y, double Z)[] corners) =>
            string.Concat(corners.Select((p, i) => (i == 0 ? "M" : "L") + XY(projection.Map(p.X, p.Y, p.Z)))) + "Z";
        // The walls are lit like the faces of a box: the back wall like a front, the side wall like a side, the
        // floor like a top. Surface charts light them for their inner normals, 0.4 + 0.75 n · (0.572, 0.586,
        // −0.574), as the camera sees them in perspective and as they are with right-angle axes (measured on walls
        // seen from 13 sides).
        double WallShade((double X, double Y, double Z) inner, double box)
        {
            if (!group.IsSurface)
                return box;
            var n = projection.Seen(inner);
            return Math.Min(1, 0.4 + 0.75 * Math.Max(0, n.X * 0.572 + n.Y * 0.586 - n.Z * 0.574));
        }
        if (slabs.Behind > 0)
            svg.Append(WallSlab(model.Chart.Element(Ns.C + "backWall"), projection, (0, 0, Math.Min(backZ, backOuter)), (w, h, Math.Max(backZ, backOuter))));
        else
            svg.Append(Wall(model.Chart.Element(Ns.C + "backWall"), Quad((0, 0, backZ), (w, 0, backZ), (w, h, backZ), (0, h, backZ)), WallShade((0, 0, behind ? 1 : -1), 1)));
        if (sideOuter != sideX)
            svg.Append(WallSlab(model.Chart.Element(Ns.C + "sideWall"), projection, (Math.Min(sideX, sideOuter), 0, 0), (Math.Max(sideX, sideOuter), h, d)));
        else
            svg.Append(Wall(model.Chart.Element(Ns.C + "sideWall"), Quad((sideX, 0, 0), (sideX, 0, d), (sideX, h, d), (sideX, h, 0)), WallShade((sideX == 0 ? 1 : -1, 0, 0), SideShade)));
        if (floorOuter != floorY)
            svg.Append(WallSlab(model.Chart.Element(Ns.C + "floor"), projection, (0, Math.Min(floorY, floorOuter), 0), (w, Math.Max(floorY, floorOuter), d)));
        else
            svg.Append(Wall(model.Chart.Element(Ns.C + "floor"), Quad((0, floorY, 0), (w, floorY, 0), (w, floorY, d), (0, floorY, d)), WallShade((0, floorY == 0 ? 1 : -1, 0), TopShade)));

        // Gridlines on the back wall and on the wall or floor beside the value axis; on thick walls they also cross
        // the side wall's (or floor's) front face and the back wall's end (measured).
        string Line(Outline outline, params (double X, double Y, double Z)[] points)
        {
            var kept = points.Where((p, i) => i == 0 || p != points[i - 1]).ToList();
            return $"<path d=\"{string.Concat(kept.Select((p, i) => (i == 0 ? "M" : "L") + XY(projection.Map(p.X, p.Y, p.Z))))}\" fill=\"none\"{paint.Stroke(outline, 0, 0, defs)}/>";
        }
        foreach (var (element, minor) in new[] { (valueAxis.MinorGridlines, true), (valueAxis.MajorGridlines, false) })
        {
            if (element == null)
                continue;
            var outline = GridOutline(element);
            foreach (var t in minor ? value.Scale.MinorTicks() : value.Scale.Ticks())
            {
                var p = frame.ValuePos(t);
                svg.Append(columns
                    ? Line(outline, (sideOuter, p, frontZ), (sideX, p, frontZ), (sideX, p, backZ), (farX, p, backZ), (farX, p, backOuter))
                    : Line(outline, (p, floorOuter, frontZ), (p, floorY, frontZ), (p, floorY, backZ), (p, topY, backZ), (p, topY, backOuter)));
            }
        }
        if (categoryAxis.MajorGridlines is { } categoryGrid)
        {
            var outline = GridOutline(categoryGrid);
            for (var i = 0; i <= nCat; i++)
            {
                var p = frame.CategoryPos(frame.Between ? i * c : Math.Min(i, nCat - 1) / (double)Math.Max(1, nCat - 1));
                svg.Append(columns
                    ? Line(outline, (p, floorOuter, frontZ), (p, floorY, frontZ), (p, floorY, backZ), (p, topY, backZ), (p, topY, backOuter))
                    : Line(outline, (sideOuter, p, frontZ), (sideX, p, frontZ), (sideX, p, backZ), (farX, p, backZ), (farX, p, backOuter)));
            }
        }

        if (group.IsBar)
            svg.Append(BarSeries3D(group, frame, gap, gapDepth, depth, barWidth, rows));
        else if (group.IsArea)
            svg.Append(AreaSeries3D(group, frame, gapDepth, rows));
        else if (group.IsLine)
            svg.Append(LineSeries3D(group, frame, gapDepth));
        else if (group.IsSurface)
            svg.Append(SurfaceSeries3D(group, frame));

        // Axis lines along the front edges of the side wall and the floor, with their tick marks, and the series
        // axis along the floor's far side.
        var categoryOutline = AxisOutline(categoryAxis);
        if (AxisOutline(valueAxis) is { Visible: true } valueLine && !valueAxis.Deleted)
            svg.Append(columns ? Line(valueLine, (axisX, 0, axisZ), (axisX, h, axisZ)) : Line(valueLine, (0, floorOuter, frontZ), (w, floorOuter, frontZ)));
        if (categoryOutline is { Visible: true } && !categoryAxis.Deleted)
        {
            svg.Append(columns ? Line(categoryOutline, (0, floorOuter, frontZ), (w, floorOuter, frontZ)) : Line(categoryOutline, (axisX, 0, axisZ), (axisX, h, axisZ)));
            if (categoryAxis.MajorTickMark is "out" or "cross" && columns)
            {
                // Ticks run 5 px down from the floor's front edge at the category boundaries (or points).
                for (var i = 0; i <= (frame.Between ? nCat : nCat - 1); i++)
                {
                    var p = frame.CategoryPos(frame.Between ? i * c : i / (double)Math.Max(1, nCat - 1));
                    var (x, y) = projection.Map(p, floorOuter, frontZ);
                    svg.Append(GridLine(categoryOutline, x, y, x, y + TickLength));
                }
            }
        }
        if (seriesAxis is { Deleted: false } && rows && AxisOutline(seriesAxis) is { Visible: true } seriesLine)
        {
            var edge = sideX == 0 ? w : 0;
            svg.Append(Line(seriesLine, (edge, floorY, 0), (edge, floorY, d)));
            if (seriesAxis.MajorTickMark is "out" or "cross")
            {
                for (var k = 0; k <= (frame.SeriesOnTicks ? nSer - 1 : nSer); k++)
                {
                    var (x, y) = projection.Map(edge, floorY, frame.SeriesOnTicks ? frame.SeriesPos(k) : k * rowDepth);
                    svg.Append(GridLine(seriesLine, x, y, x, y + TickLength));
                }
            }
        }

        // Labels next to the front edges: values beside the side wall (columns) or under the floor (bars),
        // categories under the floor (columns) or beside the side wall (bars), series along the floor's far side.
        var valuePositions = value.Scale.Ticks().Select(frame.ValuePos).ToList();
        if (value.HasLabels)
        {
            for (var i = 0; i < value.Labels.Count && i < valuePositions.Count; i++)
                svg.Append(columns ? SideLabel(value, value.Labels[i], projection.Map(axisX, valuePositions[i], axisZ), axisRight) : FloorLabel(value, value.Labels[i], projection.Map(valuePositions[i], floorOuter, frontZ)));
        }
        if (category.HasLabels)
        {
            var tickRoom = categoryOutline is { Visible: true } && categoryAxis.MajorTickMark is "out" or "cross" && !categoryAxis.Deleted ? TickLength : 0;
            // Along a floor edge steeper than 34° the labels stand beside it, away from the box (measured on columns
            // turned 40° to 300°).
            var steep = columns && floorAt is { Under: false };
            var beside = floorAt is { Right: true };
            for (var i = 0; i < nCat; i++)
            {
                var p = frame.CategoryCenter(i);
                var at = columns ? projection.Map(p, floorOuter, frontZ) : projection.Map(axisX, p, axisZ);
                svg.Append(!columns ? SideLabel(category, category.Labels[i], at, axisRight)
                    : steep ? SideLabel(category, category.Labels[i], at, beside)
                    : FloorLabel(category, category.Labels[i], (at.X, at.Y + tickRoom)));
            }
        }
        if (seriesLayout is { HasLabels: true })
        {
            // Labels closer than a line apart show every second (third …) one, starting with the first; along an
            // edge within 34° of the horizontal they stand under it (measured at 260° and 300°).
            var edge = sideX == 0 ? w : 0;
            var under = seriesAt is { Under: true };
            var (p0, p1) = nSer > 1 ? (projection.Map(edge, floorY, frame.SeriesPos(0)), projection.Map(edge, floorY, frame.SeriesPos(1))) : default;
            var spacing = nSer > 1 ? (under ? Math.Abs(p1.X - p0.X) : Math.Abs(p1.Y - p0.Y)) : 0;
            var lineHeight = under ? seriesLayout.Labels.Max(l => Width(l, seriesLayout.Style)) : LineHeight(seriesLayout.Style);
            var step = nSer > 1 && spacing < lineHeight && spacing > 1e-6 ? (int)Math.Ceiling(lineHeight / spacing) : 1;
            for (var k = 0; k < nSer; k += step)
            {
                var (x, y) = projection.Map(edge, floorY, frame.SeriesPos(k));
                var label = seriesLayout.Labels[k];
                var s = seriesLayout.Style.Size;
                if (under)
                {
                    svg.Append(FloorLabel(seriesLayout, label, (x, y)));
                    continue;
                }
                var left = seriesAt?.Right ?? true ? x + ValueLabelGap * s : x - ValueLabelGap * s - Width(label, seriesLayout.Style);
                svg.Append(text.Label(label, seriesLayout.Style, left, CenteredBaseline(seriesLayout.Style, y)));
            }
        }
        return svg.ToString();
    }

    /// <summary>A category label: the cached text, or a date formatted with the axis number format.</summary>
    string CategoryLabel(ChartAxis axis, List<ChartSeries> series, List<string> names, int i)
    {
        var format = axis.SourceLinked ? series.FirstOrDefault()?.CategoryFormatCode ?? axis.FormatCode : axis.FormatCode;
        if (axis.Kind == "dateAx" && double.TryParse(names[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var serial))
            return NumberFormat.Format(serial, format, culture);
        return names[i];
    }

    /// <summary>The bars of a 3D bar chart, from the back, then in the direction the depth runs, then from the bottom.</summary>
    string BarSeries3D(ChartGroup group, Frame3D frame, double gap, double gapDepth, double depth, double barWidth, bool rows)
    {
        var series = group.Series;
        var projection = frame.Projection;
        var (c, rowDepth, d) = (frame.CategoryWidth, frame.RowDepth, frame.Depth);
        (double X, double Y, double Z) Point(double along, double across, double z) => frame.Columns ? (along, across, z) : (across, along, z);
        var cross = CrossValue(frame.Value, frame.CategoryAxis);
        var groupShape = ChartModel.Val(group.Element, "shape") ?? "box";
        var bars = new List<(double X0, double Y0, double Z0, double X1, double Y1, double Z1, Fill Fill, Outline? Outline, string Shape, double Base, double Apex,
            ChartSeries Series, int Point, double From, double To)>();
        for (var i = 0; i < frame.Categories; i++)
        {
            double positive = 0, negative = 0;
            var total = group.PercentStacked ? series.Sum(s => Math.Abs(s.Values.ElementAtOrDefault(i) ?? 0)) : 1;
            // A stack of cones or pyramids tapers over the whole stack.
            var stackTop = group.Stacked ? series.Sum(s => Math.Max(0, s.Values.ElementAtOrDefault(i) ?? 0)) / (group.PercentStacked && total != 0 ? total : 1) : 0;
            for (var k = 0; k < series.Count; k++)
            {
                if (series[k].Values.ElementAtOrDefault(i) is not { } v)
                    continue;
                double from, to;
                if (group.Stacked)
                {
                    if (group.PercentStacked) v = total == 0 ? 0 : v / total;
                    if (v >= 0) { from = positive; positive += v; to = positive; }
                    else { from = negative; negative += v; to = negative; }
                }
                else
                {
                    (from, to) = (cross, v);
                }
                var slot = group.Stacked || rows ? 0 : k;
                var a0 = frame.CategoryPos(i * c + gap * barWidth / 2 + slot * barWidth);
                var a1 = frame.CategoryPos(i * c + gap * barWidth / 2 + (slot + 1) * barWidth);
                var zCenter = rows ? (k + 0.5) * rowDepth : d / 2;
                var half = (rows ? rowDepth / (1 + gapDepth) : depth * barWidth) / 2;
                var (v0, v1) = (frame.ValuePos(Math.Min(from, to)), frame.ValuePos(Math.Max(from, to)));
                var (p0, p1) = (Point(Math.Min(a0, a1), v0, zCenter - half), Point(Math.Max(a0, a1), v1, zCenter + half));
                var shape = ChartModel.Val(series[k].Element, "shape") ?? groupShape;
                // Cones and pyramids end in a point: at the bar's end, the stack's end or the axis maximum (…ToMax).
                var (solidBase, apex) = shape switch
                {
                    "coneToMax" or "pyramidToMax" => (frame.ValuePos(cross), frame.ValuePos(frame.Value.Scale.Max)),
                    _ when group.Stacked => (frame.ValuePos(0), frame.ValuePos(stackTop)),
                    _ => (frame.ValuePos(from), frame.ValuePos(to)),
                };
                bars.Add((p0.X, p0.Y, p0.Z, p1.X, p1.Y, p1.Z, PointFill(group, series[k], i), PointOutline(series[k], i), shape, solidBase, apex, series[k], i,
                    frame.ValuePos(from), frame.ValuePos(to)));
            }
        }
        var ordered = projection is Oblique oblique
            ? bars.OrderByDescending(b => b.Z0).ThenBy(b => oblique.Sx >= 0 ? b.X0 : -b.X1).ThenBy(b => oblique.Sy >= 0 ? b.Y0 : -b.Y1).ToList()
            : BackToFront(bars, b => (b.X0, b.Y0, b.Z0, b.X1, b.Y1, b.Z1), projection);
        var svg = new StringBuilder();
        foreach (var bar in ordered)
        {
            svg.Append(Part(bar.Series, bar.Point, bar.Shape switch
            {
                "cylinder" or "cone" or "coneToMax" or "pyramid" or "pyramidToMax" =>
                    Solid3D(projection, bar.X0, bar.Y0, bar.Z0, bar.X1, bar.Y1, bar.Z1, bar.Fill, bar.Outline, bar.Shape, frame.Columns, bar.Base, bar.Apex),
                _ => Box3D(projection, bar.X0, bar.Y0, bar.Z0, bar.X1, bar.Y1, bar.Z1, bar.Fill, bar.Outline),
            }));
        }

        // Data labels: at the end of the bar beyond its end face (outEnd, the default) centered on its front face,
        // or on the front face (ctr, the default of stacked bars; inEnd, inBase).
        foreach (var bar in bars)
        {
            if (DataLabel(group, bar.Series, bar.Point) is not { } spec || bar.Series.Values.ElementAtOrDefault(bar.Point) is not { } raw || Beyond(frame.Value, raw))
                continue;
            var label = LabelText(spec, bar.Series, bar.Point, raw, bar.Series.Values.Sum(v => Math.Abs(v ?? 0)));
            var (w, h) = LabelSize(spec, label);
            var position = spec.Position ?? (group.Stacked ? "ctr" : "outEnd");
            var (x0, x1, y0, y1, z0, z1) = (bar.X0, bar.X1, bar.Y0, bar.Y1, bar.Z0, bar.Z1);
            var front = Math.Min(z0, z1);
            // The end face's corners on the chart, and the front face's middle across the bar.
            var end = bar.To;
            var endFace = (frame.Columns
                ? new[] { (x0, end, z0), (x1, end, z0), (x0, end, z1), (x1, end, z1) }
                : new[] { (end, y0, z0), (end, y1, z0), (end, y0, z1), (end, y1, z1) }).Select(p => projection.Map(p.Item1, p.Item2, p.Item3)).ToList();
            var startPoint = frame.Columns ? projection.Map((x0 + x1) / 2, bar.From, front) : projection.Map(bar.From, (y0 + y1) / 2, front);
            var endPoint = frame.Columns ? projection.Map((x0 + x1) / 2, end, front) : projection.Map(end, (y0 + y1) / 2, front);
            // Which way the bar grows on the chart: up or down for columns, right or left for bars.
            var grows = frame.Columns ? Math.Sign(startPoint.Y - endPoint.Y) : Math.Sign(endPoint.X - startPoint.X);
            if (grows == 0)
                grows = 1;
            Box box;
            if (frame.Columns)
            {
                var cx = endPoint.X;
                var top = grows > 0 ? endFace.Min(p => p.Y) : endFace.Max(p => p.Y);
                var y = position switch
                {
                    "ctr" => (startPoint.Y + endPoint.Y) / 2 - h / 2,
                    "inEnd" => grows > 0 ? endPoint.Y + LabelOffset : endPoint.Y - LabelOffset - h,
                    "inBase" => grows > 0 ? startPoint.Y - LabelOffset - h : startPoint.Y + LabelOffset,
                    _ => grows > 0 ? top - LabelOffset - h : top + LabelOffset,
                };
                box = new Box(cx - w / 2, y, w, h);
            }
            else
            {
                var cy = endPoint.Y;
                var reach = grows > 0 ? endFace.Max(p => p.X) : endFace.Min(p => p.X);
                var x = position switch
                {
                    "ctr" => (startPoint.X + endPoint.X) / 2 - w / 2,
                    "inEnd" => grows > 0 ? endPoint.X - LabelOffset - w : endPoint.X + LabelOffset,
                    "inBase" => grows > 0 ? startPoint.X + LabelOffset : startPoint.X - LabelOffset - w,
                    _ => grows > 0 ? reach + LabelOffset : reach - LabelOffset - w,
                };
                box = new Box(x, cy - h / 2, w, h);
            }
            var placed = Moved(spec, box);
            DrawLabel(spec, label, placed.X, placed.Y);
        }
        return svg.ToString();
    }

    /// <summary>
    /// Boxes from the back to the front: a box comes before every box whose picture overlaps its own and which a plane
    /// between the two puts on the viewer's side; others keep the order of their centers' depth (in perspective a tall
    /// bar's center can lie nearer than a short bar's in front of it).
    /// </summary>
    static List<T> BackToFront<T>(List<T> items, Func<T, (double X0, double Y0, double Z0, double X1, double Y1, double Z1)> boxOf, Projection3D projection)
    {
        var n = items.Count;
        var boxes = items.Select(boxOf).ToList();
        var depth = boxes.Select(b => projection.Depth((b.X0 + b.X1) / 2, (b.Y0 + b.Y1) / 2, (b.Z0 + b.Z1) / 2)).ToList();
        var bounds = boxes.Select(b =>
        {
            var corners = new[] { b.X0, b.X1 }.SelectMany(x => new[] { b.Y0, b.Y1 }.SelectMany(y => new[] { b.Z0, b.Z1 }.Select(z => projection.Map(x, y, z)))).ToList();
            return (L: corners.Min(c => c.X), T: corners.Min(c => c.Y), R: corners.Max(c => c.X), B: corners.Max(c => c.Y));
        }).ToList();
        var later = Enumerable.Range(0, n).Select(_ => new List<int>()).ToArray();
        var before = new int[n];
        static (double Lo, double Hi) Range((double X0, double Y0, double Z0, double X1, double Y1, double Z1) b, int axis) => axis switch
        {
            0 => (Math.Min(b.X0, b.X1), Math.Max(b.X0, b.X1)),
            1 => (Math.Min(b.Y0, b.Y1), Math.Max(b.Y0, b.Y1)),
            _ => (Math.Min(b.Z0, b.Z1), Math.Max(b.Z0, b.Z1)),
        };
        for (var i = 0; i < n; i++)
        {
            for (var j = i + 1; j < n; j++)
            {
                if (bounds[i].R <= bounds[j].L || bounds[j].R <= bounds[i].L || bounds[i].B <= bounds[j].T || bounds[j].B <= bounds[i].T)
                    continue;
                for (var axis = 0; axis < 3; axis++)
                {
                    var (a, b) = (Range(boxes[i], axis), Range(boxes[j], axis));
                    var eps = 1e-9 * Math.Max(1, Math.Abs(a.Hi) + Math.Abs(b.Hi));
                    int low, high;
                    double plane;
                    if (a.Hi <= b.Lo + eps) (low, high, plane) = (i, j, (a.Hi + b.Lo) / 2);
                    else if (b.Hi <= a.Lo + eps) (low, high, plane) = (j, i, (b.Hi + a.Lo) / 2);
                    else continue;
                    // The one on the viewer's side of the plane is painted last.
                    var (back, front) = projection.NearerBelow(axis, plane) ? (high, low) : (low, high);
                    later[back].Add(front);
                    before[front]++;
                    break;
                }
            }
        }
        var order = new List<T>(n);
        var done = new bool[n];
        var ready = new SortedSet<(double Depth, int Index)>(Enumerable.Range(0, n).Where(i => before[i] == 0).Select(i => (-depth[i], i)));
        while (order.Count < n)
        {
            int next;
            if (ready.Count > 0)
            {
                next = ready.Min.Index;
                ready.Remove(ready.Min);
            }
            else
            {
                // A cycle (boxes that cross): the farthest one left goes first.
                next = Enumerable.Range(0, n).Where(i => !done[i]).OrderByDescending(i => depth[i]).First();
            }
            if (done[next])
                continue;
            done[next] = true;
            order.Add(items[next]);
            foreach (var k in later[next])
            {
                if (!done[k] && --before[k] == 0)
                    ready.Add((-depth[k], k));
            }
        }
        return order;
    }

    /// <summary>A flat polygon of the box in a fill lit by the chart light for its normal (solid fills) or as it is.</summary>
    string Facet(Projection3D projection, IReadOnlyList<(double X, double Y, double Z)> corners, Fill fill, Outline? outline, double shade)
    {
        var d = string.Concat(corners.Select((p, i) => (i == 0 ? "M" : "L") + XY(projection.Map(p.X, p.Y, p.Z)))) + "Z";
        var shaded = Shaded(fill, shade);
        var seam = shaded is SolidFill s ? $" stroke=\"{s.Color.ToHex()}\" stroke-width=\"0.5\" stroke-linejoin=\"round\"" : "";
        var face = $"<path d=\"{d}\"{paint.Fill(shaded, width * PxPerPt, height * PxPerPt, defs)}{(outline is { Visible: true } ? paint.Stroke(outline, 0, 0, defs) : seam)}/>";
        if (shade < 1 && shaded is not SolidFill and not NoFill)
            face += $"<path d=\"{d}\" fill=\"#000\" fill-opacity=\"{Units.Css4(1 - shade)}\"/>";
        return face;
    }

    /// <summary>
    /// The areas of a 3D area chart: every series a slab in its row (stacked areas share one), its front face the
    /// area, its top and ends lit by the chart light for their slope; rows from the back.
    /// </summary>
    string AreaSeries3D(ChartGroup group, Frame3D frame, double gapDepth, bool rows)
    {
        var projection = frame.Projection;
        var series = group.Series;
        var svg = new StringBuilder();
        var stack = new double[frame.Categories];
        var slabs = new List<(int Index, double Z0, double Z1, List<(double X, double Y0, double Y1)> Points)>();
        for (var k = 0; k < series.Count; k++)
        {
            var total = new double[frame.Categories];
            var points = new List<(double X, double Y0, double Y1)>();
            for (var i = 0; i < frame.Categories; i++)
            {
                var v = series[k].Values.ElementAtOrDefault(i) ?? 0;
                if (group.PercentStacked)
                {
                    var sum = series.Sum(s => Math.Abs(s.Values.ElementAtOrDefault(i) ?? 0));
                    v = sum == 0 ? 0 : v / sum;
                }
                var from = group.Stacked ? stack[i] : 0;
                var to = from + v;
                if (group.Stacked) stack[i] = to;
                points.Add((frame.CategoryCenter(i), frame.ValuePos(Math.Min(from, to)), frame.ValuePos(Math.Max(from, to))));
            }
            var zCenter = rows ? (k + 0.5) * frame.RowDepth : frame.Depth / 2;
            var half = frame.RowDepth / (1 + gapDepth) / 2;
            slabs.Add((k, zCenter - half, zCenter + half, points));
        }
        foreach (var slab in slabs.OrderByDescending(s => s.Z0))
        {
            var partStart = svg.Length;
            var fill = PointFill(group, series[slab.Index], -1);
            var outline = SeriesOutline(group, series[slab.Index]);
            var pts = slab.Points;
            var (z0, z1) = (slab.Z0, slab.Z1);
            if (pts.Count == 0)
                continue;
            // Back face, ends and tops where they face the viewer, then the front face.
            var back = pts.Select(p => (p.X, p.Y1, z1)).Concat(pts.AsEnumerable().Reverse().Select(p => (p.X, p.Y0, z1))).ToList();
            if (projection.Faces((pts[0].X, pts[0].Y1, z1), (0, 0, 1)))
                svg.Append(Facet(projection, back, fill, outline, Shade(0, 0, 1)));
            var faces = new List<(double Depth, (double X, double Y, double Z)[] Corners, double Shade)>();
            for (var i = 0; i + 1 < pts.Count; i++)
            {
                var (a, b) = (pts[i], pts[i + 1]);
                // The top of the segment: its outward normal is perpendicular to it, pointing up.
                var (nx, ny) = (-(b.Y1 - a.Y1), b.X - a.X);
                if (ny < 0) (nx, ny) = (-nx, -ny);
                (double X, double Y, double Z)[] top = [(a.X, a.Y1, z0), (b.X, b.Y1, z0), (b.X, b.Y1, z1), (a.X, a.Y1, z1)];
                if (projection.Faces(((a.X + b.X) / 2, (a.Y1 + b.Y1) / 2, (z0 + z1) / 2), (nx, ny, 0)))
                    faces.Add((projection.Depth((a.X + b.X) / 2, (a.Y1 + b.Y1) / 2, (z0 + z1) / 2), top, Shade(nx, ny, 0)));
            }
            foreach (var (end, n) in new[] { (pts[0], -1.0), (pts[^1], 1.0) })
            {
                if (projection.Faces((end.X, (end.Y0 + end.Y1) / 2, (z0 + z1) / 2), (n, 0, 0)))
                    faces.Add((projection.Depth(end.X, (end.Y0 + end.Y1) / 2, (z0 + z1) / 2),
                        [(end.X, end.Y0, z0), (end.X, end.Y1, z0), (end.X, end.Y1, z1), (end.X, end.Y0, z1)], Shade(n, 0, 0)));
            }
            foreach (var face in faces.OrderByDescending(f => f.Depth))
                svg.Append(Facet(projection, face.Corners, fill, outline, face.Shade));
            var front = pts.Select(p => (p.X, p.Y1, z0)).Concat(pts.AsEnumerable().Reverse().Select(p => (p.X, p.Y0, z0))).ToList();
            if (projection.Faces((pts[0].X, pts[0].Y1, z0), (0, 0, -1)))
                svg.Append(Facet(projection, front, fill, outline, Shade(0, 0, -1)));
            WrapPart(svg, partStart, series[slab.Index], -1);
        }

        // Data labels in the middle of their band on the slab's front face (measured: a 3D area's values halfway
        // up at their categories).
        foreach (var slab in slabs)
        {
            var s = series[slab.Index];
            for (var i = 0; i < slab.Points.Count; i++)
            {
                if (DataLabel(group, s, i) is not { } spec || s.Values.ElementAtOrDefault(i) is not { } raw)
                    continue;
                var label = LabelText(spec, s, i, raw, s.Values.Sum(v => Math.Abs(v ?? 0)));
                var p = slab.Points[i];
                var (x, y) = projection.Map(p.X, (p.Y0 + p.Y1) / 2, slab.Z0);
                LabelAtPoint(spec, label, x, y, "ctr");
            }
        }
        return svg.ToString();
    }

    /// <summary>
    /// The lines of a 3D line chart: every series a ribbon in its row, a thin slab along the line whose top and
    /// front are lit by the chart light for their slope; rows from the back.
    /// </summary>
    string LineSeries3D(ChartGroup group, Frame3D frame, double gapDepth)
    {
        var projection = frame.Projection;
        var series = group.Series;
        var svg = new StringBuilder();
        // The ribbon is as thick as a twentieth of its depth, at least 1 pt on the chart (measured roughly).
        for (var k = series.Count - 1; k >= 0; k--)
        {
            var fill = PointFill(group, series[k], -1);
            var outline = SeriesOutline(group, series[k]);
            var zCenter = (k + 0.5) * frame.RowDepth;
            var half = frame.RowDepth / (1 + gapDepth) / 2;
            var (z0, z1) = (zCenter - half, zCenter + half);
            var thickness = half * 2 / 20;
            var points = new List<(double X, double Y)?>();
            for (var i = 0; i < frame.Categories; i++)
                points.Add(series[k].Values.ElementAtOrDefault(i) is { } v ? (frame.CategoryCenter(i), frame.ValuePos(v)) : null);
            var faces = new List<(double Depth, (double X, double Y, double Z)[] Corners, double Shade, int Point)>();
            for (var i = 0; i + 1 < points.Count; i++)
            {
                if (points[i] is not { } a || points[i + 1] is not { } b)
                    continue;
                var (nx, ny) = (-(b.Y - a.Y), b.X - a.X);
                if (ny < 0) (nx, ny) = (-nx, -ny);
                var length = Math.Sqrt(nx * nx + ny * ny);
                var (ox, oy) = (nx / length * thickness / 2, ny / length * thickness / 2);
                (double X, double Y, double Z)[] top = [(a.X + ox, a.Y + oy, z0), (b.X + ox, b.Y + oy, z0), (b.X + ox, b.Y + oy, z1), (a.X + ox, a.Y + oy, z1)];
                (double X, double Y, double Z)[] under = [(a.X - ox, a.Y - oy, z0), (b.X - ox, b.Y - oy, z0), (b.X - ox, b.Y - oy, z1), (a.X - ox, a.Y - oy, z1)];
                (double X, double Y, double Z)[] front = [(a.X + ox, a.Y + oy, z0), (b.X + ox, b.Y + oy, z0), (b.X - ox, b.Y - oy, z0), (a.X - ox, a.Y - oy, z0)];
                var centre = ((a.X + b.X) / 2, (a.Y + b.Y) / 2, (z0 + z1) / 2);
                var depthOf = projection.Depth(centre.Item1, centre.Item2, centre.Item3);
                if (projection.Faces(centre, (nx, ny, 0)))
                    faces.Add((depthOf, top, Shade(nx, ny, 0), i + 1));
                else
                    faces.Add((depthOf, under, Shade(-nx, -ny, 0), i + 1));
                faces.Add((depthOf - 1e-6, front, Shade(0, 0, -1), i + 1));
            }
            foreach (var face in faces.OrderByDescending(f => f.Depth))
                svg.Append(Part(series[k], face.Point, Facet(projection, face.Corners, fill, outline, face.Shade)));
        }

        // Data labels beside their points at the front of the ribbon, right of them like on flat lines.
        for (var k = 0; k < series.Count; k++)
        {
            var z0 = (k + 0.5) * frame.RowDepth - frame.RowDepth / (1 + gapDepth) / 2;
            for (var i = 0; i < frame.Categories; i++)
            {
                if (DataLabel(group, series[k], i) is not { } spec || series[k].Values.ElementAtOrDefault(i) is not { } raw)
                    continue;
                var label = LabelText(spec, series[k], i, raw, series[k].Values.Sum(v => Math.Abs(v ?? 0)));
                var (x, y) = projection.Map(frame.CategoryCenter(i), frame.ValuePos(raw), z0);
                LabelAtPoint(spec, label, x, y, spec.Position switch { "l" or "t" or "b" or "ctr" => spec.Position, _ => "r" });
            }
        }
        return svg.ToString();
    }

    /// <summary>
    /// The surface of a 3D surface chart: the grid of categories (left to right, on their ticks) and series (front to
    /// back, on theirs) at the values' heights, cut into triangles along each cell's diagonal with the larger sum
    /// (measured on folded cells) and every triangle into the value axis' bands, each piece in its band's fill lit for
    /// the triangle's slope; a wireframe draws the grid lines in the bands' lines instead.
    /// </summary>
    string SurfaceSeries3D(ChartGroup group, Frame3D frame)
    {
        var projection = frame.Projection;
        var series = group.Series;
        var (nCat, nSer) = (frame.Categories, series.Count);
        // The bands of the legend (the value axis' steps).
        var bands = Bands(SurfaceScale(group));
        if (bands.Count == 0 || nSer == 0)
            return "";
        var fills = Enumerable.Range(0, bands.Count).Select(b => BandFill(group, b)).ToList();
        var wireframe = ChartModel.Flag(group.Element, "wireframe", false);
        double X(int i) => frame.CategoryCenter(i);
        double Z(int k) => frame.SeriesPos(k);
        double Value(int k, int i) => series[k].Values.ElementAtOrDefault(i) ?? 0;
        double Height(double v) => frame.ValuePos(v);

        // The light of 3D surfaces (measured on single cells of known slope in 39 views): in perspective it stays with
        // the camera, 0.821 + 0.313 n · (0.768, 0.182, −0.614) for the normal as the camera sees it (x right, y up,
        // z away); with right-angle axes it stays with the box, 0.641 + 0.380 n · (0.616, 0.491, −0.616) at any
        // rotation.
        var (ambient, strength, light) = projection is PerspectiveBox
            ? (0.821, 0.313, (X: 0.768, Y: 0.182, Z: -0.614))
            : (0.641, 0.380, (X: 0.616, Y: 0.491, Z: -0.616));

        // A height field is drawn back to front by its grid cells on the floor (steep facets would sort wrongly by
        // their own centres), each cell's two triangles by theirs.
        var triangles = new List<(double Cell, double Depth, (double X, double Z, double V)[] Corners)>();
        for (var k = 0; k + 1 < nSer; k++)
        {
            for (var i = 0; i + 1 < nCat; i++)
            {
                var a = (X(i), Z(k), Value(k, i));
                var b = (X(i + 1), Z(k), Value(k, i + 1));
                var c = (X(i), Z(k + 1), Value(k + 1, i));
                var e = (X(i + 1), Z(k + 1), Value(k + 1, i + 1));
                (double X, double Z, double V)[][] pair = a.Item3 + e.Item3 >= c.Item3 + b.Item3 ? [[a, b, e], [a, e, c]] : [[c, a, b], [c, b, e]];
                var cell = projection.Depth((X(i) + X(i + 1)) / 2, frame.FloorY, (Z(k) + Z(k + 1)) / 2);
                foreach (var t in pair)
                    triangles.Add((cell, projection.Depth(t.Average(p => p.X), Height(t.Average(p => p.V)), t.Average(p => p.Z)), t));
            }
        }

        var svg = new StringBuilder();
        if (wireframe)
        {
            // Grid lines along the categories and along the series, each piece in the line of its band; a level
            // piece on a band boundary belongs to the band below (measured on single wireframe cells).
            void Edge((double X, double Z, double V) p, (double X, double Z, double V) q)
            {
                for (var band = 0; band < bands.Count; band++)
                {
                    var (low, high) = bands[band];
                    double t0 = 0, t1 = 1;
                    if (Math.Abs(q.V - p.V) < 1e-12)
                    {
                        var inside = (band == 0 || p.V > low) && (band == bands.Count - 1 || p.V <= high);
                        if (!inside) continue;
                    }
                    else
                    {
                        // The first and last bands reach on below and above their limits, along the edge either way.
                        var (lowV, highV) = (band == 0 ? double.NegativeInfinity : low, band == bands.Count - 1 ? double.PositiveInfinity : high);
                        var (lo, hi) = ((lowV - p.V) / (q.V - p.V), (highV - p.V) / (q.V - p.V));
                        if (lo > hi) (lo, hi) = (hi, lo);
                        (t0, t1) = (Math.Max(0, lo), Math.Min(1, hi));
                        if (t1 - t0 < 1e-9) continue;
                    }
                    var line = BandLine(group, band) ?? new Outline(9525, fills[band], "solid", "flat", "round", 8, "sng");
                    var (u, v) = (projection.Map(p.X + (q.X - p.X) * t0, Height(p.V + (q.V - p.V) * t0), p.Z + (q.Z - p.Z) * t0),
                                  projection.Map(p.X + (q.X - p.X) * t1, Height(p.V + (q.V - p.V) * t1), p.Z + (q.Z - p.Z) * t1));
                    svg.Append($"<path d=\"M{XY(u)}L{XY(v)}\" fill=\"none\"{paint.Stroke(line, 0, 0, defs)}/>");
                }
            }
            for (var k = 0; k < nSer; k++)
                for (var i = 0; i + 1 < nCat; i++)
                    Edge((X(i), Z(k), Value(k, i)), (X(i + 1), Z(k), Value(k, i + 1)));
            for (var i = 0; i < nCat; i++)
                for (var k = 0; k + 1 < nSer; k++)
                    Edge((X(i), Z(k), Value(k, i)), (X(i), Z(k + 1), Value(k + 1, i)));
            // The contour lines where the surface crosses a band boundary, in the line of the band below (measured
            // on single wireframe cells).
            foreach (var (_, _, corners) in triangles)
            {
                for (var band = 1; band < bands.Count; band++)
                {
                    var level = bands[band].Low;
                    var crossing = new List<(double X, double Z)>();
                    for (var e = 0; e < 3; e++)
                    {
                        var (p, q) = (corners[e], corners[(e + 1) % 3]);
                        if ((p.V - level) * (q.V - level) < 0)
                        {
                            var t = (level - p.V) / (q.V - p.V);
                            crossing.Add((p.X + (q.X - p.X) * t, p.Z + (q.Z - p.Z) * t));
                        }
                    }
                    if (crossing.Count != 2)
                        continue;
                    var line = BandLine(group, band - 1) ?? new Outline(9525, fills[band - 1], "solid", "flat", "round", 8, "sng");
                    var (u, v) = (projection.Map(crossing[0].X, Height(level), crossing[0].Z), projection.Map(crossing[1].X, Height(level), crossing[1].Z));
                    svg.Append($"<path d=\"M{XY(u)}L{XY(v)}\" fill=\"none\"{paint.Stroke(line, 0, 0, defs)}/>");
                }
            }
            return svg.ToString();
        }

        foreach (var (_, _, corners) in triangles.OrderByDescending(t => t.Cell).ThenByDescending(t => t.Depth))
        {
            // The facet's normal in the box (y up): its value gradient over the floor.
            var p3 = corners.Select(p => (p.X, Y: Height(p.V), p.Z)).ToArray();
            var (u1, u2) = ((p3[1].X - p3[0].X, p3[1].Y - p3[0].Y, p3[1].Z - p3[0].Z), (p3[2].X - p3[0].X, p3[2].Y - p3[0].Y, p3[2].Z - p3[0].Z));
            var n = (u1.Item2 * u2.Item3 - u1.Item3 * u2.Item2, u1.Item3 * u2.Item1 - u1.Item1 * u2.Item3, u1.Item1 * u2.Item2 - u1.Item2 * u2.Item1);
            if (n.Item2 < 0) n = (-n.Item1, -n.Item2, -n.Item3);
            var length = Math.Sqrt(n.Item1 * n.Item1 + n.Item2 * n.Item2 + n.Item3 * n.Item3);
            if (length < 1e-12)
                continue;
            var seen = projection.Seen((n.Item1 / length, n.Item2 / length, n.Item3 / length));
            var diffuse = Math.Max(0, ambient + strength * (seen.X * light.X + seen.Y * light.Y + seen.Z * light.Z));
            List<(double X, double Y, double V)> triangle = [.. corners.Select(p => (p.X, p.Z, p.V))];
            for (var band = 0; band < bands.Count; band++)
            {
                var (low, high) = bands[band];
                var part = band == 0 ? triangle : Clip(triangle, low, above: true);
                if (band < bands.Count - 1) part = Clip(part, high, above: false);
                if (part.Count < 3)
                    continue;
                var d = string.Concat(part.Select((p, idx) => (idx == 0 ? "M" : "L") + XY(projection.Map(p.X, Height(p.V), p.Y)))) + "Z";
                if (fills[band] is SolidFill solid)
                {
                    var lit = new RgbColor(Math.Min(1, solid.Color.R * diffuse), Math.Min(1, solid.Color.G * diffuse), Math.Min(1, solid.Color.B * diffuse), solid.Color.A);
                    svg.Append($"<path d=\"{d}\"{paint.Fill(new SolidFill(lit), 0, 0, defs)} stroke=\"{lit.ToHex()}\" stroke-width=\"0.5\" stroke-linejoin=\"round\"/>");
                }
                else
                {
                    svg.Append($"<path d=\"{d}\"{paint.Fill(fills[band], width * PxPerPt, height * PxPerPt, defs)}/>");
                }
            }
        }
        return svg.ToString();
    }

    /// <summary>The line of band <paramref name="index"/> (wireframe surfaces): its c:bandFmt's, or null.</summary>
    Outline? BandLine(ChartGroup group, int index)
    {
        var bandFmt = group.Element.Element(Ns.C + "bandFmts")?.Elements(Ns.C + "bandFmt").FirstOrDefault(b => b.Element(Ns.C + "idx").Int("val") == index);
        return bandFmt?.Element(Ns.C + "spPr")?.Element(Ns.A + "ln") is { } ln ? OutlineReader.Merge([(ln, colors)]) : null;
    }

    /// <summary>
    /// The plot area of a 3D chart inside its axis labels, as flat charts compute it (measured: the inner plot area
    /// of a 3D stacked area equals the flat chart's): value labels beside it, category labels below it, labels
    /// centered on the edges of charts whose categories sit on them (area and surface) within the outer edges, and
    /// the series axis's labels right of it a category label gap away; the side labels go right of it on charts
    /// seen from behind (<paramref name="sideRight"/>). Labels along a floor edge that runs more up than across
    /// stand beside it and take their room on that side instead of below (<paramref name="floorAt"/>,
    /// <paramref name="seriesAt"/>; series labels along an edge running more across stand under it).
    /// </summary>
    Box Plot3D(Box outer, AxisLayout side, AxisLayout floor, AxisLayout? series, bool edgeCategories, bool sideRight = false,
        (bool Under, bool Right)? floorAt = null, (bool Under, bool Right)? seriesAt = null)
    {
        var (floorUnder, floorRight) = floorAt ?? (true, false);
        var (seriesUnder, seriesRight) = seriesAt ?? (false, true);
        double left = outer.X, top = outer.Y, right = outer.Right, bottom = outer.Bottom;
        if (side.HasLabels && side.Labels.Count > 0)
        {
            // On the right (charts seen from behind) they take no room (measured: the box reaches as far right as
            // without labels).
            var widest = side.Labels.Max(l => Width(l, side.Style));
            if (!sideRight)
                left = Math.Max(left, outer.X - LabelBleed + widest + ValueLabelGap * side.Style.Size);
        }
        if (floor.HasLabels && floor.Labels.Count > 0 && floorUnder)
        {
            bottom = Math.Min(bottom, outer.Bottom + LabelBleed - LabelExtent(floor) - CategoryLabelGap * floor.Style.Size);
            if (edgeCategories && floor.Rotation == 0 && !floor.Axis.IsValue)
            {
                left = Math.Max(left, outer.X + Width(floor.Labels[0], floor.Style) / 2);
                right = Math.Min(right, outer.Right - Width(floor.Labels[^1], floor.Style) / 2);
            }
        }
        else if (floor.HasLabels && floor.Labels.Count > 0)
        {
            var widest = floor.Labels.Max(l => Width(l, floor.Style));
            if (floorRight)
                right = Math.Min(right, outer.Right + LabelBleed - widest - ValueLabelGap * floor.Style.Size);
            else
                left = Math.Max(left, outer.X - LabelBleed + widest + ValueLabelGap * floor.Style.Size);
        }
        if (series is { HasLabels: true } && series.Labels.Count > 0)
        {
            var widest = series.Labels.Max(l => Width(l, series.Style));
            if (seriesUnder)
                bottom = Math.Min(bottom, outer.Bottom + LabelBleed - LineHeight(series.Style) - CategoryLabelGap * series.Style.Size);
            else if (seriesRight)
                right = Math.Min(right, outer.Right + LabelBleed - widest - CategoryLabelGap * series.Style.Size);
            else
                left = Math.Max(left, outer.X - LabelBleed + widest + CategoryLabelGap * series.Style.Size);
        }
        if (side is { HasLabels: true, Axis.IsValue: true })
            top = Math.Max(top, outer.Y - 6 + LineHeight(side.Style) / 2);
        return new Box(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    /// <summary>Value axis scale of a 3D chart: like flat charts but without headroom above the largest value (measured: 5 stays 5).</summary>
    void ScaleValueAxis3D(AxisLayout layout, double length)
    {
        double min = double.PositiveInfinity, max = double.NegativeInfinity;
        var percent = false;
        foreach (var group in layout.Groups)
        {
            percent |= group.PercentStacked;
            foreach (var (lo, hi) in Ranges(group, layout.Axis))
            {
                min = Math.Min(min, lo);
                max = Math.Max(max, hi);
            }
        }
        if (double.IsInfinity(min)) (min, max) = (0, 1);
        var style = layout.Style;
        // Horizontal value axes keep a label and three font sizes per interval as on flat charts (measured: a 3D bar
        // chart's axis up to 5 steps by 1).
        var extent = layout.Axis.Horizontal
            ? Math.Max(Width(NumberFormat.Format(max, Format(layout), culture), style), Width(NumberFormat.Format(min, Format(layout), culture), style)) + 3 * style.Size
            : LineHeight(style) * 1.1;
        var maxIntervals = Math.Clamp((int)Math.Floor(length / Math.Max(1, extent)), 1, 10);
        layout.Scale = AxisScaling.Compute(min, max, layout.Axis, maxIntervals, percent, headroom: false);
        layout.Labels = layout.Scale.Ticks().Select(v => NumberFormat.Format(v, Format(layout), culture)).ToList();
        layout.MaxLabelWidth = layout.Labels.Count == 0 ? 0 : layout.Labels.Max(l => Width(l, style));
    }

    /// <summary>A label right-aligned before a point on the vertical axis's edge (left-aligned after it on the right), centered on it.</summary>
    string SideLabel(AxisLayout layout, string label, (double X, double Y) at, bool right = false) =>
        text.Label(label, layout.Style, right ? at.X + ValueLabelGap * layout.Style.Size : at.X - ValueLabelGap * layout.Style.Size - Width(label, layout.Style), CenteredBaseline(layout.Style, at.Y));

    /// <summary>A label centered under a point on the floor's front edge, or turned with its end (rising) or start at it.</summary>
    string FloorLabel(AxisLayout layout, string label, (double X, double Y) at)
    {
        var style = layout.Style;
        var w = Width(label, style);
        if (layout.Rotation == 0)
            return text.Label(label, style, at.X - w / 2, at.Y + CategoryLabelGap * style.Size + Ascent(style));
        var angle = Math.Abs(layout.Rotation) * Math.PI / 180;
        var anchorY = at.Y + CategoryLabelGap * style.Size + LineHeight(style) / 2 * Math.Cos(angle);
        return $"<g transform=\"translate({Px(at.X)} {Px(anchorY)}) rotate({Units.Css(layout.Rotation)})\">" +
               text.Label(label, style, layout.Rotation < 0 ? -w : 0, CenteredBaseline(style, 0)) + "</g>";
    }

    static string XY((double X, double Y) p) => $"{Px(p.X)} {Px(p.Y)}";

    /// <summary>A thick wall or floor: a box in its c:spPr fill and line, none by default.</summary>
    string WallSlab(XElement? wall, Projection3D projection, (double X, double Y, double Z) from, (double X, double Y, double Z) to)
    {
        var spPr = wall?.Element(Ns.C + "spPr");
        var fill = FillReader.FindFill(spPr) is { } f ? ReadFill(f) : Fill.None;
        var outline = spPr?.Element(Ns.A + "ln") is { } ln ? OutlineReader.Merge([(ln, colors)]) : null;
        return fill is NoFill && outline is not { Visible: true } ? "" : Box3D(projection, from.X, from.Y, from.Z, to.X, to.Y, to.Z, fill, outline);
    }

    /// <summary>A wall or the floor: its c:spPr fill (shaded) and line, none by default.</summary>
    string Wall(XElement? wall, string path, double shade)
    {
        var spPr = wall?.Element(Ns.C + "spPr");
        var fill = FillReader.FindFill(spPr) is { } f ? ReadFill(f) : Fill.None;
        var outline = spPr?.Element(Ns.A + "ln") is { } ln ? OutlineReader.Merge([(ln, colors)]) : null;
        if (fill is NoFill && outline is not { Visible: true })
            return "";
        var svg = $"<path d=\"{path}\"{paint.Fill(Shaded(fill, shade), width * PxPerPt, height * PxPerPt, defs)}{paint.Stroke(outline, 0, 0, defs)}/>";
        if (shade < 1 && fill is not (SolidFill or NoFill))
            svg += $"<path d=\"{path}\" fill=\"#000\" fill-opacity=\"{Units.Css4(1 - shade)}\"/>";
        return svg;
    }

    /// <summary>A solid fill darkened by a shade factor (in sRGB, measured); other fills stay as they are.</summary>
    static Fill Shaded(Fill fill, double shade) =>
        shade < 1 && fill is SolidFill solid ? new SolidFill(new RgbColor(solid.Color.R * shade, solid.Color.G * shade, solid.Color.B * shade, solid.Color.A)) : fill;

    /// <summary>
    /// A box's visible faces: the front in its fill, the top or bottom and the side towards the viewer darkened
    /// (solid fills shaded, other fills under a black veil); each face also strokes its own edge so that faces meet
    /// without seams.
    /// </summary>
    string Box3D(Projection3D projection, double x0, double y0, double z0, double x1, double y1, double z1, Fill fill, Outline? outline)
    {
        if (x1 - x0 < 1e-9 && y1 - y0 < 1e-9)
            return "";
        var svg = new StringBuilder();
        string Face((double X, double Y, double Z)[] corners, double shade)
        {
            var d = string.Concat(corners.Select((p, i) => (i == 0 ? "M" : "L") + XY(projection.Map(p.X, p.Y, p.Z)))) + "Z";
            var shaded = Shaded(fill, shade);
            var paintAttributes = paint.Fill(shaded, width * PxPerPt, height * PxPerPt, defs);
            var seam = shaded is SolidFill s ? $" stroke=\"{s.Color.ToHex()}\" stroke-width=\"0.5\" stroke-linejoin=\"round\"" + (s.Color.A < 0.999 ? $" stroke-opacity=\"{Units.Css4(s.Color.A)}\"" : "") : "";
            var face = $"<path d=\"{d}\"{paintAttributes}{(outline is { Visible: true } ? paint.Stroke(outline, 0, 0, defs) : seam)}/>";
            if (shade < 1 && shaded is not SolidFill and not NoFill)
                face += $"<path d=\"{d}\" fill=\"#000\" fill-opacity=\"{Units.Css4(1 - shade)}\"/>";
            return face;
        }
        var (mx, my, mz) = ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2);
        foreach (var (x, n) in new[] { (x0, -1.0), (x1, 1.0) })
        {
            if (projection.Faces((x, my, mz), (n, 0, 0)))
                svg.Append(Face([(x, y0, z0), (x, y0, z1), (x, y1, z1), (x, y1, z0)], SideShade));
        }
        foreach (var (y, n) in new[] { (y0, -1.0), (y1, 1.0) })
        {
            if (projection.Faces((mx, y, mz), (0, n, 0)))
                svg.Append(Face([(x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)], TopShade));
        }
        foreach (var (z, n) in new[] { (z1, 1.0), (z0, -1.0) })
        {
            if (projection.Faces((mx, my, z), (0, 0, n)))
                svg.Append(Face([(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)], 1));
        }
        return svg.ToString();
    }

    // Round bars (measured on PowerPoint's rendering): a cylinder's side runs from 0.6 of the color at its edges to
    // 1.07 in its middle, a cone's from 0.65 to 1.13, as 1.07 − 0.47 t² and 1.13 − 0.48 t² across the silhouette
    // (t from −1 to 1); caps are shaded like tops, pyramids like boxes.
    const double CylinderPeak = 1.07, CylinderDrop = 0.47, ConePeak = 1.13, ConeDrop = 0.48;

    /// <summary>
    /// A cylinder, cone or pyramid filling the bar's box (x0…x1, y0…y1, z0…z1) along its value direction (y for
    /// columns, x for bars). Cones and pyramids taper from <paramref name="solidBase"/> to a point at
    /// <paramref name="apex"/> (value positions); the bar shows the part between its ends. The silhouette of the
    /// round ones is the convex hull of the projected end sections.
    /// </summary>
    string Solid3D(Projection3D projection, double x0, double y0, double z0, double x1, double y1, double z1, Fill fill, Outline? outline,
        string shape, bool columns, double solidBase, double apex)
    {
        var round = shape.StartsWith("cyl", StringComparison.Ordinal) || shape.StartsWith("cone", StringComparison.Ordinal);
        var tapered = !shape.StartsWith("cyl", StringComparison.Ordinal);
        // Local frame: along the value direction from a0 to a1, across (category direction) and depth.
        var (a0, a1) = columns ? (y0, y1) : (x0, x1);
        var (c0, c1) = columns ? (x0, x1) : (y0, y1);
        var (cc, cz) = ((c0 + c1) / 2, (z0 + z1) / 2);
        var (rc, rz) = ((c1 - c0) / 2, (z1 - z0) / 2);
        double Radius(double a) =>
            !tapered || Math.Abs(apex - solidBase) < 1e-12 ? 1 : Math.Clamp((apex - a) / (apex - solidBase), 0, 1);
        (double X, double Y) Map(double a, double across, double depth) =>
            columns ? projection.Map(across, a, depth) : projection.Map(a, across, depth);
        List<(double X, double Y)> Section(double a)
        {
            var f = Radius(a);
            var points = new List<(double X, double Y)>();
            if (round)
            {
                for (var k = 0; k < 64; k++)
                {
                    var t = k * Math.PI / 32;
                    points.Add(Map(a, cc + rc * f * Math.Cos(t), cz + rz * f * Math.Sin(t)));
                }
            }
            else
            {
                foreach (var (sc, sz) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                    points.Add(Map(a, cc + sc * rc * f, cz + sz * rz * f));
            }
            return points;
        }
        var bottom = Section(a0);
        var top = Section(a1);
        var svg = new StringBuilder();
        static string PathOf(IEnumerable<(double X, double Y)> points) => string.Concat(points.Select((p, i) => (i == 0 ? "M" : "L") + XY(p))) + "Z";
        static string Seam(Fill f) => f is SolidFill s ? $" stroke=\"{s.Color.ToHex()}\" stroke-width=\"0.5\" stroke-linejoin=\"round\"" : "";
        var stroke = outline is { Visible: true } ? paint.Stroke(outline, 0, 0, defs) : null;
        // The end the viewer looks at, and whether it is more than a point.
        var capAtTop = columns ? projection.Faces((cc, a1, cz), (0, 1, 0)) : projection.Faces((a1, cc, cz), (1, 0, 0));
        var capEnd = capAtTop ? top : bottom;
        var capOpen = Radius(capAtTop ? a1 : a0) > 1e-6;

        if (round)
        {
            // The side: a gradient across the silhouette, perpendicular to the value direction.
            var hull = ConvexHull(bottom.Concat(top).ToList());
            var (lo, hi) = columns ? (hull.Min(p => p.X), hull.Max(p => p.X)) : (hull.Min(p => p.Y), hull.Max(p => p.Y));
            var (peak, drop) = tapered ? (ConePeak, ConeDrop) : (CylinderPeak, CylinderDrop);
            var color = Fill.MainColor(fill) ?? RgbColor.Black;
            var id = paint.NewId("cyl");
            defs.Append(columns
                ? $"<linearGradient id=\"{id}\" gradientUnits=\"userSpaceOnUse\" x1=\"{Px(lo)}\" y1=\"0\" x2=\"{Px(hi)}\" y2=\"0\">"
                : $"<linearGradient id=\"{id}\" gradientUnits=\"userSpaceOnUse\" x1=\"0\" y1=\"{Px(lo)}\" x2=\"0\" y2=\"{Px(hi)}\">");
            for (var k = 0; k <= 16; k++)
            {
                var t = k / 8.0 - 1;
                var shade = peak - drop * t * t;
                var c = new RgbColor(Math.Min(1, color.R * shade), Math.Min(1, color.G * shade), Math.Min(1, color.B * shade), color.A);
                defs.Append($"<stop offset=\"{Units.Css4(k / 16.0)}\" stop-color=\"{c.ToHex()}\"{(c.A < 0.999 ? $" stop-opacity=\"{Units.Css4(c.A)}\"" : "")}/>");
            }
            defs.Append("</linearGradient>");
            svg.Append($"<path d=\"{PathOf(hull)}\" fill=\"url(#{id})\"{stroke ?? ""}/>");
            if (capOpen)
            {
                var cap = Shaded(fill, columns ? TopShade : SideShade);
                svg.Append($"<path d=\"{PathOf(capEnd)}\"{paint.Fill(cap, width * PxPerPt, height * PxPerPt, defs)}{stroke ?? Seam(cap)}/>");
            }
            return svg.ToString();
        }

        // Pyramids (and cut pyramids): the faces towards the viewer, flat shaded like a box.
        string Face(IEnumerable<(double X, double Y)> points, double shade)
        {
            var f = Shaded(fill, shade);
            var face = $"<path d=\"{PathOf(points)}\"{paint.Fill(f, width * PxPerPt, height * PxPerPt, defs)}{stroke ?? Seam(f)}/>";
            if (shade < 1 && f is not SolidFill)
                face += $"<path d=\"{PathOf(points)}\" fill=\"#000\" fill-opacity=\"{Units.Css4(1 - shade)}\"/>";
            return face;
        }
        // Section corners: 0 (low, front), 1 (high, front), 2 (high, back), 3 (low, back) across × depth. The faces
        // between them are seen when their (tilted) outward normal points towards the viewer.
        (double X, double Y, double Z) Model(double a, double across, double depth) => columns ? (across, a, depth) : (a, across, depth);
        (double A, double C, double Z)[] Corners(double a)
        {
            var f = Radius(a);
            return [(a, cc - rc * f, cz - rz * f), (a, cc + rc * f, cz - rz * f), (a, cc + rc * f, cz + rz * f), (a, cc - rc * f, cz + rz * f)];
        }
        var (low, high) = (Corners(a0), Corners(a1));
        foreach (var (i, j, shade) in new[] { (1, 2, columns ? SideShade : TopShade), (3, 0, columns ? SideShade : TopShade), (2, 3, 1.0), (0, 1, 1.0) })
        {
            var (p0, p1, p2) = (Model(low[i].A, low[i].C, low[i].Z), Model(low[j].A, low[j].C, low[j].Z), Model(high[j].A, high[j].C, high[j].Z));
            var (e1, e2) = ((p1.X - p0.X, p1.Y - p0.Y, p1.Z - p0.Z), (p2.X - p0.X, p2.Y - p0.Y, p2.Z - p0.Z));
            var normal = (e1.Item2 * e2.Item3 - e1.Item3 * e2.Item2, e1.Item3 * e2.Item1 - e1.Item1 * e2.Item3, e1.Item1 * e2.Item2 - e1.Item2 * e2.Item1);
            var centre = Model((a0 + a1) / 2, cc, cz);
            // The normal of the quad (low i, low j, high j) points outwards for corners in this order.
            var outward = (normal.Item1 * (p0.X - centre.X) + normal.Item2 * (p0.Y - centre.Y) + normal.Item3 * (p0.Z - centre.Z)) >= 0 ? normal : (-normal.Item1, -normal.Item2, -normal.Item3);
            if (!projection.Faces(p0, outward))
                continue;
            svg.Append(Face([bottom[i], bottom[j], top[j], top[i]], shade));
        }
        if (capOpen)
            svg.Append(Face(capEnd, columns ? TopShade : SideShade));
        return svg.ToString();
    }

    /// <summary>The convex hull of points (monotone chain), counterclockwise in screen coordinates.</summary>
    static List<(double X, double Y)> ConvexHull(List<(double X, double Y)> points)
    {
        var sorted = points.Distinct().OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
        if (sorted.Count < 3)
            return sorted;
        static double Cross((double X, double Y) o, (double X, double Y) a, (double X, double Y) b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        var hull = new List<(double X, double Y)>();
        foreach (var pass in new[] { sorted, Enumerable.Reverse(sorted).ToList() })
        {
            var start = hull.Count;
            foreach (var p in pass)
            {
                while (hull.Count >= start + 2 && Cross(hull[^2], hull[^1], p) <= 0)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            hull.RemoveAt(hull.Count - 1);
        }
        return hull;
    }
}
