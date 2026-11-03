using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using FreeSlate.Editing;
using FreeSlate.Editor.Writing;

namespace FreeSlate.Editor.Controls;

/// <summary>
/// The lever of a paragraph's level (see Levels): a grip in the margin left of the paragraph the pointer is beside, its level's name
/// before it. Dragged sideways it moves the paragraph a level for every 64 pixels, to the left what weighs more, to the right what is
/// subordinate: the page shows it at once, and a bar of all levels above the paragraph marks where it stands. Let go, that stays, one
/// step to undo; Escape takes it back. Clicked, or invoked otherwise, the bar stays for a level to be chosen. The lever lies over the
/// sheets in their coordinates (it scrolls with them) and takes the pointer only on its grip and its bar.
/// </summary>
public sealed class LevelLever : Canvas
{
    /// <summary>How far the pointer goes for one level, in pixels.</summary>
    const double Step = 64;

    /// <summary>The grip's size: what the pointer hits, 44 pixels each way.</summary>
    const double Hit = 44;

    /// <summary>The gap between the grip and the page's text.</summary>
    const double Gap = 14;

    readonly Button grip = new() { Width = Hit, Height = Hit, IsTabStop = false };
    readonly TextBlock name = new() { FontSize = 11, TextAlignment = TextAlignment.Right, Width = 96, IsHitTestVisible = false };
    readonly StackPanel items = new() { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 1 };
    readonly Border bar;
    readonly DispatcherTimer leaving = new() { Interval = TimeSpan.FromMilliseconds(500) };
    DocumentView? sheets;
    EditSession? editing;

    /// <summary>The paragraph the lever stands at.</summary>
    int? paragraph;

    /// <summary>
    /// A drag of the grip: where it began (the lever's coordinates), the level then and now, whether the paragraph has the level now as a
    /// step of its own (a preview, see Preview), and how far the grip has followed the pointer.
    /// </summary>
    (Point Start, Level From, Level Now, bool Previewed, double By)? drag;

    /// <summary>Whether the release of the pointer ends a drag, so that the button's click after it is none.</summary>
    bool dragged;

    /// <summary>Whether the bar stays after a click, for a level to be chosen.</summary>
    bool choosing;

    public LevelLever()
    {
        bar = new Border { Child = items, IsVisible = false };
        Children.Add(name);
        Children.Add(grip);
        Children.Add(bar);
        foreach (var level in Levels.All)
        {
            var item = new Button { Tag = level, IsTabStop = false };
            item.Click += (_, _) => Choose(level);
            items.Children.Add(item);
        }
        Hide();
        grip.AddHandler(PointerPressedEvent, Pressed, RoutingStrategies.Tunnel);
        grip.AddHandler(PointerMovedEvent, Dragged, RoutingStrategies.Tunnel);
        grip.AddHandler(PointerReleasedEvent, Released, RoutingStrategies.Tunnel);
        // A click without a drag, or the button invoked otherwise (a screen reader, the keyboard): the bar to choose from.
        grip.Click += (_, _) =>
        {
            if (dragged)
                dragged = false;
            else
                OpenBar();
        };
        leaving.Tick += (_, _) =>
        {
            leaving.Stop();
            if (drag == null && !choosing && !grip.IsPointerOver && !bar.IsPointerOver)
                Hide();
        };
    }

    /// <summary>The sheets the lever stands beside and the session that changes their paragraphs' levels.</summary>
    public void Attach(DocumentView view, EditSession session)
    {
        (sheets, editing) = (view, session);
        view.AddHandler(PointerMovedEvent, (_, e) => Beside(e.GetPosition(view)), RoutingStrategies.Bubble, handledEventsToo: true);
        view.AddHandler(PointerExitedEvent, (_, _) => leaving.Start(), RoutingStrategies.Direct, handledEventsToo: true);
        // Writing sends it away, the pointer brings it back.
        view.AddHandler(TextInputEvent, (_, _) =>
        {
            if (drag == null)
                Hide();
        }, RoutingStrategies.Bubble, handledEventsToo: true);
        view.Changed += () =>
        {
            if (grip.IsVisible)
                Place();
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        grip.Theme = this.FindResource("LeverGrip") as ControlTheme;
        bar.Theme = this.FindResource("LevelBar") as ControlTheme;
        name.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("InkFaint"));
        foreach (var item in items.Children.OfType<Button>())
        {
            item.Theme = this.FindResource("LevelItem") as ControlTheme;
            item.Bind(ContentControl.ContentProperty, this.GetResourceObservable(((Level)item.Tag!).NameKey));
        }
    }

    /// <summary>The pointer is at a point of the sheets: the lever goes to the paragraph beside it, or away after a moment.</summary>
    void Beside(Point point)
    {
        if (drag != null || choosing)
            return;
        if (sheets?.ParagraphBeside(point) is not var (at, _, _))
        {
            leaving.Start();
            return;
        }
        leaving.Stop();
        if (paragraph != at || !grip.IsVisible)
        {
            paragraph = at;
            Place();
        }
    }

