# pvml: VML (legacy drawing)

`urn:schemas-microsoft-com:office:powerpoint`

- Source: ECMA-376-4 transitional schema vml-presentationDrawing.xsd
- Office version: 2007
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 2 | 1 | 0 | 2 | 0 | 0 | 2 | 1 |

Full detail (content models, attribute types, defaults): [pvml.json](pvml.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`pvml:iscomment`](#pvmliscomment) ◆ | `pvml:CT_Empty` | 0 | 0 | 1 | Part 4 §19.5.2.1 p.877 |
| [`pvml:textdata`](#pvmltextdata) ◆ | `pvml:CT_Rel` | 1 | 0 | 11 | Part 4 §19.5.2.2 p.877, OI 2.1.1892 |

◆ declared globally (can be the root element of a part)

## Element details

### pvml:iscomment

- Type: `pvml:CT_Empty`
- Parents: `v:shape`
- Specification: Part 4 §19.5.2.1 p.877

### pvml:textdata

- Type: `pvml:CT_Rel`
- Attributes: `id`
- Parents: `v:arc`, `v:curve`, `v:group`, `v:image`, `v:line`, `v:oval`, `v:polyline`, `v:rect`, `v:roundrect`, `v:shape`, `v:shapetype`
- Specification: Part 4 §19.5.2.2 p.877, OI 2.1.1892

`!` marks required attributes.
