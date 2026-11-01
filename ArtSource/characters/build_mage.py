# Builds the low-poly Mage (placeholder; same chibi style and skeleton as the Priest) and exports FBX for Unity.
# Purple robe with a capelet, big pointed hat, blonde hair, staff with a blue orb in the left hand.
# Run headless:
#   /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup -P ArtSource/characters/build_mage.py -- "<repo root>"
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from char_common import *  # noqa: E402,F401,F403

MAT_PREFIX[0] = "Mage_"
SKIN = mat("Skin", "E0B69E", rough=0.7, spec=0.2)
HAIR = mat("Hair", "D9B26A", rough=0.8, spec=0.15)
HAIR_DK = mat("HairShade", "B08A4A", rough=0.85, spec=0.1)
ROBE = mat("Robe", "4B3280")
ROBE_DK = mat("RobeDark", "2A1E48")
HAT = mat("Hat", "3A2864")
SASH = mat("Sash", "2E8A8A")
TIGHTS = mat("Tights", "2B2238")
BOOT = mat("Boot", "6A4630", rough=0.75, spec=0.15)
GOLD = mat("Gold", "CDA250", rough=0.45, metal=0.3, spec=0.45)
WOOD = mat("Staff", "5A3A24", rough=0.8)
ORB = mat("Orb", "4FB4FF", rough=0.15, spec=0.8)
EYE = mat("Eye", "3E5A8A", rough=0.4, spec=0.4)
EYE_DK = mat("EyeShade", "22304E", rough=0.4, spec=0.3)
EYE_HL = mat("EyeHighlight", "A8C4E8", rough=0.3, spec=0.5)
LID = mat("Lid", "4A3024", rough=0.6, spec=0.2)
BROW = mat("Brow", "A07A40", rough=0.8)
MOUTH = mat("Mouth", "8A5040", rough=0.7)

B = Body()

# ---------------------------------------------------------------- legs: dark tights, brown boots with gold cuffs
for side, sx in (("Left", 1), ("Right", -1)):
    up_a, up_b = bone_pos(side + "UpLeg"), bone_pos(side + "UpLeg", True)
    lo_b = bone_pos(side + "Leg", True)
    B.limb(TIGHTS, side + "UpLeg", up_a, up_b, 0.15, 0.135)
    B.limb(TIGHTS, side + "Leg", up_b, lo_b, 0.135, 0.125)
    foot_a, toe = bone_pos(side + "Foot"), bone_pos(side + "ToeBase", True)
    fx = foot_a.x + sx * 0.01
    B.box(BOOT, side + "Foot", (fx, (foot_a.y + toe.y) / 2 + 0.01, 0.08), (0.26, 0.42, 0.16), bevel=0.04)
    B.box(BOOT, side + "Foot", (fx, foot_a.y, 0.17), (0.3, 0.3, 0.18), bevel=0.03)
    B.box(GOLD, side + "Foot", (fx, foot_a.y, 0.265), (0.33, 0.33, 0.04), bevel=0.012)

# ---------------------------------------------------------------- robe skirt: purple, open at the front over a dark under-layer
SK_N = 36
skirt_rows = ((0.88, 0.34, 0.26), (0.66, 0.46, 0.34), (0.5, 0.54, 0.39), (0.4, 0.58, 0.41), (0.32, 0.6, 0.42))
SKR = [(z, rx, ry, -0.06) for z, rx, ry in skirt_rows]


def skirt_mat(i, j):
    a = (j + 0.5) * 360.0 / SK_N
    return ROBE_DK if min(a, 360 - a) < 14 else ROBE


B.loft([ellipse((0, -0.06, z), rx, ry, SK_N) for z, rx, ry in skirt_rows], skirt_mat, "Hips", inner=ROBE_DK,
       inner_shrink=0.94, seal=True)
for sx in (1, -1):
    pts, nrm = surf_path(SKR, [(sx * 14, z) for z in (0.86, 0.74, 0.62, 0.5, 0.42, 0.36)])
    B.ribbon(GOLD, "Hips", pts, nrm, 0.05, 0.025)
pts, nrm = surf_path(SKR, [(a * 10.0, 0.36) for a in range(37)], 0.01)
B.ribbon(GOLD, "Hips", pts, nrm, 0.07, 0.03)
for sx in (1, -1):
    c, n = surf(SKR, math.radians(sx * 50), 0.55, 0.012)
    B.gem(GOLD, "Hips", c, n, (0, 0, 1), 0.05, 0.07, 0.02, 4, 0.4)
