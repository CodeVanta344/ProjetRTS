using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Campaign;
using NapoleonicWars.Data;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// Logistics & Supply panel — clean, card-based, readable layout.
    /// Inspired by Victoria 3 / HOI4 supply screens.
    /// </summary>
    public class LogisticsUI : MonoBehaviour
    {
        private CampaignManager campaignManager;
        private FactionData playerFaction;
        private NavigationBar navBar;

        // Colors
        static readonly Color RowDark      = new Color(0.065f, 0.065f, 0.075f, 1f);
        static readonly Color RowAlt       = new Color(0.08f, 0.08f, 0.09f, 1f);
        static readonly Color StatBox      = new Color(0.055f, 0.06f, 0.07f, 1f);
        static readonly Color GreenOk      = new Color(0.30f, 0.78f, 0.30f);
        static readonly Color YellowWarn   = new Color(0.92f, 0.78f, 0.22f);
        static readonly Color OrangeAlert  = new Color(0.88f, 0.55f, 0.15f);
        static readonly Color RedCrit      = new Color(0.90f, 0.25f, 0.20f);
        static readonly Color ConvoyBlue   = new Color(0.35f, 0.55f, 0.85f);
        static readonly Color RouteGreen   = new Color(0.35f, 0.70f, 0.40f);
        static readonly Color AllyTeal     = new Color(0.30f, 0.70f, 0.75f);

        // ==================== FACTORY ====================
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

            // 1. Supply health summary
            BuildSupplyHeader(content);

            // 2. Key stats row
            BuildStatsRow(content);

            // 3. Convoys
            AddSectionGap(content);
            BuildConvoySection(content);

            // 4. Routes
            AddSectionGap(content);
            BuildRouteSection(content);

            // 5. Critical zones
            AddSectionGap(content);
            BuildCriticalZones(content);

            // 6. Equipment
            AddSectionGap(content);
            BuildEquipmentSection(content);

            // 7. Trade
            AddSectionGap(content);
            BuildTradeSection(content);

            // 8. Allied proposals
            BuildAlliedSection(content);

            // Bottom padding
            AddSectionGap(content);
        }

        // ==================== 1. SUPPLY STATUS HEADER ====================
        private void BuildSupplyHeader(Transform content)
        {
            float supply = SupplySystem.GetGlobalSupplyHealth();
            int pct = (int)(supply * 100f);
            string status = pct > 75 ? "OPTIMAL" : pct > 50 ? "TENDU" : pct > 25 ? "PRÉCAIRE" : "CRITIQUE";
            Color statusColor = pct > 75 ? GreenOk : pct > 50 ? YellowWarn : pct > 25 ? OrangeAlert : RedCrit;

            // Container
            RectTransform header = UIFactory.CreatePanel(content, "SupplyHeader", new Color(0.07f, 0.075f, 0.085f, 1f));
            UIFactory.AddLayoutElement(header.gameObject, preferredHeight: 60);

            // Left: label
            Text label = UIFactory.CreateText(header, "Label", "APPROVISIONNEMENT GÉNÉRAL", 10,
                TextAnchor.UpperLeft, UIFactory.SilverText);
            label.fontStyle = FontStyle.Bold;
            RectTransform labelRT = label.GetComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0, 0.55f); labelRT.anchorMax = new Vector2(0.35f, 0.95f);
            labelRT.offsetMin = new Vector2(16, 0); labelRT.offsetMax = Vector2.zero;

            // Left: percentage
            Text pctText = UIFactory.CreateText(header, "Pct", $"{pct}%", 26,
                TextAnchor.MiddleLeft, statusColor);
            pctText.fontStyle = FontStyle.Bold;
            RectTransform pctRT = pctText.GetComponent<RectTransform>();
            pctRT.anchorMin = new Vector2(0, 0.05f); pctRT.anchorMax = new Vector2(0.12f, 0.60f);
            pctRT.offsetMin = new Vector2(16, 0); pctRT.offsetMax = Vector2.zero;

            // Status badge
            Text statusText = UIFactory.CreateText(header, "Status", status, 11,
                TextAnchor.MiddleLeft, statusColor);
            statusText.fontStyle = FontStyle.Bold;
            RectTransform statRT = statusText.GetComponent<RectTransform>();
            statRT.anchorMin = new Vector2(0.12f, 0.10f); statRT.anchorMax = new Vector2(0.35f, 0.55f);
            statRT.offsetMin = Vector2.zero; statRT.offsetMax = Vector2.zero;

            // Progress bar — right side
            var (barBg, barFill) = UIFactory.CreateProgressBar(header, "SupplyBar", statusColor);
            RectTransform barRT = barBg.GetComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0.38f, 0.25f); barRT.anchorMax = new Vector2(0.97f, 0.75f);
            barRT.offsetMin = Vector2.zero; barRT.offsetMax = Vector2.zero;
            barRT.sizeDelta = Vector2.zero;
            barBg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            barFill.GetComponent<RectTransform>().anchorMax = new Vector2(supply, 1f);
        }

        // ==================== 2. KEY STATS ROW ====================
        private void BuildStatsRow(Transform content)
        {
            float totalCost = LogisticsConvoySystem.GetTotalMaintenanceCost(playerFaction.factionType);
            int convoyCount = LogisticsConvoySystem.GetConvoyCount(playerFaction.factionType);
            int routeCount = LogisticsConvoySystem.GetRouteCount(playerFaction.factionType);

            // Row container
            GameObject row = new GameObject("StatsRow");
            row.transform.SetParent(content, false);
            UIFactory.AddHorizontalLayout(row, 3f, new RectOffset(4, 4, 0, 0));
            UIFactory.AddLayoutElement(row, preferredHeight: 50);

            CreateStatBox(row.transform, "CONVOIS", $"{convoyCount}", ConvoyBlue);
            CreateStatBox(row.transform, "ROUTES", $"{routeCount}", RouteGreen);
            CreateStatBox(row.transform, "MAINTENANCE", totalCost > 0 ? $"-{totalCost:F0}g" : "0g",
                totalCost > 50f ? OrangeAlert : UIFactory.SilverText);
            CreateStatBox(row.transform, "TRÉSOR", $"{playerFaction.gold:F0}g", UIFactory.EmpireGold);
        }

        private void CreateStatBox(Transform parent, string label, string value, Color valueColor)
        {
            RectTransform box = UIFactory.CreatePanel(parent, $"Stat_{label}", StatBox);
            UIFactory.AddLayoutElement(box.gameObject, flexibleWidth: 1, preferredHeight: 48);

            Text lbl = UIFactory.CreateText(box, "Lbl", label, 9,
                TextAnchor.UpperCenter, UIFactory.SilverText);
            lbl.fontStyle = FontStyle.Bold;
            RectTransform lblRT = lbl.GetComponent<RectTransform>();
            lblRT.anchorMin = new Vector2(0, 0.55f); lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = new Vector2(4, 0); lblRT.offsetMax = new Vector2(-4, -4);

            Text val = UIFactory.CreateText(box, "Val", value, 16,
                TextAnchor.MiddleCenter, valueColor);
            val.fontStyle = FontStyle.Bold;
            RectTransform valRT = val.GetComponent<RectTransform>();
            valRT.anchorMin = Vector2.zero; valRT.anchorMax = new Vector2(1, 0.58f);
            valRT.offsetMin = new Vector2(4, 2); valRT.offsetMax = new Vector2(-4, 0);
        }

        // ==================== 3. CONVOYS ====================
        private void BuildConvoySection(Transform content)
        {
            UIFactory.CreateSectionHeader(content, "ConvoyHdr", "CONVOIS EN TRANSIT");
            UIFactory.AddLayoutElement(content.GetChild(content.childCount - 1).gameObject, preferredHeight: 28);

            var convoys = LogisticsConvoySystem.GetConvoys(playerFaction.factionType)
                .Where(c => c.isActive).ToList();

            if (convoys.Count == 0)
            {
                BuildEmptyRow(content, "Aucun convoi en transit", "Envoyez des convois depuis vos villes.");
                return;
            }

            // Table header
            BuildTableHeader(content, new[] { "ROUTE", "RESSOURCES", "PROGRESSION", "COÛT" },
                new[] { 0.30f, 0.25f, 0.30f, 0.15f });

            bool alt = false;
            foreach (var convoy in convoys)
            {
                CityData origin = campaignManager.GetCity(convoy.originCityId);
                CityData dest = campaignManager.GetCity(convoy.destinationCityId);
                string originName = origin?.cityName ?? convoy.originCityId;
                string destName = dest?.cityName ?? convoy.destinationCityId;
                float progress = convoy.totalSteps > 0 ? (float)convoy.currentStep / convoy.totalSteps : 0f;

                RectTransform row = UIFactory.CreatePanel(content, $"C_{convoy.convoyId}", alt ? RowAlt : RowDark);
                UIFactory.AddLayoutElement(row.gameObject, preferredHeight: 40);

                // Route name
                string allyTag = convoy.isAlliedConvoy ? $" ({convoy.allyFaction})" : "";
                Text route = UIFactory.CreateText(row, "Route",
                    $"{originName} → {destName}{allyTag}", 11, TextAnchor.MiddleLeft,
                    convoy.isAlliedConvoy ? AllyTeal : UIFactory.Parchment);
                route.fontStyle = FontStyle.Bold;
                SetAnchored(route, 0f, 0.30f, 14f);

                // Resources
                string res = "";
                if (convoy.gold > 0) res += $"{convoy.gold:F0}g  ";
                if (convoy.food > 0) res += $"{convoy.food:F0}🌾  ";
                if (convoy.iron > 0) res += $"{convoy.iron:F0}⛏  ";
                Text resTxt = UIFactory.CreateText(row, "Res", res.Trim(), 10,
                    TextAnchor.MiddleLeft, UIFactory.Parchment);
                SetAnchored(resTxt, 0.30f, 0.55f, 4f);

                // Progress bar
                var (barBg, barFill) = UIFactory.CreateProgressBar(row, "Bar", ConvoyBlue);
                RectTransform barRT = barBg.GetComponent<RectTransform>();
                barRT.anchorMin = new Vector2(0.56f, 0.30f); barRT.anchorMax = new Vector2(0.82f, 0.70f);
                barRT.offsetMin = Vector2.zero; barRT.offsetMax = Vector2.zero;
                barRT.sizeDelta = Vector2.zero;
                barBg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                barFill.GetComponent<RectTransform>().anchorMax = new Vector2(progress, 1f);

                // Step text over bar
                Text stepText = UIFactory.CreateText(row, "Step",
                    $"{convoy.currentStep}/{convoy.totalSteps}", 9,
                    TextAnchor.MiddleCenter, UIFactory.Porcelain);
                RectTransform stepRT = stepText.GetComponent<RectTransform>();
                stepRT.anchorMin = new Vector2(0.56f, 0f); stepRT.anchorMax = new Vector2(0.82f, 1f);
                stepRT.offsetMin = Vector2.zero; stepRT.offsetMax = Vector2.zero;

                // Cost
                Text cost = UIFactory.CreateText(row, "Cost",
                    $"-{convoy.maintenanceCostPerTurn:F0}g", 10,
                    TextAnchor.MiddleCenter, OrangeAlert);
                SetAnchored(cost, 0.83f, 1f, 0f);

                alt = !alt;
            }
        }

        // ==================== 4. ROUTES ====================
        private int selectedFromCityIndex = 0;
        private int selectedToCityIndex = 1;

        private void BuildRouteSection(Transform content)
        {
            UIFactory.CreateSectionHeader(content, "RouteHdr", "CHAÎNES LOGISTIQUES");
            UIFactory.AddLayoutElement(content.GetChild(content.childCount - 1).gameObject, preferredHeight: 28);

            var routes = LogisticsConvoySystem.GetRoutes(playerFaction.factionType)
                .Where(r => r.isActive).ToList();

            if (routes.Count > 0)
            {
                BuildTableHeader(content, new[] { "ITINÉRAIRE", "NIVEAU", "CAPACITÉ", "COÛT", "" },
                    new[] { 0.30f, 0.12f, 0.18f, 0.15f, 0.25f });

                bool alt = false;
                foreach (var route in routes)
                {
                    CityData fromCity = campaignManager.GetCity(route.fromCityId);
                    CityData toCity = campaignManager.GetCity(route.toCityId);
                    string fromName = fromCity?.cityName ?? route.fromCityId;
                    string toName = toCity?.cityName ?? route.toCityId;

                    RectTransform row = UIFactory.CreatePanel(content, $"R_{route.routeId}", alt ? RowAlt : RowDark);
                    UIFactory.AddLayoutElement(row.gameObject, preferredHeight: 36);

                    // Route name
                    string allyTag = route.isAlliedRoute ? $" ({route.allyFaction})" : "";
                    Text rName = UIFactory.CreateText(row, "Name",
                        $"{fromName} → {toName}{allyTag}", 11, TextAnchor.MiddleLeft,
                        route.isAlliedRoute ? AllyTeal : UIFactory.Parchment);
                    rName.fontStyle = FontStyle.Bold;
                    SetAnchored(rName, 0f, 0.30f, 14f);

                    // Level
                    string lvlStr = route.level switch { 1 => "I", 2 => "II", 3 => "III", _ => "?" };
                    Color lvlColor = route.level switch { 1 => UIFactory.SilverText, 2 => UIFactory.EmpireGold, 3 => UIFactory.BrightGold, _ => Color.white };
                    Text lvl = UIFactory.CreateText(row, "Lvl", $"Nv.{lvlStr}", 11,
                        TextAnchor.MiddleCenter, lvlColor);
                    lvl.fontStyle = FontStyle.Bold;
                    SetAnchored(lvl, 0.30f, 0.42f, 0f);

                    // Capacity
                    Text cap = UIFactory.CreateText(row, "Cap",
                        $"{route.maxCapacityPerTurn:F0}/tour", 10,
                        TextAnchor.MiddleCenter, UIFactory.Parchment);
                    SetAnchored(cap, 0.42f, 0.60f, 0f);

                    // Maintenance
                    Text maint = UIFactory.CreateText(row, "Maint",
                        $"-{route.maintenanceCost:F0}g", 10,
                        TextAnchor.MiddleCenter, OrangeAlert);
                    SetAnchored(maint, 0.60f, 0.75f, 0f);

                    // Upgrade button (if not max)
                    if (route.level < 3)
                    {
                        int upgGold = LogisticsRoute.GetUpgradeCostGold(route.level);
                        int upgIron = LogisticsRoute.GetUpgradeCostIron(route.level);
                        bool canUpgrade = playerFaction.gold >= upgGold && playerFaction.iron >= upgIron;
                        string routeId = route.routeId;

                        Button upgBtn = UIFactory.CreateButton(row, "Upg",
                            canUpgrade ? $"AMÉLIORER ({upgGold}g {upgIron}⛏)" : "FONDS INSUF.", 9, () =>
                            {
                                var r = LogisticsConvoySystem.GetRoutes(playerFaction.factionType)
                                    .Find(x => x.routeId == routeId);
                                if (r != null && r.Upgrade(playerFaction))
                                {
                                    Destroy(gameObject);
                                    Create(navBar, campaignManager);
                                }
                            });
                        RectTransform btnRT = upgBtn.GetComponent<RectTransform>();
                        btnRT.anchorMin = new Vector2(0.76f, 0.12f);
                        btnRT.anchorMax = new Vector2(0.99f, 0.88f);
                        btnRT.offsetMin = Vector2.zero; btnRT.offsetMax = Vector2.zero;
                        upgBtn.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

                        upgBtn.GetComponent<Image>().color = canUpgrade
                            ? new Color(0.10f, 0.12f, 0.06f) : new Color(0.06f, 0.06f, 0.06f);
                        Text bTxt = upgBtn.GetComponentInChildren<Text>();
                        if (bTxt != null) bTxt.color = canUpgrade ? UIFactory.EmpireGold : UIFactory.SilverText;
                    }
                    else
                    {
                        Text maxTxt = UIFactory.CreateText(row, "Max", "MAX", 10,
                            TextAnchor.MiddleCenter, GreenOk);
                        maxTxt.fontStyle = FontStyle.Bold;
                        SetAnchored(maxTxt, 0.76f, 0.99f, 0f);
                    }

                    alt = !alt;
                }
            }
            else
            {
                BuildEmptyRow(content, "Aucune chaîne logistique", "Créez une route ci-dessous.");
            }

            // ── ROUTE CREATION ──
            BuildRouteCreator(content);
        }

        private void BuildRouteCreator(Transform content)
        {
            List<CityData> playerCities = campaignManager.GetFactionCities(playerFaction.factionType);
            if (playerCities == null || playerCities.Count < 2)
            {
                BuildEmptyRow(content, "Villes insuffisantes", "2 villes minimum requises.");
                return;
            }

            selectedFromCityIndex = Mathf.Clamp(selectedFromCityIndex, 0, playerCities.Count - 1);
            selectedToCityIndex = Mathf.Clamp(selectedToCityIndex, 0, playerCities.Count - 1);
            if (selectedToCityIndex == selectedFromCityIndex)
                selectedToCityIndex = (selectedFromCityIndex + 1) % playerCities.Count;

            CityData fromCity = playerCities[selectedFromCityIndex];
            CityData toCity = playerCities[selectedToCityIndex];

            // Creator container
            RectTransform creator = UIFactory.CreateBorderedPanel(content, "RouteCreator",
                new Color(0.06f, 0.07f, 0.055f, 1f), new Color(0.25f, 0.30f, 0.20f, 0.6f));
            UIFactory.AddLayoutElement(creator.gameObject, preferredHeight: 90);

            Transform inner = creator.Find("Inner") ?? creator;

            // Title
            Text title = UIFactory.CreateText(inner, "Title", "NOUVELLE ROUTE", 10,
                TextAnchor.MiddleLeft, UIFactory.EmpireGold);
            title.fontStyle = FontStyle.Bold;
            RectTransform titleRT = title.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0, 0.75f); titleRT.anchorMax = new Vector2(0.5f, 1f);
            titleRT.offsetMin = new Vector2(14, 0); titleRT.offsetMax = Vector2.zero;

            // FROM button
            Button fromBtn = UIFactory.CreateButton(inner, "FromBtn", $"DÉPART: {fromCity.cityName}", 10, () =>
            {
                selectedFromCityIndex = (selectedFromCityIndex + 1) % playerCities.Count;
                if (selectedFromCityIndex == selectedToCityIndex)
                    selectedFromCityIndex = (selectedFromCityIndex + 1) % playerCities.Count;
                Destroy(gameObject);
                Create(navBar, campaignManager);
            });
            RectTransform fromRT = fromBtn.GetComponent<RectTransform>();
            fromRT.anchorMin = new Vector2(0.02f, 0.38f); fromRT.anchorMax = new Vector2(0.32f, 0.72f);
            fromRT.offsetMin = Vector2.zero; fromRT.offsetMax = Vector2.zero;
            fromBtn.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            fromBtn.GetComponentInChildren<Text>().color = UIFactory.BrightGold;

            // Arrow
            Text arrow = UIFactory.CreateText(inner, "Arrow", "→", 18, TextAnchor.MiddleCenter, UIFactory.EmpireGold);
            RectTransform arrowRT = arrow.GetComponent<RectTransform>();
            arrowRT.anchorMin = new Vector2(0.33f, 0.38f); arrowRT.anchorMax = new Vector2(0.37f, 0.72f);
            arrowRT.offsetMin = Vector2.zero; arrowRT.offsetMax = Vector2.zero;

            // TO button
            Button toBtn = UIFactory.CreateButton(inner, "ToBtn", $"ARRIVÉE: {toCity.cityName}", 10, () =>
            {
                selectedToCityIndex = (selectedToCityIndex + 1) % playerCities.Count;
                if (selectedToCityIndex == selectedFromCityIndex)
                    selectedToCityIndex = (selectedToCityIndex + 1) % playerCities.Count;
                Destroy(gameObject);
                Create(navBar, campaignManager);
            });
            RectTransform toRT = toBtn.GetComponent<RectTransform>();
            toRT.anchorMin = new Vector2(0.38f, 0.38f); toRT.anchorMax = new Vector2(0.68f, 0.72f);
            toRT.offsetMin = Vector2.zero; toRT.offsetMax = Vector2.zero;
            toBtn.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            toBtn.GetComponentInChildren<Text>().color = UIFactory.BrightGold;

            // Path info
            List<string> path = LogisticsConvoySystem.FindPath(
                fromCity.provinceId, toCity.provinceId, playerFaction.factionType, campaignManager.Provinces);
            bool pathFound = path != null && path.Count > 0;
            int setupCost = pathFound ? 100 + path.Count * 20 : 0;
            bool canAfford = playerFaction.gold >= setupCost;

            if (pathFound)
            {
                Text info = UIFactory.CreateText(inner, "Info",
                    $"{path.Count} prov.  •  Coût: {setupCost}g  •  Cap: {LogisticsRoute.GetCapacity(1):F0}/tour",
                    9, TextAnchor.MiddleLeft, canAfford ? UIFactory.SilverText : RedCrit);
                RectTransform infoRT = info.GetComponent<RectTransform>();
                infoRT.anchorMin = new Vector2(0.02f, 0.02f); infoRT.anchorMax = new Vector2(0.68f, 0.35f);
                infoRT.offsetMin = new Vector2(14, 0); infoRT.offsetMax = Vector2.zero;
            }
            else
            {
                Text noPath = UIFactory.CreateText(inner, "NoPath", "Aucun chemin trouvé",
                    10, TextAnchor.MiddleLeft, RedCrit);
                noPath.fontStyle = FontStyle.Bold;
                RectTransform npRT = noPath.GetComponent<RectTransform>();
                npRT.anchorMin = new Vector2(0.02f, 0.02f); npRT.anchorMax = new Vector2(0.68f, 0.35f);
                npRT.offsetMin = new Vector2(14, 0); npRT.offsetMax = Vector2.zero;
            }

            // Create button
            bool canCreate = pathFound && canAfford;
            Button createBtn = UIFactory.CreateButton(inner, "CreateBtn",
                canCreate ? $"CRÉER — {setupCost}g" : pathFound ? "FONDS INSUFFISANTS" : "IMPOSSIBLE", 11, () =>
                {
                    if (!canCreate) return;
                    var result = LogisticsConvoySystem.CreateRoute(fromCity, toCity, playerFaction.factionType, campaignManager.Provinces);
                    if (result != null)
                    {
                        Destroy(gameObject);
                        Create(navBar, campaignManager);
                    }
                });
            RectTransform cRT = createBtn.GetComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0.70f, 0.15f); cRT.anchorMax = new Vector2(0.98f, 0.85f);
            cRT.offsetMin = Vector2.zero; cRT.offsetMax = Vector2.zero;
            createBtn.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            createBtn.GetComponent<Image>().color = canCreate
                ? new Color(0.12f, 0.18f, 0.08f) : new Color(0.06f, 0.06f, 0.06f);
            Text cTxt = createBtn.GetComponentInChildren<Text>();
            if (cTxt != null)
            {
                cTxt.color = canCreate ? GreenOk : UIFactory.SilverText;
                cTxt.fontStyle = FontStyle.Bold;
            }
        }

        // ==================== 5. CRITICAL ZONES ====================
        private void BuildCriticalZones(Transform content)
        {
            UIFactory.CreateSectionHeader(content, "CritHdr", "ZONES CRITIQUES");
            UIFactory.AddLayoutElement(content.GetChild(content.childCount - 1).gameObject, preferredHeight: 28);

            var critical = SupplySystem.GetCriticalProvinces(0.50f);
            if (critical.Count == 0)
            {
                BuildEmptyRow(content, "Aucune zone critique", "Approvisionnement nominal.", GreenOk);
                return;
            }

            BuildTableHeader(content, new[] { "PROVINCE", "CAUSE", "ÉTAT" },
                new[] { 0.35f, 0.35f, 0.30f });

            bool alt = false;
            foreach (var prov in critical)
            {
                float ratio = prov.SupplyRatio;
                Color barCol = ratio < 0.25f ? RedCrit : YellowWarn;
                string reason = prov.infrastructureLevel <= 2 ? "Infrastructure faible" : "Surcharge d'unités";

                RectTransform row = UIFactory.CreatePanel(content, $"Crit_{prov.provinceId}", alt ? RowAlt : RowDark);
                UIFactory.AddLayoutElement(row.gameObject, preferredHeight: 32);

                Text name = UIFactory.CreateText(row, "Name", prov.provinceId, 11,
                    TextAnchor.MiddleLeft, OrangeAlert);
                name.fontStyle = FontStyle.Bold;
                SetAnchored(name, 0f, 0.35f, 14f);

                Text reasonTxt = UIFactory.CreateText(row, "Reason", reason, 10,
                    TextAnchor.MiddleLeft, UIFactory.SilverText);
                SetAnchored(reasonTxt, 0.35f, 0.65f, 4f);

                // Status: bar + percentage
                var (bg, fill) = UIFactory.CreateProgressBar(row, "Bar", barCol);
                RectTransform barRT = bg.GetComponent<RectTransform>();
                barRT.anchorMin = new Vector2(0.67f, 0.25f); barRT.anchorMax = new Vector2(0.88f, 0.75f);
                barRT.offsetMin = Vector2.zero; barRT.offsetMax = Vector2.zero;
                barRT.sizeDelta = Vector2.zero;
                bg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                fill.GetComponent<RectTransform>().anchorMax = new Vector2(ratio, 1f);

                Text pctTxt = UIFactory.CreateText(row, "Pct", $"{(int)(ratio * 100)}%", 11,
                    TextAnchor.MiddleRight, barCol);
                pctTxt.fontStyle = FontStyle.Bold;
                SetAnchored(pctTxt, 0.90f, 0.98f, 0f);

                alt = !alt;
            }
        }

        // ==================== 6. EQUIPMENT ====================
        private void BuildEquipmentSection(Transform content)
        {
            UIFactory.CreateSectionHeader(content, "EquipHdr", "ÉQUIPEMENT MILITAIRE");
            UIFactory.AddLayoutElement(content.GetChild(content.childCount - 1).gameObject, preferredHeight: 28);

            Dictionary<string, ArmyData> armies = campaignManager.Armies ?? new Dictionary<string, ArmyData>();
            var summary = ProductionManager.GetEquipmentSummary(playerFaction, armies);
            bool hasAny = false;

            BuildTableHeader(content, new[] { "ÉQUIPEMENT", "STOCK", "ÉTAT", "PROD." },
                new[] { 0.25f, 0.20f, 0.35f, 0.20f });

            bool alt = false;
            foreach (var kvp in summary)
            {
                var (stock, demand, production) = kvp.Value;
                if (demand <= 0) continue;
                hasAny = true;

                bool isDeficit = stock < demand;
                float ratio = demand > 0 ? Mathf.Clamp01(stock / demand) : 1f;
                Color barCol = isDeficit ? (stock < demand * 0.5f ? RedCrit : YellowWarn) : GreenOk;

                RectTransform row = UIFactory.CreatePanel(content, $"Eq_{kvp.Key}", alt ? RowAlt : RowDark);
                UIFactory.AddLayoutElement(row.gameObject, preferredHeight: 30);

                // Name
                Text eqName = UIFactory.CreateText(row, "Name",
                    ProductionUI.GetEquipmentName(kvp.Key), 11, TextAnchor.MiddleLeft,
                    isDeficit ? OrangeAlert : UIFactory.Parchment);
                eqName.fontStyle = FontStyle.Bold;
                SetAnchored(eqName, 0f, 0.25f, 14f);

                // Stock
                Text stockTxt = UIFactory.CreateText(row, "Stock", $"{stock:F0}/{demand:F0}", 10,
                    TextAnchor.MiddleCenter, barCol);
                SetAnchored(stockTxt, 0.25f, 0.42f, 0f);

                // Progress bar
                var (bg, fill) = UIFactory.CreateProgressBar(row, "Bar", barCol);
                RectTransform barRT = bg.GetComponent<RectTransform>();
                barRT.anchorMin = new Vector2(0.44f, 0.25f); barRT.anchorMax = new Vector2(0.75f, 0.75f);
                barRT.offsetMin = Vector2.zero; barRT.offsetMax = Vector2.zero;
                barRT.sizeDelta = Vector2.zero;
                bg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                fill.GetComponent<RectTransform>().anchorMax = new Vector2(ratio, 1f);

                // Production
                string prodStr = production > 0 ? $"+{production:F0}/tour" : "—";
                Text prodTxt = UIFactory.CreateText(row, "Prod", prodStr, 10,
                    TextAnchor.MiddleCenter, production > 0 ? GreenOk : UIFactory.SilverText);
                SetAnchored(prodTxt, 0.78f, 0.98f, 0f);

                alt = !alt;
            }

            if (!hasAny)
            {
                BuildEmptyRow(content, "Stock complet", "Tous les équipements en quantité suffisante.", GreenOk);
            }
        }

        // ==================== 7. TRADE ====================
        private void BuildTradeSection(Transform content)
        {
            UIFactory.CreateSectionHeader(content, "TradeHdr", "ROUTES COMMERCIALES");
            UIFactory.AddLayoutElement(content.GetChild(content.childCount - 1).gameObject, preferredHeight: 28);

            bool hasRoutes = false;
            if (TradeSystem.Instance != null)
            {
                foreach (var route in TradeSystem.Instance.TradeRoutes.Values)
                {
                    if (!route.isActive) continue;
                    if (route.fromFaction != playerFaction.factionType && route.toFaction != playerFaction.factionType) continue;
                    hasRoutes = true;

                    string seaTag = route.isSeaRoute ? " 🚢" : "";
                    RectTransform row = UIFactory.CreatePanel(content, $"TR_{route.routeId}", RowDark);
                    UIFactory.AddLayoutElement(row.gameObject, preferredHeight: 28);

                    Text line = UIFactory.CreateText(row, "Line",
                        $"{route.fromCityId} → {route.toCityId}{seaTag}  •  +{route.goldPerTurn:F0}g/tour  ({route.resource})",
                        11, TextAnchor.MiddleLeft, UIFactory.Parchment);
                    SetAnchored(line, 0f, 1f, 14f);
                }

                foreach (var agreement in TradeSystem.Instance.TradeAgreements.Values)
                {
                    if (!agreement.isActive) continue;
                    if (agreement.faction1 != playerFaction.factionType && agreement.faction2 != playerFaction.factionType) continue;
                    hasRoutes = true;

                    FactionType partner = agreement.faction1 == playerFaction.factionType ? agreement.faction2 : agreement.faction1;
                    float income = agreement.faction1 == playerFaction.factionType ? agreement.goldPerTurnFaction1 : agreement.goldPerTurnFaction2;

                    RectTransform row = UIFactory.CreatePanel(content, $"TA_{agreement.agreementId}", RowAlt);
                    UIFactory.AddLayoutElement(row.gameObject, preferredHeight: 28);

                    Text line = UIFactory.CreateText(row, "Line",
                        $"Accord: {CampaignManager.GetFactionSubtitle(partner)}  •  +{income:F0}g/tour",
                        11, TextAnchor.MiddleLeft, GreenOk);
                    SetAnchored(line, 0f, 1f, 14f);
                }
            }

            if (!hasRoutes)
                BuildEmptyRow(content, "Aucune route commerciale", "Établissez des accords diplomatiques.");
        }

        // ==================== 8. ALLIED PROPOSALS ====================
        private void BuildAlliedSection(Transform content)
        {
            var pending = LogisticsConvoySystem.GetPendingAgreements(playerFaction.factionType);
            if (pending.Count == 0) return;

            AddSectionGap(content);
            UIFactory.CreateSectionHeader(content, "AllyHdr", "PROPOSITIONS ALLIÉES");
            UIFactory.AddLayoutElement(content.GetChild(content.childCount - 1).gameObject, preferredHeight: 28);

            foreach (var agreement in pending)
            {
                RectTransform card = UIFactory.CreateBorderedPanel(content, $"A_{agreement.agreementId}",
                    new Color(0.06f, 0.08f, 0.10f, 1f), new Color(0.20f, 0.40f, 0.50f, 0.5f));
                UIFactory.AddLayoutElement(card.gameObject, preferredHeight: 54);

                Transform inner = card.Find("Inner") ?? card;

                // Proposer
                Text title = UIFactory.CreateText(inner, "Title",
                    $"Proposition de {CampaignManager.GetFactionSubtitle(agreement.proposer)}", 11,
                    TextAnchor.MiddleLeft, AllyTeal);
                title.fontStyle = FontStyle.Bold;
                RectTransform titleRT = title.GetComponent<RectTransform>();
                titleRT.anchorMin = new Vector2(0, 0.5f); titleRT.anchorMax = new Vector2(0.50f, 1f);
                titleRT.offsetMin = new Vector2(12, 0); titleRT.offsetMax = Vector2.zero;

                // Offer details
                string offer = "";
                if (agreement.goldPerTurn > 0) offer += $"{agreement.goldPerTurn:F0}g  ";
                if (agreement.foodPerTurn > 0) offer += $"{agreement.foodPerTurn:F0}🌾  ";
                if (agreement.ironPerTurn > 0) offer += $"{agreement.ironPerTurn:F0}⛏  ";
                offer += "/tour";

                Text offerTxt = UIFactory.CreateText(inner, "Offer", offer, 10,
                    TextAnchor.MiddleLeft, UIFactory.Parchment);
                RectTransform offerRT = offerTxt.GetComponent<RectTransform>();
                offerRT.anchorMin = new Vector2(0, 0f); offerRT.anchorMax = new Vector2(0.50f, 0.50f);
                offerRT.offsetMin = new Vector2(12, 4); offerRT.offsetMax = Vector2.zero;

                // Accept
                string agrId = agreement.agreementId;
                Button acceptBtn = UIFactory.CreateButton(inner, "Accept", "ACCEPTER", 10, () =>
                {
                    LogisticsConvoySystem.RespondToAgreement(agrId, true, campaignManager.Provinces);
                    Destroy(gameObject);
                    Create(navBar, campaignManager);
                });
                RectTransform accRT = acceptBtn.GetComponent<RectTransform>();
                accRT.anchorMin = new Vector2(0.55f, 0.15f); accRT.anchorMax = new Vector2(0.75f, 0.85f);
                accRT.offsetMin = Vector2.zero; accRT.offsetMax = Vector2.zero;
                acceptBtn.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                acceptBtn.GetComponent<Image>().color = new Color(0.08f, 0.16f, 0.08f);
                Text accTxt = acceptBtn.GetComponentInChildren<Text>();
                if (accTxt != null) accTxt.color = GreenOk;

                // Reject
                Button rejectBtn = UIFactory.CreateButton(inner, "Reject", "REFUSER", 10, () =>
                {
                    LogisticsConvoySystem.RespondToAgreement(agrId, false, campaignManager.Provinces);
                    Destroy(gameObject);
                    Create(navBar, campaignManager);
                });
                RectTransform rejRT = rejectBtn.GetComponent<RectTransform>();
                rejRT.anchorMin = new Vector2(0.78f, 0.15f); rejRT.anchorMax = new Vector2(0.98f, 0.85f);
                rejRT.offsetMin = Vector2.zero; rejRT.offsetMax = Vector2.zero;
                rejectBtn.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                rejectBtn.GetComponent<Image>().color = new Color(0.16f, 0.06f, 0.06f);
                Text rejTxt = rejectBtn.GetComponentInChildren<Text>();
                if (rejTxt != null) rejTxt.color = RedCrit;
            }
        }

        // ==================== HELPERS ====================

        /// <summary>Table column header row</summary>
        private void BuildTableHeader(Transform parent, string[] columns, float[] widths)
        {
            RectTransform hdr = UIFactory.CreatePanel(parent, "TblHdr", new Color(0.05f, 0.055f, 0.065f, 1f));
            UIFactory.AddLayoutElement(hdr.gameObject, preferredHeight: 22);

            float x = 0f;
            for (int i = 0; i < columns.Length && i < widths.Length; i++)
            {
                Text col = UIFactory.CreateText(hdr, $"Col{i}", columns[i], 9,
                    i == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, UIFactory.SilverText);
                col.fontStyle = FontStyle.Bold;
                RectTransform rt = col.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(x, 0); rt.anchorMax = new Vector2(x + widths[i], 1);
                rt.offsetMin = i == 0 ? new Vector2(14, 0) : Vector2.zero;
                rt.offsetMax = Vector2.zero;
                x += widths[i];
            }
        }

        /// <summary>Empty state row</summary>
        private void BuildEmptyRow(Transform parent, string title, string subtitle, Color? color = null)
        {
            RectTransform row = UIFactory.CreatePanel(parent, "Empty", RowDark);
            UIFactory.AddLayoutElement(row.gameObject, preferredHeight: 42);

            Text t = UIFactory.CreateText(row, "Title", title, 11,
                TextAnchor.MiddleCenter, color ?? UIFactory.SilverText);
            t.fontStyle = FontStyle.Bold;
            RectTransform tRT = t.GetComponent<RectTransform>();
            tRT.anchorMin = new Vector2(0, 0.45f); tRT.anchorMax = Vector2.one;
            tRT.offsetMin = Vector2.zero; tRT.offsetMax = Vector2.zero;

            Text sub = UIFactory.CreateText(row, "Sub", subtitle, 9,
                TextAnchor.MiddleCenter, UIFactory.MutedGold);
            RectTransform sRT = sub.GetComponent<RectTransform>();
            sRT.anchorMin = Vector2.zero; sRT.anchorMax = new Vector2(1, 0.48f);
            sRT.offsetMin = Vector2.zero; sRT.offsetMax = Vector2.zero;
        }

        /// <summary>Set anchored position for text spanning row columns</summary>
        private void SetAnchored(Text text, float xMin, float xMax, float leftPad)
        {
            RectTransform rt = text.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(xMin, 0);
            rt.anchorMax = new Vector2(xMax, 1);
            rt.offsetMin = new Vector2(leftPad, 0);
            rt.offsetMax = Vector2.zero;
        }

        private void AddSectionGap(Transform content)
        {
            RectTransform gap = UIFactory.CreatePanel(content, "Gap", Color.clear);
            UIFactory.AddLayoutElement(gap.gameObject, preferredHeight: 8);
        }
    }
}
