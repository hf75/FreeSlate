using System.IO.Compression;
using System.Runtime.Versioning;
using System.Xml.Linq;

namespace Fidelity.PowerPoint;

/// <summary>
/// Effect graphs (catalog: effect.dag) on rectangles with a fill, a thick line and text: references to the fill, the
/// line and both, effects alone and over the shape, sibling and tree containers, named containers, blends and fills.
/// The object model cannot write effect graphs, so the patch adds them after saving.
/// </summary>
[SupportedOSPlatform("windows")]
static class EffectDagDeck
{
    static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";

    public static void Build(dynamic deck)
    {
        object presentation = deck;
        foreach (var prefix in new[] { "d", "e" })
        {
            var slide = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
            for (var i = 0; i < 10; i++)
            {
                var shape = ((dynamic)slide).Shapes.AddShape(1, 40 + i % 5 * 185, 80 + i / 5 * 230, 140, 110);
                shape.Name = $"{prefix}{i}";
                shape.Line.Weight = 6f;
                shape.Line.ForeColor.RGB = CorpusGenerator.Rgb(192, 0, 0);
                shape.Fill.ForeColor.RGB = CorpusGenerator.Rgb(0, 112, 192);
                shape.TextFrame.TextRange.Text = $"{prefix}{i} text";
                shape.TextFrame.TextRange.Font.Size = 20;
            }
        }
    }

    const string Shadow = """<a:outerShdw blurRad="50800" dist="101600" dir="2700000" algn="tl" rotWithShape="0"><a:srgbClr val="000000"><a:alpha val="70000"/></a:srgbClr></a:outerShdw>""";
    const string Glow = """<a:glow rad="101600"><a:srgbClr val="FF0000"><a:alpha val="60000"/></a:srgbClr></a:glow>""";

    static readonly Dictionary<string, string> Graphs = new()
    {
        ["d0"] = """<a:effect ref="fillLine"/>""",
        ["d1"] = Shadow + """<a:effect ref="fillLine"/>""",
        ["d2"] = """<a:effect ref="fill"/>""",
        ["d3"] = """<a:effect ref="line"/>""",
        ["d4"] = """<a:cont type="tree"><a:effect ref="fillLine"/><a:blur rad="63500"/></a:cont>""",
        ["d5"] = """<a:cont type="tree"><a:effect ref="fillLine"/><a:relOff tx="20000" ty="30000"/></a:cont>""",
        ["d6"] = """<a:cont type="tree"><a:effect ref="fillLine"/><a:grayscl/></a:cont>""",
        ["d7"] = """<a:cont/>""",
        ["d8"] = """<a:cont type="tree"><a:effect ref="fillLine"/><a:alphaModFix amt="40000"/></a:cont>""",
        ["d9"] = """<a:effect ref="fillLine"/><a:blend blend="mult"><a:cont><a:fill><a:solidFill><a:srgbClr val="FF0000"/></a:solidFill></a:fill></a:cont></a:blend>""",
        ["e0"] = Shadow,
        ["e1"] = """<a:cont type="sib">""" + Glow + """<a:reflection blurRad="6350" stA="50000" endA="300" endPos="55000" dir="5400000" sy="-100000" algn="bl" rotWithShape="0"/></a:cont>""",
        ["e2"] = """<a:cont type="tree"><a:cont type="sib"><a:alphaOutset rad="38100"/><a:fill><a:solidFill><a:srgbClr val="00B050"/></a:solidFill></a:fill></a:cont><a:relOff tx="40000" ty="40000"/></a:cont><a:cont type="sib"/>""",
        ["e3"] = """<a:blend blend="over"><a:cont type="sib"><a:fill><a:solidFill><a:srgbClr val="FFC000"/></a:solidFill></a:fill><a:relOff tx="60000" ty="0"/></a:cont></a:blend>""",
        ["e4"] = Glow + """<a:effect ref="fillLine"/>""",
        ["e5"] = """<a:cont type="tree"><a:effect ref="fillLine"/><a:softEdge rad="127000"/></a:cont>""",
        ["e6"] = """<a:effect ref="fillLine"/><a:innerShdw blurRad="63500" dist="50800" dir="13500000"><a:srgbClr val="000000"><a:alpha val="80000"/></a:srgbClr></a:innerShdw>""",
        ["e7"] = """<a:cont name="gray" type="tree"><a:effect ref="fillLine"/><a:grayscl/></a:cont><a:cont type="tree"><a:effect ref="gray"/><a:relOff tx="0" ty="60000"/></a:cont>""",
        ["e8"] = """<a:cont type="tree"><a:effect ref="fillLine"/><a:fill><a:gradFill><a:gsLst><a:gs pos="0"><a:srgbClr val="FFC000"/></a:gs><a:gs pos="100000"><a:srgbClr val="7030A0"/></a:gs></a:gsLst><a:lin ang="0" scaled="0"/></a:gradFill></a:fill></a:cont>""",
        ["e9"] = """<a:effect ref="fillLine"/><a:fillOverlay blend="mult"><a:solidFill><a:srgbClr val="FFFF00"/></a:solidFill></a:fillOverlay>""",
    };

    public static void Patch(string path)
    {
        var changes = Graphs.ToDictionary(g => g.Key, g => (Action<XElement>)(shape =>
        {
            var spPr = shape.Element(P + "spPr")!;
            var graph = XElement.Parse($"<a:effectDag xmlns:a=\"{A}\">{g.Value}</a:effectDag>");
            // Effects follow the line in spPr.
            if (spPr.Element(A + "ln") is { } line)
                line.AddAfterSelf(graph);
            else
                spPr.Add(graph);
        }));
        ShapePatch.Apply(path, changes);
    }
}
