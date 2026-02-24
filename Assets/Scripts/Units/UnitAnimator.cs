using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Units
{
    /// <summary>
    /// Controls unit animations based on UnitBase state.
    /// Supports:
    ///   - ProceduralUnitAnimation (for models with no skeleton, e.g. French Infantry)
    ///   - Legacy Animation (for units using Legacy FBX clips)
    ///   - Mecanim Animator (for units with AnimatorController)
    /// Auto-detects which system is available.
    /// </summary>
    public class UnitAnimator : MonoBehaviour
    {
        private Animation legacyAnim;
        private Animator mecanimAnimator;
        private ProceduralUnitAnimation proceduralAnim;
        private UnitBase unit;
        private bool initialized;
        private string currentAnim = "";
        private bool useMecanim;
        private bool useProcedural;

        // Optimization: throttle animation updates
        private float updateTimer;
        private const float UPDATE_INTERVAL = 0.1f;

        private UnitState lastState = UnitState.Idle;

        // Kneeling state for volley fire
        private bool isKneeling = false;

        private static int logCount = 0;

        private void TryInit()
        {
            if (initialized) return;

            unit = GetComponent<UnitBase>();
            if (unit == null)
                unit = GetComponentInParent<UnitBase>();
            if (unit == null) return;

            // Try ProceduralUnitAnimation first (French Infantry uses this)
            proceduralAnim = GetComponentInChildren<ProceduralUnitAnimation>();
            if (proceduralAnim != null)
            {
                useProcedural = true;
                initialized = true;
                
                if (logCount < 3)
                {
                    logCount++;
                    Debug.Log($"[UnitAnimator] ProceduralUnitAnimation found on {gameObject.name}");
                }
                
                updateTimer = Random.Range(0f, UPDATE_INTERVAL);
                return;
            }

            // Also check old ProceduralIdleAnimation (backwards compat)
            var oldProcedural = GetComponentInChildren<ProceduralIdleAnimation>();
            if (oldProcedural != null)
            {
                // Upgrade: replace old component with new one
                var go = oldProcedural.gameObject;
                Destroy(oldProcedural);
                proceduralAnim = go.AddComponent<ProceduralUnitAnimation>();
                useProcedural = true;
                initialized = true;
                updateTimer = Random.Range(0f, UPDATE_INTERVAL);
                return;
            }

            // Try Mecanim Animator
            mecanimAnimator = GetComponentInChildren<Animator>();
            if (mecanimAnimator != null && mecanimAnimator.runtimeAnimatorController != null)
            {
                useMecanim = true;
                initialized = true;
                updateTimer = Random.Range(0f, UPDATE_INTERVAL);
                return;
            }

            // Fallback to Legacy Animation
            legacyAnim = GetComponentInChildren<Animation>();
            if (legacyAnim != null)
            {
                useMecanim = false;
                useProcedural = false;
                initialized = true;
                
                if (logCount < 3)
                {
                    logCount++;
                    int clipCount = 0;
                    foreach (AnimationState s in legacyAnim) clipCount++;
                    Debug.Log($"[UnitAnimator] Legacy Animation — {clipCount} clips on {gameObject.name}");
                }
                
                updateTimer = Random.Range(0f, UPDATE_INTERVAL);
                PlayAnim("idle");
                return;
            }

            // Nothing found — will retry next frame
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

            if (unit == null) return;

            UnitState state = unit.CurrentState;
            if (state == lastState && state != UnitState.Attacking) return;
            lastState = state;

            if (useProcedural)
            {
                UpdateProceduralAnimation(state);
                return;
            }

            // Mecanim or Legacy path
            UpdateClipAnimation(state);
        }

        // ============================================================
        // PROCEDURAL ANIMATION DRIVER
        // ============================================================

        private void UpdateProceduralAnimation(UnitState state)
        {
            if (proceduralAnim == null) return;

            bool isRanged = unit.Data != null && unit.Data.attackRange > unit.Data.meleeRange + 1f;

            switch (state)
            {
                case UnitState.Idle:
                    if (isKneeling)
                        proceduralAnim.SetState(ProceduralAnimState.Kneeling);
                    else
                        proceduralAnim.SetState(ProceduralAnimState.Idle);
                    break;

                case UnitState.Moving:
                    proceduralAnim.SetSpeedMultiplier(1f);
                    proceduralAnim.SetState(ProceduralAnimState.Walking);
                    break;

                case UnitState.Charging:
                    proceduralAnim.SetSpeedMultiplier(1.5f);
                    proceduralAnim.SetState(ProceduralAnimState.Sprinting);
                    break;

                case UnitState.Attacking:
                    if (isRanged)
                    {
                        if (isKneeling)
                        {
                            proceduralAnim.SetState(ProceduralAnimState.KneelingFire);
                            proceduralAnim.TriggerFire();
                        }
                        else
                        {
                            proceduralAnim.SetState(ProceduralAnimState.StandingFire);
                            proceduralAnim.TriggerFire();
                        }
                    }
                    else
                    {
                        // Melee attack: sprint forward + periodic recoil (sword swing)
                        proceduralAnim.SetState(ProceduralAnimState.Sprinting);
                        proceduralAnim.TriggerFire(); // Reuse recoil as "strike" motion
                    }
                    break;

                case UnitState.Fleeing:
                    proceduralAnim.SetSpeedMultiplier(2f);
                    proceduralAnim.SetState(ProceduralAnimState.Sprinting);
                    break;

                case UnitState.Dead:
                    proceduralAnim.SetState(ProceduralAnimState.Death);
                    break;
            }
        }

        // ============================================================
        // CLIP-BASED ANIMATION PATH (Mecanim / Legacy)
        // ============================================================

        private void UpdateClipAnimation(UnitState state)
        {
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

        // ============================================================
        // PUBLIC API — called by UnitBase
        // ============================================================

        public void TriggerFire()
        {
            if (useProcedural && proceduralAnim != null)
            {
                proceduralAnim.TriggerFire();
                return;
            }

            bool shouldBeKneeling = unit.Regiment != null && unit.Regiment.IsVolleyMode && isKneeling;
            PlayAnim(shouldBeKneeling ? "kneeling_fire" : "standing_fire", true);
        }

        public void SetKneeling(bool kneeling)
        {
            isKneeling = kneeling;
            lastState = UnitState.Idle; // Force update on next tick

            if (useProcedural && proceduralAnim != null)
            {
                proceduralAnim.SetState(kneeling
                    ? ProceduralAnimState.Kneeling
                    : ProceduralAnimState.Idle);
            }
        }

        public void TriggerReload()
        {
            if (useProcedural && proceduralAnim != null)
            {
                proceduralAnim.SetState(ProceduralAnimState.Reload);
                return;
            }
            PlayAnim("reload", true);
        }

        public void OnReloadComplete() { }

        // ============================================================
        // CLIP PLAYBACK
        // ============================================================

        private void PlayAnim(string clipName, bool force = false)
        {
            if (!force && currentAnim == clipName) return;

            if (useMecanim)
            {
                if (mecanimAnimator == null || mecanimAnimator.runtimeAnimatorController == null) return;
                if (mecanimAnimator.HasState(0, Animator.StringToHash(clipName)))
                {
                    currentAnim = clipName;
                    mecanimAnimator.CrossFadeInFixedTime(clipName, 0.15f);
                }
            }
            else if (legacyAnim != null)
            {
                if (legacyAnim.GetClip(clipName) != null)
                {
                    currentAnim = clipName;
                    legacyAnim.CrossFade(clipName, 0.15f);
                }
            }
        }
    }
}
