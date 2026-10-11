using System.Text;
using System.Xml.Linq;
using FreeSlate.Drawing;
using FreeSlate.Html;
using FreeSlate.Text;

namespace FreeSlate.Pptx;

/// <summary>
/// Renders a:tbl (catalog: table.structure, table.cells, table.styles): the grid is laid out by the engine,
/// rows grow to fit their text, and every cell gets fill, borders and text from its own properties and from the
/// table style parts that apply to it.
/// </summary>
public sealed class TableRenderer(SlideContext slide, SvgPaint paint, TextRenderer text)
{
    const double PxPerPt = 96.0 / 72;

    /// <summary>Style parts in the order PowerPoint applies them; later parts win.</summary>
    static readonly string[] PartOrder = ["wholeTbl", "band1H", "band2H", "band1V", "band2V", "lastCol", "firstCol", "lastRow", "firstRow", "seCell", "swCell", "neCell", "nwCell"];

    sealed record Cell(XElement Element, int Row, int Column, int RowSpan, int ColumnSpan);

    /// <summary>SVG content (pixels, frame-local) and the table's size in pixels.</summary>
    public (string Svg, double Width, double Height) Render(XElement table)
    {
        var tblPr = table.Element(Ns.A + "tblPr");
        var columns = table.Element(Ns.A + "tblGrid")?.Elements(Ns.A + "gridCol").Select(c => (c.Long("w") ?? 0) / Units.EmuPerPoint).ToList() ?? [];
        var rows = table.Elements(Ns.A + "tr").ToList();
        if (columns.Count == 0 || rows.Count == 0)
            return ("", 0, 0);

        var style = Style(tblPr);

        // Cells with their spans; hMerge/vMerge cells are covered by another cell and not drawn.
        var cells = new List<Cell>();
        for (var r = 0; r < rows.Count; r++)
        {
            var c = 0;
            foreach (var tc in rows[r].Elements(Ns.A + "tc"))
            {
                if (!(tc.Bool("hMerge") ?? false) && !(tc.Bool("vMerge") ?? false))
                    cells.Add(new Cell(tc, r, c, Math.Max(1, tc.Int("rowSpan") ?? 1), Math.Max(1, tc.Int("gridSpan") ?? 1)));
                c++;
            }
        }

        var xs = new double[columns.Count + 1];
        for (var c = 0; c < columns.Count; c++)
            xs[c + 1] = xs[c] + columns[c];

        // Row heights: the stored height is a minimum; rows grow to fit the text of their single-row cells.
        var heights = rows.Select(r => (r.Long("h") ?? 0) / Units.EmuPerPoint).ToArray();
        var layouts = new Dictionary<Cell, LaidOutText>();
        foreach (var cell in cells)
        {
            var (marL, marR, marT, marB) = Margins(cell.Element);
            var width = Span(xs, cell.Column, cell.ColumnSpan) - marL - marR;
            // Vertical cell text (tcPr vert) wraps at the row's height and makes the row as long as its longest line.
            var turned = Turned(cell);
            var stored = Enumerable.Range(cell.Row, Math.Min(cell.RowSpan, heights.Length - cell.Row)).Sum(r => heights[r]);
            var layout = text.LayoutCell(cell.Element.Element(Ns.A + "txBody") ?? new XElement(Ns.A + "txBody"), TextStyles(style, Parts(cell, rows.Count, columns.Count, tblPr)),
                turned != null ? Math.Max(1, stored - marT - marB) : width);
            layouts[cell] = layout;
            var extent = turned != null ? (layout.Lines.Count == 0 ? 0 : layout.Lines.Max(l => l.Width)) : layout.Height;
            if (cell.RowSpan == 1 && cell.Row < heights.Length)
                heights[cell.Row] = Math.Max(heights[cell.Row], extent + marT + marB);
        }
        var ys = new double[rows.Count + 1];
        for (var r = 0; r < rows.Count; r++)
            ys[r + 1] = ys[r] + heights[r];

        var defs = new StringBuilder();
        var fills = new StringBuilder();
        var borders = new StringBuilder();
        var texts = new StringBuilder();
        // Each edge is drawn once: of the two cells sharing it, the border with the higher priority wins (own cell
        // borders over style parts, later style parts over earlier ones); measured on a total row's double top
        // border, which the row above does not paint over.
        var edges = new Dictionary<(bool Horizontal, double At, double From, double To), (Outline Outline, int Priority, double X1, double Y1, double X2, double Y2)>();

        // The table background behind all cells: the table's own fill, else the style's tblBg (a fill or a
        // theme fill reference), visible through cells without a fill or with transparent ones.
        var background = TableBackground(tblPr, style);
        if (background is not NoFill)
            fills.Append($"<rect width=\"{Px(xs[^1])}\" height=\"{Px(ys[^1])}\"{paint.Fill(background, xs[^1] * PxPerPt, ys[^1] * PxPerPt, defs)}/>");

        // A right-to-left table (tblPr rtl) puts its first column on the right: every cell is mirrored with its left
        // and right borders, the text in it is not (measured).
        var rtl = tblPr.Bool("rtl") == true;
        foreach (var cell in cells)
        {
            var parts = Parts(cell, rows.Count, columns.Count, tblPr).Select(p => style?.Element(Ns.A + p)).Where(p => p != null).Cast<XElement>().ToList();
            var w = Span(xs, cell.Column, cell.ColumnSpan);
            var x = rtl ? xs[^1] - xs[cell.Column] - w : xs[cell.Column];
            var y = ys[cell.Row];
            var h = Math.Max(0, ys[Math.Min(rows.Count, cell.Row + cell.RowSpan)] - y);
            var tcPr = cell.Element.Element(Ns.A + "tcPr");

            var fill = CellFill(tcPr, parts);
            var cell3D = tcPr?.Element(Ns.A + "cell3D")
                         ?? Enumerable.Range(0, parts.Count).Reverse().Select(i => parts[i].Element(Ns.A + "tcStyle")?.Element(Ns.A + "cell3D")).FirstOrDefault(e => e != null);
            if (fill is not NoFill && cell3D?.Element(Ns.A + "bevel") is { } bevel)
                fills.Append(Beveled(cell3D, bevel, fill, x, y, w, h, defs));
            else if (fill is not NoFill)
                fills.Append($"<rect x=\"{Px(x)}\" y=\"{Px(y)}\" width=\"{Px(w)}\" height=\"{Px(h)}\"{paint.Fill(fill, w * PxPerPt, h * PxPerPt, defs)}/>");

            // A style part's borders refer to its own region (a row, a column, a corner cell or the whole table):
            // an edge on the region's outline takes top/bottom/left/right, an edge inside it insideH/insideV.
            var lastRow = cell.Row + cell.RowSpan >= rows.Count;
            var lastColumn = cell.Column + cell.ColumnSpan >= columns.Count;
            static bool RowRegion(string part) => part is "band1H" or "band2H" or "firstRow" or "lastRow" || part.EndsWith("Cell", StringComparison.Ordinal);
            static bool ColumnRegion(string part) => part is "band1V" or "band2V" or "firstCol" or "lastCol" || part.EndsWith("Cell", StringComparison.Ordinal);
            string Side(string part, string outer, bool onTableEdge, bool horizontalEdge) =>
                (horizontalEdge ? RowRegion(part) : ColumnRegion(part)) || onTableEdge ? outer : horizontalEdge ? "insideH" : "insideV";
            void Edge(string own, Func<string, string> side, double x1, double y1, double x2, double y2)
            {
                if (Border(tcPr, parts, own, side) is not ({ } outline, var priority))
                    return;
                var horizontal = y1 == y2 && x1 != x2;
                var key = horizontal ? (true, Math.Round(y1, 3), Math.Round(Math.Min(x1, x2), 3), Math.Round(Math.Max(x1, x2), 3))
                    : x1 == x2 ? (false, Math.Round(x1, 3), Math.Round(Math.Min(y1, y2), 3), Math.Round(Math.Max(y1, y2), 3))
                    : (false, double.NaN, x1 * 1000 + y1, x2 * 1000 + y2);   // diagonals are never shared
                if (!edges.TryGetValue(key, out var existing) || priority >= existing.Priority)
                    edges[key] = (outline, priority, x1, y1, x2, y2);
            }
            var (leftX, rightX) = rtl ? (x + w, x) : (x, x + w);
            Edge("lnL", part => Side(part, "left", cell.Column == 0, false), leftX, y, leftX, y + h);
            Edge("lnR", part => Side(part, "right", lastColumn, false), rightX, y, rightX, y + h);
            Edge("lnT", part => Side(part, "top", cell.Row == 0, true), x, y, x + w, y);
            Edge("lnB", part => Side(part, "bottom", lastRow, true), x, y + h, x + w, y + h);
            Edge("lnTlToBr", _ => "tl2br", x, y, x + w, y + h);
            Edge("lnBlToTr", _ => "tr2bl", x, y + h, x + w, y);

            var (marL, marR, marT, marB) = Margins(cell.Element);
            if (rtl)
                (marL, marR) = (marR, marL);
            if (tcPr.Bool("anchorCtr") == true)
                TextLayout.CenterBlock(layouts[cell], w - marL - marR);
            texts.Append(Turned(cell) is { } mode
                ? text.EmitTurned(layouts[cell], mode, x + marL, y + marT, w - marL - marR, h - marT - marB, tcPr.Str("anchor") ?? "t")
                : text.Emit(layouts[cell], x + marL, y + marT, h - marT - marB, tcPr.Str("anchor") ?? "t"));
        }

        foreach (var edge in edges.Values)
            Draw(borders, defs, edge.Outline, edge.X1, edge.Y1, edge.X2, edge.Y2);
        // The table's effects (a:tblPr/a:effectLst): the cells' fills and borders cast its shadow and glow, the text
        // does not; translucent fills let the shadow show through (measured: LibreOffice's table-shadow-blur).
        var body = fills.ToString() + borders;
        var effects = tblPr?.Element(Ns.A + "effectLst");
        if (SvgEffects.Filter(effects, slide.Colors, xs[^1] * PxPerPt, ys[^1] * PxPerPt, defs, parts: SvgEffects.Parts.Outer) is { } outer)
            body = $"<g filter=\"url(#{outer})\">{body}</g>" + body;
        // A scaled, skewed or flipped shadow is a transformed copy of the table behind it, as for shapes (measured:
        // LibreOffice's tdf150020-shadow-alignment, a black shadow 130 % the table's size about its center).
        if (SvgEffects.Shadow(effects, slide.Colors, xs[^1] * PxPerPt, ys[^1] * PxPerPt, defs) is { } copy)
            body = $"<g filter=\"url(#{copy.Filter})\"><g transform=\"{copy.Transform.Css()}\">{fills}{borders}</g></g>" + body;
        var svg = (defs.Length > 0 ? $"<defs>{defs}</defs>" : "") + body + texts;
        return (svg, xs[^1] * PxPerPt, ys[^1] * PxPerPt);
    }

