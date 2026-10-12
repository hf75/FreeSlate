using System.Globalization;
using System.Xml.Linq;

namespace FreeSlate.Docx;

/// <summary>
/// What the balloons of the markup view say (catalog: review.revisions, review.comments), in the language of Word's
/// user interface: German as Word 365 words it (measured with docx/local/markup-a and markup-c: 37 formatting changes
/// of runs and paragraphs), else English. A formatting change lists what the properties now have that the ones before
/// did not, as they are now: the font's name, size, bold and italic after "Font:", then underline, strikethrough,
/// color, caps, position, highlight, hidden and spacing; a paragraph's alignment, indents (in centimeters or inches),
/// spacing, line spacing and keep options.
/// </summary>
public sealed class Describe(CultureInfo culture)
{
    readonly bool german = culture.TwoLetterISOLanguageName == "de";

    string T(string english, string german) => this.german ? german : english;

    public string Commented(string label) => T($"Commented {label}:", $"Kommentiert {label}:");

    public string FormattedRun => T("Formatted:", "hat formatiert:");

    public string FormattedParagraph => T("Formatted:", "Formatiert:");

    public string FormattedTable => T("Formatted Table", "Formatierte Tabelle");

    /// <summary>The last of an element repeated in properties counts, as Word reads them.</summary>
    static XElement? Last(XElement? properties, string name) => properties?.Elements(Wml.W + name).LastOrDefault();

    static bool Changed(XElement? now, XElement? before, string name)
    {
        static XElement? Strip(XElement? e) => e == null ? null : new XElement(e.Name, e.Attributes().Where(a => !a.IsNamespaceDeclaration));
        return !XNode.DeepEquals(Strip(Last(now, name)), Strip(Last(before, name)));
    }

    static bool On(XElement? properties, string name) => Last(properties, name) is { } e && e.OnOff() != false;

    string Number(double value, string format = "0.##") => value.ToString(format, german ? CultureInfo.GetCultureInfo("de-DE") : CultureInfo.InvariantCulture);

    string Points(double points) => T($"{Number(points)} pt", $"{Number(points)} Pt.");

    /// <summary>A length in twips as Word's balloons give it: centimeters cut to two places in German, inches in English.</summary>
    string Length(double twips) => german
        ? Number(Math.Floor(twips / 1440 * 2.54 * 100 + 1e-6) / 100) + " cm"
        : Number(Math.Round(twips / 1440, 2)) + "\"";

    /// <summary>A run's formatting change (w:rPrChange).</summary>
    public string Run(XElement? now, XElement? before)
    {
        var font = new List<string>();
        if (Changed(now, before, "rFonts") && Last(now, "rFonts") is { } fonts && fonts.Attr("ascii") is { } ascii)
            font.Add(fonts.Attr("eastAsia") != null || fonts.Attr("cs") != null ? T($"(Default) {ascii}", $"(Standard) {ascii}") : ascii);
        if (Changed(now, before, "sz") && Last(now, "sz").Number("val") is { } half)
            font.Add(Points(half / 2));
        if (Changed(now, before, "b"))
            font.Add(On(now, "b") ? T("Bold", "Fett") : T("Not Bold", "Nicht Fett"));
        if (Changed(now, before, "i"))
            font.Add(On(now, "i") ? T("Italic", "Kursiv") : T("Not Italic", "Nicht Kursiv"));
        var parts = new List<string>();
        if (font.Count > 0)
            parts.Add(T("Font: ", "Schriftart: ") + string.Join(", ", font));
        if (Changed(now, before, "u"))
            parts.Add(Last(now, "u").Val() switch
            {
                null or "none" => T("No underline", "Nicht unterstrichen"),
                "double" => T("Double underline", "Doppelt unterstreichen"),
                _ => T("Underline", "Unterstrichen"),
            });
        if (Changed(now, before, "strike"))
            parts.Add(On(now, "strike") ? T("Strikethrough", "Durchgestrichen") : T("Not Strikethrough", "Nicht durchgestrichen"));
        if (Changed(now, before, "dstrike"))
            parts.Add(On(now, "dstrike") ? T("Double strikethrough", "Doppelt durchgestrichen") : T("Not Double strikethrough", "Nicht doppelt durchgestrichen"));
        if (Changed(now, before, "color") && Last(now, "color").Val() is { } color)
            parts.Add(T("Font color: ", "Schriftfarbe: ") + ColorName(color));
        if (Changed(now, before, "caps"))
            parts.Add(On(now, "caps") ? T("All caps", "Großbuchstaben") : T("Not All caps", "Nicht Großbuchstaben"));
        if (Changed(now, before, "smallCaps"))
            parts.Add(On(now, "smallCaps") ? T("Small caps", "Kapitälchen") : T("Not Small caps", "Nicht Kapitälchen"));
        if (Changed(now, before, "vertAlign"))
            parts.Add(Last(now, "vertAlign").Val() switch
            {
                "superscript" => T("Superscript", "Hochgestellt"),
                "subscript" => T("Subscript", "Tiefgestellt"),
                _ => T("Not Superscript/ Subscript", "Nicht Hochgestellt/ Tiefgestellt"),
            });
        if (Changed(now, before, "highlight"))
            parts.Add(Last(now, "highlight").Val() is { } h && h != "none" ? T("Highlight", "Hervorheben") : T("Not Highlight", "Nicht Hervorheben"));
        if (Changed(now, before, "vanish"))
            parts.Add(On(now, "vanish") ? T("Hidden", "Ausgeblendet") : T("Not Hidden", "Nicht Ausgeblendet"));
        if (Changed(now, before, "spacing") && Last(now, "spacing").Number("val") is { } spacing)
            parts.Add(spacing >= 0 ? T($"Expanded by  {Points(spacing / 20)}", $"Erweitert durch  {Points(spacing / 20)}")
                : T($"Condensed by  {Points(-spacing / 20)}", $"Schmal durch  {Points(-spacing / 20)}"));
        return string.Join(", ", parts);
    }

