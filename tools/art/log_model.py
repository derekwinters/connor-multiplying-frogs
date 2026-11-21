"""Multiplying Frogs -- the 3D log, modelled headless in Blender.

Re-runnable: builds the log pieces from nothing and writes log.glb, log.fbx,
log_albedo.png and preview PNGs. Nothing is touched by hand in a Blender UI;
change the numbers here and run it again. It follows frog_model.py's pipeline
(dense mesh -> vertex colour -> shipped mesh -> UVs -> Cycles bake -> export ->
Cycles previews).

    # bpy wheels are built for one Python (bpy 5.0 wants 3.11):
    uv venv -p 3.11 venv && VIRTUAL_ENV=venv uv pip install bpy pillow
    venv/bin/python log_model.py --out <dir>               # model, exports, previews
    venv/bin/python log_model.py --out <dir> --no-render   # model and exports only
    venv/bin/python log_model.py --out <dir> --frog <frog.glb>  # frog for the previews

Conventions (Blender space), the same as the frog's: Z is up, one unit is about
one frog length (the frog sits on a 112 px lily pad, so 1 px ~ 1/112 unit).
The log's long axis is Y. The log floats: the WATERLINE is z = 0, about the
lower third of the log is under it, and the mesh is cut off flat a little below
the waterline (z ~ CUT_Z) because the board camera never sees further down.
Its width across X is LogWidth, 176 px = LOG_WIDTH units.

The pieces (each a separate named mesh)
--------------------------------------

* LogBody_A, LogBody_B -- tileable middle segments, exactly 1.0 unit along +Y.
  Origin: on the -Y end face, centred in X, ON THE WATERLINE (z = 0), so a
  piece dropped at z = 0 floats correctly. Both end rings of both bodies are
  the same ring (same vertices, normals and UV rows), and every shape and
  colour term is periodic in Y, so any sequence of A/B butts together with no
  seam. B adds a sawn-off branch stub on the +X side (not the top, where frogs
  sit) to break the repetition.
* LogCap -- an end piece CAP_LEN (0.5) long along +Y, origin on its joining
  face (same convention: centred in X, on the waterline). Its far end is a
  sawn face with tree rings and a bark rim, and it has a little moss on top.
  The same cap is used at both ends: the start cap is rotated 180 deg about Z.
  That works because every body end ring is point-symmetric under that
  rotation (all shape terms are chosen so that F(-theta, -y) = F(theta, y)).

All three share one material, LogBark, and one baked texture, log_albedo.png.
The albedo is the real colour (centred on LogBrown #4A2E1A); it is NOT tinted
per player like the frog.

Assembly rule (what Unity would do for any target length L)
-----------------------------------------------------------

    mid = L - 2 * CAP_LEN                 # length left for the bodies
    n   = max(1, floor(mid / 1.0 + 0.5))  # number of bodies (round half up)
    sy  = mid / n                         # Y scale of every body
    bodies alternate A, B, A, B ... starting with A

    start cap: origin at y0 + CAP_LEN, rotated 180 deg about Z (it runs back to y0)
    body k   : origin at y0 + CAP_LEN + k * sy, scaled (1, sy, 1)
    end cap  : origin at y0 + L - CAP_LEN, unrotated

For mid >= 0.5 the body scale stays within [0.67, 1.5] (and within [0.875,
1.17] once mid >= 3.5). Below that (L < 1.5) only one squashed body is left
and the caps should be scaled too -- see the report. The two assemblies in the
GLB are built by exactly this rule:

* SharedLog -- the full pond band, 896 px = 8.0 units: n = 7, sy = 1.0.
* PlayerLog -- one lane, 184 px = 1.643 units: n = 1, sy = 0.643 (the one
  body is squashed along Y to fit -- the caps are not).

Assembly roots sit at the CENTRE of the log's length, on the waterline, so a
root placed at a lane's (or the pond band's) centre puts the log on it.

glTF is Y-up: the exporter maps Blender (x, y, z) to glTF (x, z, -y), so in
log.glb the log's long axis is -Z and "up" is +Y. FBX is written with the
frog's axis settings (forward -Z, up Y).
"""

import argparse
import math
import os
import sys
import time

import bpy  # noqa: E402  (bmesh needs bpy imported first)
import bmesh
import numpy as np
from mathutils import Euler, Matrix, Vector

TAU = 2 * math.pi
PX = 1 / 112                      # one board pixel, in frog units

# --------------------------------------------------------------------------
# Shape
# --------------------------------------------------------------------------

LOG_WIDTH = 176 * PX              # LogWidth: 1.571 units across X
HALF_W = LOG_WIDTH / 2            # ellipse half-width (x)
HALF_H = 0.68                     # ellipse half-height (z): a little flatter than round,
                                  # so the top is broad enough for a frog to sit on
ZC = HALF_H / 3                   # section centre: a third of the log under water
CUT_Z = -0.22                     # flat cut-off below the waterline (unseen)

N_RIDGES = 14                     # bark ridges round the whole circumference
COLS_PER_RIDGE = 4                # low-mesh columns per ridge: groove, shoulder, top, shoulder
STEP = TAU / (N_RIDGES * COLS_PER_RIDGE)
J = 20                            # columns each side of the top: theta up to 20 * STEP = 128.6 deg
RIDGE_DEPTH = 0.05                # groove depth on the sides (the top is shallower)
RIDGE_SHARP = 0.4                 # sin^k plate profile: broad plates, narrow V grooves

BODY_LEN = 1.0
BODY_ROWS = 12                    # low-mesh rings along a body
CAP_LEN = 0.5
CAP_Y = [0.0, 0.07, 0.14, 0.21, 0.28, 0.35, 0.40, 0.44, 0.47, 0.49, 0.50]
FACE_RINGS = [0.93, 0.80, 0.60, 0.38, 0.17]  # sawn face, scaled toward the pith

# Branch stub on LogBody_B (side, not top).
STUB_THETA = 1.15                 # radians from the top toward +X
STUB_Y = 0.5
STUB_SIDES = 8

