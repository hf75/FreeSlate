# wps: WordprocessingML (DOCX)

`http://schemas.microsoft.com/office/word/2010/wordprocessingShape`

- Source: [MS-ODRAWXML] v20261006 §5.2
- Office version: 2010
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 10 | 30 | 0 | 3 | 0 | 0 | 4 | 0 |

Full detail (content models, attribute types, defaults): [wps.json](wps.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`wps:bodyPr`](#wpsbodypr) | `a:CT_TextBodyProperties` | 19 | 8 | 1 |  |
| [`wps:cNvCnPr`](#wpscnvcnpr) | `a:CT_NonVisualConnectorProperties` | 0 | 4 | 1 |  |
| [`wps:cNvPr`](#wpscnvpr) | `a:CT_NonVisualDrawingProps` | 5 | 3 | 1 |  |
| [`wps:cNvSpPr`](#wpscnvsppr) | `a:CT_NonVisualDrawingShapeProps` | 1 | 2 | 1 |  |
| [`wps:extLst`](#wpsextlst) | `a:CT_OfficeArtExtensionList` | 0 | 1 | 3 |  |
| [`wps:linkedTxbx`](#wpslinkedtxbx) | `wps:CT_LinkedTextboxInformation` | 2 | 1 | 1 | MS-ODRAWXML §2.13.3.1 p.157 |
| [`wps:spPr`](#wpssppr) | `a:CT_ShapeProperties` | 1 | 15 | 1 |  |
| [`wps:style`](#wpsstyle) | `a:CT_ShapeStyle` | 0 | 4 | 1 |  |
| [`wps:txbx`](#wpstxbx) | `wps:CT_TextboxInfo` | 1 | 3 | 1 | MS-ODRAWXML §2.13.3.2 p.157 |
| [`wps:wsp`](#wpswsp) ◆ | `wps:CT_WordprocessingShape` | 1 | 9 | 3 | MS-ODRAWXML §2.13.1.1 p.156 |

◆ declared globally (can be the root element of a part)

## Element details

### wps:bodyPr

- Type: `a:CT_TextBodyProperties`
- Attributes: `anchor`, `anchorCtr`, `bIns`, `compatLnSpc`, `forceAA`, `fromWordArt`, `horzOverflow`, `lIns`, `numCol`, `rIns`, `rot`, `rtlCol`, `spcCol`, `spcFirstLastPara`, `tIns`, `upright`, `vert`, `vertOverflow`, `wrap`
- Children: `a:prstTxWarp`, `a:noAutofit`, `a:normAutofit`, `a:spAutoFit`, `a:scene3d`, `a:sp3d`, `a:flatTx`, `a:extLst`
- Parents: `wps:wsp`

### wps:cNvCnPr

- Type: `a:CT_NonVisualConnectorProperties`
- Children: `a:cxnSpLocks`, `a:stCxn`, `a:endCxn`, `a:extLst`
- Parents: `wps:wsp`

### wps:cNvPr

- Type: `a:CT_NonVisualDrawingProps`
- Attributes: `descr`, `hidden`, `id`!, `name`!, `title`
- Children: `a:hlinkClick`, `a:hlinkHover`, `a:extLst`
- Parents: `wps:wsp`

### wps:cNvSpPr

- Type: `a:CT_NonVisualDrawingShapeProps`
- Attributes: `txBox`
- Children: `a:spLocks`, `a:extLst`
- Parents: `wps:wsp`

### wps:extLst

- Type: `a:CT_OfficeArtExtensionList`
- Children: `a:ext`
- Parents: `wps:linkedTxbx`, `wps:txbx`, `wps:wsp`

### wps:linkedTxbx

- Type: `wps:CT_LinkedTextboxInformation`
- Attributes: `id`!, `seq`!
- Children: `wps:extLst`
- Parents: `wps:wsp`
- Specification: MS-ODRAWXML §2.13.3.1 p.157

### wps:spPr

- Type: `a:CT_ShapeProperties`
- Attributes: `bwMode`
- Children: `a:xfrm`, `a:custGeom`, `a:prstGeom`, `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`, `a:ln`, `a:effectLst`, `a:effectDag`, `a:scene3d`, `a:sp3d`, `a:extLst`
- Parents: `wps:wsp`

### wps:style

- Type: `a:CT_ShapeStyle`
- Children: `a:lnRef`, `a:fillRef`, `a:effectRef`, `a:fontRef`
- Parents: `wps:wsp`

### wps:txbx

- Type: `wps:CT_TextboxInfo`
- Attributes: `id`
- Children: `w:txbxContent`, `wne:txbxContent`, `wps:extLst`
- Parents: `wps:wsp`
- Specification: MS-ODRAWXML §2.13.3.2 p.157

### wps:wsp

- Type: `wps:CT_WordprocessingShape`
- Attributes: `normalEastAsianFlow`
- Children: `wps:cNvPr`, `wps:cNvSpPr`, `wps:cNvCnPr`, `wps:spPr`, `wps:style`, `wps:extLst`, `wps:txbx`, `wps:linkedTxbx`, `wps:bodyPr`
- Parents: `wpc:wpc`, `wpg:grpSp`, `wpg:wgp`
- Specification: MS-ODRAWXML §2.13.1.1 p.156

`!` marks required attributes.
