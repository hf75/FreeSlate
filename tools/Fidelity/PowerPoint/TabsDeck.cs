using System.Runtime.Versioning;

namespace Fidelity.PowerPoint;

/// <summary>
/// Tab stops (catalog: text.tabs): default tabs at several default spacings, custom left, center, right and decimal
/// stops, stops with paragraph margins and first-line indents, tabs past the last custom stop and past the end of the
/// line, and tab stops in centered and right-aligned paragraphs.
/// </summary>
[SupportedOSPlatform("windows")]
static class TabsDeck
{
    // MsoTabStopType
    const int Left = 1, Center = 2, Right = 3, Decimal = 4;

    static dynamic Box(dynamic slide, float left, float top, float width, float height, string text, float size = 20)
    {
        var box = slide.Shapes.AddTextbox(1, left, top, width, height);
        box.TextFrame2.WordWrap = PowerPointSession.MsoTrue;
        box.TextFrame2.AutoSize = 0;
        box.Line.Visible = PowerPointSession.MsoTrue;
        box.Line.ForeColor.RGB = CorpusGenerator.Rgb(191, 191, 191);
        var range = box.TextFrame2.TextRange;
        range.Text = text;
        range.Font.Name = "Arial";
        range.Font.Size = size;
        return box;
    }

    static void Stops(object shape, params (int Type, float Position)[] stops)
    {
        dynamic box = shape;
        var paragraphs = box.TextFrame2.TextRange.Paragraphs;
        for (var i = 1; i <= paragraphs.Count; i++)
        {
            foreach (var (type, position) in stops)
                paragraphs.Item(i).ParagraphFormat.TabStops.Add(type, position);
        }
    }

    public static void Build(dynamic deck)
    {
        object presentation = deck;

        // Default tabs: the box's default spacing (1 inch), 0.5 inch and 2 cm.
        var defaults = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        Box(defaults, 30, 20, 900, 110, "A\tB\tC\tD\tE\r1\t22\t333\t4444\t55555\rWord\tWordWord\tW");
        var half = Box(defaults, 30, 150, 900, 110, "A\tB\tC\tD\tE\tF\tG\r1\t22\t333\t4444\t55555");
        half.TextFrame2.TextRange.ParagraphFormat.TabStops.DefaultSpacing = 36f;
        var cm = Box(defaults, 30, 280, 900, 110, "A\tB\tC\tD\tE\tF\r\tStarts with a tab\r\t\tTwo tabs");
        cm.TextFrame2.TextRange.ParagraphFormat.TabStops.DefaultSpacing = 56.7f;
        Box(defaults, 30, 410, 300, 110, "Long text before a tab\tafter\tand more\tagain");

        // Custom stops of each kind.
        var custom = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        var left = Box(custom, 30, 20, 900, 110, "Name\tValue\tNote\rApples\t12\tred\rPears\t7\tgreen");
        Stops((object)left, (Left, 200), (Left, 380));
        var right = Box(custom, 30, 150, 900, 110, "Item\tPrice\tTotal\rBooks\t12.50\t1,250.00\rPens\t3.20\t64.00");
        Stops((object)right, (Right, 300), (Right, 520));
        var center = Box(custom, 30, 280, 900, 110, "Left\tCentered\tMore\rx\tA longer centered text\ty\rx\tShort\ty");
        Stops((object)center, (Center, 330), (Center, 650));
        var dec = Box(custom, 30, 410, 900, 110, "A\t1.5\tB\t10.25\rC\t123.456\tD\t7\rE\t99\tF\t0.125");
        Stops((object)dec, (Decimal, 200), (Decimal, 520));

        // Margins and indents, tabs past the last stop and past the line.
        var margins = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        var indented = Box(margins, 30, 20, 900, 110, "First\tsecond\tthird\rMore\ttext\there");
        indented.TextFrame2.TextRange.ParagraphFormat.LeftIndent = 60f;
        indented.TextFrame2.TextRange.ParagraphFormat.FirstLineIndent = -40f;
        Stops((object)indented, (Left, 150), (Left, 300));
        var past = Box(margins, 30, 150, 900, 110, "A\tB\tC\tD\tE\tF\rx\ty\tz");
        Stops((object)past, (Left, 100), (Right, 250));
        var beyond = Box(margins, 30, 280, 400, 110, "Text\tafter a stop past the line end and more words");
        Stops((object)beyond, (Left, 380));
        var bullets = Box(margins, 30, 410, 900, 110, "One\tA\rTwo\tB\rThree\tC");
        bullets.TextFrame2.TextRange.ParagraphFormat.Bullet.Visible = PowerPointSession.MsoTrue;
        Stops((object)bullets, (Left, 250));

        // Aligned paragraphs and sizes.
        var aligned = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        var centered = Box(aligned, 30, 20, 900, 110, "A\tB\tC\rCentered\twith\ttabs");
        centered.TextFrame2.TextRange.ParagraphFormat.Alignment = 2;   // msoAlignCenter
        var rightAligned = Box(aligned, 30, 150, 900, 110, "A\tB\tC\rRight\twith\ttabs");
        rightAligned.TextFrame2.TextRange.ParagraphFormat.Alignment = 3;   // msoAlignRight
        Stops((object)rightAligned, (Left, 300));
        var big = Box(aligned, 30, 280, 900, 110, "Big\tsize\ttabs", 40);
        Stops((object)big, (Center, 450), (Right, 850));
        var small = Box(aligned, 30, 410, 900, 110, "small\ttext\twith\tmany\ttabs\tand\tdefault\tstops", 12);
        Stops((object)small, (Decimal, 100));
    }
}
