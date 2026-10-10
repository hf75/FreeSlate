using static FreeSlate.Html.JxrFormat;

namespace FreeSlate.Html;

/// <summary>A macroblock and its place in its tile.</summary>
readonly record struct JxrMb(int X, int Y, int Index, int Tile, bool Left, bool Top, bool SegmentStart, bool Adapt)
{
    public bool TileStart => Left && Top;
}

/// <summary>
/// A JPEG XR codestream (ITU-T T.832 clauses 8 and 9): the image header, the primary image plane and an optional
/// interleaved alpha image plane, tiles in spatial or frequency order. Decoding leaves the sample planes of each
/// image plane in its internal colour format; <see cref="JxrOutput"/> converts them.
/// </summary>
sealed class JxrCodestream
{
    public bool HardTiling, Frequency, TrimFlexbitsFlag, RedBlueNotSwapped, Premultiplied, AlphaPlane;
    public int Overlap, SpatialTransform, OutputFormat, BitDepth, Width, Height;
    public int TopMargin, LeftMargin, BottomMargin, RightMargin;
    public int MbWidth, MbHeight, TilesX, TilesY;
    public int[] TileLeft = [], TileTop = [];
    public JxrPlane Primary = null!;
    public JxrPlane? Alpha;
    long[]? indexOffsets;

    public int ExtendedWidth => MbWidth * 16;
    public int ExtendedHeight => MbHeight * 16;

    /// <summary>Parses and decodes the codestream between two byte positions.</summary>
    public static JxrCodestream Decode(byte[] data, int start, int end)
    {
        var image = new JxrCodestream();
        var bits = new JxrBits(data, start, end);
        image.ReadHeader(bits);
        var tilesStart = bits.Position;
        image.DecodeTiles(new JxrBits(data, tilesStart, end), tilesStart);
        image.Primary.Reconstruct();
        image.Alpha?.Reconstruct();
        return image;
    }

    void ReadHeader(JxrBits bits)
    {
        if (bits.Read32() != 0x574D5048 || bits.Read32() != 0x4F544F00)
            throw new InvalidDataException("No JPEG XR codestream");
        bits.Read(4);
        HardTiling = bits.Bit() != 0;
        bits.Read(3);
        var tiling = bits.Bit() != 0;
        Frequency = bits.Bit() != 0;
        SpatialTransform = bits.Read(3);
        var indexTable = bits.Bit() != 0;
        Overlap = bits.Read(2);
        var shortHeader = bits.Bit() != 0;
        bits.Bit();
        var windowing = bits.Bit() != 0;
        TrimFlexbitsFlag = bits.Bit() != 0;
        bits.Bit();
        RedBlueNotSwapped = bits.Bit() != 0;
        Premultiplied = bits.Bit() != 0;
        AlphaPlane = bits.Bit() != 0;
        OutputFormat = bits.Read(4);
        BitDepth = bits.Read(4);
        var (w, h) = shortHeader ? (bits.Read(16) + 1L, bits.Read(16) + 1L) : (bits.Read32() + 1L, bits.Read32() + 1L);
        if (w > 1 << 16 || h > 1 << 16 || w * h > 1 << 28)
            throw new InvalidDataException("JPEG XR image too large");
        (Width, Height) = ((int)w, (int)h);
        var (tilesX, tilesY) = tiling ? (bits.Read(12) + 1, bits.Read(12) + 1) : (1, 1);
        var tileWidths = new int[tilesX - 1];
        for (var n = 0; n < tileWidths.Length; n++)
            tileWidths[n] = bits.Read(shortHeader ? 8 : 16);
        var tileHeights = new int[tilesY - 1];
        for (var n = 0; n < tileHeights.Length; n++)
            tileHeights[n] = bits.Read(shortHeader ? 8 : 16);
        if (windowing)
            (TopMargin, LeftMargin, BottomMargin, RightMargin) = (bits.Read(6), bits.Read(6), bits.Read(6), bits.Read(6));
        else
            (BottomMargin, RightMargin) = ((16 - Height % 16) % 16, (16 - Width % 16) % 16);
        var (extendedWidth, extendedHeight) = (Width + LeftMargin + RightMargin, Height + TopMargin + BottomMargin);
        if (extendedWidth % 16 != 0 || extendedHeight % 16 != 0)
            throw new InvalidDataException("JPEG XR image size is no multiple of 16");
        (MbWidth, MbHeight, TilesX, TilesY) = (extendedWidth / 16, extendedHeight / 16, tilesX, tilesY);
        TileLeft = Boundaries(tileWidths, MbWidth);
        TileTop = Boundaries(tileHeights, MbHeight);

        Primary = new JxrPlane(this, bits, alpha: false);
        if (AlphaPlane)
            Alpha = new JxrPlane(this, bits, alpha: true);
        if (indexTable)
        {
            var entries = TilesX * TilesY * (Frequency ? Primary.BandCount : 1);
            if (bits.Read(16) != 1)
                throw new InvalidDataException("Invalid JPEG XR index table");
            indexOffsets = new long[entries];
            for (var n = 0; n < entries; n++)
                indexOffsets[n] = bits.VariableLength();
        }
        var subsequent = bits.VariableLength();
        bits.Align();
        bits.Seek(bits.Position + (int)Math.Min(subsequent, int.MaxValue / 2));
    }

    static int[] Boundaries(int[] sizes, int total)
    {
        var bounds = new int[sizes.Length + 2];
        for (var n = 0; n < sizes.Length; n++)
            bounds[n + 1] = bounds[n] + sizes[n];
        bounds[^1] = total;
        for (var n = 0; n + 1 < bounds.Length; n++)
            if (bounds[n + 1] <= bounds[n])
                throw new InvalidDataException("Invalid JPEG XR tile layout");
        return bounds;
    }

