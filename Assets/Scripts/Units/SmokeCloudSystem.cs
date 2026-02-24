using UnityEngine;
using System.Collections.Generic;
using NapoleonicWars.Core;

namespace NapoleonicWars.Units
{
    /// <summary>
    /// Manages accumulating smoke clouds from musket fire.
    /// Smoke thickens with each shot and dissipates when firing stops.
    /// Uses URP particle shader with procedural smoke texture for volumetric look.
    /// </summary>
    public class SmokeCloudSystem : MonoBehaviour
    {
        public static SmokeCloudSystem Instance { get; private set; }

        [Header("Smoke Settings")]
        [SerializeField] private float maxSmokeDensity = 100f;
        [SerializeField] private float smokeDissipationRate = 5f;
        [SerializeField] private float smokePerShot = 4f;

        // Active smoke clouds per area (grid-based)
        private Dictionary<int, SmokeCloud> smokeClouds = new Dictionary<int, SmokeCloud>();
        private float cellSize = 15f;
        
        // Cached list to avoid GC alloc every frame
        private List<int> cachedRemoveList = new List<int>(32);
        
        // Shared URP material — created once, used by all clouds
        private static Material sharedSmokeMat;

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
            EnsureMaterial();
        }

        private static void EnsureMaterial()
        {
            if (sharedSmokeMat != null) return;

            // MUST use URP particle shader — Standard/Unlit will render as opaque blob
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Standard");

            sharedSmokeMat = new Material(shader);
            sharedSmokeMat.color = new Color(0.8f, 0.78f, 0.72f, 0.35f);

            // Apply procedural smoke texture for volumetric look
            Texture2D smokeTex = VFXTextureGenerator.SmokeTexture;
            if (smokeTex != null)
            {
                sharedSmokeMat.mainTexture = smokeTex;
                sharedSmokeMat.SetTexture("_BaseMap", smokeTex);
            }

            // Transparency setup for URP
            sharedSmokeMat.SetFloat("_Surface", 1); // Transparent
            sharedSmokeMat.SetFloat("_Blend", 0);   // Alpha blend
            sharedSmokeMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            sharedSmokeMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            sharedSmokeMat.SetInt("_ZWrite", 0);
            sharedSmokeMat.SetOverrideTag("RenderType", "Transparent");
            sharedSmokeMat.renderQueue = 3000;
            sharedSmokeMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        private void Update()
        {
            cachedRemoveList.Clear();
            foreach (var kvp in smokeClouds)
            {
                kvp.Value.Dissipate(smokeDissipationRate * Time.deltaTime);
                if (kvp.Value.Density <= 0f)
                    cachedRemoveList.Add(kvp.Key);
            }

            foreach (int key in cachedRemoveList)
            {
                if (smokeClouds.TryGetValue(key, out SmokeCloud cloud))
                {
                    cloud.Cleanup();
                    smokeClouds.Remove(key);
                }
            }
        }

        /// <summary>
        /// Add smoke at a specific position (called when a unit fires)
        /// </summary>
        public void AddSmoke(Vector3 position, float amount)
        {
            EnsureMaterial();
            int cellKey = GetCellKey(position);

            if (!smokeClouds.TryGetValue(cellKey, out SmokeCloud cloud))
            {
                cloud = new SmokeCloud(position, sharedSmokeMat);
                smokeClouds[cellKey] = cloud;
            }

            cloud.AddSmoke(amount * smokePerShot);
        }

        private int GetCellKey(Vector3 position)
        {
            int x = Mathf.FloorToInt(position.x / cellSize);
            int z = Mathf.FloorToInt(position.z / cellSize);
            return x * 73856093 ^ z * 19349663;
        }

        /// <summary>
        /// Gets smoke density at a position (0-1 range)
        /// </summary>
        public float GetSmokeDensity(Vector3 position)
        {
            int cellKey = GetCellKey(position);
            if (smokeClouds.TryGetValue(cellKey, out SmokeCloud cloud))
                return Mathf.Clamp01(cloud.Density / maxSmokeDensity);
            return 0f;
        }

        /// <summary>
        /// Persistent smoke cloud — accumulates with fire, dissipates over time.
        /// Uses URP particle system with volumetric smoke texture.
        /// </summary>
        private class SmokeCloud
        {
            public float Density { get; private set; }
            private Vector3 position;
            private GameObject visualObject;
            private ParticleSystem particles;
            private float timeSinceLastAdd;

            public SmokeCloud(Vector3 pos, Material mat)
            {
                position = pos;
                Density = 0f;
                timeSinceLastAdd = 0f;
                CreateSmokeSystem(mat);
            }

            private void CreateSmokeSystem(Material mat)
            {
                visualObject = new GameObject("SmokeCloud");
                visualObject.transform.position = position + Vector3.up * 1.5f;

                particles = visualObject.AddComponent<ParticleSystem>();
                
                var psRenderer = visualObject.GetComponent<ParticleSystemRenderer>();
                if (psRenderer == null)
                    psRenderer = visualObject.AddComponent<ParticleSystemRenderer>();
                psRenderer.sharedMaterial = mat;
                psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                psRenderer.sortingOrder = 1;

                var main = particles.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 12f);
                main.startSize = new ParticleSystem.MinMaxCurve(2f, 5f); // LARGE visible puffs
                main.startColor = new Color(0.8f, 0.78f, 0.72f, 0.3f);
                main.maxParticles = 200;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = -0.04f; // Slowly rises
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.8f);

                var emission = particles.emission;
                emission.rateOverTime = 0f;

                var shape = particles.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 3f; // Wide spread
                shape.radiusThickness = 0.7f;

                // Size growth — smoke expands as it ages
                var sol = particles.sizeOverLifetime;
                sol.enabled = true;
                var sizeCurve = new AnimationCurve(
                    new Keyframe(0f, 0.3f),
                    new Keyframe(0.3f, 0.8f),
                    new Keyframe(0.6f, 1.2f),
                    new Keyframe(1f, 1.8f)
                );
                sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

                // Color gradient: white-gray → gray → transparent
                var col = particles.colorOverLifetime;
                col.enabled = true;
                Gradient grad = new Gradient();
                grad.SetKeys(
                    new[] {
                        new GradientColorKey(new Color(0.85f, 0.83f, 0.78f), 0f),
                        new GradientColorKey(new Color(0.7f, 0.68f, 0.62f), 0.3f),
                        new GradientColorKey(new Color(0.55f, 0.52f, 0.48f), 0.7f),
                        new GradientColorKey(new Color(0.4f, 0.38f, 0.35f), 1f)
                    },
                    new[] {
                        new GradientAlphaKey(0f, 0f),
                        new GradientAlphaKey(0.35f, 0.1f),
                        new GradientAlphaKey(0.25f, 0.5f),
                        new GradientAlphaKey(0f, 1f)
                    }
                );
                col.color = grad;

                // Turbulence noise
                var noise = particles.noise;
                noise.enabled = true;
                noise.strength = 0.6f;
                noise.frequency = 0.2f;
                noise.scrollSpeed = 0.15f;
                noise.octaveCount = 2;

                // Slow rotation for natural look
                var rot = particles.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
            }

            public void AddSmoke(float amount)
            {
                Density = Mathf.Min(Density + amount, 100f);
                timeSinceLastAdd = 0f;

                if (particles != null)
                {
                    // Emit burst proportional to amount
                    int burstCount = Mathf.Clamp(Mathf.RoundToInt(amount * 1.5f), 3, 15);
                    particles.Emit(burstCount);
                }
            }

            public void Dissipate(float amount)
            {
                timeSinceLastAdd += Time.deltaTime;
                
                float dissipationMult = 1f;
                if (timeSinceLastAdd > 3f)
                    dissipationMult = 2f;
                
                Density = Mathf.Max(Density - amount * dissipationMult, 0f);
            }

            public void Cleanup()
            {
                if (visualObject != null)
                {
                    if (particles != null)
                    {
                        particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                        Object.Destroy(visualObject, 12f); // Let existing particles fully fade
                    }
                    else
                    {
                        Object.Destroy(visualObject);
                    }
                }
            }
        }
    }
}
