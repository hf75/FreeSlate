using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace FreeSlate.Editor.Controls;

/// <summary>
/// The title bar's content. It has the pseudo class :inactive while its window is not the active one, and it shows the tips of its
/// buttons: tool tips need a window among their ancestors, which the layers of a frame do not have.
/// </summary>
public sealed class CaptionBar : Panel
{
    /// <summary>How long the pointer rests on a button before its tip shows, and for how long after a tip the next one needs no waiting.</summary>
    const int TipDelay = 600, TipFollows = 400;

    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(TipDelay) };
    Window? window;
    CaptionButton? under, tipOf;
    long tipWentAt = long.MinValue / 2;

    public CaptionBar() => timer.Tick += (_, _) => ShowTip();

    CaptionTip? Tip => Children.OfType<CaptionTip>().FirstOrDefault();

    public void PointerOn(CaptionButton button)
    {
        under = button;
        if (Environment.TickCount64 - tipWentAt < TipFollows)
            ShowTip();
        else
            timer.Start();
    }

    public void PointerOff()
    {
        under = null;
        timer.Stop();
        if (Tip is { IsShown: true } tip)
        {
            tip.IsShown = false;
            tipWentAt = Environment.TickCount64;
        }
    }

    /// <summary>The button is in use: no tip, until the pointer comes anew.</summary>
    public void Pressed()
    {
        PointerOff();
        tipWentAt = long.MinValue / 2;
    }

    void ShowTip()
    {
        timer.Stop();
        if (under?.Tip is not { } text || Tip is not { } tip)
            return;
        tipOf = under;
        tip.Text = text;
        tip.IsShown = true;
        InvalidateArrange();
    }

    /// <summary>All children fill the bar; the tip hangs beneath it, under the middle of its button but within the window.</summary>
    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            if (child is not CaptionTip tip)
            {
                child.Arrange(new Rect(finalSize));
                continue;
            }
            var middle = tipOf?.TranslatePoint(new Point(tipOf.Bounds.Width / 2, 0), this)?.X ?? 0;
            var x = Math.Min(middle - tip.DesiredSize.Width / 2, finalSize.Width - tip.DesiredSize.Width - 6);
            tip.Arrange(new Rect(new Point(x, finalSize.Height + 4), tip.DesiredSize));
        }
        return finalSize;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // The frame's layers are the neighbours of the window under the common root, not its children.
        window = this.GetVisualAncestors().LastOrDefault()?.GetVisualChildren().OfType<Window>().FirstOrDefault();
        if (window != null)
        {
            window.Activated += Activated;
            window.Deactivated += Deactivated;
            PseudoClasses.Set(":inactive", !window.IsActive);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        timer.Stop();
        if (window != null)
        {
            window.Activated -= Activated;
            window.Deactivated -= Deactivated;
            window = null;
        }
    }

    void Activated(object? sender, EventArgs e) => PseudoClasses.Set(":inactive", false);

    void Deactivated(object? sender, EventArgs e) => PseudoClasses.Set(":inactive", true);
}
