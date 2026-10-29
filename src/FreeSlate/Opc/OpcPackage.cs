using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace FreeSlate.Opc;

public sealed record Relationship(string Id, string Type, string Target, bool External)
{
    /// <summary>True if the relationship type ends with the given name, e.g. "slideLayout" (transitional and strict URIs).</summary>
    public bool Is(string typeName) => Type.EndsWith("/" + typeName, StringComparison.Ordinal);
}

/// <summary>
/// A tolerant reader for Open Packaging Conventions packages (ECMA-376 Part 2): parts, content types and
/// relationships of a ZIP container. Part names are absolute ("/ppt/slides/slide1.xml") and case-insensitive.
/// </summary>
public sealed class OpcPackage : IDisposable
{
    const string RelationshipsNs = "http://schemas.openxmlformats.org/package/2006/relationships";
    const string ContentTypesNs = "http://schemas.openxmlformats.org/package/2006/content-types";

    readonly Stream source;
    readonly ZipArchive zip;
    readonly Dictionary<string, ZipArchiveEntry> entries = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, List<Relationship>> relationships = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> defaults = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> overrides = new(StringComparer.OrdinalIgnoreCase);

    public OpcPackage(Stream stream)
    {
        source = stream;
        zip = new ZipArchive(stream, ZipArchiveMode.Read);
        foreach (var entry in zip.Entries)
            entries[PartName(entry)] = entry;

        if (entries.ContainsKey(ContentTypesPart))
            ReadContentTypes();
    }

    /// <summary>The part that names the content types of the others.</summary>
    public const string ContentTypesPart = "/[Content_Types].xml";

    /// <summary>The content types of the parts (by extension, and by part name where they differ), as the package reads now.</summary>
    void ReadContentTypes()
    {
        defaults.Clear();
        overrides.Clear();
        using var s = new MemoryStream(ReadBytes(ContentTypesPart), false);
        var doc = XDocument.Load(s);
        foreach (var d in doc.Root!.Elements(XName.Get("Default", ContentTypesNs)))
            defaults[(string?)d.Attribute("Extension") ?? ""] = (string?)d.Attribute("ContentType") ?? "";
        foreach (var o in doc.Root!.Elements(XName.Get("Override", ContentTypesNs)))
            overrides[(string?)o.Attribute("PartName") ?? ""] = (string?)o.Attribute("ContentType") ?? "";
    }

    /// <summary>
    /// Opens a package file; a password-protected one (a compound file with an encrypted package) is decrypted
    /// first with the password, or with Office's default password when none is given (catalog: package.encryption).
    /// </summary>
    /// <exception cref="PasswordException">The file needs a password, or the password is wrong.</exception>
    public static OpcPackage Open(string path, string? password = null)
    {
        var stream = File.OpenRead(path);
        Span<byte> head = stackalloc byte[8];
        if (stream.Read(head) < 8 || head[0] != 0xD0 || head[1] != 0xCF)
        {
            stream.Position = 0;
            return new OpcPackage(stream);
        }
        stream.Dispose();
        return Decrypted(File.ReadAllBytes(path), password);
    }

    /// <summary>
    /// Opens a package from the bytes of its file (see Open), which it reads from memory: the file can be written while
    /// the package is open.
    /// </summary>
    /// <exception cref="PasswordException">The file needs a password, or the password is wrong.</exception>
    public static OpcPackage Open(byte[] bytes, string? password = null) =>
        bytes is [0xD0, 0xCF, _, _, _, _, _, _, ..] ? Decrypted(bytes, password) : new OpcPackage(new MemoryStream(bytes, writable: false));

    static OpcPackage Decrypted(byte[] bytes, string? password)
    {
        var file = new CompoundFile(bytes);
        if (!OfficeCrypto.IsEncrypted(file))
            throw new InvalidDataException("The file is a compound file without an encrypted package (a binary .ppt?).");
        return new OpcPackage(new MemoryStream(OfficeCrypto.Decrypt(file, password)));
    }

    public bool Exists(string partName) => replaced.ContainsKey(partName) || entries.ContainsKey(partName) && !removed.Contains(partName);

    /// <summary>A part's bytes as ReadBytes gives them, kept where asked: reading the part as XML takes them then (see ReadXml), not unpacking it again.</summary>
    public byte[] ReadBytes(string partName, bool keep)
    {
        var bytes = ReadBytes(partName);
        if (keep)
            kept[partName] = bytes;
        return bytes;
    }

