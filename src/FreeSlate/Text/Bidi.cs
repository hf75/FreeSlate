using System.Globalization;

namespace FreeSlate.Text;

/// <summary>
/// The Unicode bidirectional algorithm (UAX #9) for one paragraph with a given base direction (catalog: text.rtl):
/// character types, the weak, neutral and implicit rules (W1–W7, N1–N2, I1–I2) and the line rules L1–L2. Explicit
/// embeddings and isolates (rare in presentations) are treated as neutrals; mirroring and marks are left to shaping.
/// </summary>
public static class Bidi
{
    public enum Type : byte { L, R, AL, EN, ES, ET, AN, CS, NSM, BN, B, S, WS, ON }

    /// <summary>The bidirectional type of a code point (from its script range and general category).</summary>
    public static Type Classify(int c)
    {
        switch (c)
        {
            case 0x09: case 0x0B: case 0x1F: return Type.S;
            case 0x0A: case 0x0D: case 0x1C: case 0x1D: case 0x1E: case 0x85: case 0x2029: return Type.B;
            case 0x0C: case 0x20: return Type.WS;
            case '+': case '-': case 0x207A: case 0x207B: case 0x208A: case 0x208B: case 0x2212: case 0xFB29: case 0xFE62: case 0xFE63: case 0xFF0B: case 0xFF0D: return Type.ES;
            case '#': case '$': case '%': case 0xA2: case 0xA3: case 0xA4: case 0xA5: case 0xB0: case 0xB1: case 0x2030: case 0x2031: case 0x2032: case 0x2033: case 0x2034:
            case 0x066A: case 0x09F2: case 0x09F3: case 0x0E3F: case 0x17DB: case 0x212E: case 0x2213: case 0xFE5F: case 0xFE69: case 0xFE6A: case 0xFF03: case 0xFF04: case 0xFF05: case 0xFFE0: case 0xFFE1: case 0xFFE5: case 0xFFE6:
                return Type.ET;
            case ',': case '.': case '/': case ':': case 0xA0: case 0x060C: case 0x202F: case 0x2044: case 0xFE50: case 0xFE52: case 0xFE55: case 0xFF0C: case 0xFF0E: case 0xFF0F: case 0xFF1A:
                return Type.CS;
            case 0x200E: return Type.L;
            case 0x200F: return Type.R;
            case 0x061C: return Type.AL;
        }
        if (c is >= '0' and <= '9' || c is >= 0x06F0 and <= 0x06F9 || c is >= 0x2070 and <= 0x2079 && c != 0x2071 && c is not (0x2072 or 0x2073)
            || c is >= 0x2080 and <= 0x2089 || c is >= 0xFF10 and <= 0xFF19 || c is 0xB2 or 0xB3 or 0xB9 || c is >= 0x2488 and <= 0x249B)
            return Type.EN;
        if (c is >= 0x0660 and <= 0x0669 || c is 0x066B or 0x066C || c is >= 0x0600 and <= 0x0605 || c == 0x06DD || c is >= 0x10E60 and <= 0x10E7E)
            return Type.AN;
        if (c is >= 0x20A0 and <= 0x20CF)
            return Type.ET;
        var category = CharUnicodeInfo.GetUnicodeCategory(c);
        if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark)
            return Type.NSM;
        if (category == UnicodeCategory.Format)
            return Type.BN;
        if (category == UnicodeCategory.SpaceSeparator)
            return Type.WS;
        if (category is UnicodeCategory.LineSeparator)
            return Type.WS;
        if (category is UnicodeCategory.ParagraphSeparator)
            return Type.B;
        if (category == UnicodeCategory.Control)
            return Type.BN;
        // Right-to-left scripts: Hebrew, NKo, Samaritan, Mandaic and the like (R); Arabic, Syriac, Thaana (AL).
        if (c is >= 0x0590 and <= 0x05FF || c is >= 0x07C0 and <= 0x085F || c is >= 0xFB1D and <= 0xFB4F || c is >= 0x10800 and <= 0x10FFF && c is < 0x10D00 or > 0x10D3F && c is < 0x10F30 or > 0x10F6F
            || c is >= 0x1E800 and <= 0x1EDFF)
            return category is UnicodeCategory.OtherPunctuation or UnicodeCategory.MathSymbol or UnicodeCategory.OtherSymbol ? Type.ON : Type.R;
        if (c is >= 0x0600 and <= 0x07BF || c is >= 0x0860 and <= 0x08FF || c is >= 0xFB50 and <= 0xFDFF || c is >= 0xFE70 and <= 0xFEFF || c is >= 0x10D00 and <= 0x10D3F
            || c is >= 0x10F30 and <= 0x10F6F || c is >= 0x1EC70 and <= 0x1EEFF)
            return category is UnicodeCategory.MathSymbol or UnicodeCategory.OtherSymbol || c is 0x060E or 0x060F or 0x06DE or 0x06E9 or 0xFD3E or 0xFD3F ? Type.ON : Type.AL;
        return category switch
        {
            UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter
                or UnicodeCategory.OtherLetter or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.LetterNumber or UnicodeCategory.PrivateUse
                or UnicodeCategory.Surrogate => Type.L,
            UnicodeCategory.DecimalDigitNumber => Type.L,
            UnicodeCategory.OtherNumber => Type.ON,
            _ => Type.ON,
        };
    }

    /// <summary>Whether <paramref name="text"/> needs the algorithm at all (it has right-to-left characters).</summary>
    public static bool HasRightToLeft(string text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            if (Classify(rune.Value) is Type.R or Type.AL or Type.AN)
                return true;
        }
        return false;
    }

    /// <summary>
    /// The embedding level of every UTF-16 code unit of a paragraph (0 for left to right, 1 base for right to left).
    /// Neutrals between text of different directions follow the direction of their run's language when
    /// <paramref name="language"/> knows it, else the paragraph's (measured: PowerPoint keeps "PowerPoint " with its
    /// space together in an Arabic paragraph); UAX #9 alone would give them the paragraph's direction.
    /// </summary>
    public static byte[] Levels(string text, bool rightToLeft, Func<int, bool?>? language = null)
    {
        var n = text.Length;
        var levels = new byte[n];
        var baseLevel = (byte)(rightToLeft ? 1 : 0);
        if (n == 0)
            return levels;
        if (!rightToLeft && !HasRightToLeft(text))
            return levels;

        // Types per code unit (a surrogate pair's low half shares its rune's type).
        var types = new Type[n];
        for (var i = 0; i < n; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < n && char.IsLowSurrogate(text[i + 1]))
            {
                types[i] = types[i + 1] = Classify(char.ConvertToUtf32(text[i], text[i + 1]));
                i++;
            }
            else
            {
                types[i] = Classify(text[i]);
            }
        }
        var original = (Type[])types.Clone();
        var sos = rightToLeft ? Type.R : Type.L;

        // X9: boundary neutrals are ignored; treat them as their neighbours by skipping them below.
        // W1: NSM takes the type of the previous character (sos at the start).
        var previous = sos;
        for (var i = 0; i < n; i++)
        {
            if (types[i] == Type.BN) continue;
            if (types[i] == Type.NSM) types[i] = previous;
            previous = types[i];
        }
        // W2: EN after AL (the last strong type) becomes AN.
        var lastStrong = sos;
        for (var i = 0; i < n; i++)
        {
            switch (types[i])
            {
                case Type.L or Type.R or Type.AL: lastStrong = types[i]; break;
                case Type.EN when lastStrong == Type.AL: types[i] = Type.AN; break;
            }
        }
        // W3: AL becomes R.
        for (var i = 0; i < n; i++)
        {
            if (types[i] == Type.AL) types[i] = Type.R;
        }
        // W4: a single ES between ENs becomes EN; a single CS between numbers of the same type takes that type.
        for (var i = 1; i < n - 1; i++)
        {
            if (types[i] == Type.ES && types[i - 1] == Type.EN && types[i + 1] == Type.EN) types[i] = Type.EN;
            else if (types[i] == Type.CS && types[i - 1] == Type.EN && types[i + 1] == Type.EN) types[i] = Type.EN;
            else if (types[i] == Type.CS && types[i - 1] == Type.AN && types[i + 1] == Type.AN) types[i] = Type.AN;
        }
        // W5: a sequence of ETs next to an EN becomes EN.
        for (var i = 0; i < n; i++)
        {
            if (types[i] != Type.ET) continue;
            var end = i;
            while (end < n && (types[end] == Type.ET || types[end] == Type.BN)) end++;
            var touches = (i > 0 && types[i - 1] == Type.EN) || (end < n && types[end] == Type.EN);
            if (touches)
            {
                for (var k = i; k < end; k++) types[k] = Type.EN;
            }
            i = end - 1;
        }
        // W6: remaining separators and terminators become ON.
        for (var i = 0; i < n; i++)
        {
            if (types[i] is Type.ES or Type.ET or Type.CS) types[i] = Type.ON;
        }
        // W7: EN after L (the last strong type, sos at the start) becomes L.
        lastStrong = sos;
        for (var i = 0; i < n; i++)
        {
            switch (types[i])
            {
                case Type.L or Type.R: lastStrong = types[i]; break;
                case Type.EN when lastStrong == Type.L: types[i] = Type.L; break;
            }
        }
        // N1, N2: runs of neutrals take the direction around them when both sides agree (numbers count as R),
        // else the embedding direction.
        static bool Neutral(Type t) => t is Type.B or Type.S or Type.WS or Type.ON or Type.BN;
        Type Strong(Type t) => t is Type.EN or Type.AN ? Type.R : t;
        for (var i = 0; i < n; i++)
        {
            if (!Neutral(types[i])) continue;
            var end = i;
            while (end < n && Neutral(types[end])) end++;
            var before = i == 0 ? sos : Strong(types[i - 1]);
            var after = end == n ? sos : Strong(types[end]);
            for (var k = i; k < end; k++)
                types[k] = before == after ? before : language?.Invoke(k) switch { true => Type.R, false => Type.L, _ => rightToLeft ? Type.R : Type.L };
            i = end - 1;
        }
        // I1, I2: implicit levels.
        for (var i = 0; i < n; i++)
        {
            var level = baseLevel;
            if (level % 2 == 0)
                level += types[i] switch { Type.R => 1, Type.AN or Type.EN => 2, _ => 0 };
            else
                level += types[i] is Type.L or Type.EN or Type.AN ? (byte)1 : (byte)0;
            levels[i] = level;
        }
        // L1: separators, and whitespace before them and at the end, go back to the paragraph level.
        var trailing = true;
        for (var i = n - 1; i >= 0; i--)
        {
            var t = original[i];
            if (t is Type.S or Type.B)
            {
                levels[i] = baseLevel;
                trailing = true;
            }
            else if (t is Type.WS or Type.BN && trailing)
            {
                levels[i] = baseLevel;
            }
            else
            {
                trailing = false;
            }
        }
        return levels;
    }

    /// <summary>
    /// The visual order of items with the given levels (L2): from the highest level down to the lowest odd one,
    /// every run at that level or above is reversed. Returns the item indices left to right.
    /// </summary>
    public static int[] Reorder(IReadOnlyList<byte> levels)
    {
        var order = Enumerable.Range(0, levels.Count).ToArray();
        if (levels.Count == 0)
            return order;
        var highest = levels.Max();
        var lowestOdd = levels.Where(l => l % 2 == 1).DefaultIfEmpty((byte)(highest + 1)).Min();
        for (var level = highest; level >= lowestOdd && level > 0; level--)
        {
            for (var i = 0; i < order.Length; i++)
            {
                if (levels[order[i]] < level) continue;
                var end = i;
                while (end < order.Length && levels[order[end]] >= level) end++;
                Array.Reverse(order, i, end - i);
                i = end - 1;
            }
        }
        return order;
    }
}
