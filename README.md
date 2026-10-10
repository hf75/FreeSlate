# FreeSlate

An open-source, cross-platform engine in C# that renders PPTX and DOCX as HTML/CSS/JS with about 99 % fidelity,
and later the foundation of an editor.

## Repository layout

| Path | Content |
|---|---|
| `FreeSlate.slnx` | the solution: engine and presenter, so the presenter is always built against the current engine |
| `src/FreeSlate/` | the rendering engine (PPTX → HTML for now) |
| `src/FreeSlate.Presenter/` | a small Avalonia app that opens a presentation and runs its slide show |
| `tools/SpecTools/` | downloads the specifications, builds the format catalog and checks the feature catalogs |
| `tools/Fidelity/` | measures fidelity: our HTML against PowerPoint's rendering, slide by slide |
| `catalog/format/` | format catalog, level 1: every element, attribute and type of DOCX and PPTX, linked to the specification |
| `catalog/features/` | feature catalogs, level 2: curated features with priority, difficulty, rendering strategy and status |
| `catalog/spec/` | section index of ECMA-376 and the Microsoft documents |
| `spec/ms-xsd/` | Office extension schemas (w14, p14, a14, cx, …) extracted from the MS-* documents |
| `spec/downloads/`, `spec/ecma-376/` | specification sources, not in git (`fetch` restores them) |
| `tests/corpus/pptx/` | test decks; `generated/` is created by PowerPoint through `Fidelity generate` |
| `tests/fidelity/reference/` | PowerPoint's renderings of the test decks (PNG per slide), committed so comparisons run without Office |

## Converting a presentation

```csharp
FreeSlate.PptxConverter.ToFile("deck.pptx", "deck.html");
```

The HTML is self-contained: all slides one below the other; `deck.html#slide-3` shows a single slide at its original size.
A password-protected presentation needs its password (`ToFile("deck.pptx", "deck.html", password: "…")`); without
it, or with a wrong one, `FreeSlate.Opc.PasswordException` says which. Fonts embedded in the presentation are decoded
and used where the family is not installed (`embeddedFontsFirst: true` uses them everywhere); the HTML carries them.

## Presenting

```sh
dotnet run --project src/FreeSlate.Presenter -- deck.pptx
```

The presenter (Avalonia 12) converts the presentation and shows the document in the platform's web view (WebView2 on
Windows); *Präsentation starten* runs the slide show full screen. Arrow keys, space and Escape work as in PowerPoint.

In Visual Studio, open `FreeSlate.slnx` rather than the presenter's project file: Visual Studio builds referenced
projects only when they belong to the solution, so a presenter opened on its own runs against the engine as it was
last built.

## Measuring fidelity

```sh
dotnet run --project tools/Fidelity -- run
```

Converts every deck of the corpus, takes a screenshot of each slide with headless Chrome or Edge and compares it with
PowerPoint's reference rendering. The report (`artifacts/fidelity/report.html`) shows reference, our rendering and a
difference image for every slide. The score is the share of content pixels that match the reference within a small
tolerance. On Windows with Office installed, `generate` rebuilds the test decks and `reference` re-renders the references.

`animate` compares animations the same way: `animate-reference` (Windows, Office and ffmpeg) exports PowerPoint's
video of every animated slide and keeps every eighth frame in `tests/fidelity/reference/<deck>/anim/`; `animate` draws
the same moments with the player (`deck.html?frame=<ms>&fps=30#slide-3`) and scores them.

## Regenerating the catalogs

```sh
dotnet run --project tools/SpecTools -- all
```

`all` downloads ECMA-376 Parts 1–4 and [MS-DOCX], [MS-PPTX], [MS-ODRAWXML] and [MS-OI29500] (about 90 MB),
extracts the Office extension schemas, writes the format catalog and checks the feature catalogs
(`catalog/features/*.yaml`). When Microsoft publishes a new revision, the git diff of `spec/ms-xsd/` and `catalog/`
shows exactly what changed.
