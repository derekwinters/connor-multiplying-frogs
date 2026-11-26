"""Stage 2 of the splash rebuild: trace each cut-out object to vector paths with VTracer.

Per object (`trace_object`): crop with the hidden part already filled (cutout.plate),
upscale, flatten the crayon texture with mean-shift, quantize to a small palette
(deterministic k-means in Lab), fold anti-aliasing colours into their neighbours, smooth
the label map, then trace every colour as a *stacked* shape - each shape is the union of
its colour and every colour above it - so neighbouring colours overlap instead of leaving
hairline seams. Paths are written in the original's pixel space (1586 x 992).

The pond and the front grass are "ground": a vertical gradient fitted row by row to the
original's water / grass is the base, and only what stands out from it (ripple lines,
shore tufts) is traced on top, where the original shows it. Hidden ground is plain
gradient.

Adapted from derekwinters/lucas-doggiehood art-source/splash (trace_env.py,
trace_dogs.py).
"""
from __future__ import annotations

import io
import re
from functools import lru_cache

import numpy as np
from PIL import Image

import cutout as cu

TRACE_LONG = 1600          # an object is traced at about this many px on its long side
TRACE_SCALE = (1, 8)       # ... with the upscale clamped to this range
MEANSHIFT = (4, 10)        # spatial / colour radius of the flattening filter (trace px)
LABEL_SIGMA = 0.6          # blur of the one-hot label map before argmax (original px)
SPECKLE = 1.2              # VTracer filter_speckle: drop patches under SPECKLE^2 original px
EDGE_GRAD = 24.0           # Sobel magnitude of L (trace px) counted as an edge
TRANSITION_FRAC = 0.6      # a colour this often on edges is an anti-aliasing artefact
CLUSTER_DE = 20.0          # palette colours closer than this (Lab) share a colour group
GROUND_DETAIL_DE = 9.0     # ground pixels this far (Lab) from the gradient are traced as detail
GROUND_DETAIL_K = 8        # palette size of the ground's detail
GROUND_MIN = 3             # ground detail smaller than this (original px, squared) is dropped
GROUND_STOP = 8            # original rows per gradient stop of the ground
GROUND_CORE_DE = 12.0      # per row, ground pixels this close to its main colour set the gradient


def fmt(v: float) -> str:
    s = f"{v:.2f}".rstrip("0").rstrip(".")
    return "0" if s in ("-0", "") else s


def hexc(rgb):
    return "#%02x%02x%02x" % tuple(int(v) for v in np.clip(np.round(rgb), 0, 255))


