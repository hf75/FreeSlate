namespace FreeSlate.Metafiles;

/// <summary>
/// Where a picture lies on the output, in output pixels (a slide's CSS pixels). GDI lays pattern and hatch brushes on
/// the device's pixels, one bit a pixel whatever the picture's scale, from the device's origin; PowerPoint draws a
/// metafile with that origin 4 pixels above and left of the picture's rounded corner (measured, see BrushGrid).
/// </summary>
public readonly record struct PixelBox(double X, double Y, double Width, double Height)
{
    /// <summary>
    /// Brush pixels to the device units of a view box shown in this box: bit (0, 0) of a brush at the output pixel
    /// (round(X) − 4, round(Y) − 4). Measured in PowerPoint's rendering: generated/metafiles' 8 × 8 DIB pattern brush at
    /// x 666.67 has its origin at 663 (mod 8), its EMF+ hatch at 53.33 at 49, both at y 173.33 at 169; the 1-pixel
    /// dithers of libreoffice/sd/activex_scrollbar and activex_togglebutton and the 4-pixel pattern of generated/
    /// metafiles' WMF star (0.16 pixels a logical unit) lie as that predicts, on whole pixels at every scale. A brush
    /// pixel is a hundred-thousandth under an output pixel: Chrome draws a pattern through a bitmap of its tile's size in
    /// device pixels rounded up, and a tile a hair over its 8 pixels comes out as 9, resampled (poi/45541_Footer's
    /// checkered shadows in grey waves, 2938 units on 960 pixels written as 3.06042 units a pixel); the last brush pixel
    /// of a picture 1000 pixels wide moves by a hundredth of a pixel so.
    /// </summary>
    public Xform BrushGrid((double X, double Y, double W, double H) viewBox)
    {
        const double under = 1 - 1e-5;
        var (ux, uy) = (viewBox.W / Width * under, viewBox.H / Height * under);
        var (ox, oy) = (Math.Floor(X + 0.5) - 4 - X, Math.Floor(Y + 0.5) - 4 - Y);
        return new Xform(ux, 0, 0, uy, viewBox.X + ox * ux, viewBox.Y + oy * uy);
    }
}

/// <summary>Converts EMF, EMF+ and WMF pictures to SVG (catalog: picture.metafiles).</summary>
public static class MetafileConverter
{
    /// <summary>The picture as an SVG document, or null when the data is not a metafile we can read.</summary>
    /// <param name="displayWidth">Display width in pixels; hatch patterns are sized in output pixels.</param>
    /// <param name="word">Whether the picture is a Word document's, whose EMF frames map as Word maps them (see EmfPlayer).</param>
    public static string? ToSvg(byte[] data, string idPrefix = "m", double? displayWidth = null, bool word = false) =>
        ToSvg(data, idPrefix, displayWidth, word, null, out _);

    /// <summary>
    /// The picture as an SVG document whose pattern and hatch brushes lie on the pixels of <paramref name="box"/> (see
    /// PixelBox); <paramref name="placed"/> tells whether the document has such brushes, which depend on the box.
    /// </summary>
    public static string? ToSvg(byte[] data, string idPrefix, double? displayWidth, bool word, PixelBox? box, out bool placed)
    {
        placed = false;
        try
        {
            if (EmfPlayer.IsEmf(data))
                return EmfPlayer.ToSvg(data, idPrefix, displayWidth, word, box, out placed);
            if (WmfPlayer.IsWmf(data))
                return WmfPlayer.ToSvg(data, idPrefix, displayWidth, box, out placed);
        }
        catch (Exception e) when (e is IndexOutOfRangeException or ArgumentException or InvalidOperationException or OverflowException)
        {
            placed = false;
        }
        return null;
    }

    public static string? ToDataUri(byte[] data, string idPrefix = "m", double? displayWidth = null, bool word = false) =>
        DataUri(ToSvg(data, idPrefix, displayWidth, word));

    public static string? DataUri(string? svg) =>
        svg != null ? "data:image/svg+xml;base64," + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svg)) : null;
}
