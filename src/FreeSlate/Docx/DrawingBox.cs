using System.Globalization;
using System.Xml.Linq;

namespace FreeSlate.Docx;

/// <summary>
/// The boxes of a drawing that the text lays out around or beside (catalog: drawing.anchor, drawing.wrap), in points,
/// read from the drawing as they are asked for: its extent (wp:extent), the effect extents around it (wp:effectExtent,
/// the room its shadow, glow, reflection, line or turn takes past the extent; they may be negative), its distances from
/// the text (distL, distT, distR, distB) and its wrapping (the wrap element, the sides the text takes and the polygon
/// of tight and through wrapping). Inline drawings go by <see cref="Drawings.Box"/>, which works out some groups'
/// effect extents anew.
/// </summary>
public readonly record struct DrawingBox(XElement Drawing)
{
    /// <summary>The extent (wp:extent cx and cy).</summary>
    public (double Width, double Height) Extent => Drawings.Extent(Drawing);

    /// <summary>The effect extents as the document stores them (wp:effectExtent l, t, r and b), 0 where missing.</summary>
    public (double Left, double Top, double Right, double Bottom) Effect => Sides(Drawing.Element(Drawings.Wp + "effectExtent"), "l", "t", "r", "b");

    /// <summary>The distances from the text (distL, distT, distR and distB), 0 where missing.</summary>
    public (double Left, double Top, double Right, double Bottom) Distance => Sides(Drawing, "distL", "distT", "distR", "distB");

    /// <summary>The wrap element (wp:wrapNone, wrapSquare, wrapTight, wrapThrough or wrapTopAndBottom), null for none.</summary>
    public XElement? Wrap => Drawing.Elements().FirstOrDefault(e => e.Name.LocalName.StartsWith("wrap", StringComparison.Ordinal));

    /// <summary>Whether the text goes around the drawing: it has a wrap element other than wp:wrapNone.</summary>
    public bool Wraps => Wrap is { } wrap && wrap.Name.LocalName != "wrapNone";

    /// <summary>Whether the text keeps free above and below the drawing (wp:wrapTopAndBottom).</summary>
    public bool TopAndBottom => Wrap?.Name.LocalName == "wrapTopAndBottom";

    /// <summary>Whether the text goes along the drawing's wrap polygon (wp:wrapTight, wp:wrapThrough).</summary>
    public bool Tight => Wrap?.Name.LocalName is "wrapTight" or "wrapThrough";

    /// <summary>The sides the text goes around the drawing on (wrapText: bothSides, left, right or largest).</summary>
    public string Side => (string?)Wrap?.Attribute("wrapText") ?? "bothSides";

    /// <summary>
    /// The wrap polygon of tight and through wrapping (wp:wrapPolygon start and lineTo, in 21600ths of the extent) in
    /// fractions of the extent; null without one of three points or more.
    /// </summary>
    public List<(double X, double Y)>? Polygon =>
        Wrap is { Name.LocalName: "wrapTight" or "wrapThrough" } wrap && wrap.Element(Drawings.Wp + "wrapPolygon") is { } polygon
        && polygon.Elements().Where(p => p.Name.LocalName is "start" or "lineTo")
            .Select(p => (X: Points(p, "x") * 12700 / 21600, Y: Points(p, "y") * 12700 / 21600)).ToList() is { Count: >= 3 } points
            ? points : null;

    /// <summary>The sides with the negative ones as none: the room effect extents take outward.</summary>
    public static (double Left, double Top, double Right, double Bottom) Outward((double Left, double Top, double Right, double Bottom) sides) =>
        (Math.Max(0, sides.Left), Math.Max(0, sides.Top), Math.Max(0, sides.Right), Math.Max(0, sides.Bottom));

    /// <summary>
    /// A coordinate in EMU, or a universal measure (docx/external/libreoffice/extras-ooxmlexport/tdf166201_simplePosCM:
    /// a simple position of 10cm 5cm), in points; 0 where it is missing or neither.
    /// </summary>
    public static double Points(string? value) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v / 12700
        : value != null && Wml.Universal(value.Trim()) is { } measure ? measure : 0;

    static double Points(XElement? element, string name) => Points((string?)element?.Attribute(name));

    static (double, double, double, double) Sides(XElement? element, string left, string top, string right, string bottom) =>
        (Points(element, left), Points(element, top), Points(element, right), Points(element, bottom));
}
