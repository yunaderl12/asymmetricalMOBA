# Rigs a textured mesh from outside (Tripo FBX: T/A-pose, no armature, height ~1, front = -Y) onto the Knight skeleton
# so it can share the Knight's animation clips, and bakes its held items into the same FBX.
#   - Bones keep the Knight's orientation and only move to this character's joints (ModelBoneFit restores those positions
#     at runtime, because the clips write the Knight's bone positions).
#   - Arms are straightened to the Knight's rest direction, so the clips' arm poses land the same way.
#   - Items are skinned 100% to the hand. A "float" item stays a separate mesh named FloatingBook (see FloatingHeldItem).
# Materials carry no textures here; URP materials are made and remapped on the Unity side (Resources/<folder>/).
# Run headless (a source is an original .fbx, decimated here, or a .blend from an earlier decimation):
#   /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup -P ArtSource/characters/rig_tripo.py -- \
#       "<repo root>" mage body=<fbx> right=<fbx> float=<fbx>
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from char_common import *  # noqa: E402,F401,F403
import numpy as np  # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:]
CHAR = args[1]
SOURCES = dict(a.split("=", 1) for a in args[2:])

UP = (0, 0, 1)
# item: source key, hand, length along the item's up axis, world up and front (item -Y) in the idle pose, grip point on the item.
# grip: fraction of the height; plate items (shield, book) are held on their back side.
CHARS = {
    "mage": dict(
        folder="Mage", fbx="mage.fbx", blend="mage_tripo.blend", prefix="Mage", height=2.9, target=9000, head_z=0.50,
        hip_center=(0, -0.03, 0.24), hip=(0.08, -0.03, 0.22), knee=(0.09, -0.03, 0.12), ankle=(0.10, -0.03, 0.05),
        toe=(0.105, -0.07, 0.02), toe_tip=(0.11, -0.13, 0.02), neck=(0, -0.02, 0.47), head=(0, -0.02, 0.50),
        shoulder=(0.03, -0.02, 0.445), arm=(0.15, 0.45), elbow=(0.24, 0.415), wrist=(0.33, 0.385), tip=(0.43, 0.385),
        items=[dict(src="right", hand="Right", name="Wand", length=1.9, target=2500, up=UP, front=(0, -1, 0), grip=0.40),
               dict(src="float", name="Book", length=0.85, target=1800, plate=True)]),
    "priest": dict(
        folder="Priest", fbx="priest.fbx", blend="priest_tripo.blend", prefix="Priest", height=2.8, target=9000, head_z=0.53,
        hip_center=(0, -0.03, 0.26), hip=(0.08, -0.03, 0.24), knee=(0.09, -0.04, 0.13), ankle=(0.11, -0.06, 0.05),
        toe=(0.115, -0.10, 0.02), toe_tip=(0.12, -0.14, 0.02), neck=(0, -0.03, 0.50), head=(0, -0.02, 0.53),
        shoulder=(0.03, -0.02, 0.47), arm=(0.16, 0.485), elbow=(0.25, 0.455), wrist=(0.345, 0.43), tip=(0.485, 0.44),
        items=[dict(src="right", hand="Right", name="Wand", length=2.0, target=3000, up=UP, front=(0, -1, 0), grip=0.40),
               dict(src="left", hand="Left", name="Book", length=0.85, target=1800, up=UP, front=(0.6, -0.8, 0), grip=0.40, plate=True)]),
    "warrior": dict(
        folder="Warrior", fbx="warrior.fbx", blend="warrior_tripo.blend", prefix="Warrior", height=2.9, target=9000, head_z=0.60,
        hip_center=(0, -0.13, 0.32), hip=(0.09, -0.13, 0.30), knee=(0.11, -0.165, 0.135), ankle=(0.11, -0.13, 0.05),
        toe=(0.115, -0.18, 0.02), toe_tip=(0.12, -0.24, 0.02), neck=(0, -0.14, 0.56), head=(0, -0.15, 0.59),
        shoulder=(0.04, -0.13, 0.53), arm=(0.19, 0.52), elbow=(0.33, 0.38), wrist=(0.39, 0.30), tip=(0.39, 0.21),
        items=[dict(src="right", hand="Right", name="Axe", length=2.2, target=2000, up=UP, front=(-0.57, -0.82, 0), grip=0.26),
               dict(src="left", hand="Left", name="Shield", length=1.35, target=1500, up=UP, front=(0.45, -0.89, 0), grip=0.55, plate=True)]),
    "goblin": dict(
        folder="Goblin", fbx="goblin.fbx", blend="goblin_tripo.blend", prefix="Goblin", height=2.8, target=3500, head_z=0.67,
        hip_center=(0, 0.03, 0.37), hip=(0.07, 0.03, 0.345), knee=(0.10, 0.01, 0.19), ankle=(0.11, 0.044, 0.06),
        toe=(0.12, -0.02, 0.02), toe_tip=(0.125, -0.09, 0.015), neck=(0, -0.03, 0.64), head=(0, -0.08, 0.66),
        shoulder=(0.05, 0.05, 0.62), arm=(0.16, 0.60), elbow=(0.31, 0.50), wrist=(0.41, 0.40), tip=(0.45, 0.25),
        items=[dict(src="right", hand="Right", name="Dagger", length=0.85, target=600, up=UP, front=(-0.7, -0.7, 0), grip=0.17),
               dict(src="left", hand="Left", name="Shield", length=1.1, target=900, up=UP, front=(0.45, -0.89, 0), grip=0.5, plate=True)]),
}
CFG = CHARS[CHAR]

