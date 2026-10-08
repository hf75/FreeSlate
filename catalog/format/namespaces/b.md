# b: Packages and document properties

`http://schemas.openxmlformats.org/officeDocument/2006/bibliography`

- Source: ECMA-376-4 transitional schema shared-bibliography.xsd
- Office version: 2007
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 75 | 3 | 0 | 7 | 1 | 17 | 77 | 7 |

Full detail (content models, attribute types, defaults): [b.json](b.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`b:AbbreviatedCaseNumber`](#babbreviatedcasenumber) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.1 p.3759 |
| [`b:AlbumTitle`](#balbumtitle) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.2 p.3760 |
| [`b:Artist`](#bartist) | `b:CT_NameType` | 0 | 1 | 1 | §22.6.2.3 p.3760 |
| [`b:Author`](#bauthor) | `b:CT_NameOrCorporateType`, `b:CT_AuthorType` | 0 | 18 | 2 | §22.6.2.4 p.3761, OI 2.1.1734; §22.6.2.5 p.3762 |
| [`b:BookAuthor`](#bbookauthor) | `b:CT_NameType` | 0 | 1 | 1 | §22.6.2.6 p.3763 |
| [`b:BookTitle`](#bbooktitle) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.7 p.3763 |
| [`b:BroadcastTitle`](#bbroadcasttitle) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.9 p.3764 |
| [`b:Broadcaster`](#bbroadcaster) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.8 p.3764 |
| [`b:CaseNumber`](#bcasenumber) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.10 p.3765 |
| [`b:ChapterNumber`](#bchapternumber) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.11 p.3765 |
| [`b:City`](#bcity) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.12 p.3765 |
| [`b:Comments`](#bcomments) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.13 p.3766 |
| [`b:Compiler`](#bcompiler) | `b:CT_NameType` | 0 | 1 | 1 | §22.6.2.14 p.3766 |
| [`b:Composer`](#bcomposer) | `b:CT_NameType` | 0 | 1 | 1 | §22.6.2.15 p.3767 |
| [`b:Conductor`](#bconductor) | `b:CT_NameType` | 0 | 1 | 1 | §22.6.2.16 p.3767 |
| [`b:ConferenceName`](#bconferencename) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.17 p.3767 |
| [`b:Corporate`](#bcorporate) | `s:ST_String` | 0 | 0 | 2 | §22.6.2.18 p.3768 |
| [`b:Counsel`](#bcounsel) | `b:CT_NameType` | 0 | 1 | 1 | §22.6.2.19 p.3768 |
| [`b:CountryRegion`](#bcountryregion) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.20 p.3768 |
| [`b:Court`](#bcourt) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.21 p.3769 |
| [`b:Day`](#bday) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.22 p.3769 |
| [`b:DayAccessed`](#bdayaccessed) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.23 p.3770 |
| [`b:Department`](#bdepartment) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.24 p.3770 |
| [`b:Director`](#bdirector) | `b:CT_NameType` | 0 | 1 | 1 | §22.6.2.25 p.3770 |
| [`b:Distributor`](#bdistributor) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.26 p.3771 |
| [`b:Edition`](#bedition) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.27 p.3771 |
| [`b:Editor`](#beditor) | `b:CT_NameType` | 0 | 1 | 1 | §22.6.2.28 p.3771 |
| [`b:First`](#bfirst) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.29 p.3772 |
| [`b:Guid`](#bguid) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.30 p.3772, OI 2.1.1735 |
| [`b:Institution`](#binstitution) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.31 p.3773 |
| [`b:InternetSiteTitle`](#binternetsitetitle) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.32 p.3773 |
| [`b:Interviewee`](#binterviewee) | `b:CT_NameType` | 0 | 1 | 1 | §22.6.2.33 p.3774 |
| [`b:Interviewer`](#binterviewer) | `b:CT_NameType` | 0 | 1 | 1 | §22.6.2.34 p.3774 |
| [`b:Inventor`](#binventor) | `b:CT_NameType` | 0 | 1 | 1 | §22.6.2.35 p.3775 |
| [`b:Issue`](#bissue) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.36 p.3775 |
| [`b:JournalName`](#bjournalname) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.37 p.3776 |
| [`b:LCID`](#blcid) | `s:ST_Lang` | 0 | 0 | 1 | §22.6.2.39 p.3777, OI 2.1.1736 |
| [`b:Last`](#blast) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.38 p.3776 |
| [`b:Medium`](#bmedium) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.40 p.3777 |
| [`b:Middle`](#bmiddle) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.41 p.3777 |
| [`b:Month`](#bmonth) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.42 p.3778 |
| [`b:MonthAccessed`](#bmonthaccessed) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.43 p.3778 |
| [`b:NameList`](#bnamelist) | `b:CT_NameListType` | 0 | 1 | 16 | §22.6.2.44 p.3778 |
| [`b:NumberVolumes`](#bnumbervolumes) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.45 p.3779 |
| [`b:Pages`](#bpages) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.46 p.3779 |
| [`b:PatentNumber`](#bpatentnumber) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.47 p.3780 |
| [`b:Performer`](#bperformer) | `b:CT_NameOrCorporateType` | 0 | 2 | 1 | §22.6.2.48 p.3780 |
| [`b:PeriodicalTitle`](#bperiodicaltitle) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.49 p.3780 |
| [`b:Person`](#bperson) | `b:CT_PersonType` | 0 | 3 | 1 | §22.6.2.50 p.3781, OI 2.1.1737 |
| [`b:ProducerName`](#bproducername) | `b:CT_NameType` | 0 | 1 | 1 | §22.6.2.51 p.3781 |
| [`b:ProductionCompany`](#bproductioncompany) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.52 p.3782 |
| [`b:PublicationTitle`](#bpublicationtitle) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.53 p.3782 |
| [`b:Publisher`](#bpublisher) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.54 p.3782 |
| [`b:RecordingNumber`](#brecordingnumber) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.55 p.3783 |
| [`b:RefOrder`](#breforder) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.56 p.3783 |
| [`b:Reporter`](#breporter) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.57 p.3783 |
| [`b:ShortTitle`](#bshorttitle) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.58 p.3784 |
| [`b:Source`](#bsource) | `b:CT_SourceType` | 0 | 52 | 1 | §22.6.2.59 p.3784, OI 2.1.1738 |
| [`b:SourceType`](#bsourcetype) | `b:ST_SourceType` | 0 | 0 | 1 | §22.6.2.61 p.3786 |
| [`b:Sources`](#bsources) ◆ | `b:CT_Sources` | 3 | 1 | 0 | §22.6.2.60 p.3785 |
| [`b:StandardNumber`](#bstandardnumber) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.62 p.3787 |
| [`b:StateProvince`](#bstateprovince) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.63 p.3787 |
| [`b:Station`](#bstation) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.64 p.3787 |
| [`b:Tag`](#btag) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.65 p.3788, OI 2.1.1739 |
| [`b:Theater`](#btheater) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.66 p.3788 |
| [`b:ThesisType`](#bthesistype) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.67 p.3789 |
| [`b:Title`](#btitle) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.68 p.3789 |
| [`b:Translator`](#btranslator) | `b:CT_NameType` | 0 | 1 | 1 | §22.6.2.69 p.3789 |
| [`b:Type`](#btype) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.70 p.3790 |
| [`b:URL`](#burl) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.71 p.3790 |
| [`b:Version`](#bversion) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.72 p.3791 |
| [`b:Volume`](#bvolume) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.73 p.3791 |
| [`b:Writer`](#bwriter) | `b:CT_NameType` | 0 | 1 | 1 | §22.6.2.74 p.3792 |
| [`b:Year`](#byear) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.75 p.3792 |
| [`b:YearAccessed`](#byearaccessed) | `s:ST_String` | 0 | 0 | 1 | §22.6.2.76 p.3792, OI 2.1.1740 |

◆ declared globally (can be the root element of a part)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `b:ST_SourceType` | restriction of s:ST_String | `ArticleInAPeriodical`, `Book`, `BookSection`, `JournalArticle`, `ConferenceProceedings`, `Report`, `SoundRecording`, `Performance`, `Art`, `DocumentFromInternetSite`, `InternetSite`, `Film` … (+5) | §22.6.3.1 p.3793 |

## Element details

### b:AbbreviatedCaseNumber

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.1 p.3759

### b:AlbumTitle

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.2 p.3760

### b:Artist

- Type: `b:CT_NameType`
- Children: `b:NameList`
- Parents: `b:Author`
- Specification: §22.6.2.3 p.3760

### b:Author

- Type: `b:CT_NameOrCorporateType`, `b:CT_AuthorType`
- Children: `b:NameList`, `b:Corporate`, `b:Artist`, `b:Author`, `b:BookAuthor`, `b:Compiler`, `b:Composer`, `b:Conductor`, `b:Counsel`, `b:Director`, `b:Editor`, `b:Interviewee`, `b:Interviewer`, `b:Inventor`, `b:Performer`, `b:ProducerName`, `b:Translator`, `b:Writer`
- Parents: `b:Author`, `b:Source`
- Specification: §22.6.2.4 p.3761, OI 2.1.1734; §22.6.2.5 p.3762

### b:BookAuthor

- Type: `b:CT_NameType`
- Children: `b:NameList`
- Parents: `b:Author`
- Specification: §22.6.2.6 p.3763

### b:BookTitle

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.7 p.3763

### b:BroadcastTitle

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.9 p.3764

### b:Broadcaster

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.8 p.3764

### b:CaseNumber

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.10 p.3765

### b:ChapterNumber

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.11 p.3765

### b:City

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.12 p.3765

### b:Comments

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.13 p.3766

### b:Compiler

- Type: `b:CT_NameType`
- Children: `b:NameList`
- Parents: `b:Author`
- Specification: §22.6.2.14 p.3766

### b:Composer

- Type: `b:CT_NameType`
- Children: `b:NameList`
- Parents: `b:Author`
- Specification: §22.6.2.15 p.3767

### b:Conductor

- Type: `b:CT_NameType`
- Children: `b:NameList`
- Parents: `b:Author`
- Specification: §22.6.2.16 p.3767

### b:ConferenceName

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.17 p.3767

### b:Corporate

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Author`, `b:Performer`
- Specification: §22.6.2.18 p.3768

### b:Counsel

- Type: `b:CT_NameType`
- Children: `b:NameList`
- Parents: `b:Author`
- Specification: §22.6.2.19 p.3768

### b:CountryRegion

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.20 p.3768

### b:Court

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.21 p.3769

### b:Day

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.22 p.3769

### b:DayAccessed

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.23 p.3770

### b:Department

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.24 p.3770

### b:Director

- Type: `b:CT_NameType`
- Children: `b:NameList`
- Parents: `b:Author`
- Specification: §22.6.2.25 p.3770

### b:Distributor

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.26 p.3771

### b:Edition

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.27 p.3771

### b:Editor

- Type: `b:CT_NameType`
- Children: `b:NameList`
- Parents: `b:Author`
- Specification: §22.6.2.28 p.3771

### b:First

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Person`
- Specification: §22.6.2.29 p.3772

### b:Guid

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.30 p.3772, OI 2.1.1735

### b:Institution

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.31 p.3773

### b:InternetSiteTitle

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.32 p.3773

### b:Interviewee

- Type: `b:CT_NameType`
- Children: `b:NameList`
- Parents: `b:Author`
- Specification: §22.6.2.33 p.3774

### b:Interviewer

- Type: `b:CT_NameType`
- Children: `b:NameList`
- Parents: `b:Author`
- Specification: §22.6.2.34 p.3774

### b:Inventor

- Type: `b:CT_NameType`
- Children: `b:NameList`
- Parents: `b:Author`
- Specification: §22.6.2.35 p.3775

### b:Issue

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.36 p.3775

### b:JournalName

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.37 p.3776

### b:LCID

- Type: `s:ST_Lang` (text: `s:ST_Lang`)
- Parents: `b:Source`
- Specification: §22.6.2.39 p.3777, OI 2.1.1736

### b:Last

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Person`
- Specification: §22.6.2.38 p.3776

### b:Medium

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.40 p.3777

### b:Middle

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Person`
- Specification: §22.6.2.41 p.3777

### b:Month

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.42 p.3778

### b:MonthAccessed

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.43 p.3778

### b:NameList

- Type: `b:CT_NameListType`
- Children: `b:Person`
- Parents: `b:Artist`, `b:Author`, `b:BookAuthor`, `b:Compiler`, `b:Composer`, `b:Conductor`, `b:Counsel`, `b:Director`, `b:Editor`, `b:Interviewee`, `b:Interviewer`, `b:Inventor`, `b:Performer`, `b:ProducerName`, `b:Translator`, `b:Writer`
- Specification: §22.6.2.44 p.3778

### b:NumberVolumes

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.45 p.3779

### b:Pages

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.46 p.3779

### b:PatentNumber

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.47 p.3780

### b:Performer

- Type: `b:CT_NameOrCorporateType`
- Children: `b:NameList`, `b:Corporate`
- Parents: `b:Author`
- Specification: §22.6.2.48 p.3780

### b:PeriodicalTitle

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.49 p.3780

### b:Person

- Type: `b:CT_PersonType`
- Children: `b:Last`, `b:First`, `b:Middle`
- Parents: `b:NameList`
- Specification: §22.6.2.50 p.3781, OI 2.1.1737

### b:ProducerName

- Type: `b:CT_NameType`
- Children: `b:NameList`
- Parents: `b:Author`
- Specification: §22.6.2.51 p.3781

### b:ProductionCompany

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.52 p.3782

### b:PublicationTitle

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.53 p.3782

### b:Publisher

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.54 p.3782

### b:RecordingNumber

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.55 p.3783

### b:RefOrder

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.56 p.3783

### b:Reporter

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.57 p.3783

### b:ShortTitle

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.58 p.3784

### b:Source

- Type: `b:CT_SourceType`
- Children: `b:AbbreviatedCaseNumber`, `b:AlbumTitle`, `b:Author`, `b:BookTitle`, `b:Broadcaster`, `b:BroadcastTitle`, `b:CaseNumber`, `b:ChapterNumber`, `b:City`, `b:Comments`, `b:ConferenceName`, `b:CountryRegion`, `b:Court`, `b:Day`, `b:DayAccessed`, `b:Department`, `b:Distributor`, `b:Edition`, `b:Guid`, `b:Institution`, `b:InternetSiteTitle`, `b:Issue`, `b:JournalName`, `b:LCID`, `b:Medium`, `b:Month`, `b:MonthAccessed`, `b:NumberVolumes`, `b:Pages`, `b:PatentNumber`, `b:PeriodicalTitle`, `b:ProductionCompany`, `b:PublicationTitle`, `b:Publisher`, `b:RecordingNumber`, `b:RefOrder`, `b:Reporter`, `b:SourceType`, `b:ShortTitle`, `b:StandardNumber`, `b:StateProvince`, `b:Station`, `b:Tag`, `b:Theater`, `b:ThesisType`, `b:Title`, `b:Type`, `b:URL`, `b:Version`, `b:Volume`, `b:Year`, `b:YearAccessed`
- Parents: `b:Sources`
- Specification: §22.6.2.59 p.3784, OI 2.1.1738

### b:SourceType

- Type: `b:ST_SourceType` (text: `b:ST_SourceType`)
- Parents: `b:Source`
- Specification: §22.6.2.61 p.3786

### b:Sources

- Type: `b:CT_Sources`
- Attributes: `SelectedStyle`, `StyleName`, `URI`
- Children: `b:Source`
- Specification: §22.6.2.60 p.3785

### b:StandardNumber

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.62 p.3787

### b:StateProvince

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.63 p.3787

### b:Station

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.64 p.3787

### b:Tag

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.65 p.3788, OI 2.1.1739

### b:Theater

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.66 p.3788

### b:ThesisType

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.67 p.3789

### b:Title

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.68 p.3789

### b:Translator

- Type: `b:CT_NameType`
- Children: `b:NameList`
- Parents: `b:Author`
- Specification: §22.6.2.69 p.3789

### b:Type

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.70 p.3790

### b:URL

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.71 p.3790

### b:Version

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.72 p.3791

### b:Volume

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.73 p.3791

### b:Writer

- Type: `b:CT_NameType`
- Children: `b:NameList`
- Parents: `b:Author`
- Specification: §22.6.2.74 p.3792

### b:Year

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.75 p.3792

### b:YearAccessed

- Type: `s:ST_String` (text: `s:ST_String`)
- Parents: `b:Source`
- Specification: §22.6.2.76 p.3792, OI 2.1.1740

`!` marks required attributes.
