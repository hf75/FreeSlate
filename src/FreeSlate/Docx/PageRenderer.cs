using System.Globalization;
using System.Net;
using System.Text;
using FreeSlate.Docx.Layout;

namespace FreeSlate.Docx;

/// <summary>
/// Draws laid-out pages as SVG (catalog: engine.output): glyphs as outlines in font units placed with &lt;use&gt;,
/// a transparent text layer on top for selection and search, decorations as rectangles.
/// </summary>
public sealed class PageRenderer(DocumentContext context, Drawings? drawings = null, TableLayout? blocks = null)
{
    const double PxPerPt = 96.0 / 72.0;

    /// <summary>
    /// Text below this size in pixels goes to the browser as text, larger text is drawn as glyph outlines. Word's
    /// reference is its PDF with the fonts in it, which Windows' renderer draws grid-fitted like the browser draws
    /// text: printed through the same renderer, our text then matched Word's pixel for pixel where its glyphs stand at
    /// Word's positions (docx/generated/text-basic), outlines did not. FS_DOCX_HINTED sets another size for measuring.
    /// </summary>
    static readonly double HintedBelow = double.TryParse(Environment.GetEnvironmentVariable("FS_DOCX_HINTED"), System.Globalization.CultureInfo.InvariantCulture, out var h) ? h : double.MaxValue;

    static string Px(double points) => Units.Css(points * PxPerPt);

    /// <param name="paper">The named @page of the page's size, so that printing keeps every page at its size.</param>
    public string Render(PageBox page, string paper)
    {
        var width = page.Width * PxPerPt;
        var height = page.Height * PxPerPt;
        var svg = new StringBuilder();
        svg.Append($"<section class=\"page {paper}\" id=\"page-{page.Number}\" style=\"width:{Units.Css(width)}px;height:{Units.Css(height)}px\">");
        // Drawings behind the text first, the text, then inline drawings and the drawings in front of the text.
        var front = new List<PlacedDrawing>();
        if (drawings != null)
        {
            foreach (var drawing in page.Drawings.Where(d => d.Behind).OrderBy(d => d.Order))
                svg.Append(Drawing(drawing, page));
        }
        svg.Append($"<svg width=\"{Units.Css(width)}\" height=\"{Units.Css(height)}\" viewBox=\"0 0 {Units.Css(width)} {Units.Css(height)}\">");
        var selection = new StringBuilder();
        // Lines between columns, 0.75 pt wide (measured: 0.72 to 0.84 pt as Word's 600 dpi device rounds them).
        foreach (var (x, top, bottom) in page.ColumnLines)
            svg.Append($"<rect x=\"{Px(x - 0.375)}\" y=\"{Px(top)}\" width=\"{Px(0.75)}\" height=\"{Px(bottom - top)}\" fill=\"#000\"/>");
        // Table shading, then table borders, below the text.
        Fills(page.Shading.Concat(page.Borders), page.Strokes, svg);
        Lines(page.Lines, svg, selection, front);
        svg.Append(selection);
        svg.Append("</svg>");
        if (drawings != null)
        {
            foreach (var drawing in front.Concat(page.Drawings.Where(d => !d.Behind).OrderBy(d => d.Order)))
                svg.Append(Drawing(drawing, page));
        }
        svg.Append("</section>\n");
        return svg.ToString();
    }

    void Fills(IEnumerable<FillRect> fills, IEnumerable<Stroke> strokes, StringBuilder svg)
    {
        foreach (var fill in fills)
            svg.Append($"<rect x=\"{Px(fill.X)}\" y=\"{Px(fill.Y)}\" width=\"{Px(fill.Width)}\" height=\"{Px(fill.Height)}\" fill=\"{fill.Color.ToHex()}\"/>");
        foreach (var s in strokes)
        {
            var dash = s.Dash is { } pattern ? $" stroke-dasharray=\"{string.Join(' ', pattern.Split(' ').Select(v => Units.Css(double.Parse(v, CultureInfo.InvariantCulture) * PxPerPt)))}\"" : "";
            svg.Append($"<line x1=\"{Px(s.X1)}\" y1=\"{Px(s.Y1)}\" x2=\"{Px(s.X2)}\" y2=\"{Px(s.Y2)}\" stroke=\"{s.Color.ToHex()}\" stroke-width=\"{Px(s.Width)}\"{dash}/>");
        }
    }

