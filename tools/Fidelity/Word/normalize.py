"""Writes a copy of a PDF (Word's or the browser's) with what Windows' renderer draws differently for the same thing
brought to one form, before rasterizing; the PDF itself stays as it was written.

Usage: normalize.py --pdf <file> --out <copy>

- Word places its metafile pictures (EMF, WMF: equations, OLE previews, ActiveX controls) as form XObjects with a
  matrix whose off-diagonal terms are tiny but not zero (0.13542 -0.000000754 -0.000000377 0.125 290.21 768.76 cm), and
  Windows.Data.Pdf then draws the forms' text now unhinted, now hinted (LibreOffice's extras-ooxmlimport/tdf119200 and
  extras-layout/tdf138465min unhinted, extras-odfexport/2_MathType3 hinted, whatever the size of the terms), where the
  browser's PDF of the same text, with exact zeros, is hinted: terms below 1e-4 that place a picture (a cm just before
  a Do) become zero.
- Windows.Data.Pdf fills a path of one rectangle written with re without antialiasing, and the same rectangle written
  as a path (m, three l, h) or among others with it; Word writes a chart series' bars as one path of such rectangles
  and a bar of its own format with re (LibreOffice's extras-layout/tdf138773), the browser every rectangle with re of
  its own: a filled path made of nothing but axis-aligned rectangles, none overlapping another, becomes a fill of one
  re for each. Paths are read line by line, as Word and the browser write their operators one to a line.

Optional part of the measurement (tools/Fidelity calls it where Python can import PyMuPDF, as repaint.py): needs
PyMuPDF (`pip install pymupdf`), which brings MuPDF under the AGPL-3.0. FreeSlate itself does not use it.
Prints "unsheared <n> rectangles <m>", the matrices and paths changed; writes the copy only where either is more than 0.
"""
import argparse
import re

import pymupdf

pymupdf.TOOLS.mupdf_display_errors(False)

NUMBER = r"[-+]?(?:\d+\.?\d*|\.\d+)"
PLACING = re.compile(rf"(?<![\w.+-])({NUMBER})(\s+)({NUMBER})(\s+)({NUMBER})(\s+)({NUMBER}\s+{NUMBER}\s+{NUMBER}\s+cm\s*/[^\s/<>\[\]()]+\s+Do)")
IS_NUMBER = re.compile(rf"{NUMBER}$")
PATH_OPERATORS = {"m": 2, "l": 2, "c": 6, "v": 4, "y": 4, "h": 0, "re": 4}
FILLS = {"f", "f*", "F"}


def rectangle(points):
    """The corner and the positive sides of a closed subpath that is an axis-aligned rectangle, else None."""
    if len(points) == 5 and points[4] == points[0]:
        points = points[:4]
    if len(points) != 4:
        return None
    for i in range(4):
        (ax, ay), (bx, by) = points[i], points[(i + 1) % 4]
        if ax != bx and ay != by:
            return None
    xs, ys = sorted({p[0] for p in points}), sorted({p[1] for p in points})
    if len(xs) != 2 or len(ys) != 2:
        return None
    return xs[0], ys[0], xs[1] - xs[0], ys[1] - ys[0]


def rectangles_of(lines):
    """The rectangles of a path's lines when it is nothing but closed axis-aligned rectangles, none overlapping another."""
    found, points = [], None
    for tokens in lines:
        op = tokens[-1]
        if op == "m":
            if points is not None:
                return None
            points = [(float(tokens[0]), float(tokens[1]))]
        elif op == "l" and points is not None:
            points.append((float(tokens[0]), float(tokens[1])))
        elif op == "h" and points is not None:
            box = rectangle(points)
            if box is None:
                return None
            found.append(box)
            points = None
        else:
            return None
    if points is not None or not found:
        return None
    for i, (x, y, w, h) in enumerate(found):
        for (x2, y2, w2, h2) in found[i + 1:]:
            if x < x2 + w2 and x2 < x + w and y < y2 + h2 and y2 < y + h:
                return None
    return found


def number(v):
    text = f"{v:.5f}".rstrip("0").rstrip(".")
    return "0" if text in ("", "-0") else text


def squared(source):
    """The content with filled paths of rectangles as fills of one re each, and how many paths changed."""
    lines = source.splitlines(keepends=True)
    out, path, changed = [], [], 0
    for line in lines:
        tokens = line.split()
        op = tokens[-1] if tokens else None
        if op in PATH_OPERATORS and len(tokens) == PATH_OPERATORS[op] + 1 and all(IS_NUMBER.match(t) for t in tokens[:-1]):
            path.append((line, tokens))
            continue
        if op in FILLS and len(tokens) == 1 and path and (boxes := rectangles_of([t for _, t in path])) is not None:
            end = "\r\n" if line.endswith("\r\n") else "\n"
            out.extend(f"{number(x)} {number(y)} {number(w)} {number(h)} re{end}{op}{end}" for x, y, w, h in boxes)
            changed += 1
        else:
            out.extend(l for l, _ in path)
            out.append(line)
        path = []
    out.extend(l for l, _ in path)
    return "".join(out), changed


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--pdf", required=True)
    parser.add_argument("--out", required=True)
    args = parser.parse_args()
    doc = pymupdf.open(args.pdf)
    unsheared = rectangles = 0

    def zeroed(match):
        nonlocal unsheared
        b, c = float(match.group(3)), float(match.group(5))
        if (b == 0 and c == 0) or abs(b) >= 1e-4 or abs(c) >= 1e-4:
            return match.group(0)
        unsheared += 1
        return f"{match.group(1)}{match.group(2)}0{match.group(4)}0{match.group(6)}{match.group(7)}"

    for page in doc:
        for xref in page.get_contents():
            source = doc.xref_stream(xref).decode("latin1")
            result, changed = squared(PLACING.sub(zeroed, source))
            rectangles += changed
            if result != source:
                doc.update_stream(xref, result.encode("latin1"))
    if unsheared or rectangles:
        doc.save(args.out)
    print(f"unsheared {unsheared} rectangles {rectangles}")


if __name__ == "__main__":
    main()
