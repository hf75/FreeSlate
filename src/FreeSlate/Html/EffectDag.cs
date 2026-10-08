using System.Text;
using System.Xml.Linq;
using FreeSlate.Drawing;

namespace FreeSlate.Html;

/// <summary>
/// Effect graphs (a:effectDag, catalog: effect.dag) as SVG, evaluated the way PowerPoint draws them (measured with
/// generated/effects-dag): the graph's input is the whole shape (fill, line and text) and its output replaces the
/// shape. A container (a:cont, and a:effectDag itself) of type sib draws its children's outputs over each other, one of
/// type tree feeds each child the output of the one before; an empty container draws nothing, and a graph without
/// any effect leaves the shape as it is. An effect
/// reference (a:effect ref) brings in the shape's fill, its line, both with the text (fillLine), or the output of a named
/// container. Each effect outputs its result only (an outer shadow without the shape, a reflection without the
/// original); a:blend draws its container's output over the input in a blend mode (over is overlay), a:fillOverlay
/// its fill. The effects use the constants of <see cref="SvgEffects"/> and <see cref="BlipEffects"/>.
/// </summary>
public sealed class EffectDag(ColorContext colors, double width, double height, StringBuilder defs, Func<XElement, string> paint, Matrix? frame = null)
{
    static int nextId;

    /// <summary>The shape drawn in parts: its fill, its line, and fill, line and text together.</summary>
    public sealed record Sources(string Fill, string Line, string FillLine);

    readonly Dictionary<string, string> named = new(StringComparer.Ordinal);
    Sources sources = new("", "", "");

    /// <summary>The graph's output for the shape drawn as <paramref name="parts"/>.</summary>
    public string Render(XElement effectDag, Sources parts)
    {
        sources = parts;
        named.Clear();
        if (!effectDag.Descendants().Any(e => e.Name.Namespace == Ns.A && e.Name.LocalName != "cont"))
            return parts.FillLine;
        return Container(effectDag, parts.FillLine);
    }

    string Id(string prefix) => $"{prefix}{Interlocked.Increment(ref nextId)}";

    /// <summary>A filter region around the shape with room for <paramref name="extent"/> pixels on every side.</summary>
    string Region(double extent)
    {
        var margin = Math.Max(width, height) + extent;
        return $"filterUnits=\"userSpaceOnUse\" x=\"{Units.Css(-margin)}\" y=\"{Units.Css(-margin)}\" width=\"{Units.Css(width + 2 * margin)}\" height=\"{Units.Css(height + 2 * margin)}\" color-interpolation-filters=\"sRGB\"";
    }

    string Filtered(string input, string primitives, double extent = 0)
    {
        var id = Id("dag");
        defs.Append($"<filter id=\"{id}\" {Region(extent)}>{primitives}</filter>");
        return $"<g filter=\"url(#{id})\">{input}</g>";
    }

    string Container(XElement container, string input)
    {
        var children = container.Elements().Where(e => e.Name.Namespace == Ns.A).ToList();
        string output;
        if (children.Count == 0)
            output = "";
        else if (container.Str("type") == "tree")
        {
            output = input;
            foreach (var child in children)
                output = Node(child, output);
        }
        else
            output = string.Concat(children.Select(child => Node(child, input)));
        if (container.Str("name") is { Length: > 0 } name)
            named[name] = output;
        return output;
    }

