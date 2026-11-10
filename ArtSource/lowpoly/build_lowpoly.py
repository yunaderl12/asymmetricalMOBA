# Builds the placeholder low-poly environment (StartTown / DemonCastle / meadow field) and exports FBX for Unity.
# Run headless:
#   /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup -P ArtSource/lowpoly/build_lowpoly.py -- "<repo root>" [only.fbx]
# Giving only.fbx exports just that model and leaves the other FBX files and the .blend untouched.
# Coordinates: Blender (x, y, z) = Unity (x, z, y) before import. Each model has Marker_PlusX / Marker_PlusZ
# empties so the Unity placer can fix any axis flip from the FBX conversion.
import json
import bpy
import bmesh
import math
import os
import random
import sys
from mathutils import Matrix, Vector, noise

ROOT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else os.getcwd()
ONLY = sys.argv[sys.argv.index("--") + 2] if "--" in sys.argv and len(sys.argv) > sys.argv.index("--") + 2 else None
OUT_FBX = os.path.join(ROOT, "Assets", "_Project", "Art", "LowPoly")
OUT_SRC = os.path.join(ROOT, "ArtSource", "lowpoly")
os.makedirs(OUT_FBX, exist_ok=True)

with open(os.path.join(ROOT, "Assets", "_Project", "Resources", "StageMap.json"), encoding="utf-8") as _f:
    MAP = json.load(_f)
TOWN = Vector((MAP["townX"], MAP["townZ"]))
CASTLE = Vector((MAP["castleX"], MAP["castleZ"]))
SPAWNERS = [Vector((sp["x"], sp["z"])) for sp in MAP["spawners"]]
# Rocks (Unity x, z, size x, size y, size z); visuals sit on the existing colliders, sizes follow the scene's Rock_00..09.
_ROCK_SIZES = [(8, 5, 8)] * 4 + [(6, 4, 5)] * 4 + [(4, 3, 4), (3.5, 3, 4.5)]
ROCKS = [(r["x"], r["z"], *_ROCK_SIZES[i]) for i, r in enumerate(MAP["rocks"])]

# Start from an empty scene (materials below must be created after the reset).
bpy.ops.wm.read_factory_settings(use_empty=True)

# ---------------------------------------------------------------- materials (linear colors)
MATS = {}


def mat(name, rgb, emit=0.0):
    if name in MATS:
        return MATS[name]
    m = bpy.data.materials.new("LP_" + name)
    m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (*rgb, 1.0)
    b.inputs["Roughness"].default_value = 0.9
    if emit > 0:
        b.inputs["Emission Color"].default_value = (*rgb, 1.0)
        b.inputs["Emission Strength"].default_value = emit
    m.diffuse_color = (*rgb, 1.0)
    MATS[name] = m
    return m


# ---------------------------------------------------------------- mesh builder
class Group:
    """One output mesh; parts are added in model space with per-face materials."""

    def __init__(self, name, rng):
        self.name = name
        self.bm = bmesh.new()
        self.mats = []
        self.rng = rng
        self.force_up = False

    def _mi(self, m):
        if m not in self.mats:
            self.mats.append(m)
        return self.mats.index(m)

    def _finish(self, verts, m, jitter=0.0):
        idx = self._mi(m)
        for f in {f for v in verts for f in v.link_faces}:
            f.material_index = idx
        if jitter > 0:
            r = self.rng
            for v in verts:
                v.co += Vector((r.uniform(-jitter, jitter), r.uniform(-jitter, jitter), r.uniform(-jitter, jitter)))

    @staticmethod
    def _m(pos, rotz=0.0, scale=(1, 1, 1)):
        return Matrix.Translation(Vector(pos)) @ Matrix.Rotation(rotz, 4, "Z") @ Matrix.Diagonal((*scale, 1.0))

    def box(self, m, center, size, rotz=0.0, jitter=0.0):
        r = bmesh.ops.create_cube(self.bm, size=1.0, matrix=self._m(center, rotz, size))
        self._finish(r["verts"], m, jitter)

    def cyl(self, m, base, r1, h, n=8, r2=None, rotz=0.0, jitter=0.0, tilt=None):
        mtx = self._m((base[0], base[1], base[2]), rotz)
        if tilt is not None:
            mtx = mtx @ tilt
        mtx = mtx @ Matrix.Translation((0, 0, h / 2))
        r = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=n,
                                  radius1=r1, radius2=r1 if r2 is None else r2, depth=h, matrix=mtx)
        self._finish(r["verts"], m, jitter)

    def cone(self, m, base, r, h, n=8, rotz=0.0, jitter=0.0, tilt=None):
        self.cyl(m, base, r, h, n, r2=0.0, rotz=rotz, jitter=jitter, tilt=tilt)

    def ico(self, m, center, r, sub=1, scale=(1, 1, 1), jitter=0.0, rotz=0.0):
        res = bmesh.ops.create_icosphere(self.bm, subdivisions=sub, radius=r, matrix=self._m(center, rotz, scale))
        self._finish(res["verts"], m, jitter)

    def roof(self, m, base, w, d, h, rotz=0.0):
        """Gable roof: ridge along local x."""
        mtx = self._m(base, rotz)
        pts = [(-w / 2, -d / 2, 0), (w / 2, -d / 2, 0), (w / 2, d / 2, 0), (-w / 2, d / 2, 0), (-w / 2, 0, h), (w / 2, 0, h)]
        vs = [self.bm.verts.new(mtx @ Vector(p)) for p in pts]
        for f in [(0, 3, 2, 1), (0, 1, 5, 4), (2, 3, 4, 5), (0, 4, 3), (1, 2, 5)]:
            self.bm.faces.new([vs[i] for i in f])
        self._finish(vs, m)

    def tri_fan(self, m, verts_co):
        vs = [self.bm.verts.new(Vector(c)) for c in verts_co]
        self.bm.faces.new(vs)
        self._finish(vs, m)

    def build(self, parent, collection):
        bm = self.bm
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0001)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        if self.force_up:
            for f in bm.faces:
                if f.normal.z < 0:
                    f.normal_flip()
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        bm.free()
        for m in self.mats:
            me.materials.append(m)
        for p in me.polygons:
            p.use_smooth = False
        obj = bpy.data.objects.new(self.name, me)
        collection.objects.link(obj)
        obj.parent = parent
        return obj


def new_model(name):
    col = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(col)
    root = bpy.data.objects.new(name, None)
    col.objects.link(root)
    return col, root


def add_markers(col, root, dist_x, dist_z):
    """Tiny mesh markers (not empties) so they go through the same FBX axis conversion as the model meshes.
    The geometry sits at the marker position with the object origin at 0; Unity reads the bounds center."""
    for nm, pos in (("Marker_PlusX", (dist_x, 0, 0)), ("Marker_PlusZ", (0, dist_z, 0))):
        g = Group(nm, random.Random(0))
        g.box(mat("Marker", (1.0, 0.0, 1.0)), pos, (0.2, 0.2, 0.2))
        g.build(root, col)


def export(col, root, filename):
    bpy.ops.object.select_all(action="DESELECT")
    for o in col.objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT_FBX, filename), use_selection=True,
        object_types={"MESH", "EMPTY"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z", axis_up="Y", bake_space_transform=True, mesh_smooth_type="FACE",
        use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False, path_mode="AUTO")


# ---------------------------------------------------------------- palette
P = {
    "grass_a": mat("GrassA", (0.13, 0.36, 0.06)),
    "grass_b": mat("GrassB", (0.18, 0.44, 0.08)),
    "grass_c": mat("GrassC", (0.10, 0.28, 0.05)),
    "grass_dry": mat("GrassDry", (0.36, 0.42, 0.10)),
    "tuft": mat("Tuft", (0.15, 0.48, 0.07)),
    "sand": mat("Sand", (0.72, 0.56, 0.28)),
    "sand_b": mat("SandB", (0.80, 0.63, 0.32)),
    "sand_c": mat("SandC", (0.62, 0.46, 0.22)),
    "sand_wet": mat("SandWet", (0.42, 0.34, 0.20)),
    "forest_a": mat("ForestA", (0.05, 0.17, 0.05)),
    "forest_b": mat("ForestB", (0.08, 0.22, 0.05)),
    "snow": mat("Snow", (0.85, 0.87, 0.90)),
    "cactus": mat("Cactus", (0.10, 0.32, 0.12)),
    "sandstone": mat("Sandstone", (0.46, 0.27, 0.15)),
    "sandstone_b": mat("SandstoneB", (0.36, 0.20, 0.11)),
    "sandstone_dark": mat("SandstoneDark", (0.20, 0.11, 0.06)),
    "water_shallow": mat("WaterShallow", (0.05, 0.30, 0.46)),
    "water_shallow_b": mat("WaterShallowB", (0.08, 0.36, 0.52)),
    "water_deep": mat("WaterDeep", (0.02, 0.07, 0.30)),
    "water_deep_b": mat("WaterDeepB", (0.03, 0.10, 0.36)),
    "water_sea": mat("WaterSea", (0.02, 0.12, 0.40)),
    "water_sea_b": mat("WaterSeaB", (0.03, 0.17, 0.48)),
    "dirt": mat("Dirt", (0.38, 0.24, 0.11)),
    "dirt_dark": mat("DirtDark", (0.24, 0.15, 0.07)),
    "ash": mat("Ash", (0.06, 0.05, 0.06)),
    "scorched": mat("Scorched", (0.12, 0.13, 0.05)),
    "stone": mat("Stone", (0.42, 0.42, 0.40)),
    "stone_dark": mat("StoneDark", (0.24, 0.24, 0.24)),
    "moss": mat("Moss", (0.12, 0.30, 0.06)),
    "bark": mat("Bark", (0.20, 0.11, 0.05)),
    "leaf_a": mat("LeafA", (0.08, 0.32, 0.06)),
    "leaf_b": mat("LeafB", (0.14, 0.40, 0.05)),
    "leaf_pine": mat("LeafPine", (0.04, 0.20, 0.07)),
    "flower_w": mat("FlowerWhite", (0.9, 0.9, 0.85)),
    "flower_y": mat("FlowerYellow", (0.95, 0.75, 0.08)),
    "flower_p": mat("FlowerPink", (0.9, 0.35, 0.55)),
    "flower_b": mat("FlowerBlue", (0.25, 0.4, 0.95)),
    "cobble": mat("Cobble", (0.50, 0.46, 0.38)),
    "plaster": mat("Plaster", (0.85, 0.78, 0.62)),
    "wood": mat("Wood", (0.33, 0.19, 0.08)),
    "wood_dark": mat("WoodDark", (0.17, 0.09, 0.04)),
    "wood_light": mat("WoodLight", (0.46, 0.30, 0.15)),
    "rope": mat("Rope", (0.55, 0.45, 0.28)),
    "roof_r": mat("RoofRed", (0.55, 0.12, 0.07)),
    "roof_b": mat("RoofBlue", (0.10, 0.20, 0.50)),
    "roof_o": mat("RoofOrange", (0.70, 0.30, 0.08)),
    "window": mat("Window", (0.55, 0.75, 0.95)),
    "water": mat("Water", (0.10, 0.45, 0.85)),
    "crystal_town": mat("CrystalTown", (0.35, 0.85, 1.0), emit=1.5),
    "lamp": mat("Lamp", (1.0, 0.8, 0.35), emit=2.0),
    "flag_blue": mat("FlagBlue", (0.08, 0.22, 0.75)),
    "obsidian": mat("Obsidian", (0.05, 0.04, 0.07)),
    "castle": mat("CastleStone", (0.10, 0.09, 0.12)),
    "castle_trim": mat("CastleTrim", (0.18, 0.15, 0.20)),
    "roof_purple": mat("RoofPurple", (0.22, 0.04, 0.30)),
    "crystal_dk": mat("CrystalDemon", (0.65, 0.10, 1.0), emit=2.5),
    "lava": mat("Lava", (1.0, 0.25, 0.04), emit=3.0),
    "flag_red": mat("FlagRed", (0.45, 0.02, 0.04)),
    "bone": mat("Bone", (0.75, 0.72, 0.62)),
}


