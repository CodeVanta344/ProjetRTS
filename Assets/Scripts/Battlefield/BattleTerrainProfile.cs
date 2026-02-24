using UnityEngine;
using NapoleonicWars.Campaign;
using NapoleonicWars.Core;

namespace NapoleonicWars.Battlefield
{
    /// <summary>
    /// Defines all visual/gameplay parameters for a biome-specific battlefield.
    /// Each ProvinceTerrainType maps to one profile.
    /// </summary>
    [System.Serializable]
    public class BattleTerrainProfile
    {
        // ==================== IDENTITY ====================
        public ProvinceTerrainType biome;
        public string displayName;

        // ==================== HEIGHTMAP ====================
        public float terrainScale = 1200f;
        public int octaves = 6;
        public float persistence = 0.45f;
        public float lacunarity = 2.2f;
        public float terrainHeightMax = 50f;       // TerrainGenerator terrainHeight
        public float battleAreaRadius = 0.25f;
        public bool flattenCenter = true;
        public float flattenBaseHeight = 0.1f;     // Height the center flattens to

        // ==================== TEXTURE COLORS (5 layers) ====================
        // Layer 0: Primary ground (grass/sand/snow)
        public Color primaryGroundColor = new Color(0.2f, 0.38f, 0.1f);
        // Layer 1: Secondary ground (dry grass/gravel)
        public Color secondaryGroundColor = new Color(0.5f, 0.45f, 0.25f);
        // Layer 2: Dirt/path
        public Color dirtColor = new Color(0.33f, 0.28f, 0.2f);
        // Layer 3: Rock/cliff
        public Color rockColor = new Color(0.42f, 0.4f, 0.37f);
        // Layer 4: Special (mud/snow/sand)
        public Color specialColor = new Color(0.22f, 0.18f, 0.15f);
        public float specialSmoothness = 0.3f;

        // ==================== VEGETATION ====================
        public int treeCount = 1500;
        public float forestDensity = 25f;
        public Color treeColor1 = new Color(0.18f, 0.38f, 0.12f);  // Oak/deciduous
        public Color treeColor2 = new Color(0.12f, 0.3f, 0.1f);    // Pine/conifer
        public float treeHeight1 = 7f;
        public float treeHeight2 = 10f;
        public float treeWidth1 = 5f;
        public float treeWidth2 = 3f;
        public string treeName1 = "Oak";
        public string treeName2 = "Pine";

        // ==================== GRASS DETAIL ====================
        public Color grassHealthy = new Color(0.25f, 0.45f, 0.15f);
        public Color grassDry = new Color(0.5f, 0.45f, 0.2f);
        public float grassDensityMult = 1.0f;
        public float grassMaxHeight = 1.0f;

        // ==================== LIGHTING ====================
        public Color sunColor = new Color(1f, 0.95f, 0.85f);
        public float sunIntensity = 1.5f;
        public Vector3 sunRotation = new Vector3(45f, -30f, 0f);
        public Color fillLightColor = new Color(0.6f, 0.7f, 0.85f);
        public float fillLightIntensity = 0.3f;
        public Color ambientColor = new Color(0.4f, 0.45f, 0.5f);
        public Color skyColor = new Color(0.5f, 0.65f, 0.85f);
        public Color fogColor = new Color(0.6f, 0.65f, 0.7f);
        public float fogDensity = 0f; // 0 = no fog

        // ==================== WEATHER ====================
        public WeatherType defaultWeather = WeatherType.Clear;

        // ==================== ASSET FLAGS ====================
        public bool placeVillage = true;
        public bool placeDefensiveLines = true;
        public bool placeNaturalCover = true;
        public bool placeBiomeSpecificAssets = true;

        // ============================================================
        // STATIC PROFILE FACTORY
        // ============================================================

