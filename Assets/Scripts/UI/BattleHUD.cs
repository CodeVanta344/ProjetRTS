using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Core;
using NapoleonicWars.Data;
using NapoleonicWars.Units;
using System.Collections.Generic;

namespace NapoleonicWars.UI
{
    public class BattleHUD : MonoBehaviour
    {
        private Canvas canvas;

        // Top bar
        private Text playerCountText;
        private Text titleText;
        private Text enemyCountText;

        // Selection panel removed

        // Formation buttons removed (replaced by FormationUIManager)

        // Command buttons
        private GameObject commandPanel;
        private Button btnVolley, btnCharge, btnRally, btnCover, btnLimber;
        private Text volleyLabel, chargeLabel, rallyLabel, coverLabel, limberLabel;
        private GameObject volleyGO, chargeGO, rallyGO, coverGO, limberGO;
        private float rallyCooldown;

        // Deployment panel
        private GameObject deploymentPanel;
        private Button btnStartBattle, btnReady;
        private Text deploymentStatusText;

        // Battle result overlay
        private GameObject resultOverlay;
        private Text resultText;

        // Controls help removed

        // Speed controls
        private Text speedText;
        private Text timerText;
        private float currentSpeed = 1f;

        // Unit cards panel (Warhammer style)
        private GameObject unitCardsPanel;
        private CanvasGroup unitCardsCanvasGroup;
        private Transform unitCardsContainer;
        private List<GameObject> unitCardObjects = new List<GameObject>();
        private List<Regiment> playerRegiments = new List<Regiment>();
        private float unitCardsTimer;
        private int lastRegimentCount = -1;

        // Throttle timers — these panels show slow-changing data, no need for 60fps updates
        private float topBarTimer;
        private float selectionTimer;

        private void Start()
        {
            BuildUI();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // Throttle top bar — updates army counts, changes slowly
            topBarTimer -= dt;
            if (topBarTimer <= 0f)
            {
                topBarTimer = 0.5f;
                UpdateTopBar();
            }

            // UpdateSelectionPanel removed

            // Default formation panel removed
            UpdateCommandButtons();
            UpdateDeploymentPanel();
            UpdateBattleResult();
            UpdateSpeedControls();

            // Throttle unit cards update - increase interval to reduce flickering
            unitCardsTimer -= dt;
            if (unitCardsTimer <= 0f)
            {
                unitCardsTimer = 1.0f; // Update once per second instead of 0.3s
                UpdateUnitCards();
            }
        }

        private void BuildUI()
        {
            canvas = UIFactory.CreateCanvas("BattleHUDCanvas", 10);
            canvas.transform.SetParent(transform);

            BuildTopBar();
            // BuildSelectionPanel(); removed
            // BuildFormationPanel(); removed
            BuildCommandPanel();
            BuildDeploymentPanel();
            BuildBattleResult();
            // BuildControlsHelp(); removed
            BuildSpeedControls();
            BuildUnitCardsPanel();
        }

        // ===================== TOP BAR =====================
        private void BuildTopBar()
        {
            // Bottom Bar Surface
            RectTransform bar = UIFactory.CreatePanel(canvas.transform, "TopBar", UIFactory.DeepCharcoal);
            bar.anchorMin = new Vector2(0.5f, 1);
            bar.anchorMax = new Vector2(0.5f, 1);
            bar.pivot = new Vector2(0.5f, 1);
            bar.sizeDelta = new Vector2(800, 44);
            bar.anchoredPosition = new Vector2(0, -6);

            // Gold Border Shadow (Top & Bottom)
            RectTransform borderBot = UIFactory.CreatePanel(bar, "BorderBot", UIFactory.EmpireGold);
            UIFactory.SetAnchors(borderBot.gameObject, new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 2));

            // France count (left)
            playerCountText = UIFactory.CreateText(bar, "PlayerCount", "FRANCE: 0", 16, TextAnchor.MiddleLeft, UIFactory.Porcelain);
            UIFactory.SetAnchors(playerCountText.gameObject, new Vector2(0, 0), new Vector2(0.35f, 1), new Vector2(20, 0), Vector2.zero);
            playerCountText.fontStyle = FontStyle.Bold;

            // Title (center)
            titleText = UIFactory.CreateText(bar, "Title", "FIELD COMMAND", 18, TextAnchor.MiddleCenter, UIFactory.EmpireGold);
            UIFactory.SetAnchors(titleText.gameObject, new Vector2(0.35f, 0), new Vector2(0.65f, 1), Vector2.zero, Vector2.zero);
            titleText.fontStyle = FontStyle.Bold;

            // Britain count (right)
            enemyCountText = UIFactory.CreateText(bar, "EnemyCount", "BRITAIN: 0", 16, TextAnchor.MiddleRight, UIFactory.Porcelain);
            UIFactory.SetAnchors(enemyCountText.gameObject, new Vector2(0.65f, 0), new Vector2(1, 1), Vector2.zero, new Vector2(-20, 0));
            enemyCountText.fontStyle = FontStyle.Bold;
        }





