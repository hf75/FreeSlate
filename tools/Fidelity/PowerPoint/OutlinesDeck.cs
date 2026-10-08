using System.IO.Compression;
using System.Runtime.Versioning;
using System.Xml.Linq;

namespace Fidelity.PowerPoint;

/// <summary>
/// Outline fills and effects without a COM interface (catalog: line.fill, effect.blur): gradient and pattern
/// outlines, the blur effect and fill overlays. Shapes are created through COM and get the XML-only properties
/// in <see cref="Patch"/>, after PowerPoint saved the deck; the references come from PowerPoint's rendering of the
/// patched file.
/// </summary>
[SupportedOSPlatform("windows")]
static class OutlinesDeck
{
    const int Rectangle = 1, Oval = 9, Star5 = 92, RightArrow = 33;

    // MsoPatternType
    const int DarkDownwardDiagonal = 15, LargeCheckerBoard = 36, Percent50 = 7, Horizontal = 49, Divot = 46, Shingle = 47;

    static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";

    public static void Build(dynamic deck)
    {
        object presentation = deck;

        // Gradient outlines (patched): linear at several angles, radial and on a line with arrows.
        var gradients = Slide(presentation);
        Shape(gradients, Rectangle, 40, 40, 260, 160, "grad-lin0");
        Shape(gradients, Oval, 350, 40, 260, 160, "grad-lin45");
        Shape(gradients, Star5, 660, 30, 200, 190, "grad-path");
        Shape(gradients, RightArrow, 40, 270, 260, 160, "grad-lin90");
        var line = ((dynamic)gradients).Shapes.AddLine(360, 300, 900, 420);
        line.Name = "grad-line";
        line.Line.Weight = 12f;
        line.Line.EndArrowheadStyle = 2;

        // Pattern outlines through COM.
        var patterns = Slide(presentation);
        int[] presets = [DarkDownwardDiagonal, LargeCheckerBoard, Percent50, Horizontal, Divot, Shingle];
        for (var i = 0; i < presets.Length; i++)
        {
            var shape = Shape(patterns, i % 2 == 0 ? Rectangle : Oval, 40 + (i % 3) * 300, 40 + (i / 3) * 240, 240, 180, $"patt-{i}");
            shape.Line.Weight = 14f;
            shape.Line.Pattern = presets[i];
            shape.Line.ForeColor.RGB = CorpusGenerator.Rgb(31, 56, 100);
            shape.Line.BackColor.RGB = CorpusGenerator.Rgb(255, 230, 153);
        }

        // Blur and fill overlay (patched).
        var effects = Slide(presentation);
        Shape(effects, Rectangle, 40, 40, 260, 160, "blur-4");
        Shape(effects, Star5, 350, 30, 200, 190, "blur-10");
        Shape(effects, Oval, 650, 40, 260, 160, "blur-grow");
        Shape(effects, Rectangle, 40, 290, 260, 160, "overlay-mult");
        Shape(effects, Oval, 350, 290, 260, 160, "overlay-screen");
        Shape(effects, Star5, 660, 280, 200, 190, "overlay-darken");
    }

    static dynamic Slide(object deck) => CorpusGenerator.AddSlide((dynamic)deck, CorpusGenerator.LayoutBlank);

    static dynamic Shape(object target, int type, float left, float top, float width, float height, string name)
    {
        dynamic slide = target;
        var shape = slide.Shapes.AddShape(type, left, top, width, height);
        shape.Name = name;
        shape.Fill.ForeColor.RGB = CorpusGenerator.Rgb(222, 235, 247);
        shape.Line.ForeColor.RGB = CorpusGenerator.Rgb(68, 114, 196);
        shape.Line.Weight = 10f;
        return shape;
    }

    /// <summary>Adds the gradient outlines, blur effects and fill overlays to the saved deck.</summary>
    public static void Patch(string path)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
        foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith("ppt/slides/slide", StringComparison.Ordinal) && e.FullName.EndsWith(".xml", StringComparison.Ordinal)).ToList())
        {
            XDocument slide;
            using (var stream = entry.Open())
                slide = XDocument.Load(stream);
            foreach (var shape in slide.Descendants().Where(e => e.Name == P + "sp" || e.Name == P + "cxnSp"))
            {
                var name = shape.Descendants(P + "cNvPr").FirstOrDefault()?.Attribute("name")?.Value ?? "";
                var spPr = shape.Element(P + "spPr");
                if (spPr == null)
                    continue;
                if (name.StartsWith("grad-", StringComparison.Ordinal))
                    GradientLine(spPr, name);
                else if (name.StartsWith("blur-", StringComparison.Ordinal))
                    Effect(spPr, new XElement(A + "blur", new XAttribute("rad", name == "blur-4" ? 50800 : 127000), new XAttribute("grow", name == "blur-grow" ? "1" : "0")));
                else if (name.StartsWith("overlay-", StringComparison.Ordinal))
                    Effect(spPr, new XElement(A + "fillOverlay", new XAttribute("blend", name["overlay-".Length..]),
                        new XElement(A + "gradFill",
                            new XElement(A + "gsLst",
                                Stop(0, "C00000"), Stop(100000, "FFC000")),
                            new XElement(A + "lin", new XAttribute("ang", 0), new XAttribute("scaled", 0)))));
            }
            entry.Delete();
            using var output = zip.CreateEntry(entry.FullName).Open();
            slide.Save(output);
        }
    }

    static XElement Stop(int position, string color) =>
        new(A + "gs", new XAttribute("pos", position), new XElement(A + "srgbClr", new XAttribute("val", color)));

    static void GradientLine(XElement spPr, string name)
    {
        var ln = spPr.Element(A + "ln");
        if (ln == null)
            return;
        ln.Elements().Where(e => e.Name.LocalName is "solidFill" or "noFill" or "gradFill" or "pattFill").Remove();
        XElement shade = name == "grad-path"
            ? new XElement(A + "path", new XAttribute("path", "circle"),
                new XElement(A + "fillToRect", new XAttribute("l", 50000), new XAttribute("t", 50000), new XAttribute("r", 50000), new XAttribute("b", 50000)))
            : new XElement(A + "lin", new XAttribute("ang", name switch { "grad-lin45" => 2700000, "grad-lin90" => 5400000, _ => 0 }), new XAttribute("scaled", 0));
        ln.AddFirst(new XElement(A + "gradFill", new XAttribute("rotWithShape", 1),
            new XElement(A + "gsLst", Stop(0, "C00000"), Stop(50000, "FFC000"), Stop(100000, "0070C0")), shade));
    }

    static void Effect(XElement spPr, XElement effect)
    {
        var list = spPr.Element(A + "effectLst");
        if (list == null)
        {
            list = new XElement(A + "effectLst");
            // effectLst follows the fill and the line.
            var anchor = spPr.Element(A + "ln") ?? spPr.Elements().LastOrDefault(e => e.Name.LocalName.EndsWith("Fill", StringComparison.Ordinal));
            if (anchor != null) anchor.AddAfterSelf(list); else spPr.Add(list);
        }
        // Schema order: blur, fillOverlay, glow, innerShdw, outerShdw, prstShdw, reflection, softEdge.
        if (effect.Name.LocalName == "blur") list.AddFirst(effect);
        else if (list.Element(A + "blur") is { } blur) blur.AddAfterSelf(effect);
        else list.AddFirst(effect);
    }
}
