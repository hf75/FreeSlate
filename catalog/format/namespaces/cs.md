# cs: Charts

`http://schemas.microsoft.com/office/drawing/2012/chartStyle`

- Source: [MS-ODRAWXML] v20261006 §5.15
- Office version: 2013
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 44 | 83 | 0 | 8 | 12 | 18 | 22 | 0 |

Full detail (content models, attribute types, defaults): [cs.json](cs.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`cs:axisTitle`](#csaxistitle) | `cs:CT_StyleEntry` | 1 | 9 | 1 | MS-ODRAWXML §2.8.3.7 p.128 |
| [`cs:bodyPr`](#csbodypr) | `a:CT_TextBodyProperties` | 19 | 8 | 30 |  |
| [`cs:categoryAxis`](#cscategoryaxis) | `cs:CT_StyleEntry` | 1 | 9 | 1 | MS-ODRAWXML §2.8.3.7 p.128 |
| [`cs:chartArea`](#cschartarea) | `cs:CT_StyleEntry` | 1 | 9 | 1 | MS-ODRAWXML §2.8.3.7 p.128 |
| [`cs:chartStyle`](#cschartstyle) ◆ | `cs:CT_ChartStyle` | 1 | 32 | 0 | MS-ODRAWXML §2.8.1.1 p.118 |
| [`cs:colorStyle`](#cscolorstyle) ◆ | `cs:CT_ColorStyle` | 2 | 8 | 0 | MS-ODRAWXML §2.8.1.2 p.118 |
| [`cs:dataLabel`](#csdatalabel) | `cs:CT_StyleEntry` | 1 | 9 | 1 | MS-ODRAWXML §2.8.3.7 p.128 |
| [`cs:dataLabelCallout`](#csdatalabelcallout) | `cs:CT_StyleEntry` | 1 | 9 | 1 | MS-ODRAWXML §2.8.3.7 p.128 |
| [`cs:dataPoint`](#csdatapoint) | `cs:CT_StyleEntry` | 1 | 9 | 1 | MS-ODRAWXML §2.8.3.7 p.128 |
| [`cs:dataPoint3D`](#csdatapoint3d) | `cs:CT_StyleEntry` | 1 | 9 | 1 | MS-ODRAWXML §2.8.3.7 p.128 |
| [`cs:dataPointLine`](#csdatapointline) | `cs:CT_StyleEntry` | 1 | 9 | 1 | MS-ODRAWXML §2.8.3.7 p.128 |
| [`cs:dataPointMarker`](#csdatapointmarker) | `cs:CT_StyleEntry` | 1 | 9 | 1 | MS-ODRAWXML §2.8.3.7 p.128 |
| [`cs:dataPointMarkerLayout`](#csdatapointmarkerlayout) | `cs:CT_MarkerLayout` | 2 | 0 | 1 | MS-ODRAWXML §2.8.3.5 p.125 |
| [`cs:dataPointWireframe`](#csdatapointwireframe) | `cs:CT_StyleEntry` | 1 | 9 | 1 | MS-ODRAWXML §2.8.3.7 p.128 |
| [`cs:dataTable`](#csdatatable) | `cs:CT_StyleEntry` | 1 | 9 | 1 | MS-ODRAWXML §2.8.3.7 p.128 |
| [`cs:defRPr`](#csdefrpr) | `a:CT_TextCharacterProperties` | 19 | 22 | 30 |  |
| [`cs:downBar`](#csdownbar) | `cs:CT_StyleEntry` | 1 | 9 | 1 | MS-ODRAWXML §2.8.3.7 p.128 |
| [`cs:dropLine`](#csdropline) | `cs:CT_StyleEntry` | 1 | 9 | 1 | MS-ODRAWXML §2.8.3.7 p.128 |
| [`cs:effectRef`](#cseffectref) | `cs:CT_StyleReference` | 2 | 7 | 30 | MS-ODRAWXML §2.8.3.8 p.129 |
| [`cs:errorBar`](#cserrorbar) | `cs:CT_StyleEntry` | 1 | 9 | 1 | MS-ODRAWXML §2.8.3.7 p.128 |
| [`cs:extLst`](#csextlst) | `a:CT_OfficeArtExtensionList` | 0 | 1 | 32 |  |
| [`cs:fillRef`](#csfillref) | `cs:CT_StyleReference` | 2 | 7 | 30 | MS-ODRAWXML §2.8.3.8 p.129 |
| [`cs:floor`](#csfloor) | `cs:CT_StyleEntry` | 1 | 9 | 1 | MS-ODRAWXML §2.8.3.7 p.128 |
| [`cs:fontRef`](#csfontref) | `cs:CT_FontReference` | 2 | 7 | 30 | MS-ODRAWXML §2.8.3.4 p.124 |
| [`cs:gridlineMajor`](#csgridlinemajor) | `cs:CT_StyleEntry` | 1 | 9 | 1 | MS-ODRAWXML §2.8.3.7 p.128 |
| [`cs:gridlineMinor`](#csgridlineminor) | `cs:CT_StyleEntry` | 1 | 9 | 1 | MS-ODRAWXML §2.8.3.7 p.128 |
| [`cs:hiLoLine`](#cshiloline) | `cs:CT_StyleEntry` | 1 | 9 | 1 | MS-ODRAWXML §2.8.3.7 p.128 |
| [`cs:leaderLine`](#csleaderline) | `cs:CT_StyleEntry` | 1 | 9 | 1 | MS-ODRAWXML §2.8.3.7 p.128 |
| [`cs:legend`](#cslegend) | `cs:CT_StyleEntry` | 1 | 9 | 1 | MS-ODRAWXML §2.8.3.7 p.128 |
| [`cs:lineWidthScale`](#cslinewidthscale) | `xsd:double` | 0 | 0 | 30 |  |
| [`cs:lnRef`](#cslnref) | `cs:CT_StyleReference` | 2 | 7 | 30 | MS-ODRAWXML §2.8.3.8 p.129 |
| [`cs:plotArea`](#csplotarea) | `cs:CT_StyleEntry` | 1 | 9 | 1 | MS-ODRAWXML §2.8.3.7 p.128 |
| [`cs:plotArea3D`](#csplotarea3d) | `cs:CT_StyleEntry` | 1 | 9 | 1 | MS-ODRAWXML §2.8.3.7 p.128 |
| [`cs:seriesAxis`](#csseriesaxis) | `cs:CT_StyleEntry` | 1 | 9 | 1 | MS-ODRAWXML §2.8.3.7 p.128 |
| [`cs:seriesLine`](#csseriesline) | `cs:CT_StyleEntry` | 1 | 9 | 1 | MS-ODRAWXML §2.8.3.7 p.128 |
| [`cs:spPr`](#cssppr) | `a:CT_ShapeProperties` | 1 | 15 | 30 |  |
| [`cs:styleClr`](#csstyleclr) | `cs:CT_StyleColor` | 1 | 28 | 4 | MS-ODRAWXML §2.8.3.6 p.125 |
| [`cs:title`](#cstitle) | `cs:CT_StyleEntry` | 1 | 9 | 1 | MS-ODRAWXML §2.8.3.7 p.128 |
| [`cs:trendline`](#cstrendline) | `cs:CT_StyleEntry` | 1 | 9 | 1 | MS-ODRAWXML §2.8.3.7 p.128 |
| [`cs:trendlineLabel`](#cstrendlinelabel) | `cs:CT_StyleEntry` | 1 | 9 | 1 | MS-ODRAWXML §2.8.3.7 p.128 |
| [`cs:upBar`](#csupbar) | `cs:CT_StyleEntry` | 1 | 9 | 1 | MS-ODRAWXML §2.8.3.7 p.128 |
| [`cs:valueAxis`](#csvalueaxis) | `cs:CT_StyleEntry` | 1 | 9 | 1 | MS-ODRAWXML §2.8.3.7 p.128 |
| [`cs:variation`](#csvariation) | `cs:CT_ColorStyleVariation` | 0 | 28 | 1 | MS-ODRAWXML §2.8.3.3 p.122 |
| [`cs:wall`](#cswall) | `cs:CT_StyleEntry` | 1 | 9 | 1 | MS-ODRAWXML §2.8.3.7 p.128 |

◆ declared globally (can be the root element of a part)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `cs:ST_ColorStyleMethod` | union of cs:ST_ColorStyleMethodEnum, xsd:string |  | MS-ODRAWXML §2.8.4.1 p.130 |
| `cs:ST_ColorStyleMethodEnum` | restriction of xsd:token | `cycle`, `withinLinear`, `acrossLinear`, `withinLinearReversed`, `acrossLinearReversed` | MS-ODRAWXML §2.8.4.2 p.130 |
| `cs:ST_MarkerSize` | restriction of xsd:unsignedByte | minInclusive `2`, maxInclusive `72` | MS-ODRAWXML §2.8.4.3 p.131 |
| `cs:ST_MarkerStyle` | restriction of xsd:token | `circle`, `dash`, `diamond`, `dot`, `plus`, `square`, `star`, `triangle`, `x` | MS-ODRAWXML §2.8.4.4 p.131 |
| `cs:ST_StyleColorEnum` | restriction of xsd:token | `auto` | MS-ODRAWXML §2.8.4.5 p.132 |
| `cs:ST_StyleColorVal` | union of xsd:unsignedInt, cs:ST_StyleColorEnum, xsd:string |  | MS-ODRAWXML §2.8.4.6 p.133 |
| `cs:ST_StyleEntryModifier` | union of cs:ST_StyleEntryModifierEnum, xsd:string |  | MS-ODRAWXML §2.8.4.7 p.133 |
| `cs:ST_StyleEntryModifierEnum` | restriction of xsd:token | `allowNoFillOverride`, `allowNoLineOverride` | MS-ODRAWXML §2.8.4.8 p.134 |
| `cs:ST_StyleEntryModifierList` | list of cs:ST_StyleEntryModifier |  | MS-ODRAWXML §2.8.4.9 p.134 |
| `cs:ST_StyleReferenceModifier` | union of cs:ST_StyleReferenceModifierEnum, xsd:string |  | MS-ODRAWXML §2.8.4.10 p.134 |
| `cs:ST_StyleReferenceModifierEnum` | restriction of xsd:token | `ignoreCSTransforms` | MS-ODRAWXML §2.8.4.11 p.135 |
| `cs:ST_StyleReferenceModifierList` | list of cs:ST_StyleReferenceModifier |  | MS-ODRAWXML §2.8.4.12 p.135 |

## Element details

### cs:axisTitle

- Type: `cs:CT_StyleEntry`
- Attributes: `mods`
- Children: `cs:lnRef`, `cs:lineWidthScale`, `cs:fillRef`, `cs:effectRef`, `cs:fontRef`, `cs:spPr`, `cs:defRPr`, `cs:bodyPr`, `cs:extLst`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.7 p.128

### cs:bodyPr

- Type: `a:CT_TextBodyProperties`
- Attributes: `anchor`, `anchorCtr`, `bIns`, `compatLnSpc`, `forceAA`, `fromWordArt`, `horzOverflow`, `lIns`, `numCol`, `rIns`, `rot`, `rtlCol`, `spcCol`, `spcFirstLastPara`, `tIns`, `upright`, `vert`, `vertOverflow`, `wrap`
- Children: `a:prstTxWarp`, `a:noAutofit`, `a:normAutofit`, `a:spAutoFit`, `a:scene3d`, `a:sp3d`, `a:flatTx`, `a:extLst`
- Parents: `cs:axisTitle`, `cs:categoryAxis`, `cs:chartArea`, `cs:dataLabel`, `cs:dataLabelCallout`, `cs:dataPoint`, `cs:dataPoint3D`, `cs:dataPointLine`, `cs:dataPointMarker`, `cs:dataPointWireframe`, `cs:dataTable`, `cs:downBar`, `cs:dropLine`, `cs:errorBar`, `cs:floor`, `cs:gridlineMajor`, `cs:gridlineMinor`, `cs:hiLoLine`, `cs:leaderLine`, `cs:legend`, `cs:plotArea`, `cs:plotArea3D`, `cs:seriesAxis`, `cs:seriesLine`, `cs:title`, `cs:trendline`, `cs:trendlineLabel`, `cs:upBar`, `cs:valueAxis`, `cs:wall`

### cs:categoryAxis

- Type: `cs:CT_StyleEntry`
- Attributes: `mods`
- Children: `cs:lnRef`, `cs:lineWidthScale`, `cs:fillRef`, `cs:effectRef`, `cs:fontRef`, `cs:spPr`, `cs:defRPr`, `cs:bodyPr`, `cs:extLst`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.7 p.128

### cs:chartArea

- Type: `cs:CT_StyleEntry`
- Attributes: `mods`
- Children: `cs:lnRef`, `cs:lineWidthScale`, `cs:fillRef`, `cs:effectRef`, `cs:fontRef`, `cs:spPr`, `cs:defRPr`, `cs:bodyPr`, `cs:extLst`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.7 p.128

### cs:chartStyle

- Type: `cs:CT_ChartStyle`
- Attributes: `id`
- Children: `cs:axisTitle`, `cs:categoryAxis`, `cs:chartArea`, `cs:dataLabel`, `cs:dataLabelCallout`, `cs:dataPoint`, `cs:dataPoint3D`, `cs:dataPointLine`, `cs:dataPointMarker`, `cs:dataPointMarkerLayout`, `cs:dataPointWireframe`, `cs:dataTable`, `cs:downBar`, `cs:dropLine`, `cs:errorBar`, `cs:floor`, `cs:gridlineMajor`, `cs:gridlineMinor`, `cs:hiLoLine`, `cs:leaderLine`, `cs:legend`, `cs:plotArea`, `cs:plotArea3D`, `cs:seriesAxis`, `cs:seriesLine`, `cs:title`, `cs:trendline`, `cs:trendlineLabel`, `cs:upBar`, `cs:valueAxis`, `cs:wall`, `cs:extLst`
- Specification: MS-ODRAWXML §2.8.1.1 p.118

### cs:colorStyle

- Type: `cs:CT_ColorStyle`
- Attributes: `id`, `meth`!
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`, `cs:variation`, `cs:extLst`
- Specification: MS-ODRAWXML §2.8.1.2 p.118

### cs:dataLabel

- Type: `cs:CT_StyleEntry`
- Attributes: `mods`
- Children: `cs:lnRef`, `cs:lineWidthScale`, `cs:fillRef`, `cs:effectRef`, `cs:fontRef`, `cs:spPr`, `cs:defRPr`, `cs:bodyPr`, `cs:extLst`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.7 p.128

### cs:dataLabelCallout

- Type: `cs:CT_StyleEntry`
- Attributes: `mods`
- Children: `cs:lnRef`, `cs:lineWidthScale`, `cs:fillRef`, `cs:effectRef`, `cs:fontRef`, `cs:spPr`, `cs:defRPr`, `cs:bodyPr`, `cs:extLst`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.7 p.128

### cs:dataPoint

- Type: `cs:CT_StyleEntry`
- Attributes: `mods`
- Children: `cs:lnRef`, `cs:lineWidthScale`, `cs:fillRef`, `cs:effectRef`, `cs:fontRef`, `cs:spPr`, `cs:defRPr`, `cs:bodyPr`, `cs:extLst`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.7 p.128

### cs:dataPoint3D

- Type: `cs:CT_StyleEntry`
- Attributes: `mods`
- Children: `cs:lnRef`, `cs:lineWidthScale`, `cs:fillRef`, `cs:effectRef`, `cs:fontRef`, `cs:spPr`, `cs:defRPr`, `cs:bodyPr`, `cs:extLst`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.7 p.128

### cs:dataPointLine

- Type: `cs:CT_StyleEntry`
- Attributes: `mods`
- Children: `cs:lnRef`, `cs:lineWidthScale`, `cs:fillRef`, `cs:effectRef`, `cs:fontRef`, `cs:spPr`, `cs:defRPr`, `cs:bodyPr`, `cs:extLst`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.7 p.128

### cs:dataPointMarker

- Type: `cs:CT_StyleEntry`
- Attributes: `mods`
- Children: `cs:lnRef`, `cs:lineWidthScale`, `cs:fillRef`, `cs:effectRef`, `cs:fontRef`, `cs:spPr`, `cs:defRPr`, `cs:bodyPr`, `cs:extLst`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.7 p.128

### cs:dataPointMarkerLayout

- Type: `cs:CT_MarkerLayout`
- Attributes: `size`, `symbol`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.5 p.125

### cs:dataPointWireframe

- Type: `cs:CT_StyleEntry`
- Attributes: `mods`
- Children: `cs:lnRef`, `cs:lineWidthScale`, `cs:fillRef`, `cs:effectRef`, `cs:fontRef`, `cs:spPr`, `cs:defRPr`, `cs:bodyPr`, `cs:extLst`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.7 p.128

### cs:dataTable

- Type: `cs:CT_StyleEntry`
- Attributes: `mods`
- Children: `cs:lnRef`, `cs:lineWidthScale`, `cs:fillRef`, `cs:effectRef`, `cs:fontRef`, `cs:spPr`, `cs:defRPr`, `cs:bodyPr`, `cs:extLst`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.7 p.128

### cs:defRPr

- Type: `a:CT_TextCharacterProperties`
- Attributes: `altLang`, `b`, `baseline`, `bmk`, `cap`, `dirty`, `err`, `i`, `kern`, `kumimoji`, `lang`, `noProof`, `normalizeH`, `smtClean`, `smtId`, `spc`, `strike`, `sz`, `u`
- Children: `a:ln`, `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`, `a:effectLst`, `a:effectDag`, `a:highlight`, `a:uLnTx`, `a:uLn`, `a:uFillTx`, `a:uFill`, `a:latin`, `a:ea`, `a:cs`, `a:sym`, `a:hlinkClick`, `a:hlinkMouseOver`, `a:rtl`, `a:extLst`
- Parents: `cs:axisTitle`, `cs:categoryAxis`, `cs:chartArea`, `cs:dataLabel`, `cs:dataLabelCallout`, `cs:dataPoint`, `cs:dataPoint3D`, `cs:dataPointLine`, `cs:dataPointMarker`, `cs:dataPointWireframe`, `cs:dataTable`, `cs:downBar`, `cs:dropLine`, `cs:errorBar`, `cs:floor`, `cs:gridlineMajor`, `cs:gridlineMinor`, `cs:hiLoLine`, `cs:leaderLine`, `cs:legend`, `cs:plotArea`, `cs:plotArea3D`, `cs:seriesAxis`, `cs:seriesLine`, `cs:title`, `cs:trendline`, `cs:trendlineLabel`, `cs:upBar`, `cs:valueAxis`, `cs:wall`

### cs:downBar

- Type: `cs:CT_StyleEntry`
- Attributes: `mods`
- Children: `cs:lnRef`, `cs:lineWidthScale`, `cs:fillRef`, `cs:effectRef`, `cs:fontRef`, `cs:spPr`, `cs:defRPr`, `cs:bodyPr`, `cs:extLst`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.7 p.128

### cs:dropLine

- Type: `cs:CT_StyleEntry`
- Attributes: `mods`
- Children: `cs:lnRef`, `cs:lineWidthScale`, `cs:fillRef`, `cs:effectRef`, `cs:fontRef`, `cs:spPr`, `cs:defRPr`, `cs:bodyPr`, `cs:extLst`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.7 p.128

### cs:effectRef

- Type: `cs:CT_StyleReference`
- Attributes: `idx`!, `mods`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`, `cs:styleClr`
- Parents: `cs:axisTitle`, `cs:categoryAxis`, `cs:chartArea`, `cs:dataLabel`, `cs:dataLabelCallout`, `cs:dataPoint`, `cs:dataPoint3D`, `cs:dataPointLine`, `cs:dataPointMarker`, `cs:dataPointWireframe`, `cs:dataTable`, `cs:downBar`, `cs:dropLine`, `cs:errorBar`, `cs:floor`, `cs:gridlineMajor`, `cs:gridlineMinor`, `cs:hiLoLine`, `cs:leaderLine`, `cs:legend`, `cs:plotArea`, `cs:plotArea3D`, `cs:seriesAxis`, `cs:seriesLine`, `cs:title`, `cs:trendline`, `cs:trendlineLabel`, `cs:upBar`, `cs:valueAxis`, `cs:wall`
- Specification: MS-ODRAWXML §2.8.3.8 p.129

### cs:errorBar

- Type: `cs:CT_StyleEntry`
- Attributes: `mods`
- Children: `cs:lnRef`, `cs:lineWidthScale`, `cs:fillRef`, `cs:effectRef`, `cs:fontRef`, `cs:spPr`, `cs:defRPr`, `cs:bodyPr`, `cs:extLst`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.7 p.128

### cs:extLst

- Type: `a:CT_OfficeArtExtensionList`
- Children: `a:ext`
- Parents: `cs:axisTitle`, `cs:categoryAxis`, `cs:chartArea`, `cs:chartStyle`, `cs:colorStyle`, `cs:dataLabel`, `cs:dataLabelCallout`, `cs:dataPoint`, `cs:dataPoint3D`, `cs:dataPointLine`, `cs:dataPointMarker`, `cs:dataPointWireframe`, `cs:dataTable`, `cs:downBar`, `cs:dropLine`, `cs:errorBar`, `cs:floor`, `cs:gridlineMajor`, `cs:gridlineMinor`, `cs:hiLoLine`, `cs:leaderLine`, `cs:legend`, `cs:plotArea`, `cs:plotArea3D`, `cs:seriesAxis`, `cs:seriesLine`, `cs:title`, `cs:trendline`, `cs:trendlineLabel`, `cs:upBar`, `cs:valueAxis`, `cs:wall`

### cs:fillRef

- Type: `cs:CT_StyleReference`
- Attributes: `idx`!, `mods`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`, `cs:styleClr`
- Parents: `cs:axisTitle`, `cs:categoryAxis`, `cs:chartArea`, `cs:dataLabel`, `cs:dataLabelCallout`, `cs:dataPoint`, `cs:dataPoint3D`, `cs:dataPointLine`, `cs:dataPointMarker`, `cs:dataPointWireframe`, `cs:dataTable`, `cs:downBar`, `cs:dropLine`, `cs:errorBar`, `cs:floor`, `cs:gridlineMajor`, `cs:gridlineMinor`, `cs:hiLoLine`, `cs:leaderLine`, `cs:legend`, `cs:plotArea`, `cs:plotArea3D`, `cs:seriesAxis`, `cs:seriesLine`, `cs:title`, `cs:trendline`, `cs:trendlineLabel`, `cs:upBar`, `cs:valueAxis`, `cs:wall`
- Specification: MS-ODRAWXML §2.8.3.8 p.129

### cs:floor

- Type: `cs:CT_StyleEntry`
- Attributes: `mods`
- Children: `cs:lnRef`, `cs:lineWidthScale`, `cs:fillRef`, `cs:effectRef`, `cs:fontRef`, `cs:spPr`, `cs:defRPr`, `cs:bodyPr`, `cs:extLst`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.7 p.128

### cs:fontRef

- Type: `cs:CT_FontReference`
- Attributes: `idx`!, `mods`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`, `cs:styleClr`
- Parents: `cs:axisTitle`, `cs:categoryAxis`, `cs:chartArea`, `cs:dataLabel`, `cs:dataLabelCallout`, `cs:dataPoint`, `cs:dataPoint3D`, `cs:dataPointLine`, `cs:dataPointMarker`, `cs:dataPointWireframe`, `cs:dataTable`, `cs:downBar`, `cs:dropLine`, `cs:errorBar`, `cs:floor`, `cs:gridlineMajor`, `cs:gridlineMinor`, `cs:hiLoLine`, `cs:leaderLine`, `cs:legend`, `cs:plotArea`, `cs:plotArea3D`, `cs:seriesAxis`, `cs:seriesLine`, `cs:title`, `cs:trendline`, `cs:trendlineLabel`, `cs:upBar`, `cs:valueAxis`, `cs:wall`
- Specification: MS-ODRAWXML §2.8.3.4 p.124

### cs:gridlineMajor

- Type: `cs:CT_StyleEntry`
- Attributes: `mods`
- Children: `cs:lnRef`, `cs:lineWidthScale`, `cs:fillRef`, `cs:effectRef`, `cs:fontRef`, `cs:spPr`, `cs:defRPr`, `cs:bodyPr`, `cs:extLst`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.7 p.128

### cs:gridlineMinor

- Type: `cs:CT_StyleEntry`
- Attributes: `mods`
- Children: `cs:lnRef`, `cs:lineWidthScale`, `cs:fillRef`, `cs:effectRef`, `cs:fontRef`, `cs:spPr`, `cs:defRPr`, `cs:bodyPr`, `cs:extLst`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.7 p.128

### cs:hiLoLine

- Type: `cs:CT_StyleEntry`
- Attributes: `mods`
- Children: `cs:lnRef`, `cs:lineWidthScale`, `cs:fillRef`, `cs:effectRef`, `cs:fontRef`, `cs:spPr`, `cs:defRPr`, `cs:bodyPr`, `cs:extLst`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.7 p.128

### cs:leaderLine

- Type: `cs:CT_StyleEntry`
- Attributes: `mods`
- Children: `cs:lnRef`, `cs:lineWidthScale`, `cs:fillRef`, `cs:effectRef`, `cs:fontRef`, `cs:spPr`, `cs:defRPr`, `cs:bodyPr`, `cs:extLst`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.7 p.128

### cs:legend

- Type: `cs:CT_StyleEntry`
- Attributes: `mods`
- Children: `cs:lnRef`, `cs:lineWidthScale`, `cs:fillRef`, `cs:effectRef`, `cs:fontRef`, `cs:spPr`, `cs:defRPr`, `cs:bodyPr`, `cs:extLst`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.7 p.128

### cs:lineWidthScale

- Type: `xsd:double` (text: `xsd:double`)
- Parents: `cs:axisTitle`, `cs:categoryAxis`, `cs:chartArea`, `cs:dataLabel`, `cs:dataLabelCallout`, `cs:dataPoint`, `cs:dataPoint3D`, `cs:dataPointLine`, `cs:dataPointMarker`, `cs:dataPointWireframe`, `cs:dataTable`, `cs:downBar`, `cs:dropLine`, `cs:errorBar`, `cs:floor`, `cs:gridlineMajor`, `cs:gridlineMinor`, `cs:hiLoLine`, `cs:leaderLine`, `cs:legend`, `cs:plotArea`, `cs:plotArea3D`, `cs:seriesAxis`, `cs:seriesLine`, `cs:title`, `cs:trendline`, `cs:trendlineLabel`, `cs:upBar`, `cs:valueAxis`, `cs:wall`

### cs:lnRef

- Type: `cs:CT_StyleReference`
- Attributes: `idx`!, `mods`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`, `cs:styleClr`
- Parents: `cs:axisTitle`, `cs:categoryAxis`, `cs:chartArea`, `cs:dataLabel`, `cs:dataLabelCallout`, `cs:dataPoint`, `cs:dataPoint3D`, `cs:dataPointLine`, `cs:dataPointMarker`, `cs:dataPointWireframe`, `cs:dataTable`, `cs:downBar`, `cs:dropLine`, `cs:errorBar`, `cs:floor`, `cs:gridlineMajor`, `cs:gridlineMinor`, `cs:hiLoLine`, `cs:leaderLine`, `cs:legend`, `cs:plotArea`, `cs:plotArea3D`, `cs:seriesAxis`, `cs:seriesLine`, `cs:title`, `cs:trendline`, `cs:trendlineLabel`, `cs:upBar`, `cs:valueAxis`, `cs:wall`
- Specification: MS-ODRAWXML §2.8.3.8 p.129

### cs:plotArea

- Type: `cs:CT_StyleEntry`
- Attributes: `mods`
- Children: `cs:lnRef`, `cs:lineWidthScale`, `cs:fillRef`, `cs:effectRef`, `cs:fontRef`, `cs:spPr`, `cs:defRPr`, `cs:bodyPr`, `cs:extLst`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.7 p.128

### cs:plotArea3D

- Type: `cs:CT_StyleEntry`
- Attributes: `mods`
- Children: `cs:lnRef`, `cs:lineWidthScale`, `cs:fillRef`, `cs:effectRef`, `cs:fontRef`, `cs:spPr`, `cs:defRPr`, `cs:bodyPr`, `cs:extLst`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.7 p.128

### cs:seriesAxis

- Type: `cs:CT_StyleEntry`
- Attributes: `mods`
- Children: `cs:lnRef`, `cs:lineWidthScale`, `cs:fillRef`, `cs:effectRef`, `cs:fontRef`, `cs:spPr`, `cs:defRPr`, `cs:bodyPr`, `cs:extLst`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.7 p.128

### cs:seriesLine

- Type: `cs:CT_StyleEntry`
- Attributes: `mods`
- Children: `cs:lnRef`, `cs:lineWidthScale`, `cs:fillRef`, `cs:effectRef`, `cs:fontRef`, `cs:spPr`, `cs:defRPr`, `cs:bodyPr`, `cs:extLst`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.7 p.128

### cs:spPr

- Type: `a:CT_ShapeProperties`
- Attributes: `bwMode`
- Children: `a:xfrm`, `a:custGeom`, `a:prstGeom`, `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`, `a:ln`, `a:effectLst`, `a:effectDag`, `a:scene3d`, `a:sp3d`, `a:extLst`
- Parents: `cs:axisTitle`, `cs:categoryAxis`, `cs:chartArea`, `cs:dataLabel`, `cs:dataLabelCallout`, `cs:dataPoint`, `cs:dataPoint3D`, `cs:dataPointLine`, `cs:dataPointMarker`, `cs:dataPointWireframe`, `cs:dataTable`, `cs:downBar`, `cs:dropLine`, `cs:errorBar`, `cs:floor`, `cs:gridlineMajor`, `cs:gridlineMinor`, `cs:hiLoLine`, `cs:leaderLine`, `cs:legend`, `cs:plotArea`, `cs:plotArea3D`, `cs:seriesAxis`, `cs:seriesLine`, `cs:title`, `cs:trendline`, `cs:trendlineLabel`, `cs:upBar`, `cs:valueAxis`, `cs:wall`

### cs:styleClr

- Type: `cs:CT_StyleColor`
- Attributes: `val`
- Children: `a:tint`, `a:shade`, `a:comp`, `a:inv`, `a:gray`, `a:alpha`, `a:alphaOff`, `a:alphaMod`, `a:hue`, `a:hueOff`, `a:hueMod`, `a:sat`, `a:satOff`, `a:satMod`, `a:lum`, `a:lumOff`, `a:lumMod`, `a:red`, `a:redOff`, `a:redMod`, `a:green`, `a:greenOff`, `a:greenMod`, `a:blue`, `a:blueOff`, `a:blueMod`, `a:gamma`, `a:invGamma`
- Parents: `cs:effectRef`, `cs:fillRef`, `cs:fontRef`, `cs:lnRef`
- Specification: MS-ODRAWXML §2.8.3.6 p.125

### cs:title

- Type: `cs:CT_StyleEntry`
- Attributes: `mods`
- Children: `cs:lnRef`, `cs:lineWidthScale`, `cs:fillRef`, `cs:effectRef`, `cs:fontRef`, `cs:spPr`, `cs:defRPr`, `cs:bodyPr`, `cs:extLst`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.7 p.128

### cs:trendline

- Type: `cs:CT_StyleEntry`
- Attributes: `mods`
- Children: `cs:lnRef`, `cs:lineWidthScale`, `cs:fillRef`, `cs:effectRef`, `cs:fontRef`, `cs:spPr`, `cs:defRPr`, `cs:bodyPr`, `cs:extLst`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.7 p.128

### cs:trendlineLabel

- Type: `cs:CT_StyleEntry`
- Attributes: `mods`
- Children: `cs:lnRef`, `cs:lineWidthScale`, `cs:fillRef`, `cs:effectRef`, `cs:fontRef`, `cs:spPr`, `cs:defRPr`, `cs:bodyPr`, `cs:extLst`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.7 p.128

### cs:upBar

- Type: `cs:CT_StyleEntry`
- Attributes: `mods`
- Children: `cs:lnRef`, `cs:lineWidthScale`, `cs:fillRef`, `cs:effectRef`, `cs:fontRef`, `cs:spPr`, `cs:defRPr`, `cs:bodyPr`, `cs:extLst`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.7 p.128

### cs:valueAxis

- Type: `cs:CT_StyleEntry`
- Attributes: `mods`
- Children: `cs:lnRef`, `cs:lineWidthScale`, `cs:fillRef`, `cs:effectRef`, `cs:fontRef`, `cs:spPr`, `cs:defRPr`, `cs:bodyPr`, `cs:extLst`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.7 p.128

### cs:variation

- Type: `cs:CT_ColorStyleVariation`
- Children: `a:tint`, `a:shade`, `a:comp`, `a:inv`, `a:gray`, `a:alpha`, `a:alphaOff`, `a:alphaMod`, `a:hue`, `a:hueOff`, `a:hueMod`, `a:sat`, `a:satOff`, `a:satMod`, `a:lum`, `a:lumOff`, `a:lumMod`, `a:red`, `a:redOff`, `a:redMod`, `a:green`, `a:greenOff`, `a:greenMod`, `a:blue`, `a:blueOff`, `a:blueMod`, `a:gamma`, `a:invGamma`
- Parents: `cs:colorStyle`
- Specification: MS-ODRAWXML §2.8.3.3 p.122

### cs:wall

- Type: `cs:CT_StyleEntry`
- Attributes: `mods`
- Children: `cs:lnRef`, `cs:lineWidthScale`, `cs:fillRef`, `cs:effectRef`, `cs:fontRef`, `cs:spPr`, `cs:defRPr`, `cs:bodyPr`, `cs:extLst`
- Parents: `cs:chartStyle`
- Specification: MS-ODRAWXML §2.8.3.7 p.128

`!` marks required attributes.
