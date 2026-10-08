# msink: DrawingML

`http://schemas.microsoft.com/ink/2010/main`

- Source: [MS-ODRAWXML] v20261006 §5.7
- Office version: ?
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 4 | 26 | 0 | 3 | 9 | 37 | 13 | 0 |

Full detail (content models, attribute types, defaults): [msink.json](msink.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`msink:context`](#msinkcontext) ◆ | `msink:CT_CtxNode` | 21 | 3 | 0 | MS-ODRAWXML §2.11.1.1 p.142 |
| [`msink:destinationLink`](#msinkdestinationlink) | `msink:CT_CtxLink` | 2 | 0 | 1 | MS-ODRAWXML §2.11.3.1 p.142 |
| [`msink:property`](#msinkproperty) | `msink:CT_Property` | 1 | 0 | 1 | MS-ODRAWXML §2.11.3.3 p.145 |
| [`msink:sourceLink`](#msinksourcelink) | `msink:CT_CtxLink` | 2 | 0 | 1 | MS-ODRAWXML §2.11.3.1 p.142 |

◆ declared globally (can be the root element of a part)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `msink:ST_CtxNodeType` | union of msink:ST_KnownCtxNodeType, msink:ST_Guid |  | MS-ODRAWXML §2.11.4.1 p.146 |
| `msink:ST_Dir` | restriction of xsd:string | `to`, `from`, `with` | MS-ODRAWXML §2.11.4.2 p.146 |
| `msink:ST_Guid` | restriction of xsd:token | pattern `\{[0-9A-F]{8}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{12}\}` | MS-ODRAWXML §2.11.4.3 p.147 |
| `msink:ST_KnownCtxNodeType` | restriction of xsd:string | `root`, `unclassifiedInk`, `writingRegion`, `analysisHint`, `object`, `inkDrawing`, `image`, `paragraph`, `line`, `inkBullet`, `inkWord`, `textWord` … (+11) | MS-ODRAWXML §2.11.4.4 p.147 |
| `msink:ST_KnownSemanticType` | restriction of xsd:string | `none`, `underline`, `strikethrough`, `highlight`, `scratchOut`, `verticalRange`, `callout`, `enclosure`, `comment`, `container`, `connector` | MS-ODRAWXML §2.11.4.5 p.149 |
| `msink:ST_Point` | restriction of xsd:string | pattern `-?[0-9]+,-?[0-9]+` | MS-ODRAWXML §2.11.4.6 p.150 |
| `msink:ST_Points` | list of msink:ST_Point |  | MS-ODRAWXML §2.11.4.7 p.150 |
| `msink:ST_Ref` | union of msink:ST_Guid, xsd:unsignedInt |  | MS-ODRAWXML §2.11.4.8 p.150 |
| `msink:ST_SemanticType` | union of msink:ST_KnownSemanticType, xsd:unsignedInt |  | MS-ODRAWXML §2.11.4.9 p.151 |

## Element details

### msink:context

- Type: `msink:CT_CtxNode`
- Attributes: `alignmentLevel`, `ascender`, `baseline`, `beginModifierType`, `centroid`, `contentType`, `customRecognizerId`, `descender`, `endModifierType`, `hotPoints`, `id`, `mathML`, `mathStruct`, `mathSymbol`, `midline`, `rotatedBoundingBox`, `rotationAngle`, `semanticType`, `shapeGeometry`, `shapeName`, `type`!
- Children: `msink:property`, `msink:sourceLink`, `msink:destinationLink`
- Specification: MS-ODRAWXML §2.11.1.1 p.142

### msink:destinationLink

- Type: `msink:CT_CtxLink`
- Attributes: `direction`, `ref`
- Parents: `msink:context`
- Specification: MS-ODRAWXML §2.11.3.1 p.142

### msink:property

- Type: `msink:CT_Property` (text: `xsd:hexBinary`)
- Attributes: `type`
- Parents: `msink:context`
- Specification: MS-ODRAWXML §2.11.3.3 p.145

### msink:sourceLink

- Type: `msink:CT_CtxLink`
- Attributes: `direction`, `ref`
- Parents: `msink:context`
- Specification: MS-ODRAWXML §2.11.3.1 p.142

`!` marks required attributes.
