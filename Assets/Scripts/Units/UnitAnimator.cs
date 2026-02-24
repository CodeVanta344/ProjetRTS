using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Units
{
    /// <summary>
    /// Controls unit animations based on UnitBase state.
    /// Uses Unity's Legacy Animation system for simple, reliable playback.
    /// No AnimatorController needed — clips loaded directly from FBX.
    /// </summary>
    public class UnitAnimator : MonoBehaviour
    {
        private Animation anim;
        private UnitBase unit;
        private bool initialized;
        private string currentAnim = "";

        // Optimization: throttle animation updates
        private float updateTimer;
        private const float UPDATE_INTERVAL = 0.1f;

        private UnitState lastState = UnitState.Idle;

        // Kneeling state for volley fire
        private bool isKneeling = false;

        // Cached front rank
        private bool cachedIsInFrontRank;

        private static int logCount = 0;

        private void TryInit()
        {
            if (initialized) return;

            anim = GetComponentInChildren<Animation>();
            unit = GetComponent<UnitBase>();
            if (unit == null)
                unit = GetComponentInParent<UnitBase>();

            if (anim == null || unit == null) return;

            initialized = true;
            cachedIsInFrontRank = ComputeIsInFrontRank();
            updateTimer = Random.Range(0f, UPDATE_INTERVAL);

            if (logCount < 3)
            {
                logCount++;
                int clipCount = 0;
                foreach (AnimationState s in anim) clipCount++;
                Debug.Log($"[UnitAnimator] Legacy Animation OK — {clipCount} clips on {gameObject.name}");
            }

            PlayAnim("idle");
        }

        private void Update()
        {
            if (!initialized)
            {
                TryInit();
                if (!initialized) return;
            }

            updateTimer -= Time.deltaTime;
            if (updateTimer > 0f) return;
            updateTimer = UPDATE_INTERVAL;

            if (anim == null || unit == null) return;

            UnitState state = unit.CurrentState;
            if (state == lastState && state != UnitState.Attacking) return;
            lastState = state;

            bool isRanged = unit.Data != null && unit.Data.attackRange > unit.Data.meleeRange + 1f;
            bool isVolley = unit.Regiment != null && unit.Regiment.IsVolleyMode;
            bool shouldBeKneeling = isVolley && isKneeling;

            switch (state)
            {
                case UnitState.Idle:
                    if (isVolley && isRanged)
                        PlayAnim(shouldBeKneeling ? "kneeling_aim" : "standing_aim");
                    else
                        PlayAnim("idle");
                    break;

                case UnitState.Moving:
                    PlayAnim("walk");
                    break;

                case UnitState.Charging:
                    PlayAnim("charge");
                    break;

                case UnitState.Attacking:
                    if (isRanged)
                        PlayAnim(shouldBeKneeling ? "kneeling_fire" : "standing_fire");
                    else
                        PlayAnim("attack_melee");
                    break;

                case UnitState.Fleeing:
                    PlayAnim("flee");
                    break;

                case UnitState.Dead:
                    PlayAnim("death");
                    break;
            }
        }

        /// <summary>
        /// Called by UnitBase when a ranged attack is performed.
        /// </summary>
        public void TriggerFire()
        {
            bool shouldBeKneeling = unit.Regiment != null && unit.Regiment.IsVolleyMode && isKneeling;
            PlayAnim(shouldBeKneeling ? "kneeling_fire" : "standing_fire", true);
        }

        /// <summary>
        /// Set kneeling state for volley fire (called by UnitBase)
        /// </summary>
        public void SetKneeling(bool kneeling)
        {
            isKneeling = kneeling;
            lastState = UnitState.Idle;
        }

        /// <summary>
        /// Called by UnitBase after firing to start reload animation.
        /// </summary>
        public void TriggerReload()
        {
            PlayAnim("reload", true);
        }

        /// <summary>
        /// Called when reload animation completes.
        /// </summary>
        public void OnReloadComplete()
        {
        }

        private void PlayAnim(string clipName, bool force = false)
        {
            if (!force && currentAnim == clipName) return;
            if (anim == null) return;

            // Check if the clip exists
            if (anim.GetClip(clipName) != null)
            {
                currentAnim = clipName;
                anim.CrossFade(clipName, 0.15f);
            }
        }

        private bool ComputeIsInFrontRank()
        {
            if (unit.Regiment == null) return false;
            var units = unit.Regiment.Units;
            int idx = units.IndexOf(unit);
            if (idx < 0) return false;
            return idx < units.Count / 2;
        }
    }
}
