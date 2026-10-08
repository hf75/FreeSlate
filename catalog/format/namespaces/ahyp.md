# ahyp: DrawingML

`http://schemas.microsoft.com/office/drawing/2018/hyperlinkcolor`

- Source: [MS-ODRAWXML] v20261006 §5.33
- Office version: 2019
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 1 | 0 | 1 | 1 | 2 | 3 | 0 |

Full detail (content models, attribute types, defaults): [ahyp.json](ahyp.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`ahyp:hlinkClr`](#ahyphlinkclr) ◆ | `ahyp:CT_HyperlinkColor` | 1 | 0 | 0 | MS-ODRAWXML §2.35.1.1 p.313 |

◆ declared globally (can be the root element of a part)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `ahyp:ST_HyperlinkColor` | restriction of xsd:token | `hlink`, `tx` | MS-ODRAWXML §2.35.4.1 p.313 |

## Element details

### ahyp:hlinkClr

- Type: `ahyp:CT_HyperlinkColor`
- Attributes: `val`!
- Specification: MS-ODRAWXML §2.35.1.1 p.313

`!` marks required attributes.
