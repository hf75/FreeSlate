# cp: Packages and document properties

`http://schemas.openxmlformats.org/package/2006/metadata/core-properties`

- Source: ECMA-376-2 schema opc-coreProperties.xsd
- Office version: 2007
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 9 | 2 | 0 | 3 | 0 | 0 | 0 | 0 |

Full detail (content models, attribute types, defaults): [cp.json](cp.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`cp:category`](#cpcategory) | `xsd:string` | 0 | 0 | 1 |  |
| [`cp:contentStatus`](#cpcontentstatus) | `xsd:string` | 0 | 0 | 1 |  |
| [`cp:coreProperties`](#cpcoreproperties) ◆ | `cp:CT_CoreProperties` | 0 | 15 | 0 |  |
| [`cp:keywords`](#cpkeywords) | `cp:CT_Keywords` | 1 | 1 | 1 |  |
| [`cp:lastModifiedBy`](#cplastmodifiedby) | `xsd:string` | 0 | 0 | 1 |  |
| [`cp:lastPrinted`](#cplastprinted) | `xsd:dateTime` | 0 | 0 | 1 |  |
| [`cp:revision`](#cprevision) | `xsd:string` | 0 | 0 | 1 |  |
| [`cp:value`](#cpvalue) | `cp:CT_Keyword` | 1 | 0 | 1 |  |
| [`cp:version`](#cpversion) | `xsd:string` | 0 | 0 | 1 |  |

◆ declared globally (can be the root element of a part)

## Element details

### cp:category

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `cp:coreProperties`

### cp:contentStatus

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `cp:coreProperties`

### cp:coreProperties

- Type: `cp:CT_CoreProperties`
- Children: `cp:category`, `cp:contentStatus`, `dcterms:created`, `dc:creator`, `dc:description`, `dc:identifier`, `cp:keywords`, `dc:language`, `cp:lastModifiedBy`, `cp:lastPrinted`, `dcterms:modified`, `cp:revision`, `dc:subject`, `dc:title`, `cp:version`

### cp:keywords

- Type: `cp:CT_Keywords`
- Attributes: `xml:lang`
- Children: `cp:value`
- Parents: `cp:coreProperties`

### cp:lastModifiedBy

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `cp:coreProperties`

### cp:lastPrinted

- Type: `xsd:dateTime` (text: `xsd:dateTime`)
- Parents: `cp:coreProperties`

### cp:revision

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `cp:coreProperties`

### cp:value

- Type: `cp:CT_Keyword` (text: `xsd:string`)
- Attributes: `xml:lang`
- Parents: `cp:keywords`

### cp:version

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `cp:coreProperties`

`!` marks required attributes.