    /// <summary>The macroblocks of a tile in raster order.</summary>
    IEnumerable<JxrMb> Macroblocks(int tile)
    {
        var (tx, ty) = (tile % TilesX, tile / TilesX);
        for (var y = TileTop[ty]; y < TileTop[ty + 1]; y++)
            for (var x = TileLeft[tx]; x < TileLeft[tx + 1]; x++)
            {
                var segmentStart = (x - TileLeft[tx]) % 16 == 0;
                yield return new JxrMb(x, y, y * MbWidth + x, tile, x == TileLeft[tx], y == TileTop[ty], segmentStart,
                    segmentStart || x == TileLeft[tx + 1] - 1);
            }
    }

    IEnumerable<JxrPlane> Planes => Alpha != null ? [Primary, Alpha] : [Primary];

    void DecodeTiles(JxrBits bits, int tilesStart)
    {
        foreach (var plane in Planes)
            plane.Attach(bits);
        var tiles = TilesX * TilesY;
        if (!Frequency)
        {
            for (var n = 0; n < tiles; n++)
            {
                Packet(bits, tilesStart, n);
                var trim = TrimFlexbitsFlag ? bits.Read(4) : 0;
                foreach (var plane in Planes)
                {
                    plane.BeginTile(n);
                    plane.TileHeaderDc(n);
                    if (plane.Bands != BandsDcOnly)
                    {
                        plane.TileHeaderLp(n);
                        if (plane.Bands != BandsNoHighpass)
                            plane.TileHeaderHp(n);
                    }
                }
                foreach (var mb in Macroblocks(n))
                    foreach (var plane in Planes)
                        plane.SpatialMacroblock(mb, trim);
                bits.Align();
            }
            return;
        }
        var bands = Primary.BandCount;
        for (var band = 0; band < bands; band++)
        {
            for (var n = 0; n < tiles; n++)
            {
                Packet(bits, tilesStart, indexOffsets != null ? n * bands + band : -1);
                var trim = band == 3 && TrimFlexbitsFlag ? bits.Read(4) : 0;
                foreach (var plane in Planes)
                    switch (band)
                    {
                        case 0:
                            plane.BeginTile(n);
                            plane.TileHeaderDc(n);
                            break;
                        case 1 when plane.Bands != BandsDcOnly:
                            plane.TileHeaderLp(n);
                            break;
                        case 2 when plane.Bands is not (BandsDcOnly or BandsNoHighpass):
                            plane.TileHeaderHp(n);
                            break;
                    }
                foreach (var mb in Macroblocks(n))
                    foreach (var plane in Planes)
                        plane.FrequencyMacroblock(mb, band, trim);
                bits.Align();
            }
            // The scan order of highpass coefficients follows from the decoded lowpass ones.
            if (band == Math.Min(bands, 2) - 1)
                foreach (var plane in Planes)
                    for (var n = 0; n < tiles; n++)
                        foreach (var mb in Macroblocks(n))
                            plane.DecodeDcLp(mb);
        }
        foreach (var plane in Planes)
            if (plane.HasHighpass)
                for (var n = 0; n < tiles; n++)
                    foreach (var mb in Macroblocks(n))
                        plane.DecodeHp(mb);
    }

    /// <summary>Moves to a tile packet by the index table (n &lt; 0: the next packet) and checks its start code.</summary>
    void Packet(JxrBits bits, int tilesStart, int n)
    {
        if (indexOffsets != null && n >= 0)
            bits.Seek(tilesStart + (int)Math.Min(indexOffsets[n], int.MaxValue / 2));
        if (bits.Read(24) != 1)
            throw new InvalidDataException("Invalid JPEG XR tile start code");
        bits.Read(8);
    }
}

/// <summary>The quantization parameter sets of a tile: per set the parameter of each component.</summary>
sealed class JxrQp
{
    public int[] Dc = [];
    public int[][] Lp = [], Hp = [];
    public bool UseDcQp, UseLpQp;

    public JxrQp Copy() => (JxrQp)MemberwiseClone();
}

/// <summary>
/// An image plane: its header, the adaptive parsing state of each band, the transform coefficients of every
/// macroblock and, once reconstructed, its samples.
/// </summary>
sealed class JxrPlane
{
    readonly JxrCodestream image;
    JxrBits bits;
    public readonly int Format, Components, Bands, CenterX, CenterY, ShiftBits, LenMantissa, ExpBias;
    public readonly bool Scaled;
    readonly bool dcUniform, lpUniform, hpUniform;
    readonly JxrQp imageQp = new();
    JxrQp[] tileQp = [];

    /// <summary>Per component the samples (highpass coefficients until reconstruction), rows of SampleWidth.</summary>
    public readonly int[][] Samples;
    public readonly int[] SampleWidth, SampleHeight;
    readonly int[][] dclp, pred;
    readonly byte[] lpIndex, hpIndex, hpMode, modelBitsHp;
    readonly int[] cbphp;

    readonly JxrAdaptiveVlc dcLum = new(JxrTables.AbsLevelIndex), dcChr = new(JxrTables.AbsLevelIndex);
    readonly JxrModel modelDc = new(), modelLp = new(), modelHp = new();
    readonly JxrBandVlc lp = new(), hp = new();
    readonly JxrScan lpScan = new(JxrTables.ScanOrder0), hpHor = new(JxrTables.ScanOrder0), hpVer = new(JxrTables.ScanOrder1);
    readonly JxrAdaptiveVlc numCbphp = new(JxrTables.NumCbphp), numBlkCbphp;
    int countZeroCbplp, countMaxCbplp;
    readonly int[] cbphpState = new int[2], countOnes = new int[2], countZeroes = new int[2];

