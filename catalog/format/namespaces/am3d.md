# am3d: DrawingML

`http://schemas.microsoft.com/office/drawing/2017/model3d`

- Source: [MS-ODRAWXML] v20261006 §5.29
- Office version: 2019
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 31 | 49 | 0 | 16 | 1 | 0 | 19 | 0 |

Full detail (content models, attribute types, defaults): [am3d.json](am3d.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`am3d:ambientLight`](#am3dambientlight) | `am3d:CT_AmbientLight` | 1 | 3 | 1 | MS-ODRAWXML §2.31.3.1 p.298 |
| [`am3d:attrSrcUrl`](#am3dattrsrcurl) | `a1611:CT_PictureAttributionSourceURL` | 1 | 0 | 1 | MS-ODRAWXML §2.27.3.1 p.269 |
| [`am3d:blip`](#am3dblip) | `a:CT_Blip` | 3 | 18 | 1 |  |
| [`am3d:camera`](#am3dcamera) | `am3d:CT_Model3DCamera` | 0 | 6 | 1 | MS-ODRAWXML §2.31.3.4 p.301 |
| [`am3d:clr`](#am3dclr) | `a:CT_Color` | 0 | 6 | 4 |  |
| [`am3d:dirLight`](#am3ddirlight) | `am3d:CT_DirectionalLight` | 2 | 5 | 1 | MS-ODRAWXML §2.31.3.2 p.298 |
| [`am3d:extLst`](#am3dextlst) | `a:CT_OfficeArtExtensionList` | 0 | 1 | 11 |  |
| [`am3d:illuminance`](#am3dilluminance) | `am3d:CT_PositiveRatio` | 2 | 0 | 2 | MS-ODRAWXML §2.31.3.16 p.308 |
| [`am3d:intensity`](#am3dintensity) | `am3d:CT_PositiveRatio` | 2 | 0 | 2 | MS-ODRAWXML §2.31.3.16 p.308 |
| [`am3d:lookAt`](#am3dlookat) | `a:CT_Point3D` | 3 | 0 | 3 |  |
| [`am3d:meterPerModelUnit`](#am3dmeterpermodelunit) | `am3d:CT_PositiveRatio` | 2 | 0 | 1 | MS-ODRAWXML §2.31.3.16 p.308 |
| [`am3d:model3d`](#am3dmodel3d) ◆ | `am3d:CT_Model3D` | 2 | 13 | 0 | MS-ODRAWXML §2.31.1.1 p.297 |
| [`am3d:objViewport`](#am3dobjviewport) | `am3d:CT_ObjectViewport` | 1 | 1 | 1 | MS-ODRAWXML §2.31.3.7 p.303 |
| [`am3d:orthographic`](#am3dorthographic) | `am3d:CT_OrthographicProjection` | 0 | 2 | 1 | MS-ODRAWXML §2.31.3.8 p.303 |
| [`am3d:perspective`](#am3dperspective) | `am3d:CT_PerspectiveProjection` | 1 | 1 | 1 | MS-ODRAWXML §2.31.3.9 p.304 |
| [`am3d:pos`](#am3dpos) | `a:CT_Point3D` | 3 | 0 | 4 |  |
| [`am3d:postTrans`](#am3dposttrans) | `a:CT_Vector3D` | 3 | 0 | 1 |  |
| [`am3d:preTrans`](#am3dpretrans) | `a:CT_Vector3D` | 3 | 0 | 1 |  |
| [`am3d:ptLight`](#am3dptlight) | `am3d:CT_PointLight` | 2 | 4 | 1 | MS-ODRAWXML §2.31.3.10 p.304 |
| [`am3d:raster`](#am3draster) | `am3d:CT_Model3DRaster` | 2 | 1 | 1 | MS-ODRAWXML §2.31.3.5 p.301 |
| [`am3d:rot`](#am3drot) | `am3d:CT_Rotate3D` | 3 | 0 | 1 | MS-ODRAWXML §2.31.3.11 p.305 |
| [`am3d:scale`](#am3dscale) | `am3d:CT_Scale3D` | 0 | 3 | 1 | MS-ODRAWXML §2.31.3.12 p.306 |
| [`am3d:spPr`](#am3dsppr) | `a:CT_ShapeProperties` | 1 | 15 | 1 |  |
| [`am3d:spotLight`](#am3dspotlight) | `am3d:CT_SpotLight` | 3 | 5 | 1 | MS-ODRAWXML §2.31.3.13 p.306 |
| [`am3d:sx`](#am3dsx) | `a:CT_Ratio` | 2 | 0 | 1 |  |
| [`am3d:sy`](#am3dsy) | `a:CT_Ratio` | 2 | 0 | 1 |  |
| [`am3d:sz`](#am3dsz) | `a:CT_Ratio`, `am3d:CT_PositiveRatio` | 2 | 0 | 2 | MS-ODRAWXML §2.31.3.16 p.308 |
| [`am3d:trans`](#am3dtrans) | `am3d:CT_Model3DTransform` | 0 | 6 | 1 | MS-ODRAWXML §2.31.3.6 p.302 |
| [`am3d:unkLight`](#am3dunklight) | `am3d:CT_UnknownLight` | 0 | 0 | 1 | MS-ODRAWXML §2.31.3.14 p.307 |
| [`am3d:up`](#am3dup) | `a:CT_Vector3D` | 3 | 0 | 1 |  |
| [`am3d:winViewport`](#am3dwinviewport) | `am3d:CT_WindowViewport` | 0 | 1 | 1 | MS-ODRAWXML §2.31.3.15 p.308 |

◆ declared globally (can be the root element of a part)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `am3d:ST_AngularRadius` | restriction of a:ST_Angle | minInclusive `0`, maxInclusive `5400000` | MS-ODRAWXML §2.31.4.1 p.309 |

## Element details

### am3d:ambientLight

- Type: `am3d:CT_AmbientLight`
- Attributes: `enabled`
- Children: `am3d:clr`, `am3d:illuminance`, `am3d:extLst`
- Parents: `am3d:model3d`
- Specification: MS-ODRAWXML §2.31.3.1 p.298

### am3d:attrSrcUrl

- Type: `a1611:CT_PictureAttributionSourceURL`
- Attributes: `r:id`!
- Parents: `am3d:model3d`
- Specification: MS-ODRAWXML §2.27.3.1 p.269

### am3d:blip

- Type: `a:CT_Blip`
- Attributes: `cstate`, `r:embed`, `r:link`
- Children: `a:alphaBiLevel`, `a:alphaCeiling`, `a:alphaFloor`, `a:alphaInv`, `a:alphaMod`, `a:alphaModFix`, `a:alphaRepl`, `a:biLevel`, `a:blur`, `a:clrChange`, `a:clrRepl`, `a:duotone`, `a:fillOverlay`, `a:grayscl`, `a:hsl`, `a:lum`, `a:tint`, `a:extLst`
- Parents: `am3d:raster`

### am3d:camera

- Type: `am3d:CT_Model3DCamera`
- Children: `am3d:pos`, `am3d:up`, `am3d:lookAt`, `am3d:orthographic`, `am3d:perspective`, `am3d:extLst`
- Parents: `am3d:model3d`
- Specification: MS-ODRAWXML §2.31.3.4 p.301

### am3d:clr

- Type: `a:CT_Color`
- Children: `a:scrgbClr`, `a:srgbClr`, `a:hslClr`, `a:sysClr`, `a:schemeClr`, `a:prstClr`
- Parents: `am3d:ambientLight`, `am3d:dirLight`, `am3d:ptLight`, `am3d:spotLight`

### am3d:dirLight

- Type: `am3d:CT_DirectionalLight`
- Attributes: `angularRad`!, `enabled`
- Children: `am3d:clr`, `am3d:illuminance`, `am3d:pos`, `am3d:lookAt`, `am3d:extLst`
- Parents: `am3d:model3d`
- Specification: MS-ODRAWXML §2.31.3.2 p.298

### am3d:extLst

- Type: `a:CT_OfficeArtExtensionList`
- Children: `a:ext`
- Parents: `am3d:ambientLight`, `am3d:camera`, `am3d:dirLight`, `am3d:model3d`, `am3d:objViewport`, `am3d:orthographic`, `am3d:perspective`, `am3d:ptLight`, `am3d:spotLight`, `am3d:trans`, `am3d:winViewport`

### am3d:illuminance

- Type: `am3d:CT_PositiveRatio`
- Attributes: `d`!, `n`!
- Parents: `am3d:ambientLight`, `am3d:dirLight`
- Specification: MS-ODRAWXML §2.31.3.16 p.308

### am3d:intensity

- Type: `am3d:CT_PositiveRatio`
- Attributes: `d`!, `n`!
- Parents: `am3d:ptLight`, `am3d:spotLight`
- Specification: MS-ODRAWXML §2.31.3.16 p.308

### am3d:lookAt

- Type: `a:CT_Point3D`
- Attributes: `x`!, `y`!, `z`!
- Parents: `am3d:camera`, `am3d:dirLight`, `am3d:spotLight`

### am3d:meterPerModelUnit

- Type: `am3d:CT_PositiveRatio`
- Attributes: `d`!, `n`!
- Parents: `am3d:trans`
- Specification: MS-ODRAWXML §2.31.3.16 p.308

### am3d:model3d

- Type: `am3d:CT_Model3D`
- Attributes: `r:embed`, `r:link`
- Children: `am3d:spPr`, `am3d:camera`, `am3d:trans`, `am3d:attrSrcUrl`, `am3d:raster`, `am3d:extLst`, `am3d:objViewport`, `am3d:winViewport`, `am3d:ambientLight`, `am3d:ptLight`, `am3d:spotLight`, `am3d:dirLight`, `am3d:unkLight`
- Specification: MS-ODRAWXML §2.31.1.1 p.297

### am3d:objViewport

- Type: `am3d:CT_ObjectViewport`
- Attributes: `viewportSz`!
- Children: `am3d:extLst`
- Parents: `am3d:model3d`
- Specification: MS-ODRAWXML §2.31.3.7 p.303

### am3d:orthographic

- Type: `am3d:CT_OrthographicProjection`
- Children: `am3d:sz`, `am3d:extLst`
- Parents: `am3d:camera`
- Specification: MS-ODRAWXML §2.31.3.8 p.303

### am3d:perspective

- Type: `am3d:CT_PerspectiveProjection`
- Attributes: `fov`!
- Children: `am3d:extLst`
- Parents: `am3d:camera`
- Specification: MS-ODRAWXML §2.31.3.9 p.304

### am3d:pos

- Type: `a:CT_Point3D`
- Attributes: `x`!, `y`!, `z`!
- Parents: `am3d:camera`, `am3d:dirLight`, `am3d:ptLight`, `am3d:spotLight`

### am3d:postTrans

- Type: `a:CT_Vector3D`
- Attributes: `dx`!, `dy`!, `dz`!
- Parents: `am3d:trans`

### am3d:preTrans

- Type: `a:CT_Vector3D`
- Attributes: `dx`!, `dy`!, `dz`!
- Parents: `am3d:trans`

### am3d:ptLight

- Type: `am3d:CT_PointLight`
- Attributes: `enabled`, `rad`!
- Children: `am3d:clr`, `am3d:intensity`, `am3d:pos`, `am3d:extLst`
- Parents: `am3d:model3d`
- Specification: MS-ODRAWXML §2.31.3.10 p.304

### am3d:raster

- Type: `am3d:CT_Model3DRaster`
- Attributes: `rName`!, `rVer`!
- Children: `am3d:blip`
- Parents: `am3d:model3d`
- Specification: MS-ODRAWXML §2.31.3.5 p.301

### am3d:rot

- Type: `am3d:CT_Rotate3D`
- Attributes: `ax`, `ay`, `az`
- Parents: `am3d:trans`
- Specification: MS-ODRAWXML §2.31.3.11 p.305

### am3d:scale

- Type: `am3d:CT_Scale3D`
- Children: `am3d:sx`, `am3d:sy`, `am3d:sz`
- Parents: `am3d:trans`
- Specification: MS-ODRAWXML §2.31.3.12 p.306

### am3d:spPr

- Type: `a:CT_ShapeProperties`
- Attributes: `bwMode`
- Children: `a:xfrm`, `a:custGeom`, `a:prstGeom`, `a:noFill`, `a:solidFill`, `a:gradFill`, `a:blipFill`, `a:pattFill`, `a:grpFill`, `a:ln`, `a:effectLst`, `a:effectDag`, `a:scene3d`, `a:sp3d`, `a:extLst`
- Parents: `am3d:model3d`

### am3d:spotLight

- Type: `am3d:CT_SpotLight`
- Attributes: `enabled`, `rad`!, `spotAng`!
- Children: `am3d:clr`, `am3d:intensity`, `am3d:pos`, `am3d:lookAt`, `am3d:extLst`
- Parents: `am3d:model3d`
- Specification: MS-ODRAWXML §2.31.3.13 p.306

### am3d:sx

- Type: `a:CT_Ratio`
- Attributes: `d`!, `n`!
- Parents: `am3d:scale`

### am3d:sy

- Type: `a:CT_Ratio`
- Attributes: `d`!, `n`!
- Parents: `am3d:scale`

### am3d:sz

- Type: `a:CT_Ratio`, `am3d:CT_PositiveRatio`
- Attributes: `d`!, `n`!
- Parents: `am3d:orthographic`, `am3d:scale`
- Specification: MS-ODRAWXML §2.31.3.16 p.308

### am3d:trans

- Type: `am3d:CT_Model3DTransform`
- Children: `am3d:meterPerModelUnit`, `am3d:preTrans`, `am3d:scale`, `am3d:rot`, `am3d:postTrans`, `am3d:extLst`
- Parents: `am3d:model3d`
- Specification: MS-ODRAWXML §2.31.3.6 p.302

### am3d:unkLight

- Type: `am3d:CT_UnknownLight`
- Parents: `am3d:model3d`
- Specification: MS-ODRAWXML §2.31.3.14 p.307

### am3d:up

- Type: `a:CT_Vector3D`
- Attributes: `dx`!, `dy`!, `dz`!
- Parents: `am3d:camera`

### am3d:winViewport

- Type: `am3d:CT_WindowViewport`
- Children: `am3d:extLst`
- Parents: `am3d:model3d`
- Specification: MS-ODRAWXML §2.31.3.15 p.308

`!` marks required attributes.
