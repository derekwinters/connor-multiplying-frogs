"""Multiplying Frogs -- the 3D lily pad, modelled headless in Blender.

Re-runnable, in the same way as ../frog-3d/frog_model.py: builds the pads from
nothing and writes lilypad.glb, lilypad.fbx, lilypad_albedo.png and preview
PNGs. Nothing is touched by hand in a Blender UI; change the numbers here and
run it again.

    python3 -m venv venv && venv/bin/pip install bpy pillow   # Blender 5.x as a module
    venv/bin/python lilypad_model.py --out <dir>               # model, exports, previews
    venv/bin/python lilypad_model.py --out <dir> --no-render   # model and exports only
    venv/bin/python lilypad_model.py --out <dir> --frog ../frog-3d/frog.glb   # frog in the previews

What it follows (docs/specs/ui/game-board.md, "The lily pad is notched,
veined, and varies per pad"):

* A pad is a circle with a wedge cut from it. The wedge's two rim ends sit
  half the notch width either side of the direction the pad points; its apex
  is LilyPadNotchDepth (0.15) of the radius out from the centre, towards the
  notch.
* There are FOUR pads, one per entry in LilyPadNotchAngles (10, 15, 20, 25
  degrees). The spec's twelve-entry variation table only rotates them, and
  rotation is free at runtime, so the game rotates these four -- exactly what
  the spec says real art should do.
* Every pad points its notch along +X (Blender), which is the spec's 0 deg,
  "points right along the lane". The game turns it by the table's angle.
* Five veins radiate from the circle's centre (not the notch apex),
  symmetric about the notch's axis, spaced (360 - notch) / 6 apart, each
  stopped short by LilyPadVeinInset at the centre and LilyPadVeinOutset at the
  rim. They are drawn in LilyPadEdge at LilyPadVeinOpacity (0.5).
* Fill is LilyPadGreen #B2E67F; the rim is LilyPadEdge #6E9E4A.

Scale: one unit is about one frog length, as for the frog. The sitting frog
is ~1.38 units across its splayed back feet, and on the board the 88 px frog
piece sits on a 112 px pad, so the pad is about 1.38 * 112 / 88 ~= 1.76
units across; seen from above the splayed feet overhang a little, so it is
trimmed to 1.64 (PAD_RADIUS 0.82), where the frog covers the pad about as
much as the 88 px piece covers the 112 px pad. The top of the pad's flat middle is z = 0, so the
frog (whose feet rest on z = 0) sits on it with no offset.

Conventions (Blender space): Z is up, the pad's centre is the origin. The
glTF export converts to Y-up; the FBX is written -Z forward, Y up, like the
frog.

The four pads share ONE material and ONE texture (a 2 x 2 atlas, one quadrant
per notch width), so a board of 28 pads is one material for the renderer.
The albedo is the pad's real colour -- unlike the frog, pads are not tinted
per player.
"""

import argparse
import math
import os
import sys
import time

import bpy  # noqa: E402
import bmesh
import numpy as np
from mathutils import Euler, Vector

# --------------------------------------------------------------------------
# Spec constants (game-board.md, named constants)
# --------------------------------------------------------------------------

NOTCH_ANGLES = (10, 15, 20, 25)   # LilyPadNotchAngles, degrees
NOTCH_DEPTH = 0.15                # LilyPadNotchDepth, apex distance / radius
VEIN_COUNT = 5                    # LilyPadVeinCount
VEIN_INSET = 0.20                 # LilyPadVeinInset, fraction of radius
VEIN_OUTSET = 0.12                # LilyPadVeinOutset, fraction of radius
VEIN_WIDTH_PX = 2.5               # LilyPadVeinWidth, on a 112 px pad
VEIN_OPACITY = 0.5                # LilyPadVeinOpacity
OUTLINE_PX = 3.0                  # TrackOutline, on a 112 px pad
DIAMETER_PX = 112.0               # LilyPadDiameter

LILYPAD_GREEN = (0xB2, 0xE6, 0x7F)  # LilyPadGreen
LILYPAD_EDGE = (0x6E, 0x9E, 0x4A)   # LilyPadEdge

# --------------------------------------------------------------------------
# Shape (ours: the spec draws a flat disc; this is how it becomes a solid)
# --------------------------------------------------------------------------

