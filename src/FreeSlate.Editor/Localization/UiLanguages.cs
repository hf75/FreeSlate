using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FreeSlate.Editor.Localization;

/// <summary>A language of the user interface: its code, its name in itself, and its texts.</summary>
public sealed record UiLanguage(string Code, string Name, Func<ResourceDictionary> Texts);

/// <summary>
/// The languages of the user interface. A language is a dictionary of texts, which the controls read as dynamic resources, so
/// that a change shows at once and everywhere. English lies under the others: a text a language does not have yet shows in English.
/// </summary>
public static class UiLanguages
{
    /// <summary>A new language: its dictionary (a copy of English.axaml), a class at the end of this file, a line here.</summary>
    public static readonly IReadOnlyList<UiLanguage> All =
    [
        new("de", "Deutsch", () => new German()),
        new("en", "English", () => new English()),
    ];

    static ResourceDictionary? shown;

    public static UiLanguage? Current { get; private set; }

    /// <summary>The language of the given code, otherwise the system's, otherwise English.</summary>
    public static UiLanguage Best(string? code) =>
        Find(code) ?? Find(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName) ?? Find("en")!;

    static UiLanguage? Find(string? code) => All.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));

    public static void Show(UiLanguage language)
    {
        CheckComplete();
        var dictionaries = Application.Current!.Resources.MergedDictionaries;
        if (shown == null)
            dictionaries.Add(new English());
        else
            dictionaries.Remove(shown);
        dictionaries.Add(shown = language.Texts());
        Current = language;
    }

    /// <summary>What the user chooses: shown now and kept for the next run.</summary>
    public static void Choose(UiLanguage language)
    {
        Show(language);
        Settings.Current.Language = language.Code;
        Settings.Current.Save();
    }

    /// <summary>Every language has every text and no other, or a text is missing somewhere.</summary>
    [Conditional("DEBUG")]
    static void CheckComplete()
    {
        var keys = new English().Keys.ToHashSet();
        foreach (var language in All)
            Debug.Assert(keys.SetEquals(language.Texts().Keys), $"The texts of {language.Name} differ from the English ones.");
    }
}

public sealed partial class German : ResourceDictionary
{
    public German() => AvaloniaXamlLoader.Load(this);
}

public sealed partial class English : ResourceDictionary
{
    public English() => AvaloniaXamlLoader.Load(this);
}
