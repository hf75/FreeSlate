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
    readonly ConditionalWeakTable<RunFormat, Dictionary<(WordSlot, bool, bool), RunStyle>> cache = new();

    /// <summary>
    /// The slot a character's font comes from, in ECMA-376's two steps (17.3.2.26) as Word takes them (measured with
    /// docx/local/rtlslot-a to -m): by its block, Basic Latin and the right-to-left scripts (Hebrew, Arabic, Syriac,
    /// Thaana, the Arabic presentation forms) are ASCII, East Asian blocks East Asian (with the hint eastAsia also
    /// General Punctuation to Dingbats), all else high ANSI, Thai and the Indic scripts too (they fall back from the high
    /// ANSI font, never take the w:cs font); then w:cs and w:rtl make every character complex script but East Asian ones
    /// under the hint eastAsia, and in a w:rtl run (not w:cs) numbers keep their font, with complex script formatting
    /// (see <see cref="Character"/>): Arabic-Indic digits (bidirectional type AN) always, digits and number signs (EN,
    /// ET: % $) unless the run's complex script language is Hebrew (w:lang w:bidi he-IL: Aharoni, none or ar-SA: Times
    /// New Roman; rtlslot-m, -o, -p; docx/external/powertools/HC002-Hebrew-01). w:hint="cs" changes nothing (rtlslot-c, -n).
    /// </summary>
    public static WordSlot SlotOf(int c, RunFormat format) => SlotOf(c, format, format.RightToLeft == true);

    /// <param name="rightToLeft">Whether the character takes the rules of a w:rtl run.</param>
    static WordSlot SlotOf(int c, RunFormat format, bool rightToLeft)
    {
        var slot = c < 0x80 || c is >= 0x0590 and <= 0x07BF or >= 0xFB50 and <= 0xFDFF or >= 0xFE70 and <= 0xFEFE ? WordSlot.Ascii
            : Scripts.Of(c) is { Slot: FontSlot.EastAsian } || format.Hint == "eastAsia" && c is >= 0x2000 and <= 0x2BFF ? WordSlot.EastAsia
            : WordSlot.HAnsi;
        if (slot == WordSlot.EastAsia && format.Hint == "eastAsia")
            return slot;
        if (format.UseComplexScript == true || rightToLeft && !KeepsFont(c, format))
            return WordSlot.ComplexScript;
        return slot;
    }

    /// <summary>Whether a character of a w:rtl run keeps the font of its block (see <see cref="SlotOf"/>).</summary>
    static bool KeepsFont(int c, RunFormat format) => Bidi.Classify(c) switch
    {
        Bidi.Type.AN => true,
        Bidi.Type.EN or Bidi.Type.ET => format.LanguageBidi?.Split('-')[0].ToLowerInvariant() is not ("he" or "iw"),
        _ => false,
    };

    /// <summary>
    /// The style of a character of a run: the font of its slot (<see cref="SlotOf"/>), with complex script size, bold
    /// and italic where the slot is complex script and where a right-to-left run's number keeps its own font (Word sets
    /// the digits of a w:rtl run with w:szCs 20 pt in its ASCII font at 20 pt, bold by w:bCs: docx/local/rtlslot-e, -f).
    /// </summary>
    /// <param name="smallCapital">A lowercase letter of small caps text, drawn as a capital at the small caps size.</param>
    /// <param name="numberRightToLeft">
    /// For a character of a list number, whether its paragraph is right to left (w:bidi), which alone gives the number
    /// its direction: then all but its left-to-right letters take the rules of a w:rtl run (Word sets "1." beside Hebrew
    /// in the w:cs font, beside Arabic or without a language "1" in the ASCII font and "." in the w:cs font, "a." with
    /// "a" in the ASCII font; a left-to-right paragraph's number all in the ASCII font, though its mark is w:rtl:
    /// docx/local/rtlnum-a to -f). Null for the characters of runs, whose w:rtl counts.
    /// </param>
    public RunStyle Character(RunFormat format, int c, bool smallCapital = false, bool? numberRightToLeft = null)
    {
        var rightToLeft = numberRightToLeft is { } paragraph ? paragraph && Bidi.Classify(c) != Bidi.Type.L : format.RightToLeft == true;
        var slot = SlotOf(c, format, rightToLeft);
        var style = Style(format, slot, smallCapital, slot == WordSlot.ComplexScript || rightToLeft && slot != WordSlot.EastAsia);
        // A list number's complex script font that the machine lacks becomes Times New Roman, not what the font table
        // puts for it (Word draws the "-" of a level whose w:cs font is missing in Times New Roman, whether the table
        // gives it an altName, a character set or nothing, whatever the level's ASCII font: docx/local/rtlnum-k, -m to -o).
        if (numberRightToLeft != null && slot == WordSlot.ComplexScript && FontTable.Available(context.Fonts, Family(format, slot), style.Bold, style.Italic) == null)
            style = numberFonts.GetOrAdd(style, s => context.Fonts.TryResolveTypeface("Times New Roman", s.Bold, s.Italic) is { } times ? s with { Font = times } : s);
        return style;
    }

    readonly System.Collections.Concurrent.ConcurrentDictionary<RunStyle, RunStyle> numberFonts = new();

    /// <summary>
    /// The style of a paragraph mark: complex script where the mark is right to left or complex script (Word measures
    /// the last line of a w:rtl paragraph in Aharoni 12 pt with the mark's w:cs font: docx/local/rtlslot-a), else ASCII.
    /// </summary>
    public RunStyle Mark(RunFormat format) =>
        Style(format, format.UseComplexScript == true || format.RightToLeft == true ? WordSlot.ComplexScript : WordSlot.Ascii);

    /// <param name="smallCapital">A lowercase letter of small caps text, drawn as a capital at the small caps size.</param>
    /// <param name="complex">Complex script size, bold and italic; by default where the slot is complex script.</param>
    public RunStyle Style(RunFormat format, WordSlot slot, bool smallCapital = false, bool? complex = null)
    {
        var key = (slot, smallCapital, complex ?? slot == WordSlot.ComplexScript);
        var bySlot = cache.GetOrCreateValue(format);
        lock (bySlot)
        {
            if (!bySlot.TryGetValue(key, out var style))
                bySlot[key] = style = Create(format, slot, smallCapital, key.Item3);
            return style;
        }
    }

    readonly System.Collections.Concurrent.ConcurrentDictionary<(RunStyle, string), RunStyle> fallbacks = new();

    /// <summary>
    /// The style of a character the run's font lacks (catalog: engine.fonts): the same formatting in the first font of
    /// Word's fallbacks for the character that has it, or null where none has. Word measures the line with that font
    /// too (lines with Segoe UI Symbol are 15.96 pt in 12 pt Arial). Spaces, format characters and the private use
    /// area stay in the run's font (empty text form fields of en spaces keep Aptos's line height,
    /// docx/generated/form-fields; the shaper gives missing spaces their widths).
    /// </summary>
    /// <param name="emoji">Whether an emoji variation selector (U+FE0F) follows: the character is shown as emoji.</param>
    public RunStyle? Fallback(RunStyle style, int c, bool emoji = false)
    {
        if (c < 0x80 || c is >= 0xE000 and <= 0xF8FF || System.Text.Rune.IsWhiteSpace(new System.Text.Rune(c))
            || System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) is System.Globalization.UnicodeCategory.Format
                or System.Globalization.UnicodeCategory.Control or System.Globalization.UnicodeCategory.NonSpacingMark
                or System.Globalization.UnicodeCategory.EnclosingMark)
            return null;
        var data = style.Font.Data;
        foreach (var family in emoji ? ["Segoe UI Emoji", .. Fallbacks(c, data.Serif, data.EastAsian)] : Fallbacks(c, data.Serif, data.EastAsian))
        {
            if (context.Fonts.TryResolveTypeface(family, style.Bold, style.Italic) is { } font && font.Data.HasGlyph(c))
                return fallbacks.GetOrAdd((style, family), _ => style with { Font = font });
        }
        return null;
    }

    /// <summary>
    /// The fonts Word falls back to for a character the run's font lacks, measured with docx/local/fallback-arial,
    /// fallback-calibri and fallback-times (Word 16, Windows 11): emoji Segoe UI Emoji; symbols, dingbats and Braille
    /// Segoe UI Symbol; letterlike symbols, mathematical operators and alphanumerics, technical symbols and enclosed
    /// numbers Cambria Math (enclosed letters then MS Gothic, or MS Mincho for serif fonts); geometric shapes Arial;
    /// Thai, Lao and Khmer Leelawadee UI; Indic scripts and Sinhala Nirmala UI; Georgian Sylfaen; Ethiopic Ebrima;
    /// Cherokee Gadugi; Syriac Segoe UI Historic; Tibetan Microsoft Himalaya; Myanmar Myanmar Text; Hangul Malgun
    /// Gothic, Batang beside serif and East Asian fonts (Yu Mincho, DengXian, PMingLiU: docx/local/tf-*); Chinese
    /// characters and kana MS Mincho, then PMingLiU beside serif fonts and Microsoft JhengHei beside others, whatever
    /// the run's language (w:lang w:eastAsia="zh-CN" keeps MS Mincho: docx/local/tf-de).
    /// </summary>
    static IEnumerable<string> Fallbacks(int c, bool serif, bool eastAsian)
    {
        if (c >= 0x1F000 && c <= 0x1FAFF || EmojiPresentation(c))
            return ["Segoe UI Emoji", "Segoe UI Symbol", "Noto Color Emoji"];
        return c switch
        {
            >= 0x2100 and <= 0x214F or >= 0x2200 and <= 0x23FF or >= 0x1D400 and <= 0x1D7FF or >= 0x27C0 and <= 0x27EF or >= 0x2980 and <= 0x2AFF
                => ["Cambria Math", "Segoe UI Symbol", "STIX Two Math", "DejaVu Sans"],
            >= 0x2460 and <= 0x24FF => ["Cambria Math", serif ? "MS Mincho" : "MS Gothic", "Segoe UI Symbol"],
            >= 0x25A0 and <= 0x25FF => ["Arial", "Segoe UI Symbol", "DejaVu Sans"],
            >= 0x2190 and <= 0x21FF or >= 0x2600 and <= 0x27BF or >= 0x27F0 and <= 0x297F or >= 0x2800 and <= 0x28FF or >= 0x2B00 and <= 0x2BFF
                => ["Segoe UI Symbol", "Noto Sans Symbols", "Noto Sans Symbols 2", "DejaVu Sans"],
            >= 0x0E00 and <= 0x0EFF or >= 0x1780 and <= 0x17FF => ["Leelawadee UI", "Tahoma", "Noto Sans Thai", "Noto Sans Lao", "Noto Sans Khmer"],
            >= 0x0900 and <= 0x0DFF => ["Nirmala UI", "Mangal", "Noto Sans Devanagari"],
            >= 0x10A0 and <= 0x10FF => ["Sylfaen", "Noto Sans Georgian"],
            >= 0x1200 and <= 0x139F => ["Ebrima", "Nyala", "Noto Sans Ethiopic"],
            >= 0x13A0 and <= 0x13FF => ["Gadugi", "Plantagenet Cherokee", "Noto Sans Cherokee"],
            >= 0x0700 and <= 0x074F => ["Segoe UI Historic", "Estrangelo Edessa", "Noto Sans Syriac"],
            >= 0x0F00 and <= 0x0FFF => ["Microsoft Himalaya", "Noto Serif Tibetan"],
            >= 0x1000 and <= 0x109F => ["Myanmar Text", "Noto Sans Myanmar"],
            >= 0x1100 and <= 0x11FF or >= 0x3130 and <= 0x318F or >= 0xAC00 and <= 0xD7AF
                => serif || eastAsian ? ["Batang", "Malgun Gothic", "Noto Serif CJK KR"] : ["Malgun Gothic", "Batang", "Noto Sans CJK KR"],
            >= 0x2E80 and <= 0x9FFF or >= 0xF900 and <= 0xFAFF or >= 0xFF00 and <= 0xFFEF or >= 0x20000 and <= 0x2FFFF
                => ["MS Mincho", serif ? "PMingLiU" : "Microsoft JhengHei", "SimSun", "Microsoft YaHei", "Noto Serif CJK JP"],
            _ => ["Segoe UI Symbol", "Segoe UI", "Arial Unicode MS", "DejaVu Sans"],
        };
    }

    /// <summary>Characters shown as emoji by default (Unicode's Emoji_Presentation in the basic plane).</summary>
    static bool EmojiPresentation(int c) => c is 0x231A or 0x231B or >= 0x23E9 and <= 0x23EC or 0x23F0 or 0x23F3 or 0x25FD or 0x25FE
        or 0x2614 or 0x2615 or >= 0x2648 and <= 0x2653 or 0x267F or 0x2693 or 0x26A1 or 0x26AA or 0x26AB or 0x26BD or 0x26BE
        or 0x26C4 or 0x26C5 or 0x26CE or 0x26D4 or 0x26EA or 0x26F2 or 0x26F3 or 0x26F5 or 0x26FA or 0x26FD or 0x2705
        or 0x270A or 0x270B or 0x2728 or 0x274C or 0x274E or >= 0x2753 and <= 0x2755 or 0x2757 or >= 0x2795 and <= 0x2797
        or 0x27B0 or 0x27BF or 0x2B1B or 0x2B1C or 0x2B50 or 0x2B55;

    /// <summary>The size of the small capitals of text this size: 80 % on whole half points (12 pt → 9.5 pt, measured).</summary>
    public static double SmallCapsSize(double size) => Math.Round(size * 0.8 * 2, MidpointRounding.AwayFromZero) / 2;

    /// <summary>
    /// The size of small capitals as Word 5 for the Macintosh set them (w:mwSmallCaps, before Word 2013; catalog:
    /// settings.compatibility): the largest of the Macintosh's standard sizes below the text's size, 7 pt at least
    /// (measured with docx/local/c2-legacy* and c3-smallcaps12 from 5 to 72 pt: 11 pt → 10 pt, 24 pt → 18 pt).
    /// </summary>
    public static double MacSmallCapsSize(double size) => MacSizes.LastOrDefault(s => s < size - 1e-9, 7);

    static readonly double[] MacSizes = [7, 9, 10, 12, 14, 18, 24, 36, 48, 60, 72];

    bool? macSmallCaps;
    LineHeights? lineHeights;

    bool MacSmallCaps => macSmallCaps ??= context.CompatibilityMode < 15 && context.Compat("mwSmallCaps");

    bool? eastAsianDefaults;

    /// <summary>
    /// Whether the document's default East Asian font (w:docDefaults, through the theme) is an East Asian one, which
    /// makes Word space East Asian and Latin text apart (paragraph.eastAsian; measured with docx/local/ea-auto-*: MS
    /// Mincho, SimSun or Yu Gothic there space whatever the language, Arial or none there, or an East Asian font only
    /// in a style or on the paragraph mark, do not).
    /// </summary>
    public bool EastAsianDefaults => eastAsianDefaults ??=
        context.FontTable.Resolve(context.Fonts, Family(context.Styles.DefaultRun, WordSlot.EastAsia), false, false).Data.EastAsian;

    /// <summary>How the document takes line heights from fonts (w:noLeading, w:suppressTopSpacingWP; before Word 2013 only).</summary>
    public LineHeights LineHeights => lineHeights ??= context.CompatibilityMode >= 15 ? LineHeights.Word
        : context.Compat("suppressTopSpacingWP") ? LineHeights.WordPerfect
        : context.Compat("noLeading") ? LineHeights.NoLeading : LineHeights.Word;

    RunStyle Create(RunFormat format, WordSlot slot, bool smallCapital, bool complex)
    {
        var bold = (complex ? format.BoldComplex : format.Bold) == true;
        var italic = (complex ? format.ItalicComplex : format.Italic) == true;
        var size = (complex ? format.SizeComplex ?? format.Size : format.Size) ?? 10;
        var family = Family(format, slot);
        var font = context.FontTable.Resolve(context.Fonts, family, bold, italic);
        var color = Color(format);
        var vertical = format.VerticalAlign;
        // Super- and subscript (catalog: run.decoration), as Word sets them (measured with docx/local/supsizes and
        // supsizes2: Arial, Calibri, Times New Roman, Segoe UI, Cambria, Aptos and Courier New from 8 to 48 pt): the
        // font's OS/2 superscript or subscript size in whole half-points; a superscript raised by the font's
        // superscript offset or, where its top would stand above the line's (its ascent over the font's ascent and
        // line gap), as far as that leaves it, a subscript lowered by the subscript offset or as far as its bottom stays
        // on the line's, both in whole half-points (exact where the offsets decide, within half a point where the
        // line does; Garamond from 12 pt on takes other sizes in Word).
        var data = font.Data;
        var scriptSize = size;
        var scriptShift = 0.0;
        if (vertical is "superscript" or "subscript")
        {
            var superscript = vertical == "superscript";
            scriptSize = Math.Round(size * 2 * (superscript ? data.SuperscriptSize : data.SubscriptSize), MidpointRounding.AwayFromZero) / 2;
            var (above, below) = WordFonts.LineMetrics(data, size, LineHeights);
            var shift = superscript
                ? Math.Min(data.SuperscriptOffset * size, above - WordFonts.Ascent(data) * scriptSize)
                : Math.Min(data.SubscriptOffset * size, below - WordFonts.LineMetrics(data, scriptSize, LineHeights).Below);
            scriptShift = (superscript ? 1 : -1) * Math.Round(Math.Max(0, shift) * 2, MidpointRounding.AwayFromZero) / 2;
        }
        var raise = (format.Position ?? 0) + scriptShift;
        if (smallCapital)
            scriptSize = MacSmallCaps ? MacSmallCapsSize(scriptSize) : SmallCapsSize(scriptSize);
        // Tracked changes in the markup view (catalog: review.revisions, measured with docx/local/markup-a): insertions
        // underlined and deletions struck through in their author's color, moved text dark green, underlined twice
        // where it is and struck through twice where it was.
        var revision = format.Revision;
        if (revision != null)
            color = revision.Kind is "moveFrom" or "moveTo" ? Review.Moved : Review.Color(revision.Author);
        return new RunStyle(font, scriptSize, color, WordFonts.Features(format, size, context.CompatibilityMode < 14, font.Data.Table("kern") != null), bold, italic)
        {
            Revision = revision,
            Change = format.Change,
            CharacterStyle = format.Style,
            LineHeights = LineHeights,
            Underline = revision?.Kind switch { "ins" => "single", "moveTo" => "double", _ => format.Underline is { } u && u != "none" ? u : null },
            UnderlineColor = revision?.Kind is "ins" or "moveTo" ? null : format.UnderlineColor is { Length: 6 } uc ? RgbColor.FromHex(uc) : null,
            Strike = revision?.Kind == "del" || format.Strike == true && revision?.Kind != "moveFrom",
            DoubleStrike = revision?.Kind == "moveFrom" || format.DoubleStrike == true && revision?.Kind != "del",
            Emboss = format.Emboss == true,
            Imprint = format.Imprint == true,
            Hollow = format.Outline == true,
            Shadow = format.Shadow == true,
            Border = format.Border is { Visible: true } border ? border : null,
            Hidden = format.Vanish == true || format.SpecVanish == true,
            Highlight = WordFonts.Highlight(format.Highlight),
            Shading = format.ShadingPattern != null || format.ShadingFill != null ? Shading.Color(format.ShadingPattern, format.ShadingColor, format.ShadingFill) : null,
            Raise = raise,
            // Small capitals stand in the line of the text's size (measured with docx/local/c3-smallcaps12: Mac small caps
            // larger than their 5 pt text leave its line as it is).
            ScriptBase = vertical is "superscript" or "subscript" || smallCapital ? size : null,
            ScriptShift = scriptShift,
            Spacing = format.Spacing ?? 0,
            Scale = (format.Scale ?? 100) / 100,
            FitText = format.FitText is { } fit && fit > 0 ? fit : null,
            FitTextId = format.FitTextId,
            Caps = format.Caps == true,
            SmallCaps = format.SmallCaps == true,
            Effects = TextEffects.EffectList(format),
            Outline = TextEffects.Outline(format),
            TextFill = TextEffects.Fill(format),
            Solid = TextEffects.Solid(format),
            RightToLeft = format.RightToLeft == true || format.Embedding == "rtl",
            Override = format.Override,
            Emphasis = format.Emphasis is { } em && em != "none" ? em : null,
            LegacyKerning = context.CompatibilityMode < 14 && format.Kern is { } kern && kern > 0 && size >= kern,
            Language = slot switch
            {
                WordSlot.EastAsia => format.LanguageEastAsia ?? format.Language,
                WordSlot.ComplexScript => format.LanguageBidi ?? format.Language,
                _ => format.Language,
            },
        };
    }

    /// <summary>
    /// The family of a slot: the theme font it names (major or minor, by script), else its own name (catalog:
    /// font.theme). Where the theme's East Asian or complex script font is empty, Word takes the theme's font for the
    /// script of the language w:themeFontLang names (ja-JP: Jpan, zh-CN: Hans, ko-KR: Hang, ar-SA: Arab …); without
    /// one East Asian text keeps the run's Latin font and complex scripts take Times New Roman, both falling back by
    /// glyph from there; the run's own languages do not matter (measured with docx/local/tf-de, tf-ja, tf-zh, tf-ko,
    /// tf-he and tf-none).
    /// </summary>
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
            var east = theme.EndsWith("EastAsia", StringComparison.Ordinal);
            var bidi = theme.EndsWith("Bidi", StringComparison.Ordinal);
            var themed = east ? major ? t.MajorEastAsian : t.MinorEastAsian
                : bidi ? major ? t.MajorComplex : t.MinorComplex
                : major ? t.MajorLatin : t.MinorLatin;
            if (string.IsNullOrEmpty(themed) && (east || bidi)
                && Script(context.Settings.Child("themeFontLang").Attr(east ? "eastAsia" : "bidi")) is { } script
                && t.ScriptFonts.TryGetValue((major, script), out var scripted))
                themed = scripted;
            if (!string.IsNullOrEmpty(themed))
                return themed;
            // An East Asian theme font in the ASCII slot itself (w:asciiTheme="minorEastAsia") has nothing further to
            // fall back on.
            if (east && slot != WordSlot.Ascii)
                return name ?? Family(format, WordSlot.Ascii);
            if (bidi)
                return name ?? "Times New Roman";
        }
        return name ?? format.Ascii ?? "Times New Roman";
    }

    /// <summary>The script code of a theme's a:font entries for a language tag (ISO 15924 as Office writes them).</summary>
    static string? Script(string? language)
    {
        if (string.IsNullOrEmpty(language))
            return null;
        var tag = language.ToLowerInvariant();
        var primary = tag.Split('-')[0];
        return primary switch
        {
            "ja" => "Jpan",
            "ko" => "Hang",
            "zh" => tag is "zh-tw" or "zh-hk" or "zh-mo" || tag.Contains("hant") ? "Hant" : "Hans",
            "ar" or "fa" or "ur" or "ps" or "sd" or "ug" => primary == "ug" ? "Uigh" : "Arab",
            "he" or "yi" => "Hebr",
            "th" => "Thai",
            "hi" or "mr" or "ne" or "sa" or "kok" => "Deva",
            "bn" or "as" => "Beng",
            "gu" => "Gujr",
            "pa" => "Guru",
            "ta" => "Taml",
            "te" => "Telu",
            "kn" => "Knda",
            "ml" => "Mlym",
            "or" => "Orya",
            "si" => "Sinh",
            "km" => "Khmr",
            "lo" => "Laoo",
            "my" => "Mymr",
            "am" or "ti" => "Ethi",
            "ka" => "Geor",
            "hy" => "Armn",
            "dv" => "Thaa",
            "syr" => "Syrc",
            "bo" => "Tibt",
            "mn" => "Mong",
            "vi" => "Viet",
            "chr" => "Cher",
            "iu" => "Cans",
            "ii" => "Yiii",
            _ => null,
        };
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
