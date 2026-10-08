using SkiaSharp;

namespace Fidelity.PowerPoint;

/// <summary>Test pictures for the objects deck, generated so that the corpus needs no external image files.</summary>
public static class TestImages
{
    public sealed record Files(string Png, string Jpeg, string Svg);

    public static Files Write(string directory)
    {
        Directory.CreateDirectory(directory);
        var png = Path.Combine(directory, "test.png");
        var jpeg = Path.Combine(directory, "test.jpg");
        var svg = Path.Combine(directory, "test.svg");

        // PNG with transparency: overlapping colored circles on a transparent background.
        using (var surface = SKSurface.Create(new SKImageInfo(400, 300, SKColorType.Rgba8888, SKAlphaType.Premul)))
        {
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            using var paint = new SKPaint { IsAntialias = true };
            SKColor[] colors = [new(0xC0, 0x00, 0x00, 0xE0), new(0x00, 0x70, 0xC0, 0xE0), new(0x00, 0xB0, 0x50, 0xE0)];
            for (var i = 0; i < 3; i++)
            {
                paint.Color = colors[i];
                canvas.DrawCircle(120 + i * 80, 150, 100, paint);
            }
            paint.Color = SKColors.Black;
            canvas.DrawRect(10, 10, 380, 6, paint);
            Save(surface, png, SKEncodedImageFormat.Png);
        }

        // JPEG: an opaque gradient with a grid, photo-like content for compression.
        using (var surface = SKSurface.Create(new SKImageInfo(480, 320)))
        {
            var canvas = surface.Canvas;
            using var gradient = new SKPaint
            {
                Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(480, 320),
                    [new SKColor(0xFF, 0xC0, 0x00), new SKColor(0x70, 0x30, 0xA0)], SKShaderTileMode.Clamp),
            };
            canvas.DrawRect(0, 0, 480, 320, gradient);
            using var grid = new SKPaint { Color = new SKColor(255, 255, 255, 120), StrokeWidth = 3, IsAntialias = true };
            for (var x = 0; x < 480; x += 40) canvas.DrawLine(x, 0, x, 320, grid);
            for (var y = 0; y < 320; y += 40) canvas.DrawLine(0, y, 480, y, grid);
            Save(surface, jpeg, SKEncodedImageFormat.Jpeg);
        }

        File.WriteAllText(svg,
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\" width=\"100\" height=\"100\">" +
            "<circle cx=\"50\" cy=\"50\" r=\"46\" fill=\"#0070C0\"/>" +
            "<path d=\"M50 14 L60 40 L88 40 L65 57 L74 84 L50 67 L26 84 L35 57 L12 40 L40 40 Z\" fill=\"#FFC000\" stroke=\"#7F6000\" stroke-width=\"2\"/>" +
            "</svg>");
        return new Files(png, jpeg, svg);
    }

    static void Save(SKSurface surface, string path, SKEncodedImageFormat format)
    {
        using var image = surface.Snapshot();
        using var data = image.Encode(format, 90);
        File.WriteAllBytes(path, data.ToArray());
    }
}
