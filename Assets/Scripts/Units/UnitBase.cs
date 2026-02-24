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
        Fleeing,
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

        // Volley fire state
        private bool isKneeling = false;
        private float reloadTimer = 0f;
        public bool IsReloaded => reloadTimer <= 0f;

        // Visual
        private Renderer unitRenderer;
        private Color originalColor;
        private static readonly Color selectedHighlight = new Color(0.3f, 1f, 0.3f, 1f);
        
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
            return Mathf.Clamp01(unitData.accuracy + cachedAccuracyBonus + rankMods.accuracyBonus);
        }
        
        public float GetEffectiveMoveSpeed()
        {
            if (unitData == null) return 3.5f;
            return unitData.moveSpeed * cachedSpeedMultiplier * rankMods.speedMultiplier * GetFatigueMultiplier(unitData.fatigueSpeedPenalty);
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
            }
        }

        private void SnapToTerrainSmooth()
        {
            float targetY = NapoleonicWars.Core.BattleManager.GetTerrainHeight(transform.position);
            Vector3 pos = transform.position;
            pos.y = Mathf.Lerp(pos.y, targetY, 0.3f); // Smooth transition
            transform.position = pos;
        }

        private void UpdateIdle()
        {
            // No combat during deployment phase
            if (BattleManager.Instance != null && BattleManager.Instance.IsDeploymentPhase)
                return;

            // Player melee units stay put until ordered — but ranged units auto-fire at targets in range
            if (TeamId == 0)
            {
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
                    currentState = UnitState.Attacking;
                }
                else if (targetEnemy != null)
                {
                    // Target exists but out of range — don't chase, just drop target
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

            if (distSqr < 0.09f) // 0.3^2 — tighter than before for clean ranks
            {
                // SNAP to exact target position for clean formation alignment
                Vector3 snapPos = transform.position;
                snapPos.x = targetPosition.x;
                snapPos.z = targetPosition.z;
                transform.position = snapPos;
                currentState = UnitState.Idle;
                return;
            }

            float invDist = 1f / Mathf.Sqrt(distSqr);
            float dirX = dx * invDist;
            float dirZ = dz * invDist;

            float speed = GetEffectiveMoveSpeed();
            float dist = Mathf.Sqrt(distSqr);
            float step = Mathf.Min(speed * dt, dist); // Clamp step to remaining distance (prevent overshoot)

            Vector3 pos = transform.position;
            pos.x += dirX * step;
            pos.z += dirZ * step;
            transform.position = pos;

            Vector3 dir = new Vector3(dirX, 0f, dirZ);
            transform.forward = Vector3.Lerp(transform.forward, dir, dt * 3f);

            // Snap to terrain height — needs to be frequent for smooth movement
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

            if (distSqr < 4f) // 2^2
            {
                // Impact! Apply charge bonus damage
                float chargeBonus = unitData != null ? unitData.chargeBonusDamage : 20f;
                float meleeDmg = unitData != null ? unitData.meleeDamage : 15f;

                AttackDirection atkDir = GetAttackDirection(targetEnemy);
                float dirMultiplier = GetDirectionMultiplier(atkDir);

                float totalDamage = (meleeDmg + chargeBonus) * dirMultiplier;
                targetEnemy.TakeDamage(totalDamage, this);

                // Charge morale shock to target regiment
                if (targetEnemy.Regiment != null)
                {
                    float moraleLoss = unitData != null ? unitData.moraleLossOnCharged : 15f;
                    targetEnemy.Regiment.ApplyMoraleShock(moraleLoss);
                }

                MuzzleFlashEffect.Create(transform.position, transform.forward);
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
                
                // Melee-only units can chase their target
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

            // Experience boosts max morale
            if (unitData != null)
                maxMorale += unitData.experienceMoraleBonus * (experience / 100f);

            // Officer bonus
            if (Regiment != null && Regiment.OfficerAlive && unitData != null)
            {
                recoveryRate += unitData.officerRecoveryBonus;
                fleeThreshold = Mathf.Max(fleeThreshold - unitData.officerMoraleBonus * 0.5f, 5f);
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

            if (currentMorale <= fleeThreshold && currentState != UnitState.Fleeing && currentState != UnitState.Dead)
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

        private void PerformAttack(UnitBase target)
        {
            if (target == null) return;

            // No combat during deployment phase
            if (BattleManager.Instance != null && BattleManager.Instance.IsDeploymentPhase)
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
            float accuracy = GetEffectiveAccuracy();

            // Consume ammo for ranged attacks
            if (isRanged && unitData != null && !unitData.hasUnlimitedAmmo)
                currentAmmo = Mathf.Max(currentAmmo - 1, 0);

            // Fatigue reduces accuracy
            accuracy *= GetFatigueMultiplier(unitData != null ? unitData.fatigueAccuracyPenalty : 0.3f);

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
            if (isRanged && ProjectileSystem.Instance != null)
            {
                if (unitData != null && unitData.unitType == UnitType.Artillery)
                {
                    // Artillery: fire physical cannonball from barrel position
                    Vector3 barrelPos = transform.position + transform.forward * 0.6f + Vector3.up * 0.4f;
                    float splashRadius = unitData.splashRadius > 0 ? unitData.splashRadius : 5f;
                    ProjectileSystem.Instance.FireCannonball(barrelPos, target.transform.position, totalDamage, splashRadius, TeamId);
                    
                    // Cannon fire VFX: massive blast + smoke ring
                    if (AdvancedParticleManager.Instance != null)
                        AdvancedParticleManager.Instance.SpawnCannonFire(barrelPos, transform.forward);
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

            // Check if shot hits
            if (Random.value <= accuracy)
            {
                // === APPLY DAMAGE ===
                target.TakeDamage(totalDamage, this);
                
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
        }

        public AttackDirection GetAttackDirection(UnitBase target)
        {
            if (target == null) return AttackDirection.Front;

            Vector3 toAttacker = (transform.position - target.transform.position).normalized;
            float dot = Vector3.Dot(target.transform.forward, toAttacker);

            if (dot > 0.5f) return AttackDirection.Front;
            if (dot < -0.3f) return AttackDirection.Rear;
            return AttackDirection.Flank;
        }

        private float GetDirectionMultiplier(AttackDirection dir)
        {
            switch (dir)
            {
                case AttackDirection.Flank:
                    return unitData != null ? unitData.flankingDamageMultiplier : 1.5f;
                case AttackDirection.Rear:
                    return unitData != null ? unitData.rearDamageMultiplier : 2f;
                default:
                    return 1f;
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
                
                // Visual feedback - cover protection (Debug.Log removed — hot path)
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
