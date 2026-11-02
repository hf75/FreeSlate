using System.Text.Json;
using System.Text.Json.Serialization;

namespace FreeSlate.Editor;

/// <summary>What the editor keeps between runs: LocalApplicationData/FreeSlate/Editor/settings.json, or what the head sets.</summary>
public sealed class Settings
{
    static Settings? current;

    public static Settings Current => current ??= Load();

    /// <summary>Where the text of the settings lies; the browser's head puts it into the page's storage.</summary>
    public static Func<string?> Read { get; set; } = () => File.ReadAllText(Location);

    public static Action<string> Write { get; set; } = text =>
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Location)!);
        File.WriteAllText(Location, text);
    };

    /// <summary>The language the user chose; without one the editor follows the system.</summary>
    public string? Language { get; set; }

    public WindowPlace? Window { get; set; }

    static string Location => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FreeSlate", "Editor", "settings.json");

    static Settings Load()
    {
        try { return Read() is { } text ? JsonSerializer.Deserialize(text, SettingsJson.Default.Settings) ?? new() : new(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }

    public void Save()
    {
        try { Write(JsonSerializer.Serialize(this, SettingsJson.Default.Settings)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}

/// <summary>Where the window stands when it is neither maximized nor minimized, and whether it is maximized.</summary>
public sealed class WindowPlace
{
    public int X { get; set; }
    public int Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool Maximized { get; set; }
}

/// <summary>How settings are read and written, generated when the editor is built (no reflection at run time: NativeAOT).</summary>
[JsonSerializable(typeof(Settings))]
sealed partial class SettingsJson : JsonSerializerContext;
