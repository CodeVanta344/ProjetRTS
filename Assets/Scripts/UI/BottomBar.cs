using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Campaign;
using NapoleonicWars.Data;
using NapoleonicWars.Network;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// Bottom bar with faction info, event feed, and real-time speed controls.
    /// Replaces the old "End Turn" button with Pause / 1× / 2× / 5× controls.
    /// </summary>
    public class BottomBar : MonoBehaviour
    {
        private CampaignManager campaignManager;
        private FactionData playerFaction;
        private Text factionNameText, eventText, turnInfoText;
        private Transform eventContainer;

        // Speed controls
        private Button pauseBtn;
        private Button speed1Btn, speed2Btn, speed5Btn;
        private Text pauseBtnText;
        private Image progressFill;
        private Text speedLabel;

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

        private void Update()
        {
            // Update tick progress bar every frame — shows season progress (90 days)
            if (CampaignClock.Instance != null && progressFill != null)
            {
                float p = CampaignClock.Instance.SeasonProgress;
                progressFill.fillAmount = p;
                
                // Color shifts from gold to green as season approaches end
                progressFill.color = Color.Lerp(
                    new Color(0.85f, 0.7f, 0.2f, 0.9f),
                    new Color(0.4f, 0.8f, 0.3f, 0.9f),
                    p);
            }
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
            eventPanel.offsetMax = new Vector2(-420, -4);

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

            // === RIGHT: Date + Speed Controls ===
            RectTransform rightPanel = UIFactory.CreatePanel(barRT, "RightPanel", new Color(0, 0, 0, 0));
            rightPanel.anchorMin = new Vector2(1, 0);
            rightPanel.anchorMax = new Vector2(1, 1);
            rightPanel.offsetMin = new Vector2(-410, 2);
            rightPanel.offsetMax = new Vector2(-8, -2);
            HorizontalLayoutGroup rhlg = UIFactory.AddHorizontalLayout(rightPanel.gameObject, 6f);
            rhlg.childControlWidth = false;
            rhlg.childControlHeight = true;
            rhlg.childAlignment = TextAnchor.MiddleRight;

            // Date/Season display
            turnInfoText = UIFactory.CreateText(rightPanel, "DateInfo", "🌱 Printemps 1700",
                13, TextAnchor.MiddleRight, UIFactory.ParchmentBeige);
            UIFactory.AddLayoutElement(turnInfoText.gameObject, preferredWidth: 160, preferredHeight: 42);

            // Tick progress bar
            GameObject progressContainer = new GameObject("TickProgress");
            progressContainer.transform.SetParent(rightPanel, false);
            RectTransform pcRT = progressContainer.AddComponent<RectTransform>();
            UIFactory.AddLayoutElement(progressContainer, preferredWidth: 6, preferredHeight: 42);
            
            // BG
            Image progressBg = progressContainer.AddComponent<Image>();
            progressBg.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            
            // Fill (child, fills bottom-to-top)
            GameObject fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(progressContainer.transform, false);
            RectTransform fillRT = fillGO.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = new Vector2(1f, 0f);
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            progressFill = fillGO.AddComponent<Image>();
            progressFill.color = new Color(0.85f, 0.7f, 0.2f, 0.9f);
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Vertical;
            progressFill.fillOrigin = 0; // bottom
            progressFill.fillAmount = 0f;
            // Make fill cover the full container
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;

            // === SPEED CONTROL BUTTONS ===
            // Pause button
            pauseBtn = UIFactory.CreateWarhammerButton(rightPanel, "PauseBtn", "⏸", 16, () => OnPausePressed());
            UIFactory.AddLayoutElement(pauseBtn.gameObject, preferredWidth: 42, preferredHeight: 42);
            pauseBtnText = pauseBtn.GetComponentInChildren<Text>();

            // Speed 1×
            speed1Btn = UIFactory.CreateGoldButton(rightPanel, "Speed1", "1×", 13, () => SetGameSpeed(1));
            UIFactory.AddLayoutElement(speed1Btn.gameObject, preferredWidth: 40, preferredHeight: 38);

            // Speed 2×
            speed2Btn = UIFactory.CreateGoldButton(rightPanel, "Speed2", "2×", 13, () => SetGameSpeed(2));
            UIFactory.AddLayoutElement(speed2Btn.gameObject, preferredWidth: 40, preferredHeight: 38);

            // Speed 5×
            speed5Btn = UIFactory.CreateGoldButton(rightPanel, "Speed5", "5×", 13, () => SetGameSpeed(5));
            UIFactory.AddLayoutElement(speed5Btn.gameObject, preferredWidth: 40, preferredHeight: 38);

            // Speed label
            speedLabel = UIFactory.CreateText(rightPanel, "SpeedLbl", "⏸",
                11, TextAnchor.MiddleCenter, UIFactory.TextGrey);
            UIFactory.AddLayoutElement(speedLabel.gameObject, preferredWidth: 30, preferredHeight: 42);

            // Subscribe to speed changes
            if (CampaignClock.Instance != null)
            {
                CampaignClock.Instance.OnSpeedChanged += OnSpeedChanged;
                OnSpeedChanged(CampaignClock.Instance.Speed);
            }

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
            rt.offsetMin = new Vector2(-580, 6);
            rt.offsetMax = new Vector2(-415, -6);

            HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(coopReadyPanel, 4f);
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childAlignment = TextAnchor.MiddleCenter;

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

        // ── Speed Controls ──────────────────────────────────────

        private void OnPausePressed()
        {
            if (CoopRoleManager.Instance != null && CoopRoleManager.Instance.IsCoopMode)
            {
                // Co-op mode: toggle local ready state (legacy behavior)
                localReady = !localReady;
                CoopRoleManager.Instance.SetReadyServerRpc(localReady);
                if (pauseBtnText != null)
                    pauseBtnText.text = localReady ? "✓" : "⏸";
            }
            else
            {
                // Single player: toggle pause
                if (CampaignClock.Instance != null)
                    CampaignClock.Instance.TogglePause();
            }
        }

        private void SetGameSpeed(int speed)
        {
            if (CampaignClock.Instance != null)
                CampaignClock.Instance.SetSpeed(speed);
        }

        private void OnSpeedChanged(int newSpeed)
        {
            // Update button highlights
            HighlightButton(speed1Btn, newSpeed == 1);
            HighlightButton(speed2Btn, newSpeed == 2);
            HighlightButton(speed5Btn, newSpeed == 5);

            // Update pause button
            if (pauseBtnText != null)
                pauseBtnText.text = newSpeed == 0 ? "▶" : "⏸";

            // Speed label
            if (speedLabel != null)
            {
                speedLabel.text = newSpeed == 0 ? "⏸" : $"{newSpeed}×";
                speedLabel.color = newSpeed == 0 ? new Color(1f, 0.5f, 0.3f) : UIFactory.GoldAccent;
            }
        }

        private void HighlightButton(Button btn, bool active)
        {
            if (btn == null) return;
            var colors = btn.colors;
            colors.normalColor = active ? new Color(0.6f, 0.5f, 0.2f) : new Color(0.25f, 0.22f, 0.18f);
            btn.colors = colors;

            Text txt = btn.GetComponentInChildren<Text>();
            if (txt != null)
                txt.color = active ? UIFactory.GoldAccent : UIFactory.TextGrey;
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

            // Date display — full date with day
            string seasonIcon = SeasonSystem.GetSeasonIcon();
            string dateLine = $"{seasonIcon} {SeasonSystem.DateString}";
            
            // Add speed indicator when paused
            if (CampaignClock.Instance != null && CampaignClock.Instance.IsPaused)
                dateLine += "  ⏸";

            if (turnInfoText != null)
                turnInfoText.text = dateLine;

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
        }

        public void ShowEvent(string message)
        {
            if (eventText != null)
                eventText.text = $"  📜 {message}";
        }
    }
}
