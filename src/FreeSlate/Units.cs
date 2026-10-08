using System.Globalization;

namespace FreeSlate;

/// <summary>Length units of Office Open XML.</summary>
public static class Units
{
    public const double EmuPerInch = 914400;
    public const double EmuPerPoint = 12700;

    /// <summary>EMU per CSS pixel at 96 dpi.</summary>
    public const double EmuPerPixel = 9525;

    public static double ToPx(double emu) => emu / EmuPerPixel;

    /// <summary>Formats a number for CSS/SVG output: invariant, at most two decimals, no trailing zeros.</summary>
    public static string Css(double value) =>
        Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Like <see cref="Css"/> with four decimals, for factors such as matrix coefficients.</summary>
    public static string Css4(double value) =>
        Math.Round(value, 4).ToString("0.####", CultureInfo.InvariantCulture);
}
