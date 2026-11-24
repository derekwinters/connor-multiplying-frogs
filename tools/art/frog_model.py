"""Multiplying Frogs -- the 3D frog piece, modelled headless in Blender.

Re-runnable: builds the frog from nothing, rigs it, animates a hop, and
writes frog.fbx, frog.glb, frog_albedo.png and preview PNGs. Nothing is
touched by hand in a Blender UI; change the numbers here and run it again.

    python3 -m venv venv && venv/bin/pip install bpy pillow # Blender 5.x as a module
    venv/bin/python frog_model.py --out <dir>               # model, exports, previews
    venv/bin/python frog_model.py --out <dir> --no-render   # model and exports only

Conventions (Blender space): the frog faces -Y, Z is up, +X is the frog's
left (".L" bones). One unit is about one frog length; the feet rest on z = 0.

How the mesh is made -- one continuous surface, not a pile of primitives:

1. Metaballs (ellipsoids, capsules, balls) are blended into a single organic
   field -- body, head, jaw, eye bulges, haunches, legs, toes and the webs
   between them all melt into each other.
2. That field is meshed, voxel-remeshed so it is closed and manifold, and
   smoothed with a Subdivision Surface (applied).
3. Detail -- belly, back spots, the wide mouth line, nostrils, pupils with a
   catch-light -- is painted as vertex colour on that dense mesh.
4. A copy is decimated to a mobile budget (MESH_TRIS), UV-unwrapped, and the
   dense mesh's colour is baked onto it as a small texture (frog_albedo.png).

The albedo is white wherever the frog is "its colour" and darker everywhere
else -- never lighter. The game tints the frog per player by multiplying one
colour, so white becomes exactly FrogGreen / FrogBlue / FrogOrange / FrogPink
and shading only ever darkens it (game-board.md, the frog-colour invariants).

The rig is an armature (root, spine, and hip/knee/ankle -- thigh, shin, foot
-- for each leg) bound with automatic weights. The bind pose holds the legs
open so the knees can fold without the mesh fusing; the sitting frog is the
"Sit" clip, and the "Hop" clip runs sit -> push -> tuck -> reach -> sit over
0.4 s as authored, with its key poses at the progress points of
game-board.md's "The legs through a hop" table. The board samples it by
progress and stretches it over FrogHopDuration, so the two need not match.
"""

import argparse
import math
import os
import sys

import bpy  # noqa: E402  (bmesh needs bpy imported first)
import bmesh
import numpy as np
from mathutils import Euler, Matrix, Vector

# --------------------------------------------------------------------------
# Shape. Points are (x, y, z) for the frog's LEFT side; the right side is the
# mirror image. Radii are the surface radii wanted.
# --------------------------------------------------------------------------

MB_SURFACE = 0.574  # a lone metaball of radius 1 has its surface at ~0.574

BODY = dict(co=(0.0, 0.12, 0.33), radii=(0.29, 0.37, 0.23), tilt=-24)
HEAD = dict(co=(0.0, -0.20, 0.47), radii=(0.33, 0.26, 0.19), tilt=-4)
JAW = dict(co=(0.0, -0.25, 0.39), radii=(0.31, 0.25, 0.13), tilt=0)
EYE = dict(co=(0.175, -0.24, 0.66), r=0.135)
BROW = dict(co=(0.0, -0.22, 0.60), radii=(0.16, 0.12, 0.08))  # fills the valley between the eyes
HAUNCH = dict(co=(0.19, 0.28, 0.25), r=0.14)

