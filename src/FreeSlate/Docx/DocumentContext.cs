using System.Globalization;
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

    readonly System.Runtime.CompilerServices.ConditionalWeakTable<XDocument, Dictionary<XElement, long>> stacking = new();

    /// <summary>
    /// The stacking order of a floating drawing (wp:anchor/@relativeHeight) as Word takes it, through its part in
    /// document order: a height of 0 or 1, or of 0x1E000000 and more, counts as unset and stands above every drawing
    /// before it, at 0x0F000000 at least; the others as they are, equal ones the later on top (measured with
    /// docx/local/zo-a to zo-g, modes 12 and 15: 0x05000000, 0, 0x06000000 put the unset one on top; 0x10000000, 0,
    /// 0x0F000005 the unset one above 0x10000000; 0 above 0x0EFFFFFF; 0x1E000000 below 0x1DFFFFFE; LibreOffice's
    /// extras-ooxmlexport/tdf159158_zOrder_*), also in parts with VML shapes; those keep their z-index, but where one of
    /// them has 0 or two shapes of the part stand at the same height, all of them stand above all DrawingML drawings (fits
    /// all of docx/local/zo-a to zo-x, found by agent BB: a z-index of 5 under 0x0F000000 alone, over it beside another
    /// shape of 5 or after an unset height; z-index 0 over 0x0F000000; tdf159158_zOrder_zIndexWins). Shapes of VML in
    /// the fallback of markup compatibility, which shows the DrawingML choice, do not count.
    /// </summary>
    public long StackingOrder(XElement anchor)
    {
        static long? Height(XElement element) => long.TryParse((string?)element.Attribute("relativeHeight"), out var height) ? height : null;
        var shape = anchor.Annotation<Vml.Origin>()?.Shape;
        if ((shape ?? anchor).Document is not { } document)
            return Height(anchor) ?? 0;
        var orders = stacking.GetValue(document, part =>
        {
            var result = new Dictionary<XElement, long>();
            var highest = 0L;
            foreach (var element in part.Descendants(Docx.Drawings.Wp + "anchor"))
            {
                var height = Height(element) ?? 0;
                if (height is 0 or 1 || height >= 0x1E000000)
                    height = Math.Max(highest + 1, 0x0F000000);
                result[element] = height;
                highest = Math.Max(highest, height);
            }
            XNamespace v = "urn:schemas-microsoft-com:vml";
            XNamespace mc = "http://schemas.openxmlformats.org/markup-compatibility/2006";
            static long? Z(XElement e) => ((string?)e.Attribute("style"))?.Split(';').Select(s => s.Split(':', 2)).FirstOrDefault(kv => kv.Length == 2 && kv[0].Trim() == "z-index") is { } kv
                && long.TryParse(kv[1].Trim(), out var z) ? z : null;
            var shapes = part.Descendants().Where(e => e.Name.Namespace == v && e.Parent?.Name.Namespace == Wml.W && Z(e) != null
                && !e.Ancestors(mc + "Fallback").Any()).ToList();
            var values = shapes.Select(e => Z(e)!.Value).Concat(result.Values).ToList();
            var above = shapes.Count > 0 && result.Count > 0 && (shapes.Any(e => Z(e) == 0) || values.Distinct().Count() < values.Count);
            foreach (var e in shapes)
                result[e] = (above ? 1L << 40 : 0) + Math.Abs(Z(e)!.Value);
            return result;
        });
        return orders.TryGetValue(shape ?? anchor, out var order) ? order : Height(anchor) ?? 0;
    }

    /// <summary>
    /// A damaged document with several w:body elements shows them all, one after the other, under the last one's
    /// section properties (measured with docx/external/poi/MultipleBodyBug).
    /// </summary>
    static void JoinBodies(XDocument document)
    {
        if (document.Root?.Elements(Wml.W + "body").ToList() is not { Count: > 1 } bodies)
            return;
        var first = bodies[0];
        var section = first.Elements(Wml.W + "sectPr").LastOrDefault();
        section?.Remove();
        foreach (var extra in bodies.Skip(1))
        {
            if (extra.Elements(Wml.W + "sectPr").LastOrDefault() is { } own)
            {
                own.Remove();
                section = own;
            }
            first.Add(extra.Elements());
            extra.Remove();
        }
        if (section != null)
            first.Add(section);
    }

    public DocumentContext(OpcPackage package, FontLocator? fonts = null)
    {
        Package = package;
        MainPart = package.FirstRelationship("/", "officeDocument")?.Target
                   ?? throw new InvalidDataException(package.Exists("/mimetype")
                       ? "The package is an OpenDocument file (it has a mimetype part), not an Office Open XML document."
                       : "No main document part (officeDocument relationship) in the package.");
        // The settings and the theme first: the parts loaded after them take their shading's theme colors (Load).
        Settings = Related("settings")?.Root;
        Theme = Related("theme") is { } theme ? Theme.Load(theme) : Theme.WordDefault();
        Document = Load(MainPart);
        JoinBodies(Document);
        // Styles linked to the template without one attached update from Word's Normal template (StyleSheet).
        Styles = new StyleSheet(Related("styles"), Settings.Child("linkStyles").OnOff() == true && Settings.Child("attachedTemplate") == null,
            Related("stylesWithEffects"));
        Styles.TableStyleOverridesNormal = TableStyleOverridesNormal();
        FontLocator = (fonts ?? FontLocator.Default).With(EmbeddedFonts());
        Fonts = ReferenceEquals(FontLocator, FontLocator.Default) ? FontResolver.Default : new FontResolver(FontLocator);
        // Word keeps the default tab stop in 16 bits: 65536 twips are none, 66256 half an inch (measured with
        // docx/local/dts-*; docx/external/libreoffice/extras-ooxmlexport/tdf168607_tabstopZero: 65536, tabs of no width).
        // A document without settings takes the default tab stop of Word's own Normal template, 1.25 cm in the German
        // Word the references come from (LibreOffice's extras-ooxmlimport/xml_space: 708 twips, 86.6 → 99.9 % emulated).
        DefaultTabStop = long.TryParse(Settings.Child("defaultTabStop").Val(), out var stop) ? (stop % 65536 + 65536) % 65536 / 20.0
            : Settings.Child("defaultTabStop").Twips("val") ?? (Settings == null ? 35.4 : 36);
        Numbering = new NumberingDefinitions(Related("numbering"), Styles) { Part = Package.FirstRelationship(MainPart, "numbering")?.Target };
        FontTable = new FontTable(Related("fontTable"));
        Divs = new HtmlDivs(Related("webSettings"));
    }

    public OpcPackage Package { get; }
    public string MainPart { get; }

    /// <summary>
    /// Whether the document is shown with its markup (catalog: review.revisions, review.comments): tracked changes
    /// marked in their authors' colors, comments in balloons beside the page; else as it reads with all changes
    /// accepted and without comments, as Word prints by default.
    /// </summary>
    public bool Markup { get; init; }

    /// <summary>What the markup view's balloons say, in the language of the culture.</summary>
    public Describe Describe => describe ??= new Describe(Culture);

    Describe? describe;

    /// <summary>The authors of tracked changes and comments, in the order they appear (their colors follow it).</summary>
    public List<string> Authors { get; } = [];

    /// <summary>An author's place among the document's authors.</summary>
    public int Author(string? name)
    {
        name ??= "";
        var index = Authors.IndexOf(name);
        if (index < 0)
        {
            Authors.Add(name);
            index = Authors.Count - 1;
        }
        return index;
    }

    /// <summary>
    /// The document's page background (w:background), or null; one without a color Word ignores, its VML fill too
    /// (docx/external/libreoffice/extras-ooxmlexport/tdf126533_noPageBitmap: a picture of the page not printed, as
    /// tdf126533_pageBitmap's with w:color is).
    /// </summary>
    public XElement? Background => Document?.Root?.Element(Wml.W + "background") is { } background
        && (background.Attr("color") != null || background.Attr("themeColor") != null) ? background : null;
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

    /// <summary>How Word lays the document out by its compatibility settings, read once.</summary>
    public Layout.LayoutProfile Profile => profile ??= new(this, CompatibilityMode);
    Layout.LayoutProfile? profile;

    /// <summary>Whether one of the layout options of the compatibility settings (w:compat) is on.</summary>
    public bool Compat(string option) => Settings.Child("compat").Child(option).OnOff() == true;

    /// <summary>
    /// Whether table styles' font sizes and justification win over Normal's (StyleSheet.TableStyleOverridesNormal): the
    /// compatibility settings read in order, compatibilityMode 15 and later switching
    /// overrideTableStyleFontSizeAndJustification on, each of its own entries setting it, the last one counting
    /// (measured with docx/local/tsq-*: 0 after mode 15 lets the table style win as mode 11 does, 0 before it not;
    /// 1, 1, 0 after mode 15 wins, 0, 0, 1 not; w:uiCompat97To2003 changes nothing; tso-A-11s1: 1 before mode 11
    /// stacks the styles; docx/external/libreoffice/extras-ooxmlexport/tdf118947_tableStyle).
    /// </summary>
    bool TableStyleOverridesNormal()
    {
        var stacked = false;
        foreach (var setting in Settings.Child("compat")?.Elements(Wml.W + "compatSetting") ?? [])
        {
            if (setting.Attr("name") == "compatibilityMode")
                stacked |= int.TryParse(setting.Attr("val"), out var mode) && mode >= 15;
            else if (setting.Attr("name") == "overrideTableStyleFontSizeAndJustification")
                stacked = setting.Attr("val") is "1" or "true" or "on";
        }
        return !stacked;
    }

    /// <summary>The value of a named setting of w:compat (w:compatSetting), or null.</summary>
    public string? CompatSetting(string name) =>
        Settings.Child("compat")?.Elements(Wml.W + "compatSetting").FirstOrDefault(e => e.Attr("name") == name)?.Attr("val");

    /// <summary>
    /// The space between two paragraphs (catalog: paragraph.spacing): the space after the first and what the space
    /// before the second has more than the first's own space after, as HTML collapses margins (where contextual
    /// spacing dropped the space after, the space before still gives only its excess: 20 pt before after a dropped
    /// 12 pt make 8 pt, measured with docx/local/c2-html); both in full where the document says
    /// w:doNotUseHTMLParagraphAutoSpacing (catalog: settings.compatibility, measured with docx/local/compat15-* and
    /// c2-html).
    /// </summary>
    public double Between(double after, double before, double? ownAfter = null) =>
        Profile.AddsSpacing ? after + before : after + Math.Max(0, before - (ownAfter ?? after));

    /// <summary>
    /// The kinsoku characters of an East Asian language (catalog: paragraph.eastAsian): the document's own lists for it
    /// (w:noLineBreaksBefore, w:noLineBreaksAfter with its w:lang) where it has them, else Word's (measured with
    /// docx/local/kinsoku-chars*: 154 candidates in Japanese, Simplified and Traditional Chinese and Korean, each
    /// where it would begin and where it would end a line). Japanese takes the standard characters, small kana and
    /// the prolonged sound mark too where the document says w:strictFirstAndLastChars; Korean has none; other languages
    /// and none have the Japanese rules for Latin punctuation alone (measured with docx/local/hgl-*, hgd-*, hgn-*: a 。
    /// after a full line of ideographs begins the next line in en-US, de-DE and without a language whatever the default
    /// East Asian font, in zh-CN and ja-JP the last ideograph goes down with it or the 。 hangs; hangcomma-h2: a Latin
    /// comma hangs without a language). The characters that may hang past the end were measured in compatibility
    /// mode 12 and in justified paragraphs (kinsoku-chars12-*, kinsoku-chars15-*-both).
    /// </summary>
    public Text.LineBreaker.Kinsoku Kinsoku(string? language)
    {
        var tag = (language ?? "").ToLowerInvariant();
        var family = tag.StartsWith("ko", StringComparison.Ordinal) ? "ko"
            : tag is "zh-tw" or "zh-hk" or "zh-mo" || tag.StartsWith("zh-hant", StringComparison.Ordinal) ? "zh-tw"
            : tag.StartsWith("zh", StringComparison.Ordinal) ? "zh-cn" : tag.StartsWith("ja", StringComparison.Ordinal) ? "ja" : "";
        if (kinsoku.TryGetValue(family, out var known))
            return known;
        if (family.Length == 0)
        {
            // Other languages and none keep the Japanese rules for Latin punctuation alone.
            var japanese = Kinsoku("ja");
            static string Latin(string characters) => string.Concat(characters.Where(c => c < '\u2E80'));
            return kinsoku[family] = new Text.LineBreaker.Kinsoku(Latin(japanese.NotFirst), Latin(japanese.NotLast), Latin(japanese.Hanging));
        }
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

    /// <summary>The regional format for dates the document shows (date blocks, fields), the system's by default.</summary>
    public System.Globalization.CultureInfo Culture { get; set; } = System.Globalization.CultureInfo.CurrentCulture;

    /// <summary>The time the document prints at, which DATE, TIME and PRINTDATE fields show; now where null.</summary>
    public DateTime? Printed { get; set; }

    /// <summary>The interval of the default tab stops in points (w:defaultTabStop, 720 twips when absent).</summary>
    public double DefaultTabStop { get; }

    /// <summary>
    /// The character decimal tab stops align on: the decimal separator of the regional settings, whatever w:decimalSymbol
    /// or the text's language say (measured with docx/local/dect-*: on a machine with "," "1,5" and "1,234.5" align on
    /// their commas, "1.5" and ".5" end at the stop, with w:decimalSymbol "." or "," or none, in en-US and de-DE text;
    /// docx/external/powertools/HC024, HC025).
    /// </summary>
    public char DecimalSymbol => Culture.NumberFormat.NumberDecimalSeparator is { Length: > 0 } separator ? separator[0] : '.';

    /// <summary>
    /// The character that parts the arguments of EQ fields: the list separator of the regional settings, ";" in German
    /// (measured with docx/local/eqf-15: a comma there is text).
    /// </summary>
    public char ListSeparator => Culture.TextInfo.ListSeparator is { Length: > 0 } separator ? separator[0] : ',';

    /// <summary>
    /// How much more 3D text the document draws as bodies, in characters of 12 pt: a 48 pt character takes 85 to 420 KB
    /// of markup (docx/local/text-3d, docx/external/libreoffice/writerfilter-dmapper/fdo78333-1-minimized), so a page
    /// of it would not fit in memory (fdo78333: 2900 characters in a text box); the rest is drawn flat.
    /// </summary>
    public double Text3DBudget { get; set; } = 400;

    /// <summary>The glyph outlines the document uses, defined once.</summary>
    public Html.GlyphCache Glyphs { get; } = new();

    /// <summary>Font families the output names (the browser draws their text where outlines are not used).</summary>
    public HashSet<string> UsedFonts { get; } = new(StringComparer.OrdinalIgnoreCase);

    public XDocument Load(string part)
    {
        if (documents.TryGetValue(part, out var document))
            return document;
        document = Package.ReadXml(part);
        MarkupCompatibility.Process(document, chartEx: true);
        if (Theme != null)
            ThemeShading(document);
        documents[part] = document;
        return document;
    }

    /// <summary>
    /// Shading that names a theme color (w:shd's w:themeFill, w:themeColor) takes it, tinted or shaded, whatever
    /// w:fill and w:color say, as text colors do (catalog: color.models; docx/external/libreoffice/
    /// extras-ooxmlexport/n780853: cells of accent2 and accent1 print C0504D and 4F81BD where w:fill says DD8047 and
    /// 94B6D2 of another theme).
    /// </summary>
    void ThemeShading(XDocument document)
    {
        var w = Wml.W;
        foreach (var shd in document.Descendants(w + "shd"))
        {
            if ((string?)shd.Attribute(w + "themeFill") is { } fill && SchemeColor(fill) is { } fillColor)
                shd.SetAttributeValue(w + "fill", Hex(Tinted(fillColor, (string?)shd.Attribute(w + "themeFillTint"), (string?)shd.Attribute(w + "themeFillShade"))));
            if ((string?)shd.Attribute(w + "themeColor") is { } pattern && SchemeColor(pattern) is { } patternColor)
                shd.SetAttributeValue(w + "color", Hex(Tinted(patternColor, (string?)shd.Attribute(w + "themeTint"), (string?)shd.Attribute(w + "themeShade"))));
        }
        static string Hex(RgbColor color) => color.ToHex().TrimStart('#').ToUpperInvariant();
    }

    /// <summary>A theme color by Word's name of it, through the settings' color scheme mapping (w:clrSchemeMapping), or null.</summary>
    public RgbColor? SchemeColor(string name)
    {
        var mapping = Settings.Child("clrSchemeMapping");
        var scheme = name switch
        {
            "text1" or "dark1" => mapping.Attr("t1") ?? "dark1",
            "background1" or "light1" => mapping.Attr("bg1") ?? "light1",
            "text2" or "dark2" => mapping.Attr("t2") ?? "dark2",
            "background2" or "light2" => mapping.Attr("bg2") ?? "light2",
            _ => name,
        };
        var key = scheme switch
        {
            "dark1" => "dk1",
            "light1" => "lt1",
            "dark2" => "dk2",
            "light2" => "lt2",
            "hyperlink" => "hlink",
            "followedHyperlink" => "folHlink",
            _ => scheme,
        };
        return Theme.Colors.TryGetValue(key, out var color) ? color : null;
    }

    /// <summary>
    /// A theme color with Word's w:themeTint or w:themeShade (two hex digits) applied to its HSL lightness, its
    /// channels cut to whole steps of 255 (measured: docx/local/thc-plain, accent1 156082 shaded BF prints 0F4761 and
    /// tinted 99 45B0E1, not 104862 and 46B0E1; powertools/HC001: text2 tinted E6 404040).
    /// </summary>
    public static RgbColor Tinted(RgbColor color, string? tint, string? shade)
    {
        static double? Share(string? value) =>
            value is { Length: 2 } && int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) ? v / 255.0 : null;
        if (Share(shade) == null && Share(tint) == null)
            return color;
        var (h, sat, l) = color.ToHsl();
        if (Share(shade) is { } dark)
            l *= dark;
        if (Share(tint) is { } light)
            l = l * light + 1 - light;
        var c = RgbColor.FromHsl(h, sat, l, color.A);
        static double Cut(double v) => Math.Floor(Math.Clamp(v, 0, 1) * 255 + 1e-6) / 255;
        return new RgbColor(Cut(c.R), Cut(c.G), Cut(c.B), c.A);
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
                // A subset that lacks characters of the text set in it is not used at all (LibreOffice's
                // writerfilter-dmapper/subsetted-embedded-font: a subset of 11 characters, "MASTER SERVICES AGREEMENT"
                // printed in the substitute; subsetted-full-embedded-font: 755 characters, used).
                if (embed.Attr("subsetted") is "1" or "true" or "on" && !Covers(Deobfuscate(Package.ReadBytes(part), key), family))
                    continue;
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

    /// <summary>Whether a font has every character of the runs of the main part that name it directly.</summary>
    bool Covers(byte[] font, string family)
    {
        FontData data;
        try
        {
            data = new FontData(font, family, family);
        }
        catch (Exception)
        {
            return false;
        }
        foreach (var run in Document.Descendants(Wml.W + "r"))
        {
            if (run.Child("rPr")?.Child("rFonts") is not { } fonts || fonts.Attr("ascii") != family && fonts.Attr("hAnsi") != family)
                continue;
            foreach (var text in run.Elements(Wml.W + "t"))
            {
                foreach (var rune in text.Value.EnumerateRunes())
                {
                    if (!System.Text.Rune.IsWhiteSpace(rune) && !data.HasGlyph(rune.Value))
                        return false;
                }
            }
        }
        return true;
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
