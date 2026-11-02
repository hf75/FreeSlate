using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Threading;

namespace FreeSlate.Editor;

/// <summary>
/// The editor's window on the desktop. Its frame is the app's own (see WindowDrawnDecorations in Themes/Controls.axaml),
/// its size and place come back with the next run.
/// </summary>
public sealed class EditorWindow : Window
{
    const double StartWidth = 1180, StartHeight = 760;

    /// <summary>How long Windows takes to show a window maximizing, and how long the content takes to fade out and to settle.</summary>
    const int MaximizingTakes = 260, FadeTakes = 110, SettlingTakes = 40;

    WindowPlace place = new();
    bool maximizedBeforeFullScreen, onFullScreenTrip, changingFullScreen;
    WindowPlacement normalPlace;

    public EditorWindow()
    {
        Title = "FreeSlate";
        Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://FreeSlate.Editor/Assets/FreeSlate.ico")));
        ExtendClientAreaToDecorationsHint = true;
        MinWidth = 520;
        MinHeight = 360;
        Comeback(Settings.Current.Window);
        Content = new EditorView();

        PositionChanged += (_, _) => Note();
        SizeChanged += (_, _) => Note();
    }

    /// <summary>Back to the place of the last run if it still lies on a screen, otherwise a calm size in the middle of the main screen.</summary>
    void Comeback(WindowPlace? last)
    {
        var screen = last == null ? null : Screens.All.FirstOrDefault(s =>
            s.WorkingArea.Contains(new PixelPoint(last.X + (int)(last.Width * s.Scaling / 2), last.Y + (int)(10 * s.Scaling))));
        if (screen != null)
        {
            place = last!;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint(last!.X, last.Y);
            Width = Math.Max(last.Width, MinWidth);
            Height = Math.Max(last.Height, MinHeight);
            WindowState = last.Maximized ? WindowState.Maximized : WindowState.Normal;
            return;
        }
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var room = Screens.Primary is { } main ? main.WorkingArea.Size.ToSize(main.Scaling) : new Size(StartWidth, StartHeight);
        Width = Math.Min(StartWidth, room.Width * 0.92);
        Height = Math.Min(StartHeight, room.Height * 0.92);
    }

    /// <summary>
    /// Remembers the place, later: maximizing moves the window before its state says so, and only what stood there as a normal
    /// window counts.
    /// </summary>
    void Note() => Dispatcher.UIThread.Post(() =>
    {
        if (WindowState == WindowState.Normal)
            place = new WindowPlace { X = Position.X, Y = Position.Y, Width = ClientSize.Width, Height = ClientSize.Height };
        if (!onFullScreenTrip && WindowState is WindowState.Normal or WindowState.Maximized)
            place.Maximized = WindowState == WindowState.Maximized;
    });

    /// <summary>F11 goes to full screen and back, Escape only back.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
            return;
        if (e.Key == Key.F11 || (WindowState == WindowState.FullScreen && e.Key == Key.Escape))
        {
            ToggleFullScreen();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Goes to full screen and back, not at a stroke: by way of the maximized window, whose change Windows animates (unless the user
    /// has turned that off), while the content fades out and comes back in its new place.
    /// </summary>
    public void ToggleFullScreen()
    {
        if (changingFullScreen)
            return;
        changingFullScreen = true;
        Classes.Set("changing", true);
        if (WindowState == WindowState.FullScreen)
        {
            After(FadeTakes, () =>
            {
                WindowState = WindowState.Maximized;
                After(SettlingTakes, () =>
                {
                    RestoreNormalPlace();
                    if (!maximizedBeforeFullScreen)
                        WindowState = WindowState.Normal;
                    onFullScreenTrip = false;
                    After(maximizedBeforeFullScreen ? SettlingTakes : MaximizingTakes, FullScreenChanged);
                });
            });
        }
        else
        {
            maximizedBeforeFullScreen = WindowState == WindowState.Maximized;
            onFullScreenTrip = true;
            RememberNormalPlace();
            WindowState = WindowState.Maximized;
            After(maximizedBeforeFullScreen ? FadeTakes : MaximizingTakes, () =>
            {
                NoLongerMaximized();
                WindowState = WindowState.FullScreen;
                After(SettlingTakes, FullScreenChanged);
            });
        }
    }

    void FullScreenChanged()
    {
        changingFullScreen = false;
        Classes.Set("changing", false);
    }

    static void After(int milliseconds, Action action) => DispatcherTimer.RunOnce(action, TimeSpan.FromMilliseconds(milliseconds));

    /// <summary>Without this the window vanishes from the screen for a frame on its way from maximized to full screen.</summary>
    void NoLongerMaximized()
    {
        if (OperatingSystem.IsWindows() && TryGetPlatformHandle() is { } handle)
            SetWindowLongPtr(handle.Handle, GWL_STYLE, GetWindowLongPtr(handle.Handle, GWL_STYLE) & ~WS_MAXIMIZE);
    }

    /// <summary>The window that is no longer maximized forgets where it stood before: it is told again, for the way back.</summary>
    void RememberNormalPlace()
    {
        if (OperatingSystem.IsWindows() && TryGetPlatformHandle() is { } handle)
        {
            normalPlace.Length = Marshal.SizeOf<WindowPlacement>();
            GetWindowPlacement(handle.Handle, ref normalPlace);
        }
    }

    void RestoreNormalPlace()
    {
        if (OperatingSystem.IsWindows() && TryGetPlatformHandle() is { } handle)
        {
            normalPlace.ShowCommand = SW_SHOWMAXIMIZED;
            SetWindowPlacement(handle.Handle, ref normalPlace);
        }
    }

    const int GWL_STYLE = -16, SW_SHOWMAXIMIZED = 3;
    const nint WS_MAXIMIZE = 0x01000000;

    [StructLayout(LayoutKind.Sequential)]
    struct WindowPlacement
    {
        public int Length, Flags, ShowCommand, MinimizedX, MinimizedY, MaximizedX, MaximizedY, Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    static extern nint GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    static extern nint SetWindowLongPtr(IntPtr window, int index, nint value);

    [DllImport("user32.dll")]
    static extern bool GetWindowPlacement(IntPtr window, ref WindowPlacement placement);

    [DllImport("user32.dll")]
    static extern bool SetWindowPlacement(IntPtr window, ref WindowPlacement placement);

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (place.Width > 0)
            Settings.Current.Window = place;
        Settings.Current.Save();
    }
}
