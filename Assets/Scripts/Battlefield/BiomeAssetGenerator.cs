using UnityEngine;

namespace NapoleonicWars.Battlefield
{
    /// <summary>
    /// Generates biome-specific blocking assets (simple colored primitives).
    /// These are placeholder meshes to be replaced with proper 3D models later.
    /// </summary>
    public static class BiomeAssetGenerator
    {
        // Shared materials (lazily initialized)
        private static Material _sandMat, _sandDarkMat, _snowMat, _snowDarkMat;
        private static Material _waterMat, _mudMat, _reedMat, _woodDarkMat;
        private static Material _stoneLightMat, _stoneDarkMat, _plasterMat;
        private static Material _canvasMat, _metalMat, _fireMat;

        private static Material GetOrCreate(ref Material field, string name, Color color, float smoothness = 0.1f)
        {
            if (field == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                field = new Material(shader);
                field.name = name;
                field.color = color;
                field.SetFloat("_Smoothness", smoothness);
            }
            return field;
        }

        private static GameObject Box(Transform parent, string name, Material mat, Vector3 scale, Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.localScale = scale;
            go.transform.localPosition = pos;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static GameObject Sphere(Transform parent, string name, Material mat, Vector3 scale, Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.localScale = scale;
            go.transform.localPosition = pos;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static GameObject Cylinder(Transform parent, string name, Material mat, Vector3 scale, Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.localScale = scale;
            go.transform.localPosition = pos;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        // ==================== DESERT ====================

        public static GameObject CreateDesertTent(string name = "DesertTent")
        {
            var go = new GameObject(name);
            var canvas = GetOrCreate(ref _canvasMat, "Canvas", new Color(0.85f, 0.78f, 0.60f));
            var wood = GetOrCreate(ref _woodDarkMat, "WoodDark", new Color(0.35f, 0.25f, 0.15f));

            // Tent body (wide flat pyramid approximated by scaled cube)
            Box(go.transform, "Body", canvas, new Vector3(8f, 4f, 6f), new Vector3(0f, 2f, 0f));
            // Tent peak (narrower cube on top)
            Box(go.transform, "Peak", canvas, new Vector3(2f, 2f, 7f), new Vector3(0f, 5f, 0f));
            // Support poles
            Cylinder(go.transform, "Pole1", wood, new Vector3(0.2f, 3f, 0.2f), new Vector3(-3.5f, 3f, 0f));
            Cylinder(go.transform, "Pole2", wood, new Vector3(0.2f, 3f, 0.2f), new Vector3(3.5f, 3f, 0f));

            go.AddComponent<BoxCollider>().size = new Vector3(8f, 6f, 6f);
            return go;
        }

        public static GameObject CreateOasis(string name = "Oasis")
        {
            var go = new GameObject(name);
            var water = GetOrCreate(ref _waterMat, "Water", new Color(0.15f, 0.35f, 0.50f), 0.8f);
            var sand = GetOrCreate(ref _sandMat, "Sand", new Color(0.75f, 0.65f, 0.42f));
            var green = GetOrCreate(ref _reedMat, "PalmGreen", new Color(0.20f, 0.42f, 0.12f));
            var wood = GetOrCreate(ref _woodDarkMat, "WoodDark", new Color(0.35f, 0.25f, 0.15f));

            // Water pool (flat cylinder)
            Cylinder(go.transform, "Pool", water, new Vector3(15f, 0.2f, 15f), Vector3.zero);
            // Sandy rim
            Cylinder(go.transform, "Rim", sand, new Vector3(18f, 0.3f, 18f), new Vector3(0f, -0.1f, 0f));
            // Palm trees (3)
            for (int i = 0; i < 3; i++)
            {
                float angle = i * 120f * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Sin(angle) * 8f, 0f, Mathf.Cos(angle) * 8f);
                Cylinder(go.transform, $"Trunk{i}", wood, new Vector3(0.4f, 4f, 0.4f), p + Vector3.up * 4f);
                Sphere(go.transform, $"Canopy{i}", green, new Vector3(4f, 2f, 4f), p + Vector3.up * 9f);
            }

            return go;
        }

        public static GameObject CreateSandDune(string name = "SandDune")
        {
            var go = new GameObject(name);
            var sand = GetOrCreate(ref _sandMat, "Sand", new Color(0.75f, 0.65f, 0.42f));

            // Large elongated mound
            Sphere(go.transform, "Mound", sand,
                new Vector3(20f + Random.Range(-4f, 4f), 3f + Random.Range(-1f, 1f), 10f + Random.Range(-2f, 2f)),
                Vector3.up * 1.5f);

            return go;
        }

        // ==================== SNOW ====================

        public static GameObject CreateLogCabin(string name = "LogCabin")
        {
            var go = new GameObject(name);
            var wood = GetOrCreate(ref _woodDarkMat, "WoodDark", new Color(0.35f, 0.25f, 0.15f));
            var snow = GetOrCreate(ref _snowMat, "Snow", new Color(0.90f, 0.92f, 0.95f), 0.4f);

            // Cabin body
            Box(go.transform, "Body", wood, new Vector3(10f, 5f, 8f), new Vector3(0f, 2.5f, 0f));
            // Snow-covered roof
            Box(go.transform, "Roof", snow, new Vector3(11f, 1.5f, 9f), new Vector3(0f, 5.75f, 0f));
            // Chimney
            Box(go.transform, "Chimney", GetOrCreate(ref _stoneDarkMat, "StoneDark", new Color(0.38f, 0.36f, 0.34f)),
                new Vector3(1.2f, 3f, 1.2f), new Vector3(3f, 7f, -2f));
            // Door
            Box(go.transform, "Door", wood, new Vector3(1.5f, 2.5f, 0.2f), new Vector3(0f, 1.25f, 4f));

            go.AddComponent<BoxCollider>().size = new Vector3(10f, 6f, 8f);
            return go;
        }

        public static GameObject CreateCampfire(string name = "Campfire")
        {
            var go = new GameObject(name);
            var stone = GetOrCreate(ref _stoneDarkMat, "StoneDark", new Color(0.38f, 0.36f, 0.34f));
            var fire = GetOrCreate(ref _fireMat, "Fire", new Color(1f, 0.6f, 0.1f), 0.0f);
            var wood = GetOrCreate(ref _woodDarkMat, "WoodDark", new Color(0.35f, 0.25f, 0.15f));

            // Stone ring
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                Sphere(go.transform, $"Stone{i}", stone,
                    new Vector3(0.5f, 0.4f, 0.5f),
                    new Vector3(Mathf.Sin(a) * 1.2f, 0.2f, Mathf.Cos(a) * 1.2f));
            }
            // Fire glow
            Sphere(go.transform, "Flame", fire, new Vector3(1f, 1.5f, 1f), new Vector3(0f, 0.8f, 0f));
            // Logs
            Cylinder(go.transform, "Log1", wood, new Vector3(0.2f, 1f, 0.2f), new Vector3(0.3f, 0.15f, 0f));
            Cylinder(go.transform, "Log2", wood, new Vector3(0.2f, 1f, 0.2f), new Vector3(-0.3f, 0.15f, 0.2f));

            return go;
        }

        public static GameObject CreateSnowDrift(string name = "SnowDrift")
        {
            var go = new GameObject(name);
            var snow = GetOrCreate(ref _snowMat, "Snow", new Color(0.90f, 0.92f, 0.95f), 0.4f);

            Sphere(go.transform, "Drift", snow,
                new Vector3(12f + Random.Range(-3f, 3f), 2f + Random.Range(-0.5f, 0.5f), 6f + Random.Range(-1f, 1f)),
                Vector3.up);

            return go;
        }

        // ==================== MARSH ====================

        public static GameObject CreateMarshPool(string name = "MarshPool")
        {
            var go = new GameObject(name);
            var water = GetOrCreate(ref _waterMat, "Water", new Color(0.15f, 0.35f, 0.50f), 0.8f);
            var mud = GetOrCreate(ref _mudMat, "Mud", new Color(0.20f, 0.18f, 0.12f), 0.5f);

            // Muddy water (flat dark disc)
            Cylinder(go.transform, "Water", water, new Vector3(12f, 0.15f, 12f), Vector3.zero);
            // Mud rim
            Cylinder(go.transform, "MudRim", mud, new Vector3(14f, 0.1f, 14f), new Vector3(0f, -0.05f, 0f));

            return go;
        }

        public static GameObject CreateWoodenWalkway(string name = "WoodenWalkway")
        {
            var go = new GameObject(name);
            var wood = GetOrCreate(ref _woodDarkMat, "WoodDark", new Color(0.35f, 0.25f, 0.15f));

            // Planks
            for (int i = 0; i < 8; i++)
            {
                Box(go.transform, $"Plank{i}", wood,
                    new Vector3(3f, 0.15f, 0.6f),
                    new Vector3(0f, 0.3f, (i - 3.5f) * 0.8f));
            }
            // Support stilts
            for (int i = 0; i < 4; i++)
            {
                Cylinder(go.transform, $"Stilt{i}", wood,
                    new Vector3(0.15f, 0.5f, 0.15f),
                    new Vector3((i % 2 == 0 ? -1.2f : 1.2f), 0.1f, (i < 2 ? -2f : 2f)));
            }

            go.AddComponent<BoxCollider>().size = new Vector3(3f, 0.5f, 7f);
            return go;
        }

        public static GameObject CreateReedCluster(string name = "ReedCluster")
        {
            var go = new GameObject(name);
            var reed = GetOrCreate(ref _reedMat, "Reed", new Color(0.35f, 0.45f, 0.20f));

            for (int i = 0; i < 12; i++)
            {
                float x = Random.Range(-1.5f, 1.5f);
                float z = Random.Range(-1.5f, 1.5f);
                float h = Random.Range(1.5f, 3f);
                Cylinder(go.transform, $"Reed{i}", reed,
                    new Vector3(0.06f, h * 0.5f, 0.06f),
                    new Vector3(x, h * 0.5f, z));
            }

            return go;
        }

        // ==================== URBAN ====================

        public static GameObject CreateUrbanBuilding(string name = "UrbanBuilding")
        {
            var go = new GameObject(name);
            var plaster = GetOrCreate(ref _plasterMat, "Plaster", new Color(0.72f, 0.68f, 0.60f));
            var stone = GetOrCreate(ref _stoneLightMat, "StoneLight", new Color(0.55f, 0.52f, 0.48f));
            var wood = GetOrCreate(ref _woodDarkMat, "WoodDark", new Color(0.35f, 0.25f, 0.15f));

            float floors = Random.Range(2, 5);
            float height = floors * 3.5f;
            float width = Random.Range(6f, 12f);
            float depth = Random.Range(5f, 9f);

            // Main structure
            Box(go.transform, "Walls", plaster, new Vector3(width, height, depth), new Vector3(0f, height * 0.5f, 0f));
            // Roof
            Box(go.transform, "Roof", stone, new Vector3(width + 0.5f, 1f, depth + 0.5f), new Vector3(0f, height + 0.5f, 0f));
            // Door
            Box(go.transform, "Door", wood, new Vector3(1.5f, 2.5f, 0.2f), new Vector3(0f, 1.25f, depth * 0.5f));
            // Windows
            for (int f = 0; f < (int)floors; f++)
            {
                float wy = 2f + f * 3.5f;
                Box(go.transform, $"WinL{f}", stone, new Vector3(0.15f, 1.2f, 0.8f), new Vector3(-width * 0.5f, wy, 0f));
                Box(go.transform, $"WinR{f}", stone, new Vector3(0.15f, 1.2f, 0.8f), new Vector3(width * 0.5f, wy, 0f));
            }

            go.AddComponent<BoxCollider>().size = new Vector3(width, height, depth);
            go.AddComponent<BoxCollider>().center = new Vector3(0f, height * 0.5f, 0f);
            return go;
        }

        public static GameObject CreateTownSquare(string name = "TownSquare")
        {
            var go = new GameObject(name);
            var stone = GetOrCreate(ref _stoneLightMat, "StoneLight", new Color(0.55f, 0.52f, 0.48f));
            var metal = GetOrCreate(ref _metalMat, "Metal", new Color(0.30f, 0.30f, 0.32f), 0.5f);

            // Paved ground (flat)
            Cylinder(go.transform, "Pavement", stone, new Vector3(25f, 0.2f, 25f), Vector3.zero);
            // Central fountain/monument
            Cylinder(go.transform, "Base", stone, new Vector3(4f, 1f, 4f), new Vector3(0f, 0.5f, 0f));
            Cylinder(go.transform, "Pillar", stone, new Vector3(1.5f, 4f, 1.5f), new Vector3(0f, 3f, 0f));
            Sphere(go.transform, "Top", metal, new Vector3(2f, 2f, 2f), new Vector3(0f, 6f, 0f));

            return go;
        }

        public static GameObject CreateBarricade(string name = "Barricade")
        {
            var go = new GameObject(name);
            var wood = GetOrCreate(ref _woodDarkMat, "WoodDark", new Color(0.35f, 0.25f, 0.15f));
            var metal = GetOrCreate(ref _metalMat, "Metal", new Color(0.30f, 0.30f, 0.32f), 0.5f);

            // Overturned cart/table base
            Box(go.transform, "Base", wood, new Vector3(6f, 2f, 1.5f), new Vector3(0f, 1f, 0f));
            // Stacked items on top
            Box(go.transform, "Barrel", wood, new Vector3(1.5f, 1.5f, 1.5f), new Vector3(-1.5f, 2.75f, 0f));
            Box(go.transform, "Crate", wood, new Vector3(2f, 1f, 1.5f), new Vector3(1.5f, 2.5f, 0f));
            // Metal reinforcement
            Box(go.transform, "Metal", metal, new Vector3(6f, 0.3f, 1.8f), new Vector3(0f, 2f, 0f));

            go.AddComponent<BoxCollider>().size = new Vector3(6f, 3.5f, 2f);
            return go;
        }

        // ==================== COASTAL ====================

        public static GameObject CreateCliffFace(string name = "CliffFace")
        {
            var go = new GameObject(name);
            var stone = GetOrCreate(ref _stoneDarkMat, "StoneDark", new Color(0.38f, 0.36f, 0.34f));
            var stoneLight = GetOrCreate(ref _stoneLightMat, "StoneLight", new Color(0.55f, 0.52f, 0.48f));

            // Large cliff wall
            Box(go.transform, "Main", stone, new Vector3(40f, 15f, 8f), new Vector3(0f, 7.5f, 0f));
            // Ledges
            Box(go.transform, "Ledge1", stoneLight, new Vector3(15f, 2f, 10f), new Vector3(-10f, 5f, 3f));
            Box(go.transform, "Ledge2", stoneLight, new Vector3(12f, 2f, 8f), new Vector3(8f, 10f, 2f));

            go.AddComponent<BoxCollider>().size = new Vector3(40f, 15f, 10f);
            return go;
        }

        public static GameObject CreateLighthouse(string name = "Lighthouse")
        {
            var go = new GameObject(name);
            var stone = GetOrCreate(ref _stoneLightMat, "StoneLight", new Color(0.55f, 0.52f, 0.48f));
            var metal = GetOrCreate(ref _metalMat, "Metal", new Color(0.30f, 0.30f, 0.32f), 0.5f);

            // Tower
            Cylinder(go.transform, "Tower", stone, new Vector3(3f, 8f, 3f), new Vector3(0f, 8f, 0f));
            // Lantern room
            Cylinder(go.transform, "Lantern", metal, new Vector3(4f, 2f, 4f), new Vector3(0f, 17f, 0f));
            // Cap
            Sphere(go.transform, "Cap", metal, new Vector3(4.5f, 2f, 4.5f), new Vector3(0f, 19f, 0f));
            // Base
            Cylinder(go.transform, "Base", stone, new Vector3(5f, 1f, 5f), new Vector3(0f, 0.5f, 0f));

            go.AddComponent<BoxCollider>().size = new Vector3(5f, 20f, 5f);
            return go;
        }

        public static GameObject CreateFishingBoat(string name = "FishingBoat")
        {
            var go = new GameObject(name);
            var wood = GetOrCreate(ref _woodDarkMat, "WoodDark", new Color(0.35f, 0.25f, 0.15f));
            var canvas = GetOrCreate(ref _canvasMat, "Canvas", new Color(0.85f, 0.78f, 0.60f));

            // Hull
            Box(go.transform, "Hull", wood, new Vector3(3f, 1.5f, 8f), new Vector3(0f, 0.75f, 0f));
            // Mast
            Cylinder(go.transform, "Mast", wood, new Vector3(0.15f, 4f, 0.15f), new Vector3(0f, 4.5f, 0f));
            // Sail
            Box(go.transform, "Sail", canvas, new Vector3(0.1f, 3f, 4f), new Vector3(0.5f, 5f, 0f));

            return go;
        }

        // ==================== MOUNTAINS ====================

        public static GameObject CreateLargeBoulder(string name = "LargeBoulder")
        {
            var go = new GameObject(name);
            var stone = GetOrCreate(ref _stoneDarkMat, "StoneDark", new Color(0.38f, 0.36f, 0.34f));

            float sx = Random.Range(4f, 10f);
            float sy = Random.Range(3f, 7f);
            float sz = Random.Range(4f, 8f);
            Sphere(go.transform, "Rock", stone, new Vector3(sx, sy, sz), new Vector3(0f, sy * 0.4f, 0f));

            // Smaller rocks around
            for (int i = 0; i < 3; i++)
            {
                float s = Random.Range(1f, 3f);
                Sphere(go.transform, $"Small{i}", stone,
                    new Vector3(s, s * 0.8f, s),
                    new Vector3(Random.Range(-sx * 0.6f, sx * 0.6f), s * 0.3f, Random.Range(-sz * 0.6f, sz * 0.6f)));
            }

            go.AddComponent<BoxCollider>().size = new Vector3(sx, sy, sz);
            return go;
        }

        public static GameObject CreateStoneBridge(string name = "StoneBridge")
        {
            var go = new GameObject(name);
            var stone = GetOrCreate(ref _stoneLightMat, "StoneLight", new Color(0.55f, 0.52f, 0.48f));
            var stoneDark = GetOrCreate(ref _stoneDarkMat, "StoneDark", new Color(0.38f, 0.36f, 0.34f));

            // Bridge deck
            Box(go.transform, "Deck", stone, new Vector3(6f, 1f, 20f), new Vector3(0f, 4f, 0f));
            // Arch underneath (approximated)
            Box(go.transform, "Arch", stoneDark, new Vector3(5f, 3f, 5f), new Vector3(0f, 1.5f, 0f));
            // Railings
            Box(go.transform, "Rail1", stone, new Vector3(0.3f, 1.5f, 20f), new Vector3(-2.8f, 5.25f, 0f));
            Box(go.transform, "Rail2", stone, new Vector3(0.3f, 1.5f, 20f), new Vector3(2.8f, 5.25f, 0f));
            // Pillars
            Box(go.transform, "Pillar1", stoneDark, new Vector3(2f, 5f, 2f), new Vector3(0f, 2f, -8f));
            Box(go.transform, "Pillar2", stoneDark, new Vector3(2f, 5f, 2f), new Vector3(0f, 2f, 8f));

            go.AddComponent<BoxCollider>().size = new Vector3(6f, 6f, 20f);
            return go;
        }

        public static GameObject CreateMountainShrine(string name = "MountainShrine")
        {
            var go = new GameObject(name);
            var stone = GetOrCreate(ref _stoneLightMat, "StoneLight", new Color(0.55f, 0.52f, 0.48f));
            var stoneDark = GetOrCreate(ref _stoneDarkMat, "StoneDark", new Color(0.38f, 0.36f, 0.34f));

            // Base platform
            Box(go.transform, "Platform", stoneDark, new Vector3(6f, 0.5f, 6f), new Vector3(0f, 0.25f, 0f));
            // Columns
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -2f : 2f);
                float z = (i < 2 ? -2f : 2f);
                Cylinder(go.transform, $"Col{i}", stone, new Vector3(0.4f, 2.5f, 0.4f), new Vector3(x, 3f, z));
            }
            // Roof
            Box(go.transform, "Roof", stoneDark, new Vector3(7f, 0.5f, 7f), new Vector3(0f, 5.75f, 0f));

            return go;
        }
    }
}
