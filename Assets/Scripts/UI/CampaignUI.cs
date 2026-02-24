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
            BuildDiplomacyPanel();
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
                    if (diplomacyPanel != null)
                    {
                        diplomacyPanel.SetActive(true);
                        RefreshDiplomacy();
                    }
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
            RectTransform panel = UIFactory.CreateOrnatePanel(canvas.transform, "BuildMenuPanel",
                new Color(0.07f, 0.05f, 0.04f, 0.97f));
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(320, 300);
            buildMenuPanel = panel.gameObject;

            Transform inner = panel.Find("Inner");
            UIFactory.AddVerticalLayout(inner.gameObject, 3f, new RectOffset(12, 12, 10, 10));

            RectTransform titleBanner = UIFactory.CreateBannerHeader(inner, "TitleBanner", "BUILD", 18);
            UIFactory.AddLayoutElement(titleBanner.gameObject, 36);

            var (scroll, content) = UIFactory.CreateScrollView(inner, "BuildScroll");
            UIFactory.AddLayoutElement(scroll.gameObject, 180, flexibleWidth: 1);
            buildMenuContainer = content;

            Button btnCancel = UIFactory.CreateGoldButton(inner, "BtnCancel", "Cancel", 13, () => buildMenuPanel.SetActive(false));
            UIFactory.AddLayoutElement(btnCancel.gameObject, 28);

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
                Button btn = UIFactory.CreateGoldButton(buildMenuContainer, $"Btn_{type}", label, 12, () =>
                {
                    CampaignManager.Instance.BuildInProvince(selectedProvinceId, buildSlotIndex, capturedType);
                    buildMenuPanel.SetActive(false);
                });
                UIFactory.AddLayoutElement(btn.gameObject, 28);
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
            // Main panel — right side, large
            RectTransform panel = UIFactory.CreateOrnatePanel(canvas.transform, "CityPanel",
                new Color(0.10f, 0.11f, 0.10f, 0.96f));
            panel.anchorMin = new Vector2(0.60f, 0.06f);
            panel.anchorMax = new Vector2(0.99f, 0.94f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            cityPanel = panel.gameObject;
    
            Transform inner = panel.transform.Find("Inner");
            UIFactory.AddVerticalLayout(inner.gameObject, 3f, new RectOffset(12, 12, 8, 8));
            
            // === HEADER ===
            GameObject headerRow = new GameObject("HeaderRow");
            headerRow.transform.SetParent(inner, false);
            headerRow.AddComponent<RectTransform>();
            UIFactory.AddHorizontalLayout(headerRow, 6f);
            UIFactory.AddLayoutElement(headerRow, 32);
            
            cityNameText = UIFactory.CreateText(headerRow.transform, "CityName", "City", 20,
                TextAnchor.MiddleLeft, UIFactory.GoldAccent);
            cityNameText.fontStyle = FontStyle.Bold;
            Shadow ns = cityNameText.gameObject.AddComponent<Shadow>();
            ns.effectColor = new Color(0, 0, 0, 0.8f);
            ns.effectDistance = new Vector2(1, -1);
            UIFactory.AddLayoutElement(cityNameText.gameObject, flexibleWidth: 1);
            
            cityLevelText = UIFactory.CreateText(headerRow.transform, "CityLevel", "", 14,
                TextAnchor.MiddleRight, UIFactory.ParchmentBeige);
            UIFactory.AddLayoutElement(cityLevelText.gameObject, 120);
            
            Button closeBtn = UIFactory.CreateGoldButton(headerRow.transform, "CloseBtn", "✕", 16,
                () => CloseCityPanel());
            UIFactory.AddLayoutElement(closeBtn.gameObject, 32, 28);
            
            RectTransform sep = UIFactory.CreateOrnamentalSeparator(inner);
            UIFactory.AddLayoutElement(sep.gameObject, 12);
            
            // === TAB BAR ===
            GameObject tabBar = new GameObject("TabBar");
            tabBar.transform.SetParent(inner, false);
            tabBar.AddComponent<RectTransform>();
            UIFactory.AddHorizontalLayout(tabBar, 3f);
            UIFactory.AddLayoutElement(tabBar, 30);
            
            string[] tabNames = { "Overview", "Buildings", "Military", "Industry" };
            cityTabButtons = new Button[4];
            for (int t = 0; t < 4; t++)
            {
                int tabIdx = t;
                cityTabButtons[t] = UIFactory.CreateGoldButton(tabBar.transform, $"Tab_{tabNames[t]}",
                    tabNames[t], 12, () => { activeCityTab = tabIdx; cityPanelDirty = true; });
                UIFactory.AddLayoutElement(cityTabButtons[t].gameObject, 28, flexibleWidth: 1);
            }
            
            RectTransform sep2 = UIFactory.CreateOrnamentalSeparator(inner);
            UIFactory.AddLayoutElement(sep2.gameObject, 8);
            
            // === TAB CONTENT CONTAINERS ===
            cityOverviewTab = CreateTabContainer(inner, "OverviewTab");
            cityBuildingsTab = CreateTabContainer(inner, "BuildingsTab");
            cityMilitaryTab = CreateTabContainer(inner, "MilitaryTab");
            cityIndustryTab = CreateTabContainer(inner, "IndustryTab");
            
            cityPanel.SetActive(false);
        }
        
        private GameObject CreateTabContainer(Transform parent, string name)
        {
            var (scroll, content) = UIFactory.CreateScrollView(parent, name);
            UIFactory.AddLayoutElement(scroll.gameObject, 100, flexibleWidth: 1, flexibleHeight: 1);
            return scroll.gameObject;
        }
        
        private Transform GetTabContent(GameObject tab)
        {
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
            cityNameText.text = city != null ? city.cityName : prov.provinceName;
            cityLevelText.text = city != null 
                ? $"Lv.{city.cityLevel} — {city.CityLevelName}" 
                : $"{prov.owner}";
            
            // Highlight active tab
            Color activeCol = new Color(0.85f, 0.7f, 0.3f);
            Color inactiveCol = new Color(0.5f, 0.45f, 0.35f);
            for (int t = 0; t < 4; t++)
            {
                var colors = cityTabButtons[t].colors;
                colors.normalColor = t == activeCityTab ? activeCol : inactiveCol;
                cityTabButtons[t].colors = colors;
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
            
            Color gold = UIFactory.GoldAccent;
            Color beige = UIFactory.ParchmentBeige;
            Color green = new Color(0.5f, 0.8f, 0.3f);
            Color grey = new Color(0.6f, 0.6f, 0.65f);
            Color red = new Color(0.8f, 0.3f, 0.2f);
            Color purple = new Color(0.7f, 0.5f, 0.9f);
            Color blue = new Color(0.4f, 0.7f, 0.9f);
            
            // CITY INFO
            AddInfoRow(content, city != null ? city.cityName : prov.provinceName, gold, 16, true);
            AddInfoRow(content, $"Owner: {prov.owner}", beige);
            if (city != null)
                AddInfoRow(content, $"City Level: {city.cityLevel} — {city.CityLevelName}", gold);
            AddSpacer(content, 6);
            
            // POPULATION
            AddSectionLabel(content, "POPULATION");
            if (city != null)
            {
                AddInfoRow(content, $"  Total: {city.population:N0} / {city.maxPopulation:N0}", beige);
                AddInfoRow(content, $"  Workers: {city.populationData.workers:N0} ({city.populationData.WorkerRatio:P0})", beige);
                AddInfoRow(content, $"  Bourgeois: {city.populationData.bourgeois:N0} ({city.populationData.BourgeoisRatio:P0})", gold);
                AddInfoRow(content, $"  Nobles: {city.populationData.nobles:N0} ({city.populationData.NobleRatio:P0})", purple);
                if (city.NextLevelPopulation > 0)
                {
                    float prog = Mathf.Clamp01((float)city.population / city.NextLevelPopulation);
                    AddInfoRow(content, $"  Next: {CityLevelThresholds.GetLevelName(city.cityLevel + 1)} at {city.NextLevelPopulation:N0} ({prog:P0})", grey);
                }
            }
            else
            {
                AddInfoRow(content, $"  Population: {prov.population:N0}", beige);
            }
            AddSpacer(content, 6);
            
            // ECONOMY
            AddSectionLabel(content, "ECONOMY");
            AddInfoRow(content, $"  Gold income: +{prov.goldIncome:F0}/turn", gold);
            AddInfoRow(content, $"  Food: +{prov.foodProduction:F0}/turn", green);
            AddInfoRow(content, $"  Iron: +{prov.ironProduction:F0}/turn", grey);
            if (city != null)
            {
                AddInfoRow(content, $"  Bourgeois bonus: +{city.BourgeoisGoldBonus:F1}g/turn", gold);
                AddInfoRow(content, $"  Stored food: {city.storedFood:F0}", green);
                AddInfoRow(content, $"  Stored iron: {city.storedIron:F0}", grey);
                AddInfoRow(content, $"  Stored goods: {city.storedGoods:F0}", beige);
            }
            AddSpacer(content, 6);
            
            // DEFENSE
            AddSectionLabel(content, "DEFENSE & STABILITY");
            if (city != null)
            {
                Color oCol = city.publicOrder > 70 ? green : city.publicOrder > 40 ? gold : red;
                AddInfoRow(content, $"  Public Order: {city.publicOrder:F0}/100", oCol);
                AddInfoRow(content, $"  Fortification: Lv.{city.GetFortificationLevel()}", beige);
                AddInfoRow(content, $"  Garrison: {city.garrisonSize}/{city.maxGarrison}", beige);
                AddInfoRow(content, $"  Research bonus: +{city.NobleResearchBonus:F1}", blue);
            }
            else
            {
                AddInfoRow(content, "  No city data available", Color.gray);
            }
        }
        
        // ─── BUILDINGS TAB ────────────────────────────────────────
        private void RefreshBuildingsTab(CityData city, ProvinceData prov)
        {
            Transform content = GetTabContent(cityBuildingsTab);
            if (content == null) return;
            ClearContent(content);
            
            AddInfoRow(content, city != null ? city.cityName : prov.provinceName, UIFactory.GoldAccent, 16, true);
            AddSpacer(content, 4);
            
            // Use ProvinceData building slots — grouped by category
            if (prov.buildings != null && prov.buildings.Length > 0)
            {
                int built = 0, building = 0, empty = 0;
                foreach (var b in prov.buildings)
                    if (b.type == BuildingType.Empty) empty++; 
                    else if (b.isConstructing) building++; 
                    else built++;
                
                AddInfoRow(content, $"Slots: {built} built, {building} constructing, {empty} empty ({prov.buildings.Length} total)", UIFactory.ParchmentBeige);
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
                    
                    // Category header
                    string catName = cat switch
                    {
                        BuildingCategory.Military => "⚔ MILITARY",
                        BuildingCategory.Economy => "💰 ECONOMY",
                        BuildingCategory.Religion => "⛪ RELIGION",
                        BuildingCategory.Academic => "🏛 ACADEMIC",
                        _ => "🔧 INFRASTRUCTURE"
                    };
                    Color catColor = cat switch
                    {
                        BuildingCategory.Military => new Color(0.85f, 0.3f, 0.25f),
                        BuildingCategory.Economy => new Color(0.85f, 0.75f, 0.3f),
                        BuildingCategory.Religion => new Color(0.6f, 0.5f, 0.85f),
                        BuildingCategory.Academic => new Color(0.4f, 0.7f, 0.9f),
                        _ => new Color(0.6f, 0.6f, 0.55f)
                    };
                    AddSectionLabel(content, catName);
                    
                    foreach (var (idx, slot) in categories[cat])
                    {
                        if (slot.isConstructing)
                        {
                            // Building under construction — with progress info
                            int totalTime = BuildingInfo.GetBuildTime(slot.type);
                            int elapsed = Mathf.Max(0, totalTime - slot.turnsToComplete);
                            float progress = totalTime > 0 ? (float)elapsed / totalTime : 0f;
                            
                            AddInfoRow(content, $"  {BuildingInfo.GetIcon(slot.type)} {BuildingInfo.GetName(slot.type)} Lv.{slot.level} — Building ({slot.turnsToComplete}t remaining)", UIFactory.GoldAccent);
                            AddBuildingProgressBar(content, progress);
                        }
                        else
                        {
                            // Constructed building — show effects
                            AddInfoRow(content, $"  {BuildingInfo.GetIcon(slot.type)} {BuildingInfo.GetName(slot.type)} Lv.{slot.level} — Built", new Color(0.4f, 0.8f, 0.4f));
                            string effects = BuildingInfo.GetEffects(slot.type);
                            if (!string.IsNullOrEmpty(effects) && effects != "No effects")
                                AddInfoRow(content, $"    ↳ {effects}", new Color(0.55f, 0.55f, 0.5f), 10);
                        }
                    }
                    AddSpacer(content, 4);
                }
                
                // Empty slots at the bottom
                if (emptySlots.Count > 0)
                {
                    AddSectionLabel(content, "📦 EMPTY SLOTS");
                    foreach (int idx in emptySlots)
                    {
                        GameObject row = new GameObject("EmptySlot");
                        row.transform.SetParent(content, false);
                        row.AddComponent<RectTransform>();
                        UIFactory.AddHorizontalLayout(row, 4f);
                        UIFactory.AddLayoutElement(row, 28);
                        
                        UIFactory.CreateText(row.transform, "Lbl", $"  Slot {idx+1} — Empty", 12, TextAnchor.MiddleLeft, Color.gray);
                        
                        int slotIdx = idx;
                        Button btn = UIFactory.CreateGoldButton(row.transform, "Build", "+ Build", 11, () =>
                        {
                            if (city != null) ShowCityBuildPicker(city);
                        });
                        UIFactory.AddLayoutElement(btn.gameObject, 70, 24);
                    }
                }
            }
            else if (city != null)
            {
                AddInfoRow(content, $"Buildings: {city.buildings.Count}/{city.maxBuildings}", UIFactory.ParchmentBeige);
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
                        BuildingCategory.Military => "⚔ MILITARY",
                        BuildingCategory.Economy => "💰 ECONOMY",
                        BuildingCategory.Religion => "⛪ RELIGION",
                        BuildingCategory.Academic => "🏛 ACADEMIC",
                        _ => "🔧 INFRASTRUCTURE"
                    };
                    AddSectionLabel(content, catName);
                    
                    foreach (var bld in categories[cat])
                    {
                        if (bld.isConstructed)
                        {
                            AddInfoRow(content, $"  {BuildingInfo.GetName(bld.buildingType)} Lv.{bld.level} — Built", new Color(0.4f, 0.8f, 0.4f));
                            string effects = BuildingInfo.GetEffects(bld.buildingType);
                            if (!string.IsNullOrEmpty(effects) && effects != "No effects")
                                AddInfoRow(content, $"    ↳ {effects}", new Color(0.55f, 0.55f, 0.5f), 10);
                        }
                        else if (bld.isConstructing)
                        {
                            AddInfoRow(content, $"  {BuildingInfo.GetName(bld.buildingType)} Lv.{bld.level} — Building ({bld.constructionTurnsRemaining}t)", UIFactory.GoldAccent);
                            int totalTime = BuildingInfo.GetBuildTime(bld.buildingType);
                            int elapsed = Mathf.Max(0, totalTime - bld.constructionTurnsRemaining);
                            float progress = totalTime > 0 ? (float)elapsed / totalTime : 0f;
                            AddBuildingProgressBar(content, progress);
                        }
                        else
                        {
                            AddInfoRow(content, $"  {BuildingInfo.GetName(bld.buildingType)} Lv.{bld.level} — Planned", new Color(0.5f, 0.5f, 0.45f));
                        }
                    }
                    AddSpacer(content, 4);
                }
                
                int emptyCount = city.maxBuildings - city.buildings.Count;
                if (emptyCount > 0)
                {
                    AddSectionLabel(content, "📦 EMPTY SLOTS");
                    for (int s = 0; s < emptyCount; s++)
                    {
                        GameObject row = new GameObject("EmptySlot");
                        row.transform.SetParent(content, false);
                        row.AddComponent<RectTransform>();
                        UIFactory.AddHorizontalLayout(row, 4f);
                        UIFactory.AddLayoutElement(row, 28);
                        
                        UIFactory.CreateText(row.transform, "Lbl", "  Empty slot", 12, TextAnchor.MiddleLeft, Color.gray);
                        
                        Button btn = UIFactory.CreateGoldButton(row.transform, "Build", "+ Build", 11, () =>
                        {
                            ShowCityBuildPicker(city);
                        });
                        UIFactory.AddLayoutElement(btn.gameObject, 70, 24);
                    }
                }
            }
            else
            {
                AddInfoRow(content, "No building data available", Color.gray);
            }
            
            // Production queue
            if (city != null && city.productionQueue.Count > 0)
            {
                AddSpacer(content, 6);
                AddSectionLabel(content, "PRODUCTION QUEUE");
                foreach (var item in city.productionQueue)
                {
                    string n = item.itemType == ProductionItemType.Building ? "Building" :
                              item.itemType == ProductionItemType.Unit ? item.unitType.ToString() : "Goods";
                    AddInfoRow(content, $"  {n} — {item.turnsRemaining} turns", UIFactory.GoldAccent);
                }
            }
        }
        
        /// <summary>
        /// Add a thin construction progress bar beneath a building row.
        /// </summary>
        private void AddBuildingProgressBar(Transform parent, float progress)
        {
            GameObject barContainer = new GameObject("ProgressBar");
            barContainer.transform.SetParent(parent, false);
            RectTransform barRect = barContainer.AddComponent<RectTransform>();
            UIFactory.AddLayoutElement(barContainer, 8);
            
            // Background bar (dark)
            GameObject bgBar = new GameObject("BG");
            bgBar.transform.SetParent(barContainer.transform, false);
            RectTransform bgRect = bgBar.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.05f, 0.2f);
            bgRect.anchorMax = new Vector2(0.95f, 0.8f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            Image bgImg = bgBar.AddComponent<Image>();
            bgImg.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            
            // Fill bar (gold/green)
            GameObject fillBar = new GameObject("Fill");
            fillBar.transform.SetParent(barContainer.transform, false);
            RectTransform fillRect = fillBar.AddComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0.05f, 0.2f);
            fillRect.anchorMax = new Vector2(0.05f + 0.9f * Mathf.Clamp01(progress), 0.8f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            Image fillImg = fillBar.AddComponent<Image>();
            fillImg.color = progress >= 0.75f ? new Color(0.4f, 0.8f, 0.3f, 0.9f) : new Color(0.85f, 0.7f, 0.2f, 0.9f);
        }
        
        private void ShowCityBuildPicker(CityData city)
        {
            foreach (Transform child in buildMenuContainer) Destroy(child.gameObject);
            BuildingType[] types = { BuildingType.Farm, BuildingType.Mine, BuildingType.Barracks,
                BuildingType.Stables, BuildingType.Armory, BuildingType.Market,
                BuildingType.Fortress, BuildingType.Church, BuildingType.University };
            foreach (var type in types)
            {
                if (city.buildings.Exists(b => b.buildingType == type && (b.isConstructed || b.isConstructing))) continue;
                int cost = BuildingInfo.GetCostGold(type, 0);
                int time = BuildingInfo.GetBuildTime(type);
                BuildingType ct = type;
                Button btn = UIFactory.CreateGoldButton(buildMenuContainer, $"B_{type}", $"{BuildingInfo.GetName(type)} — {cost}g, {time}t", 12, () =>
                { city.StartBuildingConstruction(ct, cost); buildMenuPanel.SetActive(false); cityPanelDirty = true; });
                UIFactory.AddLayoutElement(btn.gameObject, 28);
            }
            buildMenuPanel.SetActive(true);
        }
        
        // ─── MILITARY TAB ─────────────────────────────────────────
        private void RefreshMilitaryTab(CityData city, ProvinceData prov)
        {
            Transform content = GetTabContent(cityMilitaryTab);
            if (content == null) return;
            ClearContent(content);
            
            AddInfoRow(content, city != null ? city.cityName : prov.provinceName, UIFactory.GoldAccent, 16, true);
            AddSpacer(content, 4);
            AddSectionLabel(content, "RECRUIT UNITS");
            
            var units = new (UnitType type, string name, string req, int cost, int turns)[]
            {
                (UnitType.LineInfantry, "Line Infantry", "Barracks", 100, 2),
                (UnitType.LightInfantry, "Light Infantry", "Barracks", 80, 2),
                (UnitType.Grenadier, "Grenadier", "Barracks", 150, 3),
                (UnitType.Cavalry, "Cavalry", "Stables", 200, 3),
                (UnitType.Hussar, "Hussar", "Stables", 180, 3),
                (UnitType.Lancer, "Lancer", "Stables", 190, 3),
                (UnitType.Artillery, "Artillery", "Armory", 300, 4),
            };
            
            foreach (var (type, name, req, cost, turns) in units)
            {
                bool canProduce = city != null && city.CanProduceUnit(type);
                
                GameObject row = new GameObject($"U_{name}");
                row.transform.SetParent(content, false);
                row.AddComponent<RectTransform>();
                UIFactory.AddHorizontalLayout(row, 4f);
                UIFactory.AddLayoutElement(row, 28);
                
                Color col = canProduce ? UIFactory.ParchmentBeige : new Color(0.4f, 0.4f, 0.4f);
                Text lbl = UIFactory.CreateText(row.transform, "Lbl", $"  {name} ({cost}g, {turns}t)", 12, TextAnchor.MiddleLeft, col);
                UIFactory.AddLayoutElement(lbl.gameObject, flexibleWidth: 1);
                
                if (canProduce)
                {
                    UnitType ct = type; int cc = cost;
                    Button btn = UIFactory.CreateGoldButton(row.transform, "Rec", "Recruit", 11, () =>
                    { city.StartUnitProduction(ct, cc); cityPanelDirty = true; });
                    UIFactory.AddLayoutElement(btn.gameObject, 70, 24);
                }
                else
                {
                    Text r = UIFactory.CreateText(row.transform, "Req", $"Need {req}", 10, TextAnchor.MiddleRight, new Color(0.7f, 0.3f, 0.2f));
                    UIFactory.AddLayoutElement(r.gameObject, 80);
                }
            }
            
            AddSpacer(content, 6);
            AddSectionLabel(content, "GARRISON");
            if (city != null)
            {
                AddInfoRow(content, $"  Garrison: {city.garrisonSize}/{city.maxGarrison}", UIFactory.ParchmentBeige);
                AddInfoRow(content, $"  Fortification: Lv.{city.GetFortificationLevel()}", UIFactory.ParchmentBeige);
            }
            
            AddSpacer(content, 6);
            AddSectionLabel(content, "PRODUCTION QUEUE");
            if (city != null && city.productionQueue.Count > 0)
            {
                foreach (var item in city.productionQueue)
                {
                    string n = item.itemType == ProductionItemType.Unit ? item.unitType.ToString() : "Building";
                    AddInfoRow(content, $"  {n} — {item.turnsRemaining} turns", UIFactory.GoldAccent);
                }
            }
            else
            {
                AddInfoRow(content, "  No production queued", Color.gray);
            }
        }
        
        // ─── INDUSTRY TAB ─────────────────────────────────────────
        private void RefreshIndustryTab(CityData city, ProvinceData prov)
        {
            Transform content = GetTabContent(cityIndustryTab);
            if (content == null) return;
            ClearContent(content);
            
            AddInfoRow(content, city != null ? city.cityName : prov.provinceName, UIFactory.GoldAccent, 16, true);
            AddSpacer(content, 4);
            AddSectionLabel(content, "ACTIVE INDUSTRIES");
            
            if (city != null && city.industries.Count > 0)
            {
                foreach (var ind in city.industries)
                {
                    string output = "";
                    if (ind.GetGoldOutput() > 0) output += $"+{ind.GetGoldOutput():F0}g ";
                    if (ind.GetFoodOutput() > 0) output += $"+{ind.GetFoodOutput():F0} food ";
                    if (ind.GetIronOutput() > 0) output += $"+{ind.GetIronOutput():F0} iron ";
                    if (ind.GetWoodOutput() > 0) output += $"+{ind.GetWoodOutput():F0} wood ";
                    if (ind.GetProductionOutput() > 0) output += $"+{ind.GetProductionOutput():F0} prod ";
                    if (string.IsNullOrEmpty(output)) output = "(base)";
                    
                    GameObject row = new GameObject($"I_{ind.industryType}");
                    row.transform.SetParent(content, false);
                    row.AddComponent<RectTransform>();
                    UIFactory.AddHorizontalLayout(row, 4f);
                    UIFactory.AddLayoutElement(row, 28);
                    
                    UIFactory.CreateText(row.transform, "Lbl", $"  {ind.industryType} Lv.{ind.level} — {output}", 12, TextAnchor.MiddleLeft, UIFactory.ParchmentBeige);
                    
                    IndustrySlot ci = ind;
                    Button btn = UIFactory.CreateGoldButton(row.transform, "Up", "Upgrade", 10, () =>
                    { ci.level++; cityPanelDirty = true; });
                    UIFactory.AddLayoutElement(btn.gameObject, 65, 24);
                }
            }
            else
            {
                AddInfoRow(content, "  Agriculture Lv.1 — +25 food", UIFactory.ParchmentBeige);
                AddInfoRow(content, "  Logging Lv.1 — +20 wood", UIFactory.ParchmentBeige);
            }
            
            AddSpacer(content, 6);
            AddSectionLabel(content, "ADD INDUSTRY");
            
            IndustryType[] avail = { IndustryType.Commerce, IndustryType.Manufacturing, IndustryType.Mining,
                IndustryType.Fishing, IndustryType.Shipbuilding, IndustryType.Textile };
            
            foreach (var it in avail)
            {
                if (city != null && city.industries.Exists(i => i.industryType == it)) continue;
                IndustryType ct = it;
                Button ab = UIFactory.CreateGoldButton(content, $"Add_{it}", $"+ {it}", 11, () =>
                { if (city != null) { city.AddIndustry(ct); cityPanelDirty = true; } });
                UIFactory.AddLayoutElement(ab.gameObject, 26);
            }
            
            AddSpacer(content, 6);
            AddSectionLabel(content, "ECONOMY SUMMARY");
            AddInfoRow(content, $"  Gold: +{prov.goldIncome:F0}/turn", UIFactory.GoldAccent);
            AddInfoRow(content, $"  Food: +{prov.foodProduction:F0}/turn", new Color(0.5f, 0.8f, 0.3f));
            AddInfoRow(content, $"  Iron: +{prov.ironProduction:F0}/turn", new Color(0.6f, 0.6f, 0.65f));
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