# Where the world axes come from in the hand's rest frame, sampled from knight_alert: rest-pose direction of world up / forward.
# An item built with these stands the way it is specified while the hero idles.
HAND_REST_DIRS = {
    "Left": ((-0.735, 0.496, 0.463), (-0.034, -0.708, 0.705)),
    "Right": ((0.572, -0.145, 0.807), (-0.343, -0.936, 0.075)),
}


def load_source(path, target):
    """Returns a mesh object with its transform applied, decimated to target faces."""
    if path.endswith(".blend"):
        with bpy.data.libraries.load(path) as (src, dst):
            dst.objects = list(src.objects)
        o = [x for x in dst.objects if x.type == "MESH"][0]
        bpy.context.scene.collection.objects.link(o)
    else:
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=path)
        o = [x for x in bpy.data.objects if x not in before and x.type == "MESH"][0]
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    if len(o.data.polygons) > target:
        m = o.modifiers.new("Decimate", "DECIMATE")
        m.decimate_type = "COLLAPSE"
        m.ratio = target / len(o.data.polygons)
        bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if o.data.has_custom_normals:
        bpy.ops.mesh.customdata_custom_splitnormals_clear()
    return o


def get_co(o):
    a = np.zeros(len(o.data.vertices) * 3)
    o.data.vertices.foreach_get("co", a)
    return a.reshape(-1, 3)


def set_co(o, co):
    o.data.vertices.foreach_set("co", co.flatten())
    o.data.update()


def assign_material(o, name):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (0.7, 0.7, 0.7, 1)
    o.data.materials.clear()
    o.data.materials.append(m)
    o.data.polygons.foreach_set("material_index", [0] * len(o.data.polygons))


def smooth(o):
    o.data.polygons.foreach_set("use_smooth", [True] * len(o.data.polygons))
    o.data.set_sharp_from_angle(angle=math.radians(50))
    o.data.update()


def seg_dist(p, a, b):
    ab = b - a
    t = np.clip(((p - a) @ ab) / max(ab @ ab, 1e-9), 0, 1)
    return np.linalg.norm(p - (a + t[:, None] * ab), axis=1)


def rot_between(a, b):
    return np.array(Vector(a).rotation_difference(Vector(b)).to_matrix())


def k(name, tail=False):
    return np.array(bone_pos(name, tail))


