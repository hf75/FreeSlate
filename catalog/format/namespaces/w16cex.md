# w16cex: WordprocessingML (DOCX)

`http://schemas.microsoft.com/office/word/2018/wordml/cex`

- Source: [MS-DOCX] v20260818 §5.6
- Office version: 2021
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 3 | 3 | 0 | 2 | 0 | 0 | 4 | 0 |

Full detail (content models, attribute types, defaults): [w16cex.json](w16cex.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`w16cex:commentExtensible`](#w16cexcommentextensible) | `w16cex:CT_CommentExtensible` | 3 | 1 | 1 | MS-DOCX §2.10.3.1 p.90 |
| [`w16cex:commentsExtensible`](#w16cexcommentsextensible) ◆ | `w16cex:CT_CommentsExtensible` | 0 | 2 | 0 | MS-DOCX §2.10.1.1 p.90 |
| [`w16cex:extLst`](#w16cexextlst) | `w16cur:CT_ExtensionList` | 0 | 1 | 2 | MS-DOCX §2.9.3.2 p.89 |

◆ declared globally (can be the root element of a part)

## Element details

### w16cex:commentExtensible

- Type: `w16cex:CT_CommentExtensible`
- Attributes: `w16cex:dateUtc`, `w16cex:durableId`!, `w16cex:intelligentPlaceholder`
- Children: `w16cex:extLst`
- Parents: `w16cex:commentsExtensible`
- Specification: MS-DOCX §2.10.3.1 p.90

### w16cex:commentsExtensible

- Type: `w16cex:CT_CommentsExtensible`
- Children: `w16cex:commentExtensible`, `w16cex:extLst`
- Specification: MS-DOCX §2.10.1.1 p.90

### w16cex:extLst

- Type: `w16cur:CT_ExtensionList`
- Children: `w16cur:ext`
- Parents: `w16cex:commentExtensible`, `w16cex:commentsExtensible`
- Specification: MS-DOCX §2.9.3.2 p.89

`!` marks required attributes.