        public static BattleTerrainProfile GetProfile(ProvinceTerrainType type)
        {
            return type switch
            {
                ProvinceTerrainType.Plains => CreatePlains(),
                ProvinceTerrainType.Forest => CreateForest(),
                ProvinceTerrainType.Hills => CreateHills(),
                ProvinceTerrainType.Mountains => CreateMountains(),
                ProvinceTerrainType.Desert => CreateDesert(),
                ProvinceTerrainType.Snow => CreateSnow(),
                ProvinceTerrainType.Coastal => CreateCoastal(),
                ProvinceTerrainType.Marsh => CreateMarsh(),
                ProvinceTerrainType.Urban => CreateUrban(),
                _ => CreatePlains()
            };
        }

        // ==================== PLAINS ====================
        private static BattleTerrainProfile CreatePlains()
        {
            return new BattleTerrainProfile
            {
                biome = ProvinceTerrainType.Plains,
                displayName = "Plaines",
                terrainScale = 1200f,
                octaves = 5,
                persistence = 0.35f,
                terrainHeightMax = 30f,
                battleAreaRadius = 0.3f,
                flattenCenter = true,
                flattenBaseHeight = 0.08f,

                // Rich green European meadow
                primaryGroundColor = new Color(0.12f, 0.42f, 0.10f),
                secondaryGroundColor = new Color(0.28f, 0.40f, 0.16f),
                dirtColor = new Color(0.30f, 0.24f, 0.16f),
                rockColor = new Color(0.38f, 0.36f, 0.32f),
                specialColor = new Color(0.20f, 0.16f, 0.12f),
                specialSmoothness = 0.3f,

                // Dense woodland surrounding battlefield
                treeCount = 2500,
                forestDensity = 18f,
                treeColor1 = new Color(0.14f, 0.45f, 0.10f),
                treeColor2 = new Color(0.18f, 0.50f, 0.14f),
                treeHeight1 = 8f, treeHeight2 = 10f,
                treeWidth1 = 6f, treeWidth2 = 4f,
                treeName1 = "Oak", treeName2 = "Birch",

                grassHealthy = new Color(0.18f, 0.52f, 0.12f),
                grassDry = new Color(0.35f, 0.48f, 0.18f),
                grassDensityMult = 1.0f,
                grassMaxHeight = 0.8f,

                // Natural outdoor lighting
                sunColor = new Color(1f, 0.94f, 0.82f),
                sunIntensity = 1.3f,
                sunRotation = new Vector3(35f, -40f, 0f),
                fillLightColor = new Color(0.55f, 0.65f, 0.80f),
                fillLightIntensity = 0.25f,
                ambientColor = new Color(0.42f, 0.48f, 0.50f),
                skyColor = new Color(0.48f, 0.62f, 0.82f),
                fogColor = new Color(0.62f, 0.68f, 0.76f),
                fogDensity = 0.0012f, // Light atmospheric haze

                defaultWeather = WeatherType.Clear,
                placeVillage = true,
                placeDefensiveLines = true,
                placeNaturalCover = true,
            };
        }

