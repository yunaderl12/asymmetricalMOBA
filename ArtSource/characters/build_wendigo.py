# 森のボスのウェンディゴを取り込む。外から来た骨なしのメッシュ（Tripo の FBX。鹿の頭骨と角、黒い毛皮の獣が
# 前かがみで四つ足をつき、後ろ足の片方を後ろへ伸ばした姿。頭は +X）に骨を入れて Resources/Wendigo/wendigo.fbx に書き出す。
# Unity 側の WendigoBoss が骨を回して、四つ足の駆け足・飛び掛かり・引っ掻き・雄叫びを作る。
#   - 背骨は Body（腰）→ Chest → Neck → Head。角は頭に付ける。
#   - 腕と脚は左右に3本ずつ（Arm → Fore → Hand、Thigh → Shin → Foot）。P は元の +Y 側、N は -Y 側。
#   - 元の大きさは高さ 0.87 くらい。高さ（角の先まで）が 1 になるよう縮めてから骨を入れる。原点は胴の真下の地面。
#   - 向き: 頭 +X を書き出しでは -Y にする（Unity の +Z）。
#   - 元は約190万面あるので、先に間引く。
# Run headless:
#   /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup -P ArtSource/characters/build_wendigo.py -- \
#       "<repo root>" "<tripo_convert_....fbx>"
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from boss_rig import chain_weights, load_source, rig_and_export, smooth  # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:]
ROOT, SOURCE = args[0], args[1]
TARGET_FACES = 18000
ORIGIN = Vector((-0.04, 0.0, 0.0))

# 背骨（高さ 1 にそろえた座標）。腰から鼻先まで。
SPINE = [Vector(p) for p in ((-0.26, 0, 0.42), (0.04, 0, 0.52), (0.20, 0, 0.58), (0.29, 0, 0.60), (0.53, 0, 0.47))]
SPINE_NAMES = ["Body", "Chest", "Neck", "Head"]


def side_chain(points, sy):
    return [Vector((x, y * sy, z)) for x, y, z in points]


# 腕: 肩 → ひじ（後ろへ曲がる）→ 手首 → 指先。左右ほぼ同じ。
ARM = ((0.14, 0.16, 0.52), (0.04, 0.22, 0.30), (0.36, 0.29, 0.08), (0.50, 0.31, 0.02))
# 脚: 腰 → ひざ → かかと → 足の裏の先。+Y 側は体の下に、-Y 側は後ろへ伸ばしている。
LEG = {
    1: ((-0.14, 0.15, 0.40), (-0.04, 0.16, 0.25), (-0.21, 0.17, 0.22), (-0.21, 0.17, 0.05), (0.0, 0.17, 0.02)),
    -1: ((-0.18, 0.15, 0.40), (-0.30, 0.16, 0.16), (-0.47, 0.17, 0.25), (-0.45, 0.17, 0.05), (-0.36, 0.17, 0.02)),
}
ARM_NAMES = ["Arm", "Fore", "Hand"]
LEG_NAMES = ["Thigh", "Shin", "Foot"]


def suffix(sy):
    return "P" if sy > 0 else "N"


BONES = [("Body", tuple(SPINE[0]), tuple(SPINE[1]), None)]
for i, name in enumerate(SPINE_NAMES[1:], start=1):
    BONES.append((name, tuple(SPINE[i]), tuple(SPINE[i + 1]), SPINE_NAMES[i - 1]))
ARM_CHAINS, LEG_CHAINS = {}, {}
for sy in (1, -1):
    s = suffix(sy)
    arm = side_chain(ARM, sy)
    ARM_CHAINS[sy] = arm
    for i, name in enumerate(ARM_NAMES):
        BONES.append((name + s, tuple(arm[i]), tuple(arm[i + 1]), "Chest" if i == 0 else ARM_NAMES[i - 1] + s))
    leg = [Vector((x, y * sy, z)) for x, y, z in LEG[sy]]
    LEG_CHAINS[sy] = leg
    for i, name in enumerate(LEG_NAMES):
        # 足の骨はかかとから足の裏まで。つま先も同じ骨に乗せる。
        BONES.append((name + s, tuple(leg[i]), tuple(leg[i + 1]), "Body" if i == 0 else LEG_NAMES[i - 1] + s))


def chain_dist(p, pts):
    best = 1e9
    for a, b in zip(pts, pts[1:]):
        ab = b - a
        t = max(0.0, min(1.0, (p - a).dot(ab) / ab.dot(ab)))
        best = min(best, (a + ab * t - p).length)
    return best


def weights_for(p):
    x, y, z = p
    w = {}
    # 角と頭: 頭骨より上の角は頭にだけ付ける。
    if z > 0.66 and x > 0.18:
        return {"Head": 1.0}
    limbs = 1.0 - smooth(0.36, 0.50, z)
    if x > 0.30 and z > 0.36:
        limbs = 0.0  # 頭骨の下あご
    if limbs > 0.0:
        sy = 1 if y >= 0 else -1
        s = suffix(sy)
        da = chain_dist(p, ARM_CHAINS[sy]) + 1e-3
        dl = chain_dist(p, LEG_CHAINS[sy]) + 1e-3
        arm_share = (1 / da ** 4) / (1 / da ** 4 + 1 / dl ** 4)
        if arm_share > 0.0:
            for bone, v in chain_weights(ARM_CHAINS[sy][:4], [n + s for n in ARM_NAMES], p, "Chest").items():
                w[bone] = w.get(bone, 0.0) + v * limbs * arm_share
        if arm_share < 1.0:
            leg = LEG_CHAINS[sy][:4]
            for bone, v in chain_weights(leg, [n + s for n in LEG_NAMES], p, "Body").items():
                w[bone] = w.get(bone, 0.0) + v * limbs * (1.0 - arm_share)
    rest = 1.0 - limbs
    if rest > 0.0:
        for bone, v in chain_weights(SPINE, SPINE_NAMES, p, "Body").items():
            w[bone] = w.get(bone, 0.0) + v * rest
    return w


mesh = load_source(SOURCE, "Wendigo_Body")
bpy.context.view_layer.objects.active = mesh
mesh.select_set(True)
dec = mesh.modifiers.new("Decimate", "DECIMATE")
dec.decimate_type = "COLLAPSE"
dec.ratio = TARGET_FACES / len(mesh.data.polygons)
bpy.ops.object.modifier_apply(modifier=dec.name)
if mesh.data.has_custom_normals:
    bpy.ops.mesh.customdata_custom_splitnormals_clear()

# 高さ 1・足元 0・左右と前後の中心 0 にそろえる。
co = [v.co for v in mesh.data.vertices]
lo = Vector((min(c.x for c in co), min(c.y for c in co), min(c.z for c in co)))
hi = Vector((max(c.x for c in co), max(c.y for c in co), max(c.z for c in co)))
h = hi.z - lo.z
mesh.data.transform(Matrix.Diagonal((1 / h, 1 / h, 1 / h, 1)) @ Matrix.Translation(-Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))))
mesh.data.update()
print("NORMALIZED faces", len(mesh.data.polygons), "height", h)

FIX = Matrix.Rotation(math.radians(-90), 4, "Z") @ Matrix.Translation(-ORIGIN)
rig_and_export(ROOT, mesh, BONES, weights_for, FIX, "Wendigo", "wendigo.fbx", "Wendigo_Body.jpg", "wendigo_tripo.blend", SOURCE)
