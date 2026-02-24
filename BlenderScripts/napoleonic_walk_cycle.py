"""
Napoleonic Soldier Walk Cycle Animation - Blender Script
=========================================================
Usage:
  1. Open Blender
  2. Select your armature (soldier rig)
  3. Open the Scripting workspace
  4. Open or paste this script
  5. Adjust BONE_MAP below to match your rig's bone names
  6. Run the script (Alt+P)

The script creates a 24-frame military march cycle at 24 FPS (1 second loop).
Napoleonic soldiers marched in rigid, upright formation with:
  - Stiff upper body, chest out
  - Short, disciplined steps (~75 cm stride)
  - Arms swinging moderately (musket carry limits arm swing)
  - Left arm holds musket against shoulder (minimal swing)
  - Right arm swings naturally but restrained
"""

import bpy
import math
from mathutils import Euler

# ============================================================
# CONFIGURATION - Adjust bone names to match YOUR rig
# ============================================================

BONE_MAP = {
    # Spine / Torso
    "hips":           "Hips",
    "spine":          "Spine",
    "spine1":         "Spine01",
    "spine2":         "Spine02",
    "neck":           "neck",
    "head":           "Head",

    # Left leg
    "left_upper_leg":  "LeftUpLeg",
    "left_lower_leg":  "LeftLeg",
    "left_foot":       "LeftFoot",
    "left_toe":        "LeftToeBase",

    # Right leg
    "right_upper_leg": "RightUpLeg",
    "right_lower_leg": "RightLeg",
    "right_foot":      "RightFoot",
    "right_toe":       "RightToeBase",

    # Left arm (musket side - less swing)
    "left_shoulder":   "LeftShoulder",
    "left_upper_arm":  "LeftArm",
    "left_lower_arm":  "LeftForeArm",
    "left_hand":       "LeftHand",

    # Right arm (free arm - more swing)
    "right_shoulder":  "RightShoulder",
    "right_upper_arm": "RightArm",
    "right_lower_arm": "RightForeArm",
    "right_hand":      "RightHand",
}

# Animation settings
FRAME_START = 1
FRAME_END = 24          # 24 frames = 1 second at 24fps
FPS = 24
ACTION_NAME = "NapoleonicWalkCycle"

# March parameters (in degrees)
STRIDE_ANGLE = 22.0         # Upper leg forward/back swing (disciplined short steps)
KNEE_BEND = 35.0            # Knee bend at passing
FOOT_ROLL = 15.0            # Ankle dorsiflexion
TOE_BEND = 20.0             # Toe push-off

HIP_VERTICAL = 0.02         # Hips up/down (meters)
HIP_SWAY = 1.5              # Hips side-to-side rotation (degrees)
HIP_TWIST = 3.0             # Hips twist (degrees)

SPINE_COUNTER = 2.0         # Spine counter-rotation
SPINE_UPRIGHT = -2.0        # Slight backward lean (chest out, Napoleonic posture)

HEAD_STABLE = 1.0           # Head counter-movement to stay stable

# Arm swing (asymmetric: left arm holds musket)
RIGHT_ARM_SWING = 15.0      # Right arm swings more freely
LEFT_ARM_SWING = 5.0        # Left arm barely swings (holding musket)
ELBOW_BEND_BASE = 25.0      # Base elbow bend
RIGHT_ELBOW_EXTRA = 10.0    # Extra elbow bend during swing
LEFT_ELBOW_MUSKET = 45.0    # Left elbow bent to hold musket at shoulder


# ============================================================
# HELPER FUNCTIONS
# ============================================================

def deg(d):
    """Convert degrees to radians."""
    return math.radians(d)


def set_keyframe(pose_bone, frame, rotation_euler, location=None):
    """Set a rotation (and optional location) keyframe on a pose bone."""
    pose_bone.rotation_mode = 'XYZ'
    pose_bone.rotation_euler = Euler([deg(r) for r in rotation_euler], 'XYZ')
    pose_bone.keyframe_insert(data_path="rotation_euler", frame=frame)

    if location is not None:
        pose_bone.location = location
        pose_bone.keyframe_insert(data_path="location", frame=frame)