        // ==================== FOREST ====================
        private static BattleTerrainProfile CreateForest()
        {
            return new BattleTerrainProfile
            {
                biome = ProvinceTerrainType.Forest,
                displayName = "Forêt",
                terrainScale = 1000f,
                octaves = 6,
                persistence = 0.45f,
                terrainHeightMax = 40f,
                battleAreaRadius = 0.22f,
                flattenCenter = true,
                flattenBaseHeight = 0.10f,

                primaryGroundColor = new Color(0.15f, 0.30f, 0.08f),
                secondaryGroundColor = new Color(0.20f, 0.28f, 0.12f),
                dirtColor = new Color(0.25f, 0.20f, 0.14f),
                rockColor = new Color(0.35f, 0.34f, 0.30f),
                specialColor = new Color(0.18f, 0.22f, 0.12f),
                specialSmoothness = 0.4f,

                treeCount = 3000,
                forestDensity = 15f,
                treeColor1 = new Color(0.14f, 0.35f, 0.10f),
                treeColor2 = new Color(0.10f, 0.28f, 0.08f),
                treeHeight1 = 9f, treeHeight2 = 12f,
                treeWidth1 = 5f, treeWidth2 = 3.5f,
                treeName1 = "Oak", treeName2 = "Pine",

                grassHealthy = new Color(0.18f, 0.38f, 0.10f),
                grassDry = new Color(0.30f, 0.32f, 0.15f),
                grassDensityMult = 0.7f,
                grassMaxHeight = 0.6f,

                sunColor = new Color(0.95f, 0.92f, 0.80f),
                sunIntensity = 1.2f,
                sunRotation = new Vector3(40f, -20f, 0f),
                fillLightColor = new Color(0.45f, 0.55f, 0.40f),
                fillLightIntensity = 0.35f,
                ambientColor = new Color(0.30f, 0.38f, 0.28f),
                skyColor = new Color(0.40f, 0.55f, 0.45f),
                fogColor = new Color(0.35f, 0.42f, 0.35f),
                fogDensity = 0.003f,

                defaultWeather = WeatherType.Cloudy,
                placeVillage = true,
                placeDefensiveLines = true,
                placeNaturalCover = true,
            };
        }

        // ==================== HILLS ====================
        private static BattleTerrainProfile CreateHills()
        {
            return new BattleTerrainProfile
            {
                biome = ProvinceTerrainType.Hills,
                displayName = "Collines",
                terrainScale = 900f,
                octaves = 6,
                persistence = 0.50f,
                terrainHeightMax = 55f,
                battleAreaRadius = 0.22f,
                flattenCenter = false,
                flattenBaseHeight = 0.12f,

                primaryGroundColor = new Color(0.25f, 0.38f, 0.15f),
                secondaryGroundColor = new Color(0.48f, 0.44f, 0.28f),
                dirtColor = new Color(0.38f, 0.30f, 0.22f),
                rockColor = new Color(0.48f, 0.46f, 0.42f),
                specialColor = new Color(0.30f, 0.25f, 0.18f),
                specialSmoothness = 0.2f,

                treeCount = 1200,
                forestDensity = 28f,
                treeColor1 = new Color(0.18f, 0.36f, 0.12f),
                treeColor2 = new Color(0.14f, 0.30f, 0.10f),
                treeHeight1 = 6f, treeHeight2 = 8f,
                treeWidth1 = 4f, treeWidth2 = 3f,
                treeName1 = "Birch", treeName2 = "Pine",

                grassHealthy = new Color(0.26f, 0.42f, 0.16f),
                grassDry = new Color(0.50f, 0.46f, 0.24f),
                grassDensityMult = 1.0f,
                grassMaxHeight = 0.8f,

                sunColor = new Color(1f, 0.95f, 0.86f),
                sunIntensity = 1.5f,
                sunRotation = new Vector3(45f, -25f, 0f),
                fillLightColor = new Color(0.55f, 0.65f, 0.80f),
                fillLightIntensity = 0.3f,
                ambientColor = new Color(0.40f, 0.45f, 0.50f),
                skyColor = new Color(0.50f, 0.65f, 0.85f),

                defaultWeather = WeatherType.Clear,
                placeVillage = true,
                placeDefensiveLines = true,
                placeNaturalCover = true,
            };
        }

