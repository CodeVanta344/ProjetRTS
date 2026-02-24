using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NapoleonicWars.Core
{
    /// <summary>
    /// Camera impact effects: screen shake, bloom pulse, vignette flash.
    /// Triggered by explosions, cannon fire, and nearby impacts.
    /// Intensity scales with distance to the effect source.
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        [Header("Shake Settings")]
        [SerializeField] private float maxShakeIntensity = 0.5f;
        #pragma warning disable CS0414
        [SerializeField] private float shakeDecay = 3f;
        #pragma warning restore CS0414

        // Shake state
        private float currentShakeIntensity;
        private float currentShakeDuration;
        private float shakeTimer;
        private Vector3 originalPosition;
        private bool isShaking;
        
        // Bloom pulse state
        private float bloomPulseTimer;
        private float bloomPulseIntensity;
        private float originalBloomIntensity = 0.5f;
        
        // Vignette flash
        private float vignettePulseTimer;
        private float originalVignetteIntensity = 0.25f;

        private Camera mainCamera;

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
            mainCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null) return;
            }

            UpdateShake();
            UpdateBloomPulse();
            UpdateVignettePulse();
        }

        // =========================================
        // PUBLIC API
        // =========================================

        /// <summary>
        /// Trigger camera shake. Intensity auto-scales by distance to camera.
        /// </summary>
        /// <param name="worldPos">World position of the explosion/impact</param>
        /// <param name="intensity">Base intensity (0-1)</param>
        /// <param name="duration">Shake duration in seconds</param>
        public void Shake(Vector3 worldPos, float intensity = 0.5f, float duration = 0.3f)
        {
            if (mainCamera == null) return;

            float dist = Vector3.Distance(mainCamera.transform.position, worldPos);
            // Intensity falloff: full at 0m, halves every 30m, minimum at 120m
            float distanceFalloff = Mathf.Clamp01(1f - (dist / 120f));
            distanceFalloff = distanceFalloff * distanceFalloff; // Quadratic falloff
            
            float finalIntensity = intensity * distanceFalloff * maxShakeIntensity;
            
            // Only override if stronger than current shake
            if (finalIntensity > currentShakeIntensity)
            {
                currentShakeIntensity = finalIntensity;
                currentShakeDuration = duration;
                shakeTimer = duration;
                isShaking = true;
                originalPosition = mainCamera.transform.localPosition;
            }
        }

        /// <summary>
        /// Trigger a cannon explosion — shake + bloom pulse + vignette flash
        /// </summary>
        public void ExplosionImpact(Vector3 worldPos, float intensity = 1f)
        {
            Shake(worldPos, intensity * 0.7f, 0.4f);
            BloomPulse(worldPos, intensity);
            VignetteFlash(worldPos, intensity * 0.3f);
        }

        /// <summary>
        /// Trigger a cannon fire — moderate shake + brief bloom pulse
        /// </summary>
        public void CannonFireImpact(Vector3 worldPos)
        {
            Shake(worldPos, 0.35f, 0.2f);
            BloomPulse(worldPos, 0.5f);
        }

        /// <summary>
        /// Brief bloom pulse for muzzle flashes and explosions
        /// </summary>
        public void BloomPulse(Vector3 worldPos, float intensity = 0.5f)
        {
            if (mainCamera == null) return;
            float dist = Vector3.Distance(mainCamera.transform.position, worldPos);
            float falloff = Mathf.Clamp01(1f - (dist / 80f));
            
            float pulse = intensity * falloff;
            if (pulse > bloomPulseIntensity)
            {
                bloomPulseIntensity = pulse;
                bloomPulseTimer = 0.15f;
            }
        }

        /// <summary>
        /// Brief vignette darkening for nearby impacts
        /// </summary>
        public void VignetteFlash(Vector3 worldPos, float intensity = 0.3f)
        {
            if (mainCamera == null) return;
            float dist = Vector3.Distance(mainCamera.transform.position, worldPos);
            float falloff = Mathf.Clamp01(1f - (dist / 40f));
            
            if (falloff > 0.1f)
                vignettePulseTimer = 0.2f * intensity * falloff;
        }

        // =========================================
        // UPDATE LOOPS
        // =========================================

        private void UpdateShake()
        {
            if (!isShaking || mainCamera == null) return;

            shakeTimer -= Time.deltaTime;
            
            if (shakeTimer <= 0f)
            {
                isShaking = false;
                currentShakeIntensity = 0f;
                return;
            }

            // Decay the intensity over duration
            float t = shakeTimer / currentShakeDuration;
            float shakeAmount = currentShakeIntensity * t;

            // Perlin-based smooth shake (not random jitter — that looks cheap)
            float time = Time.time * 25f;
            float offsetX = (Mathf.PerlinNoise(time, 0f) - 0.5f) * 2f * shakeAmount;
            float offsetY = (Mathf.PerlinNoise(0f, time) - 0.5f) * 2f * shakeAmount;
            float offsetZ = (Mathf.PerlinNoise(time, time) - 0.5f) * 1f * shakeAmount;

            mainCamera.transform.localPosition += new Vector3(offsetX, offsetY, offsetZ);
        }

        private void UpdateBloomPulse()
        {
            if (bloomPulseTimer <= 0f) return;
            
            bloomPulseTimer -= Time.deltaTime;
            
            if (URPSetup.Instance != null)
            {
                // Access the bloom through URPSetup's Volume
                Volume vol = URPSetup.Instance.GetComponentInChildren<Volume>();
                if (vol != null && vol.profile != null && vol.profile.TryGet<Bloom>(out var bloom))
                {
                    float t = Mathf.Clamp01(bloomPulseTimer / 0.15f);
                    float pulseValue = originalBloomIntensity + bloomPulseIntensity * 3f * t;
                    bloom.intensity.Override(pulseValue);
                    
                    if (bloomPulseTimer <= 0f)
                    {
                        bloom.intensity.Override(originalBloomIntensity);
                        bloomPulseIntensity = 0f;
                    }
                }
            }
        }

        private void UpdateVignettePulse()
        {
            if (vignettePulseTimer <= 0f) return;
            
            vignettePulseTimer -= Time.deltaTime;
            
            if (URPSetup.Instance != null)
            {
                Volume vol = URPSetup.Instance.GetComponentInChildren<Volume>();
                if (vol != null && vol.profile != null && vol.profile.TryGet<Vignette>(out var vignette))
                {
                    float t = Mathf.Clamp01(vignettePulseTimer / 0.2f);
                    float pulseValue = originalVignetteIntensity + 0.3f * t;
                    vignette.intensity.Override(pulseValue);
                    
                    if (vignettePulseTimer <= 0f)
                    {
                        vignette.intensity.Override(originalVignetteIntensity);
                    }
                }
            }
        }
    }
}
