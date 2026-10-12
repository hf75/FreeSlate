using System.Xml.Linq;

namespace FreeSlate.Docx;

/// <summary>
/// Reads tables (catalog: table.structure, table.styles): the table style along its basedOn chain under the table's
/// own properties, the grid, rows with their exceptions and cells; every cell gets the conditional formats of the
/// style that apply to it (switched by w:tblLook, in Word's order: whole table, column bands, row bands, first and
/// last row, first and last column, corner cells, ISO/IEC 29500 17.7.6), with their cell properties, borders and
/// the paragraph and run properties its paragraphs inherit.
/// </summary>
public sealed partial class ModelReader
{
    /// <summary>The table style's paragraph and run properties for the paragraphs of the cell being read.</summary>
    ParagraphFormat? tableParagraph;
    RunFormat? tableRun;

    TableBlock Table(XElement tbl)
    {
        var model = new TableModel();
        var tblPr = tbl.Child("tblPr");
        var styleId = tblPr.Child("tblStyle").Val();
        var chain = context.Styles.Chain(styleId is { } id && context.Styles.Find(id) is { Type: "table" } ? id : context.Styles.DefaultTableStyle)
            .Where(s => s.Type == "table").ToList();
        foreach (var style in chain)
            model.Format.Apply(style.TableProperties);
        model.Format.Styled = chain.Count > 0;
        model.Format.Apply(tblPr);
        foreach (var col in tbl.Child("tblGrid")?.Elements(Wml.W + "gridCol") ?? [])
            model.Grid.Add(col.Twips("w") ?? 0);

        var rows = Rows(tbl, context.Markup).ToList();
        if (context.Markup && tblPr?.Child("tblPrChange") is { } tblPrChange)
            model.Change = new FormatChange("", context.Author(tblPrChange.Attr("author")));
        foreach (var tr in rows)
        {
            var row = new RowModel();
            foreach (var style in chain)
                row.Format.Apply(style.Element.Child("trPr"));
            row.Format.Apply(tr.Child("trPr"));
            if (tr.Child("tblPrEx") is { } exceptions)
            {
                row.BorderExceptions = BorderSet.Read(exceptions.Child("tblBorders"));
                row.MarginExceptions = CellMargins.Read(exceptions.Child("tblCellMar"));
                row.ShadingException = exceptions.Child("shd");
            }
            var column = row.Format.GridBefore;
            foreach (var tc in Cells(tr))
            {
                var cell = new CellModel { Column = column };
                // The span is needed before the formats are resolved: the direct one counts.
                var span = Math.Max(1, (int?)tc.Child("tcPr").Child("gridSpan").Number("val") ?? 1);
                column += span;
                cell.Format.Apply(tc.Child("tcPr"));
                row.Cells.Add(cell);
            }
            model.Rows.Add(row);
        }

        // Conditional formats and the formatting they bring, then the cells' content in their light.
        for (var r = 0; r < model.Rows.Count; r++)
        {
            var row = model.Rows[r];
            var cellElements = Cells(rows[r]).ToList();
            for (var c = 0; c < row.Cells.Count; c++)
            {
                var cell = row.Cells[c];
                cell.Conditions.AddRange(Conditions(model, r, c));
                var format = new CellFormat();
                foreach (var style in chain)
                    format.Apply(style.Element.Child("tcPr"));
                var borders = new BorderSet();
                var paragraph = new ParagraphFormat();
                var run = new RunFormat();
                foreach (var style in chain)
                {
                    paragraph.Overlay(Read(style.ParagraphProperties));
                    run.Overlay(ReadRun(style.RunProperties));
                }
                foreach (var condition in cell.Conditions)
                {
                    foreach (var style in chain)
                    {
                        var conditional = style.Element.Elements(Wml.W + "tblStylePr").FirstOrDefault(e => e.Attr("type") == condition);
                        if (conditional == null)
                            continue;
                        format.Apply(WithoutBorders(conditional.Child("tcPr")));
                        borders = borders.Overlay(RegionBorders(BorderSet.Read(conditional.Child("tcPr").Child("tcBorders")), model, r, c, condition));
                        paragraph.Overlay(Read(conditional.Child("pPr")));
                        run.Overlay(ReadRun(conditional.Child("rPr")));
                    }
                }
                var direct = cellElements[c].Child("tcPr");
                format.Apply(direct);
                CopyFormat(format, cell.Format);
                cell.CellBorders = borders.Overlay(BorderSet.Read(direct.Child("tcBorders")));

                var (savedParagraph, savedRun, savedRevision) = (tableParagraph, tableRun, revision);
                (tableParagraph, tableRun) = (paragraph, run);
                // In the markup view a deleted or inserted row's text is marked as its change.
                if (context.Markup && rows[r].Child("trPr") is { } trPr && (trPr.Child("del") ?? trPr.Child("ins")) is { } rowChange)
                    revision = new RevisionMark(rowChange.Name.LocalName, context.Author(rowChange.Attr("author")));
                ReadBlocks(cellElements[c], cell.Blocks);
                (tableParagraph, tableRun, revision) = (savedParagraph, savedRun, savedRevision);
            }
        }
        // Horizontally merged cells of old documents (w:hMerge): a cell that continues the merge widens the one before
        // it, which keeps its content and takes the right border of the last (measured with docx/local/hmerge: the
        // content of the continuing cells does not show).
        foreach (var row in model.Rows)
        {
            for (var c = 1; c < row.Cells.Count; c++)
            {
                var (cell, before) = (row.Cells[c], row.Cells[c - 1]);
                if (cell.Format.HorizontalMerge != "continue" || before.Format.HorizontalMerge is null)
                    continue;
                before.Format.GridSpan += cell.Format.GridSpan;
                if (before.Format.Width is { Type: "dxa" } a && cell.Format.Width is { Type: "dxa" } b)
                    before.Format.Width = a with { Value = a.Value + b.Value };
                before.CellBorders.Right = cell.CellBorders.Right;
                row.Cells.RemoveAt(c--);
            }
        }
        return new TableBlock(tbl, model);
    }

