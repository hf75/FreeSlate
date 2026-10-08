using System.Xml.Linq;

namespace FreeSlate.Drawing;

/// <summary>A resolved DrawingML fill (ECMA-376 Part 1, §20.1.8).</summary>
public abstract record Fill
{
    public static readonly Fill None = new NoFill();
}

public sealed record NoFill : Fill;

public sealed record SolidFill(RgbColor Color) : Fill;

public sealed record GradientStop(double Position, RgbColor Color);

/// <summary>A gradient; either linear (Angle in degrees) or a path gradient (circle, rect, shape) towards FocusRect.</summary>
public sealed record GradientFill(
    List<GradientStop> Stops,
    double? Angle,
    bool Scaled,
    string? Path,
    (double L, double T, double R, double B) FocusRect,
    (double L, double T, double R, double B) TileRect,
    bool RotateWithShape) : Fill;

public sealed record PatternFill(string Preset, RgbColor Foreground, RgbColor Background) : Fill;

/// <summary>A picture fill; Image is a data URI, SourceRect the crop in fractions (negative values pad).</summary>
public sealed record PictureFill(string Image, (double L, double T, double R, double B) SourceRect, (double L, double T, double R, double B) FillRect, bool Tile) : Fill;

/// <summary>Use the fill of the parent group.</summary>
public sealed record GroupFill : Fill;

public static class FillReader
{
    static readonly HashSet<string> FillElements = ["noFill", "solidFill", "gradFill", "blipFill", "pattFill", "grpFill"];

    /// <summary>The fill element among the children of a property container (spPr, bgPr, ln, rPr, tcPr …).</summary>
    public static XElement? FindFill(XElement? container) =>
        container?.Elements().FirstOrDefault(e => FillElements.Contains(e.Name.LocalName) && e.Name.Namespace == Ns.A);

    /// <param name="images">Resolves an r:embed id to a data URI.</param>
    public static Fill Read(XElement fill, ColorContext colors, Func<string, string?>? images = null)
    {
        switch (fill.Name.LocalName)
        {
            case "noFill":
                return Fill.None;
            case "solidFill":
                return colors.ResolveChild(fill) is { } c ? new SolidFill(c) : Fill.None;
            case "gradFill":
                var stops = fill.Element(Ns.A + "gsLst")?.Elements(Ns.A + "gs")
                    .Select(gs => new GradientStop((gs.Long("pos") ?? 0) / 100000.0, colors.ResolveChild(gs) ?? RgbColor.Black))
                    .OrderBy(s => s.Position)
                    .ToList() ?? [];
                if (stops.Count == 0)
                    return Fill.None;
                var lin = fill.Element(Ns.A + "lin");
                var path = fill.Element(Ns.A + "path");
                return new GradientFill(
                    stops,
                    lin != null ? (lin.Long("ang") ?? 0) / 60000.0 : path == null ? 90 : null,
                    lin.Bool("scaled") ?? false,
                    path?.Str("path"),
                    Rect(path?.Element(Ns.A + "fillToRect")),
                    Rect(fill.Element(Ns.A + "tileRect")),
                    fill.Bool("rotWithShape") ?? true);
            case "pattFill":
                return new PatternFill(
                    fill.Str("prst") ?? "pct5",
                    colors.ResolveChild(fill.Element(Ns.A + "fgClr")) ?? RgbColor.Black,
                    colors.ResolveChild(fill.Element(Ns.A + "bgClr")) ?? RgbColor.White);
            case "blipFill":
                var blip = fill.Element(Ns.A + "blip");
                var image = blip?.Attribute(Ns.R + "embed") is { } id ? images?.Invoke(id.Value) : null;
                if (image == null)
                    return Fill.None;
                var stretch = fill.Element(Ns.A + "stretch");
                return new PictureFill(image, Rect(fill.Element(Ns.A + "srcRect")), Rect(stretch?.Element(Ns.A + "fillRect")), fill.Element(Ns.A + "tile") != null);
            case "grpFill":
                return new GroupFill();
            default:
                return Fill.None;
        }
    }

    /// <summary>Reads l/t/r/b in thousandths of a percent as fractions.</summary>
    public static (double L, double T, double R, double B) Rect(XElement? rect) =>
        ((rect.Long("l") ?? 0) / 100000.0, (rect.Long("t") ?? 0) / 100000.0, (rect.Long("r") ?? 0) / 100000.0, (rect.Long("b") ?? 0) / 100000.0);
}
