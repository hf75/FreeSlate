# ac: Packages and document properties

`http://schemas.openxmlformats.org/officeDocument/2006/characteristics`

- Source: ECMA-376-4 transitional schema shared-additionalCharacteristics.xsd
- Office version: 2007
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 2 | 4 | 0 | 2 | 1 | 5 | 3 | 0 |

Full detail (content models, attribute types, defaults): [ac.json](ac.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`ac:additionalCharacteristics`](#acadditionalcharacteristics) ◆ | `ac:CT_AdditionalCharacteristics` | 0 | 1 | 0 | §22.7.2.1 p.3795 |
| [`ac:characteristic`](#accharacteristic) | `ac:CT_Characteristic` | 4 | 0 | 1 | §22.7.2.2 p.3795 |

◆ declared globally (can be the root element of a part)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `ac:ST_Relation` | restriction of xsd:string | `ge`, `le`, `gt`, `lt`, `eq` | §22.7.3.1 p.3797 |

## Element details

### ac:additionalCharacteristics

- Type: `ac:CT_AdditionalCharacteristics`
- Children: `ac:characteristic`
- Specification: §22.7.2.1 p.3795

### ac:characteristic

- Type: `ac:CT_Characteristic`
- Attributes: `name`!, `relation`!, `val`!, `vocabulary`
- Parents: `ac:additionalCharacteristics`
- Specification: §22.7.2.2 p.3795

`!` marks required attributes.
