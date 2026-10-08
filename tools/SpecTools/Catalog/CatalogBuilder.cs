using System.Xml;
using System.Xml.Schema;

namespace SpecTools.Catalog;

/// <summary>
/// Compiles the ECMA-376 transitional schemas, the OPC schemas and the Office extension schemas
/// and turns them into one <see cref="NamespaceEntry"/> per namespace, linked to the specification sections.
/// </summary>
public sealed class CatalogBuilder(NamespaceRegistry registry, SpecIndex spec)
{
    const string XmlNamespace = "http://www.w3.org/XML/1998/namespace";
    static readonly XmlQualifiedName AnyType = new("anyType", XmlSchema.Namespace);

    /// <summary>The ECMA-376 chapters (Part 1 and Part 4 section prefixes) that describe each standard namespace.</summary>
    static readonly Dictionary<string, (string[] Part1, string[] Part4)> Chapters = new()
    {
        ["http://schemas.openxmlformats.org/wordprocessingml/2006/main"] = (["17."], ["14."]),
        ["http://schemas.openxmlformats.org/spreadsheetml/2006/main"] = (["18."], ["15."]),
        ["http://schemas.openxmlformats.org/presentationml/2006/main"] = (["19."], ["16."]),
        ["http://schemas.openxmlformats.org/drawingml/2006/main"] = (["20.1.", "21.1."], ["17."]),
        ["http://schemas.openxmlformats.org/drawingml/2006/picture"] = (["20.2."], ["17."]),
        ["http://schemas.openxmlformats.org/drawingml/2006/lockedCanvas"] = (["20.3."], ["17."]),
        ["http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"] = (["20.4."], ["17."]),
        ["http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing"] = (["20.5."], ["17."]),
        ["http://schemas.openxmlformats.org/drawingml/2006/chart"] = (["21.2."], ["18."]),
        ["http://schemas.openxmlformats.org/drawingml/2006/chartDrawing"] = (["21.3."], ["18."]),
        ["http://schemas.openxmlformats.org/drawingml/2006/diagram"] = (["21.4."], ["18."]),
        ["http://schemas.openxmlformats.org/officeDocument/2006/math"] = (["22.1."], ["20."]),
        ["http://schemas.openxmlformats.org/officeDocument/2006/extended-properties"] = (["22.2."], ["20."]),
        ["http://schemas.openxmlformats.org/officeDocument/2006/custom-properties"] = (["22.3."], ["20."]),
        ["http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes"] = (["22.4."], ["20."]),
        ["http://schemas.openxmlformats.org/officeDocument/2006/customXml"] = (["22.5."], ["20."]),
        ["http://schemas.openxmlformats.org/officeDocument/2006/bibliography"] = (["22.6."], ["20."]),
        ["http://schemas.openxmlformats.org/officeDocument/2006/characteristics"] = (["22.7."], ["20."]),
        ["http://schemas.openxmlformats.org/officeDocument/2006/relationships"] = (["22.8."], ["20."]),
        ["http://schemas.openxmlformats.org/officeDocument/2006/sharedTypes"] = (["22.9."], ["20."]),
        ["http://schemas.openxmlformats.org/schemaLibrary/2006/main"] = (["23."], ["20."]),
        ["urn:schemas-microsoft-com:vml"] = ([], ["19."]),
        ["urn:schemas-microsoft-com:office:office"] = ([], ["19."]),
        ["urn:schemas-microsoft-com:office:word"] = ([], ["19."]),
        ["urn:schemas-microsoft-com:office:excel"] = ([], ["19."]),
        ["urn:schemas-microsoft-com:office:powerpoint"] = ([], ["19."]),
    };

    public static readonly string[] Areas =
    [
        "WordprocessingML (DOCX)",
        "PresentationML (PPTX)",
        "DrawingML",
        "Charts",
        "Diagrams (SmartArt)",
        "Math (OMML)",
        "VML (legacy drawing)",
        "Packages and document properties",
        "SpreadsheetML (out of scope)",
    ];

    readonly Dictionary<string, List<string>> sources = [];
    readonly ILookup<string, SpecSection> ecmaByName = spec.Ecma.ToLookup(s => s.Name);
    readonly ILookup<(string Ns, string Kind, string Name), SpecSection> msByKey = spec.Ms.ToLookup(s => (s.Namespace!, s.Kind!, s.Name));
    readonly ILookup<(int Part, string Section), string> notesBySection = spec.OfficeNotes.ToLookup(n => (n.Part, n.IsoSection), n => n.Section);