PAD_RADIUS = 0.82        # 1.64 units across -- see "Scale" above
THICKNESS = 0.035        # underside sits this far below the flat top
RIM_LIFT = 0.030         # the outer edge curls up by this much ...
RIM_LIFT_START = 0.72    # ... starting at this fraction of the radius
RIM_WAVE = 0.010         # a gentle ripple along the rim, so it is not a coin
RIM_WAVE_COUNT = 5
NOTCH_LIP_LIFT = 0.012   # the cut edges lift slightly too, like a real leaf
DOME = 0.006             # the middle bulges up a hair (frog still sits ~z=0)

RIM_SEGMENTS = 72        # around the leaf
RINGS = 8                # from notch apex out to the rim
TEXTURE_SIZE = 2048      # lilypad_albedo.png, 2 x 2 atlas of 1024 quadrants
QUAD = TEXTURE_SIZE // 2
UV_MARGIN = 0.04         # each pad's disc sits inside its quadrant with this margin


def log(msg, _t0=time.time()):
    print(f"[lilypad {time.time() - _t0:6.1f}s] {msg}", flush=True)


def srgb_to_linear(c):
    return tuple(((x + 0.055) / 1.055) ** 2.4 if x > 0.04045 else x / 12.92 for x in c[:3]) + (1.0,)


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


# --------------------------------------------------------------------------
# 1. Geometry: the leaf outline, swept as rays out of the notch apex
# --------------------------------------------------------------------------
#
# The leaf (circle minus wedge) is star-shaped from the notch apex: every ray
# from the apex to a rim point between the notch's two rim ends stays inside
# it, and the two extreme rays ARE the notch's cut edges. So one (rim angle,
# t) grid covers the leaf exactly, with the cut edges falling on grid lines.

def leaf_frame(notch_deg):
    half = math.radians(notch_deg) / 2
    apex = np.array([NOTCH_DEPTH * PAD_RADIUS, 0.0])
    thetas = np.linspace(half, 2 * math.pi - half, RIM_SEGMENTS + 1)
    return apex, thetas


def height(x, y, notch_deg):
    """Top-surface z at (x, y). Flat middle at ~0, curling rim, wavy edge."""
    r = math.hypot(x, y) / PAD_RADIUS
    th = math.atan2(y, x)
    z = DOME * (1 - min(r, 1) ** 2)
    lift = float(smoothstep(RIM_LIFT_START, 1.0, r))
    z += RIM_LIFT * lift ** 1.5
    z += RIM_WAVE * lift * math.sin(RIM_WAVE_COUNT * th + 0.7)
    # lift near the cut edges: distance in angle from the notch axis
    half = math.radians(notch_deg) / 2
    ang = abs(math.atan2(y, x - NOTCH_DEPTH * PAD_RADIUS))
    near_cut = float(smoothstep(half + 0.35, half, ang)) * float(smoothstep(0.1, 0.5, r))
    z += NOTCH_LIP_LIFT * near_cut
    return z


def uv_of(x, y, quadrant):
    """Planar, top-down UV into this pad's quadrant of the atlas."""
    qx, qy = quadrant % 2, quadrant // 2
    s = (1 - 2 * UV_MARGIN) / 2  # disc diameter in UV units within the quadrant
    u = (qx + UV_MARGIN + (x / PAD_RADIUS + 1) / 2 * (1 - 2 * UV_MARGIN)) / 2
    v = (qy + UV_MARGIN + (y / PAD_RADIUS + 1) / 2 * (1 - 2 * UV_MARGIN)) / 2
    return u, v


