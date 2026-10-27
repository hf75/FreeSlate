using System.Buffers.Binary;
using System.Text;

namespace FreeSlate.Fonts;

/// <summary>A font file with the family and style it declares; <see cref="Index"/> is the face's place in a collection (.ttc).</summary>
public sealed record FontFace(string Family, bool Bold, bool Italic, string Path, bool System, int Index = 0)
{
    /// <summary>The family's names in other languages (theme fonts are often given as 游ゴシック, 맑은 고딕 …).</summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];

    /// <summary>The face's full and PostScript names (name IDs 4 and 6), by which browsers find it as a local font.</summary>
    public IReadOnlyList<string> FullNames { get; init; } = [];

    /// <summary>Whether the font names a typographic family of its own (name ID 16), which browsers group it by.</summary>
    public bool Typographic { get; init; }

    /// <summary>
    /// The font of a face that exists in memory only (a font embedded in the presentation, catalog: text.embeddedFonts),
    /// decoded when first needed; null for font files and for embedded fonts that are damaged.
    /// </summary>
    public Lazy<byte[]?>? Embedded { get; init; }

    /// <summary>
    /// The axis values of a named instance of a variable font, which Windows lists as a face of its own (see
    /// OpenTypeInfo.Instances); null for the face a font file declares.
    /// </summary>
    public IReadOnlyList<(string Axis, float Value)>? Variations { get; init; }

    /// <summary>The family the browser finds a named instance's font file by (see OpenTypeInfo.Instances); null for other faces.</summary>
    public string? Base { get; init; }

