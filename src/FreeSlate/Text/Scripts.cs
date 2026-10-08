using System.Text;

namespace FreeSlate.Text;

/// <summary>The font slot a character is drawn with: a:latin, a:ea (East Asian) or a:cs (complex scripts).</summary>
public enum FontSlot { Latin, EastAsian, Complex }

/// <summary>
/// Classifies characters into PowerPoint's font slots (catalog: text.fonts) and names the theme script
/// (a:font script="…") that supplies the font when the run's own East Asian or complex font is empty.
/// Spaces, digits and punctuation are neutral and stay with the text around them.
/// </summary>
public static class Scripts
{
    /// <summary>The slot and theme script of a code point; neutral characters return null.</summary>
    public static (FontSlot Slot, string? Script)? Of(int c)
    {
        if (c < 0x80)
            return char.IsLetter((char)c) ? (FontSlot.Latin, null) : null;
        if (Rune.IsValid(c) && !Rune.IsLetter(new Rune(c)) && !Rune.IsDigit(new Rune(c)) && !IsEastAsian(c) && c is < 0x0590 or > 0x0FFF)
            return null;   // punctuation and symbols outside the script blocks are neutral
        return c switch
        {
            >= 0x0590 and <= 0x05FF or >= 0xFB1D and <= 0xFB4F => (FontSlot.Complex, "Hebr"),
            >= 0x0600 and <= 0x06FF or >= 0x0750 and <= 0x077F or >= 0x08A0 and <= 0x08FF or >= 0xFB50 and <= 0xFDFF or >= 0xFE70 and <= 0xFEFF => (FontSlot.Complex, "Arab"),
            >= 0x0700 and <= 0x074F => (FontSlot.Complex, "Syrc"),
            >= 0x0780 and <= 0x07BF => (FontSlot.Complex, "Thaa"),
            >= 0x07C0 and <= 0x07FF => (FontSlot.Complex, "Nkoo"),
            >= 0x0900 and <= 0x097F => (FontSlot.Complex, "Deva"),
            >= 0x0980 and <= 0x09FF => (FontSlot.Complex, "Beng"),
            >= 0x0A00 and <= 0x0A7F => (FontSlot.Complex, "Guru"),
            >= 0x0A80 and <= 0x0AFF => (FontSlot.Complex, "Gujr"),
            >= 0x0B00 and <= 0x0B7F => (FontSlot.Complex, "Orya"),
            >= 0x0B80 and <= 0x0BFF => (FontSlot.Complex, "Taml"),
            >= 0x0C00 and <= 0x0C7F => (FontSlot.Complex, "Telu"),
            >= 0x0C80 and <= 0x0CFF => (FontSlot.Complex, "Knda"),
            >= 0x0D00 and <= 0x0D7F => (FontSlot.Complex, "Mlym"),
            >= 0x0D80 and <= 0x0DFF => (FontSlot.Complex, "Sinh"),
            >= 0x0E00 and <= 0x0E7F => (FontSlot.Complex, "Thai"),
            >= 0x0E80 and <= 0x0EFF => (FontSlot.Complex, "Laoo"),
            >= 0x0F00 and <= 0x0FFF => (FontSlot.Complex, "Tibt"),
            >= 0x1000 and <= 0x109F => (FontSlot.Complex, "Mymr"),
            >= 0x1780 and <= 0x17FF => (FontSlot.Complex, "Khmr"),
            >= 0x1800 and <= 0x18AF => (FontSlot.Complex, "Mong"),
            >= 0xAC00 and <= 0xD7AF or >= 0x1100 and <= 0x11FF or >= 0x3130 and <= 0x318F => (FontSlot.EastAsian, "Hang"),
            >= 0x3040 and <= 0x30FF or >= 0x31F0 and <= 0x31FF => (FontSlot.EastAsian, "Jpan"),
            _ when IsEastAsian(c) => (FontSlot.EastAsian, null),
            _ => (FontSlot.Latin, null),
        };
    }

    /// <summary>
    /// Characters that stay upright in East Asian vertical text (catalog: text.vertical): ideographs, kana, hangul
    /// and full-width forms; the prolonged sound mark, dashes and half-width forms turn with the Latin text.
    /// </summary>
    public static bool Upright(int c) => IsEastAsian(c) && c is not (0x30FC or 0x2015 or 0xFF5E or 0x301C) and not (>= 0xFF61 and <= 0xFFDC);

    static bool IsEastAsian(int c) =>
        c is >= 0x2E80 and <= 0x2FDF or >= 0x3000 and <= 0x303F or >= 0x3040 and <= 0x31FF or >= 0x3200 and <= 0x33FF
            or >= 0x3400 and <= 0x4DBF or >= 0x4E00 and <= 0x9FFF or >= 0xA000 and <= 0xA4CF or >= 0xAC00 and <= 0xD7AF
            or >= 0xF900 and <= 0xFAFF or >= 0xFE30 and <= 0xFE4F or >= 0xFF00 and <= 0xFFEF or >= 0x20000 and <= 0x2FA1F;

