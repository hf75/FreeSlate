using System.Xml.Linq;

namespace FreeSlate.Drawing;

/// <summary>
/// Resolves DrawingML colors (ECMA-376 Part 1, §20.1.2.3) with their transforms against a theme, the color map of
/// the master/slide and an optional placeholder color (phClr, set by style references).
/// </summary>
public sealed class ColorContext(Theme theme, IReadOnlyDictionary<string, string> colorMap, RgbColor? placeholder = null)
{
    static readonly HashSet<string> ColorElements = ["srgbClr", "schemeClr", "sysClr", "prstClr", "hslClr", "scrgbClr"];

    public Theme Theme { get; } = theme;

    public ColorContext WithPlaceholder(RgbColor? color) => new(Theme, colorMap, color);

    /// <summary>The same color map with another theme (theme overrides of slides and charts).</summary>
    public ColorContext WithTheme(Theme theme) => new(theme, colorMap, placeholder);

    /// <summary>The context with a color map override (a chart's c:clrMapOvr): its mappings replace the current ones.</summary>
    public ColorContext WithColorMap(IEnumerable<KeyValuePair<string, string>> overrides)
    {
        var map = new Dictionary<string, string>(colorMap, StringComparer.Ordinal);
        foreach (var (name, target) in overrides)
            map[name] = target;
        return new ColorContext(Theme, map, placeholder);
    }

    /// <summary>The color of the first color child of <paramref name="parent"/>, if any.</summary>
    public RgbColor? ResolveChild(XElement? parent)
    {
        var color = parent?.Elements().FirstOrDefault(e => ColorElements.Contains(e.Name.LocalName));
        return color == null ? null : Resolve(color);
    }

    public RgbColor Resolve(XElement color)
    {
        var baseColor = color.Name.LocalName switch
        {
            "srgbClr" => RgbColor.FromHex(color.Str("val") ?? "000000"),
            "sysClr" => RgbColor.FromHex(color.Str("lastClr") ?? (color.Str("val") == "window" ? "FFFFFF" : "000000")),
            "schemeClr" => Scheme(color.Str("val") ?? "tx1"),
            "prstClr" => PresetColors.Get(color.Str("val")),
            "hslClr" => RgbColor.FromHsl((color.Long("hue") ?? 0) / 60000.0, (color.Long("sat") ?? 0) / 100000.0, (color.Long("lum") ?? 0) / 100000.0),
            "scrgbClr" => new RgbColor((color.Long("r") ?? 0) / 100000.0, (color.Long("g") ?? 0) / 100000.0, (color.Long("b") ?? 0) / 100000.0).FromLinear(),
            _ => RgbColor.Black,
        };
        return ApplyTransforms(baseColor, color.Elements());
    }

    RgbColor Scheme(string name)
    {
        if (name == "phClr")
            return placeholder ?? RgbColor.Black;
        var mapped = colorMap.TryGetValue(name, out var target) ? target : name switch
        {
            "bg1" => "lt1", "tx1" => "dk1", "bg2" => "lt2", "tx2" => "dk2", _ => name,
        };
        return Theme.Colors.TryGetValue(mapped, out var color) ? color : RgbColor.Black;
    }

    /// <summary>Applies color transforms in document order (lum/sat/hue in HSL, tint/shade and channels in linear RGB).</summary>
    public static RgbColor ApplyTransforms(RgbColor color, IEnumerable<XElement> transforms)
    {
        foreach (var t in transforms)
        {
            var val = (t.Long("val") ?? 0) / 100000.0;
            switch (t.Name.LocalName)
            {
                case "alpha": color = color.WithAlpha(val); break;
                case "alphaMod": color = color.WithAlpha(color.A * val); break;
                case "alphaOff": color = color.WithAlpha(color.A + val); break;
                case "lumMod": color = Hsl(color, (h, s, l) => (h, s, l * val)); break;
                case "lumOff": color = Hsl(color, (h, s, l) => (h, s, l + val)); break;
                case "lum": color = Hsl(color, (h, s, _) => (h, s, val)); break;
                case "satMod": color = Hsl(color, (h, s, l) => (h, s * val, l)); break;
                case "satOff": color = Hsl(color, (h, s, l) => (h, s + val, l)); break;
                case "sat": color = Hsl(color, (h, _, l) => (h, val, l)); break;
                case "hueMod": color = Hsl(color, (h, s, l) => (h * val, s, l)); break;
                case "hueOff": color = Hsl(color, (h, s, l) => (h + (t.Long("val") ?? 0) / 60000.0, s, l)); break;
                case "hue": color = Hsl(color, (_, s, l) => ((t.Long("val") ?? 0) / 60000.0, s, l)); break;
                case "comp": color = Hsl(color, (h, s, l) => (h + 180, s, l)); break;
                case "tint": color = Linear(color, c => 1 - (1 - c) * val); break;
                case "shade": color = Linear(color, c => c * val); break;
                case "inv": color = new RgbColor(1 - color.R, 1 - color.G, 1 - color.B, color.A); break;
                case "gray":
                    var y = 0.299 * color.R + 0.587 * color.G + 0.114 * color.B;
                    color = new RgbColor(y, y, y, color.A);
                    break;
                case "red": color = Channel(color, 0, _ => val); break;
                case "redMod": color = Channel(color, 0, c => c * val); break;
                case "redOff": color = Channel(color, 0, c => c + val); break;
                case "green": color = Channel(color, 1, _ => val); break;
                case "greenMod": color = Channel(color, 1, c => c * val); break;
                case "greenOff": color = Channel(color, 1, c => c + val); break;
                case "blue": color = Channel(color, 2, _ => val); break;
                case "blueMod": color = Channel(color, 2, c => c * val); break;
                case "blueOff": color = Channel(color, 2, c => c + val); break;
                case "gamma": color = color.ToLinear(); break;
                case "invGamma": color = color.FromLinear(); break;
            }
            color = color.Clamp();
        }
        return color;
    }

    static RgbColor Hsl(RgbColor c, Func<double, double, double, (double H, double S, double L)> change)
    {
        var (h, s, l) = c.ToHsl();
        var (nh, ns, nl) = change(h, s, l);
        return RgbColor.FromHsl(nh, ns, nl, c.A);
    }

    static RgbColor Linear(RgbColor c, Func<double, double> change)
    {
        var lin = c.ToLinear();
        return new RgbColor(change(lin.R), change(lin.G), change(lin.B), c.A).FromLinear();
    }

    static RgbColor Channel(RgbColor c, int channel, Func<double, double> change)
    {
        var lin = c.ToLinear();
        lin = channel switch
        {
            0 => lin with { R = change(lin.R) },
            1 => lin with { G = change(lin.G) },
            _ => lin with { B = change(lin.B) },
        };
        return lin.Clamp().FromLinear();
    }
}
