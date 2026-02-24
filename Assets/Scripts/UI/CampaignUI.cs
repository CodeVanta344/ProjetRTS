using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Campaign;
using NapoleonicWars.Campaign.UI;
using NapoleonicWars.Core;
using NapoleonicWars.Data;
using NapoleonicWars.Units;
using System.Reflection;

namespace NapoleonicWars.UI
{
    public class CampaignUI : MonoBehaviour
    {
        public static CampaignUI Instance { get; private set; }
        
        private Canvas canvas;

        private CampaignMap3D map3D;

        // === HoI4 UI Components ===
        private HoI4TopBar hoi4TopBar;
        private NavigationBar navBar;
        private BottomBar bottomBar;

        // Old top bar (kept for compatibility, hidden)
        private Text turnText;

        // Resource bar (replaced by HoI4TopBar)
        private Text goldText, foodText, ironText, provCountText, armyCountText;

        // Map
        private RectTransform mapPanel;
        private Texture2D mapTexture;
        private int mapRes = 512;

        // Province panel
        private GameObject provincePanel;
        private Text provNameText, provOwnerText, provPopText;
        private Text provGoldText, provFoodText, provIronText;
        private Transform buildingsContainer;
        private Text moveHintText;

        // Army panel
        private GameObject armyPanel;
        private Text armyNameText, armySoldiersText, armyMoveText, armyUpkeepText;
        private Transform regimentsContainer;
        private GameObject recruitRow;

        // Diplomacy overlay
        private GameObject diplomacyPanel;
        private Transform diplomacyContainer;

        // Build menu overlay
        private GameObject buildMenuPanel;
        private Transform buildMenuContainer;
        
        // Intelligence panel
        private GameObject intelligencePanel;
        
        // Research panel
        private GameObject researchPanel;
        
        // ==================== CITY MANAGEMENT PANEL ====================
        private GameObject cityPanel;
        private Text cityNameText, cityLevelText;
        private GameObject cityOverviewTab, cityBuildingsTab, cityMilitaryTab, cityIndustryTab;
        private Transform cityOverviewContent, cityBuildingsContent, cityMilitaryContent, cityIndustryContent;
        private int activeCityTab = 0; // 0=Overview, 1=Buildings, 2=Military, 3=Industry
        private Button[] cityTabButtons;
        private ProvinceData openCityProvince = null;
        private bool cityPanelDirty = false;

        public string selectedProvinceId = null;
        private string selectedArmyId = null;
        private int buildSlotIndex = 0;
        
        // ===== DIRTY CHECKING SYSTEM =====
        private bool provincePanelDirty = true;
        private bool armyPanelDirty = true;
        private string lastProvinceId = null;
        private string lastArmyId = null;
        private int lastTurn = -1;
        private float lastGold = -1f, lastFood = -1f, lastIron = -1f;
        private int lastProvinceCount = -1, lastArmyCount = -1;

        private void Start()
        {
            BuildUI();
            Initialize3DMap();
        }
        

        
        private void Initialize3DMap()
        {
            // Find or create the 3D map
            map3D = FindAnyObjectByType<CampaignMap3D>();
            
            if (map3D == null)
            {
                GameObject mapGO = new GameObject("CampaignMap3D");
                map3D = mapGO.AddComponent<CampaignMap3D>();
            }
            
            // Subscribe to city click events
            map3D.OnCityClicked += OnCityClicked;
            
            // Subscribe to army events
            map3D.OnArmySelected += OnArmySelected;
            map3D.OnArmyMoveOrdered += OnArmyMoveOrdered;
            map3D.OnBattleTriggered += OnBattleTriggered;
        }
        
        private void OnArmySelected(ArmyData army)
        {
            selectedArmyId = army?.armyId;
            armyPanelDirty = true;
            
            // Also select the province where the army is
            if (army != null && !string.IsNullOrEmpty(army.currentProvinceId))
            {
                selectedProvinceId = army.currentProvinceId;
                provincePanelDirty = true;
            }
            
            Debug.Log($"[CampaignUI] Army selected: {army?.armyName}");
        }
        
        private void OnArmyMoveOrdered(ArmyData army, ProvinceData targetProvince)
        {
            // Update UI after army movement
            selectedProvinceId = targetProvince?.provinceId;
            provincePanelDirty = true;
            armyPanelDirty = true;
            
            Debug.Log($"[CampaignUI] Army {army?.armyName} moved to {targetProvince?.provinceName}");
        }
        
        private void OnBattleTriggered(ArmyData attacker, ArmyData defender, ProvinceData province)
        {
            Debug.Log($"[CampaignUI] Battle triggered in {province?.provinceName}!");
            
            // Show battle preparation UI
            ShowBattlePreparation(attacker, defender, province);
        }
        
        private void OnCityClicked(ProvinceData province)
        {
            if (CampaignManager.Instance == null) return;
            
            selectedProvinceId = province.provinceId;
            provincePanelDirty = true;
            
            // Only open city panel for player's cities
            if (province.owner != CampaignManager.Instance.PlayerFaction)
            {
                Debug.Log($"[CampaignUI] Cannot manage {province.provinceName} - owned by {province.owner}");
                CloseCityPanel();
                return;
            }
            
            // Open city management panel — pass province directly
            OpenCityPanel(province);
        }

        private void Update()
        {
            if (CampaignManager.Instance == null) return;

            // Only update top bar when turn changes
            if (CampaignManager.Instance.CurrentTurn != lastTurn)
            {
                if (hoi4TopBar != null) hoi4TopBar.UpdateDisplay();
                if (bottomBar != null) bottomBar.UpdateDisplay();
                lastTurn = CampaignManager.Instance.CurrentTurn;
            }

            // Also update top bar when resources change
            FactionData fd = CampaignManager.Instance.GetPlayerFaction();
            if (fd != null)
            {
                if (fd.gold != lastGold || fd.food != lastFood || fd.iron != lastIron ||
                    fd.ownedProvinceIds.Count != lastProvinceCount || fd.armyIds.Count != lastArmyCount)
                {
                    if (hoi4TopBar != null) hoi4TopBar.UpdateDisplay();
                    if (bottomBar != null) bottomBar.UpdateDisplay();
                    lastGold = fd.gold;
                    lastFood = fd.food;
                    lastIron = fd.iron;
                    lastProvinceCount = fd.ownedProvinceIds.Count;
                    lastArmyCount = fd.armyIds.Count;
                    provincePanelDirty = true;
                    armyPanelDirty = true;
                }
            }

            // Only update province panel when dirty or selection changed
            if (provincePanelDirty || selectedProvinceId != lastProvinceId)
            {
                UpdateProvincePanel();
                lastProvinceId = selectedProvinceId;
                provincePanelDirty = false;
            }

            // Only update army panel when dirty or selection changed
            if (armyPanelDirty || selectedArmyId != lastArmyId)
            {
                UpdateArmyPanel();
                lastArmyId = selectedArmyId;
                armyPanelDirty = false;
            }
            
            // Update city panel when dirty
            if (cityPanelDirty && cityPanel != null && cityPanel.activeSelf)
            {
                RefreshCityPanel();
                cityPanelDirty = false;
            }
            
            // Handle hotkeys
            HandleHotkeys();
            
            // ESC to close city panel
            if (Input.GetKeyDown(KeyCode.Escape) && cityPanel != null && cityPanel.activeSelf)
            {
                CloseCityPanel();
            }
        }
        
        private void HandleHotkeys()
        {
            if (InputManager.Instance == null) return;
            
            // Toggle Intelligence Panel with 'I' key
            if (InputManager.Instance.IntelligencePanel)
            {
                ToggleIntelligencePanel();
            }
            
            // Toggle Research Panel with 'T' key
            if (InputManager.Instance.ResearchPanel)
            {
                ToggleResearchPanel();
            }
        }
        
        private void ToggleResearchPanel()
        {
            if (researchPanel == null)
            {
                CreateResearchPanel();
            }
            
            if (researchPanel != null)
            {
                bool willShow = !researchPanel.activeSelf;
                researchPanel.SetActive(willShow);
                
                if (willShow)
                {
                    ResearchAssignmentPanel rp = researchPanel.GetComponent<ResearchAssignmentPanel>();
                    rp?.Show();
                }
            }
        }
        
        private void CreateResearchPanel()
        {
            GameObject panelGO = new GameObject("ResearchAssignmentPanelUI");
            panelGO.transform.SetParent(transform);
            
            ResearchAssignmentPanel rp = panelGO.AddComponent<ResearchAssignmentPanel>();
            
            researchPanel = panelGO;
            panelGO.SetActive(false);
        }
        
        private void ToggleIntelligencePanel()
        {
            if (intelligencePanel == null)
            {
                CreateIntelligencePanel();
            }
            
            if (intelligencePanel != null)
            {
                bool willShow = !intelligencePanel.activeSelf;
                intelligencePanel.SetActive(willShow);
                
                if (willShow)
                {
                    // Refresh when showing
                    IntelligencePanel ip = intelligencePanel.GetComponent<IntelligencePanel>();
                    ip?.Show();
                }
            }
        }
        
        private void CreateIntelligencePanel()
        {
            // Create panel GameObject and add IntelligencePanel component
            GameObject panelGO = new GameObject("IntelligencePanelUI");
            panelGO.transform.SetParent(transform);
            
            IntelligencePanel ip = panelGO.AddComponent<IntelligencePanel>();
            
            // Set references if needed
            intelligencePanel = panelGO;
            panelGO.SetActive(false);
        }

        private void BuildUI()
        {
            canvas = UIFactory.CreateCanvas("CampaignCanvas", 10);
            canvas.transform.SetParent(transform);

            // === HoI4 TOP BAR (replaces old top bar + resource bar) ===
            if (CampaignManager.Instance != null)
            {
                GameObject topBarGO = new GameObject("HoI4TopBar");
                topBarGO.transform.SetParent(canvas.transform, false);
                RectTransform topBarRT = topBarGO.AddComponent<RectTransform>();
                topBarRT.anchorMin = Vector2.zero;
                topBarRT.anchorMax = Vector2.one;
                topBarRT.offsetMin = Vector2.zero;
                topBarRT.offsetMax = Vector2.zero;
                hoi4TopBar = topBarGO.AddComponent<HoI4TopBar>();
                hoi4TopBar.Initialize(CampaignManager.Instance);
            }

            // === HoI4 NAVIGATION BAR ===
            GameObject navBarGO = new GameObject("NavigationBar");
            navBarGO.transform.SetParent(canvas.transform, false);
            RectTransform navBarRT = navBarGO.AddComponent<RectTransform>();
            navBarRT.anchorMin = Vector2.zero;
            navBarRT.anchorMax = Vector2.one;
            navBarRT.offsetMin = Vector2.zero;
            navBarRT.offsetMax = Vector2.zero;
            navBar = navBarGO.AddComponent<NavigationBar>();
            
            // Panel container fills the screen (panels position themselves)
            RectTransform container = canvas.GetComponent<RectTransform>();
            navBar.Initialize(container);
            navBar.onPanelRequested = OnNavPanelRequested;

            // Keep legacy panels for province/army/diplomacy/build (they're still useful)
            BuildMapPanel();
            // BuildProvincePanel(); — OBSOLETE: province info shown via CityUIPanel now
            BuildArmyPanel();
            // DiplomacyUI now creates itself on demand via DiplomacyUI.Create()
            BuildBuildMenu();

            // === BOTTOM BAR (Total War style — end turn, faction, events) ===
            if (CampaignManager.Instance != null)
            {
                GameObject bottomBarGO = new GameObject("BottomBar");
                bottomBarGO.transform.SetParent(canvas.transform, false);
                RectTransform bottomBarRT = bottomBarGO.AddComponent<RectTransform>();
                bottomBarRT.anchorMin = Vector2.zero;
                bottomBarRT.anchorMax = Vector2.one;
                bottomBarRT.offsetMin = Vector2.zero;
                bottomBarRT.offsetMax = Vector2.zero;
                bottomBar = bottomBarGO.AddComponent<BottomBar>();
                bottomBar.Initialize(CampaignManager.Instance);
                // Speed controls are handled internally by BottomBar ↔ CampaignClock
                // Keep legacy callback for save/load compatibility
                bottomBar.OnEndTurnClicked = () => CampaignManager.Instance.EndPlayerTurn();
            }
        }

        /// <summary>
        /// Called when a NavBar button is clicked. Creates the corresponding overlay panel.
        /// </summary>
        private void OnNavPanelRequested(NavigationBar.NavPanel panel)
        {
            if (CampaignManager.Instance == null) return;
            
            switch (panel)
            {
                case NavigationBar.NavPanel.Laws:
                    NationalLawsUI.Create(navBar, CampaignManager.Instance);
                    break;
                case NavigationBar.NavPanel.Production:
                    ProductionUI.Create(navBar, CampaignManager.Instance);
                    break;
                case NavigationBar.NavPanel.Research:
                    ResearchTreeUI.Create(navBar, CampaignManager.Instance);
                    break;
                case NavigationBar.NavPanel.Construction:
                    ConstructionUI.Create(navBar, CampaignManager.Instance);
                    break;
                case NavigationBar.NavPanel.Military:
                    MilitaryUI.Create(navBar, CampaignManager.Instance);
                    break;
                case NavigationBar.NavPanel.Logistics:
                    LogisticsUI.Create(navBar, CampaignManager.Instance);
                    break;
                case NavigationBar.NavPanel.Diplomacy:
                    DiplomacyUI.Create(navBar, CampaignManager.Instance);
                    break;
                default:
                    Debug.Log($"[CampaignUI] Panel not yet implemented: {panel}");
                    break;
            }
        }

        // Old BuildTopBar/BuildResourceBar removed — replaced by HoI4TopBar and BottomBar

        // ===================== MAP =====================
        private void BuildMapPanel()
        {
            // 2D Map is hidden - using 3D map instead
            // Create a small invisible placeholder for compatibility
            mapPanel = new GameObject("MapPanelPlaceholder").AddComponent<RectTransform>();
            mapPanel.SetParent(canvas.transform);
            mapPanel.gameObject.SetActive(false);
            
            // Keep reference but don't use it
            mapTexture = null;
        }

