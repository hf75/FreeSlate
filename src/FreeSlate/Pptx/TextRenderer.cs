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

    /// <summary>Text defaults ahead of the master's styles: a chart's for the shapes drawn on it.</summary>
    public XElement? Defaults { get; init; }

    /// <summary>The shape's p:style/a:fontRef (default text color and theme font).</summary>
    public XElement? FontRef { get; init; }

    /// <summary>
    /// Every character drawn on its own and marked with its place in the text and its word (data-c, data-w): the
    /// slide show builds the text letter by letter or word by word (p:iterate, catalog: animation.timing).
    /// </summary>
    public bool Units { get; init; }

    /// <summary>Character formatting that overrides the text's own: the text as an animation restyles it.</summary>
    public TextOverride? Override { get; init; }

    /// <summary>The same text in another formatting.</summary>
    public TextChain With(TextOverride formatting) =>
        new() { Shapes = Shapes, StyleKind = StyleKind, Defaults = Defaults, FontRef = FontRef, Units = Units, Override = formatting };
}

/// <summary>
/// Character formatting an animation gives text (catalog: animation.behaviors: style.fontWeight, style.fontStyle,
/// style.textDecorationUnderline, style.fontFamily, style.fontSize); null keeps the text's own.
/// </summary>
public sealed record TextOverride(bool? Bold, bool? Italic, bool? Underline, string? Family, double Size = 1)
{
    /// <summary>The player's name for the formatting: weight|style|underline|family|size.</summary>
    public string Key =>
        $"{(Bold is { } b ? b ? "bold" : "normal" : "")}|{(Italic is { } i ? i ? "italic" : "normal" : "")}|" +
        $"{(Underline is { } u ? u ? "true" : "false" : "")}|{Family}|{(Size == 1 ? "" : Size.ToString("0.####", CultureInfo.InvariantCulture))}";
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

    /// <summary>
    /// The colors and theme (theme fonts too) the text resolves against: the slide's, or <see cref="ColorOverride"/>
    /// (measured: a chart's theme override brings its own fonts, Calibri in a Lucida Sans deck).
    /// </summary>
    ColorContext Colors => ColorOverride ?? slide.Colors;

    /// <summary>A chart's own color mapping and theme while the chart is drawn.</summary>
    public ColorContext? ColorOverride { get; set; }

    /// <summary>The shape's box in pixels: scaled or skewed shadows of its text align on it, as the shape's own do.</summary>
    public (double Width, double Height)? ShapeSize { get; set; }

    int nextClip;

    static readonly XNamespace HyperlinkColor = "http://schemas.microsoft.com/office/drawing/2018/hyperlinkcolor";

    const double PxPerPt = 96.0 / 72;
    static readonly string[] FillNames = ["noFill", "solidFill", "gradFill", "blipFill", "pattFill", "grpFill"];

    abstract record Source;
    sealed record ListStyleSource(XElement ListStyle) : Source;
    sealed record FontRefSource(XElement FontRef) : Source;
    /// <summary>A list style that gives only the text's fill (see <see cref="BuildSources"/>).</summary>
    sealed record FillSource(XElement ListStyle) : Source;

    /// <summary>a:tcTxStyle of a table style part: b/i as on/off, a font or font reference and a color.</summary>
    sealed record TableTextSource(XElement Style) : Source;

    /// <summary>SVG markup for the text of a shape (shape-local pixel coordinates), or "".</summary>
    /// <summary>
    /// When set, the outlines of the glyphs drawn (pixels, one entry per glyph) with their color are collected: 3D text
    /// (catalog: text.threeD) gives them a body.
    /// </summary>
    public List<(List<List<(double X, double Y)>> Contours, RgbColor Color)>? Outlines { get; set; }

    /// <summary>Whether the text being rendered is drawn character by character (see <see cref="TextChain.Units"/>).</summary>
    bool units;

    /// <summary>The formatting the text being rendered takes instead of its own (see <see cref="TextChain.Override"/>).</summary>
    TextOverride? runOverride;

    public string Render(TextChain chain, EvaluatedGeometry geometry, double boxWidth, bool unflip)
    {
        units = chain.Units;
        runOverride = chain.Override;
        try
        {
            var markup = RenderText(chain, geometry, boxWidth, unflip);
            // a:bodyPr rot turns the laid-out text about the middle of the text rectangle (LibreOffice's tdf149551).
            var rotation = chain.Shapes.Select(s => TxBody(s.Element)?.Element(Ns.A + "bodyPr")?.Attribute("rot")?.Value)
                .FirstOrDefault(v => v != null) is { } rot && long.TryParse(rot, out var angle) ? angle / 60000.0 : 0;
            if (markup.Length > 0 && Math.Abs(rotation) > 1e-9)
                markup = $"<g transform=\"rotate({Units.Css(rotation)} {Units.Css((geometry.TextLeft + geometry.TextRight) / 2)} {Units.Css((geometry.TextTop + geometry.TextBottom) / 2)})\">{markup}</g>";
            return markup;
        }
        finally
        {
            units = false;
            runOverride = null;
        }
    }

