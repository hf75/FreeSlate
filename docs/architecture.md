# Architecture

FreeSlate turns a presentation or a Word document into one HTML document, draws it as images or PDF, and opens it for
editing. This page follows a presentation through the engine and describes the parts it meets on the way, then a Word
document, the scenes both are drawn from, editing and large documents. Class and file names refer to `src/FreeSlate/`.

## Output

The document has fixed-size, absolutely positioned slides, one below the other. Every shape is an absolutely
positioned element holding an SVG: its geometry as paths, its fills, lines and effects as SVG paint and filters, its
text as glyph runs. Glyph outlines are defined once per font and glyph and placed with `<use>`; small text, which
PowerPoint hints to the pixel grid, is left to the browser. A transparent text layer keeps all text selectable and
searchable.

The renderers do not write this HTML themselves: they lay out each slide and page as a scene (see Scenes), which
`Rendering/HtmlScene` writes.

Two scripts are embedded: `Resources/player.js`, the slide show player (transitions, animations, media, hyperlinks,
zoom, presenter view), and, in documents with 3D models, `Resources/model3d.js`, a glTF reader and WebGL2 renderer.
Fonts the browser cannot find by name (fonts embedded in the presentation, Office cloud fonts) are embedded as
`@font-face` data.

## Reading the package (`Opc/`)

- `OpcPackage` reads the ZIP container tolerantly: parts, content types and relationships, also from damaged files.
- `OfficeCrypto` and `CompoundFile` open password-protected presentations (agile and standard encryption inside an
  OLE compound file).
- `MarkupCompatibility` resolves `mc:AlternateContent`: the first choice whose namespaces the engine implements,
  otherwise the fallback. It also annotates fallbacks with what their alternatives said (zoom targets, 3D models,
  Office 2010 chart styles), so that the renderers can use it.

## The presentation and its slides (`Pptx/`)

- `PresentationContext` holds the package-level state: cached parts, themes, images, fonts (with the presentation's
  embedded fonts), the slide size, the default text style and the regional format for chart numbers.
- `SlideContext` is one slide with its layout, master, theme (with theme overrides) and effective color map.
- `SlideRenderer` draws the background, the master's and layout's shapes and the slide's shapes. Every property of a
  shape is resolved through its inheritance chain (slide → layout → master placeholder → theme style matrix), see
  `Placeholders`.
- `TextRenderer` lays out and draws text: the sources of text properties in priority order, autofit, bullets,
  columns, vertical text, WordArt (`TextWarp`), 3D text, effects on text.
- `TableRenderer` lays out tables: rows grow to fit their text; table styles come from
  `Resources/builtinTableStyles.xml`, since PowerPoint draws only its built-in styles.
- `SlideShow` writes the transitions and the timing tree of each slide as JSON for the player; `Comments`, `Ink`,
  `Links`, `LegacyDrawing` (VML) and `Model3D` handle their objects.

## Drawing (`Drawing/`, `Html/`)

- `Drawing/Geometry` evaluates the preset geometries of ECMA-376 (`Resources/presetShapeDefinitions.xml`, 187 shapes
  and 40 text warps) and custom geometry with one formula engine.
- `ColorContext` resolves colors and their transforms (in linear light, as PowerPoint does) against theme and color
  map; `Fill` and `Outline` are the resolved fills and lines.
- `SvgPaint` turns fills and outlines into SVG paint; `SvgEffects` builds SVG filters for shadows, glow, soft edges,
  blur and reflections; `EffectDag` evaluates effect graphs; `BlipEffects` recolors pictures; `CompoundLines` and
  `LineEnds` draw compound lines and arrowheads.
- 3D scenes: `Camera`, `Lighting`, `LightRigs` and `BevelProfiles` reproduce PowerPoint's cameras, light rigs,
  materials and bevels, fitted to PowerPoint's rendering; `Pptx/Solid` builds the body of a 3D shape (bevels,
  extrusion, contour).

## Text (`Text/`, `Fonts/`)

- `TextLayout` is FreeSlate's own layout engine: line breaking (`LineBreaker`, a pragmatic UAX #14 with East Asian
  rules), the bidirectional algorithm (`Bidi`, UAX #9), tabs, indents, spacing and autofit. Glyph advances, kerning
  and spacing are rounded to PowerPoint's master units.
- `FontData` shapes text with HarfBuzz and reads the metrics PowerPoint uses; `GlyphOutlines` reads glyph outlines
  through HarfBuzz's draw API.
- `FontResolver` picks a font for each run like PowerPoint does: theme fonts, script fallbacks, font names that are
  not families, substitutes for missing fonts, synthetic bold and italic.
- `FontLocator` finds installed fonts, Office cloud fonts and the presentation's embedded fonts.
  `EmbeddedOpenType`, `MicroTypeExpress` and `CompactTableFormat` decode the fonts PowerPoint embeds.
- `Equations/` lays out Office Math (OMML) with the OpenType MATH table of a math font.

## Pictures (`Html/`, `Metafiles/`)

- Browsers show PNG, JPEG, GIF, BMP, WebP and SVG directly. `Tiff` (with `Fax` and `TiffJpeg`) and `JpegXr` decode the
  formats they do not; `Cmyk` shows CMYK pictures in the colors PowerPoint shows them.
- `MetafileConverter` plays EMF (`EmfPlayer`), EMF+ (`EmfPlusPlayer`) and WMF (`WmfPlayer`) pictures into SVG with a
  GDI state machine (`GdiContext`).

## Charts (`Pptx/Charts/`)

- `ChartModel` reads a chart part (`c:chartSpace`) with its cached data; `ChartExModel` reads Office 2016 charts
  (`cx:chartSpace`).
- `ChartRenderer` draws classic 2D charts: the automatic layout of title, legend, axes and plot area as PowerPoint
  computes it, axis scaling (`AxisScale`), number formats (`NumberFormat`), data labels, trendlines, error bars and
  data tables.
- `ChartRendererLegacy` supplies everything a chart does not format itself: the look of its Office 2007 chart style
  (`c:style`), made of the theme's fill, line and effect styles. PowerPoint draws classic charts by this look even
  where they carry chart style and color parts.
- `ChartRenderer3D` and `ChartRenderer3DPie` draw 3D charts with PowerPoint's projection; `ChartRendererEx` and
  `ChartRendererMap` draw Office 2016 charts (waterfall, histogram and Pareto, box and whisker, funnel, treemap,
  sunburst, region maps).

## SmartArt (`Pptx/SmartArt/`)

PowerPoint saves the drawing of every SmartArt diagram, which the slide renderer draws like any other shapes. For
diagrams without a saved drawing (PowerPoint 2007 and other producers), FreeSlate runs its own SmartArt engine: the
data model (`DataModel`), the layout definition instantiated for it (`LayoutTree`), constraints and rules
(`Constraints`), the layout algorithms (`Engine`: linear, snake, cycle, hierarchy, pyramid, connectors), text fitting
(`Fitting`) and the shapes as PowerPoint would have saved them (`Output`, `Styling`, `TextBody`).

## The player (`Resources/player.js`)

The player runs the slide show inside the document: the transitions (classic, Office 2010 and 2013 transitions and
morph), the timing tree with entrance, emphasis, exit and motion path effects, triggers and build steps, audio and
video, hyperlinks and action settings, zoom objects, custom shows, the presenter view and the slide show settings of
the presentation. `deck.html?frame=<ms>&fps=30#slide-3` renders a single moment of an animation, which the fidelity
tool uses to compare animations with PowerPoint's video.

## Word documents (`Docx/`)

A Word document shares the package reader, the drawing, text, picture, chart and SmartArt parts with presentations;
what is Word's own lives in `Docx/`. `DocxConverter` runs the steps:

- `ModelReader` (`Model.cs`, `TableReader.cs`) reads the body, headers, footers, notes and comments into blocks:
  paragraphs with their runs and resolved formatting (`StyleSheet`, `Formats`, `Numbering`), tables, drawings,
  fields, content controls; imported content goes through `HtmlImport` and `RtfImport`. Without markup it reads the
  document with all tracked changes accepted; with markup it keeps them, marked (`Review`).
- `Layout/LayoutProfile` names what the compatibility mode and the layout options change, read once per document;
  `Layout/RunStyles` turns run formatting into styles with Word's font metrics; `Layout/ParagraphLayout` sets
  paragraphs into lines by Word's rules (line heights, spacing, tabs, justification, kinsoku, bidi, vertical text);
  `Layout/TableLayout` sizes and lays out tables; `Layout/Paginator` breaks the body into pages: sections, columns,
  headers and footers, notes, floating objects and text wrapping (`DrawingBox`: a drawing's extent, effect extents,
  distances and wrapping), page borders (`PageBorders`, `ArtBorders`: Word's art borders tiled as Word tiles its pictures,
  the basic ones from their geometry, the others as FreeSlate's own motifs in their spirit, `ArtMotifs` from
  Resources/artMotifs.json); `Layout/FieldEvaluator` computes the fields
  Word recomputes when it prints.
- `Citations/` holds what citations and bibliographies need, which Word does not compute when it opens or prints a document (it
  shows their saved results): `SourceList` reads and writes the sources Word keeps in a custom XML part (b:Sources, the style chosen),
  `CitationRequest` a CITATION field's instruction (its sources, each with its switches, \m adding one), `CitationStyle` Word's twelve
  styles (`Styles/`: APA, MLA, Chicago, IEEE, Harvard - Anglia, ISO 690 by first element and date and numerical, Turabian, GB7714,
  GOST by name and by title, SIST02), each a citation's text and the bibliography's entries in English or German as Word's own style
  sheets make them (measured with a probe of all of Word's kinds of source, Word's style sheets not copied), `CitationMarkup` the
  markup Word's Insert Citation and Insert Bibliography write. `WordDocument.Sources`, `SetSources`, `RefreshCitations` (Word's
  Update Citations and Bibliography for the citations; the model reads a citation as the text it shows, so they are found in the
  paragraphs' XML) and `BibliographyMarkup`.
- Fields without a result and building blocks the model reads through: `WordDocument.AuthoritiesOf` reads the TA fields of a paragraph
  (Word's Mark Citation, left out of the model as XE fields are), `FieldsBegunIn` the instructions of the fields that begin in one;
  `GroupTexts` the paragraphs of a group's shapes, `TextBoxLink` and `NewStoryId` the chains of linked text boxes (wps:txbx id,
  wps:linkedTxbx id and seq); `GalleryBlock` and `RemoveGalleryBlock` a building block of a gallery as it stands in the body (Word's
  cover pages: a block content control of the gallery Cover Pages, its fields content controls named by their aliases).
- Forms, properties and mailings as Word's Developer, File and Mailings tabs keep them (`WordDocument.Forms.cs`, `.Properties.cs`,
  `.Mailings.cs`, measured with Word by COM): a paragraph's content controls (`ControlsOf`: check boxes, plain and rich text,
  drop-down lists, combo boxes, date pickers, which the model reads as the text they show; their placeholders' texts from the
  glossary), legacy form fields (`FormFieldsOf`) and fields (`FieldsOf`); the placeholders' building blocks made in the glossary
  document (`SettlePlaceholders`: one for each control, as every control showing a shared one shows the first one's content, which
  the model cannot change where it stands); Restrict Editing (`Protection`, w:documentProtection without a password); the core and
  extended properties (`Properties`, `SetProperties`: parts made where there are none, never an author not given) and the saved
  results of the fields that show them and the document's facts as Word's update shows them (`RefreshPropertyFields`: TITLE …,
  NUMWORDS and NUMCHARS as Word's statistics count, CREATEDATE, MERGEFIELD's «Name», and those the layout computes as the pages show
  them); a form letter (`FormLetter`, w:mailMerge without a data source), an envelope before the letter (`SetEnvelope`,
  `EnvelopeOf`, `AfterEnvelope`: a section of its own in DL, C4, C5 or C6, the addresses framed where Envelope.Insert puts them) and a
  sheet of labels (`SetLabels`, `LabelsOf`, `LabelSheets`: twelve of Word's products as MailingLabel.CreateNewDocument lays them
  out, a document of its own).