# ---------------------------------------------------------------- body: joints in normalised source units, scaled to the Knight's size
body = load_source(SOURCES["body"], CFG["target"])
assign_material(body, CFG["prefix"] + "_Body")
src = get_co(body)
S = CFG["height"] / src[:, 2].max()
P = src * S


def arm_joint(xz):
    """Arm joints only give x, z; y is the middle of the limb there."""
    x, z = xz
    near = src[(np.abs(src[:, 0] - x) < 0.03) & (np.abs(src[:, 2] - z) < 0.04)]
    y = (np.percentile(near[:, 1], 5) + np.percentile(near[:, 1], 95)) / 2 if len(near) else 0.0
    return np.array((x, y, z)) * S


J = {}
for key in ("hip_center", "neck", "head"):
    J[key] = np.array(CFG[key]) * S
top = np.array((0, J["head"][1], CFG["height"]))
for side, sx in (("Left", 1), ("Right", -1)):
    m = np.array((sx, 1, 1))
    for key in ("hip", "knee", "ankle", "toe", "toe_tip", "shoulder"):
        J[side + key] = np.array(CFG[key]) * S * m
    for key in ("arm", "elbow", "wrist", "tip"):
        J[side + key] = arm_joint(CFG[key]) * m
spine = [J["hip_center"] + (J["neck"] - J["hip_center"]) * f for f in (0.262, 0.524, 0.786)]

# ---------------------------------------------------------------- weights: inverse distance to the bone capsules
H = CFG["height"]
SEGS = [("Hips", J["hip_center"], spine[0], 0.06), ("Spine02", spine[0], spine[1], 0.06),
        ("Spine01", spine[1], spine[2], 0.06), ("Spine", spine[2], J["neck"], 0.06),
        ("neck", J["neck"], J["head"], 0.02), ("Head", J["head"], top, 0.08)]
for side in ("Left", "Right"):
    SEGS += [(side + "Shoulder", J[side + "shoulder"], J[side + "arm"], 0.02),
             (side + "Arm", J[side + "arm"], J[side + "elbow"], 0.02),
             (side + "ForeArm", J[side + "elbow"], J[side + "wrist"], 0.02),
             (side + "Hand", J[side + "wrist"], J[side + "tip"], 0.015),
             (side + "UpLeg", J[side + "hip"], J[side + "knee"], 0.03),
             (side + "Leg", J[side + "knee"], J[side + "ankle"], 0.025),
             (side + "Foot", J[side + "ankle"], J[side + "toe"], 0.02),
             (side + "ToeBase", J[side + "toe"], J[side + "toe_tip"], 0.015)]
NAMES = [s[0] for s in SEGS]
IDX = {n: i for i, n in enumerate(NAMES)}
D = np.stack([np.maximum(seg_dist(P, a, b) - r * H, 0) for _, a, b, r in SEGS], axis=1)
W = 1.0 / (D + 0.04 * H) ** 4

hip_z = J["Lefthip"][2]
head_z = CFG["head_z"] * S
for side, sx in (("Left", 1), ("Right", -1)):
    other_side = sx * P[:, 0] < -0.01 * H
    for b in ("Shoulder", "Arm", "ForeArm", "Hand", "UpLeg", "Leg", "Foot", "ToeBase"):
        W[other_side, IDX[side + b]] = 0
    # a hanging arm must not catch the waist and legs next to it
    inner = sx * P[:, 0] < J[side + "arm"][0] * sx * 0.6
    for b in ("Arm", "ForeArm", "Hand"):
        W[inner, IDX[side + b]] = 0
    for b in ("UpLeg", "Leg", "Foot", "ToeBase"):
        W[P[:, 2] > hip_z + 0.03 * H, IDX[side + b]] = 0
W[P[:, 2] < head_z - 0.06 * H, IDX["Head"]] = 0
W[P[:, 2] > head_z] = 0
W[P[:, 2] > head_z, IDX["Head"]] = 1
W /= W.sum(axis=1, keepdims=True)

