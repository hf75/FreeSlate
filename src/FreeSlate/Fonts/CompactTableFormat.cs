using System.Buffers.Binary;

namespace FreeSlate.Fonts;

/// <summary>
/// Compact Table Format ([MTX] chapters 4 to 7) back to TrueType. The first block is the font with its table directory,
/// where 'loca' is empty, 'cvt ' holds variable-length deltas, 'hdmx' and 'VDMX' the differences to the values a
/// prediction gives and 'glyf' the glyphs in a compact form: a contour's point count instead of its end point, the
/// points as variable-length triplets of x and y delta and on-curve flag, the bounding box only where the points do
/// not give it, and two counts for the glyph's instructions, whose initial pushes come from the second block and whose
/// remaining code comes from the third. The font is written with a long 'loca'.
/// </summary>
static class CompactTableFormat
{
    public static byte[] ToTrueType(byte[] font, byte[] pushData, byte[] code)
    {
        var count = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4));
        var tables = new List<(string Tag, byte[] Data)>();
        for (var i = 0; i < count; i++)
        {
            var record = font.AsSpan(12 + 16 * i);
            var tag = System.Text.Encoding.ASCII.GetString(record[..4]);
            var offset = (int)BinaryPrimitives.ReadUInt32BigEndian(record[8..]);
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(record[12..]);
            tables.Add((tag, font.AsSpan(offset, length).ToArray()));
        }
        byte[] Table(string tag) => tables.FirstOrDefault(t => t.Tag == tag).Data
                                     ?? throw new InvalidDataException($"Compact Table Format font without '{tag}' table.");

        var head = Table("head");
        var maxp = Table("maxp");
        var glyphCount = BinaryPrimitives.ReadUInt16BigEndian(maxp.AsSpan(4));
        var unitsPerEm = BinaryPrimitives.ReadUInt16BigEndian(head.AsSpan(18));

        var (glyphs, longestProgram) = tables.Any(t => t.Tag == "glyf")
            ? Glyphs(Table("glyf"), glyphCount, new Reader(pushData), new Reader(code))
            : (Array.Empty<byte[]>(), 0);
        var result = new List<(string Tag, byte[] Data)>();
        foreach (var (tag, data) in tables)
        {
            switch (tag)
            {
                case "glyf":
                    result.Add((tag, glyphs.SelectMany(g => g).ToArray()));
                    break;
                case "loca":
                    var loca = new byte[4 * (glyphCount + 1)];
                    var position = 0;
                    for (var g = 0; g < glyphs.Length; g++)
                    {
                        BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan(4 * g), (uint)position);
                        position += glyphs[g].Length;
                    }
                    BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan(4 * glyphCount), (uint)position);
                    result.Add((tag, loca));
                    break;
                case "cvt ":
                    result.Add((tag, ControlValues(data)));
                    break;
                case "hdmx":
                    result.Add((tag, DeviceWidths(data, glyphCount, unitsPerEm, Advances(Table("hhea"), Table("hmtx"), glyphCount))));
                    break;
                case "VDMX":
                    result.Add((tag, VerticalExtremes(data)));
                    break;
                case "head":
                    var newHead = data.ToArray();
                    BinaryPrimitives.WriteUInt32BigEndian(newHead.AsSpan(8), 0);   // checkSumAdjustment, set at the end
                    BinaryPrimitives.WriteInt16BigEndian(newHead.AsSpan(50), 1);   // indexToLocFormat: long offsets
                    result.Add((tag, newHead));
                    break;
                case "maxp":
                    var newMaxp = data.ToArray();
                    // The glyph programs' new pushes may be longer than the original ones.
                    if (newMaxp.Length >= 28 && BinaryPrimitives.ReadUInt32BigEndian(newMaxp) == 0x00010000
                        && BinaryPrimitives.ReadUInt16BigEndian(newMaxp.AsSpan(26)) < longestProgram)
                        BinaryPrimitives.WriteUInt16BigEndian(newMaxp.AsSpan(26), (ushort)Math.Min(longestProgram, ushort.MaxValue));
                    result.Add((tag, newMaxp));
                    break;
                default:
                    result.Add((tag, data));
                    break;
            }
        }
        return Sfnt(BinaryPrimitives.ReadUInt32BigEndian(font), result);
    }

    /// <summary>The glyphs as TrueType glyph records, each padded to four bytes, and the longest glyph program.</summary>
    static (byte[][] Glyphs, int LongestProgram) Glyphs(byte[] glyf, int count, Reader push, Reader code)
    {
        var input = new Reader(glyf);
        var glyphs = new byte[count][];
        var longest = 0;
        for (var g = 0; g < count; g++)
        {
            var output = new Writer();
            var contours = input.Int16();
            if (contours == 0)
            {
                glyphs[g] = [];
                continue;
            }
            byte[]? program = null;
            if (contours < 0)
            {
                output.Int16(-1);
                for (var i = 0; i < 4; i++)
                    output.Int16(input.Int16());   // bounding box
                int flags;
                do
                {
                    flags = input.UInt16();
                    output.UInt16(flags);
                    output.UInt16(input.UInt16());   // glyph index
                    var arguments = (flags & 0x0001) != 0 ? 4 : 2;
                    var transform = (flags & 0x0008) != 0 ? 2 : (flags & 0x0040) != 0 ? 4 : (flags & 0x0080) != 0 ? 8 : 0;
                    output.Bytes(input.Bytes(arguments + transform));
                } while ((flags & 0x0020) != 0);
                if ((flags & 0x0100) != 0)
                {
                    program = Program(input, push, code);
                    output.UInt16(program.Length);
                    output.Bytes(program);
                }
            }
            else
            {
                var boxGiven = contours == 0x7FFF;
                var box = new int[4];
                if (boxGiven)
                {
                    contours = input.Int16();
                    for (var i = 0; i < 4; i++)
                        box[i] = input.Int16();
                }
                var ends = new int[contours];
                for (var c = 0; c < contours; c++)
                    ends[c] = (c == 0 ? 0 : ends[c - 1]) + input.UShort255();
                var points = contours > 0 ? ends[^1] + 1 : 0;
                var flags = input.Bytes(points);
                var xs = new int[points];
                var ys = new int[points];
                int x = 0, y = 0;
                for (var p = 0; p < points; p++)
                {
                    var (dx, dy) = Triplet(flags[p] & 0x7F, input);
                    xs[p] = x += dx;
                    ys[p] = y += dy;
                }
                program = Program(input, push, code);
                if (!boxGiven && points > 0)
                    box = [xs.Min(), ys.Min(), xs.Max(), ys.Max()];

                output.Int16(contours);
                foreach (var value in box)
                    output.Int16(value);
                foreach (var end in ends)
                    output.UInt16(end);
                output.UInt16(program.Length);
                output.Bytes(program);
                Points(output, flags, xs, ys);
            }
            longest = Math.Max(longest, program?.Length ?? 0);
            output.Align(4);
            glyphs[g] = output.ToArray();
        }
        return (glyphs, longest);
    }

    /// <summary>A glyph's program: its push count and code size, the pushes rebuilt as push instructions, then the code.</summary>
    static byte[] Program(Reader glyf, Reader push, Reader code)
    {
        var pushCount = glyf.UShort255();
        var codeSize = glyf.UShort255();
        var values = new int[pushCount];
        for (var i = 0; i < pushCount;)
        {
            // Hop codes repeat the value two places back: A x1 hop3 x2 is A x1 A x2 A, A x1 hop4 x2 x3 is A x1 A x2 A x3 A.
            var hop = push.Peek() switch { 251 => 1, 252 => 2, _ => 0 };
            if (hop == 0)
            {
                values[i++] = push.Short255();
                continue;
            }
            push.Byte();
            if (i < 2 || i + 2 * hop + 1 > pushCount)
                throw new InvalidDataException("Push data hop code out of place.");
            var repeated = values[i - 2];
            values[i++] = repeated;
            for (var k = 0; k < hop; k++)
            {
                values[i++] = push.Short255();
                values[i++] = repeated;
            }
        }
        var output = new Writer();
        Pushes(output, values);
        output.Bytes(code.Bytes(codeSize));
        return output.ToArray();
    }

    /// <summary>TrueType push instructions for the values: runs of bytes with (N)PUSHB, runs of other values with (N)PUSHW.</summary>
    static void Pushes(Writer output, int[] values)
    {
        for (var i = 0; i < values.Length;)
        {
            var bytes = values[i] is >= 0 and <= 255;
            var end = i;
            while (end < values.Length && end - i < 255 && (values[end] is >= 0 and <= 255) == bytes)
                end++;
            var n = end - i;
            if (n <= 8)
                output.Byte((bytes ? 0xB0 : 0xB8) + n - 1);
            else
            {
                output.Byte(bytes ? 0x40 : 0x41);
                output.Byte(n);
            }
            for (; i < end; i++)
            {
                if (bytes)
                    output.Byte(values[i]);
                else
                    output.Int16(values[i]);
            }
        }
    }

    /// <summary>
    /// The 128 triplet encodings ([MTX] 5.11): bytes in all (with the flag byte), bits of x and y, what is added to
    /// them, and their signs.
    /// </summary>
    static readonly (int Bytes, int XBits, int YBits, int DeltaX, int DeltaY, bool XNegative, bool YNegative)[] Triplets = MakeTriplets();

    static (int, int, int, int, int, bool, bool)[] MakeTriplets()
    {
        var table = new (int, int, int, int, int, bool, bool)[128];
        for (var i = 0; i < 10; i++)
            table[i] = (2, 0, 8, 0, i / 2 * 256, false, i % 2 == 0);
        for (var i = 0; i < 10; i++)
            table[10 + i] = (2, 8, 0, i / 2 * 256, 0, i % 2 == 0, false);
        for (var i = 0; i < 64; i++)
            table[20 + i] = (2, 4, 4, 1 + 16 * (i / 16), 1 + 16 * (i % 16 / 4), i % 2 == 0, i % 4 < 2);
        for (var i = 0; i < 36; i++)
            table[84 + i] = (3, 8, 8, 1 + 256 * (i / 12), 1 + 256 * (i % 12 / 4), i % 2 == 0, i % 4 < 2);
        for (var i = 0; i < 4; i++)
        {
            table[120 + i] = (4, 12, 12, 0, 0, i % 2 == 0, i < 2);
            table[124 + i] = (5, 16, 16, 0, 0, i % 2 == 0, i < 2);
        }
        return table;
    }

    static (int Dx, int Dy) Triplet(int index, Reader input)
    {
        var (bytes, xBits, yBits, deltaX, deltaY, xNegative, yNegative) = Triplets[index];
        var data = 0L;
        for (var i = 1; i < bytes; i++)
            data = data << 8 | input.Byte();
        var total = 8 * (bytes - 1);
        var dx = (int)(data >> total - xBits & (1L << xBits) - 1) + deltaX;
        var dy = (int)(data >> total - xBits - yBits & (1L << yBits) - 1) + deltaY;
        return (xNegative ? -dx : dx, yNegative ? -dy : dy);
    }

    /// <summary>A simple glyph's flags and coordinates in TrueType's form: short deltas where they fit, repeated flags folded.</summary>
    static void Points(Writer output, byte[] compactFlags, int[] xs, int[] ys)
    {
        var flags = new byte[xs.Length];
        var xBytes = new Writer();
        var yBytes = new Writer();
        for (var p = 0; p < xs.Length; p++)
        {
            var flag = (compactFlags[p] & 0x80) == 0 ? 0x01 : 0x00;
            flag |= Delta(xs[p] - (p > 0 ? xs[p - 1] : 0), xBytes, 0x02, 0x10);
            flag |= Delta(ys[p] - (p > 0 ? ys[p - 1] : 0), yBytes, 0x04, 0x20);
            flags[p] = (byte)flag;
        }
        for (var p = 0; p < flags.Length;)
        {
            var repeats = 0;
            while (p + repeats + 1 < flags.Length && flags[p + repeats + 1] == flags[p] && repeats < 255)
                repeats++;
            if (repeats > 0)
            {
                output.Byte(flags[p] | 0x08);
                output.Byte(repeats);
            }
            else
                output.Byte(flags[p]);
            p += repeats + 1;
        }
        output.Bytes(xBytes.ToArray());
        output.Bytes(yBytes.ToArray());
    }

    static int Delta(int delta, Writer bytes, int shortFlag, int sameOrPositive)
    {
        if (delta == 0)
            return sameOrPositive;
        if (delta is >= -255 and <= 255)
        {
            bytes.Byte(Math.Abs(delta));
            return shortFlag | (delta > 0 ? sameOrPositive : 0);
        }
        bytes.Int16(delta);
        return 0;
    }

    /// <summary>'cvt ': a count, then every value as the delta to the one before in 1 to 3 bytes.</summary>
    static byte[] ControlValues(byte[] data)
    {
        var input = new Reader(data);
        var count = input.UInt16();
        var output = new byte[2 * count];
        var value = 0;
        for (var i = 0; i < count; i++)
        {
            int code = input.Byte();
            value += code switch
            {
                < 238 => code,
                238 => input.Int16(),
                <= 247 => -(238 * (code - 239) + input.Byte()),
                _ => 238 * (code - 247) + input.Byte(),
            };
            BinaryPrimitives.WriteInt16BigEndian(output.AsSpan(2 * i), (short)value);
        }
        return output;
    }

    /// <summary>The advance widths of all glyphs from 'hmtx' (the last one repeated beyond numberOfHMetrics).</summary>
    static int[] Advances(byte[] hhea, byte[] hmtx, int glyphCount)
    {
        var metrics = Math.Max(1, (int)BinaryPrimitives.ReadUInt16BigEndian(hhea.AsSpan(34)));
        var advances = new int[glyphCount];
        for (var g = 0; g < glyphCount; g++)
            advances[g] = BinaryPrimitives.ReadUInt16BigEndian(hmtx.AsSpan(4 * Math.Min(g, metrics - 1)));
        return advances;
    }

    /// <summary>
    /// 'hdmx': each device record's widths as the difference to the advance scaled to its size and rounded, coded by
    /// magnitude ([MTX] 5.3 and 5.4); version 0xFFFF marks a table stored as it is.
    /// </summary>
    static byte[] DeviceWidths(byte[] data, int glyphCount, int unitsPerEm, int[] advances)
    {
        var input = new Reader(data);
        var version = input.UInt16();
        if (version == 0xFFFF)
        {
            var stored = data.ToArray();
            BinaryPrimitives.WriteUInt16BigEndian(stored, 0);
            return stored;
        }
        var records = input.UInt16();
        var recordSize = input.Int32();
        if (recordSize < 2 + glyphCount)
            recordSize = (2 + glyphCount + 3) & ~3;
        var sizes = new (int Ppem, int MaxWidth)[records];
        for (var r = 0; r < records; r++)
            sizes[r] = (input.Byte(), input.Byte());
        var surprises = new MagnitudeReader(data, input.Position);
        var output = new Writer();
        output.UInt16(version);
        output.UInt16(records);
        output.Int32(recordSize);
        foreach (var (ppem, maxWidth) in sizes)
        {
            output.Byte(ppem);
            output.Byte(maxWidth);
            for (var g = 0; g < glyphCount; g++)
            {
                var predicted = ((64L * ppem * advances[g] + unitsPerEm / 2) / unitsPerEm + 32) / 64;
                output.Byte((int)predicted + surprises.Next());
            }
            for (var pad = 2 + glyphCount; pad < recordSize; pad++)
                output.Byte(0);
        }
        return output.ToArray();
    }

    /// <summary>
    /// 'VDMX': every group's sizes and extremes as the differences to the previous size plus one and to the size times
    /// the group's two multipliers ([MTX] 5.5); versions 0xFFFF and 0xFFFE mark a table stored as it is.
    /// </summary>
    static byte[] VerticalExtremes(byte[] data)
    {
        var input = new Reader(data);
        var version = input.UInt16();
        if (version >= 0xFFFE)
        {
            var stored = data.ToArray();
            BinaryPrimitives.WriteUInt16BigEndian(stored, (ushort)(0xFFFF - version));
            return stored;
        }
        var groupCount = input.UInt16();
        var ratioCount = input.UInt16();
        var ratios = input.Bytes(4 * ratioCount);
        var offsets = new int[ratioCount];
        for (var i = 0; i < ratioCount; i++)
            offsets[i] = input.UInt16();
        var groups = new List<List<(int Ppem, int YMax, int YMin)>>();
        var position = ratioCount > 0 ? offsets[0] : input.Position;
        for (var i = 0; i < groupCount; i++)
        {
            input.Position = position;
            var records = input.UInt16();
            var maxFactor = input.Int16();
            var minFactor = input.Int16();
            var errors = new MagnitudeReader(data, input.Position);
            var group = new List<(int, int, int)>();
            var predictedPpem = 8;
            for (var r = 0; r < records; r++)
            {
                var ppem = predictedPpem + errors.Next();
                var yMax = (ppem * maxFactor + 1024) / 2048 + errors.Next();
                var yMin = -((ppem * minFactor + 1024) / 2048) + errors.Next();
                group.Add((ppem, yMax, yMin));
                predictedPpem = ppem + 1;
            }
            groups.Add(group);
            position = errors.NextByte;
        }

        // Ratios share a group where their stored offsets are the same; groups follow each other in the stored order.
        var distinct = offsets.Distinct().Order().ToList();
        var output = new Writer();
        output.UInt16(version);
        output.UInt16(groupCount);
        output.UInt16(ratioCount);
        output.Bytes(ratios);
        var starts = new List<int>();
        var start = 6 + 6 * ratioCount;
        foreach (var group in groups)
        {
            starts.Add(start);
            start += 4 + 6 * group.Count;
        }
        foreach (var offset in offsets)
            output.UInt16(starts.Count == 0 ? 0 : starts[Math.Min(distinct.IndexOf(offset), starts.Count - 1)]);
        foreach (var group in groups)
        {
            output.UInt16(group.Count);
            output.Byte(group.Count > 0 ? group[0].Ppem : 0);
            output.Byte(group.Count > 0 ? group[^1].Ppem : 0);
            foreach (var (ppem, yMax, yMin) in group)
            {
                output.UInt16(ppem);
                output.Int16(yMax);
                output.Int16(yMin);
            }
        }
        return output.ToArray();
    }

    /// <summary>An sfnt file of the tables in their order, with checksums and head.checkSumAdjustment.</summary>
    static byte[] Sfnt(uint version, List<(string Tag, byte[] Data)> tables)
    {
        var n = tables.Count;
        var power = 1;
        while (power * 2 <= n)
            power *= 2;
        var output = new Writer();
        output.Int32((int)version);
        output.UInt16(n);
        output.UInt16(16 * power);
        output.UInt16(int.Log2(power));
        output.UInt16(16 * n - 16 * power);
        var offset = 12 + 16 * n;
        foreach (var (tag, data) in tables)
        {
            output.Bytes(System.Text.Encoding.ASCII.GetBytes(tag));
            output.Int32((int)Checksum(data));
            output.Int32(offset);
            output.Int32(data.Length);
            offset += (data.Length + 3) & ~3;
        }
        var headAt = -1;
        foreach (var (tag, data) in tables)
        {
            if (tag == "head")
                headAt = output.Length;
            output.Bytes(data);
            output.Align(4);
        }
        var font = output.ToArray();
        if (headAt >= 0)
            BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(headAt + 8), 0xB1B0AFBA - Checksum(font));
        return font;
    }

    static uint Checksum(byte[] data)
    {
        var sum = 0u;
        for (var i = 0; i < data.Length; i += 4)
        {
            var word = 0u;
            for (var k = 0; k < 4; k++)
                word = word << 8 | (i + k < data.Length ? data[i + k] : 0u);
            sum += word;
        }
        return sum;
    }

    /// <summary>Big-endian reading, with the 255USHORT and 255SHORT types of [MTX] 6.1.</summary>
    sealed class Reader(byte[] data)
    {
        public int Position { get; set; }

        public int Peek() => Position < data.Length ? data[Position] : throw new InvalidDataException("Compact Table Format data ends early.");

        public int Byte()
        {
            var value = Peek();
            Position++;
            return value;
        }

        public int UInt16() => Byte() << 8 | Byte();

        public int Int16() => (short)UInt16();

        public int Int32() => UInt16() << 16 | UInt16();

        public byte[] Bytes(int count)
        {
            if (count > data.Length - Position)
                throw new InvalidDataException("Compact Table Format data ends early.");
            var bytes = data.AsSpan(Position, count).ToArray();
            Position += count;
            return bytes;
        }

        /// <summary>0 … 252 as they are, 253 and a word, 254 and a byte for 506 on, 255 and a byte for 253 on.</summary>
        public int UShort255() => Byte() switch
        {
            253 => UInt16(),
            254 => 506 + Byte(),
            255 => 253 + Byte(),
            var code => code,
        };

        /// <summary>0 … 249 as they are, 250 negating what follows, 253 and a word, 254 and a byte for 500 on, 255 and a byte for 250 on.</summary>
        public int Short255()
        {
            var code = Byte();
            if (code == 253)
                return Int16();
            var sign = 1;
            if (code == 250)
            {
                sign = -1;
                code = Byte();
            }
            return sign * code switch
            {
                254 => 500 + Byte(),
                255 => 250 + Byte(),
                _ => code,
            };
        }
    }

    /// <summary>
    /// Values coded by magnitude ([MTX] 5.3), bits from the least significant one: as many 1s as the value's magnitude,
    /// a 0, then for a value other than 0 its sign (1 negative).
    /// </summary>
    sealed class MagnitudeReader(byte[] data, int start)
    {
        long bit = 8L * start;

        /// <summary>Where the next whole byte starts.</summary>
        public int NextByte => (int)((bit + 7) >> 3);

        int Bit()
        {
            var index = bit >> 3;
            if (index >= data.Length)
                throw new InvalidDataException("Magnitude-coded data ends early.");
            return data[index] >> (int)(bit++ & 7) & 1;
        }

        public int Next()
        {
            var magnitude = 0;
            while (Bit() == 1)
                magnitude++;
            return magnitude == 0 || Bit() == 0 ? magnitude : -magnitude;
        }
    }

    sealed class Writer
    {
        readonly List<byte> bytes = [];

        public int Length => bytes.Count;

        public void Byte(int value) => bytes.Add((byte)value);

        public void UInt16(int value)
        {
            bytes.Add((byte)(value >> 8));
            bytes.Add((byte)value);
        }

        public void Int16(int value) => UInt16(value & 0xFFFF);

        public void Int32(int value)
        {
            UInt16(value >> 16 & 0xFFFF);
            UInt16(value & 0xFFFF);
        }

        public void Bytes(byte[] data) => bytes.AddRange(data);

        public void Align(int unit)
        {
            while (bytes.Count % unit != 0)
                bytes.Add(0);
        }

        public byte[] ToArray() => bytes.ToArray();
    }
}
