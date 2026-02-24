"""
Auto-rig a Meshy AI soldier model with a proper Armature and automatic weights.

This creates a REAL skeleton (Armature with Bones) and uses Blender's automatic
weight painting to assign vertex groups. Unity will import this as a proper
SkinnedMeshRenderer with correct bone weights.

Run: blender --background --python auto_rig_soldier.py
"""

import bpy
import os
from mathutils import Vector

# ============================================================
# PATHS
# ============================================================
INPUT_FBX = r"e:\FPSLowPoly\Assets\Resources\Models\FrenchLineInfantry_HighPoly\Meshy_AI_Colonial_Soldier_in_a_0223111650_texture.fbx"
OUTPUT_FBX = r"e:\FPSLowPoly\Assets\Resources\Models\FrenchLineInfantry_Rigged\Soldier_Rigged.fbx"

os.makedirs(os.path.dirname(OUTPUT_FBX), exist_ok=True)

# ============================================================
# CLEAN SCENE
# ============================================================
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for block in bpy.data.meshes:
    if block.users == 0: bpy.data.meshes.remove(block)
for block in bpy.data.armatures:
    if block.users == 0: bpy.data.armatures.remove(block)

# ============================================================
# IMPORT FBX
# ============================================================
print("=== Importing FBX ===")
bpy.ops.import_scene.fbx(filepath=INPUT_FBX, use_anim=False, ignore_leaf_bones=True)

meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
if not meshes:
    print("ERROR: No mesh found!")
    exit(1)

mesh_obj = meshes[0]
print(f"Found mesh: {mesh_obj.name}, verts: {len(mesh_obj.data.vertices)}")

# Apply transforms
bpy.context.view_layer.objects.active = mesh_obj
mesh_obj.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# Get mesh bounds  
verts = mesh_obj.data.vertices
all_coords = [v.co for v in verts]
min_x = min(v.x for v in all_coords)
max_x = max(v.x for v in all_coords)
min_y = min(v.y for v in all_coords)
max_y = max(v.y for v in all_coords)
min_z = min(v.z for v in all_coords)
max_z = max(v.z for v in all_coords)
height = max_z - min_z
width = max_x - min_x
cx = (min_x + max_x) / 2
cy = (min_y + max_y) / 2

print(f"Bounds: X=[{min_x:.3f},{max_x:.3f}] Y=[{min_y:.3f},{max_y:.3f}] Z=[{min_z:.3f},{max_z:.3f}]")
print(f"Height={height:.3f}, Width={width:.3f}, Center=({cx:.3f},{cy:.3f})")

# ============================================================
# CREATE ARMATURE WITH REAL BONES
# ============================================================
print("=== Creating Armature ===")

bz = min_z
h = height

# Bone joint positions (x_offset from center, z as fraction of height)
def pos(x_frac, z_frac):
    return Vector((cx + x_frac * width, cy, bz + z_frac * h))

bpy.ops.object.add(type='ARMATURE', location=(0, 0, 0))
armature_obj = bpy.context.active_object
armature_obj.name = "Armature"
armature = armature_obj.data
armature.name = "SoldierArmature"

# Enter edit mode to create bones
bpy.ops.object.mode_set(mode='EDIT')

# Helper to create a bone
def make_bone(name, head_pos, tail_pos, parent_name=None):
    bone = armature.edit_bones.new(name)
    bone.head = head_pos
    bone.tail = tail_pos
    if parent_name and parent_name in armature.edit_bones:
        bone.parent = armature.edit_bones[parent_name]
    bone.use_connect = False
    print(f"  Bone: {name} head={head_pos} tail={tail_pos}")
    return bone

# Spine chain
make_bone("Hips",        pos(0, 0.45), pos(0, 0.50))
make_bone("Spine",       pos(0, 0.50), pos(0, 0.55), "Hips")
make_bone("Spine1",      pos(0, 0.55), pos(0, 0.62), "Spine")
make_bone("Spine2",      pos(0, 0.62), pos(0, 0.70), "Spine1")

# Neck & Head
make_bone("Neck",        pos(0, 0.70), pos(0, 0.78), "Spine2")
make_bone("Head",        pos(0, 0.78), pos(0, 0.90), "Neck")

# Left arm
make_bone("LeftShoulder",  pos(0.06, 0.70), pos(0.12, 0.70), "Spine2")
make_bone("LeftArm",       pos(0.12, 0.70), pos(0.25, 0.70), "LeftShoulder")
make_bone("LeftForeArm",   pos(0.25, 0.70), pos(0.38, 0.70), "LeftArm")
make_bone("LeftHand",      pos(0.38, 0.70), pos(0.44, 0.70), "LeftForeArm")

