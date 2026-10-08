using System.Globalization;
using System.Xml.Linq;
using FreeSlate.Drawing;

namespace FreeSlate.Pptx.SmartArt;

/// <summary>
/// The colors and styles of a SmartArt diagram (catalog: smartart.styles). The color definition (dgm:colorsDef) gives
/// every style label lists of fill, line, effect and text colors, applied to the label's shapes in turn (repeat),
/// spread across them from the first color to the last (span: shape i of n at i / (n − 1)), or there and back
/// (cycle: at 2i / n up to the middle, then back); spread colors are the first color with the HSL and alpha
/// differences to the next added as hueOff, satOff, lumOff and alphaOff, the hue by the plain difference unless
/// hueDir says which way round (all measured from PowerPoint's drawings). The style definition (dgm:styleDef) gives every
/// label its theme style references, 3D properties and text properties, and the diagram its 3D scene.
/// </summary>
public sealed class Styling(XElement? colorsDef, XElement? styleDef, ColorContext colors)
{
    static readonly XNamespace D = DataModel.Dgm;
    static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    static readonly HashSet<string> ColorElements = ["srgbClr", "schemeClr", "sysClr", "prstClr", "hslClr", "scrgbClr"];

    /// <summary>The 3D scene of the whole diagram (styleDef/scene3d), if any.</summary>
    public XElement? DiagramScene => styleDef?.Element(D + "scene3d");

    static XElement? Label(XElement? definition, string? label) =>
        label == null ? null : definition?.Elements(D + "styleLbl").FirstOrDefault(l => (string?)l.Attribute("name") == label);

    /// <summary>The label's theme style references (a:lnRef, a:fillRef, a:effectRef, a:fontRef) in the style definition.</summary>
    public XElement? Style(string? label) => Label(styleDef, label)?.Element(D + "style");

    /// <summary>The label's 3D scene, 3D shape properties or text properties (scene3d, sp3d, txPr) in the style definition.</summary>
    public XElement? StyleElement(string? label, string name) => Label(styleDef, label)?.Element(D + name);

    /// <summary>The list style of a layout definition (Office 2016 extension dgm1612:lstStyle), if it has one.</summary>
    public static XElement? ListStyle(XElement? layoutDef) =>
        layoutDef?.Element(D + "extLst")?.Elements(A + "ext").Where(e => (string?)e.Attribute("uri") == "{68A01E43-0DF5-4B5B-8FA6-DAF915123BFB}")
            .Elements().FirstOrDefault(e => e.Name.LocalName == "lstStyle");

    /// <summary>
    /// Applies a layout's list style to a text body made of the paragraphs of <paramref name="points"/>, in order: the
    /// runs of a paragraph take the run defaults (defRPr) of the level of their data point's depth (top-level nodes
    /// lvl1pPr) where they set nothing themselves (measured: a timeline's titles bold in accent 1, their descendants'
    /// text, written at the same paragraph level, plain). Nothing changes when the paragraphs do not match the points.
    /// </summary>
    public static void ApplyListStyle(XElement txBody, IEnumerable<DataPoint> points, XElement listStyle)
    {
        var depths = new List<int>();
        foreach (var point in points)
        {
            var count = Math.Max(1, point.Element.Element(D + "t")?.Elements(A + "p").Count() ?? 0);
            depths.AddRange(Enumerable.Repeat(DataModel.Depth(point), count));
        }
        var paragraphs = txBody.Elements(A + "p").ToList();
        if (paragraphs.Count != depths.Count)
            return;
        for (var i = 0; i < paragraphs.Count; i++)
        {
            if (listStyle.Element(A + $"lvl{Math.Clamp(depths[i], 1, 9)}pPr")?.Element(A + "defRPr") is not { } defaults)
                continue;
            foreach (var run in paragraphs[i].Elements().Where(e => e.Name.LocalName is "r" or "fld"))
            {
                if (run.Element(A + "rPr") is not { } rPr)
                    run.AddFirst(rPr = new XElement(A + "rPr"));
                foreach (var attribute in defaults.Attributes().Where(a => rPr.Attribute(a.Name) == null))
                    rPr.SetAttributeValue(attribute.Name, attribute.Value);
                foreach (var child in defaults.Elements())
                {
                    var isFill = child.Name.LocalName.EndsWith("Fill", StringComparison.Ordinal);
                    var present = isFill ? rPr.Elements().Any(e => e.Name.LocalName.EndsWith("Fill", StringComparison.Ordinal)) : rPr.Element(child.Name) != null;
                    if (present)
                        continue;
                    if (isFill && rPr.Element(A + "ln") is { } line)
                        line.AddAfterSelf(new XElement(child));
                    else if (isFill)
                        rPr.AddFirst(new XElement(child));
                    else
                        rPr.Add(new XElement(child));
                }
            }
        }
    }

    /// <summary>
    /// The color a label's list (fillClrLst, linClrLst, effectClrLst, txLinClrLst, txFillClrLst, txEffectClrLst) gives
    /// the <paramref name="index"/>-th of <paramref name="count"/> shapes, or null when the list is empty.
    /// </summary>
    public XElement? Color(string? label, string list, int index, int count)
    {
        var colorList = Label(colorsDef, label)?.Element(D + list);
        var entries = colorList?.Elements().Where(e => ColorElements.Contains(e.Name.LocalName)).ToList();
        if (entries is not { Count: > 0 })
            return null;
        count = Math.Max(count, 1);
        index = Math.Clamp(index, 0, count - 1);
        var method = (string?)colorList!.Attribute("meth") ?? "span";
        if (entries.Count == 1 || method == "repeat")
            return new XElement(entries[index % entries.Count]);
        var t = method == "cycle"
            ? (2 * index <= count ? 2.0 * index / count : 2.0 * (count - index) / count)
            : count > 1 ? (double)index / (count - 1) : 0;
        var position = Math.Clamp(t, 0, 1) * (entries.Count - 1);
        var segment = Math.Min((int)Math.Floor(position), entries.Count - 2);
        var local = position - segment;
        var (first, second) = (entries[segment], entries[segment + 1]);
        var result = new XElement(first);
        if (local < 1e-9)
            return result;
        var (a, b) = (colors.Resolve(first), colors.Resolve(second));
        var (ha, sa, la) = a.ToHsl();
        var (hb, sb, lb) = b.ToHsl();
        // The hue changes by the plain difference (measured: accent 5 to accent 6 turns back by 202.5°); hueDir turns
        // it the given way round.
        var hue = hb - ha;
        if ((string?)colorList.Attribute("hueDir") == "cw" && hue < 0)
            hue += 360;
        else if ((string?)colorList.Attribute("hueDir") == "ccw" && hue > 0)
            hue -= 360;
        static string Value(double v) => ((long)Math.Round(v)).ToString(CultureInfo.InvariantCulture);
        result.Add(
            new XElement(A + "hueOff", new XAttribute("val", Value(hue * local * 60000))),
            new XElement(A + "satOff", new XAttribute("val", Value((sb - sa) * local * 100000))),
            new XElement(A + "lumOff", new XAttribute("val", Value((lb - la) * local * 100000))),
            new XElement(A + "alphaOff", new XAttribute("val", Value((b.A - a.A) * local * 100000))));
        return result;
    }
}
