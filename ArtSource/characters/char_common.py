# Shared builder for the low-poly chibi characters (Priest, Mage, Warrior, Demon King).
# Every character reuses the Knight's armature from knight.fbx, so bone names / rest pose match and the Knight's
# animation clips can be shared. Coordinates are Blender world space (front = -Y, up = +Z), T-pose, height ~2.8.
# A character script does `from char_common import *`, builds its parts with Body / HeadKit, then calls export().
import bpy
import bmesh
import math
import os
import random
import sys
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

ROOT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else os.getcwd()
KNIGHT_FBX = os.path.join(ROOT, "Assets", "_Project", "Resources", "Knight", "knight.fbx")

bpy.ops.wm.read_factory_settings(use_empty=True)

# ---------------------------------------------------------------- skeleton from the Knight
bpy.ops.import_scene.fbx(filepath=KNIGHT_FBX)
ARM = bpy.data.objects["Armature"]
for o in list(bpy.data.objects):
    if o.type == "MESH":
        bpy.data.objects.remove(o, do_unlink=True)
if ARM.animation_data:
    ARM.animation_data_clear()
for a in list(bpy.data.actions):
    bpy.data.actions.remove(a)
for m in list(bpy.data.materials):
    bpy.data.materials.remove(m)
for img in list(bpy.data.images):
    bpy.data.images.remove(img)
for pb in ARM.pose.bones:
    pb.matrix_basis.identity()
BONES = [b.name for b in ARM.data.bones]


def bone_pos(name, tail=False):
    b = ARM.data.bones[name]
    return ARM.matrix_world @ (b.tail_local if tail else b.head_local)



# ---------------------------------------------------------------- materials
MATS = {}
MAT_PREFIX = [""]


