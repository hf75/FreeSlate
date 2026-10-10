using System.Globalization;
using System.Xml.Linq;
using FreeSlate.Drawing;
using FreeSlate.Opc;

namespace FreeSlate.Pptx.Charts;

/// <summary>
/// The look of charts without a chart style part (catalog: chart.formatting): Office 2007 and 2010 drew them by one of 48
/// built-in styles (c:style, c14:style; 2 where there is none), made of the theme's fill and line styles. Measured in
/// PowerPoint with charts of every style, series count and theme (also themes whose styles can be told apart): style N
/// has the color variant (N − 1) % 8 (0 grays, 1 the accents in turn, 2–7 shades of accent 1–6) and the level
/// (N − 1) / 8: 0 flat colors (fill style 1), 1 white edges, 2 fill style 3 and effect style 2, 3 fill style 3, effect
/// style 3 and wider lines, 4 dark edges, a tinted plot area and a bordered chart area, 5 fill style 3 and effect style 3
/// in a black chart area with white text. Charts that Office 2010 or later saved without c14:style (and those of other
/// programs) take the plain variant of their style: black axis and grid lines, the full accent for plot areas of level 4
/// and black edges where the grays have dark gray.
/// </summary>
public sealed partial class ChartRenderer
{
    /// <summary>The Office 2007 style of a chart without style part; null for charts with one.</summary>
    LegacyStyle? legacy;

    sealed record LegacyStyle(int Number, bool Plain)
    {
        /// <summary>0 grays, 1 the six accents in turn, 2–7 one accent (1–6) from dark to light.</summary>
        public int Variant => (Number - 1) % 8;

        public int Level => (Number - 1) / 8;

        /// <summary>The theme fill style of data points: 3 at levels 2, 3 and 5, else 1.</summary>
        public int FillStyle => Level is 2 or 3 or 5 ? 3 : 1;

        /// <summary>The width of automatic series lines in widths of the theme's line style 1.</summary>
        public int LineFactor => Level switch { 0 => 3, 3 => 7, _ => 5 };
    }

    /// <summary>The tints of tx1 for the six grays of variant 0 (measured: 616161, B3B3B3 … on black text).</summary>
    static readonly int[] LegacyGrays = [88000, 55000, 78000, 92000, 70000, 30000];

    /// <summary>
    /// The style of a chart without style part: c:style (1–48, else 2), in full where Office 2010 marked it with
    /// c14:style or Office 2007 saved the presentation (AppVersion 12), else plain.
    /// </summary>
    static LegacyStyle Legacy(XElement chartSpace, int? appVersion)
    {
        var style = chartSpace.Element(Ns.C + "style");
        var number = style.Int("val") is { } value and >= 1 and <= 48 ? value : 2;
        var office2010 = style?.Attribute(MarkupCompatibility.Annotation + "c14") != null;
        return new LegacyStyle(number, Plain: !office2010 && !(appVersion < 14));
    }

    static XElement Scheme(string name, params (string Transform, int Value)[] transforms) =>
        new(Ns.A + "schemeClr", new XAttribute("val", name),
            transforms.Select(t => new XElement(Ns.A + t.Transform, new XAttribute("val", t.Value.ToString(CultureInfo.InvariantCulture)))));

    /// <summary>
    /// The automatic color of series or point <paramref name="index"/> of <paramref name="count"/>: the variant's base
    /// color, shaded in the first and tinted in the last of c cycles through the base colors, by 1.4 / (c + 1) per cycle
    /// from the middle in linear light. The gray and accent variants cycle through their six colors (c = n / 6 + 1, so
    /// that six series are already shaded), the one-accent variants give every series a cycle (c = n).
    /// </summary>
    RgbColor LegacyColor(int index, int count)
    {
        var style = legacy!;
        index = Math.Max(0, index);
        var color = style.Variant switch
        {
            0 => Scheme("tx1", ("tint", LegacyGrays[index % 6])),
            1 => Scheme($"accent{index % 6 + 1}"),
            var v => Scheme($"accent{v - 1}"),
        };
        var (cycles, cycle) = style.Variant >= 2 ? (Math.Max(1, count), index) : (Math.Max(0, count) / 6 + 1, index / 6);
        var offset = cycle - (cycles - 1) / 2.0;
        var factor = Math.Clamp(1 - Math.Abs(offset) * 1.4 / (cycles + 1), 0, 1);
        if (offset != 0)
            color.Add(new XElement(Ns.A + (offset < 0 ? "shade" : "tint"), new XAttribute("val", ((int)Math.Round(factor * 100000)).ToString(CultureInfo.InvariantCulture))));
        return colors.Resolve(color);
    }

