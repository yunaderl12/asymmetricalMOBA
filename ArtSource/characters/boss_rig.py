# ボス（ドラゴン・スコーピオン）の取り込みで共通に使う処理。
# 外から来た骨なしのメッシュ（Tripo の FBX）に、位置で決めた重みの骨を入れて Resources/<folder>/ に書き出す。
# アニメーションのクリップは持たない。Unity 側のスクリプトが骨を毎フレーム回して動かす。
import os
import shutil

import bpy
from mathutils import Matrix, Vector


def smooth(a, b, x):
    """a から b へ 0→1 になるなめらかな段差。a > b なら逆向き。"""
    if a == b:
        return 1.0 if x >= b else 0.0
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def sided(bones, specs):
    """y（または x）の符号つきの骨を左右に作る。specs: (名前, 付け根, 先, 親, 左右の軸 0=x 1=y)。"""
    for side, sgn in (("P", 1.0), ("N", -1.0)):
        for name, head, tail, parent, axis in specs:
            h, t = list(head), list(tail)
            h[axis] *= sgn
            t[axis] *= sgn
            bones.append((name + side, tuple(h), tuple(t), parent if parent in ("Body", None) or parent.startswith("Tail") else parent + side))
    return bones


def chain_weights(heads, names, p, parent_name, blend=0.2):
    """折れ線（骨の付け根を順につないだもの）に p を投影し、いちばん近い区間の骨に乗せる。
    区間の始まり近く（blend の割合まで）は、一つ前の骨と半々から滑らかに分ける。"""
    best, best_i, best_t = 1e9, 0, 0.0
    for i in range(len(heads) - 1):
        a, b = heads[i], heads[i + 1]
        ab = b - a
        t = max(0.0, min(1.0, (p - a).dot(ab) / ab.dot(ab)))
        d = (a + ab * t - p).length
        if d < best:
            best, best_i, best_t = d, i, t
    bone = names[best_i]
    prev = names[best_i - 1] if best_i > 0 else parent_name
    if best_t < blend:
        w_prev = 0.5 * (1.0 - best_t / blend)
        return {bone: 1.0 - w_prev, prev: w_prev}
    return {bone: 1.0}


def load_source(source, mesh_name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=source)
    mesh = [o for o in bpy.data.objects if o.type == "MESH"][0]
    mesh.name = mesh.data.name = mesh_name
    # 取り込み時の回転を頂点へ焼き込む（以後の座標は Blender の Z 上向き）。
    mesh.data.transform(mesh.matrix_world)
    mesh.matrix_world = Matrix.Identity(4)
    for m in mesh.data.materials:
        m.name = mesh_name
    return mesh


def rig_and_export(root, mesh, bones, weights_for, fix, folder, fbx_name, tex_name, blend_name, source):
    """bones: (名前, 付け根, 先, 親) を元の座標で。weights_for(元の座標) -> {骨: 重み}。fix: 元の座標から書き出す座標への変換。"""
    out_dir = os.path.join(root, "Assets", "_Project", "Resources", folder)
    os.makedirs(out_dir, exist_ok=True)
    src = [v.co.copy() for v in mesh.data.vertices]

    arm_data = bpy.data.armatures.new(mesh.name + "_Armature")
    arm = bpy.data.objects.new(mesh.name + "_Armature", arm_data)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for name, head, tail, parent in bones:
        b = arm_data.edit_bones.new(name)
        b.head = fix @ Vector(head)
        b.tail = fix @ Vector(tail)
        if parent:
            b.parent = arm_data.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")

    groups = {name: mesh.vertex_groups.new(name=name) for name, _, _, _ in bones}
    counts = {}
    for i, p in enumerate(src):
        w = weights_for(p)
        total = sum(w.values())
        if total <= 0:
            w, total = {"Body": 1.0}, 1.0
        for bone, v in w.items():
            if v / total > 1e-4:
                groups[bone].add([i], v / total, "REPLACE")
                counts[bone] = counts.get(bone, 0) + 1
    mesh.data.transform(fix)
    mesh.data.update()
    mesh.parent = arm
    mod = mesh.modifiers.new("Armature", "ARMATURE")
    mod.object = arm

    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(filepath=os.path.join(out_dir, fbx_name), use_selection=True, object_types={"ARMATURE", "MESH"},
                             add_leaf_bones=False, bake_anim=False, mesh_smooth_type="FACE",
                             use_armature_deform_only=False)
    fbm = os.path.splitext(source)[0] + ".fbm"
    if os.path.isdir(fbm):
        for f in os.listdir(fbm):
            if "basecolor" in f:
                shutil.copyfile(os.path.join(fbm, f), os.path.join(out_dir, tex_name))
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(root, "ArtSource", "characters", blend_name))
    print("RIG_DONE", folder, len(mesh.data.vertices), "verts", sorted(counts.items()))
