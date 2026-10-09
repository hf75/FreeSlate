using System.Runtime.Versioning;
using System.Xml.Linq;

namespace Fidelity.PowerPoint;

/// <summary>
/// Locked canvases (catalog: shape.group, text.body): a graphic frame holding a group of DrawingML shapes (a:sp with
/// its text in a:txSp, a:cxnSp, a nested a:grpSp), as documents from older Office versions bring them along.
/// </summary>
[SupportedOSPlatform("windows")]
static class ObjectsMoreDeck
{
    public static void Build(dynamic deck)
    {
        object presentation = deck;
        var slide = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        // A marker shape; the canvas is added by the patch.
        var marker = slide.Shapes.AddShape(1, 30f, 470f, 120f, 40f);
        marker.TextFrame.TextRange.Text = "Marker";
    }

    /// <summary>Adds two locked canvases to the first slide: one at the canvas's own size, one scaled into a smaller frame.</summary>
    public static void Patch(string path)
    {
        using var zip = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Update);
        XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
        XNamespace p = "http://schemas.openxmlformats.org/presentationml/2006/main";
        XNamespace lc = "http://schemas.openxmlformats.org/drawingml/2006/lockedCanvas";
        const string name = "ppt/slides/slide1.xml";
        XDocument document;
        using (var stream = zip.GetEntry(name)!.Open())
            document = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        var tree = document.Descendants(p + "spTree").First();
        const long Pt = 12700;
        static XElement Xfrm(XNamespace ns, long x, long y, long w, long h, bool group = false)
        {
            var xfrm = new XElement(ns + "xfrm",
                new XElement(ns.NamespaceName == "http://schemas.openxmlformats.org/drawingml/2006/main" ? ns + "off" : XNamespace.Get("http://schemas.openxmlformats.org/drawingml/2006/main") + "off", new XAttribute("x", x), new XAttribute("y", y)),
                new XElement(XNamespace.Get("http://schemas.openxmlformats.org/drawingml/2006/main") + "ext", new XAttribute("cx", w), new XAttribute("cy", h)));
            if (group)
                xfrm.Add(new XElement(XNamespace.Get("http://schemas.openxmlformats.org/drawingml/2006/main") + "chOff", new XAttribute("x", x), new XAttribute("y", y)),
                    new XElement(XNamespace.Get("http://schemas.openxmlformats.org/drawingml/2006/main") + "chExt", new XAttribute("cx", w), new XAttribute("cy", h)));
            return xfrm;
        }
        XElement Shape(int id, string geometry, long x, long y, long w, long h, string fill, string? text)
        {
            var sp = new XElement(a + "sp",
                new XElement(a + "nvSpPr", new XElement(a + "cNvPr", new XAttribute("id", id), new XAttribute("name", $"Shape {id}")), new XElement(a + "cNvSpPr")),
                new XElement(a + "spPr", Xfrm(a, x, y, w, h), new XElement(a + "prstGeom", new XAttribute("prst", geometry), new XElement(a + "avLst")),
                    new XElement(a + "solidFill", new XElement(a + "srgbClr", new XAttribute("val", fill))),
                    new XElement(a + "ln", new XAttribute("w", 19050), new XElement(a + "solidFill", new XElement(a + "srgbClr", new XAttribute("val", "1F3864"))))));
            if (text != null)
                sp.Add(new XElement(a + "txSp",
                    new XElement(a + "txBody", new XElement(a + "bodyPr", new XAttribute("anchor", "ctr")), new XElement(a + "lstStyle"),
                        new XElement(a + "p", new XElement(a + "pPr", new XAttribute("algn", "ctr")),
                            new XElement(a + "r", new XElement(a + "rPr", new XAttribute("lang", "en-US"), new XAttribute("sz", 2000),
                                new XElement(a + "solidFill", new XElement(a + "srgbClr", new XAttribute("val", "000000")))), new XElement(a + "t", text)))),
                    new XElement(a + "useSpRect")));
            return sp;
        }
        XElement Canvas(int id, long x, long y, long w, long h)
        {
            // The canvas's own space is 400 × 250 pt; the frame places and scales it.
            var (cw, ch) = (400 * Pt, 250 * Pt);
            var canvas = new XElement(lc + "lockedCanvas",
                new XElement(a + "nvGrpSpPr", new XElement(a + "cNvPr", new XAttribute("id", 0), new XAttribute("name", "")), new XElement(a + "cNvGrpSpPr")),
                new XElement(a + "grpSpPr", Xfrm(a, 0, 0, cw, ch, group: true)),
                Shape(id + 1, "rect", 10 * Pt, 10 * Pt, 180 * Pt, 90 * Pt, "BDD7EE", "Canvas text"),
                Shape(id + 2, "ellipse", 220 * Pt, 20 * Pt, 160 * Pt, 100 * Pt, "F8CBAD", "Ellipse"),
                new XElement(a + "cxnSp",
                    new XElement(a + "nvCxnSpPr", new XElement(a + "cNvPr", new XAttribute("id", id + 3), new XAttribute("name", "Line")), new XElement(a + "cNvCxnSpPr")),
                    new XElement(a + "spPr", Xfrm(a, 100 * Pt, 140 * Pt, 200 * Pt, 80 * Pt), new XElement(a + "prstGeom", new XAttribute("prst", "line"), new XElement(a + "avLst")),
                        new XElement(a + "ln", new XAttribute("w", 38100), new XElement(a + "solidFill", new XElement(a + "srgbClr", new XAttribute("val", "C00000")))))),
                new XElement(a + "grpSp",
                    new XElement(a + "nvGrpSpPr", new XElement(a + "cNvPr", new XAttribute("id", id + 4), new XAttribute("name", "Inner group")), new XElement(a + "cNvGrpSpPr")),
                    new XElement(a + "grpSpPr", Xfrm(a, 20 * Pt, 160 * Pt, 120 * Pt, 80 * Pt, group: true)),
                    Shape(id + 5, "triangle", 20 * Pt, 160 * Pt, 60 * Pt, 80 * Pt, "A9D18E", null),
                    Shape(id + 6, "diamond", 80 * Pt, 160 * Pt, 60 * Pt, 80 * Pt, "FFE699", null)));
            return new XElement(p + "graphicFrame",
                new XElement(p + "nvGraphicFramePr", new XElement(p + "cNvPr", new XAttribute("id", id), new XAttribute("name", $"Canvas {id}")),
                    new XElement(p + "cNvGraphicFramePr"), new XElement(p + "nvPr")),
                new XElement(p + "xfrm", new XElement(a + "off", new XAttribute("x", x), new XAttribute("y", y)), new XElement(a + "ext", new XAttribute("cx", w), new XAttribute("cy", h))),
                new XElement(a + "graphic", new XElement(a + "graphicData", new XAttribute("uri", "http://schemas.openxmlformats.org/drawingml/2006/lockedCanvas"), canvas)));
        }
        tree.Add(Canvas(100, 30 * Pt, 30 * Pt, 400 * Pt, 250 * Pt));
        tree.Add(Canvas(200, 500 * Pt, 60 * Pt, 300 * Pt, 160 * Pt));
        zip.GetEntry(name)!.Delete();
        using var output = zip.CreateEntry(name).Open();
        document.Save(output, SaveOptions.DisableFormatting);
    }
}