# Robes and capes hanging away from the legs follow the hips, otherwise they tear apart when the legs swing.
leg_ids = [IDX[s + b] for s in ("Left", "Right") for b in ("UpLeg", "Leg", "Foot", "ToeBase")]
arm_ids = [IDX[s + b] for s in ("Left", "Right") for b in ("Arm", "ForeArm", "Hand")]
leg_d = D[:, leg_ids].min(axis=1)
f = np.clip((leg_d - 0.03 * H) / (0.05 * H), 0, 1) * (P[:, 2] < hip_z) * (1 - W[:, arm_ids].sum(axis=1))
W *= (1 - f)[:, None]
W[:, IDX["Hips"]] += f

order = np.argsort(-W, axis=1)[:, :3]
keep = np.zeros_like(W, dtype=bool)
np.put_along_axis(keep, order, True, axis=1)
W[~keep] = 0
W /= W.sum(axis=1, keepdims=True)

# ---------------------------------------------------------------- straighten the arms to the Knight's rest directions
bone_new = {}
Xform = {}  # bone -> (rotation, pivot, moved pivot)
for side in ("Left", "Right"):
    S0, E0, W0, T0 = J[side + "arm"], J[side + "elbow"], J[side + "wrist"], J[side + "tip"]
    Ru = rot_between(E0 - S0, k(side + "ForeArm") - k(side + "Arm"))
    Rf = rot_between(W0 - E0, k(side + "Hand") - k(side + "ForeArm"))
    Rh = rot_between(T0 - W0, k(side + "Hand", True) - k(side + "Hand"))
    E1 = S0 + Ru @ (E0 - S0)
    W1 = E1 + Rf @ (W0 - E0)
    T1 = W1 + Rh @ (T0 - W0)
    Xform[side + "Arm"] = (Ru, S0, S0)
    Xform[side + "ForeArm"] = (Rf, E0, E1)
    Xform[side + "Hand"] = (Rh, W0, W1)
    bone_new[side + "Arm"], bone_new[side + "ForeArm"], bone_new[side + "Hand"] = S0, E1, W1
    J[side + "palm"] = W1 + (T1 - W1) * 0.42

moved = P.copy()
for name, (R, a, b) in Xform.items():
    w = W[:, IDX[name]][:, None]
    moved += w * ((P - a) @ R.T + b - P)
set_co(body, moved)

# ---------------------------------------------------------------- fit the skeleton: translate each bone, orientation untouched
bone_new.update({"Hips": J["hip_center"], "Spine02": spine[0], "Spine01": spine[1], "Spine": spine[2],
                 "neck": J["neck"], "Head": J["head"]})
for side in ("Left", "Right"):
    bone_new[side + "Shoulder"] = J[side + "shoulder"]
    bone_new[side + "UpLeg"] = J[side + "hip"]
    bone_new[side + "Leg"] = J[side + "knee"]
    bone_new[side + "Foot"] = J[side + "ankle"]
    bone_new[side + "ToeBase"] = J[side + "toe"]

bpy.ops.object.select_all(action="DESELECT")
ARM.select_set(True)
bpy.context.view_layer.objects.active = ARM
inv = np.array(ARM.matrix_world.inverted())
bpy.ops.object.mode_set(mode="EDIT")
ebones = ARM.data.edit_bones
for b in ebones:
    b.use_connect = False
deltas = {}


def fit(b):
    target = bone_new.get(b.name)
    if target is not None:
        goal = inv[:3, :3] @ target + inv[:3, 3]
        deltas[b.name] = Vector(goal) - b.head
    else:
        deltas[b.name] = deltas[b.parent.name] if b.parent else Vector((0, 0, 0))
    for c in b.children:
        fit(c)


for root in [b for b in ebones if b.parent is None]:
    fit(root)
for b in ebones:
    d = deltas[b.name]
    b.head += d
    b.tail += d
bpy.ops.object.mode_set(mode="OBJECT")

# ---------------------------------------------------------------- items
for name in NAMES:
    body.vertex_groups.new(name=name)
for i, row in enumerate(W):
    for j in np.nonzero(row)[0]:
        body.vertex_groups[NAMES[j]].add([i], float(row[j]), "REPLACE")


