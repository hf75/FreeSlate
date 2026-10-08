using System.IO.Compression;
using System.Runtime.Versioning;
using System.Xml.Linq;

namespace Fidelity.PowerPoint;

/// <summary>
/// Sketched outlines (catalog: shape.sketched): the curved, freehand and scribble styles on rectangles, rounded
/// rectangles, ellipses, triangles and arrows with text, and on shapes without fill, with gradient fill, dashes, a
/// shadow, rotation and flips. The object model cannot sketch a shape, so the patch replaces each named shape's
/// preset by a placeholder path and adds the sketch properties (the original preset, type and seed) to its line;
/// PowerPoint then opens and saves the deck again (see CorpusGenerator), which draws the hand-drawn paths it writes.
/// </summary>
[SupportedOSPlatform("windows")]
static class SketchedDeck
{
    // MsoAutoShapeType
    const int Rectangle = 1, RoundedRectangle = 5, IsoscelesTriangle = 7, Oval = 9, Heart = 21, RightArrow = 33, Star5 = 92, Cloud = 179;

    static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";
    static readonly XNamespace Ask = "http://schemas.microsoft.com/office/drawing/2018/sketchyshapes";
    const string SketchUri = "{C807C97D-BFC1-408E-A445-0C87EB9F89A2}";

    public static void Build(dynamic deck)
    {
        object presentation = deck;

        // The three styles (rows) on five presets with text.
        var grid = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        int[] presets = [Rectangle, RoundedRectangle, Oval, IsoscelesTriangle, RightArrow];
        string[] styles = ["curved", "freehand", "scribble"];
        for (var row = 0; row < styles.Length; row++)
        {
            for (var column = 0; column < presets.Length; column++)
            {
                var shape = ((dynamic)grid).Shapes.AddShape(presets[column], 40 + column * 180, 40 + row * 165, 150, 120);
                shape.Name = $"sk-{styles[row]}-{column}";
                shape.Line.Weight = 2.25f;
                shape.TextFrame.TextRange.Text = styles[row];
                shape.TextFrame.TextRange.Font.Size = 16;
            }
        }

        // Fills, lines and transforms.
        var more = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        dynamic Add(int type, float left, float top, float width, float height, string name)
        {
            var shape = ((dynamic)more).Shapes.AddShape(type, left, top, width, height);
            shape.Name = name;
            return shape;
        }
        var empty = Add(Rectangle, 40, 40, 200, 140, "sk-freehand-nofill");
        empty.Fill.Visible = PowerPointSession.MsoFalse;
        empty.Line.Weight = 6f;
        empty.Line.ForeColor.RGB = CorpusGenerator.Rgb(192, 0, 0);

        var star = Add(Star5, 290, 30, 180, 170, "sk-scribble-gradient");
        star.Fill.TwoColorGradient(1, 1);   // msoGradientHorizontal, variant 1
        star.Fill.ForeColor.RGB = CorpusGenerator.Rgb(255, 192, 0);
        star.Fill.BackColor.RGB = CorpusGenerator.Rgb(192, 0, 0);
        star.Line.Weight = 1.5f;

        var cloud = Add(Cloud, 520, 40, 200, 140, "sk-curved-cloud");
        cloud.Line.Weight = 3f;

        var dashed = Add(Rectangle, 760, 40, 160, 140, "sk-curved-dash");
        dashed.Line.DashStyle = 4;   // msoLineDash
        dashed.Line.Weight = 3f;
        dashed.Fill.ForeColor.RGB = CorpusGenerator.Rgb(112, 173, 71);

        var rotated = Add(Rectangle, 60, 280, 200, 120, "sk-freehand-rotated");
        rotated.Rotation = 30f;
        rotated.Line.Weight = 3f;
        rotated.TextFrame.TextRange.Text = "Rotated";

        var flipped = Add(RightArrow, 300, 270, 200, 140, "sk-scribble-flipped");
        flipped.Flip(0);   // msoFlipHorizontal
        flipped.Flip(1);   // msoFlipVertical
        flipped.Line.Weight = 2f;

        var heart = Add(Heart, 540, 260, 160, 150, "sk-freehand-heart");
        heart.Line.Weight = 2.5f;
        heart.Fill.ForeColor.RGB = CorpusGenerator.Rgb(255, 102, 153);

        var shadow = Add(Oval, 740, 260, 180, 150, "sk-curved-shadow");
        shadow.Line.Weight = 2f;
        shadow.Shadow.Visible = PowerPointSession.MsoTrue;
        shadow.Shadow.OffsetX = 6f;
        shadow.Shadow.OffsetY = 6f;
        shadow.TextFrame.TextRange.Text = "A longer text inside the ellipse";
        shadow.TextFrame.TextRange.Font.Size = 14;
    }