    Fill TableBackground(XElement? tblPr, XElement? style)
    {
        if (tblPr?.Elements().FirstOrDefault(e => e.Name.LocalName is "solidFill" or "gradFill" or "pattFill" or "blipFill" or "noFill") is { } own)
            return FillReader.Read(own, slide.Colors);
        var tblBg = style?.Element(Ns.A + "tblBg");
        if (tblBg?.Element(Ns.A + "fillRef") is { } fillRef && slide.Theme.FillStyle(fillRef.Int("idx") ?? 0) is { } themed)
            return slide.ThemedFill(themed, slide.Colors.WithPlaceholder(slide.Colors.ResolveChild(fillRef)));
        if (tblBg?.Element(Ns.A + "fill")?.Elements().FirstOrDefault() is { } fill)
            return FillReader.Read(fill, slide.Colors);
        return Fill.None;
    }

    static double Span(double[] xs, int start, int count) => xs[Math.Min(xs.Length - 1, start + count)] - xs[start];

    static string Px(double points) => Units.Css(points * PxPerPt);

    static (double L, double R, double T, double B) Margins(XElement tc)
    {
        var tcPr = tc.Element(Ns.A + "tcPr");
        double M(string name, long fallback) => (tcPr.Long(name) ?? fallback) / Units.EmuPerPoint;
        return (M("marL", 91440), M("marR", 91440), M("marT", 45720), M("marB", 45720));
    }

