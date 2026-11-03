using Avalonia;
using Avalonia.Controls;

namespace FreeSlate.Editor.Writing;

/// <summary>A meaning a writer gives text instead of a look: a character style of the document, and its name's key among the texts.</summary>
public sealed record Meaning(string Style, string NameKey);

/// <summary>
/// The meanings text can have. The document keeps each as a character style (IEditableDocument.CharacterStyles), which says what the
/// text is; how it looks, the tone says (see Tones). Only meanings that show stand here: what text is without showing it comes later.
/// </summary>
public static class Meanings
{
    public static readonly IReadOnlyList<Meaning> All =
    [
        new("Important", "MeaningImportant"),
        new("Emphasized", "MeaningEmphasized"),
        new("Term", "MeaningTerm"),
        new("Quotation", "MeaningQuotation"),
        new("OpenQuestion", "MeaningOpenQuestion"),
    ];

    /// <summary>The meaning a character style stands for, or null for one that stands for none.</summary>
    public static Meaning? Of(string style) => All.FirstOrDefault(m => m.Style == style);

    /// <summary>A meaning's name in the language of the interface.</summary>
    public static string Name(Meaning meaning) =>
        Application.Current?.TryFindResource(meaning.NameKey, out var name) == true && name is string text ? text : meaning.Style;
}
