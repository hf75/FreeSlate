using FreeSlate.Drawing;
using FreeSlate.Text;

namespace FreeSlate.Docx.Layout;

/// <summary>
/// Character formatting ready for layout and drawing: the font file, the size in points, the shaping features and
/// the decorations (catalog: run.basic, run.decoration, run.spacing).
/// </summary>
public sealed record RunStyle(
    ResolvedFont Font,
    double Size,
    RgbColor Color,
    IReadOnlyList<string> Features,
    bool Bold,
    bool Italic)
{
    public string? Underline { get; init; }
    public RgbColor? UnderlineColor { get; init; }
    public bool Strike { get; init; }
    public bool DoubleStrike { get; init; }
    public bool Hidden { get; init; }
    public RgbColor? Highlight { get; init; }
    public RgbColor? Shading { get; init; }

    /// <summary>How far the baseline is raised (points; negative lowers it): w:position and super/subscript.</summary>
    public double Raise { get; init; }

    /// <summary>Extra space after every character in points (w:spacing).</summary>
    public double Spacing { get; init; }

    /// <summary>Horizontal scale (w:w), 1 = 100 %.</summary>
    public double Scale { get; init; } = 1;

    public bool Caps { get; init; }
    public bool SmallCaps { get; init; }
    public string? Language { get; init; }

    /// <summary>The line metrics Word takes from the font, in points (<see cref="WordFonts.LineMetrics"/>).</summary>
    public (double Above, double Below) Metrics => WordFonts.LineMetrics(Font.Data, Size);
}

/// <summary>What Word takes from fonts (catalog: engine.fonts, engine.lineLayout); every rule measured against Word.</summary>
public static class WordFonts
{
    /// <summary>
    /// The part of a single-spaced line above and below the baseline in points (measured with 14 fonts, docx/local/lm-*):
    /// the line is as high as the font's hhea ascender, descender and line gap together; the baseline sits below the
    /// line gap and the ascent, both taken as DirectWrite takes them: the OS/2 typo metrics when the font sets
    /// USE_TYPO_METRICS (Aptos, Gabriola, Bahnschrift, Cambria Math), else the win metrics, the line gap then being
    /// what the hhea line spacing has more than they (Arial's and Times New Roman's 0.03 to 0.04 em; Calibri's and
    /// Consolas' hhea gaps are already in their win metrics).
    /// </summary>
    public static (double Above, double Below) LineMetrics(FontData font, double size)
    {
        var (hheaAscent, hheaDescent, hheaGap) = font.Hhea;
        var total = hheaAscent + hheaDescent + hheaGap;
        double ascent, gap;
        if (font.Typo is { Use: true } typo)
        {
            ascent = typo.Ascender;
            gap = typo.LineGap;
            total = typo.Ascender + typo.Descender + typo.LineGap;
        }
        else
        {
            ascent = font.WinAscent;
            gap = Math.Max(0, total - (font.WinAscent + font.WinDescent));
            total = Math.Max(total, font.WinAscent + font.WinDescent);
        }
        var above = (gap + ascent) * size;
        return (above, total * size - above);
    }

    /// <summary>
    /// The OpenType features Word shapes a run with (catalog: run.openType): kerning from GPOS where the run's size
    /// reaches its w:kern threshold (no w:kern, no kerning; measured with docx/local/lm-*), ligatures and contextual
    /// alternates only where w14:ligatures asks for them.
    /// </summary>
    public static List<string> Features(RunFormat format, double size)
    {
        var features = new List<string>();
        features.Add(format.Kern is { } threshold && threshold > 0 && size >= threshold ? "kern" : "-kern");
        var ligatures = format.Ligatures ?? "none";
        var standard = ligatures is "standard" or "standardContextual" or "standardHistorical" or "standardDiscretional"
            or "standardContextualHistorical" or "standardContextualDiscretional" or "standardHistoricalDiscretional" or "all";
        var contextual = ligatures.Contains("ontextual", StringComparison.Ordinal) || ligatures == "all";
        var historical = ligatures.Contains("istorical", StringComparison.Ordinal) || ligatures == "all";
        var discretional = ligatures.Contains("iscretional", StringComparison.Ordinal) || ligatures == "all";
        features.Add(standard ? "liga" : "-liga");
        features.Add(standard ? "clig" : "-clig");
        features.Add(contextual ? "calt" : "-calt");
        features.Add(historical ? "hlig" : "-hlig");
        features.Add(discretional ? "dlig" : "-dlig");
        return features;
    }

    /// <summary>The highlight colors of w:highlight (ST_HighlightColor).</summary>
    public static RgbColor? Highlight(string? name) => name switch
    {
        "yellow" => RgbColor.FromHex("FFFF00"),
        "green" => RgbColor.FromHex("00FF00"),
        "cyan" => RgbColor.FromHex("00FFFF"),
        "magenta" => RgbColor.FromHex("FF00FF"),
        "blue" => RgbColor.FromHex("0000FF"),
        "red" => RgbColor.FromHex("FF0000"),
        "darkBlue" => RgbColor.FromHex("000080"),
        "darkCyan" => RgbColor.FromHex("008080"),
        "darkGreen" => RgbColor.FromHex("008000"),
        "darkMagenta" => RgbColor.FromHex("800080"),
        "darkRed" => RgbColor.FromHex("800000"),
        "darkYellow" => RgbColor.FromHex("808000"),
        "darkGray" => RgbColor.FromHex("808080"),
        "lightGray" => RgbColor.FromHex("C0C0C0"),
        "black" => RgbColor.FromHex("000000"),
        "white" => RgbColor.FromHex("FFFFFF"),
        _ => null,
    };
}
