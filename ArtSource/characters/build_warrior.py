# Builds the low-poly Warrior (heavy armour placeholder; same chibi style and skeleton as the Priest) and exports FBX.
# Steel plate armour with a red tabard, horned helmet with an open face, big axe in the right hand, round shield on the left arm.
# Run headless:
#   /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup -P ArtSource/characters/build_warrior.py -- "<repo root>"
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from char_common import *  # noqa: E402,F401,F403

MAT_PREFIX[0] = "Warrior_"
SKIN = mat("Skin", "DDB096", rough=0.7, spec=0.2)
HAIR = mat("Hair", "7A4A2E", rough=0.8, spec=0.15)
STEEL = mat("Steel", "9AA0AA", rough=0.4, metal=0.6, spec=0.5)
STEEL_DK = mat("SteelDark", "5C626C", rough=0.45, metal=0.6, spec=0.4)
RED = mat("Tabard", "A8302C")
RED_DK = mat("TabardDark", "6A1C1A")
LEATHER = mat("Leather", "6A4A30", rough=0.75, spec=0.15)
GOLD = mat("Gold", "CDA250", rough=0.45, metal=0.3, spec=0.45)
HORN = mat("Horn", "E2D6BC", rough=0.6, spec=0.2)
WOOD = mat("Wood", "5A3A24", rough=0.8)
EYE = mat("Eye", "4A3424", rough=0.4, spec=0.4)
EYE_DK = mat("EyeShade", "2A1C14", rough=0.4, spec=0.3)
LID = mat("Lid", "3A2418", rough=0.6, spec=0.2)
BROW = mat("Brow", "5A3420", rough=0.8)
MOUTH = mat("Mouth", "8A5040", rough=0.7)

B = Body()

# ---------------------------------------------------------------- legs: steel greaves and heavy boots
for side, sx in (("Left", 1), ("Right", -1)):
    up_a, up_b = bone_pos(side + "UpLeg"), bone_pos(side + "UpLeg", True)
    lo_b = bone_pos(side + "Leg", True)
    B.limb(LEATHER, side + "UpLeg", up_a, up_b, 0.16, 0.15)
    B.limb(STEEL, side + "Leg", up_b, lo_b, 0.16, 0.15)
    B.ico(STEEL_DK, side + "Leg", up_b + Vector((0, -0.06, 0)), 0.11, 1)
    foot_a, toe = bone_pos(side + "Foot"), bone_pos(side + "ToeBase", True)
    fx = foot_a.x + sx * 0.01
    B.box(STEEL_DK, side + "Foot", (fx, (foot_a.y + toe.y) / 2 + 0.01, 0.09), (0.3, 0.46, 0.18), bevel=0.05)
    B.box(STEEL, side + "Foot", (fx, foot_a.y, 0.2), (0.34, 0.34, 0.18), bevel=0.04)
    B.box(GOLD, side + "Foot", (fx, foot_a.y, 0.3), (0.36, 0.36, 0.04), bevel=0.012)

# ---------------------------------------------------------------- armoured skirt (tassets) with a red tabard hanging front and back
SKR = [(0.9, 0.4, 0.31, -0.06), (0.72, 0.5, 0.38, -0.06), (0.55, 0.56, 0.43, -0.06)]
B.loft([ellipse((0, c, z), rx, ry, 16) for z, rx, ry, c in SKR], lambda i, j: STEEL if i == 0 else STEEL_DK, "Hips",
       inner=STEEL_DK, inner_shrink=0.93, seal=True)
pts, nrm = surf_path(SKR, [(a * 22.5, 0.57) for a in range(17)], 0.006)
B.ribbon(GOLD, "Hips", pts, nrm, 0.04, 0.025)
for a0 in (0, 180):
    tab = [(a0 - 18, 0.88), (a0 + 18, 0.88), (a0 + 20, 0.58), (a0, 0.56), (a0 - 20, 0.58)]
    pts, nrm = surf_path(SKR, tab, 0.03)
    n = sum(nrm, Vector()).normalized()
    flap = [pts[0], pts[1], pts[2] - Vector((0, 0, 0.12)) + n * 0.05, pts[3] - Vector((0, 0, 0.2)) + n * 0.06,
            pts[4] - Vector((0, 0, 0.12)) + n * 0.05]
    B.prism(RED, "Hips", flap, 0.03, n, True)
    B.ribbon(GOLD, "Hips", flap[1:] + flap[:1], [n] * 5, 0.035, 0.03, 0.3)
# leather belt with a big gold buckle
BR = [(0.98, 0.38, 0.29, -0.06), (0.84, 0.4, 0.31, -0.06)]
B.loft([ellipse((0, -0.06, z), rx, ry, 24) for z, rx, ry, _ in BR], lambda i, j: LEATHER, "Hips", inner=LEATHER,
       inner_shrink=0.93, seal=True)
