using System.Globalization;
using System.Text;
using System.Xml.Linq;
using FreeSlate.Drawing;

namespace FreeSlate.Html;

/// <summary>
/// Picture color effects of a:blip (catalog: picture.effects) as SVG filter primitives, applied in document order
/// in sRGB. Measured against PowerPoint with a gray ramp:
/// <list type="bullet">
/// <item>lum: out = k·in + ½(1 − k) + ½·bright·(1 + k), k = 1 + contrast below zero, 1 / (1 − contrast) above;
/// half the brightness applies before the contrast, half after.</item>
/// <item>grayscl and biLevel use Rec. 709 luma (0.2126, 0.7152, 0.0722) on sRGB values.</item>
/// </list>
/// </summary>
public static class BlipEffects
{
    static string N(double v) => Math.Round(v, 5).ToString("0.#####", CultureInfo.InvariantCulture);

    const string Luma = "0.2126 0.7152 0.0722";

    /// <summary>The filter primitives for the effects in <paramref name="blip"/>, or null when there are none.</summary>
    public static string? Filter(XElement? blip, ColorContext colors)
    {
        if (blip == null)
            return null;
        var sb = new StringBuilder();
        var input = "SourceGraphic";
        var n = 0;
        string Next() => $"f{++n}";
        void Step(string primitive)
        {
            var result = Next();
            sb.Append(primitive.Replace("IN", input).Replace("OUT", result));
            input = result;
        }

        foreach (var e in blip.Elements().Where(e => e.Name.Namespace == Ns.A))
        {
            double V(string name, double fallback = 0) => (e.Long(name) ?? fallback * 100000) / 100000.0;
            switch (e.Name.LocalName)
            {
                case "grayscl":
                    Step($"<feColorMatrix in=\"IN\" type=\"matrix\" values=\"{Luma} 0 0 {Luma} 0 0 {Luma} 0 0 0 0 0 1 0\" result=\"OUT\"/>");
                    break;
                case "biLevel":
                {
                    // Luma at or above the threshold becomes white, below it black.
                    var threshold = V("thresh", 0.5);
                    Step($"<feColorMatrix in=\"IN\" type=\"matrix\" values=\"{Luma} 0 0 {Luma} 0 0 {Luma} 0 0 0 0 0 1 0\" result=\"OUT\"/>");
                    Step($"<feComponentTransfer in=\"IN\" result=\"OUT\">{Threshold("R", threshold)}{Threshold("G", threshold)}{Threshold("B", threshold)}</feComponentTransfer>");
                    break;
                }
                case "lum":
                {
                    var bright = V("bright");
                    var contrast = V("contrast");
                    var k = contrast >= 0 ? 1 / Math.Max(1e-6, 1 - contrast) : 1 + contrast;
                    var intercept = 0.5 * (1 - k) + 0.5 * bright * (1 + k);
                    var f = $"type=\"linear\" slope=\"{N(k)}\" intercept=\"{N(intercept)}\"";
                    Step($"<feComponentTransfer in=\"IN\" result=\"OUT\"><feFuncR {f}/><feFuncG {f}/><feFuncB {f}/></feComponentTransfer>");
                    break;
                }
                case "alphaModFix":
                    Step($"<feComponentTransfer in=\"IN\" result=\"OUT\"><feFuncA type=\"linear\" slope=\"{N(V("amt", 1))}\"/></feComponentTransfer>");
                    break;
                case "alphaBiLevel":
                    Step($"<feComponentTransfer in=\"IN\" result=\"OUT\">{Threshold("A", V("thresh", 0.5))}</feComponentTransfer>");
                    break;
                case "alphaCeiling":
                    Step("<feComponentTransfer in=\"IN\" result=\"OUT\"><feFuncA type=\"discrete\" tableValues=\"0 1\"/></feComponentTransfer>");
                    break;
                case "alphaFloor":
                    Step("<feComponentTransfer in=\"IN\" result=\"OUT\"><feFuncA type=\"discrete\" tableValues=\"0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 1\"/></feComponentTransfer>");
                    break;
                case "alphaInv":
                    Step("<feComponentTransfer in=\"IN\" result=\"OUT\"><feFuncA type=\"table\" tableValues=\"1 0\"/></feComponentTransfer>");
                    break;
                case "alphaRepl":
                    Step($"<feComponentTransfer in=\"IN\" result=\"OUT\"><feFuncA type=\"linear\" slope=\"0\" intercept=\"{N(V("a", 1))}\"/></feComponentTransfer>");
                    break;
                case "clrRepl":
                {
                    var color = colors.ResolveChild(e) ?? RgbColor.Black;
                    var flood = Next();
                    sb.Append($"<feFlood flood-color=\"{color.ToHex()}\" flood-opacity=\"{N(color.A)}\" result=\"{flood}\"/>");
                    Step($"<feComposite in=\"{flood}\" in2=\"IN\" operator=\"in\" result=\"OUT\"/>");
                    break;
                }
                case "duotone":
                {
                    // Luma interpolates between the first (dark) and second (light) color.
                    var duo = e.Elements().Where(c => c.Name.Namespace == Ns.A).Select(colors.Resolve).ToList();
                    if (duo.Count < 2) break;
                    var (a, b) = (duo[0], duo[1]);
                    Step($"<feColorMatrix in=\"IN\" type=\"matrix\" values=\"{Luma} 0 0 {Luma} 0 0 {Luma} 0 0 0 0 0 1 0\" result=\"OUT\"/>");
                    Step($"<feComponentTransfer in=\"IN\" result=\"OUT\"><feFuncR type=\"table\" tableValues=\"{N(a.R)} {N(b.R)}\"/><feFuncG type=\"table\" tableValues=\"{N(a.G)} {N(b.G)}\"/><feFuncB type=\"table\" tableValues=\"{N(a.B)} {N(b.B)}\"/></feComponentTransfer>");
                    break;
                }
                case "clrChange":
                {
                    // Pixels of the "from" color take the "to" color (usually transparent), with a small tolerance; with
                    // useA (the default) their alpha has to match as well.
                    var from = colors.ResolveChild(e.Element(Ns.A + "clrFrom"));
                    var to = colors.ResolveChild(e.Element(Ns.A + "clrTo"));
                    if (from is not { } f || to is not { } t) break;
                    var useAlpha = e.Bool("useA") ?? true;
                    var source = input;
                    var mask = Next();
                    var alpha = useAlpha ? Near("A", f.A) : "<feFuncA type=\"linear\" slope=\"0\" intercept=\"1\"/>";
                    var weights = useAlpha ? "0.25 0.25 0.25 0.25" : "0.3334 0.3333 0.3333 0";
                    sb.Append($"<feComponentTransfer in=\"{source}\" result=\"{mask}m\">{Near("R", f.R)}{Near("G", f.G)}{Near("B", f.B)}{alpha}</feComponentTransfer>");
                    sb.Append($"<feColorMatrix in=\"{mask}m\" type=\"matrix\" values=\"0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 {weights} 0\" result=\"{mask}s\"/>");
                    sb.Append($"<feComponentTransfer in=\"{mask}s\" result=\"{mask}\"><feFuncA type=\"discrete\" tableValues=\"0 0 0 0 0 0 0 0 0 1\"/></feComponentTransfer>");
                    var kept = Next();
                    sb.Append($"<feComposite in=\"{source}\" in2=\"{mask}\" operator=\"out\" result=\"{kept}\"/>");
                    if (t.A > 0.001)
                    {
                        var flood = Next();
                        sb.Append($"<feFlood flood-color=\"{t.ToHex()}\" flood-opacity=\"{N(t.A)}\"/><feComposite in2=\"{mask}\" operator=\"in\" result=\"{flood}\"/>");
                        Step($"<feMerge result=\"OUT\"><feMergeNode in=\"{kept}\"/><feMergeNode in=\"{flood}\"/></feMerge>");
                    }
                    else
                    {
                        input = kept;
                    }
                    break;
                }
                case "hsl":
                {
                    var hue = (e.Long("hue") ?? 0) / 60000.0;
                    var sat = V("sat");
                    var lum = V("lum");
                    if (hue != 0) Step($"<feColorMatrix in=\"IN\" type=\"hueRotate\" values=\"{N(hue)}\" result=\"OUT\"/>");
                    if (sat != 0) Step($"<feColorMatrix in=\"IN\" type=\"saturate\" values=\"{N(Math.Max(0, 1 + sat))}\" result=\"OUT\"/>");
                    if (lum != 0)
                    {
                        var f = lum > 0 ? $"type=\"linear\" slope=\"{N(1 - lum)}\" intercept=\"{N(lum)}\"" : $"type=\"linear\" slope=\"{N(1 + lum)}\"";
                        Step($"<feComponentTransfer in=\"IN\" result=\"OUT\"><feFuncR {f}/><feFuncG {f}/><feFuncB {f}/></feComponentTransfer>");
                    }
                    break;
                }
                case "tint":
                {
                    // Tint towards a hue: the luma colored with the hue, mixed by the amount.
                    var amount = V("amt");
                    if (amount <= 0) break;
                    var hue = (e.Long("hue") ?? 0) / 60000.0;
                    var color = RgbColor.FromHsl(hue, 1, 0.5);
                    var keep = 1 - amount;
                    string Row(double c, int channel) =>
                        string.Join(' ', Enumerable.Range(0, 3).Select(i => N((i == channel ? keep : 0) + amount * c * (i == 0 ? 0.2126 : i == 1 ? 0.7152 : 0.0722) * 2)));
                    Step($"<feColorMatrix in=\"IN\" type=\"matrix\" values=\"{Row(color.R, 0)} 0 0 {Row(color.G, 1)} 0 0 {Row(color.B, 2)} 0 0 0 0 0 1 0\" result=\"OUT\"/>");
                    break;
                }
                case "blur":
                {
                    var radius = Units.ToPx(e.Long("rad") ?? 0);
                    if (radius > 0) Step($"<feGaussianBlur in=\"IN\" stdDeviation=\"{N(radius / 2)}\" result=\"OUT\"/>");
                    break;
                }
            }
        }
        return n == 0 && input == "SourceGraphic" ? null : sb.ToString();
    }

    /// <summary>A transfer function: 0 below the threshold, 1 from it on (256 steps).</summary>
    static string Threshold(string channel, double threshold)
    {
        var values = Enumerable.Range(0, 256).Select(i => i / 255.0 >= threshold ? "1" : "0");
        return $"<feFunc{channel} type=\"discrete\" tableValues=\"{string.Join(' ', values)}\"/>";
    }

    /// <summary>1 for values within a small tolerance of <paramref name="target"/>, else 0 (256 steps).</summary>
    static string Near(string channel, double target)
    {
        var values = Enumerable.Range(0, 256).Select(i => Math.Abs(i / 255.0 - target) <= 3.5 / 255 ? "1" : "0");
        return $"<feFunc{channel} type=\"discrete\" tableValues=\"{string.Join(' ', values)}\"/>";
    }
}
