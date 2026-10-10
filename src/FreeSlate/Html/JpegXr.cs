using System.Buffers.Binary;
using static FreeSlate.Html.JxrFormat;

namespace FreeSlate.Html;

/// <summary>
/// A JPEG XR (HD Photo, Windows Media Photo) decoder after ITU-T T.832 (catalog: picture.formats): the tag-based
/// file format of annex A, spatial and frequency ordered codestreams, tiles, every overlap mode, the internal colour
/// formats with their output conversions, interleaved and separate alpha planes. PowerPoint keeps these pictures as
/// image/vnd.ms-photo, which browsers do not show, so they are converted to PNG.
/// </summary>
public static class JpegXr
{
    public static bool IsJpegXr(ReadOnlySpan<byte> data) =>
        data.Length >= 8 && data[0] == 'I' && data[1] == 'I' && data[2] == 0xBC && data[3] <= 1;

    /// <summary>The pixel size and resolution (dots per inch, 96 when unknown) of the image, or zeros.</summary>
    public static (int Width, int Height, double DpiX, double DpiY) Info(byte[] data)
    {
        try
        {
            if (Directory(data) is not { } tags)
                return default;
            var (w, h) = ((int)Number(data, tags, 0xBC80), (int)Number(data, tags, 0xBC81));
            if (Spatial(data, tags) >= 4)
                (w, h) = (h, w);
            var (dpiX, dpiY) = (Float(data, tags, 0xBC82), Float(data, tags, 0xBC83));
            return (w, h, dpiX > 0 ? dpiX : 96, dpiY > 0 ? dpiY : 96);
        }
        catch (Exception e) when (e is IndexOutOfRangeException or ArgumentException)
        {
            return default;
        }
    }

    /// <summary>The image as top-down RGBA rows, or null when the file is not a JPEG XR file this decoder reads.</summary>
    public static (int Width, int Height, byte[] Rgba)? Decode(byte[] data)
    {
        try
        {
            return DecodeOrThrow(data);
        }
        catch (Exception e) when (e is IndexOutOfRangeException or ArgumentException or InvalidDataException or OverflowException)
        {
            return null;
        }
    }

    /// <summary>The image as top-down RGBA rows; throws <see cref="InvalidDataException"/> and others for broken files.</summary>
    public static (int Width, int Height, byte[] Rgba) DecodeOrThrow(byte[] data)
    {
        {
            if (Directory(data) is not { } tags)
                throw new InvalidDataException("No JPEG XR file");
            var offset = (int)Number(data, tags, 0xBCC0);
            var length = (int)Math.Min(Number(data, tags, 0xBCC1), data.Length - (long)offset);
            var image = JxrCodestream.Decode(data, offset, offset + length);
            JxrCodestream? alpha = null;
            if (image.Alpha == null && tags.ContainsKey(0xBCC2))
            {
                var alphaOffset = (int)Number(data, tags, 0xBCC2);
                var alphaLength = (int)Math.Min(Number(data, tags, 0xBCC3), data.Length - (long)alphaOffset);
                alpha = JxrCodestream.Decode(data, alphaOffset, alphaOffset + alphaLength);
                if (alpha.Width != image.Width || alpha.Height != image.Height)
                    alpha = null;
            }
            var premultiplied = image.Premultiplied || PixelFormat(data, tags) is 0x10 or 0x17 or 0x1A;
            var (w, h, rgba) = JxrOutput.Rgba(image, alpha, premultiplied);
            return Orient(w, h, rgba, Spatial(data, tags));
        }
    }

    /// <summary>The image as a PNG file, or null.</summary>
    public static byte[]? ToPng(byte[] data) =>
        Decode(data) is var (w, h, rgba) ? Metafiles.Png.Encode(w, h, rgba) : null;

