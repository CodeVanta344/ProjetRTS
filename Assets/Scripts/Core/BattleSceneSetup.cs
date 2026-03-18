using UnityEngine;
using UnityEngine.EventSystems;
using NapoleonicWars.AI;
using NapoleonicWars.Battlefield;

namespace NapoleonicWars.Core
{
    /// <summary>
    /// Attach this to an empty GameObject in the Battle scene.
    /// It creates the entire scene programmatically so no manual Unity editor setup is needed.
    /// </summary>
    public class BattleSceneSetup : MonoBehaviour
    {
        [Header("Terrain")]
        [SerializeField] private float terrainSize = 500f;
        [SerializeField] private Color skyColor = new Color(0.5f, 0.65f, 0.85f);
        [SerializeField] private bool useAdvancedTerrain = true;

        private BattleTerrainConfigurator terrainConfigurator;

        private void Awake()
        {
            CreateConfigurator();
            CreateTerrain();
            CreateLighting();
            CreateCamera();
            CreateManagers();
        }

        private void CreateConfigurator()
        {
            if (BattleTerrainConfigurator.Instance == null)
            {
                GameObject confGO = new GameObject("BattleTerrainConfigurator");
                terrainConfigurator = confGO.AddComponent<BattleTerrainConfigurator>();
            }
            else
            {
                terrainConfigurator = BattleTerrainConfigurator.Instance;
            }
        }

        private void CreateTerrain()
        {
            if (useAdvancedTerrain)
            {
                // Use the new procedural terrain generator
                GameObject tgenGO = new GameObject("TerrainGenerator");
                TerrainGenerator tgen = tgenGO.AddComponent<TerrainGenerator>();

                // Apply biome profile before generation
                if (terrainConfigurator != null)
                    terrainConfigurator.ConfigureTerrainGenerator(tgen);

                tgen.GenerateBattlefieldTerrain();
            }
            else
            {
                // Fallback: simple ground plane
                GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Terrain";
                ground.transform.position = Vector3.zero;
                ground.transform.localScale = new Vector3(terrainSize / 10f, 1f, terrainSize / 10f);

                Renderer groundRenderer = ground.GetComponent<Renderer>();
                groundRenderer.material = URPMaterialHelper.CreateLit(new Color(0.35f, 0.55f, 0.2f));
            }
        }

        private void CreateLighting()
        {
            var profile = terrainConfigurator?.ActiveProfile;

            // ============================================================
            // SUN — warm golden light, dramatic shadows
            // ============================================================
            GameObject sunGO = new GameObject("Sun");
            Light sun = sunGO.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = profile != null ? profile.sunColor : new Color(1f, 0.94f, 0.82f);
            sun.intensity = profile != null ? profile.sunIntensity : 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.65f;
            sun.shadowBias = 0.015f;
            sun.shadowNormalBias = 0.35f;
            sun.shadowResolution = UnityEngine.Rendering.LightShadowResolution.High;
            sunGO.transform.rotation = profile != null
                ? Quaternion.Euler(profile.sunRotation)
                : Quaternion.Euler(35f, -40f, 0f);

            // ============================================================
            // FILL LIGHT — cool sky bounce from opposite side
            // ============================================================
            GameObject fillGO = new GameObject("FillLight");
            Light fill = fillGO.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = profile != null ? profile.fillLightColor : new Color(0.55f, 0.65f, 0.80f);
            fill.intensity = profile != null ? profile.fillLightIntensity : 0.25f;
            fill.shadows = LightShadows.None;
            fillGO.transform.rotation = Quaternion.Euler(45f, 140f, 0f);

            // ============================================================
            // AMBIENT — trilight for natural outdoor feeling
            // ============================================================
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            Color baseAmbient = profile != null ? profile.ambientColor : new Color(0.42f, 0.47f, 0.50f);
            RenderSettings.ambientSkyColor = new Color(
                baseAmbient.r * 0.9f, baseAmbient.g * 1.1f, baseAmbient.b * 1.4f
            );
            RenderSettings.ambientEquatorColor = new Color(
                baseAmbient.r * 1.1f, baseAmbient.g * 1.15f, baseAmbient.b * 0.9f
            );
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.22f, 0.18f);

