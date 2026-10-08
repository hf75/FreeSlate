# alf: DrawingML

`http://schemas.microsoft.com/office/drawing/2021/livefeed`

- Source: [MS-ODRAWXML] v20261006 §5.40
- Office version: 2021
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 7 | 0 | 0 | 6 | 0 | 0 | 7 | 0 |

Full detail (content models, attribute types, defaults): [alf.json](alf.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`alf:Blur`](#alfblur) | `alf:CT_BackgroundBlurProperties` | 0 | 1 | 1 | MS-ODRAWXML §2.42.3.1 p.327 |
| [`alf:Custom`](#alfcustom) | `alf:CT_BackgroundCustomProperties` | 0 | 1 | 1 | MS-ODRAWXML §2.42.3.2 p.327 |
| [`alf:Normal`](#alfnormal) | `alf:CT_BackgroundNormalProperties` | 0 | 1 | 1 | MS-ODRAWXML §2.42.3.3 p.328 |
| [`alf:Removed`](#alfremoved) | `alf:CT_BackgroundRemovedProperties` | 0 | 1 | 1 | MS-ODRAWXML §2.42.3.4 p.328 |
| [`alf:backgroundProps`](#alfbackgroundprops) | `alf:CT_LiveFeedBackgroundProperties` | 0 | 5 | 1 | MS-ODRAWXML §2.42.3.5 p.328 |
| [`alf:extLst`](#alfextlst) | `a:CT_OfficeArtExtensionList` | 0 | 1 | 6 |  |
| [`alf:liveFeedProps`](#alflivefeedprops) ◆ | `alf:CT_LiveFeedProperties` | 0 | 2 | 0 | MS-ODRAWXML §2.42.1.1 p.326 |

◆ declared globally (can be the root element of a part)

## Element details

### alf:Blur

- Type: `alf:CT_BackgroundBlurProperties`
- Children: `alf:extLst`
- Parents: `alf:backgroundProps`
- Specification: MS-ODRAWXML §2.42.3.1 p.327

### alf:Custom

- Type: `alf:CT_BackgroundCustomProperties`
- Children: `alf:extLst`
- Parents: `alf:backgroundProps`
- Specification: MS-ODRAWXML §2.42.3.2 p.327

### alf:Normal

- Type: `alf:CT_BackgroundNormalProperties`
- Children: `alf:extLst`
- Parents: `alf:backgroundProps`
- Specification: MS-ODRAWXML §2.42.3.3 p.328

### alf:Removed

- Type: `alf:CT_BackgroundRemovedProperties`
- Children: `alf:extLst`
- Parents: `alf:backgroundProps`
- Specification: MS-ODRAWXML §2.42.3.4 p.328

### alf:backgroundProps

- Type: `alf:CT_LiveFeedBackgroundProperties`
- Children: `alf:Normal`, `alf:Removed`, `alf:Blur`, `alf:Custom`, `alf:extLst`
- Parents: `alf:liveFeedProps`
- Specification: MS-ODRAWXML §2.42.3.5 p.328

### alf:extLst

- Type: `a:CT_OfficeArtExtensionList`
- Children: `a:ext`
- Parents: `alf:Blur`, `alf:Custom`, `alf:Normal`, `alf:Removed`, `alf:backgroundProps`, `alf:liveFeedProps`

### alf:liveFeedProps

- Type: `alf:CT_LiveFeedProperties`
- Children: `alf:backgroundProps`, `alf:extLst`
- Specification: MS-ODRAWXML §2.42.1.1 p.326

`!` marks required attributes.
