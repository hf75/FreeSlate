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
public sealed record PictureFill(string Image, (double L, double T, double R, double B) SourceRect, (double L, double T, double R, double B) FillRect, bool Tile) : Fill
{
    /// <summary>SVG filter primitives for the blip's color effects (catalog: picture.effects), or null.</summary>
    public string? Filter { get; init; }

    /// <summary>Tile placement (a:tile): offset in points, scale, flip and alignment; null when stretched.</summary>
    public TileSpec? Tiling { get; init; }

    /// <summary>The picture's own size in pixels at its resolution (tiles are sized from it).</summary>
    public (double W, double H) NaturalSize { get; init; }
}

/// <summary>a:tile: offsets in points, scales as factors, flip x/y/xy/none and alignment (tl, ctr, br …).</summary>
public sealed record TileSpec(double Tx, double Ty, double Sx, double Sy, string Flip, string Align);

/// <summary>Use the fill of the parent group.</summary>
public sealed record GroupFill : Fill;

public static class FillReader
{
    static readonly HashSet<string> FillElements = ["noFill", "solidFill", "gradFill", "blipFill", "pattFill", "grpFill"];

    /// <summary>The fill element among the children of a property container (spPr, bgPr, ln, rPr, tcPr …).</summary>
    public static XElement? FindFill(XElement? container) =>
        container?.Elements().FirstOrDefault(e => FillElements.Contains(e.Name.LocalName) && e.Name.Namespace == Ns.A);

    /// <param name="images">Resolves an r:embed id to a data URI.</param>
    public static Fill Read(XElement fill, ColorContext colors, Func<string, string?>? images = null, Func<string, (double W, double H)>? sizes = null)
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
                // SVG pictures keep the SVG in an extension and may have no bitmap fallback at all.
                var reference = blip?.Descendants(Ns.Asvg + "svgBlip").FirstOrDefault()?.Attribute(Ns.R + "embed") ?? blip?.Attribute(Ns.R + "embed");
                var image = reference != null ? images?.Invoke(reference.Value) : null;
                if (image == null)
                    return Fill.None;
                var stretch = fill.Element(Ns.A + "stretch");
                var tile = fill.Element(Ns.A + "tile");
                return new PictureFill(image, Rect(fill.Element(Ns.A + "srcRect")), Rect(stretch?.Element(Ns.A + "fillRect")), tile != null)
                {
                    Filter = Html.BlipEffects.Filter(blip, colors),
                    Tiling = tile == null ? null : new TileSpec(
                        (tile.Long("tx") ?? 0) / Units.EmuPerPoint, (tile.Long("ty") ?? 0) / Units.EmuPerPoint,
                        (tile.Long("sx") ?? 100000) / 100000.0, (tile.Long("sy") ?? 100000) / 100000.0,
                        tile.Str("flip") ?? "none", tile.Str("algn") ?? "tl"),
                    NaturalSize = blip?.Attribute(Ns.R + "embed") is { } embed && sizes != null ? sizes(embed.Value) : default,
                };
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
