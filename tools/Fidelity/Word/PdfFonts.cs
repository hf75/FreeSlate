using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Fidelity.Word;

/// <summary>
/// Brings the fonts of the browser's PDF to Word's convention before both are rasterized: Word's PDFs embed TrueType
/// fonts without their gasp table, the browser's keep it, and Windows' PDF renderer follows it, smoothing our glyphs
/// where it grid-fits Word's (Aptos, Cambria and Cambria Math at text sizes: 7–12 % of a line's ink differ with every
/// glyph in place). The gasp table of every embedded TrueType program (FontFile2) is renamed, so that the renderer
/// draws both PDFs' text alike.
/// </summary>
public static partial class PdfFonts
{
    [GeneratedRegex(@"/FontFile2\s+(\d+)\s+0\s+R")]
    private static partial Regex FontFileReference();

    [GeneratedRegex(@"/Length\s+(\d+)")]
    private static partial Regex LengthEntry();

    /// <summary>Renames the gasp tables of the PDF's embedded TrueType fonts in place; returns how many it renamed.</summary>
    public static int HideGasp(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var text = Encoding.Latin1.GetString(bytes);
        var fonts = FontFileReference().Matches(text).Select(m => int.Parse(m.Groups[1].Value)).ToHashSet();
        if (fonts.Count == 0)
            return 0;
        // The classic cross-reference table of a browser's PDF: one section, every object's offset.
        var xref = text.LastIndexOf("\nxref", StringComparison.Ordinal);
        var trailer = text.IndexOf("trailer", xref, StringComparison.Ordinal);
        if (xref < 0 || trailer < 0)
            return 0;
        var rows = text[(xref + 5)..trailer].Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var header = rows[0].Split(' ');
        if (header.Length != 2 || int.Parse(header[0]) != 0)
            return 0;
        var offsets = new SortedDictionary<long, int>();
        for (var k = 1; k < rows.Length; k++)
        {
            var parts = rows[k].Split(' ');
            if (parts.Length == 3 && parts[2] == "n")
                offsets[long.Parse(parts[0])] = k - 1;
        }
        var starts = offsets.Keys.ToList();
        var renamed = 0;
        var output = new MemoryStream();
        output.Write(bytes, 0, (int)starts[0]);
        var newOffsets = new Dictionary<int, long>();
        for (var i = 0; i < starts.Count; i++)
        {
            var start = (int)starts[i];
            var end = i + 1 < starts.Count ? (int)starts[i + 1] : xref + 1;
            var number = offsets[starts[i]];
            newOffsets[number] = output.Position;
            var segment = bytes.AsSpan(start, end - start);
            if (fonts.Contains(number) && Renamed(segment) is { } changed)
            {
                output.Write(changed);
                renamed++;
            }
            else
                output.Write(segment);
        }
        // A new cross-reference table, the trailer as it was.
        var table = new StringBuilder();
        var size = int.Parse(header[1]);
        var xrefOffset = output.Position;
        table.Append($"xref\n0 {size}\n0000000000 65535 f \n");
        for (var n = 1; n < size; n++)
            table.Append(newOffsets.TryGetValue(n, out var o) ? $"{o:D10} 00000 n \n" : "0000000000 65535 f \n");
        var tail = text[trailer..];
        tail = Regex.Replace(tail, @"startxref\s+\d+", $"startxref\n{xrefOffset}");
        output.Write(Encoding.Latin1.GetBytes(table + tail));
        if (renamed > 0)
            File.WriteAllBytes(path, output.ToArray());
        return renamed;
    }

    /// <summary>A font file object with its program's gasp table renamed, or null where it has none or cannot be read.</summary>
    static byte[]? Renamed(ReadOnlySpan<byte> segment)
    {
        var text = Encoding.Latin1.GetString(segment);
        var streamAt = text.IndexOf("stream", StringComparison.Ordinal);
        if (streamAt < 0 || !text[..streamAt].Contains("/FlateDecode", StringComparison.Ordinal) || LengthEntry().Match(text[..streamAt]) is not { Success: true } length)
            return null;
        var dataStart = streamAt + "stream".Length;
        if (dataStart < text.Length && text[dataStart] == '\r')
            dataStart++;
        if (dataStart < text.Length && text[dataStart] == '\n')
            dataStart++;
        var dataLength = int.Parse(length.Groups[1].Value);
        if (dataStart + dataLength > segment.Length)
            return null;
        byte[] program;
        using (var input = new ZLibStream(new MemoryStream(segment.Slice(dataStart, dataLength).ToArray()), CompressionMode.Decompress))
        using (var plain = new MemoryStream())
        {
            input.CopyTo(plain);
            program = plain.ToArray();
        }
        // The table directory: a gasp entry becomes "gasq", which sorts in its place and which no renderer knows.
        if (program.Length < 12)
            return null;
        var tables = (program[4] << 8) | program[5];
        var found = false;
        for (var k = 0; k < tables && 12 + 16 * k + 4 <= program.Length; k++)
        {
            var at = 12 + 16 * k;
            if (program[at] == 'g' && program[at + 1] == 'a' && program[at + 2] == 's' && program[at + 3] == 'p')
            {
                program[at + 3] = (byte)'q';
                found = true;
            }
        }
        if (!found)
            return null;
        byte[] packed;
        using (var compressed = new MemoryStream())
        {
            using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
                zlib.Write(program);
            packed = compressed.ToArray();
        }
        var dictionary = text[..length.Index] + $"/Length {packed.Length}" + text[(length.Index + length.Length)..dataStart];
        var after = text[(dataStart + dataLength)..];
        using var result = new MemoryStream();
        result.Write(Encoding.Latin1.GetBytes(dictionary));
        result.Write(packed);
        result.Write(Encoding.Latin1.GetBytes(after));
        return result.ToArray();
    }
}
