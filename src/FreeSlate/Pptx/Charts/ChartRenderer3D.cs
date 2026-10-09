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

    /// <summary>How the box of a 3D chart (x right, y up, z away from the viewer; 0…w, 0…h, 0…d) lands on the chart.</summary>
    abstract class Projection3D
    {
        /// <summary>Scales and places the projected box in the plot box.</summary>
        public abstract void Fit(double w, double h, double d, Box plot);

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

        public override void Fit(double w, double h, double d, Box plot)
        {
            var (u0, u1) = (Math.Min(0, d * Sx), w + Math.Max(0, d * Sx));
            var (v0, v1) = (Math.Min(0, d * Sy), h + Math.Max(0, d * Sy));
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

    /// <summary>The perspective view of charts without right-angle axes: the camera fits the box enlarged by 2.5 %.</summary>
    sealed class PerspectiveBox(View3D view) : Projection3D
    {
        readonly Perspective camera = new(view.RotX, view.RotY);
        double cx, cy, cz;

        public override void Fit(double w, double h, double d, Box plot)
        {
            (cx, cy, cz) = (w / 2, h / 2, d / 2);
            var volume = new List<(double X, double Y, double Z)>();
            foreach (var x in new[] { -cx, cx })
                foreach (var y in new[] { -cy, cy })
                    foreach (var z in new[] { -cz, cz })
                        volume.Add(camera.View(x * 1.025, y * 1.025, z * 1.025));
            camera.Fit(volume, plot, view.Perspective / 2);
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

        public override bool NearerBelow(int axis, double plane)
        {
            var eye = camera.ToModel(camera.Eye);
            return (axis switch { 0 => eye.X + cx, 1 => eye.Y + cy, _ => eye.Z + cz }) < plane;
        }

        public override (bool FromRight, bool FromAbove) Side
        {
            get
            {
                var eye = camera.ToModel(camera.Eye);
                return (eye.X > 0, eye.Y > 0);
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

        var plot = outer;
        for (var pass = 0; pass < 3; pass++)
        {
            // The labels take their room from the plot area like on flat charts; the box fills the rest.
            plot = Plot3D(outer, columns ? value : category, columns ? category : value, seriesLayout);
            var ratio = view.HeightPercent is { } hp ? hp / 100 : plot.H / Math.Max(1, plot.W);
            if (columns)
                (frame.W, frame.H) = (1, rows ? ratio * c * Math.Floor((nCat + nSer) / 2.0) : ratio);
            else
                (frame.W, frame.H) = (1 / Math.Max(1e-9, ratio), 1);
            projection.Fit(frame.W, frame.H, d, plot);
            // The value scale follows the box's edge halfway back (in perspective the front edge looks longer).
            var (edgeX, edgeY) = (frame.SideX, frame.FloorY);
            var length = columns ? projection.Length((edgeX, 0, d / 2), (edgeX, frame.H, d / 2)) : projection.Length((0, edgeY, d / 2), (frame.W, edgeY, d / 2));
            ScaleValueAxis3D(value, length);
            // Category labels that do not fit under their categories turn by 45° like on flat charts.
            if (columns && category.HasLabels)
                category.Rotation = Rotation(category, projection.Length((0, edgeY, 0), (frame.W, edgeY, 0)));
        }
        var (w, h) = (frame.W, frame.H);
        var sideX = frame.SideX;
        var floorY = frame.FloorY;

        var svg = new StringBuilder();
        string Quad(params (double X, double Y, double Z)[] corners) =>
            string.Concat(corners.Select((p, i) => (i == 0 ? "M" : "L") + XY(projection.Map(p.X, p.Y, p.Z)))) + "Z";
        // The walls are lit like the faces of a box: the back wall like a front, the side wall like a side, the
        // floor like a top.
        svg.Append(Wall(model.Chart.Element(Ns.C + "backWall"), Quad((0, 0, d), (w, 0, d), (w, h, d), (0, h, d)), 1));
        svg.Append(Wall(model.Chart.Element(Ns.C + "sideWall"), Quad((sideX, 0, 0), (sideX, 0, d), (sideX, h, d), (sideX, h, 0)), SideShade));
        svg.Append(Wall(model.Chart.Element(Ns.C + "floor"), Quad((0, floorY, 0), (w, floorY, 0), (w, floorY, d), (0, floorY, d)), TopShade));

        // Gridlines on the back wall and on the wall or floor beside the value axis.
        string Line(Outline outline, params (double X, double Y, double Z)[] points) =>
            $"<path d=\"{string.Concat(points.Select((p, i) => (i == 0 ? "M" : "L") + XY(projection.Map(p.X, p.Y, p.Z))))}\" fill=\"none\"{paint.Stroke(outline, 0, 0, defs)}/>";
        foreach (var (element, minor) in new[] { (valueAxis.MinorGridlines, true), (valueAxis.MajorGridlines, false) })
        {
            if (element == null)
                continue;
            var outline = GridOutline(element);
            foreach (var t in minor ? value.Scale.MinorTicks() : value.Scale.Ticks())
            {
                var p = frame.ValuePos(t);
                svg.Append(columns
                    ? Line(outline, (sideX, p, 0), (sideX, p, d), (sideX == 0 ? w : 0, p, d))
                    : Line(outline, (p, floorY, 0), (p, floorY, d), (p, floorY == 0 ? h : 0, d)));
            }
        }
        if (categoryAxis.MajorGridlines is { } categoryGrid)
        {
            var outline = GridOutline(categoryGrid);
            for (var i = 0; i <= nCat; i++)
            {
                var p = frame.CategoryPos(frame.Between ? i * c : Math.Min(i, nCat - 1) / (double)Math.Max(1, nCat - 1));
                svg.Append(columns
                    ? Line(outline, (p, floorY, 0), (p, floorY, d), (p, floorY == 0 ? h : 0, d))
                    : Line(outline, (sideX, p, 0), (sideX, p, d), (sideX == 0 ? w : 0, p, d)));
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
            svg.Append(columns ? Line(valueLine, (sideX, 0, 0), (sideX, h, 0)) : Line(valueLine, (0, floorY, 0), (w, floorY, 0)));
        if (categoryOutline is { Visible: true } && !categoryAxis.Deleted)
        {
            svg.Append(columns ? Line(categoryOutline, (0, floorY, 0), (w, floorY, 0)) : Line(categoryOutline, (sideX, 0, 0), (sideX, h, 0)));
            if (categoryAxis.MajorTickMark is "out" or "cross" && columns)
            {
                // Ticks run 5 px down from the floor's front edge at the category boundaries (or points).
                for (var i = 0; i <= (frame.Between ? nCat : nCat - 1); i++)
                {
                    var p = frame.CategoryPos(frame.Between ? i * c : i / (double)Math.Max(1, nCat - 1));
                    var (x, y) = projection.Map(p, floorY, 0);
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
                svg.Append(columns ? SideLabel(value, value.Labels[i], projection.Map(sideX, valuePositions[i], 0)) : FloorLabel(value, value.Labels[i], projection.Map(valuePositions[i], floorY, 0)));
        }
        if (category.HasLabels)
        {
            var tickRoom = categoryOutline is { Visible: true } && categoryAxis.MajorTickMark is "out" or "cross" && !categoryAxis.Deleted ? TickLength : 0;
            for (var i = 0; i < nCat; i++)
            {
                var p = frame.CategoryCenter(i);
                var at = columns ? projection.Map(p, floorY, 0) : projection.Map(sideX, p, 0);
                svg.Append(columns ? FloorLabel(category, category.Labels[i], (at.X, at.Y + tickRoom)) : SideLabel(category, category.Labels[i], at));
            }
        }
        if (seriesLayout is { HasLabels: true })
        {
            // Labels closer than a line apart show every second (third …) one, starting with the first.
            var edge = sideX == 0 ? w : 0;
            var spacing = nSer > 1 ? Math.Abs(projection.Map(edge, floorY, frame.SeriesPos(0)).Y - projection.Map(edge, floorY, frame.SeriesPos(1)).Y) : 0;
            var lineHeight = LineHeight(seriesLayout.Style);
            var step = nSer > 1 && spacing < lineHeight && spacing > 1e-6 ? (int)Math.Ceiling(lineHeight / spacing) : 1;
            for (var k = 0; k < nSer; k += step)
            {
                var (x, y) = projection.Map(edge, floorY, frame.SeriesPos(k));
                var label = seriesLayout.Labels[k];
                var s = seriesLayout.Style.Size;
                var left = sideX == 0 ? x + ValueLabelGap * s : x - ValueLabelGap * s - Width(label, seriesLayout.Style);
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
        var bars = new List<(double X0, double Y0, double Z0, double X1, double Y1, double Z1, Fill Fill, Outline? Outline, string Shape, double Base, double Apex)>();
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
                bars.Add((p0.X, p0.Y, p0.Z, p1.X, p1.Y, p1.Z, PointFill(group, series[k], i), PointOutline(series[k], i), shape, solidBase, apex));
            }
        }
        var ordered = projection is Oblique oblique
            ? bars.OrderByDescending(b => b.Z0).ThenBy(b => oblique.Sx >= 0 ? b.X0 : -b.X1).ThenBy(b => oblique.Sy >= 0 ? b.Y0 : -b.Y1).ToList()
            : BackToFront(bars, b => (b.X0, b.Y0, b.Z0, b.X1, b.Y1, b.Z1), projection);
        var svg = new StringBuilder();
        foreach (var bar in ordered)
        {
            svg.Append(bar.Shape switch
            {
                "cylinder" or "cone" or "coneToMax" or "pyramid" or "pyramidToMax" =>
                    Solid3D(projection, bar.X0, bar.Y0, bar.Z0, bar.X1, bar.Y1, bar.Z1, bar.Fill, bar.Outline, bar.Shape, frame.Columns, bar.Base, bar.Apex),
                _ => Box3D(projection, bar.X0, bar.Y0, bar.Z0, bar.X1, bar.Y1, bar.Z1, bar.Fill, bar.Outline),
            });
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
            var faces = new List<(double Depth, (double X, double Y, double Z)[] Corners, double Shade)>();
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
                    faces.Add((depthOf, top, Shade(nx, ny, 0)));
                else
                    faces.Add((depthOf, under, Shade(-nx, -ny, 0)));
                faces.Add((depthOf - 1e-6, front, Shade(0, 0, -1)));
            }
            foreach (var face in faces.OrderByDescending(f => f.Depth))
                svg.Append(Facet(projection, face.Corners, fill, outline, face.Shade));
        }
        return svg.ToString();
    }

    /// <summary>
    /// The surface of a 3D surface chart: the grid of categories (left to right, on their ticks) and series (front to
    /// back, on theirs) at the values' heights, cut into triangles along each cell's diagonal with the larger sum and
    /// every triangle into the value axis' bands, each piece in its band's fill lit for the triangle's slope (the
    /// light of flat surface charts); a wireframe draws the grid lines in the bands' lines instead.
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

        // The light of flat surface charts, turned into the box: from the upper right at 31.1° elevation.
        var elevation = 31.1 * Math.PI / 180;
        var light = (X: Math.Cos(elevation) * Math.Sqrt(0.5), Y: Math.Sin(elevation), Z: Math.Cos(elevation) * Math.Sqrt(0.5));
        var halfLength = Math.Sqrt(light.X * light.X + (light.Y + 1) * (light.Y + 1) + light.Z * light.Z);
        var half = (X: light.X / halfLength, Y: (light.Y + 1) / halfLength, Z: light.Z / halfLength);

        var triangles = new List<(double Depth, (double X, double Z, double V)[] Corners)>();
        for (var k = 0; k + 1 < nSer; k++)
        {
            for (var i = 0; i + 1 < nCat; i++)
            {
                var a = (X(i), Z(k), Value(k, i));
                var b = (X(i + 1), Z(k), Value(k, i + 1));
                var c = (X(i), Z(k + 1), Value(k + 1, i));
                var e = (X(i + 1), Z(k + 1), Value(k + 1, i + 1));
                (double X, double Z, double V)[][] pair = a.Item3 + e.Item3 >= c.Item3 + b.Item3 ? [[a, b, e], [a, e, c]] : [[c, a, b], [c, b, e]];
                foreach (var t in pair)
                    triangles.Add((projection.Depth(t.Average(p => p.X), Height(t.Average(p => p.V)), t.Average(p => p.Z)), t));
            }
        }

        var svg = new StringBuilder();
        if (wireframe)
        {
            // Grid lines along the categories and along the series, each piece in the line of its band.
            void Edge((double X, double Z, double V) p, (double X, double Z, double V) q)
            {
                for (var band = 0; band < bands.Count; band++)
                {
                    var (low, high) = bands[band];
                    double t0 = 0, t1 = 1;
                    if (Math.Abs(q.V - p.V) < 1e-12)
                    {
                        var inside = (band == 0 || p.V >= low) && (band == bands.Count - 1 || p.V <= high);
                        if (!inside) continue;
                    }
                    else
                    {
                        var (lo, hi) = ((low - p.V) / (q.V - p.V), (high - p.V) / (q.V - p.V));
                        if (lo > hi) (lo, hi) = (hi, lo);
                        if (band == 0) lo = double.NegativeInfinity;
                        if (band == bands.Count - 1) hi = double.PositiveInfinity;
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
            // The contour lines where the surface crosses a band boundary, in the line of the band above.
            foreach (var (_, corners) in triangles)
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
                    var line = BandLine(group, band) ?? new Outline(9525, fills[band], "solid", "flat", "round", 8, "sng");
                    var (u, v) = (projection.Map(crossing[0].X, Height(level), crossing[0].Z), projection.Map(crossing[1].X, Height(level), crossing[1].Z));
                    svg.Append($"<path d=\"M{XY(u)}L{XY(v)}\" fill=\"none\"{paint.Stroke(line, 0, 0, defs)}/>");
                }
            }
            return svg.ToString();
        }

        foreach (var (_, corners) in triangles.OrderByDescending(t => t.Depth))
        {
            // The facet's normal in the box (y up): its value gradient over the floor.
            var p3 = corners.Select(p => (p.X, Y: Height(p.V), p.Z)).ToArray();
            var (u1, u2) = ((p3[1].X - p3[0].X, p3[1].Y - p3[0].Y, p3[1].Z - p3[0].Z), (p3[2].X - p3[0].X, p3[2].Y - p3[0].Y, p3[2].Z - p3[0].Z));
            var n = (u1.Item2 * u2.Item3 - u1.Item3 * u2.Item2, u1.Item3 * u2.Item1 - u1.Item1 * u2.Item3, u1.Item1 * u2.Item2 - u1.Item2 * u2.Item1);
            if (n.Item2 < 0) n = (-n.Item1, -n.Item2, -n.Item3);
            var length = Math.Sqrt(n.Item1 * n.Item1 + n.Item2 * n.Item2 + n.Item3 * n.Item3);
            if (length < 1e-12)
                continue;
            n = (n.Item1 / length, n.Item2 / length, n.Item3 / length);
            var diffuse = 0.52 + 0.595 * Math.Max(0, n.Item1 * light.X + n.Item2 * light.Y + n.Item3 * light.Z);
            var specular = Math.Min(0.15, 0.278 * Math.Pow(Math.Max(0, n.Item1 * half.X + n.Item2 * half.Y + n.Item3 * half.Z), 26.8));
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
                    static double Channel(double v, double diffuse, double specular) => Math.Clamp(v * diffuse + specular, 0, 1);
                    var lit = new RgbColor(Channel(solid.Color.R, diffuse, specular), Channel(solid.Color.G, diffuse, specular), Channel(solid.Color.B, diffuse, specular), solid.Color.A);
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

    /// <summary>The plot box the projected 3D box fits into: the outer plot area less the room of the axis labels.</summary>
    Box Plot3D(Box outer, AxisLayout side, AxisLayout floor, AxisLayout? series)
    {
        double left = outer.X, top = outer.Y, right = outer.Right, bottom = outer.Bottom;
        if (side.HasLabels && side.Labels.Count > 0)
        {
            var widest = side.Labels.Max(l => Width(l, side.Style));
            left = Math.Max(left, outer.X - LabelBleed + widest + ValueLabelGap * side.Style.Size);
        }
        if (floor.HasLabels && floor.Labels.Count > 0)
            bottom = Math.Min(bottom, outer.Bottom + LabelBleed - LabelExtent(floor) - CategoryLabelGap * floor.Style.Size);
        if (series is { HasLabels: true } && series.Labels.Count > 0)
        {
            var widest = series.Labels.Max(l => Width(l, series.Style));
            right = Math.Min(right, outer.Right + LabelBleed - widest - ValueLabelGap * series.Style.Size);
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
        var extent = layout.Axis.Horizontal
            ? Math.Max(Width(NumberFormat.Format(max, Format(layout), culture), style), Width(NumberFormat.Format(min, Format(layout), culture), style)) + style.Size
            : LineHeight(style) * 1.1;
        var maxIntervals = Math.Clamp((int)Math.Floor(length / Math.Max(1, extent)), 1, 10);
        layout.Scale = AxisScaling.Compute(min, max, layout.Axis, maxIntervals, percent, headroom: false);
        layout.Labels = layout.Scale.Ticks().Select(v => NumberFormat.Format(v, Format(layout), culture)).ToList();
        layout.MaxLabelWidth = layout.Labels.Count == 0 ? 0 : layout.Labels.Max(l => Width(l, style));
    }

    /// <summary>A label right-aligned before a point on the side wall's front edge, centered on it.</summary>
    string SideLabel(AxisLayout layout, string label, (double X, double Y) at) =>
        text.Label(label, layout.Style, at.X - ValueLabelGap * layout.Style.Size - Width(label, layout.Style), CenteredBaseline(layout.Style, at.Y));

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
            if (shade < 1 && shaded is not SolidFill)
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
