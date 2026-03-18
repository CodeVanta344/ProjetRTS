using UnityEngine;
using NapoleonicWars.Core;
using NapoleonicWars.Data;
using NapoleonicWars.Battlefield;

namespace NapoleonicWars.Units
{
    public enum UnitState
    {
        Idle,
        Moving,
        Charging,
        Attacking,
        Retreating,  // Orderly withdrawal - can be rallied
        Fleeing,      // Full rout - cannot be rallied
        Dead
    }

    public enum AttackDirection
    {
        Front,
        Flank,
        Rear
    }

    public class UnitBase : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private UnitData unitData;

        [Header("Runtime State")]
        private float currentHealth;
        private float currentMorale;
        private UnitState currentState = UnitState.Idle;
        private Vector3 targetPosition;
        private UnitBase targetEnemy;
        private float attackTimer;
        private float volleyTimer;
        private bool isSelected;
        private bool isInVolleyMode;
        private float chargeStartDistance;
        private float lodTimer;
        private float enemySearchTimer;
        private float terrainSnapTimer;
        private float moraleTimer;
        private float rendererCullTimer;
        private bool isRendererCulled;

        // === CACHED TECH MODIFIERS — computed once at spawn, avoids dictionary lookups every call ===
        private float cachedDamageMultiplier = 1f;
        private float cachedAccuracyBonus = 0f;
        private float cachedSpeedMultiplier = 1f;
        private float cachedMoraleBonus = 0f;
        private float cachedRangeMultiplier = 1f;
        private float cachedReloadMultiplier = 1f;
        private bool cachedIsRanged = false;

        // === CACHED RANK MODIFIERS — set once via ApplyRankBonuses ===
        private RankStatModifiers rankMods = RankStatModifiers.Identity;
        private int appliedRank = 0;

        // Fatigue system
        private float currentStamina;

        // Ammunition system
        private int currentAmmo;

        // Experience system
        private float experience;

        // Cover system
        private bool isInCover = false;

        // Officer flag — the first unit in a regiment is the officer
        public bool IsOfficer { get; set; } = false;

        // Suppression system
        private float currentSuppression = 0f;  // 0-100, reduces effectiveness under fire

        // Public accessors
        public UnitData Data => unitData;
        public float CurrentHealth => currentHealth;
        public float CurrentMorale => currentMorale;
        public UnitState CurrentState => currentState;
        public bool IsSelected => isSelected;
        public bool IsInVolleyMode => isInVolleyMode;
        public float CurrentStamina => currentStamina;
        public float StaminaPercent => unitData != null ? currentStamina / unitData.maxStamina : 1f;
        public int CurrentAmmo => currentAmmo;
        public bool HasAmmo => unitData != null && (unitData.hasUnlimitedAmmo || currentAmmo > 0);
        public float Experience => experience;
        public bool IsVeteran => unitData != null && experience >= unitData.veteranThreshold;
        public bool IsElite => unitData != null && experience >= unitData.eliteThreshold;
        public Regiment Regiment { get; set; }
        public int TeamId { get; set; } = 0;
        public Vector3 TargetPosition => targetPosition;
        public bool IsRetreating => currentState == UnitState.Retreating;
        public bool IsRouted => currentState == UnitState.Fleeing;
        public bool CanBeRallied => currentState == UnitState.Retreating;
        public float CurrentSuppression => currentSuppression;
        public float SuppressionPercent => currentSuppression / 100f;

        // Volley fire state
        private bool isKneeling = false;
        private float reloadTimer = 0f;
        public bool IsReloaded => reloadTimer <= 0f;

        // Visual
        private Renderer unitRenderer;
        private Color originalColor;
        private static readonly Color selectedHighlight = new Color(0.3f, 1f, 0.3f, 1f);
        private float formationBaseSpeed = 0f;
        
        // Material property block for zero-allocation color changes
        private MaterialPropertyBlock mpb;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        // Firing range visualizer - moved to Regiment level (one per regiment, not per soldier)
        private FiringRangeVisualizer rangeVisualizer;

        // Animation
        private UnitAnimator unitAnimator;
        
        // Physics components for ragdoll
        private Rigidbody rb;
        private Collider unitCollider;
        private bool isRagdollActive = false;

        private void Awake()
        {
            unitRenderer = GetComponentInChildren<Renderer>();
            if (unitRenderer != null)
            {
                // Use sharedMaterial to avoid creating material instance
                originalColor = unitRenderer.sharedMaterial.color;
                mpb = new MaterialPropertyBlock();
            }
            unitAnimator = GetComponentInChildren<UnitAnimator>();
            
            // Setup physics components
            SetupPhysics();
        }
        
        private void SetupPhysics()
        {
            // Get or add Rigidbody
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            
            // Configure Rigidbody for character physics (kinematic during life)
            rb.isKinematic = true;
            rb.useGravity = true;
            rb.mass = 70f; // Average human mass
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;
            rb.interpolation = RigidbodyInterpolation.None; // Interpolate doubles physics cost — units move via transform
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            
            // Get or add Collider
            unitCollider = GetComponent<Collider>();
            if (unitCollider == null)
            {
                // Add capsule collider for humanoid shape
                CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
                capsule.height = 1.8f;
                capsule.radius = 0.3f;
                capsule.center = new Vector3(0f, 0.9f, 0f);
                unitCollider = capsule;
            }
        }

        private void Start()
        {
            if (unitData != null)
            {
                currentHealth = unitData.maxHealth;
                currentMorale = unitData.maxMorale;
                currentStamina = unitData.maxStamina;
                currentAmmo = unitData.maxAmmo;
                experience = unitData.startingExperience;
                cachedIsRanged = unitData.attackRange > unitData.meleeRange + 1f;
            }

            // Cache technology modifiers ONCE — they never change mid-battle
            CacheTechnologyModifiers();

            // Register with spatial grid
            if (SpatialGrid.Instance != null)
                SpatialGrid.Instance.RegisterUnit(this);

            // Stagger timers so not all units update on the same frame
            float stagger = Random.Range(0f, 0.5f);
            lodTimer = stagger * 0.1f;
            enemySearchTimer = stagger;
            terrainSnapTimer = stagger * 0.4f;
            moraleTimer = stagger * 0.5f;
        }
        
        /// <summary>
        /// Cache technology modifiers once at spawn — avoids dictionary lookups every GetEffective* call.
        /// With 5000+ units calling 6 GetEffective methods repeatedly, this saves ~30k+ dictionary lookups/s.
        /// </summary>
        private void CacheTechnologyModifiers()
        {
            if (UnitTechnologySystem.Instance == null || unitData == null) return;
            
            var modifiers = UnitTechnologySystem.Instance.GetModifiersForUnit(unitData.unitType);
            cachedDamageMultiplier = 1f + modifiers.damageBonus;
            cachedAccuracyBonus = modifiers.accuracyBonus;
            cachedSpeedMultiplier = 1f + modifiers.speedBonus;
            cachedMoraleBonus = modifiers.moraleBonus;
            cachedRangeMultiplier = 1f + modifiers.rangeBonus;
            cachedReloadMultiplier = 1f + modifiers.reloadSpeedBonus;
        }

        /// <summary>
        /// Legacy API — calls CacheTechnologyModifiers internally.
        /// </summary>
        public void ApplyTechnologyModifiers()
        {
            CacheTechnologyModifiers();
        }
        
        public float GetEffectiveAttackDamage()
        {
            if (unitData == null) return 25f;
            return unitData.attackDamage * cachedDamageMultiplier * rankMods.damageMultiplier;
        }
        
        public float GetEffectiveAccuracy()
        {
            if (unitData == null) return 0.75f;
            float acc = Mathf.Clamp01(unitData.accuracy + cachedAccuracyBonus + rankMods.accuracyBonus);
            // Weather affects accuracy (rain degrades musket fire, fog limits visibility, etc.)
            if (WeatherSystem.Instance != null)
                acc *= WeatherSystem.Instance.AccuracyModifier;
            return Mathf.Clamp01(acc);
        }
        