- `PageRenderer` writes every page as SVG and positioned HTML: glyph outlines placed with `<use>`, a transparent text
  layer, `Drawings` and `Vml` for DrawingML and VML objects, and with markup the pane, balloons and change bars
  (`PageRenderer.Markup`, `Describe`; a `WordDocument` read with markup as DocxConverter, `WordDocument.Balloons`); lines and glyphs
  stand on Word's 600 dpi device as it rounds onto it
  (`WordDevice`, `DeviceGlyphs`).

## Scenes (`Rendering/`)

A scene (`PageScene`, `Rendering/Scene.cs`) is a slide or page as it is drawn, from back to front, in points from its
top left: glyph runs with their fonts, paths with their paint, pictures, groups with their transforms, clips and
effects, and the places of the text (`PlacesItem`: for each character a line shows, the paragraph's id, the offset in
its text and where it stands). What is not broken down into items yet (charts, 3D bodies) is held as the SVG it is
drawn as (`MarkupItem`). `PptxConverter.ToScenes` and `DocxConverter.ToScenes` give them; two backends draw them:

- `Rendering/HtmlScene` writes the HTML document described above.
- `src/FreeSlate.Skia` draws scenes on a Skia canvas: PNG (`SkiaScene.Png`), PDF and PDF/A (`PdfExport`, fonts embedded
  as subsets made with HarfBuzz's subsetter, `Text/FontSubset`), and the viewer's pages. `TextPlaces` turns the places
  into hit tests, cursors and selections (a drawing turned or tilted hit where it shows, the outline the renderer gives
  for it, `PlacesItem.Outlines`, `Docx/Drawings.TurnedOutline`); the SVG of `MarkupItem`s is drawn by its own SVG
  reader (`SvgDrawing`).

A Word page's scene also says where the rows of its tables stand, drawing nothing (`TableRowItem`): the table's block, the
row of its model, where its grid's lines stand, its top and bottom, what its height has beyond its at-least height,
whether it is a head row repeated on a later page, and which of its lines show (upright on each line of the grid, across
over each grid column at its top and bottom, as the layout resolves the cells' and the table's borders). The table's layout gives each row its place in the table
(`Layout/RowPlace`, kept through a row's parts and copies), the paginator collects the rows it places on each page
(`PageBox.TableRows`); an editor finds a table's cells and lines on the page from them.

## Editing (`WordDocument`, `PresentationDocument`, `Editing/`)

- `IEditableDocument` is a document whose text an editor changes paragraph by paragraph: paragraphs go by the ids the
  places of its scenes name, a place is an offset in a paragraph's text (`Docx/ParagraphText`, `Pptx/SlideText`).
  Each change is a version that `Undo` goes back from and `Redo` forward to, and `Combine` takes the last few as one (an
  editor's step, words typed one after the other); a change the document cannot make leaves it as it was and throws
  `NotSupportedException`. Text can mean something without a look of its own: a character style (`TextFormatting.Style`,
  `CharacterStyles`), as a paragraph can have a paragraph style (`ParagraphStyle`), whose look the style definitions give,
  which `DefineStyles` replaces all at once as a style set does in Word, with the lists they name: a Word document is then
  read whole again, and so is a version with other styles that undoing goes to. A Word document's paragraph names the list
  it is numbered in by its formatting (`ParagraphFormatting.List`, `ListLevel`; `OwnList`, `ListLabel` read it back), and
  `AddList` makes a list of a numbering definition that starts over where it is first used (`WordDocument.Lists.cs`).
- `WordDocument` makes a change in the XML of the blocks it touches: their text is parsed afresh, changed
  (`Docx/TextEdits`; as tracked changes through `Docx/Tracking` where an author is set), made ready as the part was when
  it was loaded and read anew in place of those blocks (`ModelReader.Reread`, `Docx/Reread.cs`). The reader records the
  numbers each story asked for (`Docx/Numbers`), so that the lists after a change are numbered anew without reading
  their paragraphs again. Saving writes the changed stretches back into the bytes the part was read from
  (`Opc/PartText`), every other byte of the package as it was. Versions share the blocks they have alike, and the text:
  a stretch changed since the part was read keeps the texts it was read from in runs, a change the text of its own
  blocks alone (`Island`, `Piece`), so that typing on in a long stretch costs as much as the paragraph, not the stretch.
- `WordDocument` also takes markup of its own kind (`WordDocument.Insert.cs`): runs among the text (pictures, charts,
  shapes and WordArt, equations, fields), blocks next to a paragraph (paragraphs, tables, sections) and footnotes, with
  the parts they bring (a picture, a chart and its workbook, the notes). Each version keeps the other parts as it has
  them, so that undoing a picture takes its part away again with the drawing.
- `WordDocument` changes how a drawing stands among the text as Word's Layout Options and its Position gallery do
  (`WordDocument.Drawings.cs`, `DrawingOf` and `Format` with a `DrawingFormatting`): in the line or floating, the text
  round it (square, top and bottom, behind, in front), its place across and down (aligned or at an offset, from the page,
  the margin, the column or the paragraph), its size, its description for those who cannot see it and whether it is
  decoration only (wp:docPr descr, Word's adec:decorative). A drawing made floating with no place given stays where it stood,
  from its column and its paragraph, as the pages laid out show it; one floating keeps its place where none is given. A
  floating drawing's anchor is a character of its paragraph's text that no line shows: its place stands before the
  character after it, of no width, and the drawing's box among the places of its page (`PlacesItem.Floating`), so that a
  click on the drawing selects it and the cursor goes round it. `MoveDrawing` moves a drawing to another place of the text
  (in a run of its own with its run's formatting), `PlaceDrawing` puts a floating one where its box's top left is to stand
  on a page, as Word does when it is dragged there: by offsets from what it stands from (measured as laid out, and once
  more where the layout put it elsewhere), its anchor going to the paragraph of the body under its top (not while that top
  stays within its own paragraph, whose text a wide drawing pushes below it); `SizeDrawing` sizes one as Word does by a
  handle, the side it is sized from moving and its anchor staying; `TextArea` gives a page's columns and margins for lining
  up with.
- A picture's caption makes it a figure (`WordDocument.Captions.cs`, `AddCaption`, `RemoveCaption`): a text box where the
  picture stood (in the line or floating, wrapped and placed as it was, its name and description kept), as wide as the
  picture and resizing to its text (a:spAutoFit, no insets, no fill, no line), the picture in a paragraph of its own first,
  then the caption's paragraphs of Word's caption style (defined where the document has none). Word moves, wraps and
  deletes it as one drawing; the text box of a figure in the line takes the height it is saved with until Word fits it,
  so the engine keeps that height right as the caption changes (`FitFigure`, from the text's height as the paginator lays
  it out, `Paginator.TextHeight`), after a style's change too. A figure is sized by its picture (`SizeDrawing`: a corner
  keeps the picture's proportions, the caption grows with the width; `SizePicture` for a picture's own size), its
  description is its picture's too. `DrawingAround` gives the drawing whose text box holds a paragraph, `FigureOf` the
  figure of a caption or of its picture's paragraph. The room a picture's effects and line take round it stays the
  picture's in its line, the figure as wide as both and its caption indented by it (Word shows of a text box only what
  lies within its text).
- How a picture shows, as Word's Picture Format writes it (`DrawingFormatting` Crop, Shape, Recolor, Outline; a figure's
  picture likewise): the part of it shown (a:srcRect), its shape (a:prstGeom, an ellipse for a circle), its colors (one of
  a:grayscl, a:biLevel at half, a:lum as Word's washout), the line round it (a:ln, with Word's effect extents for it: its
  width up to the next 1.5 pt). `WordDocument.Pictures.cs` crops one at the scale it has (`CropPicture`: a floating one's box
  moving with the sides cropped so that the picture stays where it stands, or staying while the picture moves under it) and
  shows another picture in one's frame (`ReplacePicture`: a part of its own, the frame as it stands and shows, the new one
  filling it at its middle, `Filling`). `EditSession` crops to proportions about the middle of what shows, no larger than the
  room there (`CropPicture(double)`), and to a circle (`RoundPicture`); the size it took when it was put in is its part's.
- How a picture turns, as Word's Rotate, Flip and 3-D Rotation write it (`DrawingFormatting` Rotation, FlipHorizontal,
  FlipVertical, Tilt; a figure's picture turns in its frame, the caption staying level): a:xfrm rot, flipH and flipV of its
  pic:spPr, and a:scene3d with a preset camera, X, Y and Z turns in place of the preset's and a perspective's field of view
  (`PictureTilt`, its `Angles` the preset's where none are given), with Word's three-point light. Word gives a picture by
  itself the effect extents of what it covers turned and seen through its camera, and lays it out by them (`Margins`,
  measured in Word by COM: the box swapped from 45 up to 135 degrees, tops and bottoms up to the next 1.5 pt, a tilt about
  3 pt more, a line by its whole width); a floating one whose effect extents reach above its offset stands above its
  paragraph (`Paginator.Above`). Word and PowerPoint turn the picture a camera shows after the camera (`Camera.TurnsPicture`,
  legacy cameras the shape before). Cropped, a turned or mirrored floating picture's box moves so that the part kept stays
  where it shows. `EditSession` turns by quarters and more (`TurnPicture`), mirrors (`FlipPicture`), tilts (`TiltPicture`)
  and stands a picture straight (`StraightenPicture`); `JoinSince` makes the steps since a point one, as an editor's
  handlings that belong together.
- Tables are put in as Word's Insert Table puts them (`WordDocument.TableMarkup`, `EditSession.InsertTable`, measured in Word by
  COM): the paragraph split where the cursor is (none at its start), columns of even width over the text's width less 10 twips,
  a table style and here one for the text in its cells, the first row its head, repeated on later pages. Tab goes to the next
  cell and selects its text, in the last a row like it is added (`NextCell`). A table is changed as a whole
  (`WordDocument.TableEdits.cs`, `Docx/TextEdits.Tables.cs`): its shape is read as the rectangles of its grid its cells cover
  (`TableShape`: merged across, w:gridSpan, down, w:vMerge), changed there and written back as Word writes tables, the
  paragraphs moving with their cells: cells merged (the content of those not empty together), split along a line down or
  across as Word's pen splits them (a new line of the grid, a new row, the neighbours spanning it), rows and columns put in,
  taken out and moved (a table keeps its width where Word makes it wider or narrower), the rows sorted by a column (numbers
  by value, in the document's language), rows turned into columns with the look's marks of the first row and column turned
  too (a head row a head column), columns made even, a line of the grid moved as Word moves it (the column after it giving
  way, the outside ones moving the table's edges) and a row made higher, a line between cells taken away as Word's eraser
  takes it (`EraseLine`: each stretch of it merges the cells on either side, into the smallest rectangle that holds them,
  `Enclosing`), a head row put on or taken off (`SetHead`: the look's mark and the repeat on later pages), another table style
  (`StyleTable`), a cell split into two or three of even size (`SplitCell`), what cells hold taken away (`ClearCells`, as Word's
  Delete over cells). Their look as Word writes it where cells' sides are given lines (measured by COM): a stretch of a line of
  the grid given a line of its own or none on the cells' sides on either side of it (`SetLine`, w:tcBorders, nil for none; a
  rectangle's sides and inside lines, `SetLines`), cells shaded (`ShadeCells`, w:shd), the table centered or right in the table's
  and each row's w:jc (`AlignTable`, its indent gone; a centered table's outside lines move both edges) or as wide as the text
  again (`WidenTable`), the text's place in cells as the nine buttons of Word's Layout tab set it (`PlaceText`: w:vAlign and the
  paragraphs' w:jc, neither where it is the top or the left unless the styles give another place; `CellPlace` reads it; both also as
  the page shows it in a turned cell, where vAlign moves the lines across and jc along), cells' text turned as Word's Text Direction
  turns it (`TurnText`: w:textDirection btLr or tbRl, the paragraphs 113 twips in at either end of their lines, the row at least
  1134 twips high and kept on one page; turned back, all of it gone again; `CellDirection` reads it), the table floating beside
  the text as Word's text wrapping "Around" writes it (`FloatTable`: w:tblpPr 141 twips from the text, at its column's left, right
  or middle, below the top of the paragraph after it, w:tblOverlap never; one wider than 60 % of its column half as wide;
  `TableFloat` reads it), a floating table put where its top left is to stand on a page as Word puts one dragged by its handle
  (`PlaceTable`: offsets from its column and from the paragraph after it, the table going before the body's paragraph at its top,
  measured as laid out and corrected once), a table moved in the text before another paragraph (`MoveTable`). Cells
  are copied as a table of their own (`CopyCells`) and put over cells from a place on as Word's Overwrite Cells puts them
  (`PasteCells`: rows and columns added as they need, each cell taking the content, colour and place up and down of the copied
  cells that begin in it, the table's lines staying its own), a spreadsheet's rows of text so too (`PasteTexts`). A table is known
  by its block's id
  (`TableOf`, `CellsOf` gives its cells), which it keeps; a change that makes more or fewer cells reads its paragraphs anew,
  so each change names the paragraph to go on in (`EditSession.ChangeTable`). Tables with tracked changes, content controls
  or custom markup among their rows, or the old way of merging across, are not changed so. A selection reaching from one cell
  into another selects whole cells, as Word does (`EditSession.CellSelection`, the rectangle holding both): Delete and Backspace
  empty them, text typed over them takes the first one's place alone, formatting goes to all their text, and rows and columns
  put in beside them are as many as they span. Copied, they give a spreadsheet's rows as their text (tabs between cells, a line
  end after each row) and their markup as a table; pasted, they go over the cells from the cursor's on, at the start of a
  paragraph right after a table as rows at its end (Word joins tables that meet), elsewhere as a table of their own, and text of
  a spreadsheet's rows pasted in a table goes into its cells. A picture put in in a cell is no wider than the cell's text; in lines exactly so
  high (the notebook's) its paragraph's lines become at least so high (`WordDocument.OpenLines`), as Word cuts it at their height.
- Sections are broken as Word's Layout > Breaks breaks them (`WordDocument.Breaks.cs`, `BreakSection`, measured in Word by COM):
  at a paragraph's start in an empty paragraph whose w:sectPr is a copy of the section's, the references of its headers and
  footers with it, after a paragraph's text with that paragraph (`TextEdits.EndSection`); the part after the break keeps the rest,
  linked to the part before for its headers and footers, beginning as the break's kind says (w:type). Word gives the part after a
  break the section's first page of its own and its first page's number again; only the part before keeps them here, as for a
  table's pages across. A section's properties as written (`SectionMarkup`) end a section again where a change took them away.
  Page setup gives a section columns of widths of their own (`SectionFormatting.ColumnWidths`, w:col with equalWidth 0) and its
  notes' numbering (`Footnotes`, `Endnotes`: w:footnotePr's numFmt, numStart, numRestart, as Word's Footnote dialog writes them for
  a section); the document mirrors its margins on facing pages (`MirrorMargins`, w:mirrorMargins in the settings). A paragraph's
  runs' own properties are read as written (`RunProperties`: each stretch's w:rPr), for the editor to say them.
- `WordDocument` keeps other versions of a section's text and notes for planning it (`WordDocument.Versions.cs`,
  `Docx/SectionPlan`) in the paragraph that begins the section, in an alternative of markup compatibility only the engine
  can choose: `mc:AlternateContent` with `mc:Choice Requires="fs"` (namespace `urn:freeslate:plan`) and an empty
  `mc:Fallback`, a version's blocks as CDATA. Word shows none of it and writes none of it back when it saves, what only the
  versions refer to going with them; so a version shares the document's ids and no version ever reaches what renumbers
  them. Putting a version in place moves text within the main part, one change to undo. A copy without plans
  (`SaveWithoutPlans`) leaves out the versions and what only they refer to, for a file that goes to others.
