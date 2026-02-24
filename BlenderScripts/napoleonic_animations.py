"""
Napoleonic Soldier Animations - Blender Script
================================================
Creates all combat animations for the Napoleonic soldier rig:
  - Idle (standing at attention)
  - Walk (march cycle - uses napoleonic_walk_cycle.py logic)
  - Run (double-time march)
  - Attack_Melee (bayonet thrust)
  - Attack_Ranged (musket fire)
  - Death (fall backward)
  - Charge (sword/bayonet charge run)
  - Flee (panicked run)
  - VolleyFire (coordinated fire stance)

Usage:
  1. Open Blender with a rigged soldier (from napoleonic_soldier_generator.py)
  2. Select the armature
  3. Run this script
  4. All animations are created as separate Actions
"""

import bpy
import math
from mathutils import Euler

# ============================================================
# BONE MAP (must match the rig from napoleonic_soldier_generator.py)
# ============================================================

BONE_MAP = {
    "hips": "Hips",
    "spine": "Spine",
    "spine1": "Spine01",
    "spine2": "Spine02",
    "neck": "Neck",
    "head": "Head",
    "left_upper_leg": "LeftUpLeg",
    "left_lower_leg": "LeftLeg",
    "left_foot": "LeftFoot",
    "left_toe": "LeftToeBase",
    "right_upper_leg": "RightUpLeg",
    "right_lower_leg": "RightLeg",
    "right_foot": "RightFoot",
    "right_toe": "RightToeBase",
    "left_shoulder": "LeftShoulder",
    "left_upper_arm": "LeftArm",
    "left_lower_arm": "LeftForeArm",
    "left_hand": "LeftHand",
    "right_shoulder": "RightShoulder",
    "right_upper_arm": "RightArm",
    "right_lower_arm": "RightForeArm",
    "right_hand": "RightHand",
}

FPS = 24


# ============================================================
# HELPERS
# ============================================================

def deg(d):
    return math.radians(d)


def get_bone(armature, key):
    name = BONE_MAP.get(key)
    if name and name in armature.pose.bones:
        return armature.pose.bones[name]
    return None


def set_key(bone, frame, rot=(0, 0, 0), loc=None):
    if bone is None:
        return
    bone.rotation_mode = 'XYZ'
    bone.rotation_euler = Euler([deg(r) for r in rot], 'XYZ')
    bone.keyframe_insert(data_path="rotation_euler", frame=frame)
    if loc is not None:
        bone.location = loc
        bone.keyframe_insert(data_path="location", frame=frame)


def sin(phase):
    return math.sin(phase * 2 * math.pi)


def cos(phase):
    return math.cos(phase * 2 * math.pi)


def create_action(armature, name, frame_start, frame_end):
    """Create or replace an action and assign it."""
    if name in bpy.data.actions:
        bpy.data.actions.remove(bpy.data.actions[name])
    action = bpy.data.actions.new(name=name)
    armature.animation_data_create()
    armature.animation_data.action = action
    return action


def finalize_action(action):
    """Set interpolation and cycle modifiers on all fcurves.
    Compatible with Blender 4.x and 5.0+.
    Gracefully skips if fcurves API is unavailable."""
    if action is None:
        return

    fcurves = None

    # Blender 4.x: action.fcurves exists directly
    if hasattr(action, 'fcurves'):
        try:
            # Test if it's actually iterable (not just an attribute that errors)
            _ = len(action.fcurves)
            fcurves = action.fcurves
        except Exception:
            pass

    # Blender 5.0+: fcurves live in slots > channelbags
    if fcurves is None and hasattr(action, 'slots'):
        try:
            for slot in action.slots:
                for channelbag in slot.channelbags:
                    if hasattr(channelbag, 'fcurves'):
                        fcurves = channelbag.fcurves
                        break
                if fcurves is not None:
                    break
        except Exception:
            pass

    if fcurves is None:
        return

    try:
        for fcurve in fcurves:
            for kf in fcurve.keyframe_points:
                kf.interpolation = 'BEZIER'
                kf.handle_left_type = 'AUTO_CLAMPED'
                kf.handle_right_type = 'AUTO_CLAMPED'
            try:
                mod = fcurve.modifiers.new(type='CYCLES')
                mod.mode_before = 'REPEAT'
                mod.mode_after = 'REPEAT'
            except Exception:
                pass
    except Exception:
        pass


# ============================================================
# ANIMATION: IDLE (Standing at Attention)
# ============================================================

def create_idle(armature):
    """Napoleonic soldier standing at attention with musket."""
    action = create_action(armature, "Idle", 1, 48)
    num_frames = 48

    for f in range(num_frames + 1):
        frame = 1 + f
        phase = f / num_frames
        breath = sin(phase) * 1.0  # Subtle breathing

        set_key(get_bone(armature, "hips"), frame, (0, 0, 0), (0, 0, breath * 0.002))
        set_key(get_bone(armature, "spine"), frame, (-2, 0, 0))  # Chest out
        set_key(get_bone(armature, "spine1"), frame, (-1, 0, 0))
        set_key(get_bone(armature, "spine2"), frame, (-0.5, 0, 0))
        set_key(get_bone(armature, "neck"), frame, (0, 0, 0))
        set_key(get_bone(armature, "head"), frame, (0, sin(phase * 0.5) * 0.5, 0))

        # Legs straight
        set_key(get_bone(armature, "left_upper_leg"), frame, (0, 0, 0))
        set_key(get_bone(armature, "left_lower_leg"), frame, (0, 0, 0))
        set_key(get_bone(armature, "left_foot"), frame, (0, 0, 0))
        set_key(get_bone(armature, "right_upper_leg"), frame, (0, 0, 0))
        set_key(get_bone(armature, "right_lower_leg"), frame, (0, 0, 0))
        set_key(get_bone(armature, "right_foot"), frame, (0, 0, 0))

        # Left arm: musket at shoulder
        set_key(get_bone(armature, "left_upper_arm"), frame, (-15, 0, 8))
        set_key(get_bone(armature, "left_lower_arm"), frame, (45, 0, 0))
        set_key(get_bone(armature, "left_hand"), frame, (10, 0, -5))

        # Right arm: at side
        set_key(get_bone(armature, "right_upper_arm"), frame, (5, 0, -5))
        set_key(get_bone(armature, "right_lower_arm"), frame, (15, 0, 0))
        set_key(get_bone(armature, "right_hand"), frame, (0, 0, 0))

    finalize_action(action)
    print("  Created: Idle (48 frames)")
    return action


# ============================================================
# ANIMATION: WALK (Military March)
# ============================================================

