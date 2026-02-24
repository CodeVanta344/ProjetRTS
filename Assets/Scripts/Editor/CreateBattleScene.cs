#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using NapoleonicWars.Core;
using NapoleonicWars.Campaign;

namespace NapoleonicWars.EditorTools
{
    public static class CreateBattleScene
    {
        [MenuItem("Napoleonic Wars/Create Battle Scene")]
        public static void Execute()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject setupGO = new GameObject("BattleSceneSetup");
            setupGO.AddComponent<BattleSceneSetup>();

            string scenePath = "Assets/Scenes/Battle.unity";
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(
                System.IO.Path.Combine(Application.dataPath, "..", scenePath)));
            EditorSceneManager.SaveScene(scene, scenePath);

            Debug.Log("Battle scene created and saved at: " + scenePath);
        }

        [MenuItem("Napoleonic Wars/Create Campaign Scene")]
        public static void CreateCampaignScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject setupGO = new GameObject("CampaignSceneSetup");
            setupGO.AddComponent<CampaignSceneSetup>();

            string scenePath = "Assets/Scenes/Campaign.unity";
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(
                System.IO.Path.Combine(Application.dataPath, "..", scenePath)));
            EditorSceneManager.SaveScene(scene, scenePath);

            Debug.Log("Campaign scene created and saved at: " + scenePath);
        }

        [MenuItem("Napoleonic Wars/Create Network Battle Scene")]
        public static void CreateNetworkBattleScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject setupGO = new GameObject("NetworkBattleSetup");
            setupGO.AddComponent<NapoleonicWars.Network.NetworkBattleSetup>();

            string scenePath = "Assets/Scenes/NetworkBattle.unity";
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(
                System.IO.Path.Combine(Application.dataPath, "..", scenePath)));
            EditorSceneManager.SaveScene(scene, scenePath);

            Debug.Log("Network Battle scene created and saved at: " + scenePath);
        }

        [MenuItem("Napoleonic Wars/Create Main Menu Scene")]
        public static void CreateMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject setupGO = new GameObject("MainMenuSetup");
            setupGO.AddComponent<NapoleonicWars.UI.MainMenuSceneSetup>();

            string scenePath = "Assets/Scenes/MainMenu.unity";
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(
                System.IO.Path.Combine(Application.dataPath, "..", scenePath)));
            EditorSceneManager.SaveScene(scene, scenePath);

            Debug.Log("Main Menu scene created and saved at: " + scenePath);
        }

        [MenuItem("Napoleonic Wars/Create Lobby Scene")]
        public static void CreateLobbyScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

#if UNITY_NETCODE_GAMEOBJECTS
            // Create NetworkManager with UnityTransport
            GameObject nmGO = new GameObject("NetworkManager");
            var nm = nmGO.AddComponent<Unity.Netcode.NetworkManager>();
            var transport = nmGO.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            nm.NetworkConfig = new Unity.Netcode.NetworkConfig();
            nm.NetworkConfig.NetworkTransport = transport;
            nmGO.AddComponent<NapoleonicWars.Network.NetworkLobbyManager>();
#else
            Debug.LogWarning("[CreateBattleScene] Unity Netcode package not found. Lobby created without NetworkManager.");
#endif

            // Setup UI
            GameObject uiGO = new GameObject("LobbyUI");
            uiGO.AddComponent<NapoleonicWars.UI.LobbyUI>();

            string scenePath = "Assets/Scenes/Lobby.unity";
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(
                System.IO.Path.Combine(Application.dataPath, "..", scenePath)));
            EditorSceneManager.SaveScene(scene, scenePath);

            Debug.Log("Lobby scene created and saved at: " + scenePath);
        }

        public static void CreateFromCommandLine()
        {
            CreateMainMenuScene();
            Execute();
            CreateCampaignScene();
            CreateNetworkBattleScene();
            CreateLobbyScene();

            EditorBuildSettings.scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Lobby.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Campaign.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Battle.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/NetworkBattle.unity", true)
            };

            AssetDatabase.SaveAssets();
            Debug.Log("All scenes created from command line.");
        }
    }
}
#endif
