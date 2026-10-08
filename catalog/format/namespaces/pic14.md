# pic14: DrawingML

`http://schemas.microsoft.com/office/drawing/2010/picture`

- Source: [MS-ODRAWXML] v20261006 §5.12
- Office version: 2010
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 2 | 0 | 0 | 0 | 0 | 0 | 2 | 0 |

Full detail (content models, attribute types, defaults): [pic14.json](pic14.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`pic14:extLst`](#pic14extlst) ◆ | `a:CT_OfficeArtExtensionList` | 0 | 1 | 0 | MS-ODRAWXML §2.14.1.1 p.160 |
| [`pic14:style`](#pic14style) ◆ | `a:CT_ShapeStyle` | 0 | 4 | 0 | MS-ODRAWXML §2.14.1.2 p.160 |

◆ declared globally (can be the root element of a part)

## Element details

### pic14:extLst

- Type: `a:CT_OfficeArtExtensionList`
- Children: `a:ext`
- Specification: MS-ODRAWXML §2.14.1.1 p.160

### pic14:style

- Type: `a:CT_ShapeStyle`
- Children: `a:lnRef`, `a:fillRef`, `a:effectRef`, `a:fontRef`
- Specification: MS-ODRAWXML §2.14.1.2 p.160

`!` marks required attributes.
