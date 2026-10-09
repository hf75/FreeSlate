namespace FreeSlate.Fonts;

/// <summary>
/// MicroType Express ([MTX], W3C Member Submission of 5 March 2008): a TrueType font in Compact Table Format whose
/// three blocks (the font's tables, the glyphs' initial push data and their instructions, see
/// <see cref="CompactTableFormat"/>) are each compressed with LZCOMP. A 10-byte header gives the version (3), the
/// compressor's copy limit and where the second and third block start.
/// </summary>
public static class MicroTypeExpress
{
    /// <summary>The TrueType font the MicroType Express data holds.</summary>
    /// <exception cref="InvalidDataException">The data is damaged.</exception>
    public static byte[] Decompress(byte[] data)
    {
        if (data.Length < 10 || data[0] != 3)
            throw new InvalidDataException("Not a MicroType Express font (version 3 expected).");
        var second = UInt24(data, 4);
        var third = UInt24(data, 7);
        if (second < 10 || third < second || third > data.Length)
            throw new InvalidDataException("MicroType Express blocks out of order.");
        return CompactTableFormat.ToTrueType(
            Lzcomp.Unpack(data.AsSpan(10, second - 10)),
            Lzcomp.Unpack(data.AsSpan(second, third - second)),
            Lzcomp.Unpack(data.AsSpan(third)));
    }

    static int UInt24(byte[] data, int at) => data[at] << 16 | data[at + 1] << 8 | data[at + 2];
}

/// <summary>
/// LZCOMP ([MTX] chapter 3 and appendix B), a variant of LZ77. The data is a run-length flag (one bit), its unpacked
/// length (24 bits) and a sequence of items, each coded with an adaptive Huffman tree: a literal byte, a byte copied
/// from 2, 4 or 6 bytes back, or a copy of an earlier string. A copy's symbol says how many 3-bit pieces its distance
/// takes and holds the first 2 bits of its length with a flag whether 2 more follow (from a second tree); the
/// distance pieces come from a third tree, and the distance counts to the end of the copied string. Copies reach into
/// a history that starts with fixed data. Run-length coded data still has its runs to expand.
/// </summary>
static class Lzcomp
{
    /// <summary>The history before the data: every pair (0 … 31, 0 … 95), then every byte value four times.</summary>
    static readonly byte[] Preload = MakePreload();

    static byte[] MakePreload()
    {
        var preload = new byte[2 * 32 * 96 + 4 * 256];
        var i = 0;
        for (var high = 0; high < 32; high++)
        {
            for (var low = 0; low < 96; low++)
            {
                preload[i++] = (byte)high;
                preload[i++] = (byte)low;
            }
        }
        for (var value = 0; value < 256; value++)
        {
            for (var k = 0; k < 4; k++)
                preload[i++] = (byte)value;
        }
        return preload;
    }

    public static byte[] Unpack(ReadOnlySpan<byte> packed)
    {
        if (packed.IsEmpty)
            return [];
        var bits = new BitReader(packed.ToArray());
        var runLength = bits.Read() != 0;
        var distances = new AdaptiveHuffman(8);
        var lengths = new AdaptiveHuffman(8);
        var size = bits.Read(24);
        // A copy names up to as many distance pieces as the longest distance in the data needs; the three single byte
        // copies follow the copy symbols.
        var pieces = 1;
        while (1L << 3 * pieces < size)
            pieces++;
        var backTwo = 256 + 8 * pieces;
        var symbols = new AdaptiveHuffman(backTwo + 3);

        var history = new byte[Preload.Length + size];
        Preload.CopyTo(history, 0);
        var at = Preload.Length;
        while (at < history.Length)
        {
            var symbol = symbols.Read(bits);
            if (symbol < 256)
            {
                history[at++] = (byte)symbol;
                continue;
            }
            if (symbol >= backTwo)
            {
                history[at] = history[at - 2 * (symbol - backTwo + 1)];
                at++;
                continue;
            }
            var code = symbol - 256;
            var length = 0;
            for (var part = code & 7; ; part = lengths.Read(bits))
            {
                length = length << 2 | part & 3;
                if ((part & 4) == 0)
                    break;
            }
            length += 2;
            var distance = 0;
            for (var i = code >> 3; i >= 0; i--)
                distance = distance << 3 | distances.Read(bits);
            distance++;
            // From 512 bytes back on, copies are at least 3 bytes long, and their lengths count from 3.
            if (distance >= 512)
                length++;
            var from = at - distance - length + 1;
            if (from < 0 || length > history.Length - at)
                throw new InvalidDataException("LZCOMP copy outside the data.");
            for (var i = 0; i < length; i++)
                history[at++] = history[from + i];
        }
        var unpacked = history.AsSpan(Preload.Length);
        return runLength ? ExpandRuns(unpacked) : unpacked.ToArray();
    }

