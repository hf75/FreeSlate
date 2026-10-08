using System.Globalization;
using System.Net;
using System.Text;
using System.Xml.Linq;
using FreeSlate.Drawing;
using FreeSlate.Drawing.Geometry;
using FreeSlate.Html;
using FreeSlate.Text;

namespace FreeSlate.Pptx;

/// <summary>Where a shape's text properties come from, in priority order after the paragraph/run itself.</summary>
public sealed class TextChain
{
    /// <summary>The shape, then the layout and master placeholders it inherits from.</summary>
    public required List<PartElement> Shapes { get; init; }

    /// <summary>"title", "body" or "other": which master text style applies.</summary>
    public required string StyleKind { get; init; }

    /// <summary>The shape's p:style/a:fontRef (default text color and theme font).</summary>
    public XElement? FontRef { get; init; }
}

/// <summary>
/// Renders a text body: resolves the text style cascade (catalog: text.styles), lays the text out with our own
/// engine (catalog: engine.textLayout) and emits every line as SVG text on its exact baseline.
/// </summary>
public sealed class TextRenderer(SlideContext slide, FontResolver fonts)
{
    const double PxPerPt = 96.0 / 72;
    static readonly string[] FillNames = ["noFill", "solidFill", "gradFill", "blipFill", "pattFill", "grpFill"];

    abstract record Source;
    sealed record ListStyleSource(XElement ListStyle) : Source;
    sealed record FontRefSource(XElement FontRef) : Source;

    /// <summary>a:tcTxStyle of a table style part: b/i as on/off, a font or font reference and a color.</summary>
    sealed record TableTextSource(XElement Style) : Source;

    /// <summary>SVG markup for the text of a shape (shape-local pixel coordinates), or "".</summary>
    public string Render(TextChain chain, EvaluatedGeometry geometry, double boxWidth, bool unflip)
    {
        var txBody = TxBody(chain.Shapes[0].Element);
        if (txBody == null || !txBody.Elements(Ns.A + "p").Any(p => p.Elements().Any(e => e.Name.LocalName is "r" or "fld")))
            return "";

        var bodies = chain.Shapes.Select(s => TxBody(s.Element)?.Element(Ns.A + "bodyPr")).ToList();
        string? Body(string attribute) => bodies.Select(b => b?.Attribute(attribute)?.Value).FirstOrDefault(v => v != null);
        double Inset(string attribute, long fallback) => (long.TryParse(Body(attribute), out var v) ? v : fallback) / Units.EmuPerPoint;

        var autofit = bodies.Select(b => b?.Elements().FirstOrDefault(e => e.Name.LocalName is "noAutofit" or "normAutofit" or "spAutoFit"))
            .FirstOrDefault(e => e != null);
        var fontScale = autofit?.Name.LocalName == "normAutofit" ? (autofit.Long("fontScale") ?? 100000) / 100000.0 : 1;
        var spacingReduction = autofit?.Name.LocalName == "normAutofit" ? (autofit.Long("lnSpcReduction") ?? 0) / 100000.0 : 0;

        var sources = BuildSources(chain);
        var left = geometry.TextLeft / PxPerPt + Inset("lIns", 91440);
        var top = geometry.TextTop / PxPerPt + Inset("tIns", 45720);
        var width = (geometry.TextRight - geometry.TextLeft) / PxPerPt - Inset("lIns", 91440) - Inset("rIns", 91440);
        var height = (geometry.TextBottom - geometry.TextTop) / PxPerPt - Inset("tIns", 45720) - Inset("bIns", 45720);
        var layout = Layout(txBody, sources, Math.Max(0, width), Body("wrap") != "none", fontScale, spacingReduction);
        return Emit(layout, left, top, height, Body("anchor") ?? "t", unflip ? boxWidth : null);
    }

    /// <summary>Lays out the text of a table cell; <paramref name="tableStyles"/> are the cell's a:tcTxStyle elements, highest priority first.</summary>
    public LaidOutText LayoutCell(XElement txBody, IReadOnlyList<XElement> tableStyles, double width)
    {
        var sources = new List<Source>();
        if (txBody.Element(Ns.A + "lstStyle") is { } own) sources.Add(new ListStyleSource(own));
        sources.AddRange(tableStyles.Select(t => new TableTextSource(t)));
        if (slide.Master?.Root?.Element(Ns.P + "txStyles")?.Element(Ns.P + "otherStyle") is { } other) sources.Add(new ListStyleSource(other));
        if (slide.Presentation.DefaultTextStyle is { } defaults) sources.Add(new ListStyleSource(defaults));
        return Layout(txBody, sources, Math.Max(0, width), wrap: true, 1, 0);
    }