BODY_TRIS = 1200                  # budgets, asserted
CAP_TRIS = 2000
TEXTURE_SIZE = 1024
UV_S = 0.22                       # UV units per world unit (~225 texels/unit at 1024)

SHARED_LOG_LEN = 896 * PX         # SharedLogHeight
PLAYER_LOG_LEN = 184 * PX         # one lane

# Albedo, sRGB.
def rgb(h):
    return tuple(int(h[i:i + 2], 16) / 255 for i in (1, 3, 5))


LOG_BROWN = rgb("#4A2E1A")        # BoardColours.LogBrown
GROOVE = rgb("#28180D")
PLATE = rgb("#583720")
TOP = rgb("#654228")
BOTTOM = rgb("#2A1A0E")
WOOD = rgb("#D2A46C")
SAPWOOD = rgb("#E3C08C")
RING = rgb("#A87A4A")
PITH = rgb("#8A5E38")
BARK_RIM = rgb("#38220F")
MOSS = rgb("#6E8F3C")
MOSS_DARK = rgb("#4F6E2C")


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def mix(c, target, t):
    """c (N,3) toward a colour by t (N,)."""
    return c + (np.asarray(target)[None, :] - c) * np.asarray(t, dtype=float)[:, None]


# --------------------------------------------------------------------------
# 1. The surface, as functions of (column jf, y)
#
# jf is the column coordinate: 0 is the top of the log, +-J the cut edges,
# and a whole multiple of COLS_PER_RIDGE is the middle of a bark plate. The
# low mesh samples whole columns; the dense (bake source) mesh samples them
# finely. Columns follow the ridges' wander, so the low mesh's grooves stay on
# its grooves. Every term is periodic in y with period 1, and every term that
# reaches y = 0 is point-symmetric (theta, y) -> (-theta, -y): that is what
# makes A|B, B|A and cap|body joins seamless.
# --------------------------------------------------------------------------

def phase(jf):
    return np.mod(0.5 + jf / COLS_PER_RIDGE, 1.0)


def ridge_f(p):
    return np.sin(np.pi * p) ** RIDGE_SHARP


def bump4(y, period):
    """0 with zero 1st-3rd derivatives at y = 0 and y = period: interior-only terms."""
    return np.sin(np.pi * y / period) ** 4


def wander(t0, y, piece):
    d = (0.028 * np.sin(TAU * y) * np.cos(2 * t0)
         + 0.016 * np.cos(TAU * y) * np.sin(3 * t0)
         + 0.012 * np.sin(2 * TAU * y) * np.cos(5 * t0))
    if piece == "B":
        d = d + 0.045 * bump4(y, 1.0) * np.sin(4 * t0)
    elif piece == "CAP":
        d = d + 0.03 * bump4(y, CAP_LEN) * np.sin(3 * t0)
    return d


def amplitude(t0, y, piece):
    a = RIDGE_DEPTH * (0.5 + 0.5 * smoothstep(0.35, 1.0, np.abs(t0)))
    a = a * (1 + 0.22 * np.cos(3 * t0) * np.cos(TAU * y)
             + 0.18 * np.sin(TAU * y) * np.sin(2 * t0)
             + 0.12 * np.cos(7 * t0))
    if piece == "B":
        a = a * (1 + 0.35 * bump4(y, 1.0) * np.cos(5 * t0))
    return a


def cap_shrink(y):
    """The cap's rounded bark edge: the section shrinks just before the sawn face."""
    return 1 - 0.07 * smoothstep(0.40, 0.50, y) ** 1.6


def cap_ridge(y):
    return 1 - 0.5 * smoothstep(0.42, 0.50, y)


def surf(jf, y, piece):
    """Surface points (N,3) at column jf, distance y along the piece."""
    jf, y = np.broadcast_arrays(np.asarray(jf, float), np.asarray(y, float))
    t0 = jf * STEP
    t = t0 + wander(t0, y, piece)
    s = np.ones_like(y)
    r = np.ones_like(y)
    if piece == "CAP":
        s, r = cap_shrink(y), cap_ridge(y)
    d = -amplitude(t0, y, piece) * (1 - ridge_f(phase(jf))) * r
    bx, bz = HALF_W * np.sin(t) * s, HALF_H * np.cos(t) * s
    nx, nz = HALF_H * np.sin(t), HALF_W * np.cos(t)
    nl = np.hypot(nx, nz)
    x = bx + d * nx / nl
    z = ZC + bz + d * nz / nl
    return np.stack([x, y, z], axis=-1)


def surf_normal(jf, y, piece, e=1e-4):
    dj = surf(jf + e, y, piece) - surf(jf - e, y, piece)
    dy = surf(jf, y + e, piece) - surf(jf, y - e, piece)
    n = np.cross(dj, dy)
    return n / np.linalg.norm(n, axis=-1, keepdims=True)


# Fixed UV row per column: the arc length round the y = 0 ring.
_ring0 = surf(np.arange(-J, J + 1), 0.0, "A")
ARC = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(_ring0, axis=0), axis=1))])

# --------------------------------------------------------------------------
# 2. Building meshes
# --------------------------------------------------------------------------


