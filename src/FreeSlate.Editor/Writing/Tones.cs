using System.Security;

namespace FreeSlate.Editor.Writing;

/// <summary>
/// A tone: how all of a document looks at once, where its text says only what it means. It is a set of style definitions
/// (Tones/{Key}.xml) for the levels of the paragraphs and the meanings (see Meanings), which the document takes as Word takes a style
/// set (IEditableDocument.DefineStyles); a hidden style "Tone" in it says which tone the document has.
/// </summary>
public sealed record Tone(string Key, string NameKey)
{
    /// <summary>The style definitions, the meanings' styles named in the language of the interface.</summary>
    public string Styles()
    {
        using var stream = typeof(Tone).Assembly.GetManifestResourceStream($"FreeSlate.Editor.Tones.{Key}.xml")
            ?? throw new InvalidOperationException($"The tone {Key} has no styles.");
        using var reader = new StreamReader(stream);
        var markup = reader.ReadToEnd();
        foreach (var meaning in Meanings.All)
            markup = markup.Replace("{" + meaning.Style + "}", SecurityElement.Escape(Meanings.Name(meaning)), StringComparison.Ordinal);
        return markup.Replace("</w:styles>", $"""<w:style w:type="character" w:customStyle="1" w:styleId="Tone"><w:name w:val="Tone {Key}" /><w:hidden /><w:semiHidden /></w:style></w:styles>""",
            StringComparison.Ordinal);
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
