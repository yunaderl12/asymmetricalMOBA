# Builds the low-poly Priest (chibi, same style and skeleton as the Knight; design from the hooded priest reference) and exports FBX for Unity.
# Shared pieces (skeleton, mesh builder, head / face / hair helpers, export) live in char_common.py.
# Run headless:
#   /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup -P ArtSource/characters/build_priest.py -- "<repo root>"
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from char_common import *  # noqa: E402,F401,F403

MAT_PREFIX[0] = "Priest_"
SKIN = mat("Skin", "E0B69E", rough=0.7, spec=0.2)
HAIR = mat("Hair", "A07258", rough=0.8, spec=0.15)
HAIR_DK = mat("HairShade", "865A40", rough=0.85, spec=0.1)
NAVY = mat("Navy", "2E3355")
NAVY_IN = mat("NavyLining", "232742")
PINK = mat("Cape", "CC4AA0")
PINK_IN = mat("CapeLining", "962371")
WHITE = mat("Cloth", "D8CEC0")
GOLD = mat("Gold", "CDA250", rough=0.45, metal=0.3, spec=0.45)
GOLD_DK = mat("GoldShade", "A58038", rough=0.5, metal=0.3, spec=0.4)
BELT = mat("Belt", "8A6038", rough=0.75, spec=0.15)
BELT_DK = mat("BeltShade", "64442A", rough=0.8, spec=0.1)
WOOD = mat("Staff", "4A3020", rough=0.8)
DARK = mat("Dark", "2E1E1A", rough=0.6, spec=0.2)
MOUTH = mat("Mouth", "8A5040", rough=0.7)
BOOT = mat("Boot", "33385A")
EYE_BROWN = mat("Eye", "5E3E2C", rough=0.4, spec=0.4)
EYE_DK = mat("EyeShade", "3A261C", rough=0.4, spec=0.3)
EYE_HL = mat("EyeHighlight", "9A7A62", rough=0.3, spec=0.5)
LID = mat("Lid", "4A3024", rough=0.6, spec=0.2)
BROW = mat("Brow", "6A4432", rough=0.8)


B = Body()


def fleur(w, c, nrm, s=1.0):
    """Gold fleur-de-lis on a surface: tall centre petal, two curled side petals, a band and a small foot."""
    nrm = Vector(nrm).normalized()
    up = Vector((0, 0, 1))
    up = (up - nrm * up.dot(nrm)).normalized()
    rt = up.cross(nrm)
    P = lambda u, v, h=0.0: c + rt * u * s + up * v * s + nrm * h
    B.gem(GOLD, w, P(0, 0.035), nrm, up, 0.028 * s, 0.07 * s, 0.022, 4, 0.35)
    for sx in (1, -1):
        pts = [P(sx * u, v) for u, v in ((0.012, -0.005), (0.04, 0.012), (0.066, 0.04), (0.07, 0.068), (0.052, 0.07))]
        B.ribbon(GOLD, w, pts, [nrm] * 5, taper(5, 0.026 * s, 0.014 * s), 0.018)
    B.ribbon(GOLD, w, [P(-0.045, -0.02), P(0.045, -0.02)], [nrm] * 2, 0.022 * s, 0.022)
    B.gem(GOLD, w, P(0, -0.048), nrm, up, 0.018 * s, 0.025 * s, 0.016, 4, 0.4)


# ---------------------------------------------------------------- legs: short white leggings, navy boots with gold cuffs
for side, sx in (("Left", 1), ("Right", -1)):
    up_a, up_b = bone_pos(side + "UpLeg"), bone_pos(side + "UpLeg", True)
    lo_b = bone_pos(side + "Leg", True)
    B.limb(WHITE, side + "UpLeg", up_a, up_b, 0.15, 0.135)
    B.limb(WHITE, side + "Leg", up_b, lo_b, 0.135, 0.125)
    foot_a, toe = bone_pos(side + "Foot"), bone_pos(side + "ToeBase", True)
    fx = foot_a.x + sx * 0.01
    B.box(BOOT, side + "Foot", (fx, (foot_a.y + toe.y) / 2 + 0.01, 0.08), (0.26, 0.42, 0.16), bevel=0.04)
    B.box(BOOT, side + "Foot", (fx, foot_a.y, 0.15), (0.3, 0.3, 0.14), bevel=0.03)
    B.box(GOLD, side + "Foot", (fx, foot_a.y, 0.225), (0.33, 0.33, 0.05), bevel=0.015)
    B.box(GOLD, side + "Foot", (fx, toe.y + 0.07, 0.07), (0.22, 0.07, 0.07), bevel=0.02)
    B.gem(GOLD, side + "Foot", (fx, toe.y + 0.02, 0.155), (0, -0.6, 1), (0, 1, 0), 0.05, 0.05, 0.02, 4)
    B.gem(GOLD, side + "Foot", (fx, foot_a.y - 0.165, 0.225), (0, -1, 0), (0, 0, 1), 0.04, 0.05, 0.02, 4)

