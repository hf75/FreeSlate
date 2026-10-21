namespace FreeSlate.Docx.Layout;

/// <summary>
/// How Word lays the document out by its compatibility settings (catalog: settings.compatibility): the compatibility
/// mode (w:compatSetting compatibilityMode; Word 2013 and later lay out in mode 15, earlier versions in theirs) and the
/// layout options of w:compat, read once and named by what they change. The places that follow a switch say how it
/// was measured.
/// </summary>
/// <param name="mode">The compatibility mode to lay out in, the document's (see <see cref="DocumentContext.CompatibilityMode"/>).</param>
public sealed class LayoutProfile(DocumentContext context, int mode)
{
    // Paragraph spacing and line heights.

    /// <summary>
    /// Whether paragraphs add their spaces after and before (w:doNotUseHTMLParagraphAutoSpacing) rather than overlap
    /// them (see <see cref="DocumentContext.Between"/>).
    /// </summary>
    public bool AddsSpacing { get; } = context.Compat("doNotUseHTMLParagraphAutoSpacing");

    /// <summary>
    /// Whether exact line heights put the text at their top instead of centering it: w:noExtraLineSpacing (measured
    /// with docx/local/c2-exact* and c3-exact, in every mode).
    /// </summary>
    public bool ExactFromTop { get; } = context.Compat("noExtraLineSpacing");

    /// <summary>
    /// Whether raised and lowered text (w:position) stays out of the line height: w:noSpaceRaiseLower, which Word
    /// follows before Word 2013 only (catalog: settings.compatibility, measured with docx/local/c2-legacy*: text 6 pt
    /// up and down in modes 11 to 14).
    /// </summary>
    public bool NoSpaceRaiseLower { get; } = mode < 15 && context.Compat("noSpaceRaiseLower");

    /// <summary>How the document takes line heights from fonts (w:noLeading, w:suppressTopSpacingWP; before Word 2013 only).</summary>
    public LineHeights LineHeights { get; } = mode >= 15 ? LineHeights.Word
        : context.Compat("suppressTopSpacingWP") ? LineHeights.WordPerfect
        : context.Compat("noLeading") ? LineHeights.NoLeading : LineHeights.Word;

    /// <summary>
    /// Whether the first line on a page loses room above its text where its line height is exact or at least:
    /// w:suppressTopSpacing (see Paginator.TopTrim).
    /// </summary>
    public bool SuppressTopSpacing { get; } = context.Compat("suppressTopSpacing");

    /// <summary>
    /// Whether the first line on a page, at least as high as its rule says or on the document grid, has its natural
    /// ascent above its baseline: w:suppressSpacingAtTopOfPage, before Word 2013 only (see Paginator.TopTrim).
    /// </summary>
    public bool SuppressSpacingAtTop { get; } = mode < 15 && context.Compat("suppressSpacingAtTopOfPage");

    /// <summary>
    /// Whether a paragraph after a page break keeps what its space before has beyond the space after of the paragraph
    /// before it: before Word 2013 (see Paginator.Place).
    /// </summary>
    public bool PageBreakKeepsSpaceBefore { get; } = mode < 15;

    /// <summary>
    /// Whether the space after the paragraph that ends a section of columns is shared out among them where the
    /// paragraph has text: before Word 2013.
    /// </summary>
    public bool SharesSectionEndSpace { get; } = mode < 15;

    /// <summary>
    /// Whether lines in table cells keep the document grid's line pitch: w:adjustLineHeightInTable (see
    /// TableLayout.Layout).
    /// </summary>
    public bool AdjustsLineHeightInTables { get; } = context.Compat("adjustLineHeightInTable");

    /// <summary>
    /// Whether contextual spacing drops the space between paragraphs of one style in table cells too: from Word 2013 on
    /// always, before unless w:allowSpaceOfSameStyleInTable.
    /// </summary>
    public bool ContextualSpacingInCells { get; } = !(mode < 15 && context.Compat("allowSpaceOfSameStyleInTable"));

    // Characters.

    /// <summary>
    /// Whether a backslash in Japanese text (w:lang w:eastAsia ja) shows as a yen sign, unless its font is an East Asian
    /// one: w:doNotLeaveBackslashAlone (measured with docx/local/c2-backslash and c3-yen: Arial, Times New Roman and
    /// Courier New change, MS Mincho, MS Gothic, Yu Gothic, Meiryo, SimSun and Malgun Gothic keep the backslash).
    /// </summary>
    public bool YenBackslash { get; } = context.Compat("doNotLeaveBackslashAlone");

