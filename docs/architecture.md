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
  distances and wrapping), page borders (`PageBorders`, `ArtBorders`); `Layout/FieldEvaluator` computes the fields
  Word recomputes when it prints.
- `PageRenderer` writes every page as SVG and positioned HTML: glyph outlines placed with `<use>`, a transparent text
  layer, `Drawings` and `Vml` for DrawingML and VML objects, and with markup the pane, balloons and change bars
  (`PageRenderer.Markup`, `Describe`); lines and glyphs stand on Word's 600 dpi device as it rounds onto it
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
  into hit tests, cursors and selections; the SVG of `MarkupItem`s is drawn by its own SVG reader (`SvgDrawing`).

## Editing (`WordDocument`, `PresentationDocument`, `Editing/`)

- `IEditableDocument` is a document whose text an editor changes paragraph by paragraph: paragraphs go by the ids the
  places of its scenes name, a place is an offset in a paragraph's text (`Docx/ParagraphText`, `Pptx/SlideText`).
  Each change is a version that `Undo` goes back from and `Redo` forward to; a change the document cannot make leaves
  it as it was and throws `NotSupportedException`.
- `WordDocument` makes a change in the XML of the blocks it touches: that stretch of the main part's text is parsed
  afresh, changed (`Docx/TextEdits`; as tracked changes through `Docx/Tracking` where an author is set), made ready as
  the part was when it was loaded and read anew in place of those blocks (`ModelReader.Reread`, `Docx/Reread.cs`). The
  reader records the numbers each story asked for (`Docx/Numbers`), so that the lists after a change are numbered anew
  without reading their paragraphs again. Saving writes the changed stretches back into the bytes the part was read
  from (`Opc/PartText`), every other byte of the package as it was. Versions share the blocks they have alike.
- `PresentationDocument` makes its changes in the XML of the slides (`Pptx/SlideEdits` for text, `Pptx/SlideShapes` for
  shapes, `Pptx/SlideList` for the list of slides and the parts around it), reads the slide again and draws it anew;
  text that shrinks to fit its shape, and shapes that grow to their text, are fitted anew as PowerPoint fits them.
- `Editing/EditSession` is an editor's work without its surface, over either kind of document: the cursor and the
  selection as places, typing, Backspace, Delete, Enter, formats, copy and paste, each handling one step to undo.

The rules of each change (where new text goes, which paragraph keeps its properties and its mark, what a tracked
change records) were measured in Word and PowerPoint through COM, as the rendering rules were.

## Large documents

A Word document of thousands of pages opens with its first pages at once and grows from there:

- The main part's body is read in portions (`Docx/BodyStream`), the model of the first children before the rest.
- `Layout/Paginator` lays out the first pages and the rest in slices (`WordDocument.LayOut`, while `LayingOut`), as an
  editor does while it waits for input, leaving checkpoints: the paginator's state where pages begin. After a change it
  lays out again from the last checkpoint before it (`Paginator.Relayout`) and stops where a page begins as it began
  before (`SameFlow`); where a change moves everything after it on, it lays out a few pages and leaves the rest to the
  slices. Fields that read the pages (a table of contents) are laid out again once their pages are known.
- Pages are drawn when they are asked for (`Scene(page)`; `Scenes()` gives the others as scenes of their size without
  items), and the scenes of the 32 pages around the last one asked for are kept.
- What would walk the whole document on every change does not: the children of the body are found and taken out in
  constant time (`Docx/Siblings`), formats are shared between the paragraphs and runs that have them alike, laid out
  paragraphs are kept for the next layout.

## Catalogs (`catalog/`)

Two catalogs drive and document the work:

- The format catalog (`catalog/format/`) lists every element, attribute and type of PPTX and DOCX, generated from
  the schemas of ECMA-376 and the Microsoft Open Specifications by `tools/SpecTools`.
- The feature catalogs (`catalog/features/pptx.yaml`, `catalog/features/docx.yaml`) group those elements into
  features (146 for PowerPoint, 194 for Word) with priority, difficulty, rendering strategy, status and the Office
  behavior measured for each. `tools/SpecTools features` checks that every element is assigned and writes
  [pptx.md](../catalog/features/pptx.md) and [docx.md](../catalog/features/docx.md). Source comments refer to
  features by their id (`catalog: chart.formatting`).