def kmeans_lab(px, k, min_dist=9.0, iters=16):
    """Deterministic k-means: seeds are the most frequent colours, kept min_dist apart."""
    q = np.clip(px / np.array([100.0, 256.0, 256.0]) * 24 + np.array([0, 12, 12]), 0, 23).astype(int)
    key = (q[:, 0] * 24 + q[:, 1]) * 24 + q[:, 2]
    vals, inv, cnt = np.unique(key, return_inverse=True, return_counts=True)
    sums = np.zeros((len(vals), 3))
    np.add.at(sums, inv.ravel(), px)
    means = sums / cnt[:, None]
    cents = []
    for i in np.argsort(-cnt, kind="stable"):
        if all(np.linalg.norm(means[i] - c) > min_dist for c in cents):
            cents.append(means[i])
        if len(cents) == k:
            break
    c = np.array(cents)
    sub = px[:: max(1, len(px) // 200000)]
    for _ in range(iters):
        lab_ = ((sub[:, None, :] - c[None]) ** 2).sum(-1).argmin(1)
        c = np.array([sub[lab_ == j].mean(0) if (lab_ == j).any() else c[j] for j in range(len(c))])
    return c


def smooth_labels(labels, n, inside, sigma):
    import cv2
    best = np.full(labels.shape, -1, np.int32)
    score = np.full(labels.shape, -1.0, np.float32)
    for j in range(n):
        s = cv2.GaussianBlur((labels == j).astype(np.float32), (0, 0), sigma)
        upd = s > score
        best[upd] = j
        score[upd] = s[upd]
    best[~inside] = -1
    return best


def vtrace(binary, scale):
    """Trace a bool image (True = shape) to SVG path data in its own pixel space."""
    import vtracer
    buf = io.BytesIO()
    Image.fromarray(np.where(binary, 0, 255).astype(np.uint8)).save(buf, "PNG")
    svg = vtracer.convert_raw_image_to_svg(
        buf.getvalue(), img_format="png", colormode="binary", mode="spline",
        filter_speckle=max(1, round(SPECKLE * scale)), corner_threshold=60, length_threshold=4.0,
        splice_threshold=45, path_precision=3)
    out = []
    for d, tx, ty in re.findall(r'<path d="([^"]+)" fill="[^"]+" transform="translate\(([-\d.]+),([-\d.]+)\)"', svg):
        assert set(re.findall(r"[A-Za-z]", d)) <= {"M", "C", "Z"}, "unexpected path command"
        out.append((d, float(tx), float(ty)))
    return out


def to_original(d, tx, ty, x0, y0, s):
    """Rewrite traced path data from trace-crop px into the original's pixel space."""
    toks = re.findall(r"[MCZ]|-?\d+(?:\.\d+)?", d)
    out, i = [], 0
    while i < len(toks):
        t = toks[i]
        if t in "MCZ":
            out.append(t)
            i += 1
            continue
        x, y = float(toks[i]), float(toks[i + 1])
        out.append(f"{fmt((x + tx) / s + x0)} {fmt((y + ty) / s + y0)}")
        i += 2
    return " ".join(out)


def traced(shape, s, x0, y0):
    return " ".join(to_original(d, tx, ty, x0, y0, s) for d, tx, ty in vtrace(shape, s))


def colour_name(rgb):
    import colorsys
    r, g, b = (float(c) / 255 for c in rgb)
    h, l, s = colorsys.rgb_to_hls(r, g, b)
    if l < 0.16:
        return "outline"
    if s < 0.18 or max(r, g, b) - min(r, g, b) < 0.08:
        return "white" if l > 0.85 else ("grey" if l > 0.35 else "outline")
    hue = h * 360
    base = "red"
    for lim, name in ((15, "red"), (40, "orange"), (65, "yellow"), (160, "green"), (195, "teal"),
                      (250, "blue"), (290, "purple"), (340, "pink"), (361, "red")):
        if hue < lim:
            base = name
            break
    if base in ("orange", "red", "yellow") and l < 0.4:
        base = "brown"
    return base


def ground_gradient(img, vrc):
    """Per row: the ground's main colour where the original shows it, smoothed."""
    from scipy import ndimage as ndi
    ilab = cu.to_lab(np.clip(img, 0, 255).astype(np.uint8))
    rows = np.full((img.shape[0], 3), np.nan)
    for y in range(img.shape[0]):
        sel = vrc[y]
        if sel.sum() < 8:
            continue
        for _ in range(2):
            m = np.median(ilab[y][sel], axis=0)
            keep = sel & (np.linalg.norm(ilab[y] - m, axis=-1) < GROUND_CORE_DE)
            if keep.sum() >= 3:
                sel = keep
        rows[y] = np.median(img[y][sel], axis=0)
    ok = ~np.isnan(rows[:, 0])
    yy = np.arange(img.shape[0])
    return np.stack([ndi.gaussian_filter1d(np.interp(yy, yy[ok], rows[ok, c]), 2.0) for c in range(3)], -1)


@lru_cache(maxsize=None)
def trace_object(oid):
    """(crop box, [(group name, [(fill, path d)])], <defs>) of one object, back to front."""
    import cv2
    from scipy import ndimage as ndi
    box, img, rc, vc = cu.plate(oid)
    x0, y0, x1, y1 = box
    long_side = max(x1 - x0, y1 - y0)
    s = int(min(max(round(TRACE_LONG / long_side), TRACE_SCALE[0]), TRACE_SCALE[1]))
    size = ((x1 - x0) * s, (y1 - y0) * s)
    up = np.asarray(Image.fromarray(np.clip(img, 0, 255).round().astype(np.uint8)).resize(size, Image.LANCZOS))
    mk = cv2.resize(rc.astype(np.float32), size, interpolation=cv2.INTER_LINEAR)
    inside = cv2.GaussianBlur(mk, (0, 0), 0.5 * s) > 0.5
    flat = cv2.pyrMeanShiftFiltering(np.ascontiguousarray(up[..., ::-1]), *MEANSHIFT)[..., ::-1]
    lab = cu.to_lab(flat)
    area = float(rc.sum())
    k = int(min(14, max(4, round(3 + np.sqrt(area) / 14))))
    k = cu.OBJ[oid].get("colours", k)
    quant, defs, base = inside, "", None
    ground = cu.OBJ[oid]["cut"] == "ground"
    if ground:
        rows = ground_gradient(img, vc & rc)
        model = np.stack([np.interp((np.arange(size[1]) + 0.5) / s - 0.5, np.arange(img.shape[0]), rows[:, c])
                          for c in range(3)], -1)
        mlab = cu.to_lab(np.clip(model, 0, 255).astype(np.uint8)[None])[0]
        de = np.linalg.norm(lab - mlab[:, None, :], axis=-1)
        vis_up = cv2.resize(vc.astype(np.uint8), size, interpolation=cv2.INTER_NEAREST).astype(bool)
        quant = inside & vis_up & (de > GROUND_DETAIL_DE)
        quant = ndi.binary_opening(quant, cu.disk(max(1, s // 2)).astype(bool))
        lbl, nl = ndi.label(quant)
        if nl:
            sz = ndi.sum(quant, lbl, range(1, nl + 1))
            quant = np.isin(lbl, [j + 1 for j, v in enumerate(sz) if v >= (GROUND_MIN * s) ** 2])
        gid = f"{oid}-gradient"
        stops = "".join(f'<stop offset="{fmt(y / max(1, img.shape[0] - 1))}" stop-color="{hexc(rows[y])}"/>'
                        for y in list(range(0, img.shape[0], GROUND_STOP)) + [img.shape[0] - 1])
        defs = (f'<linearGradient id="{gid}" x1="0" y1="{y0}" x2="0" y2="{y0 + img.shape[0] - 1}" '
                f'gradientUnits="userSpaceOnUse">{stops}</linearGradient>\n')
        base = f"url(#{gid})"
        k = GROUND_DETAIL_K
    if not quant.any():
        quant = inside if base is None else quant
    cents = kmeans_lab(lab[quant], k, min_dist=6.0) if quant.any() else np.zeros((1, 3))
    dist = ((lab[..., None, :] - cents[None, None]) ** 2).sum(-1)
    labels = dist.argmin(-1)
    g = cv2.GaussianBlur(lab[..., 0], (0, 0), 1.5)
    grad = np.hypot(cv2.Sobel(g, cv2.CV_32F, 1, 0), cv2.Sobel(g, cv2.CV_32F, 0, 1)) / s
    edgy = grad > EDGE_GRAD / 4
    trans = [j for j in range(len(cents))
             if ((labels == j) & quant).sum() and edgy[(labels == j) & quant].mean() > TRANSITION_FRAC]
    if base is None and trans and len(trans) < len(cents) and not cu.OBJ[oid].get("keep_small"):
        dist[..., trans] = np.inf
        labels = dist.argmin(-1)
    labels = smooth_labels(labels, len(cents), quant, LABEL_SIGMA * s)
    upf = up.astype(np.float64)
    regs = []
    for j in range(len(cents)):
        mm = labels == j
        if mm.sum() > (SPECKLE * s) ** 2:
            regs.append((upf[mm].mean(0), mm))
    regs.sort(key=lambda r: -int(r[1].sum()))
    groups = []
    for col, mm in regs:
        lc = cu.to_lab(np.array([[col]], np.uint8))[0, 0]
        for gr in groups:
            if np.linalg.norm(gr["lab"] - lc) < CLUSTER_DE:
                gr["items"].append((col, mm))
                break
        else:
            groups.append(dict(lab=lc, items=[(col, mm)], col=col))
    groups.sort(key=lambda gr: -sum(int(m.sum()) for _, m in gr["items"]))
    out, below, first, used = [], quant.copy(), True, {}
    if base is not None:
        out.append(("ground", [(base, traced(inside, s, x0, y0))]))
        used["ground"] = 1
        first = False
    for gr in groups:
        paths = []
        for col, mm in gr["items"]:
            shape = inside if first else below
            first = False
            d = traced(shape, s, x0, y0)
            below = below & ~mm
            if d:
                paths.append((hexc(col), d))
        if paths:
            name = colour_name(gr["col"])
            used[name] = used.get(name, 0) + 1
            out.append((name if used[name] == 1 else f"{name}-{used[name]}", paths))
    return box, out, defs


def clear_caches():
    trace_object.cache_clear()
