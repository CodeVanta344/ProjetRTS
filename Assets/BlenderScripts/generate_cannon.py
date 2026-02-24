"""
Blender Script — Napoleonic Field Cannon Generator
Run in Blender: Edit > Preferences > Add-ons (not needed)
Just open in Blender's Script Editor and press Run.

Generates a complete Napoleonic cannon with:
- Bronze barrel with muzzle flare, reinforcing rings, touchhole, and cascabel
- Wooden carriage (cheeks + transom)
- Two spoked wheels with hub and rim
- Axle
- Trail (tail of the carriage)

Output is game-ready low-poly (~3k-5k tris).
"""

import bpy
import bmesh
import math
from mathutils import Vector, Matrix

# ============================================================
# PARAMETERS — tweak these to change proportions
# ============================================================
BARREL_LENGTH = 2.2        # Total barrel length
BARREL_RADIUS_BREECH = 0.18  # Radius at breech (back)
BARREL_RADIUS_MUZZLE = 0.12  # Radius at muzzle (front)
MUZZLE_FLARE_RADIUS = 0.15   # Flared muzzle opening
BARREL_SEGMENTS = 24         # Smoothness of barrel cylinder

WHEEL_RADIUS = 0.55
WHEEL_WIDTH = 0.08
WHEEL_SPOKES = 12
HUB_RADIUS = 0.08
RIM_THICKNESS = 0.04

CARRIAGE_LENGTH = 1.8
CARRIAGE_HEIGHT = 0.35
CARRIAGE_WIDTH = 0.5
CHEEK_THICKNESS = 0.06

TRAIL_LENGTH = 1.6
TRAIL_WIDTH = 0.12
TRAIL_HEIGHT = 0.10

AXLE_RADIUS = 0.035
AXLE_EXTRA = 0.08  # How much axle sticks out past wheels

BARREL_HEIGHT_OFFSET = 0.42   # Height of barrel center above ground


# ============================================================
# UTILITIES
# ============================================================
def clean_scene():
    """Remove all mesh objects from the scene."""
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)


def create_material(name, color, metallic=0.0, roughness=0.5):
    """Create a simple PBR material."""
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = color
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = roughness
    return mat


def assign_material(obj, mat):
    """Assign material to object."""
    if obj.data.materials:
        obj.data.materials[0] = mat
    else:
        obj.data.materials.append(mat)


def set_smooth(obj):
    """Apply smooth shading."""
    for poly in obj.data.polygons:
        poly.use_smooth = True


# ============================================================
# MATERIALS
# ============================================================
def create_materials():
    bronze = create_material("Bronze", (0.72, 0.45, 0.20, 1.0), metallic=0.85, roughness=0.35)
    wood_dark = create_material("WoodDark", (0.25, 0.13, 0.06, 1.0), metallic=0.0, roughness=0.75)
    iron = create_material("Iron", (0.25, 0.25, 0.28, 1.0), metallic=0.9, roughness=0.5)
    wood_light = create_material("WoodLight", (0.45, 0.28, 0.12, 1.0), metallic=0.0, roughness=0.7)
    return bronze, wood_dark, iron, wood_light


