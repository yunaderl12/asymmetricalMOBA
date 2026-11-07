# 中ボスのドラゴンを取り込む。外から来た骨なしのメッシュ（Tripo の FBX。高さ約0.5、頭が +X、翼が ±Y に広がった立ち姿）に、
# 羽ばたき・首・尻尾・脚を動かすための骨を入れて Resources/Dragon/dragon.fbx に書き出す。
# アニメーションのクリップは持たない。Unity 側の DragonBody が骨を毎フレーム回して、飛ぶ姿を作る。
#   - 向きは他のキャラに合わせて頭が -Y（Unity で +Z）。原点は胴の中ほど（飛ぶときの回転の中心）。
#   - 重みは位置で決める。翼は付け根から先へ2本、尻尾は3本、首と頭、脚は4本。
#   - マテリアルはテクスチャを持たない。URP のマテリアルは Unity 側で作る（Resources/Dragon/Dragon_BodyMat）。
# Run headless:
#   /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup -P ArtSource/characters/build_dragon.py -- \
#       "<repo root>" "<dragon_3d_model.fbx>"
import math
import os
import sys

from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from boss_rig import load_source, rig_and_export, smooth  # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:]
ROOT, SOURCE = args[0], args[1]

# 元のメッシュの座標（頭 +X、翼 ±Y、上 +Z）で書く。最後に ORIGIN を原点へ移して Z 軸まわりに -90度回す。
ORIGIN = Vector((0.2, 0.0, 0.22))


# 骨: 名前, 付け根, 先, 親。y の符号つきの骨は左右に作る（P = +Y 側、N = -Y 側）。
BONES = [
    ("Body", (0.10, 0, 0.20), (0.26, 0, 0.22), None),
    ("Neck", (0.27, 0, 0.29), (0.35, 0, 0.35), "Body"),
    ("Head", (0.36, 0, 0.36), (0.47, 0, 0.36), "Neck"),
    ("Tail1", (0.06, 0, 0.15), (-0.08, 0, 0.11), "Body"),
    ("Tail2", (-0.08, 0, 0.11), (-0.24, 0, 0.05), "Tail1"),
    ("Tail3", (-0.24, 0, 0.05), (-0.46, 0, 0.10), "Tail2"),
]
SIDED = [
    ("Wing", (0.24, 0.07, 0.30), (0.20, 0.27, 0.40), "Body"),
    ("WingTip", (0.20, 0.27, 0.40), (0.15, 0.50, 0.48), "Wing"),
    ("LegFront", (0.33, 0.06, 0.19), (0.35, 0.08, 0.0), "Body"),
    ("LegBack", (0.13, 0.06, 0.15), (0.13, 0.09, 0.0), "Body"),
]
for side, sy in (("P", 1.0), ("N", -1.0)):
    for name, head, tail, parent in SIDED:
        BONES.append((name + side, (head[0], head[1] * sy, head[2]), (tail[0], tail[1] * sy, tail[2]),
                      parent if parent == "Body" else parent + side))

# 鎖になった骨の、付け根の x（元の座標）。その間の頂点は前後の骨に分けて乗せる。
TAIL_CHAIN = [("Body", 0.08), ("Tail1", 0.0), ("Tail2", -0.15), ("Tail3", -0.30)]
NECK_CHAIN = [("Body", 0.22), ("Neck", 0.30), ("Head", 0.38)]


def chain_weights(chain, x, descending):
    """x に沿って並んだ骨へ、前後の2本で直線的に分ける。"""
    xs = [c[1] for c in chain]
    if descending:
        if x >= xs[0]:
            return {chain[0][0]: 1.0}
        if x <= xs[-1]:
            return {chain[-1][0]: 1.0}
        for (a, xa), (b, xb) in zip(chain, chain[1:]):
            if xb <= x <= xa:
                t = (xa - x) / (xa - xb)
                return {a: 1.0 - t, b: t}
    else:
        if x <= xs[0]:
            return {chain[0][0]: 1.0}
        if x >= xs[-1]:
            return {chain[-1][0]: 1.0}
        for (a, xa), (b, xb) in zip(chain, chain[1:]):
            if xa <= x <= xb:
                t = (x - xa) / (xb - xa)
                return {a: 1.0 - t, b: t}
    return {chain[0][0]: 1.0}


def weights_for(p):
    x, y, z = p
    ay = abs(y)
    side = "P" if y >= 0 else "N"
    w = {}

    # 翼: 胴から横へ出た膜。脚（低い所）と尻尾（後ろ）は除く。
    wing = smooth(0.075, 0.15, ay) * smooth(0.20, 0.26, z) * smooth(-0.02, 0.05, x) * (1.0 - smooth(0.38, 0.42, x))
    tip = smooth(0.24, 0.34, ay)
    if wing > 0:
        w["Wing" + side] = wing * (1.0 - tip)
        w["WingTip" + side] = wing * tip

    # 脚: 胴の下へ出た部分。中心線（腹）は動かさない。
    lateral = smooth(0.012, 0.035, ay) * (1.0 - smooth(0.13, 0.16, ay))
    front = smooth(0.19, 0.13, z) * smooth(0.26, 0.29, x) * (1.0 - smooth(0.41, 0.44, x)) * lateral
    back = smooth(0.15, 0.09, z) * smooth(0.03, 0.06, x) * (1.0 - smooth(0.21, 0.24, x)) * lateral
    rest = 1.0 - wing
    if front > 0:
        w["LegFront" + side] = front * rest
    if back > 0:
        w["LegBack" + side] = back * rest
    rest *= 1.0 - front - back

    # 残りは胴・尻尾・首と頭。首は高い所だけ（前脚の付け根を引っぱらない）。
    if rest > 1e-4:
        if x < 0.08:
            for bone, v in chain_weights(TAIL_CHAIN, x, True).items():
                w[bone] = w.get(bone, 0.0) + v * rest
        else:
            up = smooth(0.20, 0.27, z) if x < 0.40 else 1.0
            for bone, v in chain_weights(NECK_CHAIN, x, False).items():
                w[bone] = w.get(bone, 0.0) + v * rest * up
            w["Body"] = w.get("Body", 0.0) + rest * (1.0 - up)
    total = sum(w.values())
    return {k: v / total for k, v in w.items() if v > 1e-4} if total > 0 else {"Body": 1.0}


mesh = load_source(SOURCE, "Dragon_Body")
FIX = Matrix.Rotation(math.radians(-90), 4, "Z") @ Matrix.Translation(-ORIGIN)
rig_and_export(ROOT, mesh, BONES, weights_for, FIX, "Dragon", "dragon.fbx", "Dragon_Body.jpg", "dragon_tripo.blend", SOURCE)
