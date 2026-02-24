using UnityEngine;
using System.Collections.Generic;

namespace NapoleonicWars.Core
{
    /// <summary>
    /// Advanced Battle VFX system — realistic multi-phase effects for Napoleonic warfare.
    /// Each effect uses multiple particle sub-systems for realism:
    /// muzzle flash, directional smoke, sparks, debris, shockwaves, fire, etc.
    /// All prefabs are pooled for zero-GC gameplay.
    /// </summary>
    public class AdvancedParticleManager : MonoBehaviour
    {
        public static AdvancedParticleManager Instance { get; private set; }

        // Prefab references (created at Start)
        private GameObject musketSmokePrefab;
        private GameObject cannonFirePrefab;
        private GameObject cannonImpactPrefab;
        private GameObject volleySmokePrefab;
        private GameObject dustCloudPrefab;
        private GameObject bloodSplatPrefab;
        private GameObject cavalryDustPrefab;
        private GameObject shrapnelBurstPrefab;
        private GameObject firePrefab;
        private GameObject bayonetSparkPrefab;

        // Shared materials (cached to reduce draw calls)
        private Material smokeParticleMat;
        private Material flashParticleMat;
        private Material sparkParticleMat;
        private Material debrisParticleMat;
        private Material bloodParticleMat;
        private Material fireMat;
        private Material emberMat;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            CreateMaterials();
            CreateAllPrefabs();
            RegisterPools();
        }

        // ===========================================================================
        // PUBLIC API — call these from gameplay code
        // ===========================================================================

        /// <summary>
        /// Musket fire: quick muzzle flash + forward smoke plume + sparks
        /// </summary>
        public void SpawnMusketSmoke(Vector3 position, Vector3 direction)
        {
            SpawnPooled("MusketSmoke", position, Quaternion.LookRotation(direction), 3.5f);
        }

        /// <summary>
        /// Cannon fire: massive blast flash + expanding smoke ring + sparks + ground debris
        /// </summary>
        public void SpawnCannonFire(Vector3 position, Vector3 direction)
        {
            SpawnPooled("CannonFire", position, Quaternion.LookRotation(direction), 6f);
            // Camera shake + bloom pulse
            if (CameraShake.Instance != null)
                CameraShake.Instance.CannonFireImpact(position);
        }

        /// <summary>
        /// Cannonball impact: dirt geyser + smoke cloud + debris + shockwave ring
        /// </summary>
        public void SpawnExplosion(Vector3 position)
        {
            SpawnPooled("CannonImpact", position, Quaternion.identity, 5f);
            // Full explosion camera impact
            if (CameraShake.Instance != null)
                CameraShake.Instance.ExplosionImpact(position, 1f);
            // Ground scorch decal
            SpawnGroundDecal(position);
        }

        /// <summary>
        /// Volley smoke: thick lingering wall of smoke from line infantry firing
        /// </summary>
        public void SpawnVolleySmoke(Vector3 position, Vector3 direction, float width = 8f)
        {
            SpawnPooled("VolleySmoke", position, Quaternion.LookRotation(direction), 10f, width);
        }

        /// <summary>
        /// Dust cloud from movement / charges
        /// </summary>
        public void SpawnDustCloud(Vector3 position, float scale = 1f)
        {
            SpawnPooled("DustCloud", position, Quaternion.identity, 4f, scale);
        }

        /// <summary>
        /// Blood hit effect: splatter mist + drops
        /// </summary>
        public void SpawnBloodSplat(Vector3 position)
        {
            SpawnPooled("BloodSplat", position, Quaternion.identity, 2.5f);
        }

        /// <summary>
        /// Cavalry charge dust: trailing dust clouds behind horses
        /// </summary>
        public void SpawnCavalryDust(Vector3 position, Vector3 direction)
        {
            SpawnPooled("CavalryDust", position, Quaternion.LookRotation(direction), 3f);
        }

        /// <summary>
        /// Grape shot / shrapnel burst: air-burst with metal fragments
        /// </summary>
        public void SpawnShrapnelBurst(Vector3 position)
        {
            SpawnPooled("ShrapnelBurst", position, Quaternion.identity, 4f);
            if (CameraShake.Instance != null)
                CameraShake.Instance.ExplosionImpact(position, 0.6f);
        }

        /// <summary>
        /// Fire effect: burning building / unit with flames + embers + smoke column
        /// </summary>
        public void SpawnFire(Vector3 position, float scale = 1f)
        {
            SpawnPooled("Fire", position, Quaternion.identity, 12f, scale);
        }

