using System.Globalization;
using FreeSlate.Fonts;
using FreeSlate.Opc;
using SkiaSharp;

namespace FreeSlate.Skia;

/// <summary>
/// Documents as PDF (catalog: engine.output): their pages drawn by Skia (see SkiaScene.Pdf), text in the fonts it is set
/// in with their subsets embedded, drawings as vectors and pictures; the document's title, author, subject, keywords
/// and dates from its core properties; for archiving as PDF/A-2b where asked. Skia writes no structure tree (SkiaSharp
/// passes none to it), so the PDF is not tagged.
/// </summary>
public static class PdfExport
{
    /// <summary>A Word document's pages as a PDF, with the markup view's comments and changes where asked.</summary>
    /// <param name="archive">Whether the PDF is PDF/A-2b (see Metadata).</param>
    /// <exception cref="PasswordException">The document needs a password, or the password is wrong.</exception>
    public static void FromDocx(string docxPath, Stream output, FontLocator? fonts = null, string? password = null, CultureInfo? culture = null,
        bool markup = false, bool archive = false)
    {
        var scenes = DocxConverter.ToScenes(docxPath, fonts, password, culture, markup);
        SkiaScene.Pdf(scenes, output, metadata: Metadata(docxPath, password, archive));
    }

    /// <summary>A presentation's slides as a PDF, those the slide show leaves out only where asked.</summary>
    /// <param name="archive">Whether the PDF is PDF/A-2b (see Metadata).</param>
    /// <exception cref="PasswordException">The presentation needs a password, or the password is wrong.</exception>
    public static void FromPptx(string pptxPath, Stream output, FontLocator? fonts = null, string? password = null, CultureInfo? culture = null,
        bool hidden = false, bool archive = false)
    {
        var scenes = PptxConverter.ToScenes(pptxPath, fonts, password, culture: culture, hidden: hidden);
        SkiaScene.Pdf(scenes, output, metadata: Metadata(pptxPath, password, archive));
    }

    /// <summary>
    /// The PDF's information from the package's core properties (docProps/core.xml); with <paramref name="archive"/>
    /// PDF/A-2b as Skia writes it: XMP metadata matching the information, an sRGB output intent, the fonts embedded.
    /// </summary>
    public static SKDocumentPdfMetadata Metadata(string path, string? password, bool archive = false)
    {
        var metadata = SkiaScene.PdfMetadata();
        metadata.Creator = "FreeSlate";
        metadata.PdfA = archive;
        using var package = OpcPackage.Open(path, password);
        if (package.FirstRelationship("/", "core-properties")?.Target is not { } part || !package.Exists(part))
            return metadata;
        var core = package.ReadXml(part).Root;
        string? Value(string name) => core?.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value is { Length: > 0 } value ? value : null;
        DateTime? Date(string name) => DateTime.TryParse(Value(name), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var date) ? date : null;
        metadata.Title = Value("title");
        metadata.Author = Value("creator");
        metadata.Subject = Value("subject");
        metadata.Keywords = Value("keywords");
        metadata.Creation = Date("created");
        metadata.Modified = Date("modified");
        return metadata;
    }
}
