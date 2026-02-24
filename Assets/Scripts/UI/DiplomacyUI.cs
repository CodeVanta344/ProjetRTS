using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Campaign;
using NapoleonicWars.Data;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// Premium Diplomacy overlay — HoI4/EU4 inspired with full diplomatic actions,
    /// relation gauges, faction detail panels, and styled offers.
    /// </summary>
    public class DiplomacyUI : MonoBehaviour
    {
        private CampaignManager campaignManager;
        private FactionData playerFaction;
        private NavigationBar navBar;
        private Transform scrollContent;

        // Colors
        private static readonly Color WarRed = new Color(0.75f, 0.15f, 0.12f);
        private static readonly Color HostileOrange = new Color(0.80f, 0.45f, 0.15f);
        private static readonly Color NeutralGrey = new Color(0.55f, 0.55f, 0.55f);
        private static readonly Color FriendlyGreen = new Color(0.35f, 0.65f, 0.35f);
        private static readonly Color AllyBlue = new Color(0.25f, 0.55f, 0.85f);
        private static readonly Color CardBg = new Color(0.09f, 0.08f, 0.06f, 0.95f);
        private static readonly Color SectionBg = new Color(0.14f, 0.12f, 0.08f, 0.95f);
        private static readonly Color ActionBtnBg = new Color(0.12f, 0.10f, 0.07f, 0.95f);

        public static DiplomacyUI Create(NavigationBar navBar, CampaignManager manager)
        {
            var overlay = navBar.CreateOverlayPanel("DIPLOMATIE");
            if (overlay == null) return null;

            var ui = overlay.AddComponent<DiplomacyUI>();
            ui.navBar = navBar;
            ui.campaignManager = manager;
            ui.playerFaction = manager.GetPlayerFaction();
            navBar.RegisterPanel(NavigationBar.NavPanel.Diplomacy, overlay);

            ui.BuildContent(overlay.transform);
            return ui;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  BUILD MAIN CONTENT
        // ═══════════════════════════════════════════════════════════════════

        private void BuildContent(Transform parent)
        {
            var (scroll, content) = UIFactory.CreateScrollView(parent, "DiplomacyScroll");
            UIFactory.AddLayoutElement(scroll.gameObject, flexibleHeight: 1, preferredHeight: 900);
            scrollContent = content;

            // === PLAYER RESOURCE BAR ===
            BuildResourceBar(content);

            // === PENDING OFFERS (if any) ===
            BuildPendingOffers(content);

            // === ACTIVE TREATIES ===
            BuildActiveTreaties(content);

            // === FACTION TABLE ===
            BuildFactionTable(content);

            // === HISTORY ===
            BuildHistory(content);
        }

        // ─────────────────────────────────────────────────────────────────
        //  RESOURCE BAR
        // ─────────────────────────────────────────────────────────────────

        private void BuildResourceBar(Transform parent)
        {
            RectTransform bar = UIFactory.CreateGradientPanel(parent, "ResourceBar",
                new Color(0.14f, 0.11f, 0.07f, 0.98f),
                new Color(0.09f, 0.07f, 0.04f, 0.98f),
                new Color(0.18f, 0.14f, 0.08f, 0.2f));
            UIFactory.AddLayoutElement(bar.gameObject, preferredHeight: 50);

            HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(bar.gameObject, 0f, new RectOffset(20, 20, 0, 0));
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childAlignment = TextAnchor.MiddleCenter;

            float pp = playerFaction?.politicalPower ?? 0;
            float stab = playerFaction?.stability ?? 0;
            float ws = playerFaction?.warSupport ?? 0;

            CreateResourceBadge(bar, "⚜ Pouvoir Politique", $"{pp:F0} PP", UIFactory.GoldAccent, 220);

            // Separator
            RectTransform sep1 = UIFactory.CreateGlowSeparator(bar, "Sep1", true);
            UIFactory.AddLayoutElement(sep1.gameObject, preferredWidth: 2, preferredHeight: 30);

            Color stabColor = stab > 0.6f ? FriendlyGreen : stab > 0.35f ? HostileOrange : WarRed;
            CreateResourceBadge(bar, "⚖ Stabilité", $"{(int)(stab * 100)}%", stabColor, 170);

            RectTransform sep2 = UIFactory.CreateGlowSeparator(bar, "Sep2", true);
            UIFactory.AddLayoutElement(sep2.gameObject, preferredWidth: 2, preferredHeight: 30);

            Color wsColor = ws > 0.5f ? FriendlyGreen : ws > 0.25f ? HostileOrange : WarRed;
            CreateResourceBadge(bar, "⚔ Soutien de Guerre", $"{(int)(ws * 100)}%", wsColor, 200);

            RectTransform sep3 = UIFactory.CreateGlowSeparator(bar, "Sep3", true);
            UIFactory.AddLayoutElement(sep3.gameObject, preferredWidth: 2, preferredHeight: 30);

            // Faction count
            int factionCount = 0;
            int atWar = 0;
            int allies = 0;
            if (campaignManager != null)
            {
                foreach (var kvp in campaignManager.Factions)
                {
                    if (kvp.Key == playerFaction.factionType || kvp.Value.isEliminated) continue;
                    factionCount++;
                    try
                    {
                        var state = DiplomacySystem.Instance?.GetRelationState(playerFaction.factionType, kvp.Key) ?? DiplomacyState.Neutral;
                        if (state == DiplomacyState.War) atWar++;
                        if (state == DiplomacyState.Alliance) allies++;
                    }
                    catch { }
                }
            }
            CreateResourceBadge(bar, "🌍 Nations", $"{factionCount} ({allies} alliés, {atWar} en guerre)", UIFactory.SilverText, 300);
        }

        private void CreateResourceBadge(Transform parent, string label, string value, Color valueColor, float width)
        {
            GameObject go = new GameObject(label);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            UIFactory.AddLayoutElement(go, preferredWidth: width, preferredHeight: 44);

            Text labelText = UIFactory.CreateText(go.transform, "Label", label, 10, TextAnchor.LowerCenter, UIFactory.SilverText);
            RectTransform lRT = labelText.GetComponent<RectTransform>();
            lRT.anchorMin = new Vector2(0, 0.5f); lRT.anchorMax = new Vector2(1, 1);
            lRT.offsetMin = Vector2.zero; lRT.offsetMax = Vector2.zero;

            Text valueText = UIFactory.CreateText(go.transform, "Value", value, 14, TextAnchor.UpperCenter, valueColor);
            valueText.fontStyle = FontStyle.Bold;
            RectTransform vRT = valueText.GetComponent<RectTransform>();
            vRT.anchorMin = new Vector2(0, 0); vRT.anchorMax = new Vector2(1, 0.55f);
            vRT.offsetMin = Vector2.zero; vRT.offsetMax = Vector2.zero;
        }

        // ─────────────────────────────────────────────────────────────────
        //  PENDING OFFERS
        // ─────────────────────────────────────────────────────────────────

        private void BuildPendingOffers(Transform parent)
        {
            List<DiplomaticOffer> incoming = new List<DiplomaticOffer>();
            try
            {
                if (DiplomacySystem.Instance != null)
                    foreach (var offer in DiplomacySystem.Instance.PendingOffers)
                        if (offer.toFaction == playerFaction.factionType) incoming.Add(offer);
            }
            catch { }

            if (incoming.Count == 0) return;

            CreateSectionHeader(parent, $"📨 OFFRES DIPLOMATIQUES EN ATTENTE ({incoming.Count})");

            foreach (var offer in incoming)
                CreateOfferCard(parent, offer);
        }

        private void CreateOfferCard(Transform parent, DiplomaticOffer offer)
        {
            RectTransform cardRT = UIFactory.CreateBorderedPanel(parent, $"Offer_{offer.offerId}",
                new Color(0.12f, 0.10f, 0.06f, 0.95f), UIFactory.MutedGold, 1.5f);
            UIFactory.AddLayoutElement(cardRT.gameObject, preferredHeight: 55);

            Transform inner = cardRT.transform.childCount > 0 ? cardRT.transform.GetChild(0) : cardRT.transform;
            HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(inner.gameObject, 10f, new RectOffset(15, 15, 6, 6));
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            string fromName = CampaignManager.GetFactionSubtitle(offer.fromFaction);
            string actionName = GetActionNameFrench(offer.actionType);
            int turnsLeft = offer.expiresInTurns - ((CampaignManager.Instance?.CurrentTurn ?? 0) - offer.turnProposed);

            // Icon
            Text icon = UIFactory.CreateText(inner, "Icon", "📜", 18, TextAnchor.MiddleCenter, Color.white);
            UIFactory.AddLayoutElement(icon.gameObject, preferredWidth: 30, preferredHeight: 40);

            // Text
            Text offerText = UIFactory.CreateText(inner, "Text",
                $"{fromName} propose : {actionName}", 13, TextAnchor.MiddleLeft, UIFactory.Parchment);
            offerText.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(offerText.gameObject, preferredWidth: 380, preferredHeight: 40);

            // Timer
            Text timerText = UIFactory.CreateText(inner, "Timer",
                $"⏳ {turnsLeft} tours", 11, TextAnchor.MiddleCenter, UIFactory.SilverText);
            UIFactory.AddLayoutElement(timerText.gameObject, preferredWidth: 80, preferredHeight: 40);

            // Accept
            string capturedId = offer.offerId;
            Button acceptBtn = UIFactory.CreateButton(inner, "Accept", "✓ Accepter", 12, () => DoAcceptOffer(capturedId));
            UIFactory.AddLayoutElement(acceptBtn.gameObject, preferredWidth: 110, preferredHeight: 32);
            acceptBtn.GetComponent<Image>().color = new Color(0.15f, 0.35f, 0.15f, 0.95f);

            // Reject
            Button rejectBtn = UIFactory.CreateButton(inner, "Reject", "✕ Refuser", 12, () => DoRejectOffer(capturedId));
            UIFactory.AddLayoutElement(rejectBtn.gameObject, preferredWidth: 100, preferredHeight: 32);
            rejectBtn.GetComponent<Image>().color = new Color(0.45f, 0.12f, 0.12f, 0.95f);
        }

        // ─────────────────────────────────────────────────────────────────
        //  ACTIVE TREATIES
        // ─────────────────────────────────────────────────────────────────

        private void BuildActiveTreaties(Transform parent)
        {
            CreateSectionHeader(parent, "📋 TRAITÉS ACTIFS");

            bool hasTreaties = false;
            try
            {
                if (DiplomacySystem.Instance != null)
                {
                    foreach (var kvp in DiplomacySystem.Instance.ActiveTreaties)
                    {
                        var treaty = kvp.Value;
                        if (!treaty.factions.Contains(playerFaction.factionType)) continue;
                        hasTreaties = true;
                        CreateTreatyRow(parent, treaty);
                    }
                }
            }
            catch { }

            if (!hasTreaties)
            {
                RectTransform emptyRT = UIFactory.CreatePanel(parent, "NoTreaty", CardBg);
                UIFactory.AddLayoutElement(emptyRT.gameObject, preferredHeight: 30);
                Text txt = UIFactory.CreateText(emptyRT, "T", "  Aucun traité actif.", 12, TextAnchor.MiddleLeft, UIFactory.SilverText);
                FillAnchors(txt, 15);
            }
        }

        private void CreateTreatyRow(Transform parent, Treaty treaty)
        {
            FactionType other = FactionType.France;
            foreach (var f in treaty.factions) if (f != playerFaction.factionType) other = f;

            string otherName = CampaignManager.GetFactionSubtitle(other);
            string typeStr = GetTreatyTypeFrench(treaty.type);
            string durationStr = treaty.duration > 0 ? $" ({treaty.duration} tours)" : " (permanent)";

            Color treatyColor = treaty.type switch
            {
                TreatyType.Alliance => AllyBlue,
                TreatyType.TradeAgreement => FriendlyGreen,
                TreatyType.MilitaryAccess => new Color(0.55f, 0.70f, 0.85f),
                TreatyType.Vassalage => UIFactory.GoldAccent,
                TreatyType.NonAggression => NeutralGrey,
                TreatyType.Guarantee => new Color(0.70f, 0.55f, 0.85f),
                _ => UIFactory.SilverText
            };

            string icon = treaty.type switch
            {
                TreatyType.Alliance => "🤝",
                TreatyType.TradeAgreement => "💰",
                TreatyType.MilitaryAccess => "🚩",
                TreatyType.Vassalage => "👑",
                TreatyType.NonAggression => "🕊",
                TreatyType.Guarantee => "🛡",
                _ => "📜"
            };

            RectTransform rowRT = UIFactory.CreatePanel(parent, $"Treaty_{treaty.treatyId}",
                new Color(0.07f, 0.09f, 0.06f, 0.90f));
            UIFactory.AddLayoutElement(rowRT.gameObject, preferredHeight: 32);

            HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(rowRT.gameObject, 8f, new RectOffset(15, 15, 3, 3));
            hlg.childControlWidth = false; hlg.childControlHeight = true;

            Text iconText = UIFactory.CreateText(rowRT, "Icon", icon, 14, TextAnchor.MiddleCenter, Color.white);
            UIFactory.AddLayoutElement(iconText.gameObject, preferredWidth: 25, preferredHeight: 26);

            Text text = UIFactory.CreateText(rowRT, "Text",
                $"{typeStr} avec {otherName}{durationStr}", 12, TextAnchor.MiddleLeft, treatyColor);
            text.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(text.gameObject, preferredWidth: 500, preferredHeight: 26);
        }

        // ─────────────────────────────────────────────────────────────────
        //  FACTION TABLE
        // ─────────────────────────────────────────────────────────────────

        private void BuildFactionTable(Transform parent)
        {
            CreateSectionHeader(parent, "🌐 RELATIONS AVEC LES NATIONS");

            if (campaignManager == null) return;

            foreach (var kvp in campaignManager.Factions)
            {
                if (kvp.Key == playerFaction.factionType) continue;
                if (kvp.Value.isEliminated) continue;
                CreateFactionCard(parent, kvp.Key, kvp.Value);
            }
        }

        private void CreateFactionCard(Transform parent, FactionType fType, FactionData faction)
        {
            DiplomacyState state = DiplomacyState.Neutral;
            int score = 0;
            try
            {
                if (DiplomacySystem.Instance != null)
                {
                    state = DiplomacySystem.Instance.GetRelationState(playerFaction.factionType, fType);
                    score = DiplomacySystem.Instance.GetRelationScore(playerFaction.factionType, fType);
                }
            }
            catch { }

            // Card background based on relation
            Color cardBg = state switch
            {
                DiplomacyState.War => new Color(0.18f, 0.06f, 0.04f, 0.95f),
                DiplomacyState.Alliance => new Color(0.05f, 0.10f, 0.18f, 0.95f),
                DiplomacyState.Friendly => new Color(0.06f, 0.12f, 0.06f, 0.95f),
                DiplomacyState.Hostile => new Color(0.16f, 0.10f, 0.05f, 0.95f),
                _ => CardBg
            };

            Color borderColor = GetRelationColor(state);

            RectTransform cardRT = UIFactory.CreateBorderedPanel(parent, $"Faction_{fType}", cardBg, borderColor, 1.5f);
            UIFactory.AddLayoutElement(cardRT.gameObject, preferredHeight: 125);

            Transform inner = cardRT.transform.childCount > 0 ? cardRT.transform.GetChild(0) : cardRT.transform;

            // === TOP ROW: Name | Status | Score | Strength ===
            RectTransform topRow = UIFactory.CreatePanel(inner, "TopRow", Color.clear);
            topRow.anchorMin = new Vector2(0, 0.60f);
            topRow.anchorMax = Vector2.one;
            topRow.offsetMin = new Vector2(15, 0);
            topRow.offsetMax = new Vector2(-15, -5);
            HorizontalLayoutGroup topHLG = UIFactory.AddHorizontalLayout(topRow.gameObject, 10f, new RectOffset(0, 0, 0, 0));
            topHLG.childControlWidth = false; topHLG.childControlHeight = true;

            // Faction name
            string fName = CampaignManager.GetFactionSubtitle(fType);
            Text nameText = UIFactory.CreateText(topRow, "Name", fName, 16, TextAnchor.MiddleLeft, UIFactory.Porcelain);
            nameText.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(nameText.gameObject, preferredWidth: 200, preferredHeight: 36);

            // Status badge
            string stateStr = GetStateFrench(state);
            string stateIcon = state switch 
            {
                DiplomacyState.War => "⚔",
                DiplomacyState.Hostile => "😡",
                DiplomacyState.Neutral => "😐",
                DiplomacyState.Friendly => "😊",
                DiplomacyState.Alliance => "🤝",
                _ => ""
            };
            Color stateColor = GetRelationColor(state);
            Text stateText = UIFactory.CreateText(topRow, "State", $"{stateIcon} {stateStr}", 13, TextAnchor.MiddleCenter, stateColor);
            stateText.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(stateText.gameObject, preferredWidth: 130, preferredHeight: 36);

            // Relation gauge
            CreateRelationGauge(topRow, score);

            // Military strength
            int armyCount = 0;
            int totalSoldiers = 0;
            if (campaignManager.Armies != null)
                foreach (var a in campaignManager.Armies.Values)
                    if (a.faction == fType) { armyCount++; totalSoldiers += a.TotalSoldiers; }

            Text strengthText = UIFactory.CreateText(topRow, "Strength",
                $"⚔ {armyCount} armées ({totalSoldiers:N0})", 11, TextAnchor.MiddleRight, UIFactory.SilverText);
            UIFactory.AddLayoutElement(strengthText.gameObject, preferredWidth: 180, preferredHeight: 36);

            // Province count
            Text provText = UIFactory.CreateText(topRow, "Prov",
                $"🏰 {faction.ownedProvinceIds.Count} prov.", 11, TextAnchor.MiddleRight, UIFactory.SilverText);
            UIFactory.AddLayoutElement(provText.gameObject, preferredWidth: 90, preferredHeight: 36);

            // === BOTTOM ROW: Action Buttons ===
            RectTransform bottomRow = UIFactory.CreatePanel(inner, "BottomRow", new Color(0.05f, 0.04f, 0.03f, 0.5f));
            bottomRow.anchorMin = Vector2.zero;
            bottomRow.anchorMax = new Vector2(1, 0.58f);
            bottomRow.offsetMin = new Vector2(10, 5);
            bottomRow.offsetMax = new Vector2(-10, -2);
            HorizontalLayoutGroup botHLG = UIFactory.AddHorizontalLayout(bottomRow.gameObject, 6f, new RectOffset(5, 5, 3, 3));
            botHLG.childControlWidth = false; botHLG.childControlHeight = true;

            CreateActionButtons(bottomRow, fType, state, score);
        }

        private void CreateRelationGauge(Transform parent, int score)
        {
            // Container
            GameObject gaugeGO = new GameObject("Gauge");
            gaugeGO.transform.SetParent(parent, false);
            UIFactory.AddLayoutElement(gaugeGO, preferredWidth: 150, preferredHeight: 36);

            // Label
            string scoreSign = score >= 0 ? "+" : "";
            Color scoreColor = score > 20 ? FriendlyGreen : score > 0 ? new Color(0.6f, 0.8f, 0.5f) :
                              score < -20 ? WarRed : score < 0 ? HostileOrange : NeutralGrey;

            Text scoreText = UIFactory.CreateText(gaugeGO.transform, "Score", $"{scoreSign}{score}", 14, TextAnchor.UpperCenter, scoreColor);
            scoreText.fontStyle = FontStyle.Bold;
            RectTransform sRT = scoreText.GetComponent<RectTransform>();
            sRT.anchorMin = new Vector2(0, 0.45f); sRT.anchorMax = Vector2.one;
            sRT.offsetMin = Vector2.zero; sRT.offsetMax = Vector2.zero;

            // Bar background
            RectTransform barBg = UIFactory.CreatePanel(gaugeGO.transform, "BarBg", new Color(0.15f, 0.15f, 0.15f, 0.8f));
            barBg.anchorMin = new Vector2(0.05f, 0.15f);
            barBg.anchorMax = new Vector2(0.95f, 0.42f);
            barBg.offsetMin = Vector2.zero;
            barBg.offsetMax = Vector2.zero;

            // Fill (centered at 50%, extends left or right based on score)
            float normalizedScore = Mathf.Clamp(score / 100f, -1f, 1f);
            float fillMin = normalizedScore >= 0 ? 0.5f : 0.5f + normalizedScore * 0.5f;
            float fillMax = normalizedScore >= 0 ? 0.5f + normalizedScore * 0.5f : 0.5f;

            RectTransform fillBar = UIFactory.CreatePanel(barBg, "Fill", scoreColor);
            fillBar.anchorMin = new Vector2(fillMin, 0.1f);
            fillBar.anchorMax = new Vector2(fillMax, 0.9f);
            fillBar.offsetMin = Vector2.zero;
            fillBar.offsetMax = Vector2.zero;

            // Center line
            RectTransform centerLine = UIFactory.CreatePanel(barBg, "Center", new Color(1, 1, 1, 0.4f));
            centerLine.anchorMin = new Vector2(0.495f, 0);
            centerLine.anchorMax = new Vector2(0.505f, 1);
            centerLine.offsetMin = Vector2.zero;
            centerLine.offsetMax = Vector2.zero;
        }

        // ─────────────────────────────────────────────────────────────────
        //  ACTION BUTTONS
        // ─────────────────────────────────────────────────────────────────

        private void CreateActionButtons(Transform parent, FactionType target, DiplomacyState state, int score)
        {
            float pp = playerFaction?.politicalPower ?? 0;

            if (state == DiplomacyState.War)
            {
                // War actions
                CreateActionBtn(parent, "🕊 Proposer la paix", "10 PP", 140,
                    pp >= 10 ? ActionBtnBg : new Color(0.15f, 0.15f, 0.15f, 0.5f),
                    new Color(0.5f, 0.8f, 0.5f), () => DoProposePeace(target), pp >= 10);

                CreateActionBtn(parent, "👑 Exiger vassalité", "100 PP", 150,
                    pp >= 100 ? ActionBtnBg : new Color(0.15f, 0.15f, 0.15f, 0.5f),
                    UIFactory.GoldAccent, () => DoDemandVassal(target), pp >= 100);
            }
            else if (state == DiplomacyState.Alliance)
            {
                // Alliance actions
                CreateActionBtn(parent, "💔 Rompre alliance", "", 140,
                    new Color(0.25f, 0.10f, 0.10f, 0.9f), WarRed,
                    () => DoBreakAlliance(target), true);

                CreateActionBtn(parent, "🎁 Cadeau (200g)", "", 130,
                    ActionBtnBg, FriendlyGreen, () => DoSendGift(target),
                    playerFaction.gold >= 200);
            }
            else
            {
                // Neutral/Friendly/Hostile actions

                // Declare War
                CreateActionBtn(parent, "⚔ Guerre", "20 PP", 100,
                    new Color(0.30f, 0.08f, 0.08f, 0.95f), WarRed,
                    () => DoDeclareWar(target), pp >= 20);

                // Alliance
                bool hasAlliance = false;
                try { hasAlliance = DiplomacySystem.Instance?.HasTreaty(playerFaction.factionType, target, TreatyType.Alliance) ?? false; } catch { }
                if (!hasAlliance)
                {
                    CreateActionBtn(parent, "🤝 Alliance", "35 PP", 110,
                        ActionBtnBg, AllyBlue, () => DoProposeAlliance(target), pp >= 35 && score > -20);
                }

                // Trade
                bool hasTrade = false;
                try { hasTrade = DiplomacySystem.Instance?.HasTreaty(playerFaction.factionType, target, TreatyType.TradeAgreement) ?? false; } catch { }
                if (!hasTrade)
                {
                    CreateActionBtn(parent, "💰 Commerce", "15 PP", 110,
                        ActionBtnBg, FriendlyGreen, () => DoProposeTrade(target), pp >= 15);
                }

                // Non-aggression
                bool hasNAP = false;
                try { hasNAP = DiplomacySystem.Instance?.HasTreaty(playerFaction.factionType, target, TreatyType.NonAggression) ?? false; } catch { }
                if (!hasNAP)
                {
                    CreateActionBtn(parent, "🕊 Non-agression", "30 PP", 130,
                        ActionBtnBg, NeutralGrey, () => DoNonAggression(target), pp >= 30);
                }

                // Military access
                bool hasAccess = false;
                try { hasAccess = DiplomacySystem.Instance?.HasTreaty(playerFaction.factionType, target, TreatyType.MilitaryAccess) ?? false; } catch { }
                if (!hasAccess)
                {
                    CreateActionBtn(parent, "🚩 Accès mil.", "20 PP", 110,
                        ActionBtnBg, new Color(0.55f, 0.70f, 0.85f), () => DoRequestAccess(target), pp >= 20);
                }

                // Improve relations
                CreateActionBtn(parent, "💬 Améliorer", "25 PP", 110,
                    ActionBtnBg, new Color(0.5f, 0.75f, 0.5f), () => DoImproveRelations(target), pp >= 25);

                // Gift
                CreateActionBtn(parent, "🎁 Cadeau", "200g", 100,
                    ActionBtnBg, FriendlyGreen, () => DoSendGift(target), playerFaction.gold >= 200);

                // Insult
                if (state != DiplomacyState.Friendly)
                {
                    CreateActionBtn(parent, "😤 Insulter", "", 100,
                        new Color(0.20f, 0.08f, 0.05f, 0.9f), HostileOrange, () => DoInsult(target), true);
                }

                // Guarantee
                bool hasGuarantee = false;
                try { hasGuarantee = DiplomacySystem.Instance?.HasTreaty(playerFaction.factionType, target, TreatyType.Guarantee) ?? false; } catch { }
                if (!hasGuarantee)
                {
                    CreateActionBtn(parent, "🛡 Garantir", "50 PP", 110,
                        ActionBtnBg, new Color(0.70f, 0.55f, 0.85f), () => DoGuarantee(target), pp >= 50);
                }
            }
        }

        private void CreateActionBtn(Transform parent, string label, string cost, float width,
            Color bgColor, Color textColor, UnityEngine.Events.UnityAction action, bool enabled)
        {
            string fullLabel = string.IsNullOrEmpty(cost) ? label : $"{label}\n<size=9>{cost}</size>";

            Button btn = UIFactory.CreateButton(parent, label.GetHashCode().ToString(), fullLabel, 10, action);
            UIFactory.AddLayoutElement(btn.gameObject, preferredWidth: width, preferredHeight: 38);
            Image img = btn.GetComponent<Image>();
            img.color = bgColor;

            Text txt = btn.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.color = enabled ? textColor : new Color(textColor.r, textColor.g, textColor.b, 0.35f);
                txt.alignment = TextAnchor.MiddleCenter;
            }

            btn.interactable = enabled;

            if (enabled)
            {
                Outline outline = btn.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(textColor.r, textColor.g, textColor.b, 0.3f);
                outline.effectDistance = new Vector2(1, 1);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  HISTORY
        // ─────────────────────────────────────────────────────────────────

        private void BuildHistory(Transform parent)
        {
            CreateSectionHeader(parent, "📜 HISTORIQUE DIPLOMATIQUE");

            bool hasHistory = false;
            try
            {
                if (DiplomacySystem.Instance != null)
                {
                    var history = DiplomacySystem.Instance.GetHistory(playerFaction.factionType, 15);
                    foreach (var entry in history)
                    {
                        hasHistory = true;
                        Color entryColor = entry.action switch
                        {
                            DiplomaticActionType.DeclareWar => WarRed,
                            DiplomaticActionType.AcceptPeace => FriendlyGreen,
                            DiplomaticActionType.FormAlliance => AllyBlue,
                            DiplomaticActionType.BreakAlliance => HostileOrange,
                            DiplomaticActionType.InsultFaction => HostileOrange,
                            DiplomaticActionType.SendGift => FriendlyGreen,
                            DiplomaticActionType.ImproveRelations => FriendlyGreen,
                            _ => UIFactory.SilverText
                        };

                        RectTransform histRT = UIFactory.CreatePanel(parent, $"Hist_{entry.turn}_{entry.action}",
                            new Color(0.08f, 0.07f, 0.06f, 0.7f));
                        UIFactory.AddLayoutElement(histRT.gameObject, preferredHeight: 24);

                        HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(histRT.gameObject, 8f, new RectOffset(15, 15, 1, 1));
                        hlg.childControlWidth = false; hlg.childControlHeight = true;

                        Text turnText = UIFactory.CreateText(histRT, "Turn", $"Tour {entry.turn}", 10, TextAnchor.MiddleCenter, UIFactory.MutedGold);
                        turnText.fontStyle = FontStyle.Bold;
                        UIFactory.AddLayoutElement(turnText.gameObject, preferredWidth: 65, preferredHeight: 20);

                        Text descText = UIFactory.CreateText(histRT, "Desc", entry.description, 11, TextAnchor.MiddleLeft, entryColor);
                        UIFactory.AddLayoutElement(descText.gameObject, preferredWidth: 600, preferredHeight: 20);
                    }
                }
            }
            catch { }

            if (!hasHistory)
            {
                RectTransform emptyRT = UIFactory.CreatePanel(parent, "NoHist", CardBg);
                UIFactory.AddLayoutElement(emptyRT.gameObject, preferredHeight: 28);
                Text txt = UIFactory.CreateText(emptyRT, "T", "  Aucun événement diplomatique.", 12, TextAnchor.MiddleLeft, UIFactory.SilverText);
                FillAnchors(txt, 15);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════════════════════

        private void CreateSectionHeader(Transform parent, string title)
        {
            RectTransform hdrRT = UIFactory.CreateGradientPanel(parent, $"Hdr_{title.GetHashCode()}",
                SectionBg, new Color(0.10f, 0.08f, 0.05f, 0.95f));
            UIFactory.AddLayoutElement(hdrRT.gameObject, preferredHeight: 32);
            Text hdrText = UIFactory.CreateText(hdrRT, "Text", title, 14, TextAnchor.MiddleLeft, UIFactory.GoldAccent);
            hdrText.fontStyle = FontStyle.Bold;
            FillAnchors(hdrText, 15);
        }

        private void FillAnchors(Text text, float padding)
        {
            RectTransform rt = text.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, 0); rt.offsetMax = new Vector2(-padding, 0);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  DIPLOMATIC ACTIONS
        // ═══════════════════════════════════════════════════════════════════

        private void DoDeclareWar(FactionType target)
        {
            try { DiplomacySystem.Instance?.DeclareWar(playerFaction.factionType, target); } catch { }
            RefreshPanel();
        }

        private void DoProposePeace(FactionType target)
        {
            try
            {
                var terms = new PeaceTerms(playerFaction.factionType, target);
                DiplomacySystem.Instance?.ProposePeace(playerFaction.factionType, target, terms);
            }
            catch { }
            RefreshPanel();
        }

        private void DoProposeAlliance(FactionType target)
        {
            try { DiplomacySystem.Instance?.ProposeAlliance(playerFaction.factionType, target, AllianceType.Defensive); } catch { }
            RefreshPanel();
        }

        private void DoProposeTrade(FactionType target)
        {
            try { DiplomacySystem.Instance?.ProposeTradeAgreement(playerFaction.factionType, target, 50f); } catch { }
            RefreshPanel();
        }

        private void DoBreakAlliance(FactionType target)
        {
            try { DiplomacySystem.Instance?.BreakAlliance(playerFaction.factionType, target); } catch { }
            RefreshPanel();
        }

        private void DoDemandVassal(FactionType target)
        {
            try { DiplomacySystem.Instance?.DemandVassalization(playerFaction.factionType, target); } catch { }
            RefreshPanel();
        }

        private void DoNonAggression(FactionType target)
        {
            try { DiplomacySystem.Instance?.ProposeNonAggression(playerFaction.factionType, target); } catch { }
            RefreshPanel();
        }

        private void DoImproveRelations(FactionType target)
        {
            try { DiplomacySystem.Instance?.ImproveRelations(playerFaction.factionType, target); } catch { }
            RefreshPanel();
        }

        private void DoSendGift(FactionType target)
        {
            try { DiplomacySystem.Instance?.SendGift(playerFaction.factionType, target); } catch { }
            RefreshPanel();
        }

        private void DoInsult(FactionType target)
        {
            try { DiplomacySystem.Instance?.InsultFaction(playerFaction.factionType, target); } catch { }
            RefreshPanel();
        }

        private void DoGuarantee(FactionType target)
        {
            try { DiplomacySystem.Instance?.GuaranteeIndependence(playerFaction.factionType, target); } catch { }
            RefreshPanel();
        }

        private void DoRequestAccess(FactionType target)
        {
            try { DiplomacySystem.Instance?.RequestMilitaryAccess(playerFaction.factionType, target); } catch { }
            RefreshPanel();
        }

        private void DoAcceptOffer(string offerId)
        {
            try { DiplomacySystem.Instance?.AcceptOffer(offerId); } catch { }
            RefreshPanel();
        }

        private void DoRejectOffer(string offerId)
        {
            try { DiplomacySystem.Instance?.RejectOffer(offerId); } catch { }
            RefreshPanel();
        }

        private void RefreshPanel()
        {
            navBar.TogglePanel(NavigationBar.NavPanel.Diplomacy);
            navBar.TogglePanel(NavigationBar.NavPanel.Diplomacy);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  LOCALIZATION
        // ═══════════════════════════════════════════════════════════════════

        private static string GetStateFrench(DiplomacyState state) => state switch
        {
            DiplomacyState.War => "EN GUERRE",
            DiplomacyState.Hostile => "Hostile",
            DiplomacyState.Neutral => "Neutre",
            DiplomacyState.Friendly => "Amical",
            DiplomacyState.Alliance => "Allié",
            _ => state.ToString()
        };

        private static string GetActionNameFrench(DiplomaticActionType action) => action switch
        {
            DiplomaticActionType.ProposePeace => "Traité de paix",
            DiplomaticActionType.ProposeAlliance => "Alliance",
            DiplomaticActionType.RequestMilitaryAccess => "Accès militaire",
            DiplomaticActionType.ProposeTradeAgreement => "Accord commercial",
            DiplomaticActionType.DemandVassalization => "Vassalisation",
            DiplomaticActionType.ProposeNonAggression => "Pacte de non-agression",
            DiplomaticActionType.ImproveRelations => "Amélioration des relations",
            DiplomaticActionType.SendGift => "Cadeau diplomatique",
            DiplomaticActionType.InsultFaction => "Insulte",
            DiplomaticActionType.GuaranteeIndependence => "Garantie d'indépendance",
            _ => action.ToString()
        };

        private static string GetTreatyTypeFrench(TreatyType type) => type switch
        {
            TreatyType.Alliance => "Alliance",
            TreatyType.MilitaryAccess => "Accès militaire",
            TreatyType.TradeAgreement => "Accord commercial",
            TreatyType.Vassalage => "Vassalité",
            TreatyType.NonAggression => "Non-agression",
            TreatyType.Marriage => "Mariage royal",
            TreatyType.Guarantee => "Garantie d'indépendance",
            _ => type.ToString()
        };

        private static Color GetRelationColor(DiplomacyState state) => state switch
        {
            DiplomacyState.War => new Color(0.9f, 0.2f, 0.2f),
            DiplomacyState.Hostile => new Color(0.8f, 0.4f, 0.2f),
            DiplomacyState.Neutral => new Color(0.7f, 0.7f, 0.7f),
            DiplomacyState.Friendly => new Color(0.4f, 0.7f, 0.4f),
            DiplomacyState.Alliance => new Color(0.3f, 0.6f, 0.9f),
            _ => Color.white
        };
    }
}