# ============================================================
# BARREL
# ============================================================
def create_barrel(bronze_mat, iron_mat):
    """Create the cannon barrel with taper, muzzle flare, reinforcing rings, and cascabel."""
    
    # Main barrel — tapered cylinder
    verts = []
    faces = []
    n = BARREL_SEGMENTS
    num_sections = 20  # Number of length sections for taper
    
    for s in range(num_sections + 1):
        t = s / num_sections
        z = -BARREL_LENGTH / 2 + t * BARREL_LENGTH
        
        # Taper: breech is wider, muzzle is narrower
        radius = BARREL_RADIUS_BREECH + (BARREL_RADIUS_MUZZLE - BARREL_RADIUS_BREECH) * t
        
        # Muzzle flare at the very end (last 5%)
        if t > 0.95:
            flare_t = (t - 0.95) / 0.05
            radius = BARREL_RADIUS_MUZZLE + (MUZZLE_FLARE_RADIUS - BARREL_RADIUS_MUZZLE) * flare_t
        
        # Reinforcing rings — slight bumps at specific positions
        ring_positions = [0.05, 0.15, 0.45, 0.75]
        for rp in ring_positions:
            if abs(t - rp) < 0.015:
                radius += 0.012
        
        for i in range(n):
            angle = 2 * math.pi * i / n
            x = radius * math.cos(angle)
            y = radius * math.sin(angle)
            verts.append((x, y, z))
    
    # Create faces between sections
    for s in range(num_sections):
        for i in range(n):
            i_next = (i + 1) % n
            v0 = s * n + i
            v1 = s * n + i_next
            v2 = (s + 1) * n + i_next
            v3 = (s + 1) * n + i
            faces.append((v0, v1, v2, v3))
    
    # Cap the breech end
    breech_center_idx = len(verts)
    verts.append((0, 0, -BARREL_LENGTH / 2))
    for i in range(n):
        i_next = (i + 1) % n
        faces.append((breech_center_idx, i_next, i))
    
    # Cap the muzzle end (hollow — just a ring, no center cap for realism)
    muzzle_center_idx = len(verts)
    bore_radius = BARREL_RADIUS_MUZZLE * 0.55
    verts.append((0, 0, BARREL_LENGTH / 2))
    for i in range(n):
        i_next = (i + 1) % n
        base = num_sections * n
        faces.append((base + i, base + i_next, muzzle_center_idx))
    
    # Create mesh
    mesh = bpy.data.meshes.new("CannonBarrel")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    
    barrel = bpy.data.objects.new("CannonBarrel", mesh)
    bpy.context.collection.objects.link(barrel)
    
    # Rotate barrel to point forward (along Y axis) and position it
    barrel.rotation_euler = (math.radians(90), 0, 0)
    barrel.location = (0, BARREL_LENGTH * 0.15, BARREL_HEIGHT_OFFSET)
    
    assign_material(barrel, bronze_mat)
    set_smooth(barrel)
    
    # === CASCABEL (knob at breech) ===
    bpy.ops.mesh.primitive_uv_sphere_add(
        radius=BARREL_RADIUS_BREECH * 0.7,
        segments=16, ring_count=8,
        location=(0, -BARREL_LENGTH * 0.35 - 0.02, BARREL_HEIGHT_OFFSET)
    )
    cascabel = bpy.context.active_object
    cascabel.name = "Cascabel"
    cascabel.scale = (1, 1.4, 1)
    assign_material(cascabel, bronze_mat)
    set_smooth(cascabel)
    
    # === TRUNNIONS (cylindrical pegs on sides where barrel sits on carriage) ===
    for side in [-1, 1]:
        bpy.ops.mesh.primitive_cylinder_add(
            radius=0.035, depth=0.15,
            vertices=12,
            location=(side * (BARREL_RADIUS_BREECH + 0.06), 0, BARREL_HEIGHT_OFFSET)
        )
        trunnion = bpy.context.active_object
        trunnion.name = f"Trunnion_{'L' if side < 0 else 'R'}"
        trunnion.rotation_euler = (0, math.radians(90), 0)
        assign_material(trunnion, iron_mat)
        set_smooth(trunnion)
    
    # === TOUCHHOLE — small cylinder on top of breech ===
    bpy.ops.mesh.primitive_cylinder_add(
        radius=0.015, depth=0.03,
        vertices=8,
        location=(0, -BARREL_LENGTH * 0.25, BARREL_HEIGHT_OFFSET + BARREL_RADIUS_BREECH + 0.01)
    )
    touchhole = bpy.context.active_object
    touchhole.name = "Touchhole"
    assign_material(touchhole, iron_mat)
    
    return barrel