    /// <summary>
    /// The table style: PowerPoint draws tables only in its built-in styles, looked up by a:tableStyleId (measured: a
    /// definition in ppt/tableStyles.xml never shows, nor does an inline a:tableStyle; a built-in id draws the built-in
    /// style whatever the file defines under it, and any other id draws No Style, Table Grid, as does a table without
    /// one, not the presentation's default style).
    /// </summary>
    static XElement? Style(XElement? tblPr) =>
        BuiltInTableStyles.Find(tblPr?.Element(Ns.A + "tableStyleId")?.Value ?? "") ?? BuiltInTableStyles.Find(BuiltInTableStyles.TableGrid);

    /// <summary>The names of the style parts that apply to a cell, lowest priority first.</summary>
    static List<string> Parts(Cell cell, int rowCount, int columnCount, XElement? tblPr)
    {
        bool Flag(string name) => tblPr.Bool(name) ?? false;
        var first = Flag("firstRow") && cell.Row == 0;
        var last = Flag("lastRow") && cell.Row + cell.RowSpan >= rowCount;
        var firstCol = Flag("firstCol") && cell.Column == 0;
        var lastCol = Flag("lastCol") && cell.Column + cell.ColumnSpan >= columnCount;
        var parts = new List<string> { "wholeTbl" };
        if (Flag("bandRow") && !first && !last)
            parts.Add((cell.Row - (Flag("firstRow") ? 1 : 0)) % 2 == 0 ? "band1H" : "band2H");
        if (Flag("bandCol") && !firstCol && !lastCol)
            parts.Add((cell.Column - (Flag("firstCol") ? 1 : 0)) % 2 == 0 ? "band1V" : "band2V");
        if (lastCol) parts.Add("lastCol");
        if (firstCol) parts.Add("firstCol");
        if (last) parts.Add("lastRow");
        if (first) parts.Add("firstRow");
        if (last && lastCol) parts.Add("seCell");
        if (last && firstCol) parts.Add("swCell");
        if (first && lastCol) parts.Add("neCell");
        if (first && firstCol) parts.Add("nwCell");
        return parts.OrderBy(p => Array.IndexOf(PartOrder, p)).ToList();
    }