    /// <summary>
    /// Replaces each sketched shape's preset by a placeholder path and adds the sketch properties to its line: the
    /// original preset, the style and a seed (PowerPoint keeps a sketch only on custom geometry).
    /// </summary>
    public static void Patch(string path)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
        foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith("ppt/slides/slide", StringComparison.Ordinal) && e.FullName.EndsWith(".xml", StringComparison.Ordinal)).ToList())
        {
            XDocument slide;
            using (var stream = entry.Open())
                slide = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
            var seed = 1219033472u;
            foreach (var shape in slide.Descendants(P + "sp"))
            {
                var name = shape.Descendants(P + "cNvPr").First().Attribute("name")?.Value ?? "";
                if (!name.StartsWith("sk-", StringComparison.Ordinal))
                    continue;
                var style = name.Split('-')[1];
                var spPr = shape.Element(P + "spPr")!;
                var preset = spPr.Element(A + "prstGeom")!;
                var extent = spPr.Element(A + "xfrm")!.Element(A + "ext")!;
                var (w, h) = ((string)extent.Attribute("cx")!, (string)extent.Attribute("cy")!);
                preset.ReplaceWith(new XElement(A + "custGeom",
                    new XElement(A + "avLst"), new XElement(A + "gdLst"), new XElement(A + "ahLst"), new XElement(A + "cxnLst"),
                    new XElement(A + "rect", new XAttribute("l", "l"), new XAttribute("t", "t"), new XAttribute("r", "r"), new XAttribute("b", "b")),
                    new XElement(A + "pathLst",
                        new XElement(A + "path", new XAttribute("w", w), new XAttribute("h", h),
                            new XElement(A + "moveTo", Point("0", "0")),
                            new XElement(A + "lnTo", Point(w, "0")),
                            new XElement(A + "lnTo", Point(w, h)),
                            new XElement(A + "lnTo", Point("0", h)),
                            new XElement(A + "close")))));
                var line = spPr.Element(A + "ln");
                if (line == null)
                {
                    line = new XElement(A + "ln");
                    spPr.Add(line);
                }
                var extensions = line.Element(A + "extLst");
                if (extensions == null)
                {
                    extensions = new XElement(A + "extLst");
                    line.Add(extensions);
                }
                extensions.Add(new XElement(A + "ext", new XAttribute("uri", SketchUri),
                    new XElement(Ask + "lineSketchStyleProps", new XAttribute(XNamespace.Xmlns + "ask", Ask), new XAttribute("sd", seed),
                        new XElement(preset),
                        new XElement(Ask + "type", new XElement(Ask + ("lineSketch" + char.ToUpperInvariant(style[0]) + style[1..]))))));
                seed = seed * 1664525u + 1013904223u;
            }
            entry.Delete();
            using var output = zip.CreateEntry(entry.FullName).Open();
            slide.Save(output, SaveOptions.DisableFormatting);
        }
    }

    static XElement Point(string x, string y) => new(A + "pt", new XAttribute("x", x), new XAttribute("y", y));
}
