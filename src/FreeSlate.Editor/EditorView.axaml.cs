using Avalonia.Controls;
using Avalonia.Input;
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

        Sheets.IsEmptyChanged += () => Classes.Set("written", !Sheets.IsEmpty);
        // Before the first frame, so that the sheet comes with the window; and the keys go to it from the start.
        var document = Environment.GetCommandLineArgs().Contains("--sample")
            ? Sample.Document(Tones.All[0])
            : WordDocument.New("Inter", 10.5, EngineFonts.Locator, styles: Tones.All[0].Styles());
        editing = new EditSession(document);
        Sheets.Open(editing);
        FollowTone();
        Loaded += (_, _) => Sheets.Focus();
    }

    /// <summary>Gives the document a tone, one step to undo: what is in view fades into its new look.</summary>
    void Take(Tone tone)
    {
        if (Tones.Of(editing.Document) == tone)
            return;
        Sheets.Restyle(() => editing.Handle(() =>
        {
            editing.Document.DefineStyles(tone.Styles());
            return null;
        }));
        Sheets.Focus();
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