        /// <summary>
        /// Bayonet charge impact: metal sparks from melee clash
        /// </summary>
        public void SpawnBayonetSpark(Vector3 position)
        {
            SpawnPooled("BayonetSpark", position, Quaternion.identity, 1.5f);
        }

        // ===========================================================================
        // POOLING HELPERS
        // ===========================================================================

        private void SpawnPooled(string poolName, Vector3 pos, Quaternion rot, float returnDelay, float scale = 1f)
        {
            if (ObjectPool.Instance == null) return;
            GameObject go = ObjectPool.Instance.Get(poolName);
            if (go == null) return;

            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = Vector3.one * scale;

            // Play all particle systems on the object
            var systems = go.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in systems)
            {
                ps.Clear();
                ps.Play();
            }

            ObjectPool.Instance.ReturnDelayed(poolName, go, returnDelay);
        }

        private void RegisterPools()
        {
            if (ObjectPool.Instance == null) return;

            ObjectPool.Instance.RegisterPool("MusketSmoke", musketSmokePrefab, 30);
            ObjectPool.Instance.RegisterPool("CannonFire", cannonFirePrefab, 12);
            ObjectPool.Instance.RegisterPool("CannonImpact", cannonImpactPrefab, 12);
            ObjectPool.Instance.RegisterPool("VolleySmoke", volleySmokePrefab, 10);
            ObjectPool.Instance.RegisterPool("DustCloud", dustCloudPrefab, 20);
            ObjectPool.Instance.RegisterPool("BloodSplat", bloodSplatPrefab, 25);
            ObjectPool.Instance.RegisterPool("CavalryDust", cavalryDustPrefab, 15);
            ObjectPool.Instance.RegisterPool("ShrapnelBurst", shrapnelBurstPrefab, 8);
            ObjectPool.Instance.RegisterPool("Fire", firePrefab, 6);
            ObjectPool.Instance.RegisterPool("BayonetSpark", bayonetSparkPrefab, 15);
        }

        // ===========================================================================
        // MATERIAL CREATION
        // ===========================================================================

        // Scorch decal materials
        private Material scorchDecalMat;
        private Material shockwaveMat;

        private void CreateMaterials()
        {
            // Smoke: textured volumetric puff
            smokeParticleMat = CreateTexturedParticleMat(
                new Color(0.75f, 0.72f, 0.65f, 0.45f), false, VFXTextureGenerator.SmokeTexture);
            // Flash: bright starburst
            flashParticleMat = CreateTexturedParticleMat(
                new Color(1f, 0.95f, 0.7f, 1f), true, VFXTextureGenerator.FlashTexture);
            // Sparks: bright dot with glow
            sparkParticleMat = CreateTexturedParticleMat(
                new Color(1f, 0.7f, 0.2f, 1f), true, VFXTextureGenerator.SparkTexture);
            // Debris: textured chunks
            debrisParticleMat = CreateTexturedParticleMat(
                new Color(0.35f, 0.28f, 0.18f, 0.8f), false, VFXTextureGenerator.DebrisTexture);
            // Blood: organic splatter
            bloodParticleMat = CreateTexturedParticleMat(
                new Color(0.45f, 0.02f, 0.02f, 0.85f), false, VFXTextureGenerator.BloodTexture);
            // Fire: volumetric flame
            fireMat = CreateTexturedParticleMat(
                new Color(1f, 0.6f, 0.1f, 0.9f), true, VFXTextureGenerator.FireTexture);
            // Embers: bright spark dot
            emberMat = CreateTexturedParticleMat(
                new Color(1f, 0.45f, 0.05f, 1f), true, VFXTextureGenerator.SparkTexture);
            // Scorch ground decal
            scorchDecalMat = CreateTexturedParticleMat(
                new Color(0.05f, 0.03f, 0.02f, 0.7f), false, VFXTextureGenerator.ScorchTexture);
            // Shockwave ring
            shockwaveMat = CreateTexturedParticleMat(
                new Color(1f, 0.95f, 0.8f, 0.6f), true, VFXTextureGenerator.ShockwaveTexture);
        }

        private Material CreateTexturedParticleMat(Color color, bool additive, Texture2D texture)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Standard");

            Material mat = new Material(shader);
            mat.color = color;

            // Apply texture
            if (texture != null)
            {
                mat.mainTexture = texture;
                mat.SetTexture("_BaseMap", texture);
            }

            if (additive)
            {
                mat.SetFloat("_Surface", 1);
                mat.SetFloat("_Blend", 1); // Additive
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            }
            else
            {
                mat.SetFloat("_Surface", 1);
                mat.SetFloat("_Blend", 0); // Alpha
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }
            mat.SetInt("_ZWrite", 0);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = additive ? 3100 : 3000;

            return mat;
        }

        // ===========================================================================
        // PREFAB FACTORIES
        // ===========================================================================

        private void CreateAllPrefabs()
        {
            musketSmokePrefab = BuildMusketSmokePrefab();
            cannonFirePrefab = BuildCannonFirePrefab();
            cannonImpactPrefab = BuildCannonImpactPrefab();
            volleySmokePrefab = BuildVolleySmokePrefab();
            dustCloudPrefab = BuildDustCloudPrefab();
            bloodSplatPrefab = BuildBloodSplatPrefab();
            cavalryDustPrefab = BuildCavalryDustPrefab();
            shrapnelBurstPrefab = BuildShrapnelBurstPrefab();
            firePrefab = BuildFirePrefab();
            bayonetSparkPrefab = BuildBayonetSparkPrefab();
        }

        // -------------------------------------------------------
        // MUSKET SMOKE — 3 sub-systems: flash, smoke plume, sparks
        // -------------------------------------------------------
        private GameObject BuildMusketSmokePrefab()
        {
            var root = MakePrefabRoot("MusketSmoke_Prefab");

            // 1) MUZZLE FLASH — brief bright burst
            {
                var go = AddChild(root, "MuzzleFlash");
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 0.05f;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
                main.startSpeed = 0.5f;
                main.startSize = new ParticleSystem.MinMaxCurve(1f, 2f);
                main.startColor = new Color(1f, 0.95f, 0.6f, 1f);
                main.maxParticles = 5;
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                var em = ps.emission;
                em.rateOverTime = 0;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, 3, 5) });

                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 8f;
                sh.radius = 0.05f;

                SetupFadeOut(ps, new Color(1f, 0.95f, 0.6f), new Color(1f, 0.5f, 0.1f), 1f, 0f);
                SetRenderer(go, flashParticleMat);
            }

            // 2) SMOKE PLUME — medium cone of smoke forward
            {
                var go = AddChild(root, "SmokePlume");
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 0.3f;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 2f);
                main.startColor = new Color(0.8f, 0.78f, 0.7f, 0.5f);
                main.maxParticles = 40;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = -0.08f; // Rise slightly
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);

                var em = ps.emission;
                em.rateOverTime = 0;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, 15, 25) });

                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 18f;
                sh.radius = 0.08f;

                // Size growth
                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                var sizeCurve = new AnimationCurve(
                    new Keyframe(0f, 0.4f),
                    new Keyframe(0.3f, 1f),
                    new Keyframe(1f, 1.6f)
                );
                sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

                SetupSmokeGradient(ps, 0.5f);

                // Noise for turbulence
                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 0.8f;
                noise.frequency = 0.4f;
                noise.scrollSpeed = 0.3f;
                noise.octaveCount = 2;

                // Rotation
                var rot = ps.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

                SetRenderer(go, smokeParticleMat);
            }

            // 3) SPARKS — tiny bright particles ejected fast
            {
                var go = AddChild(root, "Sparks");
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 0.05f;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 12f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
                main.startColor = new Color(1f, 0.8f, 0.3f, 1f);
                main.maxParticles = 15;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = 2f;

                var em = ps.emission;
                em.rateOverTime = 0;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, 5, 12) });

                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 20f;
                sh.radius = 0.02f;

                SetupFadeOut(ps, new Color(1f, 0.8f, 0.3f), new Color(1f, 0.3f, 0f), 1f, 0f);

                var renderer = SetRenderer(go, sparkParticleMat);
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 3f;
            }

            return root;
        }

        // -------------------------------------------------------
        // CANNON FIRE — 4 sub-systems: massive flash, smoke ring, sparks, ground debris
        // -------------------------------------------------------
        private GameObject BuildCannonFirePrefab()
        {
            var root = MakePrefabRoot("CannonFire_Prefab");

            // 1) MASSIVE MUZZLE FLASH
            {
                var go = AddChild(root, "MuzzleFlash");
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 0.08f;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
                main.startSpeed = 1f;
                main.startSize = new ParticleSystem.MinMaxCurve(4f, 8f);
                main.startColor = new Color(1f, 0.95f, 0.5f, 1f);
                main.maxParticles = 8;
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                var em = ps.emission;
                em.rateOverTime = 0;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, 5, 8) });

                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 12f;
                sh.radius = 0.2f;

                SetupFadeOut(ps, new Color(1f, 0.95f, 0.5f), new Color(1f, 0.4f, 0f), 1f, 0f);
                SetRenderer(go, flashParticleMat);
            }

            // 2) MASSIVE SMOKE CLOUD
            {
                var go = AddChild(root, "SmokeBall");
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 0.5f;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f);
                main.startSize = new ParticleSystem.MinMaxCurve(2f, 4f);
                main.startColor = new Color(0.75f, 0.72f, 0.65f, 0.6f);
                main.maxParticles = 120;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = -0.12f;
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);

                var em = ps.emission;
                em.rateOverTime = 0;
                em.SetBursts(new[] {
                    new ParticleSystem.Burst(0f, 40, 60),
                    new ParticleSystem.Burst(0.05f, 20, 30) // Secondary burst
                });

                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 30f;
                sh.radius = 0.3f;

                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                var sizeCurve = new AnimationCurve(
                    new Keyframe(0f, 0.3f),
                    new Keyframe(0.2f, 1f),
                    new Keyframe(1f, 2f)
                );
                sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

                SetupSmokeGradient(ps, 0.6f);

                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 1.2f;
                noise.frequency = 0.3f;
                noise.scrollSpeed = 0.4f;
                noise.octaveCount = 3;

                var rot = ps.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);

                SetRenderer(go, smokeParticleMat);
            }

            // 3) HOT SPARKS — many bright embers ejected
            {
                var go = AddChild(root, "HotSparks");
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 0.1f;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 18f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
                main.startColor = new Color(1f, 0.7f, 0.2f, 1f);
                main.maxParticles = 30;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = 3f;

                var em = ps.emission;
                em.rateOverTime = 0;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, 15, 25) });

                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 25f;
                sh.radius = 0.1f;

                SetupFadeOut(ps, new Color(1f, 0.9f, 0.4f), new Color(1f, 0.2f, 0f), 1f, 0f);

                var renderer = SetRenderer(go, sparkParticleMat);
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 4f;
            }

            // 4) GROUND DUST ring
            {
                var go = AddChild(root, "GroundDust");
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 0.15f;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
                main.startSize = new ParticleSystem.MinMaxCurve(2.5f, 5f);
                main.startColor = new Color(0.55f, 0.48f, 0.35f, 0.3f);
                main.maxParticles = 30;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = 0.05f;

                var em = ps.emission;
                em.rateOverTime = 0;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, 10, 20) });

                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Circle;
                sh.radius = 1f;
                sh.arc = 360f;

                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                var sizeCurve = new AnimationCurve(
                    new Keyframe(0f, 0.5f), new Keyframe(1f, 1.5f)
                );
                sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

                SetupFadeOut(ps, new Color(0.55f, 0.48f, 0.35f), new Color(0.4f, 0.35f, 0.25f), 0.35f, 0f);
                SetRenderer(go, smokeParticleMat);
            }

            return root;
        }

        // -------------------------------------------------------
        // CANNON IMPACT — dirt geyser + shockwave + smoke + debris
        // -------------------------------------------------------
        private GameObject BuildCannonImpactPrefab()
        {
            var root = MakePrefabRoot("CannonImpact_Prefab");

            // 1) INITIAL FLASH (ground explosion)
            {
                var go = AddChild(root, "ImpactFlash");
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 0.06f;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
                main.startSpeed = 0.5f;
                main.startSize = new ParticleSystem.MinMaxCurve(5f, 10f);
                main.startColor = new Color(1f, 0.85f, 0.4f, 1f);
                main.maxParticles = 6;
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                var em = ps.emission;
                em.rateOverTime = 0;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, 3, 6) });

                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 0.3f;

                SetupFadeOut(ps, new Color(1f, 0.9f, 0.5f), new Color(1f, 0.3f, 0f), 1f, 0f);
                SetRenderer(go, flashParticleMat);
            }

            // 2) DIRT GEYSER — upward spray of earth
            {
                var go = AddChild(root, "DirtGeyser");
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 0.2f;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 14f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
                main.startColor = new Color(0.4f, 0.32f, 0.2f, 0.9f);
                main.maxParticles = 80;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = 4f;
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);

                var em = ps.emission;
                em.rateOverTime = 0;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, 30, 60) });

                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 35f;
                sh.radius = 0.5f;
                sh.rotation = new Vector3(-90f, 0f, 0f); // Upward

                SetupFadeOut(ps, new Color(0.4f, 0.32f, 0.2f), new Color(0.3f, 0.25f, 0.15f), 0.9f, 0.1f);
                SetRenderer(go, debrisParticleMat);
            }

            // 3) SMOKE CLOUD from impact
            {
                var go = AddChild(root, "ImpactSmoke");
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 0.4f;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
                main.startSize = new ParticleSystem.MinMaxCurve(2.5f, 5f);
                main.startColor = new Color(0.5f, 0.45f, 0.35f, 0.4f);
                main.maxParticles = 60;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = -0.15f;
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);

                var em = ps.emission;
                em.rateOverTime = 0;
                em.SetBursts(new[] {
                    new ParticleSystem.Burst(0.05f, 20, 40),
                    new ParticleSystem.Burst(0.15f, 10, 20)
                });

                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 1f;

                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                var sc = new AnimationCurve(
                    new Keyframe(0f, 0.3f), new Keyframe(0.3f, 1f), new Keyframe(1f, 2.5f)
                );
                sol.size = new ParticleSystem.MinMaxCurve(1f, sc);

                SetupSmokeGradient(ps, 0.45f);

                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 1f;
                noise.frequency = 0.4f;
                noise.scrollSpeed = 0.5f;

                SetRenderer(go, smokeParticleMat);
            }

            // 4) HOT SPARKS from impact
            {
                var go = AddChild(root, "ImpactSparks");
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 0.08f;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 15f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
                main.startColor = new Color(1f, 0.7f, 0.2f, 1f);
                main.maxParticles = 25;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = 2.5f;

                var em = ps.emission;
                em.rateOverTime = 0;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, 10, 20) });

                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 0.3f;

                SetupFadeOut(ps, new Color(1f, 0.8f, 0.3f), new Color(1f, 0.2f, 0f), 1f, 0f);

                var renderer = SetRenderer(go, sparkParticleMat);
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 3f;
            }

            // 5) EXPANDING SHOCKWAVE RING
            {
                var go = AddChild(root, "ShockwaveRing");
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 0.05f;
                main.loop = false;
                main.startLifetime = 0.4f;
                main.startSpeed = 0f;
                main.startSize = 0.5f;
                main.startColor = new Color(1f, 0.95f, 0.8f, 0.5f);
                main.maxParticles = 1;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startRotation = new ParticleSystem.MinMaxCurve(1.5708f); // 90° to lie flat

                var em = ps.emission;
                em.rateOverTime = 0;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

                // Rapid expansion
                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                var sc = new AnimationCurve(
                    new Keyframe(0f, 0.1f), new Keyframe(0.3f, 1f), new Keyframe(1f, 3f)
                );
                sol.size = new ParticleSystem.MinMaxCurve(8f, sc);

                SetupFadeOut(ps, new Color(1f, 0.95f, 0.8f), new Color(0.8f, 0.7f, 0.5f), 0.6f, 0f);

                var renderer = SetRenderer(go, shockwaveMat);
                renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            }

            return root;
        }

        // -------------------------------------------------------
        // VOLLEY SMOKE — wide, thick, lingering smoke wall
        // -------------------------------------------------------
        private GameObject BuildVolleySmokePrefab()
        {
            var root = MakePrefabRoot("VolleySmoke_Prefab");

            var go = AddChild(root, "SmokeWall");
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 2f);
            main.startColor = new Color(0.82f, 0.8f, 0.72f, 0.5f);
            main.maxParticles = 200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.06f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);

            var em = ps.emission;
            em.rateOverTime = 0;
            em.SetBursts(new[] {
                new ParticleSystem.Burst(0f, 60, 80),
                new ParticleSystem.Burst(0.15f, 30, 50),
                new ParticleSystem.Burst(0.3f, 20, 30)
            });

            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(8f, 0.5f, 1f); // Wide line

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.3f), new Keyframe(0.15f, 0.8f),
                new Keyframe(0.5f, 1.5f), new Keyframe(1f, 2.5f)
            );
            sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            SetupSmokeGradient(ps, 0.5f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.2f;
            noise.scrollSpeed = 0.15f;
            noise.octaveCount = 3;

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);

            SetRenderer(go, smokeParticleMat);
            return root;
        }

        // -------------------------------------------------------
        // DUST CLOUD — for movement/charges
        // -------------------------------------------------------
        private GameObject BuildDustCloudPrefab()
        {
            var root = MakePrefabRoot("DustCloud_Prefab");

            var go = AddChild(root, "Dust");
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startColor = new Color(0.6f, 0.55f, 0.4f, 0.25f);
            main.maxParticles = 50;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.02f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);

            var em = ps.emission;
            em.rateOverTime = 0;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 20, 40) });

            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Hemisphere;
            sh.radius = 1.5f;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            var sc = new AnimationCurve(
                new Keyframe(0f, 0.4f), new Keyframe(0.5f, 1f), new Keyframe(1f, 1.4f)
            );
            sol.size = new ParticleSystem.MinMaxCurve(1f, sc);

            SetupFadeOut(ps, new Color(0.6f, 0.55f, 0.4f), new Color(0.5f, 0.45f, 0.35f), 0.3f, 0f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.3f;

            SetRenderer(go, smokeParticleMat);
            return root;
        }

        // -------------------------------------------------------
        // BLOOD SPLAT — mist + droplets
        // -------------------------------------------------------
        private GameObject BuildBloodSplatPrefab()
        {
            var root = MakePrefabRoot("BloodSplat_Prefab");

            // 1) BLOOD MIST — soft cloud
            {
                var go = AddChild(root, "BloodMist");
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 0.08f;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
                main.startColor = new Color(0.5f, 0.02f, 0.02f, 0.7f);
                main.maxParticles = 15;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = 0.2f;

                var em = ps.emission;
                em.rateOverTime = 0;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, 8, 12) });

                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 0.15f;

                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                var sc = new AnimationCurve(
                    new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1.5f)
                );
                sol.size = new ParticleSystem.MinMaxCurve(1f, sc);

                SetupFadeOut(ps, new Color(0.5f, 0.02f, 0.02f), new Color(0.25f, 0.01f, 0.01f), 0.7f, 0f);
                SetRenderer(go, bloodParticleMat);
            }

            // 2) BLOOD DROPS — gravity-affected droplets
            {
                var go = AddChild(root, "BloodDrops");
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 0.05f;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
                main.startColor = new Color(0.4f, 0.01f, 0.01f, 0.9f);
                main.maxParticles = 15;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = 4f;

                var em = ps.emission;
                em.rateOverTime = 0;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, 5, 12) });

                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 0.1f;

                SetRenderer(go, bloodParticleMat);
            }

            return root;
        }

        // -------------------------------------------------------
        // CAVALRY DUST — trailing ground dust
        // -------------------------------------------------------
        private GameObject BuildCavalryDustPrefab()
        {
            var root = MakePrefabRoot("CavalryDust_Prefab");

            var go = AddChild(root, "TrailDust");
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.8f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 1f);
            main.startColor = new Color(0.55f, 0.5f, 0.35f, 0.2f);
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.03f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);

            var em = ps.emission;
            em.rateOverTime = 30;

            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(2f, 0.1f, 0.5f);

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            var sc = new AnimationCurve(
                new Keyframe(0f, 0.3f), new Keyframe(0.4f, 1f), new Keyframe(1f, 1.8f)
            );
            sol.size = new ParticleSystem.MinMaxCurve(1f, sc);

            SetupFadeOut(ps, new Color(0.55f, 0.5f, 0.35f), new Color(0.45f, 0.4f, 0.3f), 0.25f, 0f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.4f;
            noise.frequency = 0.3f;

            SetRenderer(go, smokeParticleMat);
            return root;
        }

        // -------------------------------------------------------
        // SHRAPNEL BURST — air-burst grape shot
        // -------------------------------------------------------
        private GameObject BuildShrapnelBurstPrefab()
        {
            var root = MakePrefabRoot("ShrapnelBurst_Prefab");

            // 1) Air flash
            {
                var go = AddChild(root, "AirFlash");
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 0.04f;
                main.loop = false;
                main.startLifetime = 0.06f;
                main.startSpeed = 0.2f;
                main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3f);
                main.startColor = new Color(1f, 0.9f, 0.5f, 0.8f);
                main.maxParticles = 4;
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                var em = ps.emission;
                em.rateOverTime = 0;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, 2, 4) });

                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 0.2f;

                SetupFadeOut(ps, new Color(1f, 0.9f, 0.5f), new Color(1f, 0.3f, 0f), 1f, 0f);
                SetRenderer(go, flashParticleMat);
            }

            // 2) Metal fragments — fast, gravity-heavy
            {
                var go = AddChild(root, "Fragments");
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 0.05f;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(10f, 25f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.04f);
                main.startColor = new Color(0.6f, 0.6f, 0.6f, 0.9f);
                main.maxParticles = 40;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = 3f;

                var em = ps.emission;
                em.rateOverTime = 0;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, 20, 35) });

                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 0.3f;

                var renderer = SetRenderer(go, sparkParticleMat);
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 5f;
            }

            // 3) Smoke puff
            {
                var go = AddChild(root, "BurstSmoke");
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 0.2f;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
                main.startColor = new Color(0.6f, 0.55f, 0.45f, 0.4f);
                main.maxParticles = 30;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = -0.1f;

                var em = ps.emission;
                em.rateOverTime = 0;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, 15, 25) });

                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 0.5f;

                SetupSmokeGradient(ps, 0.4f);
                SetRenderer(go, smokeParticleMat);
            }

            return root;
        }

        // -------------------------------------------------------
        // FIRE — flames + embers + smoke column
        // -------------------------------------------------------
        private GameObject BuildFirePrefab()
        {
            var root = MakePrefabRoot("Fire_Prefab");

            // 1) FLAMES
            {
                var go = AddChild(root, "Flames");
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 10f;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
                main.startColor = new Color(1f, 0.6f, 0.1f, 0.85f);
                main.maxParticles = 60;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = -0.5f;

                var em = ps.emission;
                em.rateOverTime = 40;

                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 10f;
                sh.radius = 0.3f;
                sh.rotation = new Vector3(-90f, 0f, 0f);

                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                var sc = new AnimationCurve(
                    new Keyframe(0f, 1f), new Keyframe(0.5f, 0.7f), new Keyframe(1f, 0.1f)
                );
                sol.size = new ParticleSystem.MinMaxCurve(1f, sc);

                Gradient grad = new Gradient();
                grad.SetKeys(
                    new[] {
                        new GradientColorKey(new Color(1f, 0.95f, 0.3f), 0f),
                        new GradientColorKey(new Color(1f, 0.5f, 0.05f), 0.3f),
                        new GradientColorKey(new Color(0.8f, 0.15f, 0f), 0.7f),
                        new GradientColorKey(new Color(0.2f, 0.05f, 0f), 1f)
                    },
                    new[] {
                        new GradientAlphaKey(0f, 0f),
                        new GradientAlphaKey(0.9f, 0.1f),
                        new GradientAlphaKey(0.7f, 0.5f),
                        new GradientAlphaKey(0f, 1f)
                    }
                );
                var col = ps.colorOverLifetime;
                col.enabled = true;
                col.color = grad;

                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 1f;
                noise.frequency = 1f;
                noise.scrollSpeed = 0.8f;

                SetRenderer(go, fireMat);
            }

            // 2) EMBERS — tiny rising sparks
            {
                var go = AddChild(root, "Embers");
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 10f;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 3f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.04f);
                main.startColor = new Color(1f, 0.6f, 0.1f, 1f);
                main.maxParticles = 30;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = -0.3f;

                var em = ps.emission;
                em.rateOverTime = 8;

                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 15f;
                sh.radius = 0.5f;
                sh.rotation = new Vector3(-90f, 0f, 0f);

                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 0.5f;
                noise.frequency = 0.8f;
                noise.scrollSpeed = 1f;

                SetupFadeOut(ps, new Color(1f, 0.6f, 0.1f), new Color(1f, 0.2f, 0f), 1f, 0f);
                SetRenderer(go, emberMat);
            }

            // 3) FIRE SMOKE — dark rising column
            {
                var go = AddChild(root, "FireSmoke");
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 10f;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
                main.startColor = new Color(0.15f, 0.12f, 0.08f, 0.35f);
                main.maxParticles = 50;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = -0.3f;
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);

                var em = ps.emission;
                em.rateOverTime = 6;

                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 8f;
                sh.radius = 0.4f;
                sh.rotation = new Vector3(-90f, 0f, 0f);

                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                var sc = new AnimationCurve(
                    new Keyframe(0f, 0.3f), new Keyframe(0.3f, 0.8f), new Keyframe(1f, 2.5f)
                );
                sol.size = new ParticleSystem.MinMaxCurve(1f, sc);

                Gradient grad = new Gradient();
                grad.SetKeys(
                    new[] {
                        new GradientColorKey(new Color(0.2f, 0.15f, 0.1f), 0f),
                        new GradientColorKey(new Color(0.15f, 0.12f, 0.08f), 0.5f),
                        new GradientColorKey(new Color(0.1f, 0.08f, 0.05f), 1f)
                    },
                    new[] {
                        new GradientAlphaKey(0f, 0f),
                        new GradientAlphaKey(0.35f, 0.1f),
                        new GradientAlphaKey(0.25f, 0.6f),
                        new GradientAlphaKey(0f, 1f)
                    }
                );
                var col = ps.colorOverLifetime;
                col.enabled = true;
                col.color = grad;

                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 0.6f;
                noise.frequency = 0.2f;
                noise.scrollSpeed = 0.2f;

                var rot = ps.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

                SetRenderer(go, smokeParticleMat);
            }

            return root;
        }

        // -------------------------------------------------------
        // BAYONET SPARK — quick metal-on-metal sparks for melee
        // -------------------------------------------------------
        private GameObject BuildBayonetSparkPrefab()
        {
            var root = MakePrefabRoot("BayonetSpark_Prefab");

            var go = AddChild(root, "MetalSparks");
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.03f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 10f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.04f);
            main.startColor = new Color(1f, 0.9f, 0.5f, 1f);
            main.maxParticles = 15;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 3f;

            var em = ps.emission;
            em.rateOverTime = 0;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 6, 12) });

            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.1f;

            SetupFadeOut(ps, new Color(1f, 0.9f, 0.5f), new Color(1f, 0.3f, 0f), 1f, 0f);

            var renderer = SetRenderer(go, sparkParticleMat);
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 4f;

            return root;
        }

        // ===========================================================================
        // UTILITY METHODS
        // ===========================================================================

        private GameObject MakePrefabRoot(string name)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            go.transform.SetParent(transform);
            return go;
        }

        private GameObject AddChild(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        private ParticleSystemRenderer SetRenderer(GameObject go, Material mat)
        {
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) renderer = go.AddComponent<ParticleSystemRenderer>();
            renderer.material = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 1;
            return renderer;
        }

        /// <summary>
        /// Sets up a standard smoke color gradient: white-gray → gray → dark gray, with fade in/out
        /// </summary>
        private void SetupSmokeGradient(ParticleSystem ps, float peakAlpha)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new[] {
                    new GradientColorKey(new Color(0.85f, 0.83f, 0.78f), 0f),
                    new GradientColorKey(new Color(0.65f, 0.63f, 0.58f), 0.3f),
                    new GradientColorKey(new Color(0.45f, 0.43f, 0.38f), 0.7f),
                    new GradientColorKey(new Color(0.3f, 0.28f, 0.25f), 1f)
                },
                new[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(peakAlpha, 0.1f),
                    new GradientAlphaKey(peakAlpha * 0.7f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = grad;
        }

        /// <summary>
        /// Sets up a simple two-color fade-out gradient
        /// </summary>
        private void SetupFadeOut(ParticleSystem ps, Color startColor, Color endColor, float startAlpha, float endAlpha)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new[] {
                    new GradientColorKey(startColor, 0f),
                    new GradientColorKey(endColor, 1f)
                },
                new[] {
                    new GradientAlphaKey(startAlpha, 0f),
                    new GradientAlphaKey(endAlpha, 1f)
                }
            );
            col.color = grad;
        }

        // ===========================================================================
        // GROUND SCORCH DECALS
        // ===========================================================================

        /// <summary>
        /// Spawn a ground-level scorch mark at an impact site.
        /// Uses a flat quad with scorch texture projected downward.
        /// </summary>
        private void SpawnGroundDecal(Vector3 position)
        {
            if (scorchDecalMat == null) return;

            // Raycast down to find ground position
            Vector3 groundPos = position;
            if (Physics.Raycast(position + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f))
                groundPos = hit.point + Vector3.up * 0.05f; // Slightly above ground

            GameObject decal = GameObject.CreatePrimitive(PrimitiveType.Quad);
            decal.name = "ScorchDecal";

            // Remove collider
            var col = decal.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Position flat on ground
            decal.transform.position = groundPos;
            decal.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
            float scale = Random.Range(3f, 6f);
            decal.transform.localScale = Vector3.one * scale;

            // Apply scorch material
            var renderer = decal.GetComponent<Renderer>();
            renderer.material = scorchDecalMat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Fade out and destroy after 30 seconds
            StartCoroutine(FadeAndDestroyDecal(decal, renderer, 30f));
        }

        private System.Collections.IEnumerator FadeAndDestroyDecal(GameObject decal, Renderer renderer, float lifetime)
        {
            yield return new WaitForSeconds(lifetime - 5f); // Wait most of lifetime

            // Fade out over 5 seconds
            float fadeTimer = 5f;
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            Color baseColor = scorchDecalMat.color;

            while (fadeTimer > 0f)
            {
                fadeTimer -= Time.deltaTime;
                float alpha = Mathf.Clamp01(fadeTimer / 5f) * baseColor.a;
                renderer.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", new Color(baseColor.r, baseColor.g, baseColor.b, alpha));
                mpb.SetColor("_Color", new Color(baseColor.r, baseColor.g, baseColor.b, alpha));
                renderer.SetPropertyBlock(mpb);
                yield return null;
            }

            Destroy(decal);
        }
    }
}
