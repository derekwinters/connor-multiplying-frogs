#!/usr/bin/env python3
"""Rebuild the Multiplying Frogs splash picture as separate, stackable layers.

Reads `Assets/Art/splash-source.png` (read-only) and writes, under art-source/splash/:

    layers/<id>.svg, layers/<id>.png   one per object, on the shared full-frame canvas
    composite.svg, composite.png       every layer stacked back together
    objects/<id>.png                   each notable object cropped on its own (its vector is
                                       layers/<id>.svg; no cropped SVG is kept, as it would
                                       only repeat that file's paths)
    review/*.png                       the images for reviewing the rebuild, and the app
                                       icon in three launcher shapes (app-icon-masks.png)
    ../../Assets/Art/app-icon-source.png
                                       the app icon: a square crop of the big frog on the
                                       log, title hidden (window and hidden layers in
                                       icon.py; #483)
    ../../Assets/Art/Splash/splash-1920x1200.png
                                       the launch splash: the whole composite at the
                                       tablet's 1920 x 1200 (window in launch.py; #482)
    ../../docs/specs/ui/images/title-screen/{scene,scene-no-log,title}.png
                                       the title screen's wireframe art: the splash
                                       without the frogs, in pieces (home.py; #484)

Every coordinate is a pixel of the original (1586 x 992): all SVGs share
`viewBox="0 0 1586 992"`, so any set of layers stacks straight back together. Layer and
composite PNGs are rendered 3965 x 2480: at least twice the 1920 x 1200 tablet both ways,
and an exact multiple of the canvas (5/2 of it), so no edge row is half covered.

Deterministic: running it twice writes byte-identical files. `--check-rebuild` proves it
by building a second time from scratch (every cut-out and trace recomputed) and comparing.

    pip install vtracer opencv-python-headless scipy cairosvg pillow numpy fonttools skia-pathops
    python3 art-source/splash/build_splash.py                  # build + self-checks
    python3 art-source/splash/build_splash.py --check-rebuild  # ... and a second build to compare
    python3 art-source/splash/build_splash.py --icon-only      # only the app icon, from the committed layers
    python3 art-source/splash/build_splash.py --splash-only    # only the launch splash, likewise
    python3 art-source/splash/build_splash.py --home-only      # only the title screen's art, likewise

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
import home  # noqa: E402
import icon  # noqa: E402
import launch  # noqa: E402
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

# Back to front. (family, [members]); each family is one Inkscape layer in the
# composite, each object one group (and one file in layers/). A member can itself be
# (group id, [object ids]): a group of objects that belong together - a jumping frog and
# the dashed arc of its hop, so hiding the frogs hides the arcs too (Derek's review of
# #512: "there shouldn't be dotted lines for jumps if the frogs aren't there").
FAMILIES = [
    ("sky", ["sky"]),
    ("title-glow", ["title-glow"]),
    ("clouds", ["cloud-left", "cloud-right"]),
    ("banks", ["bank-left", "bank-right"]),
    ("reeds", ["reeds-left", "reeds-right"]),
    ("pond", ["pond"]),
    ("lily-pads", ["pad-left-back", "pad-centre-left", "pad-right-back", "pad-left-front", "pad-right-front"]),
    ("water-lilies", ["lily-left", "lily-right"]),
    ("grass", ["grass"]),
    ("log", ["log"]),
    ("grass-tufts", ["tuft-left", "tuft-right"]),
    ("frogs", [("frog-jump-left", ["arc-left", "frog-left"]),
               ("frog-jump-centre-left", ["arc-centre-left", "frog-centre-left"]),
               ("frog-jump-right", ["arc-right", "frog-right"]),
               "frog-big"]),
    ("title", ["title"]),
]
# Layers that are built and exported but left out of the composite and the review images:
# the splashes on the lily pads (Derek, review of #512: "make it a layer that we don't use
# right now so we don't lose it"). The pads and water under them are complete.
UNUSED = ["splash-left", "splash-centre-left", "splash-right"]
JUMP_LABELS = {"frog-jump-left": "Jumping frog, left, with its hop",
               "frog-jump-centre-left": "Jumping frog, centre left, with its hop",
               "frog-jump-right": "Jumping frog, right, with its hop"}
LABELS = {"sky": "Sky", "title-glow": "Glow behind the title", "title": "Title: MULTIPLYING FROGS"}
FROGS = ["frog-left", "frog-centre-left", "frog-right", "frog-big"]
ARCS = ["arc-left", "arc-centre-left", "arc-right"]
STANDALONE = ["title", "cloud-left", "cloud-right", "reeds-left", "reeds-right", "pad-left-back",
              "pad-centre-left", "pad-right-back", "pad-left-front", "pad-right-front", "lily-left",
              "lily-right", "tuft-left", "tuft-right", "log"] + FROGS
WATER_LINE = 600           # original px: below it is water, pads, log and grass
PADS = ["pad-left-back", "pad-centre-left", "pad-right-back", "pad-left-front", "pad-right-front"]
BEHIND_LOG = ["pond", "grass", "tuft-left", "tuft-right"] + PADS
# (what is taken away, what must then show through with no hole)
NO_HOLES = [
    (["frog-big"], ["log"] + BEHIND_LOG),
    (["log", "frog-big"], BEHIND_LOG),
    (UNUSED + ARCS, ["pond"] + PADS),
]

NS = ('xmlns="http://www.w3.org/2000/svg" '
      'xmlns:inkscape="http://www.inkscape.org/namespaces/inkscape"')


def flat(members):
    for m in members:
        if isinstance(m, tuple):
            yield from m[1]
        else:
            yield m


def object_ids():
    """Every object in the composite, back to front."""
    return [oid for _, members in FAMILIES for oid in flat(members)]


def layer_ids():
    """Every object with a layer file: the composite's, and the unused ones."""
    return object_ids() + UNUSED


