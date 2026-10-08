# w16cei: WordprocessingML (DOCX)

`http://schemas.microsoft.com/office/word/2026/wordml/cei`

- Source: [MS-DOCX] v20260818 §5.10
- Office version: ?
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 2 | 1 | 0 | 1 | 0 | 0 | 3 | 0 |

Full detail (content models, attribute types, defaults): [w16cei.json](w16cei.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`w16cei:commentEntityInfo`](#w16ceicommententityinfo) ◆ | `w16cei:CT_CommentEntityInfo` | 1 | 1 | 0 | MS-DOCX §2.14.1.1 p.93 |
| [`w16cei:extLst`](#w16ceiextlst) | `w16cur:CT_ExtensionList` | 0 | 1 | 1 | MS-DOCX §2.9.3.2 p.89 |

◆ declared globally (can be the root element of a part)

## Element details

### w16cei:commentEntityInfo

- Type: `w16cei:CT_CommentEntityInfo`
- Attributes: `w16cei:entityType`
- Children: `w16cei:extLst`
- Specification: MS-DOCX §2.14.1.1 p.93

### w16cei:extLst

- Type: `w16cur:CT_ExtensionList`
- Children: `w16cur:ext`
- Parents: `w16cei:commentEntityInfo`
- Specification: MS-DOCX §2.9.3.2 p.89

`!` marks required attributes.
