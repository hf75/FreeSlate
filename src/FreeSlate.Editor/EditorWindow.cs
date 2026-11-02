using Avalonia;
using Avalonia.Controls;
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

    WindowPlace place = new();

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
        if (WindowState != WindowState.Minimized)
            place.Maximized = WindowState == WindowState.Maximized;
    });

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (place.Width > 0)
            Settings.Current.Window = place;
        Settings.Current.Save();
    }
}
