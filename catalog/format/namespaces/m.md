# m: Math (OMML)

`http://schemas.openxmlformats.org/officeDocument/2006/math`

- Source: ECMA-376-4 transitional schema shared-math.xsd
- Office version: 2007
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 124 | 66 | 0 | 72 | 14 | 30 | 138 | 74 |

Full detail (content models, attribute types, defaults): [m.json](m.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`m:acc`](#macc) | `m:CT_Acc` | 0 | 2 | 13 | §22.1.2.1 p.3616, OI 2.1.1643 |
| [`m:accPr`](#maccpr) | `m:CT_AccPr` | 0 | 2 | 1 | §22.1.2.2 p.3617 |
| [`m:aln`](#maln) | `m:CT_OnOff` | 1 | 0 | 2 | §22.1.2.3 p.3617, OI 2.1.1644 |
| [`m:alnScr`](#malnscr) | `m:CT_OnOff` | 1 | 0 | 1 | §22.1.2.4 p.3619 |
| [`m:argPr`](#margpr) | `m:CT_OMathArgPr` | 0 | 1 | 8 | §22.1.2.5 p.3620 |
| [`m:argSz`](#margsz) | `m:CT_Integer2` | 1 | 0 | 1 | §22.1.2.6 p.3621 |
| [`m:bar`](#mbar) | `m:CT_Bar` | 0 | 2 | 13 | §22.1.2.7 p.3623, OI 2.1.1645 |
| [`m:barPr`](#mbarpr) | `m:CT_BarPr` | 0 | 2 | 1 | §22.1.2.8 p.3623, OI 2.1.1646 |
| [`m:baseJc`](#mbasejc) | `m:CT_YAlign` | 1 | 0 | 2 | §22.1.2.9 p.3624, OI 2.1.1647 |
| [`m:begChr`](#mbegchr) | `m:CT_Char` | 1 | 0 | 1 | §22.1.2.10 p.3627 |
| [`m:borderBox`](#mborderbox) | `m:CT_BorderBox` | 0 | 2 | 13 | §22.1.2.11 p.3628, OI 2.1.1648 |
| [`m:borderBoxPr`](#mborderboxpr) | `m:CT_BorderBoxPr` | 0 | 9 | 1 | §22.1.2.12 p.3628 |
| [`m:box`](#mbox) | `m:CT_Box` | 0 | 2 | 13 | §22.1.2.13 p.3629, OI 2.1.1649 |
| [`m:boxPr`](#mboxpr) | `m:CT_BoxPr` | 0 | 6 | 1 | §22.1.2.14 p.3629 |
| [`m:brk`](#mbrk) | `m:CT_ManualBreak` | 1 | 0 | 2 | §22.1.2.15 p.3630, OI 2.1.1650 |
| [`m:brkBin`](#mbrkbin) | `m:CT_BreakBin` | 1 | 0 | 1 | §22.1.2.16 p.3632 |
| [`m:brkBinSub`](#mbrkbinsub) | `m:CT_BreakBinSub` | 1 | 0 | 1 | §22.1.2.17 p.3633 |
| [`m:cGp`](#mcgp) | `m:CT_UnSignedInteger` | 1 | 0 | 1 | §22.1.2.18 p.3634, OI 2.1.1651 |
| [`m:cGpRule`](#mcgprule) | `m:CT_SpacingRule` | 1 | 0 | 1 | §22.1.2.19 p.3635, OI 2.1.1652 |
| [`m:cSp`](#mcsp) | `m:CT_UnSignedInteger` | 1 | 0 | 1 | §22.1.2.22 p.3639, OI 2.1.1654 |
| [`m:chr`](#mchr) | `m:CT_Char` | 1 | 0 | 3 | §22.1.2.20 p.3637 |
| [`m:count`](#mcount) | `m:CT_Integer255` | 1 | 0 | 1 | §22.1.2.21 p.3638, OI 2.1.1653 |
| [`m:ctrlPr`](#mctrlpr) | `m:CT_CtrlPr` | 0 | 3 | 27 | §22.1.2.23 p.3640, OI 2.1.1655 |
| [`m:d`](#md) | `m:CT_D` | 0 | 2 | 13 | §22.1.2.24 p.3641, OI 2.1.1656 |
| [`m:dPr`](#mdpr) | `m:CT_DPr` | 0 | 6 | 1 | §22.1.2.31 p.3648 |
| [`m:defJc`](#mdefjc) | `m:CT_OMathJc` | 1 | 0 | 1 | §22.1.2.25 p.3642, OI 2.1.1657 |
| [`m:deg`](#mdeg) | `m:CT_OMathArg` | 0 | 52 | 1 | §22.1.2.26 p.3643, OI 2.1.1658 |
| [`m:degHide`](#mdeghide) | `m:CT_OnOff` | 1 | 0 | 1 | §22.1.2.27 p.3644, OI 2.1.1659 |
| [`m:den`](#mden) | `m:CT_OMathArg` | 0 | 52 | 1 | §22.1.2.28 p.3645, OI 2.1.1660 |
| [`m:diff`](#mdiff) | `m:CT_OnOff` | 1 | 0 | 1 | §22.1.2.29 p.3645 |
| [`m:dispDef`](#mdispdef) | `m:CT_OnOff` | 1 | 0 | 1 | §22.1.2.30 p.3647, OI 2.1.1661 |
| [`m:e`](#me) | `m:CT_OMathArg` | 0 | 52 | 18 | §22.1.2.32 p.3648, OI 2.1.1662 |
| [`m:endChr`](#mendchr) | `m:CT_Char` | 1 | 0 | 1 | §22.1.2.33 p.3650 |
| [`m:eqArr`](#meqarr) | `m:CT_EqArr` | 0 | 2 | 13 | §22.1.2.34 p.3650, OI 2.1.1663 |
| [`m:eqArrPr`](#meqarrpr) | `m:CT_EqArrPr` | 0 | 6 | 1 | §22.1.2.35 p.3652 |
| [`m:f`](#mf) | `m:CT_F` | 0 | 3 | 13 | §22.1.2.36 p.3653, OI 2.1.1664 |
| [`m:fName`](#mfname) | `m:CT_OMathArg` | 0 | 52 | 1 | §22.1.2.37 p.3654, OI 2.1.1665 |
| [`m:fPr`](#mfpr) | `m:CT_FPr` | 0 | 2 | 1 | §22.1.2.38 p.3654 |
| [`m:func`](#mfunc) | `m:CT_Func` | 0 | 3 | 13 | §22.1.2.39 p.3655, OI 2.1.1666 |
| [`m:funcPr`](#mfuncpr) | `m:CT_FuncPr` | 0 | 1 | 1 | §22.1.2.40 p.3655 |
| [`m:groupChr`](#mgroupchr) | `m:CT_GroupChr` | 0 | 2 | 13 | §22.1.2.41 p.3656, OI 2.1.1667 |
| [`m:groupChrPr`](#mgroupchrpr) | `m:CT_GroupChrPr` | 0 | 4 | 1 | §22.1.2.42 p.3657 |
| [`m:grow`](#mgrow) | `m:CT_OnOff` | 1 | 0 | 2 | §22.1.2.43 p.3657, OI 2.1.1668 |
| [`m:hideBot`](#mhidebot) | `m:CT_OnOff` | 1 | 0 | 1 | §22.1.2.44 p.3658 |
| [`m:hideLeft`](#mhideleft) | `m:CT_OnOff` | 1 | 0 | 1 | §22.1.2.45 p.3659 |
| [`m:hideRight`](#mhideright) | `m:CT_OnOff` | 1 | 0 | 1 | §22.1.2.46 p.3660 |
| [`m:hideTop`](#mhidetop) | `m:CT_OnOff` | 1 | 0 | 1 | §22.1.2.47 p.3661 |
| [`m:intLim`](#mintlim) | `m:CT_LimLoc` | 1 | 0 | 1 | §22.1.2.49 p.3662, OI 2.1.1670 |
| [`m:interSp`](#mintersp) | `m:CT_TwipsMeasure` | 1 | 0 | 1 | §22.1.2.48 p.3661, OI 2.1.1669 |
| [`m:intraSp`](#mintrasp) | `m:CT_TwipsMeasure` | 1 | 0 | 1 | §22.1.2.50 p.3663, OI 2.1.1671 |
| [`m:jc`](#mjc) | `m:CT_OMathJc` | 1 | 0 | 1 | §22.1.2.51 p.3663, OI 2.1.1672 |
| [`m:lMargin`](#mlmargin) | `m:CT_TwipsMeasure` | 1 | 0 | 1 | §22.1.2.59 p.3669 |
| [`m:lim`](#mlim) | `m:CT_OMathArg` | 0 | 52 | 2 | §22.1.2.52 p.3664, OI 2.1.1673 |
| [`m:limLoc`](#mlimloc) | `m:CT_LimLoc` | 1 | 0 | 1 | §22.1.2.53 p.3664 |
| [`m:limLow`](#mlimlow) | `m:CT_LimLow` | 0 | 3 | 13 | §22.1.2.54 p.3665, OI 2.1.1674 |
| [`m:limLowPr`](#mlimlowpr) | `m:CT_LimLowPr` | 0 | 1 | 1 | §22.1.2.55 p.3666 |
| [`m:limUpp`](#mlimupp) | `m:CT_LimUpp` | 0 | 3 | 13 | §22.1.2.56 p.3666, OI 2.1.1675 |
| [`m:limUppPr`](#mlimupppr) | `m:CT_LimUppPr` | 0 | 1 | 1 | §22.1.2.57 p.3667 |
| [`m:lit`](#mlit) | `m:CT_OnOff` | 1 | 0 | 1 | §22.1.2.58 p.3668 |
| [`m:m`](#mm) | `m:CT_M` | 0 | 2 | 13 | §22.1.2.60 p.3670, OI 2.1.1676 |
| [`m:mPr`](#mmpr) | `m:CT_MPr` | 0 | 9 | 1 | §22.1.2.68 p.3682 |
| [`m:mathFont`](#mmathfont) | `m:CT_String` | 1 | 0 | 1 | §22.1.2.61 p.3672, OI 2.1.1677 |
| [`m:mathPr`](#mmathpr) ◆ | `m:CT_MathPr` | 0 | 16 | 1 | §22.1.2.62 p.3673 |
| [`m:maxDist`](#mmaxdist) | `m:CT_OnOff` | 1 | 0 | 1 | §22.1.2.63 p.3674, OI 2.1.1678 |
| [`m:mc`](#mmc) | `m:CT_MC` | 0 | 1 | 1 | §22.1.2.64 p.3676, OI 2.1.1679 |
| [`m:mcJc`](#mmcjc) | `m:CT_XAlign` | 1 | 0 | 1 | §22.1.2.65 p.3677 |
| [`m:mcPr`](#mmcpr) | `m:CT_MCPr` | 0 | 2 | 1 | §22.1.2.66 p.3679 |
| [`m:mcs`](#mmcs) | `m:CT_MCS` | 0 | 1 | 1 | §22.1.2.67 p.3681, OI 2.1.1680 |
| [`m:mr`](#mmr) | `m:CT_MR` | 0 | 1 | 1 | §22.1.2.69 p.3683, OI 2.1.1681 |
| [`m:nary`](#mnary) | `m:CT_Nary` | 0 | 4 | 13 | §22.1.2.70 p.3685, OI 2.1.1682 |
| [`m:naryLim`](#mnarylim) | `m:CT_LimLoc` | 1 | 0 | 1 | §22.1.2.71 p.3686, OI 2.1.1683 |
| [`m:naryPr`](#mnarypr) | `m:CT_NaryPr` | 0 | 6 | 1 | §22.1.2.72 p.3687 |
| [`m:noBreak`](#mnobreak) | `m:CT_OnOff` | 1 | 0 | 1 | §22.1.2.73 p.3688, OI 2.1.1684 |
| [`m:nor`](#mnor) | `m:CT_OnOff` | 1 | 0 | 1 | §22.1.2.74 p.3689 |
| [`m:num`](#mnum) | `m:CT_OMathArg` | 0 | 52 | 1 | §22.1.2.75 p.3690, OI 2.1.1685 |
| [`m:oMath`](#momath) ◆ | `m:CT_OMath` | 0 | 50 | 38 | §22.1.2.77 p.3692, OI 2.1.1687 |
| [`m:oMathPara`](#momathpara) ◆ | `m:CT_OMathPara` | 0 | 2 | 36 | §22.1.2.78 p.3693, OI 2.1.1688 |
| [`m:oMathParaPr`](#momathparapr) | `m:CT_OMathParaPr` | 0 | 1 | 1 | §22.1.2.79 p.3693 |
| [`m:objDist`](#mobjdist) | `m:CT_OnOff` | 1 | 0 | 1 | §22.1.2.76 p.3691, OI 2.1.1686 |
| [`m:opEmu`](#mopemu) | `m:CT_OnOff` | 1 | 0 | 1 | §22.1.2.80 p.3694, OI 2.1.1689 |
| [`m:phant`](#mphant) | `m:CT_Phant` | 0 | 2 | 13 | §22.1.2.81 p.3695, OI 2.1.1690 |
| [`m:phantPr`](#mphantpr) | `m:CT_PhantPr` | 0 | 6 | 1 | §22.1.2.82 p.3696 |
| [`m:plcHide`](#mplchide) | `m:CT_OnOff` | 1 | 0 | 1 | §22.1.2.83 p.3696 |
| [`m:pos`](#mpos) | `m:CT_TopBot` | 1 | 0 | 2 | §22.1.2.84 p.3697, OI 2.1.1691 |
| [`m:postSp`](#mpostsp) | `m:CT_TwipsMeasure` | 1 | 0 | 1 | §22.1.2.85 p.3698, OI 2.1.1692 |
| [`m:preSp`](#mpresp) | `m:CT_TwipsMeasure` | 1 | 0 | 1 | §22.1.2.86 p.3698, OI 2.1.1693 |
| [`m:r`](#mr) | `m:CT_R` | 0 | 36 | 13 | §22.1.2.87 p.3699, OI 2.1.1694 |
| [`m:rMargin`](#mrmargin) | `m:CT_TwipsMeasure` | 1 | 0 | 1 | §22.1.2.90 p.3700 |
| [`m:rPr`](#mrpr) | `m:CT_RPR` | 0 | 6 | 1 | §22.1.2.91 p.3701 |
| [`m:rSp`](#mrsp) | `m:CT_UnSignedInteger` | 1 | 0 | 2 | §22.1.2.92 p.3702, OI 2.1.1696 |
| [`m:rSpRule`](#mrsprule) | `m:CT_SpacingRule` | 1 | 0 | 2 | §22.1.2.93 p.3703, OI 2.1.1697 |
| [`m:rad`](#mrad) | `m:CT_Rad` | 0 | 3 | 13 | §22.1.2.88 p.3699, OI 2.1.1695 |
| [`m:radPr`](#mradpr) | `m:CT_RadPr` | 0 | 2 | 1 | §22.1.2.89 p.3700 |
| [`m:sPre`](#mspre) | `m:CT_SPre` | 0 | 4 | 13 | §22.1.2.99 p.3709, OI 2.1.1701 |
| [`m:sPrePr`](#msprepr) | `m:CT_SPrePr` | 0 | 1 | 1 | §22.1.2.100 p.3709 |
| [`m:sSub`](#mssub) | `m:CT_SSub` | 0 | 3 | 13 | §22.1.2.101 p.3710, OI 2.1.1702 |
| [`m:sSubPr`](#mssubpr) | `m:CT_SSubPr` | 0 | 1 | 1 | §22.1.2.102 p.3711 |
| [`m:sSubSup`](#mssubsup) | `m:CT_SSubSup` | 0 | 4 | 13 | §22.1.2.103 p.3711, OI 2.1.1703 |
| [`m:sSubSupPr`](#mssubsuppr) | `m:CT_SSubSupPr` | 0 | 2 | 1 | §22.1.2.104 p.3712 |
| [`m:sSup`](#mssup) | `m:CT_SSup` | 0 | 3 | 13 | §22.1.2.105 p.3713, OI 2.1.1704 |
| [`m:sSupPr`](#mssuppr) | `m:CT_SSupPr` | 0 | 1 | 1 | §22.1.2.106 p.3713 |
| [`m:scr`](#mscr) | `m:CT_Script` | 1 | 0 | 1 | §22.1.2.94 p.3705, OI 2.1.1698 |
| [`m:sepChr`](#msepchr) | `m:CT_Char` | 1 | 0 | 1 | §22.1.2.95 p.3706 |
| [`m:show`](#mshow) | `m:CT_OnOff` | 1 | 0 | 1 | §22.1.2.96 p.3707 |
| [`m:shp`](#mshp) | `m:CT_Shp` | 1 | 0 | 1 | §22.1.2.97 p.3707, OI 2.1.1699 |
| [`m:smallFrac`](#msmallfrac) | `m:CT_OnOff` | 1 | 0 | 1 | §22.1.2.98 p.3708, OI 2.1.1700 |
| [`m:strikeBLTR`](#mstrikebltr) | `m:CT_OnOff` | 1 | 0 | 1 | §22.1.2.107 p.3714 |
| [`m:strikeH`](#mstrikeh) | `m:CT_OnOff` | 1 | 0 | 1 | §22.1.2.108 p.3715 |
| [`m:strikeTLBR`](#mstriketlbr) | `m:CT_OnOff` | 1 | 0 | 1 | §22.1.2.109 p.3715 |
| [`m:strikeV`](#mstrikev) | `m:CT_OnOff` | 1 | 0 | 1 | §22.1.2.110 p.3716 |
| [`m:sty`](#msty) | `m:CT_Style` | 1 | 0 | 1 | §22.1.2.111 p.3717, OI 2.1.1705 |
| [`m:sub`](#msub) | `m:CT_OMathArg` | 0 | 52 | 4 | §22.1.2.112 p.3718, OI 2.1.1706 |
| [`m:subHide`](#msubhide) | `m:CT_OnOff` | 1 | 0 | 1 | §22.1.2.113 p.3718, OI 2.1.1707 |
| [`m:sup`](#msup) | `m:CT_OMathArg` | 0 | 52 | 4 | §22.1.2.114 p.3719, OI 2.1.1708 |
| [`m:supHide`](#msuphide) | `m:CT_OnOff` | 1 | 0 | 1 | §22.1.2.115 p.3720, OI 2.1.1709 |
| [`m:t`](#mt) | `m:CT_Text` | 1 | 0 | 1 | §22.1.2.116 p.3721, OI 2.1.1710 |
| [`m:transp`](#mtransp) | `m:CT_OnOff` | 1 | 0 | 1 | §22.1.2.117 p.3721, OI 2.1.1711 |
| [`m:type`](#mtype) | `m:CT_FType` | 1 | 0 | 1 | §22.1.2.118 p.3722 |
| [`m:vertJc`](#mvertjc) | `m:CT_TopBot` | 1 | 0 | 1 | §22.1.2.119 p.3723 |
| [`m:wrapIndent`](#mwrapindent) | `m:CT_TwipsMeasure` | 1 | 0 | 1 | §22.1.2.120 p.3725 |
| [`m:wrapRight`](#mwrapright) | `m:CT_OnOff` | 1 | 0 | 1 | §22.1.2.121 p.3725 |
| [`m:zeroAsc`](#mzeroasc) | `m:CT_OnOff` | 1 | 0 | 1 | §22.1.2.122 p.3726 |
| [`m:zeroDesc`](#mzerodesc) | `m:CT_OnOff` | 1 | 0 | 1 | §22.1.2.123 p.3727 |
| [`m:zeroWid`](#mzerowid) | `m:CT_OnOff` | 1 | 0 | 1 | §22.1.2.124 p.3728 |

◆ declared globally (can be the root element of a part)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `m:ST_BreakBin` | restriction of xsd:string | `before`, `after`, `repeat` | §22.1.3.1 p.3729, OI 2.1.1712 |
| `m:ST_BreakBinSub` | restriction of xsd:string | `--`, `-+`, `+-` | §22.1.3.2 p.3729 |
| `m:ST_Char` | restriction of xsd:string | maxLength `1` | §22.1.3.3 p.3730 |
| `m:ST_FType` | restriction of xsd:string | `bar`, `skw`, `lin`, `noBar` | §22.1.3.4 p.3730 |
| `m:ST_Integer2` | restriction of xsd:integer | minInclusive `-2`, maxInclusive `2` | §22.1.3.5 p.3731 |
| `m:ST_Integer255` | restriction of xsd:integer | minInclusive `1`, maxInclusive `255` | §22.1.3.6 p.3731 |
| `m:ST_Jc` | restriction of xsd:string | `left`, `right`, `center`, `centerGroup` | §22.1.3.7 p.3731, OI 2.1.1713 |
| `m:ST_LimLoc` | restriction of xsd:string | `undOvr`, `subSup` | §22.1.3.8 p.3732 |
| `m:ST_Script` | restriction of xsd:string | `roman`, `script`, `fraktur`, `double-struck`, `sans-serif`, `monospace` | §22.1.3.9 p.3732 |
| `m:ST_Shp` | restriction of xsd:string | `centered`, `match` | §22.1.3.10 p.3733, OI 2.1.1714 |
| `m:ST_SpacingRule` | restriction of xsd:integer | minInclusive `0`, maxInclusive `4` | §22.1.3.11 p.3733 |
| `m:ST_Style` | restriction of xsd:string | `p`, `b`, `i`, `bi` | §22.1.3.12 p.3733 |
| `m:ST_TopBot` | restriction of xsd:string | `top`, `bot` | §22.1.3.13 p.3734, OI 2.1.1715 |
| `m:ST_UnSignedInteger` | restriction of xsd:unsignedInt |  | §22.1.3.14 p.3734, OI 2.1.1716 |

## Element details

### m:acc

- Type: `m:CT_Acc`
- Children: `m:accPr`, `m:e`
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:del`, `w:ins`, `w:moveFrom`, `w:moveTo`
- Specification: §22.1.2.1 p.3616, OI 2.1.1643

### m:accPr

- Type: `m:CT_AccPr`
- Children: `m:chr`, `m:ctrlPr`
- Parents: `m:acc`
- Specification: §22.1.2.2 p.3617

### m:aln

- Type: `m:CT_OnOff`
- Attributes: `m:val`
- Parents: `m:boxPr`, `m:rPr`
- Specification: §22.1.2.3 p.3617, OI 2.1.1644

### m:alnScr

- Type: `m:CT_OnOff`
- Attributes: `m:val`
- Parents: `m:sSubSupPr`
- Specification: §22.1.2.4 p.3619

### m:argPr

- Type: `m:CT_OMathArgPr`
- Children: `m:argSz`
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:sub`, `m:sup`
- Specification: §22.1.2.5 p.3620

### m:argSz

- Type: `m:CT_Integer2`
- Attributes: `m:val`!
- Parents: `m:argPr`
- Specification: §22.1.2.6 p.3621

### m:bar

- Type: `m:CT_Bar`
- Children: `m:barPr`, `m:e`
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:del`, `w:ins`, `w:moveFrom`, `w:moveTo`
- Specification: §22.1.2.7 p.3623, OI 2.1.1645

### m:barPr

- Type: `m:CT_BarPr`
- Children: `m:pos`, `m:ctrlPr`
- Parents: `m:bar`
- Specification: §22.1.2.8 p.3623, OI 2.1.1646

### m:baseJc

- Type: `m:CT_YAlign`
- Attributes: `m:val`!
- Parents: `m:eqArrPr`, `m:mPr`
- Specification: §22.1.2.9 p.3624, OI 2.1.1647

### m:begChr

- Type: `m:CT_Char`
- Attributes: `m:val`!
- Parents: `m:dPr`
- Specification: §22.1.2.10 p.3627

### m:borderBox

- Type: `m:CT_BorderBox`
- Children: `m:borderBoxPr`, `m:e`
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:del`, `w:ins`, `w:moveFrom`, `w:moveTo`
- Specification: §22.1.2.11 p.3628, OI 2.1.1648

### m:borderBoxPr

- Type: `m:CT_BorderBoxPr`
- Children: `m:hideTop`, `m:hideBot`, `m:hideLeft`, `m:hideRight`, `m:strikeH`, `m:strikeV`, `m:strikeBLTR`, `m:strikeTLBR`, `m:ctrlPr`
- Parents: `m:borderBox`
- Specification: §22.1.2.12 p.3628

### m:box

- Type: `m:CT_Box`
- Children: `m:boxPr`, `m:e`
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:del`, `w:ins`, `w:moveFrom`, `w:moveTo`
- Specification: §22.1.2.13 p.3629, OI 2.1.1649

### m:boxPr

- Type: `m:CT_BoxPr`
- Children: `m:opEmu`, `m:noBreak`, `m:diff`, `m:brk`, `m:aln`, `m:ctrlPr`
- Parents: `m:box`
- Specification: §22.1.2.14 p.3629

### m:brk

- Type: `m:CT_ManualBreak`
- Attributes: `m:alnAt`
- Parents: `m:boxPr`, `m:rPr`
- Specification: §22.1.2.15 p.3630, OI 2.1.1650

### m:brkBin

- Type: `m:CT_BreakBin`
- Attributes: `m:val`
- Parents: `m:mathPr`
- Specification: §22.1.2.16 p.3632

### m:brkBinSub

- Type: `m:CT_BreakBinSub`
- Attributes: `m:val`
- Parents: `m:mathPr`
- Specification: §22.1.2.17 p.3633

### m:cGp

- Type: `m:CT_UnSignedInteger`
- Attributes: `m:val`!
- Parents: `m:mPr`
- Specification: §22.1.2.18 p.3634, OI 2.1.1651

### m:cGpRule

- Type: `m:CT_SpacingRule`
- Attributes: `m:val`!
- Parents: `m:mPr`
- Specification: §22.1.2.19 p.3635, OI 2.1.1652

### m:cSp

- Type: `m:CT_UnSignedInteger`
- Attributes: `m:val`!
- Parents: `m:mPr`
- Specification: §22.1.2.22 p.3639, OI 2.1.1654

### m:chr

- Type: `m:CT_Char`
- Attributes: `m:val`!
- Parents: `m:accPr`, `m:groupChrPr`, `m:naryPr`
- Specification: §22.1.2.20 p.3637

### m:count

- Type: `m:CT_Integer255`
- Attributes: `m:val`!
- Parents: `m:mcPr`
- Specification: §22.1.2.21 p.3638, OI 2.1.1653

### m:ctrlPr

- Type: `m:CT_CtrlPr`
- Children: `w:rPr`, `w:ins`, `w:del`
- Parents: `m:accPr`, `m:barPr`, `m:borderBoxPr`, `m:boxPr`, `m:dPr`, `m:deg`, `m:den`, `m:e`, `m:eqArrPr`, `m:fName`, `m:fPr`, `m:funcPr`, `m:groupChrPr`, `m:lim`, `m:limLowPr`, `m:limUppPr`, `m:mPr`, `m:naryPr`, `m:num`, `m:phantPr`, `m:radPr`, `m:sPrePr`, `m:sSubPr`, `m:sSubSupPr`, `m:sSupPr`, `m:sub`, `m:sup`
- Specification: §22.1.2.23 p.3640, OI 2.1.1655

### m:d

- Type: `m:CT_D`
- Children: `m:dPr`, `m:e`
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:del`, `w:ins`, `w:moveFrom`, `w:moveTo`
- Specification: §22.1.2.24 p.3641, OI 2.1.1656

### m:dPr

- Type: `m:CT_DPr`
- Children: `m:begChr`, `m:sepChr`, `m:endChr`, `m:grow`, `m:shp`, `m:ctrlPr`
- Parents: `m:d`
- Specification: §22.1.2.31 p.3648

### m:defJc

- Type: `m:CT_OMathJc`
- Attributes: `m:val`
- Parents: `m:mathPr`
- Specification: §22.1.2.25 p.3642, OI 2.1.1657

### m:deg

- Type: `m:CT_OMathArg`
- Children: `m:argPr`, `m:acc`, `m:bar`, `m:box`, `m:borderBox`, `m:d`, `m:eqArr`, `m:f`, `m:func`, `m:groupChr`, `m:limLow`, `m:limUpp`, `m:m`, `m:nary`, `m:phant`, `m:rad`, `m:sPre`, `m:sSub`, `m:sSubSup`, `m:sSup`, `m:r`, `w:customXml`, `w:fldSimple`, `w:hyperlink`, `w:smartTag`, `w:sdt`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`, `m:ctrlPr`
- Parents: `m:rad`
- Specification: §22.1.2.26 p.3643, OI 2.1.1658

### m:degHide

- Type: `m:CT_OnOff`
- Attributes: `m:val`
- Parents: `m:radPr`
- Specification: §22.1.2.27 p.3644, OI 2.1.1659

### m:den

- Type: `m:CT_OMathArg`
- Children: `m:argPr`, `m:acc`, `m:bar`, `m:box`, `m:borderBox`, `m:d`, `m:eqArr`, `m:f`, `m:func`, `m:groupChr`, `m:limLow`, `m:limUpp`, `m:m`, `m:nary`, `m:phant`, `m:rad`, `m:sPre`, `m:sSub`, `m:sSubSup`, `m:sSup`, `m:r`, `w:customXml`, `w:fldSimple`, `w:hyperlink`, `w:smartTag`, `w:sdt`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`, `m:ctrlPr`
- Parents: `m:f`
- Specification: §22.1.2.28 p.3645, OI 2.1.1660

### m:diff

- Type: `m:CT_OnOff`
- Attributes: `m:val`
- Parents: `m:boxPr`
- Specification: §22.1.2.29 p.3645

### m:dispDef

- Type: `m:CT_OnOff`
- Attributes: `m:val`
- Parents: `m:mathPr`
- Specification: §22.1.2.30 p.3647, OI 2.1.1661

### m:e

- Type: `m:CT_OMathArg`
- Children: `m:argPr`, `m:acc`, `m:bar`, `m:box`, `m:borderBox`, `m:d`, `m:eqArr`, `m:f`, `m:func`, `m:groupChr`, `m:limLow`, `m:limUpp`, `m:m`, `m:nary`, `m:phant`, `m:rad`, `m:sPre`, `m:sSub`, `m:sSubSup`, `m:sSup`, `m:r`, `w:customXml`, `w:fldSimple`, `w:hyperlink`, `w:smartTag`, `w:sdt`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`, `m:ctrlPr`
- Parents: `m:acc`, `m:bar`, `m:borderBox`, `m:box`, `m:d`, `m:eqArr`, `m:func`, `m:groupChr`, `m:limLow`, `m:limUpp`, `m:mr`, `m:nary`, `m:phant`, `m:rad`, `m:sPre`, `m:sSub`, `m:sSubSup`, `m:sSup`
- Specification: §22.1.2.32 p.3648, OI 2.1.1662

### m:endChr

- Type: `m:CT_Char`
- Attributes: `m:val`!
- Parents: `m:dPr`
- Specification: §22.1.2.33 p.3650

### m:eqArr

- Type: `m:CT_EqArr`
- Children: `m:eqArrPr`, `m:e`
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:del`, `w:ins`, `w:moveFrom`, `w:moveTo`
- Specification: §22.1.2.34 p.3650, OI 2.1.1663

### m:eqArrPr

- Type: `m:CT_EqArrPr`
- Children: `m:baseJc`, `m:maxDist`, `m:objDist`, `m:rSpRule`, `m:rSp`, `m:ctrlPr`
- Parents: `m:eqArr`
- Specification: §22.1.2.35 p.3652

### m:f

- Type: `m:CT_F`
- Children: `m:fPr`, `m:num`, `m:den`
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:del`, `w:ins`, `w:moveFrom`, `w:moveTo`
- Specification: §22.1.2.36 p.3653, OI 2.1.1664

### m:fName

- Type: `m:CT_OMathArg`
- Children: `m:argPr`, `m:acc`, `m:bar`, `m:box`, `m:borderBox`, `m:d`, `m:eqArr`, `m:f`, `m:func`, `m:groupChr`, `m:limLow`, `m:limUpp`, `m:m`, `m:nary`, `m:phant`, `m:rad`, `m:sPre`, `m:sSub`, `m:sSubSup`, `m:sSup`, `m:r`, `w:customXml`, `w:fldSimple`, `w:hyperlink`, `w:smartTag`, `w:sdt`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`, `m:ctrlPr`
- Parents: `m:func`
- Specification: §22.1.2.37 p.3654, OI 2.1.1665

### m:fPr

- Type: `m:CT_FPr`
- Children: `m:type`, `m:ctrlPr`
- Parents: `m:f`
- Specification: §22.1.2.38 p.3654

### m:func

- Type: `m:CT_Func`
- Children: `m:funcPr`, `m:fName`, `m:e`
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:del`, `w:ins`, `w:moveFrom`, `w:moveTo`
- Specification: §22.1.2.39 p.3655, OI 2.1.1666

### m:funcPr

- Type: `m:CT_FuncPr`
- Children: `m:ctrlPr`
- Parents: `m:func`
- Specification: §22.1.2.40 p.3655

### m:groupChr

- Type: `m:CT_GroupChr`
- Children: `m:groupChrPr`, `m:e`
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:del`, `w:ins`, `w:moveFrom`, `w:moveTo`
- Specification: §22.1.2.41 p.3656, OI 2.1.1667

### m:groupChrPr

- Type: `m:CT_GroupChrPr`
- Children: `m:chr`, `m:pos`, `m:vertJc`, `m:ctrlPr`
- Parents: `m:groupChr`
- Specification: §22.1.2.42 p.3657

### m:grow

- Type: `m:CT_OnOff`
- Attributes: `m:val`
- Parents: `m:dPr`, `m:naryPr`
- Specification: §22.1.2.43 p.3657, OI 2.1.1668

### m:hideBot

- Type: `m:CT_OnOff`
- Attributes: `m:val`
- Parents: `m:borderBoxPr`
- Specification: §22.1.2.44 p.3658

### m:hideLeft

- Type: `m:CT_OnOff`
- Attributes: `m:val`
- Parents: `m:borderBoxPr`
- Specification: §22.1.2.45 p.3659

### m:hideRight

- Type: `m:CT_OnOff`
- Attributes: `m:val`
- Parents: `m:borderBoxPr`
- Specification: §22.1.2.46 p.3660

### m:hideTop

- Type: `m:CT_OnOff`
- Attributes: `m:val`
- Parents: `m:borderBoxPr`
- Specification: §22.1.2.47 p.3661

### m:intLim

- Type: `m:CT_LimLoc`
- Attributes: `m:val`!
- Parents: `m:mathPr`
- Specification: §22.1.2.49 p.3662, OI 2.1.1670

### m:interSp

- Type: `m:CT_TwipsMeasure`
- Attributes: `m:val`!
- Parents: `m:mathPr`
- Specification: §22.1.2.48 p.3661, OI 2.1.1669

### m:intraSp

- Type: `m:CT_TwipsMeasure`
- Attributes: `m:val`!
- Parents: `m:mathPr`
- Specification: §22.1.2.50 p.3663, OI 2.1.1671

### m:jc

- Type: `m:CT_OMathJc`
- Attributes: `m:val`
- Parents: `m:oMathParaPr`
- Specification: §22.1.2.51 p.3663, OI 2.1.1672

### m:lMargin

- Type: `m:CT_TwipsMeasure`
- Attributes: `m:val`!
- Parents: `m:mathPr`
- Specification: §22.1.2.59 p.3669

### m:lim

- Type: `m:CT_OMathArg`
- Children: `m:argPr`, `m:acc`, `m:bar`, `m:box`, `m:borderBox`, `m:d`, `m:eqArr`, `m:f`, `m:func`, `m:groupChr`, `m:limLow`, `m:limUpp`, `m:m`, `m:nary`, `m:phant`, `m:rad`, `m:sPre`, `m:sSub`, `m:sSubSup`, `m:sSup`, `m:r`, `w:customXml`, `w:fldSimple`, `w:hyperlink`, `w:smartTag`, `w:sdt`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`, `m:ctrlPr`
- Parents: `m:limLow`, `m:limUpp`
- Specification: §22.1.2.52 p.3664, OI 2.1.1673

### m:limLoc

- Type: `m:CT_LimLoc`
- Attributes: `m:val`!
- Parents: `m:naryPr`
- Specification: §22.1.2.53 p.3664

### m:limLow

- Type: `m:CT_LimLow`
- Children: `m:limLowPr`, `m:e`, `m:lim`
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:del`, `w:ins`, `w:moveFrom`, `w:moveTo`
- Specification: §22.1.2.54 p.3665, OI 2.1.1674

### m:limLowPr

- Type: `m:CT_LimLowPr`
- Children: `m:ctrlPr`
- Parents: `m:limLow`
- Specification: §22.1.2.55 p.3666

### m:limUpp

- Type: `m:CT_LimUpp`
- Children: `m:limUppPr`, `m:e`, `m:lim`
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:del`, `w:ins`, `w:moveFrom`, `w:moveTo`
- Specification: §22.1.2.56 p.3666, OI 2.1.1675

### m:limUppPr

- Type: `m:CT_LimUppPr`
- Children: `m:ctrlPr`
- Parents: `m:limUpp`
- Specification: §22.1.2.57 p.3667

### m:lit

- Type: `m:CT_OnOff`
- Attributes: `m:val`
- Parents: `m:rPr`
- Specification: §22.1.2.58 p.3668

### m:m

- Type: `m:CT_M`
- Children: `m:mPr`, `m:mr`
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:del`, `w:ins`, `w:moveFrom`, `w:moveTo`
- Specification: §22.1.2.60 p.3670, OI 2.1.1676

### m:mPr

- Type: `m:CT_MPr`
- Children: `m:baseJc`, `m:plcHide`, `m:rSpRule`, `m:cGpRule`, `m:rSp`, `m:cSp`, `m:cGp`, `m:mcs`, `m:ctrlPr`
- Parents: `m:m`
- Specification: §22.1.2.68 p.3682

### m:mathFont

- Type: `m:CT_String`
- Attributes: `m:val`
- Parents: `m:mathPr`
- Specification: §22.1.2.61 p.3672, OI 2.1.1677

### m:mathPr

- Type: `m:CT_MathPr`
- Children: `m:mathFont`, `m:brkBin`, `m:brkBinSub`, `m:smallFrac`, `m:dispDef`, `m:lMargin`, `m:rMargin`, `m:defJc`, `m:preSp`, `m:postSp`, `m:interSp`, `m:intraSp`, `m:wrapIndent`, `m:wrapRight`, `m:intLim`, `m:naryLim`
- Parents: `w:settings`
- Specification: §22.1.2.62 p.3673

### m:maxDist

- Type: `m:CT_OnOff`
- Attributes: `m:val`
- Parents: `m:eqArrPr`
- Specification: §22.1.2.63 p.3674, OI 2.1.1678

### m:mc

- Type: `m:CT_MC`
- Children: `m:mcPr`
- Parents: `m:mcs`
- Specification: §22.1.2.64 p.3676, OI 2.1.1679

### m:mcJc

- Type: `m:CT_XAlign`
- Attributes: `m:val`!
- Parents: `m:mcPr`
- Specification: §22.1.2.65 p.3677

### m:mcPr

- Type: `m:CT_MCPr`
- Children: `m:count`, `m:mcJc`
- Parents: `m:mc`
- Specification: §22.1.2.66 p.3679

### m:mcs

- Type: `m:CT_MCS`
- Children: `m:mc`
- Parents: `m:mPr`
- Specification: §22.1.2.67 p.3681, OI 2.1.1680

### m:mr

- Type: `m:CT_MR`
- Children: `m:e`
- Parents: `m:m`
- Specification: §22.1.2.69 p.3683, OI 2.1.1681

### m:nary

- Type: `m:CT_Nary`
- Children: `m:naryPr`, `m:sub`, `m:sup`, `m:e`
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:del`, `w:ins`, `w:moveFrom`, `w:moveTo`
- Specification: §22.1.2.70 p.3685, OI 2.1.1682

### m:naryLim

- Type: `m:CT_LimLoc`
- Attributes: `m:val`!
- Parents: `m:mathPr`
- Specification: §22.1.2.71 p.3686, OI 2.1.1683

### m:naryPr

- Type: `m:CT_NaryPr`
- Children: `m:chr`, `m:limLoc`, `m:grow`, `m:subHide`, `m:supHide`, `m:ctrlPr`
- Parents: `m:nary`
- Specification: §22.1.2.72 p.3687

### m:noBreak

- Type: `m:CT_OnOff`
- Attributes: `m:val`
- Parents: `m:boxPr`
- Specification: §22.1.2.73 p.3688, OI 2.1.1684

### m:nor

- Type: `m:CT_OnOff`
- Attributes: `m:val`
- Parents: `m:rPr`
- Specification: §22.1.2.74 p.3689

### m:num

- Type: `m:CT_OMathArg`
- Children: `m:argPr`, `m:acc`, `m:bar`, `m:box`, `m:borderBox`, `m:d`, `m:eqArr`, `m:f`, `m:func`, `m:groupChr`, `m:limLow`, `m:limUpp`, `m:m`, `m:nary`, `m:phant`, `m:rad`, `m:sPre`, `m:sSub`, `m:sSubSup`, `m:sSup`, `m:r`, `w:customXml`, `w:fldSimple`, `w:hyperlink`, `w:smartTag`, `w:sdt`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`, `m:ctrlPr`
- Parents: `m:f`
- Specification: §22.1.2.75 p.3690, OI 2.1.1685

### m:oMath

- Type: `m:CT_OMath`
- Children: `m:acc`, `m:bar`, `m:box`, `m:borderBox`, `m:d`, `m:eqArr`, `m:f`, `m:func`, `m:groupChr`, `m:limLow`, `m:limUpp`, `m:m`, `m:nary`, `m:phant`, `m:rad`, `m:sPre`, `m:sSub`, `m:sSubSup`, `m:sSup`, `m:r`, `w:customXml`, `w:fldSimple`, `w:hyperlink`, `w:smartTag`, `w:sdt`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`
- Parents: `a14:m`, `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:oMathPara`, `m:sub`, `m:sup`, `w:bdo`, `w:body`, `w:comment`, `w:customXml`, `w:del`, `w:dir`, `w:docPartBody`, `w:endnote`, `w:fldSimple`, `w:footnote`, `w:ftr`, `w:hdr`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:p`, `w:rt`, `w:rubyBase`, `w:sdtContent`, `w:smartTag`, `w:tbl`, `w:tc`, `w:tr`, `w:txbxContent`, `wne:txbxContent`, `wp:txbxContent`
- Specification: §22.1.2.77 p.3692, OI 2.1.1687

### m:oMathPara

- Type: `m:CT_OMathPara`
- Children: `m:oMathParaPr`, `m:oMath`
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:bdo`, `w:body`, `w:comment`, `w:customXml`, `w:del`, `w:dir`, `w:docPartBody`, `w:endnote`, `w:fldSimple`, `w:footnote`, `w:ftr`, `w:hdr`, `w:hyperlink`, `w:ins`, `w:moveFrom`, `w:moveTo`, `w:p`, `w:rt`, `w:rubyBase`, `w:sdtContent`, `w:smartTag`, `w:tbl`, `w:tc`, `w:tr`, `w:txbxContent`, `wne:txbxContent`, `wp:txbxContent`
- Specification: §22.1.2.78 p.3693, OI 2.1.1688

### m:oMathParaPr

- Type: `m:CT_OMathParaPr`
- Children: `m:jc`
- Parents: `m:oMathPara`
- Specification: §22.1.2.79 p.3693

### m:objDist

- Type: `m:CT_OnOff`
- Attributes: `m:val`
- Parents: `m:eqArrPr`
- Specification: §22.1.2.76 p.3691, OI 2.1.1686

### m:opEmu

- Type: `m:CT_OnOff`
- Attributes: `m:val`
- Parents: `m:boxPr`
- Specification: §22.1.2.80 p.3694, OI 2.1.1689

### m:phant

- Type: `m:CT_Phant`
- Children: `m:phantPr`, `m:e`
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:del`, `w:ins`, `w:moveFrom`, `w:moveTo`
- Specification: §22.1.2.81 p.3695, OI 2.1.1690

### m:phantPr

- Type: `m:CT_PhantPr`
- Children: `m:show`, `m:zeroWid`, `m:zeroAsc`, `m:zeroDesc`, `m:transp`, `m:ctrlPr`
- Parents: `m:phant`
- Specification: §22.1.2.82 p.3696

### m:plcHide

- Type: `m:CT_OnOff`
- Attributes: `m:val`
- Parents: `m:mPr`
- Specification: §22.1.2.83 p.3696

### m:pos

- Type: `m:CT_TopBot`
- Attributes: `m:val`!
- Parents: `m:barPr`, `m:groupChrPr`
- Specification: §22.1.2.84 p.3697, OI 2.1.1691

### m:postSp

- Type: `m:CT_TwipsMeasure`
- Attributes: `m:val`!
- Parents: `m:mathPr`
- Specification: §22.1.2.85 p.3698, OI 2.1.1692

### m:preSp

- Type: `m:CT_TwipsMeasure`
- Attributes: `m:val`!
- Parents: `m:mathPr`
- Specification: §22.1.2.86 p.3698, OI 2.1.1693

### m:r

- Type: `m:CT_R`
- Children: `m:rPr`, `w:rPr`, `w:br`, `w:t`, `w:contentPart`, `w:delText`, `w:instrText`, `w:delInstrText`, `w:noBreakHyphen`, `w:softHyphen`, `w:dayShort`, `w:monthShort`, `w:yearShort`, `w:dayLong`, `w:monthLong`, `w:yearLong`, `w:annotationRef`, `w:footnoteRef`, `w:endnoteRef`, `w:separator`, `w:continuationSeparator`, `w:sym`, `w:pgNum`, `w:cr`, `w:tab`, `w:object`, `w:pict`, `w:fldChar`, `w:ruby`, `w:footnoteReference`, `w:endnoteReference`, `w:commentReference`, `w:drawing`, `w:ptab`, `w:lastRenderedPageBreak`, `m:t`
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:del`, `w:ins`, `w:moveFrom`, `w:moveTo`
- Specification: §22.1.2.87 p.3699, OI 2.1.1694

### m:rMargin

- Type: `m:CT_TwipsMeasure`
- Attributes: `m:val`!
- Parents: `m:mathPr`
- Specification: §22.1.2.90 p.3700

### m:rPr

- Type: `m:CT_RPR`
- Children: `m:lit`, `m:nor`, `m:scr`, `m:sty`, `m:brk`, `m:aln`
- Parents: `m:r`
- Specification: §22.1.2.91 p.3701

### m:rSp

- Type: `m:CT_UnSignedInteger`
- Attributes: `m:val`!
- Parents: `m:eqArrPr`, `m:mPr`
- Specification: §22.1.2.92 p.3702, OI 2.1.1696

### m:rSpRule

- Type: `m:CT_SpacingRule`
- Attributes: `m:val`!
- Parents: `m:eqArrPr`, `m:mPr`
- Specification: §22.1.2.93 p.3703, OI 2.1.1697

### m:rad

- Type: `m:CT_Rad`
- Children: `m:radPr`, `m:deg`, `m:e`
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:del`, `w:ins`, `w:moveFrom`, `w:moveTo`
- Specification: §22.1.2.88 p.3699, OI 2.1.1695

### m:radPr

- Type: `m:CT_RadPr`
- Children: `m:degHide`, `m:ctrlPr`
- Parents: `m:rad`
- Specification: §22.1.2.89 p.3700

### m:sPre

- Type: `m:CT_SPre`
- Children: `m:sPrePr`, `m:sub`, `m:sup`, `m:e`
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:del`, `w:ins`, `w:moveFrom`, `w:moveTo`
- Specification: §22.1.2.99 p.3709, OI 2.1.1701

### m:sPrePr

- Type: `m:CT_SPrePr`
- Children: `m:ctrlPr`
- Parents: `m:sPre`
- Specification: §22.1.2.100 p.3709

### m:sSub

- Type: `m:CT_SSub`
- Children: `m:sSubPr`, `m:e`, `m:sub`
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:del`, `w:ins`, `w:moveFrom`, `w:moveTo`
- Specification: §22.1.2.101 p.3710, OI 2.1.1702

### m:sSubPr

- Type: `m:CT_SSubPr`
- Children: `m:ctrlPr`
- Parents: `m:sSub`
- Specification: §22.1.2.102 p.3711

### m:sSubSup

- Type: `m:CT_SSubSup`
- Children: `m:sSubSupPr`, `m:e`, `m:sub`, `m:sup`
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:del`, `w:ins`, `w:moveFrom`, `w:moveTo`
- Specification: §22.1.2.103 p.3711, OI 2.1.1703

### m:sSubSupPr

- Type: `m:CT_SSubSupPr`
- Children: `m:alnScr`, `m:ctrlPr`
- Parents: `m:sSubSup`
- Specification: §22.1.2.104 p.3712

### m:sSup

- Type: `m:CT_SSup`
- Children: `m:sSupPr`, `m:e`, `m:sup`
- Parents: `m:deg`, `m:den`, `m:e`, `m:fName`, `m:lim`, `m:num`, `m:oMath`, `m:sub`, `m:sup`, `w:del`, `w:ins`, `w:moveFrom`, `w:moveTo`
- Specification: §22.1.2.105 p.3713, OI 2.1.1704

### m:sSupPr

- Type: `m:CT_SSupPr`
- Children: `m:ctrlPr`
- Parents: `m:sSup`
- Specification: §22.1.2.106 p.3713

### m:scr

- Type: `m:CT_Script`
- Attributes: `m:val`
- Parents: `m:rPr`
- Specification: §22.1.2.94 p.3705, OI 2.1.1698

### m:sepChr

- Type: `m:CT_Char`
- Attributes: `m:val`!
- Parents: `m:dPr`
- Specification: §22.1.2.95 p.3706

### m:show

- Type: `m:CT_OnOff`
- Attributes: `m:val`
- Parents: `m:phantPr`
- Specification: §22.1.2.96 p.3707

### m:shp

- Type: `m:CT_Shp`
- Attributes: `m:val`!
- Parents: `m:dPr`
- Specification: §22.1.2.97 p.3707, OI 2.1.1699

### m:smallFrac

- Type: `m:CT_OnOff`
- Attributes: `m:val`
- Parents: `m:mathPr`
- Specification: §22.1.2.98 p.3708, OI 2.1.1700

### m:strikeBLTR

- Type: `m:CT_OnOff`
- Attributes: `m:val`
- Parents: `m:borderBoxPr`
- Specification: §22.1.2.107 p.3714

### m:strikeH

- Type: `m:CT_OnOff`
- Attributes: `m:val`
- Parents: `m:borderBoxPr`
- Specification: §22.1.2.108 p.3715

### m:strikeTLBR

- Type: `m:CT_OnOff`
- Attributes: `m:val`
- Parents: `m:borderBoxPr`
- Specification: §22.1.2.109 p.3715

### m:strikeV

- Type: `m:CT_OnOff`
- Attributes: `m:val`
- Parents: `m:borderBoxPr`
- Specification: §22.1.2.110 p.3716

### m:sty

- Type: `m:CT_Style`
- Attributes: `m:val`
- Parents: `m:rPr`
- Specification: §22.1.2.111 p.3717, OI 2.1.1705

### m:sub

- Type: `m:CT_OMathArg`
- Children: `m:argPr`, `m:acc`, `m:bar`, `m:box`, `m:borderBox`, `m:d`, `m:eqArr`, `m:f`, `m:func`, `m:groupChr`, `m:limLow`, `m:limUpp`, `m:m`, `m:nary`, `m:phant`, `m:rad`, `m:sPre`, `m:sSub`, `m:sSubSup`, `m:sSup`, `m:r`, `w:customXml`, `w:fldSimple`, `w:hyperlink`, `w:smartTag`, `w:sdt`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`, `m:ctrlPr`
- Parents: `m:nary`, `m:sPre`, `m:sSub`, `m:sSubSup`
- Specification: §22.1.2.112 p.3718, OI 2.1.1706

### m:subHide

- Type: `m:CT_OnOff`
- Attributes: `m:val`
- Parents: `m:naryPr`
- Specification: §22.1.2.113 p.3718, OI 2.1.1707

### m:sup

- Type: `m:CT_OMathArg`
- Children: `m:argPr`, `m:acc`, `m:bar`, `m:box`, `m:borderBox`, `m:d`, `m:eqArr`, `m:f`, `m:func`, `m:groupChr`, `m:limLow`, `m:limUpp`, `m:m`, `m:nary`, `m:phant`, `m:rad`, `m:sPre`, `m:sSub`, `m:sSubSup`, `m:sSup`, `m:r`, `w:customXml`, `w:fldSimple`, `w:hyperlink`, `w:smartTag`, `w:sdt`, `w:proofErr`, `w:permStart`, `w:permEnd`, `w:bookmarkStart`, `w:bookmarkEnd`, `w:moveFromRangeStart`, `w:moveFromRangeEnd`, `w:moveToRangeStart`, `w:moveToRangeEnd`, `w:commentRangeStart`, `w:commentRangeEnd`, `w:customXmlInsRangeStart`, `w:customXmlInsRangeEnd`, `w:customXmlDelRangeStart`, `w:customXmlDelRangeEnd`, `w:customXmlMoveFromRangeStart`, `w:customXmlMoveFromRangeEnd`, `w:customXmlMoveToRangeStart`, `w:customXmlMoveToRangeEnd`, `w:ins`, `w:del`, `w:moveFrom`, `w:moveTo`, `m:oMathPara`, `m:oMath`, `m:ctrlPr`
- Parents: `m:nary`, `m:sPre`, `m:sSubSup`, `m:sSup`
- Specification: §22.1.2.114 p.3719, OI 2.1.1708

### m:supHide

- Type: `m:CT_OnOff`
- Attributes: `m:val`
- Parents: `m:naryPr`
- Specification: §22.1.2.115 p.3720, OI 2.1.1709

### m:t

- Type: `m:CT_Text` (text: `s:ST_String`)
- Attributes: `xml:space`
- Parents: `m:r`
- Specification: §22.1.2.116 p.3721, OI 2.1.1710

### m:transp

- Type: `m:CT_OnOff`
- Attributes: `m:val`
- Parents: `m:phantPr`
- Specification: §22.1.2.117 p.3721, OI 2.1.1711

### m:type

- Type: `m:CT_FType`
- Attributes: `m:val`!
- Parents: `m:fPr`
- Specification: §22.1.2.118 p.3722

### m:vertJc

- Type: `m:CT_TopBot`
- Attributes: `m:val`!
- Parents: `m:groupChrPr`
- Specification: §22.1.2.119 p.3723

### m:wrapIndent

- Type: `m:CT_TwipsMeasure`
- Attributes: `m:val`!
- Parents: `m:mathPr`
- Specification: §22.1.2.120 p.3725

### m:wrapRight

- Type: `m:CT_OnOff`
- Attributes: `m:val`
- Parents: `m:mathPr`
- Specification: §22.1.2.121 p.3725

### m:zeroAsc

- Type: `m:CT_OnOff`
- Attributes: `m:val`
- Parents: `m:phantPr`
- Specification: §22.1.2.122 p.3726

### m:zeroDesc

- Type: `m:CT_OnOff`
- Attributes: `m:val`
- Parents: `m:phantPr`
- Specification: §22.1.2.123 p.3727

### m:zeroWid

- Type: `m:CT_OnOff`
- Attributes: `m:val`
- Parents: `m:phantPr`
- Specification: §22.1.2.124 p.3728

`!` marks required attributes.
