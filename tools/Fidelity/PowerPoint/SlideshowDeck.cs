using System.Runtime.Versioning;

namespace Fidelity.PowerPoint;

/// <summary>
/// The slide show deck: transitions (classic, Office 2010 and 2013), entrance, emphasis, exit and motion path
/// animations with every trigger type, text built by paragraph, a trigger on a shape, action settings and
/// hyperlinks, and automatic advance. The references show the slides after all animations.
/// </summary>
[SupportedOSPlatform("windows")]
static class SlideshowDeck
{
    const int MsoTrue = PowerPointSession.MsoTrue;
    const int Rectangle = 1, RoundedRectangle = 5, Oval = 9;

    // PpEntryEffect
    const int Fade = 1793, PushLeft = 3853, WipeLeft = 2817, CoverDown = 1284, VortexLeft = 3863, Curtains = 3938;

    // MsoAnimEffect, MsoAnimTriggerType, MsoAnimateByLevel, MsoAnimDirection
    const int EffectAppear = 1, EffectFly = 2, EffectFade = 10, EffectSplit = 16, EffectWipe = 22, EffectZoom = 23;
    const int EffectFillColor = 54, EffectGrowShrink = 59, EffectSpin = 61, EffectTransparency = 62;
    const int EffectPathCircle = 86, EffectPathDown = 127, EffectPathRight = 149;
    const int OnClick = 1, WithPrevious = 2, AfterPrevious = 3, OnShapeClick = 4;
    const int LevelNone = 0, ByFirstLevel = 2;
    const int DirectionBottom = 11, DirectionLeft = 4;

