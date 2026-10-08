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

    /// <summary>Font families used by the rendered text, for font embedding.</summary>
    public HashSet<string> UsedFonts { get; } = new(StringComparer.OrdinalIgnoreCase);

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
    public string? Image(string sourcePart, string relationshipId)
    {
        var rel = Package.Relationship(sourcePart, relationshipId);
        if (rel is not { External: false } || !Package.Exists(rel.Target))
            return null;
        if (!images.TryGetValue(rel.Target, out var uri))
        {
            var type = Package.ContentType(rel.Target);
            uri = type is "image/png" or "image/jpeg" or "image/gif" or "image/bmp" or "image/svg+xml" or "image/webp"
                ? $"data:{type};base64,{Convert.ToBase64String(Package.ReadBytes(rel.Target))}"
                : null;   // EMF, WMF, TIFF … need converters (catalog: picture.metafiles, picture.formats)
            images[rel.Target] = uri;
        }
        return uri;
    }

    public void Dispose() => Package.Dispose();
}
