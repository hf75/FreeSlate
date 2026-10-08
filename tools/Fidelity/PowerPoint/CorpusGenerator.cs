using System.IO.Compression;
using System.Runtime.Versioning;
using System.Xml.Linq;

namespace Fidelity.PowerPoint;

/// <summary>Layout of the text-metrics deck, shared by the generator and the calibration measurement.</summary>
public static class MetricsDeck
{
    /// <summary>Fonts of the text metrics deck; the calibration measures PowerPoint's line model for each.</summary>
    public static readonly string[] Fonts = ["Aptos", "Calibri", "Arial", "Times New Roman", "Segoe UI", "Georgia", "Cambria", "Courier New", "Verdana"];

    /// <summary>Left edges (points) of the four text boxes on every metrics slide; boxes start at <see cref="Top"/>.</summary>
    public static readonly float[] Columns = [20, 260, 500, 740];
    public const float Top = 40, Width = 200;
}

/// <summary>
/// Builds the generated test decks through PowerPoint itself, so the files contain exactly the markup PowerPoint
/// writes (style references, theme colors, autofit values …). Each deck isolates one group of features.
/// </summary>
[SupportedOSPlatform("windows")]
public static class CorpusGenerator
{
    // PpSlideLayout
    internal const int LayoutTitle = 1, LayoutText = 2, LayoutTitleOnly = 11, LayoutBlank = 12;
    // MsoTextOrientation, PpParagraphAlignment, MsoVerticalAnchor
    const int Horizontal = 1;
    const int AlignLeft = 1, AlignCenter = 2, AlignRight = 3, AlignJustify = 4;
    const int AnchorTop = 1, AnchorMiddle = 3, AnchorBottom = 4;
    // MsoAutoShapeType
    const int Rectangle = 1, RoundedRectangle = 5, Oval = 9, RightArrow = 33, Star5 = 92;

    const float SlideWidth = 960, SlideHeight = 540;   // points, 16:9

    /// <summary>The decks by name; SmartArt and chart galleries only work with a document window, which is kept minimized.</summary>
    static readonly (string Name, Action<dynamic> Build, bool Window)[] Decks =
    [
        ("geometry-presets", GeometryPresets, false),
        ("fills", Fills, false),
        ("lines", Lines, false),
        ("text-basic", TextBasic, false),
        ("transforms", Transforms, false),
        ("text-metrics", TextMetrics, false),
        ("objects", Objects, false),
        ("diagrams", Diagrams, true),
        ("charts", Charts.Build, true),
        ("presentation", PresentationDeck.Build, true),
        ("metafiles", MetafilesDeck.Build, true),
        ("pictures", PicturesDeck.Build, true),
        ("text-snap", TextSnapDeck.Build, false),
        ("slideshow", SlideshowDeck.Build, true),
        ("text-effects", TextEffectsDeck.Build, false),
        ("media", MediaDeck.Build, true),
        ("effects", EffectsDeck.Build, false),
        ("lists", ListsDeck.Build, false),
        ("tables", TablesDeck.Build, false),
        ("text-body", TextBodyDeck.Build, false),
        ("charts-more", ChartsMoreDeck.Build, true),
        ("outlines", OutlinesDeck.Build, false),
        ("text-vertical", TextVerticalDeck.Build, false),
        ("three-d", ThreeDDeck.Build, false),
        ("colormap", ColorMapDeck.Build, true),
        ("text-rtl", RtlDeck.Build, false),
        ("text-eastasian", EastAsianDeck.Build, false),
        ("ink", InkDeck.Build, false),
    ];

    /// <summary>XML-only properties some decks add after PowerPoint saved them.</summary>
    static readonly Dictionary<string, Action<string>> Patches = new()
    {
        ["outlines"] = OutlinesDeck.Patch,
        ["text-vertical"] = TextVerticalDeck.Patch,
        ["colormap"] = ColorMapDeck.Patch,
        ["text-body"] = TextBodyDeck.Patch,
        ["tables"] = TablesDeck.Patch,
        ["effects"] = EffectsDeck.Patch,
        ["text-eastasian"] = EastAsianDeck.Patch,
        ["ink"] = InkDeck.Patch,
    };

