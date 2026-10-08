using System.Text.Json;
using System.Text.RegularExpressions;

namespace SpecTools;

/// <summary>
/// Conventional namespace prefixes and the Office version that introduced a namespace.
/// Source: data/namespaces.json of the Open XML SDK (MIT).
/// </summary>
public sealed partial class NamespaceRegistry
{
    public sealed record Entry(string Prefix, string Uri, string? Version);

    readonly Dictionary<string, Entry> byUri = new(StringComparer.Ordinal);
    readonly HashSet<string> usedPrefixes = new(StringComparer.Ordinal);

    /// <summary>
    /// Namespaces the Open XML SDK does not list. Prefixes are taken from the XML examples in the
    /// MS-* documents; c16r2 and mdssi are what Office writes; w16cei is provisional (no example published yet);
    /// s is the prefix ECMA-376 uses; ct and pr are our choice, as package parts use these namespaces as default namespace.
    /// </summary>
    static readonly Entry[] Supplement =
    [
        new("s", "http://schemas.openxmlformats.org/officeDocument/2006/sharedTypes", null),
        new("ct", "http://schemas.openxmlformats.org/package/2006/content-types", null),
        new("pr", "http://schemas.openxmlformats.org/package/2006/relationships", null),
        new("mdssi", "http://schemas.openxmlformats.org/package/2006/digital-signature", null),
        new("p159", "http://schemas.microsoft.com/office/powerpoint/2015/09/main", null),
        new("p1510", "http://schemas.microsoft.com/office/powerpoint/2015/10/main", null),
        new("p166", "http://schemas.microsoft.com/office/powerpoint/2016/6/main", null),
        new("psez", "http://schemas.microsoft.com/office/powerpoint/2016/sectionzoom", null),
        new("pslz", "http://schemas.microsoft.com/office/powerpoint/2016/slidezoom", null),
        new("psuz", "http://schemas.microsoft.com/office/powerpoint/2016/summaryzoom", null),
        new("iact", "http://schemas.microsoft.com/office/powerpoint/2014/inkAction", null),
        new("c16r2", "http://schemas.microsoft.com/office/drawing/2015/06/chart", null),
        new("w16cei", "http://schemas.microsoft.com/office/word/2026/wordml/cei", null),
    ];

    public static NamespaceRegistry Load()
    {
        var registry = new NamespaceRegistry();
        registry.Add(new Entry("xsd", "http://www.w3.org/2001/XMLSchema", null));
        registry.Add(new Entry("xml", "http://www.w3.org/XML/1998/namespace", null));

        var entries = JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(Sources.SdkNamespacesJson)) ?? [];
        foreach (var entry in entries.Where(e => !string.IsNullOrEmpty(e.Uri) && !string.IsNullOrEmpty(e.Prefix)))
            registry.Add(entry);
        foreach (var entry in Supplement)
            registry.Add(entry);
        return registry;
    }

    void Add(Entry entry)
    {
        if (byUri.TryAdd(entry.Uri, entry))
            usedPrefixes.Add(entry.Prefix);
    }

    public bool IsKnown(string uri) => byUri.ContainsKey(uri);

    public string? OfficeVersion(string uri) => byUri.TryGetValue(uri, out var e) ? e.Version : null;

    /// <summary>The conventional prefix, or a generated one for namespaces the Open XML SDK does not know yet.</summary>
    public string Prefix(string uri)
    {
        if (string.IsNullOrEmpty(uri))
            return "";
        if (byUri.TryGetValue(uri, out var entry))
            return entry.Prefix;

        var candidate = "ns-" + NonAlphanumeric().Replace(TrimScheme().Replace(uri, ""), "-").Trim('-').ToLowerInvariant();
        var prefix = candidate;
        for (var i = 2; usedPrefixes.Contains(prefix); i++)
            prefix = candidate + i;
        Add(new Entry(prefix, uri, null));
        return prefix;
    }

    [GeneratedRegex(@"[^A-Za-z0-9]+")]
    private static partial Regex NonAlphanumeric();

    [GeneratedRegex(@"^(https?://schemas\.microsoft\.com/office/|https?://|urn:)")]
    private static partial Regex TrimScheme();
}
