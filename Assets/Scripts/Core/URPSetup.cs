using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NapoleonicWars.Core
{
    public class URPSetup : MonoBehaviour
    {
        public static URPSetup Instance { get; private set; }

        private Volume postProcessVolume;
        private Bloom bloom;
        private Vignette vignette;
        private ColorAdjustments colorAdjustments;
        private DepthOfField depthOfField;
        private FilmGrain filmGrain;
        private MotionBlur motionBlur;
        private ChromaticAberration chromaticAberration;
        private LiftGammaGain liftGammaGain;

        // Profile baseline
        private float baseBloomIntensity = 0.4f;
        private float baseVignetteIntensity = 0.22f;

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
            SetupPostProcessing();
        }

        private void Update()
        {
            // Dynamic post-processing based on weather + time
            if (WeatherSystem.Instance != null)
            {
                UpdateWeatherPostProcessing();
                UpdateTimeOfDayPostProcessing();
            }
        }

        private void SetupPostProcessing()
        {
            // Create global post-processing volume
            GameObject volumeGO = new GameObject("PostProcessVolume");
            volumeGO.transform.SetParent(transform);
            postProcessVolume = volumeGO.AddComponent<Volume>();
            postProcessVolume.isGlobal = true;
            postProcessVolume.priority = 1;

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            postProcessVolume.profile = profile;

            // ============================================================
            // BLOOM — warm glow for sunlight, muzzle flashes, fire
            // ============================================================
            bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(baseBloomIntensity);
            bloom.scatter.Override(0.65f);
            bloom.tint.Override(new Color(1f, 0.97f, 0.92f)); // Warm tint

            // ============================================================
            // VIGNETTE — cinematic edge darkening
            // ============================================================
            vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(baseVignetteIntensity);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.05f, 0.03f, 0.02f));

            // ============================================================
            // COLOR ADJUSTMENTS — Napoleonic-era tone
            // Slightly desaturated, warm, slight contrast boost
            // ============================================================
            colorAdjustments = profile.Add<ColorAdjustments>(true);
            colorAdjustments.postExposure.Override(0.05f);
            colorAdjustments.contrast.Override(12f);
            colorAdjustments.saturation.Override(-5f);
            colorAdjustments.colorFilter.Override(new Color(1f, 0.99f, 0.96f)); // Warm filter

            // ============================================================
            // LIFT GAMMA GAIN — Fine color grading
            // Lift (shadows): slightly blue for depth
            // Gamma (midtones): neutral with subtle warmth
            // Gain (highlights): warm golden sunlit feel
            // ============================================================
            liftGammaGain = profile.Add<LiftGammaGain>(true);
            liftGammaGain.lift.Override(new Vector4(0.98f, 0.98f, 1.02f, 0f)); // Cool shadows
            liftGammaGain.gamma.Override(new Vector4(1.01f, 1.00f, 0.99f, 0f)); // Warm midtones
            liftGammaGain.gain.Override(new Vector4(1.03f, 1.01f, 0.97f, 0f)); // Golden highlights

            // ============================================================
            // FILM GRAIN — subtle period look
            // ============================================================
            filmGrain = profile.Add<FilmGrain>(true);
            filmGrain.type.Override(FilmGrainLookup.Medium1);
            filmGrain.intensity.Override(0.12f);
            filmGrain.response.Override(0.8f);

            // ============================================================
            // MOTION BLUR — subtle for cavalry charges
            // ============================================================
            motionBlur = profile.Add<MotionBlur>(true);
            motionBlur.intensity.Override(0.12f);

            // ============================================================
            // CHROMATIC ABERRATION — disabled by default, activated in storms
            // ============================================================
            chromaticAberration = profile.Add<ChromaticAberration>(true);
            chromaticAberration.intensity.Override(0f);

            Debug.Log("[URP] Post-processing volume created with enhanced Napoleonic-era grading.");
        }

        // ============================================================
        // WEATHER-AWARE POST-PROCESSING
        // ============================================================

        private void UpdateWeatherPostProcessing()
        {
            var ws = WeatherSystem.Instance;
            if (ws == null) return;

            WeatherType weather = ws.CurrentWeather;

            // --- Bloom: bright in sun, muted in rain/storm ---
            float weatherBloom = baseBloomIntensity;
            float weatherVignette = baseVignetteIntensity;
            float chromatic = 0f;
            float saturation = -5f;
            float contrast = 12f;
            float exposure = 0.05f;
            Color colorFilter = new Color(1f, 0.99f, 0.96f);

            switch (weather)
            {
                case WeatherType.Clear:
                    weatherBloom = 0.5f;
                    weatherVignette = 0.18f;
                    saturation = -2f;
                    break;

                case WeatherType.Cloudy:
                    weatherBloom = 0.3f;
                    weatherVignette = 0.22f;
                    saturation = -8f;
                    exposure = -0.05f;
                    colorFilter = new Color(0.95f, 0.96f, 1f); // Cool shift
                    break;

                case WeatherType.Rain:
                    weatherBloom = 0.2f;
                    weatherVignette = 0.28f;
                    saturation = -12f;
                    contrast = 15f;
                    exposure = -0.15f;
                    colorFilter = new Color(0.90f, 0.93f, 1f); // Blue-grey
                    break;

                case WeatherType.HeavyRain:
                    weatherBloom = 0.15f;
                    weatherVignette = 0.35f;
                    saturation = -18f;
                    contrast = 18f;
                    exposure = -0.25f;
                    colorFilter = new Color(0.85f, 0.88f, 1f);
                    chromatic = 0.08f;
                    break;

                case WeatherType.Thunderstorm:
                    weatherBloom = 0.6f; // Lightning flashes bloom
                    weatherVignette = 0.4f;
                    saturation = -25f;
                    contrast = 22f;
                    exposure = -0.35f;
                    colorFilter = new Color(0.80f, 0.82f, 1f); // Heavy blue
                    chromatic = 0.15f;
                    break;

                case WeatherType.Fog:
                    weatherBloom = 0.35f;
                    weatherVignette = 0.3f;
                    saturation = -15f;
                    contrast = 5f; // Low contrast in fog
                    exposure = 0.1f;
                    colorFilter = new Color(0.95f, 0.95f, 0.95f); // Near white/grey
                    break;

                case WeatherType.Snow:
                    weatherBloom = 0.55f; // Snow reflects light
                    weatherVignette = 0.2f;
                    saturation = -10f;
                    contrast = 8f;
                    exposure = 0.15f;
                    colorFilter = new Color(0.94f, 0.96f, 1f); // Cold tint
                    break;
            }

            // Apply (use Lerp for smooth transitions)
            float lerpSpeed = 2f * Time.deltaTime;

            if (bloom != null)
                bloom.intensity.Override(Mathf.Lerp(bloom.intensity.value, weatherBloom, lerpSpeed));
            if (vignette != null)
                vignette.intensity.Override(Mathf.Lerp(vignette.intensity.value, weatherVignette, lerpSpeed));
            if (chromaticAberration != null)
                chromaticAberration.intensity.Override(Mathf.Lerp(chromaticAberration.intensity.value, chromatic, lerpSpeed));
            if (colorAdjustments != null)
            {
                colorAdjustments.saturation.Override(Mathf.Lerp(colorAdjustments.saturation.value, saturation, lerpSpeed));
                colorAdjustments.contrast.Override(Mathf.Lerp(colorAdjustments.contrast.value, contrast, lerpSpeed));
                colorAdjustments.postExposure.Override(Mathf.Lerp(colorAdjustments.postExposure.value, exposure, lerpSpeed));
                colorAdjustments.colorFilter.Override(Color.Lerp(colorAdjustments.colorFilter.value, colorFilter, lerpSpeed));
            }
        }

        // ============================================================
        // TIME-OF-DAY POST-PROCESSING
        // ============================================================

        private void UpdateTimeOfDayPostProcessing()
        {
            var ws = WeatherSystem.Instance;
            if (ws == null) return;

            float hour = ws.TimeOfDay;

            // --- Night: heavier vignette, bluer tones, more grain ---
            float nightFactor = 0f;
            if (hour < 6f || hour > 20f)
                nightFactor = 1f;
            else if (hour < 8f)
                nightFactor = 1f - (hour - 6f) / 2f;
            else if (hour > 18f)
                nightFactor = (hour - 18f) / 2f;

            if (nightFactor > 0.01f)
            {
                // Vignette intensifies at night
                if (vignette != null)
                {
                    float currentV = vignette.intensity.value;
                    float nightV = currentV + nightFactor * 0.15f;
                    vignette.intensity.Override(Mathf.Min(nightV, 0.55f));
                }

                // Shadows go bluer at night
                if (liftGammaGain != null)
                {
                    Vector4 nightLift = Vector4.Lerp(
                        new Vector4(0.98f, 0.98f, 1.02f, 0f),
                        new Vector4(0.85f, 0.88f, 1.15f, -0.1f), // Deep blue shadows
                        nightFactor
                    );
                    liftGammaGain.lift.Override(nightLift);
                }

                // More grain at night
                if (filmGrain != null)
                {
                    filmGrain.intensity.Override(Mathf.Lerp(0.12f, 0.3f, nightFactor));
                }
            }
            else
            {
                // Reset to day defaults
                if (liftGammaGain != null)
                    liftGammaGain.lift.Override(new Vector4(0.98f, 0.98f, 1.02f, 0f));
                if (filmGrain != null)
                    filmGrain.intensity.Override(0.12f);
            }

            // --- Dawn/Dusk: warm golden grading ---
            float goldenFactor = 0f;
            if (hour >= 5.5f && hour < 8f)
                goldenFactor = 1f - Mathf.Abs(hour - 6.5f) / 1.5f;
            else if (hour >= 16f && hour < 19.5f)
                goldenFactor = 1f - Mathf.Abs(hour - 17.5f) / 2f;
            goldenFactor = Mathf.Clamp01(goldenFactor);

            if (goldenFactor > 0.01f && liftGammaGain != null)
            {
                // Push gain toward warm golden
                Vector4 goldenGain = Vector4.Lerp(
                    new Vector4(1.03f, 1.01f, 0.97f, 0f),
                    new Vector4(1.12f, 0.95f, 0.80f, 0.05f), // Rich golden
                    goldenFactor
                );
                liftGammaGain.gain.Override(goldenGain);
            }
            else if (nightFactor < 0.01f && liftGammaGain != null)
            {
                // Default day gain
                liftGammaGain.gain.Override(new Vector4(1.03f, 1.01f, 0.97f, 0f));
            }
        }

        // ============================================================
        // PUBLIC API
        // ============================================================

        /// <summary>Increase bloom and vignette during intense combat</summary>
        public void SetBattleIntensity(float intensity)
        {
            if (bloom != null)
                bloom.intensity.Override(baseBloomIntensity + intensity * 0.6f);
            if (vignette != null)
                vignette.intensity.Override(baseVignetteIntensity + intensity * 0.15f);
            if (chromaticAberration != null)
                chromaticAberration.intensity.Override(intensity * 0.08f);
        }

        public void SetWeatherFog(float density, Color fogColor)
        {
            RenderSettings.fog = density > 0.0005f;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = density;
            RenderSettings.fogColor = fogColor;
        }

        public void SetTimeOfDay(float hour)
        {
            // Handled by WeatherSystem now — kept for backwards compatibility
            if (WeatherSystem.Instance != null)
                WeatherSystem.Instance.SetStartTime(hour);
        }
    }
}
