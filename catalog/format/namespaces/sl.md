# sl: Packages and document properties

`http://schemas.openxmlformats.org/schemaLibrary/2006/main`

- Source: ECMA-376-4 transitional schema shared-customXmlSchemaProperties.xsd
- Office version: 2007
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 2 | 4 | 0 | 2 | 0 | 0 | 2 | 1 |

Full detail (content models, attribute types, defaults): [sl.json](sl.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`sl:schema`](#slschema) | `sl:CT_Schema` | 4 | 0 | 1 | §23.2.1 p.3814, OI 2.1.1748 |
| [`sl:schemaLibrary`](#slschemalibrary) ◆ | `sl:CT_SchemaLibrary` | 0 | 1 | 1 | §23.2.2 p.3816 |

◆ declared globally (can be the root element of a part)

## Element details

### sl:schema

- Type: `sl:CT_Schema`
- Attributes: `sl:manifestLocation`, `sl:schemaLanguage`, `sl:schemaLocation`, `sl:uri`
- Parents: `sl:schemaLibrary`
- Specification: §23.2.1 p.3814, OI 2.1.1748

### sl:schemaLibrary

- Type: `sl:CT_SchemaLibrary`
- Children: `sl:schema`
- Parents: `w:settings`
- Specification: §23.2.2 p.3816

`!` marks required attributes.
