using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Core
{
    public enum WeatherType
    {
        Clear,
        Cloudy,
        Rain,
        HeavyRain,
        Fog,
        Snow,
        Thunderstorm
    }

    public enum BattleTimeOfDay
    {
        Dawn,       // 5-7h
        Morning,    // 7-10h
        Midday,     // 10-14h  
        Afternoon,  // 14-17h
        Dusk,       // 17-19h
        Night       // 19-5h
    }

    public class WeatherSystem : MonoBehaviour
    {
        public static WeatherSystem Instance { get; private set; }

        [Header("Weather Settings")]
        [SerializeField] private WeatherType currentWeather = WeatherType.Clear;
        [SerializeField] private WeatherType targetWeather = WeatherType.Clear;
        [SerializeField] private float weatherChangeInterval = 180f;
        [SerializeField] private bool autoChangeWeather = false;
        [SerializeField] private float weatherTransitionSpeed = 0.4f; // Smooth blending

        [Header("Time of Day")]
        [SerializeField] private float timeOfDay = 10f; // 0-24
        [SerializeField] private float timeSpeed = 0.008f; // Slow passage of time
        [SerializeField] private bool enableDayNightCycle = true;

        [Header("Wind")]
        [SerializeField] private Vector3 windDirection = new Vector3(1f, 0f, 0.3f);
        [SerializeField] private float windIntensity = 0.5f;
        [SerializeField] private float windGustiness = 0.3f;

        // Gameplay modifiers
        public float AccuracyModifier { get; private set; } = 1f;
        public float MovementModifier { get; private set; } = 1f;
        public float MoraleModifier { get; private set; } = 1f;
        public float VisibilityRange { get; private set; } = 200f;

        public WeatherType CurrentWeather => currentWeather;
        public WeatherType TargetWeather => targetWeather;
        public float TimeOfDay => timeOfDay;
        public BattleTimeOfDay CurrentTimeOfDay => GetTimeOfDayEnum();
        public Vector3 WindDirection => windDirection.normalized;
        public float WindIntensity => windIntensity;

        private float weatherTimer;
        private ParticleSystem rainParticles;
        private ParticleSystem snowParticles;
        private ParticleSystem dustParticles;
        private Light sunLight;
        private Light fillLight;
        private Light moonLight;
        private Camera mainCamera;

        // Transition state
        private float transitionProgress = 1f; // 1 = fully transitioned
        private float currentFogDensity;
        private Color currentFogColor;
        private float currentSkyBrightness = 1f;
        private float targetFogDensity;
        private Color targetFogColor;
        private float targetSkyBrightness;

        // Lightning
        private float lightningTimer;
        private float lightningFlashTimer;
        private bool isLightningFlashing;

        // Ambient wind noise timer
        private float windGustTimer;
        private float currentGustStrength;

        // Profile reference for base colors
        private Color profileSunColor = new Color(1f, 0.95f, 0.85f);
        private float profileSunIntensity = 1.3f;
        private Color profileAmbientColor = new Color(0.4f, 0.45f, 0.5f);
        private Color profileSkyColor = new Color(0.5f, 0.65f, 0.85f);
        private Color profileFogColor = new Color(0.6f, 0.65f, 0.7f);
        private float profileFogDensity = 0.0012f;

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
            sunLight = FindSunLight();
            fillLight = FindLightByName("FillLight");
            mainCamera = Camera.main;

            CreateWeatherParticles();
            CreateMoonLight();

            // Store current fog as baseline
            currentFogDensity = RenderSettings.fogDensity;
            currentFogColor = RenderSettings.fogColor;

            ApplyWeather(currentWeather);
            weatherTimer = weatherChangeInterval;
        }

        /// <summary>
        /// Set the profile baseline colors (called by BattleSceneSetup after creating lights)
        /// </summary>
        public void SetProfileBaseline(Color sunCol, float sunInt, Color ambient, Color sky, Color fog, float fogDens)
        {
            profileSunColor = sunCol;
            profileSunIntensity = sunInt;
            profileAmbientColor = ambient;
            profileSkyColor = sky;
            profileFogColor = fog;
            profileFogDensity = fogDens;
        }

        public void SetStartTime(float hour)
        {
            timeOfDay = Mathf.Clamp(hour, 0f, 24f);
        }

        // ============================================================
        // UPDATE
        // ============================================================

        private void Update()
        {
            float dt = Time.deltaTime;

            // Day/night cycle
            if (enableDayNightCycle)
            {
                timeOfDay += timeSpeed * dt;
                if (timeOfDay >= 24f) timeOfDay -= 24f;
                UpdateDayNightLighting();
            }

            // Weather transition (smooth blend)
            if (transitionProgress < 1f)
            {
                transitionProgress += weatherTransitionSpeed * dt;
                transitionProgress = Mathf.Clamp01(transitionProgress);
                ApplyTransitionStep();
            }

            // Auto weather change
            if (autoChangeWeather)
            {
                weatherTimer -= dt;
                if (weatherTimer <= 0f)
                {
                    weatherTimer = weatherChangeInterval + Random.Range(-30f, 60f);
                    WeatherType newWeather = PickRandomWeather();
                    TransitionToWeather(newWeather);
                }
            }

            // Lightning flashes
            UpdateLightning(dt);

            // Wind gusts
            UpdateWind(dt);

            // Move particles with camera
            UpdateParticlePositions();
        }

        // ============================================================
        // DAY/NIGHT LIGHTING — Smooth gradient through the day
        // ============================================================

        private void UpdateDayNightLighting()
        {
            float h = timeOfDay;

            // === SUN ANGLE ===
            // Sun rises at 6h, sets at 20h. Below horizon = night
            float sunAngle = ((h - 6f) / 14f) * 180f; // 0° at sunrise, 180° at sunset
            if (sunLight != null)
            {
                if (h >= 5.5f && h <= 20.5f)
                {
                    sunLight.enabled = true;
                    sunLight.transform.rotation = Quaternion.Euler(sunAngle, -30f, 0f);
                }
                else
                {
                    sunLight.enabled = false;
                }
            }

            // === SUN COLOR & INTENSITY (smooth gradient) ===
            Color sunCol;
            float sunInt;
            GetSunParameters(h, out sunCol, out sunInt);

            if (sunLight != null && sunLight.enabled)
            {
                sunLight.color = sunCol;
                sunLight.intensity = sunInt;
            }

            // === FILL LIGHT — weakens at night ===
            if (fillLight != null)
            {
                float fillMult = GetDaytimeFactor(h);
                fillLight.intensity = 0.25f * fillMult;
                fillLight.color = Color.Lerp(new Color(0.15f, 0.15f, 0.3f), new Color(0.55f, 0.65f, 0.80f), fillMult);
            }

            // === MOON LIGHT — active at night ===
            if (moonLight != null)
            {
                float nightFactor = 1f - GetDaytimeFactor(h);
                moonLight.enabled = nightFactor > 0.1f;
                moonLight.intensity = 0.35f * nightFactor;
                // Moon opposite to sun
                float moonAngle = sunLight != null ? sunLight.transform.eulerAngles.x + 180f : 0f;
                moonLight.transform.rotation = Quaternion.Euler(moonAngle, 150f, 0f);
            }

            // === AMBIENT — shifts blue at night, warm during day ===
            float dayFactor = GetDaytimeFactor(h);
            Color dayAmbientSky = new Color(
                profileAmbientColor.r * 0.9f,
                profileAmbientColor.g * 1.1f,
                profileAmbientColor.b * 1.4f
            );
            Color nightAmbientSky = new Color(0.04f, 0.05f, 0.12f);
            Color dayAmbientEq = new Color(
                profileAmbientColor.r * 1.1f,
                profileAmbientColor.g * 1.15f,
                profileAmbientColor.b * 0.9f
            );
            Color nightAmbientEq = new Color(0.03f, 0.03f, 0.06f);
            Color dayAmbientGround = new Color(0.25f, 0.22f, 0.18f);
            Color nightAmbientGround = new Color(0.02f, 0.02f, 0.03f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(nightAmbientSky, dayAmbientSky, dayFactor);
            RenderSettings.ambientEquatorColor = Color.Lerp(nightAmbientEq, dayAmbientEq, dayFactor);
            RenderSettings.ambientGroundColor = Color.Lerp(nightAmbientGround, dayAmbientGround, dayFactor);

            // === SKY COLOR ===
            if (mainCamera != null)
            {
                Color daySky = profileSkyColor;
                Color dawnSky = new Color(0.75f, 0.45f, 0.30f);
                Color duskSky = new Color(0.65f, 0.35f, 0.25f);
                Color nightSky = new Color(0.02f, 0.02f, 0.06f);

                Color sky;
                if (h >= 5f && h < 7f)
                    sky = Color.Lerp(nightSky, dawnSky, (h - 5f) / 2f);
                else if (h >= 7f && h < 9f)
                    sky = Color.Lerp(dawnSky, daySky, (h - 7f) / 2f);
                else if (h >= 9f && h < 16f)
                    sky = daySky;
                else if (h >= 16f && h < 18f)
                    sky = Color.Lerp(daySky, duskSky, (h - 16f) / 2f);
                else if (h >= 18f && h < 20f)
                    sky = Color.Lerp(duskSky, nightSky, (h - 18f) / 2f);
                else
                    sky = nightSky;

                mainCamera.backgroundColor = sky;
            }

            // === FOG COLOR — darker at night ===
            Color baseFogColor = currentFogColor;
            Color nightFogColor = new Color(
                baseFogColor.r * 0.15f,
                baseFogColor.g * 0.15f,
                baseFogColor.b * 0.2f
            );
            RenderSettings.fogColor = Color.Lerp(nightFogColor, baseFogColor, dayFactor);
        }

        private void GetSunParameters(float hour, out Color color, out float intensity)
        {
            // Pre-dawn: nothing
            if (hour < 5.5f || hour > 20.5f)
            {
                color = Color.black;
                intensity = 0f;
                return;
            }

            // Dawn (5.5 - 7): deep orange → golden
            if (hour < 7f)
            {
                float t = (hour - 5.5f) / 1.5f;
                color = Color.Lerp(new Color(0.9f, 0.35f, 0.1f), new Color(1f, 0.75f, 0.4f), t);
                intensity = Mathf.Lerp(0.2f, 0.8f, t);
                return;
            }

            // Morning (7 - 9): golden → warm white
            if (hour < 9f)
            {
                float t = (hour - 7f) / 2f;
                color = Color.Lerp(new Color(1f, 0.75f, 0.4f), profileSunColor, t);
                intensity = Mathf.Lerp(0.8f, profileSunIntensity, t);
                return;
            }

            // Midday (9 - 15): full profile settings
            if (hour < 15f)
            {
                color = profileSunColor;
                intensity = profileSunIntensity;
                return;
            }

            // Afternoon (15 - 17): warm white → golden
            if (hour < 17f)
            {
                float t = (hour - 15f) / 2f;
                color = Color.Lerp(profileSunColor, new Color(1f, 0.80f, 0.45f), t);
                intensity = Mathf.Lerp(profileSunIntensity, 0.9f, t);
                return;
            }

            // Dusk (17 - 19): golden → deep red-orange
            if (hour < 19f)
            {
                float t = (hour - 17f) / 2f;
                color = Color.Lerp(new Color(1f, 0.80f, 0.45f), new Color(0.85f, 0.30f, 0.10f), t);
                intensity = Mathf.Lerp(0.9f, 0.3f, t);
                return;
            }

            // Twilight (19 - 20.5): fading
            {
                float t = (hour - 19f) / 1.5f;
                color = Color.Lerp(new Color(0.85f, 0.30f, 0.10f), Color.black, t);
                intensity = Mathf.Lerp(0.3f, 0f, t);
            }
        }

        /// <summary>Returns 0 at full night, 1 at full day, with smooth transitions</summary>
        private float GetDaytimeFactor(float hour)
        {
            if (hour >= 8f && hour <= 17f) return 1f;
            if (hour < 5f || hour > 21f) return 0f;

            if (hour < 8f) return Mathf.SmoothStep(0f, 1f, (hour - 5f) / 3f);
            return Mathf.SmoothStep(1f, 0f, (hour - 17f) / 4f);
        }

        // ============================================================
        // WEATHER TRANSITIONS
        // ============================================================

        public void SetWeather(WeatherType weather)
        {
            TransitionToWeather(weather);
            Debug.Log($"[Weather] Transitioning to {weather}");
        }

        public void SetWeatherImmediate(WeatherType weather)
        {
            currentWeather = weather;
            targetWeather = weather;
            transitionProgress = 1f;
            ApplyWeather(weather);
            Debug.Log($"[Weather] Set immediately to {weather}");
        }

        private void TransitionToWeather(WeatherType weather)
        {
            if (weather == currentWeather && transitionProgress >= 1f) return;
            targetWeather = weather;
            transitionProgress = 0f;

            // Set transition targets
            GetWeatherVisuals(weather, out targetFogDensity, out targetFogColor, out targetSkyBrightness);
            GetWeatherGameplay(weather);
        }

        private void ApplyTransitionStep()
        {
            float t = Mathf.SmoothStep(0f, 1f, transitionProgress);

            // Blend fog
            float blendedFogDensity = Mathf.Lerp(currentFogDensity, targetFogDensity, t);
            Color blendedFogColor = Color.Lerp(currentFogColor, targetFogColor, t);

            RenderSettings.fog = blendedFogDensity > 0.0005f;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = blendedFogDensity;
            RenderSettings.fogColor = blendedFogColor;

            // When transition completes, lock in the new weather
            if (transitionProgress >= 1f)
            {
                currentFogDensity = targetFogDensity;
                currentFogColor = targetFogColor;
                currentSkyBrightness = targetSkyBrightness;
                currentWeather = targetWeather;
            }
        }

        private void ApplyWeather(WeatherType weather)
        {
            GetWeatherVisuals(weather, out targetFogDensity, out targetFogColor, out targetSkyBrightness);
            currentFogDensity = targetFogDensity;
            currentFogColor = targetFogColor;
            currentSkyBrightness = targetSkyBrightness;

            RenderSettings.fog = currentFogDensity > 0.0005f;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = currentFogDensity;
            RenderSettings.fogColor = currentFogColor;

            GetWeatherGameplay(weather);

            // Particles
            if (rainParticles != null) rainParticles.Stop();
            if (snowParticles != null) snowParticles.Stop();
            if (dustParticles != null) dustParticles.Stop();

            switch (weather)
            {
                case WeatherType.Rain:
                    StartRain(800); break;
                case WeatherType.HeavyRain:
                    StartRain(3000); break;
                case WeatherType.Thunderstorm:
                    StartRain(4000);
                    lightningTimer = Random.Range(3f, 8f);
                    break;
                case WeatherType.Snow:
                    StartSnow(1000); break;
                case WeatherType.Clear:
                    if (dustParticles != null) StartDust(50); break;
            }
        }

        private void GetWeatherVisuals(WeatherType weather, out float fogDens, out Color fogCol, out float skyBright)
        {
            switch (weather)
            {
                case WeatherType.Clear:
                    fogDens = profileFogDensity;
                    fogCol = profileFogColor;
                    skyBright = 1f;
                    break;
                case WeatherType.Cloudy:
                    fogDens = Mathf.Max(profileFogDensity, 0.002f);
                    fogCol = Color.Lerp(profileFogColor, new Color(0.55f, 0.55f, 0.58f), 0.5f);
                    skyBright = 0.7f;
                    break;
                case WeatherType.Rain:
                    fogDens = 0.005f;
                    fogCol = new Color(0.45f, 0.47f, 0.52f);
                    skyBright = 0.5f;
                    break;
                case WeatherType.HeavyRain:
                    fogDens = 0.012f;
                    fogCol = new Color(0.35f, 0.37f, 0.42f);
                    skyBright = 0.3f;
                    break;
                case WeatherType.Thunderstorm:
                    fogDens = 0.015f;
                    fogCol = new Color(0.25f, 0.27f, 0.35f);
                    skyBright = 0.2f;
                    break;
                case WeatherType.Fog:
                    fogDens = 0.030f;
                    fogCol = Color.Lerp(profileFogColor, new Color(0.70f, 0.72f, 0.72f), 0.7f);
                    skyBright = 0.55f;
                    break;
                case WeatherType.Snow:
                    fogDens = 0.008f;
                    fogCol = new Color(0.80f, 0.82f, 0.88f);
                    skyBright = 0.75f;
                    break;
                default:
                    fogDens = profileFogDensity;
                    fogCol = profileFogColor;
                    skyBright = 1f;
                    break;
            }
        }

        private void GetWeatherGameplay(WeatherType weather)
        {
            switch (weather)
            {
                case WeatherType.Clear:
                    AccuracyModifier = 1f; MovementModifier = 1f;
                    MoraleModifier = 1f; VisibilityRange = 200f;
                    break;
                case WeatherType.Cloudy:
                    AccuracyModifier = 0.95f; MovementModifier = 1f;
                    MoraleModifier = 0.95f; VisibilityRange = 150f;
                    break;
                case WeatherType.Rain:
                    AccuracyModifier = 0.65f; MovementModifier = 0.85f;
                    MoraleModifier = 0.88f; VisibilityRange = 100f;
                    break;
                case WeatherType.HeavyRain:
                    AccuracyModifier = 0.35f; MovementModifier = 0.7f;
                    MoraleModifier = 0.78f; VisibilityRange = 55f;
                    break;
                case WeatherType.Thunderstorm:
                    AccuracyModifier = 0.25f; MovementModifier = 0.6f;
                    MoraleModifier = 0.65f; VisibilityRange = 40f;
                    break;
                case WeatherType.Fog:
                    AccuracyModifier = 0.45f; MovementModifier = 0.9f;
                    MoraleModifier = 0.82f; VisibilityRange = 35f;
                    break;
                case WeatherType.Snow:
                    AccuracyModifier = 0.55f; MovementModifier = 0.55f;
                    MoraleModifier = 0.72f; VisibilityRange = 70f;
                    break;
            }
        }

        // ============================================================
        // LIGHTNING
        // ============================================================

        private void UpdateLightning(float dt)
        {
            if (currentWeather != WeatherType.Thunderstorm && targetWeather != WeatherType.Thunderstorm) return;

            // Flash timer
            if (isLightningFlashing)
            {
                lightningFlashTimer -= dt;
                if (lightningFlashTimer <= 0f)
                {
                    isLightningFlashing = false;
                    // Multiple flickers in one strike
                    if (Random.value > 0.5f)
                    {
                        lightningFlashTimer = 0.08f;
                        isLightningFlashing = true;
                    }
                }
            }

            // Next strike timer
            lightningTimer -= dt;
            if (lightningTimer <= 0f)
            {
                TriggerLightning();
                lightningTimer = Random.Range(4f, 15f);
            }

            // Apply flash to sun intensity
            if (sunLight != null && isLightningFlashing)
            {
                sunLight.enabled = true;
                sunLight.color = new Color(0.85f, 0.88f, 1f);
                sunLight.intensity = Random.Range(3f, 6f);
            }
        }

        private void TriggerLightning()
        {
            isLightningFlashing = true;
            lightningFlashTimer = Random.Range(0.05f, 0.15f);

            // Camera shake for thunder
            if (CameraShake.Instance != null && mainCamera != null)
            {
                CameraShake.Instance.Shake(mainCamera.transform.position, Random.Range(0.2f, 0.5f), 0.15f);
            }

            // Thunder sound delay would go here
        }

        // ============================================================
        // WIND
        // ============================================================

        private void UpdateWind(float dt)
        {
            windGustTimer -= dt;
            if (windGustTimer <= 0f)
            {
                windGustTimer = Random.Range(2f, 8f);
                float targetGust = Random.Range(0f, windGustiness);
                currentGustStrength = targetGust;

                // Slight wind direction variation
                windDirection = Quaternion.Euler(0f, Random.Range(-15f, 15f), 0f) * windDirection;
            }

            // Apply wind to rain/snow particles
            float effectiveWind = windIntensity + currentGustStrength;
            Vector3 windForce = windDirection.normalized * effectiveWind;

            if (rainParticles != null && rainParticles.isPlaying)
            {
                var forceModule = rainParticles.forceOverLifetime;
                forceModule.enabled = true;
                forceModule.x = windForce.x * 8f;
                forceModule.z = windForce.z * 8f;
            }

            if (snowParticles != null && snowParticles.isPlaying)
            {
                var forceModule = snowParticles.forceOverLifetime;
                forceModule.enabled = true;
                forceModule.x = windForce.x * 3f;
                forceModule.z = windForce.z * 3f;
            }
        }

        // ============================================================
        // PARTICLES
        // ============================================================

        private void CreateWeatherParticles()
        {
            // Rain — angled streaks with splash
            GameObject rainGO = new GameObject("RainParticles");
            rainGO.transform.SetParent(transform);
            rainParticles = rainGO.AddComponent<ParticleSystem>();
            rainParticles.Stop();

            var rainMain = rainParticles.main;
            rainMain.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
            rainMain.startSpeed = new ParticleSystem.MinMaxCurve(25f, 35f);
            rainMain.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            rainMain.startColor = new Color(0.55f, 0.60f, 0.72f, 0.35f);
            rainMain.maxParticles = 8000;
            rainMain.simulationSpace = ParticleSystemSimulationSpace.World;
            rainMain.gravityModifier = 2f;

            var rainShape = rainParticles.shape;
            rainShape.shapeType = ParticleSystemShapeType.Box;
            rainShape.scale = new Vector3(150f, 0f, 150f);
            rainShape.position = new Vector3(0f, 50f, 0f);

            var rainEmission = rainParticles.emission;
            rainEmission.rateOverTime = 800;

            // Size over lifetime — raindrops stretch
            var rainSizeOverLifetime = rainParticles.sizeOverLifetime;
            rainSizeOverLifetime.enabled = true;
            var sizeKey = new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.7f, 0.8f), new Keyframe(1f, 0.3f));
            rainSizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeKey);

            var rainRenderer = rainGO.GetComponent<ParticleSystemRenderer>();
            rainRenderer.material = CreateRainParticleMaterial(new Color(0.55f, 0.60f, 0.72f, 0.25f));
            rainRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            rainRenderer.lengthScale = 5f;

            // Snow — gentler, floaty
            GameObject snowGO = new GameObject("SnowParticles");
            snowGO.transform.SetParent(transform);
            snowParticles = snowGO.AddComponent<ParticleSystem>();
            snowParticles.Stop();

            var snowMain = snowParticles.main;
            snowMain.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            snowMain.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            snowMain.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.25f);
            snowMain.startColor = new Color(0.92f, 0.94f, 1f, 0.65f);
            snowMain.maxParticles = 5000;
            snowMain.simulationSpace = ParticleSystemSimulationSpace.World;
            snowMain.gravityModifier = 0.25f;

            var snowShape = snowParticles.shape;
            snowShape.shapeType = ParticleSystemShapeType.Box;
            snowShape.scale = new Vector3(150f, 0f, 150f);
            snowShape.position = new Vector3(0f, 35f, 0f);

            var snowEmission = snowParticles.emission;
            snowEmission.rateOverTime = 500;

            // Snow rotation for tumbling effect
            var snowRot = snowParticles.rotationOverLifetime;
            snowRot.enabled = true;
            snowRot.z = new ParticleSystem.MinMaxCurve(-90f, 90f);

            var snowRenderer = snowGO.GetComponent<ParticleSystemRenderer>();
            snowRenderer.material = CreateWeatherParticleMaterial(new Color(0.92f, 0.94f, 1f, 0.55f));

            // Dust — subtle particles for clear/dry weather
            GameObject dustGO = new GameObject("DustParticles");
            dustGO.transform.SetParent(transform);
            dustParticles = dustGO.AddComponent<ParticleSystem>();
            dustParticles.Stop();

            var dustMain = dustParticles.main;
            dustMain.startLifetime = new ParticleSystem.MinMaxCurve(6f, 12f);
            dustMain.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
            dustMain.startSize = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
            dustMain.startColor = new Color(0.7f, 0.65f, 0.55f, 0.08f);
            dustMain.maxParticles = 200;
            dustMain.simulationSpace = ParticleSystemSimulationSpace.World;
            dustMain.gravityModifier = -0.02f; // Slight upward drift

            var dustShape = dustParticles.shape;
            dustShape.shapeType = ParticleSystemShapeType.Box;
            dustShape.scale = new Vector3(120f, 15f, 120f);
            dustShape.position = new Vector3(0f, 5f, 0f);

            var dustEmission = dustParticles.emission;
            dustEmission.rateOverTime = 30;

            // Fade in/out curve for dust
            var dustAlpha = dustParticles.colorOverLifetime;
            dustAlpha.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) }
            );
            dustAlpha.color = gradient;

            var dustRenderer = dustGO.GetComponent<ParticleSystemRenderer>();
            dustRenderer.material = CreateWeatherParticleMaterial(new Color(0.7f, 0.65f, 0.55f, 0.06f));
        }

        private void CreateMoonLight()
        {
            GameObject moonGO = new GameObject("MoonLight");
            moonGO.transform.SetParent(transform);
            moonLight = moonGO.AddComponent<Light>();
            moonLight.type = LightType.Directional;
            moonLight.color = new Color(0.45f, 0.55f, 0.75f); // Cool blue moonlight
            moonLight.intensity = 0.3f;
            moonLight.shadows = LightShadows.Soft;
            moonLight.shadowStrength = 0.3f;
            moonLight.transform.rotation = Quaternion.Euler(35f, 150f, 0f);
            moonLight.enabled = false;
        }

        private void StartRain(int rate)
        {
            if (rainParticles == null) return;
            var emission = rainParticles.emission;
            emission.rateOverTime = rate;

            // Adjust speed based on intensity
            var main = rainParticles.main;
            if (rate > 2000)
            {
                main.startSpeed = new ParticleSystem.MinMaxCurve(30f, 45f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
                main.startColor = new Color(0.48f, 0.52f, 0.65f, 0.4f);
            }

            if (mainCamera != null)
                rainParticles.transform.position = mainCamera.transform.position + Vector3.up * 40f;

            rainParticles.Play();
        }

        private void StartSnow(int rate)
        {
            if (snowParticles == null) return;
            var emission = snowParticles.emission;
            emission.rateOverTime = rate;

            if (mainCamera != null)
                snowParticles.transform.position = mainCamera.transform.position + Vector3.up * 30f;

            snowParticles.Play();
        }

        private void StartDust(int rate)
        {
            if (dustParticles == null) return;
            var emission = dustParticles.emission;
            emission.rateOverTime = rate;
            dustParticles.Play();
        }

        private void UpdateParticlePositions()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null) return;
            }

            Vector3 camPos = mainCamera.transform.position;

            if (rainParticles != null && rainParticles.isPlaying)
                rainParticles.transform.position = camPos + Vector3.up * 40f;

            if (snowParticles != null && snowParticles.isPlaying)
                snowParticles.transform.position = camPos + Vector3.up * 30f;

            if (dustParticles != null && dustParticles.isPlaying)
                dustParticles.transform.position = camPos;
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private BattleTimeOfDay GetTimeOfDayEnum()
        {
            if (timeOfDay >= 5f && timeOfDay < 7f) return BattleTimeOfDay.Dawn;
            if (timeOfDay >= 7f && timeOfDay < 10f) return BattleTimeOfDay.Morning;
            if (timeOfDay >= 10f && timeOfDay < 14f) return BattleTimeOfDay.Midday;
            if (timeOfDay >= 14f && timeOfDay < 17f) return BattleTimeOfDay.Afternoon;
            if (timeOfDay >= 17f && timeOfDay < 19f) return BattleTimeOfDay.Dusk;
            return BattleTimeOfDay.Night;
        }

        private WeatherType PickRandomWeather()
        {
            // Weighted random — more chance for mild weather
            float r = Random.value;
            if (r < 0.35f) return WeatherType.Clear;
            if (r < 0.55f) return WeatherType.Cloudy;
            if (r < 0.70f) return WeatherType.Rain;
            if (r < 0.80f) return WeatherType.Fog;
            if (r < 0.88f) return WeatherType.HeavyRain;
            if (r < 0.94f) return WeatherType.Snow;
            return WeatherType.Thunderstorm;
        }

        private Light FindSunLight()
        {
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional && light.gameObject.name.Contains("Sun"))
                    return light;
            }
            // Fallback: any directional light
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional) return light;
            }
            return null;
        }

        private Light FindLightByName(string name)
        {
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.gameObject.name == name) return light;
            }
            return null;
        }

        // Cached soft-circle textures (one per type to avoid GC)
        private static Texture2D cachedSoftCircle;
        private static Texture2D cachedRainDrop;

        private Material CreateWeatherParticleMaterial(Color color)
        {
            // ---- Find best particle shader ----
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Transparent");
            if (shader == null)
                shader = URPMaterialHelper.LitShader;

            Material mat = new Material(shader);

            // ---- Apply soft-circle texture ----
            if (cachedSoftCircle == null)
                cachedSoftCircle = GenerateSoftCircleTexture(64);
            mat.mainTexture = cachedSoftCircle;

            mat.color = color;

            // ---- Proper alpha blending setup for URP Particles ----
            mat.SetFloat("_Surface", 1f); // 0=Opaque, 1=Transparent
            mat.SetFloat("_Blend", 0f);   // 0=Alpha, 1=Premultiply, 2=Additive, 3=Multiply

            // Enable alpha blending keywords
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_AlphaClip", 0f);

            // Shader keywords for transparency
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");

            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = 3000;

            return mat;
        }

        /// <summary>
        /// Create a stretched raindrop-shaped material (brighter center, elongated fade)
        /// </summary>
        private Material CreateRainParticleMaterial(Color color)
        {
            Material mat = CreateWeatherParticleMaterial(color);
            // Rain uses stretched billboard — the soft circle still works
            // but we make it slightly brighter at center for "wet streak" look
            if (cachedRainDrop == null)
                cachedRainDrop = GenerateRainDropTexture(32, 64);
            mat.mainTexture = cachedRainDrop;
            return mat;
        }

        /// <summary>
        /// Generates a 2D radial gradient: opaque white center, fading to fully transparent edges.
        /// This replaces the default square particle with a smooth, round dot.
        /// </summary>
        private static Texture2D GenerateSoftCircleTexture(int resolution)
        {
            Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float center = resolution * 0.5f;
            float maxDist = center;

            Color[] pixels = new Color[resolution * resolution];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) / maxDist;

                    // Smooth cubic falloff for soft edges
                    float alpha = Mathf.Clamp01(1f - dist);
                    alpha = alpha * alpha * (3f - 2f * alpha); // smoothstep
                    alpha *= alpha; // Extra soft outer edge

                    pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true); // makeNoLongerReadable for perf
            return tex;
        }

        /// <summary>
        /// Generates a vertically stretched raindrop texture — bright core with soft trail.
        /// Used with Stretch render mode for realistic rain streaks.
        /// </summary>
        private static Texture2D GenerateRainDropTexture(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float cx = width * 0.5f;
            float cy = height * 0.5f;

            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = (x - cx + 0.5f) / cx;
                    float dy = (y - cy + 0.5f) / cy;

                    // Elliptical — wider horizontally compressed for rain streak feel
                    float dist = Mathf.Sqrt(dx * dx * 4f + dy * dy);

                    float alpha = Mathf.Clamp01(1f - dist);
                    alpha = alpha * alpha; // Soft edges

                    // Brighter at top-center (leading edge of raindrop)
                    float vertFade = Mathf.Clamp01(1f - Mathf.Abs(dy) * 0.5f);
                    alpha *= vertFade;

                    // Core brightness
                    float coreBright = Mathf.Clamp01(1f - dist * 1.5f);
                    float r = Mathf.Lerp(0.7f, 1f, coreBright);

                    pixels[y * width + x] = new Color(r, r, r, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return tex;
        }
    }
}

