"""Cut every object out of the splash picture, and complete the parts that are hidden.

Stage 1 of the splash rebuild (`build_splash.py` runs everything). Reads
`Assets/Art/splash-source.png` read-only. Nothing here writes a file.

1. **Objects.** `OBJECTS` lists every traced object back to front, each with a box (or
   polygon) read off the original and the way it is cut out:

   - `flood` - the object is drawn with a dark outline: the background is flooded in from
     the box's border, stopping at the outline, and the object is what is left (the
     connected piece holding the object's `seed` point). Frogs, clouds, lily pads,
     water lilies, the log and the grass tufts.
   - `colour` - everything in the polygon that is not the colour of what is behind it
     (sky, water, pad): the reeds and the splashes.
   - `ink` - the dark marks in the polygon: the dashed hop arcs.
   - `ground` - the pond, the front grass and the two far banks: whatever is left inside
     their polygon once everything in front is cut out.

   Objects are cut front to back, and each takes only what nothing in front of it took.
   A thin band of outline next to an object is given to it, so no outline is left behind
   on the object underneath.
2. **Completion.** Each object's full shape is what it shows, plus what of its completion
   shape (`complete`: its convex hull, its polygon, or nothing) is hidden by something
   in front of it. Hidden pixels are filled from the object's own visible pixels with a
   smooth push-pull fill, so the log carries on as log under the big frog and the water
   as water. That is what makes the picture work with the frogs taken away.
"""
from __future__ import annotations

from functools import lru_cache
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
ORIGINAL = REPO / "Assets" / "Art" / "splash-source.png"
W, H = 1586, 992

# --------------------------------------------------------------------------
# Tuning
# --------------------------------------------------------------------------
INK_L = 45.0          # Lab lightness below which a greyish pixel is outline ink ...
INK_C = 25.0          # ... (Lab chroma under this); anything darker than INK_BLACK is ink too
INK_BLACK = 15.0
INK_CLOSE = 2         # px: gaps this small in an outline are closed before flooding
OUTLINE_REACH = 9     # px: a `flood` object's outline is at most this far from its colours
INK_CLAIM = 3         # px of adjoining ink an object takes with it (its own outline)
COLOUR_DE = 16.0      # a `colour` object differs at least this much (Lab) from its background
MIN_PART = 30         # visible specks smaller than this (px) are dropped
KNOWN_TRIM = 2        # px of an object's visible edge not used to fill its hidden part
GROUND_FRINGE = 10    # px around an object where off-colour ground is that object's fringe
TUCK = 2              # px an object runs on under whatever hides it


# --------------------------------------------------------------------------
# The objects, back to front. Coordinates are pixels of the original (1586 x 992).
# --------------------------------------------------------------------------
def _o(oid, layer, label, cut, **kw):
    return dict(id=oid, layer=layer, label=label, cut=cut, **kw)