c, n = surf(BR, 0.0, 0.91, 0.0)
B.box(GOLD, "Hips", c + n * 0.02, (0.16, 0.05, 0.13), bevel=0.02)
B.box(LEATHER, "Hips", c + n * 0.045, (0.08, 0.02, 0.06), bevel=0.008)

# ---------------------------------------------------------------- breastplate with a centre ridge, gorget at the neck
torso_rows = ((1.36, 0.32, 0.23), (1.2, 0.38, 0.28), (1.04, 0.35, 0.27), (0.9, 0.36, 0.28))
TOR = [(z, rx, ry, -0.07) for z, rx, ry in torso_rows]
B.loft([ellipse((0, -0.07, z), rx, ry, 16) for z, rx, ry in torso_rows], lambda i, j: STEEL, blend(0.95, "Spine02", 1.22, "Spine"))
pts, nrm = surf_path(TOR, [(0, z) for z in (0.92, 1.04, 1.18, 1.32)], 0.0)
B.ribbon(STEEL, "Spine01", pts, nrm, 0.05, 0.035, 0.6)
for z in (1.06, 0.94):
    pts, nrm = surf_path(TOR, [(a * 22.5, z) for a in range(17)], 0.004)
    B.ribbon(STEEL_DK, "Spine01", pts, nrm, 0.025, 0.015)
c, n = surf(TOR, 0.0, 1.17, 0.03)
B.gem(RED, "Spine01", c, n, (0, 0, 1), 0.08, 0.1, 0.03, 6, 0.6)
B.limb(SKIN, "neck", bone_pos("neck"), bone_pos("neck", True), 0.085, 0.08)
NKR = [(1.52, 0.17, 0.16, -0.12), (1.36, 0.26, 0.21, -0.1)]
B.loft([ellipse((0, c, z), rx, ry, 12) for z, rx, ry, c in NKR], lambda i, j: STEEL_DK, "Spine", inner=STEEL_DK, seal=True)

# ---------------------------------------------------------------- arms: big layered pauldrons, steel arms, heavy gauntlets
for side, sx in (("Left", 1), ("Right", -1)):
    arm_a, arm_b = bone_pos(side + "Arm"), bone_pos(side + "Arm", True)
    fa_b = bone_pos(side + "ForeArm", True)
    sh = arm_a + Vector((sx * 0.02, 0, 0.1))
    B.ico(STEEL, side + "Shoulder", sh, 0.34, 1, (1.15, 1.0, 0.8))
    B.ico(STEEL_DK, side + "Shoulder", sh + Vector((sx * 0.1, 0, -0.12)), 0.3, 1, (1.1, 1.0, 0.65))
    ring = [sh + Vector((sx * 0.02 + math.cos(2 * math.pi * k / 12) * 0.38 * sx, math.sin(2 * math.pi * k / 12) * 0.34, -0.08))
            for k in range(13)]
    B.ribbon(GOLD, side + "Shoulder", ring, [(0, 0, 1)] * 13, 0.05, 0.03, 0.3)
    B.limb(STEEL_DK, side + "Arm", arm_a, arm_b, 0.13, 0.13, 8)
    B.ico(STEEL, side + "Arm", arm_b, 0.13, 1)
    B.limb(STEEL, side + "ForeArm", arm_b, fa_b, 0.14, 0.19, 8)
    B.limb(GOLD, side + "ForeArm", fa_b - Vector((sx * 0.03, 0, 0)), fa_b, 0.2, 0.2, 8)
    hand(B, SKIN, side, sx)

# ---------------------------------------------------------------- head: short hair under a horned open-face helmet
HEAD_SCALE, HEAD_PIVOT = 0.72, Vector((0.0, -0.13, 1.47))
_body_verts = set(B.bm.verts)
B.no_jitter = True
HC = Vector((0.0, -0.17, 1.99))
K = HeadKit(B, HC)
K.face(SKIN, EYE, EYE_DK, None, LID, BROW, MOUTH, brow_tilt=0.02, smile=0.4)
K.hair_cap(HAIR)
for sx in (1, -1):
    K.strand(HAIR, [(sx * 5, 50), (sx * 18, 40), (sx * 32, 28)], 0.16, 0.08, 0.04, 0.05)
    K.strand(HAIR, [(sx * 50, 26), (sx * 56, 0), (sx * 58, -20)], 0.14, 0.08, 0.06, 0.08)
scale_new_verts(B, _body_verts, HC, 1.08)
helm = K.head_mesh(B.bm, 1.26, Vector((0, 0.03, 0.06)))
helm_faces = list({f for v in helm["verts"] for f in v.link_faces})
cut = [f for f in helm_faces if (f.calc_center_median().y < HC.y - 0.1 and HC.z - 0.5 < f.calc_center_median().z < HC.z + 0.24)
       or f.calc_center_median().z < HC.z - 0.5]
