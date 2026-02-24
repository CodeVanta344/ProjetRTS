"""
Napoleonic Soldier Generator - Blender Script
===============================================
Generates a low-poly (~800-1200 tris) Napoleonic soldier with:
  - Body, head, shako hat, backpack
  - Simple armature rig (Mixamo-compatible bone names)
  - Faction-based uniform colors (France blue, Britain red)
  - Export-ready for Unity FBX

Usage:
  1. Open Blender
  2. Open Scripting workspace
  3. Run this script (Alt+P)
  4. Soldiers are generated at origin
  5. Export as FBX with "Apply Transform" checked
"""

import bpy
import bmesh
import math
from mathutils import Vector, Matrix, Euler

# ============================================================
# CONFIGURATION
# ============================================================

FACTIONS = {
    "France": {
        "coat": (0.12, 0.18, 0.45, 1.0),       # Dark blue
        "pants": (0.85, 0.85, 0.82, 1.0),       # White
        "belt": (0.85, 0.85, 0.82, 1.0),        # White crossbelts
        "shako": (0.08, 0.08, 0.08, 1.0),       # Black shako
        "plume": (0.8, 0.1, 0.1, 1.0),          # Red plume
        "skin": (0.76, 0.6, 0.48, 1.0),         # Skin tone
        "boots": (0.06, 0.06, 0.06, 1.0),       # Black boots
        "cuffs": (0.7, 0.15, 0.15, 1.0),        # Red cuffs
        "epaulettes": (0.7, 0.15, 0.15, 1.0),   # Red epaulettes
    },
    "Britain": {
        "coat": (0.7, 0.12, 0.12, 1.0),         # Red coat
        "pants": (0.85, 0.85, 0.82, 1.0),        # White
        "belt": (0.85, 0.85, 0.82, 1.0),         # White crossbelts
        "shako": (0.08, 0.08, 0.08, 1.0),        # Black shako
        "plume": (0.85, 0.85, 0.82, 1.0),        # White plume
        "skin": (0.76, 0.6, 0.48, 1.0),
        "boots": (0.06, 0.06, 0.06, 1.0),
        "cuffs": (0.12, 0.18, 0.45, 1.0),        # Blue cuffs (facing color)
        "epaulettes": (0.85, 0.85, 0.82, 1.0),   # White epaulettes
    },
}

UNIT_TYPES = {
    "LineInfantry": {"height": 1.75, "width": 0.45, "has_musket": True, "has_bayonet": True},
    "Grenadier": {"height": 1.85, "width": 0.5, "has_musket": True, "has_bayonet": True},
    "LightInfantry": {"height": 1.7, "width": 0.4, "has_musket": True, "has_bayonet": False},
    "Cavalry": {"height": 1.8, "width": 0.45, "has_musket": False, "has_bayonet": False},
    "Artillery": {"height": 1.75, "width": 0.48, "has_musket": False, "has_bayonet": False},
}


# ============================================================
# MATERIAL HELPERS
# ============================================================

def create_material(name, color):
    """Create a simple material with the given RGBA color."""
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = color
        bsdf.inputs["Roughness"].default_value = 0.7
    return mat


def assign_material(obj, mat):
    """Assign a material to an object."""
    if obj.data.materials:
        obj.data.materials[0] = mat
    else:
        obj.data.materials.append(mat)


# ============================================================
# GEOMETRY HELPERS
# ============================================================

