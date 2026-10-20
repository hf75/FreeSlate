using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Fidelity.Word;

/// <summary>
/// Brings the premultiplied pictures of Word's PDFs to straight alpha before Windows' PDF renderer rasterizes them.
/// Word stores a picture with transparency premultiplied with its soft mask's /Matte color (black), and the renderer
/// divides every pixel by its alpha but the last: a picture of one pixel stretched over a shape comes out all dark
/// (LibreOffice's extras-layout/footer-nowrap-object: a 1 × 1 PNG of 255, 0, 0 at half opacity rasterized as 191,
/// 128, 128 over white, where Word shows 255, 128, 128). Uncompressed pictures, as Word writes those of a few pixels,
/// are divided by their alpha in place and their mask's /Matte blanked, so the file keeps its offsets; compressed
/// ones, where only that one pixel is off, stay as they are.
/// </summary>
public static partial class PdfImages
{
    [GeneratedRegex(@"(\d+) 0 obj\s*<<([^<>]*)>>\s*stream\r?\n")]
    private static partial Regex FlatStream();

    [GeneratedRegex(@"/Matte\s*\[([^\]]*)\]")]
    private static partial Regex Matte();

    /// <summary>
    /// A copy of the PDF at <paramref name="path"/> with its uncompressed premultiplied pictures in straight alpha, or
    /// null when it has none.
    /// </summary>
    public static byte[]? Unpremultiplied(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var text = Encoding.Latin1.GetString(bytes);
        if (!text.Contains("/Matte", StringComparison.Ordinal))
            return null;
        var streams = FlatStream().Matches(text).ToDictionary(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
        var changed = 0;
        foreach (var (_, image) in streams)
        {
            var dictionary = image.Groups[2].Value;
            if (!dictionary.Contains("/Image", StringComparison.Ordinal) || dictionary.Contains("/Filter", StringComparison.Ordinal)
                || Reference(dictionary, "SMask") is not { } maskNumber || !streams.TryGetValue(maskNumber, out var mask)
                || Matte().Match(mask.Groups[2].Value) is not { Success: true } matte
                || mask.Groups[2].Value.Contains("/Filter", StringComparison.Ordinal)
                || Number(dictionary, "BitsPerComponent") != 8 || Number(mask.Groups[2].Value, "BitsPerComponent") != 8)
                continue;
            var components = dictionary.Contains("/DeviceRGB", StringComparison.Ordinal) ? 3 : dictionary.Contains("/DeviceGray", StringComparison.Ordinal) ? 1 : 0;
            var (width, height, length) = (Number(dictionary, "Width") ?? 0, Number(dictionary, "Height") ?? 0, Number(dictionary, "Length") ?? -1);
            var colors = matte.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(v => double.Parse(v, CultureInfo.InvariantCulture) * 255).ToArray();
            if (components == 0 || colors.Length != components || width * height * components != length
                || Number(mask.Groups[2].Value, "Length") != width * height)
                continue;
            var (data, alpha) = (image.Index + image.Length, mask.Index + mask.Length);
            for (var i = 0; i < width * height; i++)
            {
                var a = bytes[alpha + i] / 255.0;
                for (var c = 0; c < components; c++)
                {
                    ref var value = ref bytes[data + i * components + c];
                    value = a > 0 ? (byte)Math.Clamp(Math.Round(colors[c] + (value - colors[c]) / a), 0, 255) : (byte)0;
                }
            }
            // The mask's /Matte goes, its length kept in spaces.
            var at = mask.Groups[2].Index + matte.Index;
            for (var k = 0; k < matte.Length; k++)
                bytes[at + k] = (byte)' ';
            changed++;
        }
        return changed > 0 ? bytes : null;
    }

    /// <summary>A direct whole number of the dictionary (not a reference to an object).</summary>
    static int? Number(string dictionary, string key) =>
        Regex.Match(dictionary, $@"/{key}\s+(\d+)\b(?!\s+\d+\s+R)") is { Success: true } m ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : null;

    /// <summary>The number of the object a key of the dictionary refers to.</summary>
    static int? Reference(string dictionary, string key) =>
        Regex.Match(dictionary, $@"/{key}\s+(\d+)\s+0\s+R") is { Success: true } m ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : null;
}
