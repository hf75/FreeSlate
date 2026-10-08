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
        if (elements == null)
            return theme;

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

        var fonts = elements.Element(Ns.A + "fontScheme");
        string Typeface(string font, string script) =>
            (string?)fonts?.Element(Ns.A + font)?.Element(Ns.A + script)?.Attribute("typeface") ?? "";
        theme.MajorLatin = Typeface("majorFont", "latin");
        theme.MinorLatin = Typeface("minorFont", "latin");
        theme.MajorEastAsian = Typeface("majorFont", "ea");
        theme.MinorEastAsian = Typeface("minorFont", "ea");
        theme.MajorComplex = Typeface("majorFont", "cs");
        theme.MinorComplex = Typeface("minorFont", "cs");

        var formats = elements.Element(Ns.A + "fmtScheme");
        theme.FillStyles.AddRange(formats?.Element(Ns.A + "fillStyleLst")?.Elements() ?? []);
        theme.LineStyles.AddRange(formats?.Element(Ns.A + "lnStyleLst")?.Elements() ?? []);
        theme.EffectStyles.AddRange(formats?.Element(Ns.A + "effectStyleLst")?.Elements() ?? []);
        theme.BackgroundFillStyles.AddRange(formats?.Element(Ns.A + "bgFillStyleLst")?.Elements() ?? []);
        return theme;
    }

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
