using System.Runtime.CompilerServices;
using FreeSlate.Drawing;
using FreeSlate.Text;

namespace FreeSlate.Docx.Layout;

/// <summary>The font slots of w:rFonts (ISO/IEC 29500 17.3.2.26).</summary>
public enum WordSlot { Ascii, HAnsi, EastAsia, ComplexScript }

/// <summary>
/// Turns resolved character formatting into run styles for layout and drawing (catalog: run.basic, font.theme,
/// font.table): the font of each slot (theme fonts through the theme), the size, the color (theme colors through the
/// color scheme mapping) and the decorations.
/// </summary>
public sealed class RunStyles(DocumentContext context)
{
    readonly ConditionalWeakTable<RunFormat, Dictionary<(WordSlot, bool), RunStyle>> cache = new();

    /// <summary>The slot a character's font comes from: ASCII, East Asian, complex script or high ANSI.</summary>
    public static WordSlot SlotOf(int c, RunFormat format)
    {
        if (c < 0x80)
            return WordSlot.Ascii;
        if (format.UseComplexScript == true || format.RightToLeft == true)
            return WordSlot.ComplexScript;
        return Scripts.Of(c) switch
        {
            { Slot: FontSlot.EastAsian } => WordSlot.EastAsia,
            { Slot: FontSlot.Complex } => WordSlot.ComplexScript,
            _ => format.Hint == "eastAsia" && c is >= 0x2000 and <= 0x2BFF ? WordSlot.EastAsia : WordSlot.HAnsi,
        };
    }

    /// <param name="smallCapital">A lowercase letter of small caps text, drawn as a capital at the small caps size.</param>
    public RunStyle Style(RunFormat format, WordSlot slot, bool smallCapital = false)
    {
        var bySlot = cache.GetOrCreateValue(format);
        lock (bySlot)
        {
            if (!bySlot.TryGetValue((slot, smallCapital), out var style))
                bySlot[(slot, smallCapital)] = style = Create(format, slot, smallCapital);
            return style;
        }
    }

    /// <summary>The size of the small capitals of text this size: 80 % on whole half points (12 pt → 9.5 pt, measured).</summary>
    public static double SmallCapsSize(double size) => Math.Round(size * 0.8 * 2, MidpointRounding.AwayFromZero) / 2;

    RunStyle Create(RunFormat format, WordSlot slot, bool smallCapital)
    {
        var complex = slot == WordSlot.ComplexScript;
        var bold = (complex ? format.BoldComplex : format.Bold) == true;
        var italic = (complex ? format.ItalicComplex : format.Italic) == true;
        var size = (complex ? format.SizeComplex ?? format.Size : format.Size) ?? 10;
        var family = Family(format, slot);
        var font = context.Fonts.ResolveTypeface(family, bold, italic);
        var color = Color(format);
        var vertical = format.VerticalAlign;
        // Super- and subscript (catalog: run.decoration): 58 % of the size, raised by 33 % or lowered by 8 % of it
        // (measured with docx/generated/character-formats: 12 pt text has a 6.96 pt superscript 3.96 pt up and a
        // subscript 0.96 pt down; LibreOffice uses the same factors for Word's).
        var scriptSize = vertical is "superscript" or "subscript" ? size * 0.58 : size;
        if (smallCapital)
            scriptSize = SmallCapsSize(scriptSize);
        var raise = (format.Position ?? 0) + vertical switch
        {
            "superscript" => size * 0.33,
            "subscript" => -size * 0.08,
            _ => 0,
        };
        return new RunStyle(font, scriptSize, color, WordFonts.Features(format, size), bold, italic)
        {
            Underline = format.Underline is { } u && u != "none" ? u : null,
            UnderlineColor = format.UnderlineColor is { Length: 6 } uc ? RgbColor.FromHex(uc) : null,
            Strike = format.Strike == true,
            DoubleStrike = format.DoubleStrike == true,
            Hidden = format.Vanish == true || format.SpecVanish == true,
            Highlight = WordFonts.Highlight(format.Highlight),
            Shading = format.ShadingFill is { Length: 6 } fill && fill != "auto" ? RgbColor.FromHex(fill) : null,
            Raise = raise,
            Spacing = format.Spacing ?? 0,
            Scale = (format.Scale ?? 100) / 100,
            Caps = format.Caps == true,
            SmallCaps = format.SmallCaps == true,
            Effects = TextEffects.EffectList(format),
            Outline = TextEffects.Outline(format),
            TextFill = TextEffects.Fill(format),
            Language = slot switch
            {
                WordSlot.EastAsia => format.LanguageEastAsia ?? format.Language,
                WordSlot.ComplexScript => format.LanguageBidi ?? format.Language,
                _ => format.Language,
            },
        };
    }

    /// <summary>The family of a slot: the theme font it names (major or minor, by script), else its own name.</summary>
    string Family(RunFormat format, WordSlot slot)
    {
        var (name, theme) = slot switch
        {
            WordSlot.Ascii => (format.Ascii, format.AsciiTheme),
            WordSlot.HAnsi => (format.HAnsi ?? format.Ascii, format.HAnsiTheme ?? (format.HAnsi == null ? format.AsciiTheme : null)),
            WordSlot.EastAsia => (format.EastAsia, format.EastAsiaTheme),
            _ => (format.ComplexScript, format.ComplexScriptTheme),
        };
        if (theme != null)
        {
            var major = theme.StartsWith("major", StringComparison.Ordinal);
            var t = context.Theme;
            var themed = theme switch
            {
                _ when theme.EndsWith("EastAsia", StringComparison.Ordinal) => major ? t.MajorEastAsian : t.MinorEastAsian,
                _ when theme.EndsWith("Bidi", StringComparison.Ordinal) => major ? t.MajorComplex : t.MinorComplex,
                _ => major ? t.MajorLatin : t.MinorLatin,
            };
            if (!string.IsNullOrEmpty(themed))
                return themed;
        }
        return name ?? format.Ascii ?? "Times New Roman";
    }

    /// <summary>
    /// The text color: the theme color where w:color names one (through w:clrSchemeMapping), else w:val; "auto" is
    /// black (catalog: color.models; tints and shades of theme colors are taken from w:val, which Word writes with
    /// them applied).
    /// </summary>
    RgbColor Color(RunFormat format)
    {
        if (format.Color is { Length: 6 } hex && hex != "auto")
            return RgbColor.FromHex(hex);
        if (format.ThemeColor is { } themeColor && ThemeColor(themeColor) is { } color)
            return color;
        return RgbColor.Black;
    }

    RgbColor? ThemeColor(string name)
    {
        // Word's theme color names map to scheme colors through the settings' color scheme mapping.
        var mapping = context.Settings.Child("clrSchemeMapping");
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
        return context.Theme.Colors.TryGetValue(key, out var color) ? color : null;
    }
}
