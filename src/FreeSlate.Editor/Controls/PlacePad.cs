using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;

namespace FreeSlate.Editor.Controls;

/// <summary>
/// The pad where the place of the text in cells is chosen (see EditSession.PlaceText): under the cells, on the sheets in their coordinates
/// (it scrolls with them), the nine places three by three as a cell shows them, the one the cells have marked, the name of the one pointed
/// at below them. The pointer on one, or the arrows going to it, shows the cells so (a preview); a click on one, or Enter, keeps it; Escape
/// or a press elsewhere closes the pad as it was.
/// </summary>
public sealed class PlacePad : Canvas
{
    /// <summary>How far below the cells the pad stands, and how far it keeps from the sheets' edges.</summary>
    const double Below = 10, Edge = 8;

    /// <summary>The places, row by row as the pad shows them: up and down, then along the lines.</summary>
    static readonly string[] Verticals = ["top", "center", "bottom"], Horizontals = ["left", "center", "right"];

    readonly Grid grid = new() { RowDefinitions = new("Auto,Auto,Auto"), ColumnDefinitions = new("Auto,Auto,Auto") };
    readonly TextBlock name = new() { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) };
    readonly Border card;

    /// <summary>What shows a place in the cells (null: as they are), and what keeps one, while the pad is open.</summary>
    Action<(string Vertical, string Horizontal)?>? preview;
    Action<(string Vertical, string Horizontal)>? keep;

    public PlacePad()
    {
        card = new Border { Child = new StackPanel { Children = { grid, name } }, IsVisible = false };
        Children.Add(card);
        card.AddHandler(KeyDownEvent, Keyed, RoutingStrategies.Tunnel);
        name.FontSize = 11.5;
        // (A color of the theme is found through the theme's variant, as a dynamic resource finds it.)
        name.Bind(TextBlock.ForegroundProperty, name.GetResourceObservable("InkFaint"));
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        card.Theme = this.FindResource("Card") as ControlTheme;
    }

    /// <summary>Whether the pad is open.</summary>
    public bool IsOpen => card.IsVisible;

    /// <summary>The pad closed: the keys go back where the editor wants them.</summary>
    public event Action? Closed;

    /// <summary>The key of a place's name among the texts.</summary>
    public static string NameKey(string vertical, string horizontal) => "CellPlace" + Capital(vertical) + Capital(horizontal);

    static string Capital(string word) => char.ToUpperInvariant(word[0]) + word[1..];

    /// <summary>
    /// Opens the pad under a box of the sheets' coordinates (the cells'), the place the cells have marked; <paramref name="previewed"/>
    /// shows a place in the cells (null: as they are), <paramref name="kept"/> keeps one.
    /// </summary>
    public void Open(Rect box, (string Vertical, string Horizontal) current, Func<string, string, string> icon,
        Action<(string Vertical, string Horizontal)?> previewed, Action<(string Vertical, string Horizontal)> kept)
    {
        (preview, keep) = (previewed, kept);
        grid.Children.Clear();
        Button? own = null;
        for (var r = 0; r < 3; r++)
        {
            for (var c = 0; c < 3; c++)
            {
                var place = (Vertical: Verticals[r], Horizontal: Horizontals[c]);
                var button = new Button { Tag = place, Theme = this.FindResource("LookItem") as ControlTheme, Margin = new Thickness(1) };
                var path = new Avalonia.Controls.Shapes.Path { Data = StreamGeometry.Parse(icon(place.Vertical, place.Horizontal)), Width = 20, Height = 16, Stretch = Stretch.None };
                path[!Shape.FillProperty] = button[!TemplatedControl.ForegroundProperty];
                button.Content = path;
                var key = NameKey(place.Vertical, place.Horizontal);
                button.Bind(Avalonia.Automation.AutomationProperties.NameProperty, this.GetResourceObservable(key));
                if (place == current)
                {
                    button.Classes.Add("current");
                    own = button;
                }
                // The pointer on a place takes the keys there too, so that one place is pointed at.
                button.PointerEntered += (_, _) => button.Focus(NavigationMethod.Pointer);
                button.GotFocus += (_, _) =>
                {
                    name.Text = this.FindResource(key) as string;
                    preview?.Invoke(place);
                };
                button.Click += (_, _) => Close(place);
                Grid.SetRow(button, r);
                Grid.SetColumn(button, c);
                grid.Children.Add(button);
            }
        }
        name.Text = this.FindResource(NameKey(current.Vertical, current.Horizontal)) as string;
        card.IsVisible = true;
        card.Measure(Size.Infinity);
        var size = card.DesiredSize;
        SetLeft(card, Math.Clamp(box.Center.X - size.Width / 2, Edge, Math.Max(Edge, Bounds.Width - size.Width - Edge)));
        SetTop(card, box.Bottom + Below);
        card.BringIntoView();
        // The keys on the cells' own place, which the pad marks (not as a place pointed at).
        (own ?? grid.Children.OfType<Button>().FirstOrDefault())?.Focus(NavigationMethod.Unspecified);
    }

    /// <summary>Closes the pad, a place kept where given, else the cells as they were.</summary>
    public void Close((string Vertical, string Horizontal)? kept = null)
    {
        if (!card.IsVisible)
            return;
        card.IsVisible = false;
        var (shows, keeps) = (preview, keep);
        (preview, keep) = (null, null);
        if (kept is { } place)
            keeps?.Invoke(place);
        else
            shows?.Invoke(null);
        grid.Children.Clear();
        Closed?.Invoke();
    }

    /// <summary>Escape closes the pad as it was; the arrows go from place to place.</summary>
    void Keyed(object? sender, KeyEventArgs e)
    {
        var buttons = grid.Children.OfType<Button>().ToList();
        var at = Math.Max(0, buttons.FindIndex(b => b.IsFocused));
        var (row, column) = (at / 3, at % 3);
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                break;
            case Key.Left or Key.Right or Key.Up or Key.Down when buttons.Count == 9:
                (row, column) = e.Key switch
                {
                    Key.Left => (row, Math.Max(0, column - 1)),
                    Key.Right => (row, Math.Min(2, column + 1)),
                    Key.Up => (Math.Max(0, row - 1), column),
                    _ => (Math.Min(2, row + 1), column),
                };
                buttons[row * 3 + column].Focus(NavigationMethod.Directional);
                break;
            default:
                return;
        }
        e.Handled = true;
    }
}
