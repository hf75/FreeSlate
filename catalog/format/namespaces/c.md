# c: Charts

`http://schemas.openxmlformats.org/drawingml/2006/chart`

- Source: ECMA-376-4 transitional schema dml-chart.xsd
- Office version: 2007
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 221 | 161 | 0 | 136 | 68 | 122 | 306 | 172 |

Full detail (content models, attribute types, defaults): [c.json](c.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`c:applyToEnd`](#capplytoend) | `c:CT_Boolean` | 1 | 0 | 1 | §21.2.2.1 p.3381 |
| [`c:applyToFront`](#capplytofront) | `c:CT_Boolean` | 1 | 0 | 1 | §21.2.2.2 p.3382, OI 2.1.1428 |
| [`c:applyToSides`](#capplytosides) | `c:CT_Boolean` | 1 | 0 | 1 | §21.2.2.3 p.3382, OI 2.1.1429 |
| [`c:area3DChart`](#carea3dchart) | `c:CT_Area3DChart` | 0 | 8 | 1 | §21.2.2.4 p.3382 |
| [`c:areaChart`](#careachart) | `c:CT_AreaChart` | 0 | 7 | 1 | §21.2.2.5 p.3383, OI 2.1.1430 |
| [`c:auto`](#cauto) | `c:CT_Boolean` | 1 | 0 | 2 | §21.2.2.6 p.3383 |
| [`c:autoTitleDeleted`](#cautotitledeleted) | `c:CT_Boolean` | 1 | 0 | 1 | §21.2.2.7 p.3383, OI 2.1.1431 |
| [`c:autoUpdate`](#cautoupdate) | `c:CT_Boolean` | 1 | 0 | 1 | §21.2.2.8 p.3384 |
| [`c:axId`](#caxid) | `c:CT_UnsignedInt` | 1 | 0 | 16 | §21.2.2.9 p.3384, OI 2.1.1432 |
| [`c:axPos`](#caxpos) | `c:CT_AxPos` | 1 | 0 | 4 | §21.2.2.10 p.3384, OI 2.1.1433 |
| [`c:backWall`](#cbackwall) | `c:CT_Surface` | 0 | 4 | 1 | §21.2.2.11 p.3385, OI 2.1.1434 |
| [`c:backward`](#cbackward) | `c:CT_Double` | 1 | 0 | 1 | §21.2.2.12 p.3385, OI 2.1.1435 |
| [`c:bandFmt`](#cbandfmt) | `c:CT_BandFmt` | 0 | 2 | 1 | §21.2.2.13 p.3385 |
| [`c:bandFmts`](#cbandfmts) | `c:CT_BandFmts` | 0 | 1 | 2 | §21.2.2.14 p.3385, OI 2.1.1436 |
| [`c:bar3DChart`](#cbar3dchart) | `c:CT_Bar3DChart` | 0 | 10 | 1 | §21.2.2.15 p.3386, OI 2.1.1437 |
| [`c:barChart`](#cbarchart) | `c:CT_BarChart` | 0 | 10 | 1 | §21.2.2.16 p.3386, OI 2.1.1438 |
| [`c:barDir`](#cbardir) | `c:CT_BarDir` | 1 | 0 | 2 | §21.2.2.17 p.3386, OI 2.1.1439 |
| [`c:baseTimeUnit`](#cbasetimeunit) | `c:CT_TimeUnit` | 1 | 0 | 1 | §21.2.2.18 p.3386 |
| [`c:bubble3D`](#cbubble3d) | `c:CT_Boolean` | 1 | 0 | 4 | §21.2.2.19 p.3386, OI 2.1.1440 |
| [`c:bubbleChart`](#cbubblechart) | `c:CT_BubbleChart` | 0 | 9 | 1 | §21.2.2.20 p.3387, OI 2.1.1441 |
| [`c:bubbleScale`](#cbubblescale) | `c:CT_BubbleScale` | 1 | 0 | 1 | §21.2.2.21 p.3387 |
| [`c:bubbleSize`](#cbubblesize) | `c:CT_NumDataSource` | 0 | 2 | 2 | §21.2.2.22 p.3387 |
| [`c:builtInUnit`](#cbuiltinunit) | `c:CT_BuiltInUnit` | 1 | 0 | 1 | §21.2.2.23 p.3387, OI 2.1.1442 |
| [`c:cat`](#ccat) | `c:CT_AxDataSource` | 0 | 5 | 2 | §21.2.2.24 p.3388, OI 2.1.1443 |
| [`c:catAx`](#ccatax) | `c:CT_CatAx` | 0 | 23 | 1 | §21.2.2.25 p.3388, OI 2.1.1444 |
| [`c:chart`](#cchart) ◆ | `c:CT_RelId`, `c:CT_Chart` | 1 | 13 | 1 | §21.2.2.26 p.3388; §21.2.2.27 p.3388 |
| [`c:chartObject`](#cchartobject) | `c:CT_Boolean` | 1 | 0 | 1 | §21.2.2.28 p.3389, OI 2.1.1445 |
| [`c:chartSpace`](#cchartspace) ◆ | `c:CT_ChartSpace` | 0 | 14 | 0 | §21.2.2.29 p.3389 |
| [`c:clrMapOvr`](#cclrmapovr) | `a:CT_ColorMapping` | 12 | 1 | 1 | §21.2.2.30 p.3389 |
| [`c:crossAx`](#ccrossax) | `c:CT_UnsignedInt` | 1 | 0 | 4 | §21.2.2.31 p.3391, OI 2.1.1446 |
| [`c:crossBetween`](#ccrossbetween) | `c:CT_CrossBetween` | 1 | 0 | 1 | §21.2.2.32 p.3391 |
| [`c:crosses`](#ccrosses) | `c:CT_Crosses` | 1 | 0 | 4 | §21.2.2.33 p.3392 |
| [`c:crossesAt`](#ccrossesat) | `c:CT_Double` | 1 | 0 | 4 | §21.2.2.34 p.3392, OI 2.1.1447 |
| [`c:custSplit`](#ccustsplit) | `c:CT_CustSplit` | 0 | 1 | 1 | §21.2.2.35 p.3393 |
| [`c:custUnit`](#ccustunit) | `c:CT_Double` | 1 | 0 | 1 | §21.2.2.36 p.3393, OI 2.1.1448 |
| [`c:dLbl`](#cdlbl) | `c:CT_DLbl` | 0 | 16 | 2 | §21.2.2.47 p.3396, OI 2.1.1455 |
| [`c:dLblPos`](#cdlblpos) | `c:CT_DLblPos` | 1 | 0 | 4 | §21.2.2.48 p.3397, OI 2.1.1456 |
| [`c:dLbls`](#cdlbls) | `c:CT_DLbls` | 0 | 16 | 16 | §21.2.2.49 p.3397, OI 2.1.1457 |
| [`c:dPt`](#cdpt) | `c:CT_DPt` | 0 | 8 | 2 | §21.2.2.52 p.3397 |
| [`c:dTable`](#cdtable) | `c:CT_DTable` | 0 | 7 | 1 | §21.2.2.54 p.3398 |
| [`c:data`](#cdata) | `c:CT_Boolean` | 1 | 0 | 1 | §21.2.2.37 p.3393, OI 2.1.1449 |
| [`c:date1904`](#cdate1904) | `c:CT_Boolean` | 1 | 0 | 1 | §21.2.2.38 p.3394 |
| [`c:dateAx`](#cdateax) | `c:CT_DateAx` | 0 | 24 | 1 | §21.2.2.39 p.3394, OI 2.1.1450 |
| [`c:delete`](#cdelete) | `c:CT_Boolean` | 1 | 0 | 9 | §21.2.2.40 p.3394 |
| [`c:depthPercent`](#cdepthpercent) | `c:CT_DepthPercent` | 1 | 0 | 1 | §21.2.2.41 p.3395, OI 2.1.1451 |
| [`c:dispBlanksAs`](#cdispblanksas) | `c:CT_DispBlanksAs` | 1 | 0 | 1 | §21.2.2.42 p.3395, OI 2.1.1452 |
| [`c:dispEq`](#cdispeq) | `c:CT_Boolean` | 1 | 0 | 1 | §21.2.2.43 p.3395, OI 2.1.1453 |
| [`c:dispRSqr`](#cdisprsqr) | `c:CT_Boolean` | 1 | 0 | 1 | §21.2.2.44 p.3396, OI 2.1.1454 |
| [`c:dispUnits`](#cdispunits) | `c:CT_DispUnits` | 0 | 4 | 1 | §21.2.2.45 p.3396 |
| [`c:dispUnitsLbl`](#cdispunitslbl) | `c:CT_DispUnitsLbl` | 0 | 4 | 1 | §21.2.2.46 p.3396 |
| [`c:doughnutChart`](#cdoughnutchart) | `c:CT_DoughnutChart` | 0 | 6 | 1 | §21.2.2.50 p.3397, OI 2.1.1458 |
| [`c:downBars`](#cdownbars) | `c:CT_UpDownBar` | 0 | 1 | 1 | §21.2.2.51 p.3397 |
| [`c:dropLines`](#cdroplines) | `c:CT_ChartLines` | 0 | 1 | 5 | §21.2.2.53 p.3397 |
| [`c:errBarType`](#cerrbartype) | `c:CT_ErrBarType` | 1 | 0 | 1 | §21.2.2.56 p.3398, OI 2.1.1460 |
| [`c:errBars`](#cerrbars) | `c:CT_ErrBars` | 0 | 9 | 2 | §21.2.2.55 p.3398, OI 2.1.1459 |
| [`c:errDir`](#cerrdir) | `c:CT_ErrDir` | 1 | 0 | 1 | §21.2.2.57 p.3398, OI 2.1.1461 |
| [`c:errValType`](#cerrvaltype) | `c:CT_ErrValType` | 1 | 0 | 1 | §21.2.2.58 p.3398, OI 2.1.1462 |
| [`c:evenFooter`](#cevenfooter) | `s:ST_Xstring` | 0 | 0 | 1 | §21.2.2.59 p.3399, OI 2.1.1463 |
| [`c:evenHeader`](#cevenheader) | `s:ST_Xstring` | 0 | 0 | 1 | §21.2.2.60 p.3399, OI 2.1.1464 |
| [`c:explosion`](#cexplosion) | `c:CT_UnsignedInt` | 1 | 0 | 3 | §21.2.2.61 p.3400, OI 2.1.1465 |
| [`c:ext`](#cext) | `c:CT_Extension` | 1 | 1 | 1 | §21.2.2.62 p.3400 |
| [`c:extLst`](#cextlst) | `c:CT_ExtensionList` | 0 | 1 | 67 | §21.2.2.64 p.3401 |
| [`c:externalData`](#cexternaldata) | `c:CT_ExternalData` | 1 | 1 | 1 | §21.2.2.63 p.3401, OI 2.1.1466 |
| [`c:f`](#cf) | `xsd:string` | 0 | 0 | 3 | §21.2.2.65 p.3401, OI 2.1.1467 |
| [`c:firstFooter`](#cfirstfooter) | `s:ST_Xstring` | 0 | 0 | 1 | §21.2.2.66 p.3402, OI 2.1.1468 |
| [`c:firstHeader`](#cfirstheader) | `s:ST_Xstring` | 0 | 0 | 1 | §21.2.2.67 p.3402, OI 2.1.1469 |
| [`c:firstSliceAng`](#cfirstsliceang) | `c:CT_FirstSliceAng` | 1 | 0 | 2 | §21.2.2.68 p.3403 |
| [`c:floor`](#cfloor) | `c:CT_Surface` | 0 | 4 | 1 | §21.2.2.69 p.3403, OI 2.1.1470 |
| [`c:fmtId`](#cfmtid) | `c:CT_UnsignedInt` | 1 | 0 | 2 | §21.2.2.70 p.3403, OI 2.1.1471 |
| [`c:formatCode`](#cformatcode) | `s:ST_Xstring` | 0 | 0 | 3 | §21.2.2.71 p.3404 |
| [`c:formatting`](#cformatting) | `c:CT_Boolean` | 1 | 0 | 1 | §21.2.2.72 p.3404, OI 2.1.1472 |
| [`c:forward`](#cforward) | `c:CT_Double` | 1 | 0 | 1 | §21.2.2.73 p.3404, OI 2.1.1473 |
| [`c:gapDepth`](#cgapdepth) | `c:CT_GapAmount` | 1 | 0 | 3 | §21.2.2.74 p.3405, OI 2.1.1474 |
| [`c:gapWidth`](#cgapwidth) | `c:CT_GapAmount` | 1 | 0 | 4 | §21.2.2.75 p.3405, OI 2.1.1475 |
| [`c:grouping`](#cgrouping) | `c:CT_Grouping`, `c:CT_BarGrouping` | 1 | 0 | 6 | §21.2.2.76 p.3405, OI 2.1.1476; §21.2.2.77 p.3406 |
| [`c:h`](#ch) | `c:CT_Double` | 1 | 0 | 1 | §21.2.2.78 p.3406, OI 2.1.1477 |
| [`c:hMode`](#chmode) | `c:CT_LayoutMode` | 1 | 0 | 1 | §21.2.2.81 p.3407, OI 2.1.1479 |
| [`c:hPercent`](#chpercent) | `c:CT_HPercent` | 1 | 0 | 1 | §21.2.2.83 p.3408, OI 2.1.1481 |
| [`c:headerFooter`](#cheaderfooter) | `c:CT_HeaderFooter` | 3 | 6 | 1 | §21.2.2.79 p.3406, OI 2.1.1478 |
| [`c:hiLowLines`](#chilowlines) | `c:CT_ChartLines` | 0 | 1 | 2 | §21.2.2.80 p.3407 |
| [`c:holeSize`](#cholesize) | `c:CT_HoleSize` | 1 | 0 | 1 | §21.2.2.82 p.3408, OI 2.1.1480 |
| [`c:idx`](#cidx) | `c:CT_UnsignedInt` | 1 | 0 | 9 | §21.2.2.84 p.3408, OI 2.1.1482 |
| [`c:intercept`](#cintercept) | `c:CT_Double` | 1 | 0 | 1 | §21.2.2.85 p.3409, OI 2.1.1483 |
| [`c:invertIfNegative`](#cinvertifnegative) | `c:CT_Boolean` | 1 | 0 | 3 | §21.2.2.86 p.3409, OI 2.1.1484 |
| [`c:lang`](#clang) | `c:CT_TextLanguageID` | 1 | 0 | 1 | §21.2.2.87 p.3410 |
| [`c:layout`](#clayout) | `c:CT_Layout` | 0 | 2 | 8 | §21.2.2.88 p.3410, OI 2.1.1485 |
| [`c:layoutTarget`](#clayouttarget) | `c:CT_LayoutTarget` | 1 | 0 | 1 | §21.2.2.89 p.3410, OI 2.1.1486 |
| [`c:lblAlgn`](#clblalgn) | `c:CT_LblAlgn` | 1 | 0 | 1 | §21.2.2.90 p.3410 |
| [`c:lblOffset`](#clbloffset) | `c:CT_LblOffset` | 1 | 0 | 2 | §21.2.2.91 p.3411 |
| [`c:leaderLines`](#cleaderlines) | `c:CT_ChartLines` | 0 | 1 | 1 | §21.2.2.92 p.3411, OI 2.1.1487 |
| [`c:legacyDrawingHF`](#clegacydrawinghf) | `c:CT_RelId` | 1 | 0 | 1 | Part 4 §18.1.2.1 p.295 |
| [`c:legend`](#clegend) | `c:CT_Legend` | 0 | 7 | 1 | §21.2.2.93 p.3411 |
| [`c:legendEntry`](#clegendentry) | `c:CT_LegendEntry` | 0 | 4 | 1 | §21.2.2.94 p.3411 |
| [`c:legendPos`](#clegendpos) | `c:CT_LegendPos` | 1 | 0 | 1 | §21.2.2.95 p.3411, OI 2.1.1488 |
| [`c:line3DChart`](#cline3dchart) | `c:CT_Line3DChart` | 0 | 8 | 1 | §21.2.2.96 p.3412, OI 2.1.1489 |
| [`c:lineChart`](#clinechart) | `c:CT_LineChart` | 0 | 11 | 1 | §21.2.2.97 p.3412, OI 2.1.1490 |
| [`c:logBase`](#clogbase) | `c:CT_LogBase` | 1 | 0 | 1 | §21.2.2.98 p.3412 |
| [`c:lvl`](#clvl) | `c:CT_Lvl` | 0 | 1 | 3 | §21.2.2.99 p.3412, OI 2.1.1491 |
| [`c:majorGridlines`](#cmajorgridlines) | `c:CT_ChartLines` | 0 | 1 | 4 | §21.2.2.100 p.3412 |
| [`c:majorTickMark`](#cmajortickmark) | `c:CT_TickMark` | 1 | 0 | 4 | §21.2.2.101 p.3413, OI 2.1.1492 |
| [`c:majorTimeUnit`](#cmajortimeunit) | `c:CT_TimeUnit` | 1 | 0 | 1 | §21.2.2.102 p.3413 |
| [`c:majorUnit`](#cmajorunit) | `c:CT_AxisUnit` | 1 | 0 | 2 | §21.2.2.103 p.3413, OI 2.1.1493 |
| [`c:manualLayout`](#cmanuallayout) | `c:CT_ManualLayout` | 0 | 10 | 2 | §21.2.2.104 p.3413, OI 2.1.1494 |
| [`c:marker`](#cmarker) | `c:CT_Marker`, `c:CT_Boolean` | 1 | 4 | 5 | §21.2.2.105 p.3414, OI 2.1.1495; §21.2.2.106 p.3414, OI 2.1.1496 |
| [`c:max`](#cmax) | `c:CT_Double` | 1 | 0 | 1 | §21.2.2.107 p.3414, OI 2.1.1497 |
| [`c:min`](#cmin) | `c:CT_Double` | 1 | 0 | 1 | §21.2.2.108 p.3415, OI 2.1.1498 |
| [`c:minorGridlines`](#cminorgridlines) | `c:CT_ChartLines` | 0 | 1 | 4 | §21.2.2.109 p.3415 |
| [`c:minorTickMark`](#cminortickmark) | `c:CT_TickMark` | 1 | 0 | 4 | §21.2.2.110 p.3415 |
| [`c:minorTimeUnit`](#cminortimeunit) | `c:CT_TimeUnit` | 1 | 0 | 1 | §21.2.2.111 p.3415 |
| [`c:minorUnit`](#cminorunit) | `c:CT_AxisUnit` | 1 | 0 | 2 | §21.2.2.112 p.3416, OI 2.1.1499 |
| [`c:minus`](#cminus) | `c:CT_NumDataSource` | 0 | 2 | 1 | §21.2.2.113 p.3416, OI 2.1.1500 |
| [`c:multiLvlStrCache`](#cmultilvlstrcache) | `c:CT_MultiLvlStrData` | 0 | 3 | 1 | §21.2.2.114 p.3416, OI 2.1.1501 |
| [`c:multiLvlStrRef`](#cmultilvlstrref) | `c:CT_MultiLvlStrRef` | 0 | 3 | 3 | §21.2.2.115 p.3416, OI 2.1.1502 |
| [`c:name`](#cname) | `s:ST_Xstring`, `xsd:string` | 0 | 0 | 3 | §21.2.2.116 p.3416; §21.2.2.117 p.3416, OI 2.1.1503 |
| [`c:noEndCap`](#cnoendcap) | `c:CT_Boolean` | 1 | 0 | 1 | §21.2.2.118 p.3417 |
| [`c:noMultiLvlLbl`](#cnomultilvllbl) | `c:CT_Boolean` | 1 | 0 | 1 | §21.2.2.119 p.3417 |
| [`c:numCache`](#cnumcache) | `c:CT_NumData` | 0 | 4 | 1 | §21.2.2.120 p.3418 |
| [`c:numFmt`](#cnumfmt) | `c:CT_NumFmt` | 2 | 0 | 9 | §21.2.2.121 p.3418, OI 2.1.1504 |
| [`c:numLit`](#cnumlit) | `c:CT_NumData` | 0 | 4 | 8 | §21.2.2.122 p.3418 |
| [`c:numRef`](#cnumref) | `c:CT_NumRef` | 0 | 3 | 8 | §21.2.2.123 p.3418 |
| [`c:oddFooter`](#coddfooter) | `s:ST_Xstring` | 0 | 0 | 1 | §21.2.2.124 p.3418, OI 2.1.1505 |
| [`c:oddHeader`](#coddheader) | `s:ST_Xstring` | 0 | 0 | 1 | §21.2.2.125 p.3419, OI 2.1.1506 |
| [`c:ofPieChart`](#cofpiechart) | `c:CT_OfPieChart` | 0 | 11 | 1 | §21.2.2.126 p.3419, OI 2.1.1507 |
| [`c:ofPieType`](#cofpietype) | `c:CT_OfPieType` | 1 | 0 | 1 | §21.2.2.127 p.3420, OI 2.1.1508 |
| [`c:order`](#corder) | `c:CT_Order`, `c:CT_UnsignedInt` | 1 | 0 | 3 | §21.2.2.128 p.3420, OI 2.1.1509; §21.2.2.129 p.3420, OI 2.1.1510 |
| [`c:orientation`](#corientation) | `c:CT_Orientation` | 1 | 0 | 1 | §21.2.2.130 p.3420, OI 2.1.1511 |
| [`c:overlap`](#coverlap) | `c:CT_Overlap` | 1 | 0 | 1 | §21.2.2.131 p.3421, OI 2.1.1512 |
| [`c:overlay`](#coverlay) | `c:CT_Boolean` | 1 | 0 | 2 | §21.2.2.132 p.3421 |
| [`c:pageMargins`](#cpagemargins) | `c:CT_PageMargins` | 6 | 0 | 1 | §21.2.2.133 p.3422, OI 2.1.1513 |
| [`c:pageSetup`](#cpagesetup) | `c:CT_PageSetup` | 11 | 0 | 1 | §21.2.2.134 p.3422, OI 2.1.1514 |
| [`c:period`](#cperiod) | `c:CT_Period` | 1 | 0 | 1 | §21.2.2.135 p.3426, OI 2.1.1515 |
| [`c:perspective`](#cperspective) | `c:CT_Perspective` | 1 | 0 | 1 | §21.2.2.136 p.3426, OI 2.1.1516 |
| [`c:pictureFormat`](#cpictureformat) | `c:CT_PictureFormat` | 1 | 0 | 1 | §21.2.2.137 p.3427 |
| [`c:pictureOptions`](#cpictureoptions) | `c:CT_PictureOptions` | 0 | 5 | 6 | §21.2.2.138 p.3427, OI 2.1.1517 |
| [`c:pictureStackUnit`](#cpicturestackunit) | `c:CT_PictureStackUnit` | 1 | 0 | 1 | §21.2.2.139 p.3427 |
| [`c:pie3DChart`](#cpie3dchart) | `c:CT_Pie3DChart` | 0 | 4 | 1 | §21.2.2.140 p.3428, OI 2.1.1518 |
| [`c:pieChart`](#cpiechart) | `c:CT_PieChart` | 0 | 5 | 1 | §21.2.2.141 p.3428, OI 2.1.1519 |
| [`c:pivotFmt`](#cpivotfmt) | `c:CT_PivotFmt` | 0 | 6 | 1 | §21.2.2.142 p.3428, OI 2.1.1520 |
| [`c:pivotFmts`](#cpivotfmts) | `c:CT_PivotFmts` | 0 | 1 | 1 | §21.2.2.143 p.3428, OI 2.1.1521 |
| [`c:pivotSource`](#cpivotsource) | `c:CT_PivotSource` | 0 | 3 | 1 | §21.2.2.144 p.3428, OI 2.1.1522 |
| [`c:plotArea`](#cplotarea) | `c:CT_PlotArea` | 0 | 24 | 1 | §21.2.2.145 p.3428, OI 2.1.1523 |
| [`c:plotVisOnly`](#cplotvisonly) | `c:CT_Boolean` | 1 | 0 | 1 | §21.2.2.146 p.3428 |
| [`c:plus`](#cplus) | `c:CT_NumDataSource` | 0 | 2 | 1 | §21.2.2.147 p.3429, OI 2.1.1524 |
| [`c:printSettings`](#cprintsettings) | `c:CT_PrintSettings` | 0 | 4 | 1 | §21.2.2.148 p.3429, OI 2.1.1525 |
| [`c:protection`](#cprotection) | `c:CT_Protection` | 0 | 5 | 1 | §21.2.2.149 p.3429, OI 2.1.1526 |
| [`c:pt`](#cpt) | `c:CT_NumVal`, `c:CT_StrVal` | 2 | 1 | 9 | §21.2.2.150 p.3429, OI 2.1.1527; §21.2.2.151 p.3430, OI 2.1.1528 |
| [`c:ptCount`](#cptcount) | `c:CT_UnsignedInt` | 1 | 0 | 11 | §21.2.2.152 p.3430, OI 2.1.1529 |
| [`c:rAngAx`](#crangax) | `c:CT_Boolean` | 1 | 0 | 1 | §21.2.2.155 p.3431, OI 2.1.1532 |
| [`c:radarChart`](#cradarchart) | `c:CT_RadarChart` | 0 | 6 | 1 | §21.2.2.153 p.3430, OI 2.1.1530 |
| [`c:radarStyle`](#cradarstyle) | `c:CT_RadarStyle` | 1 | 0 | 1 | §21.2.2.154 p.3430, OI 2.1.1531 |
| [`c:rich`](#crich) | `a:CT_TextBody` | 0 | 3 | 2 | §21.2.2.156 p.3431, OI 2.1.1533 |
| [`c:rotX`](#crotx) | `c:CT_RotX` | 1 | 0 | 1 | §21.2.2.157 p.3431 |
| [`c:rotY`](#croty) | `c:CT_RotY` | 1 | 0 | 1 | §21.2.2.158 p.3432 |
| [`c:roundedCorners`](#croundedcorners) | `c:CT_Boolean` | 1 | 0 | 1 | §21.2.2.159 p.3432 |
| [`c:scaling`](#cscaling) | `c:CT_Scaling` | 0 | 5 | 4 | §21.2.2.160 p.3432 |
| [`c:scatterChart`](#cscatterchart) | `c:CT_ScatterChart` | 0 | 6 | 1 | §21.2.2.161 p.3433, OI 2.1.1534 |
| [`c:scatterStyle`](#cscatterstyle) | `c:CT_ScatterStyle` | 1 | 0 | 1 | §21.2.2.162 p.3433 |
| [`c:secondPiePt`](#csecondpiept) | `c:CT_UnsignedInt` | 1 | 0 | 1 | §21.2.2.163 p.3433, OI 2.1.1535 |
| [`c:secondPieSize`](#csecondpiesize) | `c:CT_SecondPieSize` | 1 | 0 | 1 | §21.2.2.164 p.3433 |
| [`c:selection`](#cselection) | `c:CT_Boolean` | 1 | 0 | 1 | §21.2.2.165 p.3434 |
| [`c:separator`](#cseparator) | `xsd:string` | 0 | 0 | 4 | §21.2.2.166 p.3434, OI 2.1.1536 |
| [`c:ser`](#cser) | `c:CT_LineSer`, `c:CT_ScatterSer`, `c:CT_RadarSer`, `c:CT_BarSer`, `c:CT_AreaSer`, `c:CT_PieSer`, `c:CT_BubbleSer`, `c:CT_SurfaceSer` | 0 | 21 | 16 | §21.2.2.167 p.3434, OI 2.1.1537; §21.2.2.168 p.3434, OI 2.1.1538; §21.2.2.169 p.3434, OI 2.1.1539; §21.2.2.170 p.3435, OI 2.1.1540; §21.2.2.171 p.3435, OI 2.1.1541; §21.2.2.172 p.3435, OI 2.1.1542; §21.2.2.173 p.3435; §21.2.2.174 p.3435, OI 2.1.1543 |
| [`c:serAx`](#cserax) | `c:CT_SerAx` | 0 | 19 | 1 | §21.2.2.175 p.3435, OI 2.1.1544 |
| [`c:serLines`](#cserlines) | `c:CT_ChartLines` | 0 | 1 | 2 | §21.2.2.176 p.3435, OI 2.1.1545 |
| [`c:shape`](#cshape) | `c:CT_Shape` | 1 | 0 | 3 | §21.2.2.177 p.3436, OI 2.1.1546 |
| [`c:showBubbleSize`](#cshowbubblesize) | `c:CT_Boolean` | 1 | 0 | 4 | §21.2.2.178 p.3436, OI 2.1.1547 |
| [`c:showCatName`](#cshowcatname) | `c:CT_Boolean` | 1 | 0 | 4 | §21.2.2.179 p.3436 |
| [`c:showDLblsOverMax`](#cshowdlblsovermax) | `c:CT_Boolean` | 1 | 0 | 1 | §21.2.2.180 p.3437, OI 2.1.1548 |
| [`c:showHorzBorder`](#cshowhorzborder) | `c:CT_Boolean` | 1 | 0 | 1 | §21.2.2.181 p.3437 |
| [`c:showKeys`](#cshowkeys) | `c:CT_Boolean` | 1 | 0 | 1 | §21.2.2.182 p.3438 |
| [`c:showLeaderLines`](#cshowleaderlines) | `c:CT_Boolean` | 1 | 0 | 1 | §21.2.2.183 p.3438, OI 2.1.1549 |
| [`c:showLegendKey`](#cshowlegendkey) | `c:CT_Boolean` | 1 | 0 | 4 | §21.2.2.184 p.3438, OI 2.1.1550 |
| [`c:showNegBubbles`](#cshownegbubbles) | `c:CT_Boolean` | 1 | 0 | 1 | §21.2.2.185 p.3439 |
| [`c:showOutline`](#cshowoutline) | `c:CT_Boolean` | 1 | 0 | 1 | §21.2.2.186 p.3439 |
| [`c:showPercent`](#cshowpercent) | `c:CT_Boolean` | 1 | 0 | 4 | §21.2.2.187 p.3440, OI 2.1.1551 |
| [`c:showSerName`](#cshowsername) | `c:CT_Boolean` | 1 | 0 | 4 | §21.2.2.188 p.3440 |
| [`c:showVal`](#cshowval) | `c:CT_Boolean` | 1 | 0 | 4 | §21.2.2.189 p.3441 |
| [`c:showVertBorder`](#cshowvertborder) | `c:CT_Boolean` | 1 | 0 | 1 | §21.2.2.190 p.3441 |
| [`c:sideWall`](#csidewall) | `c:CT_Surface` | 0 | 4 | 1 | §21.2.2.191 p.3441, OI 2.1.1552 |
| [`c:size`](#csize) | `c:CT_MarkerSize` | 1 | 0 | 3 | §21.2.2.192 p.3442 |
| [`c:sizeRepresents`](#csizerepresents) | `c:CT_SizeRepresents` | 1 | 0 | 1 | §21.2.2.193 p.3442 |
| [`c:smooth`](#csmooth) | `c:CT_Boolean` | 1 | 0 | 3 | §21.2.2.194 p.3442 |
| [`c:spPr`](#csppr) | `a:CT_ShapeProperties` | 1 | 15 | 37 | §21.2.2.197 p.3443, OI 2.1.1555 |
| [`c:splitPos`](#csplitpos) | `c:CT_Double` | 1 | 0 | 1 | §21.2.2.195 p.3443, OI 2.1.1553 |
| [`c:splitType`](#csplittype) | `c:CT_SplitType` | 1 | 0 | 1 | §21.2.2.196 p.3443, OI 2.1.1554 |
| [`c:stockChart`](#cstockchart) | `c:CT_StockChart` | 0 | 7 | 1 | §21.2.2.198 p.3444 |
| [`c:strCache`](#cstrcache) | `c:CT_StrData` | 0 | 3 | 1 | §21.2.2.199 p.3444 |
| [`c:strLit`](#cstrlit) | `c:CT_StrData` | 0 | 3 | 3 | §21.2.2.200 p.3444 |
| [`c:strRef`](#cstrref) | `c:CT_StrRef` | 0 | 3 | 5 | §21.2.2.201 p.3444 |
| [`c:style`](#cstyle) | `c:CT_Style` | 1 | 0 | 1 | §21.2.2.202 p.3444 |
| [`c:surface3DChart`](#csurface3dchart) | `c:CT_Surface3DChart` | 0 | 5 | 1 | §21.2.2.203 p.3445, OI 2.1.1556 |
| [`c:surfaceChart`](#csurfacechart) | `c:CT_SurfaceChart` | 0 | 5 | 1 | §21.2.2.204 p.3445, OI 2.1.1557 |
| [`c:symbol`](#csymbol) | `c:CT_MarkerStyle` | 1 | 0 | 3 | §21.2.2.205 p.3445 |
| [`c:thickness`](#cthickness) | `c:CT_Thickness` | 1 | 0 | 3 | §21.2.2.206 p.3445, OI 2.1.1558 |
| [`c:tickLblPos`](#cticklblpos) | `c:CT_TickLblPos` | 1 | 0 | 4 | §21.2.2.207 p.3446 |
| [`c:tickLblSkip`](#cticklblskip) | `c:CT_Skip` | 1 | 0 | 2 | §21.2.2.208 p.3446, OI 2.1.1559 |
| [`c:tickMarkSkip`](#ctickmarkskip) | `c:CT_Skip` | 1 | 0 | 2 | §21.2.2.209 p.3446, OI 2.1.1560 |
| [`c:title`](#ctitle) | `c:CT_Title` | 0 | 6 | 5 | §21.2.2.210 p.3446, OI 2.1.1561 |
| [`c:trendline`](#ctrendline) | `c:CT_Trendline` | 0 | 12 | 2 | §21.2.2.211 p.3446, OI 2.1.1562 |
| [`c:trendlineLbl`](#ctrendlinelbl) | `c:CT_TrendlineLbl` | 0 | 6 | 1 | §21.2.2.212 p.3447, OI 2.1.1563 |
| [`c:trendlineType`](#ctrendlinetype) | `c:CT_TrendlineType` | 1 | 0 | 1 | §21.2.2.213 p.3447, OI 2.1.1564 |
| [`c:tx`](#ctx) | `c:CT_Tx`, `c:CT_SerTx` | 0 | 3 | 8 | §21.2.2.214 p.3447; §21.2.2.215 p.3447, OI 2.1.1565 |
| [`c:txPr`](#ctxpr) | `a:CT_TextBody` | 0 | 3 | 16 | §21.2.2.216 p.3447, OI 2.1.1566 |
| [`c:upBars`](#cupbars) | `c:CT_UpDownBar` | 0 | 1 | 1 | §21.2.2.217 p.3447 |
| [`c:upDownBars`](#cupdownbars) | `c:CT_UpDownBars` | 0 | 4 | 2 | §21.2.2.218 p.3448 |
| [`c:userInterface`](#cuserinterface) | `c:CT_Boolean` | 1 | 0 | 1 | §21.2.2.219 p.3448, OI 2.1.1567 |
| [`c:userShapes`](#cusershapes) ◆ | `cdr:CT_Drawing`, `c:CT_RelId` | 1 | 2 | 1 | §21.2.2.220 p.3448; §21.2.2.221 p.3448 |
| [`c:v`](#cv) | `s:ST_Xstring` | 0 | 0 | 3 | §21.2.2.222 p.3449, OI 2.1.1568; §21.2.2.223 p.3449 |
| [`c:val`](#cval) | `c:CT_Double`, `c:CT_NumDataSource` | 1 | 2 | 3 | §21.2.2.224 p.3449; §21.2.2.225 p.3449, OI 2.1.1569 |
| [`c:valAx`](#cvalax) | `c:CT_ValAx` | 0 | 21 | 1 | §21.2.2.226 p.3449, OI 2.1.1570 |
| [`c:varyColors`](#cvarycolors) | `c:CT_Boolean` | 1 | 0 | 13 | §21.2.2.227 p.3450, OI 2.1.1571 |
| [`c:view3D`](#cview3d) | `c:CT_View3D` | 0 | 7 | 1 | §21.2.2.228 p.3450, OI 2.1.1572 |
| [`c:w`](#cw) | `c:CT_Double` | 1 | 0 | 1 | §21.2.2.229 p.3450, OI 2.1.1573 |
| [`c:wMode`](#cwmode) | `c:CT_LayoutMode` | 1 | 0 | 1 | §21.2.2.231 p.3451, OI 2.1.1574 |
| [`c:wireframe`](#cwireframe) | `c:CT_Boolean` | 1 | 0 | 2 | §21.2.2.230 p.3451 |
| [`c:x`](#cx) | `c:CT_Double` | 1 | 0 | 1 | §21.2.2.232 p.3451, OI 2.1.1575 |
| [`c:xMode`](#cxmode) | `c:CT_LayoutMode` | 1 | 0 | 1 | §21.2.2.233 p.3452, OI 2.1.1576 |
| [`c:xVal`](#cxval) | `c:CT_AxDataSource` | 0 | 5 | 2 | §21.2.2.234 p.3452 |
| [`c:y`](#cy) | `c:CT_Double` | 1 | 0 | 1 | §21.2.2.235 p.3452, OI 2.1.1577 |
| [`c:yMode`](#cymode) | `c:CT_LayoutMode` | 1 | 0 | 1 | §21.2.2.236 p.3452, OI 2.1.1578 |
| [`c:yVal`](#cyval) | `c:CT_NumDataSource` | 0 | 2 | 2 | §21.2.2.237 p.3453 |

◆ declared globally (can be the root element of a part)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `c:ST_AxPos` | restriction of xsd:string | `b`, `l`, `r`, `t` | §21.2.3.2 p.3453 |
| `c:ST_AxisUnit` | restriction of xsd:double | minExclusive `0` | §21.2.3.1 p.3453, OI 2.1.1579 |
| `c:ST_BarDir` | restriction of xsd:string | `bar`, `col` | §21.2.3.3 p.3454, OI 2.1.1580 |
| `c:ST_BarGrouping` | restriction of xsd:string | `percentStacked`, `clustered`, `standard`, `stacked` | §21.2.3.4 p.3454, OI 2.1.1581 |
| `c:ST_BubbleScale` | union of c:ST_BubbleScalePercent, c:ST_BubbleScaleUInt |  | §21.2.3.5 p.3455 |
| `c:ST_BubbleScalePercent` | restriction of xsd:string | pattern `0*(([0-9])\|([1-9][0-9])\|([1-2][0-9][0-9])\|300)%` | §21.2.3.58 p.3482 |
| `c:ST_BubbleScaleUInt` | restriction of xsd:unsignedInt | minInclusive `0`, maxInclusive `300` | Part 4 §18.1.3.16 p.298 |
| `c:ST_BuiltInUnit` | restriction of xsd:string | `hundreds`, `thousands`, `tenThousands`, `hundredThousands`, `millions`, `tenMillions`, `hundredMillions`, `billions`, `trillions` | §21.2.3.6 p.3455, OI 2.1.1582 |
| `c:ST_CrossBetween` | restriction of xsd:string | `between`, `midCat` | §21.2.3.7 p.3455 |
| `c:ST_Crosses` | restriction of xsd:string | `autoZero`, `max`, `min` | §21.2.3.8 p.3456, OI 2.1.1583 |
| `c:ST_DLblPos` | restriction of xsd:string | `bestFit`, `b`, `ctr`, `inBase`, `inEnd`, `l`, `outEnd`, `r`, `t` | §21.2.3.11 p.3457 |
| `c:ST_DepthPercent` | union of c:ST_DepthPercentWithSymbol, c:ST_DepthPercentUShort |  | §21.2.3.9 p.3456 |
| `c:ST_DepthPercentUShort` | restriction of xsd:unsignedShort | minInclusive `20`, maxInclusive `2000` | Part 4 §18.1.3.2 p.295 |
| `c:ST_DepthPercentWithSymbol` | restriction of xsd:string | pattern `0*(([2-9][0-9])\|([1-9][0-9][0-9])\|(1[0-9][0-9][0-9])\|2000)%` | §21.2.3.51 p.3481 |
| `c:ST_DispBlanksAs` | restriction of xsd:string | `span`, `gap`, `zero` | §21.2.3.10 p.3456 |
| `c:ST_ErrBarType` | restriction of xsd:string | `both`, `minus`, `plus` | §21.2.3.12 p.3458 |
| `c:ST_ErrDir` | restriction of xsd:string | `x`, `y` | §21.2.3.13 p.3458 |
| `c:ST_ErrValType` | restriction of xsd:string | `cust`, `fixedVal`, `percentage`, `stdDev`, `stdErr` | §21.2.3.14 p.3458, OI 2.1.1584 |
| `c:ST_FirstSliceAng` | restriction of xsd:unsignedShort | minInclusive `0`, maxInclusive `360` | §21.2.3.15 p.3459 |
| `c:ST_GapAmount` | union of c:ST_GapAmountPercent, c:ST_GapAmountUShort |  | §21.2.3.16 p.3459 |
| `c:ST_GapAmountPercent` | restriction of xsd:string | pattern `0*(([0-9])\|([1-9][0-9])\|([1-4][0-9][0-9])\|500)%` | §21.2.3.53 p.3482 |
| `c:ST_GapAmountUShort` | restriction of xsd:unsignedShort | minInclusive `0`, maxInclusive `500` | Part 4 §18.1.3.6 p.296 |
| `c:ST_Grouping` | restriction of xsd:string | `percentStacked`, `standard`, `stacked` | §21.2.3.17 p.3459, OI 2.1.1585 |
| `c:ST_HPercent` | union of c:ST_HPercentWithSymbol, c:ST_HPercentUShort |  | §21.2.3.19 p.3460 |
| `c:ST_HPercentUShort` | restriction of xsd:unsignedShort | minInclusive `5`, maxInclusive `500` | Part 4 §18.1.3.4 p.296 |
| `c:ST_HPercentWithSymbol` | restriction of xsd:string | pattern `0*(([5-9])\|([1-9][0-9])\|([1-4][0-9][0-9])\|500)%` | §21.2.3.52 p.3482 |
| `c:ST_HoleSize` | union of c:ST_HoleSizePercent, c:ST_HoleSizeUByte |  | §21.2.3.18 p.3460, OI 2.1.1586 |
| `c:ST_HoleSizePercent` | restriction of xsd:string | pattern `0*([1-9]\|([1-8][0-9])\|90)%` | §21.2.3.55 p.3482 |
| `c:ST_HoleSizeUByte` | restriction of xsd:unsignedByte | minInclusive `1`, maxInclusive `90` | Part 4 §18.1.3.10 p.297 |
| `c:ST_LayoutMode` | restriction of xsd:string | `edge`, `factor` | §21.2.3.20 p.3460, OI 2.1.1587 |
| `c:ST_LayoutTarget` | restriction of xsd:string | `inner`, `outer` | §21.2.3.21 p.3461 |
| `c:ST_LblAlgn` | restriction of xsd:string | `ctr`, `l`, `r` | §21.2.3.22 p.3461 |
| `c:ST_LblOffset` | union of c:ST_LblOffsetPercent, c:ST_LblOffsetUShort |  | §21.2.3.23 p.3461 |
| `c:ST_LblOffsetPercent` | restriction of xsd:string | pattern `0*(([0-9])\|([1-9][0-9])\|([1-9][0-9][0-9])\|1000)%` | §21.2.3.56 p.3482 |
| `c:ST_LblOffsetUShort` | restriction of xsd:unsignedShort | minInclusive `0`, maxInclusive `1000` | Part 4 §18.1.3.12 p.297 |
| `c:ST_LegendPos` | restriction of xsd:string | `b`, `tr`, `l`, `r`, `t` | §21.2.3.24 p.3462 |
| `c:ST_LogBase` | restriction of xsd:double | minInclusive `2`, maxInclusive `1000` | §21.2.3.25 p.3462 |
| `c:ST_MarkerSize` | restriction of xsd:unsignedByte | minInclusive `2`, maxInclusive `72` | §21.2.3.26 p.3462 |
| `c:ST_MarkerStyle` | restriction of xsd:string | `circle`, `dash`, `diamond`, `dot`, `none`, `picture`, `plus`, `square`, `star`, `triangle`, `x`, `auto` | §21.2.3.27 p.3463, OI 2.1.1588 |
| `c:ST_OfPieType` | restriction of xsd:string | `pie`, `bar` | §21.2.3.28 p.3464 |
| `c:ST_Order` | restriction of xsd:unsignedByte | minInclusive `2`, maxInclusive `6` | §21.2.3.29 p.3464 |
| `c:ST_Orientation` | restriction of xsd:string | `maxMin`, `minMax` | §21.2.3.30 p.3464, OI 2.1.1589 |
| `c:ST_Overlap` | union of c:ST_OverlapPercent, c:ST_OverlapByte |  | §21.2.3.31 p.3465 |
| `c:ST_OverlapByte` | restriction of xsd:byte | minInclusive `-100`, maxInclusive `100` | Part 4 §18.1.3.14 p.298 |
| `c:ST_OverlapPercent` | restriction of xsd:string | pattern `(-?0*(([0-9])\|([1-9][0-9])\|100))%` | §21.2.3.57 p.3482 |
| `c:ST_PageSetupOrientation` | restriction of xsd:string | `default`, `portrait`, `landscape` | §21.2.3.32 p.3465 |
| `c:ST_Period` | restriction of xsd:unsignedInt | minInclusive `2` | §21.2.3.33 p.3465, OI 2.1.1590 |
| `c:ST_Perspective` | restriction of xsd:unsignedByte | minInclusive `0`, maxInclusive `240` | §21.2.3.34 p.3466, OI 2.1.1591 |
| `c:ST_PictureFormat` | restriction of xsd:string | `stretch`, `stack`, `stackScale` | §21.2.3.35 p.3466, OI 2.1.1592 |
| `c:ST_PictureStackUnit` | restriction of xsd:double | minExclusive `0` | §21.2.3.36 p.3466 |
| `c:ST_RadarStyle` | restriction of xsd:string | `standard`, `marker`, `filled` | §21.2.3.37 p.3467, OI 2.1.1593 |
| `c:ST_RotX` | restriction of xsd:byte | minInclusive `-90`, maxInclusive `90` | §21.2.3.38 p.3467 |
| `c:ST_RotY` | restriction of xsd:unsignedShort | minInclusive `0`, maxInclusive `360` | §21.2.3.39 p.3468 |
| `c:ST_ScatterStyle` | restriction of xsd:string | `none`, `line`, `lineMarker`, `marker`, `smooth`, `smoothMarker` | §21.2.3.40 p.3468, OI 2.1.1594 |
| `c:ST_SecondPieSize` | union of c:ST_SecondPieSizePercent, c:ST_SecondPieSizeUShort |  | §21.2.3.41 p.3469 |
| `c:ST_SecondPieSizePercent` | restriction of xsd:string | pattern `0*(([5-9])\|([1-9][0-9])\|(1[0-9][0-9])\|200)%` | §21.2.3.54 p.3482 |
| `c:ST_SecondPieSizeUShort` | restriction of xsd:unsignedShort | minInclusive `5`, maxInclusive `200` | Part 4 §18.1.3.8 p.296 |
| `c:ST_Shape` | restriction of xsd:string | `cone`, `coneToMax`, `box`, `cylinder`, `pyramid`, `pyramidToMax` | §21.2.3.42 p.3469, OI 2.1.1595 |
| `c:ST_SizeRepresents` | restriction of xsd:string | `area`, `w` | §21.2.3.43 p.3469 |
| `c:ST_Skip` | restriction of xsd:unsignedInt | minInclusive `1` | §21.2.3.44 p.3470, OI 2.1.1596 |
| `c:ST_SplitType` | restriction of xsd:string | `auto`, `cust`, `percent`, `pos`, `val` | §21.2.3.45 p.3470, OI 2.1.1597 |
| `c:ST_Style` | restriction of xsd:unsignedByte | minInclusive `1`, maxInclusive `48` | §21.2.3.46 p.3471, OI 2.1.1598 |
| `c:ST_Thickness` | union of c:ST_ThicknessPercent, xsd:unsignedInt |  | §21.2.3.59 p.3483 |
| `c:ST_ThicknessPercent` | restriction of xsd:string | pattern `([0-9]+)%` | §21.2.3.60 p.3483 |
| `c:ST_TickLblPos` | restriction of xsd:string | `high`, `low`, `nextTo`, `none` | §21.2.3.47 p.3480 |
| `c:ST_TickMark` | restriction of xsd:string | `cross`, `in`, `none`, `out` | §21.2.3.48 p.3480 |
| `c:ST_TimeUnit` | restriction of xsd:string | `days`, `months`, `years` | §21.2.3.49 p.3480 |
| `c:ST_TrendlineType` | restriction of xsd:string | `exp`, `linear`, `log`, `movingAvg`, `poly`, `power` | §21.2.3.50 p.3481, OI 2.1.1599 |

## Element details

### c:applyToEnd

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c:pictureOptions`
- Specification: §21.2.2.1 p.3381

### c:applyToFront

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c:pictureOptions`
- Specification: §21.2.2.2 p.3382, OI 2.1.1428

### c:applyToSides

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c:pictureOptions`
- Specification: §21.2.2.3 p.3382, OI 2.1.1429

### c:area3DChart

- Type: `c:CT_Area3DChart`
- Children: `c:grouping`, `c:varyColors`, `c:ser`, `c:dLbls`, `c:dropLines`, `c:gapDepth`, `c:axId`, `c:extLst`
- Parents: `c:plotArea`
- Specification: §21.2.2.4 p.3382

### c:areaChart

- Type: `c:CT_AreaChart`
- Children: `c:grouping`, `c:varyColors`, `c:ser`, `c:dLbls`, `c:dropLines`, `c:axId`, `c:extLst`
- Parents: `c:plotArea`
- Specification: §21.2.2.5 p.3383, OI 2.1.1430

### c:auto

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c:catAx`, `c:dateAx`
- Specification: §21.2.2.6 p.3383

### c:autoTitleDeleted

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c:chart`
- Specification: §21.2.2.7 p.3383, OI 2.1.1431

### c:autoUpdate

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c:externalData`
- Specification: §21.2.2.8 p.3384

### c:axId

- Type: `c:CT_UnsignedInt`
- Attributes: `val`!
- Parents: `c:area3DChart`, `c:areaChart`, `c:bar3DChart`, `c:barChart`, `c:bubbleChart`, `c:catAx`, `c:dateAx`, `c:line3DChart`, `c:lineChart`, `c:radarChart`, `c:scatterChart`, `c:serAx`, `c:stockChart`, `c:surface3DChart`, `c:surfaceChart`, `c:valAx`
- Specification: §21.2.2.9 p.3384, OI 2.1.1432

### c:axPos

- Type: `c:CT_AxPos`
- Attributes: `val`!
- Parents: `c:catAx`, `c:dateAx`, `c:serAx`, `c:valAx`
- Specification: §21.2.2.10 p.3384, OI 2.1.1433

### c:backWall

- Type: `c:CT_Surface`
- Children: `c:thickness`, `c:spPr`, `c:pictureOptions`, `c:extLst`
- Parents: `c:chart`
- Specification: §21.2.2.11 p.3385, OI 2.1.1434

### c:backward

- Type: `c:CT_Double`
- Attributes: `val`!
- Parents: `c:trendline`
- Specification: §21.2.2.12 p.3385, OI 2.1.1435

### c:bandFmt

- Type: `c:CT_BandFmt`
- Children: `c:idx`, `c:spPr`
- Parents: `c:bandFmts`
- Specification: §21.2.2.13 p.3385

### c:bandFmts

- Type: `c:CT_BandFmts`
- Children: `c:bandFmt`
- Parents: `c:surface3DChart`, `c:surfaceChart`
- Specification: §21.2.2.14 p.3385, OI 2.1.1436

### c:bar3DChart

- Type: `c:CT_Bar3DChart`
- Children: `c:barDir`, `c:grouping`, `c:varyColors`, `c:ser`, `c:dLbls`, `c:gapWidth`, `c:gapDepth`, `c:shape`, `c:axId`, `c:extLst`
- Parents: `c:plotArea`
- Specification: §21.2.2.15 p.3386, OI 2.1.1437

### c:barChart

- Type: `c:CT_BarChart`
- Children: `c:barDir`, `c:grouping`, `c:varyColors`, `c:ser`, `c:dLbls`, `c:gapWidth`, `c:overlap`, `c:serLines`, `c:axId`, `c:extLst`
- Parents: `c:plotArea`
- Specification: §21.2.2.16 p.3386, OI 2.1.1438

### c:barDir

- Type: `c:CT_BarDir`
- Attributes: `val`
- Parents: `c:bar3DChart`, `c:barChart`
- Specification: §21.2.2.17 p.3386, OI 2.1.1439

### c:baseTimeUnit

- Type: `c:CT_TimeUnit`
- Attributes: `val`
- Parents: `c:dateAx`
- Specification: §21.2.2.18 p.3386

### c:bubble3D

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c15:ser`, `c:bubbleChart`, `c:dPt`, `c:ser`
- Specification: §21.2.2.19 p.3386, OI 2.1.1440

### c:bubbleChart

- Type: `c:CT_BubbleChart`
- Children: `c:varyColors`, `c:ser`, `c:dLbls`, `c:bubble3D`, `c:bubbleScale`, `c:showNegBubbles`, `c:sizeRepresents`, `c:axId`, `c:extLst`
- Parents: `c:plotArea`
- Specification: §21.2.2.20 p.3387, OI 2.1.1441

### c:bubbleScale

- Type: `c:CT_BubbleScale`
- Attributes: `val`
- Parents: `c:bubbleChart`
- Specification: §21.2.2.21 p.3387

### c:bubbleSize

- Type: `c:CT_NumDataSource`
- Children: `c:numRef`, `c:numLit`
- Parents: `c15:ser`, `c:ser`
- Specification: §21.2.2.22 p.3387

### c:builtInUnit

- Type: `c:CT_BuiltInUnit`
- Attributes: `val`
- Parents: `c:dispUnits`
- Specification: §21.2.2.23 p.3387, OI 2.1.1442

### c:cat

- Type: `c:CT_AxDataSource`
- Children: `c:multiLvlStrRef`, `c:numRef`, `c:numLit`, `c:strRef`, `c:strLit`
- Parents: `c15:ser`, `c:ser`
- Specification: §21.2.2.24 p.3388, OI 2.1.1443

### c:catAx

- Type: `c:CT_CatAx`
- Children: `c:axId`, `c:scaling`, `c:delete`, `c:axPos`, `c:majorGridlines`, `c:minorGridlines`, `c:title`, `c:numFmt`, `c:majorTickMark`, `c:minorTickMark`, `c:tickLblPos`, `c:spPr`, `c:txPr`, `c:crossAx`, `c:crosses`, `c:crossesAt`, `c:auto`, `c:lblAlgn`, `c:lblOffset`, `c:tickLblSkip`, `c:tickMarkSkip`, `c:noMultiLvlLbl`, `c:extLst`
- Parents: `c:plotArea`
- Specification: §21.2.2.25 p.3388, OI 2.1.1444

### c:chart

- Type: `c:CT_RelId`, `c:CT_Chart`
- Attributes: `r:id`!
- Children: `c:title`, `c:autoTitleDeleted`, `c:pivotFmts`, `c:view3D`, `c:floor`, `c:sideWall`, `c:backWall`, `c:plotArea`, `c:legend`, `c:plotVisOnly`, `c:dispBlanksAs`, `c:showDLblsOverMax`, `c:extLst`
- Parents: `c:chartSpace`
- Specification: §21.2.2.26 p.3388; §21.2.2.27 p.3388

### c:chartObject

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c:protection`
- Specification: §21.2.2.28 p.3389, OI 2.1.1445

### c:chartSpace

- Type: `c:CT_ChartSpace`
- Children: `c:date1904`, `c:lang`, `c:roundedCorners`, `c:style`, `c:clrMapOvr`, `c:pivotSource`, `c:protection`, `c:chart`, `c:spPr`, `c:txPr`, `c:externalData`, `c:printSettings`, `c:userShapes`, `c:extLst`
- Specification: §21.2.2.29 p.3389

### c:clrMapOvr

- Type: `a:CT_ColorMapping`
- Attributes: `accent1`!, `accent2`!, `accent3`!, `accent4`!, `accent5`!, `accent6`!, `bg1`!, `bg2`!, `folHlink`!, `hlink`!, `tx1`!, `tx2`!
- Children: `a:extLst`
- Parents: `c:chartSpace`
- Specification: §21.2.2.30 p.3389

### c:crossAx

- Type: `c:CT_UnsignedInt`
- Attributes: `val`!
- Parents: `c:catAx`, `c:dateAx`, `c:serAx`, `c:valAx`
- Specification: §21.2.2.31 p.3391, OI 2.1.1446

### c:crossBetween

- Type: `c:CT_CrossBetween`
- Attributes: `val`!
- Parents: `c:valAx`
- Specification: §21.2.2.32 p.3391

### c:crosses

- Type: `c:CT_Crosses`
- Attributes: `val`!
- Parents: `c:catAx`, `c:dateAx`, `c:serAx`, `c:valAx`
- Specification: §21.2.2.33 p.3392

### c:crossesAt

- Type: `c:CT_Double`
- Attributes: `val`!
- Parents: `c:catAx`, `c:dateAx`, `c:serAx`, `c:valAx`
- Specification: §21.2.2.34 p.3392, OI 2.1.1447

### c:custSplit

- Type: `c:CT_CustSplit`
- Children: `c:secondPiePt`
- Parents: `c:ofPieChart`
- Specification: §21.2.2.35 p.3393

### c:custUnit

- Type: `c:CT_Double`
- Attributes: `val`!
- Parents: `c:dispUnits`
- Specification: §21.2.2.36 p.3393, OI 2.1.1448

### c:dLbl

- Type: `c:CT_DLbl`
- Children: `c:idx`, `c:delete`, `c:layout`, `c:tx`, `c:numFmt`, `c:spPr`, `c:txPr`, `c:dLblPos`, `c:showLegendKey`, `c:showVal`, `c:showCatName`, `c:showSerName`, `c:showPercent`, `c:showBubbleSize`, `c:separator`, `c:extLst`
- Parents: `c:dLbls`, `c:pivotFmt`
- Specification: §21.2.2.47 p.3396, OI 2.1.1455

### c:dLblPos

- Type: `c:CT_DLblPos`
- Attributes: `val`!
- Parents: `c15:dLbl`, `c16:dLbl`, `c:dLbl`, `c:dLbls`
- Specification: §21.2.2.48 p.3397, OI 2.1.1456

### c:dLbls

- Type: `c:CT_DLbls`
- Children: `c:dLbl`, `c:delete`, `c:numFmt`, `c:spPr`, `c:txPr`, `c:dLblPos`, `c:showLegendKey`, `c:showVal`, `c:showCatName`, `c:showSerName`, `c:showPercent`, `c:showBubbleSize`, `c:separator`, `c:showLeaderLines`, `c:leaderLines`, `c:extLst`
- Parents: `c15:ser`, `c:area3DChart`, `c:areaChart`, `c:bar3DChart`, `c:barChart`, `c:bubbleChart`, `c:doughnutChart`, `c:line3DChart`, `c:lineChart`, `c:ofPieChart`, `c:pie3DChart`, `c:pieChart`, `c:radarChart`, `c:scatterChart`, `c:ser`, `c:stockChart`
- Specification: §21.2.2.49 p.3397, OI 2.1.1457

### c:dPt

- Type: `c:CT_DPt`
- Children: `c:idx`, `c:invertIfNegative`, `c:marker`, `c:bubble3D`, `c:explosion`, `c:spPr`, `c:pictureOptions`, `c:extLst`
- Parents: `c15:ser`, `c:ser`
- Specification: §21.2.2.52 p.3397

### c:dTable

- Type: `c:CT_DTable`
- Children: `c:showHorzBorder`, `c:showVertBorder`, `c:showOutline`, `c:showKeys`, `c:spPr`, `c:txPr`, `c:extLst`
- Parents: `c:plotArea`
- Specification: §21.2.2.54 p.3398

### c:data

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c:protection`
- Specification: §21.2.2.37 p.3393, OI 2.1.1449

### c:date1904

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c:chartSpace`
- Specification: §21.2.2.38 p.3394

### c:dateAx

- Type: `c:CT_DateAx`
- Children: `c:axId`, `c:scaling`, `c:delete`, `c:axPos`, `c:majorGridlines`, `c:minorGridlines`, `c:title`, `c:numFmt`, `c:majorTickMark`, `c:minorTickMark`, `c:tickLblPos`, `c:spPr`, `c:txPr`, `c:crossAx`, `c:crosses`, `c:crossesAt`, `c:auto`, `c:lblOffset`, `c:baseTimeUnit`, `c:majorUnit`, `c:majorTimeUnit`, `c:minorUnit`, `c:minorTimeUnit`, `c:extLst`
- Parents: `c:plotArea`
- Specification: §21.2.2.39 p.3394, OI 2.1.1450

### c:delete

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c15:dLbl`, `c16:dLbl`, `c:catAx`, `c:dLbl`, `c:dLbls`, `c:dateAx`, `c:legendEntry`, `c:serAx`, `c:valAx`
- Specification: §21.2.2.40 p.3394

### c:depthPercent

- Type: `c:CT_DepthPercent`
- Attributes: `val`
- Parents: `c:view3D`
- Specification: §21.2.2.41 p.3395, OI 2.1.1451

### c:dispBlanksAs

- Type: `c:CT_DispBlanksAs`
- Attributes: `val`
- Parents: `c:chart`
- Specification: §21.2.2.42 p.3395, OI 2.1.1452

### c:dispEq

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c:trendline`
- Specification: §21.2.2.43 p.3395, OI 2.1.1453

### c:dispRSqr

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c:trendline`
- Specification: §21.2.2.44 p.3396, OI 2.1.1454

### c:dispUnits

- Type: `c:CT_DispUnits`
- Children: `c:custUnit`, `c:builtInUnit`, `c:dispUnitsLbl`, `c:extLst`
- Parents: `c:valAx`
- Specification: §21.2.2.45 p.3396

### c:dispUnitsLbl

- Type: `c:CT_DispUnitsLbl`
- Children: `c:layout`, `c:tx`, `c:spPr`, `c:txPr`
- Parents: `c:dispUnits`
- Specification: §21.2.2.46 p.3396

### c:doughnutChart

- Type: `c:CT_DoughnutChart`
- Children: `c:varyColors`, `c:ser`, `c:dLbls`, `c:firstSliceAng`, `c:holeSize`, `c:extLst`
- Parents: `c:plotArea`
- Specification: §21.2.2.50 p.3397, OI 2.1.1458

### c:downBars

- Type: `c:CT_UpDownBar`
- Children: `c:spPr`
- Parents: `c:upDownBars`
- Specification: §21.2.2.51 p.3397

### c:dropLines

- Type: `c:CT_ChartLines`
- Children: `c:spPr`
- Parents: `c:area3DChart`, `c:areaChart`, `c:line3DChart`, `c:lineChart`, `c:stockChart`
- Specification: §21.2.2.53 p.3397

### c:errBarType

- Type: `c:CT_ErrBarType`
- Attributes: `val`
- Parents: `c:errBars`
- Specification: §21.2.2.56 p.3398, OI 2.1.1460

### c:errBars

- Type: `c:CT_ErrBars`
- Children: `c:errDir`, `c:errBarType`, `c:errValType`, `c:noEndCap`, `c:plus`, `c:minus`, `c:val`, `c:spPr`, `c:extLst`
- Parents: `c15:ser`, `c:ser`
- Specification: §21.2.2.55 p.3398, OI 2.1.1459

### c:errDir

- Type: `c:CT_ErrDir`
- Attributes: `val`!
- Parents: `c:errBars`
- Specification: §21.2.2.57 p.3398, OI 2.1.1461

### c:errValType

- Type: `c:CT_ErrValType`
- Attributes: `val`
- Parents: `c:errBars`
- Specification: §21.2.2.58 p.3398, OI 2.1.1462

### c:evenFooter

- Type: `s:ST_Xstring` (text: `s:ST_Xstring`)
- Parents: `c:headerFooter`
- Specification: §21.2.2.59 p.3399, OI 2.1.1463

### c:evenHeader

- Type: `s:ST_Xstring` (text: `s:ST_Xstring`)
- Parents: `c:headerFooter`
- Specification: §21.2.2.60 p.3399, OI 2.1.1464

### c:explosion

- Type: `c:CT_UnsignedInt`
- Attributes: `val`!
- Parents: `c15:ser`, `c:dPt`, `c:ser`
- Specification: §21.2.2.61 p.3400, OI 2.1.1465

### c:ext

- Type: `c:CT_Extension`
- Attributes: `uri`
- Children: `any(##any)`
- Parents: `c:extLst`
- Specification: §21.2.2.62 p.3400

### c:extLst

- Type: `c:CT_ExtensionList`
- Children: `c:ext`
- Parents: `c15:dLbl`, `c15:dlblFieldTableCache`, `c15:dlblRangeCache`, `c15:layout`, `c15:marker`, `c15:pivotSource`, `c15:ser`, `c16:dLbl`, `c16:marker`, `c16:multiLvlStrCache`, `c16:numCache`, `c16:strCache`, `c16ac:multiLvlStrLit`, `c:area3DChart`, `c:areaChart`, `c:backWall`, `c:bar3DChart`, `c:barChart`, `c:bubbleChart`, `c:catAx`, `c:chart`, `c:chartSpace`, `c:dLbl`, `c:dLbls`, `c:dPt`, `c:dTable`, `c:dateAx`, `c:dispUnits`, `c:doughnutChart`, `c:errBars`, `c:floor`, `c:layout`, `c:legend`, `c:legendEntry`, `c:line3DChart`, `c:lineChart`, `c:manualLayout`, `c:marker`, `c:multiLvlStrCache`, `c:multiLvlStrRef`, `c:numCache`, `c:numLit`, `c:numRef`, `c:ofPieChart`, `c:pie3DChart`, `c:pieChart`, `c:pivotFmt`, `c:pivotSource`, `c:plotArea`, `c:radarChart`, `c:scaling`, `c:scatterChart`, `c:ser`, `c:serAx`, `c:sideWall`, `c:stockChart`, `c:strCache`, `c:strLit`, `c:strRef`, `c:surface3DChart`, `c:surfaceChart`, `c:title`, `c:trendline`, `c:trendlineLbl`, `c:upDownBars`, `c:valAx`, `c:view3D`
- Specification: §21.2.2.64 p.3401

### c:externalData

- Type: `c:CT_ExternalData`
- Attributes: `r:id`!
- Children: `c:autoUpdate`
- Parents: `c:chartSpace`
- Specification: §21.2.2.63 p.3401, OI 2.1.1466

### c:f

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `c:multiLvlStrRef`, `c:numRef`, `c:strRef`
- Specification: §21.2.2.65 p.3401, OI 2.1.1467

### c:firstFooter

- Type: `s:ST_Xstring` (text: `s:ST_Xstring`)
- Parents: `c:headerFooter`
- Specification: §21.2.2.66 p.3402, OI 2.1.1468

### c:firstHeader

- Type: `s:ST_Xstring` (text: `s:ST_Xstring`)
- Parents: `c:headerFooter`
- Specification: §21.2.2.67 p.3402, OI 2.1.1469

### c:firstSliceAng

- Type: `c:CT_FirstSliceAng`
- Attributes: `val`
- Parents: `c:doughnutChart`, `c:pieChart`
- Specification: §21.2.2.68 p.3403

### c:floor

- Type: `c:CT_Surface`
- Children: `c:thickness`, `c:spPr`, `c:pictureOptions`, `c:extLst`
- Parents: `c:chart`
- Specification: §21.2.2.69 p.3403, OI 2.1.1470

### c:fmtId

- Type: `c:CT_UnsignedInt`
- Attributes: `val`!
- Parents: `c15:pivotSource`, `c:pivotSource`
- Specification: §21.2.2.70 p.3403, OI 2.1.1471

### c:formatCode

- Type: `s:ST_Xstring` (text: `s:ST_Xstring`)
- Parents: `c16:numCache`, `c:numCache`, `c:numLit`
- Specification: §21.2.2.71 p.3404

### c:formatting

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c:protection`
- Specification: §21.2.2.72 p.3404, OI 2.1.1472

### c:forward

- Type: `c:CT_Double`
- Attributes: `val`!
- Parents: `c:trendline`
- Specification: §21.2.2.73 p.3404, OI 2.1.1473

### c:gapDepth

- Type: `c:CT_GapAmount`
- Attributes: `val`
- Parents: `c:area3DChart`, `c:bar3DChart`, `c:line3DChart`
- Specification: §21.2.2.74 p.3405, OI 2.1.1474

### c:gapWidth

- Type: `c:CT_GapAmount`
- Attributes: `val`
- Parents: `c:bar3DChart`, `c:barChart`, `c:ofPieChart`, `c:upDownBars`
- Specification: §21.2.2.75 p.3405, OI 2.1.1475

### c:grouping

- Type: `c:CT_Grouping`, `c:CT_BarGrouping`
- Attributes: `val`
- Parents: `c:area3DChart`, `c:areaChart`, `c:bar3DChart`, `c:barChart`, `c:line3DChart`, `c:lineChart`
- Specification: §21.2.2.76 p.3405, OI 2.1.1476; §21.2.2.77 p.3406

### c:h

- Type: `c:CT_Double`
- Attributes: `val`!
- Parents: `c:manualLayout`
- Specification: §21.2.2.78 p.3406, OI 2.1.1477

### c:hMode

- Type: `c:CT_LayoutMode`
- Attributes: `val`
- Parents: `c:manualLayout`
- Specification: §21.2.2.81 p.3407, OI 2.1.1479

### c:hPercent

- Type: `c:CT_HPercent`
- Attributes: `val`
- Parents: `c:view3D`
- Specification: §21.2.2.83 p.3408, OI 2.1.1481

### c:headerFooter

- Type: `c:CT_HeaderFooter`
- Attributes: `alignWithMargins`, `differentFirst`, `differentOddEven`
- Children: `c:oddHeader`, `c:oddFooter`, `c:evenHeader`, `c:evenFooter`, `c:firstHeader`, `c:firstFooter`
- Parents: `c:printSettings`
- Specification: §21.2.2.79 p.3406, OI 2.1.1478

### c:hiLowLines

- Type: `c:CT_ChartLines`
- Children: `c:spPr`
- Parents: `c:lineChart`, `c:stockChart`
- Specification: §21.2.2.80 p.3407

### c:holeSize

- Type: `c:CT_HoleSize`
- Attributes: `val`
- Parents: `c:doughnutChart`
- Specification: §21.2.2.82 p.3408, OI 2.1.1480

### c:idx

- Type: `c:CT_UnsignedInt`
- Attributes: `val`!
- Parents: `c15:dLbl`, `c15:ser`, `c16:dLbl`, `c:bandFmt`, `c:dLbl`, `c:dPt`, `c:legendEntry`, `c:pivotFmt`, `c:ser`
- Specification: §21.2.2.84 p.3408, OI 2.1.1482

### c:intercept

- Type: `c:CT_Double`
- Attributes: `val`!
- Parents: `c:trendline`
- Specification: §21.2.2.85 p.3409, OI 2.1.1483

### c:invertIfNegative

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c15:ser`, `c:dPt`, `c:ser`
- Specification: §21.2.2.86 p.3409, OI 2.1.1484

### c:lang

- Type: `c:CT_TextLanguageID`
- Attributes: `val`!
- Parents: `c:chartSpace`
- Specification: §21.2.2.87 p.3410

### c:layout

- Type: `c:CT_Layout`
- Children: `c:manualLayout`, `c:extLst`
- Parents: `c15:dLbl`, `c16:dLbl`, `c:dLbl`, `c:dispUnitsLbl`, `c:legend`, `c:plotArea`, `c:title`, `c:trendlineLbl`
- Specification: §21.2.2.88 p.3410, OI 2.1.1485

### c:layoutTarget

- Type: `c:CT_LayoutTarget`
- Attributes: `val`
- Parents: `c:manualLayout`
- Specification: §21.2.2.89 p.3410, OI 2.1.1486

### c:lblAlgn

- Type: `c:CT_LblAlgn`
- Attributes: `val`!
- Parents: `c:catAx`
- Specification: §21.2.2.90 p.3410

### c:lblOffset

- Type: `c:CT_LblOffset`
- Attributes: `val`
- Parents: `c:catAx`, `c:dateAx`
- Specification: §21.2.2.91 p.3411

### c:leaderLines

- Type: `c:CT_ChartLines`
- Children: `c:spPr`
- Parents: `c:dLbls`
- Specification: §21.2.2.92 p.3411, OI 2.1.1487

### c:legacyDrawingHF

- Type: `c:CT_RelId`
- Attributes: `r:id`!
- Parents: `c:printSettings`
- Specification: Part 4 §18.1.2.1 p.295

### c:legend

- Type: `c:CT_Legend`
- Children: `c:legendPos`, `c:legendEntry`, `c:layout`, `c:overlay`, `c:spPr`, `c:txPr`, `c:extLst`
- Parents: `c:chart`
- Specification: §21.2.2.93 p.3411

### c:legendEntry

- Type: `c:CT_LegendEntry`
- Children: `c:idx`, `c:delete`, `c:txPr`, `c:extLst`
- Parents: `c:legend`
- Specification: §21.2.2.94 p.3411

### c:legendPos

- Type: `c:CT_LegendPos`
- Attributes: `val`
- Parents: `c:legend`
- Specification: §21.2.2.95 p.3411, OI 2.1.1488

### c:line3DChart

- Type: `c:CT_Line3DChart`
- Children: `c:grouping`, `c:varyColors`, `c:ser`, `c:dLbls`, `c:dropLines`, `c:gapDepth`, `c:axId`, `c:extLst`
- Parents: `c:plotArea`
- Specification: §21.2.2.96 p.3412, OI 2.1.1489

### c:lineChart

- Type: `c:CT_LineChart`
- Children: `c:grouping`, `c:varyColors`, `c:ser`, `c:dLbls`, `c:dropLines`, `c:hiLowLines`, `c:upDownBars`, `c:marker`, `c:smooth`, `c:axId`, `c:extLst`
- Parents: `c:plotArea`
- Specification: §21.2.2.97 p.3412, OI 2.1.1490

### c:logBase

- Type: `c:CT_LogBase`
- Attributes: `val`!
- Parents: `c:scaling`
- Specification: §21.2.2.98 p.3412

### c:lvl

- Type: `c:CT_Lvl`
- Children: `c:pt`
- Parents: `c16:multiLvlStrCache`, `c16ac:multiLvlStrLit`, `c:multiLvlStrCache`
- Specification: §21.2.2.99 p.3412, OI 2.1.1491

### c:majorGridlines

- Type: `c:CT_ChartLines`
- Children: `c:spPr`
- Parents: `c:catAx`, `c:dateAx`, `c:serAx`, `c:valAx`
- Specification: §21.2.2.100 p.3412

### c:majorTickMark

- Type: `c:CT_TickMark`
- Attributes: `val`
- Parents: `c:catAx`, `c:dateAx`, `c:serAx`, `c:valAx`
- Specification: §21.2.2.101 p.3413, OI 2.1.1492

### c:majorTimeUnit

- Type: `c:CT_TimeUnit`
- Attributes: `val`
- Parents: `c:dateAx`
- Specification: §21.2.2.102 p.3413

### c:majorUnit

- Type: `c:CT_AxisUnit`
- Attributes: `val`!
- Parents: `c:dateAx`, `c:valAx`
- Specification: §21.2.2.103 p.3413, OI 2.1.1493

### c:manualLayout

- Type: `c:CT_ManualLayout`
- Children: `c:layoutTarget`, `c:xMode`, `c:yMode`, `c:wMode`, `c:hMode`, `c:x`, `c:y`, `c:w`, `c:h`, `c:extLst`
- Parents: `c15:layout`, `c:layout`
- Specification: §21.2.2.104 p.3413, OI 2.1.1494

### c:marker

- Type: `c:CT_Marker`, `c:CT_Boolean`
- Attributes: `val`
- Children: `c:symbol`, `c:size`, `c:spPr`, `c:extLst`
- Parents: `c15:ser`, `c:dPt`, `c:lineChart`, `c:pivotFmt`, `c:ser`
- Specification: §21.2.2.105 p.3414, OI 2.1.1495; §21.2.2.106 p.3414, OI 2.1.1496

### c:max

- Type: `c:CT_Double`
- Attributes: `val`!
- Parents: `c:scaling`
- Specification: §21.2.2.107 p.3414, OI 2.1.1497

### c:min

- Type: `c:CT_Double`
- Attributes: `val`!
- Parents: `c:scaling`
- Specification: §21.2.2.108 p.3415, OI 2.1.1498

### c:minorGridlines

- Type: `c:CT_ChartLines`
- Children: `c:spPr`
- Parents: `c:catAx`, `c:dateAx`, `c:serAx`, `c:valAx`
- Specification: §21.2.2.109 p.3415

### c:minorTickMark

- Type: `c:CT_TickMark`
- Attributes: `val`
- Parents: `c:catAx`, `c:dateAx`, `c:serAx`, `c:valAx`
- Specification: §21.2.2.110 p.3415

### c:minorTimeUnit

- Type: `c:CT_TimeUnit`
- Attributes: `val`
- Parents: `c:dateAx`
- Specification: §21.2.2.111 p.3415

### c:minorUnit

- Type: `c:CT_AxisUnit`
- Attributes: `val`!
- Parents: `c:dateAx`, `c:valAx`
- Specification: §21.2.2.112 p.3416, OI 2.1.1499

### c:minus

- Type: `c:CT_NumDataSource`
- Children: `c:numRef`, `c:numLit`
- Parents: `c:errBars`
- Specification: §21.2.2.113 p.3416, OI 2.1.1500

### c:multiLvlStrCache

- Type: `c:CT_MultiLvlStrData`
- Children: `c:ptCount`, `c:lvl`, `c:extLst`
- Parents: `c:multiLvlStrRef`
- Specification: §21.2.2.114 p.3416, OI 2.1.1501

### c:multiLvlStrRef

- Type: `c:CT_MultiLvlStrRef`
- Children: `c:f`, `c:multiLvlStrCache`, `c:extLst`
- Parents: `c15:cat`, `c:cat`, `c:xVal`
- Specification: §21.2.2.115 p.3416, OI 2.1.1502

### c:name

- Type: `s:ST_Xstring`, `xsd:string` (text: `s:ST_Xstring`)
- Parents: `c15:pivotSource`, `c:pivotSource`, `c:trendline`
- Specification: §21.2.2.116 p.3416; §21.2.2.117 p.3416, OI 2.1.1503

### c:noEndCap

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c:errBars`
- Specification: §21.2.2.118 p.3417

### c:noMultiLvlLbl

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c:catAx`
- Specification: §21.2.2.119 p.3417

### c:numCache

- Type: `c:CT_NumData`
- Children: `c:formatCode`, `c:ptCount`, `c:pt`, `c:extLst`
- Parents: `c:numRef`
- Specification: §21.2.2.120 p.3418

### c:numFmt

- Type: `c:CT_NumFmt`
- Attributes: `formatCode`!, `sourceLinked`
- Parents: `c15:dLbl`, `c16:dLbl`, `c:catAx`, `c:dLbl`, `c:dLbls`, `c:dateAx`, `c:serAx`, `c:trendlineLbl`, `c:valAx`
- Specification: §21.2.2.121 p.3418, OI 2.1.1504

### c:numLit

- Type: `c:CT_NumData`
- Children: `c:formatCode`, `c:ptCount`, `c:pt`, `c:extLst`
- Parents: `c15:cat`, `c:bubbleSize`, `c:cat`, `c:minus`, `c:plus`, `c:val`, `c:xVal`, `c:yVal`
- Specification: §21.2.2.122 p.3418

### c:numRef

- Type: `c:CT_NumRef`
- Children: `c:f`, `c:numCache`, `c:extLst`
- Parents: `c15:cat`, `c:bubbleSize`, `c:cat`, `c:minus`, `c:plus`, `c:val`, `c:xVal`, `c:yVal`
- Specification: §21.2.2.123 p.3418

### c:oddFooter

- Type: `s:ST_Xstring` (text: `s:ST_Xstring`)
- Parents: `c:headerFooter`
- Specification: §21.2.2.124 p.3418, OI 2.1.1505

### c:oddHeader

- Type: `s:ST_Xstring` (text: `s:ST_Xstring`)
- Parents: `c:headerFooter`
- Specification: §21.2.2.125 p.3419, OI 2.1.1506

### c:ofPieChart

- Type: `c:CT_OfPieChart`
- Children: `c:ofPieType`, `c:varyColors`, `c:ser`, `c:dLbls`, `c:gapWidth`, `c:splitType`, `c:splitPos`, `c:custSplit`, `c:secondPieSize`, `c:serLines`, `c:extLst`
- Parents: `c:plotArea`
- Specification: §21.2.2.126 p.3419, OI 2.1.1507

### c:ofPieType

- Type: `c:CT_OfPieType`
- Attributes: `val`
- Parents: `c:ofPieChart`
- Specification: §21.2.2.127 p.3420, OI 2.1.1508

### c:order

- Type: `c:CT_Order`, `c:CT_UnsignedInt`
- Attributes: `val`
- Parents: `c15:ser`, `c:ser`, `c:trendline`
- Specification: §21.2.2.128 p.3420, OI 2.1.1509; §21.2.2.129 p.3420, OI 2.1.1510

### c:orientation

- Type: `c:CT_Orientation`
- Attributes: `val`
- Parents: `c:scaling`
- Specification: §21.2.2.130 p.3420, OI 2.1.1511

### c:overlap

- Type: `c:CT_Overlap`
- Attributes: `val`
- Parents: `c:barChart`
- Specification: §21.2.2.131 p.3421, OI 2.1.1512

### c:overlay

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c:legend`, `c:title`
- Specification: §21.2.2.132 p.3421

### c:pageMargins

- Type: `c:CT_PageMargins`
- Attributes: `b`!, `footer`!, `header`!, `l`!, `r`!, `t`!
- Parents: `c:printSettings`
- Specification: §21.2.2.133 p.3422, OI 2.1.1513

### c:pageSetup

- Type: `c:CT_PageSetup`
- Attributes: `blackAndWhite`, `copies`, `draft`, `firstPageNumber`, `horizontalDpi`, `orientation`, `paperHeight`, `paperSize`, `paperWidth`, `useFirstPageNumber`, `verticalDpi`
- Parents: `c:printSettings`
- Specification: §21.2.2.134 p.3422, OI 2.1.1514

### c:period

- Type: `c:CT_Period`
- Attributes: `val`
- Parents: `c:trendline`
- Specification: §21.2.2.135 p.3426, OI 2.1.1515

### c:perspective

- Type: `c:CT_Perspective`
- Attributes: `val`
- Parents: `c:view3D`
- Specification: §21.2.2.136 p.3426, OI 2.1.1516

### c:pictureFormat

- Type: `c:CT_PictureFormat`
- Attributes: `val`!
- Parents: `c:pictureOptions`
- Specification: §21.2.2.137 p.3427

### c:pictureOptions

- Type: `c:CT_PictureOptions`
- Children: `c:applyToFront`, `c:applyToSides`, `c:applyToEnd`, `c:pictureFormat`, `c:pictureStackUnit`
- Parents: `c15:ser`, `c:backWall`, `c:dPt`, `c:floor`, `c:ser`, `c:sideWall`
- Specification: §21.2.2.138 p.3427, OI 2.1.1517

### c:pictureStackUnit

- Type: `c:CT_PictureStackUnit`
- Attributes: `val`!
- Parents: `c:pictureOptions`
- Specification: §21.2.2.139 p.3427

### c:pie3DChart

- Type: `c:CT_Pie3DChart`
- Children: `c:varyColors`, `c:ser`, `c:dLbls`, `c:extLst`
- Parents: `c:plotArea`
- Specification: §21.2.2.140 p.3428, OI 2.1.1518

### c:pieChart

- Type: `c:CT_PieChart`
- Children: `c:varyColors`, `c:ser`, `c:dLbls`, `c:firstSliceAng`, `c:extLst`
- Parents: `c:plotArea`
- Specification: §21.2.2.141 p.3428, OI 2.1.1519

### c:pivotFmt

- Type: `c:CT_PivotFmt`
- Children: `c:idx`, `c:spPr`, `c:txPr`, `c:marker`, `c:dLbl`, `c:extLst`
- Parents: `c:pivotFmts`
- Specification: §21.2.2.142 p.3428, OI 2.1.1520

### c:pivotFmts

- Type: `c:CT_PivotFmts`
- Children: `c:pivotFmt`
- Parents: `c:chart`
- Specification: §21.2.2.143 p.3428, OI 2.1.1521

### c:pivotSource

- Type: `c:CT_PivotSource`
- Children: `c:name`, `c:fmtId`, `c:extLst`
- Parents: `c:chartSpace`
- Specification: §21.2.2.144 p.3428, OI 2.1.1522

### c:plotArea

- Type: `c:CT_PlotArea`
- Children: `c:layout`, `c:areaChart`, `c:area3DChart`, `c:lineChart`, `c:line3DChart`, `c:stockChart`, `c:radarChart`, `c:scatterChart`, `c:pieChart`, `c:pie3DChart`, `c:doughnutChart`, `c:barChart`, `c:bar3DChart`, `c:ofPieChart`, `c:surfaceChart`, `c:surface3DChart`, `c:bubbleChart`, `c:valAx`, `c:catAx`, `c:dateAx`, `c:serAx`, `c:dTable`, `c:spPr`, `c:extLst`
- Parents: `c:chart`
- Specification: §21.2.2.145 p.3428, OI 2.1.1523

### c:plotVisOnly

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c:chart`
- Specification: §21.2.2.146 p.3428

### c:plus

- Type: `c:CT_NumDataSource`
- Children: `c:numRef`, `c:numLit`
- Parents: `c:errBars`
- Specification: §21.2.2.147 p.3429, OI 2.1.1524

### c:printSettings

- Type: `c:CT_PrintSettings`
- Children: `c:headerFooter`, `c:pageMargins`, `c:pageSetup`, `c:legacyDrawingHF`
- Parents: `c:chartSpace`
- Specification: §21.2.2.148 p.3429, OI 2.1.1525

### c:protection

- Type: `c:CT_Protection`
- Children: `c:chartObject`, `c:data`, `c:formatting`, `c:selection`, `c:userInterface`
- Parents: `c:chartSpace`
- Specification: §21.2.2.149 p.3429, OI 2.1.1526

### c:pt

- Type: `c:CT_NumVal`, `c:CT_StrVal`
- Attributes: `formatCode`, `idx`!
- Children: `c:v`
- Parents: `c15:dlblFieldTableCache`, `c15:dlblRangeCache`, `c16:numCache`, `c16:strCache`, `c:lvl`, `c:numCache`, `c:numLit`, `c:strCache`, `c:strLit`
- Specification: §21.2.2.150 p.3429, OI 2.1.1527; §21.2.2.151 p.3430, OI 2.1.1528

### c:ptCount

- Type: `c:CT_UnsignedInt`
- Attributes: `val`!
- Parents: `c15:dlblFieldTableCache`, `c15:dlblRangeCache`, `c16:multiLvlStrCache`, `c16:numCache`, `c16:strCache`, `c16ac:multiLvlStrLit`, `c:multiLvlStrCache`, `c:numCache`, `c:numLit`, `c:strCache`, `c:strLit`
- Specification: §21.2.2.152 p.3430, OI 2.1.1529

### c:rAngAx

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c:view3D`
- Specification: §21.2.2.155 p.3431, OI 2.1.1532

### c:radarChart

- Type: `c:CT_RadarChart`
- Children: `c:radarStyle`, `c:varyColors`, `c:ser`, `c:dLbls`, `c:axId`, `c:extLst`
- Parents: `c:plotArea`
- Specification: §21.2.2.153 p.3430, OI 2.1.1530

### c:radarStyle

- Type: `c:CT_RadarStyle`
- Attributes: `val`
- Parents: `c:radarChart`
- Specification: §21.2.2.154 p.3430, OI 2.1.1531

### c:rich

- Type: `a:CT_TextBody`
- Children: `a:bodyPr`, `a:lstStyle`, `a:p`
- Parents: `c15:tx`, `c:tx`
- Specification: §21.2.2.156 p.3431, OI 2.1.1533

### c:rotX

- Type: `c:CT_RotX`
- Attributes: `val`
- Parents: `c:view3D`
- Specification: §21.2.2.157 p.3431

### c:rotY

- Type: `c:CT_RotY`
- Attributes: `val`
- Parents: `c:view3D`
- Specification: §21.2.2.158 p.3432

### c:roundedCorners

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c:chartSpace`
- Specification: §21.2.2.159 p.3432

### c:scaling

- Type: `c:CT_Scaling`
- Children: `c:logBase`, `c:orientation`, `c:max`, `c:min`, `c:extLst`
- Parents: `c:catAx`, `c:dateAx`, `c:serAx`, `c:valAx`
- Specification: §21.2.2.160 p.3432

### c:scatterChart

- Type: `c:CT_ScatterChart`
- Children: `c:scatterStyle`, `c:varyColors`, `c:ser`, `c:dLbls`, `c:axId`, `c:extLst`
- Parents: `c:plotArea`
- Specification: §21.2.2.161 p.3433, OI 2.1.1534

### c:scatterStyle

- Type: `c:CT_ScatterStyle`
- Attributes: `val`
- Parents: `c:scatterChart`
- Specification: §21.2.2.162 p.3433

### c:secondPiePt

- Type: `c:CT_UnsignedInt`
- Attributes: `val`!
- Parents: `c:custSplit`
- Specification: §21.2.2.163 p.3433, OI 2.1.1535

### c:secondPieSize

- Type: `c:CT_SecondPieSize`
- Attributes: `val`
- Parents: `c:ofPieChart`
- Specification: §21.2.2.164 p.3433

### c:selection

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c:protection`
- Specification: §21.2.2.165 p.3434

### c:separator

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `c15:dLbl`, `c16:dLbl`, `c:dLbl`, `c:dLbls`
- Specification: §21.2.2.166 p.3434, OI 2.1.1536

### c:ser

- Type: `c:CT_LineSer`, `c:CT_ScatterSer`, `c:CT_RadarSer`, `c:CT_BarSer`, `c:CT_AreaSer`, `c:CT_PieSer`, `c:CT_BubbleSer`, `c:CT_SurfaceSer`
- Children: `c:idx`, `c:order`, `c:tx`, `c:spPr`, `c:marker`, `c:dPt`, `c:dLbls`, `c:trendline`, `c:errBars`, `c:cat`, `c:val`, `c:smooth`, `c:extLst`, `c:xVal`, `c:yVal`, `c:invertIfNegative`, `c:pictureOptions`, `c:shape`, `c:explosion`, `c:bubbleSize`, `c:bubble3D`
- Parents: `c:area3DChart`, `c:areaChart`, `c:bar3DChart`, `c:barChart`, `c:bubbleChart`, `c:doughnutChart`, `c:line3DChart`, `c:lineChart`, `c:ofPieChart`, `c:pie3DChart`, `c:pieChart`, `c:radarChart`, `c:scatterChart`, `c:stockChart`, `c:surface3DChart`, `c:surfaceChart`
- Specification: §21.2.2.167 p.3434, OI 2.1.1537; §21.2.2.168 p.3434, OI 2.1.1538; §21.2.2.169 p.3434, OI 2.1.1539; §21.2.2.170 p.3435, OI 2.1.1540; §21.2.2.171 p.3435, OI 2.1.1541; §21.2.2.172 p.3435, OI 2.1.1542; §21.2.2.173 p.3435; §21.2.2.174 p.3435, OI 2.1.1543

### c:serAx

- Type: `c:CT_SerAx`
- Children: `c:axId`, `c:scaling`, `c:delete`, `c:axPos`, `c:majorGridlines`, `c:minorGridlines`, `c:title`, `c:numFmt`, `c:majorTickMark`, `c:minorTickMark`, `c:tickLblPos`, `c:spPr`, `c:txPr`, `c:crossAx`, `c:crosses`, `c:crossesAt`, `c:tickLblSkip`, `c:tickMarkSkip`, `c:extLst`
- Parents: `c:plotArea`
- Specification: §21.2.2.175 p.3435, OI 2.1.1544

### c:serLines

- Type: `c:CT_ChartLines`
- Children: `c:spPr`
- Parents: `c:barChart`, `c:ofPieChart`
- Specification: §21.2.2.176 p.3435, OI 2.1.1545

### c:shape

- Type: `c:CT_Shape`
- Attributes: `val`
- Parents: `c15:ser`, `c:bar3DChart`, `c:ser`
- Specification: §21.2.2.177 p.3436, OI 2.1.1546

### c:showBubbleSize

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c15:dLbl`, `c16:dLbl`, `c:dLbl`, `c:dLbls`
- Specification: §21.2.2.178 p.3436, OI 2.1.1547

### c:showCatName

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c15:dLbl`, `c16:dLbl`, `c:dLbl`, `c:dLbls`
- Specification: §21.2.2.179 p.3436

### c:showDLblsOverMax

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c:chart`
- Specification: §21.2.2.180 p.3437, OI 2.1.1548

### c:showHorzBorder

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c:dTable`
- Specification: §21.2.2.181 p.3437

### c:showKeys

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c:dTable`
- Specification: §21.2.2.182 p.3438

### c:showLeaderLines

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c:dLbls`
- Specification: §21.2.2.183 p.3438, OI 2.1.1549

### c:showLegendKey

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c15:dLbl`, `c16:dLbl`, `c:dLbl`, `c:dLbls`
- Specification: §21.2.2.184 p.3438, OI 2.1.1550

### c:showNegBubbles

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c:bubbleChart`
- Specification: §21.2.2.185 p.3439

### c:showOutline

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c:dTable`
- Specification: §21.2.2.186 p.3439

### c:showPercent

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c15:dLbl`, `c16:dLbl`, `c:dLbl`, `c:dLbls`
- Specification: §21.2.2.187 p.3440, OI 2.1.1551

### c:showSerName

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c15:dLbl`, `c16:dLbl`, `c:dLbl`, `c:dLbls`
- Specification: §21.2.2.188 p.3440

### c:showVal

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c15:dLbl`, `c16:dLbl`, `c:dLbl`, `c:dLbls`
- Specification: §21.2.2.189 p.3441

### c:showVertBorder

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c:dTable`
- Specification: §21.2.2.190 p.3441

### c:sideWall

- Type: `c:CT_Surface`
- Children: `c:thickness`, `c:spPr`, `c:pictureOptions`, `c:extLst`
- Parents: `c:chart`
- Specification: §21.2.2.191 p.3441, OI 2.1.1552

### c:size

- Type: `c:CT_MarkerSize`
- Attributes: `val`
- Parents: `c15:marker`, `c16:marker`, `c:marker`
- Specification: §21.2.2.192 p.3442

### c:sizeRepresents

- Type: `c:CT_SizeRepresents`
- Attributes: `val`
- Parents: `c:bubbleChart`
- Specification: §21.2.2.193 p.3442

### c:smooth

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c15:ser`, `c:lineChart`, `c:ser`
- Specification: §21.2.2.194 p.3442

### c:spPr

- Type: `a:CT_ShapeProperties`
- Attributes: `bwMode`
- Children: `a:xfrm`, `a:custGeom`, `a:prstGeom`, `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`, `a:ln`, `a:effectLst`, `a:effectDag`, `a:scene3d`, `a:sp3d`, `a:extLst`
- Parents: `c15:dLbl`, `c15:leaderLines`, `c15:marker`, `c15:ser`, `c16:dLbl`, `c16:marker`, `c:backWall`, `c:bandFmt`, `c:catAx`, `c:chartSpace`, `c:dLbl`, `c:dLbls`, `c:dPt`, `c:dTable`, `c:dateAx`, `c:dispUnitsLbl`, `c:downBars`, `c:dropLines`, `c:errBars`, `c:floor`, `c:hiLowLines`, `c:leaderLines`, `c:legend`, `c:majorGridlines`, `c:marker`, `c:minorGridlines`, `c:pivotFmt`, `c:plotArea`, `c:ser`, `c:serAx`, `c:serLines`, `c:sideWall`, `c:title`, `c:trendline`, `c:trendlineLbl`, `c:upBars`, `c:valAx`
- Specification: §21.2.2.197 p.3443, OI 2.1.1555

### c:splitPos

- Type: `c:CT_Double`
- Attributes: `val`!
- Parents: `c:ofPieChart`
- Specification: §21.2.2.195 p.3443, OI 2.1.1553

### c:splitType

- Type: `c:CT_SplitType`
- Attributes: `val`
- Parents: `c:ofPieChart`
- Specification: §21.2.2.196 p.3443, OI 2.1.1554

### c:stockChart

- Type: `c:CT_StockChart`
- Children: `c:ser`, `c:dLbls`, `c:dropLines`, `c:hiLowLines`, `c:upDownBars`, `c:axId`, `c:extLst`
- Parents: `c:plotArea`
- Specification: §21.2.2.198 p.3444

### c:strCache

- Type: `c:CT_StrData`
- Children: `c:ptCount`, `c:pt`, `c:extLst`
- Parents: `c:strRef`
- Specification: §21.2.2.199 p.3444

### c:strLit

- Type: `c:CT_StrData`
- Children: `c:ptCount`, `c:pt`, `c:extLst`
- Parents: `c15:cat`, `c:cat`, `c:xVal`
- Specification: §21.2.2.200 p.3444

### c:strRef

- Type: `c:CT_StrRef`
- Children: `c:f`, `c:strCache`, `c:extLst`
- Parents: `c15:cat`, `c15:tx`, `c:cat`, `c:tx`, `c:xVal`
- Specification: §21.2.2.201 p.3444

### c:style

- Type: `c:CT_Style`
- Attributes: `val`!
- Parents: `c:chartSpace`
- Specification: §21.2.2.202 p.3444

### c:surface3DChart

- Type: `c:CT_Surface3DChart`
- Children: `c:wireframe`, `c:ser`, `c:bandFmts`, `c:axId`, `c:extLst`
- Parents: `c:plotArea`
- Specification: §21.2.2.203 p.3445, OI 2.1.1556

### c:surfaceChart

- Type: `c:CT_SurfaceChart`
- Children: `c:wireframe`, `c:ser`, `c:bandFmts`, `c:axId`, `c:extLst`
- Parents: `c:plotArea`
- Specification: §21.2.2.204 p.3445, OI 2.1.1557

### c:symbol

- Type: `c:CT_MarkerStyle`
- Attributes: `val`!
- Parents: `c15:marker`, `c16:marker`, `c:marker`
- Specification: §21.2.2.205 p.3445

### c:thickness

- Type: `c:CT_Thickness`
- Attributes: `val`!
- Parents: `c:backWall`, `c:floor`, `c:sideWall`
- Specification: §21.2.2.206 p.3445, OI 2.1.1558

### c:tickLblPos

- Type: `c:CT_TickLblPos`
- Attributes: `val`
- Parents: `c:catAx`, `c:dateAx`, `c:serAx`, `c:valAx`
- Specification: §21.2.2.207 p.3446

### c:tickLblSkip

- Type: `c:CT_Skip`
- Attributes: `val`!
- Parents: `c:catAx`, `c:serAx`
- Specification: §21.2.2.208 p.3446, OI 2.1.1559

### c:tickMarkSkip

- Type: `c:CT_Skip`
- Attributes: `val`!
- Parents: `c:catAx`, `c:serAx`
- Specification: §21.2.2.209 p.3446, OI 2.1.1560

### c:title

- Type: `c:CT_Title`
- Children: `c:tx`, `c:layout`, `c:overlay`, `c:spPr`, `c:txPr`, `c:extLst`
- Parents: `c:catAx`, `c:chart`, `c:dateAx`, `c:serAx`, `c:valAx`
- Specification: §21.2.2.210 p.3446, OI 2.1.1561

### c:trendline

- Type: `c:CT_Trendline`
- Children: `c:name`, `c:spPr`, `c:trendlineType`, `c:order`, `c:period`, `c:forward`, `c:backward`, `c:intercept`, `c:dispRSqr`, `c:dispEq`, `c:trendlineLbl`, `c:extLst`
- Parents: `c15:ser`, `c:ser`
- Specification: §21.2.2.211 p.3446, OI 2.1.1562

### c:trendlineLbl

- Type: `c:CT_TrendlineLbl`
- Children: `c:layout`, `c:tx`, `c:numFmt`, `c:spPr`, `c:txPr`, `c:extLst`
- Parents: `c:trendline`
- Specification: §21.2.2.212 p.3447, OI 2.1.1563

### c:trendlineType

- Type: `c:CT_TrendlineType`
- Attributes: `val`
- Parents: `c:trendline`
- Specification: §21.2.2.213 p.3447, OI 2.1.1564

### c:tx

- Type: `c:CT_Tx`, `c:CT_SerTx`
- Children: `c:strRef`, `c:rich`, `c:v`
- Parents: `c15:dLbl`, `c15:ser`, `c16:dLbl`, `c:dLbl`, `c:dispUnitsLbl`, `c:ser`, `c:title`, `c:trendlineLbl`
- Specification: §21.2.2.214 p.3447; §21.2.2.215 p.3447, OI 2.1.1565

### c:txPr

- Type: `a:CT_TextBody`
- Children: `a:bodyPr`, `a:lstStyle`, `a:p`
- Parents: `c15:dLbl`, `c16:dLbl`, `c:catAx`, `c:chartSpace`, `c:dLbl`, `c:dLbls`, `c:dTable`, `c:dateAx`, `c:dispUnitsLbl`, `c:legend`, `c:legendEntry`, `c:pivotFmt`, `c:serAx`, `c:title`, `c:trendlineLbl`, `c:valAx`
- Specification: §21.2.2.216 p.3447, OI 2.1.1566

### c:upBars

- Type: `c:CT_UpDownBar`
- Children: `c:spPr`
- Parents: `c:upDownBars`
- Specification: §21.2.2.217 p.3447

### c:upDownBars

- Type: `c:CT_UpDownBars`
- Children: `c:gapWidth`, `c:upBars`, `c:downBars`, `c:extLst`
- Parents: `c:lineChart`, `c:stockChart`
- Specification: §21.2.2.218 p.3448

### c:userInterface

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c:protection`
- Specification: §21.2.2.219 p.3448, OI 2.1.1567

### c:userShapes

- Type: `cdr:CT_Drawing`, `c:CT_RelId`
- Attributes: `r:id`!
- Children: `cdr:relSizeAnchor`, `cdr:absSizeAnchor`
- Parents: `c:chartSpace`
- Specification: §21.2.2.220 p.3448; §21.2.2.221 p.3448

### c:v

- Type: `s:ST_Xstring` (text: `s:ST_Xstring`)
- Parents: `c15:tx`, `c:pt`, `c:tx`
- Specification: §21.2.2.222 p.3449, OI 2.1.1568; §21.2.2.223 p.3449

### c:val

- Type: `c:CT_Double`, `c:CT_NumDataSource`
- Attributes: `val`!
- Children: `c:numRef`, `c:numLit`
- Parents: `c15:ser`, `c:errBars`, `c:ser`
- Specification: §21.2.2.224 p.3449; §21.2.2.225 p.3449, OI 2.1.1569

### c:valAx

- Type: `c:CT_ValAx`
- Children: `c:axId`, `c:scaling`, `c:delete`, `c:axPos`, `c:majorGridlines`, `c:minorGridlines`, `c:title`, `c:numFmt`, `c:majorTickMark`, `c:minorTickMark`, `c:tickLblPos`, `c:spPr`, `c:txPr`, `c:crossAx`, `c:crosses`, `c:crossesAt`, `c:crossBetween`, `c:majorUnit`, `c:minorUnit`, `c:dispUnits`, `c:extLst`
- Parents: `c:plotArea`
- Specification: §21.2.2.226 p.3449, OI 2.1.1570

### c:varyColors

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c:area3DChart`, `c:areaChart`, `c:bar3DChart`, `c:barChart`, `c:bubbleChart`, `c:doughnutChart`, `c:line3DChart`, `c:lineChart`, `c:ofPieChart`, `c:pie3DChart`, `c:pieChart`, `c:radarChart`, `c:scatterChart`
- Specification: §21.2.2.227 p.3450, OI 2.1.1571

### c:view3D

- Type: `c:CT_View3D`
- Children: `c:rotX`, `c:hPercent`, `c:rotY`, `c:depthPercent`, `c:rAngAx`, `c:perspective`, `c:extLst`
- Parents: `c:chart`
- Specification: §21.2.2.228 p.3450, OI 2.1.1572

### c:w

- Type: `c:CT_Double`
- Attributes: `val`!
- Parents: `c:manualLayout`
- Specification: §21.2.2.229 p.3450, OI 2.1.1573

### c:wMode

- Type: `c:CT_LayoutMode`
- Attributes: `val`
- Parents: `c:manualLayout`
- Specification: §21.2.2.231 p.3451, OI 2.1.1574

### c:wireframe

- Type: `c:CT_Boolean`
- Attributes: `val`
- Parents: `c:surface3DChart`, `c:surfaceChart`
- Specification: §21.2.2.230 p.3451

### c:x

- Type: `c:CT_Double`
- Attributes: `val`!
- Parents: `c:manualLayout`
- Specification: §21.2.2.232 p.3451, OI 2.1.1575

### c:xMode

- Type: `c:CT_LayoutMode`
- Attributes: `val`
- Parents: `c:manualLayout`
- Specification: §21.2.2.233 p.3452, OI 2.1.1576

### c:xVal

- Type: `c:CT_AxDataSource`
- Children: `c:multiLvlStrRef`, `c:numRef`, `c:numLit`, `c:strRef`, `c:strLit`
- Parents: `c15:ser`, `c:ser`
- Specification: §21.2.2.234 p.3452

### c:y

- Type: `c:CT_Double`
- Attributes: `val`!
- Parents: `c:manualLayout`
- Specification: §21.2.2.235 p.3452, OI 2.1.1577

### c:yMode

- Type: `c:CT_LayoutMode`
- Attributes: `val`
- Parents: `c:manualLayout`
- Specification: §21.2.2.236 p.3452, OI 2.1.1578

### c:yVal

- Type: `c:CT_NumDataSource`
- Children: `c:numRef`, `c:numLit`
- Parents: `c15:ser`, `c:ser`
- Specification: §21.2.2.237 p.3453

`!` marks required attributes.