    /// <summary>The bytes of parts kept as they were read (see ReadBytes).</summary>
    readonly Dictionary<string, byte[]> kept = new(StringComparer.OrdinalIgnoreCase);

    public byte[] ReadBytes(string partName)
    {
        if (replaced.TryGetValue(partName, out var bytes))
            return bytes;
        if (removed.Contains(partName))
            throw new KeyNotFoundException($"The part {partName} was taken away.");
        if (kept.TryGetValue(partName, out var read))
            return read;
        return Original(partName) ?? throw new KeyNotFoundException($"The package has no part {partName}.");
    }

    /// <summary>A part's bytes as the package had them when it was opened, null for a part it did not have.</summary>
    public byte[]? Original(string partName)
    {
        if (!entries.TryGetValue(partName, out var entry))
            return null;
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    /// <summary>The names of the package's parts as it reads now.</summary>
    public IEnumerable<string> PartNames => entries.Keys.Where(n => !removed.Contains(n) && !replaced.ContainsKey(n)).Concat(replaced.Keys);

    /// <summary>The parts given new bytes (see Replace), by part name, and those taken away (see Remove).</summary>
    readonly Dictionary<string, byte[]> replaced = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> removed = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gives a part new bytes, or adds it: reading and saving take them from now on; new content types (a part of its
    /// own) and relationships are read from new bytes of their parts.
    /// </summary>
    public void Replace(string partName, byte[] bytes)
    {
        kept.Remove(partName);
        replaced[partName] = bytes;
        removed.Remove(partName);
        Changed(partName);
    }

    /// <summary>Takes a part away: it no longer exists, and saving leaves it out.</summary>
    public void Remove(string partName)
    {
        kept.Remove(partName);
        replaced.Remove(partName);
        if (entries.ContainsKey(partName))
            removed.Add(partName);
        Changed(partName);
    }

    void Changed(string partName)
    {
        if (partName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
            relationships.Clear();
        else if (string.Equals(partName, ContentTypesPart, StringComparison.OrdinalIgnoreCase) && Exists(ContentTypesPart))
            ReadContentTypes();
    }

    /// <summary>
    /// Writes the package as a ZIP file of its own (catalog: package.opc): its entries in their order, each part with its
    /// bytes as they were read or as Replace gave them, the parts it added at the end. Entries it did not change are
    /// copied as they are stored, not unpacked and packed again (a document of large photos saves in the time it takes
    /// to write it: 6.9 s before for 349 MB of JPEG photos); new bytes are deflated. A password-protected package is
    /// written without its password.
    /// </summary>
    public void Save(Stream output)
    {
        var records = ZipStore.Records(source);
        var entryList = zip.Entries;
        var added = replaced.Keys.Count(name => !entries.ContainsKey(name));
        if (records == null || records.Count != entryList.Count || records.Count + added >= 0xFFFF
            || source.Length + replaced.Values.Sum(b => (long)b.Length) + 0x1000000 >= uint.MaxValue
            || records.Zip(entryList).Any(pair => pair.First.Name.All(b => b < 0x80) && Encoding.ASCII.GetString(pair.First.Name) != pair.Second.FullName))
        {
            Repack(output);
            return;
        }
        var writer = new ZipStore.Writer(output);
        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var k = 0; k < records.Count; k++)
        {
            var (entry, record) = (entryList[k], records[k]);
            var name = PartName(entry);
            written.Add(name);
            if (removed.Contains(name))
                continue;
            if (replaced.TryGetValue(name, out var bytes))
                writer.Add(record.Name, record.Flags, record.Time, record.Date, bytes);
            else if (record.Method is 0 or 8)
                writer.Copy(source, record);
            else
            {
                // Packed in a way that is not copied (Deflate64 …): unpacked and deflated.
                using var stream = entry.Open();
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                writer.Add(record.Name, record.Flags, record.Time, record.Date, buffer.ToArray());
            }
        }
        var (time, date) = ZipStore.DosTime(DateTime.Now);
        foreach (var (name, bytes) in replaced)
        {
            if (written.Contains(name))
                continue;
            var (entryName, flags) = ZipStore.Name(Uri.EscapeDataString(name.TrimStart('/')).Replace("%2F", "/"));
            writer.Add(entryName, flags, time, date, bytes);
        }
        writer.Finish();
    }

    /// <summary>Save through ZipArchive, every entry unpacked and deflated again: for packages ZipStore does not copy (ZIP64).</summary>
    void Repack(Stream output)
    {
        using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            var name = PartName(entry);
            written.Add(name);
            if (removed.Contains(name))
                continue;
            using var target = Created(archive, entry.FullName, entry.LastWriteTime);
            if (replaced.TryGetValue(name, out var bytes))
                target.Write(bytes);
            else
            {
                using var source = entry.Open();
                source.CopyTo(target);
            }
        }
        foreach (var (name, bytes) in replaced)
        {
            if (written.Contains(name))
                continue;
            using var target = Created(archive, Uri.EscapeDataString(name.TrimStart('/')).Replace("%2F", "/"), null);
            target.Write(bytes);
        }

        static Stream Created(ZipArchive archive, string name, DateTimeOffset? time)
        {
            var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            // ZIP keeps DOS times, 1980 to 2107.
            if (time is { Year: >= 1980 and < 2107 } at)
                entry.LastWriteTime = at;
            return entry.Open();
        }
    }

    static string PartName(ZipArchiveEntry entry) => "/" + Uri.UnescapeDataString(entry.FullName.Replace('\\', '/'));

    /// <summary>
    /// A part as XML, whitespace kept: a run of spaces (&lt;a:t&gt; &lt;/a:t&gt;) is text, and dropping it joins the words
    /// around it. With <paramref name="lines"/>, each element knows where it was read (line info), which PartText finds
    /// in the part's text to write it back.
    /// </summary>
    public XDocument ReadXml(string partName, bool lines = false)
    {
        using var stream = replaced.TryGetValue(partName, out var bytes) ? new MemoryStream(bytes, false)
            : removed.Contains(partName) ? throw new KeyNotFoundException($"The part {partName} was taken away.")
            : kept.TryGetValue(partName, out var read) ? new MemoryStream(read, false) : entries[partName].Open();
        return Strict.Transition(XDocument.Load(stream, lines ? LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo : LoadOptions.PreserveWhitespace));
    }

    public string ContentType(string partName)
    {
        if (overrides.TryGetValue(partName, out var type))
            return type;
        var extension = Path.GetExtension(partName).TrimStart('.');
        return defaults.TryGetValue(extension, out type) ? type : "application/octet-stream";
    }

    /// <summary>Relationships of a part; "/" for the package relationships.</summary>
    public IReadOnlyList<Relationship> Relationships(string partName)
    {
        if (relationships.TryGetValue(partName, out var cached))
            return cached;

        var list = new List<Relationship>();
        var directory = partName == "/" ? "/" : partName[..(partName.LastIndexOf('/') + 1)];
        var relsName = RelationshipsPart(partName);
        if (Exists(relsName))
        {
            foreach (var r in ReadXml(relsName).Root!.Elements(XName.Get("Relationship", RelationshipsNs)))
            {
                var external = string.Equals((string?)r.Attribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase);
                var target = (string?)r.Attribute("Target") ?? "";
                list.Add(new Relationship(
                    (string?)r.Attribute("Id") ?? "",
                    (string?)r.Attribute("Type") ?? "",
                    external ? target : Resolve(directory, target),
                    external));
            }
        }
        relationships[partName] = list;
        return list;
    }

    /// <summary>The part that holds a part's relationships ("/word/_rels/document.xml.rels"); "/" for the package's.</summary>
    public static string RelationshipsPart(string partName) =>
        partName == "/" ? "/_rels/.rels" : $"{partName[..(partName.LastIndexOf('/') + 1)]}_rels/{partName[(partName.LastIndexOf('/') + 1)..]}.rels";

    public Relationship? Relationship(string partName, string id) =>
        Relationships(partName).FirstOrDefault(r => r.Id == id);

    public Relationship? FirstRelationship(string partName, string typeName) =>
        Relationships(partName).FirstOrDefault(r => r.Is(typeName));

    /// <summary>Resolves a relative target against the directory of the source part.</summary>
    static string Resolve(string directory, string target)
    {
        target = Uri.UnescapeDataString(target.Replace('\\', '/'));
        var segments = new List<string>((target.StartsWith('/') ? "" : directory).Split('/', StringSplitOptions.RemoveEmptyEntries));
        foreach (var segment in target.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == "..") { if (segments.Count > 0) segments.RemoveAt(segments.Count - 1); }
            else if (segment != ".") segments.Add(segment);
        }
        return "/" + string.Join('/', segments);
    }

    public void Dispose() => zip.Dispose();
}
