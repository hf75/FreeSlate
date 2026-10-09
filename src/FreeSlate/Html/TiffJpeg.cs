namespace FreeSlate.Html;

/// <summary>
/// JPEG-compressed TIFF images (compression 7, catalog: picture.formats) as one JPEG file browsers show. Every strip
/// is a JPEG stream of its own (tables shared through JPEGTables or repeated per strip); strips restart their DC
/// predictions as restart intervals do, so the merged file is the first strip's frame at the full height and the
/// strips' scans one after another, separated by restart markers. Strips coded with different Huffman tables are
/// re-coded with the standard tables of ITU T.81 Annex K (lossless: the quantized coefficients stay as they are).
/// </summary>
static class TiffJpeg
{
    /// <summary>The merged JPEG file, or null for tiles, progressive or arithmetic coding and differing quantization.</summary>
    public static byte[]? Merge(IReadOnlyList<byte[]> strips, byte[]? tables, int width, int height, int rowsPerStrip, bool rgb, byte[]? profile = null)
    {
        var parsed = new List<Stream>();
        foreach (var strip in strips)
        {
            var stream = new Stream(strip);
            if (tables != null && !stream.Read(tables, entropy: false) || !stream.Read(strip, entropy: true) || stream.Frame == null || stream.Scan == null)
                return null;
            parsed.Add(stream);
        }
        if (parsed.Count == 0)
            return null;
        var first = parsed[0];
        var output = new List<byte>(strips.Sum(s => s.Length) + 1024) { 0xFF, 0xD8 };
        if (rgb)
            output.AddRange([0xFF, 0xEE, 0, 14, (byte)'A', (byte)'d', (byte)'o', (byte)'b', (byte)'e', 0, 100, 0, 0, 0, 0, 0]);   // RGB, not YCbCr
        foreach (var segment in first.Other)
            output.AddRange(segment);
        if (profile != null && !first.Other.Any(o => o[1] == 0xE2))
        {
            // The color profile in APP2 segments of at most 65519 bytes, numbered from 1.
            const int Chunk = 65519;
            var chunks = (profile.Length + Chunk - 1) / Chunk;
            for (var i = 0; i < chunks && chunks < 256; i++)
            {
                var part = profile.AsSpan(i * Chunk, Math.Min(Chunk, profile.Length - i * Chunk));
                var length = 2 + 12 + 2 + part.Length;
                output.AddRange([0xFF, 0xE2, (byte)(length >> 8), (byte)length]);
                output.AddRange("ICC_PROFILE\0"u8.ToArray());
                output.AddRange([(byte)(i + 1), (byte)chunks]);
                output.AddRange(part.ToArray());
            }
        }
        foreach (var segment in first.Quantization)
            output.AddRange(segment);
        var frame = (byte[])first.Frame!.Clone();
        (frame[5], frame[6]) = ((byte)(height >> 8), (byte)height);
        output.AddRange(frame);
        if (parsed.Count == 1)
        {
            foreach (var segment in first.HuffmanSegments)
                output.AddRange(segment);
            if (first.Restart > 0)
                output.AddRange([0xFF, 0xDD, 0, 4, (byte)(first.Restart >> 8), (byte)first.Restart]);
            output.AddRange(first.Scan!);
            output.AddRange(first.Data.AsSpan(first.DataStart, first.DataEnd - first.DataStart).ToArray());
            output.AddRange([0xFF, 0xD9]);
            return [.. output];
        }

        // Several strips: one restart interval per strip (or the strips' own intervals when strips hold whole ones).
        var components = Components(first.Frame!);
        var (hMax, vMax) = (components.Max(c => c.H), components.Max(c => c.V));
        var scan = Scan(first.Scan!, components);
        if (scan == null || scan.Length != components.Length || parsed.Any(p => !p.Quantization.SelectMany(q => q).SequenceEqual(first.Quantization.SelectMany(q => q))
                || !p.Scan!.AsSpan().SequenceEqual(first.Scan) || Components(p.Frame!).Length != components.Length))
            return null;
        var (mcuW, mcuH) = scan.Length == 1 ? (8 * hMax / scan[0].Component.H, 8 * vMax / scan[0].Component.V) : (8 * hMax, 8 * vMax);
        var columns = (width + mcuW - 1) / mcuW;
        var stripMcus = columns * (rowsPerStrip / mcuH);
        var own = first.Restart;
        var interval = own > 0 && stripMcus % own == 0 ? own : stripMcus;
        if (rowsPerStrip % mcuH != 0 || interval is <= 0 or > 65535 || own > 0 && stripMcus % own != 0)
            return null;
        var shared = parsed.All(p => p.HuffmanSegments.SelectMany(h => h).SequenceEqual(first.HuffmanSegments.SelectMany(h => h)));
        if (!shared)
        {
            foreach (var (id, table) in Standard.Tables)
            {
                output.AddRange([0xFF, 0xC4, (byte)((19 + table.Values.Length) >> 8), (byte)(19 + table.Values.Length), (byte)id]);
                output.AddRange(table.Counts.AsSpan(1, 16).ToArray());
                output.AddRange(table.Values);
            }
        }
        else
        {
            foreach (var segment in first.HuffmanSegments)
                output.AddRange(segment);
        }
        output.AddRange([0xFF, 0xDD, 0, 4, (byte)(interval >> 8), (byte)interval]);
        output.AddRange(first.Scan!);
        var writer = new BitWriter(output);
        var restarts = 0;
        for (var i = 0; i < parsed.Count; i++)
        {
            var strip = parsed[i];
            if (i > 0)
                writer.Marker((byte)(0xD0 + restarts++ % 8));
            if (shared)
            {
                // The same tables: the scan's bytes as they are, the strip's own restart markers renumbered.
                for (var p = strip.DataStart; p < strip.DataEnd; p++)
                {
                    var b = strip.Data[p];
                    if (b == 0xFF && p + 1 < strip.DataEnd && strip.Data[p + 1] is >= 0xD0 and <= 0xD7)
                    {
                        output.AddRange([0xFF, (byte)(0xD0 + restarts++ % 8)]);
                        p++;
                    }
                    else
                    {
                        output.Add(b);
                    }
                }
                continue;
            }
            var frameHeight = strip.Frame![5] << 8 | strip.Frame[6];
            var mcus = columns * ((Math.Min(frameHeight, rowsPerStrip) + mcuH - 1) / mcuH);
            var reader = new BitReader(strip.Data, strip.DataStart, strip.DataEnd);
            for (var m = 0; m < mcus; m++)
            {
                if (m > 0 && own > 0 && m % own == 0)
                {
                    reader.Restart();
                    writer.Marker((byte)(0xD0 + restarts++ % 8));
                }
                foreach (var (component, dc, ac) in scan)
                {
                    var blocks = scan.Length == 1 ? 1 : component.H * component.V;
                    var (dcIn, acIn) = (strip.Huffman.GetValueOrDefault(dc), strip.Huffman.GetValueOrDefault(0x10 | ac));
                    var (dcOut, acOut) = (Standard.Table(dc), Standard.Table(0x10 | ac));
                    if (dcIn == null || acIn == null)
                        return null;
                    for (var b = 0; b < blocks; b++)
                    {
                        var size = dcIn.Decode(reader);
                        if (size is < 0 or > 11)
                            return null;
                        writer.Write(dcOut.Code[size], dcOut.Length[size]);
                        writer.Write(reader.Receive(size), size);
                        for (var k = 1; k < 64;)
                        {
                            var rs = acIn.Decode(reader);
                            if (rs < 0 || acOut.Length[rs] == 0)
                                return null;
                            writer.Write(acOut.Code[rs], acOut.Length[rs]);
                            if ((rs & 15) == 0)
                            {
                                if (rs != 0xF0)
                                    break;   // end of block
                                k += 16;
                                continue;
                            }
                            writer.Write(reader.Receive(rs & 15), rs & 15);
                            k += (rs >> 4) + 1;
                        }
                    }
                }
            }
        }
        writer.Pad();
        output.AddRange([0xFF, 0xD9]);
        return [.. output];
    }

