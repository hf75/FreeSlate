using System.Xml;
using System.Xml.Schema;

namespace SpecTools.Catalog;

/// <summary>
/// Declarations that the schemas reference but our sources do not contain, plus errata for the extracted schemas.
/// <para>
/// The Office extension schemas were written against Microsoft's internal copies of the base schemas. Those still
/// declare some simple types where ECMA-376 1st edition had them (w:ST_OnOff, a:ST_Guid, c:ST_Xstring, …), while
/// ISO/IEC 29500 moved them to the shared types namespace. Shims alias the old names to the shared types. Further
/// shims stand in for external standards (XML namespace, Dublin Core, W3C InkML) and for the undocumented
/// Word 2006 namespace. Shims are compiled together with the real schemas but left out of the catalog.
/// </para>
/// </summary>
public static class Shims
{
    public sealed record Shim(string Set, string Description, string Xsd);

    const string Head = """<xsd:schema xmlns:xsd="http://www.w3.org/2001/XMLSchema" """;

    public static readonly Shim[] All =
    [
        new("both", "XML namespace attributes (xml:lang, xml:space, xml:base, xml:id)", Head + """
            targetNamespace="http://www.w3.org/XML/1998/namespace">
              <xsd:attribute name="lang" type="xsd:language"/>
              <xsd:attribute name="space">
                <xsd:simpleType>
                  <xsd:restriction base="xsd:NCName">
                    <xsd:enumeration value="default"/>
                    <xsd:enumeration value="preserve"/>
                  </xsd:restriction>
                </xsd:simpleType>
              </xsd:attribute>
              <xsd:attribute name="base" type="xsd:anyURI"/>
              <xsd:attribute name="id" type="xsd:ID"/>
            </xsd:schema>
            """),
        new("main", "1st-edition locations of shared simple types used by the Word extension schemas", Head + """
            xmlns:s="http://schemas.openxmlformats.org/officeDocument/2006/sharedTypes"
            targetNamespace="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
              <xsd:simpleType name="ST_String"><xsd:restriction base="s:ST_String"/></xsd:simpleType>
              <xsd:simpleType name="ST_OnOff"><xsd:restriction base="s:ST_OnOff"/></xsd:simpleType>
              <xsd:simpleType name="ST_HexColorRGB"><xsd:restriction base="s:ST_HexColorRGB"/></xsd:simpleType>
              <xsd:simpleType name="ST_UnsignedDecimalNumber"><xsd:restriction base="s:ST_UnsignedDecimalNumber"/></xsd:simpleType>
            </xsd:schema>
            """),
        new("main", "1st-edition locations of shared simple types used by the DrawingML and PowerPoint extension schemas", Head + """
            xmlns:s="http://schemas.openxmlformats.org/officeDocument/2006/sharedTypes"
            targetNamespace="http://schemas.openxmlformats.org/drawingml/2006/main">
              <xsd:simpleType name="ST_Guid"><xsd:restriction base="s:ST_Guid"/></xsd:simpleType>
              <xsd:simpleType name="ST_TextLanguageID"><xsd:restriction base="s:ST_Lang"/></xsd:simpleType>
            </xsd:schema>
            """),
        new("main", "1st-edition location of ST_Xstring used by the chart extension schemas", Head + """
            xmlns:s="http://schemas.openxmlformats.org/officeDocument/2006/sharedTypes"
            targetNamespace="http://schemas.openxmlformats.org/drawingml/2006/chart">
              <xsd:simpleType name="ST_Xstring"><xsd:restriction base="s:ST_Xstring"/></xsd:simpleType>
            </xsd:schema>
            """),
        new("main", "Word 2006 namespace (wne), referenced by wps:txbx but not published", Head + """
            xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"
            targetNamespace="http://schemas.microsoft.com/office/word/2006/wordml" elementFormDefault="qualified">
              <xsd:element name="txbxContent" type="w:CT_TxbxContent"/>
            </xsd:schema>
            """),
        new("main", "W3C InkML, referenced by the PowerPoint ink action schema", Head + """
            targetNamespace="http://www.w3.org/2003/InkML" elementFormDefault="qualified">
              <xsd:element name="trace" type="xsd:anyType"/>
              <xsd:element name="traceView" type="xsd:anyType"/>
              <xsd:element name="definitions" type="xsd:anyType"/>
              <xsd:complexType name="CT_Matrix">
                <xsd:sequence><xsd:any processContents="lax" minOccurs="0" maxOccurs="unbounded"/></xsd:sequence>
                <xsd:anyAttribute processContents="lax"/>
              </xsd:complexType>
              <xsd:simpleType name="ST_StandardLengthUnits"><xsd:restriction base="xsd:string"/></xsd:simpleType>
              <xsd:simpleType name="ST_StandardTimeUnits"><xsd:restriction base="xsd:string"/></xsd:simpleType>
            </xsd:schema>
            """),
        new("opc", "Dublin Core elements used by the core properties", Head + """
            targetNamespace="http://purl.org/dc/elements/1.1/" elementFormDefault="qualified">
              <xsd:element name="creator" type="xsd:anyType"/>
              <xsd:element name="description" type="xsd:anyType"/>
              <xsd:element name="identifier" type="xsd:anyType"/>
              <xsd:element name="language" type="xsd:anyType"/>
              <xsd:element name="subject" type="xsd:anyType"/>
              <xsd:element name="title" type="xsd:anyType"/>
            </xsd:schema>
            """),
        new("opc", "Dublin Core terms used by the core properties", Head + """
            targetNamespace="http://purl.org/dc/terms/" elementFormDefault="qualified">
              <xsd:element name="created" type="xsd:anyType"/>
              <xsd:element name="modified" type="xsd:anyType"/>
            </xsd:schema>
            """),
    ];

