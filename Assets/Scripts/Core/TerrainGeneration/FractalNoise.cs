using UnityEngine;

namespace NapoleonicWars.Core.TerrainGeneration
{
    public static class FractalNoise
    {
        public struct NoiseParams
        {
            public float scale;
            public int octaves;
            public float persistence;
            public float lacunarity;
            public Vector2 offset;
            public AnimationCurve heightCurve; // Optionnel : pour terrasser (terrasses/paliers)
            
            public static NoiseParams Default => new NoiseParams
            {
                scale = 50f,
                octaves = 4,
                persistence = 0.5f,
                lacunarity = 2f,
                offset = Vector2.zero,
                heightCurve = AnimationCurve.Linear(0, 0, 1, 1)
            };
        }

        public static float[,] GenerateNoiseMap(int width, int height, int seed, NoiseParams p)
        {
            float[,] noiseMap = new float[width, height];

            System.Random prng = new System.Random(seed);
            Vector2[] octaveOffsets = new Vector2[p.octaves];

            float maxPossibleHeight = 0;
            float amplitude = 1;
            float frequency = 1;

            for (int i = 0; i < p.octaves; i++)
            {
                float offsetX = prng.Next(-100000, 100000) + p.offset.x;
                float offsetY = prng.Next(-100000, 100000) - p.offset.y;
                octaveOffsets[i] = new Vector2(offsetX, offsetY);

                maxPossibleHeight += amplitude;
                amplitude *= p.persistence;
            }

            if (p.scale <= 0) p.scale = 0.0001f;

            float minLocalNoiseHeight = float.MaxValue;
            float maxLocalNoiseHeight = float.MinValue;

            float halfWidth = width / 2f;
            float halfHeight = height / 2f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    amplitude = 1;
                    frequency = 1;
                    float noiseHeight = 0;

                    for (int i = 0; i < p.octaves; i++)
                    {
                        float sampleX = (x - halfWidth + octaveOffsets[i].x) / p.scale * frequency;
                        float sampleY = (y - halfHeight + octaveOffsets[i].y) / p.scale * frequency;

                        // PerlinNoise retourne [0,1], on le recentre sur [-1,1] pour avoir des vallées et collines
                        float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
                        noiseHeight += perlinValue * amplitude;

                        amplitude *= p.persistence;
                        frequency *= p.lacunarity;
                    }

                    if (noiseHeight > maxLocalNoiseHeight) maxLocalNoiseHeight = noiseHeight;
                    if (noiseHeight < minLocalNoiseHeight) minLocalNoiseHeight = noiseHeight;

                    noiseMap[x, y] = noiseHeight;
                }
            }

            // Normalisation de la carte de bruit entre 0 et 1
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Normalisation InverseLerp
                    float normalizedHeight = Mathf.InverseLerp(minLocalNoiseHeight, maxLocalNoiseHeight, noiseMap[x, y]);
                    
                    // Application optionnelle de courbe (terrasses, etc.)
                    if (p.heightCurve != null)
                        normalizedHeight = p.heightCurve.Evaluate(normalizedHeight);
                        
                    noiseMap[x, y] = normalizedHeight;
                }
            }

            return noiseMap;
        }
        
        /// <summary>
        /// Ajoute un masque de cratère (Radial Falloff) pour créer une vallée au centre ou s'assurer que les bords soient plats.
        /// </summary>
        public static float[,] ApplyFalloffMask(float[,] map, float edgeWidth = 0.2f)
        {
            int width = map.GetLength(0);
            int height = map.GetLength(1);
            float[,] result = new float[width, height];
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = (float)x / width;
                    float ny = (float)y / height;
                    
                    // Distance par rapport au bord le plus proche (0 au bord, 0.5 au centre)
                    float distToEdgeX = Mathf.Min(nx, 1f - nx);
                    float distToEdgeY = Mathf.Min(ny, 1f - ny);
                    float distToEdge = Mathf.Min(distToEdgeX, distToEdgeY);
                    
                    // Falloff mask: 0 aux bords, 1 à l'intérieur
                    float mask = 1f;
                    if (distToEdge < edgeWidth)
                    {
                        // Courbe d'atténuation douce (SmoothStep)
                        float t = distToEdge / edgeWidth;
                        mask = t * t * (3f - 2f * t);
                    }
                    
                    result[x, y] = map[x, y] * mask;
                }
            }
            return result;
        }
    }
}