        // ==================== MOUNTAINS ====================
        private static BattleTerrainProfile CreateMountains()
        {
            return new BattleTerrainProfile
            {
                biome = ProvinceTerrainType.Mountains,
                displayName = "Montagnes",
                terrainScale = 700f,
                octaves = 7,
                persistence = 0.55f,
                lacunarity = 2.4f,
                terrainHeightMax = 80f,
                battleAreaRadius = 0.20f,
                flattenCenter = false,
                flattenBaseHeight = 0.15f,

                primaryGroundColor = new Color(0.28f, 0.35f, 0.18f),
                secondaryGroundColor = new Color(0.40f, 0.38f, 0.30f),
                dirtColor = new Color(0.35f, 0.30f, 0.25f),
                rockColor = new Color(0.52f, 0.50f, 0.48f),
                specialColor = new Color(0.55f, 0.55f, 0.52f),
                specialSmoothness = 0.15f,

                treeCount = 600,
                forestDensity = 40f,
                treeColor1 = new Color(0.12f, 0.28f, 0.08f),
                treeColor2 = new Color(0.10f, 0.25f, 0.07f),
                treeHeight1 = 5f, treeHeight2 = 8f,
                treeWidth1 = 3f, treeWidth2 = 2.5f,
                treeName1 = "SpruceMtn", treeName2 = "PineMtn",

                grassHealthy = new Color(0.22f, 0.35f, 0.14f),
                grassDry = new Color(0.40f, 0.38f, 0.22f),
                grassDensityMult = 0.5f,
                grassMaxHeight = 0.5f,

                sunColor = new Color(1f, 0.98f, 0.95f),
                sunIntensity = 1.6f,
                sunRotation = new Vector3(35f, -40f, 0f),
                fillLightColor = new Color(0.50f, 0.55f, 0.70f),
                fillLightIntensity = 0.25f,
                ambientColor = new Color(0.35f, 0.38f, 0.45f),
                skyColor = new Color(0.45f, 0.58f, 0.78f),
                fogColor = new Color(0.55f, 0.60f, 0.68f),
                fogDensity = 0.005f,

                defaultWeather = WeatherType.Fog,
                placeVillage = false,
                placeDefensiveLines = true,
                placeNaturalCover = true,
            };
        }

        // ==================== DESERT ====================
        private static BattleTerrainProfile CreateDesert()
        {
            return new BattleTerrainProfile
            {
                biome = ProvinceTerrainType.Desert,
                displayName = "Désert",
                terrainScale = 1400f,
                octaves = 4,
                persistence = 0.30f,
                terrainHeightMax = 25f,
                battleAreaRadius = 0.28f,
                flattenCenter = true,
                flattenBaseHeight = 0.06f,

                primaryGroundColor = new Color(0.72f, 0.60f, 0.38f),
                secondaryGroundColor = new Color(0.65f, 0.55f, 0.35f),
                dirtColor = new Color(0.58f, 0.48f, 0.30f),
                rockColor = new Color(0.60f, 0.55f, 0.45f),
                specialColor = new Color(0.78f, 0.68f, 0.45f),
                specialSmoothness = 0.1f,

                treeCount = 50,
                forestDensity = 100f,
                treeColor1 = new Color(0.25f, 0.40f, 0.15f),
                treeColor2 = new Color(0.20f, 0.35f, 0.12f),
                treeHeight1 = 6f, treeHeight2 = 5f,
                treeWidth1 = 4f, treeWidth2 = 3f,
                treeName1 = "Palm", treeName2 = "DatePalm",

                grassHealthy = new Color(0.45f, 0.42f, 0.25f),
                grassDry = new Color(0.60f, 0.52f, 0.30f),
                grassDensityMult = 0.1f,
                grassMaxHeight = 0.3f,

                sunColor = new Color(1f, 0.92f, 0.75f),
                sunIntensity = 2.0f,
                sunRotation = new Vector3(60f, -15f, 0f),
                fillLightColor = new Color(0.75f, 0.65f, 0.50f),
                fillLightIntensity = 0.4f,
                ambientColor = new Color(0.55f, 0.50f, 0.40f),
                skyColor = new Color(0.70f, 0.78f, 0.90f),

                defaultWeather = WeatherType.Clear,
                placeVillage = false,
                placeDefensiveLines = true,
                placeNaturalCover = true,
                placeBiomeSpecificAssets = true,
            };
        }

