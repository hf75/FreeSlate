using SkiaSharp;

namespace FreeSlate.Skia;

/// <summary>
/// Pictures made ready for a document (see Editing.EditSession.InsertPicture): turned upright as their camera says (EXIF
/// orientation, which Word does not follow in a document's pictures), in a kind Word keeps as it is (JPEG, PNG and GIF stay,
/// WebP, BMP, icons and the like become PNG), and no larger in pixels than their size in the document needs at Word's default
/// resolution of 220 pixels an inch. A picture made anew is in sRGB, without the metadata of the camera (its place too).
/// </summary>
public static class PictureFit
{
    /// <summary>The pixels an inch a picture keeps at its size in the document (Word's default resolution for pictures).</summary>
    public const double Resolution = 220;

    /// <summary>How many more pixels than it needs a picture has before it is made smaller: a quarter more.</summary>
    const double Spare = 1.25;

    /// <summary>
    /// A picture upright and in a kind Word keeps: the bytes themselves where they are so already (and GIFs, which may move); null
    /// where Skia cannot read them (TIFF, which Word keeps as it is).
    /// </summary>
    public static byte[]? Upright(byte[] bytes)
    {
        using var codec = SKCodec.Create(new SKMemoryStream(bytes));
        if (codec == null)
            return null;
        var format = codec.EncodedFormat;
        if (format == SKEncodedImageFormat.Gif || format is SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png && codec.EncodedOrigin == SKEncodedOrigin.TopLeft)
            return bytes;
        using var decoded = Decode(codec, codec.Info.Width, codec.Info.Height);
        if (decoded == null)
            return null;
        using var upright = Turned(decoded, codec.EncodedOrigin);
        return Encoded(upright, format == SKEncodedImageFormat.Jpeg);
    }

    /// <summary>
    /// A picture no larger in pixels than its size in points needs at a resolution, where it has a quarter more than that across and
    /// down (a JPEG again as JPEG, a PNG as PNG, its proportions kept); else, or where it is not upright yet, the bytes themselves.
    /// </summary>
    public static byte[] Fitted(byte[] bytes, double width, double height, double resolution = Resolution)
    {
        using var codec = SKCodec.Create(new SKMemoryStream(bytes));
        if (codec == null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png) || codec.EncodedOrigin != SKEncodedOrigin.TopLeft)
            return bytes;
        var (wide, high) = (codec.Info.Width, codec.Info.Height);
        var (needWide, needHigh) = (width / 72 * resolution, height / 72 * resolution);
        if (wide <= needWide * Spare || high <= needHigh * Spare)
            return bytes;
        var scale = Math.Max(needWide / wide, needHigh / high);
        var (toWide, toHigh) = (Math.Max(1, (int)Math.Ceiling(wide * scale)), Math.Max(1, (int)Math.Ceiling(high * scale)));
        // Decoded as small as the codec can that still has the pixels (a JPEG at a half, a quarter or an eighth), then made exact.
        var first = new[] { 0.125f, 0.25f, 0.5f }.Select(s => codec.GetScaledDimensions(s)).FirstOrDefault(d => d.Width >= toWide && d.Height >= toHigh);
        using var decoded = Decode(codec, first.Width > 0 ? first.Width : wide, first.Width > 0 ? first.Height : high);
        if (decoded == null)
            return bytes;
        using var resized = decoded.Resize(new SKImageInfo(toWide, toHigh, decoded.ColorType, decoded.AlphaType, decoded.ColorSpace), new SKSamplingOptions(SKCubicResampler.Mitchell));
        if (resized == null)
            return bytes;
        var made = Encoded(resized, codec.EncodedFormat == SKEncodedImageFormat.Jpeg);
        return made != null && made.Length < bytes.Length ? made : bytes;
    }

    /// <summary>A picture's pixels at a size the codec gives, in sRGB with premultiplied alpha; null where they cannot be read.</summary>
    static SKBitmap? Decode(SKCodec codec, int width, int height)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb());
        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result is SKCodecResult.Success or SKCodecResult.IncompleteInput)
            return bitmap;
        bitmap.Dispose();
        return null;
    }

    /// <summary>A picture turned and mirrored as its EXIF orientation says it is to be seen.</summary>
    static SKBitmap Turned(SKBitmap stored, SKEncodedOrigin origin)
    {
        var (w, h) = ((float)stored.Width, (float)stored.Height);
        var across = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var seen = new SKBitmap(new SKImageInfo(across ? stored.Height : stored.Width, across ? stored.Width : stored.Height, stored.ColorType, stored.AlphaType, stored.ColorSpace));
        using var canvas = new SKCanvas(seen);
        // Where each stored pixel (x, y) is seen.
        var matrix = origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
            _ => SKMatrix.Identity,
        };
        canvas.SetMatrix(matrix);
        canvas.DrawBitmap(stored, 0, 0);
        return seen;
    }

    /// <summary>A picture's pixels as a JPEG (quality 90) or a PNG; null where they cannot be written.</summary>
    static byte[]? Encoded(SKBitmap bitmap, bool jpeg)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(jpeg ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png, jpeg ? 90 : 100);
        return data?.ToArray();
    }
}