def ground_height(x, y, rng):
    return -0.05 + 0.06 * noise.noise(Vector((x * 0.06, y * 0.06, 0.3))) + rng.uniform(-0.015, 0.015)


def on_path(x, y, margin=0.0):
    p = Vector((x, y))
    return any(line_dist(p, pts)[0] < 2.6 + margin for pts in ROADS.values())


def near_spawner(p, r):
    return any((p - s).length < r for s in SPAWNERS)


# スコーピオンの広場: 砂漠の東寄りに敷いた円い石畳。縁に崩れた砂岩の柱が並ぶ。床は固く、砂地の遅さはない。
SCORPION = MAP["scorpionPlaza"]
SCORPION_CENTER = Vector((SCORPION["x"], SCORPION["z"]))
SCORPION_RADIUS = SCORPION["radius"]


def in_scorpion_plaza(p, margin=0.0):
    return (p - SCORPION_CENTER).length < SCORPION_RADIUS + margin


def tree(g, rng, x, y, s=1.0, kind=None, base=0.0):
    kind = kind or rng.choice(["round", "round", "pine"])
    h = rng.uniform(2.2, 3.0) * s
    g.cyl(P["bark"], (x, y, base - 0.1), 0.22 * s, h, n=6, r2=0.15 * s)
    if kind == "pine":
        for i, (r, z) in enumerate([(1.6, 0.55), (1.25, 1.35), (0.85, 2.1)]):
            g.cone(P["leaf_pine"], (x, y, base + h * 0.55 + z * s), r * s, 1.6 * s, n=7, rotz=rng.random() * 6, jitter=0.08 * s)
    else:
        leaf = P["leaf_a"] if rng.random() < 0.5 else P["leaf_b"]
        g.ico(leaf, (x, y, base + h + 0.6 * s), 1.4 * s, sub=0, scale=(1, 1, 0.9), jitter=0.18 * s, rotz=rng.random() * 6)
        g.ico(leaf, (x + 0.7 * s, y + 0.3 * s, base + h + 0.1 * s), 0.9 * s, sub=0, jitter=0.12 * s)


def dead_tree(g, rng, x, y, s=1.0):
    h = rng.uniform(2.5, 3.5) * s
    g.cyl(P["wood_dark"], (x, y, -0.1), 0.2 * s, h, n=5, r2=0.08 * s, jitter=0.03)
    for _ in range(3):
        a = rng.random() * math.tau
        tilt = Matrix.Rotation(math.radians(rng.uniform(40, 65)), 4, Vector((math.cos(a), math.sin(a), 0)))
        g.cyl(P["wood_dark"], (x, y, h * rng.uniform(0.5, 0.85)), 0.08 * s, 1.2 * s, n=4, r2=0.02, tilt=tilt)


# ================================================================ FIELD
# 地図の構成は Assets/_Project/Resources/StageMap.json にまとめてあり、Unity 側（StageMap.cs）も同じものを読む。
def _pairs(flat):
    return [Vector((flat[i], flat[i + 1])) for i in range(0, len(flat), 2)]