    static readonly int[] Transpose444 = [0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15];
    static readonly int[] Transpose422 = [0, 2, 1, 3, 4, 6, 5, 7];
    static readonly int[] Transpose420 = [0, 2, 1, 3];
    static readonly int[] RemapChroma = [4, 1, 2, 3, 5, 6, 7];
    static readonly int[] HierScanOrder = [0, 1, 4, 5, 2, 3, 6, 7, 8, 9, 12, 13, 10, 11, 14, 15];
    static readonly int[] BitsQpIndex = [0, 0, 1, 1, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 4];
    static readonly int[] AbsRemap = [2, 3, 4, 6, 10, 14], AbsFixedLength = [0, 0, 1, 2, 2, 2];
    static readonly int[] RunRemap = [1, 2, 3, 5, 7, 1, 2, 3, 5, 7, 1, 2, 3, 4, 5];
    static readonly int[] RunBin = [-1, -1, -1, -1, 2, 2, 2, 1, 1, 1, 1, 0, 0, 0, 0];
    static readonly int[] RunFixedLength = [0, 0, 1, 1, 3, 0, 0, 1, 1, 2, 0, 0, 0, 0, 1];
    static readonly int[] CbphpFlc = [0, 2, 1, 2, 2, 0], CbphpOff = [0, 4, 2, 8, 12, 1];
    static readonly int[] CbphpOut = [0, 15, 3, 12, 1, 2, 4, 8, 5, 6, 9, 10, 7, 11, 13, 14];
    static readonly int[] ChromaShift422 = [0, 1, 4, 5];

    /// <summary>Reads the IMAGE_PLANE_HEADER( ) (8.4).</summary>
    public JxrPlane(JxrCodestream image, JxrBits bits, bool alpha)
    {
        this.image = image;
        this.bits = bits;
        Format = bits.Read(3);
        Scaled = bits.Bit() != 0;
        Bands = bits.Read(4);
        if (Bands > BandsDcOnly || Format is 5 or 7)
            throw new InvalidDataException("Unknown JPEG XR image plane format");
        Components = Format switch { YOnly => 1, Yuvk => 4, NComponent => 0, _ => 3 };
        if (Format is Yuv420 or Yuv422 or Yuv444)
        {
            if (Format == Yuv444)
                bits.Read(4);
            else
            {
                bits.Bit();
                CenterX = bits.Read(3);
            }
            if (Format == Yuv420)
            {
                bits.Bit();
                CenterY = bits.Read(3);
            }
            else
                bits.Read(4);
        }
        else if (Format == NComponent)
        {
            var n = bits.Read(4);
            if (n == 15)
                Components = bits.Read(12) + 16;
            else
            {
                Components = n + 1;
                bits.Read(4);
            }
        }
        if (alpha && Components != 1)
            throw new InvalidDataException("JPEG XR alpha plane with several components");
        if (image.BitDepth is Bd16 or Bd16S or Bd32S)
            ShiftBits = bits.Read(8);
        if (image.BitDepth == Bd32F)
        {
            LenMantissa = bits.Read(8);
            ExpBias = (sbyte)bits.Read(8);
        }
        dcUniform = bits.Bit() != 0;
        if (dcUniform)
            imageQp.Dc = ReadQp();
        if (Bands != BandsDcOnly)
        {
            bits.Bit();
            lpUniform = bits.Bit() != 0;
            if (lpUniform)
                imageQp.Lp = [ReadQp()];
            if (Bands != BandsNoHighpass)
            {
                bits.Bit();
                hpUniform = bits.Bit() != 0;
                if (hpUniform)
                    imageQp.Hp = [ReadQp()];
            }
        }
        bits.Align();

        numBlkCbphp = new(Format is YOnly or Yuvk or NComponent ? JxrTables.NumBlkCbphpLuma : JxrTables.NumBlkCbphp);
        var mbs = image.MbWidth * image.MbHeight;
        Samples = new int[Components][];
        SampleWidth = new int[Components];
        SampleHeight = new int[Components];
        dclp = new int[Components][];
        pred = new int[Components][];
        for (var c = 0; c < Components; c++)
        {
            SampleWidth[c] = image.ExtendedWidth / (c > 0 && Format is Yuv420 or Yuv422 ? 2 : 1);
            SampleHeight[c] = image.ExtendedHeight / (c > 0 && Format == Yuv420 ? 2 : 1);
            Samples[c] = new int[SampleWidth[c] * SampleHeight[c]];
            dclp[c] = new int[mbs * 16];
            pred[c] = new int[mbs * 7];
        }
        lpIndex = new byte[mbs];
        hpIndex = new byte[mbs];
        hpMode = new byte[mbs];
        modelBitsHp = new byte[mbs * 2];
        cbphp = new int[mbs * Components];
    }

    /// <summary>The number of bands present: 4 with flexbits, down to 1 for DC only.</summary>
    public int BandCount => 4 - Bands;
    public bool HasHighpass => Bands is BandsAll or BandsNoFlexbits;
    bool Subsampled => Format is Yuv420 or Yuv422;

    public void Attach(JxrBits reader) => bits = reader;

    int[] ReadQp()
    {
        var mode = Components != 1 ? bits.Read(2) : 0;
        var qp = new int[Components];
        switch (mode)
        {
            case 0:
                Array.Fill(qp, bits.Read(8));
                break;
            case 1:
                qp[0] = bits.Read(8);
                Array.Fill(qp, bits.Read(8), 1, Components - 1);
                break;
            case 2:
                for (var i = 0; i < Components; i++)
                    qp[i] = bits.Read(8);
                break;
            default:
                throw new InvalidDataException("Invalid JPEG XR component mode");
        }
        return qp;
    }

    int[][] ReadQpSets(int count)
    {
        var sets = new int[count][];
        for (var q = 0; q < count; q++)
            sets[q] = ReadQp();
        return sets;
    }

    public void BeginTile(int tile)
    {
        if (tileQp.Length == 0)
            tileQp = new JxrQp[image.TilesX * image.TilesY];
        tileQp[tile] = imageQp.Copy();
    }

    public void TileHeaderDc(int tile)
    {
        if (!dcUniform)
            tileQp[tile].Dc = ReadQp();
    }

    public void TileHeaderLp(int tile)
    {
        var qp = tileQp[tile];
        if (lpUniform)
            return;
        qp.UseDcQp = bits.Bit() != 0;
        qp.Lp = qp.UseDcQp ? [qp.Dc] : ReadQpSets(bits.Read(4) + 1);
    }

