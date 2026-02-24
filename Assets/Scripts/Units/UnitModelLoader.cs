using UnityEngine;
using System.Collections.Generic;
using NapoleonicWars.Core;
using NapoleonicWars.Data;

namespace NapoleonicWars.Units
{
    /// <summary>
    /// Manages loading and instantiating unit models.
    /// Falls back to colored primitives if no model prefab is assigned.
    /// Place model prefabs in Resources/Models/ folder.
    /// </summary>
    public class UnitModelLoader : MonoBehaviour
    {
        public static UnitModelLoader Instance { get; private set; }

        [Header("Model Prefabs (optional — falls back to primitives)")]
        [SerializeField] private GameObject frenchInfantryPrefab;
        [SerializeField] private GameObject frenchGrenadierPrefab;
        [SerializeField] private GameObject frenchCavalryPrefab;
        [SerializeField] private GameObject frenchArtilleryPrefab;
        [SerializeField] private GameObject frenchLightInfantryPrefab;

        [SerializeField] private GameObject britishInfantryPrefab;
        [SerializeField] private GameObject britishGrenadierPrefab;
        [SerializeField] private GameObject britishCavalryPrefab;
        [SerializeField] private GameObject britishArtilleryPrefab;
        [SerializeField] private GameObject britishLightInfantryPrefab;

        [Header("Animation")]
        [SerializeField] private RuntimeAnimatorController soldierAnimatorController;

        private Dictionary<string, GameObject> prefabCache = new Dictionary<string, GameObject>();
        private Dictionary<string, RuntimeAnimatorController> controllerCache = new Dictionary<string, RuntimeAnimatorController>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildPrefabCache();
        }

        // Faction fallback: factions without custom models use another faction's models
        private static readonly Dictionary<string, string> factionModelFallback = new Dictionary<string, string>
        {
            { "Prussia", "France" },
            { "Austria", "France" },
            { "Russia", "Britain" },
            { "Spain", "France" },
            { "Ottoman", "Britain" },
        };

        private void BuildPrefabCache()
        {
            // Register manually assigned prefabs
            RegisterPrefab("France_LineInfantry", frenchInfantryPrefab);
            RegisterPrefab("France_Grenadier", frenchGrenadierPrefab);
            RegisterPrefab("France_Cavalry", frenchCavalryPrefab);
            RegisterPrefab("France_Artillery", frenchArtilleryPrefab);
            RegisterPrefab("France_LightInfantry", frenchLightInfantryPrefab);

            RegisterPrefab("Britain_LineInfantry", britishInfantryPrefab);
            RegisterPrefab("Britain_Grenadier", britishGrenadierPrefab);
            RegisterPrefab("Britain_Cavalry", britishCavalryPrefab);
            RegisterPrefab("Britain_Artillery", britishArtilleryPrefab);
            RegisterPrefab("Britain_LightInfantry", britishLightInfantryPrefab);

            // Try loading faction-specific models from Resources/Models/
            string[] factions = { "France", "Britain", "Prussia", "Russia", "Austria", "Spain", "Ottoman" };
            string[] unitTypes = { "LineInfantry", "Grenadier", "Cavalry", "Artillery", "LightInfantry" };
            int missing = 0;
            foreach (string f in factions)
                foreach (string u in unitTypes)
                {
                    if (!TryLoadFromResources($"{f}_{u}"))
                        missing++;
                }

            // Register fallback aliases: e.g. Prussia_LineInfantry -> France_LineInfantry
            foreach (var kvp in factionModelFallback)
            {
                foreach (string u in unitTypes)
                {
                    string key = $"{kvp.Key}_{u}";
                    string fallbackKey = $"{kvp.Value}_{u}";
                    if (!prefabCache.ContainsKey(key) && prefabCache.ContainsKey(fallbackKey))
                        prefabCache[key] = prefabCache[fallbackKey];
                }
            }

            Debug.Log($"[UnitModelLoader] {prefabCache.Count} model entries cached ({missing} using faction fallbacks).");
        }

        private void RegisterPrefab(string key, GameObject prefab)
        {
            if (prefab != null && !prefabCache.ContainsKey(key))
                prefabCache[key] = prefab;
        }

