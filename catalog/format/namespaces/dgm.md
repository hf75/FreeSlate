# dgm: Diagrams (SmartArt)

`http://schemas.openxmlformats.org/drawingml/2006/diagram`

- Source: ECMA-376-4 transitional schema dml-diagram.xsd
- Office version: 2007
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 65 | 159 | 0 | 58 | 66 | 331 | 140 | 27 |

Full detail (content models, attribute types, defaults): [dgm.json](dgm.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`dgm:adj`](#dgmadj) | `dgm:CT_Adj` | 2 | 0 | 1 | §21.4.2.1 p.3508, OI 2.1.1615 |
| [`dgm:adjLst`](#dgmadjlst) | `dgm:CT_AdjLst` | 0 | 1 | 1 | §21.4.2.2 p.3508 |
| [`dgm:alg`](#dgmalg) | `dgm:CT_Algorithm` | 2 | 2 | 4 | §21.4.2.3 p.3509 |
| [`dgm:animLvl`](#dgmanimlvl) | `dgm:CT_AnimLvl` | 1 | 0 | 2 | §21.4.6.1 p.3566 |
| [`dgm:animOne`](#dgmanimone) | `dgm:CT_AnimOne` | 1 | 0 | 2 | §21.4.6.2 p.3566 |
| [`dgm:bg`](#dgmbg) | `a:CT_BackgroundFormatting` | 0 | 8 | 1 | §21.4.3.1 p.3536 |
| [`dgm:bulletEnabled`](#dgmbulletenabled) | `dgm:CT_BulletEnabled` | 1 | 0 | 2 | §21.4.6.3 p.3567 |
| [`dgm:cat`](#dgmcat) | `dgm:CT_CTCategory`, `dgm:CT_Category`, `dgm:CT_SDCategory` | 2 | 0 | 1 | §21.4.2.4 p.3509; §21.4.4.1 p.3546; §21.4.5.1 p.3557 |
| [`dgm:catLst`](#dgmcatlst) | `dgm:CT_CTCategories`, `dgm:CT_Categories`, `dgm:CT_SDCategories` | 0 | 1 | 6 | §21.4.2.5 p.3510; §21.4.4.2 p.3547; §21.4.5.2 p.3558 |
| [`dgm:chMax`](#dgmchmax) | `dgm:CT_ChildMax` | 1 | 0 | 2 | §21.4.6.4 p.3568 |
| [`dgm:chPref`](#dgmchpref) | `dgm:CT_ChildPref` | 1 | 0 | 2 | §21.4.6.5 p.3568 |
| [`dgm:choose`](#dgmchoose) | `dgm:CT_Choose` | 1 | 2 | 4 | §21.4.2.6 p.3510 |
| [`dgm:clrData`](#dgmclrdata) | `dgm:CT_SampleData` | 1 | 1 | 1 | §21.4.2.7 p.3511, OI 2.1.1616 |
| [`dgm:colorsDef`](#dgmcolorsdef) ◆ | `dgm:CT_ColorTransform` | 2 | 5 | 0 | §21.4.4.3 p.3547, OI 2.1.1628 |
| [`dgm:colorsDefHdr`](#dgmcolorsdefhdr) ◆ | `dgm:CT_ColorTransformHeader` | 3 | 4 | 1 | §21.4.4.4 p.3548 |
| [`dgm:colorsDefHdrLst`](#dgmcolorsdefhdrlst) ◆ | `dgm:CT_ColorTransformHeaderLst` | 0 | 1 | 0 | §21.4.4.5 p.3549 |
| [`dgm:constr`](#dgmconstr) | `dgm:CT_Constraint` | 11 | 1 | 1 | §21.4.2.8 p.3512 |
| [`dgm:constrLst`](#dgmconstrlst) | `dgm:CT_Constraints` | 0 | 1 | 4 | §21.4.2.9 p.3516 |
| [`dgm:cxn`](#dgmcxn) | `dgm:CT_Cxn` | 9 | 1 | 1 | §21.4.3.2 p.3536, OI 2.1.1623 |
| [`dgm:cxnLst`](#dgmcxnlst) | `dgm:CT_CxnList` | 0 | 1 | 1 | §21.4.3.3 p.3539 |
| [`dgm:dataModel`](#dgmdatamodel) ◆ | `dgm:CT_DataModel` | 0 | 5 | 3 | §21.4.2.10 p.3516 |
| [`dgm:desc`](#dgmdesc) | `dgm:CT_CTDescription`, `dgm:CT_Description`, `dgm:CT_SDDescription` | 2 | 0 | 6 | §21.4.2.11 p.3516; §21.4.4.6 p.3549; §21.4.5.3 p.3558 |
| [`dgm:dir`](#dgmdir) | `dgm:CT_Direction` | 1 | 0 | 2 | §21.4.6.6 p.3569 |
| [`dgm:effectClrLst`](#dgmeffectclrlst) | `dgm:CT_Colors` | 2 | 6 | 1 | §21.4.4.7 p.3550, OI 2.1.1629 |
| [`dgm:else`](#dgmelse) | `dgm:CT_Otherwise` | 1 | 9 | 1 | §21.4.2.12 p.3517 |
| [`dgm:extLst`](#dgmextlst) | `a:CT_OfficeArtExtensionList` | 0 | 1 | 19 | §21.4.2.13 p.3518, OI 2.1.1617 |
| [`dgm:fillClrLst`](#dgmfillclrlst) | `dgm:CT_Colors` | 2 | 6 | 1 | §21.4.4.8 p.3551, OI 2.1.1630 |
| [`dgm:forEach`](#dgmforeach) | `dgm:CT_ForEach` | 8 | 9 | 4 | §21.4.2.14 p.3518, OI 2.1.1618 |
| [`dgm:hierBranch`](#dgmhierbranch) | `dgm:CT_HierBranchStyle` | 1 | 0 | 2 | §21.4.6.7 p.3569 |
| [`dgm:if`](#dgmif) | `dgm:CT_When` | 11 | 9 | 1 | §21.4.2.15 p.3520 |
| [`dgm:layoutDef`](#dgmlayoutdef) ◆ | `dgm:CT_DiagramDefinition` | 3 | 8 | 0 | §21.4.2.16 p.3523 |
| [`dgm:layoutDefHdr`](#dgmlayoutdefhdr) ◆ | `dgm:CT_DiagramDefinitionHeader` | 4 | 4 | 1 | §21.4.2.17 p.3524 |
| [`dgm:layoutDefHdrLst`](#dgmlayoutdefhdrlst) ◆ | `dgm:CT_DiagramDefinitionHeaderLst` | 0 | 1 | 0 | §21.4.2.18 p.3525 |
| [`dgm:layoutNode`](#dgmlayoutnode) | `dgm:CT_LayoutNode` | 4 | 10 | 5 | §21.4.2.19 p.3525 |
| [`dgm:linClrLst`](#dgmlinclrlst) | `dgm:CT_Colors` | 2 | 6 | 1 | §21.4.4.9 p.3552, OI 2.1.1631 |
| [`dgm:orgChart`](#dgmorgchart) | `dgm:CT_OrgChart` | 1 | 0 | 2 | §21.4.6.8 p.3570 |
| [`dgm:param`](#dgmparam) | `dgm:CT_Parameter` | 2 | 0 | 1 | §21.4.2.20 p.3526, OI 2.1.1619 |
| [`dgm:prSet`](#dgmprset) | `dgm:CT_ElemPropSet` | 28 | 2 | 1 | §21.4.3.4 p.3540, OI 2.1.1624 |
| [`dgm:presLayoutVars`](#dgmpreslayoutvars) | `dgm:CT_LayoutVariablePropertySet` | 0 | 9 | 1 | §21.4.5.4 p.3559 |
| [`dgm:presOf`](#dgmpresof) | `dgm:CT_PresentationOf` | 6 | 1 | 4 | §21.4.2.21 p.3526 |
| [`dgm:pt`](#dgmpt) | `dgm:CT_Pt` | 3 | 4 | 1 | §21.4.3.5 p.3543, OI 2.1.1625 |
| [`dgm:ptLst`](#dgmptlst) | `dgm:CT_PtList` | 0 | 1 | 1 | §21.4.3.6 p.3545 |
| [`dgm:relIds`](#dgmrelids) ◆ | `dgm:CT_RelIds` | 4 | 0 | 0 | §21.4.2.22 p.3528 |
| [`dgm:resizeHandles`](#dgmresizehandles) | `dgm:CT_ResizeHandles` | 1 | 0 | 2 | §21.4.2.23 p.3529 |
| [`dgm:rule`](#dgmrule) | `dgm:CT_NumericRule` | 7 | 1 | 1 | §21.4.2.24 p.3530 |
| [`dgm:ruleLst`](#dgmrulelst) | `dgm:CT_Rules` | 0 | 1 | 4 | §21.4.2.25 p.3532 |
| [`dgm:sampData`](#dgmsampdata) | `dgm:CT_SampleData` | 1 | 1 | 1 | §21.4.2.26 p.3532, OI 2.1.1620 |
| [`dgm:scene3d`](#dgmscene3d) | `a:CT_Scene3D` | 0 | 4 | 2 | §21.4.5.5 p.3559 |
| [`dgm:shape`](#dgmshape) | `dgm:CT_Shape` | 7 | 2 | 4 | §21.4.2.27 p.3533, OI 2.1.1621 |
| [`dgm:sp3d`](#dgmsp3d) | `a:CT_Shape3D` | 4 | 5 | 1 | §21.4.5.6 p.3559, OI 2.1.1635 |
| [`dgm:spPr`](#dgmsppr) | `a:CT_ShapeProperties` | 1 | 15 | 1 | §21.4.3.7 p.3545, OI 2.1.1626 |
| [`dgm:style`](#dgmstyle) | `a:CT_ShapeStyle` | 0 | 4 | 2 | §21.4.2.28 p.3534 |
| [`dgm:styleData`](#dgmstyledata) | `dgm:CT_SampleData` | 1 | 1 | 1 | §21.4.2.29 p.3534, OI 2.1.1622 |
| [`dgm:styleDef`](#dgmstyledef) ◆ | `dgm:CT_StyleDefinition` | 2 | 6 | 0 | §21.4.5.7 p.3561 |
| [`dgm:styleDefHdr`](#dgmstyledefhdr) ◆ | `dgm:CT_StyleDefinitionHeader` | 3 | 4 | 1 | §21.4.5.8 p.3563 |
| [`dgm:styleDefHdrLst`](#dgmstyledefhdrlst) ◆ | `dgm:CT_StyleDefinitionHeaderLst` | 0 | 1 | 0 | §21.4.5.9 p.3564 |
| [`dgm:styleLbl`](#dgmstylelbl) | `dgm:CT_CTStyleLabel`, `dgm:CT_StyleLabel` | 1 | 11 | 2 | §21.4.4.10 p.3553; §21.4.5.10 p.3564, OI 2.1.1636 |
| [`dgm:t`](#dgmt) | `a:CT_TextBody` | 0 | 3 | 1 | §21.4.3.8 p.3546, OI 2.1.1627 |
| [`dgm:title`](#dgmtitle) | `dgm:CT_CTName`, `dgm:CT_Name`, `dgm:CT_SDName` | 2 | 0 | 6 | §21.4.2.30 p.3535; §21.4.4.11 p.3554; §21.4.5.11 p.3565 |
| [`dgm:txEffectClrLst`](#dgmtxeffectclrlst) | `dgm:CT_Colors` | 2 | 6 | 1 | §21.4.4.12 p.3554, OI 2.1.1632 |
| [`dgm:txFillClrLst`](#dgmtxfillclrlst) | `dgm:CT_Colors` | 2 | 6 | 1 | §21.4.4.13 p.3555, OI 2.1.1633 |
| [`dgm:txLinClrLst`](#dgmtxlinclrlst) | `dgm:CT_Colors` | 2 | 6 | 1 | §21.4.4.14 p.3556, OI 2.1.1634 |
| [`dgm:txPr`](#dgmtxpr) | `dgm:CT_TextProps` | 0 | 2 | 1 | §21.4.5.12 p.3566 |
| [`dgm:varLst`](#dgmvarlst) | `dgm:CT_LayoutVariablePropertySet` | 0 | 9 | 1 | §21.4.2.31 p.3535 |
| [`dgm:whole`](#dgmwhole) | `a:CT_WholeE2oFormatting` | 0 | 3 | 1 | §21.4.3.9 p.3546 |

◆ declared globally (can be the root element of a part)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `dgm:ST_AlgorithmType` | restriction of xsd:token | `composite`, `conn`, `cycle`, `hierChild`, `hierRoot`, `pyra`, `lin`, `sp`, `tx`, `snake` | §21.4.7.1 p.3570 |
| `dgm:ST_AnimLvlStr` | restriction of xsd:token | `none`, `lvl`, `ctr` | §21.4.7.2 p.3571 |
| `dgm:ST_AnimOneStr` | restriction of xsd:token | `none`, `one`, `branch` | §21.4.7.3 p.3572, OI 2.1.1637 |
| `dgm:ST_ArrowheadStyle` | restriction of xsd:token | `auto`, `arr`, `noArr` | §21.4.7.4 p.3572 |
| `dgm:ST_AutoTextRotation` | restriction of xsd:token | `none`, `upr`, `grav` | §21.4.7.5 p.3573 |
| `dgm:ST_AxisType` | restriction of xsd:token | `self`, `ch`, `des`, `desOrSelf`, `par`, `ancst`, `ancstOrSelf`, `followSib`, `precedSib`, `follow`, `preced`, `root` … (+1) | §21.4.7.6 p.3573, OI 2.1.1638 |
| `dgm:ST_AxisTypes` | list of dgm:ST_AxisType |  | §21.4.7.7 p.3579 |
| `dgm:ST_BendPoint` | restriction of xsd:token | `beg`, `def`, `end` | §21.4.7.8 p.3579 |
| `dgm:ST_BoolOperator` | restriction of xsd:token | `none`, `equ`, `gte`, `lte` | §21.4.7.10 p.3580 |
| `dgm:ST_Booleans` | list of xsd:boolean |  | §21.4.7.9 p.3580 |
| `dgm:ST_Breakpoint` | restriction of xsd:token | `endCnv`, `bal`, `fixed` | §21.4.7.11 p.3580 |
| `dgm:ST_CenterShapeMapping` | restriction of xsd:token | `none`, `fNode` | §21.4.7.12 p.3581 |
| `dgm:ST_ChildAlignment` | restriction of xsd:token | `t`, `b`, `l`, `r` | §21.4.7.13 p.3581 |
| `dgm:ST_ChildDirection` | restriction of xsd:token | `horz`, `vert` | §21.4.7.14 p.3581 |
| `dgm:ST_ChildOrderType` | restriction of xsd:token | `b`, `t` | §21.4.7.15 p.3582 |
| `dgm:ST_ClrAppMethod` | restriction of xsd:token | `span`, `cycle`, `repeat` | §21.4.7.16 p.3582 |
| `dgm:ST_ConnectorDimension` | restriction of xsd:token | `1D`, `2D`, `cust` | §21.4.7.17 p.3584 |
| `dgm:ST_ConnectorPoint` | restriction of xsd:token | `auto`, `bCtr`, `ctr`, `midL`, `midR`, `tCtr`, `bL`, `bR`, `tL`, `tR`, `radial` | §21.4.7.18 p.3584 |
| `dgm:ST_ConnectorRouting` | restriction of xsd:token | `stra`, `bend`, `curve`, `longCurve` | §21.4.7.19 p.3585 |
| `dgm:ST_ConstraintRelationship` | restriction of xsd:token | `self`, `ch`, `des` | §21.4.7.20 p.3586 |
| `dgm:ST_ConstraintType` | restriction of xsd:token | `none`, `alignOff`, `begMarg`, `bendDist`, `begPad`, `b`, `bMarg`, `bOff`, `ctrX`, `ctrXOff`, `ctrY`, `ctrYOff` … (+52) | §21.4.7.21 p.3586 |
| `dgm:ST_ContinueDirection` | restriction of xsd:token | `revDir`, `sameDir` | §21.4.7.22 p.3588 |
| `dgm:ST_CxnType` | restriction of xsd:token | `parOf`, `presOf`, `presParOf`, `unknownRelationship` | §21.4.7.23 p.3589 |
| `dgm:ST_DiagramHorizontalAlignment` | restriction of xsd:token | `l`, `ctr`, `r`, `none` | §21.4.7.24 p.3590 |
| `dgm:ST_DiagramTextAlignment` | restriction of xsd:token | `l`, `ctr`, `r` | §21.4.7.25 p.3590 |
| `dgm:ST_Direction` | restriction of xsd:token | `norm`, `rev` | §21.4.7.26 p.3591 |
| `dgm:ST_ElementType` | restriction of xsd:token | `all`, `doc`, `node`, `norm`, `nonNorm`, `asst`, `nonAsst`, `parTrans`, `pres`, `sibTrans` | §21.4.7.27 p.3591 |
| `dgm:ST_ElementTypes` | list of dgm:ST_ElementType |  | §21.4.7.28 p.3592 |
| `dgm:ST_FallbackDimension` | restriction of xsd:token | `1D`, `2D` | §21.4.7.29 p.3592 |
| `dgm:ST_FlowDirection` | restriction of xsd:token | `row`, `col` | §21.4.7.30 p.3592 |
| `dgm:ST_FunctionArgument` | union of dgm:ST_VariableType |  | §21.4.7.31 p.3593 |
| `dgm:ST_FunctionOperator` | restriction of xsd:token | `equ`, `neq`, `gt`, `lt`, `gte`, `lte` | §21.4.7.32 p.3593 |
| `dgm:ST_FunctionType` | restriction of xsd:token | `cnt`, `pos`, `revPos`, `posEven`, `posOdd`, `var`, `depth`, `maxDepth` | §21.4.7.33 p.3593 |
| `dgm:ST_FunctionValue` | union of xsd:int, xsd:boolean, dgm:ST_Direction, dgm:ST_HierBranchStyle, dgm:ST_AnimOneStr, dgm:ST_AnimLvlStr, dgm:ST_ResizeHandlesStr |  | §21.4.7.34 p.3594 |
| `dgm:ST_GrowDirection` | restriction of xsd:token | `tL`, `tR`, `bL`, `bR` | §21.4.7.35 p.3594 |
| `dgm:ST_HierBranchStyle` | restriction of xsd:token | `l`, `r`, `hang`, `std`, `init` | §21.4.7.37 p.3596 |
| `dgm:ST_HierarchyAlignment` | restriction of xsd:token | `tL`, `tR`, `tCtrCh`, `tCtrDes`, `bL`, `bR`, `bCtrCh`, `bCtrDes`, `lT`, `lB`, `lCtrCh`, `lCtrDes` … (+4) | §21.4.7.36 p.3595 |
| `dgm:ST_HueDir` | restriction of xsd:token | `cw`, `ccw` | §21.4.7.38 p.3598 |
| `dgm:ST_Index1` | restriction of xsd:unsignedInt | minInclusive `1` | §21.4.7.39 p.3598 |
| `dgm:ST_Ints` | list of xsd:int |  | §21.4.7.40 p.3598 |
| `dgm:ST_LayoutShapeType` | union of a:ST_ShapeType, dgm:ST_OutputShapeType |  | §21.4.7.41 p.3598 |
| `dgm:ST_LinearDirection` | restriction of xsd:token | `fromL`, `fromR`, `fromT`, `fromB` | §21.4.7.42 p.3599 |
| `dgm:ST_ModelId` | union of xsd:int, s:ST_Guid |  | §21.4.7.43 p.3599 |
| `dgm:ST_NodeCount` | restriction of xsd:int | minInclusive `-1` | §21.4.7.44 p.3599 |
| `dgm:ST_NodeHorizontalAlignment` | restriction of xsd:token | `l`, `ctr`, `r` | §21.4.7.45 p.3600 |
| `dgm:ST_NodeVerticalAlignment` | restriction of xsd:token | `t`, `mid`, `b` | §21.4.7.46 p.3600 |
| `dgm:ST_Offset` | restriction of xsd:token | `ctr`, `off` | §21.4.7.47 p.3600 |
| `dgm:ST_OutputShapeType` | restriction of xsd:token | `none`, `conn` | §21.4.7.48 p.3601 |
| `dgm:ST_ParameterId` | restriction of xsd:token | `horzAlign`, `vertAlign`, `chDir`, `chAlign`, `secChAlign`, `linDir`, `secLinDir`, `stElem`, `bendPt`, `connRout`, `begSty`, `endSty` … (+43) | §21.4.7.49 p.3601, OI 2.1.1639 |
| `dgm:ST_ParameterVal` | union of dgm:ST_DiagramHorizontalAlignment, dgm:ST_VerticalAlignment, dgm:ST_ChildDirection, dgm:ST_ChildAlignment, dgm:ST_SecondaryChildAlignment, dgm:ST_LinearDirection, dgm:ST_SecondaryLinearDirection, dgm:ST_StartingElement, dgm:ST_BendPoint, dgm:ST_ConnectorRouting, dgm:ST_ArrowheadStyle, dgm:ST_ConnectorDimension, dgm:ST_RotationPath, dgm:ST_CenterShapeMapping, dgm:ST_NodeHorizontalAlignment, dgm:ST_NodeVerticalAlignment, dgm:ST_FallbackDimension, dgm:ST_TextDirection, dgm:ST_PyramidAccentPosition, dgm:ST_PyramidAccentTextMargin, dgm:ST_TextBlockDirection, dgm:ST_TextAnchorHorizontal, dgm:ST_TextAnchorVertical, dgm:ST_DiagramTextAlignment, dgm:ST_AutoTextRotation, dgm:ST_GrowDirection, dgm:ST_FlowDirection, dgm:ST_ContinueDirection, dgm:ST_Breakpoint, dgm:ST_Offset, dgm:ST_HierarchyAlignment, xsd:int, xsd:double, xsd:boolean, xsd:string, dgm:ST_ConnectorPoint |  | §21.4.7.50 p.3604 |
| `dgm:ST_PrSetCustVal` | union of s:ST_Percentage, xsd:int |  | §21.4.7.66 p.3612 |
| `dgm:ST_PtType` | restriction of xsd:token | `node`, `asst`, `doc`, `pres`, `parTrans`, `sibTrans` | §21.4.7.51 p.3606 |
| `dgm:ST_PyramidAccentPosition` | restriction of xsd:token | `bef`, `aft` | §21.4.7.52 p.3606 |
| `dgm:ST_PyramidAccentTextMargin` | restriction of xsd:token | `step`, `stack` | §21.4.7.53 p.3606 |
| `dgm:ST_ResizeHandlesStr` | restriction of xsd:token | `exact`, `rel` | §21.4.7.54 p.3608 |
| `dgm:ST_RotationPath` | restriction of xsd:token | `none`, `alongPath` | §21.4.7.55 p.3608 |
| `dgm:ST_SecondaryChildAlignment` | restriction of xsd:token | `none`, `t`, `b`, `l`, `r` | §21.4.7.56 p.3608, OI 2.1.1640 |
| `dgm:ST_SecondaryLinearDirection` | restriction of xsd:token | `none`, `fromL`, `fromR`, `fromT`, `fromB` | §21.4.7.57 p.3609, OI 2.1.1641 |
| `dgm:ST_StartingElement` | restriction of xsd:token | `node`, `trans` | §21.4.7.58 p.3609 |
| `dgm:ST_TextAnchorHorizontal` | restriction of xsd:token | `none`, `ctr` | §21.4.7.59 p.3610 |
| `dgm:ST_TextAnchorVertical` | restriction of xsd:token | `t`, `mid`, `b` | §21.4.7.60 p.3610 |
| `dgm:ST_TextBlockDirection` | restriction of xsd:token | `horz`, `vert` | §21.4.7.61 p.3610 |
| `dgm:ST_TextDirection` | restriction of xsd:token | `fromT`, `fromB` | §21.4.7.62 p.3611 |
| `dgm:ST_UnsignedInts` | list of xsd:unsignedInt |  | §21.4.7.63 p.3611 |
| `dgm:ST_VariableType` | restriction of xsd:token | `none`, `orgChart`, `chMax`, `chPref`, `bulEnabled`, `dir`, `hierBranch`, `animOne`, `animLvl`, `resizeHandles` | §21.4.7.64 p.3611 |
| `dgm:ST_VerticalAlignment` | restriction of xsd:token | `t`, `mid`, `b`, `none` | §21.4.7.65 p.3612 |

## Element details

### dgm:adj

- Type: `dgm:CT_Adj`
- Attributes: `idx`!, `val`!
- Parents: `dgm:adjLst`
- Specification: §21.4.2.1 p.3508, OI 2.1.1615

### dgm:adjLst

- Type: `dgm:CT_AdjLst`
- Children: `dgm:adj`
- Parents: `dgm:shape`
- Specification: §21.4.2.2 p.3508

### dgm:alg

- Type: `dgm:CT_Algorithm`
- Attributes: `rev`, `type`!
- Children: `dgm:param`, `dgm:extLst`
- Parents: `dgm:else`, `dgm:forEach`, `dgm:if`, `dgm:layoutNode`
- Specification: §21.4.2.3 p.3509

### dgm:animLvl

- Type: `dgm:CT_AnimLvl`
- Attributes: `val`
- Parents: `dgm:presLayoutVars`, `dgm:varLst`
- Specification: §21.4.6.1 p.3566

### dgm:animOne

- Type: `dgm:CT_AnimOne`
- Attributes: `val`
- Parents: `dgm:presLayoutVars`, `dgm:varLst`
- Specification: §21.4.6.2 p.3566

### dgm:bg

- Type: `a:CT_BackgroundFormatting`
- Children: `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`, `a:effectLst`, `a:effectDag`
- Parents: `dgm:dataModel`
- Specification: §21.4.3.1 p.3536

### dgm:bulletEnabled

- Type: `dgm:CT_BulletEnabled`
- Attributes: `val`
- Parents: `dgm:presLayoutVars`, `dgm:varLst`
- Specification: §21.4.6.3 p.3567

### dgm:cat

- Type: `dgm:CT_CTCategory`, `dgm:CT_Category`, `dgm:CT_SDCategory`
- Attributes: `pri`!, `type`!
- Parents: `dgm:catLst`
- Specification: §21.4.2.4 p.3509; §21.4.4.1 p.3546; §21.4.5.1 p.3557

### dgm:catLst

- Type: `dgm:CT_CTCategories`, `dgm:CT_Categories`, `dgm:CT_SDCategories`
- Children: `dgm:cat`
- Parents: `dgm:colorsDef`, `dgm:colorsDefHdr`, `dgm:layoutDef`, `dgm:layoutDefHdr`, `dgm:styleDef`, `dgm:styleDefHdr`
- Specification: §21.4.2.5 p.3510; §21.4.4.2 p.3547; §21.4.5.2 p.3558

### dgm:chMax

- Type: `dgm:CT_ChildMax`
- Attributes: `val`
- Parents: `dgm:presLayoutVars`, `dgm:varLst`
- Specification: §21.4.6.4 p.3568

### dgm:chPref

- Type: `dgm:CT_ChildPref`
- Attributes: `val`
- Parents: `dgm:presLayoutVars`, `dgm:varLst`
- Specification: §21.4.6.5 p.3568

### dgm:choose

- Type: `dgm:CT_Choose`
- Attributes: `name`
- Children: `dgm:if`, `dgm:else`
- Parents: `dgm:else`, `dgm:forEach`, `dgm:if`, `dgm:layoutNode`
- Specification: §21.4.2.6 p.3510

### dgm:clrData

- Type: `dgm:CT_SampleData`
- Attributes: `useDef`
- Children: `dgm:dataModel`
- Parents: `dgm:layoutDef`
- Specification: §21.4.2.7 p.3511, OI 2.1.1616

### dgm:colorsDef

- Type: `dgm:CT_ColorTransform`
- Attributes: `minVer`, `uniqueId`
- Children: `dgm:title`, `dgm:desc`, `dgm:catLst`, `dgm:styleLbl`, `dgm:extLst`
- Specification: §21.4.4.3 p.3547, OI 2.1.1628

### dgm:colorsDefHdr

- Type: `dgm:CT_ColorTransformHeader`
- Attributes: `minVer`, `resId`, `uniqueId`!
- Children: `dgm:title`, `dgm:desc`, `dgm:catLst`, `dgm:extLst`
- Parents: `dgm:colorsDefHdrLst`
- Specification: §21.4.4.4 p.3548

### dgm:colorsDefHdrLst

- Type: `dgm:CT_ColorTransformHeaderLst`
- Children: `dgm:colorsDefHdr`
- Specification: §21.4.4.5 p.3549

### dgm:constr

- Type: `dgm:CT_Constraint`
- Attributes: `fact`, `for`, `forName`, `op`, `ptType`, `refFor`, `refForName`, `refPtType`, `refType`, `type`!, `val`
- Children: `dgm:extLst`
- Parents: `dgm:constrLst`
- Specification: §21.4.2.8 p.3512

### dgm:constrLst

- Type: `dgm:CT_Constraints`
- Children: `dgm:constr`
- Parents: `dgm:else`, `dgm:forEach`, `dgm:if`, `dgm:layoutNode`
- Specification: §21.4.2.9 p.3516

### dgm:cxn

- Type: `dgm:CT_Cxn`
- Attributes: `destId`!, `destOrd`!, `modelId`!, `parTransId`, `presId`, `sibTransId`, `srcId`!, `srcOrd`!, `type`
- Children: `dgm:extLst`
- Parents: `dgm:cxnLst`
- Specification: §21.4.3.2 p.3536, OI 2.1.1623

### dgm:cxnLst

- Type: `dgm:CT_CxnList`
- Children: `dgm:cxn`
- Parents: `dgm:dataModel`
- Specification: §21.4.3.3 p.3539

### dgm:dataModel

- Type: `dgm:CT_DataModel`
- Children: `dgm:ptLst`, `dgm:cxnLst`, `dgm:bg`, `dgm:whole`, `dgm:extLst`
- Parents: `dgm:clrData`, `dgm:sampData`, `dgm:styleData`
- Specification: §21.4.2.10 p.3516

### dgm:desc

- Type: `dgm:CT_CTDescription`, `dgm:CT_Description`, `dgm:CT_SDDescription`
- Attributes: `lang`, `val`!
- Parents: `dgm:colorsDef`, `dgm:colorsDefHdr`, `dgm:layoutDef`, `dgm:layoutDefHdr`, `dgm:styleDef`, `dgm:styleDefHdr`
- Specification: §21.4.2.11 p.3516; §21.4.4.6 p.3549; §21.4.5.3 p.3558

### dgm:dir

- Type: `dgm:CT_Direction`
- Attributes: `val`
- Parents: `dgm:presLayoutVars`, `dgm:varLst`
- Specification: §21.4.6.6 p.3569

### dgm:effectClrLst

- Type: `dgm:CT_Colors`
- Attributes: `hueDir`, `meth`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `dgm:styleLbl`
- Specification: §21.4.4.7 p.3550, OI 2.1.1629

### dgm:else

- Type: `dgm:CT_Otherwise`
- Attributes: `name`
- Children: `dgm:alg`, `dgm:shape`, `dgm:presOf`, `dgm:constrLst`, `dgm:ruleLst`, `dgm:forEach`, `dgm:layoutNode`, `dgm:choose`, `dgm:extLst`
- Parents: `dgm:choose`
- Specification: §21.4.2.12 p.3517

### dgm:extLst

- Type: `a:CT_OfficeArtExtensionList`
- Children: `a:ext`
- Parents: `dgm:alg`, `dgm:colorsDef`, `dgm:colorsDefHdr`, `dgm:constr`, `dgm:cxn`, `dgm:dataModel`, `dgm:else`, `dgm:forEach`, `dgm:if`, `dgm:layoutDef`, `dgm:layoutDefHdr`, `dgm:layoutNode`, `dgm:presOf`, `dgm:pt`, `dgm:rule`, `dgm:shape`, `dgm:styleDef`, `dgm:styleDefHdr`, `dgm:styleLbl`
- Specification: §21.4.2.13 p.3518, OI 2.1.1617

### dgm:fillClrLst

- Type: `dgm:CT_Colors`
- Attributes: `hueDir`, `meth`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `dgm:styleLbl`
- Specification: §21.4.4.8 p.3551, OI 2.1.1630

### dgm:forEach

- Type: `dgm:CT_ForEach`
- Attributes: `axis`, `cnt`, `hideLastTrans`, `name`, `ptType`, `ref`, `st`, `step`
- Children: `dgm:alg`, `dgm:shape`, `dgm:presOf`, `dgm:constrLst`, `dgm:ruleLst`, `dgm:forEach`, `dgm:layoutNode`, `dgm:choose`, `dgm:extLst`
- Parents: `dgm:else`, `dgm:forEach`, `dgm:if`, `dgm:layoutNode`
- Specification: §21.4.2.14 p.3518, OI 2.1.1618

### dgm:hierBranch

- Type: `dgm:CT_HierBranchStyle`
- Attributes: `val`
- Parents: `dgm:presLayoutVars`, `dgm:varLst`
- Specification: §21.4.6.7 p.3569

### dgm:if

- Type: `dgm:CT_When`
- Attributes: `arg`, `axis`, `cnt`, `func`!, `hideLastTrans`, `name`, `op`!, `ptType`, `st`, `step`, `val`!
- Children: `dgm:alg`, `dgm:shape`, `dgm:presOf`, `dgm:constrLst`, `dgm:ruleLst`, `dgm:forEach`, `dgm:layoutNode`, `dgm:choose`, `dgm:extLst`
- Parents: `dgm:choose`
- Specification: §21.4.2.15 p.3520

### dgm:layoutDef

- Type: `dgm:CT_DiagramDefinition`
- Attributes: `defStyle`, `minVer`, `uniqueId`
- Children: `dgm:title`, `dgm:desc`, `dgm:catLst`, `dgm:sampData`, `dgm:styleData`, `dgm:clrData`, `dgm:layoutNode`, `dgm:extLst`
- Specification: §21.4.2.16 p.3523

### dgm:layoutDefHdr

- Type: `dgm:CT_DiagramDefinitionHeader`
- Attributes: `defStyle`, `minVer`, `resId`, `uniqueId`!
- Children: `dgm:title`, `dgm:desc`, `dgm:catLst`, `dgm:extLst`
- Parents: `dgm:layoutDefHdrLst`
- Specification: §21.4.2.17 p.3524

### dgm:layoutDefHdrLst

- Type: `dgm:CT_DiagramDefinitionHeaderLst`
- Children: `dgm:layoutDefHdr`
- Specification: §21.4.2.18 p.3525

### dgm:layoutNode

- Type: `dgm:CT_LayoutNode`
- Attributes: `chOrder`, `moveWith`, `name`, `styleLbl`
- Children: `dgm:alg`, `dgm:shape`, `dgm:presOf`, `dgm:constrLst`, `dgm:ruleLst`, `dgm:varLst`, `dgm:forEach`, `dgm:layoutNode`, `dgm:choose`, `dgm:extLst`
- Parents: `dgm:else`, `dgm:forEach`, `dgm:if`, `dgm:layoutDef`, `dgm:layoutNode`
- Specification: §21.4.2.19 p.3525

### dgm:linClrLst

- Type: `dgm:CT_Colors`
- Attributes: `hueDir`, `meth`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `dgm:styleLbl`
- Specification: §21.4.4.9 p.3552, OI 2.1.1631

### dgm:orgChart

- Type: `dgm:CT_OrgChart`
- Attributes: `val`
- Parents: `dgm:presLayoutVars`, `dgm:varLst`
- Specification: §21.4.6.8 p.3570

### dgm:param

- Type: `dgm:CT_Parameter`
- Attributes: `type`!, `val`!
- Parents: `dgm:alg`
- Specification: §21.4.2.20 p.3526, OI 2.1.1619

### dgm:prSet

- Type: `dgm:CT_ElemPropSet`
- Attributes: `coherent3DOff`, `csCatId`, `csTypeId`, `custAng`, `custFlipHor`, `custFlipVert`, `custLinFactNeighborX`, `custLinFactNeighborY`, `custLinFactX`, `custLinFactY`, `custRadScaleInc`, `custRadScaleRad`, `custScaleX`, `custScaleY`, `custSzX`, `custSzY`, `custT`, `loCatId`, `loTypeId`, `phldr`, `phldrT`, `presAssocID`, `presName`, `presStyleCnt`, `presStyleIdx`, `presStyleLbl`, `qsCatId`, `qsTypeId`
- Children: `dgm:presLayoutVars`, `dgm:style`
- Parents: `dgm:pt`
- Specification: §21.4.3.4 p.3540, OI 2.1.1624

### dgm:presLayoutVars

- Type: `dgm:CT_LayoutVariablePropertySet`
- Children: `dgm:orgChart`, `dgm:chMax`, `dgm:chPref`, `dgm:bulletEnabled`, `dgm:dir`, `dgm:hierBranch`, `dgm:animOne`, `dgm:animLvl`, `dgm:resizeHandles`
- Parents: `dgm:prSet`
- Specification: §21.4.5.4 p.3559

### dgm:presOf

- Type: `dgm:CT_PresentationOf`
- Attributes: `axis`, `cnt`, `hideLastTrans`, `ptType`, `st`, `step`
- Children: `dgm:extLst`
- Parents: `dgm:else`, `dgm:forEach`, `dgm:if`, `dgm:layoutNode`
- Specification: §21.4.2.21 p.3526

### dgm:pt

- Type: `dgm:CT_Pt`
- Attributes: `cxnId`, `modelId`!, `type`
- Children: `dgm:prSet`, `dgm:spPr`, `dgm:t`, `dgm:extLst`
- Parents: `dgm:ptLst`
- Specification: §21.4.3.5 p.3543, OI 2.1.1625

### dgm:ptLst

- Type: `dgm:CT_PtList`
- Children: `dgm:pt`
- Parents: `dgm:dataModel`
- Specification: §21.4.3.6 p.3545

### dgm:relIds

- Type: `dgm:CT_RelIds`
- Attributes: `r:cs`!, `r:dm`!, `r:lo`!, `r:qs`!
- Specification: §21.4.2.22 p.3528

### dgm:resizeHandles

- Type: `dgm:CT_ResizeHandles`
- Attributes: `val`
- Parents: `dgm:presLayoutVars`, `dgm:varLst`
- Specification: §21.4.2.23 p.3529

### dgm:rule

- Type: `dgm:CT_NumericRule`
- Attributes: `fact`, `for`, `forName`, `max`, `ptType`, `type`!, `val`
- Children: `dgm:extLst`
- Parents: `dgm:ruleLst`
- Specification: §21.4.2.24 p.3530

### dgm:ruleLst

- Type: `dgm:CT_Rules`
- Children: `dgm:rule`
- Parents: `dgm:else`, `dgm:forEach`, `dgm:if`, `dgm:layoutNode`
- Specification: §21.4.2.25 p.3532

### dgm:sampData

- Type: `dgm:CT_SampleData`
- Attributes: `useDef`
- Children: `dgm:dataModel`
- Parents: `dgm:layoutDef`
- Specification: §21.4.2.26 p.3532, OI 2.1.1620

### dgm:scene3d

- Type: `a:CT_Scene3D`
- Children: `a:camera`, `a:lightRig`, `a:backdrop`, `a:extLst`
- Parents: `dgm:styleDef`, `dgm:styleLbl`
- Specification: §21.4.5.5 p.3559

### dgm:shape

- Type: `dgm:CT_Shape`
- Attributes: `blipPhldr`, `hideGeom`, `lkTxEntry`, `r:blip`, `rot`, `type`, `zOrderOff`
- Children: `dgm:adjLst`, `dgm:extLst`
- Parents: `dgm:else`, `dgm:forEach`, `dgm:if`, `dgm:layoutNode`
- Specification: §21.4.2.27 p.3533, OI 2.1.1621

### dgm:sp3d

- Type: `a:CT_Shape3D`
- Attributes: `contourW`, `extrusionH`, `prstMaterial`, `z`
- Children: `a:bevelT`, `a:bevelB`, `a:extrusionClr`, `a:contourClr`, `a:extLst`
- Parents: `dgm:styleLbl`
- Specification: §21.4.5.6 p.3559, OI 2.1.1635

### dgm:spPr

- Type: `a:CT_ShapeProperties`
- Attributes: `bwMode`
- Children: `a:xfrm`, `a:custGeom`, `a:prstGeom`, `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`, `a:ln`, `a:effectLst`, `a:effectDag`, `a:scene3d`, `a:sp3d`, `a:extLst`
- Parents: `dgm:pt`
- Specification: §21.4.3.7 p.3545, OI 2.1.1626

### dgm:style

- Type: `a:CT_ShapeStyle`
- Children: `a:lnRef`, `a:fillRef`, `a:effectRef`, `a:fontRef`
- Parents: `dgm:prSet`, `dgm:styleLbl`
- Specification: §21.4.2.28 p.3534

### dgm:styleData

- Type: `dgm:CT_SampleData`
- Attributes: `useDef`
- Children: `dgm:dataModel`
- Parents: `dgm:layoutDef`
- Specification: §21.4.2.29 p.3534, OI 2.1.1622

### dgm:styleDef

- Type: `dgm:CT_StyleDefinition`
- Attributes: `minVer`, `uniqueId`
- Children: `dgm:title`, `dgm:desc`, `dgm:catLst`, `dgm:scene3d`, `dgm:styleLbl`, `dgm:extLst`
- Specification: §21.4.5.7 p.3561

### dgm:styleDefHdr

- Type: `dgm:CT_StyleDefinitionHeader`
- Attributes: `minVer`, `resId`, `uniqueId`!
- Children: `dgm:title`, `dgm:desc`, `dgm:catLst`, `dgm:extLst`
- Parents: `dgm:styleDefHdrLst`
- Specification: §21.4.5.8 p.3563

### dgm:styleDefHdrLst

- Type: `dgm:CT_StyleDefinitionHeaderLst`
- Children: `dgm:styleDefHdr`
- Specification: §21.4.5.9 p.3564

### dgm:styleLbl

- Type: `dgm:CT_CTStyleLabel`, `dgm:CT_StyleLabel`
- Attributes: `name`!
- Children: `dgm:fillClrLst`, `dgm:linClrLst`, `dgm:effectClrLst`, `dgm:txLinClrLst`, `dgm:txFillClrLst`, `dgm:txEffectClrLst`, `dgm:extLst`, `dgm:scene3d`, `dgm:sp3d`, `dgm:txPr`, `dgm:style`
- Parents: `dgm:colorsDef`, `dgm:styleDef`
- Specification: §21.4.4.10 p.3553; §21.4.5.10 p.3564, OI 2.1.1636

### dgm:t

- Type: `a:CT_TextBody`
- Children: `a:bodyPr`, `a:lstStyle`, `a:p`
- Parents: `dgm:pt`
- Specification: §21.4.3.8 p.3546, OI 2.1.1627

### dgm:title

- Type: `dgm:CT_CTName`, `dgm:CT_Name`, `dgm:CT_SDName`
- Attributes: `lang`, `val`!
- Parents: `dgm:colorsDef`, `dgm:colorsDefHdr`, `dgm:layoutDef`, `dgm:layoutDefHdr`, `dgm:styleDef`, `dgm:styleDefHdr`
- Specification: §21.4.2.30 p.3535; §21.4.4.11 p.3554; §21.4.5.11 p.3565

### dgm:txEffectClrLst

- Type: `dgm:CT_Colors`
- Attributes: `hueDir`, `meth`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `dgm:styleLbl`
- Specification: §21.4.4.12 p.3554, OI 2.1.1632

### dgm:txFillClrLst

- Type: `dgm:CT_Colors`
- Attributes: `hueDir`, `meth`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `dgm:styleLbl`
- Specification: §21.4.4.13 p.3555, OI 2.1.1633

### dgm:txLinClrLst

- Type: `dgm:CT_Colors`
- Attributes: `hueDir`, `meth`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `dgm:styleLbl`
- Specification: §21.4.4.14 p.3556, OI 2.1.1634

### dgm:txPr

- Type: `dgm:CT_TextProps`
- Children: `a:sp3d`, `a:flatTx`
- Parents: `dgm:styleLbl`
- Specification: §21.4.5.12 p.3566

### dgm:varLst

- Type: `dgm:CT_LayoutVariablePropertySet`
- Children: `dgm:orgChart`, `dgm:chMax`, `dgm:chPref`, `dgm:bulletEnabled`, `dgm:dir`, `dgm:hierBranch`, `dgm:animOne`, `dgm:animLvl`, `dgm:resizeHandles`
- Parents: `dgm:layoutNode`
- Specification: §21.4.2.31 p.3535

### dgm:whole

- Type: `a:CT_WholeE2oFormatting`
- Children: `a:ln`, `a:effectLst`, `a:effectDag`
- Parents: `dgm:dataModel`
- Specification: §21.4.3.9 p.3546

`!` marks required attributes.