def create_walk(armature):
    """Napoleonic military march - visible disciplined steps."""
    action = create_action(armature, "Walk", 1, 24)
    num_frames = 24

    for f in range(num_frames + 1):
        frame = 1 + f
        phase = f / num_frames

        # Hips - more pronounced sway
        set_key(get_bone(armature, "hips"), frame,
                (0, 5 * sin(phase), 3 * sin(phase)),
                (0, 0, -0.04 * cos(phase * 2)))

        # Spine upright with slight counter-rotation
        set_key(get_bone(armature, "spine"), frame, (-3, -2 * sin(phase), 0))
        set_key(get_bone(armature, "spine1"), frame, (-2, -1.5 * sin(phase), 0))
        set_key(get_bone(armature, "spine2"), frame, (-1, -1 * sin(phase), 0))
        set_key(get_bone(armature, "head"), frame, (0, 2 * sin(phase), 0))

        # Right leg - MUCH bigger stride (45° instead of 22°)
        set_key(get_bone(armature, "right_upper_leg"), frame, (-45 * sin(phase), 0, 0))
        set_key(get_bone(armature, "right_lower_leg"), frame, (60 * max(0, cos(phase * 2 - 0.5)), 0, 0))
        set_key(get_bone(armature, "right_foot"), frame, (25 * sin(phase), 0, 0))

        # Left leg (offset 0.5) - matching bigger stride
        set_key(get_bone(armature, "left_upper_leg"), frame, (-45 * sin(phase + 0.5), 0, 0))
        set_key(get_bone(armature, "left_lower_leg"), frame, (60 * max(0, cos((phase + 0.5) * 2 - 0.5)), 0, 0))
        set_key(get_bone(armature, "left_foot"), frame, (25 * sin(phase + 0.5), 0, 0))

        # Left arm (musket carry) - more movement
        set_key(get_bone(armature, "left_upper_arm"), frame, (10 * sin(phase) - 20, 0, 10))
        set_key(get_bone(armature, "left_lower_arm"), frame, (50 + 5 * sin(phase), 0, 0))
        set_key(get_bone(armature, "left_hand"), frame, (15, 0, -5))

        # Right arm (natural swing) - bigger swing
        set_key(get_bone(armature, "right_upper_arm"), frame, (30 * sin(phase + 0.5), 0, -5))
        r_elbow = 30 + 20 * max(0, -sin(phase + 0.5))
        set_key(get_bone(armature, "right_lower_arm"), frame, (r_elbow, 0, 0))

    finalize_action(action)
    print("  Created: Walk (24 frames)")
    return action


# ============================================================
# ANIMATION: RUN (Double-Time March)
# ============================================================

def create_run(armature):
    """Faster march with more body movement."""
    action = create_action(armature, "Run", 1, 16)
    num_frames = 16

    for f in range(num_frames + 1):
        frame = 1 + f
        phase = f / num_frames

        set_key(get_bone(armature, "hips"), frame,
                (5, 5 * sin(phase), 2.5 * sin(phase)),
                (0, 0, -0.03 * cos(phase * 2)))

        set_key(get_bone(armature, "spine"), frame, (-5, -1.5 * sin(phase), 0))
        set_key(get_bone(armature, "spine1"), frame, (-3, -1 * sin(phase), 0))
        set_key(get_bone(armature, "head"), frame, (3, 1.5 * sin(phase), 0))

        # Legs - bigger stride
        set_key(get_bone(armature, "right_upper_leg"), frame, (-35 * sin(phase), 0, 0))
        set_key(get_bone(armature, "right_lower_leg"), frame, (50 * max(0, cos(phase * 2 - 0.5)), 0, 0))
        set_key(get_bone(armature, "right_foot"), frame, (20 * sin(phase), 0, 0))

        set_key(get_bone(armature, "left_upper_leg"), frame, (-35 * sin(phase + 0.5), 0, 0))
        set_key(get_bone(armature, "left_lower_leg"), frame, (50 * max(0, cos((phase + 0.5) * 2 - 0.5)), 0, 0))
        set_key(get_bone(armature, "left_foot"), frame, (20 * sin(phase + 0.5), 0, 0))

        # Arms pump more
        set_key(get_bone(armature, "left_upper_arm"), frame, (20 * sin(phase) - 10, 0, 5))
        set_key(get_bone(armature, "left_lower_arm"), frame, (40 + 15 * max(0, -sin(phase)), 0, 0))

        set_key(get_bone(armature, "right_upper_arm"), frame, (25 * sin(phase + 0.5), 0, 0))
        set_key(get_bone(armature, "right_lower_arm"), frame, (35 + 15 * max(0, -sin(phase + 0.5)), 0, 0))

    finalize_action(action)
    print("  Created: Run (16 frames)")
    return action


# ============================================================
# ANIMATION: ATTACK_RANGED (Musket Fire)
# ============================================================

def create_attack_ranged(armature):
    """Musket aim and fire sequence."""
    action = create_action(armature, "Attack_Ranged", 1, 36)

    # Frame 1-8: Raise musket to aim
    for f in range(1, 9):
        t = (f - 1) / 7.0
        set_key(get_bone(armature, "hips"), f, (0, 0, 0))
        set_key(get_bone(armature, "spine"), f, (-2 - 3 * t, 0, 0))
        set_key(get_bone(armature, "spine1"), f, (-1 - 2 * t, 0, 0))
        set_key(get_bone(armature, "head"), f, (-5 * t, 0, 0))

        # Left arm raises musket
        set_key(get_bone(armature, "left_upper_arm"), f, (-15 - 55 * t, 0, 8 + 20 * t))
        set_key(get_bone(armature, "left_lower_arm"), f, (45 + 30 * t, 0, 0))
        set_key(get_bone(armature, "left_hand"), f, (10 - 20 * t, 0, -5))

        # Right arm supports
        set_key(get_bone(armature, "right_upper_arm"), f, (5 - 60 * t, 0, -5 - 15 * t))
        set_key(get_bone(armature, "right_lower_arm"), f, (15 + 50 * t, 0, 0))

        # Legs stable
        set_key(get_bone(armature, "left_upper_leg"), f, (-5 * t, 0, 0))
        set_key(get_bone(armature, "right_upper_leg"), f, (5 * t, 0, 0))

    # Frame 9-12: Hold aim
    for f in range(9, 13):
        set_key(get_bone(armature, "hips"), f, (0, 0, 0))
        set_key(get_bone(armature, "spine"), f, (-5, 0, 0))
        set_key(get_bone(armature, "spine1"), f, (-3, 0, 0))
        set_key(get_bone(armature, "head"), f, (-5, 0, 0))
        set_key(get_bone(armature, "left_upper_arm"), f, (-70, 0, 28))
        set_key(get_bone(armature, "left_lower_arm"), f, (75, 0, 0))
        set_key(get_bone(armature, "left_hand"), f, (-10, 0, -5))
        set_key(get_bone(armature, "right_upper_arm"), f, (-55, 0, -20))
        set_key(get_bone(armature, "right_lower_arm"), f, (65, 0, 0))
        set_key(get_bone(armature, "left_upper_leg"), f, (-5, 0, 0))
        set_key(get_bone(armature, "right_upper_leg"), f, (5, 0, 0))

    # Frame 13-16: FIRE! Recoil
    for f in range(13, 17):
        t = (f - 13) / 3.0
        recoil = 8 * t
        set_key(get_bone(armature, "hips"), f, (recoil * 0.5, 0, 0))
        set_key(get_bone(armature, "spine"), f, (-5 + recoil, 0, 0))
        set_key(get_bone(armature, "spine1"), f, (-3 + recoil * 0.5, 0, 0))
        set_key(get_bone(armature, "head"), f, (-5 + recoil * 0.3, 0, 0))
        set_key(get_bone(armature, "left_upper_arm"), f, (-70 + 5 * t, 0, 28))
        set_key(get_bone(armature, "left_lower_arm"), f, (75, 0, 0))
        set_key(get_bone(armature, "right_upper_arm"), f, (-55 + 5 * t, 0, -20))
        set_key(get_bone(armature, "right_lower_arm"), f, (65, 0, 0))

    # Frame 17-36: Lower musket back to carry
    for f in range(17, 37):
        t = (f - 17) / 19.0
        set_key(get_bone(armature, "hips"), f, (4 * (1 - t), 0, 0))
        set_key(get_bone(armature, "spine"), f, (-2 + 1 * (1 - t), 0, 0))
        set_key(get_bone(armature, "spine1"), f, (-1, 0, 0))
        set_key(get_bone(armature, "head"), f, (-2 * (1 - t), 0, 0))
        set_key(get_bone(armature, "left_upper_arm"), f, (-65 * (1 - t) - 15, 0, 28 * (1 - t) + 8))
        set_key(get_bone(armature, "left_lower_arm"), f, (75 * (1 - t) + 45 * t, 0, 0))
        set_key(get_bone(armature, "left_hand"), f, (-10 * (1 - t) + 10 * t, 0, -5))
        set_key(get_bone(armature, "right_upper_arm"), f, (-55 * (1 - t) + 5 * t, 0, -20 * (1 - t) - 5))
        set_key(get_bone(armature, "right_lower_arm"), f, (65 * (1 - t) + 15 * t, 0, 0))
        set_key(get_bone(armature, "left_upper_leg"), f, (-5 * (1 - t), 0, 0))
        set_key(get_bone(armature, "right_upper_leg"), f, (5 * (1 - t), 0, 0))

    # Don't loop this one — just smooth keyframes
    finalize_action(action)

    print("  Created: Attack_Ranged (36 frames)")
    return action


