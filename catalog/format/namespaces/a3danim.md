# a3danim: DrawingML

`http://schemas.microsoft.com/office/drawing/2018/animation/model3d`

- Source: [MS-ODRAWXML] v20261006 §5.34
- Office version: 2019
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 4 | 10 | 0 | 2 | 1 | 0 | 6 | 0 |

Full detail (content models, attribute types, defaults): [a3danim.json](a3danim.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`a3danim:animPr`](#a3danimanimpr) | `aanim:CT_AnimationProperties` | 7 | 1 | 1 | MS-ODRAWXML §2.37.3.1 p.317 |
| [`a3danim:embedAnim`](#a3danimembedanim) ◆ | `a3danim:CT_EmbeddedAnimation` | 1 | 2 | 0 | MS-ODRAWXML §2.36.1.2 p.314 |
| [`a3danim:extLst`](#a3danimextlst) | `a:CT_OfficeArtExtensionList` | 0 | 1 | 1 |  |
| [`a3danim:posterFrame`](#a3danimposterframe) ◆ | `a3danim:CT_PosterFrame` | 2 | 0 | 0 | MS-ODRAWXML §2.36.1.1 p.314 |

◆ declared globally (can be the root element of a part)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `a3danim:ST_EmbeddedAnimationID` | restriction of xsd:unsignedInt |  | MS-ODRAWXML §2.36.4.1 p.316 |

## Element details

### a3danim:animPr

- Type: `aanim:CT_AnimationProperties`
- Attributes: `auto`, `count`, `end`, `length`!, `name`, `offset`, `st`
- Children: `aanim:extLst`
- Parents: `a3danim:embedAnim`
- Specification: MS-ODRAWXML §2.37.3.1 p.317

### a3danim:embedAnim

- Type: `a3danim:CT_EmbeddedAnimation`
- Attributes: `animId`!
- Children: `a3danim:animPr`, `a3danim:extLst`
- Specification: MS-ODRAWXML §2.36.1.2 p.314

### a3danim:extLst

- Type: `a:CT_OfficeArtExtensionList`
- Children: `a:ext`
- Parents: `a3danim:embedAnim`

### a3danim:posterFrame

- Type: `a3danim:CT_PosterFrame`
- Attributes: `animId`!, `frame`
- Specification: MS-ODRAWXML §2.36.1.1 p.314

`!` marks required attributes.
