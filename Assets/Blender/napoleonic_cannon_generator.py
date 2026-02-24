"""
napoleonic_cannon_generator.py
Blender script to generate realistic Napoleonic-era cannons (12-pounder field artillery)
Auto-exports to Unity Assets folder for immediate use

Usage in Blender:
1. Open Blender
2. Go to Scripting tab
3. Open this file
4. Run Script

The cannon will be exported to: e:/FPSLowPoly/Assets/Models/Cannons/
"""

import bpy
import os
import math
from mathutils import Vector

# Configuration
UNITY_PROJECT_PATH = "e:/FPSLowPoly"
EXPORT_PATH = os.path.join(UNITY_PROJECT_PATH, "Assets", "Models", "Cannons")
CANNON_TYPES = {
    "12_pounder": {"barrel_length": 2.5, "caliber": 0.12, "weight": "heavy"},
    "6_pounder": {"barrel_length": 2.0, "caliber": 0.09, "weight": "medium"},
    "3_pounder": {"barrel_length": 1.5, "caliber": 0.07, "weight": "light"},
}

def clear_scene():
    """Clear existing mesh objects"""
    bpy.ops.object.select_all(action='DESELECT')
    bpy.ops.object.select_by_type(type='MESH')
    bpy.ops.object.delete()
    
    # Clear materials
    for mat in bpy.data.materials:
        if mat.users == 0:
            bpy.data.materials.remove(mat)

def create_material(name, color, metallic=0.0, roughness=0.5, specular=0.5):
    """Create PBR material for Unity URP - Blender 5.0 compatible"""
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    
    bsdf.inputs['Base Color'].default_value = (*color, 1.0)
    bsdf.inputs['Metallic'].default_value = metallic
    bsdf.inputs['Roughness'].default_value = roughness
    # Blender 5.0: Specular is now controlled by IOR and Specular Tint
    # No direct 'Specular' input available
    
    return mat

def create_cannon_barrel(length=2.5, caliber=0.12):
    """
    Create realistic cannon barrel with:
    - Tapered shape (thicker at breech)
    - Reinforcing rings
    - Bore (hollow center for cannonball)
    - Touchhole (ignition hole)
    """
    # Main barrel - tapered cylinder
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=32,
        radius=caliber * 2.5,  # Breech diameter
        depth=length,
        location=(0, 0, length/2)
    )
    barrel = bpy.context.active_object
    barrel.name = "Cannon_Barrel"
    
    # Add taper by scaling the top vertices (muzzle is narrower)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='DESELECT')
    
    # Select top face (muzzle)
    bpy.ops.mesh.select_non_manifold()
    
    # Scale down for taper effect
    bpy.ops.transform.resize(value=(0.7, 0.7, 1.0))
    
    bpy.ops.object.mode_set(mode='OBJECT')
    
    # Add reinforcing rings
    ring_positions = [0.1, 0.3, 0.5, 0.7, 0.9]  # Relative positions along barrel
    for i, pos in enumerate(ring_positions):
        ring_z = pos * length
        ring_radius = caliber * 2.8 if pos < 0.5 else caliber * 2.5  # Thicker rings at breech
        
        bpy.ops.mesh.primitive_torus_add(
            major_radius=ring_radius,
            minor_radius=0.03,
            major_segments=32,
            minor_segments=8,
            location=(0, 0, ring_z)
        )
        ring = bpy.context.active_object
        ring.name = f"Reinforcing_Ring_{i}"
        ring.parent = barrel
    
    # Create bore (inner hollow - visual only, boolean operation)
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=16,
        radius=caliber / 2,
        depth=length * 1.01,
        location=(0, 0, length/2)
    )
    bore = bpy.context.active_object
    bore.name = "Cannon_Bore"
    
    # Boolean difference to create hollow barrel
    bool_mod = barrel.modifiers.new(name="Bore_Boolean", type='BOOLEAN')
    bool_mod.operation = 'DIFFERENCE'
    bool_mod.object = bore
    
    # Apply boolean
    bpy.context.view_layer.objects.active = barrel
    bpy.ops.object.modifier_apply(modifier="Bore_Boolean")
    
    # Delete bore object
    bpy.data.objects.remove(bore, do_unlink=True)
    
    # Add touchhole (ignition hole)
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=8,
        radius=0.015,
        depth=0.08,
        location=(caliber * 2.3, 0, length * 0.85)
    )
    touchhole = bpy.context.active_object
    touchhole.rotation_euler = (0, math.pi/2, 0)
    touchhole.name = "Touchhole"
    touchhole.parent = barrel
    
    # Create trunnions (pivot points for elevation)
    trunnion_z = length * 0.35
    for x_offset in [-caliber * 3, caliber * 3]:
        bpy.ops.mesh.primitive_cylinder_add(
            vertices=16,
            radius=0.06,
            depth=0.15,
            location=(x_offset, 0, trunnion_z),
            rotation=(0, math.pi/2, 0)
        )
        trunnion = bpy.context.active_object
        trunnion.name = f"Trunnion_{'Left' if x_offset < 0 else 'Right'}"
        trunnion.parent = barrel
    
    return barrel