# ============================================================
# ANIMATION: ATTACK_MELEE (Bayonet Thrust)
# ============================================================

def create_attack_melee(armature):
    """Bayonet thrust attack."""
    action = create_action(armature, "Attack_Melee", 1, 20)

    # Frame 1-6: Wind up
    for f in range(1, 7):
        t = (f - 1) / 5.0
        set_key(get_bone(armature, "hips"), f, (0, -5 * t, 0))
        set_key(get_bone(armature, "spine"), f, (-2 + 10 * t, 5 * t, 0))
        set_key(get_bone(armature, "left_upper_arm"), f, (-15 - 30 * t, 0, 8 + 10 * t))
        set_key(get_bone(armature, "left_lower_arm"), f, (45 + 20 * t, 0, 0))
        set_key(get_bone(armature, "right_upper_leg"), f, (-10 * t, 0, 0))
        set_key(get_bone(armature, "left_upper_leg"), f, (5 * t, 0, 0))

    # Frame 7-10: THRUST forward
    for f in range(7, 11):
        t = (f - 7) / 3.0
        set_key(get_bone(armature, "hips"), f, (10 * t, -5 + 10 * t, 0))
        set_key(get_bone(armature, "spine"), f, (8 - 15 * t, 5 - 10 * t, 0))
        set_key(get_bone(armature, "left_upper_arm"), f, (-45 - 25 * t, 0, 18 - 10 * t))
        set_key(get_bone(armature, "left_lower_arm"), f, (65 - 40 * t, 0, 0))
        set_key(get_bone(armature, "right_upper_leg"), f, (-10 - 15 * t, 0, 0))
        set_key(get_bone(armature, "left_upper_leg"), f, (5 + 10 * t, 0, 0))

    # Frame 11-20: Recover
    for f in range(11, 21):
        t = (f - 11) / 9.0
        set_key(get_bone(armature, "hips"), f, (10 * (1 - t), 5 * (1 - t), 0))
        set_key(get_bone(armature, "spine"), f, (-7 * (1 - t) - 2, -5 * (1 - t), 0))
        set_key(get_bone(armature, "left_upper_arm"), f, (-70 * (1 - t) - 15, 0, 8))
        set_key(get_bone(armature, "left_lower_arm"), f, (25 * (1 - t) + 45, 0, 0))
        set_key(get_bone(armature, "right_upper_leg"), f, (-25 * (1 - t), 0, 0))
        set_key(get_bone(armature, "left_upper_leg"), f, (15 * (1 - t), 0, 0))

    finalize_action(action)

    print("  Created: Attack_Melee (20 frames)")
    return action


# ============================================================
# ANIMATION: DEATH (Fall Backward)
# ============================================================

def create_death(armature):
    """Soldier falls backward after being hit."""
    action = create_action(armature, "Death", 1, 30)

    # Frame 1-3: Impact reaction
    for f in range(1, 4):
        t = (f - 1) / 2.0
        set_key(get_bone(armature, "hips"), f, (5 * t, 0, 0), (0, 0, 0))
        set_key(get_bone(armature, "spine"), f, (10 * t, 0, 0))
        set_key(get_bone(armature, "head"), f, (15 * t, 0, 0))
        set_key(get_bone(armature, "left_upper_arm"), f, (-15 + 30 * t, 0, 8 + 20 * t))
        set_key(get_bone(armature, "right_upper_arm"), f, (5 + 25 * t, 0, -5 + 20 * t))

    # Frame 4-15: Fall backward
    for f in range(4, 16):
        t = (f - 4) / 11.0
        fall = t * t  # Accelerating fall

        set_key(get_bone(armature, "hips"), f,
                (5 + 75 * fall, 0, 0),
                (0, 0, -0.5 * fall))
        set_key(get_bone(armature, "spine"), f, (10 + 20 * fall, 3 * sin(t * 0.5), 0))
        set_key(get_bone(armature, "spine1"), f, (5 * fall, 0, 0))
        set_key(get_bone(armature, "head"), f, (15 - 30 * fall, 5 * sin(t), 0))

        # Arms flail
        set_key(get_bone(armature, "left_upper_arm"), f, (15 + 60 * fall, 0, 28 + 30 * fall))
        set_key(get_bone(armature, "left_lower_arm"), f, (20 * (1 - fall), 0, 0))
        set_key(get_bone(armature, "right_upper_arm"), f, (30 + 50 * fall, 0, 15 - 40 * fall))
        set_key(get_bone(armature, "right_lower_arm"), f, (30 * (1 - fall), 0, 0))

        # Legs buckle
        set_key(get_bone(armature, "left_upper_leg"), f, (20 * fall, 0, 0))
        set_key(get_bone(armature, "left_lower_leg"), f, (40 * fall, 0, 0))
        set_key(get_bone(armature, "right_upper_leg"), f, (15 * fall, 0, 5 * fall))
        set_key(get_bone(armature, "right_lower_leg"), f, (35 * fall, 0, 0))

    # Frame 16-30: Lying on ground (hold)
    for f in range(16, 31):
        set_key(get_bone(armature, "hips"), f, (80, 0, 0), (0, 0, -0.5))
        set_key(get_bone(armature, "spine"), f, (30, 0, 0))
        set_key(get_bone(armature, "spine1"), f, (5, 0, 0))
        set_key(get_bone(armature, "head"), f, (-15, 5, 0))
        set_key(get_bone(armature, "left_upper_arm"), f, (75, 0, 58))
        set_key(get_bone(armature, "left_lower_arm"), f, (0, 0, 0))
        set_key(get_bone(armature, "right_upper_arm"), f, (80, 0, -25))
        set_key(get_bone(armature, "right_lower_arm"), f, (0, 0, 0))
        set_key(get_bone(armature, "left_upper_leg"), f, (20, 0, 0))
        set_key(get_bone(armature, "left_lower_leg"), f, (40, 0, 0))
        set_key(get_bone(armature, "right_upper_leg"), f, (15, 0, 5))
        set_key(get_bone(armature, "right_lower_leg"), f, (35, 0, 0))

    finalize_action(action)

    print("  Created: Death (30 frames)")
    return action


