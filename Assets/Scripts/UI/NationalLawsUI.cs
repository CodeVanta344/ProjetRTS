using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Campaign;
using NapoleonicWars.Data;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// HoI4-style National Laws panel. Three columns: Conscription, Economy, Trade.
    /// Each law shows PP cost, effects, and current selection.
    /// </summary>
    public class NationalLawsUI : MonoBehaviour
    {
        private CampaignManager campaignManager;
        private FactionData playerFaction;
        private NavigationBar navBar;

        public static NationalLawsUI Create(NavigationBar navBar, CampaignManager manager)
        {
            var overlay = navBar.CreateOverlayPanel("LOIS NATIONALES");
            if (overlay == null) return null;

            var ui = overlay.AddComponent<NationalLawsUI>();
            ui.navBar = navBar;
            ui.campaignManager = manager;
            ui.playerFaction = manager.GetPlayerFaction();
            navBar.RegisterPanel(NavigationBar.NavPanel.Laws, overlay);

            ui.BuildContent(overlay.transform);
            return ui;
        }

        private void BuildContent(Transform parent)
        {
            // PP summary bar
            RectTransform ppBar = UIFactory.CreatePanel(parent, "PPSummary", new Color(0.10f, 0.07f, 0.05f, 0.95f));
            UIFactory.AddLayoutElement(ppBar.gameObject, preferredHeight: 45);
            Text ppText = UIFactory.CreateText(ppBar, "PP",
                $"Pouvoir politique: {playerFaction.politicalPower:F0} (+{playerFaction.politicalPowerGain:F1}/tour)",
                16, TextAnchor.MiddleCenter, UIFactory.GoldAccent);
            ppText.fontStyle = FontStyle.Bold;
            RectTransform ppRT = ppText.GetComponent<RectTransform>();
            ppRT.anchorMin = Vector2.zero; ppRT.anchorMax = Vector2.one;
            ppRT.offsetMin = new Vector2(15, 0); ppRT.offsetMax = new Vector2(-15, 0);

            // Stability / War Support / Manpower bar
            RectTransform statBar = UIFactory.CreatePanel(parent, "StatBar", new Color(0.08f, 0.06f, 0.04f, 0.9f));
            UIFactory.AddLayoutElement(statBar.gameObject, preferredHeight: 30);
            HorizontalLayoutGroup statHlg = UIFactory.AddHorizontalLayout(statBar.gameObject, 20f, new RectOffset(30, 30, 2, 2));

            Color stabCol = playerFaction.stability > 0.7f ? new Color(0.3f, 0.9f, 0.3f) :
                           playerFaction.stability > 0.4f ? new Color(0.9f, 0.8f, 0.2f) : new Color(0.9f, 0.3f, 0.3f);
            Color warCol = playerFaction.warSupport > 0.7f ? new Color(0.3f, 0.9f, 0.3f) :
                          playerFaction.warSupport > 0.4f ? new Color(0.9f, 0.8f, 0.2f) : new Color(0.9f, 0.3f, 0.3f);

            Text stabText = UIFactory.CreateText(statBar, "Stab", $"Stabilité: {(int)(playerFaction.stability * 100)}%", 13, TextAnchor.MiddleCenter, stabCol);
            UIFactory.AddLayoutElement(stabText.gameObject, flexibleWidth: 1);
            Text warText = UIFactory.CreateText(statBar, "War", $"Soutien de guerre: {(int)(playerFaction.warSupport * 100)}%", 13, TextAnchor.MiddleCenter, warCol);
            UIFactory.AddLayoutElement(warText.gameObject, flexibleWidth: 1);
            Text mpText = UIFactory.CreateText(statBar, "MP", $"Manpower: {playerFaction.manpower:N0}", 13, TextAnchor.MiddleCenter, UIFactory.ParchmentBeige);
            UIFactory.AddLayoutElement(mpText.gameObject, flexibleWidth: 1);

            // Scroll area for columns
            var (scroll, content) = UIFactory.CreateScrollView(parent, "LawsScroll");
            UIFactory.AddLayoutElement(scroll.gameObject, flexibleHeight: 1, preferredHeight: 800);
            scroll.horizontal = true;
            scroll.vertical = true;

            // Disable VLG on content — we position columns manually
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) vlg.enabled = false;
            // Use absolute positioning: anchors at top-left so sizeDelta.x = absolute width
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(0, 1);
            content.pivot = new Vector2(0, 1);

            // Calculate column layout — fill full width
            int conscriptionCount = System.Enum.GetValues(typeof(ConscriptionLaw)).Length;
            int economyCount = System.Enum.GetValues(typeof(EconomyLaw)).Length;
            int tradeCount = System.Enum.GetValues(typeof(TradeLaw)).Length;
            int maxCards = Mathf.Max(conscriptionCount, Mathf.Max(economyCount, tradeCount));
            float cardHeight = 120f;
            float headerHeight = 38f;
            float totalHeight = headerHeight + maxCards * (cardHeight + 6f) + 20f;
            // Use full available width (1920 - 70 nav bar = 1850, minus padding)
            float availableWidth = 1850f;
            float gap = 10f;
            float colWidth = (availableWidth - gap * 4) / 3f;
            float totalWidth = availableWidth;

            // Build three columns
            BuildConscriptionColumn(content, gap, colWidth, headerHeight, cardHeight);
            BuildEconomyColumn(content, gap + colWidth + gap, colWidth, headerHeight, cardHeight);
            BuildTradeColumn(content, gap + (colWidth + gap) * 2, colWidth, headerHeight, cardHeight);

            // National Focus section below law columns
            float focusSectionY = totalHeight;
            float focusHeight = BuildNationalFocusSection(content, focusSectionY, totalWidth);
            content.sizeDelta = new Vector2(totalWidth, totalHeight + focusHeight);
        }

        // ================================ NATIONAL FOCUS TREE ================================
        private float BuildNationalFocusSection(RectTransform parent, float yOffset, float totalWidth)
        {
            float y = -yOffset;
            float focusCardH = 90f;
            float sectionStartY = y;

            // Section header
            RectTransform hdrRT = CreateManualPanel(parent, "FocusHeader",
                new Color(0.16f, 0.09f, 0.06f, 0.95f), 10f, y, totalWidth - 20f, 36f);
            Text hdrText = UIFactory.CreateText(hdrRT, "Text", "ARBRE DE FOCUS NATIONAL", 16,
                TextAnchor.MiddleCenter, UIFactory.GoldAccent);
            hdrText.fontStyle = FontStyle.Bold;
            RectTransform hdrTextRT = hdrText.GetComponent<RectTransform>();
            hdrTextRT.anchorMin = Vector2.zero; hdrTextRT.anchorMax = Vector2.one;
            hdrTextRT.offsetMin = Vector2.zero; hdrTextRT.offsetMax = Vector2.zero;
            y -= 42f;

            // Active focus info
            string activeFocusId = null;
            int focusProgress = 0;
            try
            {
                activeFocusId = NationalFocusTree.GetActiveFocus(playerFaction.factionType);
                focusProgress = NationalFocusTree.GetFocusProgress(playerFaction.factionType);
            }
            catch { }

            if (!string.IsNullOrEmpty(activeFocusId))
            {
                var focusList = NationalFocusTree.GetFocusTree(playerFaction.factionType);
                var active = focusList.Find(f => f.focusId == activeFocusId);
                if (active != null)
                {
                    RectTransform activeRT = CreateManualPanel(parent, "ActiveFocus",
                        new Color(0.10f, 0.18f, 0.10f, 0.95f), 10f, y, totalWidth - 20f, 40f);
                    float pct = active.turnsToComplete > 0 ? (float)focusProgress / active.turnsToComplete : 0f;
                    Text activeText = UIFactory.CreateText(activeRT, "Text",
                        $"Focus actif: {active.displayName}  —  {focusProgress}/{active.turnsToComplete} tours ({(int)(pct * 100)}%)",
                        13, TextAnchor.MiddleCenter, new Color(0.5f, 1f, 0.5f));
                    activeText.fontStyle = FontStyle.Bold;
                    RectTransform atRT = activeText.GetComponent<RectTransform>();
                    atRT.anchorMin = Vector2.zero; atRT.anchorMax = Vector2.one;
                    atRT.offsetMin = new Vector2(15, 0); atRT.offsetMax = new Vector2(-15, 0);

                    // Progress bar background
                    RectTransform barBg = CreateManualPanel(activeRT, "BarBg",
                        new Color(0.05f, 0.05f, 0.05f, 0.8f), 0f, 0f, 0f, 0f);
                    barBg.anchorMin = new Vector2(0.1f, 0.05f);
                    barBg.anchorMax = new Vector2(0.9f, 0.2f);
                    barBg.offsetMin = Vector2.zero; barBg.offsetMax = Vector2.zero;

                    RectTransform barFill = CreateManualPanel(barBg, "BarFill",
                        new Color(0.3f, 0.8f, 0.3f, 0.9f), 0f, 0f, 0f, 0f);
                    barFill.anchorMin = Vector2.zero;
                    barFill.anchorMax = new Vector2(Mathf.Clamp01(pct), 1f);
                    barFill.offsetMin = Vector2.zero; barFill.offsetMax = Vector2.zero;

                    y -= 46f;
                }
            }

            // Focus nodes
            var focuses = NationalFocusTree.GetFocusTree(playerFaction.factionType);
            if (focuses.Count == 0)
            {
                RectTransform noFocusRT = CreateManualPanel(parent, "NoFocus",
                    new Color(0.12f, 0.13f, 0.11f, 0.9f), 10f, y, totalWidth - 20f, 30f);
                Text noFocus = UIFactory.CreateText(noFocusRT, "Text",
                    "Aucun focus disponible pour cette faction.", 12,
                    TextAnchor.MiddleCenter, UIFactory.TextGrey);
                RectTransform nfRT = noFocus.GetComponent<RectTransform>();
                nfRT.anchorMin = Vector2.zero; nfRT.anchorMax = Vector2.one;
                nfRT.offsetMin = Vector2.zero; nfRT.offsetMax = Vector2.zero;
                y -= 36f;
            }
            else
            {
                float cardWidth = (totalWidth - 30f) / 2f;
                int col = 0;
                float rowY = y;
                foreach (var focus in focuses)
                {
                    float xPos = 10f + col * (cardWidth + 10f);
                    BuildFocusCard(parent, focus, activeFocusId, xPos, rowY, cardWidth, focusCardH);
                    col++;
                    if (col >= 2) { col = 0; rowY -= (focusCardH + 6f); }
                }
                if (col > 0) rowY -= (focusCardH + 6f);
                y = rowY;
            }

            return Mathf.Abs(y - sectionStartY) + 20f;
        }

        private void BuildFocusCard(RectTransform parent, NationalFocus focus, string activeFocusId,
            float x, float y, float w, float h)
        {
            bool isCompleted = focus.isCompleted;
            bool isActive = focus.focusId == activeFocusId;
            bool canStart = !isCompleted && !isActive && string.IsNullOrEmpty(activeFocusId);

            // Check prerequisites
            bool prereqsMet = true;
            if (focus.prerequisites != null && focus.prerequisites.Length > 0)
            {
                var tree = NationalFocusTree.GetFocusTree(playerFaction.factionType);
                foreach (string prereqId in focus.prerequisites)
                {
                    var prereq = tree.Find(f => f.focusId == prereqId);
                    if (prereq == null || !prereq.isCompleted) { prereqsMet = false; break; }
                }
            }
            canStart = canStart && prereqsMet;

            Color bgColor = isCompleted ? new Color(0.12f, 0.25f, 0.12f, 0.95f) :
                            isActive ? new Color(0.20f, 0.25f, 0.10f, 0.95f) :
                            canStart ? new Color(0.12f, 0.13f, 0.11f, 0.95f) :
                            new Color(0.06f, 0.05f, 0.04f, 0.70f);
            Color borderColor = isCompleted ? new Color(0.3f, 0.7f, 0.3f) :
                               isActive ? new Color(0.7f, 0.7f, 0.2f) :
                               canStart ? UIFactory.BorderGold :
                               new Color(0.3f, 0.3f, 0.3f);

            // Card border
            GameObject cardGO = new GameObject($"Focus_{focus.focusId}");
            cardGO.transform.SetParent(parent, false);
            RectTransform cardRT = cardGO.AddComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(0, 1); cardRT.anchorMax = new Vector2(0, 1);
            cardRT.pivot = new Vector2(0, 1);
            cardRT.anchoredPosition = new Vector2(x, y);
            cardRT.sizeDelta = new Vector2(w, h);
            Image borderImg = cardGO.AddComponent<Image>();
            borderImg.color = borderColor;

            // Inner
            GameObject innerGO = new GameObject("Inner");
            innerGO.transform.SetParent(cardGO.transform, false);
            RectTransform innerRT = innerGO.AddComponent<RectTransform>();
            innerRT.anchorMin = Vector2.zero; innerRT.anchorMax = Vector2.one;
            innerRT.offsetMin = new Vector2(2, 2); innerRT.offsetMax = new Vector2(-2, -2);
            innerGO.AddComponent<Image>().color = bgColor;

            // Name
            string prefix = isCompleted ? "✓ " : isActive ? "▶ " : "";
            Text nameText = UIFactory.CreateText(innerGO.transform, "Name", prefix + focus.displayName,
                12, TextAnchor.UpperLeft, isCompleted ? new Color(0.5f, 1f, 0.5f) :
                isActive ? new Color(1f, 1f, 0.5f) : UIFactory.GoldAccent);
            nameText.fontStyle = FontStyle.Bold;
            RectTransform nRT = nameText.GetComponent<RectTransform>();
            nRT.anchorMin = new Vector2(0, 0.7f); nRT.anchorMax = new Vector2(0.75f, 1);
            nRT.offsetMin = new Vector2(8, 0); nRT.offsetMax = new Vector2(0, -4);

            // Turns
            Text turnsText = UIFactory.CreateText(innerGO.transform, "Turns",
                $"{focus.turnsToComplete} tours", 10, TextAnchor.UpperRight, UIFactory.TextGrey);
            RectTransform tRT = turnsText.GetComponent<RectTransform>();
            tRT.anchorMin = new Vector2(0.75f, 0.7f); tRT.anchorMax = new Vector2(1, 1);
            tRT.offsetMin = new Vector2(0, 0); tRT.offsetMax = new Vector2(-8, -4);

            // Description
            Text descText = UIFactory.CreateText(innerGO.transform, "Desc", focus.description,
                10, TextAnchor.UpperLeft, UIFactory.ParchmentBeige);
            RectTransform dRT = descText.GetComponent<RectTransform>();
            dRT.anchorMin = new Vector2(0, 0.30f); dRT.anchorMax = new Vector2(1, 0.70f);
            dRT.offsetMin = new Vector2(8, 0); dRT.offsetMax = new Vector2(-8, 0);

            // Rewards
            string rewards = BuildFocusRewardsStr(focus);
            if (!string.IsNullOrEmpty(rewards))
            {
                Text rewText = UIFactory.CreateText(innerGO.transform, "Rew", rewards,
                    9, TextAnchor.UpperLeft, new Color(0.6f, 0.8f, 1f));
                RectTransform rRT = rewText.GetComponent<RectTransform>();
                rRT.anchorMin = new Vector2(0, 0.05f); rRT.anchorMax = new Vector2(1, 0.30f);
                rRT.offsetMin = new Vector2(8, 0); rRT.offsetMax = new Vector2(-8, 0);
            }

            // Prerequisites
            if (focus.prerequisites != null && focus.prerequisites.Length > 0 && !prereqsMet)
            {
                Text preText = UIFactory.CreateText(innerGO.transform, "Pre", "Prérequis manquants",
                    9, TextAnchor.LowerRight, new Color(1f, 0.4f, 0.4f));
                preText.fontStyle = FontStyle.Italic;
                RectTransform pRT = preText.GetComponent<RectTransform>();
                pRT.anchorMin = new Vector2(0.5f, 0); pRT.anchorMax = new Vector2(1, 0.15f);
                pRT.offsetMin = new Vector2(0, 2); pRT.offsetMax = new Vector2(-8, 0);
            }

            // Click to start
            if (canStart)
            {
                string capturedId = focus.focusId;
                Button btn = cardGO.AddComponent<Button>();
                btn.targetGraphic = borderImg;
                btn.onClick.AddListener(() => {
                    NationalFocusTree.StartFocus(playerFaction.factionType, capturedId);
                    RefreshPanel();
                });
            }
        }

        private string BuildFocusRewardsStr(NationalFocus focus)
        {
            List<string> parts = new List<string>();
            if (focus.goldReward > 0) parts.Add($"+{focus.goldReward:F0} or");
            if (focus.manpowerReward > 0) parts.Add($"+{focus.manpowerReward:N0} manpower");
            if (focus.stabilityChange != 0) parts.Add($"{(focus.stabilityChange > 0 ? "+" : "")}{focus.stabilityChange * 100:F0}% stabilité");
            if (focus.warSupportChange != 0) parts.Add($"{(focus.warSupportChange > 0 ? "+" : "")}{focus.warSupportChange * 100:F0}% soutien");
            if (focus.civilianFactoryReward > 0) parts.Add($"+{focus.civilianFactoryReward} ateliers civils");
            if (focus.militaryFactoryReward > 0) parts.Add($"+{focus.militaryFactoryReward} manufactures");
            return string.Join(" | ", parts);
        }

        private RectTransform CreateManualPanel(Transform parent, string name, Color color,
            float x, float y, float w, float h)
        {
            RectTransform rt = UIFactory.CreatePanel(parent, name, color);
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        // ================================ CONSCRIPTION ================================
        private void BuildConscriptionColumn(RectTransform parent, float xOff, float w, float hdrH, float cardH)
        {
            CreateColumnHeader(parent, "Conscription", "CONSCRIPTION", xOff, w, hdrH);

            float y = -(hdrH + 6f);
            foreach (ConscriptionLaw law in System.Enum.GetValues(typeof(ConscriptionLaw)))
            {
                bool isActive = playerFaction.conscriptionLaw == law;
                float ppCost = NationalLaws.GetConscriptionCost(playerFaction.conscriptionLaw, law);
                string name = NationalLaws.GetConscriptionName(law);
                string desc = GetConscriptionDesc(law);
                float stabilityPenalty = EquipmentSystem.GetConscriptionStabilityPenalty(law);
                float percent = EquipmentSystem.GetConscriptionPercent(law);

                string effectStr = $"Manpower: {percent * 100:F1}% pop.";
                if (stabilityPenalty < 0) effectStr += $" | Stab. {stabilityPenalty * 100:F0}%";
                int manpowerFromLaw = (int)(playerFaction.maxManpower * percent);
                effectStr += $"\n≈ {manpowerFromLaw:N0} hommes dispo.";

                CreateLawCard(parent, name, desc, effectStr, ppCost, isActive, xOff, y, w, cardH,
                    () => { if (NationalLaws.TryChangeConscription(playerFaction, law)) RefreshPanel(); });
                y -= (cardH + 6f);
            }
        }

        // ================================ ECONOMY ================================
        private void BuildEconomyColumn(RectTransform parent, float xOff, float w, float hdrH, float cardH)
        {
            CreateColumnHeader(parent, "Economy", "ÉCONOMIE", xOff, w, hdrH);

            float y = -(hdrH + 6f);
            foreach (EconomyLaw law in System.Enum.GetValues(typeof(EconomyLaw)))
            {
                bool isActive = playerFaction.economyLaw == law;
                float ppCost = NationalLaws.GetEconomyCost(playerFaction.economyLaw, law);
                string name = NationalLaws.GetEconomyName(law);
                string desc = GetEconomyDesc(law);
                float prodMod = EquipmentSystem.GetEconomyModifier(law);
                float stabilityPenalty = EquipmentSystem.GetEconomyStabilityPenalty(law);

                string effectStr = $"Production: ×{prodMod:F2}";
                if (stabilityPenalty < 0) effectStr += $" | Stab. {stabilityPenalty * 100:F0}%";
                float goldMod = law switch
                {
                    EconomyLaw.CivilianEconomy => 1.0f,
                    EconomyLaw.PreMobilization => 0.95f,
                    EconomyLaw.WarEconomy => 0.85f,
                    EconomyLaw.TotalWar => 0.70f,
                    _ => 1.0f
                };
                if (goldMod < 1.0f) effectStr += $"\nRevenu civil: {goldMod * 100:F0}%";

                CreateLawCard(parent, name, desc, effectStr, ppCost, isActive, xOff, y, w, cardH,
                    () => { if (NationalLaws.TryChangeEconomy(playerFaction, law)) RefreshPanel(); });
                y -= (cardH + 6f);
            }
        }

        // ================================ TRADE ================================
        private void BuildTradeColumn(RectTransform parent, float xOff, float w, float hdrH, float cardH)
        {
            CreateColumnHeader(parent, "Trade", "COMMERCE", xOff, w, hdrH);

            float y = -(hdrH + 6f);
            foreach (TradeLaw law in System.Enum.GetValues(typeof(TradeLaw)))
            {
                bool isActive = playerFaction.tradeLaw == law;
                float ppCost = NationalLaws.GetTradeCost(playerFaction.tradeLaw, law);
                string name = NationalLaws.GetTradeName(law);
                string desc = GetTradeDesc(law);

                string effectStr = law switch
                {
                    TradeLaw.FreeTradePolicy => "Recherche: +15% | Rétention: -20%\nRelations: +10",
                    TradeLaw.ExportFocus => "Recherche: +5% | Rétention: -10%\nRelations: +5",
                    TradeLaw.LimitedExports => "Recherche: +0% | Rétention: +0%",
                    TradeLaw.ClosedEconomy => "Recherche: -5% | Rétention: +20%\nRelations: -10",
                    _ => ""
                };

                CreateLawCard(parent, name, desc, effectStr, ppCost, isActive, xOff, y, w, cardH,
                    () => { if (NationalLaws.TryChangeTrade(playerFaction, law)) RefreshPanel(); });
                y -= (cardH + 6f);
            }
        }

        // ================================ UI HELPERS ================================

        private void CreateColumnHeader(Transform parent, string id, string title, float x, float width, float height)
        {
            RectTransform header = UIFactory.CreatePanel(parent, $"Header_{id}",
                new Color(0.16f, 0.18f, 0.15f, 0.95f));
            header.anchorMin = new Vector2(0, 1);
            header.anchorMax = new Vector2(0, 1);
            header.pivot = new Vector2(0, 1);
            header.anchoredPosition = new Vector2(x, 0);
            header.sizeDelta = new Vector2(width, height);

            Text headerText = UIFactory.CreateText(header, "Text", title, 16,
                TextAnchor.MiddleCenter, UIFactory.GoldAccent);
            headerText.fontStyle = FontStyle.Bold;
            RectTransform htRT = headerText.GetComponent<RectTransform>();
            htRT.anchorMin = Vector2.zero; htRT.anchorMax = Vector2.one;
            htRT.offsetMin = Vector2.zero; htRT.offsetMax = Vector2.zero;
        }

        private void CreateLawCard(Transform parent, string name, string description, string effects,
            float ppCost, bool isActive, float x, float y, float width, float height,
            UnityEngine.Events.UnityAction onClick)
        {
            Color bgColor = isActive
                ? new Color(0.15f, 0.30f, 0.15f, 0.95f)
                : new Color(0.12f, 0.13f, 0.11f, 0.95f);
            Color borderColor = isActive ? new Color(0.3f, 0.7f, 0.3f) : UIFactory.BorderGold;

            // Card container with border
            GameObject cardGO = new GameObject($"Law_{name}");
            cardGO.transform.SetParent(parent, false);
            RectTransform cardRT = cardGO.AddComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(0, 1);
            cardRT.anchorMax = new Vector2(0, 1);
            cardRT.pivot = new Vector2(0, 1);
            cardRT.anchoredPosition = new Vector2(x, y);
            cardRT.sizeDelta = new Vector2(width, height);

            Image borderImg = cardGO.AddComponent<Image>();
            borderImg.color = borderColor;

            // Inner panel
            GameObject innerGO = new GameObject("Inner");
            innerGO.transform.SetParent(cardGO.transform, false);
            RectTransform innerRT = innerGO.AddComponent<RectTransform>();
            innerRT.anchorMin = Vector2.zero; innerRT.anchorMax = Vector2.one;
            innerRT.offsetMin = new Vector2(2, 2); innerRT.offsetMax = new Vector2(-2, -2);
            Image innerImg = innerGO.AddComponent<Image>();
            innerImg.color = bgColor;

            // Law name
            Text nameText = UIFactory.CreateText(innerGO.transform, "Name",
                isActive ? $"✓ {name}" : name,
                13, TextAnchor.UpperLeft, isActive ? new Color(0.5f, 1f, 0.5f) : UIFactory.GoldAccent);
            nameText.fontStyle = FontStyle.Bold;
            RectTransform nameRT = nameText.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0, 0.75f);
            nameRT.anchorMax = new Vector2(0.7f, 1);
            nameRT.offsetMin = new Vector2(8, 0);
            nameRT.offsetMax = new Vector2(0, -4);

            // PP cost (top right)
            if (!isActive)
            {
                bool canAfford = playerFaction.politicalPower >= ppCost;
                Text costText = UIFactory.CreateText(innerGO.transform, "Cost",
                    ppCost > 0 ? $"{ppCost:F0} PP" : "Gratuit",
                    12, TextAnchor.UpperRight,
                    canAfford ? new Color(0.6f, 0.7f, 1f) : new Color(1f, 0.3f, 0.3f));
                costText.fontStyle = FontStyle.Bold;
                RectTransform costRT = costText.GetComponent<RectTransform>();
                costRT.anchorMin = new Vector2(0.7f, 0.75f);
                costRT.anchorMax = new Vector2(1, 1);
                costRT.offsetMin = new Vector2(0, 0);
                costRT.offsetMax = new Vector2(-8, -4);
            }

            // Description
            Text descText = UIFactory.CreateText(innerGO.transform, "Desc", description,
                10, TextAnchor.UpperLeft, UIFactory.TextGrey);
            RectTransform descRT = descText.GetComponent<RectTransform>();
            descRT.anchorMin = new Vector2(0, 0.45f);
            descRT.anchorMax = new Vector2(1, 0.75f);
            descRT.offsetMin = new Vector2(8, 0);
            descRT.offsetMax = new Vector2(-8, 0);

            // Effects
            if (!string.IsNullOrEmpty(effects))
            {
                Text effText = UIFactory.CreateText(innerGO.transform, "Effects", effects,
                    10, TextAnchor.UpperLeft, UIFactory.ParchmentBeige);
                RectTransform effRT = effText.GetComponent<RectTransform>();
                effRT.anchorMin = new Vector2(0, 0.05f);
                effRT.anchorMax = new Vector2(1, 0.45f);
                effRT.offsetMin = new Vector2(8, 4);
                effRT.offsetMax = new Vector2(-8, 0);
            }

            // Active marker
            if (isActive)
            {
                Text activeText = UIFactory.CreateText(innerGO.transform, "Active", "EN VIGUEUR",
                    10, TextAnchor.LowerRight, new Color(0.3f, 0.9f, 0.3f));
                activeText.fontStyle = FontStyle.Bold;
                RectTransform activeRT = activeText.GetComponent<RectTransform>();
                activeRT.anchorMin = new Vector2(0.6f, 0);
                activeRT.anchorMax = new Vector2(1, 0.15f);
                activeRT.offsetMin = new Vector2(0, 2);
                activeRT.offsetMax = new Vector2(-8, 0);
            }

            // Click handler
            if (!isActive)
            {
                Button btn = cardGO.AddComponent<Button>();
                btn.targetGraphic = borderImg;
                ColorBlock cb = btn.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(1.3f, 1.2f, 1.1f);
                cb.pressedColor = new Color(0.8f, 0.7f, 0.6f);
                btn.colors = cb;
                btn.onClick.AddListener(onClick);
            }
        }

        private void RefreshPanel()
        {
            navBar.TogglePanel(NavigationBar.NavPanel.Laws);
            navBar.TogglePanel(NavigationBar.NavPanel.Laws);
        }

        // ================================ DESCRIPTIONS ================================

        private string GetConscriptionDesc(ConscriptionLaw law) => law switch
        {
            ConscriptionLaw.Volunteer => "Seuls les volontaires servent. Manpower faible mais pas de pénalité.",
            ConscriptionLaw.LimitedConscription => "Conscription de base. Bon équilibre manpower/stabilité.",
            ConscriptionLaw.ExtendedConscription => "Plus d'hommes disponibles. Légère baisse de stabilité.",
            ConscriptionLaw.ServiceByRequirement => "Service obligatoire étendu. Impact notable sur la stabilité.",
            ConscriptionLaw.TotalMobilization => "Mobilisation totale. Manpower massif mais stabilité en chute.",
            _ => ""
        };

        private string GetEconomyDesc(EconomyLaw law) => law switch
        {
            EconomyLaw.CivilianEconomy => "Économie civile standard. Pas de bonus de production militaire.",
            EconomyLaw.PreMobilization => "Début de mobilisation industrielle. +10% production militaire.",
            EconomyLaw.WarEconomy => "Économie orientée guerre. +25% production, -10% stabilité.",
            EconomyLaw.TotalWar => "Tout pour la guerre. +50% production, -20% stabilité.",
            _ => ""
        };

        private string GetTradeDesc(TradeLaw law) => law switch
        {
            TradeLaw.FreeTradePolicy => "Commerce ouvert. Recherche accélérée, perte de ressources.",
            TradeLaw.ExportFocus => "Focus export. Léger bonus recherche, perte modérée.",
            TradeLaw.LimitedExports => "Exports restreints. Équilibre neutre.",
            TradeLaw.ClosedEconomy => "Autarcie. Rétention maximale, recherche ralentie.",
            _ => ""
        };
    }
}
