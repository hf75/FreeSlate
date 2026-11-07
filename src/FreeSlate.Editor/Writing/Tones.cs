using System.Security;
using System.Xml.Linq;

namespace FreeSlate.Editor.Writing;

/// <summary>
/// A tone: how all of a document looks at once, where its text says only what it means. It is a set of style definitions for the
/// levels of the paragraphs and the meanings (see Levels, Meanings), for tables and the text in their cells (see Tables), with the
/// lists they name (Tones/{Key}.xml: a w:styles and a w:numbering element), which the document takes as Word takes a style set
/// (IEditableDocument.DefineStyles); a hidden style "Tone" in it says which tone the document has.
/// </summary>
public sealed record Tone(string Key, string NameKey)
{
    /// <summary>The style definitions and the lists' numbering definitions, the meanings', levels' and tables' own styles named in the language of the interface.</summary>
    public (string Styles, string Numbering) Definitions()
    {
        var markup = Markup();
        foreach (var (style, name) in Meanings.All.Select(m => (m.Style, Meanings.Name(m))).Concat(Levels.All.Select(l => (l.Style, Levels.Name(l))))
            .Concat(Tables.Styles.Select(s => (s.Style, Tables.Name(s)))))
        {
            if (style.Length > 0)
                markup = markup.Replace("{" + style + "}", SecurityElement.Escape(name), StringComparison.Ordinal);
        }
        var tone = XElement.Parse(markup);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var styles = tone.Element(w + "styles")!;
        // The kinds of table look beside the tone's own, made of it with other lines (see Tables.Kinds), in the tone's colours for lines.
        if (styles.Elements(w + "style").FirstOrDefault(s => (string?)s.Attribute(w + "styleId") == Tables.Table.Style) is { } own)
        {
            foreach (var kind in Tables.Kinds)
                styles.Add(Tables.Made(own, kind, (string?)tone.Attribute("lines") ?? "BFBFBF", (string?)tone.Attribute("rules") ?? "404040"));
        }
        styles.Add(new XElement(w + "style", new XAttribute(w + "type", "character"), new XAttribute(w + "customStyle", "1"), new XAttribute(w + "styleId", "Tone"),
            new XElement(w + "name", new XAttribute(w + "val", "Tone " + Key)), new XElement(w + "hidden"), new XElement(w + "semiHidden")));
        return (styles.ToString(SaveOptions.DisableFormatting), tone.Element(w + "numbering")!.ToString(SaveOptions.DisableFormatting));
    }

    /// <summary>The tone's colours for light lines and for rules (Tones/{Key}.xml, its lines and rules), hex, which its tables' lines take.</summary>
    public (string Light, string Dark) LineColors()
    {
        lock (Colors)
        {
            if (!Colors.TryGetValue(Key, out var known))
            {
                var tone = XElement.Parse(Markup());
                Colors[Key] = known = ((string?)tone.Attribute("lines") ?? "BFBFBF", (string?)tone.Attribute("rules") ?? "404040");
            }
            return known;
        }
    }

    static readonly Dictionary<string, (string Light, string Dark)> Colors = [];

    /// <summary>The families the tone's styles and lists set text in (their w:rFonts), which a page fetches before it takes the tone (see EngineFonts.Prepare).</summary>
    public IEnumerable<string> Families()
    {
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        return XElement.Parse(Markup()).Descendants(w + "rFonts")
            .SelectMany(fonts => fonts.Attributes().Where(a => a.Name.LocalName is "ascii" or "hAnsi" or "eastAsia" or "cs").Select(a => a.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    string Markup()
    {
        using var stream = typeof(Tone).Assembly.GetManifestResourceStream($"FreeSlate.Editor.Tones.{Key}.xml")
            ?? throw new InvalidOperationException($"The tone {Key} has no styles.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

/// <summary>The tones to choose from, the first the one a new document has.</summary>
public static class Tones
{
    public static readonly IReadOnlyList<Tone> All =
    [
        new("Plain", "TonePlain"),
        new("Magazine", "ToneMagazine"),
        new("Notebook", "ToneNotebook"),
    ];

    /// <summary>The tone a document has, by its hidden style "Tone"; null where it has none of these.</summary>
    public static Tone? Of(IEditableDocument document) =>
        document.StyleName("Tone") is { } name ? All.FirstOrDefault(t => name == "Tone " + t.Key) : null;
}
