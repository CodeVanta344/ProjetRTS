using UnityEngine;
using NapoleonicWars.Core.TerrainGeneration;
using NapoleonicWars.Battlefield;

namespace NapoleonicWars.Core
{
    public class TerrainGenerator : MonoBehaviour
    {
        public static TerrainGenerator Instance { get; private set; }

        [Header("Terrain Dimensions")]
        [SerializeField] private float terrainWidth = 1500f; // Tripled
        [SerializeField] private float terrainLength = 1500f; // Tripled
        [SerializeField] private float terrainHeight = 50f; // Increased for larger scale
        [SerializeField] private int heightmapResolution = 1025; // Increased resolution
        [SerializeField] private int alphamapResolution = 1024;
        
        [Header("Generation Settings")]
        [SerializeField] private TerrainGenerationSettings settings = new TerrainGenerationSettings();

        private Terrain terrain;
        private TerrainData terrainData;
        private float[,] erosionMap; // Pour le texturage (flow)

        // Biome profile (set by BattleTerrainConfigurator before generation)
        private BattleTerrainProfile activeProfile;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>Apply a biome profile to override generation settings. Call before GenerateBattlefieldTerrain.</summary>
        public void ApplyProfile(BattleTerrainProfile profile)
        {
            if (profile == null) return;
            activeProfile = profile;

            // Override heightmap settings
            settings.terrainScale = profile.terrainScale;
            settings.octaves = profile.octaves;
            settings.persistence = profile.persistence;
            settings.lacunarity = profile.lacunarity;
            settings.battleAreaRadius = profile.battleAreaRadius;
            settings.flattenCenterForBattle = profile.flattenCenter;

            // Override vegetation
            settings.treeCount = profile.treeCount;
            settings.forestDensity = profile.forestDensity;

            // Override terrain height
            terrainHeight = profile.terrainHeightMax;

            Debug.Log($"[TerrainGenerator] Profile applied: {profile.displayName} " +
                      $"(height={profile.terrainHeightMax}, trees={profile.treeCount})");
        }

        public Terrain GenerateBattlefieldTerrain()
        {
            if (settings.useRandomSeed)
                settings.seed = Random.Range(0, 1000000);

            terrainData = new TerrainData();
            terrainData.heightmapResolution = heightmapResolution;
            terrainData.alphamapResolution = alphamapResolution;
            terrainData.size = new Vector3(terrainWidth, terrainHeight, terrainLength);
            terrainData.baseMapResolution = 512;

            // 1. Heightmap with erosion
            float[,] heights = GenerateBaseHeightmap();

            if (settings.applyErosion)
            {
                float[,] originalHeights = (float[,])heights.Clone();
                HydraulicErosion.ErosionParams ep = HydraulicErosion.ErosionParams.Default;
                ep.iterations = settings.erosionIterations;
                ep.erosionRate = settings.erosionRate;
                ep.depositionRate = settings.depositionRate;
                heights = HydraulicErosion.Erode(heights, heightmapResolution, settings.seed, ep);

                erosionMap = new float[heightmapResolution, heightmapResolution];
                for (int z = 0; z < heightmapResolution; z++)
                    for (int x = 0; x < heightmapResolution; x++)
                        erosionMap[z, x] = Mathf.Max(0, originalHeights[z, x] - heights[z, x]);
            }

            terrainData.SetHeights(0, 0, heights);

            // ---------------------------------------------------------------
            // 2. FOUR TERRAIN LAYERS — Real PBR textures (Poly Haven CC0)
            //    Falls back to procedural color if textures not found
            // ---------------------------------------------------------------
            Color meadowCol = activeProfile != null ? activeProfile.primaryGroundColor : new Color(0.10f, 0.40f, 0.08f);
            Color dryCol    = activeProfile != null ? activeProfile.secondaryGroundColor : new Color(0.25f, 0.38f, 0.14f);
            Color dirtCol   = activeProfile != null ? activeProfile.dirtColor : new Color(0.32f, 0.25f, 0.16f);
            Color rockCol   = activeProfile != null ? activeProfile.rockColor : new Color(0.38f, 0.36f, 0.32f);

            TerrainLayer[] layers = new TerrainLayer[4];

            // Layer 0 — Lush green meadow (Poly Haven: forest_ground_04)
            layers[0] = new TerrainLayer();
            Texture2D grassTex = Resources.Load<Texture2D>("Textures/Terrain/Grass_Color");
            Texture2D grassNorm = Resources.Load<Texture2D>("Textures/Terrain/Grass_Normal");
            layers[0].diffuseTexture = grassTex != null ? grassTex : CreateDetailedColorTexture(meadowCol, 128, 0.10f);
            if (grassNorm != null) layers[0].normalMapTexture = grassNorm;
            layers[0].tileSize = new Vector2(20f, 20f); // Smaller tiles = more detail
            layers[0].normalScale = 0.6f;

            // Layer 1 — Dry/lighter grass (tinted grass or procedural)
            layers[1] = new TerrainLayer();
            Texture2D dryTex = Resources.Load<Texture2D>("Textures/Terrain/DryGrass_Color");
            Texture2D dryNorm = Resources.Load<Texture2D>("Textures/Terrain/DryGrass_Normal");
            layers[1].diffuseTexture = dryTex != null ? dryTex : CreateDetailedColorTexture(dryCol, 128, 0.08f);
            if (dryNorm != null) layers[1].normalMapTexture = dryNorm;
            else if (grassNorm != null) layers[1].normalMapTexture = grassNorm; // Reuse grass normal
            layers[1].tileSize = new Vector2(18f, 18f);
            layers[1].normalScale = 0.5f;

            // Layer 2 — Dirt paths (Poly Haven: brown_mud_03)
            layers[2] = new TerrainLayer();
            Texture2D dirtTex = Resources.Load<Texture2D>("Textures/Terrain/Dirt_Color");
            Texture2D dirtNorm = Resources.Load<Texture2D>("Textures/Terrain/Dirt_Normal");
            layers[2].diffuseTexture = dirtTex != null ? dirtTex : CreateDetailedColorTexture(dirtCol, 128, 0.06f);
            if (dirtNorm != null) layers[2].normalMapTexture = dirtNorm;
            layers[2].tileSize = new Vector2(15f, 15f);
            layers[2].normalScale = 0.8f;

            // Layer 3 — Rock/cliff faces (procedural fallback)
            layers[3] = new TerrainLayer();
            Texture2D rockTex = Resources.Load<Texture2D>("Textures/Terrain/Rock_Color");
            Texture2D rockNorm = Resources.Load<Texture2D>("Textures/Terrain/Rock_Normal");
            layers[3].diffuseTexture = rockTex != null ? rockTex : CreateDetailedColorTexture(rockCol, 128, 0.05f);
            if (rockNorm != null) layers[3].normalMapTexture = rockNorm;
            else if (dirtNorm != null) layers[3].normalMapTexture = dirtNorm; // Reuse dirt normal
            layers[3].tileSize = new Vector2(12f, 12f);
            layers[3].normalScale = 1.0f;

            terrainData.terrainLayers = layers;

            int realTexCount = (grassTex != null ? 1 : 0) + (dryTex != null ? 1 : 0) + (dirtTex != null ? 1 : 0) + (rockTex != null ? 1 : 0);
            Debug.Log($"[TerrainGenerator] Terrain layers: {realTexCount}/4 real PBR textures loaded");

            // ---------------------------------------------------------------
            // 3. DETAILED SPLATMAP — Paths, biome noise, slope, erosion
            // ---------------------------------------------------------------
            PaintDetailedSplatmap();

            // ---------------------------------------------------------------
            // 4. BUILD TERRAIN GAMEOBJECT
            // ---------------------------------------------------------------
            GameObject terrainGO = Terrain.CreateTerrainGameObject(terrainData);
            terrainGO.name = "BattlefieldTerrain";
            terrainGO.transform.position = new Vector3(-terrainWidth / 2f, 0f, -terrainLength / 2f);

            terrain = terrainGO.GetComponent<Terrain>();

            // Quality settings — grass must be visible at distance on large maps
            terrain.heightmapPixelError = 5f;
            terrain.basemapDistance = 800f;
            terrain.detailObjectDistance = 300f;  // Grass visible at 300m
            terrain.detailObjectDensity = 1.0f;   // Full density
            terrain.treeDistance = 350f;
            terrain.treeBillboardDistance = 150f;
            terrain.treeCrossFadeLength = 30f;
            terrain.drawInstanced = true;

            Debug.Log($"[TerrainGenerator] Terrain built: {terrainWidth}x{terrainLength}, layers={layers.Length}");

            // ---------------------------------------------------------------
            // 5. VEGETATION (trees as GameObjects + grass detail)
            // ---------------------------------------------------------------
            AddAdvancedVegetation();
            AddGrassDetails();
            AddGroundScatter();

            return terrain;
        }

        // ===========================================================================
        // TEXTURE GENERATION
        // ===========================================================================

