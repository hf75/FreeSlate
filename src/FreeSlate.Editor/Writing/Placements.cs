using System.Globalization;
using System.Xml.Linq;

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

    /// <summary>The icon of what is done with the picture itself (its caption, its description, its size): a picture, a hill and the sun in a frame.</summary>
    public static readonly string PictureIcon = Drawn(Frame(0, 1, 20, 14), "M3.2,12.6 L8,7.4 L11.1,10.6 L13.5,8.3 L16.8,12.6 Z", Circle(14.6, 5.4, 1.7));

    /// <summary>The icon of what the clipboard holds: a clipboard, its clip at the top, a picture on it.</summary>
    public static readonly string ClipboardIcon = Drawn(Frame(3, 1.6, 14, 14.4), Block(7, 0, 6, 3.2),
        "M5.6,13.6 L9,9.6 L11.2,12 L12.6,10.6 L14.4,13.6 Z");

    /// <summary>The icon of a caption: a picture with two lines of text below it.</summary>
    public static readonly string CaptionIcon = Drawn(Frame(3, 0, 14, 9.8), Hills, Bar(3, 12.2, 14), Bar(3, 14.8, 9));

    /// <summary>The icon of taking a caption away: the picture with a cross where its lines were.</summary>
    public static readonly string CaptionRemoveIcon = Drawn(Frame(3, 0, 14, 9.8), Hills, Stroke(7.6, 11.6, 12.4, 16.2), Stroke(12.4, 11.6, 7.6, 16.2));

    /// <summary>The hills of a small picture (the caption's), as the picture's own icon has them.</summary>
    const string Hills = "M5.3,8 L8.6,4.6 L10.8,6.7 L12.4,5.1 L14.7,8 Z";

    /// <summary>The icon of a description, what screen readers read: a speech bubble with lines in it.</summary>
    public static readonly string DescriptionIcon = Drawn(Frame(1, 1, 18, 11.2), "M5,11.4 L5,15.6 L9.6,11.4 Z", Bar(4.2, 4.3, 11.6), Bar(4.2, 7.5, 7.6));

    /// <summary>The icon of the size a picture took when it was put in: a frame with the corners of its room around it.</summary>
    public static readonly string NaturalSizeIcon = Drawn(Frame(4.4, 3.6, 11.2, 8.8),
        Bar(0, 0, 4), Block(0, 0, Weight, 4), Bar(16, 0, 4), Block(20 - Weight, 0, Weight, 4),
        Bar(0, 16 - Weight, 4), Block(0, 12, Weight, 4), Bar(16, 16 - Weight, 4), Block(20 - Weight, 12, Weight, 4));

    /// <summary>The icon of cropping a picture: the two angles of a cropping frame, one round the other.</summary>
    public static readonly string CropIcon = Drawn(Block(5, 0, Weight, 12.2), Bar(5, 11, 13), Bar(2, 4, 12.6), Block(13.4, 4, Weight, 12));

    /// <summary>The icon of a picture's colors: a circle, its right half filled.</summary>
    public static readonly string RecolorIcon = Drawn(Ring(10, 8, 7.2), "M10,2 A6,6 0 0 1 10,14 Z");

    /// <summary>The icon of another picture in a picture's frame: two arrows turning round each other.</summary>
    public static readonly string ReplaceIcon = Drawn(Turn(10, 8, 5.4, 200, 320), Turn(10, 8, 5.4, 20, 140));

    /// <summary>The icon of a hairline round a picture: a picture in a frame of its own.</summary>
    public static readonly string FrameIcon = Drawn(Frame(0, 0, 20, 16), Frame(2.8, 2.8, 14.4, 10.4), FramedHills);

    /// <summary>The icon of taking the hairline round a picture away: the picture, its frame broken where it was.</summary>
    public static readonly string FrameRemoveIcon = Drawn(Frame(2.8, 2.8, 14.4, 10.4), FramedHills,
        Bar(0, 0, 3), Bar(8.5, 0, 3), Bar(17, 0, 3), Bar(0, 16 - Weight, 3), Bar(8.5, 16 - Weight, 3), Bar(17, 16 - Weight, 3),
        Block(0, 0, Weight, 3), Block(0, 6.5, Weight, 3), Block(0, 13, Weight, 3), Block(20 - Weight, 0, Weight, 3), Block(20 - Weight, 6.5, Weight, 3), Block(20 - Weight, 13, Weight, 3));

    /// <summary>The hills of the picture in a frame of its own (see FrameIcon).</summary>
    const string FramedHills = "M5,11.4 L8.2,8 L10.4,10.2 L12,8.6 L15,11.4 Z";

    /// <summary>The icon of a picture shown in a circle: a ring.</summary>
    public static readonly string CircleIcon = Drawn(Ring(10, 8, 7.4));

    /// <summary>The icon of a picture shown in its rectangle again: a frame.</summary>
    public static readonly string RectangleIcon = Drawn(Frame(1, 2, 18, 12));

    /// <summary>The icon of turning a picture: a small picture inside an arrow going round it clockwise.</summary>
    public static readonly string TurnIcon = Drawn(Frame(6.4, 5.4, 7.2, 5.2), Turn(10, 8, 6.4, 150, 410));

    /// <summary>The icon of a picture standing straight again: a picture standing level on a line.</summary>
    public static readonly string StraightIcon = Drawn(Frame(3, 0.4, 14, 11), "M5.4,9.6 L8.6,6.2 L10.8,8.4 L12.4,6.8 L14.6,9.6 Z", Bar(0, 14.6, 20));

    /// <summary>The icon of a quarter turn clockwise: a picture, and over its top an arrow going right and down.</summary>
    public static readonly string TurnRightIcon = Drawn(Frame(1, 6, 10.4, 9.6), Turn(10, 9.6, 7.4, 225, 345));

    /// <summary>The icon of a quarter turn the other way: a picture, and over its top an arrow going left and down.</summary>
    public static readonly string TurnLeftIcon = Drawn(Frame(8.6, 6, 10.4, 9.6), Turn(10, 9.6, 7.4, 315, 195, back: true));

    /// <summary>The icon of mirroring across: a triangle and its outline on either side of a broken upright line.</summary>
    public static readonly string FlipAcrossIcon = Drawn("M8.4,1.6 L8.4,14.4 L1,14.4 Z", Outline((11.6, 1.6), (19, 14.4), (11.6, 14.4)),
        Block(9.4, 0, Weight, 2.4), Block(9.4, 4.5, Weight, 2.4), Block(9.4, 9, Weight, 2.4), Block(9.4, 13.5, Weight, 2.4));

    /// <summary>The icon of mirroring down: a triangle and its outline on either side of a broken level line.</summary>
    public static readonly string FlipDownIcon = Drawn("M2,6.8 L18,6.8 L2,0.6 Z", Outline((2, 9.2), (2, 15.4), (18, 9.2)),
        Bar(0, 7.4, 3), Bar(5.6, 7.4, 3), Bar(11.4, 7.4, 3), Bar(17, 7.4, 3));

    /// <summary>
    /// The icon of a picture tilted by one of Word's 3-D Rotation presets (see PictureTilt): a picture's frame as the engine's camera shows
    /// it, fitted into the icon's box.
    /// </summary>
    public static string TiltIcon(string camera)
    {
        XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
        var seen = FreeSlate.Drawing.Camera.Read(new XElement(a + "scene3d", new XElement(a + "camera", new XAttribute("prst", camera))));
        // A picture of 240 by 168 pixels, about the size of one in a column, for its perspective as it shows there.
        (double X, double Y)[] corners = [(-120, -84), (120, -84), (120, 84), (-120, 84)];
        var shown = corners.Select(c => seen?.Project(c.X, c.Y, 0) ?? c).ToArray();
        var (left, right, top, bottom) = (shown.Min(p => p.X), shown.Max(p => p.X), shown.Min(p => p.Y), shown.Max(p => p.Y));
        var scale = Math.Min(20 / (right - left), 16 / (bottom - top));
        var (dx, dy) = (10 - (left + right) / 2 * scale, 8 - (top + bottom) / 2 * scale);
        return Drawn(Outline([.. shown.Select(p => (p.X * scale + dx, p.Y * scale + dy))]));
    }

    /// <summary>The icon of a picture slanted (see TiltIcon): a frame leaning up to the right, as Word's isometric presets show a picture.</summary>
    public static readonly string AskewIcon = TiltIcon("isometricRightUp");

    /// <summary>The icon of tilting a picture by hand: a picture turned away, its right side nearer.</summary>
    public static readonly string TiltByHandIcon = TiltIcon("perspectiveContrastingLeftFacing");

    /// <summary>The hairline the gesture ring puts round a picture (see DrawingFormatting.Outline): half a point, in a quarter's grey.</summary>
    public const double FrameWidth = 0.5;
    public const string FrameColor = "BFBFBF";

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

    /// <summary>A stroke from one point to another, as thick as a line of text (all of them turning the same way, so that they cross filled).</summary>
    static string Stroke(double x1, double y1, double x2, double y2)
    {
        var (dx, dy) = (x2 - x1, y2 - y1);
        var length = Math.Sqrt(dx * dx + dy * dy);
        var (nx, ny) = (-dy / length * Weight / 2, dx / length * Weight / 2);
        return $"M{N(x1 + nx)},{N(y1 + ny)} L{N(x2 + nx)},{N(y2 + ny)} L{N(x2 - nx)},{N(y2 - ny)} L{N(x1 - nx)},{N(y1 - ny)} Z";
    }

    /// <summary>A filled box, clockwise.</summary>
    static string Block(double x, double y, double width, double height) => $"M{N(x)},{N(y)} H{N(x + width)} V{N(y + height)} H{N(x)} Z";

    /// <summary>A frame: a box less its inside, which goes the other way round.</summary>
    static string Frame(double x, double y, double width, double height) =>
        Block(x, y, width, height) + $" M{N(x + Weight)},{N(y + Weight)} V{N(y + height - Weight)} H{N(x + width - Weight)} V{N(y + Weight)} Z";

    /// <summary>A filled circle.</summary>
    static string Circle(double x, double y, double radius) =>
        $"M{N(x - radius)},{N(y)} A{N(radius)},{N(radius)} 0 1 0 {N(x + radius)},{N(y)} A{N(radius)},{N(radius)} 0 1 0 {N(x - radius)},{N(y)} Z";

    /// <summary>A ring as thick as a line of text, its outside as large as given: a circle less its inside, which goes the other way round.</summary>
    static string Ring(double x, double y, double radius)
    {
        var inner = radius - Weight;
        return Circle(x, y, radius)
            + $" M{N(x - inner)},{N(y)} A{N(inner)},{N(inner)} 0 1 1 {N(x + inner)},{N(y)} A{N(inner)},{N(inner)} 0 1 1 {N(x - inner)},{N(y)} Z";
    }

    /// <summary>
    /// An arc as thick as a line of text round a centre, clockwise from one angle to another (degrees, 0 to the right, as the y axis goes
    /// down; <paramref name="back"/>, the other way round, to below from), and at its end a head pointing on the way it turns.
    /// </summary>
    static string Turn(double x, double y, double radius, double from, double to, bool back = false)
    {
        (double X, double Y) At(double r, double degrees) => (x + r * Math.Cos(degrees * Math.PI / 180), y + r * Math.Sin(degrees * Math.PI / 180));
        var (outer, inner) = (radius + Weight / 2, radius - Weight / 2);
        var large = Math.Abs(to - from) > 180 ? 1 : 0;
        var (on, against) = back ? (0, 1) : (1, 0);
        var (a, b, c, d) = (At(outer, from), At(outer, to), At(inner, to), At(inner, from));
        // The head: across the arc's end, twice as wide as the arc, its tip ahead along the circle.
        var (wide, tip) = (At(radius + 2.2, to), At(radius, to + (back ? -1 : 1) * 3.2 / radius * 180 / Math.PI));
        var narrow = At(radius - 2.2, to);
        // The head goes round the same way as the arc, so that where they meet they fill as one.
        var (first, last) = back ? (narrow, wide) : (wide, narrow);
        return $"M{N(a.X)},{N(a.Y)} A{N(outer)},{N(outer)} 0 {large} {on} {N(b.X)},{N(b.Y)} L{N(c.X)},{N(c.Y)} A{N(inner)},{N(inner)} 0 {large} {against} {N(d.X)},{N(d.Y)} Z"
            + $" M{N(first.X)},{N(first.Y)} L{N(tip.X)},{N(tip.Y)} L{N(last.X)},{N(last.Y)} Z";
    }

    /// <summary>
    /// A frame of four or three straight sides through corners going clockwise (as the y axis goes down), its inside a line's weight in,
    /// which goes the other way round.
    /// </summary>
    static string Outline(params (double X, double Y)[] corners)
    {
        var n = corners.Length;
        // Each side moved in by the weight; the inside's corners where neighbouring sides so moved meet.
        (double X, double Y) Inward(int k)
        {
            var (p, q) = (corners[k], corners[(k + 1) % n]);
            var length = Math.Sqrt((q.X - p.X) * (q.X - p.X) + (q.Y - p.Y) * (q.Y - p.Y));
            return (-(q.Y - p.Y) / length * Weight, (q.X - p.X) / length * Weight);
        }
        var inside = new (double X, double Y)[n];
        for (var k = 0; k < n; k++)
        {
            var (before, after) = ((k + n - 1) % n, k);
            var (u, v) = (Inward(before), Inward(after));
            var (p1, d1) = ((corners[before].X + u.X, corners[before].Y + u.Y), (corners[k].X - corners[before].X, corners[k].Y - corners[before].Y));
            var (p2, d2) = ((corners[k].X + v.X, corners[k].Y + v.Y), (corners[(k + 1) % n].X - corners[k].X, corners[(k + 1) % n].Y - corners[k].Y));
            var cross = d1.Item1 * d2.Item2 - d1.Item2 * d2.Item1;
            var t = Math.Abs(cross) < 1e-9 ? 1 : ((p2.Item1 - p1.Item1) * d2.Item2 - (p2.Item2 - p1.Item2) * d2.Item1) / cross;
            inside[k] = (p1.Item1 + d1.Item1 * t, p1.Item2 + d1.Item2 * t);
        }
        return $"M{string.Join(" L", corners.Select(c => $"{N(c.X)},{N(c.Y)}"))} Z M{string.Join(" L", inside.Reverse().Select(c => $"{N(c.X)},{N(c.Y)}"))} Z";
    }

    static string N(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