# teal sash with a knot and hanging tails
BR = [(0.9, 0.35, 0.27, -0.06), (0.74, 0.37, 0.28, -0.06)]
B.loft([ellipse((0, -0.06, z), rx, ry, SK_N) for z, rx, ry, _ in BR], lambda i, j: SASH, "Hips", inner=SASH,
       inner_shrink=0.93, seal=True)
c, n = surf(BR, math.radians(-30), 0.82, 0.0)
B.ico(SASH, "Hips", c + n * 0.03, 0.07, 1, (1, 0.7, 1))
for k, dx in enumerate((-0.04, 0.05)):
    B.ribbon(SASH, "Hips", [c + n * 0.04 + Vector((dx, 0, -0.04)), c + n * 0.06 + Vector((dx * 1.5, -0.01, -0.22)),
                            c + n * 0.08 + Vector((dx * 2, -0.02, -0.36))], [n] * 3, [0.08, 0.07, 0.06], 0.02, 0.2)

# ---------------------------------------------------------------- torso: purple with gold front trims and a blue brooch
torso_rows = ((1.33, 0.28, 0.2), (1.18, 0.32, 0.23), (1.02, 0.29, 0.22), (0.84, 0.3, 0.23))
TOR = [(z, rx, ry, -0.07) for z, rx, ry in torso_rows]
B.loft([ellipse((0, -0.07, z), rx, ry, 24) for z, rx, ry in torso_rows], lambda i, j: ROBE, blend(0.95, "Spine02", 1.22, "Spine"))
for sx in (1, -1):
    pts, nrm = surf_path(TOR, [(sx * 14, z) for z in (0.86, 1.0, 1.14, 1.3)], 0.008)
    B.ribbon(GOLD, "Spine01", pts, nrm, 0.045, 0.025)
B.limb(SKIN, "neck", bone_pos("neck"), bone_pos("neck", True), 0.085, 0.08)

# ---------------------------------------------------------------- capelet over the shoulders with a gold rim and a clasp
cap_rows = ((1.54, 0.16, 0.15, -0.12), (1.44, 0.42, 0.33, -0.1), (1.32, 0.64, 0.47, -0.08), (1.18, 0.7, 0.5, -0.08))
CPR = cap_rows
B.loft([ellipse((0, c, z), rx, ry, 16) for z, rx, ry, c in cap_rows], lambda i, j: HAT, "Spine", inner=ROBE_DK,
       inner_shrink=0.95, axis_pts=[(0, c, z) for z, _, _, c in cap_rows], seal=True)
pts, nrm = surf_path(CPR, [(a * 15.0, 1.2) for a in range(25)], 0.01)
B.ribbon(GOLD, "Spine", pts, nrm, 0.06, 0.03)
c, n = surf(CPR, 0.0, 1.4, 0.02)
B.gem(GOLD, "Spine", c, n, (0, 0, 1), 0.07, 0.07, 0.03, 6, 0.6)
B.gem(ORB, "Spine", c + n * 0.025, n, (0, 0, 1), 0.04, 0.04, 0.02, 6, 0.5)

# ---------------------------------------------------------------- arms: wide purple sleeves with gold cuffs
for side, sx in (("Left", 1), ("Right", -1)):
    arm_a, arm_b = bone_pos(side + "Arm"), bone_pos(side + "Arm", True)
    fa_b = bone_pos(side + "ForeArm", True)
    B.ico(ROBE, side + "Shoulder", arm_a + Vector((-sx * 0.05, 0, 0.0)), 0.16, 1, (1.1, 1, 0.95))
    B.limb(ROBE, side + "Arm", arm_a, arm_b, 0.15, 0.16, 8)
    cuff_a = fa_b - Vector((sx * 0.08, 0, 0))
    B.limb(ROBE, side + "ForeArm", arm_b, cuff_a, 0.16, 0.24, 8)
    B.limb(GOLD, side + "ForeArm", cuff_a, fa_b, 0.25, 0.26, 8)
    B.limb(ROBE_DK, side + "ForeArm", fa_b, fa_b + Vector((sx * 0.01, 0, 0)), 0.2, 0.2, 8)
    hand(B, SKIN, side, sx)

