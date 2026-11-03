using Avalonia;
using Avalonia.Controls;

namespace FreeSlate.Editor.Writing;

/// <summary>A paragraph's level: its place on the axis of structure (from -3 to +3), its paragraph style ("" the default one) and its name's key.</summary>
public sealed record Level(int Step, string Style, string NameKey);

/// <summary>
/// The levels of paragraphs, on one axis: to the left what weighs more (section, chapter, title), to the right what is subordinate
/// (point, subpoint, margin note), text in the middle. The document keeps each as a paragraph style, Word's own where Word has one
/// (Title, Heading 1 and 2, List Bullet and List Bullet 2), whose look the tone gives (see Tones); a style that is none of them counts
/// as text.
/// </summary>
public static class Levels
{
    public static readonly IReadOnlyList<Level> All =
    [
        new(-3, "Title", "LevelTitle"),
        new(-2, "Heading1", "LevelChapter"),
        new(-1, "Heading2", "LevelSection"),
        new(0, "", "LevelText"),
        new(1, "ListBullet", "LevelPoint"),
        new(2, "ListBullet2", "LevelSubpoint"),
        new(3, "MarginNote", "LevelMarginNote"),
    ];

    public static Level Text => All[3];

    /// <summary>The level a paragraph style stands for (null: the default one).</summary>
    public static Level Of(string? style) => All.FirstOrDefault(l => l.Style.Length > 0 && l.Style == style) ?? Text;

    /// <summary>The level so many steps from another, or null past the ends of the axis.</summary>
    public static Level? Beside(Level level, int steps) =>
        level.Step + steps is var step && step >= All[0].Step && step <= All[^1].Step ? All[step - All[0].Step] : null;

    /// <summary>A level's name in the language of the interface.</summary>
    public static string Name(Level level) =>
        Application.Current?.TryFindResource(level.NameKey, out var name) == true && name is string text ? text : level.Style;
}
