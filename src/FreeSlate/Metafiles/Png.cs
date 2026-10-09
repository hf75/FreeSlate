using System.Buffers.Binary;
using System.IO.Compression;

namespace FreeSlate.Metafiles;

/// <summary>A minimal PNG encoder for 32-bit RGBA pixels (bitmaps inside metafiles and converted pictures become data URIs).</summary>
public static class Png
{
    static readonly uint[] CrcTable = BuildCrcTable();

    /// <summary>
    /// Encodes top-down RGBA rows (4 bytes per pixel) as a PNG file: RGB when every pixel is opaque, rows filtered
    /// adaptively; <paramref name="iccProfile"/> (an RGB profile) goes into an iCCP chunk.
    /// </summary>
    public static byte[] Encode(int width, int height, byte[] rgba, byte[]? iccProfile = null)
    {
        using var output = new MemoryStream();
        output.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var opaque = true;
        for (var i = 3; i < rgba.Length && opaque; i += 4)
            opaque = rgba[i] == 255;
        var channels = opaque ? 3 : 4;
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;   // bit depth
        header[9] = (byte)(opaque ? 2 : 6);   // RGB or RGBA
        Chunk(output, "IHDR", header);
        if (iccProfile is { Length: > 0 })
        {
            using var profile = new MemoryStream();
            profile.Write("ICC Profile\0\0"u8);   // name, compression method 0 (zlib)
            using (var zlib = new ZLibStream(profile, CompressionLevel.Optimal, leaveOpen: true))
                zlib.Write(iccProfile);
            Chunk(output, "iCCP", profile.ToArray());
        }

        using (var raw = new MemoryStream())
        {
            using (var zlib = new ZLibStream(raw, CompressionLevel.Optimal, leaveOpen: true))
            {
                var stride = width * channels;
                var (previous, row) = (new byte[stride], new byte[stride]);
                var filtered = new byte[5][];
                for (var f = 0; f < 5; f++)
                    filtered[f] = new byte[stride];
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                        rgba.AsSpan((y * width + x) * 4, channels).CopyTo(row.AsSpan(x * channels));
                    // The filter whose output has the smallest sum of magnitudes (as signed bytes) compresses best.
                    var (best, bestSum) = (0, long.MaxValue);
                    for (var f = 0; f < 5; f++)
                    {
                        long sum = 0;
                        var target = filtered[f];
                        for (var i = 0; i < stride; i++)
                        {
                            var (a, b) = (i >= channels ? row[i - channels] : 0, previous[i]);
                            var c = i >= channels ? previous[i - channels] : 0;
                            var value = (byte)(row[i] - f switch
                            {
                                1 => a,
                                2 => b,
                                3 => (a + b) >> 1,
                                4 => Paeth(a, b, c),
                                _ => 0,
                            });
                            target[i] = value;
                            sum += Math.Abs((int)(sbyte)value);
                        }
                        if (sum < bestSum)
                            (best, bestSum) = (f, sum);
                    }
                    zlib.WriteByte((byte)best);
                    zlib.Write(filtered[best]);
                    (previous, row) = (row, previous);
                }
            }
            Chunk(output, "IDAT", raw.ToArray());
        }
        Chunk(output, "IEND", []);
        return output.ToArray();
    }

    static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var (pa, pb, pc) = (Math.Abs(p - a), Math.Abs(p - b), Math.Abs(p - c));
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    public static string DataUri(int width, int height, byte[] rgba) =>
        "data:image/png;base64," + Convert.ToBase64String(Encode(width, height, rgba));

    static void Chunk(Stream output, string type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);
        var crc = 0xFFFFFFFFu;
        foreach (var b in typeBytes) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        foreach (var b in data) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc ^ 0xFFFFFFFF);
        output.Write(crcBytes);
    }

    static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