# ---------------------------------------------------------------- robe skirt: navy outer robe open over a white underskirt, gold trims
SK_N = 36
SKC = -0.06
skirt_rows = ((0.88, 0.34, 0.26), (0.66, 0.45, 0.33), (0.52, 0.525, 0.37), (0.44, 0.56, 0.385), (0.38, 0.585, 0.395))
SKR = [(z, rx, ry, SKC) for z, rx, ry in skirt_rows]


def skirt_panel(z):
    """Half-angle (degrees) of the white underskirt at height z: narrow under the belt, wider at the hem."""
    return 13 + (24 - 13) * min(1.0, max(0.0, (0.86 - z) / 0.46))


def skirt_mat(i, j):
    a = (j + 0.5) * 360.0 / SK_N
    z = (skirt_rows[i][0] + skirt_rows[i + 1][0]) / 2
    return WHITE if min(a, 360 - a) < skirt_panel(z) else NAVY


B.loft([ellipse((0, SKC, z), rx, ry, SK_N) for z, rx, ry in skirt_rows],
       skirt_mat, "Hips", inner=NAVY_IN, inner_shrink=0.94,
       seal=True)
for sx in (1, -1):
    pts, nrm = surf_path(SKR, [(sx * skirt_panel(z), z) for z in (0.86, 0.76, 0.66, 0.56, 0.48, 0.43)])
    B.ribbon(GOLD, "Hips", pts, nrm, 0.07, 0.03)
hem = [(a * 10.0, 0.425) for a in range(37)]
pts, nrm = surf_path(SKR, hem, 0.01)
B.ribbon(GOLD, "Hips", pts, nrm, 0.1, 0.035)
for sx in (1, -1):
    c, n = surf(SKR, math.radians(sx * 52), 0.57, 0.01)
    fleur("Hips", c, n, 1.15)
c, n = surf(SKR, 0.0, 0.55, 0.01)
fleur("Hips", c, n, 0.95)
for sx in (1, -1):
    pts, nrm = surf_path(SKR, smooth([(sx * 3, 0.52), (sx * 8, 0.55), (sx * 11, 0.52)], 3))
    B.ribbon(GOLD, "Hips", pts, nrm, 0.018, 0.015)

# belt with a gold diamond buckle and studs
BR = [(0.9, 0.35, 0.27, -0.06), (0.72, 0.38, 0.29, -0.06)]
B.loft([ellipse((0, -0.06, z), rx, ry, SK_N) for z, rx, ry, _ in BR], lambda i, j: BELT, "Hips", inner=BELT_DK,
       inner_shrink=0.93, seal=True)
for z in (0.895, 0.725):
    pts, nrm = surf_path(BR, [(a * 15.0, z) for a in range(25)], 0.004)
    B.ribbon(BELT_DK, "Hips", pts, nrm, 0.014, 0.01)
c, n = surf(BR, 0.0, 0.81, 0.0)
B.gem(GOLD, "Hips", c, n, (0, 0, 1), 0.14, 0.11, 0.045, 4, 0.7)
B.gem(BELT, "Hips", c + n * 0.03, n, (0, 0, 1), 0.06, 0.05, 0.025, 4, 0.55)
for a in (-48, -26, 26, 48):
    c, n = surf(BR, math.radians(a), 0.81, 0.0)
    B.gem(GOLD, "Hips", c, n, (0, 0, 1), 0.042, 0.046, 0.025, 4, 0.45)
