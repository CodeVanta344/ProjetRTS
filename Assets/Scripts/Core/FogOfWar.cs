using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Units;

namespace NapoleonicWars.Core
{
    public class FogOfWar : MonoBehaviour
    {
        public static FogOfWar Instance { get; private set; }

        [Header("Fog Settings")]
        [SerializeField] private int resolution = 64; // Reduced from 128 — 4× fewer tiles to process
        [SerializeField] private float worldSize = 250f;
        [SerializeField] private float unitVisionRange = 50f;
        [SerializeField] private float cavalryVisionRange = 70f;
        [SerializeField] private float artilleryVisionRange = 40f;
        [SerializeField] private float updateInterval = 1f; // Slowed from 0.5s — fog changes slowly

        private float[,] visibilityMap;
        private float[,] exploredMap;
        private Texture2D fogTexture;
        private float updateTimer;
        
        // Cached pixel array to avoid GC alloc every frame
        private Color[] cachedPixels;
        
        // Dirty flag — only upload texture to GPU when visibility actually changed
        private bool fogDirty;
        
        // Cached inverse for grid conversion
        private float invWorldSize;

        public Texture2D FogTexture => fogTexture;
        public int Resolution => resolution;
        public float WorldSize => worldSize;

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
            visibilityMap = new float[resolution, resolution];
            exploredMap = new float[resolution, resolution];
            invWorldSize = 1f / worldSize;

            fogTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            fogTexture.filterMode = FilterMode.Bilinear;
            fogTexture.wrapMode = TextureWrapMode.Clamp;
            
            // Pre-allocate pixel array to avoid GC alloc every frame
            cachedPixels = new Color[resolution * resolution];

            ClearFog();
        }

        private void Update()
        {
            updateTimer -= Time.deltaTime;
            if (updateTimer <= 0f)
            {
                updateTimer = updateInterval;
                UpdateVisibility();
                fogDirty = true;
            }

            // Only upload to GPU when visibility changed
            if (fogDirty)
            {
                fogDirty = false;
                ApplyToTexture();
            }
        }

        private void UpdateVisibility()
        {
            // Guard against initialization order issues
            if (visibilityMap == null || exploredMap == null) return;
            
            // Clear visibility — use System.Array.Clear instead of nested loop
            System.Array.Clear(visibilityMap, 0, visibilityMap.Length);

            // === KEY OPTIMIZATION: Use REGIMENT positions, not individual units ===
            // Before: 500 units × 2809 tiles = 1.4M iterations every 0.5s
            // After:  ~8 regiments × 700 tiles = 5600 iterations every 1s
            if (BattleManager.Instance == null) return;

            var playerRegiments = BattleManager.Instance.PlayerRegiments;
            if (playerRegiments == null) return;

            float weatherVision = WeatherSystem.Instance != null ? WeatherSystem.Instance.VisibilityRange : 999f;

            foreach (var reg in playerRegiments)
            {
                if (reg == null || reg.CachedAliveCount <= 0) continue;

                float visionRange = unitVisionRange;
                if (reg.UnitData != null)
                {
                    switch (reg.UnitData.unitType)
                    {
                        case NapoleonicWars.Data.UnitType.Cavalry:
                        case NapoleonicWars.Data.UnitType.Hussar:
                        case NapoleonicWars.Data.UnitType.Lancer:
                            visionRange = cavalryVisionRange;
                            break;
                        case NapoleonicWars.Data.UnitType.Artillery:
                            visionRange = artilleryVisionRange;
                            break;
                    }
                }

                visionRange = Mathf.Min(visionRange, weatherVision);

                // Use regiment center position — all units in a regiment are close together
                // so their vision circles overlap almost completely
                RevealArea(reg.transform.position, visionRange);
            }
        }

        private void RevealArea(Vector3 worldPos, float radius)
        {
            int cx = WorldToGrid(worldPos.x);
            int cy = WorldToGrid(worldPos.z);
            int gridRadius = Mathf.CeilToInt(radius * invWorldSize * resolution);
            float gridRadiusSqr = gridRadius * gridRadius;

            // Clamp loop bounds to avoid per-iteration bounds check
            int xMin = Mathf.Max(cx - gridRadius, 0);
            int xMax = Mathf.Min(cx + gridRadius, resolution - 1);
            int yMin = Mathf.Max(cy - gridRadius, 0);
            int yMax = Mathf.Min(cy + gridRadius, resolution - 1);

            for (int x = xMin; x <= xMax; x++)
            {
                float dxf = x - cx;
                float dxSqr = dxf * dxf;
                for (int y = yMin; y <= yMax; y++)
                {
                    float dyf = y - cy;
                    float distSqr = dxSqr + dyf * dyf;

                    if (distSqr <= gridRadiusSqr)
                    {
                        float dist = distSqr / gridRadiusSqr; // 0..1 squared
                        float falloff = 1f - Mathf.Sqrt(dist);
                        falloff = falloff * falloff;

                        if (falloff > visibilityMap[x, y])
                            visibilityMap[x, y] = falloff;
                        exploredMap[x, y] = 1f;
                    }
                }
            }
        }

        private void ApplyToTexture()
        {
            // Guard against initialization order issues
            if (visibilityMap == null || exploredMap == null || fogTexture == null) return;
            
            // Use cached pixel array
            if (cachedPixels == null) cachedPixels = new Color[resolution * resolution];

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float vis = visibilityMap[x, y];
                    float explored = exploredMap[x, y];

                    float alpha;
                    if (vis > 0.1f)
                        alpha = 0f; // Fully visible
                    else if (explored > 0.5f)
                        alpha = 0.5f; // Explored but not visible (grey fog)
                    else
                        alpha = 0.9f; // Unexplored (dark fog)

                    cachedPixels[y * resolution + x] = new Color(0f, 0f, 0f, alpha);
                }
            }

            fogTexture.SetPixels(cachedPixels);
            fogTexture.Apply();
        }

        private void ClearFog()
        {
            if (cachedPixels == null) cachedPixels = new Color[resolution * resolution];
            for (int i = 0; i < cachedPixels.Length; i++)
                cachedPixels[i] = new Color(0f, 0f, 0f, 0.9f);
            fogTexture.SetPixels(cachedPixels);
            fogTexture.Apply();
        }

        private int WorldToGrid(float worldCoord)
        {
            float normalized = (worldCoord + worldSize * 0.5f) * invWorldSize;
            return Mathf.Clamp(Mathf.FloorToInt(normalized * resolution), 0, resolution - 1);
        }

        public bool IsVisible(Vector3 worldPos)
        {
            int gx = WorldToGrid(worldPos.x);
            int gy = WorldToGrid(worldPos.z);
            if (gx < 0 || gx >= resolution || gy < 0 || gy >= resolution) return false;
            return visibilityMap[gx, gy] > 0.1f;
        }

        public bool IsExplored(Vector3 worldPos)
        {
            int gx = WorldToGrid(worldPos.x);
            int gy = WorldToGrid(worldPos.z);
            if (gx < 0 || gx >= resolution || gy < 0 || gy >= resolution) return false;
            return exploredMap[gx, gy] > 0.5f;
        }
    }
}