# Bind pose: legs held open so no two segments touch -- that is what lets one
# blended mesh bend at the knee without fusing. The sitting frog is a pose.
BACK_LEG = dict(
    hip=(0.22, 0.28, 0.25),
    knee=(0.55, 0.12, 0.19),
    ankle=(0.62, 0.50, 0.10),
    toe=(0.82, 0.82, 0.04),
    r_thigh=(0.12, 0.068),
    r_shin=(0.068, 0.05),
    toes=4, toe_r=0.03, spread=80, web=0.085,
)
FRONT_LEG = dict(
    hip=(0.19, -0.16, 0.30),
    knee=(0.30, -0.24, 0.16),
    ankle=(0.33, -0.32, 0.05),
    toe=(0.42, -0.50, 0.03),
    r_thigh=(0.065, 0.05),
    r_shin=(0.05, 0.042),
    toes=3, toe_r=0.026, spread=80, web=0.06,
)

MESH_TRIS = 6000        # the shipped mesh's budget, after every modifier
TEXTURE_SIZE = 1024     # frog_albedo.png
DENSE_VOXEL = 0.012     # remesh resolution of the detail (bake source) mesh

# Albedo -- multiplied by the player's frog colour in game.
WHITE = (1.0, 1.0, 1.0)
BELLY = (0.93, 0.93, 0.80)
SPOT = (0.72, 0.76, 0.66)
INK = (0.12, 0.14, 0.13)          # mouth line, nostrils, pupils: near BoardInk
PUPIL_LOOK = Vector((0.55, -0.80, 0.25)).normalized()


def mirror(p):
    return (-p[0], p[1], p[2])


def sides(p):
    return (p, mirror(p))


# --------------------------------------------------------------------------
# 1. The blended field
# --------------------------------------------------------------------------


class Blob:
    def __init__(self, resolution):
        self.mb = bpy.data.metaballs.new("FrogBlob")
        self.mb.resolution = resolution
        self.mb.render_resolution = resolution
        self.mb.threshold = 0.6

    def ellipsoid(self, co, radii, tilt_deg=0.0, rotation=None):
        e = self.mb.elements.new(type="ELLIPSOID")
        e.co = co
        m = max(radii)
        e.radius = m / MB_SURFACE
        e.size_x, e.size_y, e.size_z = (r / m for r in radii)  # scale factors
        e.rotation = rotation or Euler((math.radians(tilt_deg), 0, 0)).to_quaternion()
        return e

    def ball(self, co, r):
        e = self.mb.elements.new(type="BALL")
        e.co = co
        e.radius = r / MB_SURFACE
        return e

    def capsule(self, a, b, r):
        a, b = Vector(a), Vector(b)
        d = b - a
        e = self.mb.elements.new(type="CAPSULE")
        e.co = (a + b) / 2
        e.radius = r / MB_SURFACE
        e.size_x = d.length / 2  # a capsule's half-length, in object units
        e.rotation = Vector((1, 0, 0)).rotation_difference(d.normalized())
        return e

    def limb(self, a, b, r_thick, r_thin):
        """A segment: a capsule of the thin radius, swollen by a muscle near a."""
        a, b = Vector(a), Vector(b)
        self.capsule(a, b, r_thin)
        if r_thick > r_thin:
            d = b - a
            rot = Vector((1, 0, 0)).rotation_difference(d.normalized())
            along = min(r_thick * 1.6, d.length * 0.5)
            self.ellipsoid(tuple(a.lerp(b, 0.38)), (along, r_thick, r_thick * 0.92), rotation=rot)


def foot(blob, ankle, toe, leg):
    """Toes fanned flat on the ground from a pad below the ankle, webbed."""
    ankle, toe = Vector(ankle), Vector(toe)
    d = toe - ankle
    flat = Vector((d.x, d.y, 0)).normalized()
    length = d.length
    pad = ankle + flat * length * 0.28
    pad.z = 0.04
    n, toe_r = leg["toes"], leg["toe_r"]
    tips = []
    for i in range(n):
        f = (i / (n - 1)) - 0.5
        dir_ = Matrix.Rotation(math.radians(leg["spread"]) * f, 3, "Z") @ flat
        tip = pad + dir_ * length * (0.72 + 0.18 * (1 - abs(f) * 2))
        tip.z = toe_r
        blob.capsule(pad, tip, toe_r)
        blob.ball(tip, toe_r * 1.5)  # round toe pads -- cartoon frog fingers
        tips.append(tip)
    for t0, t1 in zip(tips, tips[1:]):  # the web between neighbouring toes
        mid = pad.lerp((t0 + t1) / 2, 0.62)
        rot = Vector((1, 0, 0)).rotation_difference(((t0 + t1) / 2 - pad).normalized())
        blob.ellipsoid(tuple(mid), (leg["web"] * 0.9, leg["web"], toe_r * 0.5), rotation=rot)
    blob.capsule(ankle, pad, toe_r * 1.4)
    return pad, tips


