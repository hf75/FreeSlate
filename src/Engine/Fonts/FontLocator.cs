using System.Buffers.Binary;
using System.Text;

namespace Engine.Fonts;

/// <summary>A font file with the family and style it declares.</summary>
public sealed record FontFace(string Family, bool Bold, bool Italic, string Path, bool System);

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

    public bool IsInstalled(string family) => faces.Value.Any(f => f.System && f.Family.Equals(family, StringComparison.OrdinalIgnoreCase));

    /// <summary>Faces of a family that are not installed but available as files (to be embedded).</summary>
    public IEnumerable<FontFace> Embeddable(string family) =>
        IsInstalled(family) ? [] : faces.Value.Where(f => !f.System && f.Family.Equals(family, StringComparison.OrdinalIgnoreCase));

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
                FontFace? face = null;
                try { face = OpenTypeInfo.Read(file, system); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { }
                if (face != null)
                    yield return face;
            }
        }
    }
}

/// <summary>Reads family name and style from the name and OS/2 tables of a TrueType/OpenType file.</summary>
static class OpenTypeInfo
{
    public static FontFace? Read(string path, bool system)
    {
        var data = File.ReadAllBytes(path);
        var offset = 0;
        if (data.Length > 12 && Encoding.ASCII.GetString(data, 0, 4) == "ttcf")
            offset = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(12));   // first font of the collection

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
        return new FontFace(family, bold, italic, path, system);
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
