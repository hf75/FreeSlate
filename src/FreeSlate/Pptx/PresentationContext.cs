using System.Xml.Linq;
using FreeSlate.Drawing;
using FreeSlate.Opc;

namespace FreeSlate.Pptx;

/// <summary>Package-level state while converting one presentation: parts (cached, MCE-resolved), themes, images.</summary>
public sealed class PresentationContext : IDisposable
{
    readonly Dictionary<string, XDocument> documents = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, Theme> themes = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string?> images = new(StringComparer.OrdinalIgnoreCase);

    public PresentationContext(OpcPackage package)
    {
        Package = package;
        PresentationPart = package.FirstRelationship("/", "officeDocument")?.Target
                           ?? throw new InvalidDataException("No presentation part (officeDocument relationship) in the package.");
        Presentation = Load(PresentationPart);
        var root = Presentation.Root!;

        var size = root.Element(Ns.P + "sldSz");
        SlideWidth = size.Long("cx") ?? 12192000;
        SlideHeight = size.Long("cy") ?? 6858000;
        DefaultTextStyle = root.Element(Ns.P + "defaultTextStyle");

        foreach (var id in root.Element(Ns.P + "sldIdLst")?.Elements(Ns.P + "sldId") ?? [])
        {
            if (id.Attribute(Ns.R + "id") is { } rid && package.Relationship(PresentationPart, rid.Value) is { External: false } rel && package.Exists(rel.Target))
                SlideParts.Add(rel.Target);
        }
    }

    public OpcPackage Package { get; }
    public string PresentationPart { get; }
    public XDocument Presentation { get; }
    public long SlideWidth { get; }
    public long SlideHeight { get; }
    public XElement? DefaultTextStyle { get; }
    public List<string> SlideParts { get; } = [];

    /// <summary>Font families of text the browser draws itself (not as glyph outlines), for font embedding.</summary>
    public HashSet<string> UsedFonts { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Glyph outlines of the rendered text.</summary>
    public Html.GlyphCache Glyphs { get; } = new();

    public XDocument Load(string part)
    {
        if (!documents.TryGetValue(part, out var document))
        {
            document = Package.ReadXml(part);
            MarkupCompatibility.Process(document);
            documents[part] = document;
        }
        return document;
    }

    public Theme ThemeOf(string masterPart)
    {
        if (!themes.TryGetValue(masterPart, out var theme))
        {
            var themePart = Package.FirstRelationship(masterPart, "theme")?.Target;
            theme = Theme.Load(themePart != null && Package.Exists(themePart) ? Load(themePart) : null);
            themes[masterPart] = theme;
        }
        return theme;
    }

    /// <summary>An embedded image of a part as data URI, or null for missing or linked images.</summary>
    /// <param name="displayWidth">The width the picture is shown at, in pixels: metafile hatches are output pixels.</param>
    public string? Image(string sourcePart, string relationshipId, double? displayWidth = null)
    {
        var rel = Package.Relationship(sourcePart, relationshipId);
        if (rel is not { External: false } || !Package.Exists(rel.Target))
            return null;
        var type = Package.ContentType(rel.Target);
        var metafile = type is "image/x-emf" or "image/emf" or "image/x-wmf" or "image/wmf" or "application/x-msmetafile";
        var key = metafile && displayWidth is { } w ? $"{rel.Target}@{Math.Round(w)}" : rel.Target;
        if (!images.TryGetValue(key, out var uri))
        {
            uri = type switch
            {
                "image/png" or "image/jpeg" or "image/gif" or "image/bmp" or "image/svg+xml" or "image/webp" =>
                    $"data:{type};base64,{Convert.ToBase64String(Package.ReadBytes(rel.Target))}",
                _ when metafile => Metafiles.MetafileConverter.ToDataUri(Package.ReadBytes(rel.Target), $"mf{images.Count}_", displayWidth),
                _ => Converted(Package.ReadBytes(rel.Target)),
            };
            images[key] = uri;
        }
        return uri;
    }

    /// <summary>
    /// A picture of another content type as data URI by its bytes (catalog: picture.formats): TIFF converted to PNG
    /// or JPEG, formats browsers show under a content type the package names otherwise; null for formats without
    /// converter.
    /// </summary>
    static string? Converted(byte[] data)
    {
        if (Html.Tiff.IsTiff(data))
            return Html.Tiff.ToWeb(data) is var (tiffType, web) ? $"data:{tiffType};base64,{Convert.ToBase64String(web)}" : null;
        var type = data switch
        {
            [0x89, 0x50, 0x4E, 0x47, ..] => "image/png",
            [0xFF, 0xD8, 0xFF, ..] => "image/jpeg",
            [0x47, 0x49, 0x46, 0x38, ..] => "image/gif",
            [0x42, 0x4D, ..] => "image/bmp",
            [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50, ..] => "image/webp",
            _ => null,
        };
        return type != null ? $"data:{type};base64,{Convert.ToBase64String(data)}" : null;
    }

    /// <summary>
    /// The size of an embedded picture in pixels at 96 dpi from its own resolution (PNG pHYs, JPEG density), as
    /// PowerPoint sizes tiles; (0, 0) when unknown.
    /// </summary>
    public (double W, double H) ImageSize(string sourcePart, string relationshipId, double dpi = 0)
    {
        var rel = Package.Relationship(sourcePart, relationshipId);
        if (rel is not { External: false } || !Package.Exists(rel.Target))
            return default;
        return Html.ImageInfo.Size(Package.ReadBytes(rel.Target), dpi);
    }

    public void Dispose() => Package.Dispose();
}