class MeshBuilder:
    """Vertices with per-vertex metadata (for painting) and optional analytic
    normals; faces with per-corner UVs, a hint for which way is out, and
    smooth/flat shading."""

    def __init__(self):
        self.co, self.nrm, self.meta = [], [], []
        self.faces, self.uvs, self.smooth, self.fnrm = [], [], [], []

    def vert(self, co, normal=None, **meta):
        self.co.append(tuple(co))
        self.nrm.append(None if normal is None else tuple(normal))
        self.meta.append(meta)
        return len(self.co) - 1

    def face(self, idx, uv=None, hint=None, smooth=True, normal=None):
        """normal: an exact normal for every corner of a flat face (so the
        cut bottom and the sawn faces do not depend on the ring's wander)."""
        idx = list(idx)
        uv = list(uv) if uv is not None else [(0.0, 0.0)] * len(idx)
        if hint is not None:  # wind so the face normal agrees with the hint
            P = np.array([self.co[i] for i in idx])
            n = np.zeros(3)
            for a, b in zip(P, np.roll(P, -1, axis=0)):  # Newell
                n += np.array([(a[1] - b[1]) * (a[2] + b[2]), (a[2] - b[2]) * (a[0] + b[0]),
                               (a[0] - b[0]) * (a[1] + b[1])])
            if n @ np.asarray(hint) < 0:
                idx.reverse()
                uv.reverse()
        self.faces.append(idx)
        self.uvs.append(uv)
        self.smooth.append(smooth)
        self.fnrm.append(normal)

    def tris(self):
        return sum(len(f) - 2 for f in self.faces)

    def to_object(self, name, uv=True, custom_normals=True):
        me = bpy.data.meshes.new(name)
        me.from_pydata(self.co, [], self.faces)
        me.update()
        for p, s in zip(me.polygons, self.smooth):
            p.use_smooth = s
        if uv:
            layer = me.uv_layers.new(name="UVMap")
            flat = [c for f in self.uvs for c in f]
            layer.data.foreach_set("uv", np.array(flat, dtype=np.float32).ravel())
        me.update()
        if custom_normals:
            auto = [Vector(n.vector) for n in me.corner_normals]
            loops = []
            for p, fn in zip(me.polygons, self.fnrm):
                for li in p.loop_indices:
                    vi = me.loops[li].vertex_index
                    an = self.nrm[vi]
                    if fn is not None:
                        loops.append(Vector(fn).normalized())
                    elif p.use_smooth and an is not None:
                        loops.append(Vector(an))
                    else:
                        loops.append(auto[li])
            me.normals_split_custom_set([tuple(n) for n in loops])
        obj = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(obj)
        return obj


def add_tube(mb, piece, cols, ys, uv_u0, uv_v0, normals=True, meta=None):
    """The bark surface: a grid of columns x rings, plus the flat bottom strip
    under the waterline. Returns the grid of vertex indices [ring][col]."""
    meta = meta or {}
    grid = []
    for y in ys:
        P = surf(cols, y, piece)
        N = surf_normal(cols, y, piece) if normals else [None] * len(cols)
        grid.append([mb.vert(P[k], N[k], kind=0, jf=cols[k], y=y, piece=piece, **meta)
                     for k in range(len(cols))])
    lowcols = np.allclose(cols, np.round(cols))
    v_of = (lambda k: uv_v0 + ARC[int(round(cols[k])) + J] * UV_S) if lowcols else (lambda k: 0.0)
    for i in range(len(ys) - 1):
        u0, u1 = uv_u0 + ys[i] * UV_S, uv_u0 + ys[i + 1] * UV_S
        for k in range(len(cols) - 1):
            idx = (grid[i][k], grid[i][k + 1], grid[i + 1][k + 1], grid[i + 1][k])
            uvs = ((u0, v_of(k)), (u0, v_of(k + 1)), (u1, v_of(k + 1)), (u1, v_of(k)))
            t = cols[k] * STEP
            mb.face(idx, uvs, hint=(math.sin(t), 0, math.cos(t)))
        # Bottom strip, from the +X cut edge across to the -X one.
        vb = uv_v0 + (ARC[-1] + 0.02) * UV_S
        idx = (grid[i][-1], grid[i][0], grid[i + 1][0], grid[i + 1][-1])
        uvs = ((u0, vb), (u0, vb + 0.025), (u1, vb + 0.025), (u1, vb))
        mb.face(idx, uvs, hint=(0, 0, -1), smooth=False, normal=(0, 0, -1))
    return grid


def add_cap_face(mb, outline_pts, scales, uv0, centre_y, dense_bottom=0):
    """The sawn face: concentric copies of the end ring shrinking to the pith."""
    out = np.asarray(outline_pts)
    if dense_bottom:  # dense source: put points along the bottom edge too
        a, b = out[-1], out[0]
        extra = [a + (b - a) * (i / (dense_bottom + 1)) for i in range(1, dense_bottom + 1)]
        out = np.concatenate([out, extra])
    c = np.array([0.0, centre_y, ZC])
    rings = []
    for s in [1.0] + list(scales):
        pts = c + (out - c) * np.array([s, 0, s])
        pts[:, 1] = centre_y + 0.012 * (1 - s * s)  # the faintest dome
        rings.append([mb.vert(p, None, kind=2, jf=0.0, y=centre_y, piece="CAP") for p in pts])
    centre = mb.vert(c + np.array([0, 0.012, 0]), None, kind=2, jf=0.0, y=centre_y, piece="CAP")

    def uv(i):
        p = mb.co[i]
        return (uv0[0] + (p[0] + HALF_W) * UV_S, uv0[1] + (p[2] - CUT_Z + 0.05) * UV_S)

    n = len(out)
    for r0, r1 in zip(rings, rings[1:]):
        for k in range(n):
            idx = (r0[k], r0[(k + 1) % n], r1[(k + 1) % n], r1[k])
            mb.face(idx, [uv(i) for i in idx], hint=(0, 1, 0), smooth=False, normal=(0, 1, 0))
    for k in range(n):
        idx = (rings[-1][k], rings[-1][(k + 1) % n], centre)
        mb.face(idx, [uv(i) for i in idx], hint=(0, 1, 0), smooth=False, normal=(0, 1, 0))


def stub_frame():
    t = STUB_THETA
    base = np.array([HALF_W * math.sin(t), STUB_Y, ZC + HALF_H * math.cos(t)])
    n = Vector((HALF_H * math.sin(t), 0, HALF_W * math.cos(t))).normalized()
    d = (n + Vector((0, 0.7, 0))).normalized()
    side = d.cross(Vector((0, 0, 1))).normalized()
    up = side.cross(d).normalized()
    return Vector(base), d, side, up


STUB_RINGS = [(-0.14, 0.135), (0.0, 0.14), (0.07, 0.115), (0.20, 0.10)]  # (t along axis, radius)


