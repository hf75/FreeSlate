using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace FreeSlate.Editor.Controls;

/// <summary>
/// The title bar's content. It has the pseudo class :inactive while its window is not the active one, and it shows the tips of its
/// buttons: tool tips need a window among their ancestors, which the layers of a frame do not have. It also has the pseudo class
/// :resting while the pointer has been away from the bar for a while: the window's buttons then fade away (see the theme).
/// </summary>
public sealed class CaptionBar : Panel
{
    /// <summary>How long the pointer rests on a button before its tip shows, and for how long after a tip the next one needs no waiting.</summary>
    const int TipDelay = 600, TipFollows = 400;

    /// <summary>How long the window's buttons stay after the pointer has left the bar. With their fade they are gone after about 3 s.</summary>
    const int ButtonsStay = 2200;

    const uint WM_NCMOUSEMOVE = 0xA0, WM_MOUSEMOVE = 0x200;

    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(TipDelay) };
    readonly DispatcherTimer buttonsTimer = new() { Interval = TimeSpan.FromMilliseconds(ButtonsStay) };
    Window? window;
    CaptionButton? under, tipOf;
    long tipWentAt = long.MinValue / 2, pointerSeenAt;

    public CaptionBar()
    {
        timer.Tick += (_, _) => ShowTip();
        buttonsTimer.Tick += (_, _) => LetButtonsRest();
    }

    CaptionTip? Tip => Children.OfType<CaptionTip>().FirstOrDefault();

    public void PointerOn(CaptionButton button)
    {
        if (!button.ShowsTip)
            return;
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
            // Only where the pointer can be followed over the bar; so far on Windows.
            if (OperatingSystem.IsWindows())
            {
                Win32Properties.AddWndProcHookCallback(window, FollowPointer);
                PointerOnBar();
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        timer.Stop();
        buttonsTimer.Stop();
        if (window != null)
        {
            window.Activated -= Activated;
            window.Deactivated -= Deactivated;
            Win32Properties.RemoveWndProcHookCallback(window, FollowPointer);
            window = null;
        }
    }

    void Activated(object? sender, EventArgs e) => PseudoClasses.Set(":inactive", false);

    void Deactivated(object? sender, EventArgs e) => PseudoClasses.Set(":inactive", true);

    /// <summary>
    /// Sees the pointer's moves in the window's messages: over the part of the bar that drags the window they are the platform's,
    /// and no pointer events reach the controls.
    /// </summary>
    IntPtr FollowPointer(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (window != null && message is WM_MOUSEMOVE or WM_NCMOUSEMOVE)
        {
            var x = (short)lParam.ToInt64();
            var y = (short)(lParam.ToInt64() >> 16);
            var overBar = message == WM_MOUSEMOVE
                ? y / window.RenderScaling < Bounds.Height
                : window.PointToClient(new PixelPoint(x, y)).Y < Bounds.Height;
            if (overBar)
                PointerOnBar();
        }
        return IntPtr.Zero;
    }

    void PointerOnBar()
    {
        pointerSeenAt = Environment.TickCount64;
        PseudoClasses.Set(":resting", false);
        buttonsTimer.Start();
    }

    void LetButtonsRest()
    {
        var waited = Environment.TickCount64 - pointerSeenAt;
        if (waited < ButtonsStay)
            buttonsTimer.Interval = TimeSpan.FromMilliseconds(ButtonsStay - waited);
        else if (PointerRestsOnBar())
        {
            pointerSeenAt = Environment.TickCount64;
            buttonsTimer.Interval = TimeSpan.FromMilliseconds(ButtonsStay);
        }
        else
        {
            buttonsTimer.Stop();
            buttonsTimer.Interval = TimeSpan.FromMilliseconds(ButtonsStay);
            PseudoClasses.Set(":resting", true);
        }
    }

    /// <summary>A pointer that rests sends no messages, and one that leaves over the window's edge none either: so it is asked for at the end.</summary>
    bool PointerRestsOnBar() =>
        window?.TryGetPlatformHandle() is { } handle
        && GetCursorPos(out var at)
        && WindowFromPoint(at) == handle.Handle
        && window.PointToClient(new PixelPoint(at.X, at.Y)).Y < Bounds.Height;

    [StructLayout(LayoutKind.Sequential)]
    struct ScreenPoint
    {
        public int X, Y;
    }

    [DllImport("user32.dll")]
    static extern bool GetCursorPos(out ScreenPoint point);

    [DllImport("user32.dll")]
    static extern IntPtr WindowFromPoint(ScreenPoint point);
}
