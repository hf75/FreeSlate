# psuz: PresentationML (PPTX)

`http://schemas.microsoft.com/office/powerpoint/2016/summaryzoom`

- Source: [MS-PPTX] v20240820 §5.10
- Office version: ?
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 6 | 12 | 0 | 4 | 0 | 0 | 6 | 0 |

Full detail (content models, attribute types, defaults): [psuz.json](psuz.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`psuz:extLst`](#psuzextlst) | `p:CT_ExtensionList` | 0 | 1 | 2 |  |
| [`psuz:fixedLayout`](#psuzfixedlayout) | `psuz:CT_FixedLayout` | 0 | 0 | 1 | MS-PPTX §2.11.3.1 p.79 |
| [`psuz:gridLayout`](#psuzgridlayout) | `psuz:CT_GridLayout` | 0 | 0 | 1 | MS-PPTX §2.11.3.2 p.79 |
| [`psuz:summaryZm`](#psuzsummaryzm) ◆ | `psuz:CT_SummaryZoom` | 0 | 4 | 0 | MS-PPTX §2.11.1.1 p.79 |
| [`psuz:summaryZmObj`](#psuzsummaryzmobj) | `psuz:CT_SummaryZoomObject` | 7 | 2 | 1 | MS-PPTX §2.11.3.4 p.80 |
| [`psuz:zmPr`](#psuzzmpr) | `p166:CT_ZoomObjectProperties` | 5 | 2 | 1 | MS-PPTX §2.8.3.1 p.74 |

◆ declared globally (can be the root element of a part)

## Element details

### psuz:extLst

- Type: `p:CT_ExtensionList`
- Children: `p:ext`
- Parents: `psuz:summaryZm`, `psuz:summaryZmObj`

### psuz:fixedLayout

- Type: `psuz:CT_FixedLayout`
- Parents: `psuz:summaryZm`
- Specification: MS-PPTX §2.11.3.1 p.79

### psuz:gridLayout

- Type: `psuz:CT_GridLayout`
- Parents: `psuz:summaryZm`
- Specification: MS-PPTX §2.11.3.2 p.79

### psuz:summaryZm

- Type: `psuz:CT_SummaryZoom`
- Children: `psuz:summaryZmObj`, `psuz:gridLayout`, `psuz:fixedLayout`, `psuz:extLst`
- Specification: MS-PPTX §2.11.1.1 p.79

### psuz:summaryZmObj

- Type: `psuz:CT_SummaryZoomObject`
- Attributes: `descr`, `offsetFactorX`, `offsetFactorY`, `scaleFactorX`, `scaleFactorY`, `sectionId`!, `title`
- Children: `psuz:zmPr`, `psuz:extLst`
- Parents: `psuz:summaryZm`
- Specification: MS-PPTX §2.11.3.4 p.80

### psuz:zmPr

- Type: `p166:CT_ZoomObjectProperties`
- Attributes: `id`!, `imageType`, `returnToParent`, `showBg`, `transitionDur`
- Children: `p166:blipFill`, `p166:spPr`
- Parents: `psuz:summaryZmObj`
- Specification: MS-PPTX §2.8.3.1 p.74

`!` marks required attributes.