def build_blob(resolution):
    blob = Blob(resolution)
    blob.ellipsoid(BODY["co"], BODY["radii"], BODY["tilt"])
    blob.ellipsoid(HEAD["co"], HEAD["radii"], HEAD["tilt"])
    blob.ellipsoid(JAW["co"], JAW["radii"], JAW["tilt"])
    for p in sides(EYE["co"]):
        blob.ball(p, EYE["r"])
    blob.ellipsoid(BROW["co"], BROW["radii"])
    for p in sides(HAUNCH["co"]):
        blob.ball(p, HAUNCH["r"])
    for leg in (BACK_LEG, FRONT_LEG):
        for m in (lambda p: p, mirror):
            blob.limb(m(leg["hip"]), m(leg["knee"]), *leg["r_thigh"])
            blob.limb(m(leg["knee"]), m(leg["ankle"]), *leg["r_shin"])
            foot(blob, m(leg["ankle"]), m(leg["toe"]), leg)
    obj = bpy.data.objects.new("FrogBlob", blob.mb)
    bpy.context.scene.collection.objects.link(obj)
    return obj


# --------------------------------------------------------------------------
# 2. Mesh: one closed surface, subdivision-smoothed
# --------------------------------------------------------------------------


def activate(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def apply_modifier(obj, mod):
    activate(obj)
    bpy.ops.object.modifier_apply(modifier=mod.name)


def build_dense_mesh():
    blob = build_blob(0.012)
    activate(blob)
    bpy.ops.object.convert(target="MESH")
    obj = bpy.context.active_object
    obj.name = "FrogDense"
    rem = obj.modifiers.new("Remesh", "REMESH")
    rem.mode = "VOXEL"
    rem.voxel_size = DENSE_VOXEL
    apply_modifier(obj, rem)
    sub = obj.modifiers.new("Subdivision", "SUBSURF")
    sub.levels = sub.render_levels = 1
    apply_modifier(obj, sub)
    # One gentle relax so the voxel steps are gone entirely.
    sm = obj.modifiers.new("Relax", "SMOOTH")
    sm.factor, sm.iterations = 0.5, 4
    apply_modifier(obj, sm)
    return obj


# --------------------------------------------------------------------------
# 3. Paint: vertex colour on the dense mesh
# --------------------------------------------------------------------------


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def mix(c, target, t):
    """c (N,3) toward a colour by t (N,)."""
    return c + (np.asarray(target)[None, :] - c) * t[:, None]


SPOTS = [(0.10, 0.18, 0.05), (-0.13, 0.05, 0.045), (0.02, 0.36, 0.04), (-0.06, 0.26, 0.035), (0.16, 0.38, 0.035)]


def albedo(P, N):
    """The frog's albedo at surface points P (N,3) with normals N (bind pose)."""
    x, y, z = P[:, 0], P[:, 1], P[:, 2]
    nx, ny, nz = N[:, 0], N[:, 1], N[:, 2]
    ax = np.abs(x)
    c = np.ones((len(P), 3))

    # Belly and throat: the underside, softly.
    under = smoothstep(-0.05, -0.55, nz) * smoothstep(0.30, 0.12, z)
    throat = smoothstep(0.0, -0.6, nz) * (y < -0.05)
    c = mix(c, BELLY, np.maximum(under, throat))

    # A few soft darker spots on the back.
    back = (nz > 0.3) & (z > 0.35) & (y > -0.05)
    for sx, sy, sr in SPOTS:
        d = np.hypot(x - sx, y - sy)
        c = mix(c, SPOT, back * smoothstep(sr, sr * 0.6, d))

    # Eyes: a big pupil looking forward and out, with a catch-light.
    e = np.array(EYE["co"])
    v = np.stack([ax - e[0], y - e[1], z - e[2]], axis=1)
    vl = np.linalg.norm(v, axis=1)
    on_eye = (vl < EYE["r"] * 1.35) & (v[:, 2] > -0.02)
    u = v / np.maximum(vl, 1e-9)[:, None]
    look = np.array(PUPIL_LOOK)
    c = mix(c, INK, on_eye * smoothstep(math.cos(math.radians(40)), math.cos(math.radians(35)), u @ look))
    glint = np.array([look[0] - 0.25, look[1], look[2] + 0.45])
    glint /= np.linalg.norm(glint)
    c = mix(c, WHITE, on_eye * smoothstep(math.cos(math.radians(10)), math.cos(math.radians(7)), u @ glint))

    # The mouth: a wide smile round the front of the head, curving up at the
    # corners, just above where the jaw meets the head.
    mouth_z = 0.405 + 0.55 * ax * ax + 0.25 * np.maximum(0.0, ax - 0.2) ** 1.5
    reach = smoothstep(0.31, 0.27, ax)
    front = (y < -0.10) & (ny < 0.5) & ((y < -0.30) | (ax > 0.12))
    w = 0.0095
    c = mix(c, INK, front * reach * smoothstep(w * 1.6, w, np.abs(z - mouth_z)))

    # Nostrils: two small dots on top of the snout.
    d = np.hypot(ax - 0.06, y + 0.44)
    c = mix(c, INK, ((ny < -0.1) & (nz > 0.3)) * smoothstep(0.014, 0.010, d))
    return c


def paint(obj):
    me = obj.data
    n = len(me.vertices)
    P = np.empty(n * 3)
    N = np.empty(n * 3)
    me.vertices.foreach_get("co", P)
    me.vertex_normals.foreach_get("vector", N)
    c = albedo(P.reshape(-1, 3), N.reshape(-1, 3))
    attr = me.color_attributes.new("Albedo", "FLOAT_COLOR", "POINT")
    me.color_attributes.active_color = attr
    lin = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)  # stored linear
    rgba = np.concatenate([lin, np.ones((n, 1))], axis=1)
    attr.data.foreach_set("color", rgba.ravel())