    /// <summary>
    /// Whether spaces beside spaces and East Asian characters take half an em and character units count a run's
    /// character spacing twice (w:balanceSingleByteDoubleByteWidth, see ParagraphLayout.Balance).
    /// </summary>
    public bool BalanceSpaces { get; } = context.Compat("balanceSingleByteDoubleByteWidth");

    /// <summary>Whether small capitals take Word for the Macintosh's sizes: w:mwSmallCaps, before Word 2013 only (see RunStyles.MacSmallCapsSize).</summary>
    public bool MacSmallCaps { get; } = mode < 15 && context.Compat("mwSmallCaps");

    /// <summary>
    /// Whether runs kern by the fonts' GPOS tables, with OpenType features, rather than their kern tables: from Word
    /// 2013 on, in Word 2010's mode where w:compatSetting enableOpenTypeFeatures says so (see RunStyles.Create).
    /// </summary>
    public bool OpenTypeKerning { get; } = mode >= 15 || mode == 14 && context.CompatSetting("enableOpenTypeFeatures") is "1" or "true" or "on";

    /// <summary>
    /// Whether runs take no OpenType features (ligatures, alternates, number forms, stylistic sets): before Word 2010
    /// (see WordFonts.Features).
    /// </summary>
    public bool NoOpenTypeFeatures { get; } = mode < 14;

    // Lines.

    /// <summary>
    /// Whether ideographic spaces (U+3000) at a line's end trail it as spaces do, hanging past the edge: before Word 2013
    /// (measured with docx/local/isp-both-12: "tttest（" justified to the margin, twelve ideographic spaces past it,
    /// "）" on the next line; docx/external/libreoffice/extras-uiwriter/tdf128375).
    /// </summary>
    public bool IdeographicTrails { get; } = mode < 15;

    /// <summary>Whether a line may break after a hyphen (U+2010) as after a hyphen-minus: from Word 2013 on.</summary>
    public bool BreaksAfterHyphen { get; } = mode >= 15;

    /// <summary>
    /// Whether punctuation that may not begin a line hangs past its end in paragraphs of any alignment, also where their
    /// runs have an East Asian language without East Asian text: before Word 2013; from then on in justified
    /// paragraphs with East Asian text.
    /// </summary>
    public bool PunctuationHangsAnywhere { get; } = mode < 15;

    /// <summary>
    /// Whether the part of a hyphenated word needs room for the wider of the hyphen and the letter after the break, not
    /// the hyphen alone: before Word 2013.
    /// </summary>
    public bool HyphenTakesNextLetter { get; } = mode < 15;

    /// <summary>
    /// Whether lines hyphenate only where they would end further than 0.25 in from their end, whatever
    /// w:hyphenationZone says: from Word 2013 on.
    /// </summary>
    public bool FixedHyphenationZone { get; } = mode >= 15;

    /// <summary>
    /// Whether justified lines squeeze their spaces to take a word, or a word's hyphenated part, that does not quite
    /// fit: from Word 2013 on.
    /// </summary>
    public bool SqueezesJustifiedLines { get; } = mode >= 15;

    /// <summary>
    /// Whether punctuation compression squeezes each mark by no more than 32 times what each gap of the line would
    /// stretch, from Word 2013 on, rather than by half a mark in all (see ParagraphLayout.CompressesPunctuation).
    /// </summary>
    public bool CompressionByGaps { get; } = mode >= 15;

    /// <summary>
    /// What a space takes beyond the other characters of a line spread between all of them, in its own width: 0.6
    /// before Word 2013, 0.45 from then on (see ParagraphLayout.Align).
    /// </summary>
    public double DistributedSpaceShare { get; } = mode < 15 ? 0.6 : 0.45;

    /// <summary>
    /// Whether justification fills the spaces' extras first, then the boundaries', the rest going to every gap alike:
    /// from Word 2013 on; before, extras the line has no room for go (see ParagraphLayout.Distribute).
    /// </summary>
    public bool PhasedDistribution { get; } = mode >= 15;

    /// <summary>
    /// Whether justified lines that end with a manual line break are stretched: unless w:doNotExpandShiftReturn (catalog:
    /// settings.compatibility, measured with docx/local/compat15-*).
    /// </summary>
    public bool ExpandsLineBreaks { get; } = !context.Compat("doNotExpandShiftReturn");

    /// <summary>Whether the tab after a list number passes the hanging indent for a custom stop after it: w:doNotUseIndentAsNumberingTabStop, before Word 2013 only (see ParagraphLayout.TabStop).</summary>
    public bool NumberTabSkipsIndent { get; } = mode < 15 && context.Compat("doNotUseIndentAsNumberingTabStop");

