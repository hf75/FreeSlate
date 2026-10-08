using System.IO.Compression;
using System.Runtime.Versioning;
using System.Xml.Linq;

namespace Fidelity.PowerPoint;

/// <summary>
/// Review comments (catalog: review.comments, review.modernComments): modern comments with a reply on the first
/// slide (through the object model), classic comments with a threaded reply on the second and, on the third, a
/// modern comment anchored to a shape with a reply, reactions and a task (both added after saving, see Patch).
/// PowerPoint never draws comments on slides, so the slides must look as without them; the document shows them on
/// request.
/// </summary>
[SupportedOSPlatform("windows")]
static class CommentsDeck
{
    public static void Build(dynamic deck)
    {
        object presentation = deck;
        var slide = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        var box = slide.Shapes.AddShape(1, 200, 150, 300, 120);
        box.TextFrame.TextRange.Text = "Modern comments";
        var comment = slide.Comments.Add2(100, 50, "Alex Example", "AE", "A slide comment", "None", "Alex Example");
        comment.Replies.Add2(0, 0, "Sam Sample", "SS", "A reply", "None", "Sam Sample");
        slide.Comments.Add2(700, 400, "Sam Sample", "SS", "Second comment", "None", "Sam Sample");

        var classic = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        classic.Shapes.AddTextbox(1, 40, 40, 600, 50).TextFrame.TextRange.Text = "Classic comments";

        var anchored = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        var target = anchored.Shapes.AddShape(5, 300, 200, 300, 150);
        target.Name = "Anchor";
        target.TextFrame.TextRange.Text = "Anchored comment";
    }

    static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";
    static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";
    static readonly XNamespace Ct = "http://schemas.openxmlformats.org/package/2006/content-types";
    static readonly XNamespace P188 = "http://schemas.microsoft.com/office/powerpoint/2018/8/main";
    static readonly XNamespace A16 = "http://schemas.microsoft.com/office/drawing/2014/main";

    const string ClassicAuthors = """
        <p:cmAuthorLst xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main"><p:cmAuthor id="1" name="Chris Classic" initials="CC" lastIdx="2" clrIdx="0"/><p:cmAuthor id="2" name="Dana Doe" initials="DD" lastIdx="1" clrIdx="1"/></p:cmAuthorLst>
        """;

    // Positions in 1/576 inch: (160, 200) px and (800, 400) px.
    const string ClassicComments = """
        <p:cmLst xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main"><p:cm authorId="1" dt="2016-05-04T10:20:30.000" idx="1"><p:pos x="960" y="1200"/><p:text>A classic comment</p:text></p:cm><p:cm authorId="2" dt="2016-05-04T11:00:00.000" idx="1"><p:pos x="960" y="1200"/><p:text>A classic reply</p:text><p:extLst><p:ext uri="{C676402C-5697-4E1C-873F-D02D1690AC5C}"><p15:threadingInfo xmlns:p15="http://schemas.microsoft.com/office/powerpoint/2012/main" timeZoneBias="-120"><p15:parentCm authorId="1" idx="1"/></p15:threadingInfo></p:ext></p:extLst></p:cm><p:cm authorId="1" dt="2016-05-04T12:00:00.000" idx="2"><p:pos x="4800" y="2400"/><p:text>Second classic comment
        on two lines</p:text></p:cm></p:cmLst>
        """;

    public static void Patch(string path)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
        XDocument Load(string name)
        {
            using var stream = zip.GetEntry(name)!.Open();
            return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        }
        void Save(string name, XDocument document)
        {
            zip.GetEntry(name)?.Delete();
            using var stream = zip.CreateEntry(name).Open();
            document.Save(stream, SaveOptions.DisableFormatting);
        }
        void Write(string name, string text)
        {
            using var writer = new StreamWriter(zip.CreateEntry(name).Open());
            writer.Write(text);
        }
        void Relate(string rels, string id, string type, string target)
        {
            var document = Load(rels);
            document.Root!.Add(new XElement(Rel + "Relationship", new XAttribute("Id", id), new XAttribute("Type", type), new XAttribute("Target", target)));
            Save(rels, document);
        }

        var types = Load("[Content_Types].xml");
        foreach (var (part, type) in new[]
                 {
                     ("/ppt/commentAuthors.xml", "application/vnd.openxmlformats-officedocument.presentationml.commentAuthors+xml"),
                     ("/ppt/comments/comment1.xml", "application/vnd.openxmlformats-officedocument.presentationml.comments+xml"),
                     ("/ppt/comments/modernComment_3.xml", "application/vnd.ms-powerpoint.comments+xml"),
                 })
            types.Root!.Add(new XElement(Ct + "Override", new XAttribute("PartName", part), new XAttribute("ContentType", type)));
        Save("[Content_Types].xml", types);