def frogs_family():
    return list(flat(dict(FAMILIES)["frogs"]))


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


def stack_svg(ids, title="Multiplying Frogs splash", view=(0, 0, cu.W, cu.H)):
    """The given objects stacked in family order, one Inkscape layer per family."""
    defs, body = [], []
    for fam, members in FAMILIES:
        inner = []
        for m in members:
            gid, oids = m if isinstance(m, tuple) else (None, [m])
            got = []
            for oid in oids:
                if oid in ids:
                    d, b = parts(oid)
                    defs.append(d)
                    got.append(b)
            if got and gid:
                inner.append(f'<g id="{gid}" inkscape:label="{JUMP_LABELS[gid]}">\n' + "".join(got) + "</g>\n")
            else:
                inner += got
        if inner:
            body.append(f'<g id="layer-{fam}" inkscape:groupmode="layer" inkscape:label="{fam}">\n'
                        + "".join(inner) + "</g>\n")
    return svg_doc("".join(defs), "".join(body), view=view, title=title)


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
    for oid in object_ids():          # the unused splashes are left out here too
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

    def without(hide):
        return render(stack_svg([o for o in ids if o not in hide]), REVIEW_W).convert("RGB")

    composite = without([])
    frogs = frogs_family()
    title = ["title", "title-glow"]
    out = {
        "original-vs-composite.png": stack_vertical([
            labelled(original, "Original (splash-source.png)"),
            labelled(composite, "Rebuilt from layers (composite; the splashes are kept as unused layers)")]),
        "no-frogs.png": labelled(without(frogs), "Without the frogs (and their hop arcs)"),
        "no-frogs-no-title.png": labelled(without(frogs + title), "Without the frogs, the title and its glow"),
        "no-frogs-no-log.png": labelled(without(frogs + ["log"]), "Without the frogs and the log"),
        "no-frogs-no-log-no-title.png": labelled(without(frogs + ["log"] + title),
                                                 "Without the frogs, the log, the title and its glow"),
        "layer-sheet.png": layer_sheet(layer_pngs),
    }
    x0, y0, x1, y1 = ti.TITLE_ZONE
    out["title-vs-original.png"] = stack_vertical([
        labelled(original.crop((x0, y0, x1, y1)), "Original title (misspelt MUILTIPLYING)"),
        labelled(composite.crop((x0, y0, x1, y1)), "Rebuilt title (MULTIPLYING)")])
    return out