        /// <summary>Try to load a model from Resources/Models/. Returns true if found.</summary>
        private bool TryLoadFromResources(string key)
        {
            if (prefabCache.ContainsKey(key)) return true;

            GameObject loaded = Resources.Load<GameObject>($"Models/{key}");
            if (loaded == null)
                loaded = Resources.Load<GameObject>($"Models/{key}_Rig");
            if (loaded != null)
            {
                prefabCache[key] = loaded;
                Debug.Log($"[UnitModelLoader] Loaded model from Resources: {key}");
                return true;
            }
            return false;
        }

        public GameObject CreateUnitVisual(UnitData data)
        {
            string key = GetPrefabKey(data);

            if (key == "France_LineInfantry") 
            {
                // Load LOD models
                GameObject midPolyPrefab = Resources.Load<GameObject>(
                    "Models/FrenchLineInfantry_MidPoly/Meshy_AI_Colonial_Soldier_in_a_0223111618_texture");
                GameObject lowPolyPrefab = Resources.Load<GameObject>(
                    "Models/FrenchLineInfantry_LowPoly/Meshy_AI_Colonial_Soldier_in_a_0223111450_texture");
                GameObject highPolyPrefab = Resources.Load<GameObject>("Models/French_LineInfantry_Model");

                // Pick best available: mid > high > low
                GameObject primaryPrefab = midPolyPrefab ?? highPolyPrefab ?? lowPolyPrefab;
                GameObject distantPrefab = lowPolyPrefab ?? midPolyPrefab;

                if (primaryPrefab == null)
                {
                    Debug.LogWarning("[UnitModelLoader] No French Line Infantry model found!");
                    return CreatePrimitiveFallback(data);
                }

                Debug.Log($"[UnitModelLoader] French LOD: primary={primaryPrefab.name}, distant={(distantPrefab != null ? distantPrefab.name : "none")}");

                // --- Build shared material (one for all soldiers) ---
                string midTexBase = "Models/FrenchLineInfantry_MidPoly/Meshy_AI_Colonial_Soldier_in_a_0223111618_texture";
                string lowTexBase = "Models/FrenchLineInfantry_LowPoly/Meshy_AI_Colonial_Soldier_in_a_0223111450_texture";

                Texture2D albedo = Resources.Load<Texture2D>(midTexBase) 
                    ?? Resources.Load<Texture2D>(lowTexBase)
                    ?? Resources.Load<Texture2D>("Models/FrenchSoldierAlbedo");
                Texture2D normal = Resources.Load<Texture2D>(midTexBase + "_normal") 
                    ?? Resources.Load<Texture2D>(lowTexBase + "_normal")
                    ?? Resources.Load<Texture2D>("Models/FrenchSoldierNormal");
                Texture2D metallic = Resources.Load<Texture2D>(midTexBase + "_metallic") 
                    ?? Resources.Load<Texture2D>(lowTexBase + "_metallic")
                    ?? Resources.Load<Texture2D>("Models/FrenchSoldierMetallic");

                Material mat = new Material(NapoleonicWars.Core.URPMaterialHelper.LitShader);
                mat.enableInstancing = true;
                mat.SetColor("_BaseColor", Color.white);
                if (albedo != null) mat.SetTexture("_BaseMap", albedo);
                if (normal != null) { mat.SetTexture("_BumpMap", normal); mat.EnableKeyword("_NORMALMAP"); }
                if (metallic != null) mat.SetTexture("_MetallicGlossMap", metallic);

                // --- Build wrapper with LOD ---
                float baseScale = data.visualScaleMultiplier > 0 ? data.visualScaleMultiplier : 1f;
                GameObject wrapper = new GameObject("FrenchSoldier_Wrapper");

                // LOD0: mid-poly (close range)
                GameObject lod0 = Instantiate(primaryPrefab);
                lod0.name = "LOD0_MidPoly";
                lod0.transform.SetParent(wrapper.transform);
                lod0.transform.localScale = Vector3.one * baseScale * 100f;
                lod0.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                foreach (Renderer r in lod0.GetComponentsInChildren<Renderer>())
                    r.sharedMaterial = mat;

                if (distantPrefab != null && distantPrefab != primaryPrefab)
                {
                    // LOD1: low-poly (far range)
                    GameObject lod1 = Instantiate(distantPrefab);
                    lod1.name = "LOD1_LowPoly";
                    lod1.transform.SetParent(wrapper.transform);
                    lod1.transform.localScale = Vector3.one * baseScale * 100f;
                    lod1.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                    foreach (Renderer r in lod1.GetComponentsInChildren<Renderer>())
                        r.sharedMaterial = mat;

                    // Setup LOD Group
                    LODGroup lodGroup = wrapper.AddComponent<LODGroup>();
                    Renderer[] lod0Renderers = lod0.GetComponentsInChildren<Renderer>();
                    Renderer[] lod1Renderers = lod1.GetComponentsInChildren<Renderer>();
                    LOD[] lods = new LOD[3];
                    lods[0] = new LOD(0.15f, lod0Renderers);  // Mid-poly: 100%→15% screen height
                    lods[1] = new LOD(0.02f, lod1Renderers);  // Low-poly: 15%→2% screen height
                    lods[2] = new LOD(0f, new Renderer[0]);    // Culled below 2%
                    lodGroup.SetLODs(lods);
                    lodGroup.RecalculateBounds();

                    Debug.Log("[UnitModelLoader] French Infantry: LOD system active (mid + low poly)");
                }
                else
                {
                    Debug.Log("[UnitModelLoader] French Infantry: single LOD (no distant mesh)");
                }

                CapsuleCollider col = wrapper.AddComponent<CapsuleCollider>();
                col.height = 1.8f;
                col.center = new Vector3(0f, 0.9f, 0f);
                col.radius = 0.3f;

                // --- Load animation clips from external FBX files ---
                Animation anim = lod0.GetComponentInChildren<Animation>();
                if (anim == null)
                    anim = lod0.AddComponent<Animation>();
                
                // Try loading clips from the model FBX
                AnimationClip[] modelClips = Resources.LoadAll<AnimationClip>(
                    "Models/FrenchLineInfantry_MidPoly/Meshy_AI_Colonial_Soldier_in_a_0223111618_texture");
                Debug.Log($"[UnitModelLoader] Model clips (MidPoly): {(modelClips != null ? modelClips.Length : 0)}");
                
                if (modelClips == null || modelClips.Length == 0)
                {
                    modelClips = Resources.LoadAll<AnimationClip>("Models/French_LineInfantry_Model");
                    Debug.Log($"[UnitModelLoader] Model clips (Prefab): {(modelClips != null ? modelClips.Length : 0)}");
                }
                
                // Try multiple paths for walk animation
                string[] walkPaths = {
                    "Models/Animations/conscrit_walking",
                    "Models/Animations/conscrit walking",
                };
                AnimationClip[] walkClips = null;
                foreach (string wPath in walkPaths)
                {
                    walkClips = Resources.LoadAll<AnimationClip>(wPath);
                    if (walkClips != null && walkClips.Length > 0)
                    {
                        Debug.Log($"[UnitModelLoader] Walk clips found at '{wPath}': {walkClips.Length}");
                        break;
                    }
                }
                if (walkClips == null || walkClips.Length == 0)
                    Debug.LogWarning("[UnitModelLoader] NO walk animation clips found! Run Tools > Reimport Animation FBX");
                
                // Combine all clips
                var allClipsList = new List<AnimationClip>();
                if (modelClips != null) allClipsList.AddRange(modelClips);
                if (walkClips != null) allClipsList.AddRange(walkClips);
                
                Debug.Log($"[UnitModelLoader] Total clips to add: {allClipsList.Count}");

                int addedClips = 0;
                foreach (AnimationClip clip in allClipsList)
                {
                    if (clip == null || clip.name.StartsWith("__preview__")) continue;
                    
                    // Extract simple name: "ArmatureName|Walk" -> "walk"
                    string simpleName = clip.name;
                    int pipeIdx = simpleName.LastIndexOf('|');
                    if (pipeIdx >= 0 && pipeIdx < simpleName.Length - 1)
                        simpleName = simpleName.Substring(pipeIdx + 1);
                    simpleName = simpleName.ToLower().Replace(" ", "_");
                    
                    // Map various naming conventions to the names UnitAnimator expects
                    // Mixamo exports use 'Armature|mixamo.com' as clip name
                    if (simpleName.Contains("walk") || simpleName.Contains("march") || simpleName.Contains("conscrit") || simpleName.Contains("mixamo"))
                        simpleName = "walk";
                    else if (simpleName.Contains("idle") || simpleName.Contains("stand"))
                        simpleName = "idle";
                    else if (simpleName.Contains("run") || simpleName.Contains("charge"))
                        simpleName = "charge";
                    else if (simpleName.Contains("death") || simpleName.Contains("die"))
                        simpleName = "death";
                    else if (simpleName.Contains("attack") || simpleName.Contains("melee"))
                        simpleName = "attack_melee";
                    else if (simpleName.Contains("fire") || simpleName.Contains("shoot"))
                        simpleName = "standing_fire";
                    else if (simpleName.Contains("reload"))
                        simpleName = "reload";
                    else if (simpleName.Contains("flee"))
                        simpleName = "flee";
                    
                    clip.legacy = true;
                    
                    // Set loop mode for walk/idle/charge/flee
                    if (simpleName == "walk" || simpleName == "idle" || simpleName == "charge" || simpleName == "flee")
                        clip.wrapMode = WrapMode.Loop;
                    
                    anim.AddClip(clip, simpleName);
                    addedClips++;
                    Debug.Log($"[UnitModelLoader] Added clip '{clip.name}' -> '{simpleName}' (legacy={clip.legacy}, length={clip.length:F2}s)");
                }

                // Set default clip to idle or walk
                if (anim.GetClip("idle") != null)
                {
                    anim.clip = anim.GetClip("idle");
                    anim.Play("idle");
                }
                else if (anim.GetClip("walk") != null)
                {
                    anim.clip = anim.GetClip("walk");
                    anim.Play("walk");
                }
                
                Debug.Log($"[UnitModelLoader] French Infantry FINAL: {addedClips} clips loaded, walk={anim.GetClip("walk") != null}, idle={anim.GetClip("idle") != null}");

                wrapper.AddComponent<UnitAnimator>();
                return wrapper;
            }

            // Try to use a 3D model prefab
            if (prefabCache.TryGetValue(key, out GameObject prefab))
            {
                return CreateFromPrefab(prefab, data);
            }

            // Fallback: create a colored primitive
            return CreatePrimitiveFallback(data);
        }