        // ===================== PROVINCE PANEL =====================
        private void BuildProvincePanel()
        {
            RectTransform panel = UIFactory.CreateOrnatePanel(canvas.transform, "ProvincePanel",
                new Color(0.12f, 0.13f, 0.11f, 0.95f));
            // Position on right side — below top bar, above bottom bar
            panel.anchorMin = new Vector2(0.78f, 0.55f);
            panel.anchorMax = new Vector2(0.99f, 0.95f);
            panel.offsetMin = new Vector2(0, 0);
            panel.offsetMax = new Vector2(0, 0);
            provincePanel = panel.gameObject;

            // Navigate to inner content in OrnatePanel: Outer > GoldBorder > InnerDark > Inner
            Transform inner = panel.Find("Inner");
            UIFactory.AddVerticalLayout(inner.gameObject, 2f, new RectOffset(10, 10, 8, 8));

            provNameText = UIFactory.CreateText(inner, "Name", "Select a province", 17, TextAnchor.MiddleCenter, UIFactory.GoldAccent);
            provNameText.fontStyle = FontStyle.Bold;
            Shadow nameShadow = provNameText.gameObject.AddComponent<Shadow>();
            nameShadow.effectColor = new Color(0, 0, 0, 0.7f);
            nameShadow.effectDistance = new Vector2(1f, -1f);
            UIFactory.AddLayoutElement(provNameText.gameObject, 26);

            RectTransform sep = UIFactory.CreateOrnamentalSeparator(inner);
            UIFactory.AddLayoutElement(sep.gameObject, 14);

            provOwnerText = UIFactory.CreateText(inner, "Owner", "", 13, TextAnchor.MiddleLeft, UIFactory.ParchmentBeige);
            UIFactory.AddLayoutElement(provOwnerText.gameObject, 20);

            provPopText = UIFactory.CreateText(inner, "Pop", "", 13, TextAnchor.MiddleLeft, UIFactory.ParchmentBeige);
            UIFactory.AddLayoutElement(provPopText.gameObject, 20);

            provGoldText = UIFactory.CreateText(inner, "Gold", "", 13, TextAnchor.MiddleLeft, UIFactory.GoldAccent);
            UIFactory.AddLayoutElement(provGoldText.gameObject, 20);

            provFoodText = UIFactory.CreateText(inner, "Food", "", 13, TextAnchor.MiddleLeft, new Color(0.5f, 0.80f, 0.30f));
            UIFactory.AddLayoutElement(provFoodText.gameObject, 20);

            provIronText = UIFactory.CreateText(inner, "Iron", "", 13, TextAnchor.MiddleLeft, new Color(0.6f, 0.6f, 0.65f));
            UIFactory.AddLayoutElement(provIronText.gameObject, 20);

            Text bldHeader = UIFactory.CreateSectionHeader(inner, "BldHeader", "Buildings:", 13);
            UIFactory.AddLayoutElement(bldHeader.transform.parent.gameObject, 22);

            // Buildings scroll area
            var (scroll, content) = UIFactory.CreateScrollView(inner, "BuildingsScroll");
            UIFactory.AddLayoutElement(scroll.gameObject, 100, flexibleWidth: 1);
            buildingsContainer = content;

            moveHintText = UIFactory.CreateText(inner, "MoveHint", "", 11, TextAnchor.MiddleCenter, UIFactory.TextGrey);
            moveHintText.fontStyle = FontStyle.Italic;
            UIFactory.AddLayoutElement(moveHintText.gameObject, 18);
        }

        // ===================== ARMY PANEL =====================
        private void BuildArmyPanel()
        {
            RectTransform panel = UIFactory.CreateOrnatePanel(canvas.transform, "ArmyPanel",
                new Color(0.12f, 0.13f, 0.11f, 0.95f));
            // Position below province panel, above bottom bar
            panel.anchorMin = new Vector2(0.78f, 0.10f);
            panel.anchorMax = new Vector2(0.99f, 0.52f);
            panel.offsetMin = new Vector2(0, 0);
            panel.offsetMax = new Vector2(0, 0);
            armyPanel = panel.gameObject;

            // Navigate to inner content in OrnatePanel
            Transform inner = panel.Find("Inner");
            UIFactory.AddVerticalLayout(inner.gameObject, 2f, new RectOffset(10, 10, 8, 8));

            armyNameText = UIFactory.CreateText(inner, "Name", "No army selected", 16, TextAnchor.MiddleLeft, UIFactory.GoldAccent);
            armyNameText.fontStyle = FontStyle.Bold;
            Shadow armyShadow = armyNameText.gameObject.AddComponent<Shadow>();
            armyShadow.effectColor = new Color(0, 0, 0, 0.7f);
            armyShadow.effectDistance = new Vector2(1f, -1f);
            UIFactory.AddLayoutElement(armyNameText.gameObject, 24);

            RectTransform sep = UIFactory.CreateOrnamentalSeparator(inner);
            UIFactory.AddLayoutElement(sep.gameObject, 14);

            armySoldiersText = UIFactory.CreateText(inner, "Soldiers", "", 13, TextAnchor.MiddleLeft, UIFactory.ParchmentBeige);
            UIFactory.AddLayoutElement(armySoldiersText.gameObject, 20);

            armyMoveText = UIFactory.CreateText(inner, "Move", "", 13, TextAnchor.MiddleLeft, UIFactory.ParchmentBeige);
            UIFactory.AddLayoutElement(armyMoveText.gameObject, 20);

            armyUpkeepText = UIFactory.CreateText(inner, "Upkeep", "", 13, TextAnchor.MiddleLeft, UIFactory.GoldAccent);
            UIFactory.AddLayoutElement(armyUpkeepText.gameObject, 20);

            Text regHeader = UIFactory.CreateSectionHeader(inner, "RegHeader", "Regiments:", 13);
            UIFactory.AddLayoutElement(regHeader.transform.parent.gameObject, 22);

            var (scroll, content) = UIFactory.CreateScrollView(inner, "RegimentsScroll");
            UIFactory.AddLayoutElement(scroll.gameObject, 80, flexibleWidth: 1);
            regimentsContainer = content;

            // Recruit row
            recruitRow = new GameObject("RecruitRow");
            recruitRow.transform.SetParent(inner, false);
            recruitRow.AddComponent<RectTransform>();
            UIFactory.AddHorizontalLayout(recruitRow, 4f);
            UIFactory.AddLayoutElement(recruitRow, 32);

            Button btnInf = UIFactory.CreateGoldButton(recruitRow.transform, "BtnInf", "Recruit Infantry", 11, () =>
                CampaignManager.Instance.RecruitRegiment(selectedArmyId, UnitType.LineInfantry));
            UIFactory.AddLayoutElement(btnInf.gameObject, 28, flexibleWidth: 1);

            Button btnCav = UIFactory.CreateGoldButton(recruitRow.transform, "BtnCav", "Recruit Cavalry", 11, () =>
                CampaignManager.Instance.RecruitRegiment(selectedArmyId, UnitType.Cavalry));
            UIFactory.AddLayoutElement(btnCav.gameObject, 28, flexibleWidth: 1);

            armyPanel.SetActive(false);
        }

        // ===================== DIPLOMACY =====================
        private void BuildDiplomacyPanel()
        {
            RectTransform panel = UIFactory.CreateOrnatePanel(canvas.transform, "DiplomacyPanel",
                new Color(0.07f, 0.05f, 0.04f, 0.97f));
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(420, 320);
            diplomacyPanel = panel.gameObject;

            Transform inner = panel.Find("Inner");
            UIFactory.AddVerticalLayout(inner.gameObject, 4f, new RectOffset(15, 15, 12, 12));

            // Banner header
            RectTransform titleBanner = UIFactory.CreateBannerHeader(inner, "TitleBanner", "DIPLOMACY", 20);
            UIFactory.AddLayoutElement(titleBanner.gameObject, 40);

            var (scroll, content) = UIFactory.CreateScrollView(inner, "DiploScroll");
            UIFactory.AddLayoutElement(scroll.gameObject, 190, flexibleWidth: 1);
            diplomacyContainer = content;

            Button btnClose = UIFactory.CreateGoldButton(inner, "BtnClose", "Close", 13, () => diplomacyPanel.SetActive(false));
            UIFactory.AddLayoutElement(btnClose.gameObject, 30);

            diplomacyPanel.SetActive(false);
        }

        // ===================== BUILD MENU =====================
        private void BuildBuildMenu()
        {
            RectTransform panel = UIFactory.CreateBorderedPanel(canvas.transform, "BuildMenuPanel",
                new Color(0.06f, 0.06f, 0.07f, 0.97f), UIFactory.MutedGold, 2f);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(520, 580);
            buildMenuPanel = panel.gameObject;

            var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 0f;
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;

            // Premium header
            RectTransform headerBg = UIFactory.CreateGradientPanel(panel, "HeaderBanner",
                new Color(0.14f, 0.12f, 0.10f), new Color(0.06f, 0.06f, 0.07f));
            UIFactory.AddLayoutElement(headerBg.gameObject, preferredHeight: 50);
            UIFactory.AddHorizontalLayout(headerBg.gameObject, 10f, new RectOffset(20, 20, 10, 10));
            
            Text embl = UIFactory.CreateText(headerBg, "Embl", "⚒", 24, TextAnchor.MiddleCenter, UIFactory.EmpireGold);
            UIFactory.AddLayoutElement(embl.gameObject, preferredWidth: 36);
            Text title = UIFactory.CreateText(headerBg, "Title", "CONSTRUCTION", 20, TextAnchor.MiddleLeft, UIFactory.EmpireGold);
            title.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(title.gameObject, flexibleWidth: 1);

            RectTransform hSep = UIFactory.CreateGlowSeparator(panel, "HSep", false);
            UIFactory.AddLayoutElement(hSep.gameObject, preferredHeight: 3);
            
            // Scrollable content area with padding
            GameObject contentPad = new GameObject("ContentPad");
            contentPad.transform.SetParent(panel, false);
            contentPad.AddComponent<RectTransform>();
            var cpvlg = contentPad.AddComponent<VerticalLayoutGroup>();
            cpvlg.spacing = 0f;
            cpvlg.padding = new RectOffset(14, 14, 8, 8);
            cpvlg.childControlHeight = true;
            cpvlg.childForceExpandHeight = true;
            cpvlg.childControlWidth = true;
            cpvlg.childForceExpandWidth = true;
            UIFactory.AddLayoutElement(contentPad, flexibleHeight: 1, flexibleWidth: 1);

            var (scroll, content) = UIFactory.CreateScrollView(contentPad.transform, "BuildScroll");
            UIFactory.AddLayoutElement(scroll.gameObject, 100, flexibleWidth: 1, flexibleHeight: 1);
            buildMenuContainer = content;
            
            // Bottom bar
            GameObject bottomBar = new GameObject("BottomBar");
            bottomBar.transform.SetParent(panel, false);
            bottomBar.AddComponent<RectTransform>();
            Image bbBg = bottomBar.AddComponent<Image>();
            bbBg.color = new Color(0.05f, 0.05f, 0.06f);
            UIFactory.AddHorizontalLayout(bottomBar, 0f, new RectOffset(14, 14, 8, 8));
            UIFactory.AddLayoutElement(bottomBar, preferredHeight: 44);
            
            Button btnCancel = UIFactory.CreateButton(bottomBar.transform, "BtnCancel", "✕ ANNULER", 13, 
                () => buildMenuPanel.SetActive(false));
            UIFactory.AddLayoutElement(btnCancel.gameObject, flexibleWidth: 1, preferredHeight: 30);
            btnCancel.GetComponent<Image>().color = new Color(0.15f, 0.08f, 0.08f);

            buildMenuPanel.SetActive(false);
        }

        // Old UpdateTopBar/UpdateResourceBar removed — replaced by HoI4TopBar and BottomBar

        private void UpdateProvincePanel()
        {
            if (provincePanel == null) return; // Panel not built (obsolete)
            provincePanel.SetActive(true);
            
            if (string.IsNullOrEmpty(selectedProvinceId) ||
                !CampaignManager.Instance.Provinces.ContainsKey(selectedProvinceId))
            {
                provNameText.text = "Select a province";
                provOwnerText.text = "";
                provPopText.text = "";
                provGoldText.text = "";
                provFoodText.text = "";
                provIronText.text = "";
                moveHintText.text = "";
                return;
            }

            ProvinceData prov = CampaignManager.Instance.Provinces[selectedProvinceId];
            provNameText.text = prov.provinceName;
            provOwnerText.text = $"Owner: {prov.owner}";
            provOwnerText.color = GetFactionColor(prov.owner);
            provPopText.text = $"👥 {prov.population:N0}";
            provGoldText.text = $"💰 +{prov.goldIncome:F0}";
            provFoodText.text = $"🌾 +{prov.foodProduction:F0}";
            provIronText.text = $"⚒ +{prov.ironProduction:F0}";

            // Rebuild buildings list
            foreach (Transform child in buildingsContainer)
                Destroy(child.gameObject);

            for (int i = 0; i < prov.buildings.Length; i++)
            {
                BuildingSlot slot = prov.buildings[i];
                string bName;
                Color nameColor = UIFactory.ParchmentBeige;
                
                if (slot.type == BuildingType.Empty)
                {
                    bName = "Empty Slot";
                    nameColor = Color.gray;
                }
                else if (slot.isConstructing)
                {
                    bName = $"{BuildingInfo.GetName(slot.type)} ({slot.turnsToComplete}t)";
                    nameColor = UIFactory.GoldAccent;
                }
                else
                {
                    bName = $"{BuildingInfo.GetName(slot.type)}";
                }

                GameObject row = new GameObject("BldRow");
                row.transform.SetParent(buildingsContainer, false);
                row.AddComponent<RectTransform>();
                UIFactory.AddHorizontalLayout(row, 4f);
                UIFactory.AddLayoutElement(row, 24);

                // Bullet point or icon
                string icon = slot.type == BuildingType.Empty ? "▪" : "🏰";
                Text iconT = UIFactory.CreateText(row.transform, "Icon", icon, 12, TextAnchor.MiddleCenter, nameColor);
                UIFactory.AddLayoutElement(iconT.gameObject, 15);

                Text label = UIFactory.CreateText(row.transform, "Label", bName, 12, TextAnchor.MiddleLeft, nameColor);
                if (slot.type != BuildingType.Empty) label.fontStyle = FontStyle.Bold;
                UIFactory.AddLayoutElement(label.gameObject, flexibleWidth: 1);

                if (prov.owner == CampaignManager.Instance.PlayerFaction && !slot.isConstructing && slot.type == BuildingType.Empty)
                {
                    int slotIdx = i;
                    Button btn = UIFactory.CreateGoldButton(row.transform, "BtnBuild", "+", 12, () =>
                    {
                        buildSlotIndex = slotIdx;
                        ShowBuildMenu();
                    });
                    UIFactory.AddLayoutElement(btn.gameObject, 24, 24);
                }
            }

            moveHintText.text = selectedArmyId != null && prov.owner == CampaignManager.Instance.PlayerFaction
                ? "Right-click neighbor to move army" : "";
        }

        private void UpdateArmyPanel()
        {
            if (string.IsNullOrEmpty(selectedArmyId) ||
                !CampaignManager.Instance.Armies.ContainsKey(selectedArmyId))
            {
                armyPanel.SetActive(false);
                return;
            }

            armyPanel.SetActive(true);
            ArmyData army = CampaignManager.Instance.Armies[selectedArmyId];

            armyNameText.text = army.armyName;
            armySoldiersText.text = $"Total: {army.TotalSoldiers} soldiers";
            armyMoveText.text = $"Movement: {army.movementPoints}/{army.maxMovementPoints}";
            armyUpkeepText.text = $"Upkeep: {army.MaintenanceCost:F0} gold/turn";

            // Rebuild regiments list with visual cards
            foreach (Transform child in regimentsContainer)
                Destroy(child.gameObject);

            for (int i = 0; i < army.regiments.Count; i++)
            {
                CreateRegimentCard(regimentsContainer, army.regiments[i], i, army.armyId);
            }

            recruitRow.SetActive(army.faction == CampaignManager.Instance.PlayerFaction);
        }
        
