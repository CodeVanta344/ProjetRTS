using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Campaign;
using NapoleonicWars.Data;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// HoI4-style Logistics panel showing supply overview, critical zones,
    /// equipment deficits, and trade routes.
    /// </summary>
    public class LogisticsUI : MonoBehaviour
    {
        private CampaignManager campaignManager;
        private FactionData playerFaction;
        private NavigationBar navBar;

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

        private void BuildContent(Transform parent)
        {
            var (scroll, content) = UIFactory.CreateScrollView(parent, "LogisticsScroll");
            UIFactory.AddLayoutElement(scroll.gameObject, flexibleHeight: 1, preferredHeight: 900);

            // === GLOBAL SUPPLY ===
            float globalSupply = SupplySystem.GetGlobalSupplyHealth();
            string supplyStatus = globalSupply > 0.75f ? "Bon" : globalSupply > 0.50f ? "Moyen" : "Critique";
            Color supplyColor = globalSupply > 0.75f ? new Color(0.3f, 0.9f, 0.3f) : 
                                globalSupply > 0.50f ? new Color(0.9f, 0.8f, 0.2f) : new Color(0.9f, 0.3f, 0.3f);

            RectTransform summaryRT = UIFactory.CreatePanel(content, "Summary", new Color(0.10f, 0.07f, 0.05f, 0.95f));
            UIFactory.AddLayoutElement(summaryRT.gameObject, preferredHeight: 50);
            Text summaryText = UIFactory.CreateText(summaryRT, "Text", 
                $"Approvisionnement global: {(int)(globalSupply * 100)}% ({supplyStatus})", 
                16, TextAnchor.MiddleCenter, supplyColor);
            summaryText.fontStyle = FontStyle.Bold;
            RectTransform summaryTextRT = summaryText.GetComponent<RectTransform>();
            summaryTextRT.anchorMin = Vector2.zero;
            summaryTextRT.anchorMax = Vector2.one;
            summaryTextRT.offsetMin = new Vector2(15, 0);
            summaryTextRT.offsetMax = new Vector2(-15, 0);

            // === CRITICAL ZONES ===
            CreateLogSectionLabel(content, "Zones critiques (supply < 50%)");

            var criticalProvinces = SupplySystem.GetCriticalProvinces(0.50f);
            if (criticalProvinces.Count == 0)
            {
                Text noCrit = UIFactory.CreateText(content, "NoCrit", "Aucune zone critique — approvisionnement nominal.", 
                    12, TextAnchor.MiddleLeft, UIFactory.TextGrey);
                UIFactory.AddLayoutElement(noCrit.gameObject, preferredHeight: 30);
            }
            else
            {
                foreach (var prov in criticalProvinces)
                {
                    string reason = prov.infrastructureLevel <= 2 ? "(pas d'infrastructure)" : "(trop d'unités)";
                    Text critLine = UIFactory.CreateText(content, $"Crit_{prov.provinceId}", 
                        $"  ⚠️ {prov.provinceId} — {(int)(prov.SupplyRatio * 100)}% {reason}", 
                        12, TextAnchor.MiddleLeft, new Color(1f, 0.6f, 0.2f));
                    UIFactory.AddLayoutElement(critLine.gameObject, preferredHeight: 25);
                }
            }

            // === EQUIPMENT DEFICITS ===
            CreateLogSectionLabel(content, "Équipement en déficit");

            Dictionary<string, ArmyData> armies = GetArmies();
            var summary = ProductionManager.GetEquipmentSummary(playerFaction, armies);
            bool hasDeficit = false;
            foreach (var kvp in summary)
            {
                var (stock, demand, production) = kvp.Value;
                if (demand > 0 && stock < demand)
                {
                    hasDeficit = true;
                    float deficit = demand - stock;
                    string severity = deficit > demand * 0.5f ? "🔴" : "🟡";
                    string prodInfo = production > 0 ? $"(prod. {production:F0}/tour)" : "(prod. insuffisante)";
                    Text defLine = UIFactory.CreateText(content, $"Def_{kvp.Key}", 
                        $"  {severity} {ProductionUI.GetEquipmentName(kvp.Key)}: -{deficit:F0} {prodInfo}", 
                        12, TextAnchor.MiddleLeft, deficit > demand * 0.5f ? new Color(1f, 0.3f, 0.3f) : new Color(1f, 0.8f, 0.3f));
                    UIFactory.AddLayoutElement(defLine.gameObject, preferredHeight: 25);
                }
            }
            if (!hasDeficit)
            {
                Text noDef = UIFactory.CreateText(content, "NoDef", "Tous les équipements sont en stock suffisant.", 
                    12, TextAnchor.MiddleLeft, new Color(0.3f, 0.9f, 0.3f));
                UIFactory.AddLayoutElement(noDef.gameObject, preferredHeight: 30);
            }

            // === TRADE ROUTES ===
            CreateLogSectionLabel(content, "Routes commerciales actives");

            bool hasRoutes = false;
            if (TradeSystem.Instance != null)
            {
                // Trade routes
                foreach (var route in TradeSystem.Instance.TradeRoutes.Values)
                {
                    if (!route.isActive) continue;
                    if (route.fromFaction != playerFaction.factionType && route.toFaction != playerFaction.factionType) continue;
                    hasRoutes = true;

                    string fromCity = route.fromCityId ?? "?";
                    string toCity = route.toCityId ?? "?";
                    string seaTag = route.isSeaRoute ? " 🚢" : "";
                    Text routeLine = UIFactory.CreateText(content, $"Route_{route.routeId}", 
                        $"  🔗 {fromCity} → {toCity}{seaTag} : +{route.goldPerTurn:F0}g/tour ({route.resource})", 
                        12, TextAnchor.MiddleLeft, UIFactory.ParchmentBeige);
                    UIFactory.AddLayoutElement(routeLine.gameObject, preferredHeight: 25);
                }

                // Trade agreements
                foreach (var agreement in TradeSystem.Instance.TradeAgreements.Values)
                {
                    if (!agreement.isActive) continue;
                    if (agreement.faction1 != playerFaction.factionType && agreement.faction2 != playerFaction.factionType) continue;
                    hasRoutes = true;

                    FactionType partner = agreement.faction1 == playerFaction.factionType ? agreement.faction2 : agreement.faction1;
                    float income = agreement.faction1 == playerFaction.factionType ? agreement.goldPerTurnFaction1 : agreement.goldPerTurnFaction2;
                    Text agrLine = UIFactory.CreateText(content, $"Agr_{agreement.agreementId}", 
                        $"  🤝 Accord avec {CampaignManager.GetFactionSubtitle(partner)} : +{income:F0}g/tour", 
                        12, TextAnchor.MiddleLeft, new Color(0.5f, 0.85f, 0.5f));
                    UIFactory.AddLayoutElement(agrLine.gameObject, preferredHeight: 25);
                }
            }

            if (!hasRoutes)
            {
                Text noRoutes = UIFactory.CreateText(content, "NoRoutes", 
                    "Aucune route commerciale active. Établissez des accords diplomatiques ou construisez des comptoirs.", 
                    12, TextAnchor.MiddleLeft, UIFactory.TextGrey);
                UIFactory.AddLayoutElement(noRoutes.gameObject, preferredHeight: 30);
            }
        }

        private Dictionary<string, ArmyData> GetArmies()
        {
            return campaignManager.Armies ?? new Dictionary<string, ArmyData>();
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
