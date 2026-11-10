# 新しい雑魚魔物（ドライアド・ミイラ・イフリート・ハーピー・コカトリス）を取り込む。
# 外から来た骨なしのメッシュ（Tripo の FBX）に、位置で決めた重みの骨を入れて Resources/<Folder>/<name>.fbx に書き出す。
# アニメーションのクリップは持たない。Unity 側の各魔物のスクリプトが BossRig で骨を毎フレーム回して動かす。
#   - 骨の位置は元の座標（Tripo の書き出しのまま。Z が上、高さはおよそ 0.6〜1）で書く。
#   - 重み: 各頂点にいちばん近い骨を3本まで選び、距離の4乗の逆数で分ける。allow で付けてはいけない骨を除く。
#   - 書き出しでは、元の向き（yaw 度で Z まわりに回す）を -Y（Unity の +Z）にそろえ、origin を足元の原点にする。
#   - 骨の名前: L は体の左（書き出し後の +X）、R は右。
# Run headless:
#   /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup -P ArtSource/characters/build_monsters.py -- \
#       "<repo root>" <dryad|mummy|ifrit|harpy|cockatrice> "<tripo_convert_....fbx>"
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from boss_rig import load_source, rig_and_export  # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:]
ROOT, NAME, SOURCE = args[0], args[1], args[2]


def humanoid(hips, chest, neck, top, arms, legs):
    """背骨（腰→胸→首→頭のてっぺん）と、左右の腕（肩, ひじ, 手首, 指先）・脚（股, ひざ, 足首, つま先）。"""
    bones = [("Body", hips, chest, None), ("Chest", chest, neck, "Body"), ("Head", neck, top, "Chest")]
    for side in ("L", "R"):
        s, e, w, t = arms[side]
        bones += [("Arm" + side, s, e, "Chest"), ("Fore" + side, e, w, "Arm" + side), ("Hand" + side, w, t, "Fore" + side)]
        h, k, a, toe = legs[side]
        bones += [("Thigh" + side, h, k, "Body"), ("Shin" + side, k, a, "Thigh" + side), ("Foot" + side, a, toe, "Shin" + side)]
    return bones


def sided_x(name, p, margin=0.02):
    """体の左右（元の x の符号）をまたいで腕や脚に付けない。"""
    if name.endswith("L") and name[:-1] in LIMBS:
        return p.x > -margin
    if name.endswith("R") and name[:-1] in LIMBS:
        return p.x < margin
    return True


LIMBS = {"Arm", "Fore", "Hand", "Thigh", "Shin", "Foot", "Wing", "WingTip"}

