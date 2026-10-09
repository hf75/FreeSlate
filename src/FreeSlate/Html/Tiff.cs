using System.Buffers.Binary;
using System.IO.Compression;

namespace FreeSlate.Html;

/// <summary>
/// A TIFF decoder (catalog: picture.formats): the first image of a file, in strips or tiles, uncompressed, PackBits,
/// LZW or Deflate compressed (with horizontal prediction), CCITT fax coded or JPEG compressed, as bilevel, grayscale,
/// palette, RGB(A) or CMYK samples of 1 to 16 bits. Browsers do not show TIFF, so pictures are converted to PNG, and
/// JPEG-compressed ones to JPEG.
/// </summary>
public static class Tiff
{
    public static bool IsTiff(ReadOnlySpan<byte> data) =>
        data.Length >= 8 && (data[0] == 'I' && data[1] == 'I' && data[2] == 42 && data[3] == 0
            || data[0] == 'M' && data[1] == 'M' && data[2] == 0 && data[3] == 42);

    /// <summary>The pixel size and resolution (dots per inch, 96 when unknown) of the first image, or zeros.</summary>
    public static (int Width, int Height, double DpiX, double DpiY) Info(byte[] data)
    {
        try
        {
            if (Directory(data) is not { } ifd)
                return default;
            var scale = ifd.Tag(296, 2) switch { 2 => 1, 3 => 2.54, _ => 0 };
            var (x, y) = (ifd.Tag(282, 0) * scale, ifd.Tag(283, 0) * scale);
            return ((int)ifd.Tag(256, 0), (int)ifd.Tag(257, 0), x > 0 ? x : 96, y > 0 ? y : 96);
        }
        catch (Exception e) when (e is IndexOutOfRangeException or ArgumentException)
        {
            return default;
        }
    }

    /// <summary>The first image as top-down RGBA rows, or null when the file is not a TIFF this decoder reads.</summary>
    public static (int Width, int Height, byte[] Rgba)? Decode(byte[] data)
    {
        try
        {
            return Directory(data) is { } ifd ? Read(data, ifd) : null;
        }
        catch (Exception e) when (e is IndexOutOfRangeException or ArgumentException or InvalidDataException or OverflowException)
        {
            return null;
        }
    }

    /// <summary>The first image as a PNG file (with the file's RGB color profile), or null.</summary>
    public static byte[]? ToPng(byte[] data)
    {
        if (Decode(data) is not var (w, h, rgba))
            return null;
        var profile = Directory(data) is { } ifd ? Profile(ifd) : null;
        return Metafiles.Png.Encode(w, h, rgba, profile is { Length: > 20 } && profile.AsSpan(16, 4).SequenceEqual("RGB "u8) ? profile : null);
    }

    /// <summary>
    /// The first image in a format browsers show: JPEG-compressed images as JPEG files, the others as PNG, both with
    /// the file's color profile (PowerPoint honors profiles, as browsers do); null when the file is not a TIFF this
    /// decoder reads.
    /// </summary>
    public static (string Type, byte[] Data)? ToWeb(byte[] data)
    {
        try
        {
            if (Directory(data) is { } ifd && ifd.Tag(259, 1) == 7)
                return Jpeg(data, ifd) is { } jpeg ? ("image/jpeg", jpeg) : null;
        }
        catch (Exception e) when (e is IndexOutOfRangeException or ArgumentException or OverflowException)
        {
            return null;
        }
        return ToPng(data) is { } png ? ("image/png", png) : null;
    }

    /// <summary>The embedded ICC profile (tag 34675), or null.</summary>
    static byte[]? Profile(Ifd ifd) => ifd.Values(34675) is { Length: > 128 } values ? [.. values.Select(v => (byte)v)] : null;

    /// <summary>An image file directory: every tag's values as numbers (rationals divided out).</summary>
    sealed class Ifd(Dictionary<int, double[]> tags, bool little)
    {
        public bool Little => little;
        public bool Has(int tag) => tags.ContainsKey(tag);
        public double Tag(int tag, double fallback) => tags.TryGetValue(tag, out var v) && v.Length > 0 ? v[0] : fallback;
        public double[] Values(int tag) => tags.TryGetValue(tag, out var v) ? v : [];
    }