def smooth(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def line_dist(p, pts):
    """(distance to the polyline, distance along it to the nearest point)"""
    best, best_s, s = 1e9, 0.0, 0.0
    for a, b in zip(pts, pts[1:]):
        ab = b - a
        l = ab.length
        t = max(0.0, min(1.0, (p - a).dot(ab) / (l * l)))
        d = (a + ab * t - p).length
        if d < best:
            best, best_s = d, s + t * l
        s += l
    return best, best_s


def line_length(pts):
    return sum((b - a).length for a, b in zip(pts, pts[1:]))


def in_poly(p, poly):
    inside = False
    j = len(poly) - 1
    for i in range(len(poly)):
        a, b = poly[i], poly[j]
        if (a.y > p.y) != (b.y > p.y) and p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x:
            inside = not inside
        j = i
    return inside


SHALLOW = [(l["width"], _pairs(l["points"])) for l in MAP["shallow"]]
DEEP = [(l["width"], _pairs(l["points"])) for l in MAP["deep"]]
SHORE = _pairs(MAP["seaShore"])
SEA_POLY = SHORE + [Vector(MAP["seaCorner"])]
ROADS = {r["name"]: _pairs(r["points"]) for r in MAP["roads"]}
H_CASTLE = float(MAP["castleHeight"])
R_ISLAND = float(MAP["islandRadius"])
BRIDGE_W = float(MAP["bridgeWidth"])
BRIDGE_TAN = math.tan(math.radians(MAP["bridgeSlopeDegrees"]))
BRIDGE_STARTS = _pairs(MAP["bridgeStarts"])
BRIDGE_ISLAND_OVERLAP = 2.0  # 橋の先端は島の縁より内側まで入れる
BRIDGE_POST_INSET = 2.5
BRIDGE_POST_HEIGHT = 11.0
BRIDGE_CABLE_LOW = 2.6  # 主ロープがいちばんたるんだ所の高さ
BRIDGE_HANGER_SPACING = 3.5
BRIDGE_RAIL_HEIGHT = 1.15
BIOMES = {b["name"]: b for b in MAP["biomes"]}
HALF = MAP["size"] / 2.0


def is_sea(p):
    return in_poly(p, SEA_POLY)


def capped_gap(p, pts, w):
    """川の岸までの距離（水の中は負）。川の端は丸めずに平らに切る（端の先にある浅瀬が深い水にならないように）。"""
    d = line_dist(p, pts)[0] - w / 2
    for a, b in ((pts[0], pts[1]), (pts[-1], pts[-2])):
        t = (a - b).normalized()
        over = (p - a).dot(t)
        if over > 0.0:
            side = abs((p - a).x * t.y - (p - a).y * t.x)
            d = max(side - w / 2, over)
    return d


def deep_gap(p):
    """Distance to the bank of the deep rivers (negative in the water). The ends are cut flat."""
    return min(capped_gap(p, pts, w) for w, pts in DEEP)


def shallow_gap(p):
    return min(line_dist(p, pts)[0] - w / 2 for w, pts in SHALLOW)


def shore_dist(p):
    return line_dist(p, SHORE)[0]


def road_dist(p):
    return min(line_dist(p, pts)[0] for pts in ROADS.values())


def in_ellipse(p, cx, cz, rx, rz, wobble=0.0):
    e = math.sqrt(((p.x - cx) / rx) ** 2 + ((p.y - cz) / rz) ** 2)
    if wobble:
        e += noise.noise(Vector((p.x * 0.03, p.y * 0.03, 4.2))) * wobble
    return e


def in_desert(p):
    b = BIOMES["desert"]
    return in_ellipse(p, b["x"], b["z"], b["rx"], b["rz"], 0.16) < 1.0


def in_forest(p):
    b = BIOMES["forest"]
    return in_ellipse(p, b["x"], b["z"], b["rx"], b["rz"], 0.12) < 1.0 and deep_gap(p) > 2 and not is_sea(p)


# 浅い川: 川底を土色にして窪ませ、地表のすぐ下まで半透明の水を張る（水は Unity 側で透ける材質に差し替える）。
RIVER_WATER_Y = float(MAP.get("shallowWaterY", -0.1))
RIVER_BED = float(MAP.get("shallowBedDepth", 0.75))
RIVER_BANK = 4.0  # 岸から川底の深さになるまでの幅 (m)


def nearest_on(pts, p):
    best, best_q = 1e9, pts[0]
    for a, b in zip(pts, pts[1:]):
        ab = b - a
        t = max(0.0, min(1.0, (p - a).dot(ab) / ab.dot(ab)))
        q = a + ab * t
        if (q - p).length < best:
            best, best_q = (q - p).length, q
    return best_q


# 森の広場: 巨大樹の森の奥に開けた円い空き地。枯れ木に囲まれ、骨が散らばる。ウェンディゴがここに棲む。
# 森の道から小道が1本だけ通じている。
FOREST_PLAZA = MAP["forestPlaza"]
FOREST_CENTER = Vector((FOREST_PLAZA["x"], FOREST_PLAZA["z"]))
FOREST_RADIUS = FOREST_PLAZA["radius"]


def road_gap_angles(center, radius):
    """道が円 (center, radius) を横切る所の角度。広場の縁の飾りはここを空けて、道が広場を通り抜けられるようにする。"""
    angles = []
    for pts in ROADS.values():
        for a, b in zip(pts, pts[1:]):
            d = b - a
            f = a - center
            A, B, C = d.dot(d), 2 * f.dot(d), f.dot(f) - radius * radius
            disc = B * B - 4 * A * C
            if disc < 0:
                continue
            for sgn in (-1, 1):
                t = (-B + sgn * math.sqrt(disc)) / (2 * A)
                if 0.0 <= t <= 1.0:
                    q = a + d * t - center
                    angles.append(math.atan2(q.y, q.x))
    return angles


def near_angle(a, angles, tol):
    return any(abs(math.atan2(math.sin(a - b), math.cos(a - b))) < tol for b in angles)


def in_forest_plaza(p, margin=0.0):
    return (p - FOREST_CENTER).length < FOREST_RADIUS + margin


def on_forest_trail(p, margin=0.0):
    return False  # 広場が道の上に移ったので、小道はもうない


# ---------------------------------------------------------------- ground bumps
GROUND_CELL = 2.0  # 地面の格子 (m)。Unity の当たり判定も同じ格子で作る（Resources/StageGround.bytes）。


def bump(p):
    return 0.22 * noise.noise(Vector((p.x * 0.07, p.y * 0.07, 9.3))) + 0.08 * noise.noise(Vector((p.x * 0.19, p.y * 0.19, 2.6)))


def bump_mask(p, sg):
    """凹凸を付けてよい度合い（0〜1）。まち・川岸・スポナー・広場・橋のたもと・岩・山のふもと・地図の端は平らにする。"""
    m = smooth(30, 48, (p - TOWN).length)
    if m <= 0.0:
        return 0.0
    m *= smooth(1, 5, sg) * smooth(1, 6, deep_gap(p)) * smooth(3, 10, shore_dist(p))
    if m <= 0.0:
        return 0.0
    m *= min(smooth(8, 15, (p - s).length) for s in SPAWNERS)
    m *= smooth(SCORPION_RADIUS + 1, SCORPION_RADIUS + 9, (p - SCORPION_CENTER).length)
    m *= smooth(FOREST_RADIUS + 1, FOREST_RADIUS + 9, (p - FOREST_CENTER).length)
    m *= min(smooth(12, 22, (p - b).length) for b in BRIDGE_STARTS)
    for rx, rz, sx, sy, sz in ROCKS:
        m *= smooth(1, 6, max(abs(p.x - rx) - sx / 2, abs(p.y - rz) - sz / 2))
    if m <= 0.0:
        return 0.0
    if p.x > -62 and p.y > -46:
        m *= 1.0 - smooth(-0.1, 0.3, mountain_height(p))
    m *= 0.5 + 0.5 * smooth(1.5, 5, road_dist(p))
    m *= smooth(0, 15, HALF - max(abs(p.x), abs(p.y)))
    return m


def field_height(p, r):
    """地面の頂点の高さ。乱数 r は頂点ごとに1回だけ使う。"""
    jitter = r.uniform(-1.0, 1.0)
    if is_sea(p) or deep_gap(p) < -1.0:
        return -3.0
    sg = shallow_gap(p)
    if sg < 0.0:
        return -RIVER_BED * smooth(0, RIVER_BANK, -sg) + 0.03 * jitter * smooth(0, RIVER_BANK, -sg)
    m = bump_mask(p, sg)
    h = (bump(p) + 0.03 * jitter) * m
    if on_path(p.x, p.y, 0.5):
        h -= 0.03
    return h


# ---------------------------------------------------------------- mountains
MOUNTAIN_ROAD = _pairs(sum([[p.x, p.y] for p in ROADS["mountain"][3:]], []))
MOUNTAIN_LEN = line_length(MOUNTAIN_ROAD)
BIG_PEAK = Vector((165.0, 178.0))
SUB_PEAKS = [(95, 205, 34, 26), (228, 205, 42, 34), (238, 118, 34, 28), (125, 240, 30, 22), (232, 42, 24, 18), (58, 232, 24, 14)]
RIVER_A = DEEP[0][1]
# 山の塔の足元: 斜面を円く平らに削り、すぐ横の山道と同じ高さにそろえる（塔の周りに魔物が湧けるように）。
TOWER_PAD_RADIUS, TOWER_PAD_BLEND = 10.0, 12.0
MOUNTAIN_PADS = [(Vector((sp["x"], sp["z"])), line_dist(Vector((sp["x"], sp["z"])), MOUNTAIN_ROAD)[1])
                 for sp in MAP["spawners"] if sp.get("biome") == "mountain"]


def north_of_river_a(p):
    """山は深い川の北東側だけ。川の進行方向の左側。"""
    best, side = 1e9, 0.0
    for a, b in zip(RIVER_A, RIVER_A[1:]):
        ab = b - a
        t = max(0.0, min(1.0, (p - a).dot(ab) / ab.dot(ab)))
        d = (a + ab * t - p).length
        if d < best:
            best, side = d, ab.x * (p.y - a.y) - ab.y * (p.x - a.x)
    return side > 0.0


def road_height(s):
    window = smooth(0, 50, s) * smooth(0, 50, MOUNTAIN_LEN - s)
    h = window * (8.0 + 6.0 * math.sin(s * 0.05) + 2.0 * math.sin(s * 0.11 + 1.0))
    # ドラゴンの広場は山道の上にある。広場の前後で道をゆるやかに広場の高さへそろえる。
    k = 1.0 - smooth(PLAZA_RADIUS, PLAZA_RADIUS + 40.0, abs(s - PLAZA_S))
    return h + (PLAZA_HEIGHT - h) * k


def mountain_height(p):
    b = BIOMES["mountain"]
    e = in_ellipse(p, b["x"], b["z"], b["rx"], b["rz"])
    mask = 1.0 - smooth(0.72, 1.0, e)
    mask *= smooth(6, 28, shore_dist(p)) * smooth(10, 34, line_dist(p, RIVER_A)[0] - 13)
    if mask <= 0.0 or not north_of_river_a(p):
        return -0.2
    n = noise.noise(Vector((p.x * 0.017, p.y * 0.017, 2.0)))
    ridged = 1.0 - abs(n) * 2.2
    fine = noise.noise(Vector((p.x * 0.06, p.y * 0.06, 5.0)))
    h = mask * (8.0 + 20.0 * max(ridged, 0.0) ** 2 + 5.0 * fine)
    h += mask * 88.0 * math.exp(-(((p - BIG_PEAK).length / 46.0) ** 1.6))
    for x, z, r, a in SUB_PEAKS:
        h += mask * a * math.exp(-(((p - Vector((x, z))).length / r) ** 2))
    d, s = line_dist(p, MOUNTAIN_ROAD)
    h = road_height(s) + (h - road_height(s)) * smooth(4.0, 28, d)
    w = plaza_weight(p)
    if w > 0.0:
        h += (PLAZA_HEIGHT - h) * w
    for c, cs in MOUNTAIN_PADS:
        w = 1.0 - smooth(TOWER_PAD_RADIUS, TOWER_PAD_RADIUS + TOWER_PAD_BLEND, (p - c).length)
        if w > 0.0:
            h += (road_height(cs) - h) * w
    return h


# ドラゴンの広場: 山の中ほどを平らに削った円い広場。南側が山道に開いていて、残りの三方は崖で囲まれる。
PLAZA = MAP["dragonPlaza"]
PLAZA_CENTER = Vector((PLAZA["x"], PLAZA["z"]))
PLAZA_RADIUS, PLAZA_BLEND, PLAZA_HEIGHT = PLAZA["radius"], PLAZA["blend"], PLAZA["height"]
PLAZA_S = line_dist(PLAZA_CENTER, MOUNTAIN_ROAD)[1]


def plaza_dist(p):
    return (p - PLAZA_CENTER).length


def plaza_weight(p):
    return 1.0 - smooth(PLAZA_RADIUS, PLAZA_RADIUS + PLAZA_BLEND, plaza_dist(p))


def terrain_y(p):
    if p.x > -70 and p.y > -45:
        return max(0.0, mountain_height(p))
    return 0.0


def ribbon(g, pts, width, y, mats, rng, step=7.0):
    stations = []
    for a, b in zip(pts, pts[1:]):
        n = max(1, int((b - a).length / step))
        for i in range(n):
            stations.append(a + (b - a) * (i / n))
    stations.append(pts[-1])
    rows = []
    for i, c in enumerate(stations):
        t = stations[min(i + 1, len(stations) - 1)] - stations[max(i - 1, 0)]
        t.normalize()
        side = Vector((-t.y, t.x))
        rows.append([g.bm.verts.new((*(c + side * ((k - 1) * width / 2 + rng.uniform(-0.7, 0.7) * (1 if k != 1 else 2))), y + rng.uniform(-0.03, 0.03))) for k in range(3)])
    for r0, r1 in zip(rows, rows[1:]):
        for k in range(2):
            for tri in ((r0[k], r0[k + 1], r1[k + 1]), (r0[k], r1[k + 1], r1[k])):
                f = g.bm.faces.new(tri)
                f.material_index = g._mi(rng.choice(mats))


def wall_segment(g, m, a, b, z0, z1, thick=1.5, extend=1.0):
    d = b - a
    l = d.length
    if l < 0.01:
        return
    c = (a + b) / 2
    g.box(m, (c.x, c.y, (z0 + z1) / 2), (l + extend * 2, thick, z1 - z0), rotz=math.atan2(d.y, d.x))


def prism(g, m, a0, a1, b0, b1, z_a, z_b, height):
    """Solid strip section between the left/right points a0,a1 and b0,b1; top at z_a / z_b."""
    vs = [g.bm.verts.new((*q, z)) for q, z in ((a0, z_a - height), (a1, z_a - height), (b1, z_b - height), (b0, z_b - height),
                                               (a0, z_a), (a1, z_a), (b1, z_b), (b0, z_b))]
    for f in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
        face = g.bm.faces.new([vs[i] for i in f])
        face.material_index = g._mi(m)


def bridge_profile(length):
    """Height along the bridge: hangs in a curve from the land and meets the island edge at the fixed slope."""
    span = length - BRIDGE_ISLAND_OVERLAP
    start_slope = max(0.0, 2.0 * H_CASTLE / span - BRIDGE_TAN)
    bend = (BRIDGE_TAN - start_slope) / (2.0 * span)
    return lambda s: H_CASTLE if s >= span else bend * s * s + start_slope * s


def rope_line(g, m, points, side, half_width):
    """A thin rope through the given (position, height) points."""
    for (p0, z0), (p1, z1) in zip(points, points[1:]):
        prism(g, m, p0 - side * half_width, p0 + side * half_width, p1 - side * half_width, p1 + side * half_width, z0, z1, half_width * 2)


def build_bridge(g, ropes, rng, start, end):
    d = end - start
    length = d.length
    d.normalize()
    side = Vector((-d.y, d.x))
    profile = bridge_profile(length)
    hw = BRIDGE_W / 2
    # 歩く面。隙間のない1枚の板で、当たり判定と NavMesh はこれを使う。
    n = int(length / 3.0)
    for i in range(n):
        s0, s1 = length * i / n, length * (i + 1) / n
        p0, p1 = start + d * s0, start + d * s1
        prism(g, P["wood_dark"], p0 - side * hw, p0 + side * hw, p1 - side * hw, p1 + side * hw, profile(s0), profile(s1), 0.35)
    # 踏み板。少しずつ長さと向きを変えて、手で並べたように見せる。
    s = 0.2
    while s < length - BRIDGE_ISLAND_OVERLAP:
        w = rng.uniform(0.95, 1.2)
        s1 = s + w
        c0, c1 = start + d * s, start + d * s1
        over_a, over_b = rng.uniform(-0.25, 0.25), rng.uniform(-0.25, 0.25)
        skew = rng.uniform(-0.07, 0.07)
        m = P["wood"] if rng.random() < 0.72 else P["wood_light"]
        prism(g=ropes, m=m,
              a0=c0 - side * (hw + over_a) + d * skew, a1=c0 + side * (hw + over_b) - d * skew,
              b0=c1 - side * (hw + over_a) + d * skew, b1=c1 + side * (hw + over_b) - d * skew,
              z_a=profile(s) + 0.07, z_b=profile(s1) + 0.07, height=0.14)
        s = s1 + rng.uniform(0.08, 0.2)
    # 両岸の門柱。橋の入口と島の縁に2本ずつ立てて、上に横木を渡す。
    tower_at = (BRIDGE_POST_INSET, length - BRIDGE_ISLAND_OVERLAP + 1.0)
    post_x = hw + 0.9
    for at in tower_at:
        c = start + d * at
        base = profile(at)
        for sgn in (-1, 1):
            q = c + side * sgn * post_x
            g.box(P["wood_dark"], (q.x, q.y, base + (BRIDGE_POST_HEIGHT - 4.0) / 2), (1.0, 1.0, BRIDGE_POST_HEIGHT + 4.0), rotz=math.atan2(d.y, d.x), jitter=0.04)
        ropes.box(P["wood"], (c.x, c.y, base + BRIDGE_POST_HEIGHT - 0.9), (0.7, BRIDGE_W + 3.4, 0.7), rotz=math.atan2(d.y, d.x), jitter=0.04)
        ropes.box(P["wood"], (c.x, c.y, base + BRIDGE_POST_HEIGHT - 2.3), (0.45, BRIDGE_W + 2.6, 0.45), rotz=math.atan2(d.y, d.x), jitter=0.03)
    # 主ロープは門柱の上から大きくたるみ、そこから吊りロープで踏み板を吊る。手すりは腰の高さのロープ。
    s_a, s_b = tower_at
    steps = int((s_b - s_a) / BRIDGE_HANGER_SPACING)
    for sgn in (-1, 1):
        cable, rail, foot = [], [], []
        for i in range(steps + 1):
            at = s_a + (s_b - s_a) * i / steps
            t = i / steps * 2.0 - 1.0
            clearance = BRIDGE_CABLE_LOW + (BRIDGE_POST_HEIGHT - BRIDGE_CABLE_LOW) * t * t
            base = start + d * at + side * sgn * (hw + 0.25)
            deck_z = profile(at)
            cable.append((base, deck_z + clearance))
            rail.append((base, deck_z + BRIDGE_RAIL_HEIGHT))
            foot.append((base, deck_z + 0.25))
            ropes.box(P["rope"], (base.x, base.y, deck_z + clearance / 2), (0.1, 0.1, clearance), rotz=math.atan2(d.y, d.x))
        rope_line(ropes, P["rope"], cable, side, 0.13)
        rope_line(ropes, P["rope"], rail, side, 0.07)
        rope_line(ropes, P["wood_dark"], foot, side, 0.14)


def build_island(g, rng, castle, angles):
    rings = [(R_ISLAND, 0.0), (R_ISLAND * 0.92, -5.0), (R_ISLAND * 0.68, -13.0), (R_ISLAND * 0.4, -21.0), (R_ISLAND * 0.12, -27.0)]
    n = 28
    bm = g.bm
    top = bm.verts.new((castle.x, castle.y, H_CASTLE))
    rows = []
    for ri, (r, dz) in enumerate(rings):
        row = []
        for i in range(n):
            a = i / n * math.tau
            w = 1.0 + rng.uniform(-0.05, 0.05) * (1 if ri else 0.35)
            row.append(bm.verts.new((castle.x + math.cos(a) * r * w, castle.y + math.sin(a) * r * w, H_CASTLE + dz + (rng.uniform(-1.5, 1.5) if ri else 0.0))))
        rows.append(row)
    tip = bm.verts.new((castle.x + 2, castle.y - 1, H_CASTLE - 31))
    for i in range(n):
        j = (i + 1) % n
        f = bm.faces.new((top, rows[0][i], rows[0][j]))
        f.material_index = g._mi(P["scorched"])
        for ri in range(len(rows) - 1):
            m = P["stone_dark"] if (i + ri) % 3 else P["obsidian"]
            f = bm.faces.new((rows[ri][i], rows[ri + 1][i], rows[ri + 1][j], rows[ri][j]))
            f.material_index = g._mi(m if ri else P["scorched"])
        f = bm.faces.new((rows[-1][i], tip, rows[-1][j]))
        f.material_index = g._mi(P["obsidian"])
    # 低い縁。橋の出入口だけ開ける。
    for i in range(n):
        a0, a1 = i / n * math.tau, (i + 1) / n * math.tau
        mid = (a0 + a1) / 2
        if any(abs((mid - b + math.pi) % math.tau - math.pi) < math.radians(9) for b in angles):
            continue
        q0 = castle + Vector((math.cos(a0), math.sin(a0))) * (R_ISLAND - 1.2)
        q1 = castle + Vector((math.cos(a1), math.sin(a1))) * (R_ISLAND - 1.2)
        wall_segment(g, P["stone_dark"], q0, q1, H_CASTLE - 0.2, H_CASTLE + 0.6, thick=1.0, extend=0.2)


def build_field():
    rng = random.Random(11)
    col, root = new_model("LP_Field")
    ground = Group("Field_Ground", rng)
    decor = Group("Field_Decor", rng)
    trees = Group("Field_Trees", rng)
    walls = Group("Field_Walls", rng)
    water = Group("Field_Water", rng)
    mountains = Group("Field_Mountains", rng)
    bridges = Group("Field_Bridges", rng)
    bridge_ropes = Group("Field_BridgeRopes", rng)
    barrier = Group("Field_Barrier", rng)
    tree_cols = Group("Field_TreeColliders", rng)
    mountain_fill = Group("Field_MountainFill", rng)

    def add_tree(g, x, y, s, kind=None, z=0.0):
        tree(g, rng, x, y, s, kind, z)
        tree_cols.cyl(P["bark"], (x, y, z - 0.5), 0.26 * s + 0.12, 7.0, n=6)

    # Ground: triangulated grid, flat shaded, material picked per triangle.
    # 昔の 4m 格子の地面が使っていた乱数を同じ順に使い切ってから、新しい 2m 格子の地面を専用の乱数で作る。
    # こうすると、あとに続く草や木の並びが変わらない。
    nx, ny, w, d = 125, 125, float(MAP["size"]), float(MAP["size"])

    def face_mat(cx, cy, r, new=False):
        p = Vector((cx, cy))
        if is_sea(p) or deep_gap(p) < 3.5 or shore_dist(p) < 5.0:
            return P["sand"]
        if new:
            sg = shallow_gap(p)
            if sg < -0.6:
                # 川底: 土の色。ところどころ小石。
                k = r.random()
                return P["stone_dark"] if k < 0.12 else (P["dirt_dark"] if k < 0.55 else P["dirt"])
            if in_forest_plaza(p):
                dist = (p - FOREST_CENTER).length
                k = r.random()
                if dist > FOREST_RADIUS - 2.5:
                    return P["stone_dark"] if k < 0.4 else P["moss"]
                n = noise.noise(Vector((cx * 0.13, cy * 0.13, 5.5)))
                if n > 0.25:
                    return P["moss"] if k < 0.7 else P["forest_a"]
                return P["dirt_dark"] if k < 0.55 else P["dirt"]
            if on_forest_trail(p):
                return P["dirt_dark"] if r.random() < 0.5 else P["dirt"]
        if shallow_gap(p) < 1.5:
            return P["sand_wet"]
        if in_scorpion_plaza(p):
            jitter = r.uniform(-0.2, 0.2)  # 砂漠の面と同じ数だけ乱数を使い、あとの飾りの並びを変えない
            d = (p - SCORPION_CENTER).length
            if d > SCORPION_RADIUS - 3.0:
                return P["sandstone_dark"]
            ring = int(d / 4.0) % 2
            return P["sandstone"] if (ring == 0) == (jitter > -0.12) else P["sandstone_b"]
        if on_path(cx, cy):
            return P["dirt"]
        if near_spawner(p, 7.0):
            return P["dirt_dark"]
        if (p - TOWN).length < 9:
            return P["grass_b"]
        if in_desert(p):
            n = noise.noise(Vector((cx * 0.05, cy * 0.05, 3.1))) + r.uniform(-0.2, 0.2)
            return P["sand_b"] if n > 0.2 else (P["sand_c"] if n < -0.25 else P["sand"])
        if in_forest(p):
            return P["forest_a"] if r.random() < 0.6 else P["forest_b"]
        n = noise.noise(Vector((cx * 0.02, cy * 0.02, 1.7))) + r.uniform(-0.25, 0.25)
        if n > 0.45:
            return P["grass_dry"]
        if n > 0.1:
            return P["grass_b"]
        if n < -0.35:
            return P["grass_c"]
        return P["grass_a"]

    # 昔の地面の乱数の使い方をなぞる（形は作らない）。
    for i in range(nx + 1):
        for j in range(ny + 1):
            ground_height(-w / 2 + i * w / nx, -d / 2 + j * d / ny, rng)
    for i in range(nx):
        for j in range(ny):
            xs = [-w / 2 + (i + a) * w / nx for a in (0, 1, 1, 0)]
            ys = [-d / 2 + (j + b) * d / ny for b in (0, 0, 1, 1)]
            tris = [(0, 1, 2), (0, 2, 3)] if (i + j) % 2 == 0 else [(0, 1, 3), (1, 2, 3)]
            for tr in tris:
                face_mat(sum(xs[k] for k in tr) / 3, sum(ys[k] for k in tr) / 3, rng)

    # 新しい地面: 2m 格子。ほんの少し凹凸があり、浅い川は窪んで川底が見える。
    grng = random.Random(41)
    gn = int(round(w / GROUND_CELL))
    gx0, gy0 = -w / 2, -d / 2
    bm = ground.bm
    heights = [[field_height(Vector((gx0 + i * GROUND_CELL, gy0 + j * GROUND_CELL)), grng) for j in range(gn + 1)] for i in range(gn + 1)]
    grid = [[bm.verts.new((gx0 + i * GROUND_CELL, gy0 + j * GROUND_CELL, heights[i][j])) for j in range(gn + 1)] for i in range(gn + 1)]
    for i in range(gn):
        for j in range(gn):
            a, b, c, e = grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]
            tris = [(a, b, c), (a, c, e)] if (i + j) % 2 == 0 else [(a, b, e), (b, c, e)]
            for t in tris:
                f = bm.faces.new(t)
                cx = sum(v.co.x for v in t) / 3
                cy = sum(v.co.y for v in t) / 3
                f.material_index = ground._mi(face_mat(cx, cy, grng, new=True))

    # 地面の上に置く物を凹凸に沿わせるための高さ（海と川の中は 0）。
    lift = [[max(h, -0.05) if h > -0.5 else 0.0 for h in col_h] for col_h in heights]

    def ground_lift(x, y):
        fx, fy = (x - gx0) / GROUND_CELL, (y - gy0) / GROUND_CELL
        i, j = int(math.floor(fx)), int(math.floor(fy))
        if i < 0 or j < 0 or i >= gn or j >= gn:
            return 0.0
        u, v = fx - i, fy - j
        return (lift[i][j] * (1 - u) + lift[i + 1][j] * u) * (1 - v) + (lift[i][j + 1] * (1 - u) + lift[i + 1][j + 1] * u) * v

    write_ground_heights(heights, gx0, gy0, GROUND_CELL, gn)

    # Water: 海と深い川は地面より下。浅い川は窪んだ川底の上、地表のすぐ下に水面を張る（縁は岸の地面に隠れる）。
    ws, wd, wm = [P["water_shallow"], P["water_shallow_b"]], [P["water_deep"], P["water_deep_b"]], [P["water_sea"], P["water_sea_b"]]
    for width, pts in DEEP:
        ribbon(water, pts, width + 6, -0.5, wd, rng)
    for width, pts in SHALLOW:
        ribbon(water, pts, width + 1.0, RIVER_WATER_Y, ws, rng)
    cell = 12.0
    x = -60.0
    while x < 430:
        z = -430.0
        while z < 0:
            corners = [Vector((x, z)), Vector((x + cell, z)), Vector((x + cell, z + cell)), Vector((x, z + cell))]
            if any(is_sea(q) for q in corners):
                # 隣の四角と角の高さをそろえて、継ぎ目に隙間が出ないようにする。
                vs = [water.bm.verts.new((q.x, q.y, -0.5 + 0.1 * noise.noise(Vector((q.x * 0.31, q.y * 0.31, 7.0))))) for q in corners]
                for tri in ((vs[0], vs[1], vs[2]), (vs[0], vs[2], vs[3])):
                    f = water.bm.faces.new(tri)
                    f.material_index = water._mi(rng.choice(wm))
            z += cell
        x += cell

    # Mountains: 道は起伏のある尾根道。平らな地面の上にかぶせる。
    mx0, mx1, mz0, mz1, mc = -60.0, 250.0, -44.0, 250.0, 4.0
    cols, rows = int((mx1 - mx0) / mc), int((mz1 - mz0) / mc)
    heights = [[mountain_height(Vector((mx0 + i * mc, mz0 + j * mc))) for j in range(rows + 1)] for i in range(cols + 1)]
    mverts = {}

    def mv(i, j):
        k = (i, j)
        if k not in mverts:
            jx = rng.uniform(-0.9, 0.9) if 0 < i < cols and 0 < j < rows else 0.0
            jz = rng.uniform(-0.9, 0.9) if 0 < i < cols and 0 < j < rows else 0.0
            mverts[k] = mountains.bm.verts.new((mx0 + i * mc + jx, mz0 + j * mc + jz, heights[i][j]))
        return mverts[k]

    # 山の面の色は専用の乱数で決める。広場など山の形を変えても、あとに続く草原の飾りの並びが変わらない。
    mrng = random.Random(17)

    def mountain_mat(t):
        cx = sum(v.co.x for v in t) / 3
        cy = sum(v.co.y for v in t) / 3
        hi = max(v.co.z for v in t)
        lo = min(v.co.z for v in t)
        n = (t[1].co - t[0].co).cross(t[2].co - t[0].co)
        steep = 1.0 - abs(n.z) / max(n.length, 1e-5)
        pd = plaza_dist(Vector((cx, cy)))
        if pd < PLAZA_RADIUS:
            # 広場の床: 縁を石で囲み、中は土。ドラゴンの炎で焦げた跡がまだらに残る。
            if pd > PLAZA_RADIUS - 2.5:
                return P["stone"] if mrng.random() < 0.6 else P["stone_dark"]
            burn = noise.noise(Vector((cx * 0.11, cy * 0.11, 7.0)))
            if burn > 0.28:
                return P["ash"] if burn > 0.42 else P["scorched"]
            return P["dirt"] if mrng.random() < 0.6 else P["dirt_dark"]
        if line_dist(Vector((cx, cy)), MOUNTAIN_ROAD)[0] < 3.8 and steep < 0.12:
            return P["dirt"]
        if hi > 62:
            return P["snow"]
        if steep > 0.22 or hi > 38:
            return P["stone"] if mrng.random() < 0.7 else P["stone_dark"]
        if lo > 16:
            return P["grass_dry"]
        return P["grass_c"] if mrng.random() < 0.5 else P["grass_a"]

    for i in range(cols):
        for j in range(rows):
            cells = [(i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)]
            tris = [(0, 1, 2), (0, 2, 3)] if (i + j) % 2 == 0 else [(0, 1, 3), (1, 2, 3)]
            for tr in tris:
                ids = [cells[k] for k in tr]
                if all(heights[a][b] <= 0.02 for a, b in ids):
                    continue
                t = [mv(a, b) for a, b in ids]
                f = mountains.bm.faces.new(t)
                f.material_index = mountains._mi(mountain_mat(t))

    # 山の中身。地面と山の間に歩ける空間（NavMesh）ができないように埋める。見た目には出さない。
    fill = 8.0
    fx = mx0
    while fx < mx1:
        fz = mz0
        while fz < mz1:
            samples = [mountain_height(Vector((fx + fill * a / 2, fz + fill * b / 2))) for a in range(3) for b in range(3)]
            top = min(samples) - 1.5
            if top > 0.8:
                mountain_fill.box(P["stone"], (fx + fill / 2, fz + fill / 2, (top - 1.0) / 2), (fill + 0.5, fill + 0.5, top + 1.0))
            fz += fill
        fx += fill

    # Sky island and the three bridges
    castle = CASTLE
    angles = []
    for start in BRIDGE_STARTS:
        dirv = (start - castle).normalized()
        angles.append(math.atan2(dirv.y, dirv.x))
        build_bridge(bridges, bridge_ropes, rng, start, castle + dirv * (R_ISLAND - BRIDGE_ISLAND_OVERLAP))
    build_island(bridges, rng, castle, angles)

    def on_bridge(p, margin):
        for start in BRIDGE_STARTS:
            if line_dist(p, [start, castle])[0] < BRIDGE_W / 2 + margin:
                return True
        return False

    # Invisible walls: 深い川の両岸、海岸（橋の下だけ開ける）、地図の外周。
    for width, pts in DEEP:
        for sign in (-1, 1):
            for a, b in zip(pts, pts[1:]):
                t = (b - a).normalized()
                n = Vector((-t.y, t.x)) * sign * (width / 2 + 0.5)
                wall_segment(barrier, P["stone"], a + n, b + n, -1, 6)
        for a, b in ((pts[0], pts[1]), (pts[-1], pts[-2])):
            t = (b - a).normalized()
            n = Vector((-t.y, t.x)) * (width / 2 + 1)
            wall_segment(barrier, P["stone"], a - n, a + n, -1, 6)
    shore = SHORE[1:-1]
    for a, b in zip(shore, shore[1:]):
        n = max(1, int((b - a).length / 3.0))
        for i in range(n):
            q0, q1 = a + (b - a) * (i / n), a + (b - a) * ((i + 1) / n)
            if on_bridge((q0 + q1) / 2, 0.2):
                continue
            wall_segment(barrier, P["stone"], q0, q1, -1, 6, extend=0.3)
    for a, b in (((-HALF, -HALF), (shore[0].x, -HALF)), ((-HALF, -HALF), (-HALF, HALF)), ((-HALF, HALF), (HALF, HALF)), ((HALF, HALF), (HALF, shore[-1].y))):
        wall_segment(barrier, P["stone"], Vector(a), Vector(b), -1, 130, thick=2.0)
    # 橋の両側と浮島の縁にも見えない壁を立てて、橋や島から海へ落ちないようにする（海にはどこからも入れない）。
    for start in BRIDGE_STARTS:
        dirv = (castle - start).normalized()
        end = castle - dirv * (R_ISLAND - BRIDGE_ISLAND_OVERLAP)
        length = (end - start).length
        profile = bridge_profile(length)
        side = Vector((-dirv.y, dirv.x)) * (BRIDGE_W / 2 + 0.6)
        n = max(1, int(length / 3.0))
        for i in range(n):
            s0, s1 = length * i / n, length * (i + 1) / n
            z0 = min(profile(s0), profile(s1)) - 1.0
            z1 = max(profile(s0), profile(s1)) + 6.0
            for sgn in (-1, 1):
                wall_segment(barrier, P["stone"], start + dirv * s0 + side * sgn, start + dirv * s1 + side * sgn, z0, z1, thick=1.0, extend=0.3)
    rim = int(math.tau * R_ISLAND / 1.5)
    for i in range(rim):
        a0, a1 = i / rim * math.tau, (i + 1) / rim * math.tau
        q0 = castle + Vector((math.cos(a0), math.sin(a0))) * (R_ISLAND - 0.6)
        q1 = castle + Vector((math.cos(a1), math.sin(a1))) * (R_ISLAND - 0.6)
        if on_bridge((q0 + q1) / 2, 0.6):
            continue
        wall_segment(barrier, P["stone"], q0, q1, H_CASTLE - 1.0, H_CASTLE + 6.0, thick=1.0, extend=0.3)

    def blocked(p, extra=0.0):
        if (p - TOWN).length < 22 + extra:
            return True
        if is_sea(p) or shore_dist(p) < 6 + extra or deep_gap(p) < 3 + extra or shallow_gap(p) < 1.5 + extra:
            return True
        if on_path(p.x, p.y, 0.8 + extra) or near_spawner(p, 8 + extra) or in_scorpion_plaza(p, 4 + extra):
            return True
        for rx, rz, sx, sy, sz in ROCKS:
            if abs(p.x - rx) < sx / 2 + 1.5 + extra and abs(p.y - rz) < sz / 2 + 1.5 + extra:
                return True
        return False

    def rand_point(margin=2.0):
        return Vector((rng.uniform(-w / 2 + margin, w / 2 - margin), rng.uniform(-d / 2 + margin, d / 2 - margin)))

    def on_mountain(p):
        return p.x > -60 and p.y > -44 and mountain_height(p) > 0.02

    def meadow(p):
        return not in_desert(p) and not in_forest(p) and not on_mountain(p)

    # 森の広場と小道に入る草やしげみは、乱数だけ使って捨てる（ほかの飾りの並びを変えない）。
    scratch = Group("Scratch", rng)

    def clear_of_forest_plaza(p, margin=1.0):
        return not in_forest_plaza(p, margin) and not on_forest_trail(p, margin * 0.5)

    # Grass tufts
    placed = 0
    while placed < 8000:
        p = rand_point()
        if blocked(p) or in_desert(p) or on_mountain(p):
            continue
        if in_forest(p) and rng.random() < 0.7:
            continue
        placed += 1
        z = ground_height(p.x, p.y, rng) - 0.05
        tg = decor if clear_of_forest_plaza(p) else scratch
        for _ in range(3):
            ox, oy = rng.uniform(-0.25, 0.25), rng.uniform(-0.25, 0.25)
            tilt = Matrix.Rotation(math.radians(rng.uniform(-18, 18)), 4, Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0)).normalized())
            tg.cone(P["tuft"], (p.x + ox, p.y + oy, z), 0.09, rng.uniform(0.35, 0.65), n=3, rotz=rng.random() * 6, tilt=tilt)

    # Flowers, in loose clusters
    flower_mats = [P["flower_w"], P["flower_y"], P["flower_p"], P["flower_b"]]
    for _ in range(170):
        c = rand_point()
        if blocked(c) or not meadow(c):
            continue
        fm = rng.choice(flower_mats)
        for _ in range(rng.randint(4, 9)):
            p = c + Vector((rng.uniform(-1.6, 1.6), rng.uniform(-1.6, 1.6)))
            if blocked(p):
                continue
            z = ground_height(p.x, p.y, rng)
            sh = rng.uniform(0.2, 0.35)
            decor.cyl(P["tuft"], (p.x, p.y, z - 0.02), 0.015, sh, n=3)
            decor.ico(fm, (p.x, p.y, z + sh), 0.09, sub=0, rotz=rng.random() * 6)

    # Small stones and bushes
    for _ in range(260):
        p = rand_point()
        if blocked(p) or on_mountain(p):
            continue
        s = rng.uniform(0.2, 0.55)
        decor.ico(P["stone"], (p.x, p.y, -0.05), s, sub=0, scale=(1, 1, 0.6), jitter=s * 0.25, rotz=rng.random() * 6)
    for _ in range(170):
        p = rand_point()
        if blocked(p, 1.0) or in_desert(p) or on_mountain(p):
            continue
        s = rng.uniform(0.6, 1.0)
        leaf = P["leaf_a"] if rng.random() < 0.5 else P["leaf_b"]
        (decor if clear_of_forest_plaza(p, 2.0) else scratch).ico(leaf, (p.x, p.y, 0.25 * s), 0.9 * s, sub=1, scale=(1.2, 1, 0.75), jitter=0.12 * s)

    # Meadow trees
    placed = tries = 0
    while placed < 110 and tries < 4000:
        tries += 1
        p = rand_point(3.0)
        if blocked(p, 2.0) or not meadow(p) or (p - TOWN).length < 30:
            continue
        add_tree(trees, p.x, p.y, rng.uniform(0.9, 1.3))
        placed += 1

    # Forest: 巨大樹の森。高さ24〜34mの巨大樹を15m前後の間隔で立て、すき間を中くらいの木で埋め、足元にシダを敷く。
    # 太い幹が魔法を遮る。地面は平らなので歩きやすい。森は専用の乱数で作り、木を増減してもほかの飾りの並びが変わらないようにする。
    frng = random.Random(29)

    def forest_point():
        return Vector((frng.uniform(-w / 2 + 3, w / 2 - 3), frng.uniform(-d / 2 + 3, d / 2 - 3)))

    def giant_tree(x, y, s):
        h = frng.uniform(24, 34) * s
        r = frng.uniform(1.1, 1.5) * s
        trunk_h = h * 0.62
        trees.cyl(P["bark"], (x, y, -0.3), r, trunk_h + 0.3, n=9, r2=r * 0.6, jitter=0.06 * r, rotz=frng.random() * 6)
        trees.cyl(P["bark"], (x, y, trunk_h - 0.2), r * 0.6, h - trunk_h, n=7, r2=r * 0.22, rotz=frng.random() * 6)
        # 根の張り出し
        for k in range(6):
            a = k / 6 * math.tau + frng.uniform(-0.3, 0.3)
            dirv = Vector((math.cos(a), math.sin(a), 0))
            tilt = Matrix.Rotation(math.radians(frng.uniform(62, 74)), 4, Vector((-dirv.y, dirv.x, 0)))
            trees.cone(P["bark"], (x + dirv.x * r * 0.5, y + dirv.y * r * 0.5, frng.uniform(0.6, 1.2) * r), r * 0.42, r * frng.uniform(2.0, 2.8), n=5, tilt=tilt)
        # 太い枝と、その先の葉の塊
        leaves = [P["leaf_a"], P["leaf_b"], P["leaf_pine"]]
        for k in range(frng.randint(3, 5)):
            a = frng.random() * math.tau
            dirv = Vector((math.cos(a), math.sin(a), 0))
            z = h * frng.uniform(0.55, 0.78)
            length = frng.uniform(5.0, 8.5) * s
            ang = math.radians(frng.uniform(48, 64))
            tilt = Matrix.Rotation(ang, 4, Vector((-dirv.y, dirv.x, 0)))
            trees.cyl(P["bark"], (x, y, z), r * 0.32, length, n=5, r2=r * 0.12, tilt=tilt)
            tip = Vector((x, y, z)) + (dirv * math.sin(ang) + Vector((0, 0, math.cos(ang)))) * length
            cr = frng.uniform(3.8, 5.5) * s
            trees.ico(frng.choice(leaves), tip + Vector((0, 0, cr * 0.25)), cr, sub=1, scale=(1, 1, 0.55), jitter=0.18 * cr, rotz=frng.random() * 6)
        # てっぺんの大きな傘
        for k in range(frng.randint(3, 4)):
            off = Vector((frng.uniform(-3.5, 3.5), frng.uniform(-3.5, 3.5), 0)) * s
            cr = frng.uniform(5.5, 8.0) * s
            trees.ico(frng.choice(leaves), (x + off.x, y + off.y, h * frng.uniform(0.88, 1.0)), cr, sub=1, scale=(1, 1, 0.5), jitter=0.16 * cr, rotz=frng.random() * 6)
        tree_cols.cyl(P["bark"], (x, y, -0.5), r * 1.15 + 0.2, 14.0, n=8)
        return r

    def fern(x, y):
        z = ground_height(x, y, frng) - 0.05
        mtl = P["moss"] if frng.random() < 0.6 else P["tuft"]
        for k in range(5):
            a = k / 5 * math.tau + frng.uniform(-0.3, 0.3)
            tilt = Matrix.Rotation(math.radians(frng.uniform(50, 70)), 4, Vector((-math.sin(a), math.cos(a), 0)))
            decor.cone(mtl, (x, y, z), 0.22, frng.uniform(1.0, 1.6), n=3, rotz=a, tilt=tilt)

    giants = []
    for _ in range(30000):
        p = forest_point()
        if not in_forest(p) or blocked(p, 3.0) or road_dist(p) < 9.0 or in_forest_plaza(p, 9.0) or on_forest_trail(p, 4.0):
            continue
        if any((p - q).length < rq for q, rq in giants):
            continue
        rad = giant_tree(p.x, p.y, frng.uniform(0.9, 1.15))
        giants.append((p, 13.0 + rad * 2))

    cell_size = 4.6
    used = {}
    forest_count = 0
    for _ in range(40000):
        p = forest_point()
        if not in_forest(p) or blocked(p, 1.5) or road_dist(p) < 5.5 or in_forest_plaza(p, 4.5) or on_forest_trail(p, 1.5):
            continue
        if any((p - q).length < 5.0 + (rq - 13.0) for q, rq in giants):
            continue
        key = (int(p.x // cell_size), int(p.y // cell_size))
        near = False
        for ox in (-1, 0, 1):
            for oy in (-1, 0, 1):
                q = used.get((key[0] + ox, key[1] + oy))
                if q is not None and (q - p).length < cell_size:
                    near = True
        if near:
            continue
        used[key] = p
        ts = frng.uniform(2.2, 3.4)
        tree(trees, frng, p.x, p.y, ts, "pine" if frng.random() < 0.6 else "round")
        tree_cols.cyl(P["bark"], (p.x, p.y, -0.5), 0.26 * ts + 0.12, 7.0, n=6)
        forest_count += 1

    ferns = 0
    for _ in range(12000):
        p = forest_point()
        if not in_forest(p) or blocked(p, 0.5) or road_dist(p) < 3.5 or in_forest_plaza(p, -1.0) or on_forest_trail(p, -1.0):
            continue
        fern(p.x, p.y)
        ferns += 1
        if ferns >= 2200:
            break
    print("FOREST_TREES", forest_count, "GIANTS", len(giants), "FERNS", ferns)

    # Mountain pines: まばらなので見通しが良い。
    placed = tries = 0
    while placed < 45 and tries < 3000:
        tries += 1
        p = Vector((rng.uniform(-40, 245), rng.uniform(-30, 245)))
        h = mountain_height(p)
        if h < 3 or h > 32 or line_dist(p, MOUNTAIN_ROAD)[0] < 9 or blocked(p) or plaza_dist(p) < PLAZA_RADIUS + PLAZA_BLEND + 4:
            continue
        add_tree(trees, p.x, p.y, rng.uniform(0.8, 1.1), "pine", h - 0.2)
        placed += 1

    # Road markers: どこを歩けばいいか分かるように、道の両脇に目印を立てる。
    for name, pts in ROADS.items():
        total = line_length(pts)
        s = 16.0
        side = 1
        while s < total - 6:
            remain = s
            for a, b in zip(pts, pts[1:]):
                l = (b - a).length
                if remain <= l:
                    t = (b - a).normalized()
                    q = a + (b - a) * (remain / l) + Vector((-t.y, t.x)) * side * 4.6
                    break
                remain -= l
            side = -side
            s += 13.0 if name == "mountain" else 19.0
            if shallow_gap(q) < 1.5 or deep_gap(q) < 2 or is_sea(q):
                continue
            z = terrain_y(q)
            if name == "desert":
                decor.ico(P["stone"], (q.x, q.y, z + 0.15), 0.42, sub=0, scale=(1, 1, 0.8), jitter=0.06)
                decor.ico(P["stone_dark"], (q.x, q.y, z + 0.62), 0.28, sub=0, jitter=0.04)
            else:
                decor.cyl(P["wood"], (q.x, q.y, z - 0.1), 0.09, 1.5, n=5)
                decor.box(P["wood_dark"], (q.x, q.y, z + 1.32), (0.75, 0.1, 0.24), rotz=rng.random() * 6)
                if name == "forest":
                    decor.ico(P["lamp"], (q.x, q.y, z + 1.62), 0.16, sub=0)

    # Desert
    def cactus(x, y):
        h = rng.uniform(1.6, 3.0)
        decor.cyl(P["cactus"], (x, y, -0.1), 0.22, h, n=6, r2=0.17, jitter=0.02)
        for sgn in (-1, 1):
            if rng.random() < 0.75:
                az = rng.random() * 6
                ax, ay = math.cos(az) * sgn, math.sin(az) * sgn
                z = h * rng.uniform(0.4, 0.6)
                decor.box(P["cactus"], (x + ax * 0.32, y + ay * 0.32, z), (0.6, 0.2, 0.2), rotz=az)
                decor.cyl(P["cactus"], (x + ax * 0.6, y + ay * 0.6, z - 0.1), 0.12, rng.uniform(0.6, 1.0), n=5, r2=0.09)

    def in_desert_clear(p, margin=2.0):
        return in_desert(p) and not blocked(p, margin)

    placed = tries = 0
    while placed < 90 and tries < 4000:
        tries += 1
        p = Vector((rng.uniform(-90, 80), rng.uniform(-60, 95)))
        if in_desert_clear(p):
            cactus(p.x, p.y)
            placed += 1
    placed = tries = 0
    while placed < 60 and tries < 3000:
        tries += 1
        p = Vector((rng.uniform(-90, 80), rng.uniform(-60, 95)))
        if not in_desert_clear(p):
            continue
        r = rng.uniform(4, 11)
        decor.ico(rng.choice([P["sand_b"], P["sand_c"]]), (p.x, p.y, -0.04), r, sub=1, scale=(1.3, 0.8, 0.045), jitter=0.03 * r, rotz=rng.random() * 6)
        placed += 1
    for _ in range(14):
        p = Vector((rng.uniform(-80, 70), rng.uniform(-50, 85)))
        if in_desert_clear(p):
            for k in range(4):
                decor.box(P["bone"], (p.x + k * 0.5 - 0.8, p.y + rng.uniform(-0.2, 0.2), 0.06), (0.1, 1.2, 0.08), rotz=rng.uniform(-0.3, 0.3))

    # スコーピオンの広場を囲む崩れた砂岩の柱。南西（砂漠の中央側）を入口として空ける。柱は当たり判定つき。
    prng = random.Random(53)
    scorpion_gaps = road_gap_angles(SCORPION_CENTER, SCORPION_RADIUS + 1.6)
    for k in range(12):
        a = k / 12 * math.tau
        if near_angle(a, scorpion_gaps, 0.42):
            continue
        r = SCORPION_RADIUS + 1.6
        x, y = SCORPION_CENTER.x + math.cos(a) * r, SCORPION_CENTER.y + math.sin(a) * r
        h = prng.uniform(2.5, 7.5)
        walls.cyl(P["sandstone"], (x, y, -0.2), 1.0, h, n=6, r2=0.85, rotz=prng.random() * 6)
        walls.box(P["sandstone_dark"], (x, y, 0.15), (2.6, 2.6, 0.5), rotz=prng.random() * 6)
        if h > 5.5:
            walls.box(P["sandstone_b"], (x, y, h + 0.15), (2.4, 1.2, 0.5), rotz=a + math.pi / 2)
        tree_cols.cyl(P["sandstone"], (x, y, -0.5), 1.05, h + 0.6, n=6)

    # 森の広場: 縁に大きな枯れ木を並べ、小道の側だけ空ける。床には骨と苔むした石が散らばる。枯れ木は当たり判定つき。
    wrng = random.Random(61)
    forest_gaps = road_gap_angles(FOREST_CENTER, FOREST_RADIUS + 2.5)
    entrance = forest_gaps[0] if forest_gaps else 0.0
    for k in range(26):
        a = k / 26 * math.tau + wrng.uniform(-0.08, 0.08)
        if near_angle(a, forest_gaps, 0.32):
            continue
        r = FOREST_RADIUS + wrng.uniform(1.0, 4.5)
        x, y = FOREST_CENTER.x + math.cos(a) * r, FOREST_CENTER.y + math.sin(a) * r
        s = wrng.uniform(2.6, 3.8)
        dead_tree(trees, wrng, x, y, s)
        tree_cols.cyl(P["bark"], (x, y, -0.5), 0.2 * s + 0.15, 9.0, n=6)
    # 倒れた枯れ木（縁の内側に少しだけ）
    for k in range(4):
        a = entrance + math.pi * (0.45 + 0.37 * k) + wrng.uniform(-0.1, 0.1)
        if near_angle(a, forest_gaps, 0.6):
            a += 0.7
        r = FOREST_RADIUS - wrng.uniform(3.0, 5.0)
        x, y = FOREST_CENTER.x + math.cos(a) * r, FOREST_CENTER.y + math.sin(a) * r
        length = wrng.uniform(5.0, 8.0)
        tilt = Matrix.Rotation(math.radians(90), 4, Vector((math.cos(a + math.pi / 2), math.sin(a + math.pi / 2), 0)))
        trees.cyl(P["wood_dark"], (x, y, 0.35), 0.38, length, n=6, r2=0.22, tilt=tilt, rotz=0.0, jitter=0.04)
        walls.box(P["wood_dark"], (x + math.cos(a) * length * 0.5, y + math.sin(a) * length * 0.5, 0.35), (length, 0.7, 0.7), rotz=a)
    # 骨: あばら骨の山と、頭の骨
    for k in range(9):
        a = wrng.random() * math.tau
        r = wrng.uniform(4.0, FOREST_RADIUS - 3.0)
        x, y = FOREST_CENTER.x + math.cos(a) * r, FOREST_CENTER.y + math.sin(a) * r
        rot = wrng.random() * math.tau
        for rib in range(wrng.randint(3, 5)):
            off = (rib - 2) * 0.32
            decor.box(P["bone"], (x + math.cos(rot) * off, y + math.sin(rot) * off, 0.07), (0.09, wrng.uniform(0.9, 1.4), 0.08), rotz=rot + wrng.uniform(-0.25, 0.25))
        if wrng.random() < 0.6:
            decor.ico(P["bone"], (x + math.cos(rot + 1.5) * 0.9, y + math.sin(rot + 1.5) * 0.9, 0.18), 0.24, sub=0, scale=(1.2, 0.9, 0.8), jitter=0.03, rotz=wrng.random() * 6)
    for k in range(12):
        a = wrng.random() * math.tau
        r = wrng.uniform(6.0, FOREST_RADIUS - 1.5)
        x, y = FOREST_CENTER.x + math.cos(a) * r, FOREST_CENTER.y + math.sin(a) * r
        s = wrng.uniform(0.3, 0.8)
        decor.ico(P["stone_dark"], (x, y, -0.05), s, sub=0, scale=(1, 1, 0.6), jitter=s * 0.25, rotz=wrng.random() * 6)
        decor.ico(P["moss"], (x, y, s * 0.3), s * 0.75, sub=0, scale=(1, 1, 0.35), jitter=s * 0.1)

    # Low hills just outside the map so the edge is not a hard line
    for _ in range(110):
        side = rng.randint(0, 3)
        if side < 2:
            p = Vector((rng.uniform(-300, 300), (HALF + 18 if side == 0 else -HALF - 18) + rng.uniform(-3, 12) * (1 if side == 0 else -1)))
        else:
            p = Vector(((HALF + 18 if side == 2 else -HALF - 18) + rng.uniform(-3, 12) * (1 if side == 2 else -1), rng.uniform(-300, 300)))
        if is_sea(p) or shore_dist(p) < 30:
            continue
        r = rng.uniform(12, 26)
        leaf = rng.choice([P["grass_a"], P["grass_c"], P["leaf_pine"]])
        decor.ico(leaf, (p.x, p.y, -r * 0.45), r, sub=1, scale=(1.3, 1.0, 0.6), jitter=r * 0.08, rotz=rng.random() * 6)

    # Rocks on the existing box colliders
    for rx, rz, sx, sy, sz in ROCKS:
        walls.ico(P["stone"], (rx, rz, sy * 0.38), 0.5, sub=1, scale=(sx * 1.05, sz * 1.05, sy * 1.0),
                  jitter=0.25, rotz=rng.random() * 6)
        walls.ico(P["stone_dark"], (rx + sx * 0.35, rz - sz * 0.3, 0.2), 0.5, sub=1,
                  scale=(sx * 0.5, sz * 0.45, sy * 0.45), jitter=0.15, rotz=rng.random() * 6)
        walls.ico(P["moss"], (rx - sx * 0.1, rz + sz * 0.1, sy * 0.78), 0.5, sub=0,
                  scale=(sx * 0.6, sz * 0.55, sy * 0.25), jitter=0.1)

    # 地面の上の物を、置いた場所の凹凸の高さだけ上げ下げする（形はそのまま、頂点ごとにその場の高さを足す）。
    for g in (decor, trees, walls, tree_cols):
        for v in g.bm.verts:
            v.co.z += ground_lift(v.co.x, v.co.y)
    scratch.bm.free()

    ground.force_up = True
    water.force_up = True
    mountains.force_up = True
    for g in (ground, decor, trees, walls, water, mountains, bridges, bridge_ropes, barrier, tree_cols, mountain_fill):
        g.build(root, col)
    add_markers(col, root, 100.0, 60.0)
    return col, root


# ================================================================ START TOWN
def house(g, rng, x, y, face_angle, scale=1.0):
    rot = face_angle + math.pi / 2
    w, d, h = rng.uniform(3.2, 4.2) * scale, rng.uniform(2.8, 3.4) * scale, rng.uniform(2.4, 3.0) * scale
    g.box(P["plaster"], (x, y, h / 2), (w, d, h), rotz=rot)
    # timber frame
    for sx in (-0.5, 0.5):
        off = Matrix.Rotation(rot, 3, "Z") @ Vector((sx * w, 0, 0))
        g.box(P["wood"], (x + off.x, y + off.y, h / 2), (0.18, d + 0.06, h), rotz=rot)
    g.box(P["wood"], (x, y, h - 0.05), (w + 0.06, d + 0.06, 0.15), rotz=rot)
    roof_m = rng.choice([P["roof_r"], P["roof_b"], P["roof_o"]])
    g.roof(roof_m, (x, y, h), w + 0.6, d + 0.8, rng.uniform(1.4, 1.9) * scale, rotz=rot)
    # door + windows on the side facing the plaza (local -y)
    face = Matrix.Rotation(rot, 3, "Z")
    dpos = face @ Vector((0, -d / 2 - 0.03, 0))
    g.box(P["wood_dark"], (x + dpos.x, y + dpos.y, 0.85), (0.9, 0.12, 1.7), rotz=rot)
    for sx in (-0.3, 0.3):
        wp = face @ Vector((sx * w, -d / 2 - 0.03, 0))
        g.box(P["window"], (x + wp.x, y + wp.y, 1.7), (0.6, 0.1, 0.6), rotz=rot)
    if rng.random() < 0.6:
        cp = face @ Vector((w * 0.28, d * 0.15, 0))
        g.box(P["stone"], (x + cp.x, y + cp.y, h + 1.2), (0.45, 0.45, 1.6), rotz=rot)


def build_town():
    rng = random.Random(21)
    col, root = new_model("LP_StartTown")
    g = Group("Town_Buildings", rng)
    gr = Group("Town_Ground", rng)

    # Plaza
    gr.cyl(P["cobble"], (0, 0, -0.08), 8.0, 0.14, n=14)
    gr.cyl(P["stone"], (0, 0, -0.07), 8.4, 0.12, n=14)
    # path stub to the east gate
    gr.box(P["cobble"], (12.5, 0, -0.02), (9.0, 4.2, 0.1))

    # Fountain = StartTown core (core collider sits at 0.75 .. 2.25)
    g.cyl(P["stone"], (0, 0, 0), 2.3, 0.7, n=8)
    g.cyl(P["water"], (0, 0, 0.55), 2.0, 0.2, n=8)
    g.cyl(P["stone"], (0, 0, 0.6), 0.45, 1.3, n=6)
    g.cyl(P["stone"], (0, 0, 1.75), 1.0, 0.25, n=8, r2=1.2)
    g.cyl(P["water"], (0, 0, 1.92), 0.95, 0.12, n=8)
    g.ico(P["crystal_town"], (0, 0, 2.9), 0.55, sub=0, scale=(0.8, 0.8, 1.6))

    # Lamp posts on the old pillar colliders (+-3.5, +-3.5)
    for sx in (-3.5, 3.5):
        for sy in (-3.5, 3.5):
            g.cyl(P["wood_dark"], (sx, sy, 0), 0.13, 3.0, n=6)
            g.box(P["wood_dark"], (sx, sy, 3.05), (0.5, 0.5, 0.1))
            g.box(P["lamp"], (sx, sy, 3.3), (0.36, 0.36, 0.42))
            g.cone(P["roof_r"], (sx, sy, 3.5), 0.38, 0.4, n=4, rotz=math.pi / 4)

    # Houses on a ring, leaving the east side (+x, toward the castle) open
    for deg in (55, 95, 135, 180, 225, 265, 305):
        a = math.radians(deg + rng.uniform(-6, 6))
        r = rng.uniform(11.0, 12.5)
        house(g, rng, math.cos(a) * r, math.sin(a) * r, a + math.pi, rng.uniform(0.9, 1.1))

    # Well, barrels, crates
    g.cyl(P["stone"], (6.5, 7.5, 0), 0.9, 0.8, n=8)
    g.cyl(P["water"], (6.5, 7.5, 0.6), 0.75, 0.1, n=8)
    for sx in (-0.7, 0.7):
        g.box(P["wood"], (6.5 + sx, 7.5, 1.3), (0.12, 0.12, 1.4))
    g.roof(P["roof_r"], (6.5, 7.5, 1.95), 1.9, 1.5, 0.6)
    for (bx, by) in [(-6.5, 8.0), (-7.2, 7.3), (-9.3, -5.0), (5.8, -8.0)]:
        g.cyl(P["wood"], (bx, by, 0), 0.4, 0.9, n=8, r2=0.36)
    for (bx, by) in [(6.4, -8.6), (-8.5, -5.8)]:
        g.box(P["wood"], (bx, by, 0.35), (0.7, 0.7, 0.7), rotz=rng.random())

    # Fence ring with the east gate open
    r = 16.0
    deg = 22
    prev = None
    while deg <= 338:
        a = math.radians(deg)
        p = (math.cos(a) * r, math.sin(a) * r)
        g.box(P["wood"], (p[0], p[1], 0.55), (0.2, 0.2, 1.1))
        if prev is not None:
            mx, my = (p[0] + prev[0]) / 2, (p[1] + prev[1]) / 2
            ln = math.dist(p, prev)
            ang = math.atan2(p[1] - prev[1], p[0] - prev[0])
            for hz in (0.45, 0.85):
                g.box(P["wood"], (mx, my, hz), (ln, 0.08, 0.12), rotz=ang)
        prev = p
        deg += 7
    # Gate posts with blue banners
    for sy in (-1, 1):
        a = math.radians(22 * sy)
        px, py = math.cos(a) * r, math.sin(a) * r
        g.cyl(P["wood_dark"], (px, py, 0), 0.2, 4.5, n=6)
        g.box(P["flag_blue"], (px + 0.05, py - 0.5 * sy, 3.3), (0.06, 0.9, 1.8))
        g.cone(P["flower_y"], (px, py, 4.5), 0.25, 0.4, n=6)

    # A few trees inside the fence
    for deg in (75, 160, 200, 285, 325):
        a = math.radians(deg)
        tree(g, rng, math.cos(a) * 14.2, math.sin(a) * 14.2, 0.8, kind="round")

    g.build(root, col)
    gr.build(root, col)
    add_markers(col, root, 10.0, 10.0)
    return col, root


# ================================================================ DEMON CASTLE
def build_castle():
    rng = random.Random(31)
    col, root = new_model("LP_DemonCastle")
    g = Group("Castle_Buildings", rng)
    gr = Group("Castle_Ground", rng)
    fx = Group("Castle_Glow", rng)

    half = 14.0
    th = 1.6
    wh = 5.0
    gate_half = 4.0

    gr.box(P["castle_trim"], (0, 0, -0.04), (2 * half + 2, 2 * half + 2, 0.1))
    gr.cyl(P["obsidian"], (0, 0, -0.02), 6.0, 0.08, n=8)

    def wall(x0, y0, x1, y1):
        ln = math.dist((x0, y0), (x1, y1))
        if ln < 0.1:
            return
        ang = math.atan2(y1 - y0, x1 - x0)
        mx, my = (x0 + x1) / 2, (y0 + y1) / 2
        g.box(P["castle"], (mx, my, wh / 2), (ln, th, wh), rotz=ang)
        g.box(P["castle_trim"], (mx, my, wh - 0.1), (ln, th + 0.3, 0.35), rotz=ang)
        n = int(ln / 1.4)
        for i in range(n):
            t = (i + 0.5) / n
            cx, cy = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            g.box(P["castle"], (cx, cy, wh + 0.45), (0.75, th + 0.2, 0.9), rotz=ang)

    wall(-half, half, half, half)
    wall(-half, -half, half, -half)
    wall(half, -half, half, half)
    wall(-half, half, -half, gate_half + 1.5)
    wall(-half, -half, -half, -gate_half - 1.5)

    def tower(x, y, r, h, roof_h):
        g.cyl(P["castle"], (x, y, 0), r, h, n=8, r2=r * 0.92)
        g.cyl(P["castle_trim"], (x, y, h), r * 1.15, 0.6, n=8)
        for i in range(8):
            a = i / 8 * math.tau
            g.box(P["castle"], (x + math.cos(a) * r, y + math.sin(a) * r, h + 0.95), (0.6, 0.6, 0.7), rotz=a)
        g.cone(P["roof_purple"], (x, y, h + 0.6), r * 1.2, roof_h, n=8)
        g.box(P["crystal_dk"], (x - r - 0.02, y, h * 0.65), (0.1, 0.5, 0.9))

    for sx in (-1, 1):
        for sy in (-1, 1):
            tower(sx * half, sy * half, 2.4, 9.0, 4.0)
    for sy in (-1, 1):
        tower(-half, sy * (gate_half + 1.5), 1.7, 7.0, 3.0)
        g.box(P["flag_red"], (-half - 1.8, sy * (gate_half + 1.5), 4.6), (0.06, 1.1, 2.6))
    # Gate arch
    g.box(P["castle_trim"], (-half, 0, 6.4), (th + 0.4, 2 * gate_half + 3.0, 1.2))
    g.box(P["crystal_dk"], (-half - 0.85, 0, 6.4), (0.1, 1.4, 0.6))

    # Keep behind the core (+x), leaving the core area free
    g.box(P["castle"], (9.0, 0, 5.5), (6.0, 10.0, 11.0))
    g.box(P["castle_trim"], (9.0, 0, 11.1), (6.6, 10.6, 0.4))
    g.roof(P["roof_purple"], (9.0, 0, 11.3), 6.6, 10.6, 3.5, rotz=math.pi / 2)
    for sy in (-1, 1):
        g.cyl(P["castle"], (9.0, sy * 5.2, 0), 1.4, 15.0, n=8)
        g.cone(P["roof_purple"], (9.0, sy * 5.2, 15.0), 1.8, 5.0, n=8)
        g.ico(P["crystal_dk"], (9.0, sy * 5.2, 20.6), 0.45, sub=0, scale=(1, 1, 1.8))
    g.box(P["lava"], (5.95, 0, 7.5), (0.1, 1.6, 2.4))
    g.box(P["lava"], (5.95, -2.8, 7.5), (0.1, 0.8, 1.4))
    g.box(P["lava"], (5.95, 2.8, 7.5), (0.1, 0.8, 1.4))
    g.box(P["wood_dark"], (5.9, 0, 1.6), (0.2, 3.0, 3.2))

    # Core altar (core collider sits at 0.75 .. 2.25) with a floating crystal
    g.cyl(P["obsidian"], (0, 0, 0), 2.7, 0.4, n=8)
    g.cyl(P["castle_trim"], (0, 0, 0.4), 2.1, 0.35, n=8)
    g.cyl(P["obsidian"], (0, 0, 0.75), 1.2, 0.5, n=8, r2=0.9)
    g.ico(P["crystal_dk"], (0, 0, 2.3), 0.9, sub=0, scale=(0.75, 0.75, 1.7), rotz=0.4)
    for i in range(4):
        a = i / 4 * math.tau + math.pi / 4
        g.ico(P["crystal_dk"], (math.cos(a) * 1.9, math.sin(a) * 1.9, 1.3), 0.25, sub=0, scale=(1, 1, 2))

    # Obsidian spikes on the old pillar colliders (+-3.5, +-3.5)
    for sx in (-3.5, 3.5):
        for sy in (-3.5, 3.5):
            g.cone(P["obsidian"], (sx, sy, 0), 0.55, 4.2, n=5, rotz=rng.random())
            g.cone(P["crystal_dk"], (sx, sy, 3.0), 0.22, 1.3, n=5)

    # Lava cracks in the courtyard and outside
    for _ in range(14):
        a = rng.random() * math.tau
        r = rng.uniform(5.5, 12.5)
        x, y = math.cos(a) * r, math.sin(a) * r
        if x < -10 and abs(y) < gate_half + 1:
            continue
        if 5 < x < 12.5 and abs(y) < 6.5:
            continue
        fx.box(P["lava"], (x, y, 0.0), (rng.uniform(1.0, 2.6), rng.uniform(0.15, 0.35), 0.06), rotz=rng.random() * 3)

    # Spikes and bones around the outer walls
    for _ in range(40):
        side = rng.randint(0, 3)
        t = rng.uniform(-half - 2, half + 2)
        off = half + rng.uniform(2.0, 5.0)
        x, y = [(t, off), (t, -off), (off, t), (-off, t)][side]
        if x < 0 and abs(y) < 8 and side == 3:
            continue
        tilt = Matrix.Rotation(math.radians(rng.uniform(-20, 20)), 4, Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0)).normalized())
        g.cone(P["obsidian"], (x, y, -0.1), rng.uniform(0.25, 0.45), rng.uniform(1.2, 2.6), n=4, tilt=tilt)
    for _ in range(10):
        a = rng.random() * math.tau
        r = rng.uniform(17, 21)
        x, y = math.cos(a) * r, math.sin(a) * r
        if x < 0 and abs(y) < 8:
            continue
        fx.box(P["bone"], (x, y, 0.08), (0.9, 0.12, 0.12), rotz=rng.random() * 3)
        fx.ico(P["bone"], (x + 0.5, y, 0.18), 0.2, sub=0)

    g.build(root, col)
    gr.build(root, col)
    fx.build(root, col)
    add_markers(col, root, 10.0, 10.0)
    return col, root


