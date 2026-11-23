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
/// before it. Dragged sideways it moves the paragraph a level for every 32 pixels, to the left what weighs more, to the right what is
/// subordinate: the page shows it at once, and a bar of all levels above the paragraph marks where it stands. Let go, that stays, one
/// step to undo; Escape takes it back. Clicked, or invoked otherwise, the bar stays for a level to be chosen. Beside a paragraph of the
/// selection it acts on all of the selection's paragraphs (see EditSession.SelectedParagraphs), each then at the level chosen, as Word
/// gives a style to them all. The lever lies over the sheets in their coordinates (it scrolls with them) and takes the pointer only on
/// its grip and its bar. Grip, name and bar come and go as the cards do (see Fader), the grip and its name only fading, the bar growing
/// from the grip's side.
/// </summary>
public sealed class LevelLever : Canvas
{
    /// <summary>How far the pointer goes for one level, in pixels.</summary>
    const double Step = 32;

    /// <summary>The grip's size: what the pointer hits, 44 pixels each way.</summary>
    const double Hit = 44;

    /// <summary>The gap between the grip and the page's text.</summary>
    const double Gap = 14;

    readonly Button grip = new() { Width = Hit, Height = Hit, IsTabStop = false };
    readonly TextBlock name = new() { FontSize = 11, TextAlignment = TextAlignment.Right, Width = 96, IsHitTestVisible = false };
    readonly StackPanel items = new() { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 1 };
    readonly Border bar;
    readonly Fader gripFader, nameFader, barFader;
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
        bar = new Border { Child = items };
        Children.Add(name);
        Children.Add(grip);
        Children.Add(bar);
        gripFader = new Fader(grip, RelativePoint.Center, from: 1);
        nameFader = new Fader(name, RelativePoint.Center, from: 1);
        barFader = new Fader(bar, new RelativePoint(0, 1, RelativeUnit.Relative));
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
            if (gripFader.IsShown)
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
        // A drawing dragged over the sheets is about the page, not a paragraph's level.
        if (sheets?.DraggingDrawing == true)
        {
            Hide();
            return;
        }
        if (sheets?.ParagraphBeside(point) is not var (at, _, _))
        {
            leaving.Start();
            return;
        }
        leaving.Stop();
        if (paragraph != at || !gripFader.IsShown)
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
        gripFader.Show();
        nameFader.Show();
        if (barFader.IsShown)
        {
            foreach (var item in items.Children.OfType<Button>())
                item.Classes.Set("current", item.Tag == level);
            bar.Measure(Size.Infinity);
            SetLeft(bar, Math.Max(4, textLeft - Gap - Hit / 2 - 28));
            SetTop(bar, line.Top - bar.DesiredSize.Height - 10);
        }
    }

    void Hide()
    {
        gripFader.Hide();
        nameFader.Hide();
        barFader.Hide();
        choosing = false;
    }

    void OpenBar()
    {
        choosing = true;
        barFader.Show();
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
            barFader.Show();
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
            barFader.Hide();
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
            barFader.Hide();
            Place();
            sheets?.Focus();
            return true;
        }
        if (!choosing)
            return false;
        choosing = false;
        barFader.Hide();
        sheets?.Focus();
        return true;
    }

    /// <summary>
    /// The paragraphs at another level while the grip is dragged (the grip back where it was, as they were): the preview before taken
    /// back, this one a step of its own.
    /// </summary>
    void Preview(Level target)
    {
        if (drag is not var (start, from, _, previewed, by) || paragraph is not { } at || editing is not { } session || sheets is not { } view)
            return;
        if (previewed)
            view.Edit(() => session.Undo());
        drag = (start, from, target, target != from && Give(session, view, at, target), by);
    }

    /// <summary>A level chosen on the bar after a click: one step to undo.</summary>
    void Choose(Level level)
    {
        if (paragraph is { } at && editing is { } session && sheets is { } view)
            Give(session, view, at, level);
        choosing = false;
        barFader.Hide();
        Place();
        sheets?.Focus();
    }

    /// <summary>
    /// Gives a level to the paragraphs the lever acts on, those not at it already, as one step: the selection's, where the paragraph it
    /// stands at is among them, else that one alone; numbered points in the list beside them, or in a new one (see Lists.Settle). Whether
    /// there was one to change.
    /// </summary>
    static bool Give(EditSession session, DocumentView view, int at, Level level)
    {
        var document = session.Document;
        var acted = session.Selecting && session.SelectedParagraphs is var selected && selected.Contains(at) ? selected : [at];
        var changing = acted.Where(p => Levels.Of(document.ParagraphStyle(p)) != level).ToList();
        if (changing.Count == 0)
            return false;
        view.Edit(() => session.Handle(() =>
        {
            foreach (var paragraph in changing)
                document.Format(paragraph, new ParagraphFormatting { Style = level.Style });
            Lists.Settle(document, changing);
            return null;
        }));
        return true;
    }

    string Text(string key) => this.TryFindResource(key, out var text) && text is string value ? value : key;
}
