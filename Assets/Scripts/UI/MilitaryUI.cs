using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Campaign;
using NapoleonicWars.Data;
using NapoleonicWars.Units;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// HoI4-style Military panel with 4 sub-tabs: Armies, Division Designer, Recruitment, Generals.
    /// </summary>
    public class MilitaryUI : MonoBehaviour
    {
        private CampaignManager campaignManager;
        private FactionData playerFaction;
        private NavigationBar navBar;
        private Transform contentParent;
        private int activeTab = 0;

        // Tab content containers
        private GameObject armiesContent;
        private GameObject designerContent;
        private GameObject recruitmentContent;
        private GameObject generalsContent;

        public static MilitaryUI Create(NavigationBar navBar, CampaignManager manager)
        {
            var overlay = navBar.CreateOverlayPanel("MILITAIRE");
            if (overlay == null) return null;

            var ui = overlay.AddComponent<MilitaryUI>();
            ui.navBar = navBar;
            ui.campaignManager = manager;
            ui.playerFaction = manager.GetPlayerFaction();
            navBar.RegisterPanel(NavigationBar.NavPanel.Military, overlay);

            ui.BuildContent(overlay.transform);
            return ui;
        }

        private void BuildContent(Transform parent)
        {
            contentParent = parent;

            // === TAB BAR ===
            RectTransform tabBar = UIFactory.CreatePanel(parent, "TabBar", new Color(0.10f, 0.07f, 0.05f, 0.95f));
            UIFactory.AddLayoutElement(tabBar.gameObject, preferredHeight: 38);
            
            HorizontalLayoutGroup tabHLG = UIFactory.AddHorizontalLayout(tabBar.gameObject, 4f, new RectOffset(10, 10, 4, 4));
            tabHLG.childControlWidth = false;
            tabHLG.childControlHeight = true;

            Button armiesBtn = UIFactory.CreateWarhammerButton(tabBar, "TabArmies", "⚔️ Armées", 13, () => SwitchTab(0));
            UIFactory.AddLayoutElement(armiesBtn.gameObject, preferredWidth: 160, preferredHeight: 30);
            
            Button designerBtn = UIFactory.CreateWarhammerButton(tabBar, "TabDesigner", "🔧 Concepteur", 13, () => SwitchTab(1));
            UIFactory.AddLayoutElement(designerBtn.gameObject, preferredWidth: 180, preferredHeight: 30);
            
            Button recruitBtn = UIFactory.CreateWarhammerButton(tabBar, "TabRecruit", "📋 Recrutement", 13, () => SwitchTab(2));
            UIFactory.AddLayoutElement(recruitBtn.gameObject, preferredWidth: 180, preferredHeight: 30);

            Button generalsBtn = UIFactory.CreateWarhammerButton(tabBar, "TabGenerals", "🎖️ Généraux", 13, () => SwitchTab(3));
            UIFactory.AddLayoutElement(generalsBtn.gameObject, preferredWidth: 160, preferredHeight: 30);

            // Manpower summary
            Text mpText = UIFactory.CreateText(tabBar, "Manpower", 
                $"Manpower: {playerFaction.manpower:N0} / {playerFaction.maxManpower:N0}", 
                12, TextAnchor.MiddleRight, new Color(0.7f, 0.9f, 0.7f));
            UIFactory.AddLayoutElement(mpText.gameObject, preferredWidth: 250, flexibleWidth: 1, preferredHeight: 30);

            // === CONTENT AREA ===
            BuildArmiesTab(parent);
            BuildDesignerTab(parent);
            BuildRecruitmentTab(parent);
            BuildGeneralsTab(parent);

            SwitchTab(0);
        }

        private void SwitchTab(int tab)
        {
            activeTab = tab;
            if (armiesContent != null) armiesContent.SetActive(tab == 0);
            if (designerContent != null) designerContent.SetActive(tab == 1);
            if (recruitmentContent != null) recruitmentContent.SetActive(tab == 2);
            if (generalsContent != null) generalsContent.SetActive(tab == 3);
        }

        // ================================ TAB 0: ARMIES ================================
        private void BuildArmiesTab(Transform parent)
        {
            var (scroll, content) = UIFactory.CreateScrollView(parent, "ArmiesScroll");
            UIFactory.AddLayoutElement(scroll.gameObject, flexibleHeight: 1, preferredHeight: 800);
            armiesContent = scroll.gameObject;

            var armies = GetArmies();
            if (armies.Count == 0)
            {
                RectTransform emptyRT = UIFactory.CreatePanel(content, "Empty", new Color(0.12f, 0.13f, 0.11f, 0.9f));
                UIFactory.AddLayoutElement(emptyRT.gameObject, preferredHeight: 50);
                Text emptyText = UIFactory.CreateText(emptyRT, "Text", "Aucune armée. Recrutez des divisions !", 
                    14, TextAnchor.MiddleCenter, UIFactory.TextGrey);
                RectTransform emptyRT2 = emptyText.GetComponent<RectTransform>();
                emptyRT2.anchorMin = Vector2.zero; emptyRT2.anchorMax = Vector2.one;
                emptyRT2.offsetMin = Vector2.zero; emptyRT2.offsetMax = Vector2.zero;
                return;
            }

            foreach (var kvp in armies)
            {
                ArmyData army = kvp.Value;
                if (army.faction != playerFaction.factionType) continue;
                CreateArmyCard(content, army);
            }
        }

        private void CreateArmyCard(Transform parent, ArmyData army)
        {
            RectTransform cardRT = UIFactory.CreateOrnatePanel(parent, $"Army_{army.armyId}", UIFactory.PanelBg);
            UIFactory.AddLayoutElement(cardRT.gameObject, preferredHeight: 120);

            Transform inner = cardRT.transform.Find("Inner"); // Inner content

            // Army name + general
            string generalStr = !string.IsNullOrEmpty(army.generalId) ? $" (Gén. {army.generalId})" : "";
            Text nameText = UIFactory.CreateText(inner, "Name", $"⚔️ {army.armyName}{generalStr}", 15, TextAnchor.UpperLeft, UIFactory.GoldAccent);
            nameText.fontStyle = FontStyle.Bold;
            RectTransform nameRT = nameText.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0, 0.72f);
            nameRT.anchorMax = new Vector2(0.5f, 1);
            nameRT.offsetMin = new Vector2(10, 0);
            nameRT.offsetMax = new Vector2(0, -5);

            // Stats line
            int soldiers = army.TotalSoldiers;
            string soldiersStr = soldiers >= 1000 ? $"{soldiers / 1000f:F1}K" : $"{soldiers}";
            Text statsText = UIFactory.CreateText(inner, "Stats", 
                $"{soldiersStr} hommes | {army.regiments.Count} rég. | Org: {army.organization:F0}% | Entretien: {army.MaintenanceCost:F0}g/t", 
                11, TextAnchor.MiddleLeft, UIFactory.ParchmentBeige);
            RectTransform statsRT = statsText.GetComponent<RectTransform>();
            statsRT.anchorMin = new Vector2(0, 0.48f);
            statsRT.anchorMax = new Vector2(0.68f, 0.72f);
            statsRT.offsetMin = new Vector2(10, 0);
            statsRT.offsetMax = Vector2.zero;

            // Location (province name instead of raw ID)
            string locName = campaignManager != null ? campaignManager.GetProvinceName(army.currentProvinceId) : army.currentProvinceId;
            Text locText = UIFactory.CreateText(inner, "Location", 
                $"📍 {locName}  |  Mvt: {army.movementPoints}/{army.maxMovementPoints}", 
                11, TextAnchor.MiddleLeft, UIFactory.TextGrey);
            RectTransform locRT = locText.GetComponent<RectTransform>();
            locRT.anchorMin = new Vector2(0, 0.24f);
            locRT.anchorMax = new Vector2(0.50f, 0.48f);
            locRT.offsetMin = new Vector2(10, 0);
            locRT.offsetMax = Vector2.zero;

            // Regiment composition + rank summary
            int maxRank = 0;
            int inf = 0, cav = 0, art = 0, elite = 0;
            foreach (var reg in army.regiments)
            {
                if (reg.rank > maxRank) maxRank = reg.rank;
                switch (reg.unitType)
                {
                    case UnitType.LineInfantry: case UnitType.LightInfantry: case UnitType.Grenadier: inf++; break;
                    case UnitType.Cavalry: case UnitType.Hussar: case UnitType.Lancer: cav++; break;
                    case UnitType.Artillery: art++; break;
                    case UnitType.ImperialGuard: case UnitType.GuardCavalry: case UnitType.GuardArtillery: elite++; break;
                }
            }
            string compStr = $"⚔{inf} 🐎{cav} 💥{art}";
            if (elite > 0) compStr += $" ⭐{elite}";
            string rankStr = maxRank > 0 ? $" | {RegimentRankSystem.GetRankName(maxRank)}" : "";
            Text compText = UIFactory.CreateText(inner, "Comp", 
                $"{compStr}{rankStr}", 
                10, TextAnchor.MiddleLeft, maxRank >= 5 ? new Color(0.6f, 0.4f, 1f) : UIFactory.TextGrey);
            RectTransform compRT = compText.GetComponent<RectTransform>();
            compRT.anchorMin = new Vector2(0, 0);
            compRT.anchorMax = new Vector2(0.50f, 0.24f);
            compRT.offsetMin = new Vector2(10, 5);
            compRT.offsetMax = Vector2.zero;

            // Details button
            Button detailsBtn = UIFactory.CreateGoldButton(inner, "Details", "Détails →", 12);
            RectTransform detailsBtnRT = detailsBtn.GetComponent<RectTransform>();
            detailsBtnRT.anchorMin = new Vector2(0.7f, 0.25f);
            detailsBtnRT.anchorMax = new Vector2(0.95f, 0.75f);
            detailsBtnRT.offsetMin = Vector2.zero;
            detailsBtnRT.offsetMax = Vector2.zero;

            // Organization bar
            var (bg, fill) = UIFactory.CreateProgressBar(inner, "OrgBar", new Color(0.3f, 0.7f, 0.3f));
            RectTransform bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0.52f, 0.75f);
            bgRT.anchorMax = new Vector2(0.68f, 0.92f);
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            fill.GetComponent<RectTransform>().anchorMax = new Vector2(army.organization / army.maxOrganization, 1);
        }

        // ================================ TAB 1: DIVISION DESIGNER ================================
        private void BuildDesignerTab(Transform parent)
        {
            var (scroll, content) = UIFactory.CreateScrollView(parent, "DesignerScroll");
            UIFactory.AddLayoutElement(scroll.gameObject, flexibleHeight: 1, preferredHeight: 800);
            designerContent = scroll.gameObject;

            var templates = DivisionDesigner.GetTemplates(playerFaction.factionType);
            
            if (templates.Count == 0)
            {
                DivisionDesigner.CreateDefaultTemplates(playerFaction.factionType);
                templates = DivisionDesigner.GetTemplates(playerFaction.factionType);
            }

            foreach (var template in templates)
            {
                CreateTemplateCard(content, template);
            }

            // Add template button
            RectTransform btnRow = UIFactory.CreatePanel(content, "BtnRow", new Color(0.12f, 0.13f, 0.11f, 0.9f));
            UIFactory.AddLayoutElement(btnRow.gameObject, preferredHeight: 45);
            
            Button addBtn = UIFactory.CreateGoldButton(btnRow, "AddTemplate", "+ Nouveau template", 14);
            RectTransform addBtnRT = addBtn.GetComponent<RectTransform>();
            addBtnRT.anchorMin = new Vector2(0.05f, 0.1f);
            addBtnRT.anchorMax = new Vector2(0.35f, 0.9f);
            addBtnRT.offsetMin = Vector2.zero;
            addBtnRT.offsetMax = Vector2.zero;
        }

        private void CreateTemplateCard(Transform parent, DivisionTemplate template)
        {
            RectTransform cardRT = UIFactory.CreateBorderedPanel(parent, $"Tmpl_{template.templateId}", 
                new Color(0.09f, 0.07f, 0.05f, 0.95f), UIFactory.BorderGold, 1.5f);
            UIFactory.AddLayoutElement(cardRT.gameObject, preferredHeight: 160);

            Transform inner = cardRT.transform.GetChild(0);

            // Template name
            Text nameText = UIFactory.CreateText(inner, "Name", $"🔧 {template.templateName}", 14, TextAnchor.UpperLeft, UIFactory.GoldAccent);
            nameText.fontStyle = FontStyle.Bold;
            RectTransform nameRT = nameText.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0, 0.85f);
            nameRT.anchorMax = new Vector2(0.6f, 1);
            nameRT.offsetMin = new Vector2(10, 0);
            nameRT.offsetMax = new Vector2(0, -5);

            // === BATTALION GRID (left side) ===
            float gridStartX = 10;
            float gridStartY = -30;
            int col = 0, row = 0;
            float cellSize = 42f;
            foreach (var slot in template.battalions)
            {
                for (int i = 0; i < slot.count; i++)
                {
                    RectTransform cellRT = UIFactory.CreateBorderedPanel(inner, $"Cell_{col}_{row}", 
                        GetBattalionColor(slot.type), UIFactory.BorderGold, 1f);
                    cellRT.anchorMin = new Vector2(0, 1);
                    cellRT.anchorMax = new Vector2(0, 1);
                    cellRT.pivot = new Vector2(0, 1);
                    cellRT.anchoredPosition = new Vector2(gridStartX + col * (cellSize + 3), gridStartY - row * (cellSize + 3));
                    cellRT.sizeDelta = new Vector2(cellSize, cellSize);

                    Text cellText = UIFactory.CreateText(cellRT.transform.GetChild(0), "Text", 
                        DivisionTemplate.GetBattalionIcon(slot.type), 10, TextAnchor.MiddleCenter, UIFactory.TextWhite);
                    cellText.fontStyle = FontStyle.Bold;
                    RectTransform cellTextRT = cellText.GetComponent<RectTransform>();
                    cellTextRT.anchorMin = Vector2.zero;
                    cellTextRT.anchorMax = Vector2.one;
                    cellTextRT.offsetMin = Vector2.zero;
                    cellTextRT.offsetMax = Vector2.zero;

                    col++;
                    if (col >= 4) { col = 0; row++; }
                }
            }

            // === STATS (right side) ===
            float statsX = 0.55f;
            CreateStatLine(inner, "Attaque", $"{template.attack:F0}", statsX, 0.75f);
            CreateStatLine(inner, "Défense", $"{template.defense:F0}", statsX, 0.60f);
            CreateStatLine(inner, "Percée", $"{template.breakthrough:F0}", statsX, 0.45f);
            CreateStatLine(inner, "Largeur", $"{template.combatWidth}", statsX, 0.30f);
            CreateStatLine(inner, "Org.", $"{template.organization:F0}", statsX + 0.22f, 0.75f);
            CreateStatLine(inner, "Vitesse", $"{template.speed} km/h", statsX + 0.22f, 0.60f);
            CreateStatLine(inner, "Manpower", $"{template.manpowerCost:N0}", statsX + 0.22f, 0.45f);

            // Action buttons
            Button dupBtn = UIFactory.CreateButton(inner, "Dup", "Dupliquer", 10);
            RectTransform dupBtnRT = dupBtn.GetComponent<RectTransform>();
            dupBtnRT.anchorMin = new Vector2(0.55f, 0.02f);
            dupBtnRT.anchorMax = new Vector2(0.72f, 0.18f);
            dupBtnRT.offsetMin = Vector2.zero;
            dupBtnRT.offsetMax = Vector2.zero;

            Button delBtn = UIFactory.CreateButton(inner, "Del", "Supprimer", 10);
            RectTransform delBtnRT = delBtn.GetComponent<RectTransform>();
            delBtnRT.anchorMin = new Vector2(0.74f, 0.02f);
            delBtnRT.anchorMax = new Vector2(0.95f, 0.18f);
            delBtnRT.offsetMin = Vector2.zero;
            delBtnRT.offsetMax = Vector2.zero;
        }

        private void CreateStatLine(Transform parent, string label, string value, float xStart, float yCenter)
        {
            Text statText = UIFactory.CreateText(parent, $"Stat_{label}", $"{label}: <color=#E5C850>{value}</color>", 
                11, TextAnchor.MiddleLeft, UIFactory.ParchmentBeige);
            RectTransform statRT = statText.GetComponent<RectTransform>();
            statRT.anchorMin = new Vector2(xStart, yCenter - 0.08f);
            statRT.anchorMax = new Vector2(xStart + 0.20f, yCenter + 0.08f);
            statRT.offsetMin = Vector2.zero;
            statRT.offsetMax = Vector2.zero;
        }

        private Color GetBattalionColor(BattalionType type) => type switch
        {
            BattalionType.Infantry or BattalionType.LightInfantry => new Color(0.20f, 0.30f, 0.50f, 0.95f),
            BattalionType.Grenadier or BattalionType.Guard => new Color(0.50f, 0.25f, 0.15f, 0.95f),
            BattalionType.Artillery or BattalionType.HeavyArtillery => new Color(0.40f, 0.35f, 0.15f, 0.95f),
            BattalionType.Cavalry or BattalionType.Hussar or BattalionType.Lancer => new Color(0.20f, 0.40f, 0.25f, 0.95f),
            _ => new Color(0.25f, 0.25f, 0.25f, 0.95f)
        };

        // ================================ TAB 2: RECRUITMENT ================================
        private void BuildRecruitmentTab(Transform parent)
        {
            var (scroll, content) = UIFactory.CreateScrollView(parent, "RecruitScroll");
            UIFactory.AddLayoutElement(scroll.gameObject, flexibleHeight: 1, preferredHeight: 800);
            recruitmentContent = scroll.gameObject;

            // === UNDERSTRENGTH REGIMENTS ===
            CreateMilitarySectionLabel(content, "Régiments sous-effectifs (renforts nécessaires)");

            var armies = GetArmies();
            int understrengthCount = 0;
            foreach (var kvp in armies)
            {
                ArmyData army = kvp.Value;
                if (army.faction != playerFaction.factionType) continue;
                foreach (var reg in army.regiments)
                {
                    int maxSize = RegimentData.GetDefaultSize(reg.unitType);
                    if (reg.currentSize < maxSize)
                    {
                        understrengthCount++;
                        float pct = maxSize > 0 ? (float)reg.currentSize / maxSize : 0f;
                        Color rowColor = pct < 0.5f ? new Color(0.15f, 0.05f, 0.05f, 0.9f) : new Color(0.12f, 0.13f, 0.11f, 0.9f);
                        RectTransform row = UIFactory.CreatePanel(content, $"Under_{reg.regimentName}", rowColor);
                        UIFactory.AddLayoutElement(row.gameObject, preferredHeight: 32);
                        HorizontalLayoutGroup rowHLG = UIFactory.AddHorizontalLayout(row.gameObject, 8f, new RectOffset(15, 15, 3, 3));
                        rowHLG.childControlWidth = false;
                        rowHLG.childControlHeight = true;

                        Text regName = UIFactory.CreateText(row, "Name", reg.regimentName, 11, TextAnchor.MiddleLeft, UIFactory.TextWhite);
                        UIFactory.AddLayoutElement(regName.gameObject, preferredWidth: 220, preferredHeight: 26);

                        Text regSize = UIFactory.CreateText(row, "Size", 
                            $"{reg.currentSize}/{maxSize} ({(int)(pct * 100)}%)", 
                            11, TextAnchor.MiddleLeft, pct < 0.5f ? new Color(1f, 0.3f, 0.3f) : new Color(1f, 0.8f, 0.3f));
                        UIFactory.AddLayoutElement(regSize.gameObject, preferredWidth: 120, preferredHeight: 26);

                        Text regArmy = UIFactory.CreateText(row, "Army", army.armyName, 10, TextAnchor.MiddleLeft, UIFactory.TextGrey);
                        UIFactory.AddLayoutElement(regArmy.gameObject, preferredWidth: 180, preferredHeight: 26);

                        Button reinforceBtn = UIFactory.CreateButton(row, "Reinforce", "Renforcer", 10);
                        UIFactory.AddLayoutElement(reinforceBtn.gameObject, preferredWidth: 80, preferredHeight: 24);
                    }
                }
            }
            if (understrengthCount == 0)
            {
                Text noUnder = UIFactory.CreateText(content, "NoUnder", 
                    "Tous les régiments sont à plein effectif.", 12, TextAnchor.MiddleLeft, new Color(0.3f, 0.9f, 0.3f));
                UIFactory.AddLayoutElement(noUnder.gameObject, preferredHeight: 30);
            }

            // === RECRUIT NEW ===
            RectTransform btnRow = UIFactory.CreatePanel(content, "BtnRow", new Color(0.12f, 0.13f, 0.11f, 0.9f));
            UIFactory.AddLayoutElement(btnRow.gameObject, preferredHeight: 50);

            Button recruitBtn = UIFactory.CreateGoldButton(btnRow, "RecruitNew", "+ Recruter nouvelle division", 14);
            RectTransform recruitBtnRT = recruitBtn.GetComponent<RectTransform>();
            recruitBtnRT.anchorMin = new Vector2(0.05f, 0.1f);
            recruitBtnRT.anchorMax = new Vector2(0.45f, 0.9f);
            recruitBtnRT.offsetMin = Vector2.zero;
            recruitBtnRT.offsetMax = Vector2.zero;

            // === TEMPLATES ===
            CreateMilitarySectionLabel(content, "Templates disponibles pour recrutement");

            var templates = DivisionDesigner.GetTemplates(playerFaction.factionType);
            foreach (var t in templates)
            {
                RectTransform tmplRow = UIFactory.CreatePanel(content, $"TmplRow_{t.templateId}", new Color(0.12f, 0.13f, 0.11f, 0.9f));
                UIFactory.AddLayoutElement(tmplRow.gameObject, preferredHeight: 38);
                HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(tmplRow.gameObject, 10f, new RectOffset(15, 15, 4, 4));
                hlg.childControlWidth = false;
                hlg.childControlHeight = true;

                Text tmplName = UIFactory.CreateText(tmplRow, "Name", t.templateName, 12, TextAnchor.MiddleLeft, UIFactory.TextWhite);
                UIFactory.AddLayoutElement(tmplName.gameObject, preferredWidth: 280, preferredHeight: 30);

                Text tmplStats = UIFactory.CreateText(tmplRow, "Stats", 
                    $"{t.manpowerCost:N0} MP | {t.TotalBattalions} bat.", 
                    11, TextAnchor.MiddleLeft, UIFactory.TextGrey);
                UIFactory.AddLayoutElement(tmplStats.gameObject, preferredWidth: 180, preferredHeight: 30);

                Button recruitTemplateBtn = UIFactory.CreateButton(tmplRow, "Recruit", "Recruter", 11);
                UIFactory.AddLayoutElement(recruitTemplateBtn.gameObject, preferredWidth: 100, preferredHeight: 28);
            }
        }

        // ================================ TAB 3: GENERALS ================================
        private void BuildGeneralsTab(Transform parent)
        {
            var (scroll, content) = UIFactory.CreateScrollView(parent, "GeneralsScroll");
            UIFactory.AddLayoutElement(scroll.gameObject, flexibleHeight: 1, preferredHeight: 800);
            generalsContent = scroll.gameObject;

            var allGenerals = campaignManager.Generals;
            if (allGenerals == null || allGenerals.Count == 0)
            {
                RectTransform emptyRT = UIFactory.CreatePanel(content, "Empty", new Color(0.12f, 0.13f, 0.11f, 0.9f));
                UIFactory.AddLayoutElement(emptyRT.gameObject, preferredHeight: 50);
                Text emptyText = UIFactory.CreateText(emptyRT, "Text", "Aucun général disponible.",
                    14, TextAnchor.MiddleCenter, UIFactory.TextGrey);
                RectTransform eRT = emptyText.GetComponent<RectTransform>();
                eRT.anchorMin = Vector2.zero; eRT.anchorMax = Vector2.one;
                eRT.offsetMin = Vector2.zero; eRT.offsetMax = Vector2.zero;
                return;
            }

            // Collect player generals
            List<GeneralData> playerGenerals = new List<GeneralData>();
            foreach (var gen in allGenerals.Values)
                if (gen.faction == playerFaction.factionType && gen.isAlive) playerGenerals.Add(gen);

            if (playerGenerals.Count == 0)
            {
                RectTransform emptyRT = UIFactory.CreatePanel(content, "Empty", new Color(0.12f, 0.13f, 0.11f, 0.9f));
                UIFactory.AddLayoutElement(emptyRT.gameObject, preferredHeight: 50);
                Text emptyText = UIFactory.CreateText(emptyRT, "Text", "Aucun général vivant pour votre faction.",
                    14, TextAnchor.MiddleCenter, UIFactory.TextGrey);
                RectTransform eRT = emptyText.GetComponent<RectTransform>();
                eRT.anchorMin = Vector2.zero; eRT.anchorMax = Vector2.one;
                eRT.offsetMin = Vector2.zero; eRT.offsetMax = Vector2.zero;
                return;
            }

            CreateMilitarySectionLabel(content, $"Généraux ({playerGenerals.Count})");

            foreach (var gen in playerGenerals)
                CreateGeneralCard(content, gen);

            // Unassigned armies section
            CreateMilitarySectionLabel(content, "Armées sans général");
            bool anyUnassigned = false;
            foreach (var army in GetArmies().Values)
            {
                if (army.faction != playerFaction.factionType) continue;
                if (!string.IsNullOrEmpty(army.generalId)) continue;
                anyUnassigned = true;

                RectTransform rowRT = UIFactory.CreatePanel(content, $"Unassigned_{army.armyId}",
                    new Color(0.12f, 0.06f, 0.06f, 0.90f));
                UIFactory.AddLayoutElement(rowRT.gameObject, preferredHeight: 30);
                Text rowText = UIFactory.CreateText(rowRT, "Text", $"  {army.armyName} — pas de général assigné",
                    12, TextAnchor.MiddleLeft, new Color(1f, 0.6f, 0.4f));
                RectTransform rtT = rowText.GetComponent<RectTransform>();
                rtT.anchorMin = Vector2.zero; rtT.anchorMax = Vector2.one;
                rtT.offsetMin = new Vector2(10, 0); rtT.offsetMax = new Vector2(-10, 0);
            }
            if (!anyUnassigned)
            {
                Text allAssigned = UIFactory.CreateText(content, "AllAssigned",
                    "  Toutes les armées ont un général.", 12, TextAnchor.MiddleLeft,
                    new Color(0.5f, 0.9f, 0.5f));
                UIFactory.AddLayoutElement(allAssigned.gameObject, preferredHeight: 25);
            }
        }

        private void CreateGeneralCard(Transform parent, GeneralData gen)
        {
            bool isAssigned = !string.IsNullOrEmpty(gen.assignedArmyId);
            string armyName = "";
            if (isAssigned && campaignManager.Armies.ContainsKey(gen.assignedArmyId))
                armyName = campaignManager.Armies[gen.assignedArmyId].armyName;

            Color cardBg = isAssigned
                ? new Color(0.08f, 0.12f, 0.08f, 0.95f)
                : new Color(0.12f, 0.13f, 0.11f, 0.95f);

            RectTransform cardRT = UIFactory.CreatePanel(parent, $"Gen_{gen.generalId}", cardBg);
            UIFactory.AddLayoutElement(cardRT.gameObject, preferredHeight: 110);

            // Portrait placeholder (left)
            RectTransform portraitRT = UIFactory.CreatePanel(cardRT, "Portrait",
                new Color(0.15f, 0.12f, 0.10f, 0.9f));
            portraitRT.anchorMin = new Vector2(0.01f, 0.1f);
            portraitRT.anchorMax = new Vector2(0.08f, 0.9f);
            portraitRT.offsetMin = Vector2.zero; portraitRT.offsetMax = Vector2.zero;
            Text portraitIcon = UIFactory.CreateText(portraitRT, "Icon", "👤", 22,
                TextAnchor.MiddleCenter, UIFactory.ParchmentBeige);
            RectTransform piRT = portraitIcon.GetComponent<RectTransform>();
            piRT.anchorMin = Vector2.zero; piRT.anchorMax = Vector2.one;
            piRT.offsetMin = Vector2.zero; piRT.offsetMax = Vector2.zero;

            // Name + level
            Text nameText = UIFactory.CreateText(cardRT, "Name",
                $"{gen.FullName}  (Niv. {gen.level})", 13, TextAnchor.UpperLeft, UIFactory.GoldAccent);
            nameText.fontStyle = FontStyle.Bold;
            RectTransform nRT = nameText.GetComponent<RectTransform>();
            nRT.anchorMin = new Vector2(0.09f, 0.75f); nRT.anchorMax = new Vector2(0.65f, 0.95f);
            nRT.offsetMin = new Vector2(5, 0); nRT.offsetMax = Vector2.zero;

            // Stats
            Color statColor = UIFactory.ParchmentBeige;
            string statsStr = $"Commandement: {gen.command}  |  Autorité: {gen.authority}  |  Charisme: {gen.charisma}  |  Intelligence: {gen.intelligence}";
            Text statsText = UIFactory.CreateText(cardRT, "Stats", statsStr, 11,
                TextAnchor.UpperLeft, statColor);
            RectTransform sRT = statsText.GetComponent<RectTransform>();
            sRT.anchorMin = new Vector2(0.09f, 0.52f); sRT.anchorMax = new Vector2(0.65f, 0.75f);
            sRT.offsetMin = new Vector2(5, 0); sRT.offsetMax = Vector2.zero;

            // Bonuses
            string bonusStr = $"Atk +{gen.GetAttackBonus() * 100:F0}%  |  Def +{gen.GetDefenseBonus() * 100:F0}%  |  Moral +{gen.GetMoraleBonus():F0}";
            Text bonusText = UIFactory.CreateText(cardRT, "Bonus", bonusStr, 10,
                TextAnchor.UpperLeft, new Color(0.5f, 0.8f, 1f));
            RectTransform bRT = bonusText.GetComponent<RectTransform>();
            bRT.anchorMin = new Vector2(0.09f, 0.32f); bRT.anchorMax = new Vector2(0.65f, 0.52f);
            bRT.offsetMin = new Vector2(5, 0); bRT.offsetMax = Vector2.zero;

            // Traits
            if (gen.traits.Count > 0)
            {
                List<string> traitNames = new List<string>();
                foreach (var t in gen.traits) traitNames.Add(t.name);
                Text traitText = UIFactory.CreateText(cardRT, "Traits",
                    "Traits: " + string.Join(", ", traitNames), 10,
                    TextAnchor.UpperLeft, new Color(0.9f, 0.8f, 0.5f));
                RectTransform tRT = traitText.GetComponent<RectTransform>();
                tRT.anchorMin = new Vector2(0.09f, 0.12f); tRT.anchorMax = new Vector2(0.65f, 0.32f);
                tRT.offsetMin = new Vector2(5, 0); tRT.offsetMax = Vector2.zero;
            }

            // XP bar
            int expForNext = gen.level < 10 ? (gen.level + 1) * (gen.level + 1) * 100 : 999;
            float xpPct = expForNext > 0 ? Mathf.Clamp01((float)gen.experience / expForNext) : 1f;
            Text xpText = UIFactory.CreateText(cardRT, "XP",
                $"XP: {gen.experience}/{expForNext} ({(int)(xpPct * 100)}%)", 9,
                TextAnchor.UpperLeft, UIFactory.TextGrey);
            RectTransform xpRT = xpText.GetComponent<RectTransform>();
            xpRT.anchorMin = new Vector2(0.09f, 0.02f); xpRT.anchorMax = new Vector2(0.40f, 0.14f);
            xpRT.offsetMin = new Vector2(5, 0); xpRT.offsetMax = Vector2.zero;

            // Assignment section (right side)
            if (isAssigned)
            {
                Text assignedText = UIFactory.CreateText(cardRT, "Assigned",
                    $"Assigné à: {armyName}", 11, TextAnchor.MiddleCenter,
                    new Color(0.5f, 0.9f, 0.5f));
                assignedText.fontStyle = FontStyle.Bold;
                RectTransform aRT = assignedText.GetComponent<RectTransform>();
                aRT.anchorMin = new Vector2(0.66f, 0.55f); aRT.anchorMax = new Vector2(0.99f, 0.90f);
                aRT.offsetMin = Vector2.zero; aRT.offsetMax = Vector2.zero;

                string capturedGenId = gen.generalId;
                string capturedArmyId = gen.assignedArmyId;
                Button unassignBtn = UIFactory.CreateButton(cardRT, "Unassign", "Retirer", 11,
                    () => UnassignGeneral(capturedGenId, capturedArmyId));
                RectTransform ubRT = unassignBtn.GetComponent<RectTransform>();
                ubRT.anchorMin = new Vector2(0.72f, 0.15f); ubRT.anchorMax = new Vector2(0.95f, 0.48f);
                ubRT.offsetMin = Vector2.zero; ubRT.offsetMax = Vector2.zero;
                unassignBtn.GetComponent<Image>().color = new Color(0.4f, 0.2f, 0.2f);
            }
            else
            {
                Text notAssigned = UIFactory.CreateText(cardRT, "NotAssigned",
                    "Non assigné", 11, TextAnchor.MiddleCenter, UIFactory.TextGrey);
                notAssigned.fontStyle = FontStyle.Italic;
                RectTransform naRT = notAssigned.GetComponent<RectTransform>();
                naRT.anchorMin = new Vector2(0.66f, 0.65f); naRT.anchorMax = new Vector2(0.99f, 0.90f);
                naRT.offsetMin = Vector2.zero; naRT.offsetMax = Vector2.zero;

                // Show assign buttons for each unassigned army
                float btnY = 0.55f;
                foreach (var army in GetArmies().Values)
                {
                    if (army.faction != playerFaction.factionType) continue;
                    if (!string.IsNullOrEmpty(army.generalId)) continue;
                    if (btnY < 0.05f) break;

                    string capturedGenId = gen.generalId;
                    string capturedArmyId = army.armyId;
                    string btnLabel = $"→ {army.armyName}";
                    Button assignBtn = UIFactory.CreateButton(cardRT, $"Assign_{army.armyId}",
                        btnLabel, 10, () => AssignGeneral(capturedGenId, capturedArmyId));
                    RectTransform abRT = assignBtn.GetComponent<RectTransform>();
                    abRT.anchorMin = new Vector2(0.66f, btnY - 0.18f);
                    abRT.anchorMax = new Vector2(0.99f, btnY);
                    abRT.offsetMin = Vector2.zero; abRT.offsetMax = Vector2.zero;
                    btnY -= 0.20f;
                }
            }
        }

        private void AssignGeneral(string generalId, string armyId)
        {
            if (campaignManager.Generals.ContainsKey(generalId) && campaignManager.Armies.ContainsKey(armyId))
            {
                campaignManager.Generals[generalId].assignedArmyId = armyId;
                campaignManager.Armies[armyId].generalId = generalId;
            }
            RefreshMilitaryPanel();
        }

        private void UnassignGeneral(string generalId, string armyId)
        {
            if (campaignManager.Generals.ContainsKey(generalId))
                campaignManager.Generals[generalId].assignedArmyId = null;
            if (campaignManager.Armies.ContainsKey(armyId))
                campaignManager.Armies[armyId].generalId = null;
            RefreshMilitaryPanel();
        }

        private void RefreshMilitaryPanel()
        {
            navBar.TogglePanel(NavigationBar.NavPanel.Military);
            navBar.TogglePanel(NavigationBar.NavPanel.Military);
        }

        // === HELPERS ===
        private Dictionary<string, ArmyData> GetArmies()
        {
            return campaignManager.Armies ?? new Dictionary<string, ArmyData>();
        }

        private void CreateMilitarySectionLabel(Transform parent, string title)
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
