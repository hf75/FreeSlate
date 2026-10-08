using System.Net;
using System.Text;
using System.Xml.Linq;
using Engine.Drawing;
using Engine.Drawing.Geometry;

namespace Engine.Pptx;

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
/// Renders a text body as HTML positioned in the shape's text rectangle, resolving the text style cascade
/// (catalog: text.styles). Line breaking is still left to the browser; our own layout engine replaces this later.
/// </summary>
public sealed class TextRenderer(SlideContext slide)
{
    const double LineHeight = 1.2;   // single line spacing relative to the font size

    static readonly string[] FillNames = ["noFill", "solidFill", "gradFill", "blipFill", "pattFill", "grpFill"];

    abstract record Source;
    sealed record ListStyleSource(XElement ListStyle) : Source;
    sealed record FontRefSource(XElement FontRef) : Source;

    public string Render(TextChain chain, EvaluatedGeometry geometry, bool unflip)
    {
        var txBody = TxBody(chain.Shapes[0].Element);
        if (txBody == null || !txBody.Elements(Ns.A + "p").Any(HasContent))
            return "";

        var bodies = chain.Shapes.Select(s => TxBody(s.Element)?.Element(Ns.A + "bodyPr")).ToList();
        string? Body(string attribute) => bodies.Select(b => b?.Attribute(attribute)?.Value).FirstOrDefault(v => v != null);
        double Inset(string attribute, long fallback) => Units.ToPx(long.TryParse(Body(attribute), out var v) ? v : fallback);

        var autofit = bodies.Select(b => b?.Elements().FirstOrDefault(e => e.Name.LocalName is "noAutofit" or "normAutofit" or "spAutoFit"))
            .FirstOrDefault(e => e != null);
        var fontScale = autofit?.Name.LocalName == "normAutofit" ? (autofit.Long("fontScale") ?? 100000) / 100000.0 : 1;
        var spacingReduction = autofit?.Name.LocalName == "normAutofit" ? (autofit.Long("lnSpcReduction") ?? 0) / 100000.0 : 0;

        var sources = BuildSources(chain);
        var html = new StringBuilder();
        var counters = new int[9];

        var first = true;
        foreach (var p in txBody.Elements(Ns.A + "p"))
        {
            html.Append(Paragraph(p, sources, fontScale, spacingReduction, counters, first));
            first = false;
        }

        var left = geometry.TextLeft + Inset("lIns", 91440);
        var top = geometry.TextTop + Inset("tIns", 45720);
        var width = geometry.TextRight - geometry.TextLeft - Inset("lIns", 91440) - Inset("rIns", 91440);
        var height = geometry.TextBottom - geometry.TextTop - Inset("tIns", 45720) - Inset("bIns", 45720);
        var justify = Body("anchor") switch { "ctr" => "center", "b" => "flex-end", _ => "flex-start" };

        var style = new StringBuilder($"left:{Units.Css(left)}px;top:{Units.Css(top)}px;width:{Units.Css(Math.Max(0, width))}px;height:{Units.Css(Math.Max(0, height))}px");
        if (justify != "flex-start") style.Append($";justify-content:{justify}");
        if (Body("wrap") == "none") style.Append(";white-space:pre");
        if (unflip) style.Append(";transform:scaleX(-1)");
        return $"<div class=\"tx\" style=\"{style}\">{html}</div>";
    }

    static XElement? TxBody(XElement shape) => shape.Element(Ns.P + "txBody") ?? shape.Element(Ns.A + "txBody");

    static bool HasContent(XElement p) => p.Elements().Any(e => e.Name.LocalName is "r" or "fld" or "br");

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

    static string? RunAttr(XElement? rPr, List<Source> sources, int level, string name) =>
        rPr?.Attribute(name)?.Value ?? sources.Select(s => Level(s, level)?.Element(Ns.A + "defRPr")?.Attribute(name)?.Value).FirstOrDefault(v => v != null);

