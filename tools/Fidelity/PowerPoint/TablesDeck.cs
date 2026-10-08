using System.Runtime.Versioning;

namespace Fidelity.PowerPoint;

/// <summary>
/// Tables (catalog: table.*): built-in table styles with their banding options, merged cells, cell borders and
/// fills, vertical alignment, margins and rotated text.
/// </summary>
[SupportedOSPlatform("windows")]
static class TablesDeck
{
    const int MsoTrue = PowerPointSession.MsoTrue, MsoFalse = PowerPointSession.MsoFalse;

    /// <summary>Built-in table styles by id.</summary>
    static readonly (string Id, string Name)[] Styles =
    [
        ("{5C22544A-7EE6-4342-B048-85BDC9FD1C3A}", "Medium 2 Accent 1"),
        ("{3B4B98B0-60AC-42C2-AFA5-B58CD77FA1E5}", "Light 1 Accent 1"),
        ("{E8034E78-7F5D-4C2E-B375-FC64B27BC917}", "Dark 1"),
        ("{3C2FFA5D-87B4-456A-9821-1D502468CF0F}", "Themed 1 Accent 1"),
        ("{8EC20E35-A176-4012-BC5E-935CFFF8708E}", "Medium 3"),
        ("{D7AC3CCA-C797-4891-BE02-D94E43425B78}", "Medium 4"),
        ("{69012ECD-51FC-41F1-AA8D-1B2483CD663E}", "Light 2 Accent 1"),
        ("{BC89EF96-8CEA-46FF-86C4-4CE0E7609802}", "Light 3 Accent 1"),
        ("{5940675A-B579-460E-94D1-54222C63F5DA}", "Table Grid"),
        ("{F5AB1C69-6EDB-4FF4-983F-18BD219EF322}", "Medium 2 Accent 3"),
        ("{D113A9D2-9D6B-4929-AA2D-F23B5EE8CBE7}", "Themed 2 Accent 1"),
        ("{6E25E649-3F16-4E02-A733-19D2CDBF48F0}", "Medium 3 Accent 1"),
    ];

