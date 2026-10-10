namespace FreeSlate.Html;

/// <summary>Reads a JPEG XR codestream most significant bit first (ITU-T T.832 clause 7).</summary>
sealed class JxrBits
{
    readonly byte[] data;
    readonly int end;
    int next;
    ulong buffer;
    int count;

    public JxrBits(byte[] data, int start, int end)
    {
        this.data = data;
        this.end = Math.Min(end, data.Length);
        next = start;
    }

    /// <summary>The byte position of the next unread bit, rounded down.</summary>
    public int Position => next - count / 8;

    public void Seek(int position)
    {
        next = position;
        buffer = 0;
        count = 0;
    }

    void Fill()
    {
        while (count <= 56)
        {
            // Reading far beyond the data means a broken codestream, not a long run of zero bits.
            if (next >= end + 16)
                throw new InvalidDataException("JPEG XR codestream ends early");
            buffer |= (ulong)(next < end ? data[next] : 0) << (56 - count);
            next++;
            count += 8;
        }
    }

    public int Bit()
    {
        if (count == 0)
            Fill();
        var bit = (int)(buffer >> 63);
        buffer <<= 1;
        count--;
        return bit;
    }

    /// <summary>The next <paramref name="n"/> bits (at most 31) as an unsigned number.</summary>
    public int Read(int n)
    {
        if (n == 0)
            return 0;
        if (count < n)
            Fill();
        var value = (int)(buffer >> (64 - n));
        buffer <<= n;
        count -= n;
        return value;
    }

    public uint Read32() => (uint)Read(16) << 16 | (uint)Read(16);

    public void Align()
    {
        var skip = count % 8;
        buffer <<= skip;
        count -= skip;
    }

    /// <summary>VLW_ESC( ) (8.2.4): a 16-bit, 32-bit or 64-bit value after an escape byte; 0 for escape mode.</summary>
    public long VariableLength()
    {
        var first = Read(8);
        return first switch
        {
            < 0xFB => first << 8 | Read(8),
            0xFB => Read32(),
            0xFC => (long)((ulong)Read32() << 32 | Read32()),
            _ => 0,
        };
    }
}

/// <summary>A variable-length code table: symbol n has the code given as a string of bits.</summary>
sealed class JxrVlc
{
    readonly short[] lookup;
    public readonly int[] Lengths;

    public JxrVlc(params string[] codes)
    {
        Lengths = [.. codes.Select(c => c.Length)];
        lookup = new short[2 << Lengths.Max()];
        for (var s = 0; s < codes.Length; s++)
            lookup[1 << codes[s].Length | Convert.ToInt32(codes[s], 2)] = (short)(s + 1);
    }

    public int Read(JxrBits bits)
    {
        var v = 1;
        while (true)
        {
            v = v << 1 | bits.Bit();
            if (v >= lookup.Length)
                throw new InvalidDataException("Invalid JPEG XR variable-length code");
            if (lookup[v] != 0)
                return lookup[v] - 1;
        }
    }
}

/// <summary>
/// A syntax element coded with one of several code tables chosen by the statistics of its past values (8.8): the
/// discriminants grow by the bits a neighbouring table would have saved, which is what the deltaDisc tables 86 to
/// 91 hold.
/// </summary>
sealed class JxrAdaptiveVlc(JxrVlc[] tables)
{
    int table, delta1, delta2, discrim1, discrim2;

    /// <summary>InitializeVLCTable1( ) for two tables, InitializeVLCTable2( ) for more.</summary>
    public void Reset()
    {
        (table, delta1, delta2) = tables.Length == 2 ? (0, 0, 0) : (1, 0, 1);
        discrim1 = discrim2 = 0;
    }

    public int Read(JxrBits bits)
    {
        var symbol = tables[table].Read(bits);
        discrim1 += tables[delta1].Lengths[symbol] - tables[delta1 + 1].Lengths[symbol];
        if (tables.Length > 2)
            discrim2 += tables[delta2].Lengths[symbol] - tables[delta2 + 1].Lengths[symbol];
        return symbol;
    }

