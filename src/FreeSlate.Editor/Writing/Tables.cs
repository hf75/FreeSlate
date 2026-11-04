using Avalonia;
using Avalonia.Controls;

namespace FreeSlate.Editor.Writing;

/// <summary>A style the tones give tables: its id in the document and its name's key.</summary>
public sealed record TableStyle(string Style, string NameKey);

/// <summary>
/// Tables as the tones have them: a table style for the table itself (its lines, its head) and a paragraph style for the text in its
/// cells, which every tone defines in its own way (see Tones), so that a table takes the look of the document it is in and changes
/// with its tone. The text needs a style of its own because the normal one of a tone is set for running text (spacing after,
/// justified in the magazine), and a table style cannot override a paragraph style (Word's order: table, then paragraph styles).
/// </summary>
public static class Tables
{
    public static readonly TableStyle Table = new("Table", "StyleTable");

    public static readonly TableStyle Text = new("TableText", "StyleTableText");

    public static readonly IReadOnlyList<TableStyle> Styles = [Table, Text];

    /// <summary>The numbers of columns a new table can have, in the ring that puts one in.</summary>
    public static readonly IReadOnlyList<int> Columns = [2, 3, 4, 5, 6];

    /// <summary>The rows a new table has: the head and two more; Tab in the last cell adds more.</summary>
    public const int Rows = 3;

    /// <summary>A style's name in the language of the interface.</summary>
    public static string Name(TableStyle style) =>
        Application.Current?.TryFindResource(style.NameKey, out var name) == true && name is string text ? text : style.Style;
}