# --------------------------------------------------------------------------
# The app icon (#483): a square crop of the big frog on the log, no title
# --------------------------------------------------------------------------
def icon_svg():
    x, y, side = icon.ICON_CROP
    return stack_svg(icon.icon_ids(object_ids()), title="Multiplying Frogs app icon",
                     view=(x, y, side, side))


def icon_png():
    """The icon, rendered from the vectors and flattened onto the opaque sky."""
    return render(icon_svg(), icon.ICON_SIZE).convert("RGB")


MASK_TILE = 300               # px: each shape on the icon review sheet
MASK_GAP = 30
MASK_BG = (40, 44, 53)        # a dark launcher background


def icon_masks(img):
    """The icon as three launchers cut it - a circle, a squircle and a rounded square -
    side by side on a dark background, for review."""
    n = MASK_TILE * 4
    yy, xx = np.mgrid[0:n, 0:n]
    u, v = np.abs((xx + 0.5) / n * 2 - 1), np.abs((yy + 0.5) / n * 2 - 1)
    r = 0.4
    shapes = [u * u + v * v <= 1,
              u ** 4 + v ** 4 <= 1,
              np.maximum(u - (1 - r), 0) ** 2 + np.maximum(v - (1 - r), 0) ** 2 <= r * r]
    pic = img.convert("RGB").resize((MASK_TILE, MASK_TILE), Image.LANCZOS)
    sheet = Image.new("RGB", (MASK_GAP + 3 * (MASK_TILE + MASK_GAP), MASK_TILE + 2 * MASK_GAP), MASK_BG)
    for i, m in enumerate(shapes):
        mask = Image.fromarray((m * 255).astype(np.uint8)).resize((MASK_TILE, MASK_TILE), Image.LANCZOS)
        sheet.paste(pic, (MASK_GAP + i * (MASK_TILE + MASK_GAP), MASK_GAP), mask)
    return sheet


def write_icon():
    img = icon_png()
    save_png(img, icon.ICON_OUT)
    save_png(icon_masks(img), REVIEW_DIR / "app-icon-masks.png")


def splash_svg():
    return stack_svg(launch.splash_ids(object_ids()), title="Multiplying Frogs launch splash",
                     view=launch.SPLASH_VIEW)


def splash_png():
    """The launch splash, rendered from the vectors; opaque, as the sky is."""
    img = render(splash_svg(), launch.SPLASH_SIZE[0]).convert("RGB")
    if img.size != launch.SPLASH_SIZE:
        raise SystemExit(f"the launch splash rendered at {img.size}, not {launch.SPLASH_SIZE}")
    return img


def write_splash():
    launch.SPLASH_OUT.parent.mkdir(parents=True, exist_ok=True)
    save_png(splash_png(), launch.SPLASH_OUT)


def home_png(piece):
    """One piece of the title screen's wireframe art (#484), rendered from the vectors.
    The scenes are opaque, as the sky is; the title is transparent around it."""
    svg = stack_svg(home.piece_ids(piece, object_ids()),
                    title=f"Multiplying Frogs title screen - {piece}", view=home.HOME_VIEW)
    img = render(svg, home.HOME_SIZE[0])
    if img.size != home.HOME_SIZE:
        raise SystemExit(f"title screen {piece} rendered at {img.size}, not {home.HOME_SIZE}")
    return img if piece == "title" else img.convert("RGB")


def write_home():
    home.HOME_OUT_DIR.mkdir(parents=True, exist_ok=True)
    for piece in home.PIECES:
        save_png(home_png(piece), home.HOME_OUT_DIR / f"{piece}.png")


