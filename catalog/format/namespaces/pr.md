# pr: Packages and document properties

`http://schemas.openxmlformats.org/package/2006/relationships`

- Source: ECMA-376-2 schema opc-relationships.xsd
- Office version: 2007
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 2 | 4 | 0 | 2 | 1 | 2 | 0 | 0 |

Full detail (content models, attribute types, defaults): [pr.json](pr.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`pr:Relationship`](#prrelationship) ◆ | `pr:CT_Relationship` | 4 | 0 | 1 |  |
| [`pr:Relationships`](#prrelationships) ◆ | `pr:CT_Relationships` | 0 | 1 | 0 |  |

◆ declared globally (can be the root element of a part)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `pr:ST_TargetMode` | restriction of xsd:string | `External`, `Internal` |  |

## Element details

### pr:Relationship

- Type: `pr:CT_Relationship` (text: `xsd:string`)
- Attributes: `Id`!, `Target`!, `TargetMode`, `Type`!
- Parents: `pr:Relationships`

### pr:Relationships

- Type: `pr:CT_Relationships`
- Children: `pr:Relationship`

`!` marks required attributes.
