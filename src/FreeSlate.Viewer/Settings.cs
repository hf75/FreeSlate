using System.Text.Json;
using System.Text.Json.Serialization;

namespace FreeSlate.Viewer;

/// <summary>What the viewer keeps between runs (LocalApplicationData/FreeSlate/Viewer/settings.json).</summary>
sealed class Settings
{
    public int Zoom { get; set; }
    public int Compare { get; set; }
    public double Blend { get; set; } = 50;
    public bool Markup { get; set; }
    /// <summary>Whether the document shows as HTML in the web view, not drawn by Skia.</summary>
    public bool Browser { get; set; }
    public bool Watch { get; set; } = true;
    public string? Folder { get; set; }

    static string Location => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FreeSlate", "Viewer", "settings.json");

    public static Settings Load()
    {
        try { return JsonSerializer.Deserialize(File.ReadAllText(Location), SettingsJson.Default.Settings) ?? new(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Location)!);
            File.WriteAllText(Location, JsonSerializer.Serialize(this, SettingsJson.Default.Settings));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}

/// <summary>How settings are read and written, generated when the viewer is built (no reflection at run time: NativeAOT).</summary>
[JsonSerializable(typeof(Settings))]
sealed partial class SettingsJson : JsonSerializerContext;
