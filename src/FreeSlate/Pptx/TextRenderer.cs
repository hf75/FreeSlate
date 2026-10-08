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
    /// <summary>The part whose relationships the runs' hyperlinks refer to.</summary>
    string? linkPart;

    /// <summary>The document's paint servers (gradients, patterns and pictures share their ids).</summary>
    public SvgPaint Paint { get; init; } = new();

    int nextClip;

    static readonly XNamespace HyperlinkColor = "http://schemas.microsoft.com/office/drawing/2018/hyperlinkcolor";

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
        linkPart = chain.Shapes[0].Part;
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
        svg.Append(unflipWidth is { } w ? $"<g data-tx=\"\" transform=\"matrix(-1 0 0 1 {Units.Css(w)} 0)\">" : "<g data-tx=\"\">");
        var paragraph = -1;
        foreach (var line in layout.Lines)
        {
            // Every paragraph is a group of its own: animations build text by paragraph (catalog: animation.targets).
            if (line.Paragraph != paragraph)
            {
                if (paragraph >= 0) svg.Append("</g>");
                paragraph = line.Paragraph;
                svg.Append($"<g data-p=\"{paragraph}\">");
            }
            // Baselines and glyphs sit on whole pixels counted from the top of the text area and from the start of
            // each format run (measured with the text-snap deck; see Snap and Text).
            var baseline = top + Snap(offset + line.Baseline);
            TextStyle? formatStyle = null;
            double formatOrigin = 0, formatEnd = double.NaN;
            foreach (var run in line.Runs)
            {
                if (!ReferenceEquals(run.Style, formatStyle) || Math.Abs(run.X - formatEnd) > 1e-6)
                {
                    formatStyle = run.Style;
                    formatOrigin = run.X;
                }
                formatEnd = run.X + run.Width;
                if (run.Style.Highlight is { } highlight)
                {
                    svg.Append($"<rect x=\"{Px(left + run.X)}\" y=\"{Px(baseline - line.Ascent)}\" width=\"{Px(run.Width)}\" height=\"{Px(line.Ascent + line.Descent)}\" fill=\"{highlight.ToHex()}\"/>");
                }
                if (string.IsNullOrWhiteSpace(run.Text))
                    continue;
                var filter = run.Style.Effects is { } effects
                    ? filters.TryGetValue(effects, out var known) ? known : filters[effects] = SvgEffects.Filter(effects, slide.Colors, 0, 0, defs, boundingBox: true)
                    : null;
                var shifted = run.Style.BaselineShift == 0 ? baseline : top + Snap(offset + line.Baseline - run.Style.BaselineShift * run.Style.Size);
                // Gradient and pattern fills span the line (measured: every line of a two-line text box starts the
                // gradient anew, from the line's start to its end and from its ascent to its descent).
                var box = (left + (line.Runs.Count > 0 ? line.Runs[0].X : 0), baseline - line.Ascent, line.Width, line.Ascent + line.Descent);
                svg.Append(Text(run, left + run.X, shifted, filter, left + formatOrigin, box, defs));
            }
        }
        if (paragraph >= 0) svg.Append("</g>");
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

    /// <summary>
    /// Text smaller than this (pixels per em) PowerPoint draws hinted, with crisp horizontal edges on whole-pixel
    /// baselines (measured with the text-snap deck: hinted up to 22 pt, smooth from 23 pt); the browser's own
    /// hinted text comes closest to that.
    /// </summary>
    const double HintedBelow = 30;

    /// <summary>Offsets (pixels) that align Chromium's anti-aliasing samples with PowerPoint's (see Text).</summary>
    const double SampleShiftX = 1.0 / 16, SampleShiftY = 1.0 / 8;

    /// <summary>A length in points rounded to whole pixels (half up), as PowerPoint places baselines and glyphs.</summary>
    static double Snap(double points) => Math.Floor(points * PxPerPt + 0.5) / PxPerPt;

    /// <summary>
    /// SVG for a run with its first glyph at (x, y) in points. PowerPoint draws a format run with the glyph origins
    /// rounded to whole pixels from the run's start (<paramref name="origin"/>, exact), using advances in master
    /// units (measured with the text-snap deck); a run continuing a format run passes the format run's start.
    /// Glyphs are drawn as outlines, unhinted and at their exact position like PowerPoint draws them, with the
    /// text itself transparent on top for selection, search and accessibility; color fonts, synthesized styles
    /// and glyphs missing from the font are left to the browser.
    /// </summary>
    string Text(PlacedRun run, double x, double y, string? filter = null, double? origin = null,
        (double X, double Y, double W, double H)? lineBox = null, StringBuilder? defs = null)
    {
        var style = run.Style;
        var data = style.Font.Data;
        var text = run.Style.AllCaps ? run.Text.ToUpperInvariant() : run.Text;
        var glyphs = data.Glyphs(text, style.Kerning);
        var start = origin ?? x;
        var startPx = start * PxPerPt;

        // Glyph positions in whole pixels from the format run's start; characters take their cluster's position.
        var glyphX = new double[glyphs.Length];
        var clusterX = new double?[text.Length];
        var spacing = FontData.MasterUnits(style.LetterSpacing);
        var position = x - start;
        for (var i = 0; i < glyphs.Length; i++)
        {
            var glyph = glyphs[i];
            glyphX[i] = Math.Floor(position * PxPerPt + 0.5);
            var firstOfCluster = glyph.Cluster < text.Length && clusterX[glyph.Cluster] == null;
            if (firstOfCluster)
                clusterX[glyph.Cluster] = glyphX[i];
            position += FontData.MasterUnits(glyph.Advance * style.Size) + FontData.MasterUnits(glyph.Kerning * style.Size) + (firstOfCluster ? spacing : 0);
        }
        var xs = new StringBuilder();
        double current = 0;
        for (var i = 0; i < text.Length; i++)
        {
            current = clusterX[i] ?? current;
            if (char.IsLowSurrogate(text[i]))
                continue;
            if (xs.Length > 0) xs.Append(' ');
            xs.Append(Units.Css(startPx + current));
        }

        var painted = style.Paint is not null and not NoFill || style.Line != null;
        var outlined = (style.Size * PxPerPt >= HintedBelow || painted) && !data.IsColor && !style.Font.SyntheticBold && !style.Font.SyntheticItalic
                       && glyphs.All(g => g.Id != 0 || g.Cluster < text.Length && char.IsWhiteSpace(text[g.Cluster]));
        var family = data.Family;
        var fill = $" fill=\"{style.Color.ToHex()}\"" + (style.Color.A < 0.999 ? $" fill-opacity=\"{Units.Css4(style.Color.A)}\"" : "");
        var sb = new StringBuilder();
        if (outlined)
        {
            var scale = style.Size * PxPerPt / data.UnitsPerEm;
            var units = new StringBuilder();
            for (var i = 0; i < glyphs.Length; i++)
            {
                if (slide.Presentation.Glyphs.Id(data, glyphs[i].Id) is not { } id)
                    continue;
                var gx = glyphX[i] / scale + glyphs[i].OffsetX * data.UnitsPerEm;
                var gy = -glyphs[i].OffsetY * data.UnitsPerEm;
                units.Append($"<use href=\"#{id}\"");
                if (Math.Abs(gx) > 0.05) units.Append($" x=\"{Units.Css1(gx)}\"");
                if (Math.Abs(gy) > 0.05) units.Append($" y=\"{Units.Css1(gy)}\"");
                units.Append("/>");
            }
            var decorations = Decorations(run, style, data, x, y);
            if (filter != null || decorations.Length > 0)
                sb.Append($"<g{fill}{(filter != null ? $" filter=\"url(#{filter})\"" : "")}>");
            // PowerPoint samples coverage at the top-left of 8 × 4 sub-pixels, Chromium at the centers of 4 rows:
            // shifting the glyphs by half a sub-pixel makes both see the same edges.
            var (tx, ty) = (startPx + SampleShiftX, y * PxPerPt + SampleShiftY);
            var transform = $"translate({Units.Css4(tx)} {Units.Css4(ty)}) scale({Units.Scale(scale)})";
            var local = defs ?? new StringBuilder();
            var stroke = style.Line is { } outline ? StrokeInFontUnits(Paint.Stroke(outline, 0, 0, local), scale) : "";
            if (style.Paint is { } textPaint and not NoFill and not SolidFill)
            {
                // The paint fills the line box, clipped to the glyphs: gradients, patterns and pictures keep their
                // geometry in pixels while the glyphs are placed in font units.
                var box = lineBox ?? (x, y - style.Size, run.Width, style.Size * 1.2);
                var (bx, by, bw, bh) = (box.X * PxPerPt, box.Y * PxPerPt, Math.Max(1, box.W * PxPerPt), Math.Max(1, box.H * PxPerPt));
                // A mask rather than a clip path: Chromium unites the children of a clip path with path operations
                // that fill some counters (the eye of Aptos Bold's e), a mask is rendered as an image.
                var maskId = $"tm{slide.Number}-{++nextClip}";
                var maskTransform = $"translate({Units.Css4(tx - bx)} {Units.Css4(ty - by)}) scale({Units.Scale(scale)})";
                var (rx, ry, rw, rh) = (Units.Css(-bw), Units.Css(-bh), Units.Css(3 * bw), Units.Css(3 * bh));
                local.Append($"<mask id=\"{maskId}\" maskUnits=\"userSpaceOnUse\" x=\"{rx}\" y=\"{ry}\" width=\"{rw}\" height=\"{rh}\"><g transform=\"{maskTransform}\" fill=\"#fff\">{units}</g></mask>");
                sb.Append($"<g transform=\"translate({Units.Css4(bx)} {Units.Css4(by)})\"><rect x=\"{rx}\" y=\"{ry}\" width=\"{rw}\" height=\"{rh}\"{Paint.Fill(textPaint, bw, bh, local, 0, (bx, by))} mask=\"url(#{maskId})\"/></g>");
                if (stroke.Length > 0)
                    sb.Append($"<g transform=\"{transform}\" fill=\"none\"{stroke}>{units}</g>");
            }
            else
            {
                sb.Append($"<g transform=\"{transform}\"{(filter != null || decorations.Length > 0 ? "" : fill)}{stroke}>{units}</g>");
            }
            if (filter != null || decorations.Length > 0)
                sb.Append(decorations).Append("</g>");
            if (style.Effects?.Element(Ns.A + "reflection") != null)
            {
                // The reflection mirrors the text about a line slightly below the baseline (measured: 0.055 em
                // below the bottom of the glyphs of a 60 pt line).
                var size = style.Size * PxPerPt;
                var bottom = y * PxPerPt + 0.055 * size;
                var height = size * data.Ascent + 0.055 * size;
                var mirrored = SvgEffects.Reflection(style.Effects, sb.ToString(), (x * PxPerPt, bottom - height, run.Width * PxPerPt, height), local);
                sb.Clear().Append(mirrored);
            }
            if (defs == null && local.Length > 0)
                sb.Insert(0, $"<defs>{local}</defs>");
        }
        else
        {
            slide.Presentation.UsedFonts.Add(family);
        }

        sb.Append($"<text{(outlined ? " class=\"sel\"" : "")} x=\"{xs}\" y=\"{Px(y)}\" font-family=\"'{WebUtility.HtmlEncode(family.Replace("'", "\'"))}'\" font-size=\"{Px(style.Size)}\"");
        if (style.Bold) sb.Append(" font-weight=\"700\"");
        if (style.Italic) sb.Append(" font-style=\"italic\"");
        if (!outlined)
        {
            sb.Append(fill);
            var decorations = new List<string>();
            if (style.Underline != null) decorations.Add("underline");
            if (style.Strike != null) decorations.Add("line-through");
            if (decorations.Count > 0) sb.Append($" text-decoration=\"{string.Join(' ', decorations)}\"");
            if (!style.Kerning) sb.Append(" style=\"font-kerning:none\"");
            if (filter != null) sb.Append($" filter=\"url(#{filter})\"");
        }
        sb.Append('>').Append(WebUtility.HtmlEncode(text)).Append("</text>");
        return style.Link is { } link ? Links.Open(link) + sb + "</a>" : sb.ToString();
    }

    /// <summary>Stroke attributes in pixels converted to the font units of a glyph group (width and dashes).</summary>
    static string StrokeInFontUnits(string attributes, double scale) =>
        System.Text.RegularExpressions.Regex.Replace(attributes, "(stroke-width|stroke-dasharray)=\"([^\"]+)\"", m =>
            $"{m.Groups[1].Value}=\"{string.Join(' ', m.Groups[2].Value.Split(' ').Select(v => Units.Css1(double.Parse(v, CultureInfo.InvariantCulture) / scale)))}\"");

    /// <summary>
    /// Underline and strikethrough of a run as filled rectangles (pixels), from the font's post and OS/2 metrics,
    /// placed on whole pixels; dbl and heavy underlines and double strikethrough draw two lines or a thicker one.
    /// </summary>
    static string Decorations(PlacedRun run, TextStyle style, FontData data, double x, double y)
    {
        if (style.Underline == null && style.Strike == null)
            return "";
        var sb = new StringBuilder();
        var left = x * PxPerPt;
        var width = run.Width * PxPerPt;
        var size = style.Size * PxPerPt;
        var baseline = y * PxPerPt;
        void Line(double offset, double thickness)
        {
            var height = Math.Max(1, Math.Round(thickness));
            var top = Math.Round(baseline + offset - height / 2);
            sb.Append($"<rect x=\"{Units.Css(left)}\" y=\"{Units.Css(top)}\" width=\"{Units.Css(width)}\" height=\"{Units.Css(height)}\"/>");
        }
        if (style.Underline is { } underline)
        {
            var thickness = data.UnderlineThickness * size * (underline is "heavy" or "dottedHeavy" or "dashHeavy" or "dashLongHeavy" or "dotDashHeavy" or "dotDotDashHeavy" or "wavyHeavy" ? 2 : 1);
            var offset = data.UnderlinePosition * size;
            Line(offset, thickness);
            if (underline is "dbl" or "wavyDbl")
                Line(offset + 2 * Math.Max(1, Math.Round(thickness)), thickness);
        }
        if (style.Strike is { } strike)
        {
            var thickness = data.StrikeoutThickness * size;
            var offset = -data.StrikeoutPosition * size;
            if (strike == "dblStrike")
            {
                var gap = Math.Max(1, Math.Round(thickness));
                Line(offset - gap, thickness);
                Line(offset + gap, thickness);
            }
            else
            {
                Line(offset, thickness);
            }
        }
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
        // Hyperlinked text takes the theme's hyperlink color and an underline, unless ahyp:hlinkClr keeps the
        // text color (catalog: interactivity.hyperlinks; measured).
        var hlink = rPr?.Element(Ns.A + "hlinkClick");
        var link = hlink != null && linkPart != null ? Links.Resolve(hlink, linkPart, slide.Presentation) : null;
        var color = RunColor(rPr, sources, level);
        var underline = R("u") is { } u && u != "none" ? u : null;
        // Gradient, pattern and picture fills and the text outline (catalog: text.fillOutline).
        var fillElement = RunElement(rPr, sources, level, FillNames);
        var part = linkPart ?? slide.SlidePart;
        Fill? paint = fillElement?.Name.LocalName is "gradFill" or "pattFill" or "blipFill"
            ? FillReader.Read(fillElement, slide.Colors, slide.Images(part), slide.ImageSizes(part))
            : null;
        var lineElement = RunElement(rPr, sources, level, "ln");
        var line = lineElement != null ? OutlineReader.Merge([(lineElement, slide.Colors)]) : null;
        if (line is { Visible: false }) line = null;
        if (link != null)
        {
            if (hlink!.Descendants(HyperlinkColor + "hlinkClr").FirstOrDefault()?.Str("val") != "tx")
                color = slide.Colors.Resolve(new XElement(Ns.A + "schemeClr", new XAttribute("val", "hlink")));
            if (rPr!.Attribute("u") == null) underline = "sng";
            paint = null;
        }
        return new TextStyle(
            fonts.Resolve(family, bold, italic),
            size,
            color,
            bold,
            italic,
            size >= kern,
            int.TryParse(R("spc"), out var spc) ? spc / 100.0 : 0,
            underline,
            R("strike") is "sngStrike" or "dblStrike" ? R("strike") : null,
            int.TryParse(R("baseline"), out var b) ? b / 100000.0 : 0,
            RunElement(rPr, sources, level, "highlight") is { } h ? slide.Colors.ResolveChild(h) : null,
            R("cap") == "all",
            RunElement(rPr, sources, level, "effectLst"),
            link,
            paint,
            line);
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