OBJECTS = [
    _o("cloud-left", "clouds", "Cloud, top left", "flood",
       box=(52, 46, 236, 186), seed=(140, 120), complete="hull", own=["white"]),
    _o("cloud-right", "clouds", "Cloud, top right", "flood",
       box=(1350, 82, 1540, 212), seed=(1440, 150), complete="hull", own=["white"]),
    _o("title-strokes-left", "title", "Action strokes, left of FROGS", "flood",
       box=(334, 248, 430, 380), parts=True, complete=None, own=["green"]),
    _o("title-strokes-right", "title", "Action strokes, right of FROGS", "flood",
       box=(1240, 222, 1324, 320), parts=True, complete=None, own=["green"]),
    _o("bank-left", "banks", "Far bank, left", "ground",
       poly=[(0, 572), (120, 572), (240, 586), (240, 606), (120, 616), (0, 634)], complete="poly", own=["green"]),
    _o("bank-right", "banks", "Far bank, right", "ground",
       poly=[(1350, 574), (1586, 574), (1586, 628), (1480, 614), (1360, 602)], complete="poly", own=["green"]),
    _o("reeds-left", "reeds", "Cattails and reeds, left", "flood",
       box=(0, 236, 244, 590), parts=True, complete=None, own=["green", "brown"], no_sky=True),
    _o("reeds-right", "reeds", "Cattails and reeds, right", "flood",
       box=(1352, 250, 1586, 590), parts=True, complete=None, own=["green", "brown"], no_sky=True),
    _o("pond", "pond", "Pond", "ground",
       poly=[(0, 628), (232, 600), (400, 592), (1360, 594), (1480, 606), (1586, 616),
             (1586, 912), (0, 912)], complete="poly", near_own=["water"]),
    _o("pad-left-back", "lily-pads", "Lily pad, left (back)", "flood",
       box=(150, 612, 392, 700), seed=(200, 660), complete="hull", own=["green"]),
    _o("pad-centre-left", "lily-pads", "Lily pad, centre left", "flood",
       box=(400, 604, 556, 654), seed=(500, 632), complete="hull", own=["green"]),
    _o("pad-right-back", "lily-pads", "Lily pad, right (back)", "flood",
       box=(1024, 598, 1246, 660), seed=(1070, 632), complete="hull", own=["green"]),
    _o("pad-left-front", "lily-pads", "Lily pad, left (front), under the water lily", "flood",
       box=(92, 758, 322, 830), seed=(130, 800), complete="hull", own=["green"]),
    _o("pad-right-front", "lily-pads", "Lily pad, right (front), under the water lily", "flood",
       box=(1266, 654, 1526, 720), seed=(1300, 690), complete="hull", own=["green"]),
    _o("lily-left", "water-lilies", "Water lily, left", "flood",
       box=(138, 714, 272, 804), seed=(205, 765), complete=None, own=["pink"]),
    _o("lily-right", "water-lilies", "Water lily, right", "flood",
       box=(1320, 608, 1458, 688), seed=(1390, 650), complete=None, own=["pink"]),
    _o("splash-left", "splashes", "Splash, left", "colour",
       poly=[(240, 596), (350, 596), (350, 660), (240, 660)], bg=["sky", "pond", "pad-left-back"], complete=None, blue=True),
    _o("splash-centre-left", "splashes", "Splash, centre left", "colour",
       poly=[(426, 578), (492, 578), (492, 620), (426, 620)], bg=["sky", "pond", "pad-centre-left"], complete=None, blue=True),
    _o("splash-right", "splashes", "Splash, right", "colour",
       poly=[(1106, 530), (1198, 530), (1198, 630), (1106, 630)], bg=["sky", "pond", "pad-right-back"], complete=None, blue=True),
    _o("arc-left", "hop-arcs", "Hop arc, left", "ink",
       poly=[(222, 500), (300, 500), (300, 628), (222, 628)], complete=None),
    _o("arc-centre-left", "hop-arcs", "Hop arc, centre left", "ink",
       poly=[(296, 496), (470, 496), (470, 610), (296, 610)], complete=None),
    _o("arc-right", "hop-arcs", "Hop arc, right", "ink",
       poly=[(1028, 436), (1196, 436), (1196, 604), (1028, 604)], complete=None),
    _o("grass", "grass", "Front grass", "ground",
       poly=[(0, 892), (1586, 892), (1586, 992), (0, 992)], complete="shore", near_own=["green", "water"],
       near_min_l=56, near_reach=18),
    _o("log", "log", "Log", "flood",
       box=(388, 712, 1308, 958), seed=(1000, 900), complete="hull-all", own=["brown"],
       foreign=["green"], fill="rows", hidden_outline=5),
    _o("tuft-left", "grass-tufts", "Grass tuft, left", "dark",
       poly=[(270, 893), (296, 852), (306, 850), (330, 884), (352, 846), (366, 846), (370, 886),
             (392, 888), (396, 896), (380, 936), (296, 942), (272, 916)], complete=None),
    _o("tuft-right", "grass-tufts", "Grass tuft, right", "dark",
       poly=[(1290, 880), (1300, 870), (1340, 852), (1352, 852), (1350, 900), (1372, 898),
             (1374, 908), (1350, 948), (1294, 948), (1290, 920)], complete=None),
    _o("frog-left", "frog-left", "Jumping frog, left", "flood",
       box=(200, 396, 380, 566), seed=(285, 480), complete=None, own=["green"], colours=20, keep_small=True),
    _o("frog-centre-left", "frog-centre-left", "Jumping frog, centre left", "flood",
       box=(455, 456, 602, 586), seed=(527, 510), complete=None, own=["green"], colours=20, keep_small=True),
    _o("frog-right", "frog-right", "Jumping frog, right", "flood",
       box=(1190, 386, 1358, 550), seed=(1270, 470), complete=None, own=["green"], colours=20, keep_small=True),
    _o("frog-big", "frog-big", "Big frog on the log", "flood",
       box=(584, 378, 1002, 852), seed=(800, 700), complete=None, own=["green"], foreign=["brown"],
       colours=20, keep_small=True),
]

