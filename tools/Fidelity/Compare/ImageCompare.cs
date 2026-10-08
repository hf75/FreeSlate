using System.Runtime.InteropServices;
using SkiaSharp;

namespace Fidelity.Compare;

/// <summary>Similarity of one slide rendering with its reference.</summary>
/// <param name="Score">Share of "ink" pixels (pixels that differ from the background in either image) that match
/// the reference within a small color tolerance, allowing 1 px displacement; 0..1.</param>
/// <param name="Ssim">Mean structural similarity of the grayscale images (8×8 windows); 0..1.</param>
public sealed record SlideScore(double Score, double Ssim, int InkPixels);

/// <summary>Compares two renderings of the same slide and draws a difference image.</summary>
public static class ImageCompare
{
    const int BackgroundTolerance = 24;
    const int MatchTolerance = 40;

    public static SlideScore Compare(string referencePath, string candidatePath, string diffPath)
    {
        var (w, h, reference) = Load(referencePath);
        var (cw, ch, candidate) = Load(candidatePath);
        if (cw != w || ch != h)
            candidate = Resize(candidatePath, w, h);

        var background = DominantColor(reference);
        var diff = new byte[w * h * 4];
        long ink = 0, matched = 0;

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var i = (y * w + x) * 4;
                var refInk = Distance(reference, i, background) > BackgroundTolerance;
                var candInk = Distance(candidate, i, background) > BackgroundTolerance;

                // Faded reference as backdrop.
                var gray = (byte)(255 - (255 - Luma(reference, i)) / 4);
                diff[i] = gray; diff[i + 1] = gray; diff[i + 2] = gray; diff[i + 3] = 255;
                if (!refInk && !candInk)
                    continue;

                ink++;
                if (BestMatch(reference, candidate, w, h, x, y) <= MatchTolerance)
                {
                    matched++;
                    continue;
                }
                // Red: missing or wrong in our rendering; blue: present only in ours.
                (diff[i], diff[i + 1], diff[i + 2]) = refInk ? ((byte)220, (byte)30, (byte)30) : ((byte)30, (byte)90, (byte)230);
            }
        }

        Save(diffPath, w, h, diff);
        return new SlideScore(ink == 0 ? 1 : (double)matched / ink, Ssim(reference, candidate, w, h), (int)ink);
    }

    /// <summary>Smallest distance between the reference pixel and the candidate pixels around it (3×3).</summary>
    static int BestMatch(byte[] reference, byte[] candidate, int w, int h, int x, int y)
    {
        var best = int.MaxValue;
        var i = (y * w + x) * 4;
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                var nx = x + dx;
                var ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                    continue;
                var j = (ny * w + nx) * 4;
                var d = Math.Max(Math.Abs(reference[i] - candidate[j]), Math.Max(Math.Abs(reference[i + 1] - candidate[j + 1]), Math.Abs(reference[i + 2] - candidate[j + 2])));
                best = Math.Min(best, d);
            }
        }
        return best;
    }

    static int Distance(byte[] pixels, int i, (byte R, byte G, byte B) c) =>
        Math.Max(Math.Abs(pixels[i] - c.R), Math.Max(Math.Abs(pixels[i + 1] - c.G), Math.Abs(pixels[i + 2] - c.B)));

    static int Luma(byte[] p, int i) => (299 * p[i] + 587 * p[i + 1] + 114 * p[i + 2]) / 1000;

    static (byte R, byte G, byte B) DominantColor(byte[] pixels)
    {
        var counts = new Dictionary<int, int>();
        for (var i = 0; i < pixels.Length; i += 4 * 7)
        {
            var key = ((pixels[i] >> 3) << 10) | ((pixels[i + 1] >> 3) << 5) | (pixels[i + 2] >> 3);
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }
        var top = counts.MaxBy(kv => kv.Value).Key;
        return ((byte)(((top >> 10) & 31) << 3 | 4), (byte)(((top >> 5) & 31) << 3 | 4), (byte)((top & 31) << 3 | 4));
    }

    static double Ssim(byte[] a, byte[] b, int w, int h)
    {
        const double c1 = 6.5025, c2 = 58.5225;   // (0.01·255)², (0.03·255)²
        double total = 0;
        var windows = 0;
        for (var y = 0; y + 8 <= h; y += 8)
        {
            for (var x = 0; x + 8 <= w; x += 8)
            {
                double ma = 0, mb = 0;
                for (var j = 0; j < 8; j++)
                    for (var i = 0; i < 8; i++)
                    {
                        var k = ((y + j) * w + x + i) * 4;
                        ma += Luma(a, k);
                        mb += Luma(b, k);
                    }
                ma /= 64; mb /= 64;
                double va = 0, vb = 0, cov = 0;
                for (var j = 0; j < 8; j++)
                    for (var i = 0; i < 8; i++)
                    {
                        var k = ((y + j) * w + x + i) * 4;
                        var da = Luma(a, k) - ma;
                        var db = Luma(b, k) - mb;
                        va += da * da; vb += db * db; cov += da * db;
                    }
                va /= 63; vb /= 63; cov /= 63;
                total += (2 * ma * mb + c1) * (2 * cov + c2) / ((ma * ma + mb * mb + c1) * (va + vb + c2));
                windows++;
            }
        }
        return windows == 0 ? 1 : total / windows;
    }

    static (int W, int H, byte[] Pixels) Load(string path)
    {
        using var codec = SKCodec.Create(path) ?? throw new InvalidDataException($"Cannot decode {path}");
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = SKBitmap.Decode(codec, info);
        return (info.Width, info.Height, Pixels(bitmap));
    }

    static byte[] Resize(string path, int w, int h)
    {
        using var codec = SKCodec.Create(path)!;
        using var decoded = SKBitmap.Decode(codec, new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using var resized = decoded.Resize(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul), new SKSamplingOptions(SKFilterMode.Linear));
        return Pixels(resized);
    }

    static byte[] Pixels(SKBitmap bitmap)
    {
        var pixels = new byte[bitmap.Width * bitmap.Height * 4];
        Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length);
        return pixels;
    }

    static void Save(string path, int w, int h, byte[] pixels)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }
}