def add_stub(mb, sides, rings, face_rings, uv0=(0, 0)):
    """B's sawn-off branch: a short tapered tube poking out of the trunk."""
    base, d, side, up = stub_frame()
    grid = []
    for t, r in rings:
        row = []
        for k in range(sides):
            a = TAU * k / sides
            o = side * math.cos(a) + up * math.sin(a)
            row.append(mb.vert(base + d * t + o * r, None, kind=3, jf=float(a), y=float(t), piece="B"))
        grid.append(row)
    tmax = rings[-1][0]
    for i in range(len(rings) - 1):
        for k in range(sides):
            k1 = (k + 1) % sides
            a0, a1 = k / sides, (k + 1) / sides
            idx = (grid[i][k], grid[i][k1], grid[i + 1][k1], grid[i + 1][k])
            v0 = uv0[1] + (rings[i][0] - rings[0][0]) * UV_S
            v1 = uv0[1] + (rings[i + 1][0] - rings[0][0]) * UV_S
            circ = TAU * 0.13 * UV_S
            uvs = ((uv0[0] + a0 * circ, v0), (uv0[0] + a1 * circ, v0),
                   (uv0[0] + a1 * circ, v1), (uv0[0] + a0 * circ, v1))
            a = TAU * (k + 0.5) / sides
            mb.face(idx, uvs, hint=tuple(side * math.cos(a) + up * math.sin(a)))
    # The sawn end, slightly domed.
    r_end = rings[-1][1]
    fu0 = (uv0[0] + TAU * 0.13 * UV_S + 0.02, uv0[1])
    end = base + d * tmax
    face_grid = [grid[-1]]
    for s in face_rings:
        row = []
        for k in range(sides):
            a = TAU * k / sides
            o = side * math.cos(a) + up * math.sin(a)
            row.append(mb.vert(end + d * 0.01 * (1 - s * s) + o * r_end * s, None,
                               kind=4, jf=float(a), y=float(s), piece="B"))
        face_grid.append(row)
    # the face's own outer ring (split from the tube so the edge stays crisp)
    rim = []
    for k in range(sides):
        a = TAU * k / sides
        o = side * math.cos(a) + up * math.sin(a)
        rim.append(mb.vert(end + o * r_end, None, kind=4, jf=float(a), y=1.0, piece="B"))
    face_grid[0] = rim
    centre = mb.vert(end + d * 0.01, None, kind=4, jf=0.0, y=0.0, piece="B")

    def fuv(i):
        p = Vector(mb.co[i]) - end
        return (fu0[0] + (p.dot(side) + r_end) * UV_S, fu0[1] + (p.dot(up) + r_end) * UV_S)

    for r0, r1 in zip(face_grid, face_grid[1:]):
        for k in range(sides):
            k1 = (k + 1) % sides
            idx = (r0[k], r0[k1], r1[k1], r1[k])
            mb.face(idx, [fuv(i) for i in idx], hint=tuple(d), smooth=False)
    last = face_grid[-1]
    for k in range(sides):
        idx = (last[k], last[(k + 1) % sides], centre)
        mb.face(idx, [fuv(i) for i in idx], hint=tuple(d), smooth=False)


# UV atlas (U across, V up), one 1024 texture for all three pieces.
UV_A = (0.02, 0.03)
UV_B = (0.27, 0.03)
UV_CAP = (0.52, 0.03)
UV_FACE = (0.64, 0.03)
UV_STUB = (0.64, 0.36)


def build_body(piece, dense=False):
    mb = MeshBuilder()
    if dense:
        cols = np.linspace(-J, J, 2 * J * 6 + 1)
        ys = np.linspace(0, BODY_LEN, BODY_ROWS * 8 + 1)
    else:
        cols = np.arange(-J, J + 1, dtype=float)
        ys = np.linspace(0, BODY_LEN, BODY_ROWS + 1)
    add_tube(mb, piece, cols, ys, *(UV_A if piece == "A" else UV_B), normals=not dense)
    if piece == "B":
        if dense:
            rings = [(t, np.interp(t, *zip(*STUB_RINGS))) for t in np.linspace(-0.14, 0.20, 24)]
            add_stub(mb, 48, rings, list(np.linspace(0.95, 0.05, 12)))
        else:
            add_stub(mb, STUB_SIDES, STUB_RINGS, [0.55], uv0=UV_STUB)
    name = f"LogBody_{piece}"
    return mb, mb.to_object(name + ("Dense" if dense else ""), uv=not dense, custom_normals=not dense)


def build_cap(dense=False):
    mb = MeshBuilder()
    if dense:
        cols = np.linspace(-J, J, 2 * J * 6 + 1)
        ys = np.unique(np.concatenate([np.linspace(0, 0.4, 40), np.linspace(0.4, CAP_LEN, 30)]))
    else:
        cols = np.arange(-J, J + 1, dtype=float)
        ys = np.array(CAP_Y)
    add_tube(mb, "CAP", cols, ys, *UV_CAP, normals=not dense)
    outline = surf(cols, CAP_LEN, "CAP")
    scales = list(np.linspace(0.97, 0.03, 40)) if dense else FACE_RINGS
    add_cap_face(mb, outline, scales, UV_FACE, CAP_LEN, dense_bottom=30 if dense else 0)
    return mb, mb.to_object("LogCap" + ("Dense" if dense else ""), uv=not dense, custom_normals=not dense)


# --------------------------------------------------------------------------
# 3. Paint: vertex colour on the dense meshes
# --------------------------------------------------------------------------


def hash01(x):
    return np.mod(np.sin(x * 12.9898 + 78.233) * 43758.5453, 1.0)