    string RenderText(TextChain chain, EvaluatedGeometry geometry, double boxWidth, bool unflip)
    {
        var txBody = TxBody(chain.Shapes[0].Element);
        if (txBody == null || !txBody.Elements(Ns.A + "p").Any(p => p.Elements().Any(e => e.Name.LocalName is "r" or "fld" || e.Name == Equations.Equation.A14 + "m")))
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
        // A text area narrower or lower than its insets collapses to its middle (measured with the object model: a
        // box 5 pt wide with insets of 10 and 2 pt sets its lines at 6.5 pt, a box 1 pt high 0.5 pt below its top).
        if (width < 0)
            (left, width) = (left + width / 2, 0);
        if (height < 0)
            (top, height) = (top + height / 2, 0);
        // A placeholder without width does not wrap its text (measured: one line in footer, body and title
        // placeholders 0 wide; text boxes and shapes 0 wide, and placeholders 1 pt wide, wrap every character).
        var wraps = Body("wrap") != "none" && !(boxWidth == 0 && Placeholders.Ph(chain.Shapes[0].Element) != null);
        // Vertical text (catalog: text.vertical); the insets stay with the shape's sides.
        if (Body("vert") is "vert" or "vert270" or "eaVert" or "mongolianVert")
            return RenderTurned(txBody, sources, Body("vert")!, left, top, width, height, wraps, fontScale, spacingReduction, Body("anchor") ?? "t");
        if (Body("vert") is "wordArtVert" or "wordArtVertRtl")
            return RenderStacked(Layout(txBody, sources, 1e6, false, fontScale, spacingReduction), Body("vert") == "wordArtVertRtl", left, top, width, height, Body("anchor") ?? "t");
        // Columns (catalog: text.columns): the text is laid out at the column width and its lines are shared out
        // evenly over the columns (measured: 33 lines in three columns become 11, 11 and 11, even when they overflow).
        var columns = Math.Clamp(int.TryParse(Body("numCol"), out var columnCount) ? columnCount : 1, 1, 16);
        var columnGap = (long.TryParse(Body("spcCol"), out var gap) ? gap : 0) / Units.EmuPerPoint;
        var columnWidth = columns > 1 ? Math.Max(0, (width - (columns - 1) * columnGap) / columns) : width;
        // WordArt transforms bend the glyph outlines into the preset's paths in the shape's text rectangle; text following
        // paths (arches, circle, button) is not wrapped (measured: a long line runs once around the circle, and stays
        // whole on the button's lower arc).
        var warp = bodies.Select(b => b?.Element(Ns.A + "prstTxWarp")).FirstOrDefault(e => e != null);
        var warpDefinition = warp != null ? TextWarp.Definition(warp) : null;
        var wrap = wraps && !(warpDefinition != null && TextWarp.FollowsPaths(warpDefinition));
        // Space before the first paragraph and after the last counts only when the body asks for it (measured).
        var spaceFirstLast = Body("spcFirstLastPara") is "1" or "true";
        var layout = Layout(txBody, sources, Math.Max(0, columnWidth), wrap, fontScale, spacingReduction, spaceFirstLast);
        if (columns > 1)
            layout = Columns(layout, columns, columnWidth, columnGap, Body("rtlCol") is "1" or "true", height);
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
        if (warp != null && warpDefinition is { } definition)
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
                // East Asian characters stand upright, and digits of runs that ask for it (a:rPr kumimoji; measured).
                bool Stands(char c) => Scripts.Upright(c) || run.Style.UprightDigits && char.IsAsciiDigit(c);
                if (run.Picture != null || !run.Text.Any(Stands))
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
                    var upright = i < run.Text.Length && Stands(run.Text[i]);
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

    /// <summary>
    /// The lines of a layout distributed over columns, each column starting at the top: a column takes the lines
    /// that fit the text area's height, but at least an even share, and the last column the rest (measured with
    /// the object model: 7 lines in 2 columns 5 lines high are 5 and 2, 3 lines high 4 and 3; 12 lines in 3 columns
    /// 5 lines high 5, 5 and 2, 3 lines high 4, 4 and 4).
    /// </summary>
    static LaidOutText Columns(LaidOutText layout, int columns, double columnWidth, double gap, bool rightToLeft, double areaHeight)
    {
        var lines = layout.Lines;
        if (lines.Count == 0)
            return layout;
        var share = (int)Math.Ceiling(lines.Count / (double)columns);
        var result = new LaidOutText();
        double height = 0;
        var start = 0;
        for (var c = 0; c < columns && start < lines.Count; c++)
        {
            var columnTop = lines[start].Baseline - lines[start].Ascent;
            var end = start + 1;
            if (c == columns - 1)
                end = lines.Count;
            else
                while (end < lines.Count && (end - start < share || lines[end].Baseline + lines[end].Descent - columnTop <= areaHeight + 0.01))
                    end++;
            var dx = (rightToLeft ? columns - 1 - c : c) * (columnWidth + gap);
            for (var i = start; i < end; i++)
            {
                var line = lines[i];
                var moved = new LaidOutLine
                {
                    Paragraph = line.Paragraph, Ascent = line.Ascent, Descent = line.Descent, Width = line.Width, Align = line.Align,
                    Baseline = line.Baseline - columnTop, ParagraphText = line.ParagraphText,
                };
                moved.Runs.AddRange(line.Runs.Select(r => r with { X = r.X + dx }));
                result.Lines.Add(moved);
            }
            height = Math.Max(height, lines[end - 1].Baseline + lines[end - 1].Descent - columnTop);
            start = end;
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
        // Word bends the lines of its own WordArt where it laid them out, not on whole pixels: snapped baselines,
        // stretched with the glyphs, would stand up to 0.75 pt of layout apart from Word's (docx/local/wap-1: Arial
        // 20 pt lines 23 pt apart drawn 23.25 pt apart, 3.6 times that in the shape). Text converted from old WordArt
        // keeps the pixels its lines were measured with (docx/local/vwa-*).
        double Baseline(double at) => slide.Presentation.Word && !fromWordArt ? at : Snap(at);
        var glyphs = new List<TextWarp.Glyph>();
        var owners = new List<TextStyle>();
        var selection = new StringBuilder();
        for (var lineIndex = 0; lineIndex < layout.Lines.Count; lineIndex++)
        {
            var line = layout.Lines[lineIndex];
            var baseline = top + Baseline(offset + line.Baseline);
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
                        glyphs.Add(new TextWarp.Glyph(TextWarp.Contours(outline, x, y, scale, slide.Presentation.Word), lineIndex, baseline * PxPerPt));
                        owners.Add(style);
                    }
                    position += (FontData.MasterUnits(g.Advance * style.Size) + FontData.MasterUnits(g.Kerning * style.Size)) * PxPerPt + spacing;
                }
                selection.Append($"<text class=\"sel\" x=\"{Px(left + run.X)}\" y=\"{Px(baseline)}\" font-family=\"'{WebUtility.HtmlEncode(data.Family.Replace("'", "\\'"))}'\" font-size=\"{Px(style.Size)}\">{WebUtility.HtmlEncode(text)}</text>");
            }
        }
        // VML WordArt that trims its text centres each line by its ink, spaces before and after it inked over their
        // advance (measured with docx/local/wtu-*: "11" and "rr" under "HHHHHH" with their ink's middle on the shape's,
        // in textPlain and textSlantUp; wts-*, wtu-*-lead1, -lead3: "HH" with one and three spaces after it 8.6 and 21.9
        // pt further left, before it as far right; docx/external/libreoffice/extras-ooxmlexport/
        // testWordArtWithinDraingtool: "program and "); untrimmed text keeps its advances (vwa-calibri2: "Air" over
        // "plane" in Calibri). Lines along paths (arches) are left as they are.
        if (fromWordArt && slide.Presentation.Word && warp.Attribute(TextWarp.Trimmed) != null && !TextWarp.FollowsPaths(definition))
        {
            for (var l = 0; l < layout.Lines.Count; l++)
            {
                var line = layout.Lines[l];
                var members = Enumerable.Range(0, glyphs.Count).Where(i => glyphs[i].Line == l && glyphs[i].Contours.Any(c => c.Count > 0)).ToList();
                if (line.Align != "ctr" || members.Count == 0 || line.Runs.Count == 0)
                    continue;
                var points = members.SelectMany(i => glyphs[i].Contours).SelectMany(c => c).ToList();
                var (inkStart, inkEnd) = (points.Min(p => p.X), points.Max(p => p.X));
                var (first, last) = (line.Runs.MinBy(r => r.X)!, line.Runs.MaxBy(r => r.X + r.Width)!);
                if (first.Text.Length > 0 && char.IsWhiteSpace(first.Text[0]))
                    inkStart = Math.Min(inkStart, (left + first.X) * PxPerPt);
                if (last.Text.Length > 0 && char.IsWhiteSpace(last.Text[^1]))
                    inkEnd = Math.Max(inkEnd, (left + last.X + last.Width) * PxPerPt);
                var shift = (left + line.Runs.Min(r => r.X) + line.Width / 2) * PxPerPt - (inkStart + inkEnd) / 2;
                foreach (var contour in members.SelectMany(i => glyphs[i].Contours))
                {
                    for (var k = 0; k < contour.Count; k++)
                        contour[k] = (contour[k].X + shift, contour[k].Y);
                }
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
        // Every line's baseline and how far below a single path it lies when it is the line next to it: 0.79 em under
        // the top of its line box (measured with eight fonts on circles, 12 to 72 pt); and its line box.
        var lines = layout.Lines.Select(l =>
        {
            var baseline = (top + Baseline(offset + l.Baseline)) * PxPerPt;
            return new TextWarp.Line(baseline, (l.Ascent - 0.79 * (l.Runs.Count > 0 ? l.Runs.Max(r => r.Style.Size) : l.Ascent / 1.2 / 0.8)) * PxPerPt,
                baseline - l.Ascent * PxPerPt, baseline + l.Descent * PxPerPt);
        }).ToList();
        var warped = TextWarp.Apply(definition, adjustments, box, glyphs, fromWordArt, Alignment, lines);
        for (var i = 0; i < warped.Count && Outlines != null; i++)
            Outlines.Add((warped[i], (owners[i].Paint is { } warpedPaint ? Fill.MainColor(warpedPaint) : null) ?? owners[i].Color));

        var svg = new StringBuilder("<g data-tx=\"\">");
        var defs = new StringBuilder();
        // Shadows, glow and inner shadows fall on the warped glyphs, sized with their line's largest font.
        var lineSizes = layout.Lines.Select(l => l.Runs.Count > 0 ? l.Runs.Max(r => r.Style.Size) : 0).ToList();
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
            // A VML WordArt shape's effects keep the shape's sizes (measured with docx/local/vps-wa-plain, vps-wa-4: its
            // shadow 2 pt off by default and 4 pt where it says so, not scaled with the text).
            SvgEffects.TextScale? scale = style.Effects?.Attribute(TextWarp.ShapeEffects) != null ? null : SvgEffects.TextScale.Of(lineSizes[glyphs[i].Line]);
            // A scaled or skewed shadow of VML WordArt is a transformed copy of its glyphs behind them, about the
            // shape's corner or side (docx/local/vps-*; LibreOffice's extras-ooxmlexport/fdo78300: a perspective shadow
            // half as high and skewed left under "Homer was sleeping").
            if (fromWordArt && slide.Presentation.Word && style.Effects is { } skewed && SvgEffects.Shadow(skewed, Colors, box.W, box.H, defs) is { } copy)
                svg.Append($"<g transform=\"translate({Units.Css(box.L)},{Units.Css(box.T)})\"><g filter=\"url(#{copy.Filter})\"><g transform=\"{copy.Transform.Css()}\">" +
                           $"<path transform=\"translate({Units.Css(-box.L)},{Units.Css(-box.T)})\" d=\"{TextWarp.PathData(contours)}\"{fill}/></g></g></g>");
            // On a printed page (Word) shadows and glow lie behind a copy of the glyphs and the glyphs themselves stay
            // unfiltered on top, as a page's runs do (PageRenderer.WithEffects): a filtered path is printed as a
            // picture (docx/external/libreoffice/extras-ooxmlexport/fdo80897: hollow WordArt with a shadow).
            if (style.Effects is { } own && slide.Presentation.Word
                && SvgEffects.Filter(own, Colors, 0, 0, new StringBuilder(), boundingBox: true, text: scale, parts: SvgEffects.Parts.Shape) == null
                && SvgEffects.Filter(own, Colors, 0, 0, defs, boundingBox: true, text: scale, parts: SvgEffects.Parts.Outer) is { } outer)
            {
                var id = Paint.NewId("wa");
                svg.Append($"<use href=\"#{id}\" filter=\"url(#{outer})\"/><path id=\"{id}\" d=\"{TextWarp.PathData(contours)}\"{fill}{stroke}/>");
                i = j;
                continue;
            }
            var filter = style.Effects is { } effects ? SvgEffects.Filter(effects, Colors, 0, 0, defs, boundingBox: true, text: scale) : null;
            svg.Append($"<path d=\"{TextWarp.PathData(contours)}\"{fill}{stroke}{(filter != null ? $" filter=\"url(#{filter})\"" : "")}/>");
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
        var filters = new Dictionary<(XElement, double), string?>();
        // The point that stays where it is when an animation changes the font size (style.fontSize): the text's
        // anchor down, the first line's alignment across.
        var anchorAt = "";
        if (layout.Lines.Count > 0 && layout.Lines[0].Runs.Count > 0)
        {
            var first = layout.Lines[0];
            var start = left + first.Runs[0].X;
            var x = first.Align switch { "ctr" => start + first.Width / 2, "r" => start + first.Width, _ => start };
            var y = top + anchor switch { "ctr" => height / 2, "b" => height, _ => 0 };
            anchorAt = $" data-anchor=\"{Px(x)} {Px(y)}\"";
        }
        svg.Append(unflipWidth is { } w ? $"<g data-tx=\"\" transform=\"matrix(-1 0 0 1 {Units.Css(w)} 0)\">" : $"<g data-tx=\"\"{anchorAt}>");
        // Text drawn character by character: every paragraph's start in the whole text (paragraphs end with a break,
        // as PowerPoint counts them) and every character's word.
        var paragraphStarts = new Dictionary<int, int>();
        var words = Array.Empty<int>();
        if (units)
        {
            var body = new StringBuilder();
            foreach (var group in layout.Lines.GroupBy(l => l.Paragraph).OrderBy(g => g.Key))
            {
                paragraphStarts[group.Key] = body.Length;
                body.Append(group.First().ParagraphText).Append('\n');
            }
            // The text the units index (morph pairs words and letters by it).
            svg.Replace("<g data-tx=\"\"", $"<g data-tx=\"\" data-text=\"{WebUtility.HtmlEncode(body.ToString())}\"", 0, svg.Length);
            words = new int[body.Length];
            for (int i = 0, word = -1; i < body.Length; i++)
            {
                if (char.IsWhiteSpace(body[i]))
                {
                    words[i] = -1;
                    continue;
                }
                if (i == 0 || char.IsWhiteSpace(body[i - 1]))
                    word++;
                words[i] = word;
            }
        }
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
            // Text effects take their size from the line's largest font (measured: a 40 pt run beside a 120 pt run
            // casts the 120 pt run's shadow; see SvgEffects.TextScale).
            var effectSize = line.Runs.Count > 0 ? line.Runs.Max(r => r.Style.Size) : 0;
            var effectScale = SvgEffects.TextScale.Of(effectSize);
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
                {
                    if (BetweenWords(line, run))
                        svg.Append(SpaceDecorations(run, left + run.X, run.Style.BaselineShift == 0 && run.Lift == 0
                            ? baseline
                            : top + Snap(offset + line.Baseline + shiftBy - run.Style.BaselineShift * run.Style.Size - run.Lift)));
                    continue;
                }
                if (run.Style.Inline is Equations.Equation equation)
                {
                    // An equation: its glyphs and rules, its characters transparent on top for selection.
                    var (drawn, mathText) = equation.Draw(left + run.X, baseline - run.Lift, slide.Presentation.Glyphs, PxPerPt);
                    svg.Append(drawn);
                    if (mathText.Length > 0)
                        svg.Append($"<text class=\"sel\" x=\"{Px(left + run.X)}\" y=\"{Px(baseline - run.Lift)}\" font-size=\"{Px(run.Style.Size)}\" textLength=\"{Px(equation.Width)}\" lengthAdjust=\"spacingAndGlyphs\">{WebUtility.HtmlEncode(mathText)}</text>");
                    continue;
                }
                var filter = run.Style.Effects is { } effects
                    ? filters.TryGetValue((effects, effectSize), out var known)
                        ? known
                        : filters[(effects, effectSize)] = SvgEffects.Filter(effects, Colors, 0, 0, defs, boundingBox: true, text: effectScale)
                    : null;
                var shifted = run.Style.BaselineShift == 0 && run.Lift == 0 ? baseline : top + Snap(offset + line.Baseline + shiftBy - run.Style.BaselineShift * run.Style.Size - run.Lift);
                // Gradient and pattern fills span the line (measured: every line of a two-line text box starts the
                // gradient anew, from the line's start to its end and from its ascent to its descent).
                var box = (left + (line.Runs.Count > 0 ? line.Runs[0].X : 0), baseline - line.Ascent, line.Width, line.Ascent + line.Descent);
                if (units && run.Start >= 0 && !run.RightToLeft && !run.Upright
                    && Characters(run, left + run.X - (left + formatOrigin)) is { } characters)
                {
                    foreach (var (piece, at, from) in characters)
                    {
                        var index = paragraphStarts.GetValueOrDefault(line.Paragraph) + run.Start + at;
                        var word = index < words.Length ? words[index] : -1;
                        svg.Append($"<g data-c=\"{index}\" data-w=\"{word}\">{Text(piece, left + piece.X, shifted, filter, left + formatOrigin, box, defs, from, effectSize)}</g>");
                    }
                    continue;
                }
                // A shadow that scales or skews the text (outerShdw sx, sy, kx, ky) is a transformed copy of the run behind
                // it, about the alignment point of the shape's box (measured: 110 % about its bottom middle), else of the
                // run's box on its line (as for shapes, see SvgEffects.Shadow).
                if (run.Style.Effects is { } runEffects && !run.Upright)
                {
                    var (bx, by, bw, bh) = ShapeSize is { } shape
                        ? (0.0, 0.0, shape.Width, shape.Height)
                        : ((left + run.X) * PxPerPt, (baseline - line.Ascent) * PxPerPt, run.Width * PxPerPt, (line.Ascent + line.Descent) * PxPerPt);
                    if (SvgEffects.Shadow(runEffects, Colors, bw, bh, defs, scale: effectScale.Outer) is { } copy)
                        svg.Append($"<g transform=\"translate({Units.Css(bx)} {Units.Css(by)})\"><g filter=\"url(#{copy.Filter})\"><g transform=\"{copy.Transform.Css()}\">" +
                                   $"<g transform=\"translate({Units.Css(-bx)} {Units.Css(-by)})\">{Text(run, left + run.X, shifted, null, left + formatOrigin, box, defs)}</g></g></g></g>");
                }
                var markup = Text(run, left + run.X, shifted, filter, left + formatOrigin, box, defs, effectSize: effectSize);
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
        var counters = new Numbering();
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
            else if (BetweenWords(line, run))
                svg.Append(SpaceDecorations(run, left + run.X, baseline - run.Style.BaselineShift * run.Style.Size));
        }
        return svg.ToString();
    }

    /// <summary>Whether a run of spaces stands before more of the line's text (not at its end).</summary>
    static bool BetweenWords(LaidOutLine line, PlacedRun run)
    {
        var index = line.Runs.IndexOf(run);
        for (var i = index + 1; i < line.Runs.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(line.Runs[i].Text) || line.Runs[i].Picture != null)
                return true;
        }
        return false;
    }

    /// <summary>
    /// The underline and strikethrough of spaces between words, which PowerPoint draws through (measured: "Math Problem
    /// Solving" underlined in one piece); spaces ending a line stay bare.
    /// </summary>
    static string SpaceDecorations(PlacedRun run, double x, double y)
    {
        var style = run.Style;
        if (style.Underline == null && style.Strike == null)
            return "";
        var decorations = Decorations(run, style, style.Font.Data, x, y);
        var fill = $" fill=\"{style.Color.ToHex()}\"" + (style.Color.A < 0.999 ? $" fill-opacity=\"{Units.Css4(style.Color.A)}\"" : "");
        return decorations.Length > 0 ? $"<g{fill}>{decorations}</g>" : "";
    }

    LaidOutText Layout(XElement txBody, List<Source> sources, double width, bool wrap, double fontScale, double spacingReduction,
        bool spaceFirstLast = false)
    {
        var counters = new Numbering();
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
    /// <param name="from">Where the run starts from the format run's start (points), as the format run accumulates it;
    /// by default x - origin.</param>
    /// <param name="effectSize">The largest font size of the line, which sizes its effects (by default the run's).</param>
    string Text(PlacedRun run, double x, double y, string? filter = null, double? origin = null,
        (double X, double Y, double W, double H)? lineBox = null, StringBuilder? defs = null, double? from = null, double? effectSize = null)
    {
        var style = run.Style;
        var data = style.Font.Data;
        var text = run.Style.AllCaps ? run.Text.ToUpperInvariant() : run.Text;
        var glyphs = data.Glyphs(text, style.Kerning, run.RightToLeft);
        var start = origin ?? x;
        var startPx = start * PxPerPt;

        // Glyph positions in whole pixels from the format run's start; characters take their cluster's position.
        var glyphX = new double[glyphs.Length];
        var clusterX = new double?[text.Length];
        var spacing = FontData.MasterUnits(style.LetterSpacing);
        var position = from ?? x - start;
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
        // 3D text (Outlines collected) needs the glyphs' outlines at every size.
        var outlined = (style.Size * PxPerPt >= HintedBelow || painted || Outlines != null) && !data.IsColor && !style.Font.SyntheticBold && !style.Font.SyntheticItalic
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
                Outlines?.Add((TextWarp.Contours(data.Outline(glyphs[i].Id), startPx + glyphX[i] + glyphs[i].OffsetX * style.Size * PxPerPt,
                    y * PxPerPt - glyphs[i].OffsetY * style.Size * PxPerPt, scale), (style.Paint is { } glyphPaint ? Fill.MainColor(glyphPaint) : null) ?? style.Color));
                var gx = glyphX[i] / scale + glyphs[i].OffsetX * data.UnitsPerEm;
                var gy = -glyphs[i].OffsetY * data.UnitsPerEm;
                units.Append($"<use href=\"#{id}\"");
                if (style.NormalizeHeight && data.GlyphBounds(glyphs[i].Id) is var (bottom, top) && top > bottom)
                {
                    // Equalized character height (a:rPr normalizeH): the glyph's ink from the baseline to the font's
                    // ascent (measured: every letter of 32 pt Calibri 40 px tall, descenders lifted onto the baseline).
                    var k = data.WinAscent * data.UnitsPerEm / (top - bottom);
                    units.Append($" transform=\"matrix(1 0 0 {Units.Css4(k)} {Units.Css1(gx)} {Units.Css1(gy + bottom * k)})\"/>");
                    continue;
                }
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
            if (Reflect(sb.ToString(), local) is { } mirrored)
                sb.Clear().Append(mirrored);
            if (defs == null && local.Length > 0)
                sb.Insert(0, $"<defs>{local}</defs>");
        }
        else
        {
            slide.Presentation.UsedFonts.Add(family);
        }

        // Right-to-left text goes to the browser whole, from its right end, so that it is shaped and reads in order.
        var placement = run.RightToLeft
            ? $" x=\"{Units.Css(x * PxPerPt + run.Width * PxPerPt)}\" direction=\"rtl\" unicode-bidi=\"bidi-override\""
            : $" x=\"{xs}\"";
        sb.Append($"<text{(outlined ? " class=\"sel\"" : "")}{placement} y=\"{Px(y)}\" font-family=\"'{WebUtility.HtmlEncode(family.Replace("'", "\'"))}'\" font-size=\"{Px(style.Size)}\"");
        // Simulated bold of a PostScript font only widens the text; its glyphs stay regular (measured).
        if (style.Bold && !style.Font.WidenedBold) sb.Append(" font-weight=\"700\"");
        if (style.Italic) sb.Append(" font-style=\"italic\"");
        if (!outlined)
        {
            sb.Append(fill);
            var decorations = new List<string>();
            if (style.Underline != null) decorations.Add("underline");
            if (style.Strike != null) decorations.Add("line-through");
            if (decorations.Count > 0) sb.Append($" text-decoration=\"{string.Join(' ', decorations)}\"");
            // The browser draws small text's underline: its style, color and width as CSS (catalog: text.underline).
            var css = new List<string>();
            if (style.Underline is { } underline)
            {
                var kind = underline.Replace("Heavy", "", StringComparison.Ordinal);
                if ((style.UnderlineDash ?? kind) switch
                    {
                        "dotted" or "dot" or "sysDot" => "dotted",
                        "dash" or "dashLong" or "dotDash" or "dotDotDash" or "sysDash" or "lgDash" or "dashDot" or "lgDashDot" or "lgDashDotDot" or "sysDashDot" or "sysDashDotDot" => "dashed",
                        "wavy" or "wavyDbl" => "wavy",
                        "dbl" => "double",
                        _ => null,
                    } is { } decorationStyle)
                    css.Add($"text-decoration-style:{decorationStyle}");
                if (style.UnderlineColor is { } underlineColor)
                    css.Add($"text-decoration-color:{underlineColor.ToHex()}");
                if (style.UnderlineWidth is { } underlineWidth)
                    css.Add($"text-decoration-thickness:{Units.Css(underlineWidth * PxPerPt)}px");
                else if (underline != kind || underline == "heavy")
                    css.Add("text-decoration-thickness:0.15em");
            }
            if (!style.Kerning) css.Add("font-kerning:none");
            if (css.Count > 0) sb.Append($" style=\"{string.Join(';', css)}\"");
            if (filter != null) sb.Append($" filter=\"url(#{filter})\"");
        }
        sb.Append('>').Append(WebUtility.HtmlEncode(text)).Append("</text>");
        if (!outlined)
        {
            var local = defs ?? new StringBuilder();
            if (Reflect(sb.ToString(), local) is { } reflected)
            {
                sb.Clear();
                if (defs == null && local.Length > 0) sb.Append($"<defs>{local}</defs>");
                sb.Append(reflected);
            }
        }
        // A mouse-over action on the text (a:hlinkMouseOver, catalog: interactivity.hyperlinks) runs when the pointer
        // enters it during the show, like a shape's.
        if (style.HoverLink is { } hover)
        {
            var attributes = (hover.Action != null ? $" data-hover-action=\"{WebUtility.HtmlEncode(hover.Action)}\"" : "")
                             + (hover.Href != null ? $" data-hover-href=\"{WebUtility.HtmlEncode(hover.Href)}\"" : "");
            sb.Insert(0, $"<g{attributes}>").Append("</g>");
        }
        return style.Link is { } link ? Links.Open(link) + sb + "</a>" : sb.ToString();

        // Text reflects line by line about its baseline: the box mirrored about its bottom reaches from the font's
        // ascent down to half the reflection's distance below the baseline, sized with the line's largest font, and
        // the fade runs over the mirrored box (measured: below 80 pt text the gap is 5.5 times the distance and the
        // fade starts halfway into it; see SvgEffects.TextScale).
        string? Reflect(string markup, StringBuilder into)
        {
            if (style.Effects?.Element(Ns.A + "reflection") is not { } reflection)
                return null;
            var scale = SvgEffects.TextScale.Of(effectSize ?? style.Size);
            var below = Units.ToPx(reflection.Long("dist") ?? 0) * scale.ReflectionDistance / 2;
            var bottom = y * PxPerPt + below;
            var height = style.Size * PxPerPt * data.Ascent + below;
            return SvgEffects.Reflection(style.Effects, markup, (x * PxPerPt, bottom - height, run.Width * PxPerPt, height), into,
                distanceScale: 0, blurScale: scale.ReflectionBlur);
        }
    }

    /// <summary>
    /// A run split into its characters for builds letter by letter: every non-blank character a run of its own, with
    /// its offset in the run and where it starts from the format run's start, accumulated as <see cref="Text"/> does
    /// from <paramref name="from"/> (the same pixel rounding); null where characters and glyphs do not map one to one
    /// (ligatures, marks), which keeps the run whole.
    /// </summary>
    static List<(PlacedRun Piece, int At, double From)>? Characters(PlacedRun run, double from)
    {
        var style = run.Style;
        var text = style.AllCaps ? run.Text.ToUpperInvariant() : run.Text;
        var glyphs = style.Font.Data.Glyphs(text, style.Kerning, false);
        var spacing = FontData.MasterUnits(style.LetterSpacing);
        var starts = new double?[text.Length + 1];
        var position = from;
        foreach (var glyph in glyphs)
        {
            var first = glyph.Cluster < text.Length && starts[glyph.Cluster] == null;
            if (first)
                starts[glyph.Cluster] = position;
            position += FontData.MasterUnits(glyph.Advance * style.Size) + FontData.MasterUnits(glyph.Kerning * style.Size) + (first ? spacing : 0);
        }
        starts[text.Length] = position;
        var result = new List<(PlacedRun, int, double)>();
        for (var k = 0; k < text.Length;)
        {
            var length = char.IsHighSurrogate(text[k]) && k + 1 < text.Length ? 2 : 1;
            if (starts[k] is not { } at || starts[k + length] is not { } end)
                return null;
            var character = text.Substring(k, length);
            if (!string.IsNullOrWhiteSpace(character))
                result.Add((new PlacedRun(character, style, run.X + at - from, end - at) { Lift = run.Lift, Start = run.Start + k }, k, at));
            k += length;
        }
        return result;
    }

    /// <summary>Stroke attributes in pixels converted to the font units of a glyph group (width and dashes).</summary>
    static string StrokeInFontUnits(string attributes, double scale) =>
        System.Text.RegularExpressions.Regex.Replace(attributes, "(stroke-width|stroke-dasharray)=\"([^\"]+)\"", m =>
            $"{m.Groups[1].Value}=\"{string.Join(' ', m.Groups[2].Value.Split(' ').Select(v => Units.Css1(double.Parse(v, CultureInfo.InvariantCulture) / scale)))}\"");

    /// <summary>
    /// Underline and strikethrough of a run (pixels), from the font's post and OS/2 metrics, placed on whole pixels
    /// (catalog: text.underline). The underline styles repeat in units of the plain line's thickness (measured with
    /// generated/text-underline: dotted 1:1, dash 3:2, long dash 7:4, dot dash 3:2:1:2, dot dot dash 3:2:1:2:1:2);
    /// heavy ones are twice as thick, reaching up, with the same pattern; double ones are two lines of three quarters
    /// of the thickness, the second above the first; wavy ones are waves four units long. An underline line of its own
    /// (a:uLn) gives the thickness, color and dash, a:uFill the color.
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
        string Line(double top, double height, string fill = "", double[]? dashes = null, double unit = 1)
        {
            if (dashes == null)
                return $"<rect x=\"{Units.Css(left)}\" y=\"{Units.Css(top)}\" width=\"{Units.Css(width)}\" height=\"{Units.Css(height)}\"{fill}/>";
            // A dashed line: one rectangle per dash, the pattern from the run's start.
            var line = new StringBuilder();
            var (position, i) = (0.0, 0);
            while (position < width - 1e-6)
            {
                var length = dashes[i % dashes.Length] * unit;
                if (i % 2 == 0 && length > 0)
                    line.Append($"<rect x=\"{Units.Css(left + position)}\" y=\"{Units.Css(top)}\" width=\"{Units.Css(Math.Min(length, width - position))}\" height=\"{Units.Css(height)}\"{fill}/>");
                position += Math.Max(length, 1e-3);
                i++;
            }
            return line.ToString();
        }
        if (style.Underline is { } underline)
        {
            var exact = data.UnderlineThickness * size;
            var unit = Math.Max(1, Math.Round(exact));
            var thickness = style.UnderlineWidth is { } own ? Math.Max(1, Math.Round(own * PxPerPt)) : unit;
            // The post table's underline position is the line's top (measured: 26 pt Arial, top 3.7 px below the
            // baseline).
            var offset = data.UnderlinePosition * size;
            var top = Math.Round(baseline + offset);
            var fill = style.UnderlineColor is { } color
                ? color.A < 0.999 ? $" fill=\"{color.ToHex()}\" fill-opacity=\"{Units.Css4(color.A)}\"" : $" fill=\"{color.ToHex()}\""
                : "";
            var kind = underline.Replace("Heavy", "", StringComparison.Ordinal);
            var heavy = underline != kind || underline == "heavy";
            double[]? pattern = (style.UnderlineDash ?? kind) switch
            {
                "dotted" or "dot" or "sysDot" => [1, 1],
                "dash" or "sysDash" => [3, 2],
                "dashLong" or "lgDash" => [7, 4],
                "dotDash" or "dashDot" or "sysDashDot" or "lgDashDot" => [3, 2, 1, 2],
                "dotDotDash" or "sysDashDotDot" or "lgDashDotDot" => [3, 2, 1, 2, 1, 2],
                _ => null,
            };
            if (kind is "wavy" or "wavyDbl")
            {
                // Waves four plain thicknesses long (measured: 10 px at 26 pt Arial): a thin one swinging about three
                // quarters of the thickness, a heavy one about half with twice the stroke, a double one two flat waves.
                var (stroke, swing) = kind == "wavyDbl" ? (Math.Max(1, exact * 0.8), exact * 0.25)
                    : heavy ? (Math.Max(1, exact * 0.9), exact * 0.5) : (Math.Max(1, exact * 0.45), exact * 0.75);
                var waveColor = style.UnderlineColor is { } wc ? $" stroke=\"{wc.ToHex()}\"" : " stroke=\"currentColor\"";
                var centers = kind == "wavyDbl" ? new[] { top + exact * 0.6, top - exact * 0.6 } : heavy ? [top] : [top + exact / 2];
                foreach (var center in centers)
                {
                    var path = new StringBuilder($"M{Units.Css(left)} {Units.Css(center)}");
                    var period = 4 * exact;
                    for (var px = 0.0; px < width; px += period)
                    {
                        var (a, b) = (Math.Min(period / 2, width - px), Math.Min(period, width - px));
                        path.Append($"Q{Units.Css(left + px + a / 2)} {Units.Css(center - 2 * swing)} {Units.Css(left + px + a)} {Units.Css(center)}");
                        if (b > a)
                            path.Append($"Q{Units.Css(left + px + (a + b) / 2)} {Units.Css(center + 2 * swing)} {Units.Css(left + px + b)} {Units.Css(center)}");
                    }
                    sb.Append($"<path d=\"{path}\" fill=\"none\"{waveColor} stroke-width=\"{Units.Css(stroke)}\"/>");
                }
            }
            else if (kind == "dbl")
            {
                var thin = Math.Max(1, Math.Round(thickness * 0.75));
                var lower = top + thickness - thin;
                sb.Append(Line(lower, thin, fill));
                sb.Append(Line(lower - 2 * thin, thin, fill));
            }
            else if (style.UnderlineWidth is { } lineWidth && !heavy)
            {
                // A line of its own (a:uLn) outlines the underline: the plain underline is filled with uFill (or the
                // text's color) and stroked with uLn, half inside and half outside its edges and at least a pixel
                // (measured with Arial 40 pt and uLn of 0.25 to 6 pt: 0.25 pt adds a pixel row of line above and
                // below a fill of three rows, 2 pt leaves one row of fill between three rows of line, 3 pt covers
                // it; LibreOffice's text-underline slide).
                string Paint(RgbColor? c) => c is { } v
                    ? v.A < 0.999 ? $" fill=\"{v.ToHex()}\" fill-opacity=\"{Units.Css4(v.A)}\"" : $" fill=\"{v.ToHex()}\""
                    : "";
                var stroke = lineWidth * PxPerPt;
                var bottom = top + unit;
                if (style.UnderlineDash is { } dash && Dashes.Preset(dash) is { } lineDash)
                {
                    // Dashed: the outline as a path from the bottom left corner, where PowerPoint starts the pattern.
                    var strokeColor = style.UnderlineLineColor is { } sc ? $" stroke=\"{sc.ToHex()}\"" : " stroke=\"currentColor\"";
                    var dashUnit = Dashes.Unit(stroke);
                    sb.Append($"<path d=\"M{Units.Css(left)} {Units.Css(bottom)}V{Units.Css(top)}H{Units.Css(left + width)}V{Units.Css(bottom)}Z\"{Paint(style.UnderlineFillColor)}{strokeColor}" +
                              $" stroke-width=\"{Units.Css(Math.Max(1, stroke))}\" stroke-dasharray=\"{string.Join(' ', lineDash.Select(d => Units.Css(d * dashUnit)))}\"/>");
                }
                else
                {
                    var half = stroke / 2;
                    var (outerTop, outerBottom) = (Math.Floor(top - half + 0.5), Math.Ceiling(bottom + half));
                    var (innerTop, innerBottom) = (Math.Ceiling(top + half), Math.Floor(bottom - half + 0.5));
                    sb.Append(Line(outerTop, outerBottom - outerTop, Paint(style.UnderlineLineColor)));
                    if (innerBottom > innerTop)
                        sb.Append(Line(innerTop, innerBottom - innerTop, Paint(style.UnderlineFillColor)));
                }
            }
            else
            {
                // A heavy line reaches up from the plain one's bottom.
                var height = heavy ? Math.Max(1, Math.Round(2 * (style.UnderlineWidth is { } w2 ? w2 * PxPerPt : exact))) : thickness;
                sb.Append(Line(heavy ? top + thickness - height : top, height, fill, pattern, unit));
            }
        }
        if (style.Strike is { } strike)
        {
            var thickness = Math.Max(1, Math.Round(data.StrikeoutThickness * size));
            var offset = -data.StrikeoutPosition * size;
            if (strike == "dblStrike")
            {
                var gap = thickness;
                sb.Append(Line(Math.Round(baseline + offset - gap - thickness / 2), thickness));
                sb.Append(Line(Math.Round(baseline + offset + gap - thickness / 2), thickness));
            }
            else
            {
                sb.Append(Line(Math.Round(baseline + offset - thickness / 2), thickness));
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
        AddList(chain.Defaults);

        var txStyles = slide.Master?.Root?.Element(Ns.P + "txStyles");
        // Text outside title and body placeholders takes the presentation's default text style and only its color
        // from the master's other style (measured with the object model: a text box, a shape and dt, ftr and sldNum
        // placeholders without a layout placeholder take size, italics, font, spacing, alignment, indent and line
        // spacing from defaultTextStyle, the color from otherStyle, not its bold; table cells take otherStyle).
        if (chain.StyleKind == "other")
        {
            if (txStyles?.Element(Ns.P + "otherStyle") is { } other)
                sources.Add(new FillSource(other));
        }
        else
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
            if (source is FillSource fillSource)
            {
                if (names.Contains("solidFill") && Find(fillSource.ListStyle.Element(Ns.A + $"lvl{level + 1}pPr")?.Element(Ns.A + "defRPr")) is { } fill)
                    return fill;
                continue;
            }
            if (Find(Level(source, level)?.Element(Ns.A + "defRPr")) is { } inherited)
                return inherited;
        }
        return null;
    }

    ParagraphInput Paragraph(XElement p, List<Source> sources, double fontScale, double spacingReduction, Numbering counters)
    {
        var pPr = p.Element(Ns.A + "pPr");
        var level = Math.Clamp(pPr.Int("lvl") ?? 0, 0, 8);
        // The paragraph's own default run properties (a:pPr/a:defRPr) come before the list styles (measured: POI's
        // bug63290, a run without properties in the paragraph's red 40 pt).
        if (pPr?.Element(Ns.A + "defRPr") is { } defaults)
            sources = [new ListStyleSource(new XElement(Ns.A + "lstStyle", new XElement(Ns.A + $"lvl{level + 1}pPr", defaults))), .. sources];
        string? P(string name) => ParagraphAttr(pPr, sources, level, name);
        static double Points(string? emu) => long.TryParse(emu, out var v) ? v / Units.EmuPerPoint : 0;

        var runs = new List<TextRun>();
        string? displayAlign = null;
        foreach (var element in p.Elements())
        {
            switch (element.Name.LocalName)
            {
                case "r" or "fld":
                    // Every piece of one font slot (Latin, East Asian, complex script) gets its own font.
                    var t = element.Name.LocalName == "fld" ? FieldText(element) : element.Element(Ns.A + "t")?.Value ?? "";
                    var rPr = element.Element(Ns.A + "rPr");
                    // Characters of a symbol font (stored at U+F000 to U+F0FF) take the run's symbol font (a:sym).
                    var symbol = RunElement(rPr, sources, level, "sym")?.Str("typeface") is { Length: > 0 } sym ? sym : null;
                    foreach (var (part, symbolic) in symbol != null ? SymbolPieces(t) : [(t, false)])
                    {
                        if (symbolic)
                        {
                            runs.Add(new TextRun(part, Style(rPr, sources, level, fontScale, part, symbolFont: symbol)));
                            continue;
                        }
                        foreach (var (piece, slot, script) in Scripts.Split(part))
                            runs.Add(new TextRun(piece, Style(rPr, sources, level, fontScale, piece, slot, script)));
                    }
                    break;
                case "br":
                    runs.Add(new TextRun("\n", Style(element.Element(Ns.A + "rPr"), sources, level, fontScale, null)));
                    break;
                case "m" when element.Name.Namespace == Equations.Equation.A14:
                {
                    // An equation (catalog: text.math) stands in the line as one object; display equations (m:oMathPara)
                    // take lines of their own, justified by m:jc (centered by default) whatever the paragraph says.
                    var (equations, align) = Equations.Equation.Read(element, fonts,
                        rPr => Style(rPr, sources, level, fontScale, null) is var s ? new Equations.MathRunStyle(s.Size, s.Color) : default);
                    var lineStyle = Style(null, sources, level, fontScale, null);
                    for (var i = 0; i < equations.Count; i++)
                    {
                        if (i > 0 || align != null && runs.Count > 0 && runs[^1].Text != "\n")
                            runs.Add(new TextRun("\n", lineStyle));
                        runs.Add(new TextRun("￼", lineStyle with { Inline = equations[i] }));
                    }
                    displayAlign ??= align;
                    break;
                }
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
            Align = displayAlign ?? P("algn") ?? "l",
            RightToLeft = P("rtl") is "1" or "true",
            Kinsoku = P("eaLnBrk") is not ("0" or "false"),
            LatinBreak = P("latinLnBrk") is "1" or "true",
            HangingPunctuation = P("hangingPunct") is "1" or "true",
            MarginLeft = Points(P("marL")),
            MarginRight = Points(P("marR")),
            Indent = Points(P("indent")),
            Spacing = spacing,
            SpaceBefore = Space(ParagraphElement(pPr, sources, level, "spcBef")),
            SpaceAfter = Space(ParagraphElement(pPr, sources, level, "spcAft")),
            DefaultTabSize = long.TryParse(P("defTabSz"), out var tab) ? tab / Units.EmuPerPoint : 72,
            TabStops = (ParagraphElement(pPr, sources, level, "tabLst")?.Elements(Ns.A + "tab") ?? [])
                .Select(t => (Position: (t.Long("pos") ?? 0) / Units.EmuPerPoint, Align: t.Str("algn") ?? "l"))
                .OrderBy(t => t.Position).ToList(),
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

    /// <summary>
    /// Automatic numbering per level: a numbered paragraph continues the numbering of the paragraphs before it at its
    /// level while they count in the same scheme from the same start; another scheme or start begins anew (measured:
    /// LibreOffice's n90255, pairs of paragraphs in five schemes count 3 4, II III, C D, iv v, f) g)).
    /// </summary>
    sealed class Numbering
    {
        public const int Levels = 9;
        readonly int[] values = new int[Levels];
        readonly string?[] schemes = new string?[Levels];

        public void Reset(int level) => (values[level], schemes[level]) = (0, null);

        /// <summary>The number of the next paragraph at the level, numbered in the scheme from the start.</summary>
        public int Next(int level, string type, int start)
        {
            var scheme = $"{type} {start}";
            values[level] = values[level] == 0 || schemes[level] != scheme ? start : values[level] + 1;
            schemes[level] = scheme;
            return values[level];
        }
    }

    static Bullet? ResetBullet(int level, Numbering counters)
    {
        counters.Reset(level);
        return null;
    }

    static ParagraphSpace Space(XElement? spacing) =>
        spacing?.Element(Ns.A + "spcPts") is { } pts ? new ParagraphSpace(false, (pts.Long("val") ?? 0) / 100.0)
        : spacing?.Element(Ns.A + "spcPct") is { } pct ? new ParagraphSpace(true, (pct.Long("val") ?? 0) / 100000.0)
        : ParagraphSpace.None;

    Bullet? Bullet(XElement? pPr, List<Source> sources, int level, TextStyle textStyle, Numbering counters)
    {
        for (var i = level + 1; i < Numbering.Levels; i++)
            counters.Reset(i);
        var kind = ParagraphElement(pPr, sources, level, "buNone", "buChar", "buAutoNum", "buBlip");
        string text;
        switch (kind?.Name.LocalName)
        {
            case "buBlip" when kind.Element(Ns.A + "blip")?.Attribute(Ns.R + "embed")?.Value is { } embed
                               && slide.Images(linkPart ?? slide.SlidePart)(embed) is { } picture:
            {
                // A picture bullet (catalog: text.bullets).
                counters.Reset(level);
                var (w, h) = slide.ImageSizes(linkPart ?? slide.SlidePart)(embed, 0);
                var pictureSize = ParagraphElement(pPr, sources, level, "buSzTx", "buSzPct", "buSzPts");
                var factor = pictureSize?.Name.LocalName == "buSzPct" ? (pictureSize.Long("val") ?? 100000) / 100000.0 : 1;
                return new Bullet("", textStyle with { Size = textStyle.Size * factor }, picture, w > 0 && h > 0 ? w / h : 1);
            }
            case "buChar":
                text = kind.Str("char") ?? "•";
                counters.Reset(level);
                break;
            case "buAutoNum":
                text = AutoNumbering.Format(kind.Str("type") ?? "arabicPeriod", counters.Next(level, kind.Str("type") ?? "arabicPeriod", kind.Int("startAt") ?? 1));
                break;
            default:
                counters.Reset(level);
                return null;
        }

        var font = ParagraphElement(pPr, sources, level, "buFontTx", "buFont");
        var resolved = font?.Name.LocalName == "buFont" && Colors.Theme.ResolveTypeface(font.Str("typeface") ?? "") is { Length: > 0 } family
            ? fonts.ResolveTypeface(family, textStyle.Bold, textStyle.Italic)
            : textStyle.Font;
        // Numbering in other scripts (Chinese numerals, Arabic letters, Thai digits …) needs a font that has them:
        // PowerPoint numbers in MS Gothic (East Asian), Arial (Arabic, Hebrew) and Nirmala UI (Indic scripts), whatever
        // the theme says (measured with the lists deck's PDF; its Thai numbers are Leelawadee UI drawn larger than the
        // font's own, which the theme's Thai font comes closer to).
        if (!resolved.Data.Covers(text) && Scripts.Split(text) is [var (_, slot, script)])
        {
            var candidates = Scripts.Fallbacks(slot, slot == FontSlot.EastAsian ? Scripts.EastAsianScript(null, script) : script);
            var theme = slot switch
            {
                FontSlot.EastAsian => Colors.Theme.ScriptFonts.GetValueOrDefault((false, Scripts.EastAsianScript(null, script))),
                FontSlot.Complex when script != null => Colors.Theme.ScriptFonts.GetValueOrDefault((false, script)),
                _ => null,
            };
            var numbering = kind?.Name.LocalName != "buAutoNum" ? null : slot switch
            {
                FontSlot.EastAsian => "MS Gothic",
                FontSlot.Complex when script is "Thai" => null,
                FontSlot.Complex when script is "Arab" or "Hebr" => "Arial",
                FontSlot.Complex => "Nirmala UI",
                _ => null,
            };
            resolved = fonts.ResolveCovering(numbering ?? theme ?? candidates[0], textStyle.Bold, textStyle.Italic, text, candidates);
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

    /// <summary>A run's text in pieces of symbol font characters (U+F000 to U+F0FF) and other text.</summary>
    static List<(string Text, bool Symbol)> SymbolPieces(string text)
    {
        var pieces = new List<(string Text, bool Symbol)>();
        var start = 0;
        for (var i = 1; i <= text.Length; i++)
        {
            if (i == text.Length || IsSymbol(text[i]) != IsSymbol(text[start]))
            {
                pieces.Add((text[start..i], IsSymbol(text[start])));
                start = i;
            }
        }
        return pieces;
        static bool IsSymbol(char c) => c is >= '\uF000' and <= '\uF0FF';
    }

    TextStyle Style(XElement? rPr, List<Source> sources, int level, double fontScale, string? text,
        FontSlot slot = FontSlot.Latin, string? script = null, string? symbolFont = null)
    {
        string? R(string name) => RunAttr(rPr, sources, level, name);
        var size = double.TryParse(R("sz"), out var sz) ? sz / 100 : 18;
        // Autofit shrinks text to whole points, halves rounded up (measured at 90 %: 26 pt is laid out at 23 pt and
        // 25 pt, 22.5 scaled, at 23 pt too; at 62.5 %: 18 pt at 11 pt).
        if (fontScale != 1)
            size = Math.Max(1, Math.Floor(size * fontScale + 0.5 + 1e-9));
        size *= runOverride?.Size ?? 1;
        var bold = runOverride?.Bold ?? R("b") is "1" or "true";
        var italic = runOverride?.Italic ?? R("i") is "1" or "true";

        // The font of the slot (a:latin, a:ea or a:cs); empty East Asian and complex fonts come from the theme's
        // per-script fonts (by language for East Asian text); a font without the glyphs gives way to one that has
        // them (catalog: text.fonts).
        var latin = RunElement(rPr, sources, level, "latin");
        var major = latin?.Str("typeface") is "+mj-lt";
        var family = Typeface(latin) ?? Colors.Theme.MinorLatin;
        if (slot == FontSlot.EastAsian)
        {
            var themeScript = Scripts.EastAsianScript(R("lang"), script);
            family = Typeface(RunElement(rPr, sources, level, "ea"))
                     ?? Colors.Theme.ScriptFonts.GetValueOrDefault((major, themeScript))
                     ?? Scripts.Fallbacks(slot, themeScript)[0];
            script = themeScript;
        }
        else if (slot == FontSlot.Complex)
        {
            family = Typeface(RunElement(rPr, sources, level, "cs"))
                     ?? (script != null ? Colors.Theme.ScriptFonts.GetValueOrDefault((major, script)) : null)
                     ?? Scripts.Fallbacks(slot, script)[0];
        }
        if (symbolFont != null && Colors.Theme.ResolveTypeface(symbolFont) is { Length: > 0 } symbolFamily)
            family = symbolFamily;
        else if (runOverride?.Family is { Length: > 0 } animated)
            family = animated;
        var resolved = text != null
            ? fonts.ResolveCovering(family, bold, italic, text, Scripts.Fallbacks(slot, script))
            : fonts.ResolveTypeface(family, bold, italic);

        // kern: kerning applies from this size (hundredths of a point) up; without it PowerPoint does not kern.
        var kern = double.TryParse(R("kern"), out var k) ? k / 100 : double.MaxValue;
        // Hyperlinked text takes the theme's hyperlink color and an underline, unless ahyp:hlinkClr keeps the
        // text color (catalog: interactivity.hyperlinks; measured).
        var hlink = rPr?.Element(Ns.A + "hlinkClick");
        var link = hlink != null && linkPart != null ? Links.Resolve(hlink, linkPart, slide.Presentation) : null;
        var color = RunColor(rPr, sources, level);
        var underline = R("u") is { } u && u != "none" ? u : null;
        if (runOverride?.Underline is { } underlined)
            underline = underlined ? underline ?? "sng" : null;
        // The underline's own line and fill (catalog: text.underline): uLn's width, dash and fill, uFill's fill where
        // uLn has none; uLnTx and uFillTx follow the text (measured with generated/text-underline).
        var underlineLine = RunElement(rPr, sources, level, "uLn", "uLnTx");
        var underlineFill = RunElement(rPr, sources, level, "uFill", "uFillTx");
        var underlineColor = underlineLine?.Name.LocalName == "uLn" && FillReader.FindFill(underlineLine) is { } lineFill
            ? Colors.ResolveChild(lineFill)
            : underlineFill?.Name.LocalName == "uFill" && FillReader.FindFill(underlineFill) is { } ownFill ? Colors.ResolveChild(ownFill) : null;
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
            line,
            // The run's own direction (a:rtl), else its language's.
            RunElement(rPr, sources, level, "rtl") is { } rtl ? rtl.Bool("val") ?? true : RightToLeftLanguage(R("lang")),
            R("lang"))
        {
            UnderlineColor = underlineColor,
            UnderlineLineColor = underlineLine?.Name.LocalName == "uLn" && FillReader.FindFill(underlineLine) is { } outlineFill ? Colors.ResolveChild(outlineFill) : null,
            UnderlineFillColor = underlineFill?.Name.LocalName == "uFill" && FillReader.FindFill(underlineFill) is { } insideFill ? Colors.ResolveChild(insideFill) : null,
            UnderlineWidth = underlineLine?.Name.LocalName == "uLn" && underlineLine.Long("w") is { } uw ? uw / Units.EmuPerPoint : null,
            UnderlineDash = underlineLine?.Name.LocalName == "uLn" ? underlineLine.Element(Ns.A + "prstDash")?.Str("val") : null,
            NormalizeHeight = R("normalizeH") is "1" or "true",
            HoverLink = rPr?.Element(Ns.A + "hlinkMouseOver") is { } mouseOver && linkPart != null ? Links.Resolve(mouseOver, linkPart, slide.Presentation) : null,
            UprightDigits = R("kumimoji") is "1" or "true",
        };
    }

    /// <summary>Whether a language is written right to left; null without a language.</summary>
    static bool? RightToLeftLanguage(string? language)
    {
        if (string.IsNullOrEmpty(language))
            return null;
        var primary = language.Split('-')[0].ToLowerInvariant();
        return primary is "ar" or "he" or "iw" or "fa" or "ur" or "yi" or "ji" or "ps" or "sd" or "ug" or "dv" or "syr" or "ckb" or "ks" or "pa-arab"
               || language.Contains("-Arab", StringComparison.OrdinalIgnoreCase) || language.Contains("-Hebr", StringComparison.OrdinalIgnoreCase);
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
        font?.Str("typeface") is { Length: > 0 } t ? Colors.Theme.ResolveTypeface(t) is { Length: > 0 } r ? r : null : null;

}
