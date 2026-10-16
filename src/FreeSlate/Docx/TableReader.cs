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

    /// <summary>
    /// The last paragraph or table of a cell, inside content controls and custom XML too (docx/external/libreoffice/
    /// extras-ooxmlexport/tdf157011_ins_del_empty_cols: Google Docs' paragraphs each in a w:sdt).
    /// </summary>
    static XElement? LastBlock(XElement container)
    {
        var last = container.Elements().LastOrDefault(e => e.Name.LocalName is "p" or "tbl" or "sdt" or "customXml");
        return last?.Name.LocalName switch
        {
            "sdt" => last.Child("sdtContent") is { } content ? LastBlock(content) : null,
            "customXml" => LastBlock(last),
            _ => last,
        };
    }

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
        var leading = LeadingRows(model);
        model.Format.FirstColumnStyled = chain.Any(s => s.Element.Elements(Wml.W + "tblStylePr").Any(e => e.Attr("type") == "firstCol"));
        // The row properties of the conditional formats of whole rows, under the row's own (measured with
        // docx/local/hc-1, hc-0: a first-row format's w:tblHeader repeats the first row where w:tblLook asks for
        // first-row formatting, not where it does not; docx/external/libreoffice/core-ooxml-pass/fdo78904_fileHang).
        for (var r = 0; r < model.Rows.Count; r++)
        {
            if (model.Rows[r].Cells.Count == 0)
                continue;
            var applied = false;
            foreach (var condition in Conditions(model, r, 0, leading).Where(c => c is "wholeTable" or "band1Horz" or "band2Horz" or "firstRow" or "lastRow"))
            {
                foreach (var style in chain)
                {
                    if (style.Element.Elements(Wml.W + "tblStylePr").FirstOrDefault(e => e.Attr("type") == condition)?.Child("trPr") is not { } trPr)
                        continue;
                    model.Rows[r].Format.Apply(trPr);
                    applied = true;
                }
            }
            if (applied)
                model.Rows[r].Format.Apply(rows[r].Child("trPr"));
        }
        for (var r = 0; r < model.Rows.Count; r++)
        {
            var row = model.Rows[r];
            var cellElements = Cells(rows[r]).ToList();
            for (var c = 0; c < row.Cells.Count; c++)
            {
                var cell = row.Cells[c];
                cell.Conditions.AddRange(Conditions(model, r, c, leading));
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
                        borders = borders.Overlay(RegionBorders(BorderSet.Read(conditional.Child("tcPr").Child("tcBorders")), model, r, c, condition, leading));
                        paragraph.Overlay(Read(conditional.Child("pPr")));
                        run.Overlay(ReadRun(conditional.Child("rPr")));
                    }
                }
                var direct = cellElements[c].Child("tcPr");
                format.Apply(direct);
                format.OwnShading = direct.Child("shd") != null;
                CopyFormat(format, cell.Format);
                cell.CellBorders = borders.Overlay(BorderSet.Read(direct.Child("tcBorders")));

                var (savedParagraph, savedRun, savedRevision) = (tableParagraph, tableRun, revision);
                (tableParagraph, tableRun) = (paragraph, run);
                // In the markup view a deleted or inserted row's text is marked as its change.
                if (context.Markup && rows[r].Child("trPr") is { } trPr && (trPr.Child("del") ?? trPr.Child("ins")) is { } rowChange)
                    revision = new RevisionMark(rowChange.Name.LocalName, context.Author(rowChange.Attr("author")));
                ReadBlocks(cellElements[c], cell.Blocks);
                JoinHidden(cell.Blocks, cell: true);
                // A cell's last paragraph whose mark is gone, deleted or moved away, joins the cell's end, which has the
                // document defaults' and the table style's paragraph properties, not the paragraph's style or its own;
                // in the row's last cell it joins the row's end, which has the document defaults' alone; the mark's own
                // run properties stay (measured with docx/local/cmv-*: a row whose last cell lost its mark 22.9 pt high,
                // the defaults' 8 pt after and lines of 259, not the table style's single lines, the Normal style's
                // 20 pt after or the paragraph's own 30 pt; with a 20 pt mark 34.8 pt; a first cell's lost mark single
                // lines without space after, 13.9 pt; a lost mark before another paragraph as any other;
                // docx/external/libreoffice/extras-ooxmlexport/TC-table-Separate-Move, TC-table-DnD-move: 56.0 → 100 %).
                if (!context.Markup && LastBlock(cellElements[c]) is { } lastElement
                    && lastElement.Name == Wml.W + "p" && lastElement.Child("pPr")?.Child("rPr") is { } lostMark
                    && (lostMark.Child("del") ?? lostMark.Child("moveFrom")) != null && cell.Blocks.LastOrDefault() is ParagraphBlock joined)
                {
                    var rowEnd = c == row.Cells.Count - 1;
                    var ending = context.Styles.DefaultParagraph.Clone();
                    if (!rowEnd)
                        ending.Overlay(paragraph);
                    var markDirect = new RunFormat();
                    markDirect.Apply(lostMark);
                    cell.Blocks[^1] = joined with
                    {
                        Format = ending,
                        MarkFormat = RunFormat.Combine(context.Styles.DefaultRun, rowEnd ? [] : [run], markDirect),
                    };
                }
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
        // A table whose cells' paragraphs have frame properties stands in that frame, as a floating table placed as the
        // frame would be (docx/local/lb-1 to lb-4: at the margin's outside top; docx/external/libreoffice/
        // extras-ooxmlexport/tdf164474: 28.4 pt below the top margin, the text after it at the top). Not a table in a
        // cell, where frames do not apply (fdo77887, extras-layout/fdo48718-1: a table framed 113.3 pt from the page's
        // top in a floating table's cell stays in it), nor one framed in line with the text (n780853: w:yAlign inline).
        if (model.Format.Floating == null && tableParagraph == null
            && model.Rows.FirstOrDefault()?.Cells.FirstOrDefault()?.Blocks.FirstOrDefault() is ParagraphBlock { Format.Frame: { } frame }
            && frame.Attr("yAlign") != "inline")
            (model.Format.Floating, model.Format.Framed) = (Floating(frame), true);
        return new TableBlock(tbl, model);
    }

    /// <summary>The floating table position (w:tblpPr) that frame properties (w:framePr) give a table in the frame.</summary>
    XElement Floating(XElement frame)
    {
        var position = new XElement(Wml.W + "tblpPr");
        void Set(string to, string? value)
        {
            if (value != null)
                position.SetAttributeValue(Wml.W + to, value);
        }
        Set("horzAnchor", frame.Attr("hAnchor") ?? "text");
        // Without a vertical anchor a frame stands from the margin, before Word 2013 with the text where it has no
        // vertical position either (as Paginator.VerticalAnchor).
        Set("vertAnchor", frame.Attr("vAnchor")
            ?? (context.CompatibilityMode < 15 && (frame.Twips("y") ?? 0) == 0 && frame.Attr("yAlign") is null or "inline" ? "text" : "margin"));
        Set("tblpX", frame.Attr("x"));
        Set("tblpY", frame.Attr("y"));
        Set("tblpXSpec", frame.Attr("xAlign"));
        Set("tblpYSpec", frame.Attr("yAlign"));
        Set("leftFromText", frame.Attr("hSpace"));
        Set("rightFromText", frame.Attr("hSpace"));
        Set("topFromText", frame.Attr("vSpace"));
        Set("bottomFromText", frame.Attr("vSpace"));
        return position;
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
        to.OwnShading = from.OwnShading;
    }

    /// <summary>
    /// The rows of a table, also inside content controls and custom XML; rows deleted as a tracked change are gone, as
    /// Word prints the document without markup (catalog: review.revisions; measured with docx/generated/revisions),
    /// and stay in the markup view.
    /// </summary>
    /// <summary>
    /// Whether a table is gone from the document without markup: every row of it deleted as a tracked change, so
    /// that a paragraph before it whose mark was deleted joins the one after it.
    /// </summary>
    static bool Gone(XElement tbl, bool markup) => !markup && !Rows(tbl, markup).Any();

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

    IEnumerable<XElement> Cells(XElement tr)
    {
        foreach (var e in tr.Elements())
        {
            if (e.Name == Wml.W + "tc")
                yield return e;
            else if (e.Name == Wml.W + "sdt" && e.Child("sdtContent") is { } content)
            {
                // A bound control around cells shows its node's value in its first cell's first paragraph, in the
                // formatting of its first run, as one around paragraphs does (docx/external/libreoffice/
                // extras-ooxmlexport/paragraph-sdt: the document's title from its core properties).
                var bound = e.Child("sdtPr") is { } sdtPr ? Binding.Text(sdtPr) : null;
                foreach (var cell in Cells(content))
                {
                    yield return bound != null ? Bound(cell, bound) : cell;
                    bound = null;
                }
            }
            else if (e.Name == Wml.W + "customXml")
                foreach (var cell in Cells(e)) yield return cell;
        }
    }

    /// <summary>A copy of a cell whose first paragraph holds a text instead of its runs (see Cells).</summary>
    static XElement Bound(XElement tc, string text)
    {
        var copy = new XElement(tc);
        if (copy.Elements(Wml.W + "p").FirstOrDefault() is { } p)
        {
            var rPr = p.Descendants(Wml.W + "r").FirstOrDefault()?.Element(Wml.W + "rPr");
            p.Elements().Where(x => x.Name != Wml.W + "pPr").Remove();
            p.Add(new XElement(Wml.W + "r", rPr != null ? new XElement(rPr) : null,
                new XElement(Wml.W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text)));
        }
        return copy;
    }

    /// <summary>
    /// The rows at the top of a table that horizontal bands skip: with the first-row format on, the first row and the
    /// rows repeating as header rows right after it, which all take the first-row format; with it off, the rows
    /// repeating as header rows from the top (measured with docx/local/hdrstyle, hdrstyle2, hdrstyle3: a row that
    /// is no header row ends them, the cached w:cnfStyle does not count).
    /// </summary>
    static int LeadingRows(TableModel model)
    {
        var rows = model.Rows;
        if (rows.Count == 0 || !model.Format.FirstRow && !rows[0].Format.Header)
            return 0;
        var count = 1;
        while (count < rows.Count && rows[count].Format.Header)
            count++;
        return count;
    }

    /// <summary>
    /// The conditional formats of the cell in row r, position c of its row, in the order they apply; leading is the
    /// number of rows at the top that bands skip (LeadingRows). The last row's format applies to the last row unless
    /// it is a first row, also to the one row of a table, and the last column's alike (docx/local/hdrstyle4, -5).
    /// </summary>
    static IEnumerable<string> Conditions(TableModel model, int r, int c, int leading)
    {
        var f = model.Format;
        var rows = model.Rows.Count;
        var cells = model.Rows[r].Cells.Count;
        var firstRow = f.FirstRow && r < leading;
        var lastRow = f.LastRow && r == rows - 1 && !firstRow;
        var firstColumn = f.FirstColumn && c == 0;
        var lastColumn = f.LastColumn && c == cells - 1 && !firstColumn;
        // Word applies row bands, column bands, the first and last column, then the first and last row, each over the
        // ones before, not in the specification's order (docx/external/libreoffice/extras-ooxmlexport/
        // conditionalstyles-tbllook: body cells in the column bands' colors and size, the first row's size over the
        // first column's, the last row's over the first and the last column's).
        yield return "wholeTable";
        // Bands skip the first and last column and row where those have formats of their own.
        if (!f.NoHorizontalBand && r >= leading && !lastRow)
        {
            var band = (r - leading) / Math.Max(1, f.RowBandSize);
            yield return band % 2 == 0 ? "band1Horz" : "band2Horz";
        }
        if (!f.NoVerticalBand && !(firstColumn && f.FirstColumnStyled) && !lastColumn)
        {
            var band = (c - (f.FirstColumn && f.FirstColumnStyled ? 1 : 0)) / Math.Max(1, f.ColumnBandSize);
            yield return band % 2 == 0 ? "band1Vert" : "band2Vert";
        }
        if (firstColumn) yield return "firstCol";
        if (lastColumn) yield return "lastCol";
        if (firstRow) yield return "firstRow";
        if (lastRow) yield return "lastRow";
        if (firstRow && firstColumn) yield return "nwCell";
        if (firstRow && lastColumn) yield return "neCell";
        if (lastRow && firstColumn) yield return "swCell";
        if (lastRow && lastColumn) yield return "seCell";
    }

    /// <summary>
    /// A conditional format's borders for one cell: its sides where the cell's edge is the region's edge, its inside
    /// lines where the edge lies inside the region (a row region's cells meet at inside vertical lines …). The first
    /// rows are one region: its top line above the first, inside horizontal lines between them, its bottom line
    /// below the last (docx/local/hdrstyle2).
    /// </summary>
    static BorderSet? RegionBorders(BorderSet? set, TableModel model, int r, int c, string condition, int leading)
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
            var offset = r - leading;
            topEdge = offset % size == 0;
            bottomEdge = offset % size == size - 1 || r == rows - 1;
        }
        else if (condition == "firstRow")
        {
            topEdge = r == 0;
            bottomEdge = r == leading - 1;
        }
        else
        {
            topEdge = !columnRegion || r == 0;
            bottomEdge = !columnRegion || r == rows - 1;
        }
        if (condition is "band1Vert" or "band2Vert")
        {
            var size = Math.Max(1, f.ColumnBandSize);
            var offset = c - (f.FirstColumn && f.FirstColumnStyled ? 1 : 0);
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