        // ==================== SNOW ====================
        private static BattleTerrainProfile CreateSnow()
        {
            return new BattleTerrainProfile
            {
                biome = ProvinceTerrainType.Snow,
                displayName = "Neige",
                terrainScale = 1100f,
                octaves = 5,
                persistence = 0.40f,
                terrainHeightMax = 35f,
                battleAreaRadius = 0.25f,
                flattenCenter = true,
                flattenBaseHeight = 0.09f,

                primaryGroundColor = new Color(0.85f, 0.88f, 0.92f),
                secondaryGroundColor = new Color(0.75f, 0.78f, 0.82f),
                dirtColor = new Color(0.40f, 0.38f, 0.35f),
                rockColor = new Color(0.50f, 0.50f, 0.52f),
                specialColor = new Color(0.92f, 0.94f, 0.97f),
                specialSmoothness = 0.6f,

                treeCount = 1000,
                forestDensity = 22f,
                treeColor1 = new Color(0.20f, 0.35f, 0.18f),
                treeColor2 = new Color(0.15f, 0.28f, 0.14f),
                treeHeight1 = 8f, treeHeight2 = 11f,
                treeWidth1 = 3.5f, treeWidth2 = 3f,
                treeName1 = "SnowSpruce", treeName2 = "SnowPine",

                grassHealthy = new Color(0.70f, 0.75f, 0.72f),
                grassDry = new Color(0.80f, 0.82f, 0.78f),
                grassDensityMult = 0.3f,
                grassMaxHeight = 0.4f,

                sunColor = new Color(0.90f, 0.92f, 1.0f),
                sunIntensity = 1.3f,
                sunRotation = new Vector3(25f, -35f, 0f),
                fillLightColor = new Color(0.55f, 0.60f, 0.75f),
                fillLightIntensity = 0.35f,
                ambientColor = new Color(0.50f, 0.55f, 0.65f),
                skyColor = new Color(0.60f, 0.65f, 0.75f),
                fogColor = new Color(0.75f, 0.78f, 0.82f),
                fogDensity = 0.002f,

                defaultWeather = WeatherType.Snow,
                placeVillage = true,
                placeDefensiveLines = true,
                placeNaturalCover = true,
                placeBiomeSpecificAssets = true,
            };
        }

        // ==================== COASTAL ====================
        private static BattleTerrainProfile CreateCoastal()
        {
            return new BattleTerrainProfile
            {
                biome = ProvinceTerrainType.Coastal,
                displayName = "Côte",
                terrainScale = 1000f,
                octaves = 5,
                persistence = 0.42f,
                terrainHeightMax = 40f,
                battleAreaRadius = 0.25f,
                flattenCenter = true,
                flattenBaseHeight = 0.07f,

                primaryGroundColor = new Color(0.25f, 0.42f, 0.15f),
                secondaryGroundColor = new Color(0.60f, 0.55f, 0.40f),
                dirtColor = new Color(0.55f, 0.48f, 0.35f),
                rockColor = new Color(0.50f, 0.48f, 0.44f),
                specialColor = new Color(0.65f, 0.60f, 0.45f),
                specialSmoothness = 0.4f,

                treeCount = 500,
                forestDensity = 40f,
                treeColor1 = new Color(0.18f, 0.38f, 0.14f),
                treeColor2 = new Color(0.14f, 0.30f, 0.10f),
                treeHeight1 = 5f, treeHeight2 = 7f,
                treeWidth1 = 4f, treeWidth2 = 3f,
                treeName1 = "CoastOak", treeName2 = "CoastPine",

                grassHealthy = new Color(0.30f, 0.45f, 0.20f),
                grassDry = new Color(0.55f, 0.50f, 0.30f),
                grassDensityMult = 0.8f,
                grassMaxHeight = 0.7f,

                sunColor = new Color(1f, 0.96f, 0.90f),
                sunIntensity = 1.6f,
                sunRotation = new Vector3(48f, -20f, 0f),
                fillLightColor = new Color(0.55f, 0.65f, 0.80f),
                fillLightIntensity = 0.35f,
                ambientColor = new Color(0.42f, 0.50f, 0.58f),
                skyColor = new Color(0.45f, 0.60f, 0.82f),

                defaultWeather = WeatherType.Cloudy,
                placeVillage = true,
                placeDefensiveLines = true,
                placeNaturalCover = true,
                placeBiomeSpecificAssets = true,
            };
        }