def bark_colour(jf, y, piece, nz, length):
    t0 = jf * STEP
    g = np.sin(np.pi * phase(jf))
    c = np.tile(np.array(LOG_BROWN), (len(jf), 1))
    c = mix(c, PLATE, 0.55 * smoothstep(0.70, 1.0, g))
    c = mix(c, TOP, 0.65 * smoothstep(0.45, 0.95, nz))
    # Soft mottling: end-safe terms (periodic, symmetric at y = 0) plus
    # interior-only noise that fades to nothing at the ends.
    m = (0.5 * np.cos(3 * t0) * np.cos(TAU * y / length * (length == 1.0) + 0.7)
         + bump4(y, length) * (0.6 * np.sin(5 * t0 + 2.1 * hash01(len(piece))) * np.cos(9 * y + 1.3)
                               + 0.4 * np.cos(11 * t0 - 1.0) * np.sin(17 * y)))
    c = c * (1 + 0.07 * m)[:, None]
    # Horizontal cracks across some plates -- never near an end.
    seed = {"A": 0.0, "B": 31.7, "CAP": 57.3}[piece]
    k = np.round(jf / COLS_PER_RIDGE)
    h = hash01(k + seed)
    yc = length * (0.18 + 0.64 * hash01(k * 3.7 + seed + 9.1))
    if piece == "CAP":
        yc = 0.08 + 0.22 * hash01(k * 3.7 + seed + 9.1)
    crack = (h > 0.45) * smoothstep(0.016, 0.007, np.abs(y - yc)) * smoothstep(0.55, 0.8, g)
    # second, shorter cracks on the longer pieces
    yc2 = length * (0.18 + 0.64 * hash01(k * 5.1 + seed + 2.3))
    crack2 = (h > 0.75) * (piece != "CAP") * smoothstep(0.014, 0.006, np.abs(y - yc2)) * smoothstep(0.6, 0.85, g)
    c = mix(c, GROOVE, 0.85 * np.maximum(crack, crack2))
    # The grooves, last, so they stay dark everywhere.
    c = mix(c, GROOVE, 0.92 * smoothstep(0.45, 0.08, g))
    return c


def face_colour(P):
    x, z = P[:, 0], P[:, 2] - ZC
    rho = np.hypot(x / HALF_W, z / HALF_H)
    ang = np.arctan2(z, x)
    c = np.tile(np.array(WOOD), (len(P), 1))
    c = mix(c, SAPWOOD, smoothstep(0.70, 0.80, rho))
    rr = rho * 6.2 + 0.05 * np.sin(5 * ang) + 0.03 * np.sin(11 * ang + 1)
    fr = np.abs(np.mod(rr + 0.5, 1.0) - 0.5)
    c = mix(c, RING, (rho < 0.80) * smoothstep(0.11, 0.05, fr))
    c = mix(c, PITH, smoothstep(0.07, 0.04, rho))
    # one cartoon check (radial crack) from the pith toward the rim
    da = np.abs(np.angle(np.exp(1j * (ang - math.radians(52)))))
    c = mix(c, GROOVE, smoothstep(0.035, 0.015, da * rho) * smoothstep(0.12, 0.2, rho) * smoothstep(0.62, 0.5, rho))
    # the bark rim
    c = mix(c, BARK_RIM, smoothstep(0.85, 0.89, rho))
    return c


def albedo(mb, N):
    meta = mb.meta
    P = np.array(mb.co)
    kind = np.array([m["kind"] for m in meta])
    jf = np.array([m["jf"] for m in meta], dtype=float)
    y = np.array([m["y"] for m in meta], dtype=float)
    piece = meta[0]["piece"] if meta[0]["piece"] != "B" else "B"
    c = np.zeros((len(P), 3))

    tube = kind == 0
    length = CAP_LEN if piece == "CAP" else 1.0
    if tube.any():
        c[tube] = bark_colour(jf[tube], y[tube], piece, N[tube, 2], length)
        if piece == "CAP":
            t0 = jf[tube] * STEP
            yy = y[tube]
            up = smoothstep(0.55, 0.9, N[tube, 2])
            along = smoothstep(0.26, 0.36, yy) * smoothstep(0.497, 0.47, yy)
            blob = (0.55 * np.sin(6 * t0 + 1.3) * np.cos(21 * yy + 0.4)
                    + 0.45 * np.cos(10 * t0 - 0.7) * np.sin(33 * yy + 2.0) + 0.35)
            w = up * along * smoothstep(0.40, 0.60, blob)
            c[tube] = mix(c[tube], MOSS, w)
            c[tube] = mix(c[tube], MOSS_DARK, w * smoothstep(0.6, 1.0, blob) * 0.6)
            # darker bark where it rounds over into the sawn face
            c[tube] = mix(c[tube], BARK_RIM, 0.5 * smoothstep(0.46, 0.5, yy) * (1 - w))
        if piece == "B":  # the knot ring round the stub's base
            base, d, side, up = stub_frame()
            v = P[tube] - np.array(base)
            t = v @ np.array(d)
            rr = np.linalg.norm(v - np.outer(t, np.array(d)), axis=1)
            ring = smoothstep(0.28, 0.20, rr) * smoothstep(0.10, 0.15, rr)
            c[tube] = mix(c[tube], GROOVE, 0.85 * ring)
            c[tube] = mix(c[tube], PLATE, 0.6 * smoothstep(0.15, 0.13, rr))
    bot = (kind == 1)
    c[bot] = BOTTOM
    face = kind == 2
    if face.any():
        c[face] = face_colour(P[face])
    sb = kind == 3
    if sb.any():
        a = jf[sb]
        g = np.abs(np.sin(3 * a))
        c[sb] = LOG_BROWN
        c[sb] = mix(c[sb], TOP, 0.5 * smoothstep(0.3, 0.9, N[sb, 2]))
        c[sb] = mix(c[sb], GROOVE, 0.85 * smoothstep(0.25, 0.05, g))
    sf = kind == 4
    if sf.any():
        s = y[sf]
        c[sf] = WOOD
        fr = np.abs(np.mod(s * 3.0 + 0.5, 1.0) - 0.5)
        c[sf] = mix(c[sf], RING, (s < 0.85) * smoothstep(0.12, 0.05, fr))
        c[sf] = mix(c[sf], PITH, smoothstep(0.15, 0.08, s))
        c[sf] = mix(c[sf], BARK_RIM, smoothstep(0.86, 0.92, s))
    return np.clip(c, 0, 1)


def paint(obj, mb):
    me = obj.data
    n = len(me.vertices)
    N = np.empty(n * 3)
    me.vertex_normals.foreach_get("vector", N)
    c = albedo(mb, N.reshape(-1, 3))
    attr = me.color_attributes.new("Albedo", "FLOAT_COLOR", "POINT")
    me.color_attributes.active_color = attr
    lin = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)  # stored linear
    rgba = np.concatenate([lin, np.ones((n, 1))], axis=1)
    attr.data.foreach_set("color", rgba.ravel())