        public float GetEffectiveMoveSpeed()
        {
            if (unitData == null) return 3.5f;
            float speed = unitData.moveSpeed * cachedSpeedMultiplier * rankMods.speedMultiplier * GetFatigueMultiplier(unitData.fatigueSpeedPenalty);
            // Weather affects movement speed (snow slows troops, mud from rain, etc.)
            if (WeatherSystem.Instance != null)
                speed *= WeatherSystem.Instance.MovementModifier;
            // Suppression slows movement (troops take cover, hesitate to advance)
            speed *= Mathf.Lerp(1f, 0.7f, currentSuppression / 100f);
            return speed;
        }
        
        public float GetEffectiveMaxMorale()
        {
            if (unitData == null) return 100f;
            float baseMorale = unitData.maxMorale + cachedMoraleBonus + rankMods.moraleMaxBonus;
            baseMorale += unitData.experienceMoraleBonus * (experience / 100f);
            return baseMorale;
        }

        public float GetEffectiveMeleeDamage()
        {
            if (unitData == null) return 15f;
            return unitData.meleeDamage * rankMods.meleeDamageMultiplier;
        }

        public float GetEffectiveFleeThreshold()
        {
            if (unitData == null) return 20f;
            return Mathf.Max(5f, unitData.fleeThreshold - rankMods.fleeThresholdReduction);
        }

        public float GetEffectiveMoraleRecovery()
        {
            if (unitData == null) return 1f;
            return unitData.moraleRecoveryRate * rankMods.moraleRecoveryMult;
        }
        
        public bool IsRangedUnit()
        {
            return cachedIsRanged;
        }

        public float GetEffectiveAttackRange()
        {
            if (unitData == null) return 250f;
            return unitData.attackRange * cachedRangeMultiplier;
        }
        
        public float GetEffectiveAttackCooldown()
        {
            if (unitData == null) return 3f;
            return Mathf.Max(unitData.attackCooldown * cachedReloadMultiplier, 0.5f);
        }

        private void OnDestroy()
        {
            if (SpatialGrid.Instance != null)
                SpatialGrid.Instance.UnregisterUnit(this);
        }

        private void Update()
        {
            if (currentState == UnitState.Dead) return;

            float dt = Time.deltaTime;

            // === RENDERER CULLING — hide units too far from camera (every 0.5s) ===
            rendererCullTimer -= dt;
            if (rendererCullTimer <= 0f)
            {
                rendererCullTimer = 0.5f;
                if (UnitLODManager.Instance != null && unitRenderer != null)
                {
                    bool shouldCull = UnitLODManager.Instance.ShouldDisableRenderer(transform.position);
                    if (shouldCull != isRendererCulled)
                    {
                        isRendererCulled = shouldCull;
                        unitRenderer.enabled = !shouldCull;
                    }
                }
            }

            // Movement MUST run every frame to avoid teleportation
            // Only throttle expensive logic (enemy search, morale, etc.)
            switch (currentState)
            {
                case UnitState.Moving:
                    UpdateMoving(dt);
                    break;
                case UnitState.Charging:
                    UpdateCharging(dt);
                    break;
                case UnitState.Retreating:
                    UpdateRetreating(dt);
                    break;
                case UnitState.Fleeing:
                    UpdateFleeing(dt);
                    break;
            }

            // LOD-based throttling for expensive logic only
            lodTimer -= dt;
            if (lodTimer > 0f)
                return;
            if (UnitLODManager.Instance != null)
            {
                float baseDelay = UnitLODManager.Instance.GetUpdateDelay(transform.position);
                lodTimer = baseDelay * Random.Range(0.9f, 1.1f);
            }

            // Expensive per-tick logic (throttled by LOD)
            switch (currentState)
            {
                case UnitState.Idle:
                    UpdateIdle();
                    break;
                case UnitState.Attacking:
                    UpdateAttacking();
                    break;
            }

            // Throttle morale + stamina + volley — every 0.25s instead of per-frame
            moraleTimer -= dt;
            if (moraleTimer <= 0f)
            {
                moraleTimer = 0.25f;
                UpdateMorale();
                UpdateStamina();
                UpdateVolleyTimer();
                UpdateSuppression(0.25f);
            }
        }

        private void SnapToTerrainSmooth()
        {
            float targetY = NapoleonicWars.Core.BattleManager.GetTerrainHeight(transform.position);
            Vector3 pos = transform.position;
            pos.y = Mathf.Lerp(pos.y, targetY, 0.3f); // Smooth transition
            transform.position = pos;
        }

        /// <summary>
        /// Update target position during regiment sliding formation movement.
        /// Unlike MoveTo, this only updates the target without resetting state
        /// (avoids Idle/Moving flickering when regiment pushes new targets each frame).
        /// </summary>
        public void UpdateFormationTarget(Vector3 position, float baseSpeed)
        {
            if (currentState == UnitState.Dead) return;
            targetPosition = position;
            formationBaseSpeed = baseSpeed;
            if (currentState != UnitState.Moving)
            {
                targetEnemy = null;
                currentState = UnitState.Moving;
            }
        }



        public void AttackTarget(UnitBase enemy)
        {
            if (currentState == UnitState.Dead) return;
            targetEnemy = enemy;
            currentState = UnitState.Attacking;
        }

        public void ChargeTarget(UnitBase enemy)
        {
            if (currentState == UnitState.Dead) return;
            if (unitData != null && !unitData.canCharge) { AttackTarget(enemy); return; }

            targetEnemy = enemy;
            chargeStartDistance = Vector3.Distance(transform.position, enemy.transform.position);
            currentState = UnitState.Charging;

            if (BattleSoundManager.Instance != null && Random.value < 0.15f)
                BattleSoundManager.Instance.PlayChargeShout(transform.position);
        }

        public void AttackMoveTo(Vector3 position)
        {
            if (currentState == UnitState.Dead) return;
            targetPosition = position;
            currentState = UnitState.Moving;
        }

        public void SetVolleyMode(bool enabled)
        {
            isInVolleyMode = enabled;
        }

        /// <summary>
        /// Set kneeling state for volley fire. Kneeling units stand up when reload finishes.
        /// </summary>
        public void SetKneeling(bool kneeling)
        {
            isKneeling = kneeling;
            
            // Notify animator
            if (unitAnimator != null)
            {
                unitAnimator.SetKneeling(kneeling);
            }
            
            // Start reload timer when kneeling after fire
            if (kneeling && unitData != null)
            {
                reloadTimer = GetEffectiveAttackCooldown();
            }
        }

        private AttackDirection GetAttackDirection(UnitBase target)
        {
            if (target == null) return AttackDirection.Front;
            
            Vector3 toAttacker = (transform.position - target.transform.position).normalized;
            Vector3 targetForward = target.transform.forward;
            
            float dot = Vector3.Dot(targetForward, toAttacker);
            
            if (dot < -0.5f) return AttackDirection.Rear; // attacker is behind
            if (dot < 0.5f) return AttackDirection.Flank;  // attacker is on the side
            return AttackDirection.Front;                  // attacker is in front
        }

        private float GetDirectionMultiplier(AttackDirection attackDir)
        {
            switch (attackDir)
            {
                case AttackDirection.Rear:
                    return unitData != null ? unitData.rearDamageMultiplier : 2.0f;
                case AttackDirection.Flank:
                    return unitData != null ? unitData.flankingDamageMultiplier : 1.5f;
                default:
                    return 1.0f;
            }
        }

