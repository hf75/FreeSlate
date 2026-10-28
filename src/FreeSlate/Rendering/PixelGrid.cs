namespace FreeSlate.Rendering;

/// <summary>
/// The device pixels a slide is drawn on, as how many of them a CSS pixel covers along each axis (catalog:
/// engine.output): (1, 1) draws slides at their own size; any other grid scales them to it, as PowerPoint draws a
/// slide whose size is no whole number of pixels at its rounded size (shapes 1 across and (h / H)(W / w) down; see
/// PptxConverter.ToHtml). Outlines and rectangles keep their places on the slide's own pixels and are then drawn where
/// PowerPoint's antialiasing samples the device's (SampledX, SampledY); text snaps to the device's pixels. Text and
/// tables are scaled by <paramref name="Text"/> on top, about the slide's corner: PowerPoint maps them by w / W across
/// and h / H down, which is the shapes' mapping scaled by w / W (measured with probe decks of one-cell tables at three
/// such sizes: every border as predicted; text boxes nearly so).
/// </summary>
public readonly record struct PixelGrid(double X, double Y, double Text = 1)
{
    /// <summary>The slide's own pixels.</summary>
    public static PixelGrid Css { get; } = new(1, 1);

    public bool IsCss => X == 1 && Y == 1 && Text == 1;

    /// <summary>The pixels text and tables stand on (see Text).</summary>
    public PixelGrid OfText => new(X * Text, Y * Text);

    /// <summary>An x (CSS pixels) rounded by <paramref name="round"/> on the grid's pixels, in CSS pixels.</summary>
    public double OnX(double x, Func<double, double> round) => round(x * X) / X;

    /// <summary>A y (CSS pixels) rounded by <paramref name="round"/> on the grid's pixels, in CSS pixels.</summary>
    public double OnY(double y, Func<double, double> round) => round(y * Y) / Y;

    /// <summary>
    /// A band of <paramref name="width"/> about <paramref name="center"/> across (CSS pixels, placed on the slide's own
    /// pixels) where PowerPoint draws it on the grid's: its start moved on to the next of its antialiasing samples there,
    /// an eighth's left along a row; returns the band's center.
    /// </summary>
    public double SampledX(double center, double width = 0) => X is 1 or 0 ? center : Math.Ceiling((center - width / 2) * X * 8 - 1e-9) / 8 / X + width / 2;

    /// <summary>A band down (see SampledX), its start moved on to the next quarter's top of a pixel.</summary>
    public double SampledY(double center, double width = 0) => Y is 1 or 0 ? center : Math.Ceiling((center - width / 2) * Y * 4 - 1e-9) / 4 / Y + width / 2;
}
