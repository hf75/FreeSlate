# x: SpreadsheetML (out of scope)

`http://schemas.openxmlformats.org/spreadsheetml/2006/main`

- Source: ECMA-376-4 transitional schema sml.xsd
- Office version: 2007
- In scope: no

| Elements | Attributes | Global attributes | Complex types | Simple types | Enum values | Spec sections | Office notes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 389 | 1,594 | 0 | 367 | 96 | 560 | 508 | 316 |

Full detail (content models, attribute types, defaults): [x.json](x.json)

## Elements

| Element | Type | Attributes | Children | Parents | Specification |
|---|---|---:|---:|---:|---|
| [`x:DataBinding`](#xdatabinding) | `x:CT_DataBinding` | 5 | 1 | 1 | §18.16.1 p.2044, OI 2.1.856 |
| [`x:Map`](#xmap) | `x:CT_Map` | 9 | 1 | 1 | §18.16.2 p.2046, OI 2.1.857 |
| [`x:MapInfo`](#xmapinfo) ◆ | `x:CT_MapInfo` | 1 | 2 | 0 | §18.16.3 p.2047 |
| [`x:Schema`](#xschema) | `x:CT_Schema` | 4 | 1 | 1 | §18.16.4 p.2048, OI 2.1.858 |
| [`x:alignment`](#xalignment) | `x:CT_CellAlignment` | 9 | 0 | 4 | §18.8.1 p.1762, OI 2.1.693 |
| [`x:anchor`](#xanchor) | `x:CT_ObjectAnchor` | 2 | 2 | 3 | §18.3.1.1 p.1600, OI 2.1.588 |
| [`x:author`](#xauthor) | `s:ST_Xstring` | 0 | 0 | 1 | §18.7.1 p.1756, OI 2.1.689 |
| [`x:authors`](#xauthors) | `x:CT_Authors` | 0 | 1 | 1 | §18.7.2 p.1757 |
| [`x:autoFilter`](#xautofilter) | `x:CT_AutoFilter` | 1 | 3 | 4 | §18.3.1.2 p.1601 |
| [`x:autoSortScope`](#xautosortscope) | `x:CT_AutoSortScope` | 0 | 1 | 1 | §18.10.1.1 p.1830, OI 2.1.737 |
| [`x:b`](#xb) | `x:CT_Boolean`, `x:CT_BooleanProperty` | 6 | 1 | 5 | §18.8.2 p.1765; §18.10.1.2 p.1830, OI 2.1.738 |
| [`x:bgColor`](#xbgcolor) | `x:CT_Color` | 5 | 0 | 1 | §18.8.3 p.1766, OI 2.1.694 |
| [`x:bk`](#xbk) | `x:CT_MetadataBlock`, `x:CT_FutureMetadataBlock` | 0 | 2 | 3 | §18.9.1 p.1812, OI 2.1.722; §18.9.2 p.1813 |
| [`x:bookViews`](#xbookviews) | `x:CT_BookViews` | 0 | 1 | 1 | §18.2.1 p.1553, OI 2.1.564 |
| [`x:border`](#xborder) | `x:CT_Border` | 3 | 9 | 4 | §18.8.4 p.1767, OI 2.1.695 |
| [`x:borders`](#xborders) | `x:CT_Borders` | 1 | 1 | 1 | §18.8.5 p.1768, OI 2.1.696 |
| [`x:bottom`](#xbottom) | `x:CT_BorderPr` | 1 | 1 | 1 | §18.8.6 p.1769 |
| [`x:brk`](#xbrk) | `x:CT_Break` | 5 | 0 | 2 | §18.3.1.3 p.1602, OI 2.1.589 |
| [`x:c`](#xc) | `x:CT_CalcCell`, `x:CT_Cell` | 10 | 4 | 2 | §18.3.1.4 p.1603, OI 2.1.590; §18.6.1 p.1754, OI 2.1.688 |
| [`x:cacheField`](#xcachefield) | `x:CT_CacheField` | 13 | 4 | 1 | §18.10.1.3 p.1831, OI 2.1.739 |
| [`x:cacheFields`](#xcachefields) | `x:CT_CacheFields` | 1 | 1 | 1 | §18.10.1.4 p.1834, OI 2.1.740 |
| [`x:cacheHierarchies`](#xcachehierarchies) | `x:CT_CacheHierarchies` | 1 | 1 | 1 | §18.10.1.5 p.1835, OI 2.1.741 |
| [`x:cacheHierarchy`](#xcachehierarchy) | `x:CT_CacheHierarchy` | 22 | 3 | 1 | §18.10.1.6 p.1835, OI 2.1.742 |
| [`x:cacheSource`](#xcachesource) | `x:CT_CacheSource` | 2 | 3 | 1 | §18.10.1.7 p.1840, OI 2.1.743 |
| [`x:calcChain`](#xcalcchain) ◆ | `x:CT_CalcChain` | 0 | 2 | 0 | §18.6.2 p.1755 |
| [`x:calcPr`](#xcalcpr) | `x:CT_CalcPr` | 13 | 0 | 1 | §18.2.2 p.1553, OI 2.1.565 |
| [`x:calculatedColumnFormula`](#xcalculatedcolumnformula) | `x:CT_TableFormula` | 1 | 0 | 1 | §18.5.1.1 p.1738, OI 2.1.679 |
| [`x:calculatedItem`](#xcalculateditem) | `x:CT_CalculatedItem` | 2 | 2 | 1 | §18.10.1.8 p.1841, OI 2.1.744 |
| [`x:calculatedItems`](#xcalculateditems) | `x:CT_CalculatedItems` | 1 | 1 | 1 | §18.10.1.9 p.1842 |
| [`x:calculatedMember`](#xcalculatedmember) | `x:CT_CalculatedMember` | 7 | 1 | 1 | §18.10.1.10 p.1842, OI 2.1.745 |
| [`x:calculatedMembers`](#xcalculatedmembers) | `x:CT_CalculatedMembers` | 1 | 1 | 1 | §18.10.1.11 p.1843 |
| [`x:cell`](#xcell) | `x:CT_ExternalCell` | 3 | 1 | 1 | §18.14.1 p.2027, OI 2.1.838 |
| [`x:cellMetadata`](#xcellmetadata) | `x:CT_MetadataBlocks` | 1 | 1 | 1 | §18.9.3 p.1813, OI 2.1.723 |
| [`x:cellSmartTag`](#xcellsmarttag) | `x:CT_CellSmartTag` | 3 | 1 | 1 | §18.3.1.5 p.1604, OI 2.1.591 |
| [`x:cellSmartTagPr`](#xcellsmarttagpr) | `x:CT_CellSmartTagPr` | 2 | 0 | 1 | §18.3.1.6 p.1605, OI 2.1.592 |
| [`x:cellSmartTags`](#xcellsmarttags) | `x:CT_CellSmartTags` | 1 | 1 | 1 | §18.3.1.7 p.1605, OI 2.1.593 |
| [`x:cellStyle`](#xcellstyle) | `x:CT_CellStyle` | 6 | 1 | 1 | §18.8.7 p.1770, OI 2.1.697 |
| [`x:cellStyleXfs`](#xcellstylexfs) | `x:CT_CellStyleXfs` | 1 | 1 | 1 | §18.8.9 p.1772, OI 2.1.699 |
| [`x:cellStyles`](#xcellstyles) | `x:CT_CellStyles` | 1 | 1 | 1 | §18.8.8 p.1771, OI 2.1.698 |
| [`x:cellWatch`](#xcellwatch) | `x:CT_CellWatch` | 1 | 0 | 1 | §18.3.1.8 p.1606, OI 2.1.594 |
| [`x:cellWatches`](#xcellwatches) | `x:CT_CellWatches` | 0 | 1 | 1 | §18.3.1.9 p.1606 |
| [`x:cellXfs`](#xcellxfs) | `x:CT_CellXfs` | 1 | 1 | 1 | §18.8.10 p.1773, OI 2.1.700 |
| [`x:cfRule`](#xcfrule) | `x:CT_CfRule` | 13 | 5 | 1 | §18.3.1.10 p.1607, OI 2.1.595 |
| [`x:cfvo`](#xcfvo) | `x:CT_Cfvo` | 3 | 1 | 3 | §18.3.1.11 p.1609, OI 2.1.596 |
| [`x:charset`](#xcharset) | `x:CT_IntProperty` | 1 | 0 | 2 | §18.4.1 p.1729 |
| [`x:chartFormat`](#xchartformat) | `x:CT_ChartFormat` | 3 | 1 | 1 | §18.10.1.12 p.1844, OI 2.1.746 |
| [`x:chartFormats`](#xchartformats) | `x:CT_ChartFormats` | 1 | 1 | 1 | §18.10.1.13 p.1845 |
| [`x:chartsheet`](#xchartsheet) ◆ | `x:CT_Chartsheet` | 0 | 14 | 0 | §18.3.1.12 p.1610, OI 2.1.597 |
| [`x:col`](#xcol) | `x:CT_Col` | 10 | 0 | 1 | §18.3.1.13 p.1610, OI 2.1.598 |
| [`x:colBreaks`](#xcolbreaks) | `x:CT_PageBreak` | 2 | 1 | 2 | §18.3.1.14 p.1612, OI 2.1.599 |
| [`x:colFields`](#xcolfields) | `x:CT_ColFields` | 1 | 1 | 1 | §18.10.1.14 p.1847 |
| [`x:colHierarchiesUsage`](#xcolhierarchiesusage) | `x:CT_ColHierarchiesUsage` | 1 | 1 | 1 | §18.10.1.15 p.1847, OI 2.1.747 |
| [`x:colHierarchyUsage`](#xcolhierarchyusage) | `x:CT_HierarchyUsage` | 1 | 0 | 1 | §18.10.1.16 p.1848, OI 2.1.748 |
| [`x:colItems`](#xcolitems) | `x:CT_colItems` | 1 | 1 | 1 | §18.10.1.17 p.1848, OI 2.1.749 |
| [`x:color`](#xcolor) | `x:CT_Color` | 5 | 0 | 15 | §18.3.1.15 p.1613, OI 2.1.600 |
| [`x:colorFilter`](#xcolorfilter) | `x:CT_ColorFilter` | 2 | 0 | 1 | §18.3.2.1 p.1720, OI 2.1.663 |
| [`x:colorScale`](#xcolorscale) | `x:CT_ColorScale` | 0 | 2 | 1 | §18.3.1.16 p.1614, OI 2.1.601 |
| [`x:colors`](#xcolors) | `x:CT_Colors` | 0 | 2 | 1 | §18.8.11 p.1773 |
| [`x:cols`](#xcols) | `x:CT_Cols` | 0 | 1 | 1 | §18.3.1.17 p.1615 |
| [`x:comment`](#xcomment) | `x:CT_Comment` | 4 | 2 | 1 | §18.7.3 p.1757, OI 2.1.690 |
| [`x:commentList`](#xcommentlist) | `x:CT_CommentList` | 0 | 1 | 1 | §18.7.4 p.1758 |
| [`x:commentPr`](#xcommentpr) | `x:CT_CommentPr` | 12 | 1 | 1 | §18.7.5 p.1758, OI 2.1.691 |
| [`x:comments`](#xcomments) ◆ | `x:CT_Comments` | 0 | 3 | 0 | §18.7.6 p.1762 |
| [`x:condense`](#xcondense) | `x:CT_BooleanProperty` | 1 | 0 | 2 | §18.8.12 p.1773 |
| [`x:conditionalFormat`](#xconditionalformat) | `x:CT_ConditionalFormat` | 3 | 2 | 1 | §18.10.1.18 p.1850, OI 2.1.750 |
| [`x:conditionalFormats`](#xconditionalformats) | `x:CT_ConditionalFormats` | 1 | 1 | 1 | §18.10.1.19 p.1850 |
| [`x:conditionalFormatting`](#xconditionalformatting) | `x:CT_ConditionalFormatting` | 2 | 2 | 1 | §18.3.1.18 p.1615, OI 2.1.602 |
| [`x:connection`](#xconnection) | `x:CT_Connection` | 20 | 6 | 1 | §18.13.1 p.2006, OI 2.1.829 |
| [`x:connections`](#xconnections) ◆ | `x:CT_Connections` | 0 | 1 | 0 | §18.13.2 p.2010 |
| [`x:consolidation`](#xconsolidation) | `x:CT_Consolidation` | 1 | 2 | 1 | §18.10.1.20 p.1851 |
| [`x:control`](#xcontrol) | `x:CT_Control` | 3 | 1 | 1 | §18.3.1.19 p.1616, OI 2.1.603 |
| [`x:controlPr`](#xcontrolpr) | `x:CT_ControlPr` | 15 | 1 | 1 | §18.3.1.20 p.1616, OI 2.1.604 |
| [`x:controls`](#xcontrols) | `x:CT_Controls` | 0 | 1 | 2 | §18.3.1.21 p.1621, OI 2.1.605 |
| [`x:customFilter`](#xcustomfilter) | `x:CT_CustomFilter` | 2 | 0 | 1 | §18.3.2.2 p.1721 |
| [`x:customFilters`](#xcustomfilters) | `x:CT_CustomFilters` | 1 | 1 | 1 | §18.3.2.3 p.1722 |
| [`x:customPr`](#xcustompr) | `x:CT_CustomProperty` | 2 | 0 | 1 | §18.3.1.22 p.1621, OI 2.1.606 |
| [`x:customProperties`](#xcustomproperties) | `x:CT_CustomProperties` | 0 | 1 | 1 | §18.3.1.23 p.1622 |
| [`x:customSheetView`](#xcustomsheetview) | `x:CT_CustomSheetView`, `x:CT_CustomChartsheetView` | 21 | 10 | 1 | §18.3.1.24 p.1622, OI 2.1.607; §18.3.1.25 p.1623, OI 2.1.608 |
| [`x:customSheetViews`](#xcustomsheetviews) | `x:CT_CustomSheetViews`, `x:CT_CustomChartsheetViews` | 0 | 1 | 3 | §18.3.1.26 p.1625, OI 2.1.609; §18.3.1.27 p.1626, OI 2.1.610 |
| [`x:customWorkbookView`](#xcustomworkbookview) | `x:CT_CustomWorkbookView` | 24 | 1 | 1 | §18.2.3 p.1557, OI 2.1.566 |
| [`x:customWorkbookViews`](#xcustomworkbookviews) | `x:CT_CustomWorkbookViews` | 0 | 1 | 1 | §18.2.4 p.1563, OI 2.1.567 |
| [`x:d`](#xd) | `x:CT_DateTime` | 5 | 1 | 3 | §18.10.1.21 p.1852, OI 2.1.751 |
| [`x:dataBar`](#xdatabar) | `x:CT_DataBar` | 3 | 2 | 1 | §18.3.1.28 p.1626, OI 2.1.611 |
| [`x:dataConsolidate`](#xdataconsolidate) | `x:CT_DataConsolidate` | 5 | 1 | 1 | §18.3.1.29 p.1627, OI 2.1.612 |
| [`x:dataField`](#xdatafield) | `x:CT_DataField` | 7 | 1 | 1 | §18.10.1.22 p.1852, OI 2.1.752 |
| [`x:dataFields`](#xdatafields) | `x:CT_DataFields` | 1 | 1 | 1 | §18.10.1.23 p.1854 |
| [`x:dataRef`](#xdataref) | `x:CT_DataRef` | 4 | 0 | 1 | §18.3.1.30 p.1628 |
| [`x:dataRefs`](#xdatarefs) | `x:CT_DataRefs` | 1 | 1 | 1 | §18.3.1.31 p.1628 |
| [`x:dataValidation`](#xdatavalidation) | `x:CT_DataValidation` | 13 | 2 | 1 | §18.3.1.32 p.1629, OI 2.1.613 |
| [`x:dataValidations`](#xdatavalidations) | `x:CT_DataValidations` | 4 | 1 | 1 | §18.3.1.33 p.1630, OI 2.1.614 |
| [`x:dateGroupItem`](#xdategroupitem) | `x:CT_DateGroupItem` | 7 | 0 | 1 | §18.3.2.4 p.1722, OI 2.1.664 |
| [`x:dbPr`](#xdbpr) | `x:CT_DbPr` | 4 | 0 | 1 | §18.13.3 p.2010, OI 2.1.830 |
| [`x:ddeItem`](#xddeitem) | `x:CT_DdeItem` | 4 | 1 | 1 | §18.14.2 p.2028, OI 2.1.839 |
| [`x:ddeItems`](#xddeitems) | `x:CT_DdeItems` | 0 | 1 | 1 | §18.14.3 p.2029 |
| [`x:ddeLink`](#xddelink) | `x:CT_DdeLink` | 2 | 1 | 1 | §18.14.4 p.2029, OI 2.1.840 |
| [`x:definedName`](#xdefinedname) | `x:CT_ExternalDefinedName`, `x:CT_DefinedName` | 17 | 0 | 1 | §18.2.5 p.1563, OI 2.1.568; §18.14.5 p.2030, OI 2.1.841 |
| [`x:definedNames`](#xdefinednames) | `x:CT_DefinedNames`, `x:CT_ExternalDefinedNames` | 0 | 1 | 2 | §18.2.6 p.1568, OI 2.1.569; §18.14.6 p.2030 |
| [`x:deletedField`](#xdeletedfield) | `x:CT_DeletedField` | 1 | 0 | 1 | §18.12.1 p.1999, OI 2.1.825 |
| [`x:diagonal`](#xdiagonal) | `x:CT_BorderPr` | 1 | 1 | 1 | §18.8.13 p.1774 |
| [`x:dialogsheet`](#xdialogsheet) ◆ | `x:CT_Dialogsheet` | 0 | 16 | 0 | §18.3.1.34 p.1632, OI 2.1.615 |
| [`x:dimension`](#xdimension) | `x:CT_SheetDimension`, `x:CT_PivotDimension` | 5 | 0 | 2 | §18.3.1.35 p.1632; §18.10.1.24 p.1855, OI 2.1.753 |
| [`x:dimensions`](#xdimensions) | `x:CT_Dimensions` | 1 | 1 | 1 | §18.10.1.25 p.1855 |
| [`x:discretePr`](#xdiscretepr) | `x:CT_DiscretePr` | 1 | 1 | 1 | §18.10.1.26 p.1856, OI 2.1.754 |
| [`x:drawing`](#xdrawing) | `x:CT_Drawing` | 1 | 0 | 3 | §18.3.1.36 p.1632, OI 2.1.616 |
| [`x:drawingHF`](#xdrawinghf) | `x:CT_DrawingHF` | 19 | 0 | 3 | §18.3.1.37 p.1633, OI 2.1.617 |
| [`x:dxf`](#xdxf) | `x:CT_Dxf` | 0 | 7 | 2 | §18.8.14 p.1774 |
| [`x:dxfs`](#xdxfs) | `x:CT_Dxfs` | 1 | 1 | 1 | §18.8.15 p.1774, OI 2.1.701 |
| [`x:dynamicFilter`](#xdynamicfilter) | `x:CT_DynamicFilter` | 5 | 0 | 1 | §18.3.2.5 p.1723, OI 2.1.665 |
| [`x:e`](#xe) | `x:CT_Error` | 12 | 2 | 4 | §18.10.1.27 p.1858, OI 2.1.755 |
| [`x:end`](#xend) | `x:CT_BorderPr` | 1 | 1 | 1 | §18.8.16 p.1774, OI 2.1.702 |
| [`x:entries`](#xentries) | `x:CT_PCDSDTCEntries` | 1 | 4 | 1 | §18.10.1.28 p.1860 |
| [`x:evenFooter`](#xevenfooter) | `s:ST_Xstring` | 0 | 0 | 1 | §18.3.1.38 p.1639, OI 2.1.618 |
| [`x:evenHeader`](#xevenheader) | `s:ST_Xstring` | 0 | 0 | 1 | §18.3.1.39 p.1639, OI 2.1.619 |
| [`x:ext`](#xext) | `x:CT_Extension` | 1 | 1 | 1 | §18.2.7 p.1569, OI 2.1.570 |
| [`x:extLst`](#xextlst) | `x:CT_ExtensionList` | 0 | 1 | 69 | §18.2.10 p.1570 |
| [`x:extend`](#xextend) | `x:CT_BooleanProperty` | 1 | 0 | 2 | §18.8.17 p.1775 |
| [`x:externalBook`](#xexternalbook) | `x:CT_ExternalBook` | 1 | 3 | 1 | §18.14.7 p.2030 |
| [`x:externalLink`](#xexternallink) ◆ | `x:CT_ExternalLink` | 0 | 4 | 0 | §18.14.8 p.2031, OI 2.1.842 |
| [`x:externalReference`](#xexternalreference) | `x:CT_ExternalReference` | 1 | 0 | 1 | §18.2.8 p.1570 |
| [`x:externalReferences`](#xexternalreferences) | `x:CT_ExternalReferences` | 0 | 1 | 1 | §18.2.9 p.1570, OI 2.1.571 |
| [`x:f`](#xf) | `x:CT_CellFormula` | 12 | 0 | 3 | §18.3.1.40 p.1640, OI 2.1.620 |
| [`x:family`](#xfamily) | `x:CT_IntProperty`, `x:CT_FontFamily` | 1 | 0 | 2 | §18.8.18 p.1775, OI 2.1.703 |
| [`x:fgColor`](#xfgcolor) | `x:CT_Color` | 5 | 0 | 1 | §18.8.19 p.1775, OI 2.1.704 |
| [`x:field`](#xfield) | `x:CT_Field` | 1 | 0 | 2 | §18.10.1.29 p.1861, OI 2.1.756 |
| [`x:fieldGroup`](#xfieldgroup) | `x:CT_FieldGroup` | 2 | 3 | 1 | §18.10.1.30 p.1861, OI 2.1.757 |
| [`x:fieldUsage`](#xfieldusage) | `x:CT_FieldUsage` | 1 | 0 | 1 | §18.10.1.32 p.1863, OI 2.1.759 |
| [`x:fieldsUsage`](#xfieldsusage) | `x:CT_FieldsUsage` | 1 | 1 | 1 | §18.10.1.31 p.1863, OI 2.1.758 |
| [`x:fileRecoveryPr`](#xfilerecoverypr) | `x:CT_FileRecoveryPr` | 4 | 0 | 1 | §18.2.11 p.1571, OI 2.1.572 |
| [`x:fileSharing`](#xfilesharing) | `x:CT_FileSharing` | 7 | 0 | 1 | §18.2.12 p.1572, OI 2.1.573 |
| [`x:fileVersion`](#xfileversion) | `x:CT_FileVersion` | 5 | 0 | 1 | §18.2.13 p.1575, OI 2.1.574 |
| [`x:fill`](#xfill) | `x:CT_Fill` | 0 | 2 | 4 | §18.8.20 p.1777 |
| [`x:fills`](#xfills) | `x:CT_Fills` | 1 | 1 | 1 | §18.8.21 p.1777, OI 2.1.705 |
| [`x:filter`](#xfilter) | `x:CT_Filter`, `x:CT_PivotFilter` | 12 | 2 | 1 | §18.3.2.6 p.1724, OI 2.1.666; §18.10.1.33 p.1864, OI 2.1.760 |
| [`x:filterColumn`](#xfiltercolumn) | `x:CT_FilterColumn` | 3 | 7 | 1 | §18.3.2.7 p.1725, OI 2.1.667 |
| [`x:filters`](#xfilters) | `x:CT_PivotFilters`, `x:CT_Filters` | 3 | 2 | 2 | §18.3.2.8 p.1725, OI 2.1.668; §18.10.1.34 p.1865 |
| [`x:firstFooter`](#xfirstfooter) | `s:ST_Xstring` | 0 | 0 | 1 | §18.3.1.41 p.1643, OI 2.1.621 |
| [`x:firstHeader`](#xfirstheader) | `s:ST_Xstring` | 0 | 0 | 1 | §18.3.1.42 p.1644, OI 2.1.622 |
| [`x:font`](#xfont) | `x:CT_Font` | 0 | 15 | 4 | §18.8.22 p.1778, OI 2.1.706 |
| [`x:fonts`](#xfonts) | `x:CT_Fonts` | 1 | 1 | 1 | §18.8.23 p.1778, OI 2.1.707 |
| [`x:format`](#xformat) | `x:CT_Format` | 2 | 2 | 1 | §18.10.1.35 p.1866, OI 2.1.761 |
| [`x:formats`](#xformats) | `x:CT_Formats` | 1 | 1 | 1 | §18.10.1.36 p.1867 |
| [`x:formula`](#xformula) | `x:ST_Formula` | 0 | 0 | 2 | §18.3.1.43 p.1645, OI 2.1.623 |
| [`x:formula1`](#xformula1) | `x:ST_Formula` | 0 | 0 | 1 | §18.3.1.44 p.1645, OI 2.1.624 |
| [`x:formula2`](#xformula2) | `x:ST_Formula` | 0 | 0 | 1 | §18.3.1.45 p.1646, OI 2.1.625 |
| [`x:functionGroup`](#xfunctiongroup) | `x:CT_FunctionGroup` | 1 | 0 | 1 | §18.2.14 p.1576, OI 2.1.575 |
| [`x:functionGroups`](#xfunctiongroups) | `x:CT_FunctionGroups` | 1 | 1 | 1 | §18.2.15 p.1577, OI 2.1.576 |
| [`x:futureMetadata`](#xfuturemetadata) | `x:CT_FutureMetadata` | 2 | 2 | 1 | §18.9.4 p.1813, OI 2.1.724 |
| [`x:gradientFill`](#xgradientfill) | `x:CT_GradientFill` | 6 | 1 | 1 | §18.8.24 p.1779, OI 2.1.708 |
| [`x:group`](#xgroup) | `x:CT_LevelGroup` | 5 | 1 | 1 | §18.10.1.37 p.1867, OI 2.1.762 |
| [`x:groupItems`](#xgroupitems) | `x:CT_GroupItems` | 1 | 6 | 1 | §18.10.1.38 p.1868, OI 2.1.763 |
| [`x:groupLevel`](#xgrouplevel) | `x:CT_GroupLevel` | 4 | 2 | 1 | §18.10.1.39 p.1869, OI 2.1.764 |
| [`x:groupLevels`](#xgrouplevels) | `x:CT_GroupLevels` | 1 | 1 | 1 | §18.10.1.40 p.1871 |
| [`x:groupMember`](#xgroupmember) | `x:CT_GroupMember` | 2 | 0 | 1 | §18.10.1.41 p.1872, OI 2.1.765 |
| [`x:groupMembers`](#xgroupmembers) | `x:CT_GroupMembers` | 1 | 1 | 1 | §18.10.1.42 p.1872 |
| [`x:groups`](#xgroups) | `x:CT_Groups` | 1 | 1 | 1 | §18.10.1.43 p.1873 |
| [`x:header`](#xheader) | `x:CT_RevisionHeader` | 7 | 3 | 1 | §18.11.1.1 p.1972, OI 2.1.804 |
| [`x:headerFooter`](#xheaderfooter) | `x:CT_HeaderFooter` | 4 | 6 | 4 | §18.3.1.46 p.1646 |
| [`x:headers`](#xheaders) ◆ | `x:CT_RevisionHeaders` | 12 | 1 | 0 | §18.11.1.2 p.1974, OI 2.1.805 |
| [`x:horizontal`](#xhorizontal) | `x:CT_BorderPr` | 1 | 1 | 1 | §18.8.25 p.1781 |
| [`x:hyperlink`](#xhyperlink) | `x:CT_Hyperlink` | 5 | 0 | 1 | §18.3.1.47 p.1651, OI 2.1.626 |
| [`x:hyperlinks`](#xhyperlinks) | `x:CT_Hyperlinks` | 0 | 1 | 1 | §18.3.1.48 p.1652 |
| [`x:i`](#xi) | `x:CT_I`, `x:CT_BooleanProperty` | 4 | 1 | 4 | §18.8.26 p.1782; §18.10.1.44 p.1873, OI 2.1.766 |
| [`x:iconFilter`](#xiconfilter) | `x:CT_IconFilter` | 2 | 0 | 1 | §18.3.2.9 p.1726, OI 2.1.669 |
| [`x:iconSet`](#xiconset) | `x:CT_IconSet` | 4 | 1 | 1 | §18.3.1.49 p.1652 |
| [`x:ignoredError`](#xignorederror) | `x:CT_IgnoredError` | 10 | 0 | 1 | §18.3.1.50 p.1653, OI 2.1.627 |
| [`x:ignoredErrors`](#xignorederrors) | `x:CT_IgnoredErrors` | 0 | 2 | 1 | §18.3.1.51 p.1655, OI 2.1.628 |
| [`x:indexedColors`](#xindexedcolors) | `x:CT_IndexedColors` | 0 | 1 | 1 | §18.8.27 p.1782, OI 2.1.709 |
| [`x:inputCells`](#xinputcells) | `x:CT_InputCells` | 5 | 0 | 1 | §18.3.1.52 p.1655, OI 2.1.629 |
| [`x:is`](#xis) | `x:CT_Rst` | 0 | 4 | 3 | §18.3.1.53 p.1656, OI 2.1.630 |
| [`x:item`](#xitem) | `x:CT_Item` | 11 | 0 | 1 | §18.10.1.45 p.1874, OI 2.1.767 |
| [`x:items`](#xitems) | `x:CT_Items` | 1 | 1 | 1 | §18.10.1.46 p.1877, OI 2.1.768 |
| [`x:k`](#xk) | `x:CT_MdxKPI` | 3 | 0 | 1 | §18.9.5 p.1814, OI 2.1.725 |
| [`x:kpi`](#xkpi) | `x:CT_PCDKPI` | 11 | 0 | 1 | §18.10.1.47 p.1878, OI 2.1.769 |
| [`x:kpis`](#xkpis) | `x:CT_PCDKPIs` | 1 | 1 | 1 | §18.10.1.48 p.1879 |
| [`x:left`](#xleft) | `x:CT_BorderPr` | 1 | 1 | 1 | Part 4 §15.5.1 p.235 |
| [`x:legacyDrawing`](#xlegacydrawing) | `x:CT_LegacyDrawing` | 1 | 0 | 3 | Part 4 §15.4.1.1 p.232 |
| [`x:legacyDrawingHF`](#xlegacydrawinghf) | `x:CT_LegacyDrawing` | 1 | 0 | 3 | Part 4 §15.4.1.2 p.232 |
| [`x:location`](#xlocation) | `x:CT_Location` | 6 | 0 | 1 | §18.10.1.49 p.1880, OI 2.1.770 |
| [`x:m`](#xm) | `x:CT_TableMissing`, `x:CT_Missing` | 11 | 2 | 5 | §18.10.1.50 p.1881, OI 2.1.771; §18.13.4 p.2013 |
| [`x:main`](#xmain) | `x:CT_VolMain` | 1 | 1 | 1 | §18.15.1 p.2038, OI 2.1.852 |
| [`x:map`](#xmap) | `x:CT_MeasureDimensionMap` | 2 | 0 | 1 | §18.10.1.51 p.1883, OI 2.1.772 |
| [`x:maps`](#xmaps) | `x:CT_MeasureDimensionMaps` | 1 | 1 | 1 | §18.10.1.52 p.1884 |
| [`x:mdx`](#xmdx) | `x:CT_Mdx` | 2 | 4 | 1 | §18.9.6 p.1814, OI 2.1.726 |
| [`x:mdxMetadata`](#xmdxmetadata) | `x:CT_MdxMetadata` | 1 | 1 | 1 | §18.9.7 p.1815, OI 2.1.727 |
| [`x:measureGroup`](#xmeasuregroup) | `x:CT_MeasureGroup` | 2 | 0 | 1 | §18.10.1.53 p.1884, OI 2.1.773 |
| [`x:measureGroups`](#xmeasuregroups) | `x:CT_MeasureGroups` | 1 | 1 | 1 | §18.10.1.54 p.1885 |
| [`x:member`](#xmember) | `x:CT_Member` | 1 | 0 | 1 | §18.10.1.55 p.1886, OI 2.1.774 |
| [`x:members`](#xmembers) | `x:CT_Members` | 2 | 1 | 1 | §18.10.1.56 p.1886, OI 2.1.775 |
| [`x:mergeCell`](#xmergecell) | `x:CT_MergeCell` | 1 | 0 | 1 | §18.3.1.54 p.1656 |
| [`x:mergeCells`](#xmergecells) | `x:CT_MergeCells` | 1 | 1 | 1 | §18.3.1.55 p.1656, OI 2.1.631 |
| [`x:metadata`](#xmetadata) ◆ | `x:CT_Metadata` | 0 | 7 | 0 | §18.9.8 p.1815 |
| [`x:metadataStrings`](#xmetadatastrings) | `x:CT_MetadataStrings` | 1 | 1 | 1 | §18.9.9 p.1815, OI 2.1.728 |
| [`x:metadataType`](#xmetadatatype) | `x:CT_MetadataType` | 28 | 0 | 1 | §18.9.10 p.1816, OI 2.1.729 |
| [`x:metadataTypes`](#xmetadatatypes) | `x:CT_MetadataTypes` | 1 | 1 | 1 | §18.9.11 p.1821, OI 2.1.730 |
| [`x:mp`](#xmp) | `x:CT_MemberProperty` | 9 | 0 | 1 | §18.10.1.57 p.1886, OI 2.1.776 |
| [`x:mpMap`](#xmpmap) | `x:CT_X` | 1 | 0 | 1 | §18.10.1.58 p.1888, OI 2.1.777 |
| [`x:mps`](#xmps) | `x:CT_MemberProperties` | 1 | 1 | 1 | §18.10.1.59 p.1889 |
| [`x:mruColors`](#xmrucolors) | `x:CT_MRUColors` | 0 | 1 | 1 | §18.8.28 p.1785, OI 2.1.710 |
| [`x:ms`](#xms) | `x:CT_MdxSet` | 3 | 1 | 1 | §18.9.12 p.1822, OI 2.1.731 |
| [`x:n`](#xn) | `x:CT_Number`, `x:CT_MetadataStringIndex` | 14 | 2 | 6 | §18.9.13 p.1822, OI 2.1.732; §18.10.1.60 p.1889, OI 2.1.778 |
| [`x:name`](#xname) | `x:CT_FontName` | 1 | 0 | 1 | §18.8.29 p.1785, OI 2.1.711 |
| [`x:nc`](#xnc) | `x:CT_Cell` | 6 | 4 | 1 | §18.11.1.3 p.1976, OI 2.1.806 |
| [`x:ndxf`](#xndxf) | `x:CT_Dxf` | 0 | 7 | 1 | §18.11.1.4 p.1977 |
| [`x:numFmt`](#xnumfmt) | `x:CT_NumFmt` | 2 | 0 | 4 | §18.8.30 p.1786, OI 2.1.712 |
| [`x:numFmts`](#xnumfmts) | `x:CT_NumFmts` | 1 | 1 | 1 | §18.8.31 p.1793, OI 2.1.713 |
| [`x:objectPr`](#xobjectpr) | `x:CT_ObjectPr` | 12 | 1 | 1 | §18.3.1.56 p.1657, OI 2.1.632 |
| [`x:oc`](#xoc) | `x:CT_Cell` | 6 | 4 | 1 | §18.11.1.5 p.1977, OI 2.1.807 |
| [`x:oddFooter`](#xoddfooter) | `s:ST_Xstring` | 0 | 0 | 1 | §18.3.1.57 p.1660, OI 2.1.633 |
| [`x:oddHeader`](#xoddheader) | `s:ST_Xstring` | 0 | 0 | 1 | §18.3.1.58 p.1661, OI 2.1.634 |
| [`x:odxf`](#xodxf) | `x:CT_Dxf` | 0 | 7 | 1 | §18.11.1.6 p.1978 |
| [`x:olapPr`](#xolappr) | `x:CT_OlapPr` | 9 | 0 | 1 | §18.13.5 p.2013, OI 2.1.831 |
| [`x:oldFormula`](#xoldformula) | `x:ST_Formula` | 0 | 0 | 1 | §18.11.1.7 p.1978, OI 2.1.808 |
| [`x:oleItem`](#xoleitem) | `x:CT_OleItem` | 4 | 0 | 1 | §18.14.9 p.2031, OI 2.1.843 |
| [`x:oleItems`](#xoleitems) | `x:CT_OleItems` | 0 | 1 | 1 | §18.14.10 p.2032 |
| [`x:oleLink`](#xolelink) | `x:CT_OleLink` | 2 | 1 | 1 | §18.14.11 p.2032, OI 2.1.844 |
| [`x:oleObject`](#xoleobject) | `x:CT_OleObject` | 7 | 1 | 1 | §18.3.1.59 p.1662, OI 2.1.635 |
| [`x:oleObjects`](#xoleobjects) | `x:CT_OleObjects` | 0 | 1 | 2 | §18.3.1.60 p.1663, OI 2.1.636 |
| [`x:oleSize`](#xolesize) | `x:CT_OleSize` | 1 | 0 | 1 | §18.2.16 p.1577, OI 2.1.577 |
| [`x:outline`](#xoutline) | `x:CT_BooleanProperty` | 1 | 0 | 2 | §18.4.2 p.1731 |
| [`x:outlinePr`](#xoutlinepr) | `x:CT_OutlinePr` | 4 | 0 | 1 | §18.3.1.61 p.1663 |
| [`x:p`](#xp) | `x:CT_MdxMemeberProp` | 2 | 0 | 1 | §18.9.14 p.1823, OI 2.1.733 |
| [`x:page`](#xpage) | `x:CT_PCDSCPage` | 1 | 1 | 1 | §18.10.1.61 p.1891 |
| [`x:pageField`](#xpagefield) | `x:CT_PageField` | 5 | 1 | 1 | §18.10.1.62 p.1892, OI 2.1.779 |
| [`x:pageFields`](#xpagefields) | `x:CT_PageFields` | 1 | 1 | 1 | §18.10.1.63 p.1893, OI 2.1.780 |
| [`x:pageItem`](#xpageitem) | `x:CT_PageItem` | 1 | 0 | 1 | §18.10.1.64 p.1893, OI 2.1.781 |
| [`x:pageMargins`](#xpagemargins) | `x:CT_PageMargins` | 6 | 0 | 4 | §18.3.1.62 p.1665, OI 2.1.637 |
| [`x:pageSetUpPr`](#xpagesetuppr) | `x:CT_PageSetUpPr` | 2 | 0 | 1 | §18.3.1.65 p.1674 |
| [`x:pageSetup`](#xpagesetup) | `x:CT_PageSetup`, `x:CT_CsPageSetup` | 19 | 0 | 4 | §18.3.1.63 p.1665, OI 2.1.638; §18.3.1.64 p.1671, OI 2.1.639 |
| [`x:pages`](#xpages) | `x:CT_Pages` | 1 | 1 | 1 | §18.10.1.65 p.1893 |
| [`x:pane`](#xpane) | `x:CT_Pane` | 5 | 0 | 2 | §18.3.1.66 p.1675, OI 2.1.640 |
| [`x:parameter`](#xparameter) | `x:CT_Parameter` | 10 | 0 | 1 | §18.13.6 p.2016, OI 2.1.832 |
| [`x:parameters`](#xparameters) | `x:CT_Parameters` | 1 | 1 | 1 | §18.13.7 p.2018 |
| [`x:patternFill`](#xpatternfill) | `x:CT_PatternFill` | 1 | 2 | 1 | §18.8.32 p.1802 |
| [`x:phoneticPr`](#xphoneticpr) | `x:CT_PhoneticPr` | 3 | 0 | 4 | §18.4.3 p.1731, OI 2.1.671 |
| [`x:picture`](#xpicture) | `x:CT_SheetBackgroundPicture` | 1 | 0 | 2 | §18.3.1.67 p.1676 |
| [`x:pivotArea`](#xpivotarea) | `x:CT_PivotArea` | 12 | 2 | 6 | §18.3.1.68 p.1676, OI 2.1.641 |
| [`x:pivotAreas`](#xpivotareas) | `x:CT_PivotAreas` | 1 | 1 | 1 | §18.10.1.66 p.1894 |
| [`x:pivotCache`](#xpivotcache) | `x:CT_PivotCache` | 2 | 0 | 1 | §18.2.17 p.1577 |
| [`x:pivotCacheDefinition`](#xpivotcachedefinition) ◆ | `x:CT_PivotCacheDefinition` | 19 | 11 | 0 | §18.10.1.67 p.1894, OI 2.1.782 |
| [`x:pivotCacheRecords`](#xpivotcacherecords) ◆ | `x:CT_PivotCacheRecords` | 1 | 2 | 0 | §18.10.1.68 p.1898 |
| [`x:pivotCaches`](#xpivotcaches) | `x:CT_PivotCaches` | 0 | 1 | 1 | §18.2.18 p.1578 |
| [`x:pivotField`](#xpivotfield) | `x:CT_PivotField` | 48 | 3 | 1 | §18.10.1.69 p.1899, OI 2.1.783 |
| [`x:pivotFields`](#xpivotfields) | `x:CT_PivotFields` | 1 | 1 | 1 | §18.10.1.70 p.1910, OI 2.1.784 |
| [`x:pivotHierarchies`](#xpivothierarchies) | `x:CT_PivotHierarchies` | 1 | 1 | 1 | §18.10.1.71 p.1911, OI 2.1.785 |
| [`x:pivotHierarchy`](#xpivothierarchy) | `x:CT_PivotHierarchy` | 11 | 3 | 1 | §18.10.1.72 p.1912, OI 2.1.786 |
| [`x:pivotSelection`](#xpivotselection) | `x:CT_PivotSelection` | 17 | 1 | 1 | §18.3.1.69 p.1677, OI 2.1.642 |
| [`x:pivotTableDefinition`](#xpivottabledefinition) ◆ | `x:CT_pivotTableDefinition` | 68 | 17 | 0 | §18.10.1.73 p.1914, OI 2.1.787 |
| [`x:pivotTableStyleInfo`](#xpivottablestyleinfo) | `x:CT_PivotTableStyle` | 6 | 0 | 1 | §18.10.1.74 p.1944, OI 2.1.788 |
| [`x:printOptions`](#xprintoptions) | `x:CT_PrintOptions` | 5 | 0 | 3 | §18.3.1.70 p.1681 |
| [`x:protectedRange`](#xprotectedrange) | `x:CT_ProtectedRange` | 8 | 1 | 1 | §18.3.1.71 p.1681, OI 2.1.643 |
| [`x:protectedRanges`](#xprotectedranges) | `x:CT_ProtectedRanges` | 0 | 1 | 1 | §18.3.1.72 p.1685 |
| [`x:protection`](#xprotection) | `x:CT_CellProtection` | 2 | 0 | 4 | §18.8.33 p.1802, OI 2.1.714 |
| [`x:query`](#xquery) | `x:CT_Query` | 1 | 1 | 1 | §18.10.1.75 p.1946, OI 2.1.789 |
| [`x:queryCache`](#xquerycache) | `x:CT_QueryCache` | 1 | 1 | 1 | §18.10.1.76 p.1946 |
| [`x:queryTable`](#xquerytable) ◆ | `x:CT_QueryTable` | 22 | 2 | 0 | §18.12.2 p.1999, OI 2.1.826 |
| [`x:queryTableDeletedFields`](#xquerytabledeletedfields) | `x:CT_QueryTableDeletedFields` | 1 | 1 | 1 | §18.12.3 p.2002 |
| [`x:queryTableField`](#xquerytablefield) | `x:CT_QueryTableField` | 7 | 1 | 1 | §18.12.4 p.2003, OI 2.1.827 |
| [`x:queryTableFields`](#xquerytablefields) | `x:CT_QueryTableFields` | 1 | 1 | 1 | §18.12.5 p.2004 |
| [`x:queryTableRefresh`](#xquerytablerefresh) | `x:CT_QueryTableRefresh` | 7 | 4 | 1 | §18.12.6 p.2004, OI 2.1.828 |
| [`x:r`](#xr) | `x:CT_Record`, `x:CT_RElt` | 0 | 9 | 4 | §18.4.4 p.1732; §18.10.1.77 p.1946, OI 2.1.790 |
| [`x:rFont`](#xrfont) | `x:CT_FontName` | 1 | 0 | 1 | §18.4.5 p.1732, OI 2.1.672 |
| [`x:rPh`](#xrph) | `x:CT_PhoneticRun` | 2 | 1 | 3 | §18.4.6 p.1733, OI 2.1.673 |
| [`x:rPr`](#xrpr) | `x:CT_RPrElt` | 0 | 15 | 1 | §18.4.7 p.1733, OI 2.1.674 |
| [`x:raf`](#xraf) | `x:CT_RevisionAutoFormatting` | 9 | 0 | 1 | §18.11.1.8 p.1978, OI 2.1.809 |
| [`x:rangePr`](#xrangepr) | `x:CT_RangePr` | 8 | 0 | 1 | §18.10.1.78 p.1947, OI 2.1.791 |
| [`x:rangeSet`](#xrangeset) | `x:CT_RangeSet` | 8 | 0 | 1 | §18.10.1.79 p.1948, OI 2.1.792 |
| [`x:rangeSets`](#xrangesets) | `x:CT_RangeSets` | 1 | 1 | 1 | §18.10.1.80 p.1951 |
| [`x:rc`](#xrc) | `x:CT_MetadataRecord` | 2 | 0 | 1 | §18.9.15 p.1823, OI 2.1.734 |
| [`x:rcc`](#xrcc) | `x:CT_RevisionCellChange` | 14 | 5 | 3 | §18.11.1.9 p.1980, OI 2.1.810 |
| [`x:rcft`](#xrcft) | `x:CT_RevisionConflict` | 4 | 0 | 1 | §18.11.1.10 p.1982, OI 2.1.811 |
| [`x:rcmt`](#xrcmt) | `x:CT_RevisionComment` | 11 | 0 | 1 | §18.11.1.11 p.1982, OI 2.1.812 |
| [`x:rcv`](#xrcv) | `x:CT_RevisionCustomView` | 2 | 0 | 1 | §18.11.1.12 p.1984 |
| [`x:rdn`](#xrdn) | `x:CT_RevisionDefinedName` | 24 | 3 | 1 | §18.11.1.13 p.1984, OI 2.1.813 |
| [`x:reference`](#xreference) | `x:CT_PivotAreaReference` | 17 | 2 | 1 | §18.10.2.1 p.1965, OI 2.1.803 |
| [`x:references`](#xreferences) | `x:CT_PivotAreaReferences` | 1 | 1 | 1 | §18.10.2.2 p.1969 |
| [`x:reviewed`](#xreviewed) | `x:CT_Reviewed` | 1 | 0 | 1 | §18.11.1.14 p.1987, OI 2.1.814 |
| [`x:reviewedList`](#xreviewedlist) | `x:CT_ReviewedRevisions` | 1 | 1 | 1 | §18.11.1.15 p.1988 |
| [`x:revisions`](#xrevisions) ◆ | `x:CT_Revisions` | 0 | 12 | 0 | §18.11.1.16 p.1988 |
| [`x:rfmt`](#xrfmt) | `x:CT_RevisionFormatting` | 6 | 2 | 3 | §18.11.1.17 p.1989, OI 2.1.815 |
| [`x:rgbColor`](#xrgbcolor) | `x:CT_RgbColor` | 1 | 0 | 1 | §18.8.34 p.1803 |
| [`x:right`](#xright) | `x:CT_BorderPr` | 1 | 1 | 1 | Part 4 §15.5.2 p.236 |
| [`x:ris`](#xris) | `x:CT_RevisionInsertSheet` | 6 | 0 | 1 | §18.11.1.18 p.1990, OI 2.1.816 |
| [`x:rm`](#xrm) | `x:CT_RevisionMove` | 7 | 3 | 1 | §18.11.1.19 p.1991, OI 2.1.817 |
| [`x:row`](#xrow) | `x:CT_Row`, `x:CT_ExternalRow` | 12 | 3 | 1 | §18.3.1.73 p.1685, OI 2.1.644; §18.14.12 p.2033, OI 2.1.845 |
| [`x:rowBreaks`](#xrowbreaks) | `x:CT_PageBreak` | 2 | 1 | 2 | §18.3.1.74 p.1691, OI 2.1.645 |
| [`x:rowFields`](#xrowfields) | `x:CT_RowFields` | 1 | 1 | 1 | §18.10.1.81 p.1952 |
| [`x:rowHierarchiesUsage`](#xrowhierarchiesusage) | `x:CT_RowHierarchiesUsage` | 1 | 1 | 1 | §18.10.1.82 p.1953 |
| [`x:rowHierarchyUsage`](#xrowhierarchyusage) | `x:CT_HierarchyUsage` | 1 | 0 | 1 | §18.10.1.83 p.1953, OI 2.1.793 |
| [`x:rowItems`](#xrowitems) | `x:CT_rowItems` | 1 | 1 | 1 | §18.10.1.84 p.1953 |
| [`x:rqt`](#xrqt) | `x:CT_RevisionQueryTableField` | 3 | 0 | 1 | §18.11.1.20 p.1992, OI 2.1.818 |
| [`x:rrc`](#xrrc) | `x:CT_RevisionRowColumn` | 8 | 3 | 1 | §18.11.1.21 p.1992, OI 2.1.819 |
| [`x:rsnm`](#xrsnm) | `x:CT_RevisionSheetRename` | 6 | 1 | 1 | §18.11.1.22 p.1994, OI 2.1.820 |
| [`x:s`](#xs) | `x:CT_XStringElement`, `x:CT_String` | 12 | 2 | 6 | §18.10.1.85 p.1955, OI 2.1.794; §18.13.8 p.2019, OI 2.1.833 |
| [`x:scenario`](#xscenario) | `x:CT_Scenario` | 6 | 1 | 1 | §18.3.1.75 p.1691, OI 2.1.646 |
| [`x:scenarios`](#xscenarios) | `x:CT_Scenarios` | 3 | 1 | 1 | §18.3.1.76 p.1692, OI 2.1.647 |
| [`x:scheme`](#xscheme) | `x:CT_FontScheme` | 1 | 0 | 2 | §18.8.35 p.1803 |
| [`x:securityDescriptor`](#xsecuritydescriptor) | `xsd:string` | 0 | 0 | 1 | §18.3.1.77 p.1693, OI 2.1.648 |
| [`x:selection`](#xselection) | `x:CT_Selection` | 4 | 0 | 2 | §18.3.1.78 p.1694, OI 2.1.649 |
| [`x:serverFormat`](#xserverformat) | `x:CT_ServerFormat` | 2 | 0 | 1 | §18.10.1.86 p.1957, OI 2.1.795 |
| [`x:serverFormats`](#xserverformats) | `x:CT_ServerFormats` | 1 | 1 | 1 | §18.10.1.87 p.1957 |
| [`x:set`](#xset) | `x:CT_Set` | 5 | 2 | 1 | §18.10.1.88 p.1957, OI 2.1.796 |
| [`x:sets`](#xsets) | `x:CT_Sets` | 1 | 1 | 1 | §18.10.1.89 p.1958 |
| [`x:shadow`](#xshadow) | `x:CT_BooleanProperty` | 1 | 0 | 2 | §18.8.36 p.1804 |
| [`x:sharedItems`](#xshareditems) | `x:CT_SharedItems` | 14 | 6 | 1 | §18.10.1.90 p.1959, OI 2.1.797 |
| [`x:sheet`](#xsheet) | `x:CT_Sheet` | 4 | 0 | 1 | §18.2.19 p.1578, OI 2.1.578 |
| [`x:sheetCalcPr`](#xsheetcalcpr) | `x:CT_SheetCalcPr` | 1 | 0 | 1 | §18.3.1.79 p.1694 |
| [`x:sheetData`](#xsheetdata) | `x:CT_SheetData`, `x:CT_ExternalSheetData` | 2 | 1 | 2 | §18.3.1.80 p.1695; §18.14.13 p.2033, OI 2.1.846 |
| [`x:sheetDataSet`](#xsheetdataset) | `x:CT_ExternalSheetDataSet` | 0 | 1 | 1 | §18.14.14 p.2034 |
| [`x:sheetFormatPr`](#xsheetformatpr) | `x:CT_SheetFormatPr` | 9 | 0 | 2 | §18.3.1.81 p.1695, OI 2.1.650 |
| [`x:sheetId`](#xsheetid) | `x:CT_SheetId` | 1 | 0 | 1 | §18.11.1.23 p.1995, OI 2.1.821 |
| [`x:sheetIdMap`](#xsheetidmap) | `x:CT_SheetIdMap` | 1 | 1 | 1 | §18.11.1.24 p.1995, OI 2.1.822 |
| [`x:sheetName`](#xsheetname) | `x:CT_ExternalSheetName` | 1 | 0 | 1 | §18.14.15 p.2034, OI 2.1.847 |
| [`x:sheetNames`](#xsheetnames) | `x:CT_ExternalSheetNames` | 0 | 1 | 1 | §18.14.16 p.2034, OI 2.1.848 |
| [`x:sheetPr`](#xsheetpr) | `x:CT_SheetPr`, `x:CT_ChartsheetPr` | 9 | 3 | 3 | §18.3.1.82 p.1696, OI 2.1.651; §18.3.1.83 p.1698, OI 2.1.652 |
| [`x:sheetProtection`](#xsheetprotection) | `x:CT_SheetProtection`, `x:CT_ChartsheetProtection` | 22 | 0 | 3 | §18.3.1.84 p.1698, OI 2.1.653; §18.3.1.85 p.1701, OI 2.1.654 |
| [`x:sheetView`](#xsheetview) | `x:CT_SheetView`, `x:CT_ChartsheetView` | 20 | 4 | 1 | §18.3.1.86 p.1707; §18.3.1.87 p.1708, OI 2.1.655 |
| [`x:sheetViews`](#xsheetviews) | `x:CT_SheetViews`, `x:CT_ChartsheetViews` | 0 | 2 | 3 | §18.3.1.88 p.1712, OI 2.1.656; §18.3.1.89 p.1712, OI 2.1.657 |
| [`x:sheets`](#xsheets) | `x:CT_Sheets` | 0 | 1 | 1 | §18.2.20 p.1579, OI 2.1.579 |
| [`x:si`](#xsi) | `x:CT_Rst` | 0 | 4 | 1 | §18.4.8 p.1734, OI 2.1.675 |
| [`x:singleXmlCell`](#xsinglexmlcell) | `x:CT_SingleXmlCell` | 3 | 2 | 1 | §18.5.2.1 p.1749, OI 2.1.685 |
| [`x:singleXmlCells`](#xsinglexmlcells) ◆ | `x:CT_SingleXmlCells` | 0 | 1 | 0 | §18.5.2.2 p.1750 |
| [`x:smartTagPr`](#xsmarttagpr) | `x:CT_SmartTagPr` | 2 | 0 | 1 | §18.2.21 p.1579 |
| [`x:smartTagType`](#xsmarttagtype) | `x:CT_SmartTagType` | 3 | 0 | 1 | §18.2.22 p.1580, OI 2.1.580 |
| [`x:smartTagTypes`](#xsmarttagtypes) | `x:CT_SmartTagTypes` | 0 | 1 | 1 | §18.2.23 p.1581, OI 2.1.581 |
| [`x:smartTags`](#xsmarttags) | `x:CT_SmartTags` | 0 | 1 | 1 | §18.3.1.90 p.1712 |
| [`x:sortByTuple`](#xsortbytuple) | `x:CT_Tuples` | 1 | 1 | 1 | §18.10.1.91 p.1962, OI 2.1.798 |
| [`x:sortCondition`](#xsortcondition) | `x:CT_SortCondition` | 7 | 0 | 1 | §18.3.1.91 p.1713, OI 2.1.658 |
| [`x:sortState`](#xsortstate) | `x:CT_SortState` | 4 | 2 | 4 | §18.3.1.92 p.1714 |
| [`x:sst`](#xsst) ◆ | `x:CT_Sst` | 2 | 2 | 0 | §18.4.9 p.1734, OI 2.1.676 |
| [`x:start`](#xstart) | `x:CT_BorderPr` | 1 | 1 | 1 | §18.8.37 p.1804, OI 2.1.715 |
| [`x:stop`](#xstop) | `x:CT_GradientStop` | 1 | 1 | 1 | §18.8.38 p.1804, OI 2.1.716 |
| [`x:stp`](#xstp) | `s:ST_Xstring` | 0 | 0 | 1 | §18.15.2 p.2039, OI 2.1.853 |
| [`x:strike`](#xstrike) | `x:CT_BooleanProperty` | 1 | 0 | 2 | §18.4.10 p.1734 |
| [`x:styleSheet`](#xstylesheet) ◆ | `x:CT_Stylesheet` | 0 | 11 | 0 | §18.8.39 p.1805, OI 2.1.717 |
| [`x:sz`](#xsz) | `x:CT_FontSize` | 1 | 0 | 2 | §18.4.11 p.1735, OI 2.1.677 |
| [`x:t`](#xt) | `s:ST_Xstring`, `x:CT_MdxTuple` | 10 | 1 | 6 | §18.4.12 p.1735, OI 2.1.678; §18.9.16 p.1824, OI 2.1.735 |
| [`x:tabColor`](#xtabcolor) | `x:CT_Color` | 5 | 0 | 1 | §18.3.1.93 p.1715, OI 2.1.659 |
| [`x:table`](#xtable) ◆ | `x:CT_Table` | 22 | 5 | 0 | §18.5.1.2 p.1738, OI 2.1.680 |
| [`x:tableColumn`](#xtablecolumn) | `x:CT_TableColumn` | 12 | 4 | 1 | §18.5.1.3 p.1743, OI 2.1.681 |
| [`x:tableColumns`](#xtablecolumns) | `x:CT_TableColumns` | 1 | 1 | 1 | §18.5.1.4 p.1745 |
| [`x:tablePart`](#xtablepart) | `x:CT_TablePart` | 1 | 0 | 1 | §18.3.1.94 p.1716 |
| [`x:tableParts`](#xtableparts) | `x:CT_TableParts` | 1 | 1 | 1 | §18.3.1.95 p.1717 |
| [`x:tableStyle`](#xtablestyle) | `x:CT_TableStyle` | 4 | 1 | 1 | §18.8.40 p.1805, OI 2.1.718 |
| [`x:tableStyleElement`](#xtablestyleelement) | `x:CT_TableStyleElement` | 3 | 0 | 1 | §18.8.41 p.1806, OI 2.1.719 |
| [`x:tableStyleInfo`](#xtablestyleinfo) | `x:CT_TableStyleInfo` | 5 | 0 | 1 | §18.5.1.5 p.1746, OI 2.1.682 |
| [`x:tableStyles`](#xtablestyles) | `x:CT_TableStyles` | 3 | 1 | 1 | §18.8.42 p.1808, OI 2.1.720 |
| [`x:tables`](#xtables) | `x:CT_Tables` | 1 | 3 | 1 | §18.13.9 p.2019 |
| [`x:text`](#xtext) | `x:CT_Rst` | 0 | 4 | 1 | §18.7.7 p.1762, OI 2.1.692 |
| [`x:textField`](#xtextfield) | `x:CT_TextField` | 2 | 0 | 1 | §18.13.10 p.2019, OI 2.1.834 |
| [`x:textFields`](#xtextfields) | `x:CT_TextFields` | 1 | 1 | 1 | §18.13.11 p.2020, OI 2.1.835 |
| [`x:textPr`](#xtextpr) | `x:CT_TextPr` | 16 | 1 | 1 | §18.13.12 p.2020, OI 2.1.836 |
| [`x:top`](#xtop) | `x:CT_BorderPr` | 1 | 1 | 1 | §18.8.43 p.1809 |
| [`x:top10`](#xtop10) | `x:CT_Top10` | 4 | 0 | 1 | §18.3.2.10 p.1727, OI 2.1.670 |
| [`x:totalsRowFormula`](#xtotalsrowformula) | `x:CT_TableFormula` | 1 | 0 | 1 | §18.5.1.6 p.1747, OI 2.1.683 |
| [`x:tp`](#xtp) | `x:CT_VolTopic` | 1 | 3 | 1 | §18.15.3 p.2039, OI 2.1.854 |
| [`x:tpl`](#xtpl) | `x:CT_Tuple` | 3 | 0 | 2 | §18.10.1.92 p.1963, OI 2.1.799 |
| [`x:tpls`](#xtpls) | `x:CT_Tuples` | 1 | 1 | 6 | §18.10.1.93 p.1963 |
| [`x:tr`](#xtr) | `x:CT_VolTopicRef` | 2 | 0 | 1 | §18.15.4 p.2040, OI 2.1.855 |
| [`x:tupleCache`](#xtuplecache) | `x:CT_TupleCache` | 0 | 5 | 1 | §18.10.1.94 p.1963 |
| [`x:u`](#xu) | `x:CT_UnderlineProperty` | 1 | 0 | 2 | §18.4.13 p.1736 |
| [`x:undo`](#xundo) | `x:CT_UndoInfo` | 11 | 0 | 2 | §18.11.1.25 p.1995, OI 2.1.823 |
| [`x:userInfo`](#xuserinfo) | `x:CT_SharedUser` | 4 | 1 | 1 | §18.11.2.1 p.1997 |
| [`x:users`](#xusers) ◆ | `x:CT_Users` | 1 | 1 | 0 | §18.11.2.2 p.1997, OI 2.1.824 |
| [`x:v`](#xv) | `s:ST_Xstring` | 0 | 0 | 5 | §18.3.1.96 p.1717, OI 2.1.660 |
| [`x:val`](#xval) | `s:ST_Xstring` | 0 | 0 | 1 | §18.14.17 p.2034, OI 2.1.849 |
| [`x:value`](#xvalue) | `x:CT_DdeValue` | 1 | 1 | 1 | §18.14.18 p.2035, OI 2.1.850 |
| [`x:valueMetadata`](#xvaluemetadata) | `x:CT_MetadataBlocks` | 1 | 1 | 1 | §18.9.17 p.1825, OI 2.1.736 |
| [`x:values`](#xvalues) | `x:CT_DdeValues` | 2 | 1 | 1 | §18.14.19 p.2036, OI 2.1.851 |
| [`x:vertAlign`](#xvertalign) | `x:CT_VerticalAlignFontProperty` | 1 | 0 | 2 | §18.4.14 p.1736 |
| [`x:vertical`](#xvertical) | `x:CT_BorderPr` | 1 | 1 | 1 | §18.8.44 p.1809 |
| [`x:volType`](#xvoltype) | `x:CT_VolType` | 1 | 1 | 1 | §18.15.5 p.2040 |
| [`x:volTypes`](#xvoltypes) ◆ | `x:CT_VolTypes` | 0 | 2 | 0 | §18.15.6 p.2041 |
| [`x:webPr`](#xwebpr) | `x:CT_WebPr` | 13 | 1 | 1 | §18.13.13 p.2023, OI 2.1.837 |
| [`x:webPublishItem`](#xwebpublishitem) | `x:CT_WebPublishItem` | 8 | 0 | 1 | §18.3.1.97 p.1718, OI 2.1.661 |
| [`x:webPublishItems`](#xwebpublishitems) | `x:CT_WebPublishItems` | 1 | 1 | 2 | §18.3.1.98 p.1719 |
| [`x:webPublishObject`](#xwebpublishobject) | `x:CT_WebPublishObject` | 6 | 0 | 1 | §18.2.25 p.1583, OI 2.1.583 |
| [`x:webPublishObjects`](#xwebpublishobjects) | `x:CT_WebPublishObjects` | 1 | 1 | 1 | §18.2.26 p.1583 |
| [`x:webPublishing`](#xwebpublishing) | `x:CT_WebPublishing` | 9 | 0 | 1 | §18.2.24 p.1581, OI 2.1.582 |
| [`x:workbook`](#xworkbook) ◆ | `x:CT_Workbook` | 1 | 19 | 0 | §18.2.27 p.1584, OI 2.1.584 |
| [`x:workbookPr`](#xworkbookpr) | `x:CT_WorkbookPr` | 18 | 0 | 1 | §18.2.28 p.1586, OI 2.1.585 |
| [`x:workbookProtection`](#xworkbookprotection) | `x:CT_WorkbookProtection` | 15 | 0 | 1 | §18.2.29 p.1590, OI 2.1.586 |
| [`x:workbookView`](#xworkbookview) | `x:CT_BookView` | 13 | 1 | 1 | §18.2.30 p.1597, OI 2.1.587 |
| [`x:worksheet`](#xworksheet) ◆ | `x:CT_Worksheet` | 0 | 39 | 0 | §18.3.1.99 p.1720, OI 2.1.662 |
| [`x:worksheetSource`](#xworksheetsource) | `x:CT_WorksheetSource` | 4 | 0 | 1 | §18.10.1.95 p.1963, OI 2.1.800 |
| [`x:x`](#xx) | `x:CT_Index`, `x:CT_X` | 1 | 0 | 11 | §18.10.1.96 p.1964, OI 2.1.801; §18.10.1.97 p.1964, OI 2.1.802 |
| [`x:xf`](#xxf) | `x:CT_Xf` | 13 | 3 | 2 | §18.8.45 p.1809, OI 2.1.721 |
| [`x:xmlCellPr`](#xxmlcellpr) | `x:CT_XmlCellPr` | 2 | 2 | 1 | §18.5.2.3 p.1750, OI 2.1.686 |
| [`x:xmlColumnPr`](#xxmlcolumnpr) | `x:CT_XmlColumnPr` | 4 | 1 | 1 | §18.5.1.7 p.1747, OI 2.1.684 |
| [`x:xmlPr`](#xxmlpr) | `x:CT_XmlPr` | 3 | 1 | 1 | §18.5.2.4 p.1751, OI 2.1.687 |

◆ declared globally (can be the root element of a part)

## Simple types

| Simple type | Definition | Values | Specification |
|---|---|---|---|
| `x:ST_Axis` | restriction of xsd:string | `axisRow`, `axisCol`, `axisPage`, `axisValues` | §18.18.1 p.2445 |
| `x:ST_BorderId` | restriction of xsd:unsignedInt |  | §18.18.2 p.2446, OI 2.1.1077 |
| `x:ST_BorderStyle` | restriction of xsd:string | `none`, `thin`, `medium`, `dashed`, `dotted`, `thick`, `double`, `hair`, `mediumDashed`, `dashDot`, `mediumDashDot`, `dashDotDot` … (+2) | §18.18.3 p.2446 |
| `x:ST_CalcMode` | restriction of xsd:string | `manual`, `auto`, `autoNoTable` | §18.18.4 p.2449 |
| `x:ST_CellComments` | restriction of xsd:string | `none`, `asDisplayed`, `atEnd` | §18.18.5 p.2449 |
| `x:ST_CellFormulaType` | restriction of xsd:string | `normal`, `array`, `dataTable`, `shared` | §18.18.6 p.2450, OI 2.1.1078 |
| `x:ST_CellRef` | restriction of xsd:string |  | §18.18.7 p.2450, OI 2.1.1079 |
| `x:ST_CellSpan` | restriction of xsd:string |  | §18.18.8 p.2450, OI 2.1.1080 |
| `x:ST_CellSpans` | list of x:ST_CellSpan |  | §18.18.9 p.2451 |
| `x:ST_CellStyleXfId` | restriction of xsd:unsignedInt |  | §18.18.10 p.2451 |
| `x:ST_CellType` | restriction of xsd:string | `b`, `n`, `e`, `s`, `str`, `inlineStr` | §18.18.11 p.2451 |
| `x:ST_CfType` | restriction of xsd:string | `expression`, `cellIs`, `colorScale`, `dataBar`, `iconSet`, `top10`, `uniqueValues`, `duplicateValues`, `containsText`, `notContainsText`, `beginsWith`, `endsWith` … (+6) | §18.18.12 p.2451 |
| `x:ST_CfvoType` | restriction of xsd:string | `num`, `percent`, `max`, `min`, `formula`, `percentile` | §18.18.13 p.2453 |
| `x:ST_Comments` | restriction of xsd:string | `commNone`, `commIndicator`, `commIndAndComment` | §18.18.14 p.2454 |
| `x:ST_ConditionalFormattingOperator` | restriction of xsd:string | `lessThan`, `lessThanOrEqual`, `equal`, `notEqual`, `greaterThanOrEqual`, `greaterThan`, `between`, `notBetween`, `containsText`, `notContains`, `beginsWith`, `endsWith` | §18.18.15 p.2454 |
| `x:ST_CredMethod` | restriction of xsd:string | `integrated`, `none`, `stored`, `prompt` | §18.18.16 p.2455, OI 2.1.1081 |
| `x:ST_DataConsolidateFunction` | restriction of xsd:string | `average`, `count`, `countNums`, `max`, `min`, `product`, `stdDev`, `stdDevp`, `sum`, `var`, `varp` | §18.18.17 p.2455 |
| `x:ST_DataValidationErrorStyle` | restriction of xsd:string | `stop`, `warning`, `information` | §18.18.18 p.2456 |
| `x:ST_DataValidationImeMode` | restriction of xsd:string | `noControl`, `off`, `on`, `disabled`, `hiragana`, `fullKatakana`, `halfKatakana`, `fullAlpha`, `halfAlpha`, `fullHangul`, `halfHangul` | §18.18.19 p.2457 |
| `x:ST_DataValidationOperator` | restriction of xsd:string | `between`, `notBetween`, `equal`, `notEqual`, `lessThan`, `lessThanOrEqual`, `greaterThan`, `greaterThanOrEqual` | §18.18.20 p.2458 |
| `x:ST_DataValidationType` | restriction of xsd:string | `none`, `whole`, `decimal`, `list`, `date`, `time`, `textLength`, `custom` | §18.18.21 p.2458 |
| `x:ST_DateTimeGrouping` | restriction of xsd:string | `year`, `month`, `day`, `hour`, `minute`, `second` | §18.18.22 p.2459 |
| `x:ST_DdeValueType` | restriction of xsd:string | `nil`, `b`, `n`, `e`, `str` | §18.18.23 p.2460 |
| `x:ST_DvAspect` | restriction of xsd:string | `DVASPECT_CONTENT`, `DVASPECT_ICON` | §18.18.24 p.2460 |
| `x:ST_DxfId` | restriction of xsd:unsignedInt |  | §18.18.25 p.2460, OI 2.1.1082 |
| `x:ST_DynamicFilterType` | restriction of xsd:string | `null`, `aboveAverage`, `belowAverage`, `tomorrow`, `today`, `yesterday`, `nextWeek`, `thisWeek`, `lastWeek`, `nextMonth`, `thisMonth`, `lastMonth` … (+23) | §18.18.26 p.2461 |
| `x:ST_ExternalConnectionType` | restriction of xsd:string | `general`, `text`, `MDY`, `DMY`, `YMD`, `MYD`, `DYM`, `YDM`, `skip`, `EMD` | §18.18.27 p.2462 |
| `x:ST_FieldSortType` | restriction of xsd:string | `manual`, `ascending`, `descending` | §18.18.28 p.2463 |
| `x:ST_FileType` | restriction of xsd:string | `mac`, `win`, `dos`, `lin`, `other` | §18.18.29 p.2463, OI 2.1.1083 |
| `x:ST_FillId` | restriction of xsd:unsignedInt |  | §18.18.30 p.2464, OI 2.1.1084 |
| `x:ST_FilterOperator` | restriction of xsd:string | `equal`, `lessThan`, `lessThanOrEqual`, `notEqual`, `greaterThanOrEqual`, `greaterThan` | §18.18.31 p.2464 |
| `x:ST_FontFamily` | restriction of xsd:integer | minInclusive `0`, maxInclusive `14` | §18.18.94 p.2525 |
| `x:ST_FontId` | restriction of xsd:unsignedInt |  | §18.18.32 p.2464 |
| `x:ST_FontScheme` | restriction of xsd:string | `none`, `major`, `minor` | §18.18.33 p.2465 |
| `x:ST_FormatAction` | restriction of xsd:string | `blank`, `formatting`, `drill`, `formula` | §18.18.34 p.2465 |
| `x:ST_Formula` | restriction of s:ST_Xstring |  | §18.18.35 p.2465, OI 2.1.1085 |
| `x:ST_FormulaExpression` | restriction of xsd:string | `ref`, `refError`, `area`, `areaError`, `computedArea` | §18.18.36 p.2466 |
| `x:ST_GradientType` | restriction of xsd:string | `linear`, `path` | §18.18.37 p.2466 |
| `x:ST_GroupBy` | restriction of xsd:string | `range`, `seconds`, `minutes`, `hours`, `days`, `months`, `quarters`, `years` | §18.18.38 p.2466 |
| `x:ST_GrowShrinkType` | restriction of xsd:string | `insertDelete`, `insertClear`, `overwriteClear` | §18.18.39 p.2467 |
| `x:ST_HorizontalAlignment` | restriction of xsd:string | `general`, `left`, `center`, `right`, `fill`, `justify`, `centerContinuous`, `distributed` | §18.18.40 p.2467 |
| `x:ST_HtmlFmt` | restriction of xsd:string | `none`, `rtf`, `all` | §18.18.41 p.2471 |
| `x:ST_IconSetType` | restriction of xsd:string | `3Arrows`, `3ArrowsGray`, `3Flags`, `3TrafficLights1`, `3TrafficLights2`, `3Signs`, `3Symbols`, `3Symbols2`, `4Arrows`, `4ArrowsGray`, `4RedToBlack`, `4Rating` … (+5) | §18.18.42 p.2472, OI 2.1.1086 |
| `x:ST_ItemType` | restriction of xsd:string | `data`, `default`, `sum`, `countA`, `avg`, `max`, `min`, `product`, `count`, `stdDev`, `stdDevP`, `var` … (+3) | §18.18.43 p.2474 |
| `x:ST_MdxFunctionType` | restriction of xsd:string | `m`, `v`, `s`, `c`, `r`, `p`, `k` | §18.18.44 p.2475 |
| `x:ST_MdxKPIProperty` | restriction of xsd:string | `v`, `g`, `s`, `t`, `w`, `m` | §18.18.45 p.2476 |
| `x:ST_MdxSetOrder` | restriction of xsd:string | `u`, `a`, `d`, `aa`, `ad`, `na`, `nd` | §18.18.46 p.2476 |
| `x:ST_NumFmtId` | restriction of xsd:unsignedInt |  | §18.18.47 p.2477, OI 2.1.1087 |
| `x:ST_Objects` | restriction of xsd:string | `all`, `placeholders`, `none` | §18.18.48 p.2477 |
| `x:ST_OleUpdate` | restriction of xsd:string | `OLEUPDATE_ALWAYS`, `OLEUPDATE_ONCALL` | §18.18.49 p.2477 |
| `x:ST_Orientation` | restriction of xsd:string | `default`, `portrait`, `landscape` | §18.18.50 p.2478 |
| `x:ST_PageOrder` | restriction of xsd:string | `downThenOver`, `overThenDown` | §18.18.51 p.2478 |
| `x:ST_Pane` | restriction of xsd:string | `bottomRight`, `topRight`, `bottomLeft`, `topLeft` | §18.18.52 p.2478 |
| `x:ST_PaneState` | restriction of xsd:string | `split`, `frozen`, `frozenSplit` | §18.18.53 p.2479 |
| `x:ST_ParameterType` | restriction of xsd:string | `prompt`, `value`, `cell` | §18.18.54 p.2480 |
| `x:ST_PatternType` | restriction of xsd:string | `none`, `solid`, `mediumGray`, `darkGray`, `lightGray`, `darkHorizontal`, `darkVertical`, `darkDown`, `darkUp`, `darkGrid`, `darkTrellis`, `lightHorizontal` … (+7) | §18.18.55 p.2480 |
| `x:ST_PhoneticAlignment` | restriction of xsd:string | `noControl`, `left`, `center`, `distributed` | §18.18.56 p.2485 |
| `x:ST_PhoneticType` | restriction of xsd:string | `halfwidthKatakana`, `fullwidthKatakana`, `Hiragana`, `noConversion` | §18.18.57 p.2486, OI 2.1.1088 |
| `x:ST_PivotAreaType` | restriction of xsd:string | `none`, `normal`, `data`, `all`, `origin`, `button`, `topEnd`, `topRight` | §18.18.58 p.2486 |
| `x:ST_PivotFilterType` | restriction of xsd:string | `unknown`, `count`, `percent`, `sum`, `captionEqual`, `captionNotEqual`, `captionBeginsWith`, `captionNotBeginsWith`, `captionEndsWith`, `captionNotEndsWith`, `captionContains`, `captionNotContains` … (+54) | §18.18.59 p.2487, OI 2.1.1089 |
| `x:ST_PrintError` | restriction of xsd:string | `displayed`, `blank`, `dash`, `NA` | §18.18.60 p.2489 |
| `x:ST_Qualifier` | restriction of xsd:string | `doubleQuote`, `singleQuote`, `none` | §18.18.61 p.2490 |
| `x:ST_Ref` | restriction of xsd:string |  | §18.18.62 p.2490, OI 2.1.1090 |
| `x:ST_RefA` | restriction of xsd:string |  | §18.18.63 p.2490, OI 2.1.1091 |
| `x:ST_RefMode` | restriction of xsd:string | `A1`, `R1C1` | §18.18.64 p.2491 |
| `x:ST_RevisionAction` | restriction of xsd:string | `add`, `delete` | §18.18.65 p.2491 |
| `x:ST_Scope` | restriction of xsd:string | `selection`, `data`, `field` | §18.18.67 p.2492 |
| `x:ST_SheetState` | restriction of xsd:string | `visible`, `hidden`, `veryHidden` | §18.18.68 p.2492 |
| `x:ST_SheetViewType` | restriction of xsd:string | `normal`, `pageBreakPreview`, `pageLayout` | §18.18.69 p.2493 |
| `x:ST_ShowDataAs` | restriction of xsd:string | `normal`, `difference`, `percent`, `percentDiff`, `runTotal`, `percentOfRow`, `percentOfCol`, `percentOfTotal`, `index` | §18.18.70 p.2493 |
| `x:ST_SmartTagShow` | restriction of xsd:string | `all`, `none`, `noIndicator` | §18.18.71 p.2494 |
| `x:ST_SortBy` | restriction of xsd:string | `value`, `cellColor`, `fontColor`, `icon` | §18.18.72 p.2495 |
| `x:ST_SortMethod` | restriction of xsd:string | `stroke`, `pinYin`, `none` | §18.18.73 p.2495 |
| `x:ST_SortType` | restriction of xsd:string | `none`, `ascending`, `descending`, `ascendingAlpha`, `descendingAlpha`, `ascendingNatural`, `descendingNatural` | §18.18.74 p.2495 |
| `x:ST_SourceType` | restriction of xsd:string | `worksheet`, `external`, `consolidation`, `scenario` | §18.18.75 p.2496 |
| `x:ST_Sqref` | list of x:ST_Ref |  | §18.18.76 p.2497, OI 2.1.1092 |
| `x:ST_TableStyleType` | restriction of xsd:string | `wholeTable`, `headerRow`, `totalRow`, `firstColumn`, `lastColumn`, `firstRowStripe`, `secondRowStripe`, `firstColumnStripe`, `secondColumnStripe`, `firstHeaderCell`, `lastHeaderCell`, `firstTotalCell` … (+16) | §18.18.77 p.2497, OI 2.1.1093 |
| `x:ST_TableType` | restriction of xsd:string | `worksheet`, `xml`, `queryTable` | §18.18.78 p.2515 |
| `x:ST_TargetScreenSize` | restriction of xsd:string | `544x376`, `640x480`, `720x512`, `800x600`, `1024x768`, `1152x882`, `1152x900`, `1280x1024`, `1600x1200`, `1800x1440`, `1920x1200` | §18.18.79 p.2515 |
| `x:ST_TextHAlign` | restriction of xsd:string | `left`, `center`, `right`, `justify`, `distributed` | §18.18.80 p.2516 |
| `x:ST_TextRotation` | union of restriction of xsd:nonNegativeInteger, restriction of xsd:nonNegativeInteger | `255` |  |
| `x:ST_TextVAlign` | restriction of xsd:string | `top`, `center`, `bottom`, `justify`, `distributed` | §18.18.81 p.2516 |
| `x:ST_TimePeriod` | restriction of xsd:string | `today`, `yesterday`, `tomorrow`, `last7Days`, `thisMonth`, `lastMonth`, `nextMonth`, `thisWeek`, `lastWeek`, `nextWeek` | §18.18.82 p.2517 |
| `x:ST_TotalsRowFunction` | restriction of xsd:string | `none`, `sum`, `min`, `max`, `average`, `count`, `countNums`, `stdDev`, `var`, `custom` | §18.18.83 p.2517, OI 2.1.1094 |
| `x:ST_Type` | restriction of xsd:string | `none`, `all`, `row`, `column` | §18.18.84 p.2518 |
| `x:ST_UnderlineValues` | restriction of xsd:string | `single`, `double`, `singleAccounting`, `doubleAccounting`, `none` | §18.18.85 p.2518 |
| `x:ST_UnsignedIntHex` | restriction of xsd:hexBinary | length `4` | §18.18.86 p.2519 |
| `x:ST_UnsignedShortHex` | restriction of xsd:hexBinary | length `2` | Part 4 §15.8.2 p.237 |
| `x:ST_UpdateLinks` | restriction of xsd:string | `userSet`, `never`, `always` | §18.18.87 p.2519, OI 2.1.1095 |
| `x:ST_VerticalAlignment` | restriction of xsd:string | `top`, `center`, `bottom`, `justify`, `distributed` | §18.18.88 p.2520 |
| `x:ST_Visibility` | restriction of xsd:string | `visible`, `hidden`, `veryHidden` | §18.18.89 p.2523, OI 2.1.1096 |
| `x:ST_VolDepType` | restriction of xsd:string | `realTimeData`, `olapFunctions` | §18.18.90 p.2523 |
| `x:ST_VolValueType` | restriction of xsd:string | `b`, `n`, `e`, `s` | §18.18.91 p.2524 |
| `x:ST_WebSourceType` | restriction of xsd:string | `sheet`, `printArea`, `autoFilter`, `range`, `chart`, `pivotTable`, `query`, `label` | §18.18.92 p.2524, OI 2.1.1097 |
| `x:ST_XmlDataType` | restriction of xsd:string |  | §18.18.93 p.2525 |
| `x:ST_rwColActionType` | restriction of xsd:string | `insertRow`, `deleteRow`, `insertCol`, `deleteCol` | §18.18.66 p.2492 |

## Element details

### x:DataBinding

- Type: `x:CT_DataBinding`
- Attributes: `ConnectionID`, `DataBindingLoadMode`!, `DataBindingName`, `FileBinding`, `FileBindingName`
- Children: `any(##any)`
- Parents: `x:Map`
- Specification: §18.16.1 p.2044, OI 2.1.856

### x:Map

- Type: `x:CT_Map`
- Attributes: `Append`!, `AutoFit`!, `ID`!, `Name`!, `PreserveFormat`!, `PreserveSortAFLayout`!, `RootElement`!, `SchemaID`!, `ShowImportExportValidationErrors`!
- Children: `x:DataBinding`
- Parents: `x:MapInfo`
- Specification: §18.16.2 p.2046, OI 2.1.857

### x:MapInfo

- Type: `x:CT_MapInfo`
- Attributes: `SelectionNamespaces`!
- Children: `x:Schema`, `x:Map`
- Specification: §18.16.3 p.2047

### x:Schema

- Type: `x:CT_Schema`
- Attributes: `ID`!, `Namespace`, `SchemaLanguage`, `SchemaRef`
- Children: `any(##any)`
- Parents: `x:MapInfo`
- Specification: §18.16.4 p.2048, OI 2.1.858

### x:alignment

- Type: `x:CT_CellAlignment`
- Attributes: `horizontal`, `indent`, `justifyLastLine`, `readingOrder`, `relativeIndent`, `shrinkToFit`, `textRotation`, `vertical`, `wrapText`
- Parents: `x:dxf`, `x:ndxf`, `x:odxf`, `x:xf`
- Specification: §18.8.1 p.1762, OI 2.1.693

### x:anchor

- Type: `x:CT_ObjectAnchor`
- Attributes: `moveWithCells`, `sizeWithCells`
- Children: `xdr:from`, `xdr:to`
- Parents: `x:commentPr`, `x:controlPr`, `x:objectPr`
- Specification: §18.3.1.1 p.1600, OI 2.1.588

### x:author

- Type: `s:ST_Xstring` (text: `s:ST_Xstring`)
- Parents: `x:authors`
- Specification: §18.7.1 p.1756, OI 2.1.689

### x:authors

- Type: `x:CT_Authors`
- Children: `x:author`
- Parents: `x:comments`
- Specification: §18.7.2 p.1757

### x:autoFilter

- Type: `x:CT_AutoFilter`
- Attributes: `ref`
- Children: `x:filterColumn`, `x:sortState`, `x:extLst`
- Parents: `x:customSheetView`, `x:filter`, `x:table`, `x:worksheet`
- Specification: §18.3.1.2 p.1601

### x:autoSortScope

- Type: `x:CT_AutoSortScope`
- Children: `x:pivotArea`
- Parents: `x:pivotField`
- Specification: §18.10.1.1 p.1830, OI 2.1.737

### x:b

- Type: `x:CT_Boolean`, `x:CT_BooleanProperty`
- Attributes: `c`, `cp`, `f`, `u`, `v`!, `val`
- Children: `x:x`
- Parents: `x:font`, `x:groupItems`, `x:r`, `x:rPr`, `x:sharedItems`
- Specification: §18.8.2 p.1765; §18.10.1.2 p.1830, OI 2.1.738

### x:bgColor

- Type: `x:CT_Color`
- Attributes: `auto`, `indexed`, `rgb`, `theme`, `tint`
- Parents: `x:patternFill`
- Specification: §18.8.3 p.1766, OI 2.1.694

### x:bk

- Type: `x:CT_MetadataBlock`, `x:CT_FutureMetadataBlock`
- Children: `x:rc`, `x:extLst`
- Parents: `x:cellMetadata`, `x:futureMetadata`, `x:valueMetadata`
- Specification: §18.9.1 p.1812, OI 2.1.722; §18.9.2 p.1813

### x:bookViews

- Type: `x:CT_BookViews`
- Children: `x:workbookView`
- Parents: `x:workbook`
- Specification: §18.2.1 p.1553, OI 2.1.564

### x:border

- Type: `x:CT_Border`
- Attributes: `diagonalDown`, `diagonalUp`, `outline`
- Children: `x:start`, `x:end`, `x:left`, `x:right`, `x:top`, `x:bottom`, `x:diagonal`, `x:vertical`, `x:horizontal`
- Parents: `x:borders`, `x:dxf`, `x:ndxf`, `x:odxf`
- Specification: §18.8.4 p.1767, OI 2.1.695

### x:borders

- Type: `x:CT_Borders`
- Attributes: `count`
- Children: `x:border`
- Parents: `x:styleSheet`
- Specification: §18.8.5 p.1768, OI 2.1.696

### x:bottom

- Type: `x:CT_BorderPr`
- Attributes: `style`
- Children: `x:color`
- Parents: `x:border`
- Specification: §18.8.6 p.1769

### x:brk

- Type: `x:CT_Break`
- Attributes: `id`, `man`, `max`, `min`, `pt`
- Parents: `x:colBreaks`, `x:rowBreaks`
- Specification: §18.3.1.3 p.1602, OI 2.1.589

### x:c

- Type: `x:CT_CalcCell`, `x:CT_Cell`
- Attributes: `a`, `cm`, `i`, `l`, `ph`, `r`, `ref`, `s`, `t`, `vm`
- Children: `x:f`, `x:v`, `x:is`, `x:extLst`
- Parents: `x:calcChain`, `x:row`
- Specification: §18.3.1.4 p.1603, OI 2.1.590; §18.6.1 p.1754, OI 2.1.688

### x:cacheField

- Type: `x:CT_CacheField`
- Attributes: `caption`, `databaseField`, `formula`, `hierarchy`, `level`, `mappingCount`, `memberPropertyField`, `name`!, `numFmtId`, `propertyName`, `serverField`, `sqlType`, `uniqueList`
- Children: `x:sharedItems`, `x:fieldGroup`, `x:mpMap`, `x:extLst`
- Parents: `x:cacheFields`
- Specification: §18.10.1.3 p.1831, OI 2.1.739

### x:cacheFields

- Type: `x:CT_CacheFields`
- Attributes: `count`
- Children: `x:cacheField`
- Parents: `x:pivotCacheDefinition`
- Specification: §18.10.1.4 p.1834, OI 2.1.740

### x:cacheHierarchies

- Type: `x:CT_CacheHierarchies`
- Attributes: `count`
- Children: `x:cacheHierarchy`
- Parents: `x:pivotCacheDefinition`
- Specification: §18.10.1.5 p.1835, OI 2.1.741

### x:cacheHierarchy

- Type: `x:CT_CacheHierarchy`
- Attributes: `allCaption`, `allUniqueName`, `attribute`, `caption`, `count`!, `defaultMemberUniqueName`, `dimensionUniqueName`, `displayFolder`, `hidden`, `iconSet`, `keyAttribute`, `measure`, `measureGroup`, `measures`, `memberValueDatatype`, `oneField`, `parentSet`, `set`, `time`, `unbalanced`, `unbalancedGroup`, `uniqueName`!
- Children: `x:fieldsUsage`, `x:groupLevels`, `x:extLst`
- Parents: `x:cacheHierarchies`
- Specification: §18.10.1.6 p.1835, OI 2.1.742

### x:cacheSource

- Type: `x:CT_CacheSource`
- Attributes: `connectionId`, `type`!
- Children: `x:worksheetSource`, `x:consolidation`, `x:extLst`
- Parents: `x:pivotCacheDefinition`
- Specification: §18.10.1.7 p.1840, OI 2.1.743

### x:calcChain

- Type: `x:CT_CalcChain`
- Children: `x:c`, `x:extLst`
- Specification: §18.6.2 p.1755

### x:calcPr

- Type: `x:CT_CalcPr`
- Attributes: `calcCompleted`, `calcId`, `calcMode`, `calcOnSave`, `concurrentCalc`, `concurrentManualCount`, `forceFullCalc`, `fullCalcOnLoad`, `fullPrecision`, `iterate`, `iterateCount`, `iterateDelta`, `refMode`
- Parents: `x:workbook`
- Specification: §18.2.2 p.1553, OI 2.1.565

### x:calculatedColumnFormula

- Type: `x:CT_TableFormula` (text: `x:ST_Formula`)
- Attributes: `array`
- Parents: `x:tableColumn`
- Specification: §18.5.1.1 p.1738, OI 2.1.679

### x:calculatedItem

- Type: `x:CT_CalculatedItem`
- Attributes: `field`, `formula`
- Children: `x:pivotArea`, `x:extLst`
- Parents: `x:calculatedItems`
- Specification: §18.10.1.8 p.1841, OI 2.1.744

### x:calculatedItems

- Type: `x:CT_CalculatedItems`
- Attributes: `count`
- Children: `x:calculatedItem`
- Parents: `x:pivotCacheDefinition`
- Specification: §18.10.1.9 p.1842

### x:calculatedMember

- Type: `x:CT_CalculatedMember`
- Attributes: `hierarchy`, `mdx`!, `memberName`, `name`!, `parent`, `set`, `solveOrder`
- Children: `x:extLst`
- Parents: `x:calculatedMembers`
- Specification: §18.10.1.10 p.1842, OI 2.1.745

### x:calculatedMembers

- Type: `x:CT_CalculatedMembers`
- Attributes: `count`
- Children: `x:calculatedMember`
- Parents: `x:pivotCacheDefinition`
- Specification: §18.10.1.11 p.1843

### x:cell

- Type: `x:CT_ExternalCell`
- Attributes: `r`, `t`, `vm`
- Children: `x:v`
- Parents: `x:row`
- Specification: §18.14.1 p.2027, OI 2.1.838

### x:cellMetadata

- Type: `x:CT_MetadataBlocks`
- Attributes: `count`
- Children: `x:bk`
- Parents: `x:metadata`
- Specification: §18.9.3 p.1813, OI 2.1.723

### x:cellSmartTag

- Type: `x:CT_CellSmartTag`
- Attributes: `deleted`, `type`!, `xmlBased`
- Children: `x:cellSmartTagPr`
- Parents: `x:cellSmartTags`
- Specification: §18.3.1.5 p.1604, OI 2.1.591

### x:cellSmartTagPr

- Type: `x:CT_CellSmartTagPr`
- Attributes: `key`!, `val`!
- Parents: `x:cellSmartTag`
- Specification: §18.3.1.6 p.1605, OI 2.1.592

### x:cellSmartTags

- Type: `x:CT_CellSmartTags`
- Attributes: `r`!
- Children: `x:cellSmartTag`
- Parents: `x:smartTags`
- Specification: §18.3.1.7 p.1605, OI 2.1.593

### x:cellStyle

- Type: `x:CT_CellStyle`
- Attributes: `builtinId`, `customBuiltin`, `hidden`, `iLevel`, `name`, `xfId`!
- Children: `x:extLst`
- Parents: `x:cellStyles`
- Specification: §18.8.7 p.1770, OI 2.1.697

### x:cellStyleXfs

- Type: `x:CT_CellStyleXfs`
- Attributes: `count`
- Children: `x:xf`
- Parents: `x:styleSheet`
- Specification: §18.8.9 p.1772, OI 2.1.699

### x:cellStyles

- Type: `x:CT_CellStyles`
- Attributes: `count`
- Children: `x:cellStyle`
- Parents: `x:styleSheet`
- Specification: §18.8.8 p.1771, OI 2.1.698

### x:cellWatch

- Type: `x:CT_CellWatch`
- Attributes: `r`!
- Parents: `x:cellWatches`
- Specification: §18.3.1.8 p.1606, OI 2.1.594

### x:cellWatches

- Type: `x:CT_CellWatches`
- Children: `x:cellWatch`
- Parents: `x:worksheet`
- Specification: §18.3.1.9 p.1606

### x:cellXfs

- Type: `x:CT_CellXfs`
- Attributes: `count`
- Children: `x:xf`
- Parents: `x:styleSheet`
- Specification: §18.8.10 p.1773, OI 2.1.700

### x:cfRule

- Type: `x:CT_CfRule`
- Attributes: `aboveAverage`, `bottom`, `dxfId`, `equalAverage`, `operator`, `percent`, `priority`!, `rank`, `stdDev`, `stopIfTrue`, `text`, `timePeriod`, `type`
- Children: `x:formula`, `x:colorScale`, `x:dataBar`, `x:iconSet`, `x:extLst`
- Parents: `x:conditionalFormatting`
- Specification: §18.3.1.10 p.1607, OI 2.1.595

### x:cfvo

- Type: `x:CT_Cfvo`
- Attributes: `gte`, `type`!, `val`
- Children: `x:extLst`
- Parents: `x:colorScale`, `x:dataBar`, `x:iconSet`
- Specification: §18.3.1.11 p.1609, OI 2.1.596

### x:charset

- Type: `x:CT_IntProperty`
- Attributes: `val`!
- Parents: `x:font`, `x:rPr`
- Specification: §18.4.1 p.1729

### x:chartFormat

- Type: `x:CT_ChartFormat`
- Attributes: `chart`!, `format`!, `series`
- Children: `x:pivotArea`
- Parents: `x:chartFormats`
- Specification: §18.10.1.12 p.1844, OI 2.1.746

### x:chartFormats

- Type: `x:CT_ChartFormats`
- Attributes: `count`
- Children: `x:chartFormat`
- Parents: `x:pivotTableDefinition`
- Specification: §18.10.1.13 p.1845

### x:chartsheet

- Type: `x:CT_Chartsheet`
- Children: `x:sheetPr`, `x:sheetViews`, `x:sheetProtection`, `x:customSheetViews`, `x:pageMargins`, `x:pageSetup`, `x:headerFooter`, `x:drawing`, `x:legacyDrawing`, `x:legacyDrawingHF`, `x:drawingHF`, `x:picture`, `x:webPublishItems`, `x:extLst`
- Specification: §18.3.1.12 p.1610, OI 2.1.597

### x:col

- Type: `x:CT_Col`
- Attributes: `bestFit`, `collapsed`, `customWidth`, `hidden`, `max`!, `min`!, `outlineLevel`, `phonetic`, `style`, `width`
- Parents: `x:cols`
- Specification: §18.3.1.13 p.1610, OI 2.1.598

### x:colBreaks

- Type: `x:CT_PageBreak`
- Attributes: `count`, `manualBreakCount`
- Children: `x:brk`
- Parents: `x:customSheetView`, `x:worksheet`
- Specification: §18.3.1.14 p.1612, OI 2.1.599

### x:colFields

- Type: `x:CT_ColFields`
- Attributes: `count`
- Children: `x:field`
- Parents: `x:pivotTableDefinition`
- Specification: §18.10.1.14 p.1847

### x:colHierarchiesUsage

- Type: `x:CT_ColHierarchiesUsage`
- Attributes: `count`
- Children: `x:colHierarchyUsage`
- Parents: `x:pivotTableDefinition`
- Specification: §18.10.1.15 p.1847, OI 2.1.747

### x:colHierarchyUsage

- Type: `x:CT_HierarchyUsage`
- Attributes: `hierarchyUsage`!
- Parents: `x:colHierarchiesUsage`
- Specification: §18.10.1.16 p.1848, OI 2.1.748

### x:colItems

- Type: `x:CT_colItems`
- Attributes: `count`
- Children: `x:i`
- Parents: `x:pivotTableDefinition`
- Specification: §18.10.1.17 p.1848, OI 2.1.749

### x:color

- Type: `x:CT_Color`
- Attributes: `auto`, `indexed`, `rgb`, `theme`, `tint`
- Parents: `x:bottom`, `x:colorScale`, `x:dataBar`, `x:diagonal`, `x:end`, `x:font`, `x:horizontal`, `x:left`, `x:mruColors`, `x:rPr`, `x:right`, `x:start`, `x:stop`, `x:top`, `x:vertical`
- Specification: §18.3.1.15 p.1613, OI 2.1.600

### x:colorFilter

- Type: `x:CT_ColorFilter`
- Attributes: `cellColor`, `dxfId`
- Parents: `x:filterColumn`
- Specification: §18.3.2.1 p.1720, OI 2.1.663

### x:colorScale

- Type: `x:CT_ColorScale`
- Children: `x:cfvo`, `x:color`
- Parents: `x:cfRule`
- Specification: §18.3.1.16 p.1614, OI 2.1.601

### x:colors

- Type: `x:CT_Colors`
- Children: `x:indexedColors`, `x:mruColors`
- Parents: `x:styleSheet`
- Specification: §18.8.11 p.1773

### x:cols

- Type: `x:CT_Cols`
- Children: `x:col`
- Parents: `x:worksheet`
- Specification: §18.3.1.17 p.1615

### x:comment

- Type: `x:CT_Comment`
- Attributes: `authorId`!, `guid`, `ref`!, `shapeId`
- Children: `x:text`, `x:commentPr`
- Parents: `x:commentList`
- Specification: §18.7.3 p.1757, OI 2.1.690

### x:commentList

- Type: `x:CT_CommentList`
- Children: `x:comment`
- Parents: `x:comments`
- Specification: §18.7.4 p.1758

### x:commentPr

- Type: `x:CT_CommentPr`
- Attributes: `altText`, `autoFill`, `autoLine`, `autoScale`, `defaultSize`, `disabled`, `justLastX`, `lockText`, `locked`, `print`, `textHAlign`, `textVAlign`
- Children: `x:anchor`
- Parents: `x:comment`
- Specification: §18.7.5 p.1758, OI 2.1.691

### x:comments

- Type: `x:CT_Comments`
- Children: `x:authors`, `x:commentList`, `x:extLst`
- Specification: §18.7.6 p.1762

### x:condense

- Type: `x:CT_BooleanProperty`
- Attributes: `val`
- Parents: `x:font`, `x:rPr`
- Specification: §18.8.12 p.1773

### x:conditionalFormat

- Type: `x:CT_ConditionalFormat`
- Attributes: `priority`!, `scope`, `type`
- Children: `x:pivotAreas`, `x:extLst`
- Parents: `x:conditionalFormats`
- Specification: §18.10.1.18 p.1850, OI 2.1.750

### x:conditionalFormats

- Type: `x:CT_ConditionalFormats`
- Attributes: `count`
- Children: `x:conditionalFormat`
- Parents: `x:pivotTableDefinition`
- Specification: §18.10.1.19 p.1850

### x:conditionalFormatting

- Type: `x:CT_ConditionalFormatting`
- Attributes: `pivot`, `sqref`
- Children: `x:cfRule`, `x:extLst`
- Parents: `x:worksheet`
- Specification: §18.3.1.18 p.1615, OI 2.1.602

### x:connection

- Type: `x:CT_Connection`
- Attributes: `background`, `credentials`, `deleted`, `description`, `id`!, `interval`, `keepAlive`, `minRefreshableVersion`, `name`, `new`, `odcFile`, `onlyUseConnectionFile`, `reconnectionMethod`, `refreshOnLoad`, `refreshedVersion`!, `saveData`, `savePassword`, `singleSignOnId`, `sourceFile`, `type`
- Children: `x:dbPr`, `x:olapPr`, `x:webPr`, `x:textPr`, `x:parameters`, `x:extLst`
- Parents: `x:connections`
- Specification: §18.13.1 p.2006, OI 2.1.829

### x:connections

- Type: `x:CT_Connections`
- Children: `x:connection`
- Specification: §18.13.2 p.2010

### x:consolidation

- Type: `x:CT_Consolidation`
- Attributes: `autoPage`
- Children: `x:pages`, `x:rangeSets`
- Parents: `x:cacheSource`
- Specification: §18.10.1.20 p.1851

### x:control

- Type: `x:CT_Control`
- Attributes: `name`, `r:id`!, `shapeId`!
- Children: `x:controlPr`
- Parents: `x:controls`
- Specification: §18.3.1.19 p.1616, OI 2.1.603

### x:controlPr

- Type: `x:CT_ControlPr`
- Attributes: `altText`, `autoFill`, `autoLine`, `autoPict`, `cf`, `defaultSize`, `disabled`, `linkedCell`, `listFillRange`, `locked`, `macro`, `print`, `r:id`, `recalcAlways`, `uiObject`
- Children: `x:anchor`
- Parents: `x:control`
- Specification: §18.3.1.20 p.1616, OI 2.1.604

### x:controls

- Type: `x:CT_Controls`
- Children: `x:control`
- Parents: `x:dialogsheet`, `x:worksheet`
- Specification: §18.3.1.21 p.1621, OI 2.1.605

### x:customFilter

- Type: `x:CT_CustomFilter`
- Attributes: `operator`, `val`
- Parents: `x:customFilters`
- Specification: §18.3.2.2 p.1721

### x:customFilters

- Type: `x:CT_CustomFilters`
- Attributes: `and`
- Children: `x:customFilter`
- Parents: `x:filterColumn`
- Specification: §18.3.2.3 p.1722

### x:customPr

- Type: `x:CT_CustomProperty`
- Attributes: `name`!, `r:id`!
- Parents: `x:customProperties`
- Specification: §18.3.1.22 p.1621, OI 2.1.606

### x:customProperties

- Type: `x:CT_CustomProperties`
- Children: `x:customPr`
- Parents: `x:worksheet`
- Specification: §18.3.1.23 p.1622

### x:customSheetView

- Type: `x:CT_CustomSheetView`, `x:CT_CustomChartsheetView`
- Attributes: `colorId`, `filter`, `filterUnique`, `fitToPage`, `guid`!, `hiddenColumns`, `hiddenRows`, `outlineSymbols`, `printArea`, `scale`, `showAutoFilter`, `showFormulas`, `showGridLines`, `showPageBreaks`, `showRowCol`, `showRuler`, `state`, `topLeftCell`, `view`, `zeroValues`, `zoomToFit`
- Children: `x:pane`, `x:selection`, `x:rowBreaks`, `x:colBreaks`, `x:pageMargins`, `x:printOptions`, `x:pageSetup`, `x:headerFooter`, `x:autoFilter`, `x:extLst`
- Parents: `x:customSheetViews`
- Specification: §18.3.1.24 p.1622, OI 2.1.607; §18.3.1.25 p.1623, OI 2.1.608

### x:customSheetViews

- Type: `x:CT_CustomSheetViews`, `x:CT_CustomChartsheetViews`
- Children: `x:customSheetView`
- Parents: `x:chartsheet`, `x:dialogsheet`, `x:worksheet`
- Specification: §18.3.1.26 p.1625, OI 2.1.609; §18.3.1.27 p.1626, OI 2.1.610

### x:customWorkbookView

- Type: `x:CT_CustomWorkbookView`
- Attributes: `activeSheetId`!, `autoUpdate`, `changesSavedWin`, `guid`!, `includeHiddenRowCol`, `includePrintSettings`, `maximized`, `mergeInterval`, `minimized`, `name`!, `onlySync`, `personalView`, `showComments`, `showFormulaBar`, `showHorizontalScroll`, `showObjects`, `showSheetTabs`, `showStatusbar`, `showVerticalScroll`, `tabRatio`, `windowHeight`!, `windowWidth`!, `xWindow`, `yWindow`
- Children: `x:extLst`
- Parents: `x:customWorkbookViews`
- Specification: §18.2.3 p.1557, OI 2.1.566

### x:customWorkbookViews

- Type: `x:CT_CustomWorkbookViews`
- Children: `x:customWorkbookView`
- Parents: `x:workbook`
- Specification: §18.2.4 p.1563, OI 2.1.567

### x:d

- Type: `x:CT_DateTime`
- Attributes: `c`, `cp`, `f`, `u`, `v`!
- Children: `x:x`
- Parents: `x:groupItems`, `x:r`, `x:sharedItems`
- Specification: §18.10.1.21 p.1852, OI 2.1.751

### x:dataBar

- Type: `x:CT_DataBar`
- Attributes: `maxLength`, `minLength`, `showValue`
- Children: `x:cfvo`, `x:color`
- Parents: `x:cfRule`
- Specification: §18.3.1.28 p.1626, OI 2.1.611

### x:dataConsolidate

- Type: `x:CT_DataConsolidate`
- Attributes: `function`, `leftLabels`, `link`, `startLabels`, `topLabels`
- Children: `x:dataRefs`
- Parents: `x:worksheet`
- Specification: §18.3.1.29 p.1627, OI 2.1.612

### x:dataField

- Type: `x:CT_DataField`
- Attributes: `baseField`, `baseItem`, `fld`!, `name`, `numFmtId`, `showDataAs`, `subtotal`
- Children: `x:extLst`
- Parents: `x:dataFields`
- Specification: §18.10.1.22 p.1852, OI 2.1.752

### x:dataFields

- Type: `x:CT_DataFields`
- Attributes: `count`
- Children: `x:dataField`
- Parents: `x:pivotTableDefinition`
- Specification: §18.10.1.23 p.1854

### x:dataRef

- Type: `x:CT_DataRef`
- Attributes: `name`, `r:id`, `ref`, `sheet`
- Parents: `x:dataRefs`
- Specification: §18.3.1.30 p.1628

### x:dataRefs

- Type: `x:CT_DataRefs`
- Attributes: `count`
- Children: `x:dataRef`
- Parents: `x:dataConsolidate`
- Specification: §18.3.1.31 p.1628

### x:dataValidation

- Type: `x:CT_DataValidation`
- Attributes: `allowBlank`, `error`, `errorStyle`, `errorTitle`, `imeMode`, `operator`, `prompt`, `promptTitle`, `showDropDown`, `showErrorMessage`, `showInputMessage`, `sqref`!, `type`
- Children: `x:formula1`, `x:formula2`
- Parents: `x:dataValidations`
- Specification: §18.3.1.32 p.1629, OI 2.1.613

### x:dataValidations

- Type: `x:CT_DataValidations`
- Attributes: `count`, `disablePrompts`, `xWindow`, `yWindow`
- Children: `x:dataValidation`
- Parents: `x:worksheet`
- Specification: §18.3.1.33 p.1630, OI 2.1.614

### x:dateGroupItem

- Type: `x:CT_DateGroupItem`
- Attributes: `dateTimeGrouping`!, `day`, `hour`, `minute`, `month`, `second`, `year`!
- Parents: `x:filters`
- Specification: §18.3.2.4 p.1722, OI 2.1.664

### x:dbPr

- Type: `x:CT_DbPr`
- Attributes: `command`, `commandType`, `connection`!, `serverCommand`
- Parents: `x:connection`
- Specification: §18.13.3 p.2010, OI 2.1.830

### x:ddeItem

- Type: `x:CT_DdeItem`
- Attributes: `advise`, `name`, `ole`, `preferPic`
- Children: `x:values`
- Parents: `x:ddeItems`
- Specification: §18.14.2 p.2028, OI 2.1.839

### x:ddeItems

- Type: `x:CT_DdeItems`
- Children: `x:ddeItem`
- Parents: `x:ddeLink`
- Specification: §18.14.3 p.2029

### x:ddeLink

- Type: `x:CT_DdeLink`
- Attributes: `ddeService`!, `ddeTopic`!
- Children: `x:ddeItems`
- Parents: `x:externalLink`
- Specification: §18.14.4 p.2029, OI 2.1.840

### x:definedName

- Type: `x:CT_ExternalDefinedName`, `x:CT_DefinedName` (text: `x:ST_Formula`)
- Attributes: `comment`, `customMenu`, `description`, `function`, `functionGroupId`, `help`, `hidden`, `localSheetId`, `name`!, `publishToServer`, `refersTo`, `sheetId`, `shortcutKey`, `statusBar`, `vbProcedure`, `workbookParameter`, `xlm`
- Parents: `x:definedNames`
- Specification: §18.2.5 p.1563, OI 2.1.568; §18.14.5 p.2030, OI 2.1.841

### x:definedNames

- Type: `x:CT_DefinedNames`, `x:CT_ExternalDefinedNames`
- Children: `x:definedName`
- Parents: `x:externalBook`, `x:workbook`
- Specification: §18.2.6 p.1568, OI 2.1.569; §18.14.6 p.2030

### x:deletedField

- Type: `x:CT_DeletedField`
- Attributes: `name`!
- Parents: `x:queryTableDeletedFields`
- Specification: §18.12.1 p.1999, OI 2.1.825

### x:diagonal

- Type: `x:CT_BorderPr`
- Attributes: `style`
- Children: `x:color`
- Parents: `x:border`
- Specification: §18.8.13 p.1774

### x:dialogsheet

- Type: `x:CT_Dialogsheet`
- Children: `x:sheetPr`, `x:sheetViews`, `x:sheetFormatPr`, `x:sheetProtection`, `x:customSheetViews`, `x:printOptions`, `x:pageMargins`, `x:pageSetup`, `x:headerFooter`, `x:drawing`, `x:legacyDrawing`, `x:legacyDrawingHF`, `x:drawingHF`, `x:oleObjects`, `x:controls`, `x:extLst`
- Specification: §18.3.1.34 p.1632, OI 2.1.615

### x:dimension

- Type: `x:CT_SheetDimension`, `x:CT_PivotDimension`
- Attributes: `caption`!, `measure`, `name`!, `ref`!, `uniqueName`!
- Parents: `x:dimensions`, `x:worksheet`
- Specification: §18.3.1.35 p.1632; §18.10.1.24 p.1855, OI 2.1.753

### x:dimensions

- Type: `x:CT_Dimensions`
- Attributes: `count`
- Children: `x:dimension`
- Parents: `x:pivotCacheDefinition`
- Specification: §18.10.1.25 p.1855

### x:discretePr

- Type: `x:CT_DiscretePr`
- Attributes: `count`
- Children: `x:x`
- Parents: `x:fieldGroup`
- Specification: §18.10.1.26 p.1856, OI 2.1.754

### x:drawing

- Type: `x:CT_Drawing`
- Attributes: `r:id`!
- Parents: `x:chartsheet`, `x:dialogsheet`, `x:worksheet`
- Specification: §18.3.1.36 p.1632, OI 2.1.616

### x:drawingHF

- Type: `x:CT_DrawingHF`
- Attributes: `cfe`, `cff`, `cfo`, `che`, `chf`, `cho`, `lfe`, `lff`, `lfo`, `lhe`, `lhf`, `lho`, `r:id`!, `rfe`, `rff`, `rfo`, `rhe`, `rhf`, `rho`
- Parents: `x:chartsheet`, `x:dialogsheet`, `x:worksheet`
- Specification: §18.3.1.37 p.1633, OI 2.1.617

### x:dxf

- Type: `x:CT_Dxf`
- Children: `x:font`, `x:numFmt`, `x:fill`, `x:alignment`, `x:border`, `x:protection`, `x:extLst`
- Parents: `x:dxfs`, `x:rfmt`
- Specification: §18.8.14 p.1774

### x:dxfs

- Type: `x:CT_Dxfs`
- Attributes: `count`
- Children: `x:dxf`
- Parents: `x:styleSheet`
- Specification: §18.8.15 p.1774, OI 2.1.701

### x:dynamicFilter

- Type: `x:CT_DynamicFilter`
- Attributes: `maxVal`, `maxValIso`, `type`!, `val`, `valIso`
- Parents: `x:filterColumn`
- Specification: §18.3.2.5 p.1723, OI 2.1.665

### x:e

- Type: `x:CT_Error`
- Attributes: `b`, `bc`, `c`, `cp`, `f`, `fc`, `i`, `in`, `st`, `u`, `un`, `v`!
- Children: `x:tpls`, `x:x`
- Parents: `x:entries`, `x:groupItems`, `x:r`, `x:sharedItems`
- Specification: §18.10.1.27 p.1858, OI 2.1.755

### x:end

- Type: `x:CT_BorderPr`
- Attributes: `style`
- Children: `x:color`
- Parents: `x:border`
- Specification: §18.8.16 p.1774, OI 2.1.702

### x:entries

- Type: `x:CT_PCDSDTCEntries`
- Attributes: `count`
- Children: `x:m`, `x:n`, `x:e`, `x:s`
- Parents: `x:tupleCache`
- Specification: §18.10.1.28 p.1860

### x:evenFooter

- Type: `s:ST_Xstring` (text: `s:ST_Xstring`)
- Parents: `x:headerFooter`
- Specification: §18.3.1.38 p.1639, OI 2.1.618

### x:evenHeader

- Type: `s:ST_Xstring` (text: `s:ST_Xstring`)
- Parents: `x:headerFooter`
- Specification: §18.3.1.39 p.1639, OI 2.1.619

### x:ext

- Type: `x:CT_Extension`
- Attributes: `uri`
- Children: `any(##any)`
- Parents: `x:extLst`
- Specification: §18.2.7 p.1569, OI 2.1.570

### x:extLst

- Type: `x:CT_ExtensionList`
- Children: `x:ext`
- Parents: `x:autoFilter`, `x:bk`, `x:c`, `x:cacheField`, `x:cacheHierarchy`, `x:cacheSource`, `x:calcChain`, `x:calculatedItem`, `x:calculatedMember`, `x:cellStyle`, `x:cfRule`, `x:cfvo`, `x:chartsheet`, `x:comments`, `x:conditionalFormat`, `x:conditionalFormatting`, `x:connection`, `x:customSheetView`, `x:customWorkbookView`, `x:dataField`, `x:dialogsheet`, `x:dxf`, `x:externalLink`, `x:filter`, `x:filterColumn`, `x:format`, `x:futureMetadata`, `x:groupLevel`, `x:header`, `x:ignoredErrors`, `x:metadata`, `x:nc`, `x:ndxf`, `x:oc`, `x:odxf`, `x:pageField`, `x:pivotArea`, `x:pivotCacheDefinition`, `x:pivotCacheRecords`, `x:pivotField`, `x:pivotHierarchy`, `x:pivotTableDefinition`, `x:queryTable`, `x:queryTableField`, `x:queryTableRefresh`, `x:rcc`, `x:rdn`, `x:reference`, `x:rfmt`, `x:row`, `x:rsnm`, `x:sheetView`, `x:sheetViews`, `x:singleXmlCell`, `x:sortState`, `x:sst`, `x:styleSheet`, `x:table`, `x:tableColumn`, `x:tupleCache`, `x:userInfo`, `x:volTypes`, `x:workbook`, `x:workbookView`, `x:worksheet`, `x:xf`, `x:xmlCellPr`, `x:xmlColumnPr`, `x:xmlPr`
- Specification: §18.2.10 p.1570

### x:extend

- Type: `x:CT_BooleanProperty`
- Attributes: `val`
- Parents: `x:font`, `x:rPr`
- Specification: §18.8.17 p.1775

### x:externalBook

- Type: `x:CT_ExternalBook`
- Attributes: `r:id`!
- Children: `x:sheetNames`, `x:definedNames`, `x:sheetDataSet`
- Parents: `x:externalLink`
- Specification: §18.14.7 p.2030

### x:externalLink

- Type: `x:CT_ExternalLink`
- Children: `x:externalBook`, `x:ddeLink`, `x:oleLink`, `x:extLst`
- Specification: §18.14.8 p.2031, OI 2.1.842

### x:externalReference

- Type: `x:CT_ExternalReference`
- Attributes: `r:id`!
- Parents: `x:externalReferences`
- Specification: §18.2.8 p.1570

### x:externalReferences

- Type: `x:CT_ExternalReferences`
- Children: `x:externalReference`
- Parents: `x:workbook`
- Specification: §18.2.9 p.1570, OI 2.1.571

### x:f

- Type: `x:CT_CellFormula` (text: `x:ST_Formula`)
- Attributes: `aca`, `bx`, `ca`, `del1`, `del2`, `dt2D`, `dtr`, `r1`, `r2`, `ref`, `si`, `t`
- Parents: `x:c`, `x:nc`, `x:oc`
- Specification: §18.3.1.40 p.1640, OI 2.1.620

### x:family

- Type: `x:CT_IntProperty`, `x:CT_FontFamily`
- Attributes: `val`!
- Parents: `x:font`, `x:rPr`
- Specification: §18.8.18 p.1775, OI 2.1.703

### x:fgColor

- Type: `x:CT_Color`
- Attributes: `auto`, `indexed`, `rgb`, `theme`, `tint`
- Parents: `x:patternFill`
- Specification: §18.8.19 p.1775, OI 2.1.704

### x:field

- Type: `x:CT_Field`
- Attributes: `x`!
- Parents: `x:colFields`, `x:rowFields`
- Specification: §18.10.1.29 p.1861, OI 2.1.756

### x:fieldGroup

- Type: `x:CT_FieldGroup`
- Attributes: `base`, `par`
- Children: `x:rangePr`, `x:discretePr`, `x:groupItems`
- Parents: `x:cacheField`
- Specification: §18.10.1.30 p.1861, OI 2.1.757

### x:fieldUsage

- Type: `x:CT_FieldUsage`
- Attributes: `x`!
- Parents: `x:fieldsUsage`
- Specification: §18.10.1.32 p.1863, OI 2.1.759

### x:fieldsUsage

- Type: `x:CT_FieldsUsage`
- Attributes: `count`
- Children: `x:fieldUsage`
- Parents: `x:cacheHierarchy`
- Specification: §18.10.1.31 p.1863, OI 2.1.758

### x:fileRecoveryPr

- Type: `x:CT_FileRecoveryPr`
- Attributes: `autoRecover`, `crashSave`, `dataExtractLoad`, `repairLoad`
- Parents: `x:workbook`
- Specification: §18.2.11 p.1571, OI 2.1.572

### x:fileSharing

- Type: `x:CT_FileSharing`
- Attributes: `algorithmName`, `hashValue`, `readOnlyRecommended`, `reservationPassword`, `saltValue`, `spinCount`, `userName`
- Parents: `x:workbook`
- Specification: §18.2.12 p.1572, OI 2.1.573

### x:fileVersion

- Type: `x:CT_FileVersion`
- Attributes: `appName`, `codeName`, `lastEdited`, `lowestEdited`, `rupBuild`
- Parents: `x:workbook`
- Specification: §18.2.13 p.1575, OI 2.1.574

### x:fill

- Type: `x:CT_Fill`
- Children: `x:patternFill`, `x:gradientFill`
- Parents: `x:dxf`, `x:fills`, `x:ndxf`, `x:odxf`
- Specification: §18.8.20 p.1777

### x:fills

- Type: `x:CT_Fills`
- Attributes: `count`
- Children: `x:fill`
- Parents: `x:styleSheet`
- Specification: §18.8.21 p.1777, OI 2.1.705

### x:filter

- Type: `x:CT_Filter`, `x:CT_PivotFilter`
- Attributes: `description`, `evalOrder`, `fld`!, `iMeasureFld`, `iMeasureHier`, `id`!, `mpFld`, `name`, `stringValue1`, `stringValue2`, `type`!, `val`
- Children: `x:autoFilter`, `x:extLst`
- Parents: `x:filters`
- Specification: §18.3.2.6 p.1724, OI 2.1.666; §18.10.1.33 p.1864, OI 2.1.760

### x:filterColumn

- Type: `x:CT_FilterColumn`
- Attributes: `colId`!, `hiddenButton`, `showButton`
- Children: `x:filters`, `x:top10`, `x:customFilters`, `x:dynamicFilter`, `x:colorFilter`, `x:iconFilter`, `x:extLst`
- Parents: `x:autoFilter`
- Specification: §18.3.2.7 p.1725, OI 2.1.667

### x:filters

- Type: `x:CT_PivotFilters`, `x:CT_Filters`
- Attributes: `blank`, `calendarType`, `count`
- Children: `x:filter`, `x:dateGroupItem`
- Parents: `x:filterColumn`, `x:pivotTableDefinition`
- Specification: §18.3.2.8 p.1725, OI 2.1.668; §18.10.1.34 p.1865

### x:firstFooter

- Type: `s:ST_Xstring` (text: `s:ST_Xstring`)
- Parents: `x:headerFooter`
- Specification: §18.3.1.41 p.1643, OI 2.1.621

### x:firstHeader

- Type: `s:ST_Xstring` (text: `s:ST_Xstring`)
- Parents: `x:headerFooter`
- Specification: §18.3.1.42 p.1644, OI 2.1.622

### x:font

- Type: `x:CT_Font`
- Children: `x:name`, `x:charset`, `x:family`, `x:b`, `x:i`, `x:strike`, `x:outline`, `x:shadow`, `x:condense`, `x:extend`, `x:color`, `x:sz`, `x:u`, `x:vertAlign`, `x:scheme`
- Parents: `x:dxf`, `x:fonts`, `x:ndxf`, `x:odxf`
- Specification: §18.8.22 p.1778, OI 2.1.706

### x:fonts

- Type: `x:CT_Fonts`
- Attributes: `count`
- Children: `x:font`
- Parents: `x:styleSheet`
- Specification: §18.8.23 p.1778, OI 2.1.707

### x:format

- Type: `x:CT_Format`
- Attributes: `action`, `dxfId`
- Children: `x:pivotArea`, `x:extLst`
- Parents: `x:formats`
- Specification: §18.10.1.35 p.1866, OI 2.1.761

### x:formats

- Type: `x:CT_Formats`
- Attributes: `count`
- Children: `x:format`
- Parents: `x:pivotTableDefinition`
- Specification: §18.10.1.36 p.1867

### x:formula

- Type: `x:ST_Formula` (text: `x:ST_Formula`)
- Parents: `x:cfRule`, `x:rdn`
- Specification: §18.3.1.43 p.1645, OI 2.1.623

### x:formula1

- Type: `x:ST_Formula` (text: `x:ST_Formula`)
- Parents: `x:dataValidation`
- Specification: §18.3.1.44 p.1645, OI 2.1.624

### x:formula2

- Type: `x:ST_Formula` (text: `x:ST_Formula`)
- Parents: `x:dataValidation`
- Specification: §18.3.1.45 p.1646, OI 2.1.625

### x:functionGroup

- Type: `x:CT_FunctionGroup`
- Attributes: `name`
- Parents: `x:functionGroups`
- Specification: §18.2.14 p.1576, OI 2.1.575

### x:functionGroups

- Type: `x:CT_FunctionGroups`
- Attributes: `builtInGroupCount`
- Children: `x:functionGroup`
- Parents: `x:workbook`
- Specification: §18.2.15 p.1577, OI 2.1.576

### x:futureMetadata

- Type: `x:CT_FutureMetadata`
- Attributes: `count`, `name`!
- Children: `x:bk`, `x:extLst`
- Parents: `x:metadata`
- Specification: §18.9.4 p.1813, OI 2.1.724

### x:gradientFill

- Type: `x:CT_GradientFill`
- Attributes: `bottom`, `degree`, `left`, `right`, `top`, `type`
- Children: `x:stop`
- Parents: `x:fill`
- Specification: §18.8.24 p.1779, OI 2.1.708

### x:group

- Type: `x:CT_LevelGroup`
- Attributes: `caption`!, `id`, `name`!, `uniqueName`!, `uniqueParent`
- Children: `x:groupMembers`
- Parents: `x:groups`
- Specification: §18.10.1.37 p.1867, OI 2.1.762

### x:groupItems

- Type: `x:CT_GroupItems`
- Attributes: `count`
- Children: `x:m`, `x:n`, `x:b`, `x:e`, `x:s`, `x:d`
- Parents: `x:fieldGroup`
- Specification: §18.10.1.38 p.1868, OI 2.1.763

### x:groupLevel

- Type: `x:CT_GroupLevel`
- Attributes: `caption`!, `customRollUp`, `uniqueName`!, `user`
- Children: `x:groups`, `x:extLst`
- Parents: `x:groupLevels`
- Specification: §18.10.1.39 p.1869, OI 2.1.764

### x:groupLevels

- Type: `x:CT_GroupLevels`
- Attributes: `count`
- Children: `x:groupLevel`
- Parents: `x:cacheHierarchy`
- Specification: §18.10.1.40 p.1871

### x:groupMember

- Type: `x:CT_GroupMember`
- Attributes: `group`, `uniqueName`!
- Parents: `x:groupMembers`
- Specification: §18.10.1.41 p.1872, OI 2.1.765

### x:groupMembers

- Type: `x:CT_GroupMembers`
- Attributes: `count`
- Children: `x:groupMember`
- Parents: `x:group`
- Specification: §18.10.1.42 p.1872

### x:groups

- Type: `x:CT_Groups`
- Attributes: `count`
- Children: `x:group`
- Parents: `x:groupLevel`
- Specification: §18.10.1.43 p.1873

### x:header

- Type: `x:CT_RevisionHeader`
- Attributes: `dateTime`!, `guid`!, `maxRId`, `maxSheetId`!, `minRId`, `r:id`!, `userName`!
- Children: `x:sheetIdMap`, `x:reviewedList`, `x:extLst`
- Parents: `x:headers`
- Specification: §18.11.1.1 p.1972, OI 2.1.804

### x:headerFooter

- Type: `x:CT_HeaderFooter`
- Attributes: `alignWithMargins`, `differentFirst`, `differentOddEven`, `scaleWithDoc`
- Children: `x:oddHeader`, `x:oddFooter`, `x:evenHeader`, `x:evenFooter`, `x:firstHeader`, `x:firstFooter`
- Parents: `x:chartsheet`, `x:customSheetView`, `x:dialogsheet`, `x:worksheet`
- Specification: §18.3.1.46 p.1646

### x:headers

- Type: `x:CT_RevisionHeaders`
- Attributes: `diskRevisions`, `exclusive`, `guid`!, `history`, `keepChangeHistory`, `lastGuid`, `preserveHistory`, `protected`, `revisionId`, `shared`, `trackRevisions`, `version`
- Children: `x:header`
- Specification: §18.11.1.2 p.1974, OI 2.1.805

### x:horizontal

- Type: `x:CT_BorderPr`
- Attributes: `style`
- Children: `x:color`
- Parents: `x:border`
- Specification: §18.8.25 p.1781

### x:hyperlink

- Type: `x:CT_Hyperlink`
- Attributes: `display`, `location`, `r:id`, `ref`!, `tooltip`
- Parents: `x:hyperlinks`
- Specification: §18.3.1.47 p.1651, OI 2.1.626

### x:hyperlinks

- Type: `x:CT_Hyperlinks`
- Children: `x:hyperlink`
- Parents: `x:worksheet`
- Specification: §18.3.1.48 p.1652

### x:i

- Type: `x:CT_I`, `x:CT_BooleanProperty`
- Attributes: `i`, `r`, `t`, `val`
- Children: `x:x`
- Parents: `x:colItems`, `x:font`, `x:rPr`, `x:rowItems`
- Specification: §18.8.26 p.1782; §18.10.1.44 p.1873, OI 2.1.766

### x:iconFilter

- Type: `x:CT_IconFilter`
- Attributes: `iconId`, `iconSet`!
- Parents: `x:filterColumn`
- Specification: §18.3.2.9 p.1726, OI 2.1.669

### x:iconSet

- Type: `x:CT_IconSet`
- Attributes: `iconSet`, `percent`, `reverse`, `showValue`
- Children: `x:cfvo`
- Parents: `x:cfRule`
- Specification: §18.3.1.49 p.1652

### x:ignoredError

- Type: `x:CT_IgnoredError`
- Attributes: `calculatedColumn`, `emptyCellReference`, `evalError`, `formula`, `formulaRange`, `listDataValidation`, `numberStoredAsText`, `sqref`!, `twoDigitTextYear`, `unlockedFormula`
- Parents: `x:ignoredErrors`
- Specification: §18.3.1.50 p.1653, OI 2.1.627

### x:ignoredErrors

- Type: `x:CT_IgnoredErrors`
- Children: `x:ignoredError`, `x:extLst`
- Parents: `x:worksheet`
- Specification: §18.3.1.51 p.1655, OI 2.1.628

### x:indexedColors

- Type: `x:CT_IndexedColors`
- Children: `x:rgbColor`
- Parents: `x:colors`
- Specification: §18.8.27 p.1782, OI 2.1.709

### x:inputCells

- Type: `x:CT_InputCells`
- Attributes: `deleted`, `numFmtId`, `r`!, `undone`, `val`!
- Parents: `x:scenario`
- Specification: §18.3.1.52 p.1655, OI 2.1.629

### x:is

- Type: `x:CT_Rst`
- Children: `x:t`, `x:r`, `x:rPh`, `x:phoneticPr`
- Parents: `x:c`, `x:nc`, `x:oc`
- Specification: §18.3.1.53 p.1656, OI 2.1.630

### x:item

- Type: `x:CT_Item`
- Attributes: `c`, `d`, `e`, `f`, `h`, `m`, `n`, `s`, `sd`, `t`, `x`
- Parents: `x:items`
- Specification: §18.10.1.45 p.1874, OI 2.1.767

### x:items

- Type: `x:CT_Items`
- Attributes: `count`
- Children: `x:item`
- Parents: `x:pivotField`
- Specification: §18.10.1.46 p.1877, OI 2.1.768

### x:k

- Type: `x:CT_MdxKPI`
- Attributes: `n`!, `np`!, `p`!
- Parents: `x:mdx`
- Specification: §18.9.5 p.1814, OI 2.1.725

### x:kpi

- Type: `x:CT_PCDKPI`
- Attributes: `caption`, `displayFolder`, `goal`, `measureGroup`, `parent`, `status`, `time`, `trend`, `uniqueName`!, `value`!, `weight`
- Parents: `x:kpis`
- Specification: §18.10.1.47 p.1878, OI 2.1.769

### x:kpis

- Type: `x:CT_PCDKPIs`
- Attributes: `count`
- Children: `x:kpi`
- Parents: `x:pivotCacheDefinition`
- Specification: §18.10.1.48 p.1879

### x:left

- Type: `x:CT_BorderPr`
- Attributes: `style`
- Children: `x:color`
- Parents: `x:border`
- Specification: Part 4 §15.5.1 p.235

### x:legacyDrawing

- Type: `x:CT_LegacyDrawing`
- Attributes: `r:id`!
- Parents: `x:chartsheet`, `x:dialogsheet`, `x:worksheet`
- Specification: Part 4 §15.4.1.1 p.232

### x:legacyDrawingHF

- Type: `x:CT_LegacyDrawing`
- Attributes: `r:id`!
- Parents: `x:chartsheet`, `x:dialogsheet`, `x:worksheet`
- Specification: Part 4 §15.4.1.2 p.232

### x:location

- Type: `x:CT_Location`
- Attributes: `colPageCount`, `firstDataCol`!, `firstDataRow`!, `firstHeaderRow`!, `ref`!, `rowPageCount`
- Parents: `x:pivotTableDefinition`
- Specification: §18.10.1.49 p.1880, OI 2.1.770

### x:m

- Type: `x:CT_TableMissing`, `x:CT_Missing`
- Attributes: `b`, `bc`, `c`, `cp`, `f`, `fc`, `i`, `in`, `st`, `u`, `un`
- Children: `x:tpls`, `x:x`
- Parents: `x:entries`, `x:groupItems`, `x:r`, `x:sharedItems`, `x:tables`
- Specification: §18.10.1.50 p.1881, OI 2.1.771; §18.13.4 p.2013

### x:main

- Type: `x:CT_VolMain`
- Attributes: `first`!
- Children: `x:tp`
- Parents: `x:volType`
- Specification: §18.15.1 p.2038, OI 2.1.852

### x:map

- Type: `x:CT_MeasureDimensionMap`
- Attributes: `dimension`, `measureGroup`
- Parents: `x:maps`
- Specification: §18.10.1.51 p.1883, OI 2.1.772

### x:maps

- Type: `x:CT_MeasureDimensionMaps`
- Attributes: `count`
- Children: `x:map`
- Parents: `x:pivotCacheDefinition`
- Specification: §18.10.1.52 p.1884

### x:mdx

- Type: `x:CT_Mdx`
- Attributes: `f`!, `n`!
- Children: `x:t`, `x:ms`, `x:p`, `x:k`
- Parents: `x:mdxMetadata`
- Specification: §18.9.6 p.1814, OI 2.1.726

### x:mdxMetadata

- Type: `x:CT_MdxMetadata`
- Attributes: `count`
- Children: `x:mdx`
- Parents: `x:metadata`
- Specification: §18.9.7 p.1815, OI 2.1.727

### x:measureGroup

- Type: `x:CT_MeasureGroup`
- Attributes: `caption`!, `name`!
- Parents: `x:measureGroups`
- Specification: §18.10.1.53 p.1884, OI 2.1.773

### x:measureGroups

- Type: `x:CT_MeasureGroups`
- Attributes: `count`
- Children: `x:measureGroup`
- Parents: `x:pivotCacheDefinition`
- Specification: §18.10.1.54 p.1885

### x:member

- Type: `x:CT_Member`
- Attributes: `name`!
- Parents: `x:members`
- Specification: §18.10.1.55 p.1886, OI 2.1.774

### x:members

- Type: `x:CT_Members`
- Attributes: `count`, `level`
- Children: `x:member`
- Parents: `x:pivotHierarchy`
- Specification: §18.10.1.56 p.1886, OI 2.1.775

### x:mergeCell

- Type: `x:CT_MergeCell`
- Attributes: `ref`!
- Parents: `x:mergeCells`
- Specification: §18.3.1.54 p.1656

### x:mergeCells

- Type: `x:CT_MergeCells`
- Attributes: `count`
- Children: `x:mergeCell`
- Parents: `x:worksheet`
- Specification: §18.3.1.55 p.1656, OI 2.1.631

### x:metadata

- Type: `x:CT_Metadata`
- Children: `x:metadataTypes`, `x:metadataStrings`, `x:mdxMetadata`, `x:futureMetadata`, `x:cellMetadata`, `x:valueMetadata`, `x:extLst`
- Specification: §18.9.8 p.1815

### x:metadataStrings

- Type: `x:CT_MetadataStrings`
- Attributes: `count`
- Children: `x:s`
- Parents: `x:metadata`
- Specification: §18.9.9 p.1815, OI 2.1.728

### x:metadataType

- Type: `x:CT_MetadataType`
- Attributes: `adjust`, `assign`, `cellMeta`, `clearAll`, `clearComments`, `clearContents`, `clearFormats`, `coerce`, `copy`, `delete`, `edit`, `ghostCol`, `ghostRow`, `merge`, `minSupportedVersion`!, `name`!, `pasteAll`, `pasteBorders`, `pasteColWidths`, `pasteComments`, `pasteDataValidation`, `pasteFormats`, `pasteFormulas`, `pasteNumberFormats`, `pasteValues`, `rowColShift`, `splitAll`, `splitFirst`
- Parents: `x:metadataTypes`
- Specification: §18.9.10 p.1816, OI 2.1.729

### x:metadataTypes

- Type: `x:CT_MetadataTypes`
- Attributes: `count`
- Children: `x:metadataType`
- Parents: `x:metadata`
- Specification: §18.9.11 p.1821, OI 2.1.730

### x:mp

- Type: `x:CT_MemberProperty`
- Attributes: `field`!, `level`, `name`, `nameLen`, `pLen`, `pPos`, `showAsCaption`, `showCell`, `showTip`
- Parents: `x:mps`
- Specification: §18.10.1.57 p.1886, OI 2.1.776

### x:mpMap

- Type: `x:CT_X`
- Attributes: `v`
- Parents: `x:cacheField`
- Specification: §18.10.1.58 p.1888, OI 2.1.777

### x:mps

- Type: `x:CT_MemberProperties`
- Attributes: `count`
- Children: `x:mp`
- Parents: `x:pivotHierarchy`
- Specification: §18.10.1.59 p.1889

### x:mruColors

- Type: `x:CT_MRUColors`
- Children: `x:color`
- Parents: `x:colors`
- Specification: §18.8.28 p.1785, OI 2.1.710

### x:ms

- Type: `x:CT_MdxSet`
- Attributes: `c`, `ns`!, `o`
- Children: `x:n`
- Parents: `x:mdx`
- Specification: §18.9.12 p.1822, OI 2.1.731

### x:n

- Type: `x:CT_Number`, `x:CT_MetadataStringIndex`
- Attributes: `b`, `bc`, `c`, `cp`, `f`, `fc`, `i`, `in`, `s`, `st`, `u`, `un`, `v`!, `x`!
- Children: `x:tpls`, `x:x`
- Parents: `x:entries`, `x:groupItems`, `x:ms`, `x:r`, `x:sharedItems`, `x:t`
- Specification: §18.9.13 p.1822, OI 2.1.732; §18.10.1.60 p.1889, OI 2.1.778

### x:name

- Type: `x:CT_FontName`
- Attributes: `val`!
- Parents: `x:font`
- Specification: §18.8.29 p.1785, OI 2.1.711

### x:nc

- Type: `x:CT_Cell`
- Attributes: `cm`, `ph`, `r`, `s`, `t`, `vm`
- Children: `x:f`, `x:v`, `x:is`, `x:extLst`
- Parents: `x:rcc`
- Specification: §18.11.1.3 p.1976, OI 2.1.806

### x:ndxf

- Type: `x:CT_Dxf`
- Children: `x:font`, `x:numFmt`, `x:fill`, `x:alignment`, `x:border`, `x:protection`, `x:extLst`
- Parents: `x:rcc`
- Specification: §18.11.1.4 p.1977

### x:numFmt

- Type: `x:CT_NumFmt`
- Attributes: `formatCode`!, `numFmtId`!
- Parents: `x:dxf`, `x:ndxf`, `x:numFmts`, `x:odxf`
- Specification: §18.8.30 p.1786, OI 2.1.712

### x:numFmts

- Type: `x:CT_NumFmts`
- Attributes: `count`
- Children: `x:numFmt`
- Parents: `x:styleSheet`
- Specification: §18.8.31 p.1793, OI 2.1.713

### x:objectPr

- Type: `x:CT_ObjectPr`
- Attributes: `altText`, `autoFill`, `autoLine`, `autoPict`, `dde`, `defaultSize`, `disabled`, `locked`, `macro`, `print`, `r:id`, `uiObject`
- Children: `x:anchor`
- Parents: `x:oleObject`
- Specification: §18.3.1.56 p.1657, OI 2.1.632

### x:oc

- Type: `x:CT_Cell`
- Attributes: `cm`, `ph`, `r`, `s`, `t`, `vm`
- Children: `x:f`, `x:v`, `x:is`, `x:extLst`
- Parents: `x:rcc`
- Specification: §18.11.1.5 p.1977, OI 2.1.807

### x:oddFooter

- Type: `s:ST_Xstring` (text: `s:ST_Xstring`)
- Parents: `x:headerFooter`
- Specification: §18.3.1.57 p.1660, OI 2.1.633

### x:oddHeader

- Type: `s:ST_Xstring` (text: `s:ST_Xstring`)
- Parents: `x:headerFooter`
- Specification: §18.3.1.58 p.1661, OI 2.1.634

### x:odxf

- Type: `x:CT_Dxf`
- Children: `x:font`, `x:numFmt`, `x:fill`, `x:alignment`, `x:border`, `x:protection`, `x:extLst`
- Parents: `x:rcc`
- Specification: §18.11.1.6 p.1978

### x:olapPr

- Type: `x:CT_OlapPr`
- Attributes: `local`, `localConnection`, `localRefresh`, `rowDrillCount`, `sendLocale`, `serverFill`, `serverFont`, `serverFontColor`, `serverNumberFormat`
- Parents: `x:connection`
- Specification: §18.13.5 p.2013, OI 2.1.831

### x:oldFormula

- Type: `x:ST_Formula` (text: `x:ST_Formula`)
- Parents: `x:rdn`
- Specification: §18.11.1.7 p.1978, OI 2.1.808

### x:oleItem

- Type: `x:CT_OleItem`
- Attributes: `advise`, `icon`, `name`!, `preferPic`
- Parents: `x:oleItems`
- Specification: §18.14.9 p.2031, OI 2.1.843

### x:oleItems

- Type: `x:CT_OleItems`
- Children: `x:oleItem`
- Parents: `x:oleLink`
- Specification: §18.14.10 p.2032

### x:oleLink

- Type: `x:CT_OleLink`
- Attributes: `progId`!, `r:id`!
- Children: `x:oleItems`
- Parents: `x:externalLink`
- Specification: §18.14.11 p.2032, OI 2.1.844

### x:oleObject

- Type: `x:CT_OleObject`
- Attributes: `autoLoad`, `dvAspect`, `link`, `oleUpdate`, `progId`, `r:id`, `shapeId`!
- Children: `x:objectPr`
- Parents: `x:oleObjects`
- Specification: §18.3.1.59 p.1662, OI 2.1.635

### x:oleObjects

- Type: `x:CT_OleObjects`
- Children: `x:oleObject`
- Parents: `x:dialogsheet`, `x:worksheet`
- Specification: §18.3.1.60 p.1663, OI 2.1.636

### x:oleSize

- Type: `x:CT_OleSize`
- Attributes: `ref`!
- Parents: `x:workbook`
- Specification: §18.2.16 p.1577, OI 2.1.577

### x:outline

- Type: `x:CT_BooleanProperty`
- Attributes: `val`
- Parents: `x:font`, `x:rPr`
- Specification: §18.4.2 p.1731

### x:outlinePr

- Type: `x:CT_OutlinePr`
- Attributes: `applyStyles`, `showOutlineSymbols`, `summaryBelow`, `summaryRight`
- Parents: `x:sheetPr`
- Specification: §18.3.1.61 p.1663

### x:p

- Type: `x:CT_MdxMemeberProp`
- Attributes: `n`!, `np`!
- Parents: `x:mdx`
- Specification: §18.9.14 p.1823, OI 2.1.733

### x:page

- Type: `x:CT_PCDSCPage`
- Attributes: `count`
- Children: `x:pageItem`
- Parents: `x:pages`
- Specification: §18.10.1.61 p.1891

### x:pageField

- Type: `x:CT_PageField`
- Attributes: `cap`, `fld`!, `hier`, `item`, `name`
- Children: `x:extLst`
- Parents: `x:pageFields`
- Specification: §18.10.1.62 p.1892, OI 2.1.779

### x:pageFields

- Type: `x:CT_PageFields`
- Attributes: `count`
- Children: `x:pageField`
- Parents: `x:pivotTableDefinition`
- Specification: §18.10.1.63 p.1893, OI 2.1.780

### x:pageItem

- Type: `x:CT_PageItem`
- Attributes: `name`!
- Parents: `x:page`
- Specification: §18.10.1.64 p.1893, OI 2.1.781

### x:pageMargins

- Type: `x:CT_PageMargins`
- Attributes: `bottom`!, `footer`!, `header`!, `left`!, `right`!, `top`!
- Parents: `x:chartsheet`, `x:customSheetView`, `x:dialogsheet`, `x:worksheet`
- Specification: §18.3.1.62 p.1665, OI 2.1.637

### x:pageSetUpPr

- Type: `x:CT_PageSetUpPr`
- Attributes: `autoPageBreaks`, `fitToPage`
- Parents: `x:sheetPr`
- Specification: §18.3.1.65 p.1674

### x:pageSetup

- Type: `x:CT_PageSetup`, `x:CT_CsPageSetup`
- Attributes: `blackAndWhite`, `cellComments`, `copies`, `draft`, `errors`, `firstPageNumber`, `fitToHeight`, `fitToWidth`, `horizontalDpi`, `orientation`, `pageOrder`, `paperHeight`, `paperSize`, `paperWidth`, `r:id`, `scale`, `useFirstPageNumber`, `usePrinterDefaults`, `verticalDpi`
- Parents: `x:chartsheet`, `x:customSheetView`, `x:dialogsheet`, `x:worksheet`
- Specification: §18.3.1.63 p.1665, OI 2.1.638; §18.3.1.64 p.1671, OI 2.1.639

### x:pages

- Type: `x:CT_Pages`
- Attributes: `count`
- Children: `x:page`
- Parents: `x:consolidation`
- Specification: §18.10.1.65 p.1893

### x:pane

- Type: `x:CT_Pane`
- Attributes: `activePane`, `state`, `topLeftCell`, `xSplit`, `ySplit`
- Parents: `x:customSheetView`, `x:sheetView`
- Specification: §18.3.1.66 p.1675, OI 2.1.640

### x:parameter

- Type: `x:CT_Parameter`
- Attributes: `boolean`, `cell`, `double`, `integer`, `name`, `parameterType`, `prompt`, `refreshOnChange`, `sqlType`, `string`
- Parents: `x:parameters`
- Specification: §18.13.6 p.2016, OI 2.1.832

### x:parameters

- Type: `x:CT_Parameters`
- Attributes: `count`
- Children: `x:parameter`
- Parents: `x:connection`
- Specification: §18.13.7 p.2018

### x:patternFill

- Type: `x:CT_PatternFill`
- Attributes: `patternType`
- Children: `x:fgColor`, `x:bgColor`
- Parents: `x:fill`
- Specification: §18.8.32 p.1802

### x:phoneticPr

- Type: `x:CT_PhoneticPr`
- Attributes: `alignment`, `fontId`!, `type`
- Parents: `x:is`, `x:si`, `x:text`, `x:worksheet`
- Specification: §18.4.3 p.1731, OI 2.1.671

### x:picture

- Type: `x:CT_SheetBackgroundPicture`
- Attributes: `r:id`!
- Parents: `x:chartsheet`, `x:worksheet`
- Specification: §18.3.1.67 p.1676

### x:pivotArea

- Type: `x:CT_PivotArea`
- Attributes: `axis`, `cacheIndex`, `collapsedLevelsAreSubtotals`, `dataOnly`, `field`, `fieldPosition`, `grandCol`, `grandRow`, `labelOnly`, `offset`, `outline`, `type`
- Children: `x:references`, `x:extLst`
- Parents: `x:autoSortScope`, `x:calculatedItem`, `x:chartFormat`, `x:format`, `x:pivotAreas`, `x:pivotSelection`
- Specification: §18.3.1.68 p.1676, OI 2.1.641

### x:pivotAreas

- Type: `x:CT_PivotAreas`
- Attributes: `count`
- Children: `x:pivotArea`
- Parents: `x:conditionalFormat`
- Specification: §18.10.1.66 p.1894

### x:pivotCache

- Type: `x:CT_PivotCache`
- Attributes: `cacheId`!, `r:id`!
- Parents: `x:pivotCaches`
- Specification: §18.2.17 p.1577

### x:pivotCacheDefinition

- Type: `x:CT_PivotCacheDefinition`
- Attributes: `backgroundQuery`, `createdVersion`, `enableRefresh`, `invalid`, `minRefreshableVersion`, `missingItemsLimit`, `optimizeMemory`, `r:id`, `recordCount`, `refreshOnLoad`, `refreshedBy`, `refreshedDate`, `refreshedDateIso`, `refreshedVersion`, `saveData`, `supportAdvancedDrill`, `supportSubquery`, `tupleCache`, `upgradeOnRefresh`
- Children: `x:cacheSource`, `x:cacheFields`, `x:cacheHierarchies`, `x:kpis`, `x:tupleCache`, `x:calculatedItems`, `x:calculatedMembers`, `x:dimensions`, `x:measureGroups`, `x:maps`, `x:extLst`
- Specification: §18.10.1.67 p.1894, OI 2.1.782

### x:pivotCacheRecords

- Type: `x:CT_PivotCacheRecords`
- Attributes: `count`
- Children: `x:r`, `x:extLst`
- Specification: §18.10.1.68 p.1898

### x:pivotCaches

- Type: `x:CT_PivotCaches`
- Children: `x:pivotCache`
- Parents: `x:workbook`
- Specification: §18.2.18 p.1578

### x:pivotField

- Type: `x:CT_PivotField`
- Attributes: `allDrilled`, `autoShow`, `avgSubtotal`, `axis`, `compact`, `countASubtotal`, `countSubtotal`, `dataField`, `dataSourceSort`, `defaultAttributeDrillState`, `defaultSubtotal`, `dragOff`, `dragToCol`, `dragToData`, `dragToPage`, `dragToRow`, `hiddenLevel`, `hideNewItems`, `includeNewItemsInFilter`, `insertBlankRow`, `insertPageBreak`, `itemPageCount`, `maxSubtotal`, `measureFilter`, `minSubtotal`, `multipleItemSelectionAllowed`, `name`, `nonAutoSortDefault`, `numFmtId`, `outline`, `productSubtotal`, `rankBy`, `serverField`, `showAll`, `showDropDowns`, `showPropAsCaption`, `showPropCell`, `showPropTip`, `sortType`, `stdDevPSubtotal`, `stdDevSubtotal`, `subtotalCaption`, `subtotalTop`, `sumSubtotal`, `topAutoShow`, `uniqueMemberProperty`, `varPSubtotal`, `varSubtotal`
- Children: `x:items`, `x:autoSortScope`, `x:extLst`
- Parents: `x:pivotFields`
- Specification: §18.10.1.69 p.1899, OI 2.1.783

### x:pivotFields

- Type: `x:CT_PivotFields`
- Attributes: `count`
- Children: `x:pivotField`
- Parents: `x:pivotTableDefinition`
- Specification: §18.10.1.70 p.1910, OI 2.1.784

### x:pivotHierarchies

- Type: `x:CT_PivotHierarchies`
- Attributes: `count`
- Children: `x:pivotHierarchy`
- Parents: `x:pivotTableDefinition`
- Specification: §18.10.1.71 p.1911, OI 2.1.785

### x:pivotHierarchy

- Type: `x:CT_PivotHierarchy`
- Attributes: `caption`, `dragOff`, `dragToCol`, `dragToData`, `dragToPage`, `dragToRow`, `includeNewItemsInFilter`, `multipleItemSelectionAllowed`, `outline`, `showInFieldList`, `subtotalTop`
- Children: `x:mps`, `x:members`, `x:extLst`
- Parents: `x:pivotHierarchies`
- Specification: §18.10.1.72 p.1912, OI 2.1.786

### x:pivotSelection

- Type: `x:CT_PivotSelection`
- Attributes: `activeCol`, `activeRow`, `axis`, `click`, `count`, `data`, `dimension`, `extendable`, `label`, `max`, `min`, `pane`, `previousCol`, `previousRow`, `r:id`, `showHeader`, `start`
- Children: `x:pivotArea`
- Parents: `x:sheetView`
- Specification: §18.3.1.69 p.1677, OI 2.1.642

### x:pivotTableDefinition

- Type: `x:CT_pivotTableDefinition`
- Attributes: `applyAlignmentFormats`, `applyBorderFormats`, `applyFontFormats`, `applyNumberFormats`, `applyPatternFormats`, `applyWidthHeightFormats`, `asteriskTotals`, `autoFormatId`, `cacheId`!, `chartFormat`, `colGrandTotals`, `colHeaderCaption`, `compact`, `compactData`, `createdVersion`, `customListSort`, `dataCaption`!, `dataOnRows`, `dataPosition`, `disableFieldList`, `editData`, `enableDrill`, `enableFieldProperties`, `enableWizard`, `errorCaption`, `fieldListSortAscending`, `fieldPrintTitles`, `grandTotalCaption`, `gridDropZones`, `immersive`, `indent`, `itemPrintTitles`, `mdxSubqueries`, `mergeItem`, `minRefreshableVersion`, `missingCaption`, `multipleFieldFilters`, `name`!, `outline`, `outlineData`, `pageOverThenDown`, `pageStyle`, `pageWrap`, `pivotTableStyle`, `preserveFormatting`, `printDrill`, `published`, `rowGrandTotals`, `rowHeaderCaption`, `showCalcMbrs`, `showDataDropDown`, `showDataTips`, `showDrill`, `showDropZones`, `showEmptyCol`, `showEmptyRow`, `showError`, `showHeaders`, `showItems`, `showMemberPropertyTips`, `showMissing`, `showMultipleLabel`, `subtotalHiddenItems`, `tag`, `updatedVersion`, `useAutoFormatting`, `vacatedStyle`, `visualTotals`
- Children: `x:location`, `x:pivotFields`, `x:rowFields`, `x:rowItems`, `x:colFields`, `x:colItems`, `x:pageFields`, `x:dataFields`, `x:formats`, `x:conditionalFormats`, `x:chartFormats`, `x:pivotHierarchies`, `x:pivotTableStyleInfo`, `x:filters`, `x:rowHierarchiesUsage`, `x:colHierarchiesUsage`, `x:extLst`
- Specification: §18.10.1.73 p.1914, OI 2.1.787

### x:pivotTableStyleInfo

- Type: `x:CT_PivotTableStyle`
- Attributes: `name`, `showColHeaders`, `showColStripes`, `showLastColumn`, `showRowHeaders`, `showRowStripes`
- Parents: `x:pivotTableDefinition`
- Specification: §18.10.1.74 p.1944, OI 2.1.788

### x:printOptions

- Type: `x:CT_PrintOptions`
- Attributes: `gridLines`, `gridLinesSet`, `headings`, `horizontalCentered`, `verticalCentered`
- Parents: `x:customSheetView`, `x:dialogsheet`, `x:worksheet`
- Specification: §18.3.1.70 p.1681

### x:protectedRange

- Type: `x:CT_ProtectedRange`
- Attributes: `algorithmName`, `hashValue`, `name`!, `password`, `saltValue`, `securityDescriptor`, `spinCount`, `sqref`!
- Children: `x:securityDescriptor`
- Parents: `x:protectedRanges`
- Specification: §18.3.1.71 p.1681, OI 2.1.643

### x:protectedRanges

- Type: `x:CT_ProtectedRanges`
- Children: `x:protectedRange`
- Parents: `x:worksheet`
- Specification: §18.3.1.72 p.1685

### x:protection

- Type: `x:CT_CellProtection`
- Attributes: `hidden`, `locked`
- Parents: `x:dxf`, `x:ndxf`, `x:odxf`, `x:xf`
- Specification: §18.8.33 p.1802, OI 2.1.714

### x:query

- Type: `x:CT_Query`
- Attributes: `mdx`!
- Children: `x:tpls`
- Parents: `x:queryCache`
- Specification: §18.10.1.75 p.1946, OI 2.1.789

### x:queryCache

- Type: `x:CT_QueryCache`
- Attributes: `count`
- Children: `x:query`
- Parents: `x:tupleCache`
- Specification: §18.10.1.76 p.1946

### x:queryTable

- Type: `x:CT_QueryTable`
- Attributes: `adjustColumnWidth`, `applyAlignmentFormats`, `applyBorderFormats`, `applyFontFormats`, `applyNumberFormats`, `applyPatternFormats`, `applyWidthHeightFormats`, `autoFormatId`, `backgroundRefresh`, `connectionId`!, `disableEdit`, `disableRefresh`, `fillFormulas`, `firstBackgroundRefresh`, `growShrinkType`, `headers`, `intermediate`, `name`!, `preserveFormatting`, `refreshOnLoad`, `removeDataOnSave`, `rowNumbers`
- Children: `x:queryTableRefresh`, `x:extLst`
- Specification: §18.12.2 p.1999, OI 2.1.826

### x:queryTableDeletedFields

- Type: `x:CT_QueryTableDeletedFields`
- Attributes: `count`
- Children: `x:deletedField`
- Parents: `x:queryTableRefresh`
- Specification: §18.12.3 p.2002

### x:queryTableField

- Type: `x:CT_QueryTableField`
- Attributes: `clipped`, `dataBound`, `fillFormulas`, `id`!, `name`, `rowNumbers`, `tableColumnId`
- Children: `x:extLst`
- Parents: `x:queryTableFields`
- Specification: §18.12.4 p.2003, OI 2.1.827

### x:queryTableFields

- Type: `x:CT_QueryTableFields`
- Attributes: `count`
- Children: `x:queryTableField`
- Parents: `x:queryTableRefresh`
- Specification: §18.12.5 p.2004

### x:queryTableRefresh

- Type: `x:CT_QueryTableRefresh`
- Attributes: `fieldIdWrapped`, `headersInLastRefresh`, `minimumVersion`, `nextId`, `preserveSortFilterLayout`, `unboundColumnsLeft`, `unboundColumnsRight`
- Children: `x:queryTableFields`, `x:queryTableDeletedFields`, `x:sortState`, `x:extLst`
- Parents: `x:queryTable`
- Specification: §18.12.6 p.2004, OI 2.1.828

### x:r

- Type: `x:CT_Record`, `x:CT_RElt`
- Children: `x:m`, `x:n`, `x:b`, `x:e`, `x:s`, `x:d`, `x:x`, `x:rPr`, `x:t`
- Parents: `x:is`, `x:pivotCacheRecords`, `x:si`, `x:text`
- Specification: §18.4.4 p.1732; §18.10.1.77 p.1946, OI 2.1.790

### x:rFont

- Type: `x:CT_FontName`
- Attributes: `val`!
- Parents: `x:rPr`
- Specification: §18.4.5 p.1732, OI 2.1.672

### x:rPh

- Type: `x:CT_PhoneticRun`
- Attributes: `eb`!, `sb`!
- Children: `x:t`
- Parents: `x:is`, `x:si`, `x:text`
- Specification: §18.4.6 p.1733, OI 2.1.673

### x:rPr

- Type: `x:CT_RPrElt`
- Children: `x:rFont`, `x:charset`, `x:family`, `x:b`, `x:i`, `x:strike`, `x:outline`, `x:shadow`, `x:condense`, `x:extend`, `x:color`, `x:sz`, `x:u`, `x:vertAlign`, `x:scheme`
- Parents: `x:r`
- Specification: §18.4.7 p.1733, OI 2.1.674

### x:raf

- Type: `x:CT_RevisionAutoFormatting`
- Attributes: `applyAlignmentFormats`, `applyBorderFormats`, `applyFontFormats`, `applyNumberFormats`, `applyPatternFormats`, `applyWidthHeightFormats`, `autoFormatId`, `ref`!, `sheetId`!
- Parents: `x:revisions`
- Specification: §18.11.1.8 p.1978, OI 2.1.809

### x:rangePr

- Type: `x:CT_RangePr`
- Attributes: `autoEnd`, `autoStart`, `endDate`, `endNum`, `groupBy`, `groupInterval`, `startDate`, `startNum`
- Parents: `x:fieldGroup`
- Specification: §18.10.1.78 p.1947, OI 2.1.791

### x:rangeSet

- Type: `x:CT_RangeSet`
- Attributes: `i1`, `i2`, `i3`, `i4`, `name`, `r:id`, `ref`, `sheet`
- Parents: `x:rangeSets`
- Specification: §18.10.1.79 p.1948, OI 2.1.792

### x:rangeSets

- Type: `x:CT_RangeSets`
- Attributes: `count`
- Children: `x:rangeSet`
- Parents: `x:consolidation`
- Specification: §18.10.1.80 p.1951

### x:rc

- Type: `x:CT_MetadataRecord`
- Attributes: `t`!, `v`!
- Parents: `x:bk`
- Specification: §18.9.15 p.1823, OI 2.1.734

### x:rcc

- Type: `x:CT_RevisionCellChange`
- Attributes: `dxf`, `endOfListFormulaUpdate`, `numFmtId`, `odxf`, `oldPh`, `oldQuotePrefix`, `ph`, `quotePrefix`, `rId`!, `ra`, `s`, `sId`!, `ua`, `xfDxf`
- Children: `x:oc`, `x:nc`, `x:odxf`, `x:ndxf`, `x:extLst`
- Parents: `x:revisions`, `x:rm`, `x:rrc`
- Specification: §18.11.1.9 p.1980, OI 2.1.810

### x:rcft

- Type: `x:CT_RevisionConflict`
- Attributes: `rId`!, `ra`, `sheetId`, `ua`
- Parents: `x:revisions`
- Specification: §18.11.1.10 p.1982, OI 2.1.811

### x:rcmt

- Type: `x:CT_RevisionComment`
- Attributes: `action`, `alwaysShow`, `author`!, `cell`!, `guid`!, `hiddenColumn`, `hiddenRow`, `newLength`, `old`, `oldLength`, `sheetId`!
- Parents: `x:revisions`
- Specification: §18.11.1.11 p.1982, OI 2.1.812

### x:rcv

- Type: `x:CT_RevisionCustomView`
- Attributes: `action`!, `guid`!
- Parents: `x:revisions`
- Specification: §18.11.1.12 p.1984

### x:rdn

- Type: `x:CT_RevisionDefinedName`
- Attributes: `comment`, `customMenu`, `customView`, `description`, `function`, `functionGroupId`, `help`, `hidden`, `localSheetId`, `name`!, `oldComment`, `oldCustomMenu`, `oldDescription`, `oldFunction`, `oldFunctionGroupId`, `oldHelp`, `oldHidden`, `oldShortcutKey`, `oldStatusBar`, `rId`!, `ra`, `shortcutKey`, `statusBar`, `ua`
- Children: `x:formula`, `x:oldFormula`, `x:extLst`
- Parents: `x:revisions`
- Specification: §18.11.1.13 p.1984, OI 2.1.813

### x:reference

- Type: `x:CT_PivotAreaReference`
- Attributes: `avgSubtotal`, `byPosition`, `count`, `countASubtotal`, `countSubtotal`, `defaultSubtotal`, `field`, `maxSubtotal`, `minSubtotal`, `productSubtotal`, `relative`, `selected`, `stdDevPSubtotal`, `stdDevSubtotal`, `sumSubtotal`, `varPSubtotal`, `varSubtotal`
- Children: `x:x`, `x:extLst`
- Parents: `x:references`
- Specification: §18.10.2.1 p.1965, OI 2.1.803

### x:references

- Type: `x:CT_PivotAreaReferences`
- Attributes: `count`
- Children: `x:reference`
- Parents: `x:pivotArea`
- Specification: §18.10.2.2 p.1969

### x:reviewed

- Type: `x:CT_Reviewed`
- Attributes: `rId`!
- Parents: `x:reviewedList`
- Specification: §18.11.1.14 p.1987, OI 2.1.814

### x:reviewedList

- Type: `x:CT_ReviewedRevisions`
- Attributes: `count`
- Children: `x:reviewed`
- Parents: `x:header`
- Specification: §18.11.1.15 p.1988

### x:revisions

- Type: `x:CT_Revisions`
- Children: `x:rrc`, `x:rm`, `x:rcv`, `x:rsnm`, `x:ris`, `x:rcc`, `x:rfmt`, `x:raf`, `x:rdn`, `x:rcmt`, `x:rqt`, `x:rcft`
- Specification: §18.11.1.16 p.1988

### x:rfmt

- Type: `x:CT_RevisionFormatting`
- Attributes: `length`, `s`, `sheetId`!, `sqref`!, `start`, `xfDxf`
- Children: `x:dxf`, `x:extLst`
- Parents: `x:revisions`, `x:rm`, `x:rrc`
- Specification: §18.11.1.17 p.1989, OI 2.1.815

### x:rgbColor

- Type: `x:CT_RgbColor`
- Attributes: `rgb`
- Parents: `x:indexedColors`
- Specification: §18.8.34 p.1803

### x:right

- Type: `x:CT_BorderPr`
- Attributes: `style`
- Children: `x:color`
- Parents: `x:border`
- Specification: Part 4 §15.5.2 p.236

### x:ris

- Type: `x:CT_RevisionInsertSheet`
- Attributes: `name`!, `rId`!, `ra`, `sheetId`!, `sheetPosition`!, `ua`
- Parents: `x:revisions`
- Specification: §18.11.1.18 p.1990, OI 2.1.816

### x:rm

- Type: `x:CT_RevisionMove`
- Attributes: `destination`!, `rId`!, `ra`, `sheetId`!, `source`!, `sourceSheetId`, `ua`
- Children: `x:undo`, `x:rcc`, `x:rfmt`
- Parents: `x:revisions`
- Specification: §18.11.1.19 p.1991, OI 2.1.817

### x:row

- Type: `x:CT_Row`, `x:CT_ExternalRow`
- Attributes: `collapsed`, `customFormat`, `customHeight`, `hidden`, `ht`, `outlineLevel`, `ph`, `r`, `s`, `spans`, `thickBot`, `thickTop`
- Children: `x:c`, `x:extLst`, `x:cell`
- Parents: `x:sheetData`
- Specification: §18.3.1.73 p.1685, OI 2.1.644; §18.14.12 p.2033, OI 2.1.845

### x:rowBreaks

- Type: `x:CT_PageBreak`
- Attributes: `count`, `manualBreakCount`
- Children: `x:brk`
- Parents: `x:customSheetView`, `x:worksheet`
- Specification: §18.3.1.74 p.1691, OI 2.1.645

### x:rowFields

- Type: `x:CT_RowFields`
- Attributes: `count`
- Children: `x:field`
- Parents: `x:pivotTableDefinition`
- Specification: §18.10.1.81 p.1952

### x:rowHierarchiesUsage

- Type: `x:CT_RowHierarchiesUsage`
- Attributes: `count`
- Children: `x:rowHierarchyUsage`
- Parents: `x:pivotTableDefinition`
- Specification: §18.10.1.82 p.1953

### x:rowHierarchyUsage

- Type: `x:CT_HierarchyUsage`
- Attributes: `hierarchyUsage`!
- Parents: `x:rowHierarchiesUsage`
- Specification: §18.10.1.83 p.1953, OI 2.1.793

### x:rowItems

- Type: `x:CT_rowItems`
- Attributes: `count`
- Children: `x:i`
- Parents: `x:pivotTableDefinition`
- Specification: §18.10.1.84 p.1953

### x:rqt

- Type: `x:CT_RevisionQueryTableField`
- Attributes: `fieldId`!, `ref`!, `sheetId`!
- Parents: `x:revisions`
- Specification: §18.11.1.20 p.1992, OI 2.1.818

### x:rrc

- Type: `x:CT_RevisionRowColumn`
- Attributes: `action`!, `edge`, `eol`, `rId`!, `ra`, `ref`!, `sId`!, `ua`
- Children: `x:undo`, `x:rcc`, `x:rfmt`
- Parents: `x:revisions`
- Specification: §18.11.1.21 p.1992, OI 2.1.819

### x:rsnm

- Type: `x:CT_RevisionSheetRename`
- Attributes: `newName`!, `oldName`!, `rId`!, `ra`, `sheetId`!, `ua`
- Children: `x:extLst`
- Parents: `x:revisions`
- Specification: §18.11.1.22 p.1994, OI 2.1.820

### x:s

- Type: `x:CT_XStringElement`, `x:CT_String`
- Attributes: `b`, `bc`, `c`, `cp`, `f`, `fc`, `i`, `in`, `st`, `u`, `un`, `v`!
- Children: `x:tpls`, `x:x`
- Parents: `x:entries`, `x:groupItems`, `x:metadataStrings`, `x:r`, `x:sharedItems`, `x:tables`
- Specification: §18.10.1.85 p.1955, OI 2.1.794; §18.13.8 p.2019, OI 2.1.833

### x:scenario

- Type: `x:CT_Scenario`
- Attributes: `comment`, `count`, `hidden`, `locked`, `name`!, `user`
- Children: `x:inputCells`
- Parents: `x:scenarios`
- Specification: §18.3.1.75 p.1691, OI 2.1.646

### x:scenarios

- Type: `x:CT_Scenarios`
- Attributes: `current`, `show`, `sqref`
- Children: `x:scenario`
- Parents: `x:worksheet`
- Specification: §18.3.1.76 p.1692, OI 2.1.647

### x:scheme

- Type: `x:CT_FontScheme`
- Attributes: `val`!
- Parents: `x:font`, `x:rPr`
- Specification: §18.8.35 p.1803

### x:securityDescriptor

- Type: `xsd:string` (text: `xsd:string`)
- Parents: `x:protectedRange`
- Specification: §18.3.1.77 p.1693, OI 2.1.648

### x:selection

- Type: `x:CT_Selection`
- Attributes: `activeCell`, `activeCellId`, `pane`, `sqref`
- Parents: `x:customSheetView`, `x:sheetView`
- Specification: §18.3.1.78 p.1694, OI 2.1.649

### x:serverFormat

- Type: `x:CT_ServerFormat`
- Attributes: `culture`, `format`
- Parents: `x:serverFormats`
- Specification: §18.10.1.86 p.1957, OI 2.1.795

### x:serverFormats

- Type: `x:CT_ServerFormats`
- Attributes: `count`
- Children: `x:serverFormat`
- Parents: `x:tupleCache`
- Specification: §18.10.1.87 p.1957

### x:set

- Type: `x:CT_Set`
- Attributes: `count`, `maxRank`!, `queryFailed`, `setDefinition`!, `sortType`
- Children: `x:tpls`, `x:sortByTuple`
- Parents: `x:sets`
- Specification: §18.10.1.88 p.1957, OI 2.1.796

### x:sets

- Type: `x:CT_Sets`
- Attributes: `count`
- Children: `x:set`
- Parents: `x:tupleCache`
- Specification: §18.10.1.89 p.1958

### x:shadow

- Type: `x:CT_BooleanProperty`
- Attributes: `val`
- Parents: `x:font`, `x:rPr`
- Specification: §18.8.36 p.1804

### x:sharedItems

- Type: `x:CT_SharedItems`
- Attributes: `containsBlank`, `containsDate`, `containsInteger`, `containsMixedTypes`, `containsNonDate`, `containsNumber`, `containsSemiMixedTypes`, `containsString`, `count`, `longText`, `maxDate`, `maxValue`, `minDate`, `minValue`
- Children: `x:m`, `x:n`, `x:b`, `x:e`, `x:s`, `x:d`
- Parents: `x:cacheField`
- Specification: §18.10.1.90 p.1959, OI 2.1.797

### x:sheet

- Type: `x:CT_Sheet`
- Attributes: `name`!, `r:id`!, `sheetId`!, `state`
- Parents: `x:sheets`
- Specification: §18.2.19 p.1578, OI 2.1.578

### x:sheetCalcPr

- Type: `x:CT_SheetCalcPr`
- Attributes: `fullCalcOnLoad`
- Parents: `x:worksheet`
- Specification: §18.3.1.79 p.1694

### x:sheetData

- Type: `x:CT_SheetData`, `x:CT_ExternalSheetData`
- Attributes: `refreshError`, `sheetId`!
- Children: `x:row`
- Parents: `x:sheetDataSet`, `x:worksheet`
- Specification: §18.3.1.80 p.1695; §18.14.13 p.2033, OI 2.1.846

### x:sheetDataSet

- Type: `x:CT_ExternalSheetDataSet`
- Children: `x:sheetData`
- Parents: `x:externalBook`
- Specification: §18.14.14 p.2034

### x:sheetFormatPr

- Type: `x:CT_SheetFormatPr`
- Attributes: `baseColWidth`, `customHeight`, `defaultColWidth`, `defaultRowHeight`!, `outlineLevelCol`, `outlineLevelRow`, `thickBottom`, `thickTop`, `zeroHeight`
- Parents: `x:dialogsheet`, `x:worksheet`
- Specification: §18.3.1.81 p.1695, OI 2.1.650

### x:sheetId

- Type: `x:CT_SheetId`
- Attributes: `val`!
- Parents: `x:sheetIdMap`
- Specification: §18.11.1.23 p.1995, OI 2.1.821

### x:sheetIdMap

- Type: `x:CT_SheetIdMap`
- Attributes: `count`
- Children: `x:sheetId`
- Parents: `x:header`
- Specification: §18.11.1.24 p.1995, OI 2.1.822

### x:sheetName

- Type: `x:CT_ExternalSheetName`
- Attributes: `val`
- Parents: `x:sheetNames`
- Specification: §18.14.15 p.2034, OI 2.1.847

### x:sheetNames

- Type: `x:CT_ExternalSheetNames`
- Children: `x:sheetName`
- Parents: `x:externalBook`
- Specification: §18.14.16 p.2034, OI 2.1.848

### x:sheetPr

- Type: `x:CT_SheetPr`, `x:CT_ChartsheetPr`
- Attributes: `codeName`, `enableFormatConditionsCalculation`, `filterMode`, `published`, `syncHorizontal`, `syncRef`, `syncVertical`, `transitionEntry`, `transitionEvaluation`
- Children: `x:tabColor`, `x:outlinePr`, `x:pageSetUpPr`
- Parents: `x:chartsheet`, `x:dialogsheet`, `x:worksheet`
- Specification: §18.3.1.82 p.1696, OI 2.1.651; §18.3.1.83 p.1698, OI 2.1.652

### x:sheetProtection

- Type: `x:CT_SheetProtection`, `x:CT_ChartsheetProtection`
- Attributes: `algorithmName`, `autoFilter`, `content`, `deleteColumns`, `deleteRows`, `formatCells`, `formatColumns`, `formatRows`, `hashValue`, `insertColumns`, `insertHyperlinks`, `insertRows`, `objects`, `password`, `pivotTables`, `saltValue`, `scenarios`, `selectLockedCells`, `selectUnlockedCells`, `sheet`, `sort`, `spinCount`
- Parents: `x:chartsheet`, `x:dialogsheet`, `x:worksheet`
- Specification: §18.3.1.84 p.1698, OI 2.1.653; §18.3.1.85 p.1701, OI 2.1.654

### x:sheetView

- Type: `x:CT_SheetView`, `x:CT_ChartsheetView`
- Attributes: `colorId`, `defaultGridColor`, `rightToLeft`, `showFormulas`, `showGridLines`, `showOutlineSymbols`, `showRowColHeaders`, `showRuler`, `showWhiteSpace`, `showZeros`, `tabSelected`, `topLeftCell`, `view`, `windowProtection`, `workbookViewId`!, `zoomScale`, `zoomScaleNormal`, `zoomScalePageLayoutView`, `zoomScaleSheetLayoutView`, `zoomToFit`
- Children: `x:pane`, `x:selection`, `x:pivotSelection`, `x:extLst`
- Parents: `x:sheetViews`
- Specification: §18.3.1.86 p.1707; §18.3.1.87 p.1708, OI 2.1.655

### x:sheetViews

- Type: `x:CT_SheetViews`, `x:CT_ChartsheetViews`
- Children: `x:sheetView`, `x:extLst`
- Parents: `x:chartsheet`, `x:dialogsheet`, `x:worksheet`
- Specification: §18.3.1.88 p.1712, OI 2.1.656; §18.3.1.89 p.1712, OI 2.1.657

### x:sheets

- Type: `x:CT_Sheets`
- Children: `x:sheet`
- Parents: `x:workbook`
- Specification: §18.2.20 p.1579, OI 2.1.579

### x:si

- Type: `x:CT_Rst`
- Children: `x:t`, `x:r`, `x:rPh`, `x:phoneticPr`
- Parents: `x:sst`
- Specification: §18.4.8 p.1734, OI 2.1.675

### x:singleXmlCell

- Type: `x:CT_SingleXmlCell`
- Attributes: `connectionId`!, `id`!, `r`!
- Children: `x:xmlCellPr`, `x:extLst`
- Parents: `x:singleXmlCells`
- Specification: §18.5.2.1 p.1749, OI 2.1.685

### x:singleXmlCells

- Type: `x:CT_SingleXmlCells`
- Children: `x:singleXmlCell`
- Specification: §18.5.2.2 p.1750

### x:smartTagPr

- Type: `x:CT_SmartTagPr`
- Attributes: `embed`, `show`
- Parents: `x:workbook`
- Specification: §18.2.21 p.1579

### x:smartTagType

- Type: `x:CT_SmartTagType`
- Attributes: `name`, `namespaceUri`, `url`
- Parents: `x:smartTagTypes`
- Specification: §18.2.22 p.1580, OI 2.1.580

### x:smartTagTypes

- Type: `x:CT_SmartTagTypes`
- Children: `x:smartTagType`
- Parents: `x:workbook`
- Specification: §18.2.23 p.1581, OI 2.1.581

### x:smartTags

- Type: `x:CT_SmartTags`
- Children: `x:cellSmartTags`
- Parents: `x:worksheet`
- Specification: §18.3.1.90 p.1712

### x:sortByTuple

- Type: `x:CT_Tuples`
- Attributes: `c`
- Children: `x:tpl`
- Parents: `x:set`
- Specification: §18.10.1.91 p.1962, OI 2.1.798

### x:sortCondition

- Type: `x:CT_SortCondition`
- Attributes: `customList`, `descending`, `dxfId`, `iconId`, `iconSet`, `ref`!, `sortBy`
- Parents: `x:sortState`
- Specification: §18.3.1.91 p.1713, OI 2.1.658

### x:sortState

- Type: `x:CT_SortState`
- Attributes: `caseSensitive`, `columnSort`, `ref`!, `sortMethod`
- Children: `x:sortCondition`, `x:extLst`
- Parents: `x:autoFilter`, `x:queryTableRefresh`, `x:table`, `x:worksheet`
- Specification: §18.3.1.92 p.1714

### x:sst

- Type: `x:CT_Sst`
- Attributes: `count`, `uniqueCount`
- Children: `x:si`, `x:extLst`
- Specification: §18.4.9 p.1734, OI 2.1.676

### x:start

- Type: `x:CT_BorderPr`
- Attributes: `style`
- Children: `x:color`
- Parents: `x:border`
- Specification: §18.8.37 p.1804, OI 2.1.715

### x:stop

- Type: `x:CT_GradientStop`
- Attributes: `position`!
- Children: `x:color`
- Parents: `x:gradientFill`
- Specification: §18.8.38 p.1804, OI 2.1.716

### x:stp

- Type: `s:ST_Xstring` (text: `s:ST_Xstring`)
- Parents: `x:tp`
- Specification: §18.15.2 p.2039, OI 2.1.853

### x:strike

- Type: `x:CT_BooleanProperty`
- Attributes: `val`
- Parents: `x:font`, `x:rPr`
- Specification: §18.4.10 p.1734

### x:styleSheet

- Type: `x:CT_Stylesheet`
- Children: `x:numFmts`, `x:fonts`, `x:fills`, `x:borders`, `x:cellStyleXfs`, `x:cellXfs`, `x:cellStyles`, `x:dxfs`, `x:tableStyles`, `x:colors`, `x:extLst`
- Specification: §18.8.39 p.1805, OI 2.1.717

### x:sz

- Type: `x:CT_FontSize`
- Attributes: `val`!
- Parents: `x:font`, `x:rPr`
- Specification: §18.4.11 p.1735, OI 2.1.677

### x:t

- Type: `s:ST_Xstring`, `x:CT_MdxTuple` (text: `s:ST_Xstring`)
- Attributes: `b`, `bc`, `c`, `ct`, `fc`, `fi`, `i`, `si`, `st`, `u`
- Children: `x:n`
- Parents: `x:is`, `x:mdx`, `x:r`, `x:rPh`, `x:si`, `x:text`
- Specification: §18.4.12 p.1735, OI 2.1.678; §18.9.16 p.1824, OI 2.1.735

### x:tabColor

- Type: `x:CT_Color`
- Attributes: `auto`, `indexed`, `rgb`, `theme`, `tint`
- Parents: `x:sheetPr`
- Specification: §18.3.1.93 p.1715, OI 2.1.659

### x:table

- Type: `x:CT_Table`
- Attributes: `comment`, `connectionId`, `dataCellStyle`, `dataDxfId`, `displayName`!, `headerRowBorderDxfId`, `headerRowCellStyle`, `headerRowCount`, `headerRowDxfId`, `id`!, `insertRow`, `insertRowShift`, `name`, `published`, `ref`!, `tableBorderDxfId`, `tableType`, `totalsRowBorderDxfId`, `totalsRowCellStyle`, `totalsRowCount`, `totalsRowDxfId`, `totalsRowShown`
- Children: `x:autoFilter`, `x:sortState`, `x:tableColumns`, `x:tableStyleInfo`, `x:extLst`
- Specification: §18.5.1.2 p.1738, OI 2.1.680

### x:tableColumn

- Type: `x:CT_TableColumn`
- Attributes: `dataCellStyle`, `dataDxfId`, `headerRowCellStyle`, `headerRowDxfId`, `id`!, `name`!, `queryTableFieldId`, `totalsRowCellStyle`, `totalsRowDxfId`, `totalsRowFunction`, `totalsRowLabel`, `uniqueName`
- Children: `x:calculatedColumnFormula`, `x:totalsRowFormula`, `x:xmlColumnPr`, `x:extLst`
- Parents: `x:tableColumns`
- Specification: §18.5.1.3 p.1743, OI 2.1.681

### x:tableColumns

- Type: `x:CT_TableColumns`
- Attributes: `count`
- Children: `x:tableColumn`
- Parents: `x:table`
- Specification: §18.5.1.4 p.1745

### x:tablePart

- Type: `x:CT_TablePart`
- Attributes: `r:id`!
- Parents: `x:tableParts`
- Specification: §18.3.1.94 p.1716

### x:tableParts

- Type: `x:CT_TableParts`
- Attributes: `count`
- Children: `x:tablePart`
- Parents: `x:worksheet`
- Specification: §18.3.1.95 p.1717

### x:tableStyle

- Type: `x:CT_TableStyle`
- Attributes: `count`, `name`!, `pivot`, `table`
- Children: `x:tableStyleElement`
- Parents: `x:tableStyles`
- Specification: §18.8.40 p.1805, OI 2.1.718

### x:tableStyleElement

- Type: `x:CT_TableStyleElement`
- Attributes: `dxfId`, `size`, `type`!
- Parents: `x:tableStyle`
- Specification: §18.8.41 p.1806, OI 2.1.719

### x:tableStyleInfo

- Type: `x:CT_TableStyleInfo`
- Attributes: `name`, `showColumnStripes`, `showFirstColumn`, `showLastColumn`, `showRowStripes`
- Parents: `x:table`
- Specification: §18.5.1.5 p.1746, OI 2.1.682

### x:tableStyles

- Type: `x:CT_TableStyles`
- Attributes: `count`, `defaultPivotStyle`, `defaultTableStyle`
- Children: `x:tableStyle`
- Parents: `x:styleSheet`
- Specification: §18.8.42 p.1808, OI 2.1.720

### x:tables

- Type: `x:CT_Tables`
- Attributes: `count`
- Children: `x:m`, `x:s`, `x:x`
- Parents: `x:webPr`
- Specification: §18.13.9 p.2019

### x:text

- Type: `x:CT_Rst`
- Children: `x:t`, `x:r`, `x:rPh`, `x:phoneticPr`
- Parents: `x:comment`
- Specification: §18.7.7 p.1762, OI 2.1.692

### x:textField

- Type: `x:CT_TextField`
- Attributes: `position`, `type`
- Parents: `x:textFields`
- Specification: §18.13.10 p.2019, OI 2.1.834

### x:textFields

- Type: `x:CT_TextFields`
- Attributes: `count`
- Children: `x:textField`
- Parents: `x:textPr`
- Specification: §18.13.11 p.2020, OI 2.1.835

### x:textPr

- Type: `x:CT_TextPr`
- Attributes: `characterSet`, `codePage`, `comma`, `consecutive`, `decimal`, `delimited`, `delimiter`, `fileType`, `firstRow`, `prompt`, `qualifier`, `semicolon`, `sourceFile`, `space`, `tab`, `thousands`
- Children: `x:textFields`
- Parents: `x:connection`
- Specification: §18.13.12 p.2020, OI 2.1.836

### x:top

- Type: `x:CT_BorderPr`
- Attributes: `style`
- Children: `x:color`
- Parents: `x:border`
- Specification: §18.8.43 p.1809

### x:top10

- Type: `x:CT_Top10`
- Attributes: `filterVal`, `percent`, `top`, `val`!
- Parents: `x:filterColumn`
- Specification: §18.3.2.10 p.1727, OI 2.1.670

### x:totalsRowFormula

- Type: `x:CT_TableFormula` (text: `x:ST_Formula`)
- Attributes: `array`
- Parents: `x:tableColumn`
- Specification: §18.5.1.6 p.1747, OI 2.1.683

### x:tp

- Type: `x:CT_VolTopic`
- Attributes: `t`
- Children: `x:v`, `x:stp`, `x:tr`
- Parents: `x:main`
- Specification: §18.15.3 p.2039, OI 2.1.854

### x:tpl

- Type: `x:CT_Tuple`
- Attributes: `fld`, `hier`, `item`!
- Parents: `x:sortByTuple`, `x:tpls`
- Specification: §18.10.1.92 p.1963, OI 2.1.799

### x:tpls

- Type: `x:CT_Tuples`
- Attributes: `c`
- Children: `x:tpl`
- Parents: `x:e`, `x:m`, `x:n`, `x:query`, `x:s`, `x:set`
- Specification: §18.10.1.93 p.1963

### x:tr

- Type: `x:CT_VolTopicRef`
- Attributes: `r`!, `s`!
- Parents: `x:tp`
- Specification: §18.15.4 p.2040, OI 2.1.855

### x:tupleCache

- Type: `x:CT_TupleCache`
- Children: `x:entries`, `x:sets`, `x:queryCache`, `x:serverFormats`, `x:extLst`
- Parents: `x:pivotCacheDefinition`
- Specification: §18.10.1.94 p.1963

### x:u

- Type: `x:CT_UnderlineProperty`
- Attributes: `val`
- Parents: `x:font`, `x:rPr`
- Specification: §18.4.13 p.1736

### x:undo

- Type: `x:CT_UndoInfo`
- Attributes: `array`, `cs`, `dn`, `dr`!, `exp`!, `index`!, `nf`, `r`, `ref3D`, `sId`, `v`
- Parents: `x:rm`, `x:rrc`
- Specification: §18.11.1.25 p.1995, OI 2.1.823

### x:userInfo

- Type: `x:CT_SharedUser`
- Attributes: `dateTime`!, `guid`!, `id`!, `name`!
- Children: `x:extLst`
- Parents: `x:users`
- Specification: §18.11.2.1 p.1997

### x:users

- Type: `x:CT_Users`
- Attributes: `count`
- Children: `x:userInfo`
- Specification: §18.11.2.2 p.1997, OI 2.1.824

### x:v

- Type: `s:ST_Xstring` (text: `s:ST_Xstring`)
- Parents: `x:c`, `x:cell`, `x:nc`, `x:oc`, `x:tp`
- Specification: §18.3.1.96 p.1717, OI 2.1.660

### x:val

- Type: `s:ST_Xstring` (text: `s:ST_Xstring`)
- Parents: `x:value`
- Specification: §18.14.17 p.2034, OI 2.1.849

### x:value

- Type: `x:CT_DdeValue`
- Attributes: `t`
- Children: `x:val`
- Parents: `x:values`
- Specification: §18.14.18 p.2035, OI 2.1.850

### x:valueMetadata

- Type: `x:CT_MetadataBlocks`
- Attributes: `count`
- Children: `x:bk`
- Parents: `x:metadata`
- Specification: §18.9.17 p.1825, OI 2.1.736

### x:values

- Type: `x:CT_DdeValues`
- Attributes: `cols`, `rows`
- Children: `x:value`
- Parents: `x:ddeItem`
- Specification: §18.14.19 p.2036, OI 2.1.851

### x:vertAlign

- Type: `x:CT_VerticalAlignFontProperty`
- Attributes: `val`!
- Parents: `x:font`, `x:rPr`
- Specification: §18.4.14 p.1736

### x:vertical

- Type: `x:CT_BorderPr`
- Attributes: `style`
- Children: `x:color`
- Parents: `x:border`
- Specification: §18.8.44 p.1809

### x:volType

- Type: `x:CT_VolType`
- Attributes: `type`!
- Children: `x:main`
- Parents: `x:volTypes`
- Specification: §18.15.5 p.2040

### x:volTypes

- Type: `x:CT_VolTypes`
- Children: `x:volType`, `x:extLst`
- Specification: §18.15.6 p.2041

### x:webPr

- Type: `x:CT_WebPr`
- Attributes: `consecutive`, `editPage`, `firstRow`, `htmlFormat`, `htmlTables`, `parsePre`, `post`, `sourceData`, `textDates`, `url`, `xl2000`, `xl97`, `xml`
- Children: `x:tables`
- Parents: `x:connection`
- Specification: §18.13.13 p.2023, OI 2.1.837

### x:webPublishItem

- Type: `x:CT_WebPublishItem`
- Attributes: `autoRepublish`, `destinationFile`!, `divId`!, `id`!, `sourceObject`, `sourceRef`, `sourceType`!, `title`
- Parents: `x:webPublishItems`
- Specification: §18.3.1.97 p.1718, OI 2.1.661

### x:webPublishItems

- Type: `x:CT_WebPublishItems`
- Attributes: `count`
- Children: `x:webPublishItem`
- Parents: `x:chartsheet`, `x:worksheet`
- Specification: §18.3.1.98 p.1719

### x:webPublishObject

- Type: `x:CT_WebPublishObject`
- Attributes: `autoRepublish`, `destinationFile`!, `divId`!, `id`!, `sourceObject`, `title`
- Parents: `x:webPublishObjects`
- Specification: §18.2.25 p.1583, OI 2.1.583

### x:webPublishObjects

- Type: `x:CT_WebPublishObjects`
- Attributes: `count`
- Children: `x:webPublishObject`
- Parents: `x:workbook`
- Specification: §18.2.26 p.1583

### x:webPublishing

- Type: `x:CT_WebPublishing`
- Attributes: `allowPng`, `characterSet`, `codePage`, `css`, `dpi`, `longFileNames`, `targetScreenSize`, `thicket`, `vml`
- Parents: `x:workbook`
- Specification: §18.2.24 p.1581, OI 2.1.582

### x:workbook

- Type: `x:CT_Workbook`
- Attributes: `conformance`
- Children: `x:fileVersion`, `x:fileSharing`, `x:workbookPr`, `x:workbookProtection`, `x:bookViews`, `x:sheets`, `x:functionGroups`, `x:externalReferences`, `x:definedNames`, `x:calcPr`, `x:oleSize`, `x:customWorkbookViews`, `x:pivotCaches`, `x:smartTagPr`, `x:smartTagTypes`, `x:webPublishing`, `x:fileRecoveryPr`, `x:webPublishObjects`, `x:extLst`
- Specification: §18.2.27 p.1584, OI 2.1.584

### x:workbookPr

- Type: `x:CT_WorkbookPr`
- Attributes: `allowRefreshQuery`, `autoCompressPictures`, `backupFile`, `checkCompatibility`, `codeName`, `date1904`, `defaultThemeVersion`, `filterPrivacy`, `hidePivotFieldList`, `promptedSolutions`, `publishItems`, `refreshAllConnections`, `saveExternalLinkValues`, `showBorderUnselectedTables`, `showInkAnnotation`, `showObjects`, `showPivotChartFilter`, `updateLinks`
- Parents: `x:workbook`
- Specification: §18.2.28 p.1586, OI 2.1.585

### x:workbookProtection

- Type: `x:CT_WorkbookProtection`
- Attributes: `lockRevision`, `lockStructure`, `lockWindows`, `revisionsAlgorithmName`, `revisionsHashValue`, `revisionsPassword`, `revisionsPasswordCharacterSet`, `revisionsSaltValue`, `revisionsSpinCount`, `workbookAlgorithmName`, `workbookHashValue`, `workbookPassword`, `workbookPasswordCharacterSet`, `workbookSaltValue`, `workbookSpinCount`
- Parents: `x:workbook`
- Specification: §18.2.29 p.1590, OI 2.1.586

### x:workbookView

- Type: `x:CT_BookView`
- Attributes: `activeTab`, `autoFilterDateGrouping`, `firstSheet`, `minimized`, `showHorizontalScroll`, `showSheetTabs`, `showVerticalScroll`, `tabRatio`, `visibility`, `windowHeight`, `windowWidth`, `xWindow`, `yWindow`
- Children: `x:extLst`
- Parents: `x:bookViews`
- Specification: §18.2.30 p.1597, OI 2.1.587

### x:worksheet

- Type: `x:CT_Worksheet`
- Children: `x:sheetPr`, `x:dimension`, `x:sheetViews`, `x:sheetFormatPr`, `x:cols`, `x:sheetData`, `x:sheetCalcPr`, `x:sheetProtection`, `x:protectedRanges`, `x:scenarios`, `x:autoFilter`, `x:sortState`, `x:dataConsolidate`, `x:customSheetViews`, `x:mergeCells`, `x:phoneticPr`, `x:conditionalFormatting`, `x:dataValidations`, `x:hyperlinks`, `x:printOptions`, `x:pageMargins`, `x:pageSetup`, `x:headerFooter`, `x:rowBreaks`, `x:colBreaks`, `x:customProperties`, `x:cellWatches`, `x:ignoredErrors`, `x:smartTags`, `x:drawing`, `x:legacyDrawing`, `x:legacyDrawingHF`, `x:drawingHF`, `x:picture`, `x:oleObjects`, `x:controls`, `x:webPublishItems`, `x:tableParts`, `x:extLst`
- Specification: §18.3.1.99 p.1720, OI 2.1.662

### x:worksheetSource

- Type: `x:CT_WorksheetSource`
- Attributes: `name`, `r:id`, `ref`, `sheet`
- Parents: `x:cacheSource`
- Specification: §18.10.1.95 p.1963, OI 2.1.800

### x:x

- Type: `x:CT_Index`, `x:CT_X`
- Attributes: `v`!
- Parents: `x:b`, `x:d`, `x:discretePr`, `x:e`, `x:i`, `x:m`, `x:n`, `x:r`, `x:reference`, `x:s`, `x:tables`
- Specification: §18.10.1.96 p.1964, OI 2.1.801; §18.10.1.97 p.1964, OI 2.1.802

### x:xf

- Type: `x:CT_Xf`
- Attributes: `applyAlignment`, `applyBorder`, `applyFill`, `applyFont`, `applyNumberFormat`, `applyProtection`, `borderId`, `fillId`, `fontId`, `numFmtId`, `pivotButton`, `quotePrefix`, `xfId`
- Children: `x:alignment`, `x:protection`, `x:extLst`
- Parents: `x:cellStyleXfs`, `x:cellXfs`
- Specification: §18.8.45 p.1809, OI 2.1.721

### x:xmlCellPr

- Type: `x:CT_XmlCellPr`
- Attributes: `id`!, `uniqueName`
- Children: `x:xmlPr`, `x:extLst`
- Parents: `x:singleXmlCell`
- Specification: §18.5.2.3 p.1750, OI 2.1.686

### x:xmlColumnPr

- Type: `x:CT_XmlColumnPr`
- Attributes: `denormalized`, `mapId`!, `xmlDataType`!, `xpath`!
- Children: `x:extLst`
- Parents: `x:tableColumn`
- Specification: §18.5.1.7 p.1747, OI 2.1.684

### x:xmlPr

- Type: `x:CT_XmlPr`
- Attributes: `mapId`!, `xmlDataType`!, `xpath`!
- Children: `x:extLst`
- Parents: `x:xmlCellPr`
- Specification: §18.5.2.4 p.1751, OI 2.1.687

`!` marks required attributes.
