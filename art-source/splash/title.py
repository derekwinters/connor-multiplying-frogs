"""The splash title, rebuilt as vector lettering: MULTIPLYING over FROGS.

The title is not cut out of the raster: a cut-out would be capped at the source's
1586 px width and keep a fringe of sky. Instead every letter is a glyph from an
open-licensed rounded display font (Fredoka, SIL OFL 1.1 - `fonts/OFL.txt`), placed in
the original's pixel space so the rebuild sits exactly where the drawn title sits:

- **FROGS** - each glyph is fitted to its letter in the original (position, tilt and
  size, by best overlap with the drawn letter's green fill), so the bounce of the
  drawn letters is kept.
- **MULTIPLYING** - the drawn word is misspelt "MUILTIPLYING" (an extra I). Derek
  decided on 2026-11-24 that the rebuild spells it MULTIPLYING, so its letters cannot
  be fitted one to one. Each drawn letter is fitted the same way, a smooth arch (height,
  tilt and letter size against x) is fitted through them, and the eleven letters of
  MULTIPLYING are laid along that arch, evenly spaced (as the drawn letters are) and spanning
  the same width as the drawn word.

Each letter is drawn as a dark outline (a round-joined stroke around the glyph), the
fill, and for FROGS a darker green rim just inside the outline - the same three bands
the drawn title has. All outlines are drawn before any fill, so where letters touch
their outlines merge into one shape, as they do in the drawing.

`build_splash.py` writes `layers/title.svg` from `title_group()`.
"""
from __future__ import annotations

from functools import lru_cache

import numpy as np

import cutout as cu
from tracing import fmt

FONT = cu.HERE / "fonts" / "Fredoka-Variable.ttf"
FONT_WEIGHT = 700          # Fredoka wght axis: Bold
FONT_WIDTH = 100           # Fredoka wdth axis: normal

TOP_WORD = "MULTIPLYING"   # the spelling the rebuild uses (Derek, 2026-11-24)
DRAWN_TOP = "MUILTIPLYING"  # the spelling as drawn, used only to measure the arch
BOTTOM_WORD = "FROGS"

# Where the drawn letters are (boxes around each drawn letter, original px). Two letters
# that touch in the drawing (P and L) are told apart by the column between them.
TITLE_ZONE = (290, 30, 1290, 432)
PL_SPLIT = 858
TOP_ARCH_DEGREE = 2        # height and tilt of MULTIPLYING against x: a parabola
TOP_SIZE_DEGREE = 1        # letter size against x: a straight line

OUTLINE = "#060b04"        # the dark outline (sampled from the drawing)
TOP_FILL = "#018401"       # MULTIPLYING's dark green
FROGS_FILL = "#8ec701"     # FROGS's light green
FROGS_RIM = "#68ac02"      # FROGS's darker green just inside the outline
TOP_OUTLINE_W = 7.0        # outline width outside the glyph, px of the original
FROGS_OUTLINE_W = 7.5
FROGS_RIM_W = 9.0          # rim width inside the glyph


# --------------------------------------------------------------------------
# Font
# --------------------------------------------------------------------------
@lru_cache(maxsize=1)
def font():
    from fontTools.ttLib import TTFont
    from fontTools.varLib import instancer
    f = TTFont(str(FONT))
    return instancer.instantiateVariableFont(f, {"wght": FONT_WEIGHT, "wdth": FONT_WIDTH}, inplace=False)


def glyph_name(ch):
    return font().getBestCmap()[ord(ch)]


@lru_cache(maxsize=None)
def glyph_outline(ch):
    """The glyph as one clean outline: a variable font's glyphs are built from
    overlapping contours, which would show as lines inside a stroked letter."""
    import pathops
    from fontTools.pens.recordingPen import DecomposingRecordingPen
    gs = font().getGlyphSet()
    rec = DecomposingRecordingPen(gs)
    gs[glyph_name(ch)].draw(rec)
    path = pathops.Path()
    rec.replay(path.getPen())
    path.simplify(fix_winding=True, keep_starting_points=True)
    return path


