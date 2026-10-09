namespace FreeSlate.Html;

/// <summary>
/// CCITT fax decoding for TIFF pictures (compression 2 modified Huffman, 3 group 3 with one- or two-dimensional rows,
/// 4 group 4): rows of alternating white and black runs, white first, as 1-bit pixels with black runs set (TIFF's
/// photometric interpretation then says which value is black).
/// </summary>
static class Fax
{
    static readonly string[] WhiteTerminating =
    [
        "00110101", "000111", "0111", "1000", "1011", "1100", "1110", "1111", "10011", "10100", "00111", "01000", "001000",
        "000011", "110100", "110101", "101010", "101011", "0100111", "0001100", "0001000", "0010111", "0000011", "0000100",
        "0101000", "0101011", "0010011", "0100100", "0011000", "00000010", "00000011", "00011010", "00011011", "00010010",
        "00010011", "00010100", "00010101", "00010110", "00010111", "00101000", "00101001", "00101010", "00101011",
        "00101100", "00101101", "00000100", "00000101", "00001010", "00001011", "01010010", "01010011", "01010100",
        "01010101", "00100100", "00100101", "01011000", "01011001", "01011010", "01011011", "01001010", "01001011",
        "00110010", "00110011", "00110100",
    ];

    static readonly string[] WhiteMakeup =
    [
        "11011", "10010", "010111", "0110111", "00110110", "00110111", "01100100", "01100101", "01101000", "01100111",
        "011001100", "011001101", "011010010", "011010011", "011010100", "011010101", "011010110", "011010111",
        "011011000", "011011001", "011011010", "011011011", "010011000", "010011001", "010011010", "011000", "010011011",
    ];

    static readonly string[] BlackTerminating =
    [
        "0000110111", "010", "11", "10", "011", "0011", "0010", "00011", "000101", "000100", "0000100", "0000101",
        "0000111", "00000100", "00000111", "000011000", "0000010111", "0000011000", "0000001000", "00001100111",
        "00001101000", "00001101100", "00000110111", "00000101000", "00000010111", "00000011000", "000011001010",
        "000011001011", "000011001100", "000011001101", "000001101000", "000001101001", "000001101010", "000001101011",
        "000011010010", "000011010011", "000011010100", "000011010101", "000011010110", "000011010111", "000001101100",
        "000001101101", "000011011010", "000011011011", "000001010100", "000001010101", "000001010110", "000001010111",
        "000001100100", "000001100101", "000001010010", "000001010011", "000000100100", "000000110111", "000000111000",
        "000000100111", "000000101000", "000001011000", "000001011001", "000000101011", "000000101100", "000001011010",
        "000001100110", "000001100111",
    ];

    static readonly string[] BlackMakeup =
    [
        "0000001111", "000011001000", "000011001001", "000001011011", "000000110011", "000000110100", "000000110101",
        "0000001101100", "0000001101101", "0000001001010", "0000001001011", "0000001001100", "0000001001101",
        "0000001110010", "0000001110011", "0000001110100", "0000001110101", "0000001110110", "0000001110111",
        "0000001010010", "0000001010011", "0000001010100", "0000001010101", "0000001011010", "0000001011011",
        "0000001100100", "0000001100101",
    ];

    /// <summary>Makeup codes of runs from 1792 to 2560 pixels, shared by both colors.</summary>
    static readonly string[] ExtendedMakeup =
    [
        "00000001000", "00000001100", "00000001101", "000000010010", "000000010011", "000000010100", "000000010101",
        "000000010110", "000000010111", "000000011100", "000000011101", "000000011110", "000000011111",
    ];

    const int Peek = 13;
    static readonly Lazy<(int[] White, int[] Black)> tables = new(() => (Table(WhiteTerminating, WhiteMakeup), Table(BlackTerminating, BlackMakeup)));

    /// <summary>Lookup by the next 13 bits: the run length shifted left by 4 and the code length, 0 for no code.</summary>
    static int[] Table(string[] terminating, string[] makeup)
    {
        var table = new int[1 << Peek];
        void Add(string code, int run)
        {
            var value = Convert.ToInt32(code, 2) << Peek - code.Length;
            for (var i = 0; i < 1 << Peek - code.Length; i++)
                table[value | i] = run << 4 | code.Length;
        }
        for (var i = 0; i < terminating.Length; i++)
            Add(terminating[i], i);
        for (var i = 0; i < makeup.Length; i++)
            Add(makeup[i], 64 * (i + 1));
        for (var i = 0; i < ExtendedMakeup.Length; i++)
            Add(ExtendedMakeup[i], 1792 + 64 * i);
        return table;
    }

    /// <summary>
    /// Decodes <paramref name="rows"/> rows of <paramref name="width"/> pixels into rows of (width + 7) / 8 bytes;
    /// <paramref name="options"/> are T4Options (bit 0: two-dimensional rows, bit 2: rows end on bytes) for group 3.
    /// </summary>
    public static byte[] Decode(ReadOnlySpan<byte> data, int width, int rows, int compression, int options)
    {
        var stride = (width + 7) / 8;
        var output = new byte[stride * rows];
        var reader = new Bits(data.ToArray());
        var reference = new List<int>();
        var current = new List<int>();
        for (var row = 0; row < rows && !reader.Done; row++)
        {
            var twoDimensional = compression == 4;
            if (compression == 3)
            {
                reader.SkipEol();
                if ((options & 1) != 0)
                    twoDimensional = reader.Read(1) == 0;
            }
            current.Clear();
            var ok = twoDimensional ? Row2D(reader, width, reference, current) : Row1D(reader, width, current);
            if (!ok)
                break;
            // Black between changes: positions alternate to black and back to white.
            var line = output.AsSpan(row * stride, stride);
            for (var i = 0; i < current.Count; i += 2)
            {
                var (start, end) = (Math.Min(current[i], width), Math.Min(i + 1 < current.Count ? current[i + 1] : width, width));
                for (var x = start; x < end; x++)
                    line[x >> 3] |= (byte)(0x80 >> (x & 7));
            }
            (reference, current) = (current, reference);
            if (compression == 2)
                reader.Align();
        }
        return output;
    }