    /// <summary>
    /// Expands runs: the first byte is the escape; escape, n, b stands for n times b and escape, 0 for the escape byte
    /// itself.
    /// </summary>
    static byte[] ExpandRuns(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return [];
        var escape = data[0];
        var output = new List<byte>(data.Length * 2);
        for (var i = 1; i < data.Length; i++)
        {
            if (data[i] != escape)
            {
                output.Add(data[i]);
                continue;
            }
            if (i + 1 >= data.Length)
                throw new InvalidDataException("Run-length data ends after an escape.");
            var count = data[++i];
            if (count == 0)
            {
                output.Add(escape);
                continue;
            }
            if (i + 1 >= data.Length)
                throw new InvalidDataException("Run-length data ends inside a run.");
            var value = data[++i];
            for (var k = 0; k < count; k++)
                output.Add(value);
        }
        return output.ToArray();
    }
}

/// <summary>
/// The adaptive Huffman code LZCOMP's items use: a tree over the symbols 0 … n−1 kept in slots 1 … 2n−1 by
/// decreasing weight (the sibling property of Faller, Gallager and Knuth), the root in slot 1 and every node's
/// children in two neighbouring slots, a 1 bit leading to the second. Counting a symbol adds one to its leaf and every
/// node above it, each node first trading places with the first node of its own weight. The tree starts complete, the
/// symbols in order in slots n … 2n−1, each counted once; then trees of up to 256 symbols count every symbol twice
/// more, and the item tree (between 256 and 512 symbols) the two shortest one-piece copies once each, the single byte
/// copy from 2 bytes back 12 times and the one from 4 bytes back 6 times. Weights are never rescaled.
/// </summary>
sealed class AdaptiveHuffman
{
    readonly int[] weight;
    readonly int[] parent;
    readonly int[] firstChild;   // -1 for leaves
    readonly int[] symbolAt;
    readonly int[] slotOf;

    public AdaptiveHuffman(int count)
    {
        var slots = 2 * count;
        weight = new int[slots];
        parent = new int[slots];
        firstChild = new int[slots];
        symbolAt = new int[slots];
        slotOf = new int[count];
        for (var slot = 2; slot < slots; slot++)
            parent[slot] = slot / 2;
        for (var slot = 1; slot < count; slot++)
            firstChild[slot] = 2 * slot;
        for (var symbol = 0; symbol < count; symbol++)
        {
            var leaf = count + symbol;
            firstChild[leaf] = -1;
            symbolAt[leaf] = symbol;
            slotOf[symbol] = leaf;
            weight[leaf] = 1;
        }
        for (var slot = count - 1; slot >= 1; slot--)
            weight[slot] = weight[2 * slot] + weight[2 * slot + 1];

        if (count is > 256 and < 512)
        {
            Count(256);
            Count(257);
            for (var i = 0; i < 12; i++)
                Count(count - 3);
            for (var i = 0; i < 6; i++)
                Count(count - 2);
        }
        else
        {
            for (var round = 0; round < 2; round++)
            {
                for (var symbol = 0; symbol < count; symbol++)
                    Count(symbol);
            }
        }
    }

    /// <summary>Reads one symbol and counts it.</summary>
    public int Read(BitReader bits)
    {
        var slot = 1;
        while (firstChild[slot] >= 0)
            slot = firstChild[slot] + bits.Read();
        var symbol = symbolAt[slot];
        Count(symbol);
        return symbol;
    }

    void Count(int symbol)
    {
        var slot = slotOf[symbol];
        while (slot != 1)
        {
            var w = weight[slot];
            var first = slot;
            while (weight[first - 1] == w)
                first--;
            if (first != slot)
            {
                Trade(slot, first);
                slot = first;
            }
            weight[slot] = w + 1;
            slot = parent[slot];
        }
        weight[1]++;
    }

    /// <summary>Two nodes of equal weight trade slots, their subtrees with them; the slots keep their parents.</summary>
    void Trade(int a, int b)
    {
        (weight[a], weight[b]) = (weight[b], weight[a]);
        (firstChild[a], firstChild[b]) = (firstChild[b], firstChild[a]);
        (symbolAt[a], symbolAt[b]) = (symbolAt[b], symbolAt[a]);
        Settle(a);
        Settle(b);
    }

    void Settle(int slot)
    {
        if (firstChild[slot] >= 0)
            parent[firstChild[slot]] = parent[firstChild[slot] + 1] = slot;
        else
            slotOf[symbolAt[slot]] = slot;
    }
}

/// <summary>Bits from the most significant bit of each byte on.</summary>
sealed class BitReader(byte[] data)
{
    long position;

    public int Read()
    {
        var index = position >> 3;
        if (index >= data.Length)
            throw new InvalidDataException("LZCOMP data ends early.");
        var bit = data[index] >> 7 - (int)(position & 7) & 1;
        position++;
        return bit;
    }

    public int Read(int count)
    {
        var value = 0;
        for (var i = 0; i < count; i++)
            value = value << 1 | Read();
        return value;
    }
}
