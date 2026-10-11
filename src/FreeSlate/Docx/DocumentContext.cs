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
        FontLocator = fonts ?? FontLocator.Default;
        Fonts = ReferenceEquals(FontLocator, FontLocator.Default) ? FontResolver.Default : new FontResolver(FontLocator);
        DefaultTabStop = Settings.Child("defaultTabStop").Twips("val") ?? 36;
    }

    public OpcPackage Package { get; }
    public string MainPart { get; }
    public XDocument Document { get; }
    public StyleSheet Styles { get; }
    public XElement? Settings { get; }
    public Theme Theme { get; }
    public FontLocator FontLocator { get; }
    public FontResolver Fonts { get; }

    /// <summary>The interval of the default tab stops in points (w:defaultTabStop, 720 twips when absent).</summary>
    public double DefaultTabStop { get; }

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

    /// <summary>The part the main document relates to with a relationship of this type, or null.</summary>
    public XDocument? Related(string type) =>
        Package.FirstRelationship(MainPart, type) is { External: false } rel && Package.Exists(rel.Target) ? Load(rel.Target) : null;

    public void Dispose() => Package.Dispose();
}