def create_box(name, size, location, material):
    """Create a box mesh."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = size
    bpy.ops.object.transform_apply(scale=True)
    assign_material(obj, material)
    return obj


def create_cylinder(name, radius, depth, location, material, segments=8):
    """Create a cylinder mesh."""
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=segments, radius=radius, depth=depth, location=location
    )
    obj = bpy.context.active_object
    obj.name = name
    assign_material(obj, material)
    return obj


def create_sphere(name, radius, location, material, segments=8, rings=6):
    """Create a UV sphere."""
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=segments, ring_count=rings, radius=radius, location=location
    )
    obj = bpy.context.active_object
    obj.name = name
    assign_material(obj, material)
    return obj


# ============================================================
# SOLDIER BODY PARTS
# ============================================================

def create_torso(faction_colors, height_scale=1.0):
    """Create the torso (coat)."""
    mat = create_material(f"Coat", faction_colors["coat"])
    torso = create_box(
        "Torso",
        size=Vector((0.35, 0.2, 0.45 * height_scale)),
        location=Vector((0, 0, 1.1 * height_scale)),
        material=mat
    )
    return torso


def create_head(faction_colors, height_scale=1.0):
    """Create the head."""
    mat = create_material("Skin", faction_colors["skin"])
    head = create_sphere(
        "Head",
        radius=0.12,
        location=Vector((0, 0, 1.52 * height_scale)),
        material=mat,
        segments=8,
        rings=6
    )
    return head


def create_shako(faction_colors, height_scale=1.0):
    """Create the shako (tall military hat)."""
    mat = create_material("Shako", faction_colors["shako"])
    shako = create_cylinder(
        "Shako",
        radius=0.1,
        depth=0.18,
        location=Vector((0, 0, 1.72 * height_scale)),
        material=mat,
        segments=8
    )

    # Shako plate (front decoration)
    plate_mat = create_material("ShakoPlate", (0.75, 0.65, 0.2, 1.0))  # Brass
    plate = create_box(
        "ShakoPlate",
        size=Vector((0.06, 0.01, 0.06)),
        location=Vector((0, -0.1, 1.7 * height_scale)),
        material=plate_mat
    )

    # Plume
    plume_mat = create_material("Plume", faction_colors["plume"])
    plume = create_sphere(
        "Plume",
        radius=0.03,
        location=Vector((0, 0, 1.83 * height_scale)),
        material=plume_mat,
        segments=6,
        rings=4
    )

    return shako, plate, plume


def create_legs(faction_colors, height_scale=1.0):
    """Create both legs (pants + boots)."""
    pants_mat = create_material("Pants", faction_colors["pants"])
    boots_mat = create_material("Boots", faction_colors["boots"])

    parts = []

    for side, x_offset in [("Left", -0.1), ("Right", 0.1)]:
        # Upper leg (pants)
        upper = create_box(
            f"{side}UpperLeg",
            size=Vector((0.12, 0.12, 0.3 * height_scale)),
            location=Vector((x_offset, 0, 0.7 * height_scale)),
            material=pants_mat
        )
        parts.append(upper)

        # Lower leg (pants)
        lower = create_box(
            f"{side}LowerLeg",
            size=Vector((0.11, 0.11, 0.25 * height_scale)),
            location=Vector((x_offset, 0, 0.4 * height_scale)),
            material=pants_mat
        )
        parts.append(lower)

        # Boot
        boot = create_box(
            f"{side}Boot",
            size=Vector((0.12, 0.16, 0.15 * height_scale)),
            location=Vector((x_offset, -0.02, 0.12 * height_scale)),
            material=boots_mat
        )
        parts.append(boot)

    return parts


def create_arms(faction_colors, height_scale=1.0):
    """Create both arms with cuffs."""
    coat_mat = create_material("CoatArm", faction_colors["coat"])
    cuff_mat = create_material("Cuffs", faction_colors["cuffs"])
    skin_mat = create_material("SkinHand", faction_colors["skin"])

    parts = []

    for side, x_offset in [("Left", -0.25), ("Right", 0.25)]:
        # Upper arm (coat color)
        upper = create_box(
            f"{side}UpperArm",
            size=Vector((0.1, 0.1, 0.22 * height_scale)),
            location=Vector((x_offset, 0, 1.2 * height_scale)),
            material=coat_mat
        )
        parts.append(upper)

        # Lower arm (cuff color)
        lower = create_box(
            f"{side}LowerArm",
            size=Vector((0.09, 0.09, 0.2 * height_scale)),
            location=Vector((x_offset, 0, 0.95 * height_scale)),
            material=cuff_mat
        )
        parts.append(lower)

        # Hand
        hand = create_sphere(
            f"{side}Hand",
            radius=0.04,
            location=Vector((x_offset, 0, 0.83 * height_scale)),
            material=skin_mat,
            segments=6,
            rings=4
        )
        parts.append(hand)

    return parts


def create_epaulettes(faction_colors, height_scale=1.0):
    """Create shoulder epaulettes."""
    mat = create_material("Epaulettes", faction_colors["epaulettes"])
    parts = []

    for side, x_offset in [("Left", -0.22), ("Right", 0.22)]:
        ep = create_sphere(
            f"{side}Epaulette",
            radius=0.06,
            location=Vector((x_offset, 0, 1.35 * height_scale)),
            material=mat,
            segments=6,
            rings=4
        )
        ep.scale = Vector((1.0, 0.6, 0.4))
        bpy.ops.object.transform_apply(scale=True)
        parts.append(ep)

    return parts


def create_crossbelts(faction_colors, height_scale=1.0):
    """Create the X-shaped crossbelts on the torso."""
    mat = create_material("Belt", faction_colors["belt"])
    parts = []

    # Diagonal belt 1 (left shoulder to right hip)
    belt1 = create_box(
        "Crossbelt1",
        size=Vector((0.03, 0.01, 0.5 * height_scale)),
        location=Vector((0, -0.11, 1.1 * height_scale)),
        material=mat
    )
    belt1.rotation_euler = Euler((0, 0, math.radians(20)), 'XYZ')
    bpy.ops.object.transform_apply(rotation=True)
    parts.append(belt1)

    # Diagonal belt 2 (right shoulder to left hip)
    belt2 = create_box(
        "Crossbelt2",
        size=Vector((0.03, 0.01, 0.5 * height_scale)),
        location=Vector((0, -0.11, 1.1 * height_scale)),
        material=mat
    )
    belt2.rotation_euler = Euler((0, 0, math.radians(-20)), 'XYZ')
    bpy.ops.object.transform_apply(rotation=True)
    parts.append(belt2)

    return parts


def create_backpack(faction_colors, height_scale=1.0):
    """Create the soldier's backpack."""
    mat = create_material("Backpack", (0.15, 0.12, 0.08, 1.0))  # Brown leather
    backpack = create_box(
        "Backpack",
        size=Vector((0.25, 0.12, 0.2)),
        location=Vector((0, 0.16, 1.15 * height_scale)),
        material=mat
    )
    return backpack