def get_bone(armature, key):
    """Get a pose bone by its mapped name. Returns None if not found."""
    name = BONE_MAP.get(key)
    if name and name in armature.pose.bones:
        return armature.pose.bones[name]
    return None


def sin_interp(phase):
    """Sinusoidal interpolation helper (0-1 phase -> -1 to 1)."""
    return math.sin(phase * 2 * math.pi)


def cos_interp(phase):
    return math.cos(phase * 2 * math.pi)


# ============================================================
# KEYFRAME DATA GENERATION
# ============================================================

def generate_walk_keyframes(armature):
    """Generate all keyframes for the Napoleonic march cycle."""

    # Key frames in the cycle (normalized 0-1):
    # 0.00 = Right foot contact (heel strike)
    # 0.25 = Right foot passing (left foot pushing off)
    # 0.50 = Left foot contact (heel strike)
    # 0.75 = Left foot passing (right foot pushing off)
    # 1.00 = Loop back to 0.00

    num_frames = FRAME_END - FRAME_START + 1
    # We generate keyframes at every frame for smooth motion
    # but key poses are at contact and passing positions

    for f in range(num_frames + 1):  # +1 to close the loop
        frame = FRAME_START + f
        if f == num_frames:
            frame = FRAME_END + 1  # duplicate first frame for seamless loop

        phase = f / num_frames  # 0.0 to 1.0

        # ---- HIPS ----
        hips = get_bone(armature, "hips")
        if hips:
            # Vertical bob: lowest at contact (0, 0.5), highest at passing (0.25, 0.75)
            vert = -HIP_VERTICAL * cos_interp(phase * 2)
            # Side sway: shift toward planted foot
            sway = HIP_SWAY * sin_interp(phase)
            # Twist: counter-rotate to legs
            twist = HIP_TWIST * sin_interp(phase)
            set_keyframe(hips, frame,
                         rotation_euler=(0, 0, sway),
                         location=(0, 0, vert))
            # Add twist separately on Y
            hips.rotation_euler.y = deg(twist)
            hips.keyframe_insert(data_path="rotation_euler", frame=frame)

        # ---- SPINE (upright Napoleonic posture) ----
        spine = get_bone(armature, "spine")
        if spine:
            counter = SPINE_COUNTER * sin_interp(phase)
            set_keyframe(spine, frame,
                         rotation_euler=(SPINE_UPRIGHT, -counter * 0.3, 0))

        spine1 = get_bone(armature, "spine1")
        if spine1:
            counter = SPINE_COUNTER * sin_interp(phase)
            set_keyframe(spine1, frame,
                         rotation_euler=(SPINE_UPRIGHT * 0.5, -counter * 0.3, 0))

        spine2 = get_bone(armature, "spine2")
        if spine2:
            counter = SPINE_COUNTER * sin_interp(phase)
            set_keyframe(spine2, frame,
                         rotation_euler=(SPINE_UPRIGHT * 0.3, -counter * 0.4, 0))

        # ---- HEAD (stable, slight counter) ----
        head = get_bone(armature, "head")
        if head:
            head_counter = HEAD_STABLE * sin_interp(phase)
            set_keyframe(head, frame, rotation_euler=(0, head_counter, 0))

        neck = get_bone(armature, "neck")
        if neck:
            set_keyframe(neck, frame, rotation_euler=(0, 0, 0))

        # ---- RIGHT LEG (phase 0 = contact) ----
        r_upper = get_bone(armature, "right_upper_leg")
        if r_upper:
            # Forward/back swing
            swing = STRIDE_ANGLE * sin_interp(phase)
            set_keyframe(r_upper, frame, rotation_euler=(-swing, 0, 0))

        r_lower = get_bone(armature, "right_lower_leg")
        if r_lower:
            # Knee bends during passing phase, straight at contact
            # Max bend at phase 0.25 (passing) and slight bend at 0.75
            knee = KNEE_BEND * max(0, cos_interp(phase * 2 - 0.5))
            set_keyframe(r_lower, frame, rotation_euler=(knee, 0, 0))

        r_foot = get_bone(armature, "right_foot")
        if r_foot:
            # Heel strike -> flat -> toe off
            foot = FOOT_ROLL * sin_interp(phase)
            set_keyframe(r_foot, frame, rotation_euler=(foot, 0, 0))

        r_toe = get_bone(armature, "right_toe")
        if r_toe:
            toe = TOE_BEND * max(0, sin_interp(phase - 0.25))
            set_keyframe(r_toe, frame, rotation_euler=(toe, 0, 0))

        # ---- LEFT LEG (phase offset by 0.5) ----
        l_upper = get_bone(armature, "left_upper_leg")
        if l_upper:
            swing = STRIDE_ANGLE * sin_interp(phase + 0.5)
            set_keyframe(l_upper, frame, rotation_euler=(-swing, 0, 0))

        l_lower = get_bone(armature, "left_lower_leg")
        if l_lower:
            knee = KNEE_BEND * max(0, cos_interp((phase + 0.5) * 2 - 0.5))
            set_keyframe(l_lower, frame, rotation_euler=(knee, 0, 0))

        l_foot = get_bone(armature, "left_foot")
        if l_foot:
            foot = FOOT_ROLL * sin_interp(phase + 0.5)
            set_keyframe(l_foot, frame, rotation_euler=(foot, 0, 0))

        l_toe = get_bone(armature, "left_toe")
        if l_toe:
            toe = TOE_BEND * max(0, sin_interp(phase + 0.25))
            set_keyframe(l_toe, frame, rotation_euler=(toe, 0, 0))

        # ---- RIGHT ARM (free arm, swings opposite to right leg) ----
        r_shoulder = get_bone(armature, "right_shoulder")
        if r_shoulder:
            set_keyframe(r_shoulder, frame, rotation_euler=(0, 0, 0))

        r_upper_arm = get_bone(armature, "right_upper_arm")
        if r_upper_arm:
            # Opposite to right leg -> same phase as left leg
            swing = RIGHT_ARM_SWING * sin_interp(phase + 0.5)
            set_keyframe(r_upper_arm, frame, rotation_euler=(swing, 0, 0))

        r_lower_arm = get_bone(armature, "right_lower_arm")
        if r_lower_arm:
            # Elbow bends more when arm swings back
            extra = RIGHT_ELBOW_EXTRA * max(0, -sin_interp(phase + 0.5))
            set_keyframe(r_lower_arm, frame,
                         rotation_euler=(ELBOW_BEND_BASE + extra, 0, 0))

        r_hand = get_bone(armature, "right_hand")
        if r_hand:
            set_keyframe(r_hand, frame, rotation_euler=(0, 0, 0))

        # ---- LEFT ARM (musket side, minimal swing, bent elbow) ----
        l_shoulder = get_bone(armature, "left_shoulder")
        if l_shoulder:
            set_keyframe(l_shoulder, frame, rotation_euler=(0, 0, 0))

        l_upper_arm = get_bone(armature, "left_upper_arm")
        if l_upper_arm:
            # Very slight swing (holding musket)
            swing = LEFT_ARM_SWING * sin_interp(phase)
            # Arm slightly raised to hold musket at shoulder
            set_keyframe(l_upper_arm, frame, rotation_euler=(swing - 15, 0, 8))

        l_lower_arm = get_bone(armature, "left_lower_arm")
        if l_lower_arm:
            # Elbow bent to grip musket
            set_keyframe(l_lower_arm, frame,
                         rotation_euler=(LEFT_ELBOW_MUSKET, 0, 0))

        l_hand = get_bone(armature, "left_hand")
        if l_hand:
            # Slight wrist angle for musket grip
            set_keyframe(l_hand, frame, rotation_euler=(10, 0, -5))


