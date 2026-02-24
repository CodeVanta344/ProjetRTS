using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Campaign;
using NapoleonicWars.Data;
using NapoleonicWars.Network;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// Total War-style bottom bar: faction emblem, end turn, minimap, event feed.
    /// </summary>
    public class BottomBar : MonoBehaviour
    {
        private CampaignManager campaignManager;
        private FactionData playerFaction;
        private Text factionNameText, eventText, turnInfoText;
        private Transform eventContainer;

        // Co-op ready state display
        private GameObject coopReadyPanel;
        private Dictionary<CoopRole, Text> coopReadyTexts = new Dictionary<CoopRole, Text>();
        private Text endTurnBtnText;
        private bool localReady = false;

        public System.Action OnEndTurnClicked;

        public void Initialize(CampaignManager manager)
        {
            campaignManager = manager;
            playerFaction = manager.GetPlayerFaction();
            BuildBar();
        }

        private void BuildBar()
        {
            // === BAR BACKGROUND (60px, bottom) ===
            RectTransform barRT = UIFactory.CreatePanel(transform, "BottomBarBg",
                new Color(0.10f, 0.11f, 0.10f, 0.97f));
            barRT.anchorMin = new Vector2(0, 0);
            barRT.anchorMax = new Vector2(1, 0);
            barRT.offsetMin = new Vector2(50, 0); // Right of nav bar
            barRT.offsetMax = new Vector2(0, 55);

            // Gold border top
            RectTransform border = UIFactory.CreatePanel(barRT, "TopBorder", UIFactory.BorderGold);
            border.anchorMin = new Vector2(0, 1);
            border.anchorMax = new Vector2(1, 1);
            border.offsetMin = new Vector2(0, -2);
            border.offsetMax = Vector2.zero;
            border.GetComponent<Image>().raycastTarget = false;

            // === LEFT: Faction Emblem + Name ===
            RectTransform factionPanel = UIFactory.CreatePanel(barRT, "FactionPanel", new Color(0, 0, 0, 0));
            factionPanel.anchorMin = new Vector2(0, 0);
            factionPanel.anchorMax = new Vector2(0, 1);
            factionPanel.offsetMin = new Vector2(8, 4);
            factionPanel.offsetMax = new Vector2(220, -4);
            HorizontalLayoutGroup fhlg = UIFactory.AddHorizontalLayout(factionPanel.gameObject, 8f, new RectOffset(5, 5, 2, 2));
            fhlg.childControlWidth = false;
            fhlg.childControlHeight = true;

            // Faction shield/emblem
            string emblem = GetFactionEmblem();
            Text emblemText = UIFactory.CreateText(factionPanel, "Emblem", emblem, 28, TextAnchor.MiddleCenter, UIFactory.GoldAccent);
            UIFactory.AddLayoutElement(emblemText.gameObject, preferredWidth: 40, preferredHeight: 44);

            // Faction name + type
            GameObject nameCol = new GameObject("NameCol");
            nameCol.transform.SetParent(factionPanel, false);
            nameCol.AddComponent<RectTransform>();
            VerticalLayoutGroup nvlg = UIFactory.AddVerticalLayout(nameCol, 0f);
            nvlg.childControlWidth = true;
            nvlg.childControlHeight = false;
            UIFactory.AddLayoutElement(nameCol, preferredWidth: 150, preferredHeight: 44);

            factionNameText = UIFactory.CreateText(nameCol.transform, "FName",
                playerFaction != null ? playerFaction.factionType.ToString().ToUpper() : "FRANCE",
                16, TextAnchor.MiddleLeft, UIFactory.GoldAccent);
            factionNameText.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(factionNameText.gameObject, preferredHeight: 22);

            string subtitle = playerFaction != null 
                ? CampaignManager.GetFactionSubtitle(playerFaction.factionType) 
                : "Empire Français";
            Text subText = UIFactory.CreateText(nameCol.transform, "FSub", subtitle,
                10, TextAnchor.MiddleLeft, UIFactory.TextGrey);
            UIFactory.AddLayoutElement(subText.gameObject, preferredHeight: 18);

            // === CENTER: Event feed (scrolling notifications) ===
            RectTransform eventPanel = UIFactory.CreatePanel(barRT, "EventPanel",
                new Color(0.12f, 0.13f, 0.11f, 0.8f));
            eventPanel.anchorMin = new Vector2(0, 0);
            eventPanel.anchorMax = new Vector2(1, 1);
            eventPanel.offsetMin = new Vector2(230, 4);
            eventPanel.offsetMax = new Vector2(-350, -4);

            RectTransform eventBorder = UIFactory.CreatePanel(eventPanel, "EvBorder",
                new Color(UIFactory.BorderGold.r, UIFactory.BorderGold.g, UIFactory.BorderGold.b, 0.3f));
            eventBorder.anchorMin = Vector2.zero;
            eventBorder.anchorMax = Vector2.one;
            eventBorder.offsetMin = Vector2.zero;
            eventBorder.offsetMax = Vector2.zero;
            eventBorder.GetComponent<Image>().raycastTarget = false;

            eventText = UIFactory.CreateText(eventPanel, "EventText",
                "  📜 Bienvenue, Votre Majesté. L'Europe attend vos ordres.",
                12, TextAnchor.MiddleLeft, UIFactory.ParchmentBeige);
            eventText.fontStyle = FontStyle.Italic;
            RectTransform etRT = eventText.GetComponent<RectTransform>();
            etRT.anchorMin = Vector2.zero;
            etRT.anchorMax = Vector2.one;
            etRT.offsetMin = new Vector2(10, 0);
            etRT.offsetMax = new Vector2(-10, 0);

            // === RIGHT: Turn info + End Turn Button ===
            RectTransform rightPanel = UIFactory.CreatePanel(barRT, "RightPanel", new Color(0, 0, 0, 0));
            rightPanel.anchorMin = new Vector2(1, 0);
            rightPanel.anchorMax = new Vector2(1, 1);
            rightPanel.offsetMin = new Vector2(-340, 4);
            rightPanel.offsetMax = new Vector2(-8, -4);
            HorizontalLayoutGroup rhlg = UIFactory.AddHorizontalLayout(rightPanel.gameObject, 8f);
            rhlg.childControlWidth = false;
            rhlg.childControlHeight = true;
            rhlg.childAlignment = TextAnchor.MiddleRight;

            // Turn info
            turnInfoText = UIFactory.CreateText(rightPanel, "TurnInfo", "Tour 1 — Jan 1805",
                13, TextAnchor.MiddleRight, UIFactory.ParchmentBeige);
            UIFactory.AddLayoutElement(turnInfoText.gameObject, preferredWidth: 160, preferredHeight: 40);

            // End Turn button (Total War style — big, prominent)
            Button endTurnBtn = UIFactory.CreateWarhammerButton(rightPanel, "EndTurn",
                "FIN DU TOUR ▶", 15, () => OnEndTurnButtonPressed());
            UIFactory.AddLayoutElement(endTurnBtn.gameObject, preferredWidth: 160, preferredHeight: 42);
            endTurnBtnText = endTurnBtn.GetComponentInChildren<Text>();

            // Co-op ready badges (only visible in co-op mode)
            BuildCoopReadyPanel(barRT);
        }

        private void BuildCoopReadyPanel(Transform parent)
        {
            coopReadyPanel = new GameObject("CoopReadyPanel");
            coopReadyPanel.transform.SetParent(parent, false);
            RectTransform rt = coopReadyPanel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(-510, 6);
            rt.offsetMax = new Vector2(-345, -6);

            HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(coopReadyPanel, 4f);
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childAlignment = TextAnchor.MiddleCenter;

            // Create badge for each possible role
            foreach (CoopRole role in new[] { CoopRole.Marshal, CoopRole.Intendant, CoopRole.Chancellor, CoopRole.GrandVizir })
            {
                Text badge = UIFactory.CreateText(coopReadyPanel.transform, $"Ready_{role}",
                    $"{CoopRoleManager.GetRoleIcon(role)} ⏳", 11, TextAnchor.MiddleCenter, UIFactory.TextGrey);
                UIFactory.AddLayoutElement(badge.gameObject, preferredWidth: 50, preferredHeight: 30);
                coopReadyTexts[role] = badge;
                badge.gameObject.SetActive(false);
            }

            coopReadyPanel.SetActive(false);
        }

        private void OnEndTurnButtonPressed()
        {
            if (CoopRoleManager.Instance != null && CoopRoleManager.Instance.IsCoopMode)
            {
                // Co-op mode: toggle local ready state
                localReady = !localReady;
                CoopRoleManager.Instance.SetReadyServerRpc(localReady);

                if (endTurnBtnText != null)
                    endTurnBtnText.text = localReady ? "✓ PRÊT" : "FIN DU TOUR ▶";
            }
            else
            {
                // Single player: immediate end turn
                OnEndTurnClicked?.Invoke();
            }
        }

        private string GetFactionEmblem()
        {
            if (playerFaction == null) return "⚜️";
            return playerFaction.factionType switch
            {
                FactionType.France => "⚜️",
                FactionType.Britain => "🦁",
                FactionType.Prussia => "🦅",
                FactionType.Russia => "🐻",
                FactionType.Austria => "👑",
                FactionType.Spain => "🏰",
                FactionType.Ottoman => "☪️",
                _ => "⚔️"
            };
        }

        public void UpdateDisplay()
        {
            if (campaignManager == null) return;

            int turn = campaignManager.CurrentTurn;
            int month = (turn % 12) + 1;
            int year = 1805 + turn / 12;
            string[] months = { "Jan", "Fév", "Mar", "Avr", "Mai", "Jun",
                                "Jul", "Aoû", "Sep", "Oct", "Nov", "Déc" };
            turnInfoText.text = $"Tour {turn} — {months[month - 1]} {year}";

            // Show latest faction event in the event feed
            if (playerFaction != null)
            {
                string latestEvent = campaignManager.GetLatestFactionEvent(playerFaction.factionType);
                if (!string.IsNullOrEmpty(latestEvent))
                    eventText.text = $"  📜 {latestEvent}";
            }

            // Update co-op ready state display
            UpdateCoopReadyDisplay();
        }

        private void UpdateCoopReadyDisplay()
        {
            if (CoopRoleManager.Instance == null || !CoopRoleManager.Instance.IsCoopMode)
            {
                if (coopReadyPanel != null) coopReadyPanel.SetActive(false);
                return;
            }

            if (coopReadyPanel != null) coopReadyPanel.SetActive(true);

            var players = CoopRoleManager.Instance.GetAllPlayers();
            int maxPlayers = CoopRoleManager.Instance.MaxCoopPlayers;
            bool is2Player = maxPlayers == 2;

            // Show/hide role badges based on mode
            foreach (var kvp in coopReadyTexts)
            {
                CoopRole role = kvp.Key;
                Text badge = kvp.Value;

                bool visible = false;
                if (is2Player && (role == CoopRole.Marshal || role == CoopRole.GrandVizir))
                    visible = true;
                if (!is2Player && role != CoopRole.GrandVizir)
                    visible = true;

                badge.gameObject.SetActive(visible);
            }

            // Update badges with ready state
            foreach (var player in players)
            {
                if (coopReadyTexts.TryGetValue(player.role, out Text badge))
                {
                    string icon = CoopRoleManager.GetRoleIcon(player.role);
                    if (!player.isConnected)
                    {
                        badge.text = $"{icon} 🔴";
                        badge.color = new Color(0.5f, 0.3f, 0.3f);
                    }
                    else if (player.isReady)
                    {
                        badge.text = $"{icon} ✓";
                        badge.color = new Color(0.3f, 1f, 0.3f);
                    }
                    else
                    {
                        badge.text = $"{icon} ⏳";
                        badge.color = UIFactory.TextGrey;
                    }
                }
            }

            // Reset local ready state on new turn
            if (localReady && endTurnBtnText != null && endTurnBtnText.text == "✓ PRÊT")
            {
                // Will be reset by CoopRoleManager.ResetAllReady on turn start
            }
        }

        public void ShowEvent(string message)
        {
            if (eventText != null)
                eventText.text = $"  📜 {message}";
        }
    }
}
