bl_info = {
    "name": "Antigravity File Watcher",
    "author": "Antigravity AI",
    "version": (1, 0, 0),
    "blender": (3, 0, 0),
    "location": "View3D > Sidebar > Antigravity",
    "description": "Watches a folder for Python script changes and auto-executes them in Blender for real-time 3D visualization",
    "category": "Development",
}

import bpy
import os
import time
import traceback
from bpy.props import StringProperty, BoolProperty, FloatProperty, EnumProperty
from bpy.types import Operator, Panel, PropertyGroup


# ============================================================
#  Properties
# ============================================================

class AntigravityWatcherProperties(PropertyGroup):
    watch_folder: StringProperty(
        name="Watch Folder",
        description="Folder to watch for script changes",
        default="",
        subtype='DIR_PATH',
    )
    is_watching: BoolProperty(
        name="Watching",
        description="Whether the watcher is active",
        default=False,
    )
    poll_interval: FloatProperty(
        name="Poll Interval (s)",
        description="How often to check for file changes (seconds)",
        default=0.5,
        min=0.1,
        max=5.0,
    )
    auto_clear: BoolProperty(
        name="Auto Clear Scene",
        description="Clear the scene before executing each script",
        default=False,
    )
    watch_mode: EnumProperty(
        name="Watch Mode",
        description="Which file(s) to watch",
        items=[
            ('ALL', "All .py Files", "Watch all Python files in the folder"),
            ('SINGLE', "Single File (live_script.py)", "Only watch live_script.py"),
        ],
        default='SINGLE',
    )
    last_status: StringProperty(
        name="Last Status",
        default="Idle",
    )


# ============================================================
#  File Watcher Core
# ============================================================

class FileWatcherState:
    """Tracks modification times of watched files."""
    _file_times = {}
    _timer_running = False

    @classmethod
    def reset(cls):
        cls._file_times = {}

    @classmethod
    def get_watched_files(cls, props):
        folder = bpy.path.abspath(props.watch_folder)
        if not os.path.isdir(folder):
            return []

        if props.watch_mode == 'SINGLE':
            target = os.path.join(folder, "live_script.py")
            return [target] if os.path.isfile(target) else []
        else:
            return [
                os.path.join(folder, f)
                for f in os.listdir(folder)
                if f.endswith('.py') and not f.startswith('_') and f != os.path.basename(__file__)
            ]

    @classmethod
    def check_for_changes(cls, props):
        changed_files = []
        watched = cls.get_watched_files(props)

        for filepath in watched:
            try:
                mtime = os.path.getmtime(filepath)
            except OSError:
                continue

            prev_mtime = cls._file_times.get(filepath)
            if prev_mtime is None:
                # First time seeing this file — record but don't trigger
                cls._file_times[filepath] = mtime
            elif mtime != prev_mtime:
                cls._file_times[filepath] = mtime
                changed_files.append(filepath)

        # Clean up deleted files
        current_set = set(watched)
        for old in list(cls._file_times.keys()):
            if old not in current_set:
                del cls._file_times[old]

        return changed_files


def clear_scene():
    """Remove all objects from the scene."""
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)


def execute_script(filepath, auto_clear=False):
    """Execute a Python script file in Blender's context."""
    if auto_clear:
        clear_scene()

    # Provide useful globals to the script
    script_globals = {
        "__file__": filepath,
        "__name__": "__main__",
        "bpy": bpy,
    }

    with open(filepath, 'r', encoding='utf-8') as f:
        code = f.read()

    compiled = compile(code, filepath, 'exec')
    exec(compiled, script_globals)


