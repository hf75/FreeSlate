namespace FreeSlate.Docx.Layout;

/// <summary>
/// The characters of a tab's leader (catalog: paragraph.tabs): Word repeats the leader character at its advance rounded
/// to its 600 dpi device, on a grid from the page's left edge, every one that fits whole between the tab's start and
/// its end (measured with docx/generated/tabs: the dots of Aptos 12 pt stand every 3.48 pt, not 3.43 pt, the first at
/// 28 × 3.48 pt; hyphens every 4.08 pt, underscores every 5.52 pt). Bold of a font without a bold face steps a dot
/// further (measured with docx/local/sbl-a, sbl-b: Lucida Sans Unicode and Lucida Console bold from 8 to 36 pt, dots,
/// hyphens, underscores and middle dots each one dot wider than their natural width, regular ones not).
/// </summary>
public static class TabLeader
{
    public static char? Character(string? leader) => leader switch
    {
        "dot" => '.',
        "hyphen" => '-',
        "underscore" or "heavy" => '_',
        "middleDot" => '·',
        _ => null,
    };

    /// <summary>Where the leader's characters stand, in points from the page's left edge.</summary>
    /// <param name="start">The tab's start on the page.</param>
    public static List<double> Positions(Piece tab, double start)
    {
        var positions = new List<double>();
        if (!tab.IsTab || Character(tab.Leader) is not { } c)
            return positions;
        var style = tab.Style;
        var data = style.Font.Data;
        var glyphs = data.GlyphsWithFeatures(c.ToString(), style.Features, language: style.Language);
        var advance = glyphs.Sum(g => g.Advance);
        // On the device: the advance, the tab's start and its end in whole dots (Word's PDF of docx/generated/tabs: the
        // last hyphen ends at 489.6 pt where the tab ends 0.01 pt before it).
        double device = Math.Abs(style.Scale - 1) < 1e-6
            ? DeviceGlyphs.Natural((int)Math.Round(advance * data.UnitsPerEm), data.UnitsPerEm, WordDevice.PixelSize(style.Size))
            : Math.Round(WordDevice.InDots(advance * style.Size * style.Scale));
        if (style.Bold && style.Font.SyntheticBold)
            device++;
        if (device <= 0)
            return positions;
        var (first, end) = (Math.Round(WordDevice.InDots(start)), Math.Round(WordDevice.InDots(start + tab.Width)));
        for (var k = Math.Ceiling(first / device); (k + 1) * device <= end; k++)
            positions.Add(WordDevice.InPoints(k * device));
        return positions;
    }
}
