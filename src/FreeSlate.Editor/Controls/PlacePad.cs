using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Path = Avalonia.Controls.Shapes.Path;

namespace FreeSlate.Editor.Controls;

/// <summary>
/// The pad where the place of the text in cells is chosen, and the way it runs (see EditSession.PlaceText, TurnText): under the cells, on
/// the sheets in their coordinates (it scrolls with them), the nine places three by three as the page shows them, and below them the three
/// ways the text can run: across, up the page, down it, the places drawn running so. The place and the way the cells have are marked, the
/// name of the one pointed at shows below. The pointer on one, or the arrows going to it, shows the cells so (a preview); a click on a
/// place, or Enter, keeps it and closes the pad; one on a way keeps it, the pad staying open, its places then drawn running that way;
/// Escape or a press elsewhere closes the pad as it was (what was kept staying). It comes from the cells and goes back to them (see
/// Fader).
/// </summary>
public sealed class PlacePad : Canvas
{
    /// <summary>How far below the cells the pad stands, and how far it keeps from the sheets' edges.</summary>
    const double Below = 10, Edge = 8;

    /// <summary>The places, row by row as the pad shows them: up and down, then across; the ways below them.</summary>
    static readonly string[] Verticals = ["top", "center", "bottom"], Horizontals = ["left", "center", "right"], Ways = ["across", "btLr", "tbRl"];

    /// <summary>A choice on the pad: a place of the text (Way null), or a way it runs (Place null).</summary>
    public readonly record struct Choice((string Vertical, string Horizontal)? Place, string? Way);

