using System.Buffers.Binary;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using SkiaSharp;

namespace Fidelity.PowerPoint;

/// <summary>
/// TIFF pictures (catalog: picture.formats), which PowerPoint keeps as they are inserted: written by GDI+ (LZW RGB and
/// RGBA, palette, CCITT group 4 bilevel) and by hand as other programs write them (Deflate with horizontal prediction,
/// PackBits, big-endian 16-bit samples, JPEG in strips, CMYK).
/// </summary>
[SupportedOSPlatform("windows")]
static class PictureFormatsDeck
{
    const int MsoTrue = PowerPointSession.MsoTrue, MsoFalse = PowerPointSession.MsoFalse;
    const int W = 320, H = 240;

    public static void Build(dynamic deck)
    {
        object presentation = deck;
        var files = Write(Path.Combine(Path.GetTempPath(), "fidelity-images", "tiff"));
        var slide = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        for (var i = 0; i < files.Count; i++)
            slide.Shapes.AddPicture(files[i], MsoFalse, MsoTrue, 15f + i % 5 * 188, 60f + i / 5 * 230, 178f, 133.5f);
    }

    static List<string> Write(string directory)
    {
        Directory.CreateDirectory(directory);
        var (rgba, opaque) = (Picture(transparent: true), Picture(transparent: false));
        var files = new List<string>();
        string Path(string name)
        {
            var path = System.IO.Path.Combine(directory, name);
            files.Add(path);
            return path;
        }

        // GDI+: LZW-compressed RGB and RGBA, a palette picture and a CCITT group 4 bilevel one.
        Gdi(opaque, PixelFormat.Format24bppRgb, Path("rgb-lzw.tif"));
        Gdi(rgba, PixelFormat.Format32bppArgb, Path("rgba-lzw.tif"));
        Gdi(opaque, PixelFormat.Format8bppIndexed, Path("palette.tif"));
        Gdi(opaque, PixelFormat.Format1bppIndexed, Path("bilevel-g4.tif"));

        // By hand: Deflate with horizontal differencing, PackBits, big-endian 16-bit, JPEG strips and CMYK.
        var rgb = Samples(opaque, 3);
        File.WriteAllBytes(Path("rgb-deflate.tif"), Tiff(false, 8, 2, 3, 8, Strips(Predicted(rgb, 3), 3, 60, Deflate), extra: [(317, 2)]));
        File.WriteAllBytes(Path("rgb-packbits.tif"), Tiff(false, 8, 2, 3, 32773, Strips(rgb, 3, 40, PackBits)));
        var wide = new byte[rgb.Length * 2];
        for (var i = 0; i < rgb.Length; i++)
            BinaryPrimitives.WriteUInt16BigEndian(wide.AsSpan(2 * i), (ushort)(rgb[i] * 257));
        File.WriteAllBytes(Path("rgb16-bigendian.tif"), Tiff(true, 16, 2, 3, 1, Strips(wide, 6, H, b => b)));
        File.WriteAllBytes(Path("jpeg-strips.tif"), Tiff(false, 8, 6, 3, 7, JpegStrips(opaque, 16), extra: [(530, 2), (530, 2)]));
        var cmyk = new byte[W * H * 4];
        for (var i = 0; i < W * H; i++)
        {
            var (r, g, b) = (rgb[3 * i], rgb[3 * i + 1], rgb[3 * i + 2]);
            var k = 255 - Math.Max(r, Math.Max(g, b));
            var scale = k == 255 ? 0 : 255.0 / (255 - k);
            (cmyk[4 * i], cmyk[4 * i + 1], cmyk[4 * i + 2], cmyk[4 * i + 3]) =
                ((byte)Math.Round((255 - r - k) * scale), (byte)Math.Round((255 - g - k) * scale), (byte)Math.Round((255 - b - k) * scale), (byte)k);
        }
        File.WriteAllBytes(Path("cmyk.tif"), Tiff(false, 8, 5, 4, 1, Strips(cmyk, 4, H, b => b)));
        return files;
    }

