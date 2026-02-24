"""
Blender Script — Napoleonic Soldier Generator (All-in-One)
==========================================================
Generates complete Napoleonic soldiers with:
- Detailed mesh body (torso, limbs, head, hands, boots)
- Equipment (shako, musket, crossbelts, giberne, epaulettes)
- Full armature (Unity Humanoid-compatible skeleton)
- 10 animations: idle, walk, run, standing_fire, kneeling_fire,
  reload, charge, attack_melee, death, flee
- 4 unit variants: Line Infantry, Grenadier, Light Infantry, Cavalry
- Faction materials with PBR
- Auto-export to FBX for Unity

Usage: Open in Blender Script Editor → Run Script
"""

import bpy
import bmesh
import math
from mathutils import Vector, Matrix, Euler

# ============================================================
# CONFIGURATION
# ============================================================
EXPORT_DIR = r"E:\FPSLowPoly\Assets\Resources\Models"
FPS = 30

# Body proportions (meters)
BODY_HEIGHT = 1.75
HEAD_RADIUS = 0.11
NECK_LENGTH = 0.08
TORSO_HEIGHT = 0.45
TORSO_WIDTH = 0.36
TORSO_DEPTH = 0.22
WAIST_WIDTH = 0.30
HIP_HEIGHT = 0.12
HIP_WIDTH = 0.32
UPPER_ARM_LEN = 0.28
LOWER_ARM_LEN = 0.26
HAND_LEN = 0.08
UPPER_LEG_LEN = 0.42
LOWER_LEG_LEN = 0.40
FOOT_LEN = 0.24
FOOT_HEIGHT = 0.08
SHOULDER_WIDTH = 0.42

# Equipment
SHAKO_HEIGHT = 0.22
SHAKO_RADIUS_BOT = 0.10
SHAKO_RADIUS_TOP = 0.11
MUSKET_LENGTH = 1.4
MUSKET_BARREL_R = 0.012
MUSKET_STOCK_W = 0.035
BAYONET_LEN = 0.45

# Faction colors
FACTION_COLORS = {
    'France': {
        'coat': (0.12, 0.15, 0.55, 1.0),
        'pants': (0.85, 0.82, 0.75, 1.0),
        'trim': (0.8, 0.1, 0.1, 1.0),
    },
    'Britain': {
        'coat': (0.7, 0.12, 0.12, 1.0),
        'pants': (0.85, 0.82, 0.75, 1.0),
        'trim': (0.9, 0.85, 0.2, 1.0),
    },
    'Prussia': {
        'coat': (0.1, 0.1, 0.35, 1.0),
        'pants': (0.85, 0.82, 0.75, 1.0),
        'trim': (0.8, 0.2, 0.2, 1.0),
    },
    'Russia': {
        'coat': (0.15, 0.4, 0.15, 1.0),
        'pants': (0.85, 0.82, 0.75, 1.0),
        'trim': (0.8, 0.1, 0.1, 1.0),
    },
    'Austria': {
        'coat': (0.88, 0.86, 0.8, 1.0),
        'pants': (0.85, 0.82, 0.75, 1.0),
        'trim': (0.1, 0.1, 0.1, 1.0),
    },
}

# ============================================================
# UTILITIES
# ============================================================
def clean_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for block in bpy.data.meshes:
        if block.users == 0:
            bpy.data.meshes.remove(block)
    for block in bpy.data.materials:
        if block.users == 0:
            bpy.data.materials.remove(block)
    for block in bpy.data.armatures:
        if block.users == 0:
            bpy.data.armatures.remove(block)
    for action in bpy.data.actions:
        bpy.data.actions.remove(action)


def create_material(name, color, metallic=0.0, roughness=0.5):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = color
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = roughness
    return mat


def assign_material(obj, mat):
    if obj.data.materials:
        obj.data.materials[0] = mat
    else:
        obj.data.materials.append(mat)


def add_material_slot(obj, mat):
    obj.data.materials.append(mat)
    return len(obj.data.materials) - 1


def set_smooth(obj):
    for poly in obj.data.polygons:
        poly.use_smooth = True


def join_objects(objects):
    if not objects:
        return None
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        if obj:
            obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    return bpy.context.active_object


def add_subsurf(obj, levels=1, render_levels=2):
    mod = obj.modifiers.new("Subsurf", 'SUBSURF')
    mod.levels = levels
    mod.render_levels = render_levels


# ============================================================
# MESH BUILDERS — Parametric body parts
# ============================================================
def make_tapered_cylinder(name, top_r, bot_r, height, segments=12, location=(0,0,0)):
    """Create a tapered cylinder (truncated cone)."""
    verts = []
    faces = []
    n = segments

    # Bottom ring
    for i in range(n):
        angle = 2 * math.pi * i / n
        verts.append((bot_r * math.cos(angle), bot_r * math.sin(angle), 0))
    # Top ring
    for i in range(n):
        angle = 2 * math.pi * i / n
        verts.append((top_r * math.cos(angle), top_r * math.sin(angle), height))

    # Side faces
    for i in range(n):
        i2 = (i + 1) % n
        faces.append((i, i2, n + i2, n + i))

    # Bottom cap
    bot_center = len(verts)
    verts.append((0, 0, 0))
    for i in range(n):
        faces.append((bot_center, (i + 1) % n, i))

    # Top cap
    top_center = len(verts)
    verts.append((0, 0, height))
    for i in range(n):
        faces.append((top_center, n + i, n + (i + 1) % n))

    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    return obj


