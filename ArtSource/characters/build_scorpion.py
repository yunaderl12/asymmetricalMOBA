# 砂漠のボスのスコーピオンを取り込む。外から来た骨なしのメッシュ（Tripo の FBX。幅約1・高さ約0.9、頭が -Y、
# 尾が背中から持ち上がって頭の上へ反り返った姿）に骨を入れて Resources/Scorpion/scorpion.fbx に書き出す。
# Unity 側の ScorpionBody が骨を回して、脚の歩き・尾の毒針・爪の振りを作る。
#   - 尾は付け根から毒針まで5本の鎖。爪は腕とはさみの2本ずつ。脚は左右2本ずつ。
#   - 原点は胴の真下の地面。向きはそのまま（頭 -Y が Unity の +Z）。
# Run headless:
#   /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup -P ArtSource/characters/build_scorpion.py -- \
#       "<repo root>" "<low_poly_scorpion_3d_model.fbx>"
import os
import sys

from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from boss_rig import chain_weights, load_source, rig_and_export, sided, smooth  # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:]
ROOT, SOURCE = args[0], args[1]
ORIGIN = Vector((0.0, 0.05, 0.0))

# 尾の付け根から毒針の先まで（元の座標）。尾は後ろへ上がってから前へ反り返る。
TAIL = [Vector(p) for p in ((0, 0.30, 0.36), (0, 0.40, 0.50), (0, 0.38, 0.65), (0, 0.28, 0.78), (0, 0.10, 0.83), (0, -0.23, 0.58))]
TAIL_NAMES = ["Tail1", "Tail2", "Tail3", "Tail4", "Stinger"]

BONES = [("Body", (0, 0.15, 0.25), (0, -0.15, 0.25), None)]
for i, name in enumerate(TAIL_NAMES):
    BONES.append((name, tuple(TAIL[i]), tuple(TAIL[i + 1]), "Body" if i == 0 else TAIL_NAMES[i - 1]))
LEG_FRONT = ((0.15, 0.08, 0.25), (0.45, 0.17, 0.03))
LEG_REAR = ((0.13, 0.25, 0.25), (0.35, 0.41, 0.03))
sided(BONES, [
    ("Arm", (0.14, -0.08, 0.25), (0.30, -0.19, 0.25), "Body", 0),
    ("Pincer", (0.30, -0.20, 0.25), (0.36, -0.48, 0.20), "Arm", 0),
    ("LegFront", LEG_FRONT[0], LEG_FRONT[1], "Body", 0),
    ("LegRear", LEG_REAR[0], LEG_REAR[1], "Body", 0),
])


def seg_dist(p, a, b):
    a, b = Vector(a), Vector(b)
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / ab.dot(ab)))
    return (a + ab * t - p).length


def weights_for(p):
    x, y, z = p
    ax = abs(x)
    side = "P" if x >= 0 else "N"
    mirrored = Vector((ax, y, z))
    w = {}

    tail = max(smooth(0.46, 0.52, z), smooth(0.27, 0.32, y) * smooth(0.30, 0.36, z)) * (1.0 - smooth(0.15, 0.22, ax))
    claw = smooth(0.12, 0.18, ax) * smooth(-0.03, -0.09, y) * (1.0 - tail)
    legs = smooth(0.13, 0.20, ax) * smooth(-0.06, 0.0, y) * (1.0 - smooth(0.34, 0.40, z)) * (1.0 - tail) * (1.0 - claw)

    if tail > 0:
        for bone, v in chain_weights(TAIL, TAIL_NAMES, p, "Body").items():
            w[bone] = w.get(bone, 0.0) + v * tail
    if claw > 0:
        pincer = smooth(-0.19, -0.25, y)
        w["Arm" + side] = claw * (1.0 - pincer)
        w["Pincer" + side] = claw * pincer
    if legs > 0:
        df = seg_dist(mirrored, *LEG_FRONT) + 1e-3
        dr = seg_dist(mirrored, *LEG_REAR) + 1e-3
        front = (1 / df ** 4) / (1 / df ** 4 + 1 / dr ** 4)
        w["LegFront" + side] = legs * front
        w["LegRear" + side] = legs * (1.0 - front)
    w["Body"] = max(0.0, 1.0 - tail - claw - legs)
    return w


mesh = load_source(SOURCE, "Scorpion_Body")
FIX = Matrix.Translation(-ORIGIN)
rig_and_export(ROOT, mesh, BONES, weights_for, FIX, "Scorpion", "scorpion.fbx", "Scorpion_Body.jpg", "scorpion_tripo.blend", SOURCE)
