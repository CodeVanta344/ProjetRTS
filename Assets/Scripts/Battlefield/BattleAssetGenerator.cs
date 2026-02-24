using UnityEngine;
using System.Collections.Generic;

namespace NapoleonicWars.Battlefield
{
    /// <summary>
    /// Procedural generator for Napoleonic battlefield assets.
    /// Creates 20-30 different 3D objects: trenches, redoubts, barricades,
    /// farms, stone walls, wooden fences, haystacks, etc.
    /// </summary>
    public static class BattleAssetGenerator
    {
        private static Material _woodMaterial;
        private static Material _stoneMaterial;
        private static Material _earthMaterial;
        private static Material _thatchMaterial;
        private static Material _grassMaterial;
        
        public static void InitializeMaterials()
        {
            if (_woodMaterial == null)
                _woodMaterial = CreateMaterial("Wood", new Color(0.4f, 0.3f, 0.2f));
            if (_stoneMaterial == null)
                _stoneMaterial = CreateMaterial("Stone", new Color(0.5f, 0.5f, 0.55f));
            if (_earthMaterial == null)
                _earthMaterial = CreateMaterial("Earth", new Color(0.35f, 0.25f, 0.15f));
            if (_thatchMaterial == null)
                _thatchMaterial = CreateMaterial("Thatch", new Color(0.6f, 0.5f, 0.3f));
            if (_grassMaterial == null)
                _grassMaterial = CreateMaterial("Grass", new Color(0.25f, 0.45f, 0.2f));
        }
        
        private static Material CreateMaterial(string name, Color color)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.name = name;
            mat.color = color;
            mat.SetFloat("_Smoothness", 0.1f);
            return mat;
        }
        
        // ==================== DEFENSIVE STRUCTURES ====================
        
        /// <summary>1. Simple trench with parapet - IMPROVED VISIBILITY</summary>
        public static GameObject CreateTrenches(string name = "Trench")
        {
            var go = new GameObject(name);
            
            // Trench floor - darker earth for contrast
            var floorMat = CreateMaterial("TrenchFloor", new Color(0.2f, 0.15f, 0.1f));
            var floor = CreateMeshPart(go.transform, "Floor", floorMat);
            floor.transform.localScale = new Vector3(12f, 0.3f, 3f);
            floor.transform.localPosition = new Vector3(0f, -0.2f, 0f);
            
            // Left parapet - taller and more visible
            var leftWall = CreateMeshPart(go.transform, "LeftParapet", _earthMaterial);
            leftWall.transform.localScale = new Vector3(2f, 2f, 3.5f);
            leftWall.transform.localPosition = new Vector3(-5f, 0.8f, 0f);
            leftWall.transform.rotation = Quaternion.Euler(0f, 0f, -15f);
            
            // Right parapet
            var rightWall = CreateMeshPart(go.transform, "RightParapet", _earthMaterial);
            rightWall.transform.localScale = new Vector3(2f, 2f, 3.5f);
            rightWall.transform.localPosition = new Vector3(5f, 0.8f, 0f);
            rightWall.transform.rotation = Quaternion.Euler(0f, 0f, 15f);
            
            // Add front parapet (facing enemy)
            var frontWall = CreateMeshPart(go.transform, "FrontParapet", _earthMaterial);
            frontWall.transform.localScale = new Vector3(8f, 1.5f, 1f);
            frontWall.transform.localPosition = new Vector3(0f, 0.5f, 1.5f);
            
            // Add collider for the trench - larger for better detection
            var collider = go.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.5f, 0f);
            collider.size = new Vector3(14f, 3f, 5f);
            collider.isTrigger = false;
            