def watcher_timer():
    """Timer callback that checks for file changes."""
    props = bpy.context.scene.antigravity_watcher

    if not props.is_watching:
        FileWatcherState._timer_running = False
        return None  # Stop timer

    changed = FileWatcherState.check_for_changes(props)

    for filepath in changed:
        filename = os.path.basename(filepath)
        print(f"\n{'='*60}")
        print(f"[Antigravity] Detected change: {filename}")
        print(f"{'='*60}")

        try:
            execute_script(filepath, props.auto_clear)
            props.last_status = f"✅ {filename} — OK"
            print(f"[Antigravity] ✅ {filename} executed successfully")
        except Exception as e:
            error_msg = traceback.format_exc()
            props.last_status = f"❌ {filename} — {str(e)[:80]}"
            print(f"[Antigravity] ❌ Error in {filename}:")
            print(error_msg)

        # Force viewport redraw
        for area in bpy.context.screen.areas:
            if area.type == 'VIEW_3D':
                area.tag_redraw()

    return props.poll_interval


# ============================================================
#  Operators
# ============================================================

class ANTIGRAVITY_OT_start_watching(Operator):
    bl_idname = "antigravity.start_watching"
    bl_label = "Start Watching"
    bl_description = "Start watching the folder for script changes"

    def execute(self, context):
        props = context.scene.antigravity_watcher
        folder = bpy.path.abspath(props.watch_folder)

        if not folder or not os.path.isdir(folder):
            self.report({'ERROR'}, f"Invalid folder: {folder}")
            return {'CANCELLED'}

        FileWatcherState.reset()
        props.is_watching = True
        props.last_status = "👁️ Watching..."

        # Initialize file times without triggering
        FileWatcherState.check_for_changes(props)

        if not FileWatcherState._timer_running:
            FileWatcherState._timer_running = True
            bpy.app.timers.register(watcher_timer, first_interval=props.poll_interval)

        watched = FileWatcherState.get_watched_files(props)
        self.report({'INFO'}, f"Watching {len(watched)} file(s) in {folder}")
        print(f"\n[Antigravity] 🚀 Started watching: {folder}")
        print(f"[Antigravity] Mode: {props.watch_mode}")
        print(f"[Antigravity] Files: {len(watched)}")
        return {'FINISHED'}


class ANTIGRAVITY_OT_stop_watching(Operator):
    bl_idname = "antigravity.stop_watching"
    bl_label = "Stop Watching"
    bl_description = "Stop watching for script changes"

    def execute(self, context):
        props = context.scene.antigravity_watcher
        props.is_watching = False
        props.last_status = "⏹️ Stopped"
        self.report({'INFO'}, "File watcher stopped")
        print("[Antigravity] ⏹️ Watcher stopped")
        return {'FINISHED'}


class ANTIGRAVITY_OT_run_now(Operator):
    bl_idname = "antigravity.run_now"
    bl_label = "Run Now"
    bl_description = "Manually execute the watched script(s) right now"

    def execute(self, context):
        props = context.scene.antigravity_watcher
        files = FileWatcherState.get_watched_files(props)

        if not files:
            self.report({'WARNING'}, "No script files found")
            return {'CANCELLED'}

        for filepath in files:
            filename = os.path.basename(filepath)
            try:
                execute_script(filepath, props.auto_clear)
                props.last_status = f"✅ {filename} — OK"
                self.report({'INFO'}, f"Executed: {filename}")
            except Exception as e:
                props.last_status = f"❌ {filename} — {str(e)[:80]}"
                self.report({'ERROR'}, f"Error in {filename}: {e}")
                print(traceback.format_exc())

        return {'FINISHED'}