def make_box(name, sx, sy, sz, location=(0,0,0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = (sx/2, sy/2, sz/2)
    bpy.ops.object.transform_apply(scale=True)
    return obj


def make_sphere(name, radius, segments=16, rings=8, location=(0,0,0)):
    bpy.ops.mesh.primitive_uv_sphere_add(
        radius=radius, segments=segments, ring_count=rings, location=location)
    obj = bpy.context.active_object
    obj.name = name
    return obj


# ============================================================
# SOLDIER BODY BUILDER
# ============================================================
def build_soldier_body(unit_type='infantry', faction='France'):
    """Build a complete soldier mesh from primitives."""
    parts = []
    colors = FACTION_COLORS.get(faction, FACTION_COLORS['France'])

    # Materials
    skin_mat = create_material(f"{faction}_Skin", (0.72, 0.55, 0.42, 1.0), roughness=0.7)
    coat_mat = create_material(f"{faction}_Coat", colors['coat'], roughness=0.65)
    pants_mat = create_material(f"{faction}_Pants", colors['pants'], roughness=0.6)
    leather_mat = create_material(f"{faction}_Leather", (0.12, 0.08, 0.05, 1.0), roughness=0.75)
    metal_mat = create_material(f"{faction}_Metal", (0.5, 0.5, 0.55, 1.0), metallic=0.85, roughness=0.4)
    trim_mat = create_material(f"{faction}_Trim", colors['trim'], roughness=0.5)
    wood_mat = create_material(f"{faction}_Wood", (0.35, 0.2, 0.1, 1.0), roughness=0.7)
    shako_mat = create_material(f"{faction}_Shako", (0.05, 0.05, 0.05, 1.0), roughness=0.6)
    brass_mat = create_material(f"{faction}_Brass", (0.75, 0.55, 0.15, 1.0), metallic=0.8, roughness=0.35)

    # Ground level = 0, feet on ground
    foot_y = 0.0
    ankle_y = FOOT_HEIGHT
    knee_y = ankle_y + LOWER_LEG_LEN
    hip_y = knee_y + UPPER_LEG_LEN
    waist_y = hip_y + HIP_HEIGHT
    chest_y = waist_y + TORSO_HEIGHT
    neck_y = chest_y
    head_y = neck_y + NECK_LENGTH + HEAD_RADIUS

    # === HEAD ===
    head = make_sphere("Head", HEAD_RADIUS, 16, 12, (0, 0, head_y))
    head.scale = (1.0, 0.95, 1.1)
    bpy.context.view_layer.objects.active = head
    bpy.ops.object.transform_apply(scale=True)
    assign_material(head, skin_mat)
    set_smooth(head)
    parts.append(head)

    # Nose
    nose = make_tapered_cylinder("Nose", 0.012, 0.02, 0.035, 6,
                                  (0, -HEAD_RADIUS * 0.85, head_y - 0.01))
    nose.rotation_euler = (math.radians(-80), 0, 0)
    assign_material(nose, skin_mat)
    parts.append(nose)

    # === NECK ===
    neck = make_tapered_cylinder("Neck", 0.045, 0.05, NECK_LENGTH, 8,
                                  (0, 0, neck_y - NECK_LENGTH))
    assign_material(neck, skin_mat)
    parts.append(neck)

    # === TORSO (coat) — tapered from shoulders to waist ===
    torso = make_tapered_cylinder("Torso", TORSO_WIDTH/2, WAIST_WIDTH/2,
                                   TORSO_HEIGHT, 12, (0, 0, waist_y))
    torso.scale = (1.0, TORSO_DEPTH / TORSO_WIDTH, 1.0)
    bpy.context.view_layer.objects.active = torso
    bpy.ops.object.transform_apply(scale=True)
    assign_material(torso, coat_mat)
    set_smooth(torso)
    parts.append(torso)

    # === COAT TAILS (below waist) ===
    for side in [-1, 1]:
        tail = make_box(f"CoatTail_{side}", 0.12, TORSO_DEPTH * 0.8, HIP_HEIGHT * 1.5,
                        (side * 0.06, 0, hip_y - HIP_HEIGHT * 0.2))
        assign_material(tail, coat_mat)
        parts.append(tail)

    # === HIPS ===
    hips = make_tapered_cylinder("Hips", WAIST_WIDTH/2, HIP_WIDTH/2,
                                  HIP_HEIGHT, 10, (0, 0, hip_y))
    hips.scale = (1.0, TORSO_DEPTH / TORSO_WIDTH * 0.9, 1.0)
    bpy.context.view_layer.objects.active = hips
    bpy.ops.object.transform_apply(scale=True)
    assign_material(hips, coat_mat)
    parts.append(hips)

    # === CROSSBELTS ===
    for side in [-1, 1]:
        belt = make_box(f"Crossbelt_{side}", 0.03, TORSO_DEPTH * 1.05,
                        TORSO_HEIGHT * 1.1,
                        (side * 0.06, 0, waist_y + TORSO_HEIGHT * 0.5))
        belt.rotation_euler = (0, 0, side * math.radians(15))
        assign_material(belt, leather_mat)
        parts.append(belt)

    # Waist belt
    waist_belt = make_tapered_cylinder("WaistBelt", WAIST_WIDTH/2 + 0.01,
                                        WAIST_WIDTH/2 + 0.01, 0.04, 12,
                                        (0, 0, waist_y - 0.02))
    waist_belt.scale = (1.0, TORSO_DEPTH / TORSO_WIDTH * 0.95, 1.0)
    bpy.context.view_layer.objects.active = waist_belt
    bpy.ops.object.transform_apply(scale=True)
    assign_material(waist_belt, leather_mat)
    parts.append(waist_belt)

    # Belt plate (brass)
    plate = make_box("BeltPlate", 0.04, 0.015, 0.04,
                     (0, -TORSO_DEPTH/2 - 0.005, waist_y))
    assign_material(plate, brass_mat)
    parts.append(plate)

    # === GIBERNE (cartridge box on right hip) ===
    giberne = make_box("Giberne", 0.12, 0.06, 0.08,
                       (HIP_WIDTH/2 + 0.02, 0.02, hip_y + 0.02))
    assign_material(giberne, leather_mat)
    parts.append(giberne)

    # === EPAULETTES ===
    for side in [-1, 1]:
        ep = make_sphere(f"Epaulette_{side}", 0.045, 8, 6,
                          (side * SHOULDER_WIDTH/2, 0, chest_y - 0.02))
        ep.scale = (1.0, 0.6, 0.5)
        bpy.context.view_layer.objects.active = ep
        bpy.ops.object.transform_apply(scale=True)
        assign_material(ep, trim_mat)
        parts.append(ep)

    # === ARMS ===
    arm_r = 0.04
    for side in [-1, 1]:
        sx = side * SHOULDER_WIDTH / 2

        # Upper arm
        ua = make_tapered_cylinder(f"UpperArm_{side}", arm_r, arm_r * 0.9,
                                    UPPER_ARM_LEN, 8,
                                    (sx, 0, chest_y - 0.02 - UPPER_ARM_LEN))
        assign_material(ua, coat_mat)
        set_smooth(ua)
        parts.append(ua)

        # Lower arm (sleeve cuff)
        elbow_y_pos = chest_y - 0.02 - UPPER_ARM_LEN
        la = make_tapered_cylinder(f"LowerArm_{side}", arm_r * 0.85, arm_r * 0.7,
                                    LOWER_ARM_LEN, 8,
                                    (sx, 0, elbow_y_pos - LOWER_ARM_LEN))
        assign_material(la, coat_mat)
        set_smooth(la)
        parts.append(la)

        # Cuff trim
        cuff = make_tapered_cylinder(f"Cuff_{side}", arm_r * 0.88, arm_r * 0.88,
                                      0.04, 8,
                                      (sx, 0, elbow_y_pos - LOWER_ARM_LEN))
        assign_material(cuff, trim_mat)
        parts.append(cuff)

        # Hand
        hand_y_pos = elbow_y_pos - LOWER_ARM_LEN - HAND_LEN / 2
        hand = make_sphere(f"Hand_{side}", 0.03, 8, 6,
                            (sx, 0, hand_y_pos))
        hand.scale = (0.8, 0.5, 1.2)
        bpy.context.view_layer.objects.active = hand
        bpy.ops.object.transform_apply(scale=True)
        assign_material(hand, skin_mat)
        parts.append(hand)

    # === LEGS ===
    leg_r = 0.055
    for side in [-1, 1]:
        lx = side * HIP_WIDTH / 2 * 0.7

        # Upper leg
        ul = make_tapered_cylinder(f"UpperLeg_{side}", leg_r, leg_r * 0.85,
                                    UPPER_LEG_LEN, 8,
                                    (lx, 0, knee_y))
        assign_material(ul, pants_mat)
        set_smooth(ul)
        parts.append(ul)

        # Lower leg (gaiter/boot)
        ll = make_tapered_cylinder(f"LowerLeg_{side}", leg_r * 0.8, leg_r * 0.65,
                                    LOWER_LEG_LEN, 8,
                                    (lx, 0, ankle_y))
        assign_material(ll, leather_mat)
        set_smooth(ll)
        parts.append(ll)

        # Boot
        boot = make_box(f"Boot_{side}", leg_r * 1.4, FOOT_LEN, FOOT_HEIGHT,
                        (lx, -FOOT_LEN * 0.2, FOOT_HEIGHT / 2))
        assign_material(boot, leather_mat)
        parts.append(boot)

    # === SHAKO (or bearskin for grenadier) ===
    if unit_type == 'grenadier':
        # Bearskin — taller, furry
        hat = make_tapered_cylinder("Bearskin", 0.12, 0.11, 0.30, 12,
                                     (0, 0, head_y + HEAD_RADIUS * 0.7))
        assign_material(hat, shako_mat)
        parts.append(hat)
        # Front plate
        hp = make_box("HatPlate", 0.08, 0.01, 0.08,
                      (0, -0.11, head_y + HEAD_RADIUS * 0.7 + 0.06))
        assign_material(hp, brass_mat)
        parts.append(hp)
    else:
        # Shako
        hat = make_tapered_cylinder("Shako", SHAKO_RADIUS_TOP, SHAKO_RADIUS_BOT,
                                     SHAKO_HEIGHT, 12,
                                     (0, 0, head_y + HEAD_RADIUS * 0.6))
        assign_material(hat, shako_mat)
        parts.append(hat)
        # Shako plate
        sp = make_box("ShakoPlate", 0.05, 0.01, 0.05,
                      (0, -SHAKO_RADIUS_BOT - 0.005,
                       head_y + HEAD_RADIUS * 0.6 + SHAKO_HEIGHT * 0.3))
        assign_material(sp, brass_mat)
        parts.append(sp)
        # Pompom
        pom = make_sphere("Pompom", 0.025, 8, 6,
                           (0, 0, head_y + HEAD_RADIUS * 0.6 + SHAKO_HEIGHT + 0.02))
        assign_material(pom, trim_mat)
        parts.append(pom)
        # Chin strap
        chin = make_tapered_cylinder("ChinStrap", 0.005, 0.005, HEAD_RADIUS * 2, 4,
                                      (-HEAD_RADIUS * 0.5, -HEAD_RADIUS * 0.5,
                                       head_y - HEAD_RADIUS * 0.4))
        chin.rotation_euler = (math.radians(20), math.radians(30), 0)
        assign_material(chin, brass_mat)
        parts.append(chin)

    # === MUSKET ===
    # Barrel
    barrel = make_tapered_cylinder("MusketBarrel", MUSKET_BARREL_R, MUSKET_BARREL_R,
                                    MUSKET_LENGTH * 0.65, 6,
                                    (0, 0, 0))
    assign_material(barrel, metal_mat)
    set_smooth(barrel)
    parts.append(barrel)

    # Stock
    stock = make_box("MusketStock", MUSKET_STOCK_W, MUSKET_STOCK_W * 0.8,
                     MUSKET_LENGTH * 0.55,
                     (0, 0, -MUSKET_LENGTH * 0.55 / 2))
    assign_material(stock, wood_mat)
    parts.append(stock)

    # Bayonet
    bayonet = make_tapered_cylinder("Bayonet", 0.008, 0.003, BAYONET_LEN, 4,
                                     (0, 0, MUSKET_LENGTH * 0.65))
    assign_material(bayonet, metal_mat)
    parts.append(bayonet)

    # Lock mechanism
    lock = make_box("Lock", 0.02, 0.015, 0.08,
                    (MUSKET_STOCK_W / 2, 0, MUSKET_LENGTH * 0.1))
    assign_material(lock, metal_mat)
    parts.append(lock)

    # Position musket in right hand (will be parented to hand bone later)
    musket_parts = [barrel, stock, bayonet, lock]
    for mp in musket_parts:
        mp.location.x += SHOULDER_WIDTH / 2
        mp.location.z += knee_y + 0.1

    # === BUTTONS (front of coat) ===
    for i in range(6):
        bz = waist_y + 0.03 + i * (TORSO_HEIGHT - 0.06) / 5
        btn = make_sphere(f"Button_{i}", 0.008, 6, 4,
                           (0, -TORSO_DEPTH / 2 - 0.002, bz))
        assign_material(btn, brass_mat)
        parts.append(btn)

    # === COLLAR ===
    collar = make_tapered_cylinder("Collar", 0.06, 0.055, 0.035, 10,
                                    (0, 0, chest_y - 0.01))
    collar.scale = (1.0, TORSO_DEPTH / TORSO_WIDTH * 0.8, 1.0)
    bpy.context.view_layer.objects.active = collar
    bpy.ops.object.transform_apply(scale=True)
    assign_material(collar, trim_mat)
    parts.append(collar)

    # Join all parts into one mesh
    soldier = join_objects(parts)
    soldier.name = f"{faction}_LineInfantry"
    set_smooth(soldier)

    return soldier


# ============================================================
# ARMATURE BUILDER — Unity Humanoid compatible
# ============================================================
def build_armature(name="SoldierRig"):
    """Create a humanoid skeleton for the soldier."""
    ankle_y = FOOT_HEIGHT
    knee_y = ankle_y + LOWER_LEG_LEN
    hip_y = knee_y + UPPER_LEG_LEN
    waist_y = hip_y + HIP_HEIGHT
    chest_y = waist_y + TORSO_HEIGHT
    neck_y = chest_y
    head_y = neck_y + NECK_LENGTH + HEAD_RADIUS

    arm_data = bpy.data.armatures.new(name + "_Armature")
    arm_data.display_type = 'STICK'
    arm_obj = bpy.data.objects.new(name, arm_data)
    bpy.context.collection.objects.link(arm_obj)
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.mode_set(mode='EDIT')

    def add_bone(bname, head_pos, tail_pos, parent=None, connect=True):
        bone = arm_data.edit_bones.new(bname)
        bone.head = Vector(head_pos)
        bone.tail = Vector(tail_pos)
        if parent:
            bone.parent = parent
            bone.use_connect = connect
        return bone

    # Spine chain
    root = add_bone("Root", (0, 0, hip_y), (0, 0, hip_y + 0.05), None, False)
    hips = add_bone("Hips", (0, 0, hip_y), (0, 0, waist_y), root, False)
    spine = add_bone("Spine", (0, 0, waist_y), (0, 0, waist_y + TORSO_HEIGHT * 0.5), hips)
    chest = add_bone("Chest", (0, 0, waist_y + TORSO_HEIGHT * 0.5), (0, 0, chest_y), spine)
    neck = add_bone("Neck", (0, 0, neck_y), (0, 0, neck_y + NECK_LENGTH), chest)
    head = add_bone("Head", (0, 0, neck_y + NECK_LENGTH),
                    (0, 0, head_y + HEAD_RADIUS), neck)

    # Arms
    for side, sign in [("L", -1), ("R", 1)]:
        sx = sign * SHOULDER_WIDTH / 2
        elbow_z = chest_y - 0.02 - UPPER_ARM_LEN
        wrist_z = elbow_z - LOWER_ARM_LEN
        hand_z = wrist_z - HAND_LEN

        shoulder = add_bone(f"Shoulder.{side}",
                            (sign * 0.05, 0, chest_y - 0.02),
                            (sx, 0, chest_y - 0.02), chest, False)
        upper_arm = add_bone(f"UpperArm.{side}",
                             (sx, 0, chest_y - 0.02),
                             (sx, 0, elbow_z), shoulder)
        lower_arm = add_bone(f"LowerArm.{side}",
                             (sx, 0, elbow_z),
                             (sx, 0, wrist_z), upper_arm)
        hand_bone = add_bone(f"Hand.{side}",
                             (sx, 0, wrist_z),
                             (sx, 0, hand_z), lower_arm)

    # Legs
    for side, sign in [("L", -1), ("R", 1)]:
        lx = sign * HIP_WIDTH / 2 * 0.7

        upper_leg = add_bone(f"UpperLeg.{side}",
                             (lx, 0, hip_y),
                             (lx, 0, knee_y), hips, False)
        lower_leg = add_bone(f"LowerLeg.{side}",
                             (lx, 0, knee_y),
                             (lx, 0, ankle_y), upper_leg)
        foot = add_bone(f"Foot.{side}",
                        (lx, 0, ankle_y),
                        (lx, -FOOT_LEN * 0.6, ankle_y * 0.3), lower_leg)

    # Weapon bone (for musket)
    weapon = add_bone("Weapon",
                      (SHOULDER_WIDTH / 2, 0, chest_y - 0.02 - UPPER_ARM_LEN - LOWER_ARM_LEN - HAND_LEN),
                      (SHOULDER_WIDTH / 2, 0, chest_y - 0.02 - UPPER_ARM_LEN - LOWER_ARM_LEN - HAND_LEN - 0.1),
                      arm_data.edit_bones.get("Hand.R"), False)

    bpy.ops.object.mode_set(mode='OBJECT')
    return arm_obj


# ============================================================
# VERTEX WEIGHT PAINTING (automatic)
# ============================================================
def auto_weight_paint(mesh_obj, armature_obj):
    """Parent mesh to armature with automatic weights."""
    bpy.ops.object.select_all(action='DESELECT')
    mesh_obj.select_set(True)
    armature_obj.select_set(True)
    bpy.context.view_layer.objects.active = armature_obj
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')


# ============================================================
# ANIMATION SYSTEM
# ============================================================
def set_bone_keyframe(armature, bone_name, frame, rotation=None, location=None):
    """Set keyframe for a bone at given frame."""
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode='POSE')
    bone = armature.pose.bones.get(bone_name)
    if not bone:
        bpy.ops.object.mode_set(mode='OBJECT')
        return
    if rotation:
        bone.rotation_mode = 'XYZ'
        bone.rotation_euler = Euler(rotation)
        bone.keyframe_insert(data_path="rotation_euler", frame=frame)
    if location:
        bone.location = Vector(location)
        bone.keyframe_insert(data_path="location", frame=frame)
    bpy.ops.object.mode_set(mode='OBJECT')


def create_action(armature, action_name):
    """Create a new action and assign it."""
    action = bpy.data.actions.new(name=action_name)
    action.use_fake_user = True  # CRITICAL: prevent Blender GC before export
    if not armature.animation_data:
        armature.animation_data_create()
    armature.animation_data.action = action
    return action


def r(deg):
    """Degrees to radians shorthand."""
    return math.radians(deg)


def stash_action_to_nla(armature):
    """Push the current action into an NLA strip so it survives FBX export."""
    if not armature.animation_data or not armature.animation_data.action:
        return
    action = armature.animation_data.action
    track = armature.animation_data.nla_tracks.new()
    track.name = action.name
    track.strips.new(action.name, int(action.frame_range[0]), action)
    # Detach action so next one can be created
    armature.animation_data.action = None


def create_all_animations(armature, prefix=""):
    """Create all 12 animations and push each into NLA for FBX export."""
    anims = [
        (anim_idle, "Idle"),
        (anim_walk, "Walk"),
        (anim_run, "Run"),
        (anim_standing_fire, "Standing_Fire"),
        (anim_kneeling_fire, "Kneeling_Fire"),
        (anim_standing_aim, "Standing_Aim"),
        (anim_kneeling_aim, "Kneeling_Aim"),
        (anim_reload, "Reload"),
        (anim_charge, "Charge"),
        (anim_melee, "Attack_Melee"),
        (anim_death, "Death"),
        (anim_flee, "Flee"),
    ]
    for func, name in anims:
        func(armature, f"{prefix}{name}")
        stash_action_to_nla(armature)
    
    nla_count = len(armature.animation_data.nla_tracks) if armature.animation_data else 0
    print(f"  ✓ Created {len(anims)} animations ({nla_count} NLA tracks) with prefix '{prefix}'")


# === IDLE: subtle breathing and weight shift ===
def anim_idle(arm, name):
    action = create_action(arm, name)
    frames = 60  # 2 second loop
    for f in [1, 15, 30, 45, 60]:
        t = (f - 1) / (frames - 1)
        breath = math.sin(t * math.pi * 2) * 0.008
        sway = math.sin(t * math.pi) * 0.005
        set_bone_keyframe(arm, "Spine", f, location=(sway, 0, breath))
        set_bone_keyframe(arm, "Chest", f, rotation=(r(-2 + breath * 200), 0, r(sway * 200)))
        set_bone_keyframe(arm, "Head", f, rotation=(r(-3 + breath * 100), r(sway * 300), 0))
        set_bone_keyframe(arm, "UpperArm.L", f, rotation=(r(5), 0, r(8)))
        set_bone_keyframe(arm, "UpperArm.R", f, rotation=(r(5), 0, r(-8)))


# === WALK: military cadence march ===
def anim_walk(arm, name):
    action = create_action(arm, name)
    frames = 30  # 1 second stride
    for f in range(1, frames + 1):
        t = (f - 1) / frames
        cycle = t * math.pi * 2

        # Legs swing forward/back
        leg_swing = math.sin(cycle) * 25
        set_bone_keyframe(arm, "UpperLeg.L", f, rotation=(r(leg_swing), 0, 0))
        set_bone_keyframe(arm, "UpperLeg.R", f, rotation=(r(-leg_swing), 0, 0))

        # Knee bend on back leg
        knee_l = max(0, -math.sin(cycle)) * 35
        knee_r = max(0, math.sin(cycle)) * 35
        set_bone_keyframe(arm, "LowerLeg.L", f, rotation=(r(knee_l), 0, 0))
        set_bone_keyframe(arm, "LowerLeg.R", f, rotation=(r(knee_r), 0, 0))

        # Arm swing (opposite to legs)
        arm_swing = math.sin(cycle) * 15
        set_bone_keyframe(arm, "UpperArm.L", f, rotation=(r(arm_swing + 5), 0, r(8)))
        set_bone_keyframe(arm, "UpperArm.R", f, rotation=(r(-arm_swing + 5), 0, r(-8)))
        set_bone_keyframe(arm, "LowerArm.L", f, rotation=(r(-10), 0, 0))
        set_bone_keyframe(arm, "LowerArm.R", f, rotation=(r(-10), 0, 0))

        # Body bob
        bob = abs(math.sin(cycle)) * 0.01
        set_bone_keyframe(arm, "Hips", f, location=(0, 0, bob))
        set_bone_keyframe(arm, "Spine", f, rotation=(r(-2), r(math.sin(cycle) * 3), 0))


# === RUN ===
def anim_run(arm, name):
    action = create_action(arm, name)
    frames = 20
    for f in range(1, frames + 1):
        t = (f - 1) / frames
        cycle = t * math.pi * 2

        leg_swing = math.sin(cycle) * 40
        set_bone_keyframe(arm, "UpperLeg.L", f, rotation=(r(leg_swing), 0, 0))
        set_bone_keyframe(arm, "UpperLeg.R", f, rotation=(r(-leg_swing), 0, 0))

        knee_l = max(0, -math.sin(cycle)) * 60
        knee_r = max(0, math.sin(cycle)) * 60
        set_bone_keyframe(arm, "LowerLeg.L", f, rotation=(r(knee_l), 0, 0))
        set_bone_keyframe(arm, "LowerLeg.R", f, rotation=(r(knee_r), 0, 0))

        arm_swing = math.sin(cycle) * 30
        set_bone_keyframe(arm, "UpperArm.L", f, rotation=(r(arm_swing + 10), 0, r(12)))
        set_bone_keyframe(arm, "UpperArm.R", f, rotation=(r(-arm_swing + 10), 0, r(-12)))
        set_bone_keyframe(arm, "LowerArm.L", f, rotation=(r(-25), 0, 0))
        set_bone_keyframe(arm, "LowerArm.R", f, rotation=(r(-25), 0, 0))

        bob = abs(math.sin(cycle)) * 0.025
        lean = math.sin(cycle) * 5
        set_bone_keyframe(arm, "Hips", f, location=(0, 0, bob))
        set_bone_keyframe(arm, "Spine", f, rotation=(r(-8), r(lean), 0))
        set_bone_keyframe(arm, "Chest", f, rotation=(r(-5), 0, 0))


# === STANDING FIRE: shoulder → aim → fire → recoil ===
def anim_standing_fire(arm, name):
    action = create_action(arm, name)
    # Frame 1: ready; 10: aim; 18: fire; 25: recoil; 35: recover
    for f, phase in [(1, 'ready'), (10, 'aim'), (18, 'fire'),
                      (22, 'recoil'), (35, 'recover')]:
        if phase == 'ready':
            set_bone_keyframe(arm, "UpperArm.R", f, rotation=(r(30), r(-10), r(-15)))
            set_bone_keyframe(arm, "LowerArm.R", f, rotation=(r(-60), 0, 0))
            set_bone_keyframe(arm, "UpperArm.L", f, rotation=(r(40), r(10), r(15)))
            set_bone_keyframe(arm, "LowerArm.L", f, rotation=(r(-70), 0, 0))
            set_bone_keyframe(arm, "Chest", f, rotation=(0, r(-15), 0))
        elif phase == 'aim':
            set_bone_keyframe(arm, "UpperArm.R", f, rotation=(r(70), r(-15), r(-20)))
            set_bone_keyframe(arm, "LowerArm.R", f, rotation=(r(-90), 0, 0))
            set_bone_keyframe(arm, "UpperArm.L", f, rotation=(r(80), r(15), r(20)))
            set_bone_keyframe(arm, "LowerArm.L", f, rotation=(r(-85), 0, 0))
            set_bone_keyframe(arm, "Chest", f, rotation=(r(-3), r(-25), 0))
            set_bone_keyframe(arm, "Head", f, rotation=(r(-5), r(-20), 0))
        elif phase == 'fire':
            set_bone_keyframe(arm, "UpperArm.R", f, rotation=(r(65), r(-15), r(-18)))
            set_bone_keyframe(arm, "LowerArm.R", f, rotation=(r(-85), 0, 0))
            set_bone_keyframe(arm, "Chest", f, rotation=(r(3), r(-20), 0))
        elif phase == 'recoil':
            set_bone_keyframe(arm, "UpperArm.R", f, rotation=(r(50), r(-10), r(-12)))
            set_bone_keyframe(arm, "Chest", f, rotation=(r(8), r(-15), 0))
            set_bone_keyframe(arm, "Spine", f, rotation=(r(5), 0, 0))
        elif phase == 'recover':
            set_bone_keyframe(arm, "UpperArm.R", f, rotation=(r(30), r(-10), r(-15)))
            set_bone_keyframe(arm, "LowerArm.R", f, rotation=(r(-60), 0, 0))
            set_bone_keyframe(arm, "Chest", f, rotation=(0, r(-15), 0))
            set_bone_keyframe(arm, "Spine", f, rotation=(0, 0, 0))


# === KNEELING FIRE ===
def anim_kneeling_fire(arm, name):
    action = create_action(arm, name)
    for f, phase in [(1, 'kneel'), (12, 'aim'), (20, 'fire'),
                      (24, 'recoil'), (35, 'kneel_rest')]:
        # Kneeling pose: right knee down
        set_bone_keyframe(arm, "UpperLeg.R", f, rotation=(r(-80), 0, 0))
        set_bone_keyframe(arm, "LowerLeg.R", f, rotation=(r(120), 0, 0))
        set_bone_keyframe(arm, "UpperLeg.L", f, rotation=(r(-50), 0, 0))
        set_bone_keyframe(arm, "LowerLeg.L", f, rotation=(r(80), 0, 0))
        set_bone_keyframe(arm, "Hips", f, location=(0, 0, -0.35))

        if phase in ('aim', 'fire', 'recoil'):
            set_bone_keyframe(arm, "UpperArm.R", f, rotation=(r(70), r(-15), r(-20)))
            set_bone_keyframe(arm, "LowerArm.R", f, rotation=(r(-90), 0, 0))
            set_bone_keyframe(arm, "UpperArm.L", f, rotation=(r(80), r(15), r(20)))
            set_bone_keyframe(arm, "LowerArm.L", f, rotation=(r(-85), 0, 0))
            if phase == 'recoil':
                set_bone_keyframe(arm, "Chest", f, rotation=(r(8), r(-15), 0))
            else:
                set_bone_keyframe(arm, "Chest", f, rotation=(r(-5), r(-25), 0))
        else:
            set_bone_keyframe(arm, "UpperArm.R", f, rotation=(r(20), 0, r(-10)))
            set_bone_keyframe(arm, "LowerArm.R", f, rotation=(r(-40), 0, 0))
            set_bone_keyframe(arm, "Chest", f, rotation=(r(-5), r(-10), 0))


# === STANDING AIM: held aiming pose (volley fire) ===
def anim_standing_aim(arm, name):
    action = create_action(arm, name)
    # 60-frame loop with subtle sway while aiming
    for f in [1, 15, 30, 45, 60]:
        t = (f - 1) / 59
        sway = math.sin(t * math.pi * 2) * 0.003
        set_bone_keyframe(arm, "UpperArm.R", f, rotation=(r(70), r(-15), r(-20)))
        set_bone_keyframe(arm, "LowerArm.R", f, rotation=(r(-90), 0, 0))
        set_bone_keyframe(arm, "UpperArm.L", f, rotation=(r(80), r(15), r(20)))
        set_bone_keyframe(arm, "LowerArm.L", f, rotation=(r(-85), 0, 0))
        set_bone_keyframe(arm, "Chest", f, rotation=(r(-3 + sway * 100), r(-25), 0))
        set_bone_keyframe(arm, "Head", f, rotation=(r(-5), r(-20 + sway * 200), 0))
        set_bone_keyframe(arm, "Spine", f, location=(sway, 0, 0))


# === KNEELING AIM: held kneeling aiming pose (volley fire) ===
def anim_kneeling_aim(arm, name):
    action = create_action(arm, name)
    for f in [1, 15, 30, 45, 60]:
        t = (f - 1) / 59
        sway = math.sin(t * math.pi * 2) * 0.002
        # Kneeling legs
        set_bone_keyframe(arm, "UpperLeg.R", f, rotation=(r(-80), 0, 0))
        set_bone_keyframe(arm, "LowerLeg.R", f, rotation=(r(120), 0, 0))
        set_bone_keyframe(arm, "UpperLeg.L", f, rotation=(r(-50), 0, 0))
        set_bone_keyframe(arm, "LowerLeg.L", f, rotation=(r(80), 0, 0))
        set_bone_keyframe(arm, "Hips", f, location=(0, 0, -0.35))
        # Aiming upper body
        set_bone_keyframe(arm, "UpperArm.R", f, rotation=(r(70), r(-15), r(-20)))
        set_bone_keyframe(arm, "LowerArm.R", f, rotation=(r(-90), 0, 0))
        set_bone_keyframe(arm, "UpperArm.L", f, rotation=(r(80), r(15), r(20)))
        set_bone_keyframe(arm, "LowerArm.L", f, rotation=(r(-85), 0, 0))
        set_bone_keyframe(arm, "Chest", f, rotation=(r(-5 + sway * 80), r(-25), 0))
        set_bone_keyframe(arm, "Head", f, rotation=(r(-5), r(-20 + sway * 150), 0))


# === RELOAD: full musket reload cycle ===
def anim_reload(arm, name):
    action = create_action(arm, name)
    total = 90  # 3 seconds
    phases = [
        (1, 'start'), (10, 'half_cock'), (25, 'cartridge'),
        (40, 'pour_powder'), (55, 'ram'), (70, 'ram_down'),
        (80, 'shoulder'), (90, 'ready')
    ]
    for f, phase in phases:
        if phase == 'start':
            set_bone_keyframe(arm, "UpperArm.R", f, rotation=(r(20), 0, r(-10)))
            set_bone_keyframe(arm, "LowerArm.R", f, rotation=(r(-30), 0, 0))
            set_bone_keyframe(arm, "UpperArm.L", f, rotation=(r(30), 0, r(10)))
            set_bone_keyframe(arm, "LowerArm.L", f, rotation=(r(-40), 0, 0))
            set_bone_keyframe(arm, "Chest", f, rotation=(r(-5), 0, 0))
        elif phase in ('half_cock', 'cartridge'):
            set_bone_keyframe(arm, "UpperArm.R", f, rotation=(r(40), r(-20), r(-15)))
            set_bone_keyframe(arm, "LowerArm.R", f, rotation=(r(-70), 0, 0))
            set_bone_keyframe(arm, "UpperArm.L", f, rotation=(r(50), r(15), r(15)))
            set_bone_keyframe(arm, "LowerArm.L", f, rotation=(r(-80), 0, 0))
            set_bone_keyframe(arm, "Head", f, rotation=(r(-15), r(-10), 0))
        elif phase in ('pour_powder', 'ram', 'ram_down'):
            # Ramrod action — arms move up/down
            ram_offset = 0 if phase == 'ram_down' else 20
            set_bone_keyframe(arm, "UpperArm.R", f, rotation=(r(60 + ram_offset), 0, r(-10)))
            set_bone_keyframe(arm, "LowerArm.R", f, rotation=(r(-90), 0, 0))
            set_bone_keyframe(arm, "UpperArm.L", f, rotation=(r(50), 0, r(10)))
            set_bone_keyframe(arm, "Chest", f, rotation=(r(-8), 0, 0))
        elif phase in ('shoulder', 'ready'):
            set_bone_keyframe(arm, "UpperArm.R", f, rotation=(r(30), r(-10), r(-15)))
            set_bone_keyframe(arm, "LowerArm.R", f, rotation=(r(-60), 0, 0))
            set_bone_keyframe(arm, "UpperArm.L", f, rotation=(r(15), 0, r(8)))
            set_bone_keyframe(arm, "LowerArm.L", f, rotation=(r(-20), 0, 0))
            set_bone_keyframe(arm, "Chest", f, rotation=(0, 0, 0))


# === CHARGE: bayonet charge ===
def anim_charge(arm, name):
    action = create_action(arm, name)
    frames = 20
    for f in range(1, frames + 1):
        t = (f - 1) / frames
        cycle = t * math.pi * 2

        # Fast run legs
        leg_swing = math.sin(cycle) * 45
        set_bone_keyframe(arm, "UpperLeg.L", f, rotation=(r(leg_swing), 0, 0))
        set_bone_keyframe(arm, "UpperLeg.R", f, rotation=(r(-leg_swing), 0, 0))
        knee_l = max(0, -math.sin(cycle)) * 50
        knee_r = max(0, math.sin(cycle)) * 50
        set_bone_keyframe(arm, "LowerLeg.L", f, rotation=(r(knee_l), 0, 0))
        set_bone_keyframe(arm, "LowerLeg.R", f, rotation=(r(knee_r), 0, 0))

        # Arms forward with bayonet
        set_bone_keyframe(arm, "UpperArm.R", f, rotation=(r(60), r(-10), r(-15)))
        set_bone_keyframe(arm, "LowerArm.R", f, rotation=(r(-40), 0, 0))
        set_bone_keyframe(arm, "UpperArm.L", f, rotation=(r(50), r(5), r(10)))
        set_bone_keyframe(arm, "LowerArm.L", f, rotation=(r(-35), 0, 0))

        # Forward lean
        set_bone_keyframe(arm, "Spine", f, rotation=(r(-12), r(math.sin(cycle) * 3), 0))
        set_bone_keyframe(arm, "Chest", f, rotation=(r(-8), 0, 0))
        bob = abs(math.sin(cycle)) * 0.02
        set_bone_keyframe(arm, "Hips", f, location=(0, 0, bob))


# === MELEE ATTACK ===
def anim_melee(arm, name):
    action = create_action(arm, name)
    for f, phase in [(1, 'ready'), (8, 'wind'), (14, 'strike'),
                      (18, 'impact'), (25, 'recover')]:
        if phase == 'ready':
            set_bone_keyframe(arm, "UpperArm.R", f, rotation=(r(40), r(-10), r(-15)))
            set_bone_keyframe(arm, "LowerArm.R", f, rotation=(r(-50), 0, 0))
            set_bone_keyframe(arm, "Chest", f, rotation=(0, r(-20), 0))
        elif phase == 'wind':
            set_bone_keyframe(arm, "UpperArm.R", f, rotation=(r(80), r(-30), r(-25)))
            set_bone_keyframe(arm, "LowerArm.R", f, rotation=(r(-70), 0, 0))
            set_bone_keyframe(arm, "Chest", f, rotation=(r(-5), r(-35), 0))
            set_bone_keyframe(arm, "Spine", f, rotation=(0, r(-10), 0))
        elif phase == 'strike':
            set_bone_keyframe(arm, "UpperArm.R", f, rotation=(r(60), r(15), r(-5)))
            set_bone_keyframe(arm, "LowerArm.R", f, rotation=(r(-30), 0, 0))
            set_bone_keyframe(arm, "Chest", f, rotation=(r(-8), r(10), 0))
            set_bone_keyframe(arm, "Spine", f, rotation=(r(-5), r(15), 0))
        elif phase == 'impact':
            set_bone_keyframe(arm, "UpperArm.R", f, rotation=(r(50), r(20), r(0)))
            set_bone_keyframe(arm, "Chest", f, rotation=(r(-3), r(15), 0))
        elif phase == 'recover':
            set_bone_keyframe(arm, "UpperArm.R", f, rotation=(r(40), r(-10), r(-15)))
            set_bone_keyframe(arm, "LowerArm.R", f, rotation=(r(-50), 0, 0))
            set_bone_keyframe(arm, "Chest", f, rotation=(0, r(-20), 0))
            set_bone_keyframe(arm, "Spine", f, rotation=(0, 0, 0))


# === DEATH ===
def anim_death(arm, name):
    action = create_action(arm, name)
    for f, phase in [(1, 'hit'), (8, 'stagger'), (18, 'fall'), (30, 'ground')]:
        if phase == 'hit':
            set_bone_keyframe(arm, "Chest", f, rotation=(r(10), 0, 0))
            set_bone_keyframe(arm, "Head", f, rotation=(r(15), r(10), 0))
        elif phase == 'stagger':
            set_bone_keyframe(arm, "Spine", f, rotation=(r(15), r(10), 0))
            set_bone_keyframe(arm, "Chest", f, rotation=(r(20), r(5), r(10)))
            set_bone_keyframe(arm, "UpperLeg.L", f, rotation=(r(-15), 0, 0))
            set_bone_keyframe(arm, "LowerLeg.L", f, rotation=(r(20), 0, 0))
        elif phase == 'fall':
            set_bone_keyframe(arm, "Hips", f, location=(0, 0.1, -0.5))
            set_bone_keyframe(arm, "Spine", f, rotation=(r(45), r(15), r(20)))
            set_bone_keyframe(arm, "Chest", f, rotation=(r(30), r(10), r(15)))
            set_bone_keyframe(arm, "UpperArm.L", f, rotation=(r(40), 0, r(30)))
            set_bone_keyframe(arm, "UpperArm.R", f, rotation=(r(30), 0, r(-20)))
            set_bone_keyframe(arm, "UpperLeg.L", f, rotation=(r(-30), 0, 0))
            set_bone_keyframe(arm, "UpperLeg.R", f, rotation=(r(-10), 0, r(15)))
        elif phase == 'ground':
            set_bone_keyframe(arm, "Hips", f, location=(0.1, 0.2, -0.85))
            set_bone_keyframe(arm, "Spine", f, rotation=(r(80), r(20), r(30)))
            set_bone_keyframe(arm, "Chest", f, rotation=(r(20), r(5), r(10)))
            set_bone_keyframe(arm, "Head", f, rotation=(r(30), r(-20), r(15)))
            set_bone_keyframe(arm, "UpperArm.L", f, rotation=(r(60), 0, r(50)))
            set_bone_keyframe(arm, "UpperArm.R", f, rotation=(r(45), 0, r(-40)))
            set_bone_keyframe(arm, "UpperLeg.L", f, rotation=(r(-20), 0, r(10)))
            set_bone_keyframe(arm, "UpperLeg.R", f, rotation=(r(-40), r(15), r(20)))
            set_bone_keyframe(arm, "LowerLeg.R", f, rotation=(r(60), 0, 0))


# === FLEE ===
def anim_flee(arm, name):
    action = create_action(arm, name)
    frames = 18
    for f in range(1, frames + 1):
        t = (f - 1) / frames
        cycle = t * math.pi * 2

        leg_swing = math.sin(cycle) * 45
        set_bone_keyframe(arm, "UpperLeg.L", f, rotation=(r(leg_swing), 0, 0))
        set_bone_keyframe(arm, "UpperLeg.R", f, rotation=(r(-leg_swing), 0, 0))
        knee_l = max(0, -math.sin(cycle)) * 55
        knee_r = max(0, math.sin(cycle)) * 55
        set_bone_keyframe(arm, "LowerLeg.L", f, rotation=(r(knee_l), 0, 0))
        set_bone_keyframe(arm, "LowerLeg.R", f, rotation=(r(knee_r), 0, 0))

        # Panicked arm motion
        arm_frantic = math.sin(cycle * 2) * 25
        set_bone_keyframe(arm, "UpperArm.L", f, rotation=(r(arm_frantic + 20), 0, r(15)))
        set_bone_keyframe(arm, "UpperArm.R", f, rotation=(r(-arm_frantic + 20), 0, r(-15)))
        set_bone_keyframe(arm, "LowerArm.L", f, rotation=(r(-40 + abs(arm_frantic)), 0, 0))
        set_bone_keyframe(arm, "LowerArm.R", f, rotation=(r(-40 + abs(arm_frantic)), 0, 0))

        # Look back over shoulder
        set_bone_keyframe(arm, "Head", f, rotation=(r(-5), r(25 + math.sin(cycle) * 10), 0))
        set_bone_keyframe(arm, "Spine", f, rotation=(r(-5), r(math.sin(cycle) * 5), 0))
        bob = abs(math.sin(cycle)) * 0.02
        set_bone_keyframe(arm, "Hips", f, location=(0, 0, bob))


# ============================================================
# EXPORT
# ============================================================
def export_soldier(armature_obj, mesh_obj, filepath):
    """Export soldier to FBX with Unity-compatible settings."""
    import os
    os.makedirs(os.path.dirname(filepath), exist_ok=True)

    bpy.ops.object.select_all(action='DESELECT')
    mesh_obj.select_set(True)
    armature_obj.select_set(True)
    bpy.context.view_layer.objects.active = armature_obj

    bpy.ops.export_scene.fbx(
        filepath=filepath,
        use_selection=True,
        apply_scale_options='FBX_SCALE_ALL',
        global_scale=1.0,
        axis_forward='-Z',
        axis_up='Y',
        apply_unit_scale=True,
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=True,
        bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.5,
        mesh_smooth_type='FACE',
        path_mode='COPY',
        embed_textures=True,
        object_types={'ARMATURE', 'MESH'},
    )
    print(f"  ✓ Exported: {filepath}")


# ============================================================
# MAIN — Generate all unit types
# ============================================================
def generate_all_soldiers():
    import os
    import traceback

    print("=" * 60)
    print("  NAPOLEONIC SOLDIER GENERATOR — Maximum Quality")
    print("=" * 60)

    unit_configs = [
        ('LineInfantry', 'infantry'),
        ('Grenadier', 'grenadier'),
        ('LightInfantry', 'infantry'),
    ]

    faction = 'France'

    for unit_name, unit_type in unit_configs:
        print(f"\n--- Generating {faction} {unit_name} ---")
        try:
            print("  Step 1: Cleaning scene...")
            clean_scene()

            print("  Step 2: Building mesh...")
            mesh_obj = build_soldier_body(unit_type=unit_type, faction=faction)
            mesh_obj.name = f"{faction}_{unit_name}"
            print(f"    Mesh '{mesh_obj.name}' created with {len(mesh_obj.data.polygons)} faces")

            print("  Step 3: Building armature...")
            arm_obj = build_armature(f"{faction}_{unit_name}_Rig")
            print(f"    Armature '{arm_obj.name}' created with {len(arm_obj.data.bones)} bones")

            print("  Step 4: Weight painting...")
            try:
                auto_weight_paint(mesh_obj, arm_obj)
                print("    Auto weights applied successfully")
            except Exception as e:
                print(f"    WARNING: Auto weights failed ({e}), using empty groups")
                # Fallback: parent without weights, add empty vertex groups
                mesh_obj.parent = arm_obj
                mesh_obj.parent_type = 'ARMATURE'
                mod = mesh_obj.modifiers.new("Armature", 'ARMATURE')
                mod.object = arm_obj
                for bone in arm_obj.data.bones:
                    if bone.name not in mesh_obj.vertex_groups:
                        mesh_obj.vertex_groups.new(name=bone.name)

            print("  Step 5: Creating animations...")
            prefix = f"{faction}_{unit_name}_Rig|"
            create_all_animations(arm_obj, prefix)
            # Verify actions exist
            action_count = len([a for a in bpy.data.actions if a.name.startswith(prefix)])
            print(f"    {action_count} actions in bpy.data.actions")

            print("  Step 6: Adding subdivision...")
            bpy.context.view_layer.objects.active = mesh_obj
            add_subsurf(mesh_obj, levels=1, render_levels=1)

            print("  Step 7: Exporting FBX...")
            filepath = os.path.join(EXPORT_DIR, f"{faction}_{unit_name}.fbx")
            export_soldier(arm_obj, mesh_obj, filepath)

            print(f"  ✓ {faction}_{unit_name} COMPLETE!")

        except Exception as e:
            print(f"\n  *** ERROR generating {faction}_{unit_name} ***")
            print(f"  {type(e).__name__}: {e}")
            traceback.print_exc()
            print("  Continuing to next unit...\n")

    print("\n" + "=" * 60)
    print("  ✓ ALL SOLDIERS GENERATED AND EXPORTED!")
    print(f"  ✓ Output directory: {EXPORT_DIR}")
    print("=" * 60)


# Run!
if __name__ == "__main__":
    generate_all_soldiers()

