# ct: Packages and document properties

`http://schemas.openxmlformats.org/package/2006/content-types`

- Source: ECMA-376-2 schema opc-contentTypes.xsd
- Office version: 2007
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 3 | 4 | 0 | 3 | 2 | 0 | 0 | 0 |

Full detail (content models, attribute types, defaults): [ct.json](ct.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`ct:Default`](#ctdefault) ◆ | `ct:CT_Default` | 2 | 0 | 1 |  |
| [`ct:Override`](#ctoverride) ◆ | `ct:CT_Override` | 2 | 0 | 1 |  |
| [`ct:Types`](#cttypes) ◆ | `ct:CT_Types` | 0 | 2 | 0 |  |

◆ declared globally (can be the root element of a part)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `ct:ST_ContentType` | restriction of xsd:string | pattern `(((([\p{IsBasicLatin}-[\p{Cc}\(\)<>@,;:\\"/\[\]\?=\{\}\s\t]])+))/((([\p{IsBasicLatin}-[\p{Cc}\(\)<>@,;:\\"/\[\]\?=\{\}\s\t]])+))((\s+)*;(\s+)*(((([\p{IsBasicLatin}-[\p{Cc}\(\)<>@,;:\\"/\[\]\?=\{\}\s\t]])+))=((([\p{IsBasicLatin}-[\p{Cc}\(\)<>@,;:\\"/\[\]\?=\{\}\s\t]])+)\|("(([\p{IsLatin-1Supplement}\p{IsBasicLatin}-[\p{Cc}"\n\r]]\|(\s+))\|(\\[\p{IsBasicLatin}]))*"))))*)` |  |
| `ct:ST_Extension` | restriction of xsd:string | pattern `([!$&'\(\)\*\+,:=]\|(%[0-9a-fA-F][0-9a-fA-F])\|[:@]\|[a-zA-Z0-9\-_~])+` |  |

## Element details

### ct:Default

- Type: `ct:CT_Default`
- Attributes: `ContentType`!, `Extension`!
- Parents: `ct:Types`

### ct:Override

- Type: `ct:CT_Override`
- Attributes: `ContentType`!, `PartName`!
- Parents: `ct:Types`

### ct:Types

- Type: `ct:CT_Types`
- Children: `ct:Default`, `ct:Override`

`!` marks required attributes.
