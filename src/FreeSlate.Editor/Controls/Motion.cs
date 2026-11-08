using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;

namespace FreeSlate.Editor.Controls;

/// <summary>
/// How things come and go over the sheets, the ring and the cards alike. They come in Appear, fading in and growing the last little way
/// to their size, slowing as they arrive; they go faster, in Leave (going is to read as quick, not as slow), fading out and shrinking that
/// little way, quickening as they go. The clock is the stopwatch's, finer than the system's tick, and what moves is drawn again for every
/// frame of the screen (see Frames).
/// </summary>
public static class Motion
{
    /// <summary>How long a thing takes to come, and to go, in milliseconds.</summary>
    public const double Appear = 120, Leave = 90;

    /// <summary>The clock now.</summary>
    public static long Now => Stopwatch.GetTimestamp();

    /// <summary>The milliseconds since a time of the clock.</summary>
    public static double Since(long time) => Stopwatch.GetElapsedTime(time).TotalMilliseconds;

    /// <summary>How far a thing that began to come at a time of the clock has come, from 0 to 1.</summary>
    public static double Coming(long since) => Math.Clamp(Since(since) / Appear, 0, 1);

    /// <summary>How much is left of a thing that began to go at a time of the clock, from 1 to 0.</summary>
    public static double Going(long since) => 1 - Math.Clamp(Since(since) / Leave, 0, 1);

    /// <summary>
    /// A thing's size, as a share of its own, for how far it has come (0 to 1), growing from <paramref name="from"/>: slowing as it
    /// arrives; going, the same way back, quickening as it leaves.
    /// </summary>
    public static double Size(double presence, double from) => from + (1 - from) * (1 - Math.Pow(1 - presence, 3));
}

/// <summary>
/// Frames for what moves: the step runs before each frame the screen draws (a page's frames in a browser), for as long as it says it goes
/// on.
/// </summary>
public sealed class Frames(Visual visual, Func<bool> step)
{
    bool asked;

    /// <summary>Asks for frames, unless they come already; false where none come, the visual being in no window.</summary>
    public bool Start()
    {
        if (asked)
            return true;
        if (TopLevel.GetTopLevel(visual) is not { } top)
            return false;
        asked = true;
        top.RequestAnimationFrame(_ =>
        {
            asked = false;
            if (step())
                Start();
        });
        return true;
    }
}
