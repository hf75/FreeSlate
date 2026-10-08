# pic: DrawingML

`http://schemas.openxmlformats.org/drawingml/2006/picture`

- Source: ECMA-376-4 transitional schema dml-picture.xsd
- Office version: 2007
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 6 | 9 | 0 | 2 | 0 | 0 | 6 | 2 |

Full detail (content models, attribute types, defaults): [pic.json](pic.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`pic:blipFill`](#picblipfill) | `a:CT_BlipFillProperties` | 2 | 4 | 1 | §20.2.2.1 p.3100, OI 2.1.1348 |
| [`pic:cNvPicPr`](#piccnvpicpr) | `a:CT_NonVisualPictureProperties` | 1 | 2 | 1 | §20.2.2.2 p.3103 |
| [`pic:cNvPr`](#piccnvpr) | `a:CT_NonVisualDrawingProps` | 5 | 3 | 1 | §20.2.2.3 p.3104 |
| [`pic:nvPicPr`](#picnvpicpr) | `pic:CT_PictureNonVisual` | 0 | 2 | 1 | §20.2.2.4 p.3106 |
| [`pic:pic`](#picpic) ◆ | `pic:CT_Picture` | 0 | 3 | 6 | §20.2.2.5 p.3106 |
| [`pic:spPr`](#picsppr) | `a:CT_ShapeProperties` | 1 | 15 | 1 | §20.2.2.6 p.3107, OI 2.1.1349 |

◆ declared globally (can be the root element of a part)

## Element details

### pic:blipFill

- Type: `a:CT_BlipFillProperties`
- Attributes: `dpi`, `rotWithShape`
- Children: `a:blip`, `a:srcRect`, `a:tile`, `a:stretch`
- Parents: `pic:pic`
- Specification: §20.2.2.1 p.3100, OI 2.1.1348

### pic:cNvPicPr

- Type: `a:CT_NonVisualPictureProperties`
- Attributes: `preferRelativeResize`
- Children: `a:picLocks`, `a:extLst`
- Parents: `pic:nvPicPr`
- Specification: §20.2.2.2 p.3103

### pic:cNvPr

- Type: `a:CT_NonVisualDrawingProps`
- Attributes: `descr`, `hidden`, `id`!, `name`!, `title`
- Children: `a:hlinkClick`, `a:hlinkHover`, `a:extLst`
- Parents: `pic:nvPicPr`
- Specification: §20.2.2.3 p.3104

### pic:nvPicPr

- Type: `pic:CT_PictureNonVisual`
- Children: `pic:cNvPr`, `pic:cNvPicPr`
- Parents: `pic:pic`
- Specification: §20.2.2.4 p.3106

### pic:pic

- Type: `pic:CT_Picture`
- Children: `pic:nvPicPr`, `pic:blipFill`, `pic:spPr`
- Parents: `wp:grpSp`, `wp:wgp`, `wp:wpc`, `wpc:wpc`, `wpg:grpSp`, `wpg:wgp`
- Specification: §20.2.2.5 p.3106

### pic:spPr

- Type: `a:CT_ShapeProperties`
- Attributes: `bwMode`
- Children: `a:xfrm`, `a:custGeom`, `a:prstGeom`, `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`, `a:ln`, `a:effectLst`, `a:effectDag`, `a:scene3d`, `a:sp3d`, `a:extLst`
- Parents: `pic:pic`
- Specification: §20.2.2.6 p.3107, OI 2.1.1349

`!` marks required attributes.