    /// <summary>
    /// Whether a custom tab stop past the right indent holds past the column's edge too, the line then reaching 22 in:
    /// before Word 2013; from then on up to the edge, the line not breaking next to tabs past the indent.
    /// </summary>
    public bool TabStopsPastEdge { get; } = mode < 15;

    /// <summary>
    /// Whether a first line indented or hanging beside a drop cap moves by the cap's whole room, its tab stop with it:
    /// before Word 2013; from then on hanging lines start at the room, indented ones that far past it.
    /// </summary>
    public bool DropCapShiftsFirstLine { get; } = mode < 15;

    /// <summary>
    /// Whether the room beside floating objects counts its parts by their width before the indent, those beside objects
    /// wrapped along their polygons taking text from 9 pt: before Word 2013; from then on by their width after it, from
    /// 18 pt, the first where the line's first word fits (see ParagraphLayout.MinimumSegment).
    /// </summary>
    public bool WrapRoomBeforeIndent { get; } = mode < 15;

    /// <summary>
    /// Whether a line taller than the height it was placed for keeps clear of the objects beside the rest of it too,
    /// laid out again with its own height: from Word 2013 on.
    /// </summary>
    public bool TallLinesClearObjects { get; } = mode >= 15;

    /// <summary>
    /// Whether the paragraph mark goes to a line of its own after an object in a field that is wider than the line:
    /// before Word 2013.
    /// </summary>
    public bool MarkWrapsAfterWideFields { get; } = mode < 15;

    /// <summary>
    /// Whether a horizontal rule without the paragraph mark on its line stands in a line only 2.52 pt higher and lower
    /// than itself, whatever the spacing: before Word 2013; from then on in one as high as the mark's (Vml.Rule).
    /// </summary>
    public bool RulesSizeTheirLines { get; } = mode < 15;

    /// <summary>
    /// Whether a line of drawings without the paragraph mark is as high as they are, without spacing's extra, one a
    /// line break ends having the break's font: before Word 2013; from then on the mark's.
    /// </summary>
    public bool DrawingLinesWithoutMark { get; } = mode < 15;

    /// <summary>
    /// Whether text snaps to the cells of a character grid (w:docGrid linesAndChars or snapToChars): before Word 2013
    /// (see ParagraphLayout.SnapCell).
    /// </summary>
    public bool CharacterGridCells { get; } = mode < 15;

    // Pages, columns and notes.

    /// <summary>
    /// Whether the paragraph mark after a page break that ends its paragraph goes to the next page, an empty line
    /// there: w:splitPgBreakAndParaMark, before Word 2013 only (measured with docx/local/compat12-* and c2-legacy*).
    /// </summary>
    public bool SplitPageBreak { get; } = mode < 15 && context.Compat("splitPgBreakAndParaMark");

    /// <summary>
    /// Whether a page break in the paragraph that ends a section goes unseen where the next section goes on on the same
    /// page: before Word 2013, unless w:splitPgBreakAndParaMark (see Paginator.BreakUnseen).
    /// </summary>
    public bool ContinuousSectionsHidePageBreaks { get; } = mode < 15 && !context.Compat("splitPgBreakAndParaMark");

    /// <summary>
    /// Whether a continuous section break balances the columns above it: from Word 2013 on always, before unless
    /// w:noColumnBalance.
    /// </summary>
    public bool BalancesColumns { get; } = !(mode < 15 && context.Compat("noColumnBalance"));

    /// <summary>
    /// Whether a line that ends with a hyphen goes on with the line after it rather than end a column (measured with
    /// docx/local/hyph-d): from Word 2013 on, unless the document allows hyphens at the bottom
    /// (w:compatSetting allowHyphenationAtTrackBottom; docx/external/libreoffice/extras-ooxmlexport/
    /// tdf160518_allowHyphenationAtTrackBottom: "at-" ends page 1; tdf160518_useWord2013TrackBottomHyphenation moves
    /// the line, as without the setting; powertools/HC048-Excerpt: in mode 14 "termi-" ends a page). With
    /// useWord2013TrackBottomHyphenation 0 the line breaks anew instead (<see cref="HyphenBreaksAnew"/>).
    /// </summary>
    public bool HyphenMoves { get; } = mode >= 15 && context.CompatSetting("allowHyphenationAtTrackBottom") is not ("1" or "true")
        && context.CompatSetting("useWord2013TrackBottomHyphenation") is not ("0" or "false");

