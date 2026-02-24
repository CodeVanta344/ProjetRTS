using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Network;

namespace NapoleonicWars.UI
{
    public class LobbyUI : MonoBehaviour
    {
        private Canvas canvas;

        // Connection panel
        private GameObject connectPanel;
        private InputField ipField;
        private InputField portField;
        private Text statusText;

        // Lobby panel
        private GameObject lobbyPanel;
        private Text roleText;
        private Text playerCountText;
        private Text lobbyStatusText;
        private Button btnStartCampaign;

        private void Start()
        {
            BuildUI();
        }

        private bool lastConnected = false;
        private bool stateInitialized = false;

        private void Update()
        {
            if (NetworkLobbyManager.Instance == null) return;

            bool connected = NetworkLobbyManager.Instance.IsConnected;

            // Only toggle panels when state actually changes to avoid
            // disrupting Canvas rebuild loop (InputField placeholder issue)
            if (!stateInitialized || connected != lastConnected)
            {
                connectPanel.SetActive(!connected);
                lobbyPanel.SetActive(connected);
                lastConnected = connected;
                stateInitialized = true;
            }

            if (connected)
                UpdateLobby();
        }

        private void BuildUI()
        {
            canvas = UIFactory.CreateCanvas("LobbyCanvas", 10);
            canvas.transform.SetParent(transform);

            // Full-screen dark background
            RectTransform bg = UIFactory.CreatePanel(canvas.transform, "Background", new Color(0.04f, 0.02f, 0.02f, 0.95f));
            bg.anchorMin = Vector2.zero;
            bg.anchorMax = Vector2.one;
            bg.offsetMin = Vector2.zero;
            bg.offsetMax = Vector2.zero;

            BuildConnectPanel(bg);
            BuildLobbyPanel(bg);

            lobbyPanel.SetActive(false);
        }

        private void BuildConnectPanel(Transform parent)
        {
            RectTransform panel = UIFactory.CreateOrnatePanel(parent, "ConnectPanel",
                new Color(0.07f, 0.05f, 0.04f, 0.97f));
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(440, 420);
            connectPanel = panel.gameObject;

            Transform inner = panel.Find("Inner");
            UIFactory.AddVerticalLayout(inner.gameObject, 8f, new RectOffset(25, 25, 20, 20));

            // Title
            RectTransform titleBanner = UIFactory.CreateBannerHeader(inner, "TitleBanner", "NAPOLEONIC WARS", 26);
            UIFactory.AddLayoutElement(titleBanner.gameObject, 42);

            Text subtitle = UIFactory.CreateText(inner, "Subtitle", "Multiplayer Battle", 16, TextAnchor.MiddleCenter, UIFactory.ParchmentBeige);
            subtitle.fontStyle = FontStyle.Italic;
            UIFactory.AddLayoutElement(subtitle.gameObject, 25);

            // IP row
            GameObject ipRow = new GameObject("IPRow");
            ipRow.transform.SetParent(inner, false);
            ipRow.AddComponent<RectTransform>();
            UIFactory.AddHorizontalLayout(ipRow, 8f);
            UIFactory.AddLayoutElement(ipRow, 34);

            Text ipLabel = UIFactory.CreateText(ipRow.transform, "IPLabel", "IP Address:", 14);
            UIFactory.AddLayoutElement(ipLabel.gameObject, preferredWidth: 100);

            ipField = UIFactory.CreateInputField(ipRow.transform, "IPField", "127.0.0.1", "127.0.0.1", 14);
            UIFactory.AddLayoutElement(ipField.gameObject, 30, flexibleWidth: 1);

            // Port row
            GameObject portRow = new GameObject("PortRow");
            portRow.transform.SetParent(inner, false);
            portRow.AddComponent<RectTransform>();
            UIFactory.AddHorizontalLayout(portRow, 8f);
            UIFactory.AddLayoutElement(portRow, 34);

            Text portLabel = UIFactory.CreateText(portRow.transform, "PortLabel", "Port:", 14);
            UIFactory.AddLayoutElement(portLabel.gameObject, preferredWidth: 100);

            portField = UIFactory.CreateInputField(portRow.transform, "PortField", "7777", "7777", 14);
            UIFactory.AddLayoutElement(portField.gameObject, 30, flexibleWidth: 1);

            // Spacer
            GameObject spacer = new GameObject("Spacer");
            spacer.transform.SetParent(inner, false);
            spacer.AddComponent<RectTransform>();
            UIFactory.AddLayoutElement(spacer, 8);

            // Host button
            Button btnHost = UIFactory.CreateGoldButton(inner, "BtnHost", "HOST GAME", 18, () =>
            {
                ApplySettings();
                NetworkLobbyManager.Instance.HostGame();
                statusText.text = "Hosting... Waiting for opponent.";
                statusText.color = UIFactory.GoldAccent;
            });
            UIFactory.AddLayoutElement(btnHost.gameObject, 45);

            // Join button
            Button btnJoin = UIFactory.CreateGoldButton(inner, "BtnJoin", "JOIN GAME", 18, () =>
            {
                ApplySettings();
                NetworkLobbyManager.Instance.JoinGame();
                statusText.text = "Connecting...";
                statusText.color = UIFactory.GoldAccent;
            });
            UIFactory.AddLayoutElement(btnJoin.gameObject, 45);

            // Status
            statusText = UIFactory.CreateText(inner, "Status", "", 14, TextAnchor.MiddleCenter, UIFactory.GoldAccent);
            UIFactory.AddLayoutElement(statusText.gameObject, 25);
        }

