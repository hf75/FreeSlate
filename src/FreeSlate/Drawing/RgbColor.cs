using System.Globalization;

namespace FreeSlate.Drawing;

/// <summary>An sRGB color with alpha; all components 0..1.</summary>
public readonly record struct RgbColor(double R, double G, double B, double A = 1)
{
    public static readonly RgbColor Black = new(0, 0, 0);
    public static readonly RgbColor White = new(1, 1, 1);

    public static RgbColor FromHex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
            return Black;
        return new RgbColor(((v >> 16) & 0xFF) / 255.0, ((v >> 8) & 0xFF) / 255.0, (v & 0xFF) / 255.0);
    }

    public string ToHex()
    {
        static int C(double v) => (int)Math.Round(Math.Clamp(v, 0, 1) * 255);
        return $"#{C(R):x2}{C(G):x2}{C(B):x2}";
    }

    public RgbColor WithAlpha(double alpha) => this with { A = Math.Clamp(alpha, 0, 1) };

    public RgbColor Clamp() => new(Math.Clamp(R, 0, 1), Math.Clamp(G, 0, 1), Math.Clamp(B, 0, 1), Math.Clamp(A, 0, 1));

    /// <summary>Hue in degrees 0..360, saturation and lightness 0..1.</summary>
    public (double H, double S, double L) ToHsl()
    {
        var max = Math.Max(R, Math.Max(G, B));
        var min = Math.Min(R, Math.Min(G, B));
        var l = (max + min) / 2;
        if (max - min < 1e-12)
            return (0, 0, l);
        var d = max - min;
        var s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        double h;
        if (max == R) h = (G - B) / d + (G < B ? 6 : 0);
        else if (max == G) h = (B - R) / d + 2;
        else h = (R - G) / d + 4;
        return (h * 60, s, l);
    }

    /// <param name="unclamped">Whether a saturation above 1 stays, the channels then past 0 and 1.</param>
    public static RgbColor FromHsl(double h, double s, double l, double a = 1, bool unclamped = false)
    {
        h = ((h % 360) + 360) % 360 / 360;
        s = unclamped ? Math.Max(0, s) : Math.Clamp(s, 0, 1);
        l = Math.Clamp(l, 0, 1);
        if (s < 1e-12)
            return new RgbColor(l, l, l, a);
        var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        var p = 2 * l - q;
        static double Hue(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }
        return new RgbColor(Hue(p, q, h + 1.0 / 3), Hue(p, q, h), Hue(p, q, h - 1.0 / 3), a);
    }

    // Office converts between sRGB and its linear "scRGB" with a plain power curve.
    const double Gamma = 2.3;

    public RgbColor ToLinear() => new(Math.Pow(R, Gamma), Math.Pow(G, Gamma), Math.Pow(B, Gamma), A);

    public RgbColor FromLinear() => new(Math.Pow(Math.Max(R, 0), 1 / Gamma), Math.Pow(Math.Max(G, 0), 1 / Gamma), Math.Pow(Math.Max(B, 0), 1 / Gamma), A);

    public static RgbColor Mix(RgbColor a, RgbColor b, double t) =>
        new(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);
}