@lru_cache(maxsize=None)
def glyph_contours(ch):
    """The glyph's contours as point arrays (font units, y up), curves flattened."""
    from fontTools.pens.recordingPen import RecordingPen
    pen = RecordingPen()
    glyph_outline(ch).draw(pen)
    contours, cur, last = [], [], None
    for op, args in pen.value:
        if op == "moveTo":
            cur = [args[0]]
            last = args[0]
        elif op == "lineTo":
            cur.append(args[0])
            last = args[0]
        elif op in ("qCurveTo", "curveTo"):
            pts = [last] + list(args)
            if op == "qCurveTo":
                # TrueType: implied on-curve points between consecutive off-curve points
                offs, end = pts[1:-1], pts[-1]
                segs, p0 = [], pts[0]
                for i, c in enumerate(offs):
                    p1 = end if i == len(offs) - 1 else ((c[0] + offs[i + 1][0]) / 2, (c[1] + offs[i + 1][1]) / 2)
                    segs.append((p0, c, p1))
                    p0 = p1
                for a, c, b in segs:
                    for t in np.linspace(0, 1, 9)[1:]:
                        cur.append(((1 - t) ** 2 * a[0] + 2 * (1 - t) * t * c[0] + t * t * b[0],
                                    (1 - t) ** 2 * a[1] + 2 * (1 - t) * t * c[1] + t * t * b[1]))
            else:
                a, c1, c2, b = pts
                for t in np.linspace(0, 1, 9)[1:]:
                    u = 1 - t
                    cur.append((u ** 3 * a[0] + 3 * u * u * t * c1[0] + 3 * u * t * t * c2[0] + t ** 3 * b[0],
                                u ** 3 * a[1] + 3 * u * u * t * c1[1] + 3 * u * t * t * c2[1] + t ** 3 * b[1]))
            last = args[-1]
        elif op in ("closePath", "endPath"):
            if cur:
                contours.append(np.array(cur, float))
            cur = []
    return contours


@lru_cache(maxsize=None)
def glyph_box(ch):
    pts = np.concatenate(glyph_contours(ch))
    return pts[:, 0].min(), pts[:, 1].min(), pts[:, 0].max(), pts[:, 1].max()


def affine(ch, cx, cy, ang, sx, sy):
    """(a, b, c, d, e, f): font units -> original px. The glyph's box centre lands on
    (cx, cy); it is scaled by (sx, sy) px per unit, y flipped, then turned by ang degrees."""
    x0, y0, x1, y1 = glyph_box(ch)
    gx, gy = (x0 + x1) / 2, (y0 + y1) / 2
    t = np.radians(ang)
    co, si = np.cos(t), np.sin(t)
    a, b = co * sx, si * sx          # x' = a*x + c*y + e ; y' = b*x + d*y + f
    c, d = si * sy, -co * sy
    e = cx - (a * gx + c * gy)
    f = cy - (b * gx + d * gy)
    return a, b, c, d, e, f


def raster(ch, params, box):
    """The placed glyph as a bool mask over box (x0, y0, x1, y1) of the original."""
    import cv2
    a, b, c, d, e, f = affine(ch, *params)
    x0, y0, x1, y1 = box
    polys = []
    for p in glyph_contours(ch):
        q = np.stack([a * p[:, 0] + c * p[:, 1] + e - x0, b * p[:, 0] + d * p[:, 1] + f - y0], 1)
        polys.append(np.round(q * 4).astype(np.int32))
    m = np.zeros((y1 - y0, x1 - x0), np.uint8)
    # even-odd over all contours: counters come out as holes
    acc = np.zeros_like(m)
    for p in polys:
        one = np.zeros_like(m)
        cv2.fillPoly(one, [p], 1, lineType=cv2.LINE_8, shift=2)
        acc ^= one
    return acc.astype(bool)


def glyph_path(ch, params):
    """SVG path data of the placed glyph."""
    from fontTools.pens.svgPathPen import SVGPathPen
    from fontTools.pens.transformPen import TransformPen
    pen = SVGPathPen(None, ntos=fmt)
    glyph_outline(ch).draw(TransformPen(pen, affine(ch, *params)))
    return pen.getCommands()


