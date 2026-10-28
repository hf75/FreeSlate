using System.Xml.Linq;

namespace FreeSlate.Drawing;

/// <summary>A resolved DrawingML fill (ECMA-376 Part 1, §20.1.8).</summary>
public abstract record Fill
{
    public static readonly Fill None = new NoFill();

    /// <summary>The one color standing for a fill (3D sides and backs): its color, first stop or foreground.</summary>
    public static RgbColor? MainColor(Fill fill) => fill switch
    {
        SolidFill solid => solid.Color,
        GradientFill gradient when gradient.Stops.Count > 0 => gradient.Stops[0].Color,
        PatternFill pattern => pattern.Foreground,
        SlideFill slide => MainColor(slide.Fill),
        _ => null,
    };
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
    bool RotateWithShape) : Fill
{
    /// <summary>
    /// PowerPoint draws this gradient with a bell-shaped blend in linear light: two stops at 0 and 1, or three
    /// stops A, B, A at 0, f, 1 (measured; other gradients interpolate linearly in sRGB).
    /// </summary>
    public bool Bell { get; init; }

    /// <summary>
    /// For a gradient that does not turn with its shape (rotWithShape="0"): the slide's directions in the shape's
    /// coordinates, the turns and flips of the shape and its groups undone (the linear part of the slide-to-shape
    /// transform); null where only the shape's own turn is undone.
    /// </summary>
    public (double A, double B, double C, double D)? Unturn { get; init; }
}

public sealed record PatternFill(string Preset, RgbColor Foreground, RgbColor Background) : Fill
{
    /// <summary>
    /// The pattern's own 8 × 8 bitmap instead of the preset's, a byte a row, the leftmost bit highest (a VML pattern
    /// fill's picture, catalog: vml.shapes); null for the preset.
    /// </summary>
    public byte[]? Bits { get; init; }

    /// <summary>The size of a bit in pixels: 1 for the presets, 4/3 (a point) for Word's VML patterns.</summary>
    public double Cell { get; init; } = 1;

    /// <summary>
    /// Where the slide lies in the shape's coordinates (pixels): PowerPoint draws the tiles on the slide's pixels, not
    /// turned with the shape (measured: LibreOffice's tdf107608, a downward diagonal in a shape turned by 270° stays
    /// downward); null where the tiles are only shifted by the shape's position.
    /// </summary>
    public (double A, double B, double C, double D, double E, double F)? ToShape { get; init; }
}

