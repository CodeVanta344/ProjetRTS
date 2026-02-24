using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using NapoleonicWars.Data;
using NapoleonicWars.Network;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// Co-op lobby UI: faction selection, role assignment, and game start.
    /// Replaces the standard lobby when co-op mode is selected.
    /// </summary>
    public class CoopLobbyUI : MonoBehaviour
    {
        private Canvas canvas;

        // Setup panel (host only)
        private GameObject setupPanel;
        private Dropdown factionDropdown;
        private Dropdown playerCountDropdown;
        private Text setupStatusText;

        // Role selection panel (all players)
        private GameObject rolePanel;
        private Dictionary<CoopRole, Button> roleButtons = new Dictionary<CoopRole, Button>();
        private Dictionary<CoopRole, Text> roleStatusTexts = new Dictionary<CoopRole, Text>();
        private Text roleInfoText;
        private Text connectedPlayersText;
        private Button btnStartCampaign;
        private Button btnBack;

        // State
        private bool isSetupPhase = true;
        private bool lastConnected = false;
        private bool stateInitialized = false;

        private void Start()
        {
            BuildUI();

            if (CoopRoleManager.Instance != null)
            {
                CoopRoleManager.Instance.OnRoleAssigned += OnRoleAssigned;
                CoopRoleManager.Instance.OnCoopStateChanged += RefreshRolePanel;
            }
        }

        private void OnDestroy()
        {
            if (CoopRoleManager.Instance != null)
            {
                CoopRoleManager.Instance.OnRoleAssigned -= OnRoleAssigned;
                CoopRoleManager.Instance.OnCoopStateChanged -= RefreshRolePanel;
            }
        }

        private void Update()
        {
            if (NetworkLobbyManager.Instance == null) return;

            bool connected = NetworkLobbyManager.Instance.IsConnected;

            if (!stateInitialized || connected != lastConnected)
            {
                if (connected && !isSetupPhase)
                    RefreshRolePanel();
                lastConnected = connected;
                stateInitialized = true;
            }

            if (connected && !isSetupPhase)
                UpdateRolePanel();
        }

        // ============================================================
        // BUILD UI
        // ============================================================

        private void BuildUI()
        {
            canvas = UIFactory.CreateCanvas("CoopLobbyCanvas", 12);
            canvas.transform.SetParent(transform);

            // Full-screen dark background
            RectTransform bg = UIFactory.CreatePanel(canvas.transform, "Background",
                new Color(0.04f, 0.02f, 0.02f, 0.95f));
            bg.anchorMin = Vector2.zero;
            bg.anchorMax = Vector2.one;
            bg.offsetMin = Vector2.zero;
            bg.offsetMax = Vector2.zero;

            BuildSetupPanel(bg);
            BuildRolePanel(bg);

            rolePanel.SetActive(false);
        }

        // ============================================================
        // SETUP PANEL (Host configures co-op before anyone joins)
        // ============================================================

        private void BuildSetupPanel(Transform parent)
        {
            RectTransform panel = UIFactory.CreateOrnatePanel(parent, "SetupPanel",
                new Color(0.07f, 0.05f, 0.04f, 0.97f));
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(500, 520);
            setupPanel = panel.gameObject;

            Transform inner = panel.Find("Inner");
            UIFactory.AddVerticalLayout(inner.gameObject, 10f, new RectOffset(25, 25, 20, 20));

            // Title
            RectTransform titleBanner = UIFactory.CreateBannerHeader(inner, "Title", "CAMPAGNE CO-OP", 26);
            UIFactory.AddLayoutElement(titleBanner.gameObject, 42);

            Text subtitle = UIFactory.CreateText(inner, "Subtitle",
                "Gérez un pays à plusieurs", 15, TextAnchor.MiddleCenter, UIFactory.ParchmentBeige);
            subtitle.fontStyle = FontStyle.Italic;
            UIFactory.AddLayoutElement(subtitle.gameObject, 25);

            // Faction selection
            Text factionLabel = UIFactory.CreateText(inner, "FactionLabel",
                "Pays :", 14, TextAnchor.MiddleLeft, UIFactory.GoldAccent);
            UIFactory.AddLayoutElement(factionLabel.gameObject, 22);

            GameObject factionRow = new GameObject("FactionRow");
            factionRow.transform.SetParent(inner, false);
            factionRow.AddComponent<RectTransform>();
            UIFactory.AddLayoutElement(factionRow, 35);

            factionDropdown = CreateDropdown(factionRow.transform, "FactionDropdown",
                new List<string> { "France", "Grande-Bretagne", "Prusse", "Russie", "Autriche", "Espagne", "Ottoman" });

            // Player count
            Text countLabel = UIFactory.CreateText(inner, "CountLabel",
                "Nombre de joueurs :", 14, TextAnchor.MiddleLeft, UIFactory.GoldAccent);
            UIFactory.AddLayoutElement(countLabel.gameObject, 22);

            GameObject countRow = new GameObject("CountRow");
            countRow.transform.SetParent(inner, false);
            countRow.AddComponent<RectTransform>();
            UIFactory.AddLayoutElement(countRow, 35);

            playerCountDropdown = CreateDropdown(countRow.transform, "CountDropdown",
                new List<string> { "2 joueurs (Maréchal + Grand Vizir)", "3 joueurs (Maréchal + Intendant + Chancelier)" });

            // Spacer
            CreateSpacer(inner, 10);

            // IP/Port info
            Text ipInfo = UIFactory.CreateText(inner, "IPInfo",
                "Les autres joueurs rejoindront avec votre IP.", 12, TextAnchor.MiddleCenter, UIFactory.TextGrey);
            UIFactory.AddLayoutElement(ipInfo.gameObject, 20);

            // Status
            setupStatusText = UIFactory.CreateText(inner, "Status", "", 14, TextAnchor.MiddleCenter, UIFactory.GoldAccent);
            UIFactory.AddLayoutElement(setupStatusText.gameObject, 25);

            // Host & Start
            Button btnHost = UIFactory.CreateGoldButton(inner, "BtnHostCoop", "HÉBERGER CO-OP", 18, () =>
            {
                if (NetworkLobbyManager.Instance == null) return;

                // Configure co-op mode
                FactionType faction = GetSelectedFaction();
                int maxPlayers = playerCountDropdown.value == 0 ? 2 : 3;

                NetworkLobbyManager.Instance.HostGame();

                // Wait a frame for network init, then configure co-op
                StartCoroutine(ConfigureCoopAfterHost(faction, maxPlayers));
            });
            UIFactory.AddLayoutElement(btnHost.gameObject, 45);

            // Back
            btnBack = UIFactory.CreateGoldButton(inner, "BtnBack", "RETOUR", 16, () =>
            {
                // Return to main lobby or menu
                gameObject.SetActive(false);
            });
            UIFactory.AddLayoutElement(btnBack.gameObject, 40);
            btnBack.GetComponent<Image>().color = UIFactory.CrimsonDeep;
        }

        private System.Collections.IEnumerator ConfigureCoopAfterHost(FactionType faction, int maxPlayers)
        {
            // Wait for network to initialize
            yield return new WaitForSeconds(0.5f);

            if (CoopRoleManager.Instance != null)
            {
                CoopRoleManager.Instance.ConfigureCoopMode(true, faction, maxPlayers);
                setupStatusText.text = $"Co-op hébergé: {faction}, {maxPlayers} joueurs";
                setupStatusText.color = new Color(0.3f, 0.9f, 0.3f);
            }

            // Switch to role panel
            isSetupPhase = false;
            setupPanel.SetActive(false);
            rolePanel.SetActive(true);
            RefreshRolePanel();
        }

        // ============================================================
        // ROLE SELECTION PANEL (visible once connected)
        // ============================================================

        private void BuildRolePanel(Transform parent)
        {
            RectTransform panel = UIFactory.CreateOrnatePanel(parent, "RolePanel",
                new Color(0.07f, 0.05f, 0.04f, 0.97f));
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(650, 580);
            rolePanel = panel.gameObject;

            Transform inner = panel.Find("Inner");
            UIFactory.AddVerticalLayout(inner.gameObject, 8f, new RectOffset(25, 25, 15, 15));

            // Title
            RectTransform titleBanner = UIFactory.CreateBannerHeader(inner, "Title", "CHOIX DES RÔLES", 24);
            UIFactory.AddLayoutElement(titleBanner.gameObject, 40);

            // Connected players info
            connectedPlayersText = UIFactory.CreateText(inner, "Connected",
                "Joueurs connectés: 0/2", 14, TextAnchor.MiddleCenter, UIFactory.ParchmentBeige);
            UIFactory.AddLayoutElement(connectedPlayersText.gameObject, 22);

            // Role cards container
            GameObject rolesContainer = new GameObject("RolesContainer");
            rolesContainer.transform.SetParent(inner, false);
            rolesContainer.AddComponent<RectTransform>();
            UIFactory.AddVerticalLayout(rolesContainer, 6f);
            UIFactory.AddLayoutElement(rolesContainer, preferredHeight: 320);

            // Create role cards
            CreateRoleCard(rolesContainer.transform, CoopRole.Marshal,
                "MARÉCHAL", "⚔️",
                "Armées, batailles, recrutement, divisions, généraux",
                new Color(0.9f, 0.3f, 0.3f));

            CreateRoleCard(rolesContainer.transform, CoopRole.Intendant,
                "INTENDANT", "🏭",
                "Production, construction, supply, commerce, usines",
                new Color(0.3f, 0.7f, 0.9f));

            CreateRoleCard(rolesContainer.transform, CoopRole.Chancellor,
                "CHANCELIER", "⚖️",
                "Lois nationales, diplomatie, recherche, espionnage",
                new Color(0.9f, 0.8f, 0.3f));

            CreateRoleCard(rolesContainer.transform, CoopRole.GrandVizir,
                "GRAND VIZIR", "👑",
                "Économie + Politique combinés (mode 2 joueurs)",
                new Color(0.7f, 0.4f, 0.9f));

            // Info text
            roleInfoText = UIFactory.CreateText(inner, "RoleInfo",
                "Cliquez sur un rôle pour le sélectionner.", 13, TextAnchor.MiddleCenter, UIFactory.TextGrey);
            UIFactory.AddLayoutElement(roleInfoText.gameObject, 25);

            // Start button (host only)
            btnStartCampaign = UIFactory.CreateWarhammerButton(inner, "BtnStart", "LANCER LA CAMPAGNE", 20, () =>
            {
                if (NetworkLobbyManager.Instance != null && NetworkLobbyManager.Instance.IsHost)
                {
                    if (CoopRoleManager.Instance != null && CoopRoleManager.Instance.AreAllRolesFilled())
                    {
                        LoadingScreenUI.LoadSceneWithScreen("Campaign");
                    }
                    else
                    {
                        roleInfoText.text = "Tous les rôles doivent être attribués !";
                        roleInfoText.color = new Color(1f, 0.3f, 0.3f);
                    }
                }
            });
            UIFactory.AddLayoutElement(btnStartCampaign.gameObject, 50);

            // Disconnect
            Button btnDisconnect = UIFactory.CreateGoldButton(inner, "BtnDisconnect", "DÉCONNECTER", 15, () =>
            {
                NetworkLobbyManager.Instance?.Disconnect();
                isSetupPhase = true;
                rolePanel.SetActive(false);
                setupPanel.SetActive(true);
            });
            UIFactory.AddLayoutElement(btnDisconnect.gameObject, 38);
            btnDisconnect.GetComponent<Image>().color = UIFactory.CrimsonDeep;
        }

        private void CreateRoleCard(Transform parent, CoopRole role, string title, string icon,
            string description, Color accentColor)
        {
            // Card container
            RectTransform card = UIFactory.CreateBorderedPanel(parent, $"Role_{role}",
                new Color(0.12f, 0.13f, 0.11f, 0.95f), accentColor, 1.5f);
            UIFactory.AddLayoutElement(card.gameObject, preferredHeight: 70);

            Transform cardInner = card.GetChild(0);

            // Icon + title (left side)
            Text iconText = UIFactory.CreateText(cardInner, "Icon", icon, 22, TextAnchor.MiddleCenter, Color.white);
            RectTransform iconRT = iconText.GetComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0, 0);
            iconRT.anchorMax = new Vector2(0.08f, 1);
            iconRT.offsetMin = new Vector2(8, 0);
            iconRT.offsetMax = Vector2.zero;

            Text titleText = UIFactory.CreateText(cardInner, "Title", title, 16, TextAnchor.MiddleLeft, accentColor);
            titleText.fontStyle = FontStyle.Bold;
            RectTransform titleRT = titleText.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.09f, 0.5f);
            titleRT.anchorMax = new Vector2(0.4f, 1);
            titleRT.offsetMin = new Vector2(4, 0);
            titleRT.offsetMax = Vector2.zero;

            // Description
            Text descText = UIFactory.CreateText(cardInner, "Desc", description, 11, TextAnchor.MiddleLeft, UIFactory.TextGrey);
            RectTransform descRT = descText.GetComponent<RectTransform>();
            descRT.anchorMin = new Vector2(0.09f, 0);
            descRT.anchorMax = new Vector2(0.65f, 0.5f);
            descRT.offsetMin = new Vector2(4, 4);
            descRT.offsetMax = Vector2.zero;

            // Status text (right side)
            Text statusText = UIFactory.CreateText(cardInner, "Status", "Libre", 13, TextAnchor.MiddleCenter, UIFactory.TextGrey);
            RectTransform statusRT = statusText.GetComponent<RectTransform>();
            statusRT.anchorMin = new Vector2(0.65f, 0);
            statusRT.anchorMax = new Vector2(0.85f, 1);
            statusRT.offsetMin = Vector2.zero;
            statusRT.offsetMax = Vector2.zero;
            roleStatusTexts[role] = statusText;

            // Select button
            Button selectBtn = UIFactory.CreateGoldButton(cardInner, "BtnSelect", "CHOISIR", 12, () =>
            {
                RequestRole(role);
            });
            RectTransform btnRT = selectBtn.GetComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.85f, 0.15f);
            btnRT.anchorMax = new Vector2(0.98f, 0.85f);
            btnRT.offsetMin = Vector2.zero;
            btnRT.offsetMax = Vector2.zero;
            roleButtons[role] = selectBtn;
        }

        // ============================================================
        // LOGIC
        // ============================================================

        private void RequestRole(CoopRole role)
        {
            if (CoopRoleManager.Instance == null) return;
            CoopRoleManager.Instance.RequestRoleServerRpc(role);
            roleInfoText.text = $"Demande du rôle {CoopRoleManager.GetRoleName(role)}...";
            roleInfoText.color = UIFactory.GoldAccent;
        }

        private void OnRoleAssigned(ulong clientId, CoopRole role)
        {
            RefreshRolePanel();
        }

        private void RefreshRolePanel()
        {
            if (CoopRoleManager.Instance == null) return;

            var players = CoopRoleManager.Instance.GetAllPlayers();
            int maxPlayers = CoopRoleManager.Instance.MaxCoopPlayers;
            bool is2Player = maxPlayers == 2;

            // Update connected count
            if (connectedPlayersText != null)
                connectedPlayersText.text = $"Joueurs connectés: {players.Count}/{maxPlayers}";

            // Update each role card
            foreach (var kvp in roleButtons)
            {
                CoopRole role = kvp.Key;
                Button btn = kvp.Value;
                Text statusText = roleStatusTexts[role];

                // Hide irrelevant roles based on player count
                bool roleVisible = true;
                if (is2Player && (role == CoopRole.Intendant || role == CoopRole.Chancellor))
                    roleVisible = false;
                if (!is2Player && role == CoopRole.GrandVizir)
                    roleVisible = false;

                btn.transform.parent.parent.gameObject.SetActive(roleVisible);

                if (!roleVisible) continue;

                // Check if role is taken
                bool isTaken = CoopRoleManager.Instance.IsRoleFilled(role);
                bool isLocalRole = CoopRoleManager.Instance.GetLocalRole() == role;

                if (isLocalRole)
                {
                    statusText.text = "VOUS";
                    statusText.color = new Color(0.3f, 1f, 0.3f);
                    statusText.fontStyle = FontStyle.Bold;
                    btn.interactable = false;
                    btn.GetComponentInChildren<Text>().text = "✓";
                }
                else if (isTaken)
                {
                    statusText.text = "Pris";
                    statusText.color = new Color(1f, 0.4f, 0.4f);
                    statusText.fontStyle = FontStyle.Normal;
                    btn.interactable = false;
                    btn.GetComponentInChildren<Text>().text = "---";
                }
                else
                {
                    statusText.text = "Libre";
                    statusText.color = UIFactory.TextGrey;
                    statusText.fontStyle = FontStyle.Normal;
                    btn.interactable = true;
                    btn.GetComponentInChildren<Text>().text = "CHOISIR";
                }
            }

            // Start button visibility
            if (btnStartCampaign != null)
            {
                bool isHost = NetworkLobbyManager.Instance != null && NetworkLobbyManager.Instance.IsHost;
                btnStartCampaign.gameObject.SetActive(isHost);
                btnStartCampaign.interactable = CoopRoleManager.Instance.AreAllRolesFilled();
            }
        }

        private void UpdateRolePanel()
        {
            // Periodic light refresh for connection state changes
            if (CoopRoleManager.Instance == null) return;

            var players = CoopRoleManager.Instance.GetAllPlayers();
            if (connectedPlayersText != null)
                connectedPlayersText.text = $"Joueurs connectés: {players.Count}/{CoopRoleManager.Instance.MaxCoopPlayers}";
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private FactionType GetSelectedFaction()
        {
            if (factionDropdown == null) return FactionType.France;
            return factionDropdown.value switch
            {
                0 => FactionType.France,
                1 => FactionType.Britain,
                2 => FactionType.Prussia,
                3 => FactionType.Russia,
                4 => FactionType.Austria,
                5 => FactionType.Spain,
                6 => FactionType.Ottoman,
                _ => FactionType.France
            };
        }

        private Dropdown CreateDropdown(Transform parent, string name, List<string> options)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image bgImg = go.AddComponent<Image>();
            bgImg.color = UIFactory.ButtonNormal;

            Dropdown dd = go.AddComponent<Dropdown>();

            // Label
            GameObject labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);
            RectTransform labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = new Vector2(10, 0);
            labelRT.offsetMax = new Vector2(-25, 0);
            Text labelText = labelGO.AddComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 14;
            labelText.color = UIFactory.ParchmentBeige;
            labelText.alignment = TextAnchor.MiddleLeft;
            dd.captionText = labelText;

            // Template (minimal)
            GameObject templateGO = new GameObject("Template");
            templateGO.transform.SetParent(go.transform, false);
            RectTransform templateRT = templateGO.AddComponent<RectTransform>();
            templateRT.anchorMin = new Vector2(0, 0);
            templateRT.anchorMax = new Vector2(1, 0);
            templateRT.pivot = new Vector2(0.5f, 1);
            templateRT.sizeDelta = new Vector2(0, 150);
            Image templateBg = templateGO.AddComponent<Image>();
            templateBg.color = new Color(0.10f, 0.11f, 0.10f, 0.98f);
            ScrollRect scrollRect = templateGO.AddComponent<ScrollRect>();

            // Viewport
            GameObject viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(templateGO.transform, false);
            RectTransform viewportRT = viewportGO.AddComponent<RectTransform>();
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.offsetMin = Vector2.zero;
            viewportRT.offsetMax = Vector2.zero;
            viewportGO.AddComponent<Image>().color = Color.clear;
            viewportGO.AddComponent<Mask>().showMaskGraphic = false;
            scrollRect.viewport = viewportRT;

            // Content
            GameObject contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewportGO.transform, false);
            RectTransform contentRT = contentGO.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.sizeDelta = new Vector2(0, 28);
            scrollRect.content = contentRT;

            // Item template
            GameObject itemGO = new GameObject("Item");
            itemGO.transform.SetParent(contentGO.transform, false);
            RectTransform itemRT = itemGO.AddComponent<RectTransform>();
            itemRT.anchorMin = new Vector2(0, 0.5f);
            itemRT.anchorMax = new Vector2(1, 0.5f);
            itemRT.sizeDelta = new Vector2(0, 28);
            Toggle itemToggle = itemGO.AddComponent<Toggle>();
            itemGO.AddComponent<Image>().color = UIFactory.ButtonNormal;

            // Item label
            GameObject itemLabelGO = new GameObject("Item Label");
            itemLabelGO.transform.SetParent(itemGO.transform, false);
            RectTransform itemLabelRT = itemLabelGO.AddComponent<RectTransform>();
            itemLabelRT.anchorMin = Vector2.zero;
            itemLabelRT.anchorMax = Vector2.one;
            itemLabelRT.offsetMin = new Vector2(10, 0);
            itemLabelRT.offsetMax = new Vector2(-10, 0);
            Text itemLabelText = itemLabelGO.AddComponent<Text>();
            itemLabelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            itemLabelText.fontSize = 13;
            itemLabelText.color = UIFactory.ParchmentBeige;
            itemLabelText.alignment = TextAnchor.MiddleLeft;

            dd.itemText = itemLabelText;
            itemToggle.targetGraphic = itemGO.GetComponent<Image>();

            dd.template = templateRT;
            templateGO.SetActive(false);

            dd.ClearOptions();
            dd.AddOptions(options);

            return dd;
        }

        private void CreateSpacer(Transform parent, float height)
        {
            GameObject spacer = new GameObject("Spacer");
            spacer.transform.SetParent(parent, false);
            spacer.AddComponent<RectTransform>();
            UIFactory.AddLayoutElement(spacer, height);
        }

        /// <summary>Show this panel (called from LobbyUI or MainMenu)</summary>
        public void Show()
        {
            gameObject.SetActive(true);
            isSetupPhase = true;
            setupPanel.SetActive(true);
            rolePanel.SetActive(false);
        }
    }
}
