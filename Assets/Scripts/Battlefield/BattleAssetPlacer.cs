using UnityEngine;
using System.Collections.Generic;
using NapoleonicWars.Campaign;

namespace NapoleonicWars.Battlefield
{
    /// <summary>
    /// Automatically places battlefield assets on the terrain.
    /// Creates strategic defensive positions, cover, and environmental details.
    /// </summary>
    public class BattleAssetPlacer : MonoBehaviour
    {
        [Header("Placement Settings")]
        [SerializeField] private int numberOfAssets = 30;
        [SerializeField] private float minDistanceFromEdge = 20f;
        [SerializeField] private float minDistanceBetweenAssets = 15f;
        [SerializeField] private float maxSlopeAngle = 25f;
        [SerializeField] private bool createDefensiveLine = true;
        [SerializeField] private bool createVillages = true;
        [SerializeField] private bool createNaturalCover = true;
        
        [Header("Strategic Zones")]
        [SerializeField] private Vector3 playerZone = new Vector3(-100f, 0f, 0f);
        [SerializeField] private Vector3 enemyZone = new Vector3(100f, 0f, 0f);
        [SerializeField] private float zoneRadius = 80f;
        
        private Terrain terrain;
        private List<Vector3> placedPositions = new List<Vector3>();
        private List<System.Func<GameObject>> assetCreators;
        private Transform assetsParent;

        /// <summary>Set by BattleTerrainConfigurator before Start()</summary>
        public BattleTerrainProfile ActiveProfile { get; set; }
        
        private void Start()
        {
            terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                Debug.LogError("[BattleAssetPlacer] No terrain found!");
                return;
            }

            // Adjust zones based on actual terrain size
            Vector3 terrainSize = terrain.terrainData.size;
            float terrainX = terrainSize.x;
            float terrainZ = terrainSize.z;

            // Set player zone at 25% from left edge, enemy at 75%
            playerZone = new Vector3(terrainX * 0.25f, 0f, terrainZ * 0.5f);
            enemyZone = new Vector3(terrainX * 0.75f, 0f, terrainZ * 0.5f);
            zoneRadius = Mathf.Min(terrainX, terrainZ) * 0.2f;

            Debug.Log($"[BattleAssetPlacer] Terrain size: {terrainSize}, Player zone: {playerZone}, Enemy zone: {enemyZone}");

            // Initialize materials and asset creators
            BattleAssetGenerator.InitializeMaterials();
            assetCreators = BattleAssetGenerator.GetAllAssetCreators();

            // Create parent container
            assetsParent = new GameObject("BattlefieldAssets").transform;
            assetsParent.SetParent(transform);

            // Place assets
            PlaceAllAssets();

            // OPTIMIZATION: Combine meshes for massive draw call reduction
            StaticBatchingUtility.Combine(assetsParent.gameObject);

            Debug.Log($"[BattleAssetPlacer] Placed {placedPositions.Count} assets and applied Static Batching!");
        }
        
        private void PlaceAllAssets()
        {
            bool profileVillage = ActiveProfile == null || ActiveProfile.placeVillage;
            bool profileDefense = ActiveProfile == null || ActiveProfile.placeDefensiveLines;
            bool profileCover = ActiveProfile == null || ActiveProfile.placeNaturalCover;
            bool profileBiome = ActiveProfile != null && ActiveProfile.placeBiomeSpecificAssets;

            // 1. Create defensive lines for both sides
            if (createDefensiveLine && profileDefense)
            {
                CreateDefensiveLine(playerZone, zoneRadius, 0);
                CreateDefensiveLine(enemyZone, zoneRadius, 1);
            }
            
            // 2. Create central village/farms (not in all biomes)
            if (createVillages && profileVillage)
            {
                CreateCentralVillage();
            }
            
            // 3. Scatter natural cover and obstacles
            if (createNaturalCover && profileCover)
            {
                ScatterNaturalCover();
            }

            // 4. Biome-specific assets
            if (profileBiome)
            {
                PlaceBiomeAssets();
            }
            
            // 5. Place remaining random assets
            PlaceRandomAssets();
        }