    /// <summary>SVG for a laid-out text block placed in a box (points); anchor t, ctr or b.</summary>
    public string Emit(LaidOutText layout, double left, double top, double height, string anchor, double? unflipWidth = null)
    {
        var offset = anchor switch
        {
            "ctr" => (height - layout.Height) / 2,
            "b" => height - layout.Height,
            _ => 0,
        };

        var svg = new StringBuilder();
        var defs = new StringBuilder();
        var filters = new Dictionary<XElement, string?>();
        svg.Append(unflipWidth is { } w ? $"<g transform=\"matrix(-1 0 0 1 {Units.Css(w)} 0)\">" : "<g>");
        foreach (var line in layout.Lines)
        {
            var baseline = top + offset + line.Baseline;
            foreach (var run in line.Runs)
            {
                if (run.Style.Highlight is { } highlight)
                {
                    svg.Append($"<rect x=\"{Px(left + run.X)}\" y=\"{Px(baseline - line.Ascent)}\" width=\"{Px(run.Width)}\" height=\"{Px(line.Ascent + line.Descent)}\" fill=\"{highlight.ToHex()}\"/>");
                }
                if (string.IsNullOrWhiteSpace(run.Text))
                    continue;
                var filter = run.Style.Effects is { } effects
                    ? filters.TryGetValue(effects, out var known) ? known : filters[effects] = SvgEffects.Filter(effects, slide.Colors, 0, 0, defs, boundingBox: true)
                    : null;
                svg.Append(Text(run, left + run.X, baseline - run.Style.BaselineShift * run.Style.Size, filter));
            }
        }
        svg.Append("</g>");
        return defs.Length > 0 ? $"<defs>{defs}</defs>{svg}" : svg.ToString();
    }

    /// <summary>
    /// Character formatting of chart text from a:defRPr-like elements, highest priority first (catalog: chart.text);
    /// the last layer should carry the chart's default size.
    /// </summary>
    public TextStyle ChartStyle(IEnumerable<XElement?> layers) => Style(null, ChartSources(layers), 0, 1, null);

    /// <summary>Lays out chart rich text (c:rich): the paragraph's own a:defRPr, the body's list style, then <paramref name="layers"/>.</summary>
    public LaidOutText ChartRich(XElement rich, IEnumerable<XElement?> layers, double width, bool wrap)
    {
        var shared = new List<Source>();
        if (rich.Element(Ns.A + "lstStyle") is { } own) shared.Add(new ListStyleSource(own));
        shared.AddRange(ChartSources(layers));
        var counters = new int[9];
        var paragraphs = rich.Elements(Ns.A + "p").Select(p =>
        {
            var sources = ChartSources([p.Element(Ns.A + "pPr")?.Element(Ns.A + "defRPr")]);
            sources.AddRange(shared);
            return Paragraph(p, sources, 1, 0, counters);
        }).ToList();
        return TextLayout.Layout(paragraphs, Math.Max(0, width), wrap);
    }

    static List<Source> ChartSources(IEnumerable<XElement?> layers) =>
        layers.Where(l => l != null)
            .Select(l => (Source)new ListStyleSource(new XElement(Ns.A + "lstStyle",
                new XElement(Ns.A + "lvl1pPr", new XElement(Ns.A + "defRPr", l!.Attributes(), l.Elements())))))
            .ToList();

    /// <summary>A single-style label with its baseline origin at (x, baseline) in points.</summary>
    public string Label(string value, TextStyle style, double x, double baseline) =>
        string.IsNullOrWhiteSpace(value) ? "" : Text(new PlacedRun(value, style, x, 0), x, baseline);

    /// <summary>The runs of a laid-out line with the line's left edge at <paramref name="left"/> and its baseline at <paramref name="baseline"/>.</summary>
    public string EmitLine(LaidOutLine line, double left, double baseline)
    {
        var svg = new StringBuilder();
        foreach (var run in line.Runs)
        {
            if (!string.IsNullOrWhiteSpace(run.Text))
                svg.Append(Text(run, left + run.X, baseline - run.Style.BaselineShift * run.Style.Size));
        }
        return svg.ToString();
    }

