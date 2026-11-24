using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace FreeSlate.Docx.Citations;

/// <summary>A person among a source's contributors (b:Person): the last name, the first and middle names as Word keeps them.</summary>
public sealed record Person(string Last, string? First = null, string? Middle = null);

/// <summary>A contributor role's names (b:Author, b:Editor, b:Translator …): people in order, or a corporate name (b:Corporate).</summary>
public sealed record Contributors(IReadOnlyList<Person> People, string? Corporate = null)
{
    /// <summary>Whether it names no one.</summary>
    public bool IsEmpty => People.Count == 0 && string.IsNullOrEmpty(Corporate);
}

/// <summary>
/// A source of Word's bibliography (b:Source, catalog: fields.citation): its tag (what citations name it by), its kind (Book,
/// JournalArticle, InternetSite … Word's 17), its contributors by role (Author, Editor, Translator, BookAuthor, Director …) and its
/// other fields by Word's names (Title, Year, City, Publisher, JournalName, Volume, Issue, Pages, URL, YearAccessed …) in order.
/// </summary>
public sealed class Source(string tag, string type, IReadOnlyDictionary<string, Contributors> roles, IReadOnlyList<KeyValuePair<string, string>> fields)
{
    public string Tag { get; } = tag;
    public string Type { get; } = type;
    public IReadOnlyDictionary<string, Contributors> Roles { get; } = roles;
    public IReadOnlyList<KeyValuePair<string, string>> Fields { get; } = fields;

    /// <summary>A field's value (Title, Year …), null where the source has none or an empty one.</summary>
    public string? this[string name] => Fields.FirstOrDefault(f => f.Key == name).Value is { Length: > 0 } value ? value : null;

    /// <summary>A role's contributors (Author, Editor …), null where the source names none.</summary>
    public Contributors? Role(string role) => Roles.GetValueOrDefault(role) is { IsEmpty: false } names ? names : null;

    public override string ToString() => $"{Tag} ({Type})";
}

/// <summary>
/// The sources of a document as Word keeps them in a custom XML part (b:Sources, its schema the bibliography namespace): the sources in
/// order and the citation style Word formats citations and the bibliography in (SelectedStyle, the style's file, "\APA…xsl"; StyleName;
/// Version). Read and written back alike: a source's fields in their order, unknown ones kept.
/// </summary>
public sealed record SourceList(IReadOnlyList<Source> Sources, string? SelectedStyle = null, string? StyleName = null, string? Version = null)
{
    /// <summary>The bibliography's namespace, the custom XML part's schema.</summary>
    public const string Namespace = "http://schemas.openxmlformats.org/officeDocument/2006/bibliography";

    static readonly XNamespace B = Namespace;

    /// <summary>A source by its tag, as Word finds it (letters' case aside); null where there is none.</summary>
    public Source? Find(string tag) => Sources.FirstOrDefault(s => s.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase));

    /// <summary>The sources of a b:Sources document; null where it is none.</summary>
    public static SourceList? Read(XElement root)
    {
        if (root.Name != B + "Sources")
            return null;
        var sources = new List<Source>();
        foreach (var element in root.Elements(B + "Source"))
        {
            var tag = (string?)element.Element(B + "Tag") ?? "";
            var type = (string?)element.Element(B + "SourceType") ?? "Misc";
            var roles = new Dictionary<string, Contributors>(StringComparer.Ordinal);
            foreach (var role in element.Element(B + "Author")?.Elements() ?? [])
            {
                var people = role.Element(B + "NameList")?.Elements(B + "Person").Select(p => new Person(
                    ((string?)p.Element(B + "Last") ?? "").Trim(), Text(p.Element(B + "First")), Text(p.Element(B + "Middle")))).ToList() ?? [];
                roles[role.Name.LocalName] = new Contributors(people, Text(role.Element(B + "Corporate")));
            }
            var fields = element.Elements().Where(e => e.Name.Namespace == B && e.Name.LocalName is not ("Tag" or "SourceType" or "Author") && !e.HasElements)
                .Select(e => new KeyValuePair<string, string>(e.Name.LocalName, e.Value)).ToList();
            sources.Add(new Source(tag, type, roles, fields));
        }
        return new SourceList(sources, (string?)root.Attribute("SelectedStyle"), (string?)root.Attribute("StyleName"), (string?)root.Attribute("Version"));
    }

    static string? Text(XElement? element) => element?.Value.Trim() is { Length: > 0 } text ? text : null;

    /// <summary>The sources of a custom XML part's text; null where it is not Word's sources.</summary>
    public static SourceList? Read(string xml)
    {
        try
        {
            return Read(XElement.Parse(xml.TrimStart('﻿')));
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    /// <summary>
    /// The b:Sources document as Word writes it: the style's attributes, each source's tag, kind, contributors and fields in their order,
    /// its order among those cited (RefOrder, from <paramref name="order"/> where given: numbered styles go by it).
    /// </summary>
    public string Write(IReadOnlyList<string>? order = null)
    {
        static string Escaped(string text) => System.Security.SecurityElement.Escape(text) ?? "";
        var xml = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        xml.Append($"<b:Sources xmlns:b=\"{Namespace}\" xmlns=\"{Namespace}\"");
        foreach (var (name, value) in new[] { ("SelectedStyle", SelectedStyle), ("StyleName", StyleName), ("Version", Version) })
        {
            if (value != null)
                xml.Append($" {name}=\"{Escaped(value)}\"");
        }
        xml.Append('>');
        foreach (var source in Sources)
        {
            xml.Append($"<b:Source><b:Tag>{Escaped(source.Tag)}</b:Tag><b:SourceType>{Escaped(source.Type)}</b:SourceType>");
            if (source["Guid"] is { } guid)
                xml.Append($"<b:Guid>{Escaped(guid)}</b:Guid>");
            if (source.Roles.Values.Any(r => !r.IsEmpty))
            {
                xml.Append("<b:Author>");
                foreach (var (role, names) in source.Roles.Where(r => !r.Value.IsEmpty))
                {
                    xml.Append($"<b:{role}>");
                    if (names.Corporate != null)
                        xml.Append($"<b:Corporate>{Escaped(names.Corporate)}</b:Corporate>");
                    if (names.People.Count > 0)
                    {
                        xml.Append("<b:NameList>");
                        foreach (var person in names.People)
                        {
                            xml.Append($"<b:Person><b:Last>{Escaped(person.Last)}</b:Last>");
                            if (person.First != null)
                                xml.Append($"<b:First>{Escaped(person.First)}</b:First>");
                            if (person.Middle != null)
                                xml.Append($"<b:Middle>{Escaped(person.Middle)}</b:Middle>");
                            xml.Append("</b:Person>");
                        }
                        xml.Append("</b:NameList>");
                    }
                    xml.Append($"</b:{role}>");
                }
                xml.Append("</b:Author>");
            }
            foreach (var (name, value) in source.Fields.Where(f => f.Key is not ("Guid" or "RefOrder")))
                xml.Append($"<b:{name}>{Escaped(value)}</b:{name}>");
            var rank = order?.Select((t, i) => (t, i)).FirstOrDefault(x => x.t.Equals(source.Tag, StringComparison.OrdinalIgnoreCase)) is { t: not null } found ? found.i + 1 : (int?)null;
            if ((rank?.ToString(CultureInfo.InvariantCulture) ?? source["RefOrder"]) is { } refOrder)
                xml.Append($"<b:RefOrder>{refOrder}</b:RefOrder>");
            xml.Append("</b:Source>");
        }
        return xml.Append("</b:Sources>").ToString();
    }
}
