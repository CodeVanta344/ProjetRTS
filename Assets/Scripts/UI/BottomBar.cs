using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Campaign;
using NapoleonicWars.Data;
using NapoleonicWars.Network;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// Premium bottom bar with gradient background, faction badge, glassmorphism event feed,
    /// styled speed controls, and enhanced tick progress.
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
            // Update tick progress bar every frame
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
            // === GRADIENT BACKGROUND ===
            RectTransform barRT = UIFactory.CreateGradientPanel(transform, "BottomBarBg",
                UIFactory.GradientBottom, UIFactory.GradientTop, UIFactory.InnerGlow);
            barRT.anchorMin = new Vector2(0, 0);
            barRT.anchorMax = new Vector2(1, 0);
            barRT.offsetMin = new Vector2(74, 0); // Right of nav bar (matches wider nav)
            barRT.offsetMax = new Vector2(0, 60); // Slightly taller

            // === TOP EDGE GLOW ===
            RectTransform topGlow = UIFactory.CreateGlowSeparator(barRT.transform, "TopGlow", false,
                new Color(0.85f, 0.72f, 0.35f, 0.3f), UIFactory.EmpireGold);
            UIFactory.SetAnchors(topGlow.gameObject, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -3), Vector2.zero);
            topGlow.GetComponent<Image>().raycastTarget = false;

            // === TOP SHADOW (depth above the bar) ===
            RectTransform topShadow = UIFactory.CreatePanel(barRT.transform, "TopShadow",
                new Color(0, 0, 0, 0.25f));
            UIFactory.SetAnchors(topShadow.gameObject, new Vector2(0, 1), new Vector2(1, 1),
                Vector2.zero, new Vector2(0, 4));
            topShadow.GetComponent<Image>().raycastTarget = false;

            // === LEFT: Faction Emblem Badge ===
            RectTransform factionPanel = UIFactory.CreatePanel(barRT, "FactionPanel", new Color(0, 0, 0, 0));
            factionPanel.anchorMin = new Vector2(0, 0);
            factionPanel.anchorMax = new Vector2(0, 1);
            factionPanel.offsetMin = new Vector2(8, 4);
            factionPanel.offsetMax = new Vector2(230, -4);
            HorizontalLayoutGroup fhlg = UIFactory.AddHorizontalLayout(factionPanel.gameObject, 10f, new RectOffset(5, 5, 2, 2));
            fhlg.childControlWidth = false;
            fhlg.childControlHeight = true;

            // Faction shield badge (colored background behind emblem)
            string emblem = GetFactionEmblem();
            Color factionColor = GetFactionBadgeColor();
            
            GameObject shieldBadge = new GameObject("ShieldBadge");
            shieldBadge.transform.SetParent(factionPanel, false);
            RectTransform shieldRT = shieldBadge.AddComponent<RectTransform>();
            UIFactory.AddLayoutElement(shieldBadge, preferredWidth: 48, preferredHeight: 48);
            
            // Shield outer border
            Image shieldBorder = shieldBadge.AddComponent<Image>();
            shieldBorder.color = new Color(factionColor.r * 0.7f, factionColor.g * 0.7f, factionColor.b * 0.7f, 0.7f);
            
            // Shield inner fill
            GameObject shieldInner = new GameObject("Inner");
            shieldInner.transform.SetParent(shieldBadge.transform, false);
            RectTransform innerRT = shieldInner.AddComponent<RectTransform>();
            innerRT.anchorMin = Vector2.zero;
            innerRT.anchorMax = Vector2.one;
            innerRT.offsetMin = new Vector2(2, 2);
            innerRT.offsetMax = new Vector2(-2, -2);
            Image innerImg = shieldInner.AddComponent<Image>();
            innerImg.color = new Color(factionColor.r * 0.2f, factionColor.g * 0.2f, factionColor.b * 0.2f, 0.9f);
            
            // Emblem text on shield
            Text emblemText = UIFactory.CreateText(shieldInner.transform, "Emblem", emblem, 28, TextAnchor.MiddleCenter, factionColor);
            UIFactory.SetAnchors(emblemText.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Faction name + subtitle column
            GameObject nameCol = new GameObject("NameCol");
            nameCol.transform.SetParent(factionPanel, false);
            nameCol.AddComponent<RectTransform>();
            VerticalLayoutGroup nvlg = UIFactory.AddVerticalLayout(nameCol, -4f);
            nvlg.childControlWidth = true;
            nvlg.childControlHeight = false;
            UIFactory.AddLayoutElement(nameCol, preferredWidth: 160, preferredHeight: 48);

            factionNameText = UIFactory.CreateText(nameCol.transform, "FName",
                playerFaction != null ? playerFaction.factionType.ToString().ToUpper() : "FRANCE",
                16, TextAnchor.MiddleLeft, UIFactory.EmpireGold);
            factionNameText.fontStyle = FontStyle.Bold;
            Shadow nameShadow = factionNameText.gameObject.AddComponent<Shadow>();
            nameShadow.effectColor = new Color(0, 0, 0, 0.6f);
            nameShadow.effectDistance = new Vector2(1f, -1f);
            UIFactory.AddLayoutElement(factionNameText.gameObject, preferredHeight: 24);

            string subtitle = playerFaction != null 
                ? CampaignManager.GetFactionSubtitle(playerFaction.factionType) 
                : "Empire Français";
            Text subText = UIFactory.CreateText(nameCol.transform, "FSub", subtitle.ToUpper(),
                9, TextAnchor.MiddleLeft, UIFactory.SilverText);
            subText.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(subText.gameObject, preferredHeight: 16);

            // === CENTER: Glassmorphism Event Feed ===
            RectTransform eventPanel = UIFactory.CreatePanel(barRT, "EventPanel",
                new Color(0.06f, 0.065f, 0.07f, 0.8f));
            eventPanel.anchorMin = new Vector2(0, 0);
            eventPanel.anchorMax = new Vector2(1, 1);
            eventPanel.offsetMin = new Vector2(248, 8);
            eventPanel.offsetMax = new Vector2(-440, -8);

            // Glass border (gold inner outline)
            Outline eventOutline = eventPanel.gameObject.AddComponent<Outline>();
            eventOutline.effectColor = new Color(UIFactory.EmpireGold.r, UIFactory.EmpireGold.g, UIFactory.EmpireGold.b, 0.35f);
            eventOutline.effectDistance = new Vector2(1f, 1f);

            // Inner glow overlay (top strip)
            RectTransform glassGlow = UIFactory.CreatePanel(eventPanel, "GlassGlow",
                new Color(0.77f, 0.63f, 0.35f, 0.04f));
            UIFactory.SetAnchors(glassGlow.gameObject, new Vector2(0, 0.5f), Vector2.one,
                new Vector2(2, 0), new Vector2(-2, -2));
            glassGlow.GetComponent<Image>().raycastTarget = false;

            eventText = UIFactory.CreateText(eventPanel, "EventText",
                "  📜 Bienvenue, Votre Majesté. L'Europe attend vos ordres.",
                12, TextAnchor.MiddleLeft, UIFactory.Parchment);
            eventText.fontStyle = FontStyle.Italic;
            UIFactory.SetAnchors(eventText.gameObject, Vector2.zero, Vector2.one, new Vector2(14, 0), new Vector2(-14, 0));

            // === RIGHT: Date + Speed Controls ===
            RectTransform rightPanel = UIFactory.CreatePanel(barRT, "RightPanel", new Color(0, 0, 0, 0));
            rightPanel.anchorMin = new Vector2(1, 0);
            rightPanel.anchorMax = new Vector2(1, 1);
            rightPanel.offsetMin = new Vector2(-420, 2);
            rightPanel.offsetMax = new Vector2(-8, -2);
            HorizontalLayoutGroup rhlg = UIFactory.AddHorizontalLayout(rightPanel.gameObject, 6f);
            rhlg.childControlWidth = false;
            rhlg.childControlHeight = true;
            rhlg.childAlignment = TextAnchor.MiddleRight;

            // Date/Season display with shadow
            turnInfoText = UIFactory.CreateText(rightPanel, "DateInfo", "🌱 Printemps 1700",
                13, TextAnchor.MiddleRight, UIFactory.ParchmentBeige);
            turnInfoText.fontStyle = FontStyle.Bold;
            Shadow dateShadow = turnInfoText.gameObject.AddComponent<Shadow>();
            dateShadow.effectColor = new Color(0, 0, 0, 0.5f);
            dateShadow.effectDistance = new Vector2(1f, -1f);
            UIFactory.AddLayoutElement(turnInfoText.gameObject, preferredWidth: 165, preferredHeight: 44);

            // Tick progress bar (wider, with glow)
            GameObject progressContainer = new GameObject("TickProgress");
            progressContainer.transform.SetParent(rightPanel, false);
            RectTransform pcRT = progressContainer.AddComponent<RectTransform>();
            UIFactory.AddLayoutElement(progressContainer, preferredWidth: 10, preferredHeight: 44);
            
            // BG with border
            Image progressBg = progressContainer.AddComponent<Image>();
            progressBg.color = new Color(0.12f, 0.12f, 0.13f, 0.9f);
            Outline progOutline = progressContainer.AddComponent<Outline>();
            progOutline.effectColor = new Color(UIFactory.MutedGold.r, UIFactory.MutedGold.g, UIFactory.MutedGold.b, 0.4f);
            progOutline.effectDistance = new Vector2(1f, 1f);
            
            // Fill (child, fills bottom-to-top)
            GameObject fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(progressContainer.transform, false);
            RectTransform fillRT = fillGO.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = new Vector2(1, 1);
            fillRT.offsetMax = new Vector2(-1, -1);
            progressFill = fillGO.AddComponent<Image>();
            progressFill.color = new Color(0.85f, 0.7f, 0.2f, 0.9f);
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Vertical;
            progressFill.fillOrigin = 0; // bottom
            progressFill.fillAmount = 0f;

            // === SPEED CONTROL BUTTONS (styled) ===
            // Glow separator before speed controls
            RectTransform speedSep = UIFactory.CreateGlowSeparator(rightPanel, "SpeedSep", true);
            UIFactory.AddLayoutElement(speedSep.gameObject, preferredWidth: 6, preferredHeight: 44);

            // Pause button (premium)
            pauseBtn = CreateStyledSpeedButton(rightPanel, "PauseBtn", "⏸", 18, true, () => OnPausePressed());
            UIFactory.AddLayoutElement(pauseBtn.gameObject, preferredWidth: 48, preferredHeight: 48);
            pauseBtnText = pauseBtn.GetComponentInChildren<Text>();

            // Speed buttons (styled)
            speed1Btn = CreateStyledSpeedButton(rightPanel, "Speed1", "1×", 13, false, () => SetGameSpeed(1));
            UIFactory.AddLayoutElement(speed1Btn.gameObject, preferredWidth: 44, preferredHeight: 42);

            speed2Btn = CreateStyledSpeedButton(rightPanel, "Speed2", "2×", 13, false, () => SetGameSpeed(2));
            UIFactory.AddLayoutElement(speed2Btn.gameObject, preferredWidth: 44, preferredHeight: 42);

            speed5Btn = CreateStyledSpeedButton(rightPanel, "Speed5", "5×", 13, false, () => SetGameSpeed(5));
            UIFactory.AddLayoutElement(speed5Btn.gameObject, preferredWidth: 44, preferredHeight: 42);

            // Speed label with glow
            speedLabel = UIFactory.CreateText(rightPanel, "SpeedLbl", "⏸",
                12, TextAnchor.MiddleCenter, UIFactory.SilverText);
            Shadow speedShadow = speedLabel.gameObject.AddComponent<Shadow>();
            speedShadow.effectColor = new Color(0, 0, 0, 0.5f);
            UIFactory.AddLayoutElement(speedLabel.gameObject, preferredWidth: 34, preferredHeight: 44);

            // Subscribe to speed changes
            if (CampaignClock.Instance != null)
            {
                CampaignClock.Instance.OnSpeedChanged += OnSpeedChanged;
                OnSpeedChanged(CampaignClock.Instance.Speed);
            }

            // Co-op ready badges
            BuildCoopReadyPanel(barRT);
        }

        /// <summary>Creates a styled speed control button with inset visual.</summary>
        private Button CreateStyledSpeedButton(Transform parent, string name, string label, int fontSize, bool isPrimary, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();

            Image img = go.AddComponent<Image>();
            img.color = isPrimary 
                ? new Color(UIFactory.ImperialCrimson.r * 0.7f, UIFactory.ImperialCrimson.g * 0.7f, UIFactory.ImperialCrimson.b * 0.7f) 
                : new Color(0.14f, 0.14f, 0.15f);

            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = isPrimary 
                ? new Color(UIFactory.EmpireGold.r, UIFactory.EmpireGold.g, UIFactory.EmpireGold.b, 0.6f)
                : new Color(0.3f, 0.28f, 0.24f, 0.5f);
            outline.effectDistance = new Vector2(1f, 1f);

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.3f, 1.2f, 1.1f);
            cb.pressedColor = UIFactory.EmpireGold;
            cb.selectedColor = Color.white;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            if (onClick != null)
                btn.onClick.AddListener(onClick);

            Text t = UIFactory.CreateText(go.transform, "Label", label, fontSize, TextAnchor.MiddleCenter, UIFactory.Porcelain);
            t.fontStyle = FontStyle.Bold;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            UIFactory.SetAnchors(t.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Shadow textShadow = t.gameObject.AddComponent<Shadow>();
            textShadow.effectColor = new Color(0, 0, 0, 0.5f);
            textShadow.effectDistance = new Vector2(1f, -1f);

            return btn;
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
                localReady = !localReady;
                CoopRoleManager.Instance.SetReadyServerRpc(localReady);
                if (pauseBtnText != null)
                    pauseBtnText.text = localReady ? "✓" : "⏸";
            }
            else
            {
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
            HighlightSpeedButton(speed1Btn, newSpeed == 1);
            HighlightSpeedButton(speed2Btn, newSpeed == 2);
            HighlightSpeedButton(speed5Btn, newSpeed == 5);

            if (pauseBtnText != null)
                pauseBtnText.text = newSpeed == 0 ? "▶" : "⏸";

            if (speedLabel != null)
            {
                speedLabel.text = newSpeed == 0 ? "⏸" : $"{newSpeed}×";
                speedLabel.color = newSpeed == 0 ? new Color(1f, 0.5f, 0.3f) : UIFactory.GoldAccent;
            }
        }

        private void HighlightSpeedButton(Button btn, bool active)
        {
            if (btn == null) return;
            
            Image img = btn.GetComponent<Image>();
            if (img != null)
                img.color = active ? new Color(0.35f, 0.30f, 0.15f) : new Color(0.14f, 0.14f, 0.15f);

            Outline outline = btn.GetComponent<Outline>();
            if (outline != null)
                outline.effectColor = active 
                    ? new Color(UIFactory.EmpireGold.r, UIFactory.EmpireGold.g, UIFactory.EmpireGold.b, 0.7f) 
                    : new Color(0.3f, 0.28f, 0.24f, 0.5f);

            Text txt = btn.GetComponentInChildren<Text>();
            if (txt != null)
                txt.color = active ? UIFactory.GoldAccent : UIFactory.TextGrey;
        }

        private Color GetFactionBadgeColor()
        {
            if (playerFaction == null) return UIFactory.EmpireGold;
            return playerFaction.factionType switch
            {
                FactionType.France => new Color(0.3f, 0.4f, 0.85f),
                FactionType.Britain => new Color(0.85f, 0.25f, 0.25f),
                FactionType.Prussia => new Color(0.2f, 0.2f, 0.3f),
                FactionType.Russia => new Color(0.4f, 0.7f, 0.4f),
                FactionType.Austria => new Color(0.85f, 0.75f, 0.3f),
                FactionType.Spain => new Color(0.85f, 0.6f, 0.15f),
                FactionType.Ottoman => new Color(0.85f, 0.3f, 0.3f),
                _ => UIFactory.EmpireGold
            };
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

            string seasonIcon = SeasonSystem.GetSeasonIcon();
            string dateLine = $"{seasonIcon} {SeasonSystem.DateString}";
            
            if (CampaignClock.Instance != null && CampaignClock.Instance.IsPaused)
                dateLine += "  ⏸";

            if (turnInfoText != null)
                turnInfoText.text = dateLine;

            if (playerFaction != null)
            {
                string latestEvent = campaignManager.GetLatestFactionEvent(playerFaction.factionType);
                if (!string.IsNullOrEmpty(latestEvent))
                    eventText.text = $"  📜 {latestEvent}";
            }

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