# ============================================================
# CARRIAGE
# ============================================================
def create_carriage(wood_mat, iron_mat):
    """Create the gun carriage with two cheeks and a transom."""
    parts = []
    
    # === CHEEKS (two side planks) ===
    for side in [-1, 1]:
        bpy.ops.mesh.primitive_cube_add(
            size=1,
            location=(
                side * (CARRIAGE_WIDTH / 2 - CHEEK_THICKNESS / 2),
                0,
                CARRIAGE_HEIGHT / 2 + 0.05
            )
        )
        cheek = bpy.context.active_object
        cheek.name = f"Cheek_{'L' if side < 0 else 'R'}"
        cheek.scale = (CHEEK_THICKNESS / 2, CARRIAGE_LENGTH / 2, CARRIAGE_HEIGHT / 2)
        assign_material(cheek, wood_mat)
        parts.append(cheek)
    
    # === TRANSOM (front cross beam) ===
    bpy.ops.mesh.primitive_cube_add(
        size=1,
        location=(0, CARRIAGE_LENGTH * 0.35, CARRIAGE_HEIGHT * 0.4)
    )
    transom_front = bpy.context.active_object
    transom_front.name = "TransomFront"
    transom_front.scale = (CARRIAGE_WIDTH / 2, 0.04, CARRIAGE_HEIGHT * 0.35)
    assign_material(transom_front, wood_mat)
    parts.append(transom_front)
    
    # === TRANSOM (rear cross beam) ===
    bpy.ops.mesh.primitive_cube_add(
        size=1,
        location=(0, -CARRIAGE_LENGTH * 0.35, CARRIAGE_HEIGHT * 0.4)
    )
    transom_rear = bpy.context.active_object
    transom_rear.name = "TransomRear"
    transom_rear.scale = (CARRIAGE_WIDTH / 2, 0.04, CARRIAGE_HEIGHT * 0.35)
    assign_material(transom_rear, wood_mat)
    parts.append(transom_rear)
    
    # === ELEVATION WEDGE (quoin) ===
    bpy.ops.mesh.primitive_cube_add(
        size=1,
        location=(0, -0.15, CARRIAGE_HEIGHT * 0.7)
    )
    quoin = bpy.context.active_object
    quoin.name = "ElevationQuoin"
    quoin.scale = (0.08, 0.15, 0.04)
    assign_material(quoin, wood_mat)
    parts.append(quoin)
    
    # === IRON STRAPS on cheeks ===
    for side in [-1, 1]:
        for z_pos in [0.3, 0.0, -0.3]:
            bpy.ops.mesh.primitive_cube_add(
                size=1,
                location=(
                    side * (CARRIAGE_WIDTH / 2 - CHEEK_THICKNESS / 2),
                    z_pos,
                    CARRIAGE_HEIGHT / 2 + 0.05
                )
            )
            strap = bpy.context.active_object
            strap.name = f"Strap_{side}_{z_pos}"
            strap.scale = (CHEEK_THICKNESS / 2 + 0.005, 0.02, CARRIAGE_HEIGHT / 2 + 0.01)
            assign_material(strap, iron_mat)
            parts.append(strap)
    
    return parts


# ============================================================
# WHEELS
# ============================================================
def create_wheel(x_pos, wood_mat, iron_mat):
    """Create a spoked wheel with hub, spokes, and iron rim."""
    wheel_parts = []
    
    # === RIM (torus) ===
    bpy.ops.mesh.primitive_torus_add(
        major_radius=WHEEL_RADIUS,
        minor_radius=RIM_THICKNESS,
        major_segments=36,
        minor_segments=8,
        location=(x_pos, 0, WHEEL_RADIUS)
    )
    rim = bpy.context.active_object
    rim.name = f"WheelRim_{'L' if x_pos < 0 else 'R'}"
    rim.rotation_euler = (0, math.radians(90), 0)
    assign_material(rim, iron_mat)
    set_smooth(rim)
    wheel_parts.append(rim)
    
    # === HUB ===
    bpy.ops.mesh.primitive_cylinder_add(
        radius=HUB_RADIUS, depth=WHEEL_WIDTH * 1.5,
        vertices=16,
        location=(x_pos, 0, WHEEL_RADIUS)
    )
    hub = bpy.context.active_object
    hub.name = f"WheelHub_{'L' if x_pos < 0 else 'R'}"
    hub.rotation_euler = (0, math.radians(90), 0)
    assign_material(hub, iron_mat)
    set_smooth(hub)
    wheel_parts.append(hub)
    
    # === SPOKES ===
    spoke_length = WHEEL_RADIUS - HUB_RADIUS - RIM_THICKNESS
    for i in range(WHEEL_SPOKES):
        angle = 2 * math.pi * i / WHEEL_SPOKES
        
        # Spoke center position
        spoke_center_r = HUB_RADIUS + spoke_length / 2
        cy = spoke_center_r * math.cos(angle)
        cz = spoke_center_r * math.sin(angle) + WHEEL_RADIUS
        
        bpy.ops.mesh.primitive_cube_add(
            size=1,
            location=(x_pos, cy, cz)
        )
        spoke = bpy.context.active_object
        spoke.name = f"Spoke_{i}_{'L' if x_pos < 0 else 'R'}"
        spoke.scale = (WHEEL_WIDTH * 0.3, spoke_length / 2, 0.018)
        spoke.rotation_euler = (angle, 0, 0)
        assign_material(spoke, wood_mat)
        wheel_parts.append(spoke)
    
    # === OUTER FELLOE SEGMENTS (wooden ring inside iron rim) ===
    for i in range(WHEEL_SPOKES):
        angle_start = 2 * math.pi * i / WHEEL_SPOKES
        angle_mid = angle_start + math.pi / WHEEL_SPOKES
        
        fy = (WHEEL_RADIUS - RIM_THICKNESS * 1.2) * math.cos(angle_mid)
        fz = (WHEEL_RADIUS - RIM_THICKNESS * 1.2) * math.sin(angle_mid) + WHEEL_RADIUS
        
        bpy.ops.mesh.primitive_cube_add(
            size=1,
            location=(x_pos, fy, fz)
        )
        felloe = bpy.context.active_object
        felloe.name = f"Felloe_{i}_{'L' if x_pos < 0 else 'R'}"
        seg_len = 2 * math.pi * (WHEEL_RADIUS - RIM_THICKNESS) / WHEEL_SPOKES / 2
        felloe.scale = (WHEEL_WIDTH * 0.35, seg_len, 0.025)
        felloe.rotation_euler = (angle_mid, 0, 0)
        assign_material(felloe, wood_mat)
        wheel_parts.append(felloe)
    
    return wheel_parts


