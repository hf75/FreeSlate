# wpg: WordprocessingML (DOCX)

`http://schemas.microsoft.com/office/word/2010/wordprocessingGroup`

- Source: [MS-ODRAWXML] v20261006 §5.4
- Office version: 2010
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 9 | 9 | 0 | 2 | 0 | 0 | 3 | 0 |

Full detail (content models, attribute types, defaults): [wpg.json](wpg.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`wpg:cNvFrPr`](#wpgcnvfrpr) | `a:CT_NonVisualGraphicFrameProperties` | 0 | 2 | 2 |  |
| [`wpg:cNvGrpSpPr`](#wpgcnvgrpsppr) | `a:CT_NonVisualGroupDrawingShapeProps` | 0 | 2 | 2 |  |
| [`wpg:cNvPr`](#wpgcnvpr) | `a:CT_NonVisualDrawingProps` | 5 | 3 | 4 |  |
| [`wpg:extLst`](#wpgextlst) | `a:CT_OfficeArtExtensionList` | 0 | 1 | 4 |  |
| [`wpg:graphicFrame`](#wpggraphicframe) | `wpg:CT_GraphicFrame` | 0 | 5 | 2 | MS-ODRAWXML §2.16.3.1 p.163 |
| [`wpg:grpSp`](#wpggrpsp) | `wpg:CT_WordprocessingGroup` | 0 | 9 | 2 | MS-ODRAWXML §2.16.3.2 p.164 |
| [`wpg:grpSpPr`](#wpggrpsppr) | `a:CT_GroupShapeProperties` | 1 | 11 | 2 |  |
| [`wpg:wgp`](#wpgwgp) ◆ | `wpg:CT_WordprocessingGroup` | 0 | 9 | 1 | MS-ODRAWXML §2.16.1.1 p.163 |
| [`wpg:xfrm`](#wpgxfrm) | `a:CT_Transform2D` | 3 | 2 | 2 |  |

◆ declared globally (can be the root element of a part)

## Element details

### wpg:cNvFrPr

- Type: `a:CT_NonVisualGraphicFrameProperties`
- Children: `a:graphicFrameLocks`, `a:extLst`
- Parents: `wpc:graphicFrame`, `wpg:graphicFrame`

### wpg:cNvGrpSpPr

- Type: `a:CT_NonVisualGroupDrawingShapeProps`
- Children: `a:grpSpLocks`, `a:extLst`
- Parents: `wpg:grpSp`, `wpg:wgp`

### wpg:cNvPr

- Type: `a:CT_NonVisualDrawingProps`
- Attributes: `descr`, `hidden`, `id`!, `name`!, `title`
- Children: `a:hlinkClick`, `a:hlinkHover`, `a:extLst`
- Parents: `wpc:graphicFrame`, `wpg:graphicFrame`, `wpg:grpSp`, `wpg:wgp`

### wpg:extLst

- Type: `a:CT_OfficeArtExtensionList`
- Children: `a:ext`
- Parents: `wpc:graphicFrame`, `wpg:graphicFrame`, `wpg:grpSp`, `wpg:wgp`

### wpg:graphicFrame

- Type: `wpg:CT_GraphicFrame`
- Children: `wpg:cNvPr`, `wpg:cNvFrPr`, `wpg:xfrm`, `a:graphic`, `wpg:extLst`
- Parents: `wpg:grpSp`, `wpg:wgp`
- Specification: MS-ODRAWXML §2.16.3.1 p.163

### wpg:grpSp

- Type: `wpg:CT_WordprocessingGroup`
- Children: `wpg:cNvPr`, `wpg:cNvGrpSpPr`, `wpg:grpSpPr`, `wps:wsp`, `wpg:grpSp`, `wpg:graphicFrame`, `pic:pic`, `w14:contentPart`, `wpg:extLst`
- Parents: `wpg:grpSp`, `wpg:wgp`
- Specification: MS-ODRAWXML §2.16.3.2 p.164

### wpg:grpSpPr

- Type: `a:CT_GroupShapeProperties`
- Attributes: `bwMode`
- Children: `a:xfrm`, `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`, `a:effectLst`, `a:effectDag`, `a:scene3d`, `a:extLst`
- Parents: `wpg:grpSp`, `wpg:wgp`

### wpg:wgp

- Type: `wpg:CT_WordprocessingGroup`
- Children: `wpg:cNvPr`, `wpg:cNvGrpSpPr`, `wpg:grpSpPr`, `wps:wsp`, `wpg:grpSp`, `wpg:graphicFrame`, `pic:pic`, `w14:contentPart`, `wpg:extLst`
- Parents: `wpc:wpc`
- Specification: MS-ODRAWXML §2.16.1.1 p.163

### wpg:xfrm

- Type: `a:CT_Transform2D`
- Attributes: `flipH`, `flipV`, `rot`
- Children: `a:off`, `a:ext`
- Parents: `wpc:graphicFrame`, `wpg:graphicFrame`

`!` marks required attributes.
