using System.Xml.Linq;
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

    /// <summary>
    /// Emboss, imprint, outline (hollow letters) and shadow (w:emboss, w:imprint, w:outline, w:shadow; catalog:
    /// run.decoration), which Word draws as copies of the text.
    /// </summary>
    public bool Emboss { get; init; }
    public bool Imprint { get; init; }
    public bool Hollow { get; init; }
    public bool Shadow { get; init; }

    /// <summary>The border around the run's text (w:bdr) where one shows, or null.</summary>
    public BorderLine? Border { get; init; }

    public bool Hidden { get; init; }
    public RgbColor? Highlight { get; init; }
    public RgbColor? Shading { get; init; }

    /// <summary>How far the baseline is raised (points; negative lowers it): w:position and super/subscript.</summary>
    public double Raise { get; init; }

    /// <summary>
    /// For super- and subscripts the size of the text they belong to and the part of <see cref="Raise"/> they add: they
    /// stand within the line that size makes (measured with docx/local/raised: unlike w:position, they make no line
    /// higher).
    /// </summary>
    public double? ScriptBase { get; init; }
    public double ScriptShift { get; init; }

    /// <summary>Extra space after every character in points (w:spacing).</summary>
    public double Spacing { get; init; }

    /// <summary>Horizontal scale (w:w), 1 = 100 %.</summary>
    public double Scale { get; init; } = 1;

    public bool Caps { get; init; }
    public bool SmallCaps { get; init; }
    public string? Language { get; init; }

    /// <summary>A right-to-left run (w:rtl, or inside a w:dir embedding): its neutral characters read right to left.</summary>
    public bool RightToLeft { get; init; }

    /// <summary>The direction a w:bdo around the run forces on it ("rtl", "ltr"), or null.</summary>
    public string? Override { get; init; }

    /// <summary>
    /// Kerning from the font's kern table between ANSI characters instead of GPOS (documents before Word 2010,
    /// w:useAnsiKerningPairs; catalog: settings.compatibility).
    /// </summary>
    public bool LegacyKerning { get; init; }

    /// <summary>The East Asian emphasis mark of every character (w:em: dot, comma, circle, underDot), or null.</summary>
    public string? Emphasis { get; init; }

    /// <summary>Text effects as DrawingML (catalog: run.textEffects): glow, shadow and reflection, the outline and the fill.</summary>
    public XElement? Effects { get; init; }
    public XElement? Outline { get; init; }
    public XElement? TextFill { get; init; }

    /// <summary>The line metrics Word takes from the font, in points (<see cref="WordFonts.LineMetrics"/>).</summary>
    public (double Above, double Below) Metrics => WordFonts.LineMetrics(Font.Data, Size);
}

/// <summary>What Word takes from fonts (catalog: engine.fonts, engine.lineLayout); every rule measured against Word.</summary>
public static class WordFonts
{
    /// <summary>The ascent of a font's glyphs above the baseline in em, without the line gap (as <see cref="LineMetrics"/> takes it).</summary>
    public static double Ascent(FontData font) =>
        font.EastAsian ? font.WinAscent + 0.15 * (font.WinAscent + font.WinDescent)
        : font.Typo is { Use: true } typo ? typo.Ascender : font.WinAscent;

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
        // East Asian fonts are 1.3 times their win ascent and descent high, the extra shared above and below (measured
        // with docx/local/ea-lines: MS Gothic, MS Mincho, Yu Gothic, SimSun, Malgun Gothic and Microsoft YaHei within
        // 0.07 pt, their hhea line gaps left out).
        if (font.EastAsian)
        {
            var height = font.WinAscent + font.WinDescent;
            return ((font.WinAscent + 0.15 * height) * size, (font.WinDescent + 0.15 * height) * size);
        }
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
    /// <param name="legacy">
    /// A document in a compatibility mode before Word 2010 (14): no OpenType features (ligatures, alternates, number
    /// forms, stylistic sets), kerning only from the font's kern table (w:useAnsiKerningPairs; measured with Word-saved
    /// compatibility copies: Aptos, which has none, sets wider and breaks lines later).
    /// </param>
    public static List<string> Features(RunFormat format, double size, bool legacy = false, bool kernTable = true)
    {
        var features = new List<string>();
        // Kerning before Word 2010 comes from the kern table, which the layout applies itself.
        var kern = format.Kern is { } threshold && threshold > 0 && size >= threshold && !legacy;
        features.Add(kern ? "kern" : "-kern");
        if (legacy)
        {
            features.AddRange(["-liga", "-clig", "-calt", "-hlig", "-dlig"]);
            return features;
        }
        var ligatures = format.Ligatures ?? "none";
        var standard = ligatures is "standard" or "standardContextual" or "standardHistorical" or "standardDiscretional"
            or "standardContextualHistorical" or "standardContextualDiscretional" or "standardHistoricalDiscretional" or "all";
        var contextual = ligatures.Contains("ontextual", StringComparison.Ordinal) || ligatures == "all";
        var historical = ligatures.Contains("istorical", StringComparison.Ordinal) || ligatures == "all";
        var discretional = ligatures.Contains("iscretional", StringComparison.Ordinal) || ligatures == "all";
        features.Add(standard ? "liga" : "-liga");
        features.Add(standard ? "clig" : "-clig");
        features.Add(contextual || format.ContextualAlternates == true ? "calt" : "-calt");
        features.Add(historical ? "hlig" : "-hlig");
        features.Add(discretional ? "dlig" : "-dlig");
        // Number forms and spacing replace the font's default digits; stylistic sets their glyphs.
        switch (format.NumberForm)
        {
            case "lining": features.Add("lnum"); break;
            case "oldStyle": features.Add("onum"); break;
        }
        switch (format.NumberSpacing)
        {
            case "proportional": features.Add("pnum"); break;
            case "tabular": features.Add("tnum"); break;
        }
        foreach (var set in format.StylisticSets ?? [])
            features.Add($"ss{set:00}");
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