    static Ifd? Directory(byte[] data)
    {
        if (!IsTiff(data))
            return null;
        var little = data[0] == 'I';
        ushort U16(long at) => little ? BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(checked((int)at)))
            : BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(checked((int)at)));
        uint U32(long at) => little ? BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(checked((int)at)))
            : BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(checked((int)at)));
        long offset = U32(4);
        var count = U16(offset);
        var tags = new Dictionary<int, double[]>();
        for (var i = 0; i < count; i++)
        {
            var entry = offset + 2 + i * 12L;
            var type = U16(entry + 2);
            long n = U32(entry + 4);
            var size = type switch { 1 or 2 or 6 or 7 => 1, 3 or 8 => 2, 4 or 9 or 11 or 13 => 4, 5 or 10 or 12 => 8, _ => 0 };
            if (size == 0 || n == 0 || n > 1 << 24)
                continue;
            long at = size * n <= 4 ? entry + 8 : U32(entry + 8);
            if (at + size * n > data.Length)
                continue;
            var values = new double[n];
            for (var k = 0; k < n; k++)
            {
                values[k] = type switch
                {
                    1 or 2 or 7 => data[at + k],
                    6 => (sbyte)data[at + k],
                    3 => U16(at + 2 * k),
                    8 => (short)U16(at + 2 * k),
                    4 or 13 => U32(at + 4 * k),
                    9 => (int)U32(at + 4 * k),
                    5 => U32(at + 8 * k) / (double)Math.Max(1u, U32(at + 8 * k + 4)),
                    10 => (int)U32(at + 8 * k) / (double)Math.Max(1, (int)U32(at + 8 * k + 4)),
                    _ => 0,
                };
            }
            tags[U16(entry)] = values;
        }
        return new Ifd(tags, little);
    }

    static (int, int, byte[])? Read(byte[] data, Ifd ifd)
    {
        var width = (int)ifd.Tag(256, 0);
        var height = (int)ifd.Tag(257, 0);
        var compression = (int)ifd.Tag(259, 1);
        var photometric = (int)ifd.Tag(262, 1);
        var samples = (int)ifd.Tag(277, 1);
        var bits = (int)ifd.Tag(258, 1);
        var extra = (int)ifd.Tag(338, 0);   // 1 associated (premultiplied), 2 unassociated alpha
        var colorSamples = photometric switch { 2 => 3, 5 => 4, _ => 1 };
        var stride = (width * samples * bits + 7) / 8;
        var fax = compression is 2 or 3 or 4;
        if (width <= 0 || height <= 0 || (long)width * height > 100_000_000 || (long)stride * height > int.MaxValue / 2
            || samples < colorSamples || samples > 8 || bits is not (1 or 2 or 4 or 8 or 16) || fax && (bits != 1 || samples != 1)
            || compression is not (1 or 2 or 3 or 4 or 5 or 8 or 32773 or 32946) || ifd.Tag(284, 1) != 1 || ifd.Tag(339, 1) != 1)
            return null;

        // Strips or tiles, decompressed into rows of samples.
        var tiled = ifd.Has(322);
        var blockW = tiled ? (int)ifd.Tag(322, 0) : width;
        var blockH = tiled ? (int)ifd.Tag(323, 0) : (int)Math.Min(ifd.Tag(278, height), height);
        if (blockW <= 0 || blockH <= 0)
            return null;
        var offsets = ifd.Values(tiled ? 324 : 273);
        var counts = ifd.Values(tiled ? 325 : 279);
        var blockStride = (blockW * samples * bits + 7) / 8;
        var across = (width + blockW - 1) / blockW;
        var pixels = new byte[stride * height];
        for (var b = 0; b < offsets.Length; b++)
        {
            var start = (long)offsets[b];
            var length = Math.Min(b < counts.Length ? (long)counts[b] : long.MaxValue, data.Length - start);
            var (x0, y0) = (b % across * blockW, b / across * blockH);
            if (start <= 0 || length <= 0 || y0 >= height || x0 * samples * bits % 8 != 0)
                continue;
            var input = data.AsSpan((int)start, (int)length);
            if (ifd.Tag(266, 1) == 2)
            {
                // FillOrder 2: the least significant bit of every byte comes first.
                var reversed = input.ToArray();
                for (var i = 0; i < reversed.Length; i++)
                    reversed[i] = (byte)((reversed[i] * 0x0202020202UL & 0x010884422010UL) % 1023);
                input = reversed;
            }
            var block = fax ? Fax.Decode(input, blockW, Math.Min(blockH, height - y0), compression, (int)ifd.Tag(292, 0))
                : Decompress(input, compression, blockStride * blockH);
            if (ifd.Tag(317, 1) == 2)
                Unpredict(block, blockStride, samples, bits, ifd.Little);
            var rowBytes = (Math.Min(blockW, width - x0) * samples * bits + 7) / 8;
            for (var y = 0; y < blockH && y0 + y < height; y++)
            {
                var n = Math.Min(rowBytes, block.Length - y * blockStride);
                if (n <= 0)
                    break;
                Array.Copy(block, y * blockStride, pixels, (y0 + y) * stride + x0 * samples * bits / 8, n);
            }
        }

        // Samples to RGBA.
        var max = (1 << Math.Min(bits, 8)) - 1;
        int Sample(int x, int y, int s)
        {
            var bit = (long)(x * samples + s) * bits;
            var at = y * stride + (int)(bit >> 3);
            return bits switch
            {
                8 => pixels[at],
                16 => pixels[ifd.Little ? at + 1 : at],   // the high byte
                _ => pixels[at] >> 8 - bits - (int)(bit & 7) & max,
            };
        }
        int Level(int x, int y, int s) => Sample(x, y, s) * 255 / max;
        var map = ifd.Values(320);
        var colors = map.Length / 3;
        if (photometric is not (0 or 1 or 2 or 5) && !(photometric == 3 && colors > 0))
            return null;
        var alpha = samples > colorSamples && extra is 1 or 2;
        var rgba = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                int r, g, b;
                switch (photometric)
                {
                    case 0:
                        r = g = b = 255 - Level(x, y, 0);
                        break;
                    case 1:
                        r = g = b = Level(x, y, 0);
                        break;
                    case 2:
                        (r, g, b) = (Level(x, y, 0), Level(x, y, 1), Level(x, y, 2));
                        break;
                    case 3:
                    {
                        var i = Sample(x, y, 0);
                        (r, g, b) = i < colors ? ((int)map[i] >> 8, (int)map[colors + i] >> 8, (int)map[2 * colors + i] >> 8) : (0, 0, 0);
                        break;
                    }
                    default:
                        // CMYK as PowerPoint shows it without a profile (embedded CMYK profiles are not applied).
                        (r, g, b) = Cmyk.ToRgb(Level(x, y, 0), Level(x, y, 1), Level(x, y, 2), Level(x, y, 3));
                        break;
                }
                var a = alpha ? Level(x, y, colorSamples) : 255;
                if (extra == 1 && a is > 0 and < 255)
                    (r, g, b) = (Math.Min(255, r * 255 / a), Math.Min(255, g * 255 / a), Math.Min(255, b * 255 / a));
                var o = (y * width + x) * 4;
                (rgba[o], rgba[o + 1], rgba[o + 2], rgba[o + 3]) = ((byte)r, (byte)g, (byte)b, (byte)a);
            }
        }
        return (width, height, rgba);
    }

    /// <summary>A JPEG-compressed image (compression 7) in strips as one JPEG file; null for tiles.</summary>
    static byte[]? Jpeg(byte[] data, Ifd ifd)
    {
        var (width, height) = ((int)ifd.Tag(256, 0), (int)ifd.Tag(257, 0));
        var offsets = ifd.Values(273);
        var counts = ifd.Values(279);
        if (ifd.Has(322) || offsets.Length == 0 || counts.Length < offsets.Length || width <= 0 || height <= 0)
            return null;
        var strips = new List<byte[]>();
        for (var i = 0; i < offsets.Length; i++)
        {
            var (start, length) = ((long)offsets[i], Math.Min((long)counts[i], data.Length - (long)offsets[i]));
            if (start <= 0 || length <= 0)
                return null;
            strips.Add(data.AsSpan((int)start, (int)length).ToArray());
        }
        var tables = ifd.Values(347);
        return TiffJpeg.Merge(strips, tables.Length > 4 ? [.. tables.Select(v => (byte)v)] : null, width, height,
            (int)Math.Min(ifd.Tag(278, height), height), rgb: ifd.Tag(262, 1) == 2, Profile(ifd));
    }

    static byte[] Decompress(ReadOnlySpan<byte> input, int compression, int expected) => compression switch
    {
        5 => Lzw(input, expected),
        8 or 32946 => Inflate(input),
        32773 => PackBits(input, expected),
        _ => input.ToArray(),
    };

    static byte[] Inflate(ReadOnlySpan<byte> input)
    {
        using var source = new MemoryStream(input.ToArray());
        using var zlib = new ZLibStream(source, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        return output.ToArray();
    }

    static byte[] PackBits(ReadOnlySpan<byte> input, int expected)
    {
        var output = new byte[expected];
        var used = 0;
        for (var i = 0; i < input.Length && used < expected;)
        {
            var n = (sbyte)input[i++];
            if (n >= 0)
            {
                var count = Math.Min(Math.Min(n + 1, input.Length - i), expected - used);
                input.Slice(i, count).CopyTo(output.AsSpan(used));
                used += count;
                i += n + 1;
            }
            else if (n != -128 && i < input.Length)
            {
                var count = Math.Min(1 - n, expected - used);
                output.AsSpan(used, count).Fill(input[i++]);
                used += count;
            }
        }
        return output;
    }

    /// <summary>
    /// TIFF's LZW: codes of 9 to 12 bits, most significant bit first, widened one code early; table entries are
    /// kept as spans of the output (an entry is the previous string and the first byte written after it).
    /// </summary>
    static byte[] Lzw(ReadOnlySpan<byte> input, int expected)
    {
        var output = new byte[Math.Max(expected, 256)];
        var used = 0;
        var starts = new int[4096];
        var lengths = new int[4096];
        var (next, width, previous, previousStart, previousLength) = (258, 9, -1, 0, 0);
        var bits = input.Length * 8L;
        long position = 0;
        void Ensure(int more)
        {
            if (used + more > output.Length)
                Array.Resize(ref output, Math.Max(output.Length * 2, used + more));
        }
        while (position + width <= bits && used < expected)
        {
            var at = (int)(position >> 3);
            var chunk = input[at] << 16 | (at + 1 < input.Length ? input[at + 1] << 8 : 0) | (at + 2 < input.Length ? input[at + 2] : 0);
            var code = chunk >> 24 - width - (int)(position & 7) & (1 << width) - 1;
            position += width;
            if (code == 257)
                break;
            if (code == 256)
            {
                (next, width, previous) = (258, 9, -1);
                continue;
            }
            if (code > next || code == next && previous < 0)
                break;
            var start = used;
            int length;
            if (code < 256)
            {
                Ensure(length = 1);
                output[used++] = (byte)code;
            }
            else if (code < next)
            {
                Ensure(length = lengths[code]);
                Array.Copy(output, starts[code], output, used, length);
                used += length;
            }
            else
            {
                // The code being defined: the previous string and its own first byte.
                Ensure(length = previousLength + 1);
                Array.Copy(output, previousStart, output, used, previousLength);
                output[used + previousLength] = output[previousStart];
                used += length;
            }
            if (previous >= 0 && next < 4096)
            {
                (starts[next], lengths[next]) = (previousStart, previousLength + 1);
                next++;
            }
            (previous, previousStart, previousLength) = (code, start, length);
            width = next >= 2047 ? 12 : next >= 1023 ? 11 : next >= 511 ? 10 : 9;
        }
        return output.AsSpan(0, used).ToArray();
    }

    /// <summary>Undoes horizontal differencing (Predictor 2) row by row.</summary>
    static void Unpredict(byte[] block, int stride, int samples, int bits, bool little)
    {
        int Get(int p) => little ? block[p] | block[p + 1] << 8 : block[p] << 8 | block[p + 1];
        for (var row = 0; row < block.Length; row += stride)
        {
            var end = Math.Min(row + stride, block.Length);
            if (bits == 8)
            {
                for (var i = row + samples; i < end; i++)
                    block[i] += block[i - samples];
            }
            else if (bits == 16)
            {
                for (var i = row + 2 * samples; i + 1 < end; i += 2)
                {
                    var sum = Get(i) + Get(i - 2 * samples);
                    (block[i], block[i + 1]) = little ? ((byte)sum, (byte)(sum >> 8)) : ((byte)(sum >> 8), (byte)sum);
                }
            }
        }
    }
}
