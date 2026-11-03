using System.Globalization;

namespace FreeSlate.Editor.Writing;

/// <summary>
/// Where a picture stands among the text, as the gesture ring offers it (see EditorView.Join): its name's key among the texts, its
/// icon (path data on a box of 20 × 16: the picture a frame, the text lines), and how the drawing then stands (see DrawingFormatting).
/// A picture keeps where it stands in the direction a placement does not set.
/// </summary>
public sealed record Placement(string NameKey, string Icon, DrawingFormatting Format)
{
    /// <summary>In the line, one of its characters: a frame on the middle one of three lines.</summary>
    public static readonly Placement Inline = new("PictureInline",
        Drawn(Bar(0, 1, 20), Bar(0, 10.2, 5), Frame(6.5, 4.4, 7, 7), Bar(15, 10.2, 5), Bar(0, 13.8, 20)),
        new() { Wrap = "inline" });

    /// <summary>At the column's left, the text beside it on the right: a frame left of short lines, two lines below it.</summary>
    public static readonly Placement Left = new("PictureLeft",
        Drawn(Frame(0, 1, 8, 8.2), Bar(10, 1, 10), Bar(10, 4.5, 10), Bar(10, 8, 10), Bar(0, 11.4, 20), Bar(0, 14.8, 12)),
        new() { Wrap = "square", HorizontalFrom = "column", HorizontalAlign = "left" });

    /// <summary>At the column's right, the text beside it on the left.</summary>
    public static readonly Placement Right = new("PictureRight",
        Drawn(Frame(12, 1, 8, 8.2), Bar(0, 1, 10), Bar(0, 4.5, 10), Bar(0, 8, 10), Bar(0, 11.4, 20), Bar(0, 14.8, 12)),
        new() { Wrap = "square", HorizontalFrom = "column", HorizontalAlign = "right" });

    /// <summary>On its own, in the middle of the column, the text above and below it: a frame between two lines.</summary>
    public static readonly Placement Own = new("PictureOwn",
        Drawn(Bar(0, 0.4, 20), Frame(5, 3.4, 10, 9.2), Bar(0, 14.4, 20)),
        new() { Wrap = "topAndBottom", HorizontalFrom = "column", HorizontalAlign = "center" });

    /// <summary>Behind the text, which goes over it: four lines, the middle two across a frame.</summary>
    public static readonly Placement Behind = new("PictureBehind",
        Drawn(Bar(0, 0.4, 20), Bar(0, 5, 20), Frame(4.5, 3.2, 11, 9.4), Bar(0, 9.6, 20), Bar(0, 14.2, 20)),
        new() { Wrap = "behind" });

    /// <summary>In front of the text, which goes under it: four lines, the middle two hidden by a frame and going on beside it.</summary>
    public static readonly Placement Front = new("PictureFront",
        Drawn(Bar(0, 0.4, 20), Bar(0, 5, 3.3), Bar(16.7, 5, 3.3), Frame(4.5, 3.2, 11, 9.4), Bar(0, 9.6, 3.3), Bar(16.7, 9.6, 3.3), Bar(0, 14.2, 20)),
        new() { Wrap = "front" });

    /// <summary>The icon of the places on the page: a page with nine dots.</summary>
    public static readonly string PlacesIcon = Drawn(Frame(4, 0, 12, 16),
        Block(6, 2.6, 1.6, 1.6), Block(9.2, 2.6, 1.6, 1.6), Block(12.4, 2.6, 1.6, 1.6),
        Block(6, 7.2, 1.6, 1.6), Block(9.2, 7.2, 1.6, 1.6), Block(12.4, 7.2, 1.6, 1.6),
        Block(6, 11.8, 1.6, 1.6), Block(9.2, 11.8, 1.6, 1.6), Block(12.4, 11.8, 1.6, 1.6));

    /// <summary>
    /// The eight places round the page's margins, from the top clockwise as the ring's directions point (top, top right, right …), the
    /// text beside them as Word's Position gallery has it: a page with a block where the picture goes.
    /// </summary>
    public static readonly IReadOnlyList<Placement> Places =
    [
        At("PlaceTop", "center", "top"),
        At("PlaceTopRight", "right", "top"),
        At("PlaceRight", "right", "center"),
        At("PlaceBottomRight", "right", "bottom"),
        At("PlaceBottom", "center", "bottom"),
        At("PlaceBottomLeft", "left", "bottom"),
        At("PlaceLeft", "left", "center"),
        At("PlaceTopLeft", "left", "top"),
    ];

    static Placement At(string nameKey, string across, string down)
    {
        var x = across switch { "left" => 2.2, "center" => 4.2, _ => 6.2 };
        var y = down switch { "top" => 2.2, "center" => 6.2, _ => 10.2 };
        return new(nameKey, Drawn(Frame(0, 0, 12, 16), Block(x, y, 3.6, 3.6)),
            new() { Wrap = "square", HorizontalFrom = "margin", HorizontalAlign = across, VerticalFrom = "margin", VerticalAlign = down });
    }

    /// <summary>The weight of an icon's lines and frames.</summary>
    const double Weight = 1.2;

    /// <summary>An icon of its parts, filled where any of them is (the inside of a frame is not, unless a part covers it).</summary>
    static string Drawn(params string[] parts) => "F1 " + string.Join(" ", parts);

    /// <summary>A line of text, as wide as given.</summary>
    static string Bar(double x, double y, double width) => Block(x, y, width, Weight);

    /// <summary>A filled box, clockwise.</summary>
    static string Block(double x, double y, double width, double height) => $"M{N(x)},{N(y)} H{N(x + width)} V{N(y + height)} H{N(x)} Z";

    /// <summary>A frame: a box less its inside, which goes the other way round.</summary>
    static string Frame(double x, double y, double width, double height) =>
        Block(x, y, width, height) + $" M{N(x + Weight)},{N(y + Weight)} V{N(y + height - Weight)} H{N(x + width - Weight)} V{N(y + Weight)} Z";

    static string N(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