def create_carriage(width=1.2, length=1.8, height=0.8):
    """
    Create wooden carriage (affut) with:
    - Side cheeks (brassons)
    - Transoms (traverse pieces)
    - Axle tree
    - Trunnion holes
    - Elevation screw mechanism
    """
    carriage_objects = []
    
    # Side cheeks (main wooden beams)
    cheek_thickness = 0.15
    cheek_width = 0.25
    for y_offset in [-width/2 + cheek_width/2, width/2 - cheek_width/2]:
        bpy.ops.mesh.primitive_cube_add(
            size=1,
            location=(0, y_offset, height * 0.6)
        )
        cheek = bpy.context.active_object
        cheek.scale = (length * 0.5, cheek_width/2, height * 0.4)
        cheek.name = f"Side_Cheek_{'Left' if y_offset < 0 else 'Right'}"
        carriage_objects.append(cheek)
        
        # Add trunnion notches
        bpy.ops.mesh.primitive_cube_add(
            size=1,
            location=(0, y_offset, height * 0.75)
        )
        notch = bpy.context.active_object
        notch.scale = (0.12, cheek_width/2 + 0.02, 0.08)
        notch.name = f"Trunnion_Notch_{'Left' if y_offset < 0 else 'Right'}"
        carriage_objects.append(notch)
    
    # Transoms (cross pieces)
    transom_positions = [-length * 0.35, 0, length * 0.35]
    for i, pos in enumerate(transom_positions):
        bpy.ops.mesh.primitive_cube_add(
            size=1,
            location=(pos, 0, height * 0.4)
        )
        transom = bpy.context.active_object
        transom.scale = (0.08, width/2 - 0.05, 0.12)
        transom.name = f"Transom_{i}"
        carriage_objects.append(transom)
    
    # Axle tree
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=16,
        radius=0.12,
        depth=width + 0.4,
        location=(length * 0.35, 0, height * 0.25),
        rotation=(math.pi/2, 0, 0)
    )
    axle = bpy.context.active_object
    axle.name = "Axle_Tree"
    carriage_objects.append(axle)
    
    # Elevation screw/quoin slot
    bpy.ops.mesh.primitive_cube_add(
        size=1,
        location=(length * 0.15, 0, height * 0.85)
    )
    quoin_slot = bpy.context.active_object
    quoin_slot.scale = (0.4, 0.15, 0.05)
    quoin_slot.name = "Quoin_Slot"
    carriage_objects.append(quoin_slot)
    
    # Quoin (triangular elevation wedge)
    bpy.ops.mesh.primitive_cube_add(
        size=1,
        location=(length * 0.15, 0, height * 0.82)
    )
    quoin = bpy.context.active_object
    quoin.scale = (0.3, 0.12, 0.08)
    
    # Make quoin triangular
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.transform.translate(value=(-0.15, 0, 0.04))
    bpy.ops.object.mode_set(mode='OBJECT')
    
    quoin.name = "Quoin"
    carriage_objects.append(quoin)
    
    # Create empty parent object for carriage
    bpy.ops.object.empty_add(type='ARROWS', location=(0, 0, height * 0.4))
    carriage_parent = bpy.context.active_object
    carriage_parent.name = "Carriage"
    
    for obj in carriage_objects:
        obj.parent = carriage_parent
    
    return carriage_parent