# ============================================================
# ANIMATION: CHARGE
# ============================================================

def create_charge(armature):
    """Bayonet charge - fast run with weapon forward."""
    action = create_action(armature, "Charge", 1, 12)
    num_frames = 12

    for f in range(num_frames + 1):
        frame = 1 + f
        phase = f / num_frames

        set_key(get_bone(armature, "hips"), frame,
                (8, 6 * sin(phase), 3 * sin(phase)),
                (0, 0, -0.04 * cos(phase * 2)))

        set_key(get_bone(armature, "spine"), frame, (-8, -2 * sin(phase), 0))
        set_key(get_bone(armature, "spine1"), frame, (-5, -1.5 * sin(phase), 0))
        set_key(get_bone(armature, "head"), frame, (5, 2 * sin(phase), 0))

        # Legs - big stride
        set_key(get_bone(armature, "right_upper_leg"), frame, (-40 * sin(phase), 0, 0))
        set_key(get_bone(armature, "right_lower_leg"), frame, (55 * max(0, cos(phase * 2 - 0.5)), 0, 0))
        set_key(get_bone(armature, "right_foot"), frame, (25 * sin(phase), 0, 0))

        set_key(get_bone(armature, "left_upper_leg"), frame, (-40 * sin(phase + 0.5), 0, 0))
        set_key(get_bone(armature, "left_lower_leg"), frame, (55 * max(0, cos((phase + 0.5) * 2 - 0.5)), 0, 0))
        set_key(get_bone(armature, "left_foot"), frame, (25 * sin(phase + 0.5), 0, 0))

        # Left arm: musket thrust forward
        set_key(get_bone(armature, "left_upper_arm"), frame, (-50, 0, 15))
        set_key(get_bone(armature, "left_lower_arm"), frame, (30, 0, 0))

        # Right arm pumps
        set_key(get_bone(armature, "right_upper_arm"), frame, (30 * sin(phase + 0.5), 0, -5))
        set_key(get_bone(armature, "right_lower_arm"), frame, (40 + 15 * max(0, -sin(phase + 0.5)), 0, 0))

    finalize_action(action)
    print("  Created: Charge (12 frames)")
    return action


# ============================================================
# ANIMATION: STANDING AIM (Hold musket aimed - looping)
# ============================================================

def create_standing_aim(armature):
    """Soldier holds musket aimed at shoulder height, ready to fire.
    Looping subtle breathing while aiming."""
    action = create_action(armature, "Standing_Aim", 1, 36)
    num_frames = 36

    for f in range(num_frames + 1):
        frame = 1 + f
        phase = f / num_frames
        breath = sin(phase) * 0.5  # Very subtle breathing while aiming

        # Body: slight lean forward, stable stance
        set_key(get_bone(armature, "hips"), frame, (0, 0, 0), (0, 0, breath * 0.001))
        set_key(get_bone(armature, "spine"), frame, (-5, 0, 0))
        set_key(get_bone(armature, "spine1"), frame, (-3, 0, 0))
        set_key(get_bone(armature, "spine2"), frame, (-2, 0, 0))
        set_key(get_bone(armature, "neck"), frame, (-2, 0, 0))
        set_key(get_bone(armature, "head"), frame, (-5 + breath * 0.3, 0, 0))  # Sighting down barrel

        # Legs: wide stable stance
        set_key(get_bone(armature, "left_upper_leg"), frame, (-5, 0, -3))
        set_key(get_bone(armature, "left_lower_leg"), frame, (5, 0, 0))
        set_key(get_bone(armature, "left_foot"), frame, (0, 0, 0))
        set_key(get_bone(armature, "right_upper_leg"), frame, (5, 0, 3))
        set_key(get_bone(armature, "right_lower_leg"), frame, (3, 0, 0))
        set_key(get_bone(armature, "right_foot"), frame, (0, 0, 0))

        # Left arm: supports musket barrel (front hand)
        set_key(get_bone(armature, "left_shoulder"), frame, (0, 0, 5))
        set_key(get_bone(armature, "left_upper_arm"), frame, (-70, 0, 28))
        set_key(get_bone(armature, "left_lower_arm"), frame, (75, 0, 0))
        set_key(get_bone(armature, "left_hand"), frame, (-10, 0, -5))

        # Right arm: trigger hand at stock (rear hand)
        set_key(get_bone(armature, "right_shoulder"), frame, (0, 0, -5))
        set_key(get_bone(armature, "right_upper_arm"), frame, (-55, 0, -20))
        set_key(get_bone(armature, "right_lower_arm"), frame, (65, 0, 0))
        set_key(get_bone(armature, "right_hand"), frame, (-5, 10, 0))

    finalize_action(action)
    print("  Created: Standing_Aim (36 frames, looping)")
    return action


# ============================================================
# ANIMATION: STANDING FIRE (Fire from standing with recoil)
# ============================================================