        /// <summary>
        /// Create a visual card for a regiment showing unit type, stats, and reinforcement status
        /// </summary>
        private void CreateRegimentCard(Transform parent, RegimentData reg, int regimentIndex, string armyId)
        {
            // Card container
            GameObject card = new GameObject($"RegimentCard_{reg.regimentName}");
            card.transform.SetParent(parent, false);
            RectTransform cardRect = card.AddComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(0, reg.needsReinforcement ? 105 : 85);
            
            // Background image
            Image bgImage = card.AddComponent<Image>();
            bgImage.color = reg.isBeingReinforced 
                ? new Color(0.2f, 0.15f, 0.08f, 0.95f)  // Yellowish tint for reinforcing
                : new Color(0.16f, 0.18f, 0.15f, 0.9f);
            
            // Add border
            GameObject border = new GameObject("Border");
            border.transform.SetParent(card.transform, false);
            RectTransform borderRect = border.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;
            Image borderImg = border.AddComponent<Image>();
            borderImg.color = reg.isBeingReinforced 
                ? new Color(0.9f, 0.7f, 0.2f)  // Gold border for reinforcing
                : GetUnitTypeColor(reg.unitType);
            borderImg.type = Image.Type.Sliced;
            
            // Bring border to front (it will be the outline)
            border.transform.SetAsFirstSibling();
            
            // Content layout - horizontal
            GameObject content = new GameObject("Content");
            content.transform.SetParent(card.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 0);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.offsetMin = new Vector2(4, 4);
            contentRect.offsetMax = new Vector2(-4, -4);
            HorizontalLayoutGroup hlg = content.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            
            // Unit type icon
            string typeIcon = GetUnitTypeIcon(reg.unitType);
            Color typeColor = GetUnitTypeColor(reg.unitType);
            
            GameObject iconBg = new GameObject("IconBg");
            iconBg.transform.SetParent(content.transform, false);
            RectTransform iconBgRect = iconBg.AddComponent<RectTransform>();
            iconBgRect.sizeDelta = new Vector2(50, reg.needsReinforcement ? 93 : 73);
            Image iconBgImg = iconBg.AddComponent<Image>();
            iconBgImg.color = new Color(typeColor.r * 0.3f, typeColor.g * 0.3f, typeColor.b * 0.3f, 0.8f);
            
            Text iconText = UIFactory.CreateText(iconBg.transform, "Icon", typeIcon, 28, TextAnchor.MiddleCenter, typeColor);
            RectTransform iconTextRect = iconText.GetComponent<RectTransform>();
            iconTextRect.anchorMin = Vector2.zero;
            iconTextRect.anchorMax = Vector2.one;
            iconTextRect.offsetMin = Vector2.zero;
            iconTextRect.offsetMax = Vector2.zero;
            
            // Info section - vertical layout
            GameObject infoSection = new GameObject("InfoSection");
            infoSection.transform.SetParent(content.transform, false);
            RectTransform infoRect = infoSection.AddComponent<RectTransform>();
            infoRect.sizeDelta = new Vector2(140, reg.needsReinforcement ? 93 : 73);
            VerticalLayoutGroup vlg = infoSection.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2f;
            vlg.childAlignment = TextAnchor.MiddleLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            
            // Regiment name
            Text nameText = UIFactory.CreateText(infoSection.transform, "Name", reg.regimentName, 13, TextAnchor.MiddleLeft, UIFactory.ParchmentBeige);
            nameText.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(nameText.gameObject, 18, flexibleWidth: 1);
            
            // Rank display
            string rankLabel = reg.RankDisplayName;
            float xpProgress = RegimentRankSystem.GetRankProgress(reg.experience);
            string rankStr = reg.rank >= RegimentRankSystem.TotalRanks - 1 
                ? $"{rankLabel} (MAX)" 
                : $"{rankLabel} [{xpProgress * 100:F0}%]";
            Text rankText = UIFactory.CreateText(infoSection.transform, "Rank", rankStr, 10, TextAnchor.MiddleLeft, reg.RankColor);
            UIFactory.AddLayoutElement(rankText.gameObject, 14, flexibleWidth: 1);
            
            // Strength bar
            GameObject strengthBarBg = new GameObject("StrengthBarBg");
            strengthBarBg.transform.SetParent(infoSection.transform, false);
            RectTransform strengthBgRect = strengthBarBg.AddComponent<RectTransform>();
            strengthBgRect.sizeDelta = new Vector2(0, 14);
            Image strengthBgImg = strengthBarBg.AddComponent<Image>();
            strengthBgImg.color = new Color(0.2f, 0.2f, 0.2f, 0.6f);
            
            // Strength fill
            float strengthRatio = (float)reg.currentSize / reg.maxSize;
            Color strengthColor = strengthRatio > 0.7f ? new Color(0.3f, 0.7f, 0.3f) : 
                                 strengthRatio > 0.4f ? new Color(0.9f, 0.7f, 0.2f) : 
                                 new Color(0.8f, 0.3f, 0.2f);
            
            GameObject strengthFill = new GameObject("StrengthFill");
            strengthFill.transform.SetParent(strengthBarBg.transform, false);
            RectTransform strengthFillRect = strengthFill.AddComponent<RectTransform>();
            strengthFillRect.anchorMin = Vector2.zero;
            strengthFillRect.anchorMax = new Vector2(strengthRatio, 1f);
            strengthFillRect.offsetMin = new Vector2(2, 2);
            strengthFillRect.offsetMax = new Vector2(-2, -2);
            Image strengthFillImg = strengthFill.AddComponent<Image>();
            strengthFillImg.color = strengthColor;
            
            // Strength text overlay
            Text strengthText = UIFactory.CreateText(strengthBarBg.transform, "StrengthText", 
                $"{reg.currentSize}/{reg.maxSize}", 10, TextAnchor.MiddleCenter, Color.white);
            RectTransform strengthTextRect = strengthText.GetComponent<RectTransform>();
            strengthTextRect.anchorMin = Vector2.zero;
            strengthTextRect.anchorMax = Vector2.one;
            strengthTextRect.offsetMin = Vector2.zero;
            strengthTextRect.offsetMax = Vector2.zero;
            strengthText.fontStyle = FontStyle.Bold;
            
            // Reinforcement status or button
            if (reg.isBeingReinforced)
            {
                // Show "Reinforcing... X turns" text
                Text reinforcingText = UIFactory.CreateText(infoSection.transform, "Reinforcing", 
                    $"🔄 Reinforcing... {reg.turnsToCompleteReinforcement} turns", 10, TextAnchor.MiddleLeft, UIFactory.GoldAccent);
                reinforcingText.fontStyle = FontStyle.Italic;
                UIFactory.AddLayoutElement(reinforcingText.gameObject, 16);
            }
            else if (reg.needsReinforcement)
            {
                // Show reinforcement button
                Button reinforceBtn = UIFactory.CreateGoldButton(infoSection.transform, "BtnReinforce", 
                    $"Reinforce (+{reg.maxSize - reg.currentSize})", 9, () =>
                {
                    CampaignManager.Instance.ReinforceRegiment(armyId, regimentIndex);
                    armyPanelDirty = true;
                });
                UIFactory.AddLayoutElement(reinforceBtn.gameObject, 22, flexibleWidth: 1);
                
                // Disable if army not in friendly city with required buildings
                if (!CanReinforceRegiment(armyId, regimentIndex))
                {
                    reinforceBtn.interactable = false;
                    reinforceBtn.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
                }
            }
            
            // Stats section (experience, morale)
            GameObject statsSection = new GameObject("StatsSection");
            statsSection.transform.SetParent(content.transform, false);
            RectTransform statsRect = statsSection.AddComponent<RectTransform>();
            statsRect.sizeDelta = new Vector2(60, reg.needsReinforcement ? 78 : 58);
            VerticalLayoutGroup statsVlg = statsSection.AddComponent<VerticalLayoutGroup>();
            statsVlg.spacing = 2f;
            statsVlg.childAlignment = TextAnchor.MiddleLeft;
            statsVlg.childControlWidth = true;
            statsVlg.childControlHeight = false;
            
            // Experience stars
            int expStars = Mathf.FloorToInt(reg.experience / 20f);
            string stars = new string('⭐', Mathf.Clamp(expStars, 0, 5));
            Text expText = UIFactory.CreateText(statsSection.transform, "Exp", stars, 11, TextAnchor.MiddleLeft, UIFactory.GoldAccent);
            UIFactory.AddLayoutElement(expText.gameObject, 16);
            
            // Unit type name (small)
            Text typeText = UIFactory.CreateText(statsSection.transform, "Type", GetUnitTypeName(reg.unitType), 10, TextAnchor.MiddleLeft, UIFactory.TextGrey);
            UIFactory.AddLayoutElement(typeText.gameObject, 14);
            
            UIFactory.AddLayoutElement(card, reg.needsReinforcement ? 90 : 70, flexibleWidth: 1);
        }
        
        /// <summary>
        /// Check if a regiment can be reinforced (army in friendly city with required buildings)
        /// </summary>
        private bool CanReinforceRegiment(string armyId, int regimentIndex)
        {
            if (!CampaignManager.Instance.Armies.ContainsKey(armyId)) return false;
            ArmyData army = CampaignManager.Instance.Armies[armyId];
            
            if (regimentIndex < 0 || regimentIndex >= army.regiments.Count) return false;
            RegimentData regiment = army.regiments[regimentIndex];
            
            // Must be in friendly province
            if (!CampaignManager.Instance.Provinces.ContainsKey(army.currentProvinceId)) return false;
            ProvinceData prov = CampaignManager.Instance.Provinces[army.currentProvinceId];
            if (prov.owner != army.faction) return false;
            
            // Must have required building
            return regiment.unitType switch
            {
                UnitType.LineInfantry or UnitType.LightInfantry or UnitType.Grenadier => prov.hasBarracks,
                UnitType.Cavalry or UnitType.Hussar or UnitType.Lancer => prov.hasStables,
                UnitType.Artillery => prov.hasArmory,
                _ => false
            };
        }
        
        private string GetUnitTypeIcon(UnitType type)
        {
            return type switch
            {
                UnitType.LineInfantry => "⚔",
                UnitType.LightInfantry => "🏹",
                UnitType.Grenadier => "💣",
                UnitType.Cavalry => "🐎",
                UnitType.Hussar => "⚡",
                UnitType.Lancer => "🔱",
                UnitType.Artillery => "💥",
                UnitType.ImperialGuard => "🛡",
                UnitType.GuardCavalry => "👑",
                UnitType.GuardArtillery => "🏰",
                _ => "⚔"
            };
        }
        
        private Color GetUnitTypeColor(UnitType type)
        {
            return type switch
            {
                UnitType.LineInfantry => new Color(0.2f, 0.5f, 0.2f),      // Green
                UnitType.LightInfantry => new Color(0.3f, 0.6f, 0.3f),     // Light Green
                UnitType.Grenadier => new Color(0.5f, 0.3f, 0.2f),         // Brown
                UnitType.Cavalry => new Color(0.8f, 0.6f, 0.2f),           // Gold
                UnitType.Hussar => new Color(0.9f, 0.5f, 0.1f),            // Orange
                UnitType.Lancer => new Color(0.7f, 0.7f, 0.3f),            // Yellow
                UnitType.Artillery => new Color(0.4f, 0.4f, 0.4f),         // Gray
                UnitType.ImperialGuard => new Color(1f, 0.85f, 0.3f),      // Bright Gold
                UnitType.GuardCavalry => new Color(1f, 0.7f, 0.2f),        // Deep Gold
                UnitType.GuardArtillery => new Color(0.6f, 0.5f, 0.2f),    // Bronze
                _ => Color.gray
            };
        }
        
        private string GetUnitTypeName(UnitType type)
        {
            return type switch
            {
                UnitType.LineInfantry => "Line Inf.",
                UnitType.LightInfantry => "Light Inf.",
                UnitType.Grenadier => "Grenadier",
                UnitType.Cavalry => "Cavalry",
                UnitType.Hussar => "Hussar",
                UnitType.Lancer => "Lancer",
                UnitType.Artillery => "Artillery",
                UnitType.ImperialGuard => "Garde Imp.",
                UnitType.GuardCavalry => "Garde Cav.",
                UnitType.GuardArtillery => "Garde Art.",
                _ => "Unknown"
            };
        }

        // ===================== MAP DRAWING =====================
        private void RedrawMap()
        {
            Color[] pixels = new Color[mapRes * mapRes];
            Color seaColor = new Color(0.12f, 0.15f, 0.22f);
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = seaColor;

            var provinces = CampaignManager.Instance.Provinces;
            var armies = CampaignManager.Instance.Armies;

            // Draw connections
            foreach (var kvp in provinces)
            {
                ProvinceData prov = kvp.Value;
                Vector2Int pA = ProvinceToPixel(prov.mapPosition);

                foreach (string nid in prov.neighborIds)
                {
                    if (provinces.ContainsKey(nid))
                    {
                        Vector2Int pB = ProvinceToPixel(provinces[nid].mapPosition);
                        DrawLineOnTexture(pixels, pA, pB, new Color(0.3f, 0.3f, 0.3f, 0.6f));
                    }
                }
            }

            // Draw provinces
            foreach (var kvp in provinces)
            {
                ProvinceData prov = kvp.Value;
                Vector2Int pos = ProvinceToPixel(prov.mapPosition);
                // Neutral color for all provinces (no faction colors)
                Color col = new Color(0.6f, 0.55f, 0.45f); // Beige/stone color
                bool selected = kvp.Key == selectedProvinceId;
                int radius = selected ? 10 : 7;

                DrawCircle(pixels, pos.x, pos.y, radius, col);
                if (selected)
                    DrawCircle(pixels, pos.x, pos.y, radius + 2, UIFactory.GoldAccent, true);
            }

            // Draw armies
            foreach (var kvp in armies)
            {
                ArmyData army = kvp.Value;
                if (!provinces.ContainsKey(army.currentProvinceId)) continue;
                Vector2Int pos = ProvinceToPixel(provinces[army.currentProvinceId].mapPosition);
                // Neutral color for armies
                DrawCircle(pixels, pos.x + 12, pos.y + 6, 5, new Color(0.4f, 0.35f, 0.3f));
                DrawCircle(pixels, pos.x + 12, pos.y + 6, 6, Color.black, true);
            }

            mapTexture.SetPixels(pixels);
            mapTexture.Apply();
        }

        private Vector2Int ProvinceToPixel(Vector2 mapPos)
        {
            int px = Mathf.Clamp(Mathf.FloorToInt(mapPos.x * mapRes), 5, mapRes - 5);
            int py = Mathf.Clamp(Mathf.FloorToInt(mapPos.y * mapRes), 5, mapRes - 5);
            return new Vector2Int(px, py);
        }