        // ===================== COMMAND PANEL =====================
        private void BuildCommandPanel()
        {
            RectTransform panel = UIFactory.CreateOrnatePanel(canvas.transform, "CommandPanel", UIFactory.DeepCharcoal);
            panel.anchorMin = new Vector2(0.5f, 0.02f);
            panel.anchorMax = new Vector2(0.7f, 0.08f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            commandPanel = panel.gameObject;

            Transform inner = panel.Find("Inner");
            UIFactory.AddHorizontalLayout(inner.gameObject, 6f, new RectOffset(8, 8, 8, 8));

            btnVolley = UIFactory.CreateButton(inner, "BtnVolley", "VOLLEY [V]", 11, OnVolleyClicked);
            btnCharge = UIFactory.CreateButton(inner, "BtnCharge", "CHARGE [C]", 11);
            btnRally = UIFactory.CreateButton(inner, "BtnRally", "RALLY [R]", 11, OnRallyClicked);
            btnCover = UIFactory.CreateButton(inner, "BtnCover", "COVER [T]", 11, OnCoverClicked);
            btnLimber = UIFactory.CreateButton(inner, "BtnLimber", "LIMBER [L]", 11, OnLimberClicked);

            volleyGO = btnVolley.gameObject;
            chargeGO = btnCharge.gameObject;
            rallyGO = btnRally.gameObject;
            coverGO = btnCover.gameObject;
            limberGO = btnLimber.gameObject;

            volleyLabel = btnVolley.GetComponentInChildren<Text>();
            chargeLabel = btnCharge.GetComponentInChildren<Text>();
            rallyLabel = btnRally.GetComponentInChildren<Text>();
            coverLabel = btnCover.GetComponentInChildren<Text>();
            limberLabel = btnLimber.GetComponentInChildren<Text>();

            commandPanel.SetActive(false);
        }

        // ===================== DEPLOYMENT PANEL =====================
        private void BuildDeploymentPanel()
        {
            RectTransform panel = UIFactory.CreateOrnatePanel(canvas.transform, "DeploymentPanel", UIFactory.DeepCharcoal);
            panel.anchorMin = new Vector2(0.35f, 0.88f);
            panel.anchorMax = new Vector2(0.65f, 0.98f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            deploymentPanel = panel.gameObject;

            Transform inner = panel.Find("Inner");
            UIFactory.AddVerticalLayout(inner.gameObject, 2f, new RectOffset(10, 10, 4, 4));

            deploymentStatusText = UIFactory.CreateText(inner, "Status", "STRATEGIC POSITIONING", 14, TextAnchor.MiddleCenter, UIFactory.EmpireGold);
            deploymentStatusText.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(deploymentStatusText.gameObject, preferredHeight: 22);

            // Timer
            GameObject tRow = new GameObject("TimerRow");
            tRow.transform.SetParent(inner, false);
            UIFactory.AddHorizontalLayout(tRow, 0f);
            UIFactory.AddLayoutElement(tRow, preferredHeight: 18);
            Text timerTxt = UIFactory.CreateText(tRow.transform, "TimerText", "0:00", 12, TextAnchor.MiddleCenter, UIFactory.SilverText);

            // Buttons
            GameObject bRow = new GameObject("Buttons");
            bRow.transform.SetParent(inner, false);
            UIFactory.AddHorizontalLayout(bRow, 8f);
            UIFactory.AddLayoutElement(bRow, preferredHeight: 32);

            btnReady = UIFactory.CreateButton(bRow.transform, "Ready", "DECLARE READY", 12, OnReadyClicked);
            btnStartBattle = UIFactory.CreateButton(bRow.transform, "Start", "BEGIN BATTLE", 12, OnStartBattleClicked);
            btnStartBattle.GetComponent<Image>().color = UIFactory.ImperialCrimson;

            deploymentPanel.SetActive(false);
        }

        private void OnReadyClicked()
        {
            if (BattleManager.Instance != null)
            {
                BattleManager.Instance.TogglePlayerReady();
                UpdateDeploymentPanel();
            }
        }

        private void OnStartBattleClicked()
        {
            if (BattleManager.Instance != null)
            {
                BattleManager.Instance.EndDeploymentAndStartBattle();
            }
        }

        private void UpdateDeploymentPanel()
        {
            if (BattleManager.Instance == null || deploymentPanel == null) return;

            bool isDeployment = BattleManager.Instance.IsDeploymentPhase;
            deploymentPanel.SetActive(isDeployment);

            if (!isDeployment) return;

            // Update status
            bool playerReady = BattleManager.Instance.PlayerReady;
            bool bothReady = BattleManager.Instance.BothPlayersReady;
            float timeRemaining = BattleManager.Instance.DeploymentTimeRemaining;

            if (bothReady)
            {
                deploymentStatusText.text = "Both players ready! Starting battle...";
                deploymentStatusText.color = new Color(0.3f, 1f, 0.3f);
            }
            else if (playerReady)
            {
                deploymentStatusText.text = "Waiting for opponent...";
                deploymentStatusText.color = new Color(0.9f, 0.7f, 0.3f);
            }
            else
            {
                deploymentStatusText.text = "Position your units and click Ready";
                deploymentStatusText.color = UIFactory.GoldAccent;
            }

            // Update timer
            int minutes = Mathf.FloorToInt(timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(timeRemaining % 60f);
            var timerTextLocal = deploymentPanel.transform.Find("TimerText")?.GetComponent<Text>();
            if (timerTextLocal != null)
                timerTextLocal.text = $"{minutes}:{seconds:00}";

            // Update button states
            var readyBtnImage = btnReady.GetComponent<Image>();
            readyBtnImage.color = playerReady ? new Color(0.3f, 0.8f, 0.3f) : UIFactory.ButtonNormal;
            btnReady.GetComponentInChildren<Text>().text = playerReady ? "Cancel Ready" : "I'm Ready";

            // Only enable Start Battle if both ready or in single player
            btnStartBattle.interactable = bothReady;
        }

        // ===================== BATTLE RESULT =====================
        private void BuildBattleResult()
        {
            RectTransform overlay = UIFactory.CreatePanel(canvas.transform, "ResultOverlay", new Color(0, 0, 0, 0.5f));
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            resultOverlay = overlay.gameObject;

            resultText = UIFactory.CreateText(overlay, "ResultText", "", 56, TextAnchor.MiddleCenter, UIFactory.GoldAccent);
            resultText.fontStyle = FontStyle.Bold;
            RectTransform rtRT = resultText.GetComponent<RectTransform>();
            rtRT.anchorMin = new Vector2(0, 0.35f);
            rtRT.anchorMax = new Vector2(1, 0.65f);
            rtRT.offsetMin = Vector2.zero;
            rtRT.offsetMax = Vector2.zero;

            // Add outline for readability
            Outline outline = resultText.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2, 2);

            resultOverlay.SetActive(false);
        }



        // ===================== UPDATE METHODS =====================
        private void UpdateTopBar()
        {
            if (BattleManager.Instance == null) return;
            var (playerAlive, enemyAlive) = BattleManager.Instance.GetArmyCounts();
            playerCountText.text = $"FRANCE: {playerAlive}";
            enemyCountText.text = $"BRITAIN: {enemyAlive}";
        }

        // UpdateSelectionPanel removed



        private void UpdateCommandButtons()
        {
            if (!commandPanel.activeSelf) return;

            bool hasSelection = SelectionManager.Instance != null && SelectionManager.Instance.HasSelection;
            var regs = hasSelection ? SelectionManager.Instance.SelectedRegiments : null;
            bool anyVolley = false, anyCanVolley = false;
            bool anyCanCharge = false;
            if (regs != null) foreach (var r in regs)
            {
                if (r.UnitData != null && r.UnitData.canVolleyFire) anyCanVolley = true;
                if (r.IsVolleyMode) anyVolley = true;
                if (r.UnitData != null && r.UnitData.canCharge) anyCanCharge = true;
            }

            volleyGO.SetActive(anyCanVolley);
            chargeGO.SetActive(anyCanCharge);
            coverGO.SetActive(hasSelection);

            if (anyCanVolley)
            {
                volleyLabel.text = anyVolley ? "Volley ON [V]" : "Volley [V]";
                btnVolley.GetComponent<Image>().color = anyVolley ? UIFactory.ButtonActive : UIFactory.ButtonNormal;
            }
        }

        private void UpdateBattleResult()
        {
            if (GameManager.Instance == null) { resultOverlay.SetActive(false); return; }

            string message = null;
            Color color = Color.white;

            if (GameManager.Instance.CurrentState == GameState.Victory)
            {
                message = "VICTORY!";
                color = UIFactory.GoldAccent;
            }
            else if (GameManager.Instance.CurrentState == GameState.Defeat)
            {
                message = "DEFEAT";
                color = new Color(0.8f, 0.2f, 0.2f);
            }
            else if (GameManager.Instance.CurrentState == GameState.Paused)
            {
                message = "PAUSED";
                color = UIFactory.TextWhite;
            }

            if (message != null)
            {
                resultOverlay.SetActive(true);
                resultText.text = message;
                resultText.color = color;
            }
            else
            {
                resultOverlay.SetActive(false);
            }
        }

        // ===================== ACTIONS =====================
        private void SetFormation(FormationType formation)
        {
            if (SelectionManager.Instance == null) return;
            foreach (var reg in SelectionManager.Instance.SelectedRegiments)
                reg.SetFormation(formation);
        }

        private void OnVolleyClicked()
        {
            if (SelectionManager.Instance == null) return;
            foreach (var r in SelectionManager.Instance.SelectedRegiments)
                r.ToggleVolleyFire();
        }

        private void OnRallyClicked()
        {
            if (SelectionManager.Instance == null) return;
            if (rallyCooldown > 0f) return;

            int ralliedTotal = 0;
            foreach (var r in SelectionManager.Instance.SelectedRegiments)
                ralliedTotal += r.Rally();

            if (ralliedTotal > 0)
            {
                rallyCooldown = 15f; // 15 second cooldown between rallies
                if (rallyLabel != null)
                    rallyLabel.text = $"Rally ({rallyCooldown:F0}s)";
            }
        }

        private void OnCoverClicked()
        {
            // Toggle cover mode via CoverPositioning system
            if (NapoleonicWars.Battlefield.CoverPositioning.Instance != null)
            {
                NapoleonicWars.Battlefield.CoverPositioning.Instance.ToggleCoverMode();
            }
            else
            {
                Debug.LogWarning("[BattleHUD] CoverPositioning system not available");
            }
        }

        private void OnLimberClicked()
        {
            if (SelectionManager.Instance == null) return;
            foreach (var reg in SelectionManager.Instance.SelectedRegiments)
            {
                if (reg != null && reg.IsArtillery)
                {
                    reg.ToggleLimber();
                }
            }
        }

        private Color GetMoraleColor(float percent)
        {
            if (percent > 0.6f) return new Color(0.2f, 0.8f, 0.2f);
            if (percent > 0.3f) return new Color(0.9f, 0.7f, 0.1f);
            return new Color(0.9f, 0.2f, 0.1f);
        }

        // ===================== UNIT CARDS PANEL (Warhammer Style) =====================
        private void BuildUnitCardsPanel()
        {
            RectTransform panel = UIFactory.CreateOrnatePanel(canvas.transform, "UnitCardsPanel",
                new Color(0.12f, 0.13f, 0.11f, 0.95f));
            // Bottom center, spanning most of the width
            panel.anchorMin = new Vector2(0.20f, 0.02f);
            panel.anchorMax = new Vector2(0.84f, 0.16f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            unitCardsPanel = panel.gameObject;

            Transform inner = panel.Find("Inner");
            // Use HorizontalLayout but disable child control to prevent flickering
            var hlg = UIFactory.AddHorizontalLayout(inner.gameObject, 6f, new RectOffset(8, 8, 6, 6));
            hlg.childControlWidth = false; // Don't let layout control width
            hlg.childControlHeight = false; // Don't let layout control height  
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            unitCardsContainer = inner;

            // Add CanvasGroup for smooth visibility changes
            unitCardsCanvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
            unitCardsCanvasGroup.alpha = 0f;
            unitCardsCanvasGroup.blocksRaycasts = false;

            unitCardsPanel.SetActive(true); // Keep active, control visibility with CanvasGroup
        }

        private void UpdateUnitCards()
        {
            if (BattleManager.Instance == null) return;

            // Get current player regiments
            playerRegiments.Clear();
            foreach (var reg in BattleManager.Instance.PlayerRegiments)
            {
                if (reg != null && reg.CachedAliveCount > 0)
                    playerRegiments.Add(reg);
            }

            // Show/hide panel using CanvasGroup (smooth, no layout recalc)
            bool shouldShow = playerRegiments.Count > 0;
            if (unitCardsCanvasGroup != null)
            {
                float targetAlpha = shouldShow ? 1f : 0f;
                if (Mathf.Abs(unitCardsCanvasGroup.alpha - targetAlpha) > 0.01f)
                {
                    unitCardsCanvasGroup.alpha = Mathf.Lerp(unitCardsCanvasGroup.alpha, targetAlpha, 0.3f);
                }
                unitCardsCanvasGroup.blocksRaycasts = shouldShow;
            }

            if (!shouldShow) return;

            // Only recreate cards if count changed
            if (playerRegiments.Count != lastRegimentCount)
            {
                // Ensure we have enough cards
                while (unitCardObjects.Count < playerRegiments.Count)
                {
                    CreateUnitCard();
                }

                // Hide excess cards using CanvasGroup alpha instead of SetActive
                for (int i = playerRegiments.Count; i < unitCardObjects.Count; i++)
                {
                    var cg = unitCardObjects[i].GetComponent<CanvasGroup>();
                    if (cg == null) cg = unitCardObjects[i].AddComponent<CanvasGroup>();
                    cg.alpha = 0f;
                    cg.blocksRaycasts = false;
                }

                lastRegimentCount = playerRegiments.Count;
            }

            // Update visible cards
            for (int i = 0; i < playerRegiments.Count; i++)
            {
                UpdateUnitCard(unitCardObjects[i], playerRegiments[i], i);
            }
        }

        private void CreateUnitCard()
        {
            int index = unitCardObjects.Count;

            GameObject card = new GameObject($"UnitCard_{index}");
            card.transform.SetParent(unitCardsContainer, false);
            RectTransform rt = card.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(100, 80);
            UIFactory.AddLayoutElement(card, 80, 100);

            // Add CanvasGroup for this card
            CanvasGroup cg = card.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.blocksRaycasts = true;

            // Card background
            Image bg = card.AddComponent<Image>();
            Sprite cardFrame = Resources.Load<Sprite>("UI/Cards/CardFrame");
            if (cardFrame != null)
            {
                bg.sprite = cardFrame;
                bg.color = Color.white;
            }
            else
            {
                bg.color = new Color(0.16f, 0.18f, 0.15f, 0.9f);
            }
            bg.raycastTarget = true;

            // Border/Outline (only if no sprite)
            if (cardFrame == null)
            {
                Outline outline = card.AddComponent<Outline>();
                outline.effectColor = UIFactory.BorderGold;
                outline.effectDistance = new Vector2(1, 1);
            }

            // Unit icon/symbol (Image)
            GameObject iconObj = new GameObject("IconImage");
            iconObj.transform.SetParent(card.transform, false);
            RectTransform iconRT = iconObj.AddComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.5f, 0.5f); // Center it
            iconRT.anchorMax = new Vector2(0.5f, 0.5f);
            iconRT.sizeDelta = new Vector2(50, 50); // Adjust size as needed
            iconRT.anchoredPosition = new Vector2(0, 10);
            
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.color = Color.clear; // Invisible by default until sprite loaded

            // Fallback Text Icon (keep as backup)
            Text iconText = UIFactory.CreateText(card.transform, "IconText", "⚔", 20, TextAnchor.MiddleCenter, UIFactory.GoldAccent);
            iconText.fontStyle = FontStyle.Bold;
            RectTransform iconTextRT = iconText.GetComponent<RectTransform>();
            iconTextRT.anchorMin = new Vector2(0, 1);
            iconTextRT.anchorMax = new Vector2(0.4f, 1);
            iconTextRT.pivot = new Vector2(0, 1);
            iconTextRT.sizeDelta = new Vector2(0, 28);
            iconTextRT.anchoredPosition = new Vector2(4, -4);

            // Unit name
            Text nameText = UIFactory.CreateText(card.transform, "Name", "Regiment", 10, TextAnchor.MiddleCenter, UIFactory.ParchmentBeige);
            nameText.fontStyle = FontStyle.Bold;
            RectTransform nameRT = nameText.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0.05f, 0.15f);
            nameRT.anchorMax = new Vector2(0.95f, 0.35f); // Moved down
            nameRT.offsetMin = Vector2.zero;
            nameRT.offsetMax = Vector2.zero;

            // Count text
            Text countText = UIFactory.CreateText(card.transform, "Count", "120", 11, TextAnchor.MiddleCenter, Color.white);
            RectTransform countRT = countText.GetComponent<RectTransform>();
            countRT.anchorMin = new Vector2(0.05f, 0.02f);
            countRT.anchorMax = new Vector2(0.95f, 0.20f); // Bottom
            countRT.offsetMin = Vector2.zero;
            countRT.offsetMax = Vector2.zero;

            // Health bar background (Moved to very bottom, thin line)
            GameObject hpBg = new GameObject("HpBg");
            hpBg.transform.SetParent(card.transform, false);
            RectTransform hpBgRT = hpBg.AddComponent<RectTransform>();
            hpBgRT.anchorMin = new Vector2(0, 0);
            hpBgRT.anchorMax = new Vector2(1, 0.05f);
            hpBgRT.offsetMin = Vector2.zero;
            hpBgRT.offsetMax = Vector2.zero;
            Image hpBgImg = hpBg.AddComponent<Image>();
            hpBgImg.color = new Color(0.2f, 0.2f, 0.2f);

            // Health bar fill
            GameObject hpFill = new GameObject("HpFill");
            hpFill.transform.SetParent(hpBg.transform, false);
            RectTransform hpFillRT = hpFill.AddComponent<RectTransform>();
            hpFillRT.anchorMin = Vector2.zero;
            hpFillRT.anchorMax = new Vector2(1, 1);
            hpFillRT.offsetMin = Vector2.zero;
            hpFillRT.offsetMax = Vector2.zero;
            Image hpFillImg = hpFill.AddComponent<Image>();
            hpFillImg.color = new Color(0.3f, 0.8f, 0.3f);

            // Selection highlight
            GameObject highlight = new GameObject("Highlight");
            highlight.transform.SetParent(card.transform, false);
            RectTransform hlRT = highlight.AddComponent<RectTransform>();
            hlRT.anchorMin = Vector2.zero;
            hlRT.anchorMax = Vector2.one;
            hlRT.offsetMin = new Vector2(-3, -3);
            hlRT.offsetMax = new Vector2(3, 3);
            Image hlImg = highlight.AddComponent<Image>();
            hlImg.color = new Color(0.9f, 0.75f, 0.3f, 0.3f);
            hlImg.enabled = false;

            // Make card clickable
            Button btn = card.AddComponent<Button>();
            btn.targetGraphic = bg;
            ColorBlock colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
            colors.pressedColor = new Color(0.9f, 0.9f, 0.9f);
            btn.colors = colors;

            int cardIndex = index;
            btn.onClick.AddListener(() => OnUnitCardClicked(cardIndex));

            unitCardObjects.Add(card);
        }

        private Dictionary<Regiment, int> lastAliveCounts = new Dictionary<Regiment, int>();

        private void UpdateUnitCard(GameObject card, Regiment reg, int index)
        {
            // Ensure card is visible via CanvasGroup (not SetActive)
            var cg = card.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.blocksRaycasts = true;
            }

            // Update selection highlight
            Transform highlight = card.transform.Find("Highlight");
            if (highlight != null)
            {
                Image hlImg = highlight.GetComponent<Image>();
                bool isSelected = SelectionManager.Instance != null &&
                    SelectionManager.Instance.SelectedRegiments.Contains(reg);
                if (hlImg.enabled != isSelected)
                    hlImg.enabled = isSelected;
            }

            // Update Icon (Image)
            Transform iconObj = card.transform.Find("IconImage");
            Transform iconTextObj = card.transform.Find("IconText");
            if (iconObj != null)
            {
                Image iconImg = iconObj.GetComponent<Image>();
                string spriteName = GetUnitSpriteName(reg);
                Sprite iconSprite = Resources.Load<Sprite>($"UI/Cards/{spriteName}");
                
                if (iconSprite != null)
                {
                    iconImg.sprite = iconSprite;
                    iconImg.color = Color.white;
                    if (iconTextObj != null) iconTextObj.gameObject.SetActive(false); // Hide text if sprite exists
                }
                else
                {
                    iconImg.color = Color.clear; // Hide image if no sprite
                    if (iconTextObj != null) 
                    {
                        iconTextObj.gameObject.SetActive(true);
                        iconTextObj.GetComponent<Text>().text = GetUnitSymbol(reg);
                    }
                }
            }

            // Update name (rarely changes, skip if same)
            Text nameText = card.transform.Find("Name").GetComponent<Text>();
            string newName = reg.UnitData != null ? reg.UnitData.unitName.Replace("Line Infantry", "Line Inf").Replace("Light Infantry", "Light Inf") : "Regiment";
            if (nameText.text != newName)
                nameText.text = newName;

            // Update count only if changed
            int currentAlive = reg.CachedAliveCount;
            Text countText = card.transform.Find("Count").GetComponent<Text>();
            string newCount = $"{currentAlive}/{reg.Units.Count}";
            if (countText.text != newCount)
                countText.text = newCount;

            // Update health bar only if count changed
            Transform hpFill = card.transform.Find("HpBg/HpFill");
            if (hpFill != null)
            {
                RectTransform hpRT = hpFill.GetComponent<RectTransform>();
                float healthPercent = reg.Units.Count > 0 ? (float)currentAlive / reg.Units.Count : 0f;
                Vector2 newAnchorMax = new Vector2(Mathf.Clamp01(healthPercent), 1f);
                if (Vector2.Distance(hpRT.anchorMax, newAnchorMax) > 0.001f)
                {
                    hpRT.anchorMax = newAnchorMax;

                    // Update color based on health
                    Image hpImg = hpFill.GetComponent<Image>();
                    Color newColor;
                    if (healthPercent > 0.6f) newColor = new Color(0.3f, 0.8f, 0.3f);
                    else if (healthPercent > 0.3f) newColor = new Color(0.9f, 0.7f, 0.1f);
                    else newColor = new Color(0.9f, 0.2f, 0.1f);
                    if (hpImg.color != newColor)
                        hpImg.color = newColor;
                }
            }

            // === SUPPRESSION INDICATOR ===
            Transform suppressionBar = card.transform.Find("SuppressionBg/SuppressionFill");
            if (suppressionBar == null)
            {
                // Create suppression bar if it doesn't exist (below health bar)
                Transform hpBg = card.transform.Find("HpBg");
                if (hpBg != null)
                {
                    GameObject suppBg = new GameObject("SuppressionBg");
                    suppBg.transform.SetParent(card.transform, false);
                    RectTransform suppBgRT = suppBg.AddComponent<RectTransform>();
                    suppBgRT.anchorMin = new Vector2(0.05f, 0.12f);
                    suppBgRT.anchorMax = new Vector2(0.95f, 0.18f);
                    suppBgRT.offsetMin = Vector2.zero;
                    suppBgRT.offsetMax = Vector2.zero;
                    Image suppBgImg = suppBg.AddComponent<Image>();
                    suppBgImg.color = new Color(0.15f, 0.15f, 0.15f, 0.6f);

                    GameObject suppFillObj = new GameObject("SuppressionFill");
                    suppFillObj.transform.SetParent(suppBg.transform, false);
                    RectTransform suppFillRT = suppFillObj.AddComponent<RectTransform>();
                    suppFillRT.anchorMin = Vector2.zero;
                    suppFillRT.anchorMax = new Vector2(0f, 1f);
                    suppFillRT.offsetMin = Vector2.zero;
                    suppFillRT.offsetMax = Vector2.zero;
                    Image suppFillImg = suppFillObj.AddComponent<Image>();
                    suppFillImg.color = new Color(0.9f, 0.3f, 0.1f, 0.8f); // Red-orange for suppression

                    suppressionBar = suppFillObj.transform;
                }
            }
            if (suppressionBar != null)
            {
                RectTransform suppRT = suppressionBar.GetComponent<RectTransform>();
                float suppPercent = reg.CachedAverageSuppression / 100f;
                suppRT.anchorMax = new Vector2(Mathf.Clamp01(suppPercent), 1f);
                // Only show if there's meaningful suppression
                suppressionBar.parent.gameObject.SetActive(suppPercent > 0.05f);
            }

            // === AMMO INDICATOR ===
            Transform ammoText = card.transform.Find("AmmoText");
            if (ammoText == null)
            {
                GameObject ammoObj = new GameObject("AmmoText");
                ammoObj.transform.SetParent(card.transform, false);
                RectTransform ammoRT = ammoObj.AddComponent<RectTransform>();
                ammoRT.anchorMin = new Vector2(0.6f, 0.82f);
                ammoRT.anchorMax = new Vector2(0.98f, 0.98f);
                ammoRT.offsetMin = Vector2.zero;
                ammoRT.offsetMax = Vector2.zero;
                Text ammoTxt = ammoObj.AddComponent<Text>();
                ammoTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                ammoTxt.fontSize = 9;
                ammoTxt.alignment = TextAnchor.UpperRight;
                ammoTxt.color = Color.white;
                ammoText = ammoObj.transform;
            }
            {
                Text ammoTxt = ammoText.GetComponent<Text>();
                if (reg.UnitData != null && !reg.UnitData.hasUnlimitedAmmo)
                {
                    int totalAmmo = reg.CachedTotalAmmo;
                    int maxAmmo = reg.Units.Count * reg.UnitData.maxAmmo;
                    float ammoPercent = maxAmmo > 0 ? (float)totalAmmo / maxAmmo : 0f;

                    Color ammoColor;
                    if (ammoPercent > 0.5f) ammoColor = Color.white;
                    else if (ammoPercent > 0.2f) ammoColor = new Color(1f, 0.8f, 0.2f);
                    else ammoColor = new Color(1f, 0.3f, 0.2f);

                    ammoTxt.color = ammoColor;
                    ammoTxt.text = totalAmmo <= 0 ? "NO AMMO" : $"{Mathf.RoundToInt(ammoPercent * 100)}%";
                }
                else
                {
                    ammoTxt.text = "";
                }
            }

            // === LIMBER INDICATOR (Artillery only) ===
            Transform limberText = card.transform.Find("LimberText");
            if (limberText == null && reg.IsArtillery)
            {
                GameObject limberObj = new GameObject("LimberText");
                limberObj.transform.SetParent(card.transform, false);
                RectTransform limberRT = limberObj.AddComponent<RectTransform>();
                limberRT.anchorMin = new Vector2(0.02f, 0.82f);
                limberRT.anchorMax = new Vector2(0.5f, 0.98f);
                limberRT.offsetMin = Vector2.zero;
                limberRT.offsetMax = Vector2.zero;
                Text limberTxt = limberObj.AddComponent<Text>();
                limberTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                limberTxt.fontSize = 8;
                limberTxt.alignment = TextAnchor.UpperLeft;
                limberText = limberObj.transform;
            }
            if (limberText != null && reg.IsArtillery)
            {
                Text limberTxt = limberText.GetComponent<Text>();
                if (reg.IsTransitioningLimber)
                {
                    limberTxt.text = reg.IsLimbered ? "UNLIMBERING..." : "LIMBERING...";
                    limberTxt.color = new Color(1f, 0.8f, 0.2f);
                }
                else
                {
                    limberTxt.text = reg.IsLimbered ? "LIMBERED" : "DEPLOYED";
                    limberTxt.color = reg.IsLimbered ? new Color(0.5f, 0.8f, 1f) : new Color(0.3f, 1f, 0.3f);
                }
            }
        }

