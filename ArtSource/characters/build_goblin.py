# Builds the low-poly Goblin (basic mob placeholder; same chibi style and Knight skeleton as the heroes) and exports FBX.
# Green skin, big pointed ears and hooked nose, ragged loincloth with a leather strap, wooden club in the right hand.
# Run headless:
#   /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup -P ArtSource/characters/build_goblin.py -- "<repo root>"
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from char_common import *  # noqa: E402,F401,F403

MAT_PREFIX[0] = "Goblin_"
SKIN = mat("Skin", "6E9A3C", rough=0.75, spec=0.15)
SKIN_DK = mat("SkinDark", "4A6E26", rough=0.8, spec=0.1)
CLOTH = mat("Cloth", "7A5A38")
CLOTH_DK = mat("ClothDark", "54402A")
LEATHER = mat("Leather", "3E2C1E", rough=0.75, spec=0.15)
WOOD = mat("Wood", "7A5230", rough=0.85)
WOOD_DK = mat("WoodDark", "4E3420", rough=0.85)
IRON = mat("Iron", "6A6E74", rough=0.5, metal=0.5, spec=0.35)
TOOTH = mat("Tooth", "EEE6CC", rough=0.5, spec=0.3)
HAIR = mat("Hair", "2A2420", rough=0.85)
EYE = mat("Eye", "E8C83A", rough=0.4, spec=0.5)
EYE_DK = mat("EyeShade", "3A1A10", rough=0.4, spec=0.3)
LID = mat("Lid", "33501A", rough=0.7)
BROW = mat("Brow", "2E4416", rough=0.8)
MOUTH = mat("Mouth", "3A1C14", rough=0.7)

B = Body()

# ---------------------------------------------------------------- legs: skinny green legs, big bare feet
for side, sx in (("Left", 1), ("Right", -1)):
    up_a, up_b = bone_pos(side + "UpLeg"), bone_pos(side + "UpLeg", True)
    lo_b = bone_pos(side + "Leg", True)
    B.limb(SKIN, side + "UpLeg", up_a, up_b, 0.13, 0.11)
    B.limb(SKIN, side + "Leg", up_b, lo_b, 0.11, 0.08)
    B.ico(SKIN_DK, side + "Leg", up_b + Vector((0, -0.03, 0)), 0.1, 1)
    foot_a, toe = bone_pos(side + "Foot"), bone_pos(side + "ToeBase", True)
    fx = foot_a.x + sx * 0.01
    B.box(SKIN, side + "Foot", (fx, (foot_a.y + toe.y) / 2 - 0.02, 0.07), (0.26, 0.5, 0.14), bevel=0.04)
    B.box(SKIN_DK, side + "Foot", (fx, foot_a.y + 0.02, 0.16), (0.2, 0.22, 0.14), bevel=0.03)
    for tx in (-0.07, 0.0, 0.07):
        B.limb(TOOTH, side + "ToeBase", (fx + tx, toe.y - 0.04, 0.05), (fx + tx, toe.y - 0.11, 0.03), 0.025, 0.004, 4)
    B.limb(LEATHER, side + "Leg", lo_b + Vector((0, 0, 0.14)), lo_b + Vector((0, 0, 0.2)), 0.1, 0.1, 6)

# ---------------------------------------------------------------- ragged loincloth with a rope belt
SKR = [(0.92, 0.35, 0.28, -0.06), (0.75, 0.4, 0.32, -0.06), (0.6, 0.43, 0.34, -0.06)]
B.loft([ellipse((0, c, z), rx, ry, 14) for z, rx, ry, c in SKR], lambda i, j: CLOTH if (i + j) % 3 else CLOTH_DK, "Hips",
       inner=CLOTH_DK, inner_shrink=0.93, seal=True)
pts, nrm = surf_path(SKR, [(a * 360 / 14, 0.9) for a in range(15)], 0.006)
B.ribbon(LEATHER, "Hips", pts, nrm, 0.05, 0.03)
for a0 in (0, 180):
    flap_top = [(a0 - 22, 0.86), (a0 + 22, 0.86)]
    pts, nrm = surf_path(SKR, flap_top + [(a0 + 20, 0.62), (a0 - 20, 0.62)], 0.03)
    n = sum(nrm, Vector()).normalized()
    # ragged hem: zig-zag points under the flap
    hem = [pts[2] - Vector((0, 0, 0.1)) + n * 0.03, pts[2].lerp(pts[3], 0.33) - Vector((0, 0, 0.02)) + n * 0.04,
           pts[2].lerp(pts[3], 0.5) - Vector((0, 0, 0.18)) + n * 0.05, pts[2].lerp(pts[3], 0.67) - Vector((0, 0, 0.03)) + n * 0.04,
           pts[3] - Vector((0, 0, 0.12)) + n * 0.03]
    B.prism(CLOTH_DK, "Hips", [pts[0], pts[1]] + hem, 0.03, n, True)

# ---------------------------------------------------------------- pot-bellied torso with a leather strap across it
torso_rows = ((1.36, 0.26, 0.2), (1.2, 0.32, 0.26), (1.04, 0.36, 0.33), (0.9, 0.33, 0.29))
TOR = [(z, rx, ry, -0.07) for z, rx, ry in torso_rows]
B.loft([ellipse((0, -0.07, z), rx, ry, 14) for z, rx, ry in torso_rows], lambda i, j: SKIN, blend(0.95, "Spine02", 1.22, "Spine"))
c, n = surf(TOR, 0.0, 1.0, 0.0)
B.gem(SKIN_DK, "Spine01", c, n, (0, 0, 1), 0.03, 0.03, 0.01, 6, 0.5)   # belly button
for path in ([(40, 1.34), (20, 1.22), (0, 1.1), (-20, 1.0), (-42, 0.93)],
             [(140, 1.34), (160, 1.22), (180, 1.1), (200, 1.0), (222, 0.93)]):
    pts, nrm = surf_path(TOR, path, 0.01)
    B.ribbon(LEATHER, "Spine01", pts, nrm, 0.09, 0.03)
