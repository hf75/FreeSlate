# aink: DrawingML

`http://schemas.microsoft.com/office/drawing/2016/ink`

- Source: [MS-ODRAWXML] v20261006 §5.28
- Office version: 2019
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 | 0 | 0 | 0 | 2 | 14 | 2 | 0 |

Full detail (content models, attribute types, defaults): [aink.json](aink.json)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `aink:ST_ExtendedBrushPropertyName` | restriction of xsd:string | `inkEffects`, `anchorX`, `anchorY`, `scaleFactor` | MS-ODRAWXML §2.30.4.1 p.294 |
| `aink:ST_InkEffectsType` | restriction of xsd:string | `none`, `pencil`, `rainbow`, `galaxy`, `gold`, `silver`, `lava`, `ocean`, `rosegold`, `bronze` | MS-ODRAWXML §2.30.4.2 p.295 |

