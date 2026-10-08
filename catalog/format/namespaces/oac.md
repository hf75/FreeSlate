# oac: DrawingML

`http://schemas.microsoft.com/office/drawing/2013/main/command`

- Source: [MS-ODRAWXML] v20261006 §5.27
- Office version: 2016
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 11 | 11 | 0 | 23 | 12 | 59 | 43 | 0 |

Full detail (content models, attribute types, defaults): [oac.json](oac.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`oac:chgData`](#oacchgdata) | `oac:CT_ChangesData` | 9 | 1 | 6 | MS-ODRAWXML §2.29.3.1 p.275 |
| [`oac:context`](#oaccontext) | `oac:CT_TextCharRangeContext` | 2 | 0 | 0 | MS-ODRAWXML §2.29.3.22 p.286 |
| [`oac:cxnSpMkLst`](#oaccxnspmklst) ◆ | `oac:CT_ConnectorMonikerList` | 0 | 1 | 1 | MS-ODRAWXML §2.29.1.1 p.272 |
| [`oac:deMkLst`](#oacdemklst) ◆ | `oac:CT_DrawingElementMonikerList` | 0 | 1 | 1 | MS-ODRAWXML §2.29.1.2 p.273 |
| [`oac:extLst`](#oacextlst) | `a:CT_OfficeArtExtensionList` | 0 | 1 | 9 |  |
| [`oac:graphicFrameMkLst`](#oacgraphicframemklst) ◆ | `oac:CT_GraphicFrameMonikerList` | 0 | 1 | 1 | MS-ODRAWXML §2.29.1.3 p.273 |
| [`oac:grpSpMkLst`](#oacgrpspmklst) ◆ | `oac:CT_GroupShapeMonikerList` | 0 | 1 | 1 | MS-ODRAWXML §2.29.1.4 p.273 |
| [`oac:inkMkLst`](#oacinkmklst) ◆ | `oac:CT_InkMonikerList` | 0 | 1 | 1 | MS-ODRAWXML §2.29.1.5 p.273 |
| [`oac:picMkLst`](#oacpicmklst) ◆ | `oac:CT_PictureMonikerList` | 0 | 1 | 1 | MS-ODRAWXML §2.29.1.6 p.274 |
| [`oac:spMkLst`](#oacspmklst) ◆ | `oac:CT_ShapeMonikerList` | 0 | 1 | 1 | MS-ODRAWXML §2.29.1.7 p.274 |
| [`oac:txMkLst`](#oactxmklst) ◆ | `oac:CT_TextCharRangeMonikerList` | 0 | 1 | 1 | MS-ODRAWXML §2.29.1.8 p.274 |

◆ declared globally (can be the root element of a part)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `oac:ST_ConnectorChangeBit` | restriction of xsd:token | `add`, `del`, `mod`, `ord`, `topLvl`, `modVis`, `replST`, `delST`, `replId` | MS-ODRAWXML §2.29.4.1 p.287 |
| `oac:ST_ConnectorChangesBits` | list of oac:ST_ConnectorChangeBit |  | MS-ODRAWXML §2.29.4.2 p.288 |
| `oac:ST_GraphicFrameChangeBit` | restriction of xsd:token | `add`, `del`, `mod`, `ord`, `topLvl`, `modVis`, `replST`, `delST`, `replId`, `modGraphic` | MS-ODRAWXML §2.29.4.11 p.293 |
| `oac:ST_GraphicFrameChangesBits` | list of oac:ST_GraphicFrameChangeBit |  | MS-ODRAWXML §2.29.4.12 p.294 |
| `oac:ST_GroupShapeChangeBit` | restriction of xsd:token | `add`, `del`, `mod`, `ord`, `topLvl`, `modVis`, `replST`, `delST`, `replId` | MS-ODRAWXML §2.29.4.3 p.288 |
| `oac:ST_GroupShapeChangesBits` | list of oac:ST_GroupShapeChangeBit |  | MS-ODRAWXML §2.29.4.4 p.289 |
| `oac:ST_InkChangeBit` | restriction of xsd:token | `add`, `del`, `mod`, `ord`, `topLvl`, `modVis`, `replST`, `delST`, `replId`, `reco`, `modStrokes` | MS-ODRAWXML §2.29.4.5 p.289 |
| `oac:ST_InkChangesBits` | list of oac:ST_InkChangeBit |  | MS-ODRAWXML §2.29.4.6 p.290 |
| `oac:ST_PictureChangeBit` | restriction of xsd:token | `add`, `del`, `mod`, `ord`, `topLvl`, `modVis`, `replST`, `delST`, `replId`, `modCrop` | MS-ODRAWXML §2.29.4.7 p.290 |
| `oac:ST_PictureChangesBits` | list of oac:ST_PictureChangeBit |  | MS-ODRAWXML §2.29.4.8 p.291 |
| `oac:ST_ShapeChangeBit` | restriction of xsd:token | `add`, `del`, `mod`, `ord`, `topLvl`, `modVis`, `replST`, `delST`, `replId`, `modCrop` | MS-ODRAWXML §2.29.4.9 p.292 |
| `oac:ST_ShapeChangesBits` | list of oac:ST_ShapeChangeBit |  | MS-ODRAWXML §2.29.4.10 p.292 |

## Element details

### oac:chgData

- Type: `oac:CT_ChangesData`
- Attributes: `actId`, `clId`, `dt`, `email`, `id`, `name`, `providerId`, `userId`, `v`
- Children: `oac:extLst`
- Parents: `pc:cxnChg`, `pc:graphicFrameChg`, `pc:grpChg`, `pc:inkChg`, `pc:picChg`, `pc:spChg`
- Specification: MS-ODRAWXML §2.29.3.1 p.275

### oac:context

- Type: `oac:CT_TextCharRangeContext`
- Attributes: `hash`!, `len`
- Specification: MS-ODRAWXML §2.29.3.22 p.286

### oac:cxnSpMkLst

- Type: `oac:CT_ConnectorMonikerList`
- Children: `any(##any)`
- Parents: `pc:cxnChg`
- Specification: MS-ODRAWXML §2.29.1.1 p.272

### oac:deMkLst

- Type: `oac:CT_DrawingElementMonikerList`
- Children: `any(##any)`
- Parents: `p188:cm`
- Specification: MS-ODRAWXML §2.29.1.2 p.273

### oac:extLst

- Type: `a:CT_OfficeArtExtensionList`
- Children: `a:ext`
- Parents: `oac:chgData`, `pc226:chgData`, `pc:chgData`, `pc:cxnChg`, `pc:graphicFrameChg`, `pc:grpChg`, `pc:inkChg`, `pc:picChg`, `pc:spChg`

### oac:graphicFrameMkLst

- Type: `oac:CT_GraphicFrameMonikerList`
- Children: `any(##any)`
- Parents: `pc:graphicFrameChg`
- Specification: MS-ODRAWXML §2.29.1.3 p.273

### oac:grpSpMkLst

- Type: `oac:CT_GroupShapeMonikerList`
- Children: `any(##any)`
- Parents: `pc:grpChg`
- Specification: MS-ODRAWXML §2.29.1.4 p.273

### oac:inkMkLst

- Type: `oac:CT_InkMonikerList`
- Children: `any(##any)`
- Parents: `pc:inkChg`
- Specification: MS-ODRAWXML §2.29.1.5 p.273

### oac:picMkLst

- Type: `oac:CT_PictureMonikerList`
- Children: `any(##any)`
- Parents: `pc:picChg`
- Specification: MS-ODRAWXML §2.29.1.6 p.274

### oac:spMkLst

- Type: `oac:CT_ShapeMonikerList`
- Children: `any(##any)`
- Parents: `pc:spChg`
- Specification: MS-ODRAWXML §2.29.1.7 p.274

### oac:txMkLst

- Type: `oac:CT_TextCharRangeMonikerList`
- Children: `any(##any)`
- Parents: `p188:cm`
- Specification: MS-ODRAWXML §2.29.1.8 p.274

`!` marks required attributes.