def create_musket(height_scale=1.0):
    """Create a musket (held in left hand)."""
    wood_mat = create_material("MusketWood", (0.3, 0.18, 0.08, 1.0))
    metal_mat = create_material("MusketMetal", (0.25, 0.25, 0.28, 1.0))

    parts = []

    # Stock (wood)
    stock = create_box(
        "MusketStock",
        size=Vector((0.03, 0.03, 0.5)),
        location=Vector((-0.25, -0.08, 1.15 * height_scale)),
        material=wood_mat
    )
    parts.append(stock)

    # Barrel (metal)
    barrel = create_cylinder(
        "MusketBarrel",
        radius=0.012,
        depth=0.7,
        location=Vector((-0.25, -0.08, 1.55 * height_scale)),
        material=metal_mat,
        segments=6
    )
    parts.append(barrel)

    return parts


def create_sabre(height_scale=1.0):
    """Create a cavalry sabre (held at left hip)."""
    metal_mat = create_material("SabreBlade", (0.7, 0.7, 0.75, 1.0))  # Steel
    grip_mat = create_material("SabreGrip", (0.15, 0.1, 0.05, 1.0))  # Dark leather
    guard_mat = create_material("SabreGuard", (0.75, 0.65, 0.2, 1.0))  # Brass

    parts = []

    # Grip
    grip = create_box(
        "SabreGrip",
        size=Vector((0.025, 0.025, 0.12)),
        location=Vector((-0.2, -0.12, 0.85 * height_scale)),
        material=grip_mat
    )
    parts.append(grip)

    # Guard (cross-piece)
    guard = create_box(
        "SabreGuard",
        size=Vector((0.08, 0.02, 0.015)),
        location=Vector((-0.2, -0.12, 0.92 * height_scale)),
        material=guard_mat
    )
    parts.append(guard)

    # Blade (curved slightly via rotation)
    blade = create_box(
        "SabreBlade",
        size=Vector((0.02, 0.005, 0.45)),
        location=Vector((-0.2, -0.12, 1.18 * height_scale)),
        material=metal_mat
    )
    blade.rotation_euler = Euler((0, math.radians(3), 0), 'XYZ')
    bpy.ops.object.transform_apply(rotation=True)
    parts.append(blade)

    return parts