# ---------------------------------------------------------------- head, blonde bob hair, big pointed hat
HEAD_SCALE, HEAD_PIVOT = 0.72, Vector((0.0, -0.13, 1.47))
_body_verts = set(B.bm.verts)
B.no_jitter = True
HC = Vector((0.0, -0.17, 1.99))
K = HeadKit(B, HC)
K.face(SKIN, EYE, EYE_DK, EYE_HL, LID, BROW, MOUTH)
K.hair_cap(HAIR)
for sx in (1, -1):
    K.strand(HAIR, [(sx * 3, 62), (sx * 14, 48), (sx * 28, 34), (sx * 42, 18)], 0.2, 0.1, 0.035, 0.05)
    K.strand(HAIR, [(sx * 6, 74), (sx * 28, 56), (sx * 48, 36), (sx * 60, 14), (sx * 64, -8)], 0.22, 0.12, 0.05, 0.065)
    K.strand(HAIR, [(sx * 50, 30), (sx * 56, 4), (sx * 60, -24), (sx * 62, -46)], 0.22, 0.16, 0.07, 0.1, 0.06, 0.1)
    K.strand(HAIR, [(sx * 80, 26), (sx * 86, 0), (sx * 88, -26), (sx * 86, -48)], 0.22, 0.15, 0.05, 0.07, 0.06, 0.16)
    K.strand(HAIR_DK, [(sx * 120, 20), (sx * 130, -6), (sx * 134, -40)], 0.24, 0.16, 0.03, 0.05, 0.06, 0.16)
K.strand(HAIR_DK, [(180, 30), (180, 0), (180, -36)], 0.3, 0.2, 0.03, 0.05, 0.06, 0.16)
scale_new_verts(B, _body_verts, HC, 1.08)
# hat: wide brim and a tall cone whose tip bends backwards
HZ = HC.z + 0.4
brim = [(HZ + 0.03, 0.56, 0.54, HC.y + 0.03), (HZ - 0.01, 0.85, 0.82, HC.y + 0.03), (HZ - 0.07, 1.05, 1.0, HC.y + 0.03)]
B.loft([ellipse((0, c, z), rx, ry, 20) for z, rx, ry, c in brim], lambda i, j: HAT, "Head", inner=ROBE_DK,
       inner_shrink=0.98, axis_pts=[(0, c, z - 0.5) for z, _, _, c in brim], seal=True, seal_top=True)
cone = [(HZ, 0.6, 0.0), (HZ + 0.28, 0.5, 0.0), (HZ + 0.56, 0.37, 0.03), (HZ + 0.8, 0.25, 0.1), (HZ + 0.98, 0.15, 0.22),
        (HZ + 1.06, 0.08, 0.38), (HZ + 1.04, 0.02, 0.52)]
B.loft([ellipse((0, HC.y + 0.03 + dy, z), r, r, 12) for z, r, dy in cone], lambda i, j: HAT, "Head",
       axis_pts=[(0, HC.y + 0.03 + dy, z) for z, r, dy in cone])
HTR = [(z, r, r, HC.y + 0.03 + dy) for z, r, dy in cone[:3]]
pts, nrm = surf_path(HTR, [(a * 15.0, HZ + 0.1) for a in range(25)], 0.005)
B.ribbon(GOLD, "Head", pts, nrm, 0.11, 0.03)
c, n = surf(HTR, 0.0, HZ + 0.3, 0.01)
star = [(math.sin(2 * math.pi * k / 10) * (0.09 if k % 2 == 0 else 0.04), math.cos(2 * math.pi * k / 10) * (0.09 if k % 2 == 0 else 0.04))
        for k in range(10)]
B.prism(GOLD, "Head", [c + Vector((x, 0, z)) for x, z in star], 0.03, (0, 1, 0), True)
B.no_jitter = False
scale_new_verts(B, _body_verts, HEAD_PIVOT, HEAD_SCALE)

# ---------------------------------------------------------------- staff in the left hand: wood, gold prongs holding a blue orb
grip = bone_pos("LeftForeArm", True).lerp(bone_pos("LeftHand", True), 0.42) + Vector((0, -0.02, 0))
gx, gy = grip.x, grip.y
B.limb(WOOD, "LeftHand", (gx, gy, 0.1), (gx, gy, 2.35), 0.045, 0.05, 6)
for z in (0.3, 1.8, 2.3):
    B.limb(GOLD, "LeftHand", (gx, gy, z - 0.03), (gx, gy, z + 0.03), 0.055, 0.055, 6)
B.limb(GOLD, "LeftHand", (gx, gy, 0.12), (gx, gy, -0.02), 0.045, 0.0, 4)
for k in range(3):
    a = 2 * math.pi * k / 3
    d = Vector((math.cos(a), math.sin(a), 0))
    pts = [Vector((gx, gy, 2.35)) + d * 0.04, Vector((gx, gy, 2.45)) + d * 0.16, Vector((gx, gy, 2.65)) + d * 0.2,
           Vector((gx, gy, 2.82)) + d * 0.1]
    for p0, p1 in zip(pts, pts[1:]):
        B.limb(GOLD, "LeftHand", p0, p1, 0.03, 0.025, 5)
B.ico(ORB, "LeftHand", (gx, gy, 2.6), 0.16, 2)

export(B, "MageMesh", "Mage", "mage.fbx", "mage.blend")
