# iact: PresentationML (PPTX)

`http://schemas.microsoft.com/office/powerpoint/2014/inkAction`

- Source: [MS-ODRAWXML] v20261006 §5.19
- Office version: ?
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 7 | 16 | 0 | 6 | 12 | 12 | 19 | 0 |

Full detail (content models, attribute types, defaults): [iact.json](iact.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`iact:action`](#iactaction) | `iact:CT_Action` | 3 | 3 | 2 | MS-ODRAWXML §2.21.3.1 p.176 |
| [`iact:actionData`](#iactactiondata) | `iact:CT_ActionData` | 3 | 3 | 2 | MS-ODRAWXML §2.21.3.2 p.177 |
| [`iact:actionDataGroup`](#iactactiondatagroup) | `iact:CT_ActionDataGroup` | 2 | 1 | 1 | MS-ODRAWXML §2.21.3.3 p.177 |
| [`iact:actionGroup`](#iactactiongroup) | `iact:CT_ActionGroup` | 3 | 1 | 1 | MS-ODRAWXML §2.21.3.4 p.178 |
| [`iact:actions`](#iactactions) ◆ | `iact:CT_Actions` | 3 | 3 | 0 | MS-ODRAWXML §2.21.1.1 p.175 |
| [`iact:property`](#iactproperty) | `iact:CT_ActionProperty` | 2 | 0 | 1 | MS-ODRAWXML §2.21.3.5 p.178 |
| [`iact:transform`](#iacttransform) | `inkml:CT_Matrix` | 0 | 1 | 1 |  |

◆ declared globally (can be the root element of a part)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `iact:ST_ActionType` | union of iact:ST_ActionTypeReserved, iact:ST_ActionTypeUser |  | MS-ODRAWXML §2.21.4.1 p.180 |
| `iact:ST_ActionTypeReserved` | restriction of xsd:string | `add`, `remove`, `transform` | MS-ODRAWXML §2.21.4.2 p.180 |
| `iact:ST_ActionTypeUser` | restriction of xsd:string |  | MS-ODRAWXML §2.21.4.3 p.181 |
| `iact:ST_DataName` | union of iact:ST_DataNameReserved, iact:ST_DataNameUser |  | MS-ODRAWXML §2.21.4.4 p.181 |
| `iact:ST_DataNameReserved` | restriction of xsd:string | `stroke`, `path`, `target` | MS-ODRAWXML §2.21.4.5 p.181 |
| `iact:ST_DataNameUser` | restriction of xsd:string |  | MS-ODRAWXML §2.21.4.6 p.182 |
| `iact:ST_PropertyName` | union of iact:ST_PropertyNameReserved, iact:ST_PropertyNameUser |  | MS-ODRAWXML §2.21.4.7 p.182 |
| `iact:ST_PropertyNameReserved` | restriction of xsd:string | `dataType`, `style` | MS-ODRAWXML §2.21.4.8 p.182 |
| `iact:ST_PropertyNameUser` | restriction of xsd:string |  | MS-ODRAWXML §2.21.4.9 p.183 |
| `iact:ST_PropertyValue` | union of iact:ST_PropertyValueReserved, iact:ST_PropertyValueUser |  | MS-ODRAWXML §2.21.4.10 p.183 |
| `iact:ST_PropertyValueReserved` | restriction of xsd:string | `ink`, `pointEraser`, `strokeEraser`, `instant` | MS-ODRAWXML §2.21.4.11 p.183 |
| `iact:ST_PropertyValueUser` | restriction of xsd:string |  | MS-ODRAWXML §2.21.4.12 p.184 |

## Element details

### iact:action

- Type: `iact:CT_Action`
- Attributes: `startTime`!, `type`!, `xml:id`
- Children: `iact:property`, `iact:actionData`, `iact:actionDataGroup`
- Parents: `iact:actionGroup`, `iact:actions`
- Specification: MS-ODRAWXML §2.21.3.1 p.176

### iact:actionData

- Type: `iact:CT_ActionData`
- Attributes: `name`, `ref`, `xml:id`
- Children: `iact:transform`, `inkml:trace`, `inkml:traceView`
- Parents: `iact:action`, `iact:actionDataGroup`
- Specification: MS-ODRAWXML §2.21.3.2 p.177

### iact:actionDataGroup

- Type: `iact:CT_ActionDataGroup`
- Attributes: `name`, `xml:id`
- Children: `iact:actionData`
- Parents: `iact:action`
- Specification: MS-ODRAWXML §2.21.3.3 p.177

### iact:actionGroup

- Type: `iact:CT_ActionGroup`
- Attributes: `startTime`!, `type`!, `xml:id`
- Children: `iact:action`
- Parents: `iact:actions`
- Specification: MS-ODRAWXML §2.21.3.4 p.178

### iact:actions

- Type: `iact:CT_Actions`
- Attributes: `lengthUnit`!, `timeUnit`!, `xml:id`
- Children: `inkml:definitions`, `iact:actionGroup`, `iact:action`
- Specification: MS-ODRAWXML §2.21.1.1 p.175

### iact:property

- Type: `iact:CT_ActionProperty`
- Attributes: `name`!, `value`
- Parents: `iact:action`
- Specification: MS-ODRAWXML §2.21.3.5 p.178

### iact:transform

- Type: `inkml:CT_Matrix`
- Children: `any(##any)`
- Parents: `iact:actionData`

`!` marks required attributes.
