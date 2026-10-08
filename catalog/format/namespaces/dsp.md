# dsp: Diagrams (SmartArt)

`http://schemas.microsoft.com/office/drawing/2008/diagram`

- Source: [MS-ODRAWXML] v20261006 §5.6
- Office version: 2010
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 16 | 14 | 0 | 6 | 0 | 0 | 8 | 0 |

Full detail (content models, attribute types, defaults): [dsp.json](dsp.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`dsp:cNvGrpSpPr`](#dspcnvgrpsppr) | `a:CT_NonVisualGroupDrawingShapeProps` | 0 | 2 | 1 |  |
| [`dsp:cNvPr`](#dspcnvpr) | `a:CT_NonVisualDrawingProps` | 5 | 3 | 2 |  |
| [`dsp:cNvSpPr`](#dspcnvsppr) | `a:CT_NonVisualDrawingShapeProps` | 1 | 2 | 1 |  |
| [`dsp:dataModelExt`](#dspdatamodelext) ◆ | `dsp:CT_DataModelExtBlock` | 2 | 0 | 0 | MS-ODRAWXML §2.10.1.1 p.137 |
| [`dsp:drawing`](#dspdrawing) ◆ | `dsp:CT_Drawing` | 0 | 1 | 0 | MS-ODRAWXML §2.10.1.2 p.138 |
| [`dsp:extLst`](#dspextlst) | `a:CT_OfficeArtExtensionList` | 0 | 1 | 3 |  |
| [`dsp:grpSp`](#dspgrpsp) | `dsp:CT_GroupShape` | 0 | 5 | 2 | MS-ODRAWXML §2.10.3.3 p.139 |
| [`dsp:grpSpPr`](#dspgrpsppr) | `a:CT_GroupShapeProperties` | 1 | 11 | 2 |  |
| [`dsp:nvGrpSpPr`](#dspnvgrpsppr) | `dsp:CT_GroupShapeNonVisual` | 0 | 2 | 2 | MS-ODRAWXML §2.10.3.4 p.140 |
| [`dsp:nvSpPr`](#dspnvsppr) | `dsp:CT_ShapeNonVisual` | 0 | 2 | 1 | MS-ODRAWXML §2.10.3.6 p.141 |
| [`dsp:sp`](#dspsp) | `dsp:CT_Shape` | 1 | 6 | 2 | MS-ODRAWXML §2.10.3.5 p.140 |
| [`dsp:spPr`](#dspsppr) | `a:CT_ShapeProperties` | 1 | 15 | 1 |  |
| [`dsp:spTree`](#dspsptree) | `dsp:CT_GroupShape` | 0 | 5 | 1 | MS-ODRAWXML §2.10.3.3 p.139 |
| [`dsp:style`](#dspstyle) | `a:CT_ShapeStyle` | 0 | 4 | 1 |  |
| [`dsp:txBody`](#dsptxbody) | `a:CT_TextBody` | 0 | 3 | 1 |  |
| [`dsp:txXfrm`](#dsptxxfrm) | `a:CT_Transform2D` | 3 | 2 | 1 |  |

◆ declared globally (can be the root element of a part)

## Element details

### dsp:cNvGrpSpPr

- Type: `a:CT_NonVisualGroupDrawingShapeProps`
- Children: `a:grpSpLocks`, `a:extLst`
- Parents: `dsp:nvGrpSpPr`

### dsp:cNvPr

- Type: `a:CT_NonVisualDrawingProps`
- Attributes: `descr`, `hidden`, `id`!, `name`!, `title`
- Children: `a:hlinkClick`, `a:hlinkHover`, `a:extLst`
- Parents: `dsp:nvGrpSpPr`, `dsp:nvSpPr`

### dsp:cNvSpPr

- Type: `a:CT_NonVisualDrawingShapeProps`
- Attributes: `txBox`
- Children: `a:spLocks`, `a:extLst`
- Parents: `dsp:nvSpPr`

### dsp:dataModelExt

- Type: `dsp:CT_DataModelExtBlock`
- Attributes: `minVer`, `relId`
- Specification: MS-ODRAWXML §2.10.1.1 p.137

### dsp:drawing

- Type: `dsp:CT_Drawing`
- Children: `dsp:spTree`
- Specification: MS-ODRAWXML §2.10.1.2 p.138

### dsp:extLst

- Type: `a:CT_OfficeArtExtensionList`
- Children: `a:ext`
- Parents: `dsp:grpSp`, `dsp:sp`, `dsp:spTree`

### dsp:grpSp

- Type: `dsp:CT_GroupShape`
- Children: `dsp:nvGrpSpPr`, `dsp:grpSpPr`, `dsp:sp`, `dsp:grpSp`, `dsp:extLst`
- Parents: `dsp:grpSp`, `dsp:spTree`
- Specification: MS-ODRAWXML §2.10.3.3 p.139

### dsp:grpSpPr

- Type: `a:CT_GroupShapeProperties`
- Attributes: `bwMode`
- Children: `a:xfrm`, `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`, `a:effectLst`, `a:effectDag`, `a:scene3d`, `a:extLst`
- Parents: `dsp:grpSp`, `dsp:spTree`

### dsp:nvGrpSpPr

- Type: `dsp:CT_GroupShapeNonVisual`
- Children: `dsp:cNvPr`, `dsp:cNvGrpSpPr`
- Parents: `dsp:grpSp`, `dsp:spTree`
- Specification: MS-ODRAWXML §2.10.3.4 p.140

### dsp:nvSpPr

- Type: `dsp:CT_ShapeNonVisual`
- Children: `dsp:cNvPr`, `dsp:cNvSpPr`
- Parents: `dsp:sp`
- Specification: MS-ODRAWXML §2.10.3.6 p.141

### dsp:sp

- Type: `dsp:CT_Shape`
- Attributes: `modelId`!
- Children: `dsp:nvSpPr`, `dsp:spPr`, `dsp:style`, `dsp:txBody`, `dsp:txXfrm`, `dsp:extLst`
- Parents: `dsp:grpSp`, `dsp:spTree`
- Specification: MS-ODRAWXML §2.10.3.5 p.140

### dsp:spPr

- Type: `a:CT_ShapeProperties`
- Attributes: `bwMode`
- Children: `a:xfrm`, `a:custGeom`, `a:prstGeom`, `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`, `a:ln`, `a:effectLst`, `a:effectDag`, `a:scene3d`, `a:sp3d`, `a:extLst`
- Parents: `dsp:sp`

### dsp:spTree

- Type: `dsp:CT_GroupShape`
- Children: `dsp:nvGrpSpPr`, `dsp:grpSpPr`, `dsp:sp`, `dsp:grpSp`, `dsp:extLst`
- Parents: `dsp:drawing`
- Specification: MS-ODRAWXML §2.10.3.3 p.139

### dsp:style

- Type: `a:CT_ShapeStyle`
- Children: `a:lnRef`, `a:fillRef`, `a:effectRef`, `a:fontRef`
- Parents: `dsp:sp`

### dsp:txBody

- Type: `a:CT_TextBody`
- Children: `a:bodyPr`, `a:lstStyle`, `a:p`
- Parents: `dsp:sp`

### dsp:txXfrm

- Type: `a:CT_Transform2D`
- Attributes: `flipH`, `flipV`, `rot`
- Children: `a:off`, `a:ext`
- Parents: `dsp:sp`

`!` marks required attributes.
