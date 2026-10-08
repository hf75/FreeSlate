using System.IO.Compression;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace Fidelity.PowerPoint;

/// <summary>
/// Legacy VML pictures (catalog: legacy.vml): OLE objects the way PowerPoint 2007 saved them, the p:oleObj naming a
/// VML shape (spid) whose v:imagedata holds the picture, without the DrawingML picture later versions add. The patch
/// turns the Excel object PowerPoint made into that form (its EMF in the VML drawing) and adds a second one whose
/// VML picture is a PNG, cropped on all sides.
/// </summary>
[SupportedOSPlatform("windows")]
static partial class LegacyVmlDeck
{
    public static void Build(dynamic deck)
    {
        object presentation = deck;
        var slide = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        var title = ((dynamic)slide).Shapes.AddTextbox(1, 40, 20, 880, 50);
        title.TextFrame.TextRange.Text = "OLE objects with VML pictures";
        title.TextFrame.TextRange.Font.Size = 28;
        var sheet = ((dynamic)slide).Shapes.AddOLEObject(40, 100, 360, 240, "Excel.Sheet.12");
        sheet.Name = "sheet";
    }

    public static void Patch(string path)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
        string Read(string name)
        {
            using var reader = new StreamReader(zip.GetEntry(name)!.Open());
            return reader.ReadToEnd();
        }
        void Write(string name, string text)
        {
            zip.GetEntry(name)?.Delete();
            using var writer = new StreamWriter(zip.CreateEntry(name).Open());
            writer.Write(text);
        }

        const string slidePart = "ppt/slides/slide1.xml", relsPart = "ppt/slides/_rels/slide1.xml.rels";
        var slide = Read(slidePart);
        var rels = Read(relsPart);
        var alternate = AlternateContent().Match(slide);
        var frame = GraphicFrame().Match(slide);
        var pictureId = Regex.Match(alternate.Value, "<a:blip r:embed=\"(rId\\d+)\"").Groups[1].Value;
        var emf = Regex.Match(rels, $"Id=\"{pictureId}\"[^>]*Target=\"([^\"]+)\"").Groups[1].Value;
        var attributes = Regex.Match(alternate.Value, "<p:oleObj ([^>]*)>").Groups[1].Value;

        // The first object: the 2007 form with PowerPoint's picture in the VML drawing.
        var first = frame.Value.Replace(alternate.Value, $"<p:oleObj spid=\"_x0000_s1026\" {attributes}><p:embed/></p:oleObj>");
        // The second: a copy to the right with a PNG picture, cropped.
        var second = first.Replace("_x0000_s1026", "_x0000_s1027");
        second = Regex.Replace(second, "<p:cNvPr id=\"[0-9]+\" name=\"sheet\"", "<p:cNvPr id=\"30\" name=\"cropped\"");
        second = Regex.Replace(second, "<p:xfrm><a:off x=\"[0-9]+\"", "<p:xfrm><a:off x=\"6096000\"");
        slide = slide.Replace(frame.Value, first + second);
        Write(slidePart, slide);
        Write(relsPart, rels.Replace("</Relationships>",
            "<Relationship Id=\"rId90\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/vmlDrawing\" Target=\"../drawings/vmlDrawing1.vml\"/></Relationships>"));

        const string shapeType = "<v:shapetype id=\"_x0000_t75\" coordsize=\"21600,21600\" o:spt=\"75\" o:preferrelative=\"t\" path=\"m@4@5l@4@11@9@11@9@5xe\" filled=\"f\" stroked=\"f\">" +
                                 "<v:stroke joinstyle=\"miter\"/><v:path o:extrusionok=\"f\" gradientshapeok=\"t\" o:connecttype=\"rect\"/><o:lock v:ext=\"edit\" aspectratio=\"t\"/></v:shapetype>";
        Write("ppt/drawings/vmlDrawing1.vml",
            "<xml xmlns:v=\"urn:schemas-microsoft-com:vml\" xmlns:o=\"urn:schemas-microsoft-com:office:office\" xmlns:p=\"urn:schemas-microsoft-com:office:powerpoint\">" +
            "<o:shapelayout v:ext=\"edit\"><o:idmap v:ext=\"edit\" data=\"1\"/></o:shapelayout>" + shapeType +
            "<v:shape id=\"_x0000_s1026\" type=\"#_x0000_t75\" style=\"position:absolute;left:40pt;top:100pt;width:360pt;height:240pt\"><v:imagedata o:relid=\"rId1\" o:title=\"\"/></v:shape>" +
            "<v:shape id=\"_x0000_s1027\" type=\"#_x0000_t75\" style=\"position:absolute;left:480pt;top:100pt;width:360pt;height:240pt\">" +
            "<v:imagedata o:relid=\"rId2\" o:title=\"\" croptop=\"6554f\" cropbottom=\"13107f\" cropleft=\"0.1\" cropright=\"0.05\"/></v:shape></xml>");
        Write("ppt/drawings/_rels/vmlDrawing1.vml.rels",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            $"<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image\" Target=\"{emf}\"/>" +
            "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image\" Target=\"../media/vml-picture.png\"/></Relationships>");
        using (var stream = zip.CreateEntry("ppt/media/vml-picture.png").Open())
            Picture().CopyTo(stream);
        var types = Read("[Content_Types].xml");
        if (!types.Contains("Extension=\"vml\""))
            types = types.Replace("<Default Extension=\"xml\"", "<Default Extension=\"vml\" ContentType=\"application/vnd.openxmlformats-officedocument.vmlDrawing\"/><Default Extension=\"xml\"");
        if (!types.Contains("Extension=\"png\""))
            types = types.Replace("<Default Extension=\"xml\"", "<Default Extension=\"png\" ContentType=\"image/png\"/><Default Extension=\"xml\"");
        Write("[Content_Types].xml", types);
    }

    /// <summary>A test picture: a grid of colored quarters with a circle, so that crops show.</summary>
    static Stream Picture()
    {
        using var bitmap = new SKBitmap(400, 300);
        using (var canvas = new SKCanvas(bitmap))
        {
            using var paint = new SKPaint { IsAntialias = true };
            (SKColor Color, SKRect Rect)[] quarters =
            [
                (new SKColor(0xC0, 0x00, 0x00), new SKRect(0, 0, 200, 150)), (new SKColor(0x00, 0x70, 0xC0), new SKRect(200, 0, 400, 150)),
                (new SKColor(0x00, 0xB0, 0x50), new SKRect(0, 150, 200, 300)), (new SKColor(0xFF, 0xC0, 0x00), new SKRect(200, 150, 400, 300)),
            ];
            foreach (var (color, rect) in quarters)
            {
                paint.Color = color;
                canvas.DrawRect(rect, paint);
            }
            paint.Color = SKColors.White;
            canvas.DrawCircle(200, 150, 90, paint);
        }
        var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.AsStream();
    }

    [GeneratedRegex("<mc:AlternateContent.*?</mc:AlternateContent>", RegexOptions.Singleline)]
    private static partial Regex AlternateContent();

    [GeneratedRegex("<p:graphicFrame>.*?</p:graphicFrame>", RegexOptions.Singleline)]
    private static partial Regex GraphicFrame();
}
