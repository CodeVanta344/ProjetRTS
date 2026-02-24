using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Campaign;
using NapoleonicWars.Data;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// HoI4-style Production panel — full-screen overlay showing factory assignments,
    /// equipment production rates, stockpile vs demand.
    /// </summary>
    public class ProductionUI : MonoBehaviour
    {
        private CampaignManager campaignManager;
        private FactionData playerFaction;
        private NavigationBar navBar;
        private RectTransform contentArea;

        public static ProductionUI Create(NavigationBar navBar, CampaignManager manager)
        {
            var overlay = navBar.CreateOverlayPanel("PRODUCTION MILITAIRE");
            if (overlay == null) return null;

            var ui = overlay.AddComponent<ProductionUI>();
            ui.navBar = navBar;
            ui.campaignManager = manager;
            ui.playerFaction = manager.GetPlayerFaction();
            navBar.RegisterPanel(NavigationBar.NavPanel.Production, overlay);

            ui.BuildContent(overlay.transform);
            return ui;
        }

        private void BuildContent(Transform parent)
        {
            // Scroll view for content
            var (scroll, content) = UIFactory.CreateScrollView(parent, "ProductionScroll");
            RectTransform scrollRT = scroll.GetComponent<RectTransform>();
            UIFactory.AddLayoutElement(scroll.gameObject, flexibleHeight: 1, preferredHeight: 900);
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
            contentArea = content;

            // === SUMMARY HEADER ===
            int assigned = ProductionManager.GetAssignedMilitaryFactories(playerFaction.factionType);
            RectTransform summaryRT = UIFactory.CreatePanel(content, "Summary", new Color(0.10f, 0.07f, 0.05f, 0.95f));
            UIFactory.AddLayoutElement(summaryRT.gameObject, preferredHeight: 50);
            
            HorizontalLayoutGroup summaryHLG = UIFactory.AddHorizontalLayout(summaryRT.gameObject, 20f, new RectOffset(15, 15, 8, 8));
            summaryHLG.childControlWidth = false;
            summaryHLG.childControlHeight = true;

            Text mfText = UIFactory.CreateText(summaryRT, "MF", 
                $"Manufactures militaires: {assigned}/{playerFaction.militaryFactories}", 
                15, TextAnchor.MiddleLeft, UIFactory.GoldAccent);
            UIFactory.AddLayoutElement(mfText.gameObject, preferredWidth: 300, preferredHeight: 34);

            float efficiency = GetAverageEfficiency();
            Text effText = UIFactory.CreateText(summaryRT, "Eff", 
                $"Efficacité moyenne: {(int)(efficiency * 100)}%", 
                14, TextAnchor.MiddleLeft, UIFactory.ParchmentBeige);
            UIFactory.AddLayoutElement(effText.gameObject, preferredWidth: 250, preferredHeight: 34);

            // === TABLE HEADER ===
            CreateTableHeader(content);

            // === PRODUCTION LINES ===
            var lines = ProductionManager.GetProductionLines(playerFaction.factionType);
            Dictionary<string, ArmyData> armies = GetArmies();
            var summary = ProductionManager.GetEquipmentSummary(playerFaction, armies);

            // Show all equipment types (even ones without production)
            foreach (EquipmentType eqType in System.Enum.GetValues(typeof(EquipmentType)))
            {
                var (stock, demand, production) = summary.ContainsKey(eqType) ? summary[eqType] : (0f, 0f, 0f);
                int factoriesAssigned = 0;
                float lineEff = 0f;
                foreach (var line in lines)
                {
                    if (line.equipmentType == eqType)
                    {
                        factoriesAssigned += line.assignedFactories;
                        lineEff = line.efficiency;
                    }
                }
                CreateEquipmentRow(content, eqType, factoriesAssigned, production, stock, demand, lineEff);
            }

            // === ADD LINE BUTTON ===
            RectTransform btnRow = UIFactory.CreatePanel(content, "BtnRow", new Color(0.12f, 0.13f, 0.11f, 0.9f));
            UIFactory.AddLayoutElement(btnRow.gameObject, preferredHeight: 45);
            HorizontalLayoutGroup btnHLG = UIFactory.AddHorizontalLayout(btnRow.gameObject, 10f, new RectOffset(15, 15, 6, 6));
            btnHLG.childControlWidth = false;
            btnHLG.childControlHeight = true;

            Button addBtn = UIFactory.CreateGoldButton(btnRow, "AddLine", "+ Ajouter ligne de production", 13);
            UIFactory.AddLayoutElement(addBtn.gameObject, preferredWidth: 250, preferredHeight: 32);

            Button adjustBtn = UIFactory.CreateButton(btnRow, "AdjustPri", "Ajuster priorités", 12);
            UIFactory.AddLayoutElement(adjustBtn.gameObject, preferredWidth: 180, preferredHeight: 32);
        }

        private void CreateTableHeader(Transform parent)
        {
            RectTransform headerRT = UIFactory.CreatePanel(parent, "TableHeader", new Color(0.16f, 0.18f, 0.15f, 0.95f));
            UIFactory.AddLayoutElement(headerRT.gameObject, preferredHeight: 30);

            HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(headerRT.gameObject, 4f, new RectOffset(15, 15, 4, 4));
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            AddHeaderCell(headerRT, "Équipement", 180);
            AddHeaderCell(headerRT, "Manuf.", 60);
            AddHeaderCell(headerRT, "Eff.", 55);
            AddHeaderCell(headerRT, "/Tour", 70);
            AddHeaderCell(headerRT, "Stock", 80);
            AddHeaderCell(headerRT, "Demande", 80);
            AddHeaderCell(headerRT, "Statut", 100);
        }

        private void AddHeaderCell(Transform parent, string label, float width)
        {
            Text text = UIFactory.CreateText(parent, "H_" + label, label, 11, TextAnchor.MiddleCenter, UIFactory.GoldAccent);
            text.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(text.gameObject, preferredWidth: width, preferredHeight: 22);
        }

        private void CreateEquipmentRow(Transform parent, EquipmentType type, int factories, 
            float production, float stock, float demand, float efficiency)
        {
            bool hasDeficit = stock < demand && production < demand * 0.05f;
            Color rowBg = hasDeficit ? new Color(0.20f, 0.06f, 0.06f, 0.90f) : new Color(0.12f, 0.13f, 0.11f, 0.90f);

            RectTransform rowRT = UIFactory.CreatePanel(parent, "Row_" + type, rowBg);
            UIFactory.AddLayoutElement(rowRT.gameObject, preferredHeight: 32);

            HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(rowRT.gameObject, 4f, new RectOffset(15, 15, 2, 2));
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            // Equipment name
            Text nameText = UIFactory.CreateText(rowRT, "Name", GetEquipmentName(type), 13, 
                TextAnchor.MiddleLeft, UIFactory.TextWhite);
            UIFactory.AddLayoutElement(nameText.gameObject, preferredWidth: 180, preferredHeight: 28);

            // Factories assigned
            Text factText = UIFactory.CreateText(rowRT, "Fact", factories > 0 ? factories.ToString() : "-", 13, 
                TextAnchor.MiddleCenter, factories > 0 ? UIFactory.GoldAccent : UIFactory.TextGrey);
            UIFactory.AddLayoutElement(factText.gameObject, preferredWidth: 60, preferredHeight: 28);

            // Efficiency
            Text effText = UIFactory.CreateText(rowRT, "Eff", factories > 0 ? $"{(int)(efficiency * 100)}%" : "-", 12, 
                TextAnchor.MiddleCenter, UIFactory.ParchmentBeige);
            UIFactory.AddLayoutElement(effText.gameObject, preferredWidth: 55, preferredHeight: 28);

            // Per turn
            Text perTurnText = UIFactory.CreateText(rowRT, "PerTurn", production > 0 ? $"{production:F0}" : "-", 13, 
                TextAnchor.MiddleCenter, new Color(0.5f, 0.9f, 0.5f));
            UIFactory.AddLayoutElement(perTurnText.gameObject, preferredWidth: 70, preferredHeight: 28);

            // Stock
            Text stockText = UIFactory.CreateText(rowRT, "Stock", $"{stock:F0}", 13, 
                TextAnchor.MiddleCenter, UIFactory.TextWhite);
            UIFactory.AddLayoutElement(stockText.gameObject, preferredWidth: 80, preferredHeight: 28);

            // Demand
            Text demandText = UIFactory.CreateText(rowRT, "Demand", $"{demand:F0}", 13, 
                TextAnchor.MiddleCenter, UIFactory.TextWhite);
            UIFactory.AddLayoutElement(demandText.gameObject, preferredWidth: 80, preferredHeight: 28);

            // Status
            string status;
            Color statusColor;
            if (demand <= 0) { status = "—"; statusColor = UIFactory.TextGrey; }
            else if (stock >= demand) { status = "OK ✓"; statusColor = new Color(0.3f, 0.9f, 0.3f); }
            else if (production > 0) { status = "EN COURS"; statusColor = new Color(0.9f, 0.8f, 0.2f); }
            else { status = "MANQUE!"; statusColor = new Color(1f, 0.3f, 0.3f); }

            Text statusText = UIFactory.CreateText(rowRT, "Status", status, 12, 
                TextAnchor.MiddleCenter, statusColor);
            statusText.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(statusText.gameObject, preferredWidth: 100, preferredHeight: 28);
        }

        private float GetAverageEfficiency()
        {
            var lines = ProductionManager.GetProductionLines(playerFaction.factionType);
            if (lines.Count == 0) return 0f;
            float total = 0f;
            foreach (var l in lines) total += l.efficiency;
            return total / lines.Count;
        }

        private Dictionary<string, ArmyData> GetArmies()
        {
            return campaignManager.Armies ?? new Dictionary<string, ArmyData>();
        }

        public static string GetEquipmentName(EquipmentType type) => type switch
        {
            EquipmentType.Muskets => "Mousquets",
            EquipmentType.Bayonets => "Baïonnettes",
            EquipmentType.Sabres => "Sabres",
            EquipmentType.CannonsLight => "Canons légers",
            EquipmentType.CannonsHeavy => "Canons lourds",
            EquipmentType.CannonsSiege => "Canons de siège",
            EquipmentType.Horses => "Chevaux",
            EquipmentType.Uniforms => "Uniformes",
            EquipmentType.Gunpowder => "Poudre",
            EquipmentType.Cannonballs => "Boulets",
            _ => type.ToString()
        };
    }
}
