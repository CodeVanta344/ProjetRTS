import bpy
import math

# ============================================================
#  Antigravity Live Script
#  Modifiez ce fichier et sauvegardez — Blender se met a jour !
# ============================================================

# Prefixe pour identifier les objets crees par ce script
PREFIX = "AG_"


def cleanup():
    """Supprime tous les objets crees par ce script (prefixe AG_)."""
    bpy.ops.object.select_all(action='DESELECT')
    for obj in bpy.data.objects:
        if obj.name.startswith(PREFIX):
            obj.select_set(True)
    bpy.ops.object.delete()


def make_material(name, color=(0.8, 0.2, 0.2, 1.0), metallic=0.5, roughness=0.3):
    """Cree ou recupere un materiau PBR."""
    mat_name = f"{PREFIX}{name}"
    mat = bpy.data.materials.get(mat_name)
    if mat is None:
        mat = bpy.data.materials.new(name=mat_name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    return mat


def create_scene():
    """Cree la scene 3D — MODIFIEZ CETTE FONCTION !"""

    # Nettoyage des anciens objets
    cleanup()

    # --- Cube principal ---
    bpy.ops.mesh.primitive_cube_add(size=2, location=(0, 0, 1))
    cube = bpy.context.active_object
    cube.name = f"{PREFIX}Cube"
    cube.data.materials.append(make_material("Red", color=(0.8, 0.1, 0.1, 1.0)))
    cube.modifiers.new(name="Subdiv", type='SUBSURF')
    cube.modifiers["Subdiv"].levels = 2

    # --- Sphere ---
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.8, location=(3, 0, 1))
    sphere = bpy.context.active_object
    sphere.name = f"{PREFIX}Sphere"
    sphere.data.materials.append(make_material("Blue", color=(0.1, 0.3, 0.9, 1.0), metallic=0.8))

    # --- Sol ---
    bpy.ops.mesh.primitive_plane_add(size=20, location=(0, 0, 0))
    ground = bpy.context.active_object
    ground.name = f"{PREFIX}Ground"
    ground.data.materials.append(make_material("Ground", color=(0.15, 0.15, 0.15, 1.0), roughness=0.9))

    print(f"[Antigravity] Scene creee avec succes !")


# === Lancer ===
create_scene()