OBJ = {o["id"]: o for o in OBJECTS}
ORDER = [o["id"] for o in OBJECTS]
FROGS = [o for o in ORDER if o.startswith("frog-")]


# --------------------------------------------------------------------------
# Helpers
# --------------------------------------------------------------------------
def disk(r):
    r = int(round(r))
    y, x = np.mgrid[-r:r + 1, -r:r + 1]
    return ((x * x + y * y) <= r * r).astype(np.uint8)


def dilate(m, r):
    import cv2
    return cv2.dilate(m.astype(np.uint8), disk(r)).astype(bool) if r > 0 else m.copy()


def erode(m, r):
    import cv2
    return cv2.erode(m.astype(np.uint8), disk(r)).astype(bool) if r > 0 else m.copy()


def to_lab(rgb_u8):
    import cv2
    return cv2.cvtColor(np.asarray(rgb_u8, np.float32) / 255.0, cv2.COLOR_RGB2Lab)


@lru_cache(maxsize=1)
def original():
    return np.asarray(Image.open(ORIGINAL).convert("RGB"))


@lru_cache(maxsize=1)
def original_lab():
    import cv2
    return to_lab(cv2.GaussianBlur(original(), (0, 0), 0.7))


@lru_cache(maxsize=1)
def ink():
    """Outline ink: very dark pixels, with hairline gaps closed."""
    import cv2
    lab = original_lab()
    m = ((lab[..., 0] < INK_L) & (np.hypot(lab[..., 1], lab[..., 2]) < INK_C)) | (lab[..., 0] < INK_BLACK)
    return cv2.morphologyEx(m.astype(np.uint8), cv2.MORPH_CLOSE, disk(INK_CLOSE)).astype(bool)


def poly_mask(pts):
    im = Image.new("L", (W, H), 0)
    ImageDraw.Draw(im).polygon([(float(x), float(y)) for x, y in pts], fill=255)
    return np.asarray(im) > 0


def box_mask(box):
    x0, y0, x1, y1 = box
    m = np.zeros((H, W), bool)
    m[y0:y1, x0:x1] = True
    return m


def area_of(o):
    return poly_mask(o["poly"]) if "poly" in o else box_mask(o["box"])


def drop_specks(m, n=MIN_PART):
    from scipy import ndimage as ndi
    lbl, k = ndi.label(m)
    if not k:
        return m
    sizes = ndi.sum(m, lbl, range(1, k + 1))
    return np.isin(lbl, [j + 1 for j, s in enumerate(sizes) if s >= n])


def hull(m):
    import cv2
    ys, xs = np.nonzero(m)
    if len(xs) < 3:
        return m.copy()
    pts = cv2.convexHull(np.stack([xs, ys], 1).astype(np.int32))
    out = np.zeros((H, W), np.uint8)
    cv2.fillPoly(out, [pts], 1)
    return out.astype(bool) | m