# ============================================================
# AXLE
# ============================================================
def create_axle(iron_mat):
    """Create the axle connecting both wheels."""
    total_width = CARRIAGE_WIDTH + WHEEL_WIDTH * 2 + AXLE_EXTRA * 2
    
    bpy.ops.mesh.primitive_cylinder_add(
        radius=AXLE_RADIUS, depth=total_width,
        vertices=12,
        location=(0, 0, WHEEL_RADIUS)
    )
    axle = bpy.context.active_object
    axle.name = "Axle"
    axle.rotation_euler = (0, math.radians(90), 0)
    assign_material(axle, iron_mat)
    set_smooth(axle)
    return axle


# ============================================================
# TRAIL (tail of the carriage)
# ============================================================
def create_trail(wood_mat, iron_mat):
    """Create the trail — the long tail that rests on the ground."""
    trail_parts = []
    
    # === Main trail beams (two parallel) ===
    for side in [-1, 1]:
        bpy.ops.mesh.primitive_cube_add(
            size=1,
            location=(
                side * TRAIL_WIDTH * 0.7,
                -CARRIAGE_LENGTH / 2 - TRAIL_LENGTH / 2 + 0.1,
                TRAIL_HEIGHT / 2
            )
        )
        beam = bpy.context.active_object
        beam.name = f"TrailBeam_{'L' if side < 0 else 'R'}"
        beam.scale = (TRAIL_WIDTH / 2, TRAIL_LENGTH / 2, TRAIL_HEIGHT / 2)
        assign_material(beam, wood_mat)
        trail_parts.append(beam)
    
    # === Trail cross beam ===
    bpy.ops.mesh.primitive_cube_add(
        size=1,
        location=(0, -CARRIAGE_LENGTH / 2 - TRAIL_LENGTH * 0.3, TRAIL_HEIGHT / 2)
    )
    cross = bpy.context.active_object
    cross.name = "TrailCross"
    cross.scale = (TRAIL_WIDTH * 1.3, 0.03, TRAIL_HEIGHT / 2)
    assign_material(cross, wood_mat)
    trail_parts.append(cross)
    
    # === Trail end ring (for limber attachment) ===
    bpy.ops.mesh.primitive_torus_add(
        major_radius=0.06,
        minor_radius=0.015,
        major_segments=16,
        minor_segments=6,
        location=(0, -CARRIAGE_LENGTH / 2 - TRAIL_LENGTH + 0.05, TRAIL_HEIGHT * 0.6)
    )
    ring = bpy.context.active_object
    ring.name = "TrailRing"
    ring.rotation_euler = (math.radians(90), 0, 0)
    assign_material(ring, iron_mat)
    set_smooth(ring)
    trail_parts.append(ring)
    
    # === Trail handle / handspike slot ===
    bpy.ops.mesh.primitive_cube_add(
        size=1,
        location=(0, -CARRIAGE_LENGTH / 2 - TRAIL_LENGTH * 0.7, TRAIL_HEIGHT + 0.02)
    )
    handle_slot = bpy.context.active_object
    handle_slot.name = "HandspikeSlot"
    handle_slot.scale = (0.03, 0.12, 0.02)
    assign_material(handle_slot, iron_mat)
    trail_parts.append(handle_slot)
    
    return trail_parts