def create_standing_fire(armature):
    """Quick fire from standing aim position.
    Frames: 1-4 tense, 5-8 FIRE + recoil, 9-20 recover to aim."""
    action = create_action(armature, "Standing_Fire", 1, 20)

    # Frame 1-4: Tense up before firing (hold aim, squeeze trigger)
    for f in range(1, 5):
        t = (f - 1) / 3.0
        set_key(get_bone(armature, "hips"), f, (0, 0, 0))
        set_key(get_bone(armature, "spine"), f, (-5, 0, 0))
        set_key(get_bone(armature, "spine1"), f, (-3, 0, 0))
        set_key(get_bone(armature, "head"), f, (-5 - t, 0, 0))  # Lean into sight

        set_key(get_bone(armature, "left_upper_arm"), f, (-70, 0, 28))
        set_key(get_bone(armature, "left_lower_arm"), f, (75, 0, 0))
        set_key(get_bone(armature, "left_hand"), f, (-10, 0, -5))
        set_key(get_bone(armature, "right_upper_arm"), f, (-55, 0, -20))
        set_key(get_bone(armature, "right_lower_arm"), f, (65, 0, 0))
        set_key(get_bone(armature, "right_hand"), f, (-5 - 3 * t, 10, 0))  # Squeeze

        set_key(get_bone(armature, "left_upper_leg"), f, (-5, 0, -3))
        set_key(get_bone(armature, "right_upper_leg"), f, (5, 0, 3))

    # Frame 5-8: FIRE! HEAVY recoil - very visible
    for f in range(5, 9):
        t = (f - 5) / 3.0
        recoil = 25 * t  # Much bigger body recoil
        shoulder_kick = 20 * t  # Much bigger shoulder kick

        set_key(get_bone(armature, "hips"), f, (recoil * 0.5, 0, -recoil * 0.2))
        set_key(get_bone(armature, "spine"), f, (-5 + recoil, 0, 0))
        set_key(get_bone(armature, "spine1"), f, (-3 + recoil * 0.8, 0, 0))
        set_key(get_bone(armature, "spine2"), f, (-2 + recoil * 0.5, 0, 0))
        set_key(get_bone(armature, "head"), f, (-6 + recoil * 0.7, 3 * t, 0))  # Head jerks

        # Arms absorb recoil — musket kicks UP dramatically
        set_key(get_bone(armature, "left_upper_arm"), f, (-70 + shoulder_kick * 1.5, 0, 28 - 10 * t))
        set_key(get_bone(armature, "left_lower_arm"), f, (75 - 15 * t, 0, 0))
        set_key(get_bone(armature, "left_hand"), f, (-10 + 20 * t, 0, -5))  # Hand kicks up
        set_key(get_bone(armature, "right_upper_arm"), f, (-55 + shoulder_kick, 0, -20 + 8 * t))
        set_key(get_bone(armature, "right_lower_arm"), f, (65 - 10 * t, 0, 0))
        set_key(get_bone(armature, "right_hand"), f, (-8, 10 + 15 * t, 5 * t))

        # Legs brace against recoil
        set_key(get_bone(armature, "left_upper_leg"), f, (-5 - 5 * t, 0, -3))
        set_key(get_bone(armature, "right_upper_leg"), f, (5 + 5 * t, 0, 3))

    # Frame 9-20: Recover back to aim position (from bigger recoil)
    for f in range(9, 21):
        t = (f - 9) / 11.0
        t_smooth = t * t * (3 - 2 * t)  # Smoothstep

        # Recover from the heavy recoil position
        set_key(get_bone(armature, "hips"), f, (12.5 * (1 - t_smooth), 0, -5 * (1 - t_smooth)))
        set_key(get_bone(armature, "spine"), f, (20 * (1 - t_smooth) - 5, 0, 0))
        set_key(get_bone(armature, "spine1"), f, (17 * (1 - t_smooth) - 3, 0, 0))
        set_key(get_bone(armature, "spine2"), f, (10.5 * (1 - t_smooth) - 2, 0, 0))
        set_key(get_bone(armature, "head"), f, (11.5 * (1 - t_smooth) - 6, 3 * (1 - t_smooth), 0))

        # Arms recover
        set_key(get_bone(armature, "left_upper_arm"), f, (-70 + 30 * (1 - t_smooth), 0, 28 - 10 * (1 - t_smooth)))
        set_key(get_bone(armature, "left_lower_arm"), f, (75 - 15 * (1 - t_smooth), 0, 0))
        set_key(get_bone(armature, "left_hand"), f, (-10 + 20 * (1 - t_smooth), 0, -5))
        set_key(get_bone(armature, "right_upper_arm"), f, (-55 + 20 * (1 - t_smooth), 0, -20 + 8 * (1 - t_smooth)))
        set_key(get_bone(armature, "right_lower_arm"), f, (65 - 10 * (1 - t_smooth), 0, 0))

        # Legs recover
        set_key(get_bone(armature, "left_upper_leg"), f, (-5 - 5 * (1 - t_smooth), 0, -3))
        set_key(get_bone(armature, "right_upper_leg"), f, (5 + 5 * (1 - t_smooth), 0, 3))

    finalize_action(action)
    print("  Created: Standing_Fire (20 frames)")
    return action


# ============================================================
# ANIMATION: KNEELING AIM (Kneel and aim - looping)
# ============================================================

def create_kneeling_aim(armature):
    """Soldier kneels on right knee, aims musket. Front rank volley pose.
    Looping with subtle breathing."""
    action = create_action(armature, "Kneeling_Aim", 1, 36)
    num_frames = 36

    for f in range(num_frames + 1):
        frame = 1 + f
        phase = f / num_frames
        breath = sin(phase) * 0.4

        # Hips: lowered, slight forward lean
        set_key(get_bone(armature, "hips"), frame, (15, 0, 0), (0, 0, -0.25 + breath * 0.001))
        set_key(get_bone(armature, "spine"), frame, (-15, 0, 0))
        set_key(get_bone(armature, "spine1"), frame, (-8, 0, 0))
        set_key(get_bone(armature, "spine2"), frame, (-3, 0, 0))
        set_key(get_bone(armature, "neck"), frame, (-3, 0, 0))
        set_key(get_bone(armature, "head"), frame, (-5 + breath * 0.2, 0, 0))

        # Right leg: kneeling (knee on ground)
        set_key(get_bone(armature, "right_upper_leg"), frame, (80, 0, 5))
        set_key(get_bone(armature, "right_lower_leg"), frame, (100, 0, 0))
        set_key(get_bone(armature, "right_foot"), frame, (-40, 0, 0))
        set_key(get_bone(armature, "right_toe"), frame, (30, 0, 0))

        # Left leg: forward, foot flat (supporting)
        set_key(get_bone(armature, "left_upper_leg"), frame, (-50, 0, -5))
        set_key(get_bone(armature, "left_lower_leg"), frame, (70, 0, 0))
        set_key(get_bone(armature, "left_foot"), frame, (-10, 0, 0))

        # Left arm: supports musket barrel
        set_key(get_bone(armature, "left_shoulder"), frame, (0, 0, 5))
        set_key(get_bone(armature, "left_upper_arm"), frame, (-65, 0, 25))
        set_key(get_bone(armature, "left_lower_arm"), frame, (70, 0, 0))
        set_key(get_bone(armature, "left_hand"), frame, (-10, 0, -5))

        # Right arm: trigger hand
        set_key(get_bone(armature, "right_shoulder"), frame, (0, 0, -5))
        set_key(get_bone(armature, "right_upper_arm"), frame, (-50, 0, -18))
        set_key(get_bone(armature, "right_lower_arm"), frame, (60, 0, 0))
        set_key(get_bone(armature, "right_hand"), frame, (-5, 8, 0))

    finalize_action(action)
    print("  Created: Kneeling_Aim (36 frames, looping)")
    return action


# ============================================================
# ANIMATION: KNEELING FIRE (Fire from kneeling)
# ============================================================