    /// <summary>Generates the named decks, or all of them.</summary>
    public static void Run(string outputDir, IReadOnlyCollection<string> only)
    {
        using var session = new PowerPointSession();
        Console.WriteLine($"PowerPoint {session.Version}");
        foreach (var (name, build, window) in Decks)
        {
            if (only.Count == 0 || only.Contains(name))
            {
                var path = Path.Combine(outputDir, name + ".pptx");
                Generate(session, path, build, window);
                if (Patches.TryGetValue(name, out var patch))
                    patch(path);
            }
        }
    }

    /// <summary>SmartArt and charts. Both galleries only work with a document window, which is kept minimized.</summary>
    static void Diagrams(dynamic presentation)
    {
        var smartArt = AddSlide(presentation, LayoutTitleOnly);
        smartArt.Shapes.Placeholders[1].TextFrame.TextRange.Text = "SmartArt";
        var layouts = presentation.Application.SmartArtLayouts;
        foreach (var (urn, left, top) in new[]
                 {
                     ("urn:microsoft.com/office/officeart/2005/8/layout/default", 40f, 130f),
                     ("urn:microsoft.com/office/officeart/2005/8/layout/process1", 500f, 130f),
                     ("urn:microsoft.com/office/officeart/2005/8/layout/cycle2", 40f, 320f),
                     ("urn:microsoft.com/office/officeart/2005/8/layout/hierarchy1", 500f, 320f),
                 })
        {
            var diagram = smartArt.Shapes.AddSmartArt(layouts.Item(urn), left, top, 420, 180);
            var nodes = diagram.SmartArt.AllNodes;
            for (var i = 1; i <= nodes.Count; i++)
                nodes[i].TextFrame2.TextRange.Text = $"Item {i}";
        }

        // xlColumnClustered 51, xlLine 4, xlPie 5, xlBarStacked 58
        foreach (var (type, title) in new[] { (51, "Column chart"), (4, "Line chart"), (5, "Pie chart"), (58, "Stacked bar chart") })
        {
            var slide = AddSlide(presentation, LayoutTitleOnly);
            slide.Shapes.Placeholders[1].TextFrame.TextRange.Text = title;
            var chart = slide.Shapes.AddChart2(-1, type, 80, 120, 800, 400).Chart;
            chart.ChartData.Workbook.Close();
        }
    }

