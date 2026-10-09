using System.Runtime.Versioning;

namespace Fidelity.PowerPoint;

/// <summary>
/// Animation timing details (catalog: animation.timing, animation.targets, animation.media): text built by letter and
/// by word (p:iterate), after effects (dim, hide after the animation, hide on the next click), repetitions, smooth
/// and bouncing ends, and an effect with a sound.
/// </summary>
[SupportedOSPlatform("windows")]
static class TextAnimationsDeck
{
    const int MsoTrue = PowerPointSession.MsoTrue;
    const int Rectangle = 1, RoundedRectangle = 5, Oval = 9;
    // MsoAnimEffect, MsoAnimTriggerType, MsoAnimateByLevel, MsoAnimDirection
    const int EffectAppear = 1, EffectFly = 2, EffectFade = 10, EffectWipe = 22, EffectZoom = 23, EffectBounce = 26, EffectSpin = 61;
    const int OnClick = 1, WithPrevious = 2, AfterPrevious = 3, LevelNone = 0, DirectionBottom = 11, DirectionLeft = 4;
    // MsoAnimTextUnitEffect, MsoAnimAfterEffect
    const int ByCharacter = 1, ByWord = 2, AfterDim = 1, AfterHide = 2, AfterHideOnNextClick = 3;

    public static void Build(dynamic deck)
    {
        object presentation = deck;

        var letters = Slide(presentation, "By letter and by word");
        var main = letters.TimeLine.MainSequence;
        var fly = main.AddEffect(Box(letters, Rectangle, 60, 140, 400, 120, "Fly in by letter", 0), EffectFly, LevelNone, OnClick);
        fly.EffectParameters.Direction = DirectionBottom;
        fly.Timing.Duration = 0.5f;
        main.ConvertToTextUnitEffect(fly, ByCharacter);
        var fade = main.AddEffect(Box(letters, RoundedRectangle, 500, 140, 400, 120, "Fade in word by word", 1), EffectFade, LevelNone, AfterPrevious);
        fade.Timing.Duration = 0.5f;
        main.ConvertToTextUnitEffect(fade, ByWord);
        var type = main.AddEffect(Box(letters, Rectangle, 60, 320, 840, 120, "Appear like a typewriter", 2), EffectAppear, LevelNone, AfterPrevious);
        main.ConvertToTextUnitEffect(type, ByCharacter);

        var after = Slide(presentation, "After effects");
        main = after.TimeLine.MainSequence;
        var dim = main.AddEffect(Box(after, Rectangle, 60, 140, 260, 140, "Dimmed after", 0), EffectFade, LevelNone, OnClick);
        dim.Timing.Duration = 0.5f;
        main.ConvertToAfterEffect(dim, AfterDim, CorpusGenerator.Rgb(191, 191, 191));
        var hide = main.AddEffect(Box(after, Oval, 350, 140, 260, 140, "Hidden after", 1), EffectZoom, LevelNone, AfterPrevious);
        hide.Timing.Duration = 0.5f;
        main.ConvertToAfterEffect(hide, AfterHide);
        var later = main.AddEffect(Box(after, RoundedRectangle, 640, 140, 260, 140, "Hidden on the next click", 2), EffectWipe, LevelNone, AfterPrevious);
        later.EffectParameters.Direction = DirectionLeft;
        later.Timing.Duration = 0.5f;
        main.ConvertToAfterEffect(later, AfterHideOnNextClick);
        main.AddEffect(Box(after, Rectangle, 350, 340, 260, 120, "Next click", 3), EffectAppear, LevelNone, OnClick);

        // When after effects happen: a pause before the next effect, and another before the next click.
        var waits = Slide(presentation, "When after effects happen");
        main = waits.TimeLine.MainSequence;
        var dimmed = main.AddEffect(Box(waits, Rectangle, 60, 140, 260, 140, "Dim, then wait", 0), EffectFade, LevelNone, OnClick);
        dimmed.Timing.Duration = 0.5f;
        main.ConvertToAfterEffect(dimmed, AfterDim, CorpusGenerator.Rgb(191, 191, 191));
        var waited = main.AddEffect(Box(waits, Oval, 350, 140, 260, 140, "After 1 s", 1), EffectFade, LevelNone, AfterPrevious);
        waited.Timing.Duration = 0.5f;
        waited.Timing.TriggerDelayTime = 1f;
        var hidden = main.AddEffect(Box(waits, RoundedRectangle, 640, 140, 260, 140, "Hide on next click", 2), EffectFade, LevelNone, AfterPrevious);
        hidden.Timing.Duration = 0.5f;
        main.ConvertToAfterEffect(hidden, AfterHideOnNextClick);
        var next = main.AddEffect(Box(waits, Rectangle, 350, 340, 260, 120, "After 1 s too", 3), EffectFade, LevelNone, AfterPrevious);
        next.Timing.Duration = 0.5f;
        next.Timing.TriggerDelayTime = 1f;
        main.AddEffect(Box(waits, Oval, 640, 340, 260, 120, "Click", 1), EffectFade, LevelNone, OnClick).Timing.Duration = 0.5f;

        var timing = Slide(presentation, "Repeats, smooth and bouncing ends");
        main = timing.TimeLine.MainSequence;
        var spin = main.AddEffect(Box(timing, Rectangle, 60, 150, 240, 140, "Spin 2 times", 0), EffectSpin, LevelNone, OnClick);
        spin.Timing.Duration = 0.6f;
        spin.Timing.RepeatCount = 2;
        var rewind = main.AddEffect(Box(timing, Oval, 360, 150, 240, 140, "Rewind", 1), EffectFly, LevelNone, AfterPrevious);
        rewind.EffectParameters.Direction = DirectionLeft;
        rewind.Timing.Duration = 0.6f;
        rewind.Timing.RewindAtEnd = MsoTrue;
        var smooth = main.AddEffect(Box(timing, RoundedRectangle, 660, 150, 240, 140, "Bounce end", 2), EffectFly, LevelNone, AfterPrevious);
        smooth.EffectParameters.Direction = DirectionBottom;
        smooth.Timing.Duration = 1f;
        try { smooth.Timing.BounceEnd = MsoTrue; smooth.Timing.BounceEndIntensity = 0.5f; } catch (Exception) { /* PowerPoint 2010 and later */ }
        var bounce = main.AddEffect(Box(timing, Rectangle, 210, 340, 240, 140, "Bounce", 3), EffectBounce, LevelNone, AfterPrevious);
        bounce.Timing.Duration = 1f;
        var eased = main.AddEffect(Box(timing, Oval, 510, 340, 240, 140, "Smooth start and end", 4), EffectFly, LevelNone, AfterPrevious);
        eased.EffectParameters.Direction = DirectionLeft;
        eased.Timing.Duration = 1f;
        eased.Timing.SmoothStart = MsoTrue;
        eased.Timing.SmoothEnd = MsoTrue;
    }

    static dynamic Slide(object deck, string title)
    {
        dynamic presentation = deck;
        var slide = CorpusGenerator.AddSlide(presentation, CorpusGenerator.LayoutTitleOnly);
        slide.Shapes.Placeholders[1].TextFrame.TextRange.Text = title;
        return slide;
    }

    static dynamic Box(dynamic slide, int type, float left, float top, float width, float height, string text, int color)
    {
        var shape = slide.Shapes.AddShape(type, left, top, width, height);
        shape.Fill.ForeColor.RGB = color switch
        {
            0 => CorpusGenerator.Rgb(68, 114, 196),
            1 => CorpusGenerator.Rgb(237, 125, 49),
            2 => CorpusGenerator.Rgb(112, 173, 71),
            _ => CorpusGenerator.Rgb(165, 165, 165),
        };
        shape.Line.Visible = 0;
        var range = shape.TextFrame.TextRange;
        range.Text = text;
        range.Font.Size = 28;
        range.Font.Color.RGB = CorpusGenerator.Rgb(255, 255, 255);
        return shape;
    }
}
