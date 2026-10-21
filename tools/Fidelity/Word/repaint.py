"""Repaints the parts of Windows.Data.Pdf's page rasters that Windows draws wrong, with MuPDF's pixels.

Usage: repaint.py --pdf <file> --out <dir> [--scale 1] [--sizes w1xh1,w2xh2,...]

rasterize-pdf.ps1 has written <out>/page-001.png, ... (96 dpi times the scale, page i cut to size i in points from its
top left). Windows' renderer draws three things wrong:
- content under a soft mask (an ExtGState's /SMask: reflections, fading glows) it draws as if unmasked;
- text in Type 3 fonts filled with a pattern (Chrome's outlined text with a gradient) it fills flat;
- pictures thinner than a pixel (Word's dashed table borders, drawn as tiny images) it leaves out.
For each of these a page has, MuPDF renders the page as it is and without it; the pixels that differ, grown by one for
their edges, are taken from MuPDF's render, all others stay as Windows drew them. Pages without these features stay
untouched, so scores elsewhere do not move with MuPDF's other antialiasing.

Optional part of the measurement (tools/Fidelity calls it where Python can import PyMuPDF): needs PyMuPDF
(`pip install pymupdf`), which brings MuPDF under the AGPL-3.0. FreeSlate itself does not use it.
Prints one line per repainted page: "page-001.png <pixels> <features>".
"""
import argparse
import os
import re
import sys

import pymupdf
from PIL import Image, ImageChops, ImageFilter

pymupdf.TOOLS.mupdf_display_errors(False)


def skip_value(source, at):
    """The end of the PDF value that starts at source[at]: a dictionary, an array, a reference, a name or a number."""
    n = len(source)
    while at < n and source[at] in " \r\n\t":
        at += 1
    if source.startswith("<<", at):
        depth, i = 0, at
        while i < n:
            if source.startswith("<<", i):
                depth, i = depth + 1, i + 2
            elif source.startswith(">>", i):
                depth, i = depth - 1, i + 2
                if depth == 0:
                    return i
            else:
                i += 1
        return n
    if at < n and source[at] == "[":
        depth, i = 0, at
        while i < n:
            if source[i] == "[":
                depth += 1
            elif source[i] == "]":
                depth -= 1
                if depth == 0:
                    return i + 1
            i += 1
        return n
    m = re.compile(r"\d+\s+\d+\s+R|/[^\s/<>\[\]()]*|[-+.\d]+|true|false|null").match(source, at)
    return m.end() if m else at + 1


def without_soft_masks(path):
    """The document with every graphics state's soft mask taken out, or None where no graphics state has one."""
    copy = pymupdf.open(path)
    changed = False
    for xref in range(1, copy.xref_length()):
        try:
            source = copy.xref_object(xref, compressed=True)
        except Exception:
            continue
        # A picture's /SMask is its alpha channel, which Windows draws right.
        if "/SMask" not in source or "/Subtype/Image" in source.replace(" ", ""):
            continue
        parts, at = [], 0
        for m in re.finditer(r"/SMask(?![A-Za-z])", source):
            if m.start() < at:
                continue
            end = skip_value(source, m.end())
            if source[m.end():end].strip() != "/None":
                parts.append(source[at:m.end()] + " /None")
                at = end
                changed = True
        if at:
            copy.update_object(xref, "".join(parts) + source[at:])
    return copy if changed else None


TOKEN = re.compile(rb"/[^\s/<>\[\]()%{}]*|\((?:\\.|[^\\)])*\)|<<|>>|<[0-9A-Fa-f\s]*>|\[|\]|[-+]?(?:\d+\.?\d*|\.\d+)"
                   rb"|[A-Za-z'\"*]+|%[^\r\n]*")


def pattern_type3_fonts(page):
    """The resource names of the Type 3 fonts the page's content shows text in while it fills with a pattern."""
    type3 = {f[4] for f in page.get_fonts(full=True) if f[2] == "Type3"}
    if not type3:
        return set()
    found = set()
    state = {"cs": None, "pattern": False, "font": None}
    stack, operands = [], []
    for token in TOKEN.findall(page.read_contents()):
        if token.startswith(b"%"):
            continue
        if token[:1].isalpha() or token in (b"'", b'"'):
            op = token.decode("latin1")
            if op == "q":
                stack.append(dict(state))
            elif op == "Q" and stack:
                state = stack.pop()
            elif op == "cs" and operands:
                state["cs"], state["pattern"] = operands[-1].decode("latin1"), False
            elif op in ("scn", "sc") and operands:
                state["pattern"] = state["cs"] == "/Pattern" and operands[-1].startswith(b"/")
            elif op in ("g", "rg", "k"):
                state["pattern"] = False
            elif op == "Tf" and len(operands) >= 2:
                state["font"] = operands[-2].decode("latin1").lstrip("/")
            elif op in ("Tj", "TJ", "'", '"') and state["pattern"] and state["font"] in type3:
                found.add(state["font"])
            operands = []
        else:
            operands.append(token)
    return found


