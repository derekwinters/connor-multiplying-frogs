#!/usr/bin/env python3
"""Rebuild the Multiplying Frogs splash picture as separate, stackable layers.

Reads `Assets/Art/splash-source.png` (read-only) and writes, under art-source/splash/:

    layers/<id>.svg, layers/<id>.png   one per object, on the shared full-frame canvas
    composite.svg, composite.png       every layer stacked back together
    objects/<id>.png                   each notable object cropped on its own (its vector is
                                       layers/<id>.svg; no cropped SVG is kept, as it would
                                       only repeat that file's paths)
    review/*.png                       the images for reviewing the rebuild

Every coordinate is a pixel of the original (1586 x 992): all SVGs share
`viewBox="0 0 1586 992"`, so any set of layers stacks straight back together. Layer and
composite PNGs are rendered 3965 x 2480: at least twice the 1920 x 1200 tablet both ways,
and an exact multiple of the canvas (5/2 of it), so no edge row is half covered.

Deterministic: running it twice writes byte-identical files. `--check-rebuild` proves it
by building a second time from scratch (every cut-out and trace recomputed) and comparing.

    pip install vtracer opencv-python-headless scipy cairosvg pillow numpy fonttools skia-pathops
    python3 art-source/splash/build_splash.py                  # build + self-checks
    python3 art-source/splash/build_splash.py --check-rebuild  # ... and a second build to compare

See README.md in this folder for the layers and how each is made.
"""
from __future__ import annotations

import argparse
import hashlib
import io
import shutil
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import cutout as cu  # noqa: E402
import sky  # noqa: E402
import title as ti  # noqa: E402
import tracing as tr  # noqa: E402

LAYERS_DIR = HERE / "layers"
OBJECTS_DIR = HERE / "objects"
REVIEW_DIR = HERE / "review"
COMPOSITE = HERE / "composite"

EXPORT_W = 3965            # layer / composite PNG width: 2.5x the canvas, over 2x the tablet
OBJECT_LONG = 2000         # standalone object PNGs are this long on their long side
OBJECT_PAD = 6             # original px around an object in its standalone crop
REVIEW_W = cu.W            # review images are drawn at the original's size

# Back to front. (family, [object ids]); each family is one Inkscape layer in the
# composite, each object one group (and one file in layers/).
FAMILIES = [
    ("sky", ["sky"]),
    ("title-glow", ["title-glow"]),
    ("clouds", ["cloud-left", "cloud-right"]),
    ("banks", ["bank-left", "bank-right"]),
    ("reeds", ["reeds-left", "reeds-right"]),
    ("pond", ["pond"]),
    ("lily-pads", ["pad-left-back", "pad-centre-left", "pad-right-back", "pad-left-front", "pad-right-front"]),
    ("water-lilies", ["lily-left", "lily-right"]),
    ("splashes", ["splash-left", "splash-centre-left", "splash-right"]),
    ("hop-arcs", ["arc-left", "arc-centre-left", "arc-right"]),
    ("grass", ["grass"]),
    ("grass-tufts", ["tuft-left", "tuft-right"]),
    ("log", ["log"]),
    ("frogs", ["frog-left", "frog-centre-left", "frog-right", "frog-big"]),
    ("title", ["title"]),
]
LABELS = {"sky": "Sky", "title-glow": "Glow behind the title", "title": "Title: MULTIPLYING FROGS"}
FROGS = ["frog-left", "frog-centre-left", "frog-right", "frog-big"]
STANDALONE = ["title", "cloud-left", "cloud-right", "reeds-left", "reeds-right", "pad-left-back",
              "pad-centre-left", "pad-right-back", "pad-left-front", "pad-right-front", "lily-left",
              "lily-right", "tuft-left", "tuft-right", "log"] + FROGS
WATER_LINE = 600           # original px: below it, behind the big frog, is water and log
GROUND = ["pond", "log", "grass", "pad-left-back", "pad-centre-left", "pad-right-back",
          "pad-left-front", "pad-right-front"]

NS = ('xmlns="http://www.w3.org/2000/svg" '
      'xmlns:inkscape="http://www.inkscape.org/namespaces/inkscape"')


