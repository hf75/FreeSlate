# FreeSlate

FreeSlate renders PowerPoint presentations (`.pptx`) and Word documents (`.docx`) as a single, self-contained HTML
document that looks like Office's own rendering — slide by slide and page by page, pixel by pixel. Presentations play
their slide show with its transitions, animations and media in any modern browser; Word documents are laid out into
pages by FreeSlate's own layout engine the way Word prints them. It is written in C# for .NET 10, runs on Windows,
Linux and macOS, and does not depend on Office.

The goal is fidelity, not approximation: every rule the engine follows was measured against PowerPoint and Word. The
test suite renders each slide with PowerPoint and each page with Word, renders them with FreeSlate and compares the
images.

FreeSlate is meant to grow into the foundation of an open document editor.

## Status

| | |
|---|---|
| PowerPoint features | 146 of 146 features of the [PowerPoint feature catalog](catalog/features/pptx.md) implemented; all 1,653 elements a PPTX file can contain are assigned to a feature |
| Word features | 194 of 194 features of the [Word feature catalog](catalog/features/docx.md) implemented; all 2,093 elements a DOCX file can contain are assigned to a feature |
| Fidelity, own test corpus | **98.3 %** over 414 slides in 57 decks built by PowerPoint (89 % of the slides score 95 % or better); **92.7 %** over 61 pages in 29 documents built by Word |
| Fidelity, other projects' test decks | **94.5 %** over 1,229 slides in 670 decks from LibreOffice, Apache POI, Apache Tika, Open XML SDK and python-pptx |

The score of a slide or page is the share of its content pixels that match the Office rendering within a small
tolerance (see [Measuring fidelity](docs/fidelity.md)). Decks of other projects include deliberately broken and exotic
files, which is why their average is lower. Word pages lose most of their points to anti-aliasing: Word prints text
with the fonts' hinting, FreeSlate draws glyph outlines; the lines and glyphs stand where Word puts them.

## What it renders

### Presentations


- **Slides and structure**: masters, layouts, placeholders and their inheritance, themes and theme overrides, color
  mapping, backgrounds, headers and footers, sections, custom shows, hidden slides, speaker notes, comments.
- **Shapes**: all 187 preset geometries, freeforms, connectors, groups, rotation and flips, sketched outlines.
- **Fills and lines**: solid, gradient, pattern and picture fills, dashes, joins, caps, arrowheads, compound lines.
- **Effects**: shadows, glow, soft edges, reflection, blur, effect DAGs, 3D scenes with PowerPoint's cameras and
  lights, bevels and extrusions.
- **Text**: an own layout engine (line breaking, autofit, spacing, tabs, bullets and numbering, vertical and
  right-to-left text, East Asian typography), shaping with HarfBuzz, glyphs drawn as outlines where the browser's
  rendering would differ, embedded fonts (including Embedded OpenType with MicroType Express compression), WordArt
  warps and 3D text, equations (OMML).
- **Pictures and media**: PNG, JPEG, GIF, BMP, WebP, SVG, TIFF, JPEG XR, EMF, EMF+ and WMF (with an own metafile
  player), cropping, recoloring and picture effects, audio, video and online video, 3D models (glTF), ink.
- **Tables**: the built-in table styles (custom ones drawn as PowerPoint draws them), merged cells, borders and
  diagonals.
- **Charts**: bar, column, line, area, pie and doughnut, scatter and bubble, radar, stock and surface charts, their
  3D variants, Office 2016 charts (waterfall, treemap, sunburst, histogram, box and whisker, funnel, maps), data
  labels, trendlines, error bars, data tables, and the Office 2007 chart styles PowerPoint applies to everything a
  chart does not format itself.
- **SmartArt**: the saved drawing, or a SmartArt layout engine for diagrams without one.
- **Slide show**: the player embedded in the document runs transitions (classic, 2010, 2013 and morph),
  entrance, emphasis, exit and motion path animations with their timing and triggers, media playback, hyperlinks,
  action buttons and zoom objects.

### Word documents

- **Layout**: Word's line layout (line heights, spacing, tabs, justification, hyphenation, East Asian typography and
  kinsoku, right-to-left and vertical text), lists and numbering, styles and their inheritance, document grids,
  compatibility modes of older Word versions.
- **Pages**: sections, columns, page breaks, keep and widow rules, headers and footers, footnotes and endnotes, line
  numbers, page borders, backgrounds and watermarks, vertical alignment.
