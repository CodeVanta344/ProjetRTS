using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Campaign;
using NapoleonicWars.Data;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// Premium AAA-style Logistics & Supply panel.
    /// Uses ornate panels, gradient bars, icon badges, glow separators, and premium progress bars.
    /// </summary>
    public class LogisticsUI : MonoBehaviour
    {
        private CampaignManager campaignManager;
        private FactionData playerFaction;
        private NavigationBar navBar;

        // Premium colors
        static readonly Color CardDark     = new Color(0.06f, 0.06f, 0.07f, 0.97f);
        static readonly Color CardMid      = new Color(0.09f, 0.09f, 0.10f, 0.97f);
        static readonly Color ConvoyBlue   = new Color(0.12f, 0.15f, 0.22f, 0.95f);
        static readonly Color RouteGreen   = new Color(0.08f, 0.14f, 0.08f, 0.95f);
        static readonly Color AllyTeal     = new Color(0.06f, 0.12f, 0.16f, 0.95f);
        static readonly Color CritOrange   = new Color(0.85f, 0.5f, 0.1f);
        static readonly Color HealthGreen  = new Color(0.35f, 0.82f, 0.35f);
        static readonly Color HealthYellow = new Color(0.92f, 0.82f, 0.25f);
        static readonly Color HealthRed    = new Color(0.92f, 0.30f, 0.25f);
        static readonly Color DeficitRed   = new Color(0.90f, 0.25f, 0.20f);
        static readonly Color StockGreen   = new Color(0.30f, 0.75f, 0.35f);

        public static LogisticsUI Create(NavigationBar navBar, CampaignManager manager)
        {
            var overlay = navBar.CreateOverlayPanel("LOGISTIQUE & APPROVISIONNEMENT");
            if (overlay == null) return null;

            var ui = overlay.AddComponent<LogisticsUI>();
            ui.navBar = navBar;
            ui.campaignManager = manager;
            ui.playerFaction = manager.GetPlayerFaction();
            navBar.RegisterPanel(NavigationBar.NavPanel.Logistics, overlay);

            ui.BuildContent(overlay.transform);
            return ui;
        }

        // ==================== MAIN BUILD ====================
        private void BuildContent(Transform parent)
        {
            var (scroll, content) = UIFactory.CreateScrollView(parent, "LogScroll");
            UIFactory.AddLayoutElement(scroll.gameObject, flexibleHeight: 1, preferredHeight: 900);

            // ── HERO SUPPLY GAUGE ──
            BuildSupplyHero(content);

            // ── LOGISTICS OVERVIEW STRIP ──
            BuildOverviewStrip(content);

            // ── SEPARATOR ──
            UIFactory.CreateGlowSeparator(content, "Sep1", false);
            UIFactory.AddLayoutElement(content.GetChild(content.childCount - 1).gameObject, preferredHeight: 6);

            // ── CONVOYS IN TRANSIT ──
            BuildConvoySection(content);

            // ── SEPARATOR ──
            UIFactory.CreateGlowSeparator(content, "Sep2", false);
            UIFactory.AddLayoutElement(content.GetChild(content.childCount - 1).gameObject, preferredHeight: 6);

            // ── PERMANENT ROUTES ──
            BuildRouteSection(content);

            // ── SEPARATOR ──
            UIFactory.CreateGlowSeparator(content, "Sep3", false);
            UIFactory.AddLayoutElement(content.GetChild(content.childCount - 1).gameObject, preferredHeight: 6);

            // ── CRITICAL ZONES ──
            BuildCriticalZones(content);

            // ── SEPARATOR ──
            UIFactory.CreateGlowSeparator(content, "Sep4", false);
            UIFactory.AddLayoutElement(content.GetChild(content.childCount - 1).gameObject, preferredHeight: 6);

            // ── EQUIPMENT STATUS ──
            BuildEquipmentSection(content);

            // ── SEPARATOR ──
            UIFactory.CreateGlowSeparator(content, "Sep5", false);
            UIFactory.AddLayoutElement(content.GetChild(content.childCount - 1).gameObject, preferredHeight: 6);

            // ── TRADE ROUTES ──
            BuildTradeSection(content);

            // ── ALLIED PROPOSALS ──
            BuildAlliedSection(content);
        }

        // ==================== 1. SUPPLY HERO ====================
        private void BuildSupplyHero(Transform content)
        {
            float globalSupply = SupplySystem.GetGlobalSupplyHealth();
            string status = globalSupply > 0.75f ? "OPTIMAL" : globalSupply > 0.50f ? "TENDU" : "CRITIQUE";
            Color statusColor = globalSupply > 0.75f ? HealthGreen : globalSupply > 0.50f ? HealthYellow : HealthRed;

            // Gradient hero panel
            RectTransform hero = UIFactory.CreateGradientPanel(content, "SupplyHero",
                new Color(0.14f, 0.13f, 0.11f, 0.98f),
                new Color(0.06f, 0.06f, 0.06f, 0.98f),
                new Color(0.10f, 0.09f, 0.07f, 0.5f));
            UIFactory.AddLayoutElement(hero.gameObject, preferredHeight: 80);

            // Shield emblem
            Text emblem = UIFactory.CreateText(hero, "Emblem", "⚜", 28, TextAnchor.MiddleCenter, UIFactory.EmpireGold);
            RectTransform emblRT = emblem.GetComponent<RectTransform>();
            emblRT.anchorMin = new Vector2(0, 0); emblRT.anchorMax = new Vector2(0.08f, 1);
            emblRT.offsetMin = new Vector2(15, 0); emblRT.offsetMax = Vector2.zero;

            // Title + Status
            Text title = UIFactory.CreateText(hero, "Title", "APPROVISIONNEMENT IMPÉRIAL", 11,
                TextAnchor.UpperLeft, UIFactory.SilverText);
            title.fontStyle = FontStyle.Bold;
            RectTransform titleRT = title.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.09f, 0.55f); titleRT.anchorMax = new Vector2(0.65f, 0.95f);
            titleRT.offsetMin = Vector2.zero; titleRT.offsetMax = Vector2.zero;

            Text pct = UIFactory.CreateText(hero, "Pct", $"{(int)(globalSupply * 100)}%", 32,
                TextAnchor.MiddleLeft, statusColor);
            pct.fontStyle = FontStyle.Bold;
            RectTransform pctRT = pct.GetComponent<RectTransform>();
            pctRT.anchorMin = new Vector2(0.09f, 0.05f); pctRT.anchorMax = new Vector2(0.25f, 0.60f);
            pctRT.offsetMin = Vector2.zero; pctRT.offsetMax = Vector2.zero;
            pct.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.7f);

            // Status badge
            Text badge = UIFactory.CreateText(hero, "Badge", status, 12, TextAnchor.MiddleLeft, statusColor);
            badge.fontStyle = FontStyle.Bold;
            RectTransform badgeRT = badge.GetComponent<RectTransform>();
            badgeRT.anchorMin = new Vector2(0.25f, 0.15f); badgeRT.anchorMax = new Vector2(0.45f, 0.50f);
            badgeRT.offsetMin = Vector2.zero; badgeRT.offsetMax = Vector2.zero;

            // Premium progress bar spanning center-right
            var (bgBar, fillBar, glowBar) = UIFactory.CreatePremiumProgressBar(hero, "SupplyBar", statusColor);
            RectTransform barRT = bgBar.transform.parent.GetComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0.48f, 0.30f); barRT.anchorMax = new Vector2(0.97f, 0.70f);
            barRT.offsetMin = Vector2.zero; barRT.offsetMax = Vector2.zero;
            barRT.sizeDelta = Vector2.zero;
            LayoutElement barLE = barRT.gameObject.AddComponent<LayoutElement>();
            barLE.ignoreLayout = true;
            fillBar.GetComponent<RectTransform>().anchorMax = new Vector2(globalSupply, 1f);
        }

        // ==================== 2. OVERVIEW STRIP ====================
        private void BuildOverviewStrip(Transform content)
        {
            float totalCost = LogisticsConvoySystem.GetTotalMaintenanceCost(playerFaction.factionType);
            int convoyCount = LogisticsConvoySystem.GetConvoyCount(playerFaction.factionType);
            int routeCount = LogisticsConvoySystem.GetRouteCount(playerFaction.factionType);

            // Dark ornate strip
            RectTransform strip = UIFactory.CreateOrnatePanel(content, "OverviewStrip", CardDark);
            UIFactory.AddLayoutElement(strip.gameObject, preferredHeight: 54);

            Transform inner = strip.Find("Inner");
            UIFactory.AddHorizontalLayout(inner?.gameObject ?? strip.gameObject, 6f, new RectOffset(10, 10, 4, 4));

            // Convoy badge
            UIFactory.CreateIconBadge(inner ?? strip, "ConvoyBadge", "🚛",
                $"{convoyCount} Convoi(s)", ConvoyBlue, UIFactory.Parchment, 140f);

            // Route badge
            UIFactory.CreateIconBadge(inner ?? strip, "RouteBadge", "🛤",
                $"{routeCount} Route(s)", RouteGreen, UIFactory.Parchment, 130f);

            // Maintenance badge
            Color costColor = totalCost > 50f ? CritOrange : UIFactory.MutedGold;
            UIFactory.CreateIconBadge(inner ?? strip, "CostBadge", "💰",
                totalCost > 0 ? $"-{totalCost:F0}g/tour" : "0g/tour", costColor, UIFactory.Parchment, 140f);

            // Gold reserve badge
            UIFactory.CreateIconBadge(inner ?? strip, "GoldBadge", "⚜",
                $"{playerFaction.gold:F0}g trésor", UIFactory.EmpireGold, UIFactory.BrightGold, 150f);
        }

        // ==================== 3. CONVOYS IN TRANSIT ====================
        private void BuildConvoySection(Transform content)
        {
            UIFactory.CreateBannerHeader(content, "ConvoyHdr", "🚛  CONVOIS EN TRANSIT", 14);
            UIFactory.AddLayoutElement(content.GetChild(content.childCount - 1).gameObject, preferredHeight: 36);

            var convoys = LogisticsConvoySystem.GetConvoys(playerFaction.factionType)
                .Where(c => c.isActive).ToList();

            if (convoys.Count == 0)
            {
                BuildEmptyState(content, "Aucun convoi en transit", "Créez un convoi depuis le panneau Ville.");
                return;
            }

            foreach (var convoy in convoys)
            {
                CityData origin = campaignManager.GetCity(convoy.originCityId);
                CityData dest = campaignManager.GetCity(convoy.destinationCityId);
                string originName = origin?.cityName ?? convoy.originCityId;
                string destName = dest?.cityName ?? convoy.destinationCityId;
                float progress = convoy.totalSteps > 0 ? (float)convoy.currentStep / convoy.totalSteps : 0f;

                // ─── Ornate card ───
                Color cardBg = convoy.isAlliedConvoy ? AllyTeal : ConvoyBlue;
                RectTransform card = UIFactory.CreateOrnatePanel(content, $"C_{convoy.convoyId}", cardBg);
                UIFactory.AddLayoutElement(card.gameObject, preferredHeight: 95);

                Transform cardInner = card.Find("Inner");
                UIFactory.AddVerticalLayout(cardInner?.gameObject ?? card.gameObject, 3f, new RectOffset(8, 8, 5, 5));

                Transform ci = cardInner ?? card;

                // Row 1: Route title
                GameObject row1 = new GameObject("R1");
                row1.transform.SetParent(ci, false);
                UIFactory.AddHorizontalLayout(row1, 6f);
                UIFactory.AddLayoutElement(row1, preferredHeight: 22);

                Text routeTitle = UIFactory.CreateText(row1.transform, "Route",
                    $"{originName}  ➜  {destName}",
                    13, TextAnchor.MiddleLeft, UIFactory.EmpireGold);
                routeTitle.fontStyle = FontStyle.Bold;
                UIFactory.AddLayoutElement(routeTitle.gameObject, flexibleWidth: 1);

                if (convoy.isAlliedConvoy)
                {
                    Text allyTag = UIFactory.CreateText(row1.transform, "Ally",
                        $"🤝 {convoy.allyFaction}", 10, TextAnchor.MiddleRight, UIFactory.SilverText);
                    UIFactory.AddLayoutElement(allyTag.gameObject, preferredWidth: 100);
                }

                Text stepInfo = UIFactory.CreateText(row1.transform, "Steps",
                    $"Étape {convoy.currentStep}/{convoy.totalSteps}", 10,
                    TextAnchor.MiddleRight, UIFactory.SilverText);
                UIFactory.AddLayoutElement(stepInfo.gameObject, preferredWidth: 80);

                // Row 2: Resource badges
                GameObject row2 = new GameObject("R2");
                row2.transform.SetParent(ci, false);
                UIFactory.AddHorizontalLayout(row2, 8f, new RectOffset(0, 0, 0, 0));
                UIFactory.AddLayoutElement(row2, preferredHeight: 28);

                if (convoy.gold > 0)
                    CreateMiniResourceBadge(row2.transform, "💰", $"{convoy.gold:F0}", new Color(0.85f, 0.72f, 0.30f));
                if (convoy.food > 0)
                    CreateMiniResourceBadge(row2.transform, "🌾", $"{convoy.food:F0}", new Color(0.45f, 0.72f, 0.30f));
                if (convoy.iron > 0)
                    CreateMiniResourceBadge(row2.transform, "⛏", $"{convoy.iron:F0}", new Color(0.55f, 0.60f, 0.70f));
                foreach (var eq in convoy.equipment)
                    if (eq.Value > 0)
                        CreateMiniResourceBadge(row2.transform, "🔫", $"{eq.Value:F0}", new Color(0.60f, 0.45f, 0.30f));

                // Spacer + location
                Text locText = UIFactory.CreateText(row2.transform, "Loc",
                    $"📍 {convoy.currentProvinceId}", 9, TextAnchor.MiddleRight, UIFactory.MutedGold);
                UIFactory.AddLayoutElement(locText.gameObject, flexibleWidth: 1);

                // Row 3: Premium progress bar
                var (bg, fill, glow) = UIFactory.CreatePremiumProgressBar(ci, "Prog", UIFactory.EmpireGold);
                UIFactory.AddLayoutElement(bg.transform.parent.gameObject, preferredHeight: 12);
                fill.GetComponent<RectTransform>().anchorMax = new Vector2(progress, 1f);

                // Row 4: Stats
                Text costs = UIFactory.CreateText(ci, "Cost",
                    $"Maintenance: -{convoy.maintenanceCostPerTurn:F0}g/tour   •   Valeur: {convoy.TotalResourceValue:F0}",
                    9, TextAnchor.MiddleLeft, UIFactory.MutedGold);
                UIFactory.AddLayoutElement(costs.gameObject, preferredHeight: 14);
            }
        }

        // ==================== 4. PERMANENT ROUTES ====================
        private int selectedFromCityIndex = 0;
        private int selectedToCityIndex = 1;

        private void BuildRouteSection(Transform content)
        {
            UIFactory.CreateBannerHeader(content, "RouteHdr", "🛤  CHAÎNES LOGISTIQUES", 14);
            UIFactory.AddLayoutElement(content.GetChild(content.childCount - 1).gameObject, preferredHeight: 36);

            var routes = LogisticsConvoySystem.GetRoutes(playerFaction.factionType)
                .Where(r => r.isActive).ToList();

            if (routes.Count == 0)
            {
                BuildEmptyState(content, "Aucune chaîne logistique", "Créez votre première route ci-dessous.");
            }

            foreach (var route in routes)
            {
                CityData fromCity = campaignManager.GetCity(route.fromCityId);
                CityData toCity = campaignManager.GetCity(route.toCityId);
                string fromName = fromCity?.cityName ?? route.fromCityId;
                string toName = toCity?.cityName ?? route.toCityId;

                Color cardBg = route.isAlliedRoute ? AllyTeal : RouteGreen;
                RectTransform card = UIFactory.CreateOrnatePanel(content, $"R_{route.routeId}", cardBg);
                UIFactory.AddLayoutElement(card.gameObject, preferredHeight: route.level < 3 ? 105 : 82);

                Transform ci = card.Find("Inner") ?? card;
                UIFactory.AddVerticalLayout(ci.gameObject, 2f, new RectOffset(8, 8, 5, 5));

                // Row 1: Title + Level
                GameObject row1 = new GameObject("R1");
                row1.transform.SetParent(ci, false);
                UIFactory.AddHorizontalLayout(row1, 6f);
                UIFactory.AddLayoutElement(row1, preferredHeight: 22);

                Text title = UIFactory.CreateText(row1.transform, "Title",
                    $"{fromName}  ➜  {toName}", 13, TextAnchor.MiddleLeft, UIFactory.EmpireGold);
                title.fontStyle = FontStyle.Bold;
                UIFactory.AddLayoutElement(title.gameObject, flexibleWidth: 1);

                if (route.isAlliedRoute)
                {
                    Text allyTag = UIFactory.CreateText(row1.transform, "Ally",
                        $"🤝 {route.allyFaction}", 10, TextAnchor.MiddleRight, UIFactory.SilverText);
                    UIFactory.AddLayoutElement(allyTag.gameObject, preferredWidth: 90);
                }

                // Level badge — styled pill
                string lvlStr = route.level switch { 1 => "I", 2 => "II", 3 => "III", _ => "?" };
                Color lvlColor = route.level switch
                {
                    1 => UIFactory.SilverText,
                    2 => UIFactory.MutedGold,
                    3 => UIFactory.BrightGold,
                    _ => Color.white
                };
                RectTransform lvlPanel = UIFactory.CreatePanel(row1.transform, "LvlBadge",
                    new Color(lvlColor.r * 0.2f, lvlColor.g * 0.2f, lvlColor.b * 0.2f, 0.9f));
                UIFactory.AddLayoutElement(lvlPanel.gameObject, preferredWidth: 44, preferredHeight: 20);
                Text lvlText = UIFactory.CreateText(lvlPanel, "Lvl", $"Nv.{lvlStr}", 10,
                    TextAnchor.MiddleCenter, lvlColor);
                lvlText.fontStyle = FontStyle.Bold;
                UIFactory.SetAnchors(lvlText.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                // Row 2: Stats badges
                GameObject row2 = new GameObject("R2");
                row2.transform.SetParent(ci, false);
                UIFactory.AddHorizontalLayout(row2, 10f);
                UIFactory.AddLayoutElement(row2, preferredHeight: 26);

                CreateMiniResourceBadge(row2.transform, "📦", $"{route.maxCapacityPerTurn:F0}/tour", new Color(0.5f, 0.7f, 0.4f));
                CreateMiniResourceBadge(row2.transform, "📍", $"{route.routePath.Count} prov.", new Color(0.5f, 0.6f, 0.7f));
                CreateMiniResourceBadge(row2.transform, "💰", $"-{route.maintenanceCost:F0}g", CritOrange);

                // Row 3: Auto-dispatch info
                float totalAuto = route.autoGoldPerTurn + route.autoFoodPerTurn + route.autoIronPerTurn;
                if (totalAuto > 0)
                {
                    GameObject row3 = new GameObject("R3");
                    row3.transform.SetParent(ci, false);
                    UIFactory.AddHorizontalLayout(row3, 6f);
                    UIFactory.AddLayoutElement(row3, preferredHeight: 20);

                    Text autoLabel = UIFactory.CreateText(row3.transform, "AutoLbl", "AUTO :",
                        9, TextAnchor.MiddleLeft, UIFactory.SilverText);
                    autoLabel.fontStyle = FontStyle.Bold;
                    UIFactory.AddLayoutElement(autoLabel.gameObject, preferredWidth: 45);

                    if (route.autoGoldPerTurn > 0)
                        CreateMiniResourceBadge(row3.transform, "💰", $"{route.autoGoldPerTurn:F0}", new Color(0.85f, 0.72f, 0.30f));
                    if (route.autoFoodPerTurn > 0)
                        CreateMiniResourceBadge(row3.transform, "🌾", $"{route.autoFoodPerTurn:F0}", new Color(0.45f, 0.72f, 0.30f));
                    if (route.autoIronPerTurn > 0)
                        CreateMiniResourceBadge(row3.transform, "⛏", $"{route.autoIronPerTurn:F0}", new Color(0.55f, 0.60f, 0.70f));
                }

                // Row 4: Upgrade button (if not max)
                if (route.level < 3)
                {
                    int upgGold = LogisticsRoute.GetUpgradeCostGold(route.level);
                    int upgIron = LogisticsRoute.GetUpgradeCostIron(route.level);
                    bool canUpgrade = playerFaction.gold >= upgGold && playerFaction.iron >= upgIron;
                    string nextLvl = (route.level + 1) switch { 2 => "II", 3 => "III", _ => "?" };
                    string routeId = route.routeId;

                    Button upgBtn = UIFactory.CreateButton(ci, "Upgrade",
                        canUpgrade
                            ? $"⬆  AMÉLIORER → Nv.{nextLvl}   (💰{upgGold}  ⛏{upgIron})"
                            : $"⬆  AMÉLIORER → Nv.{nextLvl}   (💰{upgGold}  ⛏{upgIron})  —  FONDS INSUFFISANTS",
                        10, () =>
                        {
                            var r = LogisticsConvoySystem.GetRoutes(playerFaction.factionType)
                                .Find(x => x.routeId == routeId);
                            if (r != null && r.Upgrade(playerFaction))
                            {
                                Destroy(gameObject);
                                Create(navBar, campaignManager);
                            }
                        });
                    UIFactory.AddLayoutElement(upgBtn.gameObject, preferredHeight: 26);

                    Image btnImg = upgBtn.GetComponent<Image>();
                    btnImg.color = canUpgrade ? new Color(0.12f, 0.10f, 0.06f) : new Color(0.06f, 0.06f, 0.06f);
                    Text btnText = upgBtn.GetComponentInChildren<Text>();
                    if (btnText != null)
                        btnText.color = canUpgrade ? UIFactory.EmpireGold : new Color(0.35f, 0.35f, 0.35f);
                }
            }

            // ===== CREATION FORM — always shown =====
            BuildRouteCreationForm(content);
        }

        private void BuildRouteCreationForm(Transform content)
        {
            List<CityData> playerCities = campaignManager.GetFactionCities(playerFaction.factionType);
            if (playerCities == null || playerCities.Count < 2) 
            {
                BuildEmptyState(content, "Villes insuffisantes", "Il faut au moins 2 villes pour créer une chaîne logistique.");
                return;
            }

            // Clamp indices
            selectedFromCityIndex = Mathf.Clamp(selectedFromCityIndex, 0, playerCities.Count - 1);
            selectedToCityIndex = Mathf.Clamp(selectedToCityIndex, 0, playerCities.Count - 1);
            if (selectedToCityIndex == selectedFromCityIndex)
                selectedToCityIndex = (selectedFromCityIndex + 1) % playerCities.Count;

            CityData fromCity = playerCities[selectedFromCityIndex];
            CityData toCity = playerCities[selectedToCityIndex];

            // --- Card Container ---
            Color createCardBg = new Color(0.07f, 0.09f, 0.06f, 0.95f);
            RectTransform createCard = UIFactory.CreateOrnatePanel(content, "RouteCreate", createCardBg);
            UIFactory.AddLayoutElement(createCard.gameObject, preferredHeight: 175);

            Transform ci = createCard.Find("Inner") ?? createCard;
            UIFactory.AddVerticalLayout(ci.gameObject, 4f, new RectOffset(10, 10, 8, 8));

            // --- Title ---
            Text formTitle = UIFactory.CreateText(ci, "FormTitle",
                "➕  CRÉER UNE CHAÎNE LOGISTIQUE", 12, TextAnchor.MiddleCenter, UIFactory.EmpireGold);
            formTitle.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(formTitle.gameObject, preferredHeight: 22);

            // --- Row: FROM / TO ---
            GameObject selectorRow = new GameObject("SelectorRow");
            selectorRow.transform.SetParent(ci, false);
            UIFactory.AddHorizontalLayout(selectorRow, 8f);
            UIFactory.AddLayoutElement(selectorRow, preferredHeight: 50);

            // FROM city selector
            BuildCitySelector(selectorRow.transform, "DÉPART", fromCity.cityName, () =>
            {
                selectedFromCityIndex = (selectedFromCityIndex + 1) % playerCities.Count;
                if (selectedFromCityIndex == selectedToCityIndex)
                    selectedFromCityIndex = (selectedFromCityIndex + 1) % playerCities.Count;
                Destroy(gameObject);
                Create(navBar, campaignManager);
            });

            // Arrow
            Text arrow = UIFactory.CreateText(selectorRow.transform, "Arrow",
                "➜", 20, TextAnchor.MiddleCenter, UIFactory.EmpireGold);
            UIFactory.AddLayoutElement(arrow.gameObject, preferredWidth: 30);

            // TO city selector
            BuildCitySelector(selectorRow.transform, "ARRIVÉE", toCity.cityName, () =>
            {
                selectedToCityIndex = (selectedToCityIndex + 1) % playerCities.Count;
                if (selectedToCityIndex == selectedFromCityIndex)
                    selectedToCityIndex = (selectedToCityIndex + 1) % playerCities.Count;
                Destroy(gameObject);
                Create(navBar, campaignManager);
            });

            // --- Route Info Preview ---
            List<string> path = LogisticsConvoySystem.FindPath(
                fromCity.provinceId, toCity.provinceId, playerFaction.factionType, campaignManager.Provinces);
            bool pathFound = path != null && path.Count > 0;
            int setupCost = pathFound ? 100 + path.Count * 20 : 0;
            bool canAfford = playerFaction.gold >= setupCost;

            GameObject infoRow = new GameObject("InfoRow");
            infoRow.transform.SetParent(ci, false);
            UIFactory.AddHorizontalLayout(infoRow, 12f);
            UIFactory.AddLayoutElement(infoRow, preferredHeight: 24);

            if (pathFound)
            {
                CreateMiniResourceBadge(infoRow.transform, "📍", $"{path.Count} provinces", new Color(0.5f, 0.6f, 0.7f));
                CreateMiniResourceBadge(infoRow.transform, "💰", $"Coût: {setupCost}g", 
                    canAfford ? new Color(0.85f, 0.72f, 0.30f) : DeficitRed);
                CreateMiniResourceBadge(infoRow.transform, "📦", $"{LogisticsRoute.GetCapacity(1):F0}/tour", new Color(0.5f, 0.7f, 0.4f));
                CreateMiniResourceBadge(infoRow.transform, "💰", $"-{LogisticsRoute.GetMaintenance(1, path.Count):F0}g/tour", CritOrange);
            }
            else
            {
                Text noPath = UIFactory.CreateText(infoRow.transform, "NoPath",
                    "⚠  Aucun chemin trouvé entre ces villes", 10, TextAnchor.MiddleCenter, DeficitRed);
                UIFactory.AddLayoutElement(noPath.gameObject, flexibleWidth: 1);
            }

            // --- Create Button ---
            string btnLabel;
            bool canCreate;
            if (!pathFound)
            {
                btnLabel = "⚠  ROUTE IMPOSSIBLE";
                canCreate = false;
            }
            else if (!canAfford)
            {
                btnLabel = $"💰  FONDS INSUFFISANTS ({setupCost}g requis, {playerFaction.gold:F0}g disponibles)";
                canCreate = false;
            }
            else
            {
                btnLabel = $"✅  CRÉER LA ROUTE — {setupCost}g";
                canCreate = true;
            }

            Button createBtn = UIFactory.CreateButton(ci, "CreateRouteBtn", btnLabel, 11, () =>
            {
                if (!canCreate) return;
                
                var result = LogisticsConvoySystem.CreateRoute(fromCity, toCity, playerFaction.factionType, campaignManager.Provinces);
                if (result != null)
                {
                    Debug.Log($"[LogisticsUI] Created route: {fromCity.cityName} → {toCity.cityName}");
                    // Rebuild panel to show new route
                    Destroy(gameObject);
                    Create(navBar, campaignManager);
                }
            });
            UIFactory.AddLayoutElement(createBtn.gameObject, preferredHeight: 30);

            Image cBtnImg = createBtn.GetComponent<Image>();
            cBtnImg.color = canCreate ? new Color(0.12f, 0.16f, 0.08f) : new Color(0.06f, 0.06f, 0.06f);
            Text cBtnText = createBtn.GetComponentInChildren<Text>();
            if (cBtnText != null)
                cBtnText.color = canCreate ? UIFactory.EmpireGold : new Color(0.40f, 0.30f, 0.25f);
        }

        private void BuildCitySelector(Transform parent, string label, string cityName, System.Action onCycle)
        {
            GameObject container = new GameObject($"CitySelector_{label}");
            container.transform.SetParent(parent, false);
            UIFactory.AddVerticalLayout(container, 2f);
            UIFactory.AddLayoutElement(container, flexibleWidth: 1, preferredHeight: 48);

            // Label
            Text lbl = UIFactory.CreateText(container.transform, "Label", label,
                9, TextAnchor.MiddleCenter, UIFactory.SilverText);
            lbl.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(lbl.gameObject, preferredHeight: 14);

            // City name button (click to cycle)
            Button cityBtn = UIFactory.CreateButton(container.transform, "CityBtn", $"🏰  {cityName}", 12, () =>
            {
                onCycle?.Invoke();
            });
            UIFactory.AddLayoutElement(cityBtn.gameObject, preferredHeight: 30, flexibleWidth: 1);

            Image btnImg = cityBtn.GetComponent<Image>();
            btnImg.color = new Color(0.10f, 0.09f, 0.06f);

            Text btnText = cityBtn.GetComponentInChildren<Text>();
            if (btnText != null)
            {
                btnText.color = UIFactory.BrightGold;
                btnText.fontStyle = FontStyle.Bold;
            }
        }

        // ==================== 5. CRITICAL ZONES ====================
        private void BuildCriticalZones(Transform content)
        {
            UIFactory.CreateBannerHeader(content, "CritHdr", "⚠  ZONES CRITIQUES", 14);
            UIFactory.AddLayoutElement(content.GetChild(content.childCount - 1).gameObject, preferredHeight: 36);

            var critical = SupplySystem.GetCriticalProvinces(0.50f);
            if (critical.Count == 0)
            {
                BuildEmptyState(content, "Aucune zone critique", "Approvisionnement nominal sur tous les fronts.", HealthGreen);
                return;
            }

            foreach (var prov in critical)
            {
                float ratio = prov.SupplyRatio;
                Color barColor = ratio < 0.25f ? HealthRed : HealthYellow;
                string reason = prov.infrastructureLevel <= 2 ? "Infrastructure insuffisante" : "Surcharge d'unités";

                RectTransform row = UIFactory.CreatePanel(content, $"Crit_{prov.provinceId}", CardDark);
                UIFactory.AddLayoutElement(row.gameObject, preferredHeight: 36);

                // Province name
                Text name = UIFactory.CreateText(row, "Name", $"⚠ {prov.provinceId}",
                    11, TextAnchor.MiddleLeft, CritOrange);
                name.fontStyle = FontStyle.Bold;
                RectTransform nameRT = name.GetComponent<RectTransform>();
                nameRT.anchorMin = new Vector2(0, 0); nameRT.anchorMax = new Vector2(0.35f, 1);
                nameRT.offsetMin = new Vector2(12, 0); nameRT.offsetMax = Vector2.zero;

                // Reason
                Text reasonTxt = UIFactory.CreateText(row, "Reason", reason,
                    9, TextAnchor.MiddleLeft, UIFactory.SilverText);
                RectTransform reasonRT = reasonTxt.GetComponent<RectTransform>();
                reasonRT.anchorMin = new Vector2(0.36f, 0); reasonRT.anchorMax = new Vector2(0.60f, 1);
                reasonRT.offsetMin = Vector2.zero; reasonRT.offsetMax = Vector2.zero;

                // Mini progress bar
                var (bg, fill, glow) = UIFactory.CreatePremiumProgressBar(row, "Bar", barColor);
                RectTransform barRT = bg.transform.parent.GetComponent<RectTransform>();
                barRT.anchorMin = new Vector2(0.62f, 0.25f); barRT.anchorMax = new Vector2(0.88f, 0.75f);
                barRT.offsetMin = Vector2.zero; barRT.offsetMax = Vector2.zero;
                barRT.sizeDelta = Vector2.zero;
                barRT.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                fill.GetComponent<RectTransform>().anchorMax = new Vector2(ratio, 1f);

                // Percentage
                Text pctTxt = UIFactory.CreateText(row, "Pct", $"{(int)(ratio * 100)}%",
                    11, TextAnchor.MiddleRight, barColor);
                pctTxt.fontStyle = FontStyle.Bold;
                RectTransform pctRT = pctTxt.GetComponent<RectTransform>();
                pctRT.anchorMin = new Vector2(0.90f, 0); pctRT.anchorMax = new Vector2(0.98f, 1);
                pctRT.offsetMin = Vector2.zero; pctRT.offsetMax = Vector2.zero;
            }
        }

        // ==================== 6. EQUIPMENT STATUS ====================
        private void BuildEquipmentSection(Transform content)
        {
            UIFactory.CreateBannerHeader(content, "EquipHdr", "🔧  ÉQUIPEMENT MILITAIRE", 14);
            UIFactory.AddLayoutElement(content.GetChild(content.childCount - 1).gameObject, preferredHeight: 36);

            Dictionary<string, ArmyData> armies = campaignManager.Armies ?? new Dictionary<string, ArmyData>();
            var summary = ProductionManager.GetEquipmentSummary(playerFaction, armies);
            bool hasDeficit = false;

            foreach (var kvp in summary)
            {
                var (stock, demand, production) = kvp.Value;
                if (demand <= 0) continue;

                bool isDeficit = stock < demand;
                if (isDeficit) hasDeficit = true;
                float ratio = demand > 0 ? Mathf.Clamp01(stock / demand) : 1f;
                Color barCol = isDeficit ? (stock < demand * 0.5f ? DeficitRed : HealthYellow) : StockGreen;

                RectTransform row = UIFactory.CreatePanel(content, $"Eq_{kvp.Key}", CardDark);
                UIFactory.AddLayoutElement(row.gameObject, preferredHeight: 34);

                // Equipment name
                Text eqName = UIFactory.CreateText(row, "Name",
                    ProductionUI.GetEquipmentName(kvp.Key), 11, TextAnchor.MiddleLeft,
                    isDeficit ? CritOrange : UIFactory.Parchment);
                eqName.fontStyle = FontStyle.Bold;
                RectTransform eqNameRT = eqName.GetComponent<RectTransform>();
                eqNameRT.anchorMin = new Vector2(0, 0); eqNameRT.anchorMax = new Vector2(0.25f, 1);
                eqNameRT.offsetMin = new Vector2(12, 0); eqNameRT.offsetMax = Vector2.zero;

                // Stock/Demand
                Text stockTxt = UIFactory.CreateText(row, "Stock", $"{stock:F0}/{demand:F0}",
                    10, TextAnchor.MiddleCenter, barCol);
                RectTransform stockRT = stockTxt.GetComponent<RectTransform>();
                stockRT.anchorMin = new Vector2(0.26f, 0); stockRT.anchorMax = new Vector2(0.40f, 1);
                stockRT.offsetMin = Vector2.zero; stockRT.offsetMax = Vector2.zero;

                // Progress bar
                var (bg, fill, glow) = UIFactory.CreatePremiumProgressBar(row, "Bar", barCol);
                RectTransform barRT = bg.transform.parent.GetComponent<RectTransform>();
                barRT.anchorMin = new Vector2(0.42f, 0.25f); barRT.anchorMax = new Vector2(0.78f, 0.75f);
                barRT.offsetMin = Vector2.zero; barRT.offsetMax = Vector2.zero;
                barRT.sizeDelta = Vector2.zero;
                barRT.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                fill.GetComponent<RectTransform>().anchorMax = new Vector2(ratio, 1f);

                // Production info
                string prodStr = production > 0 ? $"+{production:F0}/tour" : "—";
                Text prodTxt = UIFactory.CreateText(row, "Prod", prodStr,
                    10, TextAnchor.MiddleRight, production > 0 ? StockGreen : UIFactory.SilverText);
                RectTransform prodRT = prodTxt.GetComponent<RectTransform>();
                prodRT.anchorMin = new Vector2(0.80f, 0); prodRT.anchorMax = new Vector2(0.98f, 1);
                prodRT.offsetMin = Vector2.zero; prodRT.offsetMax = Vector2.zero;
            }

            if (!hasDeficit)
            {
                BuildEmptyState(content, "Stock complet", "Tous les équipements sont en quantité suffisante.", StockGreen);
            }
        }

        // ==================== 7. TRADE ROUTES ====================
        private void BuildTradeSection(Transform content)
        {
            UIFactory.CreateBannerHeader(content, "TradeHdr", "🔗  ROUTES COMMERCIALES", 14);
            UIFactory.AddLayoutElement(content.GetChild(content.childCount - 1).gameObject, preferredHeight: 36);

            bool hasRoutes = false;
            if (TradeSystem.Instance != null)
            {
                foreach (var route in TradeSystem.Instance.TradeRoutes.Values)
                {
                    if (!route.isActive) continue;
                    if (route.fromFaction != playerFaction.factionType && route.toFaction != playerFaction.factionType) continue;
                    hasRoutes = true;

                    string fromCity = route.fromCityId ?? "?";
                    string toCity = route.toCityId ?? "?";
                    string seaTag = route.isSeaRoute ? "  🚢" : "";

                    RectTransform row = UIFactory.CreatePanel(content, $"TR_{route.routeId}", CardDark);
                    UIFactory.AddLayoutElement(row.gameObject, preferredHeight: 30);

                    Text line = UIFactory.CreateText(row, "Line",
                        $"    {fromCity}  ➜  {toCity}{seaTag}  •  +{route.goldPerTurn:F0}g/tour ({route.resource})",
                        11, TextAnchor.MiddleLeft, UIFactory.Parchment);
                    UIFactory.SetAnchors(line.gameObject, Vector2.zero, Vector2.one,
                        new Vector2(8, 0), new Vector2(-8, 0));
                }

                foreach (var agreement in TradeSystem.Instance.TradeAgreements.Values)
                {
                    if (!agreement.isActive) continue;
                    if (agreement.faction1 != playerFaction.factionType && agreement.faction2 != playerFaction.factionType) continue;
                    hasRoutes = true;

                    FactionType partner = agreement.faction1 == playerFaction.factionType ? agreement.faction2 : agreement.faction1;
                    float income = agreement.faction1 == playerFaction.factionType ? agreement.goldPerTurnFaction1 : agreement.goldPerTurnFaction2;

                    RectTransform row = UIFactory.CreatePanel(content, $"TA_{agreement.agreementId}", CardDark);
                    UIFactory.AddLayoutElement(row.gameObject, preferredHeight: 30);

                    Text line = UIFactory.CreateText(row, "Line",
                        $"    🤝 Accord avec {CampaignManager.GetFactionSubtitle(partner)}  •  +{income:F0}g/tour",
                        11, TextAnchor.MiddleLeft, StockGreen);
                    UIFactory.SetAnchors(line.gameObject, Vector2.zero, Vector2.one,
                        new Vector2(8, 0), new Vector2(-8, 0));
                }
            }

            if (!hasRoutes)
            {
                BuildEmptyState(content, "Aucune route commerciale",
                    "Établissez des accords diplomatiques ou construisez des comptoirs.");
            }
        }

        // ==================== 8. ALLIED PROPOSALS ====================
        private void BuildAlliedSection(Transform content)
        {
            var pending = LogisticsConvoySystem.GetPendingAgreements(playerFaction.factionType);
            if (pending.Count == 0) return;

            UIFactory.CreateGlowSeparator(content, "SepAlly", false);
            UIFactory.AddLayoutElement(content.GetChild(content.childCount - 1).gameObject, preferredHeight: 6);

            UIFactory.CreateBannerHeader(content, "AllyHdr", "📜  PROPOSITIONS ALLIÉES", 14);
            UIFactory.AddLayoutElement(content.GetChild(content.childCount - 1).gameObject, preferredHeight: 36);

            foreach (var agreement in pending)
            {
                RectTransform card = UIFactory.CreateOrnatePanel(content, $"A_{agreement.agreementId}", AllyTeal);
                UIFactory.AddLayoutElement(card.gameObject, preferredHeight: 85);

                Transform ci = card.Find("Inner") ?? card;
                UIFactory.AddVerticalLayout(ci.gameObject, 3f, new RectOffset(10, 10, 6, 6));

                Text title = UIFactory.CreateText(ci, "Title",
                    $"📜 Proposition de {CampaignManager.GetFactionSubtitle(agreement.proposer)}",
                    13, TextAnchor.MiddleLeft, UIFactory.EmpireGold);
                title.fontStyle = FontStyle.Bold;
                UIFactory.AddLayoutElement(title.gameObject, preferredHeight: 22);

                // Offer badges
                GameObject offerRow = new GameObject("Offer");
                offerRow.transform.SetParent(ci, false);
                UIFactory.AddHorizontalLayout(offerRow, 8f);
                UIFactory.AddLayoutElement(offerRow, preferredHeight: 26);

                Text offerLbl = UIFactory.CreateText(offerRow.transform, "Lbl", "OFFRE :",
                    9, TextAnchor.MiddleLeft, UIFactory.SilverText);
                offerLbl.fontStyle = FontStyle.Bold;
                UIFactory.AddLayoutElement(offerLbl.gameObject, preferredWidth: 50);

                if (agreement.goldPerTurn > 0)
                    CreateMiniResourceBadge(offerRow.transform, "💰", $"{agreement.goldPerTurn:F0}/t", new Color(0.85f, 0.72f, 0.30f));
                if (agreement.foodPerTurn > 0)
                    CreateMiniResourceBadge(offerRow.transform, "🌾", $"{agreement.foodPerTurn:F0}/t", new Color(0.45f, 0.72f, 0.30f));
                if (agreement.ironPerTurn > 0)
                    CreateMiniResourceBadge(offerRow.transform, "⛏", $"{agreement.ironPerTurn:F0}/t", new Color(0.55f, 0.60f, 0.70f));

                // Accept / Reject buttons
                GameObject btnRow = new GameObject("Btns");
                btnRow.transform.SetParent(ci, false);
                UIFactory.AddHorizontalLayout(btnRow, 12f);
                UIFactory.AddLayoutElement(btnRow, preferredHeight: 26);

                string agrId = agreement.agreementId;

                Button acceptBtn = UIFactory.CreateButton(btnRow.transform, "Accept", "✓  ACCEPTER", 11, () =>
                {
                    LogisticsConvoySystem.RespondToAgreement(agrId, true, campaignManager.Provinces);
                    Destroy(gameObject);
                    Create(navBar, campaignManager);
                });
                UIFactory.AddLayoutElement(acceptBtn.gameObject, preferredWidth: 140, preferredHeight: 26);
                acceptBtn.GetComponent<Image>().color = new Color(0.08f, 0.18f, 0.08f);
                Text accTxt = acceptBtn.GetComponentInChildren<Text>();
                if (accTxt != null) accTxt.color = StockGreen;

                Button rejectBtn = UIFactory.CreateButton(btnRow.transform, "Reject", "✕  REFUSER", 11, () =>
                {
                    LogisticsConvoySystem.RespondToAgreement(agrId, false, campaignManager.Provinces);
                    Destroy(gameObject);
                    Create(navBar, campaignManager);
                });
                UIFactory.AddLayoutElement(rejectBtn.gameObject, preferredWidth: 140, preferredHeight: 26);
                rejectBtn.GetComponent<Image>().color = new Color(0.18f, 0.06f, 0.06f);
                Text rejTxt = rejectBtn.GetComponentInChildren<Text>();
                if (rejTxt != null) rejTxt.color = DeficitRed;
            }
        }

        // ==================== HELPERS ====================

        private void BuildEmptyState(Transform parent, string title, string subtitle, Color? titleColor = null)
        {
            RectTransform panel = UIFactory.CreatePanel(parent, "Empty", CardMid);
            UIFactory.AddLayoutElement(panel.gameObject, preferredHeight: 48);

            Text t = UIFactory.CreateText(panel, "Title", title, 11, TextAnchor.MiddleCenter,
                titleColor ?? UIFactory.SilverText);
            t.fontStyle = FontStyle.Bold;
            RectTransform tRT = t.GetComponent<RectTransform>();
            tRT.anchorMin = new Vector2(0, 0.5f); tRT.anchorMax = new Vector2(1, 1);
            tRT.offsetMin = new Vector2(12, 0); tRT.offsetMax = new Vector2(-12, 0);

            Text sub = UIFactory.CreateText(panel, "Sub", subtitle, 9, TextAnchor.MiddleCenter, UIFactory.MutedGold);
            RectTransform sRT = sub.GetComponent<RectTransform>();
            sRT.anchorMin = new Vector2(0, 0); sRT.anchorMax = new Vector2(1, 0.5f);
            sRT.offsetMin = new Vector2(12, 0); sRT.offsetMax = new Vector2(-12, 0);
        }

        private void CreateMiniResourceBadge(Transform parent, string icon, string value, Color color)
        {
            GameObject badge = new GameObject("Badge");
            badge.transform.SetParent(parent, false);
            UIFactory.AddHorizontalLayout(badge, 3f, new RectOffset(4, 6, 2, 2));
            UIFactory.AddLayoutElement(badge, preferredHeight: 22);

            // Tiny icon bg
            RectTransform iconBg = UIFactory.CreatePanel(badge.transform, "IBg",
                new Color(color.r * 0.2f, color.g * 0.2f, color.b * 0.2f, 0.85f));
            UIFactory.AddLayoutElement(iconBg.gameObject, preferredWidth: 20, preferredHeight: 18);
            Text iconTxt = UIFactory.CreateText(iconBg, "I", icon, 10, TextAnchor.MiddleCenter, color);
            UIFactory.SetAnchors(iconTxt.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Value
            Text val = UIFactory.CreateText(badge.transform, "V", value, 10, TextAnchor.MiddleLeft, UIFactory.Parchment);
            val.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(val.gameObject, preferredWidth: 60);
        }

        private void CreateLogSectionLabel(Transform parent, string title)
        {
            RectTransform hdrRT = UIFactory.CreatePanel(parent, $"Hdr_{title.GetHashCode()}",
                new Color(0.16f, 0.18f, 0.15f, 0.95f));
            UIFactory.AddLayoutElement(hdrRT.gameObject, preferredHeight: 28);
            Text hdrText = UIFactory.CreateText(hdrRT, "Text", title, 13,
                TextAnchor.MiddleLeft, UIFactory.GoldAccent);
            hdrText.fontStyle = FontStyle.Bold;
            RectTransform hdrTextRT = hdrText.GetComponent<RectTransform>();
            hdrTextRT.anchorMin = Vector2.zero; hdrTextRT.anchorMax = Vector2.one;
            hdrTextRT.offsetMin = new Vector2(15, 0); hdrTextRT.offsetMax = new Vector2(-15, 0);
        }
    }
}