        private string GetPrefabKey(UnitData data)
        {
            string faction = data.faction.ToString();
            string unitType = data.unitType.ToString();
            string key = $"{faction}_{unitType}";
            
            // If this exact key exists in cache, use it
            if (prefabCache.ContainsKey(key)) return key;
            
            // Try fallback faction
            if (factionModelFallback.TryGetValue(faction, out string fallbackFaction))
            {
                string fallbackKey = $"{fallbackFaction}_{unitType}";
                if (prefabCache.ContainsKey(fallbackKey)) return fallbackKey;
            }
            
            // Final fallback: France
            return $"France_{unitType}";
        }

        private RuntimeAnimatorController GetAnimatorController(UnitData data)
        {
            // Use manually assigned controller if set in Inspector
            if (soldierAnimatorController != null)
                return soldierAnimatorController;

            // Auto-load from Resources/Animators/ per unit type
            string key = GetPrefabKey(data);
            if (controllerCache.TryGetValue(key, out RuntimeAnimatorController cached))
                return cached;

            // Try exact match first, then fallback patterns
            RuntimeAnimatorController ctrl = Resources.Load<RuntimeAnimatorController>($"Animators/{key}");
            if (ctrl == null)
                ctrl = Resources.Load<RuntimeAnimatorController>($"Animators/{key}_Rig");

            // Cache even if null to avoid repeated lookups
            controllerCache[key] = ctrl;

            if (ctrl != null)
                Debug.Log($"[UnitModelLoader] Loaded animator: {key}");
            else
                Debug.LogWarning($"[UnitModelLoader] No animator controller found for: {key}");

            return ctrl;
        }

