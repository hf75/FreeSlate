# cx: Charts

`http://schemas.microsoft.com/office/drawing/2014/chartex`

- Source: [MS-ODRAWXML] v20261006 §5.22
- Office version: 2016
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 118 | 150 | 0 | 99 | 26 | 92 | 127 | 0 |

Full detail (content models, attribute types, defaults): [cx.json](cx.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`cx:address`](#cxaddress) | `cx:CT_Address` | 7 | 0 | 1 | MS-ODRAWXML §2.24.3.1 p.194 |
| [`cx:aggregation`](#cxaggregation) | `cx:CT_Aggregation` | 0 | 0 | 1 | MS-ODRAWXML §2.24.3.2 p.194 |
| [`cx:artifact`](#cxartifact) | `cx:CT_Artifact` | 3 | 0 | 1 | MS-ODRAWXML §2.24.3.3 p.195 |
| [`cx:artifacts`](#cxartifacts) | `cx:CT_Artifacts` | 0 | 2 | 1 | MS-ODRAWXML §2.24.3.4 p.195 |
| [`cx:axis`](#cxaxis) | `cx:CT_Axis` | 2 | 13 | 1 | MS-ODRAWXML §2.24.3.5 p.196 |
| [`cx:axisId`](#cxaxisid) | `cx:ST_AxisId` | 0 | 0 | 1 |  |
| [`cx:binCount`](#cxbincount) | `xsd:unsignedInt` | 0 | 0 | 1 |  |
| [`cx:binSize`](#cxbinsize) | `xsd:double` | 0 | 0 | 1 |  |
| [`cx:binary`](#cxbinary) | `xsd:base64Binary` | 0 | 0 | 1 |  |
| [`cx:binding`](#cxbinding) | `cx:CT_Binding` | 3 | 0 | 1 | MS-ODRAWXML §2.24.3.9 p.199 |
| [`cx:bindings`](#cxbindings) | `cx:CT_Bindings` | 0 | 2 | 1 | MS-ODRAWXML §2.24.3.10 p.199 |
| [`cx:binning`](#cxbinning) | `cx:CT_Binning` | 3 | 2 | 1 | MS-ODRAWXML §2.24.3.11 p.200 |
| [`cx:catScaling`](#cxcatscaling) | `cx:CT_CategoryAxisScaling` | 1 | 0 | 1 | MS-ODRAWXML §2.24.3.12 p.201 |
| [`cx:chart`](#cxchart) ◆ | `cx:CT_RelId`, `cx:CT_Chart` | 1 | 4 | 1 | MS-ODRAWXML §2.24.1.1 p.193 |
| [`cx:chartData`](#cxchartdata) | `cx:CT_ChartData` | 0 | 3 | 1 | MS-ODRAWXML §2.24.3.14 p.202 |
| [`cx:chartSpace`](#cxchartspace) ◆ | `cx:CT_ChartSpace` | 3 | 9 | 0 | MS-ODRAWXML §2.24.1.2 p.193 |
| [`cx:clear`](#cxclear) | `cx:CT_Clear` | 0 | 5 | 1 | MS-ODRAWXML §2.24.3.17 p.204 |
| [`cx:clrMapOvr`](#cxclrmapovr) | `a:CT_ColorMapping` | 12 | 1 | 1 |  |
| [`cx:copyright`](#cxcopyright) | `xsd:string` | 0 | 0 | 1 |  |
| [`cx:copyrights`](#cxcopyrights) | `cx:CT_Copyrights` | 0 | 1 | 1 | MS-ODRAWXML §2.24.3.18 p.205 |
| [`cx:dashboard`](#cxdashboard) | `cx:CT_Dashboard` | 1 | 3 | 1 | MS-ODRAWXML §2.24.3.19 p.205 |
| [`cx:data`](#cxdata) | `cx:CT_Data` | 1 | 3 | 1 | MS-ODRAWXML §2.24.3.20 p.206 |
| [`cx:dataId`](#cxdataid) | `cx:CT_DataId` | 1 | 0 | 1 | MS-ODRAWXML §2.24.3.21 p.207 |
| [`cx:dataLabel`](#cxdatalabel) | `cx:CT_DataLabel` | 2 | 6 | 1 | MS-ODRAWXML §2.24.3.22 p.207 |
| [`cx:dataLabelHidden`](#cxdatalabelhidden) | `cx:CT_DataLabelHidden` | 1 | 0 | 1 | MS-ODRAWXML §2.24.3.23 p.208 |
| [`cx:dataLabels`](#cxdatalabels) | `cx:CT_DataLabels` | 1 | 8 | 1 | MS-ODRAWXML §2.24.3.24 p.208 |
| [`cx:dataPt`](#cxdatapt) | `cx:CT_DataPoint` | 1 | 2 | 1 | MS-ODRAWXML §2.24.3.26 p.210 |
| [`cx:entityType`](#cxentitytype) | `cx:ST_EntityType` | 0 | 0 | 1 |  |
| [`cx:evenFooter`](#cxevenfooter) | `xsd:string` | 0 | 0 | 1 |  |
| [`cx:evenHeader`](#cxevenheader) | `xsd:string` | 0 | 0 | 1 |  |
| [`cx:ext`](#cxext) | `cx:CT_Extension` | 1 | 1 | 1 | MS-ODRAWXML §2.24.3.27 p.210 |
| [`cx:extLst`](#cxextlst) | `cx:CT_ExtensionList` | 0 | 1 | 26 | MS-ODRAWXML §2.24.3.28 p.211 |
| [`cx:externalData`](#cxexternaldata) | `cx:CT_ExternalData` | 2 | 0 | 1 | MS-ODRAWXML §2.24.3.29 p.211 |
| [`cx:extremeValue`](#cxextremevalue) | `cx:CT_ExtremeValueColorPosition` | 0 | 0 | 2 | MS-ODRAWXML §2.24.3.30 p.212 |
| [`cx:f`](#cxf) | `cx:CT_Formula` | 1 | 0 | 3 | MS-ODRAWXML §2.24.3.34 p.214 |
| [`cx:firstFooter`](#cxfirstfooter) | `xsd:string` | 0 | 0 | 1 |  |
| [`cx:firstHeader`](#cxfirstheader) | `xsd:string` | 0 | 0 | 1 |  |
| [`cx:fmtOvr`](#cxfmtovr) | `cx:CT_FormatOverride` | 1 | 2 | 1 | MS-ODRAWXML §2.24.3.32 p.213 |
| [`cx:fmtOvrs`](#cxfmtovrs) | `cx:CT_FormatOverrides` | 0 | 1 | 1 | MS-ODRAWXML §2.24.3.33 p.213 |
| [`cx:geoCache`](#cxgeocache) | `cx:CT_GeoCache` | 1 | 2 | 1 | MS-ODRAWXML §2.24.3.35 p.214 |
| [`cx:geoChildEntities`](#cxgeochildentities) | `cx:CT_GeoChildEntities` | 0 | 1 | 1 | MS-ODRAWXML §2.24.3.36 p.215 |
| [`cx:geoChildEntitiesQuery`](#cxgeochildentitiesquery) | `cx:CT_GeoChildEntitiesQuery` | 1 | 1 | 1 | MS-ODRAWXML §2.24.3.37 p.215 |
| [`cx:geoChildEntitiesQueryResult`](#cxgeochildentitiesqueryresult) | `cx:CT_GeoChildEntitiesQueryResult` | 0 | 2 | 1 | MS-ODRAWXML §2.24.3.38 p.216 |
| [`cx:geoChildEntitiesQueryResults`](#cxgeochildentitiesqueryresults) | `cx:CT_GeoChildEntitiesQueryResults` | 0 | 1 | 1 | MS-ODRAWXML §2.24.3.39 p.216 |
| [`cx:geoChildTypes`](#cxgeochildtypes) | `cx:CT_GeoChildTypes` | 0 | 1 | 1 | MS-ODRAWXML §2.24.3.40 p.217 |
| [`cx:geoData`](#cxgeodata) | `cx:CT_GeoData` | 6 | 2 | 1 | MS-ODRAWXML §2.24.3.41 p.217 |
| [`cx:geoDataEntityQuery`](#cxgeodataentityquery) | `cx:CT_GeoDataEntityQuery` | 2 | 0 | 1 | MS-ODRAWXML §2.24.3.42 p.218 |
| [`cx:geoDataEntityQueryResult`](#cxgeodataentityqueryresult) | `cx:CT_GeoDataEntityQueryResult` | 0 | 2 | 1 | MS-ODRAWXML §2.24.3.43 p.218 |
| [`cx:geoDataEntityQueryResults`](#cxgeodataentityqueryresults) | `cx:CT_GeoDataEntityQueryResults` | 0 | 1 | 1 | MS-ODRAWXML §2.24.3.44 p.219 |
| [`cx:geoDataPointQuery`](#cxgeodatapointquery) | `cx:CT_GeoDataPointQuery` | 3 | 0 | 1 | MS-ODRAWXML §2.24.3.45 p.219 |
| [`cx:geoDataPointToEntityQuery`](#cxgeodatapointtoentityquery) | `cx:CT_GeoDataPointToEntityQuery` | 2 | 0 | 1 | MS-ODRAWXML §2.24.3.46 p.220 |
| [`cx:geoDataPointToEntityQueryResult`](#cxgeodatapointtoentityqueryresult) | `cx:CT_GeoDataPointToEntityQueryResult` | 0 | 2 | 1 | MS-ODRAWXML §2.24.3.47 p.220 |
| [`cx:geoDataPointToEntityQueryResults`](#cxgeodatapointtoentityqueryresults) | `cx:CT_GeoDataPointToEntityQueryResults` | 0 | 1 | 1 | MS-ODRAWXML §2.24.3.48 p.221 |
| [`cx:geoEntity`](#cxgeoentity) | `cx:CT_GeoEntity` | 2 | 0 | 1 | MS-ODRAWXML §2.24.3.49 p.221 |
| [`cx:geoHierarchyEntity`](#cxgeohierarchyentity) | `cx:CT_GeoHierarchyEntity` | 3 | 0 | 1 | MS-ODRAWXML §2.24.3.51 p.223 |
| [`cx:geoLocation`](#cxgeolocation) | `cx:CT_GeoLocation` | 4 | 1 | 1 | MS-ODRAWXML §2.24.3.52 p.223 |
| [`cx:geoLocationQuery`](#cxgeolocationquery) | `cx:CT_GeoLocationQuery` | 5 | 0 | 1 | MS-ODRAWXML §2.24.3.53 p.224 |
| [`cx:geoLocationQueryResult`](#cxgeolocationqueryresult) | `cx:CT_GeoLocationQueryResult` | 0 | 2 | 1 | MS-ODRAWXML §2.24.3.54 p.225 |
| [`cx:geoLocationQueryResults`](#cxgeolocationqueryresults) | `cx:CT_GeoLocationQueryResults` | 0 | 1 | 1 | MS-ODRAWXML §2.24.3.55 p.225 |
| [`cx:geoLocations`](#cxgeolocations) | `cx:CT_GeoLocations` | 0 | 1 | 1 | MS-ODRAWXML §2.24.3.56 p.226 |
| [`cx:geoParentEntitiesQuery`](#cxgeoparententitiesquery) | `cx:CT_GeoParentEntitiesQuery` | 1 | 0 | 1 | MS-ODRAWXML §2.24.3.57 p.226 |
| [`cx:geoParentEntitiesQueryResult`](#cxgeoparententitiesqueryresult) | `cx:CT_GeoParentEntitiesQueryResult` | 0 | 3 | 1 | MS-ODRAWXML §2.24.3.58 p.227 |
| [`cx:geoParentEntitiesQueryResults`](#cxgeoparententitiesqueryresults) | `cx:CT_GeoParentEntitiesQueryResults` | 0 | 1 | 1 | MS-ODRAWXML §2.24.3.59 p.227 |
| [`cx:geoParentEntity`](#cxgeoparententity) | `cx:CT_GeoParentEntity` | 1 | 0 | 1 | MS-ODRAWXML §2.24.3.60 p.228 |
| [`cx:geoPolygon`](#cxgeopolygon) | `cx:CT_GeoPolygon` | 3 | 0 | 1 | MS-ODRAWXML §2.24.3.61 p.228 |
| [`cx:geoPolygons`](#cxgeopolygons) | `cx:CT_GeoPolygons` | 0 | 1 | 1 | MS-ODRAWXML §2.24.3.62 p.229 |
| [`cx:geography`](#cxgeography) | `cx:CT_Geography` | 5 | 1 | 1 | MS-ODRAWXML §2.24.3.50 p.222 |
| [`cx:headerFooter`](#cxheaderfooter) | `cx:CT_HeaderFooter` | 3 | 6 | 1 | MS-ODRAWXML §2.24.3.64 p.229 |
| [`cx:idx`](#cxidx) | `cx:CT_SubtotalIndex` | 1 | 0 | 1 | MS-ODRAWXML §2.24.3.89 p.244 |
| [`cx:layoutPr`](#cxlayoutpr) | `cx:CT_SeriesLayoutProperties` | 0 | 9 | 1 | MS-ODRAWXML §2.24.3.84 p.241 |
| [`cx:legend`](#cxlegend) | `cx:CT_Legend` | 3 | 4 | 1 | MS-ODRAWXML §2.24.3.65 p.230 |
| [`cx:lvl`](#cxlvl) | `cx:CT_StringLevel`, `cx:CT_NumericLevel` | 3 | 1 | 2 | MS-ODRAWXML §2.24.3.87 p.243; MS-ODRAWXML §2.24.3.69 p.233 |
| [`cx:majorGridlines`](#cxmajorgridlines) | `cx:CT_Gridlines` | 0 | 2 | 1 | MS-ODRAWXML §2.24.3.63 p.229 |
| [`cx:majorTickMarks`](#cxmajortickmarks) | `cx:CT_TickMarks` | 1 | 1 | 1 | MS-ODRAWXML §2.24.3.94 p.247 |
| [`cx:max`](#cxmax) | `cx:CT_ValueColorEndPosition` | 0 | 3 | 1 | MS-ODRAWXML §2.24.3.96 p.248 |
| [`cx:maxColor`](#cxmaxcolor) | `a:CT_SolidColorFillProperties` | 0 | 6 | 1 |  |
| [`cx:mid`](#cxmid) | `cx:CT_ValueColorMiddlePosition` | 0 | 2 | 1 | MS-ODRAWXML §2.24.3.97 p.248 |
| [`cx:midColor`](#cxmidcolor) | `a:CT_SolidColorFillProperties` | 0 | 6 | 1 |  |
| [`cx:min`](#cxmin) | `cx:CT_ValueColorEndPosition` | 0 | 3 | 1 | MS-ODRAWXML §2.24.3.96 p.248 |
| [`cx:minColor`](#cxmincolor) | `a:CT_SolidColorFillProperties` | 0 | 6 | 1 |  |
| [`cx:minorGridlines`](#cxminorgridlines) | `cx:CT_Gridlines` | 0 | 2 | 1 | MS-ODRAWXML §2.24.3.63 p.229 |
| [`cx:minorTickMarks`](#cxminortickmarks) | `cx:CT_TickMarks` | 1 | 1 | 1 | MS-ODRAWXML §2.24.3.94 p.247 |
| [`cx:nf`](#cxnf) | `cx:CT_Formula` | 1 | 0 | 2 | MS-ODRAWXML §2.24.3.34 p.214 |
| [`cx:numDim`](#cxnumdim) | `cx:CT_NumericDimension` | 1 | 3 | 1 | MS-ODRAWXML §2.24.3.68 p.232 |
| [`cx:numFmt`](#cxnumfmt) | `cx:CT_NumberFormat` | 2 | 0 | 3 | MS-ODRAWXML §2.24.3.67 p.232 |
| [`cx:number`](#cxnumber) | `cx:CT_NumberColorPosition` | 1 | 0 | 3 | MS-ODRAWXML §2.24.3.66 p.231 |
| [`cx:oddFooter`](#cxoddfooter) | `xsd:string` | 0 | 0 | 1 |  |
| [`cx:oddHeader`](#cxoddheader) | `xsd:string` | 0 | 0 | 1 |  |
| [`cx:offset`](#cxoffset) | `cx:CT_Offset` | 2 | 0 | 2 | MS-ODRAWXML §2.24.3.71 p.234 |
| [`cx:pageMargins`](#cxpagemargins) | `cx:CT_PageMargins` | 6 | 0 | 1 | MS-ODRAWXML §2.24.3.72 p.234 |
| [`cx:pageSetup`](#cxpagesetup) | `cx:CT_PageSetup` | 9 | 0 | 1 | MS-ODRAWXML §2.24.3.73 p.235 |
| [`cx:parentLabelLayout`](#cxparentlabellayout) | `cx:CT_ParentLabelLayout` | 1 | 0 | 1 | MS-ODRAWXML §2.24.3.74 p.236 |
| [`cx:percent`](#cxpercent) | `cx:CT_PercentageColorPosition` | 1 | 0 | 3 | MS-ODRAWXML §2.24.3.75 p.236 |
| [`cx:plotArea`](#cxplotarea) | `cx:CT_PlotArea` | 0 | 4 | 1 | MS-ODRAWXML §2.24.3.76 p.237 |
| [`cx:plotAreaRegion`](#cxplotarearegion) | `cx:CT_PlotAreaRegion` | 0 | 3 | 1 | MS-ODRAWXML §2.24.3.77 p.237 |
| [`cx:plotSurface`](#cxplotsurface) | `cx:CT_PlotSurface` | 0 | 2 | 1 | MS-ODRAWXML §2.24.3.78 p.238 |
| [`cx:printSettings`](#cxprintsettings) | `cx:CT_PrintSettings` | 0 | 3 | 1 | MS-ODRAWXML §2.24.3.79 p.238 |
| [`cx:pt`](#cxpt) | `cx:CT_StringValue`, `cx:CT_NumericValue` | 1 | 0 | 1 | MS-ODRAWXML §2.24.3.88 p.244; MS-ODRAWXML §2.24.3.70 p.233 |
| [`cx:regionLabelLayout`](#cxregionlabellayout) | `cx:CT_RegionLabelLayout` | 1 | 0 | 1 | MS-ODRAWXML §2.24.3.80 p.239 |
| [`cx:rich`](#cxrich) | `a:CT_TextBody` | 0 | 3 | 1 |  |
| [`cx:separator`](#cxseparator) | `xsd:string` | 0 | 0 | 2 |  |
| [`cx:series`](#cxseries) | `cx:CT_Series` | 5 | 10 | 1 | MS-ODRAWXML §2.24.3.82 p.239 |
| [`cx:spPr`](#cxsppr) | `a:CT_ShapeProperties` | 1 | 15 | 14 |  |
| [`cx:statistics`](#cxstatistics) | `cx:CT_Statistics` | 1 | 0 | 1 | MS-ODRAWXML §2.24.3.85 p.242 |
| [`cx:strDim`](#cxstrdim) | `cx:CT_StringDimension` | 1 | 3 | 1 | MS-ODRAWXML §2.24.3.86 p.243 |
| [`cx:subtotals`](#cxsubtotals) | `cx:CT_Subtotals` | 0 | 1 | 1 | MS-ODRAWXML §2.24.3.90 p.245 |
| [`cx:tickLabels`](#cxticklabels) | `cx:CT_TickLabels` | 0 | 1 | 1 | MS-ODRAWXML §2.24.3.93 p.246 |
| [`cx:title`](#cxtitle) | `cx:CT_AxisTitle`, `cx:CT_ChartTitle` | 3 | 5 | 2 | MS-ODRAWXML §2.24.3.6 p.197; MS-ODRAWXML §2.24.3.16 p.203 |
| [`cx:tx`](#cxtx) | `cx:CT_Text` | 0 | 2 | 3 | MS-ODRAWXML §2.24.3.91 p.245 |
| [`cx:txData`](#cxtxdata) | `cx:CT_TextData` | 0 | 2 | 1 | MS-ODRAWXML §2.24.3.92 p.246 |
| [`cx:txPr`](#cxtxpr) | `a:CT_TextBody` | 0 | 3 | 7 |  |
| [`cx:units`](#cxunits) | `cx:CT_AxisUnits` | 1 | 2 | 1 | MS-ODRAWXML §2.24.3.7 p.198 |
| [`cx:unitsLabel`](#cxunitslabel) | `cx:CT_AxisUnitsLabel` | 0 | 4 | 1 | MS-ODRAWXML §2.24.3.8 p.198 |
| [`cx:v`](#cxv) | `xsd:string` | 0 | 0 | 1 |  |
| [`cx:valScaling`](#cxvalscaling) | `cx:CT_ValueAxisScaling` | 4 | 0 | 1 | MS-ODRAWXML §2.24.3.95 p.247 |
| [`cx:valueColorPositions`](#cxvaluecolorpositions) | `cx:CT_ValueColorPositions` | 1 | 3 | 1 | MS-ODRAWXML §2.24.3.98 p.249 |
| [`cx:valueColors`](#cxvaluecolors) | `cx:CT_ValueColors` | 0 | 3 | 1 | MS-ODRAWXML §2.24.3.99 p.249 |
| [`cx:visibility`](#cxvisibility) | `cx:CT_SeriesElementVisibilities`, `cx:CT_DataLabelVisibilities` | 8 | 0 | 3 | MS-ODRAWXML §2.24.3.83 p.241; MS-ODRAWXML §2.24.3.25 p.209 |

◆ declared globally (can be the root element of a part)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `cx:ST_ArtifactType` | restriction of xsd:string | `html` | MS-ODRAWXML §2.24.4.1 p.250 |
| `cx:ST_AxisId` | restriction of xsd:unsignedInt |  | MS-ODRAWXML §2.24.4.2 p.250 |
| `cx:ST_AxisUnit` | restriction of xsd:string | `hundreds`, `thousands`, `tenThousands`, `hundredThousands`, `millions`, `tenMillions`, `hundredMillions`, `billions`, `trillions`, `percentage` | MS-ODRAWXML §2.24.4.3 p.251 |
| `cx:ST_BindingType` | union of cx:ST_NumericDimensionType, cx:ST_StringDimensionType |  | MS-ODRAWXML §2.24.4.4 p.252 |
| `cx:ST_DataId` | restriction of xsd:unsignedInt |  | MS-ODRAWXML §2.24.4.5 p.252 |
| `cx:ST_DataLabelPos` | restriction of xsd:string | `bestFit`, `b`, `ctr`, `inBase`, `inEnd`, `l`, `outEnd`, `r`, `t` | MS-ODRAWXML §2.24.4.6 p.252 |
| `cx:ST_DoubleOrAutomatic` | union of xsd:double, restriction of xsd:string | `auto` | MS-ODRAWXML §2.24.4.7 p.253 |
| `cx:ST_EntityType` | restriction of xsd:string | `Address`, `AdminDistrict`, `AdminDistrict2`, `AdminDistrict3`, `Continent`, `CountryRegion`, `Locality`, `Ocean`, `Planet`, `PostalCode`, `Region`, `Unsupported` | MS-ODRAWXML §2.24.4.8 p.253 |
| `cx:ST_ExtensionDropMode` | restriction of xsd:string | `never`, `onModelChange`, `onDataChange`, `always` | MS-ODRAWXML §2.24.4.9 p.255 |
| `cx:ST_FormulaDirection` | restriction of xsd:string | `col`, `row` | MS-ODRAWXML §2.24.4.10 p.255 |
| `cx:ST_GapWidthRatio` | union of restriction of xsd:double, restriction of xsd:string | `auto` | MS-ODRAWXML §2.24.4.11 p.256 |
| `cx:ST_GeoMappingLevel` | restriction of xsd:string | `dataOnly`, `postalCode`, `county`, `state`, `countryRegion`, `countryRegionList`, `world` | MS-ODRAWXML §2.24.4.12 p.256 |
| `cx:ST_GeoProjectionType` | restriction of xsd:string | `mercator`, `miller`, `robinson`, `albers` | MS-ODRAWXML §2.24.4.13 p.257 |
| `cx:ST_IntervalClosedSide` | restriction of xsd:string | `l`, `r` | MS-ODRAWXML §2.24.4.14 p.257 |
| `cx:ST_NumericDimensionType` | restriction of xsd:string | `val`, `x`, `y`, `size`, `colorVal` | MS-ODRAWXML §2.24.4.15 p.258 |
| `cx:ST_PageOrientation` | restriction of xsd:string | `default`, `portrait`, `landscape` | MS-ODRAWXML §2.24.4.16 p.259 |
| `cx:ST_ParentLabelLayout` | restriction of xsd:string | `none`, `banner`, `overlapping` | MS-ODRAWXML §2.24.4.17 p.259 |
| `cx:ST_PosAlign` | restriction of xsd:string | `min`, `ctr`, `max` | MS-ODRAWXML §2.24.4.18 p.260 |
| `cx:ST_QuartileMethod` | restriction of xsd:string | `inclusive`, `exclusive` | MS-ODRAWXML §2.24.4.19 p.260 |
| `cx:ST_RegionLabelLayout` | restriction of xsd:string | `none`, `bestFitOnly`, `showAll` | MS-ODRAWXML §2.24.4.20 p.261 |
| `cx:ST_SeriesLayout` | restriction of xsd:string | `boxWhisker`, `clusteredColumn`, `funnel`, `paretoLine`, `regionMap`, `sunburst`, `treemap`, `waterfall` | MS-ODRAWXML §2.24.4.21 p.261 |
| `cx:ST_SidePos` | restriction of xsd:string | `l`, `t`, `r`, `b` | MS-ODRAWXML §2.24.4.22 p.262 |
| `cx:ST_StringDimensionType` | restriction of xsd:string | `cat`, `colorStr`, `entityId` | MS-ODRAWXML §2.24.4.23 p.263 |
| `cx:ST_TickMarksType` | restriction of xsd:string | `in`, `out`, `cross`, `none` | MS-ODRAWXML §2.24.4.24 p.263 |
| `cx:ST_ValueAxisUnit` | union of restriction of xsd:double, restriction of xsd:string | `auto` | MS-ODRAWXML §2.24.4.25 p.264 |
| `cx:ST_ValueColorPositionCount` | restriction of xsd:int | minInclusive `2`, maxInclusive `3` | MS-ODRAWXML §2.24.4.26 p.264 |

## Element details

### cx:address

- Type: `cx:CT_Address`
- Attributes: `address1`, `adminDistrict1`, `adminDistrict2`, `countryRegion`, `isoCountryCode`, `locality`, `postalCode`
- Parents: `cx:geoLocation`
- Specification: MS-ODRAWXML §2.24.3.1 p.194

### cx:aggregation

- Type: `cx:CT_Aggregation`
- Parents: `cx:layoutPr`
- Specification: MS-ODRAWXML §2.24.3.2 p.194

### cx:artifact

- Type: `cx:CT_Artifact` (text: `xsd:string`)
- Attributes: `name`!, `source`, `type`!
- Parents: `cx:artifacts`
- Specification: MS-ODRAWXML §2.24.3.3 p.195

### cx:artifacts

- Type: `cx:CT_Artifacts`
- Children: `cx:artifact`, `cx:extLst`
- Parents: `cx:dashboard`
- Specification: MS-ODRAWXML §2.24.3.4 p.195

### cx:axis

- Type: `cx:CT_Axis`
- Attributes: `hidden`, `id`!
- Children: `cx:catScaling`, `cx:valScaling`, `cx:title`, `cx:units`, `cx:majorGridlines`, `cx:minorGridlines`, `cx:majorTickMarks`, `cx:minorTickMarks`, `cx:tickLabels`, `cx:numFmt`, `cx:spPr`, `cx:txPr`, `cx:extLst`
- Parents: `cx:plotArea`
- Specification: MS-ODRAWXML §2.24.3.5 p.196

### cx:axisId

- Type: `cx:ST_AxisId` (text: `cx:ST_AxisId`)
- Parents: `cx:series`

### cx:binCount

- Type: `xsd:unsignedInt` (text: `xsd:unsignedInt`)
- Parents: `cx:binning`

### cx:binSize

- Type: `xsd:double` (text: `xsd:double`)
- Parents: `cx:binning`

### cx:binary

- Type: `xsd:base64Binary` (text: `xsd:base64Binary`)
- Parents: `cx:geoCache`

### cx:binding

- Type: `cx:CT_Binding`
- Attributes: `id`!, `name`!, `type`!
- Parents: `cx:bindings`
- Specification: MS-ODRAWXML §2.24.3.9 p.199

### cx:bindings

- Type: `cx:CT_Bindings`
- Children: `cx:binding`, `cx:extLst`
- Parents: `cx:dashboard`
- Specification: MS-ODRAWXML §2.24.3.10 p.199

### cx:binning

- Type: `cx:CT_Binning`
- Attributes: `intervalClosed`, `overflow`, `underflow`
- Children: `cx:binSize`, `cx:binCount`
- Parents: `cx:layoutPr`
- Specification: MS-ODRAWXML §2.24.3.11 p.200

### cx:catScaling

- Type: `cx:CT_CategoryAxisScaling`
- Attributes: `gapWidth`
- Parents: `cx:axis`
- Specification: MS-ODRAWXML §2.24.3.12 p.201

### cx:chart

- Type: `cx:CT_RelId`, `cx:CT_Chart`
- Attributes: `r:id`!
- Children: `cx:title`, `cx:plotArea`, `cx:legend`, `cx:extLst`
- Parents: `cx:chartSpace`
- Specification: MS-ODRAWXML §2.24.1.1 p.193

### cx:chartData

- Type: `cx:CT_ChartData`
- Children: `cx:externalData`, `cx:data`, `cx:extLst`
- Parents: `cx:chartSpace`
- Specification: MS-ODRAWXML §2.24.3.14 p.202

### cx:chartSpace

- Type: `cx:CT_ChartSpace`
- Attributes: `fallbackImg`, `featureList`, `version`
- Children: `cx:chartData`, `cx:chart`, `cx:dashboard`, `cx:spPr`, `cx:txPr`, `cx:clrMapOvr`, `cx:fmtOvrs`, `cx:printSettings`, `cx:extLst`
- Specification: MS-ODRAWXML §2.24.1.2 p.193

### cx:clear

- Type: `cx:CT_Clear`
- Children: `cx:geoLocationQueryResults`, `cx:geoDataEntityQueryResults`, `cx:geoDataPointToEntityQueryResults`, `cx:geoChildEntitiesQueryResults`, `cx:geoParentEntitiesQueryResults`
- Parents: `cx:geoCache`
- Specification: MS-ODRAWXML §2.24.3.17 p.204

### cx:clrMapOvr

- Type: `a:CT_ColorMapping`
- Attributes: `accent1`!, `accent2`!, `accent3`!, `accent4`!, `accent5`!, `accent6`!, `bg1`!, `bg2`!, `folHlink`!, `hlink`!, `tx1`!, `tx2`!
- Children: `a:extLst`
- Parents: `cx:chartSpace`

### cx:copyright

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `cx:copyrights`

### cx:copyrights

- Type: `cx:CT_Copyrights`
- Children: `cx:copyright`
- Parents: `cx:geoData`
- Specification: MS-ODRAWXML §2.24.3.18 p.205

### cx:dashboard

- Type: `cx:CT_Dashboard`
- Attributes: `entrypoint`!
- Children: `cx:bindings`, `cx:artifacts`, `cx:extLst`
- Parents: `cx:chartSpace`
- Specification: MS-ODRAWXML §2.24.3.19 p.205

### cx:data

- Type: `cx:CT_Data`
- Attributes: `id`!
- Children: `cx:numDim`, `cx:strDim`, `cx:extLst`
- Parents: `cx:chartData`
- Specification: MS-ODRAWXML §2.24.3.20 p.206

### cx:dataId

- Type: `cx:CT_DataId`
- Attributes: `val`!
- Parents: `cx:series`
- Specification: MS-ODRAWXML §2.24.3.21 p.207

### cx:dataLabel

- Type: `cx:CT_DataLabel`
- Attributes: `idx`!, `pos`
- Children: `cx:numFmt`, `cx:spPr`, `cx:txPr`, `cx:visibility`, `cx:separator`, `cx:extLst`
- Parents: `cx:dataLabels`
- Specification: MS-ODRAWXML §2.24.3.22 p.207

### cx:dataLabelHidden

- Type: `cx:CT_DataLabelHidden`
- Attributes: `idx`!
- Parents: `cx:dataLabels`
- Specification: MS-ODRAWXML §2.24.3.23 p.208

### cx:dataLabels

- Type: `cx:CT_DataLabels`
- Attributes: `pos`
- Children: `cx:numFmt`, `cx:spPr`, `cx:txPr`, `cx:visibility`, `cx:separator`, `cx:dataLabel`, `cx:dataLabelHidden`, `cx:extLst`
- Parents: `cx:series`
- Specification: MS-ODRAWXML §2.24.3.24 p.208

### cx:dataPt

- Type: `cx:CT_DataPoint`
- Attributes: `idx`!
- Children: `cx:spPr`, `cx:extLst`
- Parents: `cx:series`
- Specification: MS-ODRAWXML §2.24.3.26 p.210

### cx:entityType

- Type: `cx:ST_EntityType` (text: `cx:ST_EntityType`)
- Parents: `cx:geoChildTypes`

### cx:evenFooter

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `cx:headerFooter`

### cx:evenHeader

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `cx:headerFooter`

### cx:ext

- Type: `cx:CT_Extension`
- Attributes: `uri`
- Children: `any(##any)`
- Parents: `cx:extLst`
- Specification: MS-ODRAWXML §2.24.3.27 p.210

### cx:extLst

- Type: `cx:CT_ExtensionList`
- Children: `cx:ext`
- Parents: `cx:artifacts`, `cx:axis`, `cx:bindings`, `cx:chart`, `cx:chartData`, `cx:chartSpace`, `cx:dashboard`, `cx:data`, `cx:dataLabel`, `cx:dataLabels`, `cx:dataPt`, `cx:fmtOvr`, `cx:layoutPr`, `cx:legend`, `cx:majorGridlines`, `cx:majorTickMarks`, `cx:minorGridlines`, `cx:minorTickMarks`, `cx:plotArea`, `cx:plotAreaRegion`, `cx:plotSurface`, `cx:series`, `cx:tickLabels`, `cx:title`, `cx:units`, `cx:unitsLabel`
- Specification: MS-ODRAWXML §2.24.3.28 p.211

### cx:externalData

- Type: `cx:CT_ExternalData`
- Attributes: `autoUpdate`, `r:id`!
- Parents: `cx:chartData`
- Specification: MS-ODRAWXML §2.24.3.29 p.211

### cx:extremeValue

- Type: `cx:CT_ExtremeValueColorPosition`
- Parents: `cx:max`, `cx:min`
- Specification: MS-ODRAWXML §2.24.3.30 p.212

### cx:f

- Type: `cx:CT_Formula` (text: `xsd:string`)
- Attributes: `dir`
- Parents: `cx:numDim`, `cx:strDim`, `cx:txData`
- Specification: MS-ODRAWXML §2.24.3.34 p.214

### cx:firstFooter

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `cx:headerFooter`

### cx:firstHeader

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `cx:headerFooter`

### cx:fmtOvr

- Type: `cx:CT_FormatOverride`
- Attributes: `idx`!
- Children: `cx:spPr`, `cx:extLst`
- Parents: `cx:fmtOvrs`
- Specification: MS-ODRAWXML §2.24.3.32 p.213

### cx:fmtOvrs

- Type: `cx:CT_FormatOverrides`
- Children: `cx:fmtOvr`
- Parents: `cx:chartSpace`
- Specification: MS-ODRAWXML §2.24.3.33 p.213

### cx:geoCache

- Type: `cx:CT_GeoCache`
- Attributes: `provider`!
- Children: `cx:binary`, `cx:clear`
- Parents: `cx:geography`
- Specification: MS-ODRAWXML §2.24.3.35 p.214

### cx:geoChildEntities

- Type: `cx:CT_GeoChildEntities`
- Children: `cx:geoHierarchyEntity`
- Parents: `cx:geoChildEntitiesQueryResult`
- Specification: MS-ODRAWXML §2.24.3.36 p.215

### cx:geoChildEntitiesQuery

- Type: `cx:CT_GeoChildEntitiesQuery`
- Attributes: `entityId`!
- Children: `cx:geoChildTypes`
- Parents: `cx:geoChildEntitiesQueryResult`
- Specification: MS-ODRAWXML §2.24.3.37 p.215

### cx:geoChildEntitiesQueryResult

- Type: `cx:CT_GeoChildEntitiesQueryResult`
- Children: `cx:geoChildEntitiesQuery`, `cx:geoChildEntities`
- Parents: `cx:geoChildEntitiesQueryResults`
- Specification: MS-ODRAWXML §2.24.3.38 p.216

### cx:geoChildEntitiesQueryResults

- Type: `cx:CT_GeoChildEntitiesQueryResults`
- Children: `cx:geoChildEntitiesQueryResult`
- Parents: `cx:clear`
- Specification: MS-ODRAWXML §2.24.3.39 p.216

### cx:geoChildTypes

- Type: `cx:CT_GeoChildTypes`
- Children: `cx:entityType`
- Parents: `cx:geoChildEntitiesQuery`
- Specification: MS-ODRAWXML §2.24.3.40 p.217

### cx:geoData

- Type: `cx:CT_GeoData`
- Attributes: `east`!, `entityId`!, `entityName`!, `north`!, `south`!, `west`!
- Children: `cx:geoPolygons`, `cx:copyrights`
- Parents: `cx:geoDataEntityQueryResult`
- Specification: MS-ODRAWXML §2.24.3.41 p.217

### cx:geoDataEntityQuery

- Type: `cx:CT_GeoDataEntityQuery`
- Attributes: `entityId`!, `entityType`!
- Parents: `cx:geoDataEntityQueryResult`
- Specification: MS-ODRAWXML §2.24.3.42 p.218

### cx:geoDataEntityQueryResult

- Type: `cx:CT_GeoDataEntityQueryResult`
- Children: `cx:geoDataEntityQuery`, `cx:geoData`
- Parents: `cx:geoDataEntityQueryResults`
- Specification: MS-ODRAWXML §2.24.3.43 p.218

### cx:geoDataEntityQueryResults

- Type: `cx:CT_GeoDataEntityQueryResults`
- Children: `cx:geoDataEntityQueryResult`
- Parents: `cx:clear`
- Specification: MS-ODRAWXML §2.24.3.44 p.219

### cx:geoDataPointQuery

- Type: `cx:CT_GeoDataPointQuery`
- Attributes: `entityType`!, `latitude`!, `longitude`!
- Parents: `cx:geoDataPointToEntityQueryResult`
- Specification: MS-ODRAWXML §2.24.3.45 p.219

### cx:geoDataPointToEntityQuery

- Type: `cx:CT_GeoDataPointToEntityQuery`
- Attributes: `entityId`!, `entityType`!
- Parents: `cx:geoDataPointToEntityQueryResult`
- Specification: MS-ODRAWXML §2.24.3.46 p.220

### cx:geoDataPointToEntityQueryResult

- Type: `cx:CT_GeoDataPointToEntityQueryResult`
- Children: `cx:geoDataPointQuery`, `cx:geoDataPointToEntityQuery`
- Parents: `cx:geoDataPointToEntityQueryResults`
- Specification: MS-ODRAWXML §2.24.3.47 p.220

### cx:geoDataPointToEntityQueryResults

- Type: `cx:CT_GeoDataPointToEntityQueryResults`
- Children: `cx:geoDataPointToEntityQueryResult`
- Parents: `cx:clear`
- Specification: MS-ODRAWXML §2.24.3.48 p.221

### cx:geoEntity

- Type: `cx:CT_GeoEntity`
- Attributes: `entityName`!, `entityType`!
- Parents: `cx:geoParentEntitiesQueryResult`
- Specification: MS-ODRAWXML §2.24.3.49 p.221

### cx:geoHierarchyEntity

- Type: `cx:CT_GeoHierarchyEntity`
- Attributes: `entityId`!, `entityName`!, `entityType`!
- Parents: `cx:geoChildEntities`
- Specification: MS-ODRAWXML §2.24.3.51 p.223

### cx:geoLocation

- Type: `cx:CT_GeoLocation`
- Attributes: `entityName`!, `entityType`!, `latitude`, `longitude`
- Children: `cx:address`
- Parents: `cx:geoLocations`
- Specification: MS-ODRAWXML §2.24.3.52 p.223

### cx:geoLocationQuery

- Type: `cx:CT_GeoLocationQuery`
- Attributes: `adminDistrict1`, `adminDistrict2`, `countryRegion`, `entityType`!, `postalCode`
- Parents: `cx:geoLocationQueryResult`
- Specification: MS-ODRAWXML §2.24.3.53 p.224

### cx:geoLocationQueryResult

- Type: `cx:CT_GeoLocationQueryResult`
- Children: `cx:geoLocationQuery`, `cx:geoLocations`
- Parents: `cx:geoLocationQueryResults`
- Specification: MS-ODRAWXML §2.24.3.54 p.225

### cx:geoLocationQueryResults

- Type: `cx:CT_GeoLocationQueryResults`
- Children: `cx:geoLocationQueryResult`
- Parents: `cx:clear`
- Specification: MS-ODRAWXML §2.24.3.55 p.225

### cx:geoLocations

- Type: `cx:CT_GeoLocations`
- Children: `cx:geoLocation`
- Parents: `cx:geoLocationQueryResult`
- Specification: MS-ODRAWXML §2.24.3.56 p.226

### cx:geoParentEntitiesQuery

- Type: `cx:CT_GeoParentEntitiesQuery`
- Attributes: `entityId`!
- Parents: `cx:geoParentEntitiesQueryResult`
- Specification: MS-ODRAWXML §2.24.3.57 p.226

### cx:geoParentEntitiesQueryResult

- Type: `cx:CT_GeoParentEntitiesQueryResult`
- Children: `cx:geoParentEntitiesQuery`, `cx:geoEntity`, `cx:geoParentEntity`
- Parents: `cx:geoParentEntitiesQueryResults`
- Specification: MS-ODRAWXML §2.24.3.58 p.227

### cx:geoParentEntitiesQueryResults

- Type: `cx:CT_GeoParentEntitiesQueryResults`
- Children: `cx:geoParentEntitiesQueryResult`
- Parents: `cx:clear`
- Specification: MS-ODRAWXML §2.24.3.59 p.227

### cx:geoParentEntity

- Type: `cx:CT_GeoParentEntity`
- Attributes: `entityId`!
- Parents: `cx:geoParentEntitiesQueryResult`
- Specification: MS-ODRAWXML §2.24.3.60 p.228

### cx:geoPolygon

- Type: `cx:CT_GeoPolygon`
- Attributes: `numPoints`!, `pcaRings`!, `polygonId`!
- Parents: `cx:geoPolygons`
- Specification: MS-ODRAWXML §2.24.3.61 p.228

### cx:geoPolygons

- Type: `cx:CT_GeoPolygons`
- Children: `cx:geoPolygon`
- Parents: `cx:geoData`
- Specification: MS-ODRAWXML §2.24.3.62 p.229

### cx:geography

- Type: `cx:CT_Geography`
- Attributes: `attribution`!, `cultureLanguage`!, `cultureRegion`!, `projectionType`, `viewedRegionType`
- Children: `cx:geoCache`
- Parents: `cx:layoutPr`
- Specification: MS-ODRAWXML §2.24.3.50 p.222

### cx:headerFooter

- Type: `cx:CT_HeaderFooter`
- Attributes: `alignWithMargins`, `differentFirst`, `differentOddEven`
- Children: `cx:oddHeader`, `cx:oddFooter`, `cx:evenHeader`, `cx:evenFooter`, `cx:firstHeader`, `cx:firstFooter`
- Parents: `cx:printSettings`
- Specification: MS-ODRAWXML §2.24.3.64 p.229

### cx:idx

- Type: `cx:CT_SubtotalIndex`
- Attributes: `val`!
- Parents: `cx:subtotals`
- Specification: MS-ODRAWXML §2.24.3.89 p.244

### cx:layoutPr

- Type: `cx:CT_SeriesLayoutProperties`
- Children: `cx:parentLabelLayout`, `cx:regionLabelLayout`, `cx:visibility`, `cx:aggregation`, `cx:binning`, `cx:geography`, `cx:statistics`, `cx:subtotals`, `cx:extLst`
- Parents: `cx:series`
- Specification: MS-ODRAWXML §2.24.3.84 p.241

### cx:legend

- Type: `cx:CT_Legend`
- Attributes: `align`, `overlay`, `pos`
- Children: `cx:spPr`, `cx:txPr`, `cx:offset`, `cx:extLst`
- Parents: `cx:chart`
- Specification: MS-ODRAWXML §2.24.3.65 p.230

### cx:lvl

- Type: `cx:CT_StringLevel`, `cx:CT_NumericLevel`
- Attributes: `formatCode`, `name`, `ptCount`!
- Children: `cx:pt`
- Parents: `cx:numDim`, `cx:strDim`
- Specification: MS-ODRAWXML §2.24.3.87 p.243; MS-ODRAWXML §2.24.3.69 p.233

### cx:majorGridlines

- Type: `cx:CT_Gridlines`
- Children: `cx:spPr`, `cx:extLst`
- Parents: `cx:axis`
- Specification: MS-ODRAWXML §2.24.3.63 p.229

### cx:majorTickMarks

- Type: `cx:CT_TickMarks`
- Attributes: `type`
- Children: `cx:extLst`
- Parents: `cx:axis`
- Specification: MS-ODRAWXML §2.24.3.94 p.247

### cx:max

- Type: `cx:CT_ValueColorEndPosition`
- Children: `cx:extremeValue`, `cx:number`, `cx:percent`
- Parents: `cx:valueColorPositions`
- Specification: MS-ODRAWXML §2.24.3.96 p.248

### cx:maxColor

- Type: `a:CT_SolidColorFillProperties`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `cx:valueColors`

### cx:mid

- Type: `cx:CT_ValueColorMiddlePosition`
- Children: `cx:number`, `cx:percent`
- Parents: `cx:valueColorPositions`
- Specification: MS-ODRAWXML §2.24.3.97 p.248

### cx:midColor

- Type: `a:CT_SolidColorFillProperties`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `cx:valueColors`

### cx:min

- Type: `cx:CT_ValueColorEndPosition`
- Children: `cx:extremeValue`, `cx:number`, `cx:percent`
- Parents: `cx:valueColorPositions`
- Specification: MS-ODRAWXML §2.24.3.96 p.248

### cx:minColor

- Type: `a:CT_SolidColorFillProperties`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `cx:valueColors`

### cx:minorGridlines

- Type: `cx:CT_Gridlines`
- Children: `cx:spPr`, `cx:extLst`
- Parents: `cx:axis`
- Specification: MS-ODRAWXML §2.24.3.63 p.229

### cx:minorTickMarks

- Type: `cx:CT_TickMarks`
- Attributes: `type`
- Children: `cx:extLst`
- Parents: `cx:axis`
- Specification: MS-ODRAWXML §2.24.3.94 p.247

### cx:nf

- Type: `cx:CT_Formula` (text: `xsd:string`)
- Attributes: `dir`
- Parents: `cx:numDim`, `cx:strDim`
- Specification: MS-ODRAWXML §2.24.3.34 p.214

### cx:numDim

- Type: `cx:CT_NumericDimension`
- Attributes: `type`!
- Children: `cx:f`, `cx:nf`, `cx:lvl`
- Parents: `cx:data`
- Specification: MS-ODRAWXML §2.24.3.68 p.232

### cx:numFmt

- Type: `cx:CT_NumberFormat`
- Attributes: `formatCode`!, `sourceLinked`
- Parents: `cx:axis`, `cx:dataLabel`, `cx:dataLabels`
- Specification: MS-ODRAWXML §2.24.3.67 p.232

### cx:number

- Type: `cx:CT_NumberColorPosition`
- Attributes: `val`!
- Parents: `cx:max`, `cx:mid`, `cx:min`
- Specification: MS-ODRAWXML §2.24.3.66 p.231

### cx:oddFooter

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `cx:headerFooter`

### cx:oddHeader

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `cx:headerFooter`

### cx:offset

- Type: `cx:CT_Offset`
- Attributes: `left`!, `top`!
- Parents: `cx:legend`, `cx:title`
- Specification: MS-ODRAWXML §2.24.3.71 p.234

### cx:pageMargins

- Type: `cx:CT_PageMargins`
- Attributes: `b`!, `footer`!, `header`!, `l`!, `r`!, `t`!
- Parents: `cx:printSettings`
- Specification: MS-ODRAWXML §2.24.3.72 p.234

### cx:pageSetup

- Type: `cx:CT_PageSetup`
- Attributes: `blackAndWhite`, `copies`, `draft`, `firstPageNumber`, `horizontalDpi`, `orientation`, `paperSize`, `useFirstPageNumber`, `verticalDpi`
- Parents: `cx:printSettings`
- Specification: MS-ODRAWXML §2.24.3.73 p.235

### cx:parentLabelLayout

- Type: `cx:CT_ParentLabelLayout`
- Attributes: `val`!
- Parents: `cx:layoutPr`
- Specification: MS-ODRAWXML §2.24.3.74 p.236

### cx:percent

- Type: `cx:CT_PercentageColorPosition`
- Attributes: `val`!
- Parents: `cx:max`, `cx:mid`, `cx:min`
- Specification: MS-ODRAWXML §2.24.3.75 p.236

### cx:plotArea

- Type: `cx:CT_PlotArea`
- Children: `cx:plotAreaRegion`, `cx:axis`, `cx:spPr`, `cx:extLst`
- Parents: `cx:chart`
- Specification: MS-ODRAWXML §2.24.3.76 p.237

### cx:plotAreaRegion

- Type: `cx:CT_PlotAreaRegion`
- Children: `cx:plotSurface`, `cx:series`, `cx:extLst`
- Parents: `cx:plotArea`
- Specification: MS-ODRAWXML §2.24.3.77 p.237

### cx:plotSurface

- Type: `cx:CT_PlotSurface`
- Children: `cx:spPr`, `cx:extLst`
- Parents: `cx:plotAreaRegion`
- Specification: MS-ODRAWXML §2.24.3.78 p.238

### cx:printSettings

- Type: `cx:CT_PrintSettings`
- Children: `cx:headerFooter`, `cx:pageMargins`, `cx:pageSetup`
- Parents: `cx:chartSpace`
- Specification: MS-ODRAWXML §2.24.3.79 p.238

### cx:pt

- Type: `cx:CT_StringValue`, `cx:CT_NumericValue` (text: `xsd:string`)
- Attributes: `idx`!
- Parents: `cx:lvl`
- Specification: MS-ODRAWXML §2.24.3.88 p.244; MS-ODRAWXML §2.24.3.70 p.233

### cx:regionLabelLayout

- Type: `cx:CT_RegionLabelLayout`
- Attributes: `val`!
- Parents: `cx:layoutPr`
- Specification: MS-ODRAWXML §2.24.3.80 p.239

### cx:rich

- Type: `a:CT_TextBody`
- Children: `a:bodyPr`, `a:lstStyle`, `a:p`
- Parents: `cx:tx`

### cx:separator

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `cx:dataLabel`, `cx:dataLabels`

### cx:series

- Type: `cx:CT_Series`
- Attributes: `formatIdx`, `hidden`, `layoutId`!, `ownerIdx`, `uniqueId`
- Children: `cx:tx`, `cx:spPr`, `cx:valueColors`, `cx:valueColorPositions`, `cx:dataPt`, `cx:dataLabels`, `cx:dataId`, `cx:layoutPr`, `cx:axisId`, `cx:extLst`
- Parents: `cx:plotAreaRegion`
- Specification: MS-ODRAWXML §2.24.3.82 p.239

### cx:spPr

- Type: `a:CT_ShapeProperties`
- Attributes: `bwMode`
- Children: `a:xfrm`, `a:custGeom`, `a:prstGeom`, `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`, `a:ln`, `a:effectLst`, `a:effectDag`, `a:scene3d`, `a:sp3d`, `a:extLst`
- Parents: `cx:axis`, `cx:chartSpace`, `cx:dataLabel`, `cx:dataLabels`, `cx:dataPt`, `cx:fmtOvr`, `cx:legend`, `cx:majorGridlines`, `cx:minorGridlines`, `cx:plotArea`, `cx:plotSurface`, `cx:series`, `cx:title`, `cx:unitsLabel`

### cx:statistics

- Type: `cx:CT_Statistics`
- Attributes: `quartileMethod`
- Parents: `cx:layoutPr`
- Specification: MS-ODRAWXML §2.24.3.85 p.242

### cx:strDim

- Type: `cx:CT_StringDimension`
- Attributes: `type`!
- Children: `cx:f`, `cx:nf`, `cx:lvl`
- Parents: `cx:data`
- Specification: MS-ODRAWXML §2.24.3.86 p.243

### cx:subtotals

- Type: `cx:CT_Subtotals`
- Children: `cx:idx`
- Parents: `cx:layoutPr`
- Specification: MS-ODRAWXML §2.24.3.90 p.245

### cx:tickLabels

- Type: `cx:CT_TickLabels`
- Children: `cx:extLst`
- Parents: `cx:axis`
- Specification: MS-ODRAWXML §2.24.3.93 p.246

### cx:title

- Type: `cx:CT_AxisTitle`, `cx:CT_ChartTitle`
- Attributes: `align`, `overlay`, `pos`
- Children: `cx:tx`, `cx:spPr`, `cx:txPr`, `cx:offset`, `cx:extLst`
- Parents: `cx:axis`, `cx:chart`
- Specification: MS-ODRAWXML §2.24.3.6 p.197; MS-ODRAWXML §2.24.3.16 p.203

### cx:tx

- Type: `cx:CT_Text`
- Children: `cx:txData`, `cx:rich`
- Parents: `cx:series`, `cx:title`, `cx:unitsLabel`
- Specification: MS-ODRAWXML §2.24.3.91 p.245

### cx:txData

- Type: `cx:CT_TextData`
- Children: `cx:f`, `cx:v`
- Parents: `cx:tx`
- Specification: MS-ODRAWXML §2.24.3.92 p.246

### cx:txPr

- Type: `a:CT_TextBody`
- Children: `a:bodyPr`, `a:lstStyle`, `a:p`
- Parents: `cx:axis`, `cx:chartSpace`, `cx:dataLabel`, `cx:dataLabels`, `cx:legend`, `cx:title`, `cx:unitsLabel`

### cx:units

- Type: `cx:CT_AxisUnits`
- Attributes: `unit`
- Children: `cx:unitsLabel`, `cx:extLst`
- Parents: `cx:axis`
- Specification: MS-ODRAWXML §2.24.3.7 p.198

### cx:unitsLabel

- Type: `cx:CT_AxisUnitsLabel`
- Children: `cx:tx`, `cx:spPr`, `cx:txPr`, `cx:extLst`
- Parents: `cx:units`
- Specification: MS-ODRAWXML §2.24.3.8 p.198

### cx:v

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `cx:txData`

### cx:valScaling

- Type: `cx:CT_ValueAxisScaling`
- Attributes: `majorUnit`, `max`, `min`, `minorUnit`
- Parents: `cx:axis`
- Specification: MS-ODRAWXML §2.24.3.95 p.247

### cx:valueColorPositions

- Type: `cx:CT_ValueColorPositions`
- Attributes: `count`
- Children: `cx:min`, `cx:mid`, `cx:max`
- Parents: `cx:series`
- Specification: MS-ODRAWXML §2.24.3.98 p.249

### cx:valueColors

- Type: `cx:CT_ValueColors`
- Children: `cx:minColor`, `cx:midColor`, `cx:maxColor`
- Parents: `cx:series`
- Specification: MS-ODRAWXML §2.24.3.99 p.249

### cx:visibility

- Type: `cx:CT_SeriesElementVisibilities`, `cx:CT_DataLabelVisibilities`
- Attributes: `categoryName`, `connectorLines`, `meanLine`, `meanMarker`, `nonoutliers`, `outliers`, `seriesName`, `value`
- Parents: `cx:dataLabel`, `cx:dataLabels`, `cx:layoutPr`
- Specification: MS-ODRAWXML §2.24.3.83 p.241; MS-ODRAWXML §2.24.3.25 p.209

`!` marks required attributes.
