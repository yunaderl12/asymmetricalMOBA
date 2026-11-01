# Builds the low-poly Demon King (placeholder; same chibi style and skeleton as the heroes) and exports FBX for Unity.
# Dark armour with crimson trim, spiked pauldrons, tall vampire collar, long cape, silver hair, curled horns,
# red eyes and a big dark sword in the right hand. Same rig as the Knight, so scale it up in Unity if needed.
# Run headless:
#   /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup -P ArtSource/characters/build_demonking.py -- "<repo root>"
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from char_common import *  # noqa: E402,F401,F403

MAT_PREFIX[0] = "DemonKing_"
SKIN = mat("Skin", "C8B6CC", rough=0.7, spec=0.2)
HAIR = mat("Hair", "E4E0EE", rough=0.75, spec=0.2)
HAIR_DK = mat("HairShade", "A8A2BC", rough=0.8, spec=0.1)
ARMOR = mat("Armor", "2C2836", rough=0.4, metal=0.5, spec=0.4)
ARMOR_DK = mat("ArmorDark", "18161E", rough=0.5, metal=0.4, spec=0.3)
CRIMSON = mat("Crimson", "8C1A2C")
CRIMSON_DK = mat("CrimsonDark", "4A0E18")
GOLD = mat("Gold", "B8903E", rough=0.45, metal=0.4, spec=0.45)
HORN = mat("Horn", "3A3038", rough=0.5, spec=0.3)
HORN_TIP = mat("HornTip", "C8B8A0", rough=0.5, spec=0.3)
GLOW = mat("Glow", "FF3048", rough=0.2, spec=0.8)
EYE = mat("Eye", "D0203A", rough=0.3, spec=0.6)
EYE_DK = mat("EyeShade", "5A0A18", rough=0.4, spec=0.3)
LID = mat("Lid", "241A22", rough=0.6, spec=0.2)
BROW = mat("Brow", "8A8098", rough=0.8)
MOUTH = mat("Mouth", "5A3A4A", rough=0.7)

B = Body()

# ---------------------------------------------------------------- legs: dark greaves and pointed sabatons
for side, sx in (("Left", 1), ("Right", -1)):
    up_a, up_b = bone_pos(side + "UpLeg"), bone_pos(side + "UpLeg", True)
    lo_b = bone_pos(side + "Leg", True)
    B.limb(ARMOR_DK, side + "UpLeg", up_a, up_b, 0.15, 0.14)
    B.limb(ARMOR, side + "Leg", up_b, lo_b, 0.15, 0.14)
    foot_a, toe = bone_pos(side + "Foot"), bone_pos(side + "ToeBase", True)
    fx = foot_a.x + sx * 0.01
    B.box(ARMOR, side + "Foot", (fx, (foot_a.y + toe.y) / 2 + 0.03, 0.08), (0.28, 0.44, 0.16), bevel=0.04)
    B.limb(ARMOR, side + "Foot", (fx, toe.y + 0.1, 0.08), (fx, toe.y - 0.04, 0.06), 0.08, 0.0, 4)
    B.box(ARMOR, side + "Foot", (fx, foot_a.y, 0.2), (0.32, 0.32, 0.2), bevel=0.03)
    B.box(CRIMSON, side + "Foot", (fx, foot_a.y, 0.31), (0.34, 0.34, 0.04), bevel=0.012)

# ---------------------------------------------------------------- long armoured robe: dark plates, crimson front panel, gold trim
SK_N = 32
skirt_rows = ((0.9, 0.36, 0.27), (0.66, 0.5, 0.36), (0.46, 0.58, 0.41), (0.36, 0.62, 0.43))
SKR = [(z, rx, ry, -0.06) for z, rx, ry in skirt_rows]


def skirt_mat(i, j):
    a = (j + 0.5) * 360.0 / SK_N
    return CRIMSON if min(a, 360 - a) < 16 else ARMOR


B.loft([ellipse((0, -0.06, z), rx, ry, SK_N) for z, rx, ry in skirt_rows], skirt_mat, "Hips", inner=ARMOR_DK,
       inner_shrink=0.94, seal=True)
for sx in (1, -1):
    pts, nrm = surf_path(SKR, [(sx * 16, z) for z in (0.88, 0.76, 0.62, 0.5, 0.4)])
    B.ribbon(GOLD, "Hips", pts, nrm, 0.05, 0.03)
pts, nrm = surf_path(SKR, [(a * 11.25, 0.39) for a in range(33)], 0.01)
B.ribbon(GOLD, "Hips", pts, nrm, 0.07, 0.03)
for a in range(0, 360, 45):
    if a in (0,):
        continue
    c, n = surf(SKR, math.radians(a), 0.52, 0.0)
    B.limb(ARMOR_DK, "Hips", c, c + n * 0.12 + Vector((0, 0, -0.04)), 0.045, 0.0, 4)