        private GameObject CreateFromPrefab(GameObject prefab, UnitData data)
        {
            GameObject go = Instantiate(prefab);
            go.transform.localScale = Vector3.one * (data.visualScaleMultiplier > 0 ? data.visualScaleMultiplier : 1f);

            string key = GetPrefabKey(data);

            // Remove any existing Animator — we use Legacy Animation instead
            Animator existingAnimator = go.GetComponentInChildren<Animator>();
            if (existingAnimator != null)
                Destroy(existingAnimator);

            // Load animation clips directly from FBX sub-assets
            AnimationClip[] allClips = Resources.LoadAll<AnimationClip>($"Models/{key}");
            if (allClips == null || allClips.Length == 0)
                allClips = Resources.LoadAll<AnimationClip>($"Models/{key}_Rig");

            if (allClips != null && allClips.Length > 0)
            {
                // Use Legacy Animation component — no controller needed
                Animation anim = go.GetComponent<Animation>();
                if (anim == null)
                    anim = go.AddComponent<Animation>();

                foreach (AnimationClip clip in allClips)
                {
                    if (clip == null || clip.name.StartsWith("__preview__")) continue;

                    // Extract simple name: "France_LineInfantry_Rig|Standing_Fire" -> "standing_fire"
                    string simpleName = clip.name;
                    int pipeIdx = simpleName.LastIndexOf('|');
                    if (pipeIdx >= 0 && pipeIdx < simpleName.Length - 1)
                        simpleName = simpleName.Substring(pipeIdx + 1);
                    simpleName = simpleName.ToLower();

                    // Mark as legacy so Animation component can play it
                    clip.legacy = true;
                    anim.AddClip(clip, simpleName);
                }

                // Set default clip to idle
                if (anim.GetClip("idle") != null)
                {
                    anim.clip = anim.GetClip("idle");
                    anim.Play("idle");
                }

                Debug.Log($"[UnitModelLoader] {key}: Legacy Animation setup with {allClips.Length} clips");
            }
            else
            {
                Debug.LogWarning($"[UnitModelLoader] {key}: No animation clips found in FBX");
            }

            // Add UnitAnimator for state-driven animation
            if (go.GetComponent<UnitAnimator>() == null && go.GetComponentInChildren<UnitAnimator>() == null)
                go.AddComponent<UnitAnimator>();

            // Ensure there's a collider for selection
            if (go.GetComponent<Collider>() == null && go.GetComponentInChildren<Collider>() == null)
            {
                CapsuleCollider col = go.AddComponent<CapsuleCollider>();
                col.height = 1.8f;
                col.center = new Vector3(0f, 0.9f, 0f);
                col.radius = 0.3f;
            }

            return go;
        }