    /// <summary>Pictures (PNG with transparency, JPEG, SVG, crop, outline, rotation), tables and effects.</summary>
    static void Objects(dynamic presentation)
    {
        const int MsoFalse = PowerPointSession.MsoFalse, MsoTrue = PowerPointSession.MsoTrue;
        var images = TestImages.Write(Path.Combine(Path.GetTempPath(), "fidelity-images"));

        var pictures = AddSlide(presentation, LayoutBlank);
        pictures.Shapes.AddPicture(images.Png, MsoFalse, MsoTrue, 40, 40, 240, 180);
        var cropped = pictures.Shapes.AddPicture(images.Png, MsoFalse, MsoTrue, 340, 40, 240, 180);
        cropped.PictureFormat.CropLeft = 60;
        cropped.PictureFormat.CropTop = 30;
        pictures.Shapes.AddPicture(images.Jpeg, MsoFalse, MsoTrue, 640, 40, 280, 180);
        var outlined = pictures.Shapes.AddPicture(images.Jpeg, MsoFalse, MsoTrue, 40, 280, 240, 160);
        outlined.Line.Visible = MsoTrue;
        outlined.Line.Weight = 6f;
        outlined.Line.ForeColor.RGB = Rgb(192, 0, 0);
        var rotated = pictures.Shapes.AddPicture(images.Png, MsoFalse, MsoTrue, 360, 290, 200, 150);
        rotated.Rotation = 12;
        pictures.Shapes.AddPicture(images.Svg, MsoFalse, MsoTrue, 680, 280, 200, 200);

        var tables = AddSlide(presentation, LayoutTitleOnly);
        tables.Shapes.Placeholders[1].TextFrame.TextRange.Text = "Tables";
        var first = tables.Shapes.AddTable(4, 4, 40, 130, 420, 200).Table;
        for (var r = 1; r <= 4; r++)
            for (var c = 1; c <= 4; c++)
                first.Cell(r, c).Shape.TextFrame.TextRange.Text = r == 1 ? $"Header {c}" : $"R{r}C{c}";
        var second = tables.Shapes.AddTable(4, 3, 500, 130, 420, 240).Table;
        second.ApplyStyle("{5940675A-B579-460E-94D1-54222C63F5DA}", MsoFalse);   // No Style, Table Grid
        for (var r = 1; r <= 4; r++)
            for (var c = 1; c <= 3; c++)
                second.Cell(r, c).Shape.TextFrame.TextRange.Text = $"Cell {r}.{c}";
        second.Cell(1, 1).Merge(second.Cell(1, 2));
        second.Cell(1, 1).Shape.TextFrame.TextRange.Text = "Merged header";
        second.Cell(3, 3).Merge(second.Cell(4, 3));
        second.Cell(2, 1).Shape.Fill.ForeColor.RGB = Rgb(255, 230, 153);

        var effects = AddSlide(presentation, LayoutBlank);
        var shadow = effects.Shapes.AddShape(RoundedRectangle, 60, 60, 220, 140);
        shadow.Shadow.Visible = MsoTrue;
        shadow.Shadow.OffsetX = 8;
        shadow.Shadow.OffsetY = 8;
        shadow.Shadow.Blur = 10;
        shadow.Shadow.Transparency = 0.5f;
        var glow = effects.Shapes.AddShape(Oval, 360, 60, 200, 140);
        glow.Glow.Radius = 12;
        glow.Glow.Color.RGB = Rgb(255, 192, 0);
        var soft = effects.Shapes.AddShape(Rectangle, 640, 60, 240, 140);
        soft.SoftEdge.Radius = 12;
        var reflection = effects.Shapes.AddShape(Star5, 120, 280, 160, 140);
        reflection.Reflection.Type = 1;   // msoReflectionType1
        var inner = effects.Shapes.AddShape(Rectangle, 400, 300, 220, 140);
        inner.Fill.ForeColor.RGB = Rgb(222, 235, 247);
        var text = effects.Shapes.AddTextbox(Horizontal, 660, 320, 260, 80);
        text.TextFrame.TextRange.Text = "Shadowed text";
        text.TextFrame.TextRange.Font.Size = 32;
        text.TextFrame2.TextRange.Font.Shadow.Visible = MsoTrue;
    }

    static void Generate(PowerPointSession session, string path, Action<dynamic> build, bool window = false)
    {
        var presentation = session.Create(window);
        presentation.PageSetup.SlideWidth = SlideWidth;
        presentation.PageSetup.SlideHeight = SlideHeight;
        build(presentation);
        PowerPointSession.SaveAndClose(presentation, path);
        Anonymize(path);
        Console.WriteLine($"  {Path.GetFileName(path)}");
    }