def create_kneeling_fire(armature):
    """Fire from kneeling position. Recoil pushes soldier back slightly.
    Frames: 1-3 tense, 4-7 FIRE + recoil, 8-18 recover."""
    action = create_action(armature, "Kneeling_Fire", 1, 18)

    # Frame 1-3: Hold kneeling aim, tense
    for f in range(1, 4):
        t = (f - 1) / 2.0
        set_key(get_bone(armature, "hips"), f, (15, 0, 0), (0, 0, -0.25))
        set_key(get_bone(armature, "spine"), f, (-15, 0, 0))
        set_key(get_bone(armature, "spine1"), f, (-8, 0, 0))
        set_key(get_bone(armature, "head"), f, (-5 - t, 0, 0))

        set_key(get_bone(armature, "right_upper_leg"), f, (80, 0, 5))
        set_key(get_bone(armature, "right_lower_leg"), f, (100, 0, 0))
        set_key(get_bone(armature, "right_foot"), f, (-40, 0, 0))
        set_key(get_bone(armature, "left_upper_leg"), f, (-50, 0, -5))
        set_key(get_bone(armature, "left_lower_leg"), f, (70, 0, 0))
        set_key(get_bone(armature, "left_foot"), f, (-10, 0, 0))

        set_key(get_bone(armature, "left_upper_arm"), f, (-65, 0, 25))
        set_key(get_bone(armature, "left_lower_arm"), f, (70, 0, 0))
        set_key(get_bone(armature, "right_upper_arm"), f, (-50, 0, -18))
        set_key(get_bone(armature, "right_lower_arm"), f, (60, 0, 0))

    # Frame 4-7: FIRE! Recoil
    for f in range(4, 8):
        t = (f - 4) / 3.0
        recoil = 10 * t

        set_key(get_bone(armature, "hips"), f, (15 + recoil * 0.4, 0, 0), (0, 0, -0.25))
        set_key(get_bone(armature, "spine"), f, (-15 + recoil, 0, 0))
        set_key(get_bone(armature, "spine1"), f, (-8 + recoil * 0.5, 0, 0))
        set_key(get_bone(armature, "head"), f, (-6 + recoil * 0.4, 0, 0))

        # Arms absorb recoil
        set_key(get_bone(armature, "left_upper_arm"), f, (-65 + 6 * t, 0, 25))
        set_key(get_bone(armature, "left_lower_arm"), f, (70 - 4 * t, 0, 0))
        set_key(get_bone(armature, "right_upper_arm"), f, (-50 + 6 * t, 0, -18))
        set_key(get_bone(armature, "right_lower_arm"), f, (60 - 4 * t, 0, 0))

        # Legs stay planted
        set_key(get_bone(armature, "right_upper_leg"), f, (80, 0, 5))
        set_key(get_bone(armature, "right_lower_leg"), f, (100, 0, 0))
        set_key(get_bone(armature, "left_upper_leg"), f, (-50, 0, -5))
        set_key(get_bone(armature, "left_lower_leg"), f, (70, 0, 0))

    # Frame 8-18: Recover to kneeling aim
    for f in range(8, 19):
        t = (f - 8) / 10.0
        t_smooth = t * t * (3 - 2 * t)

        set_key(get_bone(armature, "hips"), f, (19 * (1 - t_smooth) + 15 * t_smooth, 0, 0), (0, 0, -0.25))
        set_key(get_bone(armature, "spine"), f, (-5 * (1 - t_smooth) + (-15) * t_smooth, 0, 0))
        set_key(get_bone(armature, "spine1"), f, (-3 * (1 - t_smooth) + (-8) * t_smooth, 0, 0))
        set_key(get_bone(armature, "head"), f, (-2 * (1 - t_smooth) + (-5) * t_smooth, 0, 0))

        set_key(get_bone(armature, "left_upper_arm"), f, (-59 * (1 - t_smooth) + (-65) * t_smooth, 0, 25))
        set_key(get_bone(armature, "left_lower_arm"), f, (66 * (1 - t_smooth) + 70 * t_smooth, 0, 0))
        set_key(get_bone(armature, "right_upper_arm"), f, (-44 * (1 - t_smooth) + (-50) * t_smooth, 0, -18))
        set_key(get_bone(armature, "right_lower_arm"), f, (56 * (1 - t_smooth) + 60 * t_smooth, 0, 0))

        set_key(get_bone(armature, "right_upper_leg"), f, (80, 0, 5))
        set_key(get_bone(armature, "right_lower_leg"), f, (100, 0, 0))
        set_key(get_bone(armature, "left_upper_leg"), f, (-50, 0, -5))
        set_key(get_bone(armature, "left_lower_leg"), f, (70, 0, 0))

    finalize_action(action)
    print("  Created: Kneeling_Fire (18 frames)")
    return action


# ============================================================
# ANIMATION: RELOAD (Full musket reload sequence)
# ============================================================

def create_reload(armature):
    """Full musket reload: bite cartridge, pour powder, ram ball, return to aim.
    Historically accurate ~15 second process compressed to 72 frames (3s at 24fps)."""
    action = create_action(armature, "Reload", 1, 72)

    # Phase 1 (Frame 1-12): Bring musket down to waist, half-cock
    for f in range(1, 13):
        t = (f - 1) / 11.0
        set_key(get_bone(armature, "hips"), f, (0, 0, 0))
        set_key(get_bone(armature, "spine"), f, (-2, 0, 0))
        set_key(get_bone(armature, "spine1"), f, (-1, 0, 0))
        set_key(get_bone(armature, "head"), f, (5 * t, 0, 0))  # Look down at musket

        # Left arm: bring musket to waist level
        set_key(get_bone(armature, "left_upper_arm"), f, (-70 + 40 * t, 0, 28 - 15 * t))
        set_key(get_bone(armature, "left_lower_arm"), f, (75 - 30 * t, 0, 0))
        set_key(get_bone(armature, "left_hand"), f, (-10 + 15 * t, 0, -5))

        # Right arm: move to cartridge box at hip
        set_key(get_bone(armature, "right_upper_arm"), f, (-55 + 60 * t, 0, -20 + 15 * t))
        set_key(get_bone(armature, "right_lower_arm"), f, (65 - 25 * t, 0, 0))
        set_key(get_bone(armature, "right_hand"), f, (-5 + 15 * t, 10 * (1 - t), 0))

        set_key(get_bone(armature, "left_upper_leg"), f, (-3, 0, 0))
        set_key(get_bone(armature, "right_upper_leg"), f, (3, 0, 0))

    # Phase 2 (Frame 13-24): Bite cartridge, pour powder into pan
    for f in range(13, 25):
        t = (f - 13) / 11.0
        set_key(get_bone(armature, "hips"), f, (0, 0, 0))
        set_key(get_bone(armature, "spine"), f, (-2, 0, 0))
        set_key(get_bone(armature, "head"), f, (5 + 10 * t, 0, 0))  # Look further down

        # Left arm: holds musket at waist
        set_key(get_bone(armature, "left_upper_arm"), f, (-30, 0, 13))
        set_key(get_bone(armature, "left_lower_arm"), f, (45, 0, 0))
        set_key(get_bone(armature, "left_hand"), f, (5, 0, -5))

        # Right arm: bring cartridge to mouth then to muzzle
        bite_up = max(0, 1 - 2 * t)  # First half: up to mouth
        pour_down = max(0, 2 * t - 1)  # Second half: down to muzzle
        set_key(get_bone(armature, "right_upper_arm"), f,
                (5 - 60 * bite_up - 20 * pour_down, 0, -5 - 10 * bite_up))
        set_key(get_bone(armature, "right_lower_arm"), f,
                (40 + 40 * bite_up + 10 * pour_down, 0, 0))
        set_key(get_bone(armature, "right_hand"), f, (10 - 20 * bite_up, 0, 0))

    # Phase 3 (Frame 25-48): Ram ball down barrel (ramrod motion)
    for f in range(25, 49):
        t = (f - 25) / 23.0
        # Ramming is a repeated up-down motion
        ram_cycle = math.sin(t * math.pi * 3)  # 3 ram strokes
        ram_amount = max(0, ram_cycle)

        set_key(get_bone(armature, "hips"), f, (0, 0, 0))
        set_key(get_bone(armature, "spine"), f, (-2 - 3 * ram_amount, 0, 0))
        set_key(get_bone(armature, "head"), f, (15 - 5 * ram_amount, 0, 0))

        # Left arm: holds musket steady
        set_key(get_bone(armature, "left_upper_arm"), f, (-30, 0, 13))
        set_key(get_bone(armature, "left_lower_arm"), f, (45, 0, 0))

        # Right arm: ramrod motion (up and down)
        set_key(get_bone(armature, "right_upper_arm"), f,
                (-25 - 40 * ram_amount, 0, -5))
        set_key(get_bone(armature, "right_lower_arm"), f,
                (50 + 20 * ram_amount, 0, 0))
        set_key(get_bone(armature, "right_hand"), f, (-10 * ram_amount, 0, 0))

    # Phase 4 (Frame 49-72): Return ramrod, bring musket back to aim
    for f in range(49, 73):
        t = (f - 49) / 23.0
        t_smooth = t * t * (3 - 2 * t)

        set_key(get_bone(armature, "hips"), f, (0, 0, 0))
        set_key(get_bone(armature, "spine"), f, (-2 - 3 * (1 - t_smooth), 0, 0))
        set_key(get_bone(armature, "spine1"), f, (-1 - 2 * (1 - t_smooth), 0, 0))
        set_key(get_bone(armature, "head"), f, (15 * (1 - t_smooth) + (-5) * t_smooth, 0, 0))

        # Left arm: raise back to aim
        set_key(get_bone(armature, "left_upper_arm"), f,
                (-30 * (1 - t_smooth) + (-70) * t_smooth, 0, 13 * (1 - t_smooth) + 28 * t_smooth))
        set_key(get_bone(armature, "left_lower_arm"), f,
                (45 * (1 - t_smooth) + 75 * t_smooth, 0, 0))
        set_key(get_bone(armature, "left_hand"), f,
                (5 * (1 - t_smooth) + (-10) * t_smooth, 0, -5))

        # Right arm: return to trigger position
        set_key(get_bone(armature, "right_upper_arm"), f,
                (-25 * (1 - t_smooth) + (-55) * t_smooth, 0, -5 * (1 - t_smooth) + (-20) * t_smooth))
        set_key(get_bone(armature, "right_lower_arm"), f,
                (50 * (1 - t_smooth) + 65 * t_smooth, 0, 0))
        set_key(get_bone(armature, "right_hand"), f,
                (0 * (1 - t_smooth) + (-5) * t_smooth, 10 * t_smooth, 0))

        set_key(get_bone(armature, "left_upper_leg"), f, (-3 * (1 - t_smooth) + (-5) * t_smooth, 0, -3 * t_smooth))
        set_key(get_bone(armature, "right_upper_leg"), f, (3 * (1 - t_smooth) + 5 * t_smooth, 0, 3 * t_smooth))

    finalize_action(action)
    print("  Created: Reload (72 frames)")
    return action


