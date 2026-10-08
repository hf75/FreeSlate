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
            var layout = text.LayoutCell(cell.Element.Element(Ns.A + "txBody") ?? new XElement(Ns.A + "txBody"), TextStyles(style, Parts(cell, rows.Count, columns.Count, tblPr)), width);
            layouts[cell] = layout;
            if (cell.RowSpan == 1 && cell.Row < heights.Length)
                heights[cell.Row] = Math.Max(heights[cell.Row], layout.Height + marT + marB);
        }
        var ys = new double[rows.Count + 1];
        for (var r = 0; r < rows.Count; r++)
            ys[r + 1] = ys[r] + heights[r];

        var defs = new StringBuilder();
        var fills = new StringBuilder();
        var borders = new StringBuilder();
        var texts = new StringBuilder();
        foreach (var cell in cells)
        {
            var parts = Parts(cell, rows.Count, columns.Count, tblPr).Select(p => style?.Element(Ns.A + p)).Where(p => p != null).Cast<XElement>().ToList();
            var x = xs[cell.Column];
            var y = ys[cell.Row];
            var w = Span(xs, cell.Column, cell.ColumnSpan);
            var h = Math.Max(0, ys[Math.Min(rows.Count, cell.Row + cell.RowSpan)] - y);
            var tcPr = cell.Element.Element(Ns.A + "tcPr");

            var fill = CellFill(tcPr, parts);
            if (fill is not NoFill)
                fills.Append($"<rect x=\"{Px(x)}\" y=\"{Px(y)}\" width=\"{Px(w)}\" height=\"{Px(h)}\"{paint.Fill(fill, w * PxPerPt, h * PxPerPt, defs)}/>");

            var lastRow = cell.Row + cell.RowSpan >= rows.Count;
            var lastColumn = cell.Column + cell.ColumnSpan >= columns.Count;
            Border(borders, defs, tcPr, parts, "lnL", cell.Column == 0 ? "left" : "insideV", x, y, x, y + h);
            Border(borders, defs, tcPr, parts, "lnR", lastColumn ? "right" : "insideV", x + w, y, x + w, y + h);
            Border(borders, defs, tcPr, parts, "lnT", cell.Row == 0 ? "top" : "insideH", x, y, x + w, y);
            Border(borders, defs, tcPr, parts, "lnB", lastRow ? "bottom" : "insideH", x, y + h, x + w, y + h);
            Border(borders, defs, tcPr, parts, "lnTlToBr", "tl2br", x, y, x + w, y + h);
            Border(borders, defs, tcPr, parts, "lnBlToTr", "tr2bl", x, y + h, x + w, y);

            var (marL, marR, marT, marB) = Margins(cell.Element);
            texts.Append(text.Emit(layouts[cell], x + marL, y + marT, h - marT - marB, tcPr.Str("anchor") ?? "t"));
        }

        var svg = (defs.Length > 0 ? $"<defs>{defs}</defs>" : "") + fills + borders + texts;
        return (svg, xs[^1] * PxPerPt, ys[^1] * PxPerPt);
    }

    static double Span(double[] xs, int start, int count) => xs[Math.Min(xs.Length - 1, start + count)] - xs[start];

    static string Px(double points) => Units.Css(points * PxPerPt);

    static (double L, double R, double T, double B) Margins(XElement tc)
    {
        var tcPr = tc.Element(Ns.A + "tcPr");
        double M(string name, long fallback) => (tcPr.Long(name) ?? fallback) / Units.EmuPerPoint;
        return (M("marL", 91440), M("marR", 91440), M("marT", 45720), M("marB", 45720));
    }

    /// <summary>The table style: inline a:tableStyle, else a:tableStyleId looked up in ppt/tableStyles.xml.</summary>
    XElement? Style(XElement? tblPr)
    {
        if (tblPr?.Element(Ns.A + "tableStyle") is { } inline)
            return inline;
        var id = tblPr?.Element(Ns.A + "tableStyleId")?.Value;
        if (id == null)
            return null;
        var package = slide.Presentation.Package;
        var part = package.FirstRelationship(slide.Presentation.PresentationPart, "tableStyles")?.Target;
        if (part == null || !package.Exists(part))
            return null;
        return slide.Presentation.Load(part).Root?.Elements(Ns.A + "tblStyle").FirstOrDefault(s => s.Str("styleId") == id);
    }

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

    Fill CellFill(XElement? tcPr, List<XElement> parts)
    {
        if (FillReader.FindFill(tcPr) is { } own)
            return FillReader.Read(own, slide.Colors, slide.Images(slide.SlidePart));
        for (var i = parts.Count - 1; i >= 0; i--)
        {
            var tcStyle = parts[i].Element(Ns.A + "tcStyle");
            if (tcStyle?.Element(Ns.A + "fill") is { } fill && FillReader.FindFill(fill) is { } styled)
                return FillReader.Read(styled, slide.Colors);
            if (tcStyle?.Element(Ns.A + "fillRef") is { } fillRef && slide.Theme.FillStyle(fillRef.Int("idx") ?? 0) is { } themed)
                return FillReader.Read(themed, slide.Colors.WithPlaceholder(slide.Colors.ResolveChild(fillRef)));
        }
        return Fill.None;
    }

    /// <summary>A cell border: the cell's own a:lnX, else the highest style part defining the side.</summary>
    void Border(StringBuilder svg, StringBuilder defs, XElement? tcPr, List<XElement> parts, string own, string side, double x1, double y1, double x2, double y2)
    {
        XElement? line = tcPr?.Element(Ns.A + own);
        if (line == null)
        {
            for (var i = parts.Count - 1; i >= 0 && line == null; i--)
            {
                var border = parts[i].Element(Ns.A + "tcStyle")?.Element(Ns.A + "tcBdr")?.Element(Ns.A + side);
                line = border?.Element(Ns.A + "ln");
                if (line == null && border?.Element(Ns.A + "lnRef") is { } lnRef && slide.Theme.LineStyle(lnRef.Int("idx") ?? 0) is { } themed)
                {
                    var outlineFromRef = OutlineReader.Merge([(themed, slide.Colors.WithPlaceholder(slide.Colors.ResolveChild(lnRef)))]);
                    Draw(svg, defs, outlineFromRef, x1, y1, x2, y2);
                    return;
                }
            }
        }
        if (line == null)
            return;
        Draw(svg, defs, OutlineReader.Merge([(line, slide.Colors)]), x1, y1, x2, y2);
    }

    void Draw(StringBuilder svg, StringBuilder defs, Outline? outline, double x1, double y1, double x2, double y2)
    {
        if (outline is not { Visible: true })
            return;
        svg.Append($"<path d=\"M{Px(x1)} {Px(y1)}L{Px(x2)} {Px(y2)}\" fill=\"none\"{paint.Stroke(outline with { Cap = "flat" }, 0, 0, defs)}/>");
    }
}