    record struct Component(int Id, int H, int V);

    static Component[] Components(byte[] frame)
    {
        var count = frame[9];
        var result = new Component[count];
        for (var c = 0; c < count && 12 + 3 * c < frame.Length; c++)
            result[c] = new Component(frame[10 + 3 * c], Math.Max(1, frame[11 + 3 * c] >> 4), Math.Max(1, frame[11 + 3 * c] & 15));
        return result;
    }

    /// <summary>The scan's components in order with their DC and AC table ids, or null for a component the frame lacks.</summary>
    static (Component Component, int Dc, int Ac)[]? Scan(byte[] scan, Component[] components)
    {
        var count = scan[4];
        var result = new (Component, int, int)[count];
        for (var i = 0; i < count; i++)
        {
            var id = scan[5 + 2 * i];
            var index = Array.FindIndex(components, c => c.Id == id);
            if (index < 0)
                return null;
            result[i] = (components[index], scan[6 + 2 * i] >> 4, scan[6 + 2 * i] & 15);
        }
        return result;
    }

    /// <summary>A strip's markers: tables, frame and scan headers, and where its entropy-coded data lies.</summary>
    sealed class Stream(byte[] data)
    {
        public byte[] Data => data;
        public readonly List<byte[]> Quantization = [];
        public readonly List<byte[]> HuffmanSegments = [];
        public readonly List<byte[]> Other = [];
        public readonly Dictionary<int, Huffman> Huffman = [];
        public byte[]? Frame, Scan;
        public int Restart, DataStart, DataEnd;

