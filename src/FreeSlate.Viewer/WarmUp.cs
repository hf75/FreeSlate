using System.IO.Compression;
using System.Text;
using FreeSlate.Skia;
using SkiaSharp;

namespace FreeSlate.Viewer;

/// <summary>
/// The engine made ready while the window waits for a document: the system's fonts found and the code that reads, lays
/// out and draws pages compiled, by opening a small document of its own (text, a table, a header with its page number)
/// and drawing its page. Opening the first document takes that much less.
/// </summary>
static class WarmUp
{
    public static void Run()
    {
        try
        {
            using var document = WordDocument.Open(Document());
            document.Scenes();
            using var recorder = new SKPictureRecorder();
            SkiaScene.Draw(document.Scene(0), recorder.BeginRecording(new SKRect(0, 0, 800, 1100)));
            using var picture = recorder.EndRecording();
        }
        catch (Exception)
        {
            // Whatever fails here, the first document only takes longer.
        }
    }

    /// <summary>The small document: a few paragraphs of text, some of it bold, a table and a header with a PAGE field.</summary>
    static byte[] Document()
    {
        const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        const string R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        const string Text = "The quick brown fox jumps over the lazy dog, again and again, until the line is full and breaks.";
        var paragraphs = string.Concat(Enumerable.Range(0, 12).Select(i =>
            $"<w:p><w:r>{(i % 3 == 0 ? "<w:rPr><w:b/></w:rPr>" : "")}<w:t>{Text}</w:t></w:r></w:p>"));
        var table = "<w:tbl><w:tblPr><w:tblW w:w=\"0\" w:type=\"auto\"/></w:tblPr><w:tblGrid><w:gridCol w:w=\"4500\"/><w:gridCol w:w=\"4500\"/></w:tblGrid>"
                    + "<w:tr><w:tc><w:p><w:r><w:t>One</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>Two</w:t></w:r></w:p></w:tc></w:tr></w:tbl>";
        var parts = new Dictionary<string, string>
        {
            ["[Content_Types].xml"] = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
                + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/>"
                + "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>"
                + "<Override PartName=\"/word/header1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml\"/></Types>",
            ["_rels/.rels"] = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>",
            ["word/_rels/document.xml.rels"] = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/header\" Target=\"header1.xml\"/></Relationships>",
            ["word/header1.xml"] = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><w:hdr xmlns:w=\"{W}\"><w:p><w:pPr><w:jc w:val=\"right\"/></w:pPr>"
                + "<w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText xml:space=\"preserve\"> PAGE </w:instrText></w:r>"
                + "<w:r><w:fldChar w:fldCharType=\"separate\"/></w:r><w:r><w:t>1</w:t></w:r><w:r><w:fldChar w:fldCharType=\"end\"/></w:r></w:p></w:hdr>",
            ["word/document.xml"] = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><w:document xmlns:w=\"{W}\" xmlns:r=\"{R}\"><w:body>{paragraphs}{table}<w:p/>"
                + "<w:sectPr><w:headerReference w:type=\"default\" r:id=\"rId1\"/><w:pgSz w:w=\"11906\" w:h=\"16838\"/>"
                + "<w:pgMar w:top=\"1417\" w:right=\"1417\" w:bottom=\"1134\" w:left=\"1417\" w:header=\"708\" w:footer=\"708\" w:gutter=\"0\"/></w:sectPr></w:body></w:document>",
        };
        using var bytes = new MemoryStream();
        using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, xml) in parts)
            {
                using var stream = zip.CreateEntry(name).Open();
                stream.Write(Encoding.UTF8.GetBytes(xml));
            }
        }
        return bytes.ToArray();
    }
}
