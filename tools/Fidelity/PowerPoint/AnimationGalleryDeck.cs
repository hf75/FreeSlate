using System.Runtime.Versioning;

namespace Fidelity.PowerPoint;

/// <summary>
/// PowerPoint's effect gallery (catalog: animation.behaviors, animation.presets): entrance, emphasis and exit
/// effects, eight a slide, one after the other, each on a shape with text so that filters, motions, scales and
/// color changes show.
/// </summary>
[SupportedOSPlatform("windows")]
static class AnimationGalleryDeck
{
    const int Rectangle = 1, OnClick = 1, AfterPrevious = 3, LevelNone = 0;

    // MsoAnimEffect
    static readonly (string Title, (string Name, int Effect)[] Effects, bool Exit)[] Slides =
    [
        ("Entrance: shapes and bars", [("Blinds", 3), ("Box", 4), ("Checkerboard", 5), ("Circle", 6), ("Diamond", 8), ("Dissolve", 9), ("Plus", 13), ("Random bars", 14)], false),
        ("Entrance: splits and turns", [("Peek", 12), ("Spiral", 15), ("Split", 16), ("Stretch", 17), ("Strips", 18), ("Swivel", 19), ("Wedge", 20), ("Wheel", 21)], false),
        ("Entrance: moderate", [("Boomerang", 25), ("Color reveal", 27), ("Ease in", 29), ("Float", 30), ("Grow and turn", 31), ("Light speed", 32), ("Pinwheel", 33), ("Rise up", 34)], false),
        ("Entrance: more", [("Swish", 35), ("Thin line", 36), ("Unfold", 37), ("Whip", 38), ("Ascend", 39), ("Center revolve", 40), ("Faded swivel", 41), ("Descend", 42)], false),
        ("Entrance: exciting", [("Sling", 43), ("Spinner", 44), ("Stretchy", 45), ("Zip", 46), ("Arc up", 47), ("Faded zoom", 48), ("Glide", 49), ("Expand", 50)], false),
        ("Emphasis: color", [("Fill color", 54), ("Font color", 56), ("Line color", 60), ("Brush on color", 66), ("Color blend", 68), ("Complementary", 70), ("Contrasting", 72), ("Darken", 73)], false),
        ("Emphasis: more", [("Desaturate", 74), ("Flash bulb", 75), ("Flicker", 76), ("Grow with color", 77), ("Lighten", 78), ("Teeter", 80), ("Vertical grow", 81), ("Wave", 82)], false),
        ("Exit effects", [("Blinds", 3), ("Box", 4), ("Circle", 6), ("Dissolve", 9), ("Peek", 12), ("Split", 16), ("Swivel", 19), ("Zoom", 23)], true),
    ];

    public static void Build(dynamic deck)
    {
        object presentation = deck;
        foreach (var (title, effects, exit) in Slides)
        {
            var slide = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutTitleOnly);
            slide.Shapes.Placeholders[1].TextFrame.TextRange.Text = title;
            var sequence = slide.TimeLine.MainSequence;
            for (var i = 0; i < effects.Length; i++)
            {
                var shape = slide.Shapes.AddShape(Rectangle, 40f + i % 4 * 225, 140f + i / 4 * 190, 200f, 150f);
                shape.Fill.ForeColor.RGB = CorpusGenerator.Rgb(68, 114, 196);
                shape.Line.ForeColor.RGB = CorpusGenerator.Rgb(192, 0, 0);
                shape.Line.Weight = 4f;
                var range = shape.TextFrame.TextRange;
                range.Text = effects[i].Name;
                range.Font.Size = 24;
                range.Font.Color.RGB = CorpusGenerator.Rgb(255, 255, 255);
                try
                {
                    var effect = sequence.AddEffect(shape, effects[i].Effect, LevelNone, i == 0 ? OnClick : AfterPrevious);
                    if (exit)
                        effect.Exit = PowerPointSession.MsoTrue;
                    effect.Timing.Duration = 0.8f;
                }
                catch (Exception)
                {
                    range.Text = effects[i].Name + " (n/a)";
                }
            }
        }
    }
}
