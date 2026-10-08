# wp: WordprocessingML (DOCX)

`http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing`

- Source: ECMA-376-4 transitional schema dml-wordprocessingDrawing.xsd
- Office version: 2007
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 43 | 85 | 0 | 20 | 7 | 30 | 50 | 8 |

Full detail (content models, attribute types, defaults): [wp.json](wp.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`wp:align`](#wpalign) | `wp:ST_AlignH`, `wp:ST_AlignV` | 0 | 0 | 2 | §20.4.2.1 p.3110; §20.4.2.2 p.3111 |
| [`wp:anchor`](#wpanchor) ◆ | `wp:CT_Anchor` | 11 | 13 | 1 | §20.4.2.3 p.3112, OI 2.1.1350 |
| [`wp:bg`](#wpbg) | `a:CT_BackgroundFormatting` | 0 | 8 | 1 | §20.4.2.21 p.3147 |
| [`wp:bodyPr`](#wpbodypr) | `a:CT_TextBodyProperties` | 19 | 8 | 1 | §20.4.2.22 p.3147 |
| [`wp:cNvCnPr`](#wpcnvcnpr) | `a:CT_NonVisualConnectorProperties` | 0 | 4 | 1 | §20.4.2.23 p.3150 |
| [`wp:cNvContentPartPr`](#wpcnvcontentpartpr) | `a:CT_NonVisualContentPartProperties` | 1 | 2 | 1 | §20.4.2.24 p.3150 |
| [`wp:cNvFrPr`](#wpcnvfrpr) | `a:CT_NonVisualGraphicFrameProperties` | 0 | 2 | 1 | §20.4.2.25 p.3151 |
| [`wp:cNvGraphicFramePr`](#wpcnvgraphicframepr) | `a:CT_NonVisualGraphicFrameProperties` | 0 | 2 | 2 | §20.4.2.4 p.3119 |
| [`wp:cNvGrpSpPr`](#wpcnvgrpsppr) | `a:CT_NonVisualGroupDrawingShapeProps` | 0 | 2 | 2 | §20.4.2.26 p.3151 |
| [`wp:cNvPr`](#wpcnvpr) | `a:CT_NonVisualDrawingProps` | 5 | 3 | 5 | §20.4.2.27 p.3151 |
| [`wp:cNvSpPr`](#wpcnvsppr) | `a:CT_NonVisualDrawingShapeProps` | 1 | 2 | 1 | §20.4.2.28 p.3153 |
| [`wp:contentPart`](#wpcontentpart) | `wp:CT_WordprocessingContentPart` | 2 | 3 | 3 | §20.4.2.29 p.3154 |
| [`wp:docPr`](#wpdocpr) | `a:CT_NonVisualDrawingProps` | 5 | 3 | 2 | §20.4.2.5 p.3119, OI 2.1.1351 |
| [`wp:effectExtent`](#wpeffectextent) | `wp:CT_EffectExtent` | 4 | 0 | 4 | §20.4.2.6 p.3121, OI 2.1.1352 |
| [`wp:extLst`](#wpextlst) | `a:CT_OfficeArtExtensionList` | 0 | 1 | 8 | §20.4.2.30 p.3155 |
| [`wp:extent`](#wpextent) | `a:CT_PositiveSize2D` | 2 | 0 | 2 | §20.4.2.7 p.3125, OI 2.1.1353 |
| [`wp:graphicFrame`](#wpgraphicframe) | `wp:CT_GraphicFrame` | 0 | 5 | 3 | §20.4.2.31 p.3155 |
| [`wp:grpSp`](#wpgrpsp) | `wp:CT_WordprocessingGroup` | 0 | 9 | 2 | §20.4.2.32 p.3155 |
| [`wp:grpSpPr`](#wpgrpsppr) | `a:CT_GroupShapeProperties` | 1 | 11 | 2 | §20.4.2.33 p.3155 |
| [`wp:inline`](#wpinline) ◆ | `wp:CT_Inline` | 4 | 5 | 1 | §20.4.2.8 p.3126 |
| [`wp:lineTo`](#wplineto) | `a:CT_Point2D` | 2 | 0 | 1 | §20.4.2.9 p.3129, OI 2.1.1354 |
| [`wp:linkedTxbx`](#wplinkedtxbx) | `wp:CT_LinkedTextboxInformation` | 2 | 1 | 1 | §20.4.2.34 p.3156 |
| [`wp:nvContentPartPr`](#wpnvcontentpartpr) | `wp:CT_WordprocessingContentPartNonVisual` | 0 | 2 | 1 |  |
| [`wp:posOffset`](#wpposoffset) | `wp:ST_PositionOffset` | 0 | 0 | 2 | §20.4.2.12 p.3133 |
| [`wp:positionH`](#wppositionh) | `wp:CT_PosH` | 1 | 2 | 1 | §20.4.2.10 p.3131 |
| [`wp:positionV`](#wppositionv) | `wp:CT_PosV` | 1 | 2 | 1 | §20.4.2.11 p.3132, OI 2.1.1355 |
| [`wp:simplePos`](#wpsimplepos) | `a:CT_Point2D` | 2 | 0 | 1 | §20.4.2.13 p.3133, OI 2.1.1356 |
| [`wp:spPr`](#wpsppr) | `a:CT_ShapeProperties` | 1 | 15 | 1 | §20.4.2.35 p.3156 |
| [`wp:start`](#wpstart) | `a:CT_Point2D` | 2 | 0 | 1 | §20.4.2.14 p.3134, OI 2.1.1357 |
| [`wp:style`](#wpstyle) | `a:CT_ShapeStyle` | 0 | 4 | 1 | §20.4.2.36 p.3157 |
| [`wp:txbx`](#wptxbx) | `wp:CT_TextboxInfo` | 1 | 2 | 1 | §20.4.2.37 p.3157 |
| [`wp:txbxContent`](#wptxbxcontent) | `wp:CT_TxbxContent` | 0 | 30 | 1 | §20.4.2.38 p.3157 |
| [`wp:wgp`](#wpwgp) ◆ | `wp:CT_WordprocessingGroup` | 0 | 9 | 1 | §20.4.2.39 p.3157 |
| [`wp:whole`](#wpwhole) | `a:CT_WholeE2oFormatting` | 0 | 3 | 1 | §20.4.2.40 p.3157 |
| [`wp:wpc`](#wpwpc) ◆ | `wp:CT_WordprocessingCanvas` | 0 | 8 | 0 | §20.4.2.41 p.3158 |
| [`wp:wrapNone`](#wpwrapnone) | `wp:CT_WrapNone` | 0 | 0 | 1 | §20.4.2.15 p.3135 |
| [`wp:wrapPolygon`](#wpwrappolygon) | `wp:CT_WrapPath` | 1 | 2 | 2 | §20.4.2.16 p.3136 |
| [`wp:wrapSquare`](#wpwrapsquare) | `wp:CT_WrapSquare` | 5 | 1 | 1 | §20.4.2.17 p.3137 |
| [`wp:wrapThrough`](#wpwrapthrough) | `wp:CT_WrapThrough` | 3 | 1 | 1 | §20.4.2.18 p.3140 |
| [`wp:wrapTight`](#wpwraptight) | `wp:CT_WrapTight` | 3 | 1 | 1 | §20.4.2.19 p.3143 |
| [`wp:wrapTopAndBottom`](#wpwraptopandbottom) | `wp:CT_WrapTopBottom` | 2 | 1 | 1 | §20.4.2.20 p.3146 |
| [`wp:wsp`](#wpwsp) ◆ | `wp:CT_WordprocessingShape` | 1 | 9 | 3 | §20.4.2.42 p.3158 |
| [`wp:xfrm`](#wpxfrm) | `a:CT_Transform2D` | 3 | 2 | 2 | §20.4.2.43 p.3158 |

◆ declared globally (can be the root element of a part)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `wp:ST_AlignH` | restriction of xsd:token | `left`, `right`, `center`, `inside`, `outside` | §20.4.3.1 p.3159 |
| `wp:ST_AlignV` | restriction of xsd:token | `top`, `bottom`, `center`, `inside`, `outside` | §20.4.3.2 p.3160 |
| `wp:ST_PositionOffset` | restriction of xsd:int |  | §20.4.3.3 p.3161 |
| `wp:ST_RelFromH` | restriction of xsd:token | `margin`, `page`, `column`, `character`, `leftMargin`, `rightMargin`, `insideMargin`, `outsideMargin` | §20.4.3.4 p.3162 |
| `wp:ST_RelFromV` | restriction of xsd:token | `margin`, `page`, `paragraph`, `line`, `topMargin`, `bottomMargin`, `insideMargin`, `outsideMargin` | §20.4.3.5 p.3163 |
| `wp:ST_WrapDistance` | restriction of xsd:unsignedInt |  | §20.4.3.6 p.3164 |
| `wp:ST_WrapText` | restriction of xsd:token | `bothSides`, `left`, `right`, `largest` | §20.4.3.7 p.3164 |

## Element details

### wp:align

- Type: `wp:ST_AlignH`, `wp:ST_AlignV` (text: `wp:ST_AlignH`)
- Parents: `wp:positionH`, `wp:positionV`
- Specification: §20.4.2.1 p.3110; §20.4.2.2 p.3111

### wp:anchor

- Type: `wp:CT_Anchor`
- Attributes: `allowOverlap`!, `behindDoc`!, `distB`, `distL`, `distR`, `distT`, `hidden`, `layoutInCell`!, `locked`!, `relativeHeight`!, `simplePos`
- Children: `wp:simplePos`, `wp:positionH`, `wp:positionV`, `wp:extent`, `wp:effectExtent`, `wp:wrapNone`, `wp:wrapSquare`, `wp:wrapTight`, `wp:wrapThrough`, `wp:wrapTopAndBottom`, `wp:docPr`, `wp:cNvGraphicFramePr`, `a:graphic`
- Parents: `w:drawing`
- Specification: §20.4.2.3 p.3112, OI 2.1.1350

### wp:bg

- Type: `a:CT_BackgroundFormatting`
- Children: `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`, `a:effectLst`, `a:effectDag`
- Parents: `wp:wpc`
- Specification: §20.4.2.21 p.3147

### wp:bodyPr

- Type: `a:CT_TextBodyProperties`
- Attributes: `anchor`, `anchorCtr`, `bIns`, `compatLnSpc`, `forceAA`, `fromWordArt`, `horzOverflow`, `lIns`, `numCol`, `rIns`, `rot`, `rtlCol`, `spcCol`, `spcFirstLastPara`, `tIns`, `upright`, `vert`, `vertOverflow`, `wrap`
- Children: `a:prstTxWarp`, `a:noAutofit`, `a:normAutofit`, `a:spAutoFit`, `a:scene3d`, `a:sp3d`, `a:flatTx`, `a:extLst`
- Parents: `wp:wsp`
- Specification: §20.4.2.22 p.3147

### wp:cNvCnPr

- Type: `a:CT_NonVisualConnectorProperties`
- Children: `a:cxnSpLocks`, `a:stCxn`, `a:endCxn`, `a:extLst`
- Parents: `wp:wsp`
- Specification: §20.4.2.23 p.3150

### wp:cNvContentPartPr

- Type: `a:CT_NonVisualContentPartProperties`
- Attributes: `isComment`
- Children: `a:cpLocks`, `a:extLst`
- Parents: `wp:nvContentPartPr`
- Specification: §20.4.2.24 p.3150

### wp:cNvFrPr

- Type: `a:CT_NonVisualGraphicFrameProperties`
- Children: `a:graphicFrameLocks`, `a:extLst`
- Parents: `wp:graphicFrame`
- Specification: §20.4.2.25 p.3151

### wp:cNvGraphicFramePr

- Type: `a:CT_NonVisualGraphicFrameProperties`
- Children: `a:graphicFrameLocks`, `a:extLst`
- Parents: `wp:anchor`, `wp:inline`
- Specification: §20.4.2.4 p.3119

### wp:cNvGrpSpPr

- Type: `a:CT_NonVisualGroupDrawingShapeProps`
- Children: `a:grpSpLocks`, `a:extLst`
- Parents: `wp:grpSp`, `wp:wgp`
- Specification: §20.4.2.26 p.3151

### wp:cNvPr

- Type: `a:CT_NonVisualDrawingProps`
- Attributes: `descr`, `hidden`, `id`!, `name`!, `title`
- Children: `a:hlinkClick`, `a:hlinkHover`, `a:extLst`
- Parents: `wp:graphicFrame`, `wp:grpSp`, `wp:nvContentPartPr`, `wp:wgp`, `wp:wsp`
- Specification: §20.4.2.27 p.3151

### wp:cNvSpPr

- Type: `a:CT_NonVisualDrawingShapeProps`
- Attributes: `txBox`
- Children: `a:spLocks`, `a:extLst`
- Parents: `wp:wsp`
- Specification: §20.4.2.28 p.3153

### wp:contentPart

- Type: `wp:CT_WordprocessingContentPart`
- Attributes: `bwMode`, `r:id`!
- Children: `wp:nvContentPartPr`, `wp:xfrm`, `wp:extLst`
- Parents: `wp:grpSp`, `wp:wgp`, `wp:wpc`
- Specification: §20.4.2.29 p.3154

### wp:docPr

- Type: `a:CT_NonVisualDrawingProps`
- Attributes: `descr`, `hidden`, `id`!, `name`!, `title`
- Children: `a:hlinkClick`, `a:hlinkHover`, `a:extLst`
- Parents: `wp:anchor`, `wp:inline`
- Specification: §20.4.2.5 p.3119, OI 2.1.1351

### wp:effectExtent

- Type: `wp:CT_EffectExtent`
- Attributes: `b`!, `l`!, `r`!, `t`!
- Parents: `wp:anchor`, `wp:inline`, `wp:wrapSquare`, `wp:wrapTopAndBottom`
- Specification: §20.4.2.6 p.3121, OI 2.1.1352

### wp:extLst

- Type: `a:CT_OfficeArtExtensionList`
- Children: `a:ext`
- Parents: `wp:contentPart`, `wp:graphicFrame`, `wp:grpSp`, `wp:linkedTxbx`, `wp:txbx`, `wp:wgp`, `wp:wpc`, `wp:wsp`
- Specification: §20.4.2.30 p.3155

### wp:extent

- Type: `a:CT_PositiveSize2D`
- Attributes: `cx`!, `cy`!
- Parents: `wp:anchor`, `wp:inline`
- Specification: §20.4.2.7 p.3125, OI 2.1.1353

### wp:graphicFrame

- Type: `wp:CT_GraphicFrame`
- Children: `wp:cNvPr`, `wp:cNvFrPr`, `wp:xfrm`, `a:graphic`, `wp:extLst`
- Parents: `wp:grpSp`, `wp:wgp`, `wp:wpc`
- Specification: §20.4.2.31 p.3155

### wp:grpSp

- Type: `wp:CT_WordprocessingGroup`
- Children: `wp:cNvPr`, `wp:cNvGrpSpPr`, `wp:grpSpPr`, `wp:wsp`, `wp:grpSp`, `wp:graphicFrame`, `pic:pic`, `wp:contentPart`, `wp:extLst`
- Parents: `wp:grpSp`, `wp:wgp`
- Specification: §20.4.2.32 p.3155

### wp:grpSpPr

- Type: `a:CT_GroupShapeProperties`
- Attributes: `bwMode`
- Children: `a:xfrm`, `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`, `a:effectLst`, `a:effectDag`, `a:scene3d`, `a:extLst`
- Parents: `wp:grpSp`, `wp:wgp`
- Specification: §20.4.2.33 p.3155

### wp:inline

- Type: `wp:CT_Inline`
- Attributes: `distB`, `distL`, `distR`, `distT`
- Children: `wp:extent`, `wp:effectExtent`, `wp:docPr`, `wp:cNvGraphicFramePr`, `a:graphic`
- Parents: `w:drawing`
- Specification: §20.4.2.8 p.3126

### wp:lineTo

- Type: `a:CT_Point2D`
- Attributes: `x`!, `y`!
- Parents: `wp:wrapPolygon`
- Specification: §20.4.2.9 p.3129, OI 2.1.1354

### wp:linkedTxbx

- Type: `wp:CT_LinkedTextboxInformation`
- Attributes: `id`!, `seq`!
- Children: `wp:extLst`
- Parents: `wp:wsp`
- Specification: §20.4.2.34 p.3156

### wp:nvContentPartPr

- Type: `wp:CT_WordprocessingContentPartNonVisual`
- Children: `wp:cNvPr`, `wp:cNvContentPartPr`
- Parents: `wp:contentPart`

### wp:posOffset

- Type: `wp:ST_PositionOffset` (text: `wp:ST_PositionOffset`)
- Parents: `wp:positionH`, `wp:positionV`
- Specification: §20.4.2.12 p.3133

### wp:positionH

- Type: `wp:CT_PosH`
- Attributes: `relativeFrom`!
- Children: `wp:align`, `wp:posOffset`
- Parents: `wp:anchor`
- Specification: §20.4.2.10 p.3131

### wp:positionV

- Type: `wp:CT_PosV`
- Attributes: `relativeFrom`!
- Children: `wp:align`, `wp:posOffset`
- Parents: `wp:anchor`
- Specification: §20.4.2.11 p.3132, OI 2.1.1355

### wp:simplePos

- Type: `a:CT_Point2D`
- Attributes: `x`!, `y`!
- Parents: `wp:anchor`
- Specification: §20.4.2.13 p.3133, OI 2.1.1356

### wp:spPr

- Type: `a:CT_ShapeProperties`
- Attributes: `bwMode`
- Children: `a:xfrm`, `a:custGeom`, `a:prstGeom`, `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`, `a:ln`, `a:effectLst`, `a:effectDag`, `a:scene3d`, `a:sp3d`, `a:extLst`
- Parents: `wp:wsp`
- Specification: §20.4.2.35 p.3156

### wp:start

- Type: `a:CT_Point2D`
- Attributes: `x`!, `y`!
- Parents: `wp:wrapPolygon`
- Specification: §20.4.2.14 p.3134, OI 2.1.1357

### wp:style

- Type: `a:CT_ShapeStyle`
- Children: `a:lnRef`, `a:fillRef`, `a:effectRef`, `a:fontRef`
- Parents: `wp:wsp`
- Specification: §20.4.2.36 p.3157

### wp:txbx

- Type: `wp:CT_TextboxInfo`
- Attributes: `id`
- Children: `wp:txbxContent`, `wp:extLst`
- Parents: `wp:wsp`
- Specification: §20.4.2.37 p.3157

### wp:txbxContent

- Type: `wp:CT_TxbxContent`
- Children: `w:customXml`, `w:sdt`, `w:p`, `w:tbl`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`, `w:altChunk`
- Parents: `wp:txbx`
- Specification: §20.4.2.38 p.3157

### wp:wgp

- Type: `wp:CT_WordprocessingGroup`
- Children: `wp:cNvPr`, `wp:cNvGrpSpPr`, `wp:grpSpPr`, `wp:wsp`, `wp:grpSp`, `wp:graphicFrame`, `pic:pic`, `wp:contentPart`, `wp:extLst`
- Parents: `wp:wpc`
- Specification: §20.4.2.39 p.3157

### wp:whole

- Type: `a:CT_WholeE2oFormatting`
- Children: `a:ln`, `a:effectLst`, `a:effectDag`
- Parents: `wp:wpc`
- Specification: §20.4.2.40 p.3157

### wp:wpc

- Type: `wp:CT_WordprocessingCanvas`
- Children: `wp:bg`, `wp:whole`, `wp:wsp`, `pic:pic`, `wp:contentPart`, `wp:wgp`, `wp:graphicFrame`, `wp:extLst`
- Specification: §20.4.2.41 p.3158

### wp:wrapNone

- Type: `wp:CT_WrapNone`
- Parents: `wp:anchor`
- Specification: §20.4.2.15 p.3135

### wp:wrapPolygon

- Type: `wp:CT_WrapPath`
- Attributes: `edited`
- Children: `wp:start`, `wp:lineTo`
- Parents: `wp:wrapThrough`, `wp:wrapTight`
- Specification: §20.4.2.16 p.3136

### wp:wrapSquare

- Type: `wp:CT_WrapSquare`
- Attributes: `distB`, `distL`, `distR`, `distT`, `wrapText`!
- Children: `wp:effectExtent`
- Parents: `wp:anchor`
- Specification: §20.4.2.17 p.3137

### wp:wrapThrough

- Type: `wp:CT_WrapThrough`
- Attributes: `distL`, `distR`, `wrapText`!
- Children: `wp:wrapPolygon`
- Parents: `wp:anchor`
- Specification: §20.4.2.18 p.3140

### wp:wrapTight

- Type: `wp:CT_WrapTight`
- Attributes: `distL`, `distR`, `wrapText`!
- Children: `wp:wrapPolygon`
- Parents: `wp:anchor`
- Specification: §20.4.2.19 p.3143

### wp:wrapTopAndBottom

- Type: `wp:CT_WrapTopBottom`
- Attributes: `distB`, `distT`
- Children: `wp:effectExtent`
- Parents: `wp:anchor`
- Specification: §20.4.2.20 p.3146

### wp:wsp

- Type: `wp:CT_WordprocessingShape`
- Attributes: `normalEastAsianFlow`
- Children: `wp:cNvPr`, `wp:cNvSpPr`, `wp:cNvCnPr`, `wp:spPr`, `wp:style`, `wp:extLst`, `wp:txbx`, `wp:linkedTxbx`, `wp:bodyPr`
- Parents: `wp:grpSp`, `wp:wgp`, `wp:wpc`
- Specification: §20.4.2.42 p.3158

### wp:xfrm

- Type: `a:CT_Transform2D`
- Attributes: `flipH`, `flipV`, `rot`
- Children: `a:off`, `a:ext`
- Parents: `wp:contentPart`, `wp:graphicFrame`
- Specification: §20.4.2.43 p.3158

`!` marks required attributes.