    public static void Build(dynamic deck)
    {
        object presentation = deck;

        var title = Slide(presentation, CorpusGenerator.LayoutTitle);
        title.Shapes.Placeholders[1].TextFrame.TextRange.Text = "Slide show";
        title.Shapes.Placeholders[2].TextFrame.TextRange.Text = "Transitions, animations and links";
        Transition(title, Fade, 0.7f);

        var entrance = Slide(presentation, CorpusGenerator.LayoutTitleOnly);
        entrance.Shapes.Placeholders[1].TextFrame.TextRange.Text = "Entrance effects";
        Transition(entrance, PushLeft, 1f);
        var sequence = entrance.TimeLine.MainSequence;
        string[] labels = ["Fade", "Fly in", "Wipe", "Zoom", "Split", "Appear"];
        int[] effects = [EffectFade, EffectFly, EffectWipe, EffectZoom, EffectSplit, EffectAppear];
        int[] triggers = [OnClick, WithPrevious, AfterPrevious, OnClick, AfterPrevious, WithPrevious];
        for (var i = 0; i < labels.Length; i++)
        {
            var shape = Box(entrance, i % 3 == 1 ? Oval : RoundedRectangle, 60 + (i % 3) * 300, 160 + (i / 3) * 170, 240, 130, labels[i], i);
            var effect = sequence.AddEffect(shape, effects[i], LevelNone, triggers[i]);
            if (effects[i] == EffectFly) effect.EffectParameters.Direction = DirectionBottom;
            if (effects[i] == EffectWipe)
            {
                effect.EffectParameters.Direction = DirectionLeft;
                effect.Timing.TriggerDelayTime = 0.5f;
            }
            effect.Timing.Duration = 0.75f;
        }

        var text = Slide(presentation, CorpusGenerator.LayoutText);
        text.Shapes.Placeholders[1].TextFrame.TextRange.Text = "Text built by paragraph";
        text.Shapes.Placeholders[2].TextFrame.TextRange.Text = "First point\rSecond point\r\tA detail of the second point\rThird point";
        text.Shapes.Placeholders[2].TextFrame.TextRange.Paragraphs(3).IndentLevel = 2;
        Transition(text, WipeLeft, 1f);
        text.TimeLine.MainSequence.AddEffect(text.Shapes.Placeholders[2], EffectFade, ByFirstLevel, OnClick);

        var emphasis = Slide(presentation, CorpusGenerator.LayoutTitleOnly);
        emphasis.Shapes.Placeholders[1].TextFrame.TextRange.Text = "Emphasis and exit";
        Transition(emphasis, CoverDown, 0.75f);
        var main = emphasis.TimeLine.MainSequence;
        string[] emphasisLabels = ["Spin", "Grow", "Fill color", "Transparency", "Fade out", "Fly out"];
        int[] emphasisEffects = [EffectSpin, EffectGrowShrink, EffectFillColor, EffectTransparency, EffectFade, EffectFly];
        for (var i = 0; i < emphasisLabels.Length; i++)
        {
            var shape = Box(emphasis, Rectangle, 60 + (i % 3) * 300, 160 + (i / 3) * 170, 240, 130, emphasisLabels[i], i + 2);
            var effect = main.AddEffect(shape, emphasisEffects[i], LevelNone, i == 0 ? OnClick : AfterPrevious);
            if (i >= 4) effect.Exit = MsoTrue;
            effect.Timing.Duration = 0.6f;
        }

        var motion = Slide(presentation, CorpusGenerator.LayoutTitleOnly);
        motion.Shapes.Placeholders[1].TextFrame.TextRange.Text = "Motion paths";
        Transition(motion, VortexLeft, 1.4f);
        motion.SlideShowTransition.AdvanceOnTime = MsoTrue;
        motion.SlideShowTransition.AdvanceTime = 6f;
        var paths = motion.TimeLine.MainSequence;
        int[] pathEffects = [EffectPathCircle, EffectPathRight, EffectPathDown];
        for (var i = 0; i < pathEffects.Length; i++)
        {
            var shape = Box(motion, Oval, 100 + i * 280, 200, 120, 120, $"Path {i + 1}", i + 4);
            paths.AddEffect(shape, pathEffects[i], LevelNone, i == 0 ? WithPrevious : AfterPrevious).Timing.Duration = 1.5f;
        }

        var interactive = Slide(presentation, CorpusGenerator.LayoutTitleOnly);
        interactive.Shapes.Placeholders[1].TextFrame.TextRange.Text = "Triggers and links";
        Transition(interactive, Curtains, 2f);
        var button = Box(interactive, RoundedRectangle, 80, 170, 260, 90, "Click me", 0);
        var target = Box(interactive, Oval, 420, 150, 200, 200, "Triggered", 3);
        interactive.TimeLine.InteractiveSequences.Add().AddTriggerEffect(target, EffectZoom, OnShapeClick, button);
        var home = Box(interactive, Rectangle, 80, 300, 260, 70, "First slide", 1);
        home.ActionSettings[1].Action = 3;   // ppActionFirstSlide
        var link = Box(interactive, Rectangle, 80, 400, 520, 70, "FreeSlate on the web", 5);
        link.TextFrame.TextRange.ActionSettings[1].Hyperlink.Address = "https://example.com/freeslate";
    }

    static dynamic Slide(object deck, int layout) => CorpusGenerator.AddSlide((dynamic)deck, layout);

    static void Transition(dynamic slide, int effect, float duration)
    {
        var transition = slide.SlideShowTransition;
        transition.EntryEffect = effect;
        transition.Duration = duration;
    }

    static dynamic Box(dynamic slide, int type, float left, float top, float width, float height, string label, int accent)
    {
        var shape = slide.Shapes.AddShape(type, left, top, width, height);
        shape.Fill.ForeColor.ObjectThemeColor = 5 + accent % 6;   // msoThemeColorAccent1..6
        shape.Line.Visible = PowerPointSession.MsoFalse;
        shape.TextFrame.TextRange.Text = label;
        shape.TextFrame.TextRange.Font.Size = 24;
        shape.Name = label;
        return shape;
    }
}
