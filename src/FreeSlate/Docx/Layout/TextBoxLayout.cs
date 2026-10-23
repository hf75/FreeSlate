using System.Globalization;
using System.Xml.Linq;
using FreeSlate.Drawing;

namespace FreeSlate.Docx.Layout;

/// <summary>
/// A text box's text as laid out in its box: its lines from the top inset (each from the left inset), the fills and
/// strokes of its paragraphs and tables, how far down the box's anchor moves them, its direction (wps:bodyPr/@vert,
/// null for horizontal text) and the box's insets.
/// </summary>
public sealed record BoxText(List<PlacedLine> Lines, List<FillRect> Fills, List<Stroke> Strokes, double Offset, string? Vertical,
    (double L, double T, double R, double B) Insets);

/// <summary>
/// The text boxes of drawings laid out (catalog: drawing.textBoxes): their shapes sized to their text, their text in
/// them; the paginator fits shapes before text wraps around them, a page's boxes are laid out before the page is drawn
/// (PageRenderer.LayBoxes), in the order its drawing takes them.
/// </summary>
public sealed class TextBoxLayout(DocumentContext context, TableLayout blocks, Drawings? drawings)
{
    /// <summary>
    /// The text of linked text boxes that did not fit the boxes before, by the story's id and the next box's place: the
    /// paragraph it goes on in from the first line the box before had no room for, then the blocks after it.
    /// </summary>
    readonly Dictionary<(string Id, int Seq), List<Block>> continued = [];

    /// <summary>
    /// A text box's shape sized to its text (measured with docx/local/textboxes): a shape that resizes to fit
    /// (a:spAutoFit) as high as its text and insets, a box that does not wrap (wrap none) as wide as its longest
    /// line and insets.
    /// </summary>
    public PlacedDrawing Fitted(PlacedDrawing drawing, List<Block> text, XElement body, XElement? shape, PageBox page, TextSettings settings) =>
        FitToText(drawing, text, body, shape, page, blocks, settings, context.Theme);

    /// <summary>
    /// A group whose text boxes resize to their text (a:spAutoFit) with those shapes as high as their text and insets,
    /// their tops kept (LibreOffice's extras-ooxmlexport/groupshape-theme-font: a box saved 127.3 pt high fits its text,
    /// 66.45 pt); the drawing's copy, the document's model untouched.
    /// </summary>
    public PlacedDrawing FittedChildren(PlacedDrawing drawing, PageBox page, TextSettings settings)
    {
        if (!drawing.ChildText!.Any(c => c.Text.Count > 0 && c.Shape.Element(Drawings.Wps + "bodyPr") is { } b && b.Element(Ns.A + "spAutoFit") != null && !Vertical(b)))
            return drawing;
        var clone = new XElement(drawing.Drawing);
        var originals = drawing.Drawing.Descendants(Drawings.Wps + "wsp").ToList();
        var copies = clone.Descendants(Drawings.Wps + "wsp").ToList();
        var children = new List<(XElement Shape, List<Block> Text)>();
        var fits = new List<(XElement Copy, XElement Original, List<Block> Text, XElement Body)>();
        var resized = false;
        foreach (var (child, text) in drawing.ChildText!)
        {
            var index = originals.IndexOf(child);
            if (index < 0 || index >= copies.Count)
                return drawing;
            var copy = copies[index];
            children.Add((copy, text));
            if (text.Count == 0 || child.Element(Drawings.Wps + "bodyPr") is not { } body || body.Element(Ns.A + "spAutoFit") == null || Vertical(body)
                || Drawings.ChildRect(drawing.Drawing, child, drawing.X, drawing.Y, drawing.Width, drawing.Height) is not { H: > 0 } rect
                || copy.Element(Drawings.Wps + "spPr")?.Element(Ns.A + "xfrm")?.Element(Ns.A + "ext") is not { } ext
                || !double.TryParse((string?)ext.Attribute("cy"), NumberStyles.Float, CultureInfo.InvariantCulture, out var cy) || cy <= 0)
                continue;
            var fitted = FitToText(new PlacedDrawing(child, drawing.Part, rect.X, rect.Y, rect.W, rect.H), text, body, child, page, blocks, settings, context.Theme);
            var height = (long)Math.Round(fitted.Height * cy / rect.H);
            resized |= height != (long)Math.Round(cy);
            fits.Add((copy, child, text, body));
            ext.SetAttributeValue("cy", height);
        }
        var placed = drawing with { Drawing = clone, ChildText = children };
        return resized && drawing.Drawing.Attribute(Vml.Source) != null ? Stretched(placed, fits, page, settings) ?? placed : placed;
    }

