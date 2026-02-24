using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Campaign;
using NapoleonicWars.Data;
using NapoleonicWars.Units;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// Context panel appearing at the bottom when clicking on map elements.
    /// Shows province info or army info depending on selection.
    /// </summary>
    public class ContextPanelUI : MonoBehaviour
    {
        private CampaignManager campaignManager;
        private FactionData playerFaction;
        private RectTransform panelRT;
        private Transform contentParent;

        public void Initialize(CampaignManager manager, Transform uiRoot)
        {
            campaignManager = manager;
            playerFaction = manager.GetPlayerFaction();

            // Context panel at bottom of screen, right of nav bar
            panelRT = UIFactory.CreateOrnatePanel(uiRoot, "ContextPanel", UIFactory.PanelBg);
            panelRT.anchorMin = new Vector2(0, 0);
            panelRT.anchorMax = new Vector2(0.55f, 0);
            panelRT.pivot = new Vector2(0, 0);
            panelRT.anchoredPosition = new Vector2(55, 5);
            panelRT.sizeDelta = new Vector2(0, 160);
            // Adjust to use anchors properly
            panelRT.anchorMax = new Vector2(0.55f, 0);
            panelRT.offsetMin = new Vector2(55, 5);
            panelRT.offsetMax = new Vector2(0, 165);

            contentParent = panelRT.transform.Find("Inner"); // Inner content
            panelRT.gameObject.SetActive(false);
        }

        public void ShowProvinceInfo(string provinceId, ProvinceData province)
        {
            ClearContent();
            panelRT.gameObject.SetActive(true);

            // Province name + faction
            string factionName = province.owner.ToString();
            Text nameText = UIFactory.CreateText(contentParent, "Name", 
                $"{province.provinceName} ({factionName})", 16, TextAnchor.UpperLeft, UIFactory.GoldAccent);
            nameText.fontStyle = FontStyle.Bold;
            RectTransform nameRT = nameText.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0, 0.65f);
            nameRT.anchorMax = new Vector2(0.5f, 1);
            nameRT.offsetMin = new Vector2(10, 0);
            nameRT.offsetMax = new Vector2(0, -5);

            // Population + terrain
            Text popText = UIFactory.CreateText(contentParent, "Pop", 
                $"Pop: {province.population:N0} | {province.terrainType}", 
                11, TextAnchor.MiddleLeft, UIFactory.ParchmentBeige);
            RectTransform popRT = popText.GetComponent<RectTransform>();
            popRT.anchorMin = new Vector2(0.5f, 0.75f);
            popRT.anchorMax = new Vector2(1, 1);
            popRT.offsetMin = new Vector2(5, 0);
            popRT.offsetMax = new Vector2(-5, -5);

            // Per-province income
            Text incomeText = UIFactory.CreateText(contentParent, "Income", 
                $"Or: {province.goldIncome:F0}/t | Nourriture: {province.foodProduction:F0}/t | Fer: {province.ironProduction:F0}/t", 
                10, TextAnchor.MiddleLeft, new Color(0.9f, 0.8f, 0.4f));
            RectTransform incomeRT = incomeText.GetComponent<RectTransform>();
            incomeRT.anchorMin = new Vector2(0.5f, 0.55f);
            incomeRT.anchorMax = new Vector2(1, 0.75f);
            incomeRT.offsetMin = new Vector2(5, 0);
            incomeRT.offsetMax = new Vector2(-5, 0);

            // Buildings
            string buildingsStr = "";
            foreach (var slot in province.buildings)
            {
                if (slot.type != BuildingType.Empty && slot.level > 0)
                    buildingsStr += $"{BuildingInfo.GetIcon(slot.type)}{BuildingInfo.GetName(slot.type)} Nv{slot.level}  ";
                else if (slot.isConstructing)
                    buildingsStr += $"🚧{BuildingInfo.GetName(slot.type)}  ";
            }
            if (string.IsNullOrEmpty(buildingsStr)) buildingsStr = "Aucun bâtiment";
            Text buildText = UIFactory.CreateText(contentParent, "Buildings", buildingsStr,
                10, TextAnchor.MiddleLeft, UIFactory.TextGrey);
            RectTransform buildRT = buildText.GetComponent<RectTransform>();
            buildRT.anchorMin = new Vector2(0, 0.65f);
            buildRT.anchorMax = new Vector2(0.5f, 0.75f);
            buildRT.offsetMin = new Vector2(10, 0);
            buildRT.offsetMax = Vector2.zero;

            // Infrastructure
            var supply = SupplySystem.GetSupplyData(provinceId);
            Text infraText = UIFactory.CreateText(contentParent, "Infra", 
                $"Infra: {new string('█', supply.infrastructureLevel)}{new string('░', 10 - supply.infrastructureLevel)} Nv{supply.infrastructureLevel}", 
                11, TextAnchor.MiddleLeft, UIFactory.ParchmentBeige);
            RectTransform infraRT = infraText.GetComponent<RectTransform>();
            infraRT.anchorMin = new Vector2(0, 0.40f);
            infraRT.anchorMax = new Vector2(0.55f, 0.55f);
            infraRT.offsetMin = new Vector2(10, 0);
            infraRT.offsetMax = Vector2.zero;

            // Supply
            Text supplyText = UIFactory.CreateText(contentParent, "Supply", 
                $"Supply: {(int)(supply.SupplyRatio * 100)}%  |  Garnison: {province.garrisonSize}", 
                11, TextAnchor.MiddleLeft, 
                supply.SupplyRatio > 0.7f ? new Color(0.3f, 0.9f, 0.3f) : new Color(1f, 0.5f, 0.2f));
            RectTransform supplyRT = supplyText.GetComponent<RectTransform>();
            supplyRT.anchorMin = new Vector2(0.55f, 0.40f);
            supplyRT.anchorMax = new Vector2(1, 0.55f);
            supplyRT.offsetMin = new Vector2(5, 0);
            supplyRT.offsetMax = new Vector2(-5, 0);

            // Action buttons
            Button buildBtn = UIFactory.CreateButton(contentParent, "Build", "Construire", 11);
            RectTransform buildBtnRT = buildBtn.GetComponent<RectTransform>();
            buildBtnRT.anchorMin = new Vector2(0.02f, 0.02f);
            buildBtnRT.anchorMax = new Vector2(0.25f, 0.22f);
            buildBtnRT.offsetMin = Vector2.zero;
            buildBtnRT.offsetMax = Vector2.zero;

            Button recruitBtn = UIFactory.CreateButton(contentParent, "Recruit", "Recruter", 11);
            RectTransform recruitBtnRT = recruitBtn.GetComponent<RectTransform>();
            recruitBtnRT.anchorMin = new Vector2(0.27f, 0.02f);
            recruitBtnRT.anchorMax = new Vector2(0.50f, 0.22f);
            recruitBtnRT.offsetMin = Vector2.zero;
            recruitBtnRT.offsetMax = Vector2.zero;

            Button detailBtn = UIFactory.CreateButton(contentParent, "Details", "Détails", 11);
            RectTransform detailBtnRT = detailBtn.GetComponent<RectTransform>();
            detailBtnRT.anchorMin = new Vector2(0.52f, 0.02f);
            detailBtnRT.anchorMax = new Vector2(0.75f, 0.22f);
            detailBtnRT.offsetMin = Vector2.zero;
            detailBtnRT.offsetMax = Vector2.zero;
        }

        public void ShowArmyInfo(ArmyData army)
        {
            ClearContent();
            panelRT.gameObject.SetActive(true);

            // Army name + general + location
            string generalStr = !string.IsNullOrEmpty(army.generalId) ? $"(Gén. {army.generalId})" : "";
            string locationName = campaignManager != null ? campaignManager.GetProvinceName(army.currentProvinceId) : army.currentProvinceId;
            Text nameText = UIFactory.CreateText(contentParent, "Name", 
                $"⚔️ {army.armyName} {generalStr}", 15, TextAnchor.UpperLeft, UIFactory.GoldAccent);
            nameText.fontStyle = FontStyle.Bold;
            RectTransform nameRT = nameText.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0, 0.75f);
            nameRT.anchorMax = new Vector2(0.6f, 1);
            nameRT.offsetMin = new Vector2(10, 0);
            nameRT.offsetMax = new Vector2(0, -5);

            // Location
            Text locText = UIFactory.CreateText(contentParent, "Location", 
                $"📍 {locationName}  |  Entretien: {army.MaintenanceCost:F0}g/t", 
                11, TextAnchor.MiddleLeft, UIFactory.TextGrey);
            RectTransform locRT = locText.GetComponent<RectTransform>();
            locRT.anchorMin = new Vector2(0, 0.60f);
            locRT.anchorMax = new Vector2(0.6f, 0.75f);
            locRT.offsetMin = new Vector2(10, 0);
            locRT.offsetMax = Vector2.zero;

            // Stats + rank summary
            int soldiers = army.TotalSoldiers;
            string soldiersStr = soldiers >= 1000 ? $"{soldiers / 1000f:F1}K" : $"{soldiers}";
            int maxRank = 0;
            foreach (var reg in army.regiments)
                if (reg.rank > maxRank) maxRank = reg.rank;
            string rankStr = maxRank > 0 ? $" | Rang max: {RegimentRankSystem.GetRankName(maxRank)}" : "";
            Text statsText = UIFactory.CreateText(contentParent, "Stats", 
                $"{soldiersStr} hommes | {army.regiments.Count} rég. | Org: {army.organization:F0}%{rankStr}", 
                11, TextAnchor.MiddleLeft, UIFactory.ParchmentBeige);
            RectTransform statsRT = statsText.GetComponent<RectTransform>();
            statsRT.anchorMin = new Vector2(0, 0.40f);
            statsRT.anchorMax = new Vector2(0.6f, 0.60f);
            statsRT.offsetMin = new Vector2(10, 0);
            statsRT.offsetMax = Vector2.zero;

            // Org bar
            var (bg, fill) = UIFactory.CreateProgressBar(contentParent, "OrgBar", new Color(0.3f, 0.7f, 0.3f));
            RectTransform bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0.62f, 0.5f);
            bgRT.anchorMax = new Vector2(0.95f, 0.7f);
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            fill.GetComponent<RectTransform>().anchorMax = new Vector2(army.organization / army.maxOrganization, 1);

            // Supply
            Text supplyText = UIFactory.CreateText(contentParent, "Supply", 
                $"Supply: {(int)(SupplySystem.GetSupplyData(army.currentProvinceId ?? "").SupplyRatio * 100)}%", 
                11, TextAnchor.MiddleLeft, UIFactory.ParchmentBeige);
            RectTransform supplyRT = supplyText.GetComponent<RectTransform>();
            supplyRT.anchorMin = new Vector2(0.62f, 0.3f);
            supplyRT.anchorMax = new Vector2(0.95f, 0.5f);
            supplyRT.offsetMin = Vector2.zero;
            supplyRT.offsetMax = Vector2.zero;

            // Action buttons
            Button detailBtn = UIFactory.CreateButton(contentParent, "Details", "Détails", 11);
            RectTransform detailBtnRT = detailBtn.GetComponent<RectTransform>();
            detailBtnRT.anchorMin = new Vector2(0.02f, 0.02f);
            detailBtnRT.anchorMax = new Vector2(0.22f, 0.30f);
            detailBtnRT.offsetMin = Vector2.zero;
            detailBtnRT.offsetMax = Vector2.zero;

            Button splitBtn = UIFactory.CreateButton(contentParent, "Split", "Diviser", 11);
            RectTransform splitBtnRT = splitBtn.GetComponent<RectTransform>();
            splitBtnRT.anchorMin = new Vector2(0.24f, 0.02f);
            splitBtnRT.anchorMax = new Vector2(0.44f, 0.30f);
            splitBtnRT.offsetMin = Vector2.zero;
            splitBtnRT.offsetMax = Vector2.zero;

            Button mergeBtn = UIFactory.CreateButton(contentParent, "Merge", "Fusionner", 11);
            RectTransform mergeBtnRT = mergeBtn.GetComponent<RectTransform>();
            mergeBtnRT.anchorMin = new Vector2(0.46f, 0.02f);
            mergeBtnRT.anchorMax = new Vector2(0.66f, 0.30f);
            mergeBtnRT.offsetMin = Vector2.zero;
            mergeBtnRT.offsetMax = Vector2.zero;
        }

        public void Hide()
        {
            if (panelRT != null)
                panelRT.gameObject.SetActive(false);
        }

        private void ClearContent()
        {
            if (contentParent == null) return;
            for (int i = contentParent.childCount - 1; i >= 0; i--)
                Destroy(contentParent.GetChild(i).gameObject);
        }
    }
}
