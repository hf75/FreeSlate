# vt: Packages and document properties

`http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes`

- Source: ECMA-376-4 transitional schema shared-documentPropertiesVariantTypes.xsd
- Office version: 2007
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 34 | 6 | 0 | 6 | 4 | 37 | 38 | 9 |

Full detail (content models, attribute types, defaults): [vt.json](vt.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`vt:array`](#vtarray) ◆ | `vt:CT_Array` | 3 | 17 | 2 | §22.4.2.1 p.3744, OI 2.1.1725 |
| [`vt:blob`](#vtblob) ◆ | `xsd:base64Binary` | 0 | 0 | 3 | §22.4.2.2 p.3745 |
| [`vt:bool`](#vtbool) ◆ | `xsd:boolean` | 0 | 0 | 4 | §22.4.2.3 p.3745 |
| [`vt:bstr`](#vtbstr) ◆ | `xsd:string` | 0 | 0 | 4 | §22.4.2.4 p.3746 |
| [`vt:clsid`](#vtclsid) ◆ | `s:ST_Guid` | 0 | 0 | 3 | §22.4.2.5 p.3746, OI 2.1.1726 |
| [`vt:cy`](#vtcy) ◆ | `vt:ST_Cy` | 0 | 0 | 4 | §22.4.2.6 p.3746 |
| [`vt:date`](#vtdate) ◆ | `xsd:dateTime` | 0 | 0 | 4 | §22.4.2.7 p.3746, OI 2.1.1727 |
| [`vt:decimal`](#vtdecimal) ◆ | `xsd:decimal` | 0 | 0 | 3 | §22.4.2.8 p.3746 |
| [`vt:empty`](#vtempty) ◆ | `vt:CT_Empty` | 0 | 0 | 2 | §22.4.2.9 p.3746 |
| [`vt:error`](#vterror) ◆ | `vt:ST_Error` | 0 | 0 | 4 | §22.4.2.10 p.3747 |
| [`vt:filetime`](#vtfiletime) ◆ | `xsd:dateTime` | 0 | 0 | 3 | §22.4.2.11 p.3747, OI 2.1.1728 |
| [`vt:i1`](#vti1) ◆ | `xsd:byte` | 0 | 0 | 4 | §22.4.2.12 p.3747 |
| [`vt:i2`](#vti2) ◆ | `xsd:short` | 0 | 0 | 4 | §22.4.2.13 p.3747 |
| [`vt:i4`](#vti4) ◆ | `xsd:int` | 0 | 0 | 4 | §22.4.2.14 p.3747 |
| [`vt:i8`](#vti8) ◆ | `xsd:long` | 0 | 0 | 3 | §22.4.2.15 p.3747 |
| [`vt:int`](#vtint) ◆ | `xsd:int` | 0 | 0 | 3 | §22.4.2.16 p.3747 |
| [`vt:lpstr`](#vtlpstr) ◆ | `xsd:string` | 0 | 0 | 3 | §22.4.2.17 p.3747 |
| [`vt:lpwstr`](#vtlpwstr) ◆ | `xsd:string` | 0 | 0 | 3 | §22.4.2.18 p.3748 |
| [`vt:null`](#vtnull) ◆ | `vt:CT_Null` | 0 | 0 | 2 | §22.4.2.19 p.3748, OI 2.1.1729 |
| [`vt:oblob`](#vtoblob) ◆ | `xsd:base64Binary` | 0 | 0 | 2 | §22.4.2.20 p.3748 |
| [`vt:ostorage`](#vtostorage) ◆ | `xsd:base64Binary` | 0 | 0 | 2 | §22.4.2.21 p.3748, OI 2.1.1730 |
| [`vt:ostream`](#vtostream) ◆ | `xsd:base64Binary` | 0 | 0 | 2 | §22.4.2.22 p.3748 |
| [`vt:r4`](#vtr4) ◆ | `xsd:float` | 0 | 0 | 4 | §22.4.2.23 p.3749 |
| [`vt:r8`](#vtr8) ◆ | `xsd:double` | 0 | 0 | 4 | §22.4.2.24 p.3749 |
| [`vt:storage`](#vtstorage) ◆ | `xsd:base64Binary` | 0 | 0 | 2 | §22.4.2.25 p.3749, OI 2.1.1731 |
| [`vt:stream`](#vtstream) ◆ | `xsd:base64Binary` | 0 | 0 | 2 | §22.4.2.26 p.3749 |
| [`vt:ui1`](#vtui1) ◆ | `xsd:unsignedByte` | 0 | 0 | 4 | §22.4.2.27 p.3749 |
| [`vt:ui2`](#vtui2) ◆ | `xsd:unsignedShort` | 0 | 0 | 4 | §22.4.2.28 p.3749 |
| [`vt:ui4`](#vtui4) ◆ | `xsd:unsignedInt` | 0 | 0 | 4 | §22.4.2.29 p.3749 |
| [`vt:ui8`](#vtui8) ◆ | `xsd:unsignedLong` | 0 | 0 | 3 | §22.4.2.30 p.3749 |
| [`vt:uint`](#vtuint) ◆ | `xsd:unsignedInt` | 0 | 0 | 3 | §22.4.2.31 p.3750 |
| [`vt:variant`](#vtvariant) ◆ | `vt:CT_Variant` | 0 | 34 | 3 | §22.4.2.32 p.3750 |
| [`vt:vector`](#vtvector) ◆ | `vt:CT_Vector` | 2 | 20 | 5 | §22.4.2.33 p.3750 |
| [`vt:vstream`](#vtvstream) ◆ | `vt:CT_Vstream` | 1 | 0 | 2 | §22.4.2.34 p.3751, OI 2.1.1732 |

◆ declared globally (can be the root element of a part)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `vt:ST_ArrayBaseType` | restriction of xsd:string | `variant`, `i1`, `i2`, `i4`, `int`, `ui1`, `ui2`, `ui4`, `uint`, `r4`, `r8`, `decimal` … (+5) | §22.4.3.1 p.3751 |
| `vt:ST_Cy` | restriction of xsd:string | pattern `\s*[0-9]*\.[0-9]{4}\s*` | §22.4.3.2 p.3752 |
| `vt:ST_Error` | restriction of xsd:string | pattern `\s*0x[0-9A-Za-z]{8}\s*` | §22.4.3.3 p.3753, OI 2.1.1733 |
| `vt:ST_VectorBaseType` | restriction of xsd:string | `variant`, `i1`, `i2`, `i4`, `i8`, `ui1`, `ui2`, `ui4`, `ui8`, `r4`, `r8`, `lpstr` … (+8) | §22.4.3.4 p.3753 |

## Element details

### vt:array

- Type: `vt:CT_Array`
- Attributes: `baseType`!, `lBounds`!, `uBounds`!
- Children: `vt:variant`, `vt:i1`, `vt:i2`, `vt:i4`, `vt:int`, `vt:ui1`, `vt:ui2`, `vt:ui4`, `vt:uint`, `vt:r4`, `vt:r8`, `vt:decimal`, `vt:bstr`, `vt:date`, `vt:bool`, `vt:error`, `vt:cy`
- Parents: `op:property`, `vt:variant`
- Specification: §22.4.2.1 p.3744, OI 2.1.1725

### vt:blob

- Type: `xsd:base64Binary` (text: `xsd:base64Binary`)
- Parents: `ap:DigSig`, `op:property`, `vt:variant`
- Specification: §22.4.2.2 p.3745

### vt:bool

- Type: `xsd:boolean` (text: `xsd:boolean`)
- Parents: `op:property`, `vt:array`, `vt:variant`, `vt:vector`
- Specification: §22.4.2.3 p.3745

### vt:bstr

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `op:property`, `vt:array`, `vt:variant`, `vt:vector`
- Specification: §22.4.2.4 p.3746

### vt:clsid

- Type: `s:ST_Guid` (text: `s:ST_Guid`)
- Parents: `op:property`, `vt:variant`, `vt:vector`
- Specification: §22.4.2.5 p.3746, OI 2.1.1726

### vt:cy

- Type: `vt:ST_Cy` (text: `vt:ST_Cy`)
- Parents: `op:property`, `vt:array`, `vt:variant`, `vt:vector`
- Specification: §22.4.2.6 p.3746

### vt:date

- Type: `xsd:dateTime` (text: `xsd:dateTime`)
- Parents: `op:property`, `vt:array`, `vt:variant`, `vt:vector`
- Specification: §22.4.2.7 p.3746, OI 2.1.1727

### vt:decimal

- Type: `xsd:decimal` (text: `xsd:decimal`)
- Parents: `op:property`, `vt:array`, `vt:variant`
- Specification: §22.4.2.8 p.3746

### vt:empty

- Type: `vt:CT_Empty`
- Parents: `op:property`, `vt:variant`
- Specification: §22.4.2.9 p.3746

### vt:error

- Type: `vt:ST_Error` (text: `vt:ST_Error`)
- Parents: `op:property`, `vt:array`, `vt:variant`, `vt:vector`
- Specification: §22.4.2.10 p.3747

### vt:filetime

- Type: `xsd:dateTime` (text: `xsd:dateTime`)
- Parents: `op:property`, `vt:variant`, `vt:vector`
- Specification: §22.4.2.11 p.3747, OI 2.1.1728

### vt:i1

- Type: `xsd:byte` (text: `xsd:byte`)
- Parents: `op:property`, `vt:array`, `vt:variant`, `vt:vector`
- Specification: §22.4.2.12 p.3747

### vt:i2

- Type: `xsd:short` (text: `xsd:short`)
- Parents: `op:property`, `vt:array`, `vt:variant`, `vt:vector`
- Specification: §22.4.2.13 p.3747

### vt:i4

- Type: `xsd:int` (text: `xsd:int`)
- Parents: `op:property`, `vt:array`, `vt:variant`, `vt:vector`
- Specification: §22.4.2.14 p.3747

### vt:i8

- Type: `xsd:long` (text: `xsd:long`)
- Parents: `op:property`, `vt:variant`, `vt:vector`
- Specification: §22.4.2.15 p.3747

### vt:int

- Type: `xsd:int` (text: `xsd:int`)
- Parents: `op:property`, `vt:array`, `vt:variant`
- Specification: §22.4.2.16 p.3747

### vt:lpstr

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `op:property`, `vt:variant`, `vt:vector`
- Specification: §22.4.2.17 p.3747

### vt:lpwstr

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `op:property`, `vt:variant`, `vt:vector`
- Specification: §22.4.2.18 p.3748

### vt:null

- Type: `vt:CT_Null`
- Parents: `op:property`, `vt:variant`
- Specification: §22.4.2.19 p.3748, OI 2.1.1729

### vt:oblob

- Type: `xsd:base64Binary` (text: `xsd:base64Binary`)
- Parents: `op:property`, `vt:variant`
- Specification: §22.4.2.20 p.3748

### vt:ostorage

- Type: `xsd:base64Binary` (text: `xsd:base64Binary`)
- Parents: `op:property`, `vt:variant`
- Specification: §22.4.2.21 p.3748, OI 2.1.1730

### vt:ostream

- Type: `xsd:base64Binary` (text: `xsd:base64Binary`)
- Parents: `op:property`, `vt:variant`
- Specification: §22.4.2.22 p.3748

### vt:r4

- Type: `xsd:float` (text: `xsd:float`)
- Parents: `op:property`, `vt:array`, `vt:variant`, `vt:vector`
- Specification: §22.4.2.23 p.3749

### vt:r8

- Type: `xsd:double` (text: `xsd:double`)
- Parents: `op:property`, `vt:array`, `vt:variant`, `vt:vector`
- Specification: §22.4.2.24 p.3749

### vt:storage

- Type: `xsd:base64Binary` (text: `xsd:base64Binary`)
- Parents: `op:property`, `vt:variant`
- Specification: §22.4.2.25 p.3749, OI 2.1.1731

### vt:stream

- Type: `xsd:base64Binary` (text: `xsd:base64Binary`)
- Parents: `op:property`, `vt:variant`
- Specification: §22.4.2.26 p.3749

### vt:ui1

- Type: `xsd:unsignedByte` (text: `xsd:unsignedByte`)
- Parents: `op:property`, `vt:array`, `vt:variant`, `vt:vector`
- Specification: §22.4.2.27 p.3749

### vt:ui2

- Type: `xsd:unsignedShort` (text: `xsd:unsignedShort`)
- Parents: `op:property`, `vt:array`, `vt:variant`, `vt:vector`
- Specification: §22.4.2.28 p.3749

### vt:ui4

- Type: `xsd:unsignedInt` (text: `xsd:unsignedInt`)
- Parents: `op:property`, `vt:array`, `vt:variant`, `vt:vector`
- Specification: §22.4.2.29 p.3749

### vt:ui8

- Type: `xsd:unsignedLong` (text: `xsd:unsignedLong`)
- Parents: `op:property`, `vt:variant`, `vt:vector`
- Specification: §22.4.2.30 p.3749

### vt:uint

- Type: `xsd:unsignedInt` (text: `xsd:unsignedInt`)
- Parents: `op:property`, `vt:array`, `vt:variant`
- Specification: §22.4.2.31 p.3750

### vt:variant

- Type: `vt:CT_Variant`
- Children: `vt:variant`, `vt:vector`, `vt:array`, `vt:blob`, `vt:oblob`, `vt:empty`, `vt:null`, `vt:i1`, `vt:i2`, `vt:i4`, `vt:i8`, `vt:int`, `vt:ui1`, `vt:ui2`, `vt:ui4`, `vt:ui8`, `vt:uint`, `vt:r4`, `vt:r8`, `vt:decimal`, `vt:lpstr`, `vt:lpwstr`, `vt:bstr`, `vt:date`, `vt:filetime`, `vt:bool`, `vt:cy`, `vt:error`, `vt:stream`, `vt:ostream`, `vt:storage`, `vt:ostorage`, `vt:vstream`, `vt:clsid`
- Parents: `vt:array`, `vt:variant`, `vt:vector`
- Specification: §22.4.2.32 p.3750

### vt:vector

- Type: `vt:CT_Vector`
- Attributes: `baseType`!, `size`!
- Children: `vt:variant`, `vt:i1`, `vt:i2`, `vt:i4`, `vt:i8`, `vt:ui1`, `vt:ui2`, `vt:ui4`, `vt:ui8`, `vt:r4`, `vt:r8`, `vt:lpstr`, `vt:lpwstr`, `vt:bstr`, `vt:date`, `vt:filetime`, `vt:bool`, `vt:cy`, `vt:error`, `vt:clsid`
- Parents: `ap:HLinks`, `ap:HeadingPairs`, `ap:TitlesOfParts`, `op:property`, `vt:variant`
- Specification: §22.4.2.33 p.3750

### vt:vstream

- Type: `vt:CT_Vstream` (text: `xsd:base64Binary`)
- Attributes: `version`
- Parents: `op:property`, `vt:variant`
- Specification: §22.4.2.34 p.3751, OI 2.1.1732

`!` marks required attributes.
