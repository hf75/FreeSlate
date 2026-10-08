# xvml: SpreadsheetML (out of scope)

`urn:schemas-microsoft-com:office:excel`

- Source: ECMA-376-4 transitional schema vml-spreadsheetDrawing.xsd
- Office version: 2007
- In scope: no

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 68 | 1 | 0 | 1 | 2 | 19 | 70 | 37 |

Full detail (content models, attribute types, defaults): [xvml.json](xvml.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`xvml:Accel`](#xvmlaccel) | `xsd:integer` | 0 | 0 | 1 | Part 4 §19.4.2.1 p.848, OI 2.1.1855 |
| [`xvml:Accel2`](#xvmlaccel2) | `xsd:integer` | 0 | 0 | 1 | Part 4 §19.4.2.2 p.848, OI 2.1.1856 |
| [`xvml:Anchor`](#xvmlanchor) | `xsd:string` | 0 | 0 | 1 | Part 4 §19.4.2.3 p.848, OI 2.1.1857 |
| [`xvml:AutoFill`](#xvmlautofill) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.4 p.849, OI 2.1.1858 |
| [`xvml:AutoLine`](#xvmlautoline) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.5 p.850, OI 2.1.1859 |
| [`xvml:AutoPict`](#xvmlautopict) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.6 p.850 |
| [`xvml:AutoScale`](#xvmlautoscale) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.7 p.851, OI 2.1.1860 |
| [`xvml:CF`](#xvmlcf) | `xvml:ST_CF` | 0 | 0 | 1 | Part 4 §19.4.2.10 p.852, OI 2.1.1862 |
| [`xvml:Camera`](#xvmlcamera) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.8 p.851, OI 2.1.1861 |
| [`xvml:Cancel`](#xvmlcancel) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.9 p.852 |
| [`xvml:Checked`](#xvmlchecked) | `xsd:integer` | 0 | 0 | 1 | Part 4 §19.4.2.11 p.852 |
| [`xvml:ClientData`](#xvmlclientdata) ◆ | `xvml:CT_ClientData` | 1 | 67 | 11 | Part 4 §19.4.2.12 p.853, OI 2.1.1863 |
| [`xvml:ColHidden`](#xvmlcolhidden) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.13 p.854, OI 2.1.1864 |
| [`xvml:Colored`](#xvmlcolored) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.14 p.854, OI 2.1.1865 |
| [`xvml:Column`](#xvmlcolumn) | `xsd:integer` | 0 | 0 | 1 | Part 4 §19.4.2.15 p.855 |
| [`xvml:DDE`](#xvmldde) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.16 p.855, OI 2.1.1866 |
| [`xvml:Default`](#xvmldefault) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.17 p.855 |
| [`xvml:DefaultSize`](#xvmldefaultsize) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.18 p.856, OI 2.1.1867 |
| [`xvml:Disabled`](#xvmldisabled) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.19 p.856 |
| [`xvml:Dismiss`](#xvmldismiss) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.20 p.857 |
| [`xvml:DropLines`](#xvmldroplines) | `xsd:integer` | 0 | 0 | 1 | Part 4 §19.4.2.21 p.857, OI 2.1.1868 |
| [`xvml:DropStyle`](#xvmldropstyle) | `xsd:string` | 0 | 0 | 1 | Part 4 §19.4.2.22 p.857, OI 2.1.1869 |
| [`xvml:Dx`](#xvmldx) | `xsd:integer` | 0 | 0 | 1 | Part 4 §19.4.2.23 p.858, OI 2.1.1870 |
| [`xvml:FirstButton`](#xvmlfirstbutton) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.24 p.858 |
| [`xvml:FmlaGroup`](#xvmlfmlagroup) | `xsd:string` | 0 | 0 | 1 | Part 4 §19.4.2.25 p.858, OI 2.1.1871 |
| [`xvml:FmlaLink`](#xvmlfmlalink) | `xsd:string` | 0 | 0 | 1 | Part 4 §19.4.2.26 p.859 |
| [`xvml:FmlaMacro`](#xvmlfmlamacro) | `xsd:string` | 0 | 0 | 1 | Part 4 §19.4.2.27 p.859, OI 2.1.1872 |
| [`xvml:FmlaPict`](#xvmlfmlapict) | `xsd:string` | 0 | 0 | 1 | Part 4 §19.4.2.28 p.860 |
| [`xvml:FmlaRange`](#xvmlfmlarange) | `xsd:string` | 0 | 0 | 1 | Part 4 §19.4.2.29 p.860, OI 2.1.1873 |
| [`xvml:FmlaTxbx`](#xvmlfmlatxbx) | `xsd:string` | 0 | 0 | 1 | Part 4 §19.4.2.30 p.860, OI 2.1.1874 |
| [`xvml:Help`](#xvmlhelp) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.31 p.860 |
| [`xvml:Horiz`](#xvmlhoriz) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.32 p.861, OI 2.1.1875 |
| [`xvml:Inc`](#xvmlinc) | `xsd:integer` | 0 | 0 | 1 | Part 4 §19.4.2.33 p.861, OI 2.1.1876 |
| [`xvml:JustLastX`](#xvmljustlastx) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.34 p.861 |
| [`xvml:LCT`](#xvmllct) | `xsd:string` | 0 | 0 | 1 | Part 4 §19.4.2.35 p.862, OI 2.1.1877 |
| [`xvml:ListItem`](#xvmllistitem) | `xsd:string` | 0 | 0 | 1 | Part 4 §19.4.2.36 p.862, OI 2.1.1878 |
| [`xvml:LockText`](#xvmllocktext) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.38 p.863 |
| [`xvml:Locked`](#xvmllocked) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.37 p.862 |
| [`xvml:MapOCX`](#xvmlmapocx) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.39 p.863 |
| [`xvml:Max`](#xvmlmax) | `xsd:integer` | 0 | 0 | 1 | Part 4 §19.4.2.40 p.864, OI 2.1.1879 |
| [`xvml:Min`](#xvmlmin) | `xsd:integer` | 0 | 0 | 1 | Part 4 §19.4.2.41 p.864, OI 2.1.1880 |
| [`xvml:MoveWithCells`](#xvmlmovewithcells) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.42 p.864, OI 2.1.1881 |
| [`xvml:MultiLine`](#xvmlmultiline) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.43 p.865 |
| [`xvml:MultiSel`](#xvmlmultisel) | `xsd:string` | 0 | 0 | 1 | Part 4 §19.4.2.44 p.865 |
| [`xvml:NoThreeD`](#xvmlnothreed) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.45 p.865, OI 2.1.1882 |
| [`xvml:NoThreeD2`](#xvmlnothreed2) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.46 p.866 |
| [`xvml:Page`](#xvmlpage) | `xsd:integer` | 0 | 0 | 1 | Part 4 §19.4.2.47 p.866, OI 2.1.1883 |
| [`xvml:PrintObject`](#xvmlprintobject) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.48 p.866 |
| [`xvml:RecalcAlways`](#xvmlrecalcalways) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.49 p.867, OI 2.1.1884 |
| [`xvml:Row`](#xvmlrow) | `xsd:integer` | 0 | 0 | 1 | Part 4 §19.4.2.50 p.867 |
| [`xvml:RowHidden`](#xvmlrowhidden) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.51 p.867, OI 2.1.1885 |
| [`xvml:ScriptExtended`](#xvmlscriptextended) | `xsd:string` | 0 | 0 | 1 | Part 4 §19.4.2.52 p.868 |
| [`xvml:ScriptLanguage`](#xvmlscriptlanguage) | `xsd:nonNegativeInteger` | 0 | 0 | 1 | Part 4 §19.4.2.53 p.868 |
| [`xvml:ScriptLocation`](#xvmlscriptlocation) | `xsd:nonNegativeInteger` | 0 | 0 | 1 | Part 4 §19.4.2.54 p.869 |
| [`xvml:ScriptText`](#xvmlscripttext) | `xsd:string` | 0 | 0 | 1 | Part 4 §19.4.2.55 p.869 |
| [`xvml:SecretEdit`](#xvmlsecretedit) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.56 p.869 |
| [`xvml:Sel`](#xvmlsel) | `xsd:integer` | 0 | 0 | 1 | Part 4 §19.4.2.57 p.870 |
| [`xvml:SelType`](#xvmlseltype) | `xsd:string` | 0 | 0 | 1 | Part 4 §19.4.2.58 p.870 |
| [`xvml:SizeWithCells`](#xvmlsizewithcells) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.59 p.870, OI 2.1.1886 |
| [`xvml:TextHAlign`](#xvmltexthalign) | `xsd:string` | 0 | 0 | 1 | Part 4 §19.4.2.60 p.871 |
| [`xvml:TextVAlign`](#xvmltextvalign) | `xsd:string` | 0 | 0 | 1 | Part 4 §19.4.2.61 p.871, OI 2.1.1887 |
| [`xvml:UIObj`](#xvmluiobj) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.62 p.871, OI 2.1.1888 |
| [`xvml:VScroll`](#xvmlvscroll) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.66 p.873 |
| [`xvml:VTEdit`](#xvmlvtedit) | `xsd:integer` | 0 | 0 | 1 | Part 4 §19.4.2.67 p.873 |
| [`xvml:Val`](#xvmlval) | `xsd:integer` | 0 | 0 | 1 | Part 4 §19.4.2.63 p.872, OI 2.1.1889 |
| [`xvml:ValidIds`](#xvmlvalidids) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.64 p.872, OI 2.1.1890 |
| [`xvml:Visible`](#xvmlvisible) | `s:ST_TrueFalseBlank` | 0 | 0 | 1 | Part 4 §19.4.2.65 p.872 |
| [`xvml:WidthMin`](#xvmlwidthmin) | `xsd:integer` | 0 | 0 | 1 | Part 4 §19.4.2.68 p.874, OI 2.1.1891 |

◆ declared globally (can be the root element of a part)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `xvml:ST_CF` | restriction of xsd:string |  | Part 4 §19.4.3.1 p.874 |
| `xvml:ST_ObjectType` | restriction of xsd:string | `Button`, `Checkbox`, `Dialog`, `Drop`, `Edit`, `GBox`, `Label`, `LineA`, `List`, `Movie`, `Note`, `Pict` … (+7) | Part 4 §19.4.3.2 p.875 |

## Element details

### xvml:Accel

- Type: `xsd:integer` (text: `xsd:integer`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.1 p.848, OI 2.1.1855

### xvml:Accel2

- Type: `xsd:integer` (text: `xsd:integer`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.2 p.848, OI 2.1.1856

### xvml:Anchor

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.3 p.848, OI 2.1.1857

### xvml:AutoFill

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.4 p.849, OI 2.1.1858

### xvml:AutoLine

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.5 p.850, OI 2.1.1859

### xvml:AutoPict

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.6 p.850

### xvml:AutoScale

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.7 p.851, OI 2.1.1860

### xvml:CF

- Type: `xvml:ST_CF` (text: `xvml:ST_CF`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.10 p.852, OI 2.1.1862

### xvml:Camera

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.8 p.851, OI 2.1.1861

### xvml:Cancel

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.9 p.852

### xvml:Checked

- Type: `xsd:integer` (text: `xsd:integer`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.11 p.852

### xvml:ClientData

- Type: `xvml:CT_ClientData`
- Attributes: `ObjectType`!
- Children: `xvml:MoveWithCells`, `xvml:SizeWithCells`, `xvml:Anchor`, `xvml:Locked`, `xvml:DefaultSize`, `xvml:PrintObject`, `xvml:Disabled`, `xvml:AutoFill`, `xvml:AutoLine`, `xvml:AutoPict`, `xvml:FmlaMacro`, `xvml:TextHAlign`, `xvml:TextVAlign`, `xvml:LockText`, `xvml:JustLastX`, `xvml:SecretEdit`, `xvml:Default`, `xvml:Help`, `xvml:Cancel`, `xvml:Dismiss`, `xvml:Accel`, `xvml:Accel2`, `xvml:Row`, `xvml:Column`, `xvml:Visible`, `xvml:RowHidden`, `xvml:ColHidden`, `xvml:VTEdit`, `xvml:MultiLine`, `xvml:VScroll`, `xvml:ValidIds`, `xvml:FmlaRange`, `xvml:WidthMin`, `xvml:Sel`, `xvml:NoThreeD2`, `xvml:SelType`, `xvml:MultiSel`, `xvml:LCT`, `xvml:ListItem`, `xvml:DropStyle`, `xvml:Colored`, `xvml:DropLines`, `xvml:Checked`, `xvml:FmlaLink`, `xvml:FmlaPict`, `xvml:NoThreeD`, `xvml:FirstButton`, `xvml:FmlaGroup`, `xvml:Val`, `xvml:Min`, `xvml:Max`, `xvml:Inc`, `xvml:Page`, `xvml:Horiz`, `xvml:Dx`, `xvml:MapOCX`, `xvml:CF`, `xvml:Camera`, `xvml:RecalcAlways`, `xvml:AutoScale`, `xvml:DDE`, `xvml:UIObj`, `xvml:ScriptText`, `xvml:ScriptExtended`, `xvml:ScriptLanguage`, `xvml:ScriptLocation`, `xvml:FmlaTxbx`
- Parents: `v:arc`, `v:curve`, `v:group`, `v:image`, `v:line`, `v:oval`, `v:polyline`, `v:rect`, `v:roundrect`, `v:shape`, `v:shapetype`
- Specification: Part 4 §19.4.2.12 p.853, OI 2.1.1863

### xvml:ColHidden

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.13 p.854, OI 2.1.1864

### xvml:Colored

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.14 p.854, OI 2.1.1865

### xvml:Column

- Type: `xsd:integer` (text: `xsd:integer`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.15 p.855

### xvml:DDE

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.16 p.855, OI 2.1.1866

### xvml:Default

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.17 p.855

### xvml:DefaultSize

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.18 p.856, OI 2.1.1867

### xvml:Disabled

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.19 p.856

### xvml:Dismiss

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.20 p.857

### xvml:DropLines

- Type: `xsd:integer` (text: `xsd:integer`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.21 p.857, OI 2.1.1868

### xvml:DropStyle

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.22 p.857, OI 2.1.1869

### xvml:Dx

- Type: `xsd:integer` (text: `xsd:integer`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.23 p.858, OI 2.1.1870

### xvml:FirstButton

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.24 p.858

### xvml:FmlaGroup

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.25 p.858, OI 2.1.1871

### xvml:FmlaLink

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.26 p.859

### xvml:FmlaMacro

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.27 p.859, OI 2.1.1872

### xvml:FmlaPict

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.28 p.860

### xvml:FmlaRange

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.29 p.860, OI 2.1.1873

### xvml:FmlaTxbx

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.30 p.860, OI 2.1.1874

### xvml:Help

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.31 p.860

### xvml:Horiz

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.32 p.861, OI 2.1.1875

### xvml:Inc

- Type: `xsd:integer` (text: `xsd:integer`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.33 p.861, OI 2.1.1876

### xvml:JustLastX

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.34 p.861

### xvml:LCT

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.35 p.862, OI 2.1.1877

### xvml:ListItem

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.36 p.862, OI 2.1.1878

### xvml:LockText

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.38 p.863

### xvml:Locked

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.37 p.862

### xvml:MapOCX

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.39 p.863

### xvml:Max

- Type: `xsd:integer` (text: `xsd:integer`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.40 p.864, OI 2.1.1879

### xvml:Min

- Type: `xsd:integer` (text: `xsd:integer`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.41 p.864, OI 2.1.1880

### xvml:MoveWithCells

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.42 p.864, OI 2.1.1881

### xvml:MultiLine

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.43 p.865

### xvml:MultiSel

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.44 p.865

### xvml:NoThreeD

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.45 p.865, OI 2.1.1882

### xvml:NoThreeD2

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.46 p.866

### xvml:Page

- Type: `xsd:integer` (text: `xsd:integer`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.47 p.866, OI 2.1.1883

### xvml:PrintObject

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.48 p.866

### xvml:RecalcAlways

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.49 p.867, OI 2.1.1884

### xvml:Row

- Type: `xsd:integer` (text: `xsd:integer`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.50 p.867

### xvml:RowHidden

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.51 p.867, OI 2.1.1885

### xvml:ScriptExtended

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.52 p.868

### xvml:ScriptLanguage

- Type: `xsd:nonNegativeInteger` (text: `xsd:nonNegativeInteger`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.53 p.868

### xvml:ScriptLocation

- Type: `xsd:nonNegativeInteger` (text: `xsd:nonNegativeInteger`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.54 p.869

### xvml:ScriptText

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.55 p.869

### xvml:SecretEdit

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.56 p.869

### xvml:Sel

- Type: `xsd:integer` (text: `xsd:integer`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.57 p.870

### xvml:SelType

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.58 p.870

### xvml:SizeWithCells

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.59 p.870, OI 2.1.1886

### xvml:TextHAlign

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.60 p.871

### xvml:TextVAlign

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.61 p.871, OI 2.1.1887

### xvml:UIObj

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.62 p.871, OI 2.1.1888

### xvml:VScroll

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.66 p.873

### xvml:VTEdit

- Type: `xsd:integer` (text: `xsd:integer`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.67 p.873

### xvml:Val

- Type: `xsd:integer` (text: `xsd:integer`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.63 p.872, OI 2.1.1889

### xvml:ValidIds

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.64 p.872, OI 2.1.1890

### xvml:Visible

- Type: `s:ST_TrueFalseBlank` (text: `s:ST_TrueFalseBlank`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.65 p.872

### xvml:WidthMin

- Type: `xsd:integer` (text: `xsd:integer`)
- Parents: `xvml:ClientData`
- Specification: Part 4 §19.4.2.68 p.874, OI 2.1.1891

`!` marks required attributes.
