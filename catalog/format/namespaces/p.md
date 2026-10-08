# p: PresentationML (PPTX)

`http://schemas.openxmlformats.org/presentationml/2006/main`

- Source: ECMA-376-4 transitional schema pml.xsd
- Office version: 2007
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 213 | 344 | 0 | 149 | 58 | 254 | 281 | 100 |

Full detail (content models, attribute types, defaults): [p.json](p.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`p:anim`](#panim) | `p:CT_TLAnimateBehavior` | 5 | 2 | 3 | §19.5.1 p.2613, OI 2.1.1138 |
| [`p:animClr`](#panimclr) | `p:CT_TLAnimateColorBehavior` | 2 | 4 | 3 | §19.5.2 p.2614, OI 2.1.1139 |
| [`p:animEffect`](#panimeffect) | `p:CT_TLAnimateEffectBehavior` | 3 | 2 | 3 | §19.5.3 p.2615, OI 2.1.1140 |
| [`p:animMotion`](#panimmotion) | `p:CT_TLAnimateMotionBehavior` | 5 | 5 | 3 | §19.5.4 p.2621, OI 2.1.1141 |
| [`p:animRot`](#panimrot) | `p:CT_TLAnimateRotationBehavior` | 3 | 1 | 3 | §19.5.5 p.2622, OI 2.1.1142 |
| [`p:animScale`](#panimscale) | `p:CT_TLAnimateScaleBehavior` | 1 | 4 | 3 | §19.5.6 p.2623, OI 2.1.1143 |
| [`p:attrName`](#pattrname) | `xsd:string` | 0 | 0 | 1 | §19.5.7 p.2624, OI 2.1.1144 |
| [`p:attrNameLst`](#pattrnamelst) | `p:CT_TLBehaviorAttributeNameList` | 0 | 1 | 1 | §19.5.8 p.2625 |
| [`p:audio`](#paudio) | `p:CT_TLMediaNodeAudio` | 1 | 1 | 3 | §19.5.9 p.2626, OI 2.1.1145 |
| [`p:bg`](#pbg) | `p:CT_Empty`, `p:CT_Background` | 1 | 2 | 2 | §19.3.1.1 p.2570, OI 2.1.1118; §19.5.10 p.2627 |
| [`p:bgPr`](#pbgpr) | `p:CT_BackgroundProperties` | 1 | 9 | 1 | §19.3.1.2 p.2571, OI 2.1.1119 |
| [`p:bgRef`](#pbgref) | `a:CT_StyleMatrixReference` | 1 | 6 | 1 | §19.3.1.3 p.2571 |
| [`p:bldAsOne`](#pbldasone) | `p:CT_Empty` | 0 | 0 | 1 | §19.5.11 p.2627 |
| [`p:bldDgm`](#pblddgm) | `p:CT_TLBuildDiagram` | 4 | 0 | 1 | §19.5.12 p.2627, OI 2.1.1146 |
| [`p:bldGraphic`](#pbldgraphic) | `p:CT_TLGraphicalObjectBuild` | 3 | 2 | 1 | §19.5.13 p.2628, OI 2.1.1147 |
| [`p:bldLst`](#pbldlst) | `p:CT_BuildList` | 0 | 4 | 1 | §19.5.14 p.2629 |
| [`p:bldOleChart`](#pbldolechart) | `p:CT_TLOleBuildChart` | 5 | 0 | 1 | §19.5.15 p.2630, OI 2.1.1148 |
| [`p:bldP`](#pbldp) | `p:CT_TLBuildParagraph` | 9 | 1 | 1 | §19.5.16 p.2631, OI 2.1.1149 |
| [`p:bldSub`](#pbldsub) | `a:CT_AnimationGraphicalObjectBuildProperties` | 0 | 2 | 1 | §19.5.17 p.2633, OI 2.1.1150 |
| [`p:blinds`](#pblinds) | `p:CT_OrientationTransition` | 1 | 0 | 1 | §19.5.18 p.2633 |
| [`p:blipFill`](#pblipfill) | `a:CT_BlipFillProperties` | 2 | 4 | 1 | §19.3.1.4 p.2572, OI 2.1.1120 |
| [`p:bodyStyle`](#pbodystyle) | `a:CT_TextListStyle` | 0 | 11 | 1 | §19.3.1.5 p.2575 |
| [`p:bold`](#pbold) | `p:CT_EmbeddedFontDataId` | 1 | 0 | 1 | §19.2.1.1 p.2533 |
| [`p:boldItalic`](#pbolditalic) | `p:CT_EmbeddedFontDataId` | 1 | 0 | 1 | §19.2.1.2 p.2534 |
| [`p:boolVal`](#pboolval) | `p:CT_TLAnimVariantBooleanVal` | 1 | 0 | 3 | §19.5.19 p.2634 |
| [`p:browse`](#pbrowse) | `p:CT_ShowInfoBrowse` | 1 | 0 | 1 | §19.2.1.3 p.2535 |
| [`p:by`](#pby) | `p:CT_TLByAnimateColorTransform`, `p:CT_TLPoint` | 2 | 2 | 3 | §19.5.20 p.2635; §19.5.21 p.2635, OI 2.1.1151 |
| [`p:cBhvr`](#pcbhvr) | `p:CT_TLCommonBehaviorData` | 8 | 3 | 8 | §19.5.22 p.2636, OI 2.1.1152 |
| [`p:cMediaNode`](#pcmedianode) | `p:CT_TLCommonMediaNodeData` | 4 | 2 | 2 | §19.5.29 p.2643, OI 2.1.1155 |
| [`p:cNvCxnSpPr`](#pcnvcxnsppr) | `a:CT_NonVisualConnectorProperties` | 0 | 4 | 1 | §19.3.1.8 p.2577 |
| [`p:cNvGraphicFramePr`](#pcnvgraphicframepr) | `a:CT_NonVisualGraphicFrameProperties` | 0 | 2 | 1 | §19.3.1.9 p.2577 |
| [`p:cNvGrpSpPr`](#pcnvgrpsppr) | `a:CT_NonVisualGroupDrawingShapeProps` | 0 | 2 | 1 | §19.3.1.10 p.2578 |
| [`p:cNvPicPr`](#pcnvpicpr) | `a:CT_NonVisualPictureProperties` | 1 | 2 | 1 | §19.3.1.11 p.2578 |
| [`p:cNvPr`](#pcnvpr) | `a:CT_NonVisualDrawingProps` | 5 | 3 | 5 | §19.3.1.12 p.2579 |
| [`p:cNvSpPr`](#pcnvsppr) | `a:CT_NonVisualDrawingShapeProps` | 1 | 2 | 1 | §19.3.1.13 p.2581 |
| [`p:cSld`](#pcsld) | `p:CT_CommonSlideData` | 1 | 5 | 6 | §19.3.1.16 p.2585, OI 2.1.1121 |
| [`p:cSldViewPr`](#pcsldviewpr) | `p:CT_CommonSlideViewProperties` | 3 | 2 | 2 | §19.2.2.1 p.2562 |
| [`p:cTn`](#pctn) | `p:CT_TLCommonTimeNodeData` | 23 | 6 | 5 | §19.5.33 p.2648, OI 2.1.1157 |
| [`p:cViewPr`](#pcviewpr) | `p:CT_CommonViewProperties` | 1 | 2 | 4 | §19.2.2.2 p.2563 |
| [`p:charRg`](#pcharrg) | `p:CT_IndexRange` | 2 | 0 | 1 | §19.5.23 p.2638, OI 2.1.1153 |
| [`p:checker`](#pchecker) | `p:CT_OrientationTransition` | 1 | 0 | 1 | §19.5.24 p.2638 |
| [`p:childTnLst`](#pchildtnlst) | `p:CT_TimeNodeList` | 0 | 13 | 1 | §19.5.25 p.2640 |
| [`p:circle`](#pcircle) | `p:CT_Empty` | 0 | 0 | 1 | §19.5.26 p.2640 |
| [`p:clrMap`](#pclrmap) | `a:CT_ColorMapping` | 12 | 1 | 3 | §19.3.1.6 p.2575 |
| [`p:clrMapOvr`](#pclrmapovr) | `a:CT_ColorMappingOverride` | 0 | 2 | 3 | §19.3.1.7 p.2577 |
| [`p:clrMru`](#pclrmru) | `a:CT_ColorMRU` | 0 | 6 | 1 | §19.2.1.4 p.2535 |
| [`p:clrVal`](#pclrval) | `a:CT_Color` | 0 | 6 | 3 | §19.5.27 p.2640 |
| [`p:cm`](#pcm) | `p:CT_Comment` | 3 | 3 | 1 | §19.4.1 p.2608, OI 2.1.1136 |
| [`p:cmAuthor`](#pcmauthor) | `p:CT_CommentAuthor` | 5 | 1 | 1 | §19.4.2 p.2609 |
| [`p:cmAuthorLst`](#pcmauthorlst) ◆ | `p:CT_CommentAuthorList` | 0 | 1 | 0 | §19.4.3 p.2610 |
| [`p:cmLst`](#pcmlst) ◆ | `p:CT_CommentList` | 0 | 1 | 0 | §19.4.4 p.2610 |
| [`p:cmd`](#pcmd) | `p:CT_TLCommandBehavior` | 2 | 1 | 3 | §19.5.28 p.2641, OI 2.1.1154 |
| [`p:comb`](#pcomb) | `p:CT_OrientationTransition` | 1 | 0 | 1 | §19.5.30 p.2644 |
| [`p:cond`](#pcond) | `p:CT_TLTimeCondition` | 2 | 3 | 4 | §19.5.31 p.2645, OI 2.1.1156 |
| [`p:contentPart`](#pcontentpart) | `p:CT_Rel` | 1 | 0 | 2 | §19.3.1.14 p.2582 |
| [`p:control`](#pcontrol) | `p:CT_Control` | 6 | 2 | 1 | §19.3.2.1 p.2605, OI 2.1.1131 |
| [`p:controls`](#pcontrols) | `p:CT_ControlList` | 0 | 1 | 1 | §19.3.1.15 p.2585 |
| [`p:cover`](#pcover) | `p:CT_EightDirectionTransition` | 1 | 0 | 1 | §19.5.32 p.2646 |
| [`p:custData`](#pcustdata) | `p:CT_CustomerData` | 1 | 0 | 1 | §19.3.1.17 p.2586, OI 2.1.1122 |
| [`p:custDataLst`](#pcustdatalst) | `p:CT_CustomerDataList` | 0 | 2 | 4 | §19.3.1.18 p.2586 |
| [`p:custShow`](#pcustshow) | `p:CT_CustomShow`, `p:CT_CustomShowId` | 2 | 2 | 3 | §19.2.1.5 p.2536, OI 2.1.1098; §19.2.1.6 p.2537, OI 2.1.1099 |
| [`p:custShowLst`](#pcustshowlst) | `p:CT_CustomShowList` | 0 | 1 | 1 | §19.2.1.7 p.2537 |
| [`p:cut`](#pcut) | `p:CT_OptionalBlackTransition` | 1 | 0 | 1 | §19.5.34 p.2650 |
| [`p:cxnSp`](#pcxnsp) | `p:CT_Connector` | 0 | 4 | 2 | §19.3.1.19 p.2586 |
| [`p:defaultTextStyle`](#pdefaulttextstyle) | `a:CT_TextListStyle` | 0 | 11 | 1 | §19.2.1.8 p.2538 |
| [`p:diamond`](#pdiamond) | `p:CT_Empty` | 0 | 0 | 1 | §19.5.35 p.2651 |
| [`p:dissolve`](#pdissolve) | `p:CT_Empty` | 0 | 0 | 1 | §19.5.36 p.2652 |
| [`p:embed`](#pembed) | `p:CT_OleObjectEmbed` | 1 | 1 | 1 | §19.3.2.2 p.2605, OI 2.1.1132 |
| [`p:embeddedFont`](#pembeddedfont) | `p:CT_EmbeddedFontListEntry` | 0 | 5 | 1 | §19.2.1.9 p.2538 |
| [`p:embeddedFontLst`](#pembeddedfontlst) | `p:CT_EmbeddedFontList` | 0 | 1 | 1 | §19.2.1.10 p.2538, OI 2.1.1100 |
| [`p:endCondLst`](#pendcondlst) | `p:CT_TLTimeConditionList` | 0 | 1 | 1 | §19.5.37 p.2652 |
| [`p:endSnd`](#pendsnd) | `p:CT_Empty` | 0 | 0 | 1 | §19.5.38 p.2653 |
| [`p:endSync`](#pendsync) | `p:CT_TLTimeCondition` | 2 | 3 | 1 | §19.5.39 p.2653, OI 2.1.1158 |
| [`p:excl`](#pexcl) | `p:CT_TLTimeNodeExclusive` | 0 | 1 | 3 | §19.5.40 p.2654, OI 2.1.1159 |
| [`p:ext`](#pext) | `p:CT_Extension` | 1 | 1 | 13 | §19.2.1.11 p.2538, OI 2.1.1101 |
| [`p:extLst`](#pextlst) | `p:CT_ExtensionList`, `p:CT_ExtensionListModify` | 1 | 1 | 47 | §19.2.1.12 p.2539; §19.3.1.20 p.2587 |
| [`p:fade`](#pfade) | `p:CT_OptionalBlackTransition` | 1 | 0 | 1 | §19.5.41 p.2654 |
| [`p:fltVal`](#pfltval) | `p:CT_TLAnimVariantFloatVal` | 1 | 0 | 3 | §19.5.42 p.2656 |
| [`p:font`](#pfont) | `a:CT_TextFont` | 4 | 0 | 1 | §19.2.1.13 p.2539, OI 2.1.1102 |
| [`p:from`](#pfrom) | `a:CT_Color`, `p:CT_TLPoint` | 2 | 6 | 3 | §19.5.43 p.2656, OI 2.1.1160; §19.5.44 p.2656, OI 2.1.1161 |
| [`p:graphicEl`](#pgraphicel) | `a:CT_AnimationElementChoice` | 0 | 2 | 1 | §19.5.45 p.2657, OI 2.1.1162 |
| [`p:graphicFrame`](#pgraphicframe) | `p:CT_GraphicalObjectFrame` | 1 | 4 | 2 | §19.3.1.21 p.2588 |
| [`p:gridSpacing`](#pgridspacing) | `a:CT_PositiveSize2D` | 2 | 0 | 1 | §19.2.2.3 p.2563 |
| [`p:grpSp`](#pgrpsp) | `p:CT_GroupShape` | 0 | 9 | 2 | §19.3.1.22 p.2588 |
| [`p:grpSpPr`](#pgrpsppr) | `a:CT_GroupShapeProperties` | 1 | 11 | 2 | §19.3.1.23 p.2589, OI 2.1.1123 |
| [`p:guide`](#pguide) | `p:CT_Guide` | 2 | 0 | 1 | §19.2.2.4 p.2564, OI 2.1.1112 |
| [`p:guideLst`](#pguidelst) | `p:CT_GuideList` | 0 | 1 | 1 | §19.2.2.5 p.2564 |
| [`p:handoutMaster`](#phandoutmaster) ◆ | `p:CT_HandoutMaster` | 0 | 4 | 0 | §19.3.1.24 p.2590 |
| [`p:handoutMasterId`](#phandoutmasterid) | `p:CT_HandoutMasterIdListEntry` | 1 | 1 | 1 | §19.2.1.14 p.2542 |
| [`p:handoutMasterIdLst`](#phandoutmasteridlst) | `p:CT_HandoutMasterIdList` | 0 | 1 | 1 | §19.2.1.15 p.2543 |
| [`p:hf`](#phf) | `p:CT_HeaderFooter` | 4 | 1 | 4 | §19.3.1.25 p.2590 |
| [`p:hsl`](#phsl) | `p:CT_TLByHslColorTransform` | 3 | 0 | 1 | §19.5.46 p.2658, OI 2.1.1163 |
| [`p:htmlPubPr`](#phtmlpubpr) | `p:CT_HtmlPublishProperties` | 4 | 4 | 1 | Part 4 §16.3.1.1 p.246 |
| [`p:inkTgt`](#pinktgt) | `p:CT_TLSubShapeId` | 1 | 0 | 1 | §19.5.47 p.2659, OI 2.1.1164 |
| [`p:intVal`](#pintval) | `p:CT_TLAnimVariantIntegerVal` | 1 | 0 | 3 | §19.5.48 p.2659 |
| [`p:italic`](#pitalic) | `p:CT_EmbeddedFontDataId` | 1 | 0 | 1 | §19.2.1.16 p.2543 |
| [`p:iterate`](#piterate) | `p:CT_TLIterateData` | 2 | 2 | 1 | §19.5.49 p.2660, OI 2.1.1165 |
| [`p:kinsoku`](#pkinsoku) | `p:CT_Kinsoku` | 3 | 0 | 1 | §19.2.1.17 p.2544, OI 2.1.1103 |
| [`p:kiosk`](#pkiosk) | `p:CT_ShowInfoKiosk` | 1 | 0 | 1 | §19.2.1.18 p.2544 |
| [`p:link`](#plink) | `p:CT_OleObjectLink` | 1 | 1 | 1 | §19.3.2.3 p.2606, OI 2.1.1133 |
| [`p:modifyVerifier`](#pmodifyverifier) | `p:CT_ModifyVerifier` | 16 | 0 | 1 | §19.2.1.19 p.2545, OI 2.1.1104 |
| [`p:newsflash`](#pnewsflash) | `p:CT_Empty` | 0 | 0 | 1 | §19.5.50 p.2661 |
| [`p:nextCondLst`](#pnextcondlst) | `p:CT_TLTimeConditionList` | 0 | 1 | 1 | §19.5.51 p.2661, OI 2.1.1166 |
| [`p:normalViewPr`](#pnormalviewpr) | `p:CT_NormalViewProperties` | 5 | 3 | 1 | §19.2.2.6 p.2564, OI 2.1.1113 |
| [`p:notes`](#pnotes) ◆ | `p:CT_NotesSlide` | 2 | 3 | 0 | §19.3.1.26 p.2591 |
| [`p:notesMaster`](#pnotesmaster) ◆ | `p:CT_NotesMaster` | 0 | 5 | 0 | §19.3.1.27 p.2591, OI 2.1.1124 |
| [`p:notesMasterId`](#pnotesmasterid) | `p:CT_NotesMasterIdListEntry` | 1 | 1 | 1 | §19.2.1.20 p.2549 |
| [`p:notesMasterIdLst`](#pnotesmasteridlst) | `p:CT_NotesMasterIdList` | 0 | 1 | 1 | §19.2.1.21 p.2549 |
| [`p:notesStyle`](#pnotesstyle) | `a:CT_TextListStyle` | 0 | 11 | 1 | §19.3.1.28 p.2592, OI 2.1.1125 |
| [`p:notesSz`](#pnotessz) | `a:CT_PositiveSize2D` | 2 | 0 | 1 | §19.2.1.22 p.2549, OI 2.1.1105 |
| [`p:notesTextViewPr`](#pnotestextviewpr) | `p:CT_NotesTextViewProperties` | 0 | 2 | 1 | §19.2.2.7 p.2566 |
| [`p:notesViewPr`](#pnotesviewpr) | `p:CT_NotesViewProperties` | 0 | 2 | 1 | §19.2.2.8 p.2566 |
| [`p:nvCxnSpPr`](#pnvcxnsppr) | `p:CT_ConnectorNonVisual` | 0 | 3 | 1 | §19.3.1.29 p.2592 |
| [`p:nvGraphicFramePr`](#pnvgraphicframepr) | `p:CT_GraphicalObjectFrameNonVisual` | 0 | 3 | 1 | §19.3.1.30 p.2592 |
| [`p:nvGrpSpPr`](#pnvgrpsppr) | `p:CT_GroupShapeNonVisual` | 0 | 3 | 2 | §19.3.1.31 p.2592 |
| [`p:nvPicPr`](#pnvpicpr) | `p:CT_PictureNonVisual` | 0 | 3 | 1 | §19.3.1.32 p.2592 |
| [`p:nvPr`](#pnvpr) | `p:CT_ApplicationNonVisualDrawingProps` | 2 | 8 | 5 | §19.3.1.33 p.2593, OI 2.1.1126 |
| [`p:nvSpPr`](#pnvsppr) | `p:CT_ShapeNonVisual` | 0 | 3 | 1 | §19.3.1.34 p.2593 |
| [`p:oleChartEl`](#polechartel) | `p:CT_TLOleChartTargetElement` | 2 | 0 | 1 | §19.5.52 p.2662, OI 2.1.1167 |
| [`p:oleObj`](#poleobj) ◆ | `p:CT_OleObject` | 7 | 3 | 0 | §19.3.2.4 p.2606, OI 2.1.1134 |
| [`p:origin`](#porigin) | `a:CT_Point2D` | 2 | 0 | 1 | §19.2.2.9 p.2566, OI 2.1.1114 |
| [`p:otherStyle`](#potherstyle) | `a:CT_TextListStyle` | 0 | 11 | 1 | §19.3.1.35 p.2593 |
| [`p:outlineViewPr`](#poutlineviewpr) | `p:CT_OutlineViewProperties` | 0 | 3 | 1 | §19.2.2.10 p.2567 |
| [`p:pRg`](#pprg) | `p:CT_IndexRange` | 2 | 0 | 1 | §19.5.56 p.2664, OI 2.1.1169 |
| [`p:par`](#ppar) | `p:CT_TLTimeNodeParallel` | 0 | 1 | 3 | §19.5.53 p.2662 |
| [`p:penClr`](#ppenclr) | `a:CT_Color` | 0 | 6 | 1 | §19.2.1.23 p.2550 |
| [`p:ph`](#pph) | `p:CT_Placeholder` | 5 | 1 | 2 | §19.3.1.36 p.2594, OI 2.1.1127 |
| [`p:photoAlbum`](#pphotoalbum) | `p:CT_PhotoAlbum` | 4 | 1 | 1 | §19.2.1.24 p.2551, OI 2.1.1106 |
| [`p:pic`](#ppic) | `p:CT_Picture` | 0 | 5 | 4 | §19.3.1.37 p.2594 |
| [`p:plus`](#pplus) | `p:CT_Empty` | 0 | 0 | 1 | §19.5.54 p.2663 |
| [`p:pos`](#ppos) | `a:CT_Point2D` | 2 | 0 | 1 | §19.4.5 p.2611, OI 2.1.1137 |
| [`p:present`](#ppresent) | `p:CT_Empty` | 0 | 0 | 1 | §19.2.1.25 p.2552, OI 2.1.1107 |
| [`p:presentation`](#ppresentation) ◆ | `p:CT_Presentation` | 12 | 15 | 0 | §19.2.1.26 p.2552, OI 2.1.1108 |
| [`p:presentationPr`](#ppresentationpr) ◆ | `p:CT_PresentationProperties` | 0 | 6 | 0 | §19.2.1.27 p.2554 |
| [`p:prevCondLst`](#pprevcondlst) | `p:CT_TLTimeConditionList` | 0 | 1 | 1 | §19.5.55 p.2664, OI 2.1.1168 |
| [`p:prnPr`](#pprnpr) | `p:CT_PrintProperties` | 5 | 1 | 1 | §19.2.1.28 p.2554 |
| [`p:progress`](#pprogress) | `p:CT_TLAnimVariant` | 0 | 5 | 1 | §19.5.57 p.2665, OI 2.1.1170 |
| [`p:pull`](#ppull) | `p:CT_EightDirectionTransition` | 1 | 0 | 1 | §19.5.58 p.2666 |
| [`p:push`](#ppush) | `p:CT_SideDirectionTransition` | 1 | 0 | 1 | §19.5.59 p.2668 |
| [`p:rCtr`](#prctr) | `p:CT_TLPoint` | 2 | 0 | 1 | §19.5.62 p.2670, OI 2.1.1171 |
| [`p:random`](#prandom) | `p:CT_Empty` | 0 | 0 | 1 | §19.5.60 p.2669 |
| [`p:randomBar`](#prandombar) | `p:CT_OrientationTransition` | 1 | 0 | 1 | §19.5.61 p.2669 |
| [`p:regular`](#pregular) | `p:CT_EmbeddedFontDataId` | 1 | 0 | 1 | §19.2.1.29 p.2555 |
| [`p:restoredLeft`](#prestoredleft) | `p:CT_NormalViewPortion` | 2 | 0 | 1 | §19.2.2.11 p.2567, OI 2.1.1115 |
| [`p:restoredTop`](#prestoredtop) | `p:CT_NormalViewPortion` | 2 | 0 | 1 | §19.2.2.12 p.2567, OI 2.1.1116 |
| [`p:rgb`](#prgb) | `p:CT_TLByRgbColorTransform` | 3 | 0 | 1 | §19.5.63 p.2671, OI 2.1.1172 |
| [`p:rtn`](#prtn) | `p:CT_TLTriggerRuntimeNode` | 1 | 0 | 2 | §19.5.64 p.2672 |
| [`p:scale`](#pscale) | `a:CT_Scale2D` | 0 | 2 | 1 | §19.2.2.13 p.2568, OI 2.1.1117 |
| [`p:seq`](#pseq) | `p:CT_TLTimeNodeSequence` | 3 | 3 | 3 | §19.5.65 p.2673, OI 2.1.1173 |
| [`p:set`](#pset) | `p:CT_TLSetBehavior` | 0 | 2 | 3 | §19.5.66 p.2674, OI 2.1.1174 |
| [`p:showPr`](#pshowpr) | `p:CT_ShowProperties` | 4 | 8 | 1 | §19.2.1.30 p.2556 |
| [`p:sld`](#psld) ◆ | `p:CT_Slide`, `p:CT_SlideRelationshipListEntry`, `p:CT_OutlineViewSlideEntry` | 5 | 5 | 1 | §19.2.1.31 p.2556; §19.2.2.14 p.2568; §19.3.1.38 p.2595, OI 2.1.1128 |
| [`p:sldAll`](#psldall) | `p:CT_Empty` | 0 | 0 | 2 | §19.2.1.32 p.2557 |
| [`p:sldId`](#psldid) | `p:CT_SlideIdListEntry` | 2 | 1 | 1 | §19.2.1.33 p.2557 |
| [`p:sldIdLst`](#psldidlst) | `p:CT_SlideIdList` | 0 | 1 | 1 | §19.2.1.34 p.2558 |
| [`p:sldLayout`](#psldlayout) ◆ | `p:CT_SlideLayout` | 6 | 6 | 0 | §19.3.1.39 p.2596 |
| [`p:sldLayoutId`](#psldlayoutid) | `p:CT_SlideLayoutIdListEntry` | 2 | 1 | 1 | §19.3.1.40 p.2597 |
| [`p:sldLayoutIdLst`](#psldlayoutidlst) | `p:CT_SlideLayoutIdList` | 0 | 1 | 1 | §19.3.1.41 p.2597 |
| [`p:sldLst`](#psldlst) | `p:CT_SlideRelationshipList`, `p:CT_OutlineViewSlideList` | 0 | 1 | 2 | §19.2.1.35 p.2558; §19.2.2.15 p.2569 |
| [`p:sldMaster`](#psldmaster) ◆ | `p:CT_SlideMaster` | 1 | 8 | 0 | §19.3.1.42 p.2597 |
| [`p:sldMasterId`](#psldmasterid) | `p:CT_SlideMasterIdListEntry` | 2 | 1 | 1 | §19.2.1.36 p.2558 |
| [`p:sldMasterIdLst`](#psldmasteridlst) | `p:CT_SlideMasterIdList` | 0 | 1 | 1 | §19.2.1.37 p.2559 |
| [`p:sldRg`](#psldrg) | `p:CT_IndexRange` | 2 | 0 | 2 | §19.2.1.38 p.2559, OI 2.1.1109 |
| [`p:sldSyncPr`](#psldsyncpr) ◆ | `p:CT_SlideSyncProperties` | 3 | 1 | 0 | §19.6.1 p.2701 |
| [`p:sldSz`](#psldsz) | `p:CT_SlideSize` | 3 | 0 | 1 | §19.2.1.39 p.2560, OI 2.1.1110 |
| [`p:sldTgt`](#psldtgt) | `p:CT_Empty` | 0 | 0 | 1 | §19.5.67 p.2675, OI 2.1.1175 |
| [`p:slideViewPr`](#pslideviewpr) | `p:CT_SlideViewProperties` | 0 | 2 | 1 | §19.2.2.16 p.2569 |
| [`p:smartTags`](#psmarttags) | `p:CT_SmartTags` | 1 | 0 | 1 | §19.2.1.40 p.2561, OI 2.1.1111 |
| [`p:snd`](#psnd) | `a:CT_EmbeddedWAVAudioFile` | 2 | 0 | 1 | §19.5.68 p.2676 |
| [`p:sndAc`](#psndac) | `p:CT_TransitionSoundAction` | 0 | 2 | 1 | §19.5.69 p.2676 |
| [`p:sndTgt`](#psndtgt) | `a:CT_EmbeddedWAVAudioFile` | 2 | 0 | 1 | §19.5.70 p.2677 |
| [`p:sorterViewPr`](#psorterviewpr) | `p:CT_SlideSorterViewProperties` | 1 | 2 | 1 | §19.2.2.17 p.2569 |
| [`p:sp`](#psp) | `p:CT_Shape` | 1 | 5 | 2 | §19.3.1.43 p.2598 |
| [`p:spPr`](#psppr) | `a:CT_ShapeProperties` | 1 | 15 | 3 | §19.3.1.44 p.2598, OI 2.1.1129 |
| [`p:spTgt`](#psptgt) | `p:CT_TLShapeTargetElement` | 1 | 5 | 1 | §19.5.72 p.2679, OI 2.1.1176 |
| [`p:spTree`](#psptree) | `p:CT_GroupShape` | 0 | 9 | 1 | §19.3.1.45 p.2599 |
| [`p:split`](#psplit) | `p:CT_SplitTransition` | 2 | 0 | 1 | §19.5.71 p.2678 |
| [`p:stCondLst`](#pstcondlst) | `p:CT_TLTimeConditionList` | 0 | 1 | 1 | §19.5.73 p.2680 |
| [`p:stSnd`](#pstsnd) | `p:CT_TransitionStartSoundAction` | 1 | 1 | 1 | §19.5.76 p.2682 |
| [`p:strVal`](#pstrval) | `p:CT_TLAnimVariantStringVal` | 1 | 0 | 3 | §19.5.75 p.2682 |
| [`p:strips`](#pstrips) | `p:CT_CornerDirectionTransition` | 1 | 0 | 1 | §19.5.74 p.2680 |
| [`p:style`](#pstyle) | `a:CT_ShapeStyle` | 0 | 4 | 3 | §19.3.1.46 p.2600 |
| [`p:subSp`](#psubsp) | `p:CT_TLSubShapeId` | 1 | 0 | 1 | §19.5.77 p.2682, OI 2.1.1177 |
| [`p:subTnLst`](#psubtnlst) | `p:CT_TimeNodeList` | 0 | 13 | 1 | §19.5.78 p.2683, OI 2.1.1178 |
| [`p:tag`](#ptag) | `p:CT_StringTag` | 2 | 0 | 1 | §19.3.3.1 p.2607, OI 2.1.1135 |
| [`p:tagLst`](#ptaglst) ◆ | `p:CT_TagList` | 0 | 1 | 0 | §19.3.3.2 p.2607 |
| [`p:tags`](#ptags) | `p:CT_TagsData` | 1 | 0 | 1 | §19.3.1.47 p.2601 |
| [`p:tav`](#ptav) | `p:CT_TLTimeAnimateValue` | 2 | 1 | 1 | §19.5.79 p.2684 |
| [`p:tavLst`](#ptavlst) | `p:CT_TLTimeAnimateValueList` | 0 | 1 | 1 | §19.5.80 p.2688 |
| [`p:text`](#ptext) | `xsd:string` | 0 | 0 | 1 | §19.4.6 p.2612 |
| [`p:tgtEl`](#ptgtel) | `p:CT_TLTimeTargetElement` | 0 | 4 | 4 | §19.5.81 p.2689 |
| [`p:timing`](#ptiming) | `p:CT_SlideTiming` | 0 | 3 | 3 | §19.3.1.48 p.2601 |
| [`p:titleStyle`](#ptitlestyle) | `a:CT_TextListStyle` | 0 | 11 | 1 | §19.3.1.49 p.2602 |
| [`p:tmAbs`](#ptmabs) | `p:CT_TLIterateIntervalTime` | 1 | 0 | 1 | §19.5.82 p.2689 |
| [`p:tmPct`](#ptmpct) | `p:CT_TLIterateIntervalPercentage` | 1 | 0 | 1 | §19.5.83 p.2690 |
| [`p:tmpl`](#ptmpl) | `p:CT_TLTemplate` | 1 | 1 | 1 | §19.5.84 p.2691, OI 2.1.1179 |
| [`p:tmplLst`](#ptmpllst) | `p:CT_TLTemplateList` | 0 | 1 | 1 | §19.5.85 p.2691, OI 2.1.1180 |
| [`p:tn`](#ptn) | `p:CT_TLTriggerTimeNodeID` | 1 | 0 | 2 | §19.5.86 p.2692 |
| [`p:tnLst`](#ptnlst) | `p:CT_TimeNodeList` | 0 | 13 | 2 | §19.5.87 p.2693, OI 2.1.1181 |
| [`p:to`](#pto) | `a:CT_Color`, `p:CT_TLPoint`, `p:CT_TLAnimVariant` | 2 | 11 | 4 | §19.5.88 p.2693, OI 2.1.1182; §19.5.89 p.2694, OI 2.1.1183; §19.5.90 p.2694, OI 2.1.1184 |
| [`p:transition`](#ptransition) | `p:CT_SlideTransition` | 3 | 23 | 3 | §19.3.1.50 p.2602 |
| [`p:txBody`](#ptxbody) | `a:CT_TextBody` | 0 | 3 | 1 | §19.3.1.51 p.2603 |
| [`p:txEl`](#ptxel) | `p:CT_TLTextTargetElement` | 0 | 2 | 1 | §19.5.91 p.2695 |
| [`p:txStyles`](#ptxstyles) | `p:CT_SlideMasterTextStyles` | 0 | 4 | 1 | §19.3.1.52 p.2603 |
| [`p:val`](#pval) | `p:CT_TLAnimVariant` | 0 | 5 | 1 | §19.5.92 p.2695 |
| [`p:video`](#pvideo) | `p:CT_TLMediaNodeVideo` | 1 | 1 | 3 | §19.5.93 p.2696, OI 2.1.1185 |
| [`p:viewPr`](#pviewpr) ◆ | `p:CT_ViewProperties` | 2 | 8 | 0 | §19.2.2.18 p.2570 |
| [`p:webPr`](#pwebpr) | `p:CT_WebProperties` | 9 | 1 | 1 | Part 4 §16.3.1.2 p.247 |
| [`p:wedge`](#pwedge) | `p:CT_Empty` | 0 | 0 | 1 | §19.5.94 p.2697 |
| [`p:wheel`](#pwheel) | `p:CT_WheelTransition` | 1 | 0 | 1 | §19.5.95 p.2697, OI 2.1.1186 |
| [`p:wipe`](#pwipe) | `p:CT_SideDirectionTransition` | 1 | 0 | 1 | §19.5.96 p.2699 |
| [`p:xfrm`](#pxfrm) | `a:CT_Transform2D` | 3 | 2 | 1 | §19.3.1.53 p.2603, OI 2.1.1130 |
| [`p:zoom`](#pzoom) | `p:CT_InOutTransition` | 1 | 0 | 1 | §19.5.97 p.2700 |

◆ declared globally (can be the root element of a part)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `p:ST_BookmarkIdSeed` | restriction of xsd:unsignedInt | minInclusive `1`, maxExclusive `2147483648` | §19.7.1 p.2702 |
| `p:ST_Direction` | restriction of xsd:token | `horz`, `vert` | §19.7.2 p.2703 |
| `p:ST_Index` | restriction of xsd:unsignedInt |  | §19.7.3 p.2703 |
| `p:ST_IterateType` | restriction of xsd:token | `el`, `wd`, `lt` | §19.7.4 p.2703 |
| `p:ST_Name` | restriction of xsd:string |  | §19.7.5 p.2703 |
| `p:ST_OleObjectFollowColorScheme` | restriction of xsd:token | `none`, `full`, `textAndBackground` | §19.7.6 p.2704 |
| `p:ST_PhotoAlbumFrameShape` | restriction of xsd:token | `frameStyle1`, `frameStyle2`, `frameStyle3`, `frameStyle4`, `frameStyle5`, `frameStyle6`, `frameStyle7` | §19.7.7 p.2704, OI 2.1.1187 |
| `p:ST_PhotoAlbumLayout` | restriction of xsd:token | `fitToSlide`, `1pic`, `2pic`, `4pic`, `1picTitle`, `2picTitle`, `4picTitle` | §19.7.8 p.2705 |
| `p:ST_PlaceholderSize` | restriction of xsd:token | `full`, `half`, `quarter` | §19.7.9 p.2707 |
| `p:ST_PlaceholderType` | restriction of xsd:token | `title`, `body`, `ctrTitle`, `subTitle`, `dt`, `sldNum`, `ftr`, `hdr`, `obj`, `chart`, `tbl`, `clipArt` … (+4) | §19.7.10 p.2707 |
| `p:ST_PrintColorMode` | restriction of xsd:token | `bw`, `gray`, `clr` | §19.7.11 p.2708 |
| `p:ST_PrintWhat` | restriction of xsd:token | `slides`, `handouts1`, `handouts2`, `handouts3`, `handouts4`, `handouts6`, `handouts9`, `notes`, `outline` | §19.7.12 p.2709 |
| `p:ST_SlideId` | restriction of xsd:unsignedInt | minInclusive `256`, maxExclusive `2147483648` | §19.7.13 p.2709 |
| `p:ST_SlideLayoutId` | restriction of xsd:unsignedInt | minInclusive `2147483648` | §19.7.14 p.2709 |
| `p:ST_SlideLayoutType` | restriction of xsd:token | `title`, `tx`, `twoColTx`, `tbl`, `txAndChart`, `chartAndTx`, `dgm`, `chart`, `txAndClipArt`, `clipArtAndTx`, `titleOnly`, `blank` … (+24) | §19.7.15 p.2710 |
| `p:ST_SlideMasterId` | restriction of xsd:unsignedInt | minInclusive `2147483648` | §19.7.16 p.2713 |
| `p:ST_SlideSizeCoordinate` | restriction of a:ST_PositiveCoordinate32 | minInclusive `914400`, maxInclusive `51206400` | §19.7.17 p.2713 |
| `p:ST_SlideSizeType` | restriction of xsd:token | `screen4x3`, `letter`, `A4`, `35mm`, `overhead`, `banner`, `custom`, `ledger`, `A3`, `B4ISO`, `B5ISO`, `B4JIS` … (+4) | §19.7.18 p.2714 |
| `p:ST_SplitterBarState` | restriction of xsd:token | `minimized`, `restored`, `maximized` | §19.7.19 p.2714 |
| `p:ST_TLAnimateBehaviorCalcMode` | restriction of xsd:token | `discrete`, `lin`, `fmla` | §19.7.20 p.2715 |
| `p:ST_TLAnimateBehaviorValueType` | restriction of xsd:token | `str`, `num`, `clr` | §19.7.21 p.2715 |
| `p:ST_TLAnimateColorDirection` | restriction of xsd:token | `cw`, `ccw` | §19.7.22 p.2716 |
| `p:ST_TLAnimateColorSpace` | restriction of xsd:token | `rgb`, `hsl` | §19.7.23 p.2716 |
| `p:ST_TLAnimateEffectTransition` | restriction of xsd:token | `in`, `out`, `none` | §19.7.24 p.2716, OI 2.1.1188 |
| `p:ST_TLAnimateMotionBehaviorOrigin` | restriction of xsd:token | `parent`, `layout` | §19.7.25 p.2717, OI 2.1.1189 |
| `p:ST_TLAnimateMotionPathEditMode` | restriction of xsd:token | `relative`, `fixed` | §19.7.26 p.2717 |
| `p:ST_TLBehaviorAccumulateType` | restriction of xsd:token | `none`, `always` | §19.7.27 p.2717 |
| `p:ST_TLBehaviorAdditiveType` | restriction of xsd:token | `base`, `sum`, `repl`, `mult`, `none` | §19.7.28 p.2718, OI 2.1.1190 |
| `p:ST_TLBehaviorOverrideType` | restriction of xsd:token | `normal`, `childStyle` | §19.7.29 p.2718 |
| `p:ST_TLBehaviorTransformType` | restriction of xsd:token | `pt`, `img` | §19.7.30 p.2718, OI 2.1.1191 |
| `p:ST_TLChartSubelementType` | restriction of xsd:token | `gridLegend`, `series`, `category`, `ptInSeries`, `ptInCategory` | §19.7.31 p.2719 |
| `p:ST_TLCommandType` | restriction of xsd:token | `evt`, `call`, `verb` | §19.7.32 p.2719 |
| `p:ST_TLDiagramBuildType` | restriction of xsd:token | `whole`, `depthByNode`, `depthByBranch`, `breadthByNode`, `breadthByLvl`, `cw`, `cwIn`, `cwOut`, `ccw`, `ccwIn`, `ccwOut`, `inByRing` … (+5) | §19.7.33 p.2720 |
| `p:ST_TLNextActionType` | restriction of xsd:token | `none`, `seek` | §19.7.34 p.2720 |
| `p:ST_TLOleChartBuildType` | restriction of xsd:token | `allAtOnce`, `series`, `category`, `seriesEl`, `categoryEl` | §19.7.35 p.2721 |
| `p:ST_TLParaBuildType` | restriction of xsd:token | `allAtOnce`, `p`, `cust`, `whole` | §19.7.36 p.2721 |
| `p:ST_TLPreviousActionType` | restriction of xsd:token | `none`, `skipTimed` | §19.7.37 p.2722 |
| `p:ST_TLTime` | union of xsd:unsignedInt, p:ST_TLTimeIndefinite |  | §19.7.38 p.2722, OI 2.1.1192 |
| `p:ST_TLTimeAnimateValueTime` | union of a:ST_PositiveFixedPercentage, p:ST_TLTimeIndefinite |  | §19.7.39 p.2722 |
| `p:ST_TLTimeIndefinite` | restriction of xsd:token | `indefinite` | §19.7.40 p.2723 |
| `p:ST_TLTimeNodeFillType` | restriction of xsd:token | `remove`, `freeze`, `hold`, `transition` | §19.7.41 p.2723, OI 2.1.1193 |
| `p:ST_TLTimeNodeID` | restriction of xsd:unsignedInt |  | §19.7.42 p.2723 |
| `p:ST_TLTimeNodeMasterRelation` | restriction of xsd:token | `sameClick`, `lastClick`, `nextClick` | §19.7.43 p.2723, OI 2.1.1194 |
| `p:ST_TLTimeNodePresetClassType` | restriction of xsd:token | `entr`, `exit`, `emph`, `path`, `verb`, `mediacall` | §19.7.44 p.2724 |
| `p:ST_TLTimeNodeRestartType` | restriction of xsd:token | `always`, `whenNotActive`, `never` | §19.7.45 p.2724, OI 2.1.1195 |
| `p:ST_TLTimeNodeSyncType` | restriction of xsd:token | `canSlip`, `locked` | §19.7.46 p.2725 |
| `p:ST_TLTimeNodeType` | restriction of xsd:token | `clickEffect`, `withEffect`, `afterEffect`, `mainSeq`, `interactiveSeq`, `clickPar`, `withGroup`, `afterGroup`, `tmRoot` | §19.7.47 p.2725 |
| `p:ST_TLTriggerEvent` | restriction of xsd:token | `onBegin`, `onEnd`, `begin`, `end`, `onClick`, `onDblClick`, `onMouseOver`, `onMouseOut`, `onNext`, `onPrev`, `onStopAudio` | §19.7.48 p.2726, OI 2.1.1196 |
| `p:ST_TLTriggerRuntimeNode` | restriction of xsd:token | `first`, `last`, `all` | §19.7.49 p.2726 |
| `p:ST_TransitionCornerDirectionType` | restriction of xsd:token | `lu`, `ru`, `ld`, `rd` | §19.7.50 p.2727 |
| `p:ST_TransitionEightDirectionType` | union of p:ST_TransitionSideDirectionType, p:ST_TransitionCornerDirectionType |  | §19.7.51 p.2727 |
| `p:ST_TransitionInOutDirectionType` | restriction of xsd:token | `out`, `in` | §19.7.52 p.2727 |
| `p:ST_TransitionSideDirectionType` | restriction of xsd:token | `l`, `u`, `r`, `d` | §19.7.53 p.2728 |
| `p:ST_TransitionSpeed` | restriction of xsd:token | `slow`, `med`, `fast` | §19.7.54 p.2728 |
| `p:ST_ViewType` | restriction of xsd:token | `sldView`, `sldMasterView`, `notesView`, `handoutView`, `notesMasterView`, `outlineView`, `sldSorterView`, `sldThumbnailView` | §19.7.55 p.2728, OI 2.1.1197 |
| `p:ST_WebColorType` | restriction of xsd:token | `none`, `browser`, `presentationText`, `presentationAccent`, `whiteTextOnBlack`, `blackTextOnWhite` | Part 4 §16.5.1 p.255 |
| `p:ST_WebEncoding` | restriction of xsd:string |  | Part 4 §16.5.2 p.255 |
| `p:ST_WebScreenSize` | restriction of xsd:token | `544x376`, `640x480`, `720x512`, `800x600`, `1024x768`, `1152x882`, `1152x900`, `1280x1024`, `1600x1200`, `1800x1400`, `1920x1200` | Part 4 §16.5.3 p.256 |

## Element details

### p:anim

- Type: `p:CT_TLAnimateBehavior`
- Attributes: `by`, `calcmode`, `from`, `to`, `valueType`
- Children: `p:cBhvr`, `p:tavLst`
- Parents: `p:childTnLst`, `p:subTnLst`, `p:tnLst`
- Specification: §19.5.1 p.2613, OI 2.1.1138

### p:animClr

- Type: `p:CT_TLAnimateColorBehavior`
- Attributes: `clrSpc`, `dir`
- Children: `p:cBhvr`, `p:by`, `p:from`, `p:to`
- Parents: `p:childTnLst`, `p:subTnLst`, `p:tnLst`
- Specification: §19.5.2 p.2614, OI 2.1.1139

### p:animEffect

- Type: `p:CT_TLAnimateEffectBehavior`
- Attributes: `filter`, `prLst`, `transition`
- Children: `p:cBhvr`, `p:progress`
- Parents: `p:childTnLst`, `p:subTnLst`, `p:tnLst`
- Specification: §19.5.3 p.2615, OI 2.1.1140

### p:animMotion

- Type: `p:CT_TLAnimateMotionBehavior`
- Attributes: `origin`, `path`, `pathEditMode`, `ptsTypes`, `rAng`
- Children: `p:cBhvr`, `p:by`, `p:from`, `p:to`, `p:rCtr`
- Parents: `p:childTnLst`, `p:subTnLst`, `p:tnLst`
- Specification: §19.5.4 p.2621, OI 2.1.1141

### p:animRot

- Type: `p:CT_TLAnimateRotationBehavior`
- Attributes: `by`, `from`, `to`
- Children: `p:cBhvr`
- Parents: `p:childTnLst`, `p:subTnLst`, `p:tnLst`
- Specification: §19.5.5 p.2622, OI 2.1.1142

### p:animScale

- Type: `p:CT_TLAnimateScaleBehavior`
- Attributes: `zoomContents`
- Children: `p:cBhvr`, `p:by`, `p:from`, `p:to`
- Parents: `p:childTnLst`, `p:subTnLst`, `p:tnLst`
- Specification: §19.5.6 p.2623, OI 2.1.1143

### p:attrName

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `p:attrNameLst`
- Specification: §19.5.7 p.2624, OI 2.1.1144

### p:attrNameLst

- Type: `p:CT_TLBehaviorAttributeNameList`
- Children: `p:attrName`
- Parents: `p:cBhvr`
- Specification: §19.5.8 p.2625

### p:audio

- Type: `p:CT_TLMediaNodeAudio`
- Attributes: `isNarration`
- Children: `p:cMediaNode`
- Parents: `p:childTnLst`, `p:subTnLst`, `p:tnLst`
- Specification: §19.5.9 p.2626, OI 2.1.1145

### p:bg

- Type: `p:CT_Empty`, `p:CT_Background`
- Attributes: `bwMode`
- Children: `p:bgPr`, `p:bgRef`
- Parents: `p:cSld`, `p:spTgt`
- Specification: §19.3.1.1 p.2570, OI 2.1.1118; §19.5.10 p.2627

### p:bgPr

- Type: `p:CT_BackgroundProperties`
- Attributes: `shadeToTitle`
- Children: `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`, `a:effectLst`, `a:effectDag`, `p:extLst`
- Parents: `p:bg`
- Specification: §19.3.1.2 p.2571, OI 2.1.1119

### p:bgRef

- Type: `a:CT_StyleMatrixReference`
- Attributes: `idx`!
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `p:bg`
- Specification: §19.3.1.3 p.2571

### p:bldAsOne

- Type: `p:CT_Empty`
- Parents: `p:bldGraphic`
- Specification: §19.5.11 p.2627

### p:bldDgm

- Type: `p:CT_TLBuildDiagram`
- Attributes: `bld`, `grpId`!, `spid`!, `uiExpand`
- Parents: `p:bldLst`
- Specification: §19.5.12 p.2627, OI 2.1.1146

### p:bldGraphic

- Type: `p:CT_TLGraphicalObjectBuild`
- Attributes: `grpId`!, `spid`!, `uiExpand`
- Children: `p:bldAsOne`, `p:bldSub`
- Parents: `p:bldLst`
- Specification: §19.5.13 p.2628, OI 2.1.1147

### p:bldLst

- Type: `p:CT_BuildList`
- Children: `p:bldP`, `p:bldDgm`, `p:bldOleChart`, `p:bldGraphic`
- Parents: `p:timing`
- Specification: §19.5.14 p.2629

### p:bldOleChart

- Type: `p:CT_TLOleBuildChart`
- Attributes: `animBg`, `bld`, `grpId`!, `spid`!, `uiExpand`
- Parents: `p:bldLst`
- Specification: §19.5.15 p.2630, OI 2.1.1148

### p:bldP

- Type: `p:CT_TLBuildParagraph`
- Attributes: `advAuto`, `animBg`, `autoUpdateAnimBg`, `bldLvl`, `build`, `grpId`!, `rev`, `spid`!, `uiExpand`
- Children: `p:tmplLst`
- Parents: `p:bldLst`
- Specification: §19.5.16 p.2631, OI 2.1.1149

### p:bldSub

- Type: `a:CT_AnimationGraphicalObjectBuildProperties`
- Children: `a:bldDgm`, `a:bldChart`
- Parents: `p:bldGraphic`
- Specification: §19.5.17 p.2633, OI 2.1.1150

### p:blinds

- Type: `p:CT_OrientationTransition`
- Attributes: `dir`
- Parents: `p:transition`
- Specification: §19.5.18 p.2633

### p:blipFill

- Type: `a:CT_BlipFillProperties`
- Attributes: `dpi`, `rotWithShape`
- Children: `a:blip`, `a:srcRect`, `a:tile`, `a:stretch`
- Parents: `p:pic`
- Specification: §19.3.1.4 p.2572, OI 2.1.1120

### p:bodyStyle

- Type: `a:CT_TextListStyle`
- Children: `a:defPPr`, `a:lvl1pPr`, `a:lvl2pPr`, `a:lvl3pPr`, `a:lvl4pPr`, `a:lvl5pPr`, `a:lvl6pPr`, `a:lvl7pPr`, `a:lvl8pPr`, `a:lvl9pPr`, `a:extLst`
- Parents: `p:txStyles`
- Specification: §19.3.1.5 p.2575

### p:bold

- Type: `p:CT_EmbeddedFontDataId`
- Attributes: `r:id`!
- Parents: `p:embeddedFont`
- Specification: §19.2.1.1 p.2533

### p:boldItalic

- Type: `p:CT_EmbeddedFontDataId`
- Attributes: `r:id`!
- Parents: `p:embeddedFont`
- Specification: §19.2.1.2 p.2534

### p:boolVal

- Type: `p:CT_TLAnimVariantBooleanVal`
- Attributes: `val`!
- Parents: `p:progress`, `p:to`, `p:val`
- Specification: §19.5.19 p.2634

### p:browse

- Type: `p:CT_ShowInfoBrowse`
- Attributes: `showScrollbar`
- Parents: `p:showPr`
- Specification: §19.2.1.3 p.2535

### p:by

- Type: `p:CT_TLByAnimateColorTransform`, `p:CT_TLPoint`
- Attributes: `x`!, `y`!
- Children: `p:rgb`, `p:hsl`
- Parents: `p:animClr`, `p:animMotion`, `p:animScale`
- Specification: §19.5.20 p.2635; §19.5.21 p.2635, OI 2.1.1151

### p:cBhvr

- Type: `p:CT_TLCommonBehaviorData`
- Attributes: `accumulate`, `additive`, `by`, `from`, `override`, `rctx`, `to`, `xfrmType`
- Children: `p:cTn`, `p:tgtEl`, `p:attrNameLst`
- Parents: `p:anim`, `p:animClr`, `p:animEffect`, `p:animMotion`, `p:animRot`, `p:animScale`, `p:cmd`, `p:set`
- Specification: §19.5.22 p.2636, OI 2.1.1152

### p:cMediaNode

- Type: `p:CT_TLCommonMediaNodeData`
- Attributes: `mute`, `numSld`, `showWhenStopped`, `vol`
- Children: `p:cTn`, `p:tgtEl`
- Parents: `p:audio`, `p:video`
- Specification: §19.5.29 p.2643, OI 2.1.1155

### p:cNvCxnSpPr

- Type: `a:CT_NonVisualConnectorProperties`
- Children: `a:cxnSpLocks`, `a:stCxn`, `a:endCxn`, `a:extLst`
- Parents: `p:nvCxnSpPr`
- Specification: §19.3.1.8 p.2577

### p:cNvGraphicFramePr

- Type: `a:CT_NonVisualGraphicFrameProperties`
- Children: `a:graphicFrameLocks`, `a:extLst`
- Parents: `p:nvGraphicFramePr`
- Specification: §19.3.1.9 p.2577

### p:cNvGrpSpPr

- Type: `a:CT_NonVisualGroupDrawingShapeProps`
- Children: `a:grpSpLocks`, `a:extLst`
- Parents: `p:nvGrpSpPr`
- Specification: §19.3.1.10 p.2578

### p:cNvPicPr

- Type: `a:CT_NonVisualPictureProperties`
- Attributes: `preferRelativeResize`
- Children: `a:picLocks`, `a:extLst`
- Parents: `p:nvPicPr`
- Specification: §19.3.1.11 p.2578

### p:cNvPr

- Type: `a:CT_NonVisualDrawingProps`
- Attributes: `descr`, `hidden`, `id`!, `name`!, `title`
- Children: `a:hlinkClick`, `a:hlinkHover`, `a:extLst`
- Parents: `p:nvCxnSpPr`, `p:nvGraphicFramePr`, `p:nvGrpSpPr`, `p:nvPicPr`, `p:nvSpPr`
- Specification: §19.3.1.12 p.2579

### p:cNvSpPr

- Type: `a:CT_NonVisualDrawingShapeProps`
- Attributes: `txBox`
- Children: `a:spLocks`, `a:extLst`
- Parents: `p:nvSpPr`
- Specification: §19.3.1.13 p.2581

### p:cSld

- Type: `p:CT_CommonSlideData`
- Attributes: `name`
- Children: `p:bg`, `p:spTree`, `p:custDataLst`, `p:controls`, `p:extLst`
- Parents: `p:handoutMaster`, `p:notes`, `p:notesMaster`, `p:sld`, `p:sldLayout`, `p:sldMaster`
- Specification: §19.3.1.16 p.2585, OI 2.1.1121

### p:cSldViewPr

- Type: `p:CT_CommonSlideViewProperties`
- Attributes: `showGuides`, `snapToGrid`, `snapToObjects`
- Children: `p:cViewPr`, `p:guideLst`
- Parents: `p:notesViewPr`, `p:slideViewPr`
- Specification: §19.2.2.1 p.2562

### p:cTn

- Type: `p:CT_TLCommonTimeNodeData`
- Attributes: `accel`, `afterEffect`, `autoRev`, `bldLvl`, `decel`, `display`, `dur`, `evtFilter`, `fill`, `grpId`, `id`, `masterRel`, `nodePh`, `nodeType`, `presetClass`, `presetID`, `presetSubtype`, `repeatCount`, `repeatDur`, `restart`, `spd`, `syncBehavior`, `tmFilter`
- Children: `p:stCondLst`, `p:endCondLst`, `p:endSync`, `p:iterate`, `p:childTnLst`, `p:subTnLst`
- Parents: `p:cBhvr`, `p:cMediaNode`, `p:excl`, `p:par`, `p:seq`
- Specification: §19.5.33 p.2648, OI 2.1.1157

### p:cViewPr

- Type: `p:CT_CommonViewProperties`
- Attributes: `varScale`
- Children: `p:scale`, `p:origin`
- Parents: `p:cSldViewPr`, `p:notesTextViewPr`, `p:outlineViewPr`, `p:sorterViewPr`
- Specification: §19.2.2.2 p.2563

### p:charRg

- Type: `p:CT_IndexRange`
- Attributes: `end`!, `st`!
- Parents: `p:txEl`
- Specification: §19.5.23 p.2638, OI 2.1.1153

### p:checker

- Type: `p:CT_OrientationTransition`
- Attributes: `dir`
- Parents: `p:transition`
- Specification: §19.5.24 p.2638

### p:childTnLst

- Type: `p:CT_TimeNodeList`
- Children: `p:par`, `p:seq`, `p:excl`, `p:anim`, `p:animClr`, `p:animEffect`, `p:animMotion`, `p:animRot`, `p:animScale`, `p:cmd`, `p:set`, `p:audio`, `p:video`
- Parents: `p:cTn`
- Specification: §19.5.25 p.2640

### p:circle

- Type: `p:CT_Empty`
- Parents: `p:transition`
- Specification: §19.5.26 p.2640

### p:clrMap

- Type: `a:CT_ColorMapping`
- Attributes: `accent1`!, `accent2`!, `accent3`!, `accent4`!, `accent5`!, `accent6`!, `bg1`!, `bg2`!, `folHlink`!, `hlink`!, `tx1`!, `tx2`!
- Children: `a:extLst`
- Parents: `p:handoutMaster`, `p:notesMaster`, `p:sldMaster`
- Specification: §19.3.1.6 p.2575

### p:clrMapOvr

- Type: `a:CT_ColorMappingOverride`
- Children: `a:masterClrMapping`, `a:overrideClrMapping`
- Parents: `p:notes`, `p:sld`, `p:sldLayout`
- Specification: §19.3.1.7 p.2577

### p:clrMru

- Type: `a:CT_ColorMRU`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `p:presentationPr`
- Specification: §19.2.1.4 p.2535

### p:clrVal

- Type: `a:CT_Color`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `p:progress`, `p:to`, `p:val`
- Specification: §19.5.27 p.2640

### p:cm

- Type: `p:CT_Comment`
- Attributes: `authorId`!, `dt`, `idx`!
- Children: `p:pos`, `p:text`, `p:extLst`
- Parents: `p:cmLst`
- Specification: §19.4.1 p.2608, OI 2.1.1136

### p:cmAuthor

- Type: `p:CT_CommentAuthor`
- Attributes: `clrIdx`!, `id`!, `initials`!, `lastIdx`!, `name`!
- Children: `p:extLst`
- Parents: `p:cmAuthorLst`
- Specification: §19.4.2 p.2609

### p:cmAuthorLst

- Type: `p:CT_CommentAuthorList`
- Children: `p:cmAuthor`
- Specification: §19.4.3 p.2610

### p:cmLst

- Type: `p:CT_CommentList`
- Children: `p:cm`
- Specification: §19.4.4 p.2610

### p:cmd

- Type: `p:CT_TLCommandBehavior`
- Attributes: `cmd`, `type`
- Children: `p:cBhvr`
- Parents: `p:childTnLst`, `p:subTnLst`, `p:tnLst`
- Specification: §19.5.28 p.2641, OI 2.1.1154

### p:comb

- Type: `p:CT_OrientationTransition`
- Attributes: `dir`
- Parents: `p:transition`
- Specification: §19.5.30 p.2644

### p:cond

- Type: `p:CT_TLTimeCondition`
- Attributes: `delay`, `evt`
- Children: `p:tgtEl`, `p:tn`, `p:rtn`
- Parents: `p:endCondLst`, `p:nextCondLst`, `p:prevCondLst`, `p:stCondLst`
- Specification: §19.5.31 p.2645, OI 2.1.1156

### p:contentPart

- Type: `p:CT_Rel`
- Attributes: `r:id`!
- Parents: `p:grpSp`, `p:spTree`
- Specification: §19.3.1.14 p.2582

### p:control

- Type: `p:CT_Control`
- Attributes: `imgH`, `imgW`, `name`, `r:id`, `showAsIcon`, `spid`
- Children: `p:extLst`, `p:pic`
- Parents: `p:controls`
- Specification: §19.3.2.1 p.2605, OI 2.1.1131

### p:controls

- Type: `p:CT_ControlList`
- Children: `p:control`
- Parents: `p:cSld`
- Specification: §19.3.1.15 p.2585

### p:cover

- Type: `p:CT_EightDirectionTransition`
- Attributes: `dir`
- Parents: `p:transition`
- Specification: §19.5.32 p.2646

### p:custData

- Type: `p:CT_CustomerData`
- Attributes: `r:id`!
- Parents: `p:custDataLst`
- Specification: §19.3.1.17 p.2586, OI 2.1.1122

### p:custDataLst

- Type: `p:CT_CustomerDataList`
- Children: `p:custData`, `p:tags`
- Parents: `p14:nvPr`, `p:cSld`, `p:nvPr`, `p:presentation`
- Specification: §19.3.1.18 p.2586

### p:custShow

- Type: `p:CT_CustomShow`, `p:CT_CustomShowId`
- Attributes: `id`!, `name`!
- Children: `p:sldLst`, `p:extLst`
- Parents: `p:custShowLst`, `p:htmlPubPr`, `p:showPr`
- Specification: §19.2.1.5 p.2536, OI 2.1.1098; §19.2.1.6 p.2537, OI 2.1.1099

### p:custShowLst

- Type: `p:CT_CustomShowList`
- Children: `p:custShow`
- Parents: `p:presentation`
- Specification: §19.2.1.7 p.2537

### p:cut

- Type: `p:CT_OptionalBlackTransition`
- Attributes: `thruBlk`
- Parents: `p:transition`
- Specification: §19.5.34 p.2650

### p:cxnSp

- Type: `p:CT_Connector`
- Children: `p:nvCxnSpPr`, `p:spPr`, `p:style`, `p:extLst`
- Parents: `p:grpSp`, `p:spTree`
- Specification: §19.3.1.19 p.2586

### p:defaultTextStyle

- Type: `a:CT_TextListStyle`
- Children: `a:defPPr`, `a:lvl1pPr`, `a:lvl2pPr`, `a:lvl3pPr`, `a:lvl4pPr`, `a:lvl5pPr`, `a:lvl6pPr`, `a:lvl7pPr`, `a:lvl8pPr`, `a:lvl9pPr`, `a:extLst`
- Parents: `p:presentation`
- Specification: §19.2.1.8 p.2538

### p:diamond

- Type: `p:CT_Empty`
- Parents: `p:transition`
- Specification: §19.5.35 p.2651

### p:dissolve

- Type: `p:CT_Empty`
- Parents: `p:transition`
- Specification: §19.5.36 p.2652

### p:embed

- Type: `p:CT_OleObjectEmbed`
- Attributes: `followColorScheme`
- Children: `p:extLst`
- Parents: `p:oleObj`
- Specification: §19.3.2.2 p.2605, OI 2.1.1132

### p:embeddedFont

- Type: `p:CT_EmbeddedFontListEntry`
- Children: `p:font`, `p:regular`, `p:bold`, `p:italic`, `p:boldItalic`
- Parents: `p:embeddedFontLst`
- Specification: §19.2.1.9 p.2538

### p:embeddedFontLst

- Type: `p:CT_EmbeddedFontList`
- Children: `p:embeddedFont`
- Parents: `p:presentation`
- Specification: §19.2.1.10 p.2538, OI 2.1.1100

### p:endCondLst

- Type: `p:CT_TLTimeConditionList`
- Children: `p:cond`
- Parents: `p:cTn`
- Specification: §19.5.37 p.2652

### p:endSnd

- Type: `p:CT_Empty`
- Parents: `p:sndAc`
- Specification: §19.5.38 p.2653

### p:endSync

- Type: `p:CT_TLTimeCondition`
- Attributes: `delay`, `evt`
- Children: `p:tgtEl`, `p:tn`, `p:rtn`
- Parents: `p:cTn`
- Specification: §19.5.39 p.2653, OI 2.1.1158

### p:excl

- Type: `p:CT_TLTimeNodeExclusive`
- Children: `p:cTn`
- Parents: `p:childTnLst`, `p:subTnLst`, `p:tnLst`
- Specification: §19.5.40 p.2654, OI 2.1.1159

### p:ext

- Type: `p:CT_Extension`
- Attributes: `uri`!
- Children: `any(##any)`
- Parents: `p14:extLst`, `p1510:extLst`, `p15:extLst`, `p188:extLst`, `p202:extLst`, `p223:extLst`, `p228:extLst`, `p:extLst`, `pc226:extLst`, `pc:extLst`, `psez:extLst`, `pslz:extLst`, `psuz:extLst`
- Specification: §19.2.1.11 p.2538, OI 2.1.1101

### p:extLst

- Type: `p:CT_ExtensionList`, `p:CT_ExtensionListModify`
- Attributes: `mod`
- Children: `p:ext`
- Parents: `p14:nvPr`, `p:bgPr`, `p:cSld`, `p:cm`, `p:cmAuthor`, `p:control`, `p:custShow`, `p:cxnSp`, `p:embed`, `p:graphicFrame`, `p:grpSp`, `p:handoutMaster`, `p:handoutMasterId`, `p:hf`, `p:htmlPubPr`, `p:link`, `p:normalViewPr`, `p:notes`, `p:notesMaster`, `p:notesMasterId`, `p:notesTextViewPr`, `p:notesViewPr`, `p:nvPr`, `p:outlineViewPr`, `p:ph`, `p:photoAlbum`, `p:pic`, `p:presentation`, `p:presentationPr`, `p:prnPr`, `p:showPr`, `p:sld`, `p:sldId`, `p:sldLayout`, `p:sldLayoutId`, `p:sldMaster`, `p:sldMasterId`, `p:sldSyncPr`, `p:slideViewPr`, `p:sorterViewPr`, `p:sp`, `p:spTree`, `p:timing`, `p:transition`, `p:txStyles`, `p:viewPr`, `p:webPr`
- Specification: §19.2.1.12 p.2539; §19.3.1.20 p.2587

### p:fade

- Type: `p:CT_OptionalBlackTransition`
- Attributes: `thruBlk`
- Parents: `p:transition`
- Specification: §19.5.41 p.2654

### p:fltVal

- Type: `p:CT_TLAnimVariantFloatVal`
- Attributes: `val`!
- Parents: `p:progress`, `p:to`, `p:val`
- Specification: §19.5.42 p.2656

### p:font

- Type: `a:CT_TextFont`
- Attributes: `charset`, `panose`, `pitchFamily`, `typeface`!
- Parents: `p:embeddedFont`
- Specification: §19.2.1.13 p.2539, OI 2.1.1102

### p:from

- Type: `a:CT_Color`, `p:CT_TLPoint`
- Attributes: `x`!, `y`!
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `p:animClr`, `p:animMotion`, `p:animScale`
- Specification: §19.5.43 p.2656, OI 2.1.1160; §19.5.44 p.2656, OI 2.1.1161

### p:graphicEl

- Type: `a:CT_AnimationElementChoice`
- Children: `a:dgm`, `a:chart`
- Parents: `p:spTgt`
- Specification: §19.5.45 p.2657, OI 2.1.1162

### p:graphicFrame

- Type: `p:CT_GraphicalObjectFrame`
- Attributes: `bwMode`
- Children: `p:nvGraphicFramePr`, `p:xfrm`, `a:graphic`, `p:extLst`
- Parents: `p:grpSp`, `p:spTree`
- Specification: §19.3.1.21 p.2588

### p:gridSpacing

- Type: `a:CT_PositiveSize2D`
- Attributes: `cx`!, `cy`!
- Parents: `p:viewPr`
- Specification: §19.2.2.3 p.2563

### p:grpSp

- Type: `p:CT_GroupShape`
- Children: `p:nvGrpSpPr`, `p:grpSpPr`, `p:sp`, `p:grpSp`, `p:graphicFrame`, `p:cxnSp`, `p:pic`, `p:contentPart`, `p:extLst`
- Parents: `p:grpSp`, `p:spTree`
- Specification: §19.3.1.22 p.2588

### p:grpSpPr

- Type: `a:CT_GroupShapeProperties`
- Attributes: `bwMode`
- Children: `a:xfrm`, `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`, `a:effectLst`, `a:effectDag`, `a:scene3d`, `a:extLst`
- Parents: `p:grpSp`, `p:spTree`
- Specification: §19.3.1.23 p.2589, OI 2.1.1123

### p:guide

- Type: `p:CT_Guide`
- Attributes: `orient`, `pos`
- Parents: `p:guideLst`
- Specification: §19.2.2.4 p.2564, OI 2.1.1112

### p:guideLst

- Type: `p:CT_GuideList`
- Children: `p:guide`
- Parents: `p:cSldViewPr`
- Specification: §19.2.2.5 p.2564

### p:handoutMaster

- Type: `p:CT_HandoutMaster`
- Children: `p:cSld`, `p:clrMap`, `p:hf`, `p:extLst`
- Specification: §19.3.1.24 p.2590

### p:handoutMasterId

- Type: `p:CT_HandoutMasterIdListEntry`
- Attributes: `r:id`!
- Children: `p:extLst`
- Parents: `p:handoutMasterIdLst`
- Specification: §19.2.1.14 p.2542

### p:handoutMasterIdLst

- Type: `p:CT_HandoutMasterIdList`
- Children: `p:handoutMasterId`
- Parents: `p:presentation`
- Specification: §19.2.1.15 p.2543

### p:hf

- Type: `p:CT_HeaderFooter`
- Attributes: `dt`, `ftr`, `hdr`, `sldNum`
- Children: `p:extLst`
- Parents: `p:handoutMaster`, `p:notesMaster`, `p:sldLayout`, `p:sldMaster`
- Specification: §19.3.1.25 p.2590

### p:hsl

- Type: `p:CT_TLByHslColorTransform`
- Attributes: `h`!, `l`!, `s`!
- Parents: `p:by`
- Specification: §19.5.46 p.2658, OI 2.1.1163

### p:htmlPubPr

- Type: `p:CT_HtmlPublishProperties`
- Attributes: `r:id`!, `showSpeakerNotes`, `target`, `title`
- Children: `p:sldAll`, `p:sldRg`, `p:custShow`, `p:extLst`
- Parents: `p:presentationPr`
- Specification: Part 4 §16.3.1.1 p.246

### p:inkTgt

- Type: `p:CT_TLSubShapeId`
- Attributes: `spid`!
- Parents: `p:tgtEl`
- Specification: §19.5.47 p.2659, OI 2.1.1164

### p:intVal

- Type: `p:CT_TLAnimVariantIntegerVal`
- Attributes: `val`!
- Parents: `p:progress`, `p:to`, `p:val`
- Specification: §19.5.48 p.2659

### p:italic

- Type: `p:CT_EmbeddedFontDataId`
- Attributes: `r:id`!
- Parents: `p:embeddedFont`
- Specification: §19.2.1.16 p.2543

### p:iterate

- Type: `p:CT_TLIterateData`
- Attributes: `backwards`, `type`
- Children: `p:tmAbs`, `p:tmPct`
- Parents: `p:cTn`
- Specification: §19.5.49 p.2660, OI 2.1.1165

### p:kinsoku

- Type: `p:CT_Kinsoku`
- Attributes: `invalEndChars`!, `invalStChars`!, `lang`
- Parents: `p:presentation`
- Specification: §19.2.1.17 p.2544, OI 2.1.1103

### p:kiosk

- Type: `p:CT_ShowInfoKiosk`
- Attributes: `restart`
- Parents: `p:showPr`
- Specification: §19.2.1.18 p.2544

### p:link

- Type: `p:CT_OleObjectLink`
- Attributes: `updateAutomatic`
- Children: `p:extLst`
- Parents: `p:oleObj`
- Specification: §19.3.2.3 p.2606, OI 2.1.1133

### p:modifyVerifier

- Type: `p:CT_ModifyVerifier`
- Attributes: `algIdExt`, `algIdExtSource`, `algorithmName`, `cryptAlgorithmClass`, `cryptAlgorithmSid`, `cryptAlgorithmType`, `cryptProvider`, `cryptProviderType`, `cryptProviderTypeExt`, `cryptProviderTypeExtSource`, `hashData`, `hashValue`, `saltData`, `saltValue`, `spinCount`, `spinValue`
- Parents: `p:presentation`
- Specification: §19.2.1.19 p.2545, OI 2.1.1104

### p:newsflash

- Type: `p:CT_Empty`
- Parents: `p:transition`
- Specification: §19.5.50 p.2661

### p:nextCondLst

- Type: `p:CT_TLTimeConditionList`
- Children: `p:cond`
- Parents: `p:seq`
- Specification: §19.5.51 p.2661, OI 2.1.1166

### p:normalViewPr

- Type: `p:CT_NormalViewProperties`
- Attributes: `horzBarState`, `preferSingleView`, `showOutlineIcons`, `snapVertSplitter`, `vertBarState`
- Children: `p:restoredLeft`, `p:restoredTop`, `p:extLst`
- Parents: `p:viewPr`
- Specification: §19.2.2.6 p.2564, OI 2.1.1113

### p:notes

- Type: `p:CT_NotesSlide`
- Attributes: `showMasterPhAnim`, `showMasterSp`
- Children: `p:cSld`, `p:clrMapOvr`, `p:extLst`
- Specification: §19.3.1.26 p.2591

### p:notesMaster

- Type: `p:CT_NotesMaster`
- Children: `p:cSld`, `p:clrMap`, `p:hf`, `p:notesStyle`, `p:extLst`
- Specification: §19.3.1.27 p.2591, OI 2.1.1124

### p:notesMasterId

- Type: `p:CT_NotesMasterIdListEntry`
- Attributes: `r:id`!
- Children: `p:extLst`
- Parents: `p:notesMasterIdLst`
- Specification: §19.2.1.20 p.2549

### p:notesMasterIdLst

- Type: `p:CT_NotesMasterIdList`
- Children: `p:notesMasterId`
- Parents: `p:presentation`
- Specification: §19.2.1.21 p.2549

### p:notesStyle

- Type: `a:CT_TextListStyle`
- Children: `a:defPPr`, `a:lvl1pPr`, `a:lvl2pPr`, `a:lvl3pPr`, `a:lvl4pPr`, `a:lvl5pPr`, `a:lvl6pPr`, `a:lvl7pPr`, `a:lvl8pPr`, `a:lvl9pPr`, `a:extLst`
- Parents: `p:notesMaster`
- Specification: §19.3.1.28 p.2592, OI 2.1.1125

### p:notesSz

- Type: `a:CT_PositiveSize2D`
- Attributes: `cx`!, `cy`!
- Parents: `p:presentation`
- Specification: §19.2.1.22 p.2549, OI 2.1.1105

### p:notesTextViewPr

- Type: `p:CT_NotesTextViewProperties`
- Children: `p:cViewPr`, `p:extLst`
- Parents: `p:viewPr`
- Specification: §19.2.2.7 p.2566

### p:notesViewPr

- Type: `p:CT_NotesViewProperties`
- Children: `p:cSldViewPr`, `p:extLst`
- Parents: `p:viewPr`
- Specification: §19.2.2.8 p.2566

### p:nvCxnSpPr

- Type: `p:CT_ConnectorNonVisual`
- Children: `p:cNvPr`, `p:cNvCxnSpPr`, `p:nvPr`
- Parents: `p:cxnSp`
- Specification: §19.3.1.29 p.2592

### p:nvGraphicFramePr

- Type: `p:CT_GraphicalObjectFrameNonVisual`
- Children: `p:cNvPr`, `p:cNvGraphicFramePr`, `p:nvPr`
- Parents: `p:graphicFrame`
- Specification: §19.3.1.30 p.2592

### p:nvGrpSpPr

- Type: `p:CT_GroupShapeNonVisual`
- Children: `p:cNvPr`, `p:cNvGrpSpPr`, `p:nvPr`
- Parents: `p:grpSp`, `p:spTree`
- Specification: §19.3.1.31 p.2592

### p:nvPicPr

- Type: `p:CT_PictureNonVisual`
- Children: `p:cNvPr`, `p:cNvPicPr`, `p:nvPr`
- Parents: `p:pic`
- Specification: §19.3.1.32 p.2592

### p:nvPr

- Type: `p:CT_ApplicationNonVisualDrawingProps`
- Attributes: `isPhoto`, `userDrawn`
- Children: `p:ph`, `a:audioCd`, `a:wavAudioFile`, `a:audioFile`, `a:videoFile`, `a:quickTimeFile`, `p:custDataLst`, `p:extLst`
- Parents: `p:nvCxnSpPr`, `p:nvGraphicFramePr`, `p:nvGrpSpPr`, `p:nvPicPr`, `p:nvSpPr`
- Specification: §19.3.1.33 p.2593, OI 2.1.1126

### p:nvSpPr

- Type: `p:CT_ShapeNonVisual`
- Children: `p:cNvPr`, `p:cNvSpPr`, `p:nvPr`
- Parents: `p:sp`
- Specification: §19.3.1.34 p.2593

### p:oleChartEl

- Type: `p:CT_TLOleChartTargetElement`
- Attributes: `lvl`, `type`!
- Parents: `p:spTgt`
- Specification: §19.5.52 p.2662, OI 2.1.1167

### p:oleObj

- Type: `p:CT_OleObject`
- Attributes: `imgH`, `imgW`, `name`, `progId`, `r:id`, `showAsIcon`, `spid`
- Children: `p:embed`, `p:link`, `p:pic`
- Specification: §19.3.2.4 p.2606, OI 2.1.1134

### p:origin

- Type: `a:CT_Point2D`
- Attributes: `x`!, `y`!
- Parents: `p:cViewPr`
- Specification: §19.2.2.9 p.2566, OI 2.1.1114

### p:otherStyle

- Type: `a:CT_TextListStyle`
- Children: `a:defPPr`, `a:lvl1pPr`, `a:lvl2pPr`, `a:lvl3pPr`, `a:lvl4pPr`, `a:lvl5pPr`, `a:lvl6pPr`, `a:lvl7pPr`, `a:lvl8pPr`, `a:lvl9pPr`, `a:extLst`
- Parents: `p:txStyles`
- Specification: §19.3.1.35 p.2593

### p:outlineViewPr

- Type: `p:CT_OutlineViewProperties`
- Children: `p:cViewPr`, `p:sldLst`, `p:extLst`
- Parents: `p:viewPr`
- Specification: §19.2.2.10 p.2567

### p:pRg

- Type: `p:CT_IndexRange`
- Attributes: `end`!, `st`!
- Parents: `p:txEl`
- Specification: §19.5.56 p.2664, OI 2.1.1169

### p:par

- Type: `p:CT_TLTimeNodeParallel`
- Children: `p:cTn`
- Parents: `p:childTnLst`, `p:subTnLst`, `p:tnLst`
- Specification: §19.5.53 p.2662

### p:penClr

- Type: `a:CT_Color`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `p:showPr`
- Specification: §19.2.1.23 p.2550

### p:ph

- Type: `p:CT_Placeholder`
- Attributes: `hasCustomPrompt`, `idx`, `orient`, `sz`, `type`
- Children: `p:extLst`
- Parents: `p14:nvPr`, `p:nvPr`
- Specification: §19.3.1.36 p.2594, OI 2.1.1127

### p:photoAlbum

- Type: `p:CT_PhotoAlbum`
- Attributes: `bw`, `frame`, `layout`, `showCaptions`
- Children: `p:extLst`
- Parents: `p:presentation`
- Specification: §19.2.1.24 p.2551, OI 2.1.1106

### p:pic

- Type: `p:CT_Picture`
- Children: `p:nvPicPr`, `p:blipFill`, `p:spPr`, `p:style`, `p:extLst`
- Parents: `p:control`, `p:grpSp`, `p:oleObj`, `p:spTree`
- Specification: §19.3.1.37 p.2594

### p:plus

- Type: `p:CT_Empty`
- Parents: `p:transition`
- Specification: §19.5.54 p.2663

### p:pos

- Type: `a:CT_Point2D`
- Attributes: `x`!, `y`!
- Parents: `p:cm`
- Specification: §19.4.5 p.2611, OI 2.1.1137

### p:present

- Type: `p:CT_Empty`
- Parents: `p:showPr`
- Specification: §19.2.1.25 p.2552, OI 2.1.1107

### p:presentation

- Type: `p:CT_Presentation`
- Attributes: `autoCompressPictures`, `bookmarkIdSeed`, `compatMode`, `conformance`, `embedTrueTypeFonts`, `firstSlideNum`, `removePersonalInfoOnSave`, `rtl`, `saveSubsetFonts`, `serverZoom`, `showSpecialPlsOnTitleSld`, `strictFirstAndLastChars`
- Children: `p:sldMasterIdLst`, `p:notesMasterIdLst`, `p:handoutMasterIdLst`, `p:sldIdLst`, `p:sldSz`, `p:notesSz`, `p:smartTags`, `p:embeddedFontLst`, `p:custShowLst`, `p:photoAlbum`, `p:custDataLst`, `p:kinsoku`, `p:defaultTextStyle`, `p:modifyVerifier`, `p:extLst`
- Specification: §19.2.1.26 p.2552, OI 2.1.1108

### p:presentationPr

- Type: `p:CT_PresentationProperties`
- Children: `p:htmlPubPr`, `p:webPr`, `p:prnPr`, `p:showPr`, `p:clrMru`, `p:extLst`
- Specification: §19.2.1.27 p.2554

### p:prevCondLst

- Type: `p:CT_TLTimeConditionList`
- Children: `p:cond`
- Parents: `p:seq`
- Specification: §19.5.55 p.2664, OI 2.1.1168

### p:prnPr

- Type: `p:CT_PrintProperties`
- Attributes: `clrMode`, `frameSlides`, `hiddenSlides`, `prnWhat`, `scaleToFitPaper`
- Children: `p:extLst`
- Parents: `p:presentationPr`
- Specification: §19.2.1.28 p.2554

### p:progress

- Type: `p:CT_TLAnimVariant`
- Children: `p:boolVal`, `p:intVal`, `p:fltVal`, `p:strVal`, `p:clrVal`
- Parents: `p:animEffect`
- Specification: §19.5.57 p.2665, OI 2.1.1170

### p:pull

- Type: `p:CT_EightDirectionTransition`
- Attributes: `dir`
- Parents: `p:transition`
- Specification: §19.5.58 p.2666

### p:push

- Type: `p:CT_SideDirectionTransition`
- Attributes: `dir`
- Parents: `p:transition`
- Specification: §19.5.59 p.2668

### p:rCtr

- Type: `p:CT_TLPoint`
- Attributes: `x`!, `y`!
- Parents: `p:animMotion`
- Specification: §19.5.62 p.2670, OI 2.1.1171

### p:random

- Type: `p:CT_Empty`
- Parents: `p:transition`
- Specification: §19.5.60 p.2669

### p:randomBar

- Type: `p:CT_OrientationTransition`
- Attributes: `dir`
- Parents: `p:transition`
- Specification: §19.5.61 p.2669

### p:regular

- Type: `p:CT_EmbeddedFontDataId`
- Attributes: `r:id`!
- Parents: `p:embeddedFont`
- Specification: §19.2.1.29 p.2555

### p:restoredLeft

- Type: `p:CT_NormalViewPortion`
- Attributes: `autoAdjust`, `sz`!
- Parents: `p:normalViewPr`
- Specification: §19.2.2.11 p.2567, OI 2.1.1115

### p:restoredTop

- Type: `p:CT_NormalViewPortion`
- Attributes: `autoAdjust`, `sz`!
- Parents: `p:normalViewPr`
- Specification: §19.2.2.12 p.2567, OI 2.1.1116

### p:rgb

- Type: `p:CT_TLByRgbColorTransform`
- Attributes: `b`!, `g`!, `r`!
- Parents: `p:by`
- Specification: §19.5.63 p.2671, OI 2.1.1172

### p:rtn

- Type: `p:CT_TLTriggerRuntimeNode`
- Attributes: `val`!
- Parents: `p:cond`, `p:endSync`
- Specification: §19.5.64 p.2672

### p:scale

- Type: `a:CT_Scale2D`
- Children: `a:sx`, `a:sy`
- Parents: `p:cViewPr`
- Specification: §19.2.2.13 p.2568, OI 2.1.1117

### p:seq

- Type: `p:CT_TLTimeNodeSequence`
- Attributes: `concurrent`, `nextAc`, `prevAc`
- Children: `p:cTn`, `p:prevCondLst`, `p:nextCondLst`
- Parents: `p:childTnLst`, `p:subTnLst`, `p:tnLst`
- Specification: §19.5.65 p.2673, OI 2.1.1173

### p:set

- Type: `p:CT_TLSetBehavior`
- Children: `p:cBhvr`, `p:to`
- Parents: `p:childTnLst`, `p:subTnLst`, `p:tnLst`
- Specification: §19.5.66 p.2674, OI 2.1.1174

### p:showPr

- Type: `p:CT_ShowProperties`
- Attributes: `loop`, `showAnimation`, `showNarration`, `useTimings`
- Children: `p:present`, `p:browse`, `p:kiosk`, `p:sldAll`, `p:sldRg`, `p:custShow`, `p:penClr`, `p:extLst`
- Parents: `p:presentationPr`
- Specification: §19.2.1.30 p.2556

### p:sld

- Type: `p:CT_Slide`, `p:CT_SlideRelationshipListEntry`, `p:CT_OutlineViewSlideEntry`
- Attributes: `collapse`, `r:id`!, `show`, `showMasterPhAnim`, `showMasterSp`
- Children: `p:cSld`, `p:clrMapOvr`, `p:transition`, `p:timing`, `p:extLst`
- Parents: `p:sldLst`
- Specification: §19.2.1.31 p.2556; §19.2.2.14 p.2568; §19.3.1.38 p.2595, OI 2.1.1128

### p:sldAll

- Type: `p:CT_Empty`
- Parents: `p:htmlPubPr`, `p:showPr`
- Specification: §19.2.1.32 p.2557

### p:sldId

- Type: `p:CT_SlideIdListEntry`
- Attributes: `id`!, `r:id`!
- Children: `p:extLst`
- Parents: `p:sldIdLst`
- Specification: §19.2.1.33 p.2557

### p:sldIdLst

- Type: `p:CT_SlideIdList`
- Children: `p:sldId`
- Parents: `p:presentation`
- Specification: §19.2.1.34 p.2558

### p:sldLayout

- Type: `p:CT_SlideLayout`
- Attributes: `matchingName`, `preserve`, `showMasterPhAnim`, `showMasterSp`, `type`, `userDrawn`
- Children: `p:cSld`, `p:clrMapOvr`, `p:transition`, `p:timing`, `p:hf`, `p:extLst`
- Specification: §19.3.1.39 p.2596

### p:sldLayoutId

- Type: `p:CT_SlideLayoutIdListEntry`
- Attributes: `id`, `r:id`!
- Children: `p:extLst`
- Parents: `p:sldLayoutIdLst`
- Specification: §19.3.1.40 p.2597

### p:sldLayoutIdLst

- Type: `p:CT_SlideLayoutIdList`
- Children: `p:sldLayoutId`
- Parents: `p:sldMaster`
- Specification: §19.3.1.41 p.2597

### p:sldLst

- Type: `p:CT_SlideRelationshipList`, `p:CT_OutlineViewSlideList`
- Children: `p:sld`
- Parents: `p:custShow`, `p:outlineViewPr`
- Specification: §19.2.1.35 p.2558; §19.2.2.15 p.2569

### p:sldMaster

- Type: `p:CT_SlideMaster`
- Attributes: `preserve`
- Children: `p:cSld`, `p:clrMap`, `p:sldLayoutIdLst`, `p:transition`, `p:timing`, `p:hf`, `p:txStyles`, `p:extLst`
- Specification: §19.3.1.42 p.2597

### p:sldMasterId

- Type: `p:CT_SlideMasterIdListEntry`
- Attributes: `id`, `r:id`!
- Children: `p:extLst`
- Parents: `p:sldMasterIdLst`
- Specification: §19.2.1.36 p.2558

### p:sldMasterIdLst

- Type: `p:CT_SlideMasterIdList`
- Children: `p:sldMasterId`
- Parents: `p:presentation`
- Specification: §19.2.1.37 p.2559

### p:sldRg

- Type: `p:CT_IndexRange`
- Attributes: `end`!, `st`!
- Parents: `p:htmlPubPr`, `p:showPr`
- Specification: §19.2.1.38 p.2559, OI 2.1.1109

### p:sldSyncPr

- Type: `p:CT_SlideSyncProperties`
- Attributes: `clientInsertedTime`!, `serverSldId`!, `serverSldModifiedTime`!
- Children: `p:extLst`
- Specification: §19.6.1 p.2701

### p:sldSz

- Type: `p:CT_SlideSize`
- Attributes: `cx`!, `cy`!, `type`
- Parents: `p:presentation`
- Specification: §19.2.1.39 p.2560, OI 2.1.1110

### p:sldTgt

- Type: `p:CT_Empty`
- Parents: `p:tgtEl`
- Specification: §19.5.67 p.2675, OI 2.1.1175

### p:slideViewPr

- Type: `p:CT_SlideViewProperties`
- Children: `p:cSldViewPr`, `p:extLst`
- Parents: `p:viewPr`
- Specification: §19.2.2.16 p.2569

### p:smartTags

- Type: `p:CT_SmartTags`
- Attributes: `r:id`!
- Parents: `p:presentation`
- Specification: §19.2.1.40 p.2561, OI 2.1.1111

### p:snd

- Type: `a:CT_EmbeddedWAVAudioFile`
- Attributes: `name`, `r:embed`!
- Parents: `p:stSnd`
- Specification: §19.5.68 p.2676

### p:sndAc

- Type: `p:CT_TransitionSoundAction`
- Children: `p:stSnd`, `p:endSnd`
- Parents: `p:transition`
- Specification: §19.5.69 p.2676

### p:sndTgt

- Type: `a:CT_EmbeddedWAVAudioFile`
- Attributes: `name`, `r:embed`!
- Parents: `p:tgtEl`
- Specification: §19.5.70 p.2677

### p:sorterViewPr

- Type: `p:CT_SlideSorterViewProperties`
- Attributes: `showFormatting`
- Children: `p:cViewPr`, `p:extLst`
- Parents: `p:viewPr`
- Specification: §19.2.2.17 p.2569

### p:sp

- Type: `p:CT_Shape`
- Attributes: `useBgFill`
- Children: `p:nvSpPr`, `p:spPr`, `p:style`, `p:txBody`, `p:extLst`
- Parents: `p:grpSp`, `p:spTree`
- Specification: §19.3.1.43 p.2598

### p:spPr

- Type: `a:CT_ShapeProperties`
- Attributes: `bwMode`
- Children: `a:xfrm`, `a:custGeom`, `a:prstGeom`, `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`, `a:ln`, `a:effectLst`, `a:effectDag`, `a:scene3d`, `a:sp3d`, `a:extLst`
- Parents: `p:cxnSp`, `p:pic`, `p:sp`
- Specification: §19.3.1.44 p.2598, OI 2.1.1129

### p:spTgt

- Type: `p:CT_TLShapeTargetElement`
- Attributes: `spid`!
- Children: `p:bg`, `p:subSp`, `p:oleChartEl`, `p:txEl`, `p:graphicEl`
- Parents: `p:tgtEl`
- Specification: §19.5.72 p.2679, OI 2.1.1176

### p:spTree

- Type: `p:CT_GroupShape`
- Children: `p:nvGrpSpPr`, `p:grpSpPr`, `p:sp`, `p:grpSp`, `p:graphicFrame`, `p:cxnSp`, `p:pic`, `p:contentPart`, `p:extLst`
- Parents: `p:cSld`
- Specification: §19.3.1.45 p.2599

### p:split

- Type: `p:CT_SplitTransition`
- Attributes: `dir`, `orient`
- Parents: `p:transition`
- Specification: §19.5.71 p.2678

### p:stCondLst

- Type: `p:CT_TLTimeConditionList`
- Children: `p:cond`
- Parents: `p:cTn`
- Specification: §19.5.73 p.2680

### p:stSnd

- Type: `p:CT_TransitionStartSoundAction`
- Attributes: `loop`
- Children: `p:snd`
- Parents: `p:sndAc`
- Specification: §19.5.76 p.2682

### p:strVal

- Type: `p:CT_TLAnimVariantStringVal`
- Attributes: `val`!
- Parents: `p:progress`, `p:to`, `p:val`
- Specification: §19.5.75 p.2682

### p:strips

- Type: `p:CT_CornerDirectionTransition`
- Attributes: `dir`
- Parents: `p:transition`
- Specification: §19.5.74 p.2680

### p:style

- Type: `a:CT_ShapeStyle`
- Children: `a:lnRef`, `a:fillRef`, `a:effectRef`, `a:fontRef`
- Parents: `p:cxnSp`, `p:pic`, `p:sp`
- Specification: §19.3.1.46 p.2600

### p:subSp

- Type: `p:CT_TLSubShapeId`
- Attributes: `spid`!
- Parents: `p:spTgt`
- Specification: §19.5.77 p.2682, OI 2.1.1177

### p:subTnLst

- Type: `p:CT_TimeNodeList`
- Children: `p:par`, `p:seq`, `p:excl`, `p:anim`, `p:animClr`, `p:animEffect`, `p:animMotion`, `p:animRot`, `p:animScale`, `p:cmd`, `p:set`, `p:audio`, `p:video`
- Parents: `p:cTn`
- Specification: §19.5.78 p.2683, OI 2.1.1178

### p:tag

- Type: `p:CT_StringTag`
- Attributes: `name`!, `val`!
- Parents: `p:tagLst`
- Specification: §19.3.3.1 p.2607, OI 2.1.1135

### p:tagLst

- Type: `p:CT_TagList`
- Children: `p:tag`
- Specification: §19.3.3.2 p.2607

### p:tags

- Type: `p:CT_TagsData`
- Attributes: `r:id`!
- Parents: `p:custDataLst`
- Specification: §19.3.1.47 p.2601

### p:tav

- Type: `p:CT_TLTimeAnimateValue`
- Attributes: `fmla`, `tm`
- Children: `p:val`
- Parents: `p:tavLst`
- Specification: §19.5.79 p.2684

### p:tavLst

- Type: `p:CT_TLTimeAnimateValueList`
- Children: `p:tav`
- Parents: `p:anim`
- Specification: §19.5.80 p.2688

### p:text

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `p:cm`
- Specification: §19.4.6 p.2612

### p:tgtEl

- Type: `p:CT_TLTimeTargetElement`
- Children: `p:sldTgt`, `p:sndTgt`, `p:spTgt`, `p:inkTgt`
- Parents: `p:cBhvr`, `p:cMediaNode`, `p:cond`, `p:endSync`
- Specification: §19.5.81 p.2689

### p:timing

- Type: `p:CT_SlideTiming`
- Children: `p:tnLst`, `p:bldLst`, `p:extLst`
- Parents: `p:sld`, `p:sldLayout`, `p:sldMaster`
- Specification: §19.3.1.48 p.2601

### p:titleStyle

- Type: `a:CT_TextListStyle`
- Children: `a:defPPr`, `a:lvl1pPr`, `a:lvl2pPr`, `a:lvl3pPr`, `a:lvl4pPr`, `a:lvl5pPr`, `a:lvl6pPr`, `a:lvl7pPr`, `a:lvl8pPr`, `a:lvl9pPr`, `a:extLst`
- Parents: `p:txStyles`
- Specification: §19.3.1.49 p.2602

### p:tmAbs

- Type: `p:CT_TLIterateIntervalTime`
- Attributes: `val`!
- Parents: `p:iterate`
- Specification: §19.5.82 p.2689

### p:tmPct

- Type: `p:CT_TLIterateIntervalPercentage`
- Attributes: `val`!
- Parents: `p:iterate`
- Specification: §19.5.83 p.2690

### p:tmpl

- Type: `p:CT_TLTemplate`
- Attributes: `lvl`
- Children: `p:tnLst`
- Parents: `p:tmplLst`
- Specification: §19.5.84 p.2691, OI 2.1.1179

### p:tmplLst

- Type: `p:CT_TLTemplateList`
- Children: `p:tmpl`
- Parents: `p:bldP`
- Specification: §19.5.85 p.2691, OI 2.1.1180

### p:tn

- Type: `p:CT_TLTriggerTimeNodeID`
- Attributes: `val`!
- Parents: `p:cond`, `p:endSync`
- Specification: §19.5.86 p.2692

### p:tnLst

- Type: `p:CT_TimeNodeList`
- Children: `p:par`, `p:seq`, `p:excl`, `p:anim`, `p:animClr`, `p:animEffect`, `p:animMotion`, `p:animRot`, `p:animScale`, `p:cmd`, `p:set`, `p:audio`, `p:video`
- Parents: `p:timing`, `p:tmpl`
- Specification: §19.5.87 p.2693, OI 2.1.1181

### p:to

- Type: `a:CT_Color`, `p:CT_TLPoint`, `p:CT_TLAnimVariant`
- Attributes: `x`!, `y`!
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`, `p:boolVal`, `p:intVal`, `p:fltVal`, `p:strVal`, `p:clrVal`
- Parents: `p:animClr`, `p:animMotion`, `p:animScale`, `p:set`
- Specification: §19.5.88 p.2693, OI 2.1.1182; §19.5.89 p.2694, OI 2.1.1183; §19.5.90 p.2694, OI 2.1.1184

### p:transition

- Type: `p:CT_SlideTransition`
- Attributes: `advClick`, `advTm`, `spd`
- Children: `p:blinds`, `p:checker`, `p:circle`, `p:dissolve`, `p:comb`, `p:cover`, `p:cut`, `p:diamond`, `p:fade`, `p:newsflash`, `p:plus`, `p:pull`, `p:push`, `p:random`, `p:randomBar`, `p:split`, `p:strips`, `p:wedge`, `p:wheel`, `p:wipe`, `p:zoom`, `p:sndAc`, `p:extLst`
- Parents: `p:sld`, `p:sldLayout`, `p:sldMaster`
- Specification: §19.3.1.50 p.2602

### p:txBody

- Type: `a:CT_TextBody`
- Children: `a:bodyPr`, `a:lstStyle`, `a:p`
- Parents: `p:sp`
- Specification: §19.3.1.51 p.2603

### p:txEl

- Type: `p:CT_TLTextTargetElement`
- Children: `p:charRg`, `p:pRg`
- Parents: `p:spTgt`
- Specification: §19.5.91 p.2695

### p:txStyles

- Type: `p:CT_SlideMasterTextStyles`
- Children: `p:titleStyle`, `p:bodyStyle`, `p:otherStyle`, `p:extLst`
- Parents: `p:sldMaster`
- Specification: §19.3.1.52 p.2603

### p:val

- Type: `p:CT_TLAnimVariant`
- Children: `p:boolVal`, `p:intVal`, `p:fltVal`, `p:strVal`, `p:clrVal`
- Parents: `p:tav`
- Specification: §19.5.92 p.2695

### p:video

- Type: `p:CT_TLMediaNodeVideo`
- Attributes: `fullScrn`
- Children: `p:cMediaNode`
- Parents: `p:childTnLst`, `p:subTnLst`, `p:tnLst`
- Specification: §19.5.93 p.2696, OI 2.1.1185

### p:viewPr

- Type: `p:CT_ViewProperties`
- Attributes: `lastView`, `showComments`
- Children: `p:normalViewPr`, `p:slideViewPr`, `p:outlineViewPr`, `p:notesTextViewPr`, `p:sorterViewPr`, `p:notesViewPr`, `p:gridSpacing`, `p:extLst`
- Specification: §19.2.2.18 p.2570

### p:webPr

- Type: `p:CT_WebProperties`
- Attributes: `allowPng`, `clr`, `encoding`, `imgSz`, `organizeInFolders`, `relyOnVml`, `resizeGraphics`, `showAnimation`, `useLongFilenames`
- Children: `p:extLst`
- Parents: `p:presentationPr`
- Specification: Part 4 §16.3.1.2 p.247

### p:wedge

- Type: `p:CT_Empty`
- Parents: `p:transition`
- Specification: §19.5.94 p.2697

### p:wheel

- Type: `p:CT_WheelTransition`
- Attributes: `spokes`
- Parents: `p:transition`
- Specification: §19.5.95 p.2697, OI 2.1.1186

### p:wipe

- Type: `p:CT_SideDirectionTransition`
- Attributes: `dir`
- Parents: `p:transition`
- Specification: §19.5.96 p.2699

### p:xfrm

- Type: `a:CT_Transform2D`
- Attributes: `flipH`, `flipV`, `rot`
- Children: `a:off`, `a:ext`
- Parents: `p:graphicFrame`
- Specification: §19.3.1.53 p.2603, OI 2.1.1130

### p:zoom

- Type: `p:CT_InOutTransition`
- Attributes: `dir`
- Parents: `p:transition`
- Specification: §19.5.97 p.2700

`!` marks required attributes.
