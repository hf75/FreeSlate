using System.Runtime.Versioning;
using System.Xml.Linq;

namespace Fidelity.PowerPoint;

/// <summary>
/// Table cell bevels (catalog: table.cells, a:cell3D): every cell of a table beveled (circle, angle, relaxed inset)
/// under the default light, cells beveled with another light rig and material, and single beveled cells; tables in
/// built-in styles the presentation does not define (catalog: table.styles).
/// </summary>
[SupportedOSPlatform("windows")]
static class TablesMoreDeck
{
    public static void Build(dynamic deck)
    {
        object presentation = deck;
        Bevels(presentation);

        // Built-in styles the presentation names without defining them (the patch empties ppt/tableStyles.xml).
        var styles = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        string[] ids =
        [
            "{5C22544A-7EE6-4342-B048-85BDC9FD1C3A}",   // Medium Style 2 - Accent 1
            "{0E3FDE45-AF77-4B5C-9715-49D594BDF05E}",   // Light Style 1 - Accent 2
            "{E8034E78-7F5D-4C2E-B375-FC64B27BC917}",   // Dark Style 1
            "{775DCB02-9BB8-47FD-8907-85C794F793BA}",   // Themed Style 1 - Accent 4
            "{EB9631B5-78F2-41C9-869B-9F39066F8104}",   // Medium Style 3 - Accent 4
            "{616DA210-FB5B-4158-B5E0-FEB733F419BA}",   // Light Style 3
        ];
        for (var i = 0; i < ids.Length; i++)
        {
            var table = styles.Shapes.AddTable(3, 3, 30f + i % 3 * 310f, 30f + i / 3 * 260f, 290f, 200f).Table;
            table.ApplyStyle(ids[i], false);
            for (var r = 1; r <= 3; r++)
            {
                for (var c = 1; c <= 3; c++)
                    table.Cell(r, c).Shape.TextFrame.TextRange.Text = $"R{r}C{c}";
            }
        }
    }

    static void Bevels(object presentation)
    {
        var slide = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        foreach (var (left, top) in new[] { (30f, 30f), (500f, 30f), (30f, 290f), (500f, 290f) })
        {
            var table = slide.Shapes.AddTable(3, 3, left, top, 430f, 220f).Table;
            for (var r = 1; r <= 3; r++)
            {
                for (var c = 1; c <= 3; c++)
                    table.Cell(r, c).Shape.TextFrame.TextRange.Text = $"R{r}C{c}";
            }
        }
    }

    /// <summary>Bevels every cell of the first three tables (circle, angle, relaxed inset) and two cells of the last.</summary>
    public static void Patch(string path)
    {
        using var zip = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Update);
        XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
        const string name = "ppt/slides/slide1.xml";
        XDocument document;
        using (var stream = zip.GetEntry(name)!.Open())
            document = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        var tables = document.Descendants(a + "tbl").ToList();
        XElement Bevel(string preset, string rig = "threePt", string direction = "t", string? material = null)
        {
            var cell3D = new XElement(a + "cell3D",
                new XElement(a + "bevel", new XAttribute("w", 76200), new XAttribute("h", 76200), new XAttribute("prst", preset)),
                new XElement(a + "lightRig", new XAttribute("rig", rig), new XAttribute("dir", direction)));
            if (material != null)
                cell3D.SetAttributeValue("prstMaterial", material);
            return cell3D;
        }
        void Apply(XElement cell, XElement cell3D)
        {
            var tcPr = cell.Element(a + "tcPr");
            if (tcPr == null)
            {
                tcPr = new XElement(a + "tcPr");
                cell.Add(tcPr);
            }
            tcPr.Element(a + "cell3D")?.Remove();
            // cell3D follows the cell's lines and comes before its fill.
            var after = tcPr.Elements().LastOrDefault(e => e.Name.LocalName.StartsWith("ln", StringComparison.Ordinal));
            if (after != null)
                after.AddAfterSelf(cell3D);
            else
                tcPr.AddFirst(cell3D);
        }
        var presets = new[] { "circle", "angle", "relaxedInset" };
        for (var t = 0; t < 3; t++)
        {
            foreach (var cell in tables[t].Descendants(a + "tc"))
                Apply(cell, Bevel(presets[t]));
        }
        var last = tables[3].Descendants(a + "tc").ToList();
        Apply(last[0], Bevel("circle", "balanced", "tl", "matte"));
        Apply(last[4], Bevel("softRound", "harsh", "t", "metal"));
        zip.GetEntry(name)!.Delete();
        using (var output = zip.CreateEntry(name).Open())
            document.Save(output, SaveOptions.DisableFormatting);

        // No style definitions in the presentation: every table names a built-in style PowerPoint knows by itself.
        const string styles = "ppt/tableStyles.xml";
        zip.GetEntry(styles)!.Delete();
        using var stylesOutput = zip.CreateEntry(styles).Open();
        new XDocument(new XElement(a + "tblStyleLst", new XAttribute(XNamespace.Xmlns + "a", a.NamespaceName),
            new XAttribute("def", "{5C22544A-7EE6-4342-B048-85BDC9FD1C3A}"))).Save(stylesOutput, SaveOptions.DisableFormatting);
    }
}