    readonly HashSet<XmlQualifiedName> shimNames = [];

    public List<string> Errors { get; } = [];
    public List<string> Errata { get; } = [];
    public int SchemaFiles { get; private set; }
    public HashSet<string> UsedOfficeNotes { get; } = [];

    public List<NamespaceEntry> Build()
    {
        var msIndex = MsXsdExtractor.LoadIndex().ToDictionary(e => e.File);
        var main = Compile("main",
            Directory.GetFiles(Sources.TransitionalXsdDir, "*.xsd").Order()
                .Select(f => (f, $"ECMA-376-4 transitional schema {Path.GetFileName(f)}"))
                .Concat(Directory.GetFiles(Repo.MsXsd, "*.xsd").Order()
                    .Select(f => (f, msIndex.TryGetValue(Path.GetFileName(f), out var e) ? $"[{e.Document}] v{e.Version} §{e.Section}" : Path.GetFileName(f)))));
        var opc = Compile("opc",
            Directory.GetFiles(Sources.OpcXsdDir, "*.xsd").Order().Select(f => (f, $"ECMA-376-2 schema {Path.GetFileName(f)}")));

        var result = new List<NamespaceEntry>();
        foreach (var set in new[] { main, opc })
            result.AddRange(BuildNamespaces(set, new SchemaWalker(set)));

        return result.OrderBy(n => Array.IndexOf(Areas, n.Area)).ThenBy(n => n.Prefix, StringComparer.Ordinal).ToList();
    }