def object_ids():
    return [oid for _, ids in FAMILIES for oid in ids]


def label(oid):
    return LABELS.get(oid) or cu.OBJ[oid]["label"]


# --------------------------------------------------------------------------
# SVG
# --------------------------------------------------------------------------
def traced_body(oid):
    """(defs, inner groups) of one traced object."""
    _, groups, defs = tr.trace_object(oid)
    out = []
    for name, paths in groups:
        ps = "\n".join(f'<path fill="{fill}" d="{d}"/>' for fill, d in paths)
        out.append(f'<g id="{oid}-{name}" inkscape:label="{name}">\n{ps}\n</g>')
    return defs, "\n".join(out) + "\n"


def object_svg_parts(oid):
    """(defs, body) of one object: a group with its id holding everything it draws."""
    if oid == "sky":
        defs, inner = sky.sky_svg()
    elif oid == "title-glow":
        defs, inner = sky.glow_svg()
    elif oid == "title":
        strokes = []
        sdefs = []
        for sid in ("title-strokes-left", "title-strokes-right"):
            d, b = traced_body(sid)
            sdefs.append(d)
            strokes.append(f'<g id="{sid}" inkscape:label="{cu.OBJ[sid]["label"]}">\n{b}</g>\n')
        defs, inner = ti.title_group("".join(strokes))
        defs += "".join(sdefs)
    else:
        defs, inner = traced_body(oid)
    body = f'<g id="{oid}" inkscape:label="{label(oid)}">\n{inner}</g>\n'
    return defs, body


_PARTS = {}


def parts(oid):
    if oid not in _PARTS:
        _PARTS[oid] = object_svg_parts(oid)
    return _PARTS[oid]


def svg_doc(defs, body, view=(0, 0, cu.W, cu.H), title="Multiplying Frogs splash"):
    x, y, w, h = view
    return (f'<?xml version="1.0" encoding="UTF-8"?>\n'
            f'<svg {NS} width="{tr.fmt(w)}" height="{tr.fmt(h)}" viewBox="{tr.fmt(x)} {tr.fmt(y)} {tr.fmt(w)} {tr.fmt(h)}">\n'
            f'<title>{title}</title>\n<defs>\n{defs}</defs>\n{body}</svg>\n')


def layer_svg(oid):
    defs, body = parts(oid)
    wrapped = (f'<g id="layer-{oid}" inkscape:groupmode="layer" inkscape:label="{label(oid)}">\n'
               f'{body}</g>\n')
    return svg_doc(defs, wrapped, title=f"Multiplying Frogs splash - {label(oid)}")


def stack_svg(ids, title="Multiplying Frogs splash"):
    """The given objects stacked in family order, one Inkscape layer per family."""
    defs, body = [], []
    for fam, members in FAMILIES:
        inner = []
        for oid in members:
            if oid in ids:
                d, b = parts(oid)
                defs.append(d)
                inner.append(b)
        if inner:
            body.append(f'<g id="layer-{fam}" inkscape:groupmode="layer" inkscape:label="{fam}">\n'
                        + "".join(inner) + "</g>\n")
    return svg_doc("".join(defs), "".join(body), title=title)


def render(svg, width):
    import cairosvg
    png = cairosvg.svg2png(bytestring=svg.encode("utf-8"), output_width=width)
    return Image.open(io.BytesIO(png)).convert("RGBA")


def save_png(img, path):
    img.save(path, format="PNG", optimize=True)


def write(path, text):
    path.write_text(text, encoding="utf-8", newline="\n")


# --------------------------------------------------------------------------
# Standalone objects
# --------------------------------------------------------------------------
def object_view(oid):
    if oid == "title":
        x0, y0, x1, y1 = ti.TITLE_ZONE
        return (x0 - 30, y0 - 10, x1 - x0 + 60, y1 - y0 + 20)
    r = cu.regions()[oid][0]
    ys, xs = np.nonzero(r)
    x0, y0 = xs.min() - OBJECT_PAD, ys.min() - OBJECT_PAD
    return (int(x0), int(y0), int(xs.max() - x0 + 1 + OBJECT_PAD), int(ys.max() - y0 + 1 + OBJECT_PAD))