    /// <summary>The entries of the first image file directory (A.6): tag to element type, count and value field.</summary>
    static Dictionary<int, (int Type, long Count, int Field)>? Directory(byte[] data)
    {
        if (!IsJpegXr(data))
            return null;
        var at = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4));
        if (at + 2 > data.Length)
            return null;
        var count = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan((int)at));
        var tags = new Dictionary<int, (int, long, int)>();
        for (var n = 0; n < count; n++)
        {
            var entry = data.AsSpan((int)at + 2 + n * 12, 12);
            tags[BinaryPrimitives.ReadUInt16LittleEndian(entry)] = (BinaryPrimitives.ReadUInt16LittleEndian(entry[2..]),
                BinaryPrimitives.ReadUInt32LittleEndian(entry[4..]), (int)at + 2 + n * 12 + 8);
        }
        return tags;
    }

    static int Size(int type) => type switch { 3 or 8 => 2, 4 or 9 or 11 => 4, 5 or 10 or 12 => 8, _ => 1 };

    /// <summary>The bytes of an entry's values: in the value field when they fit, at its offset otherwise.</summary>
    static ReadOnlySpan<byte> Values(byte[] data, (int Type, long Count, int Field) entry)
    {
        var length = entry.Count * Size(entry.Type);
        if (length <= 4)
            return data.AsSpan(entry.Field, (int)length);
        var offset = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(entry.Field));
        return data.AsSpan((int)offset, (int)length);
    }

    static long Number(byte[] data, Dictionary<int, (int Type, long Count, int Field)> tags, int tag)
    {
        if (!tags.TryGetValue(tag, out var entry))
            throw new InvalidDataException($"JPEG XR file without tag {tag:X4}");
        var values = Values(data, entry);
        return entry.Type switch
        {
            1 => values[0],
            3 => BinaryPrimitives.ReadUInt16LittleEndian(values),
            4 => BinaryPrimitives.ReadUInt32LittleEndian(values),
            _ => throw new InvalidDataException("Unexpected JPEG XR tag type"),
        };
    }

    static double Float(byte[] data, Dictionary<int, (int Type, long Count, int Field)> tags, int tag) =>
        tags.TryGetValue(tag, out var entry) && entry.Type == 11
            ? BinaryPrimitives.ReadSingleLittleEndian(Values(data, entry))
            : 0;

    /// <summary>
    /// The preferred spatial transformation: the file's (A.7.19), or else the codestream's SPATIAL_XFRM_SUBORDINATE
    /// (bits 3 to 5 of the header's tenth byte).
    /// </summary>
    static int Spatial(byte[] data, Dictionary<int, (int Type, long Count, int Field)> tags) =>
        tags.ContainsKey(0xBC02) ? (int)Number(data, tags, 0xBC02) & 7 : data[(int)Number(data, tags, 0xBCC0) + 9] >> 3 & 7;

    /// <summary>The last byte of the pixel format GUID (A.7.18), which tells the formats apart; -1 without one.</summary>
    static int PixelFormat(byte[] data, Dictionary<int, (int Type, long Count, int Field)> tags) =>
        tags.TryGetValue(0xBC01, out var entry) && entry.Count == 16 ? Values(data, entry)[15] : -1;

    /// <summary>
    /// Applies the preferred spatial transformation (8.3.8): the place of the first row and column as in the
    /// "Fill" column of table 21.
    /// </summary>
    static (int, int, byte[]) Orient(int w, int h, byte[] rgba, int transform)
    {
        if (transform == 0)
            return (w, h, rgba);
        var turned = transform >= 4;
        var (ow, oh) = turned ? (h, w) : (w, h);
        var result = new byte[rgba.Length];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var (u, v) = transform switch
                {
                    1 => (x, h - 1 - y),
                    2 => (w - 1 - x, y),
                    3 => (w - 1 - x, h - 1 - y),
                    4 => (h - 1 - y, x),
                    5 => (h - 1 - y, w - 1 - x),
                    6 => (y, x),
                    _ => (y, w - 1 - x),
                };
                rgba.AsSpan((y * w + x) * 4, 4).CopyTo(result.AsSpan((v * ow + u) * 4));
            }
        return (ow, oh, result);
    }
}