    public void TileHeaderHp(int tile)
    {
        var qp = tileQp[tile];
        if (hpUniform)
            return;
        qp.UseLpQp = bits.Bit() != 0;
        qp.Hp = qp.UseLpQp ? qp.Lp : ReadQpSets(bits.Read(4) + 1);
    }

    int QpIndex(int count)
    {
        if (bits.Bit() == 0)
            return 0;
        var index = bits.Read(BitsQpIndex[Math.Min(count, 16)]) + 1;
        return index < count ? index : throw new InvalidDataException("Invalid JPEG XR quantizer index");
    }

    void ReadLpIndex(JxrMb mb)
    {
        var qp = tileQp[mb.Tile];
        lpIndex[mb.Index] = (byte)(qp.Lp.Length > 1 && !qp.UseDcQp ? QpIndex(qp.Lp.Length) : 0);
    }

    void ReadHpIndex(JxrMb mb)
    {
        var qp = tileQp[mb.Tile];
        hpIndex[mb.Index] = (byte)(qp.Hp.Length > 1 && !qp.UseLpQp ? QpIndex(qp.Hp.Length) : qp.UseLpQp ? lpIndex[mb.Index] : 0);
    }

    /// <summary>A macroblock of a TILE_SPATIAL( ) packet: every band, decoded right away.</summary>
    public void SpatialMacroblock(JxrMb mb, int trim)
    {
        if (Bands != BandsDcOnly)
        {
            ReadLpIndex(mb);
            if (HasHighpass)
                ReadHpIndex(mb);
        }
        ParseDc(mb);
        if (Bands != BandsDcOnly)
            ParseLp(mb);
        DecodeDcLp(mb);
        if (!HasHighpass)
            return;
        ParseCbphp(mb);
        ParseHp(mb, Bands == BandsAll, trim);
        DecodeHp(mb);
    }

    /// <summary>A macroblock of one band packet of a frequency-ordered codestream.</summary>
    public void FrequencyMacroblock(JxrMb mb, int band, int trim)
    {
        switch (band)
        {
            case 0:
                ParseDc(mb);
                break;
            case 1 when Bands != BandsDcOnly:
                ReadLpIndex(mb);
                ParseLp(mb);
                break;
            case 2 when HasHighpass:
                ReadHpIndex(mb);
                ParseCbphp(mb);
                ParseHp(mb, false, 0);
                break;
            case 3 when Bands == BandsAll:
                ParseFlexbits(mb, trim);
                break;
        }
    }

    int AbsLevel(JxrAdaptiveVlc vlc)
    {
        var index = vlc.Read(bits);
        if (index < 6)
            return AbsRemap[index] + bits.Read(AbsFixedLength[index]);
        var length = bits.Read(4) + 4;
        if (length == 19)
        {
            length += bits.Read(2);
            if (length == 22)
                length += bits.Read(3);
        }
        return 2 + (1 << length) + bits.Read(length);
    }

    int DecodeDc(int modelBits, JxrAdaptiveVlc vlc, bool absLevel)
    {
        var dc = absLevel ? AbsLevel(vlc) - 1 : 0;
        if (modelBits > 0)
            dc = dc << modelBits | bits.Read(modelBits);
        return dc != 0 && bits.Bit() != 0 ? -dc : dc;
    }

    /// <summary>MB_DC( ) (8.7.11).</summary>
    void ParseDc(JxrMb mb)
    {
        if (mb.TileStart)
        {
            dcLum.Reset();
            dcChr.Reset();
            modelDc.Reset(0);
        }
        int lap0 = 0, lap1 = 0, at = mb.Index * 16;
        if (Format is YOnly or Yuvk or NComponent)
            for (var n = 0; n < Components; n++)
            {
                var abs = bits.Bit() != 0;
                if (abs && n == 0)
                    lap0++;
                else if (abs)
                    lap1++;
                dclp[n][at] = DecodeDc(modelDc.Bits[n == 0 ? 0 : 1], dcLum, abs);
            }
        else
        {
            var nonZero = JxrTables.ValDcYuv.Read(bits);
            lap0 += nonZero >> 2 & 1;
            lap1 += (nonZero >> 1 & 1) + (nonZero & 1);
            dclp[0][at] = DecodeDc(modelDc.Bits[0], dcLum, (nonZero & 4) != 0);
            dclp[1][at] = DecodeDc(modelDc.Bits[1], dcChr, (nonZero & 2) != 0);
            dclp[2][at] = DecodeDc(modelDc.Bits[1], dcChr, (nonZero & 1) != 0);
        }
        modelDc.Update(lap0, lap1, 0, Format, Components);
        if (mb.Adapt)
        {
            dcLum.Adapt();
            dcChr.Adapt();
        }
    }

