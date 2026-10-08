# op: Packages and document properties

`http://schemas.openxmlformats.org/officeDocument/2006/custom-properties`

- Source: ECMA-376-4 transitional schema shared-documentPropertiesCustom.xsd
- Office version: 2007
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 2 | 4 | 0 | 2 | 0 | 0 | 2 | 1 |

Full detail (content models, attribute types, defaults): [op.json](op.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`op:Properties`](#opproperties) ◆ | `op:CT_Properties` | 0 | 1 | 0 | §22.3.2.1 p.3742 |
| [`op:property`](#opproperty) | `op:CT_Property` | 4 | 33 | 1 | §22.3.2.2 p.3742, OI 2.1.1724 |

◆ declared globally (can be the root element of a part)

## Element details

### op:Properties

- Type: `op:CT_Properties`
- Children: `op:property`
- Specification: §22.3.2.1 p.3742

### op:property

- Type: `op:CT_Property`
- Attributes: `fmtid`!, `linkTarget`, `name`, `pid`!
- Children: `vt:vector`, `vt:array`, `vt:blob`, `vt:oblob`, `vt:empty`, `vt:null`, `vt:i1`, `vt:i2`, `vt:i4`, `vt:i8`, `vt:int`, `vt:ui1`, `vt:ui2`, `vt:ui4`, `vt:ui8`, `vt:uint`, `vt:r4`, `vt:r8`, `vt:decimal`, `vt:lpstr`, `vt:lpwstr`, `vt:bstr`, `vt:date`, `vt:filetime`, `vt:bool`, `vt:cy`, `vt:error`, `vt:stream`, `vt:ostream`, `vt:storage`, `vt:ostorage`, `vt:vstream`, `vt:clsid`
- Parents: `op:Properties`
- Specification: §22.3.2.2 p.3742, OI 2.1.1724

`!` marks required attributes.
