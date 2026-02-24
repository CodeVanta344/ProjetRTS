using UnityEngine;
using System.Collections.Generic;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Generates 3D blocking city representations on the campaign map.
    /// All assets are colored primitives (cubes, cylinders, spheres) as placeholders.
    /// City visuals scale with city level, constructed buildings, specialization, and tech era.
    /// </summary>
    public static class CityVisualGenerator
    {
        // ==================== MATERIAL CACHE ====================
        private static Dictionary<string, Material> matCache = new Dictionary<string, Material>();

        private static Material GetMat(string key, Color color, bool unlit = false)
        {
            if (matCache.TryGetValue(key, out Material m) && m != null) return m;
            string shaderName = unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit";
            Shader shader = Shader.Find(shaderName);
            if (shader == null) shader = Shader.Find("Standard");
            m = new Material(shader) { color = color };
            m.name = key;
            matCache[key] = m;
            return m;
        }

        // ==================== MATERIAL PRESETS ====================
        // Era 0: Medieval (wood + thatch)
        private static Material WoodWall => GetMat("cv_wood", new Color(0.45f, 0.32f, 0.18f));
        private static Material ThatchRoof => GetMat("cv_thatch", new Color(0.55f, 0.48f, 0.28f));
        // Era 1: Early Modern (stone + tile)
        private static Material StoneWall => GetMat("cv_stone", new Color(0.62f, 0.58f, 0.52f));
        private static Material TileRoof => GetMat("cv_tile", new Color(0.60f, 0.22f, 0.15f));
        // Era 2: Industrial (brick + slate)
        private static Material BrickWall => GetMat("cv_brick", new Color(0.55f, 0.28f, 0.20f));
        private static Material SlateRoof => GetMat("cv_slate", new Color(0.35f, 0.35f, 0.38f));
        // Era 3: Grand (plaster + copper)
        private static Material PlasterWall => GetMat("cv_plaster", new Color(0.82f, 0.78f, 0.70f));
        private static Material CopperRoof => GetMat("cv_copper", new Color(0.40f, 0.55f, 0.45f));

        // Shared
        private static Material DarkStone => GetMat("cv_darkstone", new Color(0.38f, 0.36f, 0.34f));
        private static Material LightStone => GetMat("cv_lightstone", new Color(0.70f, 0.67f, 0.62f));
        private static Material Palisade => GetMat("cv_palisade", new Color(0.40f, 0.30f, 0.15f));
        private static Material MetalMat => GetMat("cv_metal", new Color(0.30f, 0.30f, 0.32f));
        private static Material GlassMat => GetMat("cv_glass", new Color(0.60f, 0.75f, 0.85f));
        private static Material GreenMat => GetMat("cv_green", new Color(0.25f, 0.45f, 0.18f));
        private static Material DirtMat => GetMat("cv_dirt", new Color(0.45f, 0.38f, 0.28f));
        private static Material WaterMat => GetMat("cv_water", new Color(0.20f, 0.35f, 0.50f));
        private static Material ScaffoldMat => GetMat("cv_scaffold", new Color(0.50f, 0.40f, 0.25f, 0.6f));

        private static Material GetFactionFlag(FactionType faction)
        {
            Color c = faction switch
            {
                FactionType.France => new Color(0.15f, 0.20f, 0.60f),
                FactionType.Britain => new Color(0.70f, 0.15f, 0.15f),
                FactionType.Prussia => new Color(0.15f, 0.15f, 0.15f),
                FactionType.Austria => new Color(0.85f, 0.80f, 0.20f),
                FactionType.Russia => new Color(0.20f, 0.50f, 0.20f),
                FactionType.Spain => new Color(0.75f, 0.60f, 0.10f),
                FactionType.Ottoman => new Color(0.70f, 0.15f, 0.15f),
                _ => Color.gray
            };
            return GetMat($"cv_flag_{faction}", c, true);
        }

        // ==================== TECH ERA ====================
        /// <summary>Determine architectural era from economy tech count</summary>
        public static int GetTechEra(int economyTechCount)
        {
            if (economyTechCount >= 13) return 3; // Grand
            if (economyTechCount >= 8) return 2;  // Industrial
            if (economyTechCount >= 4) return 1;  // Early Modern
            return 0; // Medieval
        }

        private static Material WallMat(int era) => era switch { 3 => PlasterWall, 2 => BrickWall, 1 => StoneWall, _ => WoodWall };
        private static Material RoofMat(int era) => era switch { 3 => CopperRoof, 2 => SlateRoof, 1 => TileRoof, _ => ThatchRoof };

        // ==================== PRIMITIVE HELPERS ====================
        private static GameObject Box(Transform parent, string name, Material mat, Vector3 scale, Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.localScale = scale;
            go.transform.localPosition = pos;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        private static GameObject Cyl(Transform parent, string name, Material mat, Vector3 scale, Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.localScale = scale;
            go.transform.localPosition = pos;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.Destroy(go.GetComponent<Collider>());
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
            Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        // ==================== MAIN ENTRY POINTS ====================

        /// <summary>
        /// Generate the full 3D visual for a capital/main city.
        /// </summary>
        public static GameObject CreateCity(CityData cityData, FactionType faction, int techEra, bool isNationCapital, Transform parent)
        {
            int level = cityData != null ? cityData.cityLevel : 1;
            var buildings = cityData?.buildings;

            GameObject root = new GameObject("CityVisual");
            root.transform.SetParent(parent);
            root.transform.localPosition = Vector3.zero;

            // Ground disc
            float groundRadius = GetCityRadius(level);
            Cyl(root.transform, "Ground", DirtMat, new Vector3(groundRadius * 2f, 0.15f, groundRadius * 2f), new Vector3(0f, 0.1f, 0f));

            // Walls (level 2+)
            if (level >= 2)
                CreateWalls(root.transform, level, techEra, groundRadius);

            // Residential buildings
            int houseCount = GetHouseCount(level);
            float houseRadius = groundRadius * 0.75f;
            CreateResidentialBuildings(root.transform, houseCount, houseRadius, level, techEra);

            // Central feature
            CreateCentralFeature(root.transform, level, techEra, faction, isNationCapital);

            // Specific constructed buildings
            if (buildings != null)
                CreateSpecificBuildings(root.transform, buildings, level, techEra, groundRadius);

            // Flagpole
            CreateFlagpole(root.transform, faction, level, isNationCapital);

            return root;
        }

        /// <summary>
        /// Generate a smaller sub-city visual based on specialization.
        /// </summary>
        public static GameObject CreateSubCity(CitySpecialization specialization, int parentLevel, int techEra, FactionType faction, Transform parent)
        {
            int subLevel = Mathf.Max(1, parentLevel - 1);
            float radius = GetCityRadius(subLevel) * 0.6f;

            GameObject root = new GameObject("SubCityVisual");
            root.transform.SetParent(parent);
            root.transform.localPosition = Vector3.zero;

            // Small ground
            Cyl(root.transform, "Ground", DirtMat, new Vector3(radius * 2f, 0.1f, radius * 2f), new Vector3(0f, 0.05f, 0f));

            // A few houses
            int houses = Mathf.Max(2, GetHouseCount(subLevel) / 2);
            CreateResidentialBuildings(root.transform, houses, radius * 0.7f, subLevel, techEra);

            // Specialization-specific structure
            CreateSpecializationBuilding(root.transform, specialization, techEra, radius);

            // Small flag
            CreateFlagpole(root.transform, faction, 1, false);

            return root;
        }

        /// <summary>
        /// Create scaffolding for a building under construction.
        /// </summary>
        public static GameObject CreateScaffolding(Transform parent, Vector3 position, float height, float width)
        {
            GameObject scaff = new GameObject("Scaffolding");
            scaff.transform.SetParent(parent);
            scaff.transform.localPosition = position;

            // Vertical poles
            float hw = width * 0.5f;
            Cyl(scaff.transform, "Pole1", ScaffoldMat, new Vector3(0.15f, height * 0.5f, 0.15f), new Vector3(-hw, height * 0.5f, -hw));
            Cyl(scaff.transform, "Pole2", ScaffoldMat, new Vector3(0.15f, height * 0.5f, 0.15f), new Vector3(hw, height * 0.5f, -hw));
            Cyl(scaff.transform, "Pole3", ScaffoldMat, new Vector3(0.15f, height * 0.5f, 0.15f), new Vector3(-hw, height * 0.5f, hw));
            Cyl(scaff.transform, "Pole4", ScaffoldMat, new Vector3(0.15f, height * 0.5f, 0.15f), new Vector3(hw, height * 0.5f, hw));

            // Horizontal planks at intervals
            for (float y = height * 0.3f; y < height; y += height * 0.35f)
            {
                Box(scaff.transform, "Plank", ScaffoldMat, new Vector3(width, 0.1f, 0.3f), new Vector3(0f, y, -hw));
                Box(scaff.transform, "Plank", ScaffoldMat, new Vector3(width, 0.1f, 0.3f), new Vector3(0f, y, hw));
            }

            return scaff;
        }

        // ==================== LAYOUT PARAMETERS ====================

        private static float GetCityRadius(int level)
        {
            return level switch
            {
                1 => 8f,
                2 => 14f,
                3 => 22f,
                4 => 32f,
                5 => 42f,
                _ => 8f
            };
        }

        private static int GetHouseCount(int level)
        {
            return level switch
            {
                1 => 4,
                2 => 8,
                3 => 15,
                4 => 25,
                5 => 40,
                _ => 4
            };
        }

        // ==================== RESIDENTIAL BUILDINGS ====================

        private static void CreateResidentialBuildings(Transform parent, int count, float radius, int level, int era)
        {
            Material wall = WallMat(era);
            Material roof = RoofMat(era);

            float baseW = level >= 3 ? 3f : 2f;
            float baseH = level >= 4 ? 4f : (level >= 2 ? 3f : 2f);

            // Place houses in rings
            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
                float dist = Random.Range(radius * 0.25f, radius);

                // Keep center clear for central feature
                if (dist < radius * 0.2f) dist = radius * 0.25f;

                Vector3 pos = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

                float w = baseW * Random.Range(0.8f, 1.3f);
                float d = baseW * Random.Range(0.8f, 1.3f);
                float h = baseH * Random.Range(0.8f, 1.4f);

                // Era 2+ can have multi-story
                if (era >= 2 && Random.value > 0.6f) h *= 1.5f;
                if (era >= 3 && Random.value > 0.7f) h *= 1.3f;

                CreateHouse(parent, $"House_{i}", wall, roof, pos, w, d, h);
            }
        }

        private static void CreateHouse(Transform parent, string name, Material wall, Material roof, Vector3 pos, float w, float d, float h)
        {
            GameObject house = new GameObject(name);
            house.transform.SetParent(parent);
            house.transform.localPosition = pos;
            house.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            // Body
            Box(house.transform, "Body", wall, new Vector3(w, h, d), new Vector3(0f, h * 0.5f, 0f));

            // Pitched roof (cylinder rotated 90°)
            var roofGO = Cyl(house.transform, "Roof", roof, new Vector3(w * 0.5f, d * 0.55f, w * 0.5f), new Vector3(0f, h + w * 0.2f, 0f));
            roofGO.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            // Door (dark recess)
            Box(house.transform, "Door", DarkStone, new Vector3(w * 0.25f, h * 0.4f, 0.1f), new Vector3(0f, h * 0.2f, d * 0.51f));
        }

        // ==================== WALLS ====================

        private static void CreateWalls(Transform parent, int level, int era, float radius)
        {
            float wallH, wallT;
            Material wallMat;
            int segments;

            if (level <= 2)
            {
                // Palisade
                wallH = 3f; wallT = 0.4f; wallMat = Palisade; segments = 12;
            }
            else if (level == 3)
            {
                // Stone walls
                wallH = 5f; wallT = 0.8f; wallMat = StoneWall; segments = 16;
            }
            else if (level == 4)
            {
                // Fortified walls
                wallH = 7f; wallT = 1.2f; wallMat = era >= 2 ? BrickWall : StoneWall; segments = 20;
            }
            else
            {
                // Massive walls with bastions
                wallH = 9f; wallT = 1.5f; wallMat = era >= 2 ? BrickWall : DarkStone; segments = 24;
            }

            float wallRadius = radius * 0.9f;

            // Wall segments
            for (int i = 0; i < segments; i++)
            {
                float a1 = (i / (float)segments) * Mathf.PI * 2f;
                float a2 = ((i + 1) / (float)segments) * Mathf.PI * 2f;
                float aMid = (a1 + a2) * 0.5f;

                Vector3 p1 = new Vector3(Mathf.Cos(a1) * wallRadius, 0f, Mathf.Sin(a1) * wallRadius);
                Vector3 p2 = new Vector3(Mathf.Cos(a2) * wallRadius, 0f, Mathf.Sin(a2) * wallRadius);
                Vector3 mid = (p1 + p2) * 0.5f;
                float segLen = Vector3.Distance(p1, p2);
                float segAngle = Mathf.Atan2(p2.x - p1.x, p2.z - p1.z) * Mathf.Rad2Deg;

                Box(parent, $"Wall_{i}", wallMat,
                    new Vector3(wallT, wallH, segLen),
                    new Vector3(mid.x, wallH * 0.5f, mid.z))
                    .transform.localRotation = Quaternion.Euler(0f, segAngle, 0f);
            }

            // Towers at corners (level 3+)
            if (level >= 3)
            {
                int towerCount = level >= 5 ? 8 : (level >= 4 ? 6 : 4);
                float towerH = wallH + 3f;
                float towerR = wallT * 1.5f;

                for (int i = 0; i < towerCount; i++)
                {
                    float a = (i / (float)towerCount) * Mathf.PI * 2f;
                    Vector3 tp = new Vector3(Mathf.Cos(a) * wallRadius, 0f, Mathf.Sin(a) * wallRadius);
                    Cyl(parent, $"Tower_{i}", wallMat, new Vector3(towerR * 2f, towerH * 0.5f, towerR * 2f),
                        new Vector3(tp.x, towerH * 0.5f, tp.z));

                    // Tower cap
                    Cyl(parent, $"TowerCap_{i}", RoofMat(era), new Vector3(towerR * 2.5f, 1f, towerR * 2.5f),
                        new Vector3(tp.x, towerH + 0.5f, tp.z));
                }
            }

            // Gate (always on south side)
            float gateW = level >= 4 ? 5f : 3f;
            float gateH = wallH * 0.7f;
            Vector3 gatePos = new Vector3(0f, gateH * 0.5f, -wallRadius);
            Box(parent, "GateArch", DarkStone, new Vector3(gateW, gateH, wallT * 1.5f), gatePos);

            // Gate towers (level 3+)
            if (level >= 3)
            {
                float gtR = wallT * 1.2f;
                float gtH = wallH + 2f;
                Cyl(parent, "GateTowerL", wallMat, new Vector3(gtR * 2f, gtH * 0.5f, gtR * 2f),
                    new Vector3(-gateW * 0.7f, gtH * 0.5f, -wallRadius));
                Cyl(parent, "GateTowerR", wallMat, new Vector3(gtR * 2f, gtH * 0.5f, gtR * 2f),
                    new Vector3(gateW * 0.7f, gtH * 0.5f, -wallRadius));
            }

            // Bastions (level 5)
            if (level >= 5)
            {
                for (int i = 0; i < 4; i++)
                {
                    float a = (i * 90f + 45f) * Mathf.Deg2Rad;
                    Vector3 bp = new Vector3(Mathf.Cos(a) * (wallRadius + 3f), 0f, Mathf.Sin(a) * (wallRadius + 3f));
                    // Star-fort bastion (diamond shape approximated by rotated cube)
                    var bastion = Box(parent, $"Bastion_{i}", wallMat,
                        new Vector3(6f, wallH * 0.6f, 6f),
                        new Vector3(bp.x, wallH * 0.3f, bp.z));
                    bastion.transform.localRotation = Quaternion.Euler(0f, 45f + i * 90f, 0f);
                }
            }
        }

        // ==================== CENTRAL FEATURE ====================

        private static void CreateCentralFeature(Transform parent, int level, int era, FactionType faction, bool isNationCapital)
        {
            if (level <= 1)
            {
                // Village well
                Cyl(parent, "Well", DarkStone, new Vector3(1.5f, 0.5f, 1.5f), new Vector3(0f, 0.5f, 0f));
                Cyl(parent, "WellPole", WoodWall, new Vector3(0.15f, 1.5f, 0.15f), new Vector3(0f, 2f, 0f));
            }
            else if (level == 2)
            {
                // Small church
                CreateChurch(parent, era, 6f);
            }
            else if (level == 3)
            {
                // Market square + church
                CreateMarketSquare(parent, era, 8f);
                CreateChurch(parent, era, 10f, new Vector3(6f, 0f, 4f));
            }
            else if (level == 4)
            {
                // Cathedral + town hall
                CreateCathedral(parent, era, 14f);
                CreateTownHall(parent, era, new Vector3(-8f, 0f, 0f));
                CreateMarketSquare(parent, era, 6f, new Vector3(6f, 0f, -5f));
            }
            else // level 5
            {
                // Palace/grand cathedral + monument
                if (isNationCapital)
                    CreatePalace(parent, era, faction);
                else
                    CreateCathedral(parent, era, 18f);

                CreateTownHall(parent, era, new Vector3(-12f, 0f, 0f));
                CreateMonument(parent, era, new Vector3(8f, 0f, -8f));
                CreateMarketSquare(parent, era, 8f, new Vector3(10f, 0f, 6f));
            }
        }

        // ==================== LANDMARK BUILDINGS ====================

        private static void CreateChurch(Transform parent, int era, float height, Vector3 offset = default)
        {
            GameObject church = new GameObject("Church");
            church.transform.SetParent(parent);
            church.transform.localPosition = offset;

            Material wall = era >= 1 ? StoneWall : WoodWall;
            Material roof = RoofMat(era);

            // Nave
            Box(church.transform, "Nave", wall, new Vector3(3f, height * 0.5f, 5f), new Vector3(0f, height * 0.25f, 0f));
            // Roof
            var r = Cyl(church.transform, "Roof", roof, new Vector3(1.5f, 2.8f, 1.5f), new Vector3(0f, height * 0.5f + 0.5f, 0f));
            r.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            // Steeple
            Cyl(church.transform, "Steeple", wall, new Vector3(1f, height * 0.3f, 1f), new Vector3(0f, height * 0.7f, -2f));
            // Cross
            Box(church.transform, "Cross", MetalMat, new Vector3(0.8f, 0.15f, 0.15f), new Vector3(0f, height * 0.88f, -2f));
            Box(church.transform, "CrossV", MetalMat, new Vector3(0.15f, 1.2f, 0.15f), new Vector3(0f, height * 0.85f, -2f));
        }

        private static void CreateCathedral(Transform parent, int era, float height)
        {
            GameObject cath = new GameObject("Cathedral");
            cath.transform.SetParent(parent);
            cath.transform.localPosition = Vector3.zero;

            Material wall = era >= 2 ? LightStone : StoneWall;
            Material roof = RoofMat(era);

            // Main body
            Box(cath.transform, "Nave", wall, new Vector3(5f, height * 0.5f, 8f), new Vector3(0f, height * 0.25f, 0f));
            // Transept
            Box(cath.transform, "Transept", wall, new Vector3(8f, height * 0.4f, 3f), new Vector3(0f, height * 0.2f, -1f));
            // Tower left
            Cyl(cath.transform, "TowerL", wall, new Vector3(1.5f, height * 0.4f, 1.5f), new Vector3(-2f, height * 0.7f, -3.5f));
            // Tower right
            Cyl(cath.transform, "TowerR", wall, new Vector3(1.5f, height * 0.4f, 1.5f), new Vector3(2f, height * 0.7f, -3.5f));
            // Spire
            Cyl(cath.transform, "Spire", MetalMat, new Vector3(0.5f, height * 0.15f, 0.5f), new Vector3(0f, height * 0.92f, -3.5f));
            // Rose window (glass disc)
            Cyl(cath.transform, "Rose", GlassMat, new Vector3(2f, 0.1f, 2f), new Vector3(0f, height * 0.35f, -4.1f))
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            // Roof
            Box(cath.transform, "Roof", roof, new Vector3(5.5f, 1f, 9f), new Vector3(0f, height * 0.52f, 0f));
        }

        private static void CreateMarketSquare(Transform parent, int era, float size, Vector3 offset = default)
        {
            GameObject mkt = new GameObject("MarketSquare");
            mkt.transform.SetParent(parent);
            mkt.transform.localPosition = offset;

            // Paved ground
            Cyl(mkt.transform, "Pavement", LightStone, new Vector3(size, 0.1f, size), new Vector3(0f, 0.1f, 0f));

            // Market stalls
            int stallCount = Mathf.Max(3, (int)(size * 0.5f));
            for (int i = 0; i < stallCount; i++)
            {
                float angle = (i / (float)stallCount) * Mathf.PI * 2f;
                float dist = size * 0.35f;
                Vector3 sp = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

                Material canopy = GetMat($"cv_canopy_{i % 4}", new Color(
                    0.5f + (i % 3) * 0.15f,
                    0.2f + (i % 2) * 0.2f,
                    0.1f + (i % 4) * 0.1f), true);

                // Stall table
                Box(mkt.transform, $"Stall_{i}", WoodWall, new Vector3(1.5f, 1f, 1f), sp + Vector3.up * 0.5f);
                // Canopy
                Box(mkt.transform, $"Canopy_{i}", canopy, new Vector3(2f, 0.1f, 1.5f), sp + Vector3.up * 2f);
                // Pole
                Cyl(mkt.transform, $"Pole_{i}", WoodWall, new Vector3(0.1f, 1f, 0.1f), sp + Vector3.up * 1.5f);
            }
        }

        private static void CreateTownHall(Transform parent, int era, Vector3 offset)
        {
            GameObject hall = new GameObject("TownHall");
            hall.transform.SetParent(parent);
            hall.transform.localPosition = offset;

            Material wall = era >= 2 ? PlasterWall : StoneWall;

            // Main building
            Box(hall.transform, "Body", wall, new Vector3(6f, 7f, 5f), new Vector3(0f, 3.5f, 0f));
            // Roof
            Box(hall.transform, "Roof", RoofMat(era), new Vector3(6.5f, 1f, 5.5f), new Vector3(0f, 7.5f, 0f));
            // Clock tower
            Cyl(hall.transform, "Tower", wall, new Vector3(1.5f, 3f, 1.5f), new Vector3(0f, 10f, 0f));
            Sphere(hall.transform, "Clock", MetalMat, new Vector3(1.2f, 1.2f, 0.3f), new Vector3(0f, 10f, 0.8f));
            // Steps
            Box(hall.transform, "Steps", LightStone, new Vector3(4f, 0.5f, 2f), new Vector3(0f, 0.25f, 3.5f));
            // Columns (era 1+)
            if (era >= 1)
            {
                for (int i = 0; i < 4; i++)
                {
                    float x = -2.2f + i * 1.5f;
                    Cyl(hall.transform, $"Col_{i}", LightStone, new Vector3(0.3f, 3f, 0.3f), new Vector3(x, 3.5f, 2.6f));
                }
            }
        }

        private static void CreatePalace(Transform parent, int era, FactionType faction)
        {
            GameObject palace = new GameObject("Palace");
            palace.transform.SetParent(parent);
            palace.transform.localPosition = Vector3.zero;

            Material wall = era >= 2 ? PlasterWall : LightStone;

            // Main wing
            Box(palace.transform, "MainWing", wall, new Vector3(12f, 8f, 6f), new Vector3(0f, 4f, 0f));
            // Left wing
            Box(palace.transform, "LeftWing", wall, new Vector3(4f, 7f, 10f), new Vector3(-8f, 3.5f, 2f));
            // Right wing
            Box(palace.transform, "RightWing", wall, new Vector3(4f, 7f, 10f), new Vector3(8f, 3.5f, 2f));
            // Roof
            Box(palace.transform, "Roof", RoofMat(era), new Vector3(13f, 1.2f, 7f), new Vector3(0f, 8.6f, 0f));
            // Central dome
            Sphere(palace.transform, "Dome", CopperRoof, new Vector3(5f, 4f, 5f), new Vector3(0f, 10f, 0f));
            // Courtyard (empty paved area in front)
            Cyl(palace.transform, "Courtyard", LightStone, new Vector3(14f, 0.1f, 10f), new Vector3(0f, 0.1f, 10f));
            // Grand columns
            for (int i = 0; i < 6; i++)
            {
                float x = -5f + i * 2f;
                Cyl(palace.transform, $"Col_{i}", LightStone, new Vector3(0.5f, 4f, 0.5f), new Vector3(x, 4f, 3.1f));
            }
            // Grand staircase
            for (int s = 0; s < 5; s++)
            {
                Box(palace.transform, $"Step_{s}", LightStone,
                    new Vector3(8f - s * 0.3f, 0.3f, 1.5f),
                    new Vector3(0f, 0.15f + s * 0.3f, 3.5f + s * 1.2f));
            }
        }

        private static void CreateMonument(Transform parent, int era, Vector3 offset)
        {
            GameObject mon = new GameObject("Monument");
            mon.transform.SetParent(parent);
            mon.transform.localPosition = offset;

            // Pedestal
            Box(mon.transform, "Base", DarkStone, new Vector3(3f, 1f, 3f), new Vector3(0f, 0.5f, 0f));
            Box(mon.transform, "Pedestal", LightStone, new Vector3(2f, 2f, 2f), new Vector3(0f, 2f, 0f));
            // Column/obelisk
            Cyl(mon.transform, "Obelisk", LightStone, new Vector3(0.8f, 5f, 0.8f), new Vector3(0f, 8f, 0f));
            // Eagle/orb on top
            Sphere(mon.transform, "Top", MetalMat, new Vector3(1.5f, 1f, 1.5f), new Vector3(0f, 13.5f, 0f));
        }

        // ==================== SPECIFIC BUILDINGS ====================

        private static void CreateSpecificBuildings(Transform parent, List<CityBuilding> buildings, int level, int era, float radius)
        {
            float angle = Mathf.PI * 0.3f; // Start placing at 30° offset
            float angleStep = Mathf.PI * 0.25f; // 45° between special buildings

            foreach (var bld in buildings)
            {
                if (!bld.isConstructed && !bld.isConstructing) continue;

                // Position on the edge of the city
                float dist = radius * 0.65f;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                angle += angleStep;

                if (bld.isConstructing && !bld.isConstructed)
                {
                    // Show scaffolding for buildings under construction
                    float scaffH = GetBuildingHeight(bld.buildingType, era);
                    float scaffW = GetBuildingWidth(bld.buildingType);
                    CreateScaffolding(parent, pos, scaffH, scaffW);
                    continue;
                }

                CreateBuildingByType(parent, bld.buildingType, bld.level, era, pos);
            }
        }

        private static float GetBuildingHeight(BuildingType type, int era)
        {
            float baseH = type switch
            {
                BuildingType.Barracks or BuildingType.VillageBarracks or BuildingType.ProvincialBarracks => 5f,
                BuildingType.MilitaryAcademy or BuildingType.RoyalMilitaryCollege or BuildingType.MilitaryUniversity => 7f,
                BuildingType.Market => 4f,
                BuildingType.Church => 8f,
                BuildingType.University => 7f,
                BuildingType.Fortress => 9f,
                BuildingType.Armory => 5f,
                BuildingType.Stables => 4f,
                BuildingType.Farm => 3f,
                BuildingType.Mine => 4f,
                _ => 4f
            };
            return baseH + era * 0.5f;
        }

        private static float GetBuildingWidth(BuildingType type)
        {
            return type switch
            {
                BuildingType.Fortress => 8f,
                BuildingType.Barracks or BuildingType.VillageBarracks or BuildingType.ProvincialBarracks => 6f,
                BuildingType.Market => 6f,
                BuildingType.University or BuildingType.MilitaryAcademy => 6f,
                BuildingType.Farm => 5f,
                _ => 4f
            };
        }

        private static void CreateBuildingByType(Transform parent, BuildingType type, int bldLevel, int era, Vector3 pos)
        {
            switch (type)
            {
                case BuildingType.Barracks:
                case BuildingType.VillageBarracks:
                case BuildingType.ProvincialBarracks:
                case BuildingType.MilitaryAcademy:
                case BuildingType.RoyalMilitaryCollege:
                case BuildingType.MilitaryUniversity:
                    CreateBarracksBuilding(parent, era, bldLevel, pos);
                    break;
                case BuildingType.Market:
                    // Market is already part of central feature for level 3+
                    break;
                case BuildingType.Church:
                    CreateChurchBuilding(parent, era, bldLevel, pos);
                    break;
                case BuildingType.University:
                    CreateUniversityBuilding(parent, era, bldLevel, pos);
                    break;
                case BuildingType.Fortress:
                    CreateFortressBuilding(parent, era, bldLevel, pos);
                    break;
                case BuildingType.Armory:
                    CreateArmoryBuilding(parent, era, bldLevel, pos);
                    break;
                case BuildingType.Stables:
                    CreateStablesBuilding(parent, era, bldLevel, pos);
                    break;
                case BuildingType.Farm:
                    CreateFarmBuilding(parent, era, bldLevel, pos);
                    break;
                case BuildingType.Mine:
                    CreateMineBuilding(parent, era, bldLevel, pos);
                    break;
                case BuildingType.SmallArtillerySchool:
                case BuildingType.ProvincialArtillerySchool:
                case BuildingType.RoyalArtilleryAcademy:
                case BuildingType.GrandArtilleryAcademy:
                case BuildingType.ImperialArtilleryAcademy:
                    CreateArtillerySchool(parent, era, bldLevel, pos);
                    break;
            }
        }

        // --- Level scale multiplier (buildings grow with upgrades) ---
        private static float LevelScale(int bldLevel)
        {
            return 1f + (bldLevel - 1) * 0.2f; // Lv1=1.0, Lv2=1.2, Lv3=1.4, Lv4=1.6
        }

        // --- Barracks ---
        private static void CreateBarracksBuilding(Transform parent, int era, int bldLevel, Vector3 pos)
        {
            GameObject bk = new GameObject("Barracks");
            bk.transform.SetParent(parent);
            bk.transform.localPosition = pos;

            Material wall = WallMat(era);
            float s = LevelScale(bldLevel);

            // Main block
            Box(bk.transform, "Block", wall, new Vector3(6f * s, 4f * s, 4f * s), new Vector3(0f, 2f * s, 0f));
            Box(bk.transform, "Roof", RoofMat(era), new Vector3(6.5f * s, 0.8f, 4.5f * s), new Vector3(0f, 4.4f * s, 0f));
            // Drill yard
            Cyl(bk.transform, "Yard", DirtMat, new Vector3(5f * s, 0.05f, 5f * s), new Vector3(0f, 0.05f, 4f * s));
            // Flagpole
            Cyl(bk.transform, "Pole", MetalMat, new Vector3(0.1f, 4f * s, 0.1f), new Vector3(3f * s, 4f * s, 0f));
            // Extra wing for level 3+
            if (bldLevel >= 3)
                Box(bk.transform, "Wing", wall, new Vector3(4f * s, 3f * s, 3f * s), new Vector3(5f * s, 1.5f * s, 0f));
        }

        // --- University ---
        private static void CreateUniversityBuilding(Transform parent, int era, int bldLevel, Vector3 pos)
        {
            GameObject uni = new GameObject("University");
            uni.transform.SetParent(parent);
            uni.transform.localPosition = pos;

            Material wall = era >= 2 ? PlasterWall : StoneWall;
            float s = LevelScale(bldLevel);

            Box(uni.transform, "Main", wall, new Vector3(6f * s, 6f * s, 5f * s), new Vector3(0f, 3f * s, 0f));
            Box(uni.transform, "Roof", RoofMat(era), new Vector3(6.5f * s, 1f, 5.5f * s), new Vector3(0f, 6.5f * s, 0f));
            // Library tower
            Cyl(uni.transform, "Tower", wall, new Vector3(1.5f * s, 4f * s, 1.5f * s), new Vector3(2.5f * s, 8f * s, 0f));
            Sphere(uni.transform, "Dome", CopperRoof, new Vector3(2f * s, 1.5f * s, 2f * s), new Vector3(2.5f * s, 10.5f * s, 0f));
            // Columns
            int colCount = 3 + (bldLevel - 1);
            for (int i = 0; i < colCount; i++)
                Cyl(uni.transform, $"Col_{i}", LightStone, new Vector3(0.3f, 2.5f * s, 0.3f), new Vector3(-2f * s + i * 1.5f, 2.5f * s, 2.6f * s));
        }

        // --- Armory ---
        private static void CreateArmoryBuilding(Transform parent, int era, int bldLevel, Vector3 pos)
        {
            GameObject arm = new GameObject("Armory");
            arm.transform.SetParent(parent);
            arm.transform.localPosition = pos;

            Material wall = era >= 1 ? StoneWall : WoodWall;
            float s = LevelScale(bldLevel);

            Box(arm.transform, "Body", wall, new Vector3(5f * s, 4f * s, 4f * s), new Vector3(0f, 2f * s, 0f));
            Box(arm.transform, "Roof", SlateRoof, new Vector3(5.5f * s, 0.8f, 4.5f * s), new Vector3(0f, 4.4f * s, 0f));
            // Cannons out front (more at higher levels)
            int cannonCount = Mathf.Min(bldLevel, 3);
            for (int c = 0; c < cannonCount; c++)
            {
                float cx = -1f + c * 1f;
                Cyl(arm.transform, $"Cannon_{c}", MetalMat, new Vector3(0.3f, 1.5f, 0.3f), new Vector3(cx, 0.5f, 3f * s))
                    .transform.localRotation = Quaternion.Euler(80f, 0f, 0f);
            }
            // Wheels
            Cyl(arm.transform, "Wheel1", WoodWall, new Vector3(0.8f, 0.1f, 0.8f), new Vector3(-0.5f, 0.4f, 2.5f * s))
                .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Cyl(arm.transform, "Wheel2", WoodWall, new Vector3(0.8f, 0.1f, 0.8f), new Vector3(0.5f, 0.4f, 2.5f * s))
                .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        // --- Stables ---
        private static void CreateStablesBuilding(Transform parent, int era, int bldLevel, Vector3 pos)
        {
            GameObject stb = new GameObject("Stables");
            stb.transform.SetParent(parent);
            stb.transform.localPosition = pos;

            float s = LevelScale(bldLevel);
            Box(stb.transform, "Barn", WoodWall, new Vector3(5f * s, 3.5f * s, 6f * s), new Vector3(0f, 1.75f * s, 0f));
            Box(stb.transform, "Roof", ThatchRoof, new Vector3(5.5f * s, 0.8f, 6.5f * s), new Vector3(0f, 3.9f * s, 0f));
            // Paddock fence (larger at higher levels)
            float fenceR = 4f * s;
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f * Mathf.Deg2Rad;
                Vector3 fp = new Vector3(Mathf.Cos(a) * fenceR, 0.5f, Mathf.Sin(a) * fenceR + 5f * s);
                Box(stb.transform, $"Fence_{i}", WoodWall, new Vector3(fenceR, 1f, 0.15f), fp)
                    .transform.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
            }
        }

        // --- Farm ---
        private static void CreateFarmBuilding(Transform parent, int era, int bldLevel, Vector3 pos)
        {
            GameObject farm = new GameObject("Farm");
            farm.transform.SetParent(parent);
            farm.transform.localPosition = pos;

            float s = LevelScale(bldLevel);
            // Farmhouse
            Box(farm.transform, "House", WoodWall, new Vector3(3f * s, 2.5f * s, 3f * s), new Vector3(0f, 1.25f * s, 0f));
            Box(farm.transform, "Roof", ThatchRoof, new Vector3(3.5f * s, 0.6f, 3.5f * s), new Vector3(0f, 2.8f * s, 0f));
            // Silo (extra silos at higher levels)
            for (int si = 0; si < bldLevel; si++)
            {
                float sx = 2.5f + si * 1.5f;
                Cyl(farm.transform, $"Silo_{si}", WoodWall, new Vector3(1f, 2f * s, 1f), new Vector3(sx, 2f * s, 0f));
                Sphere(farm.transform, $"SiloCap_{si}", ThatchRoof, new Vector3(1.2f, 0.8f, 1.2f), new Vector3(sx, 3.4f * s, 0f));
            }
            // Fields (flat green patches, more at higher levels)
            Box(farm.transform, "Field1", GreenMat, new Vector3(5f * s, 0.05f, 3f * s), new Vector3(-4f, 0.05f, 0f));
            Box(farm.transform, "Field2", GreenMat, new Vector3(3f * s, 0.05f, 5f * s), new Vector3(0f, 0.05f, -4f * s));
            if (bldLevel >= 3)
                Box(farm.transform, "Field3", GreenMat, new Vector3(4f * s, 0.05f, 4f * s), new Vector3(4f, 0.05f, -3f));
        }

        // --- Mine ---
        private static void CreateMineBuilding(Transform parent, int era, int bldLevel, Vector3 pos)
        {
            GameObject mine = new GameObject("Mine");
            mine.transform.SetParent(parent);
            mine.transform.localPosition = pos;

            float s = LevelScale(bldLevel);
            // Mine entrance
            Box(mine.transform, "Entrance", DarkStone, new Vector3(3f * s, 3f * s, 1f), new Vector3(0f, 1.5f * s, 0f));
            Box(mine.transform, "EntranceHole", GetMat("cv_black", Color.black), new Vector3(2f * s, 2f * s, 0.5f), new Vector3(0f, 1f * s, 0.3f));
            // Support beams
            Box(mine.transform, "BeamL", WoodWall, new Vector3(0.3f, 3f * s, 0.3f), new Vector3(-1.2f * s, 1.5f * s, 0.5f));
            Box(mine.transform, "BeamR", WoodWall, new Vector3(0.3f, 3f * s, 0.3f), new Vector3(1.2f * s, 1.5f * s, 0.5f));
            Box(mine.transform, "BeamTop", WoodWall, new Vector3(3f * s, 0.3f, 0.3f), new Vector3(0f, 3f * s, 0.5f));
            // Ore cart (era 1+)
            if (era >= 1)
            {
                Box(mine.transform, "Cart", MetalMat, new Vector3(1f, 0.6f, 1.5f), new Vector3(2f * s, 0.3f, 1f));
                // Rails
                Box(mine.transform, "Rail1", MetalMat, new Vector3(0.1f, 0.05f, 4f * s), new Vector3(1.7f * s, 0.05f, 2f));
                Box(mine.transform, "Rail2", MetalMat, new Vector3(0.1f, 0.05f, 4f * s), new Vector3(2.3f * s, 0.05f, 2f));
            }
            // Spoil heap (bigger at higher levels)
            float heapScale = 1f + (bldLevel - 1) * 0.3f;
            Sphere(mine.transform, "SpoilHeap", DirtMat, new Vector3(3f * heapScale, 1.5f * heapScale, 3f * heapScale), new Vector3(-3f, 0.5f * heapScale, 2f));
        }

        // --- Fortress (inner keep, distinct from walls) ---
        private static void CreateFortressBuilding(Transform parent, int era, int bldLevel, Vector3 pos)
        {
            GameObject fort = new GameObject("FortressKeep");
            fort.transform.SetParent(parent);
            fort.transform.localPosition = pos;

            Material wall = era >= 2 ? BrickWall : DarkStone;
            float s = LevelScale(bldLevel);

            // Central keep
            Box(fort.transform, "Keep", wall, new Vector3(5f * s, 8f * s, 5f * s), new Vector3(0f, 4f * s, 0f));
            Box(fort.transform, "KeepRoof", SlateRoof, new Vector3(5.5f * s, 1f, 5.5f * s), new Vector3(0f, 8.5f * s, 0f));
            // Corner turrets
            float turretOff = 2.5f * s;
            float turretH = 10f * s;
            for (int i = 0; i < 4; i++)
            {
                float tx = (i % 2 == 0 ? -1 : 1) * turretOff;
                float tz = (i < 2 ? -1 : 1) * turretOff;
                Cyl(fort.transform, $"Turret_{i}", wall, new Vector3(1.2f * s, turretH * 0.5f, 1.2f * s), new Vector3(tx, turretH * 0.5f, tz));
                Cyl(fort.transform, $"TurretCap_{i}", RoofMat(era), new Vector3(1.5f * s, 0.8f, 1.5f * s), new Vector3(tx, turretH + 0.4f, tz));
            }
            // Gatehouse
            Box(fort.transform, "Gate", DarkStone, new Vector3(3f * s, 5f * s, 1.5f), new Vector3(0f, 2.5f * s, -turretOff - 0.5f));
            Box(fort.transform, "GateArch", GetMat("cv_black", Color.black), new Vector3(1.5f * s, 3f * s, 0.5f), new Vector3(0f, 1.5f * s, -turretOff - 0.3f));
        }

        // --- Church (explicit building, distinct from central feature) ---
        private static void CreateChurchBuilding(Transform parent, int era, int bldLevel, Vector3 pos)
        {
            float s = LevelScale(bldLevel);
            float height = 6f * s + era * 0.5f;
            CreateChurch(parent, era, height, pos);
        }

        // --- Granary ---
        private static void CreateGranaryBuilding(Transform parent, int era, int bldLevel, Vector3 pos)
        {
            GameObject gran = new GameObject("Granary");
            gran.transform.SetParent(parent);
            gran.transform.localPosition = pos;

            float s = LevelScale(bldLevel);
            Material wall = era >= 1 ? StoneWall : WoodWall;

            // Main storehouse
            Box(gran.transform, "Store", wall, new Vector3(4f * s, 3f * s, 5f * s), new Vector3(0f, 1.5f * s, 0f));
            Box(gran.transform, "Roof", ThatchRoof, new Vector3(4.5f * s, 0.6f, 5.5f * s), new Vector3(0f, 3.3f * s, 0f));
            // Grain silos
            for (int si = 0; si < bldLevel + 1; si++)
            {
                float sx = -2f + si * 2f;
                Cyl(gran.transform, $"Silo_{si}", WoodWall, new Vector3(1f, 2.5f * s, 1f), new Vector3(sx, 2.5f * s, -3.5f * s));
                Sphere(gran.transform, $"SiloCap_{si}", ThatchRoof, new Vector3(1.2f, 0.8f, 1.2f), new Vector3(sx, 4.3f * s, -3.5f * s));
            }
            // Storage yard
            Cyl(gran.transform, "Yard", DirtMat, new Vector3(5f * s, 0.05f, 3f * s), new Vector3(0f, 0.03f, 4f * s));
        }

        // --- Aqueduct ---
        private static void CreateAqueductBuilding(Transform parent, int era, int bldLevel, Vector3 pos)
        {
            GameObject aq = new GameObject("Aqueduct");
            aq.transform.SetParent(parent);
            aq.transform.localPosition = pos;

            float s = LevelScale(bldLevel);
            Material stone = era >= 2 ? LightStone : StoneWall;

            // Arched supports
            int archCount = 3 + bldLevel;
            float spacing = 2.5f;
            for (int i = 0; i < archCount; i++)
            {
                float x = -spacing * (archCount - 1) * 0.5f + i * spacing;
                // Pillar
                Box(aq.transform, $"Pillar_{i}", stone, new Vector3(0.8f, 5f * s, 0.8f), new Vector3(x, 2.5f * s, 0f));
                // Arch (between pillars)
                if (i < archCount - 1)
                {
                    float midX = x + spacing * 0.5f;
                    Cyl(aq.transform, $"Arch_{i}", stone, new Vector3(spacing * 0.45f, 0.3f, 0.6f), new Vector3(midX, 4f * s, 0f));
                }
            }
            // Top water channel
            Box(aq.transform, "Channel", stone, new Vector3(archCount * spacing, 0.4f, 1f), new Vector3(0f, 5.2f * s, 0f));
            // Water in channel
            Box(aq.transform, "Water", WaterMat, new Vector3(archCount * spacing - 0.4f, 0.15f, 0.6f), new Vector3(0f, 5.35f * s, 0f));
        }

        // --- Customs House ---
        private static void CreateCustomsHouseBuilding(Transform parent, int era, int bldLevel, Vector3 pos)
        {
            GameObject customs = new GameObject("CustomsHouse");
            customs.transform.SetParent(parent);
            customs.transform.localPosition = pos;

            float s = LevelScale(bldLevel);
            Material wall = era >= 2 ? BrickWall : StoneWall;

            // Main office
            Box(customs.transform, "Office", wall, new Vector3(4f * s, 4f * s, 3f * s), new Vector3(0f, 2f * s, 0f));
            Box(customs.transform, "Roof", RoofMat(era), new Vector3(4.5f * s, 0.8f, 3.5f * s), new Vector3(0f, 4.4f * s, 0f));
            // Warehouses
            for (int w = 0; w < Mathf.Min(bldLevel, 3); w++)
            {
                float wz = 4f + w * 3.5f;
                Box(customs.transform, $"Warehouse_{w}", wall, new Vector3(3.5f * s, 3f * s, 3f * s), new Vector3(0f, 1.5f * s, wz * s * 0.5f));
            }
            // Scale with goods display
            Box(customs.transform, "ScaleBase", DarkStone, new Vector3(1f, 0.5f, 1f), new Vector3(-3f * s, 0.25f, 0f));
            Cyl(customs.transform, "ScalePole", MetalMat, new Vector3(0.1f, 1.5f, 0.1f), new Vector3(-3f * s, 1.25f, 0f));
        }

        // --- Roads ---
        private static void CreateRoadsBuilding(Transform parent, int era, int bldLevel, Vector3 pos)
        {
            GameObject roads = new GameObject("Roads");
            roads.transform.SetParent(parent);
            roads.transform.localPosition = pos;

            Material roadMat = era >= 1 ? LightStone : DirtMat;
            float roadLen = 8f + bldLevel * 3f;

            // Main road stretching outward
            Box(roads.transform, "Road1", roadMat, new Vector3(roadLen, 0.08f, 2f), new Vector3(0f, 0.04f, 0f));
            // Cross road
            Box(roads.transform, "Road2", roadMat, new Vector3(2f, 0.08f, roadLen * 0.7f), new Vector3(0f, 0.04f, 0f));
            // Stone markers
            for (int m = 0; m < bldLevel; m++)
            {
                float mx = -roadLen * 0.4f + m * (roadLen * 0.4f);
                Box(roads.transform, $"Marker_{m}", DarkStone, new Vector3(0.3f, 1f, 0.3f), new Vector3(mx, 0.5f, 1.2f));
            }
        }

        // --- Port (main city variant) ---
        private static void CreatePortMainCity(Transform parent, int era, int bldLevel, Vector3 pos)
        {
            GameObject port = new GameObject("CityPort");
            port.transform.SetParent(parent);
            port.transform.localPosition = pos;

            float s = LevelScale(bldLevel);
            // Quay
            Box(port.transform, "Quay", DarkStone, new Vector3(10f * s, 1f, 1.5f), new Vector3(0f, 0.5f, 3f));
            // Water
            Cyl(port.transform, "Water", WaterMat, new Vector3(10f * s, 0.05f, 4f), new Vector3(0f, 0.02f, 6f));
            // Warehouses
            for (int w = 0; w < Mathf.Min(bldLevel + 1, 4); w++)
            {
                float wx = -4f + w * 3f;
                Box(port.transform, $"Warehouse_{w}", era >= 2 ? BrickWall : WoodWall, new Vector3(2.5f, 3f * s, 3f * s), new Vector3(wx, 1.5f * s, -1f));
            }
            // Crane (era 1+)
            if (era >= 1)
            {
                Cyl(port.transform, "CranePole", WoodWall, new Vector3(0.3f, 4f * s, 0.3f), new Vector3(4f * s, 4f * s, 3f));
                Box(port.transform, "CraneArm", WoodWall, new Vector3(0.2f, 0.2f, 3.5f), new Vector3(4f * s, 8f * s, 4.5f));
            }
        }

        // --- Artillery School ---
        private static void CreateArtillerySchool(Transform parent, int era, int bldLevel, Vector3 pos)
        {
            GameObject school = new GameObject("ArtillerySchool");
            school.transform.SetParent(parent);
            school.transform.localPosition = pos;

            float scale = 1f + bldLevel * 0.15f;
            Material wall = era >= 2 ? BrickWall : StoneWall;

            Box(school.transform, "Main", wall, new Vector3(5f * scale, 5f, 4f * scale), new Vector3(0f, 2.5f, 0f));
            Box(school.transform, "Roof", SlateRoof, new Vector3(5.5f * scale, 0.8f, 4.5f * scale), new Vector3(0f, 5.4f, 0f));
            // Practice range
            Cyl(school.transform, "Range", DirtMat, new Vector3(4f * scale, 0.05f, 4f * scale), new Vector3(0f, 0.05f, 5f));
            // Cannon models
            for (int i = 0; i < Mathf.Min(bldLevel, 3); i++)
            {
                float x = -1.5f + i * 1.5f;
                Cyl(school.transform, $"Cannon_{i}", MetalMat, new Vector3(0.2f, 1f, 0.2f), new Vector3(x, 0.3f, 5f))
                    .transform.localRotation = Quaternion.Euler(80f, 0f, 0f);
            }
        }

        // ==================== SPECIALIZATION BUILDINGS (sub-cities) ====================

        private static void CreateSpecializationBuilding(Transform parent, CitySpecialization spec, int era, float radius)
        {
            Vector3 center = Vector3.zero;

            switch (spec)
            {
                case CitySpecialization.Agriculture:
                    CreateFarmBuilding(parent, era, 1, center);
                    // Extra fields
                    Box(parent, "BigField", GreenMat, new Vector3(radius, 0.05f, radius * 0.6f), new Vector3(0f, 0.02f, radius * 0.5f));
                    break;

                case CitySpecialization.Industry:
                    CreateWorkshop(parent, era, center);
                    break;

                case CitySpecialization.Commerce:
                    CreateMarketSquare(parent, era, radius * 0.8f, center);
                    break;

                case CitySpecialization.Military:
                    CreateBarracksBuilding(parent, era, 1, center);
                    break;

                case CitySpecialization.Mining:
                    CreateMineBuilding(parent, era, 1, center);
                    break;

                case CitySpecialization.Fishing:
                    CreateFishingHarbor(parent, era, center);
                    break;

                case CitySpecialization.Forestry:
                    CreateSawmill(parent, era, center);
                    break;

                case CitySpecialization.Port:
                    CreatePort(parent, era, center, radius);
                    break;

                case CitySpecialization.University:
                    CreateUniversityBuilding(parent, era, 1, center);
                    break;

                default:
                    // Generic small building
                    Box(parent, "GenericBld", WallMat(era), new Vector3(3f, 3f, 3f), new Vector3(0f, 1.5f, 0f));
                    break;
            }
        }

        private static void CreateWorkshop(Transform parent, int era, Vector3 pos)
        {
            GameObject ws = new GameObject("Workshop");
            ws.transform.SetParent(parent);
            ws.transform.localPosition = pos;

            Material wall = era >= 2 ? BrickWall : WoodWall;
            Box(ws.transform, "Body", wall, new Vector3(5f, 4f, 5f), new Vector3(0f, 2f, 0f));
            Box(ws.transform, "Roof", SlateRoof, new Vector3(5.5f, 0.8f, 5.5f), new Vector3(0f, 4.4f, 0f));
            // Chimney (era 2+)
            if (era >= 2)
            {
                Cyl(ws.transform, "Chimney", BrickWall, new Vector3(0.6f, 3f, 0.6f), new Vector3(2f, 6f, -1f));
                // Smoke (small grey sphere)
                Sphere(ws.transform, "Smoke", GetMat("cv_smoke", new Color(0.5f, 0.5f, 0.5f, 0.5f)), new Vector3(1.5f, 1f, 1.5f), new Vector3(2f, 8f, -1f));
            }
            // Warehouse
            Box(ws.transform, "Warehouse", wall, new Vector3(4f, 3f, 3f), new Vector3(5f, 1.5f, 0f));
        }

        private static void CreateFishingHarbor(Transform parent, int era, Vector3 pos)
        {
            GameObject harbor = new GameObject("FishingHarbor");
            harbor.transform.SetParent(parent);
            harbor.transform.localPosition = pos;

            // Dock
            Box(harbor.transform, "Dock", WoodWall, new Vector3(6f, 0.3f, 2f), new Vector3(0f, 0.3f, 3f));
            // Pilings
            for (int i = 0; i < 4; i++)
                Cyl(harbor.transform, $"Piling_{i}", WoodWall, new Vector3(0.2f, 0.8f, 0.2f), new Vector3(-2.5f + i * 1.7f, 0f, 3f));
            // Water
            Cyl(harbor.transform, "Water", WaterMat, new Vector3(8f, 0.05f, 4f), new Vector3(0f, 0.02f, 5f));
            // Fishing hut
            Box(harbor.transform, "Hut", WoodWall, new Vector3(2.5f, 2.5f, 2.5f), new Vector3(-3f, 1.25f, 0f));
            Box(harbor.transform, "HutRoof", ThatchRoof, new Vector3(3f, 0.5f, 3f), new Vector3(-3f, 2.75f, 0f));
            // Nets drying (thin planes)
            Box(harbor.transform, "Net", GetMat("cv_net", new Color(0.6f, 0.55f, 0.45f)), new Vector3(3f, 2f, 0.05f), new Vector3(3f, 1f, 0f));
        }

        private static void CreateSawmill(Transform parent, int era, Vector3 pos)
        {
            GameObject saw = new GameObject("Sawmill");
            saw.transform.SetParent(parent);
            saw.transform.localPosition = pos;

            Box(saw.transform, "Mill", WoodWall, new Vector3(4f, 3f, 4f), new Vector3(0f, 1.5f, 0f));
            Box(saw.transform, "Roof", ThatchRoof, new Vector3(4.5f, 0.6f, 4.5f), new Vector3(0f, 3.3f, 0f));
            // Water wheel
            Cyl(saw.transform, "Wheel", WoodWall, new Vector3(3f, 0.3f, 3f), new Vector3(-2.5f, 1.5f, 0f))
                .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            // Log piles
            for (int i = 0; i < 5; i++)
            {
                Cyl(saw.transform, $"Log_{i}", WoodWall, new Vector3(0.3f, 1.5f, 0.3f),
                    new Vector3(3f + i * 0.4f, 0.15f, Random.Range(-2f, 2f)))
                    .transform.localRotation = Quaternion.Euler(90f, 0f, Random.Range(-10f, 10f));
            }
        }

        private static void CreatePort(Transform parent, int era, Vector3 pos, float radius)
        {
            GameObject port = new GameObject("Port");
            port.transform.SetParent(parent);
            port.transform.localPosition = pos;

            // Harbor wall
            Box(port.transform, "Quay", DarkStone, new Vector3(radius * 1.5f, 1f, 1f), new Vector3(0f, 0.5f, radius * 0.5f));
            // Water
            Cyl(port.transform, "Water", WaterMat, new Vector3(radius * 1.2f, 0.05f, radius * 0.8f), new Vector3(0f, 0.02f, radius * 0.8f));
            // Warehouses
            for (int i = 0; i < 3; i++)
            {
                float x = -radius * 0.4f + i * radius * 0.4f;
                Box(port.transform, $"Warehouse_{i}", era >= 2 ? BrickWall : WoodWall,
                    new Vector3(3f, 3f, 4f), new Vector3(x, 1.5f, -1f));
            }
            // Crane (era 1+)
            if (era >= 1)
            {
                Cyl(port.transform, "CranePole", WoodWall, new Vector3(0.3f, 4f, 0.3f), new Vector3(radius * 0.3f, 4f, radius * 0.5f));
                Box(port.transform, "CraneArm", WoodWall, new Vector3(0.2f, 0.2f, 4f), new Vector3(radius * 0.3f, 8f, radius * 0.5f + 2f));
            }
        }

        // ==================== FLAGPOLE ====================

        private static void CreateFlagpole(Transform parent, FactionType faction, int level, bool isNationCapital)
        {
            float poleH = isNationCapital ? 18f : (level >= 3 ? 12f : 7f);
            float poleT = isNationCapital ? 0.3f : 0.15f;
            float flagW = isNationCapital ? 5f : (level >= 3 ? 3f : 2f);
            float flagH = flagW * 0.6f;

            Cyl(parent, "Flagpole", MetalMat, new Vector3(poleT, poleH * 0.5f, poleT), new Vector3(0f, poleH * 0.5f, 0f));
            Box(parent, "Flag", GetFactionFlag(faction), new Vector3(flagW, flagH, 0.1f),
                new Vector3(flagW * 0.5f, poleH - flagH * 0.5f, 0f));
        }
    }
}
