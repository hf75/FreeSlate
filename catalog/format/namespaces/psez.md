# psez: PresentationML (PPTX)

`http://schemas.microsoft.com/office/powerpoint/2016/sectionzoom`

- Source: [MS-PPTX] v20240820 §5.8
- Office version: ?
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 4 | 6 | 0 | 2 | 0 | 0 | 4 | 0 |

Full detail (content models, attribute types, defaults): [psez.json](psez.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`psez:extLst`](#psezextlst) | `p:CT_ExtensionList` | 0 | 1 | 2 |  |
| [`psez:sectionZm`](#psezsectionzm) ◆ | `psez:CT_SectionZoom` | 0 | 2 | 0 | MS-PPTX §2.9.1.1 p.76 |
| [`psez:sectionZmObj`](#psezsectionzmobj) | `psez:CT_SectionZoomObject` | 1 | 2 | 1 | MS-PPTX §2.9.3.2 p.76 |
| [`psez:zmPr`](#psezzmpr) | `p166:CT_ZoomObjectProperties` | 5 | 2 | 1 | MS-PPTX §2.8.3.1 p.74 |

◆ declared globally (can be the root element of a part)

## Element details

### psez:extLst

- Type: `p:CT_ExtensionList`
- Children: `p:ext`
- Parents: `psez:sectionZm`, `psez:sectionZmObj`

### psez:sectionZm

- Type: `psez:CT_SectionZoom`
- Children: `psez:sectionZmObj`, `psez:extLst`
- Specification: MS-PPTX §2.9.1.1 p.76

### psez:sectionZmObj

- Type: `psez:CT_SectionZoomObject`
- Attributes: `sectionId`!
- Children: `psez:zmPr`, `psez:extLst`
- Parents: `psez:sectionZm`
- Specification: MS-PPTX §2.9.3.2 p.76

### psez:zmPr

- Type: `p166:CT_ZoomObjectProperties`
- Attributes: `id`!, `imageType`, `returnToParent`, `showBg`, `transitionDur`
- Children: `p166:blipFill`, `p166:spPr`
- Parents: `psez:sectionZmObj`
- Specification: MS-PPTX §2.8.3.1 p.74

`!` marks required attributes.