    /// <summary>AdaptVLCTable1( ) and AdaptVLCTable2( ) (8.8.4.4, 8.8.4.5).</summary>
    public void Adapt()
    {
        var max = tables.Length - 1;
        if (tables.Length == 2)
        {
            if (discrim1 < -8 && table != 0)
                (table, discrim1) = (table - 1, 0);
            else if (discrim1 > 8 && table != max)
                (table, discrim1) = (table + 1, 0);
            else
                discrim1 = Math.Clamp(discrim1, -64, 64);
            return;
        }
        var change = false;
        if (discrim1 < -8 && table != 0)
            (table, change) = (table - 1, true);
        else if (discrim2 > 8 && table != max)
            (table, change) = (table + 1, true);
        if (change)
        {
            discrim1 = discrim2 = 0;
            (delta1, delta2) = table == max ? (table - 1, table - 1) : table == 0 ? (0, 0) : (table - 1, table);
        }
        else
        {
            discrim1 = Math.Clamp(discrim1, -64, 64);
            discrim2 = Math.Clamp(discrim2, -64, 64);
        }
    }
}

/// <summary>The code tables of clause 8 (tables 51 to 82).</summary>
static class JxrTables
{
    public static readonly JxrVlc ValDcYuv = new("10", "001", "00001", "0001", "11", "010", "00000", "011");
    public static readonly JxrVlc[] AbsLevelIndex =
    [
        new("01", "10", "11", "001", "0001", "00000", "00001"),
        new("1", "01", "001", "0001", "00001", "000000", "000001"),
    ];
    public static readonly JxrVlc CbplpYuv444 = new("0", "100", "1010", "1011", "1100", "1101", "1110", "1111");
    public static readonly JxrVlc CbplpYuv42X = new("0", "10", "110", "111");
    public static readonly JxrVlc[] NumCbphp =
    [
        new("1", "01", "001", "0000", "0001"),
        new("1", "000", "001", "010", "011"),
    ];
    public static readonly JxrVlc[] NumBlkCbphpLuma = NumCbphp;
    public static readonly JxrVlc[] NumBlkCbphp =
    [
        new("010", "00000", "0010", "00001", "00010", "1", "011", "00011", "0011"),
        new("1", "001", "010", "0001", "000001", "011", "00001", "0000000", "0000001"),
    ];
    /// <summary>CHR_CBPHP, VAL_INC and CBPHP_CH_BLK.</summary>
    public static readonly JxrVlc Chroma = new("1", "01", "00");
    public static readonly JxrVlc NumChBlk = new("1", "01", "000", "001");
    public static readonly JxrVlc RefCbphp1 = new("00", "01", "100", "101", "110", "111");
    public static readonly int[] RefCbphp1Values = [3, 5, 6, 9, 10, 12];
    public static readonly JxrVlc RunValue2 = new("1", "0");
    public static readonly JxrVlc RunValue3 = new("1", "01", "00");
    public static readonly JxrVlc RunValue4 = new("1", "01", "001", "000");
    public static readonly JxrVlc RunIndex = new("1", "01", "001", "0000", "0001");
    public static readonly JxrVlc[] IndexA =
    [
        new("1", "00000", "001", "00001", "01", "0001"),
        new("01", "0000", "10", "0001", "11", "001"),
        new("0000", "0001", "01", "10", "11", "001"),
        new("00000", "00001", "01", "1", "0001", "001"),
    ];
    public static readonly JxrVlc IndexB = new("0", "110", "10", "111");
    public static readonly JxrVlc[] FirstIndex =
    [
        new("00001", "000001", "0000000", "0000001", "00100", "010", "00101", "1", "00110", "0001", "00111", "011"),
        new("0010", "00010", "000000", "000001", "0011", "010", "00011", "11", "011", "100", "00001", "101"),
        new("11", "001", "0000000", "0000001", "00001", "010", "0000010", "011", "100", "101", "0000011", "0001"),
        new("001", "11", "0000000", "00001", "00010", "010", "0000001", "011", "00011", "100", "000001", "101"),
        new("010", "1", "0000001", "0001", "0000010", "011", "00000000", "0010", "0000011", "0011", "00000001", "00001"),
    ];

    /// <summary>The initial scan orders (table 107), with 0 for the unused index 0.</summary>
    public static readonly int[] ScanOrder0 = [0, 4, 1, 5, 8, 2, 9, 6, 12, 3, 10, 13, 7, 14, 11, 15];
    public static readonly int[] ScanOrder1 = [0, 1, 2, 5, 4, 3, 6, 9, 8, 7, 12, 15, 13, 10, 11, 14];
    public static readonly int[] ScanTotals = [0, 32, 30, 28, 26, 24, 22, 20, 18, 16, 14, 12, 10, 8, 6, 4];
}