- **Tables**: autofit and fixed layouts, merged and nested cells, table styles with their conditional formats,
  borders, floating tables, rows split over pages.
- **Objects**: pictures, shapes, text boxes, groups and canvases, WordArt, charts and SmartArt (with the PowerPoint
  renderers), equations, VML shapes, OLE objects, floating objects with all text wrapping modes.
- **Fields and content**: page fields, references, sequences and other fields as Word computes them when it prints,
  content controls and data binding, legacy form fields, imported content (HTML, RTF, text), ruby, emphasis marks.
- **Review**: comments and tracked changes, as Word prints without markup (all changes accepted) or, as an option,
  with markup: changes in their authors' colors, change bars, and comments and formatting changes in balloons beside
  the page.

The feature catalogs ([PowerPoint](catalog/features/pptx.md), [Word](catalog/features/docx.md)) document every feature
with the Office behavior that was measured for it.

## Quick start

Requirements: the [.NET 10 SDK](https://dotnet.microsoft.com/download). The presenter and the viewer's *Browser* view
additionally need a web view (WebView2 on Windows).

```sh
git clone https://github.com/hf75/FreeSlate.git
cd FreeSlate
dotnet build FreeSlate.slnx -c Release
```

### Converting a presentation

```csharp
using FreeSlate;

// One self-contained HTML file: all slides one below the other, the player for the slide show included.
PptxConverter.ToFile("deck.pptx", "deck.html");

// Or as a string.
string html = PptxConverter.ToHtml("deck.pptx");
```

Options:

```csharp
// Password-protected presentations (agile and standard encryption).
PptxConverter.ToFile("secret.pptx", "secret.html", password: "…");   // throws FreeSlate.Opc.PasswordException when missing or wrong

// Use the fonts embedded in the presentation even where the same family is installed.
PptxConverter.ToFile("deck.pptx", "deck.html", embeddedFontsFirst: true);

// Format chart numbers and dates for another culture (PowerPoint uses the system's regional format).
PptxConverter.ToFile("deck.pptx", "deck.html", culture: new System.Globalization.CultureInfo("en-US"));

// Look for fonts in other directories.
var fonts = new FreeSlate.Fonts.FontLocator(["/usr/share/fonts", "/home/me/fonts"], []);
PptxConverter.ToFile("deck.pptx", "deck.html", fonts: fonts);
```

### Converting a Word document

```csharp
using FreeSlate;

// One self-contained HTML file: all pages one below the other, laid out as Word prints them.
DocxConverter.ToFile("report.docx", "report.html");

// With comments and tracked changes shown as Word prints them with markup.
DocxConverter.ToFile("report.docx", "report-markup.html", markup: true);
```

The options of `PptxConverter` for passwords, fonts and the regional format (`password`, `fonts`, `culture`) work
for Word documents too; the culture also chooses the language of the markup balloons (German or English).

### Exporting PDF

```csharp
using FreeSlate.Skia;

// The pages drawn by Skia into a PDF: text in its fonts (embedded as subsets), drawings as vectors, pictures lossless
// (JPEGs as they are), the document's title, author, subject and keywords from its properties.
using (var pdf = File.Create("report.pdf"))
    PdfExport.FromDocx("report.docx", pdf);
using (var pdf = File.Create("deck.pdf"))
    PdfExport.FromPptx("deck.pptx", pdf);
// For archiving: PDF/A-2b (XMP metadata, an sRGB output intent).
using (var pdf = File.Create("report-archive.pdf"))
    PdfExport.FromDocx("report.docx", pdf, archive: true);
```

`FreeSlate.Skia` draws what the converters lay out (`DocxConverter.ToScenes`, `PptxConverter.ToScenes`: one scene of
typed items per page or slide) on any Skia canvas, as PNG (`SkiaScene.Png`) or as PDF (`SkiaScene.Pdf`). The PDF is not
tagged: SkiaSharp hands Skia no structure tree.

### Editing and saving Word documents

```csharp
using FreeSlate;
using FreeSlate.Docx;

using var document = WordDocument.Open("report.docx");
// Paragraphs by id; a place is an offset in a paragraph's text (a tab, a break or a field counts as one character).
var first = document.Blocks.OfType<ParagraphBlock>().First();
document.Insert(first.Id, 0, "Draft: ");
var second = document.Split(first.Id, 7);   // as Enter splits it
document.Join(first.Id);                    // as Delete at its end joins it with the next
document.Format(first.Id, 0, 5, new TextFormatting { Bold = true, Size = 14 });
document.Format(first.Id, new ParagraphFormatting { Alignment = "center", After = 6 });
document.Undo();                            // and Redo
document.Author = "Reviewer";               // from now on tracked changes (w:ins, w:del, w:rPrChange)
document.Delete(first.Id, 0, 7);
document.Save("report-edited.docx");        // the changed paragraphs written anew, every other byte as it was
```

`WordDocument` replaces text where the document has it as text, in the format of the text before it, splits, joins
and removes paragraphs, gives text and paragraphs direct formatting as Word's buttons do, and undoes and redoes each
change; paragraphs keep their ids through changes. A change is made in the XML of the paragraphs it touches, which are
read again on their own (the numbers of the lists after them worked out anew) and laid out again from the page
before; saving writes each changed paragraph into the XML it was read from and leaves the rest of the package as it
was. With an `Author` set, changes are tracked changes as Word's Track Changes makes them (what the author inserted and
deletes again goes away outright); `WordDocument.Open(path, markup: true)` reads a document with its markup, as Word
shows all markup. The pages it lays out (`Scenes`) know where each character stands
(`PlacesItem`), which `FreeSlate.Skia.TextPlaces` turns into hit tests, cursors and selections.