    /// <summary>MB_LP( ) (8.7.16).</summary>
    void ParseLp(JxrMb mb)
    {
        if (mb.TileStart)
        {
            countZeroCbplp = countMaxCbplp = 1;
            lp.Reset();
            lpScan.Reset();
            modelLp.Reset(1);
        }
        if (mb.SegmentStart)
            lpScan.ResetTotals();
        var fullPlanes = Subsampled ? 2 : Components;
        int cbp;
        if (Format is Yuv420 or Yuv422 or Yuv444)
        {
            var max = fullPlanes * 4 - 5;
            if (countZeroCbplp <= 0 || countMaxCbplp < 0)
            {
                var v = (Format == Yuv444 ? JxrTables.CbplpYuv444 : JxrTables.CbplpYuv42X).Read(bits);
                cbp = countMaxCbplp < countZeroCbplp ? max - v : v;
            }
            else
                cbp = bits.Read(fullPlanes);
            countZeroCbplp = Math.Clamp(countZeroCbplp + 1 - (cbp == 0 ? 4 : 0), -8, 7);
            countMaxCbplp = Math.Clamp(countMaxCbplp + 1 - (cbp == max ? 4 : 0), -8, 7);
        }
        else
        {
            cbp = 0;
            for (var n = 0; n < Components; n++)
                cbp |= bits.Bit() << n;
        }
        var at = mb.Index * 16;
        for (var n = 0; n < Components; n++)
            Array.Clear(dclp[n], at + 1, 15);
        Span<int> runs = stackalloc int[16], levels = stackalloc int[16], joint = stackalloc int[14];
        int lap0 = 0, lap1 = 0;
        for (var n = 0; n < fullPlanes; n++)
        {
            var chroma = n > 0;
            var count = 0;
            if ((cbp >> n & 1) != 0)
            {
                var location = Subsampled && chroma ? Format == Yuv420 ? 10 : 2 : 1;
                count = DecodeBlock(lp, chroma, runs, levels, location);
                if (Subsampled && chroma)
                {
                    // U and V interleaved in a fixed order.
                    joint.Clear();
                    for (int k = 0, i = 0; k < count; k++, i++)
                    {
                        i += runs[k];
                        if (i >= 14)
                            throw new InvalidDataException("Invalid JPEG XR lowpass run");
                        joint[i] = levels[k];
                    }
                    var (offset, total, map) = Format == Yuv420 ? (1, 6, Transpose420) : (0, 14, Transpose422);
                    for (var k = 0; k < total; k++)
                        dclp[(k & 1) + 1][at + map[RemapChroma[(k >> 1) + offset]]] = joint[k];
                }
                else
                    for (int k = 0, i = 1; k < count; k++, i++)
                    {
                        i += runs[k];
                        if (i > 15)
                            throw new InvalidDataException("Invalid JPEG XR lowpass run");
                        dclp[n][at + lpScan.Next(i)] = levels[k];
                    }
            }
            var modelBits = modelLp.Bits[chroma ? 1 : 0];
            if (chroma)
                lap1 += count;
            else
                lap0 += count;
            if (modelBits == 0)
                continue;
            if (Subsampled && chroma)
            {
                var map = Format == Yuv420 ? Transpose420 : Transpose422;
                for (var k = 1; k < map.Length; k++)
                {
                    dclp[1][at + map[k]] = RefineLp(dclp[1][at + map[k]], modelBits);
                    dclp[2][at + map[k]] = RefineLp(dclp[2][at + map[k]], modelBits);
                }
            }
            else
                for (var k = 1; k < 16; k++)
                    dclp[n][at + Transpose444[k]] = RefineLp(dclp[n][at + Transpose444[k]], modelBits);
        }
        modelLp.Update(lap0, lap1, 1, Format, Components);
        if (mb.Adapt)
            lp.Adapt();
    }

    int RefineLp(int coeff, int modelBits)
    {
        var refinement = bits.Read(modelBits);
        if (coeff > 0)
            return (coeff << modelBits) + refinement;
        if (coeff < 0)
            return (coeff << modelBits) - refinement;
        return refinement != 0 && bits.Bit() != 0 ? -refinement : refinement;
    }

    /// <summary>DECODE_BLOCK( ) (8.7.18.5): runs of zeros and levels of the non-zero coefficients.</summary>
    int DecodeBlock(JxrBandVlc band, bool chroma, Span<int> runs, Span<int> levels, int location)
    {
        var first = (chroma ? band.FirstChr : band.FirstLum).Read(bits);
        var negative = bits.Bit() != 0;
        var sr = first & 1;
        var srn = first >> 2;
        var context = sr & srn;
        var level = (first & 2) != 0 ? AbsLevel(band.Abs[context]) : 1;
        levels[0] = negative ? -level : level;
        runs[0] = sr == 0 ? Run(15 - location) : 0;
        location += runs[0] + 1;
        var count = 1;
        while (srn != 0)
        {
            if (count >= 16)
                throw new InvalidDataException("Invalid JPEG XR coefficient block");
            sr = srn & 1;
            runs[count] = sr == 0 ? Run(15 - location) : 0;
            location += runs[count] + 1;
            var index = location < 15 ? band.Index[(chroma ? 2 : 0) + context].Read(bits)
                : location == 15 ? JxrTables.IndexB.Read(bits) : bits.Bit();
            srn = index >> 1;
            context &= srn;
            negative = bits.Bit() != 0;
            level = (index & 1) != 0 ? AbsLevel(band.Abs[context]) : 1;
            levels[count++] = negative ? -level : level;
        }
        return count;
    }

    /// <summary>DECODE_RUN( ) (8.7.18.6).</summary>
    int Run(int maxRun)
    {
        switch (maxRun)
        {
            case 1:
                return 1;
            case 2:
                return JxrTables.RunValue2.Read(bits) + 1;
            case 3:
                return JxrTables.RunValue3.Read(bits) + 1;
            case 4:
                return JxrTables.RunValue4.Read(bits) + 1;
            case < 1 or > 14:
                throw new InvalidDataException("Invalid JPEG XR run");
        }
        var index = JxrTables.RunIndex.Read(bits) + 5 * RunBin[maxRun];
        return RunRemap[index] + bits.Read(RunFixedLength[index]);
    }

