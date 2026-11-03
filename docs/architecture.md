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
  Each change is a version that `Undo` goes back from and `Redo` forward to, and `Combine` takes the last few as one (an
  editor's step, words typed one after the other); a change the document cannot make leaves it as it was and throws
  `NotSupportedException`. Text can mean something without a look of its own: a character style (`TextFormatting.Style`,
  `CharacterStyles`), as a paragraph can have a paragraph style (`ParagraphStyle`), whose look the style definitions give,
  which `DefineStyles` replaces all at once as a style set does in Word, with the lists they name: a Word document is then
  read whole again, and so is a version with other styles that undoing goes to.
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
  the margin, the column or the paragraph), its size. A drawing made floating with no place given stays where it stood,
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
- `WordDocument` keeps other versions of a section's text and notes for planning it (`WordDocument.Versions.cs`,
  `Docx/SectionPlan`) in the paragraph that begins the section, in an alternative of markup compatibility only the engine
  can choose: `mc:AlternateContent` with `mc:Choice Requires="fs"` (namespace `urn:freeslate:plan`) and an empty
  `mc:Fallback`, a version's blocks as CDATA. Word shows none of it and writes none of it back when it saves, what only the
  versions refer to going with them; so a version shares the document's ids and no version ever reaches what renumbers
  them. Putting a version in place moves text within the main part, one change to undo. A copy without plans
  (`SaveWithoutPlans`) leaves out the versions and what only they refer to, for a file that goes to others.
- `PresentationDocument` makes its changes in the XML of the slides (`Pptx/SlideEdits` for text, `Pptx/SlideShapes` for
  shapes, `Pptx/SlideList` for the list of slides and the parts around it), reads the slide again and draws it anew;
  text that shrinks to fit its shape, and shapes that grow to their text, are fitted anew as PowerPoint fits them.
- `Editing/EditSession` is an editor's work without its surface, over either kind of document: the cursor and the
  selection as places, typing, Backspace, Delete, Enter, formats, copy and paste, markup, blocks and notes put in, each
  handling one step to undo, whose changes the document keeps as one version (typing on adds to it); what else a
  document offers (a Word document's versions of a section, its comments) goes through `Handle` as a handling of its own.

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
input method's text to an `EditSession`. It neither opens nor saves files yet.

A document says what its text means, not how it looks (`Writing/`): a meaning is a character style (`Meanings`), and a tone
(`Tones`, the style sets in `Tones/*.xml`) gives the meanings and the paragraphs' levels their look, the whole document at once,
a change of it one step to undo, which the sheets in view fade through (`DocumentView.Restyle`). The file stays a Word document
that looks alike in Word. The editor's documents hyphenate (`WordDocument.New(hyphenate)`, Word's w:autoHyphenation, a setting of
the whole document): the magazine tone justifies and hyphenates its text, the others suppress hyphenation in their normal
paragraph, which the other levels are based on. `DocumentView.ShowsMeanings` frames the stretches with a meaning and names it in the page's margin,
drawn over the pages, which do not move for it. The engine gets the app's Inter beside the system's fonts (`EngineFonts`).
A paragraph's level lies on one axis from title to margin note (`Levels`, Word's own styles where it has them): `LevelLever`
is the grip beside the paragraph the pointer is at, dragged sideways a level per 32 pixels with the page showing it at once, one
step to undo; Tab and Shift+Tab move the selection's paragraphs a level, Enter in an empty point moves it out. Meanings are
given with `GestureRing`, a marking menu whose fields share the circle in an order that stays (six: the five meanings, then
taking one away): the right button held on the selection or a word, moved blindly or into a field; a short click opens it to be
clicked; the left button held on the selection, or Space held, bring it too. A field may hold a ring of its own (a second level,
a picture's places on the page, later inserting tables and more), which opens where the pointer leaves the field, on a click, by
a turn of a blind stroke, or by Enter. The ring is always of one size and shows signs only, light, set by their ink, each in the middle of its
field with room all around and no lines between the fields: a band around an inner circle, the band's glass nearly whole and the
middle clear; the field pointed at shows as a rounded part of the band, its name beside the ring. It lies over everything; brought
on the text, it stands centred on the selection's part there, as far as the point it was brought at stays in its middle. The
whole selection is framed exactly, one outline around the lines that meet (`DocumentView.SelectionAreas`), under the ring, and
reads as the field pointed at would make it: a preview, a step of its own that letting go keeps and anything else takes back, the
selection hidden meanwhile (`GestureRing.Preview`, `DocumentView.SelectionHidden`). In full screen
the line above the sheets lies under the window's buttons and comes and goes with them (`CaptionBar.Changed`).
Pictures go in as Word puts them in from a file (`EditSession.InsertPicture`): in the line at their own size, no wider than the
text of their column; dropped on the sheets (a bar shows where they would go), or pasted where the clipboard holds no text (picture
files copied from a folder, else a picture itself), kinds Word does not keep as PNG. A click on a drawing selects it as a whole and
frames it instead of tinting it; the frame is the drawing's own box, which the renderer gives among the line's places
(`PlacesItem.Boxes`, `TextPlaces.Box`); one in front of the text is hit first, one behind it only where no text lies over it. On a
picture the ring has the picture's fields (`GestureRing.Choose`, `Writing/Placements`): in the line up, beside the text right and
left, on its own between the text down, in front of the text and behind it between them, a place on the page down right (a
ring of the eight places round the margins, each in its direction), and removing it up left, where the meanings' ring takes a
meaning away. Their signs are icons, the picture a frame among lines of text. A preview the ring takes back leaves nothing to do
again (`EditSession.TakeBack`). A picture is dragged with the main button (`DocumentView.Dragging.cs`): in the line its outline
goes with the pointer and a bar shows where it would go in the text; floating it follows the pointer over the pages, the text
going round it as it goes (a step taken back and made anew as it goes on, kept where it is let go, Escape takes it back), its
anchor's paragraph marked in the margin. Near a column's edges and middle and the margins it lines up with them, a line along
each (Alt held, not); at a column's edge or middle of a page of one column it stays aligned there. The handles at the corners and
the middles of the sides of the picture selected size it the same way, as a step that goes on: a corner keeps its proportions, a
side stretches it, the opposite side stays and an alignment there too, a side it moves lines up with the columns' edges and the
margins. Near the view's top or bottom a drag scrolls the pages, the faster the nearer (a picture dragged or sized, or the
selection, reaching on). The arrows move a floating picture a point, ten with Shift; arrows one after another are one step.

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
