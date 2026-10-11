using System.Reflection;
using System.Xml.Linq;

namespace FreeSlate.Docx;

/// <summary>
/// Character formatting as one level of the style hierarchy states it (catalog: run.basic, run.decoration,
/// run.spacing, run.hidden): every property is null until a w:rPr sets it. <see cref="Apply"/> lays a w:rPr over it.
/// </summary>
public sealed class RunFormat
{
    // Fonts per slot (catalog: font.theme): ascii, hAnsi, eastAsia, cs, each a name or a theme font.
    public string? Ascii, HAnsi, EastAsia, ComplexScript;
    public string? AsciiTheme, HAnsiTheme, EastAsiaTheme, ComplexScriptTheme;
    public string? Hint;

    /// <summary>Font sizes in points (w:sz, w:szCs are half-points).</summary>
    public double? Size, SizeComplex;

    // Toggle properties (ISO/IEC 29500 17.7.3).
    public bool? Bold, BoldComplex, Italic, ItalicComplex, Caps, SmallCaps, Strike, Emboss, Imprint, Outline, Shadow, Vanish;

    public bool? DoubleStrike, SpecVanish, WebHidden, RightToLeft, UseComplexScript, NoProof;

    /// <summary>
    /// The direction of a w:bdo (an override: the run reads in this direction whatever its characters) and of a w:dir
    /// (an embedding) around the run: "rtl", "ltr" or null (catalog: run.complexScript).
    /// </summary>
    public string? Override, Embedding;

    /// <summary>East Asian emphasis marks (w:em: dot, comma, circle, underDot) and two lines in one (w:eastAsianLayout).</summary>
    public string? Emphasis, CombineBrackets;
    public bool? CombineLines;

    /// <summary>w:color: hex RGB or "auto", with its theme color and tint or shade.</summary>
    public string? Color, ThemeColor, ThemeTint, ThemeShade;

    /// <summary>w:u: the underline style and its color.</summary>
    public string? Underline, UnderlineColor;

    public string? Highlight;

    /// <summary>w:shd: pattern, pattern color and fill.</summary>
    public string? ShadingPattern, ShadingColor, ShadingFill;

    /// <summary>w:vertAlign: superscript, subscript or baseline.</summary>
    public string? VerticalAlign;

    /// <summary>Raised or lowered text (w:position, half-points) and character spacing (w:spacing, twips), in points.</summary>
    public double? Position, Spacing;

    /// <summary>Kerning from this font size on (w:kern, half-points), in points; 0 turns kerning off.</summary>
    public double? Kern;

    /// <summary>Horizontal scale in percent (w:w).</summary>
    public double? Scale;

    public string? Language, LanguageEastAsia, LanguageBidi;

    /// <summary>w14:ligatures (catalog: run.openType).</summary>
    public string? Ligatures;

    /// <summary>w14:numForm (lining, oldStyle) and w14:numSpacing (proportional, tabular); default leaves the font's.</summary>
    public string? NumberForm, NumberSpacing;

    /// <summary>The stylistic sets of w14:stylisticSets (1 to 20).</summary>
    public int[]? StylisticSets;

    /// <summary>w14:cntxtAlts: contextual alternates.</summary>
    public bool? ContextualAlternates;

    /// <summary>Text effects (catalog: run.textEffects): the w14 elements of glow, shadow, reflection, outline, fill and 3D.</summary>
    public XElement? Glow, TextShadow, Reflection, TextOutline, TextFill, Props3D, Scene3D;

    public string? Style;

    public RunFormat Clone() => (RunFormat)MemberwiseClone();