# --------------------------------------------------------------------------
# 4. The shipped mesh: decimated, unwrapped, the colour baked to a texture
# --------------------------------------------------------------------------


def build_low_mesh(dense):
    low = dense.copy()
    low.data = dense.data.copy()
    low.name = "Frog"
    low.data.name = "Frog"
    bpy.context.scene.collection.objects.link(low)
    tris = sum(len(p.vertices) - 2 for p in low.data.polygons)
    dec = low.modifiers.new("Decimate", "DECIMATE")
    dec.decimate_type = "COLLAPSE"
    dec.ratio = MESH_TRIS / tris
    dec.use_symmetry = True
    dec.symmetry_axis = "X"
    apply_modifier(low, dec)
    low.data.validate(clean_customdata=False)
    for a in list(low.data.color_attributes):
        low.data.color_attributes.remove(a)
    activate(low)
    bpy.ops.object.shade_smooth()
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.006)
    bpy.ops.object.mode_set(mode="OBJECT")
    return low


def bake_albedo(dense, low, out_dir):
    img = bpy.data.images.new("frog_albedo", TEXTURE_SIZE, TEXTURE_SIZE, alpha=False)
    img.colorspace_settings.name = "sRGB"

    src = bpy.data.materials.new("FrogBakeSource")
    src.use_nodes = True
    nt = src.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    col = nt.nodes.new("ShaderNodeVertexColor")
    col.layer_name = "Albedo"
    nt.links.new(col.outputs["Color"], bsdf.inputs["Base Color"])
    dense.data.materials.clear()
    dense.data.materials.append(src)

    mat = skin_material(img)
    low.data.materials.clear()
    low.data.materials.append(mat)
    tex_node = mat.node_tree.nodes["Albedo"]
    mat.node_tree.nodes.active = tex_node

    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 4
    sc.render.bake.use_pass_direct = False
    sc.render.bake.use_pass_indirect = False
    sc.render.bake.use_pass_color = True
    sc.render.bake.use_selected_to_active = True
    sc.render.bake.cage_extrusion = 0.02
    sc.render.bake.max_ray_distance = 0.06
    sc.render.bake.margin = 6

    bpy.ops.object.select_all(action="DESELECT")
    dense.select_set(True)
    low.select_set(True)
    bpy.context.view_layer.objects.active = low
    bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"}, use_selected_to_active=True)

    path = os.path.join(out_dir, "frog_albedo.png")
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    return img


