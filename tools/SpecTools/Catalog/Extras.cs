using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SpecTools.Catalog;

/// <summary>A preset shape or text warp from presetShapeDefinitions.xml / presetTextWarpDefinitions.xml.</summary>
public sealed record PresetGeometry(string Name, int AdjustValues, int Guides, int Handles, int ConnectionSites, int Paths);

/// <summary>Features that are defined by data files of ECMA-376 rather than by the schemas.</summary>
public static partial class Extras
{
    public static List<PresetGeometry> ReadPresets(string file)
    {
        static int Count(XElement shape, string list) =>
            shape.Elements().FirstOrDefault(e => e.Name.LocalName == list)?.Elements().Count() ?? 0;

        return XDocument.Load(file).Root!.Elements()
            .Select(shape => new PresetGeometry(
                shape.Name.LocalName,
                Count(shape, "avLst"),
                Count(shape, "gdLst"),
                Count(shape, "ahLst"),
                Count(shape, "cxnLst"),
                Count(shape, "pathLst")))
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Names of the art page borders (ECMA-376 Part 1, Annex F), from the image file names.</summary>
    public static List<string> ReadArtBorders(string dir) =>
        Directory.GetFiles(dir, "*.png")
            .Select(f => BorderPiece().Replace(Path.GetFileName(f), ""))
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();

    [GeneratedRegex(@"_(top|bottom|left|right|topLeft|topRight|bottomLeft|bottomRight)\.png$")]
    private static partial Regex BorderPiece();
}
