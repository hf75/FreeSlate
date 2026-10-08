namespace Engine.Pptx.Charts;

/// <summary>The scale of a value axis: range, major and minor unit.</summary>
public readonly record struct AxisScale(double Min, double Max, double MajorUnit, double MinorUnit, double? LogBase = null)
{
    /// <summary>Position of <paramref name="value"/> between Min (0) and Max (1).</summary>
    public double Fraction(double value)
    {
        if (LogBase is { } b)
        {
            var lo = Math.Log(Min, b);
            var hi = Math.Log(Max, b);
            return value <= 0 || hi <= lo ? 0 : (Math.Log(value, b) - lo) / (hi - lo);
        }
        return Max > Min ? (value - Min) / (Max - Min) : 0;
    }

    /// <summary>The major tick values from Min to Max.</summary>
    public IEnumerable<double> Ticks() => Steps(MajorUnit);

    public IEnumerable<double> MinorTicks() => Steps(MinorUnit);

    IEnumerable<double> Steps(double unit)
    {
        if (LogBase is { } b)
        {
            for (var v = Min; v <= Max * (1 + 1e-9); v *= Math.Pow(b, Math.Max(1, Math.Round(unit))))
                yield return v;
            yield break;
        }
        if (unit <= 0 || Max <= Min)
            yield break;
        var count = (int)Math.Floor((Max - Min) / unit + 1e-9);
        for (var i = 0; i <= Math.Min(count, 1000); i++)
            yield return Clean(Min + i * unit);
    }

    /// <summary>Removes floating point noise such as 0.30000000000000004.</summary>
    public static double Clean(double value) => Math.Abs(value) < 1e-12 ? 0 : Math.Round(value, 12 - (int)Math.Floor(Math.Log10(Math.Abs(value))));
}

/// <summary>
/// Automatic value axis scaling like Office (catalog: chart.axisScaling): zero is included unless the data lie
/// within the top sixth of their maximum, the maximum gets 5 % headroom, and the major unit is the smallest
/// 1-2-5 step that keeps the number of intervals within what the axis length allows.
/// </summary>
public static class AxisScaling
{
    public static AxisScale Compute(double dataMin, double dataMax, ChartAxis axis, int maxIntervals, bool percent = false)
    {
        if (axis.LogBase is { } logBase and > 1)
            return Logarithmic(dataMin, dataMax, axis, logBase);

        if (percent)
        {
            // 100 % stacked: exactly 0 … 100 % (or −100 %), without headroom.
            var pMin = axis.Min ?? (dataMin < -1e-12 ? -1 : 0);
            var pMax = axis.Max ?? (dataMax > 1e-12 || pMin == 0 ? 1 : 0);
            var pMajor = axis.MajorUnit is { } m and > 0 ? m : NiceUnit(pMax - pMin, Math.Max(1, maxIntervals));
            return new AxisScale(pMin, pMax, pMajor, axis.MinorUnit is { } n and > 0 ? n : pMajor / 5);
        }
        if (double.IsInfinity(dataMin) || double.IsInfinity(dataMax) || double.IsNaN(dataMin) || double.IsNaN(dataMax))
            (dataMin, dataMax) = (0, 1);
        if (dataMin == dataMax)
        {
            if (dataMax == 0) (dataMin, dataMax) = (0, 1);
            else if (dataMax > 0) dataMin = 0;
            else dataMax = 0;
        }

        // The minimum: zero unless all values are positive and close together (or all negative).
        double lo = dataMin, hi = dataMax;
        if (axis.Min is { } fixedMin) lo = fixedMin;
        else if (dataMin >= 0) lo = dataMax - dataMin > dataMax / 6 ? 0 : dataMin - (dataMax - dataMin) / 2;
        if (axis.Max is { } fixedMax) hi = fixedMax;
        else if (dataMax <= 0) hi = dataMax - dataMin > -dataMin / 6 ? 0 : dataMax + (dataMax - dataMin) / 2;

        // Headroom of 5 % of the range on the open ends.
        var range = Math.Max(hi - lo, Math.Abs(hi) * 1e-9 + 1e-300);
        var top = axis.Max == null && hi > 0 ? hi + 0.05 * range : hi;
        var bottom = axis.Min == null && lo < 0 && dataMin < 0 ? lo - 0.05 * range : lo;

        var major = axis.MajorUnit is { } fixedMajor and > 0 ? fixedMajor : NiceUnit(top - bottom, Math.Max(1, maxIntervals));
        var min = axis.Min ?? Math.Floor(AxisScale.Clean(bottom / major)) * major;
        var max = axis.Max ?? Math.Ceiling(AxisScale.Clean(top / major)) * major;
        if (axis.Min == null && dataMin >= 0 && min < 0) min = 0;
        if (max <= min) max = min + major;
        var minor = axis.MinorUnit is { } fixedMinor and > 0 ? fixedMinor : major / 5;
        return new AxisScale(AxisScale.Clean(min), AxisScale.Clean(max), major, minor);
    }

    /// <summary>The smallest 1, 2 or 5 × 10ⁿ step that divides <paramref name="range"/> into at most <paramref name="maxIntervals"/> parts.</summary>
    public static double NiceUnit(double range, int maxIntervals)
    {
        if (range <= 0)
            return 1;
        var raw = range / maxIntervals;
        var power = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        foreach (var step in new[] { 1.0, 2, 5, 10 })
        {
            if (step * power >= raw * (1 - 1e-9))
                return AxisScale.Clean(step * power);
        }
        return AxisScale.Clean(10 * power);
    }

    static AxisScale Logarithmic(double dataMin, double dataMax, ChartAxis axis, double logBase)
    {
        dataMin = dataMin > 0 ? dataMin : 1;
        dataMax = Math.Max(dataMax, dataMin);
        var min = axis.Min ?? Math.Pow(logBase, Math.Floor(Math.Log(dataMin, logBase) + 1e-9));
        var max = axis.Max ?? Math.Pow(logBase, Math.Ceiling(Math.Log(dataMax, logBase) - 1e-9));
        if (max <= min) max = min * logBase;
        return new AxisScale(min, max, axis.MajorUnit ?? 1, axis.MinorUnit ?? 1, logBase);
    }
}
