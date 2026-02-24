using Unity.Netcode;
using UnityEngine;
using NapoleonicWars.Core;
using NapoleonicWars.Data;

namespace NapoleonicWars.Network
{
    public class NetworkGameManager : NetworkBehaviour
    {
        public static NetworkGameManager Instance { get; private set; }

        [Header("Network Settings")]
        [SerializeField] private int maxPlayers = 2;

        private NetworkVariable<int> currentPlayerCount = new NetworkVariable<int>(0);
        private NetworkVariable<bool> gameStarted = new NetworkVariable<bool>(false);
        private NetworkVariable<int> player0Team = new NetworkVariable<int>(0);
        private NetworkVariable<int> player1Team = new NetworkVariable<int>(1);

        public int CurrentPlayerCount => currentPlayerCount.Value;
        public bool GameStarted => gameStarted.Value;
        public int MaxPlayers => maxPlayers;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
                currentPlayerCount.Value = NetworkManager.Singleton.ConnectedClientsIds.Count;
            }

            Debug.Log($"[Network] Spawned. IsServer={IsServer}, IsClient={IsClient}, IsHost={IsHost}");
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        private void OnClientConnected(ulong clientId)
        {
            currentPlayerCount.Value = NetworkManager.Singleton.ConnectedClientsIds.Count;
            Debug.Log($"[Network] Client {clientId} connected. Players: {currentPlayerCount.Value}/{maxPlayers}");

            if (currentPlayerCount.Value >= maxPlayers && !gameStarted.Value)
            {
                StartNetworkBattle();
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            currentPlayerCount.Value = Mathf.Max(0, currentPlayerCount.Value - 1);
            Debug.Log($"[Network] Client {clientId} disconnected. Players: {currentPlayerCount.Value}");
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestStartBattleServerRpc(ServerRpcParams rpcParams = default)
        {
            if (currentPlayerCount.Value >= 2 && !gameStarted.Value)
            {
                StartNetworkBattle();
            }
        }

        private void StartNetworkBattle()
        {
            gameStarted.Value = true;
            StartBattleClientRpc();
            Debug.Log("[Network] Battle started!");
        }

        [ClientRpc]
        private void StartBattleClientRpc()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.StartBattle();
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestEndBattleServerRpc(bool victory, ServerRpcParams rpcParams = default)
        {
            EndBattleClientRpc(victory);
        }

        [ClientRpc]
        private void EndBattleClientRpc(bool victory)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.EndBattle(victory);
        }

        public int GetTeamForClient(ulong clientId)
        {
            // Host is always team 0 (France), client is team 1 (Britain)
            if (clientId == NetworkManager.Singleton.LocalClientId && IsHost)
                return 0;
            return 1;
        }

        public int GetLocalTeam()
        {
            if (NetworkManager.Singleton == null) return 0;
            return GetTeamForClient(NetworkManager.Singleton.LocalClientId);
        }

        public bool IsLocalPlayerTeam(int teamId)
        {
            return GetLocalTeam() == teamId;
        }
    }
}