    /// <summary>Lays the properties a w:rPr sets over this format.</summary>
    public void Apply(XElement? rPr)
    {
        if (rPr == null)
            return;
        foreach (var e in rPr.Elements())
        {
            if (e.Name.Namespace == Wml.W14)
            {
                var value = (string?)e.Attribute(Wml.W14 + "val");
                switch (e.Name.LocalName)
                {
                    case "ligatures": Ligatures = value; break;
                    case "numForm": NumberForm = value; break;
                    case "numSpacing": NumberSpacing = value; break;
                    case "cntxtAlts": ContextualAlternates = value is null or "1" or "true" or "on"; break;
                    case "glow": Glow = e; break;
                    case "shadow": TextShadow = e; break;
                    case "reflection": Reflection = e; break;
                    case "textOutline": TextOutline = e; break;
                    case "textFill": TextFill = e; break;
                    case "props3d": Props3D = e; break;
                    case "scene3d": Scene3D = e; break;
                    case "stylisticSets":
                        StylisticSets = e.Elements(Wml.W14 + "styleSet")
                            .Where(s => (string?)s.Attribute(Wml.W14 + "val") is null or "1" or "true" or "on")
                            .Select(s => int.TryParse((string?)s.Attribute(Wml.W14 + "id"), out var id) ? id : 0)
                            .Where(id => id is >= 1 and <= 20).ToArray();
                        break;
                }
                continue;
            }
            if (e.Name.Namespace != Wml.W)
                continue;
            switch (e.Name.LocalName)
            {
                case "rStyle": Style = e.Val(); break;
                case "rFonts":
                    Ascii = e.Attr("ascii") ?? Ascii;
                    HAnsi = e.Attr("hAnsi") ?? HAnsi;
                    EastAsia = e.Attr("eastAsia") ?? EastAsia;
                    ComplexScript = e.Attr("cs") ?? ComplexScript;
                    // A font named directly replaces a theme font of the same slot set at a lower level.
                    AsciiTheme = e.Attr("asciiTheme") ?? (e.Attr("ascii") != null ? null : AsciiTheme);
                    HAnsiTheme = e.Attr("hAnsiTheme") ?? (e.Attr("hAnsi") != null ? null : HAnsiTheme);
                    EastAsiaTheme = e.Attr("eastAsiaTheme") ?? (e.Attr("eastAsia") != null ? null : EastAsiaTheme);
                    ComplexScriptTheme = e.Attr("cstheme") ?? (e.Attr("cs") != null ? null : ComplexScriptTheme);
                    Hint = e.Attr("hint") ?? Hint;
                    break;
                case "sz": Size = e.Number("val") is { } sz ? sz / 2 : Size; break;
                case "szCs": SizeComplex = e.Number("val") is { } szCs ? szCs / 2 : SizeComplex; break;
                case "b": Bold = e.OnOff(); break;
                case "bCs": BoldComplex = e.OnOff(); break;
                case "i": Italic = e.OnOff(); break;
                case "iCs": ItalicComplex = e.OnOff(); break;
                case "caps": Caps = e.OnOff(); break;
                case "smallCaps": SmallCaps = e.OnOff(); break;
                case "strike": Strike = e.OnOff(); break;
                case "dstrike": DoubleStrike = e.OnOff(); break;
                case "emboss": Emboss = e.OnOff(); break;
                case "imprint": Imprint = e.OnOff(); break;
                case "outline": Outline = e.OnOff(); break;
                case "shadow": Shadow = e.OnOff(); break;
                case "vanish": Vanish = e.OnOff(); break;
                case "specVanish": SpecVanish = e.OnOff(); break;
                case "webHidden": WebHidden = e.OnOff(); break;
                case "rtl": RightToLeft = e.OnOff(); break;
                case "cs": UseComplexScript = e.OnOff(); break;
                case "noProof": NoProof = e.OnOff(); break;
                case "color":
                    Color = e.Val() ?? Color;
                    ThemeColor = e.Attr("themeColor");
                    ThemeTint = e.Attr("themeTint");
                    ThemeShade = e.Attr("themeShade");
                    break;
                case "u":
                    Underline = e.Val() ?? Underline;
                    UnderlineColor = e.Attr("color") ?? UnderlineColor;
                    break;
                case "highlight": Highlight = e.Val(); break;
                case "shd":
                    ShadingPattern = e.Val();
                    ShadingColor = e.Attr("color");
                    ShadingFill = e.Attr("fill");
                    break;
                case "vertAlign": VerticalAlign = e.Val(); break;
                case "em": Emphasis = e.Val(); break;
                case "eastAsianLayout":
                    CombineLines = e.Attr("combine") is { } combine ? combine is "1" or "true" or "on" : CombineLines;
                    CombineBrackets = e.Attr("combineBrackets") ?? CombineBrackets;
                    break;
                case "position": Position = e.Number("val") is { } position ? position / 2 : Position; break;
                case "spacing": Spacing = e.Twips("val") ?? Spacing; break;
                case "kern": Kern = e.Number("val") is { } kern ? kern / 2 : Kern; break;
                case "w": Scale = e.Number("val") ?? Scale; break;
                case "lang":
                    Language = e.Val() ?? Language;
                    LanguageEastAsia = e.Attr("eastAsia") ?? LanguageEastAsia;
                    LanguageBidi = e.Attr("bidi") ?? LanguageBidi;
                    break;
            }
        }
    }