CONFIGS = {
    # 森: 角のある小さな少女の精霊。T ポーズ。スカートは脚に付けず腰について行かせる。
    "dryad": dict(
        folder="Dryad", fbx="dryad.fbx", tex="Dryad_Body.jpg", blend="dryad_tripo.blend", yaw=0, origin=(0, 0, 0),
        bones=humanoid((0, -0.01, 0.42), (0, -0.01, 0.60), (0, -0.01, 0.75), (0, -0.01, 0.98),
                       {"L": ((0.06, -0.01, 0.72), (0.21, -0.01, 0.695), (0.31, -0.01, 0.69), (0.41, -0.01, 0.685)),
                        "R": ((-0.06, -0.01, 0.72), (-0.21, -0.01, 0.695), (-0.31, -0.01, 0.69), (-0.41, -0.01, 0.685))},
                       {"L": ((0.045, -0.01, 0.42), (0.045, 0.0, 0.20), (0.04, 0.02, 0.04), (0.04, -0.08, 0.01)),
                        "R": ((-0.045, -0.01, 0.42), (-0.045, 0.0, 0.20), (-0.04, 0.02, 0.04), (-0.04, -0.08, 0.01))}),
        allow=lambda n, p: sided_x(n, p) and not (n[:-1] in ("Thigh", "Shin", "Foot") and p.z > 0.34)
        and not (n[:-1] in ("Arm", "Fore", "Hand") and abs(p.x) < 0.07)),
    # 砂漠: 前かがみで左腕を振り上げ、右腕を前へ垂らしたミイラ。右脚（-X）は後ろ、左脚（+X）は前。
    "mummy": dict(
        folder="Mummy", fbx="mummy.fbx", tex="Mummy_Body.jpg", blend="mummy_tripo.blend", yaw=0, origin=(0, 0.07, 0),
        bones=humanoid((0, 0.07, 0.46), (-0.01, -0.03, 0.63), (-0.04, -0.13, 0.745), (-0.05, -0.22, 0.98),
                       {"L": ((0.095, 0.0, 0.76), (0.22, -0.05, 0.72), (0.32, -0.12, 0.80), (0.35, -0.20, 0.93)),
                        "R": ((-0.11, 0.0, 0.745), (-0.135, -0.19, 0.52), (-0.07, -0.30, 0.33), (-0.06, -0.36, 0.24))},
                       {"L": ((0.08, 0.07, 0.45), (0.16, -0.07, 0.37), (0.26, 0.02, 0.08), (0.30, -0.15, 0.02)),
                        "R": ((-0.08, 0.07, 0.45), (-0.16, 0.02, 0.34), (-0.33, 0.30, 0.08), (-0.36, 0.13, 0.02))}),
        allow=lambda n, p: sided_x(n, p, 0.04) and not (n[:-1] in ("Thigh", "Shin", "Foot") and p.z > 0.50)),
    # 砂漠: 炎の魔人。下半身は炎の尾、腕は胸の前で組んでいる。頭（あご）は -X を向いている。
    "ifrit": dict(
        folder="Ifrit", fbx="ifrit.fbx", tex="Ifrit_Body.jpg", blend="ifrit_tripo.blend", yaw=90, origin=(0, 0, 0),
        bones=[("Tail", (0.03, 0, 0.02), (0.0, 0, 0.27), None), ("Body", (0.0, 0, 0.27), (0.0, 0, 0.52), "Tail"),
               ("Head", (-0.02, 0, 0.52), (-0.02, 0, 0.98), "Body"),
               # 右（+Y）の腕は胸の前を通って -Y 側へ、左（-Y）の腕は +Y 側へ。
               ("ArmR", (0.0, 0.2, 0.48), (-0.12, 0.17, 0.38), "Body"), ("ForeR", (-0.12, 0.17, 0.38), (-0.18, -0.05, 0.42), "ArmR"),
               ("ArmL", (0.0, -0.2, 0.48), (-0.12, -0.17, 0.38), "Body"), ("ForeL", (-0.12, -0.17, 0.38), (-0.18, 0.05, 0.40), "ArmL")],
        allow=lambda n, p: not (n[:-1] in ("Arm", "Fore") and p.z < 0.26)),
    # 山: 腕が翼の鳥人。翼は肘を上げて広げている。脚は鳥の足。
    "harpy": dict(
        folder="Harpy", fbx="harpy.fbx", tex="Harpy_Body.jpg", blend="harpy_tripo.blend", yaw=0, origin=(0, 0, 0),
        bones=humanoid((0, 0, 0.28), (0, 0, 0.40), (0, 0, 0.45), (0, 0, 0.64),
                       {"L": ((0.06, 0, 0.42), (0.20, 0, 0.53), (0.28, 0, 0.47), (0.48, 0, 0.12)),
                        "R": ((-0.06, 0, 0.42), (-0.20, 0, 0.53), (-0.28, 0, 0.47), (-0.48, 0, 0.12))},
                       {"L": ((0.06, 0, 0.30), (0.10, 0.03, 0.15), (0.11, 0.02, 0.07), (0.12, -0.09, 0.01)),
                        "R": ((-0.06, 0, 0.30), (-0.10, 0.03, 0.15), (-0.11, 0.02, 0.07), (-0.12, -0.09, 0.01))}),
        # 体から 0.155 より外は翼だけ。ただし足元（z 0.08 以下）は外へ広がった鉤爪なので翼に付けない
        # （付けると翼を広げたときに足先が翼の方へ伸びる）。内側の腰より下は脚と胴だけ。
        allow=lambda n, p: sided_x(n, p) and (
            (n[:-1] in ("Arm", "Fore", "Hand")) if abs(p.x) > 0.155 and p.z > 0.08 else
            (n[:-1] not in ("Arm", "Fore", "Hand") or p.z > 0.36))),
    # 山: 雄鶏の魔物。頭は (-X, -Y) の斜めを向いている。翼を広げ、尾羽を立てている。
    "cockatrice": dict(
        folder="Cockatrice", fbx="cockatrice.fbx", tex="Cockatrice_Body.jpg", blend="cockatrice_tripo.blend", yaw=45,
        origin=(0.0, 0.0, 0),
        bones=[("Body", (0.10, 0.10, 0.25), (-0.06, -0.06, 0.26), None),
               ("Neck", (-0.06, -0.06, 0.27), (-0.17, -0.17, 0.30), "Body"),
               ("Head", (-0.17, -0.17, 0.30), (-0.28, -0.29, 0.28), "Neck"),
               ("Tail", (0.12, 0.12, 0.28), (0.32, 0.32, 0.45), "Body"),
               # 右の翼は (-X, +Y) 側、左は (+X, -Y) 側。
               ("WingR", (-0.06, 0.06, 0.33), (-0.25, 0.20, 0.38), "Body"), ("WingTipR", (-0.25, 0.20, 0.38), (-0.48, 0.30, 0.38), "WingR"),
               ("WingL", (0.06, -0.06, 0.33), (0.25, -0.22, 0.36), "Body"), ("WingTipL", (0.25, -0.22, 0.36), (0.48, -0.40, 0.30), "WingL"),
               ("ThighR", (0.0, 0.06, 0.17), (-0.06, 0.04, 0.09), "Body"), ("ShinR", (-0.06, 0.04, 0.09), (-0.14, 0.03, 0.03), "ThighR"),
               ("FootR", (-0.14, 0.03, 0.03), (-0.20, -0.03, 0.01), "ShinR"),
               ("ThighL", (0.12, 0.0, 0.17), (0.19, 0.0, 0.10), "Body"), ("ShinL", (0.19, 0.0, 0.10), (0.20, 0.02, 0.03), "ThighL"),
               ("FootL", (0.20, 0.02, 0.03), (0.18, -0.06, 0.01), "ShinL")],
        # 脚は胴の下だけ。翼は胴の上の外側だけ。右と左は体の右向き (-1, 1)/√2 の側で分ける。
        allow=lambda n, p: cock_allow(n, p)),
}


