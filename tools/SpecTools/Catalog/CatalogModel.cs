namespace SpecTools.Catalog;

/// <summary>Link to the section of a specification that describes an entry.</summary>
/// <param name="OfficeNotes">Sections of [MS-OI29500] with Office implementation notes for this section.</param>
public sealed record SpecRef(string Doc, string Section, string Title, int Page, List<string>? OfficeNotes = null);

public sealed record AttributeEntry(
    string Name,
    string Type,
    bool? Required = null,
    string? Default = null,
    List<string>? Values = null,
    List<SpecRef>? Spec = null);

public sealed class ElementEntry
{
    public required string Name { get; init; }

    /// <summary>True for globally declared elements (candidates for part root elements).</summary>
    public bool? Global { get; set; }

    public List<string> Types { get; set; } = [];

    /// <summary>Value type for elements with simple content.</summary>
    public string? Text { get; set; }

    public List<AttributeEntry> Attributes { get; set; } = [];
    public List<string> Children { get; set; } = [];
    public List<string> Parents { get; set; } = [];
    public List<SpecRef> Spec { get; set; } = [];
}

public sealed class ComplexTypeEntry
{
    public required string Name { get; init; }
    public string? Base { get; set; }

    /// <summary>empty, text, elements or mixed.</summary>
    public required string Content { get; init; }

    /// <summary>Content model in a compact DTD-like notation, e.g. "(w:pPr?, (w:r | w:ins)*)".</summary>
    public string? Model { get; set; }

    public List<string> Attributes { get; set; } = [];
    public List<string> UsedBy { get; set; } = [];
    public List<SpecRef>? Spec { get; set; }
}

public sealed class SimpleTypeEntry
{
    public required string Name { get; init; }

    /// <summary>"restriction of xsd:string", "union of …" or "list of …".</summary>
    public required string Definition { get; init; }

    public List<string>? Values { get; set; }
    public Dictionary<string, string>? Facets { get; set; }
    public List<SpecRef>? Spec { get; set; }
}

public sealed record NamespaceCounts(
    int Elements,
    int Attributes,
    int GlobalAttributes,
    int ComplexTypes,
    int SimpleTypes,
    int EnumValues,
    int SpecSections,
    int OfficeNotes);

public sealed class NamespaceEntry
{
    public required string Prefix { get; init; }
    public required string Uri { get; init; }
    public required string Area { get; init; }
    public required bool InScope { get; init; }
    public string? OfficeVersion { get; init; }
    public required string Source { get; init; }
    public NamespaceCounts? Counts { get; set; }
    public List<ElementEntry> Elements { get; set; } = [];
    public List<AttributeEntry> GlobalAttributes { get; set; } = [];
    public List<ComplexTypeEntry> ComplexTypes { get; set; } = [];
    public List<SimpleTypeEntry> SimpleTypes { get; set; } = [];
}
