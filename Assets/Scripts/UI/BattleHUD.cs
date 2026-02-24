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

        // Selection panel
        private GameObject selectionPanel;
        private Text selectionHeaderText;
        private Text unitsAliveText;
        private Text unitTypeText;
        private Text formationText;
        private Text moraleLabel;
        private Image moraleFill;
        private Image moraleBg;
        private Text staminaLabel;
        private Image staminaFill;
        private Image staminaBg;
        private Text ammoText;
        private Text experienceText;
        private Text officerText;
        private Text specialText;

        // Formation buttons
        private GameObject formationPanel;
        private Button btnLine, btnColumn, btnSquare, btnSkirmish;
        private Image imgLine, imgColumn, imgSquare, imgSkirmish;

        // Command buttons
        private GameObject commandPanel;
        private Button btnVolley, btnCharge, btnRally, btnCover;
        private Text volleyLabel, chargeLabel, rallyLabel, coverLabel;
        private GameObject volleyGO, chargeGO, rallyGO, coverGO;
        private float rallyCooldown;

        // Deployment panel
        private GameObject deploymentPanel;
        private Button btnStartBattle, btnReady;
        private Text deploymentStatusText;

        // Battle result overlay
        private GameObject resultOverlay;
        private Text resultText;

        // Controls help
        private GameObject controlsPanel;

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

            // Throttle selection panel — morale/stamina/ammo
            selectionTimer -= dt;
            if (selectionTimer <= 0f)
            {
                selectionTimer = 0.2f;
                UpdateSelectionPanel();
            }

            UpdateFormationButtons();
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
            BuildSelectionPanel();
            BuildFormationPanel();
            BuildCommandPanel();
            BuildDeploymentPanel();
            BuildBattleResult();
            BuildControlsHelp();
            BuildSpeedControls();
            BuildUnitCardsPanel();
        }

        // ===================== TOP BAR =====================
        private void BuildTopBar()
        {
            RectTransform bar = UIFactory.CreatePanel(canvas.transform, "TopBar", new Color(0.06f, 0.03f, 0.03f, 0.98f));
            bar.anchorMin = new Vector2(0, 1);
            bar.anchorMax = new Vector2(1, 1);
            bar.pivot = new Vector2(0.5f, 1);
            bar.offsetMin = new Vector2(0, -48);
            bar.offsetMax = Vector2.zero;

            // Gold accent line at bottom
            RectTransform accent = UIFactory.CreatePanel(bar, "Accent", UIFactory.BorderGoldBright);
            accent.anchorMin = new Vector2(0, 0);
            accent.anchorMax = new Vector2(1, 0);
            accent.offsetMin = Vector2.zero;
            accent.offsetMax = new Vector2(0, 2);

            // France count (left)
            playerCountText = UIFactory.CreateText(bar, "PlayerCount", "France: 0", 18, TextAnchor.MiddleLeft, UIFactory.FranceBlue);
            RectTransform pcRT = playerCountText.GetComponent<RectTransform>();
            pcRT.anchorMin = new Vector2(0, 0);
            pcRT.anchorMax = new Vector2(0.3f, 1);
            pcRT.offsetMin = new Vector2(20, 0);
            pcRT.offsetMax = Vector2.zero;
            playerCountText.fontStyle = FontStyle.Bold;

            // Title (center)
            titleText = UIFactory.CreateText(bar, "Title", "⚔ NAPOLEONIC WARS ⚔", 22, TextAnchor.MiddleCenter, UIFactory.GoldAccent);
            RectTransform ttRT = titleText.GetComponent<RectTransform>();
            ttRT.anchorMin = new Vector2(0.3f, 0);
            ttRT.anchorMax = new Vector2(0.7f, 1);
            ttRT.offsetMin = Vector2.zero;
            ttRT.offsetMax = Vector2.zero;
            titleText.fontStyle = FontStyle.Bold;
            Shadow titleShadow = titleText.gameObject.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0, 0, 0, 0.8f);
            titleShadow.effectDistance = new Vector2(2f, -2f);

            // Britain count (right)
            enemyCountText = UIFactory.CreateText(bar, "EnemyCount", "Britain: 0", 18, TextAnchor.MiddleRight, UIFactory.BritainRed);
            RectTransform ecRT = enemyCountText.GetComponent<RectTransform>();
            ecRT.anchorMin = new Vector2(0.7f, 0);
            ecRT.anchorMax = new Vector2(1, 1);
            ecRT.offsetMin = Vector2.zero;
            ecRT.offsetMax = new Vector2(-20, 0);
            enemyCountText.fontStyle = FontStyle.Bold;
        }

        // ===================== SELECTION PANEL =====================
        private void BuildSelectionPanel()
        {
            RectTransform panel = UIFactory.CreateOrnatePanel(canvas.transform, "SelectionPanel",
                new Color(0.12f, 0.13f, 0.11f, 0.95f));
            // Use relative anchors for responsive scaling (bottom-left, ~15% width, ~35% height)
            panel.anchorMin = new Vector2(0.005f, 0.02f);
            panel.anchorMax = new Vector2(0.18f, 0.38f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            selectionPanel = panel.gameObject;

            Transform inner = panel.Find("Inner");
            UIFactory.AddVerticalLayout(inner.gameObject, 3f, new RectOffset(10, 10, 8, 8));

            selectionHeaderText = UIFactory.CreateText(inner, "Header", "Selected: 0 regiment(s)", 15, TextAnchor.MiddleLeft, UIFactory.GoldAccent);
            selectionHeaderText.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(selectionHeaderText.gameObject, 22);

            RectTransform sep = UIFactory.CreateOrnamentalSeparator(inner);
            UIFactory.AddLayoutElement(sep.gameObject, 14);

            unitsAliveText = UIFactory.CreateText(inner, "Alive", "Units alive: 0", 13, TextAnchor.MiddleLeft, UIFactory.ParchmentBeige);
            UIFactory.AddLayoutElement(unitsAliveText.gameObject, 20);

            unitTypeText = UIFactory.CreateText(inner, "Type", "", 13, TextAnchor.MiddleLeft, UIFactory.ParchmentBeige);
            UIFactory.AddLayoutElement(unitTypeText.gameObject, 20);

            formationText = UIFactory.CreateText(inner, "Formation", "", 13, TextAnchor.MiddleLeft, UIFactory.ParchmentBeige);
            UIFactory.AddLayoutElement(formationText.gameObject, 20);

            // Morale bar row
            GameObject moraleRow = new GameObject("MoraleRow");
            moraleRow.transform.SetParent(inner, false);
            UIFactory.AddHorizontalLayout(moraleRow, 6f);
            UIFactory.AddLayoutElement(moraleRow, 20);

            moraleLabel = UIFactory.CreateText(moraleRow.transform, "MoraleLabel", "Morale:", 13, TextAnchor.MiddleLeft, UIFactory.ParchmentBeige);
            UIFactory.AddLayoutElement(moraleLabel.gameObject, preferredWidth: 55);

            var (bg, fill) = UIFactory.CreateProgressBar(moraleRow.transform, "MoraleBar", new Color(0.2f, 0.8f, 0.2f));
            moraleBg = bg;
            moraleFill = fill;
            UIFactory.AddLayoutElement(bg.gameObject, 16, flexibleWidth: 1);

            // Stamina bar row
            GameObject staminaRow = new GameObject("StaminaRow");
            staminaRow.transform.SetParent(inner, false);
            UIFactory.AddHorizontalLayout(staminaRow, 6f);
            UIFactory.AddLayoutElement(staminaRow, 20);

            staminaLabel = UIFactory.CreateText(staminaRow.transform, "StaminaLabel", "Stamina:", 13, TextAnchor.MiddleLeft, UIFactory.ParchmentBeige);
            UIFactory.AddLayoutElement(staminaLabel.gameObject, preferredWidth: 55);

            var (sBg, sFill) = UIFactory.CreateProgressBar(staminaRow.transform, "StaminaBar", UIFactory.BronzeHighlight);
            staminaBg = sBg;
            staminaFill = sFill;
            UIFactory.AddLayoutElement(sBg.gameObject, 16, flexibleWidth: 1);

            ammoText = UIFactory.CreateText(inner, "Ammo", "", 13, TextAnchor.MiddleLeft, UIFactory.ParchmentBeige);
            UIFactory.AddLayoutElement(ammoText.gameObject, 20);

            experienceText = UIFactory.CreateText(inner, "Experience", "", 13, TextAnchor.MiddleLeft, new Color(0.6f, 0.85f, 1f));
            UIFactory.AddLayoutElement(experienceText.gameObject, 20);

            officerText = UIFactory.CreateText(inner, "Officer", "", 13, TextAnchor.MiddleLeft, UIFactory.GoldAccent);
            UIFactory.AddLayoutElement(officerText.gameObject, 20);

            specialText = UIFactory.CreateText(inner, "Special", "", 13, TextAnchor.MiddleLeft, UIFactory.GoldAccent);
            UIFactory.AddLayoutElement(specialText.gameObject, 20);

            selectionPanel.SetActive(false);
        }

        // ===================== FORMATION PANEL =====================
        private void BuildFormationPanel()
        {
            RectTransform panel = UIFactory.CreateOrnatePanel(canvas.transform, "FormationPanel",
                new Color(0.12f, 0.13f, 0.11f, 0.95f));
            // Use relative anchors - positioned above selection panel
            panel.anchorMin = new Vector2(0.005f, 0.39f);
            panel.anchorMax = new Vector2(0.18f, 0.45f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            formationPanel = panel.gameObject;

            Transform inner = panel.Find("Inner");
            UIFactory.AddHorizontalLayout(inner.gameObject, 4f, new RectOffset(6, 6, 4, 4));

            btnLine = UIFactory.CreateButton(inner, "BtnLine", "Line [F1]", 12, () => SetFormation(FormationType.Line));
            UIFactory.AddLayoutElement(btnLine.gameObject, 32, 64);
            imgLine = btnLine.GetComponent<Image>();

            btnColumn = UIFactory.CreateButton(inner, "BtnColumn", "Col [F2]", 12, () => SetFormation(FormationType.Column));
            UIFactory.AddLayoutElement(btnColumn.gameObject, 32, 64);
            imgColumn = btnColumn.GetComponent<Image>();

            btnSquare = UIFactory.CreateButton(inner, "BtnSquare", "Sq [F3]", 12, () => SetFormation(FormationType.Square));
            UIFactory.AddLayoutElement(btnSquare.gameObject, 32, 64);
            imgSquare = btnSquare.GetComponent<Image>();

            btnSkirmish = UIFactory.CreateButton(inner, "BtnSkirmish", "Skrm [F4]", 12, () => SetFormation(FormationType.Skirmish));
            UIFactory.AddLayoutElement(btnSkirmish.gameObject, 32, 64);
            imgSkirmish = btnSkirmish.GetComponent<Image>();

            formationPanel.SetActive(false);
        }

        // ===================== COMMAND PANEL =====================
        private void BuildCommandPanel()
        {
            RectTransform panel = UIFactory.CreateOrnatePanel(canvas.transform, "CommandPanel",
                new Color(0.12f, 0.13f, 0.11f, 0.95f));
            // Use relative anchors - positioned above formation panel
            panel.anchorMin = new Vector2(0.005f, 0.46f);
            panel.anchorMax = new Vector2(0.18f, 0.52f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            commandPanel = panel.gameObject;

            Transform inner = panel.Find("Inner");
            UIFactory.AddHorizontalLayout(inner.gameObject, 6f, new RectOffset(6, 6, 4, 4));

            // Volley button
            btnVolley = UIFactory.CreateButton(inner, "BtnVolley", "Volley [V]", 12, OnVolleyClicked);
            UIFactory.AddLayoutElement(btnVolley.gameObject, 32, 130);
            volleyGO = btnVolley.gameObject;
            volleyLabel = btnVolley.GetComponentInChildren<Text>();

            // Charge button
            btnCharge = UIFactory.CreateButton(inner, "BtnCharge", "Charge! [C]", 12);
            UIFactory.AddLayoutElement(btnCharge.gameObject, 32, 120);
            chargeGO = btnCharge.gameObject;
            chargeLabel = btnCharge.GetComponentInChildren<Text>();
            chargeLabel.color = UIFactory.CrimsonGlow;

            // Rally button
            btnRally = UIFactory.CreateButton(inner, "BtnRally", "Rally [R]", 12, OnRallyClicked);
            UIFactory.AddLayoutElement(btnRally.gameObject, 32, 100);
            rallyGO = btnRally.gameObject;
            rallyLabel = btnRally.GetComponentInChildren<Text>();
            rallyLabel.color = new Color(0.3f, 0.85f, 0.4f);

            // Cover button
            btnCover = UIFactory.CreateButton(inner, "BtnCover", "Cover [C]", 12, OnCoverClicked);
            UIFactory.AddLayoutElement(btnCover.gameObject, 32, 100);
            coverGO = btnCover.gameObject;
            coverLabel = btnCover.GetComponentInChildren<Text>();
            coverLabel.color = new Color(0.2f, 0.7f, 0.9f); // Cyan for cover

            commandPanel.SetActive(false);
        }

        // ===================== DEPLOYMENT PANEL =====================
        private void BuildDeploymentPanel()
        {
            RectTransform panel = UIFactory.CreateOrnatePanel(canvas.transform, "DeploymentPanel",
                new Color(0.12f, 0.13f, 0.11f, 0.95f));
            // Top center, spanning width - positioned higher
            panel.anchorMin = new Vector2(0.35f, 0.90f);
            panel.anchorMax = new Vector2(0.65f, 1.00f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            deploymentPanel = panel.gameObject;

            Transform inner = panel.Find("Inner");
            UIFactory.AddVerticalLayout(inner.gameObject, 4f, new RectOffset(10, 10, 6, 6));

            // Status text
            deploymentStatusText = UIFactory.CreateText(inner, "StatusText", "Deployment Phase - Position your units", 14, TextAnchor.MiddleCenter, UIFactory.GoldAccent);
            UIFactory.AddLayoutElement(deploymentStatusText.gameObject, 24);

            // Timer text
            Text timerTextLocal = UIFactory.CreateText(inner, "TimerText", "2:00", 12, TextAnchor.MiddleCenter, UIFactory.TextWhite);
            UIFactory.AddLayoutElement(timerTextLocal.gameObject, 20);

            // Button row
            GameObject buttonRow = new GameObject("ButtonRow");
            buttonRow.transform.SetParent(inner, false);
            UIFactory.AddHorizontalLayout(buttonRow, 10f, new RectOffset(0, 0, 0, 0));
            UIFactory.AddLayoutElement(buttonRow, 36, flexibleWidth: 1);

            // Ready button
            btnReady = UIFactory.CreateButton(buttonRow.transform, "BtnReady", "I'm Ready", 14, OnReadyClicked);
            UIFactory.AddLayoutElement(btnReady.gameObject, 36, 120);

            // Start Battle button (only for single player or host)
            btnStartBattle = UIFactory.CreateButton(buttonRow.transform, "BtnStartBattle", "Start Battle", 14, OnStartBattleClicked);
            UIFactory.AddLayoutElement(btnStartBattle.gameObject, 36, 120);
            var startBtnImage = btnStartBattle.GetComponent<Image>();
            startBtnImage.color = new Color(0.2f, 0.7f, 0.3f); // Green for start

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

        // ===================== CONTROLS HELP =====================
        private void BuildControlsHelp()
        {
            RectTransform panel = UIFactory.CreateOrnatePanel(canvas.transform, "ControlsPanel",
                new Color(0.12f, 0.13f, 0.11f, 0.95f));
            // Use relative anchors - bottom right corner
            panel.anchorMin = new Vector2(0.85f, 0.02f);
            panel.anchorMax = new Vector2(0.995f, 0.30f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            controlsPanel = panel.gameObject;

            Transform inner = panel.Find("Inner");
            UIFactory.AddVerticalLayout(inner.gameObject, 1f, new RectOffset(10, 10, 6, 6));

            Text header = UIFactory.CreateSectionHeader(inner, "Header", "Controls", 14);
            UIFactory.AddLayoutElement(header.transform.parent.gameObject, 24);

            string[] controls = {
                "WASD / Arrows — Pan camera",
                "Q / E — Rotate camera",
                "Scroll — Zoom in/out",
                "Left Click — Select units",
                "Right Click — Move (in your zone)",
                "F1-F4 — Formations",
                "V — Toggle Volley Fire",
                "C — Charge (cavalry)",
                "R — Rally fleeing units",
                "1-4 — Battle speed",
                "Esc — Pause menu",
                "",
                "DEPLOYMENT PHASE:",
                "- Stay on your side of the line",
                "- Position units strategically",
                "- Click 'I'm Ready' when done"
            };

            foreach (string ctrl in controls)
            {
                Text t = UIFactory.CreateText(inner, "Ctrl", ctrl, 11, TextAnchor.MiddleLeft, UIFactory.TextGrey);
                UIFactory.AddLayoutElement(t.gameObject, 18);
            }
        }

        // ===================== UPDATE METHODS =====================
        private void UpdateTopBar()
        {
            if (BattleManager.Instance == null) return;
            var (playerAlive, enemyAlive) = BattleManager.Instance.GetArmyCounts();
            playerCountText.text = $"France: {playerAlive}";
            enemyCountText.text = $"Britain: {enemyAlive}";
        }

        private void UpdateSelectionPanel()
        {
            bool hasSelection = SelectionManager.Instance != null && SelectionManager.Instance.HasSelection;
            var regiments = hasSelection ? SelectionManager.Instance.SelectedRegiments : null;
            bool show = hasSelection && regiments != null && regiments.Count > 0;

            selectionPanel.SetActive(show);
            formationPanel.SetActive(show);
            commandPanel.SetActive(show);

            if (!show) return;

            selectionHeaderText.text = $"Selected: {regiments.Count} regiment(s)";

            int totalAlive = 0;
            foreach (var reg in regiments) totalAlive += reg.CachedAliveCount;
            unitsAliveText.text = $"Units alive: {totalAlive}";

            if (regiments.Count == 1)
            {
                var reg = regiments[0];

                unitTypeText.gameObject.SetActive(true);
                formationText.gameObject.SetActive(true);
                moraleLabel.transform.parent.gameObject.SetActive(true);
                staminaLabel.transform.parent.gameObject.SetActive(true);
                ammoText.gameObject.SetActive(true);
                experienceText.gameObject.SetActive(true);
                officerText.gameObject.SetActive(true);
                specialText.gameObject.SetActive(true);

                unitTypeText.text = reg.UnitData != null ? $"Type: {reg.UnitData.unitName}" : "";
                formationText.text = $"Formation: {reg.CurrentFormation}";

                // Morale
                float morale = reg.CachedAverageMorale;
                float maxMorale = reg.UnitData != null ? reg.UnitData.maxMorale : 100f;
                float moralePercent = Mathf.Clamp01(morale / maxMorale);
                moraleFill.color = GetMoraleColor(moralePercent);
                moraleFill.GetComponent<RectTransform>().anchorMax = new Vector2(moralePercent, 1f);

                // Stamina
                float staminaPercent = reg.CachedAverageStamina;
                staminaFill.color = staminaPercent > 0.3f ? new Color(0.9f, 0.7f, 0.1f) : new Color(0.9f, 0.2f, 0.1f);
                staminaFill.GetComponent<RectTransform>().anchorMax = new Vector2(staminaPercent, 1f);

                // Ammo
                int totalAmmo = reg.CachedTotalAmmo;
                bool unlimited = reg.UnitData != null && reg.UnitData.hasUnlimitedAmmo;
                ammoText.text = unlimited ? "Ammo: \u221E" : $"Ammo: {totalAmmo}";
                ammoText.color = (!unlimited && totalAmmo <= 0) ? new Color(0.9f, 0.2f, 0.1f) : Color.white;

                // Rank & Experience (10-rank system)
                RegimentRank regRank = reg.RegimentRankEnum;
                UnitType uType = reg.UnitData != null ? reg.UnitData.unitType : UnitType.LineInfantry;
                string rankName = RegimentRankSystem.GetRankName(regRank, uType);
                float progress = RegimentRankSystem.GetRankProgress(reg.RegimentExperience);
                experienceText.text = (int)regRank >= RegimentRankSystem.TotalRanks - 1
                    ? $"{rankName} (MAX)"
                    : $"{rankName} [{progress * 100:F0}%]";
                experienceText.color = RegimentRankSystem.GetRankColor(regRank);

                // Officer
                officerText.text = reg.OfficerAlive ? "\u2605 Officer: Alive" : "\u2606 Officer: KIA";
                officerText.color = reg.OfficerAlive ? new Color(0.3f, 1f, 0.3f) : new Color(0.9f, 0.2f, 0.1f);

                // Special ability text
                if (reg.UnitData != null && reg.UnitData.canVolleyFire)
                {
                    specialText.text = reg.IsVolleyMode ? "VOLLEY: ON" : "Volley: off";
                    specialText.color = reg.IsVolleyMode ? UIFactory.GoldAccent : UIFactory.TextGrey;
                }
                else if (reg.UnitData != null && reg.UnitData.canCharge)
                {
                    specialText.text = "Can CHARGE (C)";
                    specialText.color = new Color(1f, 0.5f, 0.2f);
                }
                else
                {
                    specialText.text = "";
                }
            }
            else
            {
                unitTypeText.gameObject.SetActive(false);
                formationText.gameObject.SetActive(false);
                moraleLabel.transform.parent.gameObject.SetActive(false);
                staminaLabel.transform.parent.gameObject.SetActive(false);
                ammoText.gameObject.SetActive(false);
                experienceText.gameObject.SetActive(false);
                officerText.gameObject.SetActive(false);
                specialText.gameObject.SetActive(false);
            }
        }

        private void UpdateFormationButtons()
        {
            if (!formationPanel.activeSelf) return;

            var regs = SelectionManager.Instance.SelectedRegiments;
            FormationType? currentForm = null;
            if (regs.Count == 1) currentForm = regs[0].CurrentFormation;

            imgLine.color = currentForm == FormationType.Line ? UIFactory.ButtonActive : UIFactory.ButtonNormal;
            imgColumn.color = currentForm == FormationType.Column ? UIFactory.ButtonActive : UIFactory.ButtonNormal;
            imgSquare.color = currentForm == FormationType.Square ? UIFactory.ButtonActive : UIFactory.ButtonNormal;
            imgSkirmish.color = currentForm == FormationType.Skirmish ? UIFactory.ButtonActive : UIFactory.ButtonNormal;
        }

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
