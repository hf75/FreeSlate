# aclsh: DrawingML

`http://schemas.microsoft.com/office/drawing/2020/classificationShape`

- Source: [MS-ODRAWXML] v20261006 §5.37
- Office version: 2021
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 1 | 0 | 1 | 1 | 4 | 3 | 0 |

Full detail (content models, attribute types, defaults): [aclsh.json](aclsh.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`aclsh:classification`](#aclshclassification) ◆ | `aclsh:CT_ClassificationOutcome` | 1 | 0 | 0 | MS-ODRAWXML §2.39.1.1 p.323 |

◆ declared globally (can be the root element of a part)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `aclsh:ST_ClassificationOutcomeType` | restriction of xsd:token | `none`, `hdr`, `ftr`, `watermark` | MS-ODRAWXML §2.39.4.1 p.323 |

## Element details

### aclsh:classification

- Type: `aclsh:CT_ClassificationOutcome`
- Attributes: `classificationOutcomeType`
- Specification: MS-ODRAWXML §2.39.1.1 p.323

`!` marks required attributes.