    /// <summary>Shims for one schema set ("main" or "opc"), parsed.</summary>
    public static IEnumerable<(Shim Shim, XmlSchema Schema)> For(string set)
    {
        foreach (var shim in All.Where(s => s.Set == set || s.Set == "both"))
        {
            using var reader = XmlReader.Create(new StringReader(shim.Xsd));
            yield return (shim, XmlSchema.Read(reader, null)!);
        }
    }

    /// <summary>Qualified names of everything a shim schema declares.</summary>
    public static IEnumerable<XmlQualifiedName> DeclaredNames(XmlSchema schema)
    {
        foreach (var item in schema.Items)
        {
            var name = item switch
            {
                XmlSchemaElement e => e.Name,
                XmlSchemaAttribute a => a.Name,
                XmlSchemaType t => t.Name,
                _ => null,
            };
            if (name != null)
                yield return new XmlQualifiedName(name, schema.TargetNamespace);
        }
    }

    /// <summary>
    /// Fixes errors in the published extension schemas. Returns a description of each fix applied.
    /// </summary>
    public static IEnumerable<string> ApplyErrata(XmlSchema schema, string file)
    {
        // a14:ST_SaturationAmount restricts a:ST_Percentage with min/max facets. In the transitional schema
        // a:ST_Percentage is a union (int or "n%"), which cannot carry such facets. The values are integers in
        // thousandths of a percent, i.e. a:ST_PercentageDecimal.
        if (schema.TargetNamespace == "http://schemas.microsoft.com/office/drawing/2010/main")
        {
            foreach (var type in schema.Items.OfType<XmlSchemaSimpleType>())
            {
                if (type.Name == "ST_SaturationAmount" && type.Content is XmlSchemaSimpleTypeRestriction restriction
                    && restriction.BaseTypeName.Name == "ST_Percentage")
                {
                    restriction.BaseTypeName = new XmlQualifiedName("ST_PercentageDecimal", restriction.BaseTypeName.Namespace);
                    yield return $"{file}: a14:ST_SaturationAmount restricts a:ST_PercentageDecimal instead of the union a:ST_Percentage";
                }
            }
        }

        // The ink action schema gives xml:id (type xsd:ID) a default value, which XML Schema does not allow for IDs.
        if (schema.TargetNamespace == "http://schemas.microsoft.com/office/powerpoint/2014/inkAction")
        {
            var fixedCount = 0;
            foreach (var attribute in Descendants(schema).OfType<XmlSchemaAttribute>())
            {
                if (attribute.RefName.Name == "id" && attribute.RefName.Namespace == "http://www.w3.org/XML/1998/namespace" && attribute.DefaultValue != null)
                {
                    attribute.DefaultValue = null;
                    fixedCount++;
                }
            }
            if (fixedCount > 0)
                yield return $"{file}: removed default=\"\" from {fixedCount} references to xml:id (an ID cannot have a default)";
        }
    }

    /// <summary>All schema objects below the schema's items (types, groups, attributes, particles).</summary>
    static IEnumerable<XmlSchemaObject> Descendants(XmlSchema schema)
    {
        var stack = new Stack<XmlSchemaObject>(schema.Items.Cast<XmlSchemaObject>());
        while (stack.Count > 0)
        {
            var item = stack.Pop();
            yield return item;
            IEnumerable<XmlSchemaObject> children = item switch
            {
                XmlSchemaComplexType ct => ct.Attributes.Cast<XmlSchemaObject>()
                    .Concat(ct.Particle is { } p ? [p] : [])
                    .Concat(ct.ContentModel?.Content is { } c ? [c] : []),
                XmlSchemaComplexContentExtension ext => ext.Attributes.Cast<XmlSchemaObject>().Concat(ext.Particle is { } p ? [p] : []),
                XmlSchemaComplexContentRestriction res => res.Attributes.Cast<XmlSchemaObject>().Concat(res.Particle is { } p ? [p] : []),
                XmlSchemaSimpleContentExtension ext => ext.Attributes.Cast<XmlSchemaObject>(),
                XmlSchemaSimpleContentRestriction res => res.Attributes.Cast<XmlSchemaObject>(),
                XmlSchemaAttributeGroup group => group.Attributes.Cast<XmlSchemaObject>(),
                XmlSchemaGroupBase group => group.Items.Cast<XmlSchemaObject>(),
                XmlSchemaGroup group => group.Particle is { } p ? [p] : [],
                XmlSchemaElement { SchemaType: { } type } => [type],
                _ => [],
            };
            foreach (var child in children)
                stack.Push(child);
        }
    }
}
