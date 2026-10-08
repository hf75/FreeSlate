# s: Packages and document properties

`http://schemas.openxmlformats.org/officeDocument/2006/sharedTypes`

- Source: ECMA-376-4 transitional schema shared-commonSimpleTypes.xsd
- Office version: 2007
- In scope: yes

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 | 0 | 0 | 0 | 28 | 50 | 27 | 8 |

Full detail (content models, attribute types, defaults): [s.json](s.json)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `s:ST_AlgClass` | restriction of xsd:string | `hash`, `custom` | Part 4 §20.1.2.1 p.879 |
| `s:ST_AlgType` | restriction of xsd:string | `typeAny`, `custom` | Part 4 §20.1.2.2 p.880 |
| `s:ST_CalendarType` | restriction of xsd:string | `gregorian`, `gregorianUs`, `gregorianMeFrench`, `gregorianArabic`, `hijri`, `hebrew`, `taiwan`, `japan`, `thai`, `korea`, `saka`, `gregorianXlitEnglish` … (+2) | §22.9.2.1 p.3800, OI 2.1.1742 |
| `s:ST_ColorType` | restriction of xsd:string |  | Part 4 §20.1.2.3 p.881, OI 2.1.1893 |
| `s:ST_ConformanceClass` | restriction of xsd:string | `strict`, `transitional` | §22.9.2.2 p.3802 |
| `s:ST_CryptProv` | restriction of xsd:string | `rsaAES`, `rsaFull`, `custom` | Part 4 §20.1.2.4 p.882 |
| `s:ST_FixedPercentage` | restriction of s:ST_Percentage | pattern `-?((100)\|([0-9][0-9]?))(\.[0-9][0-9]?)?%` | §22.9.2.3 p.3802 |
| `s:ST_Guid` | restriction of xsd:token | pattern `\{[0-9A-F]{8}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{12}\}` | §22.9.2.4 p.3803 |
| `s:ST_HexColorRGB` | restriction of xsd:hexBinary | length `3` | §22.9.2.5 p.3803 |
| `s:ST_Lang` | restriction of xsd:string |  | §22.9.2.6 p.3804, OI 2.1.1743 |
| `s:ST_OnOff` | union of xsd:boolean, s:ST_OnOff1 |  | §22.9.2.7 p.3804, OI 2.1.1744 |
| `s:ST_OnOff1` | restriction of xsd:string | `on`, `off` |  |
| `s:ST_Panose` | restriction of xsd:hexBinary | length `10` | §22.9.2.8 p.3804 |
| `s:ST_Percentage` | restriction of xsd:string | pattern `-?[0-9]+(\.[0-9]+)?%` | §22.9.2.9 p.3805 |
| `s:ST_PositiveFixedPercentage` | restriction of s:ST_Percentage | pattern `((100)\|([0-9][0-9]?))(\.[0-9][0-9]?)?%` | §22.9.2.10 p.3806 |
| `s:ST_PositivePercentage` | restriction of s:ST_Percentage | pattern `[0-9]+(\.[0-9]+)?%` | §22.9.2.11 p.3806 |
| `s:ST_PositiveUniversalMeasure` | restriction of s:ST_UniversalMeasure | pattern `[0-9]+(\.[0-9]+)?(mm\|cm\|in\|pt\|pc\|pi)` | §22.9.2.12 p.3807 |
| `s:ST_String` | restriction of xsd:string |  | §22.9.2.13 p.3807 |
| `s:ST_TrueFalse` | restriction of xsd:string | `t`, `f`, `true`, `false` | Part 4 §20.1.2.5 p.883 |
| `s:ST_TrueFalseBlank` | restriction of xsd:string | `t`, `f`, `true`, `false`, ``, `True`, `False` | Part 4 §20.1.2.6 p.883, OI 2.1.1894 |
| `s:ST_TwipsMeasure` | union of s:ST_UnsignedDecimalNumber, s:ST_PositiveUniversalMeasure |  | §22.9.2.14 p.3807, OI 2.1.1745 |
| `s:ST_UniversalMeasure` | restriction of xsd:string | pattern `-?[0-9]+(\.[0-9]+)?(mm\|cm\|in\|pt\|pc\|pi)` | §22.9.2.15 p.3808 |
| `s:ST_UnsignedDecimalNumber` | restriction of xsd:unsignedLong |  | §22.9.2.16 p.3808, OI 2.1.1746 |
| `s:ST_VerticalAlignRun` | restriction of xsd:string | `baseline`, `superscript`, `subscript` | §22.9.2.17 p.3809 |
| `s:ST_XAlign` | restriction of xsd:string | `left`, `center`, `right`, `inside`, `outside` | §22.9.2.18 p.3810 |
| `s:ST_XmlName` | restriction of xsd:NCName | minLength `1`, maxLength `255` | §22.9.2.21 p.3813 |
| `s:ST_Xstring` | restriction of xsd:string |  | §22.9.2.19 p.3811, OI 2.1.1747 |
| `s:ST_YAlign` | restriction of xsd:string | `inline`, `top`, `center`, `bottom`, `inside`, `outside` | §22.9.2.20 p.3811 |

