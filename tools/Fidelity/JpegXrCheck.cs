using SkiaSharp;

namespace Fidelity;

/// <summary>
/// Compares FreeSlate's JPEG XR decoder with reference decodings: every name.jxr in a directory against name.png
/// next to it (Windows' own codec, as 32-bit RGBA), writing our decoding as name.ours.png.
/// </summary>
public static class JpegXrCheck
{
    public static void Run(string directory)
    {
        foreach (var path in Directory.GetFiles(directory, "*.jxr").Order())
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var data = File.ReadAllBytes(path);
            var started = DateTime.UtcNow;
            int w, h;
            byte[] rgba;
            try
            {
                (w, h, rgba) = FreeSlate.Html.JpegXr.DecodeOrThrow(data);
            }
            catch (Exception e)
            {
                Console.WriteLine($"{name,-24} {e.GetType().Name}: {e.Message} {e.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}");
                continue;
            }
            var time = (DateTime.UtcNow - started).TotalMilliseconds;
            File.WriteAllBytes(Path.Combine(directory, name + ".ours.png"), FreeSlate.Metafiles.Png.Encode(w, h, rgba));
            var referencePath = Path.Combine(directory, name + ".png");
            if (!File.Exists(referencePath))
            {
                Console.WriteLine($"{name,-24} {w}x{h} decoded in {time:0} ms, no reference");
                continue;
            }
            // Decoded straight to unpremultiplied RGBA: premultiplying would lose the colour of nearly transparent pixels.
            using var codec = SKCodec.Create(referencePath);
            using var reference = SKBitmap.Decode(codec, new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            var expected = reference.Bytes;
            if (reference.Width != w || reference.Height != h)
            {
                Console.WriteLine($"{name,-24} size {w}x{h}, reference {reference.Width}x{reference.Height}");
                continue;
            }
            int max = 0, exact = 0, maxAt = 0;
            long sum = 0;
            for (var i = 0; i < rgba.Length; i++)
            {
                var d = Math.Abs(rgba[i] - expected[i]);
                sum += d;
                if (d == 0)
                    exact++;
                if (d > max)
                    (max, maxAt) = (d, i);
            }
            // The first macroblock (raster order) that differs by more than a few levels: where a parse goes astray.
            var (firstBad, badCount) = (-1, 0);
            for (var mby = 0; mby * 16 < h; mby++)
                for (var mbx = 0; mbx * 16 < w; mbx++)
                {
                    var bad = false;
                    for (var y = mby * 16; y < Math.Min(h, mby * 16 + 16) && !bad; y++)
                        for (var x = mbx * 16; x < Math.Min(w, mbx * 16 + 16) && !bad; x++)
                            for (var c = 0; c < 4; c++)
                                if (Math.Abs(rgba[(y * w + x) * 4 + c] - expected[(y * w + x) * 4 + c]) > 3)
                                    bad = true;
                    if (!bad)
                        continue;
                    badCount++;
                    if (firstBad < 0)
                        firstBad = mby * 1000 + mbx;
                }
            if (Environment.GetEnvironmentVariable("JXR_MAP") == name)
                for (var mby = 0; mby * 16 < h; mby++)
                {
                    var line = new System.Text.StringBuilder();
                    for (var mbx = 0; mbx * 16 < w; mbx++)
                    {
                        var worst = 0;
                        for (var y = mby * 16; y < Math.Min(h, mby * 16 + 16); y++)
                            for (var x = mbx * 16; x < Math.Min(w, mbx * 16 + 16); x++)
                                for (var c = 0; c < 4; c++)
                                    worst = Math.Max(worst, Math.Abs(rgba[(y * w + x) * 4 + c] - expected[(y * w + x) * 4 + c]));
                        line.Append(worst == 0 ? " ." : worst <= 3 ? " o" : $"{Math.Min(worst, 99),2}"[..2].PadLeft(3));
                    }
                    Console.WriteLine(line);
                }
            Console.WriteLine($"{name,-24} {w}x{h}  exact {exact * 100.0 / rgba.Length,7:0.000} %  mean {sum / (double)rgba.Length,6:0.000}  " +
                $"max {max,3} at ({maxAt / 4 % w},{maxAt / 4 / w}) ch {maxAt % 4}  {time,6:0} ms" +
                (firstBad >= 0 ? $"  first bad MB ({firstBad % 1000},{firstBad / 1000}) of {badCount}" : ""));
        }
    }
}