        /// <summary>Place biome-specific blocking assets based on active profile</summary>
        private void PlaceBiomeAssets()
        {
            if (ActiveProfile == null) return;

            switch (ActiveProfile.biome)
            {
                case ProvinceTerrainType.Desert:
                    PlaceDesertAssets();
                    break;
                case ProvinceTerrainType.Snow:
                    PlaceSnowAssets();
                    break;
                case ProvinceTerrainType.Marsh:
                    PlaceMarshAssets();
                    break;
                case ProvinceTerrainType.Urban:
                    PlaceUrbanAssets();
                    break;
                case ProvinceTerrainType.Coastal:
                    PlaceCoastalAssets();
                    break;
                case ProvinceTerrainType.Mountains:
                    PlaceMountainAssets();
                    break;
            }
        }

        private void PlaceDesertAssets()
        {
            Vector3 center = (playerZone + enemyZone) * 0.5f;

            // Oasis (center)
            Vector3 oasisPos = FindValidPosition(center, 40f, 20f);
            if (oasisPos != Vector3.zero)
                PlaceAsset(BiomeAssetGenerator.CreateOasis("Oasis"), oasisPos, 0f);

            // Desert tents (both sides)
            for (int i = 0; i < 3; i++)
            {
                Vector3 pos = FindValidPosition(playerZone + Random.insideUnitSphere * zoneRadius * 0.5f, 20f, 12f);
                if (pos != Vector3.zero)
                    PlaceAsset(BiomeAssetGenerator.CreateDesertTent($"Tent_P{i}"), pos, Random.Range(0f, 360f));
            }
            for (int i = 0; i < 3; i++)
            {
                Vector3 pos = FindValidPosition(enemyZone + Random.insideUnitSphere * zoneRadius * 0.5f, 20f, 12f);
                if (pos != Vector3.zero)
                    PlaceAsset(BiomeAssetGenerator.CreateDesertTent($"Tent_E{i}"), pos, Random.Range(0f, 360f));
            }

            // Sand dunes
            for (int i = 0; i < 5; i++)
            {
                Vector3 pos = GetRandomPositionOnMap();
                pos = FindValidPosition(pos, 30f, 20f);
                if (pos != Vector3.zero)
                    PlaceAsset(BiomeAssetGenerator.CreateSandDune($"Dune{i}"), pos, Random.Range(0f, 360f));
            }
        }

        private void PlaceSnowAssets()
        {
            Vector3 center = (playerZone + enemyZone) * 0.5f;

            // Log cabin (center)
            Vector3 cabinPos = FindValidPosition(center, 40f, 20f);
            if (cabinPos != Vector3.zero)
                PlaceAsset(BiomeAssetGenerator.CreateLogCabin("Cabin"), cabinPos, Random.Range(0f, 360f));

            // Campfires
            for (int i = 0; i < 4; i++)
            {
                Vector3 zone = (i < 2) ? playerZone : enemyZone;
                Vector3 pos = FindValidPosition(zone + Random.insideUnitSphere * zoneRadius * 0.4f, 15f, 8f);
                if (pos != Vector3.zero)
                    PlaceAsset(BiomeAssetGenerator.CreateCampfire($"Fire{i}"), pos, 0f);
            }

            // Snow drifts
            for (int i = 0; i < 6; i++)
            {
                Vector3 pos = GetRandomPositionOnMap();
                pos = FindValidPosition(pos, 25f, 15f);
                if (pos != Vector3.zero)
                    PlaceAsset(BiomeAssetGenerator.CreateSnowDrift($"Drift{i}"), pos, Random.Range(0f, 360f));
            }
        }

        private void PlaceMarshAssets()
        {
            // Water pools
            for (int i = 0; i < 6; i++)
            {
                Vector3 pos = GetRandomPositionBetweenZones();
                pos = FindValidPosition(pos, 25f, 20f);
                if (pos != Vector3.zero)
                    PlaceAsset(BiomeAssetGenerator.CreateMarshPool($"Pool{i}"), pos, Random.Range(0f, 360f));
            }

            // Wooden walkways
            for (int i = 0; i < 4; i++)
            {
                Vector3 pos = GetRandomPositionBetweenZones();
                pos = FindValidPosition(pos, 20f, 12f);
                if (pos != Vector3.zero)
                    PlaceAsset(BiomeAssetGenerator.CreateWoodenWalkway($"Walk{i}"), pos, Random.Range(0f, 180f));
            }

            // Reed clusters
            for (int i = 0; i < 8; i++)
            {
                Vector3 pos = GetRandomPositionOnMap();
                pos = FindValidPosition(pos, 15f, 8f);
                if (pos != Vector3.zero)
                    PlaceAsset(BiomeAssetGenerator.CreateReedCluster($"Reed{i}"), pos, Random.Range(0f, 360f));
            }
        }

