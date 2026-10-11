using System.Xml.Linq;

namespace FreeSlate.Docx;

/// <summary>A style of the styles part: its type, the style it is based on and the formatting it states.</summary>
public sealed class StyleDefinition
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public string? Name { get; init; }
    public string? BasedOn { get; init; }
    public string? Next { get; init; }
    public bool Default { get; init; }
    public XElement? ParagraphProperties { get; init; }
    public XElement? RunProperties { get; init; }
    public XElement? TableProperties { get; init; }
    public XElement Element { get; init; } = new("style");
}

/// <summary>
/// The styles part (catalog: style.definitions, style.defaults): document defaults and styles, and the resolution of
/// paragraph and character formatting through the style hierarchy (ISO/IEC 29500 17.7.2): document defaults, table
/// style, numbering, paragraph style, character style, direct formatting, each style along its basedOn chain.
/// </summary>
public sealed class StyleSheet
{
    readonly Dictionary<string, StyleDefinition> styles = new(StringComparer.Ordinal);

    public RunFormat DefaultRun { get; } = new();
    public ParagraphFormat DefaultParagraph { get; } = new();

    /// <summary>The default paragraph style (w:default="1"), applied where a paragraph names none.</summary>
    public string? DefaultParagraphStyle { get; }

    /// <summary>The default character style ("Default Paragraph Font").</summary>
    public string? DefaultCharacterStyle { get; }

    public StyleSheet(XDocument? document)
    {
        var root = document?.Root;
        if (root == null)
            return;
        var defaults = root.Child("docDefaults");
        DefaultRun.Apply(defaults.Child("rPrDefault").Child("rPr"));
        DefaultParagraph.Apply(defaults.Child("pPrDefault").Child("pPr"));
        foreach (var e in root.Elements(Wml.W + "style"))
        {
            var id = e.Attr("styleId");
            if (id == null)
                continue;
            var style = new StyleDefinition
            {
                Id = id,
                Type = e.Attr("type") ?? "paragraph",
                Name = e.Child("name").Val(),
                BasedOn = e.Child("basedOn").Val(),
                Next = e.Child("next").Val(),
                Default = e.Attr("default") is "1" or "true" or "on",
                ParagraphProperties = e.Child("pPr"),
                RunProperties = e.Child("rPr"),
                TableProperties = e.Child("tblPr"),
                Element = e,
            };
            styles.TryAdd(id, style);
            if (style.Default && style.Type == "paragraph")
                DefaultParagraphStyle ??= id;
            if (style.Default && style.Type == "character")
                DefaultCharacterStyle ??= id;
            if (style.Default && style.Type == "table")
                DefaultTableStyle ??= id;
        }
    }

    /// <summary>The default table style ("Normal Table"), applied where a table names none.</summary>
    public string? DefaultTableStyle { get; }

    public StyleDefinition? Find(string? id) => id != null && styles.TryGetValue(id, out var style) ? style : null;

    /// <summary>A style and the styles it is based on, the root first (cycles are cut).</summary>
    public List<StyleDefinition> Chain(string? id)
    {
        var chain = new List<StyleDefinition>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var style = Find(id); style != null && seen.Add(style.Id); style = Find(style.BasedOn))
            chain.Insert(0, style);
        return chain;
    }

    /// <summary>The paragraph style of a paragraph: its own if it exists and is a paragraph style, else the default one.</summary>
    public string? ParagraphStyleOf(string? id) => Find(id) is { Type: "paragraph" } ? id : DefaultParagraphStyle;

    /// <summary>The paragraph formatting of a paragraph with this style and direct formatting.</summary>
    /// <param name="numbering">The paragraph properties of the paragraph's list level, between the style and the direct formatting.</param>
    /// <param name="table">The paragraph properties of the table style of the paragraph's cell, above the document defaults.</param>
    public ParagraphFormat Paragraph(string? styleId, ParagraphFormat? direct, XElement? numbering = null, ParagraphFormat? table = null)
    {
        var result = DefaultParagraph.Clone();
        if (table != null)
            result.Overlay(table);
        foreach (var style in Chain(ParagraphStyleOf(styleId)))
        {
            var level = new ParagraphFormat();
            level.Apply(style.ParagraphProperties);
            result.Overlay(level);
        }
        if (numbering != null)
        {
            var level = new ParagraphFormat();
            level.Apply(numbering);
            result.Overlay(level);
        }
        if (direct != null)
            result.Overlay(direct);
        return result;
    }

    /// <summary>
    /// The character formatting of a run: document defaults, the paragraph style's run properties, the character style's,
    /// then the run's own (<paramref name="direct"/>), with toggle properties combined per ISO/IEC 29500 17.7.3.
    /// </summary>
    public RunFormat Run(string? paragraphStyleId, string? characterStyleId, RunFormat? direct, RunFormat? extraLevel = null)
    {
        var levels = new List<RunFormat>();
        if (extraLevel != null)
            levels.Add(extraLevel);
        levels.Add(Level(Chain(ParagraphStyleOf(paragraphStyleId)), s => s.RunProperties));
        var character = Find(characterStyleId) is { Type: "character" } ? characterStyleId : null;
        levels.Add(Level(Chain(character), s => s.RunProperties));
        return RunFormat.Combine(DefaultRun, levels, direct);
    }

    /// <summary>One level of the hierarchy: the run properties along a basedOn chain, the nearest style winning.</summary>
    static RunFormat Level(List<StyleDefinition> chain, Func<StyleDefinition, XElement?> properties)
    {
        var result = new RunFormat();
        foreach (var style in chain)
        {
            var level = new RunFormat();
            level.Apply(properties(style));
            result.Overlay(level);
        }
        return result;
    }
}
