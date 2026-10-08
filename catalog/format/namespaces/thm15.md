# thm15: DrawingML

`http://schemas.microsoft.com/office/thememl/2012/main`

- Source: [MS-ODRAWXML] v20261006 §5.17
- Office version: 2013
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 2 | 3 | 1 | 1 | 0 | 0 | 3 | 0 |

Full detail (content models, attribute types, defaults): [thm15.json](thm15.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`thm15:extLst`](#thm15extlst) | `a:CT_OfficeArtExtensionList` | 0 | 1 | 1 |  |
| [`thm15:themeFamily`](#thm15themefamily) ◆ | `thm15:CT_ThemeFamily` | 3 | 1 | 0 | MS-ODRAWXML §2.4.1.1 p.91 |

◆ declared globally (can be the root element of a part)

## Global attributes

Attributes this namespace adds to elements of other namespaces.

| Attribute | Type | Specification |
|---|---|---|
| `thm15:id` | `a:ST_Guid` | MS-ODRAWXML §2.4.2.1 p.92 |

## Element details

### thm15:extLst

- Type: `a:CT_OfficeArtExtensionList`
- Children: `a:ext`
- Parents: `thm15:themeFamily`

### thm15:themeFamily

- Type: `thm15:CT_ThemeFamily`
- Attributes: `id`!, `name`!, `vid`!
- Children: `thm15:extLst`
- Specification: MS-ODRAWXML §2.4.1.1 p.91

`!` marks required attributes.