        private void UpdateIdle()
        {
            // No combat during deployment phase
            if (BattleManager.Instance != null && BattleManager.Instance.IsDeploymentPhase)
                return;

            // Player melee units stay put until ordered — but ranged units auto-fire at targets in range
            if (TeamId == 0)
            {
                // Player units HOLD POSITION — they only fire if enemy comes within range
                // They never move from their formation slot
                if (!cachedIsRanged) return; // Melee units wait for orders
                
                // Ranged player units: auto-acquire targets in range but NEVER move
                enemySearchTimer -= Time.deltaTime;
                if (targetEnemy == null && enemySearchTimer <= 0f)
                {
                    enemySearchTimer = 0.5f;
                    FindNearestEnemy();
                }
                
                if (targetEnemy != null && targetEnemy.CurrentState == UnitState.Dead)
                    targetEnemy = null;
                
                if (targetEnemy != null && IsInAttackRange(targetEnemy))
                {
                    // Attack but DON'T move — stay in formation slot
                    currentState = UnitState.Attacking;
                }
                else
                {
                    // Target out of range — just drop it, don't chase
                    targetEnemy = null;
                }
                return;
            }

            // AI units (enemy team) auto-seek enemies
            // Throttle enemy search — every 0.5s instead of every frame
            enemySearchTimer -= Time.deltaTime;
            if (targetEnemy == null && enemySearchTimer <= 0f)
            {
                enemySearchTimer = 0.5f;
                FindNearestEnemy();
            }

            // Validate target is still alive
            if (targetEnemy != null && targetEnemy.CurrentState == UnitState.Dead)
                targetEnemy = null;

            if (targetEnemy != null && IsInAttackRange(targetEnemy))
            {
                currentState = UnitState.Attacking;
            }
        }

        private void UpdateMoving(float dt)
        {
            float dx = targetPosition.x - transform.position.x;
            float dz = targetPosition.z - transform.position.z;
            float distSqr = dx * dx + dz * dz;

            if (distSqr < 0.04f) // 0.2^2 — Snug fit in slot
            {
                if (Regiment != null && !Regiment.IsRegimentMoving)
                {
                    Vector3 snapPos = transform.position;
                    snapPos.x = targetPosition.x;
                    snapPos.z = targetPosition.z;
                    transform.position = snapPos;
                    currentState = UnitState.Idle;
                }
                else
                {
                    // We are at the moving slot, follow the regiment perfectly
                    transform.position = targetPosition; // Hard lock to slot
                }
                
                // Align to regiment facing
                if (Regiment != null)
                {
                    Vector3 regFwd = Regiment.FacingDirection;
                    if (regFwd.sqrMagnitude > 0.01f)
                        transform.forward = Vector3.Lerp(transform.forward, regFwd, dt * 10f);
                }
                return;
            }

            float invDist = 1f / Mathf.Sqrt(distSqr);
            float dirX = dx * invDist;
            float dirZ = dz * invDist;

            // Speed calculation:
            // If the regiment is moving, the slot is moving. We must run to catch up if we are far.
            float unitMaxSpeed = GetEffectiveMoveSpeed();
            float moveSpeed = unitMaxSpeed;
            
            if (Regiment != null && Regiment.IsRegimentMoving)
            {
                // If we are relatively close to our slot, match regiment speed smoothly
                if (distSqr < 4f)
                {
                    moveSpeed = Mathf.Lerp(formationBaseSpeed, unitMaxSpeed, distSqr / 4f);
                }
                else
                {
                    // Way behind, run up to 1.5x max speed to catch up
                    moveSpeed = unitMaxSpeed * 1.5f; 
                }
            }

            float dist = Mathf.Sqrt(distSqr);
            float step = Mathf.Min(moveSpeed * dt, dist);

            Vector3 pos = transform.position;
            pos.x += dirX * step;
            pos.z += dirZ * step;
            transform.position = pos;

            Vector3 dir = new Vector3(dirX, 0f, dirZ);
            
            if (Regiment != null && Regiment.IsRegimentMoving)
            {
                if (distSqr > 4f)
                {
                    // Running to catch up, face movement
                    transform.forward = Vector3.Lerp(transform.forward, dir, dt * 10f);
                }
                else
                {
                    // Caught up and marching, smoothly align to regiment facing
                    Vector3 regFwd = Regiment.FacingDirection;
                    if (regFwd.sqrMagnitude > 0.01f)
                    {
                        // Blend between walking direction and looking forward
                        Vector3 blended = Vector3.Lerp(dir, regFwd, 1f - (distSqr / 4f));
                        transform.forward = Vector3.Lerp(transform.forward, blended.normalized, dt * 5f);
                    }
                }
            }
            else
            {
                transform.forward = Vector3.Lerp(transform.forward, dir, dt * 8f);
            }

            // Snap to terrain height
            terrainSnapTimer -= dt;
            if (terrainSnapTimer <= 0f)
            {
                terrainSnapTimer = 0.15f;
                SnapToTerrainSmooth();
            }
        }

        private void UpdateCharging(float dt)
        {
            // No combat during deployment phase - return to idle
            if (BattleManager.Instance != null && BattleManager.Instance.IsDeploymentPhase)
            {
                currentState = UnitState.Idle;
                targetEnemy = null;
                return;
            }

            if (targetEnemy == null || targetEnemy.CurrentState == UnitState.Dead)
            {
                targetEnemy = null;
                currentState = UnitState.Idle;
                return;
            }

            float ex = targetEnemy.transform.position.x - transform.position.x;
            float ez = targetEnemy.transform.position.z - transform.position.z;
            float distSqr = ex * ex + ez * ez;

            // === SKIRMISHER AUTO-EVADE ===
            // Skirmishers automatically evade when charged by non-skirmish units
            if (distSqr < 225f && distSqr > 4f) // Within 15m but not yet in melee
            {
                if (targetEnemy.Regiment != null && targetEnemy.Regiment.IsSkirmishing
                    && targetEnemy.Data != null && targetEnemy.Data.canSkirmish)
                {
                    // Target is a skirmisher — they evade to the side
                    float invDist = 1f / Mathf.Sqrt(distSqr);
                    Vector3 chargeDir = new Vector3(ex * invDist, 0f, ez * invDist);
                    // Perpendicular direction (evade sideways)
                    Vector3 evadeDir = new Vector3(-chargeDir.z, 0f, chargeDir.x);
                    // Randomize which side
                    if (Random.value > 0.5f) evadeDir = -evadeDir;

                    float evadeSpeed = targetEnemy.Data.runSpeed * dt;
                    Vector3 evadePos = targetEnemy.transform.position + evadeDir * evadeSpeed;
                    targetEnemy.transform.position = evadePos;

                    // Skirmisher loses target and goes back to idle to re-engage
                    targetEnemy.ForceState(UnitState.Idle);
                }
            }

            if (distSqr < 4f) // 2^2
            {
                // === IMPACT! Enhanced charge mechanics ===
                float chargeBonus = unitData != null ? unitData.chargeBonusDamage : 20f;
                float meleeDmg = unitData != null ? unitData.meleeDamage : 15f;

                // Charge damage scales with distance charged (momentum)
                float momentumScale = unitData != null ? unitData.chargeMomentumScaling : 30f;
                float currentDist = Mathf.Sqrt(distSqr);
                float distanceCharged = chargeStartDistance - currentDist;
                float momentumMultiplier = Mathf.Clamp01(distanceCharged / momentumScale);
                // Full charge bonus at max distance, reduced if charge was short
                chargeBonus *= (0.3f + 0.7f * momentumMultiplier);

                AttackDirection atkDir = GetAttackDirection(targetEnemy);
                float dirMultiplier = GetDirectionMultiplier(atkDir);

                float totalDamage = (meleeDmg + chargeBonus) * dirMultiplier;

                // Square formation halves charge damage and negates momentum bonus
                if (targetEnemy.Regiment != null && targetEnemy.Regiment.CurrentFormation == FormationType.Square)
                {
                    totalDamage *= 0.5f;
                }

                targetEnemy.TakeDamage(totalDamage, this);

                // === AREA OF EFFECT MORALE SHOCK ===
                // All enemy units within impact radius suffer morale damage
                float impactRadius = unitData != null ? unitData.chargeImpactRadius : 5f;
                float moraleLoss = unitData != null ? unitData.moraleLossOnCharged : 15f;
                // Scale morale shock with momentum
                moraleLoss *= (0.5f + 0.5f * momentumMultiplier);

                if (targetEnemy.Regiment != null)
                {
                    // Apply morale shock to the target regiment
                    targetEnemy.Regiment.ApplyMoraleShock(moraleLoss);

                    // Apply suppression to nearby enemies (the shock of a cavalry charge)
                    foreach (var unit in targetEnemy.Regiment.Units)
                    {
                        if (unit == null || unit.CurrentState == UnitState.Dead) continue;
                        float dx = unit.transform.position.x - transform.position.x;
                        float dz = unit.transform.position.z - transform.position.z;
                        if (dx * dx + dz * dz < impactRadius * impactRadius)
                        {
                            unit.ApplySuppression(20f * momentumMultiplier);
                        }
                    }
                }

                MuzzleFlashEffect.Create(transform.position, transform.forward);

                // Register charge impact for bayonet shock tracking
                if (Regiment != null)
                    Regiment.RegisterChargeImpact();

                currentState = UnitState.Attacking;
                return;
            }

            // Charge towards enemy at charge speed
            float invDist = 1f / Mathf.Sqrt(distSqr);
            float dirX = ex * invDist;
            float dirZ = ez * invDist;

            float chargeSpeed = (unitData != null ? unitData.runSpeed : 6f) *
                                (unitData != null ? unitData.chargeSpeedMultiplier : 1.8f);
            chargeSpeed *= GetFatigueMultiplier(unitData != null ? unitData.fatigueSpeedPenalty : 0.4f);
            float step = chargeSpeed * dt;

            Vector3 pos = transform.position;
            pos.x += dirX * step;
            pos.z += dirZ * step;
            transform.position = pos;

            Vector3 dir = new Vector3(dirX, 0f, dirZ);
            transform.forward = Vector3.Lerp(transform.forward, dir, dt * 15f);

            // Snap to terrain height — needs to be frequent for smooth charge movement
            terrainSnapTimer -= dt;
            if (terrainSnapTimer <= 0f)
            {
                terrainSnapTimer = 0.15f;
                SnapToTerrainSmooth();
            }
        }