def build_pad(notch_deg, quadrant, material):
    apex, thetas = leaf_frame(notch_deg)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")

    # top grid: rings x rim segments, t from apex (0) to rim (1)
    ts = np.linspace(0.0, 1.0, RINGS + 1) ** 0.85
    top = []
    for t in ts:
        row = []
        for th in thetas:
            rim = np.array([math.cos(th), math.sin(th)]) * PAD_RADIUS
            x, y = apex + (rim - apex) * t
            row.append(bm.verts.new((x, y, height(x, y, notch_deg))))
        top.append(row)
    # bottom: the same outline pushed down, slightly flatter
    bot = []
    for i, t in enumerate(ts):
        row = []
        for j, th in enumerate(thetas):
            v = top[i][j].co
            row.append(bm.verts.new((v.x, v.y, v.z - THICKNESS * (0.55 + 0.45 * (1 - t)))))
        bot.append(row)

    def face(vs, flip=False):
        f = bm.faces.new(vs[::-1] if flip else vs)
        f.material_index = 0
        f.smooth = True
        return f

    n_r, n_s = len(ts), len(thetas)
    for i in range(n_r - 1):
        for j in range(n_s - 1):
            face([top[i][j], top[i + 1][j], top[i + 1][j + 1], top[i][j + 1]], flip=True)
            face([bot[i][j], bot[i + 1][j], bot[i + 1][j + 1], bot[i][j + 1]])
    # outer rim band
    for j in range(n_s - 1):
        face([top[-1][j], top[-1][j + 1], bot[-1][j + 1], bot[-1][j]], flip=True)
    # the two cut edges of the notch
    for i in range(n_r - 1):
        face([top[i][0], top[i + 1][0], bot[i + 1][0], bot[i][0]])
        face([top[i][-1], top[i + 1][-1], bot[i + 1][-1], bot[i][-1]], flip=True)

    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)  # apex row collapses to one point
    bm.normal_update()
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

    # UVs: planar from above. Side walls (rim and cut) map to the outline
    # band of the texture, which is painted LilyPadEdge right out past the
    # disc, so they read as the rim.
    for f in bm.faces:
        for loop in f.loops:
            co = loop.vert.co
            loop[uv].uv = uv_of(co.x, co.y, quadrant)

    name = f"LilyPad_Notch{notch_deg:02d}"
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(material)
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    # Smooth shading for the surfaces, but a hard edge where top meets side.
    obj.modifiers.new("EdgeSplit", "EDGE_SPLIT").split_angle = math.radians(50)
    activate(obj)
    bpy.ops.object.modifier_apply(modifier="EdgeSplit")
    return obj