            return go;
        }
        
        /// <summary>2. Redoubt (star-shaped earthwork)</summary>
        public static GameObject CreateRedoubt(string name = "Redoubt")
        {
            var go = new GameObject(name);
            
            // Central platform
            var center = CreateMeshPart(go.transform, "Center", _earthMaterial);
            center.transform.localScale = new Vector3(8f, 2f, 8f);
            center.transform.localPosition = new Vector3(0f, 1f, 0f);
            
            // 5 angled walls in star formation
            for (int i = 0; i < 5; i++)
            {
                float angle = i * 72f * Mathf.Deg2Rad;
                var wall = CreateMeshPart(go.transform, $"Wall{i}", _earthMaterial);
                wall.transform.localScale = new Vector3(6f, 2.5f, 1.5f);
                wall.transform.localPosition = new Vector3(
                    Mathf.Sin(angle) * 6f, 1.25f, Mathf.Cos(angle) * 6f);
                wall.transform.rotation = Quaternion.Euler(0f, -i * 72f + 90f, 15f);
            }
            
            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(20f, 4f, 20f);
            
            return go;
        }
        
        /// <summary>3. Wooden palisade/barricade</summary>
        public static GameObject CreatePalisade(string name = "Palisade")
        {
            var go = new GameObject(name);
            
            // Multiple wooden stakes
            for (int i = 0; i < 8; i++)
            {
                var stake = CreateMeshPart(go.transform, $"Stake{i}", _woodMaterial);
                float x = (i - 3.5f) * 1.2f;
                float height = Random.Range(2f, 2.5f);
                float tilt = Random.Range(-5f, 5f);
                
                stake.transform.localScale = new Vector3(0.15f, height, 0.15f);
                stake.transform.localPosition = new Vector3(x, height * 0.5f, 0f);
                stake.transform.rotation = Quaternion.Euler(0f, 0f, tilt);
            }
            
            // Horizontal beam
            var beam = CreateMeshPart(go.transform, "Beam", _woodMaterial);
            beam.transform.localScale = new Vector3(10f, 0.2f, 0.3f);
            beam.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            
            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(10f, 2.5f, 0.5f);
            
            return go;
        }
        
        /// <summary>4. Stone wall (low cover)</summary>
        public static GameObject CreateStoneWall(string name = "StoneWall")
        {
            var go = new GameObject(name);
            
            // Irregular stone blocks
            for (int i = 0; i < 12; i++)
            {
                var stone = CreateMeshPart(go.transform, $"Stone{i}", _stoneMaterial);
                float x = (i - 5.5f) * 0.8f + Random.Range(-0.1f, 0.1f);
                float y = Random.Range(0.4f, 0.8f);
                float z = Random.Range(-0.1f, 0.1f);
                
                stone.transform.localScale = new Vector3(
                    Random.Range(0.6f, 0.9f), 
                    y, 
                    Random.Range(0.4f, 0.6f));
                stone.transform.localPosition = new Vector3(x, y * 0.5f, z);
                stone.transform.rotation = Quaternion.Euler(
                    Random.Range(-3f, 3f), 
                    Random.Range(-5f, 5f), 
                    Random.Range(-3f, 3f));
            }
            
            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(10f, 1f, 0.6f);
            
            return go;
        }
        
        /// <summary>5. Wooden fence (broken/rural)</summary>
        public static GameObject CreateWoodenFence(string name = "Fence")
        {
            var go = new GameObject(name);
            
            for (int i = 0; i < 6; i++)
            {
                // Posts
                var post = CreateMeshPart(go.transform, $"Post{i}", _woodMaterial);
                float x = (i - 2.5f) * 2f;
                post.transform.localScale = new Vector3(0.12f, 1.8f, 0.12f);
                post.transform.localPosition = new Vector3(x, 0.9f, 0f);
                
                // Rails (some missing/broken)
                if (i < 5 && Random.value > 0.2f)
                {
                    var rail = CreateMeshPart(go.transform, $"Rail{i}", _woodMaterial);
                    rail.transform.localScale = new Vector3(2f, 0.08f, 0.1f);
                    rail.transform.localPosition = new Vector3(x + 1f, 1.4f, 0f);
                    rail.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-5f, 5f));
                }
            }
            
            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(12f, 2f, 0.3f);
            
            return go;
        }
        
        // ==================== BUILDINGS ====================
        
        /// <summary>6. Small farmhouse</summary>
        public static GameObject CreateFarmhouse(string name = "Farmhouse")
        {
            var go = new GameObject(name);
            
            // Main house
            var house = CreateMeshPart(go.transform, "House", _stoneMaterial);
            house.transform.localScale = new Vector3(8f, 5f, 6f);
            house.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            
            // Roof
            var roof = CreateMeshPart(go.transform, "Roof", _thatchMaterial);
            roof.transform.localScale = new Vector3(9f, 3f, 7f);
            roof.transform.localPosition = new Vector3(0f, 6.5f, 0f);
            roof.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            
            // Chimney
            var chimney = CreateMeshPart(go.transform, "Chimney", _stoneMaterial);
            chimney.transform.localScale = new Vector3(0.8f, 3f, 0.8f);
            chimney.transform.localPosition = new Vector3(2f, 7f, -1f);
            
            // Door
            var door = CreateMeshPart(go.transform, "Door", _woodMaterial);
            door.transform.localScale = new Vector3(1.2f, 2.2f, 0.1f);
            door.transform.localPosition = new Vector3(0f, 1.1f, 3f);
            
            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(10f, 9f, 8f);
            
            return go;
        }
        
        /// <summary>7. Barn</summary>
        public static GameObject CreateBarn(string name = "Barn")
        {
            var go = new GameObject(name);
            
            var main = CreateMeshPart(go.transform, "Main", _woodMaterial);
            main.transform.localScale = new Vector3(12f, 6f, 8f);
            main.transform.localPosition = new Vector3(0f, 3f, 0f);
            
            var roof = CreateMeshPart(go.transform, "Roof", _thatchMaterial);
            roof.transform.localScale = new Vector3(13f, 4f, 9f);
            roof.transform.localPosition = new Vector3(0f, 8f, 0f);
            roof.transform.rotation = Quaternion.Euler(0f, 0f, 35f);
            
            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(14f, 10f, 10f);
            
            return go;
        }
        
        /// <summary>8. Windmill</summary>
        public static GameObject CreateWindmill(string name = "Windmill")
        {
            var go = new GameObject(name);
            
            // Tower
            var tower = CreateMeshPart(go.transform, "Tower", _stoneMaterial);
            tower.transform.localScale = new Vector3(4f, 10f, 4f);
            tower.transform.localPosition = new Vector3(0f, 5f, 0f);
            
            // Roof
            var roof = CreateMeshPart(go.transform, "Roof", _thatchMaterial);
            roof.transform.localScale = new Vector3(5f, 3f, 5f);
            roof.transform.localPosition = new Vector3(0f, 11.5f, 0f);
            
            // Blades (4 blades)
            for (int i = 0; i < 4; i++)
            {
                var blade = CreateMeshPart(go.transform, $"Blade{i}", _woodMaterial);
                float angle = i * 90f;
                blade.transform.localScale = new Vector3(0.2f, 6f, 1f);
                blade.transform.localPosition = new Vector3(0f, 11f, 0f);
                blade.transform.rotation = Quaternion.Euler(0f, angle, 0f);
            }
            
            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(12f, 14f, 12f);
            
            return go;
        }
        
        /// <summary>9. Church tower</summary>
        public static GameObject CreateChurchTower(string name = "Church")
        {
            var go = new GameObject(name);
            
            // Base
            var base_ = CreateMeshPart(go.transform, "Base", _stoneMaterial);
            base_.transform.localScale = new Vector3(6f, 12f, 6f);
            base_.transform.localPosition = new Vector3(0f, 6f, 0f);
            
            // Spire
            var spire = CreateMeshPart(go.transform, "Spire", _stoneMaterial);
            spire.transform.localScale = new Vector3(3f, 8f, 3f);
            spire.transform.localPosition = new Vector3(0f, 16f, 0f);
            spire.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            
            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(8f, 24f, 8f);
            
            return go;
        }
        
        /// <summary>10. Ruined building</summary>
        public static GameObject CreateRuin(string name = "Ruin")
        {
            var go = new GameObject(name);
            
            // Partial walls
            for (int i = 0; i < 4; i++)
            {
                if (Random.value > 0.3f) // Some walls missing
                {
                    var wall = CreateMeshPart(go.transform, $"Wall{i}", _stoneMaterial);
                    float height = Random.Range(2f, 5f);
                    wall.transform.localScale = new Vector3(5f, height, 0.6f);
                    wall.transform.localPosition = new Vector3(
                        (i % 2 == 0 ? 0 : Random.Range(-3f, 3f)), 
                        height * 0.5f, 
                        (i % 2 == 1 ? 0 : Random.Range(-3f, 3f)));
                    wall.transform.rotation = Quaternion.Euler(0f, i * 90f, Random.Range(-10f, 10f));
                }
            }
            
            // Rubble
            for (int i = 0; i < 8; i++)
            {
                var rubble = CreateMeshPart(go.transform, $"Rubble{i}", _stoneMaterial);
                rubble.transform.localScale = new Vector3(
                    Random.Range(0.5f, 1.5f),
                    Random.Range(0.3f, 0.8f),
                    Random.Range(0.5f, 1.5f));
                rubble.transform.localPosition = new Vector3(
                    Random.Range(-4f, 4f),
                    rubble.transform.localScale.y * 0.5f,
                    Random.Range(-4f, 4f));
            }
            
            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(10f, 6f, 10f);
            
            return go;
        }
        
        // ==================== NATURAL / COVER ====================
        
        /// <summary>11. Haystack</summary>
        public static GameObject CreateHaystack(string name = "Haystack")
        {
            var go = new GameObject(name);
            
            var hay = CreateMeshPart(go.transform, "Hay", _thatchMaterial);
            hay.transform.localScale = new Vector3(3f, 2.5f, 3f);
            hay.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            hay.transform.rotation = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-10f, 10f));
            
            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(3.5f, 3f, 3.5f);
            
            return go;
        }
        
        /// <summary>12. Large haystack</summary>
        public static GameObject CreateLargeHaystack(string name = "LargeHaystack")
        {
            var go = new GameObject(name);
            
            // Main stack
            var main = CreateMeshPart(go.transform, "Main", _thatchMaterial);
            main.transform.localScale = new Vector3(5f, 4f, 5f);
            main.transform.localPosition = new Vector3(0f, 2f, 0f);
            
            // Smaller stacks around
            for (int i = 0; i < 3; i++)
            {
                var small = CreateMeshPart(go.transform, $"Small{i}", _thatchMaterial);
                float angle = i * 120f * Mathf.Deg2Rad;
                small.transform.localScale = new Vector3(2f, 1.5f, 2f);
                small.transform.localPosition = new Vector3(
                    Mathf.Sin(angle) * 3f, 0.75f, Mathf.Cos(angle) * 3f);
            }
            
            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(8f, 5f, 8f);
            
            return go;
        }
        
        /// <summary>13. Tree (simple stylized)</summary>
        public static GameObject CreateTree(string name = "Tree")
        {
            var go = new GameObject(name);
            
            // Trunk
            var trunk = CreateMeshPart(go.transform, "Trunk", _woodMaterial);
            trunk.transform.localScale = new Vector3(0.6f, 4f, 0.6f);
            trunk.transform.localPosition = new Vector3(0f, 2f, 0f);
            
            // Foliage (multiple spheres/cubes for canopy)
            for (int i = 0; i < 5; i++)
            {
                var foliage = CreateMeshPart(go.transform, $"Foliage{i}", _grassMaterial);
                float angle = i * 72f * Mathf.Deg2Rad;
                float radius = Random.Range(1f, 2f);
                foliage.transform.localScale = new Vector3(2f, 2f, 2f);
                foliage.transform.localPosition = new Vector3(
                    Mathf.Sin(angle) * radius * 0.5f,
                    4.5f + Random.Range(-0.5f, 0.5f),
                    Mathf.Cos(angle) * radius * 0.5f);
            }
            
            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(4f, 7f, 4f);
            
            return go;
        }
        
        /// <summary>14. Dense copse of trees</summary>
        public static GameObject CreateTreeCluster(string name = "TreeCluster")
        {
            var go = new GameObject(name);
            
            for (int i = 0; i < 6; i++)
            {
                var tree = CreateTree($"Tree{i}");
                tree.transform.SetParent(go.transform);
                float angle = i * 60f * Mathf.Deg2Rad;
                float dist = Random.Range(2f, 4f);
                tree.transform.localPosition = new Vector3(
                    Mathf.Sin(angle) * dist, 0f, Mathf.Cos(angle) * dist);
                float scale = Random.Range(0.7f, 1.3f);
                tree.transform.localScale = Vector3.one * scale;
            }
            
            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(12f, 8f, 12f);
            
            return go;
        }
        
        /// <summary>15. Bush/hedge</summary>
        public static GameObject CreateHedge(string name = "Hedge")
        {
            var go = new GameObject(name);
            
            // Multiple connected bushes
            for (int i = 0; i < 5; i++)
            {
                var bush = CreateMeshPart(go.transform, $"Bush{i}", _grassMaterial);
                float x = (i - 2f) * 1.5f + Random.Range(-0.3f, 0.3f);
                float scale = Random.Range(1f, 1.5f);
                bush.transform.localScale = new Vector3(scale, scale * 0.8f, scale);
                bush.transform.localPosition = new Vector3(x, scale * 0.4f, Random.Range(-0.2f, 0.2f));
            }
            
            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(8f, 2f, 2f);
            
            return go;
        }
        
        /// <summary>16. Rock formation</summary>
        public static GameObject CreateRocks(string name = "Rocks")
        {
            var go = new GameObject(name);
            
            for (int i = 0; i < 5; i++)
            {
                var rock = CreateMeshPart(go.transform, $"Rock{i}", _stoneMaterial);
                float scale = Random.Range(0.8f, 2f);
                rock.transform.localScale = new Vector3(
                    scale, scale * Random.Range(0.6f, 1f), scale);
                rock.transform.localPosition = new Vector3(
                    Random.Range(-3f, 3f),
                    rock.transform.localScale.y * 0.5f,
                    Random.Range(-3f, 3f));
                rock.transform.rotation = Quaternion.Euler(
                    Random.Range(-15f, 15f),
                    Random.Range(0f, 360f),
                    Random.Range(-15f, 15f));
            }
            
            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(8f, 3f, 8f);
            
            return go;
        }
        
        /// <summary>17. Crater (shell impact)</summary>
        public static GameObject CreateCrater(string name = "Crater")
        {
            var go = new GameObject(name);
            
            // Depressed center
            var center = CreateMeshPart(go.transform, "Center", _earthMaterial);
            center.transform.localScale = new Vector3(4f, 0.5f, 4f);
            center.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            
            // Rim of earth
            for (int i = 0; i < 8; i++)
            {
                var rim = CreateMeshPart(go.transform, $"Rim{i}", _earthMaterial);
                float angle = i * 45f * Mathf.Deg2Rad;
                rim.transform.localScale = new Vector3(1.5f, 0.8f, 1.5f);
                rim.transform.localPosition = new Vector3(
                    Mathf.Sin(angle) * 2.5f, 0.4f, Mathf.Cos(angle) * 2.5f);
            }
            
            var collider = go.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, -0.5f, 0f);
            collider.size = new Vector3(6f, 2f, 6f);
            
            return go;
        }
        
        // ==================== MILITARY EQUIPMENT ====================
        
        /// <summary>18. Cannon emplacement</summary>
        public static GameObject CreateCannonPosition(string name = "CannonPosition")
        {
            var go = new GameObject(name);
            
            // Earth platform
            var platform = CreateMeshPart(go.transform, "Platform", _earthMaterial);
            platform.transform.localScale = new Vector3(4f, 0.5f, 4f);
            platform.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            
            // Gabions (wicker baskets filled with earth)
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f * Mathf.Deg2Rad;
                var gabion = CreateMeshPart(go.transform, $"Gabion{i}", _earthMaterial);
                gabion.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
                gabion.transform.localPosition = new Vector3(
                    Mathf.Sin(angle) * 2f, 0.5f, Mathf.Cos(angle) * 2f);
            }
            
            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(6f, 2f, 6f);
            
            return go;
        }
        
        /// <summary>19. Ammunition stack</summary>
        public static GameObject CreateAmmoStack(string name = "AmmoStack")
        {
            var go = new GameObject(name);
            
            // Crates/boxes
            for (int i = 0; i < 12; i++)
            {
                var crate = CreateMeshPart(go.transform, $"Crate{i}", _woodMaterial);
                int row = i / 4;
                int col = i % 4;
                crate.transform.localScale = new Vector3(0.6f, 0.4f, 0.6f);
                crate.transform.localPosition = new Vector3(
                    (col - 1.5f) * 0.7f,
                    0.2f + row * 0.4f,
                    (row % 2 == 0 ? 0 : 0.3f));
            }
            
            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(3f, 2f, 2f);
            
            return go;
        }
        
        /// <summary>20. Supply wagon</summary>
        public static GameObject CreateSupplyWagon(string name = "Wagon")
        {
            var go = new GameObject(name);
            
            // Wagon body
            var body = CreateMeshPart(go.transform, "Body", _woodMaterial);
            body.transform.localScale = new Vector3(3f, 1.5f, 1.8f);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            
            // Wheels
            for (int i = 0; i < 4; i++)
            {
                var wheel = CreateMeshPart(go.transform, $"Wheel{i}", _woodMaterial);
                float x = (i < 2 ? -1.2f : 1.2f);
                float z = (i % 2 == 0 ? -1f : 1f);
                wheel.transform.localScale = new Vector3(0.15f, 0.8f, 0.8f);
                wheel.transform.localPosition = new Vector3(x, 0.4f, z);
                wheel.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            }
            
            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(4f, 2.5f, 3f);
            
            return go;
        }
        
        /// <summary>21. Tent (officer/command)</summary>
        public static GameObject CreateTent(string name = "Tent")
        {
            var go = new GameObject(name);
            
            // Tent canvas (pyramid)
            var canvas = CreateMeshPart(go.transform, "Canvas", _thatchMaterial);
            canvas.transform.localScale = new Vector3(4f, 2.5f, 4f);
            canvas.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            canvas.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            
            // Center pole
            var pole = CreateMeshPart(go.transform, "Pole", _woodMaterial);
            pole.transform.localScale = new Vector3(0.1f, 2.8f, 0.1f);
            pole.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            
            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(5f, 3f, 5f);
            
            return go;
        }
        
        /// <summary>22. Campfire</summary>
        public static GameObject CreateCampfire(string name = "Campfire")
        {
            var go = new GameObject(name);
            
            // Stones ring
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                var stone = CreateMeshPart(go.transform, $"Stone{i}", _stoneMaterial);
                stone.transform.localScale = new Vector3(0.3f, 0.2f, 0.3f);
                stone.transform.localPosition = new Vector3(
                    Mathf.Sin(angle) * 0.8f, 0.1f, Mathf.Cos(angle) * 0.8f);
            }
            
            // Logs
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                var log = CreateMeshPart(go.transform, $"Log{i}", _woodMaterial);
                log.transform.localScale = new Vector3(0.15f, 0.15f, 0.8f);
                log.transform.localPosition = new Vector3(
                    Mathf.Sin(angle) * 0.3f, 0.1f, Mathf.Cos(angle) * 0.3f);
                log.transform.rotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);
            }
            
            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(2f, 0.5f, 2f);
            
            return go;
        }
        
        /// <summary>23. Obstacle (sharpened stakes - abatis)</summary>
        public static GameObject CreateAbatis(string name = "Abatis")
        {
            var go = new GameObject(name);
            
            for (int i = 0; i < 15; i++)
            {
                var stake = CreateMeshPart(go.transform, $"Stake{i}", _woodMaterial);
                float angle = Random.Range(0f, 360f);
                float dist = Random.Range(0f, 2f);
                stake.transform.localScale = new Vector3(0.1f, 2f, 0.1f);
                stake.transform.localPosition = new Vector3(
                    Mathf.Sin(angle * Mathf.Deg2Rad) * dist,
                    0.5f,
                    Mathf.Cos(angle * Mathf.Deg2Rad) * dist);
                stake.transform.rotation = Quaternion.Euler(
                    Random.Range(30f, 70f), angle, Random.Range(-20f, 20f));
            }
            
            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(5f, 2f, 5f);
            
            return go;
        }
        
        /// <summary>24. Small bridge</summary>
        public static GameObject CreateBridge(string name = "Bridge")
        {
            var go = new GameObject(name);
            
            // Deck
            var deck = CreateMeshPart(go.transform, "Deck", _woodMaterial);
            deck.transform.localScale = new Vector3(8f, 0.2f, 3f);
            deck.transform.localPosition = new Vector3(0f, 1f, 0f);
            
            // Railings
            var leftRail = CreateMeshPart(go.transform, "LeftRail", _woodMaterial);
            leftRail.transform.localScale = new Vector3(8f, 0.8f, 0.1f);
            leftRail.transform.localPosition = new Vector3(0f, 1.5f, -1.4f);
            
            var rightRail = CreateMeshPart(go.transform, "RightRail", _woodMaterial);
            rightRail.transform.localScale = new Vector3(8f, 0.8f, 0.1f);
            rightRail.transform.localPosition = new Vector3(0f, 1.5f, 1.4f);
            
            // Supports
            for (int i = 0; i < 3; i++)
            {
                var support = CreateMeshPart(go.transform, $"Support{i}", _stoneMaterial);
                float x = (i - 1) * 3f;
                support.transform.localScale = new Vector3(0.6f, 1f, 2f);
                support.transform.localPosition = new Vector3(x, 0.5f, 0f);
            }
            
            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(10f, 2f, 4f);
            
            return go;
        }
        
        /// <summary>25. Field hospital (tent with red cross flag)</summary>
        public static GameObject CreateFieldHospital(string name = "FieldHospital")
        {
            var go = new GameObject(name);
            
            // Large tent
            var tent = CreateTent("Tent");
            tent.transform.SetParent(go.transform);
            tent.transform.localScale = new Vector3(1.5f, 1.2f, 1.5f);
            
            // Flag pole
            var pole = CreateMeshPart(go.transform, "FlagPole", _woodMaterial);
            pole.transform.localScale = new Vector3(0.08f, 4f, 0.08f);
            pole.transform.localPosition = new Vector3(3f, 2f, 3f);
            
            // Flag (white for hospital)
            var flag = CreateMeshPart(go.transform, "Flag", CreateMaterial("White", Color.white));
            flag.transform.localScale = new Vector3(0.1f, 1f, 1.5f);
            flag.transform.localPosition = new Vector3(3f, 3.5f, 3.75f);
            
            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(8f, 5f, 8f);
            
            return go;
        }
        
        // ==================== HELPER ====================
        
        private static GameObject CreateMeshPart(Transform parent, string name, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            
            var renderer = go.GetComponent<Renderer>();
            if (material != null) renderer.material = material;
            
            return go;
        }
        
        /// <summary>Get all available asset types</summary>
        public static List<System.Func<GameObject>> GetAllAssetCreators()
        {
            InitializeMaterials();
            
            var list = new List<System.Func<GameObject>>();
            list.Add(() => CreateTrenches());
            list.Add(() => CreateRedoubt());
            list.Add(() => CreatePalisade());
            list.Add(() => CreateStoneWall());
            list.Add(() => CreateWoodenFence());
            list.Add(() => CreateFarmhouse());
            list.Add(() => CreateBarn());
            list.Add(() => CreateWindmill());
            list.Add(() => CreateChurchTower());
            list.Add(() => CreateRuin());
            list.Add(() => CreateHaystack());
            list.Add(() => CreateLargeHaystack());
            list.Add(() => CreateTree());
            list.Add(() => CreateTreeCluster());
            list.Add(() => CreateHedge());
            list.Add(() => CreateRocks());
            list.Add(() => CreateCrater());
            list.Add(() => CreateCannonPosition());
            list.Add(() => CreateAmmoStack());
            list.Add(() => CreateSupplyWagon());
            list.Add(() => CreateTent());
            list.Add(() => CreateCampfire());
            list.Add(() => CreateAbatis());
            list.Add(() => CreateBridge());
            list.Add(() => CreateFieldHospital());
            return list;
        }
    }
}