def create_wheels(diameter=0.9, width=0.15, axle_z=0.6):
    """
    Create detailed wooden wheels with:
    - Hub (nave)
    - Spokes (rayons)
    - Felloes (jantes)
    - Iron tire band
    """
    wheel_objects = []
    
    # Hub (nave)
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=24,
        radius=0.15,
        depth=width + 0.1,
        location=(0, 0, axle_z),
        rotation=(math.pi/2, 0, 0)
    )
    hub = bpy.context.active_object
    hub.name = "Wheel_Hub"
    wheel_objects.append(hub)
    
    # Spokes (14 typical spokes for artillery)
    num_spokes = 14
    spoke_length = diameter / 2 - 0.18
    for i in range(num_spokes):
        angle = (2 * math.pi * i) / num_spokes
        x = math.cos(angle) * spoke_length / 2
        z = axle_z + math.sin(angle) * spoke_length / 2
        
        bpy.ops.mesh.primitive_cylinder_add(
            vertices=8,
            radius=0.025,
            depth=spoke_length,
            location=(x, 0, z),
            rotation=(0, -angle + math.pi/2, 0)
        )
        spoke = bpy.context.active_object
        spoke.name = f"Spoke_{i}"
        wheel_objects.append(spoke)
    
    # Felloe (rim segments)
    segment_angle = 2 * math.pi / num_spokes
    felloe_radius = diameter / 2 - 0.05
    for i in range(num_spokes):
        angle = (2 * math.pi * i) / num_spokes + segment_angle / 2
        x = math.cos(angle) * felloe_radius
        z = axle_z + math.sin(angle) * felloe_radius
        
        bpy.ops.mesh.primitive_cube_add(
            size=1,
            location=(x, 0, z)
        )
        felloe = bpy.context.active_object
        felloe.scale = (0.12, width * 0.8, 0.08)
        felloe.rotation_euler = (0, angle, 0)
        felloe.name = f"Felloe_{i}"
        wheel_objects.append(felloe)
    
    # Iron tire (outer rim band)
    bpy.ops.mesh.primitive_torus_add(
        major_radius=felloe_radius,
        minor_radius=0.04,
        major_segments=48,
        minor_segments=8,
        location=(0, 0, axle_z),
        rotation=(math.pi/2, 0, 0)
    )
    tire = bpy.context.active_object
    tire.name = "Iron_Tire"
    wheel_objects.append(tire)
    
    # Wheel parent
    bpy.ops.object.empty_add(type='ARROWS', location=(0, 0, axle_z))
    wheel_parent = bpy.context.active_object
    wheel_parent.name = "Wheel"
    
    for obj in wheel_objects:
        obj.parent = wheel_parent
    
    return wheel_parent