# navy hip panels edged in gold (the side "pockets" of the robe)
for sx in (1, -1):
    path = smooth([(sx * 78, 0.7), (sx * 80, 0.6), (sx * 90, 0.57), (sx * 100, 0.6), (sx * 102, 0.7)], 3)
    pts, nrm = surf_path(SKR, path, 0.02)
    B.ribbon(GOLD, "Hips", pts, nrm, 0.04, 0.025)
# diagonal sash from the right hip, tucked under the white panel; pouch on the left hip
path = smooth([(-70, 0.73), (-48, 0.68), (-30, 0.62), (-17, 0.58)], 3)
for out, wd, m in ((0.012, 0.11, GOLD), (0.02, 0.08, NAVY_IN)):
    pts, nrm = surf_path(SKR, path, out)
    B.ribbon(m, "Hips", pts, nrm, wd, 0.02, 0.2)
rot = Matrix.Rotation(math.radians(32), 4, "Z")
B.box(BELT, "Hips", (0.39, -0.24, 0.53), (0.27, 0.22, 0.26), rot, bevel=0.05)
B.box(BELT_DK, "Hips", (0.39, -0.24, 0.67), (0.17, 0.14, 0.05), rot, bevel=0.015)
B.box(BELT, "Hips", (0.43, -0.31, 0.6), (0.29, 0.08, 0.15), rot @ Matrix.Rotation(-0.25, 4, "X"), bevel=0.025)
B.gem(GOLD, "Hips", (0.46, -0.36, 0.58), (math.sin(math.radians(32)), -math.cos(math.radians(32)), 0), (0, 0, 1),
      0.04, 0.045, 0.02, 6, 0.5)
B.limb(BELT_DK, "Hips", (0.31, -0.24, 0.8), (0.36, -0.25, 0.68), 0.02, 0.02, 4)

# ---------------------------------------------------------------- torso: navy robe, white front panel framed in gold, ornate cross
TO_N = 36
TOC = -0.07
torso_rows = ((1.33, 0.28, 0.2), (1.18, 0.32, 0.23), (1.02, 0.29, 0.22), (0.84, 0.3, 0.23))
TOR = [(z, rx, ry, TOC) for z, rx, ry in torso_rows]


def panel_w(z):
    """Half-angle (degrees) of the white chest panel at height z."""
    return 16 + (30 - 16) * min(1.0, max(0.0, (z - 0.86) / 0.3))


def torso_mat(i, j):
    a = (j + 0.5) * 360.0 / TO_N
    z = (torso_rows[i][0] + torso_rows[i + 1][0]) / 2
    return WHITE if min(a, 360 - a) < panel_w(z) else NAVY


B.loft([ellipse((0, TOC, z), rx, ry, TO_N) for z, rx, ry in torso_rows], torso_mat, blend(0.95, "Spine02", 1.22, "Spine"))
for sx in (1, -1):
    path = [(sx * panel_w(z), z) for z in (0.86, 0.96, 1.06, 1.16, 1.26, 1.33)]
    pts, nrm = surf_path(TOR, path, 0.008)
    B.ribbon(GOLD, "Spine01", pts, nrm, 0.065, 0.03)
# cross pattee with a raised inner step and small scrolls
CZ = 1.1
cross = [(-0.045, 0.1), (0.045, 0.1), (0.022, 0.025), (0.1, 0.045), (0.1, -0.045), (0.022, -0.025),
         (0.05, -0.16), (-0.05, -0.16), (-0.022, -0.025), (-0.1, -0.045), (-0.1, 0.045), (-0.022, 0.025)]
c0, n0 = surf(TOR, 0.0, CZ, 0.0)
for scale, lift, m in ((1.12, 0.012, GOLD_DK), (1.0, 0.03, GOLD), (0.55, 0.045, GOLD)):
    B.prism(m, "Spine01", [c0 + Vector((x * scale, -lift, z * scale)) for x, z in cross], 0.02, (0, 1, 0), True)
for sx in (1, -1):
    pts = [c0 + Vector((sx * x, -0.015, z)) for x, z in ((0.03, -0.12), (0.08, -0.1), (0.1, -0.05), (0.08, -0.03))]
    B.ribbon(GOLD, "Spine01", pts, [(0, -1, 0)] * 4, taper(4, 0.02, 0.012), 0.015)