        // Classic comments on slide 2.
        Write("ppt/commentAuthors.xml", ClassicAuthors);
        Write("ppt/comments/comment1.xml", ClassicComments.Replace("\r\n", "\n"));
        Relate("ppt/_rels/presentation.xml.rels", "rIdAuthors", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/commentAuthors", "commentAuthors.xml");
        Relate("ppt/slides/_rels/slide2.xml.rels", "rIdComments", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/comments", "../comments/comment1.xml");

        // A modern comment on slide 3 anchored to the shape "Anchor", 20 pt right and 10 pt below its top left corner.
        var authors = Load("ppt/authors.xml").Root!.Elements(P188 + "author").ToDictionary(a => (string)a.Attribute("name")!, a => (string)a.Attribute("id")!);
        var (alex, sam) = (authors["Alex Example"], authors["Sam Sample"]);
        var slide = Load("ppt/slides/slide3.xml");
        var anchor = slide.Descendants(P + "cNvPr").First(c => (string?)c.Attribute("name") == "Anchor");
        var creationId = (string?)anchor.Descendants(A16 + "creationId").FirstOrDefault()?.Attribute("id") ?? "";
        var presentation = Load("ppt/presentation.xml");
        var sldId = (string)presentation.Descendants(P + "sldId").ElementAt(2).Attribute("id")!;
        Write("ppt/comments/modernComment_3.xml",
            "<p188:cmLst xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" xmlns:p188=\"http://schemas.microsoft.com/office/powerpoint/2018/8/main\">" +
            $"<p188:cm id=\"{{6D1E8C0A-1F00-4C3B-9E21-0A1B2C3D4E5F}}\" authorId=\"{alex}\" created=\"2026-10-01T09:00:00.000\" assignedTo=\"{sam}\" dueDate=\"2026-10-15T00:00:00.000\" complete=\"50%\" title=\"Check the figures\">" +
            "<ac:deMkLst xmlns:ac=\"http://schemas.microsoft.com/office/drawing/2013/main/command\"><pc:docMk xmlns:pc=\"http://schemas.microsoft.com/office/powerpoint/2013/main/command\"/>" +
            $"<pc:sldMk xmlns:pc=\"http://schemas.microsoft.com/office/powerpoint/2013/main/command\" cId=\"0\" sldId=\"{sldId}\"/><ac:spMk id=\"{(string)anchor.Attribute("id")!}\" creationId=\"{creationId}\"/></ac:deMkLst>" +
            "<p188:pos x=\"254000\" y=\"127000\"/>" +
            $"<p188:replyLst><p188:reply id=\"{{6D1E8C0A-1F00-4C3B-9E21-0A1B2C3D4E60}}\" authorId=\"{sam}\" created=\"2026-10-02T14:30:00.000\" status=\"resolved\"><p188:txBody><a:bodyPr/><a:lstStyle/><a:p><a:r><a:rPr lang=\"en-US\"/><a:t>Done</a:t></a:r></a:p></p188:txBody></p188:reply></p188:replyLst>" +
            "<p188:txBody><a:bodyPr/><a:lstStyle/><a:p><a:r><a:rPr lang=\"en-US\" b=\"1\"/><a:t>Please</a:t></a:r><a:r><a:rPr lang=\"en-US\"/><a:t> check the figures.</a:t></a:r></a:p></p188:txBody>" +
            "<p188:extLst><p:ext uri=\"{B1A5D0F2-7C4E-4E8B-9D3A-2F6C1E0B7A41}\"><p223:reactions xmlns:p223=\"http://schemas.microsoft.com/office/powerpoint/2022/03/main\">" +
            $"<p223:rxn type=\"&#x1F44D;\"><p223:instance time=\"2026-10-01T10:00:00.000\" authorId=\"{sam}\"/><p223:instance time=\"2026-10-01T11:00:00.000\" authorId=\"{alex}\"/></p223:rxn></p223:reactions></p:ext>" +
            "<p:ext uri=\"{C2B6E1A3-8D5F-4F9C-AE4B-3A7D2F1C8B52}\"><p228:taskDetails xmlns:p228=\"http://schemas.microsoft.com/office/powerpoint/2022/08/main\"><p228:history>" +
            $"<p228:event time=\"2026-10-01T09:00:00.000\" id=\"{{0B2E4F6A-1C3D-4E5F-8A9B-0C1D2E3F4A51}}\"><p228:atrbtn authorId=\"{alex}\"/><p228:anchr><p228:comment id=\"{{6D1E8C0A-1F00-4C3B-9E21-0A1B2C3D4E5F}}\"/></p228:anchr><p228:asgn authorId=\"{sam}\"/></p228:event>" +
            "</p228:history></p228:taskDetails></p:ext></p188:extLst></p188:cm></p188:cmLst>");
        Relate("ppt/slides/_rels/slide3.xml.rels", "rIdModern", "http://schemas.microsoft.com/office/2018/10/relationships/comments", "../comments/modernComment_3.xml");
        var extensions = slide.Root!.Element(P + "extLst") ?? new XElement(P + "extLst");
        if (extensions.Parent == null)
            slide.Root.Add(extensions);
        extensions.Add(new XElement(P + "ext", new XAttribute("uri", "{6950BFC3-D8DA-4A85-94F7-54DA5524770B}"),
            new XElement(P188 + "commentRel", new XAttribute(XNamespace.Xmlns + "p188", P188.NamespaceName), new XAttribute(R + "id", "rIdModern"))));
        Save("ppt/slides/slide3.xml", slide);
    }
}
