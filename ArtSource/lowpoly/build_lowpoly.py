# Builds the placeholder low-poly environment (StartTown / DemonCastle / meadow field) and exports FBX for Unity.
# Run headless:
#   /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup -P ArtSource/lowpoly/build_lowpoly.py -- "<repo root>"
# Coordinates: Blender (x, y, z) = Unity (x, z, y) before import. Each model has Marker_PlusX / Marker_PlusZ
# empties so the Unity placer can fix any axis flip from the FBX conversion.
import bpy
import bmesh
import math
import os
import random
import sys
from mathutils import Matrix, Vector, noise

ROOT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else os.getcwd()
OUT_FBX = os.path.join(ROOT, "Assets", "_Project", "Art", "LowPoly")
OUT_SRC = os.path.join(ROOT, "ArtSource", "lowpoly")
os.makedirs(OUT_FBX, exist_ok=True)

TOWN = Vector((-80.0, 0.0))
CASTLE = Vector((80.0, 0.0))
SPAWNERS = [Vector(p) for p in [(-40, 40), (-40, -40), (0, 0), (40, 40), (40, -40)]]
# Scene rocks (Unity x, z, size x, size y, size z) and lane walls; visuals sit on the existing colliders.
ROCKS = [(-95, 55, 8, 5, 8), (95, 55, 8, 5, 8), (-95, -55, 8, 5, 8), (95, -55, 8, 5, 8),
         (-60, 55, 6, 4, 5), (60, 55, 6, 4, 5), (-60, -55, 6, 4, 5), (60, -55, 6, 4, 5),
         (-20, 20, 4, 3, 4), (25, -15, 3.5, 3, 4.5)]
WALLS_Y = (20.0, -20.0)
WALL_HALF_LEN = 40.0

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


def path_y(x):
    return 1.5 * math.sin(x * 0.045)


def on_path(x, y, margin=0.0):
    return -73 < x < 70 and abs(y - path_y(x)) < 2.4 + margin


def near_spawner(p, r):
    return any((p - s).length < r for s in SPAWNERS)


def tree(g, rng, x, y, s=1.0, kind=None):
    kind = kind or rng.choice(["round", "round", "pine"])
    h = rng.uniform(2.2, 3.0) * s
    g.cyl(P["bark"], (x, y, -0.1), 0.22 * s, h, n=6, r2=0.15 * s)
    if kind == "pine":
        for i, (r, z) in enumerate([(1.6, 0.55), (1.25, 1.35), (0.85, 2.1)]):
            g.cone(P["leaf_pine"], (x, y, h * 0.55 + z * s), r * s, 1.6 * s, n=7, rotz=rng.random() * 6, jitter=0.08 * s)
    else:
        leaf = P["leaf_a"] if rng.random() < 0.5 else P["leaf_b"]
        g.ico(leaf, (x, y, h + 0.6 * s), 1.4 * s, sub=1, scale=(1, 1, 0.9), jitter=0.18 * s, rotz=rng.random() * 6)
        g.ico(leaf, (x + 0.7 * s, y + 0.3 * s, h + 0.1 * s), 0.9 * s, sub=1, jitter=0.12 * s)


def dead_tree(g, rng, x, y, s=1.0):
    h = rng.uniform(2.5, 3.5) * s
    g.cyl(P["wood_dark"], (x, y, -0.1), 0.2 * s, h, n=5, r2=0.08 * s, jitter=0.03)
    for _ in range(3):
        a = rng.random() * math.tau
        tilt = Matrix.Rotation(math.radians(rng.uniform(40, 65)), 4, Vector((math.cos(a), math.sin(a), 0)))
        g.cyl(P["wood_dark"], (x, y, h * rng.uniform(0.5, 0.85)), 0.08 * s, 1.2 * s, n=4, r2=0.02, tilt=tilt)


