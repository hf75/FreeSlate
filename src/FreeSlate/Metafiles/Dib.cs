using System.Buffers.Binary;

namespace FreeSlate.Metafiles;

/// <summary>A decoded bitmap: top-down RGBA pixels, or an embedded JPEG/PNG passed through as a data URI.</summary>
public sealed class Bitmap
{
    public int Width { get; init; }
    public int Height { get; init; }
    public byte[]? Rgba { get; init; }
    public string? EmbeddedUri { get; init; }

    public string DataUri() => EmbeddedUri ?? Png.DataUri(Width, Height, Rgba!);

    /// <summary>A copy where every pixel of <paramref name="color"/> is transparent (TransparentBlt).</summary>
    public Bitmap WithTransparentColor(uint rgb)
    {
        if (Rgba == null)
            return this;
        var copy = (byte[])Rgba.Clone();
        var (r, g, b) = ((byte)(rgb & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)((rgb >> 16) & 0xFF));
        for (var i = 0; i < copy.Length; i += 4)
        {
            if (copy[i] == r && copy[i + 1] == g && copy[i + 2] == b)
                copy[i + 3] = 0;
        }
        return new Bitmap { Width = Width, Height = Height, Rgba = copy };
    }
}

/// <summary>Decodes device-independent bitmaps (BITMAPCOREHEADER, BITMAPINFOHEADER, V4, V5) as stored in metafiles.</summary>
public static class Dib
{
    /// <summary>
    /// Decodes a DIB from its header bytes and pixel bytes. <paramref name="useAlpha"/> keeps the alpha channel of
    /// 32-bit pixels (AlphaBlend with per-pixel alpha); otherwise they are opaque. For monochrome bitmaps
    /// <paramref name="monochrome"/> replaces the two palette colors (pattern brushes use text and background color).
    /// </summary>
    public static Bitmap? Decode(ReadOnlySpan<byte> header, ReadOnlySpan<byte> bits, bool useAlpha = false, (uint Zero, uint One)? monochrome = null)
    {
        if (header.Length < 12)
            return null;
        var headerSize = BinaryPrimitives.ReadInt32LittleEndian(header);
        int width, height, bpp, compression, colorsUsed;
        int paletteEntrySize;
        if (headerSize == 12)
        {
            width = BinaryPrimitives.ReadUInt16LittleEndian(header[4..]);
            height = BinaryPrimitives.ReadInt16LittleEndian(header[6..]);
            bpp = BinaryPrimitives.ReadUInt16LittleEndian(header[10..]);
            compression = 0;
            colorsUsed = 0;
            paletteEntrySize = 3;
        }
        else
        {
            if (header.Length < 40)
                return null;
            width = BinaryPrimitives.ReadInt32LittleEndian(header[4..]);
            height = BinaryPrimitives.ReadInt32LittleEndian(header[8..]);
            bpp = BinaryPrimitives.ReadUInt16LittleEndian(header[14..]);
            compression = BinaryPrimitives.ReadInt32LittleEndian(header[16..]);
            colorsUsed = BinaryPrimitives.ReadInt32LittleEndian(header[32..]);
            paletteEntrySize = 4;
        }
        if (compression is 4 or 5)
        {
            var mime = compression == 4 ? "image/jpeg" : "image/png";
            return new Bitmap { Width = width, Height = Math.Abs(height), EmbeddedUri = $"data:{mime};base64,{Convert.ToBase64String(bits)}" };
        }
        if (width <= 0 || height == 0 || width > 32768 || Math.Abs(height) > 32768)
            return null;

        var topDown = height < 0;
        height = Math.Abs(height);

        // Color masks for 16/32-bit bitfields (after a 40-byte header, or inside V4/V5 headers).
        uint redMask = 0, greenMask = 0, blueMask = 0, alphaMask = 0;
        var paletteOffset = headerSize;
        if (compression is 3 or 6)
        {
            var maskOffset = headerSize >= 52 ? 40 : headerSize;
            if (header.Length >= maskOffset + 12)
            {
                redMask = BinaryPrimitives.ReadUInt32LittleEndian(header[maskOffset..]);
                greenMask = BinaryPrimitives.ReadUInt32LittleEndian(header[(maskOffset + 4)..]);
                blueMask = BinaryPrimitives.ReadUInt32LittleEndian(header[(maskOffset + 8)..]);
                if (compression == 6 || headerSize >= 56)
                    alphaMask = header.Length >= maskOffset + 16 ? BinaryPrimitives.ReadUInt32LittleEndian(header[(maskOffset + 12)..]) : 0;
            }
            if (headerSize == 40)
                paletteOffset += compression == 6 ? 16 : 12;
        }
        else if (bpp == 16)
        {
            (redMask, greenMask, blueMask) = (0x7C00, 0x03E0, 0x001F);
        }

        // Palette for indexed bitmaps.
        var paletteCount = bpp <= 8 ? (colorsUsed > 0 ? colorsUsed : 1 << bpp) : 0;
        var palette = new uint[Math.Max(paletteCount, 2)];
        for (var i = 0; i < paletteCount && paletteOffset + i * paletteEntrySize + 3 <= header.Length; i++)
        {
            var p = header[(paletteOffset + i * paletteEntrySize)..];
            palette[i] = (uint)(p[2] | (p[1] << 8) | (p[0] << 16));   // as COLORREF (R in low byte)
        }
        if (bpp == 1 && monochrome is { } mono)
        {
            palette[0] = mono.Zero;
            palette[1] = mono.One;
        }

        var rgba = new byte[width * height * 4];
        if (compression is 1 or 2)
        {
            DecodeRle(bits, width, height, compression == 2, palette, rgba, topDown);
            return new Bitmap { Width = width, Height = height, Rgba = rgba };
        }

        var stride = (width * bpp + 31) / 32 * 4;
        for (var row = 0; row < height; row++)
        {
            var source = topDown ? row : height - 1 - row;
            var offset = source * stride;
            if (offset + stride > bits.Length)
                break;
            var line = bits.Slice(offset, stride);
            for (var x = 0; x < width; x++)
            {
                byte r, g, b, a = 255;
                switch (bpp)
                {
                    case 1:
                    case 4:
                    case 8:
                    {
                        var bit = x * bpp;
                        var index = (line[bit / 8] >> (8 - bpp - bit % 8)) & ((1 << bpp) - 1);
                        var c = palette[Math.Min(index, palette.Length - 1)];
                        (r, g, b) = ((byte)c, (byte)(c >> 8), (byte)(c >> 16));
                        break;
                    }
                    case 16:
                    {
                        var v = BinaryPrimitives.ReadUInt16LittleEndian(line[(x * 2)..]);
                        (r, g, b) = (Channel(v, redMask), Channel(v, greenMask), Channel(v, blueMask));
                        break;
                    }
                    case 24:
                        (b, g, r) = (line[x * 3], line[x * 3 + 1], line[x * 3 + 2]);
                        break;
                    case 32:
                    {
                        var v = BinaryPrimitives.ReadUInt32LittleEndian(line[(x * 4)..]);
                        if (redMask != 0)
                        {
                            (r, g, b) = (Channel(v, redMask), Channel(v, greenMask), Channel(v, blueMask));
                            if (alphaMask != 0) a = Channel(v, alphaMask);
                        }
                        else
                        {
                            (b, g, r) = ((byte)v, (byte)(v >> 8), (byte)(v >> 16));
                            if (useAlpha) a = (byte)(v >> 24);
                        }
                        break;
                    }
                    default:
                        return null;
                }
                var target = (row * width + x) * 4;
                rgba[target] = r;
                rgba[target + 1] = g;
                rgba[target + 2] = b;
                rgba[target + 3] = a;
            }
        }

        // Premultiplied alpha (AlphaBlend sources) back to straight alpha.
        if (useAlpha && bpp == 32)
        {
            for (var i = 0; i < rgba.Length; i += 4)
            {
                var a = rgba[i + 3];
                if (a is 0 or 255) continue;
                for (var c = 0; c < 3; c++)
                    rgba[i + c] = (byte)Math.Min(255, rgba[i + c] * 255 / a);
            }
        }
        return new Bitmap { Width = width, Height = height, Rgba = rgba };
    }

