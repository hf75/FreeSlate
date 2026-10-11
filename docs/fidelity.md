# Measuring fidelity

FreeSlate's reference is PowerPoint itself. Every rendering rule in the engine was measured against PowerPoint, and
the test suite compares FreeSlate's rendering of every test slide with PowerPoint's. This page describes the tool,
the corpus and the way new behavior is measured.

## The score

For every slide, the Fidelity tool compares PowerPoint's rendering (the reference) with a screenshot of FreeSlate's
HTML at the same size (1280 × 720 for 16:9 slides):

- A pixel is *ink* when it differs from the slide's background in either image (by more than 24 of 255).
- An ink pixel *matches* when the other image has a pixel within one pixel's distance whose color is within 40 of 255.
- The score is the share of ink pixels that match. Empty slides score 100 %.

The report also shows the SSIM of the two images. Averages are weighted by slides.

Thin lines, small text and pictures with fine detail are the hardest to match: half a pixel of difference in where
a line sits can halve the score of a slide that shows only that line. A slide at 95 % usually looks the same at a
glance; differences below 99 % are often antialiasing and pixel snapping.

## Running it

```sh
dotnet build tools/Fidelity -c Release
dotnet run --project tools/Fidelity -c Release --no-build -- run generated
```

| Command | What it does | Needs |
|---|---|---|
| `run [decks]` | converts the decks, takes screenshots and compares them with the references | Chrome or Edge |
| `render [decks]`, `compare [decks]` | the two halves of `run` | Chrome or Edge |
| `generate` | builds the test decks of `tests/corpus/pptx/generated/` through PowerPoint | Windows, PowerPoint |
| `reference [decks] [--force]` | renders PowerPoint's references (`Slide.Export`) | Windows, PowerPoint |
| `fetch` | downloads the test decks of other projects | network |
| `animate-reference`, `animate` | records PowerPoint's video of animated slides and compares the player's frames with it | Windows, PowerPoint, ffmpeg |
| `transitions-reference` | the same for slide transitions | Windows, PowerPoint, ffmpeg |
| `smartart [files]` | compares the SmartArt engine's layout with the drawings PowerPoint saved | |

Decks are named by their path below the corpus (`generated/fills`, `local/probe`, `external/poi/bar-chart`); a folder
stands for all decks in it (`run generated`, `run external`). Without a name, all decks of `tests/corpus/pptx/` run
(not the external ones). The report is written to `artifacts/fidelity/report.html`: reference, FreeSlate's rendering
and a difference image for every slide, with the scores in `artifacts/fidelity/scores.json`.

PowerPoint formats chart numbers and dates in the system's regional format; the tool converts in the system's format
too. The committed references were made on a German system, so chart labels match where the comparison runs with
German regional settings.

## The corpus

- `tests/corpus/pptx/generated/` holds 57 decks that `tools/Fidelity/PowerPoint/*Deck.cs` builds through
  PowerPoint's object model: one deck per area (fills, lines, text, charts, transitions, …) with slides that
  exercise its features. PowerPoint saved every file, so they are what PowerPoint writes. The generator removes
  what PowerPoint records about the user (the author of the deck and of the workbooks embedded for charts, the
  profile folder in the paths of OLE packages) and stops when a deck still names the user anywhere. Their
  references (`tests/fidelity/reference/generated/`) are committed, so the comparison runs without Office.
- `fetch` downloads about 700 test decks of LibreOffice, Apache POI, Apache Tika, the Open XML SDK and python-pptx
  (pinned commits; the license of each source is recorded with it) into `spec/downloads/testdecks/`. They contain
  files from many producers, including damaged and unusual ones. `reference external` renders their references,
  `run external` scores them. Neither the decks nor their references are committed.
- `tests/corpus/pptx/local/` is for probe decks and private files; it stays out of git, and so do their references.

## Measuring new behavior

The rule of the project is to measure, not to guess. A typical measurement:

1. Build probe decks that vary one thing at a time, through PowerPoint's object model (PowerShell or C# with COM) or
   by editing the XML of a deck PowerPoint saved. Keep the probe in `tests/corpus/pptx/local/`.
2. Read what PowerPoint reports through COM where it reports something (shape and text positions, chart plot areas,
   legend entries, axis scales, line widths), and render the references with `reference local/<probe>`.
3. Measure the references (pixel positions, colors, sizes) and derive the rule; check it against all probes.
4. Implement the rule, run the probes and the whole corpus, and write the measured behavior into the feature
   catalog.

Things to keep in mind:

- PowerPoint loads Office cloud fonts when it first needs them; a reference rendered before the font arrived shows a
  substitute. Render such decks again with `reference --force`.
- Slides with date and time fields change every day; their scores drop after midnight.
- Run one Fidelity process at a time, and do not rebuild the tool while it runs.
