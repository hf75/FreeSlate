using System.Xml.Linq;

namespace FreeSlate.Drawing.Geometry;

/// <summary>The preset shape and text warp geometries, loaded once from the embedded definition files.</summary>
public static class PresetGeometries
{
    static readonly Lazy<Dictionary<string, GeometryDefinition>> Shapes = new(() =>
    {
        var result = new Dictionary<string, GeometryDefinition>(StringComparer.Ordinal);
        foreach (var resource in new[] { "presetShapeDefinitions.xml", "presetShapeSupplement.xml" })
        {
            using var stream = typeof(PresetGeometries).Assembly.GetManifestResourceStream(resource)
                               ?? throw new InvalidOperationException($"Missing resource {resource}");
            foreach (var shape in XDocument.Load(stream).Root!.Elements())
            {
                var definition = GeometryDefinition.Parse(shape);
                definition.ShadeShape = !NoShadeShape.Contains(shape.Name.LocalName);
                result[shape.Name.LocalName] = definition;
            }
        }
        return result;
    });

    /// <summary>
    /// Presets whose path gradients cannot follow their outline: PowerPoint draws a gradient along the shape
    /// (path="shape") as a circle gradient (path="circle") instead (measured: every preset filled both ways, these
    /// drawn identically; the shape types' fFillShadeShapeOK flag of the binary format).
    /// </summary>
    static readonly HashSet<string> NoShadeShape =
    [
        "bentArrow", "bentUpArrow", "blockArc", "bracePair", "chevron", "circularArrow", "cloudCallout", "cornerTabs",
        "curvedDownArrow", "curvedLeftArrow", "curvedRightArrow", "curvedUpArrow", "donut", "doubleWave", "downArrow",
        "downArrowCallout", "ellipseRibbon", "ellipseRibbon2", "flowChartDisplay", "flowChartDocument", "flowChartMagneticDisk",
        "flowChartMultidocument", "flowChartPunchedTape", "funnel", "horizontalScroll", "leftArrow", "leftArrowCallout",
        "leftBrace", "leftCircularArrow", "leftRightArrow", "leftRightArrowCallout", "leftRightCircularArrow", "leftRightRibbon",
        "leftRightUpArrow", "leftUpArrow", "lightningBolt", "mathDivide", "mathNotEqual", "moon", "noSmoking",
        "notchedRightArrow", "plaqueTabs", "quadArrow", "quadArrowCallout", "ribbon", "ribbon2", "rightArrow",
        "rightArrowCallout", "rightBrace", "smileyFace", "squareTabs", "stripedRightArrow", "sun", "swooshArrow", "upArrow",
        "upArrowCallout", "upDownArrow", "upDownArrowCallout", "uturnArrow", "verticalScroll", "wave", "wedgeEllipseCallout",
        "wedgeRectCallout", "wedgeRoundRectCallout",
    ];

    public static GeometryDefinition? Get(string preset) => Shapes.Value.GetValueOrDefault(preset);

    public static IReadOnlyCollection<string> Names => Shapes.Value.Keys;
}
