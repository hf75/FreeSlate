using System.Buffers.Binary;

namespace FreeSlate.Equations;

/// <summary>A part of a glyph assembly (OpenType MATH GlyphPart): extenders repeat to reach the size asked for.</summary>
public readonly record struct GlyphPart(uint Glyph, int StartConnector, int EndConnector, int FullAdvance, bool Extender);

/// <summary>
/// The OpenType MATH table of a math font (Cambria Math): the layout constants, italics corrections, top accent
/// attachment points and the size variants and assemblies of stretchy glyphs. Values are in font units.
/// </summary>
public sealed class MathTable
{
    readonly byte[] data;
    readonly Dictionary<uint, int> italics = new();
    readonly Dictionary<uint, int> accents = new();
    readonly Dictionary<uint, int> vertical = new();
    readonly Dictionary<uint, int> horizontal = new();
    readonly int variants;

    /// <summary>The constants in table order (MathConstants), the two percentages and two heights first.</summary>
    readonly int[] constants;

    /// <param name="gsub">The font's GSUB table, for the script-style alternates (feature ssty), or null.</param>
    public MathTable(byte[] data, byte[]? gsub = null)
    {
        this.data = data;
        if (gsub != null)
            ReadScriptStyles(gsub);
        var constantsOffset = U16(4);
        var glyphInfo = U16(6);
        variants = U16(8);

        // MathConstants: 2 int16, 2 UFWORD, 51 MathValueRecords (4 bytes), 1 int16.
        constants = new int[56];
        constants[0] = I16(constantsOffset);
        constants[1] = I16(constantsOffset + 2);
        constants[2] = U16(constantsOffset + 4);
        constants[3] = U16(constantsOffset + 6);
        for (var i = 0; i < 51; i++)
            constants[4 + i] = I16(constantsOffset + 8 + i * 4);
        constants[55] = I16(constantsOffset + 8 + 51 * 4);

        if (glyphInfo != 0)
        {
            ReadValues(glyphInfo + U16(glyphInfo), italics);
            ReadValues(glyphInfo + U16(glyphInfo + 2), accents);
        }
        if (variants != 0)
        {
            var vertCount = U16(variants + 6);
            var horizCount = U16(variants + 8);
            var vertCoverage = Coverage(variants + U16(variants + 2));
            var horizCoverage = Coverage(variants + U16(variants + 4));
            foreach (var (glyph, index) in vertCoverage)
                if (index < vertCount) vertical[glyph] = variants + U16(variants + 10 + index * 2);
            foreach (var (glyph, index) in horizCoverage)
                if (index < horizCount) horizontal[glyph] = variants + U16(variants + 10 + vertCount * 2 + index * 2);
        }
    }

    /// <summary>The MATH table of a font, or null when it has none.</summary>
    public static MathTable? Of(Text.FontData font) => font.Table("MATH") is { Length: > 10 } bytes ? new MathTable(bytes, font.Table("GSUB")) : null;

    readonly Dictionary<uint, uint[]> scriptStyles = new();

    /// <summary>
    /// A glyph's form for script (level 1) or script-script size (level 2) from the font's ssty alternates, which
    /// are drawn sturdier and wider for small sizes (Office uses them in scripts, limits and inline fractions), or
    /// the glyph itself.
    /// </summary>
    public uint ScriptStyle(uint glyph, int level) =>
        level > 0 && scriptStyles.TryGetValue(glyph, out var forms) && forms.Length > 0 ? forms[Math.Min(level, forms.Length) - 1] : glyph;

