using UnityEngine;

namespace NapoleonicWars.Core.TerrainGeneration
{
    /// <summary>
    /// Algorithme d'érosion hydraulique par particules (gouttes d'eau).
    /// Simule la pluie tombant sur le terrain, coulant le long des pentes, 
    /// arrachant de la terre (érosion) et la déposant plus loin (déposition).
    /// </summary>
    public static class HydraulicErosion
    {
        public struct ErosionParams
        {
            public int iterations;           // Nombre de gouttes d'eau simulées
            public float erosionRate;        // Quantité de terre arrachée
            public float depositionRate;     // Vitesse de déposition des sédiments
            public float evaporationRate;    // Vitesse d'évaporation de l'eau
            public float capacityMultiplier; // Capacité de transport de sédiments
            public int maxLifetime;          // Durée de vie max d'une goutte
            public float gravity;            // Vitesse d'écoulement
            
            public static ErosionParams Default => new ErosionParams
            {
                iterations = 60000,
                erosionRate = 0.05f,
                depositionRate = 0.05f,
                evaporationRate = 0.015f,
                capacityMultiplier = 20f,
                maxLifetime = 40,
                gravity = 4f
            };
        }

        public static float[,] Erode(float[,] heights, int size, int seed, ErosionParams p)
        {
            float[,] result = new float[size, size];
            System.Array.Copy(heights, result, heights.Length);
            
            System.Random prng = new System.Random(seed);
            
            // Brush (rayon d'érosion pour lisser les ravines)
            int brushRadius = 3;
            float[,] brushWeights = GenerateBrushWeights(brushRadius);

            for (int iter = 0; iter < p.iterations; iter++)
            {
                // Spawn aléatoire d'une goutte
                float posX = (float)prng.NextDouble() * (size - 1);
                float posZ = (float)prng.NextDouble() * (size - 1);
                
                float dirX = 0;
                float dirZ = 0;
                float speed = 1;
                float water = 1;
                float sediment = 0;

                for (int lifetime = 0; lifetime < p.maxLifetime; lifetime++)
                {
                    int nodeX = (int)posX;
                    int nodeZ = (int)posZ;
                    
                    // Out of bounds
                    if (nodeX < 0 || nodeX >= size - 1 || nodeZ < 0 || nodeZ >= size - 1)
                        break;

                    // Interpolation bilinéaire pour trouver la hauteur et le gradient (pente) exacts
                    float cellOffsetX = posX - nodeX;
                    float cellOffsetZ = posZ - nodeZ;

                    float h00 = result[nodeZ, nodeX];
                    float h10 = result[nodeZ, nodeX + 1];
                    float h01 = result[nodeZ + 1, nodeX];
                    float h11 = result[nodeZ + 1, nodeX + 1];

                    float gradX = (h10 - h00) * (1 - cellOffsetZ) + (h11 - h01) * cellOffsetZ;
                    float gradZ = (h01 - h00) * (1 - cellOffsetX) + (h11 - h10) * cellOffsetX;

                    // Calcul de la direction d'écoulement
                    dirX = (dirX * 0.1f - gradX * 0.9f);
                    dirZ = (dirZ * 0.1f - gradZ * 0.9f);
                    
                    float len = Mathf.Sqrt(dirX * dirX + dirZ * dirZ);
                    if (len != 0)
                    {
                        dirX /= len;
                        dirZ /= len;
                    }

                    posX += dirX;
                    posZ += dirZ;

                    // Out of bounds after move
                    if (posX < 0 || posX >= size - 1 || posZ < 0 || posZ >= size - 1)
                        break;

                    // Nouvelle hauteur
                    int newNodeX = (int)posX;
                    int newNodeZ = (int)posZ;
                    float newOffsetX = posX - newNodeX;
                    float newOffsetZ = posZ - newNodeZ;

                    float nh00 = result[newNodeZ, newNodeX];
                    float nh10 = result[newNodeZ, newNodeX + 1];
                    float nh01 = result[newNodeZ + 1, newNodeX];
                    float nh11 = result[newNodeZ + 1, newNodeX + 1];

                    float newHeight = nh00 * (1 - newOffsetX) * (1 - newOffsetZ) + 
                                     nh10 * newOffsetX * (1 - newOffsetZ) + 
                                     nh01 * (1 - newOffsetX) * newOffsetZ + 
                                     nh11 * newOffsetX * newOffsetZ;

                    float oldHeight = h00 * (1 - cellOffsetX) * (1 - cellOffsetZ) + 
                                     h10 * cellOffsetX * (1 - cellOffsetZ) + 
                                     h01 * (1 - cellOffsetX) * cellOffsetZ + 
                                     h11 * cellOffsetX * cellOffsetZ;

                    float deltaHeight = newHeight - oldHeight;

                    // Si on monte ou qu'on stagne, on dépose du sédiment (remplissage des trous)
                    if (deltaHeight >= 0)
                    {
                        float amountToDeposit = (deltaHeight >= sediment) ? sediment : deltaHeight;
                        sediment -= amountToDeposit;
                        Deposit(result, nodeX, nodeZ, amountToDeposit, cellOffsetX, cellOffsetZ);
                        break; // La goutte est piégée
                    }

                    // Calcul de la capacité de transport de sédiment
                    float sedimentCapacity = Mathf.Max(-deltaHeight * speed * water * p.capacityMultiplier, 0.01f);

                    if (sediment > sedimentCapacity)
                    {
                        // Trop de sédiment, on dépose
                        float amountToDeposit = (sediment - sedimentCapacity) * p.depositionRate;
                        sediment -= amountToDeposit;
                        Deposit(result, nodeX, nodeZ, amountToDeposit, cellOffsetX, cellOffsetZ);
                    }
                    else
                    {
                        // On peut arracher plus de terre
                        float amountToErode = Mathf.Min((sedimentCapacity - sediment) * p.erosionRate, -deltaHeight);
                        sediment += amountToErode;
                        ErodeArea(result, size, nodeX, nodeZ, amountToErode, brushRadius, brushWeights);
                    }

                    speed = Mathf.Sqrt(Mathf.Max(0, speed * speed + deltaHeight * p.gravity));
                    water *= (1 - p.evaporationRate);
                }
            }
            
            return result;
        }