    readonly Grid places = new() { RowDefinitions = new("Auto,Auto,Auto"), ColumnDefinitions = new("Auto,Auto,Auto") };
    readonly Grid ways = new() { ColumnDefinitions = new("Auto,Auto,Auto"), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Margin = new Thickness(0, 7, 0, 0) };
    readonly TextBlock name = new() { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) };
    readonly Border card;
    readonly Fader fader;

    /// <summary>
    /// What shows a choice in the cells (null: as they are), and what keeps one, while the pad is open; the icon of a place running a way;
    /// the way the cells' text runs, and the one the places are drawn running (a way pointed at, else the cells' own).
    /// </summary>
    Action<Choice?>? preview;
    Action<Choice>? keep;
    Func<string, string, string, string>? placeIcon;
    string own = "across", drawn = "across";

    public PlacePad()
    {
        card = new Border { Child = new StackPanel { Children = { places, ways, name } } };
        Children.Add(card);
        fader = new Fader(card, new RelativePoint(0.5, 0, RelativeUnit.Relative));
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
    public bool IsOpen => fader.IsShown;

    /// <summary>The pad closed: the keys go back where the editor wants them.</summary>
    public event Action? Closed;

    /// <summary>The key of a place's name among the texts.</summary>
    public static string NameKey(string vertical, string horizontal) => "CellPlace" + Capital(vertical) + Capital(horizontal);

    /// <summary>The key of a way's name among the texts.</summary>
    public static string WayKey(string way) => "CellWay" + way switch { "btLr" => "Up", "tbRl" => "Down", _ => "Across" };

    static string Capital(string word) => char.ToUpperInvariant(word[0]) + word[1..];

    /// <summary>
    /// Opens the pad under a box of the sheets' coordinates (the cells'), the place where the page shows their text and the way it runs
    /// (across, btLr or tbRl) marked; <paramref name="icon"/> draws a place as the text runs a way, <paramref name="wayIcon"/> a way;
    /// <paramref name="previewed"/> shows a choice in the cells (null: as they are), <paramref name="kept"/> keeps one (after a way the
    /// editor tells the pad where the text then stands, see Turned).
    /// </summary>
    public void Open(Rect box, (string Vertical, string Horizontal) current, string way, Func<string, string, string, string> icon, Func<string, string> wayIcon,
        Action<Choice?> previewed, Action<Choice> kept)
    {
        (preview, keep, placeIcon, own, drawn) = (previewed, kept, icon, way, way);
        places.Children.Clear();
        ways.Children.Clear();
        for (var r = 0; r < 3; r++)
        {
            for (var c = 0; c < 3; c++)
            {
                var place = (Vertical: Verticals[r], Horizontal: Horizontals[c]);
                var button = Made(new Choice(place, null), NameKey(place.Vertical, place.Horizontal), icon(place.Vertical, place.Horizontal, way));
                Grid.SetRow(button, r);
                Grid.SetColumn(button, c);
                places.Children.Add(button);
            }
        }
        for (var k = 0; k < Ways.Length; k++)
        {
            var button = Made(new Choice(null, Ways[k]), WayKey(Ways[k]), wayIcon(Ways[k]));
            Grid.SetColumn(button, k);
            ways.Children.Add(button);
        }
        Mark(current, way);
        name.Text = this.FindResource(NameKey(current.Vertical, current.Horizontal)) as string;
        fader.Show();
        card.Measure(Size.Infinity);
        var size = card.DesiredSize;
        SetLeft(card, Math.Clamp(box.Center.X - size.Width / 2, Edge, Math.Max(Edge, Bounds.Width - size.Width - Edge)));
        SetTop(card, box.Bottom + Below);
        card.BringIntoView();
        // The keys on the cells' own place, which the pad marks (not as a place pointed at).
        (Buttons().FirstOrDefault(b => b.Classes.Contains("current")) ?? Buttons().FirstOrDefault())?.Focus(NavigationMethod.Unspecified);
    }

    /// <summary>A button of a choice, its name read out, its icon drawn in its ink; the pointer on it takes the keys there too, so that one choice is pointed at.</summary>
    Button Made(Choice choice, string key, string icon)
    {
        var button = new Button { Tag = choice, Theme = this.FindResource("LookItem") as ControlTheme, Margin = new Thickness(1) };
        var path = new Path { Data = StreamGeometry.Parse(icon), Width = 20, Height = 16, Stretch = Stretch.None };
        path[!Shape.FillProperty] = button[!TemplatedControl.ForegroundProperty];
        button.Content = path;
        button.Bind(Avalonia.Automation.AutomationProperties.NameProperty, this.GetResourceObservable(key));
        button.PointerEntered += (_, _) => button.Focus(NavigationMethod.Pointer);
        button.GotFocus += (_, _) =>
        {
            name.Text = this.FindResource(key) as string;
            // The places drawn running the way pointed at, as the cells show it; with a place pointed at, running their own way.
            Draw(choice.Way ?? own);
            preview?.Invoke(choice);
        };
        button.Click += (_, _) => Chosen(choice);
        return button;
    }

    /// <summary>The place and the way the cells have, marked.</summary>
    void Mark((string Vertical, string Horizontal) current, string way)
    {
        foreach (var button in Buttons())
            button.Classes.Set("current", button.Tag is Choice { Place: { } place } ? place == current : button.Tag is Choice { Way: { } own } && own == way);
    }

    /// <summary>
    /// The text turned to run a way, kept: the places drawn running so, the one where the page now shows the text marked; the pad stays open,
    /// the keys where they were.
    /// </summary>
    public void Turned((string Vertical, string Horizontal) current, string way)
    {
        if (!fader.IsShown)
            return;
        own = way;
        Draw(way);
        Mark(current, way);
    }

    /// <summary>The places drawn running a way.</summary>
    void Draw(string way)
    {
        if (placeIcon == null || way == drawn)
            return;
        drawn = way;
        foreach (var button in places.Children.OfType<Button>())
        {
            if (button.Tag is Choice { Place: var (vertical, horizontal) } && button.Content is Path path)
                path.Data = StreamGeometry.Parse(placeIcon(vertical, horizontal, way));
        }
    }

    /// <summary>A choice clicked or entered: a place kept closes the pad, a way kept leaves it open.</summary>
    void Chosen(Choice choice)
    {
        if (choice.Place != null)
        {
            Close(choice);
            return;
        }
        keep?.Invoke(choice);
    }

    /// <summary>Closes the pad, a choice kept where given, else the cells as they were (what was kept before staying).</summary>
    public void Close(Choice? kept = null)
    {
        if (!fader.IsShown)
            return;
        // The places and ways go with the pad, unless it opens again before.
        fader.Hide(() =>
        {
            places.Children.Clear();
            ways.Children.Clear();
        });
        var (shows, keeps) = (preview, keep);
        (preview, keep, placeIcon) = (null, null, null);
        if (kept is { } choice)
            keeps?.Invoke(choice);
        else
            shows?.Invoke(null);
        Closed?.Invoke();
    }

    /// <summary>The places row by row, then the ways.</summary>
    List<Button> Buttons() => [.. places.Children.OfType<Button>(), .. ways.Children.OfType<Button>()];

    /// <summary>Escape closes the pad as it was; Enter keeps the choice the keys are on; the arrows go from choice to choice, the ways a row below the places.</summary>
    void Keyed(object? sender, KeyEventArgs e)
    {
        var buttons = Buttons();
        var at = Math.Max(0, buttons.FindIndex(b => b.IsFocused));
        var (row, column) = (at / 3, at % 3);
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                break;
            case Key.Enter when buttons.Count > at && buttons[at].Tag is Choice choice:
                Chosen(choice);
                break;
            case Key.Left or Key.Right or Key.Up or Key.Down when buttons.Count == 12:
                (row, column) = e.Key switch
                {
                    Key.Left => (row, Math.Max(0, column - 1)),
                    Key.Right => (row, Math.Min(2, column + 1)),
                    Key.Up => (Math.Max(0, row - 1), column),
                    _ => (Math.Min(3, row + 1), column),
                };
                buttons[row * 3 + column].Focus(NavigationMethod.Directional);
                break;
            default:
                return;
        }
        e.Handled = true;
    }
}
