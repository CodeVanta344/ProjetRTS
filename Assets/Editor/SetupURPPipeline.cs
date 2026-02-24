using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class SetupURPPipeline
{
    [MenuItem("Napoleonic Wars/Setup URP Pipeline")]
    public static void CreateAndAssignURP()
    {
        // Ensure Settings folder exists
        if (!AssetDatabase.IsValidFolder("Assets/Settings"))
            AssetDatabase.CreateFolder("Assets", "Settings");

        string rendererPath = "Assets/Settings/NapoleonicRenderer.asset";
        string pipelinePath = "Assets/Settings/NapoleonicURP.asset";

        // Create Universal Renderer Data
        var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
        AssetDatabase.CreateAsset(rendererData, rendererPath);

        // Create Universal Render Pipeline Asset with the renderer
        var pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
        pipelineAsset.name = "NapoleonicURP";
        pipelineAsset.renderScale = 1f;
        pipelineAsset.supportsCameraOpaqueTexture = true;
        pipelineAsset.supportsCameraDepthTexture = true;

        AssetDatabase.CreateAsset(pipelineAsset, pipelinePath);
        AssetDatabase.SaveAssets();

        // Assign to Graphics Settings
        GraphicsSettings.defaultRenderPipeline = pipelineAsset;
        QualitySettings.renderPipeline = pipelineAsset;

        EditorUtility.SetDirty(pipelineAsset);
        AssetDatabase.SaveAssets();

        Debug.Log("[URP] Pipeline asset created and assigned! Re-enter Play Mode to see the fix.");
    }
}