        private void DrawCircle(Color[] pixels, int cx, int cy, int radius, Color color, bool outline = false)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int dist = dx * dx + dy * dy;
                    if (outline)
                    {
                        if (dist < (radius - 1) * (radius - 1) || dist > radius * radius) continue;
                    }
                    else
                    {
                        if (dist > radius * radius) continue;
                    }
                    int px = cx + dx;
                    int py = cy + dy;
                    if (px < 0 || px >= mapRes || py < 0 || py >= mapRes) continue;
                    pixels[py * mapRes + px] = color;
                }
            }
        }

        private void DrawLineOnTexture(Color[] pixels, Vector2Int a, Vector2Int b, Color color)
        {
            int steps = Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
            if (steps == 0) return;
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                int px = Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, t));
                int py = Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, t));
                if (px >= 0 && px < mapRes && py >= 0 && py < mapRes)
                    pixels[py * mapRes + px] = color;
            }
        }

        // ===================== MAP CLICK =====================
        public void OnMapClicked(Vector2 normalizedPos, int mouseButton)
        {
            if (CampaignManager.Instance == null) return;

            var provinces = CampaignManager.Instance.Provinces;
            float bestDist = float.MaxValue;
            string bestId = null;

            foreach (var kvp in provinces)
            {
                float dist = Vector2.Distance(normalizedPos, kvp.Value.mapPosition);
                if (dist < bestDist && dist < 0.05f)
                {
                    bestDist = dist;
                    bestId = kvp.Key;
                }
            }

            if (mouseButton == 0 && bestId != null)
            {
                selectedProvinceId = bestId;
                buildMenuPanel.SetActive(false);

                var armiesHere = CampaignManager.Instance.GetArmiesInProvince(bestId);
                selectedArmyId = armiesHere.Count > 0 ? armiesHere[0].armyId : null;
                
                // Mark panels dirty when selection changes
                provincePanelDirty = true;
                armyPanelDirty = true;
            }
            else if (mouseButton == 1 && bestId != null && !string.IsNullOrEmpty(selectedArmyId))
            {
                if (CampaignManager.Instance.Armies.ContainsKey(selectedArmyId))
                {
                    CampaignManager.Instance.MoveArmy(selectedArmyId, bestId);
                    selectedProvinceId = bestId;
                    provincePanelDirty = true;
                    armyPanelDirty = true;
                }
            }
        }

        // ===================== BUILD MENU =====================
        private void ShowBuildMenu()
        {
            foreach (Transform child in buildMenuContainer)
                Destroy(child.gameObject);

            BuildingType[] types = {
                BuildingType.Farm, BuildingType.Mine, BuildingType.Barracks,
                BuildingType.Stables, BuildingType.Armory, BuildingType.Market,
                BuildingType.Fortress
            };

            foreach (var type in types)
            {
                int cost = BuildingInfo.GetCostGold(type, 0);
                int time = BuildingInfo.GetBuildTime(type);
                string label = $"{BuildingInfo.GetName(type)} ({cost}g, {time}t)";

                BuildingType capturedType = type;
                Button btn = UIFactory.CreateGoldButton(buildMenuContainer, $"Btn_{type}", label, 14, () =>
                {
                    CampaignManager.Instance.BuildInProvince(selectedProvinceId, buildSlotIndex, capturedType);
                    buildMenuPanel.SetActive(false);
                });
                UIFactory.AddLayoutElement(btn.gameObject, 38);
            }

            buildMenuPanel.SetActive(true);
        }
        
        // ==================== CITY MANAGEMENT PANEL ====================
        
        private void OpenCityPanel(ProvinceData province)
        {
            openCityProvince = province;
            
            if (cityPanel == null)
                BuildCityPanel();
            
            cityPanel.SetActive(true);
            cityPanelDirty = true;
            activeCityTab = 0;
            RefreshCityPanel();
        }
        
        private void CloseCityPanel()
        {
            if (cityPanel != null)
                cityPanel.SetActive(false);
            openCityProvince = null;
        }
        
        private void BuildCityPanel()
        {
            // Main panel — right side, large, ornate with gold border
            RectTransform panel = UIFactory.CreateBorderedPanel(canvas.transform, "CityPanel", 
                UIFactory.DeepCharcoal, UIFactory.BorderGold, 1.5f);
            panel.anchorMin = new Vector2(0.58f, 0.06f);
            panel.anchorMax = new Vector2(0.99f, 0.94f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            cityPanel = panel.gameObject;
    
            // Inner layout
            var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 0f;
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            
            // === PREMIUM HEADER BANNER ===
            RectTransform headerBg = UIFactory.CreateGradientPanel(panel, "HeaderBanner",
                new Color(0.12f, 0.11f, 0.09f), UIFactory.DeepCharcoal);
            UIFactory.AddLayoutElement(headerBg.gameObject, preferredHeight: 56);
            
            UIFactory.AddHorizontalLayout(headerBg.gameObject, 10f, new RectOffset(16, 12, 8, 8));
            
            // Gold emblem
            Text emblem = UIFactory.CreateText(headerBg, "Emblem", "🏛", 28, TextAnchor.MiddleCenter, UIFactory.EmpireGold);
            UIFactory.AddLayoutElement(emblem.gameObject, preferredWidth: 40);
            
            // City name + level column
            GameObject nameCol = new GameObject("NameCol");
            nameCol.transform.SetParent(headerBg, false);
            var nameVlg = nameCol.AddComponent<VerticalLayoutGroup>();
            nameVlg.spacing = 0f; nameVlg.childForceExpandWidth = true; nameVlg.childForceExpandHeight = false;
            UIFactory.AddLayoutElement(nameCol, flexibleWidth: 1);
            
            cityNameText = UIFactory.CreateText(nameCol.transform, "CityName", "CITY", 20,
                TextAnchor.MiddleLeft, UIFactory.EmpireGold);
            cityNameText.fontStyle = FontStyle.Bold;
            
            cityLevelText = UIFactory.CreateText(nameCol.transform, "CityLevel", "", 11,
                TextAnchor.MiddleLeft, UIFactory.Parchment);
            
            // Close button — elegant X
            Button closeBtn = UIFactory.CreateButton(headerBg, "CloseBtn", "✕", 16,
                () => CloseCityPanel());
            UIFactory.AddLayoutElement(closeBtn.gameObject, preferredWidth: 32, preferredHeight: 32);
            closeBtn.GetComponent<Image>().color = new Color(0.25f, 0.06f, 0.06f, 0.85f);
            
            // Gold separator line under header
            RectTransform hSep = UIFactory.CreateGlowSeparator(panel, "HeaderSep", false);
            UIFactory.AddLayoutElement(hSep.gameObject, preferredHeight: 3);
            
            // === TAB BAR — Gold-bordered tabs ===
            GameObject tabBar = new GameObject("TabBar");
            tabBar.transform.SetParent(panel, false);
            tabBar.AddComponent<RectTransform>();
            Image tabBarBg = tabBar.AddComponent<Image>();
            tabBarBg.color = new Color(0.04f, 0.04f, 0.05f, 1f);
            UIFactory.AddHorizontalLayout(tabBar, 1f, new RectOffset(4, 4, 3, 3));
            UIFactory.AddLayoutElement(tabBar, preferredHeight: 36);
            
            string[] tabNames = { "⚜ VUE", "🏗 BÂTIMENTS", "⚔ MILITAIRE", "🏭 INDUSTRIE" };
            cityTabButtons = new Button[4];
            for (int t = 0; t < 4; t++)
            {
                int tabIdx = t;
                cityTabButtons[t] = UIFactory.CreateButton(tabBar.transform, $"Tab_{t}",
                    tabNames[t], 10, () => { activeCityTab = tabIdx; RefreshCityPanel(); });
                UIFactory.AddLayoutElement(cityTabButtons[t].gameObject, flexibleWidth: 1, preferredHeight: 28);
            }
            
            // Thin separator under tabs
            RectTransform tSep = UIFactory.CreateSeparator(panel);
            UIFactory.AddLayoutElement(tSep.gameObject, preferredHeight: 2);
            
            // === TAB CONTENT CONTAINERS — with padding ===
            GameObject contentArea = new GameObject("ContentArea");
            contentArea.transform.SetParent(panel, false);
            contentArea.AddComponent<RectTransform>();
            var cavlg = contentArea.AddComponent<VerticalLayoutGroup>();
            cavlg.spacing = 0f;
            cavlg.padding = new RectOffset(12, 12, 8, 8);
            cavlg.childControlHeight = true;
            cavlg.childForceExpandHeight = true;
            cavlg.childControlWidth = true;
            cavlg.childForceExpandWidth = true;
            UIFactory.AddLayoutElement(contentArea, flexibleHeight: 1, flexibleWidth: 1);
            
            (cityOverviewTab, cityOverviewContent) = CreateTabContainerWithContent(contentArea.transform, "OverviewTab");
            (cityBuildingsTab, cityBuildingsContent) = CreateTabContainerWithContent(contentArea.transform, "BuildingsTab");
            (cityMilitaryTab, cityMilitaryContent) = CreateTabContainerWithContent(contentArea.transform, "MilitaryTab");
            (cityIndustryTab, cityIndustryContent) = CreateTabContainerWithContent(contentArea.transform, "IndustryTab");
            
            cityPanel.SetActive(false);
        }
        
        private (GameObject go, Transform content) CreateTabContainerWithContent(Transform parent, string name)
        {
            var (scroll, content) = UIFactory.CreateScrollView(parent, name);
            UIFactory.AddLayoutElement(scroll.gameObject, 100, flexibleWidth: 1, flexibleHeight: 1);
            return (scroll.gameObject, content);
        }
        
        private Transform GetTabContent(GameObject tab)
        {
            // Use stored references first
            if (tab == cityOverviewTab && cityOverviewContent != null) return cityOverviewContent;
            if (tab == cityBuildingsTab && cityBuildingsContent != null) return cityBuildingsContent;
            if (tab == cityMilitaryTab && cityMilitaryContent != null) return cityMilitaryContent;
            if (tab == cityIndustryTab && cityIndustryContent != null) return cityIndustryContent;
            
            // Fallback: navigate hierarchy
            if (tab == null || tab.transform.childCount == 0) return null;
            Transform viewport = tab.transform.GetChild(0);
            if (viewport == null || viewport.childCount == 0) return null;
            return viewport.GetChild(0);
        }
        
        private void ClearContent(Transform content)
        {
            if (content == null) return;
            for (int i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);
        }
        
        private void RefreshCityPanel()
        {
            if (openCityProvince == null) return;
            if (CampaignManager.Instance == null) return;
            
            var prov = openCityProvince;
            CityData city = CampaignManager.Instance.GetCityForProvince(prov.provinceId);
            
            // Update header
            cityNameText.text = city != null ? city.cityName.ToUpper() : prov.provinceName.ToUpper();
            cityLevelText.text = city != null 
                ? $"LV.{city.cityLevel} — {city.CityLevelName.ToUpper()}" 
                : $"{prov.owner.ToString().ToUpper()} TERRITORY";
            
            // Highlight active tab — premium gold style
            for (int t = 0; t < 4; t++)
            {
                bool isActive = (t == activeCityTab);
                Image tabImg = cityTabButtons[t].GetComponent<Image>();
                tabImg.color = isActive 
                    ? new Color(0.14f, 0.12f, 0.07f, 1f)  // warm dark amber
                    : new Color(0.05f, 0.05f, 0.06f, 1f); // near-black
                
                Text txt = cityTabButtons[t].GetComponentInChildren<Text>();
                if (txt != null)
                {
                    txt.color = isActive ? UIFactory.EmpireGold : new Color(0.45f, 0.42f, 0.38f);
                    txt.fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal;
                }
            }
            
            cityOverviewTab.SetActive(activeCityTab == 0);
            cityBuildingsTab.SetActive(activeCityTab == 1);
            cityMilitaryTab.SetActive(activeCityTab == 2);
            cityIndustryTab.SetActive(activeCityTab == 3);
            
            try
            {
                switch (activeCityTab)
                {
                    case 0: RefreshOverviewTab(city, prov); break;
                    case 1: RefreshBuildingsTab(city, prov); break;
                    case 2: RefreshMilitaryTab(city, prov); break;
                    case 3: RefreshIndustryTab(city, prov); break;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CityPanel] Tab {activeCityTab} error: {e.Message}\n{e.StackTrace}");
                // Show error in the panel so it's visible
                GameObject activeTab = activeCityTab switch
                {
                    0 => cityOverviewTab, 1 => cityBuildingsTab,
                    2 => cityMilitaryTab, 3 => cityIndustryTab, _ => null
                };
                if (activeTab != null)
                {
                    Transform errContent = GetTabContent(activeTab);
                    if (errContent != null)
                    {
                        AddInfoRow(errContent, $"ERROR: {e.Message}", Color.red, 14, true);
                        AddInfoRow(errContent, e.StackTrace?.Split('\n')[0] ?? "", Color.gray, 10);
                    }
                }
            }
        }
        
        // ─── OVERVIEW TAB ─────────────────────────────────────────
        private void RefreshOverviewTab(CityData city, ProvinceData prov)
        {
            Transform content = GetTabContent(cityOverviewTab);
            if (content == null) return;
            ClearContent(content);
            
            // === HEADER: City Level & Growth ===
            if (city != null)
            {
                string levelName = CityLevelThresholds.GetLevelName(city.cityLevel);
                int nextThresh = CityLevelThresholds.GetNextThreshold(city.cityLevel);
                string growthStr = nextThresh > 0 ? $" → {nextThresh:N0} pop pour Nv.{city.cityLevel + 1}" : " (Max)";
                AddSectionLabel(content, $"🏛 {levelName.ToUpper()} — NIVEAU {city.cityLevel}{growthStr}");
                
                // Population progress bar
                if (nextThresh > 0)
                {
                    int prevThresh = city.cityLevel > 1 ? CityLevelThresholds.GetNextThreshold(city.cityLevel - 1) : 0;
                    float progress = (float)(city.population - prevThresh) / Mathf.Max(1, nextThresh - prevThresh);
                    var (bg, fill) = UIFactory.CreateProgressBar(content, "CityProg", UIFactory.EmpireGold);
                    UIFactory.AddLayoutElement(bg.gameObject, preferredHeight: 10);
                    fill.GetComponent<RectTransform>().anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
                }
            }
            
            AddSpacer(content, 4);

            // === IMPERIAL SOCIETY ===
            UIFactory.CreateSectionHeader(content, "SEC_SOC", "⚜ Société Impériale", 13);
            if (city != null)
            {
                AddInfoRow(content, "Population totale", $"{city.population:N0} habitants", UIFactory.Porcelain);
                
                // Population classes with visual bars
                float nobleR = city.populationData.NobleRatio;
                float bourgeoisR = city.populationData.BourgeoisRatio;
                float workerR = city.populationData.WorkerRatio;
                
                AddInfoRow(content, "  👑 Noblesse", $"{(nobleR * 100):F0}% ({city.populationData.nobles:N0})", new Color(0.7f, 0.5f, 0.9f));
                AddInfoRow(content, "  💼 Bourgeoisie", $"{(bourgeoisR * 100):F0}% ({city.populationData.bourgeois:N0})", UIFactory.EmpireGold);
                AddInfoRow(content, "  ⚒ Ouvriers", $"{(workerR * 100):F0}% ({city.populationData.workers:N0})", UIFactory.SilverText);
                AddInfoRow(content, "Bâtiments", $"{city.buildings.Count}/{city.maxBuildings} ({CityLevelThresholds.GetMaxBuildings(city.cityLevel)} max au Nv.{city.cityLevel})", UIFactory.Parchment);
            }
            else
            {
                AddInfoRow(content, "Population régionale", $"{prov.population:N0}", UIFactory.Parchment);
            }
            
            UIFactory.CreateSeparator(content);
            
            // === REGIONAL ECONOMY ===
            UIFactory.CreateSectionHeader(content, "SEC_ECON", "💰 Économie Régionale", 13);
            AddInfoRow(content, "Or (trésorerie)", $"+{prov.goldIncome:F0}/tour", UIFactory.EmpireGold);
            AddInfoRow(content, "Nourriture", $"+{prov.foodProduction:F0}/tour", new Color(0.5f, 0.8f, 0.3f));
            AddInfoRow(content, "Fer (extraction)", $"+{prov.ironProduction:F0}/tour", UIFactory.SilverText);
            
            if (city != null)
            {
                AddSpacer(content, 2);
                AddInfoRow(content, "  📦 Stock nourriture", $"{city.storedFood:F0} t", UIFactory.Parchment);
                AddInfoRow(content, "  📦 Stock fer", $"{city.storedIron:F0}", UIFactory.SilverText);
                AddInfoRow(content, "  📦 Marchandises", $"{city.storedGoods:F0} unités", UIFactory.EmpireGold);
                
                if (city.industries.Count > 0)
                {
                    AddSpacer(content, 2);
                    float totalGold = 0, totalFood = 0, totalIron = 0;
                    foreach (var ind in city.industries)
                    {
                        totalGold += ind.GetGoldOutput();
                        totalFood += ind.GetFoodOutput();
                        totalIron += ind.GetIronOutput();
                    }
                    if (totalGold > 0) AddInfoRow(content, "  Industrie → Or", $"+{totalGold:F0}/tour", UIFactory.EmpireGold);
                    if (totalFood > 0) AddInfoRow(content, "  Industrie → Nourriture", $"+{totalFood:F0}/tour", new Color(0.5f, 0.8f, 0.3f));
                    if (totalIron > 0) AddInfoRow(content, "  Industrie → Fer", $"+{totalIron:F0}/tour", UIFactory.SilverText);
                }
            }

            UIFactory.CreateSeparator(content);

            // === STRATEGIC DEFENSE ===
            UIFactory.CreateSectionHeader(content, "SEC_DEF", "🏰 Défense Stratégique", 13);
            if (city != null)
            {
                float order = city.publicOrder;
                Color orderCol = order > 70 ? Color.green : order > 40 ? UIFactory.EmpireGold : Color.red;
                AddInfoRow(content, "Ordre public", $"{order:F0}/100", orderCol);
                var (obg, ofill) = UIFactory.CreateProgressBar(content, "OrderBar", orderCol);
                UIFactory.AddLayoutElement(obg.gameObject, preferredHeight: 8);
                ofill.GetComponent<RectTransform>().anchorMax = new Vector2(order / 100f, 1f);
                
                AddInfoRow(content, "Fortification", $"Grade {city.GetFortificationLevel()}", UIFactory.Porcelain);
                AddInfoRow(content, "Garnison", $"{city.garrisonSize}/{city.maxGarrison} soldats", UIFactory.SilverText);
            }
            else
            {
                AddInfoRow(content, "Terrain défensif", prov.terrainType.ToString().ToUpper(), UIFactory.Parchment);
                AddInfoRow(content, "Garnison provinciale", $"{prov.garrisonSize}", UIFactory.SilverText);
            }
            
            UIFactory.CreateSeparator(content);
            
            // === CULTURE & RELIGION ===
            UIFactory.CreateSectionHeader(content, "SEC_CUL", "🏛 Culture & Religion", 13);
            AddInfoRow(content, "Culture", prov.primaryCulture.ToString(), prov.IsCultureAccepted ? Color.green : new Color(1f, 0.5f, 0.2f));
            AddInfoRow(content, "Religion", prov.primaryReligion.ToString(), prov.IsReligionAccepted ? Color.green : new Color(1f, 0.5f, 0.2f));
            if (!prov.IsCultureAccepted)
            {
                AddInfoRow(content, "  Assimilation", $"{(prov.cultureAssimilation * 100):F0}%", UIFactory.SilverText);
                var (abg, afill) = UIFactory.CreateProgressBar(content, "AssimBar", UIFactory.EmpireGold);
                UIFactory.AddLayoutElement(abg.gameObject, preferredHeight: 6);
                afill.GetComponent<RectTransform>().anchorMax = new Vector2(prov.cultureAssimilation, 1f);
            }
            AddInfoRow(content, "Loyauté", $"{prov.loyalty:F0}/100", prov.loyalty > 60 ? Color.green : prov.loyalty > 30 ? UIFactory.EmpireGold : Color.red);
            
            UIFactory.CreateSeparator(content);
            
            // === PROVINCE STATUS ===
            UIFactory.CreateSectionHeader(content, "SEC_PROV", "🗺 État de la Province", 13);
            AddInfoRow(content, "Terrain", prov.terrainType.ToString(), UIFactory.Porcelain);
            
            Color devColor = prov.devastation < 10 ? Color.green : prov.devastation < 40 ? UIFactory.EmpireGold : Color.red;
            AddInfoRow(content, "Dévastation", $"{prov.devastation:F0}%", devColor);
            
            Color prosColor = prov.prosperity > 60 ? Color.green : prov.prosperity > 30 ? UIFactory.EmpireGold : Color.red;
            AddInfoRow(content, "Prospérité", $"{prov.prosperity:F0}%", prosColor);
            
            Color revColor = prov.revoltRisk < 20 ? Color.green : prov.revoltRisk < 50 ? UIFactory.EmpireGold : Color.red;
            AddInfoRow(content, "Risque de révolte", $"{prov.revoltRisk}%", revColor);
            
            if (prov.isOccupied) AddInfoRow(content, "⚠ PROVINCE OCCUPÉE", $"Depuis {prov.turnsOccupied} tours", Color.red);
            if (!prov.isCored) AddInfoRow(content, "⚠ NON ANNEXÉE", "Intégration en cours", new Color(1f, 0.5f, 0.2f));
            if (prov.hasActiveRevolt) AddInfoRow(content, "🔥 RÉVOLTE ACTIVE", $"{prov.revoltStrength} rebelles", Color.red);
        }
        
        // ─── BUILDINGS TAB ────────────────────────────────────────
        private void RefreshBuildingsTab(CityData city, ProvinceData prov)
        {
            Transform content = GetTabContent(cityBuildingsTab);
            if (content == null) return;
            ClearContent(content);
            
            // Header info
            if (city != null)
                AddInfoRow(content, $"🏗 {city.buildings.Count}/{city.maxBuildings} bâtiments — {CityLevelThresholds.GetMaxBuildings(city.cityLevel)} max au Nv.{city.cityLevel}", UIFactory.Parchment);
            
            AddSpacer(content, 4);
            
            // Use ProvinceData building slots — grouped by category
            if (prov.buildings != null && prov.buildings.Length > 0)
            {
                int built = 0, building = 0, empty = 0;
                foreach (var b in prov.buildings)
                    if (b.type == BuildingType.Empty) empty++; 
                    else if (b.isConstructing) building++; 
                    else built++;
                
                AddInfoRow(content, $"Créneaux: {built} construits, {building} en cours, {empty} vides ({prov.buildings.Length} total)", UIFactory.ParchmentBeige);
                AddSpacer(content, 4);
                
                // Group by category
                var categories = new Dictionary<BuildingCategory, List<(int idx, BuildingSlot slot)>>();
                var emptySlots = new List<int>();
                
                for (int i = 0; i < prov.buildings.Length; i++)
                {
                    BuildingSlot slot = prov.buildings[i];
                    if (slot.type == BuildingType.Empty)
                    {
                        emptySlots.Add(i);
                        continue;
                    }
                    BuildingCategory cat = BuildingInfo.GetCategory(slot.type);
                    if (!categories.ContainsKey(cat))
                        categories[cat] = new List<(int, BuildingSlot)>();
                    categories[cat].Add((i, slot));
                }
                
                // Render each category
                BuildingCategory[] categoryOrder = { BuildingCategory.Military, BuildingCategory.Economy, 
                    BuildingCategory.Religion, BuildingCategory.Academic, BuildingCategory.Infrastructure };
                
                foreach (var cat in categoryOrder)
                {
                    if (!categories.ContainsKey(cat)) continue;
                    
                    string catIcon = cat switch
                    {
                        BuildingCategory.Military => "⚔",
                        BuildingCategory.Economy => "💰",
                        BuildingCategory.Religion => "⛪",
                        BuildingCategory.Academic => "🏛",
                        _ => "🔧"
                    };
                    string catName = cat switch
                    {
                        BuildingCategory.Military => "Édifices Militaires",
                        BuildingCategory.Economy => "Économie Royale",
                        BuildingCategory.Religion => "Lieux Sacrés",
                        BuildingCategory.Academic => "Académie Impériale",
                        _ => "Infrastructure Civile"
                    };
                    UIFactory.CreateSectionHeader(content, "CAT_" + cat, $"{catIcon} {catName}", 13);
                    
                    foreach (var (idx, slot) in categories[cat])
                    {
                        // Card-style building row
                        GameObject row = new GameObject("BldRow");
                        row.transform.SetParent(content, false);
                        UIFactory.AddHorizontalLayout(row, 8f, new RectOffset(10, 8, 4, 4));
                        UIFactory.AddLayoutElement(row, preferredHeight: 38);
                        Image rowBg = row.AddComponent<Image>();

                        string name = BuildingInfo.GetName(slot.type);
                        string icon = BuildingInfo.GetIcon(slot.type);
                        
                        if (slot.isConstructing)
                        {
                            rowBg.color = new Color(0.08f, 0.07f, 0.05f, 0.95f); // warm dark construction bg
                            
                            GameObject leftC = new GameObject("Left");
                            leftC.transform.SetParent(row.transform, false);
                            var lv = leftC.AddComponent<VerticalLayoutGroup>();
                            lv.spacing = 1f; lv.childForceExpandWidth = true; lv.childForceExpandHeight = false;
                            UIFactory.AddLayoutElement(leftC, flexibleWidth: 1);
                            
                            Text lbl = UIFactory.CreateText(leftC.transform, "Lbl", $"{icon} {name.ToUpper()}", 11, TextAnchor.MiddleLeft, UIFactory.EmpireGold);
                            lbl.fontStyle = FontStyle.Bold;
                            UIFactory.CreateText(leftC.transform, "Status", $"  🔨 En construction — {slot.turnsToComplete} tours restants", 9, TextAnchor.MiddleLeft, UIFactory.MutedGold);
                            
                            int totalTime = BuildingInfo.GetBuildTime(slot.type);
                            int elapsed = Mathf.Max(0, totalTime - slot.turnsToComplete);
                            float progress = totalTime > 0 ? (float)elapsed / totalTime : 0f;
                            AddBuildingProgressBar(content, progress);
                        }
                        else
                        {
                            rowBg.color = new Color(0.06f, 0.065f, 0.07f, 0.95f);
                            
                            // Icon
                            Text iconTxt = UIFactory.CreateText(row.transform, "Icon", icon, 18, TextAnchor.MiddleCenter, UIFactory.EmpireGold);
                            UIFactory.AddLayoutElement(iconTxt.gameObject, preferredWidth: 28);
                            
                            // Name + effects
                            GameObject midC = new GameObject("Mid");
                            midC.transform.SetParent(row.transform, false);
                            var mv = midC.AddComponent<VerticalLayoutGroup>();
                            mv.spacing = 1f; mv.childForceExpandWidth = true; mv.childForceExpandHeight = false;
                            UIFactory.AddLayoutElement(midC, flexibleWidth: 1);
                            
                            Text lbl = UIFactory.CreateText(midC.transform, "Lbl", $"{name.ToUpper()} NV.{slot.level}", 11, TextAnchor.MiddleLeft, UIFactory.Porcelain);
                            lbl.fontStyle = FontStyle.Bold;
                            
                            string effects = BuildingInfo.GetEffects(slot.type);
                            if (!string.IsNullOrEmpty(effects) && effects != "No effects")
                                UIFactory.CreateText(midC.transform, "Eff", $"  ↳ {effects}", 8, TextAnchor.MiddleLeft, new Color(0.5f, 0.5f, 0.45f));
                            
                            // Status badge
                            Text badge = UIFactory.CreateText(row.transform, "Badge", "✓ ÉTABLI", 9, TextAnchor.MiddleCenter, new Color(0.4f, 0.8f, 0.4f));
                            UIFactory.AddLayoutElement(badge.gameObject, preferredWidth: 60);
                        }
                    }
                    AddSpacer(content, 4);
                }
                
                // Empty slots — premium style
                if (emptySlots.Count > 0)
                {
                    UIFactory.CreateSectionHeader(content, "SEC_EMPTY", "📦 Créneaux Disponibles", 13);
                    foreach (int idx in emptySlots)
                    {
                        GameObject row = new GameObject("EmptySlot");
                        row.transform.SetParent(content, false);
                        UIFactory.AddHorizontalLayout(row, 8f, new RectOffset(10, 8, 4, 4));
                        UIFactory.AddLayoutElement(row, preferredHeight: 34);
                        Image rowBg = row.AddComponent<Image>();
                        rowBg.color = new Color(0.07f, 0.07f, 0.08f, 0.8f);
                        
                        Text lbl = UIFactory.CreateText(row.transform, "Lbl", $"  ☐ CRÉNEAU {idx+1} — Vide", 11, TextAnchor.MiddleLeft, UIFactory.SilverText);
                        UIFactory.AddLayoutElement(lbl.gameObject, flexibleWidth: 1);
                        
                        Button btn = UIFactory.CreateButton(row.transform, "Build", "CONSTRUIRE", 9, () =>
                        {
                            if (city != null) ShowCityBuildPicker(city);
                        });
                        UIFactory.AddLayoutElement(btn.gameObject, preferredWidth: 90, preferredHeight: 24);
                        btn.GetComponent<Image>().color = new Color(0.15f, 0.12f, 0.06f);
                    }
                }
            }
            else if (city != null)
            {
                AddInfoRow(content, $"Bâtiments: {city.buildings.Count}/{city.maxBuildings}", UIFactory.ParchmentBeige);
                AddSpacer(content, 4);
                
                // Group CityData buildings by category
                var categories = new Dictionary<BuildingCategory, List<CityBuilding>>();
                foreach (var bld in city.buildings)
                {
                    BuildingCategory cat = BuildingInfo.GetCategory(bld.buildingType);
                    if (!categories.ContainsKey(cat))
                        categories[cat] = new List<CityBuilding>();
                    categories[cat].Add(bld);
                }
                
                BuildingCategory[] categoryOrder = { BuildingCategory.Military, BuildingCategory.Economy, 
                    BuildingCategory.Religion, BuildingCategory.Academic, BuildingCategory.Infrastructure };
                
                foreach (var cat in categoryOrder)
                {
                    if (!categories.ContainsKey(cat)) continue;
                    
                    string catName = cat switch
                    {
                        BuildingCategory.Military => "⚔ MILITAIRE",
                        BuildingCategory.Economy => "💰 ÉCONOMIE",
                        BuildingCategory.Religion => "⛪ RELIGION",
                        BuildingCategory.Academic => "🏛 ACADÉMIE",
                        _ => "🔧 INFRASTRUCTURE"
                    };
                    AddSectionLabel(content, catName);
                    
                    foreach (var bld in categories[cat])
                    {
                        string bIcon = BuildingInfo.GetIcon(bld.buildingType);
                        string bName = BuildingInfo.GetName(bld.buildingType);
                        
                        if (bld.isConstructed)
                        {
                            AddInfoRow(content, $"  {bIcon} {bName} Nv.{bld.level} — Établi", new Color(0.4f, 0.8f, 0.4f));
                            string effects = BuildingInfo.GetEffects(bld.buildingType);
                            if (!string.IsNullOrEmpty(effects) && effects != "No effects")
                                AddInfoRow(content, $"    ↳ {effects}", new Color(0.55f, 0.55f, 0.5f), 10);
                        }
                        else if (bld.isConstructing)
                        {
                            AddInfoRow(content, $"  {bIcon} {bName} Nv.{bld.level} — 🔨 Construction ({bld.constructionTurnsRemaining}t)", UIFactory.EmpireGold);
                            int totalTime = BuildingInfo.GetBuildTime(bld.buildingType);
                            int elapsed = Mathf.Max(0, totalTime - bld.constructionTurnsRemaining);
                            float progress = totalTime > 0 ? (float)elapsed / totalTime : 0f;
                            AddBuildingProgressBar(content, progress);
                        }
                        else
                        {
                            AddInfoRow(content, $"  {bIcon} {bName} Nv.{bld.level} — Planifié", new Color(0.5f, 0.5f, 0.45f));
                        }
                    }
                    AddSpacer(content, 4);
                }
                
                int emptyCount = city.maxBuildings - city.buildings.Count;
                if (emptyCount > 0)
                {
                    AddSectionLabel(content, "📦 CRÉNEAUX VIDES");
                    for (int s = 0; s < emptyCount; s++)
                    {
                        GameObject row = new GameObject("EmptySlot");
                        row.transform.SetParent(content, false);
                        row.AddComponent<RectTransform>();
                        UIFactory.AddHorizontalLayout(row, 4f, new RectOffset(10, 8, 2, 2));
                        UIFactory.AddLayoutElement(row, 30);
                        Image rowBg = row.AddComponent<Image>();
                        rowBg.color = new Color(0.07f, 0.07f, 0.08f, 0.8f);
                        
                        UIFactory.CreateText(row.transform, "Lbl", $"  ☐ Créneau vide", 11, TextAnchor.MiddleLeft, UIFactory.SilverText);
                        
                        Button btn = UIFactory.CreateButton(row.transform, "Build", "CONSTRUIRE", 9, () =>
                        {
                            ShowCityBuildPicker(city);
                        });
                        UIFactory.AddLayoutElement(btn.gameObject, preferredWidth: 90, preferredHeight: 24);
                        btn.GetComponent<Image>().color = new Color(0.15f, 0.12f, 0.06f);
                    }
                }
            }
            else
            {
                AddInfoRow(content, "Aucune donnée de bâtiment disponible", Color.gray);
            }
            
            // Production queue
            if (city != null && city.productionQueue.Count > 0)
            {
                AddSpacer(content, 6);
                UIFactory.CreateSectionHeader(content, "SEC_QUEUE", "📋 File de Production", 13);
                foreach (var item in city.productionQueue)
                {
                    string n = item.itemType == ProductionItemType.Building ? "Bâtiment" :
                              item.itemType == ProductionItemType.Unit ? item.unitType.ToString() : "Marchandises";
                    AddInfoRow(content, $"  {n} — {item.turnsRemaining} tours", UIFactory.EmpireGold);
                }
            }
        }
        
        /// <summary>
        /// Add a thin construction progress bar beneath a building row.
        /// </summary>
        private void AddBuildingProgressBar(Transform parent, float progress)
        {
            var (bg, fill) = UIFactory.CreateProgressBar(parent, "ConstProgress", UIFactory.EmpireGold);
            UIFactory.AddLayoutElement(bg.gameObject, preferredHeight: 12);
            RectTransform bgRT = bg.GetComponent<RectTransform>();
            bgRT.sizeDelta = new Vector2(0, 8); 
            
            fill.GetComponent<RectTransform>().anchorMax = new Vector2(progress, 1f);
        }
        
        private void ShowCityBuildPicker(CityData city)
        {
            foreach (Transform child in buildMenuContainer) Destroy(child.gameObject);
            
            FactionData playerFac = CampaignManager.Instance?.GetPlayerFaction();
            
            // Gather ALL building types except Empty
            var allTypes = new List<BuildingType>();
            foreach (BuildingType bt in System.Enum.GetValues(typeof(BuildingType)))
            {
                if (bt == BuildingType.Empty) continue;
                allTypes.Add(bt);
            }
            
            int count = 0;
            foreach (var type in allTypes)
            {
                // Skip if city already has this building constructed or constructing
                if (city.buildings.Exists(b => b.buildingType == type && (b.isConstructed || b.isConstructing))) continue;
                
                // Also check province building slots
                if (openCityProvince != null && openCityProvince.buildings != null)
                {
                    bool inProvince = false;
                    foreach (var slot in openCityProvince.buildings)
                    {
                        if (slot.type == type && (slot.level > 0 || slot.isConstructing))
                        { inProvince = true; break; }
                    }
                    if (inProvince) continue;
                }
                
                int cost = BuildingInfo.GetCostGold(type, 0);
                int time = BuildingInfo.GetBuildTime(type);
                string icon = BuildingInfo.GetIcon(type);
                string desc = BuildingInfo.GetEffects(type);
                string bName = BuildingInfo.GetName(type);
                
                bool canAfford = playerFac != null && playerFac.gold >= cost;
                
                // === Card-style building entry ===
                GameObject card = new GameObject($"B_{type}");
                card.transform.SetParent(buildMenuContainer, false);
                Image cardBg = card.AddComponent<Image>();
                cardBg.color = canAfford ? new Color(0.09f, 0.09f, 0.10f, 1f) : new Color(0.06f, 0.06f, 0.07f, 0.7f);
                UIFactory.AddHorizontalLayout(card, 8f, new RectOffset(10, 8, 6, 6));
                UIFactory.AddLayoutElement(card, preferredHeight: 52);
                
                // Left: Icon
                Text iconTxt = UIFactory.CreateText(card.transform, "Icon", icon, 22, TextAnchor.MiddleCenter, 
                    canAfford ? UIFactory.EmpireGold : UIFactory.SilverText);
                UIFactory.AddLayoutElement(iconTxt.gameObject, preferredWidth: 32);
                
                // Middle: Name + desc + costs
                GameObject midCol = new GameObject("Mid");
                midCol.transform.SetParent(card.transform, false);
                var mcvlg = midCol.AddComponent<VerticalLayoutGroup>();
                mcvlg.spacing = 1f; mcvlg.childForceExpandWidth = true; mcvlg.childForceExpandHeight = false;
                UIFactory.AddLayoutElement(midCol, flexibleWidth: 1);
                
                Text nameLbl = UIFactory.CreateText(midCol.transform, "Name", bName.ToUpper(), 12, TextAnchor.MiddleLeft, 
                    canAfford ? UIFactory.Porcelain : UIFactory.SilverText);
                nameLbl.fontStyle = FontStyle.Bold;
                
                UIFactory.CreateText(midCol.transform, "Cost", $"💰 {cost}g  ⏱ {time} tours", 9, TextAnchor.MiddleLeft, UIFactory.MutedGold);
                
                if (!string.IsNullOrEmpty(desc))
                    UIFactory.CreateText(midCol.transform, "Desc", desc, 8, TextAnchor.MiddleLeft, new Color(0.5f, 0.5f, 0.45f));
                
                // Right: Build button
                BuildingType ct = type; int cc = cost;
                Button btn = UIFactory.CreateButton(card.transform, "Build", canAfford ? "CONSTRUIRE" : "FONDS ✗", 9, () =>
                { 
                    if (canAfford) 
                    { city.StartBuildingConstruction(ct, cc); buildMenuPanel.SetActive(false); cityPanelDirty = true; RefreshCityPanel(); }
                });
                UIFactory.AddLayoutElement(btn.gameObject, preferredWidth: 80, preferredHeight: 28);
                btn.interactable = canAfford;
                btn.GetComponent<Image>().color = canAfford 
                    ? new Color(0.15f, 0.12f, 0.06f)  // warm dark gold
                    : new Color(0.1f, 0.08f, 0.08f);
                
                count++;
            }
            
            if (count == 0)
            {
                AddInfoRow(buildMenuContainer, "Tous les bâtiments sont déjà construits", Color.gray);
            }
            
            buildMenuPanel.SetActive(true);
        }
        
        // ─── MILITARY TAB ─────────────────────────────────────────
        private void RefreshMilitaryTab(CityData city, ProvinceData prov)
        {
            Transform content = GetTabContent(cityMilitaryTab);
            if (content == null) return;
            ClearContent(content);
            
            bool hasBarracks = city != null && city.HasBuilding(BuildingType.Barracks);
            bool hasStables = city != null && city.HasBuilding(BuildingType.Stables);
            bool hasArmory = city != null && city.HasBuilding(BuildingType.Armory);
            
            FactionData playerFac = CampaignManager.Instance?.GetPlayerFaction();
            
            // === MILITIA — Toujours disponible ===
            UIFactory.CreateSectionHeader(content, "SEC_MIL_MIL", "⚔ Milice Locale", 13);
            AddInfoRow(content, "Recrutement basique — pas besoin de caserne", UIFactory.SilverText, 10);
            AddSpacer(content, 2);
            CreateMilitaryUnitRow(content, city, playerFac, "Milice", "🗡", UnitType.LineInfantry, 30, 5, 1, true, "Milice de base — peu efficace mais toujours disponible");
            
            UIFactory.CreateSeparator(content);
            
            // === INFANTRY — Requires Barracks ===
            UIFactory.CreateSectionHeader(content, "SEC_MIL_INF", "🎖 Infanterie Régulière", 13);
            if (!hasBarracks)
            {
                AddInfoRow(content, "⚠ CASERNE REQUISE pour recruter l'infanterie régulière", Color.red, 10);
                AddSpacer(content, 2);
            }
            CreateMilitaryUnitRow(content, city, playerFac, "Infanterie de ligne", "🔫", UnitType.LineInfantry, 100, 15, 2, hasBarracks, "Mousquets: 80, Uniformes: 60");
            CreateMilitaryUnitRow(content, city, playerFac, "Infanterie légère", "🏹", UnitType.LightInfantry, 80, 10, 2, hasBarracks, "Mousquets: 60, Uniformes: 40");
            CreateMilitaryUnitRow(content, city, playerFac, "Grenadier", "💣", UnitType.Grenadier, 150, 25, 3, hasBarracks, "Mousquets: 100, Bayonnettes: 80, Uniformes: 60");
            
            UIFactory.CreateSeparator(content);
            
            // === CAVALRY — Requires Stables ===
            UIFactory.CreateSectionHeader(content, "SEC_MIL_CAV", "🏇 Cavalerie", 13);
            if (!hasStables)
            {
                AddInfoRow(content, "⚠ ÉCURIES REQUISES pour recruter la cavalerie", Color.red, 10);
                AddSpacer(content, 2);
            }
            CreateMilitaryUnitRow(content, city, playerFac, "Cavalerie", "🐴", UnitType.Cavalry, 200, 20, 3, hasStables, "Sabres: 60, Chevaux: 80, Uniformes: 60");
            CreateMilitaryUnitRow(content, city, playerFac, "Hussard", "⚡", UnitType.Hussar, 180, 15, 3, hasStables, "Sabres: 40, Chevaux: 60, Uniformes: 50");
            CreateMilitaryUnitRow(content, city, playerFac, "Lancier", "🔱", UnitType.Lancer, 190, 18, 3, hasStables, "Sabres: 50, Chevaux: 70, Uniformes: 50");
            
            UIFactory.CreateSeparator(content);
            
            // === ARTILLERY — Requires Armory ===
            UIFactory.CreateSectionHeader(content, "SEC_MIL_ART", "💥 Artillerie", 13);
            if (!hasArmory)
            {
                AddInfoRow(content, "⚠ ARMURERIE REQUISE pour recruter l'artillerie", Color.red, 10);
                AddSpacer(content, 2);
            }
            CreateMilitaryUnitRow(content, city, playerFac, "Artillerie", "🎯", UnitType.Artillery, 300, 40, 4, hasArmory, "Canons: 6, Poudre: 100, Chevaux: 20");
            
            UIFactory.CreateSeparator(content);
            
            // === GARRISON & DEFENSE ===
            UIFactory.CreateSectionHeader(content, "SEC_MIL_DEF", "🏰 Garnison & Défense", 13);
            if (city != null)
            {
                AddInfoRow(content, "Force active", $"{city.garrisonSize}/{city.maxGarrison}", UIFactory.SilverText);
                float garRatio = (float)city.garrisonSize / Mathf.Max(1, city.maxGarrison);
                var (gbg, gfill) = UIFactory.CreateProgressBar(content, "GarBar", garRatio > 0.7f ? Color.green : UIFactory.EmpireGold);
                UIFactory.AddLayoutElement(gbg.gameObject, preferredHeight: 8);
                gfill.GetComponent<RectTransform>().anchorMax = new Vector2(garRatio, 1f);
                
                AddInfoRow(content, "Fortification", $"Grade {city.GetFortificationLevel()}", UIFactory.Porcelain);
            }
            
            UIFactory.CreateSeparator(content);
            
            // === PRODUCTION QUEUE ===
            UIFactory.CreateSectionHeader(content, "SEC_MIL_Q", "📋 File de Recrutement", 13);
            if (city != null && city.productionQueue.Count > 0)
            {
                foreach (var item in city.productionQueue)
                {
                    if (item.itemType == ProductionItemType.Unit)
                    {
                        string unitIcon = GetUnitTypeIcon(item.unitType);
                        AddInfoRow(content, $"  {unitIcon} {item.unitType.ToString().ToUpper()}", $"{item.turnsRemaining} tours", UIFactory.EmpireGold);
                    }
                }
            }
            else
            {
                AddInfoRow(content, "Aucun recrutement en cours", UIFactory.SilverText);
            }
        }
        
        /// <summary>
        /// Create a military unit recruitment row with cost breakdown and recruit button
        /// </summary>
        private void CreateMilitaryUnitRow(Transform parent, CityData city, FactionData faction,
            string unitName, string icon, UnitType unitType, int goldCost, int ironCost, int turns, 
            bool meetsRequirements, string equipmentInfo)
        {
            GameObject row = new GameObject($"U_{unitName}");
            row.transform.SetParent(parent, false);
            UIFactory.AddHorizontalLayout(row, 6f, new RectOffset(8, 8, 3, 3));
            UIFactory.AddLayoutElement(row, preferredHeight: 44);
            Image bg = row.AddComponent<Image>();
            bg.color = meetsRequirements ? new Color(1, 1, 1, 0.04f) : new Color(0.3f, 0.1f, 0.1f, 0.15f);
            
            // Left: unit name and costs
            GameObject leftCol = new GameObject("Left");
            leftCol.transform.SetParent(row.transform, false);
            var vlg = leftCol.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 1f;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            UIFactory.AddLayoutElement(leftCol, flexibleWidth: 1);
            
            Text nameLabel = UIFactory.CreateText(leftCol.transform, "Name", $"{icon} {unitName.ToUpper()}", 11, 
                TextAnchor.MiddleLeft, meetsRequirements ? UIFactory.Porcelain : UIFactory.SilverText);
            nameLabel.fontStyle = FontStyle.Bold;
            
            string costStr = $"  💰{goldCost}g  ⛏{ironCost} fer  ⏱{turns}t";
            UIFactory.CreateText(leftCol.transform, "Cost", costStr, 9, TextAnchor.MiddleLeft, UIFactory.MutedGold);
            
            // Equipment tooltip
            UIFactory.CreateText(leftCol.transform, "Equip", $"  {equipmentInfo}", 8, TextAnchor.MiddleLeft, 
                new Color(0.5f, 0.5f, 0.45f));
            
            // Right: button
            if (meetsRequirements && city != null)
            {
                bool canAfford = faction != null && faction.gold >= goldCost && faction.iron >= ironCost;
                UnitType ct = unitType; int cc = goldCost;
                Button btn = UIFactory.CreateButton(row.transform, "Draft", canAfford ? "RECRUTER" : "FONDS ✗", 9, () =>
                { 
                    if (canAfford) { city.StartUnitProduction(ct, cc); cityPanelDirty = true; RefreshCityPanel(); }
                });
                UIFactory.AddLayoutElement(btn.gameObject, preferredWidth: 75, preferredHeight: 26);
                btn.interactable = canAfford;
            }
            else
            {
                Text locked = UIFactory.CreateText(row.transform, "Lock", "🔒", 12, TextAnchor.MiddleCenter, new Color(0.5f, 0.3f, 0.3f));
                UIFactory.AddLayoutElement(locked.gameObject, preferredWidth: 30);
            }
        }
        
        // ─── INDUSTRY TAB ─────────────────────────────────────────
        private void RefreshIndustryTab(CityData city, ProvinceData prov)
        {
            Transform content = GetTabContent(cityIndustryTab);
            if (content == null) return;
            ClearContent(content);
            
            FactionData playerFac = CampaignManager.Instance?.GetPlayerFaction();
            
            // === PRODUCTION SUMMARY ===
            UIFactory.CreateSectionHeader(content, "SEC_IND_SUM", "📊 Résumé de Production", 13);
            float totalGold = prov.goldIncome, totalFood = prov.foodProduction, totalIron = prov.ironProduction;
            float indGold = 0, indFood = 0, indIron = 0, indWood = 0;
            
            if (city != null)
            {
                foreach (var ind in city.industries)
                {
                    indGold += ind.GetGoldOutput();
                    indFood += ind.GetFoodOutput();
                    indIron += ind.GetIronOutput();
                    indWood += 0; // Wood output not yet implemented
                }
            }
            totalGold += indGold; totalFood += indFood; totalIron += indIron;
            
            AddInfoRow(content, "💰 Or total", $"+{totalGold:F0}/tour (province: {prov.goldIncome:F0}, industrie: {indGold:F0})", UIFactory.EmpireGold);
            AddInfoRow(content, "🌾 Nourriture", $"+{totalFood:F0}/tour (province: {prov.foodProduction:F0}, industrie: {indFood:F0})", new Color(0.5f, 0.8f, 0.3f));
            AddInfoRow(content, "⛏ Fer", $"+{totalIron:F0}/tour (province: {prov.ironProduction:F0}, industrie: {indIron:F0})", UIFactory.SilverText);
            if (indWood > 0) AddInfoRow(content, "🪵 Bois", $"+{indWood:F0}/tour", new Color(0.6f, 0.45f, 0.25f));
            
            UIFactory.CreateSeparator(content);
            
            // === EXISTING INDUSTRIES ===
            UIFactory.CreateSectionHeader(content, "SEC_IND_ACT", "🏭 Industries Actives", 13);
            if (city != null && city.industries.Count > 0)
            {
                foreach (var ind in city.industries)
                {
                    string output = "";
                    if (ind.GetGoldOutput() > 0) output += $"+{ind.GetGoldOutput():F0}g ";
                    if (ind.GetFoodOutput() > 0) output += $"+{ind.GetFoodOutput():F0}🌾 ";
                    if (ind.GetIronOutput() > 0) output += $"+{ind.GetIronOutput():F0}⛏ ";
                    // Wood output not yet implemented
                    
                    string indName = GetIndustryName(ind.industryType);
                    
                    GameObject row = new GameObject($"I_{ind.industryType}");
                    row.transform.SetParent(content, false);
                    UIFactory.AddHorizontalLayout(row, 6f, new RectOffset(8, 8, 3, 3));
                    UIFactory.AddLayoutElement(row, preferredHeight: 40);
                    Image bg = row.AddComponent<Image>();
                    bg.color = new Color(1, 1, 1, 0.04f);
                    
                    // Left info
                    GameObject leftCol = new GameObject("Left");
                    leftCol.transform.SetParent(row.transform, false);
                    var vlg = leftCol.AddComponent<VerticalLayoutGroup>();
                    vlg.spacing = 1f; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
                    UIFactory.AddLayoutElement(leftCol, flexibleWidth: 1);
                    
                    var lbl = UIFactory.CreateText(leftCol.transform, "Lbl", $"{GetIndustryIcon(ind.industryType)} {indName.ToUpper()} NV.{ind.level}", 11, TextAnchor.MiddleLeft, UIFactory.Porcelain);
                    lbl.fontStyle = FontStyle.Bold;
                    UIFactory.CreateText(leftCol.transform, "Out", $"  Production: {output}", 9, TextAnchor.MiddleLeft, UIFactory.MutedGold);
                    
                    // Upgrade button
                    int upgCost = 150 * ind.level;
                    int upgIron = 50 * ind.level;
                    bool canUpgrade = playerFac != null && playerFac.gold >= upgCost && playerFac.iron >= upgIron;
                    IndustrySlot ci = ind;
                    Button btn = UIFactory.CreateButton(row.transform, "Up", canUpgrade ? $"↑ NV.{ind.level + 1}" : "FONDS ✗", 9, () =>
                    { 
                        if (canUpgrade && playerFac != null) 
                        { 
                            playerFac.gold -= upgCost; playerFac.iron -= upgIron;
                            ci.level++; cityPanelDirty = true; RefreshCityPanel(); 
                        }
                    });
                    UIFactory.AddLayoutElement(btn.gameObject, preferredWidth: 70, preferredHeight: 24);
                    btn.interactable = canUpgrade;
                    
                    // Cost label
                    UIFactory.CreateText(row.transform, "UpCost", $"{upgCost}g\n{upgIron}⛏", 8, TextAnchor.MiddleCenter, UIFactory.SilverText);
                }
            }
            else
            {
                AddInfoRow(content, "Aucune industrie — agriculture de subsistance uniquement", UIFactory.SilverText);
            }
            
            UIFactory.CreateSeparator(content);
            
            // === MILITARY EQUIPMENT DEMAND ===
            UIFactory.CreateSectionHeader(content, "SEC_IND_MIL", "⚔ Besoins Militaires", 13);
            if (playerFac != null)
            {
                var stockpile = playerFac.equipment;
                if (stockpile != null)
                {
                    AddEquipmentRow(content, "Mousquets", EquipmentType.Muskets, stockpile);
                    AddEquipmentRow(content, "Baïonnettes", EquipmentType.Bayonets, stockpile);
                    AddEquipmentRow(content, "Sabres", EquipmentType.Sabres, stockpile);
                    AddEquipmentRow(content, "Canons légers", EquipmentType.CannonsLight, stockpile);
                    AddEquipmentRow(content, "Canons lourds", EquipmentType.CannonsHeavy, stockpile);
                    AddEquipmentRow(content, "Chevaux", EquipmentType.Horses, stockpile);
                    AddEquipmentRow(content, "Uniformes", EquipmentType.Uniforms, stockpile);
                }
                else
                {
                    AddInfoRow(content, "Aucun équipement produit", UIFactory.SilverText);
                }
            }

            UIFactory.CreateSeparator(content);
            
            // === ADD NEW INDUSTRY ===
            UIFactory.CreateSectionHeader(content, "SEC_IND_ADD", "➕ Développer l'Industrie", 13);
            
            var allIndustries = new (IndustryType type, string name, string icon, string terrain, int goldCost, int ironCost)[]
            {
                (IndustryType.Agriculture, "Agriculture", "🌾", "Tous", 100, 20),
                (IndustryType.Mining, "Extraction minière", "⛏", "Collines/Montagnes", 200, 30),
                (IndustryType.Logging, "Exploitation forestière", "🪵", "Forêts/Plaines", 120, 15),
                (IndustryType.Commerce, "Commerce", "💰", "Tous", 180, 25),
                (IndustryType.Manufacturing, "Manufacture", "🔧", "Tous", 250, 40),
                (IndustryType.Fishing, "Pêcherie", "🐟", "Côtier", 150, 20),
                (IndustryType.Shipbuilding, "Chantier naval", "⛵", "Côtier", 400, 60),
                (IndustryType.Textile, "Textile", "🧵", "Tous", 200, 30),
            };
            
            foreach (var (type, name, icon, terrain, goldCost, ironCost) in allIndustries)
            {
                if (city != null && city.industries.Exists(i => i.industryType == type)) continue;
                
                // Terrain check
                bool terrainOk = true;
                if (terrain == "Côtier" && prov.terrainType != ProvinceTerrainType.Coastal) terrainOk = false;
                if (terrain == "Collines/Montagnes" && prov.terrainType != ProvinceTerrainType.Hills && prov.terrainType != ProvinceTerrainType.Mountains) terrainOk = false;
                if (terrain == "Forêts/Plaines" && prov.terrainType != ProvinceTerrainType.Forest && prov.terrainType != ProvinceTerrainType.Plains) terrainOk = false;
                
                bool canAfford = playerFac != null && playerFac.gold >= goldCost && playerFac.iron >= ironCost;
                
                GameObject row = new GameObject($"Add_{type}");
                row.transform.SetParent(content, false);
                UIFactory.AddHorizontalLayout(row, 6f, new RectOffset(8, 8, 2, 2));
                UIFactory.AddLayoutElement(row, preferredHeight: 30);
                Image rbg = row.AddComponent<Image>();
                rbg.color = terrainOk ? new Color(1, 1, 1, 0.03f) : new Color(0.3f, 0.1f, 0.1f, 0.1f);
                
                Text lbl = UIFactory.CreateText(row.transform, "Lbl", $"{icon} {name} ({goldCost}g, {ironCost}⛏)", 10, TextAnchor.MiddleLeft, terrainOk ? UIFactory.Porcelain : UIFactory.SilverText);
                UIFactory.AddLayoutElement(lbl.gameObject, flexibleWidth: 1);
                
                if (!terrainOk)
                {
                    Text req = UIFactory.CreateText(row.transform, "Req", $"TERRAIN: {terrain}", 8, TextAnchor.MiddleRight, UIFactory.ImperialCrimson);
                    UIFactory.AddLayoutElement(req.gameObject, preferredWidth: 100);
                }
                else
                {
                    IndustryType ct = type; int gc = goldCost; int ic = ironCost;
                    Button ab = UIFactory.CreateButton(row.transform, "Add", canAfford ? "CONSTRUIRE" : "FONDS ✗", 9, () =>
                    {
                        if (city != null && canAfford && playerFac != null)
                        {
                            playerFac.gold -= gc; playerFac.iron -= ic;
                            city.AddIndustry(ct); cityPanelDirty = true; RefreshCityPanel();
                        }
                    });
                    UIFactory.AddLayoutElement(ab.gameObject, preferredWidth: 85, preferredHeight: 22);
                    ab.interactable = canAfford;
                }
            }
        }
        
        /// <summary>Equipment stockpile row showing current stock for a specific equipment type</summary>
        private void AddEquipmentRow(Transform parent, string name, EquipmentType type, EquipmentStockpile stockpile)
        {
            float stock = stockpile.Get(type);
            Color col = stock > 50 ? Color.green : stock > 10 ? UIFactory.EmpireGold : Color.red;
            AddInfoRow(parent, $"  {name}", $"{stock:F0} en stock", col);
        }
        
        private string GetIndustryName(IndustryType type)
        {
            return type switch
            {
                IndustryType.Agriculture => "Agriculture",
                IndustryType.Mining => "Extraction minière",
                IndustryType.Logging => "Exploitation forestière",
                IndustryType.Commerce => "Commerce",
                IndustryType.Manufacturing => "Manufacture",
                IndustryType.Fishing => "Pêcherie",
                IndustryType.Shipbuilding => "Chantier naval",
                IndustryType.Textile => "Textile",
                _ => type.ToString()
            };
        }
        
        private string GetIndustryIcon(IndustryType type)
        {
            return type switch
            {
                IndustryType.Agriculture => "🌾",
                IndustryType.Mining => "⛏",
                IndustryType.Logging => "🪵",
                IndustryType.Commerce => "💰",
                IndustryType.Manufacturing => "🔧",
                IndustryType.Fishing => "🐟",
                IndustryType.Shipbuilding => "⛵",
                IndustryType.Textile => "🧵",
                _ => "❓"
            };
        }
        
        // ─── HELPERS ──────────────────────────────────────────────
        private void AddSectionLabel(Transform parent, string title)
        {
            Text t = UIFactory.CreateText(parent, $"Sec_{title}", title, 13, TextAnchor.MiddleLeft, new Color(0.7f, 0.6f, 0.35f));
            t.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(t.gameObject, 22);
        }
        
        private void AddInfoRow(Transform parent, string text, Color color, int fontSize = 12, bool bold = false)
        {
            Text t = UIFactory.CreateText(parent, "Info", text, fontSize, TextAnchor.MiddleLeft, color);
            if (bold) t.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(t.gameObject, fontSize + 8);
        }

        private void AddInfoRow(Transform parent, string label, string value, Color color, int fontSize = 12)
        {
            AddInfoRow(parent, $"{label}: {value}", color, fontSize);
        }
        
        private void AddSpacer(Transform parent, int height)
        {
            GameObject spacer = new GameObject("Sp");
            spacer.transform.SetParent(parent, false);
            spacer.AddComponent<RectTransform>();
            UIFactory.AddLayoutElement(spacer, height);
        }



        // ===================== DIPLOMACY UPDATE =====================
        public void RefreshDiplomacy()
        {
            foreach (Transform child in diplomacyContainer)
                Destroy(child.gameObject);

            FactionData player = CampaignManager.Instance.GetPlayerFaction();
            if (player == null) return;

            foreach (var kvp in player.relations)
            {
                FactionType other = kvp.Key;
                DiplomaticRelation rel = kvp.Value;

                Color relColor;
                switch (rel.state)
                {
                    case DiplomacyState.War: relColor = Color.red; break;
                    case DiplomacyState.Hostile: relColor = new Color(1f, 0.5f, 0.2f); break;
                    case DiplomacyState.Friendly: relColor = new Color(0.5f, 0.8f, 0.3f); break;
                    case DiplomacyState.Alliance: relColor = Color.green; break;
                    default: relColor = Color.gray; break;
                }

                GameObject row = new GameObject("DiploRow");
                row.transform.SetParent(diplomacyContainer, false);
                row.AddComponent<RectTransform>();
                UIFactory.AddHorizontalLayout(row, 6f);
                UIFactory.AddLayoutElement(row, 30);

                Text nameT = UIFactory.CreateText(row.transform, "Name", $"{other}", 14);
                nameT.fontStyle = FontStyle.Bold;
                UIFactory.AddLayoutElement(nameT.gameObject, preferredWidth: 100);

                Text stateT = UIFactory.CreateText(row.transform, "State", $"{rel.state}", 12, TextAnchor.MiddleLeft, relColor);
                UIFactory.AddLayoutElement(stateT.gameObject, preferredWidth: 80);

                if (rel.state == DiplomacyState.War)
                {
                    FactionType capturedOther = other;
                    Button btn = UIFactory.CreateGoldButton(row.transform, "BtnPeace", "Peace Treaty", 11, () =>
                        CampaignManager.Instance.ProposePeace(capturedOther));
                    UIFactory.AddLayoutElement(btn.gameObject, 26, 90);
                }
                else if (rel.state != DiplomacyState.Alliance)
                {
                    FactionType capturedOther = other;
                    Button btn = UIFactory.CreateGoldButton(row.transform, "BtnWar", "Declare War", 11, () =>
                        CampaignManager.Instance.DeclarWar(capturedOther));
                    UIFactory.AddLayoutElement(btn.gameObject, 26, 90);
                    // Make it reddish
                    btn.GetComponent<Image>().color = new Color(0.5f, 0.1f, 0.1f, 1f); 
                }
            }
        }

        // Battle preparation panel
        private GameObject battlePrepPanel;
        private Text battlePrepText;
        private ArmyData pendingAttacker;
        private ArmyData pendingDefender;
        private ProvinceData pendingProvince;
        
        // Battle preview UI elements
        private Image attackerForceBar;
        private Image defenderForceBar;
        private Text attackerForceText;
        private Text defenderForceText;
        private Image victoryChanceBar;
        private Text victoryChanceText;
        private Text battlePredictionText;

        /// <summary>
        /// Calculate total battle strength of an army including unit type multipliers
        /// </summary>
        private float CalculateArmyStrength(ArmyData army, bool isDefending = false, int fortificationLevel = 0)
        {
            if (army == null) return 0f;
            
            float totalStrength = 0f;
            
            foreach (var regiment in army.regiments)
            {
                // Base strength per unit type
                float unitStrength = regiment.unitType switch
                {
                    UnitType.LineInfantry => 1.0f,
                    UnitType.LightInfantry => 0.8f,
                    UnitType.Grenadier => 1.3f,
                    UnitType.Cavalry => 1.5f,
                    UnitType.Hussar => 1.4f,
                    UnitType.Lancer => 1.6f,
                    UnitType.Artillery => 2.0f,
                    _ => 1.0f
                };
                
                // Experience bonus (0-50% bonus)
                float expBonus = 1f + (regiment.experience / 100f * 0.5f);
                
                // Size factor (larger units fight more effectively)
                float sizeFactor = Mathf.Sqrt(regiment.currentSize / 10f);
                
                totalStrength += unitStrength * regiment.currentSize * expBonus * sizeFactor;
            }
            
            // Defending bonuses
            if (isDefending)
            {
                // Base defensive bonus
                totalStrength *= 1.2f;
                
                // Fortification bonus (massive impact)
                // Each fortification level adds 25% strength
                float fortBonus = 1f + (fortificationLevel * 0.25f);
                totalStrength *= fortBonus;
            }
            else
            {
                // Attacker bonus for cavalry charge
                int cavalryCount = 0;
                foreach (var r in army.regiments)
                {
                    if (r.unitType == UnitType.Cavalry || r.unitType == UnitType.Hussar || r.unitType == UnitType.Lancer)
                        cavalryCount++;
                }
                float cavRatio = (float)cavalryCount / Mathf.Max(1, army.regiments.Count);
                totalStrength *= (1f + cavRatio * 0.15f); // Up to 15% bonus for all-cavalry army
            }
            
            return totalStrength;
        }
        
        /// <summary>
        /// Get battle prediction text based on victory chance
        /// </summary>
        private string GetBattlePrediction(float attackerChance)
        {
            if (attackerChance >= 80f)
                return "<color=#00FF00><b>Crushing Victory Expected!</b></color>";
            if (attackerChance >= 65f)
                return "<color=#88FF00><b>Favorable Odds</b></color>";
            if (attackerChance >= 50f)
                return "<color=#FFFF00><b>Evenly Matched</b></color>";
            if (attackerChance >= 35f)
                return "<color=#FF8800><b>Challenging Battle</b></color>";
            return "<color=#FF0000><b>Grave Danger - Likely Defeat!</b></color>";
        }
        
        private string GetChanceColor(float chance)
        {
            if (chance >= 70f) return "#44FF44";
            if (chance >= 50f) return "#FFFF44";
            if (chance >= 30f) return "#FF8844";
            return "#FF4444";
        }
        private void ShowBattlePreparation(ArmyData attacker, ArmyData defender, ProvinceData province)
        {
            pendingAttacker = attacker;
            pendingDefender = defender;
            pendingProvince = province;
            
            // Create battle prep panel if it doesn't exist
            if (battlePrepPanel == null)
            {
                CreateBattlePreparationPanel();
            }
            
            // Get fortification info
            int fortificationLevel = 0;
            CityData city = CampaignManager.Instance?.GetCityForProvince(province.provinceId);
            if (city != null)
            {
                fortificationLevel = city.GetFortificationLevel();
            }
            
            // Build battle info text
            string battleText = $"<b><size=20>⚔ BATTLE AT {province.provinceName.ToUpper()} ⚔</size></b>\n\n";
            
            // Attacker info
            battleText += $"<color=#FF4444><b>Attacker:</b></color> {attacker.armyName}\n";
            battleText += $"  Faction: {attacker.faction}\n";
            battleText += $"  Soldiers: {attacker.TotalSoldiers:N0}\n";
            battleText += $"  Regiments: {attacker.regiments.Count}\n\n";
            
            // Defender info
            if (defender != null)
            {
                battleText += $"<color=#4444FF><b>Defender:</b></color> {defender.armyName}\n";
                battleText += $"  Faction: {defender.faction}\n";
                battleText += $"  Soldiers: {defender.TotalSoldiers:N0}\n";
                battleText += $"  Regiments: {defender.regiments.Count}\n\n";
            }
            else
            {
                battleText += $"<color=#4444FF><b>Defender:</b></color> Garrison Forces\n";
                battleText += $"  Faction: {province.owner}\n\n";
            }
            
            // Fortification info
            if (fortificationLevel > 0)
            {
                battleText += $"<b>🏰 Fortification Level: {fortificationLevel}</b>\n";
                battleText += GetFortificationDescription(fortificationLevel) + "\n\n";
            }
            else
            {
                battleText += "<b>Open Field Battle</b> - No fortifications\n\n";
            }
            
            battlePrepText.text = battleText;
            battlePrepPanel.SetActive(true);
            
            // Pause the game
            Time.timeScale = 0f;
        }
        
        private string GetFortificationDescription(int level)
        {
            return level switch
            {
                1 => "Basic wooden palisade - minimal defensive bonus",
                2 => "Stone walls with ditches - moderate defensive bonus",
                3 => "Fortified stone walls with towers - significant defensive bonus",
                4 => "Heavy fortifications with multiple bastions - major defensive bonus",
                5 => "Massive fortress - maximum defensive bonus",
                _ => "Minimal defenses"
            };
        }
        
        private void CreateBattlePreparationPanel()
        {
            // Create panel - larger to accommodate force bars
            RectTransform panel = UIFactory.CreateOrnatePanel(canvas.transform, "BattlePrepPanel",
                new Color(0.05f, 0.03f, 0.02f, 0.98f));
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(500, 600);
            battlePrepPanel = panel.gameObject;
            
            // Get inner content area
            Transform inner = panel.Find("Inner");
            UIFactory.AddVerticalLayout(inner.gameObject, 4f, new RectOffset(20, 20, 15, 15));
            
            // Title
            RectTransform titleBanner = UIFactory.CreateBannerHeader(inner, "TitleBanner", "⚔ BATTLE PREVIEW ⚔", 22);
            UIFactory.AddLayoutElement(titleBanner.gameObject, 45);
            
            // === ATTACKER SECTION ===
            GameObject attackerSection = CreateForceSection(inner, "Attacker", new Color(0.9f, 0.3f, 0.3f), out attackerForceBar, out attackerForceText);
            UIFactory.AddLayoutElement(attackerSection, 70);
            
            // VS Divider
            RectTransform vsDivider = new GameObject("VSDivider").AddComponent<RectTransform>();
            vsDivider.SetParent(inner, false);
            Text vsText = UIFactory.CreateText(vsDivider, "VS", "⚔ VS ⚔", 18, TextAnchor.MiddleCenter, UIFactory.GoldAccent);
            vsText.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(vsDivider.gameObject, 25);
            
            // === DEFENDER SECTION ===
            GameObject defenderSection = CreateForceSection(inner, "Defender", new Color(0.3f, 0.3f, 0.9f), out defenderForceBar, out defenderForceText);
            UIFactory.AddLayoutElement(defenderSection, 70);
            
            // === VICTORY CHANCE BAR ===
            GameObject chanceSection = CreateChanceSection(inner, out victoryChanceBar, out victoryChanceText, out battlePredictionText);
            UIFactory.AddLayoutElement(chanceSection, 100);
            
            // Separator
            RectTransform sep = UIFactory.CreateOrnamentalSeparator(inner);
            UIFactory.AddLayoutElement(sep.gameObject, 12);
            
            // Battle details text
            battlePrepText = UIFactory.CreateText(inner, "BattleDetails", "", 12, TextAnchor.UpperLeft, UIFactory.ParchmentBeige);
            UIFactory.AddLayoutElement(battlePrepText.gameObject, 120, flexibleWidth: 1);
            
            // Button row
            GameObject buttonRow = new GameObject("ButtonRow");
            buttonRow.transform.SetParent(inner, false);
            buttonRow.AddComponent<RectTransform>();
            UIFactory.AddHorizontalLayout(buttonRow, 10f, new RectOffset(0, 0, 5, 5));
            UIFactory.AddLayoutElement(buttonRow, 50);
            
            // Fight button
            Button btnFight = UIFactory.CreateWarhammerButton(buttonRow.transform, "BtnFight", "TO BATTLE!", 16, () =>
            {
                StartBattle();
            });
            UIFactory.AddLayoutElement(btnFight.gameObject, 45, flexibleWidth: 1);
            
            // Retreat button - will be shown/hidden dynamically
            Button btnRetreat = UIFactory.CreateGoldButton(buttonRow.transform, "BtnRetreat", "Retreat", 14, () =>
            {
                CancelBattle();
            });
            btnRetreat.GetComponent<Image>().color = new Color(0.4f, 0.1f, 0.1f, 1f);
            UIFactory.AddLayoutElement(btnRetreat.gameObject, 45, flexibleWidth: 1);
            btnRetreat.gameObject.name = "BtnRetreatDynamic";
            
            battlePrepPanel.SetActive(false);
        }
        
        private GameObject CreateForceSection(Transform parent, string sectionName, Color barColor, out Image forceBar, out Text forceText)
        {
            GameObject section = new GameObject($"{sectionName}Section");
            section.transform.SetParent(parent, false);
            section.AddComponent<RectTransform>();
            VerticalLayoutGroup vlg = UIFactory.AddVerticalLayout(section, 2f, new RectOffset(0, 0, 2, 2));
            
            // Header with name
            GameObject headerRow = new GameObject("HeaderRow");
            headerRow.transform.SetParent(section.transform, false);
            headerRow.AddComponent<RectTransform>();
            UIFactory.AddHorizontalLayout(headerRow, 5f);
            UIFactory.AddLayoutElement(headerRow, 20);
            
            Text nameText = UIFactory.CreateText(headerRow.transform, $"{sectionName}Name", $"{sectionName}: ", 14, TextAnchor.MiddleLeft, UIFactory.ParchmentBeige);
            nameText.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(nameText.gameObject, flexibleWidth: 1);
            
            forceText = UIFactory.CreateText(headerRow.transform, $"{sectionName}Force", "Force: ???", 13, TextAnchor.MiddleRight, UIFactory.GoldAccent);
            UIFactory.AddLayoutElement(forceText.gameObject, 100);
            
            // Bar background
            GameObject barBg = new GameObject("ForceBarBg");
            barBg.transform.SetParent(section.transform, false);
            RectTransform bgRect = barBg.AddComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(0, 20);
            Image bgImage = barBg.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            
            // Force bar fill
            GameObject barFill = new GameObject("ForceBarFill");
            barFill.transform.SetParent(barBg.transform, false);
            RectTransform fillRect = barFill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            forceBar = barFill.AddComponent<Image>();
            forceBar.color = barColor;
            
            return section;
        }
        
        private GameObject CreateChanceSection(Transform parent, out Image chanceBar, out Text chanceText, out Text predictionText)
        {
            GameObject section = new GameObject("ChanceSection");
            section.transform.SetParent(parent, false);
            section.AddComponent<RectTransform>();
            UIFactory.AddVerticalLayout(section, 4f, new RectOffset(0, 0, 5, 5));
            
            // Header
            GameObject headerRow = new GameObject("ChanceHeader");
            headerRow.transform.SetParent(section.transform, false);
            headerRow.AddComponent<RectTransform>();
            UIFactory.AddHorizontalLayout(headerRow, 5f);
            UIFactory.AddLayoutElement(headerRow, 22);
            
            Text headerText = UIFactory.CreateText(headerRow.transform, "ChanceHeader", "⚔ Victory Chance", 14, TextAnchor.MiddleLeft, UIFactory.GoldAccent);
            headerText.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(headerText.gameObject, flexibleWidth: 1);
            
            chanceText = UIFactory.CreateText(headerRow.transform, "ChanceValue", "50%", 14, TextAnchor.MiddleRight, Color.white);
            chanceText.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(chanceText.gameObject, 60);
            
            // Chance bar background
            GameObject barBg = new GameObject("ChanceBarBg");
            barBg.transform.SetParent(section.transform, false);
            RectTransform bgRect = barBg.AddComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(0, 24);
            Image bgImage = barBg.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            
            // Gradient fill - from red (0%) to green (100%)
            GameObject barFill = new GameObject("ChanceBarFill");
            barFill.transform.SetParent(barBg.transform, false);
            RectTransform fillRect = barFill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0.5f, 1f); // Start at 50%
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            chanceBar = barFill.AddComponent<Image>();
            
            // Create gradient texture for the bar
            Texture2D gradientTex = new Texture2D(256, 1);
            for (int x = 0; x < 256; x++)
            {
                float t = x / 255f;
                Color c = Color.Lerp(new Color(1f, 0.2f, 0.2f), new Color(0.2f, 1f, 0.2f), t);
                gradientTex.SetPixel(x, 0, c);
            }
            gradientTex.Apply();
            
            Material gradientMat = new Material(Shader.Find("UI/Default"));
            gradientMat.mainTexture = gradientTex;
            chanceBar.material = gradientMat;
            
            // Prediction text
            predictionText = UIFactory.CreateText(section.transform, "Prediction", "", 13, TextAnchor.MiddleCenter, Color.white);
            predictionText.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(predictionText.gameObject, 22);
            
            return section;
        }
        
        private void StartBattle()
        {
            battlePrepPanel.SetActive(false);
            Time.timeScale = 1f;
            
            // Prepare battle through BattleTransitionManager
            if (BattleTransitionManager.Instance != null)
            {
                BattleTransitionManager.Instance.PrepareBattle(pendingAttacker, pendingDefender, pendingProvince);
            }
            else
            {
                Debug.LogWarning("[CampaignUI] BattleTransitionManager not found!");
            }
        }
        
        private void CancelBattle()
        {
            battlePrepPanel.SetActive(false);
            Time.timeScale = 1f;
            
            // Return army to previous position (simplified - in full implementation, 
            // would need to track where army came from)
            Debug.Log("[CampaignUI] Army retreated from battle");
            
            // Deduct movement point since they tried to move
            if (pendingAttacker != null)
            {
                pendingAttacker.movementPoints = Mathf.Max(0, pendingAttacker.movementPoints - 1);
            }
        }
        
        private string GetArmyCompositionText(ArmyData army, string indent)
        {
            int infantry = 0, cavalry = 0, artillery = 0, elite = 0;
            
            foreach (var reg in army.regiments)
            {
                switch (reg.unitType)
                {
                    case UnitType.LineInfantry:
                    case UnitType.LightInfantry:
                        infantry += reg.currentSize;
                        break;
                    case UnitType.Grenadier:
                        elite += reg.currentSize;
                        break;
                    case UnitType.Cavalry:
                    case UnitType.Hussar:
                    case UnitType.Lancer:
                        cavalry += reg.currentSize;
                        break;
                    case UnitType.Artillery:
                        artillery += reg.currentSize;
                        break;
                }
            }
            
            string text = "";
            if (infantry > 0) text += $"{indent}⚔ Infantry: {infantry}  ";
            if (elite > 0) text += $"💣 Elite: {elite}  ";
            if (cavalry > 0) text += $"🐎 Cavalry: {cavalry}  ";
            if (artillery > 0) text += $"💥 Artillery: {artillery}";
            
            return text + "\n";
        }
        
        private void UpdateForceBars(float attackerStrength, float defenderStrength)
        {
            float maxStrength = Mathf.Max(attackerStrength, defenderStrength, 1000f); // Minimum scale
            
            // Update attacker bar
            float attackerRatio = attackerStrength / maxStrength;
            RectTransform attackerRect = attackerForceBar.GetComponent<RectTransform>();
            attackerRect.anchorMax = new Vector2(attackerRatio, 1f);
            attackerForceText.text = $"Force: {attackerStrength:F0}";
            
            // Update defender bar
            float defenderRatio = defenderStrength / maxStrength;
            RectTransform defenderRect = defenderForceBar.GetComponent<RectTransform>();
            defenderRect.anchorMax = new Vector2(defenderRatio, 1f);
            defenderForceText.text = $"Force: {defenderStrength:F0}";
        }
        
        private void UpdateVictoryChance(float attackerChance)
        {
            // Update bar fill (0 to 1 range for anchoring)
            RectTransform chanceRect = victoryChanceBar.GetComponent<RectTransform>();
            chanceRect.anchorMax = new Vector2(attackerChance / 100f, 1f);
            
            // Update text
            string color = GetChanceColor(attackerChance);
            victoryChanceText.text = $"<color={color}>{attackerChance:F0}%</color>";
            
            // Update prediction
            battlePredictionText.text = GetBattlePrediction(attackerChance);
        }

        private Color GetFactionColor(FactionType faction)
        {
            switch (faction)
            {
                case FactionType.France: return new Color(0.25f, 0.35f, 0.9f);
                case FactionType.Britain: return new Color(0.9f, 0.25f, 0.25f);
                case FactionType.Prussia: return new Color(0.2f, 0.2f, 0.5f);
                case FactionType.Russia: return new Color(0.25f, 0.6f, 0.25f);
                case FactionType.Austria: return new Color(0.9f, 0.9f, 0.85f);
                case FactionType.Spain: return new Color(0.9f, 0.75f, 0.1f); // Gold/Yellow
                case FactionType.Ottoman: return new Color(0.2f, 0.6f, 0.4f); // Green/Teal
                default: return Color.gray;
            }
        }
    }

    // Click handler for the campaign map
    public class CampaignMapClickHandler : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
    {
        public CampaignUI campaignUI;

        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
        {
            RectTransform rt = GetComponent<RectTransform>();
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            {
                // Convert to 0-1 normalized position
                float nx = (localPoint.x / rt.rect.width) + 0.5f;
                float ny = (localPoint.y / rt.rect.height) + 0.5f;

                int mouseButton = 0;
                if (eventData.button == UnityEngine.EventSystems.PointerEventData.InputButton.Right)
                    mouseButton = 1;

                campaignUI.OnMapClicked(new Vector2(nx, ny), mouseButton);
            }
        }
    }
}
