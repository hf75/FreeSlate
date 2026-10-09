using System.Runtime.Versioning;

namespace Fidelity.PowerPoint;

/// <summary>
/// Slide transitions (catalog: transition.classic, transition.office2010, transition.office2013): one slide per
/// transition and direction, each entered from the slide before it, the slides in two designs that take turns (a dark
/// one and a light one, with shapes in the corners and a title) so that every movement shows. The transitions run one
/// second; tools/Fidelity transitions compares them with PowerPoint's video.
/// </summary>
[SupportedOSPlatform("windows")]
static class TransitionsDeck
{
    // MsoAutoShapeType
    const int Rectangle = 1, Oval = 9, IsoscelesTriangle = 7, Star5 = 92;

    // PpEntryEffect values and the markup PowerPoint writes for them (enumerated with PowerPoint 16).
    public static readonly int[] Classic =
    [
        257, 258, 769, 770, 1025, 1026, 1281, 1282, 1283, 1284, 1285, 1286, 1287, 1288, 1537, 1793, 2049, 2050, 2051,
        2052, 2053, 2054, 2055, 2056, 2305, 2306, 2565, 2566, 2567, 2568, 2817, 2818, 2819, 2820, 3073, 3074, 3585, 3586,
        3587, 3588, 3845, 3846, 3847, 3848, 3849, 3850, 3851, 3852, 3853, 3854, 3855, 3856, 3857, 3858, 3859, 3860, 3861,
    ];

    public static readonly int[] Office2010 =
    [
        3862, 3863, 3864, 3865, 3866, 3867, 3868, 3869, 3870, 3871, 3872, 3873, 3874, 3875, 3876, 3877, 3878, 3879, 3880,
        3881, 3882, 3883, 3884, 3885, 3886, 3887, 3888, 3889, 3890, 3891, 3892, 3893, 3894, 3895, 3896, 3897, 3898, 3899,
        3900, 3901, 3902, 3905, 3906, 3909, 3910, 3911, 3912, 3913, 3914, 3915, 3916, 3917, 3918, 3919, 3920, 3921, 3922,
        3923, 3924, 3925, 3926, 3927, 3928, 3929, 3930, 3931, 3932, 3933,
    ];

    public static readonly int[] Office2013 =
    [
        3934, 3935, 3936, 3937, 3938, 3939, 3940, 3941, 3942, 3943, 3944, 3945, 3946, 3947, 3948, 3949, 3950, 3951, 3952, 3953,
    ];

    // PpEntryEffect: morph by object, by word, by character.
    const int MorphObjects = 3954, MorphWords = 3955, MorphCharacters = 3956;

    /// <summary>
    /// Morph (catalog: transition.morph): a slide and copies of it whose shapes moved, grew, turned and changed color,
    /// a shape that comes and one that goes, text whose words and letters change places, and two shapes of different
    /// geometry that !! names pair.
    /// </summary>
    public static void BuildMorph(dynamic deck)
    {
        object presentation = deck;
        var first = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        first.FollowMasterBackground = PowerPointSession.MsoFalse;
        first.Background.Fill.Solid();
        first.Background.Fill.ForeColor.RGB = CorpusGenerator.Rgb(242, 242, 242);
        Text(Named(first, "Title", first.Shapes.AddTextbox(1, 40f, 30f, 600f, 60f)), "Morph", 36);
        Named(first, "Box", Filled(first.Shapes.AddShape(Rectangle, 60f, 140f, 220f, 140f), CorpusGenerator.Rgb(68, 114, 196)));
        Named(first, "Ball", Filled(first.Shapes.AddShape(Oval, 640f, 140f, 200f, 200f), CorpusGenerator.Rgb(237, 125, 49)));
        Named(first, "Peak", Filled(first.Shapes.AddShape(IsoscelesTriangle, 360f, 320f, 180f, 160f), CorpusGenerator.Rgb(112, 173, 71)));
        Named(first, "Leaving", Filled(first.Shapes.AddShape(Rectangle, 760f, 420f, 140f, 80f), CorpusGenerator.Rgb(165, 165, 165)));
        Text(Named(first, "Words", first.Shapes.AddTextbox(1, 60f, 420f, 560f, 60f)), "one two three four", 32);

        // Shapes moved, grown, turned and recolored; one gone, one new.
        var second = first.Duplicate()[1];
        second.Shapes["Box"].Left = 600f; second.Shapes["Box"].Top = 300f; second.Shapes["Box"].Width = 300f; second.Shapes["Box"].Height = 200f;
        second.Shapes["Ball"].Left = 80f; second.Shapes["Ball"].Top = 120f; second.Shapes["Ball"].Width = 120f; second.Shapes["Ball"].Height = 120f;
        second.Shapes["Ball"].Fill.ForeColor.RGB = CorpusGenerator.Rgb(112, 48, 160);
        second.Shapes["Peak"].Rotation = 90f;
        second.Shapes["Leaving"].Delete();
        Named(second, "Coming", Filled(second.Shapes.AddShape(Star5, 380f, 100f, 160f, 160f), CorpusGenerator.Rgb(255, 192, 0)));
        second.SlideShowTransition.EntryEffect = MorphObjects;
        second.SlideShowTransition.Duration = 1f;

        // Words change places.
        var third = second.Duplicate()[1];
        third.Shapes["Words"].TextFrame.TextRange.Text = "four three two one";
        third.SlideShowTransition.EntryEffect = MorphWords;
        third.SlideShowTransition.Duration = 1f;

        // Letters change places.
        var fourth = third.Duplicate()[1];
        fourth.Shapes["Words"].TextFrame.TextRange.Text = "one two three four";
        fourth.Shapes["Title"].TextFrame.TextRange.Text = "Mprho";
        fourth.SlideShowTransition.EntryEffect = MorphCharacters;
        fourth.SlideShowTransition.Duration = 1f;

        // Different shapes paired by their !! names.
        var fifth = fourth.Duplicate()[1];
        fifth.Shapes["Box"].Name = "!!Pair";
        var circle = Filled(fifth.Shapes.AddShape(Oval, 60f, 140f, 220f, 220f), CorpusGenerator.Rgb(68, 114, 196));
        circle.Name = "!!Pair";
        fourth.Shapes["Box"].Name = "!!Pair";
        fifth.Shapes["!!Pair"].Delete();
        fifth.SlideShowTransition.EntryEffect = MorphObjects;
        fifth.SlideShowTransition.Duration = 1f;
    }

