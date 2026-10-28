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
    /// <param name="lines">Read the parts with line info, for finding their elements in their text (see Opc.PartText).</param>
    public PresentationContext(OpcPackage package, FontLocator? fonts = null, bool embeddedFontsFirst = false, bool lines = false)
    {
        Package = package;
        this.lines = lines;
        PresentationPart = package.FirstRelationship("/", "officeDocument")?.Target
                           ?? throw new InvalidDataException("No presentation part (officeDocument relationship) in the package.");
        var root = Presentation.Root!;
        FontLocator = (fonts ?? FontLocator.Default).With(EmbeddedFonts(root), embeddedFontsFirst);
        Fonts = ReferenceEquals(FontLocator, FontLocator.Default) ? FontResolver.Default : new FontResolver(FontLocator);

        var size = root.Element(Ns.P + "sldSz");
        SlideWidth = size.Long("cx") ?? 12192000;
        SlideHeight = size.Long("cy") ?? 6858000;
        DefaultTextStyle = root.Element(Ns.P + "defaultTextStyle") ?? BuiltInTextStyle;
        ReadSlides();
    }

    /// <summary>
    /// Reads the slides of the presentation (p:sldIdLst) into SlideParts, anew where the presentation part changed (see
    /// Changed).
    /// </summary>
    public void ReadSlides()
    {
        SlideParts.Clear();
        slideIds.Clear();
        foreach (var id in Presentation.Root?.Element(Ns.P + "sldIdLst")?.Elements(Ns.P + "sldId") ?? [])
        {
            if (id.Attribute(Ns.R + "id") is { } rid && Package.Relationship(PresentationPart, rid.Value) is { External: false } rel && Package.Exists(rel.Target))
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
    /// <summary>The presentation part (presentation.xml) as it reads now.</summary>
    public XDocument Presentation => Load(PresentationPart);
    public long SlideWidth { get; }
    public long SlideHeight { get; }
    public XElement? DefaultTextStyle { get; }

    /// <summary>
    /// PowerPoint's own default text style for presentations without one (p:defaultTextStyle, as other programs write
    /// them), Office's default: the theme's fonts, kerning from 12 pt and half an inch more indent at every level
    /// (measured: LibreOffice's Calibri text kerned without any kern attribute; levels 1 to 3 of a text box added to
    /// LibreOffice's tdf160487 start 36 pt apart in the theme's Calibri, where a defaultTextStyle without fonts gives
    /// them Arial).
    /// </summary>
    static readonly XElement BuiltInTextStyle = XElement.Parse(
        $"<p:defaultTextStyle {StyleNamespaces}>{string.Concat(Enumerable.Range(1, 9).Select(DefaultLevel))}</p:defaultTextStyle>");

    static string DefaultLevel(int level) => $"""
        <a:lvl{level}pPr marL="{457200 * (level - 1)}" algn="l" defTabSz="914400"><a:defRPr sz="1800" kern="1200"><a:solidFill>
        <a:schemeClr val="tx1"/></a:solidFill><a:latin typeface="+mn-lt"/><a:ea typeface="+mn-ea"/><a:cs typeface="+mn-cs"/></a:defRPr></a:lvl{level}pPr>
        """;

    /// <summary>
    /// PowerPoint's own master title or body style (<paramref name="kind"/> title or body) for masters without it (no
    /// p:txStyles, or none of that kind in it, as LibreOffice writes some): Office's default ones (measured with the
    /// object model on LibreOffice's tdf160487: titles at 44 pt in the theme's heading font, body text at 28, 24, 20
    /// and 18 pt with bullets, indents and spacing before, all kerned from 12 pt at 90 % line spacing; the title's
    /// levels below the first take nothing; a style the master has is used alone).
    /// </summary>
    public static XElement BuiltInMasterStyle(string kind) => kind == "title" ? BuiltInTitleStyle : BuiltInBodyStyle;

    const string StyleNamespaces = """xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" """;

    static readonly XElement BuiltInTitleStyle = XElement.Parse($"""
        <p:titleStyle {StyleNamespaces}><a:lvl1pPr algn="l" defTabSz="914400"><a:lnSpc><a:spcPct val="90000"/></a:lnSpc>
        <a:spcBef><a:spcPct val="0"/></a:spcBef><a:buNone/><a:defRPr sz="4400" kern="1200"><a:solidFill><a:schemeClr val="tx1"/></a:solidFill>
        <a:latin typeface="+mj-lt"/><a:ea typeface="+mj-ea"/><a:cs typeface="+mj-cs"/></a:defRPr></a:lvl1pPr></p:titleStyle>
        """);

    static readonly XElement BuiltInBodyStyle = XElement.Parse(
        $"<p:bodyStyle {StyleNamespaces}>{string.Concat(Enumerable.Range(1, 9).Select(BodyLevel))}</p:bodyStyle>");

    static string BodyLevel(int level) => $"""
        <a:lvl{level}pPr marL="{228600 + 457200 * (level - 1)}" indent="-228600" algn="l" defTabSz="914400"><a:lnSpc><a:spcPct val="90000"/></a:lnSpc>
        <a:spcBef><a:spcPts val="{(level == 1 ? 1000 : 500)}"/></a:spcBef><a:buFont typeface="Arial"/><a:buChar char="•"/>
        <a:defRPr sz="{BodySize(level)}" kern="1200"><a:solidFill><a:schemeClr val="tx1"/></a:solidFill><a:latin typeface="+mn-lt"/>
        <a:ea typeface="+mn-ea"/><a:cs typeface="+mn-cs"/></a:defRPr></a:lvl{level}pPr>
        """;

    static int BodySize(int level) => level switch { 1 => 2800, 2 => 2400, 3 => 2000, _ => 1800 };

    public List<string> SlideParts { get; } = [];

    /// <summary>Whether a slide has a 3D model, for which the document gets the WebGL renderer (catalog: picture.model3d).</summary>
    public bool HasModels { get; set; }

    /// <summary>Font families of text the browser draws itself (not as glyph outlines), for font embedding.</summary>
    public HashSet<string> UsedFonts { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The fonts available to this presentation: the installed ones and those it embeds.</summary>
    public FontLocator FontLocator { get; }

    /// <summary>Resolves typefaces to fonts, the presentation's embedded fonts included.</summary>
    public FontResolver Fonts { get; }

    /// <summary>
    /// The regional format of numbers and dates in charts: PowerPoint takes the system's, not the chart's language
    /// (measured: an en-US chart shows 2,5 on a German system).
    /// </summary>
    public System.Globalization.CultureInfo Culture { get; init; } = System.Globalization.CultureInfo.CurrentCulture;

    /// <summary>Whether the drawings are a Word document's, whose charts take Word's defaults (catalog: chart.formatting).</summary>
    public bool Word { get; init; }

    /// <summary>
    /// The ids of the paragraphs (a:p) whose places the slides' scenes carry (see PlacesItem), which a document gives them
    /// (PresentationDocument); null where the scenes have none.
    /// </summary>
    public IReadOnlyDictionary<XElement, int>? Paragraphs { get; set; }

    /// <summary>
    /// The major version of the application that saved the presentation (docProps/app.xml AppVersion: 12 Office 2007,
    /// 14 Office 2010 …), null where the package does not say; charts without style part look by it (catalog: chart.formatting).
    /// </summary>
    public int? AppVersion => (appVersion ??= new Lazy<int?>(ReadAppVersion)).Value;

    Lazy<int?>? appVersion;

    int? ReadAppVersion()
    {
        XNamespace ep = "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties";
        var part = Package.FirstRelationship("/", "extended-properties")?.Target;
        var value = part != null && Package.Exists(part) ? Load(part).Root?.Element(ep + "AppVersion")?.Value : null;
        return int.TryParse(value?.Split('.')[0].Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var major) ? major : null;
    }

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
    /// <summary>The glyph outlines the output uses; a Word document's drawings share the document's.</summary>
    public Html.GlyphCache Glyphs { get; init; } = new();

    readonly bool lines;

    public XDocument Load(string part)
    {
        if (!documents.TryGetValue(part, out var document))
        {
            document = Package.ReadXml(part, lines);
            MarkupCompatibility.Process(document, chartEx: true);
            documents[part] = document;
        }
        return document;
    }

    /// <summary>Forgets a part read before, which the next Load reads anew from the package (after it was given new bytes).</summary>
    public void Changed(string part) => documents.Remove(part);

    public Theme ThemeOf(string masterPart)
    {
        if (!themes.TryGetValue(masterPart, out var theme))
        {
            var themePart = Package.FirstRelationship(masterPart, "theme")?.Target;
            theme = Theme.Load(themePart != null && Package.Exists(themePart) ? Load(themePart) : null, themePart);
            themes[masterPart] = theme;
        }
        return theme;
    }

    /// <summary>An embedded image of a part as data URI, or null for missing or linked images.</summary>
    /// <param name="displayWidth">The width the picture is shown at, in pixels: metafile hatches are output pixels.</param>
    public string? Image(string sourcePart, string relationshipId, double? displayWidth = null)
    {
        // Pictures of imported content (RTF chunks) come as data URIs; metafiles among them are converted.
        if (relationshipId.StartsWith("data:", StringComparison.Ordinal))
            return relationshipId.StartsWith("data:image/x-emf", StringComparison.Ordinal) || relationshipId.StartsWith("data:image/x-wmf", StringComparison.Ordinal)
                ? DataBytes(relationshipId) is { } embedded && IsMetafile(embedded) ? Metafiles.MetafileConverter.ToDataUri(embedded, $"mf{images.Count}_", displayWidth, Word) : null
                : relationshipId;
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
                // A metafile part may hold a raster picture (a PNG named .emf), which PowerPoint shows by its bytes.
                _ when metafile => Package.ReadBytes(rel.Target) is var bytes && IsMetafile(bytes)
                    ? Metafiles.MetafileConverter.ToDataUri(bytes, $"mf{images.Count}_", displayWidth, Word)
                    : Converted(bytes),
                _ => Converted(Package.ReadBytes(rel.Target)),
            };
            images[key] = uri;
        }
        return uri;
    }

    /// <summary>
    /// A TIFF picture cut to its source rectangle (the fractions cropped off each side) as PNG data URI, or null for
    /// other pictures: Word's PDF holds only the cropped pixels, which the rasterizer samples from their own grid (found
    /// by agent BF in Word's PDF of docx/external/libreoffice/tif-crop: 2815 by 2456 of a 1-bit TIFF's 3495 by 4958
    /// pixels, 87.8 -> 100 %).
    /// </summary>
    public string? CroppedImage(string sourcePart, string relationshipId, (double L, double T, double R, double B) crop)
    {
        var rel = Package.Relationship(sourcePart, relationshipId);
        if (rel is not { External: false } || !Package.Exists(rel.Target))
            return null;
        var key = $"{rel.Target}#{crop.L},{crop.T},{crop.R},{crop.B}";
        if (images.TryGetValue(key, out var cached))
            return cached;
        string? uri = null;
        var bytes = Package.ReadBytes(rel.Target);
        if (Html.Tiff.IsTiff(bytes) && Html.Tiff.Decode(bytes) is { } decoded)
        {
            var (width, height, rgba) = decoded;
            var (x0, y0) = ((int)Math.Round(width * crop.L), (int)Math.Round(height * crop.T));
            var (x1, y1) = (width - (int)Math.Round(width * crop.R), height - (int)Math.Round(height * crop.B));
            if (x1 > x0 && y1 > y0)
            {
                var (cw, ch) = (x1 - x0, y1 - y0);
                var cut = new byte[cw * ch * 4];
                for (var row = 0; row < ch; row++)
                    System.Buffer.BlockCopy(rgba, ((y0 + row) * width + x0) * 4, cut, row * cw * 4, cw * 4);
                uri = Metafiles.Png.DataUri(cw, ch, cut);
            }
        }
        images[key] = uri;
        return uri;
    }

    /// <summary>EMF (a header record, type 1) or WMF (placeable, or a memory or disk metafile header).</summary>
    static bool IsMetafile(byte[] data) =>
        data is [0x01, 0x00, 0x00, 0x00, ..] or [0xD7, 0xCD, 0xC6, 0x9A, ..] or [0x01 or 0x02, 0x00, 0x09, 0x00, ..];

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
    /// <summary>The bytes of a base64 data URI, or null.</summary>
    static byte[]? DataBytes(string uri)
    {
        var at = uri.IndexOf(";base64,", StringComparison.Ordinal);
        if (at < 0)
            return null;
        try
        {
            return Convert.FromBase64String(uri[(at + 8)..]);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public (double W, double H) ImageSize(string sourcePart, string relationshipId, double dpi = 0)
    {
        if (relationshipId.StartsWith("data:", StringComparison.Ordinal))
            return DataBytes(relationshipId) is { } data ? Html.ImageInfo.Size(data, dpi) : default;
        var rel = Package.Relationship(sourcePart, relationshipId);
        if (rel is not { External: false } || !Package.Exists(rel.Target))
            return default;
        return Html.ImageInfo.Size(Package.ReadBytes(rel.Target), dpi);
    }

    public void Dispose() => Package.Dispose();
}