    /// <summary>
    /// Splits text into pieces of one slot; neutral characters join the piece before them (at the start the one
    /// after them). Each piece carries the theme script of its first classified character.
    /// </summary>
    public static List<(string Text, FontSlot Slot, string? Script)> Split(string text)
    {
        var pieces = new List<(string Text, FontSlot Slot, string? Script)>();
        var current = new StringBuilder();
        (FontSlot Slot, string? Script)? kind = null;
        var pendingNeutral = new StringBuilder();
        foreach (var rune in text.EnumerateRunes())
        {
            var c = Scripts.Of(rune.Value);
            if (c == null)
            {
                (kind == null ? pendingNeutral : current).Append(rune.ToString());
                continue;
            }
            if (kind == null)
            {
                kind = c;
                current.Append(pendingNeutral).Append(rune.ToString());
                pendingNeutral.Clear();
                continue;
            }
            if (c.Value.Slot != kind.Value.Slot || c.Value.Slot == FontSlot.Complex && c.Value.Script != kind.Value.Script)
            {
                pieces.Add((current.ToString(), kind.Value.Slot, kind.Value.Script));
                current.Clear();
                kind = c;
            }
            current.Append(rune.ToString());
        }
        if (kind == null)
            return [(pendingNeutral.ToString(), FontSlot.Latin, null)];
        pieces.Add((current.ToString(), kind.Value.Slot, kind.Value.Script));

        // Neutral characters at the edges of right-to-left text take the (left-to-right) paragraph direction, as in
        // the Unicode bidi algorithm: "ج-" shows the hyphen to the right of the letter.
        for (var i = 0; i < pieces.Count; i++)
        {
            var (piece, slot, script) = pieces[i];
            if (!IsRightToLeft(script))
                continue;
            var start = 0;
            while (start < piece.Length && Scripts.Of(piece[start]) == null) start++;
            var end = piece.Length;
            while (end > start && Scripts.Of(piece[end - 1]) == null && !char.IsLowSurrogate(piece[end - 1])) end--;
            if (start == 0 && end == piece.Length)
                continue;
            var replacement = new List<(string, FontSlot, string?)>();
            if (start > 0) replacement.Add((piece[..start], FontSlot.Latin, null));
            replacement.Add((piece[start..end], slot, script));
            if (end < piece.Length) replacement.Add((piece[end..], FontSlot.Latin, null));
            pieces.RemoveAt(i);
            pieces.InsertRange(i, replacement);
            i += replacement.Count - 1;
        }
        return pieces;
    }

    static bool IsRightToLeft(string? script) => script is "Arab" or "Hebr" or "Syrc" or "Thaa" or "Nkoo";

    /// <summary>The East Asian theme script for a language tag (ja, ko, zh-CN, zh-TW …), Japanese by default.</summary>
    public static string EastAsianScript(string? language, string? characterScript)
    {
        if (characterScript != null) return characterScript;
        var lang = (language ?? "").ToLowerInvariant();
        if (lang.StartsWith("ko", StringComparison.Ordinal)) return "Hang";
        if (lang is "zh-tw" or "zh-hk" or "zh-mo" || lang.StartsWith("zh-hant", StringComparison.Ordinal)) return "Hant";
        if (lang.StartsWith("zh", StringComparison.Ordinal)) return "Hans";
        return "Jpan";
    }

    /// <summary>Installed fonts that usually cover a script, tried when the chosen font lacks glyphs.</summary>
    public static IReadOnlyList<string> Fallbacks(FontSlot slot, string? script) => (slot, script) switch
    {
        (FontSlot.EastAsian, "Hang") => ["Malgun Gothic", "Gulim", "Noto Sans CJK KR", "Noto Sans KR"],
        (FontSlot.EastAsian, "Hans") => ["Microsoft YaHei", "DengXian", "SimSun", "Noto Sans CJK SC", "Noto Sans SC"],
        (FontSlot.EastAsian, "Hant") => ["Microsoft JhengHei", "PMingLiU", "Noto Sans CJK TC", "Noto Sans TC"],
        (FontSlot.EastAsian, _) => ["Yu Gothic", "Meiryo", "MS Gothic", "Microsoft YaHei", "Noto Sans CJK JP", "Noto Sans JP"],
        (FontSlot.Complex, "Arab") => ["Times New Roman", "Arial", "Segoe UI", "Tahoma", "Noto Naskh Arabic", "Noto Sans Arabic"],
        (FontSlot.Complex, "Hebr") => ["Times New Roman", "Arial", "Segoe UI", "Noto Sans Hebrew"],
        (FontSlot.Complex, "Thai") => ["Tahoma", "Leelawadee UI", "Noto Sans Thai"],
        (FontSlot.Complex, "Laoo" or "Khmr" or "Mymr") => ["Leelawadee UI", "Khmer UI", "Myanmar Text", "Noto Sans Lao", "Noto Sans Khmer", "Noto Sans Myanmar"],
        (FontSlot.Complex, _) => ["Nirmala UI", "Mangal", "Noto Sans Devanagari", "Noto Sans Bengali", "Noto Sans Tamil"],
        _ => ["Segoe UI Symbol", "Segoe UI", "Arial Unicode MS", "Noto Sans Symbols", "DejaVu Sans"],
    };
}
