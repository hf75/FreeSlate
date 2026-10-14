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

    /// <summary>
    /// The default paragraph style, applied where a paragraph names none: the last paragraph style marked w:default,
    /// without one the paragraph style named Normal, in any case (measured with docx/local/nodefault-id, -name,
    /// -other, -lower, -first, -mine, -two: its id does not count).
    /// </summary>
    public string? DefaultParagraphStyle { get; }

    /// <summary>The default character style ("Default Paragraph Font").</summary>
    public string? DefaultCharacterStyle { get; }

    static readonly XElement TemplateParagraph = new(Wml.W + "pPr",
        new XElement(Wml.W + "spacing", new XAttribute(Wml.W + "after", "160"), new XAttribute(Wml.W + "line", "278"), new XAttribute(Wml.W + "lineRule", "auto")));

    /// <summary>
    /// The document defaults Word gives a document without a styles part: those of its own Normal template as its
    /// styles part states them, the document theme's minor fonts at 12 pt, kerning and standard and contextual
    /// ligatures, 8 pt after paragraphs and 1.16 lines (measured with docx/local/defaults-none: without a theme part
    /// Aptos, East Asian text falling back from it, Arabic without w:rtl in Arial; thd-nostyles-georgia,
    /// thd-nodef-calibri: with a theme of Georgia or Calibri those, right-to-left text in Times New Roman;
    /// docx/external/libreoffice/extras-ooxmlexport/tdf108496). So it does for each of w:rPrDefault and w:pPrDefault
    /// a styles part leaves out (also in an empty w:docDefaults, in every mode: thd-emptydef-georgia, thd-ppr-georgia,
    /// thd-nodef-georgia-12, -14; thd2-b prints as thd2-c, which names these theme fonts); where they are, empty ones
    /// too, Times New Roman 10 pt and single spacing are the base (docx/local/defaults-empty, nodefaults-none, -empty,
    /// -run, -para).
    /// </summary>
    static readonly XElement TemplateThemeRun = new(Wml.W + "rPr",
        new XElement(Wml.W + "rFonts", new XAttribute(Wml.W + "asciiTheme", "minorHAnsi"), new XAttribute(Wml.W + "eastAsiaTheme", "minorHAnsi"),
            new XAttribute(Wml.W + "hAnsiTheme", "minorHAnsi"), new XAttribute(Wml.W + "cstheme", "minorBidi")),
        new XElement(Wml.W + "kern", new XAttribute(Wml.W + "val", "2")),
        new XElement(Wml.W + "sz", new XAttribute(Wml.W + "val", "24")),
        new XElement(Wml.W + "szCs", new XAttribute(Wml.W + "val", "24")),
        new XElement(Wml.W14 + "ligatures", new XAttribute(Wml.W14 + "val", "standardContextual")));

    /// <param name="fromTemplate">
    /// Whether the styles update from the template on opening (w:linkStyles) without one attached: Word takes its
    /// Normal template's document defaults and its Normal style, which states nothing, the document's other styles
    /// based on them (measured with docx/local/lsk-1: Arial 10 pt single in Normal turns to the theme font at 12 pt,
    /// 1.16 lines and 8 pt after, a heading keeps its 16 pt bold; lsk-2: with a template attached that cannot be found
    /// nothing changes; docx/external/libreoffice/extras-ooxmlexport/tdf61000).
    /// </param>
    public StyleSheet(XDocument? document, bool fromTemplate = false)
    {
        var root = document?.Root;
        if (root == null)
        {
            DefaultRun.Apply(TemplateThemeRun);
            DefaultParagraph.Apply(TemplateParagraph);
            return;
        }
        var defaults = root.Child("docDefaults");
        if (fromTemplate)
        {
            DefaultRun.Apply(TemplateThemeRun);
            DefaultParagraph.Apply(TemplateParagraph);
        }
        else
        {
            DefaultRun.Apply(defaults.Child("rPrDefault") is { } runDefault ? runDefault.Child("rPr") : TemplateThemeRun);
            DefaultParagraph.Apply(defaults.Child("pPrDefault") is { } paragraphDefault ? paragraphDefault.Child("pPr") : TemplateParagraph);
        }
        var builtIn = new Dictionary<(string Type, string Name), List<string>>();
        foreach (var e in root.Elements(Wml.W + "style"))
        {
            // A style without an id has its name for one, as it is (measured with docx/local/styleid: a paragraph naming
            // it applies it, one naming it without its spaces or in another case not, nor one naming a style that has
            // an id by its name; docx/external/poi/60329, written by SQL Server Reporting Services).
            var id = e.Attr("styleId") ?? e.Child("name").Val();
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
            // Word keeps its own Normal Table: of a table style of that name only its place counts (docx/local/
            // tn-default, tn-explicit, tn-named-only, tdef-zero: no borders, indent or text formatting of it show,
            // cells keep 5.4 pt margins left and right; a style with its id and another name is used as it is).
            if (style.Type == "table" && string.Equals(style.Name, "Normal Table", StringComparison.OrdinalIgnoreCase))
                style = BuiltInNormalTable(id, style.Default);
            // The template's Normal style states nothing.
            if (fromTemplate && style.Type == "paragraph" && string.Equals(style.Name, "Normal", StringComparison.OrdinalIgnoreCase))
                style = new StyleDefinition
                {
                    Id = id, Type = "paragraph", Name = style.Name, Next = style.Next, Default = style.Default,
                    Element = new XElement(Wml.W + "style", new XAttribute(Wml.W + "type", "paragraph"), new XAttribute(Wml.W + "styleId", id),
                        new XElement(Wml.W + "name", new XAttribute(Wml.W + "val", style.Name!))),
                };
            if (styles.TryAdd(id, style) && style.Name is { } styleName && BuiltInNames.Contains(styleName))
            {
                if (!builtIn.TryGetValue((style.Type, styleName.ToLowerInvariant()), out var ids))
                    builtIn[(style.Type, styleName.ToLowerInvariant())] = ids = [];
                ids.Add(id);
            }
            if (style.Default && style.Type == "paragraph")
                DefaultParagraphStyle = id;
            if (style.Default && style.Type == "character")
                DefaultCharacterStyle = id;
            if (style.Default && style.Type == "table")
                DefaultTableStyle = id;
        }
        // A built-in style defined twice under its name in any case is the later definition, also where a paragraph
        // names the earlier one (measured with docx/local/dupst-*: "footnote text" of 20 pt, then "Footnote Text"
        // empty, a paragraph of the first prints in Normal's 11 pt; custom names keep the first; docx/external/tika/
        // testWORD_various).
        foreach (var ids in builtIn.Values.Where(ids => ids.Count > 1))
        {
            var last = styles[ids[^1]];
            foreach (var earlier in ids.Take(ids.Count - 1))
            {
                styles[earlier] = new StyleDefinition
                {
                    Id = earlier, Type = last.Type, Name = last.Name, BasedOn = last.BasedOn, Next = last.Next, Default = styles[earlier].Default,
                    ParagraphProperties = last.ParagraphProperties, RunProperties = last.RunProperties, TableProperties = last.TableProperties,
                    Element = last.Element,
                };
            }
        }
        DefaultParagraphStyle ??= IdByName("Normal", "paragraph");
    }

    /// <summary>Names of Word's built-in styles that a document may define twice (the later definition counts).</summary>
    static readonly HashSet<string> BuiltInNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "footnote text", "endnote text", "footnote reference", "endnote reference", "annotation text", "annotation reference",
        "header", "footer", "caption", "title", "subtitle", "balloon text", "list paragraph", "no spacing", "quote",
        "intense quote", "page number", "line number", "body text", "plain text", "hyperlink", "followedhyperlink",
        "heading 1", "heading 2", "heading 3", "heading 4", "heading 5", "heading 6", "heading 7", "heading 8", "heading 9",
        "toc 1", "toc 2", "toc 3", "toc 4", "toc 5", "toc 6", "toc 7", "toc 8", "toc 9",
    };

    /// <summary>Word's own Normal Table under a document's id for it: no indent, cell margins 0 and 5.4 pt.</summary>
    static StyleDefinition BuiltInNormalTable(string id, bool isDefault)
    {
        XElement Margin(string side, int twips) =>
            new(Wml.W + side, new XAttribute(Wml.W + "w", twips), new XAttribute(Wml.W + "type", "dxa"));
        var element = new XElement(Wml.W + "style", new XAttribute(Wml.W + "type", "table"), new XAttribute(Wml.W + "styleId", id),
            new XElement(Wml.W + "name", new XAttribute(Wml.W + "val", "Normal Table")),
            new XElement(Wml.W + "tblPr",
                new XElement(Wml.W + "tblInd", new XAttribute(Wml.W + "w", 0), new XAttribute(Wml.W + "type", "dxa")),
                new XElement(Wml.W + "tblCellMar", Margin("top", 0), Margin("left", 108), Margin("bottom", 0), Margin("right", 108))));
        return new StyleDefinition
        {
            Id = id, Type = "table", Name = "Normal Table", Default = isDefault, TableProperties = element.Element(Wml.W + "tblPr"), Element = element,
        };
    }

    /// <summary>
    /// The default table style, applied where a table names none: the last table style marked w:default
    /// (docx/local/tdef-two); none without one.
    /// </summary>
    public string? DefaultTableStyle { get; }

    public StyleDefinition? Find(string? id) => id != null && styles.TryGetValue(id, out var style) ? style : null;

    /// <summary>
    /// Adds a paragraph style of imported content (an RTF chunk's style the document lacks) with its own paragraph and
    /// run properties; returns its id.
    /// </summary>
    public string AddImported(string name, XElement paragraphProperties, XElement runProperties)
    {
        var id = "Imported" + new string(name.Where(char.IsAsciiLetterOrDigit).ToArray());
        for (var n = 2; styles.ContainsKey(id); n++)
            id = "Imported" + new string(name.Where(char.IsAsciiLetterOrDigit).ToArray()) + n;
        var element = new XElement(Wml.W + "style", new XAttribute(Wml.W + "type", "paragraph"), new XAttribute(Wml.W + "styleId", id),
            new XElement(Wml.W + "name", new XAttribute(Wml.W + "val", name)), new XElement(paragraphProperties), new XElement(runProperties));
        styles[id] = new StyleDefinition
        {
            Id = id, Type = "paragraph", Name = name, ParagraphProperties = element.Element(Wml.W + "pPr"), RunProperties = element.Element(Wml.W + "rPr"), Element = element,
        };
        return id;
    }

    /// <summary>The id of the style of a type with a name (w:name, ignoring case), or null.</summary>
    public string? IdByName(string name, string type) =>
        styles.Values.FirstOrDefault(s => s.Type == type && string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))?.Id;

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
    /// <param name="numberingUnderStyle">
    /// Whether the list level's properties go under the style's: where the numbering comes from the paragraph style,
    /// the style's indents and tabs override the level's attribute by attribute (measured with docx/local/styleind-A …
    /// -H: a style's left 1080 with a level's left 1620 and hanging 360 makes left 1080 and hanging 360); where the
    /// paragraph numbers itself, the level's override the style's (docx/generated/lists).
    /// </param>
    public ParagraphFormat Paragraph(string? styleId, ParagraphFormat? direct, XElement? numbering = null, ParagraphFormat? table = null,
        bool numberingUnderStyle = false)
    {
        var result = DefaultParagraph.Clone();
        if (table != null)
            result.Overlay(table);
        if (numbering != null && numberingUnderStyle)
        {
            var level = new ParagraphFormat();
            level.Apply(numbering);
            result.Overlay(level);
        }
        foreach (var style in Chain(ParagraphStyleOf(styleId)))
        {
            var level = new ParagraphFormat();
            level.Apply(style.ParagraphProperties);
            result.Overlay(level);
        }
        if (numbering != null && !numberingUnderStyle)
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
        // Word ignores a character style's highlighting, a color as none (docx/external/libreoffice/extras-ooxmlexport/
        // tdf138345_charStyleHighlight: no yellow behind the style's text; tdf137683_charHighlightTests: a paragraph
        // style's cyan under a character style's none).
        var characterLevel = Level(Chain(character), s => s.RunProperties);
        characterLevel.Highlight = null;
        levels.Add(characterLevel);
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
