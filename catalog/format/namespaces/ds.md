# ds: Packages and document properties

`http://schemas.openxmlformats.org/officeDocument/2006/customXml`

- Source: ECMA-376-4 transitional schema shared-customXmlDataProperties.xsd
- Office version: 2007
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 3 | 2 | 0 | 3 | 0 | 0 | 3 | 0 |

Full detail (content models, attribute types, defaults): [ds.json](ds.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`ds:datastoreItem`](#dsdatastoreitem) ◆ | `ds:CT_DatastoreItem` | 1 | 1 | 0 | §22.5.2.1 p.3755 |
| [`ds:schemaRef`](#dsschemaref) | `ds:CT_DatastoreSchemaRef` | 1 | 0 | 1 | §22.5.2.2 p.3756 |
| [`ds:schemaRefs`](#dsschemarefs) | `ds:CT_DatastoreSchemaRefs` | 0 | 1 | 1 | §22.5.2.3 p.3757 |

◆ declared globally (can be the root element of a part)

## Element details

### ds:datastoreItem

- Type: `ds:CT_DatastoreItem`
- Attributes: `ds:itemID`!
- Children: `ds:schemaRefs`
- Specification: §22.5.2.1 p.3755

### ds:schemaRef

- Type: `ds:CT_DatastoreSchemaRef`
- Attributes: `ds:uri`!
- Parents: `ds:schemaRefs`
- Specification: §22.5.2.2 p.3756

### ds:schemaRefs

- Type: `ds:CT_DatastoreSchemaRefs`
- Children: `ds:schemaRef`
- Parents: `ds:datastoreItem`
- Specification: §22.5.2.3 p.3757

`!` marks required attributes.