        // ==================== MARSH ====================
        private static BattleTerrainProfile CreateMarsh()
        {
            return new BattleTerrainProfile
            {
                biome = ProvinceTerrainType.Marsh,
                displayName = "Marécage",
                terrainScale = 1500f,
                octaves = 4,
                persistence = 0.25f,
                terrainHeightMax = 15f,
                battleAreaRadius = 0.28f,
                flattenCenter = true,
                flattenBaseHeight = 0.04f,

                primaryGroundColor = new Color(0.18f, 0.28f, 0.12f),
                secondaryGroundColor = new Color(0.25f, 0.30f, 0.18f),
                dirtColor = new Color(0.22f, 0.20f, 0.15f),
                rockColor = new Color(0.30f, 0.30f, 0.28f),
                specialColor = new Color(0.15f, 0.18f, 0.12f),
                specialSmoothness = 0.7f,

                treeCount = 400,
                forestDensity = 50f,
                treeColor1 = new Color(0.15f, 0.30f, 0.10f),
                treeColor2 = new Color(0.12f, 0.25f, 0.08f),
                treeHeight1 = 5f, treeHeight2 = 4f,
                treeWidth1 = 6f, treeWidth2 = 5f,
                treeName1 = "Willow", treeName2 = "Alder",

                grassHealthy = new Color(0.20f, 0.35f, 0.12f),
                grassDry = new Color(0.30f, 0.32f, 0.18f),
                grassDensityMult = 1.5f,
                grassMaxHeight = 1.2f,

                sunColor = new Color(0.85f, 0.88f, 0.82f),
                sunIntensity = 1.0f,
                sunRotation = new Vector3(30f, -25f, 0f),
                fillLightColor = new Color(0.40f, 0.50f, 0.40f),
                fillLightIntensity = 0.3f,
                ambientColor = new Color(0.30f, 0.35f, 0.28f),
                skyColor = new Color(0.35f, 0.42f, 0.38f),
                fogColor = new Color(0.40f, 0.45f, 0.40f),
                fogDensity = 0.008f,

                defaultWeather = WeatherType.Fog,
                placeVillage = false,
                placeDefensiveLines = false,
                placeNaturalCover = true,
                placeBiomeSpecificAssets = true,
            };
        }

