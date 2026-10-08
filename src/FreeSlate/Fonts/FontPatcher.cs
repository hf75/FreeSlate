using System.Buffers.Binary;
using System.Text;

namespace FreeSlate.Fonts;

/// <summary>
/// Adjusts embedded font files so that browsers render them like PowerPoint (catalog: engine.fonts).
/// </summary>
public static class FontPatcher
{
    const ushort GridFit = 0x0001, DoGray = 0x0002, SymmetricGridFit = 0x0004, SymmetricSmoothing = 0x0008;

    /// <summary>
    /// The font with grid fitting switched off at every size (gasp table), keeping the outlines and metrics.
    /// PowerPoint draws text unhinted: measured cap height and x-height match the font's design values exactly,
    /// while Chromium on Windows (DirectWrite) snaps them vertically to whole pixels, which makes glyphs up to
    /// 1 px taller. DirectWrite honours the gasp table, so a font without grid fitting renders like PowerPoint.
    /// Files that are not plain TrueType/OpenType fonts are returned unchanged.
    /// </summary>
    public static byte[] WithoutGridFitting(byte[] font)
    {
        if (font.Length < 12)
            return font;
        var version = BinaryPrimitives.ReadUInt32BigEndian(font);
        if (version is not (0x00010000 or 0x4F54544F or 0x74727565))   // 1.0, 'OTTO', 'true'
            return font;

        var count = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4));
        if (12 + count * 16 > font.Length)
            return font;
        var tables = new List<(string Tag, byte[] Data)>();
        for (var i = 0; i < count; i++)
        {
            var record = 12 + i * 16;
            var tag = Encoding.ASCII.GetString(font, record, 4);
            var offset = (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record + 8));
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record + 12));
            if (offset < 0 || length < 0 || offset + length > font.Length)
                return font;
            tables.Add((tag, font.AsSpan(offset, length).ToArray()));
        }

        const ushort Flags = DoGray | SymmetricSmoothing;
        var gasp = new byte[8];
        BinaryPrimitives.WriteUInt16BigEndian(gasp, 1);              // version 1
        BinaryPrimitives.WriteUInt16BigEndian(gasp.AsSpan(2), 1);    // one range
        BinaryPrimitives.WriteUInt16BigEndian(gasp.AsSpan(4), 0xFFFF);
        BinaryPrimitives.WriteUInt16BigEndian(gasp.AsSpan(6), Flags);
        var index = tables.FindIndex(t => t.Tag == "gasp");
        if (index >= 0)
        {
            var existing = tables[index].Data;
            var ranges = existing.Length >= 4 ? BinaryPrimitives.ReadUInt16BigEndian(existing.AsSpan(2)) : 0;
            if (existing.Length >= 4 + ranges * 4 && ranges > 0)
            {
                for (var r = 0; r < ranges; r++)
                {
                    var at = existing.AsSpan(4 + r * 4 + 2);
                    var flags = BinaryPrimitives.ReadUInt16BigEndian(at);
                    BinaryPrimitives.WriteUInt16BigEndian(at, (ushort)(flags & ~(GridFit | SymmetricGridFit) | Flags));
                }
                BinaryPrimitives.WriteUInt16BigEndian(existing, 1);
                gasp = existing;
            }
            tables[index] = ("gasp", gasp);
        }
        else
        {
            tables.Add(("gasp", gasp));
            tables.Sort((a, b) => string.CompareOrdinal(a.Tag, b.Tag));
        }
        return Write(version, tables);
    }

    /// <summary>Writes an sfnt file with recomputed table checksums and head.checkSumAdjustment.</summary>
    static byte[] Write(uint version, List<(string Tag, byte[] Data)> tables)
    {
        var count = tables.Count;
        var headerLength = 12 + count * 16;
        var total = headerLength + tables.Sum(t => (t.Data.Length + 3) & ~3);
        var output = new byte[total];
        BinaryPrimitives.WriteUInt32BigEndian(output, version);
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(4), (ushort)count);
        var power = 1;
        var log = 0;
        while (power * 2 <= count) { power *= 2; log++; }
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(6), (ushort)(power * 16));
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(8), (ushort)log);
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(10), (ushort)(count * 16 - power * 16));

        var offset = headerLength;
        var headOffset = -1;
        for (var i = 0; i < count; i++)
        {
            var (tag, data) = tables[i];
            if (tag == "head" && data.Length >= 12)
            {
                BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8), 0);   // checkSumAdjustment is computed last
                headOffset = offset;
            }
            var record = 12 + i * 16;
            Encoding.ASCII.GetBytes(tag, output.AsSpan(record, 4));
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(record + 4), Checksum(data));
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(record + 8), (uint)offset);
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(record + 12), (uint)data.Length);
            data.CopyTo(output.AsSpan(offset));
            offset += (data.Length + 3) & ~3;
        }
        if (headOffset >= 0)
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(headOffset + 8), 0xB1B0AFBA - Checksum(output));
        return output;
    }

    static uint Checksum(ReadOnlySpan<byte> data)
    {
        uint sum = 0;
        var i = 0;
        for (; i + 4 <= data.Length; i += 4)
            sum += BinaryPrimitives.ReadUInt32BigEndian(data[i..]);
        if (i < data.Length)
        {
            Span<byte> tail = stackalloc byte[4];
            data[i..].CopyTo(tail);
            sum += BinaryPrimitives.ReadUInt32BigEndian(tail);
        }
        return sum;
    }
}
