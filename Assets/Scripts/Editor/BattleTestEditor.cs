#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using NapoleonicWars.Core;

namespace NapoleonicWars.EditorTools
{
    public class BattleTestEditor : EditorWindow
    {
        private int regimentsPerSide = 4;
        private int unitsPerRegiment = 60;

        [MenuItem("Napoleonic Wars/Battle Test Setup")]
        public static void ShowWindow()
        {
            GetWindow<BattleTestEditor>("Battle Test");
        }

        private void OnGUI()
        {
            GUILayout.Label("Quick Battle Setup", EditorStyles.boldLabel);
            GUILayout.Space(10);

            regimentsPerSide = EditorGUILayout.IntSlider("Regiments Per Side", regimentsPerSide, 1, 10);
            unitsPerRegiment = EditorGUILayout.IntSlider("Units Per Regiment", unitsPerRegiment, 10, 120);

            int totalUnits = regimentsPerSide * unitsPerRegiment * 2;
            EditorGUILayout.HelpBox($"Total units on battlefield: {totalUnits}", MessageType.Info);

            GUILayout.Space(10);

            if (GUILayout.Button("Create Battle Scene Setup"))
            {
                CreateBattleSetup();
            }

            GUILayout.Space(5);

            if (GUILayout.Button("Clear Scene"))
            {
                ClearScene();
            }
        }

        private void CreateBattleSetup()
        {
            ClearScene();

            GameObject setupGO = new GameObject("BattleSceneSetup");
            BattleSceneSetup setup = setupGO.AddComponent<BattleSceneSetup>();

            EditorUtility.SetDirty(setupGO);
            Debug.Log($"Battle scene setup created. Press Play to start the battle with {regimentsPerSide} regiments of {unitsPerRegiment} units per side.");
        }

        private void ClearScene()
        {
            // Remove existing setup
            BattleSceneSetup existing = FindFirstObjectByType<BattleSceneSetup>();
            if (existing != null)
                DestroyImmediate(existing.gameObject);

            // Remove managers
            GameManager gm = FindFirstObjectByType<GameManager>();
            if (gm != null) DestroyImmediate(gm.gameObject);

            BattleManager bm = FindFirstObjectByType<BattleManager>();
            if (bm != null) DestroyImmediate(bm.gameObject);
        }
    }
}
#endif
