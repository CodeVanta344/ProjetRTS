"""
Simple Export Script for Blender 5.0
=====================================
Run AFTER generating soldiers with napoleonic_soldier_generator.py
Exports each armature+mesh pair as a separate FBX file.

Usage:
  1. First run napoleonic_soldier_generator.py (generates 10 soldiers)
  2. Then run this script to export them all
"""

import bpy
import os

OUTPUT_DIR = "e:/FPSLowPoly/Assets/Resources/Models"
os.makedirs(OUTPUT_DIR, exist_ok=True)

# Map soldier names to filenames
FACTION_MAP = {
    "French": "France",
    "British": "Britain",
}

exported = 0

# Find all armatures in the scene
armatures = [obj for obj in bpy.data.objects if obj.type == 'ARMATURE']

if not armatures:
    print("ERROR: No armatures found! Run napoleonic_soldier_generator.py first.")
else:
    print(f"Found {len(armatures)} armatures to export.")

    for arm in armatures:
        # Find the mesh child
        mesh = None
        for child in arm.children:
            if child.type == 'MESH':
                mesh = child
                break

        if mesh is None:
            # Try to find mesh with matching name
            for obj in bpy.data.objects:
                if obj.type == 'MESH' and obj.parent == arm:
                    mesh = obj
                    break

        if mesh is None:
            print(f"  SKIP: {arm.name} - no mesh child found")
            continue

        # Build filename from armature name
        # Names are like "Rig_French_LineInfantry" or similar
        name = arm.name.replace("Rig_", "").replace("Armature_", "")

        # Try to map faction names
        for key, val in FACTION_MAP.items():
            name = name.replace(key, val)

        # Clean up name
        name = name.strip("_").strip()
        if not name:
            name = f"Soldier_{exported}"

        filepath = os.path.join(OUTPUT_DIR, f"{name}.fbx")

        # Deselect all
        for obj in bpy.data.objects:
            obj.select_set(False)

        # Select this pair
        arm.select_set(True)
        mesh.select_set(True)
        bpy.context.view_layer.objects.active = arm

        # Export
        try:
            bpy.ops.export_scene.fbx(
                filepath=filepath,
                use_selection=True,
                apply_scale_options='FBX_SCALE_ALL',
                bake_anim=True,
                bake_anim_use_all_actions=True,
                bake_anim_simplify_factor=1.0,
                path_mode='COPY',
                embed_textures=False,
                mesh_smooth_type='OFF',
                add_leaf_bones=False,
                axis_forward='-Z',
                axis_up='Y',
            )
            exported += 1
            print(f"  OK: {filepath}")
        except Exception as e:
            print(f"  ERROR exporting {name}: {e}")

print(f"\n{'=' * 50}")
print(f"Exported {exported} soldiers to: {OUTPUT_DIR}")
print(f"{'=' * 50}")