    /// <summary>The grip and its name beside the paragraph's first line, left of the page's text; the bar above the line where it shows.</summary>
    void Place()
    {
        if (paragraph is not { } at || sheets?.FirstLine(at) is not var (line, textLeft) || editing == null)
        {
            Hide();
            return;
        }
        var level = drag?.Now ?? Levels.Of(editing.Document.ParagraphStyle(at));
        var x = textLeft - Gap - Hit + (drag?.By ?? 0);
        SetLeft(grip, x);
        SetTop(grip, line.Center.Y - Hit / 2);
        name.Text = Levels.Name(level);
        SetLeft(name, x - name.Width + 8);
        SetTop(name, line.Center.Y - 8);
        AutomationProperties.SetName(grip, $"{Text("LevelChoice")}: {Levels.Name(level)}");
        (grip.IsVisible, name.IsVisible) = (true, true);
        if (bar.IsVisible)
        {
            foreach (var item in items.Children.OfType<Button>())
                item.Classes.Set("current", item.Tag == level);
            bar.Measure(Size.Infinity);
            SetLeft(bar, Math.Max(4, textLeft - Gap - Hit / 2 - 28));
            SetTop(bar, line.Top - bar.DesiredSize.Height - 10);
        }
    }

    void Hide() => (grip.IsVisible, name.IsVisible, bar.IsVisible, choosing) = (false, false, false, false);

    void OpenBar()
    {
        choosing = true;
        bar.IsVisible = true;
        Place();
    }

    void Pressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed || paragraph is not { } at || editing == null)
            return;
        var level = Levels.Of(editing.Document.ParagraphStyle(at));
        drag = (e.GetPosition(this), level, level, false, 0);
        dragged = false;
    }

    void Dragged(object? sender, PointerEventArgs e)
    {
        if (drag is not var (start, from, now, previewed, _))
            return;
        var dx = e.GetPosition(this).X - start.X;
        if (!dragged && Math.Abs(dx) < 4)
            return;
        if (!dragged)
        {
            // Now it is a drag: the bar shows all levels.
            dragged = true;
            choosing = false;
            bar.IsVisible = true;
        }
        // A level for every step's width, the first after half of one; the grip follows the pointer as far as there are levels.
        var steps = (int)Math.Round(dx / Step);
        var target = Levels.Beside(from, steps) ?? (steps < 0 ? Levels.All[0] : Levels.All[^1]);
        var by = Math.Clamp(dx, (Levels.All[0].Step - from.Step) * Step, (Levels.All[^1].Step - from.Step) * Step);
        drag = (start, from, now, previewed, by);
        if (target != now)
            Preview(target);
        Place();
        e.Handled = true;
    }

    void Released(object? sender, PointerReleasedEventArgs e)
    {
        if (drag == null)
            return;
        // A drag ends where it is, its preview the step; a click leaves the bar to the button's click.
        drag = null;
        if (dragged)
        {
            bar.IsVisible = false;
            Place();
            sheets?.Focus();
        }
    }

    /// <summary>Escape takes a drag back, the paragraph keeping the level it had, or closes the bar.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && Cancel())
            e.Handled = true;
    }

    /// <summary>Takes back a drag or closes the bar; whether there was one.</summary>
    public bool Cancel()
    {
        if (drag is { } going)
        {
            if (going.Previewed)
                sheets?.Edit(() => editing?.Undo());
            drag = null;
            bar.IsVisible = false;
            Place();
            sheets?.Focus();
            return true;
        }
        if (!choosing)
            return false;
        choosing = false;
        bar.IsVisible = false;
        sheets?.Focus();
        return true;
    }

    /// <summary>The paragraph at another level while the grip is dragged: the preview before taken back, this one a step of its own.</summary>
    void Preview(Level target)
    {
        if (drag is not var (start, from, _, previewed, by) || paragraph is not { } at || editing is not { } session || sheets is not { } view)
            return;
        if (previewed)
            view.Edit(() => session.Undo());
        if (target != from)
            view.Edit(() => session.Handle(() =>
            {
                session.Document.Format(at, new ParagraphFormatting { Style = target.Style });
                return null;
            }));
        drag = (start, from, target, target != from, by);
    }

    /// <summary>A level chosen on the bar after a click: one step to undo.</summary>
    void Choose(Level level)
    {
        if (paragraph is { } at && editing is { } session && sheets is { } view && Levels.Of(session.Document.ParagraphStyle(at)) != level)
            view.Edit(() => session.Handle(() =>
            {
                session.Document.Format(at, new ParagraphFormatting { Style = level.Style });
                return null;
            }));
        choosing = false;
        bar.IsVisible = false;
        Place();
        sheets?.Focus();
    }

    string Text(string key) => this.TryFindResource(key, out var text) && text is string value ? value : key;
}
