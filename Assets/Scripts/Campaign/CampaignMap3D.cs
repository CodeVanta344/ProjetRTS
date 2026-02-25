using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Linq;
using NapoleonicWars.Campaign;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    public class CampaignMap3D : MonoBehaviour
    {
        [Header("Map Settings")]
        public Material mapMaterial;
        public Material borderMaterial;
        public Material waterMaterial;
        public Texture2D mapTexture;
        public Texture2D europeGroundTexture;
        
        [Header("Terrain Settings")]
        public Texture2D heightmapTexture;
        public int terrainResolution = 513;
        public float terrainHeightScale = 3f;
        public float mapWidth = 10000f;
        public float mapHeight = 7500f;
        public float baseTerrainHeight = 0.5f;
        
        private float terrainHeight => baseTerrainHeight;
        
        [Header("Visual Settings")]
        public Color seaColor = new Color(0.12f, 0.20f, 0.35f);
        public Color landColor = new Color(0.35f, 0.45f, 0.25f);
        public Color[] factionLandColors;
        
        private static Dictionary<string, Material> materialPool = new Dictionary<string, Material>();
        
        // Cities
        private List<CityMapMarker> cityMarkers = new List<CityMapMarker>();
        private List<CityNameLabel> cityLabels = new List<CityNameLabel>();
        private GameObject citiesContainer;
        private GameObject cityLabelsContainer;
        
        // City hierarchies per province
        private Dictionary<string, CityHierarchy> cityHierarchies = new Dictionary<string, CityHierarchy>();
        public Dictionary<string, CityHierarchy> ProvinceHierarchies => cityHierarchies;
        
        // Armies
        private Dictionary<string, ArmyMapMarker> armyMarkers = new Dictionary<string, ArmyMapMarker>();
        private GameObject armiesContainer;
        
        // Logistics convoys & routes
        private List<GameObject> convoyVisuals = new List<GameObject>();
        private List<GameObject> routeVisuals = new List<GameObject>();
        private GameObject convoysContainer;
        private float convoyAnimTimer = 0f;
        
        // Supply wagons (city → army resupply)
        private Dictionary<string, GameObject> wagonMarkers = new Dictionary<string, GameObject>();
        private GameObject wagonsContainer;
        
        // Selection
        private ArmyMapMarker selectedArmyMarker;
        private ProvinceData selectedTargetProvince;
        private List<GameObject> movementPathVisuals = new List<GameObject>();
        
        // Movement arrows: armyId → (lineObj, labelObj)
        private Dictionary<string, (GameObject line, GameObject label)> movementArrows = new Dictionary<string, (GameObject, GameObject)>();
        private Dictionary<string, float> smoothMarchProgress = new Dictionary<string, float>();
        
        // References
        private CampaignManager campaignManager;
        private Camera mainCamera;
        
        // Events
        public System.Action<ProvinceData> OnCityClicked;
        public System.Action<ArmyData> OnArmyClicked;
        public System.Action<ArmyData> OnArmySelected;
        public System.Action<ArmyData, ProvinceData> OnArmyMoveOrdered;
        public System.Action<ArmyData, ArmyData, ProvinceData> OnBattleTriggered;
        
        // HoI4 map system (kept for external references)
        private Material hoi4Material;
        private Texture2D provinceIdMap;
        private Texture2D factionColorMap;
        private Dictionary<string, int> provinceIndexLookup = new Dictionary<string, int>();
        private MeshRenderer terrainMeshRenderer;
        
        private bool isInitialized = false;
        
        // ==================== MATERIAL POOL ====================
        private Material GetPooledMaterial(string key, string shaderName, Color color)
        {
            if (materialPool.TryGetValue(key, out Material m) && m != null) return m;
            Shader sh = Shader.Find(shaderName);
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(sh);
            m.color = color;
            materialPool[key] = m;
            return m;
        }
        
        // ==================== LIFECYCLE ====================
        private void Start()
        {
            try
            {
                campaignManager = CampaignManager.Instance;
                mainCamera = Camera.main;
                
                if (mainCamera == null)
                {
                    foreach (var cam in Camera.allCameras)
                    {
                        if (cam.CompareTag("MainCamera")) { mainCamera = cam; break; }
                    }
                    if (mainCamera == null && Camera.allCameras.Length > 0)
                        mainCamera = Camera.allCameras[0];
                }
                
                CreateMapMesh();
                
                CreateCitiesContainer();
                CreateArmiesContainer();
                
                if (campaignManager != null)
                {
                    InitializeCities();
                    InitializeArmies();
                }
                
                isInitialized = true;
                FocusCameraOnCapital();
                
                Debug.Log($"[CampaignMap3D] Map initialized. Provinces: {(campaignManager != null ? campaignManager.Provinces.Count : 0)}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CampaignMap3D] CRITICAL ERROR during Start(): {e.Message}\n{e.StackTrace}");
            }
        }
        
        private void OnDestroy()
        {
            if (NapoleonicWars.Network.NetworkCampaignManager.Instance != null)
            {
                NapoleonicWars.Network.NetworkCampaignManager.Instance.OnArmyMoved -= OnNetworkArmyMoved;
            }
        }
        
        private void OnNetworkArmyMoved(string armyId, string fromProvinceId, string toProvinceId)
        {
            Debug.Log($"[CampaignMap3D] Network army moved: {armyId} from {fromProvinceId} to {toProvinceId}");
            InitializeArmies();
        }
        
        private void Update()
        {
            if (!isInitialized) return;
            
            if (campaignManager == null)
            {
                campaignManager = CampaignManager.Instance;
                if (campaignManager != null)
                {
                    try
                    {
                        InitializeCities();
                        InitializeArmies();
                        InitializeConvoys();
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[CampaignMap3D] Deferred init error: {e.Message}\n{e.StackTrace}");
                    }
                }
                return;
            }
            
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null) return;
            }
            
            // City edit mode toggle
            CityMapMarker.EditMode = Input.GetKey(KeyCode.LeftAlt);
            
            if (!CityMapMarker.EditMode)
                HandleInput();
            UpdateArmyPositions();
            UpdateConvoyAnimation();
        }
        
        private void OnGUI()
        {
            if (CityMapMarker.EditMode)
            {
                GUIStyle style = new GUIStyle(GUI.skin.box);
                style.fontSize = 22;
                style.fontStyle = FontStyle.Bold;
                style.normal.textColor = Color.yellow;
                style.alignment = TextAnchor.MiddleCenter;
                GUI.Box(new Rect(Screen.width / 2 - 160, 10, 320, 40), "⚙ CITY EDIT MODE (Alt)", style);
            }
        }
        
        // ==================== COORDINATE TRANSFORM ====================
        // Maps game normalized coords to the reference image coords
        private const float Y_SCALE = 1.163f;
        private const float Y_OFFSET = -0.154f;
        
        // ==================== TERRAIN HEIGHT ====================
        public float GetTerrainHeight(float x, float z)
        {
            return baseTerrainHeight;
        }
        
        // ==================== MAP MESH & TEXTURES ====================
        private void CreateMapMesh()
        {
            // --- 1. Create flat quad ---
            GameObject mapObj = new GameObject("CampaignMapQuad");
            mapObj.transform.SetParent(transform);
            mapObj.transform.localPosition = Vector3.zero;
            mapObj.layer = gameObject.layer;
            
            MeshFilter mf = mapObj.AddComponent<MeshFilter>();
            terrainMeshRenderer = mapObj.AddComponent<MeshRenderer>();
            
            // Build a single quad mesh
            Mesh mesh = new Mesh();
            mesh.name = "CampaignMapMesh";
            float hw = mapWidth * 0.5f;
            float hh = mapHeight * 0.5f;
            mesh.vertices = new Vector3[] {
                new Vector3(-hw, 0, -hh),
                new Vector3( hw, 0, -hh),
                new Vector3( hw, 0,  hh),
                new Vector3(-hw, 0,  hh)
            };
            mesh.uv = new Vector2[] {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };
            mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.mesh = mesh;
            
            // Add collider for raycasting
            BoxCollider bc = mapObj.AddComponent<BoxCollider>();
            bc.size = new Vector3(mapWidth, 0.1f, mapHeight);
            bc.center = Vector3.zero;
            
            // --- 2. Load terrain texture ---
            Texture2D terrainTex = europeGroundTexture;
            if (terrainTex == null)
                terrainTex = Resources.Load<Texture2D>("Campaign/europe_terrain");
            if (terrainTex == null)
            {
                Debug.LogError("[CampaignMap3D] europe_terrain texture not found!");
                terrainTex = new Texture2D(4, 4);
            }
            
            // --- 3. Generate province ID map, faction LUT, and height map ---
            Texture2D provIdMap = null;
            Texture2D factionLUT = null;
            Texture2D heightMap = null;
            
            if (campaignManager != null && campaignManager.Provinces.Count > 0)
            {
                provIdMap = GenerateProvinceIdMap(4096, 4096);
                factionLUT = GenerateFactionColorLUT();
                heightMap = GenerateHeightMap(2048, 2048, terrainTex);
            }
            else
            {
                Debug.LogWarning("[CampaignMap3D] No provinces — skipping political overlay");
                provIdMap = new Texture2D(4, 4, TextureFormat.R8, false);
                factionLUT = new Texture2D(256, 1, TextureFormat.RGBA32, false);
                heightMap = new Texture2D(4, 4, TextureFormat.R8, false);
            }
            
            // --- 4. Create material with HoI4 shader ---
            Shader shader = Shader.Find("NapoleonicWars/HoI4Map");
            if (shader == null)
            {
                Debug.LogError("[CampaignMap3D] Shader 'NapoleonicWars/HoI4Map' not found!");
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            
            hoi4Material = new Material(shader);
            hoi4Material.SetTexture("_BaseMap", terrainTex);
            hoi4Material.SetTexture("_HeightMap", heightMap);
            hoi4Material.SetTexture("_ProvinceMap", provIdMap);
            hoi4Material.SetTexture("_FactionMap", factionLUT);
            hoi4Material.SetFloat("_PoliticalAlpha", 0.50f);
            hoi4Material.SetFloat("_Desaturation", 0.30f);
            hoi4Material.SetFloat("_WaterThreshold", 0.70f);
            hoi4Material.SetFloat("_NatBorderWidth", 3.5f);
            hoi4Material.SetFloat("_ProvBorderWidth", 1.5f);
            hoi4Material.SetFloat("_FlagAlpha", 0f);
            hoi4Material.SetFloat("_ZoomFade", 0f);
            hoi4Material.SetFloat("_DebugMode", 0f);
            
            terrainMeshRenderer.material = hoi4Material;
            
            // Store textures for cleanup
            provinceIdMap = provIdMap;
            factionColorMap = factionLUT;
            
            // --- 5. Create sea plane underneath ---
            CreateSeaPlane();
            
            Debug.Log($"[CampaignMap3D] Map mesh created: {mapWidth}x{mapHeight}, terrain: {terrainTex.width}x{terrainTex.height}");
        }
        
        private void CreateSeaPlane()
        {
            GameObject sea = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sea.name = "SeaPlane";
            sea.transform.SetParent(transform);
            sea.transform.localPosition = new Vector3(0, -0.05f, 0);
            sea.transform.localRotation = Quaternion.Euler(90, 0, 0);
            sea.transform.localScale = new Vector3(mapWidth * 1.5f, mapHeight * 1.5f, 1);
            sea.layer = gameObject.layer;
            
            // Dark blue sea material
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            Material seaMat = new Material(sh);
            seaMat.color = seaColor;
            sea.GetComponent<MeshRenderer>().material = seaMat;
            
            // Remove collider from sea
            var col = sea.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }
        
        /// <summary>
        /// Generate a 2D texture where each pixel's R channel encodes the nearest province index.
        /// Computes Voronoi at low resolution then upscales for performance.
        /// </summary>
        private Texture2D GenerateProvinceIdMap(int w, int h)
        {
            var provinces = campaignManager.Provinces;
            var provPositions = new List<Vector2>();
            var provKeys = new List<string>();
            int idx = 0;
            
            foreach (var kvp in provinces)
            {
                Vector2 gamePos = kvp.Value.mapPosition;
                float imgY = gamePos.y * Y_SCALE + Y_OFFSET;
                provPositions.Add(new Vector2(gamePos.x, imgY));
                provKeys.Add(kvp.Key);
                provinceIndexLookup[kvp.Key] = idx;
                idx++;
            }
            
            int provCount = provPositions.Count;
            float[] posX = new float[provCount];
            float[] posY = new float[provCount];
            for (int i = 0; i < provCount; i++)
            {
                posX[i] = provPositions[i].x;
                posY[i] = provPositions[i].y;
            }
            
            // Compute at low res for speed, then upscale
            int lowW = Mathf.Min(w, 1024);
            int lowH = Mathf.Min(h, 1024);
            byte[] lowRes = new byte[lowW * lowH];
            
            for (int py = 0; py < lowH; py++)
            {
                float ny = (float)py / lowH;
                for (int px = 0; px < lowW; px++)
                {
                    float nx = (float)px / lowW;
                    float minDist = float.MaxValue;
                    int nearest = 0;
                    
                    for (int i = 0; i < provCount; i++)
                    {
                        float dx = nx - posX[i];
                        float dy = ny - posY[i];
                        float d = dx * dx + dy * dy;
                        if (d < minDist) { minDist = d; nearest = i; }
                    }
                    lowRes[py * lowW + px] = (byte)nearest;
                }
            }
            
            // Upscale to target resolution with nearest-neighbor
            Texture2D tex = new Texture2D(w, h, TextureFormat.R8, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[w * h];
            
            for (int py = 0; py < h; py++)
            {
                int srcY = (py * lowH) / h;
                for (int px = 0; px < w; px++)
                {
                    int srcX = (px * lowW) / w;
                    int nearest = lowRes[srcY * lowW + srcX];
                    pixels[py * w + px] = new Color(nearest / 255f, 0, 0, 1);
                }
            }
            
            tex.SetPixels(pixels);
            tex.Apply();
            
            Debug.Log($"[CampaignMap3D] Province ID map: {w}x{h} (computed at {lowW}x{lowH}), {provCount} provinces");
            return tex;
        }
        
        /// <summary>
        /// Generate a 256x1 LUT: pixel N stores the faction color for province index N.
        /// The alpha channel stores the faction index (for flag lookup).
        /// </summary>
        private Texture2D GenerateFactionColorLUT()
        {
            Texture2D tex = new Texture2D(256, 1, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            
            Color[] lut = new Color[256];
            for (int i = 0; i < 256; i++)
                lut[i] = new Color(0.2f, 0.2f, 0.2f, 0); // default neutral
            
            // Map faction types to indices for flag atlas
            Dictionary<FactionType, int> factionIndices = new Dictionary<FactionType, int>();
            int fi = 0;
            foreach (FactionType ft in System.Enum.GetValues(typeof(FactionType)))
                factionIndices[ft] = fi++;
            
            foreach (var kvp in campaignManager.Provinces)
            {
                if (provinceIndexLookup.TryGetValue(kvp.Key, out int pidx) && pidx < 256)
                {
                    Color fc = GetFactionColor(kvp.Value.owner);
                    float fIdx = factionIndices.ContainsKey(kvp.Value.owner) 
                        ? factionIndices[kvp.Value.owner] / (float)fi : 0f;
                    lut[pidx] = new Color(fc.r, fc.g, fc.b, fIdx);
                }
            }
            
            tex.SetPixels(lut);
            tex.Apply();
            
            Debug.Log($"[CampaignMap3D] Faction LUT generated: {fi} factions");
            return tex;
        }
        
        /// <summary>
        /// Generate a height map where water = white (high) and land = black (low).
        /// Uses the terrain texture's color to detect water (dark blue = ocean).
        /// </summary>
        private Texture2D GenerateHeightMap(int w, int h, Texture2D terrainTex)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.R8, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            
            Color[] pixels = new Color[w * h];
            
            for (int py = 0; py < h; py++)
            {
                float v = (float)py / h;
                for (int px = 0; px < w; px++)
                {
                    float u = (float)px / w;
                    Color sample = terrainTex.GetPixelBilinear(u, v);
                    
                    // Detect water: dark blue pixels
                    // Convert to HSV to check hue
                    Color.RGBToHSV(sample, out float hue, out float sat, out float val);
                    
                    bool isWater = (hue > 0.55f && hue < 0.72f && sat > 0.15f && val < 0.45f);
                    
                    // Water = white (high height = water in shader), Land = black
                    pixels[py * w + px] = isWater ? Color.white : Color.black;
                }
            }
            
            tex.SetPixels(pixels);
            tex.Apply();
            
            Debug.Log($"[CampaignMap3D] Height map generated: {w}x{h}");
            return tex;
        }
        
        // ==================== FACTION COLORS ====================
        public void RefreshFactionColors()
        {
            // TODO: Update faction color texture when province ownership changes
        }
        
        public void RefreshAllVisuals()
        {
            RefreshFactionColors();
            InitializeArmies();
            
            foreach (var kvp in cityMarkers)
            {
                if (kvp != null && kvp.gameObject != null)
                    Destroy(kvp.gameObject);
            }
            cityMarkers.Clear();
            cityHierarchies.Clear();
            InitializeCities();
        }
        
        // ==================== CITIES ====================
        private void CreateCitiesContainer()
        {
            citiesContainer = new GameObject("Cities");
            citiesContainer.transform.SetParent(transform);
            cityLabelsContainer = new GameObject("CityLabels");
            cityLabelsContainer.transform.SetParent(transform);
        }
        
        private void CreateArmiesContainer()
        {
            armiesContainer = new GameObject("Armies");
            armiesContainer.transform.SetParent(transform);
        }
        
        private void InitializeCities()
        {
            var provinces = campaignManager.Provinces;
            int ok = 0;
            
            foreach (Transform child in citiesContainer.transform) Destroy(child.gameObject);
            foreach (Transform child in cityLabelsContainer.transform) Destroy(child.gameObject);
            cityMarkers.Clear();
            cityLabels.Clear();
            cityHierarchies.Clear();
            
            foreach (var kvp in provinces)
            {
                ProvinceData prov = kvp.Value;
                try
                {
                    CityHierarchy hierarchy = CreateCityHierarchy(prov);
                    
                    Vector2 snappedPos = SnapToLand(prov.mapPosition);
                    if (snappedPos != prov.mapPosition)
                    {
                        prov.mapPosition = snappedPos;
                    }
                    
                    Vector3 worldPos = MapToWorldPosition(prov.mapPosition);
                    
                    CityMapMarker marker = CreateCityMarker(prov, worldPos);
                    cityMarkers.Add(marker);
                    cityHierarchies[prov.provinceId] = hierarchy;
                    
                    CityNameLabel capitalLabel = CreateCityNameLabelReturn(prov, hierarchy.Capital, worldPos, true);
                    if (capitalLabel != null)
                    {
                        marker.LinkedLabel = capitalLabel;
                        // Link label to CityDevelopmentVisuals for building icon updates
                        var devVisuals = marker.GetComponent<CityDevelopmentVisuals>();
                        if (devVisuals != null)
                        {
                            devVisuals.LinkedLabel = capitalLabel;
                            devVisuals.RefreshLabel();
                        }
                    }
                    ok++;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[CampaignMap3D] City init error for '{kvp.Key}': {e.Message}");
                }
            }
            
            Debug.Log($"[CampaignMap3D] Cities: {ok}/{provinces.Count}");
        }
        
        private CityHierarchy CreateCityHierarchy(ProvinceData prov)
        {
            CityHierarchy h = new CityHierarchy(prov.provinceId, prov.provinceName, prov.owner);
            CityData capital = campaignManager.GetCityForProvince(prov.provinceId);
            if (capital == null)
                capital = new CityData($"city_{prov.provinceId}", prov.provinceName, prov.provinceId, prov.mapPosition, prov.owner);
            h.InitializeCapital(capital);
            GenerateSubCities(h, prov);
            return h;
        }
        
        private void GenerateSubCities(CityHierarchy hierarchy, ProvinceData prov)
        {
            var specs = new List<CitySpecialization> { CitySpecialization.Agriculture, CitySpecialization.Commerce, CitySpecialization.Industry };
            if (IsCoastalProvince(prov)) specs.Add(CitySpecialization.Port);
            if (IsMountainousProvince(prov)) specs.Add(CitySpecialization.Mining);
            if (IsForestedProvince(prov)) specs.Add(CitySpecialization.Forestry);
            
            string[] names = GetSubCityNames(prov.provinceId, prov.provinceName);
            int n = Mathf.Min(hierarchy.MaxSubCities, names.Length);
            for (int i = 0; i < n && i < specs.Count; i++)
            {
                float angle = (i / (float)n) * Mathf.PI * 2f;
                Vector2 offset = new Vector2(Mathf.Cos(angle) * 0.03f, Mathf.Sin(angle) * 0.03f);
                hierarchy.AddSubCity(names[i], specs[i], offset);
            }
        }
        
        private string[] GetSubCityNames(string provinceId, string provinceName)
        {
            return provinceId.ToLower() switch
            {
                "paris" => new[] { "Versailles", "Saint-Denis", "Chartres", "Orléans" },
                "london" => new[] { "Westminster", "Greenwich", "Windsor", "Richmond" },
                "berlin" => new[] { "Potsdam", "Spandau", "Cölln", "Köpenick" },
                "vienna" => new[] { "Schönbrunn", "Floridsdorf", "Liesing", "Hietzing" },
                "madrid" => new[] { "Toledo", "Escorial", "Aranjuez", "El Pardo" },
                "marseille" => new[] { "Aix", "Toulon", "Avignon", "Nice" },
                "milan" => new[] { "Pavia", "Como", "Bergamo", "Novara" },
                "naples" => new[] { "Pompei", "Caserta", "Salerno", "Benevento" },
                "constantinople" => new[] { "Galata", "Üsküdar", "Kadıköy", "Eminönü" },
                _ => new[] { $"{provinceName} North", $"{provinceName} South", $"{provinceName} East", $"{provinceName} West" }
            };
        }
        
        private bool IsCoastalProvince(ProvinceData p) =>
            p.mapPosition.x < 0.15f || p.mapPosition.x > 0.85f || p.mapPosition.y < 0.15f || p.mapPosition.y > 0.85f ||
            p.provinceId.ToLower().Contains("portugal") || p.provinceId.ToLower().Contains("naples") ||
            p.provinceId.ToLower().Contains("london") || p.provinceId.ToLower().Contains("holland");
        
        private bool IsMountainousProvince(ProvinceData p) { string id = p.provinceId.ToLower(); return id.Contains("alps") || id.Contains("pyrenees") || id.Contains("carpathian") || id.Contains("switzerland"); }
        private bool IsForestedProvince(ProvinceData p) { string id = p.provinceId.ToLower(); return id.Contains("rhineland") || id.Contains("prussia") || id.Contains("poland") || id.Contains("sweden"); }
        
        private void CreateCityNameLabel(ProvinceData prov, CityNode cityNode, Vector3 worldPos, bool isCapital)
        {
            CreateCityNameLabelReturn(prov, cityNode, worldPos, isCapital);
        }
        
        private CityNameLabel CreateCityNameLabelReturn(ProvinceData prov, CityNode cityNode, Vector3 worldPos, bool isCapital)
        {
            if (cityNode == null) return null;
            GameObject labelGO = new GameObject($"Label_{cityNode.cityName}");
            labelGO.transform.SetParent(cityLabelsContainer.transform);
            CityNameLabel label = labelGO.AddComponent<CityNameLabel>();
            bool isPlayer = prov.owner == campaignManager.PlayerFaction;
            string subInfo = isCapital ? "Capital" : GetSpecializationDisplay(cityNode.specialization);
            label.Initialize(cityNode.cityName, subInfo, isCapital, isPlayer);
            label.SetPosition(worldPos);
            cityLabels.Add(label);
            return label;
        }
        
        private string GetSpecializationDisplay(CitySpecialization spec) => spec switch
        {
            CitySpecialization.Agriculture => "Agricultural", CitySpecialization.Industry => "Industrial",
            CitySpecialization.Commerce => "Commercial", CitySpecialization.Military => "Military",
            CitySpecialization.Mining => "Mining", CitySpecialization.Fishing => "Fishing",
            CitySpecialization.Forestry => "Forestry", CitySpecialization.Port => "Port",
            CitySpecialization.University => "University", _ => "Town"
        };
        
        private CityMapMarker CreateCityMarker(ProvinceData province, Vector3 worldPos)
        {
            bool isNationCapital = IsCapitalProvince(province.provinceId);
            
            GameObject cityGO = new GameObject($"City_{province.provinceName}");
            cityGO.transform.SetParent(citiesContainer.transform);
            cityGO.transform.position = worldPos;
            
            float cityScale = isNationCapital ? 60.0f : 40.0f;
            cityGO.transform.localScale = Vector3.one * cityScale;
            
            CityData cityData = campaignManager.GetCityForProvince(province.provinceId);
            int techEra = GetTechEraForFaction(province.owner);
            
            CityDevelopmentVisuals devVisuals = cityGO.AddComponent<CityDevelopmentVisuals>();
            devVisuals.Initialize(cityData, province.owner, isNationCapital, techEra);
            
            CityMapMarker marker = cityGO.AddComponent<CityMapMarker>();
            marker.Initialize(province, this);
            
            int level = cityData?.cityLevel ?? 1;
            float colliderRadius = level switch
            {
                1 => 0.8f, 2 => 1.0f, 3 => 1.3f, 4 => 1.6f, 5 => 2.0f, _ => 0.8f
            };
            SphereCollider col = cityGO.AddComponent<SphereCollider>();
            col.radius = colliderRadius; col.isTrigger = false;
            return marker;
        }
        
        private int GetTechEraForFaction(FactionType faction)
        {
            if (campaignManager == null) return 0;
            var factions = campaignManager.Factions;
            if (factions == null || !factions.ContainsKey(faction)) return 0;
            var techTree = factions[faction].techTree;
            if (techTree == null) return 0;
            int ecoTechCount = techTree.GetCompletedCountByCategory(TechCategory.Economy);
            return CityVisualGenerator.GetTechEra(ecoTechCount);
        }
        
        private bool IsCapitalProvince(string id) => id == "paris" || id == "london" || id == "lower_austria" || id == "brandenburg" || id == "moscow" || id == "castile" || id == "thrace";
        
        // ==================== REFRESH ====================
        public void RefreshCityVisuals()
        {
            int refreshed = 0;
            foreach (var marker in cityMarkers)
            {
                if (marker == null || marker.gameObject == null) continue;
                var devVisuals = marker.GetComponent<CityDevelopmentVisuals>();
                if (devVisuals == null) continue;
                
                int techEra = GetTechEraForFaction(marker.ProvinceData.owner);
                if (devVisuals.TryRefresh(techEra))
                {
                    int level = devVisuals.CityData?.cityLevel ?? 1;
                    float colliderRadius = level switch
                    {
                        1 => 12f, 2 => 18f, 3 => 28f, 4 => 38f, 5 => 50f, _ => 12f
                    };
                    var col = marker.GetComponent<SphereCollider>();
                    if (col != null) col.radius = colliderRadius;
                    refreshed++;
                }
                else
                {
                    // Even if no visual rebuild, refresh label (population/building changes)
                    devVisuals.RefreshLabel();
                }
            }
            if (refreshed > 0)
                Debug.Log($"[CampaignMap3D] Refreshed {refreshed} city visuals");
        }
        
        // ==================== LOGISTICS CONVOYS & ROUTES ====================
        
        private void InitializeConvoys()
        {
            if (convoysContainer != null) Destroy(convoysContainer);
            convoysContainer = new GameObject("ConvoysContainer");
            convoysContainer.transform.SetParent(transform);
            RefreshConvoyVisuals();
        }
        
        /// <summary>
        /// Rebuild all convoy and route visuals on the map.
        /// Call after turn processing or when convoys change.
        /// </summary>
        public void RefreshConvoyVisuals()
        {
            // Clear old visuals
            foreach (var go in convoyVisuals) if (go != null) Destroy(go);
            foreach (var go in routeVisuals) if (go != null) Destroy(go);
            convoyVisuals.Clear();
            routeVisuals.Clear();
            
            if (convoysContainer == null)
            {
                convoysContainer = new GameObject("ConvoysContainer");
                convoysContainer.transform.SetParent(transform);
            }
            
            // === Render active convoys ===
            var allConvoys = LogisticsConvoySystem.GetAllActiveConvoys();
            foreach (var convoy in allConvoys)
            {
                if (convoy.routeProvinces == null || convoy.routeProvinces.Count == 0) continue;
                
                // Get world position for current step (lerp between current and next province)
                Vector3 currentPos = GetConvoyWorldPosition(convoy);
                
                // Create 3D visual
                GameObject visual = ConvoyVisualGenerator.CreateConvoyVisual(convoy, convoy.owner);
                visual.transform.SetParent(convoysContainer.transform);
                visual.transform.position = currentPos;
                visual.transform.localScale = Vector3.one * 40f; // Map scale
                
                // Orient towards destination
                if (convoy.currentStep + 1 < convoy.routeProvinces.Count)
                {
                    string nextProv = convoy.routeProvinces[convoy.currentStep + 1];
                    var provinces = campaignManager.Provinces;
                    if (provinces.ContainsKey(nextProv))
                    {
                        Vector3 nextPos = MapToWorldPosition(provinces[nextProv].mapPosition);
                        Vector3 dir = nextPos - currentPos;
                        dir.y = 0;
                        if (dir.sqrMagnitude > 0.01f)
                            visual.transform.rotation = Quaternion.LookRotation(dir);
                    }
                }
                
                convoyVisuals.Add(visual);
            }
            
            // === Render permanent route lines ===
            var allRoutes = LogisticsConvoySystem.GetAllRoutes();
            var provinces2 = campaignManager.Provinces;
            
            foreach (var route in allRoutes)
            {
                if (!route.isActive || route.routePath == null || route.routePath.Count < 2) continue;
                
                // Convert province path to world positions
                List<Vector3> worldPoints = new List<Vector3>();
                foreach (string provId in route.routePath)
                {
                    if (provinces2.ContainsKey(provId))
                    {
                        Vector3 wp = MapToWorldPosition(provinces2[provId].mapPosition);
                        wp.y += 3f; // Slightly above terrain
                        worldPoints.Add(wp);
                    }
                }
                
                if (worldPoints.Count >= 2)
                {
                    GameObject routeVis = ConvoyVisualGenerator.CreateRouteLineVisual(
                        worldPoints, route.level, route.isAlliedRoute);
                    if (routeVis != null)
                    {
                        routeVis.transform.SetParent(convoysContainer.transform);
                        routeVisuals.Add(routeVis);
                    }
                }
            }
            
            Debug.Log($"[CampaignMap3D] Rendered {convoyVisuals.Count} convoys, {routeVisuals.Count} route lines");
        }
        
        /// <summary>
        /// Smoothly animate convoy positions between provinces.
        /// Called each frame from Update().
        /// </summary>
        private void UpdateConvoyAnimation()
        {
            convoyAnimTimer += Time.deltaTime;
            
            var allConvoys = LogisticsConvoySystem.GetAllActiveConvoys();
            int i = 0;
            foreach (var convoy in allConvoys)
            {
                if (i >= convoyVisuals.Count) break;
                GameObject visual = convoyVisuals[i];
                if (visual == null) { i++; continue; }
                
                // Smooth lerp progress (simulate movement between turns)
                convoy.progressInStep = Mathf.PingPong(convoyAnimTimer * 0.15f, 0.3f);
                
                Vector3 targetPos = GetConvoyWorldPosition(convoy);
                visual.transform.position = Vector3.Lerp(visual.transform.position, targetPos, Time.deltaTime * 2f);
                
                // Gentle bobbing
                float bob = Mathf.Sin(convoyAnimTimer * 2f + i * 1.3f) * 1.5f;
                visual.transform.position += new Vector3(0, bob, 0);
                
                i++;
            }
        }
        
        private Vector3 GetConvoyWorldPosition(LogisticsConvoy convoy)
        {
            var provinces = campaignManager.Provinces;
            string currentProv = convoy.currentProvinceId;
            string nextProv = convoy.nextProvinceId;
            
            if (!provinces.ContainsKey(currentProv)) return Vector3.zero;
            
            Vector3 currentPos = MapToWorldPosition(provinces[currentProv].mapPosition);
            
            if (currentProv != nextProv && provinces.ContainsKey(nextProv))
            {
                Vector3 nextPos = MapToWorldPosition(provinces[nextProv].mapPosition);
                currentPos = Vector3.Lerp(currentPos, nextPos, convoy.progressInStep);
            }
            
            currentPos.y = GetTerrainHeight(currentPos.x, currentPos.z) + 8f; // Above terrain
            return currentPos;
        }
        
        // ==================== ARMIES ====================
        private void InitializeArmies()
        {
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith("Army_"))
                    Destroy(child.gameObject);
            }
            armyMarkers.Clear();
            
            foreach (var kvp in campaignManager.Armies)
                CreateArmyMarker(kvp.Value);
        }
        
        public void CreateArmyMarker(ArmyData army)
        {
            // Guard: don't create duplicate markers
            if (armyMarkers.ContainsKey(army.armyId))
            {
                Debug.Log($"[CampaignMap3D] Marker already exists for '{army.armyName}', skipping");
                return;
            }
            
            if (!campaignManager.Provinces.ContainsKey(army.currentProvinceId))
            {
                Debug.LogError($"[CampaignMap3D] Province '{army.currentProvinceId}' NOT FOUND for army '{army.armyName}'");
                return;
            }
            
            ProvinceData prov = campaignManager.Provinces[army.currentProvinceId];
            Debug.Log($"[CampaignMap3D] Creating marker: army='{army.armyName}', province='{prov.provinceName}', mapPos={prov.mapPosition}");
            
            Vector3 worldPos = MapToWorldPosition(prov.mapPosition);
            worldPos.y = GetTerrainHeight(worldPos.x, worldPos.z) + 5f;
            worldPos.x += 150f; // Offset from city — far enough to not overlap city collider
            
            Debug.Log($"[CampaignMap3D] Marker world position: {worldPos}");
            
            GameObject armyGO = new GameObject($"Army_{army.armyName}");
            armyGO.transform.SetParent(armiesContainer.transform);
            armyGO.transform.position = worldPos;
            
            ArmyMapMarker marker = armyGO.AddComponent<ArmyMapMarker>();
            marker.Initialize(army, this);
            marker.OnArmySelected += OnArmyMarkerSelected;
            marker.OnArmyHoverStart += OnArmyMarkerHover;
            marker.OnArmyHoverEnd += OnArmyMarkerHoverEnd;
            armyMarkers[army.armyId] = marker;
            
            Debug.Log($"[CampaignMap3D] ✅ Marker CREATED for '{army.armyName}' at {worldPos}, total markers: {armyMarkers.Count}");
        }
        
        // ==================== ARMY INTERACTIONS ====================
        private void OnArmyMarkerSelected(ArmyData army)
        {
            // Deselect previous
            if (selectedArmyMarker != null) selectedArmyMarker.SetSelected(false);
            if (armyMarkers.TryGetValue(army.armyId, out var m)) { selectedArmyMarker = m; m.SetSelected(true); }
            OnArmySelected?.Invoke(army);
            // NOTE: OnArmyClicked (opens panel) is NOT fired here — only on second click
        }
        
        private void OnArmyMarkerHover(ArmyData army) { } // Hover does nothing
        private void OnArmyMarkerHoverEnd() { }
        
        public void ClearArmySelection()
        {
            if (selectedArmyMarker != null) { selectedArmyMarker.SetSelected(false); selectedArmyMarker = null; }
            ClearMovementRange();
        }
        
        private void ShowMovementRange(ArmyData army)
        {
            ClearMovementRange();
            if (!campaignManager.Provinces.ContainsKey(army.currentProvinceId)) return;
            ProvinceData cur = campaignManager.Provinces[army.currentProvinceId];
            Material indicatorMat = GetPooledMaterial("move_ind", "Universal Render Pipeline/Simple Lit", Color.white);
            foreach (string nid in cur.neighborIds)
            {
                if (!campaignManager.Provinces.ContainsKey(nid)) continue;
                ProvinceData nb = campaignManager.Provinces[nid];
                Vector3 pos = MapToWorldPosition(nb.mapPosition);
                pos.y = GetTerrainHeight(pos.x, pos.z) + 0.5f;
                Color c = nb.owner == army.faction ? Color.green : Color.red;
                GameObject ind = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ind.name = "MoveInd"; ind.transform.SetParent(transform);
                ind.transform.position = pos; ind.transform.localScale = new Vector3(4f, 0.5f, 4f);
                ind.GetComponent<Renderer>().material.color = new Color(c.r, c.g, c.b, 0.5f);
                Destroy(ind.GetComponent<Collider>());
                movementPathVisuals.Add(ind);
            }
        }
        
        private void ClearMovementRange()
        {
            foreach (var v in movementPathVisuals) if (v != null) Destroy(v);
            movementPathVisuals.Clear();
        }
        
        private void UpdateArmyPositions()
        {
            if (campaignManager == null) return;
            
            HashSet<string> activeArmyIds = new HashSet<string>();
            
            foreach (var kvp in campaignManager.Armies)
            {
                ArmyData army = kvp.Value;
                if (army == null) continue;
                activeArmyIds.Add(army.armyId);
                
                if (armyMarkers.TryGetValue(army.armyId, out var marker))
                {
                    Vector3 targetPos;
                    bool isMarching = false;
                    Vector3 destPos = Vector3.zero;
                    
                    if (army.isMoving && army.targetWorldPosition != Vector3.zero)
                    {
                        isMarching = true;
                        destPos = army.targetWorldPosition;
                        
                        // Continuous movement at fixed speed (units per second)
                        // Apply supply penalty: strained=-1, critical=-2, cutoff=-3
                        float baseSpeed = 30f;
                        SupplyStatus supplyStatus = SupplyLineSystem.GetArmySupplyStatus(army.armyId);
                        float supplyPenalty = SupplyLineSystem.GetMovementModifier(supplyStatus);
                        float moveSpeed = Mathf.Max(10f, baseSpeed + supplyPenalty * 5f);
                        Vector3 currentPos = marker.transform.position;
                        targetPos = Vector3.MoveTowards(currentPos, destPos, Time.deltaTime * moveSpeed);
                        // Keep army at terrain height
                        targetPos.y = GetTerrainHeight(targetPos.x, targetPos.z) + 5f;
                        
                        // Check if arrived
                        float distRemaining = Vector3.Distance(new Vector3(targetPos.x, 0, targetPos.z), new Vector3(destPos.x, 0, destPos.z));
                        if (distRemaining < 1f)
                        {
                            targetPos = destPos;
                            targetPos.y = GetTerrainHeight(targetPos.x, targetPos.z) + 5f;
                            army.isMoving = false;
                            army.marchDaysRemaining = 0;
                            army.marchDaysTotal = 0;
                            army.targetWorldPosition = Vector3.zero;
                            smoothMarchProgress.Remove(army.armyId);
                            
                            // Update currentProvinceId to nearest province
                            Vector2 mp = WorldToMapPosition(destPos);
                            string nearestProv = FindNearestProvince(mp);
                            if (!string.IsNullOrEmpty(nearestProv))
                            {
                                army.currentProvinceId = nearestProv;
                                Debug.Log($"[Movement] {army.armyName} arrived at nearest province: {nearestProv}");
                            }
                            
                            // === COMBAT CHECK ON ARRIVAL (proximity-based) ===
                            
                            // 1) Check for enemy army nearby
                            ArmyData enemyArmy = FindEnemyArmyNearPosition(destPos, army);
                            if (enemyArmy != null)
                            {
                                string provId = army.currentProvinceId ?? nearestProv;
                                ProvinceData battleProv = null;
                                if (!string.IsNullOrEmpty(provId))
                                    campaignManager.Provinces.TryGetValue(provId, out battleProv);
                                    
                                Debug.Log($"[Combat] ⚔ {army.armyName} engages {enemyArmy.armyName}!");
                                OnBattleTriggered?.Invoke(army, enemyArmy, battleProv);
                            }
                            else
                            {
                                // 2) Check for enemy city nearby (siege)
                                CityMapMarker nearestCity = FindNearbyCityMarker(destPos, 300f);
                                if (nearestCity != null && nearestCity.ProvinceData != null 
                                    && nearestCity.ProvinceData.owner != army.faction)
                                {
                                    ProvinceData siegeProv = nearestCity.ProvinceData;
                                    
                                    // Create garrison for the city
                                    int garrisonSize = 40 + (siegeProv.population / 500);
                                    ArmyData garrison = new ArmyData(
                                        $"Garnison de {siegeProv.provinceName}",
                                        $"garrison_{siegeProv.provinceId}_{Random.Range(1000,9999)}",
                                        siegeProv.owner,
                                        siegeProv.provinceId
                                    );
                                    var garrisonReg = new RegimentData(
                                        $"Garnison de {siegeProv.provinceName}",
                                        UnitType.Militia,
                                        garrisonSize
                                    );
                                    garrisonReg.experience = 5f;
                                    garrison.regiments.Add(garrisonReg);
                                    
                                    Debug.Log($"[Siege] 🏰 {army.armyName} lays siege to {siegeProv.provinceName} ({siegeProv.owner})!");
                                    OnBattleTriggered?.Invoke(army, garrison, siegeProv);
                                }
                            }
                            
                            isMarching = false;
                        }
                    }
                    else if (campaignManager.Provinces.ContainsKey(army.currentProvinceId))
                    {
                        // Stationary: stay at current position (don't snap to province center)
                        targetPos = marker.transform.position;
                        
                        // If marker hasn't been positioned yet, use province center
                        if (targetPos == Vector3.zero)
                        {
                            ProvinceData p = campaignManager.Provinces[army.currentProvinceId];
                            targetPos = MapToWorldPosition(p.mapPosition);
                            targetPos.y = GetTerrainHeight(targetPos.x, targetPos.z) + 5f;
                            targetPos.x += 150f;
                        }
                        
                        smoothMarchProgress.Remove(army.armyId);
                    }
                    else continue;
                    
                    // Apply position
                    marker.UpdatePosition(targetPos);
                    
                    // === MOVEMENT ARROW ===
                    if (isMarching)
                    {
                        UpdateMovementArrow(army.armyId, marker.transform.position, destPos, army.marchDaysRemaining);
                    }
                    else
                    {
                        RemoveMovementArrow(army.armyId);
                    }
                }
                else
                {
                    Debug.Log($"[CampaignMap3D] Creating NEW marker for army '{army.armyName}' ({army.armyId}) in province {army.currentProvinceId} with {army.regiments.Count} regiments");
                    try { CreateArmyMarker(army); }
                    catch (System.Exception e) { Debug.LogError($"[CampaignMap3D] Army marker error for '{army.armyName}': {e}"); }
                }
            }
            
            // Clean up arrows for destroyed armies
            var arrowKeys = new List<string>(movementArrows.Keys);
            foreach (var key in arrowKeys)
            {
                if (!activeArmyIds.Contains(key))
                    RemoveMovementArrow(key);
            }
            
            // === SUPPLY WAGON UPDATES ===
            SupplyWagonSystem.UpdateWagons(campaignManager.Armies, armyMarkers, Time.deltaTime);
            SupplyWagonSystem.CheckCaptures(campaignManager.Armies, armyMarkers);
            UpdateWagonVisuals();
        }
        
        /// <summary>
        /// Update supply wagon visuals on map — create/move/remove wagon GameObjects.
        /// </summary>
        private void UpdateWagonVisuals()
        {
            if (wagonsContainer == null)
            {
                wagonsContainer = new GameObject("WagonsContainer");
                wagonsContainer.transform.SetParent(transform);
            }
            
            var activeWagons = SupplyWagonSystem.GetAllWagons();
            var activeWagonIds = new HashSet<string>();
            
            foreach (var wagon in activeWagons)
            {
                if (!wagon.isActive) continue;
                activeWagonIds.Add(wagon.wagonId);
                
                if (!wagonMarkers.TryGetValue(wagon.wagonId, out var markerGO) || markerGO == null)
                {
                    // Create wagon visual
                    markerGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    markerGO.name = $"Wagon_{wagon.wagonId}";
                    markerGO.transform.SetParent(wagonsContainer.transform);
                    markerGO.transform.localScale = new Vector3(25f, 15f, 40f); // Rectangular cart shape
                    
                    // Brown color for wooden wagon
                    var rend = markerGO.GetComponent<Renderer>();
                    if (rend != null)
                    {
                        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                        mat.color = new Color(0.55f, 0.35f, 0.15f); // Dark wood brown
                        rend.material = mat;
                    }
                    
                    // Add a small flag on top (faction color)
                    var flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    flag.name = "Flag";
                    flag.transform.SetParent(markerGO.transform);
                    flag.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                    flag.transform.localScale = new Vector3(0.3f, 0.6f, 0.05f);
                    Destroy(flag.GetComponent<Collider>());
                    var flagRend = flag.GetComponent<Renderer>();
                    if (flagRend != null)
                    {
                        var flagMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                        flagMat.color = GetFactionColor(wagon.owner);
                        flagRend.material = flagMat;
                    }
                    
                    wagonMarkers[wagon.wagonId] = markerGO;
                }
                
                // Update position
                Vector3 pos = wagon.currentWorldPosition;
                pos.y = GetTerrainHeight(pos.x, pos.z) + 8f;
                markerGO.transform.position = pos;
                
                // Face toward target
                if (wagon.targetWorldPosition != Vector3.zero)
                {
                    Vector3 dir = wagon.targetWorldPosition - pos;
                    dir.y = 0;
                    if (dir.sqrMagnitude > 1f)
                        markerGO.transform.rotation = Quaternion.LookRotation(dir);
                }
            }
            
            // Remove dead wagon markers
            var deadKeys = new List<string>();
            foreach (var kvp in wagonMarkers)
            {
                if (!activeWagonIds.Contains(kvp.Key))
                {
                    if (kvp.Value != null) Destroy(kvp.Value);
                    deadKeys.Add(kvp.Key);
                }
            }
            foreach (var key in deadKeys)
                wagonMarkers.Remove(key);
        }
        
        private void UpdateMovementArrow(string armyId, Vector3 from, Vector3 to, int daysRemaining)
        {
            if (!movementArrows.TryGetValue(armyId, out var arrow))
            {
                // Create line
                GameObject lineObj = new GameObject($"MoveArrow_{armyId}");
                lineObj.transform.SetParent(transform);
                LineRenderer lr = lineObj.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.startWidth = 8f;
                lr.endWidth = 3f;
                lr.startColor = new Color(1f, 0.85f, 0.2f, 0.7f);
                lr.endColor = new Color(1f, 0.5f, 0.1f, 0.9f);
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.sortingOrder = 10;
                lr.useWorldSpace = true;
                
                // Create label
                GameObject labelObj = new GameObject($"MoveLabel_{armyId}");
                labelObj.transform.SetParent(transform);
                
                // Use world-space TextMesh for 3D label
                TextMesh tm = labelObj.AddComponent<TextMesh>();
                tm.fontSize = 80;
                tm.characterSize = 2f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = new Color(1f, 0.95f, 0.7f);
                tm.fontStyle = FontStyle.Bold;
                
                arrow = (lineObj, labelObj);
                movementArrows[armyId] = arrow;
            }
            
            // Update line positions
            LineRenderer line = arrow.line.GetComponent<LineRenderer>();
            Vector3 fromLifted = from + Vector3.up * 15f;
            Vector3 toLifted = to + Vector3.up * 15f;
            line.SetPosition(0, fromLifted);
            line.SetPosition(1, toLifted);
            
            // Update label position and text
            Vector3 midPoint = (fromLifted + toLifted) * 0.5f + Vector3.up * 25f;
            arrow.label.transform.position = midPoint;
            
            // Face camera
            if (mainCamera != null)
                arrow.label.transform.rotation = Quaternion.LookRotation(arrow.label.transform.position - mainCamera.transform.position);
            
            TextMesh text = arrow.label.GetComponent<TextMesh>();
            text.text = daysRemaining <= 0 ? "Arriving" : $"⏱ {daysRemaining}j";
        }
        
        private void RemoveMovementArrow(string armyId)
        {
            if (movementArrows.TryGetValue(armyId, out var arrow))
            {
                if (arrow.line != null) Destroy(arrow.line);
                if (arrow.label != null) Destroy(arrow.label);
                movementArrows.Remove(armyId);
            }
        }
        
        // ==================== INPUT ====================
        private void HandleInput()
        {
            if (mainCamera == null || campaignManager == null) return;
            // Don't process map clicks when mouse is over UI (prevents click-through to map)
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            
            // LEFT CLICK — select army or open city panel
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                RaycastHit[] hits = Physics.RaycastAll(ray, 15000f);
                
                // Priority 1: check ALL hits for army markers
                foreach (var h in hits)
                {
                    ArmyMapMarker am = h.collider.GetComponentInParent<ArmyMapMarker>();
                    if (am != null && am.ArmyData != null)
                    {
                        // If clicking the SAME already-selected army → open panel
                        if (selectedArmyMarker == am)
                        {
                            Debug.Log($"[CampaignMap3D] Double-click army: '{am.ArmyData.armyName}' → opening panel");
                            OnArmyClicked?.Invoke(am.ArmyData);
                        }
                        else
                        {
                            // First click → just select (highlight)
                            Debug.Log($"[CampaignMap3D] Army selected: '{am.ArmyData.armyName}'");
                            OnArmyMarkerSelected(am.ArmyData);
                        }
                        return;
                    }
                }
                
                // Priority 2: check ALL hits for city markers
                foreach (var h in hits)
                {
                    CityMapMarker cm = h.collider.GetComponent<CityMapMarker>();
                    if (cm != null)
                    {
                        ClearArmySelection();
                        OnCityClicked?.Invoke(cm.ProvinceData);
                        return;
                    }
                }
                
                // Priority 3: clicked empty terrain → deselect
                ClearArmySelection();
            }
            
            // RIGHT CLICK — move/attack with selected army
            if (Input.GetMouseButtonDown(1))
            {
                if (selectedArmyMarker == null)
                {
                    Debug.Log("[Input] Right-click but no army selected");
                    return;
                }
                
                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                RaycastHit[] hits = Physics.RaycastAll(ray, 50000f);
                
                Debug.Log($"[Input] Right-click with army '{selectedArmyMarker.ArmyData?.armyName}' — {hits.Length} raycast hits");
                for (int i = 0; i < hits.Length; i++)
                    Debug.Log($"[Input]   Hit[{i}]: '{hits[i].collider.gameObject.name}' at {hits[i].point}, dist={hits[i].distance:F0}");
                
                // PRIORITY 1: Check for enemy army marker hit → attack
                foreach (var h in hits)
                {
                    ArmyMapMarker targetArmyMarker = h.collider.GetComponentInParent<ArmyMapMarker>();
                    if (targetArmyMarker != null && targetArmyMarker != selectedArmyMarker)
                    {
                        ArmyData targetArmy = targetArmyMarker.ArmyData;
                        ArmyData myArmy = selectedArmyMarker.ArmyData;
                        if (targetArmy != null && myArmy != null && targetArmy.faction != myArmy.faction)
                        {
                            Debug.Log($"[Input] Right-click on ENEMY army: {targetArmy.armyName} ({targetArmy.faction})");
                            TryMoveSelectedArmyToPosition(targetArmyMarker.transform.position);
                            return;
                        }
                    }
                }
                
                // PRIORITY 2: Check for city marker hit → move to city (siege if enemy)
                foreach (var h in hits)
                {
                    CityMapMarker cm = h.collider.GetComponent<CityMapMarker>();
                    if (cm != null)
                    {
                        Debug.Log($"[Input] Right-click on city: {cm.ProvinceData?.provinceName}");
                        TryMoveSelectedArmyToPosition(h.point);
                        return;
                    }
                }
                
                // PRIORITY 3: Terrain click → free movement
                foreach (var h in hits)
                {
                    // Skip army marker colliders
                    if (h.collider.GetComponentInParent<ArmyMapMarker>() != null) continue;
                    
                    // Free movement: use the exact hit point as target
                    Vector3 targetWorldPos = h.point;
                    Debug.Log($"[Input] Right-click terrain at world({targetWorldPos.x:F0},{targetWorldPos.y:F0},{targetWorldPos.z:F0})");
                    TryMoveSelectedArmyToPosition(targetWorldPos);
                    return;
                }
                Debug.LogWarning("[Input] Right-click: no valid terrain hit found");
            }
        }
        
        private void TryMoveSelectedArmyToPosition(Vector3 targetWorldPos)
        {
            if (selectedArmyMarker == null) { Debug.LogWarning("[TryMove] No marker selected"); return; }
            ArmyData army = selectedArmyMarker.ArmyData;
            if (army == null) { Debug.LogWarning("[TryMove] ArmyData is null on marker"); return; }
            
            if (army.faction != campaignManager.PlayerFaction)
            {
                Debug.LogWarning($"[TryMove] BLOCKED — {army.armyName} is {army.faction}, not player faction");
                return;
            }

            Vector3 currentWorldPos = selectedArmyMarker.transform.position;
            
            bool moved = campaignManager.MoveArmyToPosition(army.armyId, targetWorldPos, currentWorldPos);
            if (moved)
            {
                Debug.Log($"[TryMove] ✅ {army.armyName} moving to world position ({army.marchDaysRemaining} days)");
            }
            else
            {
                Debug.LogWarning($"[TryMove] ❌ FAILED — MoveArmyToPosition returned false");
            }
        }
        
        private ArmyData FindEnemyArmyInProvince(string pid, FactionType exclude)
        {
            foreach (var a in campaignManager.Armies.Values)
                if (a.currentProvinceId == pid && a.faction != exclude) return a;
            return null;
        }
        
        private string FindNearestProvince(Vector2 mapPos)
        {
            string best = null; float bestD = float.MaxValue;
            foreach (var kvp in campaignManager.Provinces)
            {
                float d = Vector2.Distance(mapPos, kvp.Value.mapPosition);
                if (d < bestD) { bestD = d; best = kvp.Key; }
            }
            return best;
        }
        
        /// <summary>
        /// Find the closest enemy army near a world position (within detection radius).
        /// Returns null if no enemy army found nearby.
        /// </summary>
        private ArmyData FindEnemyArmyNearPosition(Vector3 worldPos, ArmyData movingArmy)
        {
            float detectionRadius = 200f; // World units
            ArmyData closest = null;
            float closestDist = detectionRadius;
            
            foreach (var kvp in armyMarkers)
            {
                ArmyMapMarker marker = kvp.Value;
                if (marker == null || marker.ArmyData == null) continue;
                ArmyData other = marker.ArmyData;
                
                // Skip same faction
                if (other.faction == movingArmy.faction) continue;
                // Skip same army
                if (other.armyId == movingArmy.armyId) continue;
                
                float dist = Vector3.Distance(
                    new Vector3(worldPos.x, 0, worldPos.z),
                    new Vector3(marker.transform.position.x, 0, marker.transform.position.z));
                    
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = other;
                }
            }
            return closest;
        }
        
        /// <summary>
        /// Find the closest city marker near a world position (within radius).
        /// Returns null if no city found nearby.
        /// </summary>
        private CityMapMarker FindNearbyCityMarker(Vector3 worldPos, float radius)
        {
            CityMapMarker closest = null;
            float closestDist = radius;
            
            var allCities = FindObjectsByType<CityMapMarker>(FindObjectsSortMode.None);
            foreach (var city in allCities)
            {
                if (city == null) continue;
                float dist = Vector3.Distance(
                    new Vector3(worldPos.x, 0, worldPos.z),
                    new Vector3(city.transform.position.x, 0, city.transform.position.z));
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = city;
                }
            }
            return closest;
        }
        
        // ==================== POSITIONING ====================
        
        /// <summary>
        /// If a position is in water, search nearby to find the nearest land position.
        /// Uses expanding spiral search with EuropeLandMask.
        /// </summary>
        private Vector2 SnapToLand(Vector2 pos)
        {
            if (EuropeLandMask(pos.x, pos.y) > 0.3f) return pos;
            
            float step = 0.005f;
            for (int ring = 1; ring <= 60; ring++)
            {
                float r = ring * step;
                for (int dir = 0; dir < 16; dir++)
                {
                    float angle = dir * Mathf.PI * 2f / 16f;
                    Vector2 test = pos + new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
                    if (test.x >= 0f && test.x <= 1f && test.y >= 0f && test.y <= 1f)
                    {
                        if (EuropeLandMask(test.x, test.y) > 0.3f)
                            return test;
                    }
                }
            }
            
            Debug.LogWarning($"[CampaignMap3D] SnapToLand: Couldn't find land near ({pos.x:F3},{pos.y:F3})");
            return pos;
        }
        
        public Vector3 MapToWorldPosition(Vector2 normalizedPos)
        {
            // Apply Y-transform to map game coords to reference image coords
            float imgY = normalizedPos.y * Y_SCALE + Y_OFFSET;
            float x = (normalizedPos.x - 0.5f) * mapWidth;
            float z = (imgY - 0.5f) * mapHeight;
            float y = GetTerrainHeight(x, z);
            return new Vector3(x, y, z);
        }
        
        public Vector2 WorldToMapPosition(Vector3 worldPos)
        {
            float nx = (worldPos.x / mapWidth) + 0.5f;
            float imgY = (worldPos.z / mapHeight) + 0.5f;
            // Inverse Y-transform
            float ny = (imgY - Y_OFFSET) / Y_SCALE;
            return new Vector2(nx, ny);
        }
        
        public Color GetFactionColor(FactionType faction) => faction switch
        {
            // Colors matched to reference HoI4 map image
            FactionType.France      => new Color(0.85f, 0.10f, 0.10f),  // Bright red
            FactionType.Britain     => new Color(0.90f, 0.55f, 0.15f),  // Orange
            FactionType.Prussia     => new Color(0.20f, 0.20f, 0.30f),  // Dark charcoal
            FactionType.Russia      => new Color(0.50f, 0.30f, 0.15f),  // Brown
            FactionType.Austria     => new Color(0.95f, 0.85f, 0.20f),  // Bright yellow
            FactionType.Spain       => new Color(0.85f, 0.40f, 0.10f),  // Dark orange
            FactionType.Ottoman     => new Color(0.85f, 0.15f, 0.65f),  // Magenta/pink
            FactionType.Portugal    => new Color(0.15f, 0.60f, 0.15f),  // Green
            FactionType.Sweden      => new Color(0.10f, 0.90f, 0.90f),  // Cyan
            FactionType.Denmark     => new Color(0.85f, 0.20f, 0.50f),  // Pink/red
            FactionType.Poland      => new Color(0.60f, 0.20f, 0.70f),  // Purple
            FactionType.Venice      => new Color(0.30f, 0.60f, 0.80f),  // Teal blue
            FactionType.Dutch       => new Color(0.80f, 0.60f, 0.20f),  // Olive/gold
            FactionType.Bavaria     => new Color(0.40f, 0.50f, 0.85f),  // Medium blue
            FactionType.Saxony      => new Color(0.50f, 0.70f, 0.30f),  // Yellow-green
            FactionType.PapalStates => new Color(0.90f, 0.75f, 0.25f),  // Gold
            FactionType.Savoy       => new Color(0.55f, 0.30f, 0.10f),  // Dark brown
            FactionType.Switzerland => new Color(0.70f, 0.15f, 0.15f),  // Dark red
            FactionType.Genoa       => new Color(0.20f, 0.70f, 0.40f),  // Sea green
            FactionType.Tuscany     => new Color(0.55f, 0.80f, 0.20f),  // Lime green
            FactionType.Hanover     => new Color(0.65f, 0.55f, 0.35f),  // Khaki
            FactionType.Modena      => new Color(0.80f, 0.50f, 0.30f),  // Light brown
            FactionType.Parma       => new Color(0.70f, 0.40f, 0.55f),  // Mauve
            FactionType.Lorraine    => new Color(0.55f, 0.45f, 0.35f),  // Tan
            _ => new Color(0.55f, 0.52f, 0.48f)
        };
        
        public void HighlightCity(string provinceId, bool highlight)
        {
            foreach (var m in cityMarkers)
            {
                if (m.ProvinceData.provinceId == provinceId) { m.SetHighlighted(highlight); break; }
            }
        }
        
        public void FocusCameraOnCity(string provinceId)
        {
            foreach (var m in cityMarkers)
            {
                if (m.ProvinceData.provinceId == provinceId)
                {
                    Vector3 tp = m.transform.position; tp.y += 300f;
                    mainCamera.transform.position = tp;
                    mainCamera.transform.rotation = Quaternion.Euler(75f, 0f, 0f);
                    break;
                }
            }
        }
        
        private void FocusCameraOnCapital()
        {
            int factionIdx = PlayerPrefs.GetInt("SelectedFaction", 0);
            string capitalId;
            switch ((FactionType)factionIdx)
            {
                case FactionType.France:  capitalId = "paris";          break;
                case FactionType.Britain: capitalId = "london";         break;
                case FactionType.Prussia: capitalId = "brandenburg";    break;
                case FactionType.Russia:  capitalId = "moscow";         break;
                case FactionType.Austria: capitalId = "lower_austria";  break;
                case FactionType.Spain:   capitalId = "castile";        break;
                case FactionType.Ottoman: capitalId = "thrace";         break;
                default:                  capitalId = "paris";          break;
            }
            
            FocusCameraOnCity(capitalId);
            Debug.Log($"[CampaignMap3D] Camera focused on {capitalId} (faction {(FactionType)factionIdx})");
        }
        
        // ==================== EUROPE LAND MASK (kept for SnapToLand) ====================
        private float E(float px, float pz, float cx, float cz, float rx, float rz)
        {
            float dx = (px - cx) / rx, dz = (pz - cz) / rz;
            return Mathf.Clamp01((1f - dx * dx - dz * dz) / 0.025f);
        }
        
        private float ER(float px, float pz, float cx, float cz, float rx, float rz, float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            float cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);
            float lx = px - cx, lz = pz - cz;
            float rx2 = (lx * cs + lz * sn) / rx;
            float rz2 = (-lx * sn + lz * cs) / rz;
            return Mathf.Clamp01((1f - rx2 * rx2 - rz2 * rz2) / 0.025f);
        }
        
        private float L(float a, float b) => a > b ? a : b;
        
        /// <summary>Land mask for province snapping. Returns 1 for land, 0 for water.</summary>
        private float EuropeLandMask(float nx, float nz)
        {
            float land = 0f;
            
            // === CONTINENTAL EUROPE CORE ===
            land = L(land, E(nx, nz, 0.35f, 0.58f, 0.13f, 0.07f));
            land = L(land, E(nx, nz, 0.28f, 0.56f, 0.06f, 0.05f));
            land = L(land, ER(nx, nz, 0.21f, 0.59f, 0.04f, 0.015f, -10f));
            land = L(land, ER(nx, nz, 0.24f, 0.61f, 0.025f, 0.012f, 30f));
            land = L(land, E(nx, nz, 0.30f, 0.52f, 0.07f, 0.035f));
            land = L(land, E(nx, nz, 0.27f, 0.54f, 0.04f, 0.03f));
            land = L(land, E(nx, nz, 0.34f, 0.64f, 0.04f, 0.03f));
            land = L(land, E(nx, nz, 0.37f, 0.66f, 0.04f, 0.025f));
            land = L(land, E(nx, nz, 0.40f, 0.65f, 0.05f, 0.04f));
            land = L(land, E(nx, nz, 0.43f, 0.60f, 0.08f, 0.06f));
            land = L(land, E(nx, nz, 0.50f, 0.62f, 0.08f, 0.06f));
            land = L(land, E(nx, nz, 0.38f, 0.55f, 0.06f, 0.05f));
            land = L(land, E(nx, nz, 0.45f, 0.56f, 0.04f, 0.03f));
            land = L(land, E(nx, nz, 0.48f, 0.57f, 0.07f, 0.05f));
            land = L(land, E(nx, nz, 0.55f, 0.60f, 0.08f, 0.07f));
            land = L(land, E(nx, nz, 0.52f, 0.65f, 0.05f, 0.04f));
            land = L(land, E(nx, nz, 0.60f, 0.55f, 0.08f, 0.07f));
            land = L(land, E(nx, nz, 0.68f, 0.52f, 0.10f, 0.08f));
            land = L(land, E(nx, nz, 0.65f, 0.62f, 0.08f, 0.06f));
            land = L(land, E(nx, nz, 0.62f, 0.68f, 0.05f, 0.04f));
            land = L(land, E(nx, nz, 0.60f, 0.72f, 0.03f, 0.03f));
            land = L(land, E(nx, nz, 0.58f, 0.66f, 0.05f, 0.04f));
            land = L(land, ER(nx, nz, 0.44f, 0.68f, 0.03f, 0.015f, 10f));
            
            // === RUSSIA ===
            land = L(land, E(nx, nz, 0.75f, 0.58f, 0.14f, 0.14f));
            land = L(land, E(nx, nz, 0.80f, 0.62f, 0.12f, 0.12f));
            land = L(land, E(nx, nz, 0.85f, 0.55f, 0.10f, 0.15f));
            land = L(land, E(nx, nz, 0.90f, 0.60f, 0.08f, 0.18f));
            land = L(land, E(nx, nz, 0.75f, 0.68f, 0.10f, 0.10f));
            land = L(land, E(nx, nz, 0.68f, 0.74f, 0.06f, 0.06f));
            land = L(land, E(nx, nz, 0.72f, 0.48f, 0.06f, 0.04f));
            
            // === IBERIAN PENINSULA ===
            land = L(land, E(nx, nz, 0.17f, 0.40f, 0.10f, 0.08f));
            land = L(land, E(nx, nz, 0.11f, 0.42f, 0.05f, 0.07f));
            land = L(land, E(nx, nz, 0.09f, 0.40f, 0.03f, 0.04f));
            land = L(land, E(nx, nz, 0.22f, 0.38f, 0.06f, 0.05f));
            land = L(land, E(nx, nz, 0.25f, 0.40f, 0.03f, 0.04f));
            land = L(land, E(nx, nz, 0.14f, 0.36f, 0.06f, 0.03f));
            land = L(land, E(nx, nz, 0.20f, 0.44f, 0.06f, 0.04f));
            land = L(land, ER(nx, nz, 0.12f, 0.46f, 0.03f, 0.015f, 30f));
            land = L(land, E(nx, nz, 0.22f, 0.36f, 0.03f, 0.02f));
            
            // === ITALY ===
            land = L(land, E(nx, nz, 0.38f, 0.50f, 0.06f, 0.035f));
            land = L(land, ER(nx, nz, 0.34f, 0.48f, 0.03f, 0.015f, -30f));
            land = L(land, ER(nx, nz, 0.40f, 0.47f, 0.035f, 0.02f, -20f));
            land = L(land, ER(nx, nz, 0.41f, 0.44f, 0.025f, 0.025f, -18f));
            land = L(land, ER(nx, nz, 0.42f, 0.41f, 0.022f, 0.025f, -15f));
            land = L(land, ER(nx, nz, 0.44f, 0.38f, 0.020f, 0.025f, -12f));
            land = L(land, ER(nx, nz, 0.46f, 0.35f, 0.018f, 0.022f, -8f));
            land = L(land, ER(nx, nz, 0.47f, 0.32f, 0.016f, 0.020f, -5f));
            land = L(land, E(nx, nz, 0.48f, 0.30f, 0.015f, 0.015f));
            land = L(land, ER(nx, nz, 0.44f, 0.37f, 0.035f, 0.012f, 60f));
            land = L(land, E(nx, nz, 0.44f, 0.39f, 0.015f, 0.012f));
            land = L(land, ER(nx, nz, 0.44f, 0.27f, 0.035f, 0.022f, 15f));
            land = L(land, E(nx, nz, 0.42f, 0.26f, 0.02f, 0.015f));
            land = L(land, E(nx, nz, 0.35f, 0.36f, 0.018f, 0.028f));
            land = L(land, E(nx, nz, 0.34f, 0.32f, 0.020f, 0.030f));
            land = L(land, E(nx, nz, 0.37f, 0.25f, 0.008f, 0.006f));
            
            // === BRITISH ISLES ===
            land = L(land, E(nx, nz, 0.19f, 0.66f, 0.03f, 0.03f));
            land = L(land, ER(nx, nz, 0.20f, 0.65f, 0.02f, 0.01f, 40f));
            land = L(land, E(nx, nz, 0.21f, 0.68f, 0.015f, 0.015f));
            land = L(land, E(nx, nz, 0.16f, 0.68f, 0.03f, 0.035f));
            land = L(land, E(nx, nz, 0.14f, 0.66f, 0.025f, 0.025f));
            land = L(land, ER(nx, nz, 0.14f, 0.63f, 0.015f, 0.012f, -20f));
            land = L(land, E(nx, nz, 0.16f, 0.64f, 0.02f, 0.015f));
            land = L(land, E(nx, nz, 0.17f, 0.71f, 0.025f, 0.03f));
            land = L(land, E(nx, nz, 0.16f, 0.73f, 0.02f, 0.02f));
            land = L(land, ER(nx, nz, 0.16f, 0.76f, 0.022f, 0.035f, 12f));
            land = L(land, ER(nx, nz, 0.14f, 0.79f, 0.025f, 0.025f, 20f));
            land = L(land, E(nx, nz, 0.12f, 0.80f, 0.015f, 0.012f));
            land = L(land, E(nx, nz, 0.11f, 0.69f, 0.028f, 0.035f));
            land = L(land, E(nx, nz, 0.09f, 0.71f, 0.018f, 0.02f));
            land = L(land, E(nx, nz, 0.12f, 0.72f, 0.015f, 0.015f));
            
            // === SCANDINAVIA ===
            land = L(land, ER(nx, nz, 0.40f, 0.71f, 0.012f, 0.035f, -5f));
            land = L(land, E(nx, nz, 0.41f, 0.74f, 0.012f, 0.008f));
            land = L(land, E(nx, nz, 0.43f, 0.74f, 0.015f, 0.012f));
            land = L(land, ER(nx, nz, 0.44f, 0.75f, 0.02f, 0.012f, 20f));
            land = L(land, ER(nx, nz, 0.44f, 0.78f, 0.03f, 0.04f, 15f));
            land = L(land, ER(nx, nz, 0.42f, 0.82f, 0.025f, 0.04f, 18f));
            land = L(land, ER(nx, nz, 0.40f, 0.86f, 0.022f, 0.04f, 22f));
            land = L(land, ER(nx, nz, 0.38f, 0.90f, 0.018f, 0.035f, 28f));
            land = L(land, ER(nx, nz, 0.38f, 0.80f, 0.02f, 0.035f, 15f));
            land = L(land, ER(nx, nz, 0.36f, 0.84f, 0.018f, 0.035f, 20f));
            land = L(land, ER(nx, nz, 0.34f, 0.88f, 0.015f, 0.035f, 25f));
            land = L(land, ER(nx, nz, 0.33f, 0.92f, 0.012f, 0.03f, 30f));
            
            // === FINLAND ===
            land = L(land, E(nx, nz, 0.55f, 0.80f, 0.045f, 0.05f));
            land = L(land, E(nx, nz, 0.54f, 0.86f, 0.04f, 0.05f));
            land = L(land, E(nx, nz, 0.52f, 0.78f, 0.035f, 0.03f));
            land = L(land, E(nx, nz, 0.52f, 0.92f, 0.03f, 0.04f));
            land = L(land, E(nx, nz, 0.57f, 0.84f, 0.03f, 0.04f));
            
            // === BALKANS ===
            land = L(land, E(nx, nz, 0.52f, 0.50f, 0.05f, 0.05f));
            land = L(land, E(nx, nz, 0.56f, 0.48f, 0.05f, 0.04f));
            land = L(land, E(nx, nz, 0.60f, 0.47f, 0.04f, 0.03f));
            land = L(land, ER(nx, nz, 0.46f, 0.46f, 0.015f, 0.03f, -15f));
            land = L(land, E(nx, nz, 0.49f, 0.45f, 0.03f, 0.04f));
            land = L(land, E(nx, nz, 0.52f, 0.42f, 0.04f, 0.04f));
            land = L(land, ER(nx, nz, 0.55f, 0.39f, 0.03f, 0.035f, -8f));
            land = L(land, E(nx, nz, 0.48f, 0.40f, 0.02f, 0.03f));
            land = L(land, E(nx, nz, 0.50f, 0.38f, 0.02f, 0.025f));
            
            // === GREECE ===
            land = L(land, E(nx, nz, 0.52f, 0.36f, 0.03f, 0.03f));
            land = L(land, ER(nx, nz, 0.53f, 0.33f, 0.02f, 0.03f, -3f));
            land = L(land, ER(nx, nz, 0.52f, 0.29f, 0.022f, 0.025f, 5f));
            land = L(land, E(nx, nz, 0.51f, 0.27f, 0.018f, 0.015f));
            land = L(land, E(nx, nz, 0.59f, 0.18f, 0.032f, 0.012f));
            
            // === ANATOLIA ===
            land = L(land, E(nx, nz, 0.68f, 0.35f, 0.10f, 0.05f));
            land = L(land, E(nx, nz, 0.62f, 0.37f, 0.05f, 0.04f));
            land = L(land, E(nx, nz, 0.75f, 0.33f, 0.06f, 0.04f));
            land = L(land, E(nx, nz, 0.80f, 0.35f, 0.05f, 0.04f));
            land = L(land, E(nx, nz, 0.60f, 0.35f, 0.025f, 0.03f));
            
            // === CRIMEA ===
            land = L(land, ER(nx, nz, 0.72f, 0.46f, 0.035f, 0.018f, 25f));
            
            // === CAUCASUS ===
            land = L(land, ER(nx, nz, 0.85f, 0.40f, 0.08f, 0.025f, -15f));
            land = L(land, E(nx, nz, 0.82f, 0.42f, 0.04f, 0.03f));
            land = L(land, E(nx, nz, 0.88f, 0.42f, 0.03f, 0.025f));
            
            // === NORTH AFRICA ===
            land = L(land, E(nx, nz, 0.20f, 0.22f, 0.12f, 0.04f));
            land = L(land, E(nx, nz, 0.30f, 0.18f, 0.08f, 0.05f));
            land = L(land, E(nx, nz, 0.40f, 0.15f, 0.10f, 0.06f));
            land = L(land, E(nx, nz, 0.55f, 0.10f, 0.12f, 0.07f));
            land = L(land, E(nx, nz, 0.68f, 0.08f, 0.08f, 0.06f));
            land = L(land, E(nx, nz, 0.74f, 0.12f, 0.06f, 0.08f));
            
            // === MIDDLE EAST ===
            land = L(land, E(nx, nz, 0.78f, 0.22f, 0.08f, 0.08f));
            land = L(land, E(nx, nz, 0.76f, 0.16f, 0.04f, 0.08f));
            land = L(land, E(nx, nz, 0.82f, 0.16f, 0.06f, 0.10f));
            land = L(land, E(nx, nz, 0.88f, 0.28f, 0.06f, 0.10f));
            
            // === CARVE SEAS ===
            float s;
            s = ER(nx, nz, 0.20f, 0.63f, 0.055f, 0.006f, 28f);
            land = Mathf.Min(land, 1f - s * 0.98f);
            s = E(nx, nz, 0.26f, 0.70f, 0.06f, 0.06f);
            land = Mathf.Min(land, 1f - s * 0.95f);
            s = ER(nx, nz, 0.38f, 0.74f, 0.04f, 0.008f, 15f);
            land = Mathf.Min(land, 1f - s * 0.92f);
            s = E(nx, nz, 0.50f, 0.74f, 0.04f, 0.04f);
            land = Mathf.Min(land, 1f - s * 0.92f);
            s = E(nx, nz, 0.55f, 0.35f, 0.025f, 0.04f);
            land = Mathf.Min(land, 1f - s * 0.90f);
            s = E(nx, nz, 0.42f, 0.30f, 0.02f, 0.03f);
            land = Mathf.Min(land, 1f - s * 0.85f);
            s = ER(nx, nz, 0.62f, 0.36f, 0.008f, 0.03f, -10f);
            land = Mathf.Min(land, 1f - s * 0.95f);
            
            return land;
        }
    }

    public class CityMapMarker : MonoBehaviour
    {
        public static bool EditMode = false;
        
        public ProvinceData ProvinceData { get; private set; }
        public CampaignMap3D Map { get; private set; }
        public CityNameLabel LinkedLabel { get; set; }
        
        // Hover overlay
        private GameObject glowRing;
        private Renderer glowRenderer;
        private Material glowMat;
        private float glowAlpha = 0f;
        private float targetGlowAlpha = 0f;
        
        // City visual references
        private CityDevelopmentVisuals devVisuals;
        
        private bool isHighlighted;
        private bool isDragging;
        private Camera dragCamera;
        
        public void Initialize(ProvinceData province, CampaignMap3D map)
        {
            ProvinceData = province; Map = map;
            devVisuals = GetComponent<CityDevelopmentVisuals>();
            
            // Create a subtle glow ring (flat disc)
            glowRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            glowRing.name = "GlowRing";
            glowRing.transform.SetParent(transform);
            glowRing.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            glowRing.transform.localScale = new Vector3(3.0f, 0.02f, 3.0f);
            Destroy(glowRing.GetComponent<Collider>());
            
            // Transparent glow material
            glowRenderer = glowRing.GetComponent<Renderer>();
            glowMat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            glowMat.SetFloat("_Surface", 1); // Transparent
            glowMat.SetFloat("_Blend", 0);
            glowMat.SetOverrideTag("RenderType", "Transparent");
            glowMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            glowMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            glowMat.SetInt("_ZWrite", 0);
            glowMat.renderQueue = 3000;
            glowMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glowMat.color = new Color(1f, 0.9f, 0.5f, 0f); // Warm gold, starts invisible
            glowRenderer.material = glowMat;
            
            // Info cards hidden by default (shown on hover)
            if (LinkedLabel != null)
                LinkedLabel.ShowInfoCards(false);
        }
        
        private void Update()
        {
            // Smooth fade the glow ring
            glowAlpha = Mathf.Lerp(glowAlpha, targetGlowAlpha, Time.deltaTime * 8f);
            if (glowMat != null)
            {
                Color c = glowMat.color;
                c.a = glowAlpha;
                glowMat.color = c;
            }
            
            // Pulse effect when highlighted
            if (isHighlighted && glowRing != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 3f) * 0.15f;
                glowRing.transform.localScale = new Vector3(3.0f * pulse, 0.02f, 3.0f * pulse);
            }
        }
        
        public void SetHighlighted(bool h)
        {
            isHighlighted = h;
            targetGlowAlpha = h ? 0.4f : 0f;
            
            // Buildings stay visible always; only toggle info cards
            if (LinkedLabel != null)
                LinkedLabel.ShowInfoCards(h);
        }
        
        private void ShowCityVisuals()
        {
            // Show the 3D city buildings
            if (devVisuals != null && devVisuals.transform.childCount > 0)
            {
                foreach (Transform child in devVisuals.transform)
                {
                    if (child.name != "GlowRing" && child.gameObject != glowRing)
                        child.gameObject.SetActive(true);
                }
            }
            
            // Show info cards on label
            if (LinkedLabel != null)
                LinkedLabel.ShowInfoCards(true);
        }
        
        private void HideCityVisuals()
        {
            // Hide the 3D city buildings (keep glow ring)
            if (devVisuals != null)
            {
                foreach (Transform child in devVisuals.transform)
                {
                    // Don't hide the collider or the glow ring itself
                    if (child.name != "GlowRing" && child.gameObject != glowRing 
                        && child.GetComponent<Collider>() == null)
                        child.gameObject.SetActive(false);
                }
            }
            
            // Hide info cards on label
            if (LinkedLabel != null)
                LinkedLabel.ShowInfoCards(false);
        }
        
        private void OnMouseEnter() => SetHighlighted(true);
        private void OnMouseExit() { if (!isDragging) SetHighlighted(false); }
        
        private void OnMouseDown()
        {
            if (EditMode)
            {
                isDragging = true;
                dragCamera = Camera.main;
            }
            // Normal clicks are handled by CampaignMap3D.HandleInput
        }
        
        private void OnMouseDrag()
        {
            if (!isDragging || !EditMode || Map == null || dragCamera == null) return;
            
            Ray ray = dragCamera.ScreenPointToRay(Input.mousePosition);
            Plane plane = new Plane(Vector3.up, transform.position);
            if (plane.Raycast(ray, out float dist))
            {
                Vector3 worldHit = ray.GetPoint(dist);
                Vector2 newMapPos = Map.WorldToMapPosition(worldHit);
                newMapPos.x = Mathf.Clamp01(newMapPos.x);
                newMapPos.y = Mathf.Clamp01(newMapPos.y);
                Vector3 snappedWorld = Map.MapToWorldPosition(newMapPos);
                
                transform.position = snappedWorld;
                
                if (LinkedLabel != null)
                    LinkedLabel.SetPosition(snappedWorld);
            }
        }
        
        private void OnMouseUp()
        {
            if (!isDragging) return;
            isDragging = false;
            
            if (Map == null) return;
            
            Vector2 finalMapPos = Map.WorldToMapPosition(transform.position);
            finalMapPos.x = Mathf.Clamp01(finalMapPos.x);
            finalMapPos.y = Mathf.Clamp01(finalMapPos.y);
            
            ProvinceData.mapPosition = finalMapPos;
            
            if (CampaignManager.Instance != null)
            {
                CityData city = CampaignManager.Instance.GetCityForProvince(ProvinceData.provinceId);
                if (city != null)
                    city.mapPosition = finalMapPos;
            }
            
            Debug.Log($"[CityEdit] {ProvinceData.provinceName} moved to mapPos ({finalMapPos.x:F4}, {finalMapPos.y:F4})");
            CityPositionOverrides.SavePosition(ProvinceData.provinceId, finalMapPos);
        }
    }
}
