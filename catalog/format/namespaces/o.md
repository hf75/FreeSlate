# o: VML (legacy drawing)

`urn:schemas-microsoft-com:office:office`

- Source: ECMA-376-4 transitional schema vml-officeDrawing.xsd
- Office version: 2007
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 32 | 273 | 53 | 25 | 23 | 80 | 57 | 33 |

Full detail (content models, attribute types, defaults): [o.json](o.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`o:FieldCodes`](#ofieldcodes) | `xsd:string` | 0 | 0 | 1 | Part 4 §19.2.2.12 p.751, OI 2.1.1826 |
| [`o:LinkType`](#olinktype) | `o:ST_OLELinkType` | 0 | 0 | 1 | Part 4 §19.2.2.17 p.767, OI 2.1.1831 |
| [`o:LockedField`](#olockedfield) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.2.2.19 p.769, OI 2.1.1833 |
| [`o:OLEObject`](#ooleobject) ◆ | `o:CT_OLEObject` | 7 | 3 | 0 | Part 4 §19.2.2.20 p.769, OI 2.1.1834 |
| [`o:bottom`](#obottom) ◆ | `o:CT_StrokeChild` | 27 | 0 | 1 | Part 4 §19.2.2.1 p.699, OI 2.1.1817 |
| [`o:callout`](#ocallout) ◆ | `o:CT_Callout` | 14 | 0 | 12 | Part 4 §19.2.2.2 p.711, OI 2.1.1818 |
| [`o:clippath`](#oclippath) ◆ | `o:CT_ClipPath` | 1 | 0 | 11 | Part 4 §19.2.2.3 p.713, OI 2.1.1819 |
| [`o:colormenu`](#ocolormenu) | `o:CT_ColorMenu` | 5 | 0 | 1 | Part 4 §19.2.2.4 p.716, OI 2.1.1820 |
| [`o:colormru`](#ocolormru) | `o:CT_ColorMru` | 2 | 0 | 1 | Part 4 §19.2.2.5 p.717, OI 2.1.1821 |
| [`o:column`](#ocolumn) ◆ | `o:CT_StrokeChild` | 27 | 0 | 1 | Part 4 §19.2.2.6 p.718, OI 2.1.1822 |
| [`o:complex`](#ocomplex) ◆ | `o:CT_Complex` | 1 | 0 | 1 | Part 4 §19.2.2.7 p.730, OI 2.1.1823 |
| [`o:diagram`](#odiagram) ◆ | `o:CT_Diagram` | 10 | 1 | 1 | Part 4 §19.2.2.8 p.731, OI 2.1.1824 |
| [`o:entry`](#oentry) | `o:CT_Entry` | 2 | 0 | 1 | Part 4 §19.2.2.9 p.735 |
| [`o:equationxml`](#oequationxml) ◆ | `o:CT_EquationXml` | 1 | 1 | 1 | Part 4 §19.2.2.10 p.735 |
| [`o:extrusion`](#oextrusion) ◆ | `o:CT_Extrusion` | 33 | 0 | 12 | Part 4 §19.2.2.11 p.736, OI 2.1.1825 |
| [`o:fill`](#ofill) ◆ | `o:CT_Fill` | 2 | 0 | 1 | Part 4 §19.1.2.5 p.375, OI 2.1.1794; Part 4 §19.2.2.13 p.752, OI 2.1.1827 |
| [`o:idmap`](#oidmap) | `o:CT_IdMap` | 2 | 0 | 1 | Part 4 §19.2.2.14 p.752, OI 2.1.1828 |
| [`o:ink`](#oink) ◆ | `o:CT_Ink` | 3 | 0 | 2 | Part 4 §19.2.2.15 p.753, OI 2.1.1829 |
| [`o:left`](#oleft) ◆ | `o:CT_StrokeChild` | 27 | 0 | 1 | Part 4 §19.2.2.16 p.755, OI 2.1.1830 |
| [`o:lock`](#olock) ◆ | `o:CT_Lock` | 12 | 0 | 12 | Part 4 §19.2.2.18 p.767, OI 2.1.1832 |
| [`o:proxy`](#oproxy) | `o:CT_Proxy` | 4 | 0 | 1 | Part 4 §19.2.2.21 p.772, OI 2.1.1835 |
| [`o:r`](#or) | `o:CT_R` | 4 | 1 | 1 | Part 4 §19.2.2.22 p.773, OI 2.1.1836 |
| [`o:regrouptable`](#oregrouptable) | `o:CT_RegroupTable` | 1 | 1 | 1 | Part 4 §19.2.2.23 p.774 |
| [`o:rel`](#orel) | `o:CT_Relation` | 4 | 0 | 1 | Part 4 §19.2.2.24 p.775, OI 2.1.1837 |
| [`o:relationtable`](#orelationtable) | `o:CT_RelationTable` | 1 | 1 | 1 | Part 4 §19.2.2.25 p.777 |
| [`o:right`](#oright) ◆ | `o:CT_StrokeChild` | 27 | 0 | 1 | Part 4 §19.2.2.26 p.778, OI 2.1.1838 |
| [`o:rules`](#orules) | `o:CT_Rules` | 1 | 1 | 1 | Part 4 §19.2.2.27 p.790 |
| [`o:shapedefaults`](#oshapedefaults) ◆ | `o:CT_ShapeDefaults` | 8 | 10 | 0 | Part 4 §19.2.2.28 p.791, OI 2.1.1839 |
| [`o:shapelayout`](#oshapelayout) ◆ | `o:CT_ShapeLayout` | 1 | 3 | 0 | Part 4 §19.2.2.29 p.802, OI 2.1.1840 |
| [`o:signatureline`](#osignatureline) ◆ | `o:CT_SignatureLine` | 13 | 0 | 11 | Part 4 §19.2.2.30 p.802, OI 2.1.1841 |
| [`o:skew`](#oskew) ◆ | `o:CT_Skew` | 6 | 0 | 12 | Part 4 §19.2.2.31 p.806, OI 2.1.1842 |
| [`o:top`](#otop) ◆ | `o:CT_StrokeChild` | 27 | 0 | 1 | Part 4 §19.2.2.32 p.808, OI 2.1.1843 |

◆ declared globally (can be the root element of a part)

## Global attributes

Attributes this namespace adds to elements of other namespaces.

| Attribute | Type | Specification |
|---|---|---|
| `o:allowincell` | `s:ST_TrueFalse` |  |
| `o:allowoverlap` | `s:ST_TrueFalse` |  |
| `o:althref` | `xsd:string` |  |
| `o:borderbottomcolor` | `xsd:string` |  |
| `o:borderleftcolor` | `xsd:string` |  |
| `o:borderrightcolor` | `xsd:string` |  |
| `o:bordertopcolor` | `xsd:string` |  |
| `o:bullet` | `s:ST_TrueFalse` |  |
| `o:button` | `s:ST_TrueFalse` |  |
| `o:bwmode` | `o:ST_BWMode` |  |
| `o:bwnormal` | `o:ST_BWMode` |  |
| `o:bwpure` | `o:ST_BWMode` |  |
| `o:clip` | `s:ST_TrueFalse` |  |
| `o:cliptowrap` | `s:ST_TrueFalse` |  |
| `o:connectangles` | `xsd:string` |  |
| `o:connectlocs` | `xsd:string` |  |
| `o:connectortype` | `o:ST_ConnectorType` |  |
| `o:connecttype` | `o:ST_ConnectType` |  |
| `o:detectmouseclick` | `s:ST_TrueFalse` |  |
| `o:dgmlayout` | `o:ST_DiagramLayout` |  |
| `o:dgmlayoutmru` | `o:ST_DiagramLayout` |  |
| `o:dgmnodekind` | `xsd:integer` |  |
| `o:doubleclicknotify` | `s:ST_TrueFalse` |  |
| `o:extrusionok` | `s:ST_TrueFalse` |  |
| `o:forcedash` | `s:ST_TrueFalse` |  |
| `o:gfxdata` | `xsd:base64Binary` |  |
| `o:hr` | `s:ST_TrueFalse` |  |
| `o:hralign` | `o:ST_HrAlign` |  |
| `o:href` | `xsd:string` |  |
| `o:hrnoshade` | `s:ST_TrueFalse` |  |
| `o:hrpct` | `xsd:float` |  |
| `o:hrstd` | `s:ST_TrueFalse` |  |
| `o:insetmode` | `o:ST_InsetMode` |  |
| `o:master` | `xsd:string` |  |
| `o:movie` | `xsd:float` |  |
| `o:ole` | `s:ST_TrueFalseBlank` |  |
| `o:oleicon` | `s:ST_TrueFalse` |  |
| `o:oleid` | `xsd:float` |  |
| `o:oned` | `s:ST_TrueFalse` |  |
| `o:opacity2` | `xsd:string` |  |
| `o:preferrelative` | `s:ST_TrueFalse` |  |
| `o:regroupid` | `xsd:integer` |  |
| `o:relid` | `r:ST_RelationshipId` |  |
| `o:singleclick` | `s:ST_TrueFalse` |  |
| `o:spid` | `xsd:string` |  |
| `o:spt` | `xsd:float` |  |
| `o:tablelimits` | `xsd:string` |  |
| `o:tableproperties` | `xsd:string` |  |
| `o:targetscreensize` | `o:ST_ScreenSize` |  |
| `o:title` | `xsd:string` |  |
| `o:userdrawn` | `s:ST_TrueFalse` |  |
| `o:userhidden` | `s:ST_TrueFalse` |  |
| `o:wrapcoords` | `xsd:string` |  |

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `o:ST_AlternateMathContentType` | restriction of xsd:string |  | Part 4 §19.2.3.1 p.820 |
| `o:ST_Angle` | restriction of xsd:string | `any`, `30`, `45`, `60`, `90`, `auto` | Part 4 §19.2.3.2 p.820 |
| `o:ST_BWMode` | restriction of xsd:string | `color`, `auto`, `grayScale`, `lightGrayscale`, `inverseGray`, `grayOutline`, `highContrast`, `black`, `white`, `hide`, `undrawn`, `blackTextAndLines` | Part 4 §19.2.3.3 p.820, OI 2.1.1844 |
| `o:ST_CalloutDrop` | restriction of xsd:string |  | Part 4 §19.2.3.4 p.821, OI 2.1.1845 |
| `o:ST_CalloutPlacement` | restriction of xsd:string | `top`, `center`, `bottom`, `user` | Part 4 §19.2.3.5 p.821 |
| `o:ST_ColorMode` | restriction of xsd:string | `auto`, `custom` | Part 4 §19.2.3.6 p.822 |
| `o:ST_ConnectType` | restriction of xsd:string | `none`, `rect`, `segments`, `custom` | Part 4 §19.2.3.8 p.822 |
| `o:ST_ConnectorType` | restriction of xsd:string | `none`, `straight`, `elbow`, `curved` | Part 4 §19.2.3.7 p.822 |
| `o:ST_ContentType` | restriction of xsd:string |  | Part 4 §19.2.3.9 p.823 |
| `o:ST_DiagramLayout` | restriction of xsd:integer | `0`, `1`, `2`, `3` | Part 4 §19.2.3.10 p.823 |
| `o:ST_ExtrusionPlane` | restriction of xsd:string | `XY`, `ZX`, `YZ` | Part 4 §19.2.3.11 p.824 |
| `o:ST_ExtrusionRender` | restriction of xsd:string | `solid`, `wireFrame`, `boundingCube` | Part 4 §19.2.3.12 p.824 |
| `o:ST_ExtrusionType` | restriction of xsd:string | `perspective`, `parallel` | Part 4 §19.2.3.13 p.824 |
| `o:ST_FillType` | restriction of xsd:string | `gradientCenter`, `solid`, `pattern`, `tile`, `frame`, `gradientUnscaled`, `gradientRadial`, `gradient`, `background` | Part 4 §19.1.3.4 p.692, OI 2.1.1815; Part 4 §19.2.3.14 p.825, OI 2.1.1846 |
| `o:ST_How` | restriction of xsd:string | `top`, `middle`, `bottom`, `left`, `center`, `right` | Part 4 §19.2.3.15 p.826 |
| `o:ST_HrAlign` | restriction of xsd:string | `left`, `right`, `center` | Part 4 §19.2.3.16 p.827 |
| `o:ST_InsetMode` | restriction of xsd:string | `auto`, `custom` | Part 4 §19.2.3.17 p.827 |
| `o:ST_OLEDrawAspect` | restriction of xsd:string | `Content`, `Icon` | Part 4 §19.2.3.18 p.827 |
| `o:ST_OLELinkType` | restriction of xsd:string |  | Part 4 §19.2.3.19 p.828, OI 2.1.1847 |
| `o:ST_OLEType` | restriction of xsd:string | `Embed`, `Link` | Part 4 §19.2.3.20 p.828 |
| `o:ST_OLEUpdateMode` | restriction of xsd:string | `Always`, `OnCall` | Part 4 §19.2.3.21 p.828 |
| `o:ST_RType` | restriction of xsd:string | `arc`, `callout`, `connector`, `align` | Part 4 §19.2.3.22 p.829 |
| `o:ST_ScreenSize` | restriction of xsd:string | `544,376`, `640,480`, `720,512`, `800,600`, `1024,768`, `1152,862` | Part 4 §19.2.3.23 p.829 |

## Element details

### o:FieldCodes

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `o:OLEObject`
- Specification: Part 4 §19.2.2.12 p.751, OI 2.1.1826

### o:LinkType

- Type: `o:ST_OLELinkType` (text: `o:ST_OLELinkType`)
- Parents: `o:OLEObject`
- Specification: Part 4 §19.2.2.17 p.767, OI 2.1.1831

### o:LockedField

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `o:OLEObject`
- Specification: Part 4 §19.2.2.19 p.769, OI 2.1.1833

### o:OLEObject

- Type: `o:CT_OLEObject`
- Attributes: `DrawAspect`, `ObjectID`, `ProgID`, `ShapeID`, `Type`, `UpdateMode`, `r:id`
- Children: `o:LinkType`, `o:LockedField`, `o:FieldCodes`
- Specification: Part 4 §19.2.2.20 p.769, OI 2.1.1834

### o:bottom

- Type: `o:CT_StrokeChild`
- Attributes: `color`, `color2`, `dashstyle`, `endarrow`, `endarrowlength`, `endarrowwidth`, `endcap`, `filltype`, `imagealignshape`, `imageaspect`, `imagesize`, `insetpen`, `joinstyle`, `linestyle`, `miterlimit`, `o:althref`, `o:forcedash`, `o:href`, `o:title`, `on`, `opacity`, `src`, `startarrow`, `startarrowlength`, `startarrowwidth`, `v:ext`, `weight`
- Parents: `v:stroke`
- Specification: Part 4 §19.2.2.1 p.699, OI 2.1.1817

### o:callout

- Type: `o:CT_Callout`
- Attributes: `accentbar`, `angle`, `distance`, `drop`, `dropauto`, `gap`, `length`, `lengthspecified`, `minusx`, `minusy`, `on`, `textborder`, `type`, `v:ext`
- Parents: `o:shapedefaults`, `v:arc`, `v:curve`, `v:group`, `v:image`, `v:line`, `v:oval`, `v:polyline`, `v:rect`, `v:roundrect`, `v:shape`, `v:shapetype`
- Specification: Part 4 §19.2.2.2 p.711, OI 2.1.1818

### o:clippath

- Type: `o:CT_ClipPath`
- Attributes: `o:v`!
- Parents: `v:arc`, `v:curve`, `v:group`, `v:image`, `v:line`, `v:oval`, `v:polyline`, `v:rect`, `v:roundrect`, `v:shape`, `v:shapetype`
- Specification: Part 4 §19.2.2.3 p.713, OI 2.1.1819

### o:colormenu

- Type: `o:CT_ColorMenu`
- Attributes: `extrusioncolor`, `fillcolor`, `shadowcolor`, `strokecolor`, `v:ext`
- Parents: `o:shapedefaults`
- Specification: Part 4 §19.2.2.4 p.716, OI 2.1.1820

### o:colormru

- Type: `o:CT_ColorMru`
- Attributes: `colors`, `v:ext`
- Parents: `o:shapedefaults`
- Specification: Part 4 §19.2.2.5 p.717, OI 2.1.1821

### o:column

- Type: `o:CT_StrokeChild`
- Attributes: `color`, `color2`, `dashstyle`, `endarrow`, `endarrowlength`, `endarrowwidth`, `endcap`, `filltype`, `imagealignshape`, `imageaspect`, `imagesize`, `insetpen`, `joinstyle`, `linestyle`, `miterlimit`, `o:althref`, `o:forcedash`, `o:href`, `o:title`, `on`, `opacity`, `src`, `startarrow`, `startarrowlength`, `startarrowwidth`, `v:ext`, `weight`
- Parents: `v:stroke`
- Specification: Part 4 §19.2.2.6 p.718, OI 2.1.1822

### o:complex

- Type: `o:CT_Complex`
- Attributes: `v:ext`
- Parents: `v:shapetype`
- Specification: Part 4 §19.2.2.7 p.730, OI 2.1.1823

### o:diagram

- Type: `o:CT_Diagram`
- Attributes: `autoformat`, `autolayout`, `constrainbounds`, `dgmbasetextscale`, `dgmfontsize`, `dgmscalex`, `dgmscaley`, `dgmstyle`, `reverse`, `v:ext`
- Children: `o:relationtable`
- Parents: `v:group`
- Specification: Part 4 §19.2.2.8 p.731, OI 2.1.1824

### o:entry

- Type: `o:CT_Entry`
- Attributes: `new`, `old`
- Parents: `o:regrouptable`
- Specification: Part 4 §19.2.2.9 p.735

### o:equationxml

- Type: `o:CT_EquationXml`
- Attributes: `contentType`
- Children: `any(##any)`
- Parents: `v:shape`
- Specification: Part 4 §19.2.2.10 p.735

### o:extrusion

- Type: `o:CT_Extrusion`
- Attributes: `autorotationcenter`, `backdepth`, `brightness`, `color`, `colormode`, `diffusity`, `edge`, `facet`, `foredepth`, `lightface`, `lightharsh`, `lightharsh2`, `lightlevel`, `lightlevel2`, `lightposition`, `lightposition2`, `lockrotationcenter`, `metal`, `on`, `orientation`, `orientationangle`, `plane`, `render`, `rotationangle`, `rotationcenter`, `shininess`, `skewamt`, `skewangle`, `specularity`, `type`, `v:ext`, `viewpoint`, `viewpointorigin`
- Parents: `o:shapedefaults`, `v:arc`, `v:curve`, `v:group`, `v:image`, `v:line`, `v:oval`, `v:polyline`, `v:rect`, `v:roundrect`, `v:shape`, `v:shapetype`
- Specification: Part 4 §19.2.2.11 p.736, OI 2.1.1825

### o:fill

- Type: `o:CT_Fill`
- Attributes: `type`, `v:ext`
- Parents: `v:fill`
- Specification: Part 4 §19.1.2.5 p.375, OI 2.1.1794; Part 4 §19.2.2.13 p.752, OI 2.1.1827

### o:idmap

- Type: `o:CT_IdMap`
- Attributes: `data`, `v:ext`
- Parents: `o:shapelayout`
- Specification: Part 4 §19.2.2.14 p.752, OI 2.1.1828

### o:ink

- Type: `o:CT_Ink`
- Attributes: `annotation`, `contentType`, `i`
- Parents: `v:polyline`, `v:shape`
- Specification: Part 4 §19.2.2.15 p.753, OI 2.1.1829

### o:left

- Type: `o:CT_StrokeChild`
- Attributes: `color`, `color2`, `dashstyle`, `endarrow`, `endarrowlength`, `endarrowwidth`, `endcap`, `filltype`, `imagealignshape`, `imageaspect`, `imagesize`, `insetpen`, `joinstyle`, `linestyle`, `miterlimit`, `o:althref`, `o:forcedash`, `o:href`, `o:title`, `on`, `opacity`, `src`, `startarrow`, `startarrowlength`, `startarrowwidth`, `v:ext`, `weight`
- Parents: `v:stroke`
- Specification: Part 4 §19.2.2.16 p.755, OI 2.1.1830

### o:lock

- Type: `o:CT_Lock`
- Attributes: `adjusthandles`, `aspectratio`, `cropping`, `grouping`, `position`, `rotation`, `selection`, `shapetype`, `text`, `ungrouping`, `v:ext`, `verticies`
- Parents: `o:shapedefaults`, `v:arc`, `v:curve`, `v:group`, `v:image`, `v:line`, `v:oval`, `v:polyline`, `v:rect`, `v:roundrect`, `v:shape`, `v:shapetype`
- Specification: Part 4 §19.2.2.18 p.767, OI 2.1.1832

### o:proxy

- Type: `o:CT_Proxy`
- Attributes: `connectloc`, `end`, `idref`, `start`
- Parents: `o:r`
- Specification: Part 4 §19.2.2.21 p.772, OI 2.1.1835

### o:r

- Type: `o:CT_R`
- Attributes: `how`, `id`!, `idref`, `type`
- Children: `o:proxy`
- Parents: `o:rules`
- Specification: Part 4 §19.2.2.22 p.773, OI 2.1.1836

### o:regrouptable

- Type: `o:CT_RegroupTable`
- Attributes: `v:ext`
- Children: `o:entry`
- Parents: `o:shapelayout`
- Specification: Part 4 §19.2.2.23 p.774

### o:rel

- Type: `o:CT_Relation`
- Attributes: `idcntr`, `iddest`, `idsrc`, `v:ext`
- Parents: `o:relationtable`
- Specification: Part 4 §19.2.2.24 p.775, OI 2.1.1837

### o:relationtable

- Type: `o:CT_RelationTable`
- Attributes: `v:ext`
- Children: `o:rel`
- Parents: `o:diagram`
- Specification: Part 4 §19.2.2.25 p.777

### o:right

- Type: `o:CT_StrokeChild`
- Attributes: `color`, `color2`, `dashstyle`, `endarrow`, `endarrowlength`, `endarrowwidth`, `endcap`, `filltype`, `imagealignshape`, `imageaspect`, `imagesize`, `insetpen`, `joinstyle`, `linestyle`, `miterlimit`, `o:althref`, `o:forcedash`, `o:href`, `o:title`, `on`, `opacity`, `src`, `startarrow`, `startarrowlength`, `startarrowwidth`, `v:ext`, `weight`
- Parents: `v:stroke`
- Specification: Part 4 §19.2.2.26 p.778, OI 2.1.1838

### o:rules

- Type: `o:CT_Rules`
- Attributes: `v:ext`
- Children: `o:r`
- Parents: `o:shapelayout`
- Specification: Part 4 §19.2.2.27 p.790

### o:shapedefaults

- Type: `o:CT_ShapeDefaults`
- Attributes: `fill`, `fillcolor`, `o:allowincell`, `spidmax`, `stroke`, `strokecolor`, `style`, `v:ext`
- Children: `v:fill`, `v:stroke`, `v:textbox`, `v:shadow`, `o:skew`, `o:extrusion`, `o:callout`, `o:lock`, `o:colormru`, `o:colormenu`
- Specification: Part 4 §19.2.2.28 p.791, OI 2.1.1839

### o:shapelayout

- Type: `o:CT_ShapeLayout`
- Attributes: `v:ext`
- Children: `o:idmap`, `o:regrouptable`, `o:rules`
- Specification: Part 4 §19.2.2.29 p.802, OI 2.1.1840

### o:signatureline

- Type: `o:CT_SignatureLine`
- Attributes: `addlxml`, `allowcomments`, `id`, `issignatureline`, `o:suggestedsigner`, `o:suggestedsigner2`, `o:suggestedsigneremail`, `provid`, `showsigndate`, `signinginstructions`, `signinginstructionsset`, `sigprovurl`, `v:ext`
- Parents: `v:arc`, `v:curve`, `v:group`, `v:image`, `v:line`, `v:oval`, `v:polyline`, `v:rect`, `v:roundrect`, `v:shape`, `v:shapetype`
- Specification: Part 4 §19.2.2.30 p.802, OI 2.1.1841

### o:skew

- Type: `o:CT_Skew`
- Attributes: `id`, `matrix`, `offset`, `on`, `origin`, `v:ext`
- Parents: `o:shapedefaults`, `v:arc`, `v:curve`, `v:group`, `v:image`, `v:line`, `v:oval`, `v:polyline`, `v:rect`, `v:roundrect`, `v:shape`, `v:shapetype`
- Specification: Part 4 §19.2.2.31 p.806, OI 2.1.1842

### o:top

- Type: `o:CT_StrokeChild`
- Attributes: `color`, `color2`, `dashstyle`, `endarrow`, `endarrowlength`, `endarrowwidth`, `endcap`, `filltype`, `imagealignshape`, `imageaspect`, `imagesize`, `insetpen`, `joinstyle`, `linestyle`, `miterlimit`, `o:althref`, `o:forcedash`, `o:href`, `o:title`, `on`, `opacity`, `src`, `startarrow`, `startarrowlength`, `startarrowwidth`, `v:ext`, `weight`
- Parents: `v:stroke`
- Specification: Part 4 §19.2.2.32 p.808, OI 2.1.1843

`!` marks required attributes.
