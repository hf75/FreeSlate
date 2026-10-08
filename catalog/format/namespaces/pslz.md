# pslz: PresentationML (PPTX)

`http://schemas.microsoft.com/office/powerpoint/2016/slidezoom`

- Source: [MS-PPTX] v20240820 §5.9
- Office version: ?
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 4 | 7 | 0 | 2 | 0 | 0 | 4 | 0 |

Full detail (content models, attribute types, defaults): [pslz.json](pslz.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`pslz:extLst`](#pslzextlst) | `p:CT_ExtensionList` | 0 | 1 | 2 |  |
| [`pslz:sldZm`](#pslzsldzm) ◆ | `pslz:CT_SlideZoom` | 0 | 2 | 0 | MS-PPTX §2.10.1.1 p.77 |
| [`pslz:sldZmObj`](#pslzsldzmobj) | `pslz:CT_SlideZoomObject` | 2 | 2 | 1 | MS-PPTX §2.10.3.2 p.78 |
| [`pslz:zmPr`](#pslzzmpr) | `p166:CT_ZoomObjectProperties` | 5 | 2 | 1 | MS-PPTX §2.8.3.1 p.74 |

◆ declared globally (can be the root element of a part)

## Element details

### pslz:extLst

- Type: `p:CT_ExtensionList`
- Children: `p:ext`
- Parents: `pslz:sldZm`, `pslz:sldZmObj`

### pslz:sldZm

- Type: `pslz:CT_SlideZoom`
- Children: `pslz:sldZmObj`, `pslz:extLst`
- Specification: MS-PPTX §2.10.1.1 p.77

### pslz:sldZmObj

- Type: `pslz:CT_SlideZoomObject`
- Attributes: `cId`, `sldId`!
- Children: `pslz:zmPr`, `pslz:extLst`
- Parents: `pslz:sldZm`
- Specification: MS-PPTX §2.10.3.2 p.78

### pslz:zmPr

- Type: `p166:CT_ZoomObjectProperties`
- Attributes: `id`!, `imageType`, `returnToParent`, `showBg`, `transitionDur`
- Children: `p166:blipFill`, `p166:spPr`
- Parents: `pslz:sldZmObj`
- Specification: MS-PPTX §2.8.3.1 p.74

`!` marks required attributes.