B.limb(SKIN, "neck", bone_pos("neck"), bone_pos("neck", True), 0.085, 0.08)
NKR = [(1.56, 0.115, 0.11, -0.13), (1.4, 0.14, 0.13, -0.12)]
B.loft([ellipse((0, c, z), rx, ry, 10) for z, rx, ry, c in NKR], lambda i, j: WHITE, "Spine", inner=WHITE, seal=True)
pts, nrm = surf_path(NKR, [(a * 36.0, 1.5) for a in range(11)], 0.004)
B.ribbon(GOLD, "Spine", pts, nrm, 0.045, 0.03)
c, n = surf(NKR, 0.0, 1.5, 0.02)
B.gem(GOLD, "Spine", c, n, (0, 0, 1), 0.035, 0.035, 0.02, 4)

# ---------------------------------------------------------------- magenta cape behind, hanging from under the collar
cape_rows = ((1.28, 0.3, 0.22), (1.0, 0.52, 0.42), (0.6, 0.72, 0.6), (0.22, 0.82, 0.68))
B.loft([ellipse((0, -0.02, z), rx, ry, 10, math.radians(95), math.radians(265), closed=False) for z, rx, ry in cape_rows],
       lambda i, j: PINK, blend(0.8, "Hips", 1.15, "Spine01"), closed=False, inner=PINK_IN, inner_shrink=0.96,
       axis_pts=[(0, -0.02, z) for z, _, _ in cape_rows], seal=True)

# ---------------------------------------------------------------- mantle collar: pink stand-up band, cream shoulder piece, gold piping
CO_N = 16
collar_rows = ((1.52, 0.17, 0.15, -0.12), (1.42, 0.44, 0.33, -0.1), (1.32, 0.68, 0.48, -0.08), (1.25, 0.72, 0.51, -0.08))
gap = math.radians(22)


def collar_ring(z, rx, ry, c, drop):
    pts = ellipse((0, c, z), rx, ry, CO_N, gap, 2 * math.pi - gap, closed=False)
    out = []
    for x, y, pz in pts:
        back = (1 - math.cos(math.atan2(x, -(y - c)))) / 2
        out.append((x, y, pz - drop * back ** 3))
    return out


coll = [collar_ring(z, rx, ry, c, d) for (z, rx, ry, c), d in zip(collar_rows, (0.0, 0.06, 0.16, 0.19))]
co_axis = [(0, c, z) for z, _, _, c in collar_rows]
B.loft(coll, lambda i, j: PINK if i == 0 else WHITE, "Spine", closed=False, inner=WHITE, inner_shrink=0.95,
       axis_pts=co_axis, seal=True)




def collar_line(i, t):
    """Points along the collar between ring i and i+1 (t in 0..1), with normals."""
    pts, nrm = [], []
    for j in range(CO_N + 1):
        p = Vector(coll[i][j]).lerp(Vector(coll[i + 1][j]), t) if t > 0 else Vector(coll[i][j])
        nn = grid_normal(coll, co_axis, i if t < 0.5 else min(i + 1, 3), j)
        pts.append(p + nn * 0.012)
        nrm.append(nn)
    return pts, nrm


for i, t, wd in ((0, 1.0, 0.05), (2, 0.85, 0.08)):
    pts, nrm = collar_line(i, t)
    B.ribbon(GOLD, "Spine", pts, nrm, wd, 0.035)
for j in (0, CO_N):
    pts = [Vector(coll[i][j]) for i in range(1, 4)]
    nrm = [grid_normal(coll, co_axis, i, j) for i in range(1, 4)]
    B.ribbon(GOLD, "Spine", [p + n * 0.012 for p, n in zip(pts, nrm)], nrm, 0.06, 0.035)
