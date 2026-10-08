# ask: DrawingML

`http://schemas.microsoft.com/office/drawing/2018/sketchyshapes`

- Source: [MS-ODRAWXML] v20261006 §5.36
- Office version: 2021
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 8 | 1 | 0 | 3 | 1 | 0 | 5 | 0 |

Full detail (content models, attribute types, defaults): [ask.json](ask.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`ask:extLst`](#askextlst) | `a:CT_OfficeArtExtensionList` | 0 | 1 | 1 |  |
| [`ask:lineSketchCurved`](#asklinesketchcurved) | `ask:CT_Empty` | 0 | 0 | 1 | MS-ODRAWXML §2.38.3.1 p.320 |
| [`ask:lineSketchFreehand`](#asklinesketchfreehand) | `ask:CT_Empty` | 0 | 0 | 1 | MS-ODRAWXML §2.38.3.1 p.320 |
| [`ask:lineSketchNone`](#asklinesketchnone) | `ask:CT_Empty` | 0 | 0 | 1 | MS-ODRAWXML §2.38.3.1 p.320 |
| [`ask:lineSketchScribble`](#asklinesketchscribble) | `ask:CT_Empty` | 0 | 0 | 1 | MS-ODRAWXML §2.38.3.1 p.320 |
| [`ask:lineSketchStyleProps`](#asklinesketchstyleprops) ◆ | `ask:CT_LineSketchStyleProperties` | 1 | 5 | 0 | MS-ODRAWXML §2.38.1.1 p.320 |
| [`ask:seed`](#askseed) | `ask:ST_LineSketchSeed` | 0 | 0 | 1 |  |
| [`ask:type`](#asktype) | `ask:CT_LineSketchTypeProperties` | 0 | 4 | 1 | MS-ODRAWXML §2.38.3.3 p.322 |

◆ declared globally (can be the root element of a part)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `ask:ST_LineSketchSeed` | restriction of xsd:unsignedInt |  | MS-ODRAWXML §2.38.4.1 p.322 |

## Element details

### ask:extLst

- Type: `a:CT_OfficeArtExtensionList`
- Children: `a:ext`
- Parents: `ask:lineSketchStyleProps`

### ask:lineSketchCurved

- Type: `ask:CT_Empty`
- Parents: `ask:type`
- Specification: MS-ODRAWXML §2.38.3.1 p.320

### ask:lineSketchFreehand

- Type: `ask:CT_Empty`
- Parents: `ask:type`
- Specification: MS-ODRAWXML §2.38.3.1 p.320

### ask:lineSketchNone

- Type: `ask:CT_Empty`
- Parents: `ask:type`
- Specification: MS-ODRAWXML §2.38.3.1 p.320

### ask:lineSketchScribble

- Type: `ask:CT_Empty`
- Parents: `ask:type`
- Specification: MS-ODRAWXML §2.38.3.1 p.320

### ask:lineSketchStyleProps

- Type: `ask:CT_LineSketchStyleProperties`
- Attributes: `sd`
- Children: `a:custGeom`, `a:prstGeom`, `ask:type`, `ask:seed`, `ask:extLst`
- Specification: MS-ODRAWXML §2.38.1.1 p.320

### ask:seed

- Type: `ask:ST_LineSketchSeed` (text: `ask:ST_LineSketchSeed`)
- Parents: `ask:lineSketchStyleProps`

### ask:type

- Type: `ask:CT_LineSketchTypeProperties`
- Children: `ask:lineSketchNone`, `ask:lineSketchCurved`, `ask:lineSketchFreehand`, `ask:lineSketchScribble`
- Parents: `ask:lineSketchStyleProps`
- Specification: MS-ODRAWXML §2.38.3.3 p.322

`!` marks required attributes.
