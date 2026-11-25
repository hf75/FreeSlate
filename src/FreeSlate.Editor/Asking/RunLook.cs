using System.Globalization;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// Direct character formatting as the notation says it (see Notation.Inline, Prompt): what Word's Font dialog and Home tab give a stretch
/// of text besides its meaning (its character style): bold and italic, an underline of Word's kinds in a colour of its own, struck through
/// once or twice, in capitals or small capitals, as a superscript or subscript, a font, a size, a colour, a highlight, a shading, the
/// characters spaced out or together, raised or lowered, scaled, kerned, hidden, the language it is checked in, or not checked at all.
/// Compact where Pandoc and Markdown have marks (==marked==, ~~struck~~, H~2~O, x^2^), else as words before the text ("{font=Georgia
/// size=14 color=#C00000|text}", a character style's id first where it has one). Written as Word writes it (measured in Word 365 by COM,
/// each property given a range by the Font dialog's and the buttons' properties, the range's WordprocessingML read, scratchpad
/// gaps/C/charprobe.ps1: a font as w:rFonts ascii and hAnsi, a size as w:sz alone, a colour as w:color, an underline's colour on its w:u, a
/// shading as w:shd clear on a fill), and read back from a run's w:rPr as written (see Of), so that what Claude reads and writes back stays
/// as it was; a run with properties of other kinds (Word's text effects, a border, East Asian layout) reads as the WordprocessingML it is.
/// </summary>
public sealed partial record RunLook
{
    public bool? Bold { get; init; }
    public bool? Italic { get; init; }

    /// <summary>The underline by Word's name (w:u: single, words, double, thick, dotted, dash, wave …), none for none; and its colour (RRGGBB).</summary>
    public string? Underline { get; init; }
    public string? UnderlineColor { get; init; }

    /// <summary>Struck through: single (w:strike), double (w:dstrike) or none.</summary>
    public string? Strike { get; init; }

    public bool? Caps { get; init; }
    public bool? SmallCaps { get; init; }

    /// <summary>Raised and smaller or lowered and smaller (w:vertAlign): superscript, subscript, or baseline.</summary>
    public string? Script { get; init; }

    /// <summary>The font by its name, or the theme's (+body, +headings).</summary>
    public string? Font { get; init; }

    /// <summary>The size in points (half points, as Word sizes text).</summary>
    public double? Size { get; init; }

    /// <summary>The colour (RRGGBB) or auto (black on light, white on dark).</summary>
    public string? Color { get; init; }

    /// <summary>The highlight by Word's name (w:highlight: yellow, green, cyan, magenta, blue, red, darkBlue …), none for none.</summary>
    public string? Highlight { get; init; }

    /// <summary>A shading behind the text (w:shd's fill, RRGGBB).</summary>
    public string? Shade { get; init; }

    /// <summary>The characters spaced out (points, below 0 together), raised (points, below 0 lowered), scaled (percent), kerned from a size on (points).</summary>
    public double? Spacing { get; init; }
    public double? Raise { get; init; }
    public int? Scale { get; init; }
    public double? Kern { get; init; }

    public bool? Hidden { get; init; }

    /// <summary>The language the text is checked in (w:lang, a tag as en-US), and whether it is checked at all (false: w:noProof).</summary>
    public string? Language { get; init; }
    public bool? Proof { get; init; }

    /// <summary>A look of nothing of its own.</summary>
    public static readonly RunLook None = new();

    public bool Empty => Equals(None);

    /// <summary>This look with another laid over it: what the other gives in place of this one's.</summary>
    public RunLook Over(RunLook? top) => top == null ? this : new RunLook
    {
        Bold = top.Bold ?? Bold, Italic = top.Italic ?? Italic, Underline = top.Underline ?? Underline, UnderlineColor = top.Underline != null ? top.UnderlineColor : UnderlineColor,
        Strike = top.Strike ?? Strike, Caps = top.Caps ?? Caps, SmallCaps = top.SmallCaps ?? SmallCaps, Script = top.Script ?? Script, Font = top.Font ?? Font,
        Size = top.Size ?? Size, Color = top.Color ?? Color, Highlight = top.Highlight ?? Highlight, Shade = top.Shade ?? Shade, Spacing = top.Spacing ?? Spacing,
        Raise = top.Raise ?? Raise, Scale = top.Scale ?? Scale, Kern = top.Kern ?? Kern, Hidden = top.Hidden ?? Hidden, Language = top.Language ?? Language,
        Proof = top.Proof ?? Proof,
    };

