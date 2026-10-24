using System.Linq.Expressions;
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

    /// <summary>
    /// East Asian emphasis marks (w:em: dot, comma, circle, underDot), two lines in one (w:eastAsianLayout w:combine) and
    /// text set across the line (w:eastAsianLayout w:vert: horizontal in vertical text).
    /// </summary>
    public string? Emphasis, CombineBrackets;
    public bool? CombineLines, AcrossLine;

    /// <summary>The tracked change the run belongs to and its tracked formatting change, in the markup view (catalog: review.revisions).</summary>
    public RevisionMark? Revision;
    public FormatChange? Change;

    /// <summary>
    /// Whether the run is tracked deleted text outside the markup view: hidden, but autofit tables measure it
    /// (TableLayout, ParagraphLayout.Widths).
    /// </summary>
    public bool Deleted;

    /// <summary>w:color: hex RGB or "auto", with its theme color and tint or shade.</summary>
    public string? Color, ThemeColor, ThemeTint, ThemeShade;

    /// <summary>w:u: the underline style and its color.</summary>
    public string? Underline, UnderlineColor;

    /// <summary>w:bdr: the border around the run's text (catalog: run.decoration).</summary>
    public BorderLine? Border;

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

    /// <summary>The width the run's text is fit to (w:fitText, here in points) and the id that joins runs fit together.</summary>
    public double? FitText;
    public string? FitTextId;

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

    /// <summary>The run the format is a w:r's, as Word tells its runs apart: its w:rPr as written, w:rsidRPr and w:rsidR.</summary>
    public (string Properties, string? PropertiesRevision, string? Revision)? Source;

    public RunFormat Clone() => (RunFormat)MemberwiseClone();

    /// <summary>
    /// What a run's format was resolved from: its own properties, character style and table style level, and the
    /// instance resolved (a format changed after it, a clone, is not this one), to resolve it again under another
    /// paragraph style; null for formats not of a run.
    /// </summary>
    internal (RunFormat Direct, string? CharacterStyle, RunFormat? Table, RunFormat Resolved)? Origin { get; set; }

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
                {
                    // An empty font name names nothing: the slot keeps the font it had (LibreOffice's
                    // extras-ooxmlexport/font-name-is-empty: w:ascii="" prints in the style's Calibri).
                    string? Named(string name) => e.Attr(name) is { Length: > 0 } font && !string.IsNullOrWhiteSpace(font) ? font : null;
                    Ascii = Named("ascii") ?? Ascii;
                    HAnsi = Named("hAnsi") ?? HAnsi;
                    EastAsia = Named("eastAsia") ?? EastAsia;
                    ComplexScript = Named("cs") ?? ComplexScript;
                    // A font named directly replaces a theme font of the same slot set at a lower level.
                    AsciiTheme = e.Attr("asciiTheme") ?? (Named("ascii") != null ? null : AsciiTheme);
                    HAnsiTheme = e.Attr("hAnsiTheme") ?? (Named("hAnsi") != null ? null : HAnsiTheme);
                    EastAsiaTheme = e.Attr("eastAsiaTheme") ?? (Named("eastAsia") != null ? null : EastAsiaTheme);
                    ComplexScriptTheme = e.Attr("cstheme") ?? (Named("cs") != null ? null : ComplexScriptTheme);
                    Hint = e.Attr("hint") ?? Hint;
                    break;
                }
                case "sz": Size = e.HalfPoints("val") ?? Size; break;
                case "szCs": SizeComplex = e.HalfPoints("val") ?? SizeComplex; break;
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
                    Color = Wml.HexColor(e.Val()) ?? Color;
                    ThemeColor = e.Attr("themeColor");
                    ThemeTint = e.Attr("themeTint");
                    ThemeShade = e.Attr("themeShade");
                    break;
                case "u":
                    Underline = e.Val() ?? Underline;
                    UnderlineColor = Wml.HexColor(e.Attr("color")) ?? UnderlineColor;
                    break;
                case "highlight": Highlight = e.Val(); break;
                case "bdr": Border = BorderLine.Read(e); break;
                case "shd":
                    ShadingPattern = e.Val();
                    ShadingColor = Wml.HexColor(e.Attr("color"));
                    ShadingFill = Wml.HexColor(e.Attr("fill"));
                    break;
                case "vertAlign": VerticalAlign = e.Val(); break;
                case "em": Emphasis = e.Val(); break;
                case "eastAsianLayout":
                    CombineLines = e.Attr("combine") is { } combine ? combine is "1" or "true" or "on" : CombineLines;
                    CombineBrackets = e.Attr("combineBrackets") ?? CombineBrackets;
                    AcrossLine = e.Attr("vert") is { } across ? across is "1" or "true" or "on" : AcrossLine;
                    break;
                case "position": Position = e.HalfPoints("val") ?? Position; break;
                case "spacing": Spacing = e.Twips("val") ?? Spacing; break;
                case "kern": Kern = e.HalfPoints("val") ?? Kern; break;
                // A scale of none or less Word ignores (docx/external/libreoffice/extras-ooxmlexport/tdf106001:
                // w:w 0 prints at 100 %).
                case "w": Scale = e.Number("val") is > 0 and var scale ? scale : Scale; break;
                case "fitText":
                    FitText = e.Twips("val") ?? FitText;
                    FitTextId = e.Attr("id") ?? FitTextId;
                    break;
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

    static readonly Action<RunFormat, RunFormat> Overlaid = Overlays.Of<RunFormat>();

    /// <summary>The properties <paramref name="top"/> sets laid over this format.</summary>
    public void Overlay(RunFormat top)
    {
        Overlaid(this, top);
        // A color replaces the one below whole, its theme color, tint and shade (docx/external/powertools/HW010-*:
        // accent1 shaded 7F over defaults of text1 tinted A6 prints 234824, not tinted too).
        if (top.Color != null || top.ThemeColor != null)
            (ThemeColor, ThemeTint, ThemeShade) = (top.ThemeColor, top.ThemeTint, top.ThemeShade);
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

    /// <summary>w:textDirection of the paragraph (tbRl, btLr, …), which frames turn their text by.</summary>
    public string? TextDirection;

    /// <summary>Indents in points (w:ind): left/start, right/end, first line (positive) or hanging (negative).</summary>
    public double? Left, Right, FirstLine;

    /// <summary>Indents in hundredths of a character (w:leftChars, w:rightChars, w:firstLineChars; a hanging one negative).</summary>
    public double? LeftChars, RightChars, FirstLineChars;

    /// <summary>
    /// The indents in characters (see <see cref="LeftChars"/>) in points, over those in twips: a first line or hanging
    /// indent in characters of the paragraph's size, a left or right one in characters of the Normal style's, the left
    /// one where the first line begins, so the left edge lies the hanging indent further in; a hanging indent in
    /// characters makes the left one count in characters too; 0 leaves the twips (measured with docx/local/chu-15,
    /// chu-14: of 22 pt text under 10.5 pt Normal leftChars 200 puts the first line 21 pt in and the rest the style's
    /// hanging of 72 pt further, hangingChars 100 hangs 22 pt from a left of 0, firstLineChars 200 indents 44 pt,
    /// leftChars 0 and firstLineChars 0 keep the twips; docx/external/libreoffice/extras-ooxmlexport/tdf167721_chUnits).
    /// </summary>
    public void ApplyCharacterUnits(double normalSize, double ownSize)
    {
        if (FirstLineChars is { } first && first != 0)
            FirstLine = first / 100 * ownSize;
        if (LeftChars is { } left && left != 0 || FirstLineChars is < 0)
            Left = (LeftChars ?? 0) / 100 * normalSize + Math.Max(0, -(FirstLine ?? 0));
        if (RightChars is { } right && right != 0)
            Right = right / 100 * normalSize;
    }

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
                case "textDirection": TextDirection = e.Val() ?? TextDirection; break;
                case "ind":
                    Left = e.Twips("start") ?? e.Twips("left") ?? Left;
                    Right = e.Twips("end") ?? e.Twips("right") ?? Right;
                    if (e.Twips("hanging") is { } hanging)
                        FirstLine = -hanging;
                    else if (e.Twips("firstLine") is { } first)
                        FirstLine = first;
                    LeftChars = e.Number("startChars") ?? e.Number("leftChars") ?? LeftChars;
                    RightChars = e.Number("endChars") ?? e.Number("rightChars") ?? RightChars;
                    if (e.Number("hangingChars") is { } hangingChars)
                        FirstLineChars = -hangingChars;
                    else if (e.Number("firstLineChars") is { } firstChars)
                        FirstLineChars = firstChars;
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
                    // A negative line is its size: exactly under auto, at least or no rule, at least under exact
                    // (measured with docx/local/nls-auto, -none, -least, -exact: -360 lines 18 pt apart, exact ones with
                    // their baseline at 0.8 of the line; docx/external/libreoffice/extras-ooxmlexport/
                    // tdf125469_singleSpacing, extras-ooxmlimport/tdf108682).
                    if (Line < 0 && e.Number("line") != null)
                        (Line, LineRule) = (-Line, LineRule == "exact" ? "atLeast" : "exact");
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
                // A w:framePr without any attribute Word knows makes no frame (measured with docx/local/fe-5, fg-7: one of
                // only fillcolor stands as a paragraph; docx/external/libreoffice/core-ooxml-pass/tdf133457).
                case "framePr" when e.Attributes().Any(a => a.Name.Namespace == Wml.W): Frame = e; break;
                case "outlineLvl": OutlineLevel = e.Number("val") is { } outline ? (int)outline : OutlineLevel; break;
                case "rPr": MarkProperties = e; break;
            }
        }
    }

    static readonly Action<ParagraphFormat, ParagraphFormat> Overlaid = Overlays.Of<ParagraphFormat>(nameof(Tabs), nameof(ClearedTabs));

    /// <summary>The properties <paramref name="top"/> sets laid over this format; tab stops accumulate.</summary>
    public void Overlay(ParagraphFormat top)
    {
        // Tab stops in order already stay as they are where the format laid over sets none.
        var sorted = true;
        for (var k = 1; k < (Tabs?.Count ?? 0); k++)
            sorted &= Tabs![k - 1].Position <= Tabs[k].Position;
        var tabs = top.Tabs is not { Count: > 0 } && top.ClearedTabs is not { Count: > 0 } && sorted ? null : Tabs?.ToList() ?? [];
        // Frame properties override those below them attribute by attribute (measured with docx/external/libreoffice/
        // extras-ooxmlexport/special_styles: a paragraph's w:framePr saying only w:wrap keeps its style's width, height
        // and centering on the page).
        // w:x and w:xAlign are one position, w:y and w:yAlign another: either replaces both (docx/local/framepos: a
        // paragraph's w:x of the page, 0 too, over its style's xAlign right of the margin, its w:y over yAlign bottom;
        // tdf112446_frameStyle).
        var frame = Frame != null && top.Frame != null ? new XElement(Frame) : null;
        foreach (var attribute in top.Frame?.Attributes() ?? [])
        {
            if (frame == null)
                break;
            var other = attribute.Name.LocalName switch { "x" => "xAlign", "xAlign" => "x", "y" => "yAlign", "yAlign" => "y", _ => null };
            if (other != null && attribute.Name.Namespace == Wml.W)
                frame.Attribute(Wml.W + other)?.Remove();
            frame.SetAttributeValue(attribute.Name, attribute.Value);
        }
        Overlaid(this, top);
        if (frame != null)
            Frame = frame;
        if (tabs == null)
        {
            if (Tabs is { Count: 0 })
                Tabs = null;
            return;
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

/// <summary>
/// Lays the public fields of one format that are set over those of another, as reflection does field by field (a null
/// reference or nullable value is skipped, a plain value always laid over), compiled once for a type: reflection takes
/// a fraction of a microsecond a field, and reading a document lays formats over one another for every run.
/// </summary>
static class Overlays
{
    public static Action<T, T> Of<T>(params string[] skipped)
    {
        var (target, top) = (Expression.Parameter(typeof(T), "target"), Expression.Parameter(typeof(T), "top"));
        var steps = new List<Expression>();
        foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (Array.IndexOf(skipped, field.Name) >= 0)
                continue;
            var value = Expression.Field(top, field);
            var assign = Expression.Assign(Expression.Field(target, field), value);
            steps.Add(Nullable.GetUnderlyingType(field.FieldType) != null ? Expression.IfThen(Expression.Property(value, "HasValue"), assign)
                : !field.FieldType.IsValueType ? Expression.IfThen(Expression.ReferenceNotEqual(value, Expression.Constant(null, field.FieldType)), assign)
                : assign);
        }
        return Expression.Lambda<Action<T, T>>(steps.Count > 0 ? Expression.Block(steps) : Expression.Empty(), target, top).Compile();
    }
}

/// <summary>
/// Formats compared by the values of their public fields, as Overlays reads them, compiled once for a type: lists by
/// their items, what else a format holds by reference (elements a style or a run states, arrays) as the reference.
/// </summary>
static class FormatValues<T> where T : class
{
    public static readonly Func<T, T, bool> Equal = CompileEqual();
    public static readonly Func<T, int> Hash = CompileHash();

    static Func<T, T, bool> CompileEqual()
    {
        var (a, b) = (Expression.Parameter(typeof(T), "a"), Expression.Parameter(typeof(T), "b"));
        Expression all = Expression.Constant(true);
        foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            Expression equal;
            if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                equal = Expression.Call(typeof(FormatValues<T>).GetMethod(nameof(ListEqual), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(field.FieldType.GetGenericArguments()), Expression.Field(a, field), Expression.Field(b, field));
            else
            {
                var comparer = typeof(EqualityComparer<>).MakeGenericType(field.FieldType);
                equal = Expression.Call(Expression.Property(null, comparer.GetProperty(nameof(EqualityComparer<object>.Default))!),
                    comparer.GetMethod(nameof(EqualityComparer<object>.Equals), [field.FieldType, field.FieldType])!, Expression.Field(a, field), Expression.Field(b, field));
            }
            all = Expression.AndAlso(all, equal);
        }
        return Expression.Lambda<Func<T, T, bool>>(all, a, b).Compile();
    }

    static bool ListEqual<TItem>(List<TItem>? a, List<TItem>? b) => a == null ? b == null : b != null && a.SequenceEqual(b);

    /// <summary>A list's items hashed, as ListEqual compares them: formats of lists alike in length (tab stops) would else share a hash.</summary>
    static int ListHash<TItem>(List<TItem>? list)
    {
        if (list == null)
            return -1;
        var hash = new HashCode();
        foreach (var item in list)
            hash.Add(item);
        return hash.ToHashCode();
    }

    static Func<T, int> CompileHash()
    {
        var a = Expression.Parameter(typeof(T), "a");
        var hash = Expression.Variable(typeof(HashCode), "hash");
        var steps = new List<Expression>();
        var add = typeof(HashCode).GetMethods().Single(m => m.Name == nameof(HashCode.Add) && m.GetParameters().Length == 1);
        foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance))
            steps.Add(field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>)
                ? Expression.Call(hash, add.MakeGenericMethod(typeof(int)), Expression.Call(typeof(FormatValues<T>).GetMethod(nameof(ListHash), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(field.FieldType.GetGenericArguments()), Expression.Field(a, field)))
                : Expression.Call(hash, add.MakeGenericMethod(field.FieldType), Expression.Field(a, field)));
        steps.Add(Expression.Call(hash, typeof(HashCode).GetMethod(nameof(HashCode.ToHashCode))!));
        return Expression.Lambda<Func<T, int>>(Expression.Block([hash], steps), a).Compile();
    }
}
