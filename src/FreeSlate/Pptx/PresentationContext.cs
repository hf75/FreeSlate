using System.Xml.Linq;
using FreeSlate.Drawing;
using FreeSlate.Fonts;
using FreeSlate.Opc;
using FreeSlate.Text;

namespace FreeSlate.Pptx;

/// <summary>Package-level state while converting one presentation: parts (cached, MCE-resolved), themes, images.</summary>
public sealed class PresentationContext : IDisposable
{
    readonly Dictionary<string, XDocument> documents = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, Theme> themes = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string?> images = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="fonts">Where fonts are found; <see cref="FontLocator.Default"/> when null.</param>
    /// <param name="embeddedFontsFirst">Use the presentation's embedded fonts even where the family is installed.</param>
    public PresentationContext(OpcPackage package, FontLocator? fonts = null, bool embeddedFontsFirst = false)
    {
        Package = package;
        PresentationPart = package.FirstRelationship("/", "officeDocument")?.Target
                           ?? throw new InvalidDataException("No presentation part (officeDocument relationship) in the package.");
        Presentation = Load(PresentationPart);
        var root = Presentation.Root!;
        FontLocator = (fonts ?? FontLocator.Default).With(EmbeddedFonts(root), embeddedFontsFirst);
        Fonts = ReferenceEquals(FontLocator, FontLocator.Default) ? FontResolver.Default : new FontResolver(FontLocator);

        var size = root.Element(Ns.P + "sldSz");
        SlideWidth = size.Long("cx") ?? 12192000;
        SlideHeight = size.Long("cy") ?? 6858000;
        DefaultTextStyle = root.Element(Ns.P + "defaultTextStyle");

        foreach (var id in root.Element(Ns.P + "sldIdLst")?.Elements(Ns.P + "sldId") ?? [])
        {
            if (id.Attribute(Ns.R + "id") is { } rid && package.Relationship(PresentationPart, rid.Value) is { External: false } rel && package.Exists(rel.Target))
            {
                SlideParts.Add(rel.Target);
                slideIds.Add(id.Str("id") ?? "");
            }
        }
    }

    readonly List<string> slideIds = [];

    /// <summary>
    /// The slides (first and last, numbered from 1) a zoom object leads to: "slide:" and a slide id (p:sldId/@id),
    /// or "section:" and a section id (p14:section/@id, its slides); null if there is no such slide or section.
    /// </summary>
    public (int First, int Last)? ZoomTarget(string target)
    {
        if (target.StartsWith("slide:", StringComparison.Ordinal))
            return slideIds.IndexOf(target[6..]) is var index and >= 0 ? (index + 1, index + 1) : null;
        if (!target.StartsWith("section:", StringComparison.Ordinal))
            return null;
        XNamespace p14 = "http://schemas.microsoft.com/office/powerpoint/2010/main";
        var section = Presentation.Root!.Descendants(p14 + "section").FirstOrDefault(s => string.Equals(s.Str("id"), target[8..], StringComparison.OrdinalIgnoreCase));
        var slides = section?.Element(p14 + "sldIdLst")?.Elements(p14 + "sldId").Select(s => slideIds.IndexOf(s.Str("id") ?? "") + 1).Where(n => n > 0).ToList();
        return slides is { Count: > 0 } ? (slides.Min(), slides.Max()) : null;
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

    /// <summary>The fonts available to this presentation: the installed ones and those it embeds.</summary>
    public FontLocator FontLocator { get; }

    /// <summary>Resolves typefaces to fonts, the presentation's embedded fonts included.</summary>
    public FontResolver Fonts { get; }

    /// <summary>
    /// The fonts embedded in the presentation (p:embeddedFontLst, catalog: text.embeddedFonts), one face per style:
    /// Embedded OpenType as PowerPoint writes it, or a plain TrueType/OpenType font; decoded when first used.
    /// </summary>
    List<FontFace> EmbeddedFonts(XElement root)
    {
        var faces = new List<FontFace>();
        foreach (var font in root.Element(Ns.P + "embeddedFontLst")?.Elements(Ns.P + "embeddedFont") ?? [])
        {
            if (font.Element(Ns.P + "font")?.Str("typeface") is not { Length: > 0 } typeface)
                continue;
            foreach (var (style, bold, italic) in new[] { ("regular", false, false), ("bold", true, false), ("italic", false, true), ("boldItalic", true, true) })
            {
                if (font.Element(Ns.P + style)?.Attribute(Ns.R + "id")?.Value is not { } id
                    || Package.Relationship(PresentationPart, id) is not { External: false } rel || !Package.Exists(rel.Target))
                    continue;
                var part = rel.Target;
                faces.Add(new FontFace(typeface, bold, italic, $"embedded:{part}", System: false)
                {
                    Embedded = new Lazy<byte[]?>(() =>
                    {
                        try
                        {
                            var data = Package.ReadBytes(part);
                            return data is [0, 1, 0, 0, ..] or [(byte)'O', (byte)'T', (byte)'T', (byte)'O', ..] or [(byte)'t', (byte)'r', (byte)'u', (byte)'e', ..]
                                ? data
                                : EmbeddedOpenType.Read(data).Font;
                        }
                        catch (InvalidDataException)
                        {
                            return null;
                        }
                    }),
                });
            }
        }
        return faces;
    }

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
                "image/png" or "image/jpeg" or "image/gif" or "image/bmp" or "image/webp" => Native(type, Package.ReadBytes(rel.Target)),
                "image/svg+xml" => $"data:{type};base64,{Convert.ToBase64String(Package.ReadBytes(rel.Target))}",
                _ when metafile => Metafiles.MetafileConverter.ToDataUri(Package.ReadBytes(rel.Target), $"mf{images.Count}_", displayWidth),
                _ => Converted(Package.ReadBytes(rel.Target)),
            };
            images[key] = uri;
        }
        return uri;
    }

    /// <summary>
    /// A picture under a content type browsers show, unless its bytes are a format they do not: PowerPoint names
    /// inserted .jxr pictures image/png.
    /// </summary>
    static string? Native(string type, byte[] data) =>
        Html.JpegXr.IsJpegXr(data) || Html.Tiff.IsTiff(data) ? Converted(data) : $"data:{type};base64,{Convert.ToBase64String(data)}";

    /// <summary>
    /// A picture of another content type as data URI by its bytes (catalog: picture.formats): TIFF converted to PNG
    /// or JPEG, JPEG XR to PNG, formats browsers show under a content type the package names otherwise; null for
    /// formats without converter.
    /// </summary>
    static string? Converted(byte[] data)
    {
        if (Html.Tiff.IsTiff(data))
            return Html.Tiff.ToWeb(data) is var (tiffType, web) ? $"data:{tiffType};base64,{Convert.ToBase64String(web)}" : null;
        if (Html.JpegXr.IsJpegXr(data))
            return Html.JpegXr.ToPng(data) is { } png ? $"data:image/png;base64,{Convert.ToBase64String(png)}" : null;
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
