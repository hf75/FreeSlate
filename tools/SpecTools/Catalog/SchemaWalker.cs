using System.Xml;
using System.Xml.Schema;

namespace SpecTools.Catalog;

/// <summary>
/// Walks a compiled <see cref="XmlSchemaSet"/> and collects every element declaration (global and local)
/// with its types, plus the children and users of every complex type.
/// </summary>
public sealed class SchemaWalker
{
    public sealed class ElementData(XmlQualifiedName name)
    {
        public XmlQualifiedName Name { get; } = name;
        public bool Global { get; set; }
        public List<XmlSchemaType> Types { get; } = [];
        public HashSet<XmlQualifiedName> Parents { get; } = [];
    }

    public sealed class TypeData(XmlSchemaComplexType type)
    {
        public XmlSchemaComplexType Type { get; } = type;
        public List<XmlQualifiedName> Children { get; } = [];
        public List<string> Wildcards { get; } = [];
        public HashSet<XmlQualifiedName> Users { get; } = [];

        /// <summary>For anonymous types: the element that declares the type.</summary>
        public XmlQualifiedName? Owner { get; set; }
    }

    readonly XmlSchemaSet set;
    readonly Dictionary<XmlQualifiedName, ElementData> elements = [];
    readonly Dictionary<XmlSchemaComplexType, TypeData> types = new(ReferenceEqualityComparer.Instance);
    readonly Queue<TypeData> pending = new();

    public SchemaWalker(XmlSchemaSet set)
    {
        this.set = set;
        foreach (XmlSchemaElement element in set.GlobalElements.Values)
            Declare(element, isGlobal: true);
        foreach (XmlSchemaType type in set.GlobalTypes.Values)
        {
            if (type is XmlSchemaComplexType complex)
                Enqueue(complex);
        }
        while (pending.TryDequeue(out var data))
            CollectChildren(data.Type.ContentTypeParticle, data);

        // Groups no type refers to still declare elements: w14's EG_RPrTextEffects (glow, textOutline, ...) and
        // EG_RPrOpenType (ligatures, ...) extend w:rPr through markup compatibility, which a schema cannot express.
        foreach (XmlSchema schema in set.Schemas())
        {
            foreach (XmlSchemaGroup group in schema.Groups.Values)
                DeclareAll(group.Particle);
        }
        while (pending.TryDequeue(out var data))
            CollectChildren(data.Type.ContentTypeParticle, data);

        foreach (var data in types.Values)
        {
            foreach (var child in data.Children)
                elements[child].Parents.UnionWith(data.Users);
        }
    }

    public IReadOnlyDictionary<XmlQualifiedName, ElementData> Elements => elements;
    public IReadOnlyDictionary<XmlSchemaComplexType, TypeData> Types => types;

    TypeData Enqueue(XmlSchemaComplexType type)
    {
        if (!types.TryGetValue(type, out var data))
        {
            data = new TypeData(type);
            types.Add(type, data);
            pending.Enqueue(data);
        }
        return data;
    }

    void Declare(XmlSchemaElement element, bool isGlobal)
    {
        var name = element.QualifiedName;
        var type = element.ElementSchemaType;
        if (type == null && !element.RefName.IsEmpty && set.GlobalElements[element.RefName] is XmlSchemaElement target)
            type = target.ElementSchemaType;
        // Elements of groups no type refers to are not compiled; their named type is still there.
        if (type == null && !element.SchemaTypeName.IsEmpty)
            type = set.GlobalTypes[element.SchemaTypeName] as XmlSchemaType;

        if (!elements.TryGetValue(name, out var data))
            elements.Add(name, data = new ElementData(name));
        data.Global |= isGlobal;

        if (type != null && !data.Types.Contains(type))
            data.Types.Add(type);

        if (type is XmlSchemaComplexType complex)
        {
            var typeData = Enqueue(complex);
            typeData.Users.Add(name);
            if (complex.QualifiedName.IsEmpty)
                typeData.Owner ??= name;
        }
    }

    void DeclareAll(XmlSchemaParticle? particle)
    {
        switch (particle)
        {
            case XmlSchemaElement element:
                Declare(element, isGlobal: false);
                break;
            case XmlSchemaGroupBase group:
                foreach (XmlSchemaParticle item in group.Items)
                    DeclareAll(item);
                break;
        }
    }

    void CollectChildren(XmlSchemaParticle? particle, TypeData data)
    {
        switch (particle)
        {
            case XmlSchemaElement element:
                Declare(element, isGlobal: false);
                if (!data.Children.Contains(element.QualifiedName))
                    data.Children.Add(element.QualifiedName);
                break;
            case XmlSchemaGroupBase group:
                foreach (XmlSchemaParticle item in group.Items)
                    CollectChildren(item, data);
                break;
            case XmlSchemaGroupRef groupRef:
                CollectChildren(groupRef.Particle, data);
                break;
            case XmlSchemaAny any:
                var ns = string.IsNullOrEmpty(any.Namespace) ? "##any" : any.Namespace;
                if (!data.Wildcards.Contains(ns))
                    data.Wildcards.Add(ns);
                break;
        }
    }
}