### Editing and saving presentations

```csharp
using FreeSlate;

using var deck = PresentationDocument.Open("deck.pptx");
// Slides by id, their text bodies (shapes, groups, table cells) and paragraphs by id; a place is an offset in a
// paragraph's text (a line break, a field or an equation counts as one character).
var title = deck.Slides[0].Bodies[0].Paragraphs[0];
deck.Insert(title, 0, "Draft: ");
var second = deck.Split(title, 7);      // as Enter splits it
deck.Join(title);                       // as Delete at its end joins it with the next
deck.Format(title, 0, 5, new TextFormatting { Bold = true, Color = "C00000" });
deck.Format(title, new ParagraphFormatting { Alignment = "center", Before = 6 });
var slide = deck.Slides[0].Id;          // shapes by their ids on a slide, places in points
var box = deck.AddTextBox(slide, 40, 40, 240, 30, "Note");
deck.PlaceShape(slide, box, 60, 50, 240, 30);
deck.DeleteShape(slide, deck.Shapes(slide)[0].Id);   // with what animates it
var copy = deck.DuplicateSlide(slide);  // slides by their ids: duplicated, added from a layout, moved, taken away
deck.AddSlide(1, deck.Layouts[1].Part);
deck.MoveSlide(copy, 0);
deck.DeleteSlide(slide);
deck.Undo();                            // and Redo
deck.Save("deck-edited.pptx");          // the changed paragraphs written anew, every other byte as it was
```

`PresentationDocument` changes the text of slides as PowerPoint does (new text in the format of the text before it,
a split paragraph's halves with its properties, a joined one with the first's, text that shrinks to fit its shape and
shapes that grow to their text fitted anew), gives text and paragraphs direct
formatting as PowerPoint's buttons do, moves and sizes shapes (a placeholder takes its place from its layout until then,
a group scales its shapes), takes shapes away with their animations and adds text boxes, duplicates slides (with copies
of their notes, charts and diagrams), adds them from layouts, moves them and takes them away (with their sections,
custom shows and the links to them), undoes and redoes each change, and keeps the paragraphs' ids. A change is made in the XML of its slide, which is read and drawn again; the slides it draws
(`Scenes`) know where each character stands, as Word documents' pages do.

### Editing as a word processor does

```csharp
using FreeSlate;
using FreeSlate.Editing;

using var document = WordDocument.Open("report.docx");      // or PresentationDocument.Open("deck.pptx")
var session = new EditSession(document);
session.Place(new TextPlace(paragraph, 0));                  // where a click put the cursor (a place its pages show)
session.Type("Hello ");                                      // in place of the selection; line feeds split paragraphs
session.Backspace();                                         // and Delete: at a paragraph's edge it joins
session.Enter();
session.Place(new TextPlace(paragraph, 5), extend: true);    // a selection, over paragraphs too
session.Format(new TextFormatting { Bold = true });
session.Undo();                                              // one handling at a time, typing on counts as one
session.Save("report-edited.docx");
```

