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
        Numbering = new NumberingDefinitions(Related("numbering"), Styles) { Part = Package.FirstRelationship(MainPart, "numbering")?.Target };
        FontTable = new FontTable(Related("fontTable"));
        Divs = new HtmlDivs(Related("webSettings"));
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

    /// <summary>The HTML divisions of the web settings (catalog: paragraph.htmlDivs).</summary>
    public HtmlDivs Divs { get; }

    /// <summary>The font table, which decides the fonts for families the machine lacks (catalog: font.table).</summary>
    public FontTable FontTable { get; }
    public FontLocator FontLocator { get; }
    public FontResolver Fonts { get; }

    /// <summary>
    /// The compatibility mode Word lays the document out in (w:compatSetting compatibilityMode: 11 Word 2003, 12 Word
    /// 2007, 14 Word 2010, 15 Word 2013 and later; catalog: settings.compatibility); a document without one opens as
    /// Word 2007's.
    /// </summary>
    public int CompatibilityMode => Settings.Child("compat")?.Elements(Wml.W + "compatSetting")
        .FirstOrDefault(e => e.Attr("name") == "compatibilityMode")?.Attr("val") is { } v && int.TryParse(v, out var mode) ? mode : 12;

    /// <summary>Whether one of the layout options of the compatibility settings (w:compat) is on.</summary>
    public bool Compat(string option) => Settings.Child("compat").Child(option).OnOff() == true;

    /// <summary>
    /// The space between two paragraphs (catalog: paragraph.spacing): the space after the first and what the space
    /// before the second has more than the first's own space after, as HTML collapses margins (where contextual
    /// spacing dropped the space after, the space before still gives only its excess: 20 pt before after a dropped
    /// 12 pt make 8 pt, measured with docx/local/c2-html); both in full where the document says
    /// w:doNotUseHTMLParagraphAutoSpacing (catalog: settings.compatibility, measured with docx/local/compat15-* and
    /// c2-html).
    /// </summary>
    public double Between(double after, double before, double? ownAfter = null) =>
        AddsSpacing ? after + before : after + Math.Max(0, before - (ownAfter ?? after));

    /// <summary>
    /// The kinsoku characters of an East Asian language (catalog: paragraph.eastAsian): the document's own lists for it
    /// (w:noLineBreaksBefore, w:noLineBreaksAfter with its w:lang) where it has them, else Word's (measured with
    /// docx/local/kinsoku-chars*: 154 candidates in Japanese, Simplified and Traditional Chinese and Korean, each
    /// where it would begin and where it would end a line). Japanese takes the standard characters, small kana and
    /// the prolonged sound mark too where the document says w:strictFirstAndLastChars; Korean has none; other
    /// languages take the Japanese ones. The characters that may hang past the end were measured in compatibility
    /// mode 12 and in justified paragraphs (kinsoku-chars12-*, kinsoku-chars15-*-both).
    /// </summary>
    public Text.LineBreaker.Kinsoku Kinsoku(string? language)
    {
        var tag = (language ?? "").ToLowerInvariant();
        var family = tag.StartsWith("ko", StringComparison.Ordinal) ? "ko"
            : tag is "zh-tw" or "zh-hk" or "zh-mo" || tag.StartsWith("zh-hant", StringComparison.Ordinal) ? "zh-tw"
            : tag.StartsWith("zh", StringComparison.Ordinal) ? "zh-cn" : "ja";
        if (kinsoku.TryGetValue(family, out var known))
            return known;
        var (notFirst, notLast, hanging) = family switch
        {
            "ko" => ("", "", ""),
            "zh-cn" => ("!%),.:;?]}\u00A2\u00B0\u2019\u201D\u2030\u2032\u2033\u2103\u3001\u3002\u3009\u300B\u300D\u300F\u3011\u3015\uFF01\uFF05"
                        + "\uFF09\uFF0C\uFF0E\uFF1A\uFF1B\uFF1F\uFF3D\uFF5D\uFFE0\u2026\u3017\u203A\u00B7\uFF5E\u2016\u3003\u301E\uFE5A\uFE5C\uFF02"
                        + "\uFF07\uFF40\uFF5C\u00A8\u02C7\u02C9\u2015\u2236\uFE36\uFE3A\uFE3E\uFE40\uFE44\uFE5E",
                        "$([{\u00A3\u00A5\u2018\u201C\u3008\u300A\u300C\u300E\u3010\u3014\uFF04\uFF08\uFF3B\uFF5B\uFFE1\uFFE5\u3016\u301D\uFE59\uFE5B"
                        + "\uFE5D\uFF0E\u00B7",
                        "!%),.:;?]}\u00A2\u00B0\u2019\u201D\u2030\u2032\u2033\u2103\u3001\u3002\u3009\u300B\u300D\u300F\u3011\u3015\uFF01\uFF05"
                        + "\uFF09\uFF0C\uFF1A\uFF1B\uFF1F\uFF3D\uFF5D\uFFE0\u3017"),
            "zh-tw" => ("!),.:;?]}\u00A2\u2019\u201D\u2032\u3001\u3002\u3009\u300B\u300D\u300F\u3011\u3015\uFF01\uFF09\uFF0C\uFF0E\uFF1A"
                        + "\uFF1B\uFF1F\uFF3D\uFF5D\uFF64\u2026\u2025\u00B7\u2022\u301E\uFE5A\uFE5C\uFF5C\uFE50\uFE51\uFE52\uFE54\uFE55\uFE56"
                        + "\uFE57\uFE30\uFE31\uFE33\uFE34\uFE36\uFE38\uFE3A\uFE3C\uFE3E\uFE40\uFE42\uFE44\uFE5E\u2027",
                        "([{\u00A3\u00A5\u2018\u201C\u3008\u300A\u300C\u300E\u3010\u3014\uFF08\uFF5B\u301D\uFE59\uFE5B\uFE5D\u2035\uFE35\uFE37"
                        + "\uFE39\uFE3B\uFE3D\uFE3F\uFE41\uFE43",
                        "!),.:;?]}\u2019\u201D\u2032\u3001\u3002\u3009\u300B\u300D\u300F\u3011\u3015\uFF01\uFF09\uFF0C\uFF0E\uFF1A\uFF1B\uFF1F"
                        + "\uFF3D\uFF5D"),
            _ => ("!%),.:;?]}\u00A2\u00B0\u2019\u201D\u2030\u2032\u2033\u2103\u3001\u3002\u3005\u3009\u300B\u300D\u300F\u3011\u3015"
                  + "\u309B\u309C\u309D\u309E\u30FB\u30FD\u30FE\uFF01\uFF05\uFF09\uFF0C\uFF0E\uFF1A\uFF1B\uFF1F\uFF3D\uFF5D\uFF61\uFF63"
                  + "\uFF64\uFF65\uFF9E\uFF9F\uFFE0"
                  + (Settings.Child("strictFirstAndLastChars").OnOff() == true
                      ? "\u3041\u3043\u3045\u3047\u3049\u3063\u3083\u3085\u3087\u308E\u30A1\u30A3\u30A5\u30A7\u30A9\u30C3\u30E3\u30E5\u30E7"
                        + "\u30EE\u30F5\u30F6\u30FC\uFF67\uFF68\uFF69\uFF6A\uFF6B\uFF6C\uFF6D\uFF6E\uFF6F\uFF70"
                      : ""),
                  "$([\\{\u00A3\u00A5\u2018\u201C\u3008\u300A\u300C\u300E\u3010\u3014\uFF04\uFF08\uFF3B\uFF5B\uFF62\uFFE1\uFFE5",
                  ",.\u2019\u201D\u3001\u3002\u300D\u300F\u3011\uFF09\uFF0C\uFF0E\uFF3D\uFF5D\uFF61\uFF64"),
        };
        // The document's own lists replace Word's for their language.
        string? Own(string name) => Settings?.Elements(Wml.W + name).FirstOrDefault(e => Family(e.Attr("lang")) == family)?.Attr("val");
        static string Family(string? lang)
        {
            var t = (lang ?? "").ToLowerInvariant();
            return t.StartsWith("ko", StringComparison.Ordinal) ? "ko"
                : t is "zh-tw" or "zh-hk" or "zh-mo" || t.StartsWith("zh-hant", StringComparison.Ordinal) ? "zh-tw"
                : t.StartsWith("zh", StringComparison.Ordinal) ? "zh-cn" : t.StartsWith("ja", StringComparison.Ordinal) ? "ja" : "";
        }
        // A document's own lists replace both of Word's, a missing one by none (measured with docx/local/kinsoku-b and
        // kinsoku-custom*); Word looks the characters up as if the lists were in code point order, as it writes them
        // (lists in another order lose some characters there).
        var (before, after) = (Own("noLineBreaksBefore"), Own("noLineBreaksAfter"));
        if (before != null || after != null)
            (notFirst, notLast, hanging) = (before ?? "", after ?? "", string.Concat(hanging.Where(c => (before ?? "").Contains(c))));
        var rules = new Text.LineBreaker.Kinsoku(notFirst, notLast, hanging);
        kinsoku[family] = rules;
        return rules;
    }

    readonly Dictionary<string, Text.LineBreaker.Kinsoku> kinsoku = [];

    /// <summary>Whether paragraphs add their spaces after and before (w:doNotUseHTMLParagraphAutoSpacing) rather than overlap them.</summary>
    public bool AddsSpacing => addsSpacing ??= Compat("doNotUseHTMLParagraphAutoSpacing");
    bool? addsSpacing;

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
