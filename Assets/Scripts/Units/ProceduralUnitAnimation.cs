using UnityEngine;
using System.Collections.Generic;

namespace NapoleonicWars.Units
{
    /// <summary>
    /// Animation state for procedural animation — driven by UnitAnimator.
    /// </summary>
    public enum ProceduralAnimState
    {
        Idle,
        Walking,
        WalkingUnarmed, // Officer walk — arms relaxed, no weapon bob
        Sprinting,      // Charge / flee
        Kneeling,       // Volley fire kneel
        KneelingFire,   // Firing while kneeling
        StandingFire,   // Firing while standing
        Reload,         // After firing, before next shot
        Death,          // Fall and stay down
        Celebrate       // Victory pose
    }

    /// <summary>
    /// Full procedural animation system for Napoleonic soldiers.
    /// 
    /// DUAL MODE:
    ///   1. BONE MODE — if the model has a skeleton, finds arm/leg/spine bones 
    ///      and rotates them for proper posing (idle arms down, walk cycle, etc.)
    ///   2. ROOT-ONLY MODE — if no skeleton, uses amplified root transform movement
    ///      (bob, sway, lean) visible at regiment scale.
    /// 
    /// All animations use per-soldier random phase so units never move in sync.
    /// </summary>
    public class ProceduralUnitAnimation : MonoBehaviour
    {
        // ============================================================
        // TUNING — REGIMENT-VISIBLE amplitudes
        // ============================================================

        [Header("Root Motion - Idle")]
        [SerializeField] private float idleBobAmp = 0.04f;
        [SerializeField] private float idleBobSpeed = 1.0f;
        [SerializeField] private float idleSwayAmp = 2.0f;
        [SerializeField] private float idleSwaySpeed = 0.6f;

        [Header("Root Motion - Walk")]
        [SerializeField] private float walkBobAmp = 0.08f;
        [SerializeField] private float walkBobSpeed = 3.0f;
        [SerializeField] private float walkLeanAngle = 8f;
        [SerializeField] private float walkSwayAmp = 5f;

        [Header("Root Motion - Sprint")]
        [SerializeField] private float sprintBobAmp = 0.12f;
        [SerializeField] private float sprintBobSpeed = 4.5f;
        [SerializeField] private float sprintLeanAngle = 15f;
        [SerializeField] private float sprintSwayAmp = 7f;

        [Header("Kneeling")]
        [SerializeField] private float kneelYDrop = -0.45f;
        [SerializeField] private float kneelTilt = 18f;
        [SerializeField] private float kneelTransitionSpeed = 5f;

        [Header("Firing")]
        [SerializeField] private float fireRecoilDist = 0.15f;
        [SerializeField] private float fireRecoilTilt = 8f;
        [SerializeField] private float fireDuration = 0.4f;

        [Header("Death")]
        [SerializeField] private float deathFallSpeed = 2.5f;
        [SerializeField] private float deathYDrop = -0.7f;

        // ============================================================
        // BONE REFERENCES (auto-detected at Start)
        // ============================================================
        private bool hasBones = false;
        
        // Upper body
        private Transform spine;
        private Transform spine1;
        private Transform spine2;
        private Transform neck;
        private Transform head;
        
        // Left arm
        private Transform leftShoulder;
        private Transform leftUpperArm;
        private Transform leftLowerArm;
        private Transform leftHand;
        
        // Right arm
        private Transform rightShoulder;
        private Transform rightUpperArm;
        private Transform rightLowerArm;
        private Transform rightHand;
        
        // Left leg
        private Transform leftUpperLeg;
        private Transform leftLowerLeg;
        private Transform leftFoot;
        
        // Right leg
        private Transform rightUpperLeg;
        private Transform rightLowerLeg;
        private Transform rightFoot;

        // Base (rest) rotations for bones
        private Dictionary<Transform, Quaternion> boneRestRotations = new Dictionary<Transform, Quaternion>();

        // ============================================================
        // INTERNAL STATE
        // ============================================================
        private Vector3 baseLocalPos;
        private Quaternion baseLocalRot;
        private float phase;         // Random per-unit to desync
        private float phase2;
        private float phase3;