class ANTIGRAVITY_OT_create_template(Operator):
    bl_idname = "antigravity.create_template"
    bl_label = "Create Template Script"
    bl_description = "Create a live_script.py template in the watch folder"

    def execute(self, context):
        props = context.scene.antigravity_watcher
        folder = bpy.path.abspath(props.watch_folder)

        if not folder or not os.path.isdir(folder):
            self.report({'ERROR'}, f"Invalid folder: {folder}")
            return {'CANCELLED'}

        template_path = os.path.join(folder, "live_script.py")

        if os.path.exists(template_path):
            self.report({'WARNING'}, "live_script.py already exists")
            return {'CANCELLED'}

        template = '''import bpy
import math

# ============================================================
#  Antigravity Live Script
#  Edit this file and save — Blender will auto-update!
# ============================================================

def create_demo():
    """Create a simple demo scene."""

    # Clear existing mesh objects
    bpy.ops.object.select_all(action='DESELECT')
    for obj in bpy.data.objects:
        if obj.name.startswith("AG_"):
            obj.select_set(True)
    bpy.ops.object.delete()

    # Create a cube
    bpy.ops.mesh.primitive_cube_add(size=2, location=(0, 0, 1))
    cube = bpy.context.active_object
    cube.name = "AG_Cube"

    # Add a material
    mat = bpy.data.materials.new(name="AG_Material")
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (0.8, 0.2, 0.2, 1.0)  # Red
    bsdf.inputs["Metallic"].default_value = 0.5
    bsdf.inputs["Roughness"].default_value = 0.3
    cube.data.materials.append(mat)

    # Add subdivision surface modifier
    cube.modifiers.new(name="Subdivision", type='SUBSURF')
    cube.modifiers["Subdivision"].levels = 2

    print("[Antigravity] Demo scene created!")


# Run!
create_demo()
'''

        with open(template_path, 'w', encoding='utf-8') as f:
            f.write(template)

        self.report({'INFO'}, f"Created template: {template_path}")
        return {'FINISHED'}


# ============================================================
#  UI Panel
# ============================================================

class ANTIGRAVITY_PT_watcher_panel(Panel):
    bl_label = "🚀 Antigravity Watcher"
    bl_idname = "ANTIGRAVITY_PT_watcher_panel"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = "Antigravity"

    def draw(self, context):
        layout = self.layout
        props = context.scene.antigravity_watcher

        # --- Folder Selection ---
        box = layout.box()
        box.label(text="📁 Watch Folder", icon='FILE_FOLDER')
        box.prop(props, "watch_folder", text="")

        # --- Settings ---
        box = layout.box()
        box.label(text="⚙️ Settings", icon='PREFERENCES')
        row = box.row()
        row.prop(props, "watch_mode", expand=True)
        box.prop(props, "poll_interval")
        box.prop(props, "auto_clear")

        # --- Controls ---
        box = layout.box()
        box.label(text="🎮 Controls", icon='PLAY')

        if props.is_watching:
            box.operator("antigravity.stop_watching", icon='PAUSE', text="Stop Watching")
            row = box.row()
            row.enabled = True
            row.operator("antigravity.run_now", icon='FILE_REFRESH', text="Force Run")
        else:
            box.operator("antigravity.start_watching", icon='PLAY', text="Start Watching")

        box.operator("antigravity.create_template", icon='FILE_NEW', text="Create Template")

        # --- Status ---
        box = layout.box()
        box.label(text="📊 Status", icon='INFO')

        status_icon = 'CHECKMARK' if '✅' in props.last_status else (
            'ERROR' if '❌' in props.last_status else 'TIME'
        )
        box.label(text=props.last_status, icon=status_icon)

        if props.is_watching:
            watched = FileWatcherState.get_watched_files(props)
            box.label(text=f"Watching {len(watched)} file(s)")


# ============================================================
#  Registration
# ============================================================

classes = (
    AntigravityWatcherProperties,
    ANTIGRAVITY_OT_start_watching,
    ANTIGRAVITY_OT_stop_watching,
    ANTIGRAVITY_OT_run_now,
    ANTIGRAVITY_OT_create_template,
    ANTIGRAVITY_PT_watcher_panel,
)


def register():
    for cls in classes:
        bpy.utils.register_class(cls)
    bpy.types.Scene.antigravity_watcher = bpy.props.PointerProperty(type=AntigravityWatcherProperties)
    print("\n[Antigravity] ✅ File Watcher add-on loaded!")
    print("[Antigravity] Open the sidebar (N) > Antigravity tab to start")


def unregister():
    # Stop any active timer
    if hasattr(bpy.context, 'scene') and hasattr(bpy.context.scene, 'antigravity_watcher'):
        bpy.context.scene.antigravity_watcher.is_watching = False

    del bpy.types.Scene.antigravity_watcher
    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)
    print("[Antigravity] Add-on unloaded")


if __name__ == "__main__":
    register()