    /// <summary>The test picture: a gradient with shapes and text, transparent around a circle when asked.</summary>
    static SKBitmap Picture(bool transparent)
    {
        var bitmap = new SKBitmap(new SKImageInfo(W, H, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(transparent ? SKColors.Transparent : SKColors.White);
        using var gradient = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(W, H),
                [new SKColor(0x00, 0x70, 0xC0), new SKColor(0xFF, 0xC0, 0x00), new SKColor(0xC0, 0x00, 0x30)], SKShaderTileMode.Clamp),
        };
        if (transparent)
            canvas.DrawCircle(W / 2f, H / 2f, 115, gradient);
        else
            canvas.DrawRect(0, 0, W, H, gradient);
        using var shapes = new SKPaint { IsAntialias = true, Color = new SKColor(255, 255, 255, 200) };
        canvas.DrawRect(30, 30, 90, 60, shapes);
        shapes.Color = new SKColor(0, 0, 0);
        canvas.DrawCircle(230, 170, 35, shapes);
        using var font = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 30);
        canvas.DrawText("TIFF", 110, 140, font, shapes);
        return bitmap;
    }

    /// <summary>Saves through GDI+, which writes TIFF with LZW compression (CCITT group 4 for bilevel pictures).</summary>
    static void Gdi(SKBitmap picture, PixelFormat format, string path)
    {
        using var bitmap = new System.Drawing.Bitmap(W, H, format);
        var data = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, W, H), ImageLockMode.WriteOnly, format);
        var palette = format == PixelFormat.Format8bppIndexed ? bitmap.Palette.Entries : [];
        var row = new byte[Math.Abs(data.Stride)];
        for (var y = 0; y < H; y++)
        {
            Array.Clear(row);
            for (var x = 0; x < W; x++)
            {
                var c = picture.GetPixel(x, y);
                switch (format)
                {
                    case PixelFormat.Format24bppRgb:
                        (row[3 * x], row[3 * x + 1], row[3 * x + 2]) = (c.Blue, c.Green, c.Red);
                        break;
                    case PixelFormat.Format32bppArgb:
                        (row[4 * x], row[4 * x + 1], row[4 * x + 2], row[4 * x + 3]) = (c.Blue, c.Green, c.Red, c.Alpha);
                        break;
                    case PixelFormat.Format8bppIndexed:
                        row[x] = (byte)Enumerable.Range(0, palette.Length).MinBy(i =>
                            Math.Pow(palette[i].R - c.Red, 2) + Math.Pow(palette[i].G - c.Green, 2) + Math.Pow(palette[i].B - c.Blue, 2));
                        break;
                    default:
                        if (c.Red * 0.3 + c.Green * 0.59 + c.Blue * 0.11 > 110)
                            row[x >> 3] |= (byte)(0x80 >> (x & 7));   // index 1: white
                        break;
                }
            }
            Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, row.Length);
        }
        bitmap.UnlockBits(data);
        var encoder = ImageCodecInfo.GetImageEncoders().First(e => e.FormatID == ImageFormat.Tiff.Guid);
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(Encoder.Compression,
            (long)(format == PixelFormat.Format1bppIndexed ? EncoderValue.CompressionCCITT4 : EncoderValue.CompressionLZW));
        bitmap.Save(path, encoder, parameters);
    }

    /// <summary>The picture's samples, row by row: RGB for 3.</summary>
    static byte[] Samples(SKBitmap picture, int count)
    {
        var samples = new byte[W * H * count];
        for (var y = 0; y < H; y++)
        {
            for (var x = 0; x < W; x++)
            {
                var c = picture.GetPixel(x, y);
                (samples[(y * W + x) * count], samples[(y * W + x) * count + 1], samples[(y * W + x) * count + 2]) = (c.Red, c.Green, c.Blue);
            }
        }
        return samples;
    }

    /// <summary>Horizontal differencing (Predictor 2) of 8-bit samples.</summary>
    static byte[] Predicted(byte[] samples, int count)
    {
        var result = (byte[])samples.Clone();
        for (var y = 0; y < H; y++)
        {
            for (var i = W * count - 1; i >= count; i--)
                result[y * W * count + i] -= samples[y * W * count + i - count];
        }
        return result;
    }

    static List<byte[]> Strips(byte[] samples, int bytesPerPixel, int rows, Func<byte[], byte[]> compress)
    {
        var strips = new List<byte[]>();
        for (var y = 0; y < H; y += rows)
            strips.Add(compress(samples.AsSpan(y * W * bytesPerPixel, Math.Min(rows, H - y) * W * bytesPerPixel).ToArray()));
        return strips;
    }

    static byte[] Deflate(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal))
            zlib.Write(data);
        return output.ToArray();
    }

    static byte[] PackBits(byte[] data)
    {
        var output = new List<byte>();
        for (var i = 0; i < data.Length;)
        {
            var run = 1;
            while (i + run < data.Length && run < 128 && data[i + run] == data[i])
                run++;
            if (run >= 3)
            {
                output.Add((byte)(1 - run));
                output.Add(data[i]);
                i += run;
                continue;
            }
            var start = i;
            while (i < data.Length && i - start < 128 && !(i + 2 < data.Length && data[i] == data[i + 1] && data[i] == data[i + 2]))
                i++;
            output.Add((byte)(i - start - 1));
            output.AddRange(data.AsSpan(start, i - start).ToArray());
        }
        return [.. output];
    }

    /// <summary>Bands of 16 rows, each a complete JPEG file with the same standard tables (4:2:0 YCbCr).</summary>
    static List<byte[]> JpegStrips(SKBitmap picture, int rows)
    {
        var strips = new List<byte[]>();
        for (var y = 0; y < H; y += rows)
        {
            using var band = new SKBitmap();
            picture.ExtractSubset(band, new SKRectI(0, y, W, Math.Min(H, y + rows)));
            using var pixmap = band.PeekPixels();
            using var data = pixmap.Encode(new SKJpegEncoderOptions(90, SKJpegEncoderDownsample.Downsample420, SKJpegEncoderAlphaOption.Ignore));
            strips.Add(data.ToArray());
        }
        return strips;
    }

    static byte[] Tiff(bool bigEndian, int bits, int photometric, int samples, int compression, List<byte[]> strips, (int Tag, int Value)[]? extra = null) =>
        Tiff(W, H, bigEndian, bits, photometric, samples, compression, strips, extra);

    /// <summary>A one-image TIFF file in the given strips (all but the last of the same rows).</summary>
    internal static byte[] Tiff(int width, int height, bool bigEndian, int bits, int photometric, int samples, int compression, List<byte[]> strips,
        (int Tag, int Value)[]? extra = null)
    {
        var rows = (height + strips.Count - 1) / strips.Count;
        var entries = new SortedDictionary<int, (int Type, long[] Values)>
        {
            [256] = (3, [width]),
            [257] = (3, [height]),
            [258] = (3, Enumerable.Repeat((long)bits, samples).ToArray()),
            [259] = (3, [compression]),
            [262] = (3, [photometric]),
            [273] = (4, new long[strips.Count]),
            [277] = (3, [samples]),
            [278] = (3, [rows]),
            [279] = (4, strips.Select(s => (long)s.Length).ToArray()),
            [282] = (5, [96, 1]),
            [283] = (5, [96, 1]),
            [296] = (3, [2]),
        };
        foreach (var group in (extra ?? []).GroupBy(e => e.Tag))
            entries[group.Key] = (3, group.Select(e => (long)e.Value).ToArray());

        var output = new MemoryStream();
        void U16(long value)
        {
            Span<byte> b = stackalloc byte[2];
            if (bigEndian) BinaryPrimitives.WriteUInt16BigEndian(b, (ushort)value); else BinaryPrimitives.WriteUInt16LittleEndian(b, (ushort)value);
            output.Write(b);
        }
        void U32(long value)
        {
            Span<byte> b = stackalloc byte[4];
            if (bigEndian) BinaryPrimitives.WriteUInt32BigEndian(b, (uint)value); else BinaryPrimitives.WriteUInt32LittleEndian(b, (uint)value);
            output.Write(b);
        }
        output.Write(bigEndian ? "MM"u8 : "II"u8);
        U16(42);
        U32(0);   // the directory's offset, written below
        for (var i = 0; i < strips.Count; i++)
        {
            entries[273].Values[i] = output.Position;
            output.Write(strips[i]);
            if (output.Position % 2 != 0)
                output.WriteByte(0);
        }
        // Values longer than four bytes go after the directory.
        var directory = output.Position;
        var after = directory + 2 + entries.Count * 12 + 4;
        var overflow = new List<(int Type, long[] Values)>();
        U16(entries.Count);
        foreach (var (tag, (type, values)) in entries)
        {
            var count = type == 5 ? values.Length / 2 : values.Length;
            var size = type switch { 3 => 2, 4 => 4, _ => 8 } * count;
            U16(tag);
            U16(type);
            U32(count);
            if (size <= 4)
            {
                foreach (var v in values)
                {
                    if (type == 3) U16(v); else U32(v);
                }
                for (var pad = size; pad < 4; pad += 2)
                    U16(0);
            }
            else
            {
                U32(after);
                overflow.Add((type, values));
                after += size;
            }
        }
        U32(0);
        foreach (var (type, values) in overflow)
        {
            foreach (var v in values)
            {
                if (type == 3) U16(v); else U32(v);
            }
        }
        var bytes = output.ToArray();
        if (bigEndian) BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), (uint)directory);
        else BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), (uint)directory);
        return bytes;
    }
}