/// <summary>
/// The output formatting of 9.10 for display: chroma upsampling, the inverse colour transforms, bias, scaling and
/// clipping into the output bit depth, then 8-bit RGBA (high dynamic range formats from linear light to sRGB).
/// </summary>
static class JxrOutput
{
    public static (int Width, int Height, byte[] Rgba) Rgba(JxrCodestream image, JxrCodestream? separateAlpha, bool premultiplied)
    {
        var (w, h) = (image.Width, image.Height);
        var color = Channels(image, image.Primary, false);
        var alpha = image.Alpha != null ? Channels(image, image.Alpha, true)[0]
            : separateAlpha != null ? Channels(separateAlpha, separateAlpha.Primary, true)[0] : null;
        var alphaDepth = separateAlpha?.BitDepth ?? image.BitDepth;
        var rgba = new byte[w * h * 4];
        var depth = image.BitDepth;
        var format = image.OutputFormat;
        Span<double> linear = stackalloc double[4];
        for (var i = 0; i < w * h; i++)
        {
            byte r, g, b;
            if (format == OutRgbe)
            {
                var exponent = color[3][i];
                for (var k = 0; k < 3; k++)
                    linear[k] = exponent == 0 ? 0 : Math.ScaleB(color[k][i], exponent - 136);
                (r, g, b) = (Srgb(linear[0]), Srgb(linear[1]), Srgb(linear[2]));
            }
            else if (format is OutCmyk or OutCmykDirect)
            {
                // As PowerPoint shows CMYK pictures without a profile of their own.
                var (cr, cg, cb) = Cmyk.ToRgb(Byte(Unit(color[0][i], depth, 0)), Byte(Unit(color[1][i], depth, 1)),
                    Byte(Unit(color[2][i], depth, 2)), Byte(Unit(color[3][i], depth, 3)));
                (r, g, b) = ((byte)cr, (byte)cg, (byte)cb);
            }
            else if (color.Length >= 3)
            {
                (r, g, b) = (Sample(color[0][i], depth, 0), Sample(color[1][i], depth, 1), Sample(color[2][i], depth, 2));
                // Packed formats keep red and blue in the order the codestream says (InvColorFmtConvert2( )).
                if (depth is Bd5 or Bd565 or Bd10 && !image.RedBlueNotSwapped)
                    (r, b) = (b, r);
            }
            else
                r = g = b = Sample(color[0][i], depth, 0);
            var a = alpha != null ? Byte(Unit(alpha[i], alphaDepth, 0)) : (byte)255;
            if (premultiplied && a is > 0 and < 255)
                (r, g, b) = ((byte)Math.Min(255, r * 255 / a), (byte)Math.Min(255, g * 255 / a), (byte)Math.Min(255, b * 255 / a));
            rgba[i * 4] = r;
            rgba[i * 4 + 1] = g;
            rgba[i * 4 + 2] = b;
            rgba[i * 4 + 3] = a;
        }
        return (w, h, rgba);
    }

