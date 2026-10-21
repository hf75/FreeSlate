using static FreeSlate.Docx.WordDevice;

namespace FreeSlate.Docx;

/// <summary>
/// Where Word's PDF puts the glyphs of a line on the page (catalog: engine.output). Word lays the line out on its 600 dpi
/// device: each glyph advances by its natural width in whole dots at the font's pixel size, a dot less or more where the
/// error against the exact advances would pass 1.5 dots after the next glyph; it goes through the line in stretches of
/// 1024 dots, at whose ends it brings the error within half a dot (and at the glyph before the paragraph mark, which
/// moves only the mark). Its PDF writer then encodes those advances per run against the glyphs' widths, writing an
/// adjustment only where the difference reaches 3/1000 em and dropping the adjustment's remainder (measured with
/// Word's PDFs of docx/local/adv-*, probes in five fonts from 8 to 28 pt: all 90 lines and 100 runs exactly; 410,000
/// glyphs of 3,179 Word PDFs: 99 % of the decisions).
/// </summary>
static class DeviceGlyphs
{
    /// <summary>The exact advances as Word adds them up: a hair longer than the font's (600/72 taken as 8.334).</summary>
    const double Scale = 1 + 7.8e-5;

    /// <summary>The error after the next glyph past which a glyph's advance loses or gains a dot.</summary>
    const double Threshold = 1.5;

    /// <summary>The error at the end of a stretch past which its last glyph's advance loses or gains a dot.</summary>
    const double Snap = 0.5;

    /// <summary>How far Word goes through a line before it brings the error back.</summary>
    const int Stretch = 1024;

    /// <summary>
    /// A glyph's natural width in whole dots at the pixel size, as DirectWrite gives it for GDI (its design width
    /// scaled, up from 0.46 of a dot: 99.95 % of Calibri's and Cambria's widths, 99.3 % of Arial's, 98 % of Times New
    /// Roman's).
    /// </summary>
    public static int Natural(int design, int unitsPerEm, int pixelSize) => (int)Math.Floor((double)design * pixelSize / unitsPerEm + 0.54);

    /// <summary>
    /// The places of a stretch of glyphs on the device, in whole dots from where the stretch starts: one per glyph and
    /// where the last one ends.
    /// </summary>
    /// <param name="natural">Each glyph's natural width in dots.</param>
    /// <param name="nominal">Each glyph's exact advance in dots.</param>
    /// <param name="startError">How far the stretch starts right of its exact place, in dots (the rounding of its start).</param>
    public static int[] Place(IReadOnlyList<int> natural, IReadOnlyList<double> nominal, double startError)
    {
        var count = natural.Count;
        var places = new int[count + 1];
        var (at, exact, reference, stretchStart) = (0, 0.0, -startError, 0);
        for (var k = 0; k < count; k++)
        {
            var end = at + natural[k];
            var exactEnd = (exact + nominal[k]) * Scale;
            var error = end - exactEnd - reference;
            // The next glyph decides: the error it would leave (the last glyph looks ahead with itself).
            var (nextNatural, nextNominal) = k + 1 < count ? (natural[k + 1], nominal[k + 1]) : (natural[k], nominal[k]);
            var ahead = error + nextNatural - nextNominal * Scale;
            if (at + natural[k] + nextNatural - stretchStart > Stretch)
            {
                if (error > Snap)
                    end--;
                else if (error < -Snap)
                    end++;
                reference = end - exactEnd;
                stretchStart = end;
            }
            else if (ahead > Threshold)
                end--;
            else if (ahead < -Threshold)
                end++;
            places[k] = at;
            at = end;
            exact += nominal[k];
        }
        places[count] = at;
        return places;
    }

    /// <summary>
    /// Where the glyphs of a run stand in Word's PDF, in dots from its first glyph: the advances on the device written
    /// against the glyphs' widths, as character spacing where the first seven glyphs need what the first one needs
    /// (rounded to three digits) within 0.01 pt, and otherwise as adjustments, each written once the difference reaches
    /// 3/1000 em, rounded and its remainder dropped (the spacing measured with 14,496 runs of Word's PDFs of
    /// LibreOffice's extras-ooxmlexport documents: 99.8 % used or left as by this rule).
    /// </summary>
    /// <param name="advances">The device advance of every glyph but the last, in dots.</param>
    /// <param name="widths">The PDF width of every glyph but the last, in points.</param>
    /// <param name="size">The font size in the PDF, in points.</param>
    public static double[] Encode(IReadOnlyList<int> advances, IReadOnlyList<double> widths, double size)
    {
        var count = advances.Count;
        var places = new double[count + 1];
        var spacing = 0.0;
        if (count > 0 && Math.Round(advances[0] * Dot - widths[0], 6) is var needed && needed != 0)
        {
            spacing = ThreeDigits(needed);
            for (var k = 0; k < Math.Min(count, 7); k++)
            {
                if (Math.Abs(widths[k] + spacing - advances[k] * Dot) >= 0.01)
                {
                    spacing = 0;
                    break;
                }
            }
        }
        var pending = 0.0;
        for (var k = 0; k < count; k++)
        {
            pending += (widths[k] + spacing - advances[k] * Dot) * 1000 / size;
            var written = 0;
            if (Math.Abs(pending) >= 3)
            {
                written = (int)Math.Truncate(pending + 0.5);
                pending = 0;
            }
            places[k + 1] = places[k] + (widths[k] + spacing - written * size / 1000) / Dot;
        }
        return places;
    }

    /// <summary>A number rounded to three significant digits.</summary>
    static double ThreeDigits(double value)
    {
        var scale = Math.Pow(10, 2 - (int)Math.Floor(Math.Log10(Math.Abs(value))));
        return Math.Round(value * scale, MidpointRounding.ToEven) / scale;
    }
}
