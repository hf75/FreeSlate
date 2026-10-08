# pc2: PresentationML (PPTX)

`http://schemas.microsoft.com/office/powerpoint/2019/9/main/command`

- Source: [MS-PPTX] v20240820 §5.15
- Office version: M365
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 2 | 0 | 0 | 4 | 0 | 0 | 6 | 0 |

Full detail (content models, attribute types, defaults): [pc2.json](pc2.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`pc2:cmMkLst`](#pc2cmmklst) ◆ | `pc2:CT_CommentV2MonikerList` | 0 | 1 | 1 | MS-PPTX §2.18.1.1 p.120 |
| [`pc2:cmRplyMkLst`](#pc2cmrplymklst) ◆ | `pc2:CT_CommentReplyV2MonikerList` | 0 | 1 | 1 | MS-PPTX §2.18.1.2 p.121 |

◆ declared globally (can be the root element of a part)

## Element details

### pc2:cmMkLst

- Type: `pc2:CT_CommentV2MonikerList`
- Children: `any(##any)`
- Parents: `pc226:cmChg`
- Specification: MS-PPTX §2.18.1.1 p.120

### pc2:cmRplyMkLst

- Type: `pc2:CT_CommentReplyV2MonikerList`
- Children: `any(##any)`
- Parents: `pc226:cmRplyChg`
- Specification: MS-PPTX §2.18.1.2 p.121

`!` marks required attributes.
