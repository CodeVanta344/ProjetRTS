using UnityEngine;
using UnityEditor;

public class ImportMeshySoldier
{
    [MenuItem("Tools/Setup Colonial Soldier")]
    public static void Setup()
    {
        string folderPath = "Assets/Models/FrenchLineInfantry";
        string fbxPath = folderPath + "/Meshy_AI_Colonial_Soldier_in_a_0223111650_texture.fbx";
        
        AssetDatabase.Refresh();

        // 1. Create Material
        string matPath = folderPath + "/FrenchSoldierMat.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, matPath);
        }

        // 2. Assign Textures
        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(folderPath + "/Meshy_AI_Colonial_Soldier_in_a_0223111650_texture.png");
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(folderPath + "/Meshy_AI_Colonial_Soldier_in_a_0223111650_texture_normal.png");
        Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(folderPath + "/Meshy_AI_Colonial_Soldier_in_a_0223111650_texture_metallic.png");
        
        mat.SetTexture("_BaseMap", albedo);
        if (normal != null) {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
        }
        if (metallic != null) {
            mat.SetTexture("_MetallicGlossMap", metallic);
        }
        EditorUtility.SetDirty(mat);

        // 3. Create Prefab
        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (fbx == null) {
            Debug.LogError("FBX not found at " + fbxPath);
            return;
        }

        GameObject inst = PrefabUtility.InstantiatePrefab(fbx) as GameObject;
        inst.name = "French_LineInfantry_Model";
        
        // Correct scaling for meshy AI models (usually tiny or huge, standardizing scale)
        // Leaving local scale at 1, unit is typically configured via UnitModelLoader.CreateFromPrefab
        
        // Apply Material
        foreach(var renderer in inst.GetComponentsInChildren<Renderer>())
        {
            renderer.sharedMaterial = mat;
        }
        
        // Ensure Collider
        if (inst.GetComponent<Collider>() == null)
        {
            CapsuleCollider col = inst.AddComponent<CapsuleCollider>();
            col.height = 1.8f;
            col.center = new Vector3(0f, 0.9f, 0f);
            col.radius = 0.3f;
        }
        
        // Add UnitAnimator (empty animator will be added by Unity, UnitAnimator component manages state)
        if (inst.GetComponent<NapoleonicWars.Units.UnitAnimator>() == null)
        {
            inst.AddComponent<NapoleonicWars.Units.UnitAnimator>();
        }

        string finalPath = "Assets/Resources/Models/French_LineInfantry_Model.prefab";
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Models")) {
            AssetDatabase.CreateFolder("Assets/Resources", "Models");
        }

        PrefabUtility.SaveAsPrefabAsset(inst, finalPath);
        GameObject.DestroyImmediate(inst);

        Debug.Log("[Colonial Soldier] Successfully created Colonial_LineInfantry prefab in Resources/Models!");
        
        // Clean up delay trigger
        SessionState.SetBool("MeshySetupDone", true);
    }
}