        /// <summary>
        /// Create a color texture with organic Perlin noise variation.
        /// Uses multiple octaves for a natural, detailed look.
        /// </summary>
        private Texture2D CreateDetailedColorTexture(Color baseColor, int size, float variance)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            float ox = Random.Range(0f, 1000f);
            float oy = Random.Range(0f, 1000f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;

                    // Large patches
                    float n1 = Mathf.PerlinNoise(u * 6f + ox, v * 6f + oy) - 0.5f;
                    // Medium clumps
                    float n2 = Mathf.PerlinNoise(u * 14f + ox + 50f, v * 14f + oy + 50f) - 0.5f;
                    // Fine blade-level
                    float n3 = Mathf.PerlinNoise(u * 30f + ox + 100f, v * 30f + oy + 100f) - 0.5f;
                    // Tiny speckle
                    float n4 = Mathf.PerlinNoise(u * 60f + ox + 200f, v * 60f + oy + 200f) - 0.5f;

                    float combined = n1 * 0.5f + n2 * 0.3f + n3 * 0.15f + n4 * 0.05f;

                    float r = baseColor.r + combined * variance * 1.2f;
                    float g = baseColor.g + combined * variance * 1.6f; // More variation in green
                    float b = baseColor.b + combined * variance * 0.8f;

                    // Add occasional dark spots (roots, shadows)
                    float dark = Mathf.PerlinNoise(u * 20f + ox + 300f, v * 20f + oy + 300f);
                    if (dark > 0.7f)
                    {
                        float darken = (dark - 0.7f) * 2f;
                        r -= darken * 0.04f;
                        g -= darken * 0.06f;
                        b -= darken * 0.03f;
                    }

                    // Add bright highlights
                    float bright = Mathf.PerlinNoise(u * 18f + ox + 400f, v * 18f + oy + 400f);
                    if (bright > 0.75f)
                    {
                        float lighten = (bright - 0.75f) * 2f;
                        r += lighten * 0.03f;
                        g += lighten * 0.05f;
                        b += lighten * 0.02f;
                    }

                    tex.SetPixel(x, y, new Color(
                        Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), 1f
                    ));
                }
            }

            tex.Apply(true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = 4;
            return tex;
        }

        // ===========================================================================
        // DETAILED SPLATMAP PAINTING
        // ===========================================================================

        /// <summary>
        /// Paint terrain with 4 layers:
        ///   [0] Lush meadow grass (dominant)
        ///   [1] Dry/light grass patches (biome noise)
        ///   [2] Dirt paths + trampled center
        ///   [3] Rock on steep slopes
        /// Includes procedural winding paths for atmosphere.
        /// </summary>
        private void PaintDetailedSplatmap()
        {
            int res = terrainData.alphamapResolution;
            float[,,] splatmap = new float[res, res, 4];
            float[,] heights = terrainData.GetHeights(0, 0, heightmapResolution, heightmapResolution);

            float seed = settings.seed * 0.01f;
            float battleRadius = activeProfile != null ? activeProfile.battleAreaRadius : 0.25f;

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float nx = (float)x / res;
                    float nz = (float)z / res;

                    // ---- Height & Slope ----
                    int hx = Mathf.Clamp(Mathf.FloorToInt(nx * (heightmapResolution - 1)), 1, heightmapResolution - 2);
                    int hz = Mathf.Clamp(Mathf.FloorToInt(nz * (heightmapResolution - 1)), 1, heightmapResolution - 2);
                    float h = heights[hz, hx];
                    float hL = heights[hz, hx - 1]; float hR = heights[hz, hx + 1];
                    float hU = heights[hz + 1, hx]; float hD = heights[hz - 1, hx];
                    float slope = (Mathf.Abs(hR - hL) + Mathf.Abs(hU - hD)) * heightmapResolution * 0.5f;

                    // ---- Start with pure lush grass ----
                    float grass = 1.0f;
                    float dryGrass = 0f;
                    float dirt = 0f;
                    float rock = 0f;

                    // ---- ROCK on steep slopes ----
                    if (slope > 0.3f)
                    {
                        rock = Mathf.SmoothStep(0f, 1f, (slope - 0.3f) * 4f);
                        grass -= rock;
                    }

                    // ---- DIRT on moderate slopes ----
                    if (slope > 0.15f && slope <= 0.5f)
                    {
                        float dirtBlend = Mathf.SmoothStep(0f, 0.4f, (slope - 0.15f) * 3f);
                        dirt += dirtBlend;
                        grass -= dirtBlend;
                    }

                    // ---- DRY GRASS patches (biome noise) ----
                    float biomeNoise = Mathf.PerlinNoise(nx * 5f + seed, nz * 5f + seed * 1.3f);
                    float biomeNoise2 = Mathf.PerlinNoise(nx * 12f + seed + 50f, nz * 12f + seed + 50f);
                    float dryFactor = Mathf.SmoothStep(0f, 0.5f, (biomeNoise - 0.55f) * 3f) * 0.4f;
                    dryFactor += Mathf.SmoothStep(0f, 0.3f, (biomeNoise2 - 0.6f) * 3f) * 0.2f;
                    if (dryFactor > 0f && rock < 0.5f)
                    {
                        dryGrass += dryFactor;
                        grass -= dryFactor;
                    }

                    // ---- PATHS (winding dirt paths across the battlefield) ----
                    // Path 1: diagonal road
                    float pathSine1 = Mathf.Sin(nz * 12f + seed) * 0.04f + Mathf.Sin(nz * 5f + seed * 2f) * 0.08f;
                    float pathDist1 = Mathf.Abs(nx - 0.5f - pathSine1);
                    float path1 = 1f - Mathf.SmoothStep(0f, 1f, pathDist1 / 0.015f);

                    // Path 2: perpendicular road
                    float pathSine2 = Mathf.Sin(nx * 10f + seed * 3f) * 0.05f + Mathf.Sin(nx * 4f + seed) * 0.06f;
                    float pathDist2 = Mathf.Abs(nz - 0.5f - pathSine2);
                    float path2 = 1f - Mathf.SmoothStep(0f, 1f, pathDist2 / 0.012f);

                    // Path 3: diagonal supply route
                    float pathNoise3 = Mathf.PerlinNoise(nx * 8f + seed * 5f, nz * 8f + seed * 4f) * 0.05f;
                    float pathLine3 = Mathf.Abs((nx + nz) * 0.5f - 0.5f + pathNoise3);
                    float path3 = 1f - Mathf.SmoothStep(0f, 1f, pathLine3 / 0.01f);

                    float pathTotal = Mathf.Clamp01(path1 + path2 + path3 * 0.6f);
                    if (pathTotal > 0f)
                    {
                        // Paths blend in dirt, reduce grass
                        float pathDirt = pathTotal * 0.8f;
                        dirt += pathDirt;
                        grass -= pathDirt * 0.7f;
                        dryGrass -= pathDirt * 0.3f;
                    }

                    // ---- BATTLE CENTER (trampled zone with more dirt) ----
                    float distToCenter = Vector2.Distance(new Vector2(nx, nz), new Vector2(0.5f, 0.5f));
                    if (distToCenter < battleRadius)
                    {
                        float centerFade = 1f - Mathf.SmoothStep(0f, 1f, distToCenter / battleRadius);
                        float trampleNoise = Mathf.PerlinNoise(nx * 20f + seed, nz * 20f + seed) * 0.5f + 0.5f;
                        float trample = centerFade * 0.35f * trampleNoise; // Light trampling
                        dirt += trample;
                        grass -= trample * 0.6f;
                        dryGrass += trample * 0.2f;
                    }

                    // ---- EROSION GULLIES (if erosion data exists) ----
                    if (erosionMap != null)
                    {
                        float ero = erosionMap[hz, hx];
                        if (ero > 0.001f)
                        {
                            float eroFactor = Mathf.SmoothStep(0f, 0.6f, ero * 200f);
                            dirt += eroFactor * 0.5f;
                            grass -= eroFactor * 0.5f;
                        }
                    }

                    // ---- NORMALIZE ----
                    grass = Mathf.Max(0f, grass);
                    dryGrass = Mathf.Max(0f, dryGrass);
                    dirt = Mathf.Max(0f, dirt);
                    rock = Mathf.Max(0f, rock);

                    float total = grass + dryGrass + dirt + rock;
                    if (total > 0f)
                    {
                        splatmap[z, x, 0] = grass / total;
                        splatmap[z, x, 1] = dryGrass / total;
                        splatmap[z, x, 2] = dirt / total;
                        splatmap[z, x, 3] = rock / total;
                    }
                    else
                    {
                        splatmap[z, x, 0] = 1f;
                    }
                }
            }

            terrainData.SetAlphamaps(0, 0, splatmap);
            Debug.Log("[TerrainGenerator] Splatmap painted: 4 layers (meadow, dry grass, dirt paths, rock)");
        }

        // ===========================================================================
        // HEIGHTMAP (Noise + Shaping)
        // ===========================================================================

        private float[,] GenerateBaseHeightmap()
        {
            FractalNoise.NoiseParams noiseParams = new FractalNoise.NoiseParams
            {
                scale = settings.terrainScale,
                octaves = settings.octaves,
                persistence = settings.persistence,
                lacunarity = settings.lacunarity,
                offset = new Vector2(settings.seed, settings.seed),
                heightCurve = settings.heightCurve
            };

            float[,] heights = FractalNoise.GenerateNoiseMap(heightmapResolution, heightmapResolution, settings.seed, noiseParams);

            // ============================================================
            // MEDIUM-SCALE UNDULATION — gentle rolling hills everywhere
            // Even the battle zone gets soft mounds for visual interest
            // ============================================================
            float undulationSeed = settings.seed * 1.37f;
            for (int z = 0; z < heightmapResolution; z++)
            {
                for (int x = 0; x < heightmapResolution; x++)
                {
                    float nx = (float)x / heightmapResolution;
                    float nz = (float)z / heightmapResolution;

                    // Large gentle mounds (200-300m wavelength on 1500m map)
                    float mound = Mathf.PerlinNoise(nx * 5f + undulationSeed, nz * 5f + undulationSeed) * 0.04f;
                    // Medium bumps (80-100m wavelength)
                    float bump = Mathf.PerlinNoise(nx * 12f + undulationSeed + 100f, nz * 12f + undulationSeed + 100f) * 0.015f;
                    // Small micro-terrain (30-50m wavelength)
                    float micro = Mathf.PerlinNoise(nx * 30f + undulationSeed + 200f, nz * 30f + undulationSeed + 200f) * 0.005f;

                    heights[z, x] += mound + bump + micro;
                }
            }

            // ============================================================
            // MACRO SHAPING — flatten center for battle, but keep undulation
            // ============================================================
            if (settings.flattenCenterForBattle)
            {
                for (int z = 0; z < heightmapResolution; z++)
                {
                    for (int x = 0; x < heightmapResolution; x++)
                    {
                        float nx = (float)x / heightmapResolution;
                        float nz = (float)z / heightmapResolution;
                        
                        float distToCenter = Vector2.Distance(new Vector2(nx, nz), new Vector2(0.5f, 0.5f));
                        
                        if (distToCenter < settings.battleAreaRadius)
                        {
                            // Softer flattening — keep some undulation
                            float blend = 1f - (distToCenter / settings.battleAreaRadius);
                            blend = Mathf.SmoothStep(0, 1, blend);
                            
                            // Keep base height + undulation, just reduce vertical extremes
                            float baseH = 0.08f;
                            // Preserve 30% of original height variation in battle zone
                            heights[z, x] = Mathf.Lerp(heights[z, x], baseH, blend * 0.6f);
                        }
                        else
                        {
                            // Outside battle: boost height variation for visible hills
                            float hillBoost = Mathf.SmoothStep(0f, 1f, (distToCenter - settings.battleAreaRadius) / (0.5f - settings.battleAreaRadius));
                            heights[z, x] *= (1f + hillBoost * 0.5f);
                        }
                        
                        // Edge falloff
                        float edgeDistX = Mathf.Min(nx, 1f - nx);
                        float edgeDistZ = Mathf.Min(nz, 1f - nz);
                        float edgeDist = Mathf.Min(edgeDistX, edgeDistZ);
                        
                        if (edgeDist < 0.1f)
                        {
                            float falloff = edgeDist / 0.1f;
                            heights[z, x] *= falloff;
                        }
                    }
                }
            }

            return heights;
        }

        // ===========================================================================
        // TERRAIN LAYERS
        // ===========================================================================

        private TerrainLayer[] CreateTerrainLayers()
        {
            TerrainLayer[] layers = new TerrainLayer[5];

            // Layer 0: Lush green meadow grass
            layers[0] = new TerrainLayer();
            layers[0].diffuseTexture = GenerateMeadowGrassTexture();
            layers[0].normalMapTexture = GenerateNormalMap(layers[0].diffuseTexture, 1.5f);
            layers[0].tileSize = new Vector2(45f, 45f);
            layers[0].normalScale = 0.5f;

            // Layer 1: Dry/trampled grass with dirt patches
            layers[1] = new TerrainLayer();
            layers[1].diffuseTexture = GenerateDryGrassTexture();
            layers[1].normalMapTexture = GenerateNormalMap(layers[1].diffuseTexture, 2f);
            layers[1].tileSize = new Vector2(35f, 35f);
            layers[1].normalScale = 0.6f;

            // Layer 2: Packed earth / dirt road
            layers[2] = new TerrainLayer();
            layers[2].diffuseTexture = GenerateDirtTexture();
            layers[2].normalMapTexture = GenerateNormalMap(layers[2].diffuseTexture, 2.5f);
            layers[2].tileSize = new Vector2(30f, 30f);
            layers[2].normalScale = 0.8f;

            // Layer 3: Rocky/stony ground (hilltops)
            layers[3] = new TerrainLayer();
            layers[3].diffuseTexture = GenerateRockTexture();
            layers[3].normalMapTexture = GenerateNormalMap(layers[3].diffuseTexture, 3f);
            layers[3].tileSize = new Vector2(25f, 25f);
            layers[3].normalScale = 1f;

            // Layer 4: Wet mud (erosion gullies)
            layers[4] = new TerrainLayer();
            layers[4].diffuseTexture = GenerateMudTexture();
            layers[4].normalMapTexture = GenerateNormalMap(layers[4].diffuseTexture, 3.5f);
            layers[4].tileSize = new Vector2(20f, 20f);
            layers[4].normalScale = 1.2f;
            layers[4].smoothness = 0.6f; // Plus brillant car humide

            return layers;
        }

        // ----- MEADOW GRASS: Vibrant green with rich blade detail and color variation -----
        private Texture2D GenerateMeadowGrassTexture()
        {
            int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            float ox = Random.Range(0f, 1000f);
            float oy = Random.Range(0f, 1000f);

            // Rich emerald green — lush European meadow
            Color baseCol = activeProfile != null ? activeProfile.primaryGroundColor : new Color(0.12f, 0.42f, 0.10f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;

                    float baseR = baseCol.r;
                    float baseG = baseCol.g;
                    float baseB = baseCol.b;

                    // Large patches — field-scale color variation (lush vs drier zones)
                    float largePatch = Mathf.PerlinNoise(u * 3f + ox, v * 3f + oy);
                    baseR += (largePatch - 0.5f) * 0.08f;
                    baseG += (largePatch - 0.5f) * 0.14f;
                    baseB += (largePatch - 0.5f) * 0.05f;

                    // Medium clumps: darker green patches for depth
                    float medClump = Mathf.PerlinNoise(u * 9f + ox + 100f, v * 9f + oy + 100f);
                    if (medClump > 0.55f)
                    {
                        float blend = (medClump - 0.55f) * 2f;
                        baseG += blend * 0.08f;
                        baseR -= blend * 0.03f;
                    }
                    else if (medClump < 0.3f)
                    {
                        // Slightly darker/earthier spots
                        float blend = (0.3f - medClump) * 2f;
                        baseG -= blend * 0.05f;
                        baseR += blend * 0.02f;
                    }

                    // Grass blade stripes (strong visible vertical pattern)
                    float bladeNoise = Mathf.PerlinNoise(u * 18f + ox, v * 6f + oy);
                    float blade = Mathf.Sin((u * size * 3f) + bladeNoise * 4f);
                    blade = blade * 0.5f + 0.5f; // 0 to 1
                    float bladeVar = blade * 0.06f;
                    baseG += bladeVar;
                    baseR -= bladeVar * 0.35f;
                    baseB -= bladeVar * 0.15f;

                    // Fine noise for micro-texture
                    float fine = Mathf.PerlinNoise(u * 50f + ox * 2f, v * 50f + oy * 2f);
                    baseR += (fine - 0.5f) * 0.035f;
                    baseG += (fine - 0.5f) * 0.05f;
                    baseB += (fine - 0.5f) * 0.02f;

                    // Tiny dark soil spots showing through grass
                    float spot = Mathf.PerlinNoise(u * 65f + ox * 3f, v * 65f + oy * 3f);
                    if (spot > 0.72f)
                    {
                        float darken = (spot - 0.72f) * 2.5f;
                        baseR -= darken * 0.06f;
                        baseG -= darken * 0.10f;
                        baseB -= darken * 0.04f;
                    }

                    // Occasional lighter yellow-green highlight (sun catch)
                    float highlight = Mathf.PerlinNoise(u * 30f + ox * 5f, v * 30f + oy * 5f);
                    if (highlight > 0.80f)
                    {
                        float blend = (highlight - 0.80f) * 3.5f;
                        baseR = Mathf.Lerp(baseR, 0.40f, blend * 0.2f);
                        baseG = Mathf.Lerp(baseG, 0.52f, blend * 0.2f);
                        baseB = Mathf.Lerp(baseB, 0.20f, blend * 0.2f);
                    }

                    // Wildflower specks (rare)
                    float flower = Mathf.PerlinNoise(u * 80f + ox * 7f, v * 80f + oy * 7f);
                    if (flower > 0.92f)
                    {
                        float blend = (flower - 0.92f) * 8f;
                        baseR = Mathf.Lerp(baseR, 0.55f, blend * 0.3f);
                        baseG = Mathf.Lerp(baseG, 0.50f, blend * 0.2f);
                        baseB = Mathf.Lerp(baseB, 0.15f, blend * 0.3f);
                    }

                    tex.SetPixel(x, y, new Color(
                        Mathf.Clamp01(baseR),
                        Mathf.Clamp01(baseG),
                        Mathf.Clamp01(baseB),
                        1f));
                }
            }

            tex.Apply(true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = 8;
            return tex;
        }

        // ----- DRY GRASS: Brown/golden with persistent green undertones -----
        private Texture2D GenerateDryGrassTexture()
        {
            int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            float ox = Random.Range(0f, 1000f);
            float oy = Random.Range(0f, 1000f);

            // Greener dry grass — still alive, just slightly faded
            Color baseCol = activeProfile != null ? activeProfile.secondaryGroundColor : new Color(0.30f, 0.42f, 0.18f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;

                    float baseR = baseCol.r;
                    float baseG = baseCol.g;
                    float baseB = baseCol.b;

                    // Large color variation — some patches greener, some browner
                    float patch = Mathf.PerlinNoise(u * 4f + ox, v * 4f + oy);
                    baseR += (patch - 0.5f) * 0.10f;
                    baseG += (patch - 0.5f) * 0.08f;
                    baseB += (patch - 0.5f) * 0.04f;

                    // Grass blades (thinner, wind-swept)
                    float bladeNoise = Mathf.PerlinNoise(u * 22f + ox, v * 7f + oy);
                    float blade = Mathf.Sin((u * size * 3f) + bladeNoise * 5f);
                    blade = Mathf.Clamp01(blade * 0.5f + 0.5f);
                    baseR += blade * 0.04f;
                    baseG += blade * 0.03f;
                    baseB -= blade * 0.01f;

                    // Surviving green patches (larger, more prominent)
                    float greenPatch = Mathf.PerlinNoise(u * 5f + ox + 200f, v * 5f + oy + 200f);
                    if (greenPatch > 0.50f)
                    {
                        float blend = (greenPatch - 0.50f) * 2f;
                        baseR = Mathf.Lerp(baseR, 0.22f, blend * 0.4f);
                        baseG = Mathf.Lerp(baseG, 0.40f, blend * 0.4f);
                        baseB = Mathf.Lerp(baseB, 0.14f, blend * 0.4f);
                    }

                    // Exposed dirt patches
                    float dirt = Mathf.PerlinNoise(u * 14f + ox + 300f, v * 14f + oy + 300f);
                    if (dirt > 0.72f)
                    {
                        float blend = (dirt - 0.72f) * 3f;
                        baseR = Mathf.Lerp(baseR, 0.32f, blend * 0.4f);
                        baseG = Mathf.Lerp(baseG, 0.26f, blend * 0.4f);
                        baseB = Mathf.Lerp(baseB, 0.18f, blend * 0.4f);
                    }

                    // Fine noise for detail
                    float fine = Mathf.PerlinNoise(u * 50f + ox * 2f, v * 50f + oy * 2f);
                    baseR += (fine - 0.5f) * 0.04f;
                    baseG += (fine - 0.5f) * 0.035f;
                    baseB += (fine - 0.5f) * 0.02f;

                    // Sun-bleached highlights
                    float sun = Mathf.PerlinNoise(u * 35f + ox * 4f, v * 35f + oy * 4f);
                    if (sun > 0.82f)
                    {
                        float blend = (sun - 0.82f) * 4f;
                        baseR = Mathf.Lerp(baseR, 0.52f, blend * 0.2f);
                        baseG = Mathf.Lerp(baseG, 0.48f, blend * 0.2f);
                        baseB = Mathf.Lerp(baseB, 0.25f, blend * 0.2f);
                    }

                    tex.SetPixel(x, y, new Color(
                        Mathf.Clamp01(baseR),
                        Mathf.Clamp01(baseG),
                        Mathf.Clamp01(baseB),
                        1f));
                }
            }

            tex.Apply(true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = 8;
            return tex;
        }

        // ----- DIRT: Packed earth with pebbles and ruts -----
        private Texture2D GenerateDirtTexture()
        {
            int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            float ox = Random.Range(0f, 1000f);
            float oy = Random.Range(0f, 1000f);

            Color baseCol = activeProfile != null ? activeProfile.dirtColor : new Color(0.33f, 0.28f, 0.2f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;

                    // Base color from profile or default
                    float baseR = baseCol.r;
                    float baseG = baseCol.g;
                    float baseB = baseCol.b;

                    // Large variation (wet/dry patches)
                    float patch = Mathf.PerlinNoise(u * 3f + ox, v * 3f + oy);
                    float darkPatch = Mathf.PerlinNoise(u * 5f + ox + 500f, v * 5f + oy + 500f);
                    // Keep all channels proportional to avoid color shifts
                    float patchVar = (patch - 0.5f) * 0.08f;
                    baseR += patchVar;
                    baseG += patchVar * 0.9f;
                    baseB += patchVar * 0.7f;

                    // Darker wet patches
                    if (darkPatch > 0.6f)
                    {
                        float blend = (darkPatch - 0.6f) * 2f;
                        baseR -= blend * 0.08f;
                        baseG -= blend * 0.06f;
                        baseB -= blend * 0.04f;
                    }

                    // Pebble/grain texture
                    float grain = Mathf.PerlinNoise(u * 50f + ox * 2f, v * 50f + oy * 2f);
                    float grain2 = Mathf.PerlinNoise(u * 35f + ox * 3f, v * 35f + oy * 3f);
                    baseR += (grain - 0.5f) * 0.06f + (grain2 - 0.5f) * 0.04f;
                    baseG += (grain - 0.5f) * 0.05f + (grain2 - 0.5f) * 0.03f;
                    baseB += (grain - 0.5f) * 0.04f + (grain2 - 0.5f) * 0.02f;

                    // Small pebbles (bright spots)
                    float pebble = Mathf.PerlinNoise(u * 80f + ox * 4f, v * 80f + oy * 4f);
                    if (pebble > 0.75f)
                    {
                        float bright = (pebble - 0.75f) * 3f;
                        baseR += bright * 0.08f;
                        baseG += bright * 0.07f;
                        baseB += bright * 0.06f;
                    }

                    // Rut lines (horizontal depressions from wagon wheels)
                    float rut = Mathf.Sin(v * size * 0.8f + Mathf.PerlinNoise(u * 3f, v * 8f) * 2f);
                    rut = Mathf.Clamp01(1f - Mathf.Abs(rut));
                    if (rut > 0.85f)
                    {
                        float blend = (rut - 0.85f) * 5f;
                        baseR -= blend * 0.04f;
                        baseG -= blend * 0.03f;
                        baseB -= blend * 0.02f;
                    }

                    tex.SetPixel(x, y, new Color(
                        Mathf.Clamp01(baseR),
                        Mathf.Clamp01(baseG),
                        Mathf.Clamp01(baseB),
                        1f));
                }
            }

            tex.Apply(true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = 8;
            return tex;
        }

        // ----- ROCK: Stony ground with cracks and lichen -----
        private Texture2D GenerateRockTexture()
        {
            int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            float ox = Random.Range(0f, 1000f);
            float oy = Random.Range(0f, 1000f);

            Color baseCol = activeProfile != null ? activeProfile.rockColor : new Color(0.42f, 0.4f, 0.37f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;

                    // Base color from profile or default
                    float baseR = baseCol.r;
                    float baseG = baseCol.g;
                    float baseB = baseCol.b;

                    // Strata layers (horizontal banding)
                    float strata = Mathf.Sin(v * 15f + Mathf.PerlinNoise(u * 4f + ox, v * 2f + oy) * 3f);
                    strata = strata * 0.5f + 0.5f;
                    baseR += (strata - 0.5f) * 0.06f;
                    baseG += (strata - 0.5f) * 0.05f;
                    baseB += (strata - 0.5f) * 0.04f;

                    // Fracture/crack pattern (Worley-like using multiple noise)
                    float crack1 = Mathf.PerlinNoise(u * 20f + ox, v * 20f + oy);
                    float crack2 = Mathf.PerlinNoise(u * 20f + ox + 100f, v * 20f + oy + 100f);
                    float crackPattern = Mathf.Abs(crack1 - crack2);
                    if (crackPattern < 0.05f)
                    {
                        float crackStrength = 1f - crackPattern / 0.05f;
                        baseR -= crackStrength * 0.12f;
                        baseG -= crackStrength * 0.1f;
                        baseB -= crackStrength * 0.08f;
                    }

                    // Large color blocks (different stone types)
                    float block = Mathf.PerlinNoise(u * 4f + ox + 200f, v * 4f + oy + 200f);
                    if (block > 0.6f)
                    {
                        // Lighter sandstone
                        float blend = (block - 0.6f) * 2f;
                        baseR = Mathf.Lerp(baseR, 0.52f, blend * 0.4f);
                        baseG = Mathf.Lerp(baseG, 0.48f, blend * 0.4f);
                        baseB = Mathf.Lerp(baseB, 0.42f, blend * 0.4f);
                    }

                    // Lichen patches (greenish)
                    float lichen = Mathf.PerlinNoise(u * 10f + ox + 400f, v * 10f + oy + 400f);
                    if (lichen > 0.72f)
                    {
                        float blend = (lichen - 0.72f) * 3f;
                        baseR = Mathf.Lerp(baseR, 0.3f, blend * 0.25f);
                        baseG = Mathf.Lerp(baseG, 0.38f, blend * 0.25f);
                        baseB = Mathf.Lerp(baseB, 0.22f, blend * 0.25f);
                    }

                    // Surface roughness
                    float rough = Mathf.PerlinNoise(u * 60f + ox * 3f, v * 60f + oy * 3f);
                    baseR += (rough - 0.5f) * 0.05f;
                    baseG += (rough - 0.5f) * 0.04f;
                    baseB += (rough - 0.5f) * 0.035f;

                    tex.SetPixel(x, y, new Color(
                        Mathf.Clamp01(baseR),
                        Mathf.Clamp01(baseG),
                        Mathf.Clamp01(baseB),
                        1f));
                }
            }

            tex.Apply(true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = 8;
            return tex;
        }

        // ----- MUD/SPECIAL: Wet mud, snow, or sand for eroded areas -----
        private Texture2D GenerateMudTexture()
        {
            int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            float ox = Random.Range(0f, 1000f);
            float oy = Random.Range(0f, 1000f);

            Color baseCol = activeProfile != null ? activeProfile.specialColor : new Color(0.22f, 0.18f, 0.15f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;

                    // Base color from profile or default
                    float baseR = baseCol.r;
                    float baseG = baseCol.g;
                    float baseB = baseCol.b;

                    // Flow lines (directional noise)
                    float flow = Mathf.Sin(v * 20f + Mathf.PerlinNoise(u * 5f + ox, v * 15f + oy) * 4f);
                    flow = flow * 0.5f + 0.5f;
                    
                    if (flow > 0.7f)
                    {
                        float blend = (flow - 0.7f) * 3f;
                        baseR -= blend * 0.04f;
                        baseG -= blend * 0.03f;
                        baseB -= blend * 0.02f;
                    }

                    // Wet puddles (very dark, reflective)
                    float puddle = Mathf.PerlinNoise(u * 15f + ox * 2f, v * 15f + oy * 2f);
                    if (puddle > 0.6f)
                    {
                        float blend = (puddle - 0.6f) * 2.5f;
                        baseR = Mathf.Lerp(baseR, 0.12f, blend);
                        baseG = Mathf.Lerp(baseG, 0.10f, blend);
                        baseB = Mathf.Lerp(baseB, 0.08f, blend);
                    }

                    // Fine grain
                    float grain = Mathf.PerlinNoise(u * 60f + ox * 3f, v * 60f + oy * 3f);
                    baseR += (grain - 0.5f) * 0.03f;
                    baseG += (grain - 0.5f) * 0.03f;
                    baseB += (grain - 0.5f) * 0.03f;

                    tex.SetPixel(x, y, new Color(
                        Mathf.Clamp01(baseR),
                        Mathf.Clamp01(baseG),
                        Mathf.Clamp01(baseB),
                        puddle > 0.6f ? 0.8f : 0.3f)); // Alpha can be used for smoothness map
                }
            }

            tex.Apply(true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = 8;
            return tex;
        }

        // ----- NORMAL MAP GENERATOR from diffuse texture -----
        private Texture2D GenerateNormalMap(Texture2D source, float strength)
        {
            int w = source.width;
            int h = source.height;
            var normal = new Texture2D(w, h, TextureFormat.RGBA32, true);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Sample heights from neighbors (grayscale)
                    float tl = source.GetPixel((x - 1 + w) % w, (y + 1) % h).grayscale;
                    float t  = source.GetPixel(x, (y + 1) % h).grayscale;
                    float tr = source.GetPixel((x + 1) % w, (y + 1) % h).grayscale;
                    float l  = source.GetPixel((x - 1 + w) % w, y).grayscale;
                    float r  = source.GetPixel((x + 1) % w, y).grayscale;
                    float bl = source.GetPixel((x - 1 + w) % w, (y - 1 + h) % h).grayscale;
                    float b  = source.GetPixel(x, (y - 1 + h) % h).grayscale;
                    float br = source.GetPixel((x + 1) % w, (y - 1 + h) % h).grayscale;

                    // Sobel filter
                    float dX = (tr + 2f * r + br) - (tl + 2f * l + bl);
                    float dY = (bl + 2f * b + br) - (tl + 2f * t + tr);

                    dX *= strength;
                    dY *= strength;

                    // Convert to normal
                    Vector3 n = new Vector3(-dX, -dY, 1f).normalized;

                    // Pack to color (0-1 range)
                    normal.SetPixel(x, y, new Color(
                        n.x * 0.5f + 0.5f,
                        n.y * 0.5f + 0.5f,
                        n.z * 0.5f + 0.5f,
                        1f));
                }
            }

            normal.Apply(true);
            normal.wrapMode = TextureWrapMode.Repeat;
            normal.filterMode = FilterMode.Bilinear;
            return normal;
        }

        // ===========================================================================
        // SPLATMAP — Realistic texture blending based on height, slope, noise
        // ===========================================================================

        private void PaintAdvancedSplatmap()
        {
            int res = terrainData.alphamapResolution;
            float[,,] splatmap = new float[res, res, 5]; // 5 layers now (Grass, DryGrass, Dirt, Rock, Mud)
            float[,] heights = terrainData.GetHeights(0, 0, heightmapResolution, heightmapResolution);

            float ox = settings.seed * 0.1f;
            float oz = settings.seed * 0.2f;

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float nx = (float)x / res;
                    float nz = (float)z / res;

                    int hx = Mathf.FloorToInt(nx * (heightmapResolution - 1));
                    int hz = Mathf.FloorToInt(nz * (heightmapResolution - 1));
                    hx = Mathf.Clamp(hx, 1, heightmapResolution - 2);
                    hz = Mathf.Clamp(hz, 1, heightmapResolution - 2);

                    float h = heights[hz, hx];

                    // Calculate slope from neighbor heights
                    float hL = heights[hz, hx - 1];
                    float hR = heights[hz, hx + 1];
                    float hU = heights[hz + 1, hx];
                    float hD = heights[hz - 1, hx];
                    float slope = Mathf.Abs(hR - hL) + Mathf.Abs(hU - hD);
                    slope *= heightmapResolution * 0.5f; // Normalize

                    // Read erosion amount if available
                    float erosion = (erosionMap != null) ? erosionMap[hz, hx] : 0f;

                    // Noise layers for variety
                    float noise1 = Mathf.PerlinNoise(nx * 12f + ox, nz * 12f + oz);
                    float noise2 = Mathf.PerlinNoise(nx * 25f + ox + 100f, nz * 25f + oz + 100f);

                    // Weights
                    float grass = 1f;
                    float dryGrass = 0f;
                    float dirt = 0f;
                    float rock = 0f;
                    float mud = 0f;

                    // 1. Height-based: hilltops get rocky
                    if (h > 0.08f)
                    {
                        rock = Mathf.SmoothStep(0f, 1f, (h - 0.08f) * 20f);
                        grass -= rock;
                    }

                    // 2. Steep slopes get rocky/dirt
                    if (slope > 0.25f)
                    {
                        float slopeFactor = Mathf.SmoothStep(0f, 1f, (slope - 0.25f) * 4f);
                        rock += slopeFactor * 0.6f;
                        dirt += slopeFactor * 0.4f;
                        grass -= slopeFactor;
                        dryGrass -= slopeFactor;
                    }

                    // 3. Erosion gullies get mud/dirt
                    if (erosion > 0.005f)
                    {
                        float erosionFactor = Mathf.Clamp01(erosion * 50f);
                        mud += erosionFactor * 0.7f;
                        dirt += erosionFactor * 0.3f;
                        grass -= erosionFactor;
                        dryGrass -= erosionFactor;
                    }

                    // 4. Natural biome noise (dry grass vs lush grass) — less dry grass for greener look
                    if (noise1 > 0.65f && rock < 0.5f && dirt < 0.5f && mud < 0.5f)
                    {
                        float blend = Mathf.SmoothStep(0f, 1f, (noise1 - 0.65f) * 3f);
                        dryGrass += blend * grass * 0.5f; // Only 50% conversion to dry
                        grass -= blend * grass * 0.5f;
                    }

                    // 5. Random dirt patches (trampled areas) — fewer and smaller
                    if (noise2 > 0.75f && slope < 0.1f && mud < 0.5f)
                    {
                        float blend = (noise2 - 0.75f) * 2f;
                        dirt += blend * 0.3f;
                        grass -= blend * 0.2f;
                        dryGrass -= blend * 0.1f;
                    }

                    // Normalize weights
                    grass = Mathf.Max(0f, grass);
                    dryGrass = Mathf.Max(0f, dryGrass);
                    dirt = Mathf.Max(0f, dirt);
                    rock = Mathf.Max(0f, rock);
                    mud = Mathf.Max(0f, mud);
                    
                    float total = grass + dryGrass + dirt + rock + mud;

                    if (total > 0f)
                    {
                        splatmap[z, x, 0] = grass / total;
                        splatmap[z, x, 1] = dryGrass / total;
                        splatmap[z, x, 2] = dirt / total;
                        splatmap[z, x, 3] = rock / total;
                        splatmap[z, x, 4] = mud / total;
                    }
                    else
                    {
                        splatmap[z, x, 0] = 1f;
                    }
                }
            }

            terrainData.SetAlphamaps(0, 0, splatmap);
        }

        // ===========================================================================
        // TERRAIN MATERIAL
        // ===========================================================================

        private Material CreateTerrainMaterial()
        {
            // Try multiple known URP Terrain shader names (varies by Unity version)
            string[] shaderNames = new string[]
            {
                "Universal Render Pipeline/Terrain/Lit",
                "Shader Graphs/Terrain Lit",
                "Nature/Terrain/Standard",
                "Hidden/TerrainEngine/Splatmap/Standard-Base",
            };

            Shader shader = null;
            foreach (string name in shaderNames)
            {
                shader = Shader.Find(name);
                if (shader != null)
                {
                    Debug.Log($"[TerrainGenerator] Terrain shader found: {name}");
                    break;
                }
            }

            if (shader == null)
            {
                Debug.LogWarning("[TerrainGenerator] No terrain shader found! Falling back to URP Lit.");
                shader = URPMaterialHelper.LitShader;
            }

            Material mat = new Material(shader);
            mat.enableInstancing = true;
            return mat;
        }

        // ===========================================================================
        // VEGETATION (Poisson Disk Sampling)
        // ===========================================================================

        private void AddAdvancedVegetation()
        {
            // Lush deciduous forest colors — rich greens for a wooded battlefield
            string name1 = activeProfile != null ? activeProfile.treeName1 : "Oak";
            Color col1 = activeProfile != null ? activeProfile.treeColor1 : new Color(0.14f, 0.45f, 0.10f);
            float h1 = activeProfile != null ? activeProfile.treeHeight1 : 8f;
            float w1 = activeProfile != null ? activeProfile.treeWidth1 : 6f;

            string name2 = activeProfile != null ? activeProfile.treeName2 : "Birch";
            Color col2 = activeProfile != null ? activeProfile.treeColor2 : new Color(0.18f, 0.50f, 0.14f);
            float h2 = activeProfile != null ? activeProfile.treeHeight2 : 10f;
            float w2 = activeProfile != null ? activeProfile.treeWidth2 : 4f;

            // Pre-create shared materials (one per color — all trees share these)
            Material canopyMat1 = URPMaterialHelper.CreateLit(col1);
            canopyMat1.enableInstancing = true;
            Material canopyMat2 = URPMaterialHelper.CreateLit(col2);
            canopyMat2.enableInstancing = true;
            Material trunkMat = URPMaterialHelper.CreateLit(new Color(0.35f, 0.22f, 0.10f));
            trunkMat.enableInstancing = true;

            // Get shared meshes from primitives (create once, reuse for all trees)
            GameObject sphereTemp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh sphereMesh = sphereTemp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(sphereTemp);

            GameObject cylTemp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Mesh cylMesh = cylTemp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(cylTemp);

            // Container for all trees
            GameObject treeContainer = new GameObject("Trees");
            treeContainer.transform.position = Vector3.zero;

            // Generate Poisson Disk sample points for natural distribution
            var points = PoissonDiskSampling.GeneratePoints(
                settings.forestDensity,
                new Vector2(terrainWidth, terrainLength),
                30,
                settings.seed
            );

            float[,] heights = terrainData.GetHeights(0, 0, heightmapResolution, heightmapResolution);
            int treeCount = 0;
            float terrainOffsetX = -terrainWidth / 2f;
            float terrainOffsetZ = -terrainLength / 2f;

            foreach (Vector2 point in points)
            {
                if (treeCount >= settings.treeCount) break;

                float nx = point.x / terrainWidth;
                float nz = point.y / terrainLength;

                // Only clear the core fighting area
                float distToCenter = Vector2.Distance(new Vector2(nx, nz), new Vector2(0.5f, 0.5f));
                if (distToCenter < settings.battleAreaRadius * 0.6f) continue;

                int hx = Mathf.Clamp(Mathf.FloorToInt(nx * (heightmapResolution - 1)), 0, heightmapResolution - 1);
                int hz = Mathf.Clamp(Mathf.FloorToInt(nz * (heightmapResolution - 1)), 0, heightmapResolution - 1);
                float h = heights[hz, hx];

                float steepness = terrainData.GetSteepness(nx, nz);
                if (steepness > 30f || h > 0.15f) continue;

                // World position
                float worldX = point.x + terrainOffsetX;
                float worldZ = point.y + terrainOffsetZ;
                float worldY = h * terrainHeight;

                // Choose tree type based on noise
                float typeNoise = Mathf.PerlinNoise(nx * 10f, nz * 10f);
                bool isType1 = typeNoise > 0.5f;
                Material canopyMat = isType1 ? canopyMat1 : canopyMat2;
                float treeH = isType1 ? h1 : h2;
                float treeW = isType1 ? w1 : w2;

                // Random variation
                float hScale = Random.Range(0.6f, 1.3f);
                float wScale = Random.Range(0.7f, 1.2f);
                float finalH = treeH * hScale;
                float finalW = treeW * wScale;

                // Create tree GameObject
                GameObject treeGO = new GameObject("Tree");
                treeGO.transform.SetParent(treeContainer.transform);
                treeGO.transform.position = new Vector3(worldX, worldY, worldZ);

                // Trunk
                GameObject trunk = new GameObject("Trunk");
                trunk.transform.SetParent(treeGO.transform);
                trunk.AddComponent<MeshFilter>().sharedMesh = cylMesh;
                trunk.AddComponent<MeshRenderer>().sharedMaterial = trunkMat;
                float trunkH = finalH * 0.5f;
                trunk.transform.localScale = new Vector3(0.4f, trunkH, 0.4f);
                trunk.transform.localPosition = new Vector3(0f, trunkH, 0f);

                // Canopy
                GameObject canopy = new GameObject("Canopy");
                canopy.transform.SetParent(treeGO.transform);
                canopy.AddComponent<MeshFilter>().sharedMesh = sphereMesh;
                canopy.AddComponent<MeshRenderer>().sharedMaterial = canopyMat;
                canopy.transform.localScale = new Vector3(finalW, finalH * 0.6f, finalW);
                canopy.transform.localPosition = new Vector3(0f, trunkH * 2f + finalH * 0.2f, 0f);

                treeGO.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                treeCount++;
            }

            Debug.Log($"[TerrainGenerator] Spawned {treeCount} trees as GameObjects (max: {settings.treeCount})");
        }

        private GameObject CreateTreePrefab(string name, Color canopyColor, float height, float canopyWidth)
        {
            GameObject tree = new GameObject($"Tree_{name}");

            // --- TRUNK (brown cylinder) ---
            GameObject trunkTemp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            MeshFilter trunkMF = trunkTemp.GetComponent<MeshFilter>();

            GameObject trunkGO = new GameObject("Trunk");
            trunkGO.transform.SetParent(tree.transform);
            MeshFilter trunkMeshF = trunkGO.AddComponent<MeshFilter>();
            trunkMeshF.sharedMesh = trunkMF.sharedMesh;
            MeshRenderer trunkMR = trunkGO.AddComponent<MeshRenderer>();
            trunkMR.sharedMaterial = CreateTreeMaterial(new Color(0.35f, 0.22f, 0.10f)); // Brown bark
            trunkGO.transform.localScale = new Vector3(0.15f, 0.5f, 0.15f); // Thin & tall
            trunkGO.transform.localPosition = new Vector3(0f, 0.5f, 0f);

            // Remove collider from temp
            Object.DestroyImmediate(trunkTemp);

            // --- CANOPY (green sphere) ---
            GameObject canopyTemp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            MeshFilter canopyMF = canopyTemp.GetComponent<MeshFilter>();

            GameObject canopyGO = new GameObject("Canopy");
            canopyGO.transform.SetParent(tree.transform);
            MeshFilter canopyMeshF = canopyGO.AddComponent<MeshFilter>();
            canopyMeshF.sharedMesh = canopyMF.sharedMesh;
            MeshRenderer canopyMR = canopyGO.AddComponent<MeshRenderer>();
            canopyMR.sharedMaterial = CreateTreeMaterial(canopyColor);
            // Canopy sits on top of trunk with some overlap
            float canopyScale = canopyWidth * 0.2f; // Relative to tree scale
            canopyGO.transform.localScale = new Vector3(canopyScale, canopyScale * 0.7f, canopyScale);
            canopyGO.transform.localPosition = new Vector3(0f, 0.85f, 0f);

            Object.DestroyImmediate(canopyTemp);

            // Overall tree scale
            tree.transform.localScale = new Vector3(1f, height * 0.5f, 1f);
            tree.SetActive(false);
            tree.transform.SetParent(transform);
            return tree;
        }

        private Material CreateTreeMaterial(Color color)
        {
            // Use URP Lit shader directly — Nature/Tree shaders don't exist in URP
            Material mat = URPMaterialHelper.CreateLit(color);
            mat.enableInstancing = true;
            return mat;
        }

        // ===========================================================================
        // GRASS/DETAIL
        // ===========================================================================

        private void AddGrassDetails()
        {
            // Biome colors
            Color healthy = activeProfile != null ? activeProfile.grassHealthy : new Color(0.18f, 0.52f, 0.12f);
            Color dry = activeProfile != null ? activeProfile.grassDry : new Color(0.35f, 0.48f, 0.18f);
            float maxH = activeProfile != null ? activeProfile.grassMaxHeight : 0.8f;
            float profileDensity = activeProfile != null ? activeProfile.grassDensityMult : 1f;

            // ============================================================
            // 4 DETAIL LAYERS: tall grass, short grass, wildflowers, clover
            // ============================================================
            DetailPrototype[] details = new DetailPrototype[4];

            // [0] Tall grass — full blades swaying in wind
            details[0] = new DetailPrototype();
            details[0].renderMode = DetailRenderMode.GrassBillboard;
            details[0].healthyColor = healthy;
            details[0].dryColor = dry;
            details[0].minHeight = maxH * 0.5f;
            details[0].maxHeight = maxH;
            details[0].minWidth = 0.4f;
            details[0].maxWidth = 0.8f;

            // [1] Short grass — ground cover between tall patches
            details[1] = new DetailPrototype();
            details[1].renderMode = DetailRenderMode.GrassBillboard;
            details[1].healthyColor = Color.Lerp(healthy, dry, 0.15f);
            details[1].dryColor = Color.Lerp(dry, healthy, 0.1f);
            details[1].minHeight = maxH * 0.15f;
            details[1].maxHeight = maxH * 0.35f;
            details[1].minWidth = 0.2f;
            details[1].maxWidth = 0.5f;

            // [2] Wildflowers — small color pops (conditional on biome)
            Color flowerHealthy = activeProfile != null && activeProfile.biome == NapoleonicWars.Campaign.ProvinceTerrainType.Desert
                ? new Color(0.65f, 0.55f, 0.30f) // Desert sage
                : new Color(0.75f, 0.72f, 0.20f); // Yellow wildflowers
            Color flowerDry = activeProfile != null && activeProfile.biome == NapoleonicWars.Campaign.ProvinceTerrainType.Snow
                ? new Color(0.85f, 0.85f, 0.90f) // Frost white
                : new Color(0.85f, 0.50f, 0.25f); // Orange/brown
            details[2] = new DetailPrototype();
            details[2].renderMode = DetailRenderMode.GrassBillboard;
            details[2].healthyColor = flowerHealthy;
            details[2].dryColor = flowerDry;
            details[2].minHeight = maxH * 0.2f;
            details[2].maxHeight = maxH * 0.45f;
            details[2].minWidth = 0.15f;
            details[2].maxWidth = 0.35f;

            // [3] Clover/moss — very low ground cover
            Color cloverHealthy = Color.Lerp(healthy, new Color(0.10f, 0.35f, 0.08f), 0.4f);
            details[3] = new DetailPrototype();
            details[3].renderMode = DetailRenderMode.GrassBillboard;
            details[3].healthyColor = cloverHealthy;
            details[3].dryColor = Color.Lerp(cloverHealthy, dry, 0.3f);
            details[3].minHeight = maxH * 0.08f;
            details[3].maxHeight = maxH * 0.18f;
            details[3].minWidth = 0.3f;
            details[3].maxWidth = 0.6f;

            terrainData.detailPrototypes = details;

            int detailRes = 512;  // Higher resolution for better coverage
            terrainData.SetDetailResolution(detailRes, 16);

            int[,] tallGrassMap = new int[detailRes, detailRes];
            int[,] shortGrassMap = new int[detailRes, detailRes];
            int[,] flowerMap = new int[detailRes, detailRes];
            int[,] cloverMap = new int[detailRes, detailRes];

            float ox = settings.seed * 0.3f;

            for (int z = 0; z < detailRes; z++)
            {
                for (int x = 0; x < detailRes; x++)
                {
                    float nx = (float)x / detailRes;
                    float nz = (float)z / detailRes;

                    // Skip deeply trampled center
                    float distToCenter = Vector2.Distance(new Vector2(nx, nz), new Vector2(0.5f, 0.5f));
                    if (distToCenter < settings.battleAreaRadius * 0.3f) continue;

                    // Terrain checks
                    float steepness = terrainData.GetSteepness(nx, nz);
                    int hx = Mathf.Clamp(Mathf.FloorToInt(nx * (heightmapResolution - 1)), 0, heightmapResolution - 1);
                    int hz = Mathf.Clamp(Mathf.FloorToInt(nz * (heightmapResolution - 1)), 0, heightmapResolution - 1);
                    float h = terrainData.GetHeight(hx, hz) / terrainHeight;
                    float erosion = (erosionMap != null) ? erosionMap[hz, hx] : 0f;

                    // Much more relaxed cutoffs — grass grows almost everywhere
                    if (steepness > 35f || h > 0.5f || erosion > 0.005f) continue;

                    // Noise layers at different frequencies
                    float noise1 = Mathf.PerlinNoise(nx * 8f + ox, nz * 8f + ox);
                    float noise2 = Mathf.PerlinNoise(nx * 15f + ox + 100f, nz * 15f + ox + 100f);
                    float noise3 = Mathf.PerlinNoise(nx * 25f + ox + 200f, nz * 25f + ox + 200f);
                    float noise4 = Mathf.PerlinNoise(nx * 12f + ox + 300f, nz * 12f + ox + 300f);

                    // Density fades toward battle center (but still some grass)
                    float centerFade = Mathf.Clamp01((distToCenter - settings.battleAreaRadius * 0.3f) / (settings.battleAreaRadius * 0.5f));
                    float densityMult = Mathf.Lerp(0.3f, 1f, centerFade) * profileDensity;

                    // Reduce on very steep slopes
                    float slopeReduction = Mathf.Clamp01(1f - steepness / 35f);
                    densityMult *= slopeReduction;

                    // [0] Tall grass in organic patches — everywhere with noise
                    if (noise1 > 0.28f)
                        tallGrassMap[z, x] = Mathf.RoundToInt(Random.Range(2, 8) * densityMult);

                    // [1] Short grass — very uniform, fills all gaps densely
                    if (noise2 > 0.15f)
                        shortGrassMap[z, x] = Mathf.RoundToInt(Random.Range(4, 12) * densityMult);

                    // [2] Wildflowers — scattered clusters in outer areas
                    if (noise3 > 0.55f && distToCenter > settings.battleAreaRadius * 0.6f)
                        flowerMap[z, x] = Mathf.RoundToInt(Random.Range(1, 4) * densityMult * 0.5f);

                    // [3] Clover/moss — in damp low areas
                    if (noise4 > 0.35f && steepness < 15f && h < 0.15f)
                        cloverMap[z, x] = Mathf.RoundToInt(Random.Range(3, 8) * densityMult * 0.6f);
                }
            }

            terrainData.SetDetailLayer(0, 0, 0, tallGrassMap);
            terrainData.SetDetailLayer(0, 0, 1, shortGrassMap);
            terrainData.SetDetailLayer(0, 0, 2, flowerMap);
            terrainData.SetDetailLayer(0, 0, 3, cloverMap);

            Debug.Log($"[TerrainGenerator] Grass detail: 4 layers at {detailRes}x{detailRes}, density={profileDensity:F2}");
        }

        // ============================================================
        // GROUND SCATTER — Rocks, bushes, fallen logs, debris
        // Procedural low-poly meshes placed as GameObjects
        // ============================================================

        private void AddGroundScatter()
        {
            GameObject scatterContainer = new GameObject("GroundScatter");
            scatterContainer.transform.position = Vector3.zero;

            // Shared primitive meshes
            GameObject cubeTemp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh cubeMesh = cubeTemp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(cubeTemp);

            GameObject sphereTemp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh sphereMesh = sphereTemp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(sphereTemp);

            GameObject cylTemp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Mesh cylMesh = cylTemp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(cylTemp);

            // Materials (shared, instanced)
            Color rockCol = activeProfile != null ? activeProfile.rockColor : new Color(0.45f, 0.43f, 0.40f);
            Color dirtCol = activeProfile != null ? activeProfile.dirtColor : new Color(0.35f, 0.28f, 0.18f);
            Color bushCol = activeProfile != null ? activeProfile.treeColor1 : new Color(0.15f, 0.35f, 0.10f);

            Material rockMat = URPMaterialHelper.CreateLit(rockCol);
            rockMat.enableInstancing = true;
            Material darkRockMat = URPMaterialHelper.CreateLit(rockCol * 0.7f);
            darkRockMat.enableInstancing = true;
            Material mossRockMat = URPMaterialHelper.CreateLit(Color.Lerp(rockCol, bushCol, 0.3f));
            mossRockMat.enableInstancing = true;
            Material bushMat = URPMaterialHelper.CreateLit(bushCol);
            bushMat.enableInstancing = true;
            Material darkBushMat = URPMaterialHelper.CreateLit(Color.Lerp(bushCol, dirtCol, 0.3f));
            darkBushMat.enableInstancing = true;
            Material logMat = URPMaterialHelper.CreateLit(new Color(0.30f, 0.20f, 0.12f));
            logMat.enableInstancing = true;

            float[,] heights = terrainData.GetHeights(0, 0, heightmapResolution, heightmapResolution);
            float terrainOffsetX = -terrainWidth / 2f;
            float terrainOffsetZ = -terrainLength / 2f;
            float ox = settings.seed * 0.7f;

            int rockCount = 0;
            int bushCount = 0;
            int logCount = 0;

            // Determine scatter density from biome
            float scatterDensity = 1f;
            if (activeProfile != null)
            {
                switch (activeProfile.biome)
                {
                    case NapoleonicWars.Campaign.ProvinceTerrainType.Desert:
                        scatterDensity = 0.4f; break; // Few rocks, almost no vegetation
                    case NapoleonicWars.Campaign.ProvinceTerrainType.Snow:
                        scatterDensity = 0.6f; break;
                    case NapoleonicWars.Campaign.ProvinceTerrainType.Forest:
                        scatterDensity = 1.8f; break; // Dense undergrowth
                    case NapoleonicWars.Campaign.ProvinceTerrainType.Marsh:
                        scatterDensity = 1.2f; break;
                    case NapoleonicWars.Campaign.ProvinceTerrainType.Urban:
                        scatterDensity = 0.2f; break; // Very few natural objects
                    case NapoleonicWars.Campaign.ProvinceTerrainType.Mountains:
                        scatterDensity = 1.4f; break; // More rocks
                }
            }

            // ============================================================
            // PASS 1: ROCKS (large, medium, small)
            // ============================================================
            int maxRocks = Mathf.RoundToInt(200 * scatterDensity);
            for (int i = 0; i < maxRocks; i++)
            {
                float rx = Random.Range(0.05f, 0.95f);
                float rz = Random.Range(0.05f, 0.95f);

                float distToCenter = Vector2.Distance(new Vector2(rx, rz), new Vector2(0.5f, 0.5f));
                if (distToCenter < settings.battleAreaRadius * 0.7f) continue;

                int hx = Mathf.Clamp(Mathf.FloorToInt(rx * (heightmapResolution - 1)), 0, heightmapResolution - 1);
                int hz = Mathf.Clamp(Mathf.FloorToInt(rz * (heightmapResolution - 1)), 0, heightmapResolution - 1);
                float steepness = terrainData.GetSteepness(rx, rz);
                float h = heights[hz, hx];

                // Rocks like slopes and rocky areas
                float rockChance = Mathf.PerlinNoise(rx * 6f + ox + 500f, rz * 6f + ox + 500f);
                if (steepness < 5f && rockChance < 0.5f) continue;

                float worldX = rx * terrainWidth + terrainOffsetX;
                float worldZ = rz * terrainLength + terrainOffsetZ;
                float worldY = h * terrainHeight;

                // Size category
                float sizeRoll = Random.value;
                Vector3 scale;
                Material mat;

                if (sizeRoll < 0.15f)
                {
                    // Large boulder
                    float s = Random.Range(1.5f, 3.5f);
                    scale = new Vector3(s * Random.Range(0.7f, 1.3f), s * Random.Range(0.4f, 0.8f), s * Random.Range(0.7f, 1.3f));
                    mat = Random.value > 0.4f ? rockMat : mossRockMat;
                }
                else if (sizeRoll < 0.5f)
                {
                    // Medium rock
                    float s = Random.Range(0.5f, 1.5f);
                    scale = new Vector3(s * Random.Range(0.6f, 1.4f), s * Random.Range(0.5f, 0.9f), s * Random.Range(0.6f, 1.4f));
                    mat = Random.value > 0.6f ? darkRockMat : rockMat;
                }
                else
                {
                    // Small pebble/stone
                    float s = Random.Range(0.1f, 0.5f);
                    scale = new Vector3(s * Random.Range(0.7f, 1.3f), s * Random.Range(0.5f, 1f), s * Random.Range(0.7f, 1.3f));
                    mat = Random.value > 0.5f ? rockMat : darkRockMat;
                }

                GameObject rock = new GameObject("Rock");
                rock.transform.SetParent(scatterContainer.transform);
                rock.transform.position = new Vector3(worldX, worldY - scale.y * 0.2f, worldZ);
                rock.transform.rotation = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-10f, 10f));

                // Use sphere for rounded rocks, cube for angular ones
                MeshFilter mf = rock.AddComponent<MeshFilter>();
                mf.sharedMesh = Random.value > 0.4f ? sphereMesh : cubeMesh;
                MeshRenderer mr = rock.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                rock.transform.localScale = scale;

                rockCount++;
            }

            // ============================================================
            // PASS 2: BUSHES (sphere clusters)
            // ============================================================
            bool isDesert = activeProfile != null && activeProfile.biome == NapoleonicWars.Campaign.ProvinceTerrainType.Desert;
            bool isUrban = activeProfile != null && activeProfile.biome == NapoleonicWars.Campaign.ProvinceTerrainType.Urban;
            int maxBushes = isDesert || isUrban ? 20 : Mathf.RoundToInt(120 * scatterDensity);

            for (int i = 0; i < maxBushes; i++)
            {
                float bx = Random.Range(0.08f, 0.92f);
                float bz = Random.Range(0.08f, 0.92f);

                float distToCenter = Vector2.Distance(new Vector2(bx, bz), new Vector2(0.5f, 0.5f));
                if (distToCenter < settings.battleAreaRadius * 0.8f) continue;

                int hx = Mathf.Clamp(Mathf.FloorToInt(bx * (heightmapResolution - 1)), 0, heightmapResolution - 1);
                int hz = Mathf.Clamp(Mathf.FloorToInt(bz * (heightmapResolution - 1)), 0, heightmapResolution - 1);
                float steepness = terrainData.GetSteepness(bx, bz);
                if (steepness > 20f) continue;

                float h = heights[hz, hx];
                float worldX = bx * terrainWidth + terrainOffsetX;
                float worldZ = bz * terrainLength + terrainOffsetZ;
                float worldY = h * terrainHeight;

                // Bush = 2-3 overlapping spheres
                GameObject bush = new GameObject("Bush");
                bush.transform.SetParent(scatterContainer.transform);
                bush.transform.position = new Vector3(worldX, worldY, worldZ);
                bush.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                int spheres = Random.Range(2, 4);
                float bushSize = Random.Range(0.6f, 1.8f);
                Material bMat = Random.value > 0.5f ? bushMat : darkBushMat;

                for (int s = 0; s < spheres; s++)
                {
                    GameObject leaf = new GameObject("Leaf");
                    leaf.transform.SetParent(bush.transform);
                    leaf.AddComponent<MeshFilter>().sharedMesh = sphereMesh;
                    leaf.AddComponent<MeshRenderer>().sharedMaterial = bMat;

                    float ls = bushSize * Random.Range(0.5f, 1f);
                    leaf.transform.localScale = new Vector3(ls, ls * Random.Range(0.6f, 0.9f), ls);
                    leaf.transform.localPosition = new Vector3(
                        Random.Range(-bushSize * 0.3f, bushSize * 0.3f),
                        ls * 0.3f + s * ls * 0.2f,
                        Random.Range(-bushSize * 0.3f, bushSize * 0.3f)
                    );
                }

                bushCount++;
            }

            // ============================================================
            // PASS 3: FALLEN LOGS (cylinders lying flat)
            // ============================================================
            bool hasLogs = activeProfile == null ||
                (activeProfile.biome != NapoleonicWars.Campaign.ProvinceTerrainType.Desert &&
                 activeProfile.biome != NapoleonicWars.Campaign.ProvinceTerrainType.Urban);

            if (hasLogs)
            {
                int maxLogs = Mathf.RoundToInt(30 * scatterDensity);
                for (int i = 0; i < maxLogs; i++)
                {
                    float lx = Random.Range(0.1f, 0.9f);
                    float lz = Random.Range(0.1f, 0.9f);

                    float distToCenter = Vector2.Distance(new Vector2(lx, lz), new Vector2(0.5f, 0.5f));
                    if (distToCenter < settings.battleAreaRadius * 0.85f) continue;

                    int hx = Mathf.Clamp(Mathf.FloorToInt(lx * (heightmapResolution - 1)), 0, heightmapResolution - 1);
                    int hz = Mathf.Clamp(Mathf.FloorToInt(lz * (heightmapResolution - 1)), 0, heightmapResolution - 1);
                    float steepness = terrainData.GetSteepness(lx, lz);
                    if (steepness > 15f) continue;

                    float h = heights[hz, hx];
                    float worldX = lx * terrainWidth + terrainOffsetX;
                    float worldZ = lz * terrainLength + terrainOffsetZ;
                    float worldY = h * terrainHeight;

                    float logLength = Random.Range(2f, 5f);
                    float logRadius = Random.Range(0.15f, 0.35f);

                    GameObject log = new GameObject("FallenLog");
                    log.transform.SetParent(scatterContainer.transform);
                    log.transform.position = new Vector3(worldX, worldY + logRadius * 0.5f, worldZ);
                    // Lying flat: cylinder default is vertical, rotate 90 on Z
                    log.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 90f);

                    MeshFilter mf = log.AddComponent<MeshFilter>();
                    mf.sharedMesh = cylMesh;
                    MeshRenderer mr = log.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = logMat;
                    log.transform.localScale = new Vector3(logRadius, logLength * 0.5f, logRadius);

                    logCount++;
                }
            }

            Debug.Log($"[TerrainGenerator] Ground scatter: {rockCount} rocks, {bushCount} bushes, {logCount} logs (density={scatterDensity:F1})");
        }
    }
}

