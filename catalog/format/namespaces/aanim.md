# aanim: DrawingML

`http://schemas.microsoft.com/office/drawing/2018/animation`

- Source: [MS-ODRAWXML] v20261006 §5.35
- Office version: 2019
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 2 | 7 | 0 | 1 | 4 | 1 | 6 | 0 |

Full detail (content models, attribute types, defaults): [aanim.json](aanim.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`aanim:animPr`](#aanimanimpr) ◆ | `aanim:CT_AnimationProperties` | 7 | 1 | 0 | MS-ODRAWXML §2.37.1.1 p.316 |
| [`aanim:extLst`](#aanimextlst) | `a:CT_OfficeArtExtensionList` | 0 | 1 | 2 |  |

◆ declared globally (can be the root element of a part)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `aanim:ST_AnimationName` | restriction of xsd:string |  | MS-ODRAWXML §2.37.4.1 p.318 |
| `aanim:ST_Indefinite` | restriction of xsd:token | `indefinite` | MS-ODRAWXML §2.37.4.2 p.318 |
| `aanim:ST_PlayCount` | union of xsd:unsignedInt, aanim:ST_Indefinite |  | MS-ODRAWXML §2.37.4.3 p.319 |
| `aanim:ST_UniversalTimeOffset` | restriction of xsd:string |  | MS-ODRAWXML §2.37.4.4 p.319 |

## Element details

### aanim:animPr

- Type: `aanim:CT_AnimationProperties`
- Attributes: `auto`, `count`, `end`, `length`!, `name`, `offset`, `st`
- Children: `aanim:extLst`
- Specification: MS-ODRAWXML §2.37.1.1 p.316

### aanim:extLst

- Type: `a:CT_OfficeArtExtensionList`
- Children: `a:ext`
- Parents: `a3danim:animPr`, `aanim:animPr`

`!` marks required attributes.