    /// <summary>MB_CBPHP( ) (8.7.17.2) with the prediction of 8.7.17.5.</summary>
    void ParseCbphp(JxrMb mb)
    {
        if (mb.TileStart)
        {
            numCbphp.Reset();
            numBlkCbphp.Reset();
        }
        Span<int> diff = stackalloc int[Components];
        var groups = Format is Yuvk or NComponent ? Components : 1;
        for (var i = 0; i < groups; i++)
        {
            var blocks = RefineCbphp(numCbphp.Read(bits));
            for (var block = 0; block < 4; block++)
            {
                if ((blocks & 1 << block) == 0)
                    continue;
                var value = numBlkCbphp.Read(bits) + 1;
                var pattern = 0;
                if (value >= 6)
                {
                    pattern = 0x10 * (JxrTables.Chroma.Read(bits) + 1);
                    if (value >= 9)
                        value += JxrTables.Chroma.Read(bits);
                    value -= 6;
                }
                if (value > 5)
                    throw new InvalidDataException("Invalid JPEG XR block pattern");
                pattern += CbphpOut[CbphpOff[value] + bits.Read(CbphpFlc[value])];
                switch (Format)
                {
                    case Yuv444:
                        diff[0] |= (pattern & 0xF) << block * 4;
                        for (var k = 0; k < 2; k++)
                            if ((pattern >> (k + 4) & 1) != 0)
                                diff[k + 1] |= RefineCbphp(JxrTables.NumChBlk.Read(bits) + 1) << block * 4;
                        break;
                    case Yuv422:
                        diff[0] |= (pattern & 0xF) << block * 4;
                        for (var k = 0; k < 2; k++)
                            if ((pattern >> (k + 4) & 1) != 0)
                                diff[k + 1] |= ChromaShift422[JxrTables.Chroma.Read(bits) + 1] << ChromaShift422[block];
                        break;
                    case Yuv420:
                        diff[0] |= (pattern & 0xF) << block * 4;
                        diff[1] |= (pattern >> 4 & 1) << block;
                        diff[2] |= (pattern >> 5 & 1) << block;
                        break;
                    default:
                        diff[i] |= pattern << block * 4;
                        break;
                }
            }
        }
        if (mb.TileStart)
        {
            cbphpState[0] = cbphpState[1] = 0;
            countOnes[0] = countOnes[1] = -4;
            countZeroes[0] = countZeroes[1] = 4;
        }
        var at = mb.Index * Components;
        var full = Subsampled ? 1 : Components;
        for (var i = 0; i < full; i++)
            cbphp[at + i] = PredictCbphp(mb, i, diff[i], 16);
        if (Subsampled)
            for (var i = 1; i < 3; i++)
                cbphp[at + i] = PredictCbphp(mb, i, diff[i], Format == Yuv422 ? 8 : 4);
    }

    int RefineCbphp(int count) => count switch
    {
        1 => 1 << bits.Read(2),
        2 => JxrTables.RefCbphp1Values[JxrTables.RefCbphp1.Read(bits)],
        3 => 0xF ^ 1 << bits.Read(2),
        4 => 0xF,
        _ => 0,
    };

    /// <summary>PredCBPHP444( ), PredCBPHP422( ) and PredCBPHP420( ) by the number of blocks.</summary>
    int PredictCbphp(JxrMb mb, int i, int pattern, int blocks)
    {
        var model = i > 0 ? 1 : 0;
        if (cbphpState[model] == 0)
        {
            var (top, left) = blocks switch { 16 => (10, 5), 8 => (6, 1), _ => (2, 1) };
            if (mb.Left)
                pattern ^= mb.Top ? 1 : cbphp[(mb.Index - image.MbWidth) * Components + i] >> top & 1;
            else
                pattern ^= cbphp[(mb.Index - 1) * Components + i] >> left & 1;
            switch (blocks)
            {
                case 16:
                    pattern ^= 0x02 & pattern << 1;
                    pattern ^= 0x10 & pattern << 3;
                    pattern ^= 0x20 & pattern << 1;
                    pattern ^= (pattern & 0x33) << 2;
                    pattern ^= (pattern & 0xCC) << 6;
                    pattern ^= (pattern & 0x3300) << 2;
                    break;
                case 8:
                    pattern ^= (pattern & 0x01) << 1;
                    pattern ^= (pattern & 0x03) << 2;
                    pattern ^= (pattern & 0x0C) << 2;
                    pattern ^= (pattern & 0x30) << 2;
                    break;
                default:
                    pattern ^= 0x02 & pattern << 1;
                    pattern ^= (pattern & 0x3) << 2;
                    break;
            }
        }
        else if (cbphpState[model] == 2)
            pattern ^= (1 << blocks) - 1;
        var ones = System.Numerics.BitOperations.PopCount((uint)pattern) * (16 / blocks);
        countOnes[model] = Math.Clamp(countOnes[model] + ones - 3, -16, 15);
        countZeroes[model] = Math.Clamp(countZeroes[model] + 16 - ones - 3, -16, 15);
        cbphpState[model] = countOnes[model] < 0 ? countOnes[model] < countZeroes[model] ? 1 : 2 : countZeroes[model] < 0 ? 2 : 0;
        return pattern;
    }

    /// <summary>The number of 4×4 blocks of a component across a macroblock (4, or 2 for subsampled chroma).</summary>
    int BlocksAcross(int c) => c > 0 && Subsampled ? 2 : 4;

    int BlocksDown(int c) => c > 0 && Format == Yuv420 ? 2 : 4;

    /// <summary>The sample index of the first coefficient of a block (raster order in the macroblock).</summary>
    int BlockOrigin(int c, JxrMb mb, int block)
    {
        var across = BlocksAcross(c);
        var x = mb.X * across * 4 + block % across * 4;
        var y = mb.Y * BlocksDown(c) * 4 + block / across * 4;
        return y * SampleWidth[c] + x;
    }

    int Coefficient(int c, int k) => (k >> 2) * SampleWidth[c] + (k & 3);