# --------------------------------------------------------------------------
# Fitting to the drawing
# --------------------------------------------------------------------------
@lru_cache(maxsize=1)
def drawn_letters():
    """{'top': [fill mask per drawn letter, left to right], 'frogs': [...]}: the green fill
    of each drawn letter (full-frame bool)."""
    from scipy import ndimage as ndi
    lab = cu.original_lab()
    green = (lab[..., 1] < -12) & (lab[..., 2] > 10)
    x0, y0, x1, y1 = TITLE_ZONE
    zone = cu.box_mask(TITLE_ZONE)
    body = ndi.binary_opening(zone & (green | cu.ink()), np.ones((3, 3)))
    lbl, n = ndi.label(body)
    sizes = ndi.sum(body, lbl, range(1, n + 1))
    parts = []
    for j in range(1, n + 1):
        if sizes[j - 1] < 3000:
            continue        # the action strokes, bits of frog at the zone's edge
        ys, xs = np.nonzero(lbl == j)
        parts.append((xs.min(), ys.min(), j))
    top = sorted([p for p in parts if p[1] < 120])
    bottom = sorted([p for p in parts if p[1] >= 120])
    fill = green & ~cu.dilate(cu.ink(), 1)
    tops = []
    for _, _, j in top:
        m = (lbl == j) & fill
        xs = np.nonzero(m.any(0))[0]
        if xs.min() < PL_SPLIT < xs.max():
            cols = np.arange(cu.W)[None, :]
            tops += [m & (cols < PL_SPLIT), m & (cols >= PL_SPLIT)]
        else:
            tops.append(m)
    frogs = [(lbl == j) & fill for _, _, j in bottom]
    assert len(tops) == len(DRAWN_TOP), f"found {len(tops)} drawn top letters"
    assert len(frogs) == len(BOTTOM_WORD), f"found {len(frogs)} drawn FROGS letters"
    return {"top": tops, "frogs": frogs}


def iou(ch, params, target, box):
    g = raster(ch, params, box)
    t = target[box[1]:box[3], box[0]:box[2]]
    return (g & t).sum() / max(1, (g | t).sum())


@lru_cache(maxsize=None)
def fit(word, i):
    """(cx, cy, angle, sx, sy) of the glyph that best covers drawn letter i of word."""
    ch = (DRAWN_TOP if word == "top" else BOTTOM_WORD)[i]
    target = drawn_letters()[word][i]
    ys, xs = np.nonzero(target)
    pad = 40
    box = (max(0, xs.min() - pad), max(0, ys.min() - pad), min(cu.W, xs.max() + pad), min(cu.H, ys.max() + pad))
    gx0, gy0, gx1, gy1 = glyph_box(ch)
    s = float((ys.max() - ys.min()) / (gy1 - gy0))
    best = None
    for ang in range(-30, 31, 3):
        p = (float(xs.mean()), float(ys.mean()), float(ang), s, s)
        v = iou(ch, p, target, box)
        if best is None or v > best[0]:
            best = (v, p)
    v, p = best
    steps = [3.0, 3.0, 2.0, s * 0.06, s * 0.06]
    for _ in range(7):
        improved = True
        while improved:
            improved = False
            for k in range(5):
                for sgn in (1, -1):
                    q = list(p)
                    q[k] += sgn * steps[k]
                    q = tuple(q)
                    w = iou(ch, q, target, box)
                    if w > v + 1e-9:
                        v, p, improved = w, q, True
        steps = [st / 2 for st in steps]
    return p, v


@lru_cache(maxsize=1)
def top_layout():
    """Placements of MULTIPLYING's letters along the drawn word's arch."""
    fits = [fit("top", i)[0] for i in range(len(DRAWN_TOP))]
    cx = np.array([f[0] for f in fits])
    cy = np.array([f[1] for f in fits])
    ang = np.array([f[2] for f in fits])
    sy = np.array([f[4] for f in fits])
    sx = np.array([f[3] for f in fits])
    arch = np.polyfit(cx, cy, TOP_ARCH_DEGREE)
    tilt = np.polyfit(cx, ang, TOP_ARCH_DEGREE)
    size_y = np.polyfit(cx, sy, TOP_SIZE_DEGREE)
    widen = float(np.median(sx / sy))
    # the drawn word's horizontal extent, measured on the fitted glyph boxes
    left = cx[0] - (glyph_box(DRAWN_TOP[0])[2] - glyph_box(DRAWN_TOP[0])[0]) / 2 * sx[0]
    right = cx[-1] + (glyph_box(DRAWN_TOP[-1])[2] - glyph_box(DRAWN_TOP[-1])[0]) / 2 * sx[-1]
    # lay the new word out with even gaps between the letters (as the drawn letters
    # have), spanning the drawn word's width
    s0 = float(np.polyval(size_y, (left + right) / 2)) * widen
    widths = np.array([(glyph_box(c)[2] - glyph_box(c)[0]) * s0 for c in TOP_WORD])
    gap = ((right - left) - widths.sum()) / (len(TOP_WORD) - 1)
    xs = left + np.cumsum(widths) - widths / 2 + gap * np.arange(len(TOP_WORD))
    out = []
    for ch, xc in zip(TOP_WORD, xs):
        syv = float(np.polyval(size_y, xc))
        out.append((ch, (float(xc), float(np.polyval(arch, xc)), float(np.polyval(tilt, xc)), syv * widen, syv)))
    return out


