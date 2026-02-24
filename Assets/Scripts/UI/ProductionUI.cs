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
        private GameObject addLinePickerPanel;

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

        private void RebuildContent()
        {
            if (contentArea == null) return;
            // Clear and rebuild
            Transform scrollParent = contentArea.transform.parent?.parent?.parent; // scroll -> viewport -> content
            if (scrollParent == null) return;
            
            // Destroy old scroll
            Transform parent = scrollParent.parent;
            Destroy(scrollParent.gameObject);
            
            // Destroy picker if open
            if (addLinePickerPanel != null) Destroy(addLinePickerPanel);
            
            // Refresh player faction
            playerFaction = campaignManager.GetPlayerFaction();
            BuildContent(parent);
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
            int totalFactories = playerFaction.militaryFactories;
            int available = totalFactories - assigned;
            
            RectTransform summaryRT = UIFactory.CreatePanel(content, "Summary", UIFactory.CardSurface);
            UIFactory.AddLayoutElement(summaryRT.gameObject, preferredHeight: 50);
            
            HorizontalLayoutGroup summaryHLG = UIFactory.AddHorizontalLayout(summaryRT.gameObject, 20f, new RectOffset(15, 15, 8, 8));
            summaryHLG.childControlWidth = false;
            summaryHLG.childControlHeight = true;

            Text mfText = UIFactory.CreateText(summaryRT, "MF", 
                $"Manufactures militaires: {assigned}/{totalFactories}", 
                15, TextAnchor.MiddleLeft, UIFactory.GoldAccent);
            UIFactory.AddLayoutElement(mfText.gameObject, preferredWidth: 300, preferredHeight: 34);

            Color availColor = available > 0 ? UIFactory.ActionGreen : UIFactory.SilverText;
            Text availText = UIFactory.CreateText(summaryRT, "Avail", 
                $"Disponibles: {available}", 
                14, TextAnchor.MiddleLeft, availColor);
            UIFactory.AddLayoutElement(availText.gameObject, preferredWidth: 150, preferredHeight: 34);

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
                string lineId = null;
                foreach (var line in lines)
                {
                    if (line.equipmentType == eqType)
                    {
                        factoriesAssigned += line.assignedFactories;
                        lineEff = line.efficiency;
                        lineId = line.lineId;
                    }
                }
                CreateEquipmentRow(content, eqType, factoriesAssigned, production, stock, demand, lineEff, lineId, available);
            }

            // === ACTION BUTTONS ===
            UIFactory.CreateSeparator(content);
            
            RectTransform btnRow = UIFactory.CreatePanel(content, "BtnRow", UIFactory.CardSurface);
            UIFactory.AddLayoutElement(btnRow.gameObject, preferredHeight: 50);
            HorizontalLayoutGroup btnHLG = UIFactory.AddHorizontalLayout(btnRow.gameObject, 12f, new RectOffset(15, 15, 8, 8));
            btnHLG.childControlWidth = false;
            btnHLG.childControlHeight = true;

            // "Add production line" button — opens equipment type picker
            bool canAdd = available > 0;
            Button addBtn = UIFactory.CreateGoldButton(btnRow, "AddLine", 
                canAdd ? "+ Ajouter ligne de production" : "⚠ Aucune manufacture disponible", 13, 
                () => ShowAddLinePicker(parent));
            UIFactory.AddLayoutElement(addBtn.gameObject, preferredWidth: 280, preferredHeight: 34);
            addBtn.interactable = canAdd;

            // "Adjust priorities" button — redistribute factories equally
            Button adjustBtn = UIFactory.CreateButton(btnRow, "AdjustPri", "⚖ Répartition automatique", 12, 
                () => AutoDistributeFactories());
            UIFactory.AddLayoutElement(adjustBtn.gameObject, preferredWidth: 220, preferredHeight: 34);
            adjustBtn.interactable = lines.Count > 0;
        }

        // ═══════════════════════════════════════════════════════
        //  ADD PRODUCTION LINE PICKER
        // ═══════════════════════════════════════════════════════

        private void ShowAddLinePicker(Transform overlayParent)
        {
            if (addLinePickerPanel != null) Destroy(addLinePickerPanel);

            // Create picker overlay
            RectTransform pickerBg = UIFactory.CreateBorderedPanel(overlayParent, "AddLinePicker",
                UIFactory.DeepCharcoal, UIFactory.EmpireGold, 1.5f);
            pickerBg.anchorMin = new Vector2(0.25f, 0.2f);
            pickerBg.anchorMax = new Vector2(0.75f, 0.8f);
            pickerBg.offsetMin = Vector2.zero;
            pickerBg.offsetMax = Vector2.zero;
            addLinePickerPanel = pickerBg.gameObject;

            // Inner layout
            VerticalLayoutGroup vlg = pickerBg.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2f;
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;

            // Header
            RectTransform header = UIFactory.CreateBannerHeader(pickerBg, "Header", "Nouvelle Ligne de Production", 18);
            UIFactory.AddLayoutElement(header.gameObject, preferredHeight: 44);

            // Close button
            Button closeBtn = UIFactory.CreateButton(header, "Close", "✕", 14, 
                () => { Destroy(addLinePickerPanel); addLinePickerPanel = null; });
            RectTransform closeBtnRT = closeBtn.GetComponent<RectTransform>();
            closeBtnRT.anchorMin = new Vector2(1, 0);
            closeBtnRT.anchorMax = Vector2.one;
            closeBtnRT.offsetMin = new Vector2(-36, 2);
            closeBtnRT.offsetMax = new Vector2(-4, -2);
            closeBtn.GetComponent<Image>().color = new Color(0.25f, 0.06f, 0.06f, 0.85f);

            // Instruction
            Text instr = UIFactory.CreateText(pickerBg, "Instr",
                "  Sélectionnez un type d'équipement à produire. 1 manufacture sera assignée.", 
                11, TextAnchor.MiddleLeft, UIFactory.SilverText);
            UIFactory.AddLayoutElement(instr.gameObject, preferredHeight: 28);

            // Scroll for equipment list
            var (scroll, scrollContent) = UIFactory.CreateScrollView(pickerBg, "PickerScroll");
            UIFactory.AddLayoutElement(scroll.gameObject, preferredHeight: 400, flexibleHeight: 1);

            // Get existing lines to show what's already being produced
            var existingLines = ProductionManager.GetProductionLines(playerFaction.factionType);
            var producingTypes = new HashSet<EquipmentType>();
            foreach (var line in existingLines) producingTypes.Add(line.equipmentType);

            // List all equipment types
            foreach (EquipmentType eqType in System.Enum.GetValues(typeof(EquipmentType)))
            {
                EquipmentType capturedType = eqType;
                bool alreadyProducing = producingTypes.Contains(eqType);

                GameObject row = new GameObject($"Pick_{eqType}");
                row.transform.SetParent(scrollContent, false);
                Image rowBg = row.AddComponent<Image>();
                rowBg.color = alreadyProducing 
                    ? new Color(0.08f, 0.07f, 0.05f, 0.95f) 
                    : UIFactory.CardSurface;
                
                UIFactory.AddHorizontalLayout(row, 10f, new RectOffset(12, 12, 6, 6));
                UIFactory.AddLayoutElement(row, preferredHeight: 42);

                // Equipment icon + name
                string icon = GetEquipmentIcon(eqType);
                Text nameTxt = UIFactory.CreateText(row.transform, "Name", 
                    $"{icon}  {GetEquipmentName(eqType)}", 14, TextAnchor.MiddleLeft, 
                    alreadyProducing ? UIFactory.EmpireGold : UIFactory.Porcelain);
                nameTxt.fontStyle = FontStyle.Bold;
                UIFactory.AddLayoutElement(nameTxt.gameObject, flexibleWidth: 1);

                if (alreadyProducing)
                {
                    Text status = UIFactory.CreateText(row.transform, "Status", 
                        "EN PRODUCTION ✓", 11, TextAnchor.MiddleRight, UIFactory.ActionGreen);
                    UIFactory.AddLayoutElement(status.gameObject, preferredWidth: 130);

                    // Add extra factory button
                    Button extraBtn = UIFactory.CreateButton(row.transform, "Extra", "+1 Manuf.", 10, () =>
                    {
                        // Find existing line and increment
                        var currentLines = ProductionManager.GetProductionLines(playerFaction.factionType);
                        foreach (var line in currentLines)
                        {
                            if (line.equipmentType == capturedType)
                            {
                                line.assignedFactories++;
                                break;
                            }
                        }
                        Destroy(addLinePickerPanel);
                        addLinePickerPanel = null;
                        RebuildContent();
                    });
                    UIFactory.AddLayoutElement(extraBtn.gameObject, preferredWidth: 90, preferredHeight: 28);
                }
                else
                {
                    // Add new line button
                    Button addBtn = UIFactory.CreateGoldButton(row.transform, "Add", "PRODUIRE", 12, () =>
                    {
                        ProductionManager.AddProductionLine(playerFaction.factionType, capturedType, 1);
                        Destroy(addLinePickerPanel);
                        addLinePickerPanel = null;
                        RebuildContent();
                    });
                    UIFactory.AddLayoutElement(addBtn.gameObject, preferredWidth: 100, preferredHeight: 28);
                }
            }
        }

        // ═══════════════════════════════════════════════════════
        //  AUTO-DISTRIBUTE FACTORIES
        // ═══════════════════════════════════════════════════════

        private void AutoDistributeFactories()
        {
            var lines = ProductionManager.GetProductionLines(playerFaction.factionType);
            if (lines.Count == 0) return;

            int totalFactories = playerFaction.militaryFactories;
            int perLine = Mathf.Max(1, totalFactories / lines.Count);
            int remainder = totalFactories - (perLine * lines.Count);

            for (int i = 0; i < lines.Count; i++)
            {
                lines[i].assignedFactories = perLine + (i < remainder ? 1 : 0);
            }

            RebuildContent();
        }

        // ═══════════════════════════════════════════════════════
        //  TABLE HEADER
        // ═══════════════════════════════════════════════════════

        private void CreateTableHeader(Transform parent)
        {
            RectTransform headerRT = UIFactory.CreatePanel(parent, "TableHeader", UIFactory.HeaderSurface);
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
            AddHeaderCell(headerRT, "Statut", 80);
            AddHeaderCell(headerRT, "Actions", 100);
        }

        private void AddHeaderCell(Transform parent, string label, float width)
        {
            Text text = UIFactory.CreateText(parent, "H_" + label, label, 11, TextAnchor.MiddleCenter, UIFactory.GoldAccent);
            text.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(text.gameObject, preferredWidth: width, preferredHeight: 22);
        }

        // ═══════════════════════════════════════════════════════
        //  EQUIPMENT ROWS — with +/- factory buttons
        // ═══════════════════════════════════════════════════════

        private void CreateEquipmentRow(Transform parent, EquipmentType type, int factories, 
            float production, float stock, float demand, float efficiency, string lineId, int availableFactories)
        {
            bool hasDeficit = stock < demand && production < demand * 0.05f;
            Color rowBg = hasDeficit 
                ? new Color(0.15f, 0.04f, 0.04f, 0.90f) 
                : UIFactory.CardSurface;

            RectTransform rowRT = UIFactory.CreatePanel(parent, "Row_" + type, rowBg);
            UIFactory.AddLayoutElement(rowRT.gameObject, preferredHeight: 34);

            HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(rowRT.gameObject, 4f, new RectOffset(15, 15, 2, 2));
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            // Equipment name
            Text nameText = UIFactory.CreateText(rowRT, "Name", $"{GetEquipmentIcon(type)} {GetEquipmentName(type)}", 13, 
                TextAnchor.MiddleLeft, factories > 0 ? UIFactory.Porcelain : UIFactory.SilverText);
            UIFactory.AddLayoutElement(nameText.gameObject, preferredWidth: 180, preferredHeight: 28);

            // Factories assigned
            Text factText = UIFactory.CreateText(rowRT, "Fact", factories > 0 ? factories.ToString() : "-", 13, 
                TextAnchor.MiddleCenter, factories > 0 ? UIFactory.GoldAccent : UIFactory.SilverText);
            UIFactory.AddLayoutElement(factText.gameObject, preferredWidth: 60, preferredHeight: 28);

            // Efficiency
            Text effText = UIFactory.CreateText(rowRT, "Eff", factories > 0 ? $"{(int)(efficiency * 100)}%" : "-", 12, 
                TextAnchor.MiddleCenter, UIFactory.ParchmentBeige);
            UIFactory.AddLayoutElement(effText.gameObject, preferredWidth: 55, preferredHeight: 28);

            // Per turn
            Text perTurnText = UIFactory.CreateText(rowRT, "PerTurn", production > 0 ? $"{production:F0}" : "-", 13, 
                TextAnchor.MiddleCenter, production > 0 ? new Color(0.4f, 0.85f, 0.4f) : UIFactory.SilverText);
            UIFactory.AddLayoutElement(perTurnText.gameObject, preferredWidth: 70, preferredHeight: 28);

            // Stock
            Text stockText = UIFactory.CreateText(rowRT, "Stock", $"{stock:F0}", 13, 
                TextAnchor.MiddleCenter, UIFactory.Porcelain);
            UIFactory.AddLayoutElement(stockText.gameObject, preferredWidth: 80, preferredHeight: 28);

            // Demand
            Text demandText = UIFactory.CreateText(rowRT, "Demand", $"{demand:F0}", 13, 
                TextAnchor.MiddleCenter, UIFactory.Porcelain);
            UIFactory.AddLayoutElement(demandText.gameObject, preferredWidth: 80, preferredHeight: 28);

            // Status
            string status;
            Color statusColor;
            if (demand <= 0) { status = "—"; statusColor = UIFactory.SilverText; }
            else if (stock >= demand) { status = "✓ OK"; statusColor = new Color(0.3f, 0.85f, 0.3f); }
            else if (production > 0) { status = "EN COURS"; statusColor = UIFactory.WarningAmber; }
            else { status = "MANQUE!"; statusColor = UIFactory.DangerRed; }

            Text statusText = UIFactory.CreateText(rowRT, "Status", status, 11, 
                TextAnchor.MiddleCenter, statusColor);
            statusText.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(statusText.gameObject, preferredWidth: 80, preferredHeight: 28);

            // === ACTION BUTTONS (+/- factories) ===
            if (factories > 0 && lineId != null)
            {
                // Inline +/- buttons
                string capturedLineId = lineId;
                EquipmentType capturedType = type;

                // "-" button (remove 1 factory or remove line)
                Button minusBtn = UIFactory.CreateButton(rowRT, "Minus", "−", 14, () =>
                {
                    var currentLines = ProductionManager.GetProductionLines(playerFaction.factionType);
                    foreach (var line in currentLines)
                    {
                        if (line.lineId == capturedLineId)
                        {
                            if (line.assignedFactories > 1)
                                line.assignedFactories--;
                            else
                                ProductionManager.RemoveProductionLine(playerFaction.factionType, capturedLineId);
                            break;
                        }
                    }
                    RebuildContent();
                });
                UIFactory.AddLayoutElement(minusBtn.gameObject, preferredWidth: 28, preferredHeight: 24);
                minusBtn.GetComponent<Image>().color = new Color(0.20f, 0.06f, 0.06f, 0.9f);

                // "+" button (add 1 factory if available)
                Button plusBtn = UIFactory.CreateButton(rowRT, "Plus", "+", 14, () =>
                {
                    var currentLines = ProductionManager.GetProductionLines(playerFaction.factionType);
                    foreach (var line in currentLines)
                    {
                        if (line.lineId == capturedLineId)
                        {
                            line.assignedFactories++;
                            break;
                        }
                    }
                    RebuildContent();
                });
                UIFactory.AddLayoutElement(plusBtn.gameObject, preferredWidth: 28, preferredHeight: 24);
                plusBtn.interactable = availableFactories > 0;
                plusBtn.GetComponent<Image>().color = new Color(0.06f, 0.15f, 0.06f, 0.9f);
            }
            else
            {
                // Empty space placeholder for alignment
                GameObject spacer = new GameObject("ActionSpacer");
                spacer.transform.SetParent(rowRT, false);
                spacer.AddComponent<RectTransform>();
                UIFactory.AddLayoutElement(spacer, preferredWidth: 60);
            }
        }

        // ═══════════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════════

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

        private static string GetEquipmentIcon(EquipmentType type) => type switch
        {
            EquipmentType.Muskets => "🔫",
            EquipmentType.Bayonets => "🗡",
            EquipmentType.Sabres => "⚔",
            EquipmentType.CannonsLight => "💣",
            EquipmentType.CannonsHeavy => "💥",
            EquipmentType.CannonsSiege => "🏰",
            EquipmentType.Horses => "🐎",
            EquipmentType.Uniforms => "👔",
            EquipmentType.Gunpowder => "🧨",
            EquipmentType.Cannonballs => "⚫",
            _ => "❓"
        };

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
