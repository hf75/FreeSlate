using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FreeSlate.Docx;

namespace FreeSlate;

/// <summary>A face of a font to embed in a document (see WordDocument.EmbedFonts): its family, whether it is bold and italic, its file.</summary>
public sealed record EmbeddedFace(string Family, bool Bold, bool Italic, byte[] Data);

/// <summary>
/// Fonts embedded as Word's File > Options > Save > Embed fonts in the file embeds them (catalog: font.embedded; ISO/IEC 29500 17.8.1),
/// so that Word shows a document in fonts it does not have as the engine does.
/// </summary>
public sealed partial class WordDocument
{
    const string FontTableType = "application/vnd.openxmlformats-officedocument.wordprocessingml.fontTable+xml";
    const string ObfuscatedFontType = "application/vnd.openxmlformats-officedocument.obfuscatedFont";

    /// <summary>
    /// The families the document sets text in as its parts name them: the runs', styles' and lists' fonts (w:rFonts ascii, hAnsi, eastAsia,
    /// cs) and those of its drawings, charts, SmartArt and theme (a:latin, a:ea, a:cs typeface), but a theme's references (+mn-lt).
    /// </summary>
    public IReadOnlyCollection<string> FontFamilies()
    {
        Read();
        var families = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in package.PartNames.Where(p => p.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) && !p.Contains("fontTable", StringComparison.OrdinalIgnoreCase)))
        {
            var text = Encoding.UTF8.GetString(part == context.MainPart ? Spliced(whole, islands) : package.ReadBytes(part));
            foreach (Match m in FontNames().Matches(text))
            {
                var name = SecurityElement.FromString("<n>" + m.Groups[1].Value + "</n>")?.Text ?? m.Groups[1].Value;
                if (name.Length > 0 && !name.StartsWith('+'))
                    families.Add(name);
            }
        }
        return families;
    }

    [GeneratedRegex(@"(?:\bw:(?:ascii|hAnsi|eastAsia|cs)|\btypeface)=""([^""]*)""")]
    private static partial Regex FontNames();

    /// <summary>
    /// The family a paragraph's Latin text is set in where its runs name none of their own: its mark's font (its style's, the document's
    /// defaults), a theme font as the theme names it (see Docx.Layout.RunStyles.Family).
    /// </summary>
    public string FontOf(int paragraph) => new Docx.Layout.RunStyles(context).Family(Find(paragraph).Paragraph.MarkFormat, Docx.Layout.WordSlot.HAnsi);

    /// <summary>
    /// Embeds fonts in the document as Word does with Embed fonts in the file: each face a part of its own (word/fonts/fontN.odttf, an
    /// obfuscated font, its first 32 bytes XORed with a new GUID's bytes read from its end, which is its w:fontKey: the reverse of what
    /// DocumentContext reads, checked against Word), named in the font table by its family's w:font (w:embedRegular, w:embedBold,
    /// w:embedItalic, w:embedBoldItalic after its other properties; a face it embeds already kept), the font table made where the
    /// document has none, and the settings' w:embedTrueTypeFonts on. A change of its own; returns how many faces were embedded.
    /// </summary>
    public int EmbedFonts(IReadOnlyList<EmbeddedFace> faces)
    {
        Read();
        XNamespace w = Wml.W.NamespaceName, r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var changes = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        var tablePart = package.FirstRelationship(context.MainPart, "fontTable") is { External: false } related && package.Exists(related.Target) ? related.Target : null;
        var table = tablePart != null ? Xml(package.ReadBytes(tablePart))
            : new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), new XElement(w + "fonts", new XAttribute(XNamespace.Xmlns + "w", w.NamespaceName)));
        var root = table.Root!;
        if (root.Attributes().All(a => a.Value != r.NamespaceName))
            root.Add(new XAttribute(XNamespace.Xmlns + "r", r.NamespaceName));
        string Style(EmbeddedFace face) => face.Bold ? face.Italic ? "embedBoldItalic" : "embedBold" : face.Italic ? "embedItalic" : "embedRegular";
        var parts = new List<NewPart>();
        var placed = new List<(XElement Font, XElement Embed)>();
        foreach (var face in faces)
        {
            var font = root.Elements(w + "font").FirstOrDefault(f => string.Equals((string?)f.Attribute(w + "name"), face.Family, StringComparison.OrdinalIgnoreCase));
            if (font == null)
                root.Add(font = new XElement(w + "font", new XAttribute(w + "name", face.Family)));
            if (font.Element(w + Style(face)) != null || placed.Any(p => p.Font == font && p.Embed.Name.LocalName == Style(face)))
                continue;
            var guid = Guid.NewGuid();
            var key = "{" + guid.ToString().ToUpperInvariant() + "}";
            var hex = guid.ToString("N").ToUpperInvariant();
            var data = (byte[])face.Data.Clone();
            for (var i = 0; i < 32 && i < data.Length; i++)
                data[i] ^= Convert.ToByte(hex.Substring(30 - 2 * (i % 16), 2), 16);
            var name = "font" + parts.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            parts.Add(new NewPart(name, "/word/fonts/font1.odttf", ObfuscatedFontType, RelationshipTypes + "font", data, From: tablePart == null ? "fontTable" : null));
            placed.Add((font, new XElement(w + Style(face), new XAttribute(r + "id", "{" + name + "}"), new XAttribute(w + "fontKey", key))));
        }
        if (placed.Count == 0)
            return 0;
        // The faces after each font's other properties, in the schema's order.
        foreach (var font in placed.Select(p => p.Font).Distinct())
        {
            var embeds = font.Elements().Where(e => e.Name.LocalName.StartsWith("embed", StringComparison.Ordinal)).Concat(placed.Where(p => p.Font == font).Select(p => p.Embed)).ToList();
            foreach (var embed in embeds.Where(e => e.Parent != null))
                embed.Remove();
            font.Add(embeds.OrderBy(e => Array.IndexOf(["embedRegular", "embedBold", "embedItalic", "embedBoldItalic"], e.Name.LocalName)));
        }
        if (tablePart != null)
        {
            // The ids the faces' relationships from the font table got, in its XML.
            var text = Added(table.ToString(SaveOptions.DisableFormatting), parts, changes, tablePart);
            changes[tablePart] = Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" + text);
        }
        else
            Added("", [new NewPart("fontTable", "/word/fontTable.xml", FontTableType, RelationshipTypes + "fontTable", Bytes(table)), .. parts], changes, context.MainPart);
        // Word's Embed fonts in the file: w:embedTrueTypeFonts, after the settings before it in the schema.
        if (SettingsPart() is { } settingsPart && Xml(Current(settingsPart, changes)!) is { Root: { } settings } settingsXml && settings.Element(w + "embedTrueTypeFonts") == null)
        {
            string[] before = ["writeProtection", "view", "zoom", "removePersonalInformation", "removeDateAndTime", "doNotDisplayPageBoundaries",
                "displayBackgroundShape", "printPostScriptOverText", "printFractionalCharacterWidth", "printFormsData"];
            var embed = new XElement(w + "embedTrueTypeFonts");
            if (settings.Elements().LastOrDefault(e => before.Contains(e.Name.LocalName)) is { } after)
                after.AddAfterSelf(embed);
            else
                settings.AddFirst(embed);
            changes[settingsPart] = Bytes(settingsXml);
        }
        ChangeParts(changes);
        return placed.Count;
    }
}
