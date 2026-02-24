using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Full auto-import pipeline for Napoleonic soldier FBX files.
/// NOW AUTO-RUNS on Editor load / script recompile via [InitializeOnLoad].
/// </summary>
[InitializeOnLoad]
public class ConfigureSoldierFBX : AssetPostprocessor
{
    // Auto-run on Editor load / script recompile
    static ConfigureSoldierFBX()
    {
        // Delay to ensure AssetDatabase is ready
        EditorApplication.delayCall += AutoConfigureIfNeeded;
    }

    private static void AutoConfigureIfNeeded()
    {
        string modelsPath = "Assets/Resources/Models";
        if (!AssetDatabase.IsValidFolder(modelsPath)) return;

        string[] fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { modelsPath });
        bool needsSetup = false;

        foreach (string guid in fbxGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
            if (path.Contains("Models/Animations/")) continue; // Skip animation-only FBX
            string name = Path.GetFileNameWithoutExtension(path);
            if (name.EndsWith("_Rig")) continue;

            // Check if controller exists with valid clips
            string ctrlPath = $"Assets/Resources/Animators/{name}.controller";
            AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if (ctrl == null || ctrl.animationClips == null || ctrl.animationClips.Length == 0)
            {
                needsSetup = true;
                break;
            }
        }