def skin_material(img):
    """The one material: albedo texture straight into base colour.

    Exporters read this as baseColorTexture (glTF) / DiffuseColor (FBX). The
    game multiplies the player's frog colour on top."""
    mat = bpy.data.materials.new("FrogSkin")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.55
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.name = "Albedo"
    tex.image = img
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    return mat


# --------------------------------------------------------------------------
# 5. The rig
# --------------------------------------------------------------------------

LEG_BONES = ("thigh", "shin", "foot")


def leg_bone_names(which, side):
    return [f"{which}_{part}.{side}" for part in LEG_BONES]


def foot_tip(leg):
    ankle, toe = Vector(leg["ankle"]), Vector(leg["toe"])
    d = toe - ankle
    flat = Vector((d.x, d.y, 0)).normalized()
    pad = ankle + flat * d.length * 0.28
    tip = pad + flat * d.length * 0.85
    tip.z = 0.03
    return tip


def build_rig(low):
    arm = bpy.data.armatures.new("FrogRig")
    rig = bpy.data.objects.new("FrogRig", arm)
    bpy.context.scene.collection.objects.link(rig)
    activate(rig)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm.edit_bones

    root = eb.new("root")
    root.head, root.tail = (0, 0, 0), (0, 0.25, 0)
    root.use_deform = False

    spine = eb.new("spine")
    spine.head, spine.tail = (0, 0.30, 0.30), (0, -0.30, 0.50)
    spine.parent = root

    for which, leg in (("back", BACK_LEG), ("front", FRONT_LEG)):
        for side, m in (("L", lambda p: Vector(p)), ("R", lambda p: Vector(mirror(p)))):
            names = leg_bone_names(which, side)
            tip = foot_tip(leg)
            joints = [m(leg["hip"]), m(leg["knee"]), m(leg["ankle"]), m(tuple(tip))]
            parent = spine
            for i, name in enumerate(names):
                b = eb.new(name)
                b.head, b.tail = joints[i], joints[i + 1]
                b.roll = 0
                b.parent = parent
                b.use_connect = i > 0
                parent = b
    bpy.ops.object.mode_set(mode="OBJECT")

    bpy.ops.object.select_all(action="DESELECT")
    low.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    # Mobile skinning: at most four bones per vertex, weights summing to 1.
    activate(low)
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)
    unweighted = [v.index for v in low.data.vertices if not v.groups]
    assert not unweighted, f"{len(unweighted)} vertices have no bone weight"
    return rig


# --------------------------------------------------------------------------
# 6. Poses and clips
#
# A pose says which way each bone points, in the frog's own frame (before
# the body's pitch), for the LEFT side; the right side mirrors it. Bones keep
# their length, so a pose is directions only.
# --------------------------------------------------------------------------