    public bool Matches(string family) =>
        Family.Equals(family, StringComparison.OrdinalIgnoreCase) || Aliases.Any(a => a.Equals(family, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Finds font files: installed fonts (the browser finds those by name) and Office cloud fonts such as Aptos,
/// which Office downloads to its own cache and which therefore have to be embedded (catalog: engine.fonts); with
/// <see cref="With"/> also the fonts a presentation embeds.
/// </summary>
public sealed class FontLocator
{
    public static FontLocator Default { get; } = new(SystemDirectories(), CloudDirectories());

    readonly Lazy<List<FontFace>> faces;
    readonly bool embeddedFirst;

    public FontLocator(IEnumerable<string> systemDirectories, IEnumerable<string> cloudDirectories)
    {
        var system = systemDirectories.ToList();
        var cloud = cloudDirectories.ToList();
        faces = new Lazy<List<FontFace>>(() => Scan(system, true).Concat(Scan(cloud, false)).ToList());
    }

    FontLocator(Lazy<List<FontFace>> faces, bool embeddedFirst)
    {
        this.faces = faces;
        this.embeddedFirst = embeddedFirst;
    }

    /// <summary>
    /// These fonts and a presentation's embedded ones (faces with <see cref="FontFace.Embedded"/>), which come after
    /// the installed fonts, as in PowerPoint, or before them with <paramref name="embeddedFirst"/> (as on a computer
    /// without those fonts).
    /// </summary>
    public FontLocator With(IReadOnlyCollection<FontFace> embedded, bool embeddedFirst = false) =>
        embedded.Count == 0 && !embeddedFirst ? this : new(new Lazy<List<FontFace>>(() => faces.Value.Concat(embedded).ToList()), embeddedFirst);

    public bool IsInstalled(string family) => faces.Value.Any(f => f.System && f.Matches(family));

    /// <summary>All known faces of a family: installed ones first, then embedded ones, then cloud fonts.</summary>
    public IEnumerable<FontFace> Faces(string family) =>
        faces.Value.Where(f => f.Matches(family)).OrderBy(f => f.Embedded != null ? embeddedFirst ? -1 : 1 : f.System ? 0 : 2);

    /// <summary>
    /// Installed faces of a family that browsers do not find by its name, which the HTML names by their own (catalog:
    /// engine.fonts): fonts without a typographic family whose family name holds a weight or width, which Windows
    /// groups by the rest of the name (measured with Chrome: "Franklin Gothic Medium" drawn in Franklin Gothic Demi,
    /// "Franklin Gothic Demi", "Eras Medium ITC", "OCR A Extended", "Arial Rounded MT Bold" in Times New Roman,
    /// "Gill Sans MT Condensed" italic in Gill Sans MT Italic; each its own face by local()).
    /// </summary>
    public IEnumerable<FontFace> Pinned(string family) =>
        StyleWord.IsMatch(family) && faces.Value.Where(f => f.System && f.Matches(family)).ToList() is { Count: > 0 } found
            && found.All(f => !f.Typographic && f.FullNames.Count > 0) ? found : [];

    static readonly System.Text.RegularExpressions.Regex StyleWord = new(
        @"\b(Thin|Hairline|(Extra|Ultra|Semi|Demi)?Light|Book|Medium|Demi|(Demi|Semi|Extra|Ultra)?Bold|Black|Heavy|Cond|Condensed|Narrow|Extended|Expanded|Compressed)\b",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>
    /// Faces of a family the HTML has to carry: the presentation's embedded fonts, else fonts that are not installed but
    /// available as files.
    /// </summary>
    public IEnumerable<FontFace> Embeddable(string family)
    {
        var embedded = faces.Value.Where(f => f.Embedded != null && f.Matches(family)).ToList();
        if (embedded.Count > 0)
            return embedded;
        return IsInstalled(family) ? [] : faces.Value.Where(f => !f.System && f.Matches(family));
    }

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
    /// <summary>
    /// Every face of a font file: one, or each font of a collection (Cambria Math is the second of cambria.ttc), each with
    /// the named instances of a variable font after it (see Instances).
    /// </summary>
    public static List<FontFace> ReadAll(string path, bool system)
    {
        var data = File.ReadAllBytes(path);
        if (data.Length > 12 && Encoding.ASCII.GetString(data, 0, 4) == "ttcf")
        {
            var count = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(8));
            var faces = new List<FontFace>();
            for (var i = 0; i < count && 12 + i * 4 + 4 <= data.Length; i++)
                faces.AddRange(Read(data, path, system, (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(12 + i * 4)), i));
            return faces;
        }
        return [.. Read(data, path, system, 0, 0)];
    }

    static IEnumerable<FontFace> Read(byte[] data, string path, bool system, int offset, int index)
    {
        var tables = new Dictionary<string, int>();
        var count = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 4));
        for (var i = 0; i < count; i++)
        {
            var record = offset + 12 + i * 16;
            tables[Encoding.ASCII.GetString(data, record, 4)] = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(record + 8));
        }
        if (!tables.TryGetValue("name", out var name))
            return [];

        var family = NameString(data, name, 1);
        if (string.IsNullOrEmpty(family))
            return [];

        bool bold = false, italic = false;
        if (tables.TryGetValue("OS/2", out var os2))
        {
            var selection = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(os2 + 62));
            italic = (selection & 0x01) != 0;
            bold = (selection & 0x20) != 0;
        }
        var aliases = NameStrings(data, name, 1).Where(n => n.Length > 0 && !n.Equals(family, StringComparison.OrdinalIgnoreCase)).Distinct().ToList();
        var full = new[] { NameString(data, name, 4), NameString(data, name, 6) }.OfType<string>().Where(n => n.Length > 0).Distinct().ToList();
        var face = new FontFace(family, bold, italic, path, system, index)
        {
            Aliases = aliases, FullNames = full, Typographic = !string.IsNullOrEmpty(NameString(data, name, 16)),
        };
        return [face, .. Instances(data, tables, face)];
    }

    /// <summary>
    /// The named instances of a variable font (fvar) as Windows lists them, each a family of its own (catalog:
    /// engine.fonts; Word's font list, Application.FontNames): the typographic family with the names the STAT table gives
    /// the instance's values, axis by axis in its order, the names it marks elidable left out and Bold and Italic taken
    /// as the style; without a STAT table the words of the instance's own name ("Bahnschrift Light", "Bahnschrift
    /// SemiBold Condensed", "Bahnschrift Condensed" bold, "Segoe UI Variable Small Light", "Segoe UI Variable Text" for
    /// its regular instance, "Sitka Small"). Names are cut to 31 characters as GDI's face names are ("Bahnschrift
    /// SemiLight SemiConde"). Instances that name the file's own family are its face already (its regular, and its bold
    /// as the weight axis at 700). The browser finds an instance's file by the typographic family with the names of its
    /// values on the axes other than weight, width and slant ("Bahnschrift", "Segoe UI Variable Small").
    /// </summary>
    static IEnumerable<FontFace> Instances(byte[] data, Dictionary<string, int> tables, FontFace face)
    {
        if (!tables.TryGetValue("fvar", out var fvar) || !tables.TryGetValue("name", out var name) || fvar + 16 > data.Length)
            yield break;
        int U16(int at) => at + 2 <= data.Length ? BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(at)) : 0;
        float Fixed(int at) => at + 4 <= data.Length ? BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(at)) / 65536f : 0;
        var (axesOffset, axisCount, axisSize, instanceCount, instanceSize) = (U16(fvar + 4), U16(fvar + 8), U16(fvar + 10), U16(fvar + 12), U16(fvar + 14));
        if (axisSize < 20 || instanceSize < 4 + 4 * axisCount || fvar + axesOffset + axisCount * axisSize > data.Length)
            yield break;
        var tags = new string[axisCount];
        for (var a = 0; a < axisCount; a++)
            tags[a] = Encoding.ASCII.GetString(data, fvar + axesOffset + a * axisSize, 4);
        var stat = tables.TryGetValue("STAT", out var statOffset) ? Stat(data, statOffset, name) : null;
        var typographic = NameString(data, name, 16) is { Length: > 0 } t ? t : face.Family;
        var seen = new HashSet<(string, bool, bool)>();
        for (var i = 0; i < instanceCount; i++)
        {
            var record = fvar + axesOffset + axisCount * axisSize + i * instanceSize;
            if (record + 4 + 4 * axisCount > data.Length)
                yield break;
            var coordinates = new (string Axis, float Value)[axisCount];
            for (var a = 0; a < axisCount; a++)
                coordinates[a] = (tags[a], Fixed(record + 4 + 4 * a));
            var values = stat != null
                ? stat.SelectMany(axis => coordinates.Where(c => c.Axis == axis.Tag).Take(1).Select(c => (axis.Tag, Name: axis.Name(c.Value)))).Where(v => v.Name != null).ToList()
                : (NameString(data, name, U16(record)) ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => (Tag: "", Name: (string?)w)).ToList();
            var bold = values.Any(v => v.Name == "Bold");
            var italic = values.Any(v => v.Name is "Italic" or "Oblique");
            var named = values.Where(v => v.Name is not ("Regular" or "Bold" or "Italic" or "Oblique")).ToList();
            var whole = string.Join(' ', [typographic, .. named.Select(v => v.Name)]);
            var family = whole.Length > 31 ? whole[..31] : whole;
            if (family.Equals(face.Family, StringComparison.OrdinalIgnoreCase) || !seen.Add((family.ToUpperInvariant(), bold, italic)))
                continue;
            var css = string.Join(' ', [typographic, .. named.Where(v => v.Tag is not ("" or "wght" or "wdth" or "slnt" or "ital")).Select(v => v.Name)]);
            yield return new FontFace(family, bold, italic, face.Path, face.System, face.Index)
            {
                Aliases = whole != family ? [whole] : [], Typographic = true, Variations = coordinates, Base = css,
            };
        }
    }

    /// <summary>A design axis of the STAT table and the names of its values: exact values, then ranges; null for an elidable one.</summary>
    sealed record StatAxis(string Tag, int Ordering, List<(float Min, float Max, bool Range, string? Name)> Values)
    {
        public string? Name(float value) =>
            Values.Where(v => !v.Range).Concat(Values.Where(v => v.Range)).FirstOrDefault(v => v.Min - 0.001f <= value && value <= v.Max + 0.001f).Name;
    }

    /// <summary>The STAT table's design axes in their order with their values (formats 1 to 3; format 4 combinations are left out).</summary>
    static List<StatAxis>? Stat(byte[] data, int stat, int name)
    {
        int U16(int at) => at + 2 <= data.Length ? BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(at)) : 0;
        int U32(int at) => at + 4 <= data.Length ? (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(at)) : 0;
        float Fixed(int at) => at + 4 <= data.Length ? BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(at)) / 65536f : 0;
        var (axisSize, axisCount, axesOffset, valueCount, valuesOffset) = (U16(stat + 4), U16(stat + 6), U32(stat + 8), U16(stat + 12), U32(stat + 14));
        if (axisSize < 8 || stat + axesOffset + axisCount * axisSize > data.Length)
            return null;
        var axes = new List<StatAxis>();
        for (var a = 0; a < axisCount; a++)
        {
            var record = stat + axesOffset + a * axisSize;
            axes.Add(new StatAxis(Encoding.ASCII.GetString(data, record, 4), U16(record + 6), []));
        }
        for (var v = 0; v < valueCount; v++)
        {
            var value = stat + valuesOffset + U16(stat + valuesOffset + 2 * v);
            var (format, axis, flags, nameId) = (U16(value), U16(value + 2), U16(value + 4), U16(value + 6));
            if (format is < 1 or > 3 || axis >= axes.Count)
                continue;
            // Flag 2: the name may be left out (ELIDABLE_AXIS_VALUE_NAME), as the regular weight's.
            var text = (flags & 2) != 0 ? null : NameString(data, name, nameId);
            axes[axis].Values.Add(format == 2 ? (Fixed(value + 12), Fixed(value + 16), true, text) : (Fixed(value + 8), Fixed(value + 8), false, text));
        }
        return [.. axes.OrderBy(a => a.Ordering)];
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
