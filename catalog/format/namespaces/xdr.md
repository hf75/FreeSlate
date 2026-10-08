# xdr: SpreadsheetML (out of scope)

`http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing`

- Source: ECMA-376-4 transitional schema dml-spreadsheetDrawing.xsd
- Office version: 2007
- In scope: no

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 36 | 32 | 0 | 17 | 3 | 3 | 39 | 20 |

Full detail (content models, attribute types, defaults): [xdr.json](xdr.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`xdr:absoluteAnchor`](#xdrabsoluteanchor) | `xdr:CT_AbsoluteAnchor` | 0 | 9 | 1 | §20.5.2.1 p.3167 |
| [`xdr:blipFill`](#xdrblipfill) | `a:CT_BlipFillProperties` | 2 | 4 | 1 | §20.5.2.2 p.3168, OI 2.1.1358 |
| [`xdr:cNvCxnSpPr`](#xdrcnvcxnsppr) | `a:CT_NonVisualConnectorProperties` | 0 | 4 | 1 | §20.5.2.4 p.3170 |
| [`xdr:cNvGraphicFramePr`](#xdrcnvgraphicframepr) | `a:CT_NonVisualGraphicFrameProperties` | 0 | 2 | 1 | §20.5.2.5 p.3171 |
| [`xdr:cNvGrpSpPr`](#xdrcnvgrpsppr) | `a:CT_NonVisualGroupDrawingShapeProps` | 0 | 2 | 1 | §20.5.2.6 p.3171 |
| [`xdr:cNvPicPr`](#xdrcnvpicpr) | `a:CT_NonVisualPictureProperties` | 1 | 2 | 1 | §20.5.2.7 p.3171 |
| [`xdr:cNvPr`](#xdrcnvpr) | `a:CT_NonVisualDrawingProps` | 5 | 3 | 5 | §20.5.2.8 p.3172, OI 2.1.1359 |
| [`xdr:cNvSpPr`](#xdrcnvsppr) | `a:CT_NonVisualDrawingShapeProps` | 1 | 2 | 1 | §20.5.2.9 p.3174, OI 2.1.1360 |
| [`xdr:clientData`](#xdrclientdata) | `xdr:CT_AnchorClientData` | 2 | 0 | 3 | §20.5.2.3 p.3170 |
| [`xdr:col`](#xdrcol) | `xdr:ST_ColID` | 0 | 0 | 2 | §20.5.2.10 p.3175, OI 2.1.1361 |
| [`xdr:colOff`](#xdrcoloff) | `a:ST_Coordinate` | 0 | 0 | 2 | §20.5.2.11 p.3175, OI 2.1.1362 |
| [`xdr:contentPart`](#xdrcontentpart) | `xdr:CT_Rel` | 1 | 0 | 3 | §20.5.2.12 p.3175 |
| [`xdr:cxnSp`](#xdrcxnsp) | `xdr:CT_Connector` | 2 | 3 | 4 | §20.5.2.13 p.3179, OI 2.1.1363 |
| [`xdr:ext`](#xdrext) | `a:CT_PositiveSize2D` | 2 | 0 | 2 | §20.5.2.14 p.3180, OI 2.1.1364 |
| [`xdr:from`](#xdrfrom) ◆ | `xdr:CT_Marker` | 0 | 4 | 3 | §20.5.2.15 p.3181, OI 2.1.1365 |
| [`xdr:graphicFrame`](#xdrgraphicframe) | `xdr:CT_GraphicalObjectFrame` | 2 | 3 | 4 | §20.5.2.16 p.3181, OI 2.1.1366 |
| [`xdr:grpSp`](#xdrgrpsp) | `xdr:CT_GroupShape` | 0 | 7 | 4 | §20.5.2.17 p.3182 |
| [`xdr:grpSpPr`](#xdrgrpsppr) | `a:CT_GroupShapeProperties` | 1 | 11 | 1 | §20.5.2.18 p.3183, OI 2.1.1367 |
| [`xdr:nvCxnSpPr`](#xdrnvcxnsppr) | `xdr:CT_ConnectorNonVisual` | 0 | 2 | 1 | §20.5.2.19 p.3183 |
| [`xdr:nvGraphicFramePr`](#xdrnvgraphicframepr) | `xdr:CT_GraphicalObjectFrameNonVisual` | 0 | 2 | 1 | §20.5.2.20 p.3184 |
| [`xdr:nvGrpSpPr`](#xdrnvgrpsppr) | `xdr:CT_GroupShapeNonVisual` | 0 | 2 | 1 | §20.5.2.21 p.3184 |
| [`xdr:nvPicPr`](#xdrnvpicpr) | `xdr:CT_PictureNonVisual` | 0 | 2 | 1 | §20.5.2.22 p.3184 |
| [`xdr:nvSpPr`](#xdrnvsppr) | `xdr:CT_ShapeNonVisual` | 0 | 2 | 1 | §20.5.2.23 p.3185 |
| [`xdr:oneCellAnchor`](#xdronecellanchor) | `xdr:CT_OneCellAnchor` | 0 | 9 | 1 | §20.5.2.24 p.3185 |
| [`xdr:pic`](#xdrpic) | `xdr:CT_Picture` | 2 | 4 | 4 | §20.5.2.25 p.3185, OI 2.1.1368 |
| [`xdr:pos`](#xdrpos) | `a:CT_Point2D` | 2 | 0 | 1 | §20.5.2.26 p.3186, OI 2.1.1369 |
| [`xdr:row`](#xdrrow) | `xdr:ST_RowID` | 0 | 0 | 2 | §20.5.2.27 p.3187, OI 2.1.1370 |
| [`xdr:rowOff`](#xdrrowoff) | `a:ST_Coordinate` | 0 | 0 | 2 | §20.5.2.28 p.3187, OI 2.1.1371 |
| [`xdr:sp`](#xdrsp) | `xdr:CT_Shape` | 4 | 4 | 4 | §20.5.2.29 p.3187, OI 2.1.1372 |
| [`xdr:spPr`](#xdrsppr) | `a:CT_ShapeProperties` | 1 | 15 | 3 | §20.5.2.30 p.3188, OI 2.1.1373 |
| [`xdr:style`](#xdrstyle) | `a:CT_ShapeStyle` | 0 | 4 | 3 | §20.5.2.31 p.3189, OI 2.1.1374 |
| [`xdr:to`](#xdrto) ◆ | `xdr:CT_Marker` | 0 | 4 | 2 | §20.5.2.32 p.3189, OI 2.1.1375 |
| [`xdr:twoCellAnchor`](#xdrtwocellanchor) | `xdr:CT_TwoCellAnchor` | 1 | 9 | 1 | §20.5.2.33 p.3189 |
| [`xdr:txBody`](#xdrtxbody) | `a:CT_TextBody` | 0 | 3 | 1 | §20.5.2.34 p.3190, OI 2.1.1376 |
| [`xdr:wsDr`](#xdrwsdr) ◆ | `xdr:CT_Drawing` | 0 | 3 | 0 | §20.5.2.35 p.3190 |
| [`xdr:xfrm`](#xdrxfrm) | `a:CT_Transform2D` | 3 | 2 | 1 | §20.5.2.36 p.3190, OI 2.1.1377 |

◆ declared globally (can be the root element of a part)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `xdr:ST_ColID` | restriction of xsd:int | minInclusive `0` | §20.5.3.1 p.3191 |
| `xdr:ST_EditAs` | restriction of xsd:token | `twoCell`, `oneCell`, `absolute` | §20.5.3.2 p.3192 |
| `xdr:ST_RowID` | restriction of xsd:int | minInclusive `0` | §20.5.3.3 p.3193 |

## Element details

### xdr:absoluteAnchor

- Type: `xdr:CT_AbsoluteAnchor`
- Children: `xdr:pos`, `xdr:ext`, `xdr:sp`, `xdr:grpSp`, `xdr:graphicFrame`, `xdr:cxnSp`, `xdr:pic`, `xdr:contentPart`, `xdr:clientData`
- Parents: `xdr:wsDr`
- Specification: §20.5.2.1 p.3167

### xdr:blipFill

- Type: `a:CT_BlipFillProperties`
- Attributes: `dpi`, `rotWithShape`
- Children: `a:blip`, `a:srcRect`, `a:tile`, `a:stretch`
- Parents: `xdr:pic`
- Specification: §20.5.2.2 p.3168, OI 2.1.1358

### xdr:cNvCxnSpPr

- Type: `a:CT_NonVisualConnectorProperties`
- Children: `a:cxnSpLocks`, `a:stCxn`, `a:endCxn`, `a:extLst`
- Parents: `xdr:nvCxnSpPr`
- Specification: §20.5.2.4 p.3170

### xdr:cNvGraphicFramePr

- Type: `a:CT_NonVisualGraphicFrameProperties`
- Children: `a:graphicFrameLocks`, `a:extLst`
- Parents: `xdr:nvGraphicFramePr`
- Specification: §20.5.2.5 p.3171

### xdr:cNvGrpSpPr

- Type: `a:CT_NonVisualGroupDrawingShapeProps`
- Children: `a:grpSpLocks`, `a:extLst`
- Parents: `xdr:nvGrpSpPr`
- Specification: §20.5.2.6 p.3171

### xdr:cNvPicPr

- Type: `a:CT_NonVisualPictureProperties`
- Attributes: `preferRelativeResize`
- Children: `a:picLocks`, `a:extLst`
- Parents: `xdr:nvPicPr`
- Specification: §20.5.2.7 p.3171

### xdr:cNvPr

- Type: `a:CT_NonVisualDrawingProps`
- Attributes: `descr`, `hidden`, `id`!, `name`!, `title`
- Children: `a:hlinkClick`, `a:hlinkHover`, `a:extLst`
- Parents: `xdr:nvCxnSpPr`, `xdr:nvGraphicFramePr`, `xdr:nvGrpSpPr`, `xdr:nvPicPr`, `xdr:nvSpPr`
- Specification: §20.5.2.8 p.3172, OI 2.1.1359

### xdr:cNvSpPr

- Type: `a:CT_NonVisualDrawingShapeProps`
- Attributes: `txBox`
- Children: `a:spLocks`, `a:extLst`
- Parents: `xdr:nvSpPr`
- Specification: §20.5.2.9 p.3174, OI 2.1.1360

### xdr:clientData

- Type: `xdr:CT_AnchorClientData`
- Attributes: `fLocksWithSheet`, `fPrintsWithSheet`
- Parents: `xdr:absoluteAnchor`, `xdr:oneCellAnchor`, `xdr:twoCellAnchor`
- Specification: §20.5.2.3 p.3170

### xdr:col

- Type: `xdr:ST_ColID` (text: `xdr:ST_ColID`)
- Parents: `xdr:from`, `xdr:to`
- Specification: §20.5.2.10 p.3175, OI 2.1.1361

### xdr:colOff

- Type: `a:ST_Coordinate` (text: `a:ST_Coordinate`)
- Parents: `xdr:from`, `xdr:to`
- Specification: §20.5.2.11 p.3175, OI 2.1.1362

### xdr:contentPart

- Type: `xdr:CT_Rel`
- Attributes: `r:id`!
- Parents: `xdr:absoluteAnchor`, `xdr:oneCellAnchor`, `xdr:twoCellAnchor`
- Specification: §20.5.2.12 p.3175

### xdr:cxnSp

- Type: `xdr:CT_Connector`
- Attributes: `fPublished`, `macro`
- Children: `xdr:nvCxnSpPr`, `xdr:spPr`, `xdr:style`
- Parents: `xdr:absoluteAnchor`, `xdr:grpSp`, `xdr:oneCellAnchor`, `xdr:twoCellAnchor`
- Specification: §20.5.2.13 p.3179, OI 2.1.1363

### xdr:ext

- Type: `a:CT_PositiveSize2D`
- Attributes: `cx`!, `cy`!
- Parents: `xdr:absoluteAnchor`, `xdr:oneCellAnchor`
- Specification: §20.5.2.14 p.3180, OI 2.1.1364

### xdr:from

- Type: `xdr:CT_Marker`
- Children: `xdr:col`, `xdr:colOff`, `xdr:row`, `xdr:rowOff`
- Parents: `x:anchor`, `xdr:oneCellAnchor`, `xdr:twoCellAnchor`
- Specification: §20.5.2.15 p.3181, OI 2.1.1365

### xdr:graphicFrame

- Type: `xdr:CT_GraphicalObjectFrame`
- Attributes: `fPublished`, `macro`
- Children: `xdr:nvGraphicFramePr`, `xdr:xfrm`, `a:graphic`
- Parents: `xdr:absoluteAnchor`, `xdr:grpSp`, `xdr:oneCellAnchor`, `xdr:twoCellAnchor`
- Specification: §20.5.2.16 p.3181, OI 2.1.1366

### xdr:grpSp

- Type: `xdr:CT_GroupShape`
- Children: `xdr:nvGrpSpPr`, `xdr:grpSpPr`, `xdr:sp`, `xdr:grpSp`, `xdr:graphicFrame`, `xdr:cxnSp`, `xdr:pic`
- Parents: `xdr:absoluteAnchor`, `xdr:grpSp`, `xdr:oneCellAnchor`, `xdr:twoCellAnchor`
- Specification: §20.5.2.17 p.3182

### xdr:grpSpPr

- Type: `a:CT_GroupShapeProperties`
- Attributes: `bwMode`
- Children: `a:xfrm`, `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`, `a:effectLst`, `a:effectDag`, `a:scene3d`, `a:extLst`
- Parents: `xdr:grpSp`
- Specification: §20.5.2.18 p.3183, OI 2.1.1367

### xdr:nvCxnSpPr

- Type: `xdr:CT_ConnectorNonVisual`
- Children: `xdr:cNvPr`, `xdr:cNvCxnSpPr`
- Parents: `xdr:cxnSp`
- Specification: §20.5.2.19 p.3183

### xdr:nvGraphicFramePr

- Type: `xdr:CT_GraphicalObjectFrameNonVisual`
- Children: `xdr:cNvPr`, `xdr:cNvGraphicFramePr`
- Parents: `xdr:graphicFrame`
- Specification: §20.5.2.20 p.3184

### xdr:nvGrpSpPr

- Type: `xdr:CT_GroupShapeNonVisual`
- Children: `xdr:cNvPr`, `xdr:cNvGrpSpPr`
- Parents: `xdr:grpSp`
- Specification: §20.5.2.21 p.3184

### xdr:nvPicPr

- Type: `xdr:CT_PictureNonVisual`
- Children: `xdr:cNvPr`, `xdr:cNvPicPr`
- Parents: `xdr:pic`
- Specification: §20.5.2.22 p.3184

### xdr:nvSpPr

- Type: `xdr:CT_ShapeNonVisual`
- Children: `xdr:cNvPr`, `xdr:cNvSpPr`
- Parents: `xdr:sp`
- Specification: §20.5.2.23 p.3185

### xdr:oneCellAnchor

- Type: `xdr:CT_OneCellAnchor`
- Children: `xdr:from`, `xdr:ext`, `xdr:sp`, `xdr:grpSp`, `xdr:graphicFrame`, `xdr:cxnSp`, `xdr:pic`, `xdr:contentPart`, `xdr:clientData`
- Parents: `xdr:wsDr`
- Specification: §20.5.2.24 p.3185

### xdr:pic

- Type: `xdr:CT_Picture`
- Attributes: `fPublished`, `macro`
- Children: `xdr:nvPicPr`, `xdr:blipFill`, `xdr:spPr`, `xdr:style`
- Parents: `xdr:absoluteAnchor`, `xdr:grpSp`, `xdr:oneCellAnchor`, `xdr:twoCellAnchor`
- Specification: §20.5.2.25 p.3185, OI 2.1.1368

### xdr:pos

- Type: `a:CT_Point2D`
- Attributes: `x`!, `y`!
- Parents: `xdr:absoluteAnchor`
- Specification: §20.5.2.26 p.3186, OI 2.1.1369

### xdr:row

- Type: `xdr:ST_RowID` (text: `xdr:ST_RowID`)
- Parents: `xdr:from`, `xdr:to`
- Specification: §20.5.2.27 p.3187, OI 2.1.1370

### xdr:rowOff

- Type: `a:ST_Coordinate` (text: `a:ST_Coordinate`)
- Parents: `xdr:from`, `xdr:to`
- Specification: §20.5.2.28 p.3187, OI 2.1.1371

### xdr:sp

- Type: `xdr:CT_Shape`
- Attributes: `fLocksText`, `fPublished`, `macro`, `textlink`
- Children: `xdr:nvSpPr`, `xdr:spPr`, `xdr:style`, `xdr:txBody`
- Parents: `xdr:absoluteAnchor`, `xdr:grpSp`, `xdr:oneCellAnchor`, `xdr:twoCellAnchor`
- Specification: §20.5.2.29 p.3187, OI 2.1.1372

### xdr:spPr

- Type: `a:CT_ShapeProperties`
- Attributes: `bwMode`
- Children: `a:xfrm`, `a:custGeom`, `a:prstGeom`, `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`, `a:ln`, `a:effectLst`, `a:effectDag`, `a:scene3d`, `a:sp3d`, `a:extLst`
- Parents: `xdr:cxnSp`, `xdr:pic`, `xdr:sp`
- Specification: §20.5.2.30 p.3188, OI 2.1.1373

### xdr:style

- Type: `a:CT_ShapeStyle`
- Children: `a:lnRef`, `a:fillRef`, `a:effectRef`, `a:fontRef`
- Parents: `xdr:cxnSp`, `xdr:pic`, `xdr:sp`
- Specification: §20.5.2.31 p.3189, OI 2.1.1374

### xdr:to

- Type: `xdr:CT_Marker`
- Children: `xdr:col`, `xdr:colOff`, `xdr:row`, `xdr:rowOff`
- Parents: `x:anchor`, `xdr:twoCellAnchor`
- Specification: §20.5.2.32 p.3189, OI 2.1.1375

### xdr:twoCellAnchor

- Type: `xdr:CT_TwoCellAnchor`
- Attributes: `editAs`
- Children: `xdr:from`, `xdr:to`, `xdr:sp`, `xdr:grpSp`, `xdr:graphicFrame`, `xdr:cxnSp`, `xdr:pic`, `xdr:contentPart`, `xdr:clientData`
- Parents: `xdr:wsDr`
- Specification: §20.5.2.33 p.3189

### xdr:txBody

- Type: `a:CT_TextBody`
- Children: `a:bodyPr`, `a:lstStyle`, `a:p`
- Parents: `xdr:sp`
- Specification: §20.5.2.34 p.3190, OI 2.1.1376

### xdr:wsDr

- Type: `xdr:CT_Drawing`
- Children: `xdr:twoCellAnchor`, `xdr:oneCellAnchor`, `xdr:absoluteAnchor`
- Specification: §20.5.2.35 p.3190

### xdr:xfrm

- Type: `a:CT_Transform2D`
- Attributes: `flipH`, `flipV`, `rot`
- Children: `a:off`, `a:ext`
- Parents: `xdr:graphicFrame`
- Specification: §20.5.2.36 p.3190, OI 2.1.1377

`!` marks required attributes.
