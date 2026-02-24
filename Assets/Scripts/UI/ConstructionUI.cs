using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Campaign;
using NapoleonicWars.Data;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// Premium AAA-style Construction Nationale panel.
    /// Each city appears as a collapsible section with its own building slots,
    /// construction queue, and available buildings. Building slots scale with city size.
    /// </summary>
    public class ConstructionUI : MonoBehaviour
    {
        private CampaignManager campaignManager;
        private FactionData playerFaction;
        private NavigationBar navBar;

        // Premium colors
        static readonly Color CardDark      = new Color(0.06f, 0.06f, 0.07f, 0.97f);
        static readonly Color CardMid       = new Color(0.09f, 0.09f, 0.10f, 0.97f);
        static readonly Color SlotEmpty     = new Color(0.08f, 0.08f, 0.08f, 0.92f);
        static readonly Color SlotBuilt     = new Color(0.08f, 0.14f, 0.08f, 0.95f);
        static readonly Color SlotBuilding  = new Color(0.14f, 0.12f, 0.06f, 0.95f);
        static readonly Color CityCardBg    = new Color(0.07f, 0.07f, 0.08f, 0.97f);
        static readonly Color CapitalGlow   = new Color(0.22f, 0.18f, 0.08f, 0.97f);
        static readonly Color EconColor     = new Color(0.45f, 0.72f, 0.30f);
        static readonly Color MilitaryColor = new Color(0.82f, 0.30f, 0.25f);
        static readonly Color AcademicColor = new Color(0.40f, 0.55f, 0.85f);
        static readonly Color ReligionColor = new Color(0.75f, 0.65f, 0.85f);
        static readonly Color InfraColor    = new Color(0.60f, 0.58f, 0.50f);
        
        // Track expanded cities for collapse state
        private static HashSet<string> expandedCities = new HashSet<string>();

        public static ConstructionUI Create(NavigationBar navBar, CampaignManager manager)
        {
            var overlay = navBar.CreateOverlayPanel("CONSTRUCTION NATIONALE");
            if (overlay == null) return null;

            var ui = overlay.AddComponent<ConstructionUI>();
            ui.navBar = navBar;
            ui.campaignManager = manager;
            ui.playerFaction = manager.GetPlayerFaction();
            navBar.RegisterPanel(NavigationBar.NavPanel.Construction, overlay);

            // Default: expand capital city
            if (expandedCities.Count == 0)
            {
                var cities = manager.GetFactionCities(ui.playerFaction.factionType);
                var capital = cities.Find(c => c.isCapital);
                if (capital != null) expandedCities.Add(capital.cityId);
            }

            ui.BuildContent(overlay.transform);
            return ui;
        }

        // ==================== MAIN BUILD ====================
        private void BuildContent(Transform parent)
        {
            var (scroll, content) = UIFactory.CreateScrollView(parent, "ConstructionScroll");
            UIFactory.AddLayoutElement(scroll.gameObject, flexibleHeight: 1, preferredHeight: 900);
            scroll.horizontal = false;
            scroll.vertical = true;

            // === NATIONAL SUMMARY HERO ===
            BuildNationalSummary(content);

            // === SEPARATOR ===
            UIFactory.CreateGlowSeparator(content, "Sep0", false);
            UIFactory.AddLayoutElement(content.GetChild(content.childCount - 1).gameObject, preferredHeight: 6);

            // === GLOBAL CONSTRUCTION QUEUE ===
            BuildGlobalQueue(content);

            // === SEPARATOR ===
            UIFactory.CreateGlowSeparator(content, "Sep1", false);
            UIFactory.AddLayoutElement(content.GetChild(content.childCount - 1).gameObject, preferredHeight: 6);

            // === PER-CITY SECTIONS ===
            var cities = campaignManager.GetFactionCities(playerFaction.factionType);
            
            // Sort: capital first, then by population desc
            cities.Sort((a, b) =>
            {
                if (a.isCapital && !b.isCapital) return -1;
                if (!a.isCapital && b.isCapital) return 1;
                return b.population.CompareTo(a.population);
            });

            for (int i = 0; i < cities.Count; i++)
            {
                BuildCitySection(content, cities[i]);
                
                if (i < cities.Count - 1)
                {
                    UIFactory.CreateGlowSeparator(content, $"CSep{i}", false,
                        new Color(0.3f, 0.28f, 0.2f, 0.12f), new Color(0.4f, 0.35f, 0.25f, 0.4f));
                    UIFactory.AddLayoutElement(content.GetChild(content.childCount - 1).gameObject, preferredHeight: 3);
                }
            }
        }

        // ==================== 1. NATIONAL SUMMARY ====================
        private void BuildNationalSummary(Transform content)
        {
            int totalWorkshops = playerFaction != null ? playerFaction.civilianFactories : 0;
            var cities = campaignManager.GetFactionCities(playerFaction.factionType);
            int totalCities = cities.Count;
            int totalSlots = 0, usedSlots = 0, constructing = 0;

            foreach (var city in cities)
            {
                int max = CityLevelThresholds.GetMaxBuildings(city.cityLevel);
                totalSlots += max;
                usedSlots += city.buildings.Count(b => b.isConstructed);
                constructing += city.buildings.Count(b => b.isConstructing);
            }

            // Gradient hero
            RectTransform hero = UIFactory.CreateGradientPanel(content, "NatHero",
                new Color(0.14f, 0.12f, 0.08f, 0.98f),
                new Color(0.06f, 0.06f, 0.06f, 0.98f),
                new Color(0.10f, 0.09f, 0.06f, 0.5f));
            UIFactory.AddLayoutElement(hero.gameObject, preferredHeight: 70);

            // Emblem
            Text emblem = UIFactory.CreateText(hero, "Emblem", "🏗", 26, TextAnchor.MiddleCenter, UIFactory.EmpireGold);
            RectTransform emRT = emblem.GetComponent<RectTransform>();
            emRT.anchorMin = new Vector2(0, 0); emRT.anchorMax = new Vector2(0.07f, 1);
            emRT.offsetMin = new Vector2(15, 0); emRT.offsetMax = Vector2.zero;

            // Title
            Text title = UIFactory.CreateText(hero, "Title", "CONSTRUCTION NATIONALE", 12,
                TextAnchor.UpperLeft, UIFactory.SilverText);
            title.fontStyle = FontStyle.Bold;
            RectTransform titRT = title.GetComponent<RectTransform>();
            titRT.anchorMin = new Vector2(0.08f, 0.55f); titRT.anchorMax = new Vector2(0.5f, 0.95f);
            titRT.offsetMin = Vector2.zero; titRT.offsetMax = Vector2.zero;

            // Workshops
            Text workshops = UIFactory.CreateText(hero, "WS",
                $"Ateliers: {totalWorkshops}   •   {totalCities} villes", 11,
                TextAnchor.MiddleLeft, UIFactory.Parchment);
            RectTransform wsRT = workshops.GetComponent<RectTransform>();
            wsRT.anchorMin = new Vector2(0.08f, 0.10f); wsRT.anchorMax = new Vector2(0.5f, 0.55f);
            wsRT.offsetMin = Vector2.zero; wsRT.offsetMax = Vector2.zero;

            // Stats badges on right side
            CreateHeroBadge(hero, "SlotsUsed", $"{usedSlots}/{totalSlots}", "Construits", UIFactory.EmpireGold, 0.55f, 0.72f);
            CreateHeroBadge(hero, "Building", $"{constructing}", "En cours", UIFactory.WarningAmber, 0.74f, 0.88f);
            CreateHeroBadge(hero, "Free", $"{totalSlots - usedSlots - constructing}", "Libres", UIFactory.ActionGreen, 0.90f, 1f);
        }

        private void CreateHeroBadge(Transform parent, string name, string value, string label, Color color, float xMin, float xMax)
        {
            Text val = UIFactory.CreateText(parent, $"{name}Val", value, 22, TextAnchor.MiddleCenter, color);
            val.fontStyle = FontStyle.Bold;
            val.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.7f);
            RectTransform vRT = val.GetComponent<RectTransform>();
            vRT.anchorMin = new Vector2(xMin, 0.35f); vRT.anchorMax = new Vector2(xMax, 0.95f);
            vRT.offsetMin = Vector2.zero; vRT.offsetMax = Vector2.zero;

            Text lbl = UIFactory.CreateText(parent, $"{name}Lbl", label, 8, TextAnchor.MiddleCenter, UIFactory.MutedGold);
            RectTransform lRT = lbl.GetComponent<RectTransform>();
            lRT.anchorMin = new Vector2(xMin, 0.05f); lRT.anchorMax = new Vector2(xMax, 0.35f);
            lRT.offsetMin = Vector2.zero; lRT.offsetMax = Vector2.zero;
        }

        // ==================== 2. GLOBAL QUEUE ====================
        private void BuildGlobalQueue(Transform content)
        {
            UIFactory.CreateBannerHeader(content, "QueueHdr", "📋  FILE DE CONSTRUCTION ACTIVE", 14);
            UIFactory.AddLayoutElement(content.GetChild(content.childCount - 1).gameObject, preferredHeight: 36);

            // Collect all ongoing constructions across cities
            var allBuilding = new List<(CityData city, CityBuilding building)>();
            foreach (var city in campaignManager.GetFactionCities(playerFaction.factionType))
            {
                foreach (var bld in city.buildings)
                {
                    if (bld.isConstructing)
                        allBuilding.Add((city, bld));
                }
            }

            if (allBuilding.Count == 0)
            {
                BuildEmptyState(content, "Aucun projet en cours",
                    "Sélectionnez une ville ci-dessous pour lancer une construction.");
                return;
            }

            foreach (var (city, bld) in allBuilding)
            {
                int totalTurns = BuildingInfo.GetBuildTime(bld.buildingType);
                int remaining = bld.turnsToComplete > 0 ? bld.turnsToComplete : bld.constructionTurnsRemaining;
                float progress = totalTurns > 0 ? 1f - ((float)remaining / totalTurns) : 0.5f;
                progress = Mathf.Clamp01(progress);

                RectTransform row = UIFactory.CreateOrnatePanel(content, $"Q_{bld.buildingId}", SlotBuilding);
                UIFactory.AddLayoutElement(row.gameObject, preferredHeight: 42);

                Transform inner = row.Find("Inner") ?? row;

                // Icon 
                string icon = BuildingInfo.GetIcon(bld.buildingType);
                Text iconTxt = UIFactory.CreateText(inner, "Icon", icon, 18, TextAnchor.MiddleCenter, UIFactory.EmpireGold);
                RectTransform iRT = iconTxt.GetComponent<RectTransform>();
                iRT.anchorMin = new Vector2(0, 0); iRT.anchorMax = new Vector2(0.06f, 1);
                iRT.offsetMin = new Vector2(8, 0); iRT.offsetMax = Vector2.zero;

                // Building name + city
                Text nameTxt = UIFactory.CreateText(inner, "Name",
                    $"{BuildingInfo.GetName(bld.buildingType)}  —  {city.cityName}",
                    11, TextAnchor.MiddleLeft, UIFactory.Parchment);
                nameTxt.fontStyle = FontStyle.Bold;
                RectTransform nRT = nameTxt.GetComponent<RectTransform>();
                nRT.anchorMin = new Vector2(0.07f, 0); nRT.anchorMax = new Vector2(0.45f, 1);
                nRT.offsetMin = Vector2.zero; nRT.offsetMax = Vector2.zero;

                // Progress bar
                var (bg, fill, glow) = UIFactory.CreatePremiumProgressBar(inner, "Prog", UIFactory.EmpireGold);
                RectTransform barRT = bg.transform.parent.GetComponent<RectTransform>();
                barRT.anchorMin = new Vector2(0.47f, 0.25f); barRT.anchorMax = new Vector2(0.82f, 0.75f);
                barRT.offsetMin = Vector2.zero; barRT.offsetMax = Vector2.zero;
                barRT.sizeDelta = Vector2.zero;
                barRT.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                fill.GetComponent<RectTransform>().anchorMax = new Vector2(progress, 1f);

                // Turns remaining
                Text turnsTxt = UIFactory.CreateText(inner, "Turns",
                    $"{remaining} tour{(remaining > 1 ? "s" : "")} restant{(remaining > 1 ? "s" : "")}",
                    10, TextAnchor.MiddleRight, UIFactory.MutedGold);
                RectTransform tRT = turnsTxt.GetComponent<RectTransform>();
                tRT.anchorMin = new Vector2(0.84f, 0); tRT.anchorMax = new Vector2(0.98f, 1);
                tRT.offsetMin = Vector2.zero; tRT.offsetMax = Vector2.zero;
            }
        }

        // ==================== 3. CITY SECTION ====================
        private void BuildCitySection(Transform content, CityData city)
        {
            bool isExpanded = expandedCities.Contains(city.cityId);
            int maxSlots = CityLevelThresholds.GetMaxBuildings(city.cityLevel);
            string levelName = CityLevelThresholds.GetLevelName(city.cityLevel);
            int builtCount = city.buildings.Count(b => b.isConstructed);
            int constructingCount = city.buildings.Count(b => b.isConstructing);
            int freeSlots = Mathf.Max(0, maxSlots - builtCount - constructingCount);

            // ──── City Header (clickable to expand/collapse) ────
            Color headerBg = city.isCapital ? CapitalGlow : new Color(0.10f, 0.10f, 0.10f, 0.97f);
            RectTransform header = UIFactory.CreateGradientPanel(content, $"CityHdr_{city.cityId}",
                new Color(headerBg.r + 0.05f, headerBg.g + 0.04f, headerBg.b + 0.02f, 0.98f),
                headerBg);
            UIFactory.AddLayoutElement(header.gameObject, preferredHeight: 50);

            // Make header clickable
            Button headerBtn = header.gameObject.AddComponent<Button>();
            headerBtn.transition = Selectable.Transition.None;
            string cityId = city.cityId;
            headerBtn.onClick.AddListener(() =>
            {
                if (expandedCities.Contains(cityId))
                    expandedCities.Remove(cityId);
                else
                    expandedCities.Add(cityId);
                Refresh();
            });

            // Expand arrow
            string arrow = isExpanded ? "▼" : "▶";
            Text arrowTxt = UIFactory.CreateText(header, "Arrow", arrow, 14, TextAnchor.MiddleCenter, UIFactory.MutedGold);
            RectTransform arRT = arrowTxt.GetComponent<RectTransform>();
            arRT.anchorMin = new Vector2(0, 0); arRT.anchorMax = new Vector2(0.04f, 1);
            arRT.offsetMin = new Vector2(10, 0); arRT.offsetMax = Vector2.zero;

            // Capital star
            string capitalMark = city.isCapital ? "⭐ " : "";

            // City name
            Text cityName = UIFactory.CreateText(header, "Name",
                $"{capitalMark}{city.cityName}", 14, TextAnchor.MiddleLeft,
                city.isCapital ? UIFactory.BrightGold : UIFactory.Parchment);
            cityName.fontStyle = FontStyle.Bold;
            RectTransform cnRT = cityName.GetComponent<RectTransform>();
            cnRT.anchorMin = new Vector2(0.05f, 0.2f); cnRT.anchorMax = new Vector2(0.35f, 0.85f);
            cnRT.offsetMin = Vector2.zero; cnRT.offsetMax = Vector2.zero;

            // Level badge
            Color levelColor = city.cityLevel switch
            {
                1 => UIFactory.SilverText,
                2 => UIFactory.SilverText,
                3 => UIFactory.MutedGold,
                4 => UIFactory.EmpireGold,
                5 => UIFactory.BrightGold,
                _ => UIFactory.SilverText
            };
            Text levelTxt = UIFactory.CreateText(header, "Level",
                levelName, 10, TextAnchor.MiddleLeft, levelColor);
            RectTransform lvlRT = levelTxt.GetComponent<RectTransform>();
            lvlRT.anchorMin = new Vector2(0.35f, 0.3f); lvlRT.anchorMax = new Vector2(0.48f, 0.75f);
            lvlRT.offsetMin = Vector2.zero; lvlRT.offsetMax = Vector2.zero;

            // Population
            string popStr = city.population >= 1000 ? $"{city.population / 1000}k" : $"{city.population}";
            Text popTxt = UIFactory.CreateText(header, "Pop", $"👤 {popStr}", 10,
                TextAnchor.MiddleCenter, UIFactory.SilverText);
            RectTransform popRT = popTxt.GetComponent<RectTransform>();
            popRT.anchorMin = new Vector2(0.50f, 0.3f); popRT.anchorMax = new Vector2(0.62f, 0.75f);
            popRT.offsetMin = Vector2.zero; popRT.offsetMax = Vector2.zero;

            // Slots summary
            Color slotColor = freeSlots > 0 ? UIFactory.ActionGreen : UIFactory.MutedGold;
            Text slotsTxt = UIFactory.CreateText(header, "Slots",
                $"🏗 {builtCount + constructingCount}/{maxSlots}", 11,
                TextAnchor.MiddleCenter, slotColor);
            slotsTxt.fontStyle = FontStyle.Bold;
            RectTransform slRT = slotsTxt.GetComponent<RectTransform>();
            slRT.anchorMin = new Vector2(0.64f, 0.3f); slRT.anchorMax = new Vector2(0.78f, 0.75f);
            slRT.offsetMin = Vector2.zero; slRT.offsetMax = Vector2.zero;

            // Resources summary
            Text resTxt = UIFactory.CreateText(header, "Res",
                $"💰{city.storedGoods:F0}  🌾{city.storedFood:F0}  ⛏{city.storedIron:F0}",
                9, TextAnchor.MiddleRight, UIFactory.MutedGold);
            RectTransform resRT = resTxt.GetComponent<RectTransform>();
            resRT.anchorMin = new Vector2(0.78f, 0.3f); resRT.anchorMax = new Vector2(0.98f, 0.75f);
            resRT.offsetMin = Vector2.zero; resRT.offsetMax = Vector2.zero;

            // ──── Expanded Content ────
            if (!isExpanded) return;

            // Building slots grid
            BuildBuildingSlotGrid(content, city, maxSlots);

            // Available buildings (categorized)
            if (freeSlots > 0)
                BuildAvailableBuildings(content, city);
        }

        // ==================== 3a. BUILDING SLOTS GRID ====================
        private void BuildBuildingSlotGrid(Transform content, CityData city, int maxSlots)
        {
            RectTransform slotArea = UIFactory.CreatePanel(content, $"Slots_{city.cityId}", CityCardBg);

            // Calculate rows needed (4 per row)
            int cols = 4;
            int rows = Mathf.CeilToInt((float)maxSlots / cols);
            int height = rows * 62 + 12;
            UIFactory.AddLayoutElement(slotArea.gameObject, preferredHeight: height);

            var vlg = UIFactory.AddVerticalLayout(slotArea.gameObject, 4f, new RectOffset(10, 10, 6, 6));
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            int slotIndex = 0;
            for (int r = 0; r < rows; r++)
            {
                GameObject rowGO = new GameObject($"Row{r}");
                rowGO.transform.SetParent(slotArea, false);
                HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(rowGO, 6f);
                hlg.childControlWidth = false;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = false;
                UIFactory.AddLayoutElement(rowGO, preferredHeight: 54);

                for (int c = 0; c < cols && slotIndex < maxSlots; c++, slotIndex++)
                {
                    if (slotIndex < city.buildings.Count)
                    {
                        var bld = city.buildings[slotIndex];
                        CreateFilledSlot(rowGO.transform, bld);
                    }
                    else
                    {
                        CreateEmptySlot(rowGO.transform, slotIndex + 1);
                    }
                }
            }
        }

        private void CreateFilledSlot(Transform parent, CityBuilding bld)
        {
            Color bg = bld.isConstructing ? SlotBuilding : SlotBuilt;
            Color border = bld.isConstructing ? UIFactory.WarningAmber : UIFactory.MutedGold;

            RectTransform card = UIFactory.CreateBorderedPanel(parent, $"Slot_{bld.buildingId}", bg, border, 1.5f);
            UIFactory.AddLayoutElement(card.gameObject, preferredWidth: 200, preferredHeight: 50);

            Transform inner = card.childCount > 0 ? card.GetChild(0) : card;

            string icon = BuildingInfo.GetIcon(bld.buildingType);
            string bName = BuildingInfo.GetName(bld.buildingType);
            Color catColor = GetCategoryColor(BuildingInfo.GetCategory(bld.buildingType));

            // Icon
            Text iconTxt = UIFactory.CreateText(inner, "Icon", icon, 16, TextAnchor.MiddleCenter, catColor);
            RectTransform iRT = iconTxt.GetComponent<RectTransform>();
            iRT.anchorMin = new Vector2(0, 0.3f); iRT.anchorMax = new Vector2(0.18f, 0.95f);
            iRT.offsetMin = new Vector2(4, 0); iRT.offsetMax = Vector2.zero;

            // Name
            Text nameTxt = UIFactory.CreateText(inner, "Name", bName, 10, TextAnchor.MiddleLeft,
                bld.isConstructing ? UIFactory.WarningAmber : UIFactory.Parchment);
            nameTxt.fontStyle = FontStyle.Bold;
            RectTransform nRT = nameTxt.GetComponent<RectTransform>();
            nRT.anchorMin = new Vector2(0.20f, 0.45f); nRT.anchorMax = new Vector2(0.98f, 0.98f);
            nRT.offsetMin = Vector2.zero; nRT.offsetMax = new Vector2(-4, 0);

            if (bld.isConstructing)
            {
                int remaining = bld.turnsToComplete > 0 ? bld.turnsToComplete : bld.constructionTurnsRemaining;
                Text statusTxt = UIFactory.CreateText(inner, "Status",
                    $"🔨 {remaining}t restant{(remaining > 1 ? "s" : "")}", 8,
                    TextAnchor.MiddleLeft, UIFactory.WarningAmber);
                RectTransform sRT = statusTxt.GetComponent<RectTransform>();
                sRT.anchorMin = new Vector2(0.20f, 0.02f); sRT.anchorMax = new Vector2(0.98f, 0.45f);
                sRT.offsetMin = Vector2.zero; sRT.offsetMax = new Vector2(-4, 0);
            }
            else
            {
                // Level indicator
                Text levelTxt = UIFactory.CreateText(inner, "Lvl",
                    $"Nv.{bld.level}", 8, TextAnchor.MiddleLeft, UIFactory.MutedGold);
                RectTransform lRT = levelTxt.GetComponent<RectTransform>();
                lRT.anchorMin = new Vector2(0.20f, 0.02f); lRT.anchorMax = new Vector2(0.98f, 0.45f);
                lRT.offsetMin = Vector2.zero; lRT.offsetMax = new Vector2(-4, 0);
            }
        }

        private void CreateEmptySlot(Transform parent, int slotNum)
        {
            RectTransform card = UIFactory.CreateBorderedPanel(parent, $"Empty_{slotNum}",
                SlotEmpty, new Color(0.18f, 0.18f, 0.18f, 0.5f), 1f);
            UIFactory.AddLayoutElement(card.gameObject, preferredWidth: 200, preferredHeight: 50);

            Transform inner = card.childCount > 0 ? card.GetChild(0) : card;

            Text emptyTxt = UIFactory.CreateText(inner, "Txt", $"— Emplacement {slotNum} —",
                9, TextAnchor.MiddleCenter, new Color(0.3f, 0.3f, 0.3f));
            RectTransform eRT = emptyTxt.GetComponent<RectTransform>();
            eRT.anchorMin = Vector2.zero; eRT.anchorMax = Vector2.one;
            eRT.offsetMin = Vector2.zero; eRT.offsetMax = Vector2.zero;
        }

        // ==================== 3b. AVAILABLE BUILDINGS ====================
        private void BuildAvailableBuildings(Transform content, CityData city)
        {
            // Group buildings by category
            var categories = new (string label, BuildingCategory cat, BuildingType[] types)[]
            {
                ("💰 Économie", BuildingCategory.Economy, new[] {
                    BuildingType.Farm, BuildingType.Mine, BuildingType.Market }),
                ("⚔ Militaire", BuildingCategory.Military, new[] {
                    BuildingType.Barracks, BuildingType.Stables, BuildingType.Armory, BuildingType.Fortress }),
                ("🎓 Académique", BuildingCategory.Academic, new[] {
                    BuildingType.University, BuildingType.MilitaryAcademy,
                    BuildingType.RoyalMilitaryCollege, BuildingType.MilitaryUniversity }),
                ("⛪ Religion", BuildingCategory.Religion, new[] {
                    BuildingType.Church }),
                ("🏭 Formation Militaire", BuildingCategory.Military, new[] {
                    BuildingType.VillageBarracks, BuildingType.ProvincialBarracks }),
                ("🎯 Artillerie", BuildingCategory.Military, new[] {
                    BuildingType.SmallArtillerySchool, BuildingType.ProvincialArtillerySchool,
                    BuildingType.RoyalArtilleryAcademy, BuildingType.GrandArtilleryAcademy,
                    BuildingType.ImperialArtilleryAcademy }),
            };

            foreach (var (label, cat, types) in categories)
            {
                // Filter out already built/building
                var available = types.Where(t =>
                {
                    bool alreadyBuilt = city.buildings.Exists(b =>
                        b.buildingType == t && (b.isConstructed || b.isConstructing));
                    if (alreadyBuilt) return false;

                    // Check unlock
                    bool unlocked = true;
                    try { unlocked = campaignManager.IsBuildingUnlocked(t); } catch { }
                    return true; // Show locked buildings too (greyed out)
                }).ToList();

                if (available.Count == 0) continue;

                // Category header
                UIFactory.CreateSectionHeader(content, $"Cat_{label.GetHashCode()}", label, 12);
                UIFactory.AddLayoutElement(content.GetChild(content.childCount - 1).gameObject, preferredHeight: 28);

                // Building cards row
                RectTransform rowRT = UIFactory.CreatePanel(content, $"BldRow_{label.GetHashCode()}", CardDark);
                UIFactory.AddLayoutElement(rowRT.gameObject, preferredHeight: 78);
                HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(rowRT.gameObject, 6f, new RectOffset(8, 8, 4, 4));
                hlg.childControlWidth = false;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = false;

                foreach (var bType in available)
                    CreateBuildingCard(rowRT, bType, city);
            }
        }

        private void CreateBuildingCard(RectTransform parent, BuildingType bType, CityData city)
        {
            string icon = BuildingInfo.GetIcon(bType);
            string bName = BuildingInfo.GetName(bType);
            string effects = BuildingInfo.GetEffects(bType);
            if (effects.Length > 45) effects = effects.Substring(0, 42) + "...";
            int cost = BuildingInfo.GetCostGold(bType, 0);
            int turns = BuildingInfo.GetBuildTime(bType);
            BuildingCategory cat = BuildingInfo.GetCategory(bType);
            Color catColor = GetCategoryColor(cat);

            bool unlocked = true;
            try { unlocked = campaignManager.IsBuildingUnlocked(bType); } catch { }

            bool canAfford = playerFaction.gold >= cost;
            int maxSlots = CityLevelThresholds.GetMaxBuildings(city.cityLevel);
            bool hasSlot = city.buildings.Count < maxSlots;
            bool canBuild = unlocked && canAfford && hasSlot;

            // Prerequisites — checked at build time by StartBuildingConstruction
            bool prereqMet = true;
            canBuild = canBuild && prereqMet;

            Color cardBg = canBuild
                ? new Color(catColor.r * 0.15f, catColor.g * 0.15f, catColor.b * 0.15f, 0.95f)
                : new Color(0.08f, 0.08f, 0.08f, 0.85f);
            Color borderCol = canBuild ? catColor : new Color(0.2f, 0.2f, 0.2f, 0.4f);

            RectTransform cardRT = UIFactory.CreateBorderedPanel(parent, $"Bld_{bType}_{city.cityId}",
                cardBg, borderCol, canBuild ? 1.5f : 1f);
            UIFactory.AddLayoutElement(cardRT.gameObject, preferredWidth: 180, preferredHeight: 68);

            Transform inner = cardRT.childCount > 0 ? cardRT.GetChild(0) : cardRT;

            // Icon + Name
            Text nameTxt = UIFactory.CreateText(inner, "Name", $"{icon} {bName}",
                11, TextAnchor.MiddleLeft,
                canBuild ? UIFactory.Parchment :
                !unlocked ? new Color(0.4f, 0.4f, 0.4f) : UIFactory.SilverText);
            nameTxt.fontStyle = FontStyle.Bold;
            RectTransform nRT = nameTxt.GetComponent<RectTransform>();
            nRT.anchorMin = new Vector2(0, 0.62f); nRT.anchorMax = new Vector2(1, 1);
            nRT.offsetMin = new Vector2(6, 0); nRT.offsetMax = new Vector2(-4, -2);

            // Effects
            Text effectsTxt = UIFactory.CreateText(inner, "Eff", effects, 8,
                TextAnchor.MiddleLeft, new Color(0.55f, 0.55f, 0.55f));
            RectTransform eRT = effectsTxt.GetComponent<RectTransform>();
            eRT.anchorMin = new Vector2(0, 0.32f); eRT.anchorMax = new Vector2(1, 0.64f);
            eRT.offsetMin = new Vector2(6, 0); eRT.offsetMax = new Vector2(-4, 0);

            // Cost + time + lock status
            string statusStr;
            Color statusColor;
            if (!unlocked)
            {
                statusStr = "🔒 Verrouillé";
                statusColor = new Color(0.45f, 0.35f, 0.35f);
            }
            else if (!prereqMet)
            {
                statusStr = "⚠ Prérequis manquant";
                statusColor = UIFactory.WarningAmber;
            }
            else
            {
                statusStr = $"💰{cost}g  •  {turns}t";
                statusColor = canAfford ? UIFactory.EmpireGold : new Color(0.85f, 0.3f, 0.25f);
            }

            Text costTxt = UIFactory.CreateText(inner, "Cost", statusStr, 9,
                TextAnchor.MiddleLeft, statusColor);
            RectTransform cRT = costTxt.GetComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0, 0); cRT.anchorMax = new Vector2(1, 0.34f);
            cRT.offsetMin = new Vector2(6, 2); cRT.offsetMax = new Vector2(-4, 0);

            // Make clickable if can build
            if (canBuild)
            {
                Button btn = cardRT.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.ColorTint;
                ColorBlock cb = btn.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(1.2f, 1.15f, 1f);
                cb.pressedColor = new Color(0.8f, 0.8f, 0.7f);
                btn.colors = cb;

                string cid = city.cityId;
                BuildingType bt = bType;
                btn.onClick.AddListener(() =>
                {
                    var c = campaignManager.GetCity(cid);
                    if (c != null)
                    {
                        c.StartBuildingConstruction(bt, BuildingInfo.GetCostGold(bt, 0));
                        Refresh();
                    }
                });
            }
        }

        // ==================== UTILITY ====================

        private void Refresh()
        {
            Destroy(gameObject);
            Create(navBar, campaignManager);
        }

        private void BuildEmptyState(Transform parent, string title, string subtitle)
        {
            RectTransform panel = UIFactory.CreatePanel(parent, "Empty", CardMid);
            UIFactory.AddLayoutElement(panel.gameObject, preferredHeight: 44);

            Text t = UIFactory.CreateText(panel, "Title", title, 11, TextAnchor.MiddleCenter, UIFactory.SilverText);
            t.fontStyle = FontStyle.Bold;
            RectTransform tRT = t.GetComponent<RectTransform>();
            tRT.anchorMin = new Vector2(0, 0.5f); tRT.anchorMax = new Vector2(1, 1);
            tRT.offsetMin = new Vector2(12, 0); tRT.offsetMax = new Vector2(-12, 0);

            Text sub = UIFactory.CreateText(panel, "Sub", subtitle, 9, TextAnchor.MiddleCenter, UIFactory.MutedGold);
            RectTransform sRT = sub.GetComponent<RectTransform>();
            sRT.anchorMin = new Vector2(0, 0); sRT.anchorMax = new Vector2(1, 0.5f);
            sRT.offsetMin = new Vector2(12, 0); sRT.offsetMax = new Vector2(-12, 0);
        }

        private Color GetCategoryColor(BuildingCategory cat)
        {
            return cat switch
            {
                BuildingCategory.Economy => EconColor,
                BuildingCategory.Military => MilitaryColor,
                BuildingCategory.Academic => AcademicColor,
                BuildingCategory.Religion => ReligionColor,
                BuildingCategory.Infrastructure => InfraColor,
                _ => UIFactory.SilverText
            };
        }
    }
}
