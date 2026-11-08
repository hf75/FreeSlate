using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace FreeSlate.Editor.Controls;

/// <summary>
/// The coming and going of a card over the sheets (see Motion): shown, it fades in and grows to its size from its origin, on the side of
/// what it belongs to (a picture's card from its top); hidden, it fades out and shrinks back there, takes no pointer meanwhile, and then
/// is gone (not visible). Shown again before it is gone, it turns back from where it is.
/// </summary>
public sealed class Fader
{
    readonly Control control;
    readonly double from;
    readonly bool hits;
    readonly ScaleTransform scaled = new();
    readonly Frames frames;

    /// <summary>Whether it is shown (coming or there), how far it had come when it last turned, and when that was.</summary>
    bool shown;
    double start;
    long since;

    /// <summary>What is to be done once it is gone.</summary>
    Action? gone;

    /// <param name="control">The card, hidden from now on until shown; where it takes the pointer, it takes it again when shown.</param>
    /// <param name="origin">The point it grows from and shrinks to.</param>
    /// <param name="from">The share of its size it grows from; 1, it only fades.</param>
    public Fader(Control control, RelativePoint origin, double from = 0.96)
    {
        (this.control, this.from, hits) = (control, from, control.IsHitTestVisible);
        control.RenderTransformOrigin = origin;
        control.IsVisible = false;
        frames = new Frames(control, Step);
    }

    /// <summary>Whether it is shown, coming or there; not while it goes.</summary>
    public bool IsShown => shown;

    /// <summary>Shows it: it comes, or turns back where it was going.</summary>
    public void Show()
    {
        gone = null;
        if (shown)
            return;
        Turn(showing: true);
        control.IsVisible = true;
        control.IsHitTestVisible = hits;
        Go();
    }

    /// <summary>Hides it: it goes, taking no pointer; <paramref name="then"/> is done once it is gone, unless it is shown again before.</summary>
    public void Hide(Action? then = null)
    {
        if (!shown)
        {
            if (control.IsVisible)
                gone += then;
            else
                then?.Invoke();
            return;
        }
        Turn(showing: false);
        gone = then;
        control.IsHitTestVisible = false;
        Go();
    }

    /// <summary>How far it has come now, from 0 to 1.</summary>
    double Presence => shown ? Math.Min(1, start + Motion.Since(since) / Motion.Appear) : Math.Max(0, start - Motion.Since(since) / Motion.Leave);

    void Turn(bool showing)
    {
        start = control.IsVisible ? Presence : 0;
        (shown, since) = (showing, Motion.Now);
    }

    /// <summary>On its way, frame by frame; in no window, there or gone at once.</summary>
    void Go()
    {
        if (!frames.Start())
            (start, since) = (shown ? 1 : 0, Motion.Now);
        Step();
    }

    /// <summary>The card as far as it has come; false once it is there, or gone.</summary>
    bool Step()
    {
        var presence = Presence;
        control.Opacity = presence;
        var size = Motion.Size(presence, from);
        (scaled.ScaleX, scaled.ScaleY) = (size, size);
        control.RenderTransform = size < 1 ? scaled : null;
        if (shown)
            return presence < 1;
        if (presence > 0)
            return true;
        control.IsVisible = false;
        var then = gone;
        gone = null;
        then?.Invoke();
        return false;
    }
}