# ============================================================
# MAIN
# ============================================================
def generate_cannon():
    """Generate the complete Napoleonic cannon."""
    print("=" * 50)
    print("  Generating Napoleonic Field Cannon...")
    print("=" * 50)
    
    clean_scene()
    
    # Create materials
    bronze, wood_dark, iron, wood_light = create_materials()
    
    # Create all parts
    barrel = create_barrel(bronze, iron)
    carriage_parts = create_carriage(wood_dark, iron)
    
    wheel_x = CARRIAGE_WIDTH / 2 + WHEEL_WIDTH / 2
    left_wheel = create_wheel(-wheel_x, wood_light, iron)
    right_wheel = create_wheel(wheel_x, wood_light, iron)
    
    axle = create_axle(iron)
    trail_parts = create_trail(wood_dark, iron)
    
    # === Parent everything under an empty ===
    bpy.ops.object.empty_add(type='ARROWS', location=(0, 0, 0))
    cannon_root = bpy.context.active_object
    cannon_root.name = "Cannon"
    
    # Parent all objects
    all_objects = [barrel]
    all_objects.extend(carriage_parts)
    all_objects.extend(left_wheel)
    all_objects.extend(right_wheel)
    all_objects.append(axle)
    all_objects.extend(trail_parts)
    
    # Also parent cascabel, trunnions, touchhole
    for obj in bpy.data.objects:
        if obj.name in ["Cascabel", "Trunnion_L", "Trunnion_R", "Touchhole"]:
            all_objects.append(obj)
    
    for obj in all_objects:
        if obj is not None:
            obj.parent = cannon_root
    
    # Scale to game-appropriate size (1 unit ≈ 1 meter)
    cannon_root.scale = (1, 1, 1)
    
    # Select root
    bpy.ops.object.select_all(action='DESELECT')
    cannon_root.select_set(True)
    bpy.context.view_layer.objects.active = cannon_root
    
    # Count triangles
    total_tris = 0
    for obj in all_objects:
        if obj is not None and obj.type == 'MESH':
            total_tris += len(obj.data.polygons) * 2  # Approximate
    
    print(f"\n  ✓ Cannon generated successfully!")
    print(f"  ✓ Total parts: {len(all_objects)}")
    print(f"  ✓ Approximate triangles: {total_tris}")
    print(f"  ✓ Materials: Bronze, WoodDark, WoodLight, Iron")
    print("=" * 50)
    
    return cannon_root


# ============================================================
# AUTO EXPORT FBX
# ============================================================
EXPORT_PATH = r"E:\FPSLowPoly\Assets\Resources\Models\Cannon.fbx"

def export_to_fbx(filepath=EXPORT_PATH):
    """Export the cannon to FBX for Unity import."""
    import os
    
    # Ensure output directory exists
    os.makedirs(os.path.dirname(filepath), exist_ok=True)
    
    # Select all mesh objects + the root
    bpy.ops.object.select_all(action='SELECT')
    
    # Export FBX with Unity-compatible settings
    bpy.ops.export_scene.fbx(
        filepath=filepath,
        use_selection=False,          # Export everything
        apply_scale_options='FBX_SCALE_ALL',
        global_scale=1.0,
        axis_forward='-Z',           # Unity forward
        axis_up='Y',                 # Unity up
        apply_unit_scale=True,
        use_mesh_modifiers=True,
        mesh_smooth_type='FACE',
        use_mesh_edges=False,
        path_mode='COPY',            # Embed textures if any
        embed_textures=True,
        batch_mode='OFF',
        object_types={'MESH', 'EMPTY'},
    )
    
    print(f"\n  ✓ Exported to: {filepath}")
    print(f"  ✓ Unity will auto-import from Assets/Models/Cannon.fbx")


# Run!
if __name__ == "__main__":
    generate_cannon()
    export_to_fbx()