    static List<XElement> TextStyles(XElement? style, List<string> parts) =>
        parts.AsEnumerable().Reverse()
            .Select(p => style?.Element(Ns.A + p)?.Element(Ns.A + "tcTxStyle"))
            .Where(t => t != null).Cast<XElement>().ToList();

    /// <summary>
    /// A beveled cell (a:cell3D, catalog: table.cells): the cell as a shape with a top bevel seen from the front, the
    /// bevel's strips lit by the cell's light rig (three-point from the top by default) and material (plastic), the
    /// fill on the face inside the bevel.
    /// </summary>
    string Beveled(XElement cell3D, XElement bevel, Fill fill, double x, double y, double w, double h, StringBuilder defs)
    {
        var (pw, ph) = (w * PxPerPt, h * PxPerPt);
        var width = Math.Min(Units.ToPx(bevel.Long("w") ?? 76200), Math.Min(pw, ph) / 2);
        var height = Units.ToPx(bevel.Long("h") ?? 76200);
        var scene = new XElement(Ns.A + "scene3d", new XElement(Ns.A + "camera", new XAttribute("prst", "orthographicFront")),
            cell3D.Element(Ns.A + "lightRig") is { } rig ? new XElement(rig) : new XElement(Ns.A + "lightRig", new XAttribute("rig", "threePt"), new XAttribute("dir", "t")));
        var material = new XElement(Ns.A + "sp3d", new XAttribute("prstMaterial", cell3D.Attribute("prstMaterial")?.Value ?? "plastic"));
        var camera = Camera.Read(scene)!;
        var lighting = Lighting.For(scene, material);
        var solid = new Solid(camera, lighting, pw, ph, 0, false, false);
        solid.Bevel([[(0, 0), (pw, 0), (pw, ph), (0, ph)]], width, height, BevelProfiles.Get(bevel.Attribute("prst")?.Value), Fill.MainColor(fill) ?? RgbColor.FromHex("808080"));
        // The face is lit too (measured: lighter under the three-point rig, darker as metal under the harsh one).
        if (fill is SolidFill solidFill)
            fill = new SolidFill(Lighting.Apply(solidFill.Color, lighting.Shade((0, 0, 1), camera.View((pw / 2, ph / 2, 0)))));
        var (fw, fh) = (Math.Max(0, pw - 2 * width), Math.Max(0, ph - 2 * width));
        return $"<g transform=\"translate({Px(x)} {Px(y)})\"><g transform=\"translate({Units.Css(width)} {Units.Css(width)})\"><rect width=\"{Units.Css(fw)}\" height=\"{Units.Css(fh)}\"{paint.Fill(fill, fw, fh, defs)}/></g>{solid.Markup()}</g>";
    }