FPS = 60
HOP_FRAMES = 24  # 24 frames at 60 fps = 0.4 s, stretched over FrogHopDuration

POSES = {
    # Sitting on the pad: back legs folded Z-wise under the haunches, feet
    # forward; front legs propping the chest up.
    "sit": dict(
        lift=None, pitch=0.0,
        back=((0.55, -0.70, -0.25), (-0.15, 0.97, -0.10), (0.35, -0.92, -0.05)),
        front=((0.20, -0.25, -0.95), (0.05, -0.10, -1.0), (0.30, -0.95, -0.02)),
    ),
    # Take-off (progress 0.25): back legs drive out straight behind.
    "push": dict(
        lift=None, pitch=18.0,
        back=((0.22, 0.88, -0.42), (0.10, 0.92, -0.38), (0.05, 0.80, -0.60)),
        front=((0.30, 0.60, -0.75), (0.15, -0.25, -0.95), (0.20, -0.40, -0.90)),
    ),
    # In the air (0.5): everything tucked.
    "tuck": dict(
        lift=0.42, pitch=5.0,
        back=((0.50, -0.75, -0.30), (-0.10, 0.98, -0.10), (0.30, -0.95, 0.0)),
        front=((0.30, 0.30, -0.90), (0.20, -0.55, -0.80), (0.20, -0.95, -0.25)),
    ),
    # Landing (0.8): front legs reach forward and down for the pad.
    "reach": dict(
        lift=None, pitch=-14.0,
        back=((0.30, 0.92, -0.22), (0.10, 0.97, 0.05), (0.10, 0.85, 0.35)),
        front=((0.25, -0.55, -0.80), (0.10, -0.35, -0.93), (0.20, -0.95, -0.15)),
    ),
}

HOP_KEYS = [(0.0, "sit"), (0.25, "push"), (0.5, "tuck"), (0.8, "reach"), (1.0, "sit")]


def apply_pose(rig, pose, base_lift):
    pbs = rig.pose.bones
    for pb in pbs:
        pb.rotation_mode = "QUATERNION"
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()

    # Everything is set in armature space, so no bone's local axes matter.
    root = pbs["root"]
    m = root.bone.matrix_local.copy()
    m.translation = Vector((0, 0, base_lift))
    root.matrix = m
    bpy.context.view_layer.update()

    # Pitch the body about the world X axis: front (-Y) up is positive.
    spine = pbs["spine"]
    body_rot = Matrix.Rotation(math.radians(-pose["pitch"]), 3, "X")
    m = (body_rot @ spine.bone.matrix_local.to_3x3()).to_4x4()
    m.translation = spine.head
    spine.matrix = m
    bpy.context.view_layer.update()

    for which in ("back", "front"):
        for side, sx in (("L", 1), ("R", -1)):
            for name, d in zip(leg_bone_names(which, side), pose[which]):
                pb = pbs[name]
                want = body_rot @ Vector((d[0] * sx, d[1], d[2])).normalized()
                rest = pb.bone.matrix_local.to_3x3()
                rest_dir = rest.col[1].normalized()
                parent_rot = pb.parent.matrix.to_3x3() @ pb.parent.bone.matrix_local.to_3x3().inverted() if pb.parent else Matrix.Identity(3)
                cur_dir = (parent_rot @ rest_dir).normalized()
                r = cur_dir.rotation_difference(want).to_matrix()
                new = (r @ parent_rot @ rest).to_4x4()
                new.translation = pb.head
                pb.matrix = new
                bpy.context.view_layer.update()


def lowest_point(obj):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(dg)
    me = ev.to_mesh()
    z = min((ev.matrix_world @ v.co).z for v in me.vertices)
    ev.to_mesh_clear()
    return z