        private string GetUnitSpriteName(Regiment reg)
        {
            if (reg.UnitData == null) return "Unknown";
            string typeName = reg.UnitData.unitType.ToString().ToLower();
            if (typeName.Contains("cavalry")) return "Icon_Cavalry";
            if (typeName.Contains("artillery")) return "Icon_Artillery";
            if (typeName.Contains("light")) return "Icon_LightInfantry";
            return "Icon_LineInfantry";
        }

        private string GetUnitSymbol(Regiment reg)
        {
            if (reg.UnitData == null) return "⚔";

            // Simplified - just check unit type name
            string typeName = reg.UnitData.unitType.ToString().ToLower();
            if (typeName.Contains("cavalry")) return "🏇";
            if (typeName.Contains("artillery")) return "💣";
            if (typeName.Contains("light")) return "🎯";
            return "⚔";
        }

        private void OnUnitCardClicked(int cardIndex)
        {
            if (cardIndex < 0 || cardIndex >= playerRegiments.Count) return;

            Regiment reg = playerRegiments[cardIndex];
            if (reg == null)
            {
                Debug.Log("[UnitCard] Regiment is null!");
                return;
            }

            Debug.Log($"[UnitCard] Clicked on {reg.RegimentName}, alive: {reg.CachedAliveCount}");

            // Add to selection with shift key, or replace selection
            bool addToSelection = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (SelectionManager.Instance != null)
            {
                if (addToSelection)
                {
                    // Toggle selection
                    if (SelectionManager.Instance.SelectedRegiments.Contains(reg))
                    {
                        Debug.Log($"[UnitCard] Deselecting {reg.RegimentName}");
                        // Deselect this regiment (clear all and re-add others)
                        var otherRegiments = new List<Regiment>(SelectionManager.Instance.SelectedRegiments);
                        otherRegiments.Remove(reg);
                        SelectionManager.Instance.ClearSelection();
                        foreach (var r in otherRegiments)
                            SelectionManager.Instance.SelectRegiment(r);
                    }
                    else
                    {
                        Debug.Log($"[UnitCard] Adding {reg.RegimentName} to selection");
                        SelectionManager.Instance.SelectRegiment(reg);
                    }
                }
                else
                {
                    Debug.Log($"[UnitCard] Selecting only {reg.RegimentName}");
                    // Select only this regiment
                    SelectionManager.Instance.ClearSelection();
                    SelectionManager.Instance.SelectRegiment(reg);
                }
            }
            else
            {
                Debug.LogError("[UnitCard] SelectionManager.Instance is null!");
            }

            // Focus camera on the regiment using CameraController with closer zoom
            var camController = FindFirstObjectByType<CameraController>();
            if (camController != null)
            {
                // Zoom to height 40 (medium-close) to see the regiment better
                camController.FocusOnPosition(reg.transform.position, 40f);
            }
            else
            {
                // Fallback: move camera directly
                var mainCam = Camera.main;
                if (mainCam != null)
                {
                    Vector3 targetPos = reg.transform.position;
                    Vector3 camPos = mainCam.transform.position;
                    camPos = new Vector3(targetPos.x, camPos.y, targetPos.z - 20f);
                    mainCam.transform.position = camPos;
                }
            }
        }

