using Avalonia.Media.Imaging;
using SkiaSharp;

namespace FreeSlate.Editor.Writing;

/// <summary>
/// The looks a picture takes by its colors (Word's Recolor, see DrawingFormatting.Recolor), as the editor offers them: their keys and the
/// keys of their names among the texts; and a picture's pixels in each, to show it small or round its crop. The document's own pixels are
/// the engine's (see Html.BlipEffects), whose formulas these follow: luma of Rec. 709 on the sRGB values, black below half of it and white
/// from there, a washout's slope of 0.3 and rise of 0.805.
/// </summary>
public static class Looks
{
    public static readonly IReadOnlyList<(string Recolor, string NameKey)> All =
    [
        ("none", "RecolorNone"),
        ("grayscale", "RecolorGrayscale"),
        ("blackWhite", "RecolorBlackWhite"),
        ("washout", "RecolorWashout"),
    ];

    /// <summary>
    /// A part of a picture (see PictureCrop) in a look, as a bitmap no larger than given, its proportions kept; null where the bytes are
    /// no picture Skia reads (a TIFF).
    /// </summary>
    public static Bitmap? Shown(byte[] bytes, PictureCrop part, string? recolor, int width, int height)
    {
        using var decoded = SKBitmap.Decode(bytes);
        if (decoded == null || decoded.Width == 0 || decoded.Height == 0 || part.Wide <= 0 || part.High <= 0)
            return null;
        var source = SKRect.Create((float)(Math.Max(0, part.Left) * decoded.Width), (float)(Math.Max(0, part.Top) * decoded.Height),
            (float)(Math.Min(1, part.Wide) * decoded.Width), (float)(Math.Min(1, part.High) * decoded.Height));
        var scale = Math.Min(1, Math.Min(width / (double)source.Width, height / (double)source.Height));
        var (w, h) = (Math.Max(1, (int)Math.Round(source.Width * scale)), Math.Max(1, (int)Math.Round(source.Height * scale)));
        using var shown = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using (var canvas = new SKCanvas(shown))
        using (var image = SKImage.FromBitmap(decoded))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawImage(image, source, SKRect.Create(w, h), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        }
        Recolor(shown, recolor);
        using var encoded = shown.Encode(SKEncodedImageFormat.Png, 100);
        return encoded != null ? new Bitmap(encoded.AsStream()) : null;
    }

    /// <summary>A bitmap's pixels in a look (see the class), changed in place; "none" and others leave them.</summary>
    static void Recolor(SKBitmap bitmap, string? recolor)
    {
        if (recolor is not ("grayscale" or "blackWhite" or "washout"))
            return;
        var pixels = bitmap.Pixels;
        for (var i = 0; i < pixels.Length; i++)
        {
            var c = pixels[i];
            if (recolor == "washout")
            {
                static byte Washed(byte v) => (byte)Math.Clamp(Math.Round((0.3 * v / 255 + 0.805) * 255), 0, 255);
                pixels[i] = new SKColor(Washed(c.Red), Washed(c.Green), Washed(c.Blue), c.Alpha);
                continue;
            }
            var luma = (0.2126 * c.Red + 0.7152 * c.Green + 0.0722 * c.Blue) / 255;
            var v = (byte)(recolor == "blackWhite" ? luma >= 0.5 ? 255 : 0 : Math.Clamp(Math.Round(luma * 255), 0, 255));
            pixels[i] = new SKColor(v, v, v, c.Alpha);
        }
        bitmap.Pixels = pixels;
    }
}