        private void PlaceUrbanAssets()
        {
            Vector3 center = (playerZone + enemyZone) * 0.5f;

            // Town square
            Vector3 squarePos = FindValidPosition(center, 30f, 25f);
            if (squarePos != Vector3.zero)
                PlaceAsset(BiomeAssetGenerator.CreateTownSquare("Square"), squarePos, 0f);

            // Street buildings (both sides)
            for (int i = 0; i < 6; i++)
            {
                float t = (float)i / 5f;
                Vector3 pos = Vector3.Lerp(playerZone, enemyZone, t);
                pos += new Vector3(Random.Range(-30f, 30f), 0f, Random.Range(-30f, 30f));
                pos = FindValidPosition(pos, 15f, 12f);
                if (pos != Vector3.zero)
                    PlaceAsset(BiomeAssetGenerator.CreateUrbanBuilding($"Bldg{i}"), pos, Random.Range(0f, 360f));
            }

            // Barricades
            for (int i = 0; i < 5; i++)
            {
                Vector3 pos = GetRandomPositionBetweenZones();
                pos = FindValidPosition(pos, 20f, 10f);
                if (pos != Vector3.zero)
                    PlaceAsset(BiomeAssetGenerator.CreateBarricade($"Barr{i}"), pos, Random.Range(0f, 180f));
            }
        }

        private void PlaceCoastalAssets()
        {
            // Cliff face on one edge
            Vector3 cliffPos = new Vector3(terrain.terrainData.size.x * 0.9f, 0f, terrain.terrainData.size.z * 0.5f);
            cliffPos = FindValidPosition(cliffPos, 30f, 20f);
            if (cliffPos != Vector3.zero)
                PlaceAsset(BiomeAssetGenerator.CreateCliffFace("Cliff"), cliffPos, 90f);

            // Lighthouse
            Vector3 lightPos = new Vector3(terrain.terrainData.size.x * 0.85f, 0f, terrain.terrainData.size.z * 0.3f);
            lightPos = FindValidPosition(lightPos, 20f, 15f);
            if (lightPos != Vector3.zero)
                PlaceAsset(BiomeAssetGenerator.CreateLighthouse("Lighthouse"), lightPos, 0f);

            // Fishing boats
            for (int i = 0; i < 3; i++)
            {
                Vector3 pos = new Vector3(terrain.terrainData.size.x * Random.Range(0.8f, 0.95f), 0f,
                    terrain.terrainData.size.z * Random.Range(0.2f, 0.8f));
                pos = FindValidPosition(pos, 15f, 10f);
                if (pos != Vector3.zero)
                    PlaceAsset(BiomeAssetGenerator.CreateFishingBoat($"Boat{i}"), pos, Random.Range(60f, 120f));
            }
        }

        private void PlaceMountainAssets()
        {
            Vector3 center = (playerZone + enemyZone) * 0.5f;

            // Large boulder formations
            for (int i = 0; i < 8; i++)
            {
                Vector3 pos = GetRandomPositionOnMap();
                pos = FindValidPosition(pos, 30f, 15f);
                if (pos != Vector3.zero)
                    PlaceAsset(BiomeAssetGenerator.CreateLargeBoulder($"Boulder{i}"), pos, Random.Range(0f, 360f));
            }

            // Stone bridge (center)
            Vector3 bridgePos = FindValidPosition(center, 40f, 25f);
            if (bridgePos != Vector3.zero)
                PlaceAsset(BiomeAssetGenerator.CreateStoneBridge("Bridge"), bridgePos, Random.Range(0f, 180f));

            // Mountain shrine
            Vector3 shrinePos = FindValidPosition(center + new Vector3(50f, 0f, -30f), 20f, 15f);
            if (shrinePos != Vector3.zero)
                PlaceAsset(BiomeAssetGenerator.CreateMountainShrine("Shrine"), shrinePos, Random.Range(0f, 360f));
        }
        
