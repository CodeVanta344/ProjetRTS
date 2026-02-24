using UnityEngine;
using System.Collections.Generic;

namespace NapoleonicWars.Core
{
    /// <summary>
    /// Generates high-quality VFX textures procedurally at runtime using Perlin noise,
    /// Worley noise, and radial gradients. Much better than default circle particles.
    /// Produces soft, volumetric-looking smoke, fire, and spark textures.
    /// </summary>
    public static class VFXTextureGenerator
    {
        // Cache all generated textures — only created once
        private static Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        // ===================================================================
        // PUBLIC API — Get pre-cached textures
        // ===================================================================

        /// <summary>Soft volumetric smoke puff — 6-octave fractal noise with radial falloff</summary>
        public static Texture2D SmokeTexture => GetOrCreate("smoke", 128, CreateSmokeTexture);

        /// <summary>Bright muzzle flash — radial starburst with hot core</summary>
        public static Texture2D FlashTexture => GetOrCreate("flash", 64, CreateFlashTexture);

        /// <summary>Round spark/ember dot — tiny hard center with soft glow</summary>
        public static Texture2D SparkTexture => GetOrCreate("spark", 32, CreateSparkTexture);

        /// <summary>Fire/flame texture — vertical gradient noise with hot-to-cool colors</summary>
        public static Texture2D FireTexture => GetOrCreate("fire", 128, CreateFireTexture);

        /// <summary>Debris/dirt chunk — irregular noisy shape</summary>
        public static Texture2D DebrisTexture => GetOrCreate("debris", 32, CreateDebrisTexture);

        /// <summary>Soft circle for dust — simple radial gradient, very soft edges</summary>
        public static Texture2D DustTexture => GetOrCreate("dust", 64, CreateDustTexture);

        /// <summary>Ground scorch mark — dark ring with noise</summary>
        public static Texture2D ScorchTexture => GetOrCreate("scorch", 128, CreateScorchTexture);

        /// <summary>Blood splatter — organic irregular shape</summary>
        public static Texture2D BloodTexture => GetOrCreate("blood", 64, CreateBloodTexture);

        /// <summary>Shockwave ring — thin expanding ring</summary>
        public static Texture2D ShockwaveTexture => GetOrCreate("shockwave", 128, CreateShockwaveTexture);

        // ===================================================================
        // TEXTURE GENERATORS
        // ===================================================================

        private static Texture2D CreateSmokeTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float halfSize = size * 0.5f;
            float offsetX = Random.Range(0f, 1000f);
            float offsetY = Random.Range(0f, 1000f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - halfSize) / halfSize;
                    float ny = (y - halfSize) / halfSize;
                    float dist = Mathf.Sqrt(nx * nx + ny * ny);

                    // Radial falloff — smooth edge
                    float radial = 1f - Mathf.SmoothStep(0f, 1f, dist);

                    // Multi-octave Perlin noise for soft volumetric look
                    float noise = 0f;
                    float amp = 1f;
                    float freq = 2f;
                    float totalAmp = 0f;
                    for (int o = 0; o < 6; o++)
                    {
                        noise += amp * Mathf.PerlinNoise(
                            offsetX + (float)x / size * freq,
                            offsetY + (float)y / size * freq);
                        totalAmp += amp;
                        amp *= 0.5f;
                        freq *= 2f;
                    }
                    noise /= totalAmp;

                    // Combine
                    float alpha = radial * noise * 1.3f;
                    alpha = Mathf.Clamp01(alpha);

                    // Power curve for softer edges
                    alpha = Mathf.Pow(alpha, 0.7f);

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static Texture2D CreateFlashTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float halfSize = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - halfSize) / halfSize;
                    float ny = (y - halfSize) / halfSize;
                    float dist = Mathf.Sqrt(nx * nx + ny * ny);

                    // Tight bright core
                    float core = Mathf.Exp(-dist * dist * 8f);
                    // Wider glow
                    float glow = Mathf.Exp(-dist * dist * 2f) * 0.5f;
                    // Starburst rays
                    float angle = Mathf.Atan2(ny, nx);
                    float rays = (Mathf.Sin(angle * 6f) * 0.5f + 0.5f) * 0.3f;
                    rays *= Mathf.Exp(-dist * 3f);

