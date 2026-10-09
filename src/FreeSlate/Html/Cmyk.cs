namespace FreeSlate.Html;

/// <summary>
/// CMYK samples as PowerPoint shows pictures without a color profile of their own (catalog: picture.formats):
/// interpolated in a grid of 9⁴ colors measured from PowerPoint's rendering (Resources/cmykToRgb.bin, written by
/// tools/Fidelity harvest-cmyk), not the naive complement.
/// </summary>
static class Cmyk
{
    const int Levels = 9;
    static readonly Lazy<byte[]> table = new(() =>
    {
        using var stream = typeof(Cmyk).Assembly.GetManifestResourceStream("cmykToRgb.bin")
            ?? throw new InvalidOperationException("cmykToRgb.bin is missing.");
        var bytes = new byte[Levels * Levels * Levels * Levels * 3];
        stream.ReadExactly(bytes);
        return bytes;
    });

    /// <summary>The RGB color of 8-bit CMYK samples (quadrilinear interpolation between the measured colors).</summary>
    public static (int R, int G, int B) ToRgb(int c, int m, int y, int k)
    {
        var lut = table.Value;
        Span<int> i = stackalloc int[4];
        Span<double> f = stackalloc double[4];
        ReadOnlySpan<int> values = [c, m, y, k];
        for (var d = 0; d < 4; d++)
        {
            var t = Math.Clamp(values[d], 0, 255) * (Levels - 1) / 255.0;
            i[d] = Math.Min((int)t, Levels - 2);
            f[d] = t - i[d];
        }
        double r = 0, g = 0, b = 0;
        for (var corner = 0; corner < 16; corner++)
        {
            var weight = 1.0;
            var index = 0;
            for (var d = 0; d < 4; d++)
            {
                var bit = corner >> 3 - d & 1;
                weight *= bit == 1 ? f[d] : 1 - f[d];
                index = index * Levels + i[d] + bit;
            }
            if (weight == 0)
                continue;
            (r, g, b) = (r + weight * lut[index * 3], g + weight * lut[index * 3 + 1], b + weight * lut[index * 3 + 2]);
        }
        return ((int)Math.Round(r), (int)Math.Round(g), (int)Math.Round(b));
    }
}
