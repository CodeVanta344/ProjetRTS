using UnityEngine;
using UnityEditor;
using System.Text;

/// <summary>
/// Editor utility to inspect bone hierarchy of a model and diagnose animation issues.
/// Run via Tools > Inspect Model Bones to dump the hierarchy of any selected GameObject.
/// </summary>
public class InspectModelBones : EditorWindow
{
    [MenuItem("Tools/Inspect Model Bones")]
    public static void InspectBones()
    {
        // Try to find the French Infantry model
        string[] modelPaths = {
            "Assets/Resources/Models/FrenchLineInfantry_MidPoly/Meshy_AI_Colonial_Soldier_in_a_0223111618_texture.fbx",
            "Assets/Resources/Models/FrenchLineInfantry_LowPoly/Meshy_AI_Colonial_Soldier_in_a_0223111450_texture.fbx",
            "Assets/Resources/Models/French_LineInfantry_Model.fbx",
            "Assets/Models/French/INFLigne/LineSoldier.fbx",
        };

        foreach (string path in modelPaths)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) continue;

            Debug.Log($"=== MODEL: {path} ===");
            
            // Check renderers
            var smr = model.GetComponentInChildren<SkinnedMeshRenderer>();
            var mr = model.GetComponentInChildren<MeshRenderer>();
            Debug.Log($"  SkinnedMeshRenderer: {(smr != null ? "YES" : "NO")}");
            Debug.Log($"  MeshRenderer: {(mr != null ? "YES" : "NO")}");
            
            if (smr != null && smr.bones != null)
            {
                Debug.Log($"  Bone count: {smr.bones.Length}");
                Debug.Log($"  RootBone: {(smr.rootBone != null ? smr.rootBone.name : "null")}");
            }

            // Dump full hierarchy
            StringBuilder sb = new StringBuilder();
            DumpHierarchy(model.transform, "", sb);
            Debug.Log($"  Full Hierarchy:\n{sb}");
            
            // Check Animation/Animator
            var anim = model.GetComponent<Animation>();
            var animator = model.GetComponent<Animator>();
            Debug.Log($"  Animation component: {(anim != null ? "YES" : "NO")}");
            Debug.Log($"  Animator component: {(animator != null ? "YES" : "NO")}");
            
            // Check sub-asset clips
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            int clipCount = 0;
            foreach (var asset in subAssets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    clipCount++;
                    // Get the curve bindings to see what paths the clip animates
                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    Debug.Log($"  Clip: '{clip.name}' legacy={clip.legacy} bindings={bindings.Length}");
                    int shown = 0;
                    foreach (var b in bindings)
                    {
                        if (shown < 5)
                            Debug.Log($"    binding: path='{b.path}' property='{b.propertyName}' type={b.type.Name}");
                        shown++;
                    }
                    if (shown < bindings.Length)
                        Debug.Log($"    ... and {bindings.Length - shown} more bindings");
                }
            }
            Debug.Log($"  Total clips in FBX: {clipCount}");
        }
        
        // Also check animation FBX clips
        string[] animPaths = {
            "Assets/Resources/Models/Animations/idle_breathing.fbx",
            "Assets/Resources/Models/Animations/conscrit_walking.fbx",
        };
        foreach (string path in animPaths)
        {
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (subAssets == null) { Debug.Log($"=== ANIM: {path} — NOT FOUND ==="); continue; }
            
            Debug.Log($"=== ANIM: {path} ===");
            foreach (var asset in subAssets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    Debug.Log($"  Clip: '{clip.name}' legacy={clip.legacy} bindings={bindings.Length}");
                    int shown = 0;
                    foreach (var b in bindings)
                    {
                        if (shown < 10)
                            Debug.Log($"    binding: path='{b.path}' property='{b.propertyName}'");
                        shown++;
                    }
                    if (shown < bindings.Length)
                        Debug.Log($"    ... and {bindings.Length - shown} more bindings");
                }
            }
        }
    }

    private static void DumpHierarchy(Transform t, string indent, StringBuilder sb)
    {
        sb.AppendLine($"{indent}{t.name}");
        for (int i = 0; i < t.childCount; i++)
            DumpHierarchy(t.GetChild(i), indent + "  ", sb);
    }
}