    /// <summary>The look of the compact marks: ==marked== (Word's yellow highlight), ~~struck~~, ^superscript^, ~subscript~.</summary>
    public static readonly RunLook Marked = new() { Highlight = "yellow" }, Struck = new() { Strike = "single" },
        Superscript = new() { Script = "superscript" }, Subscript = new() { Script = "subscript" };

    /// <summary>Word's highlight colours (ST_HighlightColor).</summary>
    static readonly string[] Highlights =
        ["yellow", "green", "cyan", "magenta", "blue", "red", "darkBlue", "darkCyan", "darkGreen", "darkMagenta", "darkRed", "darkYellow", "darkGray", "lightGray", "black", "white", "none"];

    /// <summary>Word's underlines (ST_Underline).</summary>
    static readonly string[] Underlines =
    [
        "single", "words", "double", "thick", "dotted", "dottedHeavy", "dash", "dashedHeavy", "dashLong", "dashLongHeavy", "dotDash", "dashDotHeavy",
        "dotDotDash", "dashDotDotHeavy", "wave", "wavyHeavy", "wavyDouble", "none",
    ];

    /// <summary>Word's standard colours by their names (the row under the theme's in its colour menus), and black, white and gray.</summary>
    static readonly Dictionary<string, string> Colors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["darkred"] = "C00000", ["red"] = "FF0000", ["orange"] = "FFC000", ["yellow"] = "FFFF00", ["lightgreen"] = "92D050", ["green"] = "00B050",
        ["lightblue"] = "00B0F0", ["blue"] = "0070C0", ["darkblue"] = "002060", ["purple"] = "7030A0", ["black"] = "000000", ["white"] = "FFFFFF",
        ["gray"] = "808080", ["grey"] = "808080",
    };

    [GeneratedRegex(@"\G\s*([A-Za-z][A-Za-z0-9_-]*)(?:\s*=\s*(""[^""]*""|'[^']*'|[^\s""']+))?\s*")]
    private static partial Regex Setting();

    /// <summary>
    /// Settings as words (see Words): a name alone, or a name and a value, the value quoted where it holds spaces; null where the text is
    /// none.
    /// </summary>
    public static List<(string Name, string? Value)>? Settings(string text)
    {
        var words = new List<(string, string?)>();
        var at = 0;
        while (at < text.Length)
        {
            if (Setting().Match(text, at) is not { Success: true, Length: > 0 } word)
                return null;
            words.Add((word.Groups[1].Value, word.Groups[2].Success ? word.Groups[2].Value.Trim('"', '\'') : null));
            at += word.Length;
        }
        return words.Count > 0 ? words : null;
    }

    /// <summary>
    /// The words before a stretch of text read ("{Term color=#C00000|…}"): a character style's id first where one is given (a word alone,
    /// before settings), and the look; null where they are not such words (a style's id alone reads as the style's, see Notation).
    /// </summary>
    public static (string? Style, RunLook Look)? Parse(string head)
    {
        if (Settings(head) is not { } words)
            return null;
        string? style = null;
        var look = None;
        for (var k = 0; k < words.Count; k++)
        {
            var (name, value) = words[k];
            if (look.With(name, value) is { } given)
                look = given;
            else if (k == 0 && value == null && words.Count > 1)
                style = name;
            else
                return null;
        }
        return (style, look);
    }

    /// <summary>This look given a setting of the notation; null where it is no setting, or its value none it takes.</summary>
    RunLook? With(string name, string? value)
    {
        bool? Flag() => value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "yes" or "true" or "on" => true,
            "no" or "false" or "off" or "none" => false,
            _ => null,
        };
        double? Points() => Lengths.Points(value);
        switch (name.ToLowerInvariant())
        {
            case "bold" or "b":
                return Flag() is { } bold ? this with { Bold = bold } : null;
            case "italic" or "i":
                return Flag() is { } italic ? this with { Italic = italic } : null;
            case "underline" or "u":
            {
                if (value == null)
                    return this with { Underline = "single", UnderlineColor = null };
                var (kind, color) = ("single", (string?)null);
                foreach (var part in value.Split(',', ':', ';').Select(p => p.Trim()).Where(p => p.Length > 0))
                {
                    if (Underlines.FirstOrDefault(u => u.Equals(part, StringComparison.OrdinalIgnoreCase)) is { } known)
                        kind = known;
                    else if (part.Equals("no", StringComparison.OrdinalIgnoreCase) || part.Equals("false", StringComparison.OrdinalIgnoreCase))
                        kind = "none";
                    else if (Hex(part) is { } hex)
                        color = hex;
                    else
                        return null;
                }
                return this with { Underline = kind, UnderlineColor = kind == "none" ? null : color };
            }
            case "strike" or "strikethrough" or "s":
                return value?.Trim().ToLowerInvariant() switch
                {
                    "double" => this with { Strike = "double" },
                    "single" => this with { Strike = "single" },
                    _ => Flag() is { } strike ? this with { Strike = strike ? "single" : "none" } : null,
                };
            case "dstrike":
                return Flag() is { } twice ? this with { Strike = twice ? "double" : "none" } : null;
            case "caps" or "allcaps":
                return Flag() is { } caps ? this with { Caps = caps } : null;
            case "smallcaps":
                return Flag() is { } small ? this with { SmallCaps = small } : null;
            case "super" or "superscript" or "sup":
                return Flag() is { } up ? this with { Script = up ? "superscript" : "baseline" } : null;
            case "sub" or "subscript":
                return Flag() is { } down ? this with { Script = down ? "subscript" : "baseline" } : null;
            case "font":
                return value is { Length: > 0 } font && font.Trim().Length > 0 ? this with { Font = font.Trim() } : null;
            case "size":
                return Points() is > 0 and <= 1638 and var size ? this with { Size = Math.Round(size * 2) / 2 } : null;
            case "color" or "colour":
                return value?.Trim().ToLowerInvariant() == "auto" ? this with { Color = "auto" } : Colored(value) is { } colored ? this with { Color = colored } : null;
            case "highlight":
                return Highlights.FirstOrDefault(h => h.Equals(value?.Trim(), StringComparison.OrdinalIgnoreCase)) is { } highlight ? this with { Highlight = highlight }
                    : value == null ? this with { Highlight = "yellow" } : null;
            case "shade" or "shading":
                return TableWords.FillHex(value, out var named) is { } fill && named ? this with { Shade = fill } : null;
            case "spacing":
                return Lengths.Signed(value) is { } spacing && Math.Abs(spacing) <= 1584 ? this with { Spacing = spacing } : null;
            case "raise" or "position":
                return Lengths.Signed(value) is { } raise && Math.Abs(raise) <= 1584 ? this with { Raise = raise } : null;
            case "scale":
                return int.TryParse(value?.Trim().TrimEnd('%'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var scale) && scale is >= 1 and <= 600 ? this with { Scale = scale } : null;
            case "kern" or "kerning":
                return Points() is >= 0 and <= 1638 and var kern ? this with { Kern = kern } : null;
            case "hidden":
                return Flag() is { } hidden ? this with { Hidden = hidden } : null;
            case "lang" or "language":
                return value is { } tag && LanguageTag().IsMatch(tag.Trim()) ? this with { Language = tag.Trim() } : null;
            case "proof" or "check":
                return Flag() is { } proof ? this with { Proof = proof } : null;
            default:
                return null;
        }
    }

    [GeneratedRegex(@"^[A-Za-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$")]
    private static partial Regex LanguageTag();

    /// <summary>A colour as six hex digits: #RRGGBB, RRGGBB, #RGB or one of Word's standard colours by name; null for none.</summary>
    static string? Colored(string? given) => given != null && Colors.TryGetValue(given.Trim(), out var named) ? named : Hex(given);

    static string? Hex(string? given)
    {
        if (string.IsNullOrWhiteSpace(given))
            return null;
        var hex = given.Trim().TrimStart('#');
        if (hex.Length == 3 && hex.All(Uri.IsHexDigit))
            hex = string.Concat(hex.Select(c => $"{c}{c}"));
        return hex.Length == 6 && hex.All(Uri.IsHexDigit) ? hex.ToUpperInvariant() : null;
    }

    static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>The look as the notation's words, in an order of their own (see With), empty for none.</summary>
    public string Words()
    {
        var words = new List<string>();
        void Flag(string name, bool? value)
        {
            if (value is { } on)
                words.Add(on ? name : name + "=no");
        }
        Flag("bold", Bold);
        Flag("italic", Italic);
        if (Underline is { } underline)
            words.Add(underline == "single" && UnderlineColor == null ? "underline" : "underline=" + underline + (UnderlineColor is { } color ? ",#" + color : ""));
        if (Strike is { } strike)
            words.Add(strike switch { "single" => "strike", "double" => "strike=double", _ => "strike=no" });
        Flag("caps", Caps);
        Flag("smallcaps", SmallCaps);
        if (Script is { } script)
            words.Add(script switch { "superscript" => "super", "subscript" => "sub", _ => "super=no" });
        if (Font is { } font)
            words.Add("font=" + (font.Any(char.IsWhiteSpace) ? "\"" + font + "\"" : font));
        if (Size is { } size)
            words.Add("size=" + Number(size));
        if (Color is { } colored)
            words.Add("color=" + (colored == "auto" ? "auto" : "#" + colored));
        if (Highlight is { } highlight)
            words.Add("highlight=" + highlight);
        if (Shade is { } shade)
            words.Add("shade=#" + shade);
        if (Spacing is { } spacing)
            words.Add("spacing=" + Number(spacing));
        if (Raise is { } raise)
            words.Add("raise=" + Number(raise));
        if (Scale is { } scale)
            words.Add("scale=" + scale.ToString(CultureInfo.InvariantCulture));
        if (Kern is { } kern)
            words.Add("kern=" + Number(kern));
        Flag("hidden", Hidden);
        if (Language is { } language)
            words.Add("lang=" + language);
        if (Proof is { } proof)
            words.Add(proof ? "proof" : "proof=no");
        return string.Join(" ", words);
    }

    /// <summary>
    /// The look's run properties as Word writes them (see the class), in w:rPr's order (CT_RPr), without the character style: for after a
    /// w:rStyle.
    /// </summary>
    public string Xml()
    {
        var markup = new StringBuilder();
        void Toggle(string name, bool? value)
        {
            if (value is { } on)
                markup.Append(on ? $"<w:{name}/>" : $"<w:{name} w:val=\"0\"/>");
        }
        static string HalfPoints(double points) => ((int)Math.Round(points * 2)).ToString(CultureInfo.InvariantCulture);
        if (Font is { } font)
            markup.Append(font.ToLowerInvariant() switch
            {
                "+body" => "<w:rFonts w:asciiTheme=\"minorHAnsi\" w:hAnsiTheme=\"minorHAnsi\"/>",
                "+headings" => "<w:rFonts w:asciiTheme=\"majorHAnsi\" w:hAnsiTheme=\"majorHAnsi\"/>",
                _ => $"<w:rFonts w:ascii=\"{Escape(font)}\" w:hAnsi=\"{Escape(font)}\"/>",
            });
        Toggle("b", Bold);
        Toggle("i", Italic);
        Toggle("caps", Caps);
        Toggle("smallCaps", SmallCaps);
        switch (Strike)
        {
            case "single":
                markup.Append("<w:strike/>");
                break;
            case "double":
                markup.Append("<w:dstrike/>");
                break;
            case "none":
                markup.Append("<w:strike w:val=\"0\"/><w:dstrike w:val=\"0\"/>");
                break;
        }
        if (Proof is { } proof)
            markup.Append(proof ? "<w:noProof w:val=\"0\"/>" : "<w:noProof/>");
        Toggle("vanish", Hidden);
        if (Color is { } color)
            markup.Append($"<w:color w:val=\"{color}\"/>");
        if (Spacing is { } spacing)
            markup.Append($"<w:spacing w:val=\"{((int)Math.Round(spacing * 20)).ToString(CultureInfo.InvariantCulture)}\"/>");
        if (Scale is { } scale)
            markup.Append($"<w:w w:val=\"{scale.ToString(CultureInfo.InvariantCulture)}\"/>");
        if (Kern is { } kern)
            markup.Append($"<w:kern w:val=\"{HalfPoints(kern)}\"/>");
        if (Raise is { } raise)
            markup.Append($"<w:position w:val=\"{HalfPoints(raise)}\"/>");
        if (Size is { } size)
            markup.Append($"<w:sz w:val=\"{HalfPoints(size)}\"/>");
        if (Highlight is { } highlight)
            markup.Append($"<w:highlight w:val=\"{highlight}\"/>");
        if (Underline is { } underline)
            markup.Append($"<w:u w:val=\"{underline}\"").Append(UnderlineColor is { } underlineColor ? $" w:color=\"{underlineColor}\"" : "").Append("/>");
        if (Shade is { } shade)
            markup.Append($"<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"{shade}\"/>");
        if (Script is { } script)
            markup.Append($"<w:vertAlign w:val=\"{script}\"/>");
        if (Language is { } language)
            markup.Append($"<w:lang w:val=\"{Escape(language)}\"/>");
        return markup.ToString();
    }

    static string Escape(string text) => SecurityElement.Escape(text) ?? "";

    /// <summary>The run properties that go without saying: a character style's (said otherwise), the complex scripts' twins, the grid, hidden in a web view, a field's own vanishing, an old animation, Office Math's.</summary>
    static readonly HashSet<string> Unsaid = ["rStyle", "bCs", "iCs", "szCs", "snapToGrid", "webHidden", "specVanish", "effect", "oMath"];

    /// <summary>
    /// The look a run's properties as written give (its w:rPr, see WordDocument.RunProperties), and whether they hold what the notation
    /// does not say (Word's text effects, a border, East Asian layout, right to left, a formatting change tracked …).
    /// </summary>
    public static (RunLook Look, bool Other) Of(string properties)
    {
        if (properties.Length == 0)
            return (None, false);
        XElement rPr;
        try
        {
            rPr = XElement.Parse(properties);
        }
        catch (XmlException)
        {
            return (None, true);
        }
        var look = None;
        var other = false;
        foreach (var e in rPr.Elements())
        {
            if (!e.Name.NamespaceName.EndsWith("/wordprocessingml/2006/main", StringComparison.Ordinal) && e.Name.NamespaceName != "http://purl.oclc.org/ooxml/wordprocessingml/main")
            {
                other = true;
                continue;
            }
            string? Val(string name = "val") => (string?)e.Attributes().FirstOrDefault(a => a.Name.LocalName == name);
            bool On() => Val() is not ("0" or "false" or "off");
            double? Half() => double.TryParse(Val(), NumberStyles.Float, CultureInfo.InvariantCulture, out var half) ? half / 2 : null;
            if (Unsaid.Contains(e.Name.LocalName))
                continue;
            switch (e.Name.LocalName)
            {
                case "rFonts":
                {
                    var theme = Val("asciiTheme") ?? Val("hAnsiTheme");
                    if ((Val("ascii") ?? Val("hAnsi")) is { Length: > 0 } font)
                        look = look with { Font = font };
                    else if (theme != null)
                        look = look with { Font = theme.StartsWith("major", StringComparison.Ordinal) ? "+headings" : "+body" };
                    break;
                }
                case "b":
                    look = look with { Bold = On() };
                    break;
                case "i":
                    look = look with { Italic = On() };
                    break;
                case "caps":
                    look = look with { Caps = On() };
                    break;
                case "smallCaps":
                    look = look with { SmallCaps = On() };
                    break;
                case "strike":
                    look = look with { Strike = On() ? "single" : look.Strike ?? "none" };
                    break;
                case "dstrike":
                    look = look with { Strike = On() ? "double" : look.Strike ?? "none" };
                    break;
                case "noProof":
                    look = look with { Proof = !On() };
                    break;
                case "vanish":
                    look = look with { Hidden = On() };
                    break;
                case "color":
                    if (Val() is "auto")
                        look = look with { Color = "auto" };
                    else if (Hex(Val()) is { } color)
                        look = look with { Color = color };
                    else
                        other = true;
                    break;
                case "spacing":
                    if (double.TryParse(Val(), NumberStyles.Float, CultureInfo.InvariantCulture, out var twips))
                        look = look with { Spacing = twips / 20 };
                    break;
                case "w":
                    if (int.TryParse(Val(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var scale) && scale > 0)
                        look = look with { Scale = scale };
                    break;
                case "kern":
                    look = look with { Kern = Half() };
                    break;
                case "position":
                    look = look with { Raise = Half() };
                    break;
                case "sz":
                    look = look with { Size = Half() };
                    break;
                case "highlight":
                    if (Highlights.FirstOrDefault(h => h.Equals(Val(), StringComparison.OrdinalIgnoreCase)) is { } highlight)
                        look = look with { Highlight = highlight };
                    else
                        other = true;
                    break;
                case "u":
                    if (Underlines.FirstOrDefault(u => u.Equals(Val(), StringComparison.OrdinalIgnoreCase)) is { } underline)
                        look = look with { Underline = underline, UnderlineColor = Hex(Val("color")) };
                    else
                        other = true;
                    break;
                case "shd":
                    // A clear shading on a fill, as the Home tab's shading gives it; none where its fill is automatic, other patterns as they are.
                    if (Val() is "clear" or "nil" && Hex(Val("fill")) is { } fill)
                        look = look with { Shade = fill };
                    else if (Val() is not ("clear" or "nil"))
                        other = true;
                    break;
                case "vertAlign":
                    look = look with { Script = Val() is "superscript" or "subscript" ? Val() : "baseline" };
                    break;
                case "lang":
                    if (Val() is { Length: > 0 } language)
                        look = look with { Language = language };
                    break;
                default:
                    other = true;
                    break;
            }
        }
        return (look, other);
    }
}