def create_cavalry_helmet(faction_colors, height_scale=1.0):
    """Create a cavalry helmet with crest/plume (replaces shako)."""
    helmet_mat = create_material("CavHelmet", (0.2, 0.2, 0.22, 1.0))  # Dark metal
    crest_mat = create_material("CavCrest", faction_colors["plume"])
    visor_mat = create_material("CavVisor", (0.75, 0.65, 0.2, 1.0))  # Brass

    parts = []

    # Helmet dome
    helmet = create_sphere(
        "CavHelmet",
        radius=0.14,
        location=Vector((0, 0, 1.58 * height_scale)),
        material=helmet_mat,
        segments=10,
        rings=6
    )
    helmet.scale = Vector((1.0, 1.1, 0.9))
    bpy.ops.object.transform_apply(scale=True)
    parts.append(helmet)

    # Crest (tall ridge on top)
    crest = create_box(
        "CavCrest",
        size=Vector((0.02, 0.18, 0.12)),
        location=Vector((0, 0, 1.72 * height_scale)),
        material=crest_mat
    )
    parts.append(crest)

    # Flowing plume behind
    plume = create_box(
        "CavPlume",
        size=Vector((0.03, 0.04, 0.2)),
        location=Vector((0, 0.1, 1.65 * height_scale)),
        material=crest_mat
    )
    plume.rotation_euler = Euler((math.radians(30), 0, 0), 'XYZ')
    bpy.ops.object.transform_apply(rotation=True)
    parts.append(plume)

    # Visor (front peak)
    visor = create_box(
        "CavVisor",
        size=Vector((0.12, 0.06, 0.01)),
        location=Vector((0, -0.12, 1.5 * height_scale)),
        material=visor_mat
    )
    parts.append(visor)

    return parts


def create_tall_boots(faction_colors, height_scale=1.0):
    """Create tall cavalry boots (covers entire lower leg)."""
    boots_mat = create_material("TallBoots", faction_colors["boots"])
    parts = []

    for side, x_offset in [("Left", -0.1), ("Right", 0.1)]:
        boot = create_box(
            f"{side}TallBoot",
            size=Vector((0.13, 0.14, 0.45 * height_scale)),
            location=Vector((x_offset, -0.01, 0.3 * height_scale)),
            material=boots_mat
        )
        parts.append(boot)

    return parts


def create_grenadier_bearskin(faction_colors, height_scale=1.0):
    """Create a tall bearskin hat for grenadiers (replaces shako)."""
    fur_mat = create_material("Bearskin", (0.05, 0.04, 0.03, 1.0))  # Very dark brown
    plate_mat = create_material("BearskinPlate", (0.75, 0.65, 0.2, 1.0))  # Brass
    plume_mat = create_material("GrenadierPlume", faction_colors["plume"])

    parts = []

    # Tall fur hat
    bearskin = create_cylinder(
        "Bearskin",
        radius=0.12,
        depth=0.28,
        location=Vector((0, 0, 1.78 * height_scale)),
        material=fur_mat,
        segments=10
    )
    # Slightly wider at top
    bearskin.scale = Vector((1.0, 1.0, 1.0))
    parts.append(bearskin)

    # Front plate
    plate = create_box(
        "BearskinPlate",
        size=Vector((0.1, 0.01, 0.1)),
        location=Vector((0, -0.12, 1.72 * height_scale)),
        material=plate_mat
    )
    parts.append(plate)

    # Side plume (tall feather)
    plume = create_cylinder(
        "GrenadierPlume",
        radius=0.02,
        depth=0.2,
        location=Vector((0.1, 0, 1.88 * height_scale)),
        material=plume_mat,
        segments=6
    )
    parts.append(plume)

    return parts


def create_light_shako(faction_colors, height_scale=1.0):
    """Create a smaller, lighter shako for light infantry."""
    mat = create_material("LightShako", faction_colors["shako"])
    shako = create_cylinder(
        "LightShako",
        radius=0.09,
        depth=0.13,
        location=Vector((0, 0, 1.68 * height_scale)),
        material=mat,
        segments=8
    )

    # Small cockade instead of big plume
    cockade_mat = create_material("Cockade", faction_colors["plume"])
    cockade = create_sphere(
        "Cockade",
        radius=0.025,
        location=Vector((0, -0.09, 1.68 * height_scale)),
        material=cockade_mat,
        segments=6,
        rings=4
    )

    return shako, cockade


