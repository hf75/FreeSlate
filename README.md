# FreeSlate

FreeSlate renders PowerPoint presentations (`.pptx`) as a single, self-contained HTML document that looks like
PowerPoint's own rendering — slide by slide, pixel by pixel — and plays the slide show with its transitions,
animations and media in any modern browser. It is written in C# for .NET 10, runs on Windows, Linux and macOS, and
does not depend on Office.

The goal is fidelity, not approximation: every rule the engine follows was measured against PowerPoint. The test
suite renders each slide with PowerPoint and with FreeSlate and compares the two images.

FreeSlate is meant to grow into the foundation of an open document editor; DOCX support is planned next.

## Status

| | |
|---|---|
| Features | 146 of 146 features of the [PowerPoint feature catalog](catalog/features/pptx.md) implemented; all 1,653 elements a PPTX file can contain are assigned to a feature |
| Fidelity, own test corpus | **98.3 %** over 414 slides in 57 decks built by PowerPoint (89 % of the slides score 95 % or better) |
| Fidelity, other projects' test decks | **94.3 %** over 1,229 slides in 670 decks from LibreOffice, Apache POI, Apache Tika, Open XML SDK and python-pptx |

The score of a slide is the share of its content pixels that match PowerPoint's rendering within a small tolerance
(see [Measuring fidelity](docs/fidelity.md)). Decks of other projects include deliberately broken and exotic files,
which is why their average is lower.

## What it renders

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

The [feature catalog](catalog/features/pptx.md) documents every feature with the PowerPoint behavior that was measured
for it.

## Quick start

Requirements: the [.NET 10 SDK](https://dotnet.microsoft.com/download). The presenter app additionally needs a web view
(WebView2 on Windows).

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

### The output

- `deck.html` shows all slides one below the other; `deck.html#slide-3` shows a single slide at its original size.
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
| `FreeSlate.slnx` | the solution: engine and presenter |
| `src/FreeSlate/` | the rendering engine |
| `src/FreeSlate.Presenter/` | the presenter app (Avalonia) |
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

- DOCX is not supported yet.
- Slides look like PowerPoint's where the fonts they use are available: installed, embedded in the presentation, or
  in Office's cloud font cache. Without Office fonts such as Calibri or Aptos (on Linux, for instance), text is set
  in substitutes with different metrics.
- Slides are matched against PowerPoint for Windows (Microsoft 365); other PowerPoint versions differ in details.
- A few rare cases still differ visibly, among them pies whose outside labels make PowerPoint shrink them, the axis
  intervals of some radar charts, text on circular WordArt paths, ActiveX controls, and modern oblique 3D cameras
  with a rotation of their own. The feature catalog lists the known deviations per feature.
- The presenter is a viewer, not an editor.

## Contributing

Issues and pull requests are welcome. The guiding rule of the project: behavior is measured against PowerPoint,
not guessed. A change that alters the rendering comes with the measurement behind it (a probe deck, COM values or
reference images) and keeps the fidelity of the test corpus (`Fidelity run generated`) at least where it was; the
measured behavior goes into the feature catalog (`catalog/features/pptx.yaml`, then
`dotnet run --project tools/SpecTools -- features`).

## License

FreeSlate is released under the [MIT License](LICENSE).

The preset shape and text warp definitions in `src/FreeSlate/Resources/` are part of ECMA-376 (© Ecma International)
and included under Ecma's copyright policy; see [NOTICE](src/FreeSlate/Resources/NOTICE.md). The schemas in
`spec/ms-xsd/` are extracted from the Microsoft Open Specifications. PowerPoint and Office are trademarks of
Microsoft; FreeSlate is not affiliated with Microsoft.