    XmlSchemaSet Compile(string name, IEnumerable<(string File, string Label)> files)
    {
        var set = new XmlSchemaSet { XmlResolver = null };
        set.ValidationEventHandler += (_, e) =>
        {
            if (e.Severity == XmlSeverityType.Error)
                Errors.Add($"{name}: {e.Message} ({Path.GetFileName(e.Exception?.SourceUri)}:{e.Exception?.LineNumber})");
        };

        foreach (var (file, label) in files)
        {
            SchemaFiles++;
            using var reader = XmlReader.Create(file, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
            var schema = XmlSchema.Read(reader, (_, e) => Errors.Add($"{Path.GetFileName(file)}: {e.Message}"))!;
            Errata.AddRange(Shims.ApplyErrata(schema, Path.GetFileName(file)));
            set.Add(schema);
            var ns = schema.TargetNamespace ?? "";
            if (!sources.TryGetValue(ns, out var labels))
                sources[ns] = labels = [];
            labels.Add(label);
        }

        foreach (var (_, schema) in Shims.For(name))
        {
            set.Add(schema);
            shimNames.UnionWith(Shims.DeclaredNames(schema));
        }

        set.Compile();
        return set;
    }

    IEnumerable<NamespaceEntry> BuildNamespaces(XmlSchemaSet set, SchemaWalker walker)
    {
        // Namespaces that only shims declare are not part of the catalog.
        var uris = set.Schemas().Cast<XmlSchema>()
            .Select(s => s.TargetNamespace ?? "")
            .Where(ns => ns.Length > 0 && ns != XmlNamespace && sources.ContainsKey(ns))
            .Distinct();

        foreach (var uri in uris)
        {
            var (area, inScope) = Classify(uri);
            var entry = new NamespaceEntry
            {
                Prefix = registry.Prefix(uri),
                Uri = uri,
                Area = area,
                InScope = inScope,
                OfficeVersion = registry.OfficeVersion(uri) ?? (Chapters.ContainsKey(uri) || uri.Contains("openxmlformats.org") ? "Office2007" : null),
                Source = string.Join(" + ", sources.GetValueOrDefault(uri) ?? []),
            };

            entry.Elements = walker.Elements.Values
                .Where(e => e.Name.Namespace == uri)
                .OrderBy(e => e.Name.Name, StringComparer.Ordinal)
                .Select(e => BuildElement(e, walker))
                .ToList();
            entry.GlobalAttributes = set.GlobalAttributes.Values.Cast<XmlSchemaAttribute>()
                .Where(a => a.QualifiedName.Namespace == uri)
                .OrderBy(a => a.QualifiedName.Name, StringComparer.Ordinal)
                .Select(a => BuildAttribute(a) with { Spec = NullIfEmpty(SpecFor(a.QualifiedName, "Attributes")) })
                .ToList();
            entry.ComplexTypes = walker.Types.Values
                .Where(t => !t.Type.QualifiedName.IsEmpty && t.Type.QualifiedName.Namespace == uri)
                .OrderBy(t => t.Type.QualifiedName.Name, StringComparer.Ordinal)
                .Select(BuildComplexType)
                .ToList();
            entry.SimpleTypes = set.GlobalTypes.Values.OfType<XmlSchemaSimpleType>()
                .Where(t => t.QualifiedName.Namespace == uri && !shimNames.Contains(t.QualifiedName))
                .OrderBy(t => t.QualifiedName.Name, StringComparer.Ordinal)
                .Select(BuildSimpleType)
                .ToList();
            entry.Counts = Count(entry);
            yield return entry;
        }
    }

    ElementEntry BuildElement(SchemaWalker.ElementData data, SchemaWalker walker)
    {
        var entry = new ElementEntry { Name = Q(data.Name), Global = data.Global ? true : null };
        var attributes = new Dictionary<XmlQualifiedName, AttributeEntry>();
        foreach (var type in data.Types)
        {
            entry.Types.Add(TypeName(type, walker));
            switch (type)
            {
                case XmlSchemaComplexType complex:
                    foreach (XmlSchemaAttribute attribute in complex.AttributeUses.Values)
                        attributes.TryAdd(attribute.QualifiedName, BuildAttribute(attribute));
                    var typeData = walker.Types[complex];
                    foreach (var child in typeData.Children.Select(Q).Concat(typeData.Wildcards.Select(w => $"any({w})")))
                    {
                        if (!entry.Children.Contains(child))
                            entry.Children.Add(child);
                    }
                    if (complex.ContentType == XmlSchemaContentType.TextOnly)
                        entry.Text ??= SimpleContentType(complex);
                    break;
                case XmlSchemaSimpleType simple:
                    entry.Text ??= TypeName(simple, walker);
                    break;
            }
        }

        entry.Attributes = attributes.Values.OrderBy(a => a.Name, StringComparer.Ordinal).ToList();
        entry.Parents = data.Parents.Select(Q).Order(StringComparer.Ordinal).ToList();
        entry.Spec = SpecFor(data.Name, "Elements");

        // The Microsoft documents describe local elements only as part of their complex type.
        if (entry.Spec.Count == 0 && !Chapters.ContainsKey(data.Name.Namespace))
        {
            entry.Spec = data.Types
                .Where(t => !t.QualifiedName.IsEmpty)
                .SelectMany(t => SpecFor(t.QualifiedName, "Complex Types"))
                .ToList();
        }
        return entry;
    }

    AttributeEntry BuildAttribute(XmlSchemaAttribute attribute)
    {
        var type = attribute.AttributeSchemaType;
        var typeName = "xsd:anySimpleType";
        List<string>? values = null;
        if (type != null && !type.QualifiedName.IsEmpty)
            typeName = Q(type.QualifiedName);
        else if (type != null)
            (typeName, values, _) = Describe(type);

        return new AttributeEntry(
            Q(attribute.QualifiedName),
            typeName,
            attribute.Use == XmlSchemaUse.Required ? true : null,
            attribute.DefaultValue ?? attribute.FixedValue,
            values);
    }

    ComplexTypeEntry BuildComplexType(SchemaWalker.TypeData data)
    {
        var type = data.Type;
        var content = type.ContentType switch
        {
            XmlSchemaContentType.Empty => "empty",
            XmlSchemaContentType.TextOnly => "text",
            XmlSchemaContentType.Mixed => "mixed",
            _ => "elements",
        };
        var baseType = type.BaseXmlSchemaType;
        return new ComplexTypeEntry
        {
            Name = Q(type.QualifiedName),
            Base = baseType != null && !baseType.QualifiedName.IsEmpty && baseType.QualifiedName != AnyType ? Q(baseType.QualifiedName) : null,
            Content = content,
            Model = content is "elements" or "mixed" ? NullIfEmpty(Model(type.ContentTypeParticle)) : null,
            Attributes = type.AttributeUses.Values.Cast<XmlSchemaAttribute>().Select(a => Q(a.QualifiedName)).Order(StringComparer.Ordinal).ToList(),
            UsedBy = data.Users.Select(Q).Order(StringComparer.Ordinal).ToList(),
            Spec = NullIfEmpty(SpecFor(type.QualifiedName, "Complex Types")),
        };
    }

    SimpleTypeEntry BuildSimpleType(XmlSchemaSimpleType type)
    {
        var (definition, values, facets) = Describe(type);
        return new SimpleTypeEntry
        {
            Name = Q(type.QualifiedName),
            Definition = definition,
            Values = values,
            Facets = facets,
            Spec = NullIfEmpty(SpecFor(type.QualifiedName, "Simple Types")),
        };
    }

    (string Definition, List<string>? Values, Dictionary<string, string>? Facets) Describe(XmlSchemaSimpleType type)
    {
        switch (type.Content)
        {
            case XmlSchemaSimpleTypeRestriction restriction:
            {
                var baseName = !restriction.BaseTypeName.IsEmpty ? Q(restriction.BaseTypeName)
                    : restriction.BaseType != null ? Describe(restriction.BaseType).Definition
                    : "xsd:anySimpleType";
                var values = restriction.Facets.OfType<XmlSchemaEnumerationFacet>().Select(f => f.Value ?? "").ToList();
                var facets = restriction.Facets.OfType<XmlSchemaFacet>()
                    .Where(f => f is not XmlSchemaEnumerationFacet)
                    .GroupBy(FacetName)
                    .ToDictionary(g => g.Key, g => string.Join(" | ", g.Select(f => f.Value)));
                return ($"restriction of {baseName}", values.Count > 0 ? values : null, facets.Count > 0 ? facets : null);
            }
            case XmlSchemaSimpleTypeUnion union:
            {
                var members = union.MemberTypes?.Select(Q).ToList() ?? [];
                var values = new List<string>();
                foreach (XmlSchemaSimpleType inline in union.BaseTypes)
                {
                    var (definition, inlineValues, _) = Describe(inline);
                    members.Add(definition);
                    values.AddRange(inlineValues ?? []);
                }
                return ($"union of {string.Join(", ", members)}", values.Count > 0 ? values : null, null);
            }
            case XmlSchemaSimpleTypeList list:
            {
                var item = !list.ItemTypeName.IsEmpty ? Q(list.ItemTypeName)
                    : list.ItemType != null ? Describe(list.ItemType).Definition
                    : "xsd:anySimpleType";
                return ($"list of {item}", null, null);
            }
            default:
                return (Q(type.QualifiedName), null, null);
        }
    }

    static string FacetName(XmlSchemaFacet facet)
    {
        var name = facet.GetType().Name.Replace("XmlSchema", "").Replace("Facet", "");
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    string SimpleContentType(XmlSchemaComplexType complex)
    {
        for (XmlSchemaType? type = complex; type != null; type = type.BaseXmlSchemaType)
        {
            if (type is XmlSchemaSimpleType simple)
                return simple.QualifiedName.IsEmpty ? Describe(simple).Definition : Q(simple.QualifiedName);
        }
        return "xsd:anySimpleType";
    }

    string TypeName(XmlSchemaType type, SchemaWalker walker)
    {
        if (!type.QualifiedName.IsEmpty)
            return Q(type.QualifiedName);
        if (type is XmlSchemaComplexType complex && walker.Types.TryGetValue(complex, out var data) && data.Owner is { } owner)
            return $"(anonymous type of {Q(owner)})";
        if (type is XmlSchemaSimpleType simple)
            return Describe(simple).Definition;
        return "(anonymous)";
    }

    /// <summary>Content model in a compact notation: "," sequence, "|" choice, "&amp;" all; ?, *, + and {min,max} for occurrence.</summary>
    string Model(XmlSchemaParticle? particle)
    {
        static string Occurs(XmlSchemaParticle p)
        {
            var unbounded = p.MaxOccurs == decimal.MaxValue;
            if (p.MinOccurs == 1 && p.MaxOccurs == 1) return "";
            if (p.MinOccurs == 0 && p.MaxOccurs == 1) return "?";
            if (p.MinOccurs == 0 && unbounded) return "*";
            if (p.MinOccurs == 1 && unbounded) return "+";
            return $"{{{p.MinOccurs},{(unbounded ? "" : p.MaxOccurs.ToString())}}}";
        }

        string Group(XmlSchemaGroupBase group, string separator, XmlSchemaParticle occurs)
        {
            var items = group.Items.Cast<XmlSchemaParticle>().Select(Model).Where(s => s.Length > 0).ToList();
            if (items.Count == 0) return "";
            var suffix = Occurs(occurs);
            return items.Count == 1 && suffix.Length == 0 ? items[0] : $"({string.Join(separator, items)}){suffix}";
        }

        return particle switch
        {
            XmlSchemaElement e => Q(e.QualifiedName) + Occurs(e),
            XmlSchemaAny any => $"any({(string.IsNullOrEmpty(any.Namespace) ? "##any" : any.Namespace)}){Occurs(any)}",
            XmlSchemaSequence s => Group(s, ", ", s),
            XmlSchemaChoice c => Group(c, " | ", c),
            XmlSchemaAll a => Group(a, " & ", a),
            XmlSchemaGroupRef { Particle: XmlSchemaSequence s } r => Group(s, ", ", r),
            XmlSchemaGroupRef { Particle: XmlSchemaChoice c } r => Group(c, " | ", r),
            XmlSchemaGroupRef { Particle: XmlSchemaAll a } r => Group(a, " & ", r),
            _ => "",
        };
    }

    List<SpecRef> SpecFor(XmlQualifiedName name, string kind)
    {
        var refs = new List<SpecRef>();
        if (Chapters.TryGetValue(name.Namespace, out var chapters))
        {
            // ECMA-376 has sections for elements and simple types only.
            if (kind is not ("Elements" or "Simple Types"))
                return refs;
            foreach (var section in ecmaByName[name.Name])
            {
                var (part, prefixes) = section.Doc == "ECMA-376-1" ? (1, chapters.Part1) : (4, chapters.Part4);
                if (!prefixes.Any(p => section.Section.StartsWith(p, StringComparison.Ordinal)))
                    continue;
                var notes = notesBySection[(part, section.Section)].ToList();
                UsedOfficeNotes.UnionWith(notes);
                refs.Add(new SpecRef(section.Doc, section.Section, section.Title, section.Page, notes.Count > 0 ? notes : null));
            }
        }
        else
        {
            foreach (var section in msByKey[(name.Namespace, kind, name.Name)])
                refs.Add(new SpecRef(section.Doc, section.Section, section.Title, section.Page));
        }
        return refs;
    }

    static NamespaceCounts Count(NamespaceEntry ns)
    {
        var refs = ns.Elements.SelectMany(e => e.Spec)
            .Concat(ns.GlobalAttributes.SelectMany(a => a.Spec ?? []))
            .Concat(ns.ComplexTypes.SelectMany(t => t.Spec ?? []))
            .Concat(ns.SimpleTypes.SelectMany(t => t.Spec ?? []))
            .ToList();
        return new NamespaceCounts(
            Elements: ns.Elements.Count,
            Attributes: ns.Elements.Sum(e => e.Attributes.Count),
            GlobalAttributes: ns.GlobalAttributes.Count,
            ComplexTypes: ns.ComplexTypes.Count,
            SimpleTypes: ns.SimpleTypes.Count,
            EnumValues: ns.SimpleTypes.Sum(t => t.Values?.Count ?? 0),
            SpecSections: refs.Select(r => (r.Doc, r.Section)).Distinct().Count(),
            OfficeNotes: refs.SelectMany(r => r.OfficeNotes ?? []).Distinct().Count());
    }

    static (string Area, bool InScope) Classify(string uri)
    {
        static bool Has(string uri, params string[] parts) => parts.Any(p => uri.Contains(p, StringComparison.OrdinalIgnoreCase));

        if (Has(uri, "spreadsheetml", "/office/excel/", "spreadsheetDrawing", "office:excel"))
            return (Areas[8], false);
        if (uri.StartsWith("urn:schemas-microsoft-com:", StringComparison.Ordinal))
            return (Areas[6], true);
        if (Has(uri, "wordprocessingml", "/office/word/", "wordprocessingDrawing"))
            return (Areas[0], true);
        if (Has(uri, "presentationml", "/office/powerpoint/"))
            return (Areas[1], true);
        if (Has(uri, "/chart"))
            return (Areas[3], true);
        if (Has(uri, "diagram"))
            return (Areas[4], true);
        if (Has(uri, "/math"))
            return (Areas[5], true);
        if (Has(uri, "drawingml", "/office/drawing/", "thememl", "/ink/"))
            return (Areas[2], true);
        return (Areas[7], true);
    }

    string Q(XmlQualifiedName name)
    {
        if (string.IsNullOrEmpty(name.Namespace))
            return name.Name;
        var prefix = registry.Prefix(name.Namespace);
        return $"{prefix}:{name.Name}";
    }

    static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;
    static List<T>? NullIfEmpty<T>(List<T> list) => list.Count == 0 ? null : list;
}