                    float val = Mathf.Clamp01(core + glow + rays);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, val));
                }
            }

            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static Texture2D CreateSparkTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float halfSize = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - halfSize) / halfSize;
                    float ny = (y - halfSize) / halfSize;
                    float dist = Mathf.Sqrt(nx * nx + ny * ny);

                    // Sharp bright center with soft glow falloff
                    float val = Mathf.Exp(-dist * dist * 12f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, val));
                }
            }

            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static Texture2D CreateFireTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float halfSize = size * 0.5f;
            float offsetX = Random.Range(0f, 1000f);
            float offsetY = Random.Range(0f, 1000f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - halfSize) / halfSize;
                    float ny = (y - halfSize) / halfSize;
                    float dist = Mathf.Sqrt(nx * nx + ny * ny);

                    // Vertical bias — fire rises
                    float vertBias = Mathf.Clamp01(1f - (ny + 1f) * 0.5f);

                    // Noise for irregular fire shape
                    float noise = 0f;
                    float amp = 1f;
                    float freq = 3f;
                    for (int o = 0; o < 4; o++)
                    {
                        noise += amp * Mathf.PerlinNoise(
                            offsetX + (float)x / size * freq,
                            offsetY + (float)y / size * freq);
                        amp *= 0.5f;
                        freq *= 2.2f;
                    }
                    noise = noise / 1.875f;

                    // Radial falloff
                    float radial = 1f - Mathf.SmoothStep(0f, 0.9f, dist);

                    float alpha = radial * noise * vertBias * 2f;
                    alpha = Mathf.Clamp01(alpha);

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static Texture2D CreateDebrisTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float halfSize = size * 0.5f;
            float offsetX = Random.Range(0f, 1000f);
            float offsetY = Random.Range(0f, 1000f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - halfSize) / halfSize;
                    float ny = (y - halfSize) / halfSize;
                    float dist = Mathf.Sqrt(nx * nx + ny * ny);

                    float noise = Mathf.PerlinNoise(offsetX + x * 0.3f, offsetY + y * 0.3f);
                    float shape = dist < (0.3f + noise * 0.3f) ? 1f : 0f;

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, shape));
                }
            }

            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Point;
            return tex;
        }

        private static Texture2D CreateDustTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float halfSize = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - halfSize) / halfSize;
                    float ny = (y - halfSize) / halfSize;
                    float dist = Mathf.Sqrt(nx * nx + ny * ny);

                    // Very soft radial gradient
                    float val = Mathf.Exp(-dist * dist * 3f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, val));
                }
            }

            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static Texture2D CreateScorchTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float halfSize = size * 0.5f;
            float offsetX = Random.Range(0f, 1000f);
            float offsetY = Random.Range(0f, 1000f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - halfSize) / halfSize;
                    float ny = (y - halfSize) / halfSize;
                    float dist = Mathf.Sqrt(nx * nx + ny * ny);

                    // Scorch ring shape: dark in center, darker ring, fading edge
                    float ring = Mathf.Exp(-Mathf.Pow(dist - 0.4f, 2) * 20f);
                    float center = Mathf.Exp(-dist * dist * 4f) * 0.7f;

                    // Noise for irregular edges
                    float noise = Mathf.PerlinNoise(
                        offsetX + (float)x / size * 4f,
                        offsetY + (float)y / size * 4f);
                    noise = noise * 0.4f + 0.6f;

                    float radialCutoff = 1f - Mathf.SmoothStep(0.5f, 0.9f, dist);

                    float alpha = (ring + center) * noise * radialCutoff;
                    alpha = Mathf.Clamp01(alpha);

                    tex.SetPixel(x, y, new Color(0.05f, 0.03f, 0.02f, alpha));
                }
            }

            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static Texture2D CreateBloodTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float halfSize = size * 0.5f;
            float offsetX = Random.Range(0f, 1000f);
            float offsetY = Random.Range(0f, 1000f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - halfSize) / halfSize;
                    float ny = (y - halfSize) / halfSize;
                    float dist = Mathf.Sqrt(nx * nx + ny * ny);

                    // Irregular organic shape
                    float noise = Mathf.PerlinNoise(offsetX + x * 0.15f, offsetY + y * 0.15f);
                    float threshold = 0.35f + noise * 0.25f;
                    float alpha = dist < threshold ? 1f : 0f;

                    // Soft edge
                    if (dist >= threshold * 0.7f && dist < threshold)
                    {
                        alpha = 1f - Mathf.InverseLerp(threshold * 0.7f, threshold, dist);
                    }

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static Texture2D CreateShockwaveTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float halfSize = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - halfSize) / halfSize;
                    float ny = (y - halfSize) / halfSize;
                    float dist = Mathf.Sqrt(nx * nx + ny * ny);

                    // Thin ring at ~0.7 radius
                    float ring = Mathf.Exp(-Mathf.Pow(dist - 0.7f, 2) * 80f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, ring));
                }
            }

            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        // ===================================================================
        // CACHE HELPER
        // ===================================================================

        private static Texture2D GetOrCreate(string key, int size, System.Func<int, Texture2D> generator)
        {
            if (cache.TryGetValue(key, out Texture2D existing) && existing != null)
                return existing;

            Texture2D tex = generator(size);
            tex.name = $"VFX_{key}";
            cache[key] = tex;
            return tex;
        }
    }
}
