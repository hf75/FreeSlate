using System.Buffers.Binary;
using System.Collections.Concurrent;
using HarfBuzzSharp;
using Buffer = HarfBuzzSharp.Buffer;

namespace FreeSlate.Text;

/// <summary>A shaped glyph: id, UTF-16 index of its cluster, advance and kerning to the next glyph, offsets (em).</summary>
public readonly record struct ShapedGlyph(uint Id, int Cluster, double Advance, double Kerning, double OffsetX, double OffsetY);

/// <summary>
/// A loaded font file: the vertical metrics PowerPoint's line model needs and a HarfBuzz font for shaping.
/// Advances are measured unhinted in font units. Kerning comes from the legacy 'kern' table, as in PowerPoint
/// (GDI kerning pairs, measured with the text-snap deck); fonts without one are kerned from GPOS only where
/// PowerPoint lays them out as OpenType fonts (PostScript and variable fonts).
/// </summary>
public sealed class FontData : IDisposable
{
    static readonly Feature[] NoLigaturesNoKerning = Parse("-liga", "-clig", "-kern");
    static readonly Feature[] NoLigatures = Parse("-liga", "-clig");
    static readonly Feature[] NoAlternatesNoKerning = Parse("-liga", "-clig", "-calt", "-kern");

    readonly Blob blob;
    readonly Face face;
    readonly Font font;
    readonly ConcurrentDictionary<(string, bool), double> widths = new();
    readonly ConcurrentDictionary<(string, double, bool, double), double> layoutWidths = new();
    readonly Dictionary<uint, short>? kerningPairs;
    readonly ConcurrentDictionary<uint, string> outlines = new();

    /// <param name="index">The face's place in a font collection (.ttc).</param>
    public FontData(string path, string family, int index = 0) : this(File.ReadAllBytes(path), path, family, index, inMemory: false)
    {
    }

    /// <summary>A font that exists in memory only (embedded in the presentation); <paramref name="name"/> stands for its path.</summary>
    public FontData(byte[] data, string name, string family) : this(data, name, family, 0, inMemory: true)
    {
    }

