using System.Globalization;
using System.Xml.Linq;
using FreeSlate.Text;

namespace FreeSlate.Docx;

/// <summary>
/// The font table (w:fonts, catalog: font.table) and the font Word draws for a family the machine lacks, measured
/// with docx/local/font-sub and font-sub2 (Word 16 on Windows 11), in this order:
/// <list type="number">
/// <item>An installed family, or a family known by the name: PostScript names and other platforms' names as
/// PowerPoint knows them (ArialMT, Arial-BoldMT, TimesNewRomanPSMT, CourierNewPSMT, Calibri-Bold, Helvetica,
/// Helvetica Neue, Times, Courier, Palatino; the style in a name is dropped), Geneva (Arial), Lucida Grande (Segoe UI)
/// and Windows' font substitutes (MS Shell Dlg, Tahoma Armenian, Arial CE …).</item>
/// <item>The table's alternate name (w:altName).</item>
/// <item>The table's character set: symbol fonts become Symbol, Shift JIS Yu Gothic, Hangul Malgun Gothic, GB 2312
/// Microsoft YaHei, Big5 PMingLiU, Thai Browallia New; Arabic and Hebrew by the table's family, serif (roman) Sakkal
/// Majalla and Times New Roman, monospaced (modern) Tahoma, all others Arial, whatever the PANOSE and the signature
/// (docx/local/font-sub3; docx/external/libreoffice/extras-odfexport/tdf163575: B Mitra, auto, in Arial).</item>
/// <item>The table's family: serif fonts (roman), and fonts the table does not describe, become Cambria; all others
/// Calibri, sans serif, monospaced (even with a fixed pitch and Courier's PANOSE), script and decorative alike. The
/// PANOSE numbers and the document's theme fonts do not matter.</item>
/// </list>
/// Where Cambria and Calibri are missing too, their metric twins Caladea and Carlito stand in.
/// </summary>
public sealed class FontTable
{
    sealed record Entry(string? AltName, int? Charset, string? Family);

    readonly Dictionary<string, Entry> entries = new(StringComparer.OrdinalIgnoreCase);

    public FontTable(XDocument? document)
    {
        foreach (var font in document?.Root?.Elements(Wml.W + "font") ?? [])
        {
            if (font.Attr("name") is not { Length: > 0 } name)
                continue;
            var charset = font.Child("charset").Val() is { } hex && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var c) ? c : (int?)null;
            entries.TryAdd(name, new Entry(font.Child("altName").Val(), charset, font.Child("family").Val()));
        }
    }

    /// <summary>Names Word replaces by an installed family (Windows' font substitutes and other platforms' fonts).</summary>
    static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Geneva"] = "Arial", ["Lucida Grande"] = "Segoe UI", ["MS Shell Dlg"] = "Microsoft Sans Serif",
        ["MS Shell Dlg 2"] = "Tahoma", ["Tahoma Armenian"] = "Tahoma", ["Arabic Transparent"] = "Arial",
        ["Arabic Transparent Bold"] = "Arial", ["FangSong_GB2312"] = "FangSong", ["KaiTi_GB2312"] = "KaiTi",
    };

    static readonly string[] Serif = ["Cambria", "Caladea", "Times New Roman", "Liberation Serif", "DejaVu Serif"];
    static readonly string[] Sans = ["Calibri", "Carlito", "Arial", "Liberation Sans", "DejaVu Sans"];

    /// <summary>The font Word draws for a family in this style.</summary>
    public ResolvedFont Resolve(FontResolver fonts, string family, bool bold, bool italic) =>
        Available(fonts, family, bold, italic) ?? Substitute(fonts, family, bold, italic);

    /// <summary>The family itself where the machine has it, or the font Windows or Word puts for its name, else null.</summary>
    public static ResolvedFont? Available(FontResolver fonts, string family, bool bold, bool italic) =>
        fonts.TryResolveTypeface(family, bold, italic)
        ?? (Aliases.TryGetValue(family, out var alias) ? fonts.TryResolveTypeface(alias, bold, italic) : null);

    ResolvedFont Substitute(FontResolver fonts, string family, bool bold, bool italic)
    {
        entries.TryGetValue(family, out var entry);
        if (entry?.AltName is { Length: > 0 } alternate && fonts.TryResolveTypeface(alternate, bold, italic) is { } named)
            return named;
        var byCharset = entry?.Charset switch
        {
            2 => "Symbol",
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
        return fonts.ResolveFirst(entry == null || entry.Family == "roman" ? Serif : Sans, bold, italic);
    }
}
