# SmartArt test decks

PowerPoint (via COM) lays out built-in SmartArt layouts at chosen node counts, trees or frame sizes and saves its
drawings; `Fidelity.exe smartart <deck> --verbose` compares the engine's layout of the same data with them.

- `Generate.ps1 -Out deck.pptx -Ids "cycle2,cycle5" -Counts "1,2,3,4,5,6,8" [-Mode children]`: top-level nodes
  (or one node with that many children).
- `GenerateTrees.ps1 -Out deck.pptx -Ids "orgChart1,hierarchy1" -Specs "1,2,3,2x2,3x1"`: one root with children per
  level.
- `GenerateFrames.ps1 -Out deck.pptx -Id BendingPictureBlocks -Count "1,2" -Frames "840x460,500x460"`: one layout at
  several frame sizes.
- `compare_slides.py before.txt after.txt`: per-slide mean error of two verbose runs; `compare_layouts.py`: per-layout
  error of two summary runs.

Generated decks go to `tests/corpus/pptx/local/` (not committed).