    FontData(byte[] data, string path, string family, int index, bool inMemory, double advanceExtra = 0, float weight = 0)
    {
        Path = path;
        Family = family;
        Index = index;
        memory = inMemory ? data : null;
        this.advanceExtra = advanceExtra;
        var offset = data.Length > 12 && data[0] == 't' && data[1] == 't' && data[2] == 'c' && data[3] == 'f'
            ? (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(12 + 4 * index))
            : 0;
        var tables = new Dictionary<string, int>();
        var count = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 4));
        for (var i = 0; i < count; i++)
        {
            var record = offset + 12 + i * 16;
            var tag = System.Text.Encoding.ASCII.GetString(data, record, 4);
            tables[tag] = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(record + 8));
            tableLengths[tag] = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(record + 12));
        }
        tableOffsets = tables;

        UnitsPerEm = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(tables["head"] + 18));
        double ascent, descent, lineGap = 0;
        double winAscent, winDescent;
        if (tables.TryGetValue("hhea", out var hheaOffset))
        {
            winAscent = BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(hheaOffset + 4));
            winDescent = -BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(hheaOffset + 6));
        }
        else
        {
            (winAscent, winDescent) = (0.8, 0.2);
        }
        if (tables.TryGetValue("OS/2", out var os2))
        {
            winAscent = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(os2 + 74));
            winDescent = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(os2 + 76));
            var selection = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(os2 + 62));
            var useTypo = (selection & 0x80) != 0;
            ascent = useTypo ? BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(os2 + 68)) : BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(os2 + 74));
            descent = useTypo ? -BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(os2 + 70)) : BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(os2 + 76));
            lineGap = useTypo ? BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(os2 + 72)) : 0;
        }
        else
        {
            ascent = BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(tables["hhea"] + 4));
            descent = -BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(tables["hhea"] + 6));
        }
        // The typo line gap goes above the text.
        var gapAbove = Math.Max(0, lineGap);
        AscentRatio = ascent + descent > 0 ? (gapAbove + ascent) / (gapAbove + ascent + descent) : 0.8;
        UnitsPerEm = UnitsPerEm == 0 ? 1000 : UnitsPerEm;
        Ascent = ascent / UnitsPerEm;
        Descent = descent / UnitsPerEm;
        LineGap = Math.Max(0, lineGap) / UnitsPerEm;
        WinAscent = winAscent / UnitsPerEm;
        WinDescent = winDescent / UnitsPerEm;

        kerningPairs = tables.TryGetValue("kern", out var kernOffset) ? ReadKerning(data, kernOffset) : null;
        IsPostScript = tables.ContainsKey("CFF ") || tables.ContainsKey("CFF2");
        openType = IsPostScript || tables.ContainsKey("fvar");
        gposKerning = kerningPairs == null && openType && tables.ContainsKey("GPOS");
        IsColor = tables.ContainsKey("COLR") || tables.ContainsKey("CBDT") || tables.ContainsKey("sbix") || tables.ContainsKey("SVG ");
        if (tables.TryGetValue("post", out var post) && post + 12 <= data.Length)
        {
            UnderlinePosition = -BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(post + 8)) / (double)UnitsPerEm;
            UnderlineThickness = BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(post + 10)) / (double)UnitsPerEm;
        }
        if (tables.TryGetValue("OS/2", out var capTable) && capTable + 90 <= data.Length
            && BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(capTable)) >= 2 && BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(capTable + 88)) is > 0 and var cap)
            CapHeight = cap / (double)UnitsPerEm;
        if (tables.TryGetValue("OS/2", out var os2Table) && os2Table + 30 <= data.Length)
        {
            StrikeoutThickness = BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(os2Table + 26)) / (double)UnitsPerEm;
            StrikeoutPosition = BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(os2Table + 28)) / (double)UnitsPerEm;
            double Em(int offset) => BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(os2Table + offset)) / (double)UnitsPerEm;
            (SubscriptSize, SubscriptOffset, SuperscriptSize, SuperscriptOffset) = (Em(12), Em(16), Em(20), Em(24));
        }
        if (SuperscriptSize <= 0 || SubscriptSize <= 0)
            (SubscriptSize, SubscriptOffset, SuperscriptSize, SuperscriptOffset) = (0.65, 0.14, 0.65, 0.48);
        if (UnderlineThickness <= 0) (UnderlinePosition, UnderlineThickness) = (0.1, 0.05);
        if (StrikeoutThickness <= 0) (StrikeoutPosition, StrikeoutThickness) = (0.3, 0.05);

        blob = inMemory ? NativeBlob(data) : Blob.FromFile(path);
        face = new Face(blob, index);
        font = new Font(face);
        font.SetScale(UnitsPerEm, UnitsPerEm);
        // A variable font's weight axis; an instance of it set to the weight asked for.
        var wght = new Tag('w', 'g', 'h', 't');
        if (face.TryFindVariationAxis(wght, out var axis))
        {
            WeightAxis = (axis.MinValue, axis.DefaultValue, axis.MaxValue);
            if (weight > 0)
            {
                Weight = Math.Clamp(weight, axis.MinValue, axis.MaxValue);
                font.SetVariations([new Variation { Tag = wght, Value = Weight }]);
            }
        }
    }

    /// <summary>
    /// A blob of a font in memory, in native memory of its own: HarfBuzz reads a font when first used, and the managed
    /// array HarfBuzzSharp's Blob.FromStream points to may have been moved by the garbage collector by then (an embedded
    /// font then had no glyphs at all and its text fell back to another font).
    /// </summary>
    static Blob NativeBlob(byte[] data)
    {
        var memory = System.Runtime.InteropServices.Marshal.AllocHGlobal(Math.Max(1, data.Length));
        System.Runtime.InteropServices.Marshal.Copy(data, 0, memory, data.Length);
        return new Blob(memory, data.Length, MemoryMode.ReadOnly, () => System.Runtime.InteropServices.Marshal.FreeHGlobal(memory));
    }

    readonly Dictionary<string, int> tableOffsets;
    readonly Dictionary<string, int> tableLengths = new();
    readonly byte[]? memory;

    /// <summary>The bytes of a font table (read from the file again), or null when the font has none.</summary>
    public byte[]? Table(string tag)
    {
        if (!tableOffsets.TryGetValue(tag, out var offset) || !tableLengths.TryGetValue(tag, out var length))
            return null;
        if (memory != null)
            return memory.AsSpan(offset, length).ToArray();
        using var stream = File.OpenRead(Path);
        stream.Position = offset;
        var bytes = new byte[length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    /// <summary>The glyph for a character, or 0 when the font has none.</summary>
    public uint GlyphFor(int codepoint) => font.TryGetGlyph((uint)codepoint, out var glyph) ? glyph : 0;

    /// <summary>A glyph's advance in font units.</summary>
    public int GlyphAdvance(uint glyph) => font.GetHorizontalGlyphAdvance(glyph);

    /// <summary>A glyph's ink box in font units (x right, y up): left, bottom, right, top.</summary>
    public (int Left, int Bottom, int Right, int Top) GlyphBox(uint glyph) =>
        font.TryGetGlyphExtents(glyph, out var e) ? (e.XBearing, e.YBearing + e.Height, e.XBearing + e.Width, e.YBearing) : (0, 0, 0, 0);

    /// <summary>The face's place in its font collection.</summary>
    public int Index { get; }

    public string Path { get; }

    /// <summary>The family name as used in CSS.</summary>
    public string Family { get; }

    public int UnitsPerEm { get; }

    /// <summary>
    /// Share of the line height above the baseline at single spacing: with the typo metrics if the font sets
    /// USE_TYPO_METRICS, the line gap above the ascent, (gap + ascent) / (gap + ascent + descent); else the Windows
    /// metrics, ascent / (ascent + descent) (measured against PowerPoint: Gabriola, whose gap is 0.7 em, sits 6 pt
    /// lower at 40 pt than without its gap; Bahnschrift and Cambria Math 1.5 pt).
    /// </summary>
    public double AscentRatio { get; }

    /// <summary>Ascent and descent in em from the same metrics as <see cref="AscentRatio"/> (chart text boxes use them).</summary>
    public double Ascent { get; }

    public double Descent { get; }

    /// <summary>The typo line gap in em when the font sets USE_TYPO_METRICS, else 0.</summary>
    public double LineGap { get; }

    /// <summary>Color fonts (COLR, CBDT, sbix, SVG): their glyphs are left to the browser.</summary>
    public bool IsColor { get; }

    /// <summary>Fonts with PostScript outlines (CFF), which PowerPoint kerns and emboldens otherwise than TrueType fonts.</summary>
    public bool IsPostScript { get; }

    /// <summary>
    /// What simulated bold adds to every glyph's advance of a PostScript font, in em; PowerPoint draws the glyphs
    /// themselves as they are (measured with an embedded CFF font: 0.027 em per character, spaces too, from 40 to
    /// 48 pt). TrueType fonts keep their advances and are emboldened a little instead.
    /// </summary>
    public const double WidenedBold = 0.027;

    /// <summary>The same font with <paramref name="extra"/> em added to every glyph's advance (simulated bold of PostScript fonts).</summary>
    public FontData Widened(double extra) => new(memory ?? File.ReadAllBytes(Path), Path, Family, Index, memory != null, extra, Weight);

    /// <summary>The weight axis (wght) of a variable font: its lowest, default and highest weight; null for other fonts.</summary>
    public (float Min, float Default, float Max)? WeightAxis { get; }

    /// <summary>The weight this instance of a variable font is set to; 0 for the default instance.</summary>
    public float Weight { get; }

    /// <summary>Which instance of the font file this is ("" for the default one), for what tells instances apart.</summary>
    public string Instance => Weight > 0 ? "wght=" + Weight.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";

    /// <summary>The same variable font set to another weight (wght axis).</summary>
    public FontData Weighted(float weight) => new(memory ?? File.ReadAllBytes(Path), Path, Family, Index, memory != null, advanceExtra, weight);

    // PostScript and variable fonts, which PowerPoint lays out as OpenType fonts: without a 'kern' table they are kerned
    // from GPOS (measured with an embedded CFF font, an embedded variable font, Bahnschrift, Segoe UI Variable and
    // Sitka) and contextual alternates apply (Cascadia Code's calt ligatures). Static TrueType fonts are kerned only from
    // their 'kern' table (Gabriola, Javanese Text, Yu Mincho: GPOS pairs, but not kerned) and take no contextual
    // alternates in simple scripts (Segoe Script's x keeps its joining stroke).
    readonly bool openType;
    readonly bool gposKerning;
    readonly double advanceExtra;

    /// <summary>Height of the capitals (OS/2 sCapHeight) in em; 0.7 when the font does not say.</summary>
    public double CapHeight { get; } = 0.7;

    /// <summary>Underline offset below the baseline and thickness (post table), in em.</summary>
    public double UnderlinePosition { get; }

    public double UnderlineThickness { get; }

    /// <summary>Strikeout offset above the baseline and thickness (OS/2 table), in em.</summary>
    public double StrikeoutPosition { get; }

    public double StrikeoutThickness { get; }

    /// <summary>
    /// The size of sub- and superscripts and how far below and above the baseline they stand (OS/2 ySubscriptYSize,
    /// ySubscriptYOffset, ySuperscriptYSize, ySuperscriptYOffset), in em.
    /// </summary>
    public double SubscriptSize { get; }

    public double SubscriptOffset { get; }

    public double SuperscriptSize { get; }

    public double SuperscriptOffset { get; }

    /// <summary>GDI's ascent and descent (OS/2 usWinAscent/usWinDescent) in em; metafile text is placed with them.</summary>
    public double WinAscent { get; }

    public double WinDescent { get; }

    /// <summary>Advance width of <paramref name="text"/> in em (multiply by the font size).</summary>
    public double Width(string text, bool kerning)
    {
        if (text.Length == 0)
            return 0;
        return widths.GetOrAdd((text, kerning), key => Advances(key.Item1, key.Item2).Sum());
    }

    /// <summary>Advance of every UTF-16 code unit of <paramref name="text"/> in em; a cluster's advance goes to its first unit.</summary>
    public double[] Advances(string text, bool kerning) => Shape(text, kerning).Select(a => a.Advance + a.Kerning).ToArray();

    /// <summary>
    /// Advances in points as PowerPoint lays text out: glyph advance, kerning and letter spacing each rounded to
    /// 1/8 point (PowerPoint's 576 dpi master units), measured with the text-snap calibration deck.
    /// </summary>
    public double[] LayoutAdvances(string text, double size, bool kerning, double letterSpacing = 0)
    {
        var shaped = Shape(text, kerning);
        var result = new double[shaped.Length];
        var spacing = MasterUnits(letterSpacing);
        for (var i = 0; i < shaped.Length; i++)
        {
            if (char.IsLowSurrogate(text[i]))
                continue;
            result[i] = MasterUnits(shaped[i].Advance * size) + MasterUnits(shaped[i].Kerning * size) + spacing;
        }
        return result;
    }

    /// <summary>The width of <paramref name="text"/> in points from <see cref="LayoutAdvances"/>.</summary>
    public double LayoutWidth(string text, double size, bool kerning, double letterSpacing = 0) =>
        text.Length == 0 ? 0 : layoutWidths.GetOrAdd((text, size, kerning, letterSpacing), key => LayoutAdvances(key.Item1, key.Item2, key.Item3, key.Item4).Sum());

    /// <summary>A length in points rounded to PowerPoint's master units (1/576 inch = 1/8 point).</summary>
    public static double MasterUnits(double points) => Math.Floor(points * 8 + 0.5) / 8;

    /// <summary>Per UTF-16 code unit: advance and kerning to the next glyph in em (a cluster's values go to its first unit).</summary>
    (double Advance, double Kerning)[] Shape(string text, bool kerning)
    {
        var result = new (double Advance, double Kerning)[text.Length];
        foreach (var glyph in Glyphs(text, kerning))
        {
            if (glyph.Cluster < result.Length)
            {
                result[glyph.Cluster].Advance += glyph.Advance;
                result[glyph.Cluster].Kerning += glyph.Kerning;
            }
        }
        return result;
    }

    /// <summary>
    /// The glyphs of <paramref name="text"/> without ligatures; kerning from the legacy 'kern' table (PostScript and
    /// variable fonts without one: from GPOS). Pieces of different scripts are shaped separately in logical order, so
    /// right-to-left pieces keep their own order inside a left-to-right line.
    /// </summary>
    public ShapedGlyph[] Glyphs(string text, bool kerning, bool rightToLeft = false)
    {
        // A right-to-left run (an odd bidi level) is shaped as one piece right to left: glyphs in visual order,
        // brackets mirrored.
        if (rightToLeft)
            return ShapePiece(text, kerning, rightToLeft: true);
        var pieces = Scripts.Split(text);
        if (pieces.Count <= 1)
            return ShapePiece(text, kerning);
        var result = new List<ShapedGlyph>();
        var offset = 0;
        foreach (var (piece, _, _) in pieces)
        {
            result.AddRange(ShapePiece(piece, kerning).Select(g => g with { Cluster = g.Cluster + offset }));
            offset += piece.Length;
        }
        return result.ToArray();
    }

    ShapedGlyph[] ShapePiece(string text, bool kerning, bool rightToLeft = false)
    {
        if (text.Length == 0)
            return [];
        using var buffer = new Buffer();
        buffer.AddUtf16(text);
        buffer.GuessSegmentProperties();
        if (rightToLeft)
            buffer.Direction = Direction.RightToLeft;
        var simple = buffer.Script == Script.Latin || buffer.Script == Script.Greek || buffer.Script == Script.Cyrillic
            || buffer.Script == Script.Common || buffer.Script == Script.Inherited;
        font.Shape(buffer, !openType && simple ? NoAlternatesNoKerning : NoLigaturesNoKerning);
        var infos = buffer.GlyphInfos;
        var positions = buffer.GlyphPositions;
        // GPOS kerning: what shaping with the 'kern' feature changes in each advance.
        GlyphPosition[]? kerned = null;
        if (kerning && gposKerning)
        {
            using var withKerning = new Buffer();
            withKerning.AddUtf16(text);
            withKerning.GuessSegmentProperties();
            if (rightToLeft)
                withKerning.Direction = Direction.RightToLeft;
            font.Shape(withKerning, NoLigatures);
            kerned = withKerning.GlyphPositions.Length == positions.Length ? withKerning.GlyphPositions : null;
        }
        var result = new ShapedGlyph[infos.Length];
        for (var i = 0; i < infos.Length; i++)
        {
            var kern = 0.0;
            if (kerning && kerningPairs != null && i + 1 < infos.Length
                && kerningPairs.TryGetValue(infos[i].Codepoint << 16 | infos[i + 1].Codepoint & 0xFFFF, out var value))
                kern = (double)value / UnitsPerEm;
            else if (kerned != null)
                kern = (double)(kerned[i].XAdvance - positions[i].XAdvance) / UnitsPerEm;
            var advance = (double)positions[i].XAdvance / UnitsPerEm + (positions[i].XAdvance != 0 ? advanceExtra : 0);
            result[i] = new ShapedGlyph(infos[i].Codepoint, (int)infos[i].Cluster, advance, kern,
                (double)positions[i].XOffset / UnitsPerEm, (double)positions[i].YOffset / UnitsPerEm);
        }
        return result;
    }

    /// <summary>
    /// The glyphs of <paramref name="text"/> shaped with exactly the OpenType features given ("kern", "-liga", …), in
    /// logical order for left-to-right text; advances and offsets in em include what GPOS positioning adds, Kerning
    /// stays 0. Word chooses its features itself (kerning by size, ligatures and contextual alternates by the run's
    /// settings), unlike PowerPoint (<see cref="Glyphs"/>).
    /// </summary>
    public ShapedGlyph[] GlyphsWithFeatures(string text, IReadOnlyList<string> features, bool rightToLeft = false, string? language = null)
    {
        if (text.Length == 0)
            return [];
        var parsed = features.Select(f => Feature.TryParse(f, out var feature) ? feature : throw new ArgumentException(f)).ToArray();
        List<string> pieces = rightToLeft ? [text] : Scripts.Split(text).Select(p => p.Text).ToList();
        var result = new List<ShapedGlyph>();
        var offset = 0;
        foreach (var piece in pieces)
        {
            using var buffer = new Buffer();
            buffer.AddUtf16(piece);
            buffer.GuessSegmentProperties();
            if (rightToLeft)
                buffer.Direction = Direction.RightToLeft;
            if (language != null)
                buffer.Language = new Language(language);
            font.Shape(buffer, parsed);
            var infos = buffer.GlyphInfos;
            var positions = buffer.GlyphPositions;
            for (var i = 0; i < infos.Length; i++)
            {
                var advance = (double)positions[i].XAdvance / UnitsPerEm + (positions[i].XAdvance != 0 ? advanceExtra : 0);
                result.Add(new ShapedGlyph(infos[i].Codepoint, (int)infos[i].Cluster + offset, advance, 0,
                    (double)positions[i].XOffset / UnitsPerEm, (double)positions[i].YOffset / UnitsPerEm));
            }
            offset += piece.Length;
        }
        return result.ToArray();
    }

    /// <summary>The kerning of a glyph pair in the legacy 'kern' table in em, 0 when there is none.</summary>
    public double LegacyKerning(uint left, uint right) =>
        kerningPairs != null && kerningPairs.TryGetValue(left << 16 | right & 0xFFFF, out var value) ? (double)value / UnitsPerEm : 0;

    /// <summary>The line metrics of the hhea table in em: ascender, descender (positive below) and line gap.</summary>
    public (double Ascender, double Descender, double LineGap) Hhea
    {
        get
        {
            var hhea = Table("hhea");
            return hhea == null || hhea.Length < 10
                ? (Ascent, Descent, LineGap)
                : (BinaryPrimitives.ReadInt16BigEndian(hhea.AsSpan(4)) / (double)UnitsPerEm, -BinaryPrimitives.ReadInt16BigEndian(hhea.AsSpan(6)) / (double)UnitsPerEm,
                    BinaryPrimitives.ReadInt16BigEndian(hhea.AsSpan(8)) / (double)UnitsPerEm);
        }
    }

    /// <summary>Whether the font has a glyph for the character.</summary>
    public bool HasGlyph(int c) => font.TryGetGlyph((uint)c, out var glyph) && glyph != 0;

    /// <summary>Whether the font has serifs by its PANOSE numbers: a Latin text face with a serif style of 2 to 10.</summary>
    public bool Serif
    {
        get
        {
            var os2 = Table("OS/2");
            return os2 != null && os2.Length >= 42 && os2[32] == 2 && os2[33] is >= 2 and <= 10;
        }
    }

    /// <summary>
    /// Whether the font is made for East Asian text: its OS/2 code page range names a Japanese, Chinese or Korean code
    /// page (JIS, GB 2312, Wansung, Big5, Johab).
    /// </summary>
    public bool EastAsian
    {
        get
        {
            var os2 = Table("OS/2");
            if (os2 == null || os2.Length < 82 || BinaryPrimitives.ReadUInt16BigEndian(os2) < 1)
                return false;
            var range = BinaryPrimitives.ReadUInt32BigEndian(os2.AsSpan(78));
            return (range & (0x1Fu << 17)) != 0;
        }
    }

    /// <summary>The OS/2 typographic metrics in em (ascender, descender positive below, line gap) and whether USE_TYPO_METRICS is set.</summary>
    public (double Ascender, double Descender, double LineGap, bool Use)? Typo
    {
        get
        {
            var os2 = Table("OS/2");
            if (os2 == null || os2.Length < 78)
                return null;
            var selection = BinaryPrimitives.ReadUInt16BigEndian(os2.AsSpan(62));
            return (BinaryPrimitives.ReadInt16BigEndian(os2.AsSpan(68)) / (double)UnitsPerEm, -BinaryPrimitives.ReadInt16BigEndian(os2.AsSpan(70)) / (double)UnitsPerEm,
                BinaryPrimitives.ReadInt16BigEndian(os2.AsSpan(72)) / (double)UnitsPerEm, (selection & 0x80) != 0);
        }
    }

    /// <summary>The outline of a glyph as SVG path data in font units, y down; "" for glyphs without contours.</summary>
    public string Outline(uint glyph) => outlines.GetOrAdd(glyph, g => GlyphOutlines.Draw(font, g));

    /// <summary>The bottom and top of a glyph's ink in font units (y up); zero for empty glyphs.</summary>
    public (double Bottom, double Top) GlyphBounds(uint glyph) =>
        font.TryGetGlyphExtents(glyph, out var extents) ? (extents.YBearing + extents.Height, extents.YBearing) : (0, 0);

    /// <summary>Whether the font has a glyph for every character of <paramref name="text"/> (controls and spaces aside).</summary>
    public bool Covers(string text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value < 0x21 || System.Text.Rune.IsWhiteSpace(rune) || System.Text.Rune.IsControl(rune) || rune.Value is 0x200E or 0x200F)
                continue;
            if (!font.TryGetGlyph((uint)rune.Value, out var glyph) || glyph == 0)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Horizontal format 0 pairs of a version 0 'kern' table. Subtable lengths are not trusted (large tables
    /// overflow the 16-bit field); the pair count gives the size.
    /// </summary>
    static Dictionary<uint, short>? ReadKerning(byte[] data, int offset)
    {
        if (offset + 4 > data.Length || BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset)) != 0)
            return null;
        var pairs = new Dictionary<uint, short>();
        var count = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 2));
        var position = offset + 4;
        for (var t = 0; t < count && position + 14 <= data.Length; t++)
        {
            var coverage = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(position + 4));
            var nPairs = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(position + 6));
            var horizontal = (coverage & 0x07) == 0x01 && coverage >> 8 == 0;
            var overrides = (coverage & 0x08) != 0;
            for (var i = 0; i < nPairs && horizontal; i++)
            {
                var record = position + 14 + i * 6;
                if (record + 6 > data.Length)
                    break;
                var key = (uint)BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(record)) << 16 | BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(record + 2));
                var value = BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(record + 4));
                pairs[key] = overrides || !pairs.TryGetValue(key, out var existing) ? value : (short)(existing + value);
            }
            position += 14 + nPairs * 6;
        }
        return pairs.Count > 0 ? pairs : null;
    }

    Dictionary<ushort, char>? glyphToChar;

    /// <summary>A reverse character map (glyph id to the first BMP character using it), for glyph-index text in metafiles.</summary>
    public IReadOnlyDictionary<ushort, char> GlyphToChar()
    {
        if (glyphToChar != null)
            return glyphToChar;
        var map = new Dictionary<ushort, char>();
        for (var c = 0x20; c <= 0xFFFF; c++)
        {
            if (c is >= 0xD800 and <= 0xDFFF) continue;
            if (font.TryGetGlyph((uint)c, out var glyph) && glyph != 0 && glyph <= ushort.MaxValue)
                map.TryAdd((ushort)glyph, (char)c);
        }
        return glyphToChar = map;
    }

    static Feature[] Parse(params string[] features) =>
        features.Select(f => Feature.TryParse(f, out var feature) ? feature : throw new InvalidOperationException(f)).ToArray();

    public void Dispose()
    {
        font.Dispose();
        face.Dispose();
        blob.Dispose();
    }
}