def standalone_svg(oid):
    defs, body = parts(oid)
    return svg_doc(defs, body, view=object_view(oid), title=f"Multiplying Frogs splash - {label(oid)}")


def standalone_png(oid):
    _, _, w, h = object_view(oid)
    width = OBJECT_LONG if w >= h else int(round(OBJECT_LONG * w / h))
    return render(standalone_svg(oid), width)


# --------------------------------------------------------------------------
# Review images
# --------------------------------------------------------------------------
def labelled(img, text):
    out = Image.new("RGB", (img.width, img.height + 28), "white")
    out.paste(img.convert("RGB"), (0, 28))
    ImageDraw.Draw(out).text((8, 8), text, fill="black")
    return out


def stack_vertical(imgs):
    w = max(i.width for i in imgs)
    out = Image.new("RGB", (w, sum(i.height for i in imgs)), "white")
    y = 0
    for i in imgs:
        out.paste(i, (0, y))
        y += i.height
    return out


def checker(size, cell=12):
    w, h = size
    yy, xx = np.mgrid[0:h, 0:w]
    c = np.where(((xx // cell) + (yy // cell)) % 2 == 0, 236, 204).astype(np.uint8)
    return Image.fromarray(np.stack([c, c, c, np.full_like(c, 255)], -1), "RGBA")


def layer_sheet(layer_pngs):
    """Every object on its own, cropped, on a checkerboard, with its id."""
    tiles = []
    for oid in object_ids():
        img = layer_pngs[oid]
        a = np.asarray(img)[..., 3]
        ys, xs = np.nonzero(a > 0)
        crop = img.crop((int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1))
        crop.thumbnail((300, 200), Image.LANCZOS)
        bg = checker(crop.size)
        bg.alpha_composite(crop)
        tiles.append(labelled(bg, oid))
    cols, tw, th = 6, 312, 236
    rows = (len(tiles) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * tw, rows * th), "white")
    for i, t in enumerate(tiles):
        sheet.paste(t, ((i % cols) * tw + 6, (i // cols) * th + 4))
    return sheet


def reviews(layer_pngs):
    original = Image.open(cu.ORIGINAL).convert("RGB")
    ids = object_ids()
    composite = render(stack_svg(ids), REVIEW_W).convert("RGB")
    no_frogs = render(stack_svg([o for o in ids if o not in FROGS]), REVIEW_W).convert("RGB")
    bare = render(stack_svg([o for o in ids if o not in FROGS + ["title", "title-glow"]]), REVIEW_W).convert("RGB")
    out = {
        "original-vs-composite.png": stack_vertical([labelled(original, "Original (splash-source.png)"),
                                                     labelled(composite, "Rebuilt from layers (composite)")]),
        "no-frogs.png": labelled(no_frogs, "Every layer except the four frogs"),
        "no-frogs-no-title.png": labelled(bare, "Every layer except the frogs, the title and its glow"),
        "layer-sheet.png": layer_sheet(layer_pngs),
    }
    x0, y0, x1, y1 = ti.TITLE_ZONE
    out["title-vs-original.png"] = stack_vertical([
        labelled(original.crop((x0, y0, x1, y1)), "Original title (misspelt MUILTIPLYING)"),
        labelled(composite.crop((x0, y0, x1, y1)), "Rebuilt title (MULTIPLYING)")])
    return out


# --------------------------------------------------------------------------
# Build
# --------------------------------------------------------------------------
def build():
    """Write every output. Returns {object id: layer PNG image}."""
    for d in (LAYERS_DIR, OBJECTS_DIR, REVIEW_DIR):
        if d.exists():
            shutil.rmtree(d)
        d.mkdir(parents=True)
    ids = object_ids()
    layer_pngs = {}
    for oid in ids:
        svg = layer_svg(oid)
        write(LAYERS_DIR / f"{oid}.svg", svg)
        img = render(svg, EXPORT_W)
        save_png(img, LAYERS_DIR / f"{oid}.png")
        layer_pngs[oid] = img
        print(f"  layer {oid}", flush=True)
    comp = stack_svg(ids)
    write(COMPOSITE.with_suffix(".svg"), comp)
    save_png(render(comp, EXPORT_W), COMPOSITE.with_suffix(".png"))
    for oid in STANDALONE:
        save_png(standalone_png(oid), OBJECTS_DIR / f"{oid}.png")
    for name, img in reviews(layer_pngs).items():
        save_png(img, REVIEW_DIR / name)
    return layer_pngs


def outputs():
    files = [COMPOSITE.with_suffix(".svg"), COMPOSITE.with_suffix(".png")]
    for d in (LAYERS_DIR, OBJECTS_DIR, REVIEW_DIR):
        files += sorted(d.iterdir())
    return files


def digests():
    return {str(p.relative_to(HERE)): hashlib.sha256(p.read_bytes()).hexdigest() for p in outputs()}


def clear_caches():
    _PARTS.clear()
    for m in (cu, sky, ti, tr):
        m.clear_caches()


# --------------------------------------------------------------------------
# Self-checks
# --------------------------------------------------------------------------
def check(layer_pngs):
    import re
    problems = []
    ids = object_ids()
    h = int(round(cu.H * EXPORT_W / cu.W))
    for oid, img in layer_pngs.items():
        if img.size != (EXPORT_W, h) or img.width < 1920 or img.height < 1200:
            problems.append(f"{oid}: layer PNG is {img.size}")
        if not np.asarray(img)[..., 3].any():
            problems.append(f"{oid}: layer PNG is empty")
    if np.asarray(layer_pngs["sky"])[..., 3].min() != 255:
        problems.append("sky: not opaque everywhere")
    comp = COMPOSITE.with_suffix(".svg").read_text(encoding="utf-8")
    all_ids = re.findall(r'\bid="([^"]+)"', comp)
    dupes = sorted({i for i in all_ids if all_ids.count(i) > 1})
    if dupes:
        problems.append(f"composite: duplicate ids {dupes[:5]}")
    for oid in ids:
        if f'<g id="{oid}" ' not in comp:
            problems.append(f"composite: no group with id {oid}")
    if ti.spelled() != ("MULTIPLYING", "FROGS"):
        problems.append(f"title spells {ti.spelled()}")
    # no frog-shaped hole: under the big frog, below the water line, water / log / pads
    # cover every pixel
    s = EXPORT_W / cu.W
    big = np.asarray(layer_pngs["frog-big"])[..., 3] > 0
    big[: int(WATER_LINE * s)] = False
    clear = np.ones(big.shape)
    for g in GROUND:
        clear *= 1 - np.asarray(layer_pngs[g])[..., 3] / 255.0
    cover = clear <= 1 / 255.0
    if (big & ~cover).sum():
        problems.append(f"no-frogs: {(big & ~cover).sum()} px under the big frog are not covered")
    # each small frog: what is under it, with the frogs gone, is sky or ground, never a hole
    nof = render(stack_svg([o for o in ids if o not in FROGS]), cu.W)
    if np.asarray(nof)[..., 3].min() != 255:
        problems.append("no-frogs composite is not opaque")
    for oid in STANDALONE:
        img = Image.open(OBJECTS_DIR / f"{oid}.png")
        if max(img.size) < OBJECT_LONG - 1:
            problems.append(f"objects/{oid}.png is only {img.size}")
    return problems


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check-rebuild", action="store_true",
                    help="build a second time from scratch and require byte-identical files")
    args = ap.parse_args()
    print("building ...", flush=True)
    layer_pngs = build()
    problems = check(layer_pngs)
    if args.check_rebuild:
        first = digests()
        clear_caches()
        print("building again to compare ...", flush=True)
        build()
        second = digests()
        changed = sorted(k for k in set(first) | set(second) if first.get(k) != second.get(k))
        if changed:
            problems.append(f"second build differs: {changed[:8]}")
        else:
            print(f"second build is byte-identical ({len(first)} files)")
    for p in problems:
        print("FAIL:", p)
    print("self-checks:", "FAILED" if problems else "passed")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
