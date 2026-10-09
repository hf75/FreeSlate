using System.Buffers.Binary;
using System.Text;

namespace FreeSlate.Fonts;

/// <summary>A font file with the family and style it declares; <see cref="Index"/> is the face's place in a collection (.ttc).</summary>
public sealed record FontFace(string Family, bool Bold, bool Italic, string Path, bool System, int Index = 0)
{
    /// <summary>The family's names in other languages (theme fonts are often given as 游ゴシック, 맑은 고딕 …).</summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];

    public bool Matches(string family) =>
        Family.Equals(family, StringComparison.OrdinalIgnoreCase) || Aliases.Any(a => a.Equals(family, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Finds font files: installed fonts (the browser finds those by name) and Office cloud fonts such as Aptos,
/// which Office downloads to its own cache and which therefore have to be embedded (catalog: engine.fonts).
/// </summary>
public sealed class FontLocator
{
    public static FontLocator Default { get; } = new(SystemDirectories(), CloudDirectories());

    readonly Lazy<List<FontFace>> faces;

    public FontLocator(IEnumerable<string> systemDirectories, IEnumerable<string> cloudDirectories)
    {
        var system = systemDirectories.ToList();
        var cloud = cloudDirectories.ToList();
        faces = new Lazy<List<FontFace>>(() => Scan(system, true).Concat(Scan(cloud, false)).ToList());
    }

    public bool IsInstalled(string family) => faces.Value.Any(f => f.System && f.Matches(family));

    /// <summary>All known faces of a family, installed ones first.</summary>
    public IEnumerable<FontFace> Faces(string family) =>
        faces.Value.Where(f => f.Matches(family)).OrderBy(f => f.System ? 0 : 1);

    /// <summary>Faces of a family that are not installed but available as files (to be embedded).</summary>
    public IEnumerable<FontFace> Embeddable(string family) =>
        IsInstalled(family) ? [] : faces.Value.Where(f => !f.System && f.Matches(family));

    static IEnumerable<string> SystemDirectories()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts");
        }
        else if (OperatingSystem.IsMacOS())
        {
            yield return "/Library/Fonts";
            yield return "/System/Library/Fonts";
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Fonts");
        }
        else
        {
            yield return "/usr/share/fonts";
            yield return "/usr/local/share/fonts";
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".fonts");
        }
    }

    static IEnumerable<string> CloudDirectories()
    {
        if (OperatingSystem.IsWindows())
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "FontCache", "4", "CloudFonts");
    }

    static IEnumerable<FontFace> Scan(List<string> directories, bool system)
    {
        foreach (var directory in directories.Where(Directory.Exists))
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
            {
                var extension = Path.GetExtension(file).ToLowerInvariant();
                if (extension is not (".ttf" or ".otf" or ".ttc"))
                    continue;
                List<FontFace> found = [];
                try { found = OpenTypeInfo.ReadAll(file, system); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { }
                foreach (var face in found)
                    yield return face;
            }
        }
    }
}

/// <summary>Reads family name and style from the name and OS/2 tables of a TrueType/OpenType file.</summary>
static class OpenTypeInfo
{
    /// <summary>Every face of a font file: one, or each font of a collection (Cambria Math is the second of cambria.ttc).</summary>
    public static List<FontFace> ReadAll(string path, bool system)
    {
        var data = File.ReadAllBytes(path);
        if (data.Length > 12 && Encoding.ASCII.GetString(data, 0, 4) == "ttcf")
        {
            var count = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(8));
            var faces = new List<FontFace>();
            for (var i = 0; i < count && 12 + i * 4 + 4 <= data.Length; i++)
            {
                if (Read(data, path, system, (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(12 + i * 4)), i) is { } face)
                    faces.Add(face);
            }
            return faces;
        }
        return Read(data, path, system, 0, 0) is { } single ? [single] : [];
    }

    static FontFace? Read(byte[] data, string path, bool system, int offset, int index)
    {
        var tables = new Dictionary<string, int>();
        var count = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 4));
        for (var i = 0; i < count; i++)
        {
            var record = offset + 12 + i * 16;
            tables[Encoding.ASCII.GetString(data, record, 4)] = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(record + 8));
        }
        if (!tables.TryGetValue("name", out var name))
            return null;

        var family = NameString(data, name, 1);
        if (string.IsNullOrEmpty(family))
            return null;

        bool bold = false, italic = false;
        if (tables.TryGetValue("OS/2", out var os2))
        {
            var selection = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(os2 + 62));
            italic = (selection & 0x01) != 0;
            bold = (selection & 0x20) != 0;
        }
        var aliases = NameStrings(data, name, 1).Where(n => n.Length > 0 && !n.Equals(family, StringComparison.OrdinalIgnoreCase)).Distinct().ToList();
        return new FontFace(family, bold, italic, path, system, index) { Aliases = aliases };
    }

    /// <summary>All values of a name record, in every language.</summary>
    static IEnumerable<string> NameStrings(byte[] data, int table, int nameId)
    {
        var count = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(table + 2));
        var storage = table + BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(table + 4));
        for (var i = 0; i < count; i++)
        {
            var r = table + 6 + i * 12;
            var platform = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(r));
            var id = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(r + 6));
            var length = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(r + 8));
            var start = storage + BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(r + 10));
            if (id != nameId || start + length > data.Length || platform is not (0 or 3))
                continue;
            yield return Encoding.BigEndianUnicode.GetString(data, start, length);
        }
    }

    /// <summary>A name record, preferring Windows/Unicode/English (US).</summary>
    static string? NameString(byte[] data, int table, int nameId)
    {
        var count = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(table + 2));
        var storage = table + BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(table + 4));
        string? fallback = null;
        for (var i = 0; i < count; i++)
        {
            var r = table + 6 + i * 12;
            var platform = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(r));
            var language = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(r + 4));
            var id = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(r + 6));
            var length = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(r + 8));
            var start = storage + BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(r + 10));
            if (id != nameId || start + length > data.Length)
                continue;
            var value = platform is 0 or 3 ? Encoding.BigEndianUnicode.GetString(data, start, length) : Encoding.Latin1.GetString(data, start, length);
            if (platform == 3 && language == 0x409)
                return value;
            fallback ??= value;
        }
        return fallback;
    }
}
