using System.Globalization;
using FreeSlate.Fonts;
using FreeSlate.Opc;
using SkiaSharp;

namespace FreeSlate.Skia;

/// <summary>
/// Documents as PDF (catalog: engine.output): their pages drawn by Skia (see SkiaScene.Pdf), text in the fonts it is set
/// in with their subsets embedded, drawings as vectors and pictures; the document's title, author, subject, keywords
/// and dates from its core properties.
/// </summary>
public static class PdfExport
{
    /// <summary>A Word document's pages as a PDF, with the markup view's comments and changes where asked.</summary>
    /// <exception cref="PasswordException">The document needs a password, or the password is wrong.</exception>
    public static void FromDocx(string docxPath, Stream output, FontLocator? fonts = null, string? password = null, CultureInfo? culture = null,
        bool markup = false)
    {
        var scenes = DocxConverter.ToScenes(docxPath, fonts, password, culture, markup);
        SkiaScene.Pdf(scenes, output, metadata: Metadata(docxPath, password));
    }

    /// <summary>A presentation's slides as a PDF, those the slide show leaves out only where asked.</summary>
    /// <exception cref="PasswordException">The presentation needs a password, or the password is wrong.</exception>
    public static void FromPptx(string pptxPath, Stream output, FontLocator? fonts = null, string? password = null, CultureInfo? culture = null,
        bool hidden = false)
    {
        var scenes = PptxConverter.ToScenes(pptxPath, fonts, password, culture: culture, hidden: hidden);
        SkiaScene.Pdf(scenes, output, metadata: Metadata(pptxPath, password));
    }

    /// <summary>The PDF's information from the package's core properties (docProps/core.xml).</summary>
    static SKDocumentPdfMetadata Metadata(string path, string? password)
    {
        var metadata = new SKDocumentPdfMetadata { Producer = "FreeSlate", Creator = "FreeSlate", RasterDpi = 300 };
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
