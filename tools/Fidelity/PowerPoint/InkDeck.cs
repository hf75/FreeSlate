using System.Globalization;
using System.IO.Compression;
using System.Runtime.Versioning;
using System.Xml.Linq;

namespace Fidelity.PowerPoint;

/// <summary>
/// Ink (catalog: picture.ink): InkML parts with pens, a thin pen and highlighters, at their natural size, moved and
/// enlarged, so that the mapping of the points onto the content part's box shows. PowerPoint's object model cannot
/// create ink from InkML, so the parts are added after saving (see Patch).
/// </summary>
[SupportedOSPlatform("windows")]
static class InkDeck
{
    public static void Build(dynamic deck)
    {
        object presentation = deck;
        var slide = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        var text = slide.Shapes.AddTextbox(1, 40, 400, 400, 50);
        text.TextFrame.TextRange.Text = "Text under a highlighter";
        text.TextFrame.TextRange.Font.Size = 28;
    }

    const string Ink = """<inkml:ink xmlns:inkml="http://www.w3.org/2003/InkML"><inkml:definitions><inkml:context xml:id="ctx0"><inkml:inkSource xml:id="inkSrc0"><inkml:traceFormat><inkml:channel name="X" type="integer" max="32767" units="cm"/><inkml:channel name="Y" type="integer" max="32767" units="cm"/></inkml:traceFormat><inkml:channelProperties><inkml:channelProperty channel="X" name="resolution" value="1000" units="1/cm"/><inkml:channelProperty channel="Y" name="resolution" value="1000" units="1/cm"/></inkml:channelProperties></inkml:inkSource><inkml:timestamp xml:id="ts0" timeString="2024-01-01T12:00:00.000"/></inkml:context>{0}</inkml:definitions>{1}</inkml:ink>""";

    static string Brush(string id, double width, string color, bool highlighter = false) =>
        $"<inkml:brush xml:id=\"{id}\"><inkml:brushProperty name=\"width\" value=\"{width.ToString(CultureInfo.InvariantCulture)}\" units=\"cm\"/>" +
        $"<inkml:brushProperty name=\"height\" value=\"{width.ToString(CultureInfo.InvariantCulture)}\" units=\"cm\"/><inkml:brushProperty name=\"color\" value=\"{color}\"/>" +
        (highlighter ? "<inkml:brushProperty name=\"tip\" value=\"rectangle\"/><inkml:brushProperty name=\"rasterOp\" value=\"maskPen\"/>" : "") + "</inkml:brush>";

    static string Trace(string brush, params (int X, int Y)[] points) =>
        $"<inkml:trace contextRef=\"#ctx0\" brushRef=\"#{brush}\">{string.Join(", ", points.Select(p => $"{p.X} {p.Y}"))}</inkml:trace>";

    /// <summary>A zigzag 4 cm wide and 1.5 cm high (1/1000 cm).</summary>
    static (int, int)[] Zigzag(int x, int y) => [(x, y), (x + 1000, y + 1500), (x + 2000, y), (x + 3000, y + 1500), (x + 4000, y)];

    /// <summary>The ink parts: brushes, traces and the content part's box (1/1000 cm).</summary>
    static readonly (string Brushes, string Traces, (int X, int Y, int W, int H) Box)[] Parts =
    [
        (Brush("b", 0.2, "#E71224"), Trace("b", Zigzag(2000, 2000)), (2000, 2000, 4000, 1500)),
        (Brush("b", 0.2, "#E71224"), Trace("b", Zigzag(9000, 2000)), (8900, 1900, 4200, 1700)),
        (Brush("b", 0.2, "#E71224"), Trace("b", Zigzag(16000, 2000)), (16000, 2000, 8000, 3000)),
        (Brush("b", 0.2, "#0070C0"), Trace("b", Zigzag(2000, 6000)), (1900, 7900, 4200, 1700)),
        (Brush("b", 0.2, "#0070C0") + Brush("c", 0.6, "#00B050"), Trace("b", Zigzag(9000, 6000)) + Trace("c", (9000, 8000), (13000, 8000)), (9000, 6000, 4000, 2000)),
        (Brush("b", 0.05, "#0070C0"), Trace("b", Zigzag(16000, 6000)), (15975, 5975, 4050, 1550)),
        (Brush("b", 0.6, "#FFFF00", highlighter: true), Trace("b", (2000, 14200), (12000, 14200)), (1700, 13900, 10600, 600)),
        (Brush("b", 0.6, "#FFFF00", highlighter: true) + Brush("c", 0.05, "#0070C0"),
            Trace("b", (16000, 12000), (24000, 12000)) + Trace("c", (16000, 13000), (18000, 14000), (20000, 13000)), (15700, 11700, 8600, 2600)),
    ];

    public static void Patch(string path)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
        void Update(string name, Func<string, string> change)
        {
            var entry = zip.GetEntry(name)!;
            string text;
            using (var reader = new StreamReader(entry.Open()))
                text = reader.ReadToEnd();
            entry.Delete();
            using var writer = new StreamWriter(zip.CreateEntry(name).Open());
            writer.Write(change(text));
        }
        static long Emu(int cm1000) => cm1000 * 360L;
        Update("[Content_Types].xml", t => t.Replace("</Types>",
            string.Concat(Parts.Select((_, i) => $"<Override PartName=\"/ppt/ink/ink{i + 1}.xml\" ContentType=\"application/inkml+xml\"/>")) + "</Types>"));
        Update("ppt/slides/_rels/slide1.xml.rels", t => t.Replace("</Relationships>",
            string.Concat(Parts.Select((_, i) => $"<Relationship Id=\"rIdInk{i + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/customXml\" Target=\"../ink/ink{i + 1}.xml\"/>")) + "</Relationships>"));
        Update("ppt/slides/slide1.xml", t => t.Replace("</p:spTree>", string.Concat(Parts.Select((p, i) =>
            "<mc:AlternateContent xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\"><mc:Choice xmlns:p14=\"http://schemas.microsoft.com/office/powerpoint/2010/main\" Requires=\"p14\">" +
            $"<p:contentPart p14:bwMode=\"auto\" r:id=\"rIdInk{i + 1}\"><p14:nvContentPartPr><p14:cNvPr id=\"{200 + i}\" name=\"Ink {i + 1}\"/><p14:cNvContentPartPr/><p14:nvPr/></p14:nvContentPartPr>" +
            $"<p14:xfrm><a:off x=\"{Emu(p.Box.X)}\" y=\"{Emu(p.Box.Y)}\"/><a:ext cx=\"{Emu(p.Box.W)}\" cy=\"{Emu(p.Box.H)}\"/></p14:xfrm></p:contentPart></mc:Choice><mc:Fallback/></mc:AlternateContent>")) + "</p:spTree>"));
        for (var i = 0; i < Parts.Length; i++)
        {
            using var writer = new StreamWriter(zip.CreateEntry($"ppt/ink/ink{i + 1}.xml").Open());
            writer.Write(string.Format(CultureInfo.InvariantCulture, Ink, Parts[i].Brushes, Parts[i].Traces));
        }
    }
}
