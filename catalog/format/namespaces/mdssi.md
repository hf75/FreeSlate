# mdssi: Packages and document properties

`http://schemas.openxmlformats.org/package/2006/digital-signature`

- Source: ECMA-376-2 schema opc-digSig.xsd
- Office version: 2007
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 5 | 2 | 0 | 3 | 2 | 0 | 0 | 0 |

Full detail (content models, attribute types, defaults): [mdssi.json](mdssi.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`mdssi:Format`](#mdssiformat) | `mdssi:ST_Format` | 0 | 0 | 1 |  |
| [`mdssi:RelationshipReference`](#mdssirelationshipreference) ◆ | `mdssi:CT_RelationshipReference` | 1 | 0 | 0 |  |
| [`mdssi:RelationshipsGroupReference`](#mdssirelationshipsgroupreference) ◆ | `mdssi:CT_RelationshipsGroupReference` | 1 | 0 | 0 |  |
| [`mdssi:SignatureTime`](#mdssisignaturetime) ◆ | `mdssi:CT_SignatureTime` | 0 | 2 | 0 |  |
| [`mdssi:Value`](#mdssivalue) | `mdssi:ST_Value` | 0 | 0 | 1 |  |

◆ declared globally (can be the root element of a part)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `mdssi:ST_Format` | restriction of xsd:string | pattern `(YYYY)\|(YYYY-MM)\|(YYYY-MM-DD)\|(YYYY-MM-DDThh:mmTZD)\|(YYYY-MM-DDThh:mm:ssTZD)\|(YYYY-MM-DDThh:mm:ss.sTZD)` |  |
| `mdssi:ST_Value` | restriction of xsd:string | pattern `(([0-9][0-9][0-9][0-9]))\|(([0-9][0-9][0-9][0-9])-((0[1-9])\|(1(0\|1\|2))))\|(([0-9][0-9][0-9][0-9])-((0[1-9])\|(1(0\|1\|2)))-((0[1-9])\|(1[0-9])\|(2[0-9])\|(3(0\|1))))\|(([0-9][0-9][0-9][0-9])-((0[1-9])\|(1(0\|1\|2)))-((0[1-9])\|(1[0-9])\|(2[0-9])\|(3(0\|1)))T((0[0-9])\|(1[0-9])\|(2(0\|1\|2\|3))):((0[0-9])\|(1[0-9])\|(2[0-9])\|(3[0-9])\|(4[0-9])\|(5[0-9]))(((\+\|-)((0[0-9])\|(1[0-9])\|(2(0\|1\|2\|3))):((0[0-9])\|(1[0-9])\|(2[0-9])\|(3[0-9])\|(4[0-9])\|(5[0-9])))\|Z))\|(([0-9][0-9][0-9][0-9])-((0[1-9])\|(1(0\|1\|2)))-((0[1-9])\|(1[0-9])\|(2[0-9])\|(3(0\|1)))T((0[0-9])\|(1[0-9])\|(2(0\|1\|2\|3))):((0[0-9])\|(1[0-9])\|(2[0-9])\|(3[0-9])\|(4[0-9])\|(5[0-9])):((0[0-9])\|(1[0-9])\|(2[0-9])\|(3[0-9])\|(4[0-9])\|(5[0-9]))(((\+\|-)((0[0-9])\|(1[0-9])\|(2(0\|1\|2\|3))):((0[0-9])\|(1[0-9])\|(2[0-9])\|(3[0-9])\|(4[0-9])\|(5[0-9])))\|Z))\|(([0-9][0-9][0-9][0-9])-((0[1-9])\|(1(0\|1\|2)))-((0[1-9])\|(1[0-9])\|(2[0-9])\|(3(0\|1)))T((0[0-9])\|(1[0-9])\|(2(0\|1\|2\|3))):((0[0-9])\|(1[0-9])\|(2[0-9])\|(3[0-9])\|(4[0-9])\|(5[0-9])):(((0[0-9])\|(1[0-9])\|(2[0-9])\|(3[0-9])\|(4[0-9])\|(5[0-9]))\.[0-9])(((\+\|-)((0[0-9])\|(1[0-9])\|(2(0\|1\|2\|3))):((0[0-9])\|(1[0-9])\|(2[0-9])\|(3[0-9])\|(4[0-9])\|(5[0-9])))\|Z))` |  |

## Element details

### mdssi:Format

- Type: `mdssi:ST_Format` (text: `mdssi:ST_Format`)
- Parents: `mdssi:SignatureTime`

### mdssi:RelationshipReference

- Type: `mdssi:CT_RelationshipReference` (text: `xsd:string`)
- Attributes: `SourceId`!

### mdssi:RelationshipsGroupReference

- Type: `mdssi:CT_RelationshipsGroupReference` (text: `xsd:string`)
- Attributes: `SourceType`!

### mdssi:SignatureTime

- Type: `mdssi:CT_SignatureTime`
- Children: `mdssi:Format`, `mdssi:Value`

### mdssi:Value

- Type: `mdssi:ST_Value` (text: `mdssi:ST_Value`)
- Parents: `mdssi:SignatureTime`

`!` marks required attributes.