`EditSession` is an editor's work without its surface, over either kind of document (`IEditableDocument`): the cursor
and the selection as places in the text, typing, Backspace, Delete, Enter and formats as a word processor's keys do
them, each handling one step to undo and redo, however many changes of the document it took. A handling the document
cannot make (within a field's result, a table of contents, an object, text that is not the body's) throws
`NotSupportedException` and leaves the document as it was.

After a change, `Scenes()` lays out and draws again only the pages it changed, also in documents of thousands of pages
(a keystroke on page 4 000 of 5 000 within a frame). Where a change moves everything after it on (a paragraph one line
longer), it lays out the next few pages and leaves the rest as they were (`LayingOut`); `LayOut()` lays them out bit by
bit, as an editor does while it waits for input. A Word document lays out its first pages so when it opens, the rest
following, and draws its pages when they are asked for: `Scenes()` gives a page not drawn yet as a scene of its size
without items (`Drawn` false), `Scene(page)` draws it:

```csharp
session.Type("x");
var pages = document.Scenes();                               // the pages the change touched, at once
while (document.LayingOut)
    document.LayOut();                                       // the rest, a few pages at a time
var shown = document.Scene(3);                               // page 4 drawn, as it comes into view
```

### The output

- `deck.html` shows all slides one below the other; `deck.html#slide-3` shows a single slide at its original size.
  `report.html` shows all pages one below the other; `report.html#page-3` shows a single page, and printing it
  prints every page on its own paper size.
- The play button or F5 starts the slide show full screen, Shift+F5 from the current slide, Alt+F5 (or V during the
  show) opens the presenter view with the next slide, the speaker notes and a timer. The keys work as in PowerPoint:
  arrow keys, space, Enter, Page Up/Down, Home/End, N/P, a slide number and Enter, B/W for a black or white screen,
  Escape to end the show.
- Fonts the browser cannot find by name — fonts embedded in the presentation and Office cloud fonts such as Aptos —
  are embedded into the HTML.
- Slides are SVG and positioned HTML: text stays selectable and searchable.

### Presenting

```sh
dotnet run --project src/FreeSlate.Presenter -- deck.pptx
```

The presenter is a small [Avalonia](https://avaloniaui.net/) app that converts a presentation and shows it in the
platform's web view; *Präsentation starten* runs the slide show full screen (the presenter's interface is German
for now). In Visual Studio, open `FreeSlate.slnx` rather than the presenter's project file, so that the presenter
always runs against the current engine.

### Viewing Word documents

```sh
dotnet run --project src/FreeSlate.Viewer -- report.docx
```

The viewer is the presenter's counterpart for Word documents (its interface is German too): it converts a document and
draws its pages with Skia (*Browser* shows FreeSlate's HTML in the platform's web view instead), zooms (automatic,
page width, whole page, 50 to 200 %), jumps to a page, follows links (to bookmarks of the document or to the web),
puts a cursor where one clicks and selects by dragging or with Shift and the arrow keys (the status bar names the
paragraph and the place), edits the text there (typing, Backspace, Delete, Enter, Ctrl+B, I and U for bold, italic and
underline, Ctrl+Z and Ctrl+Y to undo and redo; only the pages a change touches are drawn again) and saves it with
Ctrl+S, shows the document with its markup, prints it, saves it as PDF (or PDF/A for archiving) or HTML, and loads it
again when it is saved elsewhere (F5 does too). A document that cannot be converted shows the whole exception.
It opens presentations too, their slides drawn by Skia with a cursor and selections in their text. On Windows with Word
installed, *Word daneben* puts Word's own pages beside FreeSlate's, *Word überblendet* lays them over FreeSlate's with
a slider between the two, and *Unterschiede* shows where the two differ: Word prints the document to PDF through COM,
as the fidelity tool renders its references, and Windows rasterizes the pages.

## How it works

FreeSlate reads the package (ZIP, encryption, markup compatibility), resolves the inheritance of every shape through
slide, layout and master, and writes each shape as SVG: its geometry from the preset definitions, its fills, lines and
effects as SVG paint and filters, its text laid out by FreeSlate's own engine and placed glyph by glyph. Charts,
tables, SmartArt, metafiles, equations and 3D scenes have renderers of their own. A JavaScript player embedded in the
document runs the slide show.

