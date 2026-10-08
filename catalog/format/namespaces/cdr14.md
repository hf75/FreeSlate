# cdr14: Charts

`http://schemas.microsoft.com/office/drawing/2010/chartDrawing`

- Source: [MS-ODRAWXML] v20261006 §5.8
- Office version: 2010
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 7 | 13 | 0 | 3 | 0 | 0 | 5 | 0 |

Full detail (content models, attribute types, defaults): [cdr14.json](cdr14.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`cdr14:cNvContentPartPr`](#cdr14cnvcontentpartpr) | `a14:CT_NonVisualInkContentPartProperties` | 1 | 2 | 1 | MS-ODRAWXML §2.3.3.7 p.64 |
| [`cdr14:cNvPr`](#cdr14cnvpr) | `a:CT_NonVisualDrawingProps` | 5 | 3 | 1 |  |
| [`cdr14:contentPart`](#cdr14contentpart) ◆ | `cdr14:CT_ContentPart` | 2 | 4 | 0 | MS-ODRAWXML §2.5.1.1 p.93 |
| [`cdr14:extLst`](#cdr14extlst) | `a:CT_OfficeArtExtensionList` | 0 | 1 | 1 |  |
| [`cdr14:nvContentPartPr`](#cdr14nvcontentpartpr) | `cdr14:CT_ContentPartNonVisual` | 0 | 2 | 1 | MS-ODRAWXML §2.5.3.3 p.95 |
| [`cdr14:nvPr`](#cdr14nvpr) | `cdr14:CT_ApplicationNonVisualDrawingProps` | 2 | 0 | 1 | MS-ODRAWXML §2.5.3.1 p.93 |
| [`cdr14:xfrm`](#cdr14xfrm) | `a:CT_Transform2D` | 3 | 2 | 1 |  |

◆ declared globally (can be the root element of a part)

## Element details

### cdr14:cNvContentPartPr

- Type: `a14:CT_NonVisualInkContentPartProperties`
- Attributes: `isComment`
- Children: `a14:cpLocks`, `a14:extLst`
- Parents: `cdr14:nvContentPartPr`
- Specification: MS-ODRAWXML §2.3.3.7 p.64

### cdr14:cNvPr

- Type: `a:CT_NonVisualDrawingProps`
- Attributes: `descr`, `hidden`, `id`!, `name`!, `title`
- Children: `a:hlinkClick`, `a:hlinkHover`, `a:extLst`
- Parents: `cdr14:nvContentPartPr`

### cdr14:contentPart

- Type: `cdr14:CT_ContentPart`
- Attributes: `bwMode`, `r:id`!
- Children: `cdr14:nvContentPartPr`, `cdr14:nvPr`, `cdr14:xfrm`, `cdr14:extLst`
- Specification: MS-ODRAWXML §2.5.1.1 p.93

### cdr14:extLst

- Type: `a:CT_OfficeArtExtensionList`
- Children: `a:ext`
- Parents: `cdr14:contentPart`

### cdr14:nvContentPartPr

- Type: `cdr14:CT_ContentPartNonVisual`
- Children: `cdr14:cNvPr`, `cdr14:cNvContentPartPr`
- Parents: `cdr14:contentPart`
- Specification: MS-ODRAWXML §2.5.3.3 p.95

### cdr14:nvPr

- Type: `cdr14:CT_ApplicationNonVisualDrawingProps`
- Attributes: `fPublished`, `macro`
- Parents: `cdr14:contentPart`
- Specification: MS-ODRAWXML §2.5.3.1 p.93

### cdr14:xfrm

- Type: `a:CT_Transform2D`
- Attributes: `flipH`, `flipV`, `rot`
- Children: `a:off`, `a:ext`
- Parents: `cdr14:contentPart`

`!` marks required attributes.
