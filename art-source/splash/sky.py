"""The sky and the glow behind the title: fitted to the original, not traced.

- **Sky** - a vertical gradient. Each row's colour is the median of the sky the original
  shows in that row away from the title (left of the title and right of it), so it is
  the picture's own blue, deep at the top and pale toward the water. Being a plain
  gradient over the whole canvas, it is complete behind everything: behind the title,
  the clouds, the frogs.
- **Title glow** - the whitish haze behind the title. How far each sky pixel is pushed
  from its row's blue toward white is measured, smoothed, and drawn as a stack of
  nested white shapes (`GLOW_LEVELS` of them, traced at evenly spaced strengths), so
  the haze keeps the original's outline - hugging the lettering - while staying vector.
  Under the letters, where the original shows no sky, the haze is taken as full.
"""
from __future__ import annotations

from functools import lru_cache

import numpy as np

import cutout as cu
import title as ti
from tracing import fmt, hexc, traced

SKY_SIDES = (260, 1320)    # sky measured left of the first x and right of the second
SKY_STEP = 16              # rows per gradient stop
SKY_BOTTOM = 590           # below this the original shows no sky; the last colour carries on
GLOW_WHITE = (248, 250, 246)   # the haze's colour (sampled between the letters)
GLOW_LEVELS = 8            # nested shapes the haze is drawn with
GLOW_SIGMA = 9.0           # px of smoothing of the measured haze
GLOW_MIN = 0.30            # haze weaker than this is crayon texture, not glow
GLOW_SCALE = 2             # trace the haze shapes at this upscale


@lru_cache(maxsize=1)
def sky_rows():
    """(H x 3) the sky's colour in every row."""
    vis = cu.visible()
    sky = vis["sky"] & ~cu.dilate(cu.ink(), 3) & ~title_core()
    xx = np.arange(cu.W)[None, :]
    side = (xx < SKY_SIDES[0]) | (xx > SKY_SIDES[1])
    o = cu.original().astype(np.float64)
    rows = np.full((cu.H, 3), np.nan)
    for y in range(SKY_BOTTOM):
        m = sky[y] & side[0]
        if m.sum() >= 8:
            rows[y] = np.median(o[y][m], axis=0)
    ok = ~np.isnan(rows[:, 0])
    yy = np.arange(cu.H)
    from scipy import ndimage as ndi
    return np.stack([ndi.gaussian_filter1d(np.interp(yy, yy[ok], rows[ok, c]), 4.0) for c in range(3)], -1)


def sky_svg():
    rows = sky_rows()
    stops = "".join(f'<stop offset="{fmt(y / (cu.H - 1))}" stop-color="{hexc(rows[y])}"/>'
                    for y in list(range(0, SKY_BOTTOM, SKY_STEP)) + [SKY_BOTTOM, cu.H - 1])
    defs = (f'<linearGradient id="sky-gradient" x1="0" y1="0" x2="0" y2="{cu.H - 1}" '
            f'gradientUnits="userSpaceOnUse">{stops}</linearGradient>\n')
    # a little past the canvas, so the edges stay opaque at any export size
    body = (f'<rect id="sky-fill" x="-4" y="-4" width="{cu.W + 8}" height="{cu.H + 8}" '
            f'fill="url(#sky-gradient)"/>\n')
    return defs, body


@lru_cache(maxsize=1)
def title_core():
    """The drawn title (letters and outline), full-frame bool."""
    m = np.zeros((cu.H, cu.W), bool)
    for w in ("top", "frogs"):
        for f in ti.drawn_letters()[w]:
            m |= f
    zone = cu.box_mask(ti.TITLE_ZONE)
    from scipy import ndimage as ndi
    m = ndi.binary_fill_holes(cu.dilate(m, 9)) & zone
    return m


@lru_cache(maxsize=1)
def glow_strength():
    """(H x W) 0..1: how far the original pushes the sky toward white."""
    import cv2
    vis = cu.visible()
    o = cu.original().astype(np.float32)
    rows = sky_rows().astype(np.float32)
    white = np.array(GLOW_WHITE, np.float32)
    # red is the channel the haze moves most (blue barely changes from sky to white)
    a = (o[..., 0] - rows[:, None, 0]) / np.maximum(white[0] - rows[:, None, 0], 1.0)
    a = np.clip(a, 0, 1)
    known = vis["sky"] & ~title_core() & ~cu.dilate(cu.ink(), 2)
    known[SKY_BOTTOM:] = False
    a[~known] = 0
    core = title_core()
    a[core] = 1.0
    known |= core
    filled = cu.push_pull(np.repeat(a[..., None], 3, -1), known)[..., 0]
    filled = cv2.GaussianBlur(filled.astype(np.float32), (0, 0), GLOW_SIGMA)
    filled[SKY_BOTTOM + 20:] = 0
    return np.clip(filled, 0, 1)


@lru_cache(maxsize=1)
def glow_svg():
    """The haze as nested shapes; stacked, shape k of n is alpha k/n."""
    from scipy import ndimage as ndi
    import cv2
    a = glow_strength()
    core = title_core()
    paths = []
    n = GLOW_LEVELS
    for k in range(1, n + 1):
        t = GLOW_MIN + (1 - GLOW_MIN) * (k - 1) / n
        m = a >= t
        lbl, _ = ndi.label(m)
        keep = np.unique(lbl[core & m])
        m = np.isin(lbl, keep[keep > 0])          # only haze joined to the title
        m = ndi.binary_fill_holes(m)
        if not m.any():
            continue
        up = cv2.resize(m.astype(np.uint8), (cu.W * GLOW_SCALE, cu.H * GLOW_SCALE),
                        interpolation=cv2.INTER_LINEAR).astype(bool)
        d = traced(up, GLOW_SCALE, 0, 0)
        op = 1.0 / (n - k + 1)
        paths.append(f'<path id="title-glow-{k}" fill-opacity="{fmt(op)}" d="{d}"/>')
    body = (f'<g id="title-glow-levels" fill="{hexc(GLOW_WHITE)}">\n' + "\n".join(paths) + "\n</g>\n")
    return "", body


def clear_caches():
    for f in (sky_rows, title_core, glow_strength, glow_svg):
        f.cache_clear()