    /// <summary>A color as Word names it in balloons: red by name, any other by its parts.</summary>
    string ColorName(string hex) => hex.ToUpperInvariant() switch
    {
        "AUTO" => T("Auto", "Automatisch"),
        "FF0000" => T("Red", "Rot"),
        _ when hex.Length == 6 && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb)
            => german ? $"Benutzerdefinierte Farbe(RGB({rgb >> 16};{(rgb >> 8) & 255};{rgb & 255}))" : $"Custom Color(RGB({rgb >> 16},{(rgb >> 8) & 255},{rgb & 255}))",
        _ => hex,
    };

    /// <summary>A paragraph's formatting change (w:pPrChange).</summary>
    public string Paragraph(XElement? now, XElement? before)
    {
        string? Attr(XElement? pPr, string element, string attribute) => Last(pPr, element)?.Attr(attribute);
        var parts = new List<string>();
        if (Attr(now, "jc", "val") != Attr(before, "jc", "val"))
            parts.Add(Attr(now, "jc", "val") switch
            {
                "center" => T("Centered", "Zentriert"),
                "right" or "end" => T("Right", "Rechts"),
                "both" => T("Justified", "Block"),
                "distribute" => T("Distributed", "Verteilt"),
                _ => T("Left", "Links"),
            });
        var indents = new List<string>();
        foreach (var (attributes, english, germanName) in new[]
                 {
                     (new[] { "left", "start" }, "Left", "Links"), (new[] { "right", "end" }, "Right", "Rechts"),
                     (new[] { "firstLine" }, "First line", "Erste Zeile"), (new[] { "hanging" }, "Hanging", "Hängend"),
                 })
        {
            var value = attributes.Select(a => Attr(now, "ind", a)).FirstOrDefault(v => v != null);
            var old = attributes.Select(a => Attr(before, "ind", a)).FirstOrDefault(v => v != null);
            if (value != null && value != old && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var twips))
            {
                // With a hanging indent the left one is where the first line begins (measured: left 720 and hanging
                // 360 twips say "Links:  0,63 cm, Hängend:  0,63 cm").
                if (english == "Left" && double.TryParse(Attr(now, "ind", "hanging"), NumberStyles.Float, CultureInfo.InvariantCulture, out var hanging))
                    twips -= hanging;
                indents.Add($"{T(english, germanName)}:  {Length(twips)}");
            }
        }
        if (indents.Count > 0)
            parts.Add((indents[0].StartsWith(T("Right", "Rechts"), StringComparison.Ordinal) ? "" : T("Indent: ", "Einzug: ")) + string.Join(", ", indents));
        var spacing = new List<string>();
        foreach (var (attribute, english, germanName) in new[] { ("before", "Before", "Vor"), ("after", "After", "Nach") })
        {
            if (Attr(now, "spacing", attribute) is { } value && value != Attr(before, "spacing", attribute)
                && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var twips))
                spacing.Add($"{T(english, germanName)}:  {Points(twips / 20)}");
        }
        if (spacing.Count > 0)
            parts.Add(T("Space ", "Abstand ") + string.Join(", ", spacing));
        if (Attr(now, "spacing", "line") is { } line && line != Attr(before, "spacing", "line")
            && double.TryParse(line, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount))
        {
            var rule = Attr(now, "spacing", "lineRule") ?? "auto";
            parts.Add(T("Line spacing:  ", "Zeilenabstand:  ") + (rule switch
            {
                "exact" => T($"Exactly {Points(amount / 20)}", $"Genau {Points(amount / 20)}"),
                "atLeast" => T($"At least {Points(amount / 20)}", $"Mindestens {Points(amount / 20)}"),
                _ => amount switch
                {
                    240 => T("single", "einfach"),
                    360 => T("1.5 lines", "1,5 Zeilen"),
                    480 => T("Double", "Doppelt"),
                    _ => T($"Multiple {Number(amount / 240)} li", $"Mehrere {Number(amount / 240)} ze"),
                },
            }));
        }
        foreach (var (element, english, germanName) in new[]
                 {
                     ("keepNext", "Keep with next", "Nicht vom nächsten Absatz trennen"),
                     ("keepLines", "Keep lines together", "Diesen Absatz zusammenhalten"),
                     ("pageBreakBefore", "Page break before", "Seitenumbruch oberhalb"),
                 })
        {
            if (On(now, element) != On(before, element))
                parts.Add(On(now, element) ? T(english, germanName) : T("Don't " + char.ToLowerInvariant(english[0]) + english[1..], "Nicht: " + germanName));
        }
        return string.Join(", ", parts);
    }
}