# ================================================================ ARCHER TOWER
def build_archer_tower():
    """Stands at the town gate. Tower_Archer turns toward its target (origin on the tower axis, faces +x).
    Tower_Arrow is the template each shot clones (points along +x)."""
    rng = random.Random(41)
    col, root = new_model("LP_ArcherTower")
    g = Group("Tower_Body", rng)

    floor = 6.2
    g.cyl(P["stone_dark"], (0, 0, 0), 2.0, 0.7, n=8, r2=1.7)
    g.cyl(P["stone"], (0, 0, 0.7), 1.65, floor - 0.7, n=8, r2=1.35, jitter=0.02)
    for z in (2.4, 4.2):
        g.cyl(P["stone_dark"], (0, 0, z), 1.62 - z * 0.05, 0.16, n=8)
    g.box(P["wood_dark"], (1.6, 0, 1.1), (0.14, 1.0, 2.2))
    # Platform with a low wall
    g.cyl(P["wood"], (0, 0, floor - 0.25), 2.1, 0.25, n=8)
    for i in range(8):
        a = i * math.pi / 4 + math.pi / 8
        g.box(P["stone"], (math.cos(a) * 1.95, math.sin(a) * 1.95, floor + 0.3), (0.5, 0.5, 0.65), rotz=a)
    # Roof on four posts
    for sx in (-1, 1):
        for sy in (-1, 1):
            g.cyl(P["wood_dark"], (sx * 1.35, sy * 1.35, floor), 0.1, 2.9, n=6)
    g.cone(P["roof_b"], (0, 0, floor + 2.9), 2.7, 1.9, n=8)
    g.cone(P["flower_y"], (0, 0, floor + 4.75), 0.14, 0.4, n=6)
    # Banners
    for sy in (-1, 1):
        g.box(P["flag_blue"], (0, sy * 1.5, 4.9), (0.9, 0.06, 1.6))
    g.build(root, col)

    a = Group("Tower_Archer", rng)
    a.box(P["wood_dark"], (0, -0.14, 0.3), (0.2, 0.2, 0.6))
    a.box(P["wood_dark"], (0, 0.14, 0.3), (0.2, 0.2, 0.6))
    a.cyl(P["flag_blue"], (0, 0, 0.55), 0.34, 0.75, n=8, r2=0.26)
    a.ico(P["plaster"], (0, 0, 1.55), 0.27, sub=1)
    a.cone(P["roof_b"], (-0.02, 0, 1.58), 0.34, 0.5, n=8)
    a.box(P["plaster"], (0.32, 0.18, 1.12), (0.65, 0.12, 0.12))
    a.box(P["plaster"], (0.32, -0.18, 1.12), (0.65, 0.12, 0.12))
    # Bow: a shallow arc of short sticks in the x-z plane, then the string.
    points = [(0.62 + 0.26 * math.cos(t), 1.12 + 0.72 * math.sin(t)) for t in [math.radians(d) for d in range(-90, 91, 30)]]
    for (x0, z0), (x1, z1) in zip(points, points[1:]):
        a.box(P["wood_dark"], ((x0 + x1) / 2, 0, (z0 + z1) / 2), (max(abs(x1 - x0), 0.07), 0.07, max(abs(z1 - z0), 0.07)))
    a.box(P["flower_w"], (0.62, 0, 1.12), (0.02, 0.02, 1.44))
    archer = a.build(root, col)
    archer.location = (0, 0, floor)

    r = Group("Tower_Arrow", rng)
    r.box(P["wood"], (0, 0, 0), (1.5, 0.07, 0.07))
    r.box(P["stone_dark"], (0.8, 0, 0), (0.16, 0.13, 0.13))
    r.box(P["stone_dark"], (0.92, 0, 0), (0.1, 0.06, 0.06))
    r.box(P["flower_w"], (-0.66, 0, 0), (0.24, 0.24, 0.02))
    r.box(P["flower_w"], (-0.66, 0, 0), (0.24, 0.02, 0.24))
    r.build(root, col)

    add_markers(col, root, 10.0, 10.0)
    return col, root


