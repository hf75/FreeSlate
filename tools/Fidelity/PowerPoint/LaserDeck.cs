using System.Globalization;
using System.IO.Compression;
using System.Runtime.Versioning;
using System.Xml.Linq;

namespace Fidelity.PowerPoint;

/// <summary>
/// A recorded laser pointer (catalog: animation.laser): two traces on a slide, one along a line with times in
/// milliseconds, one around a circle with times in seconds, and a green laser color in the show settings with
/// narrations on. PowerPoint only replays them in the slide show, so the slide must look as without them; the
/// player's replay is checked in the browser. The object model cannot record traces, so they are added after saving.
/// </summary>
[SupportedOSPlatform("windows")]
static class LaserDeck
{
    public static void Build(dynamic deck)
    {
        object presentation = deck;
        var slide = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        var title = slide.Shapes.AddTextbox(1, 40, 30, 880, 60);
        title.TextFrame.TextRange.Text = "Recorded laser pointer";
        title.TextFrame.TextRange.Font.Size = 36;
        slide.Shapes.AddShape(1, 150, 180, 300, 160).Fill.ForeColor.RGB = 0xC07000;
        slide.Shapes.AddShape(9, 560, 160, 260, 260).Fill.ForeColor.RGB = 0x4F9A2E;
    }

    static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";
    static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    static readonly XNamespace P14 = "http://schemas.microsoft.com/office/powerpoint/2010/main";

    static long Emu(double points) => (long)Math.Round(points * 12700);

    public static void Patch(string path)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
        void Change(string name, Action<XDocument> change)
        {
            var entry = zip.GetEntry(name)!;
            XDocument document;
            using (var stream = entry.Open())
                document = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
            change(document);
            entry.Delete();
            using var output = zip.CreateEntry(name).Open();
            document.Save(output, SaveOptions.DisableFormatting);
        }

        // Trace 1: 0.5 s to 2.5 s from (200, 260) pt to (680, 300) pt; trace 2: 3 s to 4 s once around (690, 290) pt.
        var line = Enumerable.Range(0, 5).Select(i => (T: (500 + i * 500).ToString(CultureInfo.InvariantCulture), X: 200 + i * 120.0, Y: 260 + i * 10.0));
        var circle = Enumerable.Range(0, 9).Select(i => (T: (3 + i / 8.0).ToString("0.###", CultureInfo.InvariantCulture) + "s",
            X: 690 + 80 * Math.Cos(i * Math.PI / 4), Y: 290 + 80 * Math.Sin(i * Math.PI / 4)));
        XElement Trace(IEnumerable<(string T, double X, double Y)> points) =>
            new(P14 + "tracePtLst", points.Select(p => new XElement(P14 + "tracePt",
                new XAttribute("t", p.T), new XAttribute("x", Emu(p.X)), new XAttribute("y", Emu(p.Y)))));
        Change("ppt/slides/slide1.xml", slide =>
        {
            var extensions = slide.Root!.Element(P + "extLst") ?? new XElement(P + "extLst");
            if (extensions.Parent == null)
                slide.Root.Add(extensions);
            extensions.Add(new XElement(P + "ext", new XAttribute("uri", "{3A86A75C-4F4B-4683-9AE1-C65F6400EC91}"),
                new XElement(P14 + "laserTraceLst", new XAttribute(XNamespace.Xmlns + "p14", P14.NamespaceName), Trace(line), Trace(circle))));
        });
        Change("ppt/presProps.xml", properties =>
            properties.Root!.AddFirst(new XElement(P + "showPr", new XAttribute("showNarration", "1"),
                new XElement(P + "present"), new XElement(P + "sldAll"),
                new XElement(P + "penClr", new XElement(A + "prstClr", new XAttribute("val", "red"))),
                new XElement(P + "extLst", new XElement(P + "ext", new XAttribute("uri", "{EC167BDD-8182-4AB7-AECC-EB403E3ABB37}"),
                    new XElement(P14 + "laserClr", new XAttribute(XNamespace.Xmlns + "p14", P14.NamespaceName),
                        new XElement(A + "srgbClr", new XAttribute("val", "00B050"))))))));
    }
}
