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

    public static Theme Load(XDocument? document)
    {
        var theme = new Theme();
        var elements = document?.Root?.Element(Ns.A + "themeElements");
        if (elements != null)
            theme.Read(elements);
        return theme;
    }

    /// <summary>
    /// A copy with the parts of an a:themeOverride (catalog: theme.override): a layout or slide can replace the
    /// color, font or format scheme of its master's theme.
    /// </summary>
    public Theme WithOverride(XElement themeOverride)
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
        theme.Read(themeOverride);
        return theme;
    }

    /// <summary>Reads the schemes present in a:themeElements or a:themeOverride.</summary>
    void Read(XElement elements)
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
