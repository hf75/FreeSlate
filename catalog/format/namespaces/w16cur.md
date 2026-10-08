# w16cur: WordprocessingML (DOCX)

`http://schemas.microsoft.com/office/word/2018/wordml`

- Source: [MS-DOCX] v20260818 §5.5
- Office version: 2021
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 1 | 0 | 2 | 0 | 0 | 2 | 0 |

Full detail (content models, attribute types, defaults): [w16cur.json](w16cur.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`w16cur:ext`](#w16curext) | `w16cur:CT_Extension` | 1 | 1 | 2 | MS-DOCX §2.9.3.1 p.89 |

◆ declared globally (can be the root element of a part)

## Element details

### w16cur:ext

- Type: `w16cur:CT_Extension`
- Attributes: `w16cur:uri`
- Children: `any(##any)`
- Parents: `w16cei:extLst`, `w16cex:extLst`
- Specification: MS-DOCX §2.9.3.1 p.89

`!` marks required attributes.