    static XElement? RunElement(XElement? rPr, List<Source> sources, int level, params string[] names)
    {
        XElement? Find(XElement? e) => e?.Elements().FirstOrDefault(c => names.Contains(c.Name.LocalName));
        if (Find(rPr) is { } own)
            return own;
        foreach (var source in sources)
        {
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

    string Paragraph(XElement p, List<Source> sources, double fontScale, double spacingReduction, int[] counters, bool first)
    {
        var pPr = p.Element(Ns.A + "pPr");
        var level = Math.Clamp(pPr.Int("lvl") ?? 0, 0, 8);
        var runs = p.Elements().Where(e => e.Name.LocalName is "r" or "fld" or "br").ToList();
        var firstProps = runs.FirstOrDefault(r => r.Name.LocalName != "br")?.Element(Ns.A + "rPr") ?? p.Element(Ns.A + "endParaRPr");
        var size = RunSize(firstProps, sources, level, fontScale);

        string? P(string name) => ParagraphAttr(pPr, sources, level, name);
        static double Emu(string? s) => long.TryParse(s, out var v) ? Units.ToPx(v) : 0;

        var style = new StringBuilder();
        style.Append("text-align:").Append(P("algn") switch
        {
            "ctr" => "center",
            "r" => "right",
            "just" or "justLow" => "justify",
            "dist" or "thaiDist" => "justify;text-align-last:justify",
            _ => "left",
        }).Append(';');
        var marL = Emu(P("marL"));
        var indent = Emu(P("indent"));
        if (marL != 0) style.Append($"padding-left:{Units.Css(marL)}px;");
        if (Emu(P("marR")) is var marR and not 0) style.Append($"padding-right:{Units.Css(marR)}px;");
        if (indent != 0) style.Append($"text-indent:{Units.Css(indent)}px;");

        var lineSpacing = ParagraphElement(pPr, sources, level, "lnSpc");
        if (lineSpacing?.Element(Ns.A + "spcPts") is { } pts)
            style.Append($"line-height:{Units.Css((pts.Long("val") ?? 0) / 100.0)}pt;");
        else
        {
            var pct = (lineSpacing?.Element(Ns.A + "spcPct").Long("val") ?? 100000) / 100000.0;
            style.Append($"line-height:{Units.Css4(Math.Max(0.1, pct - spacingReduction) * LineHeight)};");
        }
        // PowerPoint ignores the space before the first paragraph of a text body.
        if (!first && Spacing(ParagraphElement(pPr, sources, level, "spcBef"), size) is var before and > 0) style.Append($"margin-top:{Units.Css(before)}pt;");
        if (Spacing(ParagraphElement(pPr, sources, level, "spcAft"), size) is var after and > 0) style.Append($"margin-bottom:{Units.Css(after)}pt;");
        style.Append($"font-size:{Units.Css(size)}pt");

        var content = new StringBuilder();
        var hasText = runs.Any(r => r.Name.LocalName != "br" && (r.Element(Ns.A + "t")?.Value.Length ?? 0) > 0);
        if (hasText)
            content.Append(Bullet(pPr, sources, level, firstProps, size, indent, counters));
        else
            counters[level] = 0;
        for (var i = level + 1; i < counters.Length; i++)
            counters[i] = 0;

        foreach (var run in runs)
        {
            if (run.Name.LocalName == "br")
            {
                content.Append("<br>");
                continue;
            }
            var text = run.Name.LocalName == "fld" && run.Str("type") == "slidenum"
                ? slide.Number.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : run.Element(Ns.A + "t")?.Value ?? "";
            content.Append($"<span style=\"{RunStyle(run.Element(Ns.A + "rPr"), sources, level, fontScale)}\">{WebUtility.HtmlEncode(text)}</span>");
        }
        if (!hasText)
            content.Append($"<span style=\"{RunStyle(p.Element(Ns.A + "endParaRPr"), sources, level, fontScale)}\">&#8203;</span>");

        return $"<p style=\"{style}\">{content}</p>";
    }

    static double Spacing(XElement? spacing, double fontSize) =>
        spacing?.Element(Ns.A + "spcPts") is { } pts ? (pts.Long("val") ?? 0) / 100.0
        : spacing?.Element(Ns.A + "spcPct") is { } pct ? (pct.Long("val") ?? 0) / 100000.0 * fontSize * LineHeight
        : 0;

    static double RunSize(XElement? rPr, List<Source> sources, int level, double fontScale) =>
        (double.TryParse(RunAttr(rPr, sources, level, "sz"), out var sz) ? sz / 100 : 18) * fontScale;

    string Bullet(XElement? pPr, List<Source> sources, int level, XElement? firstRun, double size, double indent, int[] counters)
    {
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
                return "";
        }

        // line-height:0 keeps the bullet's font metrics out of the line box, as in PowerPoint.
        var style = new StringBuilder("display:inline-block;text-indent:0;line-height:0;");
        if (indent < 0) style.Append($"width:{Units.Css(-indent)}px;");
        else style.Append("padding-right:0.3em;");

        var font = ParagraphElement(pPr, sources, level, "buFontTx", "buFont");
        var family = font?.Name.LocalName == "buFont" ? slide.Theme.ResolveTypeface(font.Str("typeface") ?? "") : null;
        family ??= Typeface(RunElement(firstRun, sources, level, "latin"));
        if (!string.IsNullOrEmpty(family))
        {
            style.Append($"font-family:{CssFamily(family)};");
            slide.Presentation.UsedFonts.Add(family);
        }

        var sizeElement = ParagraphElement(pPr, sources, level, "buSzTx", "buSzPct", "buSzPts");
        var bulletSize = sizeElement?.Name.LocalName switch
        {
            "buSzPct" => size * (sizeElement.Long("val") ?? 100000) / 100000.0,
            "buSzPts" => (sizeElement.Long("val") ?? 1800) / 100.0,
            _ => size,
        };
        style.Append($"font-size:{Units.Css(bulletSize)}pt;");

        var color = ParagraphElement(pPr, sources, level, "buClrTx", "buClr");
        var resolved = color?.Name.LocalName == "buClr" ? slide.Colors.ResolveChild(color) : RunColor(firstRun, sources, level);
        if (resolved is { } c) style.Append($"color:{Css(c)};");

        return $"<span style=\"{style}\">{WebUtility.HtmlEncode(text)}</span>";
    }

    string RunStyle(XElement? rPr, List<Source> sources, int level, double fontScale)
    {
        string? R(string name) => RunAttr(rPr, sources, level, name);
        var style = new StringBuilder();

        var latin = Typeface(RunElement(rPr, sources, level, "latin")) ?? slide.Theme.MinorLatin;
        var eastAsian = Typeface(RunElement(rPr, sources, level, "ea"));
        slide.Presentation.UsedFonts.Add(latin);
        style.Append($"font-family:{CssFamily(latin)}");
        if (!string.IsNullOrEmpty(eastAsian) && eastAsian != latin)
        {
            style.Append(',').Append(CssFamily(eastAsian));
            slide.Presentation.UsedFonts.Add(eastAsian);
        }
        style.Append(",sans-serif;");
        style.Append($"font-size:{Units.Css(RunSize(rPr, sources, level, fontScale))}pt;");

        if (R("b") is "1" or "true") style.Append("font-weight:700;");
        if (R("i") is "1" or "true") style.Append("font-style:italic;");
        var decorations = new List<string>();
        if (R("u") is { } u && u != "none") decorations.Add(u.StartsWith("dbl", StringComparison.Ordinal) ? "underline double" : "underline");
        if (R("strike") is "sngStrike" or "dblStrike") decorations.Add("line-through");
        if (decorations.Count > 0) style.Append($"text-decoration:{string.Join(' ', decorations)};");
        switch (R("cap"))
        {
            case "all": style.Append("text-transform:uppercase;"); break;
            case "small": style.Append("font-variant:small-caps;"); break;
        }
        if (int.TryParse(R("baseline"), out var baseline) && baseline != 0)
            style.Append($"vertical-align:{Units.Css4(baseline / 100000.0)}em;");
        if (int.TryParse(R("spc"), out var spacing) && spacing != 0)
            style.Append($"letter-spacing:{Units.Css(spacing / 100.0)}pt;");

        if (RunColor(rPr, sources, level) is { } color)
            style.Append($"color:{Css(color)};");
        if (RunElement(rPr, sources, level, "highlight") is { } highlight && slide.Colors.ResolveChild(highlight) is { } h)
            style.Append($"background-color:{Css(h)};");
        return style.ToString().TrimEnd(';');
    }

    RgbColor? RunColor(XElement? rPr, List<Source> sources, int level)
    {
        var fill = RunElement(rPr, sources, level, FillNames);
        return fill?.Name.LocalName switch
        {
            null => slide.Colors.Resolve(new XElement(Ns.A + "schemeClr", new XAttribute("val", "tx1"))),
            "solidFill" => slide.Colors.ResolveChild(fill),
            "noFill" => RgbColor.Black.WithAlpha(0),
            "gradFill" => slide.Colors.ResolveChild(fill.Element(Ns.A + "gsLst")?.Element(Ns.A + "gs")),
            _ => null,
        };
    }

    string? Typeface(XElement? font) =>
        font?.Str("typeface") is { Length: > 0 } t ? slide.Theme.ResolveTypeface(t) is { Length: > 0 } r ? r : null : null;

    static string CssFamily(string family) => $"'{family.Replace("'", "\\'")}'";

    static string Css(RgbColor c) =>
        c.A < 0.999 ? $"rgba({(int)Math.Round(c.R * 255)},{(int)Math.Round(c.G * 255)},{(int)Math.Round(c.B * 255)},{Units.Css4(c.A)})" : c.ToHex();

    static string AutoNumber(string scheme, int n)
    {
        var value = scheme switch
        {
            _ when scheme.StartsWith("alphaLc", StringComparison.Ordinal) => Alpha(n).ToLowerInvariant(),
            _ when scheme.StartsWith("alphaUc", StringComparison.Ordinal) => Alpha(n),
            _ when scheme.StartsWith("romanLc", StringComparison.Ordinal) => Roman(n).ToLowerInvariant(),
            _ when scheme.StartsWith("romanUc", StringComparison.Ordinal) => Roman(n),
            _ => n.ToString(System.Globalization.CultureInfo.InvariantCulture),
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