# Right arm
make_bone("RightShoulder", pos(-0.06, 0.70), pos(-0.12, 0.70), "Spine2")
make_bone("RightArm",      pos(-0.12, 0.70), pos(-0.25, 0.70), "RightShoulder")
make_bone("RightForeArm",  pos(-0.25, 0.70), pos(-0.38, 0.70), "RightArm")
make_bone("RightHand",     pos(-0.38, 0.70), pos(-0.44, 0.70), "RightForeArm")

# Left leg
make_bone("LeftUpLeg",    pos(0.08, 0.45), pos(0.08, 0.25), "Hips")
make_bone("LeftLeg",      pos(0.08, 0.25), pos(0.08, 0.05), "LeftUpLeg")
make_bone("LeftFoot",     pos(0.08, 0.05), pos(0.08, 0.00), "LeftLeg")

# Right leg
make_bone("RightUpLeg",   pos(-0.08, 0.45), pos(-0.08, 0.25), "Hips")
make_bone("RightLeg",     pos(-0.08, 0.25), pos(-0.08, 0.05), "RightUpLeg")
make_bone("RightFoot",    pos(-0.08, 0.05), pos(-0.08, 0.00), "RightLeg")

bpy.ops.object.mode_set(mode='OBJECT')

print(f"Armature created with {len(armature.bones)} bones")

# ============================================================
# PARENT MESH TO ARMATURE WITH AUTOMATIC WEIGHTS
# ============================================================
print("=== Parenting mesh to armature with automatic weights ===")

# Select mesh first, then armature (armature must be active)
bpy.ops.object.select_all(action='DESELECT')
mesh_obj.select_set(True)
armature_obj.select_set(True)
bpy.context.view_layer.objects.active = armature_obj

# Parent with automatic weights — Blender calculates vertex weights from bone proximity
bpy.ops.object.parent_set(type='ARMATURE_AUTO')

# Verify vertex groups were created
print(f"Vertex groups on mesh: {len(mesh_obj.vertex_groups)}")
for vg in mesh_obj.vertex_groups:
    # Count vertices in this group
    count = 0
    for v in mesh_obj.data.vertices:
        for g in v.groups:
            if g.group == vg.index and g.weight > 0.01:
                count += 1
                break
    print(f"  {vg.name}: {count} vertices")

# ============================================================
# VERIFY WEIGHTS QUALITY
# ============================================================
print("\n=== Weight verification ===")
total_verts = len(mesh_obj.data.vertices)
weighted_verts = 0
multi_bone_verts = 0
for v in mesh_obj.data.vertices:
    groups_with_weight = [g for g in v.groups if g.weight > 0.01]
    if len(groups_with_weight) > 0:
        weighted_verts += 1
    if len(groups_with_weight) > 1:
        multi_bone_verts += 1

print(f"Total vertices: {total_verts}")
print(f"Vertices with weights: {weighted_verts} ({100*weighted_verts/total_verts:.1f}%)")
print(f"Vertices with multi-bone influence: {multi_bone_verts}")

if weighted_verts < total_verts * 0.5:
    print("WARNING: Less than 50% of vertices have weights!")
    print("Attempting to fix with bone heat weighting...")
    
    # Try envelope weights as fallback
    bpy.ops.object.select_all(action='DESELECT')
    mesh_obj.select_set(True)
    armature_obj.select_set(True)
    bpy.context.view_layer.objects.active = armature_obj
    
    try:
        bpy.ops.object.parent_set(type='ARMATURE_ENVELOPE')
        print("Applied envelope weights as fallback")
    except:
        print("Envelope weights also failed, proceeding anyway")

# ============================================================
# EXPORT FBX
# ============================================================
print(f"\n=== Exporting to {OUTPUT_FBX} ===")

bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(
    filepath=OUTPUT_FBX,
    use_selection=True,
    apply_scale_options='FBX_SCALE_ALL',
    global_scale=1.0,
    apply_unit_scale=True,
    bake_anim=False,
    object_types={'ARMATURE', 'MESH'},
    add_leaf_bones=False,
    path_mode='COPY',
    embed_textures=True,
    mesh_smooth_type='FACE',
    axis_forward='-Z',
    axis_up='Y',
)

print("\n=== DONE ===")
print(f"Output: {OUTPUT_FBX}")
print(f"Bones: {len(armature.bones)}")
print(f"Mesh vertices: {len(mesh_obj.data.vertices)}")
print(f"Vertex groups: {len(mesh_obj.vertex_groups)}")
