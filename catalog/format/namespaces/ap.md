# ap: Packages and document properties

`http://schemas.openxmlformats.org/officeDocument/2006/extended-properties`

- Source: ECMA-376-4 transitional schema shared-documentPropertiesExtended.xsd
- Office version: 2007
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 28 | 0 | 0 | 4 | 0 | 0 | 28 | 7 |

Full detail (content models, attribute types, defaults): [ap.json](ap.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`ap:AppVersion`](#apappversion) | `xsd:string` | 0 | 0 | 1 | §22.2.2.2 p.3736, OI 2.1.1717 |
| [`ap:Application`](#apapplication) | `xsd:string` | 0 | 0 | 1 | §22.2.2.1 p.3736 |
| [`ap:Characters`](#apcharacters) | `xsd:int` | 0 | 0 | 1 | §22.2.2.3 p.3736 |
| [`ap:CharactersWithSpaces`](#apcharacterswithspaces) | `xsd:int` | 0 | 0 | 1 | §22.2.2.4 p.3737 |
| [`ap:Company`](#apcompany) | `xsd:string` | 0 | 0 | 1 | §22.2.2.5 p.3737 |
| [`ap:DigSig`](#apdigsig) | `ap:CT_DigSigBlob` | 0 | 1 | 1 | §22.2.2.6 p.3737, OI 2.1.1718 |
| [`ap:DocSecurity`](#apdocsecurity) | `xsd:int` | 0 | 0 | 1 | §22.2.2.7 p.3737, OI 2.1.1719 |
| [`ap:HLinks`](#aphlinks) | `ap:CT_VectorVariant` | 0 | 1 | 1 | §22.2.2.10 p.3738, OI 2.1.1721 |
| [`ap:HeadingPairs`](#apheadingpairs) | `ap:CT_VectorVariant` | 0 | 1 | 1 | §22.2.2.8 p.3737, OI 2.1.1720 |
| [`ap:HiddenSlides`](#aphiddenslides) | `xsd:int` | 0 | 0 | 1 | §22.2.2.9 p.3738 |
| [`ap:HyperlinkBase`](#aphyperlinkbase) | `xsd:string` | 0 | 0 | 1 | §22.2.2.11 p.3738 |
| [`ap:HyperlinksChanged`](#aphyperlinkschanged) | `xsd:boolean` | 0 | 0 | 1 | §22.2.2.12 p.3738 |
| [`ap:Lines`](#aplines) | `xsd:int` | 0 | 0 | 1 | §22.2.2.13 p.3739 |
| [`ap:LinksUpToDate`](#aplinksuptodate) | `xsd:boolean` | 0 | 0 | 1 | §22.2.2.14 p.3739 |
| [`ap:MMClips`](#apmmclips) | `xsd:int` | 0 | 0 | 1 | §22.2.2.16 p.3739 |
| [`ap:Manager`](#apmanager) | `xsd:string` | 0 | 0 | 1 | §22.2.2.15 p.3739 |
| [`ap:Notes`](#apnotes) | `xsd:int` | 0 | 0 | 1 | §22.2.2.17 p.3739 |
| [`ap:Pages`](#appages) | `xsd:int` | 0 | 0 | 1 | §22.2.2.18 p.3739 |
| [`ap:Paragraphs`](#apparagraphs) | `xsd:int` | 0 | 0 | 1 | §22.2.2.19 p.3739 |
| [`ap:PresentationFormat`](#appresentationformat) | `xsd:string` | 0 | 0 | 1 | §22.2.2.20 p.3739 |
| [`ap:Properties`](#approperties) ◆ | `ap:CT_Properties` | 0 | 27 | 0 | §22.2.2.21 p.3740 |
| [`ap:ScaleCrop`](#apscalecrop) | `xsd:boolean` | 0 | 0 | 1 | §22.2.2.22 p.3740 |
| [`ap:SharedDoc`](#apshareddoc) | `xsd:boolean` | 0 | 0 | 1 | §22.2.2.23 p.3740 |
| [`ap:Slides`](#apslides) | `xsd:int` | 0 | 0 | 1 | §22.2.2.24 p.3740 |
| [`ap:Template`](#aptemplate) | `xsd:string` | 0 | 0 | 1 | §22.2.2.25 p.3740 |
| [`ap:TitlesOfParts`](#aptitlesofparts) | `ap:CT_VectorLpstr` | 0 | 1 | 1 | §22.2.2.26 p.3740, OI 2.1.1722 |
| [`ap:TotalTime`](#aptotaltime) | `xsd:int` | 0 | 0 | 1 | §22.2.2.27 p.3741, OI 2.1.1723 |
| [`ap:Words`](#apwords) | `xsd:int` | 0 | 0 | 1 | §22.2.2.28 p.3741 |

◆ declared globally (can be the root element of a part)

## Element details

### ap:AppVersion

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `ap:Properties`
- Specification: §22.2.2.2 p.3736, OI 2.1.1717

### ap:Application

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `ap:Properties`
- Specification: §22.2.2.1 p.3736

### ap:Characters

- Type: `xsd:int` (text: `xsd:int`)
- Parents: `ap:Properties`
- Specification: §22.2.2.3 p.3736

### ap:CharactersWithSpaces

- Type: `xsd:int` (text: `xsd:int`)
- Parents: `ap:Properties`
- Specification: §22.2.2.4 p.3737

### ap:Company

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `ap:Properties`
- Specification: §22.2.2.5 p.3737

### ap:DigSig

- Type: `ap:CT_DigSigBlob`
- Children: `vt:blob`
- Parents: `ap:Properties`
- Specification: §22.2.2.6 p.3737, OI 2.1.1718

### ap:DocSecurity

- Type: `xsd:int` (text: `xsd:int`)
- Parents: `ap:Properties`
- Specification: §22.2.2.7 p.3737, OI 2.1.1719

### ap:HLinks

- Type: `ap:CT_VectorVariant`
- Children: `vt:vector`
- Parents: `ap:Properties`
- Specification: §22.2.2.10 p.3738, OI 2.1.1721

### ap:HeadingPairs

- Type: `ap:CT_VectorVariant`
- Children: `vt:vector`
- Parents: `ap:Properties`
- Specification: §22.2.2.8 p.3737, OI 2.1.1720

### ap:HiddenSlides

- Type: `xsd:int` (text: `xsd:int`)
- Parents: `ap:Properties`
- Specification: §22.2.2.9 p.3738

### ap:HyperlinkBase

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `ap:Properties`
- Specification: §22.2.2.11 p.3738

### ap:HyperlinksChanged

- Type: `xsd:boolean` (text: `xsd:boolean`)
- Parents: `ap:Properties`
- Specification: §22.2.2.12 p.3738

### ap:Lines

- Type: `xsd:int` (text: `xsd:int`)
- Parents: `ap:Properties`
- Specification: §22.2.2.13 p.3739

### ap:LinksUpToDate

- Type: `xsd:boolean` (text: `xsd:boolean`)
- Parents: `ap:Properties`
- Specification: §22.2.2.14 p.3739

### ap:MMClips

- Type: `xsd:int` (text: `xsd:int`)
- Parents: `ap:Properties`
- Specification: §22.2.2.16 p.3739

### ap:Manager

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `ap:Properties`
- Specification: §22.2.2.15 p.3739

### ap:Notes

- Type: `xsd:int` (text: `xsd:int`)
- Parents: `ap:Properties`
- Specification: §22.2.2.17 p.3739

### ap:Pages

- Type: `xsd:int` (text: `xsd:int`)
- Parents: `ap:Properties`
- Specification: §22.2.2.18 p.3739

### ap:Paragraphs

- Type: `xsd:int` (text: `xsd:int`)
- Parents: `ap:Properties`
- Specification: §22.2.2.19 p.3739

### ap:PresentationFormat

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `ap:Properties`
- Specification: §22.2.2.20 p.3739

### ap:Properties

- Type: `ap:CT_Properties`
- Children: `ap:Template`, `ap:Manager`, `ap:Company`, `ap:Pages`, `ap:Words`, `ap:Characters`, `ap:PresentationFormat`, `ap:Lines`, `ap:Paragraphs`, `ap:Slides`, `ap:Notes`, `ap:TotalTime`, `ap:HiddenSlides`, `ap:MMClips`, `ap:ScaleCrop`, `ap:HeadingPairs`, `ap:TitlesOfParts`, `ap:LinksUpToDate`, `ap:CharactersWithSpaces`, `ap:SharedDoc`, `ap:HyperlinkBase`, `ap:HLinks`, `ap:HyperlinksChanged`, `ap:DigSig`, `ap:Application`, `ap:AppVersion`, `ap:DocSecurity`
- Specification: §22.2.2.21 p.3740

### ap:ScaleCrop

- Type: `xsd:boolean` (text: `xsd:boolean`)
- Parents: `ap:Properties`
- Specification: §22.2.2.22 p.3740

### ap:SharedDoc

- Type: `xsd:boolean` (text: `xsd:boolean`)
- Parents: `ap:Properties`
- Specification: §22.2.2.23 p.3740

### ap:Slides

- Type: `xsd:int` (text: `xsd:int`)
- Parents: `ap:Properties`
- Specification: §22.2.2.24 p.3740

### ap:Template

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `ap:Properties`
- Specification: §22.2.2.25 p.3740

### ap:TitlesOfParts

- Type: `ap:CT_VectorLpstr`
- Children: `vt:vector`
- Parents: `ap:Properties`
- Specification: §22.2.2.26 p.3740, OI 2.1.1722

### ap:TotalTime

- Type: `xsd:int` (text: `xsd:int`)
- Parents: `ap:Properties`
- Specification: §22.2.2.27 p.3741, OI 2.1.1723

### ap:Words

- Type: `xsd:int` (text: `xsd:int`)
- Parents: `ap:Properties`
- Specification: §22.2.2.28 p.3741

`!` marks required attributes.