    /// <summary>
    /// Whether a column's last line that would end with an automatic hyphen breaks anew before that word, from Word 2016
    /// on (w:compatSetting useWord2013TrackBottomHyphenation 0, mode 15, unless allowHyphenationAtTrackBottom;
    /// LibreOffice's extras-ooxmlexport/tdf165354: page 1 ends "except that it has an" and "atmosphere" begins page 2,
    /// where "an at-" would have fitted).
    /// </summary>
    public bool HyphenBreaksAnew { get; } = mode >= 15 && context.CompatSetting("allowHyphenationAtTrackBottom") is not ("1" or "true")
        && context.CompatSetting("useWord2013TrackBottomHyphenation") is "0" or "false";

    /// <summary>Whether a page's last line above the notes needs room for its paragraph's space after too: from Word 2013 on.</summary>
    public bool NotesNeedSpaceAfter { get; } = mode >= 15;

    /// <summary>
    /// Whether footnotes beneath the text (w:pos beneathText) stand above the endnotes on a page they share: before Word
    /// 2013; from then on below them.
    /// </summary>
    public bool FootnotesAboveEndnotes { get; } = mode < 15;

    /// <summary>
    /// Whether the footnotes' continuation notice takes its room below the last note of every page with notes, where
    /// the settings list it: before Word 2013 (see Paginator.SeparatorHeight).
    /// </summary>
    public bool CountsContinuationNotice { get; } = mode < 15;

    // Tables.

    /// <summary>
    /// Whether tables of the text have their first cell's text at their indent and their percentage width with their
    /// cells' margins: before Word 2013 (see TableLayout.Legacy).
    /// </summary>
    public bool TablesIndentedByText { get; } = mode < 15;

    /// <summary>
    /// Whether a floating table's horizontal position places its cells' text, the table out by its insets, and counts
    /// its indent (w:tblInd): before Word 2013 (see Paginator.PlaceFloatingTable).
    /// </summary>
    public bool FloatingTablesByText { get; } = mode < 15;

    /// <summary>
    /// Whether a table anchored to the page or the margin breaks where it would at the paragraph after it, as many
    /// lines down: before Word 2013; from then on at the bottom margin, its first row past it where it begins a page
    /// without text before it, else on the next page.
    /// </summary>
    public bool PageTablesBreakAsText { get; } = mode < 15;

    /// <summary>Whether floating tables stay whole where they stand: w:doNotBreakWrappedTables.</summary>
    public bool KeepsWrappedTablesWhole { get; } = context.Compat("doNotBreakWrappedTables");

    /// <summary>
    /// Whether the text after a floating table that breaks over pages goes on beside and below its first part, each
    /// further part at the top of the next page: w:compatSetting allowTextAfterFloatingTableBreak, from Word 2013 on
    /// (see Paginator.PlaceFloatingTable).
    /// </summary>
    public bool TextAfterFloatingTableBreak { get; } = mode >= 15 && context.CompatSetting("allowTextAfterFloatingTableBreak") is "1" or "true" or "on";

    /// <summary>
    /// Whether a table goes below every floating object and frame it would overlap, whatever their wrapping, also those
    /// anchored after it on the page: from Word 2013 on; before only below those kept free above and below and those
    /// that leave it less than half its width right of them (see Paginator.BelowFloats).
    /// </summary>
    public bool TablesBelowAllFloats { get; } = mode >= 15;

    /// <summary>
    /// Whether header rows with room for no row below them go on with the table to the next page: from Word 2013 on;
    /// before they stay and repeat there.
    /// </summary>
    public bool HeaderRowsGoOn { get; } = mode >= 15;

    /// <summary>Whether widow control and keeping with the next paragraph hold where rows split: from Word 2013 on.</summary>
    public bool SplitRowsKeepLines { get; } = mode >= 15;

    /// <summary>
    /// Whether a line above a row's cut needs room for its cell's bottom margin too: before Word 2013 (see
    /// RowBox.MarginBelowCut).
    /// </summary>
    public bool CellMarginBelowCut { get; } = mode < 15;

    /// <summary>
    /// Whether a paragraph whose drawing that text goes around would reach past a row's cut goes on with it: from Word
    /// 2013 on (see RowBox.AnchorsReach).
    /// </summary>
    public bool AnchorsReachPastCut { get; } = mode >= 15;

    // Frames and drawings.

    /// <summary>
    /// Whether a text frame, or a floating table, without a vertical position stands at its paragraph whatever its
    /// anchor: before Word 2013 (see Paginator.VerticalAnchor).
    /// </summary>
    public bool UnplacedFramesAtParagraph { get; } = mode < 15;

    /// <summary>
    /// Whether a frame's inline shapes are drawn at its left edge, as far below the page's top as the frame stands
    /// below where its paragraph would stand: before Word 2013 (see PlacedLine.InlineOrigin).
    /// </summary>
    public bool FrameShapesAtFrameEdge { get; } = mode < 15;