def load_committed_parts():
    """Fill the parts cache from the committed layers/<id>.svg, so the icon can be
    rendered without re-tracing anything. layer_svg() wrote each file as
    svg_doc(defs, <g layer>body</g>), so this reads back exactly the same parts."""
    import re
    for oid in object_ids():
        text = (LAYERS_DIR / f"{oid}.svg").read_text(encoding="utf-8")
        m = re.search(r"<defs>\n(.*?)</defs>\n<g id=\"layer-[^\"]+\"[^>]*>\n(.*)</g>\n</svg>\n\Z", text, re.S)
        if not m:
            raise SystemExit(f"layers/{oid}.svg is not in the shape build_splash.py writes")
        _PARTS[oid] = (m.group(1), m.group(2))


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
    for oid in layer_ids():
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
    write_icon()
    write_splash()
    write_home()
    return layer_pngs


def outputs():
    files = [COMPOSITE.with_suffix(".svg"), COMPOSITE.with_suffix(".png"), icon.ICON_OUT,
             launch.SPLASH_OUT] + [home.HOME_OUT_DIR / f"{p}.png" for p in home.PIECES]
    for d in (LAYERS_DIR, OBJECTS_DIR, REVIEW_DIR):
        files += sorted(d.iterdir())
    return files


def digests():
    return {str(p.relative_to(HERE.parents[1])): hashlib.sha256(p.read_bytes()).hexdigest() for p in outputs()}


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
    # no hole: whatever is taken away, below the water line the layers behind it let
    # no sky through (stacked alpha is 1 - product of (1 - alpha))
    s = EXPORT_W / cu.W
    for gone, behind in NO_HOLES:
        foot = np.zeros(np.asarray(layer_pngs["sky"]).shape[:2], bool)
        for g in gone:
            foot |= np.asarray(layer_pngs[g])[..., 3] > 0
        foot[: int(WATER_LINE * s)] = False
        clear = np.ones(foot.shape)
        for b in behind:
            clear *= 1 - np.asarray(layer_pngs[b])[..., 3] / 255.0
        n = int((foot & (clear > 1 / 255.0)).sum())
        if n:
            problems.append(f"without {gone}: {n} px are a hole")
    for oid in UNUSED:
        if f'id="{oid}"' in comp:
            problems.append(f"composite: unused layer {oid} is in it")
    for gid in JUMP_LABELS:
        if f'<g id="{gid}" ' not in comp:
            problems.append(f"composite: no group {gid}")
    with Image.open(icon.ICON_OUT) as img:
        if img.size != (icon.ICON_SIZE, icon.ICON_SIZE) or icon.ICON_SIZE < 1024:
            problems.append(f"app icon source is {img.size}")
    with Image.open(launch.SPLASH_OUT) as img:
        if img.size != launch.SPLASH_SIZE:
            problems.append(f"launch splash is {img.size}")
    if any(f'id="{oid}"' in icon_svg() for oid in icon.ICON_HIDDEN):
        problems.append("app icon: a hidden layer is in it")
    for oid in STANDALONE:
        img = Image.open(OBJECTS_DIR / f"{oid}.png")
        if max(img.size) < OBJECT_LONG - 1:
            problems.append(f"objects/{oid}.png is only {img.size}")
    return problems


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check-rebuild", action="store_true",
                    help="build a second time from scratch and require byte-identical files")
    ap.add_argument("--icon-only", action="store_true",
                    help="only render the app icon, from the committed layer SVGs (seconds, "
                         "needs only CairoSVG, Pillow and NumPy)")
    ap.add_argument("--splash-only", action="store_true",
                    help="only render the launch splash, from the committed layer SVGs")
    ap.add_argument("--home-only", action="store_true",
                    help="only render the title screen's wireframe art, from the committed layer SVGs")
    args = ap.parse_args()
    if args.home_only:
        load_committed_parts()
        write_home()
        print(f"wrote {home.HOME_OUT_DIR.relative_to(HERE.parents[1])}/")
        return 0
    if args.splash_only:
        load_committed_parts()
        write_splash()
        print(f"wrote {launch.SPLASH_OUT.relative_to(HERE.parents[1])}")
        return 0
    if args.icon_only:
        load_committed_parts()
        write_icon()
        print(f"wrote {icon.ICON_OUT.relative_to(HERE.parents[1])}")
        return 0
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