for sx in (1, -1):
    # cream lapels folding down to the cross, edged in gold
    lapel = [Vector(p) for p in ((sx * 0.11, -0.36, 1.47), (sx * 0.34, -0.33, 1.38), (sx * 0.06, -0.37, 1.18),
                                 (sx * 0.03, -0.36, 1.25))]
    mid = sum(lapel, Vector()) / 4
    B.prism(GOLD, "Spine01", [mid + (p - mid) * 1.2 + Vector((0, 0.012, 0)) for p in lapel], 0.03, (0, 1, 0.2), True)
    B.prism(WHITE, "Spine01", lapel, 0.035, (0, 1, 0.2), True)
    # pink stand-up collar tips with hexagonal gold gems
    B.prism(PINK, "Spine", [(sx * 0.07, -0.41, 1.45), (sx * 0.33, -0.36, 1.42), (sx * 0.31, -0.41, 1.3), (sx * 0.12, -0.44, 1.29)], 0.04,
            (sx * 0.3, -0.6, 0.75), True)
    gn = Vector((sx * 0.25, -0.9, 0.35)).normalized()
    B.gem(GOLD, "Spine", Vector((sx * 0.21, -0.43, 1.37)), gn, (0, 0, 1), 0.055, 0.055, 0.03, 6, 0.6)
    B.gem(PINK_IN, "Spine", Vector((sx * 0.21, -0.43, 1.37)) + gn * 0.025, gn, (0, 0, 1), 0.03, 0.03, 0.012, 6, 0.6)

# ---------------------------------------------------------------- arms: navy bell sleeves, cream cuff band between gold rings
for side, sx in (("Left", 1), ("Right", -1)):
    arm_a, arm_b = bone_pos(side + "Arm"), bone_pos(side + "Arm", True)
    fa_b, hand_b = bone_pos(side + "ForeArm", True), bone_pos(side + "Hand", True)
    B.ico(NAVY, side + "Shoulder", arm_a + Vector((-sx * 0.05, 0, 0.0)), 0.16, 1, (1.1, 1, 0.95))
    B.limb(NAVY, side + "Arm", arm_a, arm_b, 0.15, 0.16, 8)
    cuff_a = fa_b - Vector((sx * 0.11, 0, 0))
    B.limb(NAVY, side + "ForeArm", arm_b, cuff_a, 0.15, 0.2, 8)
    B.limb(WHITE, side + "ForeArm", cuff_a, fa_b, 0.205, 0.215, 8)
    for x, wd in ((cuff_a, 0.035), (fa_b, 0.04)):
        B.limb(GOLD, side + "ForeArm", x - Vector((sx * wd / 2, 0, 0)), x + Vector((sx * wd / 2, 0, 0)), 0.232, 0.236, 8)
    B.limb(NAVY_IN, side + "ForeArm", fa_b + Vector((sx * 0.015, 0, 0)), fa_b + Vector((sx * 0.022, 0, 0)), 0.19, 0.19, 8)
    # gold tendrils on the front of the sleeve
    L = (cuff_a - arm_b).length

    def sleeve_pt(t, dz, lift=0.012):
        p = arm_b.lerp(cuff_a, t)
        r = 0.15 + (0.2 - 0.15) * t
        y = -math.sqrt(max(0.0, r * r - dz * dz))
        nn = Vector((-sx * 0.11 / L, y, dz)).normalized()
        return p + Vector((0, y, dz)) + nn * lift, nn

    for dz_sign in (1, -1):
        ctrl = [(0.35, 0.0), (0.5, 0.06), (0.66, 0.1), (0.8, 0.07), (0.78, 0.02)]
        res = [sleeve_pt(t, dz_sign * dz) for t, dz in smooth(ctrl, 2)]
        B.ribbon(GOLD, side + "ForeArm", [p for p, _ in res], [n for _, n in res],
                 taper(len(res), 0.03, 0.014), 0.018)
    p, nn = sleeve_pt(0.28, 0.0)
    B.gem(GOLD, side + "ForeArm", p, nn, (0, 0, 1), 0.03, 0.04, 0.018, 4)
    # hand: tapered mitten plus a thumb, like the Knight
    hand(B, SKIN, side, sx)

# ---------------------------------------------------------------- head: soft low-poly face (cheeks, nose bridge, tapered chin)
# The head, hair and hood are modelled at a comfortable size and then scaled down about the neck (HEAD_SCALE)
# so the head-to-body ratio matches the reference (hood ~44% of the height).
HEAD_SCALE, HEAD_PIVOT = 0.72, Vector((0.0, -0.13, 1.47))
_body_verts = set(B.bm.verts)
B.no_jitter = True
HC = Vector((0.0, -0.17, 1.99))
K = HeadKit(B, HC)
K.face(SKIN, EYE_BROWN, EYE_DK, EYE_HL, LID, BROW, MOUTH)

