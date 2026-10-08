using System.Runtime.Versioning;

namespace Fidelity.PowerPoint;

/// <summary>
/// The presentation deck: header and footer, a hidden slide with speaker notes, alt text, hidden and decorative
/// shapes, sections, a custom show, slide show settings and document properties.
/// </summary>
[SupportedOSPlatform("windows")]
static class PresentationDeck
{
    const int MsoTrue = PowerPointSession.MsoTrue, MsoFalse = PowerPointSession.MsoFalse;
    const int Rectangle = 1, Oval = 9;

    public static void Build(dynamic deck)
    {
        object presentation = deck;
        var first = Slide(presentation, CorpusGenerator.LayoutTitle);
        first.Shapes.Placeholders[1].TextFrame.TextRange.Text = "Presentation features";
        first.Shapes.Placeholders[2].TextFrame.TextRange.Text = "Footer, sections, notes and accessibility";
        Footer(first);

        var hidden = Slide(presentation, CorpusGenerator.LayoutText);
        hidden.Shapes.Placeholders[1].TextFrame.TextRange.Text = "Hidden slide with notes";
        hidden.Shapes.Placeholders[2].TextFrame.TextRange.Text = "This slide is skipped in the slide show.";
        hidden.SlideShowTransition.Hidden = MsoTrue;
        hidden.NotesPage.Shapes.Placeholders[2].TextFrame.TextRange.Text = "Speaker notes: mention the hidden slide.\rSecond paragraph.";
        Footer(hidden);

        var shapes = Slide(presentation, CorpusGenerator.LayoutTitleOnly);
        shapes.Shapes.Placeholders[1].TextFrame.TextRange.Text = "Alt text, hidden and decorative shapes";
        var described = shapes.Shapes.AddShape(Rectangle, 80, 160, 240, 160);
        described.AlternativeText = "A blue rectangle";
        described.Title = "Box";
        var invisible = shapes.Shapes.AddShape(Oval, 360, 160, 240, 160);
        invisible.Visible = MsoFalse;
        var decorative = shapes.Shapes.AddShape(Oval, 640, 160, 240, 160);
        try { decorative.Decorative = MsoTrue; } catch (Exception) { /* older PowerPoint */ }
        Footer(shapes);

        dynamic p = presentation;
        p.SectionProperties.AddBeforeSlide(1, "Introduction");
        p.SectionProperties.AddBeforeSlide(3, "Details");
        p.SlideShowSettings.NamedSlideShows.Add("Short version", new[] { (int)first.SlideID, (int)shapes.SlideID });
        p.SlideShowSettings.LoopUntilStopped = MsoTrue;
        p.BuiltInDocumentProperties.Item("Title").Value = "Presentation features";
        p.BuiltInDocumentProperties.Item("Subject").Value = "Generated test deck";
    }

    static dynamic Slide(object deck, int layout) => CorpusGenerator.AddSlide((dynamic)deck, layout);

    /// <summary>Footer text, slide number and a fixed date (a date field would change the reference every day).</summary>
    static void Footer(dynamic slide)
    {
        var footer = slide.HeadersFooters;
        footer.Footer.Visible = MsoTrue;
        footer.Footer.Text = "FreeSlate fidelity test";
        footer.SlideNumber.Visible = MsoTrue;
        footer.DateAndTime.Visible = MsoTrue;
        footer.DateAndTime.UseFormat = MsoFalse;
        footer.DateAndTime.Text = "01.01.2026";
    }
}