    int AllSeriesCount => model.AllSeries.Count();

    /// <summary>The fill of a data point in a legacy style: the level's theme fill style in the point's color.</summary>
    Fill LegacyFill(RgbColor color) =>
        colors.Theme.FillStyle(legacy!.FillStyle) is { } style ? slide.ThemedFill(style, colors.WithPlaceholder(color)) : new SolidFill(color);

    /// <summary>The theme's line style 1 in a color, as the lowest layer of a line.</summary>
    (XElement Line, ColorContext Colors) ThemeLine(XElement color)
    {
        var line = colors.Theme.LineStyle(1) ?? new XElement(Ns.A + "ln", new XAttribute("w", "9525"), new XElement(Ns.A + "solidFill", Scheme("phClr")));
        return (line, colors.WithPlaceholder(colors.Resolve(color)));
    }

    /// <summary>
    /// The automatic edge of bars, areas, slices and bubbles in a legacy style: line style 1 in white (bg1) at level 1
    /// and dark at level 4 (the point's color shaded 50 %, the accent's in one-accent variants, tx1 tinted 92 % in the
    /// grays, plain: tx1); none at the other levels.
    /// </summary>
    List<(XElement Line, ColorContext Colors)> LegacyEdge(ChartGroup group, ChartSeries series, int point)
    {
        var style = legacy!;
        if (style.Level == 1)
            return [ThemeLine(Scheme("bg1"))];
        if (style.Level != 4)
            return [];
        var color = style.Variant switch
        {
            0 => style.Plain ? Scheme("tx1") : Scheme("tx1", ("tint", 92000)),
            1 => Rgb(AutoPointColor(group, series, point), ("shade", 50000)),
            var v => Scheme($"accent{v - 1}", ("shade", 50000)),
        };
        return [ThemeLine(color)];
    }

    static XElement Rgb(RgbColor color, params (string Transform, int Value)[] transforms) =>
        new(Ns.A + "srgbClr", new XAttribute("val", color.ToHex().TrimStart('#')),
            transforms.Select(t => new XElement(Ns.A + t.Transform, new XAttribute("val", t.Value.ToString(CultureInfo.InvariantCulture)))));

    /// <summary>
    /// The automatic line of a line, scatter or radar series in a legacy style: the series color, as wide as the theme's
    /// line style 1 times 3 (level 0), 7 (level 3) or 5, with round caps and joins; level 0 keeps the line style's dash.
    /// </summary>
    XElement LegacySeriesLine(RgbColor color)
    {
        var themed = colors.Theme.LineStyle(1);
        var dash = legacy!.Level == 0 ? themed?.Element(Ns.A + "prstDash") ?? themed?.Element(Ns.A + "custDash") : null;
        return new XElement(Ns.A + "ln", new XAttribute("w", ((long)Math.Round(LegacyLineWidth)).ToString(CultureInfo.InvariantCulture)), new XAttribute("cap", "rnd"),
            new XElement(Ns.A + "solidFill", Rgb(color)),
            dash != null ? new XElement(dash) : new XElement(Ns.A + "prstDash", new XAttribute("val", "solid")),
            new XElement(Ns.A + "round"));
    }

    /// <summary>The width of automatic series lines in EMU.</summary>
    double LegacyLineWidth => legacy!.LineFactor * (colors.Theme.LineStyle(1).Long("w") ?? 9525);

    /// <summary>
    /// The automatic marker size: twice the width of the series line (its own or the style's) in points plus 3, rounded
    /// down (measured: 7 at 2.25 pt, 19 at an 8 pt line of the series' own).
    /// </summary>
    double LegacyMarkerSize(Outline? seriesLine) => Math.Floor(2 * (seriesLine?.WidthEmu ?? LegacyLineWidth) / 12700 + 3 + 1e-9);

    /// <summary>
    /// The automatic edge of a marker in a legacy style: line style 1 (its width and dash) in the color of the series line
    /// at every level (measured: not white at level 1, not dark at level 4).
    /// </summary>
    (XElement Line, ColorContext Colors) LegacyMarkerEdge(RgbColor color) => ThemeLine(Rgb(color));