def cock_allow(n, p):
    right = (-p.x + p.y) / math.sqrt(2)
    if n in ("ThighR", "ShinR", "FootR", "ThighL", "ShinL", "FootL"):
        if p.z > 0.16:
            return False
        return right > -0.03 if n.endswith("R") else right < 0.03
    if n.startswith("Wing"):
        if p.z < 0.22:
            return False
        return right > 0.06 if n.endswith("R") else right < -0.06
    return True


CFG = CONFIGS[NAME]
BONES = [(n, Vector(h), Vector(t), parent) for n, h, t, parent in CFG["bones"]]
ALLOW = CFG["allow"]


def seg_dist(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / ab.dot(ab)))
    return (a + ab * t - p).length


def weights_for(p):
    cand = []
    for name, a, b, _ in BONES:
        if ALLOW(name, p):
            cand.append((seg_dist(p, a, b), name))
    if not cand:
        return {BONES[0][0]: 1.0}
    cand.sort()
    out = {}
    for d, name in cand[:3]:
        out[name] = 1.0 / (d + 0.02) ** 4
    return out


mesh = load_source(SOURCE, CFG["folder"] + "_Body")
if mesh.data.has_custom_normals:
    bpy.context.view_layer.objects.active = mesh
    mesh.select_set(True)
    bpy.ops.mesh.customdata_custom_splitnormals_clear()
FIX = Matrix.Rotation(math.radians(CFG["yaw"]), 4, "Z") @ Matrix.Translation(-Vector(CFG["origin"]))
rig_and_export(ROOT, mesh, [(n, tuple(h), tuple(t), parent) for n, h, t, parent in BONES], weights_for, FIX,
               CFG["folder"], CFG["fbx"], CFG["tex"], CFG["blend"], SOURCE)
