using System.Xml.Linq;

namespace FreeSlate.Drawing;

/// <summary>The parts of a DrawingML theme the renderer needs (ECMA-376 Part 1, §20.1.6).</summary>
public sealed class Theme
{
    public Dictionary<string, RgbColor> Colors { get; } = [];
    public string MajorLatin { get; private set; } = "Calibri Light";
    public string MinorLatin { get; private set; } = "Calibri";
    public string MajorEastAsian { get; private set; } = "";
    public string MinorEastAsian { get; private set; } = "";
    public string MajorComplex { get; private set; } = "";
    public string MinorComplex { get; private set; } = "";
    public List<XElement> FillStyles { get; } = [];
    public List<XElement> LineStyles { get; } = [];
    public List<XElement> EffectStyles { get; } = [];
    public List<XElement> BackgroundFillStyles { get; } = [];

    /// <summary>
    /// The theme Word gives a document without a theme part, its own since 2023: Aptos Display and Aptos (measured with
    /// docx/local/lsk-1: theme fonts without a theme part show in Aptos), its colors and its format scheme as Word
    /// writes them into new documents (docx/external/libreoffice/extras-odfexport/tdf168980: a text box outlined in
    /// accent1 without a theme part prints 156082).
    /// </summary>
    public static Theme WordDefault()
    {
        var theme = new Theme { MajorLatin = "Aptos Display", MinorLatin = "Aptos" };
        theme.Read(XElement.Parse(WordDefaultElements), null);
        return theme;
    }

    const string WordDefaultElements = """
        <a:themeElements xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
        <a:clrScheme name="Office"><a:dk1><a:sysClr val="windowText" lastClr="000000"/></a:dk1><a:lt1><a:sysClr val="window" lastClr="FFFFFF"/></a:lt1>
        <a:dk2><a:srgbClr val="0E2841"/></a:dk2><a:lt2><a:srgbClr val="E8E8E8"/></a:lt2><a:accent1><a:srgbClr val="156082"/></a:accent1>
        <a:accent2><a:srgbClr val="E97132"/></a:accent2><a:accent3><a:srgbClr val="196B24"/></a:accent3><a:accent4><a:srgbClr val="0F9ED5"/></a:accent4>
        <a:accent5><a:srgbClr val="A02B93"/></a:accent5><a:accent6><a:srgbClr val="4EA72E"/></a:accent6><a:hlink><a:srgbClr val="467886"/></a:hlink>
        <a:folHlink><a:srgbClr val="96607D"/></a:folHlink></a:clrScheme>
        <a:fmtScheme name="Office"><a:fillStyleLst><a:solidFill><a:schemeClr val="phClr"/></a:solidFill>
        <a:gradFill rotWithShape="1"><a:gsLst><a:gs pos="0"><a:schemeClr val="phClr"><a:lumMod val="110000"/><a:satMod val="105000"/><a:tint val="67000"/></a:schemeClr></a:gs>
        <a:gs pos="50000"><a:schemeClr val="phClr"><a:lumMod val="105000"/><a:satMod val="103000"/><a:tint val="73000"/></a:schemeClr></a:gs>
        <a:gs pos="100000"><a:schemeClr val="phClr"><a:lumMod val="105000"/><a:satMod val="109000"/><a:tint val="81000"/></a:schemeClr></a:gs></a:gsLst><a:lin ang="5400000" scaled="0"/></a:gradFill>
        <a:gradFill rotWithShape="1"><a:gsLst><a:gs pos="0"><a:schemeClr val="phClr"><a:satMod val="103000"/><a:lumMod val="102000"/><a:tint val="94000"/></a:schemeClr></a:gs>
        <a:gs pos="50000"><a:schemeClr val="phClr"><a:satMod val="110000"/><a:lumMod val="100000"/><a:shade val="100000"/></a:schemeClr></a:gs>
        <a:gs pos="100000"><a:schemeClr val="phClr"><a:lumMod val="99000"/><a:satMod val="120000"/><a:shade val="78000"/></a:schemeClr></a:gs></a:gsLst><a:lin ang="5400000" scaled="0"/></a:gradFill></a:fillStyleLst>
        <a:lnStyleLst><a:ln w="12700" cap="flat" cmpd="sng" algn="ctr"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:prstDash val="solid"/><a:miter lim="800000"/></a:ln>
        <a:ln w="19050" cap="flat" cmpd="sng" algn="ctr"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:prstDash val="solid"/><a:miter lim="800000"/></a:ln>
        <a:ln w="25400" cap="flat" cmpd="sng" algn="ctr"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:prstDash val="solid"/><a:miter lim="800000"/></a:ln></a:lnStyleLst>
        <a:effectStyleLst><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst>
        <a:outerShdw blurRad="57150" dist="19050" dir="5400000" algn="ctr" rotWithShape="0"><a:srgbClr val="000000"><a:alpha val="63000"/></a:srgbClr></a:outerShdw></a:effectLst></a:effectStyle></a:effectStyleLst>
        <a:bgFillStyleLst><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:solidFill><a:schemeClr val="phClr"><a:tint val="95000"/><a:satMod val="170000"/></a:schemeClr></a:solidFill>
        <a:gradFill rotWithShape="1"><a:gsLst><a:gs pos="0"><a:schemeClr val="phClr"><a:tint val="93000"/><a:satMod val="150000"/><a:shade val="98000"/><a:lumMod val="102000"/></a:schemeClr></a:gs>
        <a:gs pos="50000"><a:schemeClr val="phClr"><a:tint val="98000"/><a:satMod val="130000"/><a:shade val="90000"/><a:lumMod val="103000"/></a:schemeClr></a:gs>
        <a:gs pos="100000"><a:schemeClr val="phClr"><a:shade val="63000"/><a:satMod val="120000"/></a:schemeClr></a:gs></a:gsLst><a:lin ang="5400000" scaled="0"/></a:gradFill></a:bgFillStyleLst></a:fmtScheme>
        </a:themeElements>
        """;

