using UnityEngine;

namespace NapoleonicWars.Core
{
    public enum GraphicsQuality
    {
        Low,
        Medium,
        High
    }

    /// <summary>
    /// Controls graphics quality: shadow distance, LOD bias, particle limits,
    /// render distance, and texture quality. Persists via PlayerPrefs.
    /// </summary>
    public class GraphicsSettings : MonoBehaviour
    {
        public static GraphicsSettings Instance { get; private set; }

        [SerializeField] private GraphicsQuality currentQuality = GraphicsQuality.Medium;
        public GraphicsQuality CurrentQuality => currentQuality;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            int saved = PlayerPrefs.GetInt("GraphicsQuality", 1);
            SetQuality((GraphicsQuality)Mathf.Clamp(saved, 0, 2));
        }

        public void SetQuality(GraphicsQuality quality)
        {
            currentQuality = quality;
            ApplyQuality(quality);
            PlayerPrefs.SetInt("GraphicsQuality", (int)quality);
            PlayerPrefs.Save();
            Debug.Log($"[Graphics] Quality set to {quality}");
        }

        private void ApplyQuality(GraphicsQuality quality)
        {
            switch (quality)
            {
                case GraphicsQuality.Low:
                    QualitySettings.shadowDistance = 30f;
                    QualitySettings.shadows = ShadowQuality.HardOnly;
                    QualitySettings.lodBias = 0.5f;
                    QualitySettings.maximumLODLevel = 2;
                    QualitySettings.particleRaycastBudget = 16;
                    QualitySettings.antiAliasing = 0;
                    QualitySettings.vSyncCount = 0;
                    Application.targetFrameRate = 60;

                    // Reduce LOD distances
                    if (UnitLODManager.Instance != null)
                    {
                        // Low quality = more aggressive culling handled by LOD manager defaults
                    }
                    break;

                case GraphicsQuality.Medium:
                    QualitySettings.shadowDistance = 50f;  // Reduced from 80f
                    QualitySettings.shadows = ShadowQuality.HardOnly;  // Changed from All
                    QualitySettings.lodBias = 0.8f;  // Reduced from 1f
                    QualitySettings.maximumLODLevel = 1;
                    QualitySettings.particleRaycastBudget = 32;  // Reduced from 64
                    QualitySettings.antiAliasing = 0;  // Disabled for performance
                    QualitySettings.vSyncCount = 0;  // DISABLED to prevent stutter
                    Application.targetFrameRate = 60;
                    break;

                case GraphicsQuality.High:
                    QualitySettings.shadowDistance = 100f;  // Reduced from 150f
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.lodBias = 1.5f;  // Reduced from 2f
                    QualitySettings.maximumLODLevel = 0;
                    QualitySettings.particleRaycastBudget = 128;  // Reduced from 256
                    QualitySettings.antiAliasing = 2;  // Reduced from 4
                    QualitySettings.vSyncCount = 0;  // DISABLED - was causing 1s stutter!
                    Application.targetFrameRate = -1;
                    break;
            }
        }
    }
}