        /// <summary>
        /// Creates a cannon visual with barrel, carriage and wheels.
        /// Uses procedural primitives (no external models required).
        /// </summary>
        private GameObject CreateCannonVisual(UnitData data)
        {
            float scale = data.visualScaleMultiplier;
            Color color = data.factionColor;
            
            // Create cannon from primitives
            Debug.Log("[UnitModelLoader] Creating procedural cannon");

            // Root object for the cannon
            GameObject cannonObj = new GameObject("Cannon");

            // 1. Barrel (long cylinder)
            GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = "Barrel";
            barrel.transform.SetParent(cannonObj.transform);
            barrel.transform.localPosition = new Vector3(0f, 0.4f * scale, 0.3f * scale);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            barrel.transform.localScale = new Vector3(0.15f, 0.6f, 0.15f) * scale;
            URPMaterialHelper.FixPrimitiveMaterial(barrel, new Color(0.2f, 0.2f, 0.2f)); // Dark metal

            // 2. Carriage (wooden base)
            GameObject carriage = GameObject.CreatePrimitive(PrimitiveType.Cube);
            carriage.name = "Carriage";
            carriage.transform.SetParent(cannonObj.transform);
            carriage.transform.localPosition = new Vector3(0f, 0.15f * scale, 0f);
            carriage.transform.localScale = new Vector3(0.4f, 0.15f, 0.8f) * scale;
            URPMaterialHelper.FixPrimitiveMaterial(carriage, new Color(0.4f, 0.25f, 0.1f)); // Wood color

            // 3. Left Wheel
            GameObject wheelL = CreateWheel(scale, color);
            wheelL.name = "WheelL";
            wheelL.transform.SetParent(cannonObj.transform);
            wheelL.transform.localPosition = new Vector3(-0.35f * scale, 0.25f * scale, -0.2f * scale);

            // 4. Right Wheel
            GameObject wheelR = CreateWheel(scale, color);
            wheelR.name = "WheelR";
            wheelR.transform.SetParent(cannonObj.transform);
            wheelR.transform.localPosition = new Vector3(0.35f * scale, 0.25f * scale, -0.2f * scale);

            // 5. Rear support legs
            GameObject legL = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            legL.name = "LegL";
            legL.transform.SetParent(cannonObj.transform);
            legL.transform.localPosition = new Vector3(-0.2f * scale, 0.1f * scale, -0.5f * scale);
            legL.transform.localRotation = Quaternion.Euler(30f, 0f, -15f);
            legL.transform.localScale = new Vector3(0.05f, 0.3f, 0.05f) * scale;
            URPMaterialHelper.FixPrimitiveMaterial(legL, new Color(0.3f, 0.2f, 0.1f));

            GameObject legR = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            legR.name = "LegR";
            legR.transform.SetParent(cannonObj.transform);
            legR.transform.localPosition = new Vector3(0.2f * scale, 0.1f * scale, -0.5f * scale);
            legR.transform.localRotation = Quaternion.Euler(30f, 0f, 15f);
            legR.transform.localScale = new Vector3(0.05f, 0.3f, 0.05f) * scale;
            URPMaterialHelper.FixPrimitiveMaterial(legR, new Color(0.3f, 0.2f, 0.1f));

            // Add collider for selection
            BoxCollider fallbackCol = cannonObj.AddComponent<BoxCollider>();
            fallbackCol.size = new Vector3(0.8f, 0.8f, 1.2f) * scale;
            fallbackCol.center = new Vector3(0f, 0.3f * scale, 0f);

            return cannonObj;
        }

