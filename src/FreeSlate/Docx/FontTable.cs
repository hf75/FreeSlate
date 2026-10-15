using System.Globalization;
using System.Xml.Linq;
using FreeSlate.Text;

namespace FreeSlate.Docx;

/// <summary>
/// The font table (w:fonts, catalog: font.table) and the font Word draws for a family the machine lacks, measured
/// with docx/local/font-sub and font-sub2 (Word 16 on Windows 11), in this order:
/// <list type="number">
/// <item>An installed family, or a family known by the name: Word's own (any Futura Century Gothic, any Baskerville
/// Baskerville Old Face, Myriad Pro Segoe UI, New York Times New Roman, Gill Sans Light and Helvetica Light the Nova
/// families where Office has them: docx/local/fontnames-plain).</item>
/// <item>The table's alternate name (w:altName) where a family of that very name is installed (docx/local/
/// fontnames-alt: an altName of Courier counts for nothing; fs2-d: DejaVu Sans with altName Times New Roman in Times
/// New Roman; docx/external/powertools/RP/RP052-Deleted-Para-Mark).</item>
/// <item>PostScript names, other platforms' names and free families as PowerPoint knows them (ArialMT, Arial-BoldMT,
/// TimesNewRomanPSMT, CourierNewPSMT, Calibri-Bold, Helvetica, Helvetica Neue, Times, Courier, Palatino, Liberation
/// Serif, DejaVu Sans; the style in a name is dropped), Geneva (Arial), Lucida Grande (Segoe UI) and Windows' font
/// substitutes (MS Shell Dlg, Tahoma Armenian, Arial CE …), not for a Cyrillic font (docx/local/fs2-a to -c:
/// Liberation Serif of character set 00 or EE in Times New Roman, of CC in Cambria).</item>
/// <item>The table's character set: symbol fonts become Symbol, Shift JIS Yu Gothic, Hangul Malgun Gothic, GB 2312
/// Microsoft YaHei, Big5 PMingLiU, Thai Browallia New; Arabic and Hebrew by the table's family, serif (roman) Sakkal
/// Majalla and Times New Roman, monospaced (modern) Tahoma, all others Arial, whatever the PANOSE and the signature
/// (docx/local/font-sub3; docx/external/libreoffice/extras-odfexport/tdf163575: B Mitra, auto, in Arial).</item>
/// <item>The signature's Hebrew or Arabic code page (w:sig csb0): Arial, serif fonts Times New Roman; then American
/// Typewriter and Apple Chancery Courier New (docx/local/font-sig; pandoc/normalize).</item>
/// <item>The table's family: serif fonts (roman), and fonts the table does not describe, become Cambria; all others
/// Calibri, sans serif, monospaced (even with a fixed pitch and Courier's PANOSE), script and decorative alike. The
/// PANOSE numbers and the document's theme fonts do not matter.</item>
/// </list>
/// Where Cambria and Calibri are missing too, their metric twins Caladea and Carlito stand in. Of a family's entries
/// the table's last one counts (docx/local/fontdup; docx/external/libreoffice/extras-ooxmlexport/
/// tdf153526_commentInNumbering: OpenSymbol, first roman, then auto, prints in Calibri).
/// </summary>
public sealed class FontTable
{
    sealed record Entry(string? AltName, int? Charset, string? Family, uint CodePages = 0, uint Ranges = 0);

    readonly Dictionary<string, Entry> entries = new(StringComparer.OrdinalIgnoreCase);

