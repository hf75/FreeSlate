# xdr14: SpreadsheetML (out of scope)

`http://schemas.microsoft.com/office/excel/2010/spreadsheetDrawing`

- Source: [MS-ODRAWXML] v20261006 §5.9
- Office version: 2010
- In scope: no

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 7 | 13 | 0 | 3 | 0 | 0 | 5 | 0 |

Full detail (content models, attribute types, defaults): [xdr14.json](xdr14.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`xdr14:cNvContentPartPr`](#xdr14cnvcontentpartpr) | `a14:CT_NonVisualInkContentPartProperties` | 1 | 2 | 1 | MS-ODRAWXML §2.3.3.7 p.64 |
| [`xdr14:cNvPr`](#xdr14cnvpr) | `a:CT_NonVisualDrawingProps` | 5 | 3 | 1 |  |
| [`xdr14:contentPart`](#xdr14contentpart) ◆ | `xdr14:CT_ContentPart` | 2 | 4 | 0 | MS-ODRAWXML §2.20.1.1 p.173 |
| [`xdr14:extLst`](#xdr14extlst) | `a:CT_OfficeArtExtensionList` | 0 | 1 | 1 |  |
| [`xdr14:nvContentPartPr`](#xdr14nvcontentpartpr) | `xdr14:CT_ContentPartNonVisual` | 0 | 2 | 1 | MS-ODRAWXML §2.20.3.3 p.175 |
| [`xdr14:nvPr`](#xdr14nvpr) | `xdr14:CT_ApplicationNonVisualDrawingProps` | 2 | 0 | 1 | MS-ODRAWXML §2.20.3.1 p.173 |
| [`xdr14:xfrm`](#xdr14xfrm) | `a:CT_Transform2D` | 3 | 2 | 1 |  |

◆ declared globally (can be the root element of a part)

## Element details

### xdr14:cNvContentPartPr

- Type: `a14:CT_NonVisualInkContentPartProperties`
- Attributes: `isComment`
- Children: `a14:cpLocks`, `a14:extLst`
- Parents: `xdr14:nvContentPartPr`
- Specification: MS-ODRAWXML §2.3.3.7 p.64

### xdr14:cNvPr

- Type: `a:CT_NonVisualDrawingProps`
- Attributes: `descr`, `hidden`, `id`!, `name`!, `title`
- Children: `a:hlinkClick`, `a:hlinkHover`, `a:extLst`
- Parents: `xdr14:nvContentPartPr`

### xdr14:contentPart

- Type: `xdr14:CT_ContentPart`
- Attributes: `bwMode`, `r:id`!
- Children: `xdr14:nvContentPartPr`, `xdr14:nvPr`, `xdr14:xfrm`, `xdr14:extLst`
- Specification: MS-ODRAWXML §2.20.1.1 p.173

### xdr14:extLst

- Type: `a:CT_OfficeArtExtensionList`
- Children: `a:ext`
- Parents: `xdr14:contentPart`

### xdr14:nvContentPartPr

- Type: `xdr14:CT_ContentPartNonVisual`
- Children: `xdr14:cNvPr`, `xdr14:cNvContentPartPr`
- Parents: `xdr14:contentPart`
- Specification: MS-ODRAWXML §2.20.3.3 p.175

### xdr14:nvPr

- Type: `xdr14:CT_ApplicationNonVisualDrawingProps`
- Attributes: `fPublished`, `macro`
- Parents: `xdr14:contentPart`
- Specification: MS-ODRAWXML §2.20.3.1 p.173

### xdr14:xfrm

- Type: `a:CT_Transform2D`
- Attributes: `flipH`, `flipV`, `rot`
- Children: `a:off`, `a:ext`
- Parents: `xdr14:contentPart`

`!` marks required attributes.