        private void BuildLobbyPanel(Transform parent)
        {
            RectTransform panel = UIFactory.CreateOrnatePanel(parent, "LobbyPanel",
                new Color(0.07f, 0.05f, 0.04f, 0.97f));
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(440, 300);
            lobbyPanel = panel.gameObject;

            Transform inner = panel.Find("Inner");
            UIFactory.AddVerticalLayout(inner.gameObject, 8f, new RectOffset(25, 25, 20, 20));

            RectTransform titleBanner = UIFactory.CreateBannerHeader(inner, "TitleBanner", "LOBBY", 26);
            UIFactory.AddLayoutElement(titleBanner.gameObject, 42);

            roleText = UIFactory.CreateText(inner, "Role", "Role: ---", 15, TextAnchor.MiddleLeft, UIFactory.ParchmentBeige);
            UIFactory.AddLayoutElement(roleText.gameObject, 24);

            playerCountText = UIFactory.CreateText(inner, "Players", "Players: 0/2", 15, TextAnchor.MiddleLeft, UIFactory.ParchmentBeige);
            UIFactory.AddLayoutElement(playerCountText.gameObject, 24);

            lobbyStatusText = UIFactory.CreateText(inner, "LobbyStatus", "Waiting for players...", 15, TextAnchor.MiddleLeft, UIFactory.GoldAccent);
            UIFactory.AddLayoutElement(lobbyStatusText.gameObject, 24);

            // Spacer
            GameObject spacer = new GameObject("Spacer");
            spacer.transform.SetParent(inner, false);
            spacer.AddComponent<RectTransform>();
            UIFactory.AddLayoutElement(spacer, 8);

            // Start Campaign (Host only)
            btnStartCampaign = UIFactory.CreateGoldButton(inner, "BtnStartCampaign", "START CAMPAIGN", 18, () =>
            {
                if (NetworkLobbyManager.Instance.IsHost)
                {
                    // For now, load the campaign scene
                    // In a full implementation, the NetworkManager should handle scene loading
                    // NetworkManager.Singleton.SceneManager.LoadScene("Campaign", UnityEngine.SceneManagement.LoadSceneMode.Single);
                    LoadingScreenUI.LoadSceneWithScreen("Campaign");
                }
            });
            UIFactory.AddLayoutElement(btnStartCampaign.gameObject, 45);

            // Disconnect
            Button btnDisconnect = UIFactory.CreateGoldButton(inner, "BtnDisconnect", "DISCONNECT", 16, () =>
            {
                NetworkLobbyManager.Instance.Disconnect();
                statusText.text = "";
            });
            UIFactory.AddLayoutElement(btnDisconnect.gameObject, 42);
            btnDisconnect.GetComponent<Image>().color = UIFactory.CrimsonDeep;
            btnDisconnect.GetComponentInChildren<Text>().color = new Color(0.85f, 0.35f, 0.3f);
        }

        private void UpdateLobby()
        {
            string role = NetworkLobbyManager.Instance.IsHost ? "HOST (France)" : "CLIENT (Britain)";
            roleText.text = $"Role: {role}";
            roleText.color = NetworkLobbyManager.Instance.IsHost ? UIFactory.FranceBlue : UIFactory.BritainRed;

            int playerCount = 0;
            if (NetworkGameManager.Instance != null)
                playerCount = NetworkGameManager.Instance.CurrentPlayerCount;
            playerCountText.text = $"Players: {playerCount}/2";

            bool gameStarted = NetworkGameManager.Instance != null && NetworkGameManager.Instance.GameStarted;
            if (gameStarted)
            {
                lobbyStatusText.text = "Battle in progress!";
                lobbyStatusText.color = new Color(0.3f, 0.9f, 0.3f);
            }
            else
            {
                lobbyStatusText.text = "Waiting for players...";
                lobbyStatusText.color = UIFactory.GoldAccent;
            }

            if (btnStartCampaign != null)
            {
                btnStartCampaign.gameObject.SetActive(NetworkLobbyManager.Instance.IsHost);
                btnStartCampaign.interactable = playerCount >= 1; // Allow testing with 1 player
            }
        }

        private void ApplySettings()
        {
            NetworkLobbyManager.Instance.IpAddress = ipField.text;
            if (ushort.TryParse(portField.text, out ushort p))
                NetworkLobbyManager.Instance.Port = p;
        }
    }
}