# --------------------------------------------------------------------------
# 1. Cut-out, front to back
# --------------------------------------------------------------------------
def flood_cut(o, walls):
    """Flood the background in from the box's border, stopping at outline ink and at
    whatever is in front; the object is the piece holding its seed point."""
    from scipy import ndimage as ndi
    area = area_of(o)
    passable = area & ~ink() & ~walls
    if o.get("own"):
        passable &= ~colour_class(o["own"])
    border = area & ~erode(area, 1)
    lbl, _ = ndi.label(passable)
    bg = np.isin(lbl, np.setdiff1d(np.unique(lbl[border & passable]), [0]))
    obj = area & ~bg & ~walls
    if o.get("no_sky"):
        obj &= ~sky_guess()        # sky seen through a gap between leaves
    if o.get("foreign"):
        obj &= ~(colour_class(o["foreign"]) & ~ink())   # the log seen between the toes
    if o.get("parts"):
        return drop_specks(obj)
    lbl, _ = ndi.label(obj)
    sx, sy = o["seed"]
    k = lbl[sy, sx]
    assert k, f"{o['id']}: seed point is not inside the cut-out"
    obj = lbl == k
    # ink that only touches the object (the log's top edge running into the big frog's
    # outline) is not its outline: keep ink within an outline's width of its colours
    core = ndi.binary_fill_holes(obj & ~ink())
    return obj & dilate(core, OUTLINE_REACH)


def dark_cut(o, walls):
    """Dark leaves and their outline, and whatever they enclose (the grass tufts)."""
    from scipy import ndimage as ndi
    import cv2
    lab = original_lab()
    L, a, b = lab[..., 0], lab[..., 1], lab[..., 2]
    area = area_of(o) & ~walls
    m = area & (ink() | ((L < 48) & (a < 0) & (b > -25))) & ~colour_class(["brown"])
    m = cv2.morphologyEx(m.astype(np.uint8), cv2.MORPH_CLOSE, disk(2)).astype(bool)
    return drop_specks(ndi.binary_fill_holes(m) & area)


def colour_cut(o, walls, done):
    """Pixels in the polygon that differ from the colour of what is behind them."""
    from scipy import ndimage as ndi
    import cv2
    area = area_of(o) & ~walls
    lab = original_lab()
    behind = np.zeros((H, W), bool)
    for b in o["bg"]:
        behind |= sky_guess() if b == "sky" else done.get(b, area_of(OBJ[b]))
    ring = dilate(area_of(o), 12) & ~area_of(o) & ~walls
    # the background's colour, row by row (sky and water both change down the picture)
    ref = np.full((H, 3), np.nan)
    known = ring & behind & ~dilate(ink(), 2)
    for y in range(H):
        if known[y].sum() >= 4:
            ref[y] = np.median(lab[y][known[y]], axis=0)
    ys = np.nonzero(~np.isnan(ref[:, 0]))[0]
    ref = np.stack([np.interp(np.arange(H), ys, ref[ys, c]) for c in range(3)], -1)
    de = np.linalg.norm(lab - ref[:, None, :], axis=-1)
    m = area & (de > COLOUR_DE)
    if o.get("blue"):
        m &= lab[..., 2] < -12        # a splash is blue (pad edges and veins are not)
    # a pad or the water behind a splash is green / blue, not the row's median:
    # anything within COLOUR_DE of any background object's own colour stays background
    for b in o["bg"]:
        if b in ("sky",):
            continue
        bm = (done.get(b) if b in done else area_of(OBJ[b])) & ring & ~dilate(ink(), 2)
        if bm.sum() > 20:
            cents = _palette(lab[bm], 4)
            near = np.min(np.linalg.norm(lab[..., None, :] - cents[None, None], axis=-1), -1) < COLOUR_DE * 0.6
            m &= ~near | ink()
    m = cv2.morphologyEx(m.astype(np.uint8), cv2.MORPH_OPEN, disk(1)).astype(bool)
    m = ndi.binary_fill_holes(m) & area
    return drop_specks(m)


def _palette(px, k):
    from tracing import kmeans_lab
    return kmeans_lab(px, k, min_dist=8.0)


def ink_cut(o, walls):
    m = area_of(o) & ink() & ~walls
    return drop_specks(dilate(m, 1) & area_of(o) & ~walls, 25)


def colour_class(names):
    """Pixels of the named colour families (Lab), for `flood` objects whose outline the
    flood could slip through: their own colours stop it as well as the ink does."""
    lab = original_lab()
    L, a, b = lab[..., 0], lab[..., 1], lab[..., 2]
    m = np.zeros((H, W), bool)
    for n in names:
        if n == "green":
            m |= (a < -12) & (b > 10)
        elif n == "white":
            m |= (L > 88) & (np.hypot(a, b) < 10)
        elif n == "red":
            m |= (a > 25) & (b > 0) & (b < 45 + a)
        elif n == "brown":
            m |= (a > 6) & (a < 34) & (b > 15) & (b > a) & (L < 80)
        elif n == "water":
            m |= b < 5
        elif n == "pink":
            m |= (a > 30) & (b < 25)
        else:
            raise ValueError(n)
    return m


