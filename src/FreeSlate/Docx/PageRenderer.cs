using System.Globalization;
using System.Net;
using System.Text;
using System.Xml.Linq;
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

    /// <summary>Gradients and filters of the page's text effects, drawn at its end.</summary>
    readonly StringBuilder defs = new();

    /// <param name="paper">The named @page of the page's size, so that printing keeps every page at its size.</param>
    public string Render(PageBox page, string paper)
    {
        var width = page.Width * PxPerPt;
        var height = page.Height * PxPerPt;
        var svg = new StringBuilder();
        defs.Clear();
        svg.Append($"<section class=\"page {paper}\" id=\"page-{page.Number}\" style=\"width:{Units.Css(width)}px;height:{Units.Css(height)}px\">");
        // The page background, drawings behind the text, the text, then inline drawings and the drawings in front.
        var front = new List<PlacedDrawing>();
        if (drawings != null)
        {
            svg.Append(drawings.Background(page.Width, page.Height));
            foreach (var drawing in page.Drawings.Where(d => d.Behind).OrderBy(d => d.Order))
                svg.Append(Drawing(drawing, page));
        }
        svg.Append($"<svg width=\"{Units.Css(width)}\" height=\"{Units.Css(height)}\" viewBox=\"0 0 {Units.Css(width)} {Units.Css(height)}\">");
        var selection = new StringBuilder();
        // Lines between columns, 0.75 pt wide (measured: 0.72 to 0.84 pt as Word's 600 dpi device rounds them).
        foreach (var (x, top, bottom) in page.ColumnLines)
            svg.Append($"<rect x=\"{Px(x - 0.375)}\" y=\"{Px(top)}\" width=\"{Px(0.75)}\" height=\"{Px(bottom - top)}\" fill=\"#000\"/>");
        // A page border behind the text, table shading and borders below the text, a page border in front of it.
        if (page.Border is { Front: false } back)
            Fills(back.Fills, back.Strokes, svg);
        Fills(page.Shading.Concat(page.Borders), page.Strokes, svg);
        Lines(page.Lines, svg, selection, front);
        // Line numbers, right-aligned at their place (catalog: section.lineNumbers).
        foreach (var (number, style, right, baseline) in page.LineNumbers)
        {
            context.UsedFonts.Add(style.Font.Data.Family);
            svg.Append($"<text x=\"{Px(right)}\" y=\"{Px(baseline)}\" text-anchor=\"end\" font-family=\"'{WebUtility.HtmlEncode(style.Font.Data.Family.Replace("'", "\\'"))}'\" font-size=\"{Px(style.Size)}\" fill=\"{style.Color.ToHex()}\"{(style.Bold ? " font-weight=\"700\"" : "")}{(style.Italic ? " font-style=\"italic\"" : "")}>{number}</text>");
        }
        if (page.Border is { Front: true } border)
            Fills(border.Fills, border.Strokes, svg);
        svg.Append(selection);
        svg.Append("</svg>");
        if (drawings != null)
        {
            foreach (var drawing in front.Concat(page.Drawings.Where(d => !d.Behind).OrderBy(d => d.Order)))
                svg.Append(Drawing(drawing, page));
        }
        foreach (var (text, x, y, w, h) in page.Alternatives)
            svg.Append($"<div class=\"alt\" role=\"img\" aria-label=\"{WebUtility.HtmlEncode(text)}\" style=\"left:{Px(x)}px;top:{Px(y)}px;width:{Px(w)}px;height:{Px(h)}px\"></div>");
        foreach (var (name, x, y) in page.Bookmarks)
            svg.Append($"<a class=\"bm\" id=\"{WebUtility.HtmlEncode(name)}\" style=\"left:{Px(x)}px;top:{Px(y)}px\"></a>");
        if (defs.Length > 0)
            svg.Append("<svg width=\"0\" height=\"0\" style=\"position:absolute\"><defs>").Append(defs).Append("</defs></svg>");
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
        if (blocks == null)
            return html;
        if (drawing.Text is { Count: > 0 } text)
            html += TextBox(text, drawing.Body, drawing.Drawing.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData")?.Element(Drawings.Wps + "wsp"),
                drawing.X, drawing.Y, drawing.Width, drawing.Height, 0, page);
        // The text boxes of the shapes in a group or canvas, in their shapes' places (and turns).
        foreach (var (shape, childText) in drawing.ChildText ?? [])
        {
            if (Drawings.ChildRect(drawing.Drawing, shape, drawing.X, drawing.Y, drawing.Width, drawing.Height) is { } rect && childText.Count > 0)
                html += TextBox(childText, shape.Element(Drawings.Wps + "bodyPr"), shape, rect.X, rect.Y, rect.W, rect.H, rect.Rotation, page);
        }
        return html;
    }

    /// <summary>
    /// A shape's text box content laid out like body text in the shape's rectangle less the insets of wps:bodyPr (from
    /// the geometry's text rectangle), at the top, middle or bottom, turned with the shape.
    /// </summary>
    string TextBox(List<Block> text, XElement? body, XElement? shape, double x, double y, double w, double h, double rotation, PageBox page)
    {
        double Inset(string name, double fallback) => double.TryParse((string?)body?.Attribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v / 12700 : fallback;
        var (l, t, r, b) = (Inset("lIns", 7.2), Inset("tIns", 3.6), Inset("rIns", 7.2), Inset("bIns", 3.6));
        // The insets count from the geometry's text rectangle (rounded corners, ellipses).
        var (gl, gt, gr, gb) = shape != null ? Drawings.ShapeTextRect(shape, w, h) : default;
        (l, t, r, b) = (l + gl, t + gt, r + gr, b + gb);
        var fields = new PageFields(page.DisplayNumber, page.Section.PageNumberFormat, 0, 0);
        var (lines, fills, strokes, height) = blocks!.Blocks(text, Math.Max(1, w - l - r), fields);
        var free = h - t - b - height;
        var top = y + t + ((string?)body?.Attribute("anchor") switch { "ctr" => free / 2, "b" => free, _ => 0 });
        var left = x + l;
        var width = page.Width * PxPerPt;
        var heightPx = page.Height * PxPerPt;
        var svg = new StringBuilder($"<svg width=\"{Units.Css(width)}\" height=\"{Units.Css(heightPx)}\" viewBox=\"0 0 {Units.Css(width)} {Units.Css(heightPx)}\">");
        if (rotation != 0)
            svg.Append($"<g transform=\"rotate({Units.Css(rotation)} {Px(x + w / 2)} {Px(y + h / 2)})\">");
        Fills(fills.Select(f => f with { X = left + f.X, Y = top + f.Y }), strokes.Select(s => s.Offset(left, top)), svg);
        var selection = new StringBuilder();
        Lines(lines.Select(p => p with { Left = left + p.Left, Top = top + p.Top }), svg, selection, []);
        svg.Append(selection);
        if (rotation != 0)
            svg.Append("</g>");
        return svg.Append("</svg>").ToString();
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
            // Editable ranges (w:permStart, w:permEnd): gray brackets 0.24 pt thin through the line (measured with
            // docx/local/misc-nothing).
            foreach (var (bracketX, start) in placed.Line.Brackets ?? [])
            {
                var (bx, top, bottom) = (placed.Left + bracketX, placed.Top, placed.Top + placed.Line.Height);
                var tick = start ? bx - 0.24 : bx - 0.36;
                svg.Append($"<rect x=\"{Px(start ? bx - 0.24 : bx)}\" y=\"{Px(top + 0.38)}\" width=\"{Px(0.24)}\" height=\"{Px(bottom - top - 0.6)}\" fill=\"#7f7f7f\"/>");
                svg.Append($"<rect x=\"{Px(tick)}\" y=\"{Px(top + 0.14)}\" width=\"{Px(0.6)}\" height=\"{Px(0.24)}\" fill=\"#7f7f7f\"/>");
                svg.Append($"<rect x=\"{Px(tick)}\" y=\"{Px(bottom - 0.22)}\" width=\"{Px(0.6)}\" height=\"{Px(0.24)}\" fill=\"#7f7f7f\"/>");
            }
            foreach (var piece in placed.Line.Pieces)
            {
                var x = placed.Left + piece.X;
                // Highlight and shading lie behind the text over the line's natural height.
                if (!piece.IsTab && (piece.Style.Highlight ?? piece.Style.Shading) is { } back)
                    svg.Append($"<rect x=\"{Px(x)}\" y=\"{Px(baseline - placed.Line.Above)}\" width=\"{Px(piece.Width)}\" height=\"{Px(placed.Line.Above + placed.Line.Below)}\" fill=\"{back.ToHex()}\"/>");
                if (piece.IsTab)
                {
                    if (piece.Leader != null)
                        svg.Append(LeaderText(piece, x, baseline - piece.Style.Raise - piece.Shift));
                    continue;
                }
                // A note separator: a line 0.6 pt thick through the middle of its line, 144 pt long (measured with
                // docx/generated/notes).
                if (piece.Separator)
                {
                    svg.Append($"<rect x=\"{Px(x)}\" y=\"{Px(placed.Top + placed.Line.Height / 2 - 0.3)}\" width=\"{Px(piece.Width)}\" height=\"{Px(0.6)}\" fill=\"#000\"/>");
                    continue;
                }
                // A check box form field: a square 2.28 pt smaller than its cell, 1.08 pt in from its left and bottom,
                // crossed when checked, in 0.48 pt lines (measured with docx/local/form-boxes).
                if (piece.CheckBox is { } box)
                {
                    var (_, boxBelow) = WordFonts.LineMetrics(piece.Style.Font.Data, box.Size ?? piece.Style.Size);
                    var side = piece.Width - 2.28;
                    var bottom = baseline + boxBelow - 1.08;
                    var (bx, top) = (x + 1.08, bottom - side);
                    svg.Append($"<rect x=\"{Px(bx)}\" y=\"{Px(top)}\" width=\"{Px(side)}\" height=\"{Px(side)}\" fill=\"none\" stroke=\"#000\" stroke-width=\"{Px(0.48)}\"/>");
                    if (box.Checked)
                        svg.Append($"<path d=\"M{Px(bx)} {Px(top)}L{Px(bx + side)} {Px(bottom)}M{Px(bx + side)} {Px(top)}L{Px(bx)} {Px(bottom)}\" stroke=\"#000\" stroke-width=\"{Px(0.48)}\"/>");
                    continue;
                }
                // An equation: its glyphs and rules, its characters transparent on top for selection.
                if (piece.Equation is { } equation)
                {
                    var (drawn, mathText) = equation.Draw(x, baseline - piece.Style.Raise - piece.Shift, context.Glyphs, PxPerPt);
                    svg.Append(drawn);
                    if (mathText.Length > 0)
                        selection.Append($"<text class=\"sel\" x=\"{Px(x)}\" y=\"{Px(baseline - piece.Style.Raise - piece.Shift)}\" font-size=\"{Px(piece.Style.Size)}\" textLength=\"{Px(equation.Width)}\" lengthAdjust=\"spacingAndGlyphs\">{WebUtility.HtmlEncode(mathText)}</text>");
                    continue;
                }
                // An inline drawing's box stands on the baseline; its extent lies inside the effect extents.
                if (piece.Drawing is { } inline)
                {
                    var (dx, dy) = Drawings.EffectOffset(inline.Drawing);
                    var (w, h) = Drawings.Extent(inline.Drawing);
                    front.Add(new PlacedDrawing(inline.Drawing, inline.Part, x + dx, baseline - inline.Box.Height + dy, w, h) { Text = inline.Text, ChildText = inline.ChildText });
                    continue;
                }
                var browser = piece.Style.Size * PxPerPt < HintedBelow || piece.Style.Font.Data.IsColor;
                // Hyperlinks wrap their text (catalog: field.hyperlinks); bookmarks are anchors in the page.
                if (piece.Link != null)
                    svg.Append($"<a href=\"{WebUtility.HtmlEncode(piece.Link)}\">");
                if (browser)
                    svg.Append(WithEffects(piece, BrowserText(piece, x, baseline - piece.Style.Raise - piece.Shift), x, baseline - piece.Style.Raise - piece.Shift));
                else
                {
                    svg.Append(Glyphs(piece, x, baseline - piece.Style.Raise - piece.Shift));
                    selection.Append(SelectionText(piece, x, baseline - piece.Style.Raise - piece.Shift));
                }
                svg.Append(Decorations(piece, x, baseline - piece.Style.Raise - piece.Shift));
                if (piece.Link != null)
                    svg.Append("</a>");
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
        var text = piece.Text;
        var positions = Positions(piece);
        var on = style.Features.Where(f => !f.StartsWith('-') && f != "kern").ToList();
        string X(int from, int to) => string.Join(' ', Enumerable.Range(from, to - from).Where(i => !char.IsLowSurrogate(text[i]))
            .Select(i => Units.Css((x + positions[i]) * PxPerPt)));
        // Words whose glyphs depend on their neighbors (ligatures, contextual alternates and swashes; catalog:
        // run.openType) must reach the browser whole: it shapes every character that has a position of its own alone
        // (measured: Chrome draws "office" without its ffi ligature when each character has its x). Such a word gets
        // the position of its start and, for every cluster, how far it stands from where the browser's own advances
        // put it; the browser's advances differ from these by up to 0.01 pt a glyph, so the other words keep the
        // position of every character.
        // Right-to-left text goes to the browser whole, from its right end, so that it is shaped and reads in order
        // (as in PowerPoint); justification widens its spaces.
        var words = piece.RightToLeft ? [] : Math.Abs(style.Scale - 1) < 1e-6 ? ContextualWords(style, text) : [];
        var family = style.Font.Data.Family;
        context.UsedFonts.Add(family);
        var sb = new StringBuilder("<text");
        if (piece.RightToLeft)
        {
            sb.Append($" x=\"{Px(x + piece.Width)}\" direction=\"rtl\" unicode-bidi=\"bidi-override\"");
            if (piece.SpaceExtra > 0)
                sb.Append($" word-spacing=\"{Px(piece.SpaceExtra)}\"");
        }
        else if (words.Count == 0)
            sb.Append($" x=\"{X(0, text.Length)}\"");
        sb.Append($" y=\"{Px(baseline)}\" font-family=\"'{WebUtility.HtmlEncode(family.Replace("'", "\\'"))}'\" font-size=\"{Px(style.Size)}\"{TextPaint(piece, x, baseline)}");
        if (style.Bold) sb.Append(" font-weight=\"700\"");
        if (style.Italic) sb.Append(" font-style=\"italic\"");
        // Our positions already hold the kerning; the browser must not add its own. The features the run turns on are
        // the browser's to apply too.
        sb.Append(" style=\"font-kerning:none");
        if (on.Count > 0)
            sb.Append(";font-feature-settings:").Append(string.Join(',', on.Select(f => $"'{f}' 1")));
        sb.Append('"');
        if (Math.Abs(style.Scale - 1) > 1e-6)
            sb.Append($" transform=\"translate({Units.Css(x * PxPerPt)} 0) scale({Units.Css4(style.Scale)} 1) translate({Units.Css(-x * PxPerPt)} 0)\"");
        sb.Append('>');
        if (words.Count == 0)
            sb.Append(WebUtility.HtmlEncode(text));
        else
        {
            var at = 0;
            foreach (var (from, to) in words)
            {
                if (from > at)
                    sb.Append($"<tspan x=\"{X(at, from)}\">{WebUtility.HtmlEncode(text[at..from])}</tspan>");
                sb.Append($"<tspan x=\"{Units.Css((x + positions[from]) * PxPerPt)}\" dx=\"{Shifts(style, text[from..to], positions.AsSpan(from, to - from + 1))}\">{WebUtility.HtmlEncode(text[from..to])}</tspan>");
                at = to;
            }
            if (at < text.Length)
                sb.Append($"<tspan x=\"{X(at, text.Length)}\">{WebUtility.HtmlEncode(text[at..])}</tspan>");
        }
        sb.Append("</text>");
        return sb.ToString();
    }

    /// <summary>
    /// The fill and outline of a piece's text (catalog: run.textEffects): w14:textFill (solid, gradient over the piece
    /// from its ascent to its descent, or none) and w14:textOutline, else the run's color.
    /// </summary>
    string TextPaint(Piece piece, double x, double baseline)
    {
        var style = piece.Style;
        if (style.TextFill == null && style.Outline == null || drawings == null)
            return $" fill=\"{style.Color.ToHex()}\"";
        var (above, below) = style.Metrics;
        var (w, h) = (piece.Width * PxPerPt, (above + below) * PxPerPt);
        var origin = (X: x * PxPerPt, Y: (baseline - above) * PxPerPt);
        var fill = style.TextFill is { } own ? FreeSlate.Drawing.FillReader.Read(own, drawings.Colors) : new FreeSlate.Drawing.SolidFill(style.Color);
        var paint = drawings.Paint.Fill(fill, w, h, defs, origin: origin, offset: origin);
        if (style.Outline is { } line && FreeSlate.Drawing.OutlineReader.Merge([(line, drawings.Colors)]) is { Visible: true } outline)
            paint += drawings.Paint.Stroke(outline, w, h, defs, origin);
        return paint;
    }

    /// <summary>
    /// A piece's text with its glow and shadow (the filter shared with PowerPoint, sized with the font) and its
    /// reflection: a mirrored copy below the baseline (catalog: run.textEffects).
    /// </summary>
    string WithEffects(Piece piece, string markup, double x, double baseline)
    {
        var style = piece.Style;
        if (style.Effects is not { } effects || drawings == null || markup.Length == 0)
            return markup;
        var scale = Html.SvgEffects.TextScale.Of(style.Size);
        if (Html.SvgEffects.Filter(effects, drawings.Colors, 0, 0, defs, boundingBox: true, text: scale) is { } filter)
            markup = $"<g filter=\"url(#{filter})\">{markup}</g>";
        // Text reflects about its baseline, the box reaching up to the font's ascent (as PowerPoint draws it).
        if (effects.Element(Ns.A + "reflection") is { } reflection)
        {
            var distance = Units.ToPx(reflection.Long("dist") ?? 0) * scale.ReflectionDistance / 2;
            var bottom = baseline * PxPerPt + distance;
            var height = style.Size * PxPerPt * style.Font.Data.Ascent + distance;
            markup = Html.SvgEffects.Reflection(effects, markup, (x * PxPerPt, bottom - height, piece.Width * PxPerPt, height), defs,
                distanceScale: 0, blurScale: scale.ReflectionBlur);
        }
        return markup;
    }

    /// <summary>
    /// The words of a piece (each with the spaces after it) holding a glyph that depends on its neighbors: a cluster of
    /// several characters, or a glyph other than the character's own with the run's features. Besides ligatures and
    /// alternates the run turns on, these are the clusters every font forms: combining marks, emoji sequences and the
    /// conjuncts and contextual forms of complex scripts (catalog: run.complexScript, engine.fonts).
    /// </summary>
    static List<(int From, int To)> ContextualWords(RunStyle style, string text)
    {
        var result = new List<(int, int)>();
        if (!style.Features.Any(f => f is "liga" or "clig" or "calt" or "hlig" or "dlig" || f.StartsWith("ss", StringComparison.Ordinal))
            && !text.Any(c => c >= 0x300))
            return result;
        var data = style.Font.Data;
        var glyphs = data.GlyphsWithFeatures(text, style.Features, language: style.Language);
        var contextual = new bool[text.Length];
        var byCluster = glyphs.Where(g => g.Cluster < text.Length).GroupBy(g => g.Cluster).OrderBy(g => g.Key).ToList();
        for (var k = 0; k < byCluster.Count; k++)
        {
            var start = byCluster[k].Key;
            var end = k + 1 < byCluster.Count ? byCluster[k + 1].Key : text.Length;
            var own = (end - start == 1 || end - start == 2 && char.IsHighSurrogate(text[start])) && byCluster[k].Count() == 1;
            if (own)
            {
                var alone = data.GlyphsWithFeatures(text[start..end], style.Features, language: style.Language);
                own = alone.Length == 1 && alone[0].Id == byCluster[k].First().Id;
            }
            if (!own)
            {
                for (var i = start; i < end; i++)
                    contextual[i] = true;
            }
        }
        var from = 0;
        for (var i = 1; i <= text.Length; i++)
        {
            if (i < text.Length && !(text[i] != ' ' && text[i - 1] == ' '))
                continue;
            if (contextual.AsSpan(from, i - from).Contains(true))
                result.Add((from, i));
            from = i;
        }
        return result;
    }

    /// <summary>
    /// The dx list of a word the browser shapes whole: per character (code point), how far the cluster it begins
    /// stands from where the browser's own unkerned advances put it, 0 within clusters.
    /// </summary>
    /// <param name="positions">The positions of the word's characters and of its end.</param>
    static string Shifts(RunStyle style, string text, ReadOnlySpan<double> positions)
    {
        var features = style.Features.Select(f => f == "kern" ? "-kern" : f).ToList();
        var natural = new double[text.Length];
        var starts = new SortedSet<int>();
        foreach (var g in style.Font.Data.GlyphsWithFeatures(text, features, language: style.Language))
        {
            if (g.Cluster >= text.Length)
                continue;
            natural[g.Cluster] += g.Advance * style.Size;
            starts.Add(g.Cluster);
        }
        // Shifts add up: each is what the cluster needs less what the ones before gave, rounded as written.
        var shifts = new List<string>();
        var browser = 0.0;   // where the browser's advances put the cluster, from the first
        var given = 0.0;     // the shifts written so far, in pixels
        var previous = -1;
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsLowSurrogate(text[i]))
                continue;
            var shift = 0.0;
            if (starts.Contains(i))
            {
                if (previous >= 0)
                {
                    browser += natural[previous];
                    shift = Math.Round((positions[i] - positions[0] - browser) * PxPerPt - given, 2);
                    given += shift;
                }
                previous = i;
            }
            shifts.Add(shift == 0 ? "0" : Units.Css(shift));
        }
        // Trailing zeros say nothing.
        var count = shifts.Count;
        while (count > 1 && shifts[count - 1] == "0")
            count--;
        return string.Join(' ', shifts.Take(count));
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