# --------------------------------------------------------------------------
# 4. Bake the dense colour onto the shipped pieces
# --------------------------------------------------------------------------


def activate(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def bark_material(img):
    """The one material: albedo texture straight into base colour.

    Exporters read this as baseColorTexture (glTF) / DiffuseColor (FBX)."""
    mat = bpy.data.materials.new("LogBark")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.8
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.name = "Albedo"
    tex.image = img
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    return mat


def bake_albedo(pairs, out_dir):
    img = bpy.data.images.new("log_albedo", TEXTURE_SIZE, TEXTURE_SIZE, alpha=False)
    img.colorspace_settings.name = "sRGB"
    img.generated_color = (*LOG_BROWN, 1.0)

    src = bpy.data.materials.new("LogBakeSource")
    src.use_nodes = True
    nt = src.node_tree
    col = nt.nodes.new("ShaderNodeVertexColor")
    col.layer_name = "Albedo"
    nt.links.new(col.outputs["Color"], nt.nodes["Principled BSDF"].inputs["Base Color"])

    mat = bark_material(img)
    mat.node_tree.nodes.active = mat.node_tree.nodes["Albedo"]

    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 4
    bake = sc.render.bake
    bake.use_pass_direct = False
    bake.use_pass_indirect = False
    bake.use_pass_color = True
    bake.use_selected_to_active = True
    bake.cage_extrusion = 0.03
    bake.max_ray_distance = 0.08
    bake.margin = 6

    for i, (dense, low) in enumerate(pairs):
        dense.data.materials.clear()
        dense.data.materials.append(src)
        low.data.materials.clear()
        low.data.materials.append(mat)
        bpy.ops.object.select_all(action="DESELECT")
        dense.select_set(True)
        low.select_set(True)
        bpy.context.view_layer.objects.active = low
        bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"}, use_selected_to_active=True,
                            use_clear=(i == 0), margin=6)

    path = os.path.join(out_dir, "log_albedo.png")
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    return mat


# --------------------------------------------------------------------------
# 5. Assemblies
# --------------------------------------------------------------------------


def assembly_plan(length):
    mid = length - 2 * CAP_LEN
    n = max(1, math.floor(mid / BODY_LEN + 0.5))
    return n, mid / n


def assemble(name, length, pieces, location):
    """A root (centre of the length, on the waterline) with linked copies of
    the pieces under it, laid out by the assembly rule in the docstring."""
    root = bpy.data.objects.new(name, None)
    root.empty_display_type = "PLAIN_AXES"
    root.location = location
    bpy.context.scene.collection.objects.link(root)
    n, sy = assembly_plan(length)
    y0 = -length / 2
    kids = []

    def child(src, cname, y, rot=0.0, scale_y=1.0):
        o = bpy.data.objects.new(cname, src.data)  # shares the mesh
        o.parent = root
        o.location = (0, y, 0)
        o.rotation_euler = (0, 0, rot)
        o.scale = (1, scale_y, 1)
        bpy.context.scene.collection.objects.link(o)
        kids.append(o)

    child(pieces["CAP"], f"{name}_CapStart", y0 + CAP_LEN, rot=math.pi)
    for k in range(n):
        v = "AB"[k % 2]
        child(pieces[v], f"{name}_Body{k}_{v}", y0 + CAP_LEN + k * sy, scale_y=sy)
    child(pieces["CAP"], f"{name}_CapEnd", y0 + length - CAP_LEN)
    return root, kids, (n, sy)


# --------------------------------------------------------------------------
# 6. Export
# --------------------------------------------------------------------------


def export(objs, out_dir):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.gltf(
        filepath=os.path.join(out_dir, "log.glb"),
        export_format="GLB",
        use_selection=True,
        export_apply=False,
        export_normals=True,
        export_animations=False,
        export_image_format="AUTO",
        export_yup=True,
    )
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(out_dir, "log.fbx"),
        use_selection=True,
        object_types={"EMPTY", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        use_mesh_modifiers=False,
        mesh_smooth_type="OFF",       # keep the custom normals
        bake_anim=False,
        path_mode="COPY",
        embed_textures=True,
    )


# --------------------------------------------------------------------------
# 7. Previews
# --------------------------------------------------------------------------

FROG_GREEN = rgb("#3E933E")       # FrogColours.FrogGreen
POND_WATER = rgb("#9FD8F2")       # BoardColours.PondWater
LANE = 184 * PX


def srgb_to_linear(c):
    return tuple(((x + 0.055) / 1.055) ** 2.4 if x > 0.04045 else x / 12.92 for x in c[:3]) + (1.0,)


def flat_material(name, srgb, rough=0.6):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = srgb_to_linear(srgb)
    b.inputs["Roughness"].default_value = rough
    return m


def setup_stage(res, samples):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = False
    sc.view_settings.view_transform = "Standard"
    sc.view_settings.look = "None"
    world = bpy.data.worlds.new("PreviewWorld")
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = srgb_to_linear((0.80, 0.90, 0.95))
    bg.inputs[1].default_value = 0.85
    sc.world = world

    sun = bpy.data.lights.new("PreviewSun", "SUN")
    sun.energy = 3.0
    sun.angle = math.radians(30)
    so = bpy.data.objects.new("PreviewSun", sun)
    so.rotation_euler = Euler((math.radians(35), math.radians(-25), math.radians(-35)))
    sc.collection.objects.link(so)

    # The pond: an opaque water plane on the waterline, like the board.
    bpy.ops.mesh.primitive_plane_add(size=400, location=(0, 0, 0))
    water = bpy.context.active_object
    water.name = "PreviewWater"
    water.data.materials.append(flat_material("PreviewWater", POND_WATER, rough=0.35))
    return water


