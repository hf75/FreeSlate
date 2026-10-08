using System.Buffers.Binary;
using System.Collections.Concurrent;
using HarfBuzzSharp;
using Buffer = HarfBuzzSharp.Buffer;

namespace FreeSlate.Text;

/// <summary>
/// A loaded font file: the vertical metrics PowerPoint's line model needs and a HarfBuzz font for shaping.
/// Advances are measured unhinted in font units. Kerning comes from the legacy 'kern' table only, as in
/// PowerPoint (GDI kerning pairs): GPOS-only pairs are not kerned (measured with the text-snap deck).
/// </summary>
public sealed class FontData : IDisposable
{
    static readonly Feature[] NoLigaturesNoKerning = Parse("-liga", "-clig", "-kern");

    readonly Blob blob;
    readonly Face face;
    readonly Font font;
    readonly ConcurrentDictionary<(string, bool), double> widths = new();
    readonly ConcurrentDictionary<(string, double, bool, double), double> layoutWidths = new();
    readonly Dictionary<uint, short>? kerningPairs;

    public FontData(string path, string family)
    {
        Path = path;
        Family = family;
        var data = File.ReadAllBytes(path);
        var offset = data.Length > 12 && data[0] == 't' && data[1] == 't' && data[2] == 'c' && data[3] == 'f'
            ? (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(12))
            : 0;

        var tables = new Dictionary<string, int>();
        var count = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 4));
        for (var i = 0; i < count; i++)
        {
            var record = offset + 12 + i * 16;
            tables[System.Text.Encoding.ASCII.GetString(data, record, 4)] = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(record + 8));
        }

        UnitsPerEm = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(tables["head"] + 18));
        double ascent, descent;
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
        }
        else
        {
            ascent = BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(tables["hhea"] + 4));
            descent = -BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(tables["hhea"] + 6));
        }
        AscentRatio = ascent + descent > 0 ? ascent / (ascent + descent) : 0.8;
        UnitsPerEm = UnitsPerEm == 0 ? 1000 : UnitsPerEm;
        Ascent = ascent / UnitsPerEm;
        Descent = descent / UnitsPerEm;
        WinAscent = winAscent / UnitsPerEm;
        WinDescent = winDescent / UnitsPerEm;

        kerningPairs = tables.TryGetValue("kern", out var kernOffset) ? ReadKerning(data, kernOffset) : null;

        blob = Blob.FromFile(path);
        face = new Face(blob, 0);
        font = new Font(face);
        font.SetScale(UnitsPerEm, UnitsPerEm);
    }

    public string Path { get; }

    /// <summary>The family name as used in CSS.</summary>
    public string Family { get; }

    public int UnitsPerEm { get; }

    /// <summary>
    /// Share of the line height above the baseline at single spacing: ascent / (ascent + descent), with the typo
    /// metrics if the font sets USE_TYPO_METRICS, else the Windows metrics (measured against PowerPoint).
    /// </summary>
    public double AscentRatio { get; }

    /// <summary>Ascent and descent in em from the same metrics as <see cref="AscentRatio"/> (chart text boxes use them).</summary>
    public double Ascent { get; }

    public double Descent { get; }

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
        if (text.Length == 0)
            return result;
        using var buffer = new Buffer();
        buffer.AddUtf16(text);
        buffer.GuessSegmentProperties();
        font.Shape(buffer, NoLigaturesNoKerning);
        var infos = buffer.GlyphInfos;
        var positions = buffer.GlyphPositions;
        for (var i = 0; i < infos.Length; i++)
        {
            var cluster = (int)infos[i].Cluster;
            if (cluster >= result.Length)
                continue;
            result[cluster].Advance += (double)positions[i].XAdvance / UnitsPerEm;
            if (kerning && kerningPairs != null && i + 1 < infos.Length
                && kerningPairs.TryGetValue(infos[i].Codepoint << 16 | infos[i + 1].Codepoint & 0xFFFF, out var value))
                result[cluster].Kerning += (double)value / UnitsPerEm;
        }
        return result;
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