    string Node(XElement node, string input)
    {
        double Fraction(string attribute, double fallback) => (node.Long(attribute) ?? fallback * 100000) / 100000.0;
        switch (node.Name.LocalName)
        {
            case "cont":
                return Container(node, input);
            case "effect":
                return node.Str("ref") switch
                {
                    "fill" => sources.Fill,
                    "line" => sources.Line,
                    "fillLine" or "children" => sources.FillLine,
                    { } name when named.TryGetValue(name, out var output) => output,
                    _ => "",
                };
            case "outerShdw" or "prstShdw":
                return Shadow(node, input);
            case "innerShdw":
            {
                var blur = Units.ToPx(node.Long("blurRad") ?? 0);
                var distance = Units.ToPx(node.Long("dist") ?? 0);
                var direction = (node.Long("dir") ?? 0) / 60000.0 * Math.PI / 180;
                var color = colors.ResolveChild(node) ?? RgbColor.Black;
                return Filtered(input,
                    "<feComponentTransfer in=\"SourceAlpha\" result=\"io\"><feFuncA type=\"table\" tableValues=\"1 0\"/></feComponentTransfer>" +
                    $"<feGaussianBlur in=\"io\" stdDeviation=\"{Units.Css(blur * 0.35)}\"/>" +
                    $"<feOffset dx=\"{Units.Css(-distance * Math.Cos(direction))}\" dy=\"{Units.Css(-distance * Math.Sin(direction))}\" result=\"ib\"/>" +
                    $"<feFlood flood-color=\"{color.ToHex()}\" flood-opacity=\"{Units.Css4(color.A)}\"/>" +
                    "<feComposite in2=\"ib\" operator=\"in\"/><feComposite in2=\"SourceAlpha\" operator=\"in\"/>", distance + blur * 2);
            }
            case "glow":
            {
                var r = Units.ToPx(node.Long("rad") ?? 0);
                var color = colors.ResolveChild(node) ?? RgbColor.Black;
                return Filtered(input,
                    $"<feMorphology in=\"SourceAlpha\" operator=\"dilate\" radius=\"{Units.Css(r / 2)}\"/>" +
                    $"<feGaussianBlur stdDeviation=\"{Units.Css(r / 6)}\" result=\"gb\"/>" +
                    $"<feFlood flood-color=\"{color.ToHex()}\" flood-opacity=\"{Units.Css4(color.A)}\"/><feComposite in2=\"gb\" operator=\"in\"/>", r * 1.5);
            }
            case "softEdge":
            {
                var r = Units.ToPx(node.Long("rad") ?? 0);
                return Filtered(input,
                    $"<feMorphology in=\"SourceAlpha\" operator=\"erode\" radius=\"{Units.Css(r * 0.85)}\"/>" +
                    $"<feGaussianBlur stdDeviation=\"{Units.Css(r / 3)}\" result=\"sb\"/><feComposite in=\"SourceGraphic\" in2=\"sb\" operator=\"in\"/>");
            }
            case "blur":
            {
                var r = Units.ToPx(node.Long("rad") ?? 0);
                if (r <= 0)
                    return input;
                // Without grow the blur stays within the shape's box.
                if (node.Bool("grow") == false)
                {
                    var id = Id("dag");
                    defs.Append($"<filter id=\"{id}\" filterUnits=\"userSpaceOnUse\" x=\"0\" y=\"0\" width=\"{Units.Css(width)}\" height=\"{Units.Css(height)}\" color-interpolation-filters=\"sRGB\">" +
                                $"<feGaussianBlur stdDeviation=\"{Units.Css(r / 3)}\"/></filter>");
                    return $"<g filter=\"url(#{id})\">{input}</g>";
                }
                return Filtered(input, $"<feGaussianBlur stdDeviation=\"{Units.Css(r / 3)}\"/>", r * 1.5);
            }
            case "reflection":
            {
                var list = new XElement(Ns.A + "effectLst", new XElement(node));
                return SvgEffects.Reflection(list, input, (0, 0, width, height), defs, frame, original: false);
            }
            case "relOff":
                return $"<g transform=\"translate({Units.Css(Fraction("tx", 0) * width)} {Units.Css(Fraction("ty", 0) * height)})\">{input}</g>";
            case "xfrm":
            {
                // Scale and skew about the shape's center, then move.
                var (sx, sy) = (Fraction("sx", 1), Fraction("sy", 1));
                var (kx, ky) = (Math.Tan((node.Long("kx") ?? 0) / 60000.0 * Math.PI / 180), Math.Tan((node.Long("ky") ?? 0) / 60000.0 * Math.PI / 180));
                var (tx, ty) = (Units.ToPx(node.Long("tx") ?? 0), Units.ToPx(node.Long("ty") ?? 0));
                var m = Matrix.Translate(width / 2 + tx, height / 2 + ty).Then(new Matrix(sx, ky, kx, sy, 0, 0)).Then(Matrix.Translate(-width / 2, -height / 2));
                return $"<g transform=\"{m.Css()}\">{input}</g>";
            }
            case "fill":
                return Fill(node, input);
            case "fillOverlay":
                return Blend(input, Fill(node, input), node.Str("blend"));
            case "blend":
                return Blend(input, node.Element(Ns.A + "cont") is { } blended ? Container(blended, input) : "", node.Str("blend"));
            case "alphaModFix":
            {
                var amount = Fraction("amt", 1);
                return amount <= 1
                    ? $"<g opacity=\"{Units.Css4(amount)}\">{input}</g>"
                    : Filtered(input, $"<feComponentTransfer><feFuncA type=\"linear\" slope=\"{Units.Css4(amount)}\"/></feComponentTransfer>");
            }
            case "alphaMod":
            {
                // The input keeps as much of its alpha as the container's output has.
                var mask = Id("dagm");
                var cont = node.Element(Ns.A + "cont") is { } c ? Container(c, input) : "";
                defs.Append($"<mask id=\"{mask}\" maskUnits=\"userSpaceOnUse\" x=\"{Units.Css(-width)}\" y=\"{Units.Css(-height)}\" width=\"{Units.Css(3 * width)}\" height=\"{Units.Css(3 * height)}\">" +
                            $"{White(cont)}</mask>");
                return $"<g mask=\"url(#{mask})\">{input}</g>";
            }
            case "alphaOutset":
            {
                // The alpha grows; the new pixels are black (measured).
                var r = Units.ToPx(node.Long("rad") ?? 0);
                return r <= 0 ? input : Filtered(input,
                    $"<feMorphology in=\"SourceAlpha\" operator=\"dilate\" radius=\"{Units.Css(r)}\" result=\"grown\"/>" +
                    "<feMerge><feMergeNode in=\"grown\"/><feMergeNode in=\"SourceGraphic\"/></feMerge>", r);
            }
            default:
            {
                // Color effects as on pictures (grayscl, lum, hsl, tint, duotone, biLevel, clrChange, clrRepl, alpha…).
                var primitives = BlipEffects.Filter(new XElement(Ns.A + "blip", new XElement(node)), colors);
                return primitives == null ? input : Filtered(input, primitives);
            }
        }
    }