# ================================================================ main
# ---------------------------------------------------------------- sand mask
# 砂漠の「道から外れた砂地」を2m四方のマスで書き出す。Unity 側（StageMap.IsLooseSand）が読み、歩くと遅くなって砂が跳ねる。
# 地面の色分け（face_mat）と同じ判定を使うので、見た目の砂地とずれない。
SAND_CELL = 2.0


def write_ground_heights(heights, x0, z0, cell, n):
    """Unity の当たり判定用に地面の高さを書き出す。先頭に x0, z0, cell (float32), n (int32)。続けて z の行ごとに
    (n+1) 個の高さ (float32)。セル (i, j) は (i + j) が偶数なら左下と右上、奇数なら右下と左上を結んで三角形に分ける。"""
    import struct
    out = bytearray(struct.pack("<fffi", x0, z0, cell, n))
    for j in range(n + 1):
        out += struct.pack("<%df" % (n + 1), *[heights[i][j] for i in range(n + 1)])
    with open(os.path.join(ROOT, "Assets", "_Project", "Resources", "StageGround.bytes"), "wb") as f:
        f.write(out)
    print("GROUND_HEIGHTS", n + 1, "x", n + 1)


def is_loose_sand(p):
    if not in_desert(p) or is_sea(p) or deep_gap(p) < 3.5 or shore_dist(p) < 5.0 or shallow_gap(p) < 1.5:
        return False
    return not on_path(p.x, p.y, 0.8) and not near_spawner(p, 7.0) and (p - TOWN).length >= 9 and not in_scorpion_plaza(p, 1.0)


