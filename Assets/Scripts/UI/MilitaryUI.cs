using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Campaign;
using NapoleonicWars.Data;
using NapoleonicWars.Units;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// Premium Military panel — HoI4/Victoria 3 inspired with 4 sub-tabs.
    /// </summary>
    public class MilitaryUI : MonoBehaviour
    {
        private CampaignManager campaignManager;
        private FactionData playerFaction;
        private NavigationBar navBar;
        private Transform contentParent;
        private int activeTab = 0;

        private GameObject armiesContent;
        private GameObject designerContent;
        private GameObject recruitmentContent;
        private GameObject generalsContent;
        private List<Button> tabButtons = new List<Button>();

        // Tab styling colors
        private static readonly Color TabActiveBg = new Color(0.22f, 0.18f, 0.10f, 0.95f);
        private static readonly Color TabInactiveBg = new Color(0.10f, 0.08f, 0.06f, 0.85f);
        private static readonly Color TabActiveText = new Color(1f, 0.88f, 0.55f);
        private static readonly Color TabInactiveText = new Color(0.55f, 0.48f, 0.38f);
        private static readonly Color CardBg = new Color(0.08f, 0.07f, 0.05f, 0.92f);
        private static readonly Color CardBgAlt = new Color(0.10f, 0.09f, 0.06f, 0.92f);
        private static readonly Color SectionBg = new Color(0.14f, 0.12f, 0.08f, 0.95f);
        private static readonly Color SeparatorColor = new Color(0.45f, 0.38f, 0.22f, 0.4f);
        private static readonly Color StatValueColor = new Color(0.95f, 0.85f, 0.50f);
        private static readonly Color StatLabelColor = new Color(0.65f, 0.58f, 0.45f);

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

        // ═══════════════════════════════════════════════════════════════════
        //  BUILD CONTENT
        // ═══════════════════════════════════════════════════════════════════

        private void BuildContent(Transform parent)
        {
            contentParent = parent;

            // ── HEADER BAR with gradient ──
            RectTransform headerBar = UIFactory.CreateGradientPanel(parent, "MilHeader",
                new Color(0.16f, 0.13f, 0.08f, 0.98f),
                new Color(0.10f, 0.08f, 0.05f, 0.98f),
                new Color(0.20f, 0.16f, 0.09f, 0.3f));
            UIFactory.AddLayoutElement(headerBar.gameObject, preferredHeight: 44);

            HorizontalLayoutGroup headerHLG = UIFactory.AddHorizontalLayout(headerBar.gameObject, 0f, new RectOffset(0, 0, 0, 0));
            headerHLG.childControlWidth = false;
            headerHLG.childControlHeight = true;

            // Tab buttons with ornate styling
            string[] tabLabels = { "⚔  ARMÉES", "🔧  CONCEPTEUR", "📋  RECRUTEMENT", "🎖  GÉNÉRAUX" };
            int[] tabWidths = { 160, 175, 185, 170 };
            for (int i = 0; i < tabLabels.Length; i++)
            {
                int idx = i;
                Button btn = CreateStyledTab(headerBar, $"Tab{i}", tabLabels[i], tabWidths[i], () => SwitchTab(idx));
                tabButtons.Add(btn);
            }

            // Manpower badge (right side)
            RectTransform mpContainer = UIFactory.CreateInsetContainer(headerBar, "ManpowerBadge",
                new Color(0.08f, 0.12f, 0.06f, 0.9f), new Color(0.35f, 0.50f, 0.25f, 0.5f));
            UIFactory.AddLayoutElement(mpContainer.gameObject, preferredWidth: 220, flexibleWidth: 1, preferredHeight: 36);
            Text mpText = UIFactory.CreateText(mpContainer, "MP",
                $"♟ {playerFaction.manpower:N0} / {playerFaction.maxManpower:N0}",
                12, TextAnchor.MiddleCenter, new Color(0.7f, 0.95f, 0.6f));
            mpText.fontStyle = FontStyle.Bold;
            RectTransform mpRT = mpText.GetComponent<RectTransform>();
            mpRT.anchorMin = Vector2.zero; mpRT.anchorMax = Vector2.one;
            mpRT.offsetMin = new Vector2(8, 0); mpRT.offsetMax = new Vector2(-8, 0);

            // Gold separator under header
            RectTransform sep = UIFactory.CreateGlowSeparator(parent, "HeaderSep", false,
                new Color(0.55f, 0.45f, 0.22f, 0.4f), new Color(0.75f, 0.62f, 0.30f, 0.6f));
            UIFactory.AddLayoutElement(sep.gameObject, preferredHeight: 3);

            // ── CONTENT TABS ──
            BuildArmiesTab(parent);
            BuildDesignerTab(parent);
            BuildRecruitmentTab(parent);
            BuildGeneralsTab(parent);
            SwitchTab(0);
        }

        private Button CreateStyledTab(Transform parent, string name, string label, int width, UnityEngine.Events.UnityAction action)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Image bg = go.AddComponent<Image>();
            bg.color = TabInactiveBg;

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(action);

            // Gold bottom accent line
            GameObject accent = new GameObject("Accent");
            accent.transform.SetParent(go.transform, false);
            RectTransform accentRT = accent.AddComponent<RectTransform>();
            accentRT.anchorMin = new Vector2(0.1f, 0); accentRT.anchorMax = new Vector2(0.9f, 0);
            accentRT.sizeDelta = new Vector2(0, 2);
            accent.AddComponent<Image>().color = new Color(0.65f, 0.52f, 0.25f, 0.0f);
            accent.AddComponent<LayoutElement>().ignoreLayout = true;

            Text txt = UIFactory.CreateText(go.transform, "Label", label, 12, TextAnchor.MiddleCenter, TabInactiveText);
            txt.fontStyle = FontStyle.Bold;
            RectTransform txtRT = txt.GetComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = Vector2.zero; txtRT.offsetMax = Vector2.zero;

            UIFactory.AddLayoutElement(go, preferredWidth: width, preferredHeight: 42);
            return btn;
        }

        private void SwitchTab(int tab)
        {
            activeTab = tab;
            if (armiesContent != null) armiesContent.SetActive(tab == 0);
            if (designerContent != null) designerContent.SetActive(tab == 1);
            if (recruitmentContent != null) recruitmentContent.SetActive(tab == 2);
            if (generalsContent != null) generalsContent.SetActive(tab == 3);

            // Update tab visuals
            for (int i = 0; i < tabButtons.Count; i++)
            {
                bool active = (i == tab);
                var img = tabButtons[i].GetComponent<Image>();
                img.color = active ? TabActiveBg : TabInactiveBg;
                var label = tabButtons[i].GetComponentInChildren<Text>();
                if (label != null) label.color = active ? TabActiveText : TabInactiveText;
                // Show/hide accent line
                var accent = tabButtons[i].transform.Find("Accent");
                if (accent != null)
                    accent.GetComponent<Image>().color = active
                        ? new Color(0.85f, 0.68f, 0.30f, 0.9f)
                        : new Color(0.65f, 0.52f, 0.25f, 0.0f);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  TAB 0: ARMIES
        // ═══════════════════════════════════════════════════════════════════

        private void BuildArmiesTab(Transform parent)
        {
            var (scroll, content) = UIFactory.CreateScrollView(parent, "ArmiesScroll");
            UIFactory.AddLayoutElement(scroll.gameObject, flexibleHeight: 1, preferredHeight: 800);
            armiesContent = scroll.gameObject;

            var armies = GetArmies();
            bool hasArmies = false;
            foreach (var kvp in armies)
            {
                ArmyData army = kvp.Value;
                if (army.faction != playerFaction.factionType) continue;
                hasArmies = true;
                CreatePremiumArmyCard(content, army);
            }

            if (!hasArmies)
            {
                CreateEmptyState(content, "Aucune armée disponible", "Recrutez des divisions pour former votre première armée !");
            }
        }

        private void CreatePremiumArmyCard(Transform parent, ArmyData army)
        {
            // Ornate card with gold border
            RectTransform cardRT = UIFactory.CreateOrnatePanel(parent, $"Army_{army.armyId}", CardBg);
            UIFactory.AddLayoutElement(cardRT.gameObject, preferredHeight: 130);
            Transform inner = cardRT.transform.Find("Inner");

            // ── LEFT: Army info ──
            string generalStr = !string.IsNullOrEmpty(army.generalId) ? $"  •  Gén. {army.generalId}" : "  •  Sans général";
            Text nameText = UIFactory.CreateText(inner, "Name", $"⚔  {army.armyName}", 16, TextAnchor.UpperLeft, UIFactory.GoldAccent);
            nameText.fontStyle = FontStyle.Bold;
            SetRect(nameText, 0f, 0.75f, 0.50f, 0.98f, 12, 0, 0, -4);

            Text genText = UIFactory.CreateText(inner, "General", generalStr, 11, TextAnchor.UpperLeft,
                string.IsNullOrEmpty(army.generalId) ? new Color(0.7f, 0.4f, 0.3f) : StatLabelColor);
            genText.fontStyle = FontStyle.Italic;
            SetRect(genText, 0f, 0.62f, 0.50f, 0.77f, 12);

            // Stats row
            int soldiers = army.TotalSoldiers;
            string soldiersStr = soldiers >= 1000 ? $"{soldiers / 1000f:F1}K" : $"{soldiers}";
            string statsLine = $"<color=#E8D090>{soldiersStr}</color> hommes   <color=#E8D090>{army.regiments.Count}</color> rég.   Entretien: <color=#E8D090>{army.MaintenanceCost:F0}</color>g/t";
            Text statsText = UIFactory.CreateText(inner, "Stats", statsLine, 11, TextAnchor.MiddleLeft, StatLabelColor);
            statsText.supportRichText = true;
            SetRect(statsText, 0f, 0.42f, 0.55f, 0.60f, 12);

            // Location
            string locName = campaignManager != null ? campaignManager.GetProvinceName(army.currentProvinceId) : army.currentProvinceId;
            Text locText = UIFactory.CreateText(inner, "Loc", $"📍 {locName}   Mvt: {army.movementPoints}/{army.maxMovementPoints}", 10, TextAnchor.MiddleLeft, StatLabelColor);
            SetRect(locText, 0f, 0.26f, 0.50f, 0.42f, 12);

            // Composition badges
            int inf = 0, cav = 0, art = 0, elite = 0, maxRank = 0;
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
            string compStr = $"<color=#8AB4F8>⚔{inf}</color>  <color=#81C784>🐎{cav}</color>  <color=#FFB74D>💥{art}</color>";
            if (elite > 0) compStr += $"  <color=#CE93D8>⭐{elite}</color>";
            if (maxRank > 0) compStr += $"  <color=#B0B0B0>| {RegimentRankSystem.GetRankName(maxRank)}</color>";
            Text compText = UIFactory.CreateText(inner, "Comp", compStr, 11, TextAnchor.MiddleLeft, Color.white);
            compText.supportRichText = true;
            SetRect(compText, 0f, 0.06f, 0.55f, 0.24f, 12);

            // ── RIGHT: Organization bar + Details button ──
            // Org label
            Text orgLabel = UIFactory.CreateText(inner, "OrgLabel", "ORGANISATION", 9, TextAnchor.MiddleCenter, StatLabelColor);
            orgLabel.fontStyle = FontStyle.Bold;
            SetRect(orgLabel, 0.56f, 0.78f, 0.78f, 0.95f);

            // Premium org bar
            float orgPct = army.maxOrganization > 0 ? army.organization / army.maxOrganization : 0f;
            Color orgColor = orgPct > 0.6f ? new Color(0.35f, 0.70f, 0.35f) : orgPct > 0.3f ? new Color(0.80f, 0.65f, 0.20f) : new Color(0.75f, 0.25f, 0.20f);
            var (orgBg, orgFill, _) = UIFactory.CreatePremiumProgressBar(inner, "OrgBar", orgColor);
            SetRect(orgBg, 0.56f, 0.62f, 0.78f, 0.78f);
            orgFill.GetComponent<RectTransform>().anchorMax = new Vector2(orgPct, 1);

            Text orgPctText = UIFactory.CreateText(inner, "OrgPct", $"{(int)(orgPct * 100)}%", 10, TextAnchor.MiddleCenter, StatValueColor);
            SetRect(orgPctText, 0.56f, 0.62f, 0.78f, 0.78f);

            // Details button
            Button detailsBtn = UIFactory.CreateGoldButton(inner, "Details", "DÉTAILS  →", 12);
            SetRect(detailsBtn, 0.80f, 0.30f, 0.97f, 0.75f);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  TAB 1: DIVISION DESIGNER
        // ═══════════════════════════════════════════════════════════════════

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
                CreatePremiumTemplateCard(content, template);

            // Add template button
            CreateSectionFooterButton(content, "+ NOUVEAU TEMPLATE", null);
        }

        private void CreatePremiumTemplateCard(Transform parent, DivisionTemplate template)
        {
            RectTransform cardRT = UIFactory.CreateOrnatePanel(parent, $"Tmpl_{template.templateId}", CardBg);
            UIFactory.AddLayoutElement(cardRT.gameObject, preferredHeight: 170);
            Transform inner = cardRT.transform.Find("Inner");

            // Template name
            Text nameText = UIFactory.CreateText(inner, "Name", $"🔧  {template.templateName}", 15, TextAnchor.UpperLeft, UIFactory.GoldAccent);
            nameText.fontStyle = FontStyle.Bold;
            SetRect(nameText, 0f, 0.85f, 0.55f, 1f, 10, 0, 0, -4);

            // Battalion grid
            float gridX = 10, gridY = -30;
            int col = 0, row = 0;
            float cellSize = 40f;
            foreach (var slot in template.battalions)
            {
                for (int i = 0; i < slot.count; i++)
                {
                    RectTransform cellRT = UIFactory.CreateBorderedPanel(inner, $"Cell_{col}_{row}",
                        GetBattalionColor(slot.type), new Color(0.50f, 0.42f, 0.25f, 0.6f), 1f);
                    cellRT.anchorMin = new Vector2(0, 1); cellRT.anchorMax = new Vector2(0, 1);
                    cellRT.pivot = new Vector2(0, 1);
                    cellRT.anchoredPosition = new Vector2(gridX + col * (cellSize + 3), gridY - row * (cellSize + 3));
                    cellRT.sizeDelta = new Vector2(cellSize, cellSize);

                    Text cellText = UIFactory.CreateText(cellRT.transform.GetChild(0), "T",
                        DivisionTemplate.GetBattalionIcon(slot.type), 10, TextAnchor.MiddleCenter, UIFactory.TextWhite);
                    cellText.fontStyle = FontStyle.Bold;
                    RectTransform ctRT = cellText.GetComponent<RectTransform>();
                    ctRT.anchorMin = Vector2.zero; ctRT.anchorMax = Vector2.one;
                    ctRT.offsetMin = Vector2.zero; ctRT.offsetMax = Vector2.zero;

                    col++;
                    if (col >= 4) { col = 0; row++; }
                }
            }

            // Stats (right side) with premium formatting
            float sx = 0.52f;
            CreatePremiumStat(inner, "ATK", $"{template.attack:F0}", sx, 0.72f, new Color(0.9f, 0.5f, 0.4f));
            CreatePremiumStat(inner, "DEF", $"{template.defense:F0}", sx, 0.55f, new Color(0.5f, 0.7f, 0.9f));
            CreatePremiumStat(inner, "BRK", $"{template.breakthrough:F0}", sx, 0.38f, new Color(0.9f, 0.75f, 0.4f));
            CreatePremiumStat(inner, "WDT", $"{template.combatWidth}", sx, 0.21f, StatLabelColor);
            CreatePremiumStat(inner, "ORG", $"{template.organization:F0}", sx + 0.20f, 0.72f, new Color(0.5f, 0.85f, 0.5f));
            CreatePremiumStat(inner, "SPD", $"{template.speed}km", sx + 0.20f, 0.55f, new Color(0.7f, 0.85f, 1f));
            CreatePremiumStat(inner, "MP", $"{template.manpowerCost:N0}", sx + 0.20f, 0.38f, new Color(0.75f, 0.6f, 0.5f));

            // Action buttons
            Button dupBtn = UIFactory.CreateButton(inner, "Dup", "DUPLIQUER", 10);
            SetRect(dupBtn, 0.52f, 0.03f, 0.72f, 0.17f);
            Button delBtn = UIFactory.CreateButton(inner, "Del", "SUPPRIMER", 10);
            SetRect(delBtn, 0.74f, 0.03f, 0.97f, 0.17f);
            delBtn.GetComponent<Image>().color = new Color(0.35f, 0.12f, 0.10f, 0.9f);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  TAB 2: RECRUITMENT
        // ═══════════════════════════════════════════════════════════════════

        private void BuildRecruitmentTab(Transform parent)
        {
            var (scroll, content) = UIFactory.CreateScrollView(parent, "RecruitScroll");
            UIFactory.AddLayoutElement(scroll.gameObject, flexibleHeight: 1, preferredHeight: 800);
            recruitmentContent = scroll.gameObject;

            // Understrength section
            CreatePremiumSectionHeader(content, "⚠  RÉGIMENTS SOUS-EFFECTIFS");

            var armies = GetArmies();
            int underCount = 0;

            foreach (var kvp in armies)
            {
                ArmyData army = kvp.Value;
                if (army.faction != playerFaction.factionType) continue;
                foreach (var reg in army.regiments)
                {
                    int maxSize = RegimentData.GetDefaultSize(reg.unitType);
                    if (reg.currentSize < maxSize)
                    {
                        underCount++;
                        float pct = maxSize > 0 ? (float)reg.currentSize / maxSize : 0f;
                        CreateUnderstrengthRow(content, reg, army, pct, maxSize);
                    }
                }
            }

            if (underCount == 0)
            {
                Text allGood = UIFactory.CreateText(content, "AllGood", "  ✓ Tous les régiments sont à plein effectif.", 12, TextAnchor.MiddleLeft, new Color(0.4f, 0.85f, 0.4f));
                UIFactory.AddLayoutElement(allGood.gameObject, preferredHeight: 35);
            }

            // Separator
            RectTransform recSep = UIFactory.CreateGlowSeparator(content, "RecSep", false, SeparatorColor, UIFactory.MutedGold);
            UIFactory.AddLayoutElement(recSep.gameObject, preferredHeight: 5);

            // Templates for recruitment
            CreatePremiumSectionHeader(content, "📋  TEMPLATES DISPONIBLES");

            var templates = DivisionDesigner.GetTemplates(playerFaction.factionType);
            foreach (var t in templates)
                CreateRecruitTemplateRow(content, t);

            CreateSectionFooterButton(content, "+ RECRUTER NOUVELLE DIVISION", null);
        }

        private void CreateUnderstrengthRow(Transform parent, RegimentData reg, ArmyData army, float pct, int maxSize)
        {
            Color rowBg = pct < 0.5f ? new Color(0.18f, 0.07f, 0.05f, 0.90f) : CardBg;
            RectTransform row = UIFactory.CreatePanel(parent, $"Under_{reg.regimentName}", rowBg);
            UIFactory.AddLayoutElement(row.gameObject, preferredHeight: 36);

            HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(row.gameObject, 8f, new RectOffset(12, 12, 4, 4));
            hlg.childControlWidth = false; hlg.childControlHeight = true;

            Text regName = UIFactory.CreateText(row, "Name", reg.regimentName, 11, TextAnchor.MiddleLeft, UIFactory.TextWhite);
            UIFactory.AddLayoutElement(regName.gameObject, preferredWidth: 220, preferredHeight: 28);

            Color pctColor = pct < 0.5f ? new Color(1f, 0.35f, 0.30f) : new Color(1f, 0.80f, 0.30f);
            Text sizeText = UIFactory.CreateText(row, "Size", $"{reg.currentSize}/{maxSize} ({(int)(pct * 100)}%)", 11, TextAnchor.MiddleLeft, pctColor);
            UIFactory.AddLayoutElement(sizeText.gameObject, preferredWidth: 130, preferredHeight: 28);

            Text armyText = UIFactory.CreateText(row, "Army", army.armyName, 10, TextAnchor.MiddleLeft, StatLabelColor);
            UIFactory.AddLayoutElement(armyText.gameObject, preferredWidth: 180, preferredHeight: 28);

            Button reinforceBtn = UIFactory.CreateGoldButton(row, "Reinforce", "RENFORCER", 10);
            UIFactory.AddLayoutElement(reinforceBtn.gameObject, preferredWidth: 95, preferredHeight: 26);
        }

        private void CreateRecruitTemplateRow(Transform parent, DivisionTemplate t)
        {
            RectTransform row = UIFactory.CreatePanel(parent, $"TRow_{t.templateId}", CardBg);
            UIFactory.AddLayoutElement(row.gameObject, preferredHeight: 40);

            HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(row.gameObject, 10f, new RectOffset(14, 14, 5, 5));
            hlg.childControlWidth = false; hlg.childControlHeight = true;

            Text tmplName = UIFactory.CreateText(row, "Name", t.templateName, 12, TextAnchor.MiddleLeft, UIFactory.TextWhite);
            tmplName.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(tmplName.gameObject, preferredWidth: 260, preferredHeight: 30);

            Text tmplStats = UIFactory.CreateText(row, "Stats", $"{t.manpowerCost:N0} MP  |  {t.TotalBattalions} bat.  |  {t.combatWidth} largeur", 11, TextAnchor.MiddleLeft, StatLabelColor);
            UIFactory.AddLayoutElement(tmplStats.gameObject, preferredWidth: 250, preferredHeight: 30);

            Button recruitBtn = UIFactory.CreateGoldButton(row, "Recruit", "RECRUTER", 11);
            UIFactory.AddLayoutElement(recruitBtn.gameObject, preferredWidth: 110, preferredHeight: 28);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  TAB 3: GENERALS
        // ═══════════════════════════════════════════════════════════════════

        private void BuildGeneralsTab(Transform parent)
        {
            var (scroll, content) = UIFactory.CreateScrollView(parent, "GeneralsScroll");
            UIFactory.AddLayoutElement(scroll.gameObject, flexibleHeight: 1, preferredHeight: 800);
            generalsContent = scroll.gameObject;

            var allGenerals = campaignManager.Generals;
            if (allGenerals == null || allGenerals.Count == 0)
            {
                CreateEmptyState(content, "Aucun général", "Aucun général disponible pour votre faction.");
                return;
            }

            List<GeneralData> playerGenerals = new List<GeneralData>();
            foreach (var gen in allGenerals.Values)
                if (gen.faction == playerFaction.factionType && gen.isAlive) playerGenerals.Add(gen);

            if (playerGenerals.Count == 0)
            {
                CreateEmptyState(content, "Aucun général vivant", "Tous vos généraux sont tombés au combat.");
                return;
            }

            CreatePremiumSectionHeader(content, $"🎖  ÉTAT-MAJOR  ({playerGenerals.Count} généraux)");

            foreach (var gen in playerGenerals)
                CreatePremiumGeneralCard(content, gen);

            // Separator
            RectTransform genSep = UIFactory.CreateGlowSeparator(content, "GenSep", false, SeparatorColor, UIFactory.MutedGold);
            UIFactory.AddLayoutElement(genSep.gameObject, preferredHeight: 5);

            // Unassigned armies
            CreatePremiumSectionHeader(content, "⚠  ARMÉES SANS GÉNÉRAL");
            bool any = false;
            foreach (var army in GetArmies().Values)
            {
                if (army.faction != playerFaction.factionType || !string.IsNullOrEmpty(army.generalId)) continue;
                any = true;
                RectTransform rowRT = UIFactory.CreatePanel(content, $"UA_{army.armyId}", new Color(0.15f, 0.07f, 0.05f, 0.90f));
                UIFactory.AddLayoutElement(rowRT.gameObject, preferredHeight: 30);
                Text rowText = UIFactory.CreateText(rowRT, "T", $"  ⚠ {army.armyName} — aucun commandant assigné", 12, TextAnchor.MiddleLeft, new Color(1f, 0.6f, 0.4f));
                RectTransform rtT = rowText.GetComponent<RectTransform>();
                rtT.anchorMin = Vector2.zero; rtT.anchorMax = Vector2.one;
                rtT.offsetMin = new Vector2(10, 0); rtT.offsetMax = new Vector2(-10, 0);
            }
            if (!any)
            {
                Text ok = UIFactory.CreateText(content, "OK", "  ✓ Toutes les armées ont un général.", 12, TextAnchor.MiddleLeft, new Color(0.4f, 0.85f, 0.4f));
                UIFactory.AddLayoutElement(ok.gameObject, preferredHeight: 28);
            }
        }

        private void CreatePremiumGeneralCard(Transform parent, GeneralData gen)
        {
            bool isAssigned = !string.IsNullOrEmpty(gen.assignedArmyId);
            string armyName = "";
            if (isAssigned && campaignManager.Armies.ContainsKey(gen.assignedArmyId))
                armyName = campaignManager.Armies[gen.assignedArmyId].armyName;

            Color bg = isAssigned ? new Color(0.07f, 0.11f, 0.07f, 0.94f) : CardBg;
            RectTransform cardRT = UIFactory.CreateOrnatePanel(parent, $"Gen_{gen.generalId}", bg);
            UIFactory.AddLayoutElement(cardRT.gameObject, preferredHeight: 120);
            Transform inner = cardRT.transform.Find("Inner");

            // Portrait (left)
            RectTransform portraitRT = UIFactory.CreateBorderedPanel(inner, "Portrait",
                new Color(0.12f, 0.10f, 0.08f, 0.95f), new Color(0.50f, 0.42f, 0.25f, 0.6f), 1.5f);
            portraitRT.anchorMin = new Vector2(0.01f, 0.08f); portraitRT.anchorMax = new Vector2(0.08f, 0.92f);
            portraitRT.offsetMin = Vector2.zero; portraitRT.offsetMax = Vector2.zero;
            Text pIcon = UIFactory.CreateText(portraitRT.transform.GetChild(0), "I", "👤", 24, TextAnchor.MiddleCenter, UIFactory.ParchmentBeige);
            RectTransform piRT = pIcon.GetComponent<RectTransform>();
            piRT.anchorMin = Vector2.zero; piRT.anchorMax = Vector2.one;
            piRT.offsetMin = Vector2.zero; piRT.offsetMax = Vector2.zero;

            // Name + Level
            Text nameText = UIFactory.CreateText(inner, "Name", $"{gen.FullName}  •  Niv. {gen.level}", 14, TextAnchor.UpperLeft, UIFactory.GoldAccent);
            nameText.fontStyle = FontStyle.Bold;
            SetRect(nameText, 0.09f, 0.78f, 0.62f, 0.96f, 5);

            // Stats with colored values
            string statsStr = $"CMD: <color=#E8D090>{gen.command}</color>  AUT: <color=#E8D090>{gen.authority}</color>  CHA: <color=#E8D090>{gen.charisma}</color>  INT: <color=#E8D090>{gen.intelligence}</color>";
            Text statsText = UIFactory.CreateText(inner, "Stats", statsStr, 11, TextAnchor.UpperLeft, StatLabelColor);
            statsText.supportRichText = true;
            SetRect(statsText, 0.09f, 0.56f, 0.62f, 0.78f, 5);

            // Bonuses
            string bonusStr = $"<color=#81C784>Atk +{gen.GetAttackBonus() * 100:F0}%</color>  <color=#64B5F6>Def +{gen.GetDefenseBonus() * 100:F0}%</color>  <color=#FFB74D>Moral +{gen.GetMoraleBonus():F0}</color>";
            Text bonusText = UIFactory.CreateText(inner, "Bonus", bonusStr, 10, TextAnchor.UpperLeft, Color.white);
            bonusText.supportRichText = true;
            SetRect(bonusText, 0.09f, 0.36f, 0.62f, 0.56f, 5);

            // Traits
            if (gen.traits.Count > 0)
            {
                List<string> traitNames = new List<string>();
                foreach (var t in gen.traits) traitNames.Add(t.name);
                Text traitText = UIFactory.CreateText(inner, "Traits", "Traits: " + string.Join(", ", traitNames), 10, TextAnchor.UpperLeft, new Color(0.90f, 0.80f, 0.50f));
                SetRect(traitText, 0.09f, 0.14f, 0.62f, 0.36f, 5);
            }

            // XP bar
            int expNext = gen.level < 10 ? (gen.level + 1) * (gen.level + 1) * 100 : 999;
            float xpPct = expNext > 0 ? Mathf.Clamp01((float)gen.experience / expNext) : 1f;
            Text xpText = UIFactory.CreateText(inner, "XP", $"XP: {gen.experience}/{expNext}", 9, TextAnchor.UpperLeft, StatLabelColor);
            SetRect(xpText, 0.09f, 0.02f, 0.35f, 0.16f, 5);

            // Right side: assignment
            if (isAssigned)
            {
                Text assignedText = UIFactory.CreateText(inner, "Assigned", $"Assigné à:\n{armyName}", 11, TextAnchor.MiddleCenter, new Color(0.5f, 0.9f, 0.5f));
                assignedText.fontStyle = FontStyle.Bold;
                SetRect(assignedText, 0.65f, 0.55f, 0.98f, 0.92f);

                string cGenId = gen.generalId, cArmyId = gen.assignedArmyId;
                Button unBtn = UIFactory.CreateButton(inner, "Unassign", "RETIRER", 11, () => UnassignGeneral(cGenId, cArmyId));
                SetRect(unBtn, 0.72f, 0.12f, 0.95f, 0.45f);
                unBtn.GetComponent<Image>().color = new Color(0.40f, 0.15f, 0.12f, 0.9f);
            }
            else
            {
                Text notAssigned = UIFactory.CreateText(inner, "NA", "Non assigné", 11, TextAnchor.MiddleCenter, StatLabelColor);
                notAssigned.fontStyle = FontStyle.Italic;
                SetRect(notAssigned, 0.65f, 0.72f, 0.98f, 0.92f);

                float btnY = 0.60f;
                foreach (var army in GetArmies().Values)
                {
                    if (army.faction != playerFaction.factionType || !string.IsNullOrEmpty(army.generalId)) continue;
                    if (btnY < 0.05f) break;
                    string cGenId = gen.generalId, cArmyId = army.armyId;
                    Button assignBtn = UIFactory.CreateGoldButton(inner, $"Assign_{army.armyId}", $"→ {army.armyName}", 10);
                    assignBtn.onClick.AddListener(() => AssignGeneral(cGenId, cArmyId));
                    SetRect(assignBtn, 0.65f, btnY - 0.20f, 0.98f, btnY);
                    btnY -= 0.22f;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════════════════════

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

        private Dictionary<string, ArmyData> GetArmies() => campaignManager.Armies ?? new Dictionary<string, ArmyData>();

        private Color GetBattalionColor(BattalionType type) => type switch
        {
            BattalionType.Infantry or BattalionType.LightInfantry => new Color(0.18f, 0.28f, 0.48f, 0.95f),
            BattalionType.Grenadier or BattalionType.Guard => new Color(0.48f, 0.22f, 0.12f, 0.95f),
            BattalionType.Artillery or BattalionType.HeavyArtillery => new Color(0.42f, 0.36f, 0.14f, 0.95f),
            BattalionType.Cavalry or BattalionType.Hussar or BattalionType.Lancer => new Color(0.18f, 0.38f, 0.22f, 0.95f),
            _ => new Color(0.22f, 0.22f, 0.22f, 0.95f)
        };

        private void CreatePremiumStat(Transform parent, string label, string value, float x, float y, Color valueColor)
        {
            Text txt = UIFactory.CreateText(parent, $"S_{label}",
                $"<color=#{ColorToHex(StatLabelColor)}>{label}</color>  <color=#{ColorToHex(valueColor)}>{value}</color>",
                11, TextAnchor.MiddleLeft, Color.white);
            txt.supportRichText = true;
            SetRect(txt, x, y - 0.08f, x + 0.18f, y + 0.08f);
        }

        private string ColorToHex(Color c) => $"{(int)(c.r*255):X2}{(int)(c.g*255):X2}{(int)(c.b*255):X2}";

        private void CreatePremiumSectionHeader(Transform parent, string title)
        {
            RectTransform hdrRT = UIFactory.CreateGradientPanel(parent, $"Hdr_{title.GetHashCode()}",
                SectionBg, new Color(0.10f, 0.08f, 0.05f, 0.95f));
            UIFactory.AddLayoutElement(hdrRT.gameObject, preferredHeight: 32);
            Text hdrText = UIFactory.CreateText(hdrRT, "T", title, 13, TextAnchor.MiddleLeft, UIFactory.GoldAccent);
            hdrText.fontStyle = FontStyle.Bold;
            RectTransform hRT = hdrText.GetComponent<RectTransform>();
            hRT.anchorMin = Vector2.zero; hRT.anchorMax = Vector2.one;
            hRT.offsetMin = new Vector2(14, 0); hRT.offsetMax = new Vector2(-14, 0);
        }

        private void CreateEmptyState(Transform parent, string title, string subtitle)
        {
            RectTransform emptyRT = UIFactory.CreateOrnatePanel(parent, "Empty", CardBg);
            UIFactory.AddLayoutElement(emptyRT.gameObject, preferredHeight: 80);
            Transform inner = emptyRT.transform.Find("Inner");
            Text titleText = UIFactory.CreateText(inner, "Title", title, 16, TextAnchor.MiddleCenter, StatLabelColor);
            titleText.fontStyle = FontStyle.Bold;
            SetRect(titleText, 0f, 0.5f, 1f, 0.9f);
            Text subText = UIFactory.CreateText(inner, "Sub", subtitle, 12, TextAnchor.MiddleCenter, new Color(0.45f, 0.40f, 0.32f));
            SetRect(subText, 0f, 0.1f, 1f, 0.5f);
        }

        private void CreateSectionFooterButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            RectTransform row = UIFactory.CreatePanel(parent, "FooterBtn", new Color(0.08f, 0.07f, 0.05f, 0.85f));
            UIFactory.AddLayoutElement(row.gameObject, preferredHeight: 48);
            Button btn = UIFactory.CreateGoldButton(row, "Btn", label, 13);
            if (action != null) btn.onClick.AddListener(action);
            SetRect(btn, 0.05f, 0.12f, 0.40f, 0.88f);
        }

        private void SetRect(Component comp, float aMinX, float aMinY, float aMaxX, float aMaxY, float offL = 0, float offB = 0, float offR = 0, float offT = 0)
        {
            RectTransform rt = comp.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(aMinX, aMinY);
            rt.anchorMax = new Vector2(aMaxX, aMaxY);
            rt.offsetMin = new Vector2(offL, offB);
            rt.offsetMax = new Vector2(offR, offT);
        }
    }
}
