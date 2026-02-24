using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace NapoleonicWars.Network
{
    public class NetworkLobbyManager : MonoBehaviour
    {
        public static NetworkLobbyManager Instance { get; private set; }

        [Header("Connection Settings")]
        [SerializeField] private string ipAddress = "127.0.0.1";
        [SerializeField] private ushort port = 7777;

        public string IpAddress { get => ipAddress; set => ipAddress = value; }
        public ushort Port { get => port; set => port = value; }

        public bool IsConnected => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        public bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
        public bool IsClient => NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void HostGame()
        {
            SetupTransport();
            NetworkManager.Singleton.StartHost();
            Debug.Log($"[Lobby] Hosting game on {ipAddress}:{port}");
        }

        public void JoinGame()
        {
            SetupTransport();
            NetworkManager.Singleton.StartClient();
            Debug.Log($"[Lobby] Joining game at {ipAddress}:{port}");
        }

        public void Disconnect()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
                Debug.Log("[Lobby] Disconnected.");
            }
        }

        private void SetupTransport()
        {
            if (NetworkManager.Singleton == null) return;

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.ConnectionData.Address = ipAddress;
                transport.ConnectionData.Port = port;
            }
        }
    }
}