# ---------------------------------------------------------------- hair: cap, centre-parted bangs in thick strands, side locks to the jaw
K.hair_cap(HAIR)
strand = K.strand
for sx in (1, -1):
    strand(HAIR, [(sx * 2, 62), (sx * 10, 50), (sx * 22, 38), (sx * 36, 24), (sx * 48, 10)], 0.22, 0.1, 0.035, 0.05)
    strand(HAIR, [(sx * 4, 74), (sx * 24, 58), (sx * 44, 40), (sx * 58, 18), (sx * 64, -4)], 0.22, 0.12, 0.05, 0.065)
    strand(HAIR, [(sx * 30, 88), (sx * 62, 62), (sx * 78, 30), (sx * 82, 0)], 0.24, 0.14, 0.05, 0.07)
    strand(HAIR, [(sx * 50, 30), (sx * 55, 4), (sx * 59, -22), (sx * 60, -44)], 0.22, 0.15, 0.07, 0.1, 0.06, 0.12)
    strand(HAIR, [(sx * 78, 26), (sx * 84, 0), (sx * 86, -26), (sx * 84, -46)], 0.2, 0.13, 0.045, 0.065, 0.06, 0.2)
    strand(HAIR_DK, [(sx * 98, 20), (sx * 106, -6), (sx * 110, -36)], 0.2, 0.12, 0.03, 0.04, 0.06, 0.2)
B.no_jitter = False

FACE_SCALE = 1.08
for v in B.bm.verts:
    if v not in _body_verts:
        v.co = HC + (v.co - HC) * FACE_SCALE

# ---------------------------------------------------------------- navy hood: thick faceted shell, bevelled gold rim with a pointed arch
B.no_jitter = True
HOOD_TOP = 3.16
hood_rows = ((2.98, 0.4, 0.42, 0.0, 0), (2.76, 0.68, 0.64, -0.04, 0), (2.5, 0.78, 0.67, -0.06, 22),
             (2.22, 0.83, 0.68, -0.06, 34), (1.95, 0.84, 0.67, -0.05, 38), (1.7, 0.84, 0.66, -0.03, 40),
             (1.5, 0.78, 0.58, 0.0, 43))
HO_N = 12
hood = [ellipse((0, c, z), rx, ry, HO_N, math.radians(a), math.radians(360 - a), closed=False)
        for z, rx, ry, c, a in hood_rows]
hood_w = blend(1.5, "neck", 1.8, "Head")
B.loft([ellipse((0, 0.0, HOOD_TOP), 0.01, 0.01, HO_N, 0.0, 2 * math.pi, closed=False)] + hood,
       lambda i, j: NAVY, hood_w, closed=False, inner=NAVY_IN, inner_shrink=0.93,
       axis_pts=[(0, 0.0, HOOD_TOP)] + [(0, c, z) for z, _, _, c, _ in hood_rows], seal=True)
HOR = [(HOOD_TOP, 0.01, 0.01, 0.0)] + [(z, rx, ry, c) for z, rx, ry, c, _ in hood_rows]
rim = [Vector(r[-1]) for r in reversed(hood[3:])] + [surf(HOR, math.radians(-14), 2.56, -0.01)[0], surf(HOR, 0.0, 2.6, -0.01)[0], surf(HOR, math.radians(14), 2.56, -0.01)[0]] + [Vector(r[0]) for r in hood[3:]]
rim_n = []
for p in rim:
    out = Vector((p.x, 0, p.z - 2.05)).normalized()
    rim_n.append((Vector((0, -1, 0)) + out * 0.35).normalized())
