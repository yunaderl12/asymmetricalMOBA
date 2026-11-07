# 魔王の俯瞰画面に出す絵地図を描く。実際の地形のモデル（LP_*.fbx）を Unity と同じ配置で並べ、
# 南から仰角60度の平行投影で見下ろし、紙の色に寄せた色と墨の線（Freestyle）で描く。
# 出力: Assets/_Project/Resources/UI/<地図>_Illust.png と、ゲームの座標から絵の位置を出すための <地図>_Illust.json
# Run headless:
#   /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup -P ArtSource/lowpoly/render_map.py -- "<repo root>" [StageMap]
import colorsys
import json
import math
import os
import sys

import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ROOT = args[0] if args else os.getcwd()
MAP_NAME = args[1] if len(args) > 1 else "StageMap"
ART = os.path.join(ROOT, "Assets", "_Project", "Art", "LowPoly")
OUT = os.path.join(ROOT, "Assets", "_Project", "Resources", "UI")

# Unity の配置（MainStage の LP_* の位置と向き）。Unity の y 回転は上から見て時計回り、Blender の z 回転は反時計回り。
# LP_Field は Unity で 180 度回して置いてあり、それが Blender で作ったときの向き。ほかのモデルはそこからの差で回す。
WORLDS = {
    "StageMap": dict(field="LP_Field.fbx", size=500.0, models=[
        ("LP_StartTown.fbx", (-172.0, 193.0, 0.0), 225.0),
        ("LP_DemonCastle.fbx", (167.0, -187.0, 30.0), 225.0),
        ("LP_ArcherTower.fbx", (-154.96, 187.98, 0.0), 45.0),
        ("LP_ArcherTower.fbx", (-166.98, 175.96, 0.0), 45.0),
    ]),
}
CFG = WORLDS[MAP_NAME]
HIDDEN = ("Field_Barrier", "Field_TreeColliders", "Field_MountainFill", "Marker")

ELEVATION = 60.0           # 見下ろす角度（地面からの仰角）
MARGIN = 12.0              # 地図の外側に残す幅 (m)
TOP_EXTRA = 34.0           # 山の頭がはみ出さないよう上に足す幅 (m)
WIDTH_PX = int(args[2]) if len(args) > 2 else 3072  # 試し描きでは小さくする
PAPER = (0.93, 0.84, 0.62)
INK = (0.20, 0.12, 0.06)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


# ---------------------------------------------------------------- 色: 彩度を落として紙の色に寄せる。水は落ち着いた青。
def paperize(rgb, name):
    """彩度を落とし、明るさに応じたセピアと紙の色を混ぜる（参考: 黄土色の手描きの地図）。"""
    if "Crystal" in name or "Lava" in name or "Lamp" in name:
        return rgb
    h, sat, v = colorsys.rgb_to_hsv(*rgb)
    base = colorsys.hsv_to_rgb(h, sat * 0.55, min(1.0, v * 1.05 + 0.06))
    lum = 0.3 * base[0] + 0.59 * base[1] + 0.11 * base[2]
    warm = (min(1.0, lum * 1.10 + 0.06), lum * 0.95 + 0.03, lum * 0.62)
    mixed = tuple(b * 0.55 + w * 0.45 for b, w in zip(base, warm))
    return tuple(m * 0.85 + p * 0.15 for m, p in zip(mixed, PAPER))


def import_model(filename, location, unity_yaw):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(ART, filename), axis_forward="-Z", axis_up="Y")
    new = [o for o in bpy.data.objects if o not in before]
    roots = [o for o in new if o.parent is None]
    for o in list(new):
        if any(o.name.startswith(h) for h in HIDDEN) or o.type not in ("MESH", "EMPTY"):
            bpy.data.objects.remove(o, do_unlink=True)
    for r in roots:
        if r.name in bpy.data.objects:
            r.location = location
            # FBX の読み込みで付く X 90度（Y-up から Z-up への変換）は残し、向きだけ世界の z 軸で足す。
            r.rotation_euler.z = -math.radians(unity_yaw - 180.0)
            r.scale = (1, 1, 1)


import_model(CFG["field"], (0, 0, 0), 180.0)
for fn, loc, yaw in CFG["models"]:
    import_model(fn, loc, yaw)
bpy.context.view_layer.update()