    static byte Channel(uint value, uint mask)
    {
        if (mask == 0)
            return 0;
        var shift = System.Numerics.BitOperations.TrailingZeroCount(mask);
        var max = mask >> shift;
        return (byte)(((value & mask) >> shift) * 255 / max);
    }

    static void DecodeRle(ReadOnlySpan<byte> bits, int width, int height, bool four, uint[] palette, byte[] rgba, bool topDown)
    {
        int x = 0, y = 0, i = 0;
        void Put(int index)
        {
            if (x >= width || y >= height) { x++; return; }
            var row = topDown ? y : height - 1 - y;
            var c = palette[Math.Min(index, palette.Length - 1)];
            var t = (row * width + x) * 4;
            rgba[t] = (byte)c;
            rgba[t + 1] = (byte)(c >> 8);
            rgba[t + 2] = (byte)(c >> 16);
            rgba[t + 3] = 255;
            x++;
        }
        while (i + 1 < bits.Length)
        {
            int count = bits[i], value = bits[i + 1];
            i += 2;
            if (count > 0)
            {
                for (var k = 0; k < count; k++)
                    Put(four ? (k % 2 == 0 ? value >> 4 : value & 0x0F) : value);
                continue;
            }
            switch (value)
            {
                case 0: x = 0; y++; break;
                case 1: return;
                case 2:
                    if (i + 1 >= bits.Length) return;
                    x += bits[i]; y += bits[i + 1]; i += 2;
                    break;
                default:
                {
                    var bytes = four ? (value + 1) / 2 : value;
                    for (var k = 0; k < value && i + (four ? k / 2 : k) < bits.Length; k++)
                    {
                        var b = bits[i + (four ? k / 2 : k)];
                        Put(four ? (k % 2 == 0 ? b >> 4 : b & 0x0F) : b);
                    }
                    i += bytes + (bytes % 2);   // runs are word aligned
                    break;
                }
            }
        }
    }
}
