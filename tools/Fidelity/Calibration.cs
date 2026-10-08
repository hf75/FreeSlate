using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using Fidelity.PowerPoint;
using SkiaSharp;

namespace Fidelity;

/// <summary>A measured line of "HHHH": cap top and baseline in points from the top of the slide.</summary>
public sealed record MeasuredLine(double CapTop, double Baseline);

public sealed record MeasuredBox(int Slide, int Column, List<MeasuredLine> Lines);

/// <summary>
/// Measures PowerPoint's text line model on the text-metrics deck: renders it at four times the normal
/// resolution and finds the cap top and baseline of every line with sub-pixel precision.
/// </summary>
public static class Calibration
{
    const int Scale = 4;
    const double SlideWidthPt = 960;

    public static string Dir => Path.Combine(Repo.Artifacts, "calibration");

    [SupportedOSPlatform("windows")]
    public static void Render()
    {
        using var session = new PowerPointSession();
        session.ExportSlides(Repo.DeckFile("generated/text-metrics"), Dir, 1280 * Scale);
    }

    public static List<MeasuredBox> Measure()
    {
        var boxes = new List<MeasuredBox>();
        foreach (var file in Directory.GetFiles(Dir, "slide-*.png").Order(StringComparer.Ordinal))
        {
            var slide = int.Parse(Path.GetFileNameWithoutExtension(file)["slide-".Length..]);
            using var codec = SKCodec.Create(file);
            var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            using var bitmap = SKBitmap.Decode(codec, info);
            var pixels = new byte[info.Width * info.Height * 4];
            Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length);
            var pxPerPt = info.Width / SlideWidthPt;

            for (var column = 0; column < MetricsDeck.Columns.Length; column++)
            {
                var left = MetricsDeck.Columns[column];
                var x0 = (int)((left + 2) * pxPerPt);
                var x1 = (int)((left + MetricsDeck.Width - 2) * pxPerPt);
                var lines = Lines(pixels, info.Width, info.Height, x0, x1)
                    .Select(l => new MeasuredLine(Math.Round(l.Top / pxPerPt, 3), Math.Round(l.Bottom / pxPerPt, 3)))
                    .ToList();
                boxes.Add(new MeasuredBox(slide, column, lines));
            }
        }
        File.WriteAllText(Path.Combine(Dir, "measurements.json"), JsonSerializer.Serialize(boxes, new JsonSerializerOptions { WriteIndented = true }));
        return boxes;
    }

    /// <summary>Dark bands between x0 and x1 with sub-pixel top and bottom edges (pixel rows cover [y, y+1)).</summary>
    static IEnumerable<(double Top, double Bottom)> Lines(byte[] pixels, int width, int height, int x0, int x1)
    {
        var darkness = new double[height];
        for (var y = 0; y < height; y++)
        {
            double sum = 0;
            for (var x = x0; x < x1; x++)
            {
                var i = (y * width + x) * 4;
                sum += (255 - (299 * pixels[i] + 587 * pixels[i + 1] + 114 * pixels[i + 2]) / 1000) / 255.0;
            }
            darkness[y] = sum;
        }

        var threshold = darkness.Max() * 0.02;
        for (var y = 0; y < height; y++)
        {
            if (darkness[y] <= threshold)
                continue;
            var top = y;
            while (y < height && darkness[y] > threshold)
                y++;
            var bottom = y - 1;
            if (bottom - top < 6)
                continue;   // noise
            // Partial coverage of the edge rows relative to their full neighbours.
            var topEdge = top + 1 - Math.Min(1, darkness[top] / Math.Max(1e-9, darkness[top + 2]));
            var bottomEdge = bottom + Math.Min(1, darkness[bottom] / Math.Max(1e-9, darkness[bottom - 2]));
            yield return (topEdge, bottomEdge);
        }
    }
}
