"""Inspect the rigged FBX in Resources."""
import bpy

INPUT = r"e:\FPSLowPoly\Assets\Resources\Models\FrenchLineInfantry_Rigged\Soldier_Rigged.fbx"

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

bpy.ops.import_scene.fbx(filepath=INPUT, use_anim=False, ignore_leaf_bones=False)

print("=== ALL OBJECTS ===")
for obj in bpy.context.scene.objects:
    print(f"  {obj.type}: {obj.name}")

print("\n=== ARMATURE BONES ===")
for obj in bpy.context.scene.objects:
    if obj.type == 'ARMATURE':
        print(f"Armature: {obj.name}, bones: {len(obj.data.bones)}")
        for bone in obj.data.bones:
            parent = bone.parent.name if bone.parent else "ROOT"
            print(f"  {bone.name} (parent={parent})")

print("\n=== MESH INFO ===")
for obj in bpy.context.scene.objects:
    if obj.type == 'MESH':
        print(f"Mesh: {obj.name}, verts={len(obj.data.vertices)}, faces={len(obj.data.polygons)}")
        print(f"  Vertex groups: {len(obj.vertex_groups)}")
        for vg in obj.vertex_groups:
            count = 0
            for v in obj.data.vertices:
                for g in v.groups:
                    if g.group == vg.index and g.weight > 0.01:
                        count += 1
                        break
            print(f"    {vg.name}: {count} verts")
        
        # Summary: how many verts have weights at all
        total = len(obj.data.vertices)
        weighted = 0
        for v in obj.data.vertices:
            if any(g.weight > 0.01 for g in v.groups):
                weighted += 1
        print(f"  Weighted vertices: {weighted}/{total} ({100*weighted/total:.1f}%)")
