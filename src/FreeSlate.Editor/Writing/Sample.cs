using System.Globalization;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace FreeSlate.Editor.Writing;

/// <summary>
/// A short text with every level and meaning so far, to try the tones and the meanings on while there is no other way yet to give
/// them (`--sample`). A paragraph is its level's style and its text, where {Style|text} gives text a meaning.
/// </summary>
public static partial class Sample
{
    static readonly (string? Level, string Text)[] Moth =
    [
        ("Title", "Die Motte im Relais"),
        (null, "1947 lieferte der Rechner {Term|Mark II} plötzlich falsche Ergebnisse. Das Team suchte den Fehler {Emphasized|nicht} im Programm, sondern im Gehäuse – und fand zwischen den Kontakten eines {Term|Relais} eine Motte."),
        (null, "Die Motte wurde ins Logbuch geklebt, daneben steht: {Quotation|„First actual case of bug being found.“}"),
        ("Heading1", "Älter als die Motte"),
        (null, "Das Wort {Term|Bug} für einen technischen Fehler war damals schon Jahrzehnte alt; Thomas Edison benutzte es bereits 1878 in einem Brief. {Important|Die Motte hat den Begriff also nicht erfunden – sie hat ihn berühmt gemacht.}"),
        (null, "Erzählt hat die Geschichte vor allem Grace Hopper, die am Mark II arbeitete. {OpenQuestion|Klären: Wer hat die Motte tatsächlich entdeckt?}"),
        ("Heading2", "Das Logbuch heute"),
        (null, "Das Logbuch mit der Motte gehört heute zur Sammlung des National Museum of American History in Washington."),
    ];

    [GeneratedRegex(@"\{(\w+)\|([^}]*)\}")]
    private static partial Regex Marked();

    /// <summary>The sample as a document of the given tone, as opened: nothing to undo.</summary>
    public static WordDocument Document(Tone tone)
    {
        var culture = CultureInfo.GetCultureInfo("de-DE");
        using var made = WordDocument.New("Inter", 10.5, EngineFonts.Locator, culture, tone.Styles());
        var empty = made.Beginning!.Value;
        var body = new StringBuilder();
        foreach (var (level, text) in Moth)
        {
            body.Append("<w:p>");
            if (level != null)
                body.Append($"""<w:pPr><w:pStyle w:val="{level}"/></w:pPr>""");
            var at = 0;
            foreach (Match match in Marked().Matches(text))
            {
                Run(body, null, text[at..match.Index]);
                Run(body, match.Groups[1].Value, match.Groups[2].Value);
                at = match.Index + match.Length;
            }
            Run(body, null, text[at..]);
            body.Append("</w:p>");
        }
        // After the new document's empty paragraph, which goes then: a body's last paragraph stays.
        made.InsertBlocks(empty, body.ToString());
        made.Remove(empty);
        using var saved = new MemoryStream();
        made.Save(saved);
        return WordDocument.Open(saved.ToArray(), fonts: EngineFonts.Locator, culture: culture);
    }

    static void Run(StringBuilder body, string? style, string text)
    {
        if (text.Length == 0)
            return;
        body.Append("<w:r>");
        if (style != null)
            body.Append($"""<w:rPr><w:rStyle w:val="{style}"/></w:rPr>""");
        body.Append($"""<w:t xml:space="preserve">{SecurityElement.Escape(text)}</w:t></w:r>""");
    }
}