- Saving (`WordDocument.Save`) leaves out what editing left unnamed (`Unnamed`), as Word's saving leaves out a deleted
  picture: relationships of the kinds a part names by their ids (pictures, charts, diagrams, embedded objects, links,
  headers and footers, media) that the main part, or a part editing changed, no longer names, where it named them when
  the document was opened or they came since, and the parts only they reached, with their relationships and content
  types. What a file left unnamed already stays as it was (the 5603 documents of the corpus save with every part they
  had), and the package keeps everything in memory, which undoing brings back.
- `PresentationDocument` makes its changes in the XML of the slides (`Pptx/SlideEdits` for text, `Pptx/SlideShapes` for
  shapes, `Pptx/SlideList` for the list of slides and the parts around it), reads the slide again and draws it anew;
  text that shrinks to fit its shape, and shapes that grow to their text, are fitted anew as PowerPoint fits them.
- Tracked changes as Word's Review tab has them (`WordDocument.Revisions.cs`, measured in Word by COM on probes of every kind):
  `Revisions` lists the body's as Word's Revisions do (neighbouring elements of a kind and an author one revision whatever their
  times, a paragraph's mark a character of it, an inserted or deleted row its cells, a table's formatting changes one for each author,
  ordered by where they begin), `RevisionsOf` gives them as they stand in a paragraph's text as it reads accepted, `RevisionMarkup`
  their XML; `AcceptRevisions` and `RejectRevisions` settle all of them in every story, an author's or those named by their ids (a
  move with its other half) in the XML they stand in (`Docx/TextEdits.Review.cs`: a mark inserted and rejected or deleted and
  accepted joins its paragraph with the next, which keeps its properties; formatting rejected takes back what it kept); each one
  change to undo. `TrackRevisions` is Word's Track Changes (w:trackRevisions). `MarkChanges` marks what changed since the body read as
  `BodyMarkup` gave it as the author's tracked changes, as Word's Compare marks a revised document (`Docx/TextEdits.Compare.cs`:
  paragraphs paired by their words, the words that differ deleted and inserted, formatting with the look it had, paragraphs and rows
  that came or went inserted or deleted, the changes the blocks held kept; compared without the ids of content controls and notes'
  references and without simple fields' results, so that a citation, bibliography, cross-reference or note made anew alike is no
  change, and a note's reference never deleted, as its note goes with it). `EmbedFonts` embeds fonts as Word's Embed fonts in the
  file does (obfuscated .odttf parts named in the font table, w:embedTrueTypeFonts), `FontFamilies` names those the document sets
  text in, `FontOf` a paragraph's.
- `Editing/EditSession` is an editor's work without its surface, over either kind of document: the cursor and the
  selection as places, typing, Backspace, Delete, Enter, formats, copy and paste, markup, blocks and notes put in, each
  handling one step to undo, whose changes the document keeps as one version (typing on adds to it); what else a
  document offers (a Word document's versions of a section, its comments) goes through `Handle` as a handling of its own.
  Word's units of the text are there too: the word a double click selects, with the spaces after it (`Editing/Words.Unit`),
  the paragraph with its mark a triple click selects, and where Ctrl with the arrows, Home and End goes (`Move`).

The rules of each change (where new text goes, which paragraph keeps its properties and its mark, what a tracked
change records) were measured in Word and PowerPoint through COM, as the rendering rules were.

## Large documents

A Word document of thousands of pages opens with its first pages at once and grows from there:

- The main part's body is read in portions (`Docx/BodyStream`), the model of the first children before the rest.
- `Layout/Paginator` lays out the first pages and the rest in slices of about 10 ms (`WordDocument.LayOut`, while
  `LayingOut`), as an editor does while it waits for input, leaving checkpoints: the paginator's state where pages begin.
  What comes before the pages takes such slices too: the XML, the sections, the blocks, the notes. After a change it
  lays out again from the last checkpoint before it (`Paginator.Relayout`) and stops where a page begins as it began
  before (`SameFlow`); where a change moves everything after it on, it lays out a few pages and leaves the rest to the
  slices. Fields that read the pages (a table of contents) are laid out again once their pages are known.
- Pages are drawn when they are asked for (`Scene(page)`; `Scenes()` gives the others as scenes of their size without
  items), and the scenes of the 32 pages around the last one asked for are kept.
- What would walk the whole document on every change does not: the children of the body are found and taken out in
  constant time (`Docx/Siblings`), formats are shared between the paragraphs and runs that have them alike, laid out
  paragraphs are kept for the next layout.

## The editor (`src/FreeSlate.Editor/`)

The editor is a word processor of its own on top of the engine, the same on every platform: `FreeSlate.Editor` holds the
interface, `FreeSlate.Editor.Desktop` and `FreeSlate.Editor.Browser` only start it (in the browser `Settings.Read` and `Write`
go to the page's storage, and the engine gets the app's Inter as its only font). It opens on a new document
(`WordDocument.New`) in `DocumentView`, which draws the sheets from their scenes with Skia and sends keys, typed text and an
input method's text to an `EditSession`. It neither opens nor saves files yet. Text is selected as in Word: a double click takes
the word, a triple click the paragraph, the pointer held on from there whole ones; Shift with the arrows, Home and End moves the
selection's moving end, with Ctrl by words and paragraphs and to the text's start and end; without Shift an arrow leaves a
selection at its start or end, and up and down keep the column they started from.

A document says what its text means, not how it looks (`Writing/`): a meaning is a character style (`Meanings`), and a tone
(`Tones`, the style sets in `Tones/*.xml`) gives the meanings and the paragraphs' levels their look, the whole document at once,
a change of it one step to undo, which the sheets in view fade through (`DocumentView.Restyle`). The file stays a Word document
that looks alike in Word. The editor's documents hyphenate (`WordDocument.New(hyphenate)`, Word's w:autoHyphenation, a setting of
the whole document): the magazine tone justifies and hyphenates its text, the others suppress hyphenation in their normal
paragraph, which the other levels are based on. `DocumentView.ShowsMeanings` frames the stretches with a meaning and names it in the page's margin,
drawn over the pages, which do not move for it. The engine gets the app's Inter beside the system's fonts (`EngineFonts`).
A paragraph's level lies on one axis from title to margin note, points and numbered points among them (`Levels`, Word's own
styles where it has them): `LevelLever` is the grip beside the paragraph the pointer is at, dragged sideways a level per 32
pixels with the page showing it at once, one step to undo; Tab and Shift+Tab move the selection's paragraphs along the outline
(a point to a subpoint, numbered ones staying numbered, text after a numbered point becoming one), Enter in an empty point moves
it out. Numbered points one after another are a list that counts from 1 (`Lists`): each list is a list of its own of the tone's
numbered points (`WordDocument.AddList`, a w:num that starts over where it is first used, as Word's Restart Numbering makes
one), which each of its paragraphs names (w:numPr, `ParagraphFormatting.List`), so that a list after text starts at 1 again,
however many came before, and text put between two points leaves those after it counting on, in Word as in the editor. Meanings are
given with `GestureRing`, a marking menu whose fields share the circle in an order that stays (six: the five meanings, then
taking one away): the right button held on the selection or a word, moved blindly or into a field; a short click opens it to be
clicked; the left button held on the selection, or Space held, bring it too. A field may hold a ring of its own (a second level,
a picture's places on the page, later inserting tables and more), which opens where the pointer leaves the field, on a click, by
a turn of a blind stroke, or by Enter. Its middle is the way back: the sign of the field that holds it on a button of glass there
while no field is pointed at, lit as a field where the pointer is in it, named "Back" below the ring; a click there, letting go
there once the held pointer was on its fields, or Backspace bring the ring before again (let go there at once, it stays to be
clicked). A ring to be clicked goes with the pointer pressed in its middle and moved on, a little raised, the rings before it
along, wholly in view; let go without moving, the press there is a click. The ring is always of one size and shows signs only,
light, set by their ink, each in the middle of its
field with room all around and no lines between the fields: a band around an inner circle, the band's glass nearly whole and the
middle clear; the field pointed at shows as a rounded part of the band, its name beside the ring. It lies over everything; brought
on the text, it stands centred on the selection's part there, as far as the point it was brought at stays in its middle. The
whole selection is framed exactly, one outline around the lines that meet (`DocumentView.SelectionAreas`), under the ring, and
reads as the field pointed at would make it: a preview, a step of its own that letting go keeps and anything else takes back, the
selection hidden meanwhile (`GestureRing.Preview`, `DocumentView.SelectionHidden`). In full screen
the line above the sheets lies under the window's buttons and comes and goes with them (`CaptionBar.Changed`).
Pictures go in as Word puts them in from a file (`EditSession.InsertPicture`): in the line at their own size, no wider than the
text of their column; dropped on the sheets (a bar shows where they would go), or pasted where the clipboard holds no text (picture
files copied from a folder, else a picture itself), kinds Word does not keep as PNG. They are made ready first (`Skia/PictureFit`):
upright as their camera says (EXIF orientation, which Word does not follow in a document), and no larger in pixels than their size
needs at Word's default 220 pixels an inch, a picture made anew in sRGB and without the camera's metadata. A click on a drawing selects it as a whole and
frames it instead of tinting it; the frame is the drawing's own box, which the renderer gives among the line's places
(`PlacesItem.Boxes`, `TextPlaces.Box`); one in front of the text is hit first, one behind it only where no text lies over it, one
turned or tilted where it shows (`TextPlaces.OnDrawing`); the ring frames a turned picture turned (`GestureRing.Turned`). On a
picture the ring has the picture's fields (`GestureRing.Choose`, `Writing/Placements`): in the line up, beside the text right and
left, on its own between the text down, in front of the text and behind it between them, a place on the page down right (a
ring of the eight places round the margins, each in its direction), and up left the picture itself (a ring: a caption up, the
cursor then in it, or taking it away; cropping it by hand; its description, written on a card under it with whether it is
decoration only, `Controls/DescriptionCard`; its colors, chosen on a strip under it that shows the picture small in each look and
the one pointed at on the picture itself, `Controls/RecolorStrip`, `Writing/Looks`; turning it, a ring of its own; another
picture from a file in its frame; removing it; a hairline round it, or taking it away). Turning has straight again up, mirrored
across and down, a quarter turn right and left, slanted (Word's isometric view, the look of shearing, which Word does not have),
the size it took when it was put in, and tilting it in space by hand. Their signs are icons, the picture a frame
among lines of text. A double click on a picture, or the ring, crops it by hand (`DocumentView.Cropping.cs`): the whole picture
faint over the page round its frame, marks at the frame's corners and sides that crop it there (Shift at a corner keeps the frame's
proportions), the picture moved under its frame where it is pressed, each let go a step; the ring there offers the whole picture,
proportions turned as the picture stands and a circle; Enter, Escape or a press elsewhere ends it. While a picture is cropped it is
no selection for the ring's left hold, and the second press of a double click is the sheets'. A turned or mirrored picture is
cropped as it shows, its marks turned with it; a tilted one shows flat while it is cropped, as Office shows it, and is tilted again
after, the flattening, the crops and the tilt one step (`EditSession.JoinSince`).
A knob above a picture's frame turns it about its middle (`DocumentView.Turning.cs`): its angle beside the pointer, at the quarter
turns it stays a little (Alt held, not), with Shift by 15 degrees, as a step that goes on, Escape takes it back; the frame and its
handles turn with the picture, the handles sizing it along its own sides, the side opposite staying where it shows. Tilted by hand,
a drag on the picture turns it in space as a trackball would, in perspective (across about its upright axis, up and down about its
level one), a globe round it showing how; the ring there has Word's presets in their directions; Enter, Escape or a press
elsewhere ends it. A preview that changes nothing makes no step and leaves nothing to take back.
A caption is text where the pointer is in one of its lines, its picture selects the figure (`DocumentView.TextHolder`);
the arrows keep to the text the cursor is in (a drawing's text box is a text of its own, `DocumentView.StoryAt`): the body passes
over captions, up or left out of a caption select its picture, down from a picture selected in the line goes into its caption,
down or right past a caption's end back to the text after the picture; Backspace at an empty caption's start takes it away. The
level lever goes by the text's own paragraphs, not a caption's. A preview the ring takes back leaves nothing to do
again (`EditSession.TakeBack`). A picture is dragged with the main button (`DocumentView.Dragging.cs`): in the line its outline
goes with the pointer and a bar shows where it would go in the text; floating it follows the pointer over the pages, the text
going round it as it goes (a step taken back and made anew as it goes on, kept where it is let go, Escape takes it back), its
anchor's paragraph marked in the margin. Near a column's edges and middle and the margins it lines up with them, a line along
each (Alt held, not); at a column's edge or middle of a page of one column it stays aligned there. The handles at the corners and
the middles of the sides of the picture selected size it the same way, as a step that goes on: a corner keeps its proportions, a
side stretches it, the opposite side stays and an alignment there too, a side it moves lines up with the columns' edges and the
margins. Near the view's top or bottom a drag scrolls the pages, the faster the nearer (a picture dragged or sized, or the
selection, reaching on). The arrows move a floating picture a point, ten with Shift; arrows one after another are one step.
A table goes in from the ring at an empty spot (down right): its own ring is split (`GestureRing.Slot.Split`), the columns on its
right, one more up and one fewer down, the rows on its left alike, fields that step and leave the ring open (`Slot.Steps`). The table
shows at the cursor as it would go in from the moment that ring comes (three columns, the head and two rows), growing and shrinking
with each step, its size in a line under the ring (`Slot.Status`). The way back in the ring's middle is split as the ring is
(`Slot.Settles`): its left half (a tick) puts the table in, one step, its right half (a cross) takes it away, either back at the ring
before; Enter puts it in and closes the ring, a click beside the ring, Escape and Backspace take it away (`Slot.Opened`, `Slot.Left`). Tab goes from cell to cell, a row more from the last one, Shift+Tab back. The tones give
tables their look (`Writing/Tables`: a table style and a style for the text in cells, which has no other style below it so that
the table style's fonts reach the text: lines between the rows in the plain tone, a book's rules in the magazine, a grid in the
notebook, the head's look also for a head column). While the cursor is in a table, the lines of its cells that do not show are
dashed and faint over the page (`DocumentView.Tables.cs`, from the scene's rows), so that a look without lines still shows its
columns. Dragged from one cell into another, the selection takes whole cells and shows them whole: darker in their own colours
(multiplied by a warm grey, so that a cell's colour stays its colour) and framed in the accent. Up and down
go from a cell's first or last line to the cell above or below, at the column, and out of the table at its ends; a click in a
cell's empty part goes into that cell. A line between cells is dragged where the pointer shows a sizing arrow over it: an upright
one moves as in Word, the column after it giving way, the table's sides moving its edges, lined up with the text's edges and the
lines of other tables (Alt held, not), one across makes the row above it higher; the length beside the pointer, a step that goes
on as for pictures, Escape takes it back.
A table is shaped by hand after a double click on a line between its cells, or from its ring (`DocumentView.Shaping.cs`): it is
framed in the accent and all its cells' lines show; the pointer is a pen over its cells and an eraser over its lines. A stroke
across cells draws a line from side to side of each and splits them (at a line of the grid within reach, else at a new one), a
stroke along a line takes it away and merges the cells beside it; the line and the cells it touches show lit while it is drawn,
let go it is one step, Escape takes a stroke back. Enter, Escape or a press beside the table ends it. The ring in a table's cell
(at a spot with nothing to act on) has the cell's row up (a row above, below, the row's cells merged, the row taken away), its
column right (sorted up or down by it, a column left or right, taken away), shaping the table down (by hand, or the cell split
in two or three columns or rows), and the table left (rows turned into columns, columns made even, where it stands between the
text's edges, its look, the table taken away), each laid out as the table is and each a preview; between them the cell's colour
(soft swatches, `Tables.Fills`, drawn as discs in the ring, `GestureRing.Slot.Swatch`), what goes in it (a picture), its lines
(its frame's look) and where its text stands. That opens a pad under the cells (`Controls/PlacePad`): the nine places three by
three as the page shows them, and below them the ways the text runs (across, turned up, turned down), the places drawn running
so, the cells' own marked, the one pointed at named below; the pointer or the arrows on one show it in the cells, a click or
Enter keeps it, as a picture's colours are chosen (a way kept, the pad stays open). In a turned cell the cursor lies across the
lines and the arrows go as the page shows them (`DocumentView.TurnedKey`, `TurnedMove`): along the lines to the next character,
across them to the line beside, from the cell's ends and outer lines to the cell beside it that way. On cells selected together the ring has their rows and
columns, their colour, merging them, their text's meaning, their lines (which ones, each in its direction, then how they look)
and where their text stands; Delete empties them, Ctrl+X cuts them (rows or columns selected by their grips go). On a line itself (right-clicked within reach) the ring is
the line's: hidden, fine, strong, double, dotted, dashed (`Tables.Lines`, in the tone's colours) or as the table's look gives it,
for the whole line straight through the table, framed in the accent, or in its own ring for the stretch under the pointer alone.
Its look's ring has the tone's own up, round it the kinds (a grid of light lines, light lines between the rows, a book's rules, a
frame) and a head row or none down (`Writing/Tables.Kinds`): each kind is the tone's table style with other lines, made when the tone
is (`Tables.Made`, in the tone's colours for lines and for rules, `lines` and `rules` on `Tones/*.xml`), so it keeps the tone's type,
margins and head and changes with the tone.
Beside the table the pointer is at its rows and columns have grips, left of the row and above the column, faint, in the accent
where the pointer is on one, a plus between two (`DocumentView.Grips.cs`): a click on a grip selects its row or column (Shift
another one up to it, their cells selected together for the ring; Delete or Backspace takes them away), dragged along the table it moves them before another (rows and
columns of their own, a bar showing where), dragged out of the table it takes them away (tinted as going), a plus puts a row or a
column in there; each one step. The level lever does not show beside a table. Off the table's top left corner a handle shows
with the grips (`DocumentView.TableMoving.cs`): a click selects the whole table; dragged, a floating table follows the pointer
over the pages, the text going round it as it goes, lined up with the columns' edges and middles (a step that goes on as for
pictures, Escape takes it back), and one in the text goes between its paragraphs where a bar shows. The table's position ring
has the places in the text (middle up, right down right, across down, left down left) and floating beside the text (right up
right, left up left).

Claude works at the ring (`Asking/`): text typed while a ring shows goes to a line under it instead of a field (Ctrl+Space opens
the ring at the selection or the cursor, ready to type), and Enter sends it as a wish, with no mode to choose: where the ring was
opened says what the wish is about (`Focus`: words, paragraphs, the cursor, an empty paragraph, a picture, a table). The document
goes to Claude in a notation of its own (`Reading`, `Notation`): each paragraph a line after its id, Markdown for the levels and
the meanings ({Term|…} for those Markdown lacks; numbered points by the numbers they show, "1." and "  a)", a list written after
text starting at its first number or going on in the list before where that number follows its last), GitHub's tables with what the ring gives tables besides (a line
"{table look=book head=none float=right across=yes}" before one, "{fill=yellow valign=middle turn=up}" at a cell's start, "<"
and "^" for cells merged with the one before or above, see `TableWords`), fenced blocks of a vector picture (```svg), of
WordArt (```wordart), of a box (```box: Word's text box with paragraphs of the notation inside, a fill, a line, as high as its
text, see WordDocument.FitTextBox), of a shape (```shape: any of DrawingML's 187 preset shapes or a short name for one, with
text inside, filled, outlined, turned, mirrored, with a shadow; lines with arrowheads; read back as the block, a rectangle
growing with its text as a box), of a chart (```chart: a GitHub table of categories and series made a chart part with its
data in an embedded workbook, `Charts`: Word's classic types as c:chartSpace, in 3D, stacked, combined with lines and a second
axis, with trendlines, error bars and a data table; Office 2016's waterfall, funnel, treemap, sunburst, histogram, Pareto and
box and whisker as cx:chartSpace behind markup compatibility, with a chart style and colour style of our own beside it, without
which Word does not open the document) and of SmartArt (```smartart layout=cycle colors=blue with its points as "- " lines:
a data model, colours and a style of its own and one of 36 layout definitions written for FreeSlate, none of Office's
(`Asking/SmartArt/*.xml`: list, columns, blocks, process, steps, chevrons, bending, stairs, timeline, cycle, radial, venn,
equation, target, gears, opposing, matrix, pyramid, inverted, org and tree, whose levels are written out to five as the layout
language has no recursion, horizontal, hexagons, trapezoids, table, vchevrons, arrowprocess, upward, continuouscycle,
segments, pie, linearvenn, funnel, converging, diverging and plusminus; those that place each point by their count choose by
it, a layout taking no more points than it draws), without a drawing,
which Word and the engine lay out alike from the definition; Word takes a definition only with names that are unique among
all its elements and connection points it knows (midR, not rCtr), `SmartArts`), formulas in LaTeX ($…$, $$…$$, made Office Math and read back so, `Formulas`), links
([text](address) as HYPERLINK fields, in the tones' link style), fields ({page}, {pages}, {date}), "::: columns 2 line" to
":::", footnotes and endnotes (a note's text beginning "(endnote)"), a table of contents ("{contents Title}": Word's TOC field over
the headings of levels 1 to 3 in the styles toc 1 to 3, made once the write is in, each entry linked to a _Toc bookmark round its
heading and its page a PAGEREF field, read back as one line with its first and last paragraph, `Contents`; "{figures Figure}" a table
of figures, the same over the captions numbered with a label, TOC \c, in the style table of figures; "{index}" an index, Word's
INDEX field over the terms "{index term}" marks among the text as XE fields, which the model leaves out and `WordDocument.IndexEntriesOf`
reads from a paragraph's XML, a heading for each first letter and the pages as they show their numbers, `Index`), direct formatting
where the wish asks for a look (`RunLook`: ==marked==, ~~struck~~, H~2~O and x^2^ as Pandoc writes them, else the words of Word's
Font dialog before the text, "{font=Georgia size=14 color=#C00000|text}", a character style's id first for a meaning with a look
besides; written as Word writes them, measured by COM, and read back from the runs' w:rPr, `WordDocument.RunProperties`, those
with properties the notation does not say, Word's text effects, as their WordprocessingML; `ParagraphLook`: a paragraph's own
alignment, indents, spacing, line spacing, keeping, page break before, outline level, lines round it, shading and tab stops as
"{align=right after=12pt}" before its text, so that a letter as DIN 5008 sets it out can be written), section breaks as Word's
Layout > Breaks makes them ("\sectionbreak continuous" on a line of its own, after a paragraph ending the section with it, a
break taken away by a write giving its section's properties to the one written in its place, `Writing.Sections`; read back as
that line after the paragraph that ends the section, but where the columns' marks or a table's pages across say it, each section
after a break told as "Page from ¶12:", `Reading.Sections`), "\columnbreak", columns of widths of their own ("::: columns 2
widths=2:1 space=1cm"), citations and
bibliographies as Word's References tab makes them (a ```bibtex block's entries the document's sources in Word's kinds, its style=…
Word's citation style; "[@key]", "[see @key, p. 33; -@other]" Pandoc's citations as CITATION fields in w:citation controls;
"{bibliography}" Word's bibliography building block of all the sources; the citations' and bibliographies' text set as the style
formats them once the write is in; read back as written, the bibliography as one line and the sources after the document, `Citing`;
a block-level content control goes in as the blocks it holds, `WordDocument.InsertBlocks`), numbered captions and
cross-references as Word's Insert Caption and Cross-reference make them ("Figure {#sales}: …" in a caption: a SEQ field of the
label in a bookmark of the name with the label; {#name} elsewhere a bookmark round its paragraph's text; {ref name} and {page name}
REF and PAGEREF fields; the fields' results saved as the pages show them once the write is in, `WordDocument.RefreshFields`, as
Word shows them saved till it prints), drop caps ("{drop}" at a paragraph's start, "{drop 2 margin}": its first letter in a framed
paragraph before it, `WordDocument.DropCap`, of the lines' height exactly and the size its capitals need to reach the last line's
baseline, as Word computes them; read back as one line with both paragraphs' ids), a table of authorities as Word's Mark Citation and
Insert Table of Authorities make one ("{authority Cases: long citation | short}" a TA field, "{authority short}" a later mention, read
back at the end of the paragraph; "{authorities}" a TOA field for each category with authorities, TOA \h \c "1" \p, the fields back to
back, headings in the style toa heading, entries in table of authorities with a tab and dots to the pages, "passim" from five on, as
Word's own update of them by COM gives them; read back as one line, `Authorities`), a signature line (```signature signer="…" title="…"
email="…": VML with Word's o:signatureline and a picture of the line, `Signatures`), groups of shapes (````group holding ```shape and
```box blocks at their places: wpg:wgp, its shapes in its own space, read back as the block) and boxes whose text flows on (link=next:
wps:txbx id and wps:linkedTxbx, in the body or in a group, `Groups`), a cover page ("{cover style=band title=… …}": a building block of
Word's gallery Cover Pages at the document's start, in place of the cover it has, in one of FreeSlate's own designs, band, bar, frame
or plain, in the tone's colours, its fields content controls named and bound to the document's properties as Word's are, which take
their values: title, subject and author, the cover page properties' date and abstract, `WordDocument.SetCoverPageProperties`; read back
as one line, `Covers`; a write from its
first paragraph on takes it away whole first), tracked changes as CriticMarkup writes them with their changes' ids after the closing
marks ("{++inserted++12}", "{--deleted--13}", "{==formatted==14}", "¶" in one a paragraph's mark; listed after the document as
"Changes:"), written back as read keeping each its author, time and kind, a deletion its XML, a formatting change its looks
(`Reviewing`, known by their ids as a write begins), one without an id a new change of Claude's, forms as Word's Developer tab makes them among
the text (`Forms`: "{checkbox}", "{text Your name}", "{richtext …}", "{choice Yes|No}", "{combo …}", "{date dd.MM.yyyy}", "= …" what
one holds; content controls whose placeholders become building blocks of the glossary once the write is in, one each; Word's legacy
"{formtext}", "{formcheckbox}", "{formdropdown A|B}" with their form data and bookmarks), mail merge fields ("{merge FirstName}",
showing «FirstName», the document a form letter once one is written) and the fields of the document's properties and facts
("{title}", "{words}", "{created}", "{sectionpages}" …, their results as Word's update shows them once the write is in), read back as
written, \pagebreak and --- for a page break
and a line, and WordprocessingML for anything else: a paragraph with
formatting the notation does not say reads as its WordprocessingML, a section's end as the columns' marks and the tables' pages
across say it, so that what Claude reads and writes back stays as it was. The document's objects (its
pictures, graphics, WordArt, charts, SmartArt, text boxes, frames, fields, equations) stand in its text as tokens ("[picture 3]"),
told after it with where and how each stands, a graphic's SVG and the block of WordArt, a chart or a SmartArt (`Objects`); its notes as marks ("[^1]") with their texts.
Claude writes in the same notation with its tools (`Prompt`): write (at the focus, after or before a paragraph, in place of
paragraphs), look (a page as it is laid out, as a picture), picture (`Arranging`: an object's place, size, crop, colours,
frame, turn, mirror, tilt, caption and description, by the session's own handlings as the ring's), tone (the document's tone,
as its buttons give it) and page (the header and footer every page shows, their parts placed by positional tabs, margins,
paper, orientation, a title page, a watermark as Word's dialog writes one, a VML WordArt in each header the pages show, found
again by its name (`WordDocument.DrawingName`, `DrawingWords`), or a picture of the document by its token, a VML picture named
WordPictureWatermark washed out (gain and blacklevel as Word's Washout) and scaled to the margins as Word's Auto does (`Watermarks`),
a border round the pages, `SectionFormatting.Border`, and the
pages' colour, `WordDocument.SetPageColor`: w:background before the body, which no stretch of the body holds, so the document is
read again whole for it and for a version with another colour, as for its styles; Word's art borders by their names, drawn as
tiles of FreeSlate's own motifs in their spirit (`ArtMotifs`, Resources/artMotifs.json: upright pictures or bands turning with the
side, tiled as Word tiles its pictures); line numbers (`SectionFormatting.LineNumbers`, w:lnNumType) and automatic hyphenation
(`WordDocument.AutoHyphenation`, w:autoHyphenation in the settings), the page numbers' format and start; for one section alone
(a paragraph of it: across among upright pages, its own margins, columns, a header and footer of its own or those of the section
before, how it begins, a border round its pages, its notes' numbering), papers of any size, the first page's and even pages' own
headers and footers, margins mirrored and a gutter, `Arranging.Sections`; how others may edit it (`WordDocument.Protection`, Restrict Editing to forms, comments, tracked changes or
reading); read with the document as
"Page:", and given again where a
write takes a section's end away) and comment (`Commenting`: a comment on a paragraph or on words in it, an answer, one marked
done or taken away, by "Claude", through the engine's `AddComment`, `Reply`, `Resolve` and `DeleteComment`; the comments read
after the document as "Comments:", answers under the comments they answer), review (`Reviewing`: tracked changes accepted or rejected,
all, an author's or by their ids, through the engine's `AcceptRevisions` and `RejectRevisions`, and Word's Track Changes turned on or off,
read on the "Page:" line), properties (`Mailings.Properties`: File > Info's title, subject, keywords
and the others, never a name the wish does not give, read after the document as "Properties:") and mailings (`Mailings.Mailing`: a
form letter, an envelope before the letter, the document made a sheet of labels, read as "Mailings:", the envelope's and the
sheet's paragraphs not as the document's text; the page tool and "Page:" are the letter's) and save (`Saving`, a first way to save
before the editor has a place of its own for it: the document as a Word file by the name given in the folder FreeSlate of the user's
own folder, told without the whole path; the editor's own fonts it is set in and the machine lacks embedded, `EngineFonts.Embeddable`,
so that Word shows it in them). A write with track (or in a document whose Track Changes is on) is marked as Claude's tracked changes
against the body as it read before it (`WordDocument.MarkChanges`), word by word, as Word's Compare marks a revision; the fields Word
updates (a table of contents' pages, references, the count of words) after that, unmarked. SmartArt and charts take the text's
font where they are written (`Markup.BodyFont`, `WordDocument.FontOf`), as Word's take the theme's body font. A token written is its object: one in text a write takes away is kept from before and goes in
again where its token is written (its markup as copying keeps it, found again by that markup without its ids), one written
elsewhere moves there; a note's mark written again brings its note with its text. A table in text a write takes away waits
after the place and is written again in place where the write gives a table like it (`Writing.Tables`): rows and columns added
like its last ones or taken away at its end, cells merged or split back, the cells that read otherwise written anew, its looks
as the notation says them, so that what the notation does not say (its widths, heights, lines) stays as it was; a new table
is made as Word's Insert Table makes one and then given the same. A write streams into the document as Claude writes it (`Writing`): the place is made
once it is read, blocks go in as they are finished (a block counts as finished only once what follows shows it ended, so that a
block given once stays as it was), the one being written shows as plain text after them, taken back as it grows; `Markup` makes
the blocks WordprocessingML of the document's own kind: levels and meanings as the tone's styles, a table as Word's Insert Table
makes one, an SVG as Word keeps one (with a PNG drawn from it), WordArt as a text box with Word's text effects and warps, columns
as sections that end on the page they begin. While Claude works (`Talk`), the ring goes beside the pages, its band empty but for
an arc going round, the line saying what Claude does; what Claude writes is framed as a whole, the selection hidden. Its changes
are a preview: the ring's middle keeps them as one step to undo (or Enter) or takes them back (or Escape), a further wish typed
goes on with the same talk, a question gets its answer on a card under the line. Claude is reached through `Asking.Claude.Link`;
the desktop sets it with Anthropic's SDK where `ANTHROPIC_API_KEY` is set (`Desktop/ClaudeLink`: Claude Opus 5.5, its thinking
adaptive, its answers and the inputs of writes streamed, the instructions and the talk cached, a declined request taken on by the
model Anthropic recommends); without a key the line says that one is needed. The SDK serializes by reflection and so does not
work ahead-of-time compiled yet (System.Text.Json's reflection is off there, and allowed, its converters for the SDK's generic
types have no native code); PublishAot switches that reflection off in every build of the desktop's project, so the project
switches it on again where it is not published (`JsonSerializerIsReflectionEnabledByDefault`), for `dotnet run`. The link is
begun with the first wish, so that such a failure shows on the line instead of ending the editor.

It uses no ready-made Avalonia theme, so that nothing is there that was not designed:

- **Themes** are dictionaries of colours (`Themes/Light.axaml`) under a theme variant in `App.axaml`; `Themes/Controls.axaml`
  has one `ControlTheme` per control in use and reads the colours as dynamic resources, so a change of
  `RequestedThemeVariant` shows at once. Another theme is a copy of `Light.axaml` with other values.
  The theme has one colour family, the accent, as a signal only: it marks what the pointer or the keyboard stands on, `Accent`
  for the everyday, the darker `AccentSevere` for what has consequences, such as closing the window. Everything else is neutral.
- **Languages** are dictionaries of texts (`Localization/`), read as dynamic resources too; `UiLanguages.Show` swaps the
  dictionary, English stays underneath for texts a language does not have yet. No reflection, no satellite assemblies:
  this works ahead-of-time compiled, in the browser too.
- **The window's frame** is the theme of Avalonia's `WindowDrawnDecorations`. On Windows the platform keeps the border, the
  shadow and the rounded corners; the app draws the title bar over a window that is one surface. The parts' names connect
  the buttons to the window. Only the maximize button also tells Windows what it is (`WindowDecorationProperties.ElementRole`),
  for the snap layouts: over anything the hit test calls a window button, Windows shows a tip of its own, whatever the app
  does with the mouse's messages. The other buttons therefore count as client area, and the maximize button shows no tip of
  its own on Windows, as the snap layouts open just then. The frame's layers are neighbours of the window under the common
  root, not its children (`TopLevel.GetTopLevel` finds no window from there), so `CaptionBar` watches the window itself and
  shows the buttons' tips by itself (`CaptionTip`), and `CaptionButton` gives a screen reader its tip as its name, as Avalonia
  names frame buttons in English.
  While the pointer is away from the bar, the window's buttons fade away and come back when it returns. Over the part of the
  bar that drags the window the pointer's moves are the platform's and reach no control, so on Windows `CaptionBar` reads them
  in the window's messages (`Win32Properties.AddWndProcHookCallback`); on other platforms the buttons stay for now.
  Full screen has its own bar in the window (`CaptionBar.ForFullScreen`), as the frame's popover cannot fade: the screen's top
  edge brings it, and it rests as a whole. `EditorWindow.ToggleFullScreen` goes there and back by way of the maximized window, so
  that Windows animates the change, while the content fades out and comes back in its new place; a maximized window would
  vanish for a frame on the last step unless its maximized style is cleared first, which also makes it forget its normal place.
- **`CaptionGlyph`** draws the buttons' symbols for the screen's pixels: whole device pixels wide and on them at any scaling.
- **Text** is set in Inter, which the theme names (the default font differs from platform to platform), and smoothed without
  colour fringes and without hinting, the same on every screen (set on each root of a visual tree, as `TextOptions` are no
  styled properties).

## Catalogs (`catalog/`)

Two catalogs drive and document the work:

- The format catalog (`catalog/format/`) lists every element, attribute and type of PPTX and DOCX, generated from
  the schemas of ECMA-376 and the Microsoft Open Specifications by `tools/SpecTools`.
- The feature catalogs (`catalog/features/pptx.yaml`, `catalog/features/docx.yaml`) group those elements into
  features (146 for PowerPoint, 194 for Word) with priority, difficulty, rendering strategy, status and the Office
  behavior measured for each. `tools/SpecTools features` checks that every element is assigned and writes
  [pptx.md](../catalog/features/pptx.md) and [docx.md](../catalog/features/docx.md). Source comments refer to
  features by their id (`catalog: chart.formatting`).