    public FontTable(XDocument? document)
    {
        foreach (var font in document?.Root?.Elements(Wml.W + "font") ?? [])
        {
            if (font.Attr("name") is not { Length: > 0 } name)
                continue;
            var charset = font.Child("charset").Val() is { } hex && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var c) ? c : (int?)null;
            var codePages = font.Child("sig")?.Attr("csb0") is { } csb0 && uint.TryParse(csb0, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var cp) ? cp : 0;
            var ranges = font.Child("sig")?.Attr("usb0") is { } usb0 && uint.TryParse(usb0, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var ur) ? ur : 0;
            entries[name] = new Entry(font.Child("altName").Val(), charset, font.Child("family").Val(), codePages, ranges);
        }
    }

    /// <summary>
    /// Mac families Word puts in another, after the signature's code pages (docx/local/font-sig: American Typewriter
    /// and Apple Chancery with a Latin signature print in Courier New; pandoc/normalize).
    /// </summary>
    static readonly Dictionary<string, string> MacNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["American Typewriter"] = "Courier New", ["Apple Chancery"] = "Courier New",
    };

    /// <summary>Names Word replaces by an installed family (Windows' font substitutes and other platforms' fonts).</summary>
    static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Geneva"] = "Arial", ["Lucida Grande"] = "Segoe UI", ["MS Shell Dlg"] = "Microsoft Sans Serif",
        ["MS Shell Dlg 2"] = "Tahoma", ["Tahoma Armenian"] = "Tahoma", ["Arabic Transparent"] = "Arial",
        ["Arabic Transparent Bold"] = "Arial", ["FangSong_GB2312"] = "FangSong", ["KaiTi_GB2312"] = "KaiTi",
    };

    /// <summary>Windows' font substitutes (the registry's FontSubstitutes) for names of Latin fonts.</summary>
    static readonly Dictionary<string, string> WindowsSubstitutes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Times"] = "Times New Roman", ["Helvetica"] = "Arial",
    };

    /// <summary>Names Word itself replaces by an installed family (docx/local/fontnames-plain), before PowerPoint's.</summary>
    static readonly Dictionary<string, string> WordNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Myriad Pro"] = "Segoe UI", ["New York"] = "Times New Roman", ["Gill Sans Light"] = "Gill Sans Nova Light",
        ["Helvetica Light"] = "Arial Nova Light",
    };

    /// <summary>
    /// Beginnings of names Word replaces by an installed family, whatever follows them (Futura Medium, Futura Md BT; web
    /// font lists as HTML leaves them: docx/local/font-list, font-list2: "Open Sans;Arial;sans-serif", "Open Sans Zz"
    /// and "Lato;Arial" in Segoe UI, "Roboto;Arial" and "Arial;Helvetica" in Arial, but "Calibri;Zz", "Verdana;Arial"
    /// and "Zz;Arial" in Cambria; tika/testWORD_features).
    /// </summary>
    static readonly (string Prefix, string Family)[] WordPrefixes =
    [
        ("Futura", "Century Gothic"), ("Baskerville", "Baskerville Old Face"), ("Open Sans", "Segoe UI"), ("Lato", "Segoe UI"),
        ("Roboto", "Arial"), ("Arial", "Arial"),
    ];

    static readonly string[] Serif = ["Cambria", "Caladea", "Times New Roman", "Liberation Serif", "DejaVu Serif"];
    static readonly string[] Sans = ["Calibri", "Carlito", "Arial", "Liberation Sans", "DejaVu Sans"];

    /// <summary>
    /// The font Word draws for a family in this style. A missing family that the font table gives an East Asian
    /// character set takes that script's font before what its name begins with (measured with docx/local/fsy-*:
    /// "Arial Unicode MS" and "Arial Zz" of charset 80 in Yu Gothic, Latin and kana alike, of charset 00 or not described
    /// in Arial; docx/external/libreoffice/extras-ooxmlexport/tdf107969).
    /// </summary>
    public ResolvedFont Resolve(FontResolver fonts, string family, bool bold, bool italic)
    {
        if (entries.TryGetValue(family, out var entry) && entry.Charset is 128 or 129 or 134 or 136)
            return fonts.TryResolveFamily(family, bold, italic) ?? Substitute(fonts, family, bold, italic);
        // The family itself or Word's name for it, then an installed alternate name, then the names of other platforms
        // and free families, those not for a Cyrillic font (measured with docx/local/fs2-*, each in a Word session of
        // its own: DejaVu Sans with altName Times New Roman in Times New Roman, without it in Verdana; Liberation Serif
        // of character set 00 or EE in Times New Roman, of CC in Cambria; docx/external/libreoffice/core-layout/tdf157096).
        if ((fonts.TryResolveFamily(family, bold, italic) ?? (WordName(family) is { } named ? fonts.TryResolveFamily(named, bold, italic) : null)) is { } own)
            return own;
        if (entry?.AltName is { Length: > 0 } alternate && fonts.TryResolveFamily(alternate, bold, italic) is { } alternative)
            return alternative;
        // A signature with the Armenian Unicode range and the Georgian one, or without Basic Latin, takes Sylfaen before
        // what other platforms call the family and before its code pages (measured with docx/local/fs3-d, fs3-f, fs3-g,
        // each in a Word session of its own: DejaVu Sans with usb0 E7002EFF or the Armenian range alone in Sylfaen, with
        // csb0 alone in Verdana; pano-00 to pano-15: DejaVu, Liberation and unknown names with usb0 E7006EFF and the
        // Hebrew and Arabic code pages in Sylfaen; pano2-04: the Georgian range alone with those code pages in Arial;
        // Word's PDFs of LibreOffice's extras-ooxmlexport/fdo71646, and of extras-layout/tdf138465min: Liberation Serif
        // with E0002EFF, Latin and Armenian without Georgian, in Times New Roman).
        if (entry != null && (entry.Ranges & (1u << 10)) != 0 && ((entry.Ranges & (1u << 26)) != 0 || (entry.Ranges & 1u) == 0)
            && fonts.TryResolveFamily("Sylfaen", bold, italic) is { } armenian)
            return armenian;
        // Windows' own font substitutes hold for every character set (Word's PDF of docx/external/poi/bookmarks, in a
        // Word session of its own: Times of character set CC in Times New Roman; Liberation Serif of CC in Cambria, fs2-c).
        if (WindowsSubstitutes.TryGetValue(family, out var windows) && fonts.TryResolveFamily(windows, bold, italic) is { } substituted)
            return substituted;
        if (entry?.Charset != 204 && (fonts.TryResolveTypeface(family, bold, italic)
                ?? (Aliases.TryGetValue(family, out var alias) ? fonts.TryResolveTypeface(alias, bold, italic) : null)) is { } typeface)
            return typeface;
        return Substitute(fonts, family, bold, italic);
    }

    /// <summary>The family itself where the machine has it, or the font Windows or Word puts for its name, else null.</summary>
    public static ResolvedFont? Available(FontResolver fonts, string family, bool bold, bool italic) =>
        fonts.TryResolveFamily(family, bold, italic)
        ?? (WordName(family) is { } named ? fonts.TryResolveFamily(named, bold, italic) : null)
        ?? fonts.TryResolveTypeface(family, bold, italic)
        ?? (Aliases.TryGetValue(family, out var alias) ? fonts.TryResolveTypeface(alias, bold, italic) : null);

    static string? ScriptFont(uint ranges)
    {
        // A range without a font of its own (Thai) is passed over (measured with docx/local/pano2-01: Thai and Georgian
        // in Sylfaen).
        for (var bit = 15; bit <= 26; bit++)
        {
            if ((ranges & (1u << bit)) == 0)
                continue;
            if (bit switch
                {
                    15 => "Mangal",
                    16 or 17 or 18 or 19 or 20 or 22 or 23 => "Nirmala UI",
                    21 => "Microsoft New Tai Lue",
                    25 => "Leelawadee UI",
                    26 => "Sylfaen",
                    _ => null,
                } is { } font)
                return font;
        }
        return null;
    }

    static string? WordName(string family) =>
        WordNames.TryGetValue(family, out var named) ? named
        : WordPrefixes.FirstOrDefault(p => family.StartsWith(p.Prefix, StringComparison.OrdinalIgnoreCase)).Family;

    ResolvedFont Substitute(FontResolver fonts, string family, bool bold, bool italic)
    {
        entries.TryGetValue(family, out var entry);
        // A Japanese entry whose alternate name is Times New Roman prints in MS PMincho, Word's Japanese Times; without
        // an alternate name it takes Yu Gothic below (measured with docx/local/fs3-a, fs3-b, fs3-c, each in a Word
        // session of its own; Word's PDFs of LibreOffice's extras-ooxmlexport/fdo69548, fdo47669, extras-ooxmlimport/
        // tdf103931: Liberation Serif, roman, charset 80, altName Times New Roman).
        if (entry is { Charset: 128 } && string.Equals(entry.AltName, "Times New Roman", StringComparison.OrdinalIgnoreCase)
            && fonts.TryResolveTypeface("MS PMincho", bold, italic) is { } mincho)
            return mincho;
        if (entry?.AltName is { Length: > 0 } alternate && fonts.TryResolveFamily(alternate, bold, italic) is { } named)
            return named;
        // A symbol font prints in Wingdings (measured with docx/local/psym-a, psym-b: U+F021 to U+F0FE in a missing
        // "starbats" or "ZzSymbol" of charset 02 all in Wingdings; LibreOffice's extras-ooxmlexport/starbats-bullet).
        var byCharset = entry?.Charset switch
        {
            2 => "Wingdings",
            128 => "Yu Gothic",
            129 => "Malgun Gothic",
            134 => "Microsoft YaHei",
            136 => "PMingLiU",
            177 => entry.Family switch { "roman" => "Times New Roman", "modern" => "Tahoma", _ => "Arial" },
            178 => entry.Family switch { "roman" => "Sakkal Majalla", "modern" => "Tahoma", _ => "Arial" },
            222 => "Browallia New",
            _ => null,
        };
        if (byCharset != null && fonts.TryResolveTypeface(byCharset, bold, italic) is { } script)
            return script;
        // A signature with the Hebrew or Arabic code page (bits 5, 6 of w:sig csb0), which Calibri lacks: Arial, a
        // serif font Times New Roman (measured with docx/local/font-sig: such fonts of family auto in Arial, Hebrew
        // roman in Times New Roman, Cyrillic or Greek ones in Calibri, the Unicode ranges and PANOSE alone nothing;
        // pandoc/normalize: Apple Chancery, Apple Symbols, Big Caslon).
        // A Cyrillic font (character set CC) passes it by (Word's PDF of docx/external/libreoffice/extras-layout/
        // tdf153136, in a session of its own: Liberation Serif, roman, CC, with the Hebrew code page among others, in
        // Cambria).
        if (entry != null && entry.Charset != 204 && (entry.CodePages & 0x60) != 0
            && fonts.TryResolveTypeface(entry.Family == "roman" ? "Times New Roman" : "Arial", bold, italic) is { } paged)
            return paged;
        // A signature with an Indic, Lao or Georgian Unicode range (w:sig usb0 bits 15 to 26) takes that script's font,
        // the lowest range first (measured with docx/local/fsu-c: Devanagari Mangal, Bengali to Tamil, Kannada and
        // Malayalam Nirmala UI, Telugu Microsoft New Tai Lue, Lao Leelawadee UI, Georgian Sylfaen, Thai nothing;
        // fsu-a, fsu-b: Bangla MN's 80008003 and five other names in Mangal; pandoc/normalize).
        if (entry != null && ScriptFont(entry.Ranges) is { } scriptName && fonts.TryResolveFamily(scriptName, bold, italic) is { } ranged)
            return ranged;
        if (MacNames.TryGetValue(family, out var mac) && fonts.TryResolveFamily(mac, bold, italic) is { } macFont)
            return macFont;
        // A name in East Asian characters, of no font here, is Yu Gothic's (measured with docx/local/fsy-list, fsy-mzz:
        // "ＭＳ 明朝;MS Mincho" and "明朝Zz" in Yu Gothic, Latin and kana alike; fsy-list2: "Zz;MS Mincho" in Cambria;
        // docx/external/libreoffice/extras-uiwriter/tdf128375).
        if (family.Any(c => c is >= '　' and <= '鿿' or >= '＀' and <= '￯' or >= '가' and <= '힯')
            && fonts.TryResolveTypeface("Yu Gothic", bold, italic) is { } gothic)
            return gothic;
        return fonts.ResolveFirst(entry == null || entry.Family == "roman" ? Serif : Sans, bold, italic);
    }
}