            // ============================================================
            // SKY COLOR
            // ============================================================
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = profile != null ? profile.skyColor : new Color(0.48f, 0.62f, 0.82f);
            }

            // ============================================================
            // ATMOSPHERIC FOG
            // ============================================================
            Color fogColor;
            float fogDensity;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;

            if (profile != null && profile.fogDensity > 0f)
            {
                fogDensity = profile.fogDensity;
                fogColor = profile.fogColor;
            }
            else
            {
                fogDensity = 0.0012f;
                fogColor = new Color(0.62f, 0.68f, 0.76f);
            }
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogColor = fogColor;

            // ============================================================
            // WEATHER SYSTEM — pass biome baselines for proper blending
            // ============================================================
            if (WeatherSystem.Instance != null)
            {
                Color sunCol = profile != null ? profile.sunColor : new Color(1f, 0.94f, 0.82f);
                float sunInt = profile != null ? profile.sunIntensity : 1.3f;
                Color skyCol = profile != null ? profile.skyColor : new Color(0.48f, 0.62f, 0.82f);

                WeatherSystem.Instance.SetProfileBaseline(sunCol, sunInt, baseAmbient, skyCol, fogColor, fogDensity);

                // Set biome default weather
                if (profile != null)
                    WeatherSystem.Instance.SetWeatherImmediate(profile.defaultWeather);

                // Determine battle start time from campaign (default: morning battle)
                float startHour = DetermineBattleStartTime();
                WeatherSystem.Instance.SetStartTime(startHour);
            }