    public static void Build(dynamic deck)
    {
        object presentation = deck;
        for (var page = 0; page < 3; page++)
        {
            var slide = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
            for (var i = 0; i < 4; i++)
            {
                var index = page * 4 + i;
                var shape = slide.Shapes.AddTable(4, 3, 30 + (i % 2) * 460, 30 + (i / 2) * 250, 430, 200);
                var table = shape.Table;
                table.ApplyStyle(Styles[index].Id, MsoFalse);
                // Banding options vary with the table: header row, total row, first and last column, banded rows and columns.
                table.FirstRow = MsoTrue;
                table.HorizBanding = index % 3 != 2 ? MsoTrue : MsoFalse;
                table.LastRow = index % 2 == 1 ? MsoTrue : MsoFalse;
                table.FirstCol = index % 4 >= 2 ? MsoTrue : MsoFalse;
                table.LastCol = index % 4 == 3 ? MsoTrue : MsoFalse;
                table.VertBanding = index % 3 == 2 ? MsoTrue : MsoFalse;
                for (var r = 1; r <= 4; r++)
                {
                    for (var c = 1; c <= 3; c++)
                        table.Cell(r, c).Shape.TextFrame.TextRange.Text = r == 1 ? (c == 1 ? Styles[index].Name : $"Head {c}") : $"R{r}C{c}";
                }
            }
        }

        var cells = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        var merged = cells.Shapes.AddTable(5, 4, 30, 30, 520, 300).Table;
        for (var r = 1; r <= 5; r++)
            for (var c = 1; c <= 4; c++)
                merged.Cell(r, c).Shape.TextFrame.TextRange.Text = $"{r}.{c}";
        merged.Cell(2, 1).Merge(merged.Cell(2, 3));
        merged.Cell(3, 4).Merge(merged.Cell(5, 4));
        merged.Cell(4, 1).Merge(merged.Cell(5, 2));
        var top = merged.Cell(1, 2).Borders[1];   // ppBorderTop
        top.ForeColor.RGB = CorpusGenerator.Rgb(200, 0, 0);
        top.Weight = 4f;
        var bottom = merged.Cell(1, 3).Borders[3];   // ppBorderBottom
        bottom.ForeColor.RGB = CorpusGenerator.Rgb(0, 120, 0);
        bottom.Weight = 2f;
        bottom.DashStyle = 4;   // msoLineDash

        var formatted = cells.Shapes.AddTable(3, 3, 580, 30, 350, 300).Table;
        for (var r = 1; r <= 3; r++)
            for (var c = 1; c <= 3; c++)
                formatted.Cell(r, c).Shape.TextFrame.TextRange.Text = $"Cell {r}.{c}";
        formatted.Cell(2, 2).Shape.Fill.ForeColor.RGB = CorpusGenerator.Rgb(255, 230, 150);
        formatted.Cell(2, 2).Shape.TextFrame.VerticalAnchor = 3;   // msoAnchorMiddle
        formatted.Cell(3, 3).Shape.TextFrame.VerticalAnchor = 4;   // msoAnchorBottom
        formatted.Cell(2, 1).Shape.TextFrame.MarginLeft = 20f;
        formatted.Cell(2, 1).Shape.TextFrame.MarginTop = 15f;
        formatted.Cell(3, 1).Shape.TextFrame.Orientation = 2;   // msoTextOrientationUpward
        formatted.Cell(3, 2).Shape.TextFrame.TextRange.ParagraphFormat.Alignment = 3;   // right
        formatted.Rows[2].Height = 120f;
        formatted.Rows[3].Height = 110f;

        // A right-to-left table (its first column on the right, borders mirrored) next to the same table left to
        // right, and cells whose text block is centered (anchorCtr, patched).
        var directions = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        foreach (var (left, rtl) in new[] { (30f, true), (500f, false) })
        {
            var shape = directions.Shapes.AddTable(3, 3, left, 30, 420, 150);
            var table = shape.Table;
            for (var r = 1; r <= 3; r++)
                for (var c = 1; c <= 3; c++)
                    table.Cell(r, c).Shape.TextFrame.TextRange.Text = $"R{r} C{c}";
            var topBorder = table.Cell(2, 1).Borders[1];   // ppBorderTop
            topBorder.Weight = 4f;
            topBorder.ForeColor.RGB = CorpusGenerator.Rgb(220, 0, 0);
            var leftBorder = table.Cell(2, 1).Borders[2];   // ppBorderLeft
            leftBorder.Weight = 4f;
            leftBorder.ForeColor.RGB = CorpusGenerator.Rgb(0, 200, 0);
            if (rtl)
                table.TableDirection = 2;   // ppDirectionRightToLeft
        }
        var centered = directions.Shapes.AddTable(2, 2, 30, 280, 420, 160);
        centered.Name = "anchorCtr-cells";
        for (var r = 1; r <= 2; r++)
        {
            for (var c = 1; c <= 2; c++)
            {
                var range = centered.Table.Cell(r, c).Shape.TextFrame.TextRange;
                range.Text = "Short\rlonger second line";
                range.ParagraphFormat.Alignment = c == 1 ? 1 : 3;   // left, right
            }
        }
    }

    /// <summary>The cells of the first row of "anchorCtr-cells" center their text block (tcPr anchorCtr).</summary>
    public static void Patch(string path) => ShapePatch.Apply(path, new Dictionary<string, Action<System.Xml.Linq.XElement>>
    {
        ["anchorCtr-cells"] = shape =>
        {
            var row = shape.Descendants(ShapePatch.A + "tr").First();
            foreach (var tcPr in row.Elements(ShapePatch.A + "tc").Select(tc => tc.Element(ShapePatch.A + "tcPr")))
                tcPr?.SetAttributeValue("anchorCtr", "1");
        },
    });
}
