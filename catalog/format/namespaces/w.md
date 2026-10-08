# w: WordprocessingML (DOCX)

`http://schemas.openxmlformats.org/wordprocessingml/2006/main`

- Source: ECMA-376-4 transitional schema wml.xsd
- Office version: 2007
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 613 | 1,016 | 0 | 285 | 110 | 733 | 871 | 480 |

Full detail (content models, attribute types, defaults): [w.json](w.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`w:abstractNum`](#wabstractnum) | `w:CT_AbstractNum` | 1 | 7 | 1 | §17.9.1 p.703, OI 2.1.275 |
| [`w:abstractNumId`](#wabstractnumid) | `w:CT_DecimalNumber` | 1 | 0 | 1 | §17.9.2 p.704, OI 2.1.276 |
| [`w:active`](#wactive) | `w:CT_OnOff` | 1 | 0 | 1 | §17.14.1 p.940 |
| [`w:activeRecord`](#wactiverecord) | `w:CT_DecimalNumber` | 1 | 0 | 1 | §17.14.2 p.941, OI 2.1.360 |
| [`w:activeWritingStyle`](#wactivewritingstyle) | `w:CT_WritingStyle` | 6 | 0 | 1 | §17.15.1.1 p.980, OI 2.1.383 |
| [`w:addressFieldName`](#waddressfieldname) | `w:CT_String` | 1 | 0 | 1 | §17.14.3 p.943 |
| [`w:adjustLineHeightInTable`](#wadjustlineheightintable) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.3.1 p.1159 |
| [`w:adjustRightInd`](#wadjustrightind) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.1.1 p.205 |
| [`w:alias`](#walias) | `w:CT_String` | 1 | 0 | 1 | §17.5.2.1 p.511 |
| [`w:aliases`](#waliases) | `w:CT_String` | 1 | 0 | 1 | §17.7.4.1 p.628, OI 2.1.231 |
| [`w:alignBordersAndEdges`](#walignbordersandedges) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.2 p.982 |
| [`w:alignTablesRowByRow`](#waligntablesrowbyrow) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.1 p.97 |
| [`w:allowPNG`](#wallowpng) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.2.1 p.1096, OI 2.1.428 |
| [`w:allowSpaceOfSameStyleInTable`](#wallowspaceofsamestyleintable) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.2 p.98 |
| [`w:altChunk`](#waltchunk) | `w:CT_AltChunk` | 1 | 1 | 11 | §17.17.2.1 p.1308, OI 2.1.527 |
| [`w:altChunkPr`](#waltchunkpr) | `w:CT_AltChunkPr` | 0 | 1 | 1 | §17.17.2.2 p.1309 |
| [`w:altName`](#waltname) | `w:CT_String` | 1 | 0 | 1 | §17.8.3.1 p.681, OI 2.1.262 |
| [`w:alwaysMergeEmptyNamespace`](#walwaysmergeemptynamespace) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.3 p.983, OI 2.1.384 |
| [`w:alwaysShowPlaceholderText`](#walwaysshowplaceholdertext) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.4 p.984 |
| [`w:annotationRef`](#wannotationref) | `w:CT_Empty` | 0 | 0 | 2 | §17.13.4.1 p.812 |
| [`w:applyBreakingRules`](#wapplybreakingrules) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.3.2 p.1160, OI 2.1.451 |
| [`w:attachedSchema`](#wattachedschema) | `w:CT_String` | 1 | 0 | 1 | §17.15.1.5 p.985 |
| [`w:attachedTemplate`](#wattachedtemplate) | `w:CT_Rel` | 1 | 0 | 1 | §17.15.1.6 p.986, OI 2.1.385 |
| [`w:attr`](#wattr) | `w:CT_Attr` | 3 | 0 | 2 | §17.5.1.1 p.497, OI 2.1.186; §17.5.1.2 p.498, OI 2.1.187 |
| [`w:autoCaption`](#wautocaption) | `w:CT_AutoCaption` | 2 | 0 | 1 | §17.15.1.7 p.987, OI 2.1.386 |
| [`w:autoCaptions`](#wautocaptions) | `w:CT_AutoCaptions` | 0 | 1 | 1 | §17.15.1.8 p.989, OI 2.1.387 |
| [`w:autoFormatOverride`](#wautoformatoverride) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.9 p.990, OI 2.1.388 |
| [`w:autoHyphenation`](#wautohyphenation) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.10 p.990 |
| [`w:autoRedefine`](#wautoredefine) | `w:CT_OnOff` | 1 | 0 | 1 | §17.7.4.2 p.629, OI 2.1.232 |
| [`w:autoSpaceDE`](#wautospacede) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.1.2 p.205 |
| [`w:autoSpaceDN`](#wautospacedn) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.1.3 p.206 |
| [`w:autoSpaceLikeWord95`](#wautospacelikeword95) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.4 p.102 |
| [`w:autofitToFirstFixedWidthCell`](#wautofittofirstfixedwidthcell) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.3 p.100 |
| [`w:b`](#wb) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.2.1 p.274, OI 2.1.68 |
| [`w:bCs`](#wbcs) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.2.2 p.274, OI 2.1.69 |
| [`w:background`](#wbackground) | `w:CT_Background` | 4 | 3 | 2 | §17.2.1 p.197, OI 2.1.35 |
| [`w:balanceSingleByteDoubleByteWidth`](#wbalancesinglebytedoublebytewidth) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.3.3 p.1161 |
| [`w:bar`](#wbar) | `w:CT_Border` | 9 | 0 | 1 | §17.3.1.4 p.206, OI 2.1.38 |
| [`w:basedOn`](#wbasedon) | `w:CT_String` | 1 | 0 | 1 | §17.7.4.3 p.631, OI 2.1.233 |
| [`w:bdo`](#wbdo) | `w:CT_BdoContentRun` | 1 | 34 | 12 | §17.3.2.3 p.275, OI 2.1.70 |
| [`w:bdr`](#wbdr) | `w:CT_Border` | 9 | 0 | 1 | §17.3.2.4 p.276, OI 2.1.71 |
| [`w:behavior`](#wbehavior) | `w:CT_DocPartBehavior` | 1 | 0 | 1 | §17.12.1 p.790 |
| [`w:behaviors`](#wbehaviors) | `w:CT_DocPartBehaviors` | 0 | 1 | 1 | §17.12.2 p.793 |
| [`w:between`](#wbetween) | `w:CT_Border` | 9 | 0 | 1 | §17.3.1.5 p.207, OI 2.1.39 |
| [`w:bibliography`](#wbibliography) | `w:CT_Empty` | 0 | 0 | 1 | §17.5.2.2 p.512 |
| [`w:bidi`](#wbidi) | `w:CT_OnOff` | 1 | 0 | 2 | §17.3.1.6 p.209; §17.6.1 p.557 |
| [`w:bidiVisual`](#wbidivisual) | `w:CT_OnOff` | 1 | 0 | 1 | §17.4.1 p.383, OI 2.1.115 |
| [`w:blockQuote`](#wblockquote) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.2.2 p.1097, OI 2.1.429 |
| [`w:body`](#wbody) | `w:CT_Body` | 0 | 31 | 1 | §17.2.2 p.202, OI 2.1.36 |
| [`w:bodyDiv`](#wbodydiv) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.2.3 p.1098, OI 2.1.430 |
| [`w:bookFoldPrinting`](#wbookfoldprinting) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.11 p.991, OI 2.1.389 |
| [`w:bookFoldPrintingSheets`](#wbookfoldprintingsheets) | `w:CT_DecimalNumber` | 1 | 0 | 1 | §17.15.1.12 p.993, OI 2.1.390 |
| [`w:bookFoldRevPrinting`](#wbookfoldrevprinting) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.13 p.995, OI 2.1.391 |
| [`w:bookmarkEnd`](#wbookmarkend) | `w:CT_MarkupRange` | 2 | 0 | 36 | §17.13.6.1 p.917, OI 2.1.355 |
| [`w:bookmarkStart`](#wbookmarkstart) | `w:CT_Bookmark` | 5 | 0 | 36 | §17.13.6.2 p.919, OI 2.1.356 |
| [`w:bordersDoNotSurroundFooter`](#wbordersdonotsurroundfooter) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.14 p.997 |
| [`w:bordersDoNotSurroundHeader`](#wbordersdonotsurroundheader) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.15 p.997 |
| [`w:bottom`](#wbottom) | `w:CT_Border`, `w:CT_BottomPageBorder`, `w:CT_TblWidth` | 14 | 0 | 7 | §17.3.1.7 p.209, OI 2.1.40; §17.4.2 p.384, OI 2.1.116; §17.4.3 p.385, OI 2.1.117; §17.4.4 p.385, OI 2.1.118; §17.4.5 p.386, OI 2.1.119; §17.6.2 p.558, OI 2.1.211; §17.15.2.4 p.1099, OI 2.1.431 |
| [`w:br`](#wbr) | `w:CT_Br` | 2 | 0 | 2 | §17.3.3.1 p.333 |
| [`w:cachedColBalance`](#wcachedcolbalance) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.5 p.103 |
| [`w:calcOnExit`](#wcalconexit) | `w:CT_OnOff` | 1 | 0 | 1 | §17.16.6 p.1267 |
| [`w:calendar`](#wcalendar) | `w:CT_CalendarType` | 1 | 0 | 1 | §17.5.2.3 p.512 |
| [`w:cantSplit`](#wcantsplit) | `w:CT_OnOff` | 1 | 0 | 1 | §17.4.6 p.387, OI 2.1.120 |
| [`w:caps`](#wcaps) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.2.5 p.276 |
| [`w:caption`](#wcaption) | `w:CT_Caption` | 7 | 0 | 1 | §17.15.1.16 p.998, OI 2.1.392 |
| [`w:captions`](#wcaptions) | `w:CT_Captions` | 0 | 2 | 1 | §17.15.1.17 p.1007, OI 2.1.393 |
| [`w:category`](#wcategory) | `w:CT_DocPartCategory` | 0 | 2 | 1 | §17.12.3 p.793 |
| [`w:cellDel`](#wcelldel) | `w:CT_TrackChange` | 3 | 0 | 1 | §17.13.5.1 p.823, OI 2.1.317 |
| [`w:cellIns`](#wcellins) | `w:CT_TrackChange` | 3 | 0 | 1 | §17.13.5.2 p.825, OI 2.1.318 |
| [`w:cellMerge`](#wcellmerge) | `w:CT_CellMergeTrackChange` | 5 | 0 | 1 | §17.13.5.3 p.828, OI 2.1.319 |
| [`w:characterSpacingControl`](#wcharacterspacingcontrol) | `w:CT_CharacterSpacing` | 1 | 0 | 1 | §17.15.1.18 p.1010 |
| [`w:charset`](#wcharset) | `w:CT_Charset` | 2 | 0 | 1 | §17.8.3.2 p.683, OI 2.1.263 |
| [`w:checkBox`](#wcheckbox) | `w:CT_FFCheckBox` | 0 | 4 | 1 | §17.16.7 p.1268 |
| [`w:checkErrors`](#wcheckerrors) | `w:CT_DecimalNumber` | 1 | 0 | 1 | §17.14.4 p.944, OI 2.1.361 |
| [`w:checked`](#wchecked) | `w:CT_OnOff` | 1 | 0 | 1 | §17.16.8 p.1268 |
| [`w:citation`](#wcitation) | `w:CT_Empty` | 0 | 0 | 1 | §17.5.2.4 p.513 |
| [`w:clickAndTypeStyle`](#wclickandtypestyle) | `w:CT_String` | 1 | 0 | 1 | §17.15.1.19 p.1010, OI 2.1.394 |
| [`w:clrSchemeMapping`](#wclrschememapping) | `w:CT_ColorSchemeMapping` | 12 | 0 | 1 | §17.15.1.20 p.1012, OI 2.1.395 |
| [`w:cnfStyle`](#wcnfstyle) | `w:CT_Cnf` | 13 | 0 | 3 | §17.3.1.8 p.211, OI 2.1.41; §17.4.7 p.389, OI 2.1.121; §17.4.8 p.395, OI 2.1.122 |
| [`w:col`](#wcol) | `w:CT_Column` | 2 | 0 | 1 | §17.6.3 p.567, OI 2.1.212 |
| [`w:colDelim`](#wcoldelim) | `w:CT_DecimalNumber` | 1 | 0 | 1 | §17.14.5 p.945, OI 2.1.362 |
| [`w:color`](#wcolor) | `w:CT_Color` | 4 | 0 | 2 | §17.3.2.6 p.277, OI 2.1.72; §17.15.2.5 p.1100, OI 2.1.432 |
| [`w:cols`](#wcols) | `w:CT_Columns` | 4 | 1 | 1 | §17.6.4 p.568, OI 2.1.213 |
| [`w:column`](#wcolumn) | `w:CT_DecimalNumber` | 1 | 0 | 2 | §17.14.6 p.946, OI 2.1.363; §17.14.7 p.948, OI 2.1.364 |
| [`w:comboBox`](#wcombobox) | `w:CT_SdtComboBox` | 1 | 1 | 1 | §17.5.2.5 p.513 |
| [`w:comment`](#wcomment) | `w:CT_Comment` | 4 | 30 | 1 | §17.13.4.2 p.812, OI 2.1.313 |
| [`w:commentRangeEnd`](#wcommentrangeend) | `w:CT_MarkupRange` | 2 | 0 | 36 | §17.13.4.3 p.815, OI 2.1.314 |
| [`w:commentRangeStart`](#wcommentrangestart) | `w:CT_MarkupRange` | 2 | 0 | 36 | §17.13.4.4 p.817, OI 2.1.315 |
| [`w:commentReference`](#wcommentreference) | `w:CT_Markup` | 1 | 0 | 2 | §17.13.4.5 p.819, OI 2.1.316 |
| [`w:comments`](#wcomments) ◆ | `w:CT_Comments` | 0 | 1 | 0 | §17.13.4.6 p.821 |
| [`w:compat`](#wcompat) | `w:CT_Compat` | 0 | 66 | 1 | §17.15.1.21 p.1017 |
| [`w:compatSetting`](#wcompatsetting) | `w:CT_CompatSetting` | 3 | 0 | 1 | §17.15.3.4 p.1162 |
| [`w:connectString`](#wconnectstring) | `w:CT_String` | 1 | 0 | 1 | §17.14.8 p.949 |
| [`w:consecutiveHyphenLimit`](#wconsecutivehyphenlimit) | `w:CT_DecimalNumber` | 1 | 0 | 1 | §17.15.1.22 p.1017, OI 2.1.396 |
| [`w:contentPart`](#wcontentpart) | `w:CT_Rel` | 1 | 0 | 2 | §17.3.3.2 p.334, OI 2.1.102 |
| [`w:contextualSpacing`](#wcontextualspacing) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.1.9 p.216 |
| [`w:continuationSeparator`](#wcontinuationseparator) | `w:CT_Empty` | 0 | 0 | 2 | §17.11.1 p.758 |
| [`w:control`](#wcontrol) | `w:CT_Control` | 3 | 0 | 2 | §17.3.3.3 p.337, OI 2.1.103; Part 4 §14.3.2.1 p.51, OI 2.1.1756 |
| [`w:convMailMergeEsc`](#wconvmailmergeesc) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.6 p.104, OI 2.1.1776 |
| [`w:cr`](#wcr) | `w:CT_Empty` | 0 | 0 | 2 | §17.3.3.4 p.339 |
| [`w:cs`](#wcs) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.2.7 p.280, OI 2.1.73 |
| [`w:customXml`](#wcustomxml) | `w:CT_CustomXmlRun`, `w:CT_CustomXmlBlock`, `w:CT_CustomXmlRow`, `w:CT_CustomXmlCell` | 2 | 39 | 34 | §17.5.1.3 p.500, OI 2.1.188; §17.5.1.4 p.501, OI 2.1.189; §17.5.1.5 p.503, OI 2.1.190; §17.5.1.6 p.504, OI 2.1.191 |
| [`w:customXmlDelRangeEnd`](#wcustomxmldelrangeend) | `w:CT_Markup` | 1 | 0 | 36 | §17.13.5.4 p.832, OI 2.1.320 |
| [`w:customXmlDelRangeStart`](#wcustomxmldelrangestart) | `w:CT_TrackChange` | 3 | 0 | 36 | §17.13.5.5 p.834, OI 2.1.321 |
| [`w:customXmlInsRangeEnd`](#wcustomxmlinsrangeend) | `w:CT_Markup` | 1 | 0 | 36 | §17.13.5.6 p.836, OI 2.1.322 |
| [`w:customXmlInsRangeStart`](#wcustomxmlinsrangestart) | `w:CT_TrackChange` | 3 | 0 | 36 | §17.13.5.7 p.838, OI 2.1.323 |
| [`w:customXmlMoveFromRangeEnd`](#wcustomxmlmovefromrangeend) | `w:CT_Markup` | 1 | 0 | 36 | §17.13.5.8 p.841, OI 2.1.324 |
| [`w:customXmlMoveFromRangeStart`](#wcustomxmlmovefromrangestart) | `w:CT_TrackChange` | 3 | 0 | 36 | §17.13.5.9 p.843, OI 2.1.325 |
| [`w:customXmlMoveToRangeEnd`](#wcustomxmlmovetorangeend) | `w:CT_Markup` | 1 | 0 | 36 | §17.13.5.10 p.846, OI 2.1.326 |
| [`w:customXmlMoveToRangeStart`](#wcustomxmlmovetorangestart) | `w:CT_TrackChange` | 3 | 0 | 36 | §17.13.5.11 p.848, OI 2.1.327 |
| [`w:customXmlPr`](#wcustomxmlpr) | `w:CT_CustomXmlPr` | 0 | 2 | 1 | §17.5.1.7 p.505 |
| [`w:dataBinding`](#wdatabinding) | `w:CT_DataBinding` | 3 | 0 | 1 | §17.5.2.6 p.515, OI 2.1.193 |
| [`w:dataSource`](#wdatasource) | `w:CT_Rel` | 1 | 0 | 1 | §17.14.9 p.950, OI 2.1.365 |
| [`w:dataType`](#wdatatype) | `w:CT_MailMergeDataType` | 1 | 0 | 1 | §17.14.10 p.952 |
| [`w:date`](#wdate) | `w:CT_SdtDate` | 1 | 4 | 1 | §17.5.2.7 p.518, OI 2.1.194 |
| [`w:dateFormat`](#wdateformat) | `w:CT_String` | 1 | 0 | 1 | §17.5.2.8 p.519 |
| [`w:dayLong`](#wdaylong) | `w:CT_Empty` | 0 | 0 | 2 | §17.3.3.5 p.340 |
| [`w:dayShort`](#wdayshort) | `w:CT_Empty` | 0 | 0 | 2 | §17.3.3.6 p.340 |
| [`w:ddList`](#wddlist) | `w:CT_FFDDList` | 0 | 3 | 1 | §17.16.9 p.1269, OI 2.1.514 |
| [`w:decimalSymbol`](#wdecimalsymbol) | `w:CT_String` | 1 | 0 | 1 | §17.15.1.23 p.1018, OI 2.1.397 |
| [`w:default`](#wdefault) | `w:CT_OnOff`, `w:CT_DecimalNumber`, `w:CT_String` | 1 | 0 | 3 | §17.16.10 p.1269, OI 2.1.515; §17.16.11 p.1271, OI 2.1.516; §17.16.12 p.1272, OI 2.1.517 |
| [`w:defaultTabStop`](#wdefaulttabstop) | `w:CT_TwipsMeasure` | 1 | 0 | 1 | §17.15.1.25 p.1021, OI 2.1.399 |
| [`w:defaultTableStyle`](#wdefaulttablestyle) | `w:CT_String` | 1 | 0 | 1 | §17.15.1.24 p.1020, OI 2.1.398 |
| [`w:del`](#wdel) | `w:CT_RunTrackChange`, `w:CT_MathCtrlDel`, `w:CT_RPrChange`, `w:CT_TrackChange` | 3 | 52 | 39 | §17.13.5.12 p.851, OI 2.1.328; §17.13.5.13 p.853, OI 2.1.329; §17.13.5.14 p.854, OI 2.1.330; §17.13.5.15 p.856, OI 2.1.331 |
| [`w:delInstrText`](#wdelinstrtext) | `w:CT_Text` | 1 | 0 | 2 | §17.16.13 p.1272 |
| [`w:delText`](#wdeltext) | `w:CT_Text` | 1 | 0 | 2 | §17.3.3.7 p.341 |
| [`w:description`](#wdescription) | `w:CT_String` | 1 | 0 | 1 | §17.12.4 p.794 |
| [`w:destination`](#wdestination) | `w:CT_MailMergeDest` | 1 | 0 | 1 | §17.14.11 p.953 |
| [`w:dir`](#wdir) | `w:CT_DirContentRun` | 1 | 34 | 12 | §17.3.2.8 p.281, OI 2.1.74 |
| [`w:dirty`](#wdirty) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.3.8 p.342 |
| [`w:displayBackgroundShape`](#wdisplaybackgroundshape) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.26 p.1022 |
| [`w:displayHangulFixedWidth`](#wdisplayhangulfixedwidth) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.7 p.105 |
| [`w:displayHorizontalDrawingGridEvery`](#wdisplayhorizontaldrawinggridevery) | `w:CT_DecimalNumber` | 1 | 0 | 1 | §17.15.1.27 p.1023, OI 2.1.400 |
| [`w:displayVerticalDrawingGridEvery`](#wdisplayverticaldrawinggridevery) | `w:CT_DecimalNumber` | 1 | 0 | 1 | §17.15.1.28 p.1025, OI 2.1.401 |
| [`w:div`](#wdiv) | `w:CT_Div` | 1 | 8 | 2 | §17.15.2.6 p.1103, OI 2.1.433 |
| [`w:divBdr`](#wdivbdr) | `w:CT_DivBdr` | 0 | 4 | 1 | §17.15.2.7 p.1106 |
| [`w:divId`](#wdivid) | `w:CT_DecimalNumber` | 1 | 0 | 2 | §17.3.1.10 p.217, OI 2.1.42; §17.4.9 p.400, OI 2.1.123 |
| [`w:divs`](#wdivs) | `w:CT_Divs` | 0 | 1 | 1 | §17.15.2.8 p.1107 |
| [`w:divsChild`](#wdivschild) | `w:CT_Divs` | 0 | 1 | 1 | §17.15.2.9 p.1109 |
| [`w:doNotAutoCompressPictures`](#wdonotautocompresspictures) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.33 p.1033 |
| [`w:doNotAutofitConstrainedTables`](#wdonotautofitconstrainedtables) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.8 p.106, OI 2.1.1777 |
| [`w:doNotBreakConstrainedForcedTable`](#wdonotbreakconstrainedforcedtable) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.9 p.107 |
| [`w:doNotBreakWrappedTables`](#wdonotbreakwrappedtables) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.10 p.109 |
| [`w:doNotDemarcateInvalidXml`](#wdonotdemarcateinvalidxml) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.34 p.1034 |
| [`w:doNotDisplayPageBoundaries`](#wdonotdisplaypageboundaries) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.35 p.1034 |
| [`w:doNotEmbedSmartTags`](#wdonotembedsmarttags) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.36 p.1036 |
| [`w:doNotExpandShiftReturn`](#wdonotexpandshiftreturn) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.3.5 p.1164 |
| [`w:doNotHyphenateCaps`](#wdonothyphenatecaps) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.37 p.1036 |
| [`w:doNotIncludeSubdocsInStats`](#wdonotincludesubdocsinstats) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.38 p.1037 |
| [`w:doNotLeaveBackslashAlone`](#wdonotleavebackslashalone) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.3.6 p.1164, OI 2.1.452 |
| [`w:doNotOrganizeInFolder`](#wdonotorganizeinfolder) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.2.10 p.1111, OI 2.1.434 |
| [`w:doNotRelyOnCSS`](#wdonotrelyoncss) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.2.11 p.1111 |
| [`w:doNotSaveAsSingleFile`](#wdonotsaveassinglefile) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.2.12 p.1112 |
| [`w:doNotShadeFormData`](#wdonotshadeformdata) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.39 p.1038 |
| [`w:doNotSnapToGridInCell`](#wdonotsnaptogridincell) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.11 p.111 |
| [`w:doNotSuppressBlankLines`](#wdonotsuppressblanklines) | `w:CT_OnOff` | 1 | 0 | 1 | §17.14.12 p.954, OI 2.1.366 |
| [`w:doNotSuppressIndentation`](#wdonotsuppressindentation) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.12 p.112 |
| [`w:doNotSuppressParagraphBorders`](#wdonotsuppressparagraphborders) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.13 p.114, OI 2.1.1778 |
| [`w:doNotTrackFormatting`](#wdonottrackformatting) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.40 p.1038 |
| [`w:doNotTrackMoves`](#wdonottrackmoves) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.41 p.1040 |
| [`w:doNotUseEastAsianBreakRules`](#wdonotuseeastasianbreakrules) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.14 p.115 |
| [`w:doNotUseHTMLParagraphAutoSpacing`](#wdonotusehtmlparagraphautospacing) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.15 p.116 |
| [`w:doNotUseIndentAsNumberingTabStop`](#wdonotuseindentasnumberingtabstop) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.16 p.118 |
| [`w:doNotUseLongFileNames`](#wdonotuselongfilenames) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.2.13 p.1113 |
| [`w:doNotUseMarginsForDrawingGridOrigin`](#wdonotusemarginsfordrawinggridorigin) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.42 p.1041, OI 2.1.404 |
| [`w:doNotValidateAgainstSchema`](#wdonotvalidateagainstschema) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.43 p.1041 |
| [`w:doNotVertAlignCellWithSp`](#wdonotvertaligncellwithsp) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.17 p.119 |
| [`w:doNotVertAlignInTxbx`](#wdonotvertalignintxbx) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.18 p.121 |
| [`w:doNotWrapTextWithPunct`](#wdonotwraptextwithpunct) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.19 p.123 |
| [`w:docDefaults`](#wdocdefaults) | `w:CT_DocDefaults` | 0 | 2 | 1 | §17.7.5.1 p.657 |
| [`w:docGrid`](#wdocgrid) | `w:CT_DocGrid` | 3 | 0 | 1 | §17.6.5 p.570 |
| [`w:docPart`](#wdocpart) | `w:CT_String`, `w:CT_DocPart` | 1 | 2 | 2 | §17.5.2.9 p.521, OI 2.1.195; §17.12.5 p.795 |
| [`w:docPartBody`](#wdocpartbody) | `w:CT_Body` | 0 | 31 | 1 | §17.12.6 p.796, OI 2.1.310 |
| [`w:docPartCategory`](#wdocpartcategory) | `w:CT_String` | 1 | 0 | 2 | §17.5.2.10 p.522 |
| [`w:docPartGallery`](#wdocpartgallery) | `w:CT_String` | 1 | 0 | 2 | §17.5.2.11 p.523 |
| [`w:docPartList`](#wdocpartlist) | `w:CT_SdtDocPart` | 0 | 3 | 1 | §17.5.2.12 p.524 |
| [`w:docPartObj`](#wdocpartobj) | `w:CT_SdtDocPart` | 0 | 3 | 1 | §17.5.2.13 p.525 |
| [`w:docPartPr`](#wdocpartpr) | `w:CT_DocPartPr` | 0 | 7 | 1 | §17.12.7 p.798, OI 2.1.311 |
| [`w:docPartUnique`](#wdocpartunique) | `w:CT_OnOff` | 1 | 0 | 2 | §17.5.2.14 p.526 |
| [`w:docParts`](#wdocparts) | `w:CT_DocParts` | 0 | 1 | 1 | §17.12.8 p.798, OI 2.1.312 |
| [`w:docVar`](#wdocvar) | `w:CT_DocVar` | 2 | 0 | 1 | §17.15.1.31 p.1032, OI 2.1.403 |
| [`w:docVars`](#wdocvars) | `w:CT_DocVars` | 0 | 1 | 1 | §17.15.1.32 p.1033 |
| [`w:document`](#wdocument) ◆ | `w:CT_Document` | 1 | 2 | 0 | §17.2.3 p.203, OI 2.1.37 |
| [`w:documentProtection`](#wdocumentprotection) | `w:CT_DocProtect` | 19 | 0 | 1 | §17.15.1.29 p.1026, OI 2.1.402 |
| [`w:documentType`](#wdocumenttype) | `w:CT_DocType` | 1 | 0 | 1 | §17.15.1.30 p.1031 |
| [`w:drawing`](#wdrawing) | `w:CT_Drawing` | 0 | 2 | 5 | §17.3.3.9 p.342, OI 2.1.104 |
| [`w:drawingGridHorizontalOrigin`](#wdrawinggridhorizontalorigin) | `w:CT_TwipsMeasure` | 1 | 0 | 1 | §17.15.1.44 p.1041, OI 2.1.405 |
| [`w:drawingGridHorizontalSpacing`](#wdrawinggridhorizontalspacing) | `w:CT_TwipsMeasure` | 1 | 0 | 1 | §17.15.1.45 p.1043 |
| [`w:drawingGridVerticalOrigin`](#wdrawinggridverticalorigin) | `w:CT_TwipsMeasure` | 1 | 0 | 1 | §17.15.1.46 p.1044, OI 2.1.406 |
| [`w:drawingGridVerticalSpacing`](#wdrawinggridverticalspacing) | `w:CT_TwipsMeasure` | 1 | 0 | 1 | §17.15.1.47 p.1045 |
| [`w:dropDownList`](#wdropdownlist) | `w:CT_SdtDropDownList` | 1 | 1 | 1 | §17.5.2.15 p.526 |
| [`w:dstrike`](#wdstrike) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.2.9 p.283 |
| [`w:dynamicAddress`](#wdynamicaddress) | `w:CT_OnOff` | 1 | 0 | 1 | §17.14.13 p.954, OI 2.1.367 |
| [`w:eastAsianLayout`](#weastasianlayout) | `w:CT_EastAsianLayout` | 5 | 0 | 1 | §17.3.2.10 p.283, OI 2.1.75 |
| [`w:effect`](#weffect) | `w:CT_TextEffect` | 1 | 0 | 1 | §17.3.2.11 p.287, OI 2.1.76 |
| [`w:em`](#wem) | `w:CT_Em` | 1 | 0 | 1 | §17.3.2.12 p.288 |
| [`w:embedBold`](#wembedbold) | `w:CT_FontRel` | 3 | 0 | 1 | §17.8.3.3 p.683, OI 2.1.264 |
| [`w:embedBoldItalic`](#wembedbolditalic) | `w:CT_FontRel` | 3 | 0 | 1 | §17.8.3.4 p.686, OI 2.1.265 |
| [`w:embedItalic`](#wembeditalic) | `w:CT_FontRel` | 3 | 0 | 1 | §17.8.3.5 p.688, OI 2.1.266 |
| [`w:embedRegular`](#wembedregular) | `w:CT_FontRel` | 3 | 0 | 1 | §17.8.3.6 p.690, OI 2.1.267 |
| [`w:embedSystemFonts`](#wembedsystemfonts) | `w:CT_OnOff` | 1 | 0 | 1 | §17.8.3.7 p.692 |
| [`w:embedTrueTypeFonts`](#wembedtruetypefonts) | `w:CT_OnOff` | 1 | 0 | 1 | §17.8.3.8 p.692, OI 2.1.268 |
| [`w:emboss`](#wemboss) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.2.13 p.289, OI 2.1.77 |
| [`w:enabled`](#wenabled) | `w:CT_OnOff` | 1 | 0 | 1 | §17.16.14 p.1273 |
| [`w:encoding`](#wencoding) | `w:CT_String` | 1 | 0 | 1 | §17.15.2.14 p.1113, OI 2.1.435 |
| [`w:end`](#wend) | `w:CT_Border`, `w:CT_TblWidth` | 11 | 0 | 4 | §17.4.10 p.402, OI 2.1.124; §17.4.11 p.403, OI 2.1.125; §17.4.12 p.403, OI 2.1.126; §17.4.13 p.404, OI 2.1.127 |
| [`w:endnote`](#wendnote) | `w:CT_FtnEdn`, `w:CT_FtnEdnSepRef` | 2 | 30 | 2 | §17.11.2 p.759, OI 2.1.300; §17.11.3 p.761, OI 2.1.301 |
| [`w:endnotePr`](#wendnotepr) | `w:CT_EdnDocProps`, `w:CT_EdnProps` | 0 | 5 | 2 | §17.11.4 p.762; §17.11.5 p.763 |
| [`w:endnoteRef`](#wendnoteref) | `w:CT_Empty` | 0 | 0 | 2 | §17.11.6 p.764 |
| [`w:endnoteReference`](#wendnotereference) | `w:CT_FtnEdnRef` | 2 | 0 | 2 | §17.11.7 p.766, OI 2.1.302 |
| [`w:endnotes`](#wendnotes) ◆ | `w:CT_Endnotes` | 0 | 1 | 0 | §17.11.8 p.768 |
| [`w:entryMacro`](#wentrymacro) | `w:CT_MacroName` | 1 | 0 | 1 | §17.16.15 p.1274 |
| [`w:equation`](#wequation) | `w:CT_Empty` | 0 | 0 | 1 | §17.5.2.16 p.528 |
| [`w:evenAndOddHeaders`](#wevenandoddheaders) | `w:CT_OnOff` | 1 | 0 | 1 | §17.10.1 p.744, OI 2.1.294 |
| [`w:exitMacro`](#wexitmacro) | `w:CT_MacroName` | 1 | 0 | 1 | §17.16.16 p.1275 |
| [`w:fHdr`](#wfhdr) | `w:CT_OnOff` | 1 | 0 | 1 | §17.14.14 p.955, OI 2.1.368 |
| [`w:family`](#wfamily) | `w:CT_FontFamily` | 1 | 0 | 1 | §17.8.3.9 p.693, OI 2.1.269 |
| [`w:ffData`](#wffdata) | `w:CT_FFData` | 0 | 12 | 1 | §17.16.17 p.1276, OI 2.1.518 |
| [`w:fieldMapData`](#wfieldmapdata) | `w:CT_OdsoFieldMapData` | 0 | 6 | 1 | §17.14.15 p.956, OI 2.1.369 |
| [`w:fitText`](#wfittext) | `w:CT_FitText` | 2 | 0 | 1 | §17.3.2.14 p.289, OI 2.1.78 |
| [`w:flatBorders`](#wflatborders) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.2.15 p.1114 |
| [`w:fldChar`](#wfldchar) | `w:CT_FldChar` | 3 | 3 | 2 | §17.16.18 p.1277 |
| [`w:fldData`](#wflddata) | `w:CT_Text` | 1 | 0 | 2 | Part 4 §14.10.5 p.196; Part 4 §14.10.6 p.197 |
| [`w:fldSimple`](#wfldsimple) | `w:CT_SimpleField` | 3 | 35 | 17 | §17.16.19 p.1281, OI 2.1.519 |
| [`w:font`](#wfont) | `w:CT_Font` | 1 | 11 | 1 | §17.8.3.10 p.693, OI 2.1.270 |
| [`w:fonts`](#wfonts) ◆ | `w:CT_FontsList` | 0 | 1 | 0 | §17.8.3.11 p.694 |
| [`w:footerReference`](#wfooterreference) | `w:CT_HdrFtrRef` | 2 | 0 | 1 | §17.10.2 p.745, OI 2.1.295 |
| [`w:footnote`](#wfootnote) | `w:CT_FtnEdn`, `w:CT_FtnEdnSepRef` | 2 | 30 | 2 | §17.11.9 p.769, OI 2.1.303; §17.11.10 p.770, OI 2.1.304 |
| [`w:footnoteLayoutLikeWW8`](#wfootnotelayoutlikeww8) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.20 p.124 |
| [`w:footnotePr`](#wfootnotepr) | `w:CT_FtnDocProps`, `w:CT_FtnProps` | 0 | 5 | 2 | §17.11.11 p.773; §17.11.12 p.774 |
| [`w:footnoteRef`](#wfootnoteref) | `w:CT_Empty` | 0 | 0 | 2 | §17.11.13 p.775 |
| [`w:footnoteReference`](#wfootnotereference) | `w:CT_FtnEdnRef` | 2 | 0 | 2 | §17.11.14 p.776, OI 2.1.305 |
| [`w:footnotes`](#wfootnotes) ◆ | `w:CT_Footnotes` | 0 | 1 | 0 | §17.11.15 p.778 |
| [`w:forceUpgrade`](#wforceupgrade) | `w:CT_Empty` | 0 | 0 | 1 | §17.15.1.48 p.1047 |
| [`w:forgetLastTabAlignment`](#wforgetlasttabalignment) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.21 p.127 |
| [`w:formProt`](#wformprot) | `w:CT_OnOff` | 1 | 0 | 1 | §17.6.6 p.575 |
| [`w:format`](#wformat) | `w:CT_String` | 1 | 0 | 1 | §17.16.20 p.1283, OI 2.1.520 |
| [`w:formsDesign`](#wformsdesign) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.49 p.1047 |
| [`w:frame`](#wframe) | `w:CT_Frame` | 0 | 10 | 1 | §17.15.2.16 p.1115 |
| [`w:frameLayout`](#wframelayout) | `w:CT_FrameLayout` | 1 | 0 | 1 | §17.15.2.17 p.1117, OI 2.1.436 |
| [`w:framePr`](#wframepr) | `w:CT_FramePr` | 15 | 0 | 1 | §17.3.1.11 p.218, OI 2.1.43 |
| [`w:frameset`](#wframeset) | `w:CT_Frameset` | 0 | 6 | 2 | §17.15.2.18 p.1118, OI 2.1.437; §17.15.2.19 p.1120, OI 2.1.438 |
| [`w:framesetSplitbar`](#wframesetsplitbar) | `w:CT_FramesetSplitbar` | 0 | 4 | 1 | §17.15.2.20 p.1121 |
| [`w:ftr`](#wftr) ◆ | `w:CT_HdrFtr` | 0 | 30 | 0 | §17.10.3 p.748, OI 2.1.296 |
| [`w:gallery`](#wgallery) | `w:CT_DocPartGallery` | 1 | 0 | 1 | §17.12.9 p.799 |
| [`w:glossaryDocument`](#wglossarydocument) ◆ | `w:CT_GlossaryDocument` | 0 | 2 | 0 | §17.12.10 p.800 |
| [`w:gridAfter`](#wgridafter) | `w:CT_DecimalNumber` | 1 | 0 | 1 | §17.4.14 p.405, OI 2.1.128 |
| [`w:gridBefore`](#wgridbefore) | `w:CT_DecimalNumber` | 1 | 0 | 1 | §17.4.15 p.406, OI 2.1.129 |
| [`w:gridCol`](#wgridcol) | `w:CT_TblGridCol` | 1 | 0 | 1 | §17.4.16 p.407 |
| [`w:gridSpan`](#wgridspan) | `w:CT_DecimalNumber` | 1 | 0 | 1 | §17.4.17 p.409, OI 2.1.130 |
| [`w:group`](#wgroup) | `w:CT_Empty` | 0 | 0 | 1 | §17.5.2.17 p.529 |
| [`w:growAutofit`](#wgrowautofit) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.22 p.129 |
| [`w:guid`](#wguid) | `w:CT_Guid` | 1 | 0 | 1 | §17.12.11 p.801 |
| [`w:gutterAtTop`](#wgutterattop) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.50 p.1048 |
| [`w:hMerge`](#whmerge) | `w:CT_HMerge` | 1 | 0 | 1 | Part 4 §14.4.13 p.59, OI 2.1.1768 |
| [`w:hdr`](#whdr) ◆ | `w:CT_HdrFtr` | 0 | 30 | 0 | §17.10.4 p.750, OI 2.1.297 |
| [`w:hdrShapeDefaults`](#whdrshapedefaults) | `w:CT_ShapeDefaults` | 0 | 1 | 1 | Part 4 §14.8.2.1 p.82, OI 2.1.1773 |
| [`w:header`](#wheader) | `w:CT_String` | 1 | 0 | 1 | §17.4.18 p.411, OI 2.1.131 |
| [`w:headerReference`](#wheaderreference) | `w:CT_HdrFtrRef` | 2 | 0 | 1 | §17.10.5 p.752, OI 2.1.298 |
| [`w:headerSource`](#wheadersource) | `w:CT_Rel` | 1 | 0 | 1 | §17.14.16 p.956, OI 2.1.370 |
| [`w:headers`](#wheaders) | `w:CT_Headers` | 0 | 1 | 1 | §17.4.19 p.413, OI 2.1.132 |
| [`w:helpText`](#whelptext) | `w:CT_FFHelpText` | 2 | 0 | 1 | §17.16.21 p.1284 |
| [`w:hidden`](#whidden) | `w:CT_OnOff` | 1 | 0 | 2 | §17.4.20 p.415, OI 2.1.133; §17.7.4.4 p.633 |
| [`w:hideGrammaticalErrors`](#whidegrammaticalerrors) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.51 p.1049 |
| [`w:hideMark`](#whidemark) | `w:CT_OnOff` | 1 | 0 | 1 | §17.4.21 p.415, OI 2.1.134 |
| [`w:hideSpellingErrors`](#whidespellingerrors) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.52 p.1049 |
| [`w:highlight`](#whighlight) | `w:CT_Highlight` | 1 | 0 | 1 | §17.3.2.15 p.291, OI 2.1.79 |
| [`w:hps`](#whps) | `w:CT_HpsMeasure` | 1 | 0 | 1 | §17.3.3.10 p.343 |
| [`w:hpsBaseText`](#whpsbasetext) | `w:CT_HpsMeasure` | 1 | 0 | 1 | §17.3.3.11 p.343 |
| [`w:hpsRaise`](#whpsraise) | `w:CT_HpsMeasure` | 1 | 0 | 1 | §17.3.3.12 p.344, OI 2.1.105 |
| [`w:hyperlink`](#whyperlink) | `w:CT_Hyperlink` | 6 | 34 | 17 | §17.16.22 p.1286, OI 2.1.521; Part 4 §14.10.7 p.198 |
| [`w:hyphenationZone`](#whyphenationzone) | `w:CT_TwipsMeasure` | 1 | 0 | 1 | §17.15.1.53 p.1049, OI 2.1.407 |
| [`w:i`](#wi) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.2.16 p.292, OI 2.1.80 |
| [`w:iCs`](#wics) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.2.17 p.292, OI 2.1.81 |
| [`w:id`](#wid) | `w:CT_DecimalNumber` | 1 | 0 | 1 | §17.5.2.18 p.529, OI 2.1.196 |
| [`w:ignoreMixedContent`](#wignoremixedcontent) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.54 p.1051 |
| [`w:ilvl`](#wilvl) | `w:CT_DecimalNumber` | 1 | 0 | 1 | §17.9.3 p.706, OI 2.1.277 |
| [`w:imprint`](#wimprint) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.2.18 p.293, OI 2.1.82 |
| [`w:ind`](#wind) | `w:CT_Ind` | 12 | 0 | 1 | §17.3.1.12 p.229, OI 2.1.44 |
| [`w:ins`](#wins) | `w:CT_RunTrackChange`, `w:CT_MathCtrlIns`, `w:CT_TrackChange` | 3 | 52 | 40 | §17.13.5.16 p.859, OI 2.1.332; §17.13.5.17 p.860, OI 2.1.333; §17.13.5.18 p.862, OI 2.1.334; §17.13.5.19 p.864, OI 2.1.335; §17.13.5.20 p.867, OI 2.1.336 |
| [`w:insideH`](#winsideh) | `w:CT_Border` | 9 | 0 | 2 | §17.4.22 p.416, OI 2.1.135; §17.4.23 p.418, OI 2.1.136 |
| [`w:insideV`](#winsidev) | `w:CT_Border` | 9 | 0 | 2 | §17.4.24 p.419, OI 2.1.137; §17.4.25 p.419, OI 2.1.138 |
| [`w:instrText`](#winstrtext) | `w:CT_Text` | 1 | 0 | 2 | §17.16.23 p.1289 |
| [`w:isLgl`](#wislgl) | `w:CT_OnOff` | 1 | 0 | 1 | §17.9.4 p.707, OI 2.1.278 |
| [`w:jc`](#wjc) | `w:CT_Jc`, `w:CT_JcTable` | 1 | 0 | 4 | §17.3.1.13 p.234, OI 2.1.45; §17.4.26 p.420, OI 2.1.139; §17.4.27 p.421, OI 2.1.140; §17.4.28 p.422, OI 2.1.141 |
| [`w:keepLines`](#wkeeplines) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.1.14 p.235, OI 2.1.46 |
| [`w:keepNext`](#wkeepnext) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.1.15 p.236 |
| [`w:kern`](#wkern) | `w:CT_HpsMeasure` | 1 | 0 | 1 | §17.3.2.19 p.293, OI 2.1.83 |
| [`w:kinsoku`](#wkinsoku) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.1.16 p.238, OI 2.1.47 |
| [`w:label`](#wlabel) | `w:CT_DecimalNumber` | 1 | 0 | 2 | §17.5.2.19 p.530, OI 2.1.197; §17.16.24 p.1290, OI 2.1.522 |
| [`w:lang`](#wlang) | `w:CT_Language` | 3 | 0 | 1 | §17.3.2.20 p.295, OI 2.1.84 |
| [`w:lastRenderedPageBreak`](#wlastrenderedpagebreak) | `w:CT_Empty` | 0 | 0 | 2 | §17.3.3.13 p.345, OI 2.1.106 |
| [`w:latentStyles`](#wlatentstyles) | `w:CT_LatentStyles` | 6 | 1 | 1 | §17.7.4.5 p.634, OI 2.1.234 |
| [`w:layoutRawTableWidth`](#wlayoutrawtablewidth) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.23 p.130 |
| [`w:layoutTableRowsApart`](#wlayouttablerowsapart) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.24 p.132 |
| [`w:left`](#wleft) | `w:CT_Border`, `w:CT_PageBorder`, `w:CT_TblWidth` | 12 | 0 | 7 | §17.3.1.17 p.241, OI 2.1.48; §17.6.7 p.575, OI 2.1.214; §17.15.2.21 p.1122, OI 2.1.439; Part 4 §14.4.1 p.54, OI 2.1.1758; Part 4 §14.4.2 p.54, OI 2.1.1759; Part 4 §14.4.3 p.54, OI 2.1.1760; Part 4 §14.4.4 p.54, OI 2.1.1761 |
| [`w:legacy`](#wlegacy) | `w:CT_LvlLegacy` | 3 | 0 | 1 | Part 4 §14.6.2 p.64, OI 2.1.1770 |
| [`w:lid`](#wlid) | `w:CT_Lang` | 1 | 0 | 3 | §17.3.3.14 p.346, OI 2.1.107; §17.5.2.20 p.532, OI 2.1.198; §17.14.17 p.958, OI 2.1.371 |
| [`w:lineWrapLikeWord6`](#wlinewraplikeword6) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.25 p.133 |
| [`w:link`](#wlink) | `w:CT_String` | 1 | 0 | 1 | §17.7.4.6 p.638, OI 2.1.235 |
| [`w:linkStyles`](#wlinkstyles) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.55 p.1052 |
| [`w:linkToQuery`](#wlinktoquery) | `w:CT_OnOff` | 1 | 0 | 1 | §17.14.18 p.959, OI 2.1.372 |
| [`w:linkedToFile`](#wlinkedtofile) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.2.22 p.1123 |
| [`w:listEntry`](#wlistentry) | `w:CT_String` | 1 | 0 | 1 | §17.16.25 p.1292, OI 2.1.523 |
| [`w:listItem`](#wlistitem) | `w:CT_SdtListItem` | 2 | 0 | 2 | §17.5.2.21 p.532; §17.5.2.22 p.534 |
| [`w:listSeparator`](#wlistseparator) | `w:CT_String` | 1 | 0 | 1 | §17.15.1.56 p.1053, OI 2.1.408 |
| [`w:lnNumType`](#wlnnumtype) | `w:CT_LineNumber` | 4 | 0 | 1 | §17.6.8 p.583, OI 2.1.215 |
| [`w:lock`](#wlock) | `w:CT_Lock` | 1 | 0 | 1 | §17.5.2.23 p.536 |
| [`w:locked`](#wlocked) | `w:CT_OnOff` | 1 | 0 | 1 | §17.7.4.7 p.639 |
| [`w:longDesc`](#wlongdesc) | `w:CT_Rel` | 1 | 0 | 1 | §17.15.2.23 p.1124, OI 2.1.440 |
| [`w:lsdException`](#wlsdexception) | `w:CT_LsdException` | 6 | 0 | 1 | §17.7.4.8 p.640, OI 2.1.236 |
| [`w:lvl`](#wlvl) | `w:CT_Lvl` | 3 | 12 | 2 | §17.9.5 p.708, OI 2.1.279; §17.9.6 p.711, OI 2.1.280 |
| [`w:lvlJc`](#wlvljc) | `w:CT_Jc` | 1 | 0 | 1 | §17.9.7 p.714, OI 2.1.281 |
| [`w:lvlOverride`](#wlvloverride) | `w:CT_NumLvl` | 1 | 2 | 1 | §17.9.8 p.715 |
| [`w:lvlPicBulletId`](#wlvlpicbulletid) | `w:CT_DecimalNumber` | 1 | 0 | 1 | §17.9.9 p.717 |
| [`w:lvlRestart`](#wlvlrestart) | `w:CT_DecimalNumber` | 1 | 0 | 1 | §17.9.10 p.718, OI 2.1.282 |
| [`w:lvlText`](#wlvltext) | `w:CT_LevelText` | 2 | 0 | 1 | §17.9.11 p.721, OI 2.1.283 |
| [`w:mailAsAttachment`](#wmailasattachment) | `w:CT_OnOff` | 1 | 0 | 1 | §17.14.19 p.959 |
| [`w:mailMerge`](#wmailmerge) | `w:CT_MailMerge` | 0 | 16 | 1 | §17.14.20 p.960, OI 2.1.373 |
| [`w:mailSubject`](#wmailsubject) | `w:CT_String` | 1 | 0 | 1 | §17.14.21 p.961 |
| [`w:mainDocumentType`](#wmaindocumenttype) | `w:CT_MailMergeDocType` | 1 | 0 | 1 | §17.14.22 p.962 |
| [`w:mappedName`](#wmappedname) | `w:CT_String` | 1 | 0 | 1 | §17.14.23 p.963, OI 2.1.374 |
| [`w:marBottom`](#wmarbottom) | `w:CT_SignedTwipsMeasure` | 1 | 0 | 1 | §17.15.2.24 p.1127 |
| [`w:marH`](#wmarh) | `w:CT_PixelsMeasure` | 1 | 0 | 1 | §17.15.2.25 p.1128 |
| [`w:marLeft`](#wmarleft) | `w:CT_SignedTwipsMeasure` | 1 | 0 | 1 | §17.15.2.26 p.1130 |
| [`w:marRight`](#wmarright) | `w:CT_SignedTwipsMeasure` | 1 | 0 | 1 | §17.15.2.27 p.1131 |
| [`w:marTop`](#wmartop) | `w:CT_SignedTwipsMeasure` | 1 | 0 | 1 | §17.15.2.28 p.1133 |
| [`w:marW`](#wmarw) | `w:CT_PixelsMeasure` | 1 | 0 | 1 | §17.15.2.29 p.1135 |
| [`w:matchSrc`](#wmatchsrc) | `w:CT_OnOff` | 1 | 0 | 1 | §17.17.2.3 p.1310 |
| [`w:maxLength`](#wmaxlength) | `w:CT_DecimalNumber` | 1 | 0 | 1 | §17.16.26 p.1293, OI 2.1.524 |
| [`w:mirrorIndents`](#wmirrorindents) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.1.18 p.242, OI 2.1.49 |
| [`w:mirrorMargins`](#wmirrormargins) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.57 p.1054 |
| [`w:monthLong`](#wmonthlong) | `w:CT_Empty` | 0 | 0 | 2 | §17.3.3.15 p.346 |
| [`w:monthShort`](#wmonthshort) | `w:CT_Empty` | 0 | 0 | 2 | §17.3.3.16 p.347 |
| [`w:moveFrom`](#wmovefrom) | `w:CT_RunTrackChange`, `w:CT_TrackChange` | 3 | 51 | 37 | §17.13.5.21 p.869, OI 2.1.337; §17.13.5.22 p.871, OI 2.1.338 |
| [`w:moveFromRangeEnd`](#wmovefromrangeend) | `w:CT_MarkupRange` | 2 | 0 | 36 | §17.13.5.23 p.873, OI 2.1.339 |
| [`w:moveFromRangeStart`](#wmovefromrangestart) | `w:CT_MoveBookmark` | 7 | 0 | 36 | §17.13.5.24 p.877, OI 2.1.340 |
| [`w:moveTo`](#wmoveto) | `w:CT_RunTrackChange`, `w:CT_TrackChange` | 3 | 51 | 37 | §17.13.5.25 p.884, OI 2.1.341; §17.13.5.26 p.887, OI 2.1.342 |
| [`w:moveToRangeEnd`](#wmovetorangeend) | `w:CT_MarkupRange` | 2 | 0 | 36 | §17.13.5.27 p.889, OI 2.1.343 |
| [`w:moveToRangeStart`](#wmovetorangestart) | `w:CT_MoveBookmark` | 7 | 0 | 36 | §17.13.5.28 p.892, OI 2.1.344 |
| [`w:movie`](#wmovie) | `w:CT_Rel` | 1 | 0 | 2 | §17.3.3.17 p.348, OI 2.1.108 |
| [`w:multiLevelType`](#wmultileveltype) | `w:CT_MultiLevelType` | 1 | 0 | 1 | §17.9.12 p.722 |
| [`w:mwSmallCaps`](#wmwsmallcaps) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.26 p.134 |
| [`w:name`](#wname) | `w:CT_FFName`, `w:CT_String`, `w:CT_DocPartName` | 2 | 0 | 7 | §17.7.4.9 p.643, OI 2.1.237; §17.9.13 p.723, OI 2.1.284; §17.12.12 p.801; §17.12.13 p.803; §17.14.24 p.965, OI 2.1.375; §17.15.2.30 p.1136, OI 2.1.441; §17.16.27 p.1294 |
| [`w:next`](#wnext) | `w:CT_String` | 1 | 0 | 1 | §17.7.4.10 p.644, OI 2.1.238 |
| [`w:noBorder`](#wnoborder) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.2.31 p.1138 |
| [`w:noBreakHyphen`](#wnobreakhyphen) | `w:CT_Empty` | 0 | 0 | 2 | §17.3.3.18 p.349 |
| [`w:noColumnBalance`](#wnocolumnbalance) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.27 p.135 |
| [`w:noEndnote`](#wnoendnote) | `w:CT_OnOff` | 1 | 0 | 1 | §17.11.16 p.779 |
| [`w:noExtraLineSpacing`](#wnoextralinespacing) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.28 p.137 |
| [`w:noLeading`](#wnoleading) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.29 p.138 |
| [`w:noLineBreaksAfter`](#wnolinebreaksafter) | `w:CT_Kinsoku` | 2 | 0 | 1 | §17.15.1.58 p.1055, OI 2.1.409 |
| [`w:noLineBreaksBefore`](#wnolinebreaksbefore) | `w:CT_Kinsoku` | 2 | 0 | 1 | §17.15.1.59 p.1056, OI 2.1.410 |
| [`w:noProof`](#wnoproof) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.2.21 p.297, OI 2.1.85 |
| [`w:noPunctuationKerning`](#wnopunctuationkerning) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.60 p.1058, OI 2.1.411 |
| [`w:noResizeAllowed`](#wnoresizeallowed) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.2.32 p.1140 |
| [`w:noSpaceRaiseLower`](#wnospaceraiselower) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.30 p.140 |
| [`w:noTabHangInd`](#wnotabhangind) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.31 p.140 |
| [`w:noWrap`](#wnowrap) | `w:CT_OnOff` | 1 | 0 | 1 | §17.4.29 p.423 |
| [`w:notTrueType`](#wnottruetype) | `w:CT_OnOff` | 1 | 0 | 1 | §17.8.3.12 p.695, OI 2.1.271 |
| [`w:nsid`](#wnsid) | `w:CT_LongHexNumber` | 1 | 0 | 1 | §17.9.14 p.724 |
| [`w:num`](#wnum) | `w:CT_Num` | 1 | 2 | 1 | §17.9.15 p.725, OI 2.1.285 |
| [`w:numFmt`](#wnumfmt) | `w:CT_NumFmt` | 2 | 0 | 3 | §17.9.17 p.728, OI 2.1.286; §17.11.17 p.779, OI 2.1.306; §17.11.18 p.781, OI 2.1.307 |
| [`w:numId`](#wnumid) | `w:CT_DecimalNumber` | 1 | 0 | 1 | §17.9.18 p.729, OI 2.1.287 |
| [`w:numIdMacAtCleanup`](#wnumidmacatcleanup) | `w:CT_DecimalNumber` | 1 | 0 | 1 | §17.9.19 p.730 |
| [`w:numPicBullet`](#wnumpicbullet) | `w:CT_NumPicBullet` | 1 | 2 | 1 | §17.9.20 p.731 |
| [`w:numPr`](#wnumpr) | `w:CT_NumPr` | 0 | 4 | 1 | §17.3.1.19 p.243, OI 2.1.50 |
| [`w:numRestart`](#wnumrestart) | `w:CT_NumRestart` | 1 | 0 | 2 | §17.11.19 p.782 |
| [`w:numStart`](#wnumstart) | `w:CT_DecimalNumber` | 1 | 0 | 2 | §17.11.20 p.783, OI 2.1.308 |
| [`w:numStyleLink`](#wnumstylelink) | `w:CT_String` | 1 | 0 | 1 | §17.9.21 p.733, OI 2.1.288 |
| [`w:numbering`](#wnumbering) ◆ | `w:CT_Numbering` | 0 | 4 | 0 | §17.9.16 p.728 |
| [`w:numberingChange`](#wnumberingchange) | `w:CT_TrackChangeNumbering` | 4 | 0 | 2 | Part 4 §14.7.1.1 p.65, OI 2.1.1771; Part 4 §14.7.1.2 p.69, OI 2.1.1772 |
| [`w:oMath`](#womath) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.2.22 p.297, OI 2.1.86 |
| [`w:object`](#wobject) | `w:CT_Object` | 2 | 7 | 2 | §17.3.3.19 p.350, OI 2.1.109 |
| [`w:objectEmbed`](#wobjectembed) | `w:CT_ObjectEmbed` | 5 | 0 | 1 | §17.3.3.20 p.351, OI 2.1.110 |
| [`w:objectLink`](#wobjectlink) | `w:CT_ObjectLink` | 7 | 0 | 1 | §17.3.3.21 p.354, OI 2.1.111 |
| [`w:odso`](#wodso) | `w:CT_Odso` | 0 | 8 | 1 | §17.14.25 p.966, OI 2.1.376 |
| [`w:optimizeForBrowser`](#woptimizeforbrowser) | `w:CT_OptimizeForBrowser` | 2 | 0 | 1 | §17.15.2.33 p.1141, OI 2.1.442 |
| [`w:outline`](#woutline) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.2.23 p.298 |
| [`w:outlineLvl`](#woutlinelvl) | `w:CT_DecimalNumber` | 1 | 0 | 1 | §17.3.1.20 p.243, OI 2.1.51 |
| [`w:overflowPunct`](#woverflowpunct) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.1.21 p.244, OI 2.1.52 |
| [`w:p`](#wp) | `w:CT_P` | 5 | 35 | 13 | §17.3.1.22 p.245, OI 2.1.53 |
| [`w:pBdr`](#wpbdr) | `w:CT_PBdr` | 0 | 6 | 1 | §17.3.1.24 p.249 |
| [`w:pPr`](#wppr) | `w:CT_PPrBase`, `w:CT_PPr`, `w:CT_PPrGeneral` | 0 | 36 | 6 | §17.3.1.25 p.249, OI 2.1.54; §17.3.1.26 p.250; §17.7.5.2 p.658, OI 2.1.244; §17.7.6.1 p.664, OI 2.1.247; §17.7.8.2 p.677, OI 2.1.259; §17.9.22 p.734, OI 2.1.289 |
| [`w:pPrChange`](#wpprchange) | `w:CT_PPrChange` | 3 | 1 | 1 | §17.13.5.29 p.899, OI 2.1.345 |
| [`w:pPrDefault`](#wpprdefault) | `w:CT_PPrDefault` | 0 | 1 | 1 | §17.7.5.3 p.659 |
| [`w:pStyle`](#wpstyle) | `w:CT_String` | 1 | 0 | 2 | §17.3.1.27 p.250, OI 2.1.55; §17.9.23 p.735, OI 2.1.290 |
| [`w:pageBreakBefore`](#wpagebreakbefore) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.1.23 p.247 |
| [`w:panose1`](#wpanose1) | `w:CT_Panose` | 1 | 0 | 1 | §17.8.3.13 p.695, OI 2.1.272 |
| [`w:paperSrc`](#wpapersrc) | `w:CT_PaperSource` | 2 | 0 | 1 | §17.6.9 p.585, OI 2.1.216 |
| [`w:permEnd`](#wpermend) | `w:CT_Perm` | 2 | 0 | 36 | §17.13.7.1 p.927, OI 2.1.357 |
| [`w:permStart`](#wpermstart) | `w:CT_PermStart` | 6 | 0 | 36 | §17.13.7.2 p.929, OI 2.1.358 |
| [`w:personal`](#wpersonal) | `w:CT_OnOff` | 1 | 0 | 1 | §17.7.4.11 p.646, OI 2.1.239 |
| [`w:personalCompose`](#wpersonalcompose) | `w:CT_OnOff` | 1 | 0 | 1 | §17.7.4.12 p.646, OI 2.1.240 |
| [`w:personalReply`](#wpersonalreply) | `w:CT_OnOff` | 1 | 0 | 1 | §17.7.4.13 p.647, OI 2.1.241 |
| [`w:pgBorders`](#wpgborders) | `w:CT_PageBorders` | 3 | 4 | 1 | §17.6.10 p.586, OI 2.1.217 |
| [`w:pgMar`](#wpgmar) | `w:CT_PageMar` | 7 | 0 | 1 | §17.6.11 p.589, OI 2.1.218 |
| [`w:pgNum`](#wpgnum) | `w:CT_Empty` | 0 | 0 | 2 | §17.3.3.22 p.356 |
| [`w:pgNumType`](#wpgnumtype) | `w:CT_PageNumber` | 4 | 0 | 1 | §17.6.12 p.593, OI 2.1.219 |
| [`w:pgSz`](#wpgsz) | `w:CT_PageSz` | 4 | 0 | 1 | §17.6.13 p.595, OI 2.1.220 |
| [`w:pict`](#wpict) | `w:CT_Picture` | 0 | 4 | 3 | Part 4 §14.3.2.2 p.53, OI 2.1.1757; Part 4 §14.6.1 p.63, OI 2.1.1769 |
| [`w:picture`](#wpicture) | `w:CT_Empty` | 0 | 0 | 1 | §17.5.2.24 p.538 |
| [`w:pitch`](#wpitch) | `w:CT_Pitch` | 1 | 0 | 1 | §17.8.3.14 p.696, OI 2.1.273 |
| [`w:pixelsPerInch`](#wpixelsperinch) | `w:CT_DecimalNumber` | 1 | 0 | 1 | §17.15.2.34 p.1142, OI 2.1.443 |
| [`w:placeholder`](#wplaceholder) | `w:CT_Placeholder`, `w:CT_String` | 1 | 1 | 2 | §17.5.1.8 p.506; §17.5.2.25 p.538, OI 2.1.199 |
| [`w:pos`](#wpos) | `w:CT_FtnPos`, `w:CT_EdnPos` | 1 | 0 | 2 | §17.11.21 p.785, OI 2.1.309; §17.11.22 p.787 |
| [`w:position`](#wposition) | `w:CT_SignedHpsMeasure` | 1 | 0 | 1 | §17.3.2.24 p.299 |
| [`w:printBodyTextBeforeHeader`](#wprintbodytextbeforeheader) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.32 p.142 |
| [`w:printColBlack`](#wprintcolblack) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.33 p.143 |
| [`w:printFormsData`](#wprintformsdata) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.61 p.1058 |
| [`w:printFractionalCharacterWidth`](#wprintfractionalcharacterwidth) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.62 p.1060, OI 2.1.412 |
| [`w:printPostScriptOverText`](#wprintpostscriptovertext) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.63 p.1060, OI 2.1.413 |
| [`w:printTwoOnOne`](#wprinttwoonone) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.64 p.1060 |
| [`w:printerSettings`](#wprintersettings) | `w:CT_Rel` | 1 | 0 | 1 | §17.6.14 p.597, OI 2.1.221 |
| [`w:proofErr`](#wprooferr) | `w:CT_ProofErr` | 1 | 0 | 36 | §17.13.8.1 p.937 |
| [`w:proofState`](#wproofstate) | `w:CT_Proof` | 2 | 0 | 1 | §17.15.1.65 p.1062 |
| [`w:ptab`](#wptab) | `w:CT_PTab` | 3 | 0 | 2 | §17.3.3.23 p.357 |
| [`w:qFormat`](#wqformat) | `w:CT_OnOff` | 1 | 0 | 1 | §17.7.4.14 p.647 |
| [`w:query`](#wquery) | `w:CT_String` | 1 | 0 | 1 | §17.14.26 p.967, OI 2.1.377 |
| [`w:r`](#wr) | `w:CT_R` | 3 | 34 | 14 | §17.3.2.25 p.300, OI 2.1.87 |
| [`w:rFonts`](#wrfonts) | `w:CT_Fonts` | 9 | 0 | 1 | §17.3.2.26 p.301, OI 2.1.88 |
| [`w:rPr`](#wrpr) | `w:CT_RPr`, `w:CT_RPrOriginal`, `w:CT_ParaRPrOriginal`, `w:CT_ParaRPr` | 0 | 44 | 13 | §17.3.1.29 p.253, OI 2.1.57; §17.3.1.30 p.254, OI 2.1.58; §17.3.2.27 p.314, OI 2.1.89; §17.3.2.28 p.315, OI 2.1.90; §17.5.2.27 p.539, OI 2.1.200; §17.5.2.28 p.541, OI 2.1.201; §17.7.5.4 p.659, OI 2.1.245; §17.7.6.2 p.665, OI 2.1.248; §17.7.9.1 p.679, OI 2.1.260; §17.9.24 p.736, OI 2.1.291 |
| [`w:rPrChange`](#wrprchange) | `w:CT_RPrChange`, `w:CT_ParaRPrChange` | 3 | 1 | 1 | §17.13.5.30 p.901, OI 2.1.346; §17.13.5.31 p.903, OI 2.1.347 |
| [`w:rPrDefault`](#wrprdefault) | `w:CT_RPrDefault` | 0 | 1 | 1 | §17.7.5.5 p.660 |
| [`w:rStyle`](#wrstyle) | `w:CT_String` | 1 | 0 | 1 | §17.3.2.29 p.316, OI 2.1.91 |
| [`w:readModeInkLockDown`](#wreadmodeinklockdown) | `w:CT_ReadingModeInkLockDown` | 4 | 0 | 1 | §17.15.1.66 p.1064 |
| [`w:recipientData`](#wrecipientdata) | `w:CT_RecipientData`, `w:CT_Rel` | 1 | 3 | 2 | §17.14.27 p.968; §17.14.28 p.969, OI 2.1.378 |
| [`w:recipients`](#wrecipients) ◆ | `w:CT_Recipients` | 0 | 1 | 0 | §17.14.29 p.971 |
| [`w:relyOnVML`](#wrelyonvml) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.4.1 p.176 |
| [`w:removeDateAndTime`](#wremovedateandtime) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.67 p.1066 |
| [`w:removePersonalInformation`](#wremovepersonalinformation) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.68 p.1067 |
| [`w:result`](#wresult) | `w:CT_DecimalNumber` | 1 | 0 | 1 | §17.16.28 p.1295 |
| [`w:revisionView`](#wrevisionview) | `w:CT_TrackChangesView` | 5 | 0 | 1 | §17.15.1.69 p.1067, OI 2.1.414 |
| [`w:richText`](#wrichtext) | `w:CT_Empty` | 0 | 0 | 1 | §17.5.2.26 p.539 |
| [`w:right`](#wright) | `w:CT_Border`, `w:CT_PageBorder`, `w:CT_TblWidth` | 12 | 0 | 7 | §17.3.1.28 p.252, OI 2.1.56; §17.6.15 p.598, OI 2.1.222; §17.15.2.35 p.1143, OI 2.1.444; Part 4 §14.4.5 p.55, OI 2.1.1762; Part 4 §14.4.6 p.55, OI 2.1.1763; Part 4 §14.4.7 p.55, OI 2.1.1764; Part 4 §14.4.8 p.55, OI 2.1.1765 |
| [`w:rsid`](#wrsid) | `w:CT_LongHexNumber` | 1 | 0 | 2 | §17.7.4.15 p.648; §17.15.1.70 p.1069, OI 2.1.415 |
| [`w:rsidRoot`](#wrsidroot) | `w:CT_LongHexNumber` | 1 | 0 | 1 | §17.15.1.71 p.1070 |
| [`w:rsids`](#wrsids) | `w:CT_DocRsids` | 0 | 2 | 1 | §17.15.1.72 p.1071 |
| [`w:rt`](#wrt) | `w:CT_RubyContent` | 0 | 26 | 1 | §17.3.3.24 p.358, OI 2.1.112 |
| [`w:rtl`](#wrtl) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.2.30 p.317, OI 2.1.92 |
| [`w:rtlGutter`](#wrtlgutter) | `w:CT_OnOff` | 1 | 0 | 1 | §17.6.16 p.605 |
| [`w:ruby`](#wruby) | `w:CT_Ruby` | 0 | 3 | 2 | §17.3.3.25 p.359 |
| [`w:rubyAlign`](#wrubyalign) | `w:CT_RubyAlign` | 1 | 0 | 1 | §17.3.3.26 p.360 |
| [`w:rubyBase`](#wrubybase) | `w:CT_RubyContent` | 0 | 26 | 1 | §17.3.3.27 p.361, OI 2.1.113 |
| [`w:rubyPr`](#wrubypr) | `w:CT_RubyPr` | 0 | 6 | 1 | §17.3.3.28 p.362 |
| [`w:saveFormsData`](#wsaveformsdata) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.73 p.1072 |
| [`w:saveInvalidXml`](#wsaveinvalidxml) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.74 p.1073, OI 2.1.416 |
| [`w:savePreviewPicture`](#wsavepreviewpicture) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.75 p.1073 |
| [`w:saveSmartTagsAsXml`](#wsavesmarttagsasxml) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.2.36 p.1144 |
| [`w:saveSubsetFonts`](#wsavesubsetfonts) | `w:CT_OnOff` | 1 | 0 | 1 | §17.8.3.15 p.697 |
| [`w:saveThroughXslt`](#wsavethroughxslt) | `w:CT_SaveThroughXslt` | 2 | 0 | 1 | §17.15.1.76 p.1074, OI 2.1.417 |
| [`w:saveXmlDataOnly`](#wsavexmldataonly) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.77 p.1075 |
| [`w:scrollbar`](#wscrollbar) | `w:CT_FrameScrollbar` | 1 | 0 | 1 | §17.15.2.37 p.1145 |
| [`w:sdt`](#wsdt) | `w:CT_SdtRun`, `w:CT_SdtBlock`, `w:CT_SdtRow`, `w:CT_SdtCell` | 0 | 3 | 34 | §17.5.2.29 p.541; §17.5.2.30 p.542, OI 2.1.202; §17.5.2.31 p.543, OI 2.1.203; §17.5.2.32 p.544, OI 2.1.204 |
| [`w:sdtContent`](#wsdtcontent) | `w:CT_SdtContentBlock`, `w:CT_SdtContentRun`, `w:CT_SdtContentCell`, `w:CT_SdtContentRow` | 0 | 38 | 1 | §17.5.2.33 p.545, OI 2.1.205; §17.5.2.34 p.546, OI 2.1.206; §17.5.2.35 p.547, OI 2.1.207; §17.5.2.36 p.548, OI 2.1.208 |
| [`w:sdtEndPr`](#wsdtendpr) | `w:CT_SdtEndPr` | 0 | 1 | 1 | §17.5.2.37 p.549 |
| [`w:sdtPr`](#wsdtpr) | `w:CT_SdtPr` | 0 | 23 | 1 | §17.5.2.38 p.549 |
| [`w:sectPr`](#wsectpr) | `w:CT_SectPrBase`, `w:CT_SectPr` | 4 | 22 | 4 | §17.6.17 p.607, OI 2.1.223; §17.6.18 p.608, OI 2.1.224; §17.6.19 p.610, OI 2.1.225 |
| [`w:sectPrChange`](#wsectprchange) | `w:CT_SectPrChange` | 3 | 1 | 1 | §17.13.5.32 p.905, OI 2.1.348 |
| [`w:selectFldWithFirstOrLastChar`](#wselectfldwithfirstorlastchar) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.34 p.143 |
| [`w:semiHidden`](#wsemihidden) | `w:CT_OnOff` | 1 | 0 | 1 | §17.7.4.16 p.649 |
| [`w:separator`](#wseparator) | `w:CT_Empty` | 0 | 0 | 2 | §17.11.23 p.788 |
| [`w:settings`](#wsettings) ◆ | `w:CT_Settings` | 0 | 98 | 0 | §17.15.1.78 p.1076 |
| [`w:shadow`](#wshadow) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.2.31 p.319, OI 2.1.93 |
| [`w:shapeDefaults`](#wshapedefaults) | `w:CT_ShapeDefaults` | 0 | 1 | 1 | Part 4 §14.8.2.2 p.83, OI 2.1.1774 |
| [`w:shapeLayoutLikeWW8`](#wshapelayoutlikeww8) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.35 p.144 |
| [`w:shd`](#wshd) | `w:CT_Shd` | 9 | 0 | 5 | §17.3.1.31 p.255, OI 2.1.59; §17.3.2.32 p.319, OI 2.1.94; §17.4.30 p.425, OI 2.1.142; §17.4.31 p.426, OI 2.1.143; §17.4.32 p.427, OI 2.1.144 |
| [`w:showBreaksInFrames`](#wshowbreaksinframes) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.36 p.147 |
| [`w:showEnvelope`](#wshowenvelope) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.79 p.1077 |
| [`w:showXMLTags`](#wshowxmltags) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.80 p.1077, OI 2.1.418 |
| [`w:showingPlcHdr`](#wshowingplchdr) | `w:CT_OnOff` | 1 | 0 | 1 | §17.5.2.39 p.549 |
| [`w:sig`](#wsig) | `w:CT_FontSig` | 6 | 0 | 1 | §17.8.3.16 p.697, OI 2.1.274 |
| [`w:size`](#wsize) | `w:CT_HpsMeasure` | 1 | 0 | 1 | §17.16.29 p.1297 |
| [`w:sizeAuto`](#wsizeauto) | `w:CT_OnOff` | 1 | 0 | 1 | §17.16.30 p.1297 |
| [`w:smallCaps`](#wsmallcaps) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.2.33 p.320, OI 2.1.95 |
| [`w:smartTag`](#wsmarttag) | `w:CT_SmartTagRun` | 2 | 35 | 21 | §17.5.1.9 p.507, OI 2.1.192 |
| [`w:smartTagPr`](#wsmarttagpr) | `w:CT_SmartTagPr` | 0 | 1 | 1 | §17.5.1.10 p.509 |
| [`w:smartTagType`](#wsmarttagtype) | `w:CT_SmartTagType` | 3 | 0 | 1 | §17.15.1.81 p.1077, OI 2.1.419 |
| [`w:snapToGrid`](#wsnaptogrid) | `w:CT_OnOff` | 1 | 0 | 2 | §17.3.1.32 p.255; §17.3.2.34 p.321 |
| [`w:softHyphen`](#wsofthyphen) | `w:CT_Empty` | 0 | 0 | 2 | §17.3.3.29 p.362 |
| [`w:sourceFileName`](#wsourcefilename) | `w:CT_Rel` | 1 | 0 | 1 | §17.15.2.38 p.1147, OI 2.1.445 |
| [`w:spaceForUL`](#wspaceforul) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.3.7 p.1165 |
| [`w:spacing`](#wspacing) | `w:CT_Spacing`, `w:CT_SignedTwipsMeasure` | 9 | 0 | 2 | §17.3.1.33 p.256, OI 2.1.60; §17.3.2.35 p.321, OI 2.1.96 |
| [`w:spacingInWholePoints`](#wspacinginwholepoints) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.37 p.149 |
| [`w:specVanish`](#wspecvanish) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.2.36 p.322, OI 2.1.97 |
| [`w:splitPgBreakAndParaMark`](#wsplitpgbreakandparamark) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.38 p.151 |
| [`w:src`](#wsrc) | `w:CT_Rel` | 1 | 0 | 1 | §17.14.30 p.972, OI 2.1.379 |
| [`w:start`](#wstart) | `w:CT_Border`, `w:CT_TblWidth`, `w:CT_DecimalNumber` | 11 | 0 | 5 | §17.4.33 p.427, OI 2.1.145; §17.4.34 p.428, OI 2.1.146; §17.4.35 p.429, OI 2.1.147; §17.4.36 p.430, OI 2.1.148; §17.9.25 p.737, OI 2.1.292 |
| [`w:startOverride`](#wstartoverride) | `w:CT_DecimalNumber` | 1 | 0 | 1 | §17.9.26 p.738 |
| [`w:statusText`](#wstatustext) | `w:CT_FFStatusText` | 2 | 0 | 1 | §17.16.31 p.1298 |
| [`w:storeMappedDataAs`](#wstoremappeddataas) | `w:CT_SdtDateMappingType` | 1 | 0 | 1 | §17.5.2.40 p.550 |
| [`w:strictFirstAndLastChars`](#wstrictfirstandlastchars) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.82 p.1079, OI 2.1.420 |
| [`w:strike`](#wstrike) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.2.37 p.323 |
| [`w:style`](#wstyle) | `w:CT_Style`, `w:CT_String` | 5 | 22 | 2 | §17.7.4.17 p.650, OI 2.1.242; §17.12.14 p.804 |
| [`w:styleLink`](#wstylelink) | `w:CT_String` | 1 | 0 | 1 | §17.9.27 p.740, OI 2.1.293 |
| [`w:styleLockQFSet`](#wstylelockqfset) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.83 p.1079 |
| [`w:styleLockTheme`](#wstylelocktheme) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.84 p.1080 |
| [`w:stylePaneFormatFilter`](#wstylepaneformatfilter) | `w:CT_StylePaneFilter` | 16 | 0 | 1 | §17.15.1.85 p.1080, OI 2.1.421 |
| [`w:stylePaneSortMethod`](#wstylepanesortmethod) | `w:CT_StyleSort` | 1 | 0 | 1 | §17.15.1.86 p.1082 |
| [`w:styles`](#wstyles) ◆ | `w:CT_Styles` | 0 | 3 | 0 | §17.7.4.18 p.655 |
| [`w:subDoc`](#wsubdoc) | `w:CT_Rel` | 1 | 0 | 8 | §17.17.1.1 p.1304, OI 2.1.526 |
| [`w:subFontBySize`](#wsubfontbysize) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.39 p.152 |
| [`w:suff`](#wsuff) | `w:CT_LevelSuffix` | 1 | 0 | 1 | §17.9.28 p.742 |
| [`w:summaryLength`](#wsummarylength) | `w:CT_DecimalNumberOrPrecent` | 1 | 0 | 1 | §17.15.1.87 p.1083, OI 2.1.422 |
| [`w:suppressAutoHyphens`](#wsuppressautohyphens) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.1.34 p.261 |
| [`w:suppressBottomSpacing`](#wsuppressbottomspacing) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.40 p.153 |
| [`w:suppressLineNumbers`](#wsuppresslinenumbers) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.1.35 p.262 |
| [`w:suppressOverlap`](#wsuppressoverlap) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.1.36 p.263 |
| [`w:suppressSpBfAfterPgBrk`](#wsuppressspbfafterpgbrk) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.42 p.157 |
| [`w:suppressSpacingAtTopOfPage`](#wsuppressspacingattopofpage) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.41 p.155 |
| [`w:suppressTopSpacing`](#wsuppresstopspacing) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.43 p.159 |
| [`w:suppressTopSpacingWP`](#wsuppresstopspacingwp) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.44 p.160 |
| [`w:swapBordersFacingPages`](#wswapbordersfacingpages) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.45 p.160 |
| [`w:sym`](#wsym) | `w:CT_Sym` | 2 | 0 | 2 | §17.3.3.30 p.363, OI 2.1.114 |
| [`w:sz`](#wsz) | `w:CT_HpsMeasure`, `w:CT_String` | 1 | 0 | 3 | §17.3.2.38 p.323, OI 2.1.98; §17.15.2.39 p.1149, OI 2.1.446; §17.15.2.40 p.1151, OI 2.1.447 |
| [`w:szCs`](#wszcs) | `w:CT_HpsMeasure` | 1 | 0 | 1 | §17.3.2.39 p.324, OI 2.1.99 |
| [`w:t`](#wt) | `w:CT_Text` | 1 | 0 | 2 | §17.3.3.31 p.365 |
| [`w:tab`](#wtab) | `w:CT_Empty`, `w:CT_TabStop` | 3 | 0 | 3 | §17.3.1.37 p.263, OI 2.1.61; §17.3.3.32 p.366 |
| [`w:tabIndex`](#wtabindex) | `w:CT_UnsignedDecimalNumber` | 1 | 0 | 2 | §17.5.2.41 p.552, OI 2.1.209; §17.16.32 p.1300, OI 2.1.525 |
| [`w:table`](#wtable) | `w:CT_String` | 1 | 0 | 1 | §17.14.31 p.973, OI 2.1.380 |
| [`w:tabs`](#wtabs) | `w:CT_Tabs` | 0 | 1 | 1 | §17.3.1.38 p.265 |
| [`w:tag`](#wtag) | `w:CT_String` | 1 | 0 | 1 | §17.5.2.42 p.553 |
| [`w:targetScreenSz`](#wtargetscreensz) | `w:CT_TargetScreenSz` | 1 | 0 | 1 | §17.15.2.41 p.1153, OI 2.1.448 |
| [`w:tbl`](#wtbl) | `w:CT_Tbl` | 0 | 30 | 13 | §17.4.37 p.431, OI 2.1.149 |
| [`w:tblBorders`](#wtblborders) | `w:CT_TblBorders` | 0 | 8 | 2 | §17.4.38 p.432, OI 2.1.150; §17.4.39 p.433, OI 2.1.151 |
| [`w:tblCaption`](#wtblcaption) | `w:CT_String` | 1 | 0 | 1 | §17.4.40 p.434 |
| [`w:tblCellMar`](#wtblcellmar) | `w:CT_TblCellMar` | 0 | 6 | 2 | §17.4.41 p.435; §17.4.42 p.436 |
| [`w:tblCellSpacing`](#wtblcellspacing) | `w:CT_TblWidth` | 2 | 0 | 3 | §17.4.43 p.437, OI 2.1.152; §17.4.44 p.438, OI 2.1.153; §17.4.45 p.439, OI 2.1.154 |
| [`w:tblDescription`](#wtbldescription) | `w:CT_String` | 1 | 0 | 1 | §17.4.46 p.440 |
| [`w:tblGrid`](#wtblgrid) | `w:CT_TblGridBase`, `w:CT_TblGrid` | 0 | 2 | 2 | §17.4.47 p.441; §17.4.48 p.442 |
| [`w:tblGridChange`](#wtblgridchange) | `w:CT_TblGridChange` | 1 | 1 | 1 | §17.13.5.33 p.907, OI 2.1.349 |
| [`w:tblHeader`](#wtblheader) | `w:CT_OnOff` | 1 | 0 | 1 | §17.4.49 p.443 |
| [`w:tblInd`](#wtblind) | `w:CT_TblWidth` | 2 | 0 | 2 | §17.4.50 p.444, OI 2.1.155; §17.4.51 p.445, OI 2.1.156 |
| [`w:tblLayout`](#wtbllayout) | `w:CT_TblLayoutType` | 1 | 0 | 2 | §17.4.52 p.446, OI 2.1.157; §17.4.53 p.447, OI 2.1.158 |
| [`w:tblLook`](#wtbllook) | `w:CT_TblLook` | 7 | 0 | 2 | §17.4.54 p.448, OI 2.1.159; §17.4.55 p.449, OI 2.1.160 |
| [`w:tblOverlap`](#wtbloverlap) | `w:CT_TblOverlap` | 1 | 0 | 1 | §17.4.56 p.450, OI 2.1.161 |
| [`w:tblPr`](#wtblpr) | `w:CT_TblPrBase`, `w:CT_TblPr` | 0 | 18 | 4 | §17.4.58 p.458, OI 2.1.163; §17.4.59 p.459, OI 2.1.164; §17.7.6.3 p.666, OI 2.1.249; §17.7.6.4 p.666, OI 2.1.250 |
| [`w:tblPrChange`](#wtblprchange) | `w:CT_TblPrChange` | 3 | 1 | 1 | §17.13.5.34 p.908, OI 2.1.350 |
| [`w:tblPrEx`](#wtblprex) | `w:CT_TblPrExBase`, `w:CT_TblPrEx` | 0 | 10 | 2 | §17.4.60 p.460; §17.4.61 p.462 |
| [`w:tblPrExChange`](#wtblprexchange) | `w:CT_TblPrExChange` | 3 | 1 | 1 | §17.13.5.35 p.910, OI 2.1.351 |
| [`w:tblStyle`](#wtblstyle) | `w:CT_String` | 1 | 0 | 1 | §17.4.62 p.464, OI 2.1.165 |
| [`w:tblStyleColBandSize`](#wtblstylecolbandsize) | `w:CT_DecimalNumber` | 1 | 0 | 1 | §17.7.6.5 p.667, OI 2.1.251 |
| [`w:tblStylePr`](#wtblstylepr) | `w:CT_TblStylePr` | 1 | 5 | 1 | §17.7.6.6 p.668, OI 2.1.252 |
| [`w:tblStyleRowBandSize`](#wtblstylerowbandsize) | `w:CT_DecimalNumber` | 1 | 0 | 1 | §17.7.6.7 p.670, OI 2.1.253 |
| [`w:tblW`](#wtblw) | `w:CT_TblWidth` | 2 | 0 | 2 | §17.4.63 p.465, OI 2.1.166; §17.4.64 p.466, OI 2.1.167 |
| [`w:tblpPr`](#wtblppr) | `w:CT_TblPPr` | 10 | 0 | 1 | §17.4.57 p.452, OI 2.1.162 |
| [`w:tc`](#wtc) | `w:CT_Tc` | 1 | 31 | 3 | §17.4.65 p.467, OI 2.1.168 |
| [`w:tcBorders`](#wtcborders) | `w:CT_TcBorders` | 0 | 10 | 1 | §17.4.66 p.469, OI 2.1.169 |
| [`w:tcFitText`](#wtcfittext) | `w:CT_OnOff` | 1 | 0 | 1 | §17.4.67 p.472, OI 2.1.170 |
| [`w:tcMar`](#wtcmar) | `w:CT_TcMar` | 0 | 6 | 1 | §17.4.68 p.472 |
| [`w:tcPr`](#wtcpr) | `w:CT_TcPrInner`, `w:CT_TcPr` | 0 | 18 | 4 | §17.4.69 p.473; §17.4.70 p.474; §17.7.6.8 p.671, OI 2.1.254; §17.7.6.9 p.672, OI 2.1.255 |
| [`w:tcPrChange`](#wtcprchange) | `w:CT_TcPrChange` | 3 | 1 | 1 | §17.13.5.36 p.912, OI 2.1.352 |
| [`w:tcW`](#wtcw) | `w:CT_TblWidth` | 2 | 0 | 1 | §17.4.71 p.475, OI 2.1.171 |
| [`w:temporary`](#wtemporary) | `w:CT_OnOff` | 1 | 0 | 1 | §17.5.2.43 p.554, OI 2.1.210 |
| [`w:text`](#wtext) | `w:CT_SdtText` | 1 | 0 | 1 | §17.5.2.44 p.555 |
| [`w:textAlignment`](#wtextalignment) | `w:CT_TextAlignment` | 1 | 0 | 1 | §17.3.1.39 p.265 |
| [`w:textDirection`](#wtextdirection) | `w:CT_TextDirection` | 1 | 0 | 3 | §17.3.1.41 p.268, OI 2.1.63; §17.4.72 p.477, OI 2.1.172; §17.6.20 p.612 |
| [`w:textInput`](#wtextinput) | `w:CT_FFTextInput` | 0 | 4 | 1 | §17.16.33 p.1301 |
| [`w:textboxTightWrap`](#wtextboxtightwrap) | `w:CT_TextboxTightWrap` | 1 | 0 | 1 | §17.3.1.40 p.267, OI 2.1.62 |
| [`w:themeFontLang`](#wthemefontlang) | `w:CT_Language` | 3 | 0 | 1 | §17.15.1.88 p.1084, OI 2.1.423 |
| [`w:title`](#wtitle) | `w:CT_String` | 1 | 0 | 2 | §17.15.2.42 p.1153, OI 2.1.449 |
| [`w:titlePg`](#wtitlepg) | `w:CT_OnOff` | 1 | 0 | 1 | §17.10.6 p.755, OI 2.1.299 |
| [`w:tl2br`](#wtl2br) | `w:CT_Border` | 9 | 0 | 1 | §17.4.73 p.478, OI 2.1.173 |
| [`w:tmpl`](#wtmpl) | `w:CT_LongHexNumber` | 1 | 0 | 1 | §17.9.29 p.743 |
| [`w:top`](#wtop) | `w:CT_Border`, `w:CT_TopPageBorder`, `w:CT_TblWidth` | 14 | 0 | 7 | §17.3.1.42 p.269, OI 2.1.64; §17.4.74 p.478, OI 2.1.174; §17.4.75 p.479, OI 2.1.175; §17.4.76 p.480, OI 2.1.176; §17.4.77 p.481, OI 2.1.177; §17.6.21 p.612, OI 2.1.226; §17.15.2.43 p.1155, OI 2.1.450 |
| [`w:topLinePunct`](#wtoplinepunct) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.1.43 p.271, OI 2.1.65 |
| [`w:tr`](#wtr) | `w:CT_Row` | 4 | 30 | 3 | §17.4.78 p.481, OI 2.1.178 |
| [`w:tr2bl`](#wtr2bl) | `w:CT_Border` | 9 | 0 | 1 | §17.4.79 p.483, OI 2.1.179 |
| [`w:trHeight`](#wtrheight) | `w:CT_Height` | 2 | 0 | 1 | §17.4.80 p.484, OI 2.1.180 |
| [`w:trPr`](#wtrpr) | `w:CT_TrPrBase`, `w:CT_TrPr` | 0 | 15 | 4 | §17.4.81 p.486; §17.4.82 p.487; §17.7.6.10 p.672, OI 2.1.256; §17.7.6.11 p.673, OI 2.1.257 |
| [`w:trPrChange`](#wtrprchange) | `w:CT_TrPrChange` | 3 | 1 | 1 | §17.13.5.37 p.914, OI 2.1.353 |
| [`w:trackRevisions`](#wtrackrevisions) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.89 p.1086 |
| [`w:truncateFontHeightsLikeWP6`](#wtruncatefontheightslikewp6) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.46 p.163 |
| [`w:txbxContent`](#wtxbxcontent) ◆ | `w:CT_TxbxContent` | 0 | 30 | 2 | Part 4 §14.9.1.1 p.177, OI 2.1.1779 |
| [`w:type`](#wtype) | `w:CT_FFTextType`, `w:CT_SectType`, `w:CT_MailMergeOdsoFMDFieldType`, `w:CT_MailMergeSourceType`, `w:CT_DocPartType` | 1 | 0 | 5 | §17.6.22 p.621, OI 2.1.227; §17.12.15 p.805; §17.14.32 p.975; §17.14.33 p.975; §17.16.34 p.1301 |
| [`w:types`](#wtypes) | `w:CT_DocPartTypes` | 1 | 1 | 1 | §17.12.16 p.806 |
| [`w:u`](#wu) | `w:CT_Underline` | 5 | 0 | 1 | §17.3.2.40 p.325, OI 2.1.100 |
| [`w:udl`](#wudl) | `w:CT_String` | 1 | 0 | 1 | §17.14.34 p.976, OI 2.1.381 |
| [`w:uiPriority`](#wuipriority) | `w:CT_DecimalNumber` | 1 | 0 | 1 | §17.7.4.19 p.655, OI 2.1.243 |
| [`w:ulTrailSpace`](#wultrailspace) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.3.8 p.1166 |
| [`w:underlineTabInNumList`](#wunderlinetabinnumlist) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.47 p.164 |
| [`w:unhideWhenUsed`](#wunhidewhenused) | `w:CT_OnOff` | 1 | 0 | 1 | §17.7.4.20 p.656 |
| [`w:uniqueTag`](#wuniquetag) | `w:CT_Base64Binary` | 1 | 0 | 1 | §17.14.35 p.978, OI 2.1.382 |
| [`w:updateFields`](#wupdatefields) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.90 p.1088, OI 2.1.424 |
| [`w:useAltKinsokuLineBreakRules`](#wusealtkinsokulinebreakrules) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.48 p.165 |
| [`w:useAnsiKerningPairs`](#wuseansikerningpairs) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.49 p.166 |
| [`w:useFELayout`](#wusefelayout) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.50 p.166 |
| [`w:useNormalStyleForList`](#wusenormalstyleforlist) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.51 p.167 |
| [`w:usePrinterMetrics`](#wuseprintermetrics) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.52 p.168 |
| [`w:useSingleBorderforContiguousCells`](#wusesingleborderforcontiguouscells) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.53 p.169 |
| [`w:useWord2002TableStyleRules`](#wuseword2002tablestylerules) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.54 p.170 |
| [`w:useWord97LineBreakRules`](#wuseword97linebreakrules) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.55 p.172 |
| [`w:useXSLTWhenSaving`](#wusexsltwhensaving) | `w:CT_OnOff` | 1 | 0 | 1 | §17.15.1.91 p.1089 |
| [`w:vAlign`](#wvalign) | `w:CT_VerticalJc` | 1 | 0 | 2 | §17.4.83 p.488, OI 2.1.181; §17.6.23 p.622 |
| [`w:vMerge`](#wvmerge) | `w:CT_VMerge` | 1 | 0 | 1 | §17.4.84 p.489, OI 2.1.182 |
| [`w:vanish`](#wvanish) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.2.41 p.329 |
| [`w:vertAlign`](#wvertalign) | `w:CT_VerticalAlignRun` | 1 | 0 | 1 | §17.3.2.42 p.330 |
| [`w:view`](#wview) | `w:CT_View` | 1 | 0 | 1 | §17.15.1.92 p.1089, OI 2.1.425 |
| [`w:viewMergedData`](#wviewmergeddata) | `w:CT_OnOff` | 1 | 0 | 1 | §17.14.36 p.978 |
| [`w:w`](#ww) | `w:CT_TextScale`, `w:CT_TwipsMeasure` | 1 | 0 | 2 | §17.3.2.43 p.331; §17.15.2.44 p.1157 |
| [`w:wAfter`](#wwafter) | `w:CT_TblWidth` | 2 | 0 | 1 | §17.4.85 p.491, OI 2.1.183 |
| [`w:wBefore`](#wwbefore) | `w:CT_TblWidth` | 2 | 0 | 1 | §17.4.86 p.492, OI 2.1.184 |
| [`w:webHidden`](#wwebhidden) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.2.44 p.332, OI 2.1.101 |
| [`w:webSettings`](#wwebsettings) ◆ | `w:CT_WebSettings` | 0 | 13 | 0 | §17.15.2.45 p.1158 |
| [`w:widowControl`](#wwidowcontrol) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.1.44 p.271, OI 2.1.66 |
| [`w:wordWrap`](#wwordwrap) | `w:CT_OnOff` | 1 | 0 | 1 | §17.3.1.45 p.272, OI 2.1.67 |
| [`w:wpJustification`](#wwpjustification) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.56 p.174 |
| [`w:wpSpaceWidth`](#wwpspacewidth) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.57 p.175 |
| [`w:wrapTrailSpaces`](#wwraptrailspaces) | `w:CT_OnOff` | 1 | 0 | 1 | Part 4 §14.8.3.58 p.175 |
| [`w:writeProtection`](#wwriteprotection) | `w:CT_WriteProtection` | 17 | 0 | 1 | §17.15.1.93 p.1090, OI 2.1.426 |
| [`w:yearLong`](#wyearlong) | `w:CT_Empty` | 0 | 0 | 2 | §17.3.3.33 p.366 |
| [`w:yearShort`](#wyearshort) | `w:CT_Empty` | 0 | 0 | 2 | §17.3.3.34 p.367 |
| [`w:zoom`](#wzoom) | `w:CT_Zoom` | 2 | 0 | 1 | §17.15.1.94 p.1094, OI 2.1.427 |

◆ declared globally (can be the root element of a part)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `w:ST_AnnotationVMerge` | restriction of xsd:string | `cont`, `rest` | §17.18.1 p.1311 |
| `w:ST_Border` | restriction of xsd:string | `nil`, `none`, `single`, `thick`, `double`, `dotted`, `dashed`, `dotDash`, `dotDotDash`, `triple`, `thinThickSmallGap`, `thickThinSmallGap` … (+181) | §17.18.2 p.1312, OI 2.1.528 |
| `w:ST_BrClear` | restriction of xsd:string | `none`, `left`, `right`, `all` | §17.18.3 p.1365, OI 2.1.529 |
| `w:ST_BrType` | restriction of xsd:string | `page`, `column`, `textWrapping` | §17.18.4 p.1367 |
| `w:ST_CaptionPos` | restriction of xsd:string | `above`, `below`, `left`, `right` | §17.18.5 p.1368, OI 2.1.530 |
| `w:ST_ChapterSep` | restriction of xsd:string | `hyphen`, `period`, `colon`, `emDash`, `enDash` | §17.18.6 p.1370 |
| `w:ST_CharacterSpacing` | restriction of xsd:string | `doNotCompress`, `compressPunctuation`, `compressPunctuationAndJapaneseKana` | §17.18.7 p.1371, OI 2.1.531 |
| `w:ST_Cnf` | restriction of xsd:string | length `12`, pattern `[01]*` | Part 4 §14.11.9 p.201 |
| `w:ST_CombineBrackets` | restriction of xsd:string | `none`, `round`, `square`, `angle`, `curly` | §17.18.8 p.1371 |
| `w:ST_DateTime` | restriction of xsd:dateTime |  | §17.18.9 p.1372 |
| `w:ST_DecimalNumber` | restriction of xsd:integer |  | §17.18.10 p.1373, OI 2.1.532 |
| `w:ST_DecimalNumberOrPercent` | union of w:ST_UnqualifiedPercentage, s:ST_Percentage |  | §17.18.11 p.1373 |
| `w:ST_Direction` | restriction of xsd:string | `ltr`, `rtl` | §17.18.12 p.1374 |
| `w:ST_DisplacedByCustomXml` | restriction of xsd:string | `next`, `prev` | §17.18.13 p.1374, OI 2.1.533 |
| `w:ST_DocGrid` | restriction of xsd:string | `default`, `lines`, `linesAndChars`, `snapToChars` | §17.18.14 p.1375, OI 2.1.534 |
| `w:ST_DocPartBehavior` | restriction of xsd:string | `content`, `p`, `pg` | §17.18.15 p.1377, OI 2.1.535 |
| `w:ST_DocPartGallery` | restriction of xsd:string | `placeholder`, `any`, `default`, `docParts`, `coverPg`, `eq`, `ftrs`, `hdrs`, `pgNum`, `tbls`, `watermarks`, `autoTxt` … (+26) | §17.18.16 p.1378 |
| `w:ST_DocPartType` | restriction of xsd:string | `none`, `normal`, `autoExp`, `toolbar`, `speller`, `formFld`, `bbPlcHdr` | §17.18.17 p.1381 |
| `w:ST_DocProtect` | restriction of xsd:string | `none`, `readOnly`, `comments`, `trackedChanges`, `forms` | §17.18.18 p.1383 |
| `w:ST_DocType` | restriction of xsd:string |  | §17.18.19 p.1384 |
| `w:ST_DropCap` | restriction of xsd:string | `none`, `drop`, `margin` | §17.18.20 p.1384 |
| `w:ST_EdGrp` | restriction of xsd:string | `none`, `everyone`, `administrators`, `contributors`, `editors`, `owners`, `current` | §17.18.21 p.1385 |
| `w:ST_EdnPos` | restriction of xsd:string | `sectEnd`, `docEnd` | §17.18.22 p.1386 |
| `w:ST_EighthPointMeasure` | restriction of s:ST_UnsignedDecimalNumber |  | §17.18.23 p.1387 |
| `w:ST_Em` | restriction of xsd:string | `none`, `dot`, `comma`, `circle`, `underDot` | §17.18.24 p.1387, OI 2.1.536 |
| `w:ST_FFHelpTextVal` | restriction of xsd:string | maxLength `256` | §17.18.25 p.1389, OI 2.1.537 |
| `w:ST_FFName` | restriction of xsd:string | maxLength `65` | §17.18.26 p.1390, OI 2.1.538 |
| `w:ST_FFStatusTextVal` | restriction of xsd:string | maxLength `140` | §17.18.27 p.1391, OI 2.1.539 |
| `w:ST_FFTextType` | restriction of xsd:string | `regular`, `number`, `date`, `currentTime`, `currentDate`, `calculated` | §17.18.28 p.1391 |
| `w:ST_FldCharType` | restriction of xsd:string | `begin`, `separate`, `end` | §17.18.29 p.1392 |
| `w:ST_FontFamily` | restriction of xsd:string | `decorative`, `modern`, `roman`, `script`, `swiss`, `auto` | §17.18.30 p.1393 |
| `w:ST_FrameLayout` | restriction of xsd:string | `rows`, `cols`, `none` | §17.18.31 p.1394, OI 2.1.540 |
| `w:ST_FrameScrollbar` | restriction of xsd:string | `on`, `off`, `auto` | §17.18.32 p.1395 |
| `w:ST_FtnEdn` | restriction of xsd:string | `normal`, `separator`, `continuationSeparator`, `continuationNotice` | §17.18.33 p.1397 |
| `w:ST_FtnPos` | restriction of xsd:string | `pageBottom`, `beneathText`, `sectEnd`, `docEnd` | §17.18.34 p.1398, OI 2.1.541 |
| `w:ST_HAnchor` | restriction of xsd:string | `text`, `margin`, `page` | §17.18.35 p.1399 |
| `w:ST_HdrFtr` | restriction of xsd:string | `even`, `default`, `first` | §17.18.36 p.1400, OI 2.1.542 |
| `w:ST_HeightRule` | restriction of xsd:string | `auto`, `exact`, `atLeast` | §17.18.37 p.1401 |
| `w:ST_HexColor` | union of w:ST_HexColorAuto, s:ST_HexColorRGB |  | §17.18.38 p.1402 |
| `w:ST_HexColorAuto` | restriction of xsd:string | `auto` | §17.18.39 p.1402 |
| `w:ST_HighlightColor` | restriction of xsd:string | `black`, `blue`, `cyan`, `green`, `magenta`, `red`, `yellow`, `white`, `darkBlue`, `darkCyan`, `darkGreen`, `darkMagenta` … (+5) | §17.18.40 p.1403 |
| `w:ST_Hint` | restriction of xsd:string | `default`, `eastAsia` | §17.18.41 p.1405, OI 2.1.543 |
| `w:ST_HpsMeasure` | union of s:ST_UnsignedDecimalNumber, s:ST_PositiveUniversalMeasure |  | §17.18.42 p.1406, OI 2.1.544 |
| `w:ST_InfoTextType` | restriction of xsd:string | `text`, `autoText` | §17.18.43 p.1407 |
| `w:ST_Jc` | restriction of xsd:string | `start`, `center`, `end`, `both`, `mediumKashida`, `distribute`, `numTab`, `highKashida`, `lowKashida`, `thaiDistribute`, `left`, `right` | §17.18.44 p.1407, OI 2.1.545 |
| `w:ST_JcTable` | restriction of xsd:string | `center`, `end`, `left`, `right`, `start` | §17.18.45 p.1410 |
| `w:ST_LevelSuffix` | restriction of xsd:string | `tab`, `space`, `nothing` | §17.18.46 p.1411 |
| `w:ST_LineNumberRestart` | restriction of xsd:string | `newPage`, `newSection`, `continuous` | §17.18.47 p.1412 |
| `w:ST_LineSpacingRule` | restriction of xsd:string | `auto`, `exact`, `atLeast` | §17.18.48 p.1413 |
| `w:ST_Lock` | restriction of xsd:string | `sdtLocked`, `contentLocked`, `unlocked`, `sdtContentLocked` | §17.18.49 p.1414 |
| `w:ST_LongHexNumber` | restriction of xsd:hexBinary | length `4` | §17.18.50 p.1415 |
| `w:ST_MacroName` | restriction of xsd:string | maxLength `33` | §17.18.51 p.1415 |
| `w:ST_MailMergeDataType` | restriction of xsd:string |  | §17.18.52 p.1416, OI 2.1.546 |
| `w:ST_MailMergeDest` | restriction of xsd:string | `newDocument`, `printer`, `email`, `fax` | §17.18.53 p.1417 |
| `w:ST_MailMergeDocType` | restriction of xsd:string | `catalog`, `envelopes`, `mailingLabels`, `formLetters`, `email`, `fax` | §17.18.54 p.1418 |
| `w:ST_MailMergeOdsoFMDFieldType` | restriction of xsd:string | `null`, `dbColumn` | §17.18.55 p.1419 |
| `w:ST_MailMergeSourceType` | restriction of xsd:string | `database`, `addressBook`, `document1`, `document2`, `text`, `email`, `native`, `legacy`, `master` | §17.18.56 p.1420 |
| `w:ST_MeasurementOrPercent` | union of w:ST_DecimalNumberOrPercent, s:ST_UniversalMeasure |  | §17.18.107 p.1532 |
| `w:ST_Merge` | restriction of xsd:string | `continue`, `restart` | §17.18.57 p.1421, OI 2.1.547 |
| `w:ST_MultiLevelType` | restriction of xsd:string | `singleLevel`, `multilevel`, `hybridMultilevel` | §17.18.58 p.1424 |
| `w:ST_NumberFormat` | restriction of xsd:string | `decimal`, `upperRoman`, `lowerRoman`, `upperLetter`, `lowerLetter`, `ordinal`, `cardinalText`, `ordinalText`, `hex`, `chicago`, `ideographDigital`, `japaneseCounting` … (+51) | §17.18.59 p.1424, OI 2.1.548 |
| `w:ST_ObjectDrawAspect` | restriction of xsd:string | `content`, `icon` | §17.18.60 p.1468 |
| `w:ST_ObjectUpdateMode` | restriction of xsd:string | `always`, `onCall` | §17.18.61 p.1468 |
| `w:ST_PTabAlignment` | restriction of xsd:string | `left`, `center`, `right` | §17.18.71 p.1474 |
| `w:ST_PTabLeader` | restriction of xsd:string | `none`, `dot`, `hyphen`, `underscore`, `middleDot` | §17.18.72 p.1475 |
| `w:ST_PTabRelativeTo` | restriction of xsd:string | `margin`, `indent` | §17.18.73 p.1477 |
| `w:ST_PageBorderDisplay` | restriction of xsd:string | `allPages`, `firstPage`, `notFirstPage` | §17.18.62 p.1468, OI 2.1.549 |
| `w:ST_PageBorderOffset` | restriction of xsd:string | `page`, `text` | §17.18.63 p.1469 |
| `w:ST_PageBorderZOrder` | restriction of xsd:string | `front`, `back` | §17.18.64 p.1470 |
| `w:ST_PageOrientation` | restriction of xsd:string | `portrait`, `landscape` | §17.18.65 p.1471 |
| `w:ST_Pitch` | restriction of xsd:string | `fixed`, `variable`, `default` | §17.18.66 p.1471 |
| `w:ST_PixelsMeasure` | restriction of s:ST_UnsignedDecimalNumber |  | §17.18.67 p.1472 |
| `w:ST_PointMeasure` | restriction of s:ST_UnsignedDecimalNumber |  | §17.18.68 p.1472, OI 2.1.550 |
| `w:ST_Proof` | restriction of xsd:string | `clean`, `dirty` | §17.18.69 p.1473 |
| `w:ST_ProofErr` | restriction of xsd:string | `spellStart`, `spellEnd`, `gramStart`, `gramEnd` | §17.18.70 p.1473, OI 2.1.551 |
| `w:ST_RestartNumber` | restriction of xsd:string | `continuous`, `eachSect`, `eachPage` | §17.18.74 p.1477 |
| `w:ST_RubyAlign` | restriction of xsd:string | `center`, `distributeLetter`, `distributeSpace`, `left`, `right`, `rightVertical` | §17.18.75 p.1478, OI 2.1.552 |
| `w:ST_SdtDateMappingType` | restriction of xsd:string | `text`, `date`, `dateTime` | §17.18.76 p.1480 |
| `w:ST_SectionMark` | restriction of xsd:string | `nextPage`, `nextColumn`, `continuous`, `evenPage`, `oddPage` | §17.18.77 p.1481 |
| `w:ST_Shd` | restriction of xsd:string | `nil`, `clear`, `solid`, `horzStripe`, `vertStripe`, `reverseDiagStripe`, `diagStripe`, `horzCross`, `diagCross`, `thinHorzStripe`, `thinVertStripe`, `thinReverseDiagStripe` … (+26) | §17.18.78 p.1484, OI 2.1.553 |
| `w:ST_ShortHexNumber` | restriction of xsd:hexBinary | length `2` | §17.18.79 p.1498 |
| `w:ST_SignedHpsMeasure` | union of xsd:integer, s:ST_UniversalMeasure |  | §17.18.80 p.1499, OI 2.1.554 |
| `w:ST_SignedTwipsMeasure` | union of xsd:integer, s:ST_UniversalMeasure |  | §17.18.81 p.1499, OI 2.1.555 |
| `w:ST_StyleSort` | restriction of xsd:string | `name`, `priority`, `default`, `font`, `basedOn`, `type`, `0000`, `0001`, `0002`, `0003`, `0004`, `0005` | §17.18.82 p.1500 |
| `w:ST_StyleType` | restriction of xsd:string | `paragraph`, `character`, `table`, `numbering` | §17.18.83 p.1501 |
| `w:ST_TabJc` | restriction of xsd:string | `clear`, `start`, `center`, `end`, `decimal`, `bar`, `num`, `left`, `right` | §17.18.84 p.1502, OI 2.1.556 |
| `w:ST_TabTlc` | restriction of xsd:string | `none`, `dot`, `hyphen`, `underscore`, `heavy`, `middleDot` | §17.18.85 p.1503 |
| `w:ST_TargetScreenSz` | restriction of xsd:string | `544x376`, `640x480`, `720x512`, `800x600`, `1024x768`, `1152x882`, `1152x900`, `1280x1024`, `1600x1200`, `1800x1440`, `1920x1200` | §17.18.86 p.1505 |
| `w:ST_TblLayoutType` | restriction of xsd:string | `fixed`, `autofit` | §17.18.87 p.1506 |
| `w:ST_TblOverlap` | restriction of xsd:string | `never`, `overlap` | §17.18.88 p.1509 |
| `w:ST_TblStyleOverrideType` | restriction of xsd:string | `wholeTable`, `firstRow`, `lastRow`, `firstCol`, `lastCol`, `band1Vert`, `band2Vert`, `band1Horz`, `band2Horz`, `neCell`, `nwCell`, `seCell` … (+1) | §17.18.89 p.1511, OI 2.1.557 |
| `w:ST_TblWidth` | restriction of xsd:string | `nil`, `pct`, `dxa`, `auto` | §17.18.90 p.1512 |
| `w:ST_TextAlignment` | restriction of xsd:string | `top`, `center`, `baseline`, `bottom`, `auto` | §17.18.91 p.1513 |
| `w:ST_TextDirection` | restriction of xsd:string | `tb`, `rl`, `lr`, `tbV`, `rlV`, `lrV`, `btLr`, `lrTb`, `lrTbV`, `tbLrV`, `tbRl`, `tbRlV` | §17.18.93 p.1515, OI 2.1.558 |
| `w:ST_TextEffect` | restriction of xsd:string | `blinkBackground`, `lights`, `antsBlack`, `antsRed`, `shimmer`, `sparkle`, `none` | §17.18.94 p.1517, OI 2.1.559 |
| `w:ST_TextScale` | union of w:ST_TextScalePercent, w:ST_TextScaleDecimal |  | §17.18.95 p.1518, OI 2.1.560 |
| `w:ST_TextScaleDecimal` | restriction of xsd:integer | minInclusive `0`, maxInclusive `600` | Part 4 §14.11.11 p.202 |
| `w:ST_TextScalePercent` | restriction of xsd:string | pattern `0*(600\|([0-5]?[0-9]?[0-9]))%` | §17.18.106 p.1531 |
| `w:ST_TextboxTightWrap` | restriction of xsd:string | `none`, `allLines`, `firstAndLastLine`, `firstLineOnly`, `lastLineOnly` | §17.18.92 p.1514 |
| `w:ST_Theme` | restriction of xsd:string | `majorEastAsia`, `majorBidi`, `majorAscii`, `majorHAnsi`, `minorEastAsia`, `minorBidi`, `minorAscii`, `minorHAnsi` | §17.18.96 p.1518 |
| `w:ST_ThemeColor` | restriction of xsd:string | `dark1`, `light1`, `dark2`, `light2`, `accent1`, `accent2`, `accent3`, `accent4`, `accent5`, `accent6`, `hyperlink`, `followedHyperlink` … (+5) | §17.18.97 p.1519 |
| `w:ST_UcharHexNumber` | restriction of xsd:hexBinary | length `1` | §17.18.98 p.1521 |
| `w:ST_Underline` | restriction of xsd:string | `single`, `words`, `double`, `thick`, `dotted`, `dottedHeavy`, `dash`, `dashedHeavy`, `dashLong`, `dashLongHeavy`, `dotDash`, `dashDotHeavy` … (+6) | §17.18.99 p.1521 |
| `w:ST_UnqualifiedPercentage` | restriction of xsd:integer |  | Part 4 §14.11.10 p.202 |
| `w:ST_VAnchor` | restriction of xsd:string | `text`, `margin`, `page` | §17.18.100 p.1525 |
| `w:ST_VerticalJc` | restriction of xsd:string | `top`, `center`, `both`, `bottom` | §17.18.101 p.1526, OI 2.1.561 |
| `w:ST_View` | restriction of xsd:string | `none`, `print`, `outline`, `masterPages`, `normal`, `web` | §17.18.102 p.1527, OI 2.1.562 |
| `w:ST_WmlColorSchemeIndex` | restriction of xsd:string | `dark1`, `light1`, `dark2`, `light2`, `accent1`, `accent2`, `accent3`, `accent4`, `accent5`, `accent6`, `hyperlink`, `followedHyperlink` | §17.18.103 p.1528 |
| `w:ST_Wrap` | restriction of xsd:string | `auto`, `notBeside`, `around`, `tight`, `through`, `none` | §17.18.104 p.1529, OI 2.1.563 |
| `w:ST_Zoom` | restriction of xsd:string | `none`, `fullPage`, `bestFit`, `textFit` | §17.18.105 p.1531 |

## Element details

### w:abstractNum

- Type: `w:CT_AbstractNum`
- Attributes: `w:abstractNumId`!
- Children: `w:nsid`, `w:multiLevelType`, `w:tmpl`, `w:name`, `w:styleLink`, `w:numStyleLink`, `w:lvl`
- Parents: `w:numbering`
- Specification: §17.9.1 p.703, OI 2.1.275

### w:abstractNumId

- Type: `w:CT_DecimalNumber`
- Attributes: `w:val`!
- Parents: `w:num`
- Specification: §17.9.2 p.704, OI 2.1.276

### w:active

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:recipientData`
- Specification: §17.14.1 p.940

### w:activeRecord

- Type: `w:CT_DecimalNumber`
- Attributes: `w:val`!
- Parents: `w:mailMerge`
- Specification: §17.14.2 p.941, OI 2.1.360

### w:activeWritingStyle

- Type: `w:CT_WritingStyle`
- Attributes: `w:appName`!, `w:checkStyle`!, `w:dllVersion`!, `w:lang`!, `w:nlCheck`, `w:vendorID`!
- Parents: `w:settings`
- Specification: §17.15.1.1 p.980, OI 2.1.383

### w:addressFieldName

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:mailMerge`
- Specification: §17.14.3 p.943

### w:adjustLineHeightInTable

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: §17.15.3.1 p.1159

### w:adjustRightInd

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:pPr`
- Specification: §17.3.1.1 p.205

### w:alias

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:sdtPr`
- Specification: §17.5.2.1 p.511

### w:aliases

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:style`
- Specification: §17.7.4.1 p.628, OI 2.1.231

### w:alignBordersAndEdges

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.2 p.982

### w:alignTablesRowByRow

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.1 p.97

### w:allowPNG

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:webSettings`
- Specification: §17.15.2.1 p.1096, OI 2.1.428

### w:allowSpaceOfSameStyleInTable

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.2 p.98

### w:altChunk

- Type: `w:CT_AltChunk`
- Attributes: `r:id`
- Children: `w:altChunkPr`
- Parents: `w:body`, `w:comment`, `w:docPartBody`, `w:endnote`, `w:footnote`, `w:ftr`, `w:hdr`, `w:tc`, `w:txbxContent`, `wne:txbxContent`, `wp:txbxContent`
- Specification: §17.17.2.1 p.1308, OI 2.1.527

### w:altChunkPr

- Type: `w:CT_AltChunkPr`
- Children: `w:matchSrc`
- Parents: `w:altChunk`
- Specification: §17.17.2.2 p.1309

### w:altName

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:font`
- Specification: §17.8.3.1 p.681, OI 2.1.262

### w:alwaysMergeEmptyNamespace

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.3 p.983, OI 2.1.384

### w:alwaysShowPlaceholderText

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.4 p.984

### w:annotationRef

- Type: `w:CT_Empty`
- Parents: `m:r`, `w:r`
- Specification: §17.13.4.1 p.812

### w:applyBreakingRules

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: §17.15.3.2 p.1160, OI 2.1.451

### w:attachedSchema

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:settings`
- Specification: §17.15.1.5 p.985

### w:attachedTemplate

- Type: `w:CT_Rel`
- Attributes: `r:id`!
- Parents: `w:settings`
- Specification: §17.15.1.6 p.986, OI 2.1.385

### w:attr

- Type: `w:CT_Attr`
- Attributes: `w:name`!, `w:uri`, `w:val`!
- Parents: `w:customXmlPr`, `w:smartTagPr`
- Specification: §17.5.1.1 p.497, OI 2.1.186; §17.5.1.2 p.498, OI 2.1.187

### w:autoCaption

- Type: `w:CT_AutoCaption`
- Attributes: `w:caption`!, `w:name`!
- Parents: `w:autoCaptions`
- Specification: §17.15.1.7 p.987, OI 2.1.386

### w:autoCaptions

- Type: `w:CT_AutoCaptions`
- Children: `w:autoCaption`
- Parents: `w:captions`
- Specification: §17.15.1.8 p.989, OI 2.1.387

### w:autoFormatOverride

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.9 p.990, OI 2.1.388

### w:autoHyphenation

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.10 p.990

### w:autoRedefine

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:style`
- Specification: §17.7.4.2 p.629, OI 2.1.232

### w:autoSpaceDE

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:pPr`
- Specification: §17.3.1.2 p.205

### w:autoSpaceDN

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:pPr`
- Specification: §17.3.1.3 p.206

### w:autoSpaceLikeWord95

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.4 p.102

### w:autofitToFirstFixedWidthCell

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.3 p.100

### w:b

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:rPr`
- Specification: §17.3.2.1 p.274, OI 2.1.68

### w:bCs

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:rPr`
- Specification: §17.3.2.2 p.274, OI 2.1.69

### w:background

- Type: `w:CT_Background`
- Attributes: `w:color`, `w:themeColor`, `w:themeShade`, `w:themeTint`
- Children: `w:drawing`, `any(urn:schemas-microsoft-com:vml)`, `any(urn:schemas-microsoft-com:office:office)`
- Parents: `w:document`, `w:glossaryDocument`
- Specification: §17.2.1 p.197, OI 2.1.35

### w:balanceSingleByteDoubleByteWidth

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: §17.15.3.3 p.1161

### w:bar

- Type: `w:CT_Border`
- Attributes: `w:color`, `w:frame`, `w:shadow`, `w:space`, `w:sz`, `w:themeColor`, `w:themeShade`, `w:themeTint`, `w:val`!
- Parents: `w:pBdr`
- Specification: §17.3.1.4 p.206, OI 2.1.38

### w:basedOn

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:style`
- Specification: §17.7.4.3 p.631, OI 2.1.233

### w:bdo

- Type: `w:CT_BdoContentRun`
- Attributes: `w:val`
- Children: `w:customXml`, `w:smartTag`, `w:sdt`, `w:dir`, `w:bdo`, `w:r`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`, `w:fldSimple`, `w:hyperlink`, `w:subDoc`
- Parents: `w:bdo`, `w:customXml`, `w:del`, `w:dir`, `w:fldSimple`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:p`, `w:sdtContent`, `w:smartTag`
- Specification: §17.3.2.3 p.275, OI 2.1.70

### w:bdr

- Type: `w:CT_Border`
- Attributes: `w:color`, `w:frame`, `w:shadow`, `w:space`, `w:sz`, `w:themeColor`, `w:themeShade`, `w:themeTint`, `w:val`!
- Parents: `w:rPr`
- Specification: §17.3.2.4 p.276, OI 2.1.71

### w:behavior

- Type: `w:CT_DocPartBehavior`
- Attributes: `w:val`!
- Parents: `w:behaviors`
- Specification: §17.12.1 p.790

### w:behaviors

- Type: `w:CT_DocPartBehaviors`
- Children: `w:behavior`
- Parents: `w:docPartPr`
- Specification: §17.12.2 p.793

### w:between

- Type: `w:CT_Border`
- Attributes: `w:color`, `w:frame`, `w:shadow`, `w:space`, `w:sz`, `w:themeColor`, `w:themeShade`, `w:themeTint`, `w:val`!
- Parents: `w:pBdr`
- Specification: §17.3.1.5 p.207, OI 2.1.39

### w:bibliography

- Type: `w:CT_Empty`
- Parents: `w:sdtPr`
- Specification: §17.5.2.2 p.512

### w:bidi

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:pPr`, `w:sectPr`
- Specification: §17.3.1.6 p.209; §17.6.1 p.557

### w:bidiVisual

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:tblPr`
- Specification: §17.4.1 p.383, OI 2.1.115

### w:blockQuote

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:div`
- Specification: §17.15.2.2 p.1097, OI 2.1.429

### w:body

- Type: `w:CT_Body`
- Children: `w:customXml`, `w:sdt`, `w:p`, `w:tbl`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`, `w:altChunk`, `w:sectPr`
- Parents: `w:document`
- Specification: §17.2.2 p.202, OI 2.1.36

### w:bodyDiv

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:div`
- Specification: §17.15.2.3 p.1098, OI 2.1.430

### w:bookFoldPrinting

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.11 p.991, OI 2.1.389

### w:bookFoldPrintingSheets

- Type: `w:CT_DecimalNumber`
- Attributes: `w:val`!
- Parents: `w:settings`
- Specification: §17.15.1.12 p.993, OI 2.1.390

### w:bookFoldRevPrinting

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.13 p.995, OI 2.1.391

### w:bookmarkEnd

- Type: `w:CT_MarkupRange`
- Attributes: `w:displacedByCustomXml`, `w:id`!
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:bdo`, `w:body`, `w:comment`, `w:customXml`, `w:del`, `w:dir`, `w:docPartBody`, `w:endnote`, `w:fldSimple`, `w:footnote`, `w:ftr`, `w:hdr`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:p`, `w:rt`, `w:rubyBase`, `w:sdtContent`, `w:smartTag`, `w:tbl`, `w:tc`, `w:tr`, `w:txbxContent`, `wne:txbxContent`, `wp:txbxContent`
- Specification: §17.13.6.1 p.917, OI 2.1.355

### w:bookmarkStart

- Type: `w:CT_Bookmark`
- Attributes: `w:colFirst`, `w:colLast`, `w:displacedByCustomXml`, `w:id`!, `w:name`!
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:bdo`, `w:body`, `w:comment`, `w:customXml`, `w:del`, `w:dir`, `w:docPartBody`, `w:endnote`, `w:fldSimple`, `w:footnote`, `w:ftr`, `w:hdr`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:p`, `w:rt`, `w:rubyBase`, `w:sdtContent`, `w:smartTag`, `w:tbl`, `w:tc`, `w:tr`, `w:txbxContent`, `wne:txbxContent`, `wp:txbxContent`
- Specification: §17.13.6.2 p.919, OI 2.1.356

### w:bordersDoNotSurroundFooter

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.14 p.997

### w:bordersDoNotSurroundHeader

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.15 p.997

### w:bottom

- Type: `w:CT_Border`, `w:CT_BottomPageBorder`, `w:CT_TblWidth`
- Attributes: `r:bottomLeft`, `r:bottomRight`, `r:id`, `w:color`, `w:frame`, `w:shadow`, `w:space`, `w:sz`, `w:themeColor`, `w:themeShade`, `w:themeTint`, `w:type`, `w:val`!, `w:w`
- Parents: `w:divBdr`, `w:pBdr`, `w:pgBorders`, `w:tblBorders`, `w:tblCellMar`, `w:tcBorders`, `w:tcMar`
- Specification: §17.3.1.7 p.209, OI 2.1.40; §17.4.2 p.384, OI 2.1.116; §17.4.3 p.385, OI 2.1.117; §17.4.4 p.385, OI 2.1.118; §17.4.5 p.386, OI 2.1.119; §17.6.2 p.558, OI 2.1.211; §17.15.2.4 p.1099, OI 2.1.431

### w:br

- Type: `w:CT_Br`
- Attributes: `w:clear`, `w:type`
- Parents: `m:r`, `w:r`
- Specification: §17.3.3.1 p.333

### w:cachedColBalance

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.5 p.103

### w:calcOnExit

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:ffData`
- Specification: §17.16.6 p.1267

### w:calendar

- Type: `w:CT_CalendarType`
- Attributes: `w:val`
- Parents: `w:date`
- Specification: §17.5.2.3 p.512

### w:cantSplit

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:trPr`
- Specification: §17.4.6 p.387, OI 2.1.120

### w:caps

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:rPr`
- Specification: §17.3.2.5 p.276

### w:caption

- Type: `w:CT_Caption`
- Attributes: `w:chapNum`, `w:heading`, `w:name`!, `w:noLabel`, `w:numFmt`, `w:pos`, `w:sep`
- Parents: `w:captions`
- Specification: §17.15.1.16 p.998, OI 2.1.392

### w:captions

- Type: `w:CT_Captions`
- Children: `w:caption`, `w:autoCaptions`
- Parents: `w:settings`
- Specification: §17.15.1.17 p.1007, OI 2.1.393

### w:category

- Type: `w:CT_DocPartCategory`
- Children: `w:name`, `w:gallery`
- Parents: `w:docPartPr`
- Specification: §17.12.3 p.793

### w:cellDel

- Type: `w:CT_TrackChange`
- Attributes: `w:author`!, `w:date`, `w:id`!
- Parents: `w:tcPr`
- Specification: §17.13.5.1 p.823, OI 2.1.317

### w:cellIns

- Type: `w:CT_TrackChange`
- Attributes: `w:author`!, `w:date`, `w:id`!
- Parents: `w:tcPr`
- Specification: §17.13.5.2 p.825, OI 2.1.318

### w:cellMerge

- Type: `w:CT_CellMergeTrackChange`
- Attributes: `w:author`!, `w:date`, `w:id`!, `w:vMerge`, `w:vMergeOrig`
- Parents: `w:tcPr`
- Specification: §17.13.5.3 p.828, OI 2.1.319

### w:characterSpacingControl

- Type: `w:CT_CharacterSpacing`
- Attributes: `w:val`!
- Parents: `w:settings`
- Specification: §17.15.1.18 p.1010

### w:charset

- Type: `w:CT_Charset`
- Attributes: `w:characterSet`, `w:val`
- Parents: `w:font`
- Specification: §17.8.3.2 p.683, OI 2.1.263

### w:checkBox

- Type: `w:CT_FFCheckBox`
- Children: `w:size`, `w:sizeAuto`, `w:default`, `w:checked`
- Parents: `w:ffData`
- Specification: §17.16.7 p.1268

### w:checkErrors

- Type: `w:CT_DecimalNumber`
- Attributes: `w:val`!
- Parents: `w:mailMerge`
- Specification: §17.14.4 p.944, OI 2.1.361

### w:checked

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:checkBox`
- Specification: §17.16.8 p.1268

### w:citation

- Type: `w:CT_Empty`
- Parents: `w:sdtPr`
- Specification: §17.5.2.4 p.513

### w:clickAndTypeStyle

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:settings`
- Specification: §17.15.1.19 p.1010, OI 2.1.394

### w:clrSchemeMapping

- Type: `w:CT_ColorSchemeMapping`
- Attributes: `w:accent1`, `w:accent2`, `w:accent3`, `w:accent4`, `w:accent5`, `w:accent6`, `w:bg1`, `w:bg2`, `w:followedHyperlink`, `w:hyperlink`, `w:t1`, `w:t2`
- Parents: `w:settings`
- Specification: §17.15.1.20 p.1012, OI 2.1.395

### w:cnfStyle

- Type: `w:CT_Cnf`
- Attributes: `w:evenHBand`, `w:evenVBand`, `w:firstColumn`, `w:firstRow`, `w:firstRowFirstColumn`, `w:firstRowLastColumn`, `w:lastColumn`, `w:lastRow`, `w:lastRowFirstColumn`, `w:lastRowLastColumn`, `w:oddHBand`, `w:oddVBand`, `w:val`
- Parents: `w:pPr`, `w:tcPr`, `w:trPr`
- Specification: §17.3.1.8 p.211, OI 2.1.41; §17.4.7 p.389, OI 2.1.121; §17.4.8 p.395, OI 2.1.122

### w:col

- Type: `w:CT_Column`
- Attributes: `w:space`, `w:w`
- Parents: `w:cols`
- Specification: §17.6.3 p.567, OI 2.1.212

### w:colDelim

- Type: `w:CT_DecimalNumber`
- Attributes: `w:val`!
- Parents: `w:odso`
- Specification: §17.14.5 p.945, OI 2.1.362

### w:color

- Type: `w:CT_Color`
- Attributes: `w:themeColor`, `w:themeShade`, `w:themeTint`, `w:val`!
- Parents: `w:framesetSplitbar`, `w:rPr`
- Specification: §17.3.2.6 p.277, OI 2.1.72; §17.15.2.5 p.1100, OI 2.1.432

### w:cols

- Type: `w:CT_Columns`
- Attributes: `w:equalWidth`, `w:num`, `w:sep`, `w:space`
- Children: `w:col`
- Parents: `w:sectPr`
- Specification: §17.6.4 p.568, OI 2.1.213

### w:column

- Type: `w:CT_DecimalNumber`
- Attributes: `w:val`!
- Parents: `w:fieldMapData`, `w:recipientData`
- Specification: §17.14.6 p.946, OI 2.1.363; §17.14.7 p.948, OI 2.1.364

### w:comboBox

- Type: `w:CT_SdtComboBox`
- Attributes: `w:lastValue`
- Children: `w:listItem`
- Parents: `w:sdtPr`
- Specification: §17.5.2.5 p.513

### w:comment

- Type: `w:CT_Comment`
- Attributes: `w:author`!, `w:date`, `w:id`!, `w:initials`
- Children: `w:customXml`, `w:sdt`, `w:p`, `w:tbl`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`, `w:altChunk`
- Parents: `w:comments`
- Specification: §17.13.4.2 p.812, OI 2.1.313

### w:commentRangeEnd

- Type: `w:CT_MarkupRange`
- Attributes: `w:displacedByCustomXml`, `w:id`!
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:bdo`, `w:body`, `w:comment`, `w:customXml`, `w:del`, `w:dir`, `w:docPartBody`, `w:endnote`, `w:fldSimple`, `w:footnote`, `w:ftr`, `w:hdr`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:p`, `w:rt`, `w:rubyBase`, `w:sdtContent`, `w:smartTag`, `w:tbl`, `w:tc`, `w:tr`, `w:txbxContent`, `wne:txbxContent`, `wp:txbxContent`
- Specification: §17.13.4.3 p.815, OI 2.1.314

### w:commentRangeStart

- Type: `w:CT_MarkupRange`
- Attributes: `w:displacedByCustomXml`, `w:id`!
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:bdo`, `w:body`, `w:comment`, `w:customXml`, `w:del`, `w:dir`, `w:docPartBody`, `w:endnote`, `w:fldSimple`, `w:footnote`, `w:ftr`, `w:hdr`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:p`, `w:rt`, `w:rubyBase`, `w:sdtContent`, `w:smartTag`, `w:tbl`, `w:tc`, `w:tr`, `w:txbxContent`, `wne:txbxContent`, `wp:txbxContent`
- Specification: §17.13.4.4 p.817, OI 2.1.315

### w:commentReference

- Type: `w:CT_Markup`
- Attributes: `w:id`!
- Parents: `m:r`, `w:r`
- Specification: §17.13.4.5 p.819, OI 2.1.316

### w:comments

- Type: `w:CT_Comments`
- Children: `w:comment`
- Specification: §17.13.4.6 p.821

### w:compat

- Type: `w:CT_Compat`
- Children: `w:useSingleBorderforContiguousCells`, `w:wpJustification`, `w:noTabHangInd`, `w:noLeading`, `w:spaceForUL`, `w:noColumnBalance`, `w:balanceSingleByteDoubleByteWidth`, `w:noExtraLineSpacing`, `w:doNotLeaveBackslashAlone`, `w:ulTrailSpace`, `w:doNotExpandShiftReturn`, `w:spacingInWholePoints`, `w:lineWrapLikeWord6`, `w:printBodyTextBeforeHeader`, `w:printColBlack`, `w:wpSpaceWidth`, `w:showBreaksInFrames`, `w:subFontBySize`, `w:suppressBottomSpacing`, `w:suppressTopSpacing`, `w:suppressSpacingAtTopOfPage`, `w:suppressTopSpacingWP`, `w:suppressSpBfAfterPgBrk`, `w:swapBordersFacingPages`, `w:convMailMergeEsc`, `w:truncateFontHeightsLikeWP6`, `w:mwSmallCaps`, `w:usePrinterMetrics`, `w:doNotSuppressParagraphBorders`, `w:wrapTrailSpaces`, `w:footnoteLayoutLikeWW8`, `w:shapeLayoutLikeWW8`, `w:alignTablesRowByRow`, `w:forgetLastTabAlignment`, `w:adjustLineHeightInTable`, `w:autoSpaceLikeWord95`, `w:noSpaceRaiseLower`, `w:doNotUseHTMLParagraphAutoSpacing`, `w:layoutRawTableWidth`, `w:layoutTableRowsApart`, `w:useWord97LineBreakRules`, `w:doNotBreakWrappedTables`, `w:doNotSnapToGridInCell`, `w:selectFldWithFirstOrLastChar`, `w:applyBreakingRules`, `w:doNotWrapTextWithPunct`, `w:doNotUseEastAsianBreakRules`, `w:useWord2002TableStyleRules`, `w:growAutofit`, `w:useFELayout`, `w:useNormalStyleForList`, `w:doNotUseIndentAsNumberingTabStop`, `w:useAltKinsokuLineBreakRules`, `w:allowSpaceOfSameStyleInTable`, `w:doNotSuppressIndentation`, `w:doNotAutofitConstrainedTables`, `w:autofitToFirstFixedWidthCell`, `w:underlineTabInNumList`, `w:displayHangulFixedWidth`, `w:splitPgBreakAndParaMark`, `w:doNotVertAlignCellWithSp`, `w:doNotBreakConstrainedForcedTable`, `w:doNotVertAlignInTxbx`, `w:useAnsiKerningPairs`, `w:cachedColBalance`, `w:compatSetting`
- Parents: `w:settings`
- Specification: §17.15.1.21 p.1017

### w:compatSetting

- Type: `w:CT_CompatSetting`
- Attributes: `w:name`, `w:uri`, `w:val`
- Parents: `w:compat`
- Specification: §17.15.3.4 p.1162

### w:connectString

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:mailMerge`
- Specification: §17.14.8 p.949

### w:consecutiveHyphenLimit

- Type: `w:CT_DecimalNumber`
- Attributes: `w:val`!
- Parents: `w:settings`
- Specification: §17.15.1.22 p.1017, OI 2.1.396

### w:contentPart

- Type: `w:CT_Rel`
- Attributes: `r:id`!
- Parents: `m:r`, `w:r`
- Specification: §17.3.3.2 p.334, OI 2.1.102

### w:contextualSpacing

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:pPr`
- Specification: §17.3.1.9 p.216

### w:continuationSeparator

- Type: `w:CT_Empty`
- Parents: `m:r`, `w:r`
- Specification: §17.11.1 p.758

### w:control

- Type: `w:CT_Control`
- Attributes: `r:id`, `w:name`, `w:shapeid`
- Parents: `w:object`, `w:pict`
- Specification: §17.3.3.3 p.337, OI 2.1.103; Part 4 §14.3.2.1 p.51, OI 2.1.1756

### w:convMailMergeEsc

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.6 p.104, OI 2.1.1776

### w:cr

- Type: `w:CT_Empty`
- Parents: `m:r`, `w:r`
- Specification: §17.3.3.4 p.339

### w:cs

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:rPr`
- Specification: §17.3.2.7 p.280, OI 2.1.73

### w:customXml

- Type: `w:CT_CustomXmlRun`, `w:CT_CustomXmlBlock`, `w:CT_CustomXmlRow`, `w:CT_CustomXmlCell`
- Attributes: `w:element`!, `w:uri`
- Children: `w:customXmlPr`, `w:customXml`, `w:smartTag`, `w:sdt`, `w:dir`, `w:bdo`, `w:r`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`, `w:fldSimple`, `w:hyperlink`, `w:subDoc`, `w:p`, `w:tbl`, `w:tr`, `w:tc`
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:bdo`, `w:body`, `w:comment`, `w:customXml`, `w:del`, `w:dir`, `w:docPartBody`, `w:endnote`, `w:fldSimple`, `w:footnote`, `w:ftr`, `w:hdr`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:p`, `w:sdtContent`, `w:smartTag`, `w:tbl`, `w:tc`, `w:tr`, `w:txbxContent`, `wne:txbxContent`, `wp:txbxContent`
- Specification: §17.5.1.3 p.500, OI 2.1.188; §17.5.1.4 p.501, OI 2.1.189; §17.5.1.5 p.503, OI 2.1.190; §17.5.1.6 p.504, OI 2.1.191

### w:customXmlDelRangeEnd

- Type: `w:CT_Markup`
- Attributes: `w:id`!
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:bdo`, `w:body`, `w:comment`, `w:customXml`, `w:del`, `w:dir`, `w:docPartBody`, `w:endnote`, `w:fldSimple`, `w:footnote`, `w:ftr`, `w:hdr`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:p`, `w:rt`, `w:rubyBase`, `w:sdtContent`, `w:smartTag`, `w:tbl`, `w:tc`, `w:tr`, `w:txbxContent`, `wne:txbxContent`, `wp:txbxContent`
- Specification: §17.13.5.4 p.832, OI 2.1.320

### w:customXmlDelRangeStart

- Type: `w:CT_TrackChange`
- Attributes: `w:author`!, `w:date`, `w:id`!
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:bdo`, `w:body`, `w:comment`, `w:customXml`, `w:del`, `w:dir`, `w:docPartBody`, `w:endnote`, `w:fldSimple`, `w:footnote`, `w:ftr`, `w:hdr`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:p`, `w:rt`, `w:rubyBase`, `w:sdtContent`, `w:smartTag`, `w:tbl`, `w:tc`, `w:tr`, `w:txbxContent`, `wne:txbxContent`, `wp:txbxContent`
- Specification: §17.13.5.5 p.834, OI 2.1.321

### w:customXmlInsRangeEnd

- Type: `w:CT_Markup`
- Attributes: `w:id`!
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:bdo`, `w:body`, `w:comment`, `w:customXml`, `w:del`, `w:dir`, `w:docPartBody`, `w:endnote`, `w:fldSimple`, `w:footnote`, `w:ftr`, `w:hdr`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:p`, `w:rt`, `w:rubyBase`, `w:sdtContent`, `w:smartTag`, `w:tbl`, `w:tc`, `w:tr`, `w:txbxContent`, `wne:txbxContent`, `wp:txbxContent`
- Specification: §17.13.5.6 p.836, OI 2.1.322

### w:customXmlInsRangeStart

- Type: `w:CT_TrackChange`
- Attributes: `w:author`!, `w:date`, `w:id`!
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:bdo`, `w:body`, `w:comment`, `w:customXml`, `w:del`, `w:dir`, `w:docPartBody`, `w:endnote`, `w:fldSimple`, `w:footnote`, `w:ftr`, `w:hdr`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:p`, `w:rt`, `w:rubyBase`, `w:sdtContent`, `w:smartTag`, `w:tbl`, `w:tc`, `w:tr`, `w:txbxContent`, `wne:txbxContent`, `wp:txbxContent`
- Specification: §17.13.5.7 p.838, OI 2.1.323

### w:customXmlMoveFromRangeEnd

- Type: `w:CT_Markup`
- Attributes: `w:id`!
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:bdo`, `w:body`, `w:comment`, `w:customXml`, `w:del`, `w:dir`, `w:docPartBody`, `w:endnote`, `w:fldSimple`, `w:footnote`, `w:ftr`, `w:hdr`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:p`, `w:rt`, `w:rubyBase`, `w:sdtContent`, `w:smartTag`, `w:tbl`, `w:tc`, `w:tr`, `w:txbxContent`, `wne:txbxContent`, `wp:txbxContent`
- Specification: §17.13.5.8 p.841, OI 2.1.324

### w:customXmlMoveFromRangeStart

- Type: `w:CT_TrackChange`
- Attributes: `w:author`!, `w:date`, `w:id`!
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:bdo`, `w:body`, `w:comment`, `w:customXml`, `w:del`, `w:dir`, `w:docPartBody`, `w:endnote`, `w:fldSimple`, `w:footnote`, `w:ftr`, `w:hdr`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:p`, `w:rt`, `w:rubyBase`, `w:sdtContent`, `w:smartTag`, `w:tbl`, `w:tc`, `w:tr`, `w:txbxContent`, `wne:txbxContent`, `wp:txbxContent`
- Specification: §17.13.5.9 p.843, OI 2.1.325

### w:customXmlMoveToRangeEnd

- Type: `w:CT_Markup`
- Attributes: `w:id`!
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:bdo`, `w:body`, `w:comment`, `w:customXml`, `w:del`, `w:dir`, `w:docPartBody`, `w:endnote`, `w:fldSimple`, `w:footnote`, `w:ftr`, `w:hdr`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:p`, `w:rt`, `w:rubyBase`, `w:sdtContent`, `w:smartTag`, `w:tbl`, `w:tc`, `w:tr`, `w:txbxContent`, `wne:txbxContent`, `wp:txbxContent`
- Specification: §17.13.5.10 p.846, OI 2.1.326

### w:customXmlMoveToRangeStart

- Type: `w:CT_TrackChange`
- Attributes: `w:author`!, `w:date`, `w:id`!
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:bdo`, `w:body`, `w:comment`, `w:customXml`, `w:del`, `w:dir`, `w:docPartBody`, `w:endnote`, `w:fldSimple`, `w:footnote`, `w:ftr`, `w:hdr`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:p`, `w:rt`, `w:rubyBase`, `w:sdtContent`, `w:smartTag`, `w:tbl`, `w:tc`, `w:tr`, `w:txbxContent`, `wne:txbxContent`, `wp:txbxContent`
- Specification: §17.13.5.11 p.848, OI 2.1.327

### w:customXmlPr

- Type: `w:CT_CustomXmlPr`
- Children: `w:placeholder`, `w:attr`
- Parents: `w:customXml`
- Specification: §17.5.1.7 p.505

### w:dataBinding

- Type: `w:CT_DataBinding`
- Attributes: `w:prefixMappings`, `w:storeItemID`!, `w:xpath`!
- Parents: `w:sdtPr`
- Specification: §17.5.2.6 p.515, OI 2.1.193

### w:dataSource

- Type: `w:CT_Rel`
- Attributes: `r:id`!
- Parents: `w:mailMerge`
- Specification: §17.14.9 p.950, OI 2.1.365

### w:dataType

- Type: `w:CT_MailMergeDataType`
- Attributes: `w:val`!
- Parents: `w:mailMerge`
- Specification: §17.14.10 p.952

### w:date

- Type: `w:CT_SdtDate`
- Attributes: `w:fullDate`
- Children: `w:dateFormat`, `w:lid`, `w:storeMappedDataAs`, `w:calendar`
- Parents: `w:sdtPr`
- Specification: §17.5.2.7 p.518, OI 2.1.194

### w:dateFormat

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:date`
- Specification: §17.5.2.8 p.519

### w:dayLong

- Type: `w:CT_Empty`
- Parents: `m:r`, `w:r`
- Specification: §17.3.3.5 p.340

### w:dayShort

- Type: `w:CT_Empty`
- Parents: `m:r`, `w:r`
- Specification: §17.3.3.6 p.340

### w:ddList

- Type: `w:CT_FFDDList`
- Children: `w:result`, `w:default`, `w:listEntry`
- Parents: `w:ffData`
- Specification: §17.16.9 p.1269, OI 2.1.514

### w:decimalSymbol

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:settings`
- Specification: §17.15.1.23 p.1018, OI 2.1.397

### w:default

- Type: `w:CT_OnOff`, `w:CT_DecimalNumber`, `w:CT_String`
- Attributes: `w:val`
- Parents: `w:checkBox`, `w:ddList`, `w:textInput`
- Specification: §17.16.10 p.1269, OI 2.1.515; §17.16.11 p.1271, OI 2.1.516; §17.16.12 p.1272, OI 2.1.517

### w:defaultTabStop

- Type: `w:CT_TwipsMeasure`
- Attributes: `w:val`!
- Parents: `w:settings`
- Specification: §17.15.1.25 p.1021, OI 2.1.399

### w:defaultTableStyle

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:settings`
- Specification: §17.15.1.24 p.1020, OI 2.1.398

### w:del

- Type: `w:CT_RunTrackChange`, `w:CT_MathCtrlDel`, `w:CT_RPrChange`, `w:CT_TrackChange`
- Attributes: `w:author`!, `w:date`, `w:id`!
- Children: `w:customXml`, `w:smartTag`, `w:sdt`, `w:dir`, `w:bdo`, `w:r`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`, `m:acc`, `m:bar`, `m:box`, `m:borderBox`, `m:d`, `m:eqArr`, `m:f`, `m:func`, `m:groupChr`, `m:limLow`, `m:limUpp`, `m:m`, `m:nary`, `m:phant`, `m:rad`, `m:sPre`, `m:sSub`, `m:sSubSup`, `m:sSup`, `m:r`, `w:rPr`
- Parents: `m:ctrlPr`, `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:bdo`, `w:body`, `w:comment`, `w:customXml`, `w:del`, `w:dir`, `w:docPartBody`, `w:endnote`, `w:fldSimple`, `w:footnote`, `w:ftr`, `w:hdr`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:p`, `w:rPr`, `w:rt`, `w:rubyBase`, `w:sdtContent`, `w:smartTag`, `w:tbl`, `w:tc`, `w:tr`, `w:trPr`, `w:txbxContent`, `wne:txbxContent`, `wp:txbxContent`
- Specification: §17.13.5.12 p.851, OI 2.1.328; §17.13.5.13 p.853, OI 2.1.329; §17.13.5.14 p.854, OI 2.1.330; §17.13.5.15 p.856, OI 2.1.331

### w:delInstrText

- Type: `w:CT_Text` (text: `s:ST_String`)
- Attributes: `xml:space`
- Parents: `m:r`, `w:r`
- Specification: §17.16.13 p.1272

### w:delText

- Type: `w:CT_Text` (text: `s:ST_String`)
- Attributes: `xml:space`
- Parents: `m:r`, `w:r`
- Specification: §17.3.3.7 p.341

### w:description

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:docPartPr`
- Specification: §17.12.4 p.794

### w:destination

- Type: `w:CT_MailMergeDest`
- Attributes: `w:val`!
- Parents: `w:mailMerge`
- Specification: §17.14.11 p.953

### w:dir

- Type: `w:CT_DirContentRun`
- Attributes: `w:val`
- Children: `w:customXml`, `w:smartTag`, `w:sdt`, `w:dir`, `w:bdo`, `w:r`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`, `w:fldSimple`, `w:hyperlink`, `w:subDoc`
- Parents: `w:bdo`, `w:customXml`, `w:del`, `w:dir`, `w:fldSimple`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:p`, `w:sdtContent`, `w:smartTag`
- Specification: §17.3.2.8 p.281, OI 2.1.74

### w:dirty

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:rubyPr`
- Specification: §17.3.3.8 p.342

### w:displayBackgroundShape

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.26 p.1022

### w:displayHangulFixedWidth

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.7 p.105

### w:displayHorizontalDrawingGridEvery

- Type: `w:CT_DecimalNumber`
- Attributes: `w:val`!
- Parents: `w:settings`
- Specification: §17.15.1.27 p.1023, OI 2.1.400

### w:displayVerticalDrawingGridEvery

- Type: `w:CT_DecimalNumber`
- Attributes: `w:val`!
- Parents: `w:settings`
- Specification: §17.15.1.28 p.1025, OI 2.1.401

### w:div

- Type: `w:CT_Div`
- Attributes: `w:id`!
- Children: `w:blockQuote`, `w:bodyDiv`, `w:marLeft`, `w:marRight`, `w:marTop`, `w:marBottom`, `w:divBdr`, `w:divsChild`
- Parents: `w:divs`, `w:divsChild`
- Specification: §17.15.2.6 p.1103, OI 2.1.433

### w:divBdr

- Type: `w:CT_DivBdr`
- Children: `w:top`, `w:left`, `w:bottom`, `w:right`
- Parents: `w:div`
- Specification: §17.15.2.7 p.1106

### w:divId

- Type: `w:CT_DecimalNumber`
- Attributes: `w:val`!
- Parents: `w:pPr`, `w:trPr`
- Specification: §17.3.1.10 p.217, OI 2.1.42; §17.4.9 p.400, OI 2.1.123

### w:divs

- Type: `w:CT_Divs`
- Children: `w:div`
- Parents: `w:webSettings`
- Specification: §17.15.2.8 p.1107

### w:divsChild

- Type: `w:CT_Divs`
- Children: `w:div`
- Parents: `w:div`
- Specification: §17.15.2.9 p.1109

### w:doNotAutoCompressPictures

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.33 p.1033

### w:doNotAutofitConstrainedTables

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.8 p.106, OI 2.1.1777

### w:doNotBreakConstrainedForcedTable

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.9 p.107

### w:doNotBreakWrappedTables

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.10 p.109

### w:doNotDemarcateInvalidXml

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.34 p.1034

### w:doNotDisplayPageBoundaries

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.35 p.1034

### w:doNotEmbedSmartTags

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.36 p.1036

### w:doNotExpandShiftReturn

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: §17.15.3.5 p.1164

### w:doNotHyphenateCaps

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.37 p.1036

### w:doNotIncludeSubdocsInStats

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.38 p.1037

### w:doNotLeaveBackslashAlone

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: §17.15.3.6 p.1164, OI 2.1.452

### w:doNotOrganizeInFolder

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:webSettings`
- Specification: §17.15.2.10 p.1111, OI 2.1.434

### w:doNotRelyOnCSS

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:webSettings`
- Specification: §17.15.2.11 p.1111

### w:doNotSaveAsSingleFile

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:webSettings`
- Specification: §17.15.2.12 p.1112

### w:doNotShadeFormData

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.39 p.1038

### w:doNotSnapToGridInCell

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.11 p.111

### w:doNotSuppressBlankLines

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:mailMerge`
- Specification: §17.14.12 p.954, OI 2.1.366

### w:doNotSuppressIndentation

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.12 p.112

### w:doNotSuppressParagraphBorders

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.13 p.114, OI 2.1.1778

### w:doNotTrackFormatting

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.40 p.1038

### w:doNotTrackMoves

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.41 p.1040

### w:doNotUseEastAsianBreakRules

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.14 p.115

### w:doNotUseHTMLParagraphAutoSpacing

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.15 p.116

### w:doNotUseIndentAsNumberingTabStop

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.16 p.118

### w:doNotUseLongFileNames

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:webSettings`
- Specification: §17.15.2.13 p.1113

### w:doNotUseMarginsForDrawingGridOrigin

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.42 p.1041, OI 2.1.404

### w:doNotValidateAgainstSchema

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.43 p.1041

### w:doNotVertAlignCellWithSp

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.17 p.119

### w:doNotVertAlignInTxbx

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.18 p.121

### w:doNotWrapTextWithPunct

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.19 p.123

### w:docDefaults

- Type: `w:CT_DocDefaults`
- Children: `w:rPrDefault`, `w:pPrDefault`
- Parents: `w:styles`
- Specification: §17.7.5.1 p.657

### w:docGrid

- Type: `w:CT_DocGrid`
- Attributes: `w:charSpace`, `w:linePitch`, `w:type`
- Parents: `w:sectPr`
- Specification: §17.6.5 p.570

### w:docPart

- Type: `w:CT_String`, `w:CT_DocPart`
- Attributes: `w:val`!
- Children: `w:docPartPr`, `w:docPartBody`
- Parents: `w:docParts`, `w:placeholder`
- Specification: §17.5.2.9 p.521, OI 2.1.195; §17.12.5 p.795

### w:docPartBody

- Type: `w:CT_Body`
- Children: `w:customXml`, `w:sdt`, `w:p`, `w:tbl`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`, `w:altChunk`, `w:sectPr`
- Parents: `w:docPart`
- Specification: §17.12.6 p.796, OI 2.1.310

### w:docPartCategory

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:docPartList`, `w:docPartObj`
- Specification: §17.5.2.10 p.522

### w:docPartGallery

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:docPartList`, `w:docPartObj`
- Specification: §17.5.2.11 p.523

### w:docPartList

- Type: `w:CT_SdtDocPart`
- Children: `w:docPartGallery`, `w:docPartCategory`, `w:docPartUnique`
- Parents: `w:sdtPr`
- Specification: §17.5.2.12 p.524

### w:docPartObj

- Type: `w:CT_SdtDocPart`
- Children: `w:docPartGallery`, `w:docPartCategory`, `w:docPartUnique`
- Parents: `w:sdtPr`
- Specification: §17.5.2.13 p.525

### w:docPartPr

- Type: `w:CT_DocPartPr`
- Children: `w:name`, `w:style`, `w:category`, `w:types`, `w:behaviors`, `w:description`, `w:guid`
- Parents: `w:docPart`
- Specification: §17.12.7 p.798, OI 2.1.311

### w:docPartUnique

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:docPartList`, `w:docPartObj`
- Specification: §17.5.2.14 p.526

### w:docParts

- Type: `w:CT_DocParts`
- Children: `w:docPart`
- Parents: `w:glossaryDocument`
- Specification: §17.12.8 p.798, OI 2.1.312

### w:docVar

- Type: `w:CT_DocVar`
- Attributes: `w:name`!, `w:val`!
- Parents: `w:docVars`
- Specification: §17.15.1.31 p.1032, OI 2.1.403

### w:docVars

- Type: `w:CT_DocVars`
- Children: `w:docVar`
- Parents: `w:settings`
- Specification: §17.15.1.32 p.1033

### w:document

- Type: `w:CT_Document`
- Attributes: `w:conformance`
- Children: `w:background`, `w:body`
- Specification: §17.2.3 p.203, OI 2.1.37

### w:documentProtection

- Type: `w:CT_DocProtect`
- Attributes: `w:algIdExt`, `w:algIdExtSource`, `w:algorithmName`, `w:cryptAlgorithmClass`, `w:cryptAlgorithmSid`, `w:cryptAlgorithmType`, `w:cryptProvider`, `w:cryptProviderType`, `w:cryptProviderTypeExt`, `w:cryptProviderTypeExtSource`, `w:cryptSpinCount`, `w:edit`, `w:enforcement`, `w:formatting`, `w:hash`, `w:hashValue`, `w:salt`, `w:saltValue`, `w:spinCount`
- Parents: `w:settings`
- Specification: §17.15.1.29 p.1026, OI 2.1.402

### w:documentType

- Type: `w:CT_DocType`
- Attributes: `w:val`!
- Parents: `w:settings`
- Specification: §17.15.1.30 p.1031

### w:drawing

- Type: `w:CT_Drawing`
- Children: `wp:anchor`, `wp:inline`
- Parents: `m:r`, `w:background`, `w:numPicBullet`, `w:object`, `w:r`
- Specification: §17.3.3.9 p.342, OI 2.1.104

### w:drawingGridHorizontalOrigin

- Type: `w:CT_TwipsMeasure`
- Attributes: `w:val`!
- Parents: `w:settings`
- Specification: §17.15.1.44 p.1041, OI 2.1.405

### w:drawingGridHorizontalSpacing

- Type: `w:CT_TwipsMeasure`
- Attributes: `w:val`!
- Parents: `w:settings`
- Specification: §17.15.1.45 p.1043

### w:drawingGridVerticalOrigin

- Type: `w:CT_TwipsMeasure`
- Attributes: `w:val`!
- Parents: `w:settings`
- Specification: §17.15.1.46 p.1044, OI 2.1.406

### w:drawingGridVerticalSpacing

- Type: `w:CT_TwipsMeasure`
- Attributes: `w:val`!
- Parents: `w:settings`
- Specification: §17.15.1.47 p.1045

### w:dropDownList

- Type: `w:CT_SdtDropDownList`
- Attributes: `w:lastValue`
- Children: `w:listItem`
- Parents: `w:sdtPr`
- Specification: §17.5.2.15 p.526

### w:dstrike

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:rPr`
- Specification: §17.3.2.9 p.283

### w:dynamicAddress

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:fieldMapData`
- Specification: §17.14.13 p.954, OI 2.1.367

### w:eastAsianLayout

- Type: `w:CT_EastAsianLayout`
- Attributes: `w:combine`, `w:combineBrackets`, `w:id`, `w:vert`, `w:vertCompress`
- Parents: `w:rPr`
- Specification: §17.3.2.10 p.283, OI 2.1.75

### w:effect

- Type: `w:CT_TextEffect`
- Attributes: `w:val`!
- Parents: `w:rPr`
- Specification: §17.3.2.11 p.287, OI 2.1.76

### w:em

- Type: `w:CT_Em`
- Attributes: `w:val`!
- Parents: `w:rPr`
- Specification: §17.3.2.12 p.288

### w:embedBold

- Type: `w:CT_FontRel`
- Attributes: `r:id`!, `w:fontKey`, `w:subsetted`
- Parents: `w:font`
- Specification: §17.8.3.3 p.683, OI 2.1.264

### w:embedBoldItalic

- Type: `w:CT_FontRel`
- Attributes: `r:id`!, `w:fontKey`, `w:subsetted`
- Parents: `w:font`
- Specification: §17.8.3.4 p.686, OI 2.1.265

### w:embedItalic

- Type: `w:CT_FontRel`
- Attributes: `r:id`!, `w:fontKey`, `w:subsetted`
- Parents: `w:font`
- Specification: §17.8.3.5 p.688, OI 2.1.266

### w:embedRegular

- Type: `w:CT_FontRel`
- Attributes: `r:id`!, `w:fontKey`, `w:subsetted`
- Parents: `w:font`
- Specification: §17.8.3.6 p.690, OI 2.1.267

### w:embedSystemFonts

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.8.3.7 p.692

### w:embedTrueTypeFonts

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.8.3.8 p.692, OI 2.1.268

### w:emboss

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:rPr`
- Specification: §17.3.2.13 p.289, OI 2.1.77

### w:enabled

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:ffData`
- Specification: §17.16.14 p.1273

### w:encoding

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:webSettings`
- Specification: §17.15.2.14 p.1113, OI 2.1.435

### w:end

- Type: `w:CT_Border`, `w:CT_TblWidth`
- Attributes: `w:color`, `w:frame`, `w:shadow`, `w:space`, `w:sz`, `w:themeColor`, `w:themeShade`, `w:themeTint`, `w:type`, `w:val`!, `w:w`
- Parents: `w:tblBorders`, `w:tblCellMar`, `w:tcBorders`, `w:tcMar`
- Specification: §17.4.10 p.402, OI 2.1.124; §17.4.11 p.403, OI 2.1.125; §17.4.12 p.403, OI 2.1.126; §17.4.13 p.404, OI 2.1.127

### w:endnote

- Type: `w:CT_FtnEdn`, `w:CT_FtnEdnSepRef`
- Attributes: `w:id`!, `w:type`
- Children: `w:customXml`, `w:sdt`, `w:p`, `w:tbl`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`, `w:altChunk`
- Parents: `w:endnotePr`, `w:endnotes`
- Specification: §17.11.2 p.759, OI 2.1.300; §17.11.3 p.761, OI 2.1.301

### w:endnotePr

- Type: `w:CT_EdnDocProps`, `w:CT_EdnProps`
- Children: `w:pos`, `w:numFmt`, `w:numStart`, `w:numRestart`, `w:endnote`
- Parents: `w:sectPr`, `w:settings`
- Specification: §17.11.4 p.762; §17.11.5 p.763

### w:endnoteRef

- Type: `w:CT_Empty`
- Parents: `m:r`, `w:r`
- Specification: §17.11.6 p.764

### w:endnoteReference

- Type: `w:CT_FtnEdnRef`
- Attributes: `w:customMarkFollows`, `w:id`!
- Parents: `m:r`, `w:r`
- Specification: §17.11.7 p.766, OI 2.1.302

### w:endnotes

- Type: `w:CT_Endnotes`
- Children: `w:endnote`
- Specification: §17.11.8 p.768

### w:entryMacro

- Type: `w:CT_MacroName`
- Attributes: `w:val`!
- Parents: `w:ffData`
- Specification: §17.16.15 p.1274

### w:equation

- Type: `w:CT_Empty`
- Parents: `w:sdtPr`
- Specification: §17.5.2.16 p.528

### w:evenAndOddHeaders

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.10.1 p.744, OI 2.1.294

### w:exitMacro

- Type: `w:CT_MacroName`
- Attributes: `w:val`!
- Parents: `w:ffData`
- Specification: §17.16.16 p.1275

### w:fHdr

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:odso`
- Specification: §17.14.14 p.955, OI 2.1.368

### w:family

- Type: `w:CT_FontFamily`
- Attributes: `w:val`!
- Parents: `w:font`
- Specification: §17.8.3.9 p.693, OI 2.1.269

### w:ffData

- Type: `w:CT_FFData`
- Children: `w:name`, `w:label`, `w:tabIndex`, `w:enabled`, `w:calcOnExit`, `w:entryMacro`, `w:exitMacro`, `w:helpText`, `w:statusText`, `w:checkBox`, `w:ddList`, `w:textInput`
- Parents: `w:fldChar`
- Specification: §17.16.17 p.1276, OI 2.1.518

### w:fieldMapData

- Type: `w:CT_OdsoFieldMapData`
- Children: `w:type`, `w:name`, `w:mappedName`, `w:column`, `w:lid`, `w:dynamicAddress`
- Parents: `w:odso`
- Specification: §17.14.15 p.956, OI 2.1.369

### w:fitText

- Type: `w:CT_FitText`
- Attributes: `w:id`, `w:val`!
- Parents: `w:rPr`
- Specification: §17.3.2.14 p.289, OI 2.1.78

### w:flatBorders

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:framesetSplitbar`
- Specification: §17.15.2.15 p.1114

### w:fldChar

- Type: `w:CT_FldChar`
- Attributes: `w:dirty`, `w:fldCharType`!, `w:fldLock`
- Children: `w:fldData`, `w:ffData`, `w:numberingChange`
- Parents: `m:r`, `w:r`
- Specification: §17.16.18 p.1277

### w:fldData

- Type: `w:CT_Text` (text: `s:ST_String`)
- Attributes: `xml:space`
- Parents: `w:fldChar`, `w:fldSimple`
- Specification: Part 4 §14.10.5 p.196; Part 4 §14.10.6 p.197

### w:fldSimple

- Type: `w:CT_SimpleField`
- Attributes: `w:dirty`, `w:fldLock`, `w:instr`!
- Children: `w:fldData`, `w:customXml`, `w:smartTag`, `w:sdt`, `w:dir`, `w:bdo`, `w:r`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`, `w:fldSimple`, `w:hyperlink`, `w:subDoc`
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:bdo`, `w:customXml`, `w:dir`, `w:fldSimple`, `w:hyperlink`, `w:p`, `w:sdtContent`, `w:smartTag`
- Specification: §17.16.19 p.1281, OI 2.1.519

### w:font

- Type: `w:CT_Font`
- Attributes: `w:name`!
- Children: `w:altName`, `w:panose1`, `w:charset`, `w:family`, `w:notTrueType`, `w:pitch`, `w:sig`, `w:embedRegular`, `w:embedBold`, `w:embedItalic`, `w:embedBoldItalic`
- Parents: `w:fonts`
- Specification: §17.8.3.10 p.693, OI 2.1.270

### w:fonts

- Type: `w:CT_FontsList`
- Children: `w:font`
- Specification: §17.8.3.11 p.694

### w:footerReference

- Type: `w:CT_HdrFtrRef`
- Attributes: `r:id`!, `w:type`!
- Parents: `w:sectPr`
- Specification: §17.10.2 p.745, OI 2.1.295

### w:footnote

- Type: `w:CT_FtnEdn`, `w:CT_FtnEdnSepRef`
- Attributes: `w:id`!, `w:type`
- Children: `w:customXml`, `w:sdt`, `w:p`, `w:tbl`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`, `w:altChunk`
- Parents: `w:footnotePr`, `w:footnotes`
- Specification: §17.11.9 p.769, OI 2.1.303; §17.11.10 p.770, OI 2.1.304

### w:footnoteLayoutLikeWW8

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.20 p.124

### w:footnotePr

- Type: `w:CT_FtnDocProps`, `w:CT_FtnProps`
- Children: `w:pos`, `w:numFmt`, `w:numStart`, `w:numRestart`, `w:footnote`
- Parents: `w:sectPr`, `w:settings`
- Specification: §17.11.11 p.773; §17.11.12 p.774

### w:footnoteRef

- Type: `w:CT_Empty`
- Parents: `m:r`, `w:r`
- Specification: §17.11.13 p.775

### w:footnoteReference

- Type: `w:CT_FtnEdnRef`
- Attributes: `w:customMarkFollows`, `w:id`!
- Parents: `m:r`, `w:r`
- Specification: §17.11.14 p.776, OI 2.1.305

### w:footnotes

- Type: `w:CT_Footnotes`
- Children: `w:footnote`
- Specification: §17.11.15 p.778

### w:forceUpgrade

- Type: `w:CT_Empty`
- Parents: `w:settings`
- Specification: §17.15.1.48 p.1047

### w:forgetLastTabAlignment

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.21 p.127

### w:formProt

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:sectPr`
- Specification: §17.6.6 p.575

### w:format

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:textInput`
- Specification: §17.16.20 p.1283, OI 2.1.520

### w:formsDesign

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.49 p.1047

### w:frame

- Type: `w:CT_Frame`
- Children: `w:sz`, `w:name`, `w:title`, `w:longDesc`, `w:sourceFileName`, `w:marW`, `w:marH`, `w:scrollbar`, `w:noResizeAllowed`, `w:linkedToFile`
- Parents: `w:frameset`
- Specification: §17.15.2.16 p.1115

### w:frameLayout

- Type: `w:CT_FrameLayout`
- Attributes: `w:val`!
- Parents: `w:frameset`
- Specification: §17.15.2.17 p.1117, OI 2.1.436

### w:framePr

- Type: `w:CT_FramePr`
- Attributes: `w:anchorLock`, `w:dropCap`, `w:h`, `w:hAnchor`, `w:hRule`, `w:hSpace`, `w:lines`, `w:vAnchor`, `w:vSpace`, `w:w`, `w:wrap`, `w:x`, `w:xAlign`, `w:y`, `w:yAlign`
- Parents: `w:pPr`
- Specification: §17.3.1.11 p.218, OI 2.1.43

### w:frameset

- Type: `w:CT_Frameset`
- Children: `w:sz`, `w:framesetSplitbar`, `w:frameLayout`, `w:title`, `w:frameset`, `w:frame`
- Parents: `w:frameset`, `w:webSettings`
- Specification: §17.15.2.18 p.1118, OI 2.1.437; §17.15.2.19 p.1120, OI 2.1.438

### w:framesetSplitbar

- Type: `w:CT_FramesetSplitbar`
- Children: `w:w`, `w:color`, `w:noBorder`, `w:flatBorders`
- Parents: `w:frameset`
- Specification: §17.15.2.20 p.1121

### w:ftr

- Type: `w:CT_HdrFtr`
- Children: `w:customXml`, `w:sdt`, `w:p`, `w:tbl`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`, `w:altChunk`
- Specification: §17.10.3 p.748, OI 2.1.296

### w:gallery

- Type: `w:CT_DocPartGallery`
- Attributes: `w:val`!
- Parents: `w:category`
- Specification: §17.12.9 p.799

### w:glossaryDocument

- Type: `w:CT_GlossaryDocument`
- Children: `w:background`, `w:docParts`
- Specification: §17.12.10 p.800

### w:gridAfter

- Type: `w:CT_DecimalNumber`
- Attributes: `w:val`!
- Parents: `w:trPr`
- Specification: §17.4.14 p.405, OI 2.1.128

### w:gridBefore

- Type: `w:CT_DecimalNumber`
- Attributes: `w:val`!
- Parents: `w:trPr`
- Specification: §17.4.15 p.406, OI 2.1.129

### w:gridCol

- Type: `w:CT_TblGridCol`
- Attributes: `w:w`
- Parents: `w:tblGrid`
- Specification: §17.4.16 p.407

### w:gridSpan

- Type: `w:CT_DecimalNumber`
- Attributes: `w:val`!
- Parents: `w:tcPr`
- Specification: §17.4.17 p.409, OI 2.1.130

### w:group

- Type: `w:CT_Empty`
- Parents: `w:sdtPr`
- Specification: §17.5.2.17 p.529

### w:growAutofit

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.22 p.129

### w:guid

- Type: `w:CT_Guid`
- Attributes: `w:val`
- Parents: `w:docPartPr`
- Specification: §17.12.11 p.801

### w:gutterAtTop

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.50 p.1048

### w:hMerge

- Type: `w:CT_HMerge`
- Attributes: `w:val`
- Parents: `w:tcPr`
- Specification: Part 4 §14.4.13 p.59, OI 2.1.1768

### w:hdr

- Type: `w:CT_HdrFtr`
- Children: `w:customXml`, `w:sdt`, `w:p`, `w:tbl`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`, `w:altChunk`
- Specification: §17.10.4 p.750, OI 2.1.297

### w:hdrShapeDefaults

- Type: `w:CT_ShapeDefaults`
- Children: `any(urn:schemas-microsoft-com:office:office)`
- Parents: `w:settings`
- Specification: Part 4 §14.8.2.1 p.82, OI 2.1.1773

### w:header

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:headers`
- Specification: §17.4.18 p.411, OI 2.1.131

### w:headerReference

- Type: `w:CT_HdrFtrRef`
- Attributes: `r:id`!, `w:type`!
- Parents: `w:sectPr`
- Specification: §17.10.5 p.752, OI 2.1.298

### w:headerSource

- Type: `w:CT_Rel`
- Attributes: `r:id`!
- Parents: `w:mailMerge`
- Specification: §17.14.16 p.956, OI 2.1.370

### w:headers

- Type: `w:CT_Headers`
- Children: `w:header`
- Parents: `w:tcPr`
- Specification: §17.4.19 p.413, OI 2.1.132

### w:helpText

- Type: `w:CT_FFHelpText`
- Attributes: `w:type`, `w:val`
- Parents: `w:ffData`
- Specification: §17.16.21 p.1284

### w:hidden

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:style`, `w:trPr`
- Specification: §17.4.20 p.415, OI 2.1.133; §17.7.4.4 p.633

### w:hideGrammaticalErrors

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.51 p.1049

### w:hideMark

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:tcPr`
- Specification: §17.4.21 p.415, OI 2.1.134

### w:hideSpellingErrors

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.52 p.1049

### w:highlight

- Type: `w:CT_Highlight`
- Attributes: `w:val`!
- Parents: `w:rPr`
- Specification: §17.3.2.15 p.291, OI 2.1.79

### w:hps

- Type: `w:CT_HpsMeasure`
- Attributes: `w:val`!
- Parents: `w:rubyPr`
- Specification: §17.3.3.10 p.343

### w:hpsBaseText

- Type: `w:CT_HpsMeasure`
- Attributes: `w:val`!
- Parents: `w:rubyPr`
- Specification: §17.3.3.11 p.343

### w:hpsRaise

- Type: `w:CT_HpsMeasure`
- Attributes: `w:val`!
- Parents: `w:rubyPr`
- Specification: §17.3.3.12 p.344, OI 2.1.105

### w:hyperlink

- Type: `w:CT_Hyperlink`
- Attributes: `r:id`, `w:anchor`, `w:docLocation`, `w:history`, `w:tgtFrame`, `w:tooltip`
- Children: `w:customXml`, `w:smartTag`, `w:sdt`, `w:dir`, `w:bdo`, `w:r`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`, `w:fldSimple`, `w:hyperlink`, `w:subDoc`
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:bdo`, `w:customXml`, `w:dir`, `w:fldSimple`, `w:hyperlink`, `w:p`, `w:sdtContent`, `w:smartTag`
- Specification: §17.16.22 p.1286, OI 2.1.521; Part 4 §14.10.7 p.198

### w:hyphenationZone

- Type: `w:CT_TwipsMeasure`
- Attributes: `w:val`!
- Parents: `w:settings`
- Specification: §17.15.1.53 p.1049, OI 2.1.407

### w:i

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:rPr`
- Specification: §17.3.2.16 p.292, OI 2.1.80

### w:iCs

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:rPr`
- Specification: §17.3.2.17 p.292, OI 2.1.81

### w:id

- Type: `w:CT_DecimalNumber`
- Attributes: `w:val`!
- Parents: `w:sdtPr`
- Specification: §17.5.2.18 p.529, OI 2.1.196

### w:ignoreMixedContent

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.54 p.1051

### w:ilvl

- Type: `w:CT_DecimalNumber`
- Attributes: `w:val`!
- Parents: `w:numPr`
- Specification: §17.9.3 p.706, OI 2.1.277

### w:imprint

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:rPr`
- Specification: §17.3.2.18 p.293, OI 2.1.82

### w:ind

- Type: `w:CT_Ind`
- Attributes: `w:end`, `w:endChars`, `w:firstLine`, `w:firstLineChars`, `w:hanging`, `w:hangingChars`, `w:left`, `w:leftChars`, `w:right`, `w:rightChars`, `w:start`, `w:startChars`
- Parents: `w:pPr`
- Specification: §17.3.1.12 p.229, OI 2.1.44

### w:ins

- Type: `w:CT_RunTrackChange`, `w:CT_MathCtrlIns`, `w:CT_TrackChange`
- Attributes: `w:author`!, `w:date`, `w:id`!
- Children: `w:customXml`, `w:smartTag`, `w:sdt`, `w:dir`, `w:bdo`, `w:r`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`, `m:acc`, `m:bar`, `m:box`, `m:borderBox`, `m:d`, `m:eqArr`, `m:f`, `m:func`, `m:groupChr`, `m:limLow`, `m:limUpp`, `m:m`, `m:nary`, `m:phant`, `m:rad`, `m:sPre`, `m:sSub`, `m:sSubSup`, `m:sSup`, `m:r`, `w:rPr`
- Parents: `m:ctrlPr`, `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:bdo`, `w:body`, `w:comment`, `w:customXml`, `w:del`, `w:dir`, `w:docPartBody`, `w:endnote`, `w:fldSimple`, `w:footnote`, `w:ftr`, `w:hdr`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:numPr`, `w:p`, `w:rPr`, `w:rt`, `w:rubyBase`, `w:sdtContent`, `w:smartTag`, `w:tbl`, `w:tc`, `w:tr`, `w:trPr`, `w:txbxContent`, `wne:txbxContent`, `wp:txbxContent`
- Specification: §17.13.5.16 p.859, OI 2.1.332; §17.13.5.17 p.860, OI 2.1.333; §17.13.5.18 p.862, OI 2.1.334; §17.13.5.19 p.864, OI 2.1.335; §17.13.5.20 p.867, OI 2.1.336

### w:insideH

- Type: `w:CT_Border`
- Attributes: `w:color`, `w:frame`, `w:shadow`, `w:space`, `w:sz`, `w:themeColor`, `w:themeShade`, `w:themeTint`, `w:val`!
- Parents: `w:tblBorders`, `w:tcBorders`
- Specification: §17.4.22 p.416, OI 2.1.135; §17.4.23 p.418, OI 2.1.136

### w:insideV

- Type: `w:CT_Border`
- Attributes: `w:color`, `w:frame`, `w:shadow`, `w:space`, `w:sz`, `w:themeColor`, `w:themeShade`, `w:themeTint`, `w:val`!
- Parents: `w:tblBorders`, `w:tcBorders`
- Specification: §17.4.24 p.419, OI 2.1.137; §17.4.25 p.419, OI 2.1.138

### w:instrText

- Type: `w:CT_Text` (text: `s:ST_String`)
- Attributes: `xml:space`
- Parents: `m:r`, `w:r`
- Specification: §17.16.23 p.1289

### w:isLgl

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:lvl`
- Specification: §17.9.4 p.707, OI 2.1.278

### w:jc

- Type: `w:CT_Jc`, `w:CT_JcTable`
- Attributes: `w:val`!
- Parents: `w:pPr`, `w:tblPr`, `w:tblPrEx`, `w:trPr`
- Specification: §17.3.1.13 p.234, OI 2.1.45; §17.4.26 p.420, OI 2.1.139; §17.4.27 p.421, OI 2.1.140; §17.4.28 p.422, OI 2.1.141

### w:keepLines

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:pPr`
- Specification: §17.3.1.14 p.235, OI 2.1.46

### w:keepNext

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:pPr`
- Specification: §17.3.1.15 p.236

### w:kern

- Type: `w:CT_HpsMeasure`
- Attributes: `w:val`!
- Parents: `w:rPr`
- Specification: §17.3.2.19 p.293, OI 2.1.83

### w:kinsoku

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:pPr`
- Specification: §17.3.1.16 p.238, OI 2.1.47

### w:label

- Type: `w:CT_DecimalNumber`
- Attributes: `w:val`!
- Parents: `w:ffData`, `w:sdtPr`
- Specification: §17.5.2.19 p.530, OI 2.1.197; §17.16.24 p.1290, OI 2.1.522

### w:lang

- Type: `w:CT_Language`
- Attributes: `w:bidi`, `w:eastAsia`, `w:val`
- Parents: `w:rPr`
- Specification: §17.3.2.20 p.295, OI 2.1.84

### w:lastRenderedPageBreak

- Type: `w:CT_Empty`
- Parents: `m:r`, `w:r`
- Specification: §17.3.3.13 p.345, OI 2.1.106

### w:latentStyles

- Type: `w:CT_LatentStyles`
- Attributes: `w:count`, `w:defLockedState`, `w:defQFormat`, `w:defSemiHidden`, `w:defUIPriority`, `w:defUnhideWhenUsed`
- Children: `w:lsdException`
- Parents: `w:styles`
- Specification: §17.7.4.5 p.634, OI 2.1.234

### w:layoutRawTableWidth

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.23 p.130

### w:layoutTableRowsApart

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.24 p.132

### w:left

- Type: `w:CT_Border`, `w:CT_PageBorder`, `w:CT_TblWidth`
- Attributes: `r:id`, `w:color`, `w:frame`, `w:shadow`, `w:space`, `w:sz`, `w:themeColor`, `w:themeShade`, `w:themeTint`, `w:type`, `w:val`!, `w:w`
- Parents: `w:divBdr`, `w:pBdr`, `w:pgBorders`, `w:tblBorders`, `w:tblCellMar`, `w:tcBorders`, `w:tcMar`
- Specification: §17.3.1.17 p.241, OI 2.1.48; §17.6.7 p.575, OI 2.1.214; §17.15.2.21 p.1122, OI 2.1.439; Part 4 §14.4.1 p.54, OI 2.1.1758; Part 4 §14.4.2 p.54, OI 2.1.1759; Part 4 §14.4.3 p.54, OI 2.1.1760; Part 4 §14.4.4 p.54, OI 2.1.1761

### w:legacy

- Type: `w:CT_LvlLegacy`
- Attributes: `w:legacy`, `w:legacyIndent`, `w:legacySpace`
- Parents: `w:lvl`
- Specification: Part 4 §14.6.2 p.64, OI 2.1.1770

### w:lid

- Type: `w:CT_Lang`
- Attributes: `w:val`!
- Parents: `w:date`, `w:fieldMapData`, `w:rubyPr`
- Specification: §17.3.3.14 p.346, OI 2.1.107; §17.5.2.20 p.532, OI 2.1.198; §17.14.17 p.958, OI 2.1.371

### w:lineWrapLikeWord6

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.25 p.133

### w:link

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:style`
- Specification: §17.7.4.6 p.638, OI 2.1.235

### w:linkStyles

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.55 p.1052

### w:linkToQuery

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:mailMerge`
- Specification: §17.14.18 p.959, OI 2.1.372

### w:linkedToFile

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:frame`
- Specification: §17.15.2.22 p.1123

### w:listEntry

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:ddList`
- Specification: §17.16.25 p.1292, OI 2.1.523

### w:listItem

- Type: `w:CT_SdtListItem`
- Attributes: `w:displayText`, `w:value`
- Parents: `w:comboBox`, `w:dropDownList`
- Specification: §17.5.2.21 p.532; §17.5.2.22 p.534

### w:listSeparator

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:settings`
- Specification: §17.15.1.56 p.1053, OI 2.1.408

### w:lnNumType

- Type: `w:CT_LineNumber`
- Attributes: `w:countBy`, `w:distance`, `w:restart`, `w:start`
- Parents: `w:sectPr`
- Specification: §17.6.8 p.583, OI 2.1.215

### w:lock

- Type: `w:CT_Lock`
- Attributes: `w:val`
- Parents: `w:sdtPr`
- Specification: §17.5.2.23 p.536

### w:locked

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:style`
- Specification: §17.7.4.7 p.639

### w:longDesc

- Type: `w:CT_Rel`
- Attributes: `r:id`!
- Parents: `w:frame`
- Specification: §17.15.2.23 p.1124, OI 2.1.440

### w:lsdException

- Type: `w:CT_LsdException`
- Attributes: `w:locked`, `w:name`!, `w:qFormat`, `w:semiHidden`, `w:uiPriority`, `w:unhideWhenUsed`
- Parents: `w:latentStyles`
- Specification: §17.7.4.8 p.640, OI 2.1.236

### w:lvl

- Type: `w:CT_Lvl`
- Attributes: `w:ilvl`!, `w:tentative`, `w:tplc`
- Children: `w:start`, `w:numFmt`, `w:lvlRestart`, `w:pStyle`, `w:isLgl`, `w:suff`, `w:lvlText`, `w:lvlPicBulletId`, `w:legacy`, `w:lvlJc`, `w:pPr`, `w:rPr`
- Parents: `w:abstractNum`, `w:lvlOverride`
- Specification: §17.9.5 p.708, OI 2.1.279; §17.9.6 p.711, OI 2.1.280

### w:lvlJc

- Type: `w:CT_Jc`
- Attributes: `w:val`!
- Parents: `w:lvl`
- Specification: §17.9.7 p.714, OI 2.1.281

### w:lvlOverride

- Type: `w:CT_NumLvl`
- Attributes: `w:ilvl`!
- Children: `w:startOverride`, `w:lvl`
- Parents: `w:num`
- Specification: §17.9.8 p.715

### w:lvlPicBulletId

- Type: `w:CT_DecimalNumber`
- Attributes: `w:val`!
- Parents: `w:lvl`
- Specification: §17.9.9 p.717

### w:lvlRestart

- Type: `w:CT_DecimalNumber`
- Attributes: `w:val`!
- Parents: `w:lvl`
- Specification: §17.9.10 p.718, OI 2.1.282

### w:lvlText

- Type: `w:CT_LevelText`
- Attributes: `w:null`, `w:val`
- Parents: `w:lvl`
- Specification: §17.9.11 p.721, OI 2.1.283

### w:mailAsAttachment

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:mailMerge`
- Specification: §17.14.19 p.959

### w:mailMerge

- Type: `w:CT_MailMerge`
- Children: `w:mainDocumentType`, `w:linkToQuery`, `w:dataType`, `w:connectString`, `w:query`, `w:dataSource`, `w:headerSource`, `w:doNotSuppressBlankLines`, `w:destination`, `w:addressFieldName`, `w:mailSubject`, `w:mailAsAttachment`, `w:viewMergedData`, `w:activeRecord`, `w:checkErrors`, `w:odso`
- Parents: `w:settings`
- Specification: §17.14.20 p.960, OI 2.1.373

### w:mailSubject

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:mailMerge`
- Specification: §17.14.21 p.961

### w:mainDocumentType

- Type: `w:CT_MailMergeDocType`
- Attributes: `w:val`!
- Parents: `w:mailMerge`
- Specification: §17.14.22 p.962

### w:mappedName

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:fieldMapData`
- Specification: §17.14.23 p.963, OI 2.1.374

### w:marBottom

- Type: `w:CT_SignedTwipsMeasure`
- Attributes: `w:val`!
- Parents: `w:div`
- Specification: §17.15.2.24 p.1127

### w:marH

- Type: `w:CT_PixelsMeasure`
- Attributes: `w:val`!
- Parents: `w:frame`
- Specification: §17.15.2.25 p.1128

### w:marLeft

- Type: `w:CT_SignedTwipsMeasure`
- Attributes: `w:val`!
- Parents: `w:div`
- Specification: §17.15.2.26 p.1130

### w:marRight

- Type: `w:CT_SignedTwipsMeasure`
- Attributes: `w:val`!
- Parents: `w:div`
- Specification: §17.15.2.27 p.1131

### w:marTop

- Type: `w:CT_SignedTwipsMeasure`
- Attributes: `w:val`!
- Parents: `w:div`
- Specification: §17.15.2.28 p.1133

### w:marW

- Type: `w:CT_PixelsMeasure`
- Attributes: `w:val`!
- Parents: `w:frame`
- Specification: §17.15.2.29 p.1135

### w:matchSrc

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:altChunkPr`
- Specification: §17.17.2.3 p.1310

### w:maxLength

- Type: `w:CT_DecimalNumber`
- Attributes: `w:val`!
- Parents: `w:textInput`
- Specification: §17.16.26 p.1293, OI 2.1.524

### w:mirrorIndents

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:pPr`
- Specification: §17.3.1.18 p.242, OI 2.1.49

### w:mirrorMargins

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.57 p.1054

### w:monthLong

- Type: `w:CT_Empty`
- Parents: `m:r`, `w:r`
- Specification: §17.3.3.15 p.346

### w:monthShort

- Type: `w:CT_Empty`
- Parents: `m:r`, `w:r`
- Specification: §17.3.3.16 p.347

### w:moveFrom

- Type: `w:CT_RunTrackChange`, `w:CT_TrackChange`
- Attributes: `w:author`!, `w:date`, `w:id`!
- Children: `w:customXml`, `w:smartTag`, `w:sdt`, `w:dir`, `w:bdo`, `w:r`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`, `m:acc`, `m:bar`, `m:box`, `m:borderBox`, `m:d`, `m:eqArr`, `m:f`, `m:func`, `m:groupChr`, `m:limLow`, `m:limUpp`, `m:m`, `m:nary`, `m:phant`, `m:rad`, `m:sPre`, `m:sSub`, `m:sSubSup`, `m:sSup`, `m:r`
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:bdo`, `w:body`, `w:comment`, `w:customXml`, `w:del`, `w:dir`, `w:docPartBody`, `w:endnote`, `w:fldSimple`, `w:footnote`, `w:ftr`, `w:hdr`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:p`, `w:rPr`, `w:rt`, `w:rubyBase`, `w:sdtContent`, `w:smartTag`, `w:tbl`, `w:tc`, `w:tr`, `w:txbxContent`, `wne:txbxContent`, `wp:txbxContent`
- Specification: §17.13.5.21 p.869, OI 2.1.337; §17.13.5.22 p.871, OI 2.1.338

### w:moveFromRangeEnd

- Type: `w:CT_MarkupRange`
- Attributes: `w:displacedByCustomXml`, `w:id`!
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:bdo`, `w:body`, `w:comment`, `w:customXml`, `w:del`, `w:dir`, `w:docPartBody`, `w:endnote`, `w:fldSimple`, `w:footnote`, `w:ftr`, `w:hdr`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:p`, `w:rt`, `w:rubyBase`, `w:sdtContent`, `w:smartTag`, `w:tbl`, `w:tc`, `w:tr`, `w:txbxContent`, `wne:txbxContent`, `wp:txbxContent`
- Specification: §17.13.5.23 p.873, OI 2.1.339

### w:moveFromRangeStart

- Type: `w:CT_MoveBookmark`
- Attributes: `w:author`!, `w:colFirst`, `w:colLast`, `w:date`!, `w:displacedByCustomXml`, `w:id`!, `w:name`!
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:bdo`, `w:body`, `w:comment`, `w:customXml`, `w:del`, `w:dir`, `w:docPartBody`, `w:endnote`, `w:fldSimple`, `w:footnote`, `w:ftr`, `w:hdr`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:p`, `w:rt`, `w:rubyBase`, `w:sdtContent`, `w:smartTag`, `w:tbl`, `w:tc`, `w:tr`, `w:txbxContent`, `wne:txbxContent`, `wp:txbxContent`
- Specification: §17.13.5.24 p.877, OI 2.1.340

### w:moveTo

- Type: `w:CT_RunTrackChange`, `w:CT_TrackChange`
- Attributes: `w:author`!, `w:date`, `w:id`!
- Children: `w:customXml`, `w:smartTag`, `w:sdt`, `w:dir`, `w:bdo`, `w:r`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`, `m:acc`, `m:bar`, `m:box`, `m:borderBox`, `m:d`, `m:eqArr`, `m:f`, `m:func`, `m:groupChr`, `m:limLow`, `m:limUpp`, `m:m`, `m:nary`, `m:phant`, `m:rad`, `m:sPre`, `m:sSub`, `m:sSubSup`, `m:sSup`, `m:r`
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:bdo`, `w:body`, `w:comment`, `w:customXml`, `w:del`, `w:dir`, `w:docPartBody`, `w:endnote`, `w:fldSimple`, `w:footnote`, `w:ftr`, `w:hdr`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:p`, `w:rPr`, `w:rt`, `w:rubyBase`, `w:sdtContent`, `w:smartTag`, `w:tbl`, `w:tc`, `w:tr`, `w:txbxContent`, `wne:txbxContent`, `wp:txbxContent`
- Specification: §17.13.5.25 p.884, OI 2.1.341; §17.13.5.26 p.887, OI 2.1.342

### w:moveToRangeEnd

- Type: `w:CT_MarkupRange`
- Attributes: `w:displacedByCustomXml`, `w:id`!
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:bdo`, `w:body`, `w:comment`, `w:customXml`, `w:del`, `w:dir`, `w:docPartBody`, `w:endnote`, `w:fldSimple`, `w:footnote`, `w:ftr`, `w:hdr`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:p`, `w:rt`, `w:rubyBase`, `w:sdtContent`, `w:smartTag`, `w:tbl`, `w:tc`, `w:tr`, `w:txbxContent`, `wne:txbxContent`, `wp:txbxContent`
- Specification: §17.13.5.27 p.889, OI 2.1.343

### w:moveToRangeStart

- Type: `w:CT_MoveBookmark`
- Attributes: `w:author`!, `w:colFirst`, `w:colLast`, `w:date`!, `w:displacedByCustomXml`, `w:id`!, `w:name`!
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:bdo`, `w:body`, `w:comment`, `w:customXml`, `w:del`, `w:dir`, `w:docPartBody`, `w:endnote`, `w:fldSimple`, `w:footnote`, `w:ftr`, `w:hdr`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:p`, `w:rt`, `w:rubyBase`, `w:sdtContent`, `w:smartTag`, `w:tbl`, `w:tc`, `w:tr`, `w:txbxContent`, `wne:txbxContent`, `wp:txbxContent`
- Specification: §17.13.5.28 p.892, OI 2.1.344

### w:movie

- Type: `w:CT_Rel`
- Attributes: `r:id`!
- Parents: `w:object`, `w:pict`
- Specification: §17.3.3.17 p.348, OI 2.1.108

### w:multiLevelType

- Type: `w:CT_MultiLevelType`
- Attributes: `w:val`!
- Parents: `w:abstractNum`
- Specification: §17.9.12 p.722

### w:mwSmallCaps

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.26 p.134

### w:name

- Type: `w:CT_FFName`, `w:CT_String`, `w:CT_DocPartName`
- Attributes: `w:decorated`, `w:val`
- Parents: `w:abstractNum`, `w:category`, `w:docPartPr`, `w:ffData`, `w:fieldMapData`, `w:frame`, `w:style`
- Specification: §17.7.4.9 p.643, OI 2.1.237; §17.9.13 p.723, OI 2.1.284; §17.12.12 p.801; §17.12.13 p.803; §17.14.24 p.965, OI 2.1.375; §17.15.2.30 p.1136, OI 2.1.441; §17.16.27 p.1294

### w:next

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:style`
- Specification: §17.7.4.10 p.644, OI 2.1.238

### w:noBorder

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:framesetSplitbar`
- Specification: §17.15.2.31 p.1138

### w:noBreakHyphen

- Type: `w:CT_Empty`
- Parents: `m:r`, `w:r`
- Specification: §17.3.3.18 p.349

### w:noColumnBalance

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.27 p.135

### w:noEndnote

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:sectPr`
- Specification: §17.11.16 p.779

### w:noExtraLineSpacing

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.28 p.137

### w:noLeading

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.29 p.138

### w:noLineBreaksAfter

- Type: `w:CT_Kinsoku`
- Attributes: `w:lang`!, `w:val`!
- Parents: `w:settings`
- Specification: §17.15.1.58 p.1055, OI 2.1.409

### w:noLineBreaksBefore

- Type: `w:CT_Kinsoku`
- Attributes: `w:lang`!, `w:val`!
- Parents: `w:settings`
- Specification: §17.15.1.59 p.1056, OI 2.1.410

### w:noProof

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:rPr`
- Specification: §17.3.2.21 p.297, OI 2.1.85

### w:noPunctuationKerning

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.60 p.1058, OI 2.1.411

### w:noResizeAllowed

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:frame`
- Specification: §17.15.2.32 p.1140

### w:noSpaceRaiseLower

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.30 p.140

### w:noTabHangInd

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.31 p.140

### w:noWrap

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:tcPr`
- Specification: §17.4.29 p.423

### w:notTrueType

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:font`
- Specification: §17.8.3.12 p.695, OI 2.1.271

### w:nsid

- Type: `w:CT_LongHexNumber`
- Attributes: `w:val`!
- Parents: `w:abstractNum`
- Specification: §17.9.14 p.724

### w:num

- Type: `w:CT_Num`
- Attributes: `w:numId`!
- Children: `w:abstractNumId`, `w:lvlOverride`
- Parents: `w:numbering`
- Specification: §17.9.15 p.725, OI 2.1.285

### w:numFmt

- Type: `w:CT_NumFmt`
- Attributes: `w:format`, `w:val`!
- Parents: `w:endnotePr`, `w:footnotePr`, `w:lvl`
- Specification: §17.9.17 p.728, OI 2.1.286; §17.11.17 p.779, OI 2.1.306; §17.11.18 p.781, OI 2.1.307

### w:numId

- Type: `w:CT_DecimalNumber`
- Attributes: `w:val`!
- Parents: `w:numPr`
- Specification: §17.9.18 p.729, OI 2.1.287

### w:numIdMacAtCleanup

- Type: `w:CT_DecimalNumber`
- Attributes: `w:val`!
- Parents: `w:numbering`
- Specification: §17.9.19 p.730

### w:numPicBullet

- Type: `w:CT_NumPicBullet`
- Attributes: `w:numPicBulletId`!
- Children: `w:pict`, `w:drawing`
- Parents: `w:numbering`
- Specification: §17.9.20 p.731

### w:numPr

- Type: `w:CT_NumPr`
- Children: `w:ilvl`, `w:numId`, `w:numberingChange`, `w:ins`
- Parents: `w:pPr`
- Specification: §17.3.1.19 p.243, OI 2.1.50

### w:numRestart

- Type: `w:CT_NumRestart`
- Attributes: `w:val`!
- Parents: `w:endnotePr`, `w:footnotePr`
- Specification: §17.11.19 p.782

### w:numStart

- Type: `w:CT_DecimalNumber`
- Attributes: `w:val`!
- Parents: `w:endnotePr`, `w:footnotePr`
- Specification: §17.11.20 p.783, OI 2.1.308

### w:numStyleLink

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:abstractNum`
- Specification: §17.9.21 p.733, OI 2.1.288

### w:numbering

- Type: `w:CT_Numbering`
- Children: `w:numPicBullet`, `w:abstractNum`, `w:num`, `w:numIdMacAtCleanup`
- Specification: §17.9.16 p.728

### w:numberingChange

- Type: `w:CT_TrackChangeNumbering`
- Attributes: `w:author`!, `w:date`, `w:id`!, `w:original`
- Parents: `w:fldChar`, `w:numPr`
- Specification: Part 4 §14.7.1.1 p.65, OI 2.1.1771; Part 4 §14.7.1.2 p.69, OI 2.1.1772

### w:oMath

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:rPr`
- Specification: §17.3.2.22 p.297, OI 2.1.86

### w:object

- Type: `w:CT_Object`
- Attributes: `w:dxaOrig`, `w:dyaOrig`
- Children: `w:drawing`, `w:control`, `w:objectLink`, `w:objectEmbed`, `w:movie`, `any(urn:schemas-microsoft-com:vml)`, `any(urn:schemas-microsoft-com:office:office)`
- Parents: `m:r`, `w:r`
- Specification: §17.3.3.19 p.350, OI 2.1.109

### w:objectEmbed

- Type: `w:CT_ObjectEmbed`
- Attributes: `r:id`!, `w:drawAspect`, `w:fieldCodes`, `w:progId`, `w:shapeId`
- Parents: `w:object`
- Specification: §17.3.3.20 p.351, OI 2.1.110

### w:objectLink

- Type: `w:CT_ObjectLink`
- Attributes: `r:id`!, `w:drawAspect`, `w:fieldCodes`, `w:lockedField`, `w:progId`, `w:shapeId`, `w:updateMode`!
- Parents: `w:object`
- Specification: §17.3.3.21 p.354, OI 2.1.111

### w:odso

- Type: `w:CT_Odso`
- Children: `w:udl`, `w:table`, `w:src`, `w:colDelim`, `w:type`, `w:fHdr`, `w:fieldMapData`, `w:recipientData`
- Parents: `w:mailMerge`
- Specification: §17.14.25 p.966, OI 2.1.376

### w:optimizeForBrowser

- Type: `w:CT_OptimizeForBrowser`
- Attributes: `w:target`, `w:val`
- Parents: `w:webSettings`
- Specification: §17.15.2.33 p.1141, OI 2.1.442

### w:outline

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:rPr`
- Specification: §17.3.2.23 p.298

### w:outlineLvl

- Type: `w:CT_DecimalNumber`
- Attributes: `w:val`!
- Parents: `w:pPr`
- Specification: §17.3.1.20 p.243, OI 2.1.51

### w:overflowPunct

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:pPr`
- Specification: §17.3.1.21 p.244, OI 2.1.52

### w:p

- Type: `w:CT_P`
- Attributes: `w:rsidDel`, `w:rsidP`, `w:rsidR`, `w:rsidRDefault`, `w:rsidRPr`
- Children: `w:pPr`, `w:customXml`, `w:smartTag`, `w:sdt`, `w:dir`, `w:bdo`, `w:r`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`, `w:fldSimple`, `w:hyperlink`, `w:subDoc`
- Parents: `w:body`, `w:comment`, `w:customXml`, `w:docPartBody`, `w:endnote`, `w:footnote`, `w:ftr`, `w:hdr`, `w:sdtContent`, `w:tc`, `w:txbxContent`, `wne:txbxContent`, `wp:txbxContent`
- Specification: §17.3.1.22 p.245, OI 2.1.53

### w:pBdr

- Type: `w:CT_PBdr`
- Children: `w:top`, `w:left`, `w:bottom`, `w:right`, `w:between`, `w:bar`
- Parents: `w:pPr`
- Specification: §17.3.1.24 p.249

### w:pPr

- Type: `w:CT_PPrBase`, `w:CT_PPr`, `w:CT_PPrGeneral`
- Children: `w:pStyle`, `w:keepNext`, `w:keepLines`, `w:pageBreakBefore`, `w:framePr`, `w:widowControl`, `w:numPr`, `w:suppressLineNumbers`, `w:pBdr`, `w:shd`, `w:tabs`, `w:suppressAutoHyphens`, `w:kinsoku`, `w:wordWrap`, `w:overflowPunct`, `w:topLinePunct`, `w:autoSpaceDE`, `w:autoSpaceDN`, `w:bidi`, `w:adjustRightInd`, `w:snapToGrid`, `w:spacing`, `w:ind`, `w:contextualSpacing`, `w:mirrorIndents`, `w:suppressOverlap`, `w:jc`, `w:textDirection`, `w:textAlignment`, `w:textboxTightWrap`, `w:outlineLvl`, `w:divId`, `w:cnfStyle`, `w:rPr`, `w:sectPr`, `w:pPrChange`
- Parents: `w:lvl`, `w:p`, `w:pPrChange`, `w:pPrDefault`, `w:style`, `w:tblStylePr`
- Specification: §17.3.1.25 p.249, OI 2.1.54; §17.3.1.26 p.250; §17.7.5.2 p.658, OI 2.1.244; §17.7.6.1 p.664, OI 2.1.247; §17.7.8.2 p.677, OI 2.1.259; §17.9.22 p.734, OI 2.1.289

### w:pPrChange

- Type: `w:CT_PPrChange`
- Attributes: `w:author`!, `w:date`, `w:id`!
- Children: `w:pPr`
- Parents: `w:pPr`
- Specification: §17.13.5.29 p.899, OI 2.1.345

### w:pPrDefault

- Type: `w:CT_PPrDefault`
- Children: `w:pPr`
- Parents: `w:docDefaults`
- Specification: §17.7.5.3 p.659

### w:pStyle

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:lvl`, `w:pPr`
- Specification: §17.3.1.27 p.250, OI 2.1.55; §17.9.23 p.735, OI 2.1.290

### w:pageBreakBefore

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:pPr`
- Specification: §17.3.1.23 p.247

### w:panose1

- Type: `w:CT_Panose`
- Attributes: `w:val`!
- Parents: `w:font`
- Specification: §17.8.3.13 p.695, OI 2.1.272

### w:paperSrc

- Type: `w:CT_PaperSource`
- Attributes: `w:first`, `w:other`
- Parents: `w:sectPr`
- Specification: §17.6.9 p.585, OI 2.1.216

### w:permEnd

- Type: `w:CT_Perm`
- Attributes: `w:displacedByCustomXml`, `w:id`!
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:bdo`, `w:body`, `w:comment`, `w:customXml`, `w:del`, `w:dir`, `w:docPartBody`, `w:endnote`, `w:fldSimple`, `w:footnote`, `w:ftr`, `w:hdr`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:p`, `w:rt`, `w:rubyBase`, `w:sdtContent`, `w:smartTag`, `w:tbl`, `w:tc`, `w:tr`, `w:txbxContent`, `wne:txbxContent`, `wp:txbxContent`
- Specification: §17.13.7.1 p.927, OI 2.1.357

### w:permStart

- Type: `w:CT_PermStart`
- Attributes: `w:colFirst`, `w:colLast`, `w:displacedByCustomXml`, `w:ed`, `w:edGrp`, `w:id`!
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:bdo`, `w:body`, `w:comment`, `w:customXml`, `w:del`, `w:dir`, `w:docPartBody`, `w:endnote`, `w:fldSimple`, `w:footnote`, `w:ftr`, `w:hdr`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:p`, `w:rt`, `w:rubyBase`, `w:sdtContent`, `w:smartTag`, `w:tbl`, `w:tc`, `w:tr`, `w:txbxContent`, `wne:txbxContent`, `wp:txbxContent`
- Specification: §17.13.7.2 p.929, OI 2.1.358

### w:personal

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:style`
- Specification: §17.7.4.11 p.646, OI 2.1.239

### w:personalCompose

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:style`
- Specification: §17.7.4.12 p.646, OI 2.1.240

### w:personalReply

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:style`
- Specification: §17.7.4.13 p.647, OI 2.1.241

### w:pgBorders

- Type: `w:CT_PageBorders`
- Attributes: `w:display`, `w:offsetFrom`, `w:zOrder`
- Children: `w:top`, `w:left`, `w:bottom`, `w:right`
- Parents: `w:sectPr`
- Specification: §17.6.10 p.586, OI 2.1.217

### w:pgMar

- Type: `w:CT_PageMar`
- Attributes: `w:bottom`!, `w:footer`!, `w:gutter`!, `w:header`!, `w:left`!, `w:right`!, `w:top`!
- Parents: `w:sectPr`
- Specification: §17.6.11 p.589, OI 2.1.218

### w:pgNum

- Type: `w:CT_Empty`
- Parents: `m:r`, `w:r`
- Specification: §17.3.3.22 p.356

### w:pgNumType

- Type: `w:CT_PageNumber`
- Attributes: `w:chapSep`, `w:chapStyle`, `w:fmt`, `w:start`
- Parents: `w:sectPr`
- Specification: §17.6.12 p.593, OI 2.1.219

### w:pgSz

- Type: `w:CT_PageSz`
- Attributes: `w:code`, `w:h`, `w:orient`, `w:w`
- Parents: `w:sectPr`
- Specification: §17.6.13 p.595, OI 2.1.220

### w:pict

- Type: `w:CT_Picture`
- Children: `w:movie`, `w:control`, `any(urn:schemas-microsoft-com:vml)`, `any(urn:schemas-microsoft-com:office:office)`
- Parents: `m:r`, `w:numPicBullet`, `w:r`
- Specification: Part 4 §14.3.2.2 p.53, OI 2.1.1757; Part 4 §14.6.1 p.63, OI 2.1.1769

### w:picture

- Type: `w:CT_Empty`
- Parents: `w:sdtPr`
- Specification: §17.5.2.24 p.538

### w:pitch

- Type: `w:CT_Pitch`
- Attributes: `w:val`!
- Parents: `w:font`
- Specification: §17.8.3.14 p.696, OI 2.1.273

### w:pixelsPerInch

- Type: `w:CT_DecimalNumber`
- Attributes: `w:val`!
- Parents: `w:webSettings`
- Specification: §17.15.2.34 p.1142, OI 2.1.443

### w:placeholder

- Type: `w:CT_Placeholder`, `w:CT_String`
- Attributes: `w:val`!
- Children: `w:docPart`
- Parents: `w:customXmlPr`, `w:sdtPr`
- Specification: §17.5.1.8 p.506; §17.5.2.25 p.538, OI 2.1.199

### w:pos

- Type: `w:CT_FtnPos`, `w:CT_EdnPos`
- Attributes: `w:val`!
- Parents: `w:endnotePr`, `w:footnotePr`
- Specification: §17.11.21 p.785, OI 2.1.309; §17.11.22 p.787

### w:position

- Type: `w:CT_SignedHpsMeasure`
- Attributes: `w:val`!
- Parents: `w:rPr`
- Specification: §17.3.2.24 p.299

### w:printBodyTextBeforeHeader

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.32 p.142

### w:printColBlack

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.33 p.143

### w:printFormsData

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.61 p.1058

### w:printFractionalCharacterWidth

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.62 p.1060, OI 2.1.412

### w:printPostScriptOverText

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.63 p.1060, OI 2.1.413

### w:printTwoOnOne

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.64 p.1060

### w:printerSettings

- Type: `w:CT_Rel`
- Attributes: `r:id`!
- Parents: `w:sectPr`
- Specification: §17.6.14 p.597, OI 2.1.221

### w:proofErr

- Type: `w:CT_ProofErr`
- Attributes: `w:type`!
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:bdo`, `w:body`, `w:comment`, `w:customXml`, `w:del`, `w:dir`, `w:docPartBody`, `w:endnote`, `w:fldSimple`, `w:footnote`, `w:ftr`, `w:hdr`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:p`, `w:rt`, `w:rubyBase`, `w:sdtContent`, `w:smartTag`, `w:tbl`, `w:tc`, `w:tr`, `w:txbxContent`, `wne:txbxContent`, `wp:txbxContent`
- Specification: §17.13.8.1 p.937

### w:proofState

- Type: `w:CT_Proof`
- Attributes: `w:grammar`, `w:spelling`
- Parents: `w:settings`
- Specification: §17.15.1.65 p.1062

### w:ptab

- Type: `w:CT_PTab`
- Attributes: `w:alignment`!, `w:leader`!, `w:relativeTo`!
- Parents: `m:r`, `w:r`
- Specification: §17.3.3.23 p.357

### w:qFormat

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:style`
- Specification: §17.7.4.14 p.647

### w:query

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:mailMerge`
- Specification: §17.14.26 p.967, OI 2.1.377

### w:r

- Type: `w:CT_R`
- Attributes: `w:rsidDel`, `w:rsidR`, `w:rsidRPr`
- Children: `w:rPr`, `w:br`, `w:t`, `w:contentPart`, `w:delText`, `w:instrText`, `w:delInstrText`, `w:noBreakHyphen`, `w:softHyphen`, `w:dayShort`, `w:monthShort`, `w:yearShort`, `w:dayLong`, `w:monthLong`, `w:yearLong`, `w:annotationRef`, `w:footnoteRef`, `w:endnoteRef`, `w:separator`, `w:continuationSeparator`, `w:sym`, `w:pgNum`, `w:cr`, `w:tab`, `w:object`, `w:pict`, `w:fldChar`, `w:ruby`, `w:footnoteReference`, `w:endnoteReference`, `w:commentReference`, `w:drawing`, `w:ptab`, `w:lastRenderedPageBreak`
- Parents: `w:bdo`, `w:customXml`, `w:del`, `w:dir`, `w:fldSimple`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:p`, `w:rt`, `w:rubyBase`, `w:sdtContent`, `w:smartTag`
- Specification: §17.3.2.25 p.300, OI 2.1.87

### w:rFonts

- Type: `w:CT_Fonts`
- Attributes: `w:ascii`, `w:asciiTheme`, `w:cs`, `w:cstheme`, `w:eastAsia`, `w:eastAsiaTheme`, `w:hAnsi`, `w:hAnsiTheme`, `w:hint`
- Parents: `w:rPr`
- Specification: §17.3.2.26 p.301, OI 2.1.88

### w:rPr

- Type: `w:CT_RPr`, `w:CT_RPrOriginal`, `w:CT_ParaRPrOriginal`, `w:CT_ParaRPr`
- Children: `w:rStyle`, `w:rFonts`, `w:b`, `w:bCs`, `w:i`, `w:iCs`, `w:caps`, `w:smallCaps`, `w:strike`, `w:dstrike`, `w:outline`, `w:shadow`, `w:emboss`, `w:imprint`, `w:noProof`, `w:snapToGrid`, `w:vanish`, `w:webHidden`, `w:color`, `w:spacing`, `w:w`, `w:kern`, `w:position`, `w:sz`, `w:szCs`, `w:highlight`, `w:u`, `w:effect`, `w:bdr`, `w:shd`, `w:fitText`, `w:vertAlign`, `w:rtl`, `w:cs`, `w:em`, `w:lang`, `w:eastAsianLayout`, `w:specVanish`, `w:oMath`, `w:rPrChange`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`
- Parents: `m:ctrlPr`, `m:r`, `w:del`, `w:ins`, `w:lvl`, `w:pPr`, `w:r`, `w:rPrChange`, `w:rPrDefault`, `w:sdtEndPr`, `w:sdtPr`, `w:style`, `w:tblStylePr`
- Specification: §17.3.1.29 p.253, OI 2.1.57; §17.3.1.30 p.254, OI 2.1.58; §17.3.2.27 p.314, OI 2.1.89; §17.3.2.28 p.315, OI 2.1.90; §17.5.2.27 p.539, OI 2.1.200; §17.5.2.28 p.541, OI 2.1.201; §17.7.5.4 p.659, OI 2.1.245; §17.7.6.2 p.665, OI 2.1.248; §17.7.9.1 p.679, OI 2.1.260; §17.9.24 p.736, OI 2.1.291

### w:rPrChange

- Type: `w:CT_RPrChange`, `w:CT_ParaRPrChange`
- Attributes: `w:author`!, `w:date`, `w:id`!
- Children: `w:rPr`
- Parents: `w:rPr`
- Specification: §17.13.5.30 p.901, OI 2.1.346; §17.13.5.31 p.903, OI 2.1.347

### w:rPrDefault

- Type: `w:CT_RPrDefault`
- Children: `w:rPr`
- Parents: `w:docDefaults`
- Specification: §17.7.5.5 p.660

### w:rStyle

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:rPr`
- Specification: §17.3.2.29 p.316, OI 2.1.91

### w:readModeInkLockDown

- Type: `w:CT_ReadingModeInkLockDown`
- Attributes: `w:actualPg`!, `w:fontSz`!, `w:h`!, `w:w`!
- Parents: `w:settings`
- Specification: §17.15.1.66 p.1064

### w:recipientData

- Type: `w:CT_RecipientData`, `w:CT_Rel`
- Attributes: `r:id`!
- Children: `w:active`, `w:column`, `w:uniqueTag`
- Parents: `w:odso`, `w:recipients`
- Specification: §17.14.27 p.968; §17.14.28 p.969, OI 2.1.378

### w:recipients

- Type: `w:CT_Recipients`
- Children: `w:recipientData`
- Specification: §17.14.29 p.971

### w:relyOnVML

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:webSettings`
- Specification: Part 4 §14.8.4.1 p.176

### w:removeDateAndTime

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.67 p.1066

### w:removePersonalInformation

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.68 p.1067

### w:result

- Type: `w:CT_DecimalNumber`
- Attributes: `w:val`!
- Parents: `w:ddList`
- Specification: §17.16.28 p.1295

### w:revisionView

- Type: `w:CT_TrackChangesView`
- Attributes: `w:comments`, `w:formatting`, `w:inkAnnotations`, `w:insDel`, `w:markup`
- Parents: `w:settings`
- Specification: §17.15.1.69 p.1067, OI 2.1.414

### w:richText

- Type: `w:CT_Empty`
- Parents: `w:sdtPr`
- Specification: §17.5.2.26 p.539

### w:right

- Type: `w:CT_Border`, `w:CT_PageBorder`, `w:CT_TblWidth`
- Attributes: `r:id`, `w:color`, `w:frame`, `w:shadow`, `w:space`, `w:sz`, `w:themeColor`, `w:themeShade`, `w:themeTint`, `w:type`, `w:val`!, `w:w`
- Parents: `w:divBdr`, `w:pBdr`, `w:pgBorders`, `w:tblBorders`, `w:tblCellMar`, `w:tcBorders`, `w:tcMar`
- Specification: §17.3.1.28 p.252, OI 2.1.56; §17.6.15 p.598, OI 2.1.222; §17.15.2.35 p.1143, OI 2.1.444; Part 4 §14.4.5 p.55, OI 2.1.1762; Part 4 §14.4.6 p.55, OI 2.1.1763; Part 4 §14.4.7 p.55, OI 2.1.1764; Part 4 §14.4.8 p.55, OI 2.1.1765

### w:rsid

- Type: `w:CT_LongHexNumber`
- Attributes: `w:val`!
- Parents: `w:rsids`, `w:style`
- Specification: §17.7.4.15 p.648; §17.15.1.70 p.1069, OI 2.1.415

### w:rsidRoot

- Type: `w:CT_LongHexNumber`
- Attributes: `w:val`!
- Parents: `w:rsids`
- Specification: §17.15.1.71 p.1070

### w:rsids

- Type: `w:CT_DocRsids`
- Children: `w:rsidRoot`, `w:rsid`
- Parents: `w:settings`
- Specification: §17.15.1.72 p.1071

### w:rt

- Type: `w:CT_RubyContent`
- Children: `w:r`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`
- Parents: `w:ruby`
- Specification: §17.3.3.24 p.358, OI 2.1.112

### w:rtl

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:rPr`
- Specification: §17.3.2.30 p.317, OI 2.1.92

### w:rtlGutter

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:sectPr`
- Specification: §17.6.16 p.605

### w:ruby

- Type: `w:CT_Ruby`
- Children: `w:rubyPr`, `w:rt`, `w:rubyBase`
- Parents: `m:r`, `w:r`
- Specification: §17.3.3.25 p.359

### w:rubyAlign

- Type: `w:CT_RubyAlign`
- Attributes: `w:val`!
- Parents: `w:rubyPr`
- Specification: §17.3.3.26 p.360

### w:rubyBase

- Type: `w:CT_RubyContent`
- Children: `w:r`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`
- Parents: `w:ruby`
- Specification: §17.3.3.27 p.361, OI 2.1.113

### w:rubyPr

- Type: `w:CT_RubyPr`
- Children: `w:rubyAlign`, `w:hps`, `w:hpsRaise`, `w:hpsBaseText`, `w:lid`, `w:dirty`
- Parents: `w:ruby`
- Specification: §17.3.3.28 p.362

### w:saveFormsData

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.73 p.1072

### w:saveInvalidXml

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.74 p.1073, OI 2.1.416

### w:savePreviewPicture

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.75 p.1073

### w:saveSmartTagsAsXml

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:webSettings`
- Specification: §17.15.2.36 p.1144

### w:saveSubsetFonts

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.8.3.15 p.697

### w:saveThroughXslt

- Type: `w:CT_SaveThroughXslt`
- Attributes: `r:id`, `w:solutionID`
- Parents: `w:settings`
- Specification: §17.15.1.76 p.1074, OI 2.1.417

### w:saveXmlDataOnly

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.77 p.1075

### w:scrollbar

- Type: `w:CT_FrameScrollbar`
- Attributes: `w:val`!
- Parents: `w:frame`
- Specification: §17.15.2.37 p.1145

### w:sdt

- Type: `w:CT_SdtRun`, `w:CT_SdtBlock`, `w:CT_SdtRow`, `w:CT_SdtCell`
- Children: `w:sdtPr`, `w:sdtEndPr`, `w:sdtContent`
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:bdo`, `w:body`, `w:comment`, `w:customXml`, `w:del`, `w:dir`, `w:docPartBody`, `w:endnote`, `w:fldSimple`, `w:footnote`, `w:ftr`, `w:hdr`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:p`, `w:sdtContent`, `w:smartTag`, `w:tbl`, `w:tc`, `w:tr`, `w:txbxContent`, `wne:txbxContent`, `wp:txbxContent`
- Specification: §17.5.2.29 p.541; §17.5.2.30 p.542, OI 2.1.202; §17.5.2.31 p.543, OI 2.1.203; §17.5.2.32 p.544, OI 2.1.204

### w:sdtContent

- Type: `w:CT_SdtContentBlock`, `w:CT_SdtContentRun`, `w:CT_SdtContentCell`, `w:CT_SdtContentRow`
- Children: `w:customXml`, `w:sdt`, `w:p`, `w:tbl`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`, `w:smartTag`, `w:dir`, `w:bdo`, `w:r`, `w:fldSimple`, `w:hyperlink`, `w:subDoc`, `w:tc`, `w:tr`
- Parents: `w:sdt`
- Specification: §17.5.2.33 p.545, OI 2.1.205; §17.5.2.34 p.546, OI 2.1.206; §17.5.2.35 p.547, OI 2.1.207; §17.5.2.36 p.548, OI 2.1.208

### w:sdtEndPr

- Type: `w:CT_SdtEndPr`
- Children: `w:rPr`
- Parents: `w:sdt`
- Specification: §17.5.2.37 p.549

### w:sdtPr

- Type: `w:CT_SdtPr`
- Children: `w:rPr`, `w:alias`, `w:tag`, `w:id`, `w:lock`, `w:placeholder`, `w:temporary`, `w:showingPlcHdr`, `w:dataBinding`, `w:label`, `w:tabIndex`, `w:equation`, `w:comboBox`, `w:date`, `w:docPartObj`, `w:docPartList`, `w:dropDownList`, `w:picture`, `w:richText`, `w:text`, `w:citation`, `w:group`, `w:bibliography`
- Parents: `w:sdt`
- Specification: §17.5.2.38 p.549

### w:sectPr

- Type: `w:CT_SectPrBase`, `w:CT_SectPr`
- Attributes: `w:rsidDel`, `w:rsidR`, `w:rsidRPr`, `w:rsidSect`
- Children: `w:footnotePr`, `w:endnotePr`, `w:type`, `w:pgSz`, `w:pgMar`, `w:paperSrc`, `w:pgBorders`, `w:lnNumType`, `w:pgNumType`, `w:cols`, `w:formProt`, `w:vAlign`, `w:noEndnote`, `w:titlePg`, `w:textDirection`, `w:bidi`, `w:rtlGutter`, `w:docGrid`, `w:printerSettings`, `w:headerReference`, `w:footerReference`, `w:sectPrChange`
- Parents: `w:body`, `w:docPartBody`, `w:pPr`, `w:sectPrChange`
- Specification: §17.6.17 p.607, OI 2.1.223; §17.6.18 p.608, OI 2.1.224; §17.6.19 p.610, OI 2.1.225

### w:sectPrChange

- Type: `w:CT_SectPrChange`
- Attributes: `w:author`!, `w:date`, `w:id`!
- Children: `w:sectPr`
- Parents: `w:sectPr`
- Specification: §17.13.5.32 p.905, OI 2.1.348

### w:selectFldWithFirstOrLastChar

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.34 p.143

### w:semiHidden

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:style`
- Specification: §17.7.4.16 p.649

### w:separator

- Type: `w:CT_Empty`
- Parents: `m:r`, `w:r`
- Specification: §17.11.23 p.788

### w:settings

- Type: `w:CT_Settings`
- Children: `w:writeProtection`, `w:view`, `w:zoom`, `w:removePersonalInformation`, `w:removeDateAndTime`, `w:doNotDisplayPageBoundaries`, `w:displayBackgroundShape`, `w:printPostScriptOverText`, `w:printFractionalCharacterWidth`, `w:printFormsData`, `w:embedTrueTypeFonts`, `w:embedSystemFonts`, `w:saveSubsetFonts`, `w:saveFormsData`, `w:mirrorMargins`, `w:alignBordersAndEdges`, `w:bordersDoNotSurroundHeader`, `w:bordersDoNotSurroundFooter`, `w:gutterAtTop`, `w:hideSpellingErrors`, `w:hideGrammaticalErrors`, `w:activeWritingStyle`, `w:proofState`, `w:formsDesign`, `w:attachedTemplate`, `w:linkStyles`, `w:stylePaneFormatFilter`, `w:stylePaneSortMethod`, `w:documentType`, `w:mailMerge`, `w:revisionView`, `w:trackRevisions`, `w:doNotTrackMoves`, `w:doNotTrackFormatting`, `w:documentProtection`, `w:autoFormatOverride`, `w:styleLockTheme`, `w:styleLockQFSet`, `w:defaultTabStop`, `w:autoHyphenation`, `w:consecutiveHyphenLimit`, `w:hyphenationZone`, `w:doNotHyphenateCaps`, `w:showEnvelope`, `w:summaryLength`, `w:clickAndTypeStyle`, `w:defaultTableStyle`, `w:evenAndOddHeaders`, `w:bookFoldRevPrinting`, `w:bookFoldPrinting`, `w:bookFoldPrintingSheets`, `w:drawingGridHorizontalSpacing`, `w:drawingGridVerticalSpacing`, `w:displayHorizontalDrawingGridEvery`, `w:displayVerticalDrawingGridEvery`, `w:doNotUseMarginsForDrawingGridOrigin`, `w:drawingGridHorizontalOrigin`, `w:drawingGridVerticalOrigin`, `w:doNotShadeFormData`, `w:noPunctuationKerning`, `w:characterSpacingControl`, `w:printTwoOnOne`, `w:strictFirstAndLastChars`, `w:noLineBreaksAfter`, `w:noLineBreaksBefore`, `w:savePreviewPicture`, `w:doNotValidateAgainstSchema`, `w:saveInvalidXml`, `w:ignoreMixedContent`, `w:alwaysShowPlaceholderText`, `w:doNotDemarcateInvalidXml`, `w:saveXmlDataOnly`, `w:useXSLTWhenSaving`, `w:saveThroughXslt`, `w:showXMLTags`, `w:alwaysMergeEmptyNamespace`, `w:updateFields`, `w:hdrShapeDefaults`, `w:footnotePr`, `w:endnotePr`, `w:compat`, `w:docVars`, `w:rsids`, `m:mathPr`, `w:attachedSchema`, `w:themeFontLang`, `w:clrSchemeMapping`, `w:doNotIncludeSubdocsInStats`, `w:doNotAutoCompressPictures`, `w:forceUpgrade`, `w:captions`, `w:readModeInkLockDown`, `w:smartTagType`, `sl:schemaLibrary`, `w:shapeDefaults`, `w:doNotEmbedSmartTags`, `w:decimalSymbol`, `w:listSeparator`
- Specification: §17.15.1.78 p.1076

### w:shadow

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:rPr`
- Specification: §17.3.2.31 p.319, OI 2.1.93

### w:shapeDefaults

- Type: `w:CT_ShapeDefaults`
- Children: `any(urn:schemas-microsoft-com:office:office)`
- Parents: `w:settings`
- Specification: Part 4 §14.8.2.2 p.83, OI 2.1.1774

### w:shapeLayoutLikeWW8

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.35 p.144

### w:shd

- Type: `w:CT_Shd`
- Attributes: `w:color`, `w:fill`, `w:themeColor`, `w:themeFill`, `w:themeFillShade`, `w:themeFillTint`, `w:themeShade`, `w:themeTint`, `w:val`!
- Parents: `w:pPr`, `w:rPr`, `w:tblPr`, `w:tblPrEx`, `w:tcPr`
- Specification: §17.3.1.31 p.255, OI 2.1.59; §17.3.2.32 p.319, OI 2.1.94; §17.4.30 p.425, OI 2.1.142; §17.4.31 p.426, OI 2.1.143; §17.4.32 p.427, OI 2.1.144

### w:showBreaksInFrames

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.36 p.147

### w:showEnvelope

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.79 p.1077

### w:showXMLTags

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.80 p.1077, OI 2.1.418

### w:showingPlcHdr

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:sdtPr`
- Specification: §17.5.2.39 p.549

### w:sig

- Type: `w:CT_FontSig`
- Attributes: `w:csb0`!, `w:csb1`!, `w:usb0`!, `w:usb1`!, `w:usb2`!, `w:usb3`!
- Parents: `w:font`
- Specification: §17.8.3.16 p.697, OI 2.1.274

### w:size

- Type: `w:CT_HpsMeasure`
- Attributes: `w:val`!
- Parents: `w:checkBox`
- Specification: §17.16.29 p.1297

### w:sizeAuto

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:checkBox`
- Specification: §17.16.30 p.1297

### w:smallCaps

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:rPr`
- Specification: §17.3.2.33 p.320, OI 2.1.95

### w:smartTag

- Type: `w:CT_SmartTagRun`
- Attributes: `w:element`!, `w:uri`
- Children: `w:smartTagPr`, `w:customXml`, `w:smartTag`, `w:sdt`, `w:dir`, `w:bdo`, `w:r`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`, `w:fldSimple`, `w:hyperlink`, `w:subDoc`
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:bdo`, `w:customXml`, `w:del`, `w:dir`, `w:fldSimple`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:p`, `w:sdtContent`, `w:smartTag`
- Specification: §17.5.1.9 p.507, OI 2.1.192

### w:smartTagPr

- Type: `w:CT_SmartTagPr`
- Children: `w:attr`
- Parents: `w:smartTag`
- Specification: §17.5.1.10 p.509

### w:smartTagType

- Type: `w:CT_SmartTagType`
- Attributes: `w:name`, `w:namespaceuri`, `w:url`
- Parents: `w:settings`
- Specification: §17.15.1.81 p.1077, OI 2.1.419

### w:snapToGrid

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:pPr`, `w:rPr`
- Specification: §17.3.1.32 p.255; §17.3.2.34 p.321

### w:softHyphen

- Type: `w:CT_Empty`
- Parents: `m:r`, `w:r`
- Specification: §17.3.3.29 p.362

### w:sourceFileName

- Type: `w:CT_Rel`
- Attributes: `r:id`!
- Parents: `w:frame`
- Specification: §17.15.2.38 p.1147, OI 2.1.445

### w:spaceForUL

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: §17.15.3.7 p.1165

### w:spacing

- Type: `w:CT_Spacing`, `w:CT_SignedTwipsMeasure`
- Attributes: `w:after`, `w:afterAutospacing`, `w:afterLines`, `w:before`, `w:beforeAutospacing`, `w:beforeLines`, `w:line`, `w:lineRule`, `w:val`!
- Parents: `w:pPr`, `w:rPr`
- Specification: §17.3.1.33 p.256, OI 2.1.60; §17.3.2.35 p.321, OI 2.1.96

### w:spacingInWholePoints

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.37 p.149

### w:specVanish

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:rPr`
- Specification: §17.3.2.36 p.322, OI 2.1.97

### w:splitPgBreakAndParaMark

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.38 p.151

### w:src

- Type: `w:CT_Rel`
- Attributes: `r:id`!
- Parents: `w:odso`
- Specification: §17.14.30 p.972, OI 2.1.379

### w:start

- Type: `w:CT_Border`, `w:CT_TblWidth`, `w:CT_DecimalNumber`
- Attributes: `w:color`, `w:frame`, `w:shadow`, `w:space`, `w:sz`, `w:themeColor`, `w:themeShade`, `w:themeTint`, `w:type`, `w:val`!, `w:w`
- Parents: `w:lvl`, `w:tblBorders`, `w:tblCellMar`, `w:tcBorders`, `w:tcMar`
- Specification: §17.4.33 p.427, OI 2.1.145; §17.4.34 p.428, OI 2.1.146; §17.4.35 p.429, OI 2.1.147; §17.4.36 p.430, OI 2.1.148; §17.9.25 p.737, OI 2.1.292

### w:startOverride

- Type: `w:CT_DecimalNumber`
- Attributes: `w:val`!
- Parents: `w:lvlOverride`
- Specification: §17.9.26 p.738

### w:statusText

- Type: `w:CT_FFStatusText`
- Attributes: `w:type`, `w:val`
- Parents: `w:ffData`
- Specification: §17.16.31 p.1298

### w:storeMappedDataAs

- Type: `w:CT_SdtDateMappingType`
- Attributes: `w:val`
- Parents: `w:date`
- Specification: §17.5.2.40 p.550

### w:strictFirstAndLastChars

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.82 p.1079, OI 2.1.420

### w:strike

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:rPr`
- Specification: §17.3.2.37 p.323

### w:style

- Type: `w:CT_Style`, `w:CT_String`
- Attributes: `w:customStyle`, `w:default`, `w:styleId`, `w:type`, `w:val`!
- Children: `w:name`, `w:aliases`, `w:basedOn`, `w:next`, `w:link`, `w:autoRedefine`, `w:hidden`, `w:uiPriority`, `w:semiHidden`, `w:unhideWhenUsed`, `w:qFormat`, `w:locked`, `w:personal`, `w:personalCompose`, `w:personalReply`, `w:rsid`, `w:pPr`, `w:rPr`, `w:tblPr`, `w:trPr`, `w:tcPr`, `w:tblStylePr`
- Parents: `w:docPartPr`, `w:styles`
- Specification: §17.7.4.17 p.650, OI 2.1.242; §17.12.14 p.804

### w:styleLink

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:abstractNum`
- Specification: §17.9.27 p.740, OI 2.1.293

### w:styleLockQFSet

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.83 p.1079

### w:styleLockTheme

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.84 p.1080

### w:stylePaneFormatFilter

- Type: `w:CT_StylePaneFilter`
- Attributes: `w:allStyles`, `w:alternateStyleNames`, `w:clearFormatting`, `w:customStyles`, `w:directFormattingOnNumbering`, `w:directFormattingOnParagraphs`, `w:directFormattingOnRuns`, `w:directFormattingOnTables`, `w:headingStyles`, `w:latentStyles`, `w:numberingStyles`, `w:stylesInUse`, `w:tableStyles`, `w:top3HeadingStyles`, `w:val`, `w:visibleStyles`
- Parents: `w:settings`
- Specification: §17.15.1.85 p.1080, OI 2.1.421

### w:stylePaneSortMethod

- Type: `w:CT_StyleSort`
- Attributes: `w:val`!
- Parents: `w:settings`
- Specification: §17.15.1.86 p.1082

### w:styles

- Type: `w:CT_Styles`
- Children: `w:docDefaults`, `w:latentStyles`, `w:style`
- Specification: §17.7.4.18 p.655

### w:subDoc

- Type: `w:CT_Rel`
- Attributes: `r:id`!
- Parents: `w:bdo`, `w:customXml`, `w:dir`, `w:fldSimple`, `w:hyperlink`, `w:p`, `w:sdtContent`, `w:smartTag`
- Specification: §17.17.1.1 p.1304, OI 2.1.526

### w:subFontBySize

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.39 p.152

### w:suff

- Type: `w:CT_LevelSuffix`
- Attributes: `w:val`!
- Parents: `w:lvl`
- Specification: §17.9.28 p.742

### w:summaryLength

- Type: `w:CT_DecimalNumberOrPrecent`
- Attributes: `w:val`!
- Parents: `w:settings`
- Specification: §17.15.1.87 p.1083, OI 2.1.422

### w:suppressAutoHyphens

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:pPr`
- Specification: §17.3.1.34 p.261

### w:suppressBottomSpacing

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.40 p.153

### w:suppressLineNumbers

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:pPr`
- Specification: §17.3.1.35 p.262

### w:suppressOverlap

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:pPr`
- Specification: §17.3.1.36 p.263

### w:suppressSpBfAfterPgBrk

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.42 p.157

### w:suppressSpacingAtTopOfPage

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.41 p.155

### w:suppressTopSpacing

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.43 p.159

### w:suppressTopSpacingWP

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.44 p.160

### w:swapBordersFacingPages

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.45 p.160

### w:sym

- Type: `w:CT_Sym`
- Attributes: `w:char`, `w:font`
- Parents: `m:r`, `w:r`
- Specification: §17.3.3.30 p.363, OI 2.1.114

### w:sz

- Type: `w:CT_HpsMeasure`, `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:frame`, `w:frameset`, `w:rPr`
- Specification: §17.3.2.38 p.323, OI 2.1.98; §17.15.2.39 p.1149, OI 2.1.446; §17.15.2.40 p.1151, OI 2.1.447

### w:szCs

- Type: `w:CT_HpsMeasure`
- Attributes: `w:val`!
- Parents: `w:rPr`
- Specification: §17.3.2.39 p.324, OI 2.1.99

### w:t

- Type: `w:CT_Text` (text: `s:ST_String`)
- Attributes: `xml:space`
- Parents: `m:r`, `w:r`
- Specification: §17.3.3.31 p.365

### w:tab

- Type: `w:CT_Empty`, `w:CT_TabStop`
- Attributes: `w:leader`, `w:pos`!, `w:val`!
- Parents: `m:r`, `w:r`, `w:tabs`
- Specification: §17.3.1.37 p.263, OI 2.1.61; §17.3.3.32 p.366

### w:tabIndex

- Type: `w:CT_UnsignedDecimalNumber`
- Attributes: `w:val`!
- Parents: `w:ffData`, `w:sdtPr`
- Specification: §17.5.2.41 p.552, OI 2.1.209; §17.16.32 p.1300, OI 2.1.525

### w:table

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:odso`
- Specification: §17.14.31 p.973, OI 2.1.380

### w:tabs

- Type: `w:CT_Tabs`
- Children: `w:tab`
- Parents: `w:pPr`
- Specification: §17.3.1.38 p.265

### w:tag

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:sdtPr`
- Specification: §17.5.2.42 p.553

### w:targetScreenSz

- Type: `w:CT_TargetScreenSz`
- Attributes: `w:val`!
- Parents: `w:webSettings`
- Specification: §17.15.2.41 p.1153, OI 2.1.448

### w:tbl

- Type: `w:CT_Tbl`
- Children: `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:tblPr`, `w:tblGrid`, `w:tr`, `w:customXml`, `w:sdt`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`
- Parents: `w:body`, `w:comment`, `w:customXml`, `w:docPartBody`, `w:endnote`, `w:footnote`, `w:ftr`, `w:hdr`, `w:sdtContent`, `w:tc`, `w:txbxContent`, `wne:txbxContent`, `wp:txbxContent`
- Specification: §17.4.37 p.431, OI 2.1.149

### w:tblBorders

- Type: `w:CT_TblBorders`
- Children: `w:top`, `w:start`, `w:left`, `w:bottom`, `w:end`, `w:right`, `w:insideH`, `w:insideV`
- Parents: `w:tblPr`, `w:tblPrEx`
- Specification: §17.4.38 p.432, OI 2.1.150; §17.4.39 p.433, OI 2.1.151

### w:tblCaption

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:tblPr`
- Specification: §17.4.40 p.434

### w:tblCellMar

- Type: `w:CT_TblCellMar`
- Children: `w:top`, `w:start`, `w:left`, `w:bottom`, `w:end`, `w:right`
- Parents: `w:tblPr`, `w:tblPrEx`
- Specification: §17.4.41 p.435; §17.4.42 p.436

### w:tblCellSpacing

- Type: `w:CT_TblWidth`
- Attributes: `w:type`, `w:w`
- Parents: `w:tblPr`, `w:tblPrEx`, `w:trPr`
- Specification: §17.4.43 p.437, OI 2.1.152; §17.4.44 p.438, OI 2.1.153; §17.4.45 p.439, OI 2.1.154

### w:tblDescription

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:tblPr`
- Specification: §17.4.46 p.440

### w:tblGrid

- Type: `w:CT_TblGridBase`, `w:CT_TblGrid`
- Children: `w:gridCol`, `w:tblGridChange`
- Parents: `w:tbl`, `w:tblGridChange`
- Specification: §17.4.47 p.441; §17.4.48 p.442

### w:tblGridChange

- Type: `w:CT_TblGridChange`
- Attributes: `w:id`!
- Children: `w:tblGrid`
- Parents: `w:tblGrid`
- Specification: §17.13.5.33 p.907, OI 2.1.349

### w:tblHeader

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:trPr`
- Specification: §17.4.49 p.443

### w:tblInd

- Type: `w:CT_TblWidth`
- Attributes: `w:type`, `w:w`
- Parents: `w:tblPr`, `w:tblPrEx`
- Specification: §17.4.50 p.444, OI 2.1.155; §17.4.51 p.445, OI 2.1.156

### w:tblLayout

- Type: `w:CT_TblLayoutType`
- Attributes: `w:type`
- Parents: `w:tblPr`, `w:tblPrEx`
- Specification: §17.4.52 p.446, OI 2.1.157; §17.4.53 p.447, OI 2.1.158

### w:tblLook

- Type: `w:CT_TblLook`
- Attributes: `w:firstColumn`, `w:firstRow`, `w:lastColumn`, `w:lastRow`, `w:noHBand`, `w:noVBand`, `w:val`
- Parents: `w:tblPr`, `w:tblPrEx`
- Specification: §17.4.54 p.448, OI 2.1.159; §17.4.55 p.449, OI 2.1.160

### w:tblOverlap

- Type: `w:CT_TblOverlap`
- Attributes: `w:val`!
- Parents: `w:tblPr`
- Specification: §17.4.56 p.450, OI 2.1.161

### w:tblPr

- Type: `w:CT_TblPrBase`, `w:CT_TblPr`
- Children: `w:tblStyle`, `w:tblpPr`, `w:tblOverlap`, `w:bidiVisual`, `w:tblStyleRowBandSize`, `w:tblStyleColBandSize`, `w:tblW`, `w:jc`, `w:tblCellSpacing`, `w:tblInd`, `w:tblBorders`, `w:shd`, `w:tblLayout`, `w:tblCellMar`, `w:tblLook`, `w:tblCaption`, `w:tblDescription`, `w:tblPrChange`
- Parents: `w:style`, `w:tbl`, `w:tblPrChange`, `w:tblStylePr`
- Specification: §17.4.58 p.458, OI 2.1.163; §17.4.59 p.459, OI 2.1.164; §17.7.6.3 p.666, OI 2.1.249; §17.7.6.4 p.666, OI 2.1.250

### w:tblPrChange

- Type: `w:CT_TblPrChange`
- Attributes: `w:author`!, `w:date`, `w:id`!
- Children: `w:tblPr`
- Parents: `w:tblPr`
- Specification: §17.13.5.34 p.908, OI 2.1.350

### w:tblPrEx

- Type: `w:CT_TblPrExBase`, `w:CT_TblPrEx`
- Children: `w:tblW`, `w:jc`, `w:tblCellSpacing`, `w:tblInd`, `w:tblBorders`, `w:shd`, `w:tblLayout`, `w:tblCellMar`, `w:tblLook`, `w:tblPrExChange`
- Parents: `w:tblPrExChange`, `w:tr`
- Specification: §17.4.60 p.460; §17.4.61 p.462

### w:tblPrExChange

- Type: `w:CT_TblPrExChange`
- Attributes: `w:author`!, `w:date`, `w:id`!
- Children: `w:tblPrEx`
- Parents: `w:tblPrEx`
- Specification: §17.13.5.35 p.910, OI 2.1.351

### w:tblStyle

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:tblPr`
- Specification: §17.4.62 p.464, OI 2.1.165

### w:tblStyleColBandSize

- Type: `w:CT_DecimalNumber`
- Attributes: `w:val`!
- Parents: `w:tblPr`
- Specification: §17.7.6.5 p.667, OI 2.1.251

### w:tblStylePr

- Type: `w:CT_TblStylePr`
- Attributes: `w:type`!
- Children: `w:pPr`, `w:rPr`, `w:tblPr`, `w:trPr`, `w:tcPr`
- Parents: `w:style`
- Specification: §17.7.6.6 p.668, OI 2.1.252

### w:tblStyleRowBandSize

- Type: `w:CT_DecimalNumber`
- Attributes: `w:val`!
- Parents: `w:tblPr`
- Specification: §17.7.6.7 p.670, OI 2.1.253

### w:tblW

- Type: `w:CT_TblWidth`
- Attributes: `w:type`, `w:w`
- Parents: `w:tblPr`, `w:tblPrEx`
- Specification: §17.4.63 p.465, OI 2.1.166; §17.4.64 p.466, OI 2.1.167

### w:tblpPr

- Type: `w:CT_TblPPr`
- Attributes: `w:bottomFromText`, `w:horzAnchor`, `w:leftFromText`, `w:rightFromText`, `w:tblpX`, `w:tblpXSpec`, `w:tblpY`, `w:tblpYSpec`, `w:topFromText`, `w:vertAnchor`
- Parents: `w:tblPr`
- Specification: §17.4.57 p.452, OI 2.1.162

### w:tc

- Type: `w:CT_Tc`
- Attributes: `w:id`
- Children: `w:tcPr`, `w:customXml`, `w:sdt`, `w:p`, `w:tbl`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`, `w:altChunk`
- Parents: `w:customXml`, `w:sdtContent`, `w:tr`
- Specification: §17.4.65 p.467, OI 2.1.168

### w:tcBorders

- Type: `w:CT_TcBorders`
- Children: `w:top`, `w:start`, `w:left`, `w:bottom`, `w:end`, `w:right`, `w:insideH`, `w:insideV`, `w:tl2br`, `w:tr2bl`
- Parents: `w:tcPr`
- Specification: §17.4.66 p.469, OI 2.1.169

### w:tcFitText

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:tcPr`
- Specification: §17.4.67 p.472, OI 2.1.170

### w:tcMar

- Type: `w:CT_TcMar`
- Children: `w:top`, `w:start`, `w:left`, `w:bottom`, `w:end`, `w:right`
- Parents: `w:tcPr`
- Specification: §17.4.68 p.472

### w:tcPr

- Type: `w:CT_TcPrInner`, `w:CT_TcPr`
- Children: `w:cnfStyle`, `w:tcW`, `w:gridSpan`, `w:hMerge`, `w:vMerge`, `w:tcBorders`, `w:shd`, `w:noWrap`, `w:tcMar`, `w:textDirection`, `w:tcFitText`, `w:vAlign`, `w:hideMark`, `w:headers`, `w:cellIns`, `w:cellDel`, `w:cellMerge`, `w:tcPrChange`
- Parents: `w:style`, `w:tblStylePr`, `w:tc`, `w:tcPrChange`
- Specification: §17.4.69 p.473; §17.4.70 p.474; §17.7.6.8 p.671, OI 2.1.254; §17.7.6.9 p.672, OI 2.1.255

### w:tcPrChange

- Type: `w:CT_TcPrChange`
- Attributes: `w:author`!, `w:date`, `w:id`!
- Children: `w:tcPr`
- Parents: `w:tcPr`
- Specification: §17.13.5.36 p.912, OI 2.1.352

### w:tcW

- Type: `w:CT_TblWidth`
- Attributes: `w:type`, `w:w`
- Parents: `w:tcPr`
- Specification: §17.4.71 p.475, OI 2.1.171

### w:temporary

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:sdtPr`
- Specification: §17.5.2.43 p.554, OI 2.1.210

### w:text

- Type: `w:CT_SdtText`
- Attributes: `w:multiLine`
- Parents: `w:sdtPr`
- Specification: §17.5.2.44 p.555

### w:textAlignment

- Type: `w:CT_TextAlignment`
- Attributes: `w:val`!
- Parents: `w:pPr`
- Specification: §17.3.1.39 p.265

### w:textDirection

- Type: `w:CT_TextDirection`
- Attributes: `w:val`!
- Parents: `w:pPr`, `w:sectPr`, `w:tcPr`
- Specification: §17.3.1.41 p.268, OI 2.1.63; §17.4.72 p.477, OI 2.1.172; §17.6.20 p.612

### w:textInput

- Type: `w:CT_FFTextInput`
- Children: `w:type`, `w:default`, `w:maxLength`, `w:format`
- Parents: `w:ffData`
- Specification: §17.16.33 p.1301

### w:textboxTightWrap

- Type: `w:CT_TextboxTightWrap`
- Attributes: `w:val`!
- Parents: `w:pPr`
- Specification: §17.3.1.40 p.267, OI 2.1.62

### w:themeFontLang

- Type: `w:CT_Language`
- Attributes: `w:bidi`, `w:eastAsia`, `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.88 p.1084, OI 2.1.423

### w:title

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:frame`, `w:frameset`
- Specification: §17.15.2.42 p.1153, OI 2.1.449

### w:titlePg

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:sectPr`
- Specification: §17.10.6 p.755, OI 2.1.299

### w:tl2br

- Type: `w:CT_Border`
- Attributes: `w:color`, `w:frame`, `w:shadow`, `w:space`, `w:sz`, `w:themeColor`, `w:themeShade`, `w:themeTint`, `w:val`!
- Parents: `w:tcBorders`
- Specification: §17.4.73 p.478, OI 2.1.173

### w:tmpl

- Type: `w:CT_LongHexNumber`
- Attributes: `w:val`!
- Parents: `w:abstractNum`
- Specification: §17.9.29 p.743

### w:top

- Type: `w:CT_Border`, `w:CT_TopPageBorder`, `w:CT_TblWidth`
- Attributes: `r:id`, `r:topLeft`, `r:topRight`, `w:color`, `w:frame`, `w:shadow`, `w:space`, `w:sz`, `w:themeColor`, `w:themeShade`, `w:themeTint`, `w:type`, `w:val`!, `w:w`
- Parents: `w:divBdr`, `w:pBdr`, `w:pgBorders`, `w:tblBorders`, `w:tblCellMar`, `w:tcBorders`, `w:tcMar`
- Specification: §17.3.1.42 p.269, OI 2.1.64; §17.4.74 p.478, OI 2.1.174; §17.4.75 p.479, OI 2.1.175; §17.4.76 p.480, OI 2.1.176; §17.4.77 p.481, OI 2.1.177; §17.6.21 p.612, OI 2.1.226; §17.15.2.43 p.1155, OI 2.1.450

### w:topLinePunct

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:pPr`
- Specification: §17.3.1.43 p.271, OI 2.1.65

### w:tr

- Type: `w:CT_Row`
- Attributes: `w:rsidDel`, `w:rsidR`, `w:rsidRPr`, `w:rsidTr`
- Children: `w:tblPrEx`, `w:trPr`, `w:tc`, `w:customXml`, `w:sdt`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`
- Parents: `w:customXml`, `w:sdtContent`, `w:tbl`
- Specification: §17.4.78 p.481, OI 2.1.178

### w:tr2bl

- Type: `w:CT_Border`
- Attributes: `w:color`, `w:frame`, `w:shadow`, `w:space`, `w:sz`, `w:themeColor`, `w:themeShade`, `w:themeTint`, `w:val`!
- Parents: `w:tcBorders`
- Specification: §17.4.79 p.483, OI 2.1.179

### w:trHeight

- Type: `w:CT_Height`
- Attributes: `w:hRule`, `w:val`
- Parents: `w:trPr`
- Specification: §17.4.80 p.484, OI 2.1.180

### w:trPr

- Type: `w:CT_TrPrBase`, `w:CT_TrPr`
- Children: `w:cnfStyle`, `w:divId`, `w:gridBefore`, `w:gridAfter`, `w:wBefore`, `w:wAfter`, `w:cantSplit`, `w:trHeight`, `w:tblHeader`, `w:tblCellSpacing`, `w:jc`, `w:hidden`, `w:ins`, `w:del`, `w:trPrChange`
- Parents: `w:style`, `w:tblStylePr`, `w:tr`, `w:trPrChange`
- Specification: §17.4.81 p.486; §17.4.82 p.487; §17.7.6.10 p.672, OI 2.1.256; §17.7.6.11 p.673, OI 2.1.257

### w:trPrChange

- Type: `w:CT_TrPrChange`
- Attributes: `w:author`!, `w:date`, `w:id`!
- Children: `w:trPr`
- Parents: `w:trPr`
- Specification: §17.13.5.37 p.914, OI 2.1.353

### w:trackRevisions

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.89 p.1086

### w:truncateFontHeightsLikeWP6

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.46 p.163

### w:txbxContent

- Type: `w:CT_TxbxContent`
- Children: `w:customXml`, `w:sdt`, `w:p`, `w:tbl`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`, `w:altChunk`
- Parents: `v:textbox`, `wps:txbx`
- Specification: Part 4 §14.9.1.1 p.177, OI 2.1.1779

### w:type

- Type: `w:CT_FFTextType`, `w:CT_SectType`, `w:CT_MailMergeOdsoFMDFieldType`, `w:CT_MailMergeSourceType`, `w:CT_DocPartType`
- Attributes: `w:val`!
- Parents: `w:fieldMapData`, `w:odso`, `w:sectPr`, `w:textInput`, `w:types`
- Specification: §17.6.22 p.621, OI 2.1.227; §17.12.15 p.805; §17.14.32 p.975; §17.14.33 p.975; §17.16.34 p.1301

### w:types

- Type: `w:CT_DocPartTypes`
- Attributes: `w:all`
- Children: `w:type`
- Parents: `w:docPartPr`
- Specification: §17.12.16 p.806

### w:u

- Type: `w:CT_Underline`
- Attributes: `w:color`, `w:themeColor`, `w:themeShade`, `w:themeTint`, `w:val`
- Parents: `w:rPr`
- Specification: §17.3.2.40 p.325, OI 2.1.100

### w:udl

- Type: `w:CT_String`
- Attributes: `w:val`!
- Parents: `w:odso`
- Specification: §17.14.34 p.976, OI 2.1.381

### w:uiPriority

- Type: `w:CT_DecimalNumber`
- Attributes: `w:val`!
- Parents: `w:style`
- Specification: §17.7.4.19 p.655, OI 2.1.243

### w:ulTrailSpace

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: §17.15.3.8 p.1166

### w:underlineTabInNumList

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.47 p.164

### w:unhideWhenUsed

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:style`
- Specification: §17.7.4.20 p.656

### w:uniqueTag

- Type: `w:CT_Base64Binary`
- Attributes: `w:val`!
- Parents: `w:recipientData`
- Specification: §17.14.35 p.978, OI 2.1.382

### w:updateFields

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.90 p.1088, OI 2.1.424

### w:useAltKinsokuLineBreakRules

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.48 p.165

### w:useAnsiKerningPairs

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.49 p.166

### w:useFELayout

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.50 p.166

### w:useNormalStyleForList

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.51 p.167

### w:usePrinterMetrics

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.52 p.168

### w:useSingleBorderforContiguousCells

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.53 p.169

### w:useWord2002TableStyleRules

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.54 p.170

### w:useWord97LineBreakRules

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.55 p.172

### w:useXSLTWhenSaving

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.91 p.1089

### w:vAlign

- Type: `w:CT_VerticalJc`
- Attributes: `w:val`!
- Parents: `w:sectPr`, `w:tcPr`
- Specification: §17.4.83 p.488, OI 2.1.181; §17.6.23 p.622

### w:vMerge

- Type: `w:CT_VMerge`
- Attributes: `w:val`
- Parents: `w:tcPr`
- Specification: §17.4.84 p.489, OI 2.1.182

### w:vanish

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:rPr`
- Specification: §17.3.2.41 p.329

### w:vertAlign

- Type: `w:CT_VerticalAlignRun`
- Attributes: `w:val`!
- Parents: `w:rPr`
- Specification: §17.3.2.42 p.330

### w:view

- Type: `w:CT_View`
- Attributes: `w:val`!
- Parents: `w:settings`
- Specification: §17.15.1.92 p.1089, OI 2.1.425

### w:viewMergedData

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:mailMerge`
- Specification: §17.14.36 p.978

### w:w

- Type: `w:CT_TextScale`, `w:CT_TwipsMeasure`
- Attributes: `w:val`
- Parents: `w:framesetSplitbar`, `w:rPr`
- Specification: §17.3.2.43 p.331; §17.15.2.44 p.1157

### w:wAfter

- Type: `w:CT_TblWidth`
- Attributes: `w:type`, `w:w`
- Parents: `w:trPr`
- Specification: §17.4.85 p.491, OI 2.1.183

### w:wBefore

- Type: `w:CT_TblWidth`
- Attributes: `w:type`, `w:w`
- Parents: `w:trPr`
- Specification: §17.4.86 p.492, OI 2.1.184

### w:webHidden

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:rPr`
- Specification: §17.3.2.44 p.332, OI 2.1.101

### w:webSettings

- Type: `w:CT_WebSettings`
- Children: `w:frameset`, `w:divs`, `w:encoding`, `w:optimizeForBrowser`, `w:relyOnVML`, `w:allowPNG`, `w:doNotRelyOnCSS`, `w:doNotSaveAsSingleFile`, `w:doNotOrganizeInFolder`, `w:doNotUseLongFileNames`, `w:pixelsPerInch`, `w:targetScreenSz`, `w:saveSmartTagsAsXml`
- Specification: §17.15.2.45 p.1158

### w:widowControl

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:pPr`
- Specification: §17.3.1.44 p.271, OI 2.1.66

### w:wordWrap

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:pPr`
- Specification: §17.3.1.45 p.272, OI 2.1.67

### w:wpJustification

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.56 p.174

### w:wpSpaceWidth

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.57 p.175

### w:wrapTrailSpaces

- Type: `w:CT_OnOff`
- Attributes: `w:val`
- Parents: `w:compat`
- Specification: Part 4 §14.8.3.58 p.175

### w:writeProtection

- Type: `w:CT_WriteProtection`
- Attributes: `w:algIdExt`, `w:algIdExtSource`, `w:algorithmName`, `w:cryptAlgorithmClass`, `w:cryptAlgorithmSid`, `w:cryptAlgorithmType`, `w:cryptProvider`, `w:cryptProviderType`, `w:cryptProviderTypeExt`, `w:cryptProviderTypeExtSource`, `w:cryptSpinCount`, `w:hash`, `w:hashValue`, `w:recommended`, `w:salt`, `w:saltValue`, `w:spinCount`
- Parents: `w:settings`
- Specification: §17.15.1.93 p.1090, OI 2.1.426

### w:yearLong

- Type: `w:CT_Empty`
- Parents: `m:r`, `w:r`
- Specification: §17.3.3.33 p.366

### w:yearShort

- Type: `w:CT_Empty`
- Parents: `m:r`, `w:r`
- Specification: §17.3.3.34 p.367

### w:zoom

- Type: `w:CT_Zoom`
- Attributes: `w:percent`!, `w:val`
- Parents: `w:settings`
- Specification: §17.15.1.94 p.1094, OI 2.1.427

`!` marks required attributes.
