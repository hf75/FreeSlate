# cdr: Charts

`http://schemas.openxmlformats.org/drawingml/2006/chartDrawing`

- Source: ECMA-376-4 transitional schema dml-chartDrawing.xsd
- Office version: 2007
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 29 | 26 | 0 | 14 | 1 | 0 | 30 | 15 |

Full detail (content models, attribute types, defaults): [cdr.json](cdr.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`cdr:absSizeAnchor`](#cdrabssizeanchor) | `cdr:CT_AbsSizeAnchor` | 0 | 7 | 1 | §21.3.2.1 p.3484, OI 2.1.1600 |
| [`cdr:blipFill`](#cdrblipfill) | `a:CT_BlipFillProperties` | 2 | 4 | 1 | §21.3.2.2 p.3484, OI 2.1.1601 |
| [`cdr:cNvCxnSpPr`](#cdrcnvcxnsppr) | `a:CT_NonVisualConnectorProperties` | 0 | 4 | 1 | §21.3.2.3 p.3487 |
| [`cdr:cNvGraphicFramePr`](#cdrcnvgraphicframepr) | `a:CT_NonVisualGraphicFrameProperties` | 0 | 2 | 1 | §21.3.2.4 p.3487 |
| [`cdr:cNvGrpSpPr`](#cdrcnvgrpsppr) | `a:CT_NonVisualGroupDrawingShapeProps` | 0 | 2 | 1 | §21.3.2.5 p.3487 |
| [`cdr:cNvPicPr`](#cdrcnvpicpr) | `a:CT_NonVisualPictureProperties` | 1 | 2 | 1 | §21.3.2.6 p.3487 |
| [`cdr:cNvPr`](#cdrcnvpr) | `a:CT_NonVisualDrawingProps` | 5 | 3 | 5 | §21.3.2.7 p.3488, OI 2.1.1602 |
| [`cdr:cNvSpPr`](#cdrcnvsppr) | `a:CT_NonVisualDrawingShapeProps` | 1 | 2 | 1 | §21.3.2.8 p.3490 |
| [`cdr:cxnSp`](#cdrcxnsp) | `cdr:CT_Connector` | 2 | 3 | 3 | §21.3.2.9 p.3491, OI 2.1.1603 |
| [`cdr:ext`](#cdrext) | `a:CT_PositiveSize2D` | 2 | 0 | 1 | §21.3.2.10 p.3493, OI 2.1.1604 |
| [`cdr:from`](#cdrfrom) | `cdr:CT_Marker` | 0 | 2 | 2 | §21.3.2.11 p.3493 |
| [`cdr:graphicFrame`](#cdrgraphicframe) | `cdr:CT_GraphicFrame` | 2 | 3 | 3 | §21.3.2.12 p.3494, OI 2.1.1605 |
| [`cdr:grpSp`](#cdrgrpsp) | `cdr:CT_GroupShape` | 0 | 7 | 3 | §21.3.2.13 p.3495 |
| [`cdr:grpSpPr`](#cdrgrpsppr) | `a:CT_GroupShapeProperties` | 1 | 11 | 1 | §21.3.2.14 p.3496, OI 2.1.1606 |
| [`cdr:nvCxnSpPr`](#cdrnvcxnsppr) | `cdr:CT_ConnectorNonVisual` | 0 | 2 | 1 | §21.3.2.15 p.3496 |
| [`cdr:nvGraphicFramePr`](#cdrnvgraphicframepr) | `cdr:CT_GraphicFrameNonVisual` | 0 | 2 | 1 | §21.3.2.16 p.3497 |
| [`cdr:nvGrpSpPr`](#cdrnvgrpsppr) | `cdr:CT_GroupShapeNonVisual` | 0 | 2 | 1 | §21.3.2.17 p.3497 |
| [`cdr:nvPicPr`](#cdrnvpicpr) | `cdr:CT_PictureNonVisual` | 0 | 2 | 1 | §21.3.2.18 p.3497 |
| [`cdr:nvSpPr`](#cdrnvsppr) | `cdr:CT_ShapeNonVisual` | 0 | 2 | 1 | §21.3.2.19 p.3497 |
| [`cdr:pic`](#cdrpic) | `cdr:CT_Picture` | 2 | 4 | 3 | §21.3.2.20 p.3498, OI 2.1.1607 |
| [`cdr:relSizeAnchor`](#cdrrelsizeanchor) | `cdr:CT_RelSizeAnchor` | 0 | 7 | 1 | §21.3.2.21 p.3499, OI 2.1.1608 |
| [`cdr:sp`](#cdrsp) | `cdr:CT_Shape` | 4 | 4 | 3 | §21.3.2.22 p.3499, OI 2.1.1609 |
| [`cdr:spPr`](#cdrsppr) | `a:CT_ShapeProperties` | 1 | 15 | 3 | §21.3.2.23 p.3501, OI 2.1.1610 |
| [`cdr:style`](#cdrstyle) | `a:CT_ShapeStyle` | 0 | 4 | 3 | §21.3.2.24 p.3501, OI 2.1.1611 |
| [`cdr:to`](#cdrto) | `cdr:CT_Marker` | 0 | 2 | 1 | §21.3.2.25 p.3502, OI 2.1.1612 |
| [`cdr:txBody`](#cdrtxbody) | `a:CT_TextBody` | 0 | 3 | 1 | §21.3.2.26 p.3502, OI 2.1.1613 |
| [`cdr:x`](#cdrx) | `cdr:ST_MarkerCoordinate` | 0 | 0 | 2 | §21.3.2.27 p.3502 |
| [`cdr:xfrm`](#cdrxfrm) | `a:CT_Transform2D` | 3 | 2 | 1 | §21.3.2.28 p.3503, OI 2.1.1614 |
| [`cdr:y`](#cdry) | `cdr:ST_MarkerCoordinate` | 0 | 0 | 2 | §21.3.2.29 p.3504 |

◆ declared globally (can be the root element of a part)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `cdr:ST_MarkerCoordinate` | restriction of xsd:double | minInclusive `0.0`, maxInclusive `1.0` | §21.3.3.1 p.3504 |

## Element details

### cdr:absSizeAnchor

- Type: `cdr:CT_AbsSizeAnchor`
- Children: `cdr:from`, `cdr:ext`, `cdr:sp`, `cdr:grpSp`, `cdr:graphicFrame`, `cdr:cxnSp`, `cdr:pic`
- Parents: `c:userShapes`
- Specification: §21.3.2.1 p.3484, OI 2.1.1600

### cdr:blipFill

- Type: `a:CT_BlipFillProperties`
- Attributes: `dpi`, `rotWithShape`
- Children: `a:blip`, `a:srcRect`, `a:tile`, `a:stretch`
- Parents: `cdr:pic`
- Specification: §21.3.2.2 p.3484, OI 2.1.1601

### cdr:cNvCxnSpPr

- Type: `a:CT_NonVisualConnectorProperties`
- Children: `a:cxnSpLocks`, `a:stCxn`, `a:endCxn`, `a:extLst`
- Parents: `cdr:nvCxnSpPr`
- Specification: §21.3.2.3 p.3487

### cdr:cNvGraphicFramePr

- Type: `a:CT_NonVisualGraphicFrameProperties`
- Children: `a:graphicFrameLocks`, `a:extLst`
- Parents: `cdr:nvGraphicFramePr`
- Specification: §21.3.2.4 p.3487

### cdr:cNvGrpSpPr

- Type: `a:CT_NonVisualGroupDrawingShapeProps`
- Children: `a:grpSpLocks`, `a:extLst`
- Parents: `cdr:nvGrpSpPr`
- Specification: §21.3.2.5 p.3487

### cdr:cNvPicPr

- Type: `a:CT_NonVisualPictureProperties`
- Attributes: `preferRelativeResize`
- Children: `a:picLocks`, `a:extLst`
- Parents: `cdr:nvPicPr`
- Specification: §21.3.2.6 p.3487

### cdr:cNvPr

- Type: `a:CT_NonVisualDrawingProps`
- Attributes: `descr`, `hidden`, `id`!, `name`!, `title`
- Children: `a:hlinkClick`, `a:hlinkHover`, `a:extLst`
- Parents: `cdr:nvCxnSpPr`, `cdr:nvGraphicFramePr`, `cdr:nvGrpSpPr`, `cdr:nvPicPr`, `cdr:nvSpPr`
- Specification: §21.3.2.7 p.3488, OI 2.1.1602

### cdr:cNvSpPr

- Type: `a:CT_NonVisualDrawingShapeProps`
- Attributes: `txBox`
- Children: `a:spLocks`, `a:extLst`
- Parents: `cdr:nvSpPr`
- Specification: §21.3.2.8 p.3490

### cdr:cxnSp

- Type: `cdr:CT_Connector`
- Attributes: `fPublished`, `macro`
- Children: `cdr:nvCxnSpPr`, `cdr:spPr`, `cdr:style`
- Parents: `cdr:absSizeAnchor`, `cdr:grpSp`, `cdr:relSizeAnchor`
- Specification: §21.3.2.9 p.3491, OI 2.1.1603

### cdr:ext

- Type: `a:CT_PositiveSize2D`
- Attributes: `cx`!, `cy`!
- Parents: `cdr:absSizeAnchor`
- Specification: §21.3.2.10 p.3493, OI 2.1.1604

### cdr:from

- Type: `cdr:CT_Marker`
- Children: `cdr:x`, `cdr:y`
- Parents: `cdr:absSizeAnchor`, `cdr:relSizeAnchor`
- Specification: §21.3.2.11 p.3493

### cdr:graphicFrame

- Type: `cdr:CT_GraphicFrame`
- Attributes: `fPublished`, `macro`
- Children: `cdr:nvGraphicFramePr`, `cdr:xfrm`, `a:graphic`
- Parents: `cdr:absSizeAnchor`, `cdr:grpSp`, `cdr:relSizeAnchor`
- Specification: §21.3.2.12 p.3494, OI 2.1.1605

### cdr:grpSp

- Type: `cdr:CT_GroupShape`
- Children: `cdr:nvGrpSpPr`, `cdr:grpSpPr`, `cdr:sp`, `cdr:grpSp`, `cdr:graphicFrame`, `cdr:cxnSp`, `cdr:pic`
- Parents: `cdr:absSizeAnchor`, `cdr:grpSp`, `cdr:relSizeAnchor`
- Specification: §21.3.2.13 p.3495

### cdr:grpSpPr

- Type: `a:CT_GroupShapeProperties`
- Attributes: `bwMode`
- Children: `a:xfrm`, `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`, `a:effectLst`, `a:effectDag`, `a:scene3d`, `a:extLst`
- Parents: `cdr:grpSp`
- Specification: §21.3.2.14 p.3496, OI 2.1.1606

### cdr:nvCxnSpPr

- Type: `cdr:CT_ConnectorNonVisual`
- Children: `cdr:cNvPr`, `cdr:cNvCxnSpPr`
- Parents: `cdr:cxnSp`
- Specification: §21.3.2.15 p.3496

### cdr:nvGraphicFramePr

- Type: `cdr:CT_GraphicFrameNonVisual`
- Children: `cdr:cNvPr`, `cdr:cNvGraphicFramePr`
- Parents: `cdr:graphicFrame`
- Specification: §21.3.2.16 p.3497

### cdr:nvGrpSpPr

- Type: `cdr:CT_GroupShapeNonVisual`
- Children: `cdr:cNvPr`, `cdr:cNvGrpSpPr`
- Parents: `cdr:grpSp`
- Specification: §21.3.2.17 p.3497

### cdr:nvPicPr

- Type: `cdr:CT_PictureNonVisual`
- Children: `cdr:cNvPr`, `cdr:cNvPicPr`
- Parents: `cdr:pic`
- Specification: §21.3.2.18 p.3497

### cdr:nvSpPr

- Type: `cdr:CT_ShapeNonVisual`
- Children: `cdr:cNvPr`, `cdr:cNvSpPr`
- Parents: `cdr:sp`
- Specification: §21.3.2.19 p.3497

### cdr:pic

- Type: `cdr:CT_Picture`
- Attributes: `fPublished`, `macro`
- Children: `cdr:nvPicPr`, `cdr:blipFill`, `cdr:spPr`, `cdr:style`
- Parents: `cdr:absSizeAnchor`, `cdr:grpSp`, `cdr:relSizeAnchor`
- Specification: §21.3.2.20 p.3498, OI 2.1.1607

### cdr:relSizeAnchor

- Type: `cdr:CT_RelSizeAnchor`
- Children: `cdr:from`, `cdr:to`, `cdr:sp`, `cdr:grpSp`, `cdr:graphicFrame`, `cdr:cxnSp`, `cdr:pic`
- Parents: `c:userShapes`
- Specification: §21.3.2.21 p.3499, OI 2.1.1608

### cdr:sp

- Type: `cdr:CT_Shape`
- Attributes: `fLocksText`, `fPublished`, `macro`, `textlink`
- Children: `cdr:nvSpPr`, `cdr:spPr`, `cdr:style`, `cdr:txBody`
- Parents: `cdr:absSizeAnchor`, `cdr:grpSp`, `cdr:relSizeAnchor`
- Specification: §21.3.2.22 p.3499, OI 2.1.1609

### cdr:spPr

- Type: `a:CT_ShapeProperties`
- Attributes: `bwMode`
- Children: `a:xfrm`, `a:custGeom`, `a:prstGeom`, `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`, `a:ln`, `a:effectLst`, `a:effectDag`, `a:scene3d`, `a:sp3d`, `a:extLst`
- Parents: `cdr:cxnSp`, `cdr:pic`, `cdr:sp`
- Specification: §21.3.2.23 p.3501, OI 2.1.1610

### cdr:style

- Type: `a:CT_ShapeStyle`
- Children: `a:lnRef`, `a:fillRef`, `a:effectRef`, `a:fontRef`
- Parents: `cdr:cxnSp`, `cdr:pic`, `cdr:sp`
- Specification: §21.3.2.24 p.3501, OI 2.1.1611

### cdr:to

- Type: `cdr:CT_Marker`
- Children: `cdr:x`, `cdr:y`
- Parents: `cdr:relSizeAnchor`
- Specification: §21.3.2.25 p.3502, OI 2.1.1612

### cdr:txBody

- Type: `a:CT_TextBody`
- Children: `a:bodyPr`, `a:lstStyle`, `a:p`
- Parents: `cdr:sp`
- Specification: §21.3.2.26 p.3502, OI 2.1.1613

### cdr:x

- Type: `cdr:ST_MarkerCoordinate` (text: `cdr:ST_MarkerCoordinate`)
- Parents: `cdr:from`, `cdr:to`
- Specification: §21.3.2.27 p.3502

### cdr:xfrm

- Type: `a:CT_Transform2D`
- Attributes: `flipH`, `flipV`, `rot`
- Children: `a:off`, `a:ext`
- Parents: `cdr:graphicFrame`
- Specification: §21.3.2.28 p.3503, OI 2.1.1614

### cdr:y

- Type: `cdr:ST_MarkerCoordinate` (text: `cdr:ST_MarkerCoordinate`)
- Parents: `cdr:from`, `cdr:to`
- Specification: §21.3.2.29 p.3504

`!` marks required attributes.