/// <summary>
/// The slide's background fill on a shape (p:sp/@useBgFill): painted over the whole slide (width × height pixels) and
/// mapped into the shape's coordinates, so the shape shows the background behind it.
/// </summary>
public sealed record SlideFill(Fill Fill, double Width, double Height, (double A, double B, double C, double D, double E, double F) ToShape) : Fill;

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
    /// <summary>
    /// A pattern fill's own picture (a relationship id) and the size of its bits in pixels, as the VML conversion
    /// writes them on a:pattFill (catalog: vml.shapes).
    /// </summary>
    public static readonly XName PatternPicture = XNamespace.Get("urn:freeslate:vml") + "picture", PatternCell = XNamespace.Get("urn:freeslate:vml") + "cell";

    /// <summary>An 8 × 8 picture (a data URI) as pattern bits: its light pixels set; null for other pictures.</summary>
    static byte[]? PatternBits(string? uri)
    {
        var at = uri?.IndexOf(";base64,", StringComparison.Ordinal) ?? -1;
        if (uri == null || at < 0)
            return null;
        byte[] data;
        try
        {
            data = Convert.FromBase64String(uri[(at + 8)..]);
        }
        catch (FormatException)
        {
            return null;
        }
        if (Html.Gif.Decode(data) is not var (width, height, rgba) || width != 8 || height != 8)
            return null;
        var bits = new byte[8];
        for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
                if (rgba[(y * 8 + x) * 4] + rgba[(y * 8 + x) * 4 + 1] + rgba[(y * 8 + x) * 4 + 2] >= 3 * 128)
                    bits[y] |= (byte)(0x80 >> x);
        return bits;
    }

    static readonly HashSet<string> FillElements = ["noFill", "solidFill", "gradFill", "blipFill", "pattFill", "grpFill"];

    /// <summary>The fill element among the children of a property container (spPr, bgPr, ln, rPr, tcPr …).</summary>
    public static XElement? FindFill(XElement? container) =>
        container?.Elements().FirstOrDefault(e => FillElements.Contains(e.Name.LocalName) && e.Name.Namespace == Ns.A);

    /// <param name="images">Resolves an r:embed id to a data URI.</param>
    /// <param name="sizes">A picture's size in pixels by relationship id, at its own resolution or the given dpi (when positive).</param>
    public static Fill Read(XElement fill, ColorContext colors, Func<string, string?>? images = null, Func<string, double, (double W, double H)>? sizes = null)
    {
        switch (fill.Name.LocalName)
        {
            case "noFill":
                return Fill.None;
            case "solidFill":
                return colors.ResolveChild(fill) is { } c ? new SolidFill(c) : Fill.None;
            case "gradFill":
                var fileOrder = fill.Element(Ns.A + "gsLst")?.Elements(Ns.A + "gs")
                    .Select(gs => new GradientStop((gs.Long("pos") ?? 0) / 100000.0, colors.ResolveChild(gs) ?? RgbColor.Black))
                    .ToList() ?? [];
                var stops = fileOrder.OrderBy(s => s.Position).ToList();
                var bell = (stops.Count == 2 && stops[0].Position <= 1e-6 && stops[1].Position >= 1 - 1e-6)
                           || (stops.Count == 3 && stops[0].Position <= 1e-6 && stops[2].Position >= 1 - 1e-6
                               && stops[1].Position > 1e-6 && stops[1].Position < 1 - 1e-6 && stops[0].Color == stops[2].Color);
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
                    fill.Bool("rotWithShape") ?? true) { Bell = bell };
            case "pattFill":
                return new PatternFill(
                    fill.Str("prst") ?? "pct5",
                    colors.ResolveChild(fill.Element(Ns.A + "fgClr")) ?? RgbColor.Black,
                    colors.ResolveChild(fill.Element(Ns.A + "bgClr")) ?? RgbColor.White)
                {
                    Bits = (string?)fill.Attribute(PatternPicture) is { } picture ? PatternBits(images?.Invoke(picture)) : null,
                    Cell = double.TryParse((string?)fill.Attribute(PatternCell), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var cell) && cell > 0 ? cell : 1,
                };
            case "blipFill":
                var blip = fill.Element(Ns.A + "blip");
                // SVG pictures keep the SVG in an extension and may have no bitmap fallback at all.
                var reference = blip?.Descendants(Ns.Asvg + "svgBlip").FirstOrDefault()?.Attribute(Ns.R + "embed") ?? blip?.Attribute(Ns.R + "embed");
                var image = reference != null ? images?.Invoke(reference.Value) : null;
                if (image == null)
                    return Fill.None;
                var stretch = fill.Element(Ns.A + "stretch");
                var tile = fill.Element(Ns.A + "tile");
                // A shape's picture fill without a fill mode is tiled at the picture's own size (measured: a check
                // mark repeated over a shape, LibreOffice's tdf128596); pictures (p:blipFill) keep stretching.
                if (stretch == null && tile == null && fill.Name.Namespace == Ns.A)
                    tile = new XElement(Ns.A + "tile");
                return new PictureFill(image, Rect(fill.Element(Ns.A + "srcRect")), Rect(stretch?.Element(Ns.A + "fillRect")), tile != null)
                {
                    Filter = Html.BlipEffects.Filter(blip, colors),
                    Tiling = tile == null ? null : new TileSpec(
                        (tile.Long("tx") ?? 0) / Units.EmuPerPoint, (tile.Long("ty") ?? 0) / Units.EmuPerPoint,
                        (tile.Long("sx") ?? 100000) / 100000.0, (tile.Long("sy") ?? 100000) / 100000.0,
                        tile.Str("flip") ?? "none", tile.Str("algn") ?? "tl"),
                    // Tiles are sized from the picture's own resolution; PowerPoint ignores blipFill dpi (measured).
                    NaturalSize = blip?.Attribute(Ns.R + "embed") is { } embed && sizes != null ? sizes(embed.Value, 0) : default,
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