@lru_cache(maxsize=1)
def frogs_layout():
    return [(ch, fit("frogs", i)[0]) for i, ch in enumerate(BOTTOM_WORD)]


# --------------------------------------------------------------------------
# SVG
# --------------------------------------------------------------------------
def title_group(stroke_groups=""):
    """(defs, body) of the title: one group per word, each with an outline, fill (and
    rim) group, one path per letter with an id. stroke_groups: the traced action strokes."""
    defs, out_top, out_frogs, fill_top, fill_frogs, rim = [], [], [], [], [], []
    for i, (ch, p) in enumerate(top_layout()):
        d = glyph_path(ch, p)
        lid = f"title-multiplying-{i + 1:02d}-{ch}"
        out_top.append(f'<path id="{lid}-outline" d="{d}"/>')
        fill_top.append(f'<path id="{lid}" d="{d}"/>')
    for i, (ch, p) in enumerate(frogs_layout()):
        d = glyph_path(ch, p)
        lid = f"title-frogs-{i + 1}-{ch}"
        out_frogs.append(f'<path id="{lid}-outline" d="{d}"/>')
        fill_frogs.append(f'<path id="{lid}" d="{d}"/>')
        defs.append(f'<clipPath id="{lid}-clip"><path d="{d}"/></clipPath>')
        rim.append(f'<path d="{d}" clip-path="url(#{lid}-clip)"/>')
    join = 'stroke-linejoin="round" stroke-linecap="round"'
    body = (
        '<g id="title-multiplying" inkscape:label="MULTIPLYING">\n'
        f'<g id="title-multiplying-outline" inkscape:label="outline" fill="{OUTLINE}" stroke="{OUTLINE}" '
        f'stroke-width="{fmt(2 * TOP_OUTLINE_W)}" {join}>\n' + "\n".join(out_top) + '\n</g>\n'
        f'<g id="title-multiplying-fill" inkscape:label="fill" fill="{TOP_FILL}">\n' + "\n".join(fill_top) + '\n</g>\n'
        '</g>\n'
        '<g id="title-frogs" inkscape:label="FROGS">\n'
        f'<g id="title-frogs-outline" inkscape:label="outline" fill="{OUTLINE}" stroke="{OUTLINE}" '
        f'stroke-width="{fmt(2 * FROGS_OUTLINE_W)}" {join}>\n' + "\n".join(out_frogs) + '\n</g>\n'
        f'<g id="title-frogs-fill" inkscape:label="fill" fill="{FROGS_FILL}">\n' + "\n".join(fill_frogs) + '\n</g>\n'
        f'<g id="title-frogs-rim" inkscape:label="rim" fill="none" stroke="{FROGS_RIM}" '
        f'stroke-width="{fmt(2 * FROGS_RIM_W)}" {join}>\n' + "\n".join(rim) + '\n</g>\n'
        '</g>\n' + stroke_groups)
    return "\n".join(defs) + "\n", body


def spelled():
    """The title's words as built, top then bottom."""
    return "".join(c for c, _ in top_layout()), "".join(c for c, _ in frogs_layout())


def clear_caches():
    for f in (font, glyph_outline, glyph_contours, glyph_box, drawn_letters, top_layout, frogs_layout):
        f.cache_clear()
    fit.cache_clear()