    static dynamic Filled(dynamic shape, int color)
    {
        shape.Fill.ForeColor.RGB = color;
        shape.Line.Visible = PowerPointSession.MsoFalse;
        return shape;
    }

    static dynamic Named(dynamic slide, string name, dynamic shape)
    {
        shape.Name = name;
        return shape;
    }

    static void Text(dynamic shape, string text, int size)
    {
        shape.TextFrame.TextRange.Text = text;
        shape.TextFrame.TextRange.Font.Size = size;
    }

    public static void BuildClassic(dynamic deck) => Build(deck, Classic);
    public static void Build2010(dynamic deck) => Build(deck, Office2010);
    public static void Build2013(dynamic deck) => Build(deck, Office2013);

    static void Build(dynamic deck, int[] effects)
    {
        object presentation = deck;
        Design((dynamic)presentation, 0, "Start");
        for (var i = 0; i < effects.Length; i++)
        {
            var slide = Design((dynamic)presentation, i + 1, $"Transition {effects[i]}");
            slide.SlideShowTransition.EntryEffect = effects[i];
            slide.SlideShowTransition.Duration = 1f;
        }
    }

    /// <summary>A slide in the dark (even) or light (odd) design.</summary>
    static dynamic Design(dynamic presentation, int index, string title)
    {
        var slide = CorpusGenerator.AddSlide(presentation, CorpusGenerator.LayoutBlank);
        var dark = index % 2 == 0;
        slide.FollowMasterBackground = PowerPointSession.MsoFalse;
        slide.Background.Fill.Solid();
        slide.Background.Fill.ForeColor.RGB = dark ? CorpusGenerator.Rgb(31, 78, 121) : CorpusGenerator.Rgb(251, 229, 214);
        var text = slide.Shapes.AddTextbox(1, 40f, 30f, 880f, 70f);
        var range = text.TextFrame.TextRange;
        range.Text = title;
        range.Font.Size = 40;
        range.Font.Color.RGB = dark ? CorpusGenerator.Rgb(255, 255, 255) : CorpusGenerator.Rgb(132, 60, 12);
        if (dark)
        {
            Shape(slide, Oval, 640f, 150f, 260f, 260f, CorpusGenerator.Rgb(255, 192, 0));
            Shape(slide, Rectangle, 60f, 330f, 400f, 150f, CorpusGenerator.Rgb(255, 255, 255));
        }
        else
        {
            Shape(slide, IsoscelesTriangle, 60f, 140f, 300f, 260f, CorpusGenerator.Rgb(112, 173, 71));
            Shape(slide, Rectangle, 560f, 300f, 360f, 200f, CorpusGenerator.Rgb(68, 114, 196));
        }
        return slide;
    }

    static void Shape(dynamic slide, int type, float left, float top, float width, float height, int color)
    {
        var shape = slide.Shapes.AddShape(type, left, top, width, height);
        shape.Fill.ForeColor.RGB = color;
        shape.Line.Visible = PowerPointSession.MsoFalse;
    }
}
