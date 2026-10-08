namespace SpecTools.Features;

/// <summary>A curated feature catalog for one document type (catalog/features/&lt;document&gt;.yaml).</summary>
public sealed class FeatureDocument
{
    public string Document { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public UniverseSpec Universe { get; set; } = new();
    public List<FeatureArea> Areas { get; set; } = [];
}

/// <summary>Defines which elements can occur in this document type; coverage is measured against it.</summary>
public sealed class UniverseSpec
{
    /// <summary>Part root elements and graphic frame payloads; everything reachable from them is in scope.</summary>
    public List<string> Roots { get; set; } = [];

    /// <summary>Namespaces whose elements are all in scope (extensions live in a:ext, which the schemas leave open).</summary>
    public List<string> Namespaces { get; set; } = [];

    /// <summary>Namespaces the traversal never enters (e.g. WordprocessingML reached through VML text boxes).</summary>
    public List<string> Exclude { get; set; } = [];
}

public sealed class FeatureArea
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public List<Feature> Features { get; set; } = [];
}

public sealed class Feature
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }

    /// <summary>Qualified element names; * and ? are wildcards within the local name.</summary>
    public List<string> Elements { get; set; } = [];

    /// <summary>Namespace prefixes whose elements all belong to this feature.</summary>
    public List<string> Namespaces { get; set; } = [];

    /// <summary>P0 (needed for typical documents) to P3 (rare).</summary>
    public string Priority { get; set; } = "";

    /// <summary>1 (direct mapping) to 5 (research project).</summary>
    public int Difficulty { get; set; }

    /// <summary>todo, partial or done.</summary>
    public string Status { get; set; } = "todo";

    /// <summary>How the feature is rendered in HTML/CSS/SVG/JS.</summary>
    public string? Strategy { get; set; }

    /// <summary>What Office actually does, where it differs from or goes beyond the specification.</summary>
    public string? Office { get; set; }
}