# ============================================================
# ANIMATION: VOLLEY FIRE (Coordinated line fire stance)
# ============================================================

def create_volley_fire(armature):
    """Volley fire stance — soldier in line formation, fires on command.
    Wider stance than individual fire, more upright for line visibility.
    Frames: 1-6 present, 7-10 aim, 11-14 FIRE, 15-24 recover."""
    action = create_action(armature, "VolleyFire", 1, 24)

    # Frame 1-6: Present arms (bring musket up from shoulder)
    for f in range(1, 7):
        t = (f - 1) / 5.0
        set_key(get_bone(armature, "hips"), f, (0, 0, 0))
        set_key(get_bone(armature, "spine"), f, (-2 - 2 * t, 0, 0))
        set_key(get_bone(armature, "spine1"), f, (-1 - t, 0, 0))
        set_key(get_bone(armature, "head"), f, (-3 * t, 0, 0))

        set_key(get_bone(armature, "left_upper_arm"), f, (-15 - 50 * t, 0, 8 + 18 * t))
        set_key(get_bone(armature, "left_lower_arm"), f, (45 + 25 * t, 0, 0))
        set_key(get_bone(armature, "left_hand"), f, (10 - 18 * t, 0, -5))
        set_key(get_bone(armature, "right_upper_arm"), f, (5 - 55 * t, 0, -5 - 13 * t))
        set_key(get_bone(armature, "right_lower_arm"), f, (15 + 45 * t, 0, 0))

        # Wider stance for stability
        set_key(get_bone(armature, "left_upper_leg"), f, (-3 * t, 0, -4 * t))
        set_key(get_bone(armature, "right_upper_leg"), f, (3 * t, 0, 4 * t))

    # Frame 7-10: Hold aim (steady)
    for f in range(7, 11):
        set_key(get_bone(armature, "hips"), f, (0, 0, 0))
        set_key(get_bone(armature, "spine"), f, (-4, 0, 0))
        set_key(get_bone(armature, "spine1"), f, (-2, 0, 0))
        set_key(get_bone(armature, "head"), f, (-3, 0, 0))
        set_key(get_bone(armature, "left_upper_arm"), f, (-65, 0, 26))
        set_key(get_bone(armature, "left_lower_arm"), f, (70, 0, 0))
        set_key(get_bone(armature, "left_hand"), f, (-8, 0, -5))
        set_key(get_bone(armature, "right_upper_arm"), f, (-50, 0, -18))
        set_key(get_bone(armature, "right_lower_arm"), f, (60, 0, 0))
        set_key(get_bone(armature, "left_upper_leg"), f, (-3, 0, -4))
        set_key(get_bone(armature, "right_upper_leg"), f, (3, 0, 4))

    # Frame 11-14: FIRE! Recoil
    for f in range(11, 15):
        t = (f - 11) / 3.0
        recoil = 10 * t
        set_key(get_bone(armature, "hips"), f, (recoil * 0.3, 0, 0))
        set_key(get_bone(armature, "spine"), f, (-4 + recoil, 0, 0))
        set_key(get_bone(armature, "spine1"), f, (-2 + recoil * 0.5, 0, 0))
        set_key(get_bone(armature, "head"), f, (-3 + recoil * 0.4, 0, 0))
        set_key(get_bone(armature, "left_upper_arm"), f, (-65 + 6 * t, 0, 26))
        set_key(get_bone(armature, "left_lower_arm"), f, (70 - 4 * t, 0, 0))
        set_key(get_bone(armature, "right_upper_arm"), f, (-50 + 6 * t, 0, -18))
        set_key(get_bone(armature, "right_lower_arm"), f, (60 - 4 * t, 0, 0))
        set_key(get_bone(armature, "left_upper_leg"), f, (-3 - t, 0, -4))
        set_key(get_bone(armature, "right_upper_leg"), f, (3 + t, 0, 4))

    # Frame 15-24: Lower musket back to carry
    for f in range(15, 25):
        t = (f - 15) / 9.0
        t_smooth = t * t * (3 - 2 * t)

        set_key(get_bone(armature, "hips"), f, (3 * (1 - t_smooth), 0, 0))
        set_key(get_bone(armature, "spine"), f, (6 * (1 - t_smooth) - 2, 0, 0))
        set_key(get_bone(armature, "spine1"), f, (3 * (1 - t_smooth) - 1, 0, 0))
        set_key(get_bone(armature, "head"), f, (1 * (1 - t_smooth), 0, 0))

        set_key(get_bone(armature, "left_upper_arm"), f,
                (-59 * (1 - t_smooth) + (-15) * t_smooth, 0, 26 * (1 - t_smooth) + 8 * t_smooth))
        set_key(get_bone(armature, "left_lower_arm"), f,
                (66 * (1 - t_smooth) + 45 * t_smooth, 0, 0))
        set_key(get_bone(armature, "left_hand"), f,
                (-8 * (1 - t_smooth) + 10 * t_smooth, 0, -5))
        set_key(get_bone(armature, "right_upper_arm"), f,
                (-44 * (1 - t_smooth) + 5 * t_smooth, 0, -18 * (1 - t_smooth) + (-5) * t_smooth))
        set_key(get_bone(armature, "right_lower_arm"), f,
                (56 * (1 - t_smooth) + 15 * t_smooth, 0, 0))

        set_key(get_bone(armature, "left_upper_leg"), f, (-4 * (1 - t_smooth), 0, -4 * (1 - t_smooth)))
        set_key(get_bone(armature, "right_upper_leg"), f, (4 * (1 - t_smooth), 0, 4 * (1 - t_smooth)))

    finalize_action(action)
    print("  Created: VolleyFire (24 frames)")
    return action