bmesh.ops.delete(B.bm, geom=cut, context="FACES")
helm_verts = [v for v in helm["verts"] if v.is_valid]
B._skin(helm_verts, list({f for v in helm_verts for f in v.link_faces}), STEEL, "Head")
# brow band, nose guard, crest and horns
band = [K.on_head(a, 22, 0.13)[0] + Vector((0, 0, 0)) for a in range(-80, 81, 16)]
B.ribbon(GOLD, "Head", band, [K.on_head(a, 22, 0.1)[1] for a in range(-80, 81, 16)], 0.09, 0.035, 0.3)
p, n = K.on_face(0.0, HC.z + 0.12, 0.13)
B.box(STEEL_DK, "Head", p + Vector((0, 0, -0.1)), (0.06, 0.05, 0.3), bevel=0.012)
crest = [K.on_head(0, e, 0.2)[0] for e in range(30, 181, 15)]
B.ribbon(RED, "Head", crest, [K.on_head(0, e, 0.2)[1] for e in range(30, 181, 15)], 0.08, 0.14, 0.2)
for sx in (1, -1):
    base, n = K.on_head(sx * 80, 30, 0.12)
    pts = [base, base + Vector((sx * 0.18, 0, 0.06)), base + Vector((sx * 0.3, 0.02, 0.22)), base + Vector((sx * 0.32, 0.04, 0.42))]
    for k, (a, b) in enumerate(zip(pts, pts[1:])):
        B.limb(HORN, "Head", a, b, 0.09 - k * 0.028, 0.09 - (k + 1) * 0.028 + 0.004, 6)
B.no_jitter = False
scale_new_verts(B, _body_verts, HEAD_PIVOT, HEAD_SCALE)

# ---------------------------------------------------------------- big axe in the right hand, round shield on the left forearm
grip = bone_pos("RightForeArm", True).lerp(bone_pos("RightHand", True), 0.42) + Vector((0, -0.02, 0))
gx, gy = grip.x, grip.y
B.limb(WOOD, "RightHand", (gx, gy, 0.3), (gx, gy, 2.2), 0.05, 0.05, 6)
for z in (0.32, 1.0, 1.9):
    B.limb(LEATHER, "RightHand", (gx, gy, z - 0.05), (gx, gy, z + 0.05), 0.058, 0.058, 6)
B.limb(STEEL_DK, "RightHand", (gx, gy, 2.2), (gx, gy, 2.4), 0.06, 0.0, 4)
for sx in (1, -1):
    blade = [(gx, gy, 2.12), (gx + sx * 0.3, gy, 2.32), (gx + sx * 0.5, gy, 2.4), (gx + sx * 0.56, gy, 2.1),
             (gx + sx * 0.5, gy, 1.78), (gx + sx * 0.3, gy, 1.86), (gx, gy, 1.98)]
    B.prism(STEEL, "RightHand", blade, 0.05, (0, 1, 0), True)
    B.prism(STEEL_DK, "RightHand", [(gx + sx * 0.5, gy, 2.4), (gx + sx * 0.58, gy, 2.1), (gx + sx * 0.5, gy, 1.78),
                                    (gx + sx * 0.46, gy, 2.1)], 0.055, (0, 1, 0), True)
B.box(GOLD, "RightHand", (gx, gy, 2.05), (0.1, 0.08, 0.16), bevel=0.015)
fa_a, fa_b = bone_pos("LeftArm", True), bone_pos("LeftForeArm", True)
sc = fa_a.lerp(fa_b, 0.55) + Vector((0, -0.24, 0))
B.limb(RED_DK, "LeftForeArm", sc + Vector((0, 0.03, 0)), sc + Vector((0, -0.04, 0)), 0.48, 0.44, 12)
B.limb(STEEL, "LeftForeArm", sc + Vector((0, -0.04, 0)), sc + Vector((0, -0.06, 0)), 0.5, 0.5, 12)
B.limb(RED, "LeftForeArm", sc + Vector((0, -0.05, 0)), sc + Vector((0, -0.075, 0)), 0.4, 0.4, 12)
B.limb(GOLD, "LeftForeArm", sc + Vector((0, -0.07, 0)), sc + Vector((0, -0.14, 0)), 0.12, 0.05, 8)
B.limb(STEEL_DK, "LeftForeArm", sc + Vector((0, 0.03, 0)), fa_a.lerp(fa_b, 0.55), 0.05, 0.05, 4)

export(B, "WarriorMesh", "Warrior", "warrior.fbx", "warrior.blend")