        // ===================== SPEED CONTROLS =====================
        private void BuildSpeedControls()
        {
            RectTransform bar = UIFactory.CreatePanel(canvas.transform, "SpeedBar", UIFactory.DarkStone);
            bar.anchorMin = new Vector2(1, 1);
            bar.anchorMax = new Vector2(1, 1);
            bar.pivot = new Vector2(1, 1);
            bar.anchoredPosition = new Vector2(-10, -50);
            bar.sizeDelta = new Vector2(200, 32);

            // Timer
            timerText = UIFactory.CreateText(bar, "Timer", "00:00", 14, TextAnchor.MiddleLeft, UIFactory.TextWhite);
            RectTransform tmRT = timerText.GetComponent<RectTransform>();
            tmRT.anchorMin = new Vector2(0, 0);
            tmRT.anchorMax = new Vector2(0.4f, 1);
            tmRT.offsetMin = new Vector2(8, 0);
            tmRT.offsetMax = Vector2.zero;

            // Speed buttons
            float[] speeds = { 0.5f, 1f, 2f, 3f };
            string[] labels = { "½", "1x", "2x", "3x" };
            float btnW = 32f;
            float startX = 85f;

            for (int i = 0; i < speeds.Length; i++)
            {
                float spd = speeds[i];
                string lbl = labels[i];
                GameObject btnGO = new GameObject($"Speed_{lbl}");
                btnGO.transform.SetParent(bar, false);
                RectTransform btnRT = btnGO.AddComponent<RectTransform>();
                btnRT.anchorMin = new Vector2(0, 0.1f);
                btnRT.anchorMax = new Vector2(0, 0.9f);
                btnRT.anchoredPosition = new Vector2(startX + i * (btnW + 2), 0);
                btnRT.sizeDelta = new Vector2(btnW, 0);
                Image btnImg = btnGO.AddComponent<Image>();
                btnImg.color = spd == 1f ? UIFactory.ButtonActive : UIFactory.ButtonNormal;
                Button btn = btnGO.AddComponent<Button>();
                btn.onClick.AddListener(() => SetSpeed(spd));

                Text t = UIFactory.CreateText(btnGO.transform, "Lbl", lbl, 12, TextAnchor.MiddleCenter, UIFactory.GoldAccent);
                RectTransform tRT = t.GetComponent<RectTransform>();
                tRT.anchorMin = Vector2.zero;
                tRT.anchorMax = Vector2.one;
                tRT.offsetMin = Vector2.zero;
                tRT.offsetMax = Vector2.zero;
                t.fontStyle = FontStyle.Bold;
            }

            // Speed label
            speedText = UIFactory.CreateText(bar, "SpeedLabel", "1.0x", 12, TextAnchor.MiddleRight, UIFactory.GoldAccent);
            RectTransform slRT = speedText.GetComponent<RectTransform>();
            slRT.anchorMin = new Vector2(0, 0);
            slRT.anchorMax = new Vector2(0, 1);
            slRT.anchoredPosition = new Vector2(75, 0);
            slRT.sizeDelta = new Vector2(40, 0);
        }