def key_all(rig, frame):
    for pb in rig.pose.bones:
        pb.keyframe_insert("rotation_quaternion", frame=frame)
        pb.keyframe_insert("location", frame=frame)


def build_clips(rig, low):
    sc = bpy.context.scene
    sc.render.fps = FPS
    rig.animation_data_create()
    activate(rig)
    bpy.ops.object.mode_set(mode="POSE")

    # Sitting on the ground: how far the sitting pose must drop to touch it.
    apply_pose(rig, POSES["sit"], 0.0)
    sit_height = -lowest_point(low)

    def place(pose):
        """A planted pose (lift None) has its lowest point on the ground; the
        others float `lift` above the sitting frog."""
        if pose["lift"] is None:
            apply_pose(rig, pose, 0.0)
            apply_pose(rig, pose, -lowest_point(low))
        else:
            apply_pose(rig, pose, sit_height + pose["lift"])

    sit = bpy.data.actions.new("Sit")
    rig.animation_data.action = sit
    place(POSES["sit"])
    key_all(rig, 0)
    key_all(rig, 1)

    hop = bpy.data.actions.new("Hop")
    rig.animation_data.action = hop
    for progress, name in HOP_KEYS:
        place(POSES[name])
        key_all(rig, round(progress * HOP_FRAMES))

    for act in (sit, hop):
        act.use_fake_user = True
    bpy.ops.object.mode_set(mode="OBJECT")
    sc.frame_start, sc.frame_end = 0, HOP_FRAMES
    return sit, hop


# --------------------------------------------------------------------------
# 7. Export
# --------------------------------------------------------------------------


def export(rig, low, out_dir, sit, hop):
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    low.select_set(True)
    bpy.context.view_layer.objects.active = rig

    # Both clips on NLA tracks so each exporter writes each one by name.
    rig.animation_data.action = None
    for act in (hop, sit):
        tr = rig.animation_data.nla_tracks.new()
        tr.name = act.name
        tr.strips.new(act.name, int(act.frame_range[0]), act)
        tr.mute = False

    bpy.ops.export_scene.gltf(
        filepath=os.path.join(out_dir, "frog.glb"),
        export_format="GLB",
        use_selection=True,
        export_apply=False,
        export_skins=True,
        export_animations=True,
        export_animation_mode="NLA_TRACKS",
        export_force_sampling=True,
        export_frame_range=False,
        export_image_format="AUTO",
        export_yup=True,
    )

    bpy.ops.export_scene.fbx(
        filepath=os.path.join(out_dir, "frog.fbx"),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        use_mesh_modifiers=False,
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        armature_nodetype="NULL",
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=True,
        bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
        path_mode="COPY",
        embed_textures=True,
    )

    # Hand the rig its hop back for the previews.
    for tr in list(rig.animation_data.nla_tracks):
        rig.animation_data.nla_tracks.remove(tr)
    rig.animation_data.action = hop


# --------------------------------------------------------------------------
# 8. Previews
# --------------------------------------------------------------------------

FROG_GREEN = (0x3E / 255, 0x93 / 255, 0x3E / 255)  # FrogColours.FrogGreen, #3E933E


def srgb_to_linear(c):
    return tuple(((x + 0.055) / 1.055) ** 2.4 if x > 0.04045 else x / 12.92 for x in c[:3]) + (1.0,)


def tint_for_preview(mat, srgb):
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tex = nt.nodes["Albedo"]
    mul = nt.nodes.new("ShaderNodeMix")
    mul.data_type = "RGBA"
    mul.blend_type = "MULTIPLY"
    mul.inputs["Factor"].default_value = 1.0
    nt.links.new(tex.outputs["Color"], mul.inputs[6])
    mul.inputs[7].default_value = srgb_to_linear(srgb)
    nt.links.new(mul.outputs[2], bsdf.inputs["Base Color"])


