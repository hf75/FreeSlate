# dgm14: Diagrams (SmartArt)

`http://schemas.microsoft.com/office/drawing/2010/diagram`

- Source: [MS-ODRAWXML] v20261006 §5.16
- Office version: 2010
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 2 | 6 | 0 | 1 | 0 | 0 | 3 | 0 |

Full detail (content models, attribute types, defaults): [dgm14.json](dgm14.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`dgm14:cNvPr`](#dgm14cnvpr) ◆ | `a:CT_NonVisualDrawingProps` | 5 | 3 | 0 | MS-ODRAWXML §2.9.1.1 p.136 |
| [`dgm14:recolorImg`](#dgm14recolorimg) ◆ | `dgm14:CT_Boolean` | 1 | 0 | 0 | MS-ODRAWXML §2.9.1.2 p.136 |

◆ declared globally (can be the root element of a part)

## Element details

### dgm14:cNvPr

- Type: `a:CT_NonVisualDrawingProps`
- Attributes: `descr`, `hidden`, `id`!, `name`!, `title`
- Children: `a:hlinkClick`, `a:hlinkHover`, `a:extLst`
- Specification: MS-ODRAWXML §2.9.1.1 p.136

### dgm14:recolorImg

- Type: `dgm14:CT_Boolean`
- Attributes: `val`
- Specification: MS-ODRAWXML §2.9.1.2 p.136

`!` marks required attributes.