# ============================================================
# ANIMATION: PRESENT ARMS (Ceremonial ready position)
# ============================================================

def create_present_arms(armature):
    """Present Arms — musket held vertically in front of chest.
    Used as transition pose and formation ready state.
    Looping with subtle sway."""
    action = create_action(armature, "Present_Arms", 1, 36)
    num_frames = 36

    for f in range(num_frames + 1):
        frame = 1 + f
        phase = f / num_frames
        sway = sin(phase) * 0.5

        set_key(get_bone(armature, "hips"), frame, (0, 0, 0), (0, 0, sway * 0.001))
        set_key(get_bone(armature, "spine"), frame, (-3, 0, 0))
        set_key(get_bone(armature, "spine1"), frame, (-2, 0, 0))
        set_key(get_bone(armature, "spine2"), frame, (-1, 0, 0))
        set_key(get_bone(armature, "neck"), frame, (0, 0, 0))
        set_key(get_bone(armature, "head"), frame, (0, sway * 0.3, 0))

        # Legs: at attention
        set_key(get_bone(armature, "left_upper_leg"), frame, (0, 0, 0))
        set_key(get_bone(armature, "left_lower_leg"), frame, (0, 0, 0))
        set_key(get_bone(armature, "left_foot"), frame, (0, 0, 0))
        set_key(get_bone(armature, "right_upper_leg"), frame, (0, 0, 0))
        set_key(get_bone(armature, "right_lower_leg"), frame, (0, 0, 0))
        set_key(get_bone(armature, "right_foot"), frame, (0, 0, 0))

        # Left arm: holds musket at mid-chest, barrel up
        set_key(get_bone(armature, "left_upper_arm"), frame, (-30, 0, 15))
        set_key(get_bone(armature, "left_lower_arm"), frame, (60, 0, 0))
        set_key(get_bone(armature, "left_hand"), frame, (0, 0, -5))

        # Right arm: holds stock at chest
        set_key(get_bone(armature, "right_upper_arm"), frame, (-25, 0, -12))
        set_key(get_bone(armature, "right_lower_arm"), frame, (55, 0, 0))
        set_key(get_bone(armature, "right_hand"), frame, (0, 5, 0))

    finalize_action(action)
    print("  Created: Present_Arms (36 frames, looping)")
    return action


# ============================================================
# ANIMATION: FLEE (Panicked run)
# ============================================================

def create_flee(armature):
    """Panicked fleeing run — arms flailing, looking back."""
    action = create_action(armature, "Flee", 1, 14)
    num_frames = 14

    for f in range(num_frames + 1):
        frame = 1 + f
        phase = f / num_frames

        # Hips: hunched, erratic
        set_key(get_bone(armature, "hips"), frame,
                (10, 8 * sin(phase), 4 * sin(phase)),
                (0, 0, -0.04 * cos(phase * 2)))

        set_key(get_bone(armature, "spine"), frame, (-10, -3 * sin(phase), 2 * sin(phase)))
        set_key(get_bone(armature, "spine1"), frame, (-5, -2 * sin(phase), 0))
        set_key(get_bone(armature, "head"), frame, (5, 15 * sin(phase * 0.5), 0))  # Looking back

        # Legs: frantic running
        set_key(get_bone(armature, "right_upper_leg"), frame, (-38 * sin(phase), 0, 0))
        set_key(get_bone(armature, "right_lower_leg"), frame, (50 * max(0, cos(phase * 2 - 0.5)), 0, 0))
        set_key(get_bone(armature, "right_foot"), frame, (22 * sin(phase), 0, 0))

        set_key(get_bone(armature, "left_upper_leg"), frame, (-38 * sin(phase + 0.5), 0, 0))
        set_key(get_bone(armature, "left_lower_leg"), frame, (50 * max(0, cos((phase + 0.5) * 2 - 0.5)), 0, 0))
        set_key(get_bone(armature, "left_foot"), frame, (22 * sin(phase + 0.5), 0, 0))

        # Arms: flailing, no longer holding musket properly
        set_key(get_bone(armature, "left_upper_arm"), frame,
                (25 * sin(phase) - 5, 0, 15 + 10 * sin(phase)))
        set_key(get_bone(armature, "left_lower_arm"), frame,
                (30 + 20 * max(0, -sin(phase)), 0, 0))

        set_key(get_bone(armature, "right_upper_arm"), frame,
                (30 * sin(phase + 0.5), 0, -10 - 8 * sin(phase + 0.5)))
        set_key(get_bone(armature, "right_lower_arm"), frame,
                (35 + 15 * max(0, -sin(phase + 0.5)), 0, 0))

    finalize_action(action)
    print("  Created: Flee (14 frames)")
    return action


# ============================================================
# MAIN
# ============================================================

def main():
    obj = bpy.context.active_object
    if not obj or obj.type != 'ARMATURE':
        armatures = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE']
        if armatures:
            obj = armatures[0]
            bpy.context.view_layer.objects.active = obj
            print(f"Auto-selected armature: {obj.name}")
        else:
            raise RuntimeError("No armature found! Generate a soldier first.")

    bpy.ops.object.mode_set(mode='POSE')

    print("\n" + "=" * 50)
    print("  NAPOLEONIC ANIMATION GENERATOR")
    print("=" * 50)

    # Check bone mapping
    found = sum(1 for k, v in BONE_MAP.items() if v in obj.pose.bones)
    print(f"\nBones found: {found}/{len(BONE_MAP)}")

    # Create all animations
    create_idle(obj)
    create_walk(obj)
    create_run(obj)
    create_attack_ranged(obj)
    create_attack_melee(obj)
    create_death(obj)
    create_charge(obj)
    create_standing_aim(obj)
    create_standing_fire(obj)
    create_kneeling_aim(obj)
    create_kneeling_fire(obj)
    create_reload(obj)
    create_volley_fire(obj)
    create_present_arms(obj)
    create_flee(obj)

    # Set back to idle
    if "Idle" in bpy.data.actions:
        obj.animation_data.action = bpy.data.actions["Idle"]

    bpy.context.scene.frame_set(1)

    print(f"\n=== 15 animations created! ===")
    print("Actions: Idle, Walk, Run, Attack_Ranged, Attack_Melee, Death, Charge,")
    print("         Standing_Aim, Standing_Fire, Kneeling_Aim, Kneeling_Fire,")
    print("         Reload, VolleyFire, Present_Arms, Flee")
    print("\nExport as FBX with 'Bake Animation' and 'All Actions' checked.")


if __name__ == "__main__":
    main()