def create_bayonet(height_scale=1.0):
    """Create a bayonet attached to musket barrel tip."""
    metal_mat = create_material("BayonetBlade", (0.6, 0.6, 0.65, 1.0))
    bayonet = create_box(
        "Bayonet",
        size=Vector((0.01, 0.01, 0.18)),
        location=Vector((-0.25, -0.08, 2.0 * height_scale)),
        material=metal_mat
    )
    return bayonet


def create_artillery_tools(height_scale=1.0):
    """Create artillery tools: linstock (firing stick) and rammer."""
    wood_mat = create_material("ToolWood", (0.35, 0.22, 0.1, 1.0))
    metal_mat = create_material("ToolMetal", (0.3, 0.3, 0.32, 1.0))

    parts = []

    # Rammer (long pole with sponge end)
    pole = create_cylinder(
        "RammerPole",
        radius=0.015,
        depth=0.9,
        location=Vector((-0.25, -0.05, 1.2 * height_scale)),
        material=wood_mat,
        segments=6
    )
    parts.append(pole)

    # Sponge head
    sponge = create_cylinder(
        "RammerHead",
        radius=0.04,
        depth=0.08,
        location=Vector((-0.25, -0.05, 1.7 * height_scale)),
        material=metal_mat,
        segments=6
    )
    parts.append(sponge)

    return parts


def create_artillery_apron(faction_colors, height_scale=1.0):
    """Create a leather apron worn by artillerymen."""
    apron_mat = create_material("Apron", (0.25, 0.18, 0.1, 1.0))  # Brown leather
    apron = create_box(
        "Apron",
        size=Vector((0.3, 0.02, 0.35 * height_scale)),
        location=Vector((0, -0.12, 0.7 * height_scale)),
        material=apron_mat
    )
    return apron


# ============================================================
# ARMATURE (RIG)
# ============================================================

def create_armature(name, height_scale=1.0):
    """Create a humanoid armature with Mixamo-compatible bone names."""
    bpy.ops.object.armature_add(location=(0, 0, 0))
    armature_obj = bpy.context.active_object
    armature_obj.name = name
    armature = armature_obj.data
    armature.name = f"{name}_Armature"

    bpy.ops.object.mode_set(mode='EDIT')

    # Remove default bone
    for bone in armature.edit_bones:
        armature.edit_bones.remove(bone)

    h = height_scale

    # === SPINE ===
    hips = armature.edit_bones.new("Hips")
    hips.head = Vector((0, 0, 0.85 * h))
    hips.tail = Vector((0, 0, 0.95 * h))

    spine = armature.edit_bones.new("Spine")
    spine.head = Vector((0, 0, 0.95 * h))
    spine.tail = Vector((0, 0, 1.1 * h))
    spine.parent = hips

    spine01 = armature.edit_bones.new("Spine01")
    spine01.head = Vector((0, 0, 1.1 * h))
    spine01.tail = Vector((0, 0, 1.2 * h))
    spine01.parent = spine

    spine02 = armature.edit_bones.new("Spine02")
    spine02.head = Vector((0, 0, 1.2 * h))
    spine02.tail = Vector((0, 0, 1.35 * h))
    spine02.parent = spine01

    neck = armature.edit_bones.new("Neck")
    neck.head = Vector((0, 0, 1.35 * h))
    neck.tail = Vector((0, 0, 1.45 * h))
    neck.parent = spine02

    head = armature.edit_bones.new("Head")
    head.head = Vector((0, 0, 1.45 * h))
    head.tail = Vector((0, 0, 1.6 * h))
    head.parent = neck

    # === LEGS ===
    for side, x in [("Left", -0.1), ("Right", 0.1)]:
        upper_leg = armature.edit_bones.new(f"{side}UpLeg")
        upper_leg.head = Vector((x, 0, 0.85 * h))
        upper_leg.tail = Vector((x, 0, 0.5 * h))
        upper_leg.parent = hips

        lower_leg = armature.edit_bones.new(f"{side}Leg")
        lower_leg.head = Vector((x, 0, 0.5 * h))
        lower_leg.tail = Vector((x, 0, 0.12 * h))
        lower_leg.parent = upper_leg

        foot = armature.edit_bones.new(f"{side}Foot")
        foot.head = Vector((x, 0, 0.12 * h))
        foot.tail = Vector((x, -0.1, 0.02 * h))
        foot.parent = lower_leg

        toe = armature.edit_bones.new(f"{side}ToeBase")
        toe.head = Vector((x, -0.1, 0.02 * h))
        toe.tail = Vector((x, -0.15, 0.0))
        toe.parent = foot

    # === ARMS ===
    for side, x in [("Left", -0.2), ("Right", 0.2)]:
        shoulder = armature.edit_bones.new(f"{side}Shoulder")
        shoulder.head = Vector((x * 0.5, 0, 1.32 * h))
        shoulder.tail = Vector((x, 0, 1.32 * h))
        shoulder.parent = spine02

        upper_arm = armature.edit_bones.new(f"{side}Arm")
        upper_arm.head = Vector((x, 0, 1.32 * h))
        upper_arm.tail = Vector((x * 1.3, 0, 1.05 * h))
        upper_arm.parent = shoulder

        forearm = armature.edit_bones.new(f"{side}ForeArm")
        forearm.head = Vector((x * 1.3, 0, 1.05 * h))
        forearm.tail = Vector((x * 1.3, 0, 0.82 * h))
        forearm.parent = upper_arm

        hand = armature.edit_bones.new(f"{side}Hand")
        hand.head = Vector((x * 1.3, 0, 0.82 * h))
        hand.tail = Vector((x * 1.3, 0, 0.75 * h))
        hand.parent = forearm

    bpy.ops.object.mode_set(mode='OBJECT')
    return armature_obj