def without_type3_glyphs(path, page_no, fonts):
    """The document with the named Type 3 fonts of the page drawing nothing."""
    copy = pymupdf.open(path)
    for f in copy[page_no].get_fonts(full=True):
        if f[2] == "Type3" and f[4] in fonts:
            kind, value = copy.xref_get_key(f[0], "CharProcs")
            if kind == "xref":
                value = copy.xref_object(int(value.split()[0]), compressed=True)
            for glyph in re.findall(r"(\d+)\s+0\s+R", value):
                copy.update_stream(int(glyph), b"0 0 d0")
    return copy


def thin_pictures(page, zoom_x, zoom_y):
    """The resource names of the pictures the page draws thinner than a pixel (somewhere: placements are found by the
    picture's content, which Word repeats in many objects)."""
    names = set()
    for item in page.get_images(full=True):
        if any(min(r.width * zoom_x, r.height * zoom_y) < 1 for r in page.get_image_rects(item)):
            names.add(item[7])
    return names


def without_pictures(path, page_no, names):
    """The document without the named pictures on the page: its content no longer draws them (Do)."""
    copy = pymupdf.open(path)
    pattern = re.compile(rb"/(?:" + b"|".join(re.escape(n.encode("latin1")) for n in sorted(names)) + rb")\s+Do\b")
    for contents in copy[page_no].get_contents():
        copy.update_stream(contents, pattern.sub(b"", copy.xref_stream(contents)))
    return copy


def render(doc, page_no, clip, zoom_x, zoom_y, size, supersample=1):
    """The page as MuPDF draws it, mapped onto Windows' raster; drawn <supersample> times as fine and averaged down
    where MuPDF would round what is thinner than a pixel up to a whole one."""
    pix = doc[page_no].get_pixmap(matrix=pymupdf.Matrix(zoom_x * supersample, zoom_y * supersample), clip=clip, alpha=False)
    image = Image.frombytes("RGB", (pix.width, pix.height), pix.samples)
    full = (size[0] * supersample, size[1] * supersample)
    if image.size != full:
        canvas = Image.new("RGB", full, "white")
        canvas.paste(image, (0, 0))
        image = canvas
    return image if supersample == 1 else image.resize(size, Image.BOX)


def differing(a, b):
    """Where two renders differ in any channel, as a mask."""
    red, green, blue = ImageChops.difference(a, b).split()
    return ImageChops.lighter(ImageChops.lighter(red, green), blue).point(lambda v: 255 if v else 0)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--pdf", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--scale", type=float, default=1)
    parser.add_argument("--sizes", default="")
    args = parser.parse_args()
    doc = pymupdf.open(args.pdf)
    cuts = [tuple(float(v) for v in s.split("x")) for s in args.sizes.split(",") if s]
    masks = without_soft_masks(args.pdf)
    for page_no in range(doc.page_count):
        png = os.path.join(args.out, f"page-{page_no + 1:03}.png")
        if not os.path.exists(png):
            continue
        page = doc[page_no]
        width, height = cuts[page_no] if page_no < len(cuts) else (page.rect.width, page.rect.height)
        windows = Image.open(png)
        windows.load()
        size = windows.size
        # Windows maps the cut onto whole pixels; MuPDF draws with the same zoom across and down.
        zoom_x, zoom_y = size[0] / width, size[1] / height
        clip = pymupdf.Rect(page.rect.x0, page.rect.y0, page.rect.x0 + width, page.rect.y0 + height)
        # Each feature with the render its pixels come from: thin pictures four times as fine, averaged down.
        variants = []
        if masks is not None:
            variants.append(("soft masks", masks, 1))
        if fonts := pattern_type3_fonts(page):
            variants.append(("Type 3 text filled with a pattern", without_type3_glyphs(args.pdf, page_no, fonts), 1))
        if thin := thin_pictures(page, zoom_x, zoom_y):
            variants.append(("pictures thinner than a pixel", without_pictures(args.pdf, page_no, thin), 4))
        if not variants:
            continue
        mupdf = render(doc, page_no, clip, zoom_x, zoom_y, size)
        result, painted, names = windows, Image.new("L", size, 0), []
        for name, variant, supersample in variants:
            mask = differing(mupdf, render(variant, page_no, clip, zoom_x, zoom_y, size))
            if not mask.getbbox():
                continue
            mask = mask.filter(ImageFilter.MaxFilter(3))
            source = mupdf if supersample == 1 else render(doc, page_no, clip, zoom_x, zoom_y, size, supersample)
            result = Image.composite(source.convert(windows.mode), result, mask)
            painted = ImageChops.lighter(painted, mask)
            names.append(name)
        if not names:
            continue
        result.save(png)
        print(f"page-{page_no + 1:03}.png {sum(painted.histogram()[1:])} {', '.join(names)}")


if __name__ == "__main__":
    sys.exit(main())
