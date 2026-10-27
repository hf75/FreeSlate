"""Writes a copy of Word's PDF whose pictures stand without the tiny shear Word gives them, for Windows' renderer.

Usage: unshear.py --pdf <file> --out <copy>

Word places its metafile pictures (EMF, WMF: equations, OLE previews, ActiveX controls) as form XObjects with a matrix
whose off-diagonal terms are tiny but not zero (0.13542 -0.000000754 -0.000000377 0.125 290.21 768.76 cm), and
Windows.Data.Pdf then draws the forms' text now unhinted, now hinted (LibreOffice's extras-ooxmlimport/tdf119200 and
extras-layout/tdf138465min unhinted, extras-odfexport/2_MathType3 hinted, whatever the size of the terms), where the
browser's PDF of the same text, with exact zeros, is hinted. The copy has terms below 1e-4 that place a picture (a cm
just before a Do) set to zero; the PDF itself stays as Word wrote it.

Optional part of the measurement (tools/Fidelity calls it where Python can import PyMuPDF, as repaint.py): needs
PyMuPDF (`pip install pymupdf`), which brings MuPDF under the AGPL-3.0. FreeSlate itself does not use it.
Prints "unsheared <n>", the number of matrices changed; writes the copy only where that is more than 0.
"""
import argparse
import re

import pymupdf

pymupdf.TOOLS.mupdf_display_errors(False)

NUMBER = r"[-+]?(?:\d+\.?\d*|\.\d+)"
PLACING = re.compile(rf"(?<![\w.+-])({NUMBER})(\s+)({NUMBER})(\s+)({NUMBER})(\s+)({NUMBER}\s+{NUMBER}\s+{NUMBER}\s+cm\s*/[^\s/<>\[\]()]+\s+Do)")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--pdf", required=True)
    parser.add_argument("--out", required=True)
    args = parser.parse_args()
    doc = pymupdf.open(args.pdf)
    changed = 0

    def zeroed(match):
        nonlocal changed
        b, c = float(match.group(3)), float(match.group(5))
        if (b == 0 and c == 0) or abs(b) >= 1e-4 or abs(c) >= 1e-4:
            return match.group(0)
        changed += 1
        return f"{match.group(1)}{match.group(2)}0{match.group(4)}0{match.group(6)}{match.group(7)}"

    for page in doc:
        for xref in page.get_contents():
            source = doc.xref_stream(xref).decode("latin1")
            result = PLACING.sub(zeroed, source)
            if result != source:
                doc.update_stream(xref, result.encode("latin1"))
    if changed:
        doc.save(args.out)
    print(f"unsheared {changed}")


if __name__ == "__main__":
    main()