    static ParagraphFormat Read(XElement? pPr)
    {
        var format = new ParagraphFormat();
        format.Apply(pPr);
        return format;
    }

    static RunFormat ReadRun(XElement? rPr)
    {
        var format = new RunFormat();
        format.Apply(rPr);
        return format;
    }

    /// <summary>The cell properties without their borders, which the regions of the conditional formats place.</summary>
    static XElement? WithoutBorders(XElement? tcPr)
    {
        if (tcPr?.Child("tcBorders") == null)
            return tcPr;
        var copy = new XElement(tcPr);
        copy.Element(Wml.W + "tcBorders")?.Remove();
        return copy;
    }

    static void CopyFormat(CellFormat from, CellFormat to)
    {
        to.Width = from.Width;
        to.GridSpan = from.GridSpan;
        to.VerticalMerge = from.VerticalMerge;
        to.HorizontalMerge = from.HorizontalMerge;
        to.Shading = from.Shading;
        to.Margins = from.Margins;
        to.VerticalAlign = from.VerticalAlign;
        to.TextDirection = from.TextDirection;
        to.NoWrap = from.NoWrap;
        to.FitText = from.FitText;
        to.HideMark = from.HideMark;
    }

    /// <summary>
    /// The rows of a table, also inside content controls and custom XML; rows deleted as a tracked change are gone, as
    /// Word prints the document without markup (catalog: review.revisions; measured with docx/generated/revisions),
    /// and stay in the markup view.
    /// </summary>
    static IEnumerable<XElement> Rows(XElement tbl, bool markup)
    {
        foreach (var e in tbl.Elements())
        {
            if (e.Name == Wml.W + "tr")
            {
                if (markup || e.Child("trPr")?.Child("del") == null)
                    yield return e;
            }
            else if (e.Name == Wml.W + "sdt" && e.Child("sdtContent") is { } content)
                foreach (var row in Rows(content, markup)) yield return row;
            else if (e.Name == Wml.W + "customXml")
                foreach (var row in Rows(e, markup)) yield return row;
        }
    }