        if (needsSetup)
        {
            Debug.Log("[ConfigureSoldierFBX] AUTO-CONFIGURING — missing or empty controllers detected...");
            ConfigureAll();
        }
    }
    // Animations that should loop
    private static readonly HashSet<string> LoopingClips = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
    {
        "idle", "walk", "run", "charge", "flee",
        "standing_aim", "kneeling_aim", "present_arms"
    };

    // ================================================================
    // AUTO-IMPORT: Triggers when any FBX in Resources/Models/ is imported
    // ================================================================

    private void OnPreprocessModel()
    {
        if (!assetPath.StartsWith("Assets/Resources/Models/")) return;
        if (!assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) return;
        // Skip animation-only FBX files — they use their own clip names and don't need Generic rig
        if (assetPath.Contains("Models/Animations/")) return;

        ModelImporter importer = assetImporter as ModelImporter;
        if (importer == null) return;

        // Rig settings - Generic rig with auto-created Avatar
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.globalScale = 1f;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.CalculateMikk;
        importer.meshCompression = ModelImporterMeshCompression.Medium;
        importer.isReadable = false;
        importer.optimizeMeshPolygons = true;
        importer.optimizeMeshVertices = true;
        importer.addCollider = false;
        importer.optimizeGameObjects = false; // CRITICAL: keep bone transforms at runtime

        // Also disable bone optimization — without this, Unity strips bones
        // that have no animation curves (and we use procedural animation, not clips!)
        var so = new UnityEditor.SerializedObject(importer);
        var optimizeBones = so.FindProperty("m_OptimizeBones");
        if (optimizeBones != null)
        {
            optimizeBones.boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Animation settings
        importer.importAnimation = true;
        importer.resampleCurves = true;
        importer.animationCompression = ModelImporterAnimationCompression.Optimal;

        // CRITICAL: This ensures an Animator component is added to the imported prefab
        // Unity adds Animator when avatarSetup is CreateFromThisModel and animationType is Generic/Humanoid

        Debug.Log($"[AutoImport] Pre-processing: {Path.GetFileName(assetPath)}");
    }

    private void OnPostprocessModel(GameObject go)
    {
        if (!assetPath.StartsWith("Assets/Resources/Models/")) return;
        if (!assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) return;
        if (assetPath.Contains("Models/Animations/")) return;

        // Skip _Rig files
        string fileName = Path.GetFileNameWithoutExtension(assetPath);
        if (fileName.EndsWith("_Rig")) return;

        // Configure animation clip loop settings after import
        ModelImporter importer = assetImporter as ModelImporter;
        if (importer == null) return;

        ConfigureClips(importer);

        // Add Animator component to the prefab root
        Animator animator = go.GetComponent<Animator>();
        if (animator == null)
            animator = go.AddComponent<Animator>();

        // Load and assign the corresponding controller
        string controllerPath = $"Assets/Resources/Animators/{fileName}.controller";
        RuntimeAnimatorController ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
        if (ctrl != null)
        {
            animator.runtimeAnimatorController = ctrl;
            Debug.Log($"[AutoImport] Assigned controller to {fileName}: {ctrl.name}");
        }

        Debug.Log($"[AutoImport] Post-processed: {fileName} (Animator={animator != null}, Controller={ctrl != null})");
    }

    // ================================================================
    // MANUAL: Menu item to re-configure all FBX + create Animator Controllers
    // ================================================================

    [MenuItem("Tools/Configure Soldier FBX")]
    public static void ConfigureAll()
    {
        string modelsPath = "Assets/Resources/Models";

        if (!AssetDatabase.IsValidFolder(modelsPath))
        {
            Debug.LogWarning($"[ConfigureSoldierFBX] Folder not found: {modelsPath}");
            return;
        }

        string[] fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { modelsPath });
        int configured = 0;

        foreach (string guid in fbxGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                continue;
            // Skip animation-only FBX files
            if (path.Contains("Models/Animations/"))
                continue;

            // Skip old _Rig files
            string fileName = Path.GetFileNameWithoutExtension(path);
            if (fileName.EndsWith("_Rig"))
                continue;

            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            // Rig - Generic with auto-created Avatar (this adds Animator component)
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            // Scale
            importer.globalScale = 1f;

            // Mesh optimization
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.isReadable = false;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.optimizeGameObjects = false; // CRITICAL: keep bone transforms at runtime

            // Animation
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;

            // Clips
            ConfigureClips(importer);

            // Force reimport to apply all settings including Animator component
            importer.SaveAndReimport();
            configured++;
            Debug.Log($"[ConfigureSoldierFBX] Reimported: {Path.GetFileName(path)}");
        }

        // Create Animator Controllers for each FBX
        int controllers = CreateAnimatorControllers(modelsPath);

        // Assign controllers to FBX prefabs
        int assigned = AssignControllersToFBXPrefabs(modelsPath);

        Debug.Log($"[ConfigureSoldierFBX] Done! {configured} FBX configured, {controllers} controllers created, {assigned} prefabs updated.");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Assign Animator Controllers directly to FBX prefab assets.
    /// This ensures the prefab has the controller when instantiated at runtime.
    /// </summary>
    private static int AssignControllersToFBXPrefabs(string modelsPath)
    {
        string[] fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { modelsPath });
        int assigned = 0;

        foreach (string guid in fbxGuids)
        {
            string fbxPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!fbxPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                continue;

            string soldierName = Path.GetFileNameWithoutExtension(fbxPath);
            if (soldierName.EndsWith("_Rig"))
                continue;

            // Load the FBX as a GameObject
            GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxPrefab == null) continue;

            // Find or add Animator
            Animator animator = fbxPrefab.GetComponent<Animator>();
            if (animator == null)
                animator = fbxPrefab.GetComponentInChildren<Animator>();

            // Load the corresponding controller
            string controllerPath = $"Assets/Resources/Animators/{soldierName}.controller";
            RuntimeAnimatorController ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);

            if (ctrl == null)
            {
                Debug.LogWarning($"[ConfigureSoldierFBX] No controller found for {soldierName}");
                continue;
            }

            // We can't add components to imported FBX directly, but we can check if it has an Animator
            // The Animator is added during import if the FBX has animation
            if (animator != null)
            {
                if (animator.runtimeAnimatorController != ctrl)
                {
                    // For FBX assets, we need to use SerializedObject to modify
                    SerializedObject so = new SerializedObject(animator);
                    SerializedProperty controllerProp = so.FindProperty("m_Controller");
                    controllerProp.objectReferenceValue = ctrl;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    assigned++;
                    Debug.Log($"[ConfigureSoldierFBX] Assigned controller to {soldierName}");
                }
            }
            else
            {
                Debug.LogWarning($"[ConfigureSoldierFBX] {soldierName}: No Animator component on FBX. Animations may not work.");
            }
        }

        return assigned;
    }

    // ================================================================
    // CLIP CONFIGURATION
    // ================================================================

    private static bool ConfigureClips(ModelImporter importer)
    {
        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;

        // If still empty, build clips manually from importedTakeInfos
        if (clips == null || clips.Length == 0)
        {
            TakeInfo[] takes = importer.importedTakeInfos;
            if (takes != null && takes.Length > 0)
            {
                Debug.Log($"[ConfigureSoldierFBX] No default clips but found {takes.Length} takes in {Path.GetFileName(importer.assetPath)}");
                var clipList = new List<ModelImporterClipAnimation>();
                foreach (TakeInfo take in takes)
                {
                    ModelImporterClipAnimation newClip = new ModelImporterClipAnimation();
                    newClip.takeName = take.name;
                    newClip.name = take.name;
                    newClip.firstFrame = (float)take.bakeStartTime * take.sampleRate;
                    newClip.lastFrame = (float)take.bakeStopTime * take.sampleRate;
                    newClip.loopTime = ShouldClipLoop(take.name);
                    newClip.loopPose = newClip.loopTime;
                    if (newClip.loopTime)
                        newClip.wrapMode = WrapMode.Loop;
                    clipList.Add(newClip);
                    Debug.Log($"  Take '{take.name}': frames {newClip.firstFrame:F0}-{newClip.lastFrame:F0}");
                }
                clips = clipList.ToArray();
            }
            else
            {
                Debug.LogWarning($"[ConfigureSoldierFBX] No takes AND no clips in {Path.GetFileName(importer.assetPath)}");
                return false;
            }
        }
        else
        {
            // Configure loop settings on existing clips
            for (int i = 0; i < clips.Length; i++)
            {
                string clipName = clips[i].name;
                bool shouldLoop = ShouldClipLoop(clipName);
                clips[i].loopTime = shouldLoop;
                clips[i].loopPose = shouldLoop;
                if (shouldLoop)
                    clips[i].wrapMode = WrapMode.Loop;
            }
        }

        // ALWAYS apply clips back — this is CRITICAL for Unity to create AnimationClip sub-assets
        importer.clipAnimations = clips;
        Debug.Log($"[ConfigureSoldierFBX] Configured {clips.Length} clips for {Path.GetFileName(importer.assetPath)}");
        return true;
    }

    private static bool ShouldClipLoop(string clipName)
    {
        // Check exact match first
        if (LoopingClips.Contains(clipName))
            return true;

        // Check partial match (Blender may prefix with armature name)
        string lower = clipName.ToLower();
        foreach (string loopName in LoopingClips)
        {
            if (lower.Contains(loopName.ToLower()))
                return true;
        }

        return false;
    }

    // ================================================================
    // ANIMATOR CONTROLLER GENERATION
    // ================================================================

    [MenuItem("Tools/Create Soldier Animator Controllers")]
    public static void CreateControllersMenu()
    {
        string modelsPath = "Assets/Resources/Models";
        int count = CreateAnimatorControllers(modelsPath);
        int assigned = AssignControllersToFBXPrefabs(modelsPath);
        Debug.Log($"[ConfigureSoldierFBX] Created {count} controllers, assigned to {assigned} prefabs.");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static int CreateAnimatorControllers(string modelsPath)
    {
        string controllersPath = "Assets/Resources/Animators";
        if (!AssetDatabase.IsValidFolder(controllersPath))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            AssetDatabase.CreateFolder("Assets/Resources", "Animators");
        }

        string[] fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { modelsPath });
        int created = 0;

        foreach (string guid in fbxGuids)
        {
            string fbxPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!fbxPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                continue;

            string soldierName = Path.GetFileNameWithoutExtension(fbxPath);

            // Skip animation-only FBX files
            if (fbxPath.Contains("Models/Animations/"))
                continue;

            // Skip armature-only Rig files (no animation clips)
            if (soldierName.EndsWith("_Rig"))
                continue;

            string controllerPath = $"{controllersPath}/{soldierName}.controller";

            // Force delete existing controller to avoid stale states
            if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath) != null)
            {
                AssetDatabase.DeleteAsset(controllerPath);
                Debug.Log($"[ConfigureSoldierFBX] Deleted old controller: {soldierName}");
            }

            // Load all animation clips from the FBX
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            List<AnimationClip> clips = new List<AnimationClip>();
            foreach (Object asset in assets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    clips.Add(clip);
            }

            if (clips.Count == 0)
            {
                Debug.LogWarning($"[ConfigureSoldierFBX] No clips found in: {soldierName}");
                continue;
            }

            // Create or overwrite controller
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            // Add parameters
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsCharging", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsAttacking", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsFleeing", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsDead", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsRanged", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsAiming", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsKneeling", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsReloading", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsVolleyFire", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Fire", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Reload", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

            // Get the base layer state machine
            AnimatorStateMachine rootSM = controller.layers[0].stateMachine;

            // Add states for each clip
            Dictionary<string, AnimatorState> states = new Dictionary<string, AnimatorState>();
            AnimatorState idleState = null;

            float stateX = 250f;
            float stateY = 0f;

            foreach (AnimationClip clip in clips)
            {
                // Extract simple name (e.g., "France_LineInfantry_Rig|Standing_Fire" -> "standing_fire")
                string simpleName = clip.name;
                int pipeIndex = simpleName.LastIndexOf('|');
                if (pipeIndex >= 0 && pipeIndex < simpleName.Length - 1)
                    simpleName = simpleName.Substring(pipeIndex + 1);
                simpleName = simpleName.ToLower();

                // Create state with SIMPLE name (critical for CrossFadeInFixedTime hash matching)
                AnimatorState state = rootSM.AddState(simpleName, new Vector3(stateX, stateY, 0));
                state.motion = clip;

                states[simpleName] = state;

                if (simpleName.Contains("idle"))
                    idleState = state;

                stateY += 60f;
            }

            // Set default state to Idle
            if (idleState != null)
                rootSM.defaultState = idleState;

            // Add basic transitions (Idle -> Walk, Walk -> Run, etc.)
            // These are simplified — the UnitAnimator script handles the logic
            if (idleState != null)
            {
                AddTransitionIfExists(states, idleState, "walk", "IsMoving", true);
                AddTransitionIfExists(states, idleState, "attack_melee", "IsAttacking", true);
                AddTransitionIfExists(states, idleState, "attack_ranged", "IsAttacking", true);
                AddTransitionIfExists(states, idleState, "standing_aim", "IsAiming", true);
                AddTransitionIfExists(states, idleState, "death", "IsDead", true);
            }

            // Walk -> Idle (stop moving)
            AddTransitionBackIfExists(states, "walk", idleState, "IsMoving", false);

            // Walk -> Run (charging)
            AddTransitionIfExists(states, "walk", "run", "IsCharging", true);
            AddTransitionBackIfExists(states, "run", FindState(states, "walk"), "IsCharging", false);

            // Flee
            AddTransitionIfExists(states, "walk", "flee", "IsFleeing", true);
            if (idleState != null)
                AddTransitionBackIfExists(states, "flee", idleState, "IsFleeing", false);

            EditorUtility.SetDirty(controller);
            created++;
            Debug.Log($"[ConfigureSoldierFBX] Controller: {soldierName} ({clips.Count} clips)");
        }

        AssetDatabase.SaveAssets();
        return created;
    }

    private static AnimatorState FindState(Dictionary<string, AnimatorState> states, string name)
    {
        states.TryGetValue(name.ToLower(), out AnimatorState state);
        return state;
    }

    private static void AddTransitionIfExists(Dictionary<string, AnimatorState> states,
        AnimatorState fromState, string toName, string conditionParam, bool conditionValue)
    {
        if (fromState == null) return;
        if (!states.TryGetValue(toName.ToLower(), out AnimatorState toState)) return;

        AnimatorStateTransition t = fromState.AddTransition(toState);
        t.hasExitTime = false;
        t.duration = 0.15f;
        t.AddCondition(conditionValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
            0, conditionParam);
    }

    private static void AddTransitionIfExists(Dictionary<string, AnimatorState> states,
        string fromName, string toName, string conditionParam, bool conditionValue)
    {
        if (!states.TryGetValue(fromName.ToLower(), out AnimatorState fromState)) return;
        AddTransitionIfExists(states, fromState, toName, conditionParam, conditionValue);
    }

    private static void AddTransitionBackIfExists(Dictionary<string, AnimatorState> states,
        string fromName, AnimatorState toState, string conditionParam, bool conditionValue)
    {
        if (toState == null) return;
        if (!states.TryGetValue(fromName.ToLower(), out AnimatorState fromState)) return;

        AnimatorStateTransition t = fromState.AddTransition(toState);
        t.hasExitTime = false;
        t.duration = 0.15f;
        t.AddCondition(conditionValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
            0, conditionParam);
    }
}