def srgb(hexstr):
    c = [int(hexstr[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    return tuple(v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4 for v in c)


def mat(name, hexstr, rough=0.95, metal=0.0, spec=0.05):
    """Matte cloth by default; gold gets a little metal and gloss, skin a soft sheen."""
    if name in MATS:
        return MATS[name]
    rgb = srgb(hexstr)
    m = bpy.data.materials.new(MAT_PREFIX[0] + name)
    m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (*rgb, 1.0)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    b.inputs["Specular IOR Level"].default_value = spec
    m.diffuse_color = (*rgb, 1.0)
    m.metallic = metal
    m.roughness = rough
    MATS[name] = m
    return m



# ---------------------------------------------------------------- mesh builder with rigid / blended skin weights
class Body:
    def __init__(self):
        self.bm = bmesh.new()
        self.dl = self.bm.verts.layers.deform.verify()
        self.nj = self.bm.verts.layers.int.new("no_jitter")
        self.no_jitter = False
        self.mats = []

    def _mi(self, m):
        if m not in self.mats:
            self.mats.append(m)
        return self.mats.index(m)

    def _skin(self, verts, faces, m, w, crisp=False):
        idx = self._mi(m)
        for f in faces:
            f.material_index = idx
        for v in verts:
            if self.no_jitter or crisp:
                v[self.nj] = 1
            weights = w(v.co) if callable(w) else {w: 1.0}
            for bone, val in weights.items():
                if val > 0:
                    v[self.dl][BONES.index(bone)] = val

    def _add(self, res, m, w):
        verts = res["verts"]
        faces = list({f for v in verts for f in v.link_faces})
        self._skin(verts, faces, m, w)

    def box(self, m, w, center, size, rot=None, bevel=0.0):
        mtx = Matrix.Translation(Vector(center)) @ (rot or Matrix.Identity(4)) @ Matrix.Diagonal((*size, 1.0))
        res = bmesh.ops.create_cube(self.bm, size=1.0, matrix=mtx)
        if bevel <= 0:
            self._add(res, m, w)
            return
        verts = res["verts"]
        edges = list({e for v in verts for e in v.link_edges})
        faces0 = list({f for v in verts for f in v.link_faces})
        self._skin(verts, faces0, m, w)   # bevel keeps the material of the faces it rebuilds
        out = bmesh.ops.bevel(self.bm, geom=verts + edges, offset=bevel, offset_type="OFFSET", segments=1,
                              profile=0.5, affect="EDGES", clamp_overlap=True)
        faces = [f for f in faces0 if f.is_valid] + list(out["faces"])
        self._skin(list({v for f in faces for v in f.verts}), faces, m, w)

    def ico(self, m, w, center, r, sub=1, scale=(1, 1, 1)):
        mtx = Matrix.Translation(Vector(center)) @ Matrix.Diagonal((*scale, 1.0))
        res = bmesh.ops.create_icosphere(self.bm, subdivisions=sub, radius=r, matrix=mtx)
        self._add(res, m, w)
        return res["verts"]

    def limb(self, m, w, a, b, r1, r2, n=7, cap=True):
        """Tapered cylinder from point a to point b."""
        a, b = Vector(a), Vector(b)
        d = b - a
        rot = d.to_track_quat("Z", "Y").to_matrix().to_4x4()
        mtx = Matrix.Translation(a + d / 2) @ rot
        res = bmesh.ops.create_cone(self.bm, cap_ends=cap, cap_tris=False, segments=n,
                                    radius1=r1, radius2=r2, depth=d.length, matrix=mtx)
        self._add(res, m, w)

    def loft(self, rings, matf, w, closed=True, inner=None, inner_shrink=0.96, axis_pts=None, seal=False, seal_top=False):
        """Surface through rings of equal point count. matf(i, j) picks the material of face row i / column j.
        Faces are oriented away from the ring centers; inner adds a slightly shrunk lining surface, and seal joins the
        lining to the outside along the open edges so the part reads as cloth with thickness."""
        n = len(rings[0])
        centers = axis_pts or [sum((Vector(p) for p in r), Vector()) / len(r) for r in rings]
        cols = n if closed else n - 1
        grids = []
        made = []
        for layer, sign, shrink in ((0, 1, 1.0), (1, -1, inner_shrink)):
            if layer == 1 and inner is None:
                break
            grid = []
            for r, c in zip(rings, centers):
                grid.append([self.bm.verts.new(Vector(c) + (Vector(p) - Vector(c)) * shrink) for p in r])
            grids.append(grid)
            for i in range(len(rings) - 1):
                for j in range(cols):
                    j2 = (j + 1) % n
                    f = self.bm.faces.new([grid[i][j], grid[i][j2], grid[i + 1][j2], grid[i + 1][j]])
                    f.normal_update()
                    mid = f.calc_center_median()
                    cmid = (Vector(centers[i]) + Vector(centers[i + 1])) / 2
                    if f.normal.dot(mid - cmid) * sign < 0:
                        f.normal_flip()
                    self._skin(f.verts, [f], inner if layer == 1 else matf(i, j), w)
                    made.append(f)
        if seal and inner is not None:
            o, q = grids
            last = len(rings) - 1
            edges = [((last, j), (last, (j + 1) % n)) for j in range(cols)]
            if seal_top:
                edges += [((0, j), (0, (j + 1) % n)) for j in range(cols)]
            if not closed:
                edges += [((i, 0), (i + 1, 0)) for i in range(last)] + [((i, n - 1), (i + 1, n - 1)) for i in range(last)]
            for (i1, j1), (i2, j2) in edges:
                f = self.bm.faces.new([o[i1][j1], o[i2][j2], q[i2][j2], q[i1][j1]])
                self._skin(f.verts, [f], inner, w)
                made.append(f)
            bmesh.ops.recalc_face_normals(self.bm, faces=made)

    def prism(self, m, w, pts, thickness, normal, crisp=False):
        """Flat polygon extruded by thickness along normal."""
        off = Vector(normal).normalized() * thickness / 2
        front = [self.bm.verts.new(Vector(p) - off) for p in pts]
        back = [self.bm.verts.new(Vector(p) + off) for p in pts]
        faces = [self.bm.faces.new(front), self.bm.faces.new(list(reversed(back)))]
        k = len(pts)
        for i in range(k):
            faces.append(self.bm.faces.new([front[i], back[i], back[(i + 1) % k], front[(i + 1) % k]]))
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        self._skin(front + back, faces, m, w, crisp)

    def ribbon(self, m, w, pts, normals, width, thick=0.03, bevel=0.4):
        """Raised strip along a polyline lying on a surface (normals point away from it). The cross-section is a
        trapezoid, so trims and scrollwork get a bevelled edge. width is a number or one value per point."""
        n = len(pts)
        widths = width if isinstance(width, (list, tuple)) else [width] * n
        secs = []
        for k in range(n):
            p, nn = Vector(pts[k]), Vector(normals[k]).normalized()
            t = Vector(pts[min(k + 1, n - 1)]) - Vector(pts[max(k - 1, 0)])
            t = (t - nn * t.dot(nn)).normalized()
            sd = t.cross(nn).normalized()
            hw = max(widths[k], 0.006) / 2
            ft = hw * (1.0 - bevel)
            secs.append([self.bm.verts.new(v) for v in (p + sd * hw, p + sd * ft + nn * thick,
                                                        p - sd * ft + nn * thick, p - sd * hw)])
        faces = []
        for a, b in zip(secs, secs[1:]):
            for q in range(4):
                faces.append(self.bm.faces.new([a[q], a[(q + 1) % 4], b[(q + 1) % 4], b[q]]))
        faces.append(self.bm.faces.new(secs[0]))
        faces.append(self.bm.faces.new(list(reversed(secs[-1]))))
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        self._skin([v for s in secs for v in s], faces, m, w, True)

    def gem(self, m, w, center, normal, up, hw, hh, height, sides=4, top=0.45):
        """Bevelled stud: a polygon (diamond, hexagon...) on a surface with a smaller raised table on top."""
        n = Vector(normal).normalized()
        u = Vector(up)
        u = (u - n * u.dot(n)).normalized()
        sd = u.cross(n)
        c = Vector(center)
        ring = [(math.cos(math.pi / 2 + 2 * math.pi * k / sides), math.sin(math.pi / 2 + 2 * math.pi * k / sides))
                for k in range(sides)]
        base = [self.bm.verts.new(c - n * 0.01 + sd * x * hw + u * y * hh) for x, y in ring]
        tops = [self.bm.verts.new(c + n * height + (sd * x * hw + u * y * hh) * top) for x, y in ring]
        faces = [self.bm.faces.new(base), self.bm.faces.new(list(reversed(tops)))]
        for k in range(sides):
            faces.append(self.bm.faces.new([base[k], base[(k + 1) % sides], tops[(k + 1) % sides], tops[k]]))
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        self._skin(base + tops, faces, m, w, True)

    def build(self, name, jitter=0.0, seed=7):
        if jitter > 0:
            rng = random.Random(seed)
            for v in self.bm.verts:
                if v[self.nj]:
                    continue
                v.co += Vector((rng.uniform(-jitter, jitter), rng.uniform(-jitter, jitter), rng.uniform(-jitter, jitter)))
        # concave n-gons (crosses, eyes) are cut here so Unity does not fan-triangulate them wrongly
        bmesh.ops.triangulate(self.bm, faces=[f for f in self.bm.faces if len(f.verts) > 4], quad_method="BEAUTY",
                              ngon_method="EAR_CLIP")
        me = bpy.data.meshes.new(name)
        self.bm.to_mesh(me)
        self.bm.free()
        for m in self.mats:
            me.materials.append(m)
        # papercraft look: every face flat-shaded
        for p in me.polygons:
            p.use_smooth = False
        obj = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(obj)
        for bname in BONES:
            obj.vertex_groups.new(name=bname)
        return obj


def blend(z0, b0, z1, b1):
    """Weights fading from bone b0 (at z0 and below) to bone b1 (at z1 and above)."""
    def f(co):
        t = min(1.0, max(0.0, (co.z - z0) / (z1 - z0)))
        return {b0: 1.0 - t, b1: t}
    return f


def ellipse(c, rx, ry, n, a0=0.0, a1=2 * math.pi, closed=True):
    pts = []
    count = n if closed else n + 1
    for k in range(count):
        a = a0 + (a1 - a0) * k / n
        pts.append((c[0] + math.sin(a) * rx, c[1] - math.cos(a) * ry, c[2]))
    return pts


def surf(rows, a, z, out=0.0):
    """Point and outward normal on an elliptic loft. rows: (z, rx, ry, cy) from top to bottom; a = angle from the front."""
    for (z0, rx0, ry0, c0), (z1, rx1, ry1, c1) in zip(rows, rows[1:]):
        if min(z0, z1) - 1e-6 <= z <= max(z0, z1) + 1e-6:
            t = (z - z0) / (z1 - z0)
            rx, ry, c = rx0 + (rx1 - rx0) * t, ry0 + (ry1 - ry0) * t, c0 + (c1 - c0) * t
            slope = ((rx1 - rx0) * abs(math.sin(a)) + (ry1 - ry0) * abs(math.cos(a))) / (z1 - z0)
            nrm = Vector((math.sin(a) / rx, -math.cos(a) / ry, 0.0))
            nrm = Vector((nrm.x, nrm.y, -slope * nrm.length)).normalized()
            return Vector((math.sin(a) * rx, c - math.cos(a) * ry, z)) + nrm * out, nrm
    raise ValueError(z)


def surf_path(rows, path, out=0.012):
    """path: (angle in degrees, z) pairs -> points and normals for B.ribbon."""
    res = [surf(rows, math.radians(a), z, out) for a, z in path]
    return [p for p, _ in res], [n for _, n in res]


def smooth(path, k=3):
    """Catmull-Rom resampling of a short control path so scrollwork reads as curves, not dashes."""
    pts = [Vector(p) for p in path]
    pts = [pts[0]] + pts + [pts[-1]]
    out = []
    for i in range(1, len(pts) - 2):
        p0, p1, p2, p3 = pts[i - 1], pts[i], pts[i + 1], pts[i + 2]
        for s in range(k):
            t = s / k
            out.append(0.5 * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    out.append(pts[-2])
    return [tuple(p) for p in out]


def taper(n, w0, w1, tip=None):
    """Per-point widths from w0 to w1 (optionally pinching to tip at the end)."""
    ws = [w0 + (w1 - w0) * k / max(1, n - 1) for k in range(n)]
    if tip is not None:
        ws[-1] = tip
    return ws



def grid_normal(rings, axis, i, j):
    r = rings[i]
    du = Vector(r[min(j + 1, len(r) - 1)]) - Vector(r[max(j - 1, 0)])
    dv = Vector(rings[min(i + 1, len(rings) - 1)][j]) - Vector(rings[max(i - 1, 0)][j])
    nn = du.cross(dv).normalized()
    return nn if nn.dot(Vector(r[j]) - Vector(axis[i])) > 0 else -nn


def hand(B, skin, side, sx):
    """Tapered mitten plus a thumb, like the Knight."""
    fa_b, hand_b = bone_pos(side + "ForeArm", True), bone_pos(side + "Hand", True)
    B.limb(skin, side + "Hand", fa_b, hand_b + Vector((sx * 0.02, 0, 0)), 0.1, 0.045, 6)
    B.limb(skin, side + "Hand", fa_b.lerp(hand_b, 0.2) + Vector((0, -0.05, 0.02)),
           fa_b.lerp(hand_b, 0.45) + Vector((0, -0.13, 0.03)), 0.05, 0.03, 5)


# ---------------------------------------------------------------- head: soft low-poly face (cheeks, nose bridge, tapered chin)
class HeadKit:
    """Head shape plus helpers that place decals / hair strands on its surface. hc = head centre, hr = radius,
    xs = width factor."""

    def __init__(self, B, hc=Vector((0.0, -0.17, 1.99)), hr=0.54, xs=1.08):
        self.B, self.HC, self.HR, self.xs = B, Vector(hc), hr, xs
        hb = bmesh.new()
        self.head_mesh(hb)
        self.bvh = BVHTree.FromBMesh(hb)

    def head_mesh(self, bm, scale=1.0, lift=Vector()):
        HC, HR = self.HC, self.HR
        res = bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=12, radius=1.0)
        verts = res["verts"]
        if not any(abs(v.co.x) < 1e-4 and v.co.y < -0.5 for v in verts):
            rz = Matrix.Rotation(math.pi / 16, 3, "Z")
            for v in verts:
                v.co = rz @ v.co
        for v in verts:
            x, y, z = v.co
            jaw = min(1.0, max(0.0, (-0.1 - z) / 0.9))
            x *= (1.0 - 0.3 * jaw * jaw) * (1.0 + 0.06 * math.exp(-((z + 0.3) / 0.25) ** 2))
            if y < 0:
                y *= 0.84 - 0.1 * jaw
                if abs(x) < 1e-3 and -0.55 < z < -0.12:
                    y -= 0.07 * math.sin((z + 0.55) / 0.43 * math.pi)       # nose bridge ridge
            else:
                y *= 0.96
            z *= 1.0 if z > 0 else 1.08
            v.co = HC + lift + Vector((x * HR * self.xs, y * HR, z * HR)) * scale
        return res

    def on_face(self, x, z, lift=0.0):
        """Point and normal on the front of the head (ray-cast); lift > 0 moves it outward."""
        hit = self.bvh.ray_cast(Vector((x, -3.0, z)), Vector((0, 1, 0)))
        if hit[0] is None:
            return Vector((x, self.HC.y - self.HR * 0.84 - lift, z)), Vector((0, -1, 0))
        nn = hit[1] if hit[1].y < 0 else -hit[1]
        nn = (nn + Vector((0, -1, 0))).normalized()
        return hit[0] + nn * lift, nn

    def on_head(self, az, el, lift=0.0):
        """Point and normal on the head at azimuth az (0 = front, + = character's left) and elevation el, in degrees."""
        a, e = math.radians(az), math.radians(el)
        d = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
        hit = self.bvh.ray_cast(self.HC, d)
        nn = (hit[1] if hit[1].dot(d) > 0 else -hit[1]).lerp(d, 0.5).normalized()
        return hit[0] + nn * lift, nn

    def face_ribbon(self, m, pts2d, width, thick=0.012, lift=0.004):
        res = [self.on_face(x, z, lift) for x, z in pts2d]
        self.B.ribbon(m, "Head", [p for p, _ in res], [n for _, n in res], width, thick, 0.3)

    def face_decal(self, m, pts2d, lift=0.004, thick=0.01):
        """Flat shape that follows the face (eyes)."""
        B = self.B
        front = [self.on_face(x, z, lift + thick)[0] for x, z in pts2d]
        back = [self.on_face(x, z, lift - 0.012)[0] for x, z in pts2d]
        fv = [B.bm.verts.new(p) for p in front]
        bv = [B.bm.verts.new(p) for p in back]
        faces = [B.bm.faces.new(fv), B.bm.faces.new(list(reversed(bv)))]
        k = len(fv)
        for i in range(k):
            faces.append(B.bm.faces.new([fv[i], bv[i], bv[(i + 1) % k], fv[(i + 1) % k]]))
        bmesh.ops.recalc_face_normals(B.bm, faces=faces)
        B._skin(fv + bv, faces, m, "Head", True)

    def face(self, skin, eye, eye_dk, eye_hl, lid, brow, mouth, ex=0.235, eye_s=(1.2, 1.45), brow_tilt=0.0, smile=1.0):
        """Head plus the standard face: soft half-lidded eyes, brows, small nose, mouth.
        brow_tilt > 0 raises the outer brow end (stern / evil look); smile < 0 turns the mouth down."""
        B, HC = self.B, self.HC
        B._add(self.head_mesh(B.bm), skin, "Head")
        EZ = HC.z - 0.07
        su, sv = eye_s
        for sx in (1, -1):
            x0 = sx * ex
            shape = [(-0.07, 0.016), (-0.02, 0.022), (0.03, 0.02), (0.075, 0.01), (0.085, -0.008), (0.06, -0.03),
                     (0.02, -0.04), (-0.025, -0.036), (-0.06, -0.016)]
            self.face_decal(eye, [(x0 + sx * u * su, EZ + v * sv) for u, v in shape])
            self.face_decal(eye_dk, [(x0 + sx * u * su, EZ + v * sv) for u, v in
                                     ((-0.06, 0.016), (-0.02, 0.022), (0.03, 0.02), (0.07, 0.011), (0.06, -0.004),
                                      (0.0, 0.0), (-0.05, 0.0))], 0.008, 0.004)
            if eye_hl is not None:
                self.face_decal(eye_hl, [(x0 + sx * (-0.03 + 0.011 * math.cos(t * math.pi / 3)),
                                          EZ - 0.015 + 0.011 * math.sin(t * math.pi / 3)) for t in range(6)], 0.012, 0.003)
            self.face_ribbon(lid, [(x0 + sx * u * su, EZ + v * sv) for u, v in
                                   ((-0.085, 0.012), (-0.03, 0.026), (0.03, 0.025), (0.08, 0.012), (0.1, -0.008))],
                             [0.012, 0.02, 0.021, 0.017, 0.01], 0.01, 0.008)
            bt = brow_tilt
            self.face_ribbon(brow, [(x0 + sx * u, EZ + v) for u, v in
                                    ((-0.07, 0.112 - bt), (-0.015, 0.122 - bt * 0.3), (0.045, 0.12 + bt * 0.3),
                                     (0.1, 0.104 + bt))], [0.04, 0.038, 0.032, 0.022], 0.016, 0.004)
        nose_c, nose_n = self.on_face(0.0, HC.z - 0.2, 0.0)
        B.gem(skin, "Head", nose_c, nose_n, (0, 0, 1), 0.035, 0.05, 0.035, 3, 0.3)
        dz = 0.014 * smile
        self.face_ribbon(mouth, [(-0.06, HC.z - 0.322), (-0.025, HC.z - 0.322 - dz), (0.025, HC.z - 0.322 - dz),
                                 (0.06, HC.z - 0.322)], [0.01, 0.016, 0.016, 0.01], 0.008, 0.002)

    def hair_cap(self, m, front_z=0.3, low_z=-0.5):
        """Hair shell over the head with the face cut out (front below HC.z + front_z, and everything below low_z)."""
        B, HC = self.B, self.HC
        cap = self.head_mesh(B.bm, 1.08, Vector((0, 0.02, 0.05)))
        cap_faces = list({f for v in cap["verts"] for f in v.link_faces})
        drop = [f for f in cap_faces if (f.calc_center_median().y < HC.y - 0.12 and f.calc_center_median().z < HC.z + front_z)
                or f.calc_center_median().z < HC.z + low_z]
        bmesh.ops.delete(B.bm, geom=drop, context="FACES")
        cap_verts = [v for v in cap["verts"] if v.is_valid]
        B._skin(cap_verts, list({f for v in cap_verts for f in v.link_faces}), m, "Head")

    def strand(self, m, path, w0, w1, lift0, lift1, thick=0.05, hang=0.0):
        """Thick hair strand following the head; path = (azimuth, elevation) control points."""
        pts, nrm = [], []
        ctrl = smooth(path, 3)
        for k, (az, el) in enumerate(ctrl):
            p, nn = self.on_head(az, el, lift0 + (lift1 - lift0) * k / (len(ctrl) - 1))
            pts.append(p)
            nrm.append(nn)
        if hang > 0:
            for _ in range(2):
                pts.append(pts[-1] + Vector((0, 0, -hang * 0.5)))
                nrm.append(nrm[-1])
        self.B.ribbon(m, "Head", pts, nrm, taper(len(pts), w0, w1, 0.02), thick, 0.5)


def scale_new_verts(B, before, pivot, s):
    """Scale every vertex created since `before` (a set of verts) about pivot."""
    pivot = Vector(pivot)
    s = Vector(s) if isinstance(s, (tuple, list)) else Vector((s, s, s))
    for v in B.bm.verts:
        if v not in before:
            d = v.co - pivot
            v.co = pivot + Vector((d.x * s.x, d.y * s.y, d.z * s.z))


# ---------------------------------------------------------------- skin to the armature and export
def export(B, mesh_name, folder, fbx_name, blend_name, jitter=0.006):
    out_dir = os.path.join(ROOT, "Assets", "_Project", "Resources", folder)
    os.makedirs(out_dir, exist_ok=True)
    out_fbx = os.path.join(out_dir, fbx_name)
    out_blend = os.path.join(ROOT, "ArtSource", "characters", blend_name)
    mesh = B.build(mesh_name, jitter=jitter)
    mesh.data.attributes.remove(mesh.data.attributes["no_jitter"])
    mesh.parent = ARM
    mesh.matrix_parent_inverse = ARM.matrix_world.inverted()
    mod = mesh.modifiers.new("Armature", "ARMATURE")
    mod.object = ARM

    bpy.ops.object.select_all(action="DESELECT")
    ARM.select_set(True)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = ARM
    bpy.ops.export_scene.fbx(filepath=out_fbx, use_selection=True, object_types={"ARMATURE", "MESH"},
                             add_leaf_bones=False, bake_anim=False, mesh_smooth_type="FACE",
                             use_armature_deform_only=False)
    bpy.ops.wm.save_as_mainfile(filepath=out_blend)
    print(mesh_name, len(mesh.data.polygons), "faces,", len(mesh.data.vertices), "verts ->", out_fbx)
    return mesh