def create_cannon_complete(cannon_type="12_pounder"):
    """Create complete cannon assembly with all parts"""
    
    specs = CANNON_TYPES.get(cannon_type, CANNON_TYPES["12_pounder"])
    
    print(f"Creating {cannon_type} cannon...")
    
    # Clear scene
    clear_scene()
    
    # Materials
    bronze_mat = create_material(
        "Bronze_Barrel",
        color=(0.65, 0.45, 0.25),
        metallic=0.8,
        roughness=0.3
    )
    
    wood_mat = create_material(
        "Carriage_Wood",
        color=(0.35, 0.22, 0.12),
        metallic=0.0,
        roughness=0.8
    )
    
    iron_mat = create_material(
        "Iron_Parts",
        color=(0.25, 0.25, 0.28),
        metallic=0.9,
        roughness=0.4
    )
    
    # Create barrel
    barrel = create_cannon_barrel(specs["barrel_length"], specs["caliber"])
    barrel.data.materials.append(bronze_mat)
    
    # Apply bronze material to barrel children
    for child in barrel.children:
        if child.type == 'MESH':
            child.data.materials.append(bronze_mat)
    
    # Create carriage
    carriage = create_carriage(
        width=1.4,
        length=specs["barrel_length"] * 0.8,
        height=0.9
    )
    
    # Apply wood material to carriage
    for child in carriage.children_recursive:
        if child.type == 'MESH':
            child.data.materials.append(wood_mat)
    
    # Create wheels
    wheel_diameter = 0.9 if specs["weight"] == "heavy" else 0.75
    wheel_width = 0.12
    
    left_wheel = create_wheels(
        diameter=wheel_diameter,
        width=wheel_width,
        axle_z=0.5
    )
    left_wheel.location = (0.8, -0.85, 0)
    left_wheel.name = "Wheel_Left"
    
    right_wheel = create_wheels(
        diameter=wheel_diameter,
        width=wheel_width,
        axle_z=0.5
    )
    right_wheel.location = (0.8, 0.85, 0)
    right_wheel.name = "Wheel_Right"
    
    # Apply materials to wheels
    for wheel in [left_wheel, right_wheel]:
        for child in wheel.children_recursive:
            if child.type == 'MESH':
                if "Tire" in child.name:
                    child.data.materials.append(iron_mat)
                else:
                    child.data.materials.append(wood_mat)
    
    # Create main parent
    bpy.ops.object.empty_add(type='ARROWS', location=(0, 0, 0))
    cannon_parent = bpy.context.active_object
    cannon_parent.name = f"Cannon_{cannon_type}"
    
    barrel.parent = cannon_parent
    carriage.parent = cannon_parent
    left_wheel.parent = cannon_parent
    right_wheel.parent = cannon_parent
    
    print(f"Cannon {cannon_type} created successfully!")
    
    return cannon_parent

def export_to_unity(cannon_type="12_pounder"):
    """Export cannon as FBX to Unity Assets folder"""
    
    # Ensure export directory exists
    os.makedirs(EXPORT_PATH, exist_ok=True)
    
    # Select all cannon objects
    cannon_name = f"Cannon_{cannon_type}"
    if cannon_name not in bpy.data.objects:
        print(f"Error: {cannon_name} not found!")
        return False
    
    cannon = bpy.data.objects[cannon_name]
    
    # Select cannon and all children
    bpy.ops.object.select_all(action='DESELECT')
    cannon.select_set(True)
    
    def select_recursive(obj):
        for child in obj.children:
            child.select_set(True)
            select_recursive(child)
    
    select_recursive(cannon)
    
    # Export filepath
    filepath = os.path.join(EXPORT_PATH, f"cannon_{cannon_type}.fbx")
    
    # Export FBX optimized for Unity
    bpy.ops.export_scene.fbx(
        filepath=filepath,
        use_selection=True,
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS',
        axis_forward='-Z',
        axis_up='Y',
        use_mesh_modifiers=True,
        mesh_smooth_type='FACE',
        use_mesh_edges=False,
        use_tspace=True,
        use_custom_props=True,
        add_leaf_bones=False,
        primary_bone_axis='Y',
        secondary_bone_axis='X',
        use_armature_deform_only=False,
        bake_anim=False,  # No animation for static cannon
        use_metadata=True
    )
    
    print(f"Exported to: {filepath}")
    return True

def generate_all_cannons():
    """Generate and export all cannon types"""
    for cannon_type in CANNON_TYPES.keys():
        print(f"\n=== Generating {cannon_type} ===")
        create_cannon_complete(cannon_type)
        export_to_unity(cannon_type)
    
    print("\n=== All cannons generated and exported! ===")
    print(f"Location: {EXPORT_PATH}")

# Main execution
if __name__ == "__main__":
    # Generate single cannon (12-pounder by default)
    create_cannon_complete("12_pounder")
    export_to_unity("12_pounder")
    
    # Or generate all types:
    # generate_all_cannons()