def setup_stage(res, samples):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = res
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
    so.rotation_euler = Euler((math.radians(40), math.radians(-20), math.radians(-35)))
    sc.collection.objects.link(so)

    # A lily pad to sit on, so contact and the hop's height read.
    bpy.ops.mesh.primitive_cylinder_add(vertices=64, radius=1.1, depth=0.04, location=(0, 0.1, -0.02))
    pad = bpy.context.active_object
    pad.name = "PreviewPad"
    pm = bpy.data.materials.new("PreviewPad")
    pm.use_nodes = True
    pm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = srgb_to_linear((0.55, 0.72, 0.42))
    pm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.8
    pad.data.materials.append(pm)
    bpy.ops.object.shade_smooth()


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


def render_previews(rig, low, out_dir, sit, hop, samples=48):
    from PIL import Image  # only the strip needs it

    setup_stage((1200, 900), samples)
    mat = low.data.materials[0]
    tint_for_preview(mat, FROG_GREEN)
    sc = bpy.context.scene

    rig.animation_data.action = sit
    sc.frame_set(0)
    camera((1.9, -2.2, 1.35), (0.0, 0.05, 0.30), lens=55)
    render_to(os.path.join(out_dir, "frog_hero_3q.png"))

    camera((3.6, 0.05, 0.55), (0.0, 0.10, 0.30), lens=55)
    render_to(os.path.join(out_dir, "frog_side.png"))

    # The hop: five frames, sit -> push -> tuck -> reach -> sit, side-on.
    rig.animation_data.action = hop
    sc.render.resolution_x, sc.render.resolution_y = 640, 640
    camera((4.2, 0.15, 0.95), (0.0, 0.15, 0.42), lens=50)
    frames = []
    for progress, name in HOP_KEYS:
        f = round(progress * HOP_FRAMES)
        sc.frame_set(f)
        p = os.path.join(out_dir, f"_hop_{f:02d}.png")
        render_to(p)
        frames.append(p)
    ims = [Image.open(p) for p in frames]
    strip = Image.new("RGB", (sum(i.width for i in ims), ims[0].height), "white")
    x = 0
    for im in ims:
        strip.paste(im, (x, 0))
        x += im.width
    strip.save(os.path.join(out_dir, "frog_hop_strip.png"))
    for p in frames:
        os.remove(p)


# --------------------------------------------------------------------------


def stats(low, rig):
    tris = sum(len(p.vertices) - 2 for p in low.data.polygons)
    return dict(
        tris=tris,
        verts=len(low.data.vertices),
        bones=[b.name for b in rig.data.bones],
        material=[m.name for m in low.data.materials],
        islands=mesh_islands(low),
    )


def mesh_islands(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    seen, count = set(), 0
    for v in bm.verts:
        if v.index in seen:
            continue
        count += 1
        stack = [v]
        while stack:
            w = stack.pop()
            if w.index in seen:
                continue
            seen.add(w.index)
            stack.extend(e.other_vert(w) for e in w.link_edges)
    bm.free()
    return count


import time
_T0 = time.time()


def log(msg):
    print(f"[frog {time.time() - _T0:6.1f}s] {msg}", flush=True)


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--no-render", action="store_true")
    ap.add_argument("--samples", type=int, default=48)
    args = ap.parse_args(argv)
    out_dir = os.path.abspath(args.out)
    os.makedirs(out_dir, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    dense = build_dense_mesh()
    log(f'dense mesh: {len(dense.data.polygons)} faces')
    paint(dense)
    log('painted')
    low = build_low_mesh(dense)
    log('decimated, unwrapped')
    bake_albedo(dense, low, out_dir)
    log('baked')
    bpy.data.objects.remove(dense)
    rig = build_rig(low)
    log('rigged')
    sit, hop = build_clips(rig, low)
    export(rig, low, out_dir, sit, hop)
    log('exported')
    print("FROG_STATS", stats(low, rig))
    if not args.no_render:
        render_previews(rig, low, out_dir, sit, hop, args.samples)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    main(argv)
