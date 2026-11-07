namespace FreeSlate.Fonts;

/// <summary>
/// A face's metrics as its font file states them, in its units: the hhea line metrics, the OS/2 typographic ones and whether
/// the font uses them (USE_TYPO_METRICS), its win ascent and descent, the post table's underline (its position negative below
/// the baseline), the OS/2 strikeout, subscript and superscript, cap height and average width.
/// </summary>
public sealed record FontMetrics(int UnitsPerEm, int HheaAscender, int HheaDescender, int HheaLineGap, int TypoAscender, int TypoDescender,
    int TypoLineGap, bool UseTypoMetrics, int WinAscent, int WinDescent, int UnderlinePosition, int UnderlineThickness, int StrikeoutPosition,
    int StrikeoutSize, int SubscriptSize, int SubscriptOffset, int SuperscriptSize, int SuperscriptOffset, int CapHeight, int AverageWidth);

/// <summary>
/// Free fonts made to the widths of Office's (metric twins): Carlito to Calibri's, Caladea to Cambria's, Gelasio to Georgia's,
/// Arimo to Arial's, Tinos to Times New Roman's and Cousine to Courier New's. Where the machine lacks the original, its twin stands
/// in for it with the original's metrics (catalog: engine.fonts). Their glyphs are as wide (compared with Windows 11's fonts, every
/// advance of Latin-1, Latin Extended-A and the common punctuation): Carlito, Arimo, Tinos and Cousine of Google Fonts alike;
/// Caladea of 2013 (version 1.002, Chrome OS's extra fonts) within half a unit of its 1000 against Cambria's 2048, not Google
/// Fonts' later Caladea, 1.4 to 4.4 % narrower with proportional figures; Gelasio alike but for µ, the cedilla, Ĳ, ľ, Ŀ, Ŋ and
/// ď, and in italics Ó, Ö, Ø and the fractions. Their other metrics are not all alike: Gelasio makes lines 12 % higher than
/// Georgia's and Caladea 2 % lower than Cambria's, Arimo has taller win metrics than Arial, and underlines, strikeouts, subscripts
/// and superscripts sit elsewhere. Those are the original's here, as Windows 11's fonts state them; kerning stays the twin's own.
/// A document of the six families in four styles and five sizes from 9 to 24 pt so lays out alike, line for line, its characters
/// within 0.37 pt (Caladea's units) and 0.22 pt (Gelasio's italic Ö) of where the originals put them.
/// </summary>
public static class MetricTwins
{
    /// <summary>The twin of an Office family, or null.</summary>
    public static string? Of(string family) => Originals.TryGetValue(family, out var original) ? original.Twin : null;

    /// <summary>The metrics of an Office family's face in this style, which its twin takes when it stands in for that face.</summary>
    public static FontMetrics Metrics(string family, bool bold, bool italic) => Originals[family].Faces[(bold ? 1 : 0) + (italic ? 2 : 0)];

    /// <summary>The originals, their twins and their faces' metrics: regular, bold, italic, bold italic.</summary>
    static readonly Dictionary<string, (string Twin, FontMetrics[] Faces)> Originals = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Calibri"] = ("Carlito",
        [
            new(2048, 1536, -512, 452, 1536, -512, 452, false, 1950, 550, -232, 134, 512, 134, 1331, 286, 1331, 976, 1294, 1067),
            new(2048, 1536, -512, 452, 1536, -512, 452, false, 1950, 550, -232, 134, 512, 186, 1331, 286, 1331, 976, 1294, 1098),
            new(2048, 1536, -512, 452, 1536, -512, 452, false, 1950, 550, -232, 134, 512, 134, 1331, 286, 1331, 976, 1297, 1067),
            new(2048, 1536, -512, 452, 1536, -512, 452, false, 1950, 550, -232, 134, 512, 186, 1331, 286, 1331, 976, 1294, 1099),
        ]),
        ["Cambria"] = ("Caladea",
        [
            new(2048, 1946, -455, 0, 1593, -455, 353, false, 1946, 455, -125, 116, 510, 102, 1331, 156, 1331, 503, 1365, 1260),
            new(2048, 1946, -455, 0, 1593, -455, 353, false, 1946, 455, -105, 156, 510, 102, 1331, 156, 1331, 503, 1365, 1228),
            new(2048, 1946, -455, 0, 1593, -455, 353, false, 1946, 455, -125, 116, 510, 102, 1331, 156, 1331, 503, 1365, 1111),
            new(2048, 1946, -455, 0, 1593, -455, 353, false, 1946, 455, -105, 156, 510, 102, 1331, 156, 1331, 503, 1365, 1198),
        ]),
        ["Georgia"] = ("Gelasio",
        [
            new(2048, 1878, -449, 0, 1549, -444, 198, false, 1878, 449, -181, 101, 594, 101, 1331, 155, 1331, 542, 1419, 901),
            new(2048, 1878, -449, 0, 1549, -444, 198, false, 1878, 449, -180, 122, 618, 122, 1331, 155, 1331, 542, 1419, 1051),
            new(2048, 1878, -449, 0, 1549, -444, 198, false, 1878, 449, -183, 96, 589, 96, 1331, 155, 1331, 542, 1419, 920),
            new(2048, 1878, -449, 0, 1549, -444, 198, false, 1878, 449, -181, 120, 618, 120, 1331, 155, 1331, 542, 1419, 1073),
        ]),
        ["Arial"] = ("Arimo",
        [
            new(2048, 1854, -434, 67, 1491, -431, 307, false, 1854, 434, -217, 150, 530, 102, 1331, 283, 1331, 977, 1467, 904),
            new(2048, 1854, -434, 67, 1491, -431, 307, false, 1854, 434, -217, 215, 530, 102, 1331, 283, 1331, 977, 1466, 980),
            new(2048, 1854, -434, 67, 1491, -425, 307, false, 1854, 434, -217, 150, 530, 102, 1331, 283, 1331, 977, 1466, 904),
            new(2048, 1854, -434, 67, 1491, -431, 307, false, 1854, 434, -217, 215, 530, 102, 1331, 283, 1331, 977, 1465, 980),
        ]),
        ["Times New Roman"] = ("Tinos",
        [
            new(2048, 1825, -443, 87, 1420, -442, 307, false, 1825, 443, -223, 100, 530, 102, 1331, 293, 1331, 928, 1356, 821),
            new(2048, 1825, -443, 87, 1387, -442, 307, false, 1825, 443, -223, 195, 530, 102, 1331, 293, 1331, 928, 1356, 874),
            new(2048, 1825, -443, 87, 1422, -442, 307, false, 1825, 443, -223, 100, 530, 102, 1331, 293, 1331, 928, 1356, 823),
            new(2048, 1825, -443, 87, 1387, -442, 307, false, 1825, 443, -223, 195, 530, 102, 1331, 293, 1331, 928, 1356, 844),
        ]),
        ["Courier New"] = ("Cousine",
        [
            new(2048, 1705, -615, 0, 1255, -386, 0, false, 1705, 615, -477, 84, 530, 102, 1331, 285, 1331, 865, 1170, 1229),
            new(2048, 1705, -615, 0, 1297, -428, 0, false, 1705, 615, -477, 205, 530, 102, 1331, 285, 1331, 865, 1212, 1229),
            new(2048, 1705, -615, 0, 1255, -386, 0, false, 1705, 615, -477, 84, 530, 102, 1331, 285, 1331, 865, 1170, 1229),
            new(2048, 1705, -615, 0, 1297, -428, 0, false, 1705, 615, -477, 205, 530, 102, 1331, 285, 1331, 865, 1212, 1229),
        ]),
    };
}
