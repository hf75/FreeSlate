# Office documents to HTML (working title)

Goal: an open-source, cross-platform engine in C# that renders PPTX and DOCX as HTML/CSS/JS with about 99 % fidelity,
and later the foundation of an editor.

## Repository layout

| Path | Content |
|---|---|
| `tools/SpecTools/` | C# tool that downloads the specifications and builds the catalog |
| `spec/ms-xsd/` | Office extension schemas (w14, p14, a14, cx, …) extracted from the MS-* documents |
| `spec/downloads/`, `spec/ecma-376/` | specification sources, not in git (`fetch` restores them) |
| `catalog/format/` | format catalog, level 1: every element, attribute and type of DOCX and PPTX, linked to the specification |
| `catalog/spec/` | section index of ECMA-376 and the Microsoft documents |

## Regenerating the catalog

```sh
dotnet run --project tools/SpecTools -- all
```

`all` downloads ECMA-376 Parts 1–4 and [MS-DOCX], [MS-PPTX], [MS-ODRAWXML] and [MS-OI29500] (about 90 MB),
extracts the Office extension schemas and writes the catalog. The single steps are `fetch`, `extract-xsd` and `catalog`.
When Microsoft publishes a new revision, the git diff of `spec/ms-xsd/` and `catalog/` shows exactly what changed.
