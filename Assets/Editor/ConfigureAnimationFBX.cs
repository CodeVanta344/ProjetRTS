using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Auto-configures animation-only FBX files in Resources/Models/Animations/.
/// Imports as Legacy so clips work with the Legacy Animation component used by UnitAnimator.
/// </summary>
public class ConfigureAnimationFBX : AssetPostprocessor
{
    private void OnPreprocessModel()
    {
        if (!assetPath.Contains("Models/Animations/")) return;
        if (!assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) return;

        ModelImporter importer = assetImporter as ModelImporter;
        if (importer == null) return;

        // Import as Legacy — this creates AnimationClip sub-assets that work with Animation component
        importer.animationType = ModelImporterAnimationType.Legacy;
        
        // Animation settings
        importer.importAnimation = true;
        importer.resampleCurves = true;
        importer.animationCompression = ModelImporterAnimationCompression.Optimal;
        
        // No mesh/materials needed from animation-only FBX
        importer.materialImportMode = ModelImporterMaterialImportMode.None;

        // Configure clips for looping
        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0)
        {
            TakeInfo[] takes = importer.importedTakeInfos;
            if (takes != null && takes.Length > 0)
            {
                clips = new ModelImporterClipAnimation[takes.Length];
                for (int i = 0; i < takes.Length; i++)
                {
                    clips[i] = new ModelImporterClipAnimation();
                    clips[i].takeName = takes[i].name;
                    clips[i].name = takes[i].name;
                    clips[i].firstFrame = 0;
                    clips[i].lastFrame = (float)(takes[i].stopTime * takes[i].sampleRate);
                    clips[i].loopTime = true;
                    clips[i].loopPose = true;
                    clips[i].wrapMode = WrapMode.Loop;
                }
                importer.clipAnimations = clips;
            }
        }
        else
        {
            // Set all clips to loop (walk animations should loop)
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].loopTime = true;
                clips[i].loopPose = true;
                clips[i].wrapMode = WrapMode.Loop;
            }
            importer.clipAnimations = clips;
        }

        Debug.Log($"[ConfigureAnimationFBX] Pre-processing: {Path.GetFileName(assetPath)} as Legacy");
    }

    /// <summary>
    /// Menu item to force re-import all animation FBX files.
    /// </summary>
    [MenuItem("Tools/Reimport Animation FBX")]
    public static void ReimportAnimations()
    {
        string animPath = "Assets/Resources/Models/Animations";
        if (!AssetDatabase.IsValidFolder(animPath))
        {
            Debug.LogWarning($"[ConfigureAnimationFBX] Folder not found: {animPath}");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { animPath });
        int count = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;

            // Force reimport
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            count++;
            
            // Log what clips were found
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            int clipCount = 0;
            foreach (Object asset in assets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    clipCount++;
                    Debug.Log($"  Clip: '{clip.name}' legacy={clip.legacy} length={clip.length:F2}s wrapMode={clip.wrapMode}");
                }
            }
            Debug.Log($"[ConfigureAnimationFBX] Reimported: {Path.GetFileName(path)} ({clipCount} clips)");
        }
        Debug.Log($"[ConfigureAnimationFBX] Done! Reimported {count} animation FBX files.");
    }
    
    /// <summary>
    /// Debug tool: shows what animation clips exist and can be loaded.
    /// </summary>
    [MenuItem("Tools/Debug Animation Clips")]
    public static void DebugAnimationClips()
    {
        // Check what clips exist in the walk FBX
        string[] paths = {
            "Models/Animations/conscrit_walking",
            "Models/Animations/conscrit walking",
        };
        
        foreach (string path in paths)
        {
            AnimationClip[] clips = Resources.LoadAll<AnimationClip>(path);
            if (clips != null && clips.Length > 0)
            {
                Debug.Log($"[DebugAnim] Found {clips.Length} clips at '{path}':");
                foreach (var clip in clips)
                    Debug.Log($"  - '{clip.name}' legacy={clip.legacy} length={clip.length:F2}s wrap={clip.wrapMode}");
            }
            else
            {
                Debug.Log($"[DebugAnim] No clips found at '{path}'");
            }
        }
        
        // Also check the model FBX clips
        string[] modelPaths = {
            "Models/FrenchLineInfantry_MidPoly/Meshy_AI_Colonial_Soldier_in_a_0223111618_texture",
            "Models/French_LineInfantry_Model",
        };
        foreach (string path in modelPaths)
        {
            AnimationClip[] clips = Resources.LoadAll<AnimationClip>(path);
            Debug.Log($"[DebugAnim] Model '{path}': {(clips != null ? clips.Length : 0)} clips");
            if (clips != null)
                foreach (var clip in clips)
                    Debug.Log($"  - '{clip.name}' legacy={clip.legacy}");
        }
    }
}
