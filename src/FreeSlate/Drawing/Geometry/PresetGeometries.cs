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
                result[shape.Name.LocalName] = GeometryDefinition.Parse(shape);
        }
        return result;
    });

    public static GeometryDefinition? Get(string preset) => Shapes.Value.GetValueOrDefault(preset);

    public static IReadOnlyCollection<string> Names => Shapes.Value.Keys;
}