@lru_cache(maxsize=1)
def sky_guess():
    """Above the water line and light blue or white: the sky (for `colour` cuts)."""
    lab = original_lab()
    yy = np.arange(H)[:, None]
    return (yy < 600) & (lab[..., 0] > 70) & (lab[..., 2] < 8)


@lru_cache(maxsize=1)
def visible():
    """{object id: full-frame bool of what the original shows of it}, plus 'sky'."""
    done = {}
    claimed = np.zeros((H, W), bool)
    for oid in reversed(ORDER):
        o = OBJ[oid]
        if o["cut"] == "flood":
            m = flood_cut(o, claimed)
        elif o["cut"] == "colour":
            m = colour_cut(o, claimed, done)
        elif o["cut"] == "ink":
            m = ink_cut(o, claimed)
        elif o["cut"] == "dark":
            m = dark_cut(o, claimed)
        else:
            continue
        # the object's own outline: adjoining ink nobody in front has taken
        m = m | (dilate(m, INK_CLAIM) & ink() & area_of(o) & ~claimed)
        done[oid] = m
        claimed |= m
    # ground, front to back (grass over pond over banks)
    objects = claimed.copy()
    for oid in reversed(ORDER):
        o = OBJ[oid]
        if o["cut"] != "ground":
            continue
        m = poly_mask(o["poly"]) & ~claimed & ~sky_guess()
        if o.get("own"):
            m &= colour_class(o["own"]) | ink()
        if o.get("near_own"):
            # right next to an object, what is not the ground's own colour is that object's
            # fringe (a frog's toe, the log's shadow): it is not ground that shows, and is
            # filled like the ground hidden under the object
            near = dilate(objects, o.get("near_reach", GROUND_FRINGE))
            m &= ~(near & ~colour_class(o["near_own"]))
            if o.get("near_min_l"):
                m &= ~(near & (original_lab()[..., 0] < o["near_min_l"]))   # its shadow
        done[oid] = drop_specks(m)
        claimed |= done[oid]
    done["sky"] = ~claimed
    return done


# --------------------------------------------------------------------------
# 2. Completion
# --------------------------------------------------------------------------
@lru_cache(maxsize=1)
def regions():
    """{object id: (full region, visible part)}, both full-frame bool.

    Built front to back, so an object is completed under everything in front of it -
    including what those objects are completed with themselves (the water runs on under
    the hidden part of the log as well as under the log that shows)."""
    vis = visible()
    out = {}
    front = np.zeros((H, W), bool)       # the full regions of everything in front
    for oid in reversed(ORDER):
        v = vis[oid]
        mode = OBJ[oid]["complete"]
        if mode == "hull-all":
            # a convex object (the log): all of its hull is it, including the specks of
            # the frog's toes the cut-out left behind
            r = hull(v)
        else:
            if mode == "hull":
                shape = hull(v)
            elif mode == "poly":
                shape = poly_mask(OBJ[oid]["poly"])
            elif mode == "shore":
                shape = poly_mask(OBJ[oid]["poly"]) & below_shore(v, front)
            else:
                shape = v
            r = v | (shape & front) | (dilate(v, TUCK) & front & dilate(shape, TUCK))
            if mode in ("poly", "shore"):
                # ground also takes the specks inside its area that no object took (a
                # pocket of water between splash drops), so nothing there is left empty
                r |= shape & vis["sky"] & ~sky_guess()
        out[oid] = (r, v)
        front |= r
    return out


def below_shore(v, front):
    """Everything below the object's top edge, where the top edge behind something in
    front is carried straight across from the edge either side of it (the shoreline
    behind the log)."""
    from scipy import ndimage as ndi
    top = np.full(W, np.nan)
    water = colour_class(["water"]) & (original_lab()[..., 2] < -20)
    for x in range(W):
        ys = np.nonzero(v[:, x])[0]
        # a true shoreline has water just above it, not something in front
        if len(ys) and water[max(0, ys[0] - 4):ys[0], x].any() and not front[max(0, ys[0] - 3):ys[0], x].any():
            top[x] = ys[0]
    ok = ~np.isnan(top)
    xs = np.arange(W)
    line = ndi.median_filter(np.interp(xs, xs[ok], top[ok]), size=15)
    return np.arange(H)[:, None] >= line[None, :]