    /// <summary>The input's shadow (its alpha, blurred, moved and colored); a scaled or skewed one as a transformed copy.</summary>
    string Shadow(XElement shadow, string input)
    {
        var list = new XElement(Ns.A + "effectLst", new XElement(shadow));
        if (SvgEffects.Shadow(list, colors, width, height, defs, frame) is { } copy)
            return $"<g filter=\"url(#{copy.Filter})\"><g transform=\"{copy.Transform.Css()}\">{input}</g></g>";
        var id = SvgEffects.Filter(list, colors, width, height, defs, parts: SvgEffects.Parts.Outer, frame: frame);
        return id == null ? "" : $"<g filter=\"url(#{id})\">{input}</g>";
    }

    /// <summary>The input's alpha filled with the fill of <paramref name="node"/> (a:fill, a:fillOverlay).</summary>
    string Fill(XElement node, string input)
    {
        var fill = node.Elements().FirstOrDefault(e => e.Name.LocalName.EndsWith("Fill", StringComparison.Ordinal));
        if (fill == null || fill.Name.LocalName == "noFill")
            return "";
        if (fill.Name.LocalName == "solidFill")
        {
            var color = colors.ResolveChild(fill) ?? RgbColor.Black;
            return Filtered(input, $"<feFlood flood-color=\"{color.ToHex()}\" flood-opacity=\"{Units.Css4(color.A)}\"/><feComposite in2=\"SourceAlpha\" operator=\"in\"/>");
        }
        // Gradients, pictures and patterns: a rectangle in the fill, masked by the input's alpha.
        var mask = Id("dagm");
        defs.Append($"<mask id=\"{mask}\" maskUnits=\"userSpaceOnUse\" x=\"{Units.Css(-width)}\" y=\"{Units.Css(-height)}\" width=\"{Units.Css(3 * width)}\" height=\"{Units.Css(3 * height)}\">{White(input)}</mask>");
        return $"<rect x=\"{Units.Css(-width)}\" y=\"{Units.Css(-height)}\" width=\"{Units.Css(3 * width)}\" height=\"{Units.Css(3 * height)}\"{paint(fill)} mask=\"url(#{mask})\"/>";
    }

    /// <summary>Markup drawn white with its own alpha (a mask of its coverage).</summary>
    string White(string markup) =>
        Filtered(markup, "<feFlood flood-color=\"#fff\"/><feComposite in2=\"SourceAlpha\" operator=\"in\"/>");

    /// <summary><paramref name="top"/> over <paramref name="input"/> in the blend mode (over is overlay, measured).</summary>
    static string Blend(string input, string top, string? mode)
    {
        if (top.Length == 0)
            return input;
        var css = mode switch { "mult" => "multiply", "screen" => "screen", "darken" => "darken", "lighten" => "lighten", _ => "overlay" };
        return $"<g style=\"isolation:isolate\">{input}<g style=\"mix-blend-mode:{css}\">{top}</g></g>";
    }
}