        private static void Deposit(float[,] heights, int x, int z, float amount, float offsetX, float offsetZ)
        {
            heights[z, x] += amount * (1 - offsetX) * (1 - offsetZ);
            heights[z, x + 1] += amount * offsetX * (1 - offsetZ);
            heights[z + 1, x] += amount * (1 - offsetX) * offsetZ;
            heights[z + 1, x + 1] += amount * offsetX * offsetZ;
        }

        private static void ErodeArea(float[,] heights, int size, int cx, int cz, float amount, int brushRadius, float[,] brushWeights)
        {
            float totalWeight = 0;
            int r2 = brushRadius * brushRadius;
            
            // Calculer le poids total valide pour cette zone
            for (int z = -brushRadius; z <= brushRadius; z++)
            {
                for (int x = -brushRadius; x <= brushRadius; x++)
                {
                    int nx = cx + x;
                    int nz = cz + z;
                    if (nx >= 0 && nx < size && nz >= 0 && nz < size && (x * x + z * z) <= r2)
                    {
                        totalWeight += brushWeights[z + brushRadius, x + brushRadius];
                    }
                }
            }

            if (totalWeight <= 0) return;

            // Appliquer l'érosion
            for (int z = -brushRadius; z <= brushRadius; z++)
            {
                for (int x = -brushRadius; x <= brushRadius; x++)
                {
                    int nx = cx + x;
                    int nz = cz + z;
                    if (nx >= 0 && nx < size && nz >= 0 && nz < size && (x * x + z * z) <= r2)
                    {
                        float weight = brushWeights[z + brushRadius, x + brushRadius] / totalWeight;
                        heights[nz, nx] -= amount * weight;
                    }
                }
            }
        }

        private static float[,] GenerateBrushWeights(int radius)
        {
            int size = radius * 2 + 1;
            float[,] weights = new float[size, size];
            float sum = 0;
            
            for (int z = -radius; z <= radius; z++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    float dist = Mathf.Sqrt(x * x + z * z);
                    float weight = Mathf.Max(0, 1 - dist / radius);
                    weights[z + radius, x + radius] = weight;
                    sum += weight;
                }
            }
            
            // Normaliser
            if (sum > 0)
            {
                for (int z = 0; z < size; z++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        weights[z, x] /= sum;
                    }
                }
            }
            
            return weights;
        }
    }
}