/// <summary>An adaptive inverse scanning order with the counts that reorder it (8.11).</summary>
sealed class JxrScan(int[] initial)
{
    public readonly int[] Order = new int[16];
    readonly int[] totals = new int[16];

    public void Reset()
    {
        initial.CopyTo(Order, 0);
        ResetTotals();
    }

    public void ResetTotals() => JxrTables.ScanTotals.CopyTo(totals, 0);

    /// <summary>The raster position of the i-th parsed coefficient; counting it may move it forward in the order.</summary>
    public int Next(int i)
    {
        var k = Order[i];
        totals[i]++;
        if (i > 1 && totals[i] > totals[i - 1])
        {
            (totals[i], totals[i - 1]) = (totals[i - 1], totals[i]);
            (Order[i], Order[i - 1]) = (Order[i - 1], Order[i]);
        }
        return k;
    }
}

/// <summary>Adaptive coefficient normalization (8.12): the bits of a band coded as fixed-length refinement.</summary>
sealed class JxrModel
{
    public readonly int[] Bits = new int[2];
    readonly int[] state = new int[2];

    static readonly int[] Weight0 = [240, 12, 1];
    static readonly int[][] Weight1 =
    [
        [0, 240, 120, 80, 60, 48, 40, 34, 30, 27, 24, 22, 20, 18, 17, 16],
        [0, 12, 6, 4, 3, 2, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1],
        [0, 16, 8, 5, 4, 3, 3, 2, 2, 2, 2, 1, 1, 1, 1, 1],
    ];
    static readonly int[] Weight2 = [120, 37, 2, 120, 18, 1];

    public void Reset(int band)
    {
        state[0] = state[1] = 0;
        Bits[0] = Bits[1] = (2 - band) * 4;
    }

    /// <summary>UpdateModelMB( ) with the counts of non-zero coefficients of luma and chroma.</summary>
    public void Update(int lap0, int lap1, int band, int format, int components)
    {
        Span<int> lap = [lap0 * Weight0[band], lap1];
        if (format == JxrFormat.Yuv420)
            lap[1] *= Weight2[band];
        else if (format == JxrFormat.Yuv422)
            lap[1] *= Weight2[3 + band];
        else
        {
            lap[1] *= Weight1[band][Math.Min(components, 16) - 1];
            if (band == 2)
                lap[1] >>= 4;
        }
        var models = format == JxrFormat.YOnly ? 1 : 2;
        for (var j = 0; j < models; j++)
        {
            var ms = state[j];
            var delta = (lap[j] - 70) >> 2;
            if (delta <= -8)
            {
                delta = Math.Max(delta + 4, -16);
                ms += delta;
                if (ms < -8)
                {
                    if (Bits[j] == 0)
                        ms = -8;
                    else
                    {
                        ms = 0;
                        Bits[j]--;
                    }
                }
            }
            else if (delta >= 8)
            {
                delta = Math.Min(delta - 4, 15);
                ms += delta;
                if (ms > 8)
                {
                    if (Bits[j] >= 15)
                    {
                        Bits[j] = 15;
                        ms = 8;
                    }
                    else
                    {
                        ms = 0;
                        Bits[j]++;
                    }
                }
            }
            state[j] = ms;
        }
    }
}

/// <summary>The colour formats and bit depths of the image header and image plane header (tables 22, 23, 28).</summary>
static class JxrFormat
{
    public const int YOnly = 0, Yuv420 = 1, Yuv422 = 2, Yuv444 = 3, Yuvk = 4, NComponent = 6;
    public const int OutYOnly = 0, OutYuv420 = 1, OutYuv422 = 2, OutYuv444 = 3, OutCmyk = 4, OutCmykDirect = 5,
        OutNComponent = 6, OutRgb = 7, OutRgbe = 8;
    public const int Bd1White = 0, Bd8 = 1, Bd16 = 2, Bd16S = 3, Bd16F = 4, Bd32S = 6, Bd32F = 7, Bd5 = 8, Bd10 = 9,
        Bd565 = 10, Bd1Black = 15;
    public const int BandsAll = 0, BandsNoFlexbits = 1, BandsNoHighpass = 2, BandsDcOnly = 3;
}
