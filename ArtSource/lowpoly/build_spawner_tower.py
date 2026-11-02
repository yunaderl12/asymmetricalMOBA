# Prepares the textured spawner tower (Tripo FBX, height 1, front = -Y) and exports LP_SpawnerTower.fbx.
# The crystal on top becomes its own material slot so that Unity can light only the crystal while the tower charges.
# Materials carry no textures here; URP materials are made and remapped on the Unity side (Art/LowPoly/).
# Run headless:
#   /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup -P ArtSource/lowpoly/build_spawner_tower.py -- "<repo root>" <source.fbx>
import math
import os
import sys

import bpy
from mathutils import Vector

ROOT, SOURCE = sys.argv[sys.argv.index("--") + 1:][:2]
HEIGHT = 8.0
CRYSTAL_MIN_Z = 0.70  # of the height
CRYSTAL_RADIUS = 0.13  # of the height

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SOURCE)
o = [x for x in bpy.data.objects if x.type == "MESH"][0]
bpy.context.view_layer.objects.active = o
o.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
if o.data.has_custom_normals:
    bpy.ops.mesh.customdata_custom_splitnormals_clear()

mesh = o.data
scale = HEIGHT / max(v.co.z for v in mesh.vertices)
for v in mesh.vertices:
    v.co *= scale
o.name = "LP_SpawnerTower"

mesh.materials.clear()
for name, color in (("LP_SpawnerTower_Body", (0.4, 0.4, 0.4, 1)), ("LP_SpawnerTower_Crystal", (1, 0, 1, 1))):
    m = bpy.data.materials.new(name)
    m.diffuse_color = color
    mesh.materials.append(m)

total = Vector((0, 0, 0))
count = 0
for p in mesh.polygons:
    c = p.center
    crystal = c.z > CRYSTAL_MIN_Z * HEIGHT and math.hypot(c.x, c.y) < CRYSTAL_RADIUS * HEIGHT
    p.material_index = 1 if crystal else 0
    if crystal:
        total += c
        count += 1
print("CRYSTAL faces", count, "of", len(mesh.polygons), "center", tuple(round(x, 3) for x in total / max(count, 1)))

mesh.polygons.foreach_set("use_smooth", [True] * len(mesh.polygons))
mesh.set_sharp_from_angle(angle=math.radians(50))
mesh.update()

for img in list(bpy.data.images):
    bpy.data.images.remove(img)
out = os.path.join(ROOT, "Assets", "_Project", "Art", "LowPoly", "LP_SpawnerTower.fbx")
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={"MESH"}, mesh_smooth_type="FACE", bake_anim=False)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "ArtSource", "lowpoly", "spawner_tower.blend"))
print("EXPORTED", len(mesh.polygons), "faces ->", out)