B.ribbon(GOLD, hood_w, [p + Vector((0, 0.03, 0)) for p in rim], rim_n, 0.11, 0.065, 0.4)
# crest: tall gold diamond at the arch point, scrollwork spreading to both sides
c, n = surf(HOR, 0.0, 2.7, 0.0)
B.gem(GOLD, "Head", c, n, (0, 0, 1), 0.08, 0.13, 0.05, 4, 0.4)
for sx in (1, -1):
    for path, w0, w1 in (
            ([(sx * 5, 2.68), (sx * 13, 2.75), (sx * 24, 2.78), (sx * 35, 2.75), (sx * 40, 2.69), (sx * 36, 2.65)], 0.095, 0.03),
            ([(sx * 9, 2.65), (sx * 18, 2.62), (sx * 30, 2.56), (sx * 41, 2.45), (sx * 49, 2.32), (sx * 53, 2.18), (sx * 50, 2.12)],
             0.09, 0.03),
            ([(sx * 22, 2.61), (sx * 26, 2.67), (sx * 25, 2.71)], 0.06, 0.016),
            ([(sx * 38, 2.49), (sx * 44, 2.55), (sx * 45, 2.6)], 0.055, 0.016),
            ([(sx * 44, 2.3), (sx * 49, 2.15), (sx * 51, 2.0), (sx * 49, 1.88), (sx * 45, 1.9)], 0.08, 0.03),
            ([(sx * 49, 2.18), (sx * 56, 2.24), (sx * 59, 2.32)], 0.045, 0.016)):
        pts, nrm = surf_path(HOR, smooth(path, 3), 0.008)
        B.ribbon(GOLD, "Head", pts, nrm, taper(len(pts), w0, w1), 0.04, 0.5)
B.no_jitter = False
for v in B.bm.verts:
    if v not in _body_verts:
        v.co = HEAD_PIVOT + (v.co - HEAD_PIVOT) * HEAD_SCALE

# ---------------------------------------------------------------- staff in the left hand: twisted wood, gold cross with a sunburst
grip = bone_pos("LeftForeArm", True).lerp(bone_pos("LeftHand", True), 0.42) + Vector((0, -0.02, 0))
gx, gy = grip.x, grip.y
shaft = [Vector((gx + (0.008 if k % 2 else -0.008), gy + (0.006 if k % 3 == 0 else -0.004), 0.15 + k * 0.25)) for k in range(10)]
shaft[-1] = Vector((gx, gy, 2.42))
for a, b in zip(shaft, shaft[1:]):
    B.limb(WOOD, "LeftHand", a, b, 0.05, 0.047, 6)
for p in shaft[1:-1]:
    B.ico(WOOD, "LeftHand", p, 0.05, 1)
for z in (0.32, 2.0):
    B.limb(GOLD, "LeftHand", (gx, gy, z - 0.04), (gx, gy, z + 0.04), 0.052, 0.052, 6)
B.limb(GOLD, "LeftHand", (gx, gy, 0.16), (gx, gy, -0.02), 0.05, 0.0, 4)
B.ico(GOLD, "LeftHand", (gx, gy, 0.2), 0.06, 1)
B.limb(GOLD, "LeftHand", (gx, gy, 2.38), (gx, gy, 2.52), 0.045, 0.075, 6)
top = Vector((gx, gy, 2.9))
ring = [top + Vector((math.cos(2 * math.pi * k / 20) * 0.2, 0.0, math.sin(2 * math.pi * k / 20) * 0.2)) for k in range(21)]
B.ribbon(GOLD, "LeftHand", ring, [(0, -1, 0)] * 21, 0.045, 0.05, 0.3)
for z in (1.0, 1.6, 2.25):
    B.limb(GOLD, "LeftHand", (gx, gy, z - 0.03), (gx, gy, z + 0.03), 0.06, 0.06, 6)
for k in range(16):
    a = 2 * math.pi * k / 16 + math.radians(11.25)
    ln = 0.34 if k % 2 else 0.24
    B.limb(GOLD, "LeftHand", top + Vector((0, 0.03, 0)), top + Vector((math.cos(a) * ln, 0.03, math.sin(a) * ln)), 0.03, 0.0, 4)
B.box(GOLD, "LeftHand", (gx, gy, 2.86), (0.11, 0.07, 0.72))
B.box(GOLD, "LeftHand", (gx, gy, 2.92), (0.66, 0.07, 0.11))
B.box(WOOD, "LeftHand", (gx, gy - 0.025, 2.86), (0.035, 0.02, 0.6))
B.box(WOOD, "LeftHand", (gx, gy - 0.025, 2.92), (0.54, 0.02, 0.035))
for p in ((gx - 0.33, gy, 2.92), (gx + 0.33, gy, 2.92), (gx, gy, 3.22)):
    B.ico(GOLD, "LeftHand", p, 0.5, 1, (0.13, 0.07, 0.13))
    B.ico(WOOD, "LeftHand", (p[0], p[1] - 0.025, p[2]), 0.5, 1, (0.04, 0.02, 0.04))
export(B, "PriestMesh", "Priest", "priest.fbx", "priest.blend")