    static IEnumerable<XElement> Cells(XElement tr)
    {
        foreach (var e in tr.Elements())
        {
            if (e.Name == Wml.W + "tc")
                yield return e;
            else if (e.Name == Wml.W + "sdt" && e.Child("sdtContent") is { } content)
                foreach (var cell in Cells(content)) yield return cell;
            else if (e.Name == Wml.W + "customXml")
                foreach (var cell in Cells(e)) yield return cell;
        }
    }

    /// <summary>The conditional formats of the cell in row r, position c of its row, in the order they apply.</summary>
    static IEnumerable<string> Conditions(TableModel model, int r, int c)
    {
        var f = model.Format;
        var rows = model.Rows.Count;
        var cells = model.Rows[r].Cells.Count;
        var firstRow = f.FirstRow && r == 0;
        var lastRow = f.LastRow && r == rows - 1 && rows > 1;
        var firstColumn = f.FirstColumn && c == 0;
        var lastColumn = f.LastColumn && c == cells - 1 && cells > 1;
        yield return "wholeTable";
        // Bands skip the first and last column and row where those have formats of their own.
        if (!f.NoVerticalBand && !firstColumn && !lastColumn)
        {
            var band = (c - (f.FirstColumn ? 1 : 0)) / Math.Max(1, f.ColumnBandSize);
            yield return band % 2 == 0 ? "band1Vert" : "band2Vert";
        }
        if (!f.NoHorizontalBand && !firstRow && !lastRow)
        {
            var band = (r - (f.FirstRow ? 1 : 0)) / Math.Max(1, f.RowBandSize);
            yield return band % 2 == 0 ? "band1Horz" : "band2Horz";
        }
        if (firstRow) yield return "firstRow";
        if (lastRow) yield return "lastRow";
        if (firstColumn) yield return "firstCol";
        if (lastColumn) yield return "lastCol";
        if (firstRow && firstColumn) yield return "nwCell";
        if (firstRow && lastColumn) yield return "neCell";
        if (lastRow && firstColumn) yield return "swCell";
        if (lastRow && lastColumn) yield return "seCell";
    }

    /// <summary>
    /// A conditional format's borders for one cell: its sides where the cell's edge is the region's edge, its inside
    /// lines where the edge lies inside the region (a row region's cells meet at inside vertical lines …).
    /// </summary>
    static BorderSet? RegionBorders(BorderSet? set, TableModel model, int r, int c, string condition)
    {
        if (set == null)
            return null;
        var rows = model.Rows.Count;
        var cells = model.Rows[r].Cells.Count;
        var f = model.Format;
        var (rowRegion, columnRegion) = condition switch
        {
            "firstRow" or "lastRow" => (true, false),
            "firstCol" or "lastCol" => (false, true),
            "band1Horz" or "band2Horz" => (true, false),
            "band1Vert" or "band2Vert" => (false, true),
            "wholeTable" => (true, true),
            _ => (false, false),   // corner cells: the cell itself
        };
        bool topEdge, bottomEdge, leftEdge, rightEdge;
        if (condition is "band1Horz" or "band2Horz")
        {
            var size = Math.Max(1, f.RowBandSize);
            var offset = r - (f.FirstRow ? 1 : 0);
            topEdge = offset % size == 0;
            bottomEdge = offset % size == size - 1 || r == rows - 1;
        }
        else
        {
            topEdge = !columnRegion || r == 0;
            bottomEdge = !columnRegion || r == rows - 1;
        }
        if (condition is "band1Vert" or "band2Vert")
        {
            var size = Math.Max(1, f.ColumnBandSize);
            var offset = c - (f.FirstColumn ? 1 : 0);
            leftEdge = offset % size == 0;
            rightEdge = offset % size == size - 1 || c == cells - 1;
        }
        else
        {
            leftEdge = !rowRegion || c == 0;
            rightEdge = !rowRegion || c == cells - 1;
        }
        return new BorderSet
        {
            Top = topEdge ? set.Top : set.InsideH,
            Bottom = bottomEdge ? set.Bottom : set.InsideH,
            Left = leftEdge ? set.Left : set.InsideV,
            Right = rightEdge ? set.Right : set.InsideV,
            TopLeftToBottomRight = set.TopLeftToBottomRight,
            TopRightToBottomLeft = set.TopRightToBottomLeft,
        };
    }
}