# ============================================================
# SOLDIER ASSEMBLY
# ============================================================

def generate_soldier(faction_name="France", unit_type="LineInfantry", location=(0, 0, 0)):
    """Generate a complete Napoleonic soldier with type-specific visuals.

    Visual differences per unit type:
      LineInfantry: Standard shako, musket + bayonet, backpack, crossbelts
      Grenadier:    Tall bearskin hat, musket + bayonet, large epaulettes, backpack
      LightInfantry: Small shako with cockade, musket (no bayonet), NO backpack, thin build
      Cavalry:      Metal helmet with crest, sabre, tall boots, NO musket, NO backpack
      Artillery:    Standard shako, rammer tool, leather apron, NO musket, wider build
    """

    # Clear selection
    bpy.ops.object.select_all(action='DESELECT')

    faction = FACTIONS[faction_name]
    unit = UNIT_TYPES[unit_type]
    h = unit["height"] / 1.75  # Normalize to base height

    print(f"\n=== Generating {faction_name} {unit_type} ===")

    # Create all body parts
    all_parts = []

    # --- BODY (all types) ---
    torso = create_torso(faction, h)
    all_parts.append(torso)

    head_obj = create_head(faction, h)
    all_parts.append(head_obj)

    legs = create_legs(faction, h)
    all_parts.extend(legs)

    arms = create_arms(faction, h)
    all_parts.extend(arms)

    # --- HEADGEAR (type-specific) ---
    if unit_type == "Grenadier":
        # Tall bearskin fur hat
        bearskin_parts = create_grenadier_bearskin(faction, h)
        all_parts.extend(bearskin_parts)
    elif unit_type == "Cavalry" or unit_type == "Hussar" or unit_type == "Lancer":
        # Metal helmet with crest and plume
        helmet_parts = create_cavalry_helmet(faction, h)
        all_parts.extend(helmet_parts)
    elif unit_type == "LightInfantry":
        # Smaller shako with cockade
        light_shako, cockade = create_light_shako(faction, h)
        all_parts.extend([light_shako, cockade])
    else:
        # Standard shako (LineInfantry, Artillery)
        shako, plate, plume = create_shako(faction, h)
        all_parts.extend([shako, plate, plume])

    # --- EPAULETTES (all infantry + artillery) ---
    epaulettes = create_epaulettes(faction, h)
    all_parts.extend(epaulettes)

    # --- CROSSBELTS (infantry types only) ---
    if unit_type in ("LineInfantry", "Grenadier", "LightInfantry"):
        crossbelts = create_crossbelts(faction, h)
        all_parts.extend(crossbelts)

    # --- BACKPACK (LineInfantry and Grenadier only) ---
    if unit_type in ("LineInfantry", "Grenadier"):
        backpack = create_backpack(faction, h)
        all_parts.append(backpack)

    # --- WEAPONS & EQUIPMENT (type-specific) ---
    if unit_type == "Cavalry":
        # Sabre + tall boots (no musket)
        sabre_parts = create_sabre(h)
        all_parts.extend(sabre_parts)
        tall_boots = create_tall_boots(faction, h)
        all_parts.extend(tall_boots)
    elif unit_type == "Artillery":
        # Rammer tool + leather apron (no musket)
        tool_parts = create_artillery_tools(h)
        all_parts.extend(tool_parts)
        apron = create_artillery_apron(faction, h)
        all_parts.append(apron)
    else:
        # Infantry types: musket
        if unit["has_musket"]:
            musket = create_musket(h)
            all_parts.extend(musket)
            # Bayonet for LineInfantry and Grenadier
            if unit.get("has_bayonet", False):
                bayonet = create_bayonet(h)
                all_parts.append(bayonet)

    # --- MANUAL VERTEX WEIGHT ASSIGNMENT ---
    # Tag each mesh part with the bone it should follow BEFORE joining.
    # This maps part names to bone names based on body location.
    bone_assignments = {}
    for part in all_parts:
        name = part.name.lower()
        if "torso" in name or "crossbelt" in name:
            bone_assignments[part.name] = "Spine01"
        elif "head" in name:
            bone_assignments[part.name] = "Head"
        elif "shako" in name or "plate" in name or "plume" in name or "bearskin" in name or "cockade" in name or "helmet" in name or "crest" in name or "visor" in name or "lightshako" in name:
            bone_assignments[part.name] = "Head"
        elif "neck" in name:
            bone_assignments[part.name] = "Neck"
        elif "backpack" in name:
            bone_assignments[part.name] = "Spine02"
        elif "apron" in name:
            bone_assignments[part.name] = "Spine"
        elif "leftupperleg" in name:
            bone_assignments[part.name] = "LeftUpLeg"
        elif "leftlowerleg" in name:
            bone_assignments[part.name] = "LeftLeg"
        elif "leftboot" in name or "lefttallboot" in name:
            bone_assignments[part.name] = "LeftFoot"
        elif "rightupperleg" in name:
            bone_assignments[part.name] = "RightUpLeg"
        elif "rightlowerleg" in name:
            bone_assignments[part.name] = "RightLeg"
        elif "rightboot" in name or "righttallboot" in name:
            bone_assignments[part.name] = "RightFoot"
        elif "leftupperarm" in name or "leftepaulette" in name:
            bone_assignments[part.name] = "LeftArm"
        elif "leftlowerarm" in name:
            bone_assignments[part.name] = "LeftForeArm"
        elif "lefthand" in name:
            bone_assignments[part.name] = "LeftHand"
        elif "rightupperarm" in name or "rightepaulette" in name:
            bone_assignments[part.name] = "RightArm"
        elif "rightlowerarm" in name:
            bone_assignments[part.name] = "RightForeArm"
        elif "righthand" in name:
            bone_assignments[part.name] = "RightHand"
        elif "musket" in name or "bayonet" in name or "rammer" in name or "sabre" in name:
            bone_assignments[part.name] = "LeftHand"
        elif "epaulette" in name:
            bone_assignments[part.name] = "Spine02"
        else:
            bone_assignments[part.name] = "Spine01"

    # Store vertex ranges per part before joining
    part_vertex_ranges = []
    vert_offset = 0
    for part in all_parts:
        num_verts = len(part.data.vertices)
        part_vertex_ranges.append((part.name, vert_offset, vert_offset + num_verts))
        vert_offset += num_verts

    # Join all mesh parts into one object
    bpy.ops.object.select_all(action='DESELECT')
    for part in all_parts:
        part.select_set(True)
    bpy.context.view_layer.objects.active = torso
    bpy.ops.object.join()

    soldier_mesh = bpy.context.active_object
    soldier_mesh.name = f"{faction_name}_{unit_type}"

    # Create armature
    armature_obj = create_armature(f"{faction_name}_{unit_type}_Rig", h)

    # Move armature to soldier location
    armature_obj.location = Vector(location)
    soldier_mesh.location = Vector(location)

    # Parent mesh to armature (without weights — we'll assign manually)
    bpy.ops.object.select_all(action='DESELECT')
    soldier_mesh.select_set(True)
    armature_obj.select_set(True)
    bpy.context.view_layer.objects.active = armature_obj
    bpy.ops.object.parent_set(type='ARMATURE_NAME')

    # Create vertex groups for all bones
    for bone in armature_obj.data.bones:
        if bone.name not in soldier_mesh.vertex_groups:
            soldier_mesh.vertex_groups.new(name=bone.name)

    # Assign vertices to correct bone groups with weight 1.0
    for part_name, v_start, v_end in part_vertex_ranges:
        bone_name = bone_assignments.get(part_name, "Spine01")
        if bone_name in soldier_mesh.vertex_groups:
            vg = soldier_mesh.vertex_groups[bone_name]
            vg.add(list(range(v_start, v_end)), 1.0, 'REPLACE')

    print(f"  Vertex groups assigned: {len(part_vertex_ranges)} parts -> bones")

    # Apply smooth shading
    bpy.ops.object.select_all(action='DESELECT')
    soldier_mesh.select_set(True)
    bpy.context.view_layer.objects.active = soldier_mesh
    bpy.ops.object.shade_flat()

    # Count triangles
    bpy.ops.object.mode_set(mode='EDIT')
    bm = bmesh.from_edit_mesh(soldier_mesh.data)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    tri_count = len(bm.faces)
    bpy.ops.object.mode_set(mode='OBJECT')

    print(f"  Triangles: {tri_count}")
    print(f"  Bones: {len(armature_obj.data.bones)}")
    print(f"  Materials: {len(soldier_mesh.data.materials)}")

    return armature_obj, soldier_mesh


