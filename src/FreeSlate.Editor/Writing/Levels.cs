using Avalonia;
using Avalonia.Controls;

namespace FreeSlate.Editor.Writing;

/// <summary>A paragraph's level: its place on the axis of structure (from -3 to +5), its paragraph style ("" the default one) and its name's key.</summary>
public sealed record Level(int Step, string Style, string NameKey)
{
    /// <summary>How deep in a list it stands: 1 a point, 2 a subpoint, bulleted or numbered; 0 outside lists.</summary>
    public int Depth => Style switch { "ListBullet" or "ListNumber" => 1, "ListBullet2" or "ListNumber2" => 2, _ => 0 };

    /// <summary>Whether it is a numbered point or subpoint (List Number, List Number 2), which counts in its list (see Lists).</summary>
    public bool Numbered => Style is "ListNumber" or "ListNumber2";
}

/// <summary>
/// The levels of paragraphs, on one axis: to the left what weighs more (section, chapter, title), to the right what is subordinate
/// (point, subpoint, numbered point and subpoint, margin note), text in the middle. The document keeps each as a paragraph style, Word's
/// own where Word has one (Title, Heading 1 and 2, List Bullet, List Bullet 2, List Number and List Number 2), whose look the tone gives
/// (see Tones); a style that is none of them counts as text. The lever goes along the axis; Tab goes along the outline (see Indented),
/// where a numbered point is a point.
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
        new(3, "ListNumber", "LevelNumber"),
        new(4, "ListNumber2", "LevelSubnumber"),
        new(5, "MarginNote", "LevelMarginNote"),
    ];

    public static Level Text => All[3];

    static Level Styled(string style) => All.First(l => l.Style == style);

    /// <summary>The level a paragraph style stands for (null: the default one).</summary>
    public static Level Of(string? style) => All.FirstOrDefault(l => l.Style.Length > 0 && l.Style == style) ?? Text;

    /// <summary>The level so many steps from another, or null past the ends of the axis.</summary>
    public static Level? Beside(Level level, int steps) =>
        level.Step + steps is var step && step >= All[0].Step && step <= All[^1].Step ? All[step - All[0].Step] : null;

    /// <summary>
    /// The level so many places from another along the outline, as Tab (1) and Shift+Tab (-1) move a paragraph: title, chapter, section,
    /// text, point, subpoint, margin note; points stay numbered or bulleted as they are, and text or a margin note becomes a numbered one
    /// where <paramref name="numbered"/> says so (the paragraph before it is one). Null past the ends.
    /// </summary>
    public static Level? Indented(Level level, int steps, bool numbered)
    {
        // The outline's places: the levels before the points by their steps, the points by their depth, the margin note after them.
        var place = (level.Depth > 0 ? level.Depth : level.Style == "MarginNote" ? 3 : level.Step) + steps;
        var points = level.Depth > 0 ? level.Numbered : numbered;
        return place switch
        {
            < -3 or > 3 => null,
            1 => Styled(points ? "ListNumber" : "ListBullet"),
            2 => Styled(points ? "ListNumber2" : "ListBullet2"),
            3 => Styled("MarginNote"),
            _ => All[place - All[0].Step],
        };
    }

    /// <summary>A level's name in the language of the interface.</summary>
    public static string Name(Level level) =>
        Application.Current?.TryFindResource(level.NameKey, out var name) == true && name is string text ? text : level.Style;
}
