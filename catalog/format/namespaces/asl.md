# asl: DrawingML

`http://schemas.microsoft.com/office/drawing/2021/scriptlink`

- Source: [MS-ODRAWXML] v20261006 §5.39
- Office version: M365
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 2 | 1 | 0 | 1 | 0 | 0 | 2 | 0 |

Full detail (content models, attribute types, defaults): [asl.json](asl.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`asl:extLst`](#aslextlst) | `a:CT_OfficeArtExtensionList` | 0 | 1 | 1 |  |
| [`asl:scriptLink`](#aslscriptlink) ◆ | `asl:CT_ScriptLink` | 1 | 1 | 0 | MS-ODRAWXML §2.41.1.1 p.325 |

◆ declared globally (can be the root element of a part)

## Element details

### asl:extLst

- Type: `a:CT_OfficeArtExtensionList`
- Children: `a:ext`
- Parents: `asl:scriptLink`

### asl:scriptLink

- Type: `asl:CT_ScriptLink`
- Attributes: `val`
- Children: `asl:extLst`
- Specification: MS-ODRAWXML §2.41.1.1 p.325

`!` marks required attributes.
