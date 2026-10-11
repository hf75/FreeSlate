using System.Xml.Linq;
using FreeSlate.Drawing;
using FreeSlate.Fonts;
using FreeSlate.Opc;
using FreeSlate.Text;

namespace FreeSlate.Docx;

/// <summary>
/// Package-level state while converting one Word document (catalog: package.opc, document.body): the main document
/// part and the parts it refers to (styles, settings, theme, numbering, fonts), cached and with markup compatibility
/// resolved, and the fonts and glyphs the output uses.
/// </summary>
public sealed class DocumentContext : IDisposable
{
    readonly Dictionary<string, XDocument> documents = new(StringComparer.OrdinalIgnoreCase);

    public DocumentContext(OpcPackage package, FontLocator? fonts = null)
    {
        Package = package;
        MainPart = package.FirstRelationship("/", "officeDocument")?.Target
                   ?? throw new InvalidDataException("No main document part (officeDocument relationship) in the package.");
        Document = Load(MainPart);
        Styles = new StyleSheet(Related("styles"));
        Settings = Related("settings")?.Root;
        Theme = Theme.Load(Related("theme"));
        FontLocator = (fonts ?? FontLocator.Default).With(EmbeddedFonts());
        Fonts = ReferenceEquals(FontLocator, FontLocator.Default) ? FontResolver.Default : new FontResolver(FontLocator);
        DefaultTabStop = Settings.Child("defaultTabStop").Twips("val") ?? 36;
        Numbering = new NumberingDefinitions(Related("numbering"), Styles);
        FontTable = new FontTable(Related("fontTable"));
        DecimalSymbol = Settings.Child("decimalSymbol").Val() is { Length: > 0 } symbol ? symbol[0] : '.';
    }

    public OpcPackage Package { get; }
    public string MainPart { get; }

    /// <summary>The document's page background (w:background), or null.</summary>
    public XElement? Background => Document?.Root?.Element(Wml.W + "background");
    public XDocument Document { get; }
    public StyleSheet Styles { get; }
    public XElement? Settings { get; }
    public Theme Theme { get; }
    public NumberingDefinitions Numbering { get; }

    /// <summary>The font table, which decides the fonts for families the machine lacks (catalog: font.table).</summary>
    public FontTable FontTable { get; }
    public FontLocator FontLocator { get; }
    public FontResolver Fonts { get; }

    /// <summary>The regional format for dates the document shows (date blocks, fields), the system's by default.</summary>
    public System.Globalization.CultureInfo Culture { get; set; } = System.Globalization.CultureInfo.CurrentCulture;

    /// <summary>The interval of the default tab stops in points (w:defaultTabStop, 720 twips when absent).</summary>
    public double DefaultTabStop { get; }

    /// <summary>The character decimal tab stops align on (w:decimalSymbol, the language's decimal separator).</summary>
    public char DecimalSymbol { get; }

    /// <summary>The glyph outlines the document uses, defined once.</summary>
    public Html.GlyphCache Glyphs { get; } = new();

    /// <summary>Font families the output names (the browser draws their text where outlines are not used).</summary>
    public HashSet<string> UsedFonts { get; } = new(StringComparer.OrdinalIgnoreCase);

    public XDocument Load(string part)
    {
        if (documents.TryGetValue(part, out var document))
            return document;
        document = Package.ReadXml(part);
        MarkupCompatibility.Process(document);
        documents[part] = document;
        return document;
    }

    /// <summary>
    /// The fonts the document embeds (w:embedRegular, w:embedBold, w:embedItalic, w:embedBoldItalic, catalog:
    /// font.embedded), one face per style, decoded when first used: obfuscated fonts (.odttf) have the first 32 bytes
    /// XORed with their w:fontKey, the GUID's bytes read from its end (ISO/IEC 29500 17.8.1; checked against Word: a
    /// Gabriola it embedded decodes to the installed file). Installed fonts come first, as in Word.
    /// </summary>
    List<FontFace> EmbeddedFonts()
    {
        var faces = new List<FontFace>();
        if (Package.FirstRelationship(MainPart, "fontTable") is not { External: false } table || !Package.Exists(table.Target))
            return faces;
        foreach (var font in Load(table.Target).Root?.Elements(Wml.W + "font") ?? [])
        {
            if (font.Attr("name") is not { Length: > 0 } family)
                continue;
            foreach (var (style, bold, italic) in new[] { ("embedRegular", false, false), ("embedBold", true, false), ("embedItalic", false, true), ("embedBoldItalic", true, true) })
            {
                if (font.Child(style) is not { } embed || embed.Attr(Wml.R + "id") is not { } id
                    || Package.Relationship(table.Target, id) is not { External: false } rel || !Package.Exists(rel.Target))
                    continue;
                var (part, key) = (rel.Target, embed.Attr("fontKey"));
                faces.Add(new FontFace(family, bold, italic, $"embedded:{part}", System: false)
                {
                    Embedded = new Lazy<byte[]?>(() =>
                    {
                        try
                        {
                            return Deobfuscate(Package.ReadBytes(part), key);
                        }
                        catch (Exception e) when (e is InvalidDataException or FormatException or IOException)
                        {
                            return null;
                        }
                    }),
                });
            }
        }
        return faces;
    }

    /// <summary>An embedded font's bytes with the font key's obfuscation undone (none without a key).</summary>
    static byte[] Deobfuscate(byte[] data, string? fontKey)
    {
        var hex = (fontKey ?? "").Replace("{", "").Replace("}", "").Replace("-", "");
        if (hex.Length != 32 || data.Length < 32)
            return data;
        var key = new byte[16];
        for (var i = 0; i < 16; i++)
            key[i] = Convert.ToByte(hex.Substring(30 - 2 * i, 2), 16);
        for (var i = 0; i < 32; i++)
            data[i] ^= key[i % 16];
        return data;
    }

    /// <summary>The part the main document relates to with a relationship of this type, or null.</summary>
    public XDocument? Related(string type) =>
        Package.FirstRelationship(MainPart, type) is { External: false } rel && Package.Exists(rel.Target) ? Load(rel.Target) : null;

    public void Dispose() => Package.Dispose();
}
