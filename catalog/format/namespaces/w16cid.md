# w16cid: WordprocessingML (DOCX)

`http://schemas.microsoft.com/office/word/2016/wordml/cid`

- Source: [MS-DOCX] v20260818 §5.4
- Office version: 2019
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 2 | 2 | 1 | 2 | 0 | 0 | 5 | 0 |

Full detail (content models, attribute types, defaults): [w16cid.json](w16cid.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`w16cid:commentId`](#w16cidcommentid) | `w16cid:CT_CommentId` | 2 | 0 | 1 | MS-DOCX §2.8.3.1 p.88 |
| [`w16cid:commentsIds`](#w16cidcommentsids) ◆ | `w16cid:CT_CommentsIds` | 0 | 1 | 0 | MS-DOCX §2.8.1.1 p.87 |

◆ declared globally (can be the root element of a part)

## Global attributes

Attributes this namespace adds to elements of other namespaces.

| Attribute | Type | Specification |
|---|---|---|
| `w16cid:durableId` | `w:ST_LongHexNumber` | MS-DOCX §2.8.2.1 p.87; MS-DOCX §2.8.2.2 p.87 |

## Element details

### w16cid:commentId

- Type: `w16cid:CT_CommentId`
- Attributes: `w16cid:durableId`!, `w16cid:paraId`!
- Parents: `w16cid:commentsIds`
- Specification: MS-DOCX §2.8.3.1 p.88

### w16cid:commentsIds

- Type: `w16cid:CT_CommentsIds`
- Children: `w16cid:commentId`
- Specification: MS-DOCX §2.8.1.1 p.87

`!` marks required attributes.