        private ProceduralAnimState currentState = ProceduralAnimState.Idle;
        private ProceduralAnimState previousState = ProceduralAnimState.Idle;
        private float blendWeight = 1f;
        private const float BLEND_SPEED = 6f;

        private float fireTimer = 0f;
        private bool isFiring = false;
        private float deathProgress = 0f;
        private bool isDead = false;
        private float deathSideDir;
        private float kneelingBlend = 0f;
        private bool isKneeling = false;
        private float speedMultiplier = 1f;
        
        private static int boneLogCount = 0;

        // ============================================================
        // LIFECYCLE
        // ============================================================

        private void Start()
        {
            baseLocalPos = transform.localPosition;
            baseLocalRot = transform.localRotation;

            phase = Random.Range(0f, Mathf.PI * 2f);
            phase2 = Random.Range(0f, Mathf.PI * 2f);
            phase3 = Random.Range(0f, Mathf.PI * 2f);
            deathSideDir = Random.value > 0.5f ? 1f : -1f;

            // Auto-detect bones (or hierarchy nodes for rigid body parts)
            FindBones();
        }

        private void FindBones()
        {
            // Destroy any Animator first — it resets bone transforms every frame
            // and overrides our procedural rotations in LateUpdate
            Animator anim = GetComponent<Animator>();
            if (anim == null) anim = GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.enabled = false;
                Destroy(anim);
            }
            
            // Find all transforms in the hierarchy by name
            // Works for both proper Armature bones and Empty-based rigs
            Transform[] all = GetComponentsInChildren<Transform>();
            var lookup = new Dictionary<string, Transform>();
            foreach (Transform t in all)
            {
                string key = t.name.ToLowerInvariant().Trim();
                if (!lookup.ContainsKey(key))
                    lookup[key] = t;
            }
            
            // Map bone transforms by name
            // Names match the Blender auto_rig_soldier.py bone names
            spine = FindFirst(lookup, "hips", "pelvis");
            spine1 = FindFirst(lookup, "spine", "spine02");
            spine2 = FindFirst(lookup, "spine1", "spine01", "spine2", "chest");
            neck = FindFirst(lookup, "neck");
            head = FindFirst(lookup, "head");
            
            leftShoulder = FindFirst(lookup, "leftshoulder");
            leftUpperArm = FindFirst(lookup, "leftarm", "leftupperarm");
            leftLowerArm = FindFirst(lookup, "leftforearm", "leftlowerarm");
            leftHand = FindFirst(lookup, "lefthand");
            
            rightShoulder = FindFirst(lookup, "rightshoulder");
            rightUpperArm = FindFirst(lookup, "rightarm", "rightupperarm");
            rightLowerArm = FindFirst(lookup, "rightforearm", "rightlowerarm");
            rightHand = FindFirst(lookup, "righthand");
            
            leftUpperLeg = FindFirst(lookup, "leftupleg", "leftupperleg");
            leftLowerLeg = FindFirst(lookup, "leftleg", "leftlowerleg");
            leftFoot = FindFirst(lookup, "leftfoot");
            
            rightUpperLeg = FindFirst(lookup, "rightupleg", "rightupperleg");
            rightLowerLeg = FindFirst(lookup, "rightleg", "rightlowerleg");
            rightFoot = FindFirst(lookup, "rightfoot");

            hasBones = (leftUpperArm != null || rightUpperArm != null);

            if (hasBones)
            {
                CacheRest(spine); CacheRest(spine1); CacheRest(spine2);
                CacheRest(neck); CacheRest(head);
                CacheRest(leftShoulder); CacheRest(leftUpperArm); CacheRest(leftLowerArm); CacheRest(leftHand);
                CacheRest(rightShoulder); CacheRest(rightUpperArm); CacheRest(rightLowerArm); CacheRest(rightHand);
                CacheRest(leftUpperLeg); CacheRest(leftLowerLeg); CacheRest(leftFoot);
                CacheRest(rightUpperLeg); CacheRest(rightLowerLeg); CacheRest(rightFoot);

                // Configure SMR for runtime bone updates
                SkinnedMeshRenderer smr = GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null)
                {
                    smr.updateWhenOffscreen = true;
                    smr.forceMatrixRecalculationPerRender = true;
                }