def push_pull(img, known):
    """Fill the unknown pixels of img (H x W x 3 float) smoothly from the known ones."""
    import cv2
    levels = []
    I = img * known[..., None]
    K = known.astype(np.float32)
    while True:
        levels.append((I, K))
        h, w = K.shape
        if h <= 2 and w <= 2:
            break
        size = (max(1, (w + 1) // 2), max(1, (h + 1) // 2))
        I = cv2.resize(I, size, interpolation=cv2.INTER_AREA)
        K = cv2.resize(K, size, interpolation=cv2.INTER_AREA)
    F = None
    for I, K in reversed(levels):
        own = I / np.maximum(K, 1e-6)[..., None]
        if F is None:
            F = own
            continue
        U = cv2.resize(F, (K.shape[1], K.shape[0]), interpolation=cv2.INTER_LINEAR)
        w = np.clip(K * 4, 0, 1)[..., None]
        F = own * w + U * (1 - w)
    return F


def object_box(oid, pad=4):
    r = regions()[oid][0]
    ys, xs = np.nonzero(r)
    return (max(0, int(xs.min()) - pad), max(0, int(ys.min()) - pad),
            min(W, int(xs.max()) + pad + 1), min(H, int(ys.max()) + pad + 1))


def plate(oid):
    """(box, crop RGB float with the hidden part filled and the outside extended, region crop,
    visible crop)."""
    from scipy import ndimage as ndi
    r, v = regions()[oid]
    x0, y0, x1, y1 = object_box(oid)
    rc, vc = r[y0:y1, x0:x1], v[y0:y1, x0:x1]
    img = original()[y0:y1, x0:x1].astype(np.float32)
    known = erode(vc, KNOWN_TRIM) & ~dilate(ink()[y0:y1, x0:x1], 1)
    if known.sum() < 0.3 * vc.sum():
        known = vc
    filled = push_pull(img, known)
    if OBJ[oid].get("fill") == "rows":
        # the few px of the object right next to what hid it carry that thing's outline
        # and shadow: they are filled like the hidden part
        vc = vc & ~dilate(rc & ~vc, 4)
        # sources: the object's own visible colours, not its outline and not the fringe
        # of whatever hid it
        src = erode(vc, KNOWN_TRIM) & ~dilate(rc & ~vc, 4) & ~(ink()[y0:y1, x0:x1] & ~erode(rc, 10))
        filled = row_fill(img, src, filled)
    out = np.where(vc[..., None], img, filled)
    band = OBJ[oid].get("hidden_outline")
    if band:
        # the hidden stretch of the object's outline is drawn in, in the outline's colour
        edge = rc & ~erode(rc, band)
        seen = edge & vc & ink()[y0:y1, x0:x1]
        if seen.any():
            out[edge & ~vc] = np.median(img[seen], axis=0)
    _, (iy, ix) = ndi.distance_transform_edt(~rc, return_indices=True)
    return (x0, y0, x1, y1), out[iy, ix], rc, vc


def row_fill(img, known, fallback):
    """Each hidden pixel takes the colour between the nearest known pixels to its left and
    right in the same row, so the log's grain carries on across what is hidden; where a
    row has known pixels on one side only, the smooth fill is used."""
    out = fallback.copy()
    xs_all = np.arange(img.shape[1])
    for y in range(img.shape[0]):
        k = np.nonzero(known[y])[0]
        if len(k) < 2:
            continue
        inside = (xs_all > k[0]) & (xs_all < k[-1]) & ~known[y]
        if not inside.any():
            continue
        for c in range(3):
            out[y, inside, c] = np.interp(xs_all[inside], k, img[y, k, c])
    return out


def clear_caches():
    for f in (original, original_lab, ink, sky_guess, visible, regions):
        f.cache_clear()
