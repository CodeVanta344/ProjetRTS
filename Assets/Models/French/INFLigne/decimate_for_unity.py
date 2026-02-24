import bpy
import os

INPUT_FBX = r"E:\FPSLowPoly\Assets\Models\French\INFLigne\LineSoldier.fbx"
OUTPUT_FBX = r"E:\FPSLowPoly\Assets\Resources\Models\France_LineInfantry.fbx"
TARGET_TRIS = 1500

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

for block in bpy.data.meshes:
    if block.users == 0:
        bpy.data.meshes.remove(block)

print("Importing FBX...")
bpy.ops.import_scene.fbx(
    filepath=INPUT_FBX,
    use_anim=True,
    ignore_leaf_bones=True,
    automatic_bone_orientation=True,
)

meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
print("Found " + str(len(meshes)) + " meshes")

total = 0
for obj in meshes:
    dg = bpy.context.evaluated_depsgraph_get()
    eo = obj.evaluated_get(dg)
    m = eo.to_mesh()
    m.calc_loop_triangles()
    t = len(m.loop_triangles)
    total += t
    print("  " + obj.name + ": " + str(t) + " tris")
    eo.to_mesh_clear()

print("Total: " + str(total) + " triangles")

if total > TARGET_TRIS:
    ratio = TARGET_TRIS / total
    print("Decimating with ratio " + str(round(ratio, 4)))

    for obj in meshes:
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        if bpy.context.object and bpy.context.object.mode != 'OBJECT':
            bpy.ops.object.mode_set(mode='OBJECT')

        mod = obj.modifiers.new(name="Decimate", type='DECIMATE')
        mod.decimate_type = 'COLLAPSE'
        mod.ratio = ratio
        mod.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=mod.name)
        obj.select_set(False)

    final = 0
    for obj in meshes:
        dg = bpy.context.evaluated_depsgraph_get()
        eo = obj.evaluated_get(dg)
        m = eo.to_mesh()
        m.calc_loop_triangles()
        final += len(m.loop_triangles)
        eo.to_mesh_clear()
    print("After decimate: " + str(final) + " triangles")

print("Exporting to " + OUTPUT_FBX)
os.makedirs(os.path.dirname(OUTPUT_FBX), exist_ok=True)

bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(
    filepath=OUTPUT_FBX,
    use_selection=True,
    apply_scale_options='FBX_SCALE_ALL',
    global_scale=1.0,
    apply_unit_scale=True,
    bake_anim=True,
    bake_anim_use_all_actions=True,
    bake_anim_simplify_factor=1.0,
    path_mode='COPY',
    embed_textures=True,
    mesh_smooth_type='FACE',
    add_leaf_bones=False,
    axis_forward='-Z',
    axis_up='Y',
)

print("DONE! Exported to " + OUTPUT_FBX)