# ============================================================
# MAIN EXECUTION
# ============================================================

def main():
    # Validate selection
    obj = bpy.context.active_object
    if not obj or obj.type != 'ARMATURE':
        # Try to find an armature in the scene
        armatures = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE']
        if armatures:
            obj = armatures[0]
            bpy.context.view_layer.objects.active = obj
            print(f"Auto-selected armature: {obj.name}")
        else:
            raise RuntimeError(
                "No armature found! Please add a rigged soldier model first."
            )

    # Enter pose mode
    bpy.ops.object.mode_set(mode='POSE')

    # Create or replace the action
    if ACTION_NAME in bpy.data.actions:
        bpy.data.actions.remove(bpy.data.actions[ACTION_NAME])

    action = bpy.data.actions.new(name=ACTION_NAME)
    obj.animation_data_create()
    obj.animation_data.action = action

    # Set scene frame range and FPS
    bpy.context.scene.render.fps = FPS
    bpy.context.scene.frame_start = FRAME_START
    bpy.context.scene.frame_end = FRAME_END

    # Report which bones were found
    found = []
    missing = []
    for key, name in BONE_MAP.items():
        if name in obj.pose.bones:
            found.append(f"  OK: {key} -> '{name}'")
        else:
            missing.append(f"  MISSING: {key} -> '{name}'")

    print("\n=== Bone Mapping Report ===")
    for line in found:
        print(line)
    for line in missing:
        print(line)
    print(f"\nFound: {len(found)}/{len(BONE_MAP)}")
    if missing:
        print("WARNING: Missing bones will be skipped. "
              "Edit BONE_MAP at the top of the script to match your rig.\n")

    # Generate keyframes
    generate_walk_keyframes(obj)

    # Refresh action reference after keyframe insertion
    action = obj.animation_data.action
    print(f"\nAction: {action.name if action else 'None'}")

    # Set interpolation and loop modifiers
    try:
        curves = list(action.fcurves)
        print(f"F-Curves found: {len(curves)}")
        for fcurve in curves:
            for kf in fcurve.keyframe_points:
                kf.interpolation = 'BEZIER'
                kf.handle_left_type = 'AUTO_CLAMPED'
                kf.handle_right_type = 'AUTO_CLAMPED'
            mod = fcurve.modifiers.new(type='CYCLES')
            mod.mode_before = 'REPEAT'
            mod.mode_after = 'REPEAT'
    except AttributeError:
        print("WARNING: Could not access fcurves. Trying alternative method...")
        try:
            act = bpy.data.actions.get(ACTION_NAME)
            if act:
                curves = list(act.fcurves)
                print(f"F-Curves found (alt): {len(curves)}")
                for fcurve in curves:
                    for kf in fcurve.keyframe_points:
                        kf.interpolation = 'BEZIER'
                        kf.handle_left_type = 'AUTO_CLAMPED'
                        kf.handle_right_type = 'AUTO_CLAMPED'
                    mod = fcurve.modifiers.new(type='CYCLES')
                    mod.mode_before = 'REPEAT'
                    mod.mode_after = 'REPEAT'
        except Exception as e2:
            print(f"Could not set interpolation: {e2}")
            print("Animation keyframes were still created successfully.")

    # Back to frame 1
    bpy.context.scene.frame_set(FRAME_START)

    print(f"\n=== '{ACTION_NAME}' created successfully! ===")
    print(f"  Frames: {FRAME_START}-{FRAME_END} ({FPS} FPS)")
    print(f"  Duration: {(FRAME_END - FRAME_START + 1) / FPS:.2f}s")
    print("  Style: Napoleonic military march")
    print("  Left arm: musket carry position")
    print("  Right arm: natural swing")
    print("\nPress Space to play the animation.")


if __name__ == "__main__":
    main()