# ============================================================
# BATCH EXPORT HELPER
# ============================================================

def export_soldier_fbx(armature_obj, mesh_obj, filepath):
    """Export a soldier as FBX for Unity."""
    bpy.ops.object.select_all(action='DESELECT')
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
    )
    print(f"  Exported: {filepath}")


# ============================================================
# MAIN
# ============================================================

def main():
    # Clear scene
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)

    # Clean up orphan data
    for block in bpy.data.meshes:
        if block.users == 0:
            bpy.data.meshes.remove(block)
    for block in bpy.data.materials:
        if block.users == 0:
            bpy.data.materials.remove(block)
    for block in bpy.data.armatures:
        if block.users == 0:
            bpy.data.armatures.remove(block)

    print("\n" + "=" * 60)
    print("  NAPOLEONIC SOLDIER GENERATOR")
    print("=" * 60)

    soldiers = []

    # Generate one soldier per faction + unit type combo
    x_offset = 0
    for faction in ["France", "Britain"]:
        for unit_type in ["LineInfantry", "Grenadier", "LightInfantry", "Cavalry", "Artillery"]:
            rig, mesh = generate_soldier(faction, unit_type, location=(x_offset, 0, 0))
            soldiers.append((rig, mesh))
            x_offset += 1.5

    print(f"\n=== Generated {len(soldiers)} soldiers ===")
    print("\nVisual differences per type:")
    print("  LineInfantry : Standard shako, musket + bayonet, backpack, crossbelts")
    print("  Grenadier    : Tall bearskin hat, musket + bayonet, big epaulettes, backpack")
    print("  LightInfantry: Small shako + cockade, musket (no bayonet), NO backpack")
    print("  Cavalry      : Metal helmet + crest, sabre, tall boots, NO musket")
    print("  Artillery    : Standard shako, rammer tool, leather apron, NO musket")
    print("\nTo export: run batch_export_soldiers.py")
    print("In Unity: Tools > Configure Soldier FBX (auto-configures everything)")

    bpy.ops.object.select_all(action='SELECT')
    print("\nDone! Inspect soldiers in the 3D viewport.")


if __name__ == "__main__":
    main()
