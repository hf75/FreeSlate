# v: VML (legacy drawing)

`urn:schemas-microsoft-com:vml`

- Source: ECMA-376-4 transitional schema vml-main.xsd
- Office version: 2007
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 23 | 733 | 0 | 23 | 12 | 51 | 37 | 29 |

Full detail (content models, attribute types, defaults): [v.json](v.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`v:arc`](#varc) ◆ | `v:CT_Arc` | 55 | 23 | 1 | Part 4 §19.1.2.1 p.314, OI 2.1.1790 |
| [`v:background`](#vbackground) ◆ | `v:CT_Background` | 7 | 1 | 0 | Part 4 §19.1.2.2 p.341, OI 2.1.1791 |
| [`v:curve`](#vcurve) ◆ | `v:CT_Curve` | 57 | 23 | 1 | Part 4 §19.1.2.3 p.344, OI 2.1.1792 |
| [`v:f`](#vf) | `v:CT_F` | 1 | 0 | 1 | Part 4 §19.1.2.4 p.371, OI 2.1.1793 |
| [`v:fill`](#vfill) ◆ | `v:CT_Fill` | 27 | 1 | 13 | Part 4 §19.1.2.5 p.375, OI 2.1.1794; Part 4 §19.2.2.13 p.752, OI 2.1.1827 |
| [`v:formulas`](#vformulas) ◆ | `v:CT_Formulas` | 0 | 1 | 11 | Part 4 §19.1.2.6 p.386, OI 2.1.1795 |
| [`v:group`](#vgroup) ◆ | `v:CT_Group` | 39 | 35 | 1 | Part 4 §19.1.2.7 p.387, OI 2.1.1796 |
| [`v:h`](#vh) | `v:CT_H` | 9 | 0 | 1 | Part 4 §19.1.2.8 p.409, OI 2.1.1797 |
| [`v:handles`](#vhandles) ◆ | `v:CT_Handles` | 0 | 1 | 11 | Part 4 §19.1.2.9 p.413, OI 2.1.1798 |
| [`v:image`](#vimage) ◆ | `v:CT_Image` | 63 | 23 | 1 | Part 4 §19.1.2.10 p.413, OI 2.1.1799 |
| [`v:imagedata`](#vimagedata) ◆ | `v:CT_ImageData` | 24 | 0 | 11 | Part 4 §19.1.2.11 p.443, OI 2.1.1800 |
| [`v:line`](#vline) ◆ | `v:CT_Line` | 55 | 23 | 1 | Part 4 §19.1.2.12 p.451, OI 2.1.1801 |
| [`v:oval`](#voval) ◆ | `v:CT_Oval` | 53 | 23 | 1 | Part 4 §19.1.2.13 p.478, OI 2.1.1802 |
| [`v:path`](#vpath) ◆ | `v:CT_Path` | 15 | 0 | 11 | Part 4 §19.1.2.14 p.504, OI 2.1.1803 |
| [`v:polyline`](#vpolyline) ◆ | `v:CT_PolyLine` | 54 | 24 | 1 | Part 4 §19.1.2.15 p.512, OI 2.1.1804 |
| [`v:rect`](#vrect) ◆ | `v:CT_Rect` | 53 | 23 | 1 | Part 4 §19.1.2.16 p.539, OI 2.1.1805 |
| [`v:roundrect`](#vroundrect) ◆ | `v:CT_RoundRect` | 54 | 23 | 1 | Part 4 §19.1.2.17 p.565, OI 2.1.1806 |
| [`v:shadow`](#vshadow) ◆ | `v:CT_Shadow` | 11 | 0 | 12 | Part 4 §19.1.2.18 p.591, OI 2.1.1807 |
| [`v:shape`](#vshape) ◆ | `v:CT_Shape` | 58 | 26 | 1 | Part 4 §19.1.2.19 p.597, OI 2.1.1808 |
| [`v:shapetype`](#vshapetype) ◆ | `v:CT_Shapetype` | 56 | 24 | 1 | Part 4 §19.1.2.20 p.625, OI 2.1.1809 |
| [`v:stroke`](#vstroke) ◆ | `v:CT_Stroke` | 29 | 5 | 12 | Part 4 §19.1.2.21 p.653, OI 2.1.1810 |
| [`v:textbox`](#vtextbox) ◆ | `v:CT_Textbox` | 5 | 2 | 12 | Part 4 §19.1.2.22 p.666, OI 2.1.1811 |
| [`v:textpath`](#vtextpath) ◆ | `v:CT_TextPath` | 8 | 0 | 11 | Part 4 §19.1.2.23 p.677, OI 2.1.1812 |

◆ declared globally (can be the root element of a part)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `v:ST_EditAs` | restriction of xsd:string | `canvas`, `orgchart`, `radial`, `cycle`, `stacked`, `venn`, `bullseye` | Part 4 §19.1.3.1 p.690 |
| `v:ST_Ext` | restriction of xsd:string | `view`, `edit`, `backwardCompatible` | Part 4 §19.1.3.2 p.690, OI 2.1.1813 |
| `v:ST_FillMethod` | restriction of xsd:string | `none`, `linear`, `sigma`, `any`, `linear sigma` | Part 4 §19.1.3.3 p.691, OI 2.1.1814 |
| `v:ST_FillType` | restriction of xsd:string | `solid`, `gradient`, `gradientRadial`, `tile`, `pattern`, `frame` | Part 4 §19.1.3.4 p.692, OI 2.1.1815; Part 4 §19.2.3.14 p.825, OI 2.1.1846 |
| `v:ST_ImageAspect` | restriction of xsd:string | `ignore`, `atMost`, `atLeast` | Part 4 §19.1.3.5 p.692 |
| `v:ST_ShadowType` | restriction of xsd:string | `single`, `double`, `emboss`, `perspective` | Part 4 §19.1.3.6 p.693, OI 2.1.1816 |
| `v:ST_StrokeArrowLength` | restriction of xsd:string | `short`, `medium`, `long` | Part 4 §19.1.3.7 p.693 |
| `v:ST_StrokeArrowType` | restriction of xsd:string | `none`, `block`, `classic`, `oval`, `diamond`, `open` | Part 4 §19.1.3.8 p.694 |
| `v:ST_StrokeArrowWidth` | restriction of xsd:string | `narrow`, `medium`, `wide` | Part 4 §19.1.3.9 p.694 |
| `v:ST_StrokeEndCap` | restriction of xsd:string | `flat`, `square`, `round` | Part 4 §19.1.3.10 p.695 |
| `v:ST_StrokeJoinStyle` | restriction of xsd:string | `round`, `bevel`, `miter` | Part 4 §19.1.3.11 p.695 |
| `v:ST_StrokeLineStyle` | restriction of xsd:string | `single`, `thinThin`, `thinThick`, `thickThin`, `thickBetweenThin` | Part 4 §19.1.3.12 p.696 |

## Element details

### v:arc

- Type: `v:CT_Arc`
- Attributes: `alt`, `chromakey`, `class`, `coordorigin`, `coordsize`, `endAngle`, `fillcolor`, `filled`, `href`, `id`, `insetpen`, `o:allowincell`, `o:allowoverlap`, `o:borderbottomcolor`, `o:borderleftcolor`, `o:borderrightcolor`, `o:bordertopcolor`, `o:bullet`, `o:button`, `o:bwmode`, `o:bwnormal`, `o:bwpure`, `o:clip`, `o:cliptowrap`, `o:connectortype`, `o:dgmlayout`, `o:dgmlayoutmru`, `o:dgmnodekind`, `o:doubleclicknotify`, `o:forcedash`, `o:hr`, `o:hralign`, `o:hrnoshade`, `o:hrpct`, `o:hrstd`, `o:insetmode`, `o:ole`, `o:oleicon`, `o:oned`, `o:preferrelative`, `o:regroupid`, `o:spid`, `o:spt`, `o:userdrawn`, `o:userhidden`, `opacity`, `print`, `startAngle`, `strokecolor`, `stroked`, `strokeweight`, `style`, `target`, `title`, `wrapcoords`
- Children: `v:path`, `v:formulas`, `v:handles`, `v:fill`, `v:stroke`, `v:shadow`, `v:textbox`, `v:textpath`, `v:imagedata`, `o:skew`, `o:extrusion`, `o:callout`, `o:lock`, `o:clippath`, `o:signatureline`, `w10:wrap`, `w10:anchorlock`, `w10:bordertop`, `w10:borderbottom`, `w10:borderleft`, `w10:borderright`, `xvml:ClientData`, `pvml:textdata`
- Parents: `v:group`
- Specification: Part 4 §19.1.2.1 p.314, OI 2.1.1790

### v:background

- Type: `v:CT_Background`
- Attributes: `fillcolor`, `filled`, `id`, `o:bwmode`, `o:bwnormal`, `o:bwpure`, `o:targetscreensize`
- Children: `v:fill`
- Specification: Part 4 §19.1.2.2 p.341, OI 2.1.1791

### v:curve

- Type: `v:CT_Curve`
- Attributes: `alt`, `chromakey`, `class`, `control1`, `control2`, `coordorigin`, `coordsize`, `fillcolor`, `filled`, `from`, `href`, `id`, `insetpen`, `o:allowincell`, `o:allowoverlap`, `o:borderbottomcolor`, `o:borderleftcolor`, `o:borderrightcolor`, `o:bordertopcolor`, `o:bullet`, `o:button`, `o:bwmode`, `o:bwnormal`, `o:bwpure`, `o:clip`, `o:cliptowrap`, `o:connectortype`, `o:dgmlayout`, `o:dgmlayoutmru`, `o:dgmnodekind`, `o:doubleclicknotify`, `o:forcedash`, `o:hr`, `o:hralign`, `o:hrnoshade`, `o:hrpct`, `o:hrstd`, `o:insetmode`, `o:ole`, `o:oleicon`, `o:oned`, `o:preferrelative`, `o:regroupid`, `o:spid`, `o:spt`, `o:userdrawn`, `o:userhidden`, `opacity`, `print`, `strokecolor`, `stroked`, `strokeweight`, `style`, `target`, `title`, `to`, `wrapcoords`
- Children: `v:path`, `v:formulas`, `v:handles`, `v:fill`, `v:stroke`, `v:shadow`, `v:textbox`, `v:textpath`, `v:imagedata`, `o:skew`, `o:extrusion`, `o:callout`, `o:lock`, `o:clippath`, `o:signatureline`, `w10:wrap`, `w10:anchorlock`, `w10:bordertop`, `w10:borderbottom`, `w10:borderleft`, `w10:borderright`, `xvml:ClientData`, `pvml:textdata`
- Parents: `v:group`
- Specification: Part 4 §19.1.2.3 p.344, OI 2.1.1792

### v:f

- Type: `v:CT_F`
- Attributes: `eqn`
- Parents: `v:formulas`
- Specification: Part 4 §19.1.2.4 p.371, OI 2.1.1793

### v:fill

- Type: `v:CT_Fill`
- Attributes: `alignshape`, `angle`, `aspect`, `color`, `color2`, `colors`, `focus`, `focusposition`, `focussize`, `id`, `method`, `o:althref`, `o:detectmouseclick`, `o:href`, `o:opacity2`, `o:relid`, `o:title`, `on`, `opacity`, `origin`, `position`, `r:id`, `recolor`, `rotate`, `size`, `src`, `type`
- Children: `o:fill`
- Parents: `o:shapedefaults`, `v:arc`, `v:background`, `v:curve`, `v:group`, `v:image`, `v:line`, `v:oval`, `v:polyline`, `v:rect`, `v:roundrect`, `v:shape`, `v:shapetype`
- Specification: Part 4 §19.1.2.5 p.375, OI 2.1.1794; Part 4 §19.2.2.13 p.752, OI 2.1.1827

### v:formulas

- Type: `v:CT_Formulas`
- Children: `v:f`
- Parents: `v:arc`, `v:curve`, `v:group`, `v:image`, `v:line`, `v:oval`, `v:polyline`, `v:rect`, `v:roundrect`, `v:shape`, `v:shapetype`
- Specification: Part 4 §19.1.2.6 p.386, OI 2.1.1795

### v:group

- Type: `v:CT_Group`
- Attributes: `alt`, `class`, `coordorigin`, `coordsize`, `editas`, `fillcolor`, `filled`, `href`, `id`, `o:allowincell`, `o:allowoverlap`, `o:borderbottomcolor`, `o:borderleftcolor`, `o:borderrightcolor`, `o:bordertopcolor`, `o:bullet`, `o:button`, `o:dgmlayout`, `o:dgmlayoutmru`, `o:dgmnodekind`, `o:doubleclicknotify`, `o:hr`, `o:hralign`, `o:hrnoshade`, `o:hrpct`, `o:hrstd`, `o:insetmode`, `o:oned`, `o:regroupid`, `o:spid`, `o:tablelimits`, `o:tableproperties`, `o:userdrawn`, `o:userhidden`, `print`, `style`, `target`, `title`, `wrapcoords`
- Children: `v:path`, `v:formulas`, `v:handles`, `v:fill`, `v:stroke`, `v:shadow`, `v:textbox`, `v:textpath`, `v:imagedata`, `o:skew`, `o:extrusion`, `o:callout`, `o:lock`, `o:clippath`, `o:signatureline`, `w10:wrap`, `w10:anchorlock`, `w10:bordertop`, `w10:borderbottom`, `w10:borderleft`, `w10:borderright`, `xvml:ClientData`, `pvml:textdata`, `v:group`, `v:shape`, `v:shapetype`, `v:arc`, `v:curve`, `v:image`, `v:line`, `v:oval`, `v:polyline`, `v:rect`, `v:roundrect`, `o:diagram`
- Parents: `v:group`
- Specification: Part 4 §19.1.2.7 p.387, OI 2.1.1796

### v:h

- Type: `v:CT_H`
- Attributes: `invx`, `invy`, `map`, `polar`, `position`, `radiusrange`, `switch`, `xrange`, `yrange`
- Parents: `v:handles`
- Specification: Part 4 §19.1.2.8 p.409, OI 2.1.1797

### v:handles

- Type: `v:CT_Handles`
- Children: `v:h`
- Parents: `v:arc`, `v:curve`, `v:group`, `v:image`, `v:line`, `v:oval`, `v:polyline`, `v:rect`, `v:roundrect`, `v:shape`, `v:shapetype`
- Specification: Part 4 §19.1.2.9 p.413, OI 2.1.1798

### v:image

- Type: `v:CT_Image`
- Attributes: `alt`, `bilevel`, `blacklevel`, `chromakey`, `class`, `coordorigin`, `coordsize`, `cropbottom`, `cropleft`, `cropright`, `croptop`, `fillcolor`, `filled`, `gain`, `gamma`, `grayscale`, `href`, `id`, `insetpen`, `o:allowincell`, `o:allowoverlap`, `o:borderbottomcolor`, `o:borderleftcolor`, `o:borderrightcolor`, `o:bordertopcolor`, `o:bullet`, `o:button`, `o:bwmode`, `o:bwnormal`, `o:bwpure`, `o:clip`, `o:cliptowrap`, `o:connectortype`, `o:dgmlayout`, `o:dgmlayoutmru`, `o:dgmnodekind`, `o:doubleclicknotify`, `o:forcedash`, `o:hr`, `o:hralign`, `o:hrnoshade`, `o:hrpct`, `o:hrstd`, `o:insetmode`, `o:ole`, `o:oleicon`, `o:oned`, `o:preferrelative`, `o:regroupid`, `o:spid`, `o:spt`, `o:userdrawn`, `o:userhidden`, `opacity`, `print`, `src`, `strokecolor`, `stroked`, `strokeweight`, `style`, `target`, `title`, `wrapcoords`
- Children: `v:path`, `v:formulas`, `v:handles`, `v:fill`, `v:stroke`, `v:shadow`, `v:textbox`, `v:textpath`, `v:imagedata`, `o:skew`, `o:extrusion`, `o:callout`, `o:lock`, `o:clippath`, `o:signatureline`, `w10:wrap`, `w10:anchorlock`, `w10:bordertop`, `w10:borderbottom`, `w10:borderleft`, `w10:borderright`, `xvml:ClientData`, `pvml:textdata`
- Parents: `v:group`
- Specification: Part 4 §19.1.2.10 p.413, OI 2.1.1799

### v:imagedata

- Type: `v:CT_ImageData`
- Attributes: `bilevel`, `blacklevel`, `chromakey`, `cropbottom`, `cropleft`, `cropright`, `croptop`, `embosscolor`, `gain`, `gamma`, `grayscale`, `id`, `o:althref`, `o:detectmouseclick`, `o:href`, `o:movie`, `o:oleid`, `o:relid`, `o:title`, `r:href`, `r:id`, `r:pict`, `recolortarget`, `src`
- Parents: `v:arc`, `v:curve`, `v:group`, `v:image`, `v:line`, `v:oval`, `v:polyline`, `v:rect`, `v:roundrect`, `v:shape`, `v:shapetype`
- Specification: Part 4 §19.1.2.11 p.443, OI 2.1.1800

### v:line

- Type: `v:CT_Line`
- Attributes: `alt`, `chromakey`, `class`, `coordorigin`, `coordsize`, `fillcolor`, `filled`, `from`, `href`, `id`, `insetpen`, `o:allowincell`, `o:allowoverlap`, `o:borderbottomcolor`, `o:borderleftcolor`, `o:borderrightcolor`, `o:bordertopcolor`, `o:bullet`, `o:button`, `o:bwmode`, `o:bwnormal`, `o:bwpure`, `o:clip`, `o:cliptowrap`, `o:connectortype`, `o:dgmlayout`, `o:dgmlayoutmru`, `o:dgmnodekind`, `o:doubleclicknotify`, `o:forcedash`, `o:hr`, `o:hralign`, `o:hrnoshade`, `o:hrpct`, `o:hrstd`, `o:insetmode`, `o:ole`, `o:oleicon`, `o:oned`, `o:preferrelative`, `o:regroupid`, `o:spid`, `o:spt`, `o:userdrawn`, `o:userhidden`, `opacity`, `print`, `strokecolor`, `stroked`, `strokeweight`, `style`, `target`, `title`, `to`, `wrapcoords`
- Children: `v:path`, `v:formulas`, `v:handles`, `v:fill`, `v:stroke`, `v:shadow`, `v:textbox`, `v:textpath`, `v:imagedata`, `o:skew`, `o:extrusion`, `o:callout`, `o:lock`, `o:clippath`, `o:signatureline`, `w10:wrap`, `w10:anchorlock`, `w10:bordertop`, `w10:borderbottom`, `w10:borderleft`, `w10:borderright`, `xvml:ClientData`, `pvml:textdata`
- Parents: `v:group`
- Specification: Part 4 §19.1.2.12 p.451, OI 2.1.1801

### v:oval

- Type: `v:CT_Oval`
- Attributes: `alt`, `chromakey`, `class`, `coordorigin`, `coordsize`, `fillcolor`, `filled`, `href`, `id`, `insetpen`, `o:allowincell`, `o:allowoverlap`, `o:borderbottomcolor`, `o:borderleftcolor`, `o:borderrightcolor`, `o:bordertopcolor`, `o:bullet`, `o:button`, `o:bwmode`, `o:bwnormal`, `o:bwpure`, `o:clip`, `o:cliptowrap`, `o:connectortype`, `o:dgmlayout`, `o:dgmlayoutmru`, `o:dgmnodekind`, `o:doubleclicknotify`, `o:forcedash`, `o:hr`, `o:hralign`, `o:hrnoshade`, `o:hrpct`, `o:hrstd`, `o:insetmode`, `o:ole`, `o:oleicon`, `o:oned`, `o:preferrelative`, `o:regroupid`, `o:spid`, `o:spt`, `o:userdrawn`, `o:userhidden`, `opacity`, `print`, `strokecolor`, `stroked`, `strokeweight`, `style`, `target`, `title`, `wrapcoords`
- Children: `v:path`, `v:formulas`, `v:handles`, `v:fill`, `v:stroke`, `v:shadow`, `v:textbox`, `v:textpath`, `v:imagedata`, `o:skew`, `o:extrusion`, `o:callout`, `o:lock`, `o:clippath`, `o:signatureline`, `w10:wrap`, `w10:anchorlock`, `w10:bordertop`, `w10:borderbottom`, `w10:borderleft`, `w10:borderright`, `xvml:ClientData`, `pvml:textdata`
- Parents: `v:group`
- Specification: Part 4 §19.1.2.13 p.478, OI 2.1.1802

### v:path

- Type: `v:CT_Path`
- Attributes: `arrowok`, `fillok`, `gradientshapeok`, `id`, `insetpenok`, `limo`, `o:connectangles`, `o:connectlocs`, `o:connecttype`, `o:extrusionok`, `shadowok`, `strokeok`, `textboxrect`, `textpathok`, `v`
- Parents: `v:arc`, `v:curve`, `v:group`, `v:image`, `v:line`, `v:oval`, `v:polyline`, `v:rect`, `v:roundrect`, `v:shape`, `v:shapetype`
- Specification: Part 4 §19.1.2.14 p.504, OI 2.1.1803

### v:polyline

- Type: `v:CT_PolyLine`
- Attributes: `alt`, `chromakey`, `class`, `coordorigin`, `coordsize`, `fillcolor`, `filled`, `href`, `id`, `insetpen`, `o:allowincell`, `o:allowoverlap`, `o:borderbottomcolor`, `o:borderleftcolor`, `o:borderrightcolor`, `o:bordertopcolor`, `o:bullet`, `o:button`, `o:bwmode`, `o:bwnormal`, `o:bwpure`, `o:clip`, `o:cliptowrap`, `o:connectortype`, `o:dgmlayout`, `o:dgmlayoutmru`, `o:dgmnodekind`, `o:doubleclicknotify`, `o:forcedash`, `o:hr`, `o:hralign`, `o:hrnoshade`, `o:hrpct`, `o:hrstd`, `o:insetmode`, `o:ole`, `o:oleicon`, `o:oned`, `o:preferrelative`, `o:regroupid`, `o:spid`, `o:spt`, `o:userdrawn`, `o:userhidden`, `opacity`, `points`, `print`, `strokecolor`, `stroked`, `strokeweight`, `style`, `target`, `title`, `wrapcoords`
- Children: `v:path`, `v:formulas`, `v:handles`, `v:fill`, `v:stroke`, `v:shadow`, `v:textbox`, `v:textpath`, `v:imagedata`, `o:skew`, `o:extrusion`, `o:callout`, `o:lock`, `o:clippath`, `o:signatureline`, `w10:wrap`, `w10:anchorlock`, `w10:bordertop`, `w10:borderbottom`, `w10:borderleft`, `w10:borderright`, `xvml:ClientData`, `pvml:textdata`, `o:ink`
- Parents: `v:group`
- Specification: Part 4 §19.1.2.15 p.512, OI 2.1.1804

### v:rect

- Type: `v:CT_Rect`
- Attributes: `alt`, `chromakey`, `class`, `coordorigin`, `coordsize`, `fillcolor`, `filled`, `href`, `id`, `insetpen`, `o:allowincell`, `o:allowoverlap`, `o:borderbottomcolor`, `o:borderleftcolor`, `o:borderrightcolor`, `o:bordertopcolor`, `o:bullet`, `o:button`, `o:bwmode`, `o:bwnormal`, `o:bwpure`, `o:clip`, `o:cliptowrap`, `o:connectortype`, `o:dgmlayout`, `o:dgmlayoutmru`, `o:dgmnodekind`, `o:doubleclicknotify`, `o:forcedash`, `o:hr`, `o:hralign`, `o:hrnoshade`, `o:hrpct`, `o:hrstd`, `o:insetmode`, `o:ole`, `o:oleicon`, `o:oned`, `o:preferrelative`, `o:regroupid`, `o:spid`, `o:spt`, `o:userdrawn`, `o:userhidden`, `opacity`, `print`, `strokecolor`, `stroked`, `strokeweight`, `style`, `target`, `title`, `wrapcoords`
- Children: `v:path`, `v:formulas`, `v:handles`, `v:fill`, `v:stroke`, `v:shadow`, `v:textbox`, `v:textpath`, `v:imagedata`, `o:skew`, `o:extrusion`, `o:callout`, `o:lock`, `o:clippath`, `o:signatureline`, `w10:wrap`, `w10:anchorlock`, `w10:bordertop`, `w10:borderbottom`, `w10:borderleft`, `w10:borderright`, `xvml:ClientData`, `pvml:textdata`
- Parents: `v:group`
- Specification: Part 4 §19.1.2.16 p.539, OI 2.1.1805

### v:roundrect

- Type: `v:CT_RoundRect`
- Attributes: `alt`, `arcsize`, `chromakey`, `class`, `coordorigin`, `coordsize`, `fillcolor`, `filled`, `href`, `id`, `insetpen`, `o:allowincell`, `o:allowoverlap`, `o:borderbottomcolor`, `o:borderleftcolor`, `o:borderrightcolor`, `o:bordertopcolor`, `o:bullet`, `o:button`, `o:bwmode`, `o:bwnormal`, `o:bwpure`, `o:clip`, `o:cliptowrap`, `o:connectortype`, `o:dgmlayout`, `o:dgmlayoutmru`, `o:dgmnodekind`, `o:doubleclicknotify`, `o:forcedash`, `o:hr`, `o:hralign`, `o:hrnoshade`, `o:hrpct`, `o:hrstd`, `o:insetmode`, `o:ole`, `o:oleicon`, `o:oned`, `o:preferrelative`, `o:regroupid`, `o:spid`, `o:spt`, `o:userdrawn`, `o:userhidden`, `opacity`, `print`, `strokecolor`, `stroked`, `strokeweight`, `style`, `target`, `title`, `wrapcoords`
- Children: `v:path`, `v:formulas`, `v:handles`, `v:fill`, `v:stroke`, `v:shadow`, `v:textbox`, `v:textpath`, `v:imagedata`, `o:skew`, `o:extrusion`, `o:callout`, `o:lock`, `o:clippath`, `o:signatureline`, `w10:wrap`, `w10:anchorlock`, `w10:bordertop`, `w10:borderbottom`, `w10:borderleft`, `w10:borderright`, `xvml:ClientData`, `pvml:textdata`
- Parents: `v:group`
- Specification: Part 4 §19.1.2.17 p.565, OI 2.1.1806

### v:shadow

- Type: `v:CT_Shadow`
- Attributes: `color`, `color2`, `id`, `matrix`, `obscured`, `offset`, `offset2`, `on`, `opacity`, `origin`, `type`
- Parents: `o:shapedefaults`, `v:arc`, `v:curve`, `v:group`, `v:image`, `v:line`, `v:oval`, `v:polyline`, `v:rect`, `v:roundrect`, `v:shape`, `v:shapetype`
- Specification: Part 4 §19.1.2.18 p.591, OI 2.1.1807

### v:shape

- Type: `v:CT_Shape`
- Attributes: `adj`, `alt`, `chromakey`, `class`, `coordorigin`, `coordsize`, `equationxml`, `fillcolor`, `filled`, `href`, `id`, `insetpen`, `o:allowincell`, `o:allowoverlap`, `o:borderbottomcolor`, `o:borderleftcolor`, `o:borderrightcolor`, `o:bordertopcolor`, `o:bullet`, `o:button`, `o:bwmode`, `o:bwnormal`, `o:bwpure`, `o:clip`, `o:cliptowrap`, `o:connectortype`, `o:dgmlayout`, `o:dgmlayoutmru`, `o:dgmnodekind`, `o:doubleclicknotify`, `o:forcedash`, `o:gfxdata`, `o:hr`, `o:hralign`, `o:hrnoshade`, `o:hrpct`, `o:hrstd`, `o:insetmode`, `o:ole`, `o:oleicon`, `o:oned`, `o:preferrelative`, `o:regroupid`, `o:spid`, `o:spt`, `o:userdrawn`, `o:userhidden`, `opacity`, `path`, `print`, `strokecolor`, `stroked`, `strokeweight`, `style`, `target`, `title`, `type`, `wrapcoords`
- Children: `v:path`, `v:formulas`, `v:handles`, `v:fill`, `v:stroke`, `v:shadow`, `v:textbox`, `v:textpath`, `v:imagedata`, `o:skew`, `o:extrusion`, `o:callout`, `o:lock`, `o:clippath`, `o:signatureline`, `w10:wrap`, `w10:anchorlock`, `w10:bordertop`, `w10:borderbottom`, `w10:borderleft`, `w10:borderright`, `xvml:ClientData`, `pvml:textdata`, `o:ink`, `pvml:iscomment`, `o:equationxml`
- Parents: `v:group`
- Specification: Part 4 §19.1.2.19 p.597, OI 2.1.1808

### v:shapetype

- Type: `v:CT_Shapetype`
- Attributes: `adj`, `alt`, `chromakey`, `class`, `coordorigin`, `coordsize`, `fillcolor`, `filled`, `href`, `id`, `insetpen`, `o:allowincell`, `o:allowoverlap`, `o:borderbottomcolor`, `o:borderleftcolor`, `o:borderrightcolor`, `o:bordertopcolor`, `o:bullet`, `o:button`, `o:bwmode`, `o:bwnormal`, `o:bwpure`, `o:clip`, `o:cliptowrap`, `o:connectortype`, `o:dgmlayout`, `o:dgmlayoutmru`, `o:dgmnodekind`, `o:doubleclicknotify`, `o:forcedash`, `o:hr`, `o:hralign`, `o:hrnoshade`, `o:hrpct`, `o:hrstd`, `o:insetmode`, `o:master`, `o:ole`, `o:oleicon`, `o:oned`, `o:preferrelative`, `o:regroupid`, `o:spid`, `o:spt`, `o:userdrawn`, `o:userhidden`, `opacity`, `path`, `print`, `strokecolor`, `stroked`, `strokeweight`, `style`, `target`, `title`, `wrapcoords`
- Children: `v:path`, `v:formulas`, `v:handles`, `v:fill`, `v:stroke`, `v:shadow`, `v:textbox`, `v:textpath`, `v:imagedata`, `o:skew`, `o:extrusion`, `o:callout`, `o:lock`, `o:clippath`, `o:signatureline`, `w10:wrap`, `w10:anchorlock`, `w10:bordertop`, `w10:borderbottom`, `w10:borderleft`, `w10:borderright`, `xvml:ClientData`, `pvml:textdata`, `o:complex`
- Parents: `v:group`
- Specification: Part 4 §19.1.2.20 p.625, OI 2.1.1809

### v:stroke

- Type: `v:CT_Stroke`
- Attributes: `color`, `color2`, `dashstyle`, `endarrow`, `endarrowlength`, `endarrowwidth`, `endcap`, `filltype`, `id`, `imagealignshape`, `imageaspect`, `imagesize`, `insetpen`, `joinstyle`, `linestyle`, `miterlimit`, `o:althref`, `o:forcedash`, `o:href`, `o:relid`, `o:title`, `on`, `opacity`, `r:id`, `src`, `startarrow`, `startarrowlength`, `startarrowwidth`, `weight`
- Children: `o:left`, `o:top`, `o:right`, `o:bottom`, `o:column`
- Parents: `o:shapedefaults`, `v:arc`, `v:curve`, `v:group`, `v:image`, `v:line`, `v:oval`, `v:polyline`, `v:rect`, `v:roundrect`, `v:shape`, `v:shapetype`
- Specification: Part 4 §19.1.2.21 p.653, OI 2.1.1810

### v:textbox

- Type: `v:CT_Textbox`
- Attributes: `id`, `inset`, `o:insetmode`, `o:singleclick`, `style`
- Children: `w:txbxContent`, `any(##local)`
- Parents: `o:shapedefaults`, `v:arc`, `v:curve`, `v:group`, `v:image`, `v:line`, `v:oval`, `v:polyline`, `v:rect`, `v:roundrect`, `v:shape`, `v:shapetype`
- Specification: Part 4 §19.1.2.22 p.666, OI 2.1.1811

### v:textpath

- Type: `v:CT_TextPath`
- Attributes: `fitpath`, `fitshape`, `id`, `on`, `string`, `style`, `trim`, `xscale`
- Parents: `v:arc`, `v:curve`, `v:group`, `v:image`, `v:line`, `v:oval`, `v:polyline`, `v:rect`, `v:roundrect`, `v:shape`, `v:shapetype`
- Specification: Part 4 §19.1.2.23 p.677, OI 2.1.1812

`!` marks required attributes.