    /// <summary>The toggle properties of this level.</summary>
    public static readonly string[] Toggles = ["Bold", "BoldComplex", "Italic", "ItalicComplex", "Caps", "SmallCaps", "Strike", "Emboss", "Imprint", "Outline", "Shadow", "Vanish"];

    public bool? Toggle(string name) => name switch
    {
        "Bold" => Bold,
        "BoldComplex" => BoldComplex,
        "Italic" => Italic,
        "ItalicComplex" => ItalicComplex,
        "Caps" => Caps,
        "SmallCaps" => SmallCaps,
        "Strike" => Strike,
        "Emboss" => Emboss,
        "Imprint" => Imprint,
        "Outline" => Outline,
        "Shadow" => Shadow,
        "Vanish" => Vanish,
        _ => null,
    };

    public void SetToggle(string name, bool? value)
    {
        switch (name)
        {
            case "Bold": Bold = value; break;
            case "BoldComplex": BoldComplex = value; break;
            case "Italic": Italic = value; break;
            case "ItalicComplex": ItalicComplex = value; break;
            case "Caps": Caps = value; break;
            case "SmallCaps": SmallCaps = value; break;
            case "Strike": Strike = value; break;
            case "Emboss": Emboss = value; break;
            case "Imprint": Imprint = value; break;
            case "Outline": Outline = value; break;
            case "Shadow": Shadow = value; break;
            case "Vanish": Vanish = value; break;
        }
    }

    /// <summary>
    /// Combines the levels of the style hierarchy (ISO/IEC 29500 17.7.2 and 17.7.3): non-toggle properties take the last
    /// level that sets them; a toggle property is the direct formatting's value if it sets one, else true when the
    /// document defaults set it, else the XOR of the levels' values (each level resolved along its basedOn chain).
    /// </summary>
    public static RunFormat Combine(RunFormat defaults, IReadOnlyList<RunFormat> levels, RunFormat? direct)
    {
        var result = defaults.Clone();
        foreach (var level in levels)
            result.Overlay(level);
        if (direct != null)
            result.Overlay(direct);
        foreach (var name in Toggles)
        {
            bool? value;
            if (direct?.Toggle(name) is { } own)
                value = own;
            else if (defaults.Toggle(name) == true)
                value = true;
            else
                value = levels.Aggregate(false, (v, level) => v ^ (level.Toggle(name) ?? false));
            result.SetToggle(name, value);
        }
        return result;
    }

    static readonly FieldInfo[] Fields = typeof(RunFormat).GetFields(BindingFlags.Public | BindingFlags.Instance);

