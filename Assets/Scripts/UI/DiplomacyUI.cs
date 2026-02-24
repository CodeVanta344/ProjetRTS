using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NapoleonicWars.Campaign;
using NapoleonicWars.Data;

namespace NapoleonicWars.UI
{
    /// <summary>
    /// HoI4-style Diplomacy overlay panel — full-screen, accessible via NavigationBar.
    /// Shows all factions with relations, treaties, pending offers, and diplomatic actions.
    /// </summary>
    public class DiplomacyUI : MonoBehaviour
    {
        private CampaignManager campaignManager;
        private FactionData playerFaction;
        private NavigationBar navBar;

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

        private void BuildContent(Transform parent)
        {
            var (scroll, content) = UIFactory.CreateScrollView(parent, "DiplomacyScroll");
            UIFactory.AddLayoutElement(scroll.gameObject, flexibleHeight: 1, preferredHeight: 900);

            // === PLAYER SUMMARY ===
            RectTransform summaryRT = UIFactory.CreatePanel(content, "Summary", new Color(0.10f, 0.07f, 0.05f, 0.95f));
            UIFactory.AddLayoutElement(summaryRT.gameObject, preferredHeight: 40);
            Text summaryText = UIFactory.CreateText(summaryRT, "Text",
                $"Pouvoir politique: {playerFaction.politicalPower:F0} PP  |  Stabilité: {(int)(playerFaction.stability * 100)}%  |  Soutien de guerre: {(int)(playerFaction.warSupport * 100)}%",
                14, TextAnchor.MiddleCenter, UIFactory.GoldAccent);
            summaryText.fontStyle = FontStyle.Bold;
            FillAnchors(summaryText, 15);

            // === PENDING OFFERS ===
            List<DiplomaticOffer> incomingOffers = new List<DiplomaticOffer>();
            try
            {
                if (DiplomacySystem.Instance != null)
                {
                    foreach (var offer in DiplomacySystem.Instance.PendingOffers)
                        if (offer.toFaction == playerFaction.factionType) incomingOffers.Add(offer);
                }
            }
            catch { }

            if (incomingOffers.Count > 0)
            {
                CreateDiploSection(content, $"Offres diplomatiques en attente ({incomingOffers.Count})");
                foreach (var offer in incomingOffers)
                    CreateOfferCard(content, offer);
            }

            // === ACTIVE TREATIES ===
            CreateDiploSection(content, "Traités actifs");
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
                        CreateTreatyRow(content, treaty);
                    }
                }
            }
            catch { }
            if (!hasTreaties)
            {
                Text noTreaty = UIFactory.CreateText(content, "NoTreaty", "  Aucun traité actif.",
                    12, TextAnchor.MiddleLeft, UIFactory.TextGrey);
                UIFactory.AddLayoutElement(noTreaty.gameObject, preferredHeight: 25);
            }

            // === ALL FACTIONS TABLE ===
            CreateDiploSection(content, "Relations avec les nations");

            // Table header
            RectTransform headerRT = UIFactory.CreatePanel(content, "TblHeader", new Color(0.16f, 0.18f, 0.15f, 0.95f));
            UIFactory.AddLayoutElement(headerRT.gameObject, preferredHeight: 28);
            HorizontalLayoutGroup hdrHLG = UIFactory.AddHorizontalLayout(headerRT.gameObject, 4f, new RectOffset(15, 15, 2, 2));
            hdrHLG.childControlWidth = false; hdrHLG.childControlHeight = true;
            AddTblHeader(headerRT, "Nation", 200);
            AddTblHeader(headerRT, "Statut", 100);
            AddTblHeader(headerRT, "Score", 60);
            AddTblHeader(headerRT, "Provinces", 70);
            AddTblHeader(headerRT, "Armées", 60);
            AddTblHeader(headerRT, "Actions", 300);

            // Faction rows
            if (campaignManager != null)
            {
                foreach (var kvp in campaignManager.Factions)
                {
                    if (kvp.Key == playerFaction.factionType) continue;
                    if (kvp.Value.isEliminated) continue;
                    CreateFactionRow(content, kvp.Key, kvp.Value);
                }
            }

            // === DIPLOMATIC HISTORY ===
            CreateDiploSection(content, "Historique diplomatique récent");
            bool hasHistory = false;
            try
            {
                if (DiplomacySystem.Instance != null)
                {
                    var history = DiplomacySystem.Instance.GetHistory(playerFaction.factionType, 10);
                    foreach (var entry in history)
                    {
                        hasHistory = true;
                        Text histLine = UIFactory.CreateText(content, $"Hist_{entry.turn}",
                            $"  Tour {entry.turn}: {entry.description}",
                            11, TextAnchor.MiddleLeft, UIFactory.ParchmentBeige);
                        UIFactory.AddLayoutElement(histLine.gameObject, preferredHeight: 22);
                    }
                }
            }
            catch { }
            if (!hasHistory)
            {
                Text noHist = UIFactory.CreateText(content, "NoHist", "  Aucun événement diplomatique.",
                    12, TextAnchor.MiddleLeft, UIFactory.TextGrey);
                UIFactory.AddLayoutElement(noHist.gameObject, preferredHeight: 25);
            }
        }

        // ================================ UI HELPERS ================================

        private void CreateDiploSection(Transform parent, string title)
        {
            RectTransform hdrRT = UIFactory.CreatePanel(parent, $"Hdr_{title.GetHashCode()}",
                new Color(0.16f, 0.18f, 0.15f, 0.95f));
            UIFactory.AddLayoutElement(hdrRT.gameObject, preferredHeight: 28);
            Text hdrText = UIFactory.CreateText(hdrRT, "Text", title, 13,
                TextAnchor.MiddleLeft, UIFactory.GoldAccent);
            hdrText.fontStyle = FontStyle.Bold;
            FillAnchors(hdrText, 15);
        }

        private void AddTblHeader(Transform parent, string label, float width)
        {
            Text text = UIFactory.CreateText(parent, "H_" + label, label, 11, TextAnchor.MiddleCenter, UIFactory.GoldAccent);
            text.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(text.gameObject, preferredWidth: width, preferredHeight: 24);
        }

        private void FillAnchors(Text text, float padding)
        {
            RectTransform rt = text.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, 0); rt.offsetMax = new Vector2(-padding, 0);
        }

        private void CreateFactionRow(Transform parent, FactionType fType, FactionData faction)
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

            Color rowBg = state == DiplomacyState.War ? new Color(0.18f, 0.06f, 0.06f, 0.90f) :
                          state == DiplomacyState.Alliance ? new Color(0.06f, 0.15f, 0.06f, 0.90f) :
                          new Color(0.12f, 0.13f, 0.11f, 0.90f);

            RectTransform rowRT = UIFactory.CreatePanel(parent, $"Row_{fType}", rowBg);
            UIFactory.AddLayoutElement(rowRT.gameObject, preferredHeight: 38);
            HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(rowRT.gameObject, 4f, new RectOffset(15, 15, 3, 3));
            hlg.childControlWidth = false; hlg.childControlHeight = true;

            // Name
            string name = CampaignManager.GetFactionSubtitle(fType);
            Text nameText = UIFactory.CreateText(rowRT, "Name", name, 12, TextAnchor.MiddleLeft, UIFactory.TextWhite);
            nameText.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(nameText.gameObject, preferredWidth: 200, preferredHeight: 32);

            // State
            Color stateColor = GetRelationColor(state);
            string stateStr = GetStateFrench(state);
            Text stateText = UIFactory.CreateText(rowRT, "State", stateStr, 11, TextAnchor.MiddleCenter, stateColor);
            stateText.fontStyle = FontStyle.Bold;
            UIFactory.AddLayoutElement(stateText.gameObject, preferredWidth: 100, preferredHeight: 32);

            // Score
            string scoreSign = score >= 0 ? "+" : "";
            Color scoreColor = score > 0 ? new Color(0.4f, 0.8f, 0.4f) : score < 0 ? new Color(0.8f, 0.4f, 0.4f) : UIFactory.TextGrey;
            Text scoreText = UIFactory.CreateText(rowRT, "Score", $"{scoreSign}{score}", 12, TextAnchor.MiddleCenter, scoreColor);
            UIFactory.AddLayoutElement(scoreText.gameObject, preferredWidth: 60, preferredHeight: 32);

            // Provinces
            Text provText = UIFactory.CreateText(rowRT, "Prov", $"{faction.ownedProvinceIds.Count}", 12, TextAnchor.MiddleCenter, UIFactory.ParchmentBeige);
            UIFactory.AddLayoutElement(provText.gameObject, preferredWidth: 70, preferredHeight: 32);

            // Armies
            int armyCount = 0;
            if (campaignManager != null && campaignManager.Armies != null)
                foreach (var a in campaignManager.Armies.Values) if (a.faction == fType) armyCount++;
            Text armyText = UIFactory.CreateText(rowRT, "Army", $"{armyCount}", 12, TextAnchor.MiddleCenter, UIFactory.ParchmentBeige);
            UIFactory.AddLayoutElement(armyText.gameObject, preferredWidth: 60, preferredHeight: 32);

            // Action buttons
            CreateActionButtons(rowRT, fType, state);
        }

        private void CreateActionButtons(Transform parent, FactionType target, DiplomacyState state)
        {
            if (state == DiplomacyState.War)
            {
                Button peaceBtn = UIFactory.CreateButton(parent, "Peace", "Proposer la paix", 10,
                    () => DoProposePeace(target));
                UIFactory.AddLayoutElement(peaceBtn.gameObject, preferredWidth: 130, preferredHeight: 28);

                Button vassalBtn = UIFactory.CreateButton(parent, "Vassal", "Exiger vassalité", 10,
                    () => DoDemandVassal(target));
                UIFactory.AddLayoutElement(vassalBtn.gameObject, preferredWidth: 130, preferredHeight: 28);
            }
            else if (state == DiplomacyState.Alliance)
            {
                Button breakBtn = UIFactory.CreateButton(parent, "Break", "Rompre alliance", 10,
                    () => DoBreakAlliance(target));
                UIFactory.AddLayoutElement(breakBtn.gameObject, preferredWidth: 130, preferredHeight: 28);
                breakBtn.GetComponent<Image>().color = new Color(0.5f, 0.2f, 0.2f);
            }
            else
            {
                // Declare War
                Button warBtn = UIFactory.CreateButton(parent, "War", "Déclarer guerre", 10,
                    () => DoDeclareWar(target));
                UIFactory.AddLayoutElement(warBtn.gameObject, preferredWidth: 120, preferredHeight: 28);
                warBtn.GetComponent<Image>().color = new Color(0.5f, 0.15f, 0.15f);

                // Alliance
                bool hasAlliance = false;
                try { hasAlliance = DiplomacySystem.Instance?.HasTreaty(playerFaction.factionType, target, TreatyType.Alliance) ?? false; } catch { }
                if (!hasAlliance)
                {
                    Button allyBtn = UIFactory.CreateButton(parent, "Ally", "Alliance", 10,
                        () => DoProposeAlliance(target));
                    UIFactory.AddLayoutElement(allyBtn.gameObject, preferredWidth: 80, preferredHeight: 28);
                }

                // Trade
                bool hasTrade = false;
                try { hasTrade = DiplomacySystem.Instance?.HasTreaty(playerFaction.factionType, target, TreatyType.TradeAgreement) ?? false; } catch { }
                if (!hasTrade)
                {
                    Button tradeBtn = UIFactory.CreateButton(parent, "Trade", "Commerce", 10,
                        () => DoProposeTrade(target));
                    UIFactory.AddLayoutElement(tradeBtn.gameObject, preferredWidth: 80, preferredHeight: 28);
                }
            }
        }

        private void CreateOfferCard(Transform parent, DiplomaticOffer offer)
        {
            RectTransform cardRT = UIFactory.CreatePanel(parent, $"Offer_{offer.offerId}",
                new Color(0.12f, 0.10f, 0.08f, 0.95f));
            UIFactory.AddLayoutElement(cardRT.gameObject, preferredHeight: 50);
            HorizontalLayoutGroup hlg = UIFactory.AddHorizontalLayout(cardRT.gameObject, 8f, new RectOffset(15, 15, 6, 6));
            hlg.childControlWidth = false; hlg.childControlHeight = true;

            string fromName = CampaignManager.GetFactionSubtitle(offer.fromFaction);
            string actionName = GetActionNameFrench(offer.actionType);
            Text offerText = UIFactory.CreateText(cardRT, "Text",
                $"{fromName} propose: {actionName}", 12, TextAnchor.MiddleLeft, UIFactory.ParchmentBeige);
            UIFactory.AddLayoutElement(offerText.gameObject, preferredWidth: 400, preferredHeight: 36);

            string capturedId = offer.offerId;
            Button acceptBtn = UIFactory.CreateButton(cardRT, "Accept", "Accepter", 11,
                () => DoAcceptOffer(capturedId));
            UIFactory.AddLayoutElement(acceptBtn.gameObject, preferredWidth: 90, preferredHeight: 30);

            Button rejectBtn = UIFactory.CreateButton(cardRT, "Reject", "Refuser", 11,
                () => DoRejectOffer(capturedId));
            UIFactory.AddLayoutElement(rejectBtn.gameObject, preferredWidth: 80, preferredHeight: 30);
            rejectBtn.GetComponent<Image>().color = new Color(0.4f, 0.15f, 0.15f);
        }

        private void CreateTreatyRow(Transform parent, Treaty treaty)
        {
            FactionType other = FactionType.France;
            foreach (var f in treaty.factions) if (f != playerFaction.factionType) other = f;

            string otherName = CampaignManager.GetFactionSubtitle(other);
            string typeStr = GetTreatyTypeFrench(treaty.type);
            string durationStr = treaty.duration > 0 ? $" ({treaty.duration} tours)" : " (permanent)";

            RectTransform rowRT = UIFactory.CreatePanel(parent, $"Treaty_{treaty.treatyId}",
                new Color(0.08f, 0.12f, 0.08f, 0.90f));
            UIFactory.AddLayoutElement(rowRT.gameObject, preferredHeight: 30);
            Text treatyText = UIFactory.CreateText(rowRT, "Text",
                $"  {typeStr} avec {otherName}{durationStr}",
                12, TextAnchor.MiddleLeft, new Color(0.5f, 0.9f, 0.5f));
            FillAnchors(treatyText, 10);
        }

        // ================================ DIPLOMATIC ACTIONS ================================

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

        // ================================ LOCALIZATION ================================

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
            _ => type.ToString()
        };

        private static Color GetRelationColor(DiplomacyState state) => state switch
        {
            DiplomacyState.War => new Color(0.9f, 0.2f, 0.2f),
            DiplomacyState.Hostile => new Color(0.8f, 0.4f, 0.2f),
            DiplomacyState.Neutral => new Color(0.7f, 0.7f, 0.7f),
            DiplomacyState.Friendly => new Color(0.4f, 0.7f, 0.4f),
            DiplomacyState.Alliance => new Color(0.2f, 0.8f, 0.2f),
            _ => Color.white
        };
    }
}
