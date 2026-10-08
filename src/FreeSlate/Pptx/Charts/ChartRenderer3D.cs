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
    /// The oblique projection of right-angle axes: a point (x, y, z) of the box (y up, z into the screen) lies at
    /// (x + z sin rotY, y + z sin rotX), scaled and placed to fit the plot area; returns points.
    /// </summary>
    sealed class Oblique(double sx, double sy)
    {
        public double Sx { get; } = sx;
        public double Sy { get; } = sy;
        public double Scale { get; set; } = 1;
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }

        public (double X, double Y) Map(double x, double y, double z) => (OffsetX + (x + z * Sx) * Scale, OffsetY - (y + z * Sy) * Scale);

        /// <summary>Scales and centers the projected box (w × h × d) in the plot box.</summary>
        public void Fit(double w, double h, double d, Box plot)
        {
            var (u0, u1) = (Math.Min(0, d * Sx), w + Math.Max(0, d * Sx));
            var (v0, v1) = (Math.Min(0, d * Sy), h + Math.Max(0, d * Sy));
            Scale = Math.Min(plot.W / Math.Max(1e-9, u1 - u0), plot.H / Math.Max(1e-9, v1 - v0));
            OffsetX = plot.X + (plot.W - (u1 - u0) * Scale) / 2 - u0 * Scale;
            OffsetY = plot.Y + (plot.H - (v1 - v0) * Scale) / 2 + v1 * Scale;
        }
    }

    /// <summary>The 3D chart of the plot area, or null when it is not one this renderer draws in 3D.</summary>
    string? Chart3D(List<ChartGroup> groups, Box outer)
    {
        var group = groups.FirstOrDefault(g => g.Type.EndsWith("3DChart", StringComparison.Ordinal));
        if (group == null)
            return null;
        var view = View3D.Read(model.Chart, group.IsPie);
        if (group.IsBar && view.RightAngleAxes)
            return Bars3D(group, outer, view);
        if (group.Type == "pie3DChart")
            return Pie3D(group, outer, view);
        return null;
    }

    // Faces of 3D bars (measured, independent of the rotation): tops 0.757 and sides 0.639 of the front's color.
    const double TopShade = 0.757, SideShade = 0.639;

    /// <summary>
    /// A 3D bar chart with right-angle axes. Bars are boxes: clustered and stacked bars stand in one row whose depth
    /// is depthPercent × (1 + gapDepth) bar widths, each bar depthPercent × its width deep and centered in it;
    /// standard charts give every series a row of depthPercent × the category width. The box is as tall as the
    /// plot area's height to width ratio times its width (times ⌊(categories + series) / 2⌋ category widths for
    /// standard charts, measured on 22 combinations); it is scaled to fit and centered.
    /// </summary>
    string Bars3D(ChartGroup group, Box outer, View3D view)
    {
        var categoryAxis = model.Axes.FirstOrDefault(a => a.Kind is "catAx" or "dateAx" && group.AxisIds.Contains(a.Id));
        var valueAxis = model.Axes.FirstOrDefault(a => a.IsValue && group.AxisIds.Contains(a.Id));
        var seriesAxis = model.Axes.FirstOrDefault(a => a.Kind == "serAx" && group.AxisIds.Contains(a.Id));
        if (categoryAxis == null || valueAxis == null || group.Series.Count == 0)
            return "";
        var series = group.Series;
        var standard = group.Grouping == "standard";
        var columns = !group.Horizontal;
        var nCat = Math.Max(1, series.Max(s => Math.Max(s.Values.Count, s.Categories.Count)));
        var nSer = series.Count;
        var gap = group.GapWidth / 100;
        var gapDepth = (ChartModel.Number(group.Element, "gapDepth") ?? 150) / 100;
        var depth = view.DepthPercent / 100;

        var category = new AxisLayout { Axis = categoryAxis, Groups = [group], Style = Style(categoryAxis.TxPr, "categoryAxis") };
        var names = series.Select(s => s.Categories).FirstOrDefault(c => c.Count > 0) ?? [];
        category.Categories = nCat;
        category.Labels = Enumerable.Range(0, nCat).Select(i => i < names.Count ? names[i] : (i + 1).ToString(culture)).ToList();
        category.MaxLabelWidth = category.Labels.Max(l => Width(l, category.Style));
        var value = new AxisLayout { Axis = valueAxis, Groups = [group], Style = Style(valueAxis.TxPr, "valueAxis") };
        var seriesLayout = seriesAxis != null && standard
            ? new AxisLayout { Axis = seriesAxis, Groups = [group], Style = Style(seriesAxis.TxPr, "seriesAxis"), Labels = series.Select(s => s.Name).ToList() }
            : null;

        // The box: category axis length 1 (width of columns, height of bars), values along the other side.
        var c = 1.0 / nCat;
        var barWidth = group.Stacked || standard ? c / (1 + gap) : c / (nSer + gap);
        var rowDepth = standard ? depth * c : depth * (1 + gapDepth) * barWidth;
        var d = standard ? nSer * rowDepth : rowDepth;
        var projection = new Oblique(Math.Sin(view.RotY * Math.PI / 180), Math.Sin(view.RotX * Math.PI / 180));

        double w = 1, h = 1;
        var plot = outer;
        for (var pass = 0; pass < 3; pass++)
        {
            // The labels take their room from the plot area like on flat charts; the box fills the rest.
            plot = Plot3D(outer, columns ? value : category, columns ? category : value, seriesLayout);
            var ratio = view.HeightPercent is { } hp ? hp / 100 : plot.H / Math.Max(1, plot.W);
            if (columns)
                (w, h) = (1, standard ? ratio * c * Math.Floor((nCat + nSer) / 2.0) : ratio);
            else
                (w, h) = (1 / Math.Max(1e-9, ratio), 1);
            projection.Fit(w, h, d, plot);
            var length = (columns ? h : w) * projection.Scale;
            ScaleValueAxis3D(value, length);
            // Category labels that do not fit under their categories turn by 45° like on flat charts.
            if (columns && category.HasLabels)
                category.Rotation = Rotation(category, w * projection.Scale);
        }

        // Positions in the box: categories along x (columns) or y (bars), values along the other axis.
        double ValuePos(double v) => value.Scale.Fraction(Math.Clamp(v, value.Scale.Min, value.Scale.Max)) * (columns ? h : w);
        double CategoryPos(double t) => (categoryAxis.Reversed ? 1 - t : t) * (columns ? w : h);
        (double X, double Y, double Z) Point(double along, double across, double z) => columns ? (along, across, z) : (across, along, z);

        var svg = new StringBuilder();
        var sideX = projection.Sx >= 0 ? 0 : w;
        var floorY = projection.Sy >= 0 ? 0 : h;
        string Quad(params (double X, double Y, double Z)[] corners) =>
            string.Concat(corners.Select((p, i) => (i == 0 ? "M" : "L") + XY(projection.Map(p.X, p.Y, p.Z)))) + "Z";
        // The walls are shaded like the faces of a box (measured): the back wall like a front, the side wall like a
        // side, the floor like a top.
        svg.Append(Wall(model.Chart.Element(Ns.C + "backWall"), Quad((0, 0, d), (w, 0, d), (w, h, d), (0, h, d)), 1));
        svg.Append(Wall(model.Chart.Element(Ns.C + "sideWall"), Quad((sideX, 0, 0), (sideX, 0, d), (sideX, h, d), (sideX, h, 0)), SideShade));
        svg.Append(Wall(model.Chart.Element(Ns.C + "floor"), Quad((0, floorY, 0), (w, floorY, 0), (w, floorY, d), (0, floorY, d)), TopShade));

        // Gridlines on the back wall and on the wall or floor beside the value axis.
        string Line(Outline outline, (double X, double Y, double Z) a, (double X, double Y, double Z) b, (double X, double Y, double Z) e) =>
            $"<path d=\"M{XY(projection.Map(a.X, a.Y, a.Z))}L{XY(projection.Map(b.X, b.Y, b.Z))}L{XY(projection.Map(e.X, e.Y, e.Z))}\" fill=\"none\"{paint.Stroke(outline, 0, 0, defs)}/>";
        foreach (var (element, minor) in new[] { (valueAxis.MinorGridlines, true), (valueAxis.MajorGridlines, false) })
        {
            if (element == null)
                continue;
            var outline = GridOutline(element);
            foreach (var t in minor ? value.Scale.MinorTicks() : value.Scale.Ticks())
            {
                var p = ValuePos(t);
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
                var p = CategoryPos(i * c);
                svg.Append(columns
                    ? Line(outline, (p, floorY, 0), (p, floorY, d), (p, floorY == 0 ? h : 0, d))
                    : Line(outline, (sideX, p, 0), (sideX, p, d), (sideX == 0 ? w : 0, p, d)));
            }
        }

        // The bars, from the back, then in the direction the depth runs, then from the bottom.
        var cross = CrossValue(value, categoryAxis);
        var groupShape = ChartModel.Val(group.Element, "shape") ?? "box";
        var bars = new List<(double X0, double Y0, double Z0, double X1, double Y1, double Z1, Fill Fill, Outline? Outline, string Shape, double Base, double Apex)>();
        for (var i = 0; i < nCat; i++)
        {
            double positive = 0, negative = 0;
            var total = group.PercentStacked ? series.Sum(s => Math.Abs(s.Values.ElementAtOrDefault(i) ?? 0)) : 1;
            // A stack of cones or pyramids tapers over the whole stack.
            var stackTop = group.Stacked ? series.Sum(s => Math.Max(0, s.Values.ElementAtOrDefault(i) ?? 0)) / (group.PercentStacked && total != 0 ? total : 1) : 0;
            for (var k = 0; k < nSer; k++)
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
                var slot = group.Stacked || standard ? 0 : k;
                var a0 = CategoryPos(i * c + gap * barWidth / 2 + slot * barWidth);
                var a1 = CategoryPos(i * c + gap * barWidth / 2 + (slot + 1) * barWidth);
                var zCenter = standard ? (k + 0.5) * rowDepth : d / 2;
                var half = (standard ? rowDepth / (1 + gapDepth) : depth * barWidth) / 2;
                var (v0, v1) = (ValuePos(Math.Min(from, to)), ValuePos(Math.Max(from, to)));
                var (p0, p1) = (Point(Math.Min(a0, a1), v0, zCenter - half), Point(Math.Max(a0, a1), v1, zCenter + half));
                var shape = ChartModel.Val(series[k].Element, "shape") ?? groupShape;
                // Cones and pyramids end in a point: at the bar's end, the stack's end or the axis maximum (…ToMax).
                var (solidBase, apex) = shape switch
                {
                    "coneToMax" or "pyramidToMax" => (ValuePos(cross), ValuePos(value.Scale.Max)),
                    _ when group.Stacked => (ValuePos(0), ValuePos(stackTop)),
                    _ => (ValuePos(from), ValuePos(to)),
                };
                bars.Add((p0.X, p0.Y, p0.Z, p1.X, p1.Y, p1.Z, PointFill(group, series[k], i), PointOutline(series[k], i), shape, solidBase, apex));
            }
        }
        var ordered = bars.OrderByDescending(b => b.Z0)
            .ThenBy(b => projection.Sx >= 0 ? b.X0 : -b.X1)
            .ThenBy(b => projection.Sy >= 0 ? b.Y0 : -b.Y1);
        foreach (var bar in ordered)
        {
            svg.Append(bar.Shape switch
            {
                "cylinder" or "cone" or "coneToMax" or "pyramid" or "pyramidToMax" =>
                    Solid3D(projection, bar.X0, bar.Y0, bar.Z0, bar.X1, bar.Y1, bar.Z1, bar.Fill, bar.Outline, bar.Shape, columns, bar.Base, bar.Apex),
                _ => Box3D(projection, bar.X0, bar.Y0, bar.Z0, bar.X1, bar.Y1, bar.Z1, bar.Fill, bar.Outline),
            });
        }

        // Axis lines along the front edges of the side wall and the floor.
        if (AxisOutline(valueAxis) is { Visible: true } valueLine && !valueAxis.Deleted)
            svg.Append(columns ? Line(valueLine, (sideX, 0, 0), (sideX, h, 0), (sideX, h, 0)) : Line(valueLine, (0, floorY, 0), (w, floorY, 0), (w, floorY, 0)));
        if (AxisOutline(categoryAxis) is { Visible: true } categoryLine && !categoryAxis.Deleted)
            svg.Append(columns ? Line(categoryLine, (0, floorY, 0), (w, floorY, 0), (w, floorY, 0)) : Line(categoryLine, (sideX, 0, 0), (sideX, h, 0), (sideX, h, 0)));

        // Labels next to the front edges: values beside the side wall (columns) or under the floor (bars),
        // categories under the floor (columns) or beside the side wall (bars), series along the floor's far side.
        var valuePositions = value.Scale.Ticks().Select(ValuePos).ToList();
        if (value.HasLabels)
        {
            for (var i = 0; i < value.Labels.Count && i < valuePositions.Count; i++)
                svg.Append(columns ? SideLabel(value, value.Labels[i], projection.Map(sideX, valuePositions[i], 0)) : FloorLabel(value, value.Labels[i], projection.Map(valuePositions[i], floorY, 0)));
        }
        if (category.HasLabels)
        {
            for (var i = 0; i < nCat; i++)
            {
                var p = CategoryPos((i + 0.5) * c);
                svg.Append(columns ? FloorLabel(category, category.Labels[i], projection.Map(p, floorY, 0)) : SideLabel(category, category.Labels[i], projection.Map(sideX, p, 0)));
            }
        }
        if (seriesLayout is { HasLabels: true })
        {
            // Labels closer than a line apart show every second (third …) one, starting with the first.
            var spacing = Math.Abs(rowDepth * projection.Sy * projection.Scale);
            var lineHeight = LineHeight(seriesLayout.Style);
            var step = spacing < lineHeight && spacing > 1e-6 ? (int)Math.Ceiling(lineHeight / spacing) : 1;
            for (var k = 0; k < nSer; k += step)
            {
                var (x, y) = projection.Map(sideX == 0 ? w : 0, floorY, (k + 0.5) * rowDepth);
                var label = seriesLayout.Labels[k];
                var s = seriesLayout.Style.Size;
                var left = sideX == 0 ? x + ValueLabelGap * s : x - ValueLabelGap * s - Width(label, seriesLayout.Style);
                svg.Append(text.Label(label, seriesLayout.Style, left, CenteredBaseline(seriesLayout.Style, y)));
            }
        }
        return svg.ToString();
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
        var fill = FillReader.FindFill(spPr) is { } f ? FillReader.Read(f, colors) : Fill.None;
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
    string Box3D(Oblique projection, double x0, double y0, double z0, double x1, double y1, double z1, Fill fill, Outline? outline)
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
        var sideX = projection.Sx >= 0 ? x1 : x0;
        if (Math.Abs(projection.Sx) > 1e-9)
            svg.Append(Face([(sideX, y0, z0), (sideX, y0, z1), (sideX, y1, z1), (sideX, y1, z0)], SideShade));
        var capY = projection.Sy >= 0 ? y1 : y0;
        if (Math.Abs(projection.Sy) > 1e-9)
            svg.Append(Face([(x0, capY, z0), (x1, capY, z0), (x1, capY, z1), (x0, capY, z1)], TopShade));
        svg.Append(Face([(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0)], 1));
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
    string Solid3D(Oblique projection, double x0, double y0, double z0, double x1, double y1, double z1, Fill fill, Outline? outline,
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
        var capAtTop = columns ? projection.Sy >= 0 : projection.Sx >= 0;
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
        // Section corners: 0 (low, front), 1 (high, front), 2 (high, back), 3 (low, back) across × depth.
        var (s0, s1) = columns ? (projection.Sx >= 0 ? (1, 2) : (0, 3)) : (projection.Sy >= 0 ? (1, 2) : (0, 3));
        svg.Append(Face([bottom[s0], bottom[s1], top[s1], top[s0]], columns ? SideShade : TopShade));
        if (capOpen)
            svg.Append(Face(capEnd, columns ? TopShade : SideShade));
        svg.Append(Face([bottom[0], bottom[1], top[1], top[0]], 1));
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