    /// <summary>
    /// A drawing and, over it, its text box's content (catalog: drawing.textBoxes): laid out in the shape's rectangle
    /// less the insets of wps:bodyPr (0.1 in left and right, 0.05 in top and bottom by default), at the top, the
    /// middle or the bottom.
    /// </summary>
    string Drawing(PlacedDrawing drawing, PageBox page)
    {
        var html = drawings!.Render(drawing);
        if (drawing.Text is not { Count: > 0 } text || blocks == null)
            return html;
        var body = drawing.Body;
        double Inset(string name, double fallback) => double.TryParse((string?)body?.Attribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v / 12700 : fallback;
        var (l, t, r, b) = (Inset("lIns", 7.2), Inset("tIns", 3.6), Inset("rIns", 7.2), Inset("bIns", 3.6));
        var fields = new PageFields(page.DisplayNumber, page.Section.PageNumberFormat, 0, 0);
        var (lines, fills, strokes, height) = blocks.Blocks(text, Math.Max(1, drawing.Width - l - r), fields);
        var free = drawing.Height - t - b - height;
        var top = drawing.Y + t + ((string?)body?.Attribute("anchor") switch { "ctr" => free / 2, "b" => free, _ => 0 });
        var left = drawing.X + l;
        var width = page.Width * PxPerPt;
        var heightPx = page.Height * PxPerPt;
        var svg = new StringBuilder($"<svg width=\"{Units.Css(width)}\" height=\"{Units.Css(heightPx)}\" viewBox=\"0 0 {Units.Css(width)} {Units.Css(heightPx)}\">");
        Fills(fills.Select(f => f with { X = left + f.X, Y = top + f.Y }), strokes.Select(s => s.Offset(left, top)), svg);
        var selection = new StringBuilder();
        Lines(lines.Select(p => p with { Left = left + p.Left, Top = top + p.Top }), svg, selection, []);
        svg.Append(selection).Append("</svg>");
        return html + svg;
    }

    void Lines(IEnumerable<PlacedLine> lines, StringBuilder svg, StringBuilder selection, List<PlacedDrawing> front)
    {
        foreach (var placed in lines)
        {
            // Bar tabs: lines 0.75 pt wide from the line's top through its height (one device pixel in Word's raster).
            foreach (var bar in placed.Line.Bars ?? [])
                svg.Append($"<rect x=\"{Px(placed.Left + bar - 0.375)}\" y=\"{Px(placed.Top)}\" width=\"{Px(0.75)}\" height=\"{Px(placed.Line.Height + placed.Line.BarExtension)}\" fill=\"#000\"/>");
            var baseline = placed.Top + placed.Line.Baseline;
            if (placed.Angle != 0)
                svg.Append($"<g transform=\"rotate({Units.Css(placed.Angle)} {Px(placed.Left + placed.PivotX)} {Px(placed.Top + placed.PivotY)})\">");
            foreach (var piece in placed.Line.Pieces)
            {
                var x = placed.Left + piece.X;
                // Highlight and shading lie behind the text over the line's natural height.
                if (!piece.IsTab && (piece.Style.Highlight ?? piece.Style.Shading) is { } back)
                    svg.Append($"<rect x=\"{Px(x)}\" y=\"{Px(baseline - placed.Line.Above)}\" width=\"{Px(piece.Width)}\" height=\"{Px(placed.Line.Above + placed.Line.Below)}\" fill=\"{back.ToHex()}\"/>");
                if (piece.IsTab)
                {
                    if (piece.Leader != null)
                        svg.Append(LeaderText(piece, x, baseline - piece.Style.Raise));
                    continue;
                }
                // An inline drawing's box stands on the baseline; its extent lies inside the effect extents.
                if (piece.Drawing is { } inline)
                {
                    var (dx, dy) = Drawings.EffectOffset(inline.Drawing);
                    var (w, h) = Drawings.Extent(inline.Drawing);
                    front.Add(new PlacedDrawing(inline.Drawing, inline.Part, x + dx, baseline - inline.Box.Height + dy, w, h) { Text = inline.Text });
                    continue;
                }
                var browser = piece.Style.Size * PxPerPt < HintedBelow || piece.Style.Font.Data.IsColor;
                if (browser)
                    svg.Append(BrowserText(piece, x, baseline - piece.Style.Raise));
                else
                {
                    svg.Append(Glyphs(piece, x, baseline - piece.Style.Raise));
                    selection.Append(SelectionText(piece, x, baseline - piece.Style.Raise));
                }
                svg.Append(Decorations(piece, x, baseline - piece.Style.Raise));
            }
            if (placed.Angle != 0)
                svg.Append("</g>");
        }
    }

    /// <summary>The x position (points from the piece's start) of every UTF-16 unit, justification included.</summary>
    static double[] Positions(Piece piece)
    {
        var positions = new double[piece.Text.Length + 1];
        var x = 0.0;
        var spaces = 0;
        for (var i = 0; i < piece.Text.Length; i++)
        {
            positions[i] = x;
            x += piece.Advances[i];
            if (piece.Text[i] == ' ' && spaces++ < piece.JustifiedSpaces)
                x += piece.SpaceExtra;
        }
        positions[^1] = x;
        return positions;
    }

    string Glyphs(Piece piece, double x, double baseline)
    {
        var style = piece.Style;
        var data = style.Font.Data;
        if (data.IsColor)
            return "";
        var glyphs = data.GlyphsWithFeatures(piece.Text, style.Features, language: style.Language);
        var positions = Positions(piece);
        var scale = style.Size * PxPerPt / data.UnitsPerEm;
        var units = new StringBuilder();
        // Glyphs of one cluster follow each other from the cluster's position.
        var clusterStart = -1;
        var inCluster = 0.0;
        foreach (var g in glyphs)
        {
            if (g.Cluster != clusterStart)
            {
                clusterStart = g.Cluster;
                inCluster = 0;
            }
            var gx = (positions[Math.Min(g.Cluster, piece.Text.Length)] + inCluster) * PxPerPt / scale + g.OffsetX * data.UnitsPerEm * style.Scale;
            inCluster += g.Advance * style.Size * style.Scale;
            if (context.Glyphs.Id(data, g.Id) is not { } id)
                continue;
            var gy = -g.OffsetY * data.UnitsPerEm;
            units.Append($"<use href=\"#{id}\"");
            // Horizontally scaled text (w:w) narrows or widens each glyph about its origin.
            if (Math.Abs(style.Scale - 1) > 1e-6)
                units.Append($" transform=\"translate({Units.Css1(gx)} {Units.Css1(gy)}) scale({Units.Css4(style.Scale)} 1)\"");
            else
            {
                if (Math.Abs(gx) > 0.05) units.Append($" x=\"{Units.Css1(gx)}\"");
                if (Math.Abs(gy) > 0.05) units.Append($" y=\"{Units.Css1(gy)}\"");
            }
            units.Append("/>");
        }
        if (units.Length == 0)
            return "";
        var transform = $"translate({Units.Css4(x * PxPerPt)} {Units.Css4(baseline * PxPerPt)}) scale({Units.Scale(scale)})";
        if (style.Font.SyntheticItalic)
            transform += " skewX(-12)";
        var stroke = style.Font.SyntheticBold
            ? $" stroke=\"{style.Color.ToHex()}\" stroke-width=\"{Units.Css1(data.UnitsPerEm * 0.025)}\" stroke-linejoin=\"round\""
            : "";
        return $"<g transform=\"{transform}\" fill=\"{style.Color.ToHex()}\"{stroke}>{units}</g>";
    }

    /// <summary>Underline and strikethrough from the font's own positions and thicknesses (catalog: run.decoration).</summary>
    static string Decorations(Piece piece, double x, double baseline)
    {
        var style = piece.Style;
        if (style.Underline == null && !style.Strike && !style.DoubleStrike)
            return "";
        var data = style.Font.Data;
        var width = piece.Width;
        // Trailing spaces of the line are not underlined.
        var trimmed = piece.Text.TrimEnd(' ');
        if (trimmed.Length < piece.Text.Length)
            width = Positions(piece)[trimmed.Length];
        if (width <= 0)
            return "";
        var sb = new StringBuilder();
        var color = (style.UnderlineColor ?? style.Color).ToHex();
        if (style.Underline != null)
        {
            var thickness = Math.Max(0.5, data.UnderlineThickness * style.Size) * (style.Underline.Contains("thick", StringComparison.OrdinalIgnoreCase) || style.Underline.EndsWith("Heavy", StringComparison.Ordinal) ? 2 : 1);
            // The font's underline position is the top of the line (measured: Aptos 12 pt, post -100/2048 em, Word's
            // rectangle starts 0.6 pt below the baseline on its 600 dpi device).
            var y = baseline + data.UnderlinePosition * style.Size;
            sb.Append($"<rect x=\"{Px(x)}\" y=\"{Px(y)}\" width=\"{Px(width)}\" height=\"{Px(thickness)}\" fill=\"{color}\"/>");
            if (style.Underline == "double")
                sb.Append($"<rect x=\"{Px(x)}\" y=\"{Px(y + thickness * 1.5)}\" width=\"{Px(width)}\" height=\"{Px(thickness)}\" fill=\"{color}\"/>");
        }
        if (style.Strike || style.DoubleStrike)
        {
            var thickness = Math.Max(0.5, data.StrikeoutThickness * style.Size);
            var y = baseline - data.StrikeoutPosition * style.Size;
            var textColor = style.Color.ToHex();
            if (style.DoubleStrike)
            {
                sb.Append($"<rect x=\"{Px(x)}\" y=\"{Px(y - thickness * 1.5)}\" width=\"{Px(width)}\" height=\"{Px(thickness)}\" fill=\"{textColor}\"/>");
                sb.Append($"<rect x=\"{Px(x)}\" y=\"{Px(y + thickness * 0.5)}\" width=\"{Px(width)}\" height=\"{Px(thickness)}\" fill=\"{textColor}\"/>");
            }
            else
                sb.Append($"<rect x=\"{Px(x)}\" y=\"{Px(y - thickness / 2)}\" width=\"{Px(width)}\" height=\"{Px(thickness)}\" fill=\"{textColor}\"/>");
        }
        return sb.ToString();
    }

    /// <summary>Text the browser draws (and selects), every character at its position, in the run's font and color.</summary>
    string BrowserText(Piece piece, double x, double baseline)
    {
        if (string.IsNullOrWhiteSpace(piece.Text))
            return "";
        var style = piece.Style;
        var positions = Positions(piece);
        var xs = string.Join(' ', Enumerable.Range(0, piece.Text.Length).Where(i => !char.IsLowSurrogate(piece.Text[i]))
            .Select(i => Units.Css((x + positions[i]) * PxPerPt)));
        var family = style.Font.Data.Family;
        context.UsedFonts.Add(family);
        var sb = new StringBuilder($"<text x=\"{xs}\" y=\"{Px(baseline)}\" font-family=\"'{WebUtility.HtmlEncode(family.Replace("'", "\\'"))}'\" font-size=\"{Px(style.Size)}\" fill=\"{style.Color.ToHex()}\"");
        if (style.Bold) sb.Append(" font-weight=\"700\"");
        if (style.Italic) sb.Append(" font-style=\"italic\"");
        // Our positions already hold the kerning; the browser must not add its own.
        sb.Append(" style=\"font-kerning:none\"");
        if (Math.Abs(style.Scale - 1) > 1e-6)
            sb.Append($" transform=\"translate({Units.Css(x * PxPerPt)} 0) scale({Units.Css4(style.Scale)} 1) translate({Units.Css(-x * PxPerPt)} 0)\"");
        sb.Append('>').Append(WebUtility.HtmlEncode(piece.Text)).Append("</text>");
        return sb.ToString();
    }

    /// <summary>A tab's leader: its character repeated where <see cref="TabLeader"/> puts it, in the tab's font and color.</summary>
    string LeaderText(Piece tab, double x, double baseline)
    {
        var positions = TabLeader.Positions(tab, x);
        if (positions.Count == 0 || TabLeader.Character(tab.Leader) is not { } c)
            return "";
        var style = tab.Style;
        var family = style.Font.Data.Family;
        context.UsedFonts.Add(family);
        var sb = new StringBuilder($"<text x=\"{string.Join(' ', positions.Select(p => Units.Css(p * PxPerPt)))}\" y=\"{Px(baseline)}\" font-family=\"'{WebUtility.HtmlEncode(family.Replace("'", "\\'"))}'\" font-size=\"{Px(style.Size)}\" fill=\"{style.Color.ToHex()}\"");
        if (style.Bold || tab.Leader == "heavy") sb.Append(" font-weight=\"700\"");
        if (style.Italic) sb.Append(" font-style=\"italic\"");
        sb.Append(" style=\"font-kerning:none\">").Append(WebUtility.HtmlEncode(new string(c, positions.Count))).Append("</text>");
        return sb.ToString();
    }

    /// <summary>The piece's text, invisible, character by character where the glyphs are, for selection and search.</summary>
    string SelectionText(Piece piece, double x, double baseline)
    {
        if (string.IsNullOrWhiteSpace(piece.Text))
            return "";
        var positions = Positions(piece);
        var xs = string.Join(' ', Enumerable.Range(0, piece.Text.Length).Where(i => !char.IsLowSurrogate(piece.Text[i]))
            .Select(i => Units.Css((x + positions[i]) * PxPerPt)));
        var family = piece.Style.Font.Data.Family;
        context.UsedFonts.Add(family);
        return $"<text class=\"sel\" x=\"{xs}\" y=\"{Px(baseline)}\" font-family=\"'{WebUtility.HtmlEncode(family.Replace("'", "\\'"))}'\" font-size=\"{Px(piece.Style.Size)}\">{WebUtility.HtmlEncode(piece.Text)}</text>";
    }
}
