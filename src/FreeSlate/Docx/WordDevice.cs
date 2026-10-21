namespace FreeSlate.Docx;

/// <summary>
/// Word's 600 dpi device, on which it lays out and prints its pages: a dot is 0.12 pt, a twip five twelfths of one.
/// Each step onto the device is named by the rounding Word takes there; the places that take them say how they were
/// measured.
/// </summary>
public static class WordDevice
{
    /// <summary>A dot of Word's device, in points: it prints at 600 dpi and draws its lines on whole dots.</summary>
    public const double Dot = 0.12;

    /// <summary>The dots of a point.</summary>
    public const double DotsPerPoint = 600.0 / 72;

    /// <summary>A length in dots, unrounded, as the glyphs and tab leaders count it: times 600, over 72.</summary>
    public static double InDots(double points) => points * 600 / 72;

    /// <summary>Dots in points, as the glyphs and tab leaders count them: times 72, over 600.</summary>
    public static double InPoints(double dots) => dots * 72 / 600;

    /// <summary>A length in whole dots, halves away from zero.</summary>
    public static int Dots(double points) => (int)Math.Round(points / Dot, MidpointRounding.AwayFromZero);

    /// <summary>The whole dots a length holds, a hair short of a dot counting as the dot.</summary>
    public static double FloorDots(double points) => Math.Floor(points / Dot + 1e-6);

    /// <summary>A length down to whole dots (see <see cref="FloorDots"/>), in points.</summary>
    public static double Floored(double points) => FloorDots(points) * Dot;

    /// <summary>A line's or a gap's width on the device: down to whole dots, one at least.</summary>
    public static double LineWidth(double points) => Math.Max(Dot, Floored(points));

    /// <summary>A place on the device in whole twips first, so that halves stay halves, then in dots, halves to even.</summary>
    public static double TwipsOnDots(double points) => Math.Round(Math.Round(points * 20) * 5 / 12, MidpointRounding.ToEven) * Dot;

    /// <summary>
    /// Where a text box's text starts on the device, from its edge and left <paramref name="inset"/>: on a dot, a half
    /// dot going down with a left inset of 7.2 pt or more, up with a narrower one, to even without one (measured with
    /// docx/local/tbd-*, tbe-*: a box at 1887.5 dots has its text with left insets of 144 and 288 twips on dots 1947 and
    /// 2007, in modes 11 to 15, with 12 and 72 twips on 1893 and 1918, without on 1888; LibreOffice's
    /// extras-ooxmlexport/tdf91260: 1947.5 dots on 1947; tbi-1417: insets of nothing but half an unfilled line beside a
    /// box at 1417 twips, 592.5 dots on 593; extras-layout/tdf117188; boxes at 472.5 dots without an inset on 472:
    /// extras-ooxmlexport/tdf153613_textboxAfterPgBreak3, writerfilter-dmapper/textbox-textline). The edge and inset in
    /// whole twips, halves down: half an unfilled line of 0.25 or 0.75 pt counts 2 or 7 twips, of 0.255 pt 3 (tbi-1417,
    /// tbi-1440: the text on dots 591 and 593 beside a box at 1417 twips; LibreOffice's extras-ooxmlexport/tdf104354).
    /// </summary>
    public static double BoxTextOnDots(double points, double inset)
    {
        var dots = Math.Ceiling(points * 20 - 0.5 - 1e-6) * 5 / 12;
        var half = Math.Abs(dots - Math.Floor(dots) - 0.5) < 1e-6;
        return (!half ? Math.Round(dots) : inset >= 7.2 - 1e-6 ? Math.Floor(dots) : inset > 1e-6 ? Math.Ceiling(dots) : Math.Round(dots, MidpointRounding.ToEven)) * Dot;
    }

    /// <summary>The font's size in device pixels (its em in dots), halves up.</summary>
    public static int PixelSize(double size) => (int)Math.Floor(InDots(size) + 0.5);
}
