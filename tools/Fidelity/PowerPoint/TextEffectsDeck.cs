using System.Runtime.Versioning;

namespace Fidelity.PowerPoint;

/// <summary>
/// Text fill and outline (gradient, pattern, no fill, outlines), text effects (shadow, glow, reflection) and
/// WordArt transforms (prstTxWarp), all through TextFrame2 as PowerPoint's own formatting dialogs write them.
/// </summary>
[SupportedOSPlatform("windows")]
static class TextEffectsDeck
{
    const int MsoTrue = PowerPointSession.MsoTrue, MsoFalse = PowerPointSession.MsoFalse;
    const int GradientHorizontal = 1, GradientVertical = 2, PatternWideDownwardDiagonal = 25;

    public static void Build(dynamic deck)
    {
        object presentation = deck;

        var fills = Slide(presentation);
        var gradient = Text(fills, 40, 40, 420, 100, "Gradient", 60);
        gradient.Font.Fill.TwoColorGradient(GradientVertical, 1);
        gradient.Font.Fill.ForeColor.RGB = CorpusGenerator.Rgb(220, 40, 40);
        gradient.Font.Fill.BackColor.RGB = CorpusGenerator.Rgb(40, 60, 220);

        var lines = Text(fills, 500, 30, 420, 160, "Gradient over\rtwo lines", 48);
        lines.Font.Fill.TwoColorGradient(GradientHorizontal, 1);
        lines.Font.Fill.ForeColor.RGB = CorpusGenerator.Rgb(240, 160, 0);
        lines.Font.Fill.BackColor.RGB = CorpusGenerator.Rgb(0, 120, 60);

        var outline = Text(fills, 40, 200, 420, 100, "Outline", 60);
        outline.Font.Fill.ForeColor.RGB = CorpusGenerator.Rgb(255, 230, 120);
        outline.Font.Line.Visible = MsoTrue;
        outline.Font.Line.ForeColor.RGB = CorpusGenerator.Rgb(200, 0, 0);
        outline.Font.Line.Weight = 2f;

        var hollow = Text(fills, 500, 200, 420, 100, "No fill", 60);
        hollow.Font.Fill.Visible = MsoFalse;
        hollow.Font.Line.Visible = MsoTrue;
        hollow.Font.Line.ForeColor.RGB = CorpusGenerator.Rgb(30, 60, 160);
        hollow.Font.Line.Weight = 1.5f;

        var pattern = Text(fills, 40, 360, 420, 100, "Pattern", 60);
        pattern.Font.Fill.Patterned(PatternWideDownwardDiagonal);
        pattern.Font.Fill.ForeColor.RGB = CorpusGenerator.Rgb(0, 90, 160);
        pattern.Font.Fill.BackColor.RGB = CorpusGenerator.Rgb(170, 220, 255);

        var dashed = Text(fills, 500, 360, 420, 100, "Dashed", 60);
        dashed.Font.Fill.ForeColor.RGB = CorpusGenerator.Rgb(255, 255, 255);
        dashed.Font.Line.Visible = MsoTrue;
        dashed.Font.Line.ForeColor.RGB = CorpusGenerator.Rgb(0, 0, 0);
        dashed.Font.Line.Weight = 1f;
        dashed.Font.Line.DashStyle = 4;   // msoLineDash

        var effects = Slide(presentation);
        var shadow = Text(effects, 40, 40, 420, 100, "Shadow", 60);
        shadow.Font.Shadow.Visible = MsoTrue;
        shadow.Font.Shadow.OffsetX = 4f;
        shadow.Font.Shadow.OffsetY = 4f;
        shadow.Font.Shadow.Blur = 4f;
        shadow.Font.Shadow.ForeColor.RGB = CorpusGenerator.Rgb(80, 80, 80);
        var glow = Text(effects, 500, 40, 420, 100, "Glow", 60);
        glow.Font.Glow.Radius = 8f;
        glow.Font.Glow.Color.RGB = CorpusGenerator.Rgb(255, 200, 0);
        var reflection = Text(effects, 40, 220, 420, 100, "Reflection", 60);
        reflection.Font.Reflection.Type = 5;   // msoReflectionType5
        var combined = Text(effects, 500, 220, 420, 100, "Combined", 60);
        combined.Font.Fill.TwoColorGradient(GradientHorizontal, 1);
        combined.Font.Fill.ForeColor.RGB = CorpusGenerator.Rgb(120, 0, 180);
        combined.Font.Fill.BackColor.RGB = CorpusGenerator.Rgb(255, 120, 200);
        combined.Font.Line.Visible = MsoTrue;
        combined.Font.Line.ForeColor.RGB = CorpusGenerator.Rgb(60, 0, 90);
        combined.Font.Line.Weight = 1f;
        combined.Font.Shadow.Visible = MsoTrue;
        combined.Font.Shadow.OffsetX = 3f;
        combined.Font.Shadow.OffsetY = 3f;

        // More effects on text: inner and scaled shadows, a soft wide shadow, transparent glow, reflections.
        var more = Slide(presentation);
        var inner = Text(more, 40, 20, 420, 100, "Inner shadow", 54);
        inner.Font.Fill.ForeColor.RGB = CorpusGenerator.Rgb(240, 200, 60);
        inner.Font.Shadow.Visible = MsoTrue;
        inner.Font.Shadow.Style = 1;   // msoShadowStyleInnerShadow
        inner.Font.Shadow.OffsetX = 3f;
        inner.Font.Shadow.OffsetY = 3f;
        inner.Font.Shadow.Blur = 3f;
        inner.Font.Shadow.ForeColor.RGB = CorpusGenerator.Rgb(120, 60, 0);
        var scaled = Text(more, 500, 20, 420, 100, "Scaled shadow", 54);
        scaled.Font.Shadow.Visible = MsoTrue;
        scaled.Font.Shadow.OffsetX = 6f;
        scaled.Font.Shadow.OffsetY = -4f;
        scaled.Font.Shadow.Size = 110f;
        scaled.Font.Shadow.Blur = 2f;
        scaled.Font.Shadow.Transparency = 0.4f;
        scaled.Font.Shadow.ForeColor.RGB = CorpusGenerator.Rgb(0, 80, 160);
        var soft = Text(more, 40, 160, 420, 100, "Soft shadow", 54);
        soft.Font.Shadow.Visible = MsoTrue;
        soft.Font.Shadow.OffsetX = 0f;
        soft.Font.Shadow.OffsetY = 8f;
        soft.Font.Shadow.Blur = 14f;
        soft.Font.Shadow.Transparency = 0.3f;
        soft.Font.Shadow.ForeColor.RGB = CorpusGenerator.Rgb(0, 0, 0);
        var halo = Text(more, 500, 160, 420, 100, "Soft glow", 54);
        halo.Font.Fill.ForeColor.RGB = CorpusGenerator.Rgb(255, 255, 255);
        halo.Font.Glow.Radius = 14f;
        halo.Font.Glow.Color.RGB = CorpusGenerator.Rgb(40, 90, 220);
        halo.Font.Glow.Transparency = 0.5f;
        var tight = Text(more, 40, 300, 420, 100, "Tight reflection", 48);
        tight.Font.Reflection.Type = 1;   // msoReflectionType1: tight, touching
        var half = Text(more, 500, 300, 420, 100, "Half reflection", 48);
        half.Font.Fill.ForeColor.RGB = CorpusGenerator.Rgb(160, 30, 60);
        half.Font.Reflection.Type = 8;   // msoReflectionType8: half, 4 pt offset

        var warps = Slide(presentation);
        int[] formats = [1, 4, 8, 12, 16, 20, 24, 28];
        for (var i = 0; i < formats.Length; i++)
        {
            var box = warps.Shapes.AddTextbox(1, 30 + (i % 4) * 230, 40 + (i / 4) * 240, 210, 180);
            box.TextFrame2.AutoSize = 0;
            box.TextFrame2.WordWrap = MsoTrue;
            box.TextFrame2.TextRange.Text = "WordArt";
            box.TextFrame2.TextRange.Font.Size = 40;
            box.TextFrame2.TextRange.Font.Bold = MsoTrue;
            box.TextFrame2.TextRange.Font.Fill.ForeColor.RGB = CorpusGenerator.Rgb(30, 90, 160);
            box.TextFrame2.WarpFormat = formats[i];
            box.Height = 180;
        }
    }

    static dynamic Slide(object deck) => CorpusGenerator.AddSlide((dynamic)deck, CorpusGenerator.LayoutBlank);

    /// <summary>A text box with one run; returns its TextRange2 for formatting.</summary>
    static dynamic Text(dynamic slide, float left, float top, float width, float height, string text, float size)
    {
        var box = slide.Shapes.AddTextbox(1, left, top, width, height);
        box.TextFrame2.WordWrap = MsoFalse;
        var range = box.TextFrame2.TextRange;
        range.Text = text;
        range.Font.Name = "Aptos";
        range.Font.Size = size;
        range.Font.Bold = MsoTrue;
        return range;
    }
}
