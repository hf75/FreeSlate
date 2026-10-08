# FreeSlate

An open-source, cross-platform engine in C# that renders PPTX and DOCX as HTML/CSS/JS with about 99 % fidelity,
and later the foundation of an editor.

## Repository layout

| Path | Content |
|---|---|
| `src/FreeSlate/` | the rendering engine (PPTX → HTML for now) |
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
it, or with a wrong one, `FreeSlate.Opc.PasswordException` says which.

## Measuring fidelity

```sh
dotnet run --project tools/Fidelity -- run
```

Converts every deck of the corpus, takes a screenshot of each slide with headless Chrome or Edge and compares it with
PowerPoint's reference rendering. The report (`artifacts/fidelity/report.html`) shows reference, our rendering and a
difference image for every slide. The score is the share of content pixels that match the reference within a small
tolerance. On Windows with Office installed, `generate` rebuilds the test decks and `reference` re-renders the references.

## Regenerating the catalogs

```sh
dotnet run --project tools/SpecTools -- all
```

`all` downloads ECMA-376 Parts 1–4 and [MS-DOCX], [MS-PPTX], [MS-ODRAWXML] and [MS-OI29500] (about 90 MB),
extracts the Office extension schemas, writes the format catalog and checks the feature catalogs
(`catalog/features/*.yaml`). When Microsoft publishes a new revision, the git diff of `spec/ms-xsd/` and `catalog/`
shows exactly what changed.
