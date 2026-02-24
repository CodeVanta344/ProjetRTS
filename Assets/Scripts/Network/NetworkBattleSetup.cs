using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using NapoleonicWars.Core;

namespace NapoleonicWars.Network
{
    public class NetworkBattleSetup : MonoBehaviour
    {
        private void Awake()
        {
            // Create NetworkManager if not present
            if (NetworkManager.Singleton == null)
            {
                GameObject nmGO = new GameObject("NetworkManager");
                NetworkManager nm = nmGO.AddComponent<NetworkManager>();
                UnityTransport transport = nmGO.AddComponent<UnityTransport>();
                nm.NetworkConfig = new NetworkConfig();
                nm.NetworkConfig.NetworkTransport = transport;
            }

            // NetworkGameManager (spawned as NetworkObject)
            GameObject ngmGO = new GameObject("NetworkGameManager");
            ngmGO.AddComponent<NetworkGameManager>();
            NetworkObject netObj = ngmGO.AddComponent<NetworkObject>();

            // NetworkRegimentCommands
            GameObject nrcGO = new GameObject("NetworkRegimentCommands");
            nrcGO.AddComponent<NetworkRegimentCommands>();
            nrcGO.AddComponent<NetworkObject>();

            // Lobby Manager
            if (NetworkLobbyManager.Instance == null)
            {
                GameObject lobbyGO = new GameObject("NetworkLobbyManager");
                lobbyGO.AddComponent<NetworkLobbyManager>();
            }

            // Lobby UI
            GameObject lobbyUIGO = new GameObject("LobbyUI");
            lobbyUIGO.AddComponent<NapoleonicWars.UI.LobbyUI>();

            // BattleSceneSetup handles the rest (terrain, camera, managers, etc.)
            if (GetComponent<BattleSceneSetup>() == null)
                gameObject.AddComponent<BattleSceneSetup>();
        }
    }
}
