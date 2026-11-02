using Avalonia.Controls;
using FreeSlate.Editor.Localization;

namespace FreeSlate.Editor;

/// <summary>What the editor shows: the content of the window, or the whole page in a browser.</summary>
public sealed partial class EditorView : Panel
{
    public EditorView()
    {
        InitializeComponent();

        foreach (var language in UiLanguages.All)
        {
            var choice = new RadioButton { Content = language.Name, GroupName = "language", IsChecked = language == UiLanguages.Current };
            choice.IsCheckedChanged += (_, _) =>
            {
                if (choice.IsChecked == true)
                    UiLanguages.Choose(language);
            };
            Languages.Children.Add(choice);
        }
    }
}