            Debug.Log($"[BattleSceneSetup] Lighting: sun={sun.intensity}, fog={fogDensity:F4}, ambient=Trilight");
        }

        /// <summary>
        /// Determine battle start time from campaign season.
        /// Most Napoleonic battles started at dawn or morning.
        /// </summary>
        private float DetermineBattleStartTime()
        {
            try
            {
                var season = NapoleonicWars.Campaign.SeasonSystem.CurrentSeason;
                switch (season)
                {
                    case NapoleonicWars.Campaign.Season.Winter:
                        return Random.Range(8f, 10f); // Short days, late start
                    case NapoleonicWars.Campaign.Season.Summer:
                        return Random.Range(5.5f, 9f); // Long days, can start at dawn
                    case NapoleonicWars.Campaign.Season.Autumn:
                        return Random.Range(7f, 10f);
                    case NapoleonicWars.Campaign.Season.Spring:
                    default:
                        return Random.Range(6.5f, 10f);
                }
            }
            catch
            {
                // Campaign not initialized — default morning battle
                return Random.Range(7f, 11f);
            }
        }

        private void CreateCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camGO = new GameObject("Main Camera");
                camGO.tag = "MainCamera";
                cam = camGO.AddComponent<Camera>();
                camGO.AddComponent<AudioListener>();
            }

            // Position camera above the battlefield center, accounting for terrain height
            float terrainY = BattleManager.GetTerrainHeight(Vector3.zero);
            cam.transform.position = new Vector3(0f, terrainY + 50f, -60f);
            cam.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = terrainConfigurator?.ActiveProfile?.skyColor ?? skyColor;
            cam.farClipPlane = 2000f;
            cam.nearClipPlane = 0.3f;

            // Add camera controller
            if (cam.GetComponent<CameraController>() == null)
                cam.gameObject.AddComponent<CameraController>();

            // Add URP camera data if available
            var urpCamData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (urpCamData == null)
                urpCamData = cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            urpCamData.renderPostProcessing = true;
        }

        private void CreateManagers()
        {
            // GameManager
            if (GameManager.Instance == null)
            {
                GameObject gmGO = new GameObject("GameManager");
                gmGO.AddComponent<GameManager>();
            }

            // InputManager
            if (InputManager.Instance == null)
            {
                GameObject imGO = new GameObject("InputManager");
                imGO.AddComponent<InputManager>();
            }

            // SelectionManager
            if (SelectionManager.Instance == null)
            {
                GameObject smGO = new GameObject("SelectionManager");
                smGO.AddComponent<SelectionManager>();
            }

            // Performance systems
            if (SpatialGrid.Instance == null)
            {
                GameObject sgGO = new GameObject("SpatialGrid");
                sgGO.AddComponent<SpatialGrid>();
            }

            if (ObjectPool.Instance == null)
            {
                GameObject opGO = new GameObject("ObjectPool");
                opGO.AddComponent<ObjectPool>();
            }

            if (UnitLODManager.Instance == null)
            {
                GameObject lodGO = new GameObject("UnitLODManager");
                lodGO.AddComponent<UnitLODManager>();
            }

            // URP Post-Processing
            if (URPSetup.Instance == null)
            {
                GameObject urpGO = new GameObject("URPSetup");
                urpGO.AddComponent<URPSetup>();
            }

            // Weather System
            if (WeatherSystem.Instance == null)
            {
                GameObject weatherGO = new GameObject("WeatherSystem");
                weatherGO.AddComponent<WeatherSystem>();
            }

            // Fog of War
            if (FogOfWar.Instance == null)
            {
                GameObject fowGO = new GameObject("FogOfWar");
                fowGO.AddComponent<FogOfWar>();
            }

            // Advanced Particles
            if (AdvancedParticleManager.Instance == null)
            {
                GameObject particleGO = new GameObject("AdvancedParticleManager");
                particleGO.AddComponent<AdvancedParticleManager>();
            }

            // Camera Shake (screen shake, bloom pulse, vignette flash on impacts)
            if (CameraShake.Instance == null)
            {
                GameObject shakeGO = new GameObject("CameraShake");
                shakeGO.AddComponent<CameraShake>();
            }

            // UnitModelLoader (loads 3D models or falls back to primitives)
            if (NapoleonicWars.Units.UnitModelLoader.Instance == null)
            {
                GameObject umlGO = new GameObject("UnitModelLoader");
                umlGO.AddComponent<NapoleonicWars.Units.UnitModelLoader>();
            }

            // DifficultySettings (damage/accuracy multipliers per team, AI behavior flags)
            if (DifficultySettings.Instance == null)
            {
                GameObject dsGO = new GameObject("DifficultySettings");
                dsGO.AddComponent<DifficultySettings>();
            }

            // UnitTechnologySystem (caches tech bonuses per faction, avoids per-frame lookups)
            if (NapoleonicWars.Units.UnitTechnologySystem.Instance == null)
            {
                GameObject utsGO = new GameObject("UnitTechnologySystem");
                utsGO.AddComponent<NapoleonicWars.Units.UnitTechnologySystem>();
            }

            // BattleManager
            if (BattleManager.Instance == null)
            {
                GameObject bmGO = new GameObject("BattleManager");
                bmGO.AddComponent<BattleManager>();
            }

            // DeploymentUI (for pre-battle deployment phase)
            if (FindAnyObjectByType<NapoleonicWars.UI.DeploymentUI>() == null)
            {
                GameObject depGO = new GameObject("DeploymentUI");
                depGO.AddComponent<NapoleonicWars.UI.DeploymentUI>();
            }

            // BattleHUD
            GameObject hudGO = new GameObject("BattleHUD");
            hudGO.AddComponent<NapoleonicWars.UI.BattleHUD>();

            // Enemy AI
            GameObject aiGO = new GameObject("EnemyAI");
            aiGO.AddComponent<EnemyAIController>();

            // Minimap
            GameObject minimapGO = new GameObject("Minimap");
            minimapGO.AddComponent<NapoleonicWars.UI.Minimap>();

            // Audio
            if (AudioManager.Instance == null)
            {
                GameObject audioGO = new GameObject("AudioManager");
                audioGO.AddComponent<AudioManager>();
            }

            // Battle Sound Manager
            if (BattleSoundManager.Instance == null)
            {
                GameObject bsmGO = new GameObject("BattleSoundManager");
                bsmGO.AddComponent<BattleSoundManager>();
            }

            // Battle Statistics
            if (BattleStatistics.Instance == null)
            {
                GameObject bsGO = new GameObject("BattleStatistics");
                bsGO.AddComponent<BattleStatistics>();
            }

            // Battle Result UI
            if (FindAnyObjectByType<NapoleonicWars.UI.BattleResultUI>() == null)
            {
                GameObject brGO = new GameObject("BattleResultUI");
                brGO.AddComponent<NapoleonicWars.UI.BattleResultUI>();
            }

            // Pause Menu
            if (FindAnyObjectByType<NapoleonicWars.UI.PauseMenuUI>() == null)
            {
                GameObject pmGO = new GameObject("PauseMenuUI");
                pmGO.AddComponent<NapoleonicWars.UI.PauseMenuUI>();
            }

            // Unit Tooltip
            if (FindAnyObjectByType<NapoleonicWars.UI.UnitTooltipUI>() == null)
            {
                GameObject ttGO = new GameObject("UnitTooltipUI");
                ttGO.AddComponent<NapoleonicWars.UI.UnitTooltipUI>();
            }

            // Order Feedback
            if (NapoleonicWars.UI.OrderFeedback.Instance == null)
            {
                GameObject ofGO = new GameObject("OrderFeedback");
                ofGO.AddComponent<NapoleonicWars.UI.OrderFeedback>();
            }

            // Battle Log
            if (NapoleonicWars.UI.BattleLogUI.Instance == null)
            {
                GameObject blGO = new GameObject("BattleLogUI");
                blGO.AddComponent<NapoleonicWars.UI.BattleLogUI>();
            }

            // Smoke Cloud System (for accumulating gunpowder smoke)
            if (NapoleonicWars.Units.SmokeCloudSystem.Instance == null)
            {
                GameObject smokeGO = new GameObject("SmokeCloudSystem");
                smokeGO.AddComponent<NapoleonicWars.Units.SmokeCloudSystem>();
            }

            // Battlefield Asset Placer (procedural 3D assets: trenches, buildings, cover)
            if (FindAnyObjectByType<NapoleonicWars.Battlefield.BattleAssetPlacer>() == null)
            {
                GameObject assetsGO = new GameObject("BattleAssetPlacer");
                var placer = assetsGO.AddComponent<NapoleonicWars.Battlefield.BattleAssetPlacer>();
                if (terrainConfigurator != null)
                    placer.ActiveProfile = terrainConfigurator.ActiveProfile;
            }

            // Deployment Boundary (separates player and enemy deployment zones)
            if (FindAnyObjectByType<DeploymentBoundary>() == null)
            {
                GameObject boundaryGO = new GameObject("DeploymentBoundary");
                boundaryGO.AddComponent<DeploymentBoundary>();
            }

            // Cover System (for cover bonus and positioning)
            if (FindAnyObjectByType<NapoleonicWars.Battlefield.CoverSystem>() == null)
            {
                GameObject coverGO = new GameObject("CoverSystem");
                coverGO.AddComponent<NapoleonicWars.Battlefield.CoverSystem>();
            }

            // Cover Positioning (for "take cover" command with preview)
            if (FindAnyObjectByType<NapoleonicWars.Battlefield.CoverPositioning>() == null)
            {
                GameObject coverPosGO = new GameObject("CoverPositioning");
                coverPosGO.AddComponent<NapoleonicWars.Battlefield.CoverPositioning>();
            }

            // Projectile System (for bullet tracers)
            if (NapoleonicWars.Units.ProjectileSystem.Instance == null)
            {
                GameObject projGO = new GameObject("ProjectileSystem");
                projGO.AddComponent<NapoleonicWars.Units.ProjectileSystem>();
            }

            // EventSystem (required for Canvas UI interaction)
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
            }
        }
    }
}