# ================================================================ FIELD
def build_field():
    rng = random.Random(11)
    col, root = new_model("LP_Field")
    ground = Group("Field_Ground", rng)
    decor = Group("Field_Decor", rng)
    trees = Group("Field_Trees", rng)
    walls = Group("Field_Walls", rng)

    # Ground: 200 x 120 triangulated grid, flat shaded, material picked per triangle.
    nx, ny, w, d = 100, 60, 200.0, 120.0
    bm = ground.bm
    grid = [[bm.verts.new((-w / 2 + i * w / nx, -d / 2 + j * d / ny, 0.0)) for j in range(ny + 1)] for i in range(nx + 1)]
    for col_v in grid:
        for v in col_v:
            x, y = v.co.x, v.co.y
            h = ground_height(x, y, rng)
            if on_path(x, y, 0.5):
                h -= 0.03
            v.co.z = h

    def face_mat(cx, cy):
        p = Vector((cx, cy))
        dc = (p - CASTLE).length
        if dc < 18:
            return P["ash"]
        if dc < 30:
            return P["ash"] if rng.random() < (30 - dc) / 14 else P["scorched"]
        if on_path(cx, cy):
            return P["dirt"]
        if near_spawner(p, 4.5):
            return P["dirt_dark"]
        if (p - TOWN).length < 9:
            return P["grass_b"]
        n = noise.noise(Vector((cx * 0.035, cy * 0.035, 1.7))) + rng.uniform(-0.25, 0.25)
        if n > 0.45:
            return P["grass_dry"]
        if n > 0.1:
            return P["grass_b"]
        if n < -0.35:
            return P["grass_c"]
        return P["grass_a"]

    for i in range(nx):
        for j in range(ny):
            a, b, c, e = grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]
            tris = [(a, b, c), (a, c, e)] if (i + j) % 2 == 0 else [(a, b, e), (b, c, e)]
            for t in tris:
                f = bm.faces.new(t)
                cx = sum(v.co.x for v in t) / 3
                cy = sum(v.co.y for v in t) / 3
                f.material_index = ground._mi(face_mat(cx, cy))

    def blocked(p, extra=0.0):
        if (p - TOWN).length < 17 + extra or (p - CASTLE).length < 23 + extra:
            return True
        if on_path(p.x, p.y, 0.8 + extra) or near_spawner(p, 6 + extra):
            return True
        if abs(p.x) < WALL_HALF_LEN + 1.5 and any(abs(p.y - wy) < 1.8 + extra for wy in WALLS_Y):
            return True
        for rx, rz, sx, sy, sz in ROCKS:
            if abs(p.x - rx) < sx / 2 + 1.5 + extra and abs(p.y - rz) < sz / 2 + 1.5 + extra:
                return True
        return False

    def rand_point(margin=2.0):
        return Vector((rng.uniform(-w / 2 + margin, w / 2 - margin), rng.uniform(-d / 2 + margin, d / 2 - margin)))

    # Grass tufts
    placed = 0
    while placed < 2400:
        p = rand_point()
        if blocked(p):
            continue
        placed += 1
        z = ground_height(p.x, p.y, rng) - 0.05
        for _ in range(3):
            ox, oy = rng.uniform(-0.25, 0.25), rng.uniform(-0.25, 0.25)
            tilt = Matrix.Rotation(math.radians(rng.uniform(-18, 18)), 4, Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0)).normalized())
            decor.cone(P["tuft"], (p.x + ox, p.y + oy, z), 0.09, rng.uniform(0.35, 0.65), n=3, rotz=rng.random() * 6, tilt=tilt)

    # Flowers, in loose clusters
    flower_mats = [P["flower_w"], P["flower_y"], P["flower_p"], P["flower_b"]]
    for _ in range(70):
        c = rand_point()
        if blocked(c):
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
    for _ in range(90):
        p = rand_point()
        if blocked(p):
            continue
        s = rng.uniform(0.2, 0.55)
        decor.ico(P["stone"], (p.x, p.y, -0.05), s, sub=0, scale=(1, 1, 0.6), jitter=s * 0.25, rotz=rng.random() * 6)
    for _ in range(45):
        p = rand_point()
        if blocked(p, 1.0):
            continue
        s = rng.uniform(0.6, 1.0)
        leaf = P["leaf_a"] if rng.random() < 0.5 else P["leaf_b"]
        decor.ico(leaf, (p.x, p.y, 0.25 * s), 0.9 * s, sub=1, scale=(1.2, 1, 0.75), jitter=0.12 * s)

    # Trees: map border bands and a few groves away from the lanes
    def place_trees(count, xr, yr, scale_r=(0.9, 1.3)):
        n = 0
        tries = 0
        while n < count and tries < count * 30:
            tries += 1
            p = Vector((rng.uniform(*xr), rng.uniform(*yr)))
            if blocked(p, 2.0):
                continue
            tree(trees, rng, p.x, p.y, rng.uniform(*scale_r))
            n += 1

    place_trees(22, (-96, 96), (47, 58))
    place_trees(22, (-96, 96), (-58, -47))
    place_trees(6, (-96, -88), (-40, 40))
    place_trees(4, (88, 97), (-40, 40), (0.8, 1.0))
    place_trees(5, (-70, -50), (24, 42))
    place_trees(5, (-70, -50), (-42, -24))
    place_trees(4, (10, 30), (24, 36))
    place_trees(4, (10, 30), (-36, -24))
    for _ in range(10):
        a = rng.random() * math.tau
        r = rng.uniform(24, 32)
        p = CASTLE + Vector((math.cos(a) * r, math.sin(a) * r))
        if abs(p.y) < 4 or abs(p.x) > 98 or abs(p.y) > 58:
            continue
        dead_tree(trees, rng, p.x, p.y, rng.uniform(0.9, 1.2))

    # Low hills just outside the play area so the edge is not a hard line
    for _ in range(46):
        side = rng.randint(0, 3)
        if side < 2:
            p = Vector((rng.uniform(-120, 120), (66 if side == 0 else -66) + rng.uniform(-3, 6) * (1 if side == 0 else -1)))
        else:
            p = Vector(((108 if side == 2 else -108) + rng.uniform(-3, 6) * (1 if side == 2 else -1), rng.uniform(-70, 70)))
        r = rng.uniform(8, 16)
        leaf = rng.choice([P["grass_a"], P["grass_c"], P["leaf_pine"]])
        decor.ico(leaf, (p.x, p.y, -r * 0.45), r, sub=1, scale=(1.3, 1.0, 0.6), jitter=r * 0.08, rotz=rng.random() * 6)

    # Lane walls -> field-stone walls on the existing 80 x 4 x 1 colliders
    for wy in WALLS_Y:
        x = -WALL_HALF_LEN
        while x < WALL_HALF_LEN - 0.1:
            seg = min(rng.uniform(1.8, 2.8), WALL_HALF_LEN - x)
            cx = x + seg / 2
            walls.box(P["stone"], (cx, wy, 0.95), (seg - 0.05, 1.25, 1.9), jitter=0.07)
            walls.box(P["stone_dark"] if rng.random() < 0.3 else P["stone"],
                      (cx + rng.uniform(-0.3, 0.3), wy, 2.85), (seg * 0.95, 1.15, 1.9), jitter=0.07)
            walls.box(P["moss"], (cx, wy, 3.9), (seg * 0.98, 1.2, 0.25), jitter=0.04)
            x += seg
        for ex in (-WALL_HALF_LEN, WALL_HALF_LEN):
            walls.cyl(P["stone_dark"], (ex, wy, 0), 0.95, 4.5, n=7, jitter=0.05)
            walls.cone(P["moss"], (ex, wy, 4.5), 1.0, 0.5, n=7)

    # Rocks on the existing box colliders
    for rx, rz, sx, sy, sz in ROCKS:
        walls.ico(P["stone"], (rx, rz, sy * 0.38), 0.5, sub=1, scale=(sx * 1.05, sz * 1.05, sy * 1.0),
                  jitter=0.25, rotz=rng.random() * 6)
        walls.ico(P["stone_dark"], (rx + sx * 0.35, rz - sz * 0.3, 0.2), 0.5, sub=1,
                  scale=(sx * 0.5, sz * 0.45, sy * 0.45), jitter=0.15, rotz=rng.random() * 6)
        walls.ico(P["moss"], (rx - sx * 0.1, rz + sz * 0.1, sy * 0.78), 0.5, sub=0,
                  scale=(sx * 0.6, sz * 0.55, sy * 0.25), jitter=0.1)

    for g in (ground, decor, trees, walls):
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


# ================================================================ main
models = [(build_field(), "LP_Field.fbx"), (build_town(), "LP_StartTown.fbx"), (build_castle(), "LP_DemonCastle.fbx")]
for (col, root), fn in models:
    export(col, root, fn)

# Lay the models out like the stage for the .blend preview (FBX export above used local origins).
for (col, root), fn in models:
    if root.name == "LP_StartTown":
        root.location = (TOWN.x, TOWN.y, 0)
    elif root.name == "LP_DemonCastle":
        root.location = (CASTLE.x, CASTLE.y, 0)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT_SRC, "lowpoly_environment.blend"))
print("LOWPOLY_DONE")