BR = [(0.98, 0.37, 0.28, -0.06), (0.84, 0.39, 0.3, -0.06)]
B.loft([ellipse((0, -0.06, z), rx, ry, 24) for z, rx, ry, _ in BR], lambda i, j: ARMOR_DK, "Hips", inner=ARMOR_DK,
       inner_shrink=0.93, seal=True)
c, n = surf(BR, 0.0, 0.91, 0.0)
B.gem(GOLD, "Hips", c, n, (0, 0, 1), 0.12, 0.1, 0.04, 6, 0.6)
B.gem(GLOW, "Hips", c + n * 0.03, n, (0, 0, 1), 0.06, 0.05, 0.03, 6, 0.5)

# ---------------------------------------------------------------- breastplate with a glowing core
torso_rows = ((1.36, 0.3, 0.22), (1.2, 0.36, 0.26), (1.04, 0.33, 0.25), (0.9, 0.35, 0.27))
TOR = [(z, rx, ry, -0.07) for z, rx, ry in torso_rows]
B.loft([ellipse((0, -0.07, z), rx, ry, 16) for z, rx, ry in torso_rows], lambda i, j: ARMOR, blend(0.95, "Spine02", 1.22, "Spine"))
for sx in (1, -1):
    pts, nrm = surf_path(TOR, smooth([(sx * 8, 1.32), (sx * 30, 1.2), (sx * 34, 1.04), (sx * 14, 0.94)], 3), 0.006)
    B.ribbon(CRIMSON, "Spine01", pts, nrm, 0.05, 0.03)
c, n = surf(TOR, 0.0, 1.14, 0.02)
B.gem(GOLD, "Spine01", c, n, (0, 0, 1), 0.12, 0.15, 0.04, 4, 0.7)
B.gem(GLOW, "Spine01", c + n * 0.03, n, (0, 0, 1), 0.07, 0.09, 0.04, 4, 0.5)
B.limb(SKIN, "neck", bone_pos("neck"), bone_pos("neck", True), 0.085, 0.08)

# ---------------------------------------------------------------- long crimson cape, tall dark collar standing behind the head
cape_rows = ((1.32, 0.32, 0.24), (1.0, 0.58, 0.46), (0.5, 0.8, 0.64), (0.04, 0.95, 0.76))
B.loft([ellipse((0, -0.02, z), rx, ry, 12, math.radians(80), math.radians(280), closed=False) for z, rx, ry in cape_rows],
       lambda i, j: CRIMSON, blend(0.8, "Hips", 1.15, "Spine01"), closed=False, inner=ARMOR_DK, inner_shrink=0.96,
       axis_pts=[(0, -0.02, z) for z, _, _ in cape_rows], seal=True)
col_rows = ((1.98, 0.62, 0.5, -0.02), (1.7, 0.48, 0.38, -0.06), (1.44, 0.3, 0.24, -0.1))
B.loft([ellipse((0, c, z), rx, ry, 12, math.radians(70), math.radians(290), closed=False) for z, rx, ry, c in col_rows],
       lambda i, j: ARMOR_DK, "Spine", closed=False, inner=CRIMSON, inner_shrink=0.95,
       axis_pts=[(0, c, z) for z, _, _, c in col_rows], seal=True)

# ---------------------------------------------------------------- arms: spiked pauldrons, armoured sleeves, clawed gauntlets
for side, sx in (("Left", 1), ("Right", -1)):
    arm_a, arm_b = bone_pos(side + "Arm"), bone_pos(side + "Arm", True)
    fa_b, hand_b = bone_pos(side + "ForeArm", True), bone_pos(side + "Hand", True)
    sh = arm_a + Vector((sx * 0.02, 0, 0.1))
    B.ico(ARMOR, side + "Shoulder", sh, 0.32, 1, (1.15, 1.0, 0.8))
    B.ico(CRIMSON_DK, side + "Shoulder", sh + Vector((sx * 0.1, 0, -0.12)), 0.28, 1, (1.1, 1.0, 0.6))
    for k, (dx, dy, ln) in enumerate(((0.0, 0.0, 0.42), (-0.12, 0.12, 0.3), (-0.12, -0.12, 0.3))):
        base = sh + Vector((sx * (0.08 + dx), dy, 0.16))
        B.limb(HORN, side + "Shoulder", base, base + Vector((sx * ln * 0.45, dy * 0.6, ln)), 0.07, 0.0, 5)
    B.limb(ARMOR_DK, side + "Arm", arm_a, arm_b, 0.13, 0.13, 8)
    B.limb(ARMOR, side + "ForeArm", arm_b, fa_b, 0.14, 0.18, 8)
    B.limb(CRIMSON, side + "ForeArm", fa_b - Vector((sx * 0.03, 0, 0)), fa_b, 0.19, 0.19, 8)
    hand(B, ARMOR_DK, side, sx)
    for k in range(3):
        tip = hand_b + Vector((sx * 0.08, -0.04 + k * 0.04, 0))
        B.limb(HORN_TIP, side + "Hand", hand_b + Vector((-sx * 0.02, -0.04 + k * 0.04, 0)), tip, 0.022, 0.0, 4)

