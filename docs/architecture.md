# Architecture

FreeSlate turns a presentation or a Word document into one HTML document. This page follows a presentation through
the engine and describes the parts it meets on the way, then a Word document. Class and file names refer to
`src/FreeSlate/`.

## Output

The document has fixed-size, absolutely positioned slides, one below the other. Every shape is an absolutely
positioned element holding an SVG: its geometry as paths, its fills, lines and effects as SVG paint and filters, its
text as glyph runs. Glyph outlines are defined once per font and glyph and placed with `<use>`; small text, which
PowerPoint hints to the pixel grid, is left to the browser. A transparent text layer keeps all text selectable and
searchable.

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
- `Layout/RunStyles` turns run formatting into styles with Word's font metrics; `Layout/ParagraphLayout` sets
  paragraphs into lines by Word's rules (line heights, spacing, tabs, justification, kinsoku, bidi, vertical text);
  `Layout/TableLayout` sizes and lays out tables; `Layout/Paginator` breaks the body into pages: sections, columns,
  headers and footers, notes, floating objects and text wrapping, page borders (`PageBorders`, `ArtBorders`);
  `Layout/FieldEvaluator` computes the fields Word recomputes when it prints.
- `PageRenderer` writes every page as SVG and positioned HTML: glyph outlines placed with `<use>`, a transparent text
  layer, `Drawings` and `Vml` for DrawingML and VML objects, and with markup the pane, balloons and change bars
  (`PageRenderer.Markup`, `Describe`).

## Catalogs (`catalog/`)

Two catalogs drive and document the work:

- The format catalog (`catalog/format/`) lists every element, attribute and type of PPTX and DOCX, generated from
  the schemas of ECMA-376 and the Microsoft Open Specifications by `tools/SpecTools`.
- The feature catalogs (`catalog/features/pptx.yaml`, `catalog/features/docx.yaml`) group those elements into
  features (146 for PowerPoint, 194 for Word) with priority, difficulty, rendering strategy, status and the Office
  behavior measured for each. `tools/SpecTools features` checks that every element is assigned and writes
  [pptx.md](../catalog/features/pptx.md) and [docx.md](../catalog/features/docx.md). Source comments refer to
  features by their id (`catalog: chart.formatting`).