def write_sand_mask():
    b = BIOMES["desert"]
    x0, z0 = b["x"] - b["rx"] * 1.25, b["z"] - b["rz"] * 1.25
    cols, rows = int(b["rx"] * 2.5 / SAND_CELL) + 1, int(b["rz"] * 2.5 / SAND_CELL) + 1
    lines, count = [], 0
    for j in range(rows):
        bits = 0
        for i in range(cols):
            if is_loose_sand(Vector((x0 + (i + 0.5) * SAND_CELL, z0 + (j + 0.5) * SAND_CELL))):
                bits |= 1 << i
                count += 1
        lines.append(format(bits, "x"))
    out = {"x0": x0, "z0": z0, "cell": SAND_CELL, "cols": cols, "rows": rows, "bits": lines}
    with open(os.path.join(ROOT, "Assets", "_Project", "Resources", "StageMap_Sand.json"), "w", encoding="utf-8") as f:
        json.dump(out, f, separators=(",", ":"))
    print("SAND_MASK", cols, "x", rows, "cells", count)


if ONLY is None or ONLY == "LP_Field.fbx":
    write_sand_mask()

models = [(build_field(), "LP_Field.fbx"), (build_town(), "LP_StartTown.fbx"), (build_castle(), "LP_DemonCastle.fbx"), (build_archer_tower(), "LP_ArcherTower.fbx")]
for (col, root), fn in models:
    if ONLY is None or fn == ONLY:
        export(col, root, fn)
if ONLY is not None:
    print("LOWPOLY_DONE")
    sys.exit(0)

# Lay the models out like the stage for the .blend preview (FBX export above used local origins).
for (col, root), fn in models:
    if root.name == "LP_StartTown":
        root.location = (TOWN.x, TOWN.y, 0)
    elif root.name == "LP_DemonCastle":
        root.location = (CASTLE.x, CASTLE.y, 0)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT_SRC, "lowpoly_environment.blend"))
print("LOWPOLY_DONE")