c, n = surf(TOR, math.radians(10), 1.16, 0.04)
B.box(IRON, "Spine01", c, (0.1, 0.05, 0.1), bevel=0.02)                 # strap buckle
B.limb(SKIN, "neck", bone_pos("neck"), bone_pos("neck", True), 0.09, 0.085)
# fur scrap over the left shoulder
B.ico(CLOTH_DK, "LeftShoulder", bone_pos("LeftArm") + Vector((0.02, 0, 0.1)), 0.22, 1, (1.2, 1.0, 0.7))

# ---------------------------------------------------------------- arms: thin and long, leather wrist wraps
for side, sx in (("Left", 1), ("Right", -1)):
    arm_a, arm_b = bone_pos(side + "Arm"), bone_pos(side + "Arm", True)
    fa_b = bone_pos(side + "ForeArm", True)
    B.ico(SKIN, side + "Shoulder", arm_a + Vector((sx * 0.02, 0, 0.03)), 0.15, 1)
    B.limb(SKIN, side + "Arm", arm_a, arm_b, 0.1, 0.09, 7)
    B.ico(SKIN_DK, side + "Arm", arm_b, 0.09, 1)
    B.limb(SKIN, side + "ForeArm", arm_b, fa_b, 0.09, 0.11, 7)
    B.limb(LEATHER, side + "ForeArm", fa_b - Vector((sx * 0.12, 0, 0)), fa_b - Vector((sx * 0.02, 0, 0)), 0.125, 0.125, 7)
    hand(B, SKIN, side, sx)

# ---------------------------------------------------------------- head: big pointed ears, hooked nose, fangs, scruffy tuft
HEAD_SCALE, HEAD_PIVOT = 0.82, Vector((0.0, -0.13, 1.47))
_body_verts = set(B.bm.verts)
B.no_jitter = True
HC = Vector((0.0, -0.17, 1.99))
K = HeadKit(B, HC, hr=0.54, xs=1.12)
K.face(SKIN, EYE, EYE_DK, None, LID, BROW, MOUTH, ex=0.25, eye_s=(1.35, 1.55), brow_tilt=0.07, smile=-1.2)
for sx in (1, -1):
    base, _ = K.on_head(sx * 84, 6, -0.02)
    ear = [base + Vector((0, 0.02, 0.16)), base + Vector((sx * 0.62, 0.12, 0.26)), base + Vector((sx * 0.1, 0.04, -0.14))]
    B.prism(SKIN, "Head", ear, 0.05, (0, -1, 0.15), True)
    inner = [base.lerp(p, 0.75) + Vector((0, -0.035, 0)) for p in ear]
    B.prism(SKIN_DK, "Head", inner, 0.02, (0, -1, 0.15), True)
    B.ico(IRON, "Head", base + Vector((sx * 0.3, 0.05, 0.02)), 0.04, 0)      # earring
p, n = K.on_face(0.0, HC.z - 0.16, 0.0)
B.limb(SKIN, "Head", p - n * 0.03, p + n * 0.24 + Vector((0, 0, -0.1)), 0.08, 0.025, 6)
for sx in (1, -1):
    t, tn = K.on_face(sx * 0.07, HC.z - 0.35, 0.012)
    B.limb(TOOTH, "Head", t, t + Vector((0, 0, 0.08)) + tn * 0.01, 0.022, 0.004, 4)
for az, el, ln in ((0, 60, 0.3), (-14, 75, 0.26), (14, 75, 0.26), (0, 95, 0.24), (0, 125, 0.18)):
    hp, hn = K.on_head(az, el, -0.03)
    B.limb(HAIR, "Head", hp, hp + (hn + Vector((0, 0.35, 0.2))).normalized() * ln, 0.07, 0.006, 5)
B.no_jitter = False
scale_new_verts(B, _body_verts, HEAD_PIVOT, HEAD_SCALE)

# ---------------------------------------------------------------- crude wooden club with iron studs in the right hand
grip = bone_pos("RightForeArm", True).lerp(bone_pos("RightHand", True), 0.42) + Vector((0, -0.02, 0))
gx, gy, gz = grip
B.limb(WOOD_DK, "RightHand", (gx, gy, gz - 0.25), (gx, gy, gz + 0.2), 0.05, 0.055, 6)
B.limb(WOOD, "RightHand", (gx, gy, gz + 0.2), (gx, gy, gz + 1.05), 0.06, 0.15, 7)
B.limb(WOOD, "RightHand", (gx, gy, gz + 1.05), (gx, gy, gz + 1.18), 0.15, 0.08, 7)
B.limb(LEATHER, "RightHand", (gx, gy, gz - 0.12), (gx, gy, gz + 0.12), 0.065, 0.065, 6)
for k in range(6):
    a = k / 6 * math.tau
    z = gz + 0.7 + (k % 2) * 0.2
    r = 0.06 + (0.15 - 0.06) * (z - gz - 0.2) / 0.85
    d = Vector((math.cos(a), math.sin(a), 0))
    B.limb(IRON, "RightHand", Vector((gx, gy, z)) + d * (r - 0.01), Vector((gx, gy, z)) + d * (r + 0.09), 0.035, 0.004, 4)

export(B, "GoblinMesh", "Goblin", "goblin.fbx", "goblin.blend")