def activate(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


# --------------------------------------------------------------------------
# 2. The albedo: the spec's flat drawing, painted straight into UV space
# --------------------------------------------------------------------------

def vein_angles(notch_deg):
    """Five veins, symmetric about the notch axis; with the notch they split
    the circle into six equal parts (game-board.md)."""
    step = (360.0 - notch_deg) / (VEIN_COUNT + 1)
    return [notch_deg / 2 + step * k for k in range(1, VEIN_COUNT + 1)]


def paint_quadrant(notch_deg):
    n = QUAD
    px = np.arange(n) + 0.5
    # quadrant pixel -> pad-space (units of radius); v grows upward in UV,
    # image rows grow downward, so flip.
    span = (1 - 2 * UV_MARGIN)
    X = ((px / n - UV_MARGIN) / span) * 2 - 1
    Y = X.copy()
    XX, YY = np.meshgrid(X, Y[::-1])
    R = np.hypot(XX, YY)
    TH = np.degrees(np.arctan2(YY, XX)) % 360

    px_per_r = (n * span) / 2           # texture pixels per pad radius
    unit_per_px = 2 / DIAMETER_PX       # spec pixels -> radius units
    aa = 1.0 / px_per_r                 # one texture pixel, in radius units

    # signed distance (radius units) to the leaf's outline, positive inside
    d_rim = 1 - R
    half = math.radians(notch_deg) / 2
    ax = NOTCH_DEPTH
    # the two cut edges: rays from the apex (ax, 0) through (cos half, +-sin half)
    in_wedge = np.ones_like(R, dtype=bool)
    d_cut = np.full_like(R, np.inf)
    for s in (+1, -1):
        ex, ey = math.cos(half) - ax, s * math.sin(half)
        L = math.hypot(ex, ey)
        ex, ey = ex / L, ey / L
        side = -s * ((XX - ax) * ey - YY * ex)   # > 0 on the leaf side of this cut
        in_wedge &= side < 0
        t = np.clip((XX - ax) * ex + YY * ey, 0, None)
        d_cut = np.minimum(d_cut, np.hypot(XX - ax - t * ex, YY - t * ey))
    sd = np.minimum(d_rim, np.where(in_wedge, -d_cut, d_cut))

    green = np.array(LILYPAD_GREEN, float) / 255
    edge = np.array(LILYPAD_EDGE, float) / 255

    # fill: LilyPadGreen, a touch lighter in the middle and deeper toward the
    # rim, so it reads as a leaf under light without leaving the spec's hue.
    shade = 1.04 - 0.10 * smoothstep(0.2, 1.0, R)
    img = green[None, None, :] * shade[..., None]

    # veins
    vein_half = VEIN_WIDTH_PX * unit_per_px / 2
    vein_a = np.zeros_like(R)
    for a in vein_angles(notch_deg):
        dx, dy = math.cos(math.radians(a)), math.sin(math.radians(a))
        along = XX * dx + YY * dy
        perp = np.abs(XX * dy - YY * dx)
        t0, t1 = VEIN_INSET, 1 - VEIN_OUTSET
        # rounded caps: distance to the segment
        cl = np.clip(along, t0, t1)
        dist = np.hypot(XX - cl * dx, YY - cl * dy)
        # taper: full width near the middle, thinner toward the rim
        w = vein_half * (1.15 - 0.35 * np.clip((along - t0) / (t1 - t0), 0, 1))
        a_v = smoothstep(w + aa, w - aa, dist)
        vein_a = np.maximum(vein_a, a_v)
    a = vein_a * VEIN_OPACITY
    img = img * (1 - a[..., None]) + edge[None, None, :] * a[..., None]

    # outline: TrackOutline, inside the shape's edge; everything outside the
    # shape is the edge colour too, so the side walls and any mip bleed are rim.
    outline = OUTLINE_PX * unit_per_px
    o = smoothstep(outline + aa, outline - aa, sd)
    img = img * (1 - o[..., None]) + edge[None, None, :] * o[..., None]
    return np.clip(img, 0, 1)


def build_albedo(out_dir):
    from PIL import Image

    atlas = np.zeros((TEXTURE_SIZE, TEXTURE_SIZE, 3))
    for q, notch in enumerate(NOTCH_ANGLES):
        qx, qy = q % 2, q // 2
        # UV v=0 is the image's bottom row
        r0 = TEXTURE_SIZE - (qy + 1) * QUAD
        atlas[r0:r0 + QUAD, qx * QUAD:(qx + 1) * QUAD] = paint_quadrant(notch)
    path = os.path.join(out_dir, "lilypad_albedo.png")
    Image.fromarray((atlas * 255 + 0.5).astype(np.uint8)).save(path, optimize=True)
    img = bpy.data.images.load(path)
    img.colorspace_settings.name = "sRGB"
    return img


def pad_material(img):
    mat = bpy.data.materials.new("LilyPad")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.6
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.name = "Albedo"
    tex.image = img
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    return mat


# --------------------------------------------------------------------------
# 3. Export
# --------------------------------------------------------------------------

def export(pads, out_dir):
    bpy.ops.object.select_all(action="DESELECT")
    for p in pads:
        p.select_set(True)
    bpy.context.view_layer.objects.active = pads[0]
    bpy.ops.export_scene.gltf(
        filepath=os.path.join(out_dir, "lilypad.glb"),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_animations=False,
        export_image_format="AUTO",
        export_yup=True,
    )
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(out_dir, "lilypad.fbx"),
        use_selection=True,
        object_types={"MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        use_mesh_modifiers=True,
        bake_anim=False,
        path_mode="COPY",
        embed_textures=True,
    )


# --------------------------------------------------------------------------
# 4. Previews
# --------------------------------------------------------------------------

# The spec's variation table: index -> (notch, points at). 0 deg points right
# along the lane, 90 deg points DOWN the screen (SVG convention).
VARIATION = [(20, 14), (10, 212), (25, 96), (15, 308), (20, 175), (25, 47),
             (10, 260), (15, 131), (25, 341), (15, 78), (20, 238), (10, 158)]


def setup_stage(res, samples):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.view_settings.view_transform = "Standard"
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
    so.rotation_euler = Euler((math.radians(40), math.radians(-20), math.radians(-35)))
    sc.collection.objects.link(so)
    # pond water
    bpy.ops.mesh.primitive_plane_add(size=60, location=(0, 0, -0.03))
    water = bpy.context.active_object
    wm = bpy.data.materials.new("Water")
    wm.use_nodes = True
    b = wm.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = srgb_to_linear((0x8F / 255, 0xC9 / 255, 0xD9 / 255))
    b.inputs["Roughness"].default_value = 0.35
    water.data.materials.append(wm)


def camera(loc, target, lens=50.0, ortho=None):
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


def render_to(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def place(pad, x, y, points_at_svg):
    """Instance a pad at (x, y), turned so its notch points where the spec's
    SVG angle says (90 = down the screen, which is -Y seen from above)."""
    o = bpy.data.objects.new(pad.name + "_inst", pad.data)
    bpy.context.scene.collection.objects.link(o)
    o.location = (x, y, 0)
    o.rotation_euler = (0, 0, math.radians(-points_at_svg))
    return o


def import_frog(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    rig = next((o for o in new if o.type == "ARMATURE"), None)
    sit = bpy.data.actions.get("Sit")
    if rig and sit:
        rig.animation_data_create()
        rig.animation_data.action = sit
        if hasattr(rig.animation_data, "action_slot") and sit.slots:
            rig.animation_data.action_slot = sit.slots[0]
    # tint it FrogGreen for the preview
    for o in new:
        if o.type == "MESH":
            for m in o.data.materials:
                nt = m.node_tree
                bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
                tex = next((n for n in nt.nodes if n.type == "TEX_IMAGE"), None)
                if tex:
                    mul = nt.nodes.new("ShaderNodeMix")
                    mul.data_type, mul.blend_type = "RGBA", "MULTIPLY"
                    mul.inputs["Factor"].default_value = 1.0
                    nt.links.new(tex.outputs["Color"], mul.inputs[6])
                    mul.inputs[7].default_value = srgb_to_linear((0x3E / 255, 0x93 / 255, 0x3E / 255))
                    nt.links.new(mul.outputs[2], bsdf.inputs["Base Color"])
    roots = [o for o in new if o.parent is None]
    for r in roots:
        r.rotation_mode = "XYZ"
    return roots


def render_previews(pads, out_dir, samples, frog_path):
    setup_stage((1600, 1000), samples)
    sc = bpy.context.scene
    for p in pads:
        p.hide_render = True

    # (a) the four pads, notch pointing right, seen at three-quarters
    gap = PAD_RADIUS * 2.35
    for i, p in enumerate(pads):
        place(p, (i - 1.5) * gap, 0, 0)
    camera((0.0, -6.4, 3.3), (0.0, 0.0, -0.1), lens=46)
    render_to(os.path.join(out_dir, "lilypad_four_3q.png"))

    # (b) the same four, straight down, flat-lit like the board
    camera((0.0, 0.0, 10.0), (0.0, 0.0, 0.0), ortho=gap * 4.1)
    sc.render.resolution_x, sc.render.resolution_y = 1600, 520
    render_to(os.path.join(out_dir, "lilypad_four_top.png"))
    for o in [o for o in sc.objects if o.name.endswith("_inst")]:
        bpy.data.objects.remove(o)

    # (c) a lane: seven pads, lane 0 positions 1..7 through the variation table,
    #     spaced like the board (112 px pad, 48 px gap)
    by_notch = {int(p.name[-2:]): p for p in pads}
    pitch = PAD_RADIUS * 2 * (112 + 48) / 112
    for pos in range(1, 8):
        notch, points = VARIATION[(0 * 5 + pos) % 12]
        place(by_notch[notch], (pos - 4) * pitch, 0, points)
    frog_roots = []
    if frog_path and os.path.exists(frog_path):
        frog_roots = import_frog(frog_path)
        for r in frog_roots:
            r.location = (-3 * pitch, 0, 0)
            r.rotation_euler = (0, 0, math.radians(90))  # face along the lane, +X
        sc.frame_set(0)
    sc.render.resolution_x, sc.render.resolution_y = 1600, 560
    camera((0.0, 0.0, 12.0), (0.0, 0.0, 0.0), ortho=pitch * 7.4)
    render_to(os.path.join(out_dir, "lilypad_lane_top.png"))

    # (d) hero: frog sitting on a pad, three-quarters, for scale
    for o in [o for o in sc.objects if o.name.endswith("_inst")]:
        bpy.data.objects.remove(o)
    place(by_notch[20], 0, 0, 300)
    for r in frog_roots:
        r.location = (0, 0, 0)
        r.rotation_euler = (0, 0, math.radians(20))
    sc.render.resolution_x, sc.render.resolution_y = 1200, 900
    camera((2.3, -2.9, 1.9), (0.0, 0.0, 0.25), lens=50)
    render_to(os.path.join(out_dir, "lilypad_hero_3q.png"))


def stats(pads):
    return {p.name: sum(len(f.vertices) - 2 for f in p.data.polygons) for p in pads}


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--no-render", action="store_true")
    ap.add_argument("--samples", type=int, default=48)
    ap.add_argument("--frog", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "frog-3d", "frog.glb"))
    args = ap.parse_args(argv)
    out_dir = os.path.abspath(args.out)
    os.makedirs(out_dir, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    img = build_albedo(out_dir)
    log("albedo painted")
    mat = pad_material(img)
    pads = [build_pad(n, q, mat) for q, n in enumerate(NOTCH_ANGLES)]
    log("pads built")
    export(pads, out_dir)
    log("exported")
    print("LILYPAD_TRIS", stats(pads))
    if not args.no_render:
        render_previews(pads, out_dir, args.samples, args.frog)
        log("previews rendered")


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    main(argv)