def straighten_plate(co):
    """Book and shield sources may lean: make the thinnest axis the normal (-Y side forward) and the longest one up."""
    c = co - co.mean(axis=0)
    _, vecs = np.linalg.eigh(c.T @ c)
    normal, up = vecs[:, 0], vecs[:, 2]
    if normal[1] > 0:
        normal = -normal
    if up[2] < 0:
        up = -up
    x = np.cross(-normal, up)
    return c @ np.stack((x, -normal, up), axis=1)


def hand_frame(hand):
    ru, rf = (np.array(v, dtype=float) for v in HAND_REST_DIRS[hand])
    ru /= np.linalg.norm(ru)
    rf -= ru * (rf @ ru)
    rf /= np.linalg.norm(rf)
    # columns: where world +X, +Y, +Z sit in the rest pose (world forward is -Y)
    return np.stack((np.cross(-rf, ru), -rf, ru), axis=1)


def item_axes(up, front):
    up = np.array(up, dtype=float)
    up /= np.linalg.norm(up)
    front = np.array(front, dtype=float)
    front -= up * (front @ up)
    front /= np.linalg.norm(front)
    return np.stack((np.cross(-front, up), -front, up), axis=1)


floating = None
parts = [body]
for it in CFG["items"]:
    o = load_source(SOURCES[it["src"]], it["target"])
    assign_material(o, CFG["prefix"] + "_" + it["name"])
    co = get_co(o)
    if it.get("plate"):
        co = straighten_plate(co)
    co = co - np.array((0, 0, co[:, 2].min()))
    co *= it["length"] / co[:, 2].max()
    if "hand" not in it:
        # stays beside the left hand; ModelBoneFit's sibling FloatingHeldItem animates it from here
        center = co.max(axis=0) / 2 + co.min(axis=0) / 2
        set_co(o, co - center)
        o.location = Vector(J["Leftpalm"]) + Vector((0.1, -0.35, 0.4))
        o.name = "FloatingBook"
        smooth(o)
        floating = o
        continue
    z = it["length"] * it["grip"]
    if it.get("plate"):
        anchor = np.array((co[:, 0].mean(), co[:, 1].max() + 0.04, z))
    else:
        near = co[np.abs(co[:, 2] - z) < 0.04 * it["length"]]
        anchor = np.array((np.median(near[:, 0]), np.median(near[:, 1]), z))
    R = hand_frame(it["hand"]) @ item_axes(it["up"], it["front"])
    set_co(o, (co - anchor) @ R.T + J[it["hand"] + "palm"])
    vg = o.vertex_groups.new(name=it["hand"] + "Hand")
    vg.add(list(range(len(o.data.vertices))), 1.0, "REPLACE")
    smooth(o)
    parts.append(o)

smooth(body)
bpy.ops.object.select_all(action="DESELECT")
for o in parts:
    o.select_set(True)
bpy.context.view_layer.objects.active = body
bpy.ops.object.join()
body.name = CFG["prefix"] + "Mesh"
body.parent = ARM
body.matrix_parent_inverse = ARM.matrix_world.inverted()
body.modifiers.new("Armature", "ARMATURE").object = ARM

# ---------------------------------------------------------------- export
for img in list(bpy.data.images):
    bpy.data.images.remove(img)
bpy.ops.object.select_all(action="DESELECT")
ARM.select_set(True)
body.select_set(True)
if floating:
    floating.select_set(True)
bpy.context.view_layer.objects.active = ARM
out_fbx = os.path.join(ROOT, "Assets", "_Project", "Resources", CFG["folder"], CFG["fbx"])
bpy.ops.export_scene.fbx(filepath=out_fbx, use_selection=True, object_types={"ARMATURE", "MESH"},
                         add_leaf_bones=False, bake_anim=False, mesh_smooth_type="FACE",
                         use_armature_deform_only=False)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "ArtSource", "characters", CFG["blend"]))
print("RIGGED", CHAR, len(body.data.polygons), "faces ->", out_fbx)