        /// <summary>Create a defensive line with trenches, redoubts, and artillery positions</summary>
        private void CreateDefensiveLine(Vector3 zoneCenter, float radius, int teamId)
        {
            // Determine line direction (facing enemy - perpendicular to center line)
            Vector3 toEnemy = (teamId == 0 ? enemyZone : playerZone) - zoneCenter;
            toEnemy.y = 0f;
            float facingAngle = Mathf.Atan2(toEnemy.x, toEnemy.z) * Mathf.Rad2Deg;
            
            // Main defensive line position (slightly forward of zone center)
            Vector3 lineCenter = zoneCenter + toEnemy.normalized * (radius * 0.3f);
            
            // Place main redoubt (command position)
            Vector3 redoubtPos = FindValidPosition(lineCenter, radius * 0.2f, 20f);
            if (redoubtPos != Vector3.zero)
            {
                var redoubt = BattleAssetGenerator.CreateRedoubt("MainRedoubt" + teamId);
                PlaceAsset(redoubt, redoubtPos, facingAngle);
            }
            
            // Place artillery positions
            for (int i = 0; i < 3; i++)
            {
                Vector3 offset = Quaternion.Euler(0f, facingAngle + 90f, 0f) * Vector3.right * (i - 1) * 30f;
                Vector3 artPos = FindValidPosition(lineCenter + offset, 15f, 10f);
                if (artPos != Vector3.zero)
                {
                    var cannonPos = BattleAssetGenerator.CreateCannonPosition("CannonPos" + teamId + "_" + i);
                    PlaceAsset(cannonPos, artPos, facingAngle);
                    
                    // Add ammo nearby
                    Vector3 ammoPos = artPos + Random.insideUnitSphere * 5f;
                    ammoPos.y = 0f;
                    var ammo = BattleAssetGenerator.CreateAmmoStack("Ammo" + teamId + "_" + i);
                    PlaceAsset(ammo, ammoPos, Random.Range(0f, 360f));
                }
            }
            
            // Place connecting trenches
            int trenchSegments = 5;
            for (int i = 0; i < trenchSegments; i++)
            {
                float t = (float)i / (trenchSegments - 1);
                Vector3 lateralOffset = Quaternion.Euler(0f, facingAngle + 90f, 0f) * Vector3.right * (t - 0.5f) * 60f;
                Vector3 trenchPos = lineCenter + lateralOffset + toEnemy.normalized * Random.Range(-5f, 5f);
                
                trenchPos = FindValidPosition(trenchPos, 10f, 8f);
                if (trenchPos != Vector3.zero)
                {
                    var trench = BattleAssetGenerator.CreateTrenches("Trench" + teamId + "_" + i);
                    PlaceAsset(trench, trenchPos, facingAngle);
                }
            }
            
            // Place palisades between trenches
            for (int i = 0; i < 4; i++)
            {
                float t = (i + 0.5f) / 4f;
                Vector3 lateralOffset = Quaternion.Euler(0f, facingAngle + 90f, 0f) * Vector3.right * (t - 0.5f) * 60f;
                Vector3 palPos = lineCenter + lateralOffset + toEnemy.normalized * 10f;
                
                palPos = FindValidPosition(palPos, 8f, 6f);
                if (palPos != Vector3.zero)
                {
                    var palisade = BattleAssetGenerator.CreatePalisade("Palisade" + teamId + "_" + i);
                    PlaceAsset(palisade, palPos, facingAngle);
                }
            }
        }
        
