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
            entries["/" + Uri.UnescapeDataString(entry.FullName.Replace('\\', '/'))] = entry;

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

    public bool Exists(string partName) => entries.ContainsKey(partName);

    public byte[] ReadBytes(string partName)
    {
        using var stream = entries[partName].Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    /// <summary>
    /// A part as XML, whitespace kept: a run of spaces (&lt;a:t&gt; &lt;/a:t&gt;) is text, and dropping it joins the words
    /// around it.
    /// </summary>
    public XDocument ReadXml(string partName)
    {
        using var stream = entries[partName].Open();
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
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
