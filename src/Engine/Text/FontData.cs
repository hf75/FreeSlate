using System.Buffers.Binary;
using System.Collections.Concurrent;
using HarfBuzzSharp;
using Buffer = HarfBuzzSharp.Buffer;

namespace Engine.Text;

/// <summary>
/// A loaded font file: the vertical metrics PowerPoint's line model needs and a HarfBuzz font for shaping.
/// Advances are measured unhinted in font units, like a browser with subpixel positioning.
/// </summary>
public sealed class FontData : IDisposable
{
    static readonly Feature[] NoLigatures = Parse("-liga", "-clig");
    static readonly Feature[] NoLigaturesNoKerning = Parse("-liga", "-clig", "-kern");

    readonly Blob blob;
    readonly Face face;
    readonly Font font;
    readonly ConcurrentDictionary<(string, bool), double> widths = new();

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
        if (tables.TryGetValue("OS/2", out var os2))
        {
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

    /// <summary>Advance width of <paramref name="text"/> in em (multiply by the font size).</summary>
    public double Width(string text, bool kerning)
    {
        if (text.Length == 0)
            return 0;
        return widths.GetOrAdd((text, kerning), key =>
        {
            using var buffer = new Buffer();
            buffer.AddUtf16(key.Item1);
            buffer.GuessSegmentProperties();
            font.Shape(buffer, key.Item2 ? NoLigatures : NoLigaturesNoKerning);
            long sum = 0;
            foreach (var position in buffer.GlyphPositions)
                sum += position.XAdvance;
            return (double)sum / UnitsPerEm;
        });
    }

    /// <summary>Advance of every UTF-16 code unit of <paramref name="text"/> in em; a cluster's advance goes to its first unit.</summary>
    public double[] Advances(string text, bool kerning)
    {
        var result = new double[text.Length];
        if (text.Length == 0)
            return result;
        using var buffer = new Buffer();
        buffer.AddUtf16(text);
        buffer.GuessSegmentProperties();
        font.Shape(buffer, kerning ? NoLigatures : NoLigaturesNoKerning);
        var infos = buffer.GlyphInfos;
        var positions = buffer.GlyphPositions;
        for (var i = 0; i < infos.Length; i++)
        {
            var cluster = (int)infos[i].Cluster;
            if (cluster < result.Length)
                result[cluster] += (double)positions[i].XAdvance / UnitsPerEm;
        }
        return result;
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
