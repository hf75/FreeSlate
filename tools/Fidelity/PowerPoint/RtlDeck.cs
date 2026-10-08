using System.Runtime.Versioning;

namespace Fidelity.PowerPoint;

/// <summary>
/// Right-to-left text (catalog: text.rtl): Arabic and Hebrew paragraphs with left-to-right numbers and words in
/// them, left-to-right paragraphs with right-to-left words, numbering on the right, alignments, wrapping, justified
/// text and large text drawn as outlines.
/// </summary>
[SupportedOSPlatform("windows")]
static class RtlDeck
{
    const int RightToLeft = 2;   // msoTextDirectionRightToLeft
    const int AlignLeft = 1, AlignCenter = 2, AlignJustify = 4;

    static readonly string Arabic = new([(char)0x0645, (char)0x0631, (char)0x062D, (char)0x0628, (char)0x0627, ' ',
        (char)0x0628, (char)0x0627, (char)0x0644, (char)0x0639, (char)0x0627, (char)0x0644, (char)0x0645]);

    static readonly string Hebrew = new([(char)0x05E9, (char)0x05DC, (char)0x05D5, (char)0x05DD, ' ',
        (char)0x05E2, (char)0x05D5, (char)0x05DC, (char)0x05DD]);

    public static void Build(dynamic deck)
    {
        object presentation = deck;
        var slide = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        string Repeat(string text, int count) => string.Join(' ', Enumerable.Repeat(text, count));
        (string Text, bool Rtl, int? Align, float Size, bool Numbered)[] boxes =
        [
            (Arabic, true, null, 20, false),
            ($"{Arabic} 2024 PowerPoint {Arabic}", true, null, 20, false),
            ($"Hello {Arabic} world", false, null, 20, false),
            ($"{Hebrew}\r{Hebrew} 123\r{Hebrew}", true, null, 20, true),
            (Arabic, true, AlignLeft, 20, false),
            (Repeat(Arabic, 8), true, null, 20, false),
            ($"{Hebrew} (abc) 12.5%", true, null, 20, false),
            (Arabic, true, null, 40, false),
        ];
        Place(slide, boxes);

        var more = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        (string Text, bool Rtl, int? Align, float Size, bool Numbered)[] others =
        [
            (Repeat(Hebrew, 3), true, AlignCenter, 20, false),
            (Repeat(Arabic, 9), true, AlignJustify, 20, false),
            ($"{Hebrew} 100% {Hebrew} [1] {Hebrew}", true, null, 24, false),
            ($"Price: 100 {Hebrew}", false, null, 24, false),
        ];
        Place(more, others);
    }

    static void Place(object target, (string Text, bool Rtl, int? Align, float Size, bool Numbered)[] boxes)
    {
        dynamic slide = target;
        for (var i = 0; i < boxes.Length; i++)
        {
            var box = slide.Shapes.AddTextbox(1, 30 + (i % 2) * 470, 20 + (i / 2) * 130, 440, 110);
            box.Line.Visible = PowerPointSession.MsoTrue;
            box.Line.ForeColor.RGB = CorpusGenerator.Rgb(192, 192, 192);
            box.TextFrame.WordWrap = PowerPointSession.MsoTrue;
            box.TextFrame.AutoSize = 0;
            var range = box.TextFrame.TextRange;
            range.Text = boxes[i].Text;
            range.Font.Size = boxes[i].Size;
            if (boxes[i].Rtl)
                range.ParagraphFormat.TextDirection = RightToLeft;
            if (boxes[i].Numbered)
                range.ParagraphFormat.Bullet.Type = 2;   // ppBulletNumbered
            if (boxes[i].Align is { } align)
                range.ParagraphFormat.Alignment = align;
        }
    }
}
