using System.Runtime.Versioning;

namespace Fidelity.PowerPoint;

/// <summary>
/// 3D charts (catalog: chart.threeD) as PowerPoint builds them by default: clustered, stacked and 100 % stacked
/// columns, columns with the series in depth, bars, pies, areas, lines, surfaces, column shapes (cylinder, cone,
/// pyramid), views with other rotations, perspective and right-angle axes, and data labels.
/// </summary>
[SupportedOSPlatform("windows")]
static class Charts3DDeck
{
    // XlChartType
    const int Column3DClustered = 54, Column3DStacked = 55, Column3DStacked100 = 56, Column3D = -4100, Bar3DClustered = 60,
        Bar3DStacked = 61, Pie3D = -4102, Pie3DExploded = 70, Area3D = -4098, Area3DStacked = 78, Line3D = -4101,
        Surface = 83, SurfaceWireframe = 84, CylinderClustered = 98, ConeClustered = 105, PyramidClustered = 112;

    public static void Build(dynamic deck)
    {
        object presentation = deck;
        Charts.Chart(presentation, "3D clustered column", Column3DClustered);
        Charts.Chart(presentation, "3D stacked column", Column3DStacked);
        Charts.Chart(presentation, "3D 100 % stacked column", Column3DStacked100);
        Charts.Chart(presentation, "3D column", Column3D);
        Charts.Chart(presentation, "3D clustered bar", Bar3DClustered);
        Charts.Chart(presentation, "3D stacked bar", Bar3DStacked);
        Charts.Chart(presentation, "3D pie", Pie3D);
        Charts.Chart(presentation, "3D exploded pie", Pie3DExploded);
        Charts.Chart(presentation, "3D area", Area3D);
        Charts.Chart(presentation, "3D stacked area", Area3DStacked);
        Charts.Chart(presentation, "3D line", Line3D);
        Charts.Chart(presentation, "3D surface", Surface);
        Charts.Chart(presentation, "Wireframe surface", SurfaceWireframe);

        object shapes = Charts.Slide(presentation, "Cylinder, cone, pyramid");
        Charts.Add(shapes, CylinderClustered, 20, 120, 300, 380);
        Charts.Add(shapes, ConeClustered, 330, 120, 300, 380);
        Charts.Add(shapes, PyramidClustered, 640, 120, 300, 380);

        Charts.Chart(presentation, "Rotated, perspective", Column3DClustered, c =>
        {
            c.RightAngleAxes = false;
            c.Rotation = 40;
            c.Elevation = 25;
            c.Perspective = 60;
        });
        Charts.Chart(presentation, "Rotated, right-angle axes", Column3D, c =>
        {
            c.RightAngleAxes = true;
            c.Rotation = 30;
            c.Elevation = 20;
        });
        Charts.Chart(presentation, "Pie, elevation 60", Pie3D, c =>
        {
            c.Elevation = 60;
            c.Rotation = 90;
        });

        // Data labels on 3D charts: a pie (best fit), columns, bars and an area.
        object labelled = Charts.Slide(presentation, "Data labels");
        foreach (var (type, left, top) in new[] { (Pie3D, 20f, 120f), (Column3DClustered, 490f, 120f), (Bar3DClustered, 20f, 330f), (Area3D, 490f, 330f) })
            Charts.Add(labelled, type, left, top, 450, 200).ApplyDataLabels();
    }
}