    LaidOutText Layout(XElement txBody, List<Source> sources, double width, bool wrap, double fontScale, double spacingReduction)
    {
        var counters = new int[9];
        var paragraphs = txBody.Elements(Ns.A + "p")
            .Select(p => Paragraph(p, sources, fontScale, spacingReduction, counters))
            .ToList();
        return TextLayout.Layout(paragraphs, width, wrap);
    }

    static string Px(double points) => Units.Css(points * PxPerPt);

    string Text(PlacedRun run, double x, double y, string? filter = null)
    {
        var style = run.Style;
        var family = style.Font.Data.Family;
        slide.Presentation.UsedFonts.Add(family);

        // PowerPoint snaps every glyph origin to whole device pixels (measured with the calibration deck).
        var text = run.Style.AllCaps ? run.Text.ToUpperInvariant() : run.Text;
        var advances = style.Font.Data.Advances(text, style.Kerning);
        var xs = new StringBuilder();
        var position = x;
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsLowSurrogate(text[i]))
                continue;
            if (xs.Length > 0) xs.Append(' ');
            xs.Append(Units.Css(position * PxPerPt));
            position += advances[i] * style.Size + style.LetterSpacing;
        }

        var sb = new StringBuilder($"<text x=\"{xs}\" y=\"{Px(y)}\" font-family=\"'{WebUtility.HtmlEncode(family.Replace("'", "\\'"))}'\" font-size=\"{Px(style.Size)}\"");
        if (style.Bold) sb.Append(" font-weight=\"700\"");
        if (style.Italic) sb.Append(" font-style=\"italic\"");
        sb.Append($" fill=\"{style.Color.ToHex()}\"");
        if (style.Color.A < 0.999) sb.Append($" fill-opacity=\"{Units.Css4(style.Color.A)}\"");
        var decorations = new List<string>();
        if (style.Underline != null) decorations.Add("underline");
        if (style.Strike != null) decorations.Add("line-through");
        if (decorations.Count > 0) sb.Append($" text-decoration=\"{string.Join(' ', decorations)}\"");
        if (!style.Kerning) sb.Append(" style=\"font-kerning:none\"");
        if (filter != null) sb.Append($" filter=\"url(#{filter})\"");
        sb.Append('>').Append(WebUtility.HtmlEncode(text)).Append("</text>");
        return sb.ToString();
    }

    static XElement? TxBody(XElement shape) => shape.Element(Ns.P + "txBody") ?? shape.Element(Ns.A + "txBody");

    List<Source> BuildSources(TextChain chain)
    {
        var sources = new List<Source>();
        void AddList(XElement? list) { if (list != null) sources.Add(new ListStyleSource(list)); }

        AddList(TxBody(chain.Shapes[0].Element)?.Element(Ns.A + "lstStyle"));
        if (chain.FontRef != null)
            sources.Add(new FontRefSource(chain.FontRef));
        foreach (var inherited in chain.Shapes.Skip(1))
            AddList(TxBody(inherited.Element)?.Element(Ns.A + "lstStyle"));

        var txStyles = slide.Master?.Root?.Element(Ns.P + "txStyles");
        AddList(txStyles?.Element(Ns.P + chain.StyleKind + "Style"));
        AddList(slide.Presentation.DefaultTextStyle);
        return sources;
    }

    static XElement? Level(Source source, int level) =>
        source is ListStyleSource l ? l.ListStyle.Element(Ns.A + $"lvl{level + 1}pPr") : null;

    static string? ParagraphAttr(XElement? pPr, List<Source> sources, int level, string name) =>
        pPr?.Attribute(name)?.Value ?? sources.Select(s => Level(s, level)?.Attribute(name)?.Value).FirstOrDefault(v => v != null);

    static XElement? ParagraphElement(XElement? pPr, List<Source> sources, int level, params string[] names)
    {
        XElement? Find(XElement? e) => e?.Elements().FirstOrDefault(c => names.Contains(c.Name.LocalName));
        return Find(pPr) ?? sources.Select(s => Find(Level(s, level))).FirstOrDefault(e => e != null);
    }

    static string? RunAttr(XElement? rPr, List<Source> sources, int level, string name)
    {
        if (rPr?.Attribute(name)?.Value is { } own)
            return own;
        foreach (var source in sources)
        {
            if (source is TableTextSource table)
            {
                if (name is "b" or "i" && table.Style.Attribute(name)?.Value is { } flag && flag != "def")
                    return flag == "on" ? "1" : "0";
                continue;
            }
            if (Level(source, level)?.Element(Ns.A + "defRPr")?.Attribute(name)?.Value is { } inherited)
                return inherited;
        }
        return null;
    }

    static XElement? RunElement(XElement? rPr, List<Source> sources, int level, params string[] names)
    {
        XElement? Find(XElement? e) => e?.Elements().FirstOrDefault(c => names.Contains(c.Name.LocalName));
        if (Find(rPr) is { } own)
            return own;
        foreach (var source in sources)
        {
            if (source is TableTextSource table)
            {
                var color = table.Style.Elements().FirstOrDefault(e => e.Name.LocalName.EndsWith("Clr", StringComparison.Ordinal))
                            ?? table.Style.Element(Ns.A + "fontRef")?.Elements().FirstOrDefault();
                if (names.Contains("solidFill") && color != null)
                    return new XElement(Ns.A + "solidFill", color);
                if (names.Contains("latin"))
                {
                    if (table.Style.Element(Ns.A + "font")?.Element(Ns.A + "latin") is { } latin)
                        return latin;
                    if (table.Style.Element(Ns.A + "fontRef")?.Str("idx") is "minor" or "major")
                        return new XElement(Ns.A + "latin", new XAttribute("typeface", table.Style.Element(Ns.A + "fontRef")!.Str("idx") == "major" ? "+mj-lt" : "+mn-lt"));
                }
                continue;
            }
            if (source is FontRefSource fontRef)
            {
                if (names.Contains("solidFill") && fontRef.FontRef.Elements().Any())
                    return new XElement(Ns.A + "solidFill", fontRef.FontRef.Elements());
                if (names.Contains("latin") && fontRef.FontRef.Str("idx") is "minor" or "major")
                    return new XElement(Ns.A + "latin", new XAttribute("typeface", fontRef.FontRef.Str("idx") == "major" ? "+mj-lt" : "+mn-lt"));
                continue;
            }
            if (Find(Level(source, level)?.Element(Ns.A + "defRPr")) is { } inherited)
                return inherited;
        }
        return null;
    }

    ParagraphInput Paragraph(XElement p, List<Source> sources, double fontScale, double spacingReduction, int[] counters)
    {
        var pPr = p.Element(Ns.A + "pPr");
        var level = Math.Clamp(pPr.Int("lvl") ?? 0, 0, 8);
        string? P(string name) => ParagraphAttr(pPr, sources, level, name);
        static double Points(string? emu) => long.TryParse(emu, out var v) ? v / Units.EmuPerPoint : 0;

        var runs = new List<TextRun>();
        foreach (var element in p.Elements())
        {
            switch (element.Name.LocalName)
            {
                case "r":
                    var t = element.Element(Ns.A + "t")?.Value ?? "";
                    runs.Add(new TextRun(t, Style(element.Element(Ns.A + "rPr"), sources, level, fontScale, t)));
                    break;
                case "fld":
                    var text = FieldText(element);
                    runs.Add(new TextRun(text, Style(element.Element(Ns.A + "rPr"), sources, level, fontScale, text)));
                    break;
                case "br":
                    runs.Add(new TextRun("\n", Style(element.Element(Ns.A + "rPr"), sources, level, fontScale, null)));
                    break;
            }
        }
        var endStyle = Style(p.Element(Ns.A + "endParaRPr"), sources, level, fontScale, null);
        var hasText = runs.Any(r => r.Text.Length > 0 && r.Text != "\n");

        var lineSpacing = ParagraphElement(pPr, sources, level, "lnSpc");
        var spacing = lineSpacing?.Element(Ns.A + "spcPts") is { } pts
            ? new LineSpacing(true, (pts.Long("val") ?? 0) / 100.0)
            : new LineSpacing(false, Math.Max(0.01, (lineSpacing?.Element(Ns.A + "spcPct").Long("val") ?? 100000) / 100000.0 - spacingReduction));

        return new ParagraphInput
        {
            Runs = runs,
            EndStyle = endStyle,
            Align = P("algn") ?? "l",
            MarginLeft = Points(P("marL")),
            MarginRight = Points(P("marR")),
            Indent = Points(P("indent")),
            Spacing = spacing,
            SpaceBefore = Space(ParagraphElement(pPr, sources, level, "spcBef")),
            SpaceAfter = Space(ParagraphElement(pPr, sources, level, "spcAft")),
            DefaultTabSize = long.TryParse(P("defTabSz"), out var tab) ? tab / Units.EmuPerPoint : 72,
            Bullet = hasText
                ? Bullet(pPr, sources, level, runs.FirstOrDefault(r => r.Text != "\n")?.Style ?? endStyle, counters)
                : ResetBullet(level, counters),
        };
    }

    /// <summary>
    /// The text of a field (catalog: text.fields): slide numbers and dates are computed when rendering, like
    /// PowerPoint does when it shows the slide; other fields keep their stored text.
    /// </summary>
    string FieldText(XElement field)
    {
        var type = field.Str("type") ?? "";
        var stored = field.Element(Ns.A + "t")?.Value ?? "";
        if (type == "slidenum")
            return slide.Number.ToString(CultureInfo.InvariantCulture);
        if (!type.StartsWith("datetime", StringComparison.Ordinal))
            return stored;
        CultureInfo culture;
        try { culture = CultureInfo.GetCultureInfo(field.Element(Ns.A + "rPr").Str("lang") ?? "en-US"); }
        catch (CultureNotFoundException) { culture = CultureInfo.GetCultureInfo("en-US"); }
        var f = culture.DateTimeFormat;
        var twelveHours = f.ShortTimePattern.Contains('t');
        var time = twelveHours ? "h:mm tt" : "HH:mm";
        var seconds = twelveHours ? "h:mm:ss tt" : "HH:mm:ss";
        var dayMonth = culture.TwoLetterISOLanguageName == "en" ? "d MMMM yyyy" : f.LongDatePattern.Replace("dddd, ", "").Replace("dddd ", "");
        var format = type switch
        {
            "datetime2" => f.LongDatePattern,
            "datetime3" => dayMonth,
            "datetime4" => culture.TwoLetterISOLanguageName == "en" ? "MMMM d, yyyy" : dayMonth,
            "datetime5" => culture.TwoLetterISOLanguageName == "en" ? "dd-MMM-yy" : f.ShortDatePattern.Replace("yyyy", "yy").Replace("MM", "MMM"),
            "datetime6" => "MMMM yy",
            "datetime7" => "MMM-yy",
            "datetime8" => f.ShortDatePattern + " " + time,
            "datetime9" => f.ShortDatePattern + " " + seconds,
            "datetime10" => "HH:mm",
            "datetime11" => "HH:mm:ss",
            "datetime12" => "h:mm tt",
            "datetime13" => "h:mm:ss tt",
            _ => f.ShortDatePattern,
        };
        return DateTime.Now.ToString(format, culture);
    }

    static Bullet? ResetBullet(int level, int[] counters)
    {
        counters[level] = 0;
        return null;
    }

    static ParagraphSpace Space(XElement? spacing) =>
        spacing?.Element(Ns.A + "spcPts") is { } pts ? new ParagraphSpace(false, (pts.Long("val") ?? 0) / 100.0)
        : spacing?.Element(Ns.A + "spcPct") is { } pct ? new ParagraphSpace(true, (pct.Long("val") ?? 0) / 100000.0)
        : ParagraphSpace.None;

    Bullet? Bullet(XElement? pPr, List<Source> sources, int level, TextStyle textStyle, int[] counters)
    {
        for (var i = level + 1; i < counters.Length; i++)
            counters[i] = 0;
        var kind = ParagraphElement(pPr, sources, level, "buNone", "buChar", "buAutoNum", "buBlip");
        string text;
        switch (kind?.Name.LocalName)
        {
            case "buChar":
                text = kind.Str("char") ?? "•";
                counters[level] = 0;
                break;
            case "buAutoNum":
                counters[level] = counters[level] == 0 ? kind.Int("startAt") ?? 1 : counters[level] + 1;
                text = AutoNumber(kind.Str("type") ?? "arabicPeriod", counters[level]);
                break;
            default:
                counters[level] = 0;
                return null;
        }

        var font = ParagraphElement(pPr, sources, level, "buFontTx", "buFont");
        var resolved = font?.Name.LocalName == "buFont" && slide.Theme.ResolveTypeface(font.Str("typeface") ?? "") is { Length: > 0 } family
            ? fonts.Resolve(family, textStyle.Bold, textStyle.Italic)
            : textStyle.Font;

        var sizeElement = ParagraphElement(pPr, sources, level, "buSzTx", "buSzPct", "buSzPts");
        var size = sizeElement?.Name.LocalName switch
        {
            "buSzPct" => textStyle.Size * (sizeElement.Long("val") ?? 100000) / 100000.0,
            "buSzPts" => (sizeElement.Long("val") ?? 1800) / 100.0,
            _ => textStyle.Size,
        };

        var colorElement = ParagraphElement(pPr, sources, level, "buClrTx", "buClr");
        var color = colorElement?.Name.LocalName == "buClr" ? slide.Colors.ResolveChild(colorElement) ?? textStyle.Color : textStyle.Color;
        return new Bullet(text, new TextStyle(resolved, size, color, Kerning: false));
    }

    TextStyle Style(XElement? rPr, List<Source> sources, int level, double fontScale, string? text)
    {
        string? R(string name) => RunAttr(rPr, sources, level, name);
        var size = (double.TryParse(R("sz"), out var sz) ? sz / 100 : 18) * fontScale;
        var bold = R("b") is "1" or "true";
        var italic = R("i") is "1" or "true";

        var family = Typeface(RunElement(rPr, sources, level, "latin")) ?? slide.Theme.MinorLatin;
        if (text != null && text.Any(LineBreaker.IsIdeographic) && Typeface(RunElement(rPr, sources, level, "ea")) is { } eastAsian)
            family = eastAsian;

        // kern: kerning applies from this size (hundredths of a point) up; without it PowerPoint does not kern.
        var kern = double.TryParse(R("kern"), out var k) ? k / 100 : double.MaxValue;
        return new TextStyle(
            fonts.Resolve(family, bold, italic),
            size,
            RunColor(rPr, sources, level),
            bold,
            italic,
            size >= kern,
            int.TryParse(R("spc"), out var spc) ? spc / 100.0 : 0,
            R("u") is { } u && u != "none" ? u : null,
            R("strike") is "sngStrike" or "dblStrike" ? R("strike") : null,
            int.TryParse(R("baseline"), out var b) ? b / 100000.0 : 0,
            RunElement(rPr, sources, level, "highlight") is { } h ? slide.Colors.ResolveChild(h) : null,
            R("cap") == "all",
            RunElement(rPr, sources, level, "effectLst"));
    }

    RgbColor RunColor(XElement? rPr, List<Source> sources, int level)
    {
        var fill = RunElement(rPr, sources, level, FillNames);
        return fill?.Name.LocalName switch
        {
            "solidFill" => slide.Colors.ResolveChild(fill) ?? RgbColor.Black,
            "noFill" => RgbColor.Black.WithAlpha(0),
            "gradFill" => slide.Colors.ResolveChild(fill.Element(Ns.A + "gsLst")?.Element(Ns.A + "gs")) ?? RgbColor.Black,
            _ => slide.Colors.Resolve(new XElement(Ns.A + "schemeClr", new XAttribute("val", "tx1"))),
        };
    }

    string? Typeface(XElement? font) =>
        font?.Str("typeface") is { Length: > 0 } t ? slide.Theme.ResolveTypeface(t) is { Length: > 0 } r ? r : null : null;

    static string AutoNumber(string scheme, int n)
    {
        var value = scheme switch
        {
            _ when scheme.StartsWith("alphaLc", StringComparison.Ordinal) => Alpha(n).ToLowerInvariant(),
            _ when scheme.StartsWith("alphaUc", StringComparison.Ordinal) => Alpha(n),
            _ when scheme.StartsWith("romanLc", StringComparison.Ordinal) => Roman(n).ToLowerInvariant(),
            _ when scheme.StartsWith("romanUc", StringComparison.Ordinal) => Roman(n),
            _ => n.ToString(CultureInfo.InvariantCulture),
        };
        return scheme switch
        {
            _ when scheme.EndsWith("ParenBoth", StringComparison.Ordinal) => $"({value})",
            _ when scheme.EndsWith("ParenR", StringComparison.Ordinal) => $"{value})",
            _ when scheme.EndsWith("Plain", StringComparison.Ordinal) => value,
            _ => $"{value}.",
        };
    }

    static string Alpha(int n)
    {
        var s = "";
        for (n = Math.Max(1, n); n > 0; n = (n - 1) / 26)
            s = (char)('A' + (n - 1) % 26) + s;
        return s;
    }

    static string Roman(int n)
    {
        (int Value, string Symbol)[] table = [(1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"), (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")];
        var sb = new StringBuilder();
        foreach (var (value, symbol) in table)
        {
            while (n >= value) { sb.Append(symbol); n -= value; }
        }
        return sb.ToString();
    }
}