    /// <summary>MB_HP( ) and MB_HP_FLEX( ) (8.7.18).</summary>
    void ParseHp(JxrMb mb, bool flexbits, int trim)
    {
        if (mb.TileStart)
        {
            hp.Reset();
            hpHor.Reset();
            hpVer.Reset();
            modelHp.Reset(2);
        }
        if (mb.SegmentStart)
        {
            hpHor.ResetTotals();
            hpVer.ResetTotals();
        }
        var scan = hpMode[mb.Index] == 1 ? hpVer : hpHor;
        Span<int> runs = stackalloc int[16], levels = stackalloc int[16];
        int lap0 = 0, lap1 = 0;
        for (var i = 0; i < Components; i++)
        {
            var chroma = i > 0;
            var modelBits = modelHp.Bits[chroma ? 1 : 0];
            var blocks = BlocksAcross(i) * BlocksDown(i);
            var pattern = cbphp[mb.Index * Components + i];
            var samples = Samples[i];
            for (var block = 0; block < blocks; block++, pattern >>= 1)
            {
                var origin = BlockOrigin(i, mb, blocks == 16 ? HierScanOrder[block] : block);
                for (var k = 1; k < 16; k++)
                    samples[origin + Coefficient(i, k)] = 0;
                if ((pattern & 1) == 0)
                {
                    if (flexbits)
                        Flexbits(i, origin, modelBits, trim);
                    continue;
                }
                var count = DecodeBlock(hp, chroma, runs, levels, 1);
                for (int n = 0, k = 1; n < count; n++, k++)
                {
                    k += runs[n];
                    if (k > 15)
                        throw new InvalidDataException("Invalid JPEG XR highpass run");
                    samples[origin + Coefficient(i, scan.Next(k))] = levels[n];
                }
                if (flexbits)
                    Flexbits(i, origin, modelBits, trim);
                if (chroma)
                    lap1 += count;
                else
                    lap0 += count;
            }
        }
        modelBitsHp[mb.Index * 2] = (byte)modelHp.Bits[0];
        modelBitsHp[mb.Index * 2 + 1] = (byte)modelHp.Bits[1];
        modelHp.Update(lap0, lap1, 2, Format, Components);
        if (mb.Adapt)
        {
            hp.Adapt();
            numCbphp.Adapt();
            numBlkCbphp.Adapt();
        }
    }

    /// <summary>MB_FLEXBITS( ) (8.7.19.1) of a frequency-ordered codestream.</summary>
    void ParseFlexbits(JxrMb mb, int trim)
    {
        for (var i = 0; i < Components; i++)
        {
            var modelBits = modelBitsHp[mb.Index * 2 + (i > 0 ? 1 : 0)];
            var blocks = BlocksAcross(i) * BlocksDown(i);
            for (var block = 0; block < blocks; block++)
                Flexbits(i, BlockOrigin(i, mb, blocks == 16 ? HierScanOrder[block] : block), modelBits, trim);
        }
    }

    /// <summary>
    /// BLOCK_FLEXBITS( ) (8.7.19.2), combined with the remapping of 9.5.4: the variable-length coded part of each
    /// highpass coefficient shifted by the model bits, plus the refinement bits.
    /// </summary>
    void Flexbits(int c, int origin, int modelBits, int trim)
    {
        var left = Math.Max(modelBits - trim, 0);
        var samples = Samples[c];
        for (var n = 1; n < 16; n++)
        {
            var at = origin + Coefficient(c, Transpose444[n]);
            var coeff = samples[at];
            var flex = 0;
            if (left > 0)
            {
                flex = bits.Read(left);
                if (coeff < 0 || coeff == 0 && flex != 0 && bits.Bit() != 0)
                    flex = -flex;
                flex <<= trim;
            }
            samples[at] = (coeff << modelBits) + flex;
        }
    }

    static int QuantMap(int qp, int scaledShift, bool scaled)
    {
        if (qp == 0)
            return 1;
        if (scaled)
            return qp < 16 ? qp << scaledShift : (16 + qp % 16) << ((qp >> 4) - 1 + scaledShift);
        if (qp < 32)
            return (qp + 3) >> 2;
        if (qp < 48)
            return ((16 + qp % 16 + 1) >> 1) << ((qp >> 4) - 2);
        return (16 + qp % 16) << ((qp >> 4) - 3);
    }

