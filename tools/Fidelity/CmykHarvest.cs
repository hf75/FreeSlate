using System.Runtime.Versioning;
using Fidelity.PowerPoint;
using SkiaSharp;

namespace Fidelity;

/// <summary>
/// Measures how PowerPoint shows CMYK pictures that bring no color profile of their own (catalog: picture.formats):
/// a TIFF of 9⁴ CMYK patches (levels 0, 32, … 255) placed on a slide, exported by PowerPoint at 3840 × 2160 and
/// every patch's center averaged. Writes src/FreeSlate/Resources/cmykToRgb.bin: RGB triplets with cyan varying
/// slowest and black fastest.
/// </summary>
[SupportedOSPlatform("windows")]
public static class CmykHarvest
{
    const int Levels = 9, Patch = 6, Columns = 81;

    public static void Run()
    {
        var size = Columns * Patch;
        var cmyk = new byte[size * size * 4];
        var count = Levels * Levels * Levels * Levels;
        static byte Level(int i) => (byte)Math.Round(i * 255.0 / (Levels - 1));
        for (var i = 0; i < count; i++)
        {
            var (row, column) = Math.DivRem(i, Columns);
            byte[] color = [Level(i / (Levels * Levels * Levels)), Level(i / (Levels * Levels) % Levels), Level(i / Levels % Levels), Level(i % Levels)];
            for (var y = 0; y < Patch; y++)
            {
                for (var x = 0; x < Patch; x++)
                    color.CopyTo(cmyk, ((row * Patch + y) * size + column * Patch + x) * 4);
            }
        }
        var directory = Path.Combine(Path.GetTempPath(), "fidelity-cmyk");
        Directory.CreateDirectory(directory);
        var tiff = Path.Combine(directory, "grid.tif");
        var strips = new List<byte[]>();
        for (var y = 0; y < size; y += 54)
            strips.Add(cmyk.AsSpan(y * size * 4, Math.Min(54, size - y) * size * 4).ToArray());
        File.WriteAllBytes(tiff, PictureFormatsDeck.Tiff(size, size, false, 8, 5, 4, 1, strips));

        // A 540 pt square on the slide, exported at four pixels per point.
        const int Width = 3840, Height = 2160;
        var png = Path.Combine(directory, "grid.png");
        using (var session = new PowerPointSession())
        {
            var presentation = session.Create();
            var slide = CorpusGenerator.AddSlide(presentation, CorpusGenerator.LayoutBlank);
            slide.Shapes.AddPicture(tiff, PowerPointSession.MsoFalse, PowerPointSession.MsoTrue, 0f, 0f, 540f, 540f);
            slide.Export(png, "PNG", Width, Height);
            PowerPointSession.Close(presentation);
        }

        using var bitmap = SKBitmap.Decode(png);
        var cell = Height / (double)Columns;
        var table = new byte[count * 3];
        for (var i = 0; i < count; i++)
        {
            var (row, column) = Math.DivRem(i, Columns);
            var (cx, cy) = ((int)((column + 0.5) * cell), (int)((row + 0.5) * cell));
            var sum = new double[3];
            for (var y = cy - 6; y < cy + 6; y++)
            {
                for (var x = cx - 6; x < cx + 6; x++)
                {
                    var c = bitmap.GetPixel(x, y);
                    (sum[0], sum[1], sum[2]) = (sum[0] + c.Red, sum[1] + c.Green, sum[2] + c.Blue);
                }
            }
            for (var k = 0; k < 3; k++)
                table[i * 3 + k] = (byte)Math.Round(sum[k] / 144);
        }
        var output = Path.Combine(Repo.Root, "src", "FreeSlate", "Resources", "cmykToRgb.bin");
        File.WriteAllBytes(output, table);
        Console.WriteLine($"{count} patches measured: {output}");
    }
}
