using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Network;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// Co-op HUD: right sidebar showing teammate status, chat button, and request notifications.
    /// Only visible in co-op mode.
    /// </summary>
    public class CoopHUD : MonoBehaviour
    {
        private GameObject hudRoot;
        private Dictionary<CoopRole, Text> playerStatusTexts = new Dictionary<CoopRole, Text>();
        private Text requestBadgeText;
        private int lastPendingCount = -1;

        // Request panel (toggled)
        private GameObject requestPanel;
        private Transform requestListContent;
        private bool requestPanelOpen = false;

        public void Initialize()
        {
            BuildHUD();

            if (CoopRoleManager.Instance != null)
            {
                CoopRoleManager.Instance.OnCoopStateChanged += RefreshPlayerStatus;
                CoopRoleManager.Instance.OnRoleAssigned += (_, _) => RefreshPlayerStatus();
            }

            if (CoopRequestSystem.Instance != null)
            {
                CoopRequestSystem.Instance.OnRequestReceived += OnNewRequest;
                CoopRequestSystem.Instance.OnRequestUpdated += OnRequestUpdate;
            }
        }

        private void OnDestroy()
        {
            if (CoopRoleManager.Instance != null)
            {
                CoopRoleManager.Instance.OnCoopStateChanged -= RefreshPlayerStatus;
            }
            if (CoopRequestSystem.Instance != null)
            {
                CoopRequestSystem.Instance.OnRequestReceived -= OnNewRequest;
                CoopRequestSystem.Instance.OnRequestUpdated -= OnRequestUpdate;
            }
        }

        private void Update()
        {
            if (CoopRoleManager.Instance == null || !CoopRoleManager.Instance.IsCoopMode)
            {
                if (hudRoot != null) hudRoot.SetActive(false);
                return;
            }

            if (hudRoot != null && !hudRoot.activeSelf) hudRoot.SetActive(true);

            // Update request badge count
            UpdateRequestBadge();
        }

        // ============================================================
        // BUILD UI
        // ============================================================

        private void BuildHUD()
        {
            hudRoot = new GameObject("CoopHUD");
            hudRoot.transform.SetParent(transform, false);
            RectTransform rootRT = hudRoot.AddComponent<RectTransform>();
            rootRT.anchorMin = Vector2.zero;
            rootRT.anchorMax = Vector2.one;
            rootRT.offsetMin = Vector2.zero;
            rootRT.offsetMax = Vector2.zero;

            // Right sidebar (mirror of NavBar on the left)
            RectTransform sidebar = UIFactory.CreatePanel(hudRoot.transform, "CoopSidebar",
                new Color(0.10f, 0.11f, 0.10f, 0.95f));
            sidebar.anchorMin = new Vector2(1, 0);
            sidebar.anchorMax = new Vector2(1, 1);
            sidebar.offsetMin = new Vector2(-55, 55); // Above bottom bar
            sidebar.offsetMax = new Vector2(0, -40);  // Below top bar

            // Gold border left
            RectTransform borderLine = UIFactory.CreatePanel(sidebar, "Border", UIFactory.BorderGold);
            borderLine.anchorMin = new Vector2(0, 0);
            borderLine.anchorMax = new Vector2(0, 1);
            borderLine.offsetMin = Vector2.zero;
            borderLine.offsetMax = new Vector2(2, 0);
            borderLine.GetComponent<Image>().raycastTarget = false;

            VerticalLayoutGroup vlg = UIFactory.AddVerticalLayout(sidebar.gameObject, 4f,
                new RectOffset(4, 4, 8, 8));
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // "CO-OP" header
            Text header = UIFactory.CreateText(sidebar, "Header", "CO-OP", 10,
                TextAnchor.MiddleCenter, UIFactory.GoldAccent);
            header.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(header.gameObject, preferredHeight: 18);

            // Separator
            RectTransform sep = UIFactory.CreatePanel(sidebar, "Sep", 
                new Color(UIFactory.BorderGold.r, UIFactory.BorderGold.g, UIFactory.BorderGold.b, 0.4f));
            UIFactory.AddLayoutElement(sep.gameObject, preferredHeight: 1);

            // Player status cards
            CreatePlayerStatusCard(sidebar, CoopRole.Marshal);
            CreatePlayerStatusCard(sidebar, CoopRole.Intendant);
            CreatePlayerStatusCard(sidebar, CoopRole.Chancellor);
            CreatePlayerStatusCard(sidebar, CoopRole.GrandVizir);

            // Separator
            RectTransform sep2 = UIFactory.CreatePanel(sidebar, "Sep2",
                new Color(UIFactory.BorderGold.r, UIFactory.BorderGold.g, UIFactory.BorderGold.b, 0.4f));
            UIFactory.AddLayoutElement(sep2.gameObject, preferredHeight: 1);

            // Request button with badge
            GameObject reqBtnContainer = new GameObject("ReqBtnContainer");
            reqBtnContainer.transform.SetParent(sidebar, false);
            reqBtnContainer.AddComponent<RectTransform>();
            UIFactory.AddLayoutElement(reqBtnContainer, preferredHeight: 45);

            Button reqBtn = UIFactory.CreateButton(reqBtnContainer.transform, "ReqBtn", "📋", 18, () =>
            {
                ToggleRequestPanel();
            });
            RectTransform reqBtnRT = reqBtn.GetComponent<RectTransform>();
            reqBtnRT.anchorMin = Vector2.zero;
            reqBtnRT.anchorMax = Vector2.one;
            reqBtnRT.offsetMin = new Vector2(2, 2);
            reqBtnRT.offsetMax = new Vector2(-2, -2);

            // Badge (notification count)
            requestBadgeText = UIFactory.CreateText(reqBtnContainer.transform, "Badge", "",
                9, TextAnchor.UpperRight, new Color(1f, 0.3f, 0.3f));
            requestBadgeText.fontStyle = FontStyle.Bold;
            RectTransform badgeRT = requestBadgeText.GetComponent<RectTransform>();
            badgeRT.anchorMin = new Vector2(0.6f, 0.6f);
            badgeRT.anchorMax = new Vector2(1, 1);
            badgeRT.offsetMin = Vector2.zero;
            badgeRT.offsetMax = Vector2.zero;

            Text reqLabel = UIFactory.CreateText(reqBtnContainer.transform, "Label", "Req.",
                8, TextAnchor.LowerCenter, UIFactory.TextGrey);
            RectTransform reqLabelRT = reqLabel.GetComponent<RectTransform>();
            reqLabelRT.anchorMin = new Vector2(0, 0);
            reqLabelRT.anchorMax = new Vector2(1, 0.3f);
            reqLabelRT.offsetMin = Vector2.zero;
            reqLabelRT.offsetMax = Vector2.zero;

            // Build request panel (hidden by default)
            BuildRequestPanel();

            hudRoot.SetActive(false); // Hidden until co-op mode detected
        }

        private void CreatePlayerStatusCard(Transform parent, CoopRole role)
        {
            GameObject card = new GameObject($"Status_{role}");
            card.transform.SetParent(parent, false);
            card.AddComponent<RectTransform>();
            UIFactory.AddLayoutElement(card, preferredHeight: 38);

            Image cardBg = card.AddComponent<Image>();
            cardBg.color = new Color(0.12f, 0.13f, 0.11f, 0.8f);
            cardBg.raycastTarget = false;

            // Icon + status
            Color roleColor = CoopRoleManager.GetRoleColor(role);
            Text statusText = UIFactory.CreateText(card.transform, "Status",
                $"{CoopRoleManager.GetRoleIcon(role)}\n---", 9, TextAnchor.MiddleCenter, roleColor);
            RectTransform stRT = statusText.GetComponent<RectTransform>();
            stRT.anchorMin = Vector2.zero;
            stRT.anchorMax = Vector2.one;
            stRT.offsetMin = new Vector2(2, 1);
            stRT.offsetMax = new Vector2(-2, -1);

            playerStatusTexts[role] = statusText;
        }

        // ============================================================
        // REQUEST PANEL
        // ============================================================

        private void BuildRequestPanel()
        {
            requestPanel = new GameObject("RequestPanel");
            requestPanel.transform.SetParent(hudRoot.transform, false);
            RectTransform panelRT = requestPanel.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(1, 0);
            panelRT.anchorMax = new Vector2(1, 1);
            panelRT.offsetMin = new Vector2(-350, 60);
            panelRT.offsetMax = new Vector2(-60, -45);

            Image panelBg = requestPanel.AddComponent<Image>();
            panelBg.color = new Color(0.07f, 0.05f, 0.04f, 0.97f);
            panelBg.raycastTarget = true;

            VerticalLayoutGroup vlg = UIFactory.AddVerticalLayout(requestPanel, 0f, new RectOffset(0, 0, 0, 0));
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;

            // Header
            RectTransform header = UIFactory.CreateBannerHeader(requestPanel.transform, "ReqHeader",
                "REQUÊTES CO-OP", 18);
            UIFactory.AddLayoutElement(header.gameObject, preferredHeight: 35);

            // Close button
            Button closeBtn = UIFactory.CreateButton(header, "CloseReq", "✕", 14, () =>
            {
                requestPanel.SetActive(false);
                requestPanelOpen = false;
            });
            RectTransform closeRT = closeBtn.GetComponent<RectTransform>();
            closeRT.anchorMin = new Vector2(1, 0);
            closeRT.anchorMax = new Vector2(1, 1);
            closeRT.offsetMin = new Vector2(-32, 2);
            closeRT.offsetMax = new Vector2(-4, -2);

            // Scrollable request list
            var (scroll, content) = UIFactory.CreateScrollView(requestPanel.transform, "ReqScroll");
            UIFactory.AddLayoutElement(scroll.gameObject, flexibleHeight: 1, preferredHeight: 500);
            requestListContent = content;

            requestPanel.SetActive(false);
        }

        private void ToggleRequestPanel()
        {
            requestPanelOpen = !requestPanelOpen;
            requestPanel.SetActive(requestPanelOpen);

            if (requestPanelOpen)
                RefreshRequestList();
        }

        private void RefreshRequestList()
        {
            if (requestListContent == null) return;

            // Clear existing
            foreach (Transform child in requestListContent)
                Destroy(child.gameObject);

            if (CoopRequestSystem.Instance == null || CoopRoleManager.Instance == null) return;

            CoopRole localRole = CoopRoleManager.Instance.GetLocalRole();
            var allRequests = CoopRequestSystem.Instance.GetAllRequests();

            // Show pending first, then recent history
            allRequests.Sort((a, b) =>
            {
                if (a.status == CoopRequestStatus.Pending && b.status != CoopRequestStatus.Pending) return -1;
                if (b.status == CoopRequestStatus.Pending && a.status != CoopRequestStatus.Pending) return 1;
                return b.turnCreated.CompareTo(a.turnCreated);
            });

            foreach (var req in allRequests)
            {
                CreateRequestCard(requestListContent, req, localRole);
            }

            if (allRequests.Count == 0)
            {
                Text emptyText = UIFactory.CreateText(requestListContent, "Empty",
                    "Aucune requête.", 13, TextAnchor.MiddleCenter, UIFactory.TextGrey);
                UIFactory.AddLayoutElement(emptyText.gameObject, preferredHeight: 40);
            }
        }

        private void CreateRequestCard(Transform parent, CoopRequest req, CoopRole localRole)
        {
            bool isPending = req.status == CoopRequestStatus.Pending;
            bool isTargetingMe = req.targetRole == localRole || req.isVote;

            Color bgColor = isPending && isTargetingMe
                ? new Color(0.12f, 0.10f, 0.06f, 0.95f)
                : new Color(0.12f, 0.13f, 0.11f, 0.9f);

            RectTransform card = UIFactory.CreatePanel(parent, $"Req_{req.requestId}", bgColor);
            UIFactory.AddLayoutElement(card.gameObject, preferredHeight: isPending && isTargetingMe ? 80 : 50);

            // Sender info
            string senderIcon = CoopRoleManager.GetRoleIcon(req.senderRole);
            string senderName = CoopRoleManager.GetRoleName(req.senderRole);
            Text senderText = UIFactory.CreateText(card, "Sender",
                $"{senderIcon} {senderName} → {CoopRoleManager.GetRoleName(req.targetRole)}",
                10, TextAnchor.UpperLeft, CoopRoleManager.GetRoleColor(req.senderRole));
            RectTransform senderRT = senderText.GetComponent<RectTransform>();
            senderRT.anchorMin = new Vector2(0, 0.6f);
            senderRT.anchorMax = new Vector2(0.7f, 1);
            senderRT.offsetMin = new Vector2(8, 0);
            senderRT.offsetMax = new Vector2(0, -4);

            // Request type name
            string reqName = CoopRequestSystem.GetRequestName(req.type);
            Text nameText = UIFactory.CreateText(card, "Name", reqName,
                11, TextAnchor.MiddleLeft, UIFactory.ParchmentBeige);
            nameText.fontStyle = FontStyle.Bold;
            RectTransform nameRT = nameText.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0, 0.3f);
            nameRT.anchorMax = new Vector2(0.6f, 0.65f);
            nameRT.offsetMin = new Vector2(8, 0);
            nameRT.offsetMax = Vector2.zero;

            // Status
            Text statusText = UIFactory.CreateText(card, "Status",
                CoopRequestSystem.GetStatusName(req.status), 10, TextAnchor.UpperRight,
                CoopRequestSystem.GetStatusColor(req.status));
            RectTransform statusRT = statusText.GetComponent<RectTransform>();
            statusRT.anchorMin = new Vector2(0.7f, 0.6f);
            statusRT.anchorMax = new Vector2(1, 1);
            statusRT.offsetMin = Vector2.zero;
            statusRT.offsetMax = new Vector2(-8, -4);

            // Accept/Decline buttons (only for pending requests targeting local role)
            if (isPending && isTargetingMe)
            {
                int capturedId = req.requestId;

                Button acceptBtn = UIFactory.CreateButton(card, "Accept", "✓ Accepter", 10, () =>
                {
                    CoopRequestSystem.Instance?.RespondToRequestServerRpc(capturedId, true);
                });
                RectTransform acceptRT = acceptBtn.GetComponent<RectTransform>();
                acceptRT.anchorMin = new Vector2(0.5f, 0);
                acceptRT.anchorMax = new Vector2(0.75f, 0.3f);
                acceptRT.offsetMin = new Vector2(4, 4);
                acceptRT.offsetMax = new Vector2(-2, 0);
                acceptBtn.GetComponent<Image>().color = new Color(0.1f, 0.25f, 0.1f, 0.95f);

                Button declineBtn = UIFactory.CreateButton(card, "Decline", "✗ Refuser", 10, () =>
                {
                    CoopRequestSystem.Instance?.RespondToRequestServerRpc(capturedId, false);
                });
                RectTransform declineRT = declineBtn.GetComponent<RectTransform>();
                declineRT.anchorMin = new Vector2(0.75f, 0);
                declineRT.anchorMax = new Vector2(1, 0.3f);
                declineRT.offsetMin = new Vector2(2, 4);
                declineRT.offsetMax = new Vector2(-4, 0);
                declineBtn.GetComponent<Image>().color = new Color(0.25f, 0.08f, 0.08f, 0.95f);
            }
        }

        // ============================================================
        // REFRESH METHODS
        // ============================================================

        private void RefreshPlayerStatus()
        {
            if (CoopRoleManager.Instance == null) return;

            var players = CoopRoleManager.Instance.GetAllPlayers();
            int maxPlayers = CoopRoleManager.Instance.MaxCoopPlayers;
            bool is2Player = maxPlayers == 2;

            foreach (var kvp in playerStatusTexts)
            {
                CoopRole role = kvp.Key;
                Text text = kvp.Value;

                // Show/hide based on player count
                bool visible = true;
                if (is2Player && (role == CoopRole.Intendant || role == CoopRole.Chancellor))
                    visible = false;
                if (!is2Player && role == CoopRole.GrandVizir)
                    visible = false;

                text.transform.parent.gameObject.SetActive(visible);

                if (!visible) continue;

                string icon = CoopRoleManager.GetRoleIcon(role);
                bool isFilled = CoopRoleManager.Instance.IsRoleFilled(role);

                if (isFilled)
                {
                    var playerList = CoopRoleManager.Instance.GetAllPlayers();
                    var player = playerList.Find(p => p.role == role);
                    if (player.isConnected)
                    {
                        text.text = $"{icon}\n🟢";
                        text.color = CoopRoleManager.GetRoleColor(role);
                    }
                    else
                    {
                        text.text = $"{icon}\n🔴";
                        text.color = new Color(0.5f, 0.3f, 0.3f);
                    }
                }
                else
                {
                    text.text = $"{icon}\n---";
                    text.color = UIFactory.TextGrey;
                }
            }
        }

        private void UpdateRequestBadge()
        {
            if (CoopRequestSystem.Instance == null || CoopRoleManager.Instance == null) return;

            CoopRole localRole = CoopRoleManager.Instance.GetLocalRole();
            int count = CoopRequestSystem.Instance.GetPendingCount(localRole);

            if (count != lastPendingCount)
            {
                lastPendingCount = count;
                if (requestBadgeText != null)
                {
                    requestBadgeText.text = count > 0 ? count.ToString() : "";
                }
            }
        }

        private void OnNewRequest(CoopRequest request)
        {
            // Refresh if panel is open
            if (requestPanelOpen)
                RefreshRequestList();
        }

        private void OnRequestUpdate(CoopRequest request)
        {
            if (requestPanelOpen)
                RefreshRequestList();
        }
    }
}