    /// <summary>
    /// A VML group sized in percent both ways (mso-width-percent, mso-height-percent) one of whose text boxes changed
    /// its height to fit its text: Word gathers the group about its children and stretches their bounds onto the
    /// percent box, the fitted boxes as high as their text at the new scale; a group placed by an offset keeps the
    /// corner its coordinates gave the bounds, an aligned one aligns the box (measured with docx/local/vgp-pct-fit,
    /// vgq-fitinner, vgq-fit-center, vgq-fit-left0: rectangles 200 units wide 158.65 pt wide where the children span 600
    /// of the group's 1000 units across 476 pt, a box fitted to a 24 pt line 35 pt high ending the bounds at the box's
    /// bottom; vgq-nofit, vgq-fitsame: no stretch where no box fits or the fitted one keeps its height in the group's
    /// units; vgq-fit-plain: none without percent sizes; docx/external/libreoffice/extras-ooxmlimport/n751077: text boxes
    /// 1800 to 11492 units across the page's 612 pt). Only one way in percent (vgq-fitw) is left as it is. Null where
    /// this does not apply.
    /// </summary>
    PlacedDrawing? Stretched(PlacedDrawing drawing, List<(XElement Copy, XElement Original, List<Block> Text, XElement Body)> fits, PageBox page, TextSettings settings)
    {
        if (drawing.Drawing.Element(Drawings.Wp14 + "sizeRelH") == null || drawing.Drawing.Element(Drawings.Wp14 + "sizeRelV") == null
            || drawing.Drawing.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData")?.Element(Drawings.Wpg + "wgp") is not { } group
            || group.Element(Drawings.Wpg + "grpSpPr")?.Element(Ns.A + "xfrm") is not { } xfrm
            || fits.Any(r => r.Copy.Parent != group))
            return null;
        static double Value(XElement? e, string name) =>
            double.TryParse((string?)e?.Attribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
        var (chOff, chExt) = (xfrm.Element(Ns.A + "chOff"), xfrm.Element(Ns.A + "chExt"));
        var (ox, oy, cw, ch) = (Value(chOff, "x"), Value(chOff, "y"), Value(chExt, "cx"), Value(chExt, "cy"));
        var boxes = group.Elements()
            .Select(e => (Element: e, Xfrm: e.Name == Drawings.Wpg + "grpSp" ? e.Element(Drawings.Wpg + "grpSpPr")?.Element(Ns.A + "xfrm") : e.Descendants(Ns.A + "xfrm").FirstOrDefault()))
            .Where(b => b.Element.Name != Drawings.Wpg + "grpSpPr" && b.Xfrm?.Element(Ns.A + "off") != null && b.Xfrm.Element(Ns.A + "ext") != null)
            .Select(b => (b.Element, Off: b.Xfrm!.Element(Ns.A + "off")!, Ext: b.Xfrm.Element(Ns.A + "ext")!)).ToList();
        if (cw <= 0 || ch <= 0 || boxes.Count == 0)
            return null;
        var (left, top) = (boxes.Min(b => Value(b.Off, "x")), boxes.Min(b => Value(b.Off, "y")));
        var right = boxes.Max(b => Value(b.Off, "x") + Value(b.Ext, "cx"));
        if (right - left <= 0)
            return null;
        var sx = drawing.Width / (right - left);
        // The fitted boxes as high as their text at their new width.
        var heights = new Dictionary<XElement, double>();
        foreach (var (copy, original, text, body) in fits)
        {
            var width = Value(copy.Element(Drawings.Wps + "spPr")?.Element(Ns.A + "xfrm")?.Element(Ns.A + "ext"), "cx") * sx;
            heights[copy] = FitToText(new PlacedDrawing(original, drawing.Part, drawing.X, drawing.Y, width, drawing.Height), text, body, original, page, blocks, settings, context.Theme).Height;
        }
        // Down, the scale at which the lowest bottom reaches the box's.
        var sy = double.PositiveInfinity;
        foreach (var (element, off, ext) in boxes)
        {
            var y = Value(off, "y") - top;
            var (units, points) = heights.TryGetValue(element, out var fitted) ? (y, fitted) : (y + Value(ext, "cy"), 0.0);
            if (units > 0)
                sy = Math.Min(sy, (drawing.Height - points) / units);
        }
        if (!(sy > 0) || double.IsInfinity(sy))
            return null;
        var span = (long)Math.Round(drawing.Height / sy);
        if (span <= 0)
            return null;
        // In whole units, as the renderer reads them.
        sy = drawing.Height / span;
        foreach (var (copy, fitted) in heights)
            copy.Element(Drawings.Wps + "spPr")!.Element(Ns.A + "xfrm")!.Element(Ns.A + "ext")!.SetAttributeValue("cy", (long)Math.Round(fitted / sy));
        chOff?.SetAttributeValue("x", (long)Math.Round(left));
        chOff?.SetAttributeValue("y", (long)Math.Round(top));
        chExt?.SetAttributeValue("cx", (long)Math.Round(right - left));
        chExt?.SetAttributeValue("cy", span);
        var (x, y0) = (drawing.X, drawing.Y);
        if (drawing.Drawing.Element(Drawings.Wp + "positionH")?.Element(Drawings.Wp + "posOffset") != null)
            x += (left - ox) * drawing.Width / cw;
        if (drawing.Drawing.Element(Drawings.Wp + "positionV")?.Element(Drawings.Wp + "posOffset") != null)
            y0 += (top - oy) * drawing.Height / ch;
        return drawing with { X = x, Y = y0 };
    }

    /// <summary>The fitting of <see cref="Fitted"/>, which the paginator applies too before text wraps around the box.</summary>
    /// <param name="settings">The settings the box's text is laid out under (see TextSettings).</param>
    public static PlacedDrawing FitToText(PlacedDrawing drawing, List<Block> text, XElement body, XElement? shape, PageBox page, TableLayout blocks,
        TextSettings settings, Drawing.Theme? theme)
    {
        // Once: insets that grow with the shape are those of the size it had (see FitToTextIn).
        if (drawing.FittedToText)
            return drawing;
        // As the box lays its text out (see TextBox): its whole width, not whole cells of the character grid.
        return FitToTextIn(drawing, text, body, shape, page, blocks, settings with { GridCell = null }, theme) with { FittedToText = true };
    }

    static PlacedDrawing FitToTextIn(PlacedDrawing drawing, List<Block> text, XElement body, XElement? shape, PageBox page, TableLayout blocks,
        TextSettings settings, Drawing.Theme? theme)
    {
        var autofit = body.Element(Ns.A + "spAutoFit") != null;
        var unwrapped = (string?)body.Attribute("wrap") == "none";
        // Vertical text that fits its shape makes it as wide as its lines are thick, its insets and outline beside them,
        // its height and left edge kept (docx/external/libreoffice/extras-ooxmlimport/tdf124754: two lines down a box
        // 40 % of the margin wide, 38.8 pt wide in Word).
        if (Vertical(body) && autofit)
        {
            var (vl, vt, vr, vb) = Insets(body, shape, drawing.Width, drawing.Height, theme);
            var verticalFields = new PageFields(page.DisplayNumber, page.Section.PageNumberFormat, 0, 0) { Chapter = page.ChapterPrefix };
            // Laid out with vertical lines, as the box draws them (docx/external/libreoffice/core-layout/
            // textbox-autogrow-vertical: twelve lines of the vertical pitch).
            var vert = (string?)body.Attribute("vert");
            var (_, _, _, thickness) = blocks.Blocks(text, Math.Max(1, drawing.Height - vt - vb), settings, verticalFields,
                direction: (true, vert is "eaVert" or "mongolianVert" or "wordArtVert" or "wordArtVertRtl"));
            return drawing with { Width = thickness + vl + vr };
        }
        if (!autofit && !unwrapped || Vertical(body))
            return drawing;
        var (l, t, r, b) = Insets(body, shape, drawing.Width, drawing.Height, theme);
        var fields = new PageFields(page.DisplayNumber, page.Section.PageNumberFormat, 0, 0) { Chapter = page.ChapterPrefix };
        // Lines measured where they begin: a right aligned line's length is its text's, not its place in the unwrapped
        // width (docx/external/libreoffice/extras-ooxmlexport/testWordArtWithinDraingtool: WordArt right aligned in a
        // box that does not wrap stood 100,000 pt to the right).
        var (lines, _, _, height) = blocks.Blocks(text, unwrapped ? 1e5 : Math.Max(1, drawing.Width - l - r), settings, fields, unaligned: unwrapped);
        // The shape is as wide as its longest line and the insets it had, narrower or wider than it was, its left kept,
        // whether or not it fits its height to the text (measured with docx/local/vws-*: "Hi" in a box 114 pt wide 9.4 pt
        // wide, a line of 132.1 pt 132.1 pt wide, VML's mso-wrap-style:none and DrawingML's wrap none alike; an ellipse
        // 140 pt wide around "Hi" 50.4 pt wide, its text rectangle's 41 pt of insets kept; LibreOffice's
        // extras-layout/shape-anchored-in-follow).
        if (unwrapped)
        {
            var longest = lines.Select(p => p.Line.Pieces.Count > 0 ? p.Left + p.Line.Pieces[^1].X + p.Line.Pieces[^1].Width : 0).DefaultIfEmpty(0).Max();
            drawing = drawing with { Width = longest > 0 ? longest + l + r : Math.Max(drawing.Width, l + r) };
        }
        if (autofit)
            drawing = drawing with { Height = height + t + b };
        return drawing;
    }

    /// <summary>
    /// A text box's text laid out in its shape's rectangle less its insets (catalog: drawing.textBoxes, measured with
    /// docx/local/textboxes), its lines placed from the rectangle's top inset; null where it has none. Linked text boxes
    /// (wps:txbx id, wps:linkedTxbx id and seq) show what does not fit the boxes before them, line by line: what a box
    /// has no room for is kept for the next, which must be laid out after it.
    /// </summary>
    /// <param name="outer">The settings the box's text is laid out under, before its own direction, grid and color.</param>
    /// <param name="storyPage">The page whose header or footer the box stands in, for its fields (PageFields.On).</param>
    public BoxText? Lay(List<Block>? text, XElement? body, XElement? shape, double w, double h, PageBox page, TextSettings outer, PageBox? storyPage)
    {
        var (l, t, r, b) = Insets(body, shape, w, h, context.Theme);
        var vert = Vertical(body) ? (string?)body!.Attribute("vert") : null;
        var (areaWidth, areaHeight) = vert != null ? (h - t - b, w - l - r) : (w - l - r, h - t - b);
        var fields = new PageFields(page.DisplayNumber, page.Section.PageNumberFormat, 0, 0) { Chapter = page.ChapterPrefix, On = storyPage };
        List<PlacedLine> lines;
        List<FillRect> fills = [];
        List<Stroke> strokes = [];
        double height;
        FillRect? lastShading = null;
        var linked = shape?.Element(Drawings.Wps + "linkedTxbx");
        var story = (string?)shape?.Element(Drawings.Wps + "txbx")?.Attribute("id") ?? (string?)linked?.Attribute("id");
        var seq = linked != null && int.TryParse((string?)linked.Attribute("seq"), out var place) ? place : 0;
        // A linked box lays out the rest of its story at its own width (measured with docx/local/lke-sp: the rest of a
        // paragraph broken in a box 15.2 pt wide goes on as one line of a tab and a picture in the next, 135.6 pt wide;
        // lke-txt: "e one" of a word broken there on one line).
        if (text is not { Count: > 0 } && story != null && linked != null && continued.Remove((story, seq), out var rest))
            text = rest;
        if (text is { Count: > 0 })
        {
            // Vertical text has vertical lines; eaVert, mongolianVert and the WordArt kinds set East Asian characters
            // upright (measured with docx/local/vertical6).
            var settings = outer with { Vertical = vert != null, TurnEastAsian = vert is "eaVert" or "mongolianVert" or "wordArtVert" or "wordArtVertRtl" };
            // A text box's lines keep its whole width before Word 2013 too, which the body's shrink to whole cells of the
            // character grid (measured with docx/local/gbx-both-14: right aligned text ends at the right inset of a box
            // 185.6 pt wide under cells of 20.265 pt, as in gbx-both-15).
            settings = settings with { GridCell = null };
            // Automatic text takes the color of the shape's style (fontRef; see TextSettings.TextColor).
            settings = settings with { TextColor = drawings?.Colors.ResolveChild(shape?.Element(Drawings.Wps + "style")?.Element(Ns.A + "fontRef")) };
            // Text that does not wrap aligns in the box all the same (docx/external/libreoffice/extras-ooxmlexport/
            // testWordArtWithinDraingtool: WordArt right aligned in its box, not 100,000 pt to the right).
            var unwrapped = (string?)body?.Attribute("wrap") == "none" && vert == null;
            if (unwrapped)
                settings = settings with { AlignIn = (1e5, Math.Max(1, areaWidth)) };
            (lines, fills, strokes, height) = blocks.Blocks(text, unwrapped ? 1e5 : Math.Max(1, areaWidth), settings, fields);
            lastShading = blocks.LastShading;
        }
        else
            return null;
        // Lines of a linked story beyond the box continue in the next one; a box keeps its first line, however far it
        // reaches past the box's room (measured with docx/local/lkb-1-*: a first line 0.5 to 5 pt taller than the room
        // stays, lkb-2-*: the second line 0.5 pt past the bottom inset goes on; docx/external/libreoffice/extras-layout/
        // tdf152983-1-min, found by agent BH).
        var firstTop = lines.Count > 0 ? lines.Min(p => p.Top) : 0;
        // A box without room inside its insets and outline loses its first line: no box shows it, the next goes on with
        // the line after it (measured with docx/local/lkc-*: boxes of a 0.5 pt outline 0.55 pt and more higher than their
        // insets keep "Line one", 0.5 pt and less lose it; lkd-pic-*: a line of a picture alike).
        var lost = story != null && lines.Count > 0 && areaHeight <= 0.01;
        // The last box of a chain shows the rest of its story, however far it reaches past the box (measured with
        // docx/local/lkf-1: a second box with room for one line shows three; extras-layout/tdf152983-1-min: a picture's
        // line 4.3 pt past the bottom inset).
        var last = story != null && shape?.Document is { } part && !part.Descendants(Drawings.Wps + "linkedTxbx").Any(l =>
            (string?)l.Attribute("id") == story && int.TryParse((string?)l.Attribute("seq"), out var later) && later > seq);
        if (story != null && !last && (lost || lines.Any(p => p.Top + p.Line.FitHeight > areaHeight + 0.01 && p.Top > firstTop + 0.01)))
        {
            var kept = lost ? [] : lines.Where(p => p.Top + p.Line.FitHeight <= areaHeight + 0.01 || p.Top <= firstTop + 0.01).ToList();
            var over = lines.Except(kept).Where(p => !lost || p.Top > firstTop + 0.01).ToList();
            var shift = over.Count > 0 ? over.Min(p => p.Top) : double.MaxValue;
            if (over.Count > 0 && Resumed(text!, over.Where(p => p.Top <= shift + 0.01).MinBy(p => p.Left)!.Line) is { } resumed)
                continued[(story, seq + 1)] = resumed;
            (lines, height) = (kept, kept.Count > 0 ? kept.Max(p => p.Top + p.Line.Height) : 0);
            fills = lost ? [] : fills.Where(f => f.Y < shift).ToList();
            strokes = lost ? [] : strokes.Where(s => s.Y1 < shift).ToList();
        }
        // Text taller than its box begins at its top, whatever its anchor (docx/external/libreoffice/extras-ooxmlexport/
        // tdf131922_LanguageInGroupShape: five lines centered in a box for four, from the top inset; fdo83057).
        var free = Math.Max(0, areaHeight - height);
        var offset = (string?)body?.Attribute("anchor") switch { "ctr" => free / 2, "b" => free, _ => 0 };
        // The last paragraph's shading reaches down to the box's bottom inset where the text with its space after
        // leaves room below it (measured with docx/local/grr-h, grr-k, grr-l, grr-n: a shaded last paragraph, also with
        // 6 pt after, also alone in a box 27.5 pt high, shaded down to 3.6 pt above the box's bottom; grr-i, grr-o: one
        // before another paragraph not; grr-g, grr-m in a group; docx/external/libreoffice/extras-ooxmlexport/
        // testWPGZOrder: a line of 15.5 pt with 10 pt after in a box of 19.5 pt shaded 15.5 pt).
        if (lastShading is { } shade && free > 0.01 && fills.IndexOf(shade) is >= 0 and var at)
            fills[at] = shade with { Height = Math.Max(shade.Height, areaHeight - offset - shade.Y) };
        return new BoxText(lines, fills, strokes, offset, vert, (l, t, r, b));
    }

    /// <summary>
    /// A story's blocks from one of its lines on: the line's paragraph from the line's text, then the blocks after it;
    /// null where the line is not of one of the blocks' paragraphs (a table's).
    /// </summary>
    static List<Block>? Resumed(List<Block> blocks, Line line)
    {
        var index = blocks.FindIndex(b => ReferenceEquals(b, line.Paragraph));
        return index < 0 ? null : [((ParagraphBlock)blocks[index]) with { ResumeAt = line.TextStart }, .. blocks.Skip(index + 1)];
    }

    /// <summary>
    /// The insets of a text box from its shape's edges: wps:bodyPr's (from the geometry's text rectangle: rounded
    /// corners, ellipses) and half its outline's width, Word measuring them from the outline's inner edge, drawn or
    /// not: the width of a:ln, else of the theme's line its style refers to (measured with docx/local/textboxes: a 6 pt
    /// outline moves the text 3 pt in; fref-inset: theme lines of 1, 1.5 and 2 pt half as far, also unfilled, a:ln's
    /// own 0.5 pt over the theme's, 2 pt lines unfilled or without a fill 1 pt; docx/external/libreoffice/
    /// extras-ooxmlexport/textframe-in-shape).
    /// </summary>
    public static (double L, double T, double R, double B) Insets(XElement? body, XElement? shape, double w, double h, Drawing.Theme? theme)
    {
        double Inset(string name, double fallback) => double.TryParse((string?)body?.Attribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v / 12700 : fallback;
        static double? Width(XElement? ln) =>
            double.TryParse((string?)ln?.Attribute("w"), NumberStyles.Float, CultureInfo.InvariantCulture, out var lw) ? lw / 12700 : null;
        var (l, t, r, b) = (Inset("lIns", 7.2), Inset("tIns", 3.6), Inset("rIns", 7.2), Inset("bIns", 3.6));
        var (gl, gt, gr, gb) = shape != null ? Drawings.ShapeTextRect(shape, w, h) : default;
        var themed = shape?.Element(Drawings.Wps + "style")?.Element(Ns.A + "lnRef") is { } lnRef
            && int.TryParse((string?)lnRef.Attribute("idx"), out var idx) && idx > 0 ? theme?.LineStyle(idx) : null;
        var outline = (Width(shape?.Element(Drawings.Wps + "spPr")?.Element(Ns.A + "ln")) ?? Width(themed) ?? 0) / 2;
        return (l + gl + outline, t + gt + outline, r + gr + outline, b + gb + outline);
    }

    /// <summary>Whether a text box's text runs vertically (wps:bodyPr/@vert): its lines turned a quarter.</summary>
    public static bool Vertical(XElement? body) => (string?)body?.Attribute("vert") is "vert" or "vert270" or "eaVert" or "mongolianVert" or "wordArtVert" or "wordArtVertRtl";
}