# ---------------------------------------------------------------- head: pale face, red eyes, long silver hair, curled horns, crown
HEAD_SCALE, HEAD_PIVOT = 0.72, Vector((0.0, -0.13, 1.47))
_body_verts = set(B.bm.verts)
B.no_jitter = True
HC = Vector((0.0, -0.17, 1.99))
K = HeadKit(B, HC, xs=1.02)
K.face(SKIN, EYE, EYE_DK, None, LID, BROW, MOUTH, ex=0.22, eye_s=(1.25, 1.2), brow_tilt=0.035, smile=0.5)
K.hair_cap(HAIR)
for sx in (1, -1):
    K.strand(HAIR, [(sx * 3, 64), (sx * 16, 50), (sx * 30, 34), (sx * 40, 16)], 0.2, 0.08, 0.035, 0.05)
    K.strand(HAIR, [(sx * 8, 76), (sx * 32, 58), (sx * 52, 36), (sx * 62, 10), (sx * 64, -14)], 0.22, 0.1, 0.05, 0.065)
    K.strand(HAIR, [(sx * 54, 30), (sx * 60, 0), (sx * 64, -30), (sx * 64, -50)], 0.22, 0.14, 0.07, 0.1, 0.06, 0.5)
    K.strand(HAIR_DK, [(sx * 90, 24), (sx * 96, -6), (sx * 98, -40)], 0.24, 0.16, 0.05, 0.07, 0.06, 0.7)
    K.strand(HAIR, [(sx * 140, 30), (sx * 150, 0), (sx * 154, -40)], 0.28, 0.2, 0.03, 0.05, 0.06, 0.8)
K.strand(HAIR_DK, [(180, 40), (180, 0), (180, -40)], 0.34, 0.24, 0.03, 0.05, 0.06, 0.8)
scale_new_verts(B, _body_verts, HC, 1.08)
for sx in (1, -1):
    base, n = K.on_head(sx * 62, 42, 0.02)
    pts = [base, base + Vector((sx * 0.22, 0.04, 0.12)), base + Vector((sx * 0.36, 0.1, 0.34)),
           base + Vector((sx * 0.34, 0.16, 0.58)), base + Vector((sx * 0.22, 0.2, 0.74))]
    for k, (a, b) in enumerate(zip(pts, pts[1:])):
        B.limb(HORN if k < 3 else HORN_TIP, "Head", a, b, 0.14 - k * 0.032, 0.14 - (k + 1) * 0.032 + 0.006, 6)
crown = [K.on_head(a, 40, 0.1) for a in range(-50, 51, 10)]
B.ribbon(GOLD, "Head", [p for p, _ in crown], [n for _, n in crown], 0.08, 0.03, 0.3)
for a, h in ((-40, 0.12), (-20, 0.16), (0, 0.24), (20, 0.16), (40, 0.12)):
    p, n = K.on_head(a, 44, 0.12)
    B.limb(GOLD, "Head", p, p + Vector((0, 0, h)) + n * 0.03, 0.04, 0.0, 4)
p, n = K.on_head(0, 38, 0.15)
B.gem(GLOW, "Head", p, n, (0, 0, 1), 0.04, 0.05, 0.025, 4, 0.5)
B.no_jitter = False
scale_new_verts(B, _body_verts, HEAD_PIVOT, HEAD_SCALE)

# ---------------------------------------------------------------- dark greatsword in the right hand
grip = bone_pos("RightForeArm", True).lerp(bone_pos("RightHand", True), 0.42) + Vector((0, -0.02, 0))
gx, gy = grip.x, grip.y
B.limb(ARMOR_DK, "RightHand", (gx, gy, 0.75), (gx, gy, 1.12), 0.045, 0.045, 6)
B.ico(GOLD, "RightHand", (gx, gy, 0.72), 0.07, 1)
B.box(GOLD, "RightHand", (gx, gy, 1.14), (0.42, 0.09, 0.07), bevel=0.015)
B.gem(GLOW, "RightHand", (gx, gy - 0.05, 1.14), (0, -1, 0), (0, 0, 1), 0.04, 0.04, 0.02, 4)
blade = [(gx - 0.1, gy, 1.18), (gx + 0.1, gy, 1.18), (gx + 0.12, gy, 2.3), (gx, gy, 2.6), (gx - 0.12, gy, 2.3)]
B.prism(ARMOR_DK, "RightHand", blade, 0.05, (0, 1, 0), True)
B.prism(CRIMSON, "RightHand", [(gx - 0.025, gy, 1.24), (gx + 0.025, gy, 1.24), (gx + 0.025, gy, 2.3), (gx, gy, 2.42),
                               (gx - 0.025, gy, 2.3)], 0.058, (0, 1, 0), True)

export(B, "DemonKingMesh", "DemonKing", "demonking.fbx", "demonking.blend")