    /// <param name="part">The theme's part: pictures of its fill styles are relationships of it.</param>
    public static Theme Load(XDocument? document, string? part = null)
    {
        var theme = new Theme();
        var elements = document?.Root?.Element(Ns.A + "themeElements");
        if (elements != null)
            theme.Read(elements, part);
        return theme;
    }

    /// <summary>
    /// The part a style of the format scheme comes from (the theme's or a theme override's): the r:embed of a picture
    /// fill style (a textured background, bgFillStyleLst) is a relationship of that part, not of the slide.
    /// </summary>
    public static string? PartOf(XElement style) => style.Annotation<StylePart>()?.Path;

    sealed record StylePart(string Path);

    /// <summary>
    /// A copy with the parts of an a:themeOverride (catalog: theme.override): a layout or slide can replace the
    /// color, font or format scheme of its master's theme.
    /// </summary>
    public Theme WithOverride(XElement themeOverride, string? part = null)
    {
        var theme = new Theme
        {
            MajorLatin = MajorLatin, MinorLatin = MinorLatin, MajorEastAsian = MajorEastAsian, MinorEastAsian = MinorEastAsian,
            MajorComplex = MajorComplex, MinorComplex = MinorComplex,
        };
        foreach (var (key, value) in Colors) theme.Colors[key] = value;
        theme.FillStyles.AddRange(FillStyles);
        theme.LineStyles.AddRange(LineStyles);
        theme.EffectStyles.AddRange(EffectStyles);
        theme.BackgroundFillStyles.AddRange(BackgroundFillStyles);
        theme.Read(themeOverride, part);
        return theme;
    }

    /// <summary>Reads the schemes present in a:themeElements or a:themeOverride.</summary>
    void Read(XElement elements, string? part)
    {
        var theme = this;
        foreach (var entry in elements.Element(Ns.A + "clrScheme")?.Elements() ?? [])
        {
            var color = entry.Elements().FirstOrDefault();
            theme.Colors[entry.Name.LocalName] = color?.Name.LocalName switch
            {
                "srgbClr" => RgbColor.FromHex((string?)color.Attribute("val") ?? "000000"),
                "sysClr" => RgbColor.FromHex((string?)color.Attribute("lastClr") ?? "000000"),
                _ => RgbColor.Black,
            };
        }

        if (elements.Element(Ns.A + "fontScheme") is { } fonts)
        {
            string Typeface(string font, string script) =>
                (string?)fonts.Element(Ns.A + font)?.Element(Ns.A + script)?.Attribute("typeface") ?? "";
            theme.MajorLatin = Typeface("majorFont", "latin");
            theme.MinorLatin = Typeface("minorFont", "latin");
            theme.MajorEastAsian = Typeface("majorFont", "ea");
            theme.MinorEastAsian = Typeface("minorFont", "ea");
            theme.MajorComplex = Typeface("majorFont", "cs");
            theme.MinorComplex = Typeface("minorFont", "cs");
            theme.ScriptFonts.Clear();
            foreach (var kind in new[] { "majorFont", "minorFont" })
            {
                foreach (var font in fonts.Element(Ns.A + kind)?.Elements(Ns.A + "font") ?? [])
                {
                    if (font.Str("script") is { } script && font.Str("typeface") is { Length: > 0 } typeface)
                        theme.ScriptFonts[(kind == "majorFont", script)] = typeface;
                }
            }
        }

        if (elements.Element(Ns.A + "fmtScheme") is { } formats)
        {
            void Replace(List<XElement> list, string name)
            {
                if (formats.Element(Ns.A + name) is { } styles)
                {
                    list.Clear();
                    list.AddRange(styles.Elements());
                    foreach (var style in styles.Elements())
                    {
                        if (part != null && style.Annotation<StylePart>() == null)
                            style.AddAnnotation(new StylePart(part));
                    }
                }
            }
            Replace(theme.FillStyles, "fillStyleLst");
            Replace(theme.LineStyles, "lnStyleLst");
            Replace(theme.EffectStyles, "effectStyleLst");
            Replace(theme.BackgroundFillStyles, "bgFillStyleLst");
        }
    }

    /// <summary>Per-script fonts of the font scheme (a:font script="Jpan" …), keyed by (major, script).</summary>
    public Dictionary<(bool Major, string Script), string> ScriptFonts { get; } = [];

    /// <summary>Resolves theme font references such as "+mn-lt" or "+mj-ea".</summary>
    public string ResolveTypeface(string typeface) => typeface switch
    {
        "+mj-lt" => MajorLatin,
        "+mn-lt" => MinorLatin,
        "+mj-ea" => MajorEastAsian,
        "+mn-ea" => MinorEastAsian,
        "+mj-cs" => MajorComplex,
        "+mn-cs" => MinorComplex,
        _ => typeface,
    };

    /// <summary>A fill style by style-matrix index: 1–3 the fill styles, 1001 and above the background fills.</summary>
    public XElement? FillStyle(int index) =>
        index >= 1001 ? At(BackgroundFillStyles, index - 1000) : At(FillStyles, index);

    public XElement? LineStyle(int index) => At(LineStyles, index);

    static XElement? At(List<XElement> list, int index) => index >= 1 && index <= list.Count ? list[index - 1] : null;
}
