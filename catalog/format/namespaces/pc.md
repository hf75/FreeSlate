# pc: PresentationML (PPTX)

`http://schemas.microsoft.com/office/powerpoint/2013/main/command`

- Source: [MS-PPTX] v20240820 §5.3
- Office version: 2016
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 23 | 21 | 0 | 21 | 13 | 124 | 49 | 0 |

Full detail (content models, attribute types, defaults): [pc.json](pc.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`pc:chgData`](#pcchgdata) | `oac:CT_ChangesData` | 9 | 1 | 7 | MS-ODRAWXML §2.29.3.1 p.275 |
| [`pc:chgInfo`](#pcchginfo) ◆ | `pc:CT_ChangesInfo` | 0 | 1 | 0 | MS-PPTX §2.12.1.1 p.81 |
| [`pc:cmAuthorChg`](#pccmauthorchg) | `pc:CT_CommentAuthorChanges` | 1 | 3 | 1 | MS-PPTX §2.12.3.2 p.84 |
| [`pc:cmAuthorMkLst`](#pccmauthormklst) ◆ | `pc:CT_CommentAuthorMonikerList` | 0 | 1 | 1 | MS-PPTX §2.12.1.2 p.82 |
| [`pc:cmChg`](#pccmchg) | `pc:CT_CommentChanges` | 1 | 3 | 1 | MS-PPTX §2.12.3.5 p.86 |
| [`pc:cmMkLst`](#pccmmklst) ◆ | `pc:CT_CommentMonikerList` | 0 | 1 | 1 | MS-PPTX §2.12.1.3 p.82 |
| [`pc:cxnChg`](#pccxnchg) | `oac:CT_ConnectorChanges` | 1 | 3 | 3 | MS-ODRAWXML §2.29.3.2 p.276 |
| [`pc:docChg`](#pcdocchg) | `pc:CT_DocumentChanges` | 1 | 6 | 1 | MS-PPTX §2.12.3.8 p.87 |
| [`pc:docChgLst`](#pcdocchglst) | `pc:CT_DocumentChangesList` | 0 | 3 | 1 | MS-PPTX §2.12.3.9 p.88 |
| [`pc:docMkLst`](#pcdocmklst) ◆ | `pc:CT_DocumentMonikerList` | 0 | 1 | 1 | MS-PPTX §2.12.1.4 p.82 |
| [`pc:extLst`](#pcextlst) | `p:CT_ExtensionList` | 0 | 1 | 7 |  |
| [`pc:graphicFrameChg`](#pcgraphicframechg) | `oac:CT_GraphicFrameChanges` | 1 | 3 | 3 | MS-ODRAWXML §2.29.3.19 p.284 |
| [`pc:grpChg`](#pcgrpchg) | `oac:CT_GroupShapeChanges` | 1 | 3 | 3 | MS-ODRAWXML §2.29.3.7 p.278 |
| [`pc:inkChg`](#pcinkchg) | `oac:CT_InkChanges` | 1 | 3 | 3 | MS-ODRAWXML §2.29.3.10 p.279 |
| [`pc:picChg`](#pcpicchg) | `oac:CT_PictureChanges` | 1 | 3 | 3 | MS-ODRAWXML §2.29.3.13 p.281 |
| [`pc:sldBaseMkLst`](#pcsldbasemklst) ◆ | `pc:CT_SlideBaseMonikerList` | 0 | 1 | 0 | MS-PPTX §2.12.1.5 p.82 |
| [`pc:sldChg`](#pcsldchg) | `pc:CT_SlideChanges` | 1 | 10 | 1 | MS-PPTX §2.12.3.16 p.92 |
| [`pc:sldLayoutChg`](#pcsldlayoutchg) | `pc:CT_SlideLayoutChanges` | 1 | 9 | 1 | MS-PPTX §2.12.3.17 p.93 |
| [`pc:sldLayoutMkLst`](#pcsldlayoutmklst) ◆ | `pc:CT_SlideLayoutMonikerList` | 0 | 1 | 1 | MS-PPTX §2.12.1.6 p.83 |
| [`pc:sldMasterChg`](#pcsldmasterchg) | `pc:CT_MainMasterChanges` | 1 | 10 | 1 | MS-PPTX §2.12.3.12 p.89 |
| [`pc:sldMasterMkLst`](#pcsldmastermklst) ◆ | `pc:CT_MainMasterMonikerList` | 0 | 1 | 1 | MS-PPTX §2.12.1.7 p.83 |
| [`pc:sldMkLst`](#pcsldmklst) ◆ | `pc:CT_SlideMonikerList` | 0 | 1 | 2 | MS-PPTX §2.12.1.8 p.83 |
| [`pc:spChg`](#pcspchg) | `oac:CT_ShapeChanges` | 1 | 3 | 3 | MS-ODRAWXML §2.29.3.16 p.282 |

◆ declared globally (can be the root element of a part)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `pc:ST_CommentAuthorChangeBit` | restriction of xsd:token | `add`, `del`, `mod`, `replId` | MS-PPTX §2.12.4.1 p.96 |
| `pc:ST_CommentAuthorChangeBits` | list of pc:ST_CommentAuthorChangeBit |  | MS-PPTX §2.12.4.2 p.96 |
| `pc:ST_CommentChangeBit` | restriction of xsd:token | `add`, `del`, `mod`, `replId` | MS-PPTX §2.12.4.3 p.96 |
| `pc:ST_CommentChangeBits` | list of pc:ST_CommentChangeBit |  | MS-PPTX §2.12.4.4 p.97 |
| `pc:ST_CreationId` | restriction of xsd:unsignedInt |  | MS-PPTX §2.12.4.5 p.97 |
| `pc:ST_DocumentChangeBit` | restriction of xsd:token | `undo`, `redo`, `ext`, `custSel`, `mod`, `addSld`, `delSld`, `modSld`, `sldOrd`, `addMainMaster`, `delMainMaster`, `modMainMaster` … (+20) | MS-PPTX §2.12.4.6 p.98 |
| `pc:ST_DocumentChangeBits` | list of pc:ST_DocumentChangeBit |  | MS-PPTX §2.12.4.7 p.100 |
| `pc:ST_MainMasterChangeBit` | restriction of xsd:token | `addSp`, `delSp`, `modSp`, `spOrd`, `new`, `add`, `del`, `mod`, `ord`, `replId`, `modTransition`, `modMedia` … (+16) | MS-PPTX §2.12.4.8 p.100 |
| `pc:ST_MainMasterChangeBits` | list of pc:ST_MainMasterChangeBit |  | MS-PPTX §2.12.4.9 p.102 |
| `pc:ST_SlideChangeBit` | restriction of xsd:token | `addSp`, `delSp`, `modSp`, `spOrd`, `new`, `add`, `del`, `mod`, `ord`, `replId`, `modTransition`, `modMedia` … (+20) | MS-PPTX §2.12.4.10 p.102 |
| `pc:ST_SlideChangeBits` | list of pc:ST_SlideChangeBit |  | MS-PPTX §2.12.4.11 p.104 |
| `pc:ST_SlideLayoutChangeBit` | restriction of xsd:token | `addSp`, `delSp`, `modSp`, `spOrd`, `new`, `add`, `del`, `mod`, `ord`, `replId`, `modTransition`, `modMedia` … (+12) | MS-PPTX §2.12.4.12 p.104 |
| `pc:ST_SlideLayoutChangeBits` | list of pc:ST_SlideLayoutChangeBit |  | MS-PPTX §2.12.4.13 p.106 |

## Element details

### pc:chgData

- Type: `oac:CT_ChangesData`
- Attributes: `actId`, `clId`, `dt`, `email`, `id`, `name`, `providerId`, `userId`, `v`
- Children: `oac:extLst`
- Parents: `pc:cmAuthorChg`, `pc:cmChg`, `pc:docChg`, `pc:docChgLst`, `pc:sldChg`, `pc:sldLayoutChg`, `pc:sldMasterChg`
- Specification: MS-ODRAWXML §2.29.3.1 p.275

### pc:chgInfo

- Type: `pc:CT_ChangesInfo`
- Children: `pc:docChgLst`
- Specification: MS-PPTX §2.12.1.1 p.81

### pc:cmAuthorChg

- Type: `pc:CT_CommentAuthorChanges`
- Attributes: `chg`!
- Children: `pc:chgData`, `pc:cmAuthorMkLst`, `pc:extLst`
- Parents: `pc:docChg`
- Specification: MS-PPTX §2.12.3.2 p.84

### pc:cmAuthorMkLst

- Type: `pc:CT_CommentAuthorMonikerList`
- Children: `any(##any)`
- Parents: `pc:cmAuthorChg`
- Specification: MS-PPTX §2.12.1.2 p.82

### pc:cmChg

- Type: `pc:CT_CommentChanges`
- Attributes: `chg`!
- Children: `pc:chgData`, `pc:cmMkLst`, `pc:extLst`
- Parents: `pc:sldChg`
- Specification: MS-PPTX §2.12.3.5 p.86

### pc:cmMkLst

- Type: `pc:CT_CommentMonikerList`
- Children: `any(##any)`
- Parents: `pc:cmChg`
- Specification: MS-PPTX §2.12.1.3 p.82

### pc:cxnChg

- Type: `oac:CT_ConnectorChanges`
- Attributes: `chg`!
- Children: `oac:chgData`, `oac:cxnSpMkLst`, `oac:extLst`
- Parents: `pc:sldChg`, `pc:sldLayoutChg`, `pc:sldMasterChg`
- Specification: MS-ODRAWXML §2.29.3.2 p.276

### pc:docChg

- Type: `pc:CT_DocumentChanges`
- Attributes: `chg`!
- Children: `pc:chgData`, `pc:docMkLst`, `pc:sldChg`, `pc:sldMasterChg`, `pc:cmAuthorChg`, `pc:extLst`
- Parents: `pc:docChgLst`
- Specification: MS-PPTX §2.12.3.8 p.87

### pc:docChgLst

- Type: `pc:CT_DocumentChangesList`
- Children: `pc:chgData`, `pc:docChg`, `pc:extLst`
- Parents: `pc:chgInfo`
- Specification: MS-PPTX §2.12.3.9 p.88

### pc:docMkLst

- Type: `pc:CT_DocumentMonikerList`
- Children: `any(##any)`
- Parents: `pc:docChg`
- Specification: MS-PPTX §2.12.1.4 p.82

### pc:extLst

- Type: `p:CT_ExtensionList`
- Children: `p:ext`
- Parents: `pc:cmAuthorChg`, `pc:cmChg`, `pc:docChg`, `pc:docChgLst`, `pc:sldChg`, `pc:sldLayoutChg`, `pc:sldMasterChg`

### pc:graphicFrameChg

- Type: `oac:CT_GraphicFrameChanges`
- Attributes: `chg`!
- Children: `oac:chgData`, `oac:graphicFrameMkLst`, `oac:extLst`
- Parents: `pc:sldChg`, `pc:sldLayoutChg`, `pc:sldMasterChg`
- Specification: MS-ODRAWXML §2.29.3.19 p.284

### pc:grpChg

- Type: `oac:CT_GroupShapeChanges`
- Attributes: `chg`!
- Children: `oac:chgData`, `oac:grpSpMkLst`, `oac:extLst`
- Parents: `pc:sldChg`, `pc:sldLayoutChg`, `pc:sldMasterChg`
- Specification: MS-ODRAWXML §2.29.3.7 p.278

### pc:inkChg

- Type: `oac:CT_InkChanges`
- Attributes: `chg`!
- Children: `oac:chgData`, `oac:inkMkLst`, `oac:extLst`
- Parents: `pc:sldChg`, `pc:sldLayoutChg`, `pc:sldMasterChg`
- Specification: MS-ODRAWXML §2.29.3.10 p.279

### pc:picChg

- Type: `oac:CT_PictureChanges`
- Attributes: `chg`!
- Children: `oac:chgData`, `oac:picMkLst`, `oac:extLst`
- Parents: `pc:sldChg`, `pc:sldLayoutChg`, `pc:sldMasterChg`
- Specification: MS-ODRAWXML §2.29.3.13 p.281

### pc:sldBaseMkLst

- Type: `pc:CT_SlideBaseMonikerList`
- Children: `any(##any)`
- Specification: MS-PPTX §2.12.1.5 p.82

### pc:sldChg

- Type: `pc:CT_SlideChanges`
- Attributes: `chg`!
- Children: `pc:chgData`, `pc:sldMkLst`, `pc:spChg`, `pc:grpChg`, `pc:graphicFrameChg`, `pc:picChg`, `pc:inkChg`, `pc:cxnChg`, `pc:cmChg`, `pc:extLst`
- Parents: `pc:docChg`
- Specification: MS-PPTX §2.12.3.16 p.92

### pc:sldLayoutChg

- Type: `pc:CT_SlideLayoutChanges`
- Attributes: `chg`!
- Children: `pc:chgData`, `pc:sldLayoutMkLst`, `pc:spChg`, `pc:grpChg`, `pc:graphicFrameChg`, `pc:picChg`, `pc:inkChg`, `pc:cxnChg`, `pc:extLst`
- Parents: `pc:sldMasterChg`
- Specification: MS-PPTX §2.12.3.17 p.93

### pc:sldLayoutMkLst

- Type: `pc:CT_SlideLayoutMonikerList`
- Children: `any(##any)`
- Parents: `pc:sldLayoutChg`
- Specification: MS-PPTX §2.12.1.6 p.83

### pc:sldMasterChg

- Type: `pc:CT_MainMasterChanges`
- Attributes: `chg`!
- Children: `pc:chgData`, `pc:sldMasterMkLst`, `pc:spChg`, `pc:grpChg`, `pc:graphicFrameChg`, `pc:picChg`, `pc:inkChg`, `pc:cxnChg`, `pc:sldLayoutChg`, `pc:extLst`
- Parents: `pc:docChg`
- Specification: MS-PPTX §2.12.3.12 p.89

### pc:sldMasterMkLst

- Type: `pc:CT_MainMasterMonikerList`
- Children: `any(##any)`
- Parents: `pc:sldMasterChg`
- Specification: MS-PPTX §2.12.1.7 p.83

### pc:sldMkLst

- Type: `pc:CT_SlideMonikerList`
- Children: `any(##any)`
- Parents: `p188:cm`, `pc:sldChg`
- Specification: MS-PPTX §2.12.1.8 p.83

### pc:spChg

- Type: `oac:CT_ShapeChanges`
- Attributes: `chg`!
- Children: `oac:chgData`, `oac:spMkLst`, `oac:extLst`
- Parents: `pc:sldChg`, `pc:sldLayoutChg`, `pc:sldMasterChg`
- Specification: MS-ODRAWXML §2.29.3.16 p.282

`!` marks required attributes.
