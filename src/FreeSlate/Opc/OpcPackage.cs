using System.IO.Compression;
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

    readonly ZipArchive zip;
    readonly Dictionary<string, ZipArchiveEntry> entries = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, List<Relationship>> relationships = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> defaults = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> overrides = new(StringComparer.OrdinalIgnoreCase);

    public OpcPackage(Stream stream)
    {
        zip = new ZipArchive(stream, ZipArchiveMode.Read);
        foreach (var entry in zip.Entries)
            entries[PartName(entry)] = entry;

        if (entries.TryGetValue("/[Content_Types].xml", out var types))
        {
            using var s = types.Open();
            var doc = XDocument.Load(s);
            foreach (var d in doc.Root!.Elements(XName.Get("Default", ContentTypesNs)))
                defaults[(string?)d.Attribute("Extension") ?? ""] = (string?)d.Attribute("ContentType") ?? "";
            foreach (var o in doc.Root!.Elements(XName.Get("Override", ContentTypesNs)))
                overrides[(string?)o.Attribute("PartName") ?? ""] = (string?)o.Attribute("ContentType") ?? "";
        }
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
        var file = new CompoundFile(File.ReadAllBytes(path));
        if (!OfficeCrypto.IsEncrypted(file))
            throw new InvalidDataException("The file is a compound file without an encrypted package (a binary .ppt?).");
        return new OpcPackage(new MemoryStream(OfficeCrypto.Decrypt(file, password)));
    }

    public bool Exists(string partName) => entries.ContainsKey(partName) || replaced.ContainsKey(partName);

    public byte[] ReadBytes(string partName)
    {
        if (replaced.TryGetValue(partName, out var bytes))
            return bytes;
        using var stream = entries[partName].Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    /// <summary>The parts given new bytes (see Replace), by part name.</summary>
    readonly Dictionary<string, byte[]> replaced = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gives a part new bytes, or adds it: reading and saving take them from now on (the content types are the ones read
    /// when the package was opened).
    /// </summary>
    public void Replace(string partName, byte[] bytes)
    {
        replaced[partName] = bytes;
        if (partName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
            relationships.Clear();
    }

    /// <summary>
    /// Writes the package as a ZIP file of its own (catalog: package.opc): its entries in their order, each part with its
    /// bytes as they were read or as Replace gave them, the parts it added at the end. A password-protected package is
    /// written without its password.
    /// </summary>
    public void Save(Stream output)
    {
        using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            var name = PartName(entry);
            written.Add(name);
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
        using var stream = replaced.TryGetValue(partName, out var bytes) ? new MemoryStream(bytes, false) : entries[partName].Open();
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
        var relsName = partName == "/" ? "/_rels/.rels" : $"{directory}_rels/{partName[(partName.LastIndexOf('/') + 1)..]}.rels";
        if (entries.ContainsKey(relsName))
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
