using UnityEngine;
using NapoleonicWars.Core.TerrainGeneration;

namespace NapoleonicWars.Core
{
    [System.Serializable]
    public class TerrainGenerationSettings
    {
        [Header("Global")]
        public int seed = 42;
        public bool useRandomSeed = true;

        [Header("Relief (Fractal Noise)")]
        public float terrainScale = 1200f; // Larger scale to match larger map
        public int octaves = 6;
        public float persistence = 0.45f;
        public float lacunarity = 2.2f;
        public float heightMultiplier = 35f;
        public AnimationCurve heightCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        // Battle specific
        public bool flattenCenterForBattle = true;
        public float battleAreaRadius = 0.25f;

        [Header("Erosion (Hydraulic)")]
        public bool applyErosion = true;
        public int erosionIterations = 250000; // Increased to match larger map
        public float erosionRate = 0.03f;
        public float depositionRate = 0.05f;

        [Header("Vegetation")]
        public int treeCount = 3000;           // 1500→3000 : dense wooded battlefield
        public float forestDensity = 18f;       // 25→18 : tighter Poisson radius = more trees
        public float bushCount = 4000;
    }
}