# ---------------------------------------------------------------- 細かい飾りを外す
# 草むらと花は絵にすると茶色の点々になるので消す。地図の外の丘も消す（絵地図は島のように地図の端で終わる）。
import bmesh  # noqa: E402
LIMIT = CFG["size"] / 2 + 2
decor = bpy.data.objects.get("Field_Decor")
if decor is not None:
    bm = bmesh.new()
    bm.from_mesh(decor.data)
    names = [m.name if m else "" for m in decor.data.materials]
    mw = decor.matrix_world
    drop = []
    for f in bm.faces:
        mname = names[f.material_index] if f.material_index < len(names) else ""
        c = mw @ f.calc_center_median()
        # 砂丘の模様（SandB / SandC の平たい塊）も、線で囲まれると石畳のように見えるので消す。地図の外の丘は半分埋まっている。
        if ("Tuft" in mname or "Flower" in mname or mname.endswith("SandB") or mname.endswith("SandC")
                or abs(c.x) > LIMIT or abs(c.y) > LIMIT or c.z < -1.0):
            drop.append(f)
    bmesh.ops.delete(bm, geom=drop, context="FACES")
    bm.to_mesh(decor.data)
    bm.free()
trees = bpy.data.objects.get("Field_Trees")
if trees is not None:
    bm = bmesh.new()
    bm.from_mesh(trees.data)
    mw = trees.matrix_world
    drop = [f for f in bm.faces if abs((mw @ f.calc_center_median()).x) > LIMIT or abs((mw @ f.calc_center_median()).y) > LIMIT]
    bmesh.ops.delete(bm, geom=drop, context="FACES")
    bm.to_mesh(trees.data)
    bm.free()

# ---------------------------------------------------------------- 水: 地形の水面は4m四方のマスでギザギザなので、地図データからなめらかな水面を作って置き換える
old_water = bpy.data.objects.get("Field_Water")
if old_water is not None:
    bpy.data.objects.remove(old_water, do_unlink=True)
with open(os.path.join(ROOT, "Assets", "_Project", "Resources", MAP_NAME + ".json"), encoding="utf-8") as f:
    MAPDATA = json.load(f)
no_ink = bpy.data.collections.new("NoInk")
scene.collection.children.link(no_ink)


def water_material(name, rgb):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    d = nt.nodes.new("ShaderNodeBsdfDiffuse")
    d.inputs["Color"].default_value = (*rgb, 1.0)
    o = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(d.outputs[0], o.inputs[0])
    return m


def water_object(name, mat, build):
    bm = bmesh.new()
    build(bm)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    o = bpy.data.objects.new(name, me)
    no_ink.objects.link(o)
    return o


def ribbon(bm, pts, half, z):
    """折れ線の各区間の長方形と、各点の円を重ねる（つなぎ目が丸くなる）。"""
    for a, b in zip(pts, pts[1:]):
        d = (b - a).normalized()
        n = Vector((-d.y, d.x, 0)) * half
        vs = [bm.verts.new((p.x, p.y, z)) for p in (a - n, b - n, b + n, a + n)]
        bm.faces.new(vs)
    for p in pts:
        ring = [bm.verts.new((p.x + math.cos(t) * half, p.y + math.sin(t) * half, z))
                for t in (i / 28 * math.tau for i in range(28))]
        bm.faces.new(ring)


def pairs(flat):
    return [Vector((flat[i], flat[i + 1], 0)) for i in range(0, len(flat) - 1, 2)]


SEA = water_material("MapSea", (0.13, 0.26, 0.36))
DEEP = water_material("MapDeep", (0.17, 0.33, 0.42))
SHALLOW = water_material("MapShallow", (0.36, 0.52, 0.55))


def build_sea(bm):
    shore = pairs(MAPDATA["seaShore"])
    corner = Vector((MAPDATA["seaCorner"][0], MAPDATA["seaCorner"][1], 0))
    vs = [bm.verts.new((p.x, p.y, 0.05)) for p in shore + [corner]]
    bm.faces.new(vs)


# 岸の砂の帯（地形では4m四方のマスで塗られている）も、水の下になめらかに敷き直す。
BEACH = water_material("MapBeach", paperize((0.72, 0.56, 0.28), "Sand"))
WET = water_material("MapWetSand", paperize((0.42, 0.34, 0.20), "SandWet"))
water_object("Map_Beach", BEACH, lambda bm: (
    [ribbon(bm, pairs(l["points"]), l["width"] / 2 + 4.0, 0.03) for l in MAPDATA.get("deep", [])],
    ribbon(bm, pairs(MAPDATA["seaShore"]), 5.5, 0.03)))
