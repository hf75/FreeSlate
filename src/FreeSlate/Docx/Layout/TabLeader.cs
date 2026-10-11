namespace FreeSlate.Docx.Layout;

/// <summary>
/// The characters of a tab's leader (catalog: paragraph.tabs): Word repeats the leader character at its advance rounded
/// to its 600 dpi device, on a grid from the page's left edge, every one that fits whole between the tab's start and
/// its end (measured with docx/generated/tabs: the dots of Aptos 12 pt stand every 3.48 pt, not 3.43 pt, the first at
/// 28 × 3.48 pt; hyphens every 4.08 pt, underscores every 5.52 pt).
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
        var glyphs = style.Font.Data.GlyphsWithFeatures(c.ToString(), style.Features, language: style.Language);
        var advance = glyphs.Sum(g => g.Advance) * style.Size * style.Scale;
        var device = Math.Round(advance * 600 / 72) * 72 / 600;
        if (device <= 0)
            return positions;
        var end = start + tab.Width;
        for (var k = Math.Ceiling(start / device - 1e-6); (k + 1) * device <= end + 0.01; k++)
            positions.Add(k * device);
        return positions;
    }
}