    /// <summary>Axis, tick and grid lines in a legacy style: line style 1 in tx1 tinted 75 % (plain: tx1).</summary>
    (XElement Line, ColorContext Colors) LegacyAxisLine() =>
        ThemeLine(legacy!.Plain ? Scheme("tx1") : Scheme("tx1", ("tint", 75000)));

    /// <summary>
    /// The chart area in a legacy style: bg1, at level 4 with a border of line style 1 in tx1 tinted 75 %, at level 5 tx1.
    /// </summary>
    (Fill Fill, List<(XElement Line, ColorContext Colors)> Line) LegacyChartArea()
    {
        var style = legacy!;
        var fill = new SolidFill(colors.Resolve(Scheme(style.Level == 5 ? "tx1" : "bg1")));
        return (fill, style.Level == 4 ? [ThemeLine(Scheme("tx1", ("tint", 75000)))] : []);
    }

    /// <summary>
    /// The plot area of an axis chart in a legacy style: at level 4 tx1 tinted 20 % (one-accent variants: the accent
    /// tinted 20 %, plain: the accent), at level 5 tx1 tinted 95 %; none at the other levels and for pies.
    /// </summary>
    Fill LegacyPlotArea()
    {
        var style = legacy!;
        return style.Level switch
        {
            4 => new SolidFill(colors.Resolve(style.Variant >= 2
                ? style.Plain ? Scheme($"accent{style.Variant - 1}") : Scheme($"accent{style.Variant - 1}", ("tint", 20000))
                : Scheme("tx1", ("tint", 20000)))),
            5 => new SolidFill(colors.Resolve(Scheme("tx1", ("tint", 95000)))),
            _ => Fill.None,
        };
    }

    readonly Dictionary<ChartSeries, string?> effects = [];

    /// <summary>
    /// The filter of the effects on a series' bars, slices, areas, bubbles and markers (not on its line): the series' own
    /// a:effectLst, else in a legacy style the theme's effect style 2 (level 2) or 3 (levels 3 and 5); null without any.
    /// </summary>
    string? SeriesEffects(ChartSeries series)
    {
        if (effects.TryGetValue(series, out var id))
            return id;
        var themed = legacy?.Level switch { 2 => 2, 3 or 5 => 3, _ => 0 };
        var effectLst = series.SpPr?.Element(Ns.A + "effectLst")
                        ?? (themed >= 1 && themed <= colors.Theme.EffectStyles.Count ? colors.Theme.EffectStyles[themed - 1].Element(Ns.A + "effectLst") : null);
        id = Html.SvgEffects.Filter(effectLst, colors, width * PxPerPt, height * PxPerPt, defs);
        effects[series] = id;
        return id;
    }

    /// <summary>A data point's shape (bar, slice, area, bubble, marker) as a part, drawn with the effects of its series.</summary>
    string ShapePart(ChartSeries series, int point, string markup) =>
        Part(series, point, markup.Length > 0 && SeriesEffects(series) is { } filter ? $"<g filter=\"url(#{filter})\">{markup}</g>" : markup);

    /// <summary>The default text run of an element: 10 pt (titles 18 pt bold), in bg1 on the black chart area of level 5.</summary>
    XElement LegacyText(string element, double size)
    {
        var run = new XElement(Ns.A + "defRPr", new XAttribute("sz", ((int)Math.Round(size * 100)).ToString(CultureInfo.InvariantCulture)));
        if (element == "title")
            run.SetAttributeValue("b", "1");
        else if (legacy!.Level == 5)
            run.Add(new XElement(Ns.A + "solidFill", Scheme("bg1")));
        return run;
    }

    /// <summary>
    /// A title's own text in an Office 2007 style: paragraphs without properties (a:pPr) show 18 pt regular whatever the
    /// chart's text size, those with them (as Office writes them) the title's bold (measured).
    /// </summary>
    static XElement LegacyTitleText(XElement rich)
    {
        if (rich.Elements(Ns.A + "p").All(p => p.Element(Ns.A + "pPr") != null))
            return rich;
        rich = new XElement(rich);
        foreach (var p in rich.Elements(Ns.A + "p").Where(p => p.Element(Ns.A + "pPr") == null))
            p.AddFirst(new XElement(Ns.A + "pPr", new XElement(Ns.A + "defRPr", new XAttribute("sz", "1800"), new XAttribute("b", "0"))));
        return rich;
    }
}
