namespace FreeSlate.Html;

/// <summary>
/// A GIF decoder for the first image of a GIF87a or GIF89a file (catalog: document.background, VML pattern fills):
/// its color table, LZW data (variable-length codes, least significant bit first) and interlaced rows, as RGBA on
/// the logical screen.
/// </summary>
public static class Gif
{
    public static bool IsGif(ReadOnlySpan<byte> data) => data is [0x47, 0x49, 0x46, 0x38, 0x37 or 0x39, 0x61, ..];

    public static (int Width, int Height, byte[] Rgba)? Decode(byte[] data)
    {
        if (!IsGif(data) || data.Length < 13)
            return null;
        var (width, height) = (data[6] | data[7] << 8, data[8] | data[9] << 8);
        var position = 13;
        byte[]? global = null;
        if ((data[10] & 0x80) != 0)
        {
            var size = 3 << ((data[10] & 7) + 1);
            if (position + size > data.Length)
                return null;
            global = data[position..(position + size)];
            position += size;
        }
        while (position < data.Length)
        {
            var block = data[position++];
            if (block == 0x21)
            {
                // An extension: its label, then sub-blocks up to an empty one.
                position++;
                while (position < data.Length && data[position] != 0)
                    position += data[position] + 1;
                position++;
                continue;
            }
            if (block != 0x2C || position + 9 > data.Length)
                return null;
            var (left, top) = (data[position] | data[position + 1] << 8, data[position + 2] | data[position + 3] << 8);
            var (w, h) = (data[position + 4] | data[position + 5] << 8, data[position + 6] | data[position + 7] << 8);
            var packed = data[position + 8];
            position += 9;
            var table = global;
            if ((packed & 0x80) != 0)
            {
                var size = 3 << ((packed & 7) + 1);
                if (position + size > data.Length)
                    return null;
                table = data[position..(position + size)];
                position += size;
            }
            if (table == null || position >= data.Length)
                return null;
            var minimum = data[position++];
            var compressed = new List<byte>();
            while (position < data.Length && data[position] != 0)
            {
                var length = data[position];
                compressed.AddRange(data.AsSpan(position + 1, Math.Min(length, data.Length - position - 1)).ToArray());
                position += length + 1;
            }
            var indices = Lzw(compressed.ToArray(), minimum, w * h);
            if (width <= 0 || height <= 0)
                (width, height) = (w, h);
            var rgba = new byte[width * height * 4];
            var rows = (packed & 0x40) != 0 ? Interlaced(h) : Enumerable.Range(0, h).ToArray();
            for (var k = 0; k < Math.Min(indices.Length, w * h); k++)
            {
                var (x, y) = (left + k % w, top + rows[k / w]);
                if (x >= width || y >= height || 3 * indices[k] + 2 >= table.Length)
                    continue;
                var at = (y * width + x) * 4;
                (rgba[at], rgba[at + 1], rgba[at + 2], rgba[at + 3]) = (table[3 * indices[k]], table[3 * indices[k] + 1], table[3 * indices[k] + 2], 255);
            }
            return (width, height, rgba);
        }
        return null;
    }

    /// <summary>The row each stored row of an interlaced image goes to: every 8th from 0, from 4, every 4th from 2, every 2nd from 1.</summary>
    static int[] Interlaced(int height)
    {
        var rows = new List<int>();
        foreach (var (start, step) in new[] { (0, 8), (4, 8), (2, 4), (1, 2) })
            for (var y = start; y < height; y += step)
                rows.Add(y);
        return rows.ToArray();
    }

    /// <summary>GIF's LZW: codes from minimum + 1 bits wide up to 12, least significant bit first, a clear and an end code.</summary>
    static byte[] Lzw(byte[] input, int minimum, int expected)
    {
        var output = new List<byte>(expected);
        var (clear, end) = (1 << minimum, (1 << minimum) + 1);
        var prefix = new int[4096];
        var suffix = new byte[4096];
        var length = new int[4096];
        for (var c = 0; c < clear; c++)
            (prefix[c], suffix[c], length[c]) = (-1, (byte)c, 1);
        var (size, next, previous) = (minimum + 1, end + 1, -1);
        var (buffer, bits) = (0, 0);
        var stack = new byte[4096];
        foreach (var b in input)
        {
            buffer |= b << bits;
            bits += 8;
            while (bits >= size)
            {
                var code = buffer & ((1 << size) - 1);
                buffer >>= size;
                bits -= size;
                if (code == clear)
                {
                    (size, next, previous) = (minimum + 1, end + 1, -1);
                    continue;
                }
                if (code == end)
                    return output.ToArray();
                int first;
                if (code < next)
                {
                    // A known code: its string.
                    var n = 0;
                    for (var c = code; c >= 0; c = prefix[c])
                        stack[n++] = suffix[c];
                    first = stack[n - 1];
                    for (var i = n - 1; i >= 0; i--)
                        output.Add(stack[i]);
                }
                else if (code == next && previous >= 0)
                {
                    // The code being defined: the previous string and its first character.
                    var n = 0;
                    for (var c = previous; c >= 0; c = prefix[c])
                        stack[n++] = suffix[c];
                    first = stack[n - 1];
                    for (var i = n - 1; i >= 0; i--)
                        output.Add(stack[i]);
                    output.Add((byte)first);
                }
                else
                    return output.ToArray();
                if (previous >= 0 && next < 4096)
                {
                    (prefix[next], suffix[next], length[next]) = (previous, (byte)first, length[previous] + 1);
                    next++;
                    if (next == 1 << size && size < 12)
                        size++;
                }
                previous = code;
                if (output.Count >= expected)
                    return output.ToArray();
            }
        }
        return output.ToArray();
    }
}
