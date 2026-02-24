using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Core;

namespace NapoleonicWars.Units
{
    public class ProjectileSystem : MonoBehaviour
    {
        public static ProjectileSystem Instance { get; private set; }

        [Header("Projectile Settings")]
        [SerializeField] private float bulletSpeed = 80f;
        [SerializeField] private float bulletLifetime = 3f;
        [SerializeField] private int poolSize = 100;

        // Object pools to avoid GC allocations
        private Queue<GameObject> bulletPool = new Queue<GameObject>();
        private Queue<GameObject> cannonballPool = new Queue<GameObject>();
        private Material cachedTracerMaterial;
        private Material cachedCannonballMaterial;
        private PhysicsMaterial cachedBounceMaterial;
        
        // Active physics projectile cap — prevents artillery volleys from creating dozens
        // of simultaneous PhysicsProjectiles all running SphereCastAll every frame
        private int activePhysicsProjectiles = 0;
        private const int MAX_ACTIVE_PHYSICS_PROJECTILES = 10;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            // Pre-create cached materials (only once)
            cachedTracerMaterial = URPMaterialHelper.CreateLitEmissive(
                new Color(1f, 1f, 0f),
                new Color(1f, 1f, 0.5f) * 20f
            );
            cachedCannonballMaterial = URPMaterialHelper.CreateLit(new Color(0.2f, 0.2f, 0.2f));
            cachedBounceMaterial = new PhysicsMaterial {
                bounciness = 0.6f,
                dynamicFriction = 0.15f,
                staticFriction = 0.15f,
                bounceCombine = PhysicsMaterialCombine.Maximum
            };
            
            // Pre-warm pools
            PrewarmPools();
        }

        private void PrewarmPools()
        {
            for (int i = 0; i < poolSize; i++)
            {
                GameObject bullet = CreateBulletObject();
                bullet.SetActive(false);
                bulletPool.Enqueue(bullet);
            }
            
            for (int i = 0; i < poolSize / 4; i++)
            {
                GameObject cannonball = CreateCannonballObject();
                cannonball.SetActive(false);
                cannonballPool.Enqueue(cannonball);
            }
        }

        private GameObject CreateBulletObject()
        {
            GameObject bullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bullet.name = "PooledTracer";
            bullet.transform.SetParent(transform);
            bullet.transform.localScale = Vector3.one * 0.25f;

            Collider col = bullet.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer r = bullet.GetComponent<Renderer>();
            r.sharedMaterial = cachedTracerMaterial;

            // No Point Light — they are extremely expensive at scale (100+ active bullets)

            bullet.AddComponent<Projectile>();
            return bullet;
        }

        private GameObject CreateCannonballObject()
        {
            GameObject cannonball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cannonball.name = "PooledCannonball";
            cannonball.transform.SetParent(transform);
            cannonball.transform.localScale = Vector3.one * 0.25f;

            Rigidbody rb = cannonball.AddComponent<Rigidbody>();
            rb.mass = 5.5f; // ~12-pound round shot
            rb.useGravity = true;
            rb.linearDamping = 0.02f;   // Very low drag — iron ball in air
            rb.angularDamping = 0.05f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            SphereCollider col = cannonball.GetComponent<SphereCollider>();
            if (col != null)
            {
                col.radius = 0.5f;
                col.sharedMaterial = cachedBounceMaterial;
            }

            Renderer r = cannonball.GetComponent<Renderer>();
            r.sharedMaterial = cachedCannonballMaterial;

            cannonball.AddComponent<PhysicsProjectile>();
            return cannonball;
        }

        private GameObject GetBulletFromPool()
        {
            if (bulletPool.Count > 0)
            {
                GameObject bullet = bulletPool.Dequeue();
                bullet.SetActive(true);
                return bullet;
            }
            return CreateBulletObject();
        }

        private GameObject GetCannonballFromPool()
        {
            if (cannonballPool.Count > 0)
            {
                GameObject cannonball = cannonballPool.Dequeue();
                cannonball.SetActive(true);
                return cannonball;
            }
            return CreateCannonballObject();
        }