water_object("Map_WetSand", WET, lambda bm: [ribbon(bm, pairs(l["points"]), l["width"] / 2 + 1.6, 0.06) for l in MAPDATA.get("shallow", [])])
# 砂漠のふち（地形では4m四方のマス）を、地形と同じ判定（楕円＋ノイズ）でなめらかな形に描き直し、その上に道を引き直す。
from mathutils import noise  # noqa: E402
DESERT = next((b for b in MAPDATA.get("biomes", []) if b["name"] == "desert"), None)


def in_desert(x, y):
    e = math.sqrt(((x - DESERT["x"]) / DESERT["rx"]) ** 2 + ((y - DESERT["z"]) / DESERT["rz"]) ** 2)
    return e + noise.noise(Vector((x * 0.03, y * 0.03, 4.2))) * 0.16 < 1.0


def build_desert(bm):
    ring = []
    for i in range(240):
        t = i / 240 * math.tau
        r = 0.0
        while r < 2.0 and in_desert(DESERT["x"] + math.cos(t) * DESERT["rx"] * (r + 0.01), DESERT["z"] + math.sin(t) * DESERT["rz"] * (r + 0.01)):
            r += 0.01
        ring.append(bm.verts.new((DESERT["x"] + math.cos(t) * DESERT["rx"] * r, DESERT["z"] + math.sin(t) * DESERT["rz"] * r, 0.02)))
    bm.faces.new(ring)


if DESERT is not None:
    water_object("Map_Desert", water_material("MapDesert", paperize((0.72, 0.56, 0.28), "Sand")), build_desert)


# 砂漠の広場（スコーピオンの石畳）。砂漠の面より上に、縁の濃い色と床の色の円を重ねる。
def build_disc(plaza, radius, z):
    def build(bm):
        ring = [bm.verts.new((plaza["x"] + math.cos(i / 48 * math.tau) * radius, plaza["z"] + math.sin(i / 48 * math.tau) * radius, z)) for i in range(48)]
        bm.faces.new(ring)
    return build


SCORPION = MAPDATA.get("scorpionPlaza")
if SCORPION:
    water_object("Map_ScorpionPlazaEdge", water_material("MapPlazaEdge", paperize((0.20, 0.11, 0.06), "SandstoneDark")), build_disc(SCORPION, SCORPION["radius"], 0.05))
    water_object("Map_ScorpionPlaza", water_material("MapPlaza", paperize((0.46, 0.27, 0.15), "Sandstone")), build_disc(SCORPION, SCORPION["radius"] - 3.0, 0.07))
DIRT = water_material("MapRoad", paperize((0.38, 0.24, 0.11), "Dirt"))
water_object("Map_Roads", DIRT, lambda bm: [ribbon(bm, pairs(r["points"]), 2.6, 0.04) for r in MAPDATA.get("roads", []) if r["name"] != "mountain"])
water_object("Map_Sea", SEA, build_sea)
water_object("Map_Deep", DEEP, lambda bm: [ribbon(bm, pairs(l["points"]), l["width"] / 2 + 1.5, 0.08) for l in MAPDATA.get("deep", [])])
water_object("Map_Shallow", SHALLOW, lambda bm: [ribbon(bm, pairs(l["points"]), l["width"] / 2, 0.1) for l in MAPDATA.get("shallow", [])])


done = set()
for o in bpy.data.objects:
    if o.type != "MESH":
        continue
    for slot in o.material_slots:
        m = slot.material
        if m is None or m.name in done or m.name.startswith("Map"):
            continue
        done.add(m.name)
        base = tuple(m.diffuse_color[:3])
        if m.use_nodes:
            bsdf = m.node_tree.nodes.get("Principled BSDF")
            if bsdf is not None:
                base = tuple(bsdf.inputs["Base Color"].default_value[:3])
        col = paperize(base, m.name)
        m.use_nodes = True
        nt = m.node_tree
        nt.nodes.clear()
        bsdf = nt.nodes.new("ShaderNodeBsdfDiffuse")
        bsdf.inputs["Color"].default_value = (*col, 1.0)
        out = nt.nodes.new("ShaderNodeOutputMaterial")
        nt.links.new(bsdf.outputs[0], out.inputs[0])
        if "Crystal" in m.name or "Lava" in m.name:
            em = nt.nodes.new("ShaderNodeEmission")
            em.inputs["Color"].default_value = (*col, 1.0)
            em.inputs["Strength"].default_value = 1.5
            nt.links.new(em.outputs[0], out.inputs[0])

