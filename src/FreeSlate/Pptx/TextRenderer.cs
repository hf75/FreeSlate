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

    /// <summary>The colors the text resolves against: the slide's, or <see cref="ColorOverride"/>.</summary>
    ColorContext Colors => ColorOverride ?? slide.Colors;

    /// <summary>A chart's own color mapping while the chart is drawn.</summary>
    public ColorContext? ColorOverride { get; set; }

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
        // Vertical text (catalog: text.vertical); the insets stay with the shape's sides.
        if (Body("vert") is "vert" or "vert270" or "eaVert" or "mongolianVert")
            return RenderTurned(txBody, sources, Body("vert")!, left, top, width, height, Body("wrap") != "none", fontScale, spacingReduction, Body("anchor") ?? "t");
        if (Body("vert") is "wordArtVert" or "wordArtVertRtl")
            return RenderStacked(Layout(txBody, sources, 1e6, false, fontScale, spacingReduction), Body("vert") == "wordArtVertRtl", left, top, width, height, Body("anchor") ?? "t");
        // Columns (catalog: text.columns): the text is laid out at the column width and its lines are shared out
        // evenly over the columns (measured: 33 lines in three columns become 11, 11 and 11, even when they overflow).
        var columns = Math.Clamp(int.TryParse(Body("numCol"), out var columnCount) ? columnCount : 1, 1, 16);
        var columnGap = (long.TryParse(Body("spcCol"), out var gap) ? gap : 0) / Units.EmuPerPoint;
        var columnWidth = columns > 1 ? Math.Max(0, (width - (columns - 1) * columnGap) / columns) : width;
        var wrap = Body("wrap") != "none";
        // Space before the first paragraph and after the last counts only when the body asks for it (measured).
        var spaceFirstLast = Body("spcFirstLastPara") is "1" or "true";
        var layout = Layout(txBody, sources, Math.Max(0, columnWidth), wrap, fontScale, spacingReduction, spaceFirstLast);
        if (columns > 1)
            layout = Columns(layout, columns, columnWidth, columnGap, Body("rtlCol") is "1" or "true");
        else if (Body("anchorCtr") is "1" or "true")
            TextLayout.CenterBlock(layout, width);
        // Text that does not fit the height loses its last lines with vertOverflow ellipsis (an ellipsis ends the
        // last line kept) or clip; the remaining lines are anchored as usual (measured).
        if (Body("vertOverflow") is "ellipsis" or "clip" && layout.Height > height + 0.01 && layout.Lines.Count > 1)
        {
            var kept = layout.Lines.TakeWhile((l, i) => i == 0 || l.Baseline + l.Descent <= height + 0.01).ToList();
            if (kept.Count < layout.Lines.Count)
            {
                var trimmed = new LaidOutText { Height = kept[^1].Baseline + kept[^1].Descent };
                trimmed.Lines.AddRange(kept);
                if (Body("vertOverflow") == "ellipsis")
                    TextLayout.Ellipsize(kept[^1], width);
                layout = trimmed;
            }
        }
        // WordArt transforms bend the glyph outlines into the preset's paths in the shape's text rectangle.
        var warp = bodies.Select(b => b?.Element(Ns.A + "prstTxWarp")).FirstOrDefault(e => e != null);
        if (warp?.Str("prst") is { } preset && TextWarp.Definition(preset) is { } definition)
        {
            var box = (geometry.TextLeft, geometry.TextTop, geometry.TextRight - geometry.TextLeft, geometry.TextBottom - geometry.TextTop);
            return EmitWarped(layout, left, top, height, Body("anchor") ?? "t", warp, definition, box, Body("fromWordArt") is "1" or "true", width);
        }
        var markup = Emit(layout, left, top, height, Body("anchor") ?? "t", unflip ? boxWidth : null);
        // horzOverflow clip cuts the text at the sides of the text rectangle (measured: not at the insets).
        if (Body("horzOverflow") == "clip" && markup.Length > 0)
        {
            var id = $"hc{++nextClip}";
            markup = $"<defs><clipPath id=\"{id}\"><rect x=\"{Units.Css(geometry.TextLeft)}\" y=\"-100000\" width=\"{Units.Css(geometry.TextRight - geometry.TextLeft)}\" height=\"200000\"/></clipPath></defs>" +
                     $"<g clip-path=\"url(#{id})\">{markup}</g>";
        }
        return markup;
    }

    /// <summary>
    /// Text turned by 90° (vert, and East Asian vertical text) or 270° (vert270): laid out along the shape's height
    /// and anchored across it, the text's top facing right (left for vert270). East Asian vertical text keeps its
    /// ideographs upright; mongolianVert stacks its lines from left to right.
    /// </summary>
    string RenderTurned(XElement txBody, List<Source> sources, string mode, double left, double top, double width, double height,
        bool wrap, double fontScale, double spacingReduction, string anchor) =>
        EmitTurned(Layout(txBody, sources, Math.Max(0, height), wrap, fontScale, spacingReduction), mode, left, top, width, height, anchor);

    /// <summary>Emits text laid out along <paramref name="height"/> turned into the area (see <see cref="RenderTurned"/>).</summary>
    public string EmitTurned(LaidOutText layout, string mode, double left, double top, double width, double height, string anchor)
    {
        if (mode is "eaVert" or "mongolianVert")
            layout = Upright(layout);
        Func<LaidOutLine, double>? shift = null;
        if (mode == "mongolianVert")
        {
            // The block's anchor and its lines are mirrored: the first line lies left.
            anchor = anchor switch { "t" => "b", "b" => "t", _ => anchor };
            var total = layout.Height;
            shift = line => total - 2 * line.Baseline - line.Descent + line.Ascent;
        }
        var matrix = mode == "vert270"
            ? $"matrix(0 -1 1 0 {Px(left)} {Px(top + height)})"
            : $"matrix(0 1 -1 0 {Px(left + width)} {Px(top)})";
        return $"<g transform=\"{matrix}\">{Emit(layout, 0, 0, width, anchor, lineShift: shift)}</g>";
    }

    /// <summary>Splits runs so that East Asian characters stand alone as upright runs.</summary>
    static LaidOutText Upright(LaidOutText layout)
    {
        foreach (var line in layout.Lines)
        {
            var runs = new List<PlacedRun>();
            foreach (var run in line.Runs)
            {
                if (run.Picture != null || !run.Text.Any(c => Scripts.Upright(c)))
                {
                    runs.Add(run);
                    continue;
                }
                var style = run.Style;
                var advances = style.Font.Data.LayoutAdvances(run.Text, style.Size, style.Kerning, style.LetterSpacing);
                var x = run.X;
                var start = 0;
                for (var i = 0; i <= run.Text.Length; i++)
                {
                    var upright = i < run.Text.Length && Scripts.Upright(run.Text[i]);
                    if (i < run.Text.Length && !upright)
                        continue;
                    if (i > start)
                    {
                        var w = advances[start..i].Sum();
                        runs.Add(run with { Text = run.Text[start..i], X = x, Width = w });
                        x += w;
                    }
                    if (upright)
                    {
                        runs.Add(run with { Text = run.Text[i].ToString(), X = x, Width = advances[i], Upright = true });
                        x += advances[i];
                    }
                    start = i + 1;
                }
            }
            line.Runs.Clear();
            line.Runs.AddRange(runs);
        }
        return layout;
    }

    /// <summary>
    /// Stacked WordArt text (wordArtVert): upright characters one below the other in columns that run left to right
    /// (right to left for wordArtVertRtl), breaking at spaces. Measured: characters and columns are 1.41 × the size
    /// apart and the baseline sits 1.12 × the size into a character's cell; the anchor places the columns, the
    /// alignment the characters in their column; trailing spaces take no room.
    /// </summary>
    string RenderStacked(LaidOutText layout, bool rightToLeft, double left, double top, double width, double height, string anchor)
    {
        const double Step = 1.41, BaselineDepth = 1.12;
        var columns = new List<(List<(string Text, TextStyle Style)> Cells, string Align)>();
        foreach (var paragraph in layout.Lines.GroupBy(l => l.Paragraph))
        {
            var cells = paragraph.SelectMany(l => l.Runs).Where(r => r.Picture == null)
                .SelectMany(r => SplitGraphemes(r.Style.AllCaps ? r.Text.ToUpperInvariant() : r.Text).Select(t => (Text: t, r.Style))).ToList();
            var align = paragraph.First().Align;
            var column = new List<(string Text, TextStyle Style)>();
            double used = 0;
            var i = 0;
            while (i < cells.Count)
            {
                // The next word with its trailing spaces.
                var end = i;
                while (end < cells.Count && cells[end].Text != " ") end++;
                while (end < cells.Count && cells[end].Text == " ") end++;
                var word = cells.GetRange(i, end - i);
                var length = word.Where(c => c.Text != " ").Sum(c => Step * c.Style.Size);
                if (column.Count > 0 && used + length > height + 1e-6)
                {
                    columns.Add((column, align));
                    (column, used) = ([], 0);
                }
                // A word longer than a column breaks between its characters.
                foreach (var cell in word)
                {
                    var step = Step * cell.Style.Size;
                    if (column.Count > 0 && cell.Text != " " && used + step > height + 1e-6)
                    {
                        columns.Add((column, align));
                        (column, used) = ([], 0);
                    }
                    column.Add(cell);
                    used += step;
                }
                i = end;
            }
            columns.Add((column, align));
        }
        var pitches = columns.Select(c => Step * (c.Cells.Count == 0 ? 18 : c.Cells.Max(x => x.Style.Size))).ToList();
        var total = pitches.Sum();
        var start = anchor switch { "ctr" => (width - total) / 2, "b" => width - total, _ => 0.0 };
        if (rightToLeft) start = width - total - start;
        var svg = new StringBuilder("<g data-tx=\"\">");
        var x = left + start;
        for (var c = 0; c < columns.Count; c++)
        {
            var index = rightToLeft ? columns.Count - 1 - c : c;
            var (cells, align) = columns[index];
            var visible = cells.ToList();
            while (visible.Count > 0 && visible[^1].Text == " ") visible.RemoveAt(visible.Count - 1);
            var length = visible.Sum(v => Step * v.Style.Size);
            var y = top + align switch { "ctr" => (height - length) / 2, "r" => height - length, _ => 0.0 };
            var center = x + pitches[index] / 2;
            foreach (var (text, style) in visible)
            {
                var w = style.Font.Data.LayoutWidth(text, style.Size, false);
                if (text != " ")
                    svg.Append(Text(new PlacedRun(text, style, 0, w), center - w / 2, y + BaselineDepth * style.Size));
                y += Step * style.Size;
            }
            x += pitches[index];
        }
        svg.Append("</g>");
        return svg.ToString();
    }

    static IEnumerable<string> SplitGraphemes(string text)
    {
        var e = System.Globalization.StringInfo.GetTextElementEnumerator(text);
        while (e.MoveNext())
            yield return (string)e.Current;
    }

    /// <summary>The lines of a layout distributed over columns, each column starting at the top.</summary>
    static LaidOutText Columns(LaidOutText layout, int columns, double columnWidth, double gap, bool rightToLeft)
    {
        var lines = layout.Lines;
        if (lines.Count == 0)
            return layout;
        var perColumn = (int)Math.Ceiling(lines.Count / (double)columns);
        var result = new LaidOutText();
        double height = 0;
        for (var c = 0; c < columns; c++)
        {
            var start = c * perColumn;
            if (start >= lines.Count)
                break;
            var end = Math.Min(lines.Count, start + perColumn);
            var columnTop = lines[start].Baseline - lines[start].Ascent;
            var dx = (rightToLeft ? columns - 1 - c : c) * (columnWidth + gap);
            for (var i = start; i < end; i++)
            {
                var line = lines[i];
                var moved = new LaidOutLine
                {
                    Paragraph = line.Paragraph, Ascent = line.Ascent, Descent = line.Descent, Width = line.Width, Align = line.Align,
                    Baseline = line.Baseline - columnTop,
                };
                moved.Runs.AddRange(line.Runs.Select(r => r with { X = r.X + dx }));
                result.Lines.Add(moved);
            }
            height = Math.Max(height, lines[end - 1].Baseline + lines[end - 1].Descent - columnTop);
        }
        result.Height = height;
        return result;
    }

    /// <summary>
    /// Text with a WordArt transform (catalog: text.warp): the glyph outlines in pixels, warped by
    /// <see cref="TextWarp"/>, one path per run style; the text stays transparent on top for selection.
    /// </summary>
    string EmitWarped(LaidOutText layout, double left, double top, double height, string anchor, XElement warp,
        Drawing.Geometry.GeometryDefinition definition, (double L, double T, double W, double H) box, bool fromWordArt, double width)
    {
        var offset = anchor switch { "ctr" => (height - layout.Height) / 2, "b" => height - layout.Height, _ => 0 };
        var glyphs = new List<TextWarp.Glyph>();
        var owners = new List<TextStyle>();
        var selection = new StringBuilder();
        for (var lineIndex = 0; lineIndex < layout.Lines.Count; lineIndex++)
        {
            var line = layout.Lines[lineIndex];
            var baseline = top + Snap(offset + line.Baseline);
            foreach (var run in line.Runs)
            {
                if (string.IsNullOrWhiteSpace(run.Text))
                    continue;
                var style = run.Style;
                var data = style.Font.Data;
                var text = style.AllCaps ? run.Text.ToUpperInvariant() : run.Text;
                var scale = style.Size * PxPerPt / data.UnitsPerEm;
                var position = (left + run.X) * PxPerPt;
                var spacing = FontData.MasterUnits(style.LetterSpacing) * PxPerPt;
                foreach (var g in data.Glyphs(text, style.Kerning))
                {
                    var outline = data.Outline(g.Id);
                    if (outline.Length > 0)
                    {
                        var x = position + g.OffsetX * style.Size * PxPerPt;
                        var y = baseline * PxPerPt - g.OffsetY * style.Size * PxPerPt;
                        glyphs.Add(new TextWarp.Glyph(TextWarp.Contours(outline, x, y, scale), lineIndex, baseline * PxPerPt));
                        owners.Add(style);
                    }
                    position += (FontData.MasterUnits(g.Advance * style.Size) + FontData.MasterUnits(g.Kerning * style.Size)) * PxPerPt + spacing;
                }
                selection.Append($"<text class=\"sel\" x=\"{Px(left + run.X)}\" y=\"{Px(baseline)}\" font-family=\"'{WebUtility.HtmlEncode(data.Family.Replace("'", "\\'"))}'\" font-size=\"{Px(style.Size)}\">{WebUtility.HtmlEncode(text)}</text>");
            }
        }
        var adjustments = warp.Element(Ns.A + "avLst")?.Elements(Ns.A + "gd")
            .Select(gd => new Drawing.Geometry.Guide(gd.Str("name") ?? "", gd.Str("fmla") ?? "val 0")).ToList() ?? [];
        // Each line's alignment with the free space before and after it in the text box (pixels, insets included).
        (string, double, double, double) Alignment(int index)
        {
            var line = layout.Lines[index];
            var start = line.Runs.Count > 0 ? line.Runs.Min(r => r.X) : 0;
            return (line.Align, (left + start) * PxPerPt - box.L, box.L + box.W - (left + start + line.Width) * PxPerPt, line.Width * PxPerPt);
        }
        var warped = TextWarp.Apply(definition, adjustments, box, glyphs, fromWordArt, Alignment);

        var svg = new StringBuilder("<g data-tx=\"\">");
        var defs = new StringBuilder();
        for (var i = 0; i < warped.Count;)
        {
            var style = owners[i];
            var contours = new List<List<(double X, double Y)>>();
            var j = i;
            for (; j < warped.Count && ReferenceEquals(owners[j], style); j++)
                contours.AddRange(warped[j]);
            var fill = style.Paint is { } paint and not NoFill and not SolidFill
                ? Paint.Fill(paint, box.W, box.H, defs, 0, (box.L, box.T))
                : $" fill=\"{style.Color.ToHex()}\"" + (style.Color.A < 0.999 ? $" fill-opacity=\"{Units.Css4(style.Color.A)}\"" : "");
            var stroke = style.Line is { } outline ? Paint.Stroke(outline, box.W, box.H, defs) : "";
            svg.Append($"<path d=\"{TextWarp.PathData(contours)}\"{fill}{stroke}/>");
            i = j;
        }
        svg.Append(selection).Append("</g>");
        return defs.Length > 0 ? $"<defs>{defs}</defs>{svg}" : svg.ToString();
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
    public string Emit(LaidOutText layout, double left, double top, double height, string anchor, double? unflipWidth = null,
        Func<LaidOutLine, double>? lineShift = null)
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
            var shiftBy = lineShift?.Invoke(line) ?? 0;
            var baseline = top + Snap(offset + line.Baseline + shiftBy);
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
                if (run.Picture != null)
                {
                    var pictureHeight = run.PictureHeight;
                    svg.Append($"<image href=\"{run.Picture}\" x=\"{Px(left + run.X)}\" y=\"{Px(baseline - pictureHeight)}\" width=\"{Px(run.Width)}\" height=\"{Px(pictureHeight)}\" preserveAspectRatio=\"none\"/>");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(run.Text))
                    continue;
                var filter = run.Style.Effects is { } effects
                    ? filters.TryGetValue(effects, out var known) ? known : filters[effects] = SvgEffects.Filter(effects, Colors, 0, 0, defs, boundingBox: true)
                    : null;
                var shifted = run.Style.BaselineShift == 0 && run.Lift == 0 ? baseline : top + Snap(offset + line.Baseline + shiftBy - run.Style.BaselineShift * run.Style.Size - run.Lift);
                // Gradient and pattern fills span the line (measured: every line of a two-line text box starts the
                // gradient anew, from the line's start to its end and from its ascent to its descent).
                var box = (left + (line.Runs.Count > 0 ? line.Runs[0].X : 0), baseline - line.Ascent, line.Width, line.Ascent + line.Descent);
                var markup = Text(run, left + run.X, shifted, filter, left + formatOrigin, box, defs);
                // Upright characters in vertical text turn back about the middle of their em box.
                svg.Append(run.Upright
                    ? $"<g transform=\"rotate(-90 {Px(left + run.X + run.Width / 2)} {Px(shifted - 0.38 * run.Style.Size)})\">{markup}</g>"
                    : markup);
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

    LaidOutText Layout(XElement txBody, List<Source> sources, double width, bool wrap, double fontScale, double spacingReduction,
        bool spaceFirstLast = false)
    {
        var counters = new int[9];
        var paragraphs = txBody.Elements(Ns.A + "p")
            .Select(p => Paragraph(p, sources, fontScale, spacingReduction, counters))
            .ToList();
        return TextLayout.Layout(paragraphs, width, wrap, spaceFirstLast);
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
                case "r" or "fld":
                    // Every piece of one font slot (Latin, East Asian, complex script) gets its own font.
                    var t = element.Name.LocalName == "fld" ? FieldText(element) : element.Element(Ns.A + "t")?.Value ?? "";
                    var rPr = element.Element(Ns.A + "rPr");
                    foreach (var (piece, slot, script) in Scripts.Split(t))
                        runs.Add(new TextRun(piece, Style(rPr, sources, level, fontScale, piece, slot, script)));
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
            FontAlign = P("fontAlgn") ?? "auto",
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
            case "buBlip" when kind.Element(Ns.A + "blip")?.Attribute(Ns.R + "embed")?.Value is { } embed
                               && slide.Images(linkPart ?? slide.SlidePart)(embed) is { } picture:
            {
                // A picture bullet (catalog: text.bullets).
                counters[level] = 0;
                var (w, h) = slide.ImageSizes(linkPart ?? slide.SlidePart)(embed);
                var pictureSize = ParagraphElement(pPr, sources, level, "buSzTx", "buSzPct", "buSzPts");
                var factor = pictureSize?.Name.LocalName == "buSzPct" ? (pictureSize.Long("val") ?? 100000) / 100000.0 : 1;
                return new Bullet("", textStyle with { Size = textStyle.Size * factor }, picture, w > 0 && h > 0 ? w / h : 1);
            }
            case "buChar":
                text = kind.Str("char") ?? "•";
                counters[level] = 0;
                break;
            case "buAutoNum":
                counters[level] = counters[level] == 0 ? kind.Int("startAt") ?? 1 : counters[level] + 1;
                text = AutoNumbering.Format(kind.Str("type") ?? "arabicPeriod", counters[level]);
                break;
            default:
                counters[level] = 0;
                return null;
        }

        var font = ParagraphElement(pPr, sources, level, "buFontTx", "buFont");
        var resolved = font?.Name.LocalName == "buFont" && slide.Theme.ResolveTypeface(font.Str("typeface") ?? "") is { Length: > 0 } family
            ? fonts.Resolve(family, textStyle.Bold, textStyle.Italic)
            : textStyle.Font;
        // Numbering in other scripts (Chinese numerals, Arabic letters, Thai digits …) needs a font that has them.
        if (!resolved.Data.Covers(text) && Scripts.Split(text) is [var (_, slot, script)])
        {
            var candidates = Scripts.Fallbacks(slot, slot == FontSlot.EastAsian ? Scripts.EastAsianScript(null, script) : script);
            var theme = slot switch
            {
                FontSlot.EastAsian => slide.Theme.ScriptFonts.GetValueOrDefault((false, Scripts.EastAsianScript(null, script))),
                FontSlot.Complex when script != null => slide.Theme.ScriptFonts.GetValueOrDefault((false, script)),
                _ => null,
            };
            resolved = fonts.ResolveCovering(theme ?? candidates[0], textStyle.Bold, textStyle.Italic, text, candidates);
        }

        var sizeElement = ParagraphElement(pPr, sources, level, "buSzTx", "buSzPct", "buSzPts");
        var size = sizeElement?.Name.LocalName switch
        {
            "buSzPct" => textStyle.Size * (sizeElement.Long("val") ?? 100000) / 100000.0,
            "buSzPts" => (sizeElement.Long("val") ?? 1800) / 100.0,
            _ => textStyle.Size,
        };

        var colorElement = ParagraphElement(pPr, sources, level, "buClrTx", "buClr");
        var color = colorElement?.Name.LocalName == "buClr" ? Colors.ResolveChild(colorElement) ?? textStyle.Color : textStyle.Color;
        return new Bullet(text, new TextStyle(resolved, size, color, Kerning: false));
    }

    TextStyle Style(XElement? rPr, List<Source> sources, int level, double fontScale, string? text,
        FontSlot slot = FontSlot.Latin, string? script = null)
    {
        string? R(string name) => RunAttr(rPr, sources, level, name);
        var size = (double.TryParse(R("sz"), out var sz) ? sz / 100 : 18) * fontScale;
        var bold = R("b") is "1" or "true";
        var italic = R("i") is "1" or "true";

        // The font of the slot (a:latin, a:ea or a:cs); empty East Asian and complex fonts come from the theme's
        // per-script fonts (by language for East Asian text); a font without the glyphs gives way to one that has
        // them (catalog: text.fonts).
        var latin = RunElement(rPr, sources, level, "latin");
        var major = latin?.Str("typeface") is "+mj-lt";
        var family = Typeface(latin) ?? slide.Theme.MinorLatin;
        if (slot == FontSlot.EastAsian)
        {
            var themeScript = Scripts.EastAsianScript(R("lang"), script);
            family = Typeface(RunElement(rPr, sources, level, "ea"))
                     ?? slide.Theme.ScriptFonts.GetValueOrDefault((major, themeScript))
                     ?? Scripts.Fallbacks(slot, themeScript)[0];
            script = themeScript;
        }
        else if (slot == FontSlot.Complex)
        {
            family = Typeface(RunElement(rPr, sources, level, "cs"))
                     ?? (script != null ? slide.Theme.ScriptFonts.GetValueOrDefault((major, script)) : null)
                     ?? Scripts.Fallbacks(slot, script)[0];
        }
        var resolved = text != null
            ? fonts.ResolveCovering(family, bold, italic, text, Scripts.Fallbacks(slot, script))
            : fonts.Resolve(family, bold, italic);

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
            ? FillReader.Read(fillElement, Colors, slide.Images(part), slide.ImageSizes(part))
            : null;
        var lineElement = RunElement(rPr, sources, level, "ln");
        var line = lineElement != null ? OutlineReader.Merge([(lineElement, Colors)]) : null;
        if (line is { Visible: false }) line = null;
        if (link != null)
        {
            if (hlink!.Descendants(HyperlinkColor + "hlinkClr").FirstOrDefault()?.Str("val") != "tx")
                color = Colors.Resolve(new XElement(Ns.A + "schemeClr", new XAttribute("val", "hlink")));
            if (rPr!.Attribute("u") == null) underline = "sng";
            paint = null;
        }
        // Superscripts and subscripts are drawn at two thirds of the size, raised or lowered by the percentage of
        // the full size (measured).
        var baseline = int.TryParse(R("baseline"), out var b) ? b / 100000.0 : 0;
        var drawn = baseline != 0 ? size * 2 / 3 : size;
        return new TextStyle(
            resolved,
            drawn,
            color,
            bold,
            italic,
            size >= kern,
            int.TryParse(R("spc"), out var spc) ? spc / 100.0 : 0,
            underline,
            R("strike") is "sngStrike" or "dblStrike" ? R("strike") : null,
            baseline * size / drawn,
            RunElement(rPr, sources, level, "highlight") is { } h ? Colors.ResolveChild(h) : null,
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
            "solidFill" => Colors.ResolveChild(fill) ?? RgbColor.Black,
            "noFill" => RgbColor.Black.WithAlpha(0),
            "gradFill" => Colors.ResolveChild(fill.Element(Ns.A + "gsLst")?.Element(Ns.A + "gs")) ?? RgbColor.Black,
            _ => Colors.Resolve(new XElement(Ns.A + "schemeClr", new XAttribute("val", "tx1"))),
        };
    }

    string? Typeface(XElement? font) =>
        font?.Str("typeface") is { Length: > 0 } t ? slide.Theme.ResolveTypeface(t) is { Length: > 0 } r ? r : null : null;

}