        private void UpdateSpeedControls()
        {
            // Rally cooldown
            if (rallyCooldown > 0f)
            {
                rallyCooldown -= Time.unscaledDeltaTime;
                if (rallyLabel != null)
                    rallyLabel.text = rallyCooldown > 0f ? $"Rally ({rallyCooldown:F0}s)" : "Rally [R]";
            }

            // Rally hotkey
            if (Input.GetKeyDown(KeyCode.R)) OnRallyClicked();
            // Limber hotkey
            if (Input.GetKeyDown(KeyCode.L)) OnLimberClicked();

            // Show/hide limber button based on selected regiment type
            if (limberGO != null)
            {
                bool hasArtillery = false;
                if (SelectionManager.Instance != null)
                {
                    foreach (var reg in SelectionManager.Instance.SelectedRegiments)
                    {
                        if (reg != null && reg.IsArtillery) { hasArtillery = true; break; }
                    }
                }
                limberGO.SetActive(hasArtillery);
                if (hasArtillery && limberLabel != null && SelectionManager.Instance != null)
                {
                    foreach (var reg in SelectionManager.Instance.SelectedRegiments)
                    {
                        if (reg != null && reg.IsArtillery)
                        {
                            limberLabel.text = reg.IsLimbered ? "UNLIMBER [L]" : "LIMBER [L]";
                            break;
                        }
                    }
                }
            }

            // Keyboard shortcuts: 1-4 for speed
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetSpeed(0.5f);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetSpeed(1f);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetSpeed(2f);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SetSpeed(3f);

            // Update timer
            if (timerText != null && BattleStatistics.Instance != null)
            {
                timerText.text = BattleStatistics.Instance.GetBattleDurationString();
            }
        }

        private void SetSpeed(float speed)
        {
            currentSpeed = speed;
            Time.timeScale = speed;
            if (speedText != null)
                speedText.text = $"{speed:F1}x";
            if (GameManager.Instance != null)
                GameManager.Instance.SetBattleSpeed(speed);
        }
    }
}
