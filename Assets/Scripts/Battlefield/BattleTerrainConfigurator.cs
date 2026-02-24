using UnityEngine;
using NapoleonicWars.Campaign;
using NapoleonicWars.Core;

namespace NapoleonicWars.Battlefield
{
    /// <summary>
    /// Reads campaign battle context from PlayerPrefs and configures
    /// TerrainGenerator, BattleAssetPlacer, lighting, and weather accordingly.
    /// Instantiated by BattleSceneSetup before terrain generation.
    /// </summary>
    public class BattleTerrainConfigurator : MonoBehaviour
    {
        public static BattleTerrainConfigurator Instance { get; private set; }

        public BattleTerrainProfile ActiveProfile { get; private set; }
        public ProvinceTerrainType TerrainType { get; private set; }
        public string Season { get; private set; }
        public bool IsSiege { get; private set; }
        public bool HasFortress { get; private set; }
        public int FortificationLevel { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            ReadBattleContext();
        }

        // ============================================================
        // READ CONTEXT FROM PLAYERPREFS
        // ============================================================

        private void ReadBattleContext()
        {
            TerrainType = (ProvinceTerrainType)PlayerPrefs.GetInt("Battle_TerrainType", 0);
            Season = PlayerPrefs.GetString("Battle_Season", "Été");
            IsSiege = PlayerPrefs.GetInt("Battle_IsSiege", 0) == 1;
            HasFortress = PlayerPrefs.GetInt("Battle_HasFortress", 0) == 1;
            FortificationLevel = PlayerPrefs.GetInt("Battle_FortificationLevel", 0);

            // Build profile
            ActiveProfile = BattleTerrainProfile.GetProfile(TerrainType);
            ActiveProfile.ApplySeasonOverride(Season);

            // Siege override: more defensive structures
            if (IsSiege && FortificationLevel > 0)
            {
                ActiveProfile.placeDefensiveLines = true;
            }

            Debug.Log($"[BattleTerrainConfigurator] Biome={TerrainType} ({ActiveProfile.displayName}), " +
                      $"Season={Season}, Siege={IsSiege}, Fort={FortificationLevel}");
        }

        // ============================================================
        // CONFIGURE TERRAIN GENERATOR
        // ============================================================

        /// <summary>Apply the active profile to a TerrainGenerator before generation</summary>
        public void ConfigureTerrainGenerator(TerrainGenerator terrainGen)
        {
            if (ActiveProfile == null || terrainGen == null) return;
            terrainGen.ApplyProfile(ActiveProfile);
            Debug.Log($"[BattleTerrainConfigurator] TerrainGenerator configured for {ActiveProfile.displayName}");
        }

        // ============================================================
        // CONFIGURE LIGHTING
        // ============================================================

        /// <summary>Apply biome-appropriate lighting to the scene</summary>
        public void ConfigureLighting()
        {
            if (ActiveProfile == null) return;

            // Find or create sun
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            Light sun = null;
            Light fill = null;

            foreach (var light in lights)
            {
                if (light.type == LightType.Directional)
                {
                    if (sun == null) sun = light;
                    else if (fill == null) fill = light;
                }
            }

            if (sun != null)
            {
                sun.color = ActiveProfile.sunColor;
                sun.intensity = ActiveProfile.sunIntensity;
                sun.transform.rotation = Quaternion.Euler(ActiveProfile.sunRotation);
            }

            if (fill != null)
            {
                fill.color = ActiveProfile.fillLightColor;
                fill.intensity = ActiveProfile.fillLightIntensity;
            }

            // Ambient
            RenderSettings.ambientLight = ActiveProfile.ambientColor;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;

            // Fog
            if (ActiveProfile.fogDensity > 0f)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = ActiveProfile.fogDensity;
                RenderSettings.fogColor = ActiveProfile.fogColor;
            }
            else
            {
                RenderSettings.fog = false;
            }

            Debug.Log($"[BattleTerrainConfigurator] Lighting configured: sun={ActiveProfile.sunColor}, fog={ActiveProfile.fogDensity}");
        }

        // ============================================================
        // CONFIGURE WEATHER
        // ============================================================

        /// <summary>Set initial weather based on biome + season</summary>
        public void ConfigureWeather()
        {
            if (ActiveProfile == null) return;

            WeatherSystem weather = WeatherSystem.Instance;
            if (weather == null) return;

            weather.SetWeather(ActiveProfile.defaultWeather);
            Debug.Log($"[BattleTerrainConfigurator] Weather set to {ActiveProfile.defaultWeather}");
        }

        // ============================================================
        // CONFIGURE CAMERA SKY COLOR
        // ============================================================

        public void ConfigureSkyColor()
        {
            if (ActiveProfile == null) return;

            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.backgroundColor = ActiveProfile.skyColor;
            }
        }

        /// <summary>Get the sky color for this biome (used by BattleSceneSetup)</summary>
        public Color GetSkyColor()
        {
            return ActiveProfile?.skyColor ?? new Color(0.5f, 0.65f, 0.85f);
        }
    }
}
