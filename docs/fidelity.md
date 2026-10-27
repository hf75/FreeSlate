# Measuring fidelity

FreeSlate's reference is PowerPoint itself. Every rendering rule in the engine was measured against PowerPoint, and
the test suite compares FreeSlate's rendering of every test slide with PowerPoint's. Word documents get the same
treatment with Word as the reference, page by page. This page describes the tool, the corpus and the way new behavior
is measured.

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
| `generate docx` | builds the test documents of `tests/corpus/docx/generated/` through Word | Windows, Word |
| `reference [decks] [--force]` | renders PowerPoint's references (`Slide.Export`), Word's for documents (see below) | Windows, PowerPoint, Word |
| `reraster [documents]` | rasterizes Word's stored PDFs of documents again, without Word (after a change to the rasterizing) | Windows |
| `fetch`, `fetch docx` | downloads the test decks or the Word documents of other projects | network |
| `animate-reference`, `animate` | records PowerPoint's video of animated slides and compares the player's frames with it | Windows, PowerPoint, ffmpeg |
| `transitions-reference` | the same for slide transitions | Windows, PowerPoint, ffmpeg |
| `smartart [files]` | compares the SmartArt engine's layout with the drawings PowerPoint saved | |

Decks are named by their path below the corpus (`generated/fills`, `local/probe`, `external/poi/bar-chart`); a folder
stands for all decks in it (`run generated`, `run external`), `@file` for the names in a file, one a line. Without a name, all decks of `tests/corpus/pptx/` run
(not the external ones). The report is written to `artifacts/fidelity/report.html`: reference, FreeSlate's rendering
and a difference image for every slide, with the scores in `artifacts/fidelity/scores.json`.

PowerPoint formats chart numbers and dates in the system's regional format; the tool converts in the system's format
too. The committed references were made on a German system, so chart labels match where the comparison runs with
German regional settings.

## Word documents

Word documents are named `docx/<path>` (`docx/generated/text-basic`, `docx/local/probe`), so `reference docx` renders
all of them. Word has no export of a page as an image; the reference is what Word prints: Word exports the document to
PDF (`ExportAsFixedFormat`, the document without markup, optimized for print), and Windows' own PDF renderer
(`Windows.Data.Pdf`, through `tools/Fidelity/Word/rasterize-pdf.ps1`) rasterizes every page at 96 dpi into
`page-001.png`, … The PDF stays in `artifacts/fidelity/docx/…/reference.pdf` for measuring (it embeds font subsets
and is not committed). Pages of a document may differ in size; each is compared at its own size. `run` converts a
document with FreeSlate, prints it to PDF with headless Chrome or Edge and rasterizes that PDF with the same renderer,
so that both sides go through the same rasterizer. Before that the browser's PDF is brought to Word's convention for
fonts: Word embeds TrueType fonts without their `gasp` table, the browser keeps it, and Windows' renderer follows it,
smoothing glyphs it would grid-fit otherwise (Aptos, Cambria and Cambria Math at text sizes lost 7 to 12 % of a line's
ink with every glyph in place). `run` renames the `gasp` table of every embedded TrueType program
(`tools/Fidelity/Word/PdfFonts.cs`); scores before 2026-10-14 were measured without it.

Windows' renderer draws three things wrong, on both sides: content under a soft mask (reflections, fading glows) it
draws unmasked, text in Type 3 fonts filled with a pattern (the browser's outlined text with a gradient) it fills
flat, and pictures thinner than a pixel (Word's dashed table borders, drawn as tiny images) it leaves out. Where
Python can import PyMuPDF (`pip install pymupdf`), `tools/Fidelity/Word/repaint.py` repaints these places after
every rasterizing: MuPDF renders the page with and without the feature, and only the pixels that differ are taken
from MuPDF's render (thin pictures four times as fine, averaged down); all others stay as Windows drew them, so
MuPDF's other antialiasing moves no score. PyMuPDF brings MuPDF under the AGPL-3.0; it is part of this measurement
only, FreeSlate does not use it. Without it (or with `FIDELITY_NO_MUPDF=1`) the pages stay as Windows draws them, and
a note says so. `reraster` brings stored references up to date; scores before 2026-10-21 were measured without it.

Word places its metafile pictures (equations, OLE previews, ActiveX controls) with a matrix whose off-diagonal terms
are tiny but not zero (`0.13542 -0.000000754 -0.000000377 0.125 … cm`), and Windows' renderer then draws their text now
unhinted, now hinted, where the browser's PDF of the same text, with exact zeros, is hinted. With PyMuPDF,
`tools/Fidelity/Word/unshear.py` rasterizes Word's PDF from a copy whose terms below 1e-4 that place a picture are zero;
the PDF itself stays as Word wrote it. Scores before 2026-10-27 were measured without it (`reraster` brings the stored
references up to date: tdf119200 53.3 → 93.5 %, tdf138465min 84.5 → 94.6 %).

With `--skia`, `run`, `render` and `compare` measure the Skia backend (`FreeSlate.Skia`) instead of the browser: a
document's pages are drawn into a PDF (`SkiaScene.Pdf`) and rasterized as the browser's is, a deck's slides (the
hidden ones too, as PowerPoint exports them) are drawn straight to pictures at the reference's size. The renderings
and the report go to `artifacts/fidelity/skia`, so that both backends can be compared document by document.

Documents whose name begins with `markup-` (`docx/local/markup-revisions`) are rendered with their markup on both
sides: Word prints them with all markup (`ExportDocumentWithMarkup`), FreeSlate converts them with `markup: true`.
Word gives the authors of comments and tracked changes their colors in the order it meets them in a session, over all
its documents, so every such document gets a Word session of its own.

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
- `fetch docx` does the same for about 3,300 Word documents of LibreOffice, Apache POI, Apache Tika, the Open XML
  SDK, python-docx, mammoth, pandoc and the Open XML PowerTools into `spec/downloads/testdocs/`, named
  `docx/external/<source>/…` (`reference docx/external/poi`, `run docx/external/poi`).
- `tests/corpus/docx/generated/` holds the test documents `tools/Fidelity/Word/DocumentGenerator.cs` builds through
  Word's object model, anonymized like the decks; their references (`tests/fidelity/reference/docx/generated/`) are
  committed.
- `tests/corpus/pptx/local/` and `tests/corpus/docx/local/` are for probe files and private files; they stay out of
  git, and so do their references.

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
