# Fonts the editor brings

The editor's shelf (see `Writing/EngineFonts.cs`): families that come with the program, so that a document looks the same where
they are not installed, above all on a page, which sees no fonts of the system. The desktop reads them beside the program
(`Fonts/`), a page fetches a family's files (`fonts/`) only when a document needs them.

| Family | Stands in for | From | License |
|---|---|---|---|
| Carlito | Calibri | Google Fonts | OFL 1.1 |
| Caladea | Cambria | Chrome OS's extra fonts (crosextrafonts-20130214, version 1.002) | Apache 2.0 |
| Gelasio | Georgia | Google Fonts | OFL 1.1 |
| Arimo | Arial | Google Fonts | OFL 1.1 |
| Tinos | Times New Roman | Google Fonts | OFL 1.1 |
| Cousine | Courier New | Google Fonts | OFL 1.1 |
| Roboto, Open Sans, Lato, Montserrat, Lora, Playfair Display | | Google Fonts | OFL 1.1 |

The first six are metric twins: their glyphs are as wide as those of the Office fonts they stand in for, and the engine gives them
the originals' line metrics (`src/FreeSlate/Fonts/MetricTwins.cs`). Google Fonts' own Caladea is a later, narrower design, so the
shelf has the one of 2013.

Each family is here in four static styles (Regular, Bold, Italic, BoldItalic), as Google Fonts' download gave them on
2026-11-07, with its license beside them.
