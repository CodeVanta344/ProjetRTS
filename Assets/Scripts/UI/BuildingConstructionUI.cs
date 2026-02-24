using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using NapoleonicWars.Campaign;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// Building Construction UI - Total War: Empire style city building management
    /// Shows available buildings, construction queue, and building details
    /// </summary>
    public class BuildingConstructionUI : MonoBehaviour
    {
        private Canvas canvas;
        private CityData currentCity;
        private BuildingCategory selectedCategory = BuildingCategory.Economy;
        
        // UI References
        private Transform buildingGrid;
        private Transform constructionQueuePanel;
        private Transform detailsPanel;
        private Text cityInfoText;
        private Text goldText;
        
        // Prefabs (created dynamically via UIFactory)
        private GameObject buildingSlotPrefab;
        private GameObject queueItemPrefab;
        private GameObject categoryTabPrefab;
        
        // State
        private List<GameObject> buildingSlots = new List<GameObject>();
        private List<GameObject> queueItems = new List<GameObject>();
        private BuildingData selectedBuilding;
        
        public void Show(CityData city)
        {
            currentCity = city;
            BuildUI();
            UpdateUI();
            gameObject.SetActive(true);
        }
        
        public void Hide()
        {
            gameObject.SetActive(false);
            currentCity = null;
        }
        
        private void BuildUI()
        {
            // Clear existing
            if (canvas != null)
                Destroy(canvas.gameObject);
            
            // Main Canvas
            canvas = UIFactory.CreateCanvas("BuildingConstructionUI", 20);
            canvas.transform.SetParent(transform, false);
            
            // Dim overlay (covers full screen, click to close)
            var dimOverlay = UIFactory.CreatePanel(canvas.transform, "DimOverlay", new Color(0f, 0f, 0f, 0.5f));
            dimOverlay.anchorMin = Vector2.zero;
            dimOverlay.anchorMax = Vector2.one;
            dimOverlay.offsetMin = Vector2.zero;
            dimOverlay.offsetMax = Vector2.zero;
            var dimBtn = dimOverlay.gameObject.AddComponent<Button>();
            dimBtn.onClick.AddListener(Hide);
            
            // Main Panel — centered, 80% width × 85% height
            var bg = UIFactory.CreatePanel(canvas.transform, "Background", new Color(0.05f, 0.03f, 0.02f, 0.98f));
            bg.anchorMin = new Vector2(0.10f, 0.075f);
            bg.anchorMax = new Vector2(0.90f, 0.925f);
            bg.offsetMin = Vector2.zero;
            bg.offsetMax = Vector2.zero;
            
            // Ornamental border
            var outerBorder = UIFactory.CreatePanel(bg, "OuterBorder", UIFactory.GoldAccent);
            outerBorder.anchorMin = new Vector2(-0.003f, -0.005f);
            outerBorder.anchorMax = new Vector2(1.003f, 1.005f);
            outerBorder.offsetMin = Vector2.zero;
            outerBorder.offsetMax = Vector2.zero;
            var innerBg = UIFactory.CreatePanel(outerBorder, "InnerBg", new Color(0.05f, 0.03f, 0.02f, 1f));
            innerBg.anchorMin = new Vector2(0.002f, 0.003f);
            innerBg.anchorMax = new Vector2(0.998f, 0.997f);
            innerBg.offsetMin = Vector2.zero;
            innerBg.offsetMax = Vector2.zero;
            
            // Top Bar - City Name and Gold
            var topBar = UIFactory.CreatePanel(bg, "TopBar", new Color(0.12f, 0.13f, 0.11f, 1f));
            topBar.anchorMin = new Vector2(0, 0.92f);
            topBar.anchorMax = new Vector2(1, 1f);
            topBar.offsetMin = Vector2.zero;
            topBar.offsetMax = Vector2.zero;
            
            // Gold accent line
            var accentLine = UIFactory.CreatePanel(topBar, "AccentLine", UIFactory.GoldAccent);
            accentLine.anchorMin = new Vector2(0, 0);
            accentLine.anchorMax = new Vector2(1, 0.05f);
            accentLine.offsetMin = Vector2.zero;
            accentLine.offsetMax = Vector2.zero;
            
            // Title
            var title = UIFactory.CreateText(topBar, "Title", "🏛️ CITY CONSTRUCTION", 28, TextAnchor.MiddleCenter, UIFactory.GoldAccent);
            title.fontStyle = FontStyle.Bold;
            title.GetComponent<RectTransform>().anchorMin = new Vector2(0.3f, 0.1f);
            title.GetComponent<RectTransform>().anchorMax = new Vector2(0.7f, 0.9f);
            
            // City Name
            cityInfoText = UIFactory.CreateText(topBar, "CityInfo", currentCity?.cityName ?? "", 22, TextAnchor.MiddleLeft, Color.white);
            cityInfoText.GetComponent<RectTransform>().anchorMin = new Vector2(0.02f, 0.1f);
            cityInfoText.GetComponent<RectTransform>().anchorMax = new Vector2(0.25f, 0.9f);
            
            // Gold Display
            goldText = UIFactory.CreateText(topBar, "GoldDisplay", "💰 0", 20, TextAnchor.MiddleRight, new Color(1f, 0.84f, 0f));
            goldText.GetComponent<RectTransform>().anchorMin = new Vector2(0.75f, 0.1f);
            goldText.GetComponent<RectTransform>().anchorMax = new Vector2(0.98f, 0.9f);
            
            // Close Button
            var closeBtn = UIFactory.CreateGoldButton(topBar, "BtnClose", "✕", 20, Hide);
            closeBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.95f, 0.2f);
            closeBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.99f, 0.8f);
            
            // Category Tabs (Left side)
            BuildCategoryTabs(bg);
            
            // Building Grid (Center)
            BuildBuildingGrid(bg);
            
            // Construction Queue (Right side)
            BuildQueuePanel(bg);
            
            // Building Details (Bottom)
            BuildDetailsPanel(bg);
            
            // Existing Buildings (Left, below tabs)
            BuildExistingBuildingsPanel(bg);
        }
        
        private void BuildCategoryTabs(Transform parent)
        {
            var tabPanel = UIFactory.CreatePanel(parent, "CategoryTabs", new Color(0.10f, 0.11f, 0.10f, 1f));
            tabPanel.anchorMin = new Vector2(0.02f, 0.65f);
            tabPanel.anchorMax = new Vector2(0.15f, 0.90f);
            tabPanel.offsetMin = Vector2.zero;
            tabPanel.offsetMax = Vector2.zero;
            
            var categories = new[]
            {
                BuildingCategory.Government,
                BuildingCategory.Economy,
                BuildingCategory.Military,
                BuildingCategory.Infrastructure,
                BuildingCategory.Religion,
                BuildingCategory.Academic
            };
            
            var categoryNames = new[] { "Gov't", "Econ", "Military", "Infra", "Religion", "Academic" };
            var categoryIcons = new[] { "⚖️", "💰", "⚔️", "🏗️", "⛪", "📚" };
            
            float tabHeight = 1f / categories.Length;
            for (int i = 0; i < categories.Length; i++)
            {
                var cat = categories[i];
                var btn = UIFactory.CreateWarhammerButton(tabPanel, $"Tab_{cat}", $"{categoryIcons[i]} {categoryNames[i]}", 14, () => SelectCategory(cat));
                
                var rt = btn.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 1f - (i + 1) * tabHeight);
                rt.anchorMax = new Vector2(1, 1f - i * tabHeight - 0.02f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                
                // Highlight selected category
                var img = btn.GetComponent<Image>();
                if (cat == selectedCategory)
                    img.color = new Color(0.6f, 0.5f, 0.3f);
            }
        }
        
        private void BuildBuildingGrid(Transform parent)
        {
            var gridPanel = UIFactory.CreatePanel(parent, "BuildingGrid", new Color(0.10f, 0.11f, 0.10f, 1f));
            gridPanel.anchorMin = new Vector2(0.17f, 0.35f);
            gridPanel.anchorMax = new Vector2(0.68f, 0.90f);
            gridPanel.offsetMin = Vector2.zero;
            gridPanel.offsetMax = Vector2.zero;
            
            // Grid container
            var gridObj = new GameObject("Grid");
            gridObj.transform.SetParent(gridPanel, false);
            var grid = gridObj.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(100, 120);
            grid.spacing = new Vector2(10, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.childAlignment = TextAnchor.UpperCenter;
            
            var gridRT = gridObj.GetComponent<RectTransform>();
            gridRT.anchorMin = new Vector2(0.02f, 0.02f);
            gridRT.anchorMax = new Vector2(0.98f, 0.98f);
            gridRT.offsetMin = Vector2.zero;
            gridRT.offsetMax = Vector2.zero;
            
            buildingGrid = gridObj.transform;
        }
        
        private void BuildQueuePanel(Transform parent)
        {
            var queuePanel = UIFactory.CreatePanel(parent, "QueuePanel", new Color(0.10f, 0.11f, 0.10f, 1f));
            queuePanel.anchorMin = new Vector2(0.70f, 0.35f);
            queuePanel.anchorMax = new Vector2(0.98f, 0.90f);
            queuePanel.offsetMin = Vector2.zero;
            queuePanel.offsetMax = Vector2.zero;
            
            // Title
            var title = UIFactory.CreateText(queuePanel, "QueueTitle", "📋 CONSTRUCTION QUEUE", 16, TextAnchor.MiddleCenter, UIFactory.GoldAccent);
            title.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0.9f);
            title.GetComponent<RectTransform>().anchorMax = new Vector2(1, 1f);
            
            // Queue items container
            var scrollView = new GameObject("QueueScroll");
            scrollView.transform.SetParent(queuePanel, false);
            var scrollRT = scrollView.AddComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0.02f, 0.02f);
            scrollRT.anchorMax = new Vector2(0.98f, 0.88f);
            scrollRT.offsetMin = Vector2.zero;
            scrollRT.offsetMax = Vector2.zero;
            
            constructionQueuePanel = scrollView.transform;
        }
        
        private void BuildDetailsPanel(Transform parent)
        {
            var detailsPanel = UIFactory.CreatePanel(parent, "DetailsPanel", new Color(0.10f, 0.11f, 0.10f, 1f));
            detailsPanel.anchorMin = new Vector2(0.17f, 0.05f);
            detailsPanel.anchorMax = new Vector2(0.98f, 0.33f);
            detailsPanel.offsetMin = Vector2.zero;
            detailsPanel.offsetMax = Vector2.zero;
            
            // Ornate border
            var outline = detailsPanel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.4f, 0.3f, 0.1f);
            outline.effectDistance = new Vector2(2, 2);
            
            this.detailsPanel = detailsPanel;
            
            // Default message
            ShowNoSelection();
        }
        
        private void ShowNoSelection()
        {
            if (detailsPanel == null) return;
            
            // Clear existing
            foreach (Transform child in detailsPanel)
                if (child.name != "DetailsPanel") Destroy(child.gameObject);
            
            var msg = UIFactory.CreateText(detailsPanel, "NoSelection", 
                "Select a building to view details\n\n" +
                "🏛️ Buildings provide various bonuses to your city\n" +
                "⚡ Construction takes time and gold\n" +
                "📈 Upgrades improve existing buildings", 
                14, TextAnchor.MiddleCenter, new Color(0.5f, 0.5f, 0.5f));
            
            msg.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            msg.GetComponent<RectTransform>().anchorMax = Vector2.one;
            msg.GetComponent<RectTransform>().offsetMin = Vector2.zero;
            msg.GetComponent<RectTransform>().offsetMax = Vector2.zero;
        }
        
        private void BuildExistingBuildingsPanel(Transform parent)
        {
            var existingPanel = UIFactory.CreatePanel(parent, "ExistingBuildings", new Color(0.10f, 0.11f, 0.10f, 1f));
            existingPanel.anchorMin = new Vector2(0.02f, 0.05f);
            existingPanel.anchorMax = new Vector2(0.15f, 0.63f);
            existingPanel.offsetMin = Vector2.zero;
            existingPanel.offsetMax = Vector2.zero;
            
            // Title
            var title = UIFactory.CreateText(existingPanel, "ExistingTitle", "🏛️ BUILT", 14, TextAnchor.MiddleCenter, UIFactory.GoldAccent);
            title.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0.9f);
            title.GetComponent<RectTransform>().anchorMax = new Vector2(1, 1f);
        }
        
        private void SelectCategory(BuildingCategory category)
        {
            selectedCategory = category;
            selectedBuilding = null;
            BuildUI(); // Rebuild to update tab highlights
            UpdateUI();
        }
        
        private void UpdateUI()
        {
            if (currentCity == null) return;
            
            // Update gold
            var faction = CampaignManager.Instance?.GetPlayerFaction();
            if (faction != null && goldText != null)
                goldText.text = $"💰 {faction.gold:N0}";
            
            // Update city info
            if (cityInfoText != null)
            {
                int queueCount = BuildingManager.Instance?.GetCityQueue(currentCity.cityId)?.Count ?? 0;
                int maxQueue = CityLevelThresholds.GetMaxConcurrentConstruction(currentCity.cityLevel);
                cityInfoText.text = $"{currentCity.cityName}\n" +
                                    $"Lv.{currentCity.cityLevel} {currentCity.CityLevelName}\n" +
                                    $"🏠 {currentCity.buildings.Count}/{currentCity.maxBuildings}  ⚒ {queueCount}/{maxQueue}";
            }
            
            // Update building grid
            UpdateBuildingGrid();
            
            // Update queue
            UpdateQueue();
            
            // Update existing buildings
            UpdateExistingBuildings();
        }
        
        private void UpdateBuildingGrid()
        {
            // Clear old slots
            foreach (var slot in buildingSlots)
                if (slot != null) Destroy(slot);
            buildingSlots.Clear();
            
            if (buildingGrid == null) return;
            
            // Get available buildings for this category
            var availableBuildings = BuildingDatabase.Instance?.GetBuildingsByCategory(selectedCategory) ?? new List<BuildingData>();
            
            foreach (var building in availableBuildings)
            {
                string error = "";
                var canBuild = BuildingManager.Instance?.CanConstructBuilding(currentCity.cityId, building.buildingId, out error) ?? false;
                var slot = CreateBuildingSlot(building, canBuild, error);
                slot.transform.SetParent(buildingGrid, false);
                buildingSlots.Add(slot);
            }
        }
        
        private GameObject CreateBuildingSlot(BuildingData building, bool canBuild, string errorMessage)
        {
            var slot = new GameObject($"Slot_{building.buildingId}");
            var rt = slot.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(100, 120);
            
            // Background
            var bg = slot.AddComponent<Image>();
            bg.color = canBuild ? new Color(0.15f, 0.12f, 0.08f) : new Color(0.12f, 0.13f, 0.11f);
            
            // Border
            var border = new GameObject("Border");
            border.transform.SetParent(slot.transform, false);
            var borderRT = border.AddComponent<RectTransform>();
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = Vector2.one;
            borderRT.offsetMin = new Vector2(-2, -2);
            borderRT.offsetMax = new Vector2(2, 2);
            var borderImg = border.AddComponent<Image>();
            borderImg.color = canBuild ? UIFactory.GoldAccent : new Color(0.3f, 0.25f, 0.2f);
            
            // Building Icon
            if (building.icon != null)
            {
                var iconGO = new GameObject("Icon");
                iconGO.transform.SetParent(slot.transform, false);
                var iconRT = iconGO.AddComponent<RectTransform>();
                iconRT.anchorMin = new Vector2(0.05f, 0.35f);
                iconRT.anchorMax = new Vector2(0.95f, 0.92f);
                iconRT.offsetMin = Vector2.zero;
                iconRT.offsetMax = Vector2.zero;
                var iconImg = iconGO.AddComponent<Image>();
                iconImg.sprite = building.icon;
                iconImg.preserveAspect = true;
            }
            else
            {
                // Fallback emoji text
                var iconText = UIFactory.CreateText(slot.transform, "Icon", GetCategoryIcon(building.category), 32, TextAnchor.MiddleCenter, Color.white);
                iconText.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0.4f);
                iconText.GetComponent<RectTransform>().anchorMax = new Vector2(1, 0.9f);
            }
            
            // Name
            var nameText = UIFactory.CreateText(slot.transform, "Name", building.displayName, 11, TextAnchor.MiddleCenter, Color.white);
            nameText.GetComponent<RectTransform>().anchorMin = new Vector2(0.05f, 0.22f);
            nameText.GetComponent<RectTransform>().anchorMax = new Vector2(0.95f, 0.38f);
            
            // Cost
            var existingLevel = currentCity.buildings.Find(b => b.buildingId == building.buildingId)?.level ?? 0;
            var cost = building.GetTotalCost(existingLevel + 1);
            var costText = UIFactory.CreateText(slot.transform, "Cost", $"💰{cost}", 10, TextAnchor.MiddleCenter, new Color(1f, 0.84f, 0f));
            costText.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0.05f);
            costText.GetComponent<RectTransform>().anchorMax = new Vector2(1, 0.20f);
            
            if (!canBuild)
            {
                costText.color = new Color(0.5f, 0.5f, 0.5f);
                nameText.color = new Color(0.5f, 0.5f, 0.5f);
                
                // Dim the icon to show it's locked
                var iconImages = slot.GetComponentsInChildren<Image>();
                foreach (var img in iconImages)
                {
                    if (img.gameObject.name == "Icon")
                        img.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
                }
                
                // Small red error text overlay on the card itself
                var errorOverlay = UIFactory.CreatePanel(slot.transform, "ErrorOverlay", new Color(0f, 0f, 0f, 0.6f));
                errorOverlay.anchorMin = new Vector2(0.02f, 0.4f);
                errorOverlay.anchorMax = new Vector2(0.98f, 0.7f);
                errorOverlay.offsetMin = Vector2.zero;
                errorOverlay.offsetMax = Vector2.zero;
                
                var errorText = UIFactory.CreateText(errorOverlay, "ErrorText", errorMessage, 9, TextAnchor.MiddleCenter, new Color(1f, 0.4f, 0.4f));
                errorText.fontStyle = FontStyle.Bold;
                var errRT = errorText.GetComponent<RectTransform>();
                errRT.anchorMin = Vector2.zero;
                errRT.anchorMax = Vector2.one;
                errRT.offsetMin = new Vector2(2, 0);
                errRT.offsetMax = new Vector2(-2, 0);
            }
            
            // Click handler
            if (canBuild)
            {
                var btn = slot.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() => SelectBuilding(building));
            }
            
            return slot;
        }
        
        private void SelectBuilding(BuildingData building)
        {
            selectedBuilding = building;
            ShowBuildingDetails(building);
        }
        
        private void ShowBuildingDetails(BuildingData building)
        {
            if (detailsPanel == null) return;
            
            // Clear existing (except panel itself)
            foreach (Transform child in detailsPanel)
                if (child.name != "DetailsPanel") Destroy(child.gameObject);
            
            // Icon and Name row
            if (building.icon != null)
            {
                var iconGO = new GameObject("DetailIcon");
                iconGO.transform.SetParent(detailsPanel, false);
                var iconRT = iconGO.AddComponent<RectTransform>();
                iconRT.anchorMin = new Vector2(0.02f, 0.68f);
                iconRT.anchorMax = new Vector2(0.13f, 0.95f);
                iconRT.offsetMin = Vector2.zero;
                iconRT.offsetMax = Vector2.zero;
                var iconImg = iconGO.AddComponent<Image>();
                iconImg.sprite = building.icon;
                iconImg.preserveAspect = true;
            }
            else
            {
                var icon = UIFactory.CreateText(detailsPanel, "DetailIcon", GetCategoryIcon(building.category), 40, TextAnchor.MiddleLeft, Color.white);
                icon.GetComponent<RectTransform>().anchorMin = new Vector2(0.02f, 0.7f);
                icon.GetComponent<RectTransform>().anchorMax = new Vector2(0.12f, 0.95f);
            }
            
            var nameText = UIFactory.CreateText(detailsPanel, "DetailName", building.displayName, 22, TextAnchor.MiddleLeft, UIFactory.GoldAccent);
            nameText.fontStyle = FontStyle.Bold;
            nameText.GetComponent<RectTransform>().anchorMin = new Vector2(0.14f, 0.75f);
            nameText.GetComponent<RectTransform>().anchorMax = new Vector2(0.6f, 0.95f);
            
            var categoryText = UIFactory.CreateText(detailsPanel, "DetailCategory", building.category.ToString(), 12, TextAnchor.MiddleLeft, new Color(0.7f, 0.7f, 0.7f));
            categoryText.GetComponent<RectTransform>().anchorMin = new Vector2(0.14f, 0.65f);
            categoryText.GetComponent<RectTransform>().anchorMax = new Vector2(0.6f, 0.75f);
            
            // Description
            var descText = UIFactory.CreateText(detailsPanel, "DetailDesc", building.description, 13, TextAnchor.UpperLeft, UIFactory.ParchmentBeige);
            descText.GetComponent<RectTransform>().anchorMin = new Vector2(0.02f, 0.35f);
            descText.GetComponent<RectTransform>().anchorMax = new Vector2(0.6f, 0.63f);
            
            // Effects
            var existingLevel = currentCity.buildings.Find(b => b.buildingId == building.buildingId)?.level ?? 0;
            var targetLevel = existingLevel + 1;
            var effects = building.GetEffectsDescription(targetLevel);
            var effectsText = UIFactory.CreateText(detailsPanel, "DetailEffects", $"Level {targetLevel} Effects:\n{effects}", 12, TextAnchor.UpperLeft, new Color(0.5f, 0.8f, 0.5f));
            effectsText.GetComponent<RectTransform>().anchorMin = new Vector2(0.02f, 0.05f);
            effectsText.GetComponent<RectTransform>().anchorMax = new Vector2(0.6f, 0.33f);
            
            // Build/Upgrade Button
            var isUpgrade = existingLevel > 0;
            var btnText = isUpgrade ? $"⬆ Upgrade (Lv.{targetLevel})" : "🔨 Build";
            var cost = building.GetTotalCost(targetLevel);
            var turns = building.GetConstructionTime(targetLevel);
            
            var buildBtn = UIFactory.CreateWarhammerButton(detailsPanel, "BtnBuild", btnText, 18, () => {
                if (BuildingManager.Instance?.QueueConstruction(currentCity.cityId, building.buildingId) ?? false)
                {
                    UpdateUI();
                    ShowNoSelection();
                }
            });
            
            buildBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.65f, 0.55f);
            buildBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.98f, 0.85f);
            
            // Cost info
            var costInfo = UIFactory.CreateText(detailsPanel, "CostInfo", $"💰 {cost} | ⏱ {turns} turns", 14, TextAnchor.MiddleCenter, new Color(1f, 0.84f, 0f));
            costInfo.GetComponent<RectTransform>().anchorMin = new Vector2(0.65f, 0.35f);
            costInfo.GetComponent<RectTransform>().anchorMax = new Vector2(0.98f, 0.52f);
            
            // Max level warning
            if (existingLevel >= building.maxLevel)
            {
                var maxText = UIFactory.CreateText(detailsPanel, "MaxLevel", "MAXIMUM LEVEL REACHED", 16, TextAnchor.MiddleCenter, new Color(0.8f, 0.3f, 0.3f));
                maxText.GetComponent<RectTransform>().anchorMin = new Vector2(0.65f, 0.05f);
                maxText.GetComponent<RectTransform>().anchorMax = new Vector2(0.98f, 0.32f);
                buildBtn.interactable = false;
            }
        }
        
        private void UpdateQueue()
        {
            // Clear old items
            foreach (Transform child in constructionQueuePanel)
                Destroy(child.gameObject);
            
            var queue = BuildingManager.Instance?.GetCityQueue(currentCity.cityId) ?? new List<ConstructionQueueItem>();
            
            float yPos = 0.95f;
            foreach (var item in queue)
            {
                var building = BuildingDatabase.Instance?.GetBuilding(item.buildingId);
                if (building == null) continue;
                
                var queueItem = CreateQueueItem(item, building, yPos);
                queueItem.transform.SetParent(constructionQueuePanel, false);
                
                yPos -= 0.22f;
            }
            
            if (queue.Count == 0)
            {
                var emptyText = UIFactory.CreateText(constructionQueuePanel, "EmptyQueue", "No construction in progress", 12, TextAnchor.MiddleCenter, new Color(0.5f, 0.5f, 0.5f));
                emptyText.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0.8f);
                emptyText.GetComponent<RectTransform>().anchorMax = new Vector2(1, 0.95f);
            }
        }
        
        private GameObject CreateQueueItem(ConstructionQueueItem item, BuildingData building, float yPos)
        {
            var go = new GameObject($"Queue_{item.queueId}");
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.02f, yPos - 0.20f);
            rt.anchorMax = new Vector2(0.98f, yPos);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.08f, 0.05f);
            
            // Building icon
            if (building.icon != null)
            {
                var iconGO = new GameObject("QueueIcon");
                iconGO.transform.SetParent(go.transform, false);
                var iconRT = iconGO.AddComponent<RectTransform>();
                iconRT.anchorMin = new Vector2(0.02f, 0.1f);
                iconRT.anchorMax = new Vector2(0.18f, 0.9f);
                iconRT.offsetMin = Vector2.zero;
                iconRT.offsetMax = Vector2.zero;
                var qIcon = iconGO.AddComponent<Image>();
                qIcon.sprite = building.icon;
                qIcon.preserveAspect = true;
            }
            else
            {
                var icon = UIFactory.CreateText(go.transform, "Icon", GetCategoryIcon(building.category), 24, TextAnchor.MiddleLeft, Color.white);
                icon.GetComponent<RectTransform>().anchorMin = new Vector2(0.02f, 0.2f);
                icon.GetComponent<RectTransform>().anchorMax = new Vector2(0.18f, 0.9f);
            }
            
            // Name
            var nameText = UIFactory.CreateText(go.transform, "Name", building.displayName, 13, TextAnchor.MiddleLeft, Color.white);
            nameText.GetComponent<RectTransform>().anchorMin = new Vector2(0.20f, 0.55f);
            nameText.GetComponent<RectTransform>().anchorMax = new Vector2(0.75f, 0.85f);
            
            // Progress
            var progress = $"⏱ {item.turnsRemaining} turns remaining";
            var progressText = UIFactory.CreateText(go.transform, "Progress", progress, 11, TextAnchor.MiddleLeft, new Color(0.8f, 0.8f, 0.5f));
            progressText.GetComponent<RectTransform>().anchorMin = new Vector2(0.20f, 0.25f);
            progressText.GetComponent<RectTransform>().anchorMax = new Vector2(0.75f, 0.55f);
            
            // Cancel button
            var cancelBtn = UIFactory.CreateGoldButton(go.transform, "BtnCancel", "✕", 14, () => {
                BuildingManager.Instance?.CancelConstruction(item.queueId);
                UpdateUI();
            });
            cancelBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.78f, 0.2f);
            cancelBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.98f, 0.9f);
            
            return go;
        }
        
        private void UpdateExistingBuildings()
        {
            // Find existing panel
            var existingPanel = transform.Find("ExistingBuildings");
            if (existingPanel == null) return;
            
            // Clear old items (except title)
            foreach (Transform child in existingPanel)
                if (child.name != "ExistingTitle") Destroy(child.gameObject);
            
            float yPos = 0.85f;
            foreach (var building in currentCity.buildings)
            {
                if (!building.isConstructed) continue;
                
                var data = building.GetBuildingData();
                if (data == null) continue;
                
                var item = CreateExistingBuildingItem(building, data, yPos);
                item.transform.SetParent(existingPanel, false);
                
                yPos -= 0.12f;
            }
        }
        
        private GameObject CreateExistingBuildingItem(CityBuilding building, BuildingData data, float yPos)
        {
            var go = new GameObject($"Existing_{data.buildingId}");
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.05f, yPos - 0.10f);
            rt.anchorMax = new Vector2(0.95f, yPos);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.10f, 0.07f);
            
            // Name with level
            var name = UIFactory.CreateText(go.transform, "Name", $"{data.displayName} Lv.{building.level}", 11, TextAnchor.MiddleLeft, UIFactory.GoldAccent);
            name.GetComponent<RectTransform>().anchorMin = new Vector2(0.05f, 0);
            name.GetComponent<RectTransform>().anchorMax = new Vector2(0.95f, 1f);
            
            return go;
        }
        
        private string GetCategoryIcon(BuildingCategory category)
        {
            return category switch
            {
                BuildingCategory.Government => "⚖️",
                BuildingCategory.Economy => "💰",
                BuildingCategory.Military => "⚔️",
                BuildingCategory.Infrastructure => "🏗️",
                BuildingCategory.Religion => "⛪",
                BuildingCategory.Academic => "📚",
                BuildingCategory.Coastal => "⚓",
                _ => "🏛️"
            };
        }
        
        private void OnEnable()
        {
            // Subscribe to turn events to update queue
            if (BuildingManager.Instance != null)
            {
                BuildingManager.Instance.OnConstructionCompleted += OnConstructionCompleted;
            }
        }
        
        private void OnDisable()
        {
            if (BuildingManager.Instance != null)
            {
                BuildingManager.Instance.OnConstructionCompleted -= OnConstructionCompleted;
            }
        }
        
        private void OnConstructionCompleted(string cityId, string buildingId)
        {
            if (currentCity != null && currentCity.cityId == cityId)
            {
                UpdateUI();
            }
        }
    }
}