        public void ReturnBulletToPool(GameObject bullet)
        {
            bullet.SetActive(false);
            bulletPool.Enqueue(bullet);
        }

        public void ReturnCannonballToPool(GameObject cannonball)
        {
            cannonball.SetActive(false);
            Rigidbody rb = cannonball.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            activePhysicsProjectiles = Mathf.Max(activePhysicsProjectiles - 1, 0);
            cannonballPool.Enqueue(cannonball);
        }

        public void FireProjectile(Vector3 origin, Vector3 target, float damage, int attackerTeam)
        {
            GameObject bullet = GetBulletFromPool();
            bullet.transform.position = origin + Vector3.up * 1.5f;

            Projectile proj = bullet.GetComponent<Projectile>();
            proj.Initialize(target, bulletSpeed * 1.875f, damage, attackerTeam, bulletLifetime * 0.6f);
            proj.SetTracerMode(true);

            if (SmokeCloudSystem.Instance != null)
            {
                SmokeCloudSystem.Instance.AddSmoke(origin, 1f);
            }
        }

        public void FireCannonball(Vector3 origin, Vector3 target, float damage, float splashRadius, int attackerTeam)
        {
            // Cap active physics projectiles to prevent frame drops during volleys
            if (activePhysicsProjectiles >= MAX_ACTIVE_PHYSICS_PROJECTILES)
                return;

            if (NapoleonicWars.Core.BattleSoundManager.Instance != null)
                NapoleonicWars.Core.BattleSoundManager.Instance.PlayCannonFire(origin);

            GameObject cannonball = GetCannonballFromPool();
            cannonball.transform.position = origin;
            activePhysicsProjectiles++;

            Rigidbody rb = cannonball.GetComponent<Rigidbody>();
            rb.linearDamping = 0.02f; // Reset in case pool reuse changed it
            
            // Calculate velocity for FLAT, FAST ballistic trajectory
            Vector3 toTarget = target - origin;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            
            // Much flatter trajectory (12°) — cannonballs were fired nearly flat
            float launchAngle = 12f * Mathf.Deg2Rad;
            float velocity = Mathf.Sqrt(distance * Mathf.Abs(Physics.gravity.y) / Mathf.Sin(2f * launchAngle));
            
            // Velocity multiplier for devastating impact + ensure minimum speed
            velocity = Mathf.Max(velocity * 1.8f, 60f);
            
            Vector3 velocityDir = toTarget.normalized;
            velocityDir.y = Mathf.Tan(launchAngle);
            velocityDir = velocityDir.normalized;
            
            rb.linearVelocity = velocityDir * velocity;

            PhysicsProjectile proj = cannonball.GetComponent<PhysicsProjectile>();
            proj.Initialize(damage, splashRadius, attackerTeam, 10f);
        }
    }

    public class Projectile : MonoBehaviour
    {
        private Vector3 targetPos;
        private float speed;
        private float damage;
        private int attackerTeam;
        private float lifetime;
        private bool isCannonball;
        private float splashRadius;
        private bool isTracer;

        // Arc trajectory
        private Vector3 startPos;
        private float journeyLength;
        private float distanceTraveled;
        private float arcHeight;

        public void Initialize(Vector3 target, float speed, float damage, int team, float lifetime)
        {
            this.targetPos = target;
            this.speed = speed;
            this.damage = damage;
            this.attackerTeam = team;
            this.lifetime = lifetime;
            this.isCannonball = false;
            this.isTracer = false;

            startPos = transform.position;
            journeyLength = Vector3.Distance(startPos, targetPos);
        }

        public void SetTracerMode(bool enabled)
        {
            isTracer = enabled;
        }

        public void InitializeCannonball(Vector3 target, float speed, float damage, float splashRadius, int team, float lifetime)
        {
            Initialize(target, speed, damage, team, lifetime);
            this.isCannonball = true;
            this.splashRadius = splashRadius;
            this.arcHeight = Mathf.Clamp(journeyLength * 0.15f, 2f, 15f);
        }

        private void Update()
        {
            lifetime -= Time.deltaTime;
            if (lifetime <= 0f)
            {
                // Return to pool instead of Destroy to avoid GC spike
                if (ProjectileSystem.Instance != null)
                    ProjectileSystem.Instance.ReturnBulletToPool(gameObject);
                else
                    Destroy(gameObject);
                return;
            }

            float step = speed * Time.deltaTime;
            distanceTraveled += step;

            if (isCannonball)
            {
                // Arc trajectory for cannonballs
                float t = Mathf.Clamp01(distanceTraveled / journeyLength);
                Vector3 flatPos = Vector3.Lerp(startPos, targetPos, t);
                float heightOffset = arcHeight * 4f * t * (1f - t); // parabola
                transform.position = new Vector3(flatPos.x, flatPos.y + heightOffset, flatPos.z);
            }
            else
            {
                // Straight line for bullets
                Vector3 direction = (targetPos - transform.position).normalized;
                transform.position += direction * step;
                
                // Rotate tracer to face direction of travel
                if (isTracer && direction.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(direction);
                }
            }

            // Check if reached target (sqrMagnitude avoids expensive sqrt)
            float dtx = transform.position.x - targetPos.x;
            float dtz = transform.position.z - targetPos.z;
            float dty = transform.position.y - targetPos.y;
            float distSqr = dtx * dtx + dty * dty + dtz * dtz;
            if (distSqr < 1f || distanceTraveled >= journeyLength)
            {
                OnImpact();
            }
        }

        private void OnImpact()
        {
            if (isCannonball && splashRadius > 0f)
            {
                // Area damage
                Collider[] hits = Physics.OverlapSphere(transform.position, splashRadius);
                foreach (var hit in hits)
                {
                    UnitBase unit = hit.GetComponent<UnitBase>();
                    if (unit != null && unit.TeamId != attackerTeam)
                    {
                        float dist = Vector3.Distance(transform.position, unit.transform.position);
                        float falloff = 1f - (dist / splashRadius);
                        unit.TakeDamage(damage * Mathf.Max(falloff, 0.3f), null);
                    }
                }

                // Simple explosion effect
                CreateExplosionEffect();
            }

            // Return to pool instead of destroying
            if (ProjectileSystem.Instance != null)
                ProjectileSystem.Instance.ReturnBulletToPool(gameObject);
            else
                Destroy(gameObject);
        }

        private void CreateExplosionEffect()
        {
            if (NapoleonicWars.Core.BattleSoundManager.Instance != null)
                NapoleonicWars.Core.BattleSoundManager.Instance.PlayCannonImpact(transform.position);

            // Use advanced multi-phase VFX if available
            if (AdvancedParticleManager.Instance != null)
                AdvancedParticleManager.Instance.SpawnExplosion(transform.position);
            else
                ExplosionEffect.SpawnExplosion(transform.position, 4f);
        }
    }

    /// <summary>
    /// Physics-based projectile that uses Rigidbody and collisions.
    /// Cannonball bounces and kills on EVERY impact + rolls through lines.
    /// </summary>
    public class PhysicsProjectile : MonoBehaviour
    {
        private float damage;
        private float splashRadius;
        private int attackerTeam;
        private float lifetime;
        private int bounceCount = 0;
        private const int MAX_DAMAGING_BOUNCES = 5;
        
        // Rolling kill mechanic — continuous damage while fast
        private float rollingDamageTimer = 0f;
        private const float ROLLING_DAMAGE_INTERVAL = 0.1f; // 100ms interval (was 50ms) — halves query frequency
        private const float MIN_SPEED_FOR_ROLLING_KILL = 5f; // Lower threshold for more kills
        private const float ROLLING_KILL_RADIUS = 1.5f; // Slightly larger radius for path hits
        
        // Path piercing — track positions for line collision
        private Vector3 lastPosition;
        private float pierceDamageMultiplier = 1f; // Starts at full, reduces slightly per hit
        
        // Track already-hit units to avoid double-hitting on the same pass
        private HashSet<int> hitUnitsThisPass = new HashSet<int>();
        private float hitClearTimer = 0f;
        
        // Cached component — avoid GetComponent every frame
        private Rigidbody cachedRb;
        
        // Throttle piercing damage check — SphereCastAll is very expensive
        private int pierceFrameSkip = 0;

        private void Awake()
        {
            cachedRb = GetComponent<Rigidbody>();
            lastPosition = transform.position;
        }

        public void Initialize(float damage, float splashRadius, int attackerTeam, float lifetime)
        {
            this.damage = damage;
            this.splashRadius = splashRadius;
            this.attackerTeam = attackerTeam;
            this.lifetime = lifetime;
            this.bounceCount = 0;
            this.rollingDamageTimer = 0f;
            this.hitClearTimer = 0f;
            hitUnitsThisPass.Clear();
            if (cachedRb == null) cachedRb = GetComponent<Rigidbody>();
            
            StartCoroutine(ReturnToPoolAfterDelay(lifetime));
        }

        private void Update()
        {
            if (cachedRb == null) return;
            
            float speed = cachedRb.linearVelocity.magnitude;
            
            // === PIERCING PATH DAMAGE — throttle to every 3 frames ===
            // Physics.SphereCastAll is extremely expensive; running every frame is wasteful
            pierceFrameSkip++;
            if (pierceFrameSkip >= 3)
            {
                pierceFrameSkip = 0;
                ApplyPathPiercingDamage(speed);
            }
            
            // === ROLLING KILL — additional damage while rolling fast ===
            if (speed > MIN_SPEED_FOR_ROLLING_KILL)
            {
                rollingDamageTimer -= Time.deltaTime;
                if (rollingDamageTimer <= 0f)
                {
                    rollingDamageTimer = ROLLING_DAMAGE_INTERVAL;
                    ApplyRollingDamage(speed);
                }
            }
            
            // Clear hit tracking every 0.5s so units can be hit again on return bounce
            hitClearTimer -= Time.deltaTime;
            if (hitClearTimer <= 0f)
            {
                hitClearTimer = 0.5f;
                hitUnitsThisPass.Clear();
            }
            
            // Update last position for next frame's path check
            lastPosition = transform.position;
        }

        /// <summary>
        /// Pierce through enemies along the cannonball's path.
        /// Uses linecast from last position to current position to hit EVERYTHING in the way.
        /// This is the "blow through the line" cannonball effect.
        /// </summary>
        private void ApplyPathPiercingDamage(float speed)
        {
            Vector3 currentPos = transform.position;
            float distance = Vector3.Distance(lastPosition, currentPos);
            
            // Don't check if barely moving
            if (distance < 0.1f) return;
            
            // Direction from last position to current
            Vector3 direction = (currentPos - lastPosition).normalized;
            
            // Raycast against all layers to find units
            // Use sphere cast for width, or multiple raycasts for better coverage
            RaycastHit[] hits = Physics.SphereCastAll(lastPosition, ROLLING_KILL_RADIUS, direction, distance);
            
            foreach (RaycastHit hit in hits)
            {
                UnitBase unit = hit.collider.GetComponent<UnitBase>();
                if (unit == null) continue;
                if (unit.TeamId == attackerTeam) continue; // Don't hit friendlies
                if (unit.CurrentState == UnitState.Dead) continue;
                
                int unitId = unit.GetInstanceID();
                if (hitUnitsThisPass.Contains(unitId)) continue; // Already hit this pass
                
                // Mark as hit
                hitUnitsThisPass.Add(unitId);
                
                // Calculate damage based on speed and pierce multiplier
                float speedRatio = Mathf.Clamp01(speed / 80f); // Normalized 0-1
                float pierceDamage = damage * pierceDamageMultiplier * speedRatio * 1.5f; // 1.5x for direct path hit
                
                // Apply devastating damage
                unit.TakeDamage(pierceDamage, null);
                
                // Send unit flying with impact force
                if (cachedRb != null)
                {
                    Vector3 impactDir = new Vector3(direction.x, 0.3f, direction.z).normalized;
                    unit.ApplyImpact(impactDir, 25f * speedRatio);
                }
                
                // Morale shock
                if (unit.Regiment != null)
                    unit.Regiment.ApplyMoraleShock(12f);
                
                // Reduce pierce multiplier slightly for each hit (loses some energy)
                pierceDamageMultiplier *= 0.92f; // 8% loss per penetration
                if (pierceDamageMultiplier < 0.3f) pierceDamageMultiplier = 0.3f; // Minimum 30% damage
                
                // Visual blood effect
                if (AdvancedParticleManager.Instance != null)
                {
                    AdvancedParticleManager.Instance.SpawnBloodSplat(hit.point);
                }
            }
        }

        private System.Collections.IEnumerator ReturnToPoolAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (ProjectileSystem.Instance != null)
                ProjectileSystem.Instance.ReturnCannonballToPool(gameObject);
            else
                Destroy(gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            // === CANNONBALL DOES NOT EXPLODE - it keeps rolling and cutting through infantry ===
            // Only direct unit hits matter
            
            // Check if a unit was hit directly
            UnitBase directHit = collision.gameObject.GetComponent<UnitBase>();
            if (directHit != null && directHit.TeamId != attackerTeam && directHit.CurrentState != UnitState.Dead)
            {
                // Direct hit = devastating penetration
                directHit.TakeDamage(damage * 2f, null);
                
                // Apply morale shock to the regiment
                if (directHit.Regiment != null)
                    directHit.Regiment.ApplyMoraleShock(15f);
                
                // Apply impact force to ragdoll
                if (cachedRb != null)
                    directHit.ApplyImpact(cachedRb.linearVelocity.normalized, 20f);
                
                // CANNONBALL CONTINUES - no bounce damage, it just keeps going!
            }
            else
            {
                // Check if we hit terrain/ground (safe check — no CompareTag which throws if tag is undefined)
                string goName = collision.gameObject.name;
                bool isTerrain = collision.gameObject.GetComponent<Terrain>() != null
                    || goName.Contains("Terrain") || goName.Contains("Ground");
                
                if (!isTerrain)
                {
                    // Hit something else (building, obstacle) - play impact sound but keep going
                    if (bounceCount < 3) // Play sound for first few bounces only
                    {
                        bounceCount++;
                        CreateImpactEffect();
                    }
                }
                // If hit terrain/ground, just keep rolling (no bounce sound, no explosion)
            }
        }

        /// <summary>
        /// Continuous rolling damage — the cannonball kills everything it passes near
        /// while it's still moving fast enough. This is the "piercing through lines" effect.
        /// </summary>
        private void ApplyRollingDamage(float speed)
        {
            // Faster ball = more damage
            float speedRatio = Mathf.Clamp01(speed / 60f); // Normalized 0-1
            float rollingDmg = damage * 0.8f * speedRatio;
            
            // Use spatial grid if available for performance
            if (SpatialGrid.Instance != null)
            {
                // Fast path: spatial grid query
                UnitBase nearest = SpatialGrid.Instance.GetNearestEnemy(transform.position, attackerTeam, ROLLING_KILL_RADIUS);
                if (nearest != null && nearest.CurrentState != UnitState.Dead)
                {
                    int unitId = nearest.GetInstanceID();
                    if (!hitUnitsThisPass.Contains(unitId))
                    {
                        hitUnitsThisPass.Add(unitId);
                        nearest.TakeDamage(rollingDmg, null);
                        
                        // Impact force — send them flying
                        if (cachedRb != null)
                            nearest.ApplyImpact(cachedRb.linearVelocity.normalized, 15f);
                        
                        // Morale shock to witnesses
                        if (nearest.Regiment != null)
                            nearest.Regiment.ApplyMoraleShock(8f);
                    }
                }
            }
            else
            {
                // Fallback: physics sphere check
                Collider[] hits = Physics.OverlapSphere(transform.position, ROLLING_KILL_RADIUS);
                foreach (var hit in hits)
                {
                    UnitBase unit = hit.GetComponent<UnitBase>();
                    if (unit != null && unit.TeamId != attackerTeam && unit.CurrentState != UnitState.Dead)
                    {
                        int unitId = unit.GetInstanceID();
                        if (!hitUnitsThisPass.Contains(unitId))
                        {
                            hitUnitsThisPass.Add(unitId);
                            unit.TakeDamage(rollingDmg, null);
                            
                            if (cachedRb != null)
                                unit.ApplyImpact(cachedRb.linearVelocity.normalized, 15f);
                            
                            if (unit.Regiment != null)
                                unit.Regiment.ApplyMoraleShock(8f);
                        }
                    }
                }
            }
        }

        private void ApplyAreaDamage(float bounceFalloff)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, splashRadius);
            foreach (var hit in hits)
            {
                UnitBase unit = hit.GetComponent<UnitBase>();
                if (unit != null && unit.TeamId != attackerTeam && unit.CurrentState != UnitState.Dead)
                {
                    float dist = Vector3.Distance(transform.position, unit.transform.position);
                    float falloff = 1f - (dist / splashRadius);
                    float finalDamage = damage * Mathf.Max(falloff, 0.3f) * bounceFalloff;
                    unit.TakeDamage(finalDamage, null);
                }
            }
        }

        private void CreateImpactEffect()
        {
            if (NapoleonicWars.Core.BattleSoundManager.Instance != null)
                NapoleonicWars.Core.BattleSoundManager.Instance.PlayCannonImpact(transform.position);

            // Use advanced multi-phase VFX if available
            if (AdvancedParticleManager.Instance != null)
                AdvancedParticleManager.Instance.SpawnExplosion(transform.position);
            else
            {
                float scale = splashRadius * (bounceCount <= 1 ? 1.5f : 0.8f);
                ExplosionEffect.SpawnExplosion(transform.position, scale);
            }
        }
    }

    public class ExplosionEffect : MonoBehaviour
    {
        private float timer = 0.5f;
        public float maxScale = 4f;

        // Cached component — avoid GetComponent every frame
        private Renderer cachedRenderer;
        private MaterialPropertyBlock mpb;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        // Static pool for explosion effects
        private static Queue<GameObject> explosionPool = new Queue<GameObject>(16);
        private static Material cachedExplosionMat;
        private static int activeExplosions;
        private const int MaxActiveExplosions = 20;

        /// <summary>
        /// Spawn an explosion from the pool (no GC allocation).
        /// </summary>
        public static void SpawnExplosion(Vector3 position, float scale)
        {
            if (activeExplosions >= MaxActiveExplosions) return;

            GameObject go;
            if (explosionPool.Count > 0)
            {
                go = explosionPool.Dequeue();
                if (go == null)
                {
                    go = CreateExplosionObject();
                }
            }
            else
            {
                go = CreateExplosionObject();
            }

            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.5f;
            go.SetActive(true);

            ExplosionEffect effect = go.GetComponent<ExplosionEffect>();
            effect.timer = 0.5f;
            effect.maxScale = scale;
            activeExplosions++;
        }

        private static GameObject CreateExplosionObject()
        {
            if (cachedExplosionMat == null)
                cachedExplosionMat = URPMaterialHelper.CreateLitEmissive(
                    new Color(1f, 0.6f, 0.1f), new Color(1f, 0.4f, 0f) * 3f);

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "PooledExplosion";

            Collider col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = cachedExplosionMat;

            ExplosionEffect effect = go.AddComponent<ExplosionEffect>();
            effect.cachedRenderer = r;
            effect.mpb = new MaterialPropertyBlock();
            return go;
        }

        private void Awake()
        {
            if (cachedRenderer == null) cachedRenderer = GetComponent<Renderer>();
            if (mpb == null) mpb = new MaterialPropertyBlock();
        }

        private void Update()
        {
            timer -= Time.deltaTime;
            float t = 1f - (timer / 0.5f);
            float scale = Mathf.Lerp(0.5f, maxScale, t);
            transform.localScale = Vector3.one * scale;

            // Use MaterialPropertyBlock — zero allocation, no material clone
            if (cachedRenderer != null)
            {
                float alpha = Mathf.Lerp(1f, 0f, t);
                cachedRenderer.GetPropertyBlock(mpb);
                Color c = new Color(1f, 0.6f, 0.1f, alpha);
                mpb.SetColor(ColorId, c);
                mpb.SetColor(BaseColorId, c);
                cachedRenderer.SetPropertyBlock(mpb);
            }

            if (timer <= 0f)
            {
                activeExplosions--;
                gameObject.SetActive(false);
                explosionPool.Enqueue(gameObject);
            }
        }
    }
}