        private GameObject CreatePrimitiveFallback(UnitData data)
        {
            PrimitiveType shape;
            Vector3 scale;

            switch (data.unitType)
            {
                case UnitType.Cavalry:
                case UnitType.Hussar:
                case UnitType.Lancer:
                    shape = PrimitiveType.Cube;
                    scale = new Vector3(0.4f, 0.7f, 0.8f) * data.visualScaleMultiplier;
                    break;
                case UnitType.Artillery:
                    return CreateCannonVisual(data);
                case UnitType.Grenadier:
                    shape = PrimitiveType.Capsule;
                    scale = new Vector3(0.55f, 0.95f, 0.55f) * data.visualScaleMultiplier;
                    break;
                case UnitType.LightInfantry:
                    shape = PrimitiveType.Capsule;
                    scale = new Vector3(0.4f, 0.7f, 0.4f) * data.visualScaleMultiplier;
                    break;
                default:
                    shape = PrimitiveType.Capsule;
                    scale = new Vector3(0.5f, 0.8f, 0.5f) * data.visualScaleMultiplier;
                    break;
            }

            if (data.visualShape != UnitVisualShape.Capsule)
            {
                switch (data.visualShape)
                {
                    case UnitVisualShape.Cube: shape = PrimitiveType.Cube; break;
                    case UnitVisualShape.Cylinder: shape = PrimitiveType.Cylinder; break;
                    case UnitVisualShape.Sphere: shape = PrimitiveType.Sphere; break;
                }
            }

            GameObject go = GameObject.CreatePrimitive(shape);
            go.transform.localScale = scale;
            URPMaterialHelper.FixPrimitiveMaterial(go, data.factionColor);

            return go;
        }

        /// <summary>
        /// Creates a wheel for the cannon
        /// </summary>
        private GameObject CreateWheel(float scale, Color color)
        {
            GameObject wheel = new GameObject("Wheel");

            // Rim (torus-like using cylinder)
            GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.name = "Rim";
            rim.transform.SetParent(wheel.transform);
            rim.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            rim.transform.localScale = new Vector3(0.1f, 0.05f, 0.5f) * scale;
            URPMaterialHelper.FixPrimitiveMaterial(rim, color);

            // Hub (center)
            GameObject hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hub.name = "Hub";
            hub.transform.SetParent(wheel.transform);
            hub.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            hub.transform.localScale = new Vector3(0.15f, 0.06f, 0.15f) * scale;
            URPMaterialHelper.FixPrimitiveMaterial(hub, new Color(0.3f, 0.2f, 0.1f));

            // Spokes (4 thin cylinders)
            for (int i = 0; i < 4; i++)
            {
                GameObject spoke = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                spoke.name = $"Spoke{i}";
                spoke.transform.SetParent(wheel.transform);
                float angle = i * 45f;
                spoke.transform.localRotation = Quaternion.Euler(0f, 0f, 90f + angle);
                spoke.transform.localPosition = Vector3.zero;
                spoke.transform.localScale = new Vector3(0.03f, 0.2f, 0.03f) * scale;
                URPMaterialHelper.FixPrimitiveMaterial(spoke, new Color(0.3f, 0.2f, 0.1f));
            }

            return wheel;
        }
    }
}
