using System.Globalization;
using System.Xml.Linq;

namespace FreeSlate.Drawing.Geometry;

public sealed record Guide(string Name, string Formula);

/// <summary>One path command; arguments are guide names or numbers.</summary>
public sealed record PathCommand(string Kind, string[] Args);

/// <summary>A path of a shape geometry; W/H give its own coordinate space if set.</summary>
public sealed record PathDefinition(double? W, double? H, string Fill, bool Stroke, List<PathCommand> Commands);

/// <summary>
/// A shape geometry in the DrawingML formula language (ECMA-376 Part 1, §20.1.9): adjust values, guides,
/// text rectangle and paths. Preset definitions and a:custGeom share this structure.
/// </summary>
public sealed class GeometryDefinition
{
    public List<Guide> AdjustValues { get; } = [];
    public List<Guide> Guides { get; } = [];
    public (string L, string T, string R, string B)? TextRect { get; private set; }
    public List<PathDefinition> Paths { get; } = [];

    /// <summary>Parses a geometry element: a preset from presetShapeDefinitions.xml or an a:custGeom.</summary>
    public static GeometryDefinition Parse(XElement geometry)
    {
        var def = new GeometryDefinition();
        XElement? Child(string name) => geometry.Elements().FirstOrDefault(e => e.Name.LocalName == name);

        foreach (var gd in Child("avLst")?.Elements() ?? [])
            def.AdjustValues.Add(new Guide(gd.Str("name") ?? "", gd.Str("fmla") ?? "val 0"));
        foreach (var gd in Child("gdLst")?.Elements() ?? [])
            def.Guides.Add(new Guide(gd.Str("name") ?? "", gd.Str("fmla") ?? "val 0"));
        if (Child("rect") is { } rect)
            def.TextRect = (rect.Str("l") ?? "l", rect.Str("t") ?? "t", rect.Str("r") ?? "r", rect.Str("b") ?? "b");

        foreach (var path in Child("pathLst")?.Elements() ?? [])
        {
            var commands = new List<PathCommand>();
            foreach (var cmd in path.Elements())
            {
                var points = cmd.Elements().Where(p => p.Name.LocalName == "pt").ToList();
                commands.Add(cmd.Name.LocalName switch
                {
                    "arcTo" => new PathCommand("arcTo", [cmd.Str("wR") ?? "0", cmd.Str("hR") ?? "0", cmd.Str("stAng") ?? "0", cmd.Str("swAng") ?? "0"]),
                    "close" => new PathCommand("close", []),
                    var kind => new PathCommand(kind, points.SelectMany(p => new[] { p.Str("x") ?? "0", p.Str("y") ?? "0" }).ToArray()),
                });
            }
            def.Paths.Add(new PathDefinition(
                Number(path.Str("w")),
                Number(path.Str("h")),
                path.Str("fill") ?? "norm",
                path.Bool("stroke") ?? true,
                commands));
        }
        return def;
    }

    static double? Number(string? s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : null;
}
