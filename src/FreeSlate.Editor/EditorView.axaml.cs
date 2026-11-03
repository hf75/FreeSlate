using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using FreeSlate.Editing;
using FreeSlate.Editor.Localization;
using FreeSlate.Editor.Writing;

namespace FreeSlate.Editor;

/// <summary>What the editor shows: the content of the window, or the whole page in a browser.</summary>
public sealed partial class EditorView : Panel
{
    readonly EditSession editing;
    readonly Dictionary<Tone, RadioButton> tones = [];

    /// <summary>Whether the tone's buttons are being set to the document's, not chosen.</summary>
    bool following;

    public EditorView()
    {
        InitializeComponent();

        foreach (var language in UiLanguages.All)
        {
            var choice = new RadioButton { Content = language.Name, GroupName = "language", IsChecked = language == UiLanguages.Current };
            choice.IsCheckedChanged += (_, _) =>
            {
                if (choice.IsChecked == true)
                {
                    UiLanguages.Choose(language);
                    Sheets.Focus();
                }
            };
            Languages.Children.Add(choice);
        }

        foreach (var tone in Tones.All)
        {
            var choice = new RadioButton { GroupName = "tone" };
            choice.Bind(ContentControl.ContentProperty, this.GetResourceObservable(tone.NameKey));
            choice.IsCheckedChanged += (_, _) =>
            {
                if (choice.IsChecked == true && !following)
                    Take(tone);
            };
            tones[tone] = choice;
            ToneChoice.Children.Add(choice);
        }
        ShowMeanings.IsCheckedChanged += (_, _) =>
        {
            Sheets.ShowsMeanings = ShowMeanings.IsChecked == true;
            Sheets.Focus();
        };
        Sheets.MeaningName = style => Meanings.Of(style) is { } meaning ? Meanings.Name(meaning) : null;
        Sheets.Changed += FollowTone;
        KeyDown += GiveMeaning;
        AddHandler(KeyDownEvent, Structure, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        Sheets.IsEmptyChanged += () => Classes.Set("written", !Sheets.IsEmpty);
        // Before the first frame, so that the sheet comes with the window; and the keys go to it from the start.
        var (styles, numbering) = Tones.All[0].Definitions();
        var document = Environment.GetCommandLineArgs().Contains("--sample")
            ? Sample.Document(Tones.All[0])
            : WordDocument.New("Inter", 10.5, EngineFonts.Locator, styles: styles, numbering: numbering);
        editing = new EditSession(document);
        Sheets.Open(editing);
        Lever.Attach(Sheets, editing);
        Join(Ring);
        FollowTone();
        Loaded += (_, _) =>
        {
            Sheets.Focus();
            // In a window: its bar of full screen, which the line above the sheets comes and goes with there.
            if (this.FindAncestorOfType<Window>()?.GetVisualDescendants().OfType<Controls.CaptionBar>().FirstOrDefault(b => b.ForFullScreen) is { } bar)
            {
                void Follow()
                {
                    Classes.Set("fullScreen", bar.IsVisible);
                    Classes.Set("revealed", bar.IsVisible && !bar.IsResting);
                }
                bar.Changed += Follow;
                Follow();
            }
        };
    }

    /// <summary>Where the pointer was last over the editor, in its own coordinates: where the ring comes for the keyboard.</summary>
    Avalonia.Point? pointer;

    /// <summary>
    /// The gesture ring's fields, sharing the circle from the top clockwise in an order that stays (the hand learns it): the meanings, and
    /// last, up left, taking the meaning away; and its ways to the sheets. Space held on a selection brings it for the keyboard.
    /// </summary>
    void Join(Controls.GestureRing ring)
    {
        // The style a field pointed at gave the selection for a preview, as a step of its own; null where none did.
        string? previewed = null;
        void Undo()
        {
            if (previewed != null)
                Sheets.Edit(() => editing.Undo());
            previewed = null;
            Sheets.SelectionHidden = false;
        }
        // A field does what its preview showed: that preview stays, the step it is; another one is taken back first.
        void Give(string style)
        {
            if (previewed == style)
            {
                previewed = null;
                Sheets.SelectionHidden = false;
                return;
            }
            Undo();
            Sheets.Edit(() => editing.Format(new TextFormatting { Style = style }));
        }
        var styles = new Dictionary<Controls.GestureRing.Slot, string>();
        Controls.GestureRing.Slot Meaning(string style, string sign, bool slanted = false) =>
            Styled(new(Meanings.Of(style)!.NameKey, sign, () => Give(style), Slanted: slanted), style);
        Controls.GestureRing.Slot Styled(Controls.GestureRing.Slot slot, string style)
        {
            styles[slot] = style;
            return slot;
        }
        ring.Slots =
        [
            Meaning("Important", "!"),
            Meaning("Emphasized", "i", slanted: true),
            Meaning("Term", "Aa"),
            Meaning("Quotation", "“"),
            Meaning("OpenQuestion", "?"),
            Styled(new("RingRemove", "×", () => Give("")), ""),
        ];
        ring.Preview = slot =>
        {
            var style = slot != null && styles.TryGetValue(slot, out var given) ? given : null;
            if (style == previewed)
                return;
            Undo();
            if (style == null)
                return;
            // The text as it would read, without the selection over it.
            Sheets.Edit(() => editing.Format(new TextFormatting { Style = style }));
            previewed = style;
            Sheets.SelectionHidden = true;
        };
        Avalonia.Point OnSheets(Avalonia.Point point) => ring.TranslatePoint(point, Sheets) ?? point;
        ring.Window = point => Sheets.SelectionNear(OnSheets(point)) is { } area && Sheets.TranslatePoint(area.TopLeft, ring) is { } corner
            ? new Rect(corner, area.Size)
            : null;
        ring.Target = point => Sheets.OnSelection(OnSheets(point)) || Sheets.SelectWordAt(OnSheets(point));
        ring.OnSelection = point => Sheets.OnSelection(OnSheets(point));
        ring.Press = (point, by, dragging) => Sheets.Press(OnSheets(point), by, KeyModifiers.None, dragging);
        ring.Attach(Sheets);
        AddHandler(PointerMovedEvent, (_, e) =>
        {
            pointer = e.GetPosition(ring);
            ring.Follow(pointer.Value);
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerExitedEvent, (_, _) => pointer = null, Avalonia.Interactivity.RoutingStrategies.Direct, handledEventsToo: true);
        AddHandler(KeyUpEvent, (_, e) =>
        {
            if (e.Key == Key.Space && ring.IsBusy && !ring.SpaceUp())
                Sheets.Edit(() => editing.Type(" "));
            else if (ring.KeyChanged(e.Key, down: false))
                e.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        // While Space waits for the ring or brings it, it types nothing; let go early, it types its space then (see above).
        AddHandler(TextInputEvent, (_, e) =>
        {
            if (ring.IsBusy && e.Text == " ")
                e.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    /// <summary>Gives the document a tone, one step to undo: what is in view fades into its new look.</summary>
    void Take(Tone tone)
    {
        if (Tones.Of(editing.Document) == tone)
            return;
        var (styles, numbering) = tone.Definitions();
        Sheets.Restyle(() => editing.Handle(() =>
        {
            editing.Document.DefineStyles(styles, numbering);
            return null;
        }));
        Sheets.Focus();
    }

    /// <summary>
    /// The structure by the keys, while the sheets have them: Tab moves the paragraphs of the selection, or the cursor's, a level to the
    /// right, Shift+Tab a level to the left (see Levels); Enter in an empty point or subpoint moves it a level to the left instead of
    /// making another one. Enter after a heading or a margin note makes text, as their styles say (w:next).
    /// </summary>
    void Structure(object? sender, KeyEventArgs e)
    {
        // The ring's keys first: the arrows and Escape while it is up, Space held on a selection.
        if (Ring.KeyChanged(e.Key, down: true))
        {
            e.Handled = true;
            return;
        }
        if (!Sheets.IsFocused)
            return;
        if (e.Key == Key.Space && e.KeyModifiers == KeyModifiers.None && editing.Selecting)
        {
            var at = pointer ?? (Sheets.CaretBounds is { Height: > 0 } bar ? Sheets.TranslatePoint(bar.Center, Ring) : null) ?? new Avalonia.Point(Bounds.Width / 2, Bounds.Height / 2);
            if (Ring.SpaceDown(at))
                e.Handled = true;
            return;
        }
        var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        var others = e.KeyModifiers & ~KeyModifiers.Shift;
        if (e.Key == Key.Tab && others == KeyModifiers.None)
        {
            e.Handled = true;
            Shift(shift ? -1 : 1);
        }
        else if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None && !editing.Selecting
            && editing.Document.Text(editing.Caret.Paragraph).Length == 0 && Levels.Of(editing.Document.ParagraphStyle(editing.Caret.Paragraph)).Step is 1 or 2)
        {
            e.Handled = true;
            Shift(-1);
        }
    }

    /// <summary>Moves the paragraphs of the selection, or the cursor's, so many levels, those that can go so far: one step to undo.</summary>
    void Shift(int steps)
    {
        var document = editing.Document;
        var (start, end) = editing.Selecting ? editing.Selection : (editing.Caret, editing.Caret);
        var moved = new List<(int Paragraph, Level To)>();
        for (int? at = start.Paragraph; at is { } paragraph; at = paragraph == end.Paragraph ? null : document.Next(paragraph))
        {
            if (Levels.Beside(Levels.Of(document.ParagraphStyle(paragraph)), steps) is { } to)
                moved.Add((paragraph, to));
        }
        if (moved.Count == 0)
            return;
        Sheets.Edit(() => editing.Handle(() =>
        {
            foreach (var (paragraph, to) in moved)
                document.Format(paragraph, new ParagraphFormatting { Style = to.Style });
            return null;
        }));
    }

    /// <summary>The tone's buttons show the document's tone, also after undoing or doing again.</summary>
    void FollowTone()
    {
        if (Tones.Of(editing.Document) is not { } tone || tones[tone].IsChecked == true)
            return;
        following = true;
        tones[tone].IsChecked = true;
        following = false;
    }

    /// <summary>
    /// Ctrl+1 to Ctrl+5 give the selection, or the word at the cursor, a meaning, Ctrl+0 takes it away: a way to give meanings until the
    /// gesture ring comes.
    /// </summary>
    void GiveMeaning(object? sender, KeyEventArgs e)
    {
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) == 0 || (e.KeyModifiers & (KeyModifiers.Shift | KeyModifiers.Alt)) != 0)
            return;
        var index = e.Key - Key.D0;
        if (e.Key is >= Key.NumPad0 and <= Key.NumPad9)
            index = e.Key - Key.NumPad0;
        if (index < 0 || index > Meanings.All.Count)
            return;
        e.Handled = true;
        var style = index == 0 ? "" : Meanings.All[index - 1].Style;
        Sheets.Edit(() => editing.Format(new TextFormatting { Style = style }));
    }
}
