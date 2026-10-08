# a: DrawingML

`http://schemas.openxmlformats.org/drawingml/2006/main`

- Source: ECMA-376-4 transitional schema dml-main.xsd
- Office version: 2007
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 321 | 663 | 0 | 231 | 94 | 893 | 426 | 199 |

Full detail (content models, attribute types, defaults): [a.json](a.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`a:accent1`](#aaccent1) | `a:CT_Color` | 0 | 6 | 1 | §20.1.4.1.1 p.2793, OI 2.1.1248 |
| [`a:accent2`](#aaccent2) | `a:CT_Color` | 0 | 6 | 1 | §20.1.4.1.2 p.2794, OI 2.1.1249 |
| [`a:accent3`](#aaccent3) | `a:CT_Color` | 0 | 6 | 1 | §20.1.4.1.3 p.2795, OI 2.1.1250 |
| [`a:accent4`](#aaccent4) | `a:CT_Color` | 0 | 6 | 1 | §20.1.4.1.4 p.2795, OI 2.1.1251 |
| [`a:accent5`](#aaccent5) | `a:CT_Color` | 0 | 6 | 1 | §20.1.4.1.5 p.2796, OI 2.1.1252 |
| [`a:accent6`](#aaccent6) | `a:CT_Color` | 0 | 6 | 1 | §20.1.4.1.6 p.2796, OI 2.1.1253 |
| [`a:ahLst`](#aahlst) | `a:CT_AdjustHandleList` | 0 | 2 | 1 | §20.1.9.1 p.2897 |
| [`a:ahPolar`](#aahpolar) | `a:CT_PolarAdjustHandle` | 6 | 1 | 1 | §20.1.9.2 p.2897, OI 2.1.1309 |
| [`a:ahXY`](#aahxy) | `a:CT_XYAdjustHandle` | 6 | 1 | 1 | §20.1.9.3 p.2898, OI 2.1.1310 |
| [`a:alpha`](#aalpha) | `a:CT_PositiveFixedPercentage` | 1 | 0 | 8 | §20.1.2.3.1 p.2766, OI 2.1.1218 |
| [`a:alphaBiLevel`](#aalphabilevel) | `a:CT_AlphaBiLevelEffect` | 1 | 0 | 4 | §20.1.8.1 p.2862 |
| [`a:alphaCeiling`](#aalphaceiling) | `a:CT_AlphaCeilingEffect` | 0 | 0 | 4 | §20.1.8.2 p.2862 |
| [`a:alphaFloor`](#aalphafloor) | `a:CT_AlphaFloorEffect` | 0 | 0 | 4 | §20.1.8.3 p.2862 |
| [`a:alphaInv`](#aalphainv) | `a:CT_AlphaInverseEffect` | 0 | 6 | 4 | §20.1.8.4 p.2863, OI 2.1.1285 |
| [`a:alphaMod`](#aalphamod) | `a:CT_AlphaModulateEffect`, `a:CT_PositivePercentage` | 1 | 1 | 12 | §20.1.2.3.2 p.2767, OI 2.1.1219; §20.1.8.5 p.2863, OI 2.1.1286 |
| [`a:alphaModFix`](#aalphamodfix) | `a:CT_AlphaModulateFixedEffect` | 1 | 0 | 4 | §20.1.8.6 p.2863, OI 2.1.1287 |
| [`a:alphaOff`](#aalphaoff) | `a:CT_FixedPercentage` | 1 | 0 | 8 | §20.1.2.3.3 p.2768, OI 2.1.1220 |
| [`a:alphaOutset`](#aalphaoutset) | `a:CT_AlphaOutsetEffect` | 1 | 0 | 2 | §20.1.8.7 p.2863 |
| [`a:alphaRepl`](#aalpharepl) | `a:CT_AlphaReplaceEffect` | 1 | 0 | 4 | §20.1.8.8 p.2864 |
| [`a:anchor`](#aanchor) | `a:CT_Point3D` | 3 | 0 | 1 | §20.1.5.1 p.2833, OI 2.1.1266 |
| [`a:arcTo`](#aarcto) | `a:CT_Path2DArcTo` | 4 | 0 | 1 | §20.1.9.4 p.2899 |
| [`a:audioCd`](#aaudiocd) | `a:CT_AudioCD` | 0 | 3 | 2 | §20.1.3.1 p.2786 |
| [`a:audioFile`](#aaudiofile) | `a:CT_AudioFile` | 2 | 1 | 2 | §20.1.3.2 p.2787, OI 2.1.1244 |
| [`a:avLst`](#aavlst) | `a:CT_GeomGuideList` | 0 | 1 | 3 | §20.1.9.5 p.2901 |
| [`a:backdrop`](#abackdrop) | `a:CT_Backdrop` | 0 | 4 | 3 | §20.1.5.2 p.2834, OI 2.1.1267 |
| [`a:band1H`](#aband1h) | `a:CT_TablePartStyle` | 0 | 2 | 2 | §20.1.4.2.1 p.2812 |
| [`a:band1V`](#aband1v) | `a:CT_TablePartStyle` | 0 | 2 | 2 | §20.1.4.2.2 p.2812, OI 2.1.1261 |
| [`a:band2H`](#aband2h) | `a:CT_TablePartStyle` | 0 | 2 | 2 | §20.1.4.2.3 p.2813 |
| [`a:band2V`](#aband2v) | `a:CT_TablePartStyle` | 0 | 2 | 2 | §20.1.4.2.4 p.2814, OI 2.1.1262 |
| [`a:bevel`](#abevel) | `a:CT_LineJoinBevel`, `a:CT_Bevel` | 3 | 0 | 10 | §20.1.4.2.5 p.2815; §20.1.8.9 p.2864 |
| [`a:bevelB`](#abevelb) | `a:CT_Bevel` | 3 | 0 | 3 | §20.1.5.3 p.2835, OI 2.1.1268 |
| [`a:bevelT`](#abevelt) | `a:CT_Bevel` | 3 | 0 | 3 | §20.1.5.4 p.2836, OI 2.1.1269 |
| [`a:bgClr`](#abgclr) | `a:CT_Color` | 0 | 6 | 1 | §20.1.8.10 p.2864 |
| [`a:bgFillStyleLst`](#abgfillstylelst) | `a:CT_BackgroundFillStyleList` | 0 | 6 | 1 | §20.1.4.1.7 p.2797 |
| [`a:biLevel`](#abilevel) | `a:CT_BiLevelEffect` | 1 | 0 | 4 | §20.1.8.11 p.2864 |
| [`a:bldChart`](#abldchart) | `a:CT_AnimationChartBuildProperties` | 2 | 0 | 1 | §20.1.2.2.1 p.2738, OI 2.1.1198 |
| [`a:bldDgm`](#ablddgm) | `a:CT_AnimationDgmBuildProperties` | 2 | 0 | 1 | §20.1.2.2.2 p.2739, OI 2.1.1199 |
| [`a:blend`](#ablend) | `a:CT_BlendEffect` | 1 | 1 | 2 | §20.1.8.12 p.2865 |
| [`a:blip`](#ablip) ◆ | `a:CT_Blip` | 3 | 18 | 7 | §20.1.8.13 p.2865 |
| [`a:blipFill`](#ablipfill) | `a:CT_BlipFillProperties` | 2 | 4 | 42 | §20.1.8.14 p.2866, OI 2.1.1288 |
| [`a:blue`](#ablue) | `a:CT_Percentage` | 1 | 0 | 8 | §20.1.2.3.4 p.2768, OI 2.1.1221 |
| [`a:blueMod`](#abluemod) | `a:CT_Percentage` | 1 | 0 | 8 | §20.1.2.3.5 p.2769, OI 2.1.1222 |
| [`a:blueOff`](#ablueoff) | `a:CT_Percentage` | 1 | 0 | 8 | §20.1.2.3.6 p.2769, OI 2.1.1223 |
| [`a:blur`](#ablur) | `a:CT_BlurEffect` | 2 | 0 | 5 | §20.1.8.15 p.2868, OI 2.1.1289 |
| [`a:bodyPr`](#abodypr) | `a:CT_TextBodyProperties` | 19 | 8 | 14 | §21.1.2.1.1 p.3197, OI 2.1.1379 |
| [`a:bottom`](#abottom) | `a:CT_ThemeableLineStyle` | 0 | 2 | 1 | §20.1.4.2.6 p.2816 |
| [`a:br`](#abr) | `a:CT_TextLineBreak` | 0 | 1 | 1 | §21.1.2.2.1 p.3209 |
| [`a:buAutoNum`](#abuautonum) | `a:CT_TextAutonumberBullet` | 2 | 0 | 12 | §21.1.2.4.1 p.3268 |
| [`a:buBlip`](#abublip) | `a:CT_TextBlipBullet` | 0 | 1 | 12 | §21.1.2.4.2 p.3269 |
| [`a:buChar`](#abuchar) | `a:CT_TextCharBullet` | 1 | 0 | 12 | §21.1.2.4.3 p.3271 |
| [`a:buClr`](#abuclr) | `a:CT_Color` | 0 | 6 | 11 | §21.1.2.4.4 p.3272 |
| [`a:buClrTx`](#abuclrtx) | `a:CT_TextBulletColorFollowText` | 0 | 0 | 11 | §21.1.2.4.5 p.3273 |
| [`a:buFont`](#abufont) | `a:CT_TextFont` | 4 | 0 | 11 | §21.1.2.4.6 p.3274, OI 2.1.1403 |
| [`a:buFontTx`](#abufonttx) | `a:CT_TextBulletTypefaceFollowText` | 0 | 0 | 11 | §21.1.2.4.7 p.3276 |
| [`a:buNone`](#abunone) | `a:CT_TextNoBullet` | 0 | 0 | 12 | §21.1.2.4.8 p.3276 |
| [`a:buSzPct`](#abuszpct) | `a:CT_TextBulletSizePercent` | 1 | 0 | 11 | §21.1.2.4.9 p.3277, OI 2.1.1404 |
| [`a:buSzPts`](#abuszpts) | `a:CT_TextBulletSizePoint` | 1 | 0 | 11 | §21.1.2.4.10 p.3278, OI 2.1.1405 |
| [`a:buSzTx`](#abusztx) | `a:CT_TextBulletSizeFollowText` | 0 | 0 | 11 | §21.1.2.4.11 p.3279 |
| [`a:cNvCxnSpPr`](#acnvcxnsppr) | `a:CT_NonVisualConnectorProperties` | 0 | 4 | 1 | §20.1.2.2.4 p.2740, OI 2.1.1200 |
| [`a:cNvGraphicFramePr`](#acnvgraphicframepr) | `a:CT_NonVisualGraphicFrameProperties` | 0 | 2 | 1 | §20.1.2.2.5 p.2741, OI 2.1.1201 |
| [`a:cNvGrpSpPr`](#acnvgrpsppr) | `a:CT_NonVisualGroupDrawingShapeProps` | 0 | 2 | 1 | §20.1.2.2.6 p.2741, OI 2.1.1202 |
| [`a:cNvPicPr`](#acnvpicpr) | `a:CT_NonVisualPictureProperties` | 1 | 2 | 1 | §20.1.2.2.7 p.2741 |
| [`a:cNvPr`](#acnvpr) | `a:CT_NonVisualDrawingProps` | 5 | 3 | 5 | §20.1.2.2.8 p.2742 |
| [`a:cNvSpPr`](#acnvsppr) | `a:CT_NonVisualDrawingShapeProps` | 1 | 2 | 1 | §20.1.2.2.9 p.2744 |
| [`a:camera`](#acamera) | `a:CT_Camera` | 3 | 1 | 3 | §20.1.5.5 p.2837, OI 2.1.1270 |
| [`a:cell3D`](#acell3d) | `a:CT_Cell3D` | 1 | 3 | 2 | §21.1.3.1 p.3352, OI 2.1.1415 |
| [`a:chExt`](#achext) | `a:CT_PositiveSize2D` | 2 | 0 | 1 | §20.1.7.1 p.2857, OI 2.1.1278 |
| [`a:chOff`](#achoff) | `a:CT_Point2D` | 2 | 0 | 1 | §20.1.7.2 p.2857, OI 2.1.1279 |
| [`a:chart`](#achart) | `a:CT_AnimationChartElement` | 3 | 0 | 1 | §20.1.2.2.3 p.2740 |
| [`a:close`](#aclose) | `a:CT_Path2DClose` | 0 | 0 | 1 | §20.1.9.6 p.2902 |
| [`a:clrChange`](#aclrchange) | `a:CT_ColorChangeEffect` | 1 | 2 | 4 | §20.1.8.16 p.2869, OI 2.1.1290 |
| [`a:clrFrom`](#aclrfrom) | `a:CT_Color` | 0 | 6 | 1 | §20.1.8.17 p.2870 |
| [`a:clrMap`](#aclrmap) | `a:CT_ColorMapping` | 12 | 1 | 1 | §20.1.6.1 p.2848 |
| [`a:clrRepl`](#aclrrepl) | `a:CT_ColorReplaceEffect` | 0 | 6 | 4 | §20.1.8.18 p.2870 |
| [`a:clrScheme`](#aclrscheme) | `a:CT_ColorScheme` | 1 | 13 | 3 | §20.1.6.2 p.2849 |
| [`a:clrTo`](#aclrto) | `a:CT_Color` | 0 | 6 | 1 | §20.1.8.19 p.2870 |
| [`a:comp`](#acomp) | `a:CT_ComplementTransform` | 0 | 0 | 8 | §20.1.2.3.7 p.2770 |
| [`a:cont`](#acont) | `a:CT_EffectContainer` | 2 | 30 | 4 | §20.1.8.20 p.2870, OI 2.1.1291 |
| [`a:contourClr`](#acontourclr) | `a:CT_Color` | 0 | 6 | 3 | §20.1.5.6 p.2839, OI 2.1.1271 |
| [`a:cpLocks`](#acplocks) | `a:CT_ContentPartLocking` | 10 | 1 | 1 | §20.1.2.2.43 p.2765 |
| [`a:cs`](#acs) | `a:CT_TextFont` | 4 | 0 | 7 | §21.1.2.3.1 p.3242, OI 2.1.1392 |
| [`a:cubicBezTo`](#acubicbezto) | `a:CT_Path2DCubicBezierTo` | 0 | 1 | 1 | §20.1.9.7 p.2903 |
| [`a:custClr`](#acustclr) | `a:CT_CustomColor` | 1 | 6 | 1 | §20.1.4.1.8 p.2798 |
| [`a:custClrLst`](#acustclrlst) | `a:CT_CustomColorList` | 0 | 1 | 1 | §20.1.6.3 p.2851 |
| [`a:custDash`](#acustdash) | `a:CT_DashStopList` | 0 | 1 | 9 | §20.1.8.21 p.2870 |
| [`a:custGeom`](#acustgeom) | `a:CT_CustomGeometry2D` | 0 | 6 | 19 | §20.1.9.8 p.2904 |
| [`a:cxn`](#acxn) | `a:CT_ConnectionSite` | 1 | 1 | 1 | §20.1.9.9 p.2905 |
| [`a:cxnLst`](#acxnlst) | `a:CT_ConnectionSiteList` | 0 | 1 | 1 | §20.1.9.10 p.2907 |
| [`a:cxnSp`](#acxnsp) | `a:CT_GvmlConnector` | 0 | 4 | 2 | §20.1.2.2.10 p.2745 |
| [`a:cxnSpLocks`](#acxnsplocks) | `a:CT_ConnectorLocking` | 10 | 1 | 6 | §20.1.2.2.11 p.2746, OI 2.1.1203 |
| [`a:defPPr`](#adefppr) | `a:CT_TextParagraphProperties` | 11 | 17 | 7 | §21.1.2.2.2 p.3210, OI 2.1.1383 |
| [`a:defRPr`](#adefrpr) | `a:CT_TextCharacterProperties` | 19 | 22 | 11 | §21.1.2.3.2 p.3244, OI 2.1.1393 |
| [`a:dgm`](#adgm) | `a:CT_AnimationDgmElement` | 2 | 0 | 1 | §20.1.2.2.12 p.2748, OI 2.1.1204 |
| [`a:dk1`](#adk1) | `a:CT_Color` | 0 | 6 | 1 | §20.1.4.1.9 p.2798, OI 2.1.1254 |
| [`a:dk2`](#adk2) | `a:CT_Color` | 0 | 6 | 1 | §20.1.4.1.10 p.2798, OI 2.1.1255 |
| [`a:ds`](#ads) | `a:CT_DashStop` | 2 | 0 | 1 | §20.1.8.22 p.2871, OI 2.1.1292 |
| [`a:duotone`](#aduotone) | `a:CT_DuotoneEffect` | 0 | 6 | 4 | §20.1.8.23 p.2871, OI 2.1.1293 |
| [`a:ea`](#aea) | `a:CT_TextFont` | 4 | 0 | 7 | §21.1.2.3.3 p.3249, OI 2.1.1394 |
| [`a:effect`](#aeffect) | `a:CT_EffectReference`, `a:CT_EffectProperties` | 1 | 2 | 3 | §20.1.4.2.7 p.2816; §20.1.8.24 p.2871, OI 2.1.1294 |
| [`a:effectDag`](#aeffectdag) | `a:CT_EffectContainer` | 2 | 30 | 40 | §20.1.8.25 p.2872, OI 2.1.1295 |
| [`a:effectLst`](#aeffectlst) | `a:CT_EffectList` | 0 | 8 | 40 | §20.1.8.26 p.2872 |
| [`a:effectRef`](#aeffectref) | `a:CT_StyleMatrixReference` | 1 | 6 | 10 | §20.1.4.2.8 p.2817 |
| [`a:effectStyle`](#aeffectstyle) | `a:CT_EffectStyleItem` | 0 | 4 | 1 | §20.1.4.1.11 p.2799 |
| [`a:effectStyleLst`](#aeffectstylelst) | `a:CT_EffectStyleList` | 0 | 1 | 1 | §20.1.4.1.12 p.2800 |
| [`a:end`](#aend) | `a:CT_AudioCDTime` | 2 | 0 | 1 | §20.1.3.3 p.2788, OI 2.1.1245 |
| [`a:endCxn`](#aendcxn) | `a:CT_Connection` | 2 | 0 | 6 | §20.1.2.2.13 p.2748, OI 2.1.1205 |
| [`a:endParaRPr`](#aendpararpr) | `a:CT_TextCharacterProperties` | 19 | 22 | 1 | §21.1.2.2.3 p.3217, OI 2.1.1384 |
| [`a:ext`](#aext) | `a:CT_PositiveSize2D`, `a:CT_OfficeArtExtension` | 3 | 1 | 33 | §20.1.2.2.14 p.2748, OI 2.1.1206; §20.1.7.3 p.2858, OI 2.1.1280 |
| [`a:extLst`](#aextlst) | `a:CT_OfficeArtExtensionList` | 0 | 1 | 164 | §20.1.2.2.15 p.2749 |
| [`a:extraClrScheme`](#aextraclrscheme) | `a:CT_ColorSchemeAndMapping` | 0 | 2 | 1 | §20.1.6.4 p.2851 |
| [`a:extraClrSchemeLst`](#aextraclrschemelst) | `a:CT_ColorSchemeList` | 0 | 1 | 1 | §20.1.6.5 p.2852 |
| [`a:extrusionClr`](#aextrusionclr) | `a:CT_Color` | 0 | 6 | 3 | §20.1.5.7 p.2840, OI 2.1.1272 |
| [`a:fgClr`](#afgclr) | `a:CT_Color` | 0 | 6 | 1 | §20.1.8.27 p.2874 |
| [`a:fill`](#afill) | `a:CT_FillEffect`, `a:CT_FillProperties` | 0 | 6 | 4 | §20.1.4.2.9 p.2817, OI 2.1.1263; §20.1.8.28 p.2875 |
| [`a:fillOverlay`](#afilloverlay) | `a:CT_FillOverlayEffect` | 1 | 6 | 5 | §20.1.8.29 p.2875 |
| [`a:fillRect`](#afillrect) | `a:CT_RelativeRect` | 4 | 0 | 1 | §20.1.8.30 p.2875 |
| [`a:fillRef`](#afillref) | `a:CT_StyleMatrixReference` | 1 | 6 | 11 | §20.1.4.2.10 p.2818 |
| [`a:fillStyleLst`](#afillstylelst) | `a:CT_FillStyleList` | 0 | 6 | 1 | §20.1.4.1.13 p.2801 |
| [`a:fillToRect`](#afilltorect) | `a:CT_RelativeRect` | 4 | 0 | 1 | §20.1.8.31 p.2876, OI 2.1.1296 |
| [`a:firstCol`](#afirstcol) | `a:CT_TablePartStyle` | 0 | 2 | 2 | §20.1.4.2.11 p.2819 |
| [`a:firstRow`](#afirstrow) | `a:CT_TablePartStyle` | 0 | 2 | 2 | §20.1.4.2.12 p.2820 |
| [`a:flatTx`](#aflattx) | `a:CT_FlatText` | 1 | 0 | 5 | §20.1.5.8 p.2841, OI 2.1.1273 |
| [`a:fld`](#afld) | `a:CT_TextField` | 2 | 3 | 1 | §21.1.2.2.4 p.3222, OI 2.1.1385 |
| [`a:fmtScheme`](#afmtscheme) | `a:CT_StyleMatrix` | 1 | 4 | 2 | §20.1.4.1.14 p.2801 |
| [`a:folHlink`](#afolhlink) | `a:CT_Color` | 0 | 6 | 1 | §20.1.4.1.15 p.2802, OI 2.1.1256 |
| [`a:font`](#afont) | `a:CT_SupplementalFont`, `a:CT_FontCollection` | 2 | 5 | 4 | §20.1.4.1.16 p.2803; §20.1.4.2.13 p.2821 |
| [`a:fontRef`](#afontref) | `a:CT_FontReference` | 1 | 6 | 10 | §20.1.4.1.17 p.2804, OI 2.1.1257 |
| [`a:fontScheme`](#afontscheme) | `a:CT_FontScheme` | 1 | 3 | 2 | §20.1.4.1.18 p.2804 |
| [`a:gamma`](#agamma) | `a:CT_GammaTransform` | 0 | 0 | 8 | §20.1.2.3.8 p.2771 |
| [`a:gd`](#agd) | `a:CT_GeomGuide` | 2 | 0 | 2 | §20.1.9.11 p.2907, OI 2.1.1311 |
| [`a:gdLst`](#agdlst) | `a:CT_GeomGuideList` | 0 | 1 | 1 | §20.1.9.12 p.2910 |
| [`a:glow`](#aglow) | `a:CT_GlowEffect` | 1 | 6 | 3 | §20.1.8.32 p.2878 |
| [`a:gradFill`](#agradfill) | `a:CT_GradientFillProperties` | 2 | 4 | 50 | §20.1.8.33 p.2878, OI 2.1.1297 |
| [`a:graphic`](#agraphic) ◆ | `a:CT_GraphicalObject` | 0 | 1 | 9 | §20.1.2.2.16 p.2749 |
| [`a:graphicData`](#agraphicdata) | `a:CT_GraphicalObjectData` | 1 | 1 | 1 | §20.1.2.2.17 p.2749, OI 2.1.1207 |
| [`a:graphicFrame`](#agraphicframe) | `a:CT_GvmlGraphicalObjectFrame` | 0 | 4 | 2 | §20.1.2.2.18 p.2750 |
| [`a:graphicFrameLocks`](#agraphicframelocks) | `a:CT_GraphicalObjectFrameLocking` | 6 | 1 | 7 | §20.1.2.2.19 p.2750, OI 2.1.1208 |
| [`a:gray`](#agray) | `a:CT_GrayscaleTransform` | 0 | 0 | 8 | §20.1.2.3.9 p.2771 |
| [`a:grayscl`](#agrayscl) | `a:CT_GrayscaleEffect` | 0 | 0 | 4 | §20.1.8.34 p.2880 |
| [`a:green`](#agreen) | `a:CT_Percentage` | 1 | 0 | 8 | §20.1.2.3.10 p.2771, OI 2.1.1224 |
| [`a:greenMod`](#agreenmod) | `a:CT_Percentage` | 1 | 0 | 8 | §20.1.2.3.11 p.2771, OI 2.1.1225 |
| [`a:greenOff`](#agreenoff) | `a:CT_Percentage` | 1 | 0 | 8 | §20.1.2.3.12 p.2772, OI 2.1.1226 |
| [`a:gridCol`](#agridcol) | `a:CT_TableCol` | 1 | 1 | 1 | §21.1.3.2 p.3352, OI 2.1.1416 |
| [`a:grpFill`](#agrpfill) | `a:CT_GroupFillProperties` | 0 | 0 | 41 | §20.1.8.35 p.2880, OI 2.1.1298 |
| [`a:grpSp`](#agrpsp) | `a:CT_GvmlGroupShape` | 0 | 9 | 2 | §20.1.2.2.20 p.2751 |
| [`a:grpSpLocks`](#agrpsplocks) | `a:CT_GroupLocking` | 7 | 1 | 7 | §20.1.2.2.21 p.2752, OI 2.1.1209 |
| [`a:grpSpPr`](#agrpsppr) | `a:CT_GroupShapeProperties` | 1 | 11 | 2 | §20.1.2.2.22 p.2753, OI 2.1.1210 |
| [`a:gs`](#ags) | `a:CT_GradientStop` | 1 | 6 | 1 | §20.1.8.36 p.2880 |
| [`a:gsLst`](#agslst) | `a:CT_GradientStopList` | 0 | 1 | 1 | §20.1.8.37 p.2880 |
| [`a:headEnd`](#aheadend) | `a:CT_LineEndProperties` | 3 | 0 | 9 | §20.1.8.38 p.2880 |
| [`a:header`](#aheader) | `xsd:string` | 0 | 0 | 1 | §21.1.3.3 p.3353 |
| [`a:headers`](#aheaders) | `a:CT_Headers` | 0 | 1 | 1 | §21.1.3.4 p.3354 |
| [`a:highlight`](#ahighlight) | `a:CT_Color` | 0 | 6 | 4 | §21.1.2.3.4 p.3251 |
| [`a:hlink`](#ahlink) | `a:CT_Color` | 0 | 6 | 1 | §20.1.4.1.19 p.2805, OI 2.1.1258 |
| [`a:hlinkClick`](#ahlinkclick) | `a:CT_Hyperlink` | 8 | 2 | 20 | §21.1.2.3.5 p.3251, OI 2.1.1395 |
| [`a:hlinkHover`](#ahlinkhover) | `a:CT_Hyperlink` | 8 | 2 | 16 | §20.1.2.2.23 p.2754, OI 2.1.1211 |
| [`a:hlinkMouseOver`](#ahlinkmouseover) | `a:CT_Hyperlink` | 8 | 2 | 4 | §21.1.2.3.6 p.3253, OI 2.1.1396 |
| [`a:hsl`](#ahsl) | `a:CT_HSLEffect` | 3 | 0 | 4 | §20.1.8.39 p.2881, OI 2.1.1299 |
| [`a:hslClr`](#ahslclr) | `a:CT_HslColor` | 3 | 28 | 58 | §20.1.2.3.13 p.2773, OI 2.1.1227 |
| [`a:hue`](#ahue) | `a:CT_PositiveFixedAngle` | 1 | 0 | 8 | §20.1.2.3.14 p.2773, OI 2.1.1228 |
| [`a:hueMod`](#ahuemod) | `a:CT_PositivePercentage` | 1 | 0 | 8 | §20.1.2.3.15 p.2774, OI 2.1.1229 |
| [`a:hueOff`](#ahueoff) | `a:CT_Angle` | 1 | 0 | 8 | §20.1.2.3.16 p.2775, OI 2.1.1230 |
| [`a:innerShdw`](#ainnershdw) | `a:CT_InnerShadowEffect` | 3 | 6 | 3 | §20.1.8.40 p.2882, OI 2.1.1300 |
| [`a:insideH`](#ainsideh) | `a:CT_ThemeableLineStyle` | 0 | 2 | 1 | §20.1.4.2.14 p.2821 |
| [`a:insideV`](#ainsidev) | `a:CT_ThemeableLineStyle` | 0 | 2 | 1 | §20.1.4.2.15 p.2821 |
| [`a:inv`](#ainv) | `a:CT_InverseTransform` | 0 | 0 | 8 | §20.1.2.3.17 p.2775 |
| [`a:invGamma`](#ainvgamma) | `a:CT_InverseGammaTransform` | 0 | 0 | 8 | §20.1.2.3.18 p.2776 |
| [`a:lastCol`](#alastcol) | `a:CT_TablePartStyle` | 0 | 2 | 2 | §20.1.4.2.16 p.2822 |
| [`a:lastRow`](#alastrow) | `a:CT_TablePartStyle` | 0 | 2 | 2 | §20.1.4.2.17 p.2823 |
| [`a:latin`](#alatin) | `a:CT_TextFont` | 4 | 0 | 7 | §21.1.2.3.7 p.3255, OI 2.1.1397 |
| [`a:left`](#aleft) | `a:CT_ThemeableLineStyle` | 0 | 2 | 1 | §20.1.4.2.18 p.2824 |
| [`a:lightRig`](#alightrig) | `a:CT_LightRig` | 2 | 1 | 4 | §20.1.5.9 p.2841, OI 2.1.1274 |
| [`a:lin`](#alin) | `a:CT_LinearShadeProperties` | 2 | 0 | 1 | §20.1.8.41 p.2882 |
| [`a:ln`](#aln) | `a:CT_LineProperties` | 4 | 12 | 34 | §20.1.2.2.24 p.2755, OI 2.1.1212 |
| [`a:lnB`](#alnb) | `a:CT_LineProperties` | 4 | 12 | 1 | §21.1.3.5 p.3356, OI 2.1.1417 |
| [`a:lnBlToTr`](#alnbltotr) | `a:CT_LineProperties` | 4 | 12 | 1 | §21.1.3.6 p.3357, OI 2.1.1418 |
| [`a:lnDef`](#alndef) | `a:CT_DefaultShapeDefinition` | 0 | 5 | 1 | §20.1.4.1.20 p.2805 |
| [`a:lnL`](#alnl) | `a:CT_LineProperties` | 4 | 12 | 1 | §21.1.3.7 p.3358, OI 2.1.1419 |
| [`a:lnR`](#alnr) | `a:CT_LineProperties` | 4 | 12 | 1 | §21.1.3.8 p.3359, OI 2.1.1420 |
| [`a:lnRef`](#alnref) | `a:CT_StyleMatrixReference` | 1 | 6 | 17 | §20.1.4.2.19 p.2824, OI 2.1.1264 |
| [`a:lnSpc`](#alnspc) | `a:CT_TextSpacing` | 0 | 2 | 11 | §21.1.2.2.5 p.3224, OI 2.1.1386 |
| [`a:lnStyleLst`](#alnstylelst) | `a:CT_LineStyleList` | 0 | 1 | 1 | §20.1.4.1.21 p.2806 |
| [`a:lnT`](#alnt) | `a:CT_LineProperties` | 4 | 12 | 1 | §21.1.3.9 p.3360, OI 2.1.1421 |
| [`a:lnTlToBr`](#alntltobr) | `a:CT_LineProperties` | 4 | 12 | 1 | §21.1.3.10 p.3361, OI 2.1.1422 |
| [`a:lnTo`](#alnto) | `a:CT_Path2DLineTo` | 0 | 1 | 1 | §20.1.9.13 p.2910 |
| [`a:lstStyle`](#alststyle) | `a:CT_TextListStyle` | 0 | 11 | 14 | §21.1.2.4.12 p.3279 |
| [`a:lt1`](#alt1) | `a:CT_Color` | 0 | 6 | 1 | §20.1.4.1.22 p.2807, OI 2.1.1259 |
| [`a:lt2`](#alt2) | `a:CT_Color` | 0 | 6 | 1 | §20.1.4.1.23 p.2807, OI 2.1.1260 |
| [`a:lum`](#alum) | `a:CT_LuminanceEffect`, `a:CT_Percentage` | 3 | 0 | 12 | §20.1.2.3.19 p.2776, OI 2.1.1231; §20.1.8.42 p.2883, OI 2.1.1301 |
| [`a:lumMod`](#alummod) | `a:CT_Percentage` | 1 | 0 | 8 | §20.1.2.3.20 p.2777, OI 2.1.1232 |
| [`a:lumOff`](#alumoff) | `a:CT_Percentage` | 1 | 0 | 8 | §20.1.2.3.21 p.2777, OI 2.1.1233 |
| [`a:lvl1pPr`](#alvl1ppr) | `a:CT_TextParagraphProperties` | 11 | 17 | 7 | §21.1.2.4.13 p.3279, OI 2.1.1406 |
| [`a:lvl2pPr`](#alvl2ppr) | `a:CT_TextParagraphProperties` | 11 | 17 | 7 | §21.1.2.4.14 p.3287, OI 2.1.1407 |
| [`a:lvl3pPr`](#alvl3ppr) | `a:CT_TextParagraphProperties` | 11 | 17 | 7 | §21.1.2.4.15 p.3295, OI 2.1.1408 |
| [`a:lvl4pPr`](#alvl4ppr) | `a:CT_TextParagraphProperties` | 11 | 17 | 7 | §21.1.2.4.16 p.3303, OI 2.1.1409 |
| [`a:lvl5pPr`](#alvl5ppr) | `a:CT_TextParagraphProperties` | 11 | 17 | 7 | §21.1.2.4.17 p.3311, OI 2.1.1410 |
| [`a:lvl6pPr`](#alvl6ppr) | `a:CT_TextParagraphProperties` | 11 | 17 | 7 | §21.1.2.4.18 p.3319, OI 2.1.1411 |
| [`a:lvl7pPr`](#alvl7ppr) | `a:CT_TextParagraphProperties` | 11 | 17 | 7 | §21.1.2.4.19 p.3327, OI 2.1.1412 |
| [`a:lvl8pPr`](#alvl8ppr) | `a:CT_TextParagraphProperties` | 11 | 17 | 7 | §21.1.2.4.20 p.3335, OI 2.1.1413 |
| [`a:lvl9pPr`](#alvl9ppr) | `a:CT_TextParagraphProperties` | 11 | 17 | 7 | §21.1.2.4.21 p.3343, OI 2.1.1414 |
| [`a:majorFont`](#amajorfont) | `a:CT_FontCollection` | 0 | 5 | 1 | §20.1.4.1.24 p.2808 |
| [`a:masterClrMapping`](#amasterclrmapping) | `a:CT_EmptyElement` | 0 | 0 | 1 | §20.1.6.6 p.2853 |
| [`a:minorFont`](#aminorfont) | `a:CT_FontCollection` | 0 | 5 | 1 | §20.1.4.1.25 p.2809 |
| [`a:miter`](#amiter) | `a:CT_LineJoinMiterProperties` | 1 | 0 | 9 | §20.1.8.43 p.2883, OI 2.1.1302 |
| [`a:moveTo`](#amoveto) | `a:CT_Path2DMoveTo` | 0 | 1 | 1 | §20.1.9.14 p.2911 |
| [`a:neCell`](#anecell) | `a:CT_TablePartStyle` | 0 | 2 | 2 | §20.1.4.2.20 p.2825 |
| [`a:noAutofit`](#anoautofit) | `a:CT_TextNoAutofit` | 0 | 0 | 4 | §21.1.2.1.2 p.3205, OI 2.1.1380 |
| [`a:noFill`](#anofill) | `a:CT_NoFillProperties` | 0 | 0 | 50 | §20.1.8.44 p.2884 |
| [`a:norm`](#anorm) | `a:CT_Vector3D` | 3 | 0 | 1 | §20.1.5.10 p.2842, OI 2.1.1275 |
| [`a:normAutofit`](#anormautofit) | `a:CT_TextNormalAutofit` | 2 | 0 | 4 | §21.1.2.1.3 p.3206, OI 2.1.1381 |
| [`a:nvCxnSpPr`](#anvcxnsppr) | `a:CT_GvmlConnectorNonVisual` | 0 | 2 | 1 | §20.1.2.2.25 p.2756 |
| [`a:nvGraphicFramePr`](#anvgraphicframepr) | `a:CT_GvmlGraphicFrameNonVisual` | 0 | 2 | 1 | §20.1.2.2.26 p.2756 |
| [`a:nvGrpSpPr`](#anvgrpsppr) | `a:CT_GvmlGroupShapeNonVisual` | 0 | 2 | 2 | §20.1.2.2.27 p.2756 |
| [`a:nvPicPr`](#anvpicpr) | `a:CT_GvmlPictureNonVisual` | 0 | 2 | 1 | §20.1.2.2.28 p.2757 |
| [`a:nvSpPr`](#anvsppr) | `a:CT_GvmlShapeNonVisual` | 0 | 2 | 1 | §20.1.2.2.29 p.2757 |
| [`a:nwCell`](#anwcell) | `a:CT_TablePartStyle` | 0 | 2 | 2 | §20.1.4.2.21 p.2825 |
| [`a:objectDefaults`](#aobjectdefaults) | `a:CT_ObjectStyleDefaults` | 0 | 4 | 1 | §20.1.6.7 p.2853 |
| [`a:off`](#aoff) | `a:CT_Point2D` | 2 | 0 | 12 | §20.1.7.4 p.2859, OI 2.1.1281 |
| [`a:outerShdw`](#aoutershdw) | `a:CT_OuterShadowEffect` | 9 | 6 | 3 | §20.1.8.45 p.2884, OI 2.1.1303 |
| [`a:overrideClrMapping`](#aoverrideclrmapping) | `a:CT_ColorMapping` | 12 | 1 | 1 | §20.1.6.8 p.2853 |
| [`a:p`](#ap) | `a:CT_TextParagraph` | 0 | 5 | 11 | §21.1.2.2.6 p.3225, OI 2.1.1387 |
| [`a:pPr`](#appr) | `a:CT_TextParagraphProperties` | 11 | 17 | 2 | §21.1.2.2.7 p.3225, OI 2.1.1388 |
| [`a:path`](#apath) | `a:CT_PathShadeProperties`, `a:CT_Path2D` | 6 | 7 | 2 | §20.1.8.46 p.2885; §20.1.9.15 p.2911, OI 2.1.1312 |
| [`a:pathLst`](#apathlst) | `a:CT_Path2DList` | 0 | 1 | 1 | §20.1.9.16 p.2913 |
| [`a:pattFill`](#apattfill) | `a:CT_PatternFillProperties` | 1 | 2 | 50 | §20.1.8.47 p.2886 |
| [`a:pic`](#apic) | `a:CT_GvmlPicture` | 0 | 5 | 2 | §20.1.2.2.30 p.2757 |
| [`a:picLocks`](#apiclocks) | `a:CT_PictureLocking` | 11 | 1 | 5 | §20.1.2.2.31 p.2758, OI 2.1.1213 |
| [`a:pos`](#apos) | `a:CT_AdjPoint2D` | 2 | 0 | 3 | §20.1.9.17 p.2914, OI 2.1.1313 |
| [`a:prstClr`](#aprstclr) | `a:CT_PresetColor` | 1 | 28 | 58 | §20.1.2.3.22 p.2778, OI 2.1.1234 |
| [`a:prstDash`](#aprstdash) | `a:CT_PresetLineDashProperties` | 1 | 0 | 9 | §20.1.8.48 p.2886 |
| [`a:prstGeom`](#aprstgeom) | `a:CT_PresetGeometry2D` | 1 | 1 | 19 | §20.1.9.18 p.2916 |
| [`a:prstShdw`](#aprstshdw) | `a:CT_PresetShadowEffect` | 3 | 6 | 3 | §20.1.8.49 p.2887 |
| [`a:prstTxWarp`](#aprsttxwarp) | `a:CT_PresetTextShape` | 1 | 1 | 4 | §20.1.9.19 p.2917 |
| [`a:pt`](#apt) | `a:CT_AdjPoint2D` | 2 | 0 | 4 | §20.1.9.20 p.2920, OI 2.1.1314 |
| [`a:quadBezTo`](#aquadbezto) | `a:CT_Path2DQuadBezierTo` | 0 | 1 | 1 | §20.1.9.21 p.2922 |
| [`a:quickTimeFile`](#aquicktimefile) | `a:CT_QuickTimeFile` | 1 | 1 | 2 | §20.1.3.4 p.2789 |
| [`a:r`](#ar) | `a:CT_RegularTextRun` | 0 | 2 | 1 | §21.1.2.3.8 p.3257, OI 2.1.1398 |
| [`a:rPr`](#arpr) | `a:CT_TextCharacterProperties` | 19 | 22 | 3 | §21.1.2.3.9 p.3257, OI 2.1.1399 |
| [`a:rect`](#arect) | `a:CT_GeomRect` | 4 | 0 | 1 | §20.1.9.22 p.2922, OI 2.1.1315 |
| [`a:red`](#ared) | `a:CT_Percentage` | 1 | 0 | 8 | §20.1.2.3.23 p.2778, OI 2.1.1235 |
| [`a:redMod`](#aredmod) | `a:CT_Percentage` | 1 | 0 | 8 | §20.1.2.3.24 p.2779, OI 2.1.1236 |
| [`a:redOff`](#aredoff) | `a:CT_Percentage` | 1 | 0 | 8 | §20.1.2.3.25 p.2780, OI 2.1.1237 |
| [`a:reflection`](#areflection) | `a:CT_ReflectionEffect` | 14 | 0 | 3 | §20.1.8.50 p.2887, OI 2.1.1304 |
| [`a:relOff`](#areloff) | `a:CT_RelativeOffsetEffect` | 2 | 0 | 2 | §20.1.8.51 p.2890 |
| [`a:right`](#aright) | `a:CT_ThemeableLineStyle` | 0 | 2 | 1 | §20.1.4.2.22 p.2826 |
| [`a:rot`](#arot) | `a:CT_SphereCoords` | 3 | 0 | 2 | §20.1.5.11 p.2843 |
| [`a:round`](#around) | `a:CT_LineJoinRound` | 0 | 0 | 9 | §20.1.8.52 p.2890, OI 2.1.1305 |
| [`a:rtl`](#artl) | `a:CT_Boolean` | 1 | 0 | 4 | §21.1.2.2.8 p.3233 |
| [`a:sat`](#asat) | `a:CT_Percentage` | 1 | 0 | 8 | §20.1.2.3.26 p.2780, OI 2.1.1238 |
| [`a:satMod`](#asatmod) | `a:CT_Percentage` | 1 | 0 | 8 | §20.1.2.3.27 p.2781, OI 2.1.1239 |
| [`a:satOff`](#asatoff) | `a:CT_Percentage` | 1 | 0 | 8 | §20.1.2.3.28 p.2782, OI 2.1.1240 |
| [`a:scene3d`](#ascene3d) | `a:CT_Scene3D` | 0 | 4 | 30 | §20.1.4.1.26 p.2809 |
| [`a:schemeClr`](#aschemeclr) | `a:CT_SchemeColor` | 1 | 28 | 58 | §20.1.2.3.29 p.2782 |
| [`a:scrgbClr`](#ascrgbclr) | `a:CT_ScRgbColor` | 3 | 28 | 58 | §20.1.2.3.30 p.2783 |
| [`a:seCell`](#asecell) | `a:CT_TablePartStyle` | 0 | 2 | 2 | §20.1.4.2.23 p.2827 |
| [`a:shade`](#ashade) | `a:CT_PositiveFixedPercentage` | 1 | 0 | 8 | §20.1.2.3.31 p.2784, OI 2.1.1241 |
| [`a:snd`](#asnd) | `a:CT_EmbeddedWAVAudioFile` | 2 | 0 | 3 | §20.1.2.2.32 p.2760 |
| [`a:softEdge`](#asoftedge) | `a:CT_SoftEdgesEffect` | 1 | 0 | 3 | §20.1.8.53 p.2890, OI 2.1.1306 |
| [`a:solidFill`](#asolidfill) | `a:CT_SolidColorFillProperties` | 0 | 6 | 50 | §20.1.8.54 p.2891 |
| [`a:sp`](#asp) | `a:CT_GvmlShape` | 0 | 5 | 2 | §20.1.2.2.33 p.2760 |
| [`a:sp3d`](#asp3d) | `a:CT_Shape3D` | 4 | 5 | 24 | §20.1.5.12 p.2844, OI 2.1.1276 |
| [`a:spAutoFit`](#aspautofit) | `a:CT_TextShapeAutofit` | 0 | 0 | 4 | §21.1.2.1.4 p.3207, OI 2.1.1382 |
| [`a:spDef`](#aspdef) | `a:CT_DefaultShapeDefinition` | 0 | 5 | 1 | §20.1.4.1.27 p.2810 |
| [`a:spLocks`](#asplocks) | `a:CT_ShapeLocking` | 11 | 1 | 7 | §20.1.2.2.34 p.2761, OI 2.1.1214 |
| [`a:spPr`](#asppr) | `a:CT_ShapeProperties` | 1 | 15 | 6 | §20.1.2.2.35 p.2762, OI 2.1.1215 |
| [`a:spcAft`](#aspcaft) | `a:CT_TextSpacing` | 0 | 2 | 11 | §21.1.2.2.9 p.3236, OI 2.1.1389 |
| [`a:spcBef`](#aspcbef) | `a:CT_TextSpacing` | 0 | 2 | 11 | §21.1.2.2.10 p.3236, OI 2.1.1390 |
| [`a:spcPct`](#aspcpct) | `a:CT_TextSpacingPercent` | 1 | 0 | 3 | §21.1.2.2.11 p.3237, OI 2.1.1391 |
| [`a:spcPts`](#aspcpts) | `a:CT_TextSpacingPoint` | 1 | 0 | 3 | §21.1.2.2.12 p.3238 |
| [`a:srcRect`](#asrcrect) | `a:CT_RelativeRect` | 4 | 0 | 6 | §20.1.8.55 p.2891, OI 2.1.1307 |
| [`a:srgbClr`](#asrgbclr) | `a:CT_SRgbColor` | 1 | 28 | 58 | §20.1.2.3.32 p.2784 |
| [`a:st`](#ast) | `a:CT_AudioCDTime` | 2 | 0 | 1 | §20.1.3.5 p.2790, OI 2.1.1246 |
| [`a:stCxn`](#astcxn) | `a:CT_Connection` | 2 | 0 | 6 | §20.1.2.2.36 p.2763, OI 2.1.1216 |
| [`a:stretch`](#astretch) | `a:CT_StretchInfoProperties` | 0 | 1 | 6 | §20.1.8.56 p.2891 |
| [`a:style`](#astyle) | `a:CT_ShapeStyle` | 0 | 4 | 6 | §20.1.2.2.37 p.2763, OI 2.1.1217 |
| [`a:swCell`](#aswcell) | `a:CT_TablePartStyle` | 0 | 2 | 2 | §20.1.4.2.24 p.2827 |
| [`a:sx`](#asx) | `a:CT_Ratio` | 2 | 0 | 1 | §20.1.2.2.38 p.2763 |
| [`a:sy`](#asy) | `a:CT_Ratio` | 2 | 0 | 1 | §20.1.2.2.39 p.2764 |
| [`a:sym`](#asym) | `a:CT_TextFont` | 4 | 0 | 4 | §21.1.2.3.10 p.3262, OI 2.1.1400 |
| [`a:sysClr`](#asysclr) | `a:CT_SystemColor` | 2 | 28 | 58 | §20.1.2.3.33 p.2785, OI 2.1.1242 |
| [`a:t`](#at) | `xsd:string` | 0 | 0 | 2 | §21.1.2.3.11 p.3264 |
| [`a:tab`](#atab) | `a:CT_TextTabStop` | 2 | 0 | 1 | §21.1.2.2.13 p.3239 |
| [`a:tabLst`](#atablst) | `a:CT_TextTabStopList` | 0 | 1 | 11 | §21.1.2.2.14 p.3240 |
| [`a:tableStyle`](#atablestyle) | `a:CT_TableStyle` | 2 | 15 | 1 | §21.1.3.11 p.3362 |
| [`a:tableStyleId`](#atablestyleid) | `s:ST_Guid` | 0 | 0 | 1 | §21.1.3.12 p.3364, OI 2.1.1423 |
| [`a:tailEnd`](#atailend) | `a:CT_LineEndProperties` | 3 | 0 | 9 | §20.1.8.57 p.2892 |
| [`a:tbl`](#atbl) ◆ | `a:CT_Table` | 0 | 3 | 0 | §21.1.3.13 p.3364, OI 2.1.1424 |
| [`a:tblBg`](#atblbg) | `a:CT_TableBackgroundStyle` | 0 | 4 | 2 | §20.1.4.2.25 p.2828 |
| [`a:tblGrid`](#atblgrid) | `a:CT_TableGrid` | 0 | 1 | 1 | §21.1.3.14 p.3365, OI 2.1.1425 |
| [`a:tblPr`](#atblpr) | `a:CT_TableProperties` | 7 | 11 | 1 | §21.1.3.15 p.3365 |
| [`a:tblStyle`](#atblstyle) | `a:CT_TableStyle` | 2 | 15 | 1 | §20.1.4.2.26 p.2828, OI 2.1.1265 |
| [`a:tblStyleLst`](#atblstylelst) ◆ | `a:CT_TableStyleList` | 1 | 1 | 0 | §20.1.4.2.27 p.2829 |
| [`a:tc`](#atc) | `a:CT_TableCell` | 5 | 3 | 1 | §21.1.3.16 p.3368, OI 2.1.1426 |
| [`a:tcBdr`](#atcbdr) | `a:CT_TableCellBorderStyle` | 0 | 9 | 1 | §20.1.4.2.28 p.2829 |
| [`a:tcPr`](#atcpr) | `a:CT_TableCellProperties` | 8 | 15 | 1 | §21.1.3.17 p.3371 |
| [`a:tcStyle`](#atcstyle) | `a:CT_TableStyleCellStyle` | 0 | 4 | 13 | §20.1.4.2.29 p.2830 |
| [`a:tcTxStyle`](#atctxstyle) | `a:CT_TableStyleTextStyle` | 2 | 9 | 13 | §20.1.4.2.30 p.2831 |
| [`a:theme`](#atheme) ◆ | `a:CT_OfficeStyleSheet` | 1 | 5 | 0 | §20.1.6.9 p.2855 |
| [`a:themeElements`](#athemeelements) | `a:CT_BaseStyles` | 0 | 4 | 1 | §20.1.6.10 p.2855 |
| [`a:themeManager`](#athememanager) ◆ | `a:CT_EmptyElement` | 0 | 0 | 0 | §20.1.6.11 p.2856 |
| [`a:themeOverride`](#athemeoverride) ◆ | `a:CT_BaseStylesOverride` | 0 | 3 | 0 | §20.1.6.12 p.2856 |
| [`a:tile`](#atile) | `a:CT_TileInfoProperties` | 6 | 0 | 6 | §20.1.8.58 p.2892 |
| [`a:tileRect`](#atilerect) | `a:CT_RelativeRect` | 4 | 0 | 1 | §20.1.8.59 p.2894, OI 2.1.1308 |
| [`a:tint`](#atint) | `a:CT_TintEffect`, `a:CT_PositiveFixedPercentage` | 3 | 0 | 12 | §20.1.2.3.34 p.2785, OI 2.1.1243; §20.1.8.60 p.2895 |
| [`a:tl2br`](#atl2br) | `a:CT_ThemeableLineStyle` | 0 | 2 | 1 | §20.1.4.2.31 p.2832 |
| [`a:top`](#atop) | `a:CT_ThemeableLineStyle` | 0 | 2 | 1 | §20.1.4.2.32 p.2832 |
| [`a:tr`](#atr) | `a:CT_TableRow` | 1 | 2 | 1 | §21.1.3.18 p.3374, OI 2.1.1427 |
| [`a:tr2bl`](#atr2bl) | `a:CT_ThemeableLineStyle` | 0 | 2 | 1 | §20.1.4.2.33 p.2832 |
| [`a:txBody`](#atxbody) | `a:CT_TextBody` | 0 | 3 | 2 | §20.1.2.2.40 p.2764 |
| [`a:txDef`](#atxdef) | `a:CT_DefaultShapeDefinition` | 0 | 5 | 1 | §20.1.4.1.28 p.2811 |
| [`a:txSp`](#atxsp) | `a:CT_GvmlTextShape` | 0 | 4 | 3 | §20.1.2.2.41 p.2764 |
| [`a:uFill`](#aufill) | `a:CT_TextUnderlineFillGroupWrapper` | 0 | 6 | 4 | §21.1.2.3.12 p.3265 |
| [`a:uFillTx`](#aufilltx) | `a:CT_TextUnderlineFillFollowText` | 0 | 0 | 4 | §21.1.2.3.13 p.3265, OI 2.1.1401 |
| [`a:uLn`](#auln) | `a:CT_LineProperties` | 4 | 12 | 4 | §21.1.2.3.14 p.3266, OI 2.1.1402 |
| [`a:uLnTx`](#aulntx) | `a:CT_TextUnderlineLineFollowText` | 0 | 0 | 4 | §21.1.2.3.15 p.3267 |
| [`a:up`](#aup) | `a:CT_Vector3D` | 3 | 0 | 1 | §20.1.5.13 p.2846, OI 2.1.1277 |
| [`a:useSpRect`](#ausesprect) | `a:CT_GvmlUseShapeRectangle` | 0 | 0 | 1 | §20.1.2.2.42 p.2765 |
| [`a:videoFile`](#avideofile) ◆ | `a:CT_VideoFile` | 2 | 1 | 2 | §20.1.3.6 p.2791, OI 2.1.1247 |
| [`a:wavAudioFile`](#awavaudiofile) | `a:CT_EmbeddedWAVAudioFile` | 2 | 0 | 2 | §20.1.3.7 p.2792 |
| [`a:wholeTbl`](#awholetbl) | `a:CT_TablePartStyle` | 0 | 2 | 2 | §20.1.4.2.34 p.2833 |
| [`a:xfrm`](#axfrm) | `a:CT_Transform2D`, `a:CT_TransformEffect`, `a:CT_GroupTransform2D` | 9 | 4 | 29 | §20.1.7.5 p.2860, OI 2.1.1282; §20.1.7.6 p.2861, OI 2.1.1283; §20.1.8.61 p.2895 |

◆ declared globally (can be the root element of a part)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `a:ST_AdjAngle` | union of a:ST_Angle, a:ST_GeomGuideName |  | §20.1.10.1 p.2923 |
| `a:ST_AdjCoordinate` | union of a:ST_Coordinate, a:ST_GeomGuideName |  | §20.1.10.2 p.2924 |
| `a:ST_Angle` | restriction of xsd:int |  | §20.1.10.3 p.2924 |
| `a:ST_AnimationBuildType` | restriction of xsd:token | `allAtOnce` | §20.1.10.4 p.2924 |
| `a:ST_AnimationChartBuildType` | union of a:ST_AnimationBuildType, a:ST_AnimationChartOnlyBuildType |  | §20.1.10.5 p.2924 |
| `a:ST_AnimationChartOnlyBuildType` | restriction of xsd:token | `series`, `category`, `seriesEl`, `categoryEl` | §20.1.10.6 p.2925 |
| `a:ST_AnimationDgmBuildType` | union of a:ST_AnimationBuildType, a:ST_AnimationDgmOnlyBuildType |  | §20.1.10.7 p.2925 |
| `a:ST_AnimationDgmOnlyBuildType` | restriction of xsd:token | `one`, `lvlOne`, `lvlAtOnce` | §20.1.10.8 p.2925 |
| `a:ST_BevelPresetType` | restriction of xsd:token | `relaxedInset`, `circle`, `slope`, `cross`, `angle`, `softRound`, `convex`, `coolSlant`, `divot`, `riblet`, `hardEdge`, `artDeco` | §20.1.10.9 p.2926, OI 2.1.1316 |
| `a:ST_BlackWhiteMode` | restriction of xsd:token | `clr`, `auto`, `gray`, `ltGray`, `invGray`, `grayWhite`, `blackGray`, `blackWhite`, `black`, `white`, `hidden` | §20.1.10.10 p.2930 |
| `a:ST_BlendMode` | restriction of xsd:token | `over`, `mult`, `screen`, `darken`, `lighten` | §20.1.10.11 p.2931 |
| `a:ST_BlipCompression` | restriction of xsd:token | `email`, `screen`, `print`, `hqprint`, `none` | §20.1.10.12 p.2931, OI 2.1.1317 |
| `a:ST_ChartBuildStep` | restriction of xsd:token | `category`, `ptInCategory`, `series`, `ptInSeries`, `allPts`, `gridLegend` | §20.1.10.13 p.2931 |
| `a:ST_ColorSchemeIndex` | restriction of xsd:token | `dk1`, `lt1`, `dk2`, `lt2`, `accent1`, `accent2`, `accent3`, `accent4`, `accent5`, `accent6`, `hlink`, `folHlink` | §20.1.10.14 p.2932 |
| `a:ST_CompoundLine` | restriction of xsd:token | `sng`, `dbl`, `thickThin`, `thinThick`, `tri` | §20.1.10.15 p.2933, OI 2.1.1318 |
| `a:ST_Coordinate` | union of a:ST_CoordinateUnqualified, s:ST_UniversalMeasure |  | §20.1.10.16 p.2933, OI 2.1.1319 |
| `a:ST_Coordinate32` | union of a:ST_Coordinate32Unqualified, s:ST_UniversalMeasure |  | §20.1.10.17 p.2933, OI 2.1.1320 |
| `a:ST_Coordinate32Unqualified` | restriction of xsd:int |  | §20.1.10.18 p.2934 |
| `a:ST_CoordinateUnqualified` | restriction of xsd:long | minInclusive `-27273042329600`, maxInclusive `27273042316900` | §20.1.10.19 p.2934 |
| `a:ST_DgmBuildStep` | restriction of xsd:token | `sp`, `bg` | §20.1.10.20 p.2934 |
| `a:ST_DrawingElementId` | restriction of xsd:unsignedInt |  | §20.1.10.21 p.2934, OI 2.1.1321 |
| `a:ST_EffectContainerType` | restriction of xsd:token | `sib`, `tree` | §20.1.10.22 p.2935 |
| `a:ST_FOVAngle` | restriction of a:ST_Angle | minInclusive `0`, maxInclusive `10800000` | §20.1.10.26 p.2936 |
| `a:ST_FixedAngle` | restriction of a:ST_Angle | minExclusive `-5400000`, maxExclusive `5400000` | §20.1.10.23 p.2935 |
| `a:ST_FixedPercentage` | union of a:ST_FixedPercentageDecimal, s:ST_FixedPercentage |  | §20.1.10.24 p.2936, OI 2.1.1322 |
| `a:ST_FixedPercentageDecimal` | restriction of a:ST_PercentageDecimal | minInclusive `-100000`, maxInclusive `100000` | Part 4 §17.2.2.7 p.273 |
| `a:ST_FontCollectionIndex` | restriction of xsd:token | `major`, `minor`, `none` | §20.1.10.25 p.2936 |
| `a:ST_GeomGuideFormula` | restriction of xsd:string |  | §20.1.10.27 p.2936 |
| `a:ST_GeomGuideName` | restriction of xsd:token |  | §20.1.10.28 p.2937 |
| `a:ST_LightRigDirection` | restriction of xsd:token | `tl`, `t`, `tr`, `l`, `r`, `bl`, `b`, `br` | §20.1.10.29 p.2937, OI 2.1.1323 |
| `a:ST_LightRigType` | restriction of xsd:token | `legacyFlat1`, `legacyFlat2`, `legacyFlat3`, `legacyFlat4`, `legacyNormal1`, `legacyNormal2`, `legacyNormal3`, `legacyNormal4`, `legacyHarsh1`, `legacyHarsh2`, `legacyHarsh3`, `legacyHarsh4` … (+15) | §20.1.10.30 p.2941, OI 2.1.1324 |
| `a:ST_LineCap` | restriction of xsd:token | `rnd`, `sq`, `flat` | §20.1.10.31 p.2950 |
| `a:ST_LineEndLength` | restriction of xsd:token | `sm`, `med`, `lg` | §20.1.10.32 p.2950, OI 2.1.1325 |
| `a:ST_LineEndType` | restriction of xsd:token | `none`, `triangle`, `stealth`, `diamond`, `oval`, `arrow` | §20.1.10.33 p.2951 |
| `a:ST_LineEndWidth` | restriction of xsd:token | `sm`, `med`, `lg` | §20.1.10.34 p.2951 |
| `a:ST_LineWidth` | restriction of a:ST_Coordinate32Unqualified | minInclusive `0`, maxInclusive `20116800` | §20.1.10.35 p.2952, OI 2.1.1326 |
| `a:ST_OnOffStyleType` | restriction of xsd:token | `on`, `off`, `def` | §20.1.10.36 p.2952 |
| `a:ST_PathFillMode` | restriction of xsd:token | `none`, `norm`, `lighten`, `lightenLess`, `darken`, `darkenLess` | §20.1.10.37 p.2953, OI 2.1.1327 |
| `a:ST_PathShadeType` | restriction of xsd:token | `shape`, `circle`, `rect` | §20.1.10.38 p.2953 |
| `a:ST_PenAlignment` | restriction of xsd:token | `ctr`, `in` | §20.1.10.39 p.2953, OI 2.1.1328 |
| `a:ST_Percentage` | union of a:ST_PercentageDecimal, s:ST_Percentage |  | §20.1.10.40 p.2954, OI 2.1.1329 |
| `a:ST_PercentageDecimal` | restriction of xsd:int |  | Part 4 §17.2.2.12 p.275 |
| `a:ST_PitchFamily` | restriction of xsd:byte | `00`, `01`, `02`, `16`, `17`, `18`, `32`, `33`, `34`, `48`, `49`, `50` … (+6) | §20.1.10.41 p.2954 |
| `a:ST_PositiveCoordinate` | restriction of xsd:long | minInclusive `0`, maxInclusive `27273042316900` | §20.1.10.42 p.2955, OI 2.1.1330 |
| `a:ST_PositiveCoordinate32` | restriction of a:ST_Coordinate32Unqualified | minInclusive `0` | §20.1.10.43 p.2955 |
| `a:ST_PositiveFixedAngle` | restriction of a:ST_Angle | minInclusive `0`, maxExclusive `21600000` | §20.1.10.44 p.2956 |
| `a:ST_PositiveFixedPercentage` | union of a:ST_PositiveFixedPercentageDecimal, s:ST_PositiveFixedPercentage |  | §20.1.10.45 p.2956 |
| `a:ST_PositiveFixedPercentageDecimal` | restriction of a:ST_PercentageDecimal | minInclusive `0`, maxInclusive `100000` | Part 4 §17.2.2.8 p.274 |
| `a:ST_PositivePercentage` | union of a:ST_PositivePercentageDecimal, s:ST_PositivePercentage |  | §20.1.10.46 p.2956, OI 2.1.1332 |
| `a:ST_PositivePercentageDecimal` | restriction of a:ST_PercentageDecimal | minInclusive `0` | Part 4 §17.2.2.9 p.274 |
| `a:ST_PresetCameraType` | restriction of xsd:token | `legacyObliqueTopLeft`, `legacyObliqueTop`, `legacyObliqueTopRight`, `legacyObliqueLeft`, `legacyObliqueFront`, `legacyObliqueRight`, `legacyObliqueBottomLeft`, `legacyObliqueBottom`, `legacyObliqueBottomRight`, `legacyPerspectiveTopLeft`, `legacyPerspectiveTop`, `legacyPerspectiveTopRight` … (+50) | §20.1.10.47 p.2956 |
| `a:ST_PresetColorVal` | restriction of xsd:token | `aliceBlue`, `antiqueWhite`, `aqua`, `aquamarine`, `azure`, `beige`, `bisque`, `black`, `blanchedAlmond`, `blue`, `blueViolet`, `brown` … (+178) | §20.1.10.48 p.2978, OI 2.1.1333 |
| `a:ST_PresetLineDashVal` | restriction of xsd:token | `solid`, `dot`, `dash`, `lgDash`, `dashDot`, `lgDashDot`, `lgDashDotDot`, `sysDash`, `sysDot`, `sysDashDot`, `sysDashDotDot` | §20.1.10.49 p.2984 |
| `a:ST_PresetMaterialType` | restriction of xsd:token | `legacyMatte`, `legacyPlastic`, `legacyMetal`, `legacyWireframe`, `matte`, `plastic`, `metal`, `warmMatte`, `translucentPowder`, `powder`, `dkEdge`, `softEdge` … (+3) | §20.1.10.50 p.2984, OI 2.1.1334 |
| `a:ST_PresetPatternVal` | restriction of xsd:token | `pct5`, `pct10`, `pct20`, `pct25`, `pct30`, `pct40`, `pct50`, `pct60`, `pct70`, `pct75`, `pct80`, `pct90` … (+42) | §20.1.10.51 p.2993 |
| `a:ST_PresetShadowVal` | restriction of xsd:token | `shdw1`, `shdw2`, `shdw3`, `shdw4`, `shdw5`, `shdw6`, `shdw7`, `shdw8`, `shdw9`, `shdw10`, `shdw11`, `shdw12` … (+8) | §20.1.10.52 p.2996 |
| `a:ST_RectAlignment` | restriction of xsd:token | `tl`, `t`, `tr`, `l`, `ctr`, `r`, `bl`, `b`, `br` | §20.1.10.53 p.2999 |
| `a:ST_SchemeColorVal` | restriction of xsd:token | `bg1`, `tx1`, `bg2`, `tx2`, `accent1`, `accent2`, `accent3`, `accent4`, `accent5`, `accent6`, `hlink`, `folHlink` … (+5) | §20.1.10.54 p.3000 |
| `a:ST_ShapeID` | restriction of xsd:token |  | §20.1.10.55 p.3000, OI 2.1.1335 |
| `a:ST_ShapeType` | restriction of xsd:token | `line`, `lineInv`, `triangle`, `rtTriangle`, `rect`, `diamond`, `parallelogram`, `trapezoid`, `nonIsoscelesTrapezoid`, `pentagon`, `hexagon`, `heptagon` … (+175) | §20.1.10.56 p.3001, OI 2.1.1336 |
| `a:ST_StyleMatrixColumnIndex` | restriction of xsd:unsignedInt |  | §20.1.10.57 p.3069 |
| `a:ST_SystemColorVal` | restriction of xsd:token | `scrollBar`, `background`, `activeCaption`, `inactiveCaption`, `menu`, `window`, `windowFrame`, `menuText`, `windowText`, `captionText`, `activeBorder`, `inactiveBorder` … (+18) | §20.1.10.58 p.3070 |
| `a:ST_TextAlignType` | restriction of xsd:token | `l`, `ctr`, `r`, `just`, `justLow`, `dist`, `thaiDist` | §20.1.10.59 p.3071 |
| `a:ST_TextAnchoringType` | restriction of xsd:token | `t`, `ctr`, `b`, `just`, `dist` | §20.1.10.60 p.3072, OI 2.1.1337 |
| `a:ST_TextAutonumberScheme` | restriction of xsd:token | `alphaLcParenBoth`, `alphaUcParenBoth`, `alphaLcParenR`, `alphaUcParenR`, `alphaLcPeriod`, `alphaUcPeriod`, `arabicParenBoth`, `arabicParenR`, `arabicPeriod`, `arabicPlain`, `romanLcParenBoth`, `romanUcParenBoth` … (+29) | §20.1.10.61 p.3073, OI 2.1.1338 |
| `a:ST_TextBulletSize` | union of a:ST_TextBulletSizePercent, a:ST_TextBulletSizeDecimal |  | §20.1.10.87 p.3099 |
| `a:ST_TextBulletSizeDecimal` | restriction of a:ST_PercentageDecimal | minInclusive `25000`, maxInclusive `400000` | Part 4 §17.2.2.14 p.275 |
| `a:ST_TextBulletSizePercent` | restriction of xsd:string | pattern `0*((2[5-9])\|([3-9][0-9])\|([1-3][0-9][0-9])\|400)%` | §20.1.10.62 p.3075 |
| `a:ST_TextBulletStartAtNum` | restriction of xsd:int | minInclusive `1`, maxInclusive `32767` | §20.1.10.63 p.3075 |
| `a:ST_TextCapsType` | restriction of xsd:token | `none`, `small`, `all` | §20.1.10.64 p.3076, OI 2.1.1339 |
| `a:ST_TextColumnCount` | restriction of xsd:int | minInclusive `1`, maxInclusive `16` | §20.1.10.65 p.3076 |
| `a:ST_TextFontAlignType` | restriction of xsd:token | `auto`, `t`, `ctr`, `base`, `b` | §20.1.10.66 p.3076 |
| `a:ST_TextFontScalePercent` | restriction of a:ST_PercentageDecimal | minInclusive `1000`, maxInclusive `100000` | Part 4 §17.2.2.10 p.274, OI 2.1.1787 |
| `a:ST_TextFontScalePercentOrPercentString` | union of a:ST_TextFontScalePercent, s:ST_Percentage |  | §20.1.10.67 p.3077, OI 2.1.1340 |
| `a:ST_TextFontSize` | restriction of xsd:int | minInclusive `100`, maxInclusive `400000` | §20.1.10.68 p.3077 |
| `a:ST_TextHorzOverflowType` | restriction of xsd:token | `overflow`, `clip` | §20.1.10.69 p.3077 |
| `a:ST_TextIndent` | restriction of a:ST_Coordinate32Unqualified | minInclusive `-51206400`, maxInclusive `51206400` | §20.1.10.70 p.3078 |
| `a:ST_TextIndentLevelType` | restriction of xsd:int | minInclusive `0`, maxInclusive `8` | §20.1.10.71 p.3078 |
| `a:ST_TextMargin` | restriction of a:ST_Coordinate32Unqualified | minInclusive `0`, maxInclusive `51206400` | §20.1.10.72 p.3078 |
| `a:ST_TextNonNegativePoint` | restriction of xsd:int | minInclusive `0`, maxInclusive `400000` | §20.1.10.73 p.3079, OI 2.1.1341 |
| `a:ST_TextPoint` | union of a:ST_TextPointUnqualified, s:ST_UniversalMeasure |  | §20.1.10.74 p.3079, OI 2.1.1342 |
| `a:ST_TextPointUnqualified` | restriction of xsd:int | minInclusive `-400000`, maxInclusive `400000` | §20.1.10.75 p.3079 |
| `a:ST_TextShapeType` | restriction of xsd:token | `textNoShape`, `textPlain`, `textStop`, `textTriangle`, `textTriangleInverted`, `textChevron`, `textChevronInverted`, `textRingInside`, `textRingOutside`, `textArchUp`, `textArchDown`, `textCircle` … (+29) | §20.1.10.76 p.3080, OI 2.1.1343 |
| `a:ST_TextSpacingPercent` | restriction of a:ST_PercentageDecimal | minInclusive `0`, maxInclusive `13200000` | Part 4 §17.2.2.11 p.275, OI 2.1.1788 |
| `a:ST_TextSpacingPercentOrPercentString` | union of a:ST_TextSpacingPercent, s:ST_Percentage |  | §20.1.10.77 p.3094, OI 2.1.1344 |
| `a:ST_TextSpacingPoint` | restriction of xsd:int | minInclusive `0`, maxInclusive `158400` | §20.1.10.78 p.3094, OI 2.1.1345 |
| `a:ST_TextStrikeType` | restriction of xsd:token | `noStrike`, `sngStrike`, `dblStrike` | §20.1.10.79 p.3094 |
| `a:ST_TextTabAlignType` | restriction of xsd:token | `l`, `ctr`, `r`, `dec` | §20.1.10.80 p.3094 |
| `a:ST_TextTypeface` | restriction of xsd:string |  | §20.1.10.81 p.3095 |
| `a:ST_TextUnderlineType` | restriction of xsd:token | `none`, `words`, `sng`, `dbl`, `heavy`, `dotted`, `dottedHeavy`, `dash`, `dashHeavy`, `dashLong`, `dashLongHeavy`, `dotDash` … (+6) | §20.1.10.82 p.3095, OI 2.1.1346 |
| `a:ST_TextVertOverflowType` | restriction of xsd:token | `overflow`, `ellipsis`, `clip` | §20.1.10.84 p.3097 |
| `a:ST_TextVerticalType` | restriction of xsd:token | `horz`, `vert`, `vert270`, `wordArtVert`, `eaVert`, `mongolianVert`, `wordArtVertRtl` | §20.1.10.83 p.3096 |
| `a:ST_TextWrappingType` | restriction of xsd:token | `none`, `square` | §20.1.10.85 p.3097, OI 2.1.1347 |
| `a:ST_TileFlipMode` | restriction of xsd:token | `none`, `x`, `y`, `xy` | §20.1.10.86 p.3098 |

## Element details

### a:accent1

- Type: `a:CT_Color`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:clrScheme`
- Specification: §20.1.4.1.1 p.2793, OI 2.1.1248

### a:accent2

- Type: `a:CT_Color`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:clrScheme`
- Specification: §20.1.4.1.2 p.2794, OI 2.1.1249

### a:accent3

- Type: `a:CT_Color`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:clrScheme`
- Specification: §20.1.4.1.3 p.2795, OI 2.1.1250

### a:accent4

- Type: `a:CT_Color`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:clrScheme`
- Specification: §20.1.4.1.4 p.2795, OI 2.1.1251

### a:accent5

- Type: `a:CT_Color`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:clrScheme`
- Specification: §20.1.4.1.5 p.2796, OI 2.1.1252

### a:accent6

- Type: `a:CT_Color`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:clrScheme`
- Specification: §20.1.4.1.6 p.2796, OI 2.1.1253

### a:ahLst

- Type: `a:CT_AdjustHandleList`
- Children: `a:ahXY`, `a:ahPolar`
- Parents: `a:custGeom`
- Specification: §20.1.9.1 p.2897

### a:ahPolar

- Type: `a:CT_PolarAdjustHandle`
- Attributes: `gdRefAng`, `gdRefR`, `maxAng`, `maxR`, `minAng`, `minR`
- Children: `a:pos`
- Parents: `a:ahLst`
- Specification: §20.1.9.2 p.2897, OI 2.1.1309

### a:ahXY

- Type: `a:CT_XYAdjustHandle`
- Attributes: `gdRefX`, `gdRefY`, `maxX`, `maxY`, `minX`, `minY`
- Children: `a:pos`
- Parents: `a:ahLst`
- Specification: §20.1.9.3 p.2898, OI 2.1.1310

### a:alpha

- Type: `a:CT_PositiveFixedPercentage`
- Attributes: `val`!
- Parents: `a:hslClr`, `a:prstClr`, `a:schemeClr`, `a:scrgbClr`, `a:srgbClr`, `a:sysClr`, `cs:styleClr`, `cs:variation`
- Specification: §20.1.2.3.1 p.2766, OI 2.1.1218

### a:alphaBiLevel

- Type: `a:CT_AlphaBiLevelEffect`
- Attributes: `thresh`!
- Parents: `a:blip`, `a:cont`, `a:effectDag`, `am3d:blip`
- Specification: §20.1.8.1 p.2862

### a:alphaCeiling

- Type: `a:CT_AlphaCeilingEffect`
- Parents: `a:blip`, `a:cont`, `a:effectDag`, `am3d:blip`
- Specification: §20.1.8.2 p.2862

### a:alphaFloor

- Type: `a:CT_AlphaFloorEffect`
- Parents: `a:blip`, `a:cont`, `a:effectDag`, `am3d:blip`
- Specification: §20.1.8.3 p.2862

### a:alphaInv

- Type: `a:CT_AlphaInverseEffect`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:blip`, `a:cont`, `a:effectDag`, `am3d:blip`
- Specification: §20.1.8.4 p.2863, OI 2.1.1285

### a:alphaMod

- Type: `a:CT_AlphaModulateEffect`, `a:CT_PositivePercentage`
- Attributes: `val`!
- Children: `a:cont`
- Parents: `a:blip`, `a:cont`, `a:effectDag`, `a:hslClr`, `a:prstClr`, `a:schemeClr`, `a:scrgbClr`, `a:srgbClr`, `a:sysClr`, `am3d:blip`, `cs:styleClr`, `cs:variation`
- Specification: §20.1.2.3.2 p.2767, OI 2.1.1219; §20.1.8.5 p.2863, OI 2.1.1286

### a:alphaModFix

- Type: `a:CT_AlphaModulateFixedEffect`
- Attributes: `amt`
- Parents: `a:blip`, `a:cont`, `a:effectDag`, `am3d:blip`
- Specification: §20.1.8.6 p.2863, OI 2.1.1287

### a:alphaOff

- Type: `a:CT_FixedPercentage`
- Attributes: `val`!
- Parents: `a:hslClr`, `a:prstClr`, `a:schemeClr`, `a:scrgbClr`, `a:srgbClr`, `a:sysClr`, `cs:styleClr`, `cs:variation`
- Specification: §20.1.2.3.3 p.2768, OI 2.1.1220

### a:alphaOutset

- Type: `a:CT_AlphaOutsetEffect`
- Attributes: `rad`
- Parents: `a:cont`, `a:effectDag`
- Specification: §20.1.8.7 p.2863

### a:alphaRepl

- Type: `a:CT_AlphaReplaceEffect`
- Attributes: `a`!
- Parents: `a:blip`, `a:cont`, `a:effectDag`, `am3d:blip`
- Specification: §20.1.8.8 p.2864

### a:anchor

- Type: `a:CT_Point3D`
- Attributes: `x`!, `y`!, `z`!
- Parents: `a:backdrop`
- Specification: §20.1.5.1 p.2833, OI 2.1.1266

### a:arcTo

- Type: `a:CT_Path2DArcTo`
- Attributes: `hR`!, `stAng`!, `swAng`!, `wR`!
- Parents: `a:path`
- Specification: §20.1.9.4 p.2899

### a:audioCd

- Type: `a:CT_AudioCD`
- Children: `a:st`, `a:end`, `a:extLst`
- Parents: `p14:nvPr`, `p:nvPr`
- Specification: §20.1.3.1 p.2786

### a:audioFile

- Type: `a:CT_AudioFile`
- Attributes: `contentType`, `r:link`!
- Children: `a:extLst`
- Parents: `p14:nvPr`, `p:nvPr`
- Specification: §20.1.3.2 p.2787, OI 2.1.1244

### a:avLst

- Type: `a:CT_GeomGuideList`
- Children: `a:gd`
- Parents: `a:custGeom`, `a:prstGeom`, `a:prstTxWarp`
- Specification: §20.1.9.5 p.2901

### a:backdrop

- Type: `a:CT_Backdrop`
- Children: `a:anchor`, `a:norm`, `a:up`, `a:extLst`
- Parents: `a14:hiddenScene3d`, `a:scene3d`, `dgm:scene3d`
- Specification: §20.1.5.2 p.2834, OI 2.1.1267

### a:band1H

- Type: `a:CT_TablePartStyle`
- Children: `a:tcTxStyle`, `a:tcStyle`
- Parents: `a:tableStyle`, `a:tblStyle`
- Specification: §20.1.4.2.1 p.2812

### a:band1V

- Type: `a:CT_TablePartStyle`
- Children: `a:tcTxStyle`, `a:tcStyle`
- Parents: `a:tableStyle`, `a:tblStyle`
- Specification: §20.1.4.2.2 p.2812, OI 2.1.1261

### a:band2H

- Type: `a:CT_TablePartStyle`
- Children: `a:tcTxStyle`, `a:tcStyle`
- Parents: `a:tableStyle`, `a:tblStyle`
- Specification: §20.1.4.2.3 p.2813

### a:band2V

- Type: `a:CT_TablePartStyle`
- Children: `a:tcTxStyle`, `a:tcStyle`
- Parents: `a:tableStyle`, `a:tblStyle`
- Specification: §20.1.4.2.4 p.2814, OI 2.1.1262

### a:bevel

- Type: `a:CT_LineJoinBevel`, `a:CT_Bevel`
- Attributes: `h`, `prst`, `w`
- Parents: `a14:hiddenLine`, `a:cell3D`, `a:ln`, `a:lnB`, `a:lnBlToTr`, `a:lnL`, `a:lnR`, `a:lnT`, `a:lnTlToBr`, `a:uLn`
- Specification: §20.1.4.2.5 p.2815; §20.1.8.9 p.2864

### a:bevelB

- Type: `a:CT_Bevel`
- Attributes: `h`, `prst`, `w`
- Parents: `a14:hiddenSp3d`, `a:sp3d`, `dgm:sp3d`
- Specification: §20.1.5.3 p.2835, OI 2.1.1268

### a:bevelT

- Type: `a:CT_Bevel`
- Attributes: `h`, `prst`, `w`
- Parents: `a14:hiddenSp3d`, `a:sp3d`, `dgm:sp3d`
- Specification: §20.1.5.4 p.2836, OI 2.1.1269

### a:bgClr

- Type: `a:CT_Color`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:pattFill`
- Specification: §20.1.8.10 p.2864

### a:bgFillStyleLst

- Type: `a:CT_BackgroundFillStyleList`
- Children: `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`
- Parents: `a:fmtScheme`
- Specification: §20.1.4.1.7 p.2797

### a:biLevel

- Type: `a:CT_BiLevelEffect`
- Attributes: `thresh`!
- Parents: `a:blip`, `a:cont`, `a:effectDag`, `am3d:blip`
- Specification: §20.1.8.11 p.2864

### a:bldChart

- Type: `a:CT_AnimationChartBuildProperties`
- Attributes: `animBg`, `bld`
- Parents: `p:bldSub`
- Specification: §20.1.2.2.1 p.2738, OI 2.1.1198

### a:bldDgm

- Type: `a:CT_AnimationDgmBuildProperties`
- Attributes: `bld`, `rev`
- Parents: `p:bldSub`
- Specification: §20.1.2.2.2 p.2739, OI 2.1.1199

### a:blend

- Type: `a:CT_BlendEffect`
- Attributes: `blend`!
- Children: `a:cont`
- Parents: `a:cont`, `a:effectDag`
- Specification: §20.1.8.12 p.2865

### a:blip

- Type: `a:CT_Blip`
- Attributes: `cstate`, `r:embed`, `r:link`
- Children: `a:alphaBiLevel`, `a:alphaCeiling`, `a:alphaFloor`, `a:alphaInv`, `a:alphaMod`, `a:alphaModFix`, `a:alphaRepl`, `a:biLevel`, `a:blur`, `a:clrChange`, `a:clrRepl`, `a:duotone`, `a:fillOverlay`, `a:grayscl`, `a:hsl`, `a:lum`, `a:tint`, `a:extLst`
- Parents: `a:blipFill`, `a:buBlip`, `cdr:blipFill`, `p166:blipFill`, `p:blipFill`, `pic:blipFill`, `xdr:blipFill`
- Specification: §20.1.8.13 p.2865

### a:blipFill

- Type: `a:CT_BlipFillProperties`
- Attributes: `dpi`, `rotWithShape`
- Children: `a:blip`, `a:srcRect`, `a:tile`, `a:stretch`
- Parents: `a14:hiddenFill`, `a:bgFillStyleLst`, `a:defRPr`, `a:endParaRPr`, `a:fill`, `a:fillOverlay`, `a:fillStyleLst`, `a:grpSpPr`, `a:pic`, `a:rPr`, `a:spPr`, `a:tblPr`, `a:tcPr`, `a:uFill`, `am3d:spPr`, `c14:spPr`, `c15:spPr`, `c16:spPr`, `c:spPr`, `cdr:grpSpPr`, `cdr:spPr`, `cs:defRPr`, `cs:spPr`, `cx:spPr`, `dgm1612:spPr`, `dgm:bg`, `dgm:spPr`, `dsp:grpSpPr`, `dsp:spPr`, `p166:spPr`, `p:bgPr`, `p:grpSpPr`, `p:spPr`, `pic:spPr`, `wp:bg`, `wp:grpSpPr`, `wp:spPr`, `wpc:bg`, `wpg:grpSpPr`, `wps:spPr`, `xdr:grpSpPr`, `xdr:spPr`
- Specification: §20.1.8.14 p.2866, OI 2.1.1288

### a:blue

- Type: `a:CT_Percentage`
- Attributes: `val`!
- Parents: `a:hslClr`, `a:prstClr`, `a:schemeClr`, `a:scrgbClr`, `a:srgbClr`, `a:sysClr`, `cs:styleClr`, `cs:variation`
- Specification: §20.1.2.3.4 p.2768, OI 2.1.1221

### a:blueMod

- Type: `a:CT_Percentage`
- Attributes: `val`!
- Parents: `a:hslClr`, `a:prstClr`, `a:schemeClr`, `a:scrgbClr`, `a:srgbClr`, `a:sysClr`, `cs:styleClr`, `cs:variation`
- Specification: §20.1.2.3.5 p.2769, OI 2.1.1222

### a:blueOff

- Type: `a:CT_Percentage`
- Attributes: `val`!
- Parents: `a:hslClr`, `a:prstClr`, `a:schemeClr`, `a:scrgbClr`, `a:srgbClr`, `a:sysClr`, `cs:styleClr`, `cs:variation`
- Specification: §20.1.2.3.6 p.2769, OI 2.1.1223

### a:blur

- Type: `a:CT_BlurEffect`
- Attributes: `grow`, `rad`
- Parents: `a:blip`, `a:cont`, `a:effectDag`, `a:effectLst`, `am3d:blip`
- Specification: §20.1.8.15 p.2868, OI 2.1.1289

### a:bodyPr

- Type: `a:CT_TextBodyProperties`
- Attributes: `anchor`, `anchorCtr`, `bIns`, `compatLnSpc`, `forceAA`, `fromWordArt`, `horzOverflow`, `lIns`, `numCol`, `rIns`, `rot`, `rtlCol`, `spcCol`, `spcFirstLastPara`, `tIns`, `upright`, `vert`, `vertOverflow`, `wrap`
- Children: `a:prstTxWarp`, `a:noAutofit`, `a:normAutofit`, `a:spAutoFit`, `a:scene3d`, `a:sp3d`, `a:flatTx`, `a:extLst`
- Parents: `a:lnDef`, `a:spDef`, `a:txBody`, `a:txDef`, `c:rich`, `c:txPr`, `cdr:txBody`, `cx:rich`, `cx:txPr`, `dgm:t`, `dsp:txBody`, `p188:txBody`, `p:txBody`, `xdr:txBody`
- Specification: §21.1.2.1.1 p.3197, OI 2.1.1379

### a:bottom

- Type: `a:CT_ThemeableLineStyle`
- Children: `a:ln`, `a:lnRef`
- Parents: `a:tcBdr`
- Specification: §20.1.4.2.6 p.2816

### a:br

- Type: `a:CT_TextLineBreak`
- Children: `a:rPr`
- Parents: `a:p`
- Specification: §21.1.2.2.1 p.3209

### a:buAutoNum

- Type: `a:CT_TextAutonumberBullet`
- Attributes: `startAt`, `type`!
- Parents: `a:defPPr`, `a:lvl1pPr`, `a:lvl2pPr`, `a:lvl3pPr`, `a:lvl4pPr`, `a:lvl5pPr`, `a:lvl6pPr`, `a:lvl7pPr`, `a:lvl8pPr`, `a:lvl9pPr`, `a:pPr`, `dgm1611:buPr`
- Specification: §21.1.2.4.1 p.3268

### a:buBlip

- Type: `a:CT_TextBlipBullet`
- Children: `a:blip`
- Parents: `a:defPPr`, `a:lvl1pPr`, `a:lvl2pPr`, `a:lvl3pPr`, `a:lvl4pPr`, `a:lvl5pPr`, `a:lvl6pPr`, `a:lvl7pPr`, `a:lvl8pPr`, `a:lvl9pPr`, `a:pPr`, `dgm1611:buPr`
- Specification: §21.1.2.4.2 p.3269

### a:buChar

- Type: `a:CT_TextCharBullet`
- Attributes: `char`!
- Parents: `a:defPPr`, `a:lvl1pPr`, `a:lvl2pPr`, `a:lvl3pPr`, `a:lvl4pPr`, `a:lvl5pPr`, `a:lvl6pPr`, `a:lvl7pPr`, `a:lvl8pPr`, `a:lvl9pPr`, `a:pPr`, `dgm1611:buPr`
- Specification: §21.1.2.4.3 p.3271

### a:buClr

- Type: `a:CT_Color`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:defPPr`, `a:lvl1pPr`, `a:lvl2pPr`, `a:lvl3pPr`, `a:lvl4pPr`, `a:lvl5pPr`, `a:lvl6pPr`, `a:lvl7pPr`, `a:lvl8pPr`, `a:lvl9pPr`, `a:pPr`
- Specification: §21.1.2.4.4 p.3272

### a:buClrTx

- Type: `a:CT_TextBulletColorFollowText`
- Parents: `a:defPPr`, `a:lvl1pPr`, `a:lvl2pPr`, `a:lvl3pPr`, `a:lvl4pPr`, `a:lvl5pPr`, `a:lvl6pPr`, `a:lvl7pPr`, `a:lvl8pPr`, `a:lvl9pPr`, `a:pPr`
- Specification: §21.1.2.4.5 p.3273

### a:buFont

- Type: `a:CT_TextFont`
- Attributes: `charset`, `panose`, `pitchFamily`, `typeface`!
- Parents: `a:defPPr`, `a:lvl1pPr`, `a:lvl2pPr`, `a:lvl3pPr`, `a:lvl4pPr`, `a:lvl5pPr`, `a:lvl6pPr`, `a:lvl7pPr`, `a:lvl8pPr`, `a:lvl9pPr`, `a:pPr`
- Specification: §21.1.2.4.6 p.3274, OI 2.1.1403

### a:buFontTx

- Type: `a:CT_TextBulletTypefaceFollowText`
- Parents: `a:defPPr`, `a:lvl1pPr`, `a:lvl2pPr`, `a:lvl3pPr`, `a:lvl4pPr`, `a:lvl5pPr`, `a:lvl6pPr`, `a:lvl7pPr`, `a:lvl8pPr`, `a:lvl9pPr`, `a:pPr`
- Specification: §21.1.2.4.7 p.3276

### a:buNone

- Type: `a:CT_TextNoBullet`
- Parents: `a:defPPr`, `a:lvl1pPr`, `a:lvl2pPr`, `a:lvl3pPr`, `a:lvl4pPr`, `a:lvl5pPr`, `a:lvl6pPr`, `a:lvl7pPr`, `a:lvl8pPr`, `a:lvl9pPr`, `a:pPr`, `dgm1611:buPr`
- Specification: §21.1.2.4.8 p.3276

### a:buSzPct

- Type: `a:CT_TextBulletSizePercent`
- Attributes: `val`!
- Parents: `a:defPPr`, `a:lvl1pPr`, `a:lvl2pPr`, `a:lvl3pPr`, `a:lvl4pPr`, `a:lvl5pPr`, `a:lvl6pPr`, `a:lvl7pPr`, `a:lvl8pPr`, `a:lvl9pPr`, `a:pPr`
- Specification: §21.1.2.4.9 p.3277, OI 2.1.1404

### a:buSzPts

- Type: `a:CT_TextBulletSizePoint`
- Attributes: `val`!
- Parents: `a:defPPr`, `a:lvl1pPr`, `a:lvl2pPr`, `a:lvl3pPr`, `a:lvl4pPr`, `a:lvl5pPr`, `a:lvl6pPr`, `a:lvl7pPr`, `a:lvl8pPr`, `a:lvl9pPr`, `a:pPr`
- Specification: §21.1.2.4.10 p.3278, OI 2.1.1405

### a:buSzTx

- Type: `a:CT_TextBulletSizeFollowText`
- Parents: `a:defPPr`, `a:lvl1pPr`, `a:lvl2pPr`, `a:lvl3pPr`, `a:lvl4pPr`, `a:lvl5pPr`, `a:lvl6pPr`, `a:lvl7pPr`, `a:lvl8pPr`, `a:lvl9pPr`, `a:pPr`
- Specification: §21.1.2.4.11 p.3279

### a:cNvCxnSpPr

- Type: `a:CT_NonVisualConnectorProperties`
- Children: `a:cxnSpLocks`, `a:stCxn`, `a:endCxn`, `a:extLst`
- Parents: `a:nvCxnSpPr`
- Specification: §20.1.2.2.4 p.2740, OI 2.1.1200

### a:cNvGraphicFramePr

- Type: `a:CT_NonVisualGraphicFrameProperties`
- Children: `a:graphicFrameLocks`, `a:extLst`
- Parents: `a:nvGraphicFramePr`
- Specification: §20.1.2.2.5 p.2741, OI 2.1.1201

### a:cNvGrpSpPr

- Type: `a:CT_NonVisualGroupDrawingShapeProps`
- Children: `a:grpSpLocks`, `a:extLst`
- Parents: `a:nvGrpSpPr`
- Specification: §20.1.2.2.6 p.2741, OI 2.1.1202

### a:cNvPicPr

- Type: `a:CT_NonVisualPictureProperties`
- Attributes: `preferRelativeResize`
- Children: `a:picLocks`, `a:extLst`
- Parents: `a:nvPicPr`
- Specification: §20.1.2.2.7 p.2741

### a:cNvPr

- Type: `a:CT_NonVisualDrawingProps`
- Attributes: `descr`, `hidden`, `id`!, `name`!, `title`
- Children: `a:hlinkClick`, `a:hlinkHover`, `a:extLst`
- Parents: `a:nvCxnSpPr`, `a:nvGraphicFramePr`, `a:nvGrpSpPr`, `a:nvPicPr`, `a:nvSpPr`
- Specification: §20.1.2.2.8 p.2742

### a:cNvSpPr

- Type: `a:CT_NonVisualDrawingShapeProps`
- Attributes: `txBox`
- Children: `a:spLocks`, `a:extLst`
- Parents: `a:nvSpPr`
- Specification: §20.1.2.2.9 p.2744

### a:camera

- Type: `a:CT_Camera`
- Attributes: `fov`, `prst`!, `zoom`
- Children: `a:rot`
- Parents: `a14:hiddenScene3d`, `a:scene3d`, `dgm:scene3d`
- Specification: §20.1.5.5 p.2837, OI 2.1.1270

### a:cell3D

- Type: `a:CT_Cell3D`
- Attributes: `prstMaterial`
- Children: `a:bevel`, `a:lightRig`, `a:extLst`
- Parents: `a:tcPr`, `a:tcStyle`
- Specification: §21.1.3.1 p.3352, OI 2.1.1415

### a:chExt

- Type: `a:CT_PositiveSize2D`
- Attributes: `cx`!, `cy`!
- Parents: `a:xfrm`
- Specification: §20.1.7.1 p.2857, OI 2.1.1278

### a:chOff

- Type: `a:CT_Point2D`
- Attributes: `x`!, `y`!
- Parents: `a:xfrm`
- Specification: §20.1.7.2 p.2857, OI 2.1.1279

### a:chart

- Type: `a:CT_AnimationChartElement`
- Attributes: `bldStep`!, `categoryIdx`, `seriesIdx`
- Parents: `p:graphicEl`
- Specification: §20.1.2.2.3 p.2740

### a:close

- Type: `a:CT_Path2DClose`
- Parents: `a:path`
- Specification: §20.1.9.6 p.2902

### a:clrChange

- Type: `a:CT_ColorChangeEffect`
- Attributes: `useA`
- Children: `a:clrFrom`, `a:clrTo`
- Parents: `a:blip`, `a:cont`, `a:effectDag`, `am3d:blip`
- Specification: §20.1.8.16 p.2869, OI 2.1.1290

### a:clrFrom

- Type: `a:CT_Color`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:clrChange`
- Specification: §20.1.8.17 p.2870

### a:clrMap

- Type: `a:CT_ColorMapping`
- Attributes: `accent1`!, `accent2`!, `accent3`!, `accent4`!, `accent5`!, `accent6`!, `bg1`!, `bg2`!, `folHlink`!, `hlink`!, `tx1`!, `tx2`!
- Children: `a:extLst`
- Parents: `a:extraClrScheme`
- Specification: §20.1.6.1 p.2848

### a:clrRepl

- Type: `a:CT_ColorReplaceEffect`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:blip`, `a:cont`, `a:effectDag`, `am3d:blip`
- Specification: §20.1.8.18 p.2870

### a:clrScheme

- Type: `a:CT_ColorScheme`
- Attributes: `name`!
- Children: `a:dk1`, `a:lt1`, `a:dk2`, `a:lt2`, `a:accent1`, `a:accent2`, `a:accent3`, `a:accent4`, `a:accent5`, `a:accent6`, `a:hlink`, `a:folHlink`, `a:extLst`
- Parents: `a:extraClrScheme`, `a:themeElements`, `a:themeOverride`
- Specification: §20.1.6.2 p.2849

### a:clrTo

- Type: `a:CT_Color`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:clrChange`
- Specification: §20.1.8.19 p.2870

### a:comp

- Type: `a:CT_ComplementTransform`
- Parents: `a:hslClr`, `a:prstClr`, `a:schemeClr`, `a:scrgbClr`, `a:srgbClr`, `a:sysClr`, `cs:styleClr`, `cs:variation`
- Specification: §20.1.2.3.7 p.2770

### a:cont

- Type: `a:CT_EffectContainer`
- Attributes: `name`, `type`
- Children: `a:cont`, `a:effect`, `a:alphaBiLevel`, `a:alphaCeiling`, `a:alphaFloor`, `a:alphaInv`, `a:alphaMod`, `a:alphaModFix`, `a:alphaOutset`, `a:alphaRepl`, `a:biLevel`, `a:blend`, `a:blur`, `a:clrChange`, `a:clrRepl`, `a:duotone`, `a:fill`, `a:fillOverlay`, `a:glow`, `a:grayscl`, `a:hsl`, `a:innerShdw`, `a:lum`, `a:outerShdw`, `a:prstShdw`, `a:reflection`, `a:relOff`, `a:softEdge`, `a:tint`, `a:xfrm`
- Parents: `a:alphaMod`, `a:blend`, `a:cont`, `a:effectDag`
- Specification: §20.1.8.20 p.2870, OI 2.1.1291

### a:contourClr

- Type: `a:CT_Color`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a14:hiddenSp3d`, `a:sp3d`, `dgm:sp3d`
- Specification: §20.1.5.6 p.2839, OI 2.1.1271

### a:cpLocks

- Type: `a:CT_ContentPartLocking`
- Attributes: `noAdjustHandles`, `noChangeArrowheads`, `noChangeAspect`, `noChangeShapeType`, `noEditPoints`, `noGrp`, `noMove`, `noResize`, `noRot`, `noSelect`
- Children: `a:extLst`
- Parents: `wp:cNvContentPartPr`
- Specification: §20.1.2.2.43 p.2765

### a:cs

- Type: `a:CT_TextFont`
- Attributes: `charset`, `panose`, `pitchFamily`, `typeface`!
- Parents: `a:defRPr`, `a:endParaRPr`, `a:font`, `a:majorFont`, `a:minorFont`, `a:rPr`, `cs:defRPr`
- Specification: §21.1.2.3.1 p.3242, OI 2.1.1392

### a:cubicBezTo

- Type: `a:CT_Path2DCubicBezierTo`
- Children: `a:pt`
- Parents: `a:path`
- Specification: §20.1.9.7 p.2903

### a:custClr

- Type: `a:CT_CustomColor`
- Attributes: `name`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:custClrLst`
- Specification: §20.1.4.1.8 p.2798

### a:custClrLst

- Type: `a:CT_CustomColorList`
- Children: `a:custClr`
- Parents: `a:theme`
- Specification: §20.1.6.3 p.2851

### a:custDash

- Type: `a:CT_DashStopList`
- Children: `a:ds`
- Parents: `a14:hiddenLine`, `a:ln`, `a:lnB`, `a:lnBlToTr`, `a:lnL`, `a:lnR`, `a:lnT`, `a:lnTlToBr`, `a:uLn`
- Specification: §20.1.8.21 p.2870

### a:custGeom

- Type: `a:CT_CustomGeometry2D`
- Children: `a:avLst`, `a:gdLst`, `a:ahLst`, `a:cxnLst`, `a:rect`, `a:pathLst`
- Parents: `a:spPr`, `am3d:spPr`, `ask:lineSketchStyleProps`, `c14:spPr`, `c15:spPr`, `c16:spPr`, `c:spPr`, `cdr:spPr`, `cs:spPr`, `cx:spPr`, `dgm1612:spPr`, `dgm:spPr`, `dsp:spPr`, `p166:spPr`, `p:spPr`, `pic:spPr`, `wp:spPr`, `wps:spPr`, `xdr:spPr`
- Specification: §20.1.9.8 p.2904

### a:cxn

- Type: `a:CT_ConnectionSite`
- Attributes: `ang`!
- Children: `a:pos`
- Parents: `a:cxnLst`
- Specification: §20.1.9.9 p.2905

### a:cxnLst

- Type: `a:CT_ConnectionSiteList`
- Children: `a:cxn`
- Parents: `a:custGeom`
- Specification: §20.1.9.10 p.2907

### a:cxnSp

- Type: `a:CT_GvmlConnector`
- Children: `a:nvCxnSpPr`, `a:spPr`, `a:style`, `a:extLst`
- Parents: `a:grpSp`, `lc:lockedCanvas`
- Specification: §20.1.2.2.10 p.2745

### a:cxnSpLocks

- Type: `a:CT_ConnectorLocking`
- Attributes: `noAdjustHandles`, `noChangeArrowheads`, `noChangeAspect`, `noChangeShapeType`, `noEditPoints`, `noGrp`, `noMove`, `noResize`, `noRot`, `noSelect`
- Children: `a:extLst`
- Parents: `a:cNvCxnSpPr`, `cdr:cNvCxnSpPr`, `p:cNvCxnSpPr`, `wp:cNvCnPr`, `wps:cNvCnPr`, `xdr:cNvCxnSpPr`
- Specification: §20.1.2.2.11 p.2746, OI 2.1.1203

### a:defPPr

- Type: `a:CT_TextParagraphProperties`
- Attributes: `algn`, `defTabSz`, `eaLnBrk`, `fontAlgn`, `hangingPunct`, `indent`, `latinLnBrk`, `lvl`, `marL`, `marR`, `rtl`
- Children: `a:lnSpc`, `a:spcBef`, `a:spcAft`, `a:buClrTx`, `a:buClr`, `a:buSzTx`, `a:buSzPct`, `a:buSzPts`, `a:buFontTx`, `a:buFont`, `a:buNone`, `a:buAutoNum`, `a:buChar`, `a:buBlip`, `a:tabLst`, `a:defRPr`, `a:extLst`
- Parents: `a:lstStyle`, `dgm1612:lstStyle`, `p:bodyStyle`, `p:defaultTextStyle`, `p:notesStyle`, `p:otherStyle`, `p:titleStyle`
- Specification: §21.1.2.2.2 p.3210, OI 2.1.1383

### a:defRPr

- Type: `a:CT_TextCharacterProperties`
- Attributes: `altLang`, `b`, `baseline`, `bmk`, `cap`, `dirty`, `err`, `i`, `kern`, `kumimoji`, `lang`, `noProof`, `normalizeH`, `smtClean`, `smtId`, `spc`, `strike`, `sz`, `u`
- Children: `a:ln`, `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`, `a:effectLst`, `a:effectDag`, `a:highlight`, `a:uLnTx`, `a:uLn`, `a:uFillTx`, `a:uFill`, `a:latin`, `a:ea`, `a:cs`, `a:sym`, `a:hlinkClick`, `a:hlinkMouseOver`, `a:rtl`, `a:extLst`
- Parents: `a:defPPr`, `a:lvl1pPr`, `a:lvl2pPr`, `a:lvl3pPr`, `a:lvl4pPr`, `a:lvl5pPr`, `a:lvl6pPr`, `a:lvl7pPr`, `a:lvl8pPr`, `a:lvl9pPr`, `a:pPr`
- Specification: §21.1.2.3.2 p.3244, OI 2.1.1393

### a:dgm

- Type: `a:CT_AnimationDgmElement`
- Attributes: `bldStep`, `id`
- Parents: `p:graphicEl`
- Specification: §20.1.2.2.12 p.2748, OI 2.1.1204

### a:dk1

- Type: `a:CT_Color`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:clrScheme`
- Specification: §20.1.4.1.9 p.2798, OI 2.1.1254

### a:dk2

- Type: `a:CT_Color`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:clrScheme`
- Specification: §20.1.4.1.10 p.2798, OI 2.1.1255

### a:ds

- Type: `a:CT_DashStop`
- Attributes: `d`!, `sp`!
- Parents: `a:custDash`
- Specification: §20.1.8.22 p.2871, OI 2.1.1292

### a:duotone

- Type: `a:CT_DuotoneEffect`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:blip`, `a:cont`, `a:effectDag`, `am3d:blip`
- Specification: §20.1.8.23 p.2871, OI 2.1.1293

### a:ea

- Type: `a:CT_TextFont`
- Attributes: `charset`, `panose`, `pitchFamily`, `typeface`!
- Parents: `a:defRPr`, `a:endParaRPr`, `a:font`, `a:majorFont`, `a:minorFont`, `a:rPr`, `cs:defRPr`
- Specification: §21.1.2.3.3 p.3249, OI 2.1.1394

### a:effect

- Type: `a:CT_EffectReference`, `a:CT_EffectProperties`
- Attributes: `ref`!
- Children: `a:effectLst`, `a:effectDag`
- Parents: `a:cont`, `a:effectDag`, `a:tblBg`
- Specification: §20.1.4.2.7 p.2816; §20.1.8.24 p.2871, OI 2.1.1294

### a:effectDag

- Type: `a:CT_EffectContainer`
- Attributes: `name`, `type`
- Children: `a:cont`, `a:effect`, `a:alphaBiLevel`, `a:alphaCeiling`, `a:alphaFloor`, `a:alphaInv`, `a:alphaMod`, `a:alphaModFix`, `a:alphaOutset`, `a:alphaRepl`, `a:biLevel`, `a:blend`, `a:blur`, `a:clrChange`, `a:clrRepl`, `a:duotone`, `a:fill`, `a:fillOverlay`, `a:glow`, `a:grayscl`, `a:hsl`, `a:innerShdw`, `a:lum`, `a:outerShdw`, `a:prstShdw`, `a:reflection`, `a:relOff`, `a:softEdge`, `a:tint`, `a:xfrm`
- Parents: `a14:hiddenEffects`, `a:defRPr`, `a:effect`, `a:effectStyle`, `a:endParaRPr`, `a:grpSpPr`, `a:rPr`, `a:spPr`, `a:tblPr`, `am3d:spPr`, `c14:spPr`, `c15:spPr`, `c16:spPr`, `c:spPr`, `cdr:grpSpPr`, `cdr:spPr`, `cs:defRPr`, `cs:spPr`, `cx:spPr`, `dgm1612:spPr`, `dgm:bg`, `dgm:spPr`, `dgm:whole`, `dsp:grpSpPr`, `dsp:spPr`, `p166:spPr`, `p:bgPr`, `p:grpSpPr`, `p:spPr`, `pic:spPr`, `wp:bg`, `wp:grpSpPr`, `wp:spPr`, `wp:whole`, `wpc:bg`, `wpc:whole`, `wpg:grpSpPr`, `wps:spPr`, `xdr:grpSpPr`, `xdr:spPr`
- Specification: §20.1.8.25 p.2872, OI 2.1.1295

### a:effectLst

- Type: `a:CT_EffectList`
- Children: `a:blur`, `a:fillOverlay`, `a:glow`, `a:innerShdw`, `a:outerShdw`, `a:prstShdw`, `a:reflection`, `a:softEdge`
- Parents: `a14:hiddenEffects`, `a:defRPr`, `a:effect`, `a:effectStyle`, `a:endParaRPr`, `a:grpSpPr`, `a:rPr`, `a:spPr`, `a:tblPr`, `am3d:spPr`, `c14:spPr`, `c15:spPr`, `c16:spPr`, `c:spPr`, `cdr:grpSpPr`, `cdr:spPr`, `cs:defRPr`, `cs:spPr`, `cx:spPr`, `dgm1612:spPr`, `dgm:bg`, `dgm:spPr`, `dgm:whole`, `dsp:grpSpPr`, `dsp:spPr`, `p166:spPr`, `p:bgPr`, `p:grpSpPr`, `p:spPr`, `pic:spPr`, `wp:bg`, `wp:grpSpPr`, `wp:spPr`, `wp:whole`, `wpc:bg`, `wpc:whole`, `wpg:grpSpPr`, `wps:spPr`, `xdr:grpSpPr`, `xdr:spPr`
- Specification: §20.1.8.26 p.2872

### a:effectRef

- Type: `a:CT_StyleMatrixReference`
- Attributes: `idx`!
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:style`, `a:tblBg`, `cdr:style`, `dgm:style`, `dsp:style`, `p:style`, `pic14:style`, `wp:style`, `wps:style`, `xdr:style`
- Specification: §20.1.4.2.8 p.2817

### a:effectStyle

- Type: `a:CT_EffectStyleItem`
- Children: `a:effectLst`, `a:effectDag`, `a:scene3d`, `a:sp3d`
- Parents: `a:effectStyleLst`
- Specification: §20.1.4.1.11 p.2799

### a:effectStyleLst

- Type: `a:CT_EffectStyleList`
- Children: `a:effectStyle`
- Parents: `a:fmtScheme`
- Specification: §20.1.4.1.12 p.2800

### a:end

- Type: `a:CT_AudioCDTime`
- Attributes: `time`, `track`!
- Parents: `a:audioCd`
- Specification: §20.1.3.3 p.2788, OI 2.1.1245

### a:endCxn

- Type: `a:CT_Connection`
- Attributes: `id`!, `idx`!
- Parents: `a:cNvCxnSpPr`, `cdr:cNvCxnSpPr`, `p:cNvCxnSpPr`, `wp:cNvCnPr`, `wps:cNvCnPr`, `xdr:cNvCxnSpPr`
- Specification: §20.1.2.2.13 p.2748, OI 2.1.1205

### a:endParaRPr

- Type: `a:CT_TextCharacterProperties`
- Attributes: `altLang`, `b`, `baseline`, `bmk`, `cap`, `dirty`, `err`, `i`, `kern`, `kumimoji`, `lang`, `noProof`, `normalizeH`, `smtClean`, `smtId`, `spc`, `strike`, `sz`, `u`
- Children: `a:ln`, `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`, `a:effectLst`, `a:effectDag`, `a:highlight`, `a:uLnTx`, `a:uLn`, `a:uFillTx`, `a:uFill`, `a:latin`, `a:ea`, `a:cs`, `a:sym`, `a:hlinkClick`, `a:hlinkMouseOver`, `a:rtl`, `a:extLst`
- Parents: `a:p`
- Specification: §21.1.2.2.3 p.3217, OI 2.1.1384

### a:ext

- Type: `a:CT_PositiveSize2D`, `a:CT_OfficeArtExtension`
- Attributes: `cx`!, `cy`!, `uri`!
- Children: `any(##any)`
- Parents: `a14:extLst`, `a14:xfrm`, `a3danim:extLst`, `a:extLst`, `a:xfrm`, `aanim:extLst`, `alf:extLst`, `am3d:extLst`, `ask:extLst`, `asl:extLst`, `cdr14:extLst`, `cdr14:xfrm`, `cdr:xfrm`, `cs:extLst`, `dgm:extLst`, `dsp:extLst`, `dsp:txXfrm`, `oac:extLst`, `p14:xfrm`, `p:xfrm`, `pic14:extLst`, `thm15:extLst`, `w14:extLst`, `w14:xfrm`, `wp:extLst`, `wp:xfrm`, `wpc:extLst`, `wpg:extLst`, `wpg:xfrm`, `wps:extLst`, `xdr14:extLst`, `xdr14:xfrm`, `xdr:xfrm`
- Specification: §20.1.2.2.14 p.2748, OI 2.1.1206; §20.1.7.3 p.2858, OI 2.1.1280

### a:extLst

- Type: `a:CT_OfficeArtExtensionList`
- Children: `a:ext`
- Parents: `a14:cNvPr`, `a14:hiddenLine`, `a14:hiddenScene3d`, `a14:hiddenSp3d`, `a:audioCd`, `a:audioFile`, `a:backdrop`, `a:blip`, `a:bodyPr`, `a:cNvCxnSpPr`, `a:cNvGraphicFramePr`, `a:cNvGrpSpPr`, `a:cNvPicPr`, `a:cNvPr`, `a:cNvSpPr`, `a:cell3D`, `a:clrMap`, `a:clrScheme`, `a:cpLocks`, `a:cxnSp`, `a:cxnSpLocks`, `a:defPPr`, `a:defRPr`, `a:endParaRPr`, `a:font`, `a:fontScheme`, `a:graphicFrame`, `a:graphicFrameLocks`, `a:gridCol`, `a:grpSp`, `a:grpSpLocks`, `a:grpSpPr`, `a:hlinkClick`, `a:hlinkHover`, `a:hlinkMouseOver`, `a:ln`, `a:lnB`, `a:lnBlToTr`, `a:lnDef`, `a:lnL`, `a:lnR`, `a:lnT`, `a:lnTlToBr`, `a:lstStyle`, `a:lvl1pPr`, `a:lvl2pPr`, `a:lvl3pPr`, `a:lvl4pPr`, `a:lvl5pPr`, `a:lvl6pPr`, `a:lvl7pPr`, `a:lvl8pPr`, `a:lvl9pPr`, `a:majorFont`, `a:minorFont`, `a:objectDefaults`, `a:overrideClrMapping`, `a:pPr`, `a:pic`, `a:picLocks`, `a:quickTimeFile`, `a:rPr`, `a:scene3d`, `a:sp`, `a:sp3d`, `a:spDef`, `a:spLocks`, `a:spPr`, `a:tableStyle`, `a:tblPr`, `a:tblStyle`, `a:tc`, `a:tcBdr`, `a:tcPr`, `a:tcTxStyle`, `a:theme`, `a:themeElements`, `a:tr`, `a:txDef`, `a:txSp`, `a:uLn`, `a:videoFile`, `am3d:blip`, `am3d:spPr`, `c14:spPr`, `c15:spPr`, `c16:spPr`, `c:clrMapOvr`, `c:spPr`, `cdr14:cNvPr`, `cdr:cNvCxnSpPr`, `cdr:cNvGraphicFramePr`, `cdr:cNvGrpSpPr`, `cdr:cNvPicPr`, `cdr:cNvPr`, `cdr:cNvSpPr`, `cdr:grpSpPr`, `cdr:spPr`, `cs:bodyPr`, `cs:defRPr`, `cs:spPr`, `cx:clrMapOvr`, `cx:spPr`, `dgm14:cNvPr`, `dgm1612:lstStyle`, `dgm1612:spPr`, `dgm:scene3d`, `dgm:sp3d`, `dgm:spPr`, `dsp:cNvGrpSpPr`, `dsp:cNvPr`, `dsp:cNvSpPr`, `dsp:grpSpPr`, `dsp:spPr`, `lc:lockedCanvas`, `p14:cNvPr`, `p166:spPr`, `p:bodyStyle`, `p:cNvCxnSpPr`, `p:cNvGraphicFramePr`, `p:cNvGrpSpPr`, `p:cNvPicPr`, `p:cNvPr`, `p:cNvSpPr`, `p:clrMap`, `p:defaultTextStyle`, `p:grpSpPr`, `p:notesStyle`, `p:otherStyle`, `p:spPr`, `p:titleStyle`, `pic:cNvPicPr`, `pic:cNvPr`, `pic:spPr`, `w14:cNvPr`, `wp:bodyPr`, `wp:cNvCnPr`, `wp:cNvContentPartPr`, `wp:cNvFrPr`, `wp:cNvGraphicFramePr`, `wp:cNvGrpSpPr`, `wp:cNvPr`, `wp:cNvSpPr`, `wp:docPr`, `wp:grpSpPr`, `wp:spPr`, `wpg:cNvFrPr`, `wpg:cNvGrpSpPr`, `wpg:cNvPr`, `wpg:grpSpPr`, `wps:bodyPr`, `wps:cNvCnPr`, `wps:cNvPr`, `wps:cNvSpPr`, `wps:spPr`, `xdr14:cNvPr`, `xdr:cNvCxnSpPr`, `xdr:cNvGraphicFramePr`, `xdr:cNvGrpSpPr`, `xdr:cNvPicPr`, `xdr:cNvPr`, `xdr:cNvSpPr`, `xdr:grpSpPr`, `xdr:spPr`
- Specification: §20.1.2.2.15 p.2749

### a:extraClrScheme

- Type: `a:CT_ColorSchemeAndMapping`
- Children: `a:clrScheme`, `a:clrMap`
- Parents: `a:extraClrSchemeLst`
- Specification: §20.1.6.4 p.2851

### a:extraClrSchemeLst

- Type: `a:CT_ColorSchemeList`
- Children: `a:extraClrScheme`
- Parents: `a:theme`
- Specification: §20.1.6.5 p.2852

### a:extrusionClr

- Type: `a:CT_Color`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a14:hiddenSp3d`, `a:sp3d`, `dgm:sp3d`
- Specification: §20.1.5.7 p.2840, OI 2.1.1272

### a:fgClr

- Type: `a:CT_Color`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:pattFill`
- Specification: §20.1.8.27 p.2874

### a:fill

- Type: `a:CT_FillEffect`, `a:CT_FillProperties`
- Children: `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`
- Parents: `a:cont`, `a:effectDag`, `a:tblBg`, `a:tcStyle`
- Specification: §20.1.4.2.9 p.2817, OI 2.1.1263; §20.1.8.28 p.2875

### a:fillOverlay

- Type: `a:CT_FillOverlayEffect`
- Attributes: `blend`!
- Children: `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`
- Parents: `a:blip`, `a:cont`, `a:effectDag`, `a:effectLst`, `am3d:blip`
- Specification: §20.1.8.29 p.2875

### a:fillRect

- Type: `a:CT_RelativeRect`
- Attributes: `b`, `l`, `r`, `t`
- Parents: `a:stretch`
- Specification: §20.1.8.30 p.2875

### a:fillRef

- Type: `a:CT_StyleMatrixReference`
- Attributes: `idx`!
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:style`, `a:tblBg`, `a:tcStyle`, `cdr:style`, `dgm:style`, `dsp:style`, `p:style`, `pic14:style`, `wp:style`, `wps:style`, `xdr:style`
- Specification: §20.1.4.2.10 p.2818

### a:fillStyleLst

- Type: `a:CT_FillStyleList`
- Children: `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`
- Parents: `a:fmtScheme`
- Specification: §20.1.4.1.13 p.2801

### a:fillToRect

- Type: `a:CT_RelativeRect`
- Attributes: `b`, `l`, `r`, `t`
- Parents: `a:path`
- Specification: §20.1.8.31 p.2876, OI 2.1.1296

### a:firstCol

- Type: `a:CT_TablePartStyle`
- Children: `a:tcTxStyle`, `a:tcStyle`
- Parents: `a:tableStyle`, `a:tblStyle`
- Specification: §20.1.4.2.11 p.2819

### a:firstRow

- Type: `a:CT_TablePartStyle`
- Children: `a:tcTxStyle`, `a:tcStyle`
- Parents: `a:tableStyle`, `a:tblStyle`
- Specification: §20.1.4.2.12 p.2820

### a:flatTx

- Type: `a:CT_FlatText`
- Attributes: `z`
- Parents: `a:bodyPr`, `cs:bodyPr`, `dgm:txPr`, `wp:bodyPr`, `wps:bodyPr`
- Specification: §20.1.5.8 p.2841, OI 2.1.1273

### a:fld

- Type: `a:CT_TextField`
- Attributes: `id`!, `type`
- Children: `a:rPr`, `a:pPr`, `a:t`
- Parents: `a:p`
- Specification: §21.1.2.2.4 p.3222, OI 2.1.1385

### a:fmtScheme

- Type: `a:CT_StyleMatrix`
- Attributes: `name`
- Children: `a:fillStyleLst`, `a:lnStyleLst`, `a:effectStyleLst`, `a:bgFillStyleLst`
- Parents: `a:themeElements`, `a:themeOverride`
- Specification: §20.1.4.1.14 p.2801

### a:folHlink

- Type: `a:CT_Color`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:clrScheme`
- Specification: §20.1.4.1.15 p.2802, OI 2.1.1256

### a:font

- Type: `a:CT_SupplementalFont`, `a:CT_FontCollection`
- Attributes: `script`!, `typeface`!
- Children: `a:latin`, `a:ea`, `a:cs`, `a:font`, `a:extLst`
- Parents: `a:font`, `a:majorFont`, `a:minorFont`, `a:tcTxStyle`
- Specification: §20.1.4.1.16 p.2803; §20.1.4.2.13 p.2821

### a:fontRef

- Type: `a:CT_FontReference`
- Attributes: `idx`!
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:style`, `a:tcTxStyle`, `cdr:style`, `dgm:style`, `dsp:style`, `p:style`, `pic14:style`, `wp:style`, `wps:style`, `xdr:style`
- Specification: §20.1.4.1.17 p.2804, OI 2.1.1257

### a:fontScheme

- Type: `a:CT_FontScheme`
- Attributes: `name`!
- Children: `a:majorFont`, `a:minorFont`, `a:extLst`
- Parents: `a:themeElements`, `a:themeOverride`
- Specification: §20.1.4.1.18 p.2804

### a:gamma

- Type: `a:CT_GammaTransform`
- Parents: `a:hslClr`, `a:prstClr`, `a:schemeClr`, `a:scrgbClr`, `a:srgbClr`, `a:sysClr`, `cs:styleClr`, `cs:variation`
- Specification: §20.1.2.3.8 p.2771

### a:gd

- Type: `a:CT_GeomGuide`
- Attributes: `fmla`!, `name`!
- Parents: `a:avLst`, `a:gdLst`
- Specification: §20.1.9.11 p.2907, OI 2.1.1311

### a:gdLst

- Type: `a:CT_GeomGuideList`
- Children: `a:gd`
- Parents: `a:custGeom`
- Specification: §20.1.9.12 p.2910

### a:glow

- Type: `a:CT_GlowEffect`
- Attributes: `rad`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:cont`, `a:effectDag`, `a:effectLst`
- Specification: §20.1.8.32 p.2878

### a:gradFill

- Type: `a:CT_GradientFillProperties`
- Attributes: `flip`, `rotWithShape`
- Children: `a:gsLst`, `a:lin`, `a:path`, `a:tileRect`
- Parents: `a14:hiddenFill`, `a14:hiddenLine`, `a:bgFillStyleLst`, `a:defRPr`, `a:endParaRPr`, `a:fill`, `a:fillOverlay`, `a:fillStyleLst`, `a:grpSpPr`, `a:ln`, `a:lnB`, `a:lnBlToTr`, `a:lnL`, `a:lnR`, `a:lnT`, `a:lnTlToBr`, `a:rPr`, `a:spPr`, `a:tblPr`, `a:tcPr`, `a:uFill`, `a:uLn`, `am3d:spPr`, `c14:spPr`, `c15:spPr`, `c16:spPr`, `c:spPr`, `cdr:grpSpPr`, `cdr:spPr`, `cs:defRPr`, `cs:spPr`, `cx:spPr`, `dgm1612:spPr`, `dgm:bg`, `dgm:spPr`, `dsp:grpSpPr`, `dsp:spPr`, `p166:spPr`, `p:bgPr`, `p:grpSpPr`, `p:spPr`, `pic:spPr`, `wp:bg`, `wp:grpSpPr`, `wp:spPr`, `wpc:bg`, `wpg:grpSpPr`, `wps:spPr`, `xdr:grpSpPr`, `xdr:spPr`
- Specification: §20.1.8.33 p.2878, OI 2.1.1297

### a:graphic

- Type: `a:CT_GraphicalObject`
- Children: `a:graphicData`
- Parents: `a:graphicFrame`, `cdr:graphicFrame`, `p:graphicFrame`, `wp:anchor`, `wp:graphicFrame`, `wp:inline`, `wpc:graphicFrame`, `wpg:graphicFrame`, `xdr:graphicFrame`
- Specification: §20.1.2.2.16 p.2749

### a:graphicData

- Type: `a:CT_GraphicalObjectData`
- Attributes: `uri`!
- Children: `any(##any)`
- Parents: `a:graphic`
- Specification: §20.1.2.2.17 p.2749, OI 2.1.1207

### a:graphicFrame

- Type: `a:CT_GvmlGraphicalObjectFrame`
- Children: `a:nvGraphicFramePr`, `a:graphic`, `a:xfrm`, `a:extLst`
- Parents: `a:grpSp`, `lc:lockedCanvas`
- Specification: §20.1.2.2.18 p.2750

### a:graphicFrameLocks

- Type: `a:CT_GraphicalObjectFrameLocking`
- Attributes: `noChangeAspect`, `noDrilldown`, `noGrp`, `noMove`, `noResize`, `noSelect`
- Children: `a:extLst`
- Parents: `a:cNvGraphicFramePr`, `cdr:cNvGraphicFramePr`, `p:cNvGraphicFramePr`, `wp:cNvFrPr`, `wp:cNvGraphicFramePr`, `wpg:cNvFrPr`, `xdr:cNvGraphicFramePr`
- Specification: §20.1.2.2.19 p.2750, OI 2.1.1208

### a:gray

- Type: `a:CT_GrayscaleTransform`
- Parents: `a:hslClr`, `a:prstClr`, `a:schemeClr`, `a:scrgbClr`, `a:srgbClr`, `a:sysClr`, `cs:styleClr`, `cs:variation`
- Specification: §20.1.2.3.9 p.2771

### a:grayscl

- Type: `a:CT_GrayscaleEffect`
- Parents: `a:blip`, `a:cont`, `a:effectDag`, `am3d:blip`
- Specification: §20.1.8.34 p.2880

### a:green

- Type: `a:CT_Percentage`
- Attributes: `val`!
- Parents: `a:hslClr`, `a:prstClr`, `a:schemeClr`, `a:scrgbClr`, `a:srgbClr`, `a:sysClr`, `cs:styleClr`, `cs:variation`
- Specification: §20.1.2.3.10 p.2771, OI 2.1.1224

### a:greenMod

- Type: `a:CT_Percentage`
- Attributes: `val`!
- Parents: `a:hslClr`, `a:prstClr`, `a:schemeClr`, `a:scrgbClr`, `a:srgbClr`, `a:sysClr`, `cs:styleClr`, `cs:variation`
- Specification: §20.1.2.3.11 p.2771, OI 2.1.1225

### a:greenOff

- Type: `a:CT_Percentage`
- Attributes: `val`!
- Parents: `a:hslClr`, `a:prstClr`, `a:schemeClr`, `a:scrgbClr`, `a:srgbClr`, `a:sysClr`, `cs:styleClr`, `cs:variation`
- Specification: §20.1.2.3.12 p.2772, OI 2.1.1226

### a:gridCol

- Type: `a:CT_TableCol`
- Attributes: `w`!
- Children: `a:extLst`
- Parents: `a:tblGrid`
- Specification: §21.1.3.2 p.3352, OI 2.1.1416

### a:grpFill

- Type: `a:CT_GroupFillProperties`
- Parents: `a14:hiddenFill`, `a:bgFillStyleLst`, `a:defRPr`, `a:endParaRPr`, `a:fill`, `a:fillOverlay`, `a:fillStyleLst`, `a:grpSpPr`, `a:rPr`, `a:spPr`, `a:tblPr`, `a:tcPr`, `a:uFill`, `am3d:spPr`, `c14:spPr`, `c15:spPr`, `c16:spPr`, `c:spPr`, `cdr:grpSpPr`, `cdr:spPr`, `cs:defRPr`, `cs:spPr`, `cx:spPr`, `dgm1612:spPr`, `dgm:bg`, `dgm:spPr`, `dsp:grpSpPr`, `dsp:spPr`, `p166:spPr`, `p:bgPr`, `p:grpSpPr`, `p:spPr`, `pic:spPr`, `wp:bg`, `wp:grpSpPr`, `wp:spPr`, `wpc:bg`, `wpg:grpSpPr`, `wps:spPr`, `xdr:grpSpPr`, `xdr:spPr`
- Specification: §20.1.8.35 p.2880, OI 2.1.1298

### a:grpSp

- Type: `a:CT_GvmlGroupShape`
- Children: `a:nvGrpSpPr`, `a:grpSpPr`, `a:txSp`, `a:sp`, `a:cxnSp`, `a:pic`, `a:graphicFrame`, `a:grpSp`, `a:extLst`
- Parents: `a:grpSp`, `lc:lockedCanvas`
- Specification: §20.1.2.2.20 p.2751

### a:grpSpLocks

- Type: `a:CT_GroupLocking`
- Attributes: `noChangeAspect`, `noGrp`, `noMove`, `noResize`, `noRot`, `noSelect`, `noUngrp`
- Children: `a:extLst`
- Parents: `a:cNvGrpSpPr`, `cdr:cNvGrpSpPr`, `dsp:cNvGrpSpPr`, `p:cNvGrpSpPr`, `wp:cNvGrpSpPr`, `wpg:cNvGrpSpPr`, `xdr:cNvGrpSpPr`
- Specification: §20.1.2.2.21 p.2752, OI 2.1.1209

### a:grpSpPr

- Type: `a:CT_GroupShapeProperties`
- Attributes: `bwMode`
- Children: `a:xfrm`, `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`, `a:effectLst`, `a:effectDag`, `a:scene3d`, `a:extLst`
- Parents: `a:grpSp`, `lc:lockedCanvas`
- Specification: §20.1.2.2.22 p.2753, OI 2.1.1210

### a:gs

- Type: `a:CT_GradientStop`
- Attributes: `pos`!
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:gsLst`
- Specification: §20.1.8.36 p.2880

### a:gsLst

- Type: `a:CT_GradientStopList`
- Children: `a:gs`
- Parents: `a:gradFill`
- Specification: §20.1.8.37 p.2880

### a:headEnd

- Type: `a:CT_LineEndProperties`
- Attributes: `len`, `type`, `w`
- Parents: `a14:hiddenLine`, `a:ln`, `a:lnB`, `a:lnBlToTr`, `a:lnL`, `a:lnR`, `a:lnT`, `a:lnTlToBr`, `a:uLn`
- Specification: §20.1.8.38 p.2880

### a:header

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `a:headers`
- Specification: §21.1.3.3 p.3353

### a:headers

- Type: `a:CT_Headers`
- Children: `a:header`
- Parents: `a:tcPr`
- Specification: §21.1.3.4 p.3354

### a:highlight

- Type: `a:CT_Color`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:defRPr`, `a:endParaRPr`, `a:rPr`, `cs:defRPr`
- Specification: §21.1.2.3.4 p.3251

### a:hlink

- Type: `a:CT_Color`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:clrScheme`
- Specification: §20.1.4.1.19 p.2805, OI 2.1.1258

### a:hlinkClick

- Type: `a:CT_Hyperlink`
- Attributes: `action`, `endSnd`, `highlightClick`, `history`, `invalidUrl`, `r:id`, `tgtFrame`, `tooltip`
- Children: `a:snd`, `a:extLst`
- Parents: `a14:cNvPr`, `a:cNvPr`, `a:defRPr`, `a:endParaRPr`, `a:rPr`, `cdr14:cNvPr`, `cdr:cNvPr`, `cs:defRPr`, `dgm14:cNvPr`, `dsp:cNvPr`, `p14:cNvPr`, `p:cNvPr`, `pic:cNvPr`, `w14:cNvPr`, `wp:cNvPr`, `wp:docPr`, `wpg:cNvPr`, `wps:cNvPr`, `xdr14:cNvPr`, `xdr:cNvPr`
- Specification: §21.1.2.3.5 p.3251, OI 2.1.1395

### a:hlinkHover

- Type: `a:CT_Hyperlink`
- Attributes: `action`, `endSnd`, `highlightClick`, `history`, `invalidUrl`, `r:id`, `tgtFrame`, `tooltip`
- Children: `a:snd`, `a:extLst`
- Parents: `a14:cNvPr`, `a:cNvPr`, `cdr14:cNvPr`, `cdr:cNvPr`, `dgm14:cNvPr`, `dsp:cNvPr`, `p14:cNvPr`, `p:cNvPr`, `pic:cNvPr`, `w14:cNvPr`, `wp:cNvPr`, `wp:docPr`, `wpg:cNvPr`, `wps:cNvPr`, `xdr14:cNvPr`, `xdr:cNvPr`
- Specification: §20.1.2.2.23 p.2754, OI 2.1.1211

### a:hlinkMouseOver

- Type: `a:CT_Hyperlink`
- Attributes: `action`, `endSnd`, `highlightClick`, `history`, `invalidUrl`, `r:id`, `tgtFrame`, `tooltip`
- Children: `a:snd`, `a:extLst`
- Parents: `a:defRPr`, `a:endParaRPr`, `a:rPr`, `cs:defRPr`
- Specification: §21.1.2.3.6 p.3253, OI 2.1.1396

### a:hsl

- Type: `a:CT_HSLEffect`
- Attributes: `hue`, `lum`, `sat`
- Parents: `a:blip`, `a:cont`, `a:effectDag`, `am3d:blip`
- Specification: §20.1.8.39 p.2881, OI 2.1.1299

### a:hslClr

- Type: `a:CT_HslColor`
- Attributes: `hue`!, `lum`!, `sat`!
- Children: `a:tint`, `a:shade`, `a:comp`, `a:inv`, `a:gray`, `a:alpha`, `a:alphaOff`, `a:alphaMod`, `a:hue`, `a:hueOff`, `a:hueMod`, `a:sat`, `a:satOff`, `a:satMod`, `a:lum`, `a:lumOff`, `a:lumMod`, `a:red`, `a:redOff`, `a:redMod`, `a:green`, `a:greenOff`, `a:greenMod`, `a:blue`, `a:blueOff`, `a:blueMod`, `a:gamma`, `a:invGamma`
- Parents: `a:accent1`, `a:accent2`, `a:accent3`, `a:accent4`, `a:accent5`, `a:accent6`, `a:alphaInv`, `a:bgClr`, `a:buClr`, `a:clrFrom`, `a:clrRepl`, `a:clrTo`, `a:contourClr`, `a:custClr`, `a:dk1`, `a:dk2`, `a:duotone`, `a:effectRef`, `a:extrusionClr`, `a:fgClr`, `a:fillRef`, `a:folHlink`, `a:fontRef`, `a:glow`, `a:gs`, `a:highlight`, `a:hlink`, `a:innerShdw`, `a:lnRef`, `a:lt1`, `a:lt2`, `a:outerShdw`, `a:prstShdw`, `a:solidFill`, `a:tcTxStyle`, `am3d:clr`, `cs:colorStyle`, `cs:effectRef`, `cs:fillRef`, `cs:fontRef`, `cs:lnRef`, `cx:maxColor`, `cx:midColor`, `cx:minColor`, `dgm:effectClrLst`, `dgm:fillClrLst`, `dgm:linClrLst`, `dgm:txEffectClrLst`, `dgm:txFillClrLst`, `dgm:txLinClrLst`, `p14:laserClr`, `p15:clr`, `p:bgRef`, `p:clrMru`, `p:clrVal`, `p:from`, `p:penClr`, `p:to`
- Specification: §20.1.2.3.13 p.2773, OI 2.1.1227

### a:hue

- Type: `a:CT_PositiveFixedAngle`
- Attributes: `val`!
- Parents: `a:hslClr`, `a:prstClr`, `a:schemeClr`, `a:scrgbClr`, `a:srgbClr`, `a:sysClr`, `cs:styleClr`, `cs:variation`
- Specification: §20.1.2.3.14 p.2773, OI 2.1.1228

### a:hueMod

- Type: `a:CT_PositivePercentage`
- Attributes: `val`!
- Parents: `a:hslClr`, `a:prstClr`, `a:schemeClr`, `a:scrgbClr`, `a:srgbClr`, `a:sysClr`, `cs:styleClr`, `cs:variation`
- Specification: §20.1.2.3.15 p.2774, OI 2.1.1229

### a:hueOff

- Type: `a:CT_Angle`
- Attributes: `val`!
- Parents: `a:hslClr`, `a:prstClr`, `a:schemeClr`, `a:scrgbClr`, `a:srgbClr`, `a:sysClr`, `cs:styleClr`, `cs:variation`
- Specification: §20.1.2.3.16 p.2775, OI 2.1.1230

### a:innerShdw

- Type: `a:CT_InnerShadowEffect`
- Attributes: `blurRad`, `dir`, `dist`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:cont`, `a:effectDag`, `a:effectLst`
- Specification: §20.1.8.40 p.2882, OI 2.1.1300

### a:insideH

- Type: `a:CT_ThemeableLineStyle`
- Children: `a:ln`, `a:lnRef`
- Parents: `a:tcBdr`
- Specification: §20.1.4.2.14 p.2821

### a:insideV

- Type: `a:CT_ThemeableLineStyle`
- Children: `a:ln`, `a:lnRef`
- Parents: `a:tcBdr`
- Specification: §20.1.4.2.15 p.2821

### a:inv

- Type: `a:CT_InverseTransform`
- Parents: `a:hslClr`, `a:prstClr`, `a:schemeClr`, `a:scrgbClr`, `a:srgbClr`, `a:sysClr`, `cs:styleClr`, `cs:variation`
- Specification: §20.1.2.3.17 p.2775

### a:invGamma

- Type: `a:CT_InverseGammaTransform`
- Parents: `a:hslClr`, `a:prstClr`, `a:schemeClr`, `a:scrgbClr`, `a:srgbClr`, `a:sysClr`, `cs:styleClr`, `cs:variation`
- Specification: §20.1.2.3.18 p.2776

### a:lastCol

- Type: `a:CT_TablePartStyle`
- Children: `a:tcTxStyle`, `a:tcStyle`
- Parents: `a:tableStyle`, `a:tblStyle`
- Specification: §20.1.4.2.16 p.2822

### a:lastRow

- Type: `a:CT_TablePartStyle`
- Children: `a:tcTxStyle`, `a:tcStyle`
- Parents: `a:tableStyle`, `a:tblStyle`
- Specification: §20.1.4.2.17 p.2823

### a:latin

- Type: `a:CT_TextFont`
- Attributes: `charset`, `panose`, `pitchFamily`, `typeface`!
- Parents: `a:defRPr`, `a:endParaRPr`, `a:font`, `a:majorFont`, `a:minorFont`, `a:rPr`, `cs:defRPr`
- Specification: §21.1.2.3.7 p.3255, OI 2.1.1397

### a:left

- Type: `a:CT_ThemeableLineStyle`
- Children: `a:ln`, `a:lnRef`
- Parents: `a:tcBdr`
- Specification: §20.1.4.2.18 p.2824

### a:lightRig

- Type: `a:CT_LightRig`
- Attributes: `dir`!, `rig`!
- Children: `a:rot`
- Parents: `a14:hiddenScene3d`, `a:cell3D`, `a:scene3d`, `dgm:scene3d`
- Specification: §20.1.5.9 p.2841, OI 2.1.1274

### a:lin

- Type: `a:CT_LinearShadeProperties`
- Attributes: `ang`, `scaled`
- Parents: `a:gradFill`
- Specification: §20.1.8.41 p.2882

### a:ln

- Type: `a:CT_LineProperties`
- Attributes: `algn`, `cap`, `cmpd`, `w`
- Children: `a:noFill`, `a:solidFill`, `a:gradFill`, `a:pattFill`, `a:prstDash`, `a:custDash`, `a:round`, `a:bevel`, `a:miter`, `a:headEnd`, `a:tailEnd`, `a:extLst`
- Parents: `a:bottom`, `a:defRPr`, `a:endParaRPr`, `a:insideH`, `a:insideV`, `a:left`, `a:lnStyleLst`, `a:rPr`, `a:right`, `a:spPr`, `a:tl2br`, `a:top`, `a:tr2bl`, `am3d:spPr`, `c14:spPr`, `c15:spPr`, `c16:spPr`, `c:spPr`, `cdr:spPr`, `cs:defRPr`, `cs:spPr`, `cx:spPr`, `dgm1612:spPr`, `dgm:spPr`, `dgm:whole`, `dsp:spPr`, `p166:spPr`, `p:spPr`, `pic:spPr`, `wp:spPr`, `wp:whole`, `wpc:whole`, `wps:spPr`, `xdr:spPr`
- Specification: §20.1.2.2.24 p.2755, OI 2.1.1212

### a:lnB

- Type: `a:CT_LineProperties`
- Attributes: `algn`, `cap`, `cmpd`, `w`
- Children: `a:noFill`, `a:solidFill`, `a:gradFill`, `a:pattFill`, `a:prstDash`, `a:custDash`, `a:round`, `a:bevel`, `a:miter`, `a:headEnd`, `a:tailEnd`, `a:extLst`
- Parents: `a:tcPr`
- Specification: §21.1.3.5 p.3356, OI 2.1.1417

### a:lnBlToTr

- Type: `a:CT_LineProperties`
- Attributes: `algn`, `cap`, `cmpd`, `w`
- Children: `a:noFill`, `a:solidFill`, `a:gradFill`, `a:pattFill`, `a:prstDash`, `a:custDash`, `a:round`, `a:bevel`, `a:miter`, `a:headEnd`, `a:tailEnd`, `a:extLst`
- Parents: `a:tcPr`
- Specification: §21.1.3.6 p.3357, OI 2.1.1418

### a:lnDef

- Type: `a:CT_DefaultShapeDefinition`
- Children: `a:spPr`, `a:bodyPr`, `a:lstStyle`, `a:style`, `a:extLst`
- Parents: `a:objectDefaults`
- Specification: §20.1.4.1.20 p.2805

### a:lnL

- Type: `a:CT_LineProperties`
- Attributes: `algn`, `cap`, `cmpd`, `w`
- Children: `a:noFill`, `a:solidFill`, `a:gradFill`, `a:pattFill`, `a:prstDash`, `a:custDash`, `a:round`, `a:bevel`, `a:miter`, `a:headEnd`, `a:tailEnd`, `a:extLst`
- Parents: `a:tcPr`
- Specification: §21.1.3.7 p.3358, OI 2.1.1419

### a:lnR

- Type: `a:CT_LineProperties`
- Attributes: `algn`, `cap`, `cmpd`, `w`
- Children: `a:noFill`, `a:solidFill`, `a:gradFill`, `a:pattFill`, `a:prstDash`, `a:custDash`, `a:round`, `a:bevel`, `a:miter`, `a:headEnd`, `a:tailEnd`, `a:extLst`
- Parents: `a:tcPr`
- Specification: §21.1.3.8 p.3359, OI 2.1.1420

### a:lnRef

- Type: `a:CT_StyleMatrixReference`
- Attributes: `idx`!
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:bottom`, `a:insideH`, `a:insideV`, `a:left`, `a:right`, `a:style`, `a:tl2br`, `a:top`, `a:tr2bl`, `cdr:style`, `dgm:style`, `dsp:style`, `p:style`, `pic14:style`, `wp:style`, `wps:style`, `xdr:style`
- Specification: §20.1.4.2.19 p.2824, OI 2.1.1264

### a:lnSpc

- Type: `a:CT_TextSpacing`
- Children: `a:spcPct`, `a:spcPts`
- Parents: `a:defPPr`, `a:lvl1pPr`, `a:lvl2pPr`, `a:lvl3pPr`, `a:lvl4pPr`, `a:lvl5pPr`, `a:lvl6pPr`, `a:lvl7pPr`, `a:lvl8pPr`, `a:lvl9pPr`, `a:pPr`
- Specification: §21.1.2.2.5 p.3224, OI 2.1.1386

### a:lnStyleLst

- Type: `a:CT_LineStyleList`
- Children: `a:ln`
- Parents: `a:fmtScheme`
- Specification: §20.1.4.1.21 p.2806

### a:lnT

- Type: `a:CT_LineProperties`
- Attributes: `algn`, `cap`, `cmpd`, `w`
- Children: `a:noFill`, `a:solidFill`, `a:gradFill`, `a:pattFill`, `a:prstDash`, `a:custDash`, `a:round`, `a:bevel`, `a:miter`, `a:headEnd`, `a:tailEnd`, `a:extLst`
- Parents: `a:tcPr`
- Specification: §21.1.3.9 p.3360, OI 2.1.1421

### a:lnTlToBr

- Type: `a:CT_LineProperties`
- Attributes: `algn`, `cap`, `cmpd`, `w`
- Children: `a:noFill`, `a:solidFill`, `a:gradFill`, `a:pattFill`, `a:prstDash`, `a:custDash`, `a:round`, `a:bevel`, `a:miter`, `a:headEnd`, `a:tailEnd`, `a:extLst`
- Parents: `a:tcPr`
- Specification: §21.1.3.10 p.3361, OI 2.1.1422

### a:lnTo

- Type: `a:CT_Path2DLineTo`
- Children: `a:pt`
- Parents: `a:path`
- Specification: §20.1.9.13 p.2910

### a:lstStyle

- Type: `a:CT_TextListStyle`
- Children: `a:defPPr`, `a:lvl1pPr`, `a:lvl2pPr`, `a:lvl3pPr`, `a:lvl4pPr`, `a:lvl5pPr`, `a:lvl6pPr`, `a:lvl7pPr`, `a:lvl8pPr`, `a:lvl9pPr`, `a:extLst`
- Parents: `a:lnDef`, `a:spDef`, `a:txBody`, `a:txDef`, `c:rich`, `c:txPr`, `cdr:txBody`, `cx:rich`, `cx:txPr`, `dgm:t`, `dsp:txBody`, `p188:txBody`, `p:txBody`, `xdr:txBody`
- Specification: §21.1.2.4.12 p.3279

### a:lt1

- Type: `a:CT_Color`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:clrScheme`
- Specification: §20.1.4.1.22 p.2807, OI 2.1.1259

### a:lt2

- Type: `a:CT_Color`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:clrScheme`
- Specification: §20.1.4.1.23 p.2807, OI 2.1.1260

### a:lum

- Type: `a:CT_LuminanceEffect`, `a:CT_Percentage`
- Attributes: `bright`, `contrast`, `val`!
- Parents: `a:blip`, `a:cont`, `a:effectDag`, `a:hslClr`, `a:prstClr`, `a:schemeClr`, `a:scrgbClr`, `a:srgbClr`, `a:sysClr`, `am3d:blip`, `cs:styleClr`, `cs:variation`
- Specification: §20.1.2.3.19 p.2776, OI 2.1.1231; §20.1.8.42 p.2883, OI 2.1.1301

### a:lumMod

- Type: `a:CT_Percentage`
- Attributes: `val`!
- Parents: `a:hslClr`, `a:prstClr`, `a:schemeClr`, `a:scrgbClr`, `a:srgbClr`, `a:sysClr`, `cs:styleClr`, `cs:variation`
- Specification: §20.1.2.3.20 p.2777, OI 2.1.1232

### a:lumOff

- Type: `a:CT_Percentage`
- Attributes: `val`!
- Parents: `a:hslClr`, `a:prstClr`, `a:schemeClr`, `a:scrgbClr`, `a:srgbClr`, `a:sysClr`, `cs:styleClr`, `cs:variation`
- Specification: §20.1.2.3.21 p.2777, OI 2.1.1233

### a:lvl1pPr

- Type: `a:CT_TextParagraphProperties`
- Attributes: `algn`, `defTabSz`, `eaLnBrk`, `fontAlgn`, `hangingPunct`, `indent`, `latinLnBrk`, `lvl`, `marL`, `marR`, `rtl`
- Children: `a:lnSpc`, `a:spcBef`, `a:spcAft`, `a:buClrTx`, `a:buClr`, `a:buSzTx`, `a:buSzPct`, `a:buSzPts`, `a:buFontTx`, `a:buFont`, `a:buNone`, `a:buAutoNum`, `a:buChar`, `a:buBlip`, `a:tabLst`, `a:defRPr`, `a:extLst`
- Parents: `a:lstStyle`, `dgm1612:lstStyle`, `p:bodyStyle`, `p:defaultTextStyle`, `p:notesStyle`, `p:otherStyle`, `p:titleStyle`
- Specification: §21.1.2.4.13 p.3279, OI 2.1.1406

### a:lvl2pPr

- Type: `a:CT_TextParagraphProperties`
- Attributes: `algn`, `defTabSz`, `eaLnBrk`, `fontAlgn`, `hangingPunct`, `indent`, `latinLnBrk`, `lvl`, `marL`, `marR`, `rtl`
- Children: `a:lnSpc`, `a:spcBef`, `a:spcAft`, `a:buClrTx`, `a:buClr`, `a:buSzTx`, `a:buSzPct`, `a:buSzPts`, `a:buFontTx`, `a:buFont`, `a:buNone`, `a:buAutoNum`, `a:buChar`, `a:buBlip`, `a:tabLst`, `a:defRPr`, `a:extLst`
- Parents: `a:lstStyle`, `dgm1612:lstStyle`, `p:bodyStyle`, `p:defaultTextStyle`, `p:notesStyle`, `p:otherStyle`, `p:titleStyle`
- Specification: §21.1.2.4.14 p.3287, OI 2.1.1407

### a:lvl3pPr

- Type: `a:CT_TextParagraphProperties`
- Attributes: `algn`, `defTabSz`, `eaLnBrk`, `fontAlgn`, `hangingPunct`, `indent`, `latinLnBrk`, `lvl`, `marL`, `marR`, `rtl`
- Children: `a:lnSpc`, `a:spcBef`, `a:spcAft`, `a:buClrTx`, `a:buClr`, `a:buSzTx`, `a:buSzPct`, `a:buSzPts`, `a:buFontTx`, `a:buFont`, `a:buNone`, `a:buAutoNum`, `a:buChar`, `a:buBlip`, `a:tabLst`, `a:defRPr`, `a:extLst`
- Parents: `a:lstStyle`, `dgm1612:lstStyle`, `p:bodyStyle`, `p:defaultTextStyle`, `p:notesStyle`, `p:otherStyle`, `p:titleStyle`
- Specification: §21.1.2.4.15 p.3295, OI 2.1.1408

### a:lvl4pPr

- Type: `a:CT_TextParagraphProperties`
- Attributes: `algn`, `defTabSz`, `eaLnBrk`, `fontAlgn`, `hangingPunct`, `indent`, `latinLnBrk`, `lvl`, `marL`, `marR`, `rtl`
- Children: `a:lnSpc`, `a:spcBef`, `a:spcAft`, `a:buClrTx`, `a:buClr`, `a:buSzTx`, `a:buSzPct`, `a:buSzPts`, `a:buFontTx`, `a:buFont`, `a:buNone`, `a:buAutoNum`, `a:buChar`, `a:buBlip`, `a:tabLst`, `a:defRPr`, `a:extLst`
- Parents: `a:lstStyle`, `dgm1612:lstStyle`, `p:bodyStyle`, `p:defaultTextStyle`, `p:notesStyle`, `p:otherStyle`, `p:titleStyle`
- Specification: §21.1.2.4.16 p.3303, OI 2.1.1409

### a:lvl5pPr

- Type: `a:CT_TextParagraphProperties`
- Attributes: `algn`, `defTabSz`, `eaLnBrk`, `fontAlgn`, `hangingPunct`, `indent`, `latinLnBrk`, `lvl`, `marL`, `marR`, `rtl`
- Children: `a:lnSpc`, `a:spcBef`, `a:spcAft`, `a:buClrTx`, `a:buClr`, `a:buSzTx`, `a:buSzPct`, `a:buSzPts`, `a:buFontTx`, `a:buFont`, `a:buNone`, `a:buAutoNum`, `a:buChar`, `a:buBlip`, `a:tabLst`, `a:defRPr`, `a:extLst`
- Parents: `a:lstStyle`, `dgm1612:lstStyle`, `p:bodyStyle`, `p:defaultTextStyle`, `p:notesStyle`, `p:otherStyle`, `p:titleStyle`
- Specification: §21.1.2.4.17 p.3311, OI 2.1.1410

### a:lvl6pPr

- Type: `a:CT_TextParagraphProperties`
- Attributes: `algn`, `defTabSz`, `eaLnBrk`, `fontAlgn`, `hangingPunct`, `indent`, `latinLnBrk`, `lvl`, `marL`, `marR`, `rtl`
- Children: `a:lnSpc`, `a:spcBef`, `a:spcAft`, `a:buClrTx`, `a:buClr`, `a:buSzTx`, `a:buSzPct`, `a:buSzPts`, `a:buFontTx`, `a:buFont`, `a:buNone`, `a:buAutoNum`, `a:buChar`, `a:buBlip`, `a:tabLst`, `a:defRPr`, `a:extLst`
- Parents: `a:lstStyle`, `dgm1612:lstStyle`, `p:bodyStyle`, `p:defaultTextStyle`, `p:notesStyle`, `p:otherStyle`, `p:titleStyle`
- Specification: §21.1.2.4.18 p.3319, OI 2.1.1411

### a:lvl7pPr

- Type: `a:CT_TextParagraphProperties`
- Attributes: `algn`, `defTabSz`, `eaLnBrk`, `fontAlgn`, `hangingPunct`, `indent`, `latinLnBrk`, `lvl`, `marL`, `marR`, `rtl`
- Children: `a:lnSpc`, `a:spcBef`, `a:spcAft`, `a:buClrTx`, `a:buClr`, `a:buSzTx`, `a:buSzPct`, `a:buSzPts`, `a:buFontTx`, `a:buFont`, `a:buNone`, `a:buAutoNum`, `a:buChar`, `a:buBlip`, `a:tabLst`, `a:defRPr`, `a:extLst`
- Parents: `a:lstStyle`, `dgm1612:lstStyle`, `p:bodyStyle`, `p:defaultTextStyle`, `p:notesStyle`, `p:otherStyle`, `p:titleStyle`
- Specification: §21.1.2.4.19 p.3327, OI 2.1.1412

### a:lvl8pPr

- Type: `a:CT_TextParagraphProperties`
- Attributes: `algn`, `defTabSz`, `eaLnBrk`, `fontAlgn`, `hangingPunct`, `indent`, `latinLnBrk`, `lvl`, `marL`, `marR`, `rtl`
- Children: `a:lnSpc`, `a:spcBef`, `a:spcAft`, `a:buClrTx`, `a:buClr`, `a:buSzTx`, `a:buSzPct`, `a:buSzPts`, `a:buFontTx`, `a:buFont`, `a:buNone`, `a:buAutoNum`, `a:buChar`, `a:buBlip`, `a:tabLst`, `a:defRPr`, `a:extLst`
- Parents: `a:lstStyle`, `dgm1612:lstStyle`, `p:bodyStyle`, `p:defaultTextStyle`, `p:notesStyle`, `p:otherStyle`, `p:titleStyle`
- Specification: §21.1.2.4.20 p.3335, OI 2.1.1413

### a:lvl9pPr

- Type: `a:CT_TextParagraphProperties`
- Attributes: `algn`, `defTabSz`, `eaLnBrk`, `fontAlgn`, `hangingPunct`, `indent`, `latinLnBrk`, `lvl`, `marL`, `marR`, `rtl`
- Children: `a:lnSpc`, `a:spcBef`, `a:spcAft`, `a:buClrTx`, `a:buClr`, `a:buSzTx`, `a:buSzPct`, `a:buSzPts`, `a:buFontTx`, `a:buFont`, `a:buNone`, `a:buAutoNum`, `a:buChar`, `a:buBlip`, `a:tabLst`, `a:defRPr`, `a:extLst`
- Parents: `a:lstStyle`, `dgm1612:lstStyle`, `p:bodyStyle`, `p:defaultTextStyle`, `p:notesStyle`, `p:otherStyle`, `p:titleStyle`
- Specification: §21.1.2.4.21 p.3343, OI 2.1.1414

### a:majorFont

- Type: `a:CT_FontCollection`
- Children: `a:latin`, `a:ea`, `a:cs`, `a:font`, `a:extLst`
- Parents: `a:fontScheme`
- Specification: §20.1.4.1.24 p.2808

### a:masterClrMapping

- Type: `a:CT_EmptyElement`
- Parents: `p:clrMapOvr`
- Specification: §20.1.6.6 p.2853

### a:minorFont

- Type: `a:CT_FontCollection`
- Children: `a:latin`, `a:ea`, `a:cs`, `a:font`, `a:extLst`
- Parents: `a:fontScheme`
- Specification: §20.1.4.1.25 p.2809

### a:miter

- Type: `a:CT_LineJoinMiterProperties`
- Attributes: `lim`
- Parents: `a14:hiddenLine`, `a:ln`, `a:lnB`, `a:lnBlToTr`, `a:lnL`, `a:lnR`, `a:lnT`, `a:lnTlToBr`, `a:uLn`
- Specification: §20.1.8.43 p.2883, OI 2.1.1302

### a:moveTo

- Type: `a:CT_Path2DMoveTo`
- Children: `a:pt`
- Parents: `a:path`
- Specification: §20.1.9.14 p.2911

### a:neCell

- Type: `a:CT_TablePartStyle`
- Children: `a:tcTxStyle`, `a:tcStyle`
- Parents: `a:tableStyle`, `a:tblStyle`
- Specification: §20.1.4.2.20 p.2825

### a:noAutofit

- Type: `a:CT_TextNoAutofit`
- Parents: `a:bodyPr`, `cs:bodyPr`, `wp:bodyPr`, `wps:bodyPr`
- Specification: §21.1.2.1.2 p.3205, OI 2.1.1380

### a:noFill

- Type: `a:CT_NoFillProperties`
- Parents: `a14:hiddenFill`, `a14:hiddenLine`, `a:bgFillStyleLst`, `a:defRPr`, `a:endParaRPr`, `a:fill`, `a:fillOverlay`, `a:fillStyleLst`, `a:grpSpPr`, `a:ln`, `a:lnB`, `a:lnBlToTr`, `a:lnL`, `a:lnR`, `a:lnT`, `a:lnTlToBr`, `a:rPr`, `a:spPr`, `a:tblPr`, `a:tcPr`, `a:uFill`, `a:uLn`, `am3d:spPr`, `c14:spPr`, `c15:spPr`, `c16:spPr`, `c:spPr`, `cdr:grpSpPr`, `cdr:spPr`, `cs:defRPr`, `cs:spPr`, `cx:spPr`, `dgm1612:spPr`, `dgm:bg`, `dgm:spPr`, `dsp:grpSpPr`, `dsp:spPr`, `p166:spPr`, `p:bgPr`, `p:grpSpPr`, `p:spPr`, `pic:spPr`, `wp:bg`, `wp:grpSpPr`, `wp:spPr`, `wpc:bg`, `wpg:grpSpPr`, `wps:spPr`, `xdr:grpSpPr`, `xdr:spPr`
- Specification: §20.1.8.44 p.2884

### a:norm

- Type: `a:CT_Vector3D`
- Attributes: `dx`!, `dy`!, `dz`!
- Parents: `a:backdrop`
- Specification: §20.1.5.10 p.2842, OI 2.1.1275

### a:normAutofit

- Type: `a:CT_TextNormalAutofit`
- Attributes: `fontScale`, `lnSpcReduction`
- Parents: `a:bodyPr`, `cs:bodyPr`, `wp:bodyPr`, `wps:bodyPr`
- Specification: §21.1.2.1.3 p.3206, OI 2.1.1381

### a:nvCxnSpPr

- Type: `a:CT_GvmlConnectorNonVisual`
- Children: `a:cNvPr`, `a:cNvCxnSpPr`
- Parents: `a:cxnSp`
- Specification: §20.1.2.2.25 p.2756

### a:nvGraphicFramePr

- Type: `a:CT_GvmlGraphicFrameNonVisual`
- Children: `a:cNvPr`, `a:cNvGraphicFramePr`
- Parents: `a:graphicFrame`
- Specification: §20.1.2.2.26 p.2756

### a:nvGrpSpPr

- Type: `a:CT_GvmlGroupShapeNonVisual`
- Children: `a:cNvPr`, `a:cNvGrpSpPr`
- Parents: `a:grpSp`, `lc:lockedCanvas`
- Specification: §20.1.2.2.27 p.2756

### a:nvPicPr

- Type: `a:CT_GvmlPictureNonVisual`
- Children: `a:cNvPr`, `a:cNvPicPr`
- Parents: `a:pic`
- Specification: §20.1.2.2.28 p.2757

### a:nvSpPr

- Type: `a:CT_GvmlShapeNonVisual`
- Children: `a:cNvPr`, `a:cNvSpPr`
- Parents: `a:sp`
- Specification: §20.1.2.2.29 p.2757

### a:nwCell

- Type: `a:CT_TablePartStyle`
- Children: `a:tcTxStyle`, `a:tcStyle`
- Parents: `a:tableStyle`, `a:tblStyle`
- Specification: §20.1.4.2.21 p.2825

### a:objectDefaults

- Type: `a:CT_ObjectStyleDefaults`
- Children: `a:spDef`, `a:lnDef`, `a:txDef`, `a:extLst`
- Parents: `a:theme`
- Specification: §20.1.6.7 p.2853

### a:off

- Type: `a:CT_Point2D`
- Attributes: `x`!, `y`!
- Parents: `a14:xfrm`, `a:xfrm`, `cdr14:xfrm`, `cdr:xfrm`, `dsp:txXfrm`, `p14:xfrm`, `p:xfrm`, `w14:xfrm`, `wp:xfrm`, `wpg:xfrm`, `xdr14:xfrm`, `xdr:xfrm`
- Specification: §20.1.7.4 p.2859, OI 2.1.1281

### a:outerShdw

- Type: `a:CT_OuterShadowEffect`
- Attributes: `algn`, `blurRad`, `dir`, `dist`, `kx`, `ky`, `rotWithShape`, `sx`, `sy`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:cont`, `a:effectDag`, `a:effectLst`
- Specification: §20.1.8.45 p.2884, OI 2.1.1303

### a:overrideClrMapping

- Type: `a:CT_ColorMapping`
- Attributes: `accent1`!, `accent2`!, `accent3`!, `accent4`!, `accent5`!, `accent6`!, `bg1`!, `bg2`!, `folHlink`!, `hlink`!, `tx1`!, `tx2`!
- Children: `a:extLst`
- Parents: `p:clrMapOvr`
- Specification: §20.1.6.8 p.2853

### a:p

- Type: `a:CT_TextParagraph`
- Children: `a:pPr`, `a:r`, `a:br`, `a:fld`, `a:endParaRPr`
- Parents: `a:txBody`, `c:rich`, `c:txPr`, `cdr:txBody`, `cx:rich`, `cx:txPr`, `dgm:t`, `dsp:txBody`, `p188:txBody`, `p:txBody`, `xdr:txBody`
- Specification: §21.1.2.2.6 p.3225, OI 2.1.1387

### a:pPr

- Type: `a:CT_TextParagraphProperties`
- Attributes: `algn`, `defTabSz`, `eaLnBrk`, `fontAlgn`, `hangingPunct`, `indent`, `latinLnBrk`, `lvl`, `marL`, `marR`, `rtl`
- Children: `a:lnSpc`, `a:spcBef`, `a:spcAft`, `a:buClrTx`, `a:buClr`, `a:buSzTx`, `a:buSzPct`, `a:buSzPts`, `a:buFontTx`, `a:buFont`, `a:buNone`, `a:buAutoNum`, `a:buChar`, `a:buBlip`, `a:tabLst`, `a:defRPr`, `a:extLst`
- Parents: `a:fld`, `a:p`
- Specification: §21.1.2.2.7 p.3225, OI 2.1.1388

### a:path

- Type: `a:CT_PathShadeProperties`, `a:CT_Path2D`
- Attributes: `extrusionOk`, `fill`, `h`, `path`, `stroke`, `w`
- Children: `a:fillToRect`, `a:close`, `a:moveTo`, `a:lnTo`, `a:arcTo`, `a:quadBezTo`, `a:cubicBezTo`
- Parents: `a:gradFill`, `a:pathLst`
- Specification: §20.1.8.46 p.2885; §20.1.9.15 p.2911, OI 2.1.1312

### a:pathLst

- Type: `a:CT_Path2DList`
- Children: `a:path`
- Parents: `a:custGeom`
- Specification: §20.1.9.16 p.2913

### a:pattFill

- Type: `a:CT_PatternFillProperties`
- Attributes: `prst`
- Children: `a:fgClr`, `a:bgClr`
- Parents: `a14:hiddenFill`, `a14:hiddenLine`, `a:bgFillStyleLst`, `a:defRPr`, `a:endParaRPr`, `a:fill`, `a:fillOverlay`, `a:fillStyleLst`, `a:grpSpPr`, `a:ln`, `a:lnB`, `a:lnBlToTr`, `a:lnL`, `a:lnR`, `a:lnT`, `a:lnTlToBr`, `a:rPr`, `a:spPr`, `a:tblPr`, `a:tcPr`, `a:uFill`, `a:uLn`, `am3d:spPr`, `c14:spPr`, `c15:spPr`, `c16:spPr`, `c:spPr`, `cdr:grpSpPr`, `cdr:spPr`, `cs:defRPr`, `cs:spPr`, `cx:spPr`, `dgm1612:spPr`, `dgm:bg`, `dgm:spPr`, `dsp:grpSpPr`, `dsp:spPr`, `p166:spPr`, `p:bgPr`, `p:grpSpPr`, `p:spPr`, `pic:spPr`, `wp:bg`, `wp:grpSpPr`, `wp:spPr`, `wpc:bg`, `wpg:grpSpPr`, `wps:spPr`, `xdr:grpSpPr`, `xdr:spPr`
- Specification: §20.1.8.47 p.2886

### a:pic

- Type: `a:CT_GvmlPicture`
- Children: `a:nvPicPr`, `a:blipFill`, `a:spPr`, `a:style`, `a:extLst`
- Parents: `a:grpSp`, `lc:lockedCanvas`
- Specification: §20.1.2.2.30 p.2757

### a:picLocks

- Type: `a:CT_PictureLocking`
- Attributes: `noAdjustHandles`, `noChangeArrowheads`, `noChangeAspect`, `noChangeShapeType`, `noCrop`, `noEditPoints`, `noGrp`, `noMove`, `noResize`, `noRot`, `noSelect`
- Children: `a:extLst`
- Parents: `a:cNvPicPr`, `cdr:cNvPicPr`, `p:cNvPicPr`, `pic:cNvPicPr`, `xdr:cNvPicPr`
- Specification: §20.1.2.2.31 p.2758, OI 2.1.1213

### a:pos

- Type: `a:CT_AdjPoint2D`
- Attributes: `x`!, `y`!
- Parents: `a:ahPolar`, `a:ahXY`, `a:cxn`
- Specification: §20.1.9.17 p.2914, OI 2.1.1313

### a:prstClr

- Type: `a:CT_PresetColor`
- Attributes: `val`!
- Children: `a:tint`, `a:shade`, `a:comp`, `a:inv`, `a:gray`, `a:alpha`, `a:alphaOff`, `a:alphaMod`, `a:hue`, `a:hueOff`, `a:hueMod`, `a:sat`, `a:satOff`, `a:satMod`, `a:lum`, `a:lumOff`, `a:lumMod`, `a:red`, `a:redOff`, `a:redMod`, `a:green`, `a:greenOff`, `a:greenMod`, `a:blue`, `a:blueOff`, `a:blueMod`, `a:gamma`, `a:invGamma`
- Parents: `a:accent1`, `a:accent2`, `a:accent3`, `a:accent4`, `a:accent5`, `a:accent6`, `a:alphaInv`, `a:bgClr`, `a:buClr`, `a:clrFrom`, `a:clrRepl`, `a:clrTo`, `a:contourClr`, `a:custClr`, `a:dk1`, `a:dk2`, `a:duotone`, `a:effectRef`, `a:extrusionClr`, `a:fgClr`, `a:fillRef`, `a:folHlink`, `a:fontRef`, `a:glow`, `a:gs`, `a:highlight`, `a:hlink`, `a:innerShdw`, `a:lnRef`, `a:lt1`, `a:lt2`, `a:outerShdw`, `a:prstShdw`, `a:solidFill`, `a:tcTxStyle`, `am3d:clr`, `cs:colorStyle`, `cs:effectRef`, `cs:fillRef`, `cs:fontRef`, `cs:lnRef`, `cx:maxColor`, `cx:midColor`, `cx:minColor`, `dgm:effectClrLst`, `dgm:fillClrLst`, `dgm:linClrLst`, `dgm:txEffectClrLst`, `dgm:txFillClrLst`, `dgm:txLinClrLst`, `p14:laserClr`, `p15:clr`, `p:bgRef`, `p:clrMru`, `p:clrVal`, `p:from`, `p:penClr`, `p:to`
- Specification: §20.1.2.3.22 p.2778, OI 2.1.1234

### a:prstDash

- Type: `a:CT_PresetLineDashProperties`
- Attributes: `val`
- Parents: `a14:hiddenLine`, `a:ln`, `a:lnB`, `a:lnBlToTr`, `a:lnL`, `a:lnR`, `a:lnT`, `a:lnTlToBr`, `a:uLn`
- Specification: §20.1.8.48 p.2886

### a:prstGeom

- Type: `a:CT_PresetGeometry2D`
- Attributes: `prst`!
- Children: `a:avLst`
- Parents: `a:spPr`, `am3d:spPr`, `ask:lineSketchStyleProps`, `c14:spPr`, `c15:spPr`, `c16:spPr`, `c:spPr`, `cdr:spPr`, `cs:spPr`, `cx:spPr`, `dgm1612:spPr`, `dgm:spPr`, `dsp:spPr`, `p166:spPr`, `p:spPr`, `pic:spPr`, `wp:spPr`, `wps:spPr`, `xdr:spPr`
- Specification: §20.1.9.18 p.2916

### a:prstShdw

- Type: `a:CT_PresetShadowEffect`
- Attributes: `dir`, `dist`, `prst`!
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a:cont`, `a:effectDag`, `a:effectLst`
- Specification: §20.1.8.49 p.2887

### a:prstTxWarp

- Type: `a:CT_PresetTextShape`
- Attributes: `prst`!
- Children: `a:avLst`
- Parents: `a:bodyPr`, `cs:bodyPr`, `wp:bodyPr`, `wps:bodyPr`
- Specification: §20.1.9.19 p.2917

### a:pt

- Type: `a:CT_AdjPoint2D`
- Attributes: `x`!, `y`!
- Parents: `a:cubicBezTo`, `a:lnTo`, `a:moveTo`, `a:quadBezTo`
- Specification: §20.1.9.20 p.2920, OI 2.1.1314

### a:quadBezTo

- Type: `a:CT_Path2DQuadBezierTo`
- Children: `a:pt`
- Parents: `a:path`
- Specification: §20.1.9.21 p.2922

### a:quickTimeFile

- Type: `a:CT_QuickTimeFile`
- Attributes: `r:link`!
- Children: `a:extLst`
- Parents: `p14:nvPr`, `p:nvPr`
- Specification: §20.1.3.4 p.2789

### a:r

- Type: `a:CT_RegularTextRun`
- Children: `a:rPr`, `a:t`
- Parents: `a:p`
- Specification: §21.1.2.3.8 p.3257, OI 2.1.1398

### a:rPr

- Type: `a:CT_TextCharacterProperties`
- Attributes: `altLang`, `b`, `baseline`, `bmk`, `cap`, `dirty`, `err`, `i`, `kern`, `kumimoji`, `lang`, `noProof`, `normalizeH`, `smtClean`, `smtId`, `spc`, `strike`, `sz`, `u`
- Children: `a:ln`, `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`, `a:effectLst`, `a:effectDag`, `a:highlight`, `a:uLnTx`, `a:uLn`, `a:uFillTx`, `a:uFill`, `a:latin`, `a:ea`, `a:cs`, `a:sym`, `a:hlinkClick`, `a:hlinkMouseOver`, `a:rtl`, `a:extLst`
- Parents: `a:br`, `a:fld`, `a:r`
- Specification: §21.1.2.3.9 p.3257, OI 2.1.1399

### a:rect

- Type: `a:CT_GeomRect`
- Attributes: `b`!, `l`!, `r`!, `t`!
- Parents: `a:custGeom`
- Specification: §20.1.9.22 p.2922, OI 2.1.1315

### a:red

- Type: `a:CT_Percentage`
- Attributes: `val`!
- Parents: `a:hslClr`, `a:prstClr`, `a:schemeClr`, `a:scrgbClr`, `a:srgbClr`, `a:sysClr`, `cs:styleClr`, `cs:variation`
- Specification: §20.1.2.3.23 p.2778, OI 2.1.1235

### a:redMod

- Type: `a:CT_Percentage`
- Attributes: `val`!
- Parents: `a:hslClr`, `a:prstClr`, `a:schemeClr`, `a:scrgbClr`, `a:srgbClr`, `a:sysClr`, `cs:styleClr`, `cs:variation`
- Specification: §20.1.2.3.24 p.2779, OI 2.1.1236

### a:redOff

- Type: `a:CT_Percentage`
- Attributes: `val`!
- Parents: `a:hslClr`, `a:prstClr`, `a:schemeClr`, `a:scrgbClr`, `a:srgbClr`, `a:sysClr`, `cs:styleClr`, `cs:variation`
- Specification: §20.1.2.3.25 p.2780, OI 2.1.1237

### a:reflection

- Type: `a:CT_ReflectionEffect`
- Attributes: `algn`, `blurRad`, `dir`, `dist`, `endA`, `endPos`, `fadeDir`, `kx`, `ky`, `rotWithShape`, `stA`, `stPos`, `sx`, `sy`
- Parents: `a:cont`, `a:effectDag`, `a:effectLst`
- Specification: §20.1.8.50 p.2887, OI 2.1.1304

### a:relOff

- Type: `a:CT_RelativeOffsetEffect`
- Attributes: `tx`, `ty`
- Parents: `a:cont`, `a:effectDag`
- Specification: §20.1.8.51 p.2890

### a:right

- Type: `a:CT_ThemeableLineStyle`
- Children: `a:ln`, `a:lnRef`
- Parents: `a:tcBdr`
- Specification: §20.1.4.2.22 p.2826

### a:rot

- Type: `a:CT_SphereCoords`
- Attributes: `lat`!, `lon`!, `rev`!
- Parents: `a:camera`, `a:lightRig`
- Specification: §20.1.5.11 p.2843

### a:round

- Type: `a:CT_LineJoinRound`
- Parents: `a14:hiddenLine`, `a:ln`, `a:lnB`, `a:lnBlToTr`, `a:lnL`, `a:lnR`, `a:lnT`, `a:lnTlToBr`, `a:uLn`
- Specification: §20.1.8.52 p.2890, OI 2.1.1305

### a:rtl

- Type: `a:CT_Boolean`
- Attributes: `val`
- Parents: `a:defRPr`, `a:endParaRPr`, `a:rPr`, `cs:defRPr`
- Specification: §21.1.2.2.8 p.3233

### a:sat

- Type: `a:CT_Percentage`
- Attributes: `val`!
- Parents: `a:hslClr`, `a:prstClr`, `a:schemeClr`, `a:scrgbClr`, `a:srgbClr`, `a:sysClr`, `cs:styleClr`, `cs:variation`
- Specification: §20.1.2.3.26 p.2780, OI 2.1.1238

### a:satMod

- Type: `a:CT_Percentage`
- Attributes: `val`!
- Parents: `a:hslClr`, `a:prstClr`, `a:schemeClr`, `a:scrgbClr`, `a:srgbClr`, `a:sysClr`, `cs:styleClr`, `cs:variation`
- Specification: §20.1.2.3.27 p.2781, OI 2.1.1239

### a:satOff

- Type: `a:CT_Percentage`
- Attributes: `val`!
- Parents: `a:hslClr`, `a:prstClr`, `a:schemeClr`, `a:scrgbClr`, `a:srgbClr`, `a:sysClr`, `cs:styleClr`, `cs:variation`
- Specification: §20.1.2.3.28 p.2782, OI 2.1.1240

### a:scene3d

- Type: `a:CT_Scene3D`
- Children: `a:camera`, `a:lightRig`, `a:backdrop`, `a:extLst`
- Parents: `a:bodyPr`, `a:effectStyle`, `a:grpSpPr`, `a:spPr`, `am3d:spPr`, `c14:spPr`, `c15:spPr`, `c16:spPr`, `c:spPr`, `cdr:grpSpPr`, `cdr:spPr`, `cs:bodyPr`, `cs:spPr`, `cx:spPr`, `dgm1612:spPr`, `dgm:spPr`, `dsp:grpSpPr`, `dsp:spPr`, `p166:spPr`, `p:grpSpPr`, `p:spPr`, `pic:spPr`, `wp:bodyPr`, `wp:grpSpPr`, `wp:spPr`, `wpg:grpSpPr`, `wps:bodyPr`, `wps:spPr`, `xdr:grpSpPr`, `xdr:spPr`
- Specification: §20.1.4.1.26 p.2809

### a:schemeClr

- Type: `a:CT_SchemeColor`
- Attributes: `val`!
- Children: `a:tint`, `a:shade`, `a:comp`, `a:inv`, `a:gray`, `a:alpha`, `a:alphaOff`, `a:alphaMod`, `a:hue`, `a:hueOff`, `a:hueMod`, `a:sat`, `a:satOff`, `a:satMod`, `a:lum`, `a:lumOff`, `a:lumMod`, `a:red`, `a:redOff`, `a:redMod`, `a:green`, `a:greenOff`, `a:greenMod`, `a:blue`, `a:blueOff`, `a:blueMod`, `a:gamma`, `a:invGamma`
- Parents: `a:accent1`, `a:accent2`, `a:accent3`, `a:accent4`, `a:accent5`, `a:accent6`, `a:alphaInv`, `a:bgClr`, `a:buClr`, `a:clrFrom`, `a:clrRepl`, `a:clrTo`, `a:contourClr`, `a:custClr`, `a:dk1`, `a:dk2`, `a:duotone`, `a:effectRef`, `a:extrusionClr`, `a:fgClr`, `a:fillRef`, `a:folHlink`, `a:fontRef`, `a:glow`, `a:gs`, `a:highlight`, `a:hlink`, `a:innerShdw`, `a:lnRef`, `a:lt1`, `a:lt2`, `a:outerShdw`, `a:prstShdw`, `a:solidFill`, `a:tcTxStyle`, `am3d:clr`, `cs:colorStyle`, `cs:effectRef`, `cs:fillRef`, `cs:fontRef`, `cs:lnRef`, `cx:maxColor`, `cx:midColor`, `cx:minColor`, `dgm:effectClrLst`, `dgm:fillClrLst`, `dgm:linClrLst`, `dgm:txEffectClrLst`, `dgm:txFillClrLst`, `dgm:txLinClrLst`, `p14:laserClr`, `p15:clr`, `p:bgRef`, `p:clrMru`, `p:clrVal`, `p:from`, `p:penClr`, `p:to`
- Specification: §20.1.2.3.29 p.2782

### a:scrgbClr

- Type: `a:CT_ScRgbColor`
- Attributes: `b`!, `g`!, `r`!
- Children: `a:tint`, `a:shade`, `a:comp`, `a:inv`, `a:gray`, `a:alpha`, `a:alphaOff`, `a:alphaMod`, `a:hue`, `a:hueOff`, `a:hueMod`, `a:sat`, `a:satOff`, `a:satMod`, `a:lum`, `a:lumOff`, `a:lumMod`, `a:red`, `a:redOff`, `a:redMod`, `a:green`, `a:greenOff`, `a:greenMod`, `a:blue`, `a:blueOff`, `a:blueMod`, `a:gamma`, `a:invGamma`
- Parents: `a:accent1`, `a:accent2`, `a:accent3`, `a:accent4`, `a:accent5`, `a:accent6`, `a:alphaInv`, `a:bgClr`, `a:buClr`, `a:clrFrom`, `a:clrRepl`, `a:clrTo`, `a:contourClr`, `a:custClr`, `a:dk1`, `a:dk2`, `a:duotone`, `a:effectRef`, `a:extrusionClr`, `a:fgClr`, `a:fillRef`, `a:folHlink`, `a:fontRef`, `a:glow`, `a:gs`, `a:highlight`, `a:hlink`, `a:innerShdw`, `a:lnRef`, `a:lt1`, `a:lt2`, `a:outerShdw`, `a:prstShdw`, `a:solidFill`, `a:tcTxStyle`, `am3d:clr`, `cs:colorStyle`, `cs:effectRef`, `cs:fillRef`, `cs:fontRef`, `cs:lnRef`, `cx:maxColor`, `cx:midColor`, `cx:minColor`, `dgm:effectClrLst`, `dgm:fillClrLst`, `dgm:linClrLst`, `dgm:txEffectClrLst`, `dgm:txFillClrLst`, `dgm:txLinClrLst`, `p14:laserClr`, `p15:clr`, `p:bgRef`, `p:clrMru`, `p:clrVal`, `p:from`, `p:penClr`, `p:to`
- Specification: §20.1.2.3.30 p.2783

### a:seCell

- Type: `a:CT_TablePartStyle`
- Children: `a:tcTxStyle`, `a:tcStyle`
- Parents: `a:tableStyle`, `a:tblStyle`
- Specification: §20.1.4.2.23 p.2827

### a:shade

- Type: `a:CT_PositiveFixedPercentage`
- Attributes: `val`!
- Parents: `a:hslClr`, `a:prstClr`, `a:schemeClr`, `a:scrgbClr`, `a:srgbClr`, `a:sysClr`, `cs:styleClr`, `cs:variation`
- Specification: §20.1.2.3.31 p.2784, OI 2.1.1241

### a:snd

- Type: `a:CT_EmbeddedWAVAudioFile`
- Attributes: `name`, `r:embed`!
- Parents: `a:hlinkClick`, `a:hlinkHover`, `a:hlinkMouseOver`
- Specification: §20.1.2.2.32 p.2760

### a:softEdge

- Type: `a:CT_SoftEdgesEffect`
- Attributes: `rad`!
- Parents: `a:cont`, `a:effectDag`, `a:effectLst`
- Specification: §20.1.8.53 p.2890, OI 2.1.1306

### a:solidFill

- Type: `a:CT_SolidColorFillProperties`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `a14:hiddenFill`, `a14:hiddenLine`, `a:bgFillStyleLst`, `a:defRPr`, `a:endParaRPr`, `a:fill`, `a:fillOverlay`, `a:fillStyleLst`, `a:grpSpPr`, `a:ln`, `a:lnB`, `a:lnBlToTr`, `a:lnL`, `a:lnR`, `a:lnT`, `a:lnTlToBr`, `a:rPr`, `a:spPr`, `a:tblPr`, `a:tcPr`, `a:uFill`, `a:uLn`, `am3d:spPr`, `c14:spPr`, `c15:spPr`, `c16:spPr`, `c:spPr`, `cdr:grpSpPr`, `cdr:spPr`, `cs:defRPr`, `cs:spPr`, `cx:spPr`, `dgm1612:spPr`, `dgm:bg`, `dgm:spPr`, `dsp:grpSpPr`, `dsp:spPr`, `p166:spPr`, `p:bgPr`, `p:grpSpPr`, `p:spPr`, `pic:spPr`, `wp:bg`, `wp:grpSpPr`, `wp:spPr`, `wpc:bg`, `wpg:grpSpPr`, `wps:spPr`, `xdr:grpSpPr`, `xdr:spPr`
- Specification: §20.1.8.54 p.2891

### a:sp

- Type: `a:CT_GvmlShape`
- Children: `a:nvSpPr`, `a:spPr`, `a:txSp`, `a:style`, `a:extLst`
- Parents: `a:grpSp`, `lc:lockedCanvas`
- Specification: §20.1.2.2.33 p.2760

### a:sp3d

- Type: `a:CT_Shape3D`
- Attributes: `contourW`, `extrusionH`, `prstMaterial`, `z`
- Children: `a:bevelT`, `a:bevelB`, `a:extrusionClr`, `a:contourClr`, `a:extLst`
- Parents: `a:bodyPr`, `a:effectStyle`, `a:spPr`, `am3d:spPr`, `c14:spPr`, `c15:spPr`, `c16:spPr`, `c:spPr`, `cdr:spPr`, `cs:bodyPr`, `cs:spPr`, `cx:spPr`, `dgm1612:spPr`, `dgm:spPr`, `dgm:txPr`, `dsp:spPr`, `p166:spPr`, `p:spPr`, `pic:spPr`, `wp:bodyPr`, `wp:spPr`, `wps:bodyPr`, `wps:spPr`, `xdr:spPr`
- Specification: §20.1.5.12 p.2844, OI 2.1.1276

### a:spAutoFit

- Type: `a:CT_TextShapeAutofit`
- Parents: `a:bodyPr`, `cs:bodyPr`, `wp:bodyPr`, `wps:bodyPr`
- Specification: §21.1.2.1.4 p.3207, OI 2.1.1382

### a:spDef

- Type: `a:CT_DefaultShapeDefinition`
- Children: `a:spPr`, `a:bodyPr`, `a:lstStyle`, `a:style`, `a:extLst`
- Parents: `a:objectDefaults`
- Specification: §20.1.4.1.27 p.2810

### a:spLocks

- Type: `a:CT_ShapeLocking`
- Attributes: `noAdjustHandles`, `noChangeArrowheads`, `noChangeAspect`, `noChangeShapeType`, `noEditPoints`, `noGrp`, `noMove`, `noResize`, `noRot`, `noSelect`, `noTextEdit`
- Children: `a:extLst`
- Parents: `a:cNvSpPr`, `cdr:cNvSpPr`, `dsp:cNvSpPr`, `p:cNvSpPr`, `wp:cNvSpPr`, `wps:cNvSpPr`, `xdr:cNvSpPr`
- Specification: §20.1.2.2.34 p.2761, OI 2.1.1214

### a:spPr

- Type: `a:CT_ShapeProperties`
- Attributes: `bwMode`
- Children: `a:xfrm`, `a:custGeom`, `a:prstGeom`, `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`, `a:ln`, `a:effectLst`, `a:effectDag`, `a:scene3d`, `a:sp3d`, `a:extLst`
- Parents: `a:cxnSp`, `a:lnDef`, `a:pic`, `a:sp`, `a:spDef`, `a:txDef`
- Specification: §20.1.2.2.35 p.2762, OI 2.1.1215

### a:spcAft

- Type: `a:CT_TextSpacing`
- Children: `a:spcPct`, `a:spcPts`
- Parents: `a:defPPr`, `a:lvl1pPr`, `a:lvl2pPr`, `a:lvl3pPr`, `a:lvl4pPr`, `a:lvl5pPr`, `a:lvl6pPr`, `a:lvl7pPr`, `a:lvl8pPr`, `a:lvl9pPr`, `a:pPr`
- Specification: §21.1.2.2.9 p.3236, OI 2.1.1389

### a:spcBef

- Type: `a:CT_TextSpacing`
- Children: `a:spcPct`, `a:spcPts`
- Parents: `a:defPPr`, `a:lvl1pPr`, `a:lvl2pPr`, `a:lvl3pPr`, `a:lvl4pPr`, `a:lvl5pPr`, `a:lvl6pPr`, `a:lvl7pPr`, `a:lvl8pPr`, `a:lvl9pPr`, `a:pPr`
- Specification: §21.1.2.2.10 p.3236, OI 2.1.1390

### a:spcPct

- Type: `a:CT_TextSpacingPercent`
- Attributes: `val`!
- Parents: `a:lnSpc`, `a:spcAft`, `a:spcBef`
- Specification: §21.1.2.2.11 p.3237, OI 2.1.1391

### a:spcPts

- Type: `a:CT_TextSpacingPoint`
- Attributes: `val`!
- Parents: `a:lnSpc`, `a:spcAft`, `a:spcBef`
- Specification: §21.1.2.2.12 p.3238

### a:srcRect

- Type: `a:CT_RelativeRect`
- Attributes: `b`, `l`, `r`, `t`
- Parents: `a:blipFill`, `cdr:blipFill`, `p166:blipFill`, `p:blipFill`, `pic:blipFill`, `xdr:blipFill`
- Specification: §20.1.8.55 p.2891, OI 2.1.1307

### a:srgbClr

- Type: `a:CT_SRgbColor`
- Attributes: `val`!
- Children: `a:tint`, `a:shade`, `a:comp`, `a:inv`, `a:gray`, `a:alpha`, `a:alphaOff`, `a:alphaMod`, `a:hue`, `a:hueOff`, `a:hueMod`, `a:sat`, `a:satOff`, `a:satMod`, `a:lum`, `a:lumOff`, `a:lumMod`, `a:red`, `a:redOff`, `a:redMod`, `a:green`, `a:greenOff`, `a:greenMod`, `a:blue`, `a:blueOff`, `a:blueMod`, `a:gamma`, `a:invGamma`
- Parents: `a:accent1`, `a:accent2`, `a:accent3`, `a:accent4`, `a:accent5`, `a:accent6`, `a:alphaInv`, `a:bgClr`, `a:buClr`, `a:clrFrom`, `a:clrRepl`, `a:clrTo`, `a:contourClr`, `a:custClr`, `a:dk1`, `a:dk2`, `a:duotone`, `a:effectRef`, `a:extrusionClr`, `a:fgClr`, `a:fillRef`, `a:folHlink`, `a:fontRef`, `a:glow`, `a:gs`, `a:highlight`, `a:hlink`, `a:innerShdw`, `a:lnRef`, `a:lt1`, `a:lt2`, `a:outerShdw`, `a:prstShdw`, `a:solidFill`, `a:tcTxStyle`, `am3d:clr`, `cs:colorStyle`, `cs:effectRef`, `cs:fillRef`, `cs:fontRef`, `cs:lnRef`, `cx:maxColor`, `cx:midColor`, `cx:minColor`, `dgm:effectClrLst`, `dgm:fillClrLst`, `dgm:linClrLst`, `dgm:txEffectClrLst`, `dgm:txFillClrLst`, `dgm:txLinClrLst`, `p14:laserClr`, `p15:clr`, `p:bgRef`, `p:clrMru`, `p:clrVal`, `p:from`, `p:penClr`, `p:to`
- Specification: §20.1.2.3.32 p.2784

### a:st

- Type: `a:CT_AudioCDTime`
- Attributes: `time`, `track`!
- Parents: `a:audioCd`
- Specification: §20.1.3.5 p.2790, OI 2.1.1246

### a:stCxn

- Type: `a:CT_Connection`
- Attributes: `id`!, `idx`!
- Parents: `a:cNvCxnSpPr`, `cdr:cNvCxnSpPr`, `p:cNvCxnSpPr`, `wp:cNvCnPr`, `wps:cNvCnPr`, `xdr:cNvCxnSpPr`
- Specification: §20.1.2.2.36 p.2763, OI 2.1.1216

### a:stretch

- Type: `a:CT_StretchInfoProperties`
- Children: `a:fillRect`
- Parents: `a:blipFill`, `cdr:blipFill`, `p166:blipFill`, `p:blipFill`, `pic:blipFill`, `xdr:blipFill`
- Specification: §20.1.8.56 p.2891

### a:style

- Type: `a:CT_ShapeStyle`
- Children: `a:lnRef`, `a:fillRef`, `a:effectRef`, `a:fontRef`
- Parents: `a:cxnSp`, `a:lnDef`, `a:pic`, `a:sp`, `a:spDef`, `a:txDef`
- Specification: §20.1.2.2.37 p.2763, OI 2.1.1217

### a:swCell

- Type: `a:CT_TablePartStyle`
- Children: `a:tcTxStyle`, `a:tcStyle`
- Parents: `a:tableStyle`, `a:tblStyle`
- Specification: §20.1.4.2.24 p.2827

### a:sx

- Type: `a:CT_Ratio`
- Attributes: `d`!, `n`!
- Parents: `p:scale`
- Specification: §20.1.2.2.38 p.2763

### a:sy

- Type: `a:CT_Ratio`
- Attributes: `d`!, `n`!
- Parents: `p:scale`
- Specification: §20.1.2.2.39 p.2764

### a:sym

- Type: `a:CT_TextFont`
- Attributes: `charset`, `panose`, `pitchFamily`, `typeface`!
- Parents: `a:defRPr`, `a:endParaRPr`, `a:rPr`, `cs:defRPr`
- Specification: §21.1.2.3.10 p.3262, OI 2.1.1400

### a:sysClr

- Type: `a:CT_SystemColor`
- Attributes: `lastClr`, `val`!
- Children: `a:tint`, `a:shade`, `a:comp`, `a:inv`, `a:gray`, `a:alpha`, `a:alphaOff`, `a:alphaMod`, `a:hue`, `a:hueOff`, `a:hueMod`, `a:sat`, `a:satOff`, `a:satMod`, `a:lum`, `a:lumOff`, `a:lumMod`, `a:red`, `a:redOff`, `a:redMod`, `a:green`, `a:greenOff`, `a:greenMod`, `a:blue`, `a:blueOff`, `a:blueMod`, `a:gamma`, `a:invGamma`
- Parents: `a:accent1`, `a:accent2`, `a:accent3`, `a:accent4`, `a:accent5`, `a:accent6`, `a:alphaInv`, `a:bgClr`, `a:buClr`, `a:clrFrom`, `a:clrRepl`, `a:clrTo`, `a:contourClr`, `a:custClr`, `a:dk1`, `a:dk2`, `a:duotone`, `a:effectRef`, `a:extrusionClr`, `a:fgClr`, `a:fillRef`, `a:folHlink`, `a:fontRef`, `a:glow`, `a:gs`, `a:highlight`, `a:hlink`, `a:innerShdw`, `a:lnRef`, `a:lt1`, `a:lt2`, `a:outerShdw`, `a:prstShdw`, `a:solidFill`, `a:tcTxStyle`, `am3d:clr`, `cs:colorStyle`, `cs:effectRef`, `cs:fillRef`, `cs:fontRef`, `cs:lnRef`, `cx:maxColor`, `cx:midColor`, `cx:minColor`, `dgm:effectClrLst`, `dgm:fillClrLst`, `dgm:linClrLst`, `dgm:txEffectClrLst`, `dgm:txFillClrLst`, `dgm:txLinClrLst`, `p14:laserClr`, `p15:clr`, `p:bgRef`, `p:clrMru`, `p:clrVal`, `p:from`, `p:penClr`, `p:to`
- Specification: §20.1.2.3.33 p.2785, OI 2.1.1242

### a:t

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `a:fld`, `a:r`
- Specification: §21.1.2.3.11 p.3264

### a:tab

- Type: `a:CT_TextTabStop`
- Attributes: `algn`, `pos`
- Parents: `a:tabLst`
- Specification: §21.1.2.2.13 p.3239

### a:tabLst

- Type: `a:CT_TextTabStopList`
- Children: `a:tab`
- Parents: `a:defPPr`, `a:lvl1pPr`, `a:lvl2pPr`, `a:lvl3pPr`, `a:lvl4pPr`, `a:lvl5pPr`, `a:lvl6pPr`, `a:lvl7pPr`, `a:lvl8pPr`, `a:lvl9pPr`, `a:pPr`
- Specification: §21.1.2.2.14 p.3240

### a:tableStyle

- Type: `a:CT_TableStyle`
- Attributes: `styleId`!, `styleName`!
- Children: `a:tblBg`, `a:wholeTbl`, `a:band1H`, `a:band2H`, `a:band1V`, `a:band2V`, `a:lastCol`, `a:firstCol`, `a:lastRow`, `a:seCell`, `a:swCell`, `a:firstRow`, `a:neCell`, `a:nwCell`, `a:extLst`
- Parents: `a:tblPr`
- Specification: §21.1.3.11 p.3362

### a:tableStyleId

- Type: `s:ST_Guid` (text: `s:ST_Guid`)
- Parents: `a:tblPr`
- Specification: §21.1.3.12 p.3364, OI 2.1.1423

### a:tailEnd

- Type: `a:CT_LineEndProperties`
- Attributes: `len`, `type`, `w`
- Parents: `a14:hiddenLine`, `a:ln`, `a:lnB`, `a:lnBlToTr`, `a:lnL`, `a:lnR`, `a:lnT`, `a:lnTlToBr`, `a:uLn`
- Specification: §20.1.8.57 p.2892

### a:tbl

- Type: `a:CT_Table`
- Children: `a:tblPr`, `a:tblGrid`, `a:tr`
- Specification: §21.1.3.13 p.3364, OI 2.1.1424

### a:tblBg

- Type: `a:CT_TableBackgroundStyle`
- Children: `a:fill`, `a:fillRef`, `a:effect`, `a:effectRef`
- Parents: `a:tableStyle`, `a:tblStyle`
- Specification: §20.1.4.2.25 p.2828

### a:tblGrid

- Type: `a:CT_TableGrid`
- Children: `a:gridCol`
- Parents: `a:tbl`
- Specification: §21.1.3.14 p.3365, OI 2.1.1425

### a:tblPr

- Type: `a:CT_TableProperties`
- Attributes: `bandCol`, `bandRow`, `firstCol`, `firstRow`, `lastCol`, `lastRow`, `rtl`
- Children: `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`, `a:effectLst`, `a:effectDag`, `a:tableStyle`, `a:tableStyleId`, `a:extLst`
- Parents: `a:tbl`
- Specification: §21.1.3.15 p.3365

### a:tblStyle

- Type: `a:CT_TableStyle`
- Attributes: `styleId`!, `styleName`!
- Children: `a:tblBg`, `a:wholeTbl`, `a:band1H`, `a:band2H`, `a:band1V`, `a:band2V`, `a:lastCol`, `a:firstCol`, `a:lastRow`, `a:seCell`, `a:swCell`, `a:firstRow`, `a:neCell`, `a:nwCell`, `a:extLst`
- Parents: `a:tblStyleLst`
- Specification: §20.1.4.2.26 p.2828, OI 2.1.1265

### a:tblStyleLst

- Type: `a:CT_TableStyleList`
- Attributes: `def`!
- Children: `a:tblStyle`
- Specification: §20.1.4.2.27 p.2829

### a:tc

- Type: `a:CT_TableCell`
- Attributes: `gridSpan`, `hMerge`, `id`, `rowSpan`, `vMerge`
- Children: `a:txBody`, `a:tcPr`, `a:extLst`
- Parents: `a:tr`
- Specification: §21.1.3.16 p.3368, OI 2.1.1426

### a:tcBdr

- Type: `a:CT_TableCellBorderStyle`
- Children: `a:left`, `a:right`, `a:top`, `a:bottom`, `a:insideH`, `a:insideV`, `a:tl2br`, `a:tr2bl`, `a:extLst`
- Parents: `a:tcStyle`
- Specification: §20.1.4.2.28 p.2829

### a:tcPr

- Type: `a:CT_TableCellProperties`
- Attributes: `anchor`, `anchorCtr`, `horzOverflow`, `marB`, `marL`, `marR`, `marT`, `vert`
- Children: `a:lnL`, `a:lnR`, `a:lnT`, `a:lnB`, `a:lnTlToBr`, `a:lnBlToTr`, `a:cell3D`, `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`, `a:headers`, `a:extLst`
- Parents: `a:tc`
- Specification: §21.1.3.17 p.3371

### a:tcStyle

- Type: `a:CT_TableStyleCellStyle`
- Children: `a:tcBdr`, `a:fill`, `a:fillRef`, `a:cell3D`
- Parents: `a:band1H`, `a:band1V`, `a:band2H`, `a:band2V`, `a:firstCol`, `a:firstRow`, `a:lastCol`, `a:lastRow`, `a:neCell`, `a:nwCell`, `a:seCell`, `a:swCell`, `a:wholeTbl`
- Specification: §20.1.4.2.29 p.2830

### a:tcTxStyle

- Type: `a:CT_TableStyleTextStyle`
- Attributes: `b`, `i`
- Children: `a:font`, `a:fontRef`, `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`, `a:extLst`
- Parents: `a:band1H`, `a:band1V`, `a:band2H`, `a:band2V`, `a:firstCol`, `a:firstRow`, `a:lastCol`, `a:lastRow`, `a:neCell`, `a:nwCell`, `a:seCell`, `a:swCell`, `a:wholeTbl`
- Specification: §20.1.4.2.30 p.2831

### a:theme

- Type: `a:CT_OfficeStyleSheet`
- Attributes: `name`
- Children: `a:themeElements`, `a:objectDefaults`, `a:extraClrSchemeLst`, `a:custClrLst`, `a:extLst`
- Specification: §20.1.6.9 p.2855

### a:themeElements

- Type: `a:CT_BaseStyles`
- Children: `a:clrScheme`, `a:fontScheme`, `a:fmtScheme`, `a:extLst`
- Parents: `a:theme`
- Specification: §20.1.6.10 p.2855

### a:themeManager

- Type: `a:CT_EmptyElement`
- Specification: §20.1.6.11 p.2856

### a:themeOverride

- Type: `a:CT_BaseStylesOverride`
- Children: `a:clrScheme`, `a:fontScheme`, `a:fmtScheme`
- Specification: §20.1.6.12 p.2856

### a:tile

- Type: `a:CT_TileInfoProperties`
- Attributes: `algn`, `flip`, `sx`, `sy`, `tx`, `ty`
- Parents: `a:blipFill`, `cdr:blipFill`, `p166:blipFill`, `p:blipFill`, `pic:blipFill`, `xdr:blipFill`
- Specification: §20.1.8.58 p.2892

### a:tileRect

- Type: `a:CT_RelativeRect`
- Attributes: `b`, `l`, `r`, `t`
- Parents: `a:gradFill`
- Specification: §20.1.8.59 p.2894, OI 2.1.1308

### a:tint

- Type: `a:CT_TintEffect`, `a:CT_PositiveFixedPercentage`
- Attributes: `amt`, `hue`, `val`!
- Parents: `a:blip`, `a:cont`, `a:effectDag`, `a:hslClr`, `a:prstClr`, `a:schemeClr`, `a:scrgbClr`, `a:srgbClr`, `a:sysClr`, `am3d:blip`, `cs:styleClr`, `cs:variation`
- Specification: §20.1.2.3.34 p.2785, OI 2.1.1243; §20.1.8.60 p.2895

### a:tl2br

- Type: `a:CT_ThemeableLineStyle`
- Children: `a:ln`, `a:lnRef`
- Parents: `a:tcBdr`
- Specification: §20.1.4.2.31 p.2832

### a:top

- Type: `a:CT_ThemeableLineStyle`
- Children: `a:ln`, `a:lnRef`
- Parents: `a:tcBdr`
- Specification: §20.1.4.2.32 p.2832

### a:tr

- Type: `a:CT_TableRow`
- Attributes: `h`!
- Children: `a:tc`, `a:extLst`
- Parents: `a:tbl`
- Specification: §21.1.3.18 p.3374, OI 2.1.1427

### a:tr2bl

- Type: `a:CT_ThemeableLineStyle`
- Children: `a:ln`, `a:lnRef`
- Parents: `a:tcBdr`
- Specification: §20.1.4.2.33 p.2832

### a:txBody

- Type: `a:CT_TextBody`
- Children: `a:bodyPr`, `a:lstStyle`, `a:p`
- Parents: `a:tc`, `a:txSp`
- Specification: §20.1.2.2.40 p.2764

### a:txDef

- Type: `a:CT_DefaultShapeDefinition`
- Children: `a:spPr`, `a:bodyPr`, `a:lstStyle`, `a:style`, `a:extLst`
- Parents: `a:objectDefaults`
- Specification: §20.1.4.1.28 p.2811

### a:txSp

- Type: `a:CT_GvmlTextShape`
- Children: `a:txBody`, `a:useSpRect`, `a:xfrm`, `a:extLst`
- Parents: `a:grpSp`, `a:sp`, `lc:lockedCanvas`
- Specification: §20.1.2.2.41 p.2764

### a:uFill

- Type: `a:CT_TextUnderlineFillGroupWrapper`
- Children: `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`
- Parents: `a:defRPr`, `a:endParaRPr`, `a:rPr`, `cs:defRPr`
- Specification: §21.1.2.3.12 p.3265

### a:uFillTx

- Type: `a:CT_TextUnderlineFillFollowText`
- Parents: `a:defRPr`, `a:endParaRPr`, `a:rPr`, `cs:defRPr`
- Specification: §21.1.2.3.13 p.3265, OI 2.1.1401

### a:uLn

- Type: `a:CT_LineProperties`
- Attributes: `algn`, `cap`, `cmpd`, `w`
- Children: `a:noFill`, `a:solidFill`, `a:gradFill`, `a:pattFill`, `a:prstDash`, `a:custDash`, `a:round`, `a:bevel`, `a:miter`, `a:headEnd`, `a:tailEnd`, `a:extLst`
- Parents: `a:defRPr`, `a:endParaRPr`, `a:rPr`, `cs:defRPr`
- Specification: §21.1.2.3.14 p.3266, OI 2.1.1402

### a:uLnTx

- Type: `a:CT_TextUnderlineLineFollowText`
- Parents: `a:defRPr`, `a:endParaRPr`, `a:rPr`, `cs:defRPr`
- Specification: §21.1.2.3.15 p.3267

### a:up

- Type: `a:CT_Vector3D`
- Attributes: `dx`!, `dy`!, `dz`!
- Parents: `a:backdrop`
- Specification: §20.1.5.13 p.2846, OI 2.1.1277

### a:useSpRect

- Type: `a:CT_GvmlUseShapeRectangle`
- Parents: `a:txSp`
- Specification: §20.1.2.2.42 p.2765

### a:videoFile

- Type: `a:CT_VideoFile`
- Attributes: `contentType`, `r:link`!
- Children: `a:extLst`
- Parents: `p14:nvPr`, `p:nvPr`
- Specification: §20.1.3.6 p.2791, OI 2.1.1247

### a:wavAudioFile

- Type: `a:CT_EmbeddedWAVAudioFile`
- Attributes: `name`, `r:embed`!
- Parents: `p14:nvPr`, `p:nvPr`
- Specification: §20.1.3.7 p.2792

### a:wholeTbl

- Type: `a:CT_TablePartStyle`
- Children: `a:tcTxStyle`, `a:tcStyle`
- Parents: `a:tableStyle`, `a:tblStyle`
- Specification: §20.1.4.2.34 p.2833

### a:xfrm

- Type: `a:CT_Transform2D`, `a:CT_TransformEffect`, `a:CT_GroupTransform2D`
- Attributes: `flipH`, `flipV`, `kx`, `ky`, `rot`, `sx`, `sy`, `tx`, `ty`
- Children: `a:off`, `a:ext`, `a:chOff`, `a:chExt`
- Parents: `a:cont`, `a:effectDag`, `a:graphicFrame`, `a:grpSpPr`, `a:spPr`, `a:txSp`, `am3d:spPr`, `c14:spPr`, `c15:spPr`, `c16:spPr`, `c:spPr`, `cdr:grpSpPr`, `cdr:spPr`, `cs:spPr`, `cx:spPr`, `dgm1612:spPr`, `dgm:spPr`, `dsp:grpSpPr`, `dsp:spPr`, `p166:spPr`, `p:grpSpPr`, `p:spPr`, `pic:spPr`, `wp:grpSpPr`, `wp:spPr`, `wpg:grpSpPr`, `wps:spPr`, `xdr:grpSpPr`, `xdr:spPr`
- Specification: §20.1.7.5 p.2860, OI 2.1.1282; §20.1.7.6 p.2861, OI 2.1.1283; §20.1.8.61 p.2895

`!` marks required attributes.