    /// <summary>Reads the alternate substitutions of the GSUB feature ssty (lookup type 3, also inside extension lookups).</summary>
    void ReadScriptStyles(byte[] gsub)
    {
        int U16(int at) => at >= 0 && at + 2 <= gsub.Length ? BinaryPrimitives.ReadUInt16BigEndian(gsub.AsSpan(at)) : 0;
        int U32(int at) => at >= 0 && at + 4 <= gsub.Length ? (int)BinaryPrimitives.ReadUInt32BigEndian(gsub.AsSpan(at)) : 0;
        var features = U16(6);
        var lookups = U16(8);
        if (features == 0 || lookups == 0)
            return;
        var lookupIndices = new HashSet<int>();
        for (var f = 0; f < U16(features); f++)
        {
            var record = features + 2 + f * 6;
            if (record + 6 > gsub.Length || System.Text.Encoding.ASCII.GetString(gsub, record, 4) != "ssty")
                continue;
            var feature = features + U16(record + 4);
            for (var i = 0; i < U16(feature + 2); i++)
                lookupIndices.Add(U16(feature + 4 + i * 2));
        }
        foreach (var index in lookupIndices)
        {
            var lookup = lookups + U16(lookups + 2 + index * 2);
            var type = U16(lookup);
            for (var t = 0; t < U16(lookup + 4); t++)
            {
                var subtable = lookup + U16(lookup + 6 + t * 2);
                var subtableType = type;
                if (type == 7)
                {
                    subtableType = U16(subtable + 2);
                    subtable += U32(subtable + 4);
                }
                if (subtableType != 3 || U16(subtable) != 1)
                    continue;
                var sets = U16(subtable + 4);
                foreach (var (glyph, coverageIndex) in Coverage(gsub, subtable + U16(subtable + 2)))
                {
                    if (coverageIndex >= sets || scriptStyles.ContainsKey(glyph))
                        continue;
                    var set = subtable + U16(subtable + 6 + coverageIndex * 2);
                    var count = U16(set);
                    scriptStyles[glyph] = [.. Enumerable.Range(0, count).Select(i => (uint)U16(set + 2 + i * 2))];
                }
            }
        }
    }

    public int ScriptPercentScaleDown => constants[0];
    public int ScriptScriptPercentScaleDown => constants[1];
    public int DelimitedSubFormulaMinHeight => constants[2];
    public int DisplayOperatorMinHeight => constants[3];
    public int MathLeading => constants[4];
    public int AxisHeight => constants[5];
    public int AccentBaseHeight => constants[6];
    public int FlattenedAccentBaseHeight => constants[7];
    public int SubscriptShiftDown => constants[8];
    public int SubscriptTopMax => constants[9];
    public int SubscriptBaselineDropMin => constants[10];
    public int SuperscriptShiftUp => constants[11];
    public int SuperscriptShiftUpCramped => constants[12];
    public int SuperscriptBottomMin => constants[13];
    public int SuperscriptBaselineDropMax => constants[14];
    public int SubSuperscriptGapMin => constants[15];
    public int SuperscriptBottomMaxWithSubscript => constants[16];
    public int SpaceAfterScript => constants[17];
    public int UpperLimitGapMin => constants[18];
    public int UpperLimitBaselineRiseMin => constants[19];
    public int LowerLimitGapMin => constants[20];
    public int LowerLimitBaselineDropMin => constants[21];
    public int StackTopShiftUp => constants[22];
    public int StackTopDisplayStyleShiftUp => constants[23];
    public int StackBottomShiftDown => constants[24];
    public int StackBottomDisplayStyleShiftDown => constants[25];
    public int StackGapMin => constants[26];
    public int StackDisplayStyleGapMin => constants[27];
    public int StretchStackTopShiftUp => constants[28];
    public int StretchStackBottomShiftDown => constants[29];
    public int StretchStackGapAboveMin => constants[30];
    public int StretchStackGapBelowMin => constants[31];
    public int FractionNumeratorShiftUp => constants[32];
    public int FractionNumeratorDisplayStyleShiftUp => constants[33];
    public int FractionDenominatorShiftDown => constants[34];
    public int FractionDenominatorDisplayStyleShiftDown => constants[35];
    public int FractionNumeratorGapMin => constants[36];
    public int FractionNumDisplayStyleGapMin => constants[37];
    public int FractionRuleThickness => constants[38];
    public int FractionDenominatorGapMin => constants[39];
    public int FractionDenomDisplayStyleGapMin => constants[40];
    public int SkewedFractionHorizontalGap => constants[41];
    public int SkewedFractionVerticalGap => constants[42];
    public int OverbarVerticalGap => constants[43];
    public int OverbarRuleThickness => constants[44];
    public int OverbarExtraAscender => constants[45];
    public int UnderbarVerticalGap => constants[46];
    public int UnderbarRuleThickness => constants[47];
    public int UnderbarExtraDescender => constants[48];
    public int RadicalVerticalGap => constants[49];
    public int RadicalDisplayStyleVerticalGap => constants[50];
    public int RadicalRuleThickness => constants[51];
    public int RadicalExtraAscender => constants[52];
    public int RadicalKernBeforeDegree => constants[53];
    public int RadicalKernAfterDegree => constants[54];
    public int RadicalDegreeBottomRaisePercent => constants[55];