def camera(loc, target, lens=50.0, ortho=None, up_y=False):
    sc = bpy.context.scene
    co = sc.camera
    if co is None:
        co = bpy.data.objects.new("PreviewCam", bpy.data.cameras.new("PreviewCam"))
        sc.collection.objects.link(co)
        sc.camera = co
    co.location = loc
    if ortho:
        co.data.type, co.data.ortho_scale = "ORTHO", ortho
    else:
        co.data.type, co.data.lens = "PERSP", lens
    d = Vector(target) - co.location
    co.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    if up_y:  # straight down, with +Y up the picture (the board's up)
        co.rotation_euler = (0, 0, 0)


def render_to(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def load_frog(path):
    """The frog from frog.glb, frozen in its Sit pose, tinted FrogGreen.
    Returns a static mesh (feet on z = 0, facing -Y)."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    rig = next(o for o in new if o.type == "ARMATURE")
    mesh_obj = next(o for o in new if o.type == "MESH")
    sit = next(a for a in bpy.data.actions if a.name.lower().startswith("sit"))
    ad = rig.animation_data or rig.animation_data_create()
    ad.action = sit
    if hasattr(ad, "action_slot") and ad.action_slot is None and len(sit.slots):
        ad.action_slot = sit.slots[0]
    bpy.context.scene.frame_set(0)
    dg = bpy.context.evaluated_depsgraph_get()
    ev = mesh_obj.evaluated_get(dg)
    me = bpy.data.meshes.new_from_object(ev, preserve_all_data_layers=True, depsgraph=dg)
    me.transform(mesh_obj.matrix_world)
    me.name = "FrogSit"
    zmin = min(v.co.z for v in me.vertices)
    me.transform(Matrix.Translation((0, 0, -zmin)))
    for o in new:
        bpy.data.objects.remove(o)
    for mat in me.materials:
        nt = mat.node_tree
        bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        tex = next(n for n in nt.nodes if n.type == "TEX_IMAGE")
        mul = nt.nodes.new("ShaderNodeMix")
        mul.data_type = "RGBA"
        mul.blend_type = "MULTIPLY"
        mul.inputs["Factor"].default_value = 1.0
        nt.links.new(tex.outputs["Color"], mul.inputs[6])
        mul.inputs[7].default_value = srgb_to_linear(FROG_GREEN)
        nt.links.new(mul.outputs[2], bsdf.inputs["Base Color"])
    return me


def top_z(x, y):
    """Highest log surface below (x, y), by ray cast; None over water."""
    dg = bpy.context.evaluated_depsgraph_get()
    hit, loc, *_ = bpy.context.scene.ray_cast(dg, Vector((x, y, 5)), Vector((0, 0, -1)))
    return loc.z if hit and loc.z > 0.05 else None


FROGS = []


def place_frog(me, x, y, facing_x=True):
    """A frog sitting on the log at (x, y), facing +X -- the way it will hop."""
    z = top_z(x, y)
    o = bpy.data.objects.new(f"PreviewFrog{len(FROGS)}", me)
    bpy.context.scene.collection.objects.link(o)
    o.rotation_euler = (0, 0, math.radians(90) if facing_x else 0)
    o.location = (x, y, (z or 0) - 0.045)  # its feet settle into the round of the crest
    FROGS.append(o)
    return o


def show_only(groups, visible):
    for name, objs in groups.items():
        for o in objs:
            o.hide_render = name not in visible


def render_previews(pieces, shared, player, out_dir, frog_path, samples=48):
    from PIL import Image, ImageDraw, ImageFont

    water = setup_stage((1200, 900), samples)
    sc = bpy.context.scene
    frog = load_frog(frog_path) if frog_path and os.path.exists(frog_path) else None
    shared_root, shared_kids = shared
    player_roots = player

    # Previews put things where they need them; the export is already written.
    for p in pieces.values():
        p.hide_render = True
        p.location = (-40, 0, 0)
    shared_root.location = (0, 0, 0)
    for r, _ in player_roots:
        r.location = (40, 0, 0)  # out of the way
    bpy.context.view_layer.update()

    lanes4 = [(-1.5 + i) * LANE for i in range(4)]

    # -- hero 3/4: the shared log, a frog on it for scale
    if frog:
        place_frog(frog, 0.0, -2.0)
    camera((5.2, -8.2, 4.4), (0.0, -0.9, 0.35), lens=40)
    render_to(os.path.join(out_dir, "log_hero_3q.png"))

    # -- top-down at board scale (112 px a unit, so the log is 176 px wide)
    for f in FROGS:
        bpy.data.objects.remove(f)
    FROGS.clear()
    if frog:
        for y in lanes4:
            place_frog(frog, 0.0, y)
    W, H = 6.0, 9.5
    sc.render.resolution_x, sc.render.resolution_y = int(W * 112), int(H * 112)
    camera((0, 0, 20), (0, 0, 0), ortho=H, up_y=True)
    render_to(os.path.join(out_dir, "log_topdown_board.png"))

    # -- four player logs as four lanes: top-down at board scale + a 3/4 view
    for f in FROGS:
        bpy.data.objects.remove(f)
    FROGS.clear()
    shared_root.location = (40, 40, 0)
    for (r, _), y in zip(player_roots, lanes4):
        r.location = (0, y, 0)
    bpy.context.view_layer.update()
    if frog:
        for y in lanes4:
            place_frog(frog, 0.0, y)
    camera((0, 0, 20), (0, 0, 0), ortho=H, up_y=True)
    a = os.path.join(out_dir, "_player_top.png")
    render_to(a)
    sc.render.resolution_x, sc.render.resolution_y = 1064, 1064
    camera((5.6, -7.0, 5.0), (0.0, -0.4, 0.3), lens=40)
    b = os.path.join(out_dir, "_player_3q.png")
    render_to(b)
    ia, ib = Image.open(a), Image.open(b)
    strip = Image.new("RGB", (ia.width + ib.width, max(ia.height, ib.height)), "white")
    strip.paste(ia, (0, 0))
    strip.paste(ib, (ia.width, 0))
    strip.save(os.path.join(out_dir, "log_player_logs.png"))
    os.remove(a)
    os.remove(b)

    # -- the pieces, laid out separately, from above the water line
    for f in FROGS:
        bpy.data.objects.remove(f)
    FROGS.clear()
    for r, _ in player_roots:
        r.location = (40, 40, 0)
    water.hide_render = True
    floor = bpy.data.objects.new("PreviewFloor", water.data.copy())
    floor.data.materials.clear()
    floor.data.materials.append(flat_material("PreviewFloor", (0.86, 0.88, 0.86), rough=0.9))
    floor.location = (0, 0, CUT_Z - 0.002)
    sc.collection.objects.link(floor)
    # An exploded log: start cap (the cap turned 180 deg), A, B, end cap.
    gap = 0.45
    y = -(2 * CAP_LEN + 2 * BODY_LEN + 3 * gap) / 2
    cap2 = bpy.data.objects.new("PreviewCapStart", pieces["CAP"].data)
    sc.collection.objects.link(cap2)
    row = [(cap2, y + CAP_LEN, math.pi, CAP_LEN, "LogCap", "start: turned 180\u00b0")]
    y += CAP_LEN + gap
    row.append((pieces["A"], y, 0.0, BODY_LEN, "LogBody_A", "1.0 long, tiles"))
    y += BODY_LEN + gap
    row.append((pieces["B"], y, 0.0, BODY_LEN, "LogBody_B", "1.0, branch stub"))
    y += BODY_LEN + gap
    row.append((pieces["CAP"], y, 0.0, CAP_LEN, "LogCap", "end: 0.5 long"))
    for o, oy, rot, _, _, _ in row:
        o.location = (0, oy, 0)
        o.rotation_euler = (0, 0, rot)
        o.hide_render = False
    sc.render.resolution_x, sc.render.resolution_y = 1600, 900
    camera((7.4, -3.2, 4.4), (0.0, 0.15, 0.2), lens=40)
    p = os.path.join(out_dir, "log_pieces.png")
    render_to(p)
    # Label each piece above its middle.
    from bpy_extras.object_utils import world_to_camera_view
    im = Image.open(p).convert("RGB")
    dr = ImageDraw.Draw(im)
    try:
        font = ImageFont.load_default(size=32)
        small = ImageFont.load_default(size=24)
    except TypeError:
        font = small = ImageFont.load_default()
    for o, oy, rot, length, name, note in row:
        mid = oy + (-length / 2 if rot else length / 2)
        v = world_to_camera_view(sc, sc.camera, Vector((0, mid, 1.45)))
        px, py = v.x * im.width, (1 - v.y) * im.height
        for text, f, dy in ((name, font, -40), (note, small, -4)):
            tw = dr.textlength(text, font=f)
            dr.text((px - tw / 2, py + dy), text, fill=(30, 36, 34), font=f)
    im.save(p)


# --------------------------------------------------------------------------

_T0 = time.time()


def say(msg):
    print(f"[log {time.time() - _T0:6.1f}s] {msg}", flush=True)


def stats(objs):
    out = {}
    for o in objs:
        me = o.data
        ys = [v.co.y for v in me.vertices]
        out[o.name] = dict(tris=sum(len(p.vertices) - 2 for p in me.polygons), verts=len(me.vertices),
                           y=(round(min(ys), 4), round(max(ys), 4)),
                           x=(round(min(v.co.x for v in me.vertices), 3), round(max(v.co.x for v in me.vertices), 3)),
                           z=(round(min(v.co.z for v in me.vertices), 3), round(max(v.co.z for v in me.vertices), 3)))
    return out


def main(argv):
    here = os.path.dirname(os.path.abspath(__file__))
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--no-render", action="store_true")
    ap.add_argument("--samples", type=int, default=48)
    ap.add_argument("--frog", default=os.path.join(here, "..", "frog-3d", "frog.glb"),
                    help="frog.glb, for the previews (default: ../frog-3d/frog.glb)")
    args = ap.parse_args(argv)
    out_dir = os.path.abspath(args.out)
    os.makedirs(out_dir, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    pieces, pairs = {}, []
    for key, build in (("A", lambda d: build_body("A", d)), ("B", lambda d: build_body("B", d)),
                       ("CAP", build_cap)):
        mb_lo, lo = build(False)
        mb_hi, hi = build(True)
        paint(hi, mb_hi)
        limit = CAP_TRIS if key == "CAP" else BODY_TRIS
        assert mb_lo.tris() <= limit, f"{lo.name}: {mb_lo.tris()} tris > {limit}"
        pieces[key] = lo
        pairs.append((hi, lo))
        say(f"{lo.name}: {mb_lo.tris()} tris (source {mb_hi.tris()})")
    bake_albedo(pairs, out_dir)
    say("baked")
    for hi, _ in pairs:
        bpy.data.objects.remove(hi)

    # Pieces side by side in the file (their meshes' own origins are what count).
    pieces["A"].location = (0, 0, 0)
    pieces["B"].location = (2.5, 0, 0)
    pieces["CAP"].location = (5.0, 0, 0)
    shared_root, shared_kids, plan_s = assemble("SharedLog", SHARED_LOG_LEN, pieces, (-5.0, 0, 0))
    player_root, player_kids, plan_p = assemble("PlayerLog", PLAYER_LOG_LEN, pieces, (-2.5, 0, 0))
    say(f"SharedLog {SHARED_LOG_LEN:.3f}: n={plan_s[0]} sy={plan_s[1]:.3f}; "
        f"PlayerLog {PLAYER_LOG_LEN:.3f}: n={plan_p[0]} sy={plan_p[1]:.3f}")
    export(list(pieces.values()) + [shared_root, player_root] + shared_kids + player_kids, out_dir)
    say("exported")
    print("LOG_STATS", stats(pieces.values()))

    if not args.no_render:
        # three more player logs for the four-lane preview
        players = [(player_root, player_kids)]
        for i in range(3):
            r, k, _ = assemble(f"PreviewPlayerLog{i}", PLAYER_LOG_LEN, pieces, (40, 0, 0))
            players.append((r, k))
        render_previews(pieces, (shared_root, shared_kids), players, out_dir, os.path.abspath(args.frog),
                        args.samples)
        say("rendered")


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    main(argv)
