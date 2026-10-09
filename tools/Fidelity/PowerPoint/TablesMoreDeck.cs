using System.Runtime.Versioning;
using System.Xml.Linq;

namespace Fidelity.PowerPoint;

/// <summary>
/// Table cell bevels (catalog: table.cells, a:cell3D): every cell of a table beveled (circle, angle, relaxed inset)
/// under the default light, cells beveled with another light rig and material, and single beveled cells.
/// </summary>
[SupportedOSPlatform("windows")]
static class TablesMoreDeck
{
    public static void Build(dynamic deck)
    {
        object presentation = deck;
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
        using var output = zip.CreateEntry(name).Open();
        document.Save(output, SaveOptions.DisableFormatting);
    }
}