    /// <summary>
    /// Whether a paragraph whose floating table reaches beside its first line has that line below the page frames over
    /// the column's edges it meets: before Word 2013.
    /// </summary>
    public bool FramesLowerTableParagraphs { get; } = mode < 15;

    /// <summary>
    /// Whether a paragraph's space after counts against the floating table it stands with and the drawings wrapped top
    /// and bottom below it, the paragraph going below them where it reaches them: before Word 2013.
    /// </summary>
    public bool SpaceAfterMeetsFloats { get; } = mode < 15;

    /// <summary>
    /// Whether text goes right of an object whose wrapText "left" leaves no room left of it: before Word 2013 (see
    /// Paginator.Exclusion.OtherSide).
    /// </summary>
    public bool WrapsLeftOnOtherSide { get; } = mode < 15;

    /// <summary>
    /// Whether a floating drawing in a table cell of layoutInCell 0 stands outside the cell, from the column: before
    /// Word 2013; from then on every one is laid out in its cell (see TableLayout.InCell).
    /// </summary>
    public bool HonorsLayoutInCell { get; } = mode < 15;

    /// <summary>
    /// Whether floating drawings take their places as before Word 2013: alignments in the column count from the
    /// paragraph's indents, an aligned drawing placed down by an offset standing its effect extent lower; one aligned
    /// left or right on the page stands in the margin beside the text; a VML shape aligned one way and placed by an
    /// offset the other moves on by its line's reach; one that text goes around, placed from its paragraph or line,
    /// stays on the page down too (see Paginator.Anchored).
    /// </summary>
    public bool AnchorsAsWord2010 { get; } = mode < 15;

    /// <summary>
    /// Whether a drawing that may not overlap another and moves with its paragraph first goes its own height down:
    /// before Word 2013 (see Paginator.Clear).
    /// </summary>
    public bool OverlapStepsDown { get; } = mode < 15;

    /// <summary>
    /// Whether a drawing that text goes around, placed from its paragraph or line, stays between the margins: from Word
    /// 2013 on (see Paginator.Unpinned).
    /// </summary>
    public bool KeepsDrawingsInMargins { get; } = mode >= 15;

    /// <summary>
    /// Whether a cell cuts the drawings in it that text goes around at its edges: from Word 2013 on.
    /// </summary>
    public bool CellsCutWrappedDrawings { get; } = mode >= 15;

    /// <summary>
    /// Whether an object that wraps text and moves with its paragraph, held up by the bottom margin, takes the
    /// paragraph to the next page where it then reaches above the bottom of its line: from Word 2013 on.
    /// </summary>
    public bool HeldDrawingsTakeParagraph { get; } = mode >= 15;

    /// <summary>
    /// Whether the drawings of a paragraph that moves on stand from where it ends up, not where it stood: from Word 2013
    /// on.
    /// </summary>
    public bool DrawingsFollowMovedParagraph { get; } = mode >= 15;

    /// <summary>
    /// Whether an object taller than the page's body, whatever its wrapping, leaves the paragraph's first line where it
    /// would stand without it and takes the lines after it below it: from Word 2013 on; before only one wrapped along
    /// its polygon.
    /// </summary>
    public bool TallDrawingsTakeLines { get; } = mode >= 15;

    /// <summary>
    /// Whether a drawing that text does not go around stands from its paragraph's first line and its own line where the
    /// paragraph's floating objects pushed them: before Word 2013.
    /// </summary>
    public bool PushedLinesMoveDrawings { get; } = mode < 15;

    /// <summary>Whether a drawing turned across its line swaps its box's sides: from Word 2010 on (see Drawings.Box).</summary>
    public bool TurnedDrawingsSwapSides { get; } = mode >= 14;

    /// <summary>
    /// Whether an inline shape's or group's extent is drawn as far again right and down as its effect extents, the line
    /// keeping its room: Word 2007 and older (modes 11 and 12, and documents without a mode).
    /// </summary>
    public bool ShapesShiftedByEffects { get; } = mode <= 12;

    /// <summary>
    /// Whether turned lines in boxes stand on Word's device as before Word 2013: from the edge they start from, lines
    /// down the box stacking from its right edge (see PageRenderer.TurnedOnDots).
    /// </summary>
    public bool TurnedLinesAsWord2010 { get; } = mode < 15;

    /// <summary>Whether underlines go on under the spaces that end a line: w:ulTrailSpace.</summary>
    public bool UnderlinesTrailingSpaces { get; } = context.Compat("ulTrailSpace");
}