                if (boneLogCount < 3)
                {
                    boneLogCount++;
                    Debug.Log($"[ProceduralAnim] BONES on {gameObject.name}: " +
                        $"spine={spine?.name}, L_arm={leftUpperArm?.name}, " +
                        $"R_arm={rightUpperArm?.name}, L_leg={leftUpperLeg?.name}, " +
                        $"R_leg={rightUpperLeg?.name}, SMR={(smr != null ? smr.name : "none")}");
                }
            }
            else
            {
                if (boneLogCount < 3)
                    Debug.Log($"[ProceduralAnim] NO BONES on {gameObject.name} — root-only mode");
            }
        }
        


        /// <summary>
        /// Find the first matching transform by exact name (case insensitive).
        /// </summary>
        private Transform FindFirst(Dictionary<string, Transform> lookup, params string[] names)
        {
            foreach (string name in names)
            {
                if (lookup.TryGetValue(name, out Transform t))
                    return t;
            }
            return null;
        }

        private void CacheRest(Transform bone)
        {
            if (bone != null && !boneRestRotations.ContainsKey(bone))
                boneRestRotations[bone] = bone.localRotation;
        }

        private Quaternion GetRest(Transform bone)
        {
            if (bone != null && boneRestRotations.TryGetValue(bone, out Quaternion rest))
                return rest;
            return Quaternion.identity;
        }

        // ============================================================
        // MAIN UPDATE
        // ============================================================

        private void Update()
        {
            if (isDead)
            {
                UpdateDeath();
                return;
            }

            float dt = Time.deltaTime;
            float t = Time.time + phase;

            // Blend state transitions
            if (blendWeight < 1f)
                blendWeight = Mathf.MoveTowards(blendWeight, 1f, BLEND_SPEED * dt);

            // Kneeling blend
            float kneelTarget = isKneeling ? 1f : 0f;
            kneelingBlend = Mathf.MoveTowards(kneelingBlend, kneelTarget, kneelTransitionSpeed * dt);

            // Fire timer
            if (isFiring)
            {
                fireTimer -= dt;
                if (fireTimer <= 0f) isFiring = false;
            }

            // ===== ROOT MOTION =====
            Vector3 posOff;
            Quaternion rotOff;
            CalculateRootMotion(t, out posOff, out rotOff);

            // Kneeling additive
            if (kneelingBlend > 0.01f)
            {
                posOff.y += kneelYDrop * kneelingBlend;
                rotOff = rotOff * Quaternion.Euler(kneelTilt * kneelingBlend, 0f, 0f);
            }

            // Fire recoil additive
            if (isFiring)
            {
                float fp = 1f - (fireTimer / fireDuration);
                float rc = fp < 0.15f ? fp / 0.15f : (1f - (fp - 0.15f) / 0.85f);
                posOff.z -= fireRecoilDist * rc;
                posOff.y += 0.02f * rc;
                rotOff = rotOff * Quaternion.Euler(-fireRecoilTilt * rc, 0f, 0f);
            }

            transform.localPosition = baseLocalPos + posOff;
            transform.localRotation = baseLocalRot * rotOff;
        }

        /// <summary>
        /// LateUpdate: apply procedural bone rotations.
        /// These rotations move rigid body-part meshes via transform hierarchy.
        /// </summary>
        private void LateUpdate()
        {
            if (!hasBones) return;
            if (isDead) return;

            float t = Time.time + phase;
            float dt = Time.deltaTime;
            AnimateBones(t, dt);
        }

        // ============================================================
        // ROOT MOTION CALCULATIONS
        // ============================================================

        private void CalculateRootMotion(float t, out Vector3 pos, out Quaternion rot)
        {
            switch (currentState)
            {
                case ProceduralAnimState.Walking:
                    CalculateWalkRoot(t, walkBobAmp, walkBobSpeed, walkLeanAngle, walkSwayAmp, out pos, out rot);
                    break;
                case ProceduralAnimState.WalkingUnarmed:
                    // Officer walk: same rhythm but reduced bob (relaxed stride, no weapon weight)
                    CalculateWalkRoot(t, walkBobAmp * 0.6f, walkBobSpeed, walkLeanAngle * 0.5f, walkSwayAmp * 0.7f, out pos, out rot);
                    break;
                case ProceduralAnimState.Sprinting:
                    CalculateWalkRoot(t, sprintBobAmp, sprintBobSpeed, sprintLeanAngle, sprintSwayAmp, out pos, out rot);
                    break;
                case ProceduralAnimState.Celebrate:
                    float p = t * Mathf.PI * 2f;
                    float bob = Mathf.Abs(Mathf.Sin(p * 2.5f)) * 0.15f;
                    rot = Quaternion.Euler(-12f + Mathf.Sin(p * 1.2f) * 5f, Mathf.Sin(p * 0.6f + phase3) * 8f, Mathf.Sin(p * 1.8f + phase2) * 4f);
                    pos = new Vector3(0f, bob, 0f);
                    break;
                default: // Idle, Kneeling, Fire, Reload, etc.
                    CalculateIdleRoot(t, out pos, out rot);
                    break;
            }
        }

        private void CalculateIdleRoot(float t, out Vector3 pos, out Quaternion rot)
        {
            float bob = Mathf.Sin(t * idleBobSpeed * Mathf.PI * 2f) * idleBobAmp;
            float sway = Mathf.Sin((t + phase2) * idleSwaySpeed * Mathf.PI * 2f) * idleSwayAmp;
            float look = Mathf.Sin((t + phase3) * 0.25f * Mathf.PI * 2f) * 3f;
            float rock = Mathf.Sin(t * 0.4f * Mathf.PI * 2f + phase2) * 1.5f;
            pos = new Vector3(0f, bob, 0f);
            rot = Quaternion.Euler(rock, look, sway);
        }

        private void CalculateWalkRoot(float t, float bobA, float bobS, float lean, float swayA,
                                        out Vector3 pos, out Quaternion rot)
        {
            float speed = bobS * speedMultiplier;
            float p = t * speed * Mathf.PI * 2f;
            float bob = Mathf.Abs(Mathf.Sin(p)) * bobA;
            float lateral = Mathf.Sin(p * 0.5f) * 0.04f;
            float sway = Mathf.Sin(p * 0.5f) * swayA;
            pos = new Vector3(lateral, bob, 0f);
            rot = Quaternion.Euler(lean, 0f, sway);
        }

        // ============================================================
        // BONE ANIMATION — the magic that fixes T-pose!
        // ============================================================

        private void AnimateBones(float t, float dt)
        {
            switch (currentState)
            {
                case ProceduralAnimState.Idle:
                case ProceduralAnimState.Kneeling:
                    PoseIdle(t);
                    break;
                case ProceduralAnimState.Walking:
                    PoseWalk(t, walkBobSpeed);
                    break;
                case ProceduralAnimState.WalkingUnarmed:
                    // Officer: arms relaxed at sides while walking, not holding weapon
                    PoseWalkUnarmed(t, walkBobSpeed);
                    break;
                case ProceduralAnimState.Sprinting:
                    PoseWalk(t, sprintBobSpeed * 1.3f);
                    break;
                case ProceduralAnimState.StandingFire:
                case ProceduralAnimState.KneelingFire:
                    PoseAiming(t);
                    break;
                case ProceduralAnimState.Reload:
                    PoseReload(t);
                    break;
                case ProceduralAnimState.Celebrate:
                    PoseCelebrate(t);
                    break;
            }

            // Fire recoil additive on arms
            if (isFiring)
            {
                float fp = 1f - (fireTimer / fireDuration);
                float rc = fp < 0.15f ? fp / 0.15f : Mathf.Max(0, 1f - (fp - 0.15f) / 0.85f);
                ApplyBoneAdditive(rightUpperArm, Quaternion.Euler(-15f * rc, 0f, 0f));
                ApplyBoneAdditive(leftUpperArm, Quaternion.Euler(-15f * rc, 0f, 0f));
            }
        }

        /// <summary>
        /// IDLE: Arms down at sides, slight breathing sway.
        /// This is the key pose that FIXES the T-pose!
        /// </summary>
        private void PoseIdle(float t)
        {
            float breath = Mathf.Sin(t * 1.0f * Mathf.PI * 2f + phase2);
            
            // ARMS DOWN — rotate upper arms ~70° downward from T-pose
            // The exact axis depends on the model's bone orientation.
            // Most humanoid models: rotating around local Z brings arms down.
            SetBonePose(leftUpperArm, Quaternion.Euler(0f, 0f, 65f));
            SetBonePose(rightUpperArm, Quaternion.Euler(0f, 0f, -65f));
            
            // Slight forearm bend
            SetBonePose(leftLowerArm, Quaternion.Euler(0f, 0f, 8f));
            SetBonePose(rightLowerArm, Quaternion.Euler(0f, 0f, -8f));

            // Subtle spine breathing
            if (spine1 != null)
                SetBonePose(spine1, Quaternion.Euler(breath * 1f, 0f, 0f));

            // Subtle head look
            float look = Mathf.Sin(t * 0.3f * Mathf.PI * 2f + phase3) * 5f;
            if (head != null)
                SetBonePose(head, Quaternion.Euler(0f, look, 0f));
        }

        /// <summary>
        /// WALK: Alternating arm/leg swing.
        /// </summary>
        private void PoseWalk(float t, float speed)
        {
            float p = t * speed * speedMultiplier * Mathf.PI * 2f;
            float swing = Mathf.Sin(p);
            float absSwing = Mathf.Abs(swing);

            // Arm swing — opposite to legs (natural walking)
            float armSwingAngle = swing * 25f; // Forward/back swing
            SetBonePose(leftUpperArm, Quaternion.Euler(armSwingAngle, 0f, 55f));
            SetBonePose(rightUpperArm, Quaternion.Euler(-armSwingAngle, 0f, -55f));

            // Forearm bend during back-swing
            float leftBend = Mathf.Max(0f, swing) * 20f;
            float rightBend = Mathf.Max(0f, -swing) * 20f;
            SetBonePose(leftLowerArm, Quaternion.Euler(0f, -leftBend, 8f));
            SetBonePose(rightLowerArm, Quaternion.Euler(0f, rightBend, -8f));

            // Leg swing
            float legSwing = swing * 20f;
            SetBonePose(leftUpperLeg, Quaternion.Euler(-legSwing, 0f, 0f));
            SetBonePose(rightUpperLeg, Quaternion.Euler(legSwing, 0f, 0f));

            // Knee bend during lift
            float leftKnee = Mathf.Max(0f, -swing) * 30f;
            float rightKnee = Mathf.Max(0f, swing) * 30f;
            SetBonePose(leftLowerLeg, Quaternion.Euler(leftKnee, 0f, 0f));
            SetBonePose(rightLowerLeg, Quaternion.Euler(rightKnee, 0f, 0f));

            // Spine twist with walk
            if (spine1 != null)
                SetBonePose(spine1, Quaternion.Euler(2f, swing * 3f, 0f));

            // Head looks straight
            if (head != null)
                SetBonePose(head, Quaternion.Euler(0f, 0f, 0f));
        }

        /// <summary>
        /// WALK UNARMED: Officer walk — arms swing naturally at sides, no weapon.
        /// More relaxed than soldier walk, arms hang lower.
        /// </summary>
        private void PoseWalkUnarmed(float t, float speed)
        {
            float p = t * speed * speedMultiplier * Mathf.PI * 2f;
            float swing = Mathf.Sin(p);

            // Arms swing naturally at sides — more relaxed, arms hang lower than soldier walk
            float armSwingAngle = swing * 18f; // Smaller swing than armed walk
            SetBonePose(leftUpperArm, Quaternion.Euler(armSwingAngle, 0f, 68f));   // Arms more down
            SetBonePose(rightUpperArm, Quaternion.Euler(-armSwingAngle, 0f, -68f));

            // Slight forearm bend — natural relaxed arms
            float leftBend = Mathf.Max(0f, swing) * 12f;
            float rightBend = Mathf.Max(0f, -swing) * 12f;
            SetBonePose(leftLowerArm, Quaternion.Euler(0f, -leftBend, 5f));
            SetBonePose(rightLowerArm, Quaternion.Euler(0f, rightBend, -5f));

            // Leg swing — same as regular walk
            float legSwing = swing * 20f;
            SetBonePose(leftUpperLeg, Quaternion.Euler(-legSwing, 0f, 0f));
            SetBonePose(rightUpperLeg, Quaternion.Euler(legSwing, 0f, 0f));

            float leftKnee = Mathf.Max(0f, -swing) * 30f;
            float rightKnee = Mathf.Max(0f, swing) * 30f;
            SetBonePose(leftLowerLeg, Quaternion.Euler(leftKnee, 0f, 0f));
            SetBonePose(rightLowerLeg, Quaternion.Euler(rightKnee, 0f, 0f));

            // Spine: officer walks more upright, slight confident twist
            if (spine1 != null)
                SetBonePose(spine1, Quaternion.Euler(-2f, swing * 4f, 0f));

            // Head: officer looks around more (surveying the field)
            float look = Mathf.Sin(t * 0.5f * Mathf.PI * 2f + phase3) * 8f;
            if (head != null)
                SetBonePose(head, Quaternion.Euler(-3f, look, 0f));
        }

        /// <summary>
        /// AIMING: Right arm forward holding musket, left arm supporting.
        /// </summary>
        private void PoseAiming(float t)
        {
            float breathSway = Mathf.Sin(t * 1.5f * Mathf.PI * 2f) * 2f;

            // Right arm: raised and forward (holding musket at shoulder)
            SetBonePose(rightUpperArm, Quaternion.Euler(-60f, 0f, -30f));
            SetBonePose(rightLowerArm, Quaternion.Euler(0f, 60f, 0f));

            // Left arm: supporting the musket barrel
            SetBonePose(leftUpperArm, Quaternion.Euler(-50f, 20f, 40f));
            SetBonePose(leftLowerArm, Quaternion.Euler(0f, -70f, 0f));

            // Lean into the shot
            if (spine1 != null)
                SetBonePose(spine1, Quaternion.Euler(5f + breathSway, 0f, 0f));

            // Head aligned with musket
            if (head != null)
                SetBonePose(head, Quaternion.Euler(-5f, 5f, 0f));
        }

        /// <summary>
        /// RELOAD: Arms working the ramrod — busy motion.
        /// </summary>
        private void PoseReload(float t)
        {
            float p = t * 0.8f * Mathf.PI * 2f;
            float cycle = Mathf.Sin(p);

            // Right arm: ramming motion (up/down)
            float ramAngle = -40f + cycle * 20f;
            SetBonePose(rightUpperArm, Quaternion.Euler(ramAngle, 0f, -20f));
            SetBonePose(rightLowerArm, Quaternion.Euler(0f, 40f + cycle * 15f, 0f));

            // Left arm: holding musket steady
            SetBonePose(leftUpperArm, Quaternion.Euler(-30f, 10f, 45f));
            SetBonePose(leftLowerArm, Quaternion.Euler(0f, -50f, 0f));

            // Body leans forward during ram
            if (spine1 != null)
                SetBonePose(spine1, Quaternion.Euler(6f + cycle * 3f, 0f, 0f));

            if (head != null)
                SetBonePose(head, Quaternion.Euler(-10f, 0f, 0f));
        }

        /// <summary>
        /// CELEBRATE: Arms up, jumping.
        /// </summary>
        private void PoseCelebrate(float t)
        {
            float p = t * 2f * Mathf.PI * 2f;
            float wave = Mathf.Sin(p + phase2) * 15f;

            // Arms up!
            SetBonePose(leftUpperArm, Quaternion.Euler(-30f, 0f, -60f + wave));
            SetBonePose(rightUpperArm, Quaternion.Euler(-30f, 0f, 60f - wave));
            SetBonePose(leftLowerArm, Quaternion.Euler(0f, 0f, -20f));
            SetBonePose(rightLowerArm, Quaternion.Euler(0f, 0f, 20f));

            // Lean back celebrating
            if (spine1 != null)
                SetBonePose(spine1, Quaternion.Euler(-10f, Mathf.Sin(p * 0.5f) * 5f, 0f));
        }

        // ============================================================
        // BONE UTILITIES
        // ============================================================

        private void SetBonePose(Transform bone, Quaternion offset)
        {
            if (bone == null) return;
            bone.localRotation = GetRest(bone) * offset;
        }

        private void ApplyBoneAdditive(Transform bone, Quaternion additive)
        {
            if (bone == null) return;
            bone.localRotation = bone.localRotation * additive;
        }

        // ============================================================
        // DEATH ANIMATION
        // ============================================================

        private void UpdateDeath()
        {
            if (deathProgress >= 1f) return;

            deathProgress += deathFallSpeed * Time.deltaTime;
            deathProgress = Mathf.Clamp01(deathProgress);

            float t = deathProgress * deathProgress;
            float fallAngle = Mathf.Lerp(0f, 90f, t) * deathSideDir;
            float dropY = Mathf.Lerp(0f, deathYDrop, t);
            float crumple = Mathf.Lerp(0f, 20f, t);
            float drift = Mathf.Lerp(0f, 0.15f, t) * deathSideDir;

            transform.localPosition = baseLocalPos + new Vector3(drift, dropY, 0f);
            transform.localRotation = baseLocalRot * Quaternion.Euler(crumple, 0f, fallAngle);

            // Ragdoll-like bone collapse
            if (hasBones && deathProgress < 0.9f)
            {
                float slack = t;
                SetBonePose(leftUpperArm, Quaternion.Euler(0f, 0f, 90f * slack));
                SetBonePose(rightUpperArm, Quaternion.Euler(0f, 0f, -90f * slack));
                SetBonePose(leftLowerArm, Quaternion.Euler(0f, 0f, 30f * slack));
                SetBonePose(rightLowerArm, Quaternion.Euler(0f, 0f, -30f * slack));
                if (head != null) SetBonePose(head, Quaternion.Euler(20f * slack, 15f * slack * deathSideDir, 0f));
            }

            if (deathProgress >= 1f)
                enabled = false;
        }

        // ============================================================
        // PUBLIC API — called by UnitAnimator
        // ============================================================

        public void SetState(ProceduralAnimState newState)
        {
            if (newState == currentState) return;

            if (newState == ProceduralAnimState.Death)
            {
                isDead = true;
                deathProgress = 0f;
                currentState = newState;
                return;
            }

            isKneeling = (newState == ProceduralAnimState.Kneeling ||
                          newState == ProceduralAnimState.KneelingFire);

            previousState = currentState;
            currentState = newState;
            blendWeight = 0f;
        }

        public void TriggerFire()
        {
            isFiring = true;
            fireTimer = fireDuration;
        }

        public void SetSpeedMultiplier(float mult)
        {
            speedMultiplier = Mathf.Clamp(mult, 0.5f, 3f);
        }

        public void ResetPose()
        {
            transform.localPosition = baseLocalPos;
            transform.localRotation = baseLocalRot;

            // Reset all bones to rest pose
            foreach (var kvp in boneRestRotations)
            {
                if (kvp.Key != null)
                    kvp.Key.localRotation = kvp.Value;
            }

            isDead = false;
            deathProgress = 0f;
            isKneeling = false;
            kneelingBlend = 0f;
            isFiring = false;
            currentState = ProceduralAnimState.Idle;
            blendWeight = 1f;
        }

        public void SetMoving(bool moving, float speed = 1f)
        {
            speedMultiplier = speed;
            SetState(moving ? ProceduralAnimState.Walking : ProceduralAnimState.Idle);
        }

        public ProceduralAnimState CurrentAnimState => currentState;
        public bool HasBones => hasBones;
    }
}