        /// <summary>Create a central village with farms, church, and barns</summary>
        private void CreateCentralVillage()
        {
            Vector3 villageCenter = (playerZone + enemyZone) * 0.5f;
            villageCenter = FindValidPosition(villageCenter, 50f, 30f);
            if (villageCenter == Vector3.zero) return;
            
            // Main farm house (centerpiece)
            var farmhouse = BattleAssetGenerator.CreateFarmhouse("VillageCenter");
            PlaceAsset(farmhouse, villageCenter, Random.Range(0f, 360f));
            
            // Barn
            Vector3 barnPos = villageCenter + new Vector3(25f, 0f, 15f);
            barnPos = FindValidPosition(barnPos, 15f, 12f);
            if (barnPos != Vector3.zero)
            {
                var barn = BattleAssetGenerator.CreateBarn("VillageBarn");
                PlaceAsset(barn, barnPos, Random.Range(0f, 360f));
            }
            
            // Church tower (for visibility)
            Vector3 churchPos = villageCenter + new Vector3(-20f, 0f, -20f);
            churchPos = FindValidPosition(churchPos, 15f, 10f);
            if (churchPos != Vector3.zero)
            {
                var church = BattleAssetGenerator.CreateChurchTower("VillageChurch");
                PlaceAsset(church, churchPos, Random.Range(0f, 360f));
            }
            
            // Haystacks around the village
            for (int i = 0; i < 4; i++)
            {
                Vector3 hayPos = villageCenter + Random.insideUnitSphere * 40f;
                hayPos.y = 0f;
                hayPos = FindValidPosition(hayPos, 10f, 6f);
                if (hayPos != Vector3.zero)
                {
                    var hay = (i == 0) ? BattleAssetGenerator.CreateLargeHaystack("VillageHay" + i) 
                                       : BattleAssetGenerator.CreateHaystack("VillageHay" + i);
                    PlaceAsset(hay, hayPos, Random.Range(0f, 360f));
                }
            }
            
            // Windmill (prominent landmark)
            Vector3 windmillPos = villageCenter + new Vector3(40f, 0f, -30f);
            windmillPos = FindValidPosition(windmillPos, 15f, 12f);
            if (windmillPos != Vector3.zero)
            {
                var windmill = BattleAssetGenerator.CreateWindmill("VillageWindmill");
                PlaceAsset(windmill, windmillPos, Random.Range(0f, 360f));
            }
            
            // Stone walls around village
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f;
                Vector3 wallPos = villageCenter + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 50f;
                wallPos = FindValidPosition(wallPos, 10f, 8f);
                if (wallPos != Vector3.zero)
                {
                    var wall = BattleAssetGenerator.CreateStoneWall("VillageWall" + i);
                    PlaceAsset(wall, wallPos, angle + Random.Range(-20f, 20f));
                }
            }
        }
        
        /// <summary>Scatter natural cover like trees, hedges, and rocks</summary>
        private void ScatterNaturalCover()
        {
            // Tree clusters (flanks)
            for (int i = 0; i < 4; i++)
            {
                Vector3 zone = (i < 2) ? playerZone : enemyZone;
                float side = (i % 2 == 0) ? -1f : 1f;
                
                Vector3 treePos = zone + new Vector3(side * 60f, 0f, Random.Range(-40f, 40f));
                treePos = FindValidPosition(treePos, 20f, 15f);
                if (treePos != Vector3.zero)
                {
                    var trees = BattleAssetGenerator.CreateTreeCluster("TreeCluster" + i);
                    PlaceAsset(trees, treePos, Random.Range(0f, 360f));
                }
            }
            
            // Individual trees scattered
            for (int i = 0; i < 8; i++)
            {
                Vector3 treePos = GetRandomPositionOnMap();
                treePos = FindValidPosition(treePos, 10f, 5f);
                if (treePos != Vector3.zero)
                {
                    var tree = BattleAssetGenerator.CreateTree("Tree" + i);
                    PlaceAsset(tree, treePos, Random.Range(0f, 360f));
                }
            }
            
            // Hedges (natural boundaries)
            for (int i = 0; i < 5; i++)
            {
                Vector3 hedgePos = GetRandomPositionBetweenZones();
                hedgePos = FindValidPosition(hedgePos, 15f, 10f);
                if (hedgePos != Vector3.zero)
                {
                    var hedge = BattleAssetGenerator.CreateHedge("Hedge" + i);
                    PlaceAsset(hedge, hedgePos, Random.Range(0f, 360f));
                }
            }
            
            // Rock formations
            for (int i = 0; i < 4; i++)
            {
                Vector3 rockPos = GetRandomPositionOnMap();
                rockPos = FindValidPosition(rockPos, 15f, 8f);
                if (rockPos != Vector3.zero)
                {
                    var rocks = BattleAssetGenerator.CreateRocks("Rocks" + i);
                    PlaceAsset(rocks, rockPos, Random.Range(0f, 360f));
                }
            }
        }
        
        /// <summary>Place remaining random assets</summary>
        private void PlaceRandomAssets()
        {
            int assetsToPlace = numberOfAssets - placedPositions.Count;
            
            for (int i = 0; i < assetsToPlace; i++)
            {
                Vector3 pos = GetRandomPositionOnMap();
                pos = FindValidPosition(pos, 20f, minDistanceBetweenAssets);
                
                if (pos == Vector3.zero) continue;
                
                // Pick random asset
                var creator = assetCreators[Random.Range(0, assetCreators.Count)];
                var asset = creator();
                asset.name = asset.name + "_Random" + i;
                
                PlaceAsset(asset, pos, Random.Range(0f, 360f));
            }
        }
        
        /// <summary>Place an asset at position with given rotation</summary>
        private void PlaceAsset(GameObject asset, Vector3 position, float yRotation)
        {
            asset.transform.SetParent(assetsParent);
            asset.transform.position = position;
            asset.transform.rotation = Quaternion.Euler(0f, yRotation, 0f);

            // Snap to terrain
            float terrainHeight = terrain.SampleHeight(position);
            asset.transform.position = new Vector3(position.x, terrainHeight, position.z);

            // Tag trenches and cover objects for detection
            if (asset.name.ToLower().Contains("trench"))
            {
                SafeSetTag(asset, "Cover");
                // Add Trench component
                var trench = asset.AddComponent<TrenchComponent>();
                trench.facingDirection = Quaternion.Euler(0f, yRotation, 0f) * Vector3.forward;
                
                // Add visual gizmo helper for trenches
                var gizmo = asset.AddComponent<TrenchVisualizer>();
            }
            else if (asset.name.ToLower().Contains("wall") || 
                     asset.name.ToLower().Contains("palisade") ||
                     asset.name.ToLower().Contains("building") ||
                     asset.name.ToLower().Contains("barn") ||
                     asset.name.ToLower().Contains("hay") ||
                     asset.name.ToLower().Contains("rock"))
            {
                SafeSetTag(asset, "Cover");
            }

            // Ensure all assets have a collider for raycast detection
            if (asset.GetComponent<Collider>() == null)
            {
                var col = asset.AddComponent<BoxCollider>();
                col.size = new Vector3(8f, 3f, 8f);
            }

            // Add to placed list
            placedPositions.Add(position);
        }
        
        /// <summary>Sets a tag safely, falling back to Untagged if tag doesn't exist</summary>
        private void SafeSetTag(GameObject obj, string tag)
        {
            try
            {
                obj.tag = tag;
            }
            catch (UnityException)
            {
                // Tag not defined in Unity - create it at runtime or use default
                Debug.LogWarning($"[BattleAssetPlacer] Tag '{tag}' not defined in Unity. Using Untagged. Please add '{tag}' tag in Unity Editor > Edit > Project Settings > Tags and Layers.");
                obj.tag = "Untagged";
            }
        }

        /// <summary>Find a valid position near target that meets terrain requirements</summary>
        private Vector3 FindValidPosition(Vector3 target, float searchRadius, float minClearance)
        {
            int attempts = 20;
            
            for (int i = 0; i < attempts; i++)
            {
                Vector3 pos = target + Random.insideUnitSphere * searchRadius;
                pos.y = 0f;
                
                // Check terrain bounds
                if (pos.x < minDistanceFromEdge || pos.x > terrain.terrainData.size.x - minDistanceFromEdge)
                    continue;
                if (pos.z < minDistanceFromEdge || pos.z > terrain.terrainData.size.z - minDistanceFromEdge)
                    continue;
                
                // Check slope
                float height = terrain.SampleHeight(pos);
                float slope = terrain.terrainData.GetSteepness(pos.x / terrain.terrainData.size.x, 
                                                                  pos.z / terrain.terrainData.size.z);
                if (slope > maxSlopeAngle)
                    continue;
                
                // Check distance from other assets
                bool tooClose = false;
                foreach (var placed in placedPositions)
                {
                    if (Vector3.Distance(new Vector3(pos.x, 0f, pos.z), 
                                        new Vector3(placed.x, 0f, placed.z)) < minClearance)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;
                
                return new Vector3(pos.x, height, pos.z);
            }
            
            return Vector3.zero; // Failed to find valid position
        }
        
        /// <summary>Get random position on the map</summary>
        private Vector3 GetRandomPositionOnMap()
        {
            float x = Random.Range(minDistanceFromEdge, terrain.terrainData.size.x - minDistanceFromEdge);
            float z = Random.Range(minDistanceFromEdge, terrain.terrainData.size.z - minDistanceFromEdge);
            return new Vector3(x, 0f, z);
        }
        
        /// <summary>Get random position between player and enemy zones</summary>
        private Vector3 GetRandomPositionBetweenZones()
        {
            float t = Random.Range(0.2f, 0.8f);
            Vector3 pos = Vector3.Lerp(playerZone, enemyZone, t);
            pos += new Vector3(Random.Range(-40f, 40f), 0f, Random.Range(-40f, 40f));
            return pos;
        }
        
        /// <summary>Debug visualization of asset placement zones</summary>
        private void OnDrawGizmos()
        {
            // Player zone
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(playerZone, zoneRadius);
            
            // Enemy zone
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(enemyZone, zoneRadius);
            
            // Village center
            Gizmos.color = Color.green;
            Vector3 center = (playerZone + enemyZone) * 0.5f;
            Gizmos.DrawWireSphere(center, 30f);
        }
    }
}
