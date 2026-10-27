namespace FreeSlate.Metafiles;

/// <summary>Converts EMF, EMF+ and WMF pictures to SVG (catalog: picture.metafiles).</summary>
public static class MetafileConverter
{
    /// <summary>The picture as an SVG document, or null when the data is not a metafile we can read.</summary>
    /// <param name="displayWidth">Display width in pixels; hatch patterns are sized in output pixels.</param>
    /// <param name="word">Whether the picture is a Word document's, whose EMF frames map as Word maps them (see EmfPlayer).</param>
    public static string? ToSvg(byte[] data, string idPrefix = "m", double? displayWidth = null, bool word = false)
    {
        try
        {
            if (EmfPlayer.IsEmf(data))
                return EmfPlayer.ToSvg(data, idPrefix, displayWidth, word);
            if (WmfPlayer.IsWmf(data))
                return WmfPlayer.ToSvg(data, idPrefix, displayWidth);
        }
        catch (Exception e) when (e is IndexOutOfRangeException or ArgumentException or InvalidOperationException or OverflowException)
        {
        }
        return null;
    }

    public static string? ToDataUri(byte[] data, string idPrefix = "m", double? displayWidth = null, bool word = false) =>
        ToSvg(data, idPrefix, displayWidth, word) is { } svg ? "data:image/svg+xml;base64," + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svg)) : null;
}