        public bool Read(byte[] bytes, bool entropy)
        {
            if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8)
                return false;
            var at = 2;
            while (at + 4 <= bytes.Length)
            {
                if (bytes[at] != 0xFF)
                    return false;
                var marker = bytes[at + 1];
                if (marker == 0xFF)
                {
                    at++;   // fill byte
                    continue;
                }
                if (marker == 0xD9)
                    return !entropy;
                var length = bytes[at + 2] << 8 | bytes[at + 3];
                if (at + 2 + length > bytes.Length)
                    return false;
                var segment = bytes.AsSpan(at, 2 + length).ToArray();
                switch (marker)
                {
                    case 0xDB:
                        Quantization.Add(segment);
                        break;
                    case 0xC4:
                        HuffmanSegments.Add(segment);
                        for (var p = 4; p + 17 <= segment.Length;)
                        {
                            var counts = new byte[17];
                            Array.Copy(segment, p + 1, counts, 1, 16);
                            var total = counts.Sum(c => c);
                            if (p + 17 + total > segment.Length)
                                return false;
                            Huffman[segment[p]] = new Huffman(counts, segment.AsSpan(p + 17, total).ToArray());
                            p += 17 + total;
                        }
                        break;
                    case 0xC0 or 0xC1:
                        Frame = segment;
                        break;
                    case 0xDD:
                        Restart = segment[4] << 8 | segment[5];
                        break;
                    case 0xDA when entropy:
                        Scan = segment;
                        DataStart = at + 2 + length;
                        DataEnd = DataStart;
                        // The scan runs to the next marker that is neither stuffing nor a restart.
                        while (DataEnd + 1 < bytes.Length && !(bytes[DataEnd] == 0xFF && bytes[DataEnd + 1] is not (0x00 or >= 0xD0 and <= 0xD7)))
                            DataEnd++;
                        if (DataEnd + 1 >= bytes.Length)
                            DataEnd = bytes.Length;
                        return true;
                    case >= 0xC2 and <= 0xCF or 0xDA:
                        return false;   // progressive, lossless or arithmetic coding; a scan in the tables
                    default:
                        if (marker is >= 0xE0 and <= 0xEF && entropy && Frame == null)
                            Other.Add(segment);   // application data (JFIF, ICC profile)
                        break;
                }
                at += 2 + length;
            }
            return !entropy;
        }
    }

    /// <summary>A Huffman table (BITS counts by code length, HUFFVAL symbols) for decoding and encoding.</summary>
    sealed class Huffman
    {
        public readonly byte[] Counts, Values;
        public readonly int[] Code = new int[256], Length = new int[256];
        readonly int[] maxCode = new int[18], minCode = new int[17], pointer = new int[17];

        public Huffman(byte[] counts, byte[] values)
        {
            (Counts, Values) = (counts, values);
            var (code, k) = (0, 0);
            for (var length = 1; length <= 16; length++)
            {
                (pointer[length], minCode[length]) = (k, code);
                for (var i = 0; i < counts[length] && k < values.Length; i++, k++, code++)
                    (Code[values[k]], Length[values[k]]) = (code, length);
                maxCode[length] = counts[length] > 0 ? code - 1 : -1;
                code <<= 1;
            }
        }

        public int Decode(BitReader reader)
        {
            var code = 0;
            for (var length = 1; length <= 16; length++)
            {
                code = code << 1 | reader.Bit();
                if (code <= maxCode[length])
                    return Values[pointer[length] + code - minCode[length]];
            }
            return -1;
        }
    }

    /// <summary>The typical tables of ITU T.81 Annex K.3, which code every symbol of 8-bit baseline data.</summary>
    static class Standard
    {
        static readonly Huffman DcLuminance = new([0, 0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0], [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11]);
        static readonly Huffman DcChrominance = new([0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0], [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11]);
        static readonly Huffman AcLuminance = new([0, 0, 2, 1, 3, 3, 2, 4, 3, 5, 5, 4, 4, 0, 0, 1, 0x7D],
        [
            0x01, 0x02, 0x03, 0x00, 0x04, 0x11, 0x05, 0x12, 0x21, 0x31, 0x41, 0x06, 0x13, 0x51, 0x61, 0x07,
            0x22, 0x71, 0x14, 0x32, 0x81, 0x91, 0xA1, 0x08, 0x23, 0x42, 0xB1, 0xC1, 0x15, 0x52, 0xD1, 0xF0,
            0x24, 0x33, 0x62, 0x72, 0x82, 0x09, 0x0A, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x25, 0x26, 0x27, 0x28,
            0x29, 0x2A, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49,
            0x4A, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5A, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69,
            0x6A, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89,
            0x8A, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9A, 0xA2, 0xA3, 0xA4, 0xA5, 0xA6, 0xA7,
            0xA8, 0xA9, 0xAA, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA, 0xC2, 0xC3, 0xC4, 0xC5,
            0xC6, 0xC7, 0xC8, 0xC9, 0xCA, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0xDA, 0xE1, 0xE2,
            0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xF1, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7, 0xF8,
            0xF9, 0xFA,
        ]);
        static readonly Huffman AcChrominance = new([0, 0, 2, 1, 2, 4, 4, 3, 4, 7, 5, 4, 4, 0, 1, 2, 0x77],
        [
            0x00, 0x01, 0x02, 0x03, 0x11, 0x04, 0x05, 0x21, 0x31, 0x06, 0x12, 0x41, 0x51, 0x07, 0x61, 0x71,
            0x13, 0x22, 0x32, 0x81, 0x08, 0x14, 0x42, 0x91, 0xA1, 0xB1, 0xC1, 0x09, 0x23, 0x33, 0x52, 0xF0,
            0x15, 0x62, 0x72, 0xD1, 0x0A, 0x16, 0x24, 0x34, 0xE1, 0x25, 0xF1, 0x17, 0x18, 0x19, 0x1A, 0x26,
            0x27, 0x28, 0x29, 0x2A, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48,
            0x49, 0x4A, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5A, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68,
            0x69, 0x6A, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87,
            0x88, 0x89, 0x8A, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9A, 0xA2, 0xA3, 0xA4, 0xA5,
            0xA6, 0xA7, 0xA8, 0xA9, 0xAA, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA, 0xC2, 0xC3,
            0xC4, 0xC5, 0xC6, 0xC7, 0xC8, 0xC9, 0xCA, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0xDA,
            0xE2, 0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7, 0xF8,
            0xF9, 0xFA,
        ]);

        /// <summary>The tables by class and id (DC 0x00 and 0x01, AC 0x10 and 0x11): id 0 luminance, the others chrominance.</summary>
        public static Huffman Table(int classAndId) => classAndId switch
        {
            0x00 => DcLuminance,
            < 0x10 => DcChrominance,
            0x10 => AcLuminance,
            _ => AcChrominance,
        };

        public static IEnumerable<(int Id, Huffman Table)> Tables =>
            [(0x00, DcLuminance), (0x01, DcChrominance), (0x10, AcLuminance), (0x11, AcChrominance)];
    }

    sealed class BitReader(byte[] data, int position, int end)
    {
        int buffer, bits;

        public int Bit()
        {
            if (bits == 0)
            {
                if (position >= end)
                    return 1;
                var b = data[position];
                if (b == 0xFF)
                {
                    if (position + 1 < end && data[position + 1] == 0)
                        position += 2;
                    else
                        return 1;   // a marker: padding until the caller restarts
                }
                else
                {
                    position++;
                }
                (buffer, bits) = (b, 8);
            }
            return buffer >> --bits & 1;
        }

        public int Receive(int count)
        {
            var value = 0;
            for (var i = 0; i < count; i++)
                value = value << 1 | Bit();
            return value;
        }

        /// <summary>Drops the padding bits and steps over the restart marker that follows.</summary>
        public void Restart()
        {
            bits = 0;
            while (position + 1 < end && !(data[position] == 0xFF && data[position + 1] is >= 0xD0 and <= 0xD7))
                position++;
            position += 2;
        }
    }

    sealed class BitWriter(List<byte> output)
    {
        int buffer, bits;

        public void Write(int value, int count)
        {
            for (var i = count - 1; i >= 0; i--)
            {
                buffer = buffer << 1 | value >> i & 1;
                if (++bits == 8)
                {
                    output.Add((byte)buffer);
                    if (buffer == 0xFF)
                        output.Add(0);
                    (buffer, bits) = (0, 0);
                }
            }
        }

        /// <summary>Fills the last byte with one bits.</summary>
        public void Pad()
        {
            while (bits != 0)
                Write(1, 1);
        }

        public void Marker(byte marker)
        {
            Pad();
            output.AddRange([0xFF, marker]);
        }
    }
}