    /// <summary>The properties <paramref name="top"/> sets laid over this format.</summary>
    public void Overlay(RunFormat top)
    {
        foreach (var field in Fields)
        {
            if (field.GetValue(top) is { } value)
                field.SetValue(this, value);
        }
        // Theme fonts and named fonts of one slot replace each other.
        if (top.Ascii != null && top.AsciiTheme == null) AsciiTheme = null;
        if (top.HAnsi != null && top.HAnsiTheme == null) HAnsiTheme = null;
        if (top.EastAsia != null && top.EastAsiaTheme == null) EastAsiaTheme = null;
        if (top.ComplexScript != null && top.ComplexScriptTheme == null) ComplexScriptTheme = null;
    }
}

/// <summary>A tab stop: position from the paragraph's left edge (points), alignment and leader.</summary>
public sealed record TabStop(double Position, string Align, string Leader);

/// <summary>Paragraph formatting as one level of the style hierarchy states it; null until a w:pPr sets it.</summary>
public sealed class ParagraphFormat
{
    public string? Style;

    /// <summary>w:jc: left/start, center, right/end, both, distribute, …</summary>
    public string? Justification;

    /// <summary>Indents in points (w:ind): left/start, right/end, first line (positive) or hanging (negative).</summary>
    public double? Left, Right, FirstLine;

    /// <summary>Space before and after in points; their line-unit and automatic variants.</summary>
    public double? Before, After;
    public double? BeforeLines, AfterLines;
    public bool? BeforeAuto, AfterAuto;

    /// <summary>w:spacing/@line (twips, or 240ths of a line for auto) and @lineRule.</summary>
    public double? Line;
    public string? LineRule;

    public bool? ContextualSpacing, KeepNext, KeepLines, PageBreakBefore, WidowControl, SuppressAutoHyphens, Bidi,
        MirrorIndents, SnapToGrid, SuppressLineNumbers, WordWrap, Kinsoku, OverflowPunctuation, TopLinePunctuation,
        AutoSpaceDE, AutoSpaceDN;

    /// <summary>Tab stops this level adds and the positions it clears (w:tabs/w:tab w:val="clear").</summary>
    public List<TabStop>? Tabs;
    public List<double>? ClearedTabs;

    /// <summary>w:numPr: the numbering instance and level.</summary>
    public string? NumberingId;
    public int? NumberingLevel;

    public XElement? Borders, Shading, Frame;

    /// <summary>The HTML division the paragraph belongs to (w:divId, catalog: paragraph.htmlDivs).</summary>
    public string? DivId;

    /// <summary>How characters of different sizes align vertically in a line (w:textAlignment): top, center, baseline, bottom, auto.</summary>
    public string? TextAlignment;

    public int? OutlineLevel;

    /// <summary>The paragraph's own run properties for its mark (w:pPr/w:rPr).</summary>
    public XElement? MarkProperties;

    public ParagraphFormat Clone()
    {
        var copy = (ParagraphFormat)MemberwiseClone();
        copy.Tabs = Tabs?.ToList();
        copy.ClearedTabs = ClearedTabs?.ToList();
        return copy;
    }

