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
        private Text armyMarchStatusText, armyOrgText, armyRationsText;
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
            map3D.OnArmyClicked += OnArmyClicked;  // Second click opens panel
            map3D.OnArmyMoveOrdered += OnArmyMoveOrdered;
            map3D.OnBattleTriggered += OnBattleTriggered;
        }
        
        private void OnArmySelected(ArmyData army)
        {
            // First click: just store selection, DON'T show panel
            selectedArmyId = army?.armyId;
            Debug.Log($"[CampaignUI] Army selected (highlight only): {army?.armyName}");
        }
        
        private void OnArmyClicked(ArmyData army)
        {
            // Second click: open the army detail panel
            selectedArmyId = army?.armyId;
            armyPanelDirty = true;
            Debug.Log($"[CampaignUI] Army clicked → opening panel: {army?.armyName}");
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

        private void BuildArmyPanel()
        {
            // Compact right-side panel — not intrusive
            RectTransform panel = UIFactory.CreateBorderedPanel(canvas.transform, "ArmyPanel",
                UIFactory.DeepCharcoal, UIFactory.BorderGold, 1.5f);
            panel.anchorMin = new Vector2(0.74f, 0.08f);
            panel.anchorMax = new Vector2(0.99f, 0.52f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            armyPanel = panel.gameObject;

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
            UIFactory.AddLayoutElement(headerBg.gameObject, preferredHeight: 68);
            UIFactory.AddHorizontalLayout(headerBg.gameObject, 12f, new RectOffset(18, 14, 10, 10));

            // Army icon
            Text armyIcon = UIFactory.CreateText(headerBg, "Icon", "⚔", 32, TextAnchor.MiddleCenter, UIFactory.EmpireGold);
            UIFactory.AddLayoutElement(armyIcon.gameObject, preferredWidth: 44);

            // Name + march status column
            GameObject nameCol = new GameObject("NameCol");
            nameCol.transform.SetParent(headerBg, false);
            var nameVlg = nameCol.AddComponent<VerticalLayoutGroup>();
            nameVlg.spacing = 0f; nameVlg.childForceExpandWidth = true; nameVlg.childForceExpandHeight = false;
            UIFactory.AddLayoutElement(nameCol, flexibleWidth: 1);

            armyNameText = UIFactory.CreateText(nameCol.transform, "Name", "No army", 24,
                TextAnchor.MiddleLeft, UIFactory.EmpireGold);
            armyNameText.fontStyle = FontStyle.Bold;

            armyMarchStatusText = UIFactory.CreateText(nameCol.transform, "MarchStatus", "", 13,
                TextAnchor.MiddleLeft, UIFactory.Parchment);

            // Close button
            Button closeBtn = UIFactory.CreateButton(headerBg, "CloseBtn", "✕", 18,
                () => { selectedArmyId = null; armyPanelDirty = true; });
            UIFactory.AddLayoutElement(closeBtn.gameObject, preferredWidth: 36, preferredHeight: 36);
            closeBtn.GetComponent<Image>().color = new Color(0.25f, 0.06f, 0.06f, 0.85f);

            // Gold separator
            RectTransform hSep = UIFactory.CreateGlowSeparator(panel, "ArmySep", false);
            UIFactory.AddLayoutElement(hSep.gameObject, preferredHeight: 3);

            // === STATS ROW === soldiers | movement | upkeep | organization
            GameObject statsRow = new GameObject("StatsRow");
            statsRow.transform.SetParent(panel, false);
            statsRow.AddComponent<RectTransform>();
            Image statsBg = statsRow.AddComponent<Image>();
            statsBg.color = new Color(0.08f, 0.08f, 0.09f, 1f);
            UIFactory.AddHorizontalLayout(statsRow, 4f, new RectOffset(12, 12, 6, 6));
            UIFactory.AddLayoutElement(statsRow, preferredHeight: 52);

            armySoldiersText = UIFactory.CreateText(statsRow.transform, "Soldiers", "", 15, TextAnchor.MiddleCenter, UIFactory.Porcelain);
            UIFactory.AddLayoutElement(armySoldiersText.gameObject, flexibleWidth: 1);

            armyMoveText = UIFactory.CreateText(statsRow.transform, "Move", "", 15, TextAnchor.MiddleCenter, new Color(0.4f, 0.7f, 0.4f));
            UIFactory.AddLayoutElement(armyMoveText.gameObject, flexibleWidth: 1);

            armyUpkeepText = UIFactory.CreateText(statsRow.transform, "Upkeep", "", 15, TextAnchor.MiddleCenter, UIFactory.EmpireGold);
            UIFactory.AddLayoutElement(armyUpkeepText.gameObject, flexibleWidth: 1);

            armyOrgText = UIFactory.CreateText(statsRow.transform, "Org", "", 15, TextAnchor.MiddleCenter, new Color(0.5f, 0.7f, 0.9f));
            UIFactory.AddLayoutElement(armyOrgText.gameObject, flexibleWidth: 1);

            armyRationsText = UIFactory.CreateText(statsRow.transform, "Rations", "", 15, TextAnchor.MiddleCenter, new Color(0.6f, 0.8f, 0.3f));
            UIFactory.AddLayoutElement(armyRationsText.gameObject, flexibleWidth: 1);

            // Thin separator
            RectTransform tSep = UIFactory.CreateSeparator(panel);
            UIFactory.AddLayoutElement(tSep.gameObject, preferredHeight: 2);

            // === REGIMENTS SECTION ===
            GameObject regSection = new GameObject("RegSection");
            regSection.transform.SetParent(panel, false);
            regSection.AddComponent<RectTransform>();
            Image regBg = regSection.AddComponent<Image>();
            regBg.color = new Color(0.06f, 0.06f, 0.07f, 1f);
            var regVlg = regSection.AddComponent<VerticalLayoutGroup>();
            regVlg.spacing = 0f;
            regVlg.padding = new RectOffset(12, 12, 8, 8);
            regVlg.childControlHeight = true;
            regVlg.childForceExpandHeight = true;
            regVlg.childControlWidth = true;
            regVlg.childForceExpandWidth = true;
            UIFactory.AddLayoutElement(regSection, flexibleHeight: 1, flexibleWidth: 1);

            // Section header
            Text regLabel = UIFactory.CreateText(regSection.transform, "RegLabel", "⚜ RÉGIMENTS", 16,
                TextAnchor.MiddleLeft, UIFactory.EmpireGold);
            regLabel.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(regLabel.gameObject, preferredHeight: 28);

            // Scrollable regiments list
            var (scroll, content) = UIFactory.CreateScrollView(regSection.transform, "RegimentsScroll");
            UIFactory.AddLayoutElement(scroll.gameObject, 60, flexibleWidth: 1, flexibleHeight: 1);
            
            // Replace the default VerticalLayoutGroup with a GridLayout for square cards
            var existingVlg = content.GetComponent<VerticalLayoutGroup>();
            if (existingVlg != null) Object.DestroyImmediate(existingVlg);
            GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(95, 115);
            grid.spacing = new Vector2(4, 4);
            grid.padding = new RectOffset(4, 4, 4, 4);
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.Flexible;
            
            regimentsContainer = content;

            // === RECRUIT ROW ===
            recruitRow = new GameObject("RecruitRow");
            recruitRow.transform.SetParent(panel, false);
            recruitRow.AddComponent<RectTransform>();
            Image recruitBg = recruitRow.AddComponent<Image>();
            recruitBg.color = new Color(0.08f, 0.07f, 0.06f, 1f);
            UIFactory.AddHorizontalLayout(recruitRow, 6f, new RectOffset(12, 12, 6, 6));
            UIFactory.AddLayoutElement(recruitRow, preferredHeight: 40);

            Button btnInf = UIFactory.CreateGoldButton(recruitRow.transform, "BtnInf", "🔨 Milice (40g)", 11, () =>
            {
                var army = CampaignManager.Instance.Armies.ContainsKey(selectedArmyId) ? CampaignManager.Instance.Armies[selectedArmyId] : null;
                if (army != null) { var city = FindCityInProvince(army.currentProvinceId); city?.StartUnitProduction(UnitType.Militia, 40, true, -1, 10); }
            });
            UIFactory.AddLayoutElement(btnInf.gameObject, 34, flexibleWidth: 1);

            Button btnCav = UIFactory.CreateGoldButton(recruitRow.transform, "BtnCav", "🐴 Cav.Mil (60g)", 11, () =>
            {
                var army = CampaignManager.Instance.Armies.ContainsKey(selectedArmyId) ? CampaignManager.Instance.Armies[selectedArmyId] : null;
                if (army != null) { var city = FindCityInProvince(army.currentProvinceId); city?.StartUnitProduction(UnitType.MilitiaCavalry, 60, true, -1, 20); }
            });
            UIFactory.AddLayoutElement(btnCav.gameObject, 34, flexibleWidth: 1);

            Button btnArt = UIFactory.CreateGoldButton(recruitRow.transform, "BtnArt", "💥 Art. (120g)", 11, () =>
            {
                var army = CampaignManager.Instance.Armies.ContainsKey(selectedArmyId) ? CampaignManager.Instance.Armies[selectedArmyId] : null;
                if (army != null) { var city = FindCityInProvince(army.currentProvinceId); city?.StartUnitProduction(UnitType.Artillery, 120, false, -1, 40); }
            });
            UIFactory.AddLayoutElement(btnArt.gameObject, 34, flexibleWidth: 1);

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
                    bName = $"{BuildingInfo.GetName(slot.type)} ({slot.turnsToComplete}j)";
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
            
            // March status line
            if (army.isMoving && !string.IsNullOrEmpty(army.targetProvinceId))
            {
                string targetName = army.targetProvinceId;
                if (CampaignManager.Instance.Provinces.TryGetValue(army.targetProvinceId, out var tp))
                    targetName = tp.provinceName;
                int daysLeft = army.marchDaysRemaining > 0 ? army.marchDaysRemaining : 1;
                armyMarchStatusText.text = $"En marche vers {targetName} — {daysLeft} jour(s)";
                armyMarchStatusText.color = UIFactory.WarningAmber;
            }
            else if (army.isResting)
            {
                armyMarchStatusText.text = "Au repos — récupération";
                armyMarchStatusText.color = new Color(0.4f, 0.7f, 0.4f);
            }
            else
            {
                string locName = army.currentProvinceId;
                if (CampaignManager.Instance.Provinces.TryGetValue(army.currentProvinceId, out var cp))
                    locName = cp.provinceName;
                armyMarchStatusText.text = $"Stationné à {locName}";
                armyMarchStatusText.color = UIFactory.Parchment;
            }

            // Stat cards with icons
            armySoldiersText.text = $"{army.TotalSoldiers}\nsoldats";
            armyMoveText.text = $"{army.movementPoints}/{army.maxMovementPoints}\nmouvement";
            armyUpkeepText.text = $"{army.MaintenanceCost:F0}\nor/tour";
            armyOrgText.text = $"{army.organization:F0}%\norganisation";

            // Rations display with color coding
            float rationPct = army.maxRations > 0 ? army.currentRations / army.maxRations : 0;
            Color rationColor;
            string rationPrefix = "";
            if (army.isStarving)
            {
                rationColor = new Color(0.9f, 0.15f, 0.15f); // Red
                rationPrefix = "\u26a0 ";
            }
            else if (army.currentRations <= 5f)
                rationColor = new Color(0.9f, 0.55f, 0.2f);  // Orange
            else if (army.currentRations <= 10f)
                rationColor = new Color(0.9f, 0.8f, 0.2f);   // Yellow
            else
                rationColor = new Color(0.5f, 0.8f, 0.3f);   // Green
            armyRationsText.color = rationColor;
            armyRationsText.text = $"{rationPrefix}{army.currentRations:F0}/{army.maxRations:F0}\nrations";

            // Rebuild regiments list
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
            Color typeColor = GetUnitTypeColor(reg.unitType);
            string typeIcon = GetUnitTypeIcon(reg.unitType);
            float strengthRatio = (float)reg.currentSize / reg.maxSize;

            // === SQUARE CARD (90x110) ===
            GameObject card = new GameObject($"RegCard_{reg.regimentName}");
            card.transform.SetParent(parent, false);
            RectTransform cardRect = card.AddComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(90, 110);
            Image cardBg = card.AddComponent<Image>();
            cardBg.color = new Color(0.12f, 0.12f, 0.14f, 0.95f);

            // Left accent stripe (unit type color)
            GameObject stripe = new GameObject("Stripe");
            stripe.transform.SetParent(card.transform, false);
            RectTransform stripeRect = stripe.AddComponent<RectTransform>();
            stripeRect.anchorMin = new Vector2(0, 0);
            stripeRect.anchorMax = new Vector2(0, 1);
            stripeRect.pivot = new Vector2(0, 0.5f);
            stripeRect.sizeDelta = new Vector2(4, 0);
            stripeRect.anchoredPosition = Vector2.zero;
            Image stripeImg = stripe.AddComponent<Image>();
            stripeImg.color = reg.isBeingReinforced ? UIFactory.GoldAccent : typeColor;

            // === TOP SECTION: Big icon + unit type ===
            GameObject topSection = new GameObject("Top");
            topSection.transform.SetParent(card.transform, false);
            RectTransform topRect = topSection.AddComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0, 0.55f);
            topRect.anchorMax = new Vector2(1, 1);
            topRect.offsetMin = new Vector2(6, 0);
            topRect.offsetMax = new Vector2(-4, -3);
            Image topBg = topSection.AddComponent<Image>();
            topBg.color = new Color(typeColor.r * 0.25f, typeColor.g * 0.25f, typeColor.b * 0.25f, 0.9f);

            // Big centered icon
            Text iconText = UIFactory.CreateText(topSection.transform, "Icon", typeIcon, 26, TextAnchor.MiddleCenter, typeColor);
            RectTransform iconRect = iconText.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

            // Unit type label (bottom of top section)
            Text typeLabel = UIFactory.CreateText(topSection.transform, "TypeLabel", GetUnitTypeName(reg.unitType), 8,
                TextAnchor.LowerCenter, new Color(typeColor.r, typeColor.g, typeColor.b, 0.8f));
            RectTransform typeLabelRect = typeLabel.GetComponent<RectTransform>();
            typeLabelRect.anchorMin = new Vector2(0, 0);
            typeLabelRect.anchorMax = new Vector2(1, 0.3f);
            typeLabelRect.offsetMin = new Vector2(2, 1);
            typeLabelRect.offsetMax = new Vector2(-2, 0);

            // === BOTTOM SECTION: Name + stats ===
            GameObject botSection = new GameObject("Bot");
            botSection.transform.SetParent(card.transform, false);
            RectTransform botRect = botSection.AddComponent<RectTransform>();
            botRect.anchorMin = new Vector2(0, 0);
            botRect.anchorMax = new Vector2(1, 0.55f);
            botRect.offsetMin = new Vector2(6, 3);
            botRect.offsetMax = new Vector2(-4, 0);
            VerticalLayoutGroup vlg = botSection.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 1f;
            vlg.padding = new RectOffset(2, 2, 0, 0);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            // Regiment name (truncated)
            string displayName = reg.regimentName.Length > 14 ? reg.regimentName.Substring(0, 12) + ".." : reg.regimentName;
            Text nameText = UIFactory.CreateText(botSection.transform, "Name", displayName, 9, TextAnchor.MiddleCenter, UIFactory.ParchmentBeige);
            nameText.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(nameText.gameObject, 12, flexibleWidth: 1);

            // Experience stars (compact)
            int expStars = Mathf.FloorToInt(reg.experience / 20f);
            string stars = expStars > 0 ? new string('★', Mathf.Clamp(expStars, 0, 5)) : "—";
            Color starColor = expStars >= 4 ? UIFactory.GoldAccent : new Color(0.7f, 0.7f, 0.5f);
            Text starsText = UIFactory.CreateText(botSection.transform, "Stars", stars, 8, TextAnchor.MiddleCenter, starColor);
            UIFactory.AddLayoutElement(starsText.gameObject, 10, flexibleWidth: 1);

            // Strength bar
            GameObject barBg = new GameObject("BarBg");
            barBg.transform.SetParent(botSection.transform, false);
            Image barBgImg = barBg.AddComponent<Image>();
            barBgImg.color = new Color(0.08f, 0.08f, 0.08f, 0.8f);
            UIFactory.AddLayoutElement(barBg, 10, flexibleWidth: 1);

            Color barColor = strengthRatio > 0.7f ? new Color(0.3f, 0.7f, 0.3f) :
                             strengthRatio > 0.4f ? new Color(0.9f, 0.7f, 0.2f) :
                             new Color(0.8f, 0.3f, 0.2f);

            GameObject barFill = new GameObject("BarFill");
            barFill.transform.SetParent(barBg.transform, false);
            RectTransform fillRect = barFill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(strengthRatio, 1f);
            fillRect.offsetMin = new Vector2(1, 1);
            fillRect.offsetMax = new Vector2(-1, -1);
            Image fillImg = barFill.AddComponent<Image>();
            fillImg.color = barColor;

            // Strength count
            Text countText = UIFactory.CreateText(barBg.transform, "Count", $"{reg.currentSize}/{reg.maxSize}", 8, TextAnchor.MiddleCenter, Color.white);
            RectTransform countRect = countText.GetComponent<RectTransform>();
            countRect.anchorMin = Vector2.zero;
            countRect.anchorMax = Vector2.one;
            countRect.offsetMin = Vector2.zero;
            countRect.offsetMax = Vector2.zero;
            countText.fontStyle = FontStyle.Bold;

            // Reinforcement indicator
            if (reg.isBeingReinforced)
            {
                Text reinforceText = UIFactory.CreateText(botSection.transform, "Reinforce",
                    $"🔄 {reg.turnsToCompleteReinforcement}t", 8, TextAnchor.MiddleCenter, UIFactory.GoldAccent);
                UIFactory.AddLayoutElement(reinforceText.gameObject, 10, flexibleWidth: 1);
            }

            UIFactory.AddLayoutElement(card, 110, preferredWidth: 90);
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
                // Infantry
                UnitType.Militia => "🔨",
                UnitType.TrainedMilitia => "⚔",
                UnitType.LineInfantry => "⚔",
                UnitType.LightInfantry => "🏹",
                UnitType.Fusilier => "🔫",
                UnitType.Grenadier => "💣",
                UnitType.Voltigeur => "🎯",
                UnitType.Chasseur => "🦅",
                UnitType.GuardInfantry => "🛡",
                UnitType.OldGuard => "👑",
                // Cavalry
                UnitType.MilitiaCavalry => "🐴",
                UnitType.Dragoon => "🐎",
                UnitType.Cavalry => "🐎",
                UnitType.Hussar => "⚡",
                UnitType.Lancer => "🔱",
                UnitType.Cuirassier => "🛡",
                UnitType.GuardCavalry => "👑",
                UnitType.Mameluke => "⚔",
                // Artillery
                UnitType.GarrisonCannon => "🏰",
                UnitType.Artillery => "💥",
                UnitType.HorseArtillery => "🐎",
                UnitType.Howitzer => "💣",
                UnitType.GrandBattery => "⚡",
                UnitType.GuardArtillery => "👑",
                // Special
                UnitType.Engineer => "⚒",
                UnitType.Sapper => "🔨",
                UnitType.Marine => "⚓",
                UnitType.Partisan => "🗡",
                _ => "⚔"
            };
        }
        
        private Color GetUnitTypeColor(UnitType type)
        {
            return type switch
            {
                // Infantry — greens to gold
                UnitType.Militia => new Color(0.5f, 0.5f, 0.4f),
                UnitType.TrainedMilitia => new Color(0.4f, 0.5f, 0.3f),
                UnitType.LineInfantry => new Color(0.2f, 0.5f, 0.2f),
                UnitType.LightInfantry => new Color(0.3f, 0.6f, 0.3f),
                UnitType.Fusilier => new Color(0.3f, 0.55f, 0.25f),
                UnitType.Grenadier => new Color(0.5f, 0.3f, 0.2f),
                UnitType.Voltigeur => new Color(0.4f, 0.6f, 0.2f),
                UnitType.Chasseur => new Color(0.1f, 0.4f, 0.2f),
                UnitType.GuardInfantry => new Color(1f, 0.85f, 0.3f),
                UnitType.OldGuard => new Color(1f, 0.75f, 0.1f),
                // Cavalry — yellows/oranges
                UnitType.MilitiaCavalry => new Color(0.6f, 0.5f, 0.3f),
                UnitType.Dragoon => new Color(0.6f, 0.6f, 0.3f),
                UnitType.Cavalry => new Color(0.8f, 0.6f, 0.2f),
                UnitType.Hussar => new Color(0.9f, 0.5f, 0.1f),
                UnitType.Lancer => new Color(0.7f, 0.7f, 0.3f),
                UnitType.Cuirassier => new Color(0.7f, 0.7f, 0.7f),
                UnitType.GuardCavalry => new Color(1f, 0.7f, 0.2f),
                UnitType.Mameluke => new Color(0.9f, 0.3f, 0.1f),
                // Artillery — grays/bronze
                UnitType.GarrisonCannon => new Color(0.5f, 0.5f, 0.5f),
                UnitType.Artillery => new Color(0.4f, 0.4f, 0.4f),
                UnitType.HorseArtillery => new Color(0.5f, 0.5f, 0.3f),
                UnitType.Howitzer => new Color(0.6f, 0.4f, 0.2f),
                UnitType.GrandBattery => new Color(0.5f, 0.4f, 0.15f),
                UnitType.GuardArtillery => new Color(0.6f, 0.5f, 0.2f),
                // Special — blues/purples
                UnitType.Engineer => new Color(0.3f, 0.5f, 0.7f),
                UnitType.Sapper => new Color(0.4f, 0.4f, 0.6f),
                UnitType.Marine => new Color(0.2f, 0.4f, 0.7f),
                UnitType.Partisan => new Color(0.6f, 0.3f, 0.3f),
                _ => Color.gray
            };
        }
        
        private string GetUnitTypeName(UnitType type)
        {
            return type switch
            {
                // Infantry
                UnitType.Militia => "Milice",
                UnitType.TrainedMilitia => "Milice Entr.",
                UnitType.LineInfantry => "Inf. Ligne",
                UnitType.LightInfantry => "Inf. Légère",
                UnitType.Fusilier => "Fusilier",
                UnitType.Grenadier => "Grenadier",
                UnitType.Voltigeur => "Voltigeur",
                UnitType.Chasseur => "Chasseur",
                UnitType.GuardInfantry => "Garde Inf.",
                UnitType.OldGuard => "Vieille Garde",
                // Cavalry
                UnitType.MilitiaCavalry => "Cav. Milice",
                UnitType.Dragoon => "Dragon",
                UnitType.Cavalry => "Cavalerie",
                UnitType.Hussar => "Hussard",
                UnitType.Lancer => "Lancier",
                UnitType.Cuirassier => "Cuirassier",
                UnitType.GuardCavalry => "Garde Cav.",
                UnitType.Mameluke => "Mamelouk",
                // Artillery
                UnitType.GarrisonCannon => "Canon Garn.",
                UnitType.Artillery => "Artillerie",
                UnitType.HorseArtillery => "Art. Cheval",
                UnitType.Howitzer => "Obusier",
                UnitType.GrandBattery => "Gd. Batterie",
                UnitType.GuardArtillery => "Garde Art.",
                // Special
                UnitType.Engineer => "Ingénieur",
                UnitType.Sapper => "Sapeur",
                UnitType.Marine => "Marine",
                UnitType.Partisan => "Partisan",
                _ => "Inconnu"
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
                string label = $"{BuildingInfo.GetName(type)} ({cost}g, {time}j)";

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
            panel.anchorMin = new Vector2(0.50f, 0.06f);
            panel.anchorMax = new Vector2(0.99f, 0.95f);
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
            UIFactory.AddLayoutElement(headerBg.gameObject, preferredHeight: 72);
            
            UIFactory.AddHorizontalLayout(headerBg.gameObject, 12f, new RectOffset(20, 16, 10, 10));
            
            // Gold emblem
            Text emblem = UIFactory.CreateText(headerBg, "Emblem", "🏛", 36, TextAnchor.MiddleCenter, UIFactory.EmpireGold);
            UIFactory.AddLayoutElement(emblem.gameObject, preferredWidth: 48);
            
            // City name + level column
            GameObject nameCol = new GameObject("NameCol");
            nameCol.transform.SetParent(headerBg, false);
            var nameVlg = nameCol.AddComponent<VerticalLayoutGroup>();
            nameVlg.spacing = 0f; nameVlg.childForceExpandWidth = true; nameVlg.childForceExpandHeight = false;
            UIFactory.AddLayoutElement(nameCol, flexibleWidth: 1);
            
            cityNameText = UIFactory.CreateText(nameCol.transform, "CityName", "CITY", 26,
                TextAnchor.MiddleLeft, UIFactory.EmpireGold);
            cityNameText.fontStyle = FontStyle.Bold;
            
            cityLevelText = UIFactory.CreateText(nameCol.transform, "CityLevel", "", 15,
                TextAnchor.MiddleLeft, UIFactory.Parchment);
            
            // Close button — elegant X
            Button closeBtn = UIFactory.CreateButton(headerBg, "CloseBtn", "✕", 20,
                () => CloseCityPanel());
            UIFactory.AddLayoutElement(closeBtn.gameObject, preferredWidth: 40, preferredHeight: 40);
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
            UIFactory.AddHorizontalLayout(tabBar, 2f, new RectOffset(6, 6, 4, 4));
            UIFactory.AddLayoutElement(tabBar, preferredHeight: 44);
            
            string[] tabNames = { "⚜ VUE", "🏗 BÂTIMENTS", "⚔ MILITAIRE", "🏭 INDUSTRIE" };
            cityTabButtons = new Button[4];
            for (int t = 0; t < 4; t++)
            {
                int tabIdx = t;
                cityTabButtons[t] = UIFactory.CreateButton(tabBar.transform, $"Tab_{t}",
                    tabNames[t], 14, () => { activeCityTab = tabIdx; RefreshCityPanel(); });
                UIFactory.AddLayoutElement(cityTabButtons[t].gameObject, flexibleWidth: 1, preferredHeight: 36);
            }
            
            // Thin separator under tabs
            RectTransform tSep = UIFactory.CreateSeparator(panel);
            UIFactory.AddLayoutElement(tSep.gameObject, preferredHeight: 2);
            
            // === TAB CONTENT CONTAINERS — with padding ===
            GameObject contentArea = new GameObject("ContentArea");
            contentArea.transform.SetParent(panel, false);
            contentArea.AddComponent<RectTransform>();
            // Opaque dark background to prevent map bleed-through
            Image contentAreaBg = contentArea.AddComponent<Image>();
            contentAreaBg.color = new Color(0.06f, 0.06f, 0.07f, 1f);
            var cavlg = contentArea.AddComponent<VerticalLayoutGroup>();
            cavlg.spacing = 0f;
            cavlg.padding = new RectOffset(14, 14, 10, 10);
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
            // Force opaque background on scroll view
            Image scrollBg = scroll.gameObject.GetComponent<Image>();
            if (scrollBg == null) scrollBg = scroll.gameObject.AddComponent<Image>();
            scrollBg.color = new Color(0.06f, 0.06f, 0.07f, 1f);
            // Force opaque viewport
            Transform viewport = scroll.transform.childCount > 0 ? scroll.transform.GetChild(0) : null;
            if (viewport != null)
            {
                Image vpBg = viewport.GetComponent<Image>();
                if (vpBg == null) vpBg = viewport.gameObject.AddComponent<Image>();
                vpBg.color = new Color(0.06f, 0.06f, 0.07f, 1f);
            }
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
            
            // Force opaque background
            Image contentBg = content.gameObject.GetComponent<Image>();
            if (contentBg == null) contentBg = content.gameObject.AddComponent<Image>();
            contentBg.color = new Color(0.06f, 0.06f, 0.07f, 1f);
            
            // ═══════════════ CITY LEVEL & PROGRESS ═══════════════
            if (city != null)
            {
                // Level badge row
                GameObject levelRow = new GameObject("LevelRow");
                levelRow.transform.SetParent(content, false);
                UIFactory.AddHorizontalLayout(levelRow, 12f, new RectOffset(16, 16, 10, 6));
                UIFactory.AddLayoutElement(levelRow, preferredHeight: 50);
                Image levelBg = levelRow.AddComponent<Image>();
                levelBg.color = new Color(0.10f, 0.08f, 0.05f, 1f);
                
                // Level badge
                string levelName = CityLevelThresholds.GetLevelName(city.cityLevel);
                Text lvlBadge = UIFactory.CreateText(levelRow.transform, "LvlBadge", 
                    $"NV.{city.cityLevel}", 24, TextAnchor.MiddleCenter, UIFactory.EmpireGold);
                lvlBadge.fontStyle = FontStyle.Bold;
                UIFactory.AddLayoutElement(lvlBadge.gameObject, preferredWidth: 60);
                
                // Level name + next threshold
                GameObject lvlInfo = new GameObject("LvlInfo");
                lvlInfo.transform.SetParent(levelRow.transform, false);
                var lvlVlg = lvlInfo.AddComponent<VerticalLayoutGroup>();
                lvlVlg.spacing = 0f; lvlVlg.childForceExpandWidth = true; lvlVlg.childForceExpandHeight = false;
                UIFactory.AddLayoutElement(lvlInfo, flexibleWidth: 1);
                
                Text lvlName = UIFactory.CreateText(lvlInfo.transform, "LvlName", levelName.ToUpper(), 18, 
                    TextAnchor.MiddleLeft, UIFactory.Porcelain);
                lvlName.fontStyle = FontStyle.Bold;
                UIFactory.AddLayoutElement(lvlName.gameObject, preferredHeight: 24);
                
                int nextThresh = CityLevelThresholds.GetNextThreshold(city.cityLevel);
                string progStr = nextThresh > 0 
                    ? $"{city.population:N0} / {nextThresh:N0} hab." 
                    : "Population maximale";
                Text progLabel = UIFactory.CreateText(lvlInfo.transform, "ProgLabel", progStr, 14, 
                    TextAnchor.MiddleLeft, UIFactory.MutedGold);
                UIFactory.AddLayoutElement(progLabel.gameObject, preferredHeight: 18);
                
                // Progress bar
                if (nextThresh > 0)
                {
                    int prevThresh = city.cityLevel > 1 ? CityLevelThresholds.GetNextThreshold(city.cityLevel - 1) : 0;
                    float progress = (float)(city.population - prevThresh) / Mathf.Max(1, nextThresh - prevThresh);
                    
                    GameObject progBar = new GameObject("ProgBar");
                    progBar.transform.SetParent(content, false);
                    UIFactory.AddLayoutElement(progBar, preferredHeight: 6);
                    Image progBarBg = progBar.AddComponent<Image>();
                    progBarBg.color = new Color(0.15f, 0.13f, 0.10f, 1f);
                    
                    GameObject progFill = new GameObject("ProgFill");
                    progFill.transform.SetParent(progBar.transform, false);
                    RectTransform fillRT = progFill.AddComponent<RectTransform>();
                    fillRT.anchorMin = Vector2.zero;
                    fillRT.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
                    fillRT.offsetMin = Vector2.zero;
                    fillRT.offsetMax = Vector2.zero;
                    Image fillImg = progFill.AddComponent<Image>();
                    fillImg.color = UIFactory.EmpireGold;
                }
            }
            
            AddPremiumSpacer(content, 4);
            
            // ═══════════════ POPULATION PYRAMID ═══════════════
            CreateMilitarySectionHeader(content, "⚜ SOCIÉTÉ IMPÉRIALE", "");
            
            if (city != null)
            {
                // Total pop
                CreateCompactDataRow(content, "Population totale", $"{city.population:N0} habitants", UIFactory.Porcelain, false);
                
                // Population classes with mini progress bars
                float nobleR = city.populationData.NobleRatio;
                float bourgeoisR = city.populationData.BourgeoisRatio;
                float workerR = city.populationData.WorkerRatio;
                
                CreatePopulationBar(content, "👑 Noblesse", city.populationData.nobles, nobleR, 
                    new Color(0.7f, 0.5f, 0.9f));
                CreatePopulationBar(content, "💼 Bourgeoisie", city.populationData.bourgeois, bourgeoisR, 
                    UIFactory.EmpireGold);
                CreatePopulationBar(content, "⚒ Ouvriers", city.populationData.workers, workerR, 
                    new Color(0.55f, 0.55f, 0.50f));
            }
            else
            {
                CreateCompactDataRow(content, "Population régionale", $"{prov.population:N0}", UIFactory.Parchment, false);
            }
            
            AddPremiumSpacer(content, 4);
            
            // ═══════════════ ECONOMY ═══════════════
            CreateMilitarySectionHeader(content, "💰 ÉCONOMIE", "");
            
            // Resource grid - 3 columns
            GameObject resGrid = new GameObject("ResourceGrid");
            resGrid.transform.SetParent(content, false);
            UIFactory.AddHorizontalLayout(resGrid, 4f, new RectOffset(8, 8, 4, 4));
            UIFactory.AddLayoutElement(resGrid, preferredHeight: 50);
            Image resGridBg = resGrid.AddComponent<Image>();
            resGridBg.color = new Color(0.08f, 0.08f, 0.09f, 1f);
            
            CreateResourceCard(resGrid.transform, "💰", "Or", $"+{prov.goldIncome:F0}", UIFactory.EmpireGold);
            CreateResourceCard(resGrid.transform, "🌾", "Nourriture", $"+{prov.foodProduction:F0}", new Color(0.5f, 0.8f, 0.3f));
            CreateResourceCard(resGrid.transform, "⛏", "Fer", $"+{prov.ironProduction:F0}", UIFactory.SilverText);
            
            if (city != null)
            {
                // Stockpile row
                GameObject stockRow = new GameObject("StockRow");
                stockRow.transform.SetParent(content, false);
                UIFactory.AddHorizontalLayout(stockRow, 4f, new RectOffset(8, 8, 2, 2));
                UIFactory.AddLayoutElement(stockRow, preferredHeight: 22);
                Image stockBg = stockRow.AddComponent<Image>();
                stockBg.color = new Color(0.07f, 0.07f, 0.08f, 1f);
                
                Text stockLabel = UIFactory.CreateText(stockRow.transform, "Stock", 
                    $"📦 Stock:  🌾 {city.storedFood:F0}  ⛏ {city.storedIron:F0}  📦 {city.storedGoods:F0}", 
                    10, TextAnchor.MiddleLeft, new Color(0.45f, 0.45f, 0.42f));
                UIFactory.AddLayoutElement(stockLabel.gameObject, flexibleWidth: 1);
            }
            
            AddPremiumSpacer(content, 4);
            
            // ═══════════════ DEFENSE ═══════════════
            CreateMilitarySectionHeader(content, "🏰 DÉFENSE", "");
            
            if (city != null)
            {
                // Public order
                float order = city.publicOrder;
                Color orderCol = order > 70 ? new Color(0.4f, 0.75f, 0.35f) : order > 40 ? UIFactory.EmpireGold : new Color(0.85f, 0.3f, 0.2f);
                CreateCompactDataRow(content, "Ordre public", $"{order:F0}/100", orderCol, true, order / 100f);
                
                // Fortification stars
                int fortLevel = city.GetFortificationLevel();
                string fortStars = new string('★', fortLevel) + new string('☆', 5 - fortLevel);
                CreateCompactDataRow(content, "Fortification", fortStars, UIFactory.EmpireGold, false);
                
                // Garrison
                CreateCompactDataRow(content, "Garnison", 
                    $"{city.garrisonSize}/{city.maxGarrison}", UIFactory.SilverText, false);
            }
            else
            {
                CreateCompactDataRow(content, "Terrain", prov.terrainType.ToString().ToUpper(), UIFactory.Parchment, false);
                CreateCompactDataRow(content, "Garnison", $"{prov.garrisonSize}", UIFactory.SilverText, false);
            }
            
            AddPremiumSpacer(content, 4);
            
            // ═══════════════ CULTURE & STATUS ═══════════════
            CreateMilitarySectionHeader(content, "🗺 PROVINCE", "");
            
            CreateCompactDataRow(content, "Culture", prov.primaryCulture.ToString(), 
                prov.IsCultureAccepted ? new Color(0.4f, 0.7f, 0.35f) : new Color(1f, 0.5f, 0.2f), false);
            CreateCompactDataRow(content, "Religion", prov.primaryReligion.ToString(), 
                prov.IsReligionAccepted ? new Color(0.4f, 0.7f, 0.35f) : new Color(1f, 0.5f, 0.2f), false);
            
            if (!prov.IsCultureAccepted)
                CreateCompactDataRow(content, "Assimilation", $"{(prov.cultureAssimilation * 100):F0}%", UIFactory.MutedGold, true, prov.cultureAssimilation);
            
            CreateCompactDataRow(content, "Terrain", prov.terrainType.ToString(), UIFactory.Porcelain, false);
            
            // Status indicators — only show if something is wrong
            Color devColor = prov.devastation < 10 ? new Color(0.4f, 0.7f, 0.35f) : prov.devastation < 40 ? UIFactory.EmpireGold : new Color(0.85f, 0.3f, 0.2f);
            CreateCompactDataRow(content, "Dévastation", $"{prov.devastation:F0}%", devColor, true, prov.devastation / 100f);
            
            Color prosColor = prov.prosperity > 60 ? new Color(0.4f, 0.7f, 0.35f) : prov.prosperity > 30 ? UIFactory.EmpireGold : new Color(0.85f, 0.3f, 0.2f);
            CreateCompactDataRow(content, "Prospérité", $"{prov.prosperity:F0}%", prosColor, true, prov.prosperity / 100f);
            
            // Warnings
            if (prov.isOccupied) CreateWarningRow(content, "⚠ PROVINCE OCCUPÉE", $"Depuis {prov.turnsOccupied} jours");
            if (!prov.isCored) CreateWarningRow(content, "⚠ NON ANNEXÉE", "Intégration en cours");
            if (prov.hasActiveRevolt) CreateWarningRow(content, "🔥 RÉVOLTE ACTIVE", $"{prov.revoltStrength} rebelles");
        }
        
        // ═══ PREMIUM UI HELPERS ═══
        
        /// <summary>Compact data row with label/value, optional progress bar</summary>
        private void CreateCompactDataRow(Transform parent, string label, string value, Color valueColor, bool showBar, float barValue = 0f)
        {
            GameObject row = new GameObject($"Data_{label}");
            row.transform.SetParent(parent, false);
            UIFactory.AddLayoutElement(row, preferredHeight: showBar ? 36 : 32);
            Image bg = row.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.09f, 1f);
            
            // Label
            Text lbl = UIFactory.CreateText(row.transform, "Lbl", label, 15, TextAnchor.MiddleLeft, UIFactory.Parchment);
            RectTransform lblRT = lbl.GetComponent<RectTransform>();
            lblRT.anchorMin = new Vector2(0, showBar ? 0.35f : 0);
            lblRT.anchorMax = new Vector2(0.55f, 1);
            lblRT.offsetMin = new Vector2(16, 0);
            lblRT.offsetMax = Vector2.zero;
            
            // Value
            Text val = UIFactory.CreateText(row.transform, "Val", value, 15, TextAnchor.MiddleRight, valueColor);
            val.fontStyle = FontStyle.Bold;
            RectTransform valRT = val.GetComponent<RectTransform>();
            valRT.anchorMin = new Vector2(0.55f, showBar ? 0.35f : 0);
            valRT.anchorMax = Vector2.one;
            valRT.offsetMin = Vector2.zero;
            valRT.offsetMax = new Vector2(-16, 0);
            
            // Progress bar
            if (showBar)
            {
                GameObject barBg = new GameObject("BarBg");
                barBg.transform.SetParent(row.transform, false);
                RectTransform barBgRT = barBg.AddComponent<RectTransform>();
                barBgRT.anchorMin = new Vector2(0.05f, 0.06f);
                barBgRT.anchorMax = new Vector2(0.95f, 0.30f);
                barBgRT.offsetMin = Vector2.zero;
                barBgRT.offsetMax = Vector2.zero;
                Image barBgImg = barBg.AddComponent<Image>();
                barBgImg.color = new Color(0.15f, 0.14f, 0.12f, 1f);
                
                GameObject barFill = new GameObject("BarFill");
                barFill.transform.SetParent(barBg.transform, false);
                RectTransform barFillRT = barFill.AddComponent<RectTransform>();
                barFillRT.anchorMin = Vector2.zero;
                barFillRT.anchorMax = new Vector2(Mathf.Clamp01(barValue), 1f);
                barFillRT.offsetMin = Vector2.zero;
                barFillRT.offsetMax = Vector2.zero;
                Image barFillImg = barFill.AddComponent<Image>();
                barFillImg.color = valueColor;
            }
        }
        
        /// <summary>Population class bar with name, count, and visual ratio</summary>
        private void CreatePopulationBar(Transform parent, string className, int count, float ratio, Color color)
        {
            GameObject row = new GameObject($"Pop_{className}");
            row.transform.SetParent(parent, false);
            UIFactory.AddLayoutElement(row, preferredHeight: 32);
            Image bg = row.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.09f, 1f);
            
            // Class name
            Text lbl = UIFactory.CreateText(row.transform, "Lbl", className, 14, TextAnchor.MiddleLeft, color);
            RectTransform lblRT = lbl.GetComponent<RectTransform>();
            lblRT.anchorMin = new Vector2(0, 0);
            lblRT.anchorMax = new Vector2(0.40f, 1);
            lblRT.offsetMin = new Vector2(16, 0);
            lblRT.offsetMax = Vector2.zero;
            
            // Population bar
            GameObject barBg = new GameObject("BarBg");
            barBg.transform.SetParent(row.transform, false);
            RectTransform barBgRT = barBg.AddComponent<RectTransform>();
            barBgRT.anchorMin = new Vector2(0.42f, 0.2f);
            barBgRT.anchorMax = new Vector2(0.80f, 0.8f);
            barBgRT.offsetMin = Vector2.zero;
            barBgRT.offsetMax = Vector2.zero;
            Image barBgImg = barBg.AddComponent<Image>();
            barBgImg.color = new Color(0.15f, 0.14f, 0.12f, 1f);
            
            GameObject barFill = new GameObject("BarFill");
            barFill.transform.SetParent(barBg.transform, false);
            RectTransform barFillRT = barFill.AddComponent<RectTransform>();
            barFillRT.anchorMin = Vector2.zero;
            barFillRT.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
            barFillRT.offsetMin = Vector2.zero;
            barFillRT.offsetMax = Vector2.zero;
            Image barFillImg = barFill.AddComponent<Image>();
            barFillImg.color = color;
            
            // Count + percentage
            Text val = UIFactory.CreateText(row.transform, "Val", $"{(ratio * 100):F0}% ({count:N0})", 13, 
                TextAnchor.MiddleRight, new Color(color.r * 0.8f, color.g * 0.8f, color.b * 0.8f));
            RectTransform valRT = val.GetComponent<RectTransform>();
            valRT.anchorMin = new Vector2(0.80f, 0);
            valRT.anchorMax = Vector2.one;
            valRT.offsetMin = Vector2.zero;
            valRT.offsetMax = new Vector2(-12, 0);
        }
        
        /// <summary>Resource card for the economy grid</summary>
        private void CreateResourceCard(Transform parent, string icon, string label, string value, Color color)
        {
            GameObject card = new GameObject($"Res_{label}");
            card.transform.SetParent(parent, false);
            UIFactory.AddLayoutElement(card, flexibleWidth: 1, preferredHeight: 52);
            Image bg = card.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.09f, 0.08f, 1f);
            
            // Icon
            Text iconText = UIFactory.CreateText(card.transform, "Icon", icon, 22, TextAnchor.MiddleCenter, color);
            RectTransform iconRT = iconText.GetComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0, 0.5f);
            iconRT.anchorMax = new Vector2(1, 1);
            iconRT.offsetMin = Vector2.zero;
            iconRT.offsetMax = Vector2.zero;
            
            // Value
            Text valText = UIFactory.CreateText(card.transform, "Val", value, 14, TextAnchor.MiddleCenter, color);
            valText.fontStyle = FontStyle.Bold;
            RectTransform valRT = valText.GetComponent<RectTransform>();
            valRT.anchorMin = Vector2.zero;
            valRT.anchorMax = new Vector2(1, 0.5f);
            valRT.offsetMin = Vector2.zero;
            valRT.offsetMax = Vector2.zero;
        }
        
        /// <summary>Red warning row for critical status</summary>
        private void CreateWarningRow(Transform parent, string title, string detail)
        {
            GameObject row = new GameObject("Warning");
            row.transform.SetParent(parent, false);
            UIFactory.AddHorizontalLayout(row, 6f, new RectOffset(12, 12, 3, 3));
            UIFactory.AddLayoutElement(row, preferredHeight: 24);
            Image bg = row.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.05f, 0.05f, 1f);
            
            Text lbl = UIFactory.CreateText(row.transform, "Warn", title, 11, TextAnchor.MiddleLeft, new Color(0.9f, 0.3f, 0.25f));
            lbl.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(lbl.gameObject, flexibleWidth: 1);
            
            Text det = UIFactory.CreateText(row.transform, "Detail", detail, 10, TextAnchor.MiddleRight, new Color(0.7f, 0.3f, 0.25f));
            UIFactory.AddLayoutElement(det.gameObject, preferredWidth: 120);
        }
        
        // ─── BUILDINGS TAB ────────────────────────────────────────
        private void RefreshBuildingsTab(CityData city, ProvinceData prov)
        {
            Transform content = GetTabContent(cityBuildingsTab);
            if (content == null) return;
            ClearContent(content);
            
            // Force opaque background
            Image contentBg = content.gameObject.GetComponent<Image>();
            if (contentBg == null) contentBg = content.gameObject.AddComponent<Image>();
            contentBg.color = new Color(0.06f, 0.06f, 0.07f, 1f);
            
            // ═══════════════ HEADER SUMMARY ═══════════════
            if (city != null)
            {
                int built = 0, building = 0, empty = 0;
                if (prov.buildings != null)
                    foreach (var b in prov.buildings)
                        if (b.type == BuildingType.Empty) empty++; 
                        else if (b.isConstructing) building++; 
                        else built++;
                
                GameObject summaryRow = new GameObject("Summary");
                summaryRow.transform.SetParent(content, false);
                UIFactory.AddHorizontalLayout(summaryRow, 4f, new RectOffset(8, 8, 4, 4));
                UIFactory.AddLayoutElement(summaryRow, preferredHeight: 36);
                Image summaryBg = summaryRow.AddComponent<Image>();
                summaryBg.color = new Color(0.10f, 0.08f, 0.05f, 1f);
                
                CreateResourceCard(summaryRow.transform, "✅", "Construits", $"{built}", new Color(0.4f, 0.8f, 0.4f));
                CreateResourceCard(summaryRow.transform, "🔨", "En cours", $"{building}", UIFactory.EmpireGold);
                CreateResourceCard(summaryRow.transform, "☐", "Vides", $"{empty}", UIFactory.SilverText);
            }
            
            AddPremiumSpacer(content, 4);
            
            // ═══════════════ BUILDING SLOTS ═══════════════
            if (prov.buildings != null && prov.buildings.Length > 0)
            {
                // Group by category
                var categories = new Dictionary<BuildingCategory, List<(int idx, BuildingSlot slot)>>();
                var emptySlots = new List<int>();
                
                for (int i = 0; i < prov.buildings.Length; i++)
                {
                    BuildingSlot slot = prov.buildings[i];
                    if (slot.type == BuildingType.Empty) { emptySlots.Add(i); continue; }
                    BuildingCategory cat = BuildingInfo.GetCategory(slot.type);
                    if (!categories.ContainsKey(cat))
                        categories[cat] = new List<(int, BuildingSlot)>();
                    categories[cat].Add((i, slot));
                }
                
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
                        BuildingCategory.Military => "ÉDIFICES MILITAIRES",
                        BuildingCategory.Economy => "ÉCONOMIE ROYALE",
                        BuildingCategory.Religion => "LIEUX SACRÉS",
                        BuildingCategory.Academic => "ACADÉMIE IMPÉRIALE",
                        _ => "INFRASTRUCTURE"
                    };
                    CreateMilitarySectionHeader(content, $"{catIcon} {catName}", "");
                    
                    foreach (var (idx, slot) in categories[cat])
                    {
                        string name = BuildingInfo.GetName(slot.type);
                        string icon = BuildingInfo.GetIcon(slot.type);
                        
                        GameObject row = new GameObject($"Bld_{name}");
                        row.transform.SetParent(content, false);
                        UIFactory.AddHorizontalLayout(row, 8f, new RectOffset(10, 10, 4, 4));
                        UIFactory.AddLayoutElement(row, preferredHeight: 44);
                        Image rowBg = row.AddComponent<Image>();
                        
                        // Icon container
                        GameObject iconC = new GameObject("IconC");
                        iconC.transform.SetParent(row.transform, false);
                        UIFactory.AddLayoutElement(iconC, preferredWidth: 32, preferredHeight: 32);
                        Image iconBg = iconC.AddComponent<Image>();
                        iconBg.color = new Color(0.12f, 0.10f, 0.08f, 1f);
                        Text iconTxt = UIFactory.CreateText(iconC.transform, "Icon", icon, 18, TextAnchor.MiddleCenter, UIFactory.EmpireGold);
                        RectTransform iconTxtRT = iconTxt.GetComponent<RectTransform>();
                        iconTxtRT.anchorMin = Vector2.zero; iconTxtRT.anchorMax = Vector2.one;
                        iconTxtRT.offsetMin = Vector2.zero; iconTxtRT.offsetMax = Vector2.zero;
                        
                        // Info column
                        GameObject infoC = new GameObject("Info");
                        infoC.transform.SetParent(row.transform, false);
                        var vlg = infoC.AddComponent<VerticalLayoutGroup>();
                        vlg.spacing = 1f; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
                        vlg.childControlWidth = true; vlg.childControlHeight = false;
                        UIFactory.AddLayoutElement(infoC, flexibleWidth: 1);
                        
                        if (slot.isConstructing)
                        {
                            rowBg.color = new Color(0.10f, 0.08f, 0.04f, 1f);
                            
                            Text lbl = UIFactory.CreateText(infoC.transform, "Name", name.ToUpper(), 12, TextAnchor.MiddleLeft, UIFactory.EmpireGold);
                            lbl.fontStyle = FontStyle.Bold;
                            UIFactory.AddLayoutElement(lbl.gameObject, preferredHeight: 16);
                            
                            int totalTime = BuildingInfo.GetBuildTime(slot.type);
                            int elapsed = Mathf.Max(0, totalTime - slot.turnsToComplete);
                            Text statusTxt = UIFactory.CreateText(infoC.transform, "Status", 
                                $"🔨 Construction {elapsed}/{totalTime} — {slot.turnsToComplete}j restants", 
                                10, TextAnchor.MiddleLeft, UIFactory.MutedGold);
                            UIFactory.AddLayoutElement(statusTxt.gameObject, preferredHeight: 14);
                            
                            // Badge
                            Text badge = UIFactory.CreateText(row.transform, "Badge", "EN COURS", 9, TextAnchor.MiddleCenter, UIFactory.EmpireGold);
                            badge.fontStyle = FontStyle.Bold;
                            UIFactory.AddLayoutElement(badge.gameObject, preferredWidth: 65);
                        }
                        else
                        {
                            rowBg.color = new Color(0.08f, 0.08f, 0.09f, 1f);
                            
                            Text lbl = UIFactory.CreateText(infoC.transform, "Name", $"{name.ToUpper()} NV.{slot.level}", 12, TextAnchor.MiddleLeft, UIFactory.Porcelain);
                            lbl.fontStyle = FontStyle.Bold;
                            UIFactory.AddLayoutElement(lbl.gameObject, preferredHeight: 16);
                            
                            string effects = BuildingInfo.GetEffects(slot.type);
                            if (!string.IsNullOrEmpty(effects) && effects != "No effects")
                            {
                                Text effTxt = UIFactory.CreateText(infoC.transform, "Eff", $"↳ {effects}", 9, TextAnchor.MiddleLeft, new Color(0.45f, 0.45f, 0.42f));
                                UIFactory.AddLayoutElement(effTxt.gameObject, preferredHeight: 14);
                            }
                            
                            // Status badge
                            Text badge = UIFactory.CreateText(row.transform, "Badge", "✓ ÉTABLI", 9, TextAnchor.MiddleCenter, new Color(0.4f, 0.75f, 0.4f));
                            badge.fontStyle = FontStyle.Bold;
                            UIFactory.AddLayoutElement(badge.gameObject, preferredWidth: 65);
                        }
                    }
                    AddPremiumSpacer(content, 4);
                }
                
                // ═══════════════ EMPTY SLOTS ═══════════════
                if (emptySlots.Count > 0)
                {
                    CreateMilitarySectionHeader(content, "☐ CRÉNEAUX DISPONIBLES", $"{emptySlots.Count} emplacement(s)");
                    
                    foreach (int idx in emptySlots)
                    {
                        GameObject row = new GameObject($"Empty_{idx}");
                        row.transform.SetParent(content, false);
                        UIFactory.AddHorizontalLayout(row, 8f, new RectOffset(10, 10, 4, 4));
                        UIFactory.AddLayoutElement(row, preferredHeight: 36);
                        Image rowBg = row.AddComponent<Image>();
                        rowBg.color = new Color(0.07f, 0.07f, 0.08f, 1f);
                        
                        // Dotted icon
                        GameObject iconC = new GameObject("IconC");
                        iconC.transform.SetParent(row.transform, false);
                        UIFactory.AddLayoutElement(iconC, preferredWidth: 32, preferredHeight: 28);
                        Image iconBg = iconC.AddComponent<Image>();
                        iconBg.color = new Color(0.10f, 0.10f, 0.10f, 1f);
                        Text iconTxt = UIFactory.CreateText(iconC.transform, "Icon", "☐", 16, TextAnchor.MiddleCenter, UIFactory.SilverText);
                        RectTransform iconTxtRT = iconTxt.GetComponent<RectTransform>();
                        iconTxtRT.anchorMin = Vector2.zero; iconTxtRT.anchorMax = Vector2.one;
                        iconTxtRT.offsetMin = Vector2.zero; iconTxtRT.offsetMax = Vector2.zero;
                        
                        Text lbl = UIFactory.CreateText(row.transform, "Lbl", $"CRÉNEAU {idx+1}", 11, TextAnchor.MiddleLeft, UIFactory.SilverText);
                        UIFactory.AddLayoutElement(lbl.gameObject, flexibleWidth: 1);
                        
                        Button btn = UIFactory.CreateGoldButton(row.transform, "Build", "CONSTRUIRE", 10, () =>
                        {
                            if (city != null) ShowCityBuildPicker(city);
                        });
                        UIFactory.AddLayoutElement(btn.gameObject, preferredWidth: 90, preferredHeight: 28);
                    }
                }
            }
            else if (city != null)
            {
                // Fallback for CityData buildings
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
                    CreateMilitarySectionHeader(content, catName, "");
                    
                    foreach (var bld in categories[cat])
                    {
                        string bIcon = BuildingInfo.GetIcon(bld.buildingType);
                        string bName = BuildingInfo.GetName(bld.buildingType);
                        Color statusCol = bld.isConstructed ? new Color(0.4f, 0.75f, 0.4f) : 
                                          bld.isConstructing ? UIFactory.EmpireGold : UIFactory.SilverText;
                        string statusStr = bld.isConstructed ? "✓ ÉTABLI" : 
                                           bld.isConstructing ? $"🔨 {bld.constructionTurnsRemaining}j" : "Planifié";
                        CreateCompactDataRow(content, $"{bIcon} {bName} NV.{bld.level}", statusStr, statusCol, false);
                    }
                    AddPremiumSpacer(content, 4);
                }
                
                int emptyCount = city.maxBuildings - city.buildings.Count;
                if (emptyCount > 0)
                {
                    CreateMilitarySectionHeader(content, "☐ CRÉNEAUX VIDES", $"{emptyCount} emplacement(s)");
                    for (int s = 0; s < emptyCount; s++)
                    {
                        GameObject row = new GameObject("EmptySlot");
                        row.transform.SetParent(content, false);
                        UIFactory.AddHorizontalLayout(row, 8f, new RectOffset(10, 10, 3, 3));
                        UIFactory.AddLayoutElement(row, preferredHeight: 32);
                        Image rowBg = row.AddComponent<Image>();
                        rowBg.color = new Color(0.07f, 0.07f, 0.08f, 1f);
                        
                        Text lbl = UIFactory.CreateText(row.transform, "Lbl", $"☐ CRÉNEAU {s+1}", 11, TextAnchor.MiddleLeft, UIFactory.SilverText);
                        UIFactory.AddLayoutElement(lbl.gameObject, flexibleWidth: 1);
                        
                        Button btn = UIFactory.CreateGoldButton(row.transform, "Build", "CONSTRUIRE", 10, () => ShowCityBuildPicker(city));
                        UIFactory.AddLayoutElement(btn.gameObject, preferredWidth: 90, preferredHeight: 26);
                    }
                }
            }
            else
            {
                CreateCompactDataRow(content, "Aucune donnée", "—", UIFactory.SilverText, false);
            }
            
            // ═══════════════ PRODUCTION QUEUE ═══════════════
            if (city != null && city.productionQueue.Count > 0)
            {
                AddPremiumSpacer(content, 6);
                CreateMilitarySectionHeader(content, "📋 FILE DE PRODUCTION", "");
                foreach (var item in city.productionQueue)
                {
                    string n = item.itemType == ProductionItemType.Building ? "🏗 Bâtiment" :
                              item.itemType == ProductionItemType.Unit ? $"⚔ {item.unitType}" : "📦 Marchandises";
                    CreateCompactDataRow(content, n, $"⏱ {item.turnsRemaining}j", UIFactory.EmpireGold, false);
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
                
                UIFactory.CreateText(midCol.transform, "Cost", $"💰 {cost}g  ⏱ {time} jours", 9, TextAnchor.MiddleLeft, UIFactory.MutedGold);
                
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
            
            // Dark opaque background for the whole content area
            Image contentBg = content.gameObject.GetComponent<Image>();
            if (contentBg == null) contentBg = content.gameObject.AddComponent<Image>();
            contentBg.color = new Color(0.06f, 0.06f, 0.07f, 1f);
            
            // ═══════════════════════ MILICE ═══════════════════════
            CreateMilitarySectionHeader(content, "🗡 MILICE LOCALE", "Recrutement sans caserne");
            CreateMilitaryUnitRow(content, city, playerFac, "Milice", "🗡", UnitType.Militia, 
                40, 8, 3, true, "250 hommes • mousquets basiques", 250);
            CreateMilitaryUnitRow(content, city, playerFac, "Milice entraînée", "🗡", UnitType.TrainedMilitia, 
                60, 12, 5, true, "200 hommes • meilleur moral", 200);
            
            AddPremiumSpacer(content, 6);
            
            // ═══════════════════════ INFANTRY ═══════════════════════
            CreateMilitarySectionHeader(content, "🎖 INFANTERIE RÉGULIÈRE", 
                hasBarracks ? "Caserne opérationnelle" : "⚠ CASERNE REQUISE");
            
            CreateMilitaryUnitRow(content, city, playerFac, "Infanterie de Ligne", "🔫", UnitType.LineInfantry, 
                120, 25, 15, hasBarracks, "400 soldats • colonne de bataille", 400);
            CreateMilitaryUnitRow(content, city, playerFac, "Infanterie Légère", "🏹", UnitType.LightInfantry, 
                100, 20, 12, hasBarracks, "300 tirailleurs • rapides", 300);
            CreateMilitaryUnitRow(content, city, playerFac, "Fusilier", "🎯", UnitType.Fusilier, 
                150, 30, 18, hasBarracks, "350 soldats • tir discipliné", 350);
            CreateMilitaryUnitRow(content, city, playerFac, "Grenadier", "💣", UnitType.Grenadier, 
                200, 40, 25, hasBarracks, "250 élites • haute valeur", 250);
            CreateMilitaryUnitRow(content, city, playerFac, "Voltigeur", "⚡", UnitType.Voltigeur, 
                180, 35, 20, hasBarracks, "200 tireurs d'élite • agiles", 200);
            CreateMilitaryUnitRow(content, city, playerFac, "Chasseur", "🏹", UnitType.Chasseur, 
                220, 45, 28, hasBarracks, "180 chasseurs • fusils rayés", 180);
            
            AddPremiumSpacer(content, 6);
            
            // ═══════════════════════ CAVALRY ═══════════════════════
            CreateMilitarySectionHeader(content, "🏇 CAVALERIE", 
                hasStables ? "Écuries opérationnelles" : "⚠ ÉCURIES REQUISES");
            
            CreateMilitaryUnitRow(content, city, playerFac, "Cavalerie Milice", "🐴", UnitType.MilitiaCavalry, 
                80, 10, 8, hasStables, "120 cavaliers • sabres + chevaux", 120);
            CreateMilitaryUnitRow(content, city, playerFac, "Dragon", "🐴", UnitType.Dragoon, 
                140, 18, 14, hasStables, "150 dragons • infanterie montée", 150);
            CreateMilitaryUnitRow(content, city, playerFac, "Cavalerie", "🐴", UnitType.Cavalry, 
                160, 22, 18, hasStables, "140 cavaliers • charge de ligne", 140);
            CreateMilitaryUnitRow(content, city, playerFac, "Hussard", "⚡", UnitType.Hussar, 
                150, 20, 15, hasStables, "120 hussards • éclaireurs rapides", 120);
            CreateMilitaryUnitRow(content, city, playerFac, "Lancier", "🔱", UnitType.Lancer, 
                180, 25, 22, hasStables, "130 lanciers • charge dévastatrice", 130);
            CreateMilitaryUnitRow(content, city, playerFac, "Cuirassier", "🛡", UnitType.Cuirassier, 
                250, 35, 30, hasStables, "100 cuirassiers • cavalerie lourde", 100);
            
            AddPremiumSpacer(content, 6);
            
            // ═══════════════════════ ARTILLERY ═══════════════════════
            CreateMilitarySectionHeader(content, "💥 ARTILLERIE", 
                hasArmory ? "Armurerie opérationnelle" : "⚠ ARMURERIE REQUISE");
            
            CreateMilitaryUnitRow(content, city, playerFac, "Canon de garnison", "🏰", UnitType.GarrisonCannon, 
                100, 20, 15, hasArmory, "4 canons • défense fixe", 4);
            CreateMilitaryUnitRow(content, city, playerFac, "Artillerie de campagne", "🎯", UnitType.Artillery, 
                200, 35, 25, hasArmory, "8 canons • puissance de feu", 8);
            CreateMilitaryUnitRow(content, city, playerFac, "Artillerie à cheval", "🐴", UnitType.HorseArtillery, 
                250, 40, 30, hasArmory, "6 canons • mobile + rapide", 6);
            CreateMilitaryUnitRow(content, city, playerFac, "Obusier", "💥", UnitType.Howitzer, 
                300, 50, 35, hasArmory, "6 obusiers • tir indirect", 6);
            
            AddPremiumSpacer(content, 10);
            
            // ═══════════════════════ GARRISON ═══════════════════════
            CreateMilitarySectionHeader(content, "🏰 GARNISON & DÉFENSE", "");
            if (city != null)
            {
                // Garrison bar
                GameObject garRow = new GameObject("GarrisonRow");
                garRow.transform.SetParent(content, false);
                UIFactory.AddHorizontalLayout(garRow, 8f, new RectOffset(14, 14, 4, 4));
                UIFactory.AddLayoutElement(garRow, preferredHeight: 30);
                Image garBg = garRow.AddComponent<Image>();
                garBg.color = new Color(0.10f, 0.10f, 0.10f, 1f);
                
                Text garLabel = UIFactory.CreateText(garRow.transform, "GarLabel", "Force active", 12, 
                    TextAnchor.MiddleLeft, UIFactory.Parchment);
                UIFactory.AddLayoutElement(garLabel.gameObject, flexibleWidth: 1);
                
                float garRatio = (float)city.garrisonSize / Mathf.Max(1, city.maxGarrison);
                Text garVal = UIFactory.CreateText(garRow.transform, "GarVal", 
                    $"{city.garrisonSize}/{city.maxGarrison}", 12, TextAnchor.MiddleRight, 
                    garRatio > 0.7f ? Color.green : UIFactory.EmpireGold);
                garVal.fontStyle = FontStyle.Bold;
                UIFactory.AddLayoutElement(garVal.gameObject, preferredWidth: 80);
                
                // Fortification
                GameObject fortRow = new GameObject("FortRow");
                fortRow.transform.SetParent(content, false);
                UIFactory.AddHorizontalLayout(fortRow, 8f, new RectOffset(14, 14, 4, 4));
                UIFactory.AddLayoutElement(fortRow, preferredHeight: 30);
                Image fortBg = fortRow.AddComponent<Image>();
                fortBg.color = new Color(0.08f, 0.08f, 0.09f, 1f);
                
                int fortLevel = city.GetFortificationLevel();
                string fortStars = new string('★', fortLevel) + new string('☆', 5 - fortLevel);
                
                Text fortLabel = UIFactory.CreateText(fortRow.transform, "FortLabel", "Fortification", 12, 
                    TextAnchor.MiddleLeft, UIFactory.Parchment);
                UIFactory.AddLayoutElement(fortLabel.gameObject, flexibleWidth: 1);
                
                Text fortVal = UIFactory.CreateText(fortRow.transform, "FortVal", fortStars, 14, 
                    TextAnchor.MiddleRight, UIFactory.EmpireGold);
                UIFactory.AddLayoutElement(fortVal.gameObject, preferredWidth: 100);
            }
            
            AddPremiumSpacer(content, 10);
            
            // ═══════════════════════ QUEUE ═══════════════════════
            CreateMilitarySectionHeader(content, "📋 FILE DE RECRUTEMENT", "");
            if (city != null && city.productionQueue.Count > 0)
            {
                int qIdx = 0;
                foreach (var item in city.productionQueue)
                {
                    if (item.itemType == ProductionItemType.Unit)
                    {
                        GameObject qRow = new GameObject($"Queue_{qIdx}");
                        qRow.transform.SetParent(content, false);
                        UIFactory.AddHorizontalLayout(qRow, 8f, new RectOffset(14, 14, 3, 3));
                        UIFactory.AddLayoutElement(qRow, preferredHeight: 28);
                        Image qBg = qRow.AddComponent<Image>();
                        qBg.color = new Color(0.12f, 0.10f, 0.06f, 1f);
                        
                        string unitIcon = GetUnitTypeIcon(item.unitType);
                        Text qName = UIFactory.CreateText(qRow.transform, "QueueName", 
                            $"  {unitIcon} {item.unitType}", 12, TextAnchor.MiddleLeft, UIFactory.Parchment);
                        qName.fontStyle = FontStyle.Bold;
                        UIFactory.AddLayoutElement(qName.gameObject, flexibleWidth: 1);
                        
                        Text qTurns = UIFactory.CreateText(qRow.transform, "QueueTurns", 
                            $"⏱ {item.turnsRemaining} jour(s)", 11, TextAnchor.MiddleRight, UIFactory.EmpireGold);
                        UIFactory.AddLayoutElement(qTurns.gameObject, preferredWidth: 90);
                        qIdx++;
                    }
                }
            }
            else
            {
                GameObject emptyRow = new GameObject("EmptyQueue");
                emptyRow.transform.SetParent(content, false);
                UIFactory.AddLayoutElement(emptyRow, preferredHeight: 26);
                Image emptyBg = emptyRow.AddComponent<Image>();
                emptyBg.color = new Color(0.08f, 0.08f, 0.08f, 1f);
                Text emptyLabel = UIFactory.CreateText(emptyRow.transform, "EmptyLabel", 
                    "   Aucun recrutement en cours", 11, TextAnchor.MiddleLeft, 
                    new Color(0.4f, 0.4f, 0.4f));
                emptyLabel.fontStyle = FontStyle.Italic;
                RectTransform emptyLabelRT = emptyLabel.GetComponent<RectTransform>();
                emptyLabelRT.anchorMin = Vector2.zero;
                emptyLabelRT.anchorMax = Vector2.one;
                emptyLabelRT.offsetMin = new Vector2(14, 0);
                emptyLabelRT.offsetMax = Vector2.zero;
            }
        }
        
        /// <summary>Create a premium section header for the military tab</summary>
        private void CreateMilitarySectionHeader(Transform parent, string title, string subtitle)
        {
            GameObject header = new GameObject($"Sec_{title}");
            header.transform.SetParent(parent, false);
            UIFactory.AddLayoutElement(header, preferredHeight: subtitle.Length > 0 ? 48 : 40);
            
            Image bg = header.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.08f, 0.05f, 1f);
            
            // Gold left accent bar
            GameObject accent = new GameObject("Accent");
            accent.transform.SetParent(header.transform, false);
            RectTransform accentRT = accent.AddComponent<RectTransform>();
            accentRT.anchorMin = new Vector2(0, 0);
            accentRT.anchorMax = new Vector2(0, 1);
            accentRT.offsetMin = Vector2.zero;
            accentRT.offsetMax = new Vector2(6, 0);
            Image accentImg = accent.AddComponent<Image>();
            accentImg.color = UIFactory.EmpireGold;
            
            // Title text
            Text titleText = UIFactory.CreateText(header.transform, "Title", title, 17, 
                TextAnchor.MiddleLeft, UIFactory.EmpireGold);
            titleText.fontStyle = FontStyle.Bold;
            RectTransform titleRT = titleText.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0, subtitle.Length > 0 ? 0.45f : 0);
            titleRT.anchorMax = Vector2.one;
            titleRT.offsetMin = new Vector2(16, 0);
            titleRT.offsetMax = new Vector2(-10, 0);
            
            // Subtitle (status)
            if (!string.IsNullOrEmpty(subtitle))
            {
                bool isWarning = subtitle.Contains("⚠");
                Text subText = UIFactory.CreateText(header.transform, "Sub", subtitle, 13, 
                    TextAnchor.MiddleLeft, isWarning ? new Color(0.85f, 0.3f, 0.2f) : new Color(0.4f, 0.55f, 0.35f));
                subText.fontStyle = isWarning ? FontStyle.Bold : FontStyle.Italic;
                RectTransform subRT = subText.GetComponent<RectTransform>();
                subRT.anchorMin = Vector2.zero;
                subRT.anchorMax = new Vector2(1, 0.45f);
                subRT.offsetMin = new Vector2(16, 0);
                subRT.offsetMax = new Vector2(-10, 0);
            }
        }
        
        /// <summary>Premium spacer with subtle line</summary>
        private void AddPremiumSpacer(Transform parent, float height)
        {
            GameObject spacer = new GameObject("Spacer");
            spacer.transform.SetParent(parent, false);
            UIFactory.AddLayoutElement(spacer, preferredHeight: height);
        }
        
        /// <summary>
        /// Create a premium military unit row with icon, stats, cost, and recruit button.
        /// Fixed: passes skipBuildingCheck when meetsRequirements is true (for militia).
        /// </summary>
        private void CreateMilitaryUnitRow(Transform parent, CityData city, FactionData faction,
            string unitName, string icon, UnitType unitType, int goldCost, int ironCost, int turns, 
            bool meetsRequirements, string unitInfo, int unitSize)
        {
            GameObject row = new GameObject($"U_{unitName}");
            row.transform.SetParent(parent, false);
            UIFactory.AddLayoutElement(row, preferredHeight: 52);
            
            // Row background — alternating dark tones
            Image bg = row.AddComponent<Image>();
            bg.color = meetsRequirements 
                ? new Color(0.08f, 0.08f, 0.09f, 1f)    // Dark but opaque
                : new Color(0.08f, 0.05f, 0.05f, 1f);   // Slight red tint = locked
            
            // Horizontal layout
            UIFactory.AddHorizontalLayout(row, 8f, new RectOffset(10, 10, 4, 4));
            
            // === ICON ===
            GameObject iconContainer = new GameObject("IconContainer");
            iconContainer.transform.SetParent(row.transform, false);
            RectTransform iconContainerRT = iconContainer.AddComponent<RectTransform>();
            UIFactory.AddLayoutElement(iconContainer, preferredWidth: 36, preferredHeight: 36);
            Image iconBg = iconContainer.AddComponent<Image>();
            Color unitColor = GetUnitTypeColor(unitType);
            iconBg.color = new Color(unitColor.r * 0.25f, unitColor.g * 0.25f, unitColor.b * 0.25f, 1f);
            
            Text iconText = UIFactory.CreateText(iconContainer.transform, "Icon", icon, 22, 
                TextAnchor.MiddleCenter, meetsRequirements ? unitColor : new Color(0.3f, 0.3f, 0.3f));
            RectTransform iconTextRT = iconText.GetComponent<RectTransform>();
            iconTextRT.anchorMin = Vector2.zero;
            iconTextRT.anchorMax = Vector2.one;
            iconTextRT.offsetMin = Vector2.zero;
            iconTextRT.offsetMax = Vector2.zero;
            
            // === INFO COLUMN ===
            GameObject infoCol = new GameObject("InfoCol");
            infoCol.transform.SetParent(row.transform, false);
            var vlg = infoCol.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 1f;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            UIFactory.AddLayoutElement(infoCol, flexibleWidth: 1);
            
            // Unit name — large and bold
            Text nameLabel = UIFactory.CreateText(infoCol.transform, "Name", unitName.ToUpper(), 12, 
                TextAnchor.MiddleLeft, meetsRequirements ? UIFactory.Porcelain : new Color(0.45f, 0.35f, 0.35f));
            nameLabel.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(nameLabel.gameObject, preferredHeight: 16);
            
            // Unit info
            Text infoLabel = UIFactory.CreateText(infoCol.transform, "Info", unitInfo, 10, 
                TextAnchor.MiddleLeft, new Color(0.45f, 0.45f, 0.42f));
            UIFactory.AddLayoutElement(infoLabel.gameObject, preferredHeight: 14);
            
            // Cost line — gold icons
            string costStr = $"💰 {goldCost}  ⛏ {ironCost}  ⏱ {turns}j";
            Text costLabel = UIFactory.CreateText(infoCol.transform, "Cost", costStr, 10, 
                TextAnchor.MiddleLeft, UIFactory.MutedGold);
            UIFactory.AddLayoutElement(costLabel.gameObject, preferredHeight: 14);
            
            // === RECRUIT BUTTON ===
            if (meetsRequirements && city != null)
            {
                bool canAfford = faction != null && faction.gold >= goldCost && faction.iron >= ironCost;
                
                // Capture for closure
                UnitType capturedType = unitType;
                int capturedCost = goldCost;
                bool capturedSkipCheck = meetsRequirements;
                int capturedTurns = turns;
                CityData capturedCity = city;
                
                Button btn = UIFactory.CreateGoldButton(row.transform, "Recruit", 
                    canAfford ? "RECRUTER" : "FONDS ✗", 11, () =>
                {
                    Debug.Log($"[CampaignUI] RECRUIT CLICKED: {capturedType}, cost={capturedCost}, skip={capturedSkipCheck}, turns={capturedTurns}, city={capturedCity?.cityName}");
                    if (capturedCity.StartUnitProduction(capturedType, capturedCost, capturedSkipCheck, capturedTurns))
                    {
                        cityPanelDirty = true;
                        RefreshCityPanel();
                    }
                    else
                    {
                        Debug.LogWarning($"[CampaignUI] StartUnitProduction returned FALSE!");
                    }
                });
                UIFactory.AddLayoutElement(btn.gameObject, preferredWidth: 90, preferredHeight: 34);
                btn.interactable = canAfford;
                
                if (!canAfford)
                {
                    btn.GetComponent<Image>().color = new Color(0.15f, 0.12f, 0.12f, 1f);
                }
            }
            else
            {
                // Locked indicator
                GameObject lockContainer = new GameObject("Lock");
                lockContainer.transform.SetParent(row.transform, false);
                UIFactory.AddLayoutElement(lockContainer, preferredWidth: 60, preferredHeight: 34);
                
                Text lockText = UIFactory.CreateText(lockContainer.transform, "LockIcon", "🔒", 18, 
                    TextAnchor.MiddleCenter, new Color(0.4f, 0.25f, 0.25f));
                RectTransform lockRT = lockText.GetComponent<RectTransform>();
                lockRT.anchorMin = Vector2.zero;
                lockRT.anchorMax = Vector2.one;
                lockRT.offsetMin = Vector2.zero;
                lockRT.offsetMax = Vector2.zero;
            }
        }
        
        // ─── INDUSTRY TAB ─────────────────────────────────────────
        private void RefreshIndustryTab(CityData city, ProvinceData prov)
        {
            Transform content = GetTabContent(cityIndustryTab);
            if (content == null) return;
            ClearContent(content);
            
            // Force opaque background
            Image contentBg = content.gameObject.GetComponent<Image>();
            if (contentBg == null) contentBg = content.gameObject.AddComponent<Image>();
            contentBg.color = new Color(0.06f, 0.06f, 0.07f, 1f);
            
            FactionData playerFac = CampaignManager.Instance?.GetPlayerFaction();
            
            // ═══════════════ PRODUCTION SUMMARY ═══════════════
            CreateMilitarySectionHeader(content, "📊 PRODUCTION TOTALE", "");
            
            float totalGold = prov.goldIncome, totalFood = prov.foodProduction, totalIron = prov.ironProduction;
            float indGold = 0, indFood = 0, indIron = 0;
            
            if (city != null)
                foreach (var ind in city.industries)
                {
                    indGold += ind.GetGoldOutput();
                    indFood += ind.GetFoodOutput();
                    indIron += ind.GetIronOutput();
                }
            totalGold += indGold; totalFood += indFood; totalIron += indIron;
            
            // Resource summary grid
            GameObject resGrid = new GameObject("ResGrid");
            resGrid.transform.SetParent(content, false);
            UIFactory.AddHorizontalLayout(resGrid, 4f, new RectOffset(8, 8, 4, 4));
            UIFactory.AddLayoutElement(resGrid, preferredHeight: 50);
            Image resGridBg = resGrid.AddComponent<Image>();
            resGridBg.color = new Color(0.08f, 0.08f, 0.09f, 1f);
            
            CreateResourceCard(resGrid.transform, "💰", "Or", $"+{totalGold:F0}", UIFactory.EmpireGold);
            CreateResourceCard(resGrid.transform, "🌾", "Nourriture", $"+{totalFood:F0}", new Color(0.5f, 0.8f, 0.3f));
            CreateResourceCard(resGrid.transform, "⛏", "Fer", $"+{totalIron:F0}", UIFactory.SilverText);
            
            // Industry contribution
            if (indGold > 0 || indFood > 0 || indIron > 0)
            {
                GameObject contribRow = new GameObject("Contrib");
                contribRow.transform.SetParent(content, false);
                UIFactory.AddLayoutElement(contribRow, preferredHeight: 20);
                Image contribBg = contribRow.AddComponent<Image>();
                contribBg.color = new Color(0.07f, 0.07f, 0.08f, 1f);
                string contribStr = "  🏭 Industrie:";
                if (indGold > 0) contribStr += $" +{indGold:F0}💰";
                if (indFood > 0) contribStr += $" +{indFood:F0}🌾";
                if (indIron > 0) contribStr += $" +{indIron:F0}⛏";
                Text contribText = UIFactory.CreateText(contribRow.transform, "Contrib", contribStr, 9, 
                    TextAnchor.MiddleLeft, new Color(0.45f, 0.45f, 0.42f));
                RectTransform contribRT = contribText.GetComponent<RectTransform>();
                contribRT.anchorMin = Vector2.zero; contribRT.anchorMax = Vector2.one;
                contribRT.offsetMin = new Vector2(8, 0); contribRT.offsetMax = Vector2.zero;
            }
            
            AddPremiumSpacer(content, 6);
            
            // ═══════════════ ACTIVE INDUSTRIES ═══════════════
            CreateMilitarySectionHeader(content, "🏭 INDUSTRIES ACTIVES", 
                city != null ? $"{city.industries.Count} industrie(s)" : "");
            
            if (city != null && city.industries.Count > 0)
            {
                foreach (var ind in city.industries)
                {
                    string output = "";
                    if (ind.GetGoldOutput() > 0) output += $"+{ind.GetGoldOutput():F0}💰 ";
                    if (ind.GetFoodOutput() > 0) output += $"+{ind.GetFoodOutput():F0}🌾 ";
                    if (ind.GetIronOutput() > 0) output += $"+{ind.GetIronOutput():F0}⛏ ";
                    
                    string indName = GetIndustryName(ind.industryType);
                    string indIcon = GetIndustryIcon(ind.industryType);
                    
                    GameObject row = new GameObject($"I_{ind.industryType}");
                    row.transform.SetParent(content, false);
                    UIFactory.AddHorizontalLayout(row, 8f, new RectOffset(10, 10, 4, 4));
                    UIFactory.AddLayoutElement(row, preferredHeight: 48);
                    Image bg = row.AddComponent<Image>();
                    bg.color = new Color(0.08f, 0.08f, 0.09f, 1f);
                    
                    // Icon
                    GameObject iconC = new GameObject("IconC");
                    iconC.transform.SetParent(row.transform, false);
                    UIFactory.AddLayoutElement(iconC, preferredWidth: 32, preferredHeight: 32);
                    Image iconBg = iconC.AddComponent<Image>();
                    iconBg.color = new Color(0.12f, 0.10f, 0.08f, 1f);
                    Text iconTxt = UIFactory.CreateText(iconC.transform, "Icon", indIcon, 18, TextAnchor.MiddleCenter, UIFactory.EmpireGold);
                    RectTransform iconTxtRT = iconTxt.GetComponent<RectTransform>();
                    iconTxtRT.anchorMin = Vector2.zero; iconTxtRT.anchorMax = Vector2.one;
                    iconTxtRT.offsetMin = Vector2.zero; iconTxtRT.offsetMax = Vector2.zero;
                    
                    // Info
                    GameObject infoC = new GameObject("Info");
                    infoC.transform.SetParent(row.transform, false);
                    var vlg = infoC.AddComponent<VerticalLayoutGroup>();
                    vlg.spacing = 1f; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
                    vlg.childControlWidth = true; vlg.childControlHeight = false;
                    UIFactory.AddLayoutElement(infoC, flexibleWidth: 1);
                    
                    Text lbl = UIFactory.CreateText(infoC.transform, "Name", $"{indName.ToUpper()} NV.{ind.level}", 12, TextAnchor.MiddleLeft, UIFactory.Porcelain);
                    lbl.fontStyle = FontStyle.Bold;
                    UIFactory.AddLayoutElement(lbl.gameObject, preferredHeight: 16);
                    
                    Text outTxt = UIFactory.CreateText(infoC.transform, "Out", output.Trim(), 10, TextAnchor.MiddleLeft, UIFactory.MutedGold);
                    UIFactory.AddLayoutElement(outTxt.gameObject, preferredHeight: 14);
                    
                    // Upgrade button
                    int upgCost = 150 * ind.level;
                    int upgIron = 50 * ind.level;
                    bool canUpgrade = playerFac != null && playerFac.gold >= upgCost && playerFac.iron >= upgIron;
                    IndustrySlot ci = ind;
                    int capUpgCost = upgCost, capUpgIron = upgIron;
                    
                    GameObject btnCol = new GameObject("BtnCol");
                    btnCol.transform.SetParent(row.transform, false);
                    var bvlg = btnCol.AddComponent<VerticalLayoutGroup>();
                    bvlg.spacing = 1f; bvlg.childForceExpandWidth = true; bvlg.childForceExpandHeight = false;
                    bvlg.childAlignment = TextAnchor.MiddleCenter;
                    UIFactory.AddLayoutElement(btnCol, preferredWidth: 80);
                    
                    Button btn = UIFactory.CreateGoldButton(btnCol.transform, "Up", canUpgrade ? $"↑ NV.{ind.level + 1}" : "FONDS ✗", 9, () =>
                    { 
                        if (canUpgrade && playerFac != null) 
                        { 
                            playerFac.gold -= capUpgCost; playerFac.iron -= capUpgIron;
                            ci.level++; cityPanelDirty = true; RefreshCityPanel(); 
                        }
                    });
                    UIFactory.AddLayoutElement(btn.gameObject, preferredHeight: 22);
                    btn.interactable = canUpgrade;
                    
                    Text costLbl = UIFactory.CreateText(btnCol.transform, "Cost", $"{upgCost}g {upgIron}⛏", 8, TextAnchor.MiddleCenter, UIFactory.SilverText);
                    UIFactory.AddLayoutElement(costLbl.gameObject, preferredHeight: 12);
                }
            }
            else
            {
                CreateCompactDataRow(content, "Aucune industrie", "Agriculture de subsistance", UIFactory.SilverText, false);
            }
            
            AddPremiumSpacer(content, 6);
            
            // ═══════════════ EQUIPMENT ═══════════════
            CreateMilitarySectionHeader(content, "⚔ ÉQUIPEMENT MILITAIRE", "");
            if (playerFac != null)
            {
                var stockpile = playerFac.equipment;
                if (stockpile != null)
                {
                    AddEquipmentRow(content, "🔫 Mousquets", EquipmentType.Muskets, stockpile);
                    AddEquipmentRow(content, "🗡 Baïonnettes", EquipmentType.Bayonets, stockpile);
                    AddEquipmentRow(content, "⚔ Sabres", EquipmentType.Sabres, stockpile);
                    AddEquipmentRow(content, "💣 Canons légers", EquipmentType.CannonsLight, stockpile);
                    AddEquipmentRow(content, "💥 Canons lourds", EquipmentType.CannonsHeavy, stockpile);
                    AddEquipmentRow(content, "🐴 Chevaux", EquipmentType.Horses, stockpile);
                    AddEquipmentRow(content, "👔 Uniformes", EquipmentType.Uniforms, stockpile);
                }
            }
            
            AddPremiumSpacer(content, 6);
            
            // ═══════════════ ADD INDUSTRY ═══════════════
            CreateMilitarySectionHeader(content, "➕ DÉVELOPPER L'INDUSTRIE", "");
            
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
                
                bool terrainOk = true;
                if (terrain == "Côtier" && prov.terrainType != ProvinceTerrainType.Coastal) terrainOk = false;
                if (terrain == "Collines/Montagnes" && prov.terrainType != ProvinceTerrainType.Hills && prov.terrainType != ProvinceTerrainType.Mountains) terrainOk = false;
                if (terrain == "Forêts/Plaines" && prov.terrainType != ProvinceTerrainType.Forest && prov.terrainType != ProvinceTerrainType.Plains) terrainOk = false;
                
                bool canAfford = playerFac != null && playerFac.gold >= goldCost && playerFac.iron >= ironCost;
                
                GameObject row = new GameObject($"Add_{type}");
                row.transform.SetParent(content, false);
                UIFactory.AddHorizontalLayout(row, 6f, new RectOffset(10, 10, 3, 3));
                UIFactory.AddLayoutElement(row, preferredHeight: 32);
                Image rbg = row.AddComponent<Image>();
                rbg.color = terrainOk 
                    ? new Color(0.08f, 0.08f, 0.09f, 1f) 
                    : new Color(0.08f, 0.05f, 0.05f, 1f);
                
                Text lbl = UIFactory.CreateText(row.transform, "Lbl", $"{icon} {name}", 11, 
                    TextAnchor.MiddleLeft, terrainOk ? UIFactory.Porcelain : new Color(0.4f, 0.35f, 0.35f));
                lbl.fontStyle = FontStyle.Bold;
                UIFactory.AddLayoutElement(lbl.gameObject, flexibleWidth: 1);
                
                Text costTxt = UIFactory.CreateText(row.transform, "Cost", $"💰{goldCost} ⛏{ironCost}", 9, 
                    TextAnchor.MiddleCenter, UIFactory.MutedGold);
                UIFactory.AddLayoutElement(costTxt.gameObject, preferredWidth: 75);
                
                if (!terrainOk)
                {
                    Text req = UIFactory.CreateText(row.transform, "Req", terrain, 9, TextAnchor.MiddleCenter, new Color(0.6f, 0.3f, 0.25f));
                    UIFactory.AddLayoutElement(req.gameObject, preferredWidth: 80);
                }
                else
                {
                    IndustryType ct = type; int gc = goldCost; int ic = ironCost;
                    Button ab = UIFactory.CreateGoldButton(row.transform, "Add", canAfford ? "CONSTRUIRE" : "✗", 9, () =>
                    {
                        if (city != null && canAfford && playerFac != null)
                        {
                            playerFac.gold -= gc; playerFac.iron -= ic;
                            city.AddIndustry(ct); cityPanelDirty = true; RefreshCityPanel();
                        }
                    });
                    UIFactory.AddLayoutElement(ab.gameObject, preferredWidth: 80, preferredHeight: 24);
                    ab.interactable = canAfford;
                }
            }
        }
        
        /// <summary>Equipment stockpile row with color-coded value</summary>
        private void AddEquipmentRow(Transform parent, string name, EquipmentType type, EquipmentStockpile stockpile)
        {
            float stock = stockpile.Get(type);
            Color col = stock > 50 ? new Color(0.4f, 0.75f, 0.35f) : stock > 10 ? UIFactory.EmpireGold : new Color(0.85f, 0.3f, 0.2f);
            CreateCompactDataRow(parent, name, $"{stock:F0} en stock", col, false);
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
            Text t = UIFactory.CreateText(parent, $"Sec_{title}", title, 16, TextAnchor.MiddleLeft, new Color(0.7f, 0.6f, 0.35f));
            t.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(t.gameObject, 28);
        }
        
        private void AddInfoRow(Transform parent, string text, Color color, int fontSize = 12, bool bold = false)
        {
            Text t = UIFactory.CreateText(parent, "Info", text, fontSize, TextAnchor.MiddleLeft, color);
            if (bold) t.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(t.gameObject, fontSize + 12);
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
        
        /// <summary>Find the city in a given province (for recruitment from army panel).</summary>
        private CityData FindCityInProvince(string provinceId)
        {
            return CampaignManager.Instance?.GetCityForProvince(provinceId);
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