/// <summary>Lengths as the notation gives them for formatting: points unless a unit says otherwise (cm, mm, in, pt).</summary>
static class Lengths
{
    /// <summary>A length in points, not below 0; null where none is given or it reads as none.</summary>
    public static double? Points(string? given) => Signed(given) is >= 0 and var points ? points : null;

    /// <summary>A length in points, below 0 too.</summary>
    public static double? Signed(string? given)
    {
        if (string.IsNullOrWhiteSpace(given))
            return null;
        var text = given.Trim().ToLowerInvariant().Replace(',', '.');
        var unit = new string([.. text.SkipWhile(c => char.IsDigit(c) || c is '.' or '-' or '+')]).Trim();
        var number = text[..(text.Length - unit.Length)].Trim();
        if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || double.IsNaN(value) || double.IsInfinity(value))
            return null;
        return unit switch
        {
            "cm" => value * 72 / 2.54,
            "mm" => value * 72 / 25.4,
            "in" or "\"" => value * 72,
            "pt" or "" => value,
            _ => null,
        };
    }

    /// <summary>A length as the notation writes it: points ("12pt").</summary>
    public static string Pt(double points) => points.ToString("0.##", CultureInfo.InvariantCulture) + "pt";

    /// <summary>A length as the notation writes it: centimetres ("1.25cm").</summary>
    public static string Cm(double points) => (points / 72 * 2.54).ToString("0.##", CultureInfo.InvariantCulture) + "cm";
}
