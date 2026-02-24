"""
simple_export_cannon.py
Blender script simple - Export direct vers E:\FPSLowPoly\BlenderScripts\
Usage: Lancer dans Blender (Scripting tab) ou avec batch_simple_export.bat
"""

import bpy
import os
import math
from mathutils import Vector

# Export direct vers BlenderScripts
EXPORT_PATH = r"E:\FPSLowPoly\BlenderScripts"

def clear_scene():
    """Clear existing mesh objects"""
    bpy.ops.object.select_all(action='DESELECT')
    bpy.ops.object.select_by_type(type='MESH')
    bpy.ops.object.delete()

def create_cannon_simple():
    """Canon simplifié - tube, affût, 2 roues"""
    clear_scene()
    
    # === TUBE (cylindre conique) ===
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=24,
        radius=0.15,
        depth=2.0,
        location=(0, 0, 1.0)
    )
    barrel = bpy.context.active_object
    barrel.name = "Canon_Tube"
    
    # Anneau de renfort
    bpy.ops.mesh.primitive_torus_add(
        major_radius=0.18,
        minor_radius=0.03,
        location=(0, 0, 0.3)
    )
    ring = bpy.context.active_object
    ring.name = "Anneau_Renfort"
    ring.parent = barrel
    
    # === AFFÛT (cube allongé) ===
    bpy.ops.mesh.primitive_cube_add(
        size=1,
        location=(0, 0, 0.4)
    )
    carriage = bpy.context.active_object
    carriage.scale = (1.2, 0.5, 0.3)
    carriage.name = "Affut"
    
    # Essieu
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=16,
        radius=0.08,
        depth=1.4,
        location=(0.6, 0, 0.35),
        rotation=(0, math.pi/2, 0)
    )
    axle = bpy.context.active_object
    axle.name = "Essieu"
    
    # === ROUES (2 roues identiques) ===
    def create_wheel(name, y_pos):
        bpy.ops.mesh.primitive_cylinder_add(
            vertices=16,
            radius=0.35,
            depth=0.12,
            location=(0.6, y_pos, 0.35),
            rotation=(math.pi/2, 0, 0)
        )
        wheel = bpy.context.active_object
        wheel.name = name
        return wheel
    
    wheel_l = create_wheel("Roue_Gauche", -0.8)
    wheel_r = create_wheel("Roue_Droite", 0.8)
    
    # === PARENT ===
    bpy.ops.object.empty_add(type='PLAIN_AXES', location=(0, 0, 0))
    parent = bpy.context.active_object
    parent.name = "Canon_12_Pounder"
    
    barrel.parent = parent
    ring.parent = barrel
    carriage.parent = parent
    axle.parent = parent
    wheel_l.parent = parent
    wheel_r.parent = parent
    
    print("Canon créé!")
    return parent

def export_cannon():
    """Export en FBX vers E:\\FPSLowPoly\\BlenderScripts\\"""
    
    # Créer dossier si inexistant
    os.makedirs(EXPORT_PATH, exist_ok=True)
    
    # Sélectionner le canon
    cannon = bpy.data.objects.get("Canon_12_Pounder")
    if not cannon:
        print("ERREUR: Canon non trouvé!")
        return False
    
    bpy.ops.object.select_all(action='DESELECT')
    cannon.select_set(True)
    
    # Sélectionner récursivement tous les enfants
    def select_children(obj):
        for child in obj.children:
            child.select_set(True)
            select_children(child)
    
    select_children(cannon)
    
    # Export FBX
    filepath = os.path.join(EXPORT_PATH, "canon_12_pounder.fbx")
    
    bpy.ops.export_scene.fbx(
        filepath=filepath,
        use_selection=True,
        global_scale=1.0,
        apply_scale_options='FBX_SCALE_UNITS',
        axis_forward='-Z',
        axis_up='Y',
        use_mesh_modifiers=True,
        mesh_smooth_type='FACE',
        bake_anim=False
    )
    
    print(f"Exporté vers: {filepath}")
    return True

# === EXÉCUTION ===
if __name__ == "__main__":
    print("\n=== Création du canon ===")
    create_cannon_simple()
    
    print("\n=== Export ===")
    export_cannon()
    
    print("\n=== TERMINÉ ===")
    print(f"Fichier: {EXPORT_PATH}\\canon_12_pounder.fbx")