# ---------------------------------------------------------------- 光: 北西の高いところからの太陽と、紙色の環境光
world = bpy.data.worlds.new("Paper")
scene.world = world
world.use_nodes = True
bg = world.node_tree.nodes["Background"]
bg.inputs["Color"].default_value = (*PAPER, 1.0)
bg.inputs["Strength"].default_value = 0.55

sun_data = bpy.data.lights.new("Sun", "SUN")
sun_data.energy = 3.2
sun_data.angle = math.radians(6)
sun_data.color = (1.0, 0.95, 0.86)
sun = bpy.data.objects.new("Sun", sun_data)
scene.collection.objects.link(sun)
sun.rotation_euler = (math.radians(50), 0, math.radians(-135))  # 北西（絵の左上）から当てる

# ---------------------------------------------------------------- カメラ: 南から仰角60度の平行投影
half = CFG["size"] / 2
tilt = math.radians(90.0 - ELEVATION)
up = Vector((0, math.cos(tilt), math.sin(tilt)))
view = Vector((0, math.sin(tilt), -math.cos(tilt)))
width = CFG["size"] + MARGIN * 2
screen_bottom = -half * up.y - MARGIN
screen_top = half * up.y + MARGIN + TOP_EXTRA
# 縦の画素数を4の倍数にそろえる（そうでないと Unity で圧縮できない）。そろえた分は上に足す。
pixels_y = int(math.ceil(WIDTH_PX * (screen_top - screen_bottom) / width / 4.0)) * 4
height = width * pixels_y / WIDTH_PX
screen_top = screen_bottom + height
center_screen_y = (screen_top + screen_bottom) / 2

cam_data = bpy.data.cameras.new("MapCam")
cam_data.type = "ORTHO"
cam_data.ortho_scale = max(width, height)
cam_data.clip_start = 1
cam_data.clip_end = 5000
cam = bpy.data.objects.new("MapCam", cam_data)
scene.collection.objects.link(cam)
target = Vector((0, 0, 0)) + up * center_screen_y
cam.location = target - view * 1500
cam.rotation_euler = (tilt, 0, 0)
scene.camera = cam

scene.render.resolution_x = WIDTH_PX
scene.render.resolution_y = pixels_y
scene.render.resolution_percentage = 100
scene.render.engine = "BLENDER_EEVEE"
scene.eevee.taa_render_samples = 32
scene.eevee.use_soft_shadows = True
scene.eevee.shadow_cascade_size = "4096"
scene.eevee.use_gtao = True
scene.eevee.gtao_distance = 6
sun_data.shadow_cascade_max_distance = 3000
sun_data.shadow_cascade_count = 4
scene.view_settings.view_transform = "Standard"
scene.view_settings.look = "None"

# ---------------------------------------------------------------- 墨の線
scene.render.use_freestyle = True
scene.render.line_thickness_mode = "ABSOLUTE"
scene.render.line_thickness = 1.6
vl = scene.view_layers[0]
vl.use_freestyle = True
fs = vl.freestyle_settings
fs.crease_angle = math.radians(128)
ls = fs.linesets[0] if fs.linesets else fs.linesets.new("Ink")
ls.select_by_visibility = True
ls.select_by_edge_types = True
ls.select_silhouette = True
ls.select_border = True
ls.select_crease = True
ls.select_by_collection = True
ls.collection = no_ink
ls.collection_negation = "EXCLUSIVE"
if ls.linestyle is None:
    ls.linestyle = bpy.data.linestyles.new("Ink")
ls.linestyle.color = INK
ls.linestyle.thickness = 1.6
ls.linestyle.alpha = 0.85

os.makedirs(OUT, exist_ok=True)
png = os.path.join(OUT, MAP_NAME + "_Illust.png") if WIDTH_PX >= 3072 else os.path.join(ROOT, "ArtSource", "lowpoly", "map_preview.png")
scene.render.filepath = png
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGB"
bpy.ops.render.render(write_still=True)

# ゲームの座標 (x, y, z) から絵の位置 (u, v: 左下0〜右上1) への変換。
# u = (x - centerX) / width + 0.5、v = (z * upZ + y * upY - centerScreenY) / height + 0.5
info = dict(width=width, height=height, centerX=0.0, centerScreenY=center_screen_y,
            upZ=up.y, upY=up.z, elevation=ELEVATION, pixelsX=scene.render.resolution_x, pixelsY=scene.render.resolution_y)
if WIDTH_PX >= 3072:
    with open(os.path.join(OUT, MAP_NAME + "_Illust.json"), "w", encoding="utf-8") as f:
        json.dump(info, f, indent=1)
print("MAP_DONE", png, info)