Everything is implemented in this repository, from the decoders of Embedded OpenType, JPEG XR and TIFF to the EMF
player, the chart engine, the SmartArt layout engine and the glTF renderer. The engine's only package dependency is
[HarfBuzzSharp](https://github.com/mono/SkiaSharp) for text shaping.

More in [docs/architecture.md](docs/architecture.md).

## Measuring fidelity

```sh
dotnet run --project tools/Fidelity -c Release -- run generated
```

The Fidelity tool converts the decks of the test corpus, takes a screenshot of every slide with headless Chrome or
Edge and compares it with PowerPoint's rendering of the same slide, which is committed to the repository. The report
(`artifacts/fidelity/report.html`) shows the reference, FreeSlate's rendering and a difference image for every slide.
On Windows with Office installed the tool also builds the test decks through PowerPoint and renders the references.

More in [docs/fidelity.md](docs/fidelity.md).

## Repository layout

| Path | Content |
|---|---|
| `FreeSlate.slnx` | the solution: engine, Skia backend, presenter and viewer |
| `src/FreeSlate/` | the rendering engine |
| `src/FreeSlate.Skia/` | the Skia backend: pages and slides drawn from their scenes, PNG and PDF |
| `src/FreeSlate.Presenter/` | the presenter app (Avalonia) |
| `src/FreeSlate.Viewer/` | the viewer app for Word documents and presentations (Avalonia) |
| `tools/Fidelity/` | builds the test decks through PowerPoint, renders references, measures fidelity |
| `tools/SpecTools/` | downloads the specifications, builds the format catalog, checks the feature catalog |
| `tools/SmartArtDecks/` | scripts that build SmartArt probe decks through PowerPoint |
| `catalog/format/` | format catalog: every element, attribute and type of PPTX and DOCX, linked to the specification |
| `catalog/features/` | feature catalog: curated features with priority, difficulty, strategy, status and measured PowerPoint behavior |
| `catalog/spec/` | section index of ECMA-376 and the Microsoft Open Specifications |
| `spec/ms-xsd/` | Office extension schemas (a14, p14, c14, cx, …) extracted from the Microsoft Open Specifications |
| `tests/corpus/pptx/generated/` | the test corpus, built by PowerPoint through `Fidelity generate` |
| `tests/fidelity/reference/` | PowerPoint's renderings of the test decks, one PNG per slide |

The specification sources (`spec/downloads/`, `spec/ecma-376/`) and the other projects' test decks are downloaded on
demand and stay out of git.

## Known limitations

- Slides and pages look like Office's where the fonts they use are available: installed, embedded in the document,
  or in Office's cloud font cache. Without Office fonts such as Calibri or Aptos (on Linux, for instance), text is set
  in substitutes with different metrics.
- Slides are matched against PowerPoint and pages against Word for Windows (Microsoft 365); other versions differ in
  details.
- Word documents are rendered as pages to read and print; FreeSlate does not edit them or reflow them to the window.
  Word's pictorial art page borders (apples, hearts, …) are Microsoft's own pictures and are not drawn; the basic
  geometric ones are.
- A few rare cases still differ visibly, among them pies whose outside labels make PowerPoint shrink them, the axis
  intervals of some radar charts, text on circular WordArt paths, ActiveX controls, and modern oblique 3D cameras
  with a rotation of their own. The feature catalog lists the known deviations per feature.
- The presenter and the viewer show documents; they do not edit them.

## Contributing

Issues and pull requests are welcome. The guiding rule of the project: behavior is measured against PowerPoint and
Word, not guessed. A change that alters the rendering comes with the measurement behind it (a probe deck or document,
COM values or reference images) and keeps the fidelity of the test corpus (`Fidelity run generated`, `Fidelity run
docx/generated`) at least where it was; the measured behavior goes into the feature catalog
(`catalog/features/pptx.yaml` or `docx.yaml`, then `dotnet run --project tools/SpecTools -- features`).

## License

FreeSlate is released under the [MIT License](LICENSE).

The preset shape and text warp definitions in `src/FreeSlate/Resources/` are part of ECMA-376 (© Ecma International)
and included under Ecma's copyright policy; see [NOTICE](src/FreeSlate/Resources/NOTICE.md). The schemas in
`spec/ms-xsd/` are extracted from the Microsoft Open Specifications. PowerPoint, Word and Office are trademarks of
Microsoft; FreeSlate is not affiliated with Microsoft.
