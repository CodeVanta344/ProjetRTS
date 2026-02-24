"""
Batch Export All Napoleonic Soldiers to FBX
=============================================
Generates all 10 soldier variants with animations and exports them
as individual FBX files ready for Unity import.

Usage:
  1. Open Blender (fresh scene)
  2. Run this script (Alt+P)
  3. FBX files are exported to: e:/FPSLowPoly/Assets/Resources/Models/
  4. In Unity: Tools > Configure Soldier FBX (auto-configures rig + animations)

Each soldier gets:
  - Rigged mesh with materials
  - All 15 animations baked into the FBX:
    Idle, Walk, Run, Attack_Ranged, Attack_Melee, Death, Charge,
    Standing_Aim, Standing_Fire, Kneeling_Aim, Kneeling_Fire,
    Reload, VolleyFire, Present_Arms, Flee
"""

import bpy
import os
import sys

# Add the BlenderScripts folder to path so we can import our modules
# __file__ is unreliable in Blender's Text Editor, so we try multiple approaches
SCRIPT_DIR = None
try:
    SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
except Exception:
    pass

# Verify the directory actually contains our scripts
if SCRIPT_DIR is None or not os.path.exists(os.path.join(SCRIPT_DIR, "napoleonic_soldier_generator.py")):
    SCRIPT_DIR = "e:/FPSLowPoly/BlenderScripts"

if SCRIPT_DIR not in sys.path:
    sys.path.append(SCRIPT_DIR)

# Output directory
OUTPUT_DIR = "e:/FPSLowPoly/Assets/Resources/Models"

FACTIONS = ["France", "Britain"]
UNIT_TYPES = ["LineInfantry", "Grenadier", "LightInfantry", "Cavalry", "Artillery"]


def clear_scene():
    """Remove all objects from the scene. Works without requiring specific context."""
    # Remove all objects directly (no operator needed)
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)

    # Clean orphan data
    for block in list(bpy.data.meshes):
        if block.users == 0:
            bpy.data.meshes.remove(block)
    for block in list(bpy.data.materials):
        if block.users == 0:
            bpy.data.materials.remove(block)
    for block in list(bpy.data.armatures):
        if block.users == 0:
            bpy.data.armatures.remove(block)
    for block in list(bpy.data.actions):
        if block.users == 0:
            bpy.data.actions.remove(block)


def export_fbx(armature_obj, mesh_obj, filepath):
    """Export selected objects as FBX."""
    # Deselect all without using operators
    for obj in bpy.data.objects:
        obj.select_set(False)
    mesh_obj.select_set(True)
    armature_obj.select_set(True)
    bpy.context.view_layer.objects.active = armature_obj

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


def main():
    # Ensure output directory exists
    os.makedirs(OUTPUT_DIR, exist_ok=True)

    print("\n" + "=" * 60)
    print("  BATCH EXPORT: NAPOLEONIC SOLDIERS")
    print("=" * 60)

    # We need to import the generator and animation modules
    # Since they use bpy at module level, we exec them as needed
    generator_path = os.path.join(SCRIPT_DIR, "napoleonic_soldier_generator.py")
    animations_path = os.path.join(SCRIPT_DIR, "napoleonic_animations.py")

    # Load generator module
    import importlib.util
    gen_spec = importlib.util.spec_from_file_location("napoleonic_soldier_generator", generator_path)
    gen_module = importlib.util.module_from_spec(gen_spec)
    gen_spec.loader.exec_module(gen_module)

    anim_spec = importlib.util.spec_from_file_location("napoleonic_animations", animations_path)
    anim_module = importlib.util.module_from_spec(anim_spec)
    anim_spec.loader.exec_module(anim_module)

    exported = 0

    for faction in FACTIONS:
        for unit_type in UNIT_TYPES:
            print(f"\n--- {faction} {unit_type} ---")

            # Clear scene
            clear_scene()

            # Generate soldier
            rig, mesh = gen_module.generate_soldier(faction, unit_type, location=(0, 0, 0))

            # Create animations on the rig
            bpy.context.view_layer.objects.active = rig
            rig.select_set(True)
            try:
                bpy.ops.object.mode_set(mode='POSE')
            except RuntimeError:
                pass

            # Core animations (7)
            anim_module.create_idle(rig)
            anim_module.create_walk(rig)
            anim_module.create_run(rig)
            anim_module.create_attack_ranged(rig)
            anim_module.create_attack_melee(rig)
            anim_module.create_death(rig)
            anim_module.create_charge(rig)

            # Firing & position animations (8)
            anim_module.create_standing_aim(rig)
            anim_module.create_standing_fire(rig)
            anim_module.create_kneeling_aim(rig)
            anim_module.create_kneeling_fire(rig)
            anim_module.create_reload(rig)
            anim_module.create_volley_fire(rig)
            anim_module.create_present_arms(rig)
            anim_module.create_flee(rig)

            try:
                bpy.ops.object.mode_set(mode='OBJECT')
            except RuntimeError:
                pass

            # Export
            filename = f"{faction}_{unit_type}.fbx"
            filepath = os.path.join(OUTPUT_DIR, filename)
            export_fbx(rig, mesh, filepath)

            exported += 1
            print(f"  Exported: {filepath}")

    print(f"\n" + "=" * 60)
    print(f"  DONE! Exported {exported} soldiers to:")
    print(f"  {OUTPUT_DIR}")
    print(f"=" * 60)
    print(f"\nIn Unity:")
    print(f"  1. FBX files auto-import into Assets/Resources/Models/")
    print(f"  2. Run: Tools > Configure Soldier FBX (auto-configures everything)")
    print(f"  3. UnitModelLoader will auto-detect models from Resources/Models/")


if __name__ == "__main__":
    main()
