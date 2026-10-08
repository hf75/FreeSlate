# wpc: WordprocessingML (DOCX)

`http://schemas.microsoft.com/office/word/2010/wordprocessingCanvas`

- Source: [MS-ODRAWXML] v20261006 §5.5
- Office version: 2010
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 5 | 0 | 0 | 1 | 0 | 0 | 3 | 0 |

Full detail (content models, attribute types, defaults): [wpc.json](wpc.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`wpc:bg`](#wpcbg) | `a:CT_BackgroundFormatting` | 0 | 8 | 1 |  |
| [`wpc:extLst`](#wpcextlst) | `a:CT_OfficeArtExtensionList` | 0 | 1 | 1 |  |
| [`wpc:graphicFrame`](#wpcgraphicframe) | `wpg:CT_GraphicFrame` | 0 | 5 | 1 | MS-ODRAWXML §2.16.3.1 p.163 |
| [`wpc:whole`](#wpcwhole) | `a:CT_WholeE2oFormatting` | 0 | 3 | 1 |  |
| [`wpc:wpc`](#wpcwpc) ◆ | `wpc:CT_WordprocessingCanvas` | 0 | 8 | 0 | MS-ODRAWXML §2.17.1.1 p.165 |

◆ declared globally (can be the root element of a part)

## Element details

### wpc:bg

- Type: `a:CT_BackgroundFormatting`
- Children: `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`, `a:effectLst`, `a:effectDag`
- Parents: `wpc:wpc`

### wpc:extLst

- Type: `a:CT_OfficeArtExtensionList`
- Children: `a:ext`
- Parents: `wpc:wpc`

### wpc:graphicFrame

- Type: `wpg:CT_GraphicFrame`
- Children: `wpg:cNvPr`, `wpg:cNvFrPr`, `wpg:xfrm`, `a:graphic`, `wpg:extLst`
- Parents: `wpc:wpc`
- Specification: MS-ODRAWXML §2.16.3.1 p.163

### wpc:whole

- Type: `a:CT_WholeE2oFormatting`
- Children: `a:ln`, `a:effectLst`, `a:effectDag`
- Parents: `wpc:wpc`

### wpc:wpc

- Type: `wpc:CT_WordprocessingCanvas`
- Children: `wpc:bg`, `wpc:whole`, `wps:wsp`, `pic:pic`, `w14:contentPart`, `wpg:wgp`, `wpc:graphicFrame`, `wpc:extLst`
- Specification: MS-ODRAWXML §2.17.1.1 p.165

`!` marks required attributes.