        // ==================== URBAN ====================
        private static BattleTerrainProfile CreateUrban()
        {
            return new BattleTerrainProfile
            {
                biome = ProvinceTerrainType.Urban,
                displayName = "Ville",
                terrainScale = 2000f,
                octaves = 3,
                persistence = 0.20f,
                terrainHeightMax = 15f,
                battleAreaRadius = 0.35f,
                flattenCenter = true,
                flattenBaseHeight = 0.05f,

                primaryGroundColor = new Color(0.35f, 0.33f, 0.30f),
                secondaryGroundColor = new Color(0.40f, 0.38f, 0.35f),
                dirtColor = new Color(0.42f, 0.40f, 0.38f),
                rockColor = new Color(0.48f, 0.47f, 0.45f),
                specialColor = new Color(0.30f, 0.28f, 0.25f),
                specialSmoothness = 0.3f,

                treeCount = 100,
                forestDensity = 80f,
                treeColor1 = new Color(0.18f, 0.35f, 0.12f),
                treeColor2 = new Color(0.15f, 0.30f, 0.10f),
                treeHeight1 = 6f, treeHeight2 = 5f,
                treeWidth1 = 4f, treeWidth2 = 3f,
                treeName1 = "CityTree", treeName2 = "CityElm",

                grassHealthy = new Color(0.30f, 0.40f, 0.20f),
                grassDry = new Color(0.42f, 0.40f, 0.28f),
                grassDensityMult = 0.15f,
                grassMaxHeight = 0.3f,

                sunColor = new Color(1f, 0.95f, 0.85f),
                sunIntensity = 1.4f,
                sunRotation = new Vector3(42f, -30f, 0f),
                fillLightColor = new Color(0.58f, 0.62f, 0.70f),
                fillLightIntensity = 0.35f,
                ambientColor = new Color(0.40f, 0.42f, 0.45f),
                skyColor = new Color(0.48f, 0.55f, 0.68f),

                defaultWeather = WeatherType.Clear,
                placeVillage = false,
                placeDefensiveLines = true,
                placeNaturalCover = false,
                placeBiomeSpecificAssets = true,
            };
        }

        // ============================================================
        // SEASONAL ADJUSTMENT
        // ============================================================

        /// <summary>Modify profile based on season (call after GetProfile)</summary>
        public void ApplySeasonOverride(string season)
        {
            if (season == "Hiver" && biome != ProvinceTerrainType.Desert)
            {
                // Whiten everything, add snow tint
                primaryGroundColor = Color.Lerp(primaryGroundColor, new Color(0.82f, 0.85f, 0.90f), 0.5f);
                secondaryGroundColor = Color.Lerp(secondaryGroundColor, new Color(0.75f, 0.78f, 0.82f), 0.4f);
                specialColor = new Color(0.90f, 0.92f, 0.95f);
                specialSmoothness = 0.5f;
                grassHealthy = Color.Lerp(grassHealthy, new Color(0.70f, 0.72f, 0.68f), 0.6f);
                grassDry = Color.Lerp(grassDry, new Color(0.78f, 0.80f, 0.76f), 0.6f);
                grassDensityMult *= 0.3f;
                sunColor = new Color(0.88f, 0.90f, 0.98f);
                sunIntensity *= 0.85f;
                ambientColor = Color.Lerp(ambientColor, new Color(0.48f, 0.52f, 0.60f), 0.5f);
                skyColor = Color.Lerp(skyColor, new Color(0.55f, 0.60f, 0.70f), 0.5f);
                fogDensity = Mathf.Max(fogDensity, 0.002f);
                defaultWeather = WeatherType.Snow;
                treeColor1 = Color.Lerp(treeColor1, new Color(0.30f, 0.40f, 0.30f), 0.3f);
                treeColor2 = Color.Lerp(treeColor2, new Color(0.25f, 0.35f, 0.25f), 0.3f);
            }
            else if (season == "Été" && biome != ProvinceTerrainType.Snow)
            {
                // Summer: lush and warm but still GREEN
                sunIntensity *= 1.1f;
                sunColor = Color.Lerp(sunColor, new Color(1f, 0.96f, 0.85f), 0.2f);
                // Keep greens vibrant — don't dry out
            }
            else if (season == "Automne")
            {
                // Amber/brown tints
                primaryGroundColor = Color.Lerp(primaryGroundColor, new Color(0.40f, 0.35f, 0.18f), 0.25f);
                treeColor1 = Color.Lerp(treeColor1, new Color(0.55f, 0.35f, 0.12f), 0.4f);
                treeColor2 = Color.Lerp(treeColor2, new Color(0.45f, 0.30f, 0.10f), 0.3f);
                grassHealthy = Color.Lerp(grassHealthy, new Color(0.42f, 0.38f, 0.18f), 0.3f);
                fogDensity = Mathf.Max(fogDensity, 0.001f);
            }
        }
    }
}