    /// <summary>The overlap stretchy assemblies keep between connected parts.</summary>
    public int MinConnectorOverlap => variants != 0 ? U16(variants) : 0;

    public int ItalicsCorrection(uint glyph) => italics.GetValueOrDefault(glyph);

    /// <summary>Where an accent above the glyph centers (from its origin), or null for the middle of its advance.</summary>
    public int? TopAccentAttachment(uint glyph) => accents.TryGetValue(glyph, out var x) ? x : null;

    /// <summary>The size variants of a glyph growing vertically (or horizontally), smallest first, with their sizes.</summary>
    public IReadOnlyList<(uint Glyph, int Size)> Variants(uint glyph, bool vertically)
    {
        if (!(vertically ? vertical : horizontal).TryGetValue(glyph, out var construction))
            return [];
        var count = U16(construction + 2);
        var result = new List<(uint, int)>(count);
        for (var i = 0; i < count; i++)
            result.Add(((uint)U16(construction + 4 + i * 4), U16(construction + 6 + i * 4)));
        return result;
    }

    /// <summary>The parts a glyph is assembled from once its variants are not big enough, bottom (or left) first.</summary>
    public IReadOnlyList<GlyphPart> Assembly(uint glyph, bool vertically)
    {
        if (!(vertically ? vertical : horizontal).TryGetValue(glyph, out var construction))
            return [];
        var offset = U16(construction);
        if (offset == 0)
            return [];
        var assembly = construction + offset;
        var count = U16(assembly + 4);
        var parts = new List<GlyphPart>(count);
        for (var i = 0; i < count; i++)
        {
            var p = assembly + 6 + i * 10;
            parts.Add(new GlyphPart((uint)U16(p), U16(p + 2), U16(p + 4), U16(p + 6), (U16(p + 8) & 1) != 0));
        }
        return parts;
    }

    void ReadValues(int table, Dictionary<uint, int> into)
    {
        var count = U16(table + 2);
        foreach (var (glyph, index) in Coverage(table + U16(table)))
            if (index < count) into[glyph] = I16(table + 4 + index * 4);
    }

    /// <summary>A coverage table's glyphs with their coverage indices.</summary>
    IEnumerable<(uint Glyph, int Index)> Coverage(int table) => Coverage(data, table);

    static IEnumerable<(uint Glyph, int Index)> Coverage(byte[] bytes, int table)
    {
        int U16(int at) => at >= 0 && at + 2 <= bytes.Length ? BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(at)) : 0;
        var format = U16(table);
        if (format == 1)
        {
            var count = U16(table + 2);
            for (var i = 0; i < count; i++)
                yield return ((uint)U16(table + 4 + i * 2), i);
        }
        else if (format == 2)
        {
            var ranges = U16(table + 2);
            for (var r = 0; r < ranges; r++)
            {
                var (start, end, first) = (U16(table + 4 + r * 6), U16(table + 6 + r * 6), U16(table + 8 + r * 6));
                for (var g = start; g <= end; g++)
                    yield return ((uint)g, first + g - start);
            }
        }
    }

    int U16(int at) => at + 2 <= data.Length ? BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(at)) : 0;
    int I16(int at) => at + 2 <= data.Length ? BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(at)) : 0;
}