    /// <summary>
    /// The DC and lowpass coefficients of a macroblock (9.4.2, 9.4.3): prediction from the neighbours,
    /// dequantization, and the highpass prediction direction they imply (9.6.3.2). Windows' codec derives that
    /// direction from the lowpass coefficients before dequantization (the order of steps in 9.4 would take them
    /// after); decoding is bit-exact with it only so.
    /// </summary>
    public void DecodeDcLp(JxrMb mb)
    {
        var qp = tileQp[mb.Tile];
        var (at, p) = (mb.Index * 16, mb.Index * 7);
        var (left, top) = ((mb.Index - 1) * 7, (mb.Index - image.MbWidth) * 7);
        int dcMode;
        if (mb.Left)
            dcMode = mb.Top ? 3 : 1;
        else if (mb.Top)
            dcMode = 0;
        else
        {
            var topLeft = top - 7;
            var (strHor, strVer) = (Math.Abs(pred[0][topLeft] - pred[0][left]), Math.Abs(pred[0][topLeft] - pred[0][top]));
            if (Format is not (YOnly or NComponent))
            {
                var scale = Format == Yuv420 ? 8 : Format == Yuv422 ? 4 : 2;
                strHor = strHor * scale + Math.Abs(pred[1][topLeft] - pred[1][left]) + Math.Abs(pred[2][topLeft] - pred[2][left]);
                strVer = strVer * scale + Math.Abs(pred[1][topLeft] - pred[1][top]) + Math.Abs(pred[2][topLeft] - pred[2][top]);
            }
            dcMode = strHor * 4 < strVer ? 1 : strVer * 4 < strHor ? 0 : 2;
        }
        var lpMode = dcMode == 0 && lpIndex[mb.Index] == lpIndex[mb.Index - 1] ? 0
            : dcMode == 1 && lpIndex[mb.Index] == lpIndex[mb.Index - image.MbWidth] ? 1 : 2;
        for (var i = 0; i < Components; i++)
        {
            var (d, pr) = (dclp[i], pred[i]);
            var full = i == 0 || !Subsampled;
            d[at] += dcMode switch
            {
                0 => pr[left],
                1 => pr[top],
                2 => (pr[top] + pr[left] + (full ? 0 : 1)) >> 1,
                _ => 0,
            };
            pr[p] = d[at];
            if (full)
            {
                if (lpMode == 0)
                {
                    d[at + 4] += pr[left + 4];
                    d[at + 8] += pr[left + 5];
                    d[at + 12] += pr[left + 6];
                }
                else if (lpMode == 1)
                {
                    d[at + 1] += pr[top + 1];
                    d[at + 2] += pr[top + 2];
                    d[at + 3] += pr[top + 3];
                }
                pr[p + 1] = d[at + 1];
                pr[p + 2] = d[at + 2];
                pr[p + 3] = d[at + 3];
                pr[p + 4] = d[at + 4];
                pr[p + 5] = d[at + 8];
                pr[p + 6] = d[at + 12];
            }
            else if (Format == Yuv420)
            {
                if (lpMode == 0)
                    d[at + 2] += pr[left + 2];
                else if (lpMode == 1)
                    d[at + 1] += pr[top + 1];
                pr[p + 1] = d[at + 1];
                pr[p + 2] = d[at + 2];
            }
            else
            {
                if (lpMode == 0)
                {
                    d[at + 4] += pr[left + 4];
                    d[at + 2] += pr[left + 2];
                    d[at + 6] += pr[left + 6];
                }
                else if (lpMode == 1)
                {
                    d[at + 4] += pr[top + 4];
                    d[at + 1] += pr[top + 5];
                    d[at + 5] += d[at + 1];
                }
                else if (dcMode == 1)
                    d[at + 5] += d[at + 1];
                pr[p + 1] = d[at + 1];
                pr[p + 2] = d[at + 2];
                pr[p + 4] = d[at + 4];
                pr[p + 5] = d[at + 5];
                pr[p + 6] = d[at + 6];
            }
        }
        var y = dclp[0];
        var hor = Math.Abs(y[at + 1]) + Math.Abs(y[at + 2]) + Math.Abs(y[at + 3]);
        var ver = Math.Abs(y[at + 4]) + Math.Abs(y[at + 8]) + Math.Abs(y[at + 12]);
        if (Format is not (YOnly or NComponent))
            for (var i = 1; i <= 2; i++)
            {
                var c = dclp[i];
                hor += Math.Abs(c[at + 1]);
                if (Format == Yuv420)
                    ver += Math.Abs(c[at + 2]);
                else if (Format == Yuv422)
                {
                    ver += Math.Abs(c[at + 2]) + Math.Abs(c[at + 6]);
                    hor += Math.Abs(c[at + 5]);
                }
                else
                    ver += Math.Abs(c[at + 4]);
            }
        hpMode[mb.Index] = (byte)(hor * 4 < ver ? 0 : ver * 4 < hor ? 1 : 2);
        for (var i = 0; i < Components; i++)
        {
            var d = dclp[i];
            var shift = i == 0 ? 1 : 0;
            if (qp.Dc.Length > 0)
                d[at] *= QuantMap(qp.Dc[i], shift, Scaled);
            if (qp.Lp.Length > 0)
            {
                var scale = QuantMap(qp.Lp[lpIndex[mb.Index]][i], shift, Scaled);
                var count = i == 0 || !Subsampled ? 16 : Format == Yuv422 ? 8 : 4;
                for (var j = 1; j < count; j++)
                    d[at + j] *= scale;
            }
        }
    }

    /// <summary>The highpass coefficients of a macroblock (9.4.4): dequantization and prediction inside it.</summary>
    public void DecodeHp(JxrMb mb)
    {
        var qp = tileQp[mb.Tile];
        for (var i = 0; i < Components; i++)
        {
            var scale = qp.Hp.Length > 0 ? QuantMap(qp.Hp[hpIndex[mb.Index]][i], 1, Scaled) : 1;
            var shift = Bands == BandsAll ? 0 : modelBitsHp[mb.Index * 2 + (i > 0 ? 1 : 0)];
            var blocks = BlocksAcross(i) * BlocksDown(i);
            var samples = Samples[i];
            for (var block = 0; block < blocks; block++)
            {
                var origin = BlockOrigin(i, mb, block);
                for (var k = 1; k < 16; k++)
                {
                    var at = origin + Coefficient(i, k);
                    samples[at] = (samples[at] << shift) * scale;
                }
            }
        }
        var mode = hpMode[mb.Index];
        if (mode == 2)
            return;
        for (var i = 0; i < Components; i++)
        {
            var across = BlocksAcross(i);
            var blocks = across * BlocksDown(i);
            var samples = Samples[i];
            // From the left: the first column of a block from the block before it; from the top: the first row.
            for (var block = 0; block < blocks; block++)
            {
                if (mode == 0 ? block % across == 0 : block < across)
                    continue;
                var origin = BlockOrigin(i, mb, block);
                var from = BlockOrigin(i, mb, mode == 0 ? block - 1 : block - across);
                for (var k = 1; k < 4; k++)
                {
                    var offset = Coefficient(i, mode == 0 ? k * 4 : k);
                    samples[origin + offset] += samples[from + offset];
                }
            }
        }
    }

    /// <summary>The transforms and overlap filters of 9.9 over the whole plane, leaving samples.</summary>
    public void Reconstruct() => JxrTransform.Reconstruct(this, image, dclp);
}

/// <summary>The adaptive code tables of the lowpass or highpass band (8.8.3.2, 8.8.3.3).</summary>
sealed class JxrBandVlc
{
    public readonly JxrAdaptiveVlc FirstLum = new(JxrTables.FirstIndex), FirstChr = new(JxrTables.FirstIndex);
    /// <summary>INDEX_A tables by chroma (2) plus context.</summary>
    public readonly JxrAdaptiveVlc[] Index = [new(JxrTables.IndexA), new(JxrTables.IndexA), new(JxrTables.IndexA), new(JxrTables.IndexA)];
    /// <summary>ABS_LEVEL_INDEX tables by context.</summary>
    public readonly JxrAdaptiveVlc[] Abs = [new(JxrTables.AbsLevelIndex), new(JxrTables.AbsLevelIndex)];

    public void Reset()
    {
        FirstLum.Reset();
        FirstChr.Reset();
        foreach (var vlc in Index)
            vlc.Reset();
        foreach (var vlc in Abs)
            vlc.Reset();
    }

    public void Adapt()
    {
        FirstLum.Adapt();
        FirstChr.Adapt();
        foreach (var vlc in Index)
            vlc.Adapt();
        foreach (var vlc in Abs)
            vlc.Adapt();
    }
}