    /// <summary>
    /// The output samples of an image plane inside the window, per output component, in the representation of the
    /// output bit depth (the bit pattern for floating point).
    /// </summary>
    static int[][] Channels(JxrCodestream image, JxrPlane plane, bool alpha)
    {
        var (w, h) = (image.Width, image.Height);
        var stride = image.ExtendedWidth;
        var full = new int[plane.Components][];
        for (var c = 0; c < plane.Components; c++)
            full[c] = c > 0 && plane.Format is Yuv420 or Yuv422 ? Upsample(plane, c, stride, image.ExtendedHeight) : plane.Samples[c];
        var target = alpha ? OutYOnly : image.OutputFormat;
        var outputs = target switch
        {
            OutRgb or OutYuv444 or OutYuv422 or OutYuv420 => 3,
            OutRgbe or OutCmyk or OutCmykDirect => 4,
            _ => plane.Components,
        };
        var depth = image.BitDepth;
        var bias = depth switch { Bd5 => 16, Bd565 => 32, Bd8 => 128, Bd10 => 512, Bd16 => 32768, _ => 0 };
        if (depth is Bd16 or Bd16S or Bd32S)
            bias >>= plane.ShiftBits;
        var scale = plane.Scaled ? 3 : 0;
        var rounding = !plane.Scaled ? 0 : depth is Bd1White or Bd1Black or Bd16 ? 4 : 3;
        var result = new int[outputs][];
        for (var k = 0; k < outputs; k++)
            result[k] = new int[w * h];
        Span<int> v = stackalloc int[Math.Max(outputs, plane.Components)];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var at = (y + image.TopMargin) * stride + x + image.LeftMargin;
                for (var c = 0; c < plane.Components; c++)
                    v[c] = full[c][at];
                if (plane.Format == YOnly && outputs >= 3)
                    v[1] = v[2] = v[0];
                else if (plane.Format is Yuv420 or Yuv422 or Yuv444 && target is not (OutCmyk or OutCmykDirect))
                {
                    // InvColorFmtConvert2( ): G = Y - floor(-U / 2), R = -U + G - ceil(V / 2), B = V + R.
                    var (luma, u, vv) = (v[0], v[1], v[2]);
                    var green = luma - (-u >> 1);
                    var red = -u + green - ((vv + 1) >> 1);
                    (v[0], v[1], v[2]) = (red, green, vv + red);
                }
                else if (plane.Format == Yuvk && target == OutCmyk)
                {
                    var (luma, u, vv, kk) = (v[0], v[1], v[2], v[3]);
                    var black = kk + (luma >> 1);
                    var magenta = black - luma - (u >> 1);
                    var cyan = u + magenta + (vv >> 1);
                    (v[0], v[1], v[2], v[3]) = (cyan, magenta, cyan - vv, black);
                }
                else if (plane.Format == Yuvk && target == OutCmykDirect)
                    (v[0], v[1], v[2], v[3]) = (v[1], v[2], v[3], v[0]);
                for (var k = 0; k < outputs; k++)
                {
                    var sample = v[k];
                    if (target == OutCmyk)
                        sample += k < 3 ? (bias >> 1) << scale : -((bias >> 1) << scale);
                    else if (target != OutRgbe)
                        sample += bias << scale;
                    sample = (sample + rounding) >> (scale + (depth == Bd565 && k != 1 ? 1 : 0));
                    result[k][y * w + x] = depth switch
                    {
                        Bd8 => Math.Clamp(sample, 0, 255),
                        Bd16 => Math.Clamp(sample << plane.ShiftBits, 0, 65535),
                        Bd16S => Math.Clamp(sample << plane.ShiftBits, -32768, 32767),
                        Bd32S => sample << plane.ShiftBits,
                        Bd16F => (sample < 0 ? 0x8000 : 0) | Math.Min(Math.Abs(sample), 32767),
                        Bd32F => Float(sample, plane),
                        Bd5 => Math.Clamp(sample, 0, 31),
                        Bd565 => Math.Clamp(sample, 0, k == 1 ? 63 : 31),
                        Bd10 => Math.Clamp(sample, 0, 1023),
                        _ => Math.Clamp(sample, 0, 1),
                    };
                }
                if (target == OutRgbe)
                    Rgbe(result, y * w + x);
            }
        return result;
    }

    /// <summary>
    /// Upsample( ) (9.10.3.2) of a chroma component to the luma resolution. Centred chroma, which Windows' encoder
    /// writes, is interpolated as Windows' decoder does it, with the four taps (-2, 9, 28, -3) / 32 and for 4:2:0 both
    /// directions before a single rounding; other positions with the two-tap filters of the specification's example.
    /// </summary>
    static int[] Upsample(JxrPlane plane, int c, int width, int height)
    {
        var (cw, ch) = (plane.SampleWidth[c], plane.SampleHeight[c]);
        var source = plane.Samples[c];
        var vertical = ch < height;
        var result = new int[width * height];
        if (plane.CenterX == 2 && (!vertical || plane.CenterY == 2))
        {
            var wide = new int[width * ch];
            for (var y = 0; y < ch; y++)
                for (var k = 0; k < cw; k++)
                {
                    var row = y * cw;
                    wide[y * width + 2 * k] = Centred(i => source[row + i], k, cw, false);
                    wide[y * width + 2 * k + 1] = Centred(i => source[row + i], k, cw, true);
                }
            if (!vertical)
            {
                for (var i = 0; i < wide.Length; i++)
                    result[i] = (wide[i] + 16) >> 5;
                return result;
            }
            for (var x = 0; x < width; x++)
                for (var k = 0; k < ch; k++)
                {
                    result[2 * k * width + x] = (Centred(i => wide[i * width + x], k, ch, false) + 512) >> 10;
                    result[(2 * k + 1) * width + x] = (Centred(i => wide[i * width + x], k, ch, true) + 512) >> 10;
                }
            return result;
        }
        if (vertical)
        {
            var taps = Taps(plane.CenterY);
            var tall = new int[cw * height];
            for (var x = 0; x < cw; x++)
                for (var k = 0; k < ch; k++)
                {
                    var (above, here, below) = (source[Math.Max(0, k - 1) * cw + x], source[k * cw + x], source[Math.Min(ch - 1, k + 1) * cw + x]);
                    tall[2 * k * cw + x] = (taps[2] * above + taps[3] * here + 4) >> 3;
                    tall[(2 * k + 1) * cw + x] = (taps[0] * here + taps[1] * below + 4) >> 3;
                }
            source = tall;
        }
        var tapsX = Taps(plane.CenterX);
        for (var y = 0; y < height; y++)
            for (var k = 0; k < cw; k++)
            {
                var row = y * cw;
                var (before, here, after) = (source[row + Math.Max(0, k - 1)], source[row + k], source[row + Math.Min(cw - 1, k + 1)]);
                result[y * width + 2 * k] = (tapsX[2] * before + tapsX[3] * here + 4) >> 3;
                result[y * width + 2 * k + 1] = (tapsX[0] * here + tapsX[1] * after + 4) >> 3;
            }
        return result;
    }

    /// <summary>A centred chroma sample interpolated before (or after) source sample k, times 32.</summary>
    static int Centred(Func<int, int> at, int k, int length, bool after)
    {
        int Get(int i) => at(Math.Clamp(i, 0, length - 1));
        return after
            ? -3 * Get(k - 1) + 28 * Get(k) + 9 * Get(k + 1) - 2 * Get(k + 2)
            : -2 * Get(k - 2) + 9 * Get(k - 1) + 28 * Get(k) - 3 * Get(k + 1);
    }

    /// <summary>The filter coefficients for a chroma position (table 181).</summary>
    static int[] Taps(int centering) => centering switch
    {
        1 => [5, 3, 1, 7],
        2 => [6, 2, 2, 6],
        3 => [7, 1, 3, 5],
        4 => [8, 0, 4, 4],
        _ => [4, 4, 0, 8],
    };

    /// <summary>PostScalingFl( ) for BD32F: the bit pattern of a single-precision number.</summary>
    static int Float(int sample, JxrPlane plane)
    {
        var sign = sample < 0 ? 1 : 0;
        var magnitude = Math.Abs(sample);
        var bits = plane.LenMantissa;
        var exponent = magnitude >> bits;
        var mantissa = (magnitude & ((1 << bits) - 1)) | 1 << bits;
        if (exponent == 0)
        {
            mantissa ^= 1 << bits;
            exponent = 1;
        }
        exponent += 127 - plane.ExpBias;
        while (mantissa < 1 << bits && exponent > 1 && mantissa > 0)
        {
            exponent--;
            mantissa <<= 1;
        }
        if (mantissa < 1 << bits)
            exponent = 0;
        else
            mantissa ^= 1 << bits;
        mantissa = bits <= 23 ? mantissa << (23 - bits) : mantissa >> (bits - 23);
        return sign << 31 | Math.Clamp(exponent, 0, 255) << 23 | mantissa;
    }

    /// <summary>PostScalingF2( ): mantissas and a shared exponent from R, G and B.</summary>
    static void Rgbe(int[][] result, int i)
    {
        Span<int> exponents = stackalloc int[3];
        for (var k = 0; k < 3; k++)
        {
            var value = result[k][i];
            (result[k][i], exponents[k]) = value <= 0 ? (0, 0) : (value >> 7) > 1 ? ((value & 0x7F) + 128, value >> 7) : (value, 1);
        }
        var shared = Math.Max(exponents[0], Math.Max(exponents[1], exponents[2]));
        for (var k = 0; k < 3; k++)
            if (shared > exponents[k])
                result[k][i] = (2 * result[k][i] + 1) >> (shared - exponents[k] + 1);
        result[3][i] = shared;
    }

    /// <summary>
    /// A sample of the output bit depth as an 8-bit sRGB value; short samples widened by repeating their bits, as
    /// Windows does.
    /// </summary>
    static byte Sample(int sample, int depth, int k) => depth switch
    {
        Bd8 => (byte)sample,
        Bd5 => (byte)(sample << 3 | sample >> 2),
        Bd565 when k == 1 => (byte)(sample << 2 | sample >> 4),
        Bd565 => (byte)(sample << 3 | sample >> 2),
        Bd16S or Bd32S or Bd16F or Bd32F => Srgb(Linear(sample, depth)),
        _ => Byte(Unit(sample, depth, k)),
    };

    /// <summary>A sample as a fraction of full intensity, linear light for the high dynamic range formats.</summary>
    static double Unit(int sample, int depth, int k) => depth switch
    {
        Bd8 => sample / 255.0,
        Bd16 => sample / 65535.0,
        Bd5 => sample / 31.0,
        Bd565 => sample / (k == 1 ? 63.0 : 31.0),
        Bd10 => sample / 1023.0,
        Bd1White => sample,
        Bd1Black => 1 - sample,
        _ => Linear(sample, depth),
    };

    static double Linear(int sample, int depth) => depth switch
    {
        Bd16S => sample / 8192.0,
        Bd32S => sample / 16777216.0,
        Bd16F => (double)BitConverter.UInt16BitsToHalf((ushort)sample),
        Bd32F => BitConverter.Int32BitsToSingle(sample),
        _ => 0,
    };

    static byte Byte(double unit) => (byte)Math.Round(Math.Clamp(unit, 0, 1) * 255);

    static byte Srgb(double linear) =>
        Byte(linear <= 0.0031308 ? linear * 12.92 : 1.055 * Math.Pow(Math.Min(linear, 1), 1 / 2.4) - 0.055);
}