    Fill CellFill(XElement? tcPr, List<XElement> parts)
    {
        if (FillReader.FindFill(tcPr) is { } own)
            return FillReader.Read(own, slide.Colors, slide.Images(slide.SlidePart), slide.ImageSizes(slide.SlidePart));
        for (var i = parts.Count - 1; i >= 0; i--)
        {
            var tcStyle = parts[i].Element(Ns.A + "tcStyle");
            if (tcStyle?.Element(Ns.A + "fill") is { } fill && FillReader.FindFill(fill) is { } styled)
                return FillReader.Read(styled, slide.Colors);
            if (tcStyle?.Element(Ns.A + "fillRef") is { } fillRef && slide.Theme.FillStyle(fillRef.Int("idx") ?? 0) is { } themed)
                return slide.ThemedFill(themed, slide.Colors.WithPlaceholder(slide.Colors.ResolveChild(fillRef)));
        }
        return Fill.None;
    }

    /// <summary>A cell border: the cell's own a:lnX, else the highest style part defining the side.</summary>
    /// <summary>The vertical text mode of a cell (tcPr vert), or null for horizontal text.</summary>
    static string? Turned(Cell cell) => cell.Element.Element(Ns.A + "tcPr").Str("vert") switch
    {
        { } mode when mode is "vert" or "vert270" or "eaVert" or "mongolianVert" => mode,
        "wordArtVert" or "wordArtVertRtl" => "eaVert",
        _ => null,
    };

    /// <summary>The border of a cell edge with its priority: own properties first, then the style parts, latest first.</summary>
    (Outline? Outline, int Priority) Border(XElement? tcPr, List<XElement> parts, string own, Func<string, string> side)
    {
        if (tcPr?.Element(Ns.A + own) is { } ownLine)
            return (OutlineReader.Merge([(ownLine, slide.Colors)]), 1000);
        for (var i = parts.Count - 1; i >= 0; i--)
        {
            var border = parts[i].Element(Ns.A + "tcStyle")?.Element(Ns.A + "tcBdr")?.Element(Ns.A + side(parts[i].Name.LocalName));
            var priority = Array.IndexOf(PartOrder, parts[i].Name.LocalName);
            if (border?.Element(Ns.A + "ln") is { } line)
                return (OutlineReader.Merge([(line, slide.Colors)]), priority);
            if (border?.Element(Ns.A + "lnRef") is { } lnRef && slide.Theme.LineStyle(lnRef.Int("idx") ?? 0) is { } themed)
                return (OutlineReader.Merge([(themed, slide.Colors.WithPlaceholder(slide.Colors.ResolveChild(lnRef)))]), priority);
        }
        return (null, -1);
    }

    void Draw(StringBuilder svg, StringBuilder defs, Outline? outline, double x1, double y1, double x2, double y2)
    {
        if (outline is not { Visible: true })
            return;
        var flat = outline with { Cap = "flat" };
        if (CompoundLines.Parts(outline.Compound) != null)
        {
            // Double, thick-thin and triple borders (catalog: line.compound) as parallel strokes.
            var path = new Drawing.Geometry.EvaluatedPath(
                [new Drawing.Geometry.PathSegment('M', [x1 * PxPerPt, y1 * PxPerPt]), new Drawing.Geometry.PathSegment('L', [x2 * PxPerPt, y2 * PxPerPt])], "none", true);
            svg.Append(CompoundLines.Strokes(path, flat, width => paint.Stroke(flat with { WidthEmu = width * Units.EmuPerPixel, Compound = "sng" }, 0, 0, defs)));
            return;
        }
        svg.Append($"<path d=\"M{Px(x1)} {Px(y1)}L{Px(x2)} {Px(y2)}\" fill=\"none\"{paint.Stroke(flat, 0, 0, defs)}/>");
    }
}