        private void UpdateAttacking()
        {
            // No combat during deployment phase - return to idle
            if (BattleManager.Instance != null && BattleManager.Instance.IsDeploymentPhase)
            {
                currentState = UnitState.Idle;
                targetEnemy = null;
                return;
            }

            if (targetEnemy == null || targetEnemy.CurrentState == UnitState.Dead)
            {
                targetEnemy = null;
                currentState = UnitState.Idle;
                enemySearchTimer = 0f; // Search immediately
                return;
            }

            // Use sqrMagnitude only — avoid expensive sqrt via .magnitude
            float dx = targetEnemy.transform.position.x - transform.position.x;
            float dz = targetEnemy.transform.position.z - transform.position.z;
            float distSqr = dx * dx + dz * dz;

            if (distSqr > 0.01f)
            {
                Vector3 dirToEnemy = new Vector3(dx, 0f, dz);
                transform.forward = Vector3.Lerp(transform.forward, dirToEnemy.normalized, Time.deltaTime * 10f);
            }

            float rangeSqr = GetEffectiveAttackRange();
            rangeSqr *= rangeSqr;

            if (distSqr > rangeSqr)
            {
                // === RANGED UNITS: Stay in formation, don't chase ===
                if (cachedIsRanged)
                {
                    targetEnemy = null;
                    enemySearchTimer = 0f;
                    currentState = UnitState.Idle;
                    return;
                }
                
                // === PLAYER MELEE UNITS: Hold formation, don't chase ===
                if (TeamId == 0)
                {
                    targetEnemy = null;
                    currentState = UnitState.Idle;
                    return;
                }
                
                // AI melee units can chase their target
                targetPosition = targetEnemy.transform.position;
                currentState = UnitState.Moving;
                return;
            }

            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                PerformAttack(targetEnemy);
                float cooldown = GetEffectiveAttackCooldown();
                attackTimer = isInVolleyMode ? cooldown * 0.8f : cooldown;
            }
        }

        private Vector3 cachedFleeDir;

        private void UpdateRetreating(float dt)
        {
            // Orderly retreat: move away from enemy at normal speed (not run speed)
            // Can still be rallied by officers
            enemySearchTimer -= dt;
            if (enemySearchTimer <= 0f)
            {
                enemySearchTimer = 0.5f;
                UnitBase nearestEnemy = FindNearestEnemy();
                if (nearestEnemy != null)
                {
                    float fx = transform.position.x - nearestEnemy.transform.position.x;
                    float fz = transform.position.z - nearestEnemy.transform.position.z;
                    float fDist = Mathf.Sqrt(fx * fx + fz * fz);
                    if (fDist > 0.01f)
                        cachedFleeDir = new Vector3(fx / fDist, 0f, fz / fDist);
                }
            }

            if (cachedFleeDir.sqrMagnitude > 0.01f)
            {
                // Retreating units move at normal speed, not run speed
                float speed = unitData != null ? unitData.moveSpeed : 3.5f;
                speed *= GetFatigueMultiplier(unitData != null ? unitData.fatigueSpeedPenalty : 0.4f);

                Vector3 pos = transform.position;
                pos.x += cachedFleeDir.x * speed * dt;
                pos.z += cachedFleeDir.z * speed * dt;
                transform.position = pos;
                transform.forward = Vector3.Lerp(transform.forward, cachedFleeDir, dt * 5f);

                terrainSnapTimer -= dt;
                if (terrainSnapTimer <= 0f)
                {
                    terrainSnapTimer = 0.5f;
                    SnapToTerrainSmooth();
                }
            }
        }

        private void UpdateFleeing(float dt)
        {
            // Throttle enemy search while fleeing — every 0.5s
            enemySearchTimer -= dt;
            if (enemySearchTimer <= 0f)
            {
                enemySearchTimer = 0.5f;
                UnitBase nearestEnemy = FindNearestEnemy();
                if (nearestEnemy != null)
                {
                    float fx = transform.position.x - nearestEnemy.transform.position.x;
                    float fz = transform.position.z - nearestEnemy.transform.position.z;
                    float fDist = Mathf.Sqrt(fx * fx + fz * fz);
                    if (fDist > 0.01f)
                        cachedFleeDir = new Vector3(fx / fDist, 0f, fz / fDist);
                }
            }

            if (cachedFleeDir.sqrMagnitude > 0.01f)
            {
                float speed = unitData != null ? unitData.runSpeed : 6f;
                speed *= GetFatigueMultiplier(unitData != null ? unitData.fatigueSpeedPenalty : 0.4f);

                Vector3 pos = transform.position;
                pos.x += cachedFleeDir.x * speed * dt;
                pos.z += cachedFleeDir.z * speed * dt;
                transform.position = pos;
                transform.forward = Vector3.Lerp(transform.forward, cachedFleeDir, dt * 10f);

                terrainSnapTimer -= dt;
                if (terrainSnapTimer <= 0f)
                {
                    terrainSnapTimer = 0.5f; 
                    SnapToTerrainSmooth();
                }
            }

            if (currentMorale > (unitData != null ? unitData.fleeThreshold + 20f : 40f))
            {
                currentState = UnitState.Idle;
            }
        }

        private void UpdateMorale()
        {
            if (currentState == UnitState.Dead) return;

            float recoveryRate = GetEffectiveMoraleRecovery();
            float maxMorale = GetEffectiveMaxMorale();
            float fleeThreshold = GetEffectiveFleeThreshold();

            // Fatigue reduces morale recovery
            float fatigueMult = GetFatigueMultiplier(unitData != null ? unitData.fatigueMoralePenalty : 0.5f);
            recoveryRate *= fatigueMult;

            // Weather affects morale recovery (storms demoralize troops, etc.)
            if (WeatherSystem.Instance != null)
                recoveryRate *= WeatherSystem.Instance.MoraleModifier;

            // Experience boosts max morale
            if (unitData != null)
                maxMorale += unitData.experienceMoraleBonus * (experience / 100f);

            // Officer bonus
            if (Regiment != null && Regiment.OfficerAlive && unitData != null)
            {
                recoveryRate += unitData.officerRecoveryBonus;
                fleeThreshold = Mathf.Max(fleeThreshold - unitData.officerMoraleBonus * 0.5f, 5f);
            }

            // Terrain height morale bonus: troops on high ground feel more confident
            if (transform.position.y > 5f)
            {
                float heightMoraleBonus = Mathf.Clamp(transform.position.y * 0.1f, 0f, 3f);
                recoveryRate += heightMoraleBonus;
            }

            // Recover morale when not in combat
            if (currentState != UnitState.Attacking && currentState != UnitState.Charging)
            {
                currentMorale = Mathf.Min(currentMorale + recoveryRate * Time.deltaTime, maxMorale);
            }

            // Nearby friendly regiment boosts morale recovery
            if (Regiment != null)
            {
                int alive = Regiment.CachedAliveCount;
                int total = Regiment.Units.Count;
                if (alive > 0 && total > 0)
                {
                    float regimentBonus = (float)alive / total * 0.5f;
                    currentMorale = Mathf.Min(currentMorale + regimentBonus * 0.25f, maxMorale);
                }
            }

            // === RETREAT vs ROUT ===
            // Morale below flee threshold: orderly retreat (can be rallied)
            // Morale below half flee threshold: full rout (cannot be rallied, unit is broken)
            float routThreshold = fleeThreshold * 0.5f;

            if (currentMorale <= routThreshold && currentState != UnitState.Fleeing && currentState != UnitState.Dead)
            {
                // Full rout — broken, cannot be rallied
                currentState = UnitState.Fleeing;
            }
            else if (currentMorale <= fleeThreshold && currentState != UnitState.Retreating
                     && currentState != UnitState.Fleeing && currentState != UnitState.Dead)
            {
                // Orderly retreat — can still be rallied by officers
                currentState = UnitState.Retreating;
            }

            // A retreating unit that keeps losing morale transitions to full rout
            if (currentState == UnitState.Retreating && currentMorale <= routThreshold)
            {
                currentState = UnitState.Fleeing;
            }
        }

        private void UpdateStamina()
        {
            if (currentState == UnitState.Dead) return;
            if (unitData == null) return;

            float drain = 0f;
            switch (currentState)
            {
                case UnitState.Idle:
                    // Recover stamina when idle
                    currentStamina = Mathf.Min(currentStamina + unitData.staminaRecoveryIdle * Time.deltaTime, unitData.maxStamina);
                    return;
                case UnitState.Moving:
                    drain = unitData.staminaDrainWalk;
                    break;
                case UnitState.Charging:
                    drain = unitData.staminaDrainCharge;
                    break;
                case UnitState.Attacking:
                    drain = unitData.staminaDrainCombat;
                    break;
                case UnitState.Retreating:
                    drain = unitData.staminaDrainWalk;
                    break;
                case UnitState.Fleeing:
                    drain = unitData.staminaDrainRun;
                    break;
            }

            currentStamina = Mathf.Max(currentStamina - drain * Time.deltaTime, 0f);
        }

        /// <summary>
        /// Returns a multiplier (fatigueMinValue to 1.0) based on current stamina.
        /// At full stamina returns 1.0, at 0 stamina returns fatigueMinValue.
        /// </summary>
        private float GetFatigueMultiplier(float fatigueMinValue)
        {
            if (unitData == null) return 1f;
            float t = currentStamina / unitData.maxStamina;
            return Mathf.Lerp(fatigueMinValue, 1f, t);
        }

        // === SUPPRESSION SYSTEM ===
        private void UpdateSuppression(float dt)
        {
            if (currentSuppression <= 0f) return;

            // Suppression decays over time
            float recovery = unitData != null ? unitData.suppressionRecoveryRate : 8f;
            currentSuppression = Mathf.Max(currentSuppression - recovery * dt, 0f);

            // High suppression amplifies morale loss
            if (currentSuppression > 50f)
            {
                float moralePressure = (currentSuppression - 50f) * 0.02f * dt;
                currentMorale = Mathf.Max(currentMorale - moralePressure, 0f);
            }
        }

        /// <summary>
        /// Apply suppression to this unit (called when hit or near-missed).
        /// Suppressed units have reduced accuracy, speed, and faster morale decay.
        /// </summary>
        public void ApplySuppression(float amount)
        {
            if (currentState == UnitState.Dead) return;

            // Elite units resist suppression
            float resistance = unitData != null ? unitData.suppressionResistance : 0f;
            float effectiveAmount = amount * (1f - resistance);

            currentSuppression = Mathf.Min(currentSuppression + effectiveAmount, 100f);
        }

        /// <summary>
        /// Returns a multiplier (0.5 to 1.0) based on current suppression level.
        /// Fully suppressed units operate at 50% effectiveness.
        /// </summary>
        private float GetSuppressionMultiplier()
        {
            return 1f - (currentSuppression * 0.005f); // 0 suppression = 1.0, 100 suppression = 0.5
        }

        public void GainExperience(float amount)
        {
            experience = Mathf.Min(experience + amount, 100f);
        }

        private void UpdateVolleyTimer()
        {
            if (volleyTimer > 0f)
                volleyTimer -= Time.deltaTime;
        }

        public void MoveTo(Vector3 position)
        {
            if (currentState == UnitState.Dead) return;
            targetPosition = position;
            targetEnemy = null;
            currentState = UnitState.Moving;
        }

        /// <summary>
        /// Update target position during regiment sliding formation movement.
        /// Unlike MoveTo, this only updates the target without resetting state
        /// (avoids Idle/Moving flickering when regiment pushes new targets each frame).
        /// </summary>
        public void UpdateFormationTarget(Vector3 position)
        {
            if (currentState == UnitState.Dead) return;
            targetPosition = position;
            if (currentState != UnitState.Moving)
            {
                targetEnemy = null;
                currentState = UnitState.Moving;
            }
        }



        private void PerformAttack(UnitBase target)
        {
            if (target == null) return;

            // No combat during deployment phase
            if (BattleManager.Instance != null && BattleManager.Instance.IsDeploymentPhase)
                return;

            // === ARTILLERY LIMBER CHECK ===
            // Limbered artillery cannot fire — must unlimber first
            if (Regiment != null && Regiment.IsArtillery && !Regiment.CanArtilleryFire())
                return;

            // Check ammunition for ranged attacks
            bool isRanged = cachedIsRanged;

            // === FRONT RANK RESTRICTION ===
            // Only front-rank units can fire ranged weapons, unless "Firing by Rank" tech is researched
            if (isRanged && Regiment != null)
            {
                bool hasFiringByRank = false;
                // Check if the faction has researched "firing_by_rank" technology
                if (UnitTechnologySystem.Instance != null)
                {
                    var factionData = NapoleonicWars.Campaign.CampaignManager.Instance?.GetPlayerFaction();
                    if (factionData != null && factionData.techTree != null)
                        hasFiringByRank = factionData.techTree.IsResearched("firing_by_rank");
                }

                if (!hasFiringByRank && !Regiment.IsUnitInFrontRank(this))
                {
                    // Not in front rank and no tech — can't fire, wait for front rank to fall
                    return;
                }
            }

            // === FRIENDLY FIRE CHECK / LINE OF SIGHT ===
            // If a friendly unit is in the line of fire, hold fire to avoid shooting through allies
            if (isRanged && SpatialGrid.Instance != null)
            {
                // Check for friendly units between this unit and the target (within a narrow corridor)
                float checkRadius = 3f; // Width of the firing corridor
                Vector3 midPoint = (transform.position + target.transform.position) * 0.5f;
                float halfDist = Vector3.Distance(transform.position, target.transform.position) * 0.5f;
                var nearbyUnits = SpatialGrid.Instance.GetNearbyUnits(midPoint, halfDist);
                Vector3 fireDir = (target.transform.position - transform.position).normalized;
                float fireDist = Vector3.Distance(transform.position, target.transform.position);

                for (int i = 0; i < nearbyUnits.Count; i++)
                {
                    var u = nearbyUnits[i];
                    if (u == null || u == this || u == target) continue;
                    if (u.TeamId != TeamId) continue; // Only check friendlies
                    if (u.CurrentState == UnitState.Dead) continue;

                    // Project the friendly onto the firing line
                    Vector3 toFriendly = u.transform.position - transform.position;
                    float projDist = Vector3.Dot(toFriendly, fireDir);

                    // Must be between shooter and target (not behind or beyond)
                    if (projDist < 2f || projDist > fireDist - 1f) continue;

                    // Check perpendicular distance to firing line
                    Vector3 closestOnLine = transform.position + fireDir * projDist;
                    float perpDist = Vector3.Distance(u.transform.position, closestOnLine);

                    if (perpDist < checkRadius)
                    {
                        // Friendly in the way — hold fire
                        return;
                    }
                }
            }

            // === MELEE SWITCH: If enemy is very close, use bayonets/melee instead of shooting ===
            // Compute distance once — reused for melee check AND accuracy falloff below
            float tdx = target.transform.position.x - transform.position.x;
            float tdz = target.transform.position.z - transform.position.z;
            float tdy = target.transform.position.y - transform.position.y;
            float distToTarget = Mathf.Sqrt(tdx * tdx + tdy * tdy + tdz * tdz);
            float meleeRange = unitData != null ? unitData.meleeRange : 1.5f;
            
            // If enemy is within melee range + small buffer, switch to melee
            if (isRanged && distToTarget <= meleeRange + 0.5f)
            {
                // Enemy is too close for effective shooting - switch to bayonets
                isRanged = false;
            }
            
            if (isRanged && !HasAmmo)
            {
                // Out of ammo — switch to melee if in range, otherwise can't attack
                if (distToTarget > meleeRange)
                    return;
                isRanged = false;
            }

            float damage = isRanged
                ? GetEffectiveAttackDamage()
                : (unitData != null ? unitData.meleeDamage : 15f);

            // === BAYONET SHOCK BONUS ===
            // If regiment achieved coordinated charge impact, melee damage is boosted
            if (!isRanged && Regiment != null)
                damage *= Regiment.BayonetShockMultiplier;

            float accuracy = GetEffectiveAccuracy();

            // Consume ammo for ranged attacks
            if (isRanged && unitData != null && !unitData.hasUnlimitedAmmo)
                currentAmmo = Mathf.Max(currentAmmo - 1, 0);

            // === COMBAT FATIGUE ===
            // Each attack drains stamina — sustained fire progressively degrades accuracy
            float combatStaminaCost = isRanged ? 0.5f : 0.8f; // Melee is more exhausting
            currentStamina = Mathf.Max(currentStamina - combatStaminaCost, 0f);

            // === TERRAIN HEIGHT ADVANTAGE ===
            // Shooting downhill grants accuracy bonus; shooting uphill penalizes
            float heightDiff = transform.position.y - target.transform.position.y;
            if (heightDiff > 1f)
            {
                // Shooting downhill: up to +15% accuracy bonus
                accuracy += Mathf.Clamp(heightDiff * 0.02f, 0f, 0.15f);
            }
            else if (heightDiff < -1f)
            {
                // Shooting uphill: up to -12% accuracy penalty
                accuracy += Mathf.Clamp(heightDiff * 0.015f, -0.12f, 0f);
            }

            // Fatigue reduces accuracy
            accuracy *= GetFatigueMultiplier(unitData != null ? unitData.fatigueAccuracyPenalty : 0.3f);

            // === SUPPRESSION reduces accuracy ===
            // Suppressed units fire less accurately (ducking, flinching, panicking)
            accuracy *= GetSuppressionMultiplier();

            // Experience improves accuracy
            if (unitData != null)
                accuracy += unitData.experienceAccuracyBonus * (experience / 100f);

            // Volley fire accuracy bonus
            if (isInVolleyMode && unitData != null && unitData.canVolleyFire && volleyTimer <= 0f)
            {
                accuracy += unitData.volleyAccuracyBonus;
                volleyTimer = unitData.volleyCooldown;
                GainExperience(unitData.experiencePerVolley);
            }

            // Determine attack direction for damage multiplier
            AttackDirection atkDir = GetAttackDirection(target);
            float dirMultiplier = GetDirectionMultiplier(atkDir);

            // Anti-unit type bonus
            float typeBonus = GetAntiUnitBonus(target);

            // Apply difficulty multipliers
            if (DifficultySettings.Instance != null)
            {
                int team = Regiment != null ? Regiment.TeamId : 0;
                damage *= DifficultySettings.Instance.GetDamageMultiplier(team);
                accuracy += DifficultySettings.Instance.GetAccuracyBonus(team);
            }

            // === TRAJECTORY-STYLE DISTANCE ACCURACY ===
            // Very long range possible but accuracy drops sharply with distance
            // distToTarget already computed above — reuse it
            
            // Distance-based accuracy curve:
            // 0-30m:   100% of base accuracy (point blank, very deadly)
            // 30-60m:  85% of base accuracy (close range)
            // 60-100m: 60% of base accuracy (medium range)
            // 100-150m: 35% of base accuracy (long range)
            // 150m+:   15% of base accuracy (extreme range, mostly misses)
            float distanceMultiplier;
            if (distToTarget <= 30f)
                distanceMultiplier = 1.00f;
            else if (distToTarget <= 60f)
                distanceMultiplier = Mathf.Lerp(1.00f, 0.85f, (distToTarget - 30f) / 30f);
            else if (distToTarget <= 100f)
                distanceMultiplier = Mathf.Lerp(0.85f, 0.60f, (distToTarget - 60f) / 40f);
            else if (distToTarget <= 150f)
                distanceMultiplier = Mathf.Lerp(0.60f, 0.35f, (distToTarget - 100f) / 50f);
            else
                distanceMultiplier = Mathf.Lerp(0.35f, 0.15f, Mathf.Clamp01((distToTarget - 150f) / 100f));
            
            // Apply distance penalty
            accuracy *= distanceMultiplier;
            
            // Small random variation (less impact than before)
            accuracy += Random.Range(-0.05f, 0.05f);
            accuracy = Mathf.Clamp01(accuracy);

            float totalDamage = damage * dirMultiplier * typeBonus;

            // Trigger fire animation
            if (unitAnimator != null && isRanged)
            {
                unitAnimator.TriggerFire();
                unitAnimator.TriggerReload();
            }

            // Spawn visible projectile tracer - use cannonball for artillery
            bool isArtilleryUnit = unitData != null && Regiment != null && Regiment.IsArtillery;
            bool isCanisterMode = isArtilleryUnit && distToTarget <= (unitData != null ? unitData.canisterRange : 60f);

            if (isRanged && ProjectileSystem.Instance != null)
            {
                if (isArtilleryUnit && !isCanisterMode)
                {
                    // Artillery: fire physical cannonball from barrel position (roundshot)
                    Vector3 barrelPos = transform.position + transform.forward * 0.6f + Vector3.up * 0.4f;
                    float splashRadius = unitData.splashRadius > 0 ? unitData.splashRadius : 5f;
                    ProjectileSystem.Instance.FireCannonball(barrelPos, target.transform.position, totalDamage, splashRadius, TeamId);

                    // Cannon fire VFX: massive blast + smoke ring
                    if (AdvancedParticleManager.Instance != null)
                        AdvancedParticleManager.Instance.SpawnCannonFire(barrelPos, transform.forward);
                }
                else if (isCanisterMode)
                {
                    // === CANISTER SHOT (MITRAILLE) ===
                    // Close-range shotgun blast — multiple pellets in a cone, devastating vs infantry
                    Vector3 barrelPos = transform.position + transform.forward * 0.6f + Vector3.up * 0.4f;
                    int pellets = unitData.canisterPellets;
                    float pelletDamage = unitData.canisterDamage;
                    float coneAngle = unitData.canisterConeAngle;
                    float canisterSupp = unitData.canisterSuppression;

                    // Fire each pellet with slight random spread
                    for (int i = 0; i < pellets; i++)
                    {
                        float angleOffset = Random.Range(-coneAngle, coneAngle);
                        Vector3 spreadDir = Quaternion.Euler(0f, angleOffset, 0f) * transform.forward;
                        Vector3 pelletTarget = target.transform.position + spreadDir * Random.Range(-2f, 2f);

                        // Each pellet is a separate tracer
                        ProjectileSystem.Instance.FireProjectile(barrelPos, pelletTarget, pelletDamage, TeamId);

                        // Each pellet has independent hit chance (higher accuracy at close range)
                        float pelletAccuracy = accuracy * 1.2f; // Canister is more accurate close up
                        pelletAccuracy = Mathf.Clamp01(pelletAccuracy);

                        if (Random.value <= pelletAccuracy && target.CurrentState != UnitState.Dead)
                        {
                            target.TakeDamage(pelletDamage * dirMultiplier, this);
                            target.ApplySuppression(canisterSupp / pellets);
                        }
                    }

                    // Massive suppression to all nearby enemies from canister blast
                    if (target.Regiment != null)
                    {
                        foreach (var unit in target.Regiment.Units)
                        {
                            if (unit == null || unit.CurrentState == UnitState.Dead) continue;
                            float dx = unit.transform.position.x - target.transform.position.x;
                            float dz = unit.transform.position.z - target.transform.position.z;
                            if (dx * dx + dz * dz < 100f) // 10m radius
                                unit.ApplySuppression(canisterSupp * 0.5f);
                        }
                    }

                    // Cannon fire VFX
                    if (AdvancedParticleManager.Instance != null)
                        AdvancedParticleManager.Instance.SpawnCannonFire(barrelPos, transform.forward);

                    // Record shot
                    if (BattleStatistics.Instance != null)
                        BattleStatistics.Instance.RecordShotFired(Regiment != null ? Regiment.TeamId : 0);

                    return; // Canister handles its own damage — skip normal hit check below
                }
                else
                {
                    // Infantry: use simple tracer
                    ProjectileSystem.Instance.FireProjectile(transform.position, target.transform.position, damage, TeamId);

                    // Musket smoke VFX: muzzle flash + smoke plume + sparks
                    if (AdvancedParticleManager.Instance != null)
                        AdvancedParticleManager.Instance.SpawnMusketSmoke(
                            transform.position + transform.forward * 0.4f + Vector3.up * 1.2f,
                            transform.forward);
                }
            }

            // Check if shot hits (roundshot / musket — canister already returned above)
            if (Random.value <= accuracy)
            {
                // === APPLY DAMAGE + SUPPRESSION ON HIT ===
                target.TakeDamage(totalDamage, this);
                // Direct hit causes heavy suppression
                float suppressionOnHit = unitData != null ? unitData.suppressionPerHit : 12f;
                target.ApplySuppression(suppressionOnHit);
                
                MuzzleFlashEffect.Create(transform.position, transform.forward);

                // Record shot hit
                if (BattleStatistics.Instance != null)
                    BattleStatistics.Instance.RecordShotHit(Regiment != null ? Regiment.TeamId : 0);

                // Combat sounds
                if (BattleSoundManager.Instance != null)
                {
                    if (isRanged)
                        BattleSoundManager.Instance.PlayMusketFire(transform.position);
                    else
                    {
                        BattleSoundManager.Instance.PlayBayonetStab(transform.position);
                        // Bayonet spark VFX on melee hit
                        if (AdvancedParticleManager.Instance != null)
                            AdvancedParticleManager.Instance.SpawnBayonetSpark(target.transform.position + Vector3.up * 0.8f);
                    }
                }

                // Record kill
                if (target.CurrentHealth <= 0f && BattleStatistics.Instance != null)
                    BattleStatistics.Instance.RecordKill(Regiment != null ? Regiment.TeamId : 0);

                // Gain experience on hit
                if (unitData != null)
                    GainExperience(target.CurrentHealth <= 0f ? unitData.experiencePerKill : 1f);

                // Flanking morale penalty
                if (atkDir == AttackDirection.Flank || atkDir == AttackDirection.Rear)
                {
                    float flankMoraleLoss = unitData != null ? unitData.moraleLossOnFlanked : 10f;
                    if (atkDir == AttackDirection.Rear) flankMoraleLoss *= 1.5f;
                    target.ApplyMoraleDamage(flankMoraleLoss);
                }
            }
            else if (isRanged)
            {
                // === NEAR MISS SUPPRESSION ===
                // Even missed shots cause suppression — bullets whizzing by are terrifying
                float nearMissSuppression = unitData != null ? unitData.suppressionPerNearMiss : 5f;
                target.ApplySuppression(nearMissSuppression);
            }
        }



        private float GetAntiUnitBonus(UnitBase target)
        {
            if (unitData == null || target.Data == null) return 1f;

            UnitType targetType = target.Data.unitType;

            switch (targetType)
            {
                case UnitType.Cavalry:
                case UnitType.Hussar:
                case UnitType.Lancer:
                    return unitData.bonusVsCavalry;
                case UnitType.Artillery:
                    return unitData.bonusVsArtillery;
                default:
                    return unitData.bonusVsInfantry;
            }
        }

        public void SetCoverStatus(bool inCover)
        {
            isInCover = inCover;
        }

        public void TakeDamage(float damage, UnitBase attacker)
        {
            if (currentState == UnitState.Dead) return;

            // === COVER BONUS ===
            // Calculate damage reduction from cover
            if (isInCover && attacker != null && CoverSystem.Instance != null)
            {
                Vector3 attackerDir = (attacker.transform.position - transform.position).normalized;
                float coverBonus = CoverSystem.Instance.GetCoverBonus(transform.position, attackerDir);

                // Apply damage reduction (coverBonus is 0-0.6, meaning 0-60% reduction)
                damage *= (1f - coverBonus);
            }

            // === TERRAIN HEIGHT DEFENSE ===
            // Defender on higher ground takes reduced damage (up to 15% reduction)
            if (attacker != null)
            {
                float defenderHeightAdv = transform.position.y - attacker.transform.position.y;
                if (defenderHeightAdv > 1f)
                {
                    float heightReduction = Mathf.Clamp(defenderHeightAdv * 0.015f, 0f, 0.15f);
                    damage *= (1f - heightReduction);
                }
            }

            // === SKIRMISHER DAMAGE REDUCTION ===
            // Spread-out skirmishers are harder to hit with ranged fire
            if (Regiment != null && Regiment.IsSkirmishing && attacker != null && unitData != null)
            {
                // Only reduces ranged damage (attacker is far away)
                float distToAttacker = Vector3.Distance(transform.position, attacker.transform.position);
                if (distToAttacker > 5f) // Not melee
                {
                    damage *= (1f - unitData.skirmishDamageReduction);
                }
            }

            currentHealth -= damage;
            float moraleLoss = unitData != null ? unitData.moraleLossOnHit : 5f;
            currentMorale -= moraleLoss;

            if (Regiment != null)
            {
                Regiment.OnUnitDamaged(this);
            }

            if (currentHealth <= 0f)
            {
                // Give experience to the attacker
                if (attacker != null)
                    attacker.GainExperience(attacker.Data != null ? attacker.Data.experiencePerKill : 10f);

                // Calculate hit direction for ragdoll
                Vector3 hitDirection = Vector3.zero;
                float hitForce = Random.Range(300f, 600f);
                
                if (attacker != null)
                {
                    // Direction from attacker to this unit
                    hitDirection = (transform.position - attacker.transform.position).normalized;
                    hitDirection.y = Random.Range(0.2f, 0.5f); // Add upward component
                    
                    // Ranged attacks have less force than melee
                    bool isRanged = attacker.Data != null && attacker.Data.attackRange > attacker.Data.meleeRange + 1f;
                    hitForce = isRanged ? Random.Range(200f, 400f) : Random.Range(400f, 700f);
                }
                
                Die(hitDirection, hitForce);

                // Check if officer was hit
                if (Regiment != null)
                    Regiment.CheckOfficerCasualty();
            }
            else if (targetEnemy == null && currentState == UnitState.Idle)
            {
                if (attacker != null)
                    AttackTarget(attacker);
            }
        }

        public void ApplyMoraleDamage(float amount)
        {
            if (currentState == UnitState.Dead) return;
            currentMorale -= amount;
        }

        public void RestoreMorale(float amount)
        {
            if (currentState == UnitState.Dead) return;
            float max = GetEffectiveMaxMorale();
            currentMorale = Mathf.Min(currentMorale + amount, max);
        }

        public void ForceState(UnitState state)
        {
            if (currentState == UnitState.Dead) return;
            currentState = state;
            if (state == UnitState.Idle)
                targetEnemy = null;
        }

        private void Die(Vector3? hitDirection = null, float hitForce = 0f)
        {
            if (currentState == UnitState.Dead) return;
            currentState = UnitState.Dead;
            currentHealth = 0f;

            if (BattleSoundManager.Instance != null)
                BattleSoundManager.Instance.PlayDeathCry(transform.position);

            if (Regiment != null)
            {
                Regiment.OnUnitDied(this);
            }

            // Activate ragdoll physics
            ActivateRagdoll(hitDirection, hitForce);

            // Change color to indicate death
            if (unitRenderer != null)
            {
                Color deathColor = Color.gray * 0.3f;
                unitRenderer.GetPropertyBlock(mpb);
                mpb.SetColor(ColorId, deathColor);
                mpb.SetColor(BaseColorId, deathColor);
                unitRenderer.SetPropertyBlock(mpb);
            }

            // Disable physics + renderer after 1.5s to reduce physics/render load from dead bodies
            StartCoroutine(DisableRagdollAfterDelay(1.5f));

            // Destroy after delay to clean up (reduced for 6000+ unit perf)
            Destroy(gameObject, 2f);
        }
        
        private void ActivateRagdoll(Vector3? hitDirection = null, float hitForce = 0f)
        {
            if (isRagdollActive) return;
            isRagdollActive = true;
            
            // Disable animator to stop animations
            if (unitAnimator != null)
            {
                Animator anim = unitAnimator.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.enabled = false;
                }
            }
            
            // Enable physics
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.detectCollisions = true;
                
                // Apply death force/torque for realistic fall
                Vector3 force = hitDirection ?? new Vector3(
                    Random.Range(-0.5f, 0.5f), 
                    Random.Range(0.3f, 0.8f), 
                    Random.Range(-0.5f, 0.5f)
                );
                
                // Normalize and apply force
                force = force.normalized;
                float deathForce = hitForce > 0f ? hitForce : Random.Range(200f, 500f);
                rb.AddForce(force * deathForce, ForceMode.Impulse);
                
                // Add random rotation for variety
                rb.AddTorque(new Vector3(
                    Random.Range(-2f, 2f),
                    Random.Range(-1f, 1f),
                    Random.Range(-2f, 2f)
                ) * Random.Range(50f, 150f), ForceMode.Impulse);
            }
            
            // Ensure collider is enabled for physics
            if (unitCollider != null)
            {
                unitCollider.enabled = true;
                unitCollider.isTrigger = false;
            }
        }

        private System.Collections.IEnumerator DisableRagdollAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }
            if (unitCollider != null)
                unitCollider.enabled = false;
            // Disable renderer to save draw calls from dead bodies
            if (unitRenderer != null)
                unitRenderer.enabled = false;
        }
        
        /// <summary>
        /// Apply impact force from projectile or melee hit
        /// </summary>
        public void ApplyImpact(Vector3 direction, float force)
        {
            if (currentState == UnitState.Dead && rb != null && !rb.isKinematic)
            {
                // If already dead/ragdoll, add more force
                rb.AddForce(direction.normalized * force, ForceMode.Impulse);
            }
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            if (unitRenderer != null && mpb != null)
            {
                // Use MaterialPropertyBlock to avoid creating material instances
                Color targetColor = selected ? selectedHighlight : originalColor;
                unitRenderer.GetPropertyBlock(mpb);
                mpb.SetColor(ColorId, targetColor);
                mpb.SetColor(BaseColorId, targetColor);
                unitRenderer.SetPropertyBlock(mpb);
            }
            
            // Note: Firing range visualizer is now managed at Regiment level (one per regiment)
            // Individual units no longer control their own visualizer
        }

        public void SetUnitData(UnitData data)
        {
            unitData = data;
            currentHealth = data.maxHealth * rankMods.hpMultiplier;
            currentMorale = (data.maxMorale + rankMods.moraleMaxBonus);
            currentStamina = data.maxStamina;
            currentAmmo = data.maxAmmo;
            experience = data.startingExperience;

            if (unitRenderer != null && mpb != null)
            {
                // Check if the material already has a texture (e.g., Meshy imported models)
                // If so, keep white tint so the texture shows through at full brightness
                bool hasTexture = unitRenderer.sharedMaterial != null && 
                                  unitRenderer.sharedMaterial.HasProperty("_BaseMap") &&
                                  unitRenderer.sharedMaterial.GetTexture("_BaseMap") != null;

                if (hasTexture)
                {
                    // Textured model — use white tint so texture is visible
                    originalColor = Color.white;
                }
                else
                {
                    // Primitive/untextured model — use faction color
                    originalColor = data.factionColor;
                }
                
                // Use MaterialPropertyBlock to avoid creating material instances
                unitRenderer.GetPropertyBlock(mpb);
                mpb.SetColor(ColorId, originalColor);
                mpb.SetColor(BaseColorId, originalColor);
                unitRenderer.SetPropertyBlock(mpb);
            }
        }

        /// <summary>
        /// Apply regiment rank bonuses to this unit. Called once after Initialize.
        /// </summary>
        public void ApplyRankBonuses(int rank)
        {
            appliedRank = rank;
            rankMods = RegimentRankSystem.GetStatModifiers((RegimentRank)rank);
            
            // Re-apply HP and morale with rank bonuses
            if (unitData != null)
            {
                currentHealth = unitData.maxHealth * rankMods.hpMultiplier;
                currentMorale = unitData.maxMorale + rankMods.moraleMaxBonus;
            }
        }

        /// <summary>Currently applied rank index (0-9).</summary>
        public int AppliedRank => appliedRank;

        private UnitBase FindNearestEnemy()
        {
            float searchRadius = GetEffectiveAttackRange();

            // Use spatial grid if available (much faster than Physics.OverlapSphere)
            if (SpatialGrid.Instance != null)
            {
                UnitBase nearest = SpatialGrid.Instance.GetNearestEnemy(transform.position, TeamId, searchRadius);
                if (nearest != null && targetEnemy == null)
                    targetEnemy = nearest;
                return nearest;
            }

            // Fallback to physics
            Collider[] colliders = Physics.OverlapSphere(transform.position, searchRadius);

            UnitBase nearestFallback = null;
            float nearestDistSqr = float.MaxValue;

            foreach (var col in colliders)
            {
                UnitBase other = col.GetComponent<UnitBase>();
                if (other != null && other.TeamId != TeamId && other.CurrentState != UnitState.Dead)
                {
                    float distSqr = (transform.position - other.transform.position).sqrMagnitude;
                    if (distSqr < nearestDistSqr)
                    {
                        nearestDistSqr = distSqr;
                        nearestFallback = other;
                    }
                }
            }

            if (nearestFallback != null && targetEnemy == null)
                targetEnemy = nearestFallback;

            return nearestFallback;
        }

        private bool IsInAttackRange(UnitBase target)
        {
            float range = GetAttackRange();
            float dx = transform.position.x - target.transform.position.x;
            float dz = transform.position.z - target.transform.position.z;
            return dx * dx + dz * dz <= range * range;
        }

        private float GetAttackRange()
        {
            return GetEffectiveAttackRange();
        }
    }
}