    /// <summary>Lays the properties a w:pPr sets over this format.</summary>
    public void Apply(XElement? pPr)
    {
        if (pPr == null)
            return;
        foreach (var e in pPr.Elements())
        {
            if (e.Name.Namespace != Wml.W)
                continue;
            switch (e.Name.LocalName)
            {
                case "pStyle": Style = e.Val(); break;
                case "jc": Justification = e.Val(); break;
                case "ind":
                    Left = e.Twips("start") ?? e.Twips("left") ?? Left;
                    Right = e.Twips("end") ?? e.Twips("right") ?? Right;
                    if (e.Twips("hanging") is { } hanging)
                        FirstLine = -hanging;
                    else if (e.Twips("firstLine") is { } first)
                        FirstLine = first;
                    break;
                case "spacing":
                    Before = e.Twips("before") ?? Before;
                    After = e.Twips("after") ?? After;
                    BeforeLines = e.Number("beforeLines") ?? BeforeLines;
                    AfterLines = e.Number("afterLines") ?? AfterLines;
                    BeforeAuto = e.Attr("beforeAutospacing") is { } ba ? ba is "1" or "true" or "on" : BeforeAuto;
                    AfterAuto = e.Attr("afterAutospacing") is { } aa ? aa is "1" or "true" or "on" : AfterAuto;
                    Line = e.Number("line") ?? Line;
                    LineRule = e.Attr("lineRule") ?? (e.Attr("line") != null ? "auto" : LineRule);
                    break;
                case "contextualSpacing": ContextualSpacing = e.OnOff(); break;
                case "keepNext": KeepNext = e.OnOff(); break;
                case "keepLines": KeepLines = e.OnOff(); break;
                case "pageBreakBefore": PageBreakBefore = e.OnOff(); break;
                case "widowControl": WidowControl = e.OnOff(); break;
                case "suppressAutoHyphens": SuppressAutoHyphens = e.OnOff(); break;
                case "bidi": Bidi = e.OnOff(); break;
                case "mirrorIndents": MirrorIndents = e.OnOff(); break;
                case "snapToGrid": SnapToGrid = e.OnOff(); break;
                case "suppressLineNumbers": SuppressLineNumbers = e.OnOff(); break;
                case "divId": DivId = e.Val(); break;
                case "textAlignment": TextAlignment = e.Val(); break;
                case "wordWrap": WordWrap = e.OnOff(); break;
                case "kinsoku": Kinsoku = e.OnOff(); break;
                case "overflowPunct": OverflowPunctuation = e.OnOff(); break;
                case "topLinePunct": TopLinePunctuation = e.OnOff(); break;
                case "autoSpaceDE": AutoSpaceDE = e.OnOff(); break;
                case "autoSpaceDN": AutoSpaceDN = e.OnOff(); break;
                case "tabs":
                    foreach (var tab in e.Elements(Wml.W + "tab"))
                    {
                        var position = tab.Twips("pos") ?? 0;
                        var align = tab.Val() ?? "left";
                        if (align == "clear")
                        {
                            (ClearedTabs ??= []).Add(position);
                            Tabs?.RemoveAll(t => Math.Abs(t.Position - position) < 0.01);
                        }
                        else
                        {
                            Tabs ??= [];
                            Tabs.RemoveAll(t => Math.Abs(t.Position - position) < 0.01);
                            Tabs.Add(new TabStop(position, align, tab.Attr("leader") ?? "none"));
                        }
                    }
                    break;
                case "numPr":
                    NumberingId = e.Child("numId").Val() ?? NumberingId;
                    NumberingLevel = e.Child("ilvl").Number("val") is { } level ? (int)level : NumberingLevel;
                    break;
                case "pBdr": Borders = e; break;
                case "shd": Shading = e; break;
                case "framePr": Frame = e; break;
                case "outlineLvl": OutlineLevel = e.Number("val") is { } outline ? (int)outline : OutlineLevel; break;
                case "rPr": MarkProperties = e; break;
            }
        }
    }

    /// <summary>The properties <paramref name="top"/> sets laid over this format; tab stops accumulate.</summary>
    public void Overlay(ParagraphFormat top)
    {
        var tabs = Tabs?.ToList() ?? [];
        foreach (var field in typeof(ParagraphFormat).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.Name is nameof(Tabs) or nameof(ClearedTabs))
                continue;
            if (field.GetValue(top) is { } value)
                field.SetValue(this, value);
        }
        foreach (var cleared in top.ClearedTabs ?? [])
            tabs.RemoveAll(t => Math.Abs(t.Position - cleared) < 0.01);
        foreach (var tab in top.Tabs ?? [])
        {
            tabs.RemoveAll(t => Math.Abs(t.Position - tab.Position) < 0.01);
            tabs.Add(tab);
        }
        Tabs = tabs.Count > 0 ? tabs.OrderBy(t => t.Position).ToList() : null;
    }
}