    /// <summary>A row of one-dimensional runs as its changes (positions where the color flips), without empty runs.</summary>
    static bool Row1D(Bits reader, int width, List<int> changes)
    {
        var (position, white) = (0, true);
        while (position < width)
        {
            var run = Run(reader, white);
            if (run < 0)
                return false;
            position += run;
            Change(changes, Math.Min(position, width));
            white = !white;
        }
        return true;
    }

    /// <summary>A row coded against the previous one (pass, horizontal and vertical modes).</summary>
    static bool Row2D(Bits reader, int width, List<int> reference, List<int> changes)
    {
        var (a0, white) = (-1, true);
        while (a0 < width)
        {
            // b1: the first change of the reference row right of a0 to the color opposite a0's; b2: the change after it.
            var k = 0;
            while (k < reference.Count && (reference[k] <= a0 || k % 2 == 0 != white))
                k++;
            var b1 = k < reference.Count ? reference[k] : width;
            var b2 = k + 1 < reference.Count ? reference[k + 1] : width;
            var bits = reader.Peek(7);
            if (bits >> 6 == 1)
            {
                reader.Skip(1);   // V0
                a0 = Vertical(changes, b1, ref white);
            }
            else if (bits >> 4 == 0b011 || bits >> 4 == 0b010)
            {
                reader.Skip(3);   // VR1, VL1
                a0 = Vertical(changes, b1 + (bits >> 4 == 0b011 ? 1 : -1), ref white);
            }
            else if (bits >> 4 == 0b001)
            {
                reader.Skip(3);   // horizontal: a run of each color
                var first = Run(reader, white);
                var second = first < 0 ? -1 : Run(reader, !white);
                if (second < 0)
                    return false;
                var a1 = Math.Max(a0, 0) + first;
                Change(changes, Math.Min(a1, width));
                Change(changes, Math.Min(a1 + second, width));
                a0 = a1 + second;
            }
            else if (bits >> 3 == 0b0001)
            {
                reader.Skip(4);   // pass: the run continues below b2
                a0 = b2;
            }
            else if (bits >> 1 == 0b000011 || bits >> 1 == 0b000010)
            {
                reader.Skip(6);   // VR2, VL2
                a0 = Vertical(changes, b1 + (bits >> 1 == 0b000011 ? 2 : -2), ref white);
            }
            else if (bits == 0b0000011 || bits == 0b0000010)
            {
                reader.Skip(7);   // VR3, VL3
                a0 = Vertical(changes, b1 + (bits == 0b0000011 ? 3 : -3), ref white);
            }
            else
            {
                return false;   // extensions (uncompressed mode), end of line or end of data
            }
        }
        return true;
    }

    static int Vertical(List<int> changes, int a1, ref bool white)
    {
        Change(changes, Math.Max(0, a1));
        white = !white;
        return Math.Max(0, a1);
    }

    /// <summary>Adds a change, dropping a pair of changes at the same position (an empty run).</summary>
    static void Change(List<int> changes, int position)
    {
        if (changes.Count > 0 && changes[^1] == position)
            changes.RemoveAt(changes.Count - 1);
        else
            changes.Add(position);
    }

    /// <summary>A run of one color: makeup codes followed by a terminating code; -1 on an invalid code.</summary>
    static int Run(Bits reader, bool white)
    {
        var table = white ? tables.Value.White : tables.Value.Black;
        var total = 0;
        while (true)
        {
            var entry = table[reader.Peek(Peek)];
            if (entry == 0)
                return -1;
            reader.Skip(entry & 15);
            var run = entry >> 4;
            total += run;
            if (run < 64)
                return total;
        }
    }

    sealed class Bits(byte[] data)
    {
        long position;
        public bool Done => position >= data.Length * 8L;

        public int Peek(int count)
        {
            var value = 0;
            for (var i = 0; i < count; i++)
            {
                var at = position + i;
                var bit = at < data.Length * 8L ? data[at >> 3] >> 7 - (int)(at & 7) & 1 : 0;
                value = value << 1 | bit;
            }
            return value;
        }

        public void Skip(int count) => position += count;

        public int Read(int count)
        {
            var value = Peek(count);
            position += count;
            return value;
        }

        public void Align() => position = (position + 7) & ~7L;

        /// <summary>Skips an end-of-line code (eleven or more zero bits, then a one bit) when one follows.</summary>
        public void SkipEol()
        {
            var zeros = 0;
            while (position + zeros < data.Length * 8L && Peek1(position + zeros) == 0)
                zeros++;
            if (zeros >= 11 && position + zeros < data.Length * 8L)
                position += zeros + 1;
        }

        int Peek1(long at) => data[at >> 3] >> 7 - (int)(at & 7) & 1;
    }
}
