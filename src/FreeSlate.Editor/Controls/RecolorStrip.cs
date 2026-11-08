using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;

namespace FreeSlate.Editor.Controls;

/// <summary>
/// The strip where a picture's look is chosen (its colors, see Writing.Looks): under the picture, on the sheets in their coordinates (it
/// scrolls with them), the picture small in each look, the one it has marked. The pointer on one, or the keys going to it, shows the
/// picture so (a preview); a click on one, or Enter, keeps it, Escape or a press elsewhere closes the strip as it was. It comes from the
/// picture and goes back to it (see Fader).
/// </summary>
public sealed class RecolorStrip : Canvas
{
    /// <summary>How far below the picture the strip stands, and how far it keeps from the sheets' edges.</summary>
    const double Below = 10, Edge = 8;

    readonly StackPanel row = new() { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 4 };
    readonly Border card;
    readonly Fader fader;

    /// <summary>What shows a look on the picture (null: as it is), and what keeps one, while the strip is open.</summary>
    Action<string?>? preview;
    Action<string>? keep;

    public RecolorStrip()
    {
        card = new Border { Child = row };
        Children.Add(card);
        fader = new Fader(card, new RelativePoint(0.5, 0, RelativeUnit.Relative));
        card.AddHandler(KeyDownEvent, Keyed, RoutingStrategies.Tunnel);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        card.Theme = this.FindResource("Card") as ControlTheme;
    }

    /// <summary>Whether the strip is open.</summary>
    public bool IsOpen => fader.IsShown;

    /// <summary>The strip closed: the keys go back where the editor wants them.</summary>
    public event Action? Closed;

    /// <summary>
    /// Opens the strip under a box of the sheets' coordinates (a picture's), with the looks given (their keys, the keys of their names among
    /// the texts, the picture in each), the picture's own marked; <paramref name="previewed"/> shows a look on the picture (null: as it is),
    /// <paramref name="kept"/> keeps one.
    /// </summary>
    public void Open(Rect box, IReadOnlyList<(string Look, string NameKey, Bitmap? Picture)> looks, string current, Action<string?> previewed, Action<string> kept)
    {
        (preview, keep) = (previewed, kept);
        row.Children.Clear();
        Button? own = null;
        foreach (var (look, nameKey, picture) in looks)
        {
            var name = new TextBlock { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
            name.Bind(TextBlock.TextProperty, this.GetResourceObservable(nameKey));
            var shown = new Image { Source = picture, Width = 84, Height = 56, Stretch = Stretch.Uniform };
            RenderOptions.SetBitmapInterpolationMode(shown, Avalonia.Media.Imaging.BitmapInterpolationMode.HighQuality);
            var button = new Button
            {
                Tag = look,
                Theme = this.FindResource("LookItem") as ControlTheme,
                Content = new StackPanel { Spacing = 5, Children = { shown, name } },
            };
            button.Bind(Avalonia.Automation.AutomationProperties.NameProperty, this.GetResourceObservable(nameKey));
            if (look == current)
            {
                button.Classes.Add("current");
                own = button;
            }
            // The pointer on a look takes the keys there too, so that one look is pointed at.
            button.PointerEntered += (_, _) => button.Focus(NavigationMethod.Pointer);
            button.GotFocus += (_, _) => preview?.Invoke(look);
            button.Click += (_, _) => Close(look);
            row.Children.Add(button);
        }
        fader.Show();
        card.Measure(Size.Infinity);
        var size = card.DesiredSize;
        SetLeft(card, Math.Clamp(box.Center.X - size.Width / 2, Edge, Math.Max(Edge, Bounds.Width - size.Width - Edge)));
        SetTop(card, box.Bottom + Below);
        card.BringIntoView();
        // The keys on the picture's own look, which its ring marks (not as a look pointed at).
        (own ?? row.Children.OfType<Button>().FirstOrDefault())?.Focus(NavigationMethod.Unspecified);
    }

    /// <summary>Closes the strip, a look kept where given, else the picture as it was.</summary>
    public void Close(string? kept = null)
    {
        if (!fader.IsShown)
            return;
        // The looks go with the strip, unless it opens again before.
        fader.Hide(row.Children.Clear);
        var (shows, keeps) = (preview, keep);
        (preview, keep) = (null, null);
        if (kept != null)
            keeps?.Invoke(kept);
        else
            shows?.Invoke(null);
        Closed?.Invoke();
    }

    /// <summary>Escape closes the strip as it was; the arrows go from look to look.</summary>
    void Keyed(object? sender, KeyEventArgs e)
    {
        var buttons = row.Children.OfType<Button>().ToList();
        var at = buttons.FindIndex(b => b.IsFocused);
        if (e.Key == Key.Escape)
            Close();
        else if (e.Key is Key.Left or Key.Right && buttons.Count > 0)
            buttons[Math.Clamp((at < 0 ? 0 : at) + (e.Key == Key.Right ? 1 : -1), 0, buttons.Count - 1)].Focus(NavigationMethod.Directional);
        else
            return;
        e.Handled = true;
    }
}
