using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;

namespace FreeSlate.Editor.Controls;

/// <summary>
/// The card where a picture's description is written (its alternative text, what screen readers read for it), and whether it is
/// decoration only, which they pass over: under the picture, on the sheets in their coordinates (it scrolls with them). Enter keeps
/// what is written (Shift+Enter begins a new line), and so does a click elsewhere (see Close); Escape closes it as it was. It comes from
/// the picture and goes back to it (see Fader).
/// </summary>
public sealed class DescriptionCard : Canvas
{
    /// <summary>How far below the picture the card stands, and how far it keeps from the sheets' edges.</summary>
    const double Below = 10, Edge = 8;

    readonly TextBox text = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Width = 320, MinHeight = 64, MaxHeight = 180 };
    readonly ToggleButton decorative = new() { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
    readonly Border card;
    readonly Fader fader;

    /// <summary>What keeps what is written, while the card is open.</summary>
    Action<string, bool>? keep;

    public DescriptionCard()
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(text);
        panel.Children.Add(decorative);
        card = new Border { Child = panel };
        Children.Add(card);
        fader = new Fader(card, new RelativePoint(0.5, 0, RelativeUnit.Relative));
        text.AddHandler(KeyDownEvent, Keyed, RoutingStrategies.Tunnel);
        decorative.AddHandler(KeyDownEvent, Keyed, RoutingStrategies.Tunnel);
        // Decoration needs no description.
        decorative.IsCheckedChanged += (_, _) => text.IsEnabled = decorative.IsChecked != true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        card.Theme = this.FindResource("Card") as ControlTheme;
        text.Bind(TextBox.PlaceholderTextProperty, this.GetResourceObservable("DescriptionPlaceholder"));
        text.Bind(Avalonia.Automation.AutomationProperties.NameProperty, this.GetResourceObservable("PictureDescribe"));
        decorative.Bind(ContentControl.ContentProperty, this.GetResourceObservable("PictureDecorative"));
    }

    /// <summary>Whether the card is open.</summary>
    public bool IsOpen => fader.IsShown;

    /// <summary>The card closed: the keys go back where the editor wants them.</summary>
    public event Action? Closed;

    /// <summary>
    /// Opens the card under a box of the sheets' coordinates (a picture's), with what the picture has; <paramref name="kept"/> takes
    /// what is written and whether it is decoration only, where it is kept.
    /// </summary>
    public void Open(Rect box, string description, bool isDecorative, Action<string, bool> kept)
    {
        (text.Text, decorative.IsChecked, keep) = (description, isDecorative, kept);
        text.IsEnabled = !isDecorative;
        fader.Show();
        card.Measure(Size.Infinity);
        var size = card.DesiredSize;
        SetLeft(card, Math.Clamp(box.Center.X - size.Width / 2, Edge, Math.Max(Edge, Bounds.Width - size.Width - Edge)));
        SetTop(card, box.Bottom + Below);
        card.BringIntoView();
        if (isDecorative)
            decorative.Focus();
        else
        {
            text.Focus();
            text.CaretIndex = text.Text?.Length ?? 0;
        }
    }

    /// <summary>Closes the card, what is written kept or not.</summary>
    public void Close(bool kept)
    {
        if (!fader.IsShown)
            return;
        fader.Hide();
        var then = keep;
        keep = null;
        if (kept)
            then?.Invoke((text.Text ?? "").Trim(), decorative.IsChecked == true);
        Closed?.Invoke();
    }

    void Keyed(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close(kept: false);
        else if (e.Key == Key.Enter && (e.KeyModifiers & KeyModifiers.Shift) == 0)
            Close(kept: true);
        else
            return;
        e.Handled = true;
    }
}