    /// <summary>PowerPoint writes the Office user name as author; the decks are published, so it is replaced.</summary>
    static void Anonymize(string path)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
        var entry = zip.GetEntry("docProps/core.xml");
        if (entry == null)
            return;
        XDocument core;
        using (var stream = entry.Open())
            core = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        foreach (var e in core.Root!.Elements().Where(e => e.Name.LocalName is "creator" or "lastModifiedBy"))
            e.Value = "Fidelity generator";
        entry.Delete();
        using var output = zip.CreateEntry("docProps/core.xml").Open();
        core.Save(output, SaveOptions.DisableFormatting);
    }

    internal static dynamic AddSlide(dynamic presentation, int layout) =>
        presentation.Slides.Add(presentation.Slides.Count + 1, layout);

    internal static int Rgb(int r, int g, int b) => r | (g << 8) | (b << 16);

    /// <summary>Lays out cells of a grid on consecutive blank slides.</summary>
    sealed class Grid(dynamic presentation, int columns, int rows)
    {
        dynamic? slide;
        int index;

        public float CellWidth => SlideWidth / columns;
        public float CellHeight => SlideHeight / rows;

        public (dynamic Slide, float Left, float Top) Next()
        {
            if (slide == null || index == columns * rows)
            {
                slide = AddSlide(presentation, LayoutBlank);
                index = 0;
            }
            var (row, column) = Math.DivRem(index++, columns);
            return (slide!, column * CellWidth, row * CellHeight);
        }

        /// <summary>Gives the last cell back (the shape could not be created).</summary>
        public void Undo() => index--;

        /// <summary>Removes a trailing slide that got no shape.</summary>
        public void Finish()
        {
            if (slide is { } last && index == 0)
                last.Delete();
        }
    }

    /// <summary>Every shape type PowerPoint accepts in AddShape, default style, 24 per slide.</summary>
    static void GeometryPresets(dynamic presentation)
    {
        var grid = new Grid(presentation, 6, 4);
        for (var type = 1; type <= 200; type++)
        {
            var (slide, left, top) = grid.Next();
            try
            {
                var shape = slide.Shapes.AddShape(type, left + 30, top + 20, grid.CellWidth - 60, grid.CellHeight - 40);
                shape.Name = $"AutoShape {type}";
            }
            catch
            {
                grid.Undo();
            }
        }
        grid.Finish();
    }

    static void Fills(dynamic presentation)
    {
        // Solid RGB colors with transparency.
        var slide = AddSlide(presentation, LayoutBlank);
        int[] colors = [Rgb(192, 0, 0), Rgb(0, 112, 192), Rgb(0, 176, 80), Rgb(255, 192, 0), Rgb(112, 48, 160), Rgb(64, 64, 64)];
        for (var i = 0; i < colors.Length; i++)
        {
            for (var t = 0; t < 3; t++)
            {
                var shape = slide.Shapes.AddShape(Rectangle, 40 + i * 150, 40 + t * 160, 120, 130);
                shape.Fill.Solid();
                shape.Fill.ForeColor.RGB = colors[i];
                shape.Fill.Transparency = t * 0.35f;
                shape.Line.Visible = PowerPointSession.MsoFalse;
            }
        }

        // Theme colors with brightness (lumMod/lumOff in the file).
        slide = AddSlide(presentation, LayoutBlank);
        float[] brightness = [0.8f, 0.6f, 0.4f, 0f, -0.25f, -0.5f];
        for (var accent = 0; accent < 6; accent++)
        {
            for (var b = 0; b < brightness.Length; b++)
            {
                var shape = slide.Shapes.AddShape(Rectangle, 40 + accent * 150, 30 + b * 82, 120, 70);
                shape.Fill.Solid();
                shape.Fill.ForeColor.ObjectThemeColor = 5 + accent;   // msoThemeColorAccent1 …
                shape.Fill.ForeColor.Brightness = brightness[b];
                shape.Line.Visible = PowerPointSession.MsoFalse;
            }
        }

        // Two-color gradients in every style and variant.
        var grid = new Grid(presentation, 6, 4);
        for (var style = 1; style <= 7; style++)
        {
            for (var variant = 1; variant <= 4; variant++)
            {
                var (s, left, top) = grid.Next();
                try
                {
                    var shape = s.Shapes.AddShape(RoundedRectangle, left + 20, top + 15, grid.CellWidth - 40, grid.CellHeight - 30);
                    shape.Fill.TwoColorGradient(style, variant);
                    shape.Fill.ForeColor.RGB = Rgb(0, 112, 192);
                    shape.Fill.BackColor.RGB = Rgb(255, 255, 255);
                }
                catch
                {
                    grid.Undo();
                }
            }
        }

        // Pattern fills.
        grid = new Grid(presentation, 9, 6);
        for (var pattern = 1; pattern <= 54; pattern++)
        {
            var (s, left, top) = grid.Next();
            var shape = s.Shapes.AddShape(Rectangle, left + 10, top + 10, grid.CellWidth - 20, grid.CellHeight - 20);
            shape.Fill.Patterned(pattern);
            shape.Fill.ForeColor.RGB = Rgb(0, 0, 0);
            shape.Fill.BackColor.RGB = Rgb(255, 255, 255);
        }
    }

    static void Lines(dynamic presentation)
    {
        // Widths and dash styles.
        var slide = AddSlide(presentation, LayoutBlank);
        float[] weights = [0.25f, 0.75f, 1.5f, 3f, 6f, 10f];
        for (var i = 0; i < weights.Length; i++)
        {
            var line = slide.Shapes.AddLine(40, 40 + i * 30, 440, 40 + i * 30);
            line.Line.Weight = weights[i];
            line.Line.ForeColor.RGB = Rgb(0, 0, 0);
        }
        for (var dash = 1; dash <= 12; dash++)
        {
            var line = slide.Shapes.AddLine(500, 30 + dash * 38, 920, 30 + dash * 38);
            line.Line.Weight = 3f;
            line.Line.ForeColor.RGB = Rgb(0, 0, 0);
            try { line.Line.DashStyle = dash; } catch { line.Delete(); }
        }
        for (var style = 1; style <= 5; style++)
        {
            var shape = slide.Shapes.AddShape(Rectangle, 40 + (style - 1) * 85, 260, 70, 70);
            shape.Fill.Visible = PowerPointSession.MsoFalse;
            shape.Line.Weight = 8f;
            shape.Line.ForeColor.RGB = Rgb(0, 0, 0);
            shape.Line.Style = style;
        }

        // Arrowheads: style × length × width.
        slide = AddSlide(presentation, LayoutBlank);
        for (var head = 2; head <= 6; head++)
        {
            for (var size = 1; size <= 3; size++)
            {
                var y = 30 + (head - 2) * 100 + size * 25;
                var line = slide.Shapes.AddLine(60, y, 400, y);
                line.Line.Weight = 2.25f;
                line.Line.ForeColor.RGB = Rgb(0, 0, 0);
                line.Line.BeginArrowheadStyle = head;
                line.Line.EndArrowheadStyle = head;
                line.Line.BeginArrowheadLength = size;
                line.Line.BeginArrowheadWidth = size;
                line.Line.EndArrowheadLength = 4 - size;
                line.Line.EndArrowheadWidth = 4 - size;

                var diagonal = slide.Shapes.AddLine(480 + (size - 1) * 150, 40 + (head - 2) * 100, 600 + (size - 1) * 150, 110 + (head - 2) * 100);
                diagonal.Line.Weight = 6f;
                diagonal.Line.ForeColor.RGB = Rgb(192, 0, 0);
                diagonal.Line.EndArrowheadStyle = head;
            }
        }

        // Outlines of shapes: color, width, round corners.
        slide = AddSlide(presentation, LayoutBlank);
        for (var i = 0; i < 6; i++)
        {
            var shape = slide.Shapes.AddShape(i % 2 == 0 ? Star5 : Oval, 40 + i * 150, 150, 120, 120);
            shape.Fill.ForeColor.RGB = Rgb(255, 230, 153);
            shape.Line.ForeColor.RGB = Rgb(31, 56, 100);
            shape.Line.Weight = 1 + i * 2;
        }
    }

    static void TextBasic(dynamic presentation)
    {
        var slide = AddSlide(presentation, LayoutTitle);
        slide.Shapes.Placeholders[1].TextFrame.TextRange.Text = "Fidelity test deck";
        slide.Shapes.Placeholders[2].TextFrame.TextRange.Text = "Text rendering, generated by PowerPoint";

        slide = AddSlide(presentation, LayoutText);
        slide.Shapes.Placeholders[1].TextFrame.TextRange.Text = "Bullet levels";
        var body = slide.Shapes.Placeholders[2].TextFrame.TextRange;
        body.Text = "First level bullet\rSecond level\rThird level\rSecond level again\rFirst level with a longer text that wraps onto a second line inside the placeholder\rFinal point";
        int[] levels = [1, 2, 3, 2, 1, 1];
        for (var i = 0; i < levels.Length; i++)
            body.Paragraphs(i + 1).IndentLevel = levels[i];

        slide = AddSlide(presentation, LayoutTitleOnly);
        slide.Shapes.Placeholders[1].TextFrame.TextRange.Text = "Fonts, sizes and styles";
        string[] fonts = ["Calibri", "Arial", "Times New Roman", "Aptos", "Segoe UI", "Georgia"];
        for (var i = 0; i < fonts.Length; i++)
        {
            var box = slide.Shapes.AddTextbox(Horizontal, 40, 130 + i * 62, 880, 50);
            var range = box.TextFrame.TextRange;
            range.Text = $"{fonts[i]}: The quick brown fox jumps over the lazy dog 0123456789";
            range.Font.Name = fonts[i];
            range.Font.Size = 14 + i * 3;
            range.Font.Bold = i % 2 == 1 ? PowerPointSession.MsoTrue : PowerPointSession.MsoFalse;
            range.Font.Italic = i % 3 == 2 ? PowerPointSession.MsoTrue : PowerPointSession.MsoFalse;
            range.Font.Underline = i == 4 ? PowerPointSession.MsoTrue : PowerPointSession.MsoFalse;
            range.Font.Color.RGB = i == 5 ? Rgb(192, 0, 0) : Rgb(0, 0, 0);
        }

        slide = AddSlide(presentation, LayoutTitleOnly);
        slide.Shapes.Placeholders[1].TextFrame.TextRange.Text = "Alignment and anchoring";
        int[] aligns = [AlignLeft, AlignCenter, AlignRight, AlignJustify];
        int[] anchors = [AnchorTop, AnchorMiddle, AnchorBottom];
        for (var a = 0; a < aligns.Length; a++)
        {
            for (var v = 0; v < anchors.Length; v++)
            {
                var shape = slide.Shapes.AddShape(Rectangle, 40 + a * 225, 130 + v * 130, 205, 115);
                shape.TextFrame.TextRange.Text = "Text inside a rectangle, aligned and anchored differently.";
                shape.TextFrame.TextRange.ParagraphFormat.Alignment = aligns[a];
                shape.TextFrame.VerticalAnchor = anchors[v];
                shape.TextFrame.TextRange.Font.Size = 14;
            }
        }

        slide = AddSlide(presentation, LayoutText);
        slide.Shapes.Placeholders[1].TextFrame.TextRange.Text = "Autofit: more text than fits";
        body = slide.Shapes.Placeholders[2].TextFrame.TextRange;
        body.Text = string.Join("\r", Enumerable.Range(1, 14).Select(i => $"Point {i}: text that makes the placeholder overflow"));
    }

    static void Transforms(dynamic presentation)
    {
        var slide = AddSlide(presentation, LayoutBlank);
        int[] rotations = [0, 15, 45, 90, 180, 270];
        for (var i = 0; i < rotations.Length; i++)
        {
            var shape = slide.Shapes.AddShape(RightArrow, 40 + i * 150, 60, 120, 70);
            shape.Rotation = rotations[i];
            var text = slide.Shapes.AddShape(Rectangle, 40 + i * 150, 230, 120, 70);
            text.TextFrame.TextRange.Text = $"{rotations[i]}°";
            text.Rotation = rotations[i];
        }
        for (var i = 0; i < 4; i++)
        {
            var shape = slide.Shapes.AddShape(RightArrow, 40 + i * 230, 400, 160, 90);
            shape.TextFrame.TextRange.Text = "flip";
            if (i is 1 or 3) shape.Flip(0);   // msoFlipHorizontal
            if (i is 2 or 3) shape.Flip(1);   // msoFlipVertical
        }

        slide = AddSlide(presentation, LayoutBlank);
        var a = slide.Shapes.AddShape(Oval, 100, 100, 120, 120);
        a.Name = "A";
        var b = slide.Shapes.AddShape(Rectangle, 260, 140, 160, 80);
        b.Name = "B";
        var c = slide.Shapes.AddShape(Star5, 180, 240, 100, 100);
        c.Name = "C";
        var group = slide.Shapes.Range(new object[] { "A", "B", "C" }).Group();
        group.Rotation = 20;

        var d = slide.Shapes.AddShape(Rectangle, 560, 80, 120, 80);
        d.Name = "D";
        var e = slide.Shapes.AddShape(Oval, 760, 300, 120, 120);
        e.Name = "E";
        for (var type = 1; type <= 3; type++)
        {
            var connector = slide.Shapes.AddConnector(type, 0, 0, 10, 10);
            connector.ConnectorFormat.BeginConnect(d, 3 + (type - 1) % 2);
            connector.ConnectorFormat.EndConnect(e, type);
            connector.RerouteConnections();
            connector.Line.Weight = 2f;
        }
    }

    /// <summary>
    /// Calibration deck for the text layout engine. Every box holds lines of "HHHH" (flat cap top and baseline),
    /// so line positions can be measured exactly. Per font: slide A varies line spacing (100 %, 90 %, 150 %,
    /// exactly 40 pt) at 24 pt; slide B varies the size (12, 24, 48 pt) and adds space before/after. The last
    /// slides cover anchoring, insets, mixed sizes and bullets.
    /// </summary>
    static void TextMetrics(dynamic presentation)
    {
        const int MsoFalse = PowerPointSession.MsoFalse, MsoTrue = PowerPointSession.MsoTrue;
        dynamic Box(dynamic slide, float left, float top, float height, string font, float size, int paragraphs = 6)
        {
            var box = slide.Shapes.AddTextbox(Horizontal, left, top, MetricsDeck.Width, height);
            box.TextFrame.AutoSize = 0;   // ppAutoSizeNone
            box.TextFrame.WordWrap = MsoFalse;
            box.TextFrame.VerticalAnchor = AnchorTop;
            box.Height = height;
            var range = box.TextFrame.TextRange;
            range.Text = string.Join('\r', Enumerable.Repeat("HHHH", paragraphs));
            range.Font.Name = font;
            range.Font.Size = size;
            range.Font.Color.RGB = 0;
            range.ParagraphFormat.Alignment = AlignLeft;
            range.ParagraphFormat.SpaceBefore = 0;
            range.ParagraphFormat.SpaceAfter = 0;
            range.ParagraphFormat.LineRuleWithin = MsoTrue;
            range.ParagraphFormat.SpaceWithin = 1f;
            return box;
        }

        foreach (var font in MetricsDeck.Fonts)
        {
            var a = AddSlide(presentation, LayoutBlank);
            float[] multiples = [1f, 0.9f, 1.5f];
            for (var i = 0; i < 3; i++)
                Box(a, MetricsDeck.Columns[i], MetricsDeck.Top, 480, font, 24).TextFrame.TextRange.ParagraphFormat.SpaceWithin = multiples[i];
            var exact = Box(a, MetricsDeck.Columns[3], MetricsDeck.Top, 480, font, 24).TextFrame.TextRange.ParagraphFormat;
            exact.LineRuleWithin = MsoFalse;
            exact.SpaceWithin = 40f;

            var b = AddSlide(presentation, LayoutBlank);
            float[] sizes = [12, 24, 48];
            for (var i = 0; i < 3; i++)
                Box(b, MetricsDeck.Columns[i], MetricsDeck.Top, 480, font, sizes[i], i == 2 ? 4 : 6);
            var spaced = Box(b, MetricsDeck.Columns[3], MetricsDeck.Top, 480, font, 24).TextFrame.TextRange.ParagraphFormat;
            spaced.LineRuleBefore = MsoFalse;
            spaced.SpaceBefore = 12f;
            spaced.LineRuleAfter = MsoFalse;
            spaced.SpaceAfter = 6f;
        }

        // Anchoring: top, middle, bottom in boxes of fixed height; a box without insets.
        var anchors = AddSlide(presentation, LayoutBlank);
        int[] anchorValues = [AnchorTop, AnchorMiddle, AnchorBottom];
        for (var i = 0; i < 3; i++)
            Box(anchors, MetricsDeck.Columns[i], MetricsDeck.Top, 300, "Aptos", 24, 3).TextFrame.VerticalAnchor = anchorValues[i];
        var noInsets = Box(anchors, MetricsDeck.Columns[3], MetricsDeck.Top, 300, "Aptos", 24, 3).TextFrame;
        noInsets.MarginTop = 0;
        noInsets.MarginBottom = 0;
        noInsets.MarginLeft = 0;
        noInsets.MarginRight = 0;

        // Mixed sizes in one line, and a bullet in another font.
        var mixed = AddSlide(presentation, LayoutBlank);
        var mixedBox = Box(mixed, MetricsDeck.Columns[0], MetricsDeck.Top, 300, "Aptos", 24, 3);
        mixedBox.TextFrame.TextRange.Paragraphs(2).Characters(1, 2).Font.Size = 48;
        var bulletBox = Box(mixed, MetricsDeck.Columns[1], MetricsDeck.Top, 300, "Aptos", 24, 4);
        var bullets = bulletBox.TextFrame.TextRange.ParagraphFormat.Bullet;
        bullets.Visible = MsoTrue;
        bullets.Character = 8226;
        bullets.Font.Name = "Arial";
        var bigBullet = Box(mixed, MetricsDeck.Columns[2], MetricsDeck.Top, 300, "Aptos", 24, 4).TextFrame.TextRange.ParagraphFormat.Bullet;
        bigBullet.Visible = MsoTrue;
        bigBullet.Character = 8226;
        bigBullet.RelativeSize = 2f;
        Box(mixed, MetricsDeck.Columns[3], MetricsDeck.Top, 300, "Times New Roman", 24, 4).TextFrame.TextRange.Paragraphs(2).Font.Name = "Aptos";

        // Second round: the transition around single spacing and exact spacing below and above the natural height.
        foreach (var font in new[] { "Aptos", "Arial" })
        {
            var multiples = AddSlide(presentation, LayoutBlank);
            float[] values = [1.05f, 1.1f, 1.2f, 1.3f];
            for (var i = 0; i < 4; i++)
                Box(multiples, MetricsDeck.Columns[i], MetricsDeck.Top, 480, font, 24).TextFrame.TextRange.ParagraphFormat.SpaceWithin = values[i];

            var exactSlide = AddSlide(presentation, LayoutBlank);
            float[] points = [20, 24, 28.8f, 34];
            for (var i = 0; i < 4; i++)
            {
                var format = Box(exactSlide, MetricsDeck.Columns[i], MetricsDeck.Top, 480, font, 24).TextFrame.TextRange.ParagraphFormat;
                format.LineRuleWithin = MsoFalse;
                format.SpaceWithin = points[i];
            }
        }
        var lowSlide = AddSlide(presentation, LayoutBlank);
        float[] low = [0.8f, 0.7f, 0.5f];
        for (var i = 0; i < 3; i++)
            Box(lowSlide, MetricsDeck.Columns[i], MetricsDeck.Top, 480, "Aptos", 24).TextFrame.TextRange.ParagraphFormat.SpaceWithin = low[i];
        var relative = Box(lowSlide, MetricsDeck.Columns[3], MetricsDeck.Top, 480, "Aptos", 24).TextFrame.TextRange.ParagraphFormat;
        relative.LineRuleBefore = MsoTrue;
        relative.SpaceBefore = 0.5f;
    }
}
