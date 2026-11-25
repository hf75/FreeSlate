using System.Text.Json;
using FreeSlate.Editing;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// Saving at the user's wish, as Claude does it (a quick way, before the editor has a place of its own to save to): the document saved as a
/// Word file under the name given, in the folder FreeSlate of the user's own folder (a local one: a Documents folder may be a cloud's), a
/// file of that name saved over. Told by the file's name and the folder's alone, never by the whole path, which names the user.
/// </summary>
static class Saving
{
    /// <summary>The folder documents are saved in: FreeSlate in the user's own folder.</summary>
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "FreeSlate");

    /// <summary>The save tool: the document saved by the name in the input (see the class); what was done, and whether it failed.</summary>
    public static (string Text, bool Error) Save(EditSession editing, string json)
    {
        string? given;
        try
        {
            using var parsed = JsonDocument.Parse(json);
            given = parsed.RootElement.ValueKind == JsonValueKind.Object && parsed.RootElement.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
                ? name.GetString() : null;
        }
        catch (JsonException)
        {
            return ("The input of save is a JSON object with the file's name.", true);
        }
        var file = Cleaned(given);
        if (file.Length == 0)
            return ("Give the file a name (\"Project Proposal\").", true);
        var path = Path.Combine(Folder, file + ".docx");
        try
        {
            Directory.CreateDirectory(Folder);
            editing.Save(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Its message would name the whole path: told without it.
            return ($"Not saved: {file}.docx is open in another program, or the folder FreeSlate cannot be written to.", true);
        }
        // The editor's own fonts the document is set in (Inter, the shelf's) embedded, as Word's Embed fonts in the file does, where the
        // machine lacks them: Word shows the document in them too. Saved it is either way.
        var embedded = "";
        try
        {
            if (editing.Document is WordDocument word && FreeSlate.Editor.Writing.EngineFonts.Embeddable(word.FontFamilies()) is { Count: > 0 } faces)
            {
                var copy = WordDocument.Open(path, fonts: FreeSlate.Editor.Writing.EngineFonts.Locator);
                var count = copy.EmbedFonts(faces);
                copy.Save(path);
                embedded = $" With its fonts {string.Join(", ", faces.Select(f => f.Family).Distinct())} embedded ({count} faces), so that Word shows them.";
            }
        }
        catch (Exception e)
        {
            embedded = $" Its fonts could not be embedded ({e.GetType().Name}: {e.Message}).";
        }
        return ($"Saved as {file}.docx in the folder FreeSlate of the user's own folder.{embedded}", false);
    }

    /// <summary>A file's name as given, without .docx and without what a file's name cannot hold; empty where none is left.</summary>
    static string Cleaned(string? given)
    {
        var name = (given ?? "").Trim();
        if (name.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            name = name[..^5];
        var invalid = Path.GetInvalidFileNameChars();
        name = new string([.. name.Select(c => invalid.Contains(c) ? '-' : c)]).Trim(' ', '.', '-');
        return name.Length > 120 ? name[..120].TrimEnd() : name;
    }
}
